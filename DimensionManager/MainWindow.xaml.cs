using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DimensionManager.Models;
using DimensionManager.Services;
using Microsoft.Win32;

namespace DimensionManager
{
    public partial class MainWindow : Window
    {
        private readonly ExcelService _excelService;
        private readonly PostgresService _postgresService;

        private DimensionImportResult _importResult;

        // DB 연결 정보
        private string _dbHost = "192.168.0.17";
        private int _dbPort = 5432;
        private string _dbName = "Standard_Core";
        private string _dbUser = "clickinfo";
        private string _dbPassword = "info6130!!";

        public MainWindow()
        {
            InitializeComponent();
            
            _excelService = new ExcelService();
            _postgresService = new PostgresService();

            UpdateDbInfoText();
        }

        #region Excel Import

        private void BtnLoadExcel_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|All Files (*.*)|*.*",
                Title = "치수 데이터 엑셀 파일 선택"
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
                Mouse.OverrideCursor = Cursors.Wait;
                TxtStatus.Text = "엑셀 파일 읽는 중...";

                _importResult = _excelService.ReadDimensionExcel(filePath);

                // UI 업데이트
                TxtFileName.Text = Path.GetFileName(filePath);
                TxtPartCode.Text = _importResult.PartCode;
                TxtStandard.Text = _importResult.Standard;
                TxtKeyFields.Text = string.Join(", ", _importResult.KeyFields.Select(k => k.DisplayName + "(" + k.FieldName + ")"));
                TxtRowCount.Text = _importResult.RawData.Count.ToString() + " 행";

                // DataGrid 바인딩
                BindRawDataGrid();
                DataGridMeta.ItemsSource = _importResult.DimensionMetas;
                DataGridKeyOptions.ItemsSource = _importResult.KeyOptions;
                DataGridDimensions.ItemsSource = _importResult.Dimensions;

                TxtStatus.Text = string.Format("엑셀 파일 로드 완료: {0} 행, {1} 컬럼", 
                    _importResult.RawData.Count, _importResult.Columns.Count);
            }
            catch (Exception ex)
            {
                MessageBox.Show("엑셀 파일 로드 실패:\n" + ex.Message, "오류", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "엑셀 파일 로드 실패";
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void BindRawDataGrid()
        {
            if (_importResult == null || _importResult.RawData.Count == 0)
            {
                DataGridRaw.ItemsSource = null;
                return;
            }

            // Dictionary를 DataTable로 변환
            var dt = new DataTable();

            // 컬럼 추가 (중복 방지)
            var addedColumns = new HashSet<string>();
            foreach (var col in _importResult.Columns)
            {
                string colName = col.FieldName;
                
                // 이미 추가된 컬럼이면 스킵 (안전장치)
                if (addedColumns.Contains(colName))
                {
                    continue;
                }
                
                dt.Columns.Add(colName, typeof(object));
                addedColumns.Add(colName);
            }

            // 데이터 추가
            foreach (var row in _importResult.RawData)
            {
                var dataRow = dt.NewRow();
                foreach (var col in _importResult.Columns)
                {
                    if (addedColumns.Contains(col.FieldName) && row.ContainsKey(col.FieldName))
                    {
                        dataRow[col.FieldName] = row[col.FieldName] ?? DBNull.Value;
                    }
                }
                dt.Rows.Add(dataRow);
            }

            DataGridRaw.ItemsSource = dt.DefaultView;
        }

        #endregion

        #region DB Operations

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
                UpdateDbInfoText();

                TxtStatus.Text = "DB 설정 변경됨";
            }
        }

        private void UpdateDbInfoText()
        {
            TxtDbInfo.Text = string.Format("Host: {0} | DB: {1}", _dbHost, _dbName);
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
                bool success = await _postgresService.TestConnectionAsync();
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
                "치수 관련 테이블을 생성합니다.\n\n" +
                "• DimensionMeta (치수 필드 정의)\n" +
                "• DimensionKeyOption (키값 옵션)\n" +
                "• PartDimension (치수 데이터)\n\n" +
                "계속하시겠습니까?",
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
            if (_importResult == null || _importResult.Dimensions.Count == 0)
            {
                MessageBox.Show("먼저 엑셀 파일을 로드해주세요.", "알림", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_postgresService.ConnectionString))
            {
                _postgresService.SetConnection(_dbHost, _dbPort, _dbName, _dbUser, _dbPassword);
            }

            var message = string.Format(
                "다음 데이터를 DB에 저장합니다:\n\n" +
                "• Part Code: {0}\n" +
                "• DimensionMeta: {1} 건\n" +
                "• DimensionKeyOption: {2} 건\n" +
                "• PartDimension: {3} 건\n\n" +
                "⚠️ 해당 Part Code의 기존 데이터는 삭제됩니다.\n계속하시겠습니까?",
                _importResult.PartCode,
                _importResult.DimensionMetas.Count,
                _importResult.KeyOptions.Count,
                _importResult.Dimensions.Count);

            var result = MessageBox.Show(message, "DB 저장", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            TxtStatus.Text = "DB 저장 중...";
            Mouse.OverrideCursor = Cursors.Wait;
            BtnSaveToDb.IsEnabled = false;

            // 데이터 복사 (스레드 안전)
            var importData = _importResult;

            try
            {
                await Task.Run(async () =>
                {
                    await _postgresService.SaveDimensionDataAsync(importData, true);
                });

                TxtStatus.Text = "DB 저장 완료";
                MessageBox.Show(
                    string.Format("데이터가 성공적으로 저장되었습니다.\n\n" +
                        "• DimensionMeta: {0} 건\n" +
                        "• DimensionKeyOption: {1} 건\n" +
                        "• PartDimension: {2} 건",
                        importData.DimensionMetas.Count,
                        importData.KeyOptions.Count,
                        importData.Dimensions.Count),
                    "완료", MessageBoxButton.OK, MessageBoxImage.Information);
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
    }
}
