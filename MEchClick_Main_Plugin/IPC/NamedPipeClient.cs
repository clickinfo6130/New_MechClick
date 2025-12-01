using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PartManager.IPC
{
    public class NamedPipeClient : IDisposable
    {
        private NamedPipeClientStream _pipeClient;
        private StreamReader _reader;
        private StreamWriter _writer;
        private readonly string _pipeName;
        private bool _isConnected;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _receiveTask;

        public event EventHandler<string> MessageReceived;
        public event EventHandler<bool> ConnectionStatusChanged;

        public bool IsConnected => _isConnected && _pipeClient?.IsConnected == true;

        public NamedPipeClient(string pipeName)
        {
            _pipeName = pipeName;
        }

        /// <summary>
        /// 서버에 연결
        /// </summary>
        public async Task<bool> ConnectAsync(int timeoutMs = 5000)
        {
            try
            {
                _pipeClient = new NamedPipeClientStream(
                    ".",
                    _pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await _pipeClient.ConnectAsync(timeoutMs);

                _reader = new StreamReader(_pipeClient, Encoding.UTF8);
                _writer = new StreamWriter(_pipeClient, Encoding.UTF8) { AutoFlush = true };

                _isConnected = true;
                ConnectionStatusChanged?.Invoke(this, true);

                // 수신 시작
                _cancellationTokenSource = new CancellationTokenSource();
                _receiveTask = Task.Run(() => ReceiveMessages(_cancellationTokenSource.Token));

                System.Diagnostics.Debug.WriteLine($"[IPC Client] 연결 성공: {_pipeName}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IPC Client] 연결 실패: {ex.Message}");
                _isConnected = false;
                ConnectionStatusChanged?.Invoke(this, false);
                return false;
            }
        }

        /// <summary>
        /// 동기 연결 (호환성용)
        /// </summary>
        public bool Connect(int timeoutMs = 5000)
        {
            return ConnectAsync(timeoutMs).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 메시지 전송
        /// </summary>
        public async Task<bool> SendMessageAsync(string message)
        {
            if (!IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("[IPC Client] 연결되지 않음");
                return false;
            }

            try
            {
                await _writer.WriteLineAsync(message);
                System.Diagnostics.Debug.WriteLine($"[IPC Client] 전송: {message}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IPC Client] 전송 실패: {ex.Message}");
                _isConnected = false;
                ConnectionStatusChanged?.Invoke(this, false);
                return false;
            }
        }

        /// <summary>
        /// 동기 전송 (호환성용)
        /// </summary>
        public bool SendMessage(string message)
        {
            return SendMessageAsync(message).GetAwaiter().GetResult();
        }

        /// <summary>
        /// IPCMessage 객체 전송
        /// </summary>
        public async Task<bool> SendCommandAsync(IPCMessage command)
        {
            try
            {
                string json = JsonConvert.SerializeObject(command);
                return await SendMessageAsync(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IPC Client] JSON 직렬화 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 메시지 수신 루프
        /// </summary>
        private async Task ReceiveMessages(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && IsConnected)
                {
                    string message = await _reader.ReadLineAsync();

                    if (string.IsNullOrEmpty(message))
                    {
                        // 연결 끊김
                        break;
                    }

                    System.Diagnostics.Debug.WriteLine($"[IPC Client] 수신: {message}");
                    MessageReceived?.Invoke(this, message);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IPC Client] 수신 오류: {ex.Message}");
            }
            finally
            {
                _isConnected = false;
                ConnectionStatusChanged?.Invoke(this, false);
            }
        }

        /// <summary>
        /// 연결 해제
        /// </summary>
        public void Disconnect()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _receiveTask?.Wait(1000);

                _reader?.Dispose();
                _writer?.Dispose();
                _pipeClient?.Dispose();

                _isConnected = false;
                ConnectionStatusChanged?.Invoke(this, false);

                System.Diagnostics.Debug.WriteLine("[IPC Client] 연결 해제");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IPC Client] 연결 해제 오류: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
