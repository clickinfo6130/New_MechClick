using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Newtonsoft.Json;
using BoltSpecProgram.UI;
using BoltSpecProgram.Services;

namespace BoltSpecProgram
{
    public partial class MainWindow : Window
    {
        private BoltSpecData _data;
        private ExcelReader _excelReader;
        private DynamicUIManager _uiManager;

        private string _currentFilePath;
        private bool _isControlMoveMode = false;
        private FrameworkElement _selectedControlForMove = null;
        private const string DEFAULT_EXCEL_PATH = "SampleData_02.xlsx"; // 기본 엑셀 파일 경로
        private const string SETTINGS_FILE = "BoltSpecSettings.json"; // 설정 파일 경로

        // 윈도우 스케일링 관련
        private const double INITIAL_WIDTH = 680;
        private const double INITIAL_HEIGHT = 900;
        private bool _isInitialized = false;
        
        // ✅ 데이터 보기 패널 토글 중인지 확인하는 플래그
        private bool _isDataViewToggling = false;

        private Action<bool, Dictionary<string, object>> _responseCallback; // 2025.11.24 ADD

        public bool IsControlMoveMode => _isControlMoveMode;
        public FrameworkElement SelectedControlForMove
        {
            get => _selectedControlForMove;
            set => _selectedControlForMove = value;
        }

        public MainWindow()
        {
            InitializeComponent();
            _excelReader = new ExcelReader();
            _uiManager = new DynamicUIManager(this);
        }

        public void WaitForUserResponse(Action<bool, Dictionary<string, object>> callback)
        {
            Console.WriteLine("WaitForUserResponse called");
            _responseCallback = callback;
            Show();
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 프로그램 시작 시 기본 엑셀 파일 로드 시도
            TryLoadDefaultExcelFile();
            _isInitialized = true;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 초기화 완료 후에만 처리
            if (!_isInitialized || _uiManager == null)
                return;

            // ✅ 데이터 보기 패널 토글 애니메이션 중에는 처리하지 않음
            if (_isDataViewToggling)
                return;

            // ✅ 데이터보기 패널 상태 확인
            bool isDataViewOpen = DataViewBorder.Visibility == Visibility.Visible;

            if (isDataViewOpen)
            {
                // ✅ 데이터보기 패널이 열려있는 경우:
                // 좌측 영역은 670px로 고정되어 있으므로 컴포넌트 스케일링 불필요
                // Canvas 크기만 재조정
                _uiManager.RefreshCanvasSize();
            }
            else
            {
                // ✅ 데이터보기 패널이 닫혀있는 경우:
                // 좌측 영역이 Window 크기에 따라 늘어나므로 컴포넌트도 비례 스케일링
                double scaleX = e.NewSize.Width / INITIAL_WIDTH;
                double scaleY = e.NewSize.Height / INITIAL_HEIGHT;

                // UIManager를 통해 모든 컨트롤 스케일링
                _uiManager.ScaleControls(scaleX, scaleY);
            }
        }

        private void ScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // ScrollViewer 크기에 맞게 Canvas 크기 조정
            if (sender is ScrollViewer scrollViewer && scrollViewer.Content is Canvas canvas)
            {
                // ScrollViewer의 실제 뷰포트 크기
                double viewportWidth = scrollViewer.ViewportWidth;
                double viewportHeight = scrollViewer.ViewportHeight;
                
                if (viewportWidth > 0 && viewportHeight > 0)
                {
                    // Canvas 내 컴포넌트들의 최대 영역 계산
                    double maxRight = 0;
                    double maxBottom = 0;
                    
                    foreach (UIElement child in canvas.Children)
                    {
                        if (child is FrameworkElement element)
                        {
                            double right = Canvas.GetLeft(element) + element.ActualWidth;
                            double bottom = Canvas.GetTop(element) + element.ActualHeight;
                            
                            if (right > maxRight) maxRight = right;
                            if (bottom > maxBottom) maxBottom = bottom;
                        }
                    }
                    
                    // 여백 추가 (50px)
                    maxRight += 50;
                    maxBottom += 50;
                    
                    // Canvas 크기를 ScrollViewer 크기와 컴포넌트 영역 중 큰 값으로 설정
                    canvas.Width = Math.Max(viewportWidth, maxRight);
                    canvas.Height = Math.Max(viewportHeight, maxBottom);
                }
            }
        }

        private void TryLoadDefaultExcelFile()
        {
            // 현재 실행 파일 디렉토리에서 기본 엑셀 파일 찾기
            var exePath = AppDomain.CurrentDomain.BaseDirectory;
            var defaultPath = System.IO.Path.Combine(exePath, DEFAULT_EXCEL_PATH);

            if (File.Exists(defaultPath))
            {
                try
                {
                    _currentFilePath = defaultPath;
                    LoadExcelData(_currentFilePath);
                    StatusTextBlock.Text = $"기본 파일 로드 완료: {DEFAULT_EXCEL_PATH}";
                }
                catch (Exception ex)
                {
                    var result = MessageBox.Show(
                        $"기본 파일({DEFAULT_EXCEL_PATH})을 로드할 수 없습니다.\n\n오류: {ex.Message}\n\n다른 파일을 선택하시겠습니까?",
                        "파일 로드 오류",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        OpenExcel_Click(null, null);
                    }
                }
            }
            else
            {
                var result = MessageBox.Show(
                    $"기본 엑셀 파일({DEFAULT_EXCEL_PATH})을 찾을 수 없습니다.\n\n엑셀 파일을 선택하시겠습니까?",
                    "파일 없음",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    OpenExcel_Click(null, null);
                }
                else
                {
                    StatusTextBlock.Text = "엑셀 파일을 열어주세요. (파일 → 엑셀 파일 열기)";
                }
            }
        }

        #region 메뉴 이벤트

        private void OpenExcel_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls",
                Title = "엑셀 파일 선택"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    _currentFilePath = openFileDialog.FileName;
                    LoadExcelData(_currentFilePath);
                    StatusTextBlock.Text = $"파일 로드 완료: {System.IO.Path.GetFileName(_currentFilePath)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"파일 로드 중 오류 발생:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveData = _uiManager.GetCurrentState();
                var json = JsonConvert.SerializeObject(saveData, Formatting.Indented);
                File.WriteAllText(SETTINGS_FILE, json);
                StatusTextBlock.Text = $"설정 저장 완료: {SETTINGS_FILE}";
                MessageBox.Show("설정이 저장되었습니다.\n(선택값 + 컨트롤 위치)", "저장", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 중 오류 발생:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(SETTINGS_FILE))
            {
                MessageBox.Show($"설정 파일({SETTINGS_FILE})이 없습니다.", "파일 없음", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var json = File.ReadAllText(SETTINGS_FILE);
                var loadData = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                _uiManager.LoadState(loadData);
                StatusTextBlock.Text = $"설정 불러오기 완료: {SETTINGS_FILE}";
                MessageBox.Show("설정이 복원되었습니다.\n(선택값 + 컨트롤 위치)", "불러오기", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"불러오기 중 오류 발생:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("모든 입력을 초기화하시겠습니까?", "초기화", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _uiManager.ResetAll();
                StatusTextBlock.Text = "초기화 완료";
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region 버튼 이벤트

        private void ToggleDataView_Click(object sender, RoutedEventArgs e)
        {
            // Grid와 ColumnDefinitions 가져오기
            var splitterColumn = SplitterColumn;
            var dataViewColumn = DataViewColumn;
            
            if (DataViewBorder.Visibility == Visibility.Collapsed)
            {
                // ✅ 데이터 보기 패널 확장
                DataViewBorder.Visibility = Visibility.Visible;
                DataViewSplitter.Visibility = Visibility.Visible;
                
                // ✅ 좌측 메인 영역을 670px 고정으로 변경 (확장 시 크기 유지)
                MainColumn.Width = new GridLength(670);
                
                // 컬럼 너비 설정
                splitterColumn.Width = new GridLength(5);
                dataViewColumn.Width = new GridLength(450);
                
                // ✅ 윈도우 크기 계산: 좌측(670) + Splitter(5) + 데이터패널(450) = 1125
                // 실제로는 Border 등의 여백을 고려하여 1135px로 설정
                var targetWidth = 1135;
                
                // ✅ 데이터 보기 토글 중 플래그 설정
                _isDataViewToggling = true;
                
                // 윈도우 크기를 애니메이션으로 부드럽게 확장
                var widthAnimation = new DoubleAnimation
                {
                    From = this.ActualWidth,
                    To = targetWidth,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };
                
                // ✅ 애니메이션 완료 후 플래그 해제
                widthAnimation.Completed += (s, args) =>
                {
                    _isDataViewToggling = false;
                    
                    // Canvas 크기 재조정 (선택적)
                    if (_uiManager != null)
                    {
                        _uiManager.RefreshCanvasSize();
                    }
                };
                
                this.BeginAnimation(Window.WidthProperty, widthAnimation);
                
                ((Button)sender).Content = "데이터보기 <<";
            }
            else
            {
                // ✅ 데이터 보기 패널 숨김
                DataViewBorder.Visibility = Visibility.Collapsed;
                DataViewSplitter.Visibility = Visibility.Collapsed;
                
                // 컬럼 너비 0으로 설정
                splitterColumn.Width = new GridLength(0);
                dataViewColumn.Width = new GridLength(0);
                
                // ✅ 좌측 메인 영역을 가변 크기(*)로 복원 (Window 크기에 따라 늘어남)
                MainColumn.Width = new GridLength(1, GridUnitType.Star);
                
                // ✅ 윈도우를 원래 크기(MinWidth)로 축소
                var targetWidth = 680;
                
                // ✅ 데이터 보기 토글 중 플래그 설정
                _isDataViewToggling = true;
                
                // 윈도우 크기를 애니메이션으로 부드럽게 축소
                var widthAnimation = new DoubleAnimation
                {
                    From = this.ActualWidth,
                    To = targetWidth,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };
                
                // ✅ 애니메이션 완료 후 플래그 해제
                widthAnimation.Completed += (s, args) =>
                {
                    _isDataViewToggling = false;
                    
                    // Canvas 크기 재조정 (선택적)
                    if (_uiManager != null)
                    {
                        _uiManager.RefreshCanvasSize();
                    }
                };
                
                this.BeginAnimation(Window.WidthProperty, widthAnimation);
                
                ((Button)sender).Content = "데이터보기 >>";
            }
        }

        private void Angle_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("각도 기능", "각도", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DrawOption_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("작도옵션 기능", "작도옵션", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            // 1. 선택된 데이터 수집
            var selectedData = _uiManager.GetSelectedValues();

            // 선택된 항목이 없으면 경고
            if (selectedData.Count == 0)
            {
                MessageBox.Show("선택된 사양이 없습니다.\n항목을 선택해주세요.",
                    "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. 종류 정보 추가 (Headers[1])
            if (_data != null && _data.Headers.Count > 1)
            {
                var categoryColumn = _data.Headers[1];  // "종류"
                if (_data.DataRows.Count > 0 &&
                    _data.DataRows[0].CompleteValues.ContainsKey(categoryColumn))
                {
                    selectedData["종류"] = _data.DataRows[0].CompleteValues[categoryColumn];
                }
            }

            // 3. 선택 요약 표시
            var summary = _uiManager.GenerateSummary(selectedData);

            // 4. CAD 전송 확인
            var confirmResult = MessageBox.Show(
                summary + "\n\n이 사양을 CAD로 전송하시겠습니까?",
                "CAD 전송 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes)
            {
                StatusTextBlock.Text = "전송 취소됨";
                return;
            }

            // 5. CAD로 전송
            StatusTextBlock.Text = "CAD로 전송 중...";

            _responseCallback?.Invoke(true, selectedData);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("작도를 취소하시겠습니까?", "작도취소", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                Close();
            }
        }

        private void MaterialProperties_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("제료특성 기능", "제료특성", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region 데이터 로드 및 UI 생성

        private void LoadExcelData(string filePath)
        {
            _data = _excelReader.ReadExcel(filePath);

            if (_data.DataRows.Count == 0)
            {
                MessageBox.Show("데이터가 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _uiManager.BuildUI(_data);

            // 저장된 위치 정보 자동 로드
            if (File.Exists(SETTINGS_FILE))
            {
                try
                {
                    var json = File.ReadAllText(SETTINGS_FILE);
                    var loadData = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    _uiManager.LoadState(loadData);
                    StatusTextBlock.Text += " (저장된 설정 복원됨)";
                }
                catch
                {
                    // 로드 실패 시 무시
                }
            }
        }

        #endregion

        #region 키보드 이벤트 (컨트롤 이동/저장/초기화)

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Ctrl + S: 저장
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Save_Click(null, null);
                e.Handled = true;
            }
            // Ctrl + M: 컨트롤 이동 모드 토글
            else if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ToggleControlMoveMode();
                e.Handled = true;
            }
            // Ctrl + R: 초기화
            else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Reset_Click(null, null);
                e.Handled = true;
            }
            // Ctrl + Arrow Keys: 선택된 컨트롤 이동 (이동 모드일 때)
            else if (_isControlMoveMode && _selectedControlForMove != null && Keyboard.Modifiers == ModifierKeys.Control)
            {
                double moveDistance = 5;
                var left = Canvas.GetLeft(_selectedControlForMove);
                var top = Canvas.GetTop(_selectedControlForMove);
                
                switch (e.Key)
                {
                    case Key.Left:
                        Canvas.SetLeft(_selectedControlForMove, left - moveDistance);
                        e.Handled = true;
                        break;
                    case Key.Right:
                        Canvas.SetLeft(_selectedControlForMove, left + moveDistance);
                        e.Handled = true;
                        break;
                    case Key.Up:
                        Canvas.SetTop(_selectedControlForMove, top - moveDistance);
                        e.Handled = true;
                        break;
                    case Key.Down:
                        Canvas.SetTop(_selectedControlForMove, top + moveDistance);
                        e.Handled = true;
                        break;
                }
            }
        }

        private void ToggleControlMoveMode()
        {
            _isControlMoveMode = !_isControlMoveMode;
            
            if (_isControlMoveMode)
            {
                StatusTextBlock.Text = "📌 컨트롤 이동 모드 활성화 (컨트롤 드래그 또는 Ctrl+방향키로 이동, Ctrl+M으로 종료)";
                StatusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                StatusTextBlock.FontWeight = FontWeights.Bold;
            }
            else
            {
                StatusTextBlock.Text = "컨트롤 이동 모드 비활성화";
                StatusTextBlock.Foreground = new SolidColorBrush(Colors.Black);
                StatusTextBlock.FontWeight = FontWeights.Normal;
                _selectedControlForMove = null;
            }
        }
        /// <summary>
        /// 파일명용 시리즈 이름 반환 (현재 선택된 종류 기준)
        /// </summary>
        private string GetSeriesNameForFileName()
        {
            // ✅ DynamicUIManager에서 현재 선택된 종류 가져오기
            if (_uiManager != null && !string.IsNullOrEmpty(_uiManager.SelectedCategory))
            {
                var name = _uiManager.SelectedCategory;
                // 파일명에 사용할 수 없는 문자 제거
                return string.Join("_", name.Split(System.IO.Path.GetInvalidFileNameChars()));
            }
            
            // Fallback: 기존 방식
            if (_data != null && _data.Headers.Count > 1 && _data.DataRows.Count > 0)
            {
                var categoryColumn = _data.Headers[1];
                if (_data.DataRows[0].CompleteValues.ContainsKey(categoryColumn))
                {
                    var name = _data.DataRows[0].CompleteValues[categoryColumn];
                    // 파일명에 사용할 수 없는 문자 제거
                    return string.Join("_", name.Split(System.IO.Path.GetInvalidFileNameChars()));
                }
            }
            return "BoltSpec";
        }

        private void ExportToJson_Click(object sender, RoutedEventArgs e)
        {
            if (_data == null || _data.DataRows.Count == 0)
            {
                MessageBox.Show("내보낼 데이터가 없습니다.\n먼저 Excel 파일을 열어주세요.",
                    "데이터 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ✅ 현재 선택된 종류의 필터링된 데이터 사용
            var exportData = _uiManager?.FilteredData ?? _data;
            var selectedCategory = _uiManager?.SelectedCategory ?? "BoltSpec";
            var selectedClassification = _uiManager?.SelectedClassification ?? "";
            
            // 저장 파일 대화상자
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "JSON Files|*.json",
                Title = "JSON 파일로 내보내기",
                FileName = $"{GetSeriesNameForFileName()}_Data.json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // ✅ 필터링된 데이터로 JSON 내보내기 (분류 + CMD 포함)
                    var exporter = new JsonExporter(exportData);
                    exporter.Export(saveFileDialog.FileName);

                    StatusTextBlock.Text = $"JSON 내보내기 완료: {System.IO.Path.GetFileName(saveFileDialog.FileName)}";

                    // 결과 확인 대화상자
                    var result = MessageBox.Show(
                        $"JSON 파일이 저장되었습니다.\n\n" +
                        $"분류: {selectedClassification}\n" +
                        $"종류: {selectedCategory}\n" +
                        $"데이터 행 수: {exportData.DataRows.Count}개\n" +
                        $"파일: {saveFileDialog.FileName}\n\n" +
                        $"파일을 열어보시겠습니까?",
                        "내보내기 완료",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        // 기본 텍스트 편집기로 파일 열기
                        System.Diagnostics.Process.Start("notepad.exe", saveFileDialog.FileName);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"JSON 내보내기 중 오류 발생:\n{ex.Message}",
                        "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        /// <summary>
        /// 현재 선택된 종류의 데이터를 PostgreSQL DB에 저장
        /// </summary>
        private async void SaveToDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (_data == null || _data.DataRows.Count == 0)
            {
                MessageBox.Show("저장할 데이터가 없습니다.\n먼저 Excel 파일을 열어주세요.",
                    "데이터 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var selectedCategory = _uiManager?.SelectedCategory;
            var selectedClassification = _uiManager?.SelectedClassification;  // ✅ 분류 가져오기
            
            if (string.IsNullOrWhiteSpace(selectedCategory))
            {
                MessageBox.Show("저장할 종류를 선택해주세요.",
                    "선택 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (string.IsNullOrWhiteSpace(selectedClassification))
            {
                MessageBox.Show("분류가 선택되지 않았습니다.",
                    "선택 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // ✅ CMD 코드 가져오기
            var exporter = new JsonExporter(_data);
            var cmdCode = exporter.GetCMDCodeForCategory(selectedCategory);
            
            if (string.IsNullOrWhiteSpace(cmdCode))
            {
                var result = MessageBox.Show(
                    $"'{selectedCategory}'에 대한 CMD 코드가 없습니다.\n" +
                    $"Name_Code 시트에 매핑을 추가해주세요.\n\n" +
                    $"종류 이름을 part_code로 사용하여 저장하시겠습니까?",
                    "CMD 코드 없음",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                    
                if (result != MessageBoxResult.Yes)
                    return;
                    
                // 종류 이름을 코드로 사용
                cmdCode = selectedCategory.Replace(" ", "_").ToUpper();
            }
            
            // ✅ 확인 대화상자 (분류 정보 추가)
            var confirmResult = MessageBox.Show(
                $"다음 데이터를 PostgreSQL DB에 저장하시겠습니까?\n\n" +
                $"Part Type (분류): {selectedClassification}\n" +
                $"Part Code: {cmdCode}\n" +
                $"Part Name: {selectedCategory}\n" +
                $"Database: Standard_Spec\n" +
                $"Table: PartSpec",
                "DB 저장 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (confirmResult != MessageBoxResult.Yes)
                return;
            
            try
            {
                StatusTextBlock.Text = "DB 저장 중...";
                
                // ✅ JSON 데이터 생성 (해당 종류만)
                var jsonData = exporter.ExportSeriesToString(selectedCategory);
                
                if (string.IsNullOrWhiteSpace(jsonData))
                {
                    MessageBox.Show("JSON 데이터 생성에 실패했습니다.",
                        "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // ✅ DB 저장
                var dbService = new DatabaseService();
                
                // 연결 테스트
                bool isConnected = await dbService.TestConnectionAsync();
                if (!isConnected)
                {
                    MessageBox.Show(
                        "PostgreSQL 서버에 연결할 수 없습니다.\n\n" +
                        "연결 설정을 확인해주세요:\n" +
                        "- Host: localhost\n" +
                        "- Port: 5432\n" +
                        "- Database: Standard_Spec",
                        "연결 실패",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    StatusTextBlock.Text = "DB 연결 실패";
                    return;
                }
                
                // ✅ 저장 실행 (part_type 추가)
                bool success = await dbService.SavePartSpecAsync(selectedClassification, cmdCode, selectedCategory, jsonData);
                
                if (success)
                {
                    StatusTextBlock.Text = $"DB 저장 완료: [{selectedClassification}] {cmdCode} ({selectedCategory})";
                    MessageBox.Show(
                        $"데이터가 성공적으로 저장되었습니다.\n\n" +
                        $"Part Type: {selectedClassification}\n" +
                        $"Part Code: {cmdCode}\n" +
                        $"Part Name: {selectedCategory}",
                        "저장 완료",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    StatusTextBlock.Text = "DB 저장 실패";
                    MessageBox.Show("데이터 저장에 실패했습니다.",
                        "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "DB 저장 오류";
                MessageBox.Show($"DB 저장 중 오류 발생:\n{ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 모든 분류/종류를 순차적으로 자동 DB 저장
        /// </summary>
        private async void SaveAllToDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (_data == null || _data.DataRows.Count == 0)
            {
                MessageBox.Show("저장할 데이터가 없습니다.\n먼저 Excel 파일을 열어주세요.",
                    "데이터 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (_uiManager == null)
            {
                MessageBox.Show("UI Manager가 초기화되지 않았습니다.",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            // ✅ 전체 분류/종류 목록 수집
            var allClassifications = _uiManager.AllClassifications;
            if (allClassifications == null || allClassifications.Count == 0)
            {
                MessageBox.Show("분류 데이터가 없습니다.",
                    "데이터 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // 전체 종류 수 계산
            int totalCategories = 0;
            var categoryList = new List<(string classification, string category)>();
            
            foreach (var classification in allClassifications)
            {
                var categories = _uiManager.GetCategoriesForClassification(classification);
                foreach (var category in categories)
                {
                    categoryList.Add((classification, category));
                    totalCategories++;
                }
            }
            
            if (totalCategories == 0)
            {
                MessageBox.Show("저장할 종류가 없습니다.",
                    "데이터 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // ✅ 확인 대화상자
            var confirmResult = MessageBox.Show(
                $"전체 데이터를 DB에 저장하시겠습니까?\n\n" +
                $"분류 수: {allClassifications.Count}개\n" +
                $"종류 수: {totalCategories}개\n\n" +
                $"Database: Standard_Spec\n" +
                $"Table: PartSpec\n\n" +
                $"저장을 시작하면 기존 데이터가 업데이트됩니다.",
                "전체 DB 저장 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (confirmResult != MessageBoxResult.Yes)
                return;
            
            try
            {
                // ✅ DB 서비스 초기화 및 연결 테스트
                var dbService = new DatabaseService();
                
                StatusTextBlock.Text = "DB 연결 확인 중...";
                bool isConnected = await dbService.TestConnectionAsync();
                
                if (!isConnected)
                {
                    MessageBox.Show(
                        "PostgreSQL 서버에 연결할 수 없습니다.\n\n" +
                        "연결 설정을 확인해주세요:\n" +
                        "- Host: localhost\n" +
                        "- Port: 5432\n" +
                        "- Database: Standard_Spec",
                        "연결 실패",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    StatusTextBlock.Text = "DB 연결 실패";
                    return;
                }
                
                // ✅ 테이블 확인/생성
                await dbService.EnsureTableExistsAsync();
                
                // ✅ 전체 데이터로 JsonExporter 생성
                var exporter = new JsonExporter(_data);
                
                // ✅ 저장 결과 추적
                int successCount = 0;
                int failCount = 0;
                int skipCount = 0;
                var failedItems = new List<string>();
                
                // ✅ 순차 저장 시작
                for (int i = 0; i < categoryList.Count; i++)
                {
                    var (classification, category) = categoryList[i];
                    
                    // 진행 상황 표시
                    StatusTextBlock.Text = $"저장 중... [{i + 1}/{totalCategories}] {classification} > {category}";
                    
                    // UI 업데이트를 위해 Dispatcher 처리
                    await System.Threading.Tasks.Task.Run(() => { });
                    Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                    
                    try
                    {
                        // CMD 코드 가져오기
                        var cmdCode = exporter.GetCMDCodeForCategory(category);
                        
                        if (string.IsNullOrWhiteSpace(cmdCode))
                        {
                            // CMD 코드가 없으면 종류 이름으로 대체
                            cmdCode = category.Replace(" ", "_").ToUpper();
                        }
                        
                        // JSON 데이터 생성
                        var jsonData = exporter.ExportSeriesToString(category);
                        
                        if (string.IsNullOrWhiteSpace(jsonData))
                        {
                            skipCount++;
                            continue;
                        }
                        
                        // ✅ DB 저장 (part_type = classification 추가)
                        bool success = await dbService.SavePartSpecAsync(classification, cmdCode, category, jsonData);
                        
                        if (success)
                        {
                            successCount++;
                        }
                        else
                        {
                            failCount++;
                            failedItems.Add($"{category} ({cmdCode})");
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        failedItems.Add($"{category}: {ex.Message}");
                    }
                }
                
                // ✅ 결과 표시
                StatusTextBlock.Text = $"전체 DB 저장 완료: 성공 {successCount}, 실패 {failCount}, 건너뜀 {skipCount}";
                
                string resultMessage = $"전체 DB 저장이 완료되었습니다.\n\n" +
                    $"✅ 성공: {successCount}개\n" +
                    $"❌ 실패: {failCount}개\n" +
                    $"⏭️ 건너뜀: {skipCount}개\n" +
                    $"━━━━━━━━━━━━━━━━\n" +
                    $"총 처리: {totalCategories}개";
                
                if (failedItems.Count > 0)
                {
                    resultMessage += "\n\n실패 항목:\n" + string.Join("\n", failedItems.Take(10));
                    if (failedItems.Count > 10)
                    {
                        resultMessage += $"\n... 외 {failedItems.Count - 10}개";
                    }
                }
                
                MessageBox.Show(resultMessage,
                    "전체 DB 저장 결과",
                    MessageBoxButton.OK,
                    failCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "전체 DB 저장 오류";
                MessageBox.Show($"전체 DB 저장 중 오류 발생:\n{ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
