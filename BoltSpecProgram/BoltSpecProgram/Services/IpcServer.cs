using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Protocol;
using Newtonsoft.Json;  // 변경

namespace BoltSpecProgram
{
    public class IpcServer
    {
        private CancellationTokenSource _cts;
        private bool _isRunning;
        private TaskCompletionSource<DialogResponse> _currentResponseTcs;

        public event EventHandler<ShowDialogRequest> DialogRequested;

        public async Task StartAsync()
        {
            _cts = new CancellationTokenSource();
            _isRunning = true;

            await Task.Run(() => ServerLoop(_cts.Token));
        }

        private async void ServerLoop(CancellationToken token)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Server loop started");

            while (_isRunning && !token.IsCancellationRequested)
            {
                NamedPipeServerStream pipeServer = null;

                try
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Creating pipe server...");

                    pipeServer = new NamedPipeServerStream(
                        "AutoCAD_IPC",
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous);

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Waiting for CAD connection...");

                    await pipeServer.WaitForConnectionAsync(token);

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] CAD connected!");

                    await HandleClient(pipeServer, token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Server loop cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Server error: {ex.Message}");
                    Console.WriteLine($"StackTrace: {ex.StackTrace}");
                }
                finally
                {
                    if (pipeServer != null)
                    {
                        try
                        {
                            if (pipeServer.IsConnected)
                            {
                                pipeServer.Disconnect();
                            }
                        }
                        catch { }

                        pipeServer.Dispose();
                    }
                }

                if (_isRunning && !token.IsCancellationRequested)
                {
                    await Task.Delay(100, token);
                }
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Server loop ended");
        }

        private async Task HandleClient(NamedPipeServerStream pipeServer, CancellationToken token)
        {
            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] HandleClient started");

                // 요청 수신
                string requestJson = await ReadMessageAsync(pipeServer);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Received: {requestJson}");

                if (string.IsNullOrEmpty(requestJson))
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Empty request received");
                    return;
                }

                // === Newtonsoft.Json 사용 ===
                ShowDialogRequest request;
                try
                {
                    request = JsonConvert.DeserializeObject<ShowDialogRequest>(requestJson);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Request parsed: MessageId={request.MessageId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] JSON Deserialization error: {ex.Message}");
                    return;
                }

                _currentResponseTcs = new TaskCompletionSource<DialogResponse>();

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Invoking DialogRequested event");
                DialogRequested?.Invoke(this, request);

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Waiting for UI response...");

                var timeoutTask = Task.Delay(60000, token);
                var completedTask = await Task.WhenAny(_currentResponseTcs.Task, timeoutTask);

                DialogResponse dialogResponse;

                if (completedTask == timeoutTask)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Response timeout!");
                    dialogResponse = new DialogResponse
                    {
                        MessageId = request.MessageId,
                        IsOk = false
                    };
                }
                else
                {
                    dialogResponse = await _currentResponseTcs.Task;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Response received from UI");
                }

                await SendResponseAsync(pipeServer, dialogResponse);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Response sent to CAD");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] HandleClient error: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }
        }

        public void SetDialogResponse(DialogResponse response)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] SetDialogResponse called: IsOk={response.IsOk}");
            _currentResponseTcs?.TrySetResult(response);
        }

        private async Task<string> ReadMessageAsync(NamedPipeServerStream pipe)
        {
            try
            {
                using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true))
                {
                    string line = await reader.ReadLineAsync();
                    return line?.Trim();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ReadMessage error: {ex.Message}");
                return null;
            }
        }

        private async Task SendResponseAsync(NamedPipeServerStream pipe, DialogResponse response)
        {
            try
            {
                // === Newtonsoft.Json 사용 ===
                string json = JsonConvert.SerializeObject(response);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Sending: {json}");

                using (var writer = new StreamWriter(pipe, Encoding.UTF8, 4096, true))
                {
                    await writer.WriteLineAsync(json);
                    await writer.FlushAsync();
                }

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Response sent successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendResponse error: {ex.Message}");
                throw;
            }
        }

        public void Stop()
        {
            Console.WriteLine("Stopping IPC server...");
            _isRunning = false;
            _cts?.Cancel();
        }
    }
}