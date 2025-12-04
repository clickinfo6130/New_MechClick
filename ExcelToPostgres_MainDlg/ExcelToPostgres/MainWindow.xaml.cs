using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ExcelToPostgres.Models;
using ExcelToPostgres.Services;
using Microsoft.Win32;

namespace ExcelToPostgres
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ExcelService _excelService;
        private readonly PostgresService _postgresService;

        private ObservableCollection<MainCategory> _mainCategories = new ObservableCollection<MainCategory>();
        private ObservableCollection<SubCategory> _subCategories = new ObservableCollection<SubCategory>();
        private ObservableCollection<MidCategory> _midCategories = new ObservableCollection<MidCategory>();
        private ObservableCollection<PartType> _partTypes = new ObservableCollection<PartType>();
        private ObservableCollection<PartSeries> _partSeriesList = new ObservableCollection<PartSeries>();

        private string _currentExcelPath = "";

        // DB 설정 (기본값)
        private string _dbHost = "localhost";
        private int _dbPort = 5432;
        private string _dbName = "Standard_Core";
        private string _dbUser = "postgres";
        private string _dbPassword = "";

        public MainWindow()
        {
            InitializeComponent();

            _excelService = new ExcelService();
            _postgresService = new PostgresService();

            // 그리드 바인딩
            GridMainCategory.ItemsSource = _mainCategories;
            GridSubCategory.ItemsSource = _subCategories;
            GridMidCategory.ItemsSource = _midCategories;
            GridPartType.ItemsSource = _partTypes;
            GridPartSeries.ItemsSource = _partSeriesList;

            // 탭 변경 이벤트
            MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;

            // 설정 로드
            LoadSettings();
        }

        #region Excel 파일 처리

        private void BtnOpenExcel_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                Title = "Excel 파일 선택"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadExcelFile(dialog.FileName);
            }
        }

        private void LoadExcelFile(string filePath)
        {
            try
            {
                TxtStatus.Text = "Excel 파일 로딩 중...";
                Mouse.OverrideCursor = Cursors.Wait;

                var result = _excelService.LoadFromExcel(filePath);

                _mainCategories.Clear();
                foreach (var item in result.MainCategories) _mainCategories.Add(item);

                _subCategories.Clear();
                foreach (var item in result.SubCategories) _subCategories.Add(item);

                _midCategories.Clear();
                foreach (var item in result.MidCategories) _midCategories.Add(item);

                _partTypes.Clear();
                foreach (var item in result.PartTypes) _partTypes.Add(item);

                _partSeriesList.Clear();
                foreach (var item in result.PartSeriesList) _partSeriesList.Add(item);

                _currentExcelPath = filePath;
                TxtFileName.Text = Path.GetFileName(filePath);
                BtnReload.IsEnabled = true;
                BtnSaveToDb.IsEnabled = true;

                UpdateRowCount();
                TxtStatus.Text = "Excel 파일 로드 완료";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Excel 파일 로드 실패:\n" + ex.Message, "오류", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "Excel 파일 로드 실패";
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentExcelPath) && File.Exists(_currentExcelPath))
            {
                var result = MessageBox.Show("현재 수정사항을 모두 취소하고 다시 로드하시겠습니까?", 
                    "확인", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    LoadExcelFile(_currentExcelPath);
                }
            }
        }

        #endregion

        #region DB 처리

        private void BtnDbSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DbSettingsDialog(_dbHost, _dbPort, _dbName, _dbUser, _dbPassword);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _dbHost = dialog.Host;
                _dbPort = dialog.Port;
                _dbName = dialog.Database;
                _dbUser = dialog.Username;
                _dbPassword = dialog.Password;

                _postgresService.SetConnection(_dbHost, _dbPort, _dbName, _dbUser, _dbPassword);
                SaveSettings();
                
                TxtStatus.Text = "DB 설정이 변경되었습니다.";
            }
        }

        private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_postgresService.ConnectionString))
            {
                _postgresService.SetConnection(_dbHost, _dbPort, _dbName, _dbUser, _dbPassword);
            }

            TxtStatus.Text = "연결 테스트 중...";
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                var success = await _postgresService.TestConnectionAsync();
                
                if (success)
                {
                    var version = await _postgresService.GetServerVersionAsync();
                    ConnectionIndicator.Fill = new SolidColorBrush(Colors.LimeGreen);
                    ConnectionStatusText.Text = "연결됨 (v" + version + ")";
                    TxtStatus.Text = "PostgreSQL " + version + " 연결 성공";
                }
                else
                {
                    ConnectionIndicator.Fill = new SolidColorBrush(Colors.Red);
                    ConnectionStatusText.Text = "연결 실패";
                    TxtStatus.Text = "DB 연결 실패";
                }
            }
            catch (Exception ex)
            {
                ConnectionIndicator.Fill = new SolidColorBrush(Colors.Red);
                ConnectionStatusText.Text = "연결 실패";
                MessageBox.Show("연결 실패:\n" + ex.Message, "오류", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "DB 연결 실패";
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async void BtnCreateTables_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_postgresService.ConnectionString))
            {
                _postgresService.SetConnection(_dbHost, _dbPort, _dbName, _dbUser, _dbPassword);
            }

            var result = MessageBox.Show(
                "테이블이 없으면 생성합니다.\n계속하시겠습니까?",
                "테이블 생성", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            TxtStatus.Text = "테이블 생성 중...";
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                await _postgresService.CreateTablesIfNotExistsAsync();
                TxtStatus.Text = "테이블 생성 완료";
                MessageBox.Show("테이블이 성공적으로 생성되었습니다.", "완료", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("테이블 생성 실패:\n" + ex.Message, "오류", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "테이블 생성 실패";
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async void BtnSaveToDb_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_postgresService.ConnectionString))
            {
                _postgresService.SetConnection(_dbHost, _dbPort, _dbName, _dbUser, _dbPassword);
            }

            var message = string.Format(
                "다음 데이터를 DB에 저장합니다:\n\n" +
                "• MainCategory: {0} 건\n" +
                "• SubCategory: {1} 건\n" +
                "• MidCategory: {2} 건\n" +
                "• PartType: {3} 건\n" +
                "• PartSeries: {4} 건\n\n" +
                "⚠️ 기존 데이터는 모두 삭제됩니다.\n계속하시겠습니까?",
                _mainCategories.Count, _subCategories.Count, _midCategories.Count,
                _partTypes.Count, _partSeriesList.Count);

            var result = MessageBox.Show(message, "DB 저장", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            TxtStatus.Text = "DB 저장 중...";
            Mouse.OverrideCursor = Cursors.Wait;
            BtnSaveToDb.IsEnabled = false;

            // ObservableCollection을 List로 복사 (스레드 안전)
            var mainCats = new List<MainCategory>(_mainCategories);
            var subCats = new List<SubCategory>(_subCategories);
            var midCats = new List<MidCategory>(_midCategories);
            var partTypes = new List<PartType>(_partTypes);
            var partSeries = new List<PartSeries>(_partSeriesList);

            try
            {
                await Task.Run(async () =>
                {
                    await _postgresService.SaveAllAsync(mainCats, subCats, midCats, partTypes, partSeries);
                });

                TxtStatus.Text = "DB 저장 완료";
                MessageBox.Show("데이터가 성공적으로 저장되었습니다.", "완료", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMsg += "\n\n상세: " + ex.InnerException.Message;
                }
                MessageBox.Show("DB 저장 실패:\n" + errorMsg, "오류", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "DB 저장 실패";
            }
            finally
            {
                Mouse.OverrideCursor = null;
                BtnSaveToDb.IsEnabled = true;
            }
        }

        #endregion

        #region 유틸리티

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateRowCount();
        }

        private void UpdateRowCount()
        {
            var counts = new int[]
            {
                _mainCategories.Count,
                _subCategories.Count,
                _midCategories.Count,
                _partTypes.Count,
                _partSeriesList.Count
            };

            var tabIndex = MainTabControl.SelectedIndex;
            if (tabIndex >= 0 && tabIndex < counts.Length)
            {
                TxtRowCount.Text = "현재 탭: " + counts[tabIndex] + "건";
            }
        }

        private void LoadSettings()
        {
            try
            {
                var settingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ExcelToPostgres", "settings.txt");

                if (File.Exists(settingsPath))
                {
                    var lines = File.ReadAllLines(settingsPath);
                    foreach (var line in lines)
                    {
                        var idx = line.IndexOf('=');
                        if (idx > 0)
                        {
                            var key = line.Substring(0, idx);
                            var val = line.Substring(idx + 1);
                            switch (key)
                            {
                                case "Host": _dbHost = val; break;
                                case "Port": int.TryParse(val, out _dbPort); break;
                                case "Database": _dbName = val; break;
                                case "Username": _dbUser = val; break;
                                case "Password": _dbPassword = val; break;
                            }
                        }
                    }
                    _postgresService.SetConnection(_dbHost, _dbPort, _dbName, _dbUser, _dbPassword);
                }
            }
            catch { /* 설정 로드 실패 무시 */ }
        }

        private void SaveSettings()
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ExcelToPostgres");
                Directory.CreateDirectory(dir);

                var settingsPath = Path.Combine(dir, "settings.txt");
                var lines = new string[]
                {
                    "Host=" + _dbHost,
                    "Port=" + _dbPort,
                    "Database=" + _dbName,
                    "Username=" + _dbUser,
                    "Password=" + _dbPassword
                };
                File.WriteAllLines(settingsPath, lines);
            }
            catch { /* 설정 저장 실패 무시 */ }
        }

        #endregion
    }
}
