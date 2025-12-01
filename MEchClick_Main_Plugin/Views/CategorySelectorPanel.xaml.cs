using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PartManager.UI;

namespace PartManager.Views
{
    public partial class CategorySelectorPanel : UserControl
    {
        #region Data Classes

        public class MainCategoryItem
        {
            public string MainCatCode { get; set; }
            public string MainCatNameKr { get; set; }
            public bool IsStandard { get; set; }
            public string ColorCode { get; set; }
        }

        public class SubCategoryItem
        {
            public string SubCatCode { get; set; }
            public string SubCatNameKr { get; set; }
            public string MainCatCode { get; set; }
            public bool IsVendor { get; set; }
            public string Country { get; set; }
        }

        public class MidCategoryItem
        {
            public string MidCatCode { get; set; }
            public string MidCatNameKr { get; set; }
            public string SubCatCode { get; set; }
        }

        public class PartTypeItem
        {
            public string PartTypeCode { get; set; }
            public string PartTypeNameKr { get; set; }
            public string SubCatCode { get; set; }
            public string MidCatCode { get; set; }
            public bool HasSeries { get; set; }
        }

        public class SeriesItem
        {
            public string SeriesCode { get; set; }
            public string SeriesName { get; set; }
            public string PartTypeCode { get; set; }
        }

        #endregion

        #region Constants

        private const double GRID_MODE_MIN_WIDTH = 650;  // 이 너비 이상이면 Grid 모드

        #endregion

        #region Events

        public event EventHandler<PartSelectedEventArgs> PartSelected;

        #endregion

        #region Fields

        private List<MainCategoryItem> _mainCategories = new List<MainCategoryItem>();
        private List<SubCategoryItem> _subCategories = new List<SubCategoryItem>();
        private List<MidCategoryItem> _midCategories = new List<MidCategoryItem>();
        private List<PartTypeItem> _partTypes = new List<PartTypeItem>();
        private List<SeriesItem> _series = new List<SeriesItem>();

        private MainCategoryItem _selectedMainCat;
        private SubCategoryItem _selectedSubCat;
        private MidCategoryItem _selectedMidCat;
        private PartTypeItem _selectedPartType;
        private SeriesItem _selectedSeries;

        // Accordion용 Border 참조
        private Border _selectedMainCatBorder;
        private Border _selectedSubCatBorder;
        private Border _selectedMidCatBorder;
        private Border _selectedPartTypeBorder;
        private Border _selectedSeriesBorder;

        // Grid용 Border 참조
        private Border _gridSelectedMainCatBorder;
        private Border _gridSelectedSubCatBorder;
        private Border _gridSelectedMidCatBorder;
        private Border _gridSelectedPartTypeBorder;
        private Border _gridSelectedSeriesBorder;

        private string _dbPath;
        private bool _isStandardMode = true;
        private bool _isGridMode = false;

        // Accordion 상태
        private bool _section1Expanded = true;
        private bool _section2Expanded = true;
        private bool _section3Expanded = true;
        private bool _section4Expanded = true;

        #endregion

        #region Constructor

        public CategorySelectorPanel()
        {
            InitializeComponent();
            Loaded += OnPanelLoaded;
        }

        #endregion

        #region Public Methods

        public void UpdateConnectionStatus(bool isConnected)
        {
            Dispatcher.Invoke(() =>
            {
                if (isConnected)
                {
                    borderConnectionStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71"));
                    txtConnectionStatus.Text = "연결됨";
                }
                else
                {
                    borderConnectionStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                    txtConnectionStatus.Text = "연결 안됨";
                }
            });
        }

        public void ShowMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtMessage.Text = message;
                messageArea.Visibility = Visibility.Visible;

                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(3);
                timer.Tick += (s, e) =>
                {
                    messageArea.Visibility = Visibility.Collapsed;
                    timer.Stop();
                };
                timer.Start();
            });
        }

        #endregion

        #region Size Changed - Mode Switching

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool shouldBeGridMode = e.NewSize.Width >= GRID_MODE_MIN_WIDTH;

            if (shouldBeGridMode != _isGridMode)
            {
                _isGridMode = shouldBeGridMode;
                SwitchDisplayMode();
            }
        }

        private void SwitchDisplayMode()
        {
            if (_isGridMode)
            {
                // Grid 모드로 전환
                accordionContainer.Visibility = Visibility.Collapsed;
                gridContainer.Visibility = Visibility.Visible;
                RefreshGridUI();
            }
            else
            {
                // Accordion 모드로 전환
                gridContainer.Visibility = Visibility.Collapsed;
                accordionContainer.Visibility = Visibility.Visible;
                RefreshAccordionUI();
            }
        }

        #endregion

        #region Accordion Header Click Events

        private void OnSection1HeaderClick(object sender, MouseButtonEventArgs e)
        {
            _section1Expanded = !_section1Expanded;
            UpdateSectionVisibility(section1Content, section1Arrow, _section1Expanded);
        }

        private void OnSection2HeaderClick(object sender, MouseButtonEventArgs e)
        {
            _section2Expanded = !_section2Expanded;
            UpdateSectionVisibility(section2Content, section2Arrow, _section2Expanded);
        }

        private void OnSection3HeaderClick(object sender, MouseButtonEventArgs e)
        {
            _section3Expanded = !_section3Expanded;
            UpdateSectionVisibility(section3Content, section3Arrow, _section3Expanded);
        }

        private void OnSection4HeaderClick(object sender, MouseButtonEventArgs e)
        {
            _section4Expanded = !_section4Expanded;
            UpdateSectionVisibility(section4Content, section4Arrow, _section4Expanded);
        }

        private void UpdateSectionVisibility(Border content, TextBlock arrow, bool expanded)
        {
            content.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            arrow.Text = expanded ? "▼" : "▶";
        }

        private void CollapseSection(int sectionNumber)
        {
            switch (sectionNumber)
            {
                case 1: _section1Expanded = false; UpdateSectionVisibility(section1Content, section1Arrow, false); break;
                case 2: _section2Expanded = false; UpdateSectionVisibility(section2Content, section2Arrow, false); break;
                case 3: _section3Expanded = false; UpdateSectionVisibility(section3Content, section3Arrow, false); break;
                case 4: _section4Expanded = false; UpdateSectionVisibility(section4Content, section4Arrow, false); break;
            }
        }

        private void ExpandSection(int sectionNumber)
        {
            switch (sectionNumber)
            {
                case 1: _section1Expanded = true; UpdateSectionVisibility(section1Content, section1Arrow, true); break;
                case 2: _section2Expanded = true; UpdateSectionVisibility(section2Content, section2Arrow, true); break;
                case 3: _section3Expanded = true; UpdateSectionVisibility(section3Content, section3Arrow, true); break;
                case 4: _section4Expanded = true; UpdateSectionVisibility(section4Content, section4Arrow, true); break;
            }
        }

        #endregion

        #region Event Handlers

        private void OnPanelLoaded(object sender, RoutedEventArgs e)
        {
            string baseDir = "C:\\Program Files\\MClickCMB2026\\ACAD\\UI\\New_01\\";// AppDomain.CurrentDomain.BaseDirectory;
            _dbPath = Path.Combine(baseDir, "Database", "standard_core.db");

            if (!File.Exists(_dbPath))
            {
                string altPath = Path.Combine(baseDir, "standard_core.db");
                if (File.Exists(altPath))
                    _dbPath = altPath;
            }

            LoadData();

            // 초기 모드 결정
            _isGridMode = ActualWidth >= GRID_MODE_MIN_WIDTH;
            SwitchDisplayMode();
        }

        private void OnClearSelection(object sender, RoutedEventArgs e)
        {
            ClearAllSelections();
        }

        private void OnConfirmSelection(object sender, RoutedEventArgs e)
        {
            if (_selectedMainCat == null)
                return;

            var args = new PartSelectedEventArgs
            {
                MainCategory = _selectedMainCat?.MainCatNameKr,
                SubCategory = _selectedSubCat?.SubCatNameKr,
                MidCategory = _selectedMidCat?.MidCatNameKr,
                PartType = _selectedPartType?.PartTypeNameKr,
                Series = _selectedSeries?.SeriesName,
                PartCode = _selectedSeries?.SeriesCode ?? _selectedPartType?.PartTypeCode ?? ""
            };

            PartSelected?.Invoke(this, args);
            ShowMessage($"선택: {args.PartCode}");
        }

        #endregion

        #region Data Loading

        private void LoadData()
        {
            try
            {
                if (!File.Exists(_dbPath))
                {
                    ShowMessage($"DB 파일을 찾을 수 없습니다: {_dbPath}");
                    return;
                }

                LoadMainCategories();
            }
            catch (Exception ex)
            {
                ShowMessage($"데이터 로드 실패: {ex.Message}");
            }
        }

        private void LoadMainCategories()
        {
            _mainCategories.Clear();
            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                conn.Open();
                string sql = @"SELECT main_cat_code, main_cat_name_kr, is_standard, color_code 
                               FROM MainCategory WHERE is_active = 1 ORDER BY sort_order";
                using (var cmd = new SQLiteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        _mainCategories.Add(new MainCategoryItem
                        {
                            MainCatCode = reader["main_cat_code"].ToString(),
                            MainCatNameKr = reader["main_cat_name_kr"].ToString(),
                            IsStandard = Convert.ToInt32(reader["is_standard"]) == 1,
                            ColorCode = reader["color_code"]?.ToString() ?? "#7F8C8D"
                        });
                    }
                }
            }
        }

        private void LoadSubCategories(string mainCatCode)
        {
            _subCategories.Clear();
            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                conn.Open();
                string sql = @"SELECT sub_cat_code, sub_cat_name_kr, main_cat_code, is_vendor, country 
                               FROM SubCategory WHERE main_cat_code = @code AND is_active = 1 ORDER BY sort_order";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@code", mainCatCode);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _subCategories.Add(new SubCategoryItem
                            {
                                SubCatCode = reader["sub_cat_code"].ToString(),
                                SubCatNameKr = reader["sub_cat_name_kr"].ToString(),
                                MainCatCode = reader["main_cat_code"].ToString(),
                                IsVendor = Convert.ToInt32(reader["is_vendor"]) == 1,
                                Country = reader["country"]?.ToString()
                            });
                        }
                    }
                }
            }
        }

        private void LoadMidCategories(string subCatCode)
        {
            _midCategories.Clear();
            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                conn.Open();
                string sql = @"SELECT mid_cat_code, mid_cat_name_kr, sub_cat_code 
                               FROM MidCategory WHERE sub_cat_code = @code AND is_active = 1 ORDER BY sort_order";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@code", subCatCode);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _midCategories.Add(new MidCategoryItem
                            {
                                MidCatCode = reader["mid_cat_code"].ToString(),
                                MidCatNameKr = reader["mid_cat_name_kr"].ToString(),
                                SubCatCode = reader["sub_cat_code"].ToString()
                            });
                        }
                    }
                }
            }
        }

        private void LoadPartTypes(string midCatCode = null, string subCatCode = null)
        {
            _partTypes.Clear();
            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                conn.Open();
                string sql;
                SQLiteCommand cmd;

                if (!string.IsNullOrEmpty(midCatCode))
                {
                    sql = @"SELECT part_type_code, part_type_name_kr, sub_cat_code, mid_cat_code, has_series 
                            FROM PartType WHERE mid_cat_code = @code AND is_active = 1 ORDER BY sort_order";
                    cmd = new SQLiteCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@code", midCatCode);
                }
                else
                {
                    sql = @"SELECT part_type_code, part_type_name_kr, sub_cat_code, mid_cat_code, has_series 
                            FROM PartType WHERE sub_cat_code = @code AND is_active = 1 ORDER BY sort_order";
                    cmd = new SQLiteCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@code", subCatCode);
                }

                using (cmd)
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        _partTypes.Add(new PartTypeItem
                        {
                            PartTypeCode = reader["part_type_code"].ToString(),
                            PartTypeNameKr = reader["part_type_name_kr"].ToString(),
                            SubCatCode = reader["sub_cat_code"]?.ToString(),
                            MidCatCode = reader["mid_cat_code"]?.ToString(),
                            HasSeries = Convert.ToInt32(reader["has_series"]) == 1
                        });
                    }
                }
            }
        }

        private void LoadSeries(string partTypeCode)
        {
            _series.Clear();
            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                conn.Open();
                string sql = @"SELECT series_code, series_name, part_type_code 
                               FROM PartSeries WHERE part_type_code = @code AND is_active = 1 ORDER BY sort_order";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@code", partTypeCode);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _series.Add(new SeriesItem
                            {
                                SeriesCode = reader["series_code"].ToString(),
                                SeriesName = reader["series_name"].ToString(),
                                PartTypeCode = reader["part_type_code"].ToString()
                            });
                        }
                    }
                }
            }
        }

        #endregion

        #region UI Refresh

        private void RefreshAccordionUI()
        {
            // Accordion 패널 업데이트
            CreateAccordionMainCategoryCards();

            // Section 상태 복원
            if (_selectedMainCat != null)
            {
                section1SelectedText.Text = _selectedMainCat.MainCatNameKr;
                section1SelectedBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_selectedMainCat.ColorCode));
                section1SelectedBadge.Visibility = Visibility.Visible;

                CreateAccordionSubCategoryCards();
                section2Border.Visibility = Visibility.Visible;
                section2Title.Text = _isStandardMode ? "CATEGORY · 분류" : "VENDOR · 업체";
            }

            if (_selectedSubCat != null)
            {
                section2SelectedText.Text = _selectedSubCat.SubCatNameKr;
                section2SelectedBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_selectedMainCat?.ColorCode ?? "#7F8C8D"));
                section2SelectedBadge.Visibility = Visibility.Visible;

                if (_isStandardMode)
                {
                    CreateAccordionMidCategoryCards();
                }
                else
                {
                    CreateAccordionPartTypeCardsInMidPanel();
                }
                section3Border.Visibility = Visibility.Visible;
                section3Title.Text = _selectedSubCat.SubCatNameKr;
            }

            if (_selectedMidCat != null)
            {
                section3SelectedText.Text = _selectedMidCat.MidCatNameKr;
                section3SelectedBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_selectedMainCat?.ColorCode ?? "#7F8C8D"));
                section3SelectedBadge.Visibility = Visibility.Visible;

                CreateAccordionPartTypeCards();
                section4Border.Visibility = Visibility.Visible;
                section4Title.Text = _selectedMidCat.MidCatNameKr;
            }
            else if (_selectedPartType != null && !_isStandardMode)
            {
                section3SelectedText.Text = _selectedPartType.PartTypeNameKr;
                section3SelectedBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_selectedMainCat?.ColorCode ?? "#7F8C8D"));
                section3SelectedBadge.Visibility = Visibility.Visible;

                if (_selectedPartType.HasSeries)
                {
                    CreateAccordionSeriesCards();
                    section4Border.Visibility = Visibility.Visible;
                    section4Title.Text = _selectedPartType.PartTypeNameKr;
                }
            }

            if (_selectedPartType != null && _isStandardMode)
            {
                section4SelectedText.Text = _selectedPartType.PartTypeNameKr;
                section4SelectedBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_selectedMainCat?.ColorCode ?? "#7F8C8D"));
                section4SelectedBadge.Visibility = Visibility.Visible;
            }
            else if (_selectedSeries != null)
            {
                section4SelectedText.Text = _selectedSeries.SeriesName;
                section4SelectedBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_selectedMainCat?.ColorCode ?? "#7F8C8D"));
                section4SelectedBadge.Visibility = Visibility.Visible;
            }
        }

        private void RefreshGridUI()
        {
            // Grid 패널 업데이트
            CreateGridMainCategoryCards();

            if (_selectedMainCat != null)
            {
                CreateGridSubCategoryCards();
                gridColumn2Header.Text = _isStandardMode ? "CATEGORY · 분류" : "VENDOR · 업체";
            }

            if (_selectedSubCat != null)
            {
                if (_isStandardMode)
                {
                    CreateGridMidCategoryCards();
                    gridColumn3Header.Text = _selectedSubCat.SubCatNameKr;
                }
                else
                {
                    CreateGridPartTypeCardsInMidPanel();
                    gridColumn3Header.Text = _selectedSubCat.SubCatNameKr;
                }
            }

            if (_selectedMidCat != null)
            {
                CreateGridPartTypeCards();
                gridColumn4Header.Text = _selectedMidCat.MidCatNameKr;
            }
            else if (_selectedPartType != null && !_isStandardMode && _selectedPartType.HasSeries)
            {
                CreateGridSeriesCards();
                gridColumn4Header.Text = _selectedPartType.PartTypeNameKr;
            }
        }

        #endregion

        #region Accordion UI Creation

        private void CreateAccordionMainCategoryCards()
        {
            mainCategoryPanel.Children.Clear();
            foreach (var item in _mainCategories)
            {
                var card = CreateListItem(item.MainCatNameKr, item.ColorCode, item, null, false);
                if (_selectedMainCat == item)
                {
                    _selectedMainCatBorder = card;
                    SelectBorder(card, item.ColorCode);
                }
                mainCategoryPanel.Children.Add(card);
            }
        }

        private void CreateAccordionSubCategoryCards()
        {
            subCategoryPanel.Children.Clear();
            string color = _selectedMainCat?.ColorCode ?? "#7F8C8D";
            foreach (var item in _subCategories)
            {
                string prefix = item.IsVendor ? GetCountryFlag(item.Country) : null;
                var card = CreateListItem(item.SubCatNameKr, color, item, prefix, false);
                if (_selectedSubCat == item)
                {
                    _selectedSubCatBorder = card;
                    SelectBorder(card, color);
                }
                subCategoryPanel.Children.Add(card);
            }
        }

        private void CreateAccordionMidCategoryCards()
        {
            midCategoryPanel.Children.Clear();
            string color = _selectedMainCat?.ColorCode ?? "#7F8C8D";
            foreach (var item in _midCategories)
            {
                var card = CreateListItem(item.MidCatNameKr, color, item, null, false);
                if (_selectedMidCat == item)
                {
                    _selectedMidCatBorder = card;
                    SelectBorder(card, color);
                }
                midCategoryPanel.Children.Add(card);
            }
        }

        private void CreateAccordionPartTypeCards()
        {
            partTypePanel.Children.Clear();
            string color = _selectedMainCat?.ColorCode ?? "#7F8C8D";
            foreach (var item in _partTypes)
            {
                var card = CreateListItem(item.PartTypeNameKr, color, item, null, false);
                if (_selectedPartType == item)
                {
                    _selectedPartTypeBorder = card;
                    SelectBorder(card, color);
                }
                partTypePanel.Children.Add(card);
            }
        }

        private void CreateAccordionPartTypeCardsInMidPanel()
        {
            midCategoryPanel.Children.Clear();
            string color = _selectedMainCat?.ColorCode ?? "#7F8C8D";
            foreach (var item in _partTypes)
            {
                var card = CreateListItemForMidPanel(item.PartTypeNameKr, color, item, false);
                if (_selectedPartType == item)
                {
                    _selectedMidCatBorder = card;
                    SelectBorder(card, color);
                }
                midCategoryPanel.Children.Add(card);
            }
        }

        private void CreateAccordionSeriesCards()
        {
            partTypePanel.Children.Clear();
            string color = _selectedMainCat?.ColorCode ?? "#7F8C8D";
            foreach (var item in _series)
            {
                var card = CreateListItem(item.SeriesName, color, item, null, false);
                if (_selectedSeries == item)
                {
                    _selectedSeriesBorder = card;
                    SelectBorder(card, color);
                }
                partTypePanel.Children.Add(card);
            }
        }

        #endregion

        #region Grid UI Creation

        private void CreateGridMainCategoryCards()
        {
            gridMainCategoryPanel.Children.Clear();
            foreach (var item in _mainCategories)
            {
                var card = CreateListItem(item.MainCatNameKr, item.ColorCode, item, null, true);
                if (_selectedMainCat == item)
                {
                    _gridSelectedMainCatBorder = card;
                    SelectBorder(card, item.ColorCode);
                }
                gridMainCategoryPanel.Children.Add(card);
            }
        }

        private void CreateGridSubCategoryCards()
        {
            gridSubCategoryPanel.Children.Clear();
            string color = _selectedMainCat?.ColorCode ?? "#7F8C8D";
            foreach (var item in _subCategories)
            {
                string prefix = item.IsVendor ? GetCountryFlag(item.Country) : null;
                var card = CreateListItem(item.SubCatNameKr, color, item, prefix, true);
                if (_selectedSubCat == item)
                {
                    _gridSelectedSubCatBorder = card;
                    SelectBorder(card, color);
                }
                gridSubCategoryPanel.Children.Add(card);
            }
        }

        private void CreateGridMidCategoryCards()
        {
            gridMidCategoryPanel.Children.Clear();
            string color = _selectedMainCat?.ColorCode ?? "#7F8C8D";
            foreach (var item in _midCategories)
            {
                var card = CreateListItem(item.MidCatNameKr, color, item, null, true);
                if (_selectedMidCat == item)
                {
                    _gridSelectedMidCatBorder = card;
                    SelectBorder(card, color);
                }
                gridMidCategoryPanel.Children.Add(card);
            }
        }

        private void CreateGridPartTypeCards()
        {
            gridPartTypePanel.Children.Clear();
            string color = _selectedMainCat?.ColorCode ?? "#7F8C8D";
            foreach (var item in _partTypes)
            {
                var card = CreateListItem(item.PartTypeNameKr, color, item, null, true);
                if (_selectedPartType == item)
                {
                    _gridSelectedPartTypeBorder = card;
                    SelectBorder(card, color);
                }
                gridPartTypePanel.Children.Add(card);
            }
        }

        private void CreateGridPartTypeCardsInMidPanel()
        {
            gridMidCategoryPanel.Children.Clear();
            string color = _selectedMainCat?.ColorCode ?? "#7F8C8D";
            foreach (var item in _partTypes)
            {
                var card = CreateListItemForMidPanel(item.PartTypeNameKr, color, item, true);
                if (_selectedPartType == item)
                {
                    _gridSelectedMidCatBorder = card;
                    SelectBorder(card, color);
                }
                gridMidCategoryPanel.Children.Add(card);
            }
        }

        private void CreateGridSeriesCards()
        {
            gridPartTypePanel.Children.Clear();
            string color = _selectedMainCat?.ColorCode ?? "#7F8C8D";
            foreach (var item in _series)
            {
                var card = CreateListItem(item.SeriesName, color, item, null, true);
                if (_selectedSeries == item)
                {
                    _gridSelectedSeriesBorder = card;
                    SelectBorder(card, color);
                }
                gridPartTypePanel.Children.Add(card);
            }
        }

        #endregion

        #region Common UI Creation

        private Border CreateListItem(string text, string color, object tag, string prefix, bool isGridMode)
        {
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D44")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 3),
                Cursor = Cursors.Hand,
                Tag = tag
            };

            var sp = new StackPanel { Orientation = Orientation.Horizontal };

            if (!string.IsNullOrEmpty(prefix))
            {
                sp.Children.Add(new TextBlock
                {
                    Text = prefix,
                    FontSize = 10,
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            sp.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            border.Child = sp;

            // 클릭 이벤트
            if (tag is MainCategoryItem mc)
                border.MouseLeftButtonDown += (s, e) => SelectMainCategory(mc, border, isGridMode);
            else if (tag is SubCategoryItem sc)
                border.MouseLeftButtonDown += (s, e) => SelectSubCategory(sc, border, isGridMode);
            else if (tag is MidCategoryItem mid)
                border.MouseLeftButtonDown += (s, e) => SelectMidCategory(mid, border, isGridMode);
            else if (tag is PartTypeItem pt)
                border.MouseLeftButtonDown += (s, e) => SelectPartType(pt, border, isGridMode);
            else if (tag is SeriesItem sr)
                border.MouseLeftButtonDown += (s, e) => SelectSeries(sr, border, isGridMode);

            return border;
        }

        private Border CreateListItemForMidPanel(string text, string color, PartTypeItem item, bool isGridMode)
        {
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D44")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 3),
                Cursor = Cursors.Hand,
                Tag = item
            };

            border.Child = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            border.MouseLeftButtonDown += (s, e) => SelectPartTypeFromMidPanel(item, border, isGridMode);

            return border;
        }

        #endregion

        #region Selection Logic

        private void SelectMainCategory(MainCategoryItem item, Border border, bool isGridMode)
        {
            // 이전 선택 해제
            if (isGridMode)
                DeselectBorder(ref _gridSelectedMainCatBorder);
            else
                DeselectBorder(ref _selectedMainCatBorder);

            _selectedMainCat = item;
            _isStandardMode = item.IsStandard;

            // 현재 선택 표시
            if (isGridMode)
            {
                _gridSelectedMainCatBorder = border;
            }
            else
            {
                _selectedMainCatBorder = border;
                section1SelectedText.Text = item.MainCatNameKr;
                section1SelectedBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.ColorCode));
                section1SelectedBadge.Visibility = Visibility.Visible;
                CollapseSection(1);
            }
            SelectBorder(border, item.ColorCode);

            // 하위 초기화
            ClearSubSelections();

            // 서브카테고리 로드
            LoadSubCategories(item.MainCatCode);

            if (isGridMode)
            {
                CreateGridSubCategoryCards();
                gridColumn2Header.Text = _isStandardMode ? "CATEGORY · 분류" : "VENDOR · 업체";
                gridMidCategoryPanel.Children.Clear();
                gridPartTypePanel.Children.Clear();
            }
            else
            {
                CreateAccordionSubCategoryCards();
                section2Border.Visibility = Visibility.Visible;
                ExpandSection(2);
                section2Title.Text = _isStandardMode ? "CATEGORY · 분류" : "VENDOR · 업체";
            }

            UpdateSelectionInfo();
        }

        private void SelectSubCategory(SubCategoryItem item, Border border, bool isGridMode)
        {
            if (isGridMode)
                DeselectBorder(ref _gridSelectedSubCatBorder);
            else
                DeselectBorder(ref _selectedSubCatBorder);

            _selectedSubCat = item;

            if (isGridMode)
            {
                _gridSelectedSubCatBorder = border;
            }
            else
            {
                _selectedSubCatBorder = border;
                section2SelectedText.Text = item.SubCatNameKr;
                section2SelectedBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_selectedMainCat?.ColorCode ?? "#7F8C8D"));
                section2SelectedBadge.Visibility = Visibility.Visible;
                CollapseSection(2);
            }
            SelectBorder(border, _selectedMainCat?.ColorCode ?? "#7F8C8D");

            // 하위 초기화
            _selectedMidCat = null;
            _selectedPartType = null;
            _selectedSeries = null;

            if (_isStandardMode)
            {
                LoadMidCategories(item.SubCatCode);
                if (isGridMode)
                {
                    CreateGridMidCategoryCards();
                    gridColumn3Header.Text = item.SubCatNameKr;
                    gridPartTypePanel.Children.Clear();
                }
                else
                {
                    CreateAccordionMidCategoryCards();
                    section3Title.Text = item.SubCatNameKr;
                    section3Border.Visibility = Visibility.Visible;
                    ExpandSection(3);
                    section4Border.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                LoadPartTypes(null, item.SubCatCode);
                if (isGridMode)
                {
                    CreateGridPartTypeCardsInMidPanel();
                    gridColumn3Header.Text = item.SubCatNameKr;
                    gridPartTypePanel.Children.Clear();
                }
                else
                {
                    CreateAccordionPartTypeCardsInMidPanel();
                    section3Title.Text = item.SubCatNameKr;
                    section3Border.Visibility = Visibility.Visible;
                    ExpandSection(3);
                    section4Border.Visibility = Visibility.Collapsed;
                }
            }

            // Badge 초기화
            if (!isGridMode)
            {
                section3SelectedBadge.Visibility = Visibility.Collapsed;
                section4SelectedBadge.Visibility = Visibility.Collapsed;
            }

            UpdateSelectionInfo();
        }

        private void SelectMidCategory(MidCategoryItem item, Border border, bool isGridMode)
        {
            if (isGridMode)
                DeselectBorder(ref _gridSelectedMidCatBorder);
            else
                DeselectBorder(ref _selectedMidCatBorder);

            _selectedMidCat = item;
            _selectedPartType = null;
            _selectedSeries = null;

            if (isGridMode)
            {
                _gridSelectedMidCatBorder = border;
            }
            else
            {
                _selectedMidCatBorder = border;
                section3SelectedText.Text = item.MidCatNameKr;
                section3SelectedBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_selectedMainCat?.ColorCode ?? "#7F8C8D"));
                section3SelectedBadge.Visibility = Visibility.Visible;
                CollapseSection(3);
            }
            SelectBorder(border, _selectedMainCat?.ColorCode ?? "#7F8C8D");

            LoadPartTypes(item.MidCatCode, null);

            if (isGridMode)
            {
                CreateGridPartTypeCards();
                gridColumn4Header.Text = item.MidCatNameKr;
            }
            else
            {
                CreateAccordionPartTypeCards();
                section4Title.Text = item.MidCatNameKr;
                section4Border.Visibility = Visibility.Visible;
                ExpandSection(4);
                section4SelectedBadge.Visibility = Visibility.Collapsed;
            }

            UpdateSelectionInfo();
        }

        private void SelectPartTypeFromMidPanel(PartTypeItem item, Border border, bool isGridMode)
        {
            if (isGridMode)
                DeselectBorder(ref _gridSelectedMidCatBorder);
            else
                DeselectBorder(ref _selectedMidCatBorder);

            _selectedPartType = item;
            _selectedSeries = null;

            if (isGridMode)
            {
                _gridSelectedMidCatBorder = border;
            }
            else
            {
                _selectedMidCatBorder = border;
                section3SelectedText.Text = item.PartTypeNameKr;
                section3SelectedBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_selectedMainCat?.ColorCode ?? "#7F8C8D"));
                section3SelectedBadge.Visibility = Visibility.Visible;
                CollapseSection(3);
            }
            SelectBorder(border, _selectedMainCat?.ColorCode ?? "#7F8C8D");

            if (item.HasSeries)
            {
                LoadSeries(item.PartTypeCode);
                if (isGridMode)
                {
                    CreateGridSeriesCards();
                    gridColumn4Header.Text = item.PartTypeNameKr;
                }
                else
                {
                    CreateAccordionSeriesCards();
                    section4Title.Text = item.PartTypeNameKr;
                    section4Border.Visibility = Visibility.Visible;
                    ExpandSection(4);
                    section4SelectedBadge.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                if (isGridMode)
                {
                    gridPartTypePanel.Children.Clear();
                }
                else
                {
                    section4Border.Visibility = Visibility.Collapsed;
                }
            }

            UpdateSelectionInfo();
        }

        private void SelectPartType(PartTypeItem item, Border border, bool isGridMode)
        {
            if (isGridMode)
                DeselectBorder(ref _gridSelectedPartTypeBorder);
            else
                DeselectBorder(ref _selectedPartTypeBorder);

            _selectedPartType = item;
            _selectedSeries = null;

            if (isGridMode)
            {
                _gridSelectedPartTypeBorder = border;
            }
            else
            {
                _selectedPartTypeBorder = border;
                section4SelectedText.Text = item.PartTypeNameKr;
                section4SelectedBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_selectedMainCat?.ColorCode ?? "#7F8C8D"));
                section4SelectedBadge.Visibility = Visibility.Visible;
            }
            SelectBorder(border, _selectedMainCat?.ColorCode ?? "#7F8C8D");

            UpdateSelectionInfo();
        }

        private void SelectSeries(SeriesItem item, Border border, bool isGridMode)
        {
            if (isGridMode)
                DeselectBorder(ref _gridSelectedSeriesBorder);
            else
                DeselectBorder(ref _selectedSeriesBorder);

            _selectedSeries = item;

            if (isGridMode)
            {
                _gridSelectedSeriesBorder = border;
            }
            else
            {
                _selectedSeriesBorder = border;
                section4SelectedText.Text = item.SeriesName;
                section4SelectedBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_selectedMainCat?.ColorCode ?? "#7F8C8D"));
                section4SelectedBadge.Visibility = Visibility.Visible;
            }
            SelectBorder(border, _selectedMainCat?.ColorCode ?? "#7F8C8D");

            UpdateSelectionInfo();
        }

        private void ClearSubSelections()
        {
            _selectedSubCat = null;
            _selectedMidCat = null;
            _selectedPartType = null;
            _selectedSeries = null;

            // Accordion
            DeselectBorder(ref _selectedSubCatBorder);
            DeselectBorder(ref _selectedMidCatBorder);
            DeselectBorder(ref _selectedPartTypeBorder);
            DeselectBorder(ref _selectedSeriesBorder);

            // Grid
            DeselectBorder(ref _gridSelectedSubCatBorder);
            DeselectBorder(ref _gridSelectedMidCatBorder);
            DeselectBorder(ref _gridSelectedPartTypeBorder);
            DeselectBorder(ref _gridSelectedSeriesBorder);

            subCategoryPanel.Children.Clear();
            midCategoryPanel.Children.Clear();
            partTypePanel.Children.Clear();

            gridSubCategoryPanel.Children.Clear();
            gridMidCategoryPanel.Children.Clear();
            gridPartTypePanel.Children.Clear();

            section2Border.Visibility = Visibility.Collapsed;
            section3Border.Visibility = Visibility.Collapsed;
            section4Border.Visibility = Visibility.Collapsed;

            section2SelectedBadge.Visibility = Visibility.Collapsed;
            section3SelectedBadge.Visibility = Visibility.Collapsed;
            section4SelectedBadge.Visibility = Visibility.Collapsed;
        }

        private void ClearAllSelections()
        {
            DeselectBorder(ref _selectedMainCatBorder);
            DeselectBorder(ref _gridSelectedMainCatBorder);
            _selectedMainCat = null;
            section1SelectedBadge.Visibility = Visibility.Collapsed;
            ExpandSection(1);

            ClearSubSelections();
            UpdateSelectionInfo();
        }

        private void SelectBorder(Border border, string color)
        {
            if (border == null) return;
            border.BorderThickness = new Thickness(2);
            border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3D3D5C"));
        }

        private void DeselectBorder(ref Border border)
        {
            if (border == null) return;
            border.BorderThickness = new Thickness(1);
            border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D44"));
            border = null;
        }

        private void UpdateSelectionInfo()
        {
            var items = new List<string>();

            if (_selectedMainCat != null) items.Add(_selectedMainCat.MainCatNameKr);
            if (_selectedSubCat != null) items.Add(_selectedSubCat.SubCatNameKr);
            if (_selectedMidCat != null) items.Add(_selectedMidCat.MidCatNameKr);
            if (_selectedPartType != null) items.Add(_selectedPartType.PartTypeNameKr);
            if (_selectedSeries != null) items.Add(_selectedSeries.SeriesName);

            txtSelectionSummary.Text = items.Count > 0
                ? string.Join(" > ", items)
                : "카테고리를 선택하세요";

            bool isComplete = false;
            if (_isStandardMode)
            {
                isComplete = _selectedMainCat != null && _selectedSubCat != null &&
                            _selectedMidCat != null && _selectedPartType != null;
            }
            else
            {
                isComplete = _selectedMainCat != null && _selectedSubCat != null && _selectedPartType != null;
                if (_selectedPartType?.HasSeries == true)
                    isComplete = isComplete && _selectedSeries != null;
            }
            btnConfirm.IsEnabled = isComplete;
        }

        private string GetCountryFlag(string country)
        {
            switch (country)
            {
                case "KR": return "🇰🇷";
                case "JP": return "🇯🇵";
                case "DE": return "🇩🇪";
                case "TW": return "🇹🇼";
                default: return "";
            }
        }

        #endregion
    }
}
