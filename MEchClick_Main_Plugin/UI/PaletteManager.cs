using Autodesk.AutoCAD.Windows;
using System;
using System.Drawing;
using System.Windows.Forms.Integration;
using PartManager.IPC;
using PartManager.Views;
using Newtonsoft.Json;

namespace PartManager.UI
{
    public class PaletteManager
    {
        private static PaletteSet _paletteSet;
        private static CategorySelectorPanel _mainPanel;
        private static ElementHost _elementHost;
        private static bool _isInitialized = false;
        private static NamedPipeClient _ipcClient;
        private const string PIPE_NAME = "CAD_IPC_Pipe";

        private static System.Timers.Timer _monitorTimer;
        private static DockSides _lastDockPosition;
        private static Point _lastLocation;
        private static Size _lastFloatingSize;
        private static Size _lastDockedSize;
        private static bool _lastVisibleState;

        #region Public Methods

        public static bool GetIPCStatus()
        {
            return _ipcClient?.IsConnected ?? false;
        }

        public static bool ReconnectIPC()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[IPC] 재연결 시도...");

                if (_ipcClient != null)
                {
                    _ipcClient.Disconnect();
                    _ipcClient.Dispose();
                    _ipcClient = null;
                }

                InitializeIPC();
                System.Threading.Thread.Sleep(500);

                bool connected = _ipcClient?.IsConnected ?? false;
                System.Diagnostics.Debug.WriteLine($"[IPC] 재연결 결과: {connected}");

                return connected;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IPC] 재연결 실패: {ex.Message}");
                return false;
            }
        }

        public static void Initialize()
        {
            if (_isInitialized)
                return;

            try
            {
                CreatePalette();
                InitializeIPC();
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"UI 초기화 실패: {ex.Message}");
            }
        }

        public static void Show()
        {
            if (_paletteSet == null)
                Initialize();

            _paletteSet.Visible = true;
            _paletteSet.Activate(0);
            _paletteSet.KeepFocus = true;
        }

        public static void Hide()
        {
            if (_paletteSet != null)
            {
                _paletteSet.Visible = false;
            }
        }

        public static void Toggle()
        {
            if (_paletteSet == null)
            {
                Initialize();
                Show();
            }
            else
            {
                _paletteSet.Visible = !_paletteSet.Visible;
                if (_paletteSet.Visible)
                {
                    _paletteSet.Activate(0);
                    _paletteSet.KeepFocus = true;
                }
            }
        }

        public static bool IsVisible => _paletteSet?.Visible ?? false;

        public static void SetDockPosition(DockSides position)
        {
            if (_paletteSet != null)
            {
                _paletteSet.Dock = position;

                if (position == DockSides.None)
                {
                    _paletteSet.Size = _lastFloatingSize;
                }

                SaveCurrentSettings();
            }
        }

        public static void Cleanup()
        {
            SaveCurrentSettings();

            if (_monitorTimer != null)
            {
                _monitorTimer.Stop();
                _monitorTimer.Dispose();
                _monitorTimer = null;
            }

            _ipcClient?.Disconnect();
            _ipcClient?.Dispose();

            if (_mainPanel != null)
                _mainPanel.PartSelected -= OnPartSelected;

            if (_paletteSet != null)
            {
                _paletteSet.StateChanged -= OnPaletteStateChanged;
                _paletteSet.PaletteActivated -= OnPaletteActivated;

                _paletteSet.Visible = false;
                _paletteSet.Dispose();
                _paletteSet = null;
            }

            _elementHost?.Dispose();
            _elementHost = null;
            _mainPanel = null;
            _isInitialized = false;
        }

        #endregion

        #region Private Methods

        private static void CreatePalette()
        {
            System.Diagnostics.Debug.WriteLine("[Palette] CreatePalette 시작");

            var settings = PaletteSettings.Load();

            Size initialSize = settings.DockPosition == DockSides.None
                ? settings.FloatingSize
                : settings.DockedSize;

            _paletteSet = new PaletteSet("Part Manager")
            {
                Size = initialSize,
                MinimumSize = new Size(350, 500),

                DockEnabled = DockSides.Left | DockSides.Right |
                             DockSides.Top | DockSides.Bottom | DockSides.None,

                Style = PaletteSetStyles.ShowPropertiesMenu |
                        PaletteSetStyles.ShowAutoHideButton |
                        PaletteSetStyles.ShowCloseButton,

                Opacity = 100
            };

            // WPF 패널 생성
            _mainPanel = new CategorySelectorPanel();
            _elementHost = new ElementHost
            {
                AutoSize = true,
                Dock = System.Windows.Forms.DockStyle.Fill,
                Child = _mainPanel
            };

            _paletteSet.Add("부품 선택", _elementHost);
            _mainPanel.PartSelected += OnPartSelected;

            RegisterEvents();

            // 도킹 위치 설정
            _paletteSet.Dock = settings.DockPosition;

            if (settings.DockPosition == DockSides.None)
            {
                _paletteSet.Location = settings.Location;
                System.Threading.Thread.Sleep(100);

                if (_paletteSet.Size != initialSize)
                {
                    _paletteSet.Size = initialSize;
                }
            }

            _paletteSet.Visible = settings.Visible;

            // 상태 저장
            _lastDockPosition = _paletteSet.Dock;
            _lastLocation = _paletteSet.Location;
            _lastFloatingSize = settings.FloatingSize;
            _lastDockedSize = settings.DockedSize;
            _lastVisibleState = _paletteSet.Visible;

            StartMonitoring();
            System.Diagnostics.Debug.WriteLine("[Palette] CreatePalette 완료");
        }

        private static void StartMonitoring()
        {
            if (_monitorTimer != null)
                return;

            _monitorTimer = new System.Timers.Timer(1000);
            _monitorTimer.Elapsed += (sender, e) => CheckAndSaveChanges();
            _monitorTimer.AutoReset = true;
            _monitorTimer.Start();
        }

        private static void CheckAndSaveChanges()
        {
            if (_paletteSet == null)
                return;

            try
            {
                DockSides currentDock = CleanDockPosition(_paletteSet.Dock);
                Size currentSize = _paletteSet.Size;
                Point currentLocation = _paletteSet.Location;
                bool currentVisible = _paletteSet.Visible;

                bool hasChanged = false;

                if (currentDock != _lastDockPosition)
                {
                    if (_lastDockPosition == DockSides.None && currentDock != DockSides.None)
                    {
                        _lastFloatingSize = currentSize;
                    }
                    else if (_lastDockPosition != DockSides.None && currentDock == DockSides.None)
                    {
                        _lastDockedSize = currentSize;
                    }

                    _lastDockPosition = currentDock;
                    hasChanged = true;
                }

                if (currentDock == DockSides.None)
                {
                    if (currentLocation != _lastLocation)
                    {
                        _lastLocation = currentLocation;
                        hasChanged = true;
                    }

                    if (currentSize != _lastFloatingSize)
                    {
                        _lastFloatingSize = currentSize;
                        hasChanged = true;
                    }
                }
                else
                {
                    if (currentSize != _lastDockedSize)
                    {
                        _lastDockedSize = currentSize;
                        hasChanged = true;
                    }
                }

                if (currentVisible != _lastVisibleState)
                {
                    _lastVisibleState = currentVisible;
                    hasChanged = true;
                }

                if (hasChanged)
                {
                    SaveCurrentSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Monitor] 오류: {ex.Message}");
            }
        }

        private static DockSides CleanDockPosition(DockSides dock)
        {
            if (dock.HasFlag(DockSides.Left)) return DockSides.Left;
            if (dock.HasFlag(DockSides.Right)) return DockSides.Right;
            if (dock.HasFlag(DockSides.Top)) return DockSides.Top;
            if (dock.HasFlag(DockSides.Bottom)) return DockSides.Bottom;
            return DockSides.Left;// None;
        }

        private static void SaveCurrentSettings()
        {
            if (_paletteSet == null)
                return;

            try
            {
                var settings = new PaletteSettings
                {
                    DockPosition = _paletteSet.Dock,
                    Location = _paletteSet.Location,
                    FloatingSize = _lastFloatingSize,
                    DockedSize = _lastDockedSize,
                    Visible = _paletteSet.Visible
                };

                settings.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Palette] 설정 저장 실패: {ex.Message}");
            }
        }

        private static void RegisterEvents()
        {
            _paletteSet.StateChanged += OnPaletteStateChanged;
            _paletteSet.PaletteActivated += OnPaletteActivated;
        }

        private static void OnPaletteStateChanged(object sender, PaletteSetStateEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Palette state: {e.NewState}");
        }

        private static void OnPaletteActivated(object sender, PaletteActivatedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Palette activated: {e.Activated}");
        }

        /// <summary>
        /// 부품 선택 완료 시 C++로 데이터 전송
        /// </summary>
        private static async void OnPartSelected(object sender, PartSelectedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[Part] 선택됨: {e.PartCode}");

            // IPC로 C++에 전송
            if (_ipcClient == null || !_ipcClient.IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("[IPC] C++ 서버에 연결되지 않음");
                _mainPanel?.Dispatcher.Invoke(() =>
                {
                    _mainPanel.ShowMessage("C++ 서버에 연결되지 않음. IPCRECONNECT 명령으로 재시도하세요.");
                });
                return;
            }

            try
            {
                var message = new IPCMessage
                {
                    Command = "INSERT_PART",
                    Parameters = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "MainCategory", e.MainCategory },
                        { "SubCategory", e.SubCategory },
                        { "MidCategory", e.MidCategory },
                        { "PartType", e.PartType },
                        { "Series", e.Series },
                        { "PartCode", e.PartCode }
                    }
                };

                string json = JsonConvert.SerializeObject(message);
                bool success = await _ipcClient.SendMessageAsync(json);

                System.Diagnostics.Debug.WriteLine($"[IPC] 부품 정보 전송 {(success ? "성공" : "실패")}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IPC] 전송 오류: {ex.Message}");
            }
        }

        private static void InitializeIPC()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[IPC] 초기화 시작...");

                _ipcClient = new NamedPipeClient(PIPE_NAME);

                _ipcClient.ConnectionStatusChanged += (sender, isConnected) =>
                {
                    string status = isConnected ? "C++ 서버 연결됨" : "C++ 서버 연결 끊김";
                    System.Diagnostics.Debug.WriteLine($"[IPC] {status}");

                    _mainPanel?.Dispatcher.Invoke(() =>
                    {
                        _mainPanel.UpdateConnectionStatus(isConnected);
                    });
                };

                _ipcClient.MessageReceived += (sender, message) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[IPC] C++로부터 수신: {message}");

                    try
                    {
                        var response = JsonConvert.DeserializeObject<IPCResponse>(message);

                        _mainPanel?.Dispatcher.Invoke(() =>
                        {
                            _mainPanel.ShowMessage(response.Message);
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[IPC] 응답 파싱 실패: {ex.Message}");
                    }
                };

                // 비동기 연결 시도
                System.Threading.Tasks.Task.Run(async () =>
                {
                    System.Diagnostics.Debug.WriteLine("[IPC] 연결 시도 시작...");

                    for (int i = 0; i < 20; i++)
                    {
                        System.Diagnostics.Debug.WriteLine($"[IPC] 연결 시도 {i + 1}/20...");

                        if (await _ipcClient.ConnectAsync(1000))
                        {
                            System.Diagnostics.Debug.WriteLine("[IPC] ✅ C++ 서버 연결 성공!");
                            return;
                        }

                        await System.Threading.Tasks.Task.Delay(500);
                    }

                    System.Diagnostics.Debug.WriteLine("[IPC] ❌ C++ 서버 연결 실패 (20회 시도)");

                    _mainPanel?.Dispatcher.Invoke(() =>
                    {
                        _mainPanel.UpdateConnectionStatus(false);
                        _mainPanel.ShowMessage("⚠️ C++ 서버 연결 실패. IPCRECONNECT 명령으로 재시도하세요.");
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IPC] 초기화 실패: {ex.Message}");
            }
        }

        #endregion
    }

    /// <summary>
    /// 부품 선택 이벤트 인자
    /// </summary>
    public class PartSelectedEventArgs : EventArgs
    {
        public string MainCategory { get; set; }
        public string SubCategory { get; set; }
        public string MidCategory { get; set; }
        public string PartType { get; set; }
        public string Series { get; set; }
        public string PartCode { get; set; }
    }
}
