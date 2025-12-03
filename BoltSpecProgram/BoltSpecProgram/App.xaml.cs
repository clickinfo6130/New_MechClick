using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using BoltSpecProgram;
using Common.Protocol;

namespace BoltSpecProgram
{
    public partial class App : Application
    {
        private IpcServer _ipcServer;

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 콘솔 창 표시 (디버깅용)
            AllocConsole();
            Console.WriteLine("=== CAD UI Started ===");

            // IPC 서버 시작
            _ipcServer = new IpcServer();
            _ipcServer.DialogRequested += OnDialogRequested;

            await _ipcServer.StartAsync();

            Console.WriteLine("IPC Server started successfully");
        }

        private async void OnDialogRequested(object sender, ShowDialogRequest request)
        {
            Console.WriteLine($"Dialog requested: {request.DialogType}");

            await Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    // 메인 윈도우가 없으면 생성
                    if (MainWindow == null)
                    {
                        MainWindow = new MainWindow();
                    }

                    // 창이 숨겨져 있으면 표시
                    if (!MainWindow.IsVisible)
                    {
                        MainWindow.Show();
                    }

                    // 다이얼로그 처리
                    DialogResponse response = await HandleDialogRequest(request);

                    // MessageId 설정
                    response.MessageId = request.MessageId;

                    // AutoClose 플래그 확인
                    bool autoClose = false;
                    if (request.Context != null &&
                        request.Context.ContainsKey("AutoClose"))
                    {
                        autoClose = Convert.ToBoolean(request.Context["AutoClose"]);
                    }

                    // SelectedData가 null이면 초기화
                    if (response.SelectedData == null)
                    {
                        response.SelectedData = new Dictionary<string, object>();
                    }

                    if (autoClose)
                    {
                        response.SelectedData["UiWillClose"] = true;
                    }

                    Console.WriteLine($"Sending response: IsOk={response.IsOk}, AutoClose={autoClose}");

                    // 응답 설정
                    _ipcServer.SetDialogResponse(response);

                    // 자동 종료
                    if (autoClose)
                    {
                        Console.WriteLine("Auto-closing UI in 1 second...");
                        await Task.Delay(1000);
                        Shutdown();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"OnDialogRequested error: {ex.Message}");
                    Console.WriteLine($"StackTrace: {ex.StackTrace}");

                    // 에러 응답
                    _ipcServer.SetDialogResponse(new DialogResponse
                    {
                        MessageId = request.MessageId,
                        IsOk = false
                    });
                }
            });
        }

        private async Task<DialogResponse> HandleDialogRequest(ShowDialogRequest request)
        {
            Console.WriteLine($"Handling dialog type: {request.DialogType}");

            switch (request.DialogType)
            {
                case "SpecSelection":
                    return await ShowSpecSelectionDialog(request);

                case "TestDialog":
                    return await ShowTestDialog(request);

                default:
                    Console.WriteLine($"Unknown dialog type: {request.DialogType}");
                    return new DialogResponse { IsOk = false };
            }
        }

        private async Task<DialogResponse> ShowTestDialog(ShowDialogRequest request)
        {
            Console.WriteLine("Showing test dialog...");

            var mainWindow = MainWindow as MainWindow;
            if (mainWindow == null)
            {
                Console.WriteLine("ERROR: MainWindow is null");
                return new DialogResponse { IsOk = false };
            }

            // TaskCompletionSource로 응답 대기
            var tcs = new TaskCompletionSource<DialogResponse>();

            mainWindow.WaitForUserResponse((isOk, data) =>
            {
                Console.WriteLine($"User response received: IsOk={isOk}");

                var response = new DialogResponse
                {
                    IsOk = isOk
                };

                if (data != null)
                {
                    response.SelectedData = data;
                }
                else
                {
                    response.SelectedData = new Dictionary<string, object>();
                }

                tcs.SetResult(response);
            });

            return await tcs.Task;
        }

        private Task<DialogResponse> ShowSpecSelectionDialog(ShowDialogRequest request)
        {
            // 향후 별도 다이얼로그 구현
            // 현재는 TestDialog 방식 사용
            return ShowTestDialog(request);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Console.WriteLine("Application exiting...");
            _ipcServer?.Stop();
            base.OnExit(e);
        }
    }
}