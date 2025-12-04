using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BoltSpecProgram.UI
{
    /// <summary>
    /// 동적 UI를 관리하는 클래스
    /// </summary>
    public class DynamicUIManager
    {
        private MainWindow _mainWindow;
        private BoltSpecData _data;
        private BoltSpecData _filteredData;  // ✅ 종류별 필터링된 데이터
        private Dictionary<string, ControlInfo> _controls = new Dictionary<string, ControlInfo>();
        private Dictionary<int, string> _selectedPath = new Dictionary<int, string>();
        private HierarchyTreeBuilder _treeBuilder;
        private List<HierarchyNode> _hierarchyTree;
        private Canvas _canvas1;
        private Canvas _canvas2;
        private Point _dragStartPoint;
        private bool _isDragging = false;
        private Border _draggedBorder = null;
        
        // ✅ 분류 콤보박스 관련 (Headers[0])
        private string _classificationColumnName;    // 분류 컬럼 이름
        private List<string> _allClassifications;    // 모든 분류 목록
        private string _selectedClassification;      // 현재 선택된 분류
        private ComboBox _classificationComboBox;    // 분류 콤보박스
        
        // ✅ 종류 콤보박스 관련 (Headers[1])
        private string _categoryColumnName;           // 종류 컬럼 이름
        private List<string> _allCategories;          // 모든 종류 목록 (현재 분류에 속한)
        private string _selectedCategory;             // 현재 선택된 종류
        private ComboBox _categoryComboBox;           // 종류 콤보박스
        private List<int> _hierarchyColumns;          // 계층 컬럼 인덱스
        private bool _isUpdatingCategory = false;     // 종류 변경 중 플래그
        private bool _isUpdatingClassification = false; // 분류 변경 중 플래그
        
        // ✅ 컨트롤 위치 저장 (종류 변경 시에도 유지)
        private Dictionary<string, SavedControlPosition> _savedControlPositions = new Dictionary<string, SavedControlPosition>();
        
        // Canvas 초기 크기
        private const double INITIAL_CANVAS1_WIDTH = 600;
        private const double INITIAL_CANVAS1_HEIGHT = 400;
        private const double INITIAL_CANVAS2_WIDTH = 600;
        private const double INITIAL_CANVAS2_HEIGHT = 600;
        
        // ✅ JSON 내보내기용 Public 속성
        /// <summary>
        /// 현재 선택된 분류 이름 (예: "볼트류", "너트류")
        /// </summary>
        public string SelectedClassification => _selectedClassification;
        
        /// <summary>
        /// 현재 선택된 종류 이름 (예: "육각머리볼트", "T홈볼트")
        /// </summary>
        public string SelectedCategory => _selectedCategory;
        
        /// <summary>
        /// 현재 선택된 종류의 필터링된 데이터 (JSON 내보내기용)
        /// </summary>
        public BoltSpecData FilteredData => _filteredData ?? _data;
        
        /// <summary>
        /// 종류 컬럼 이름 (Headers[1])
        /// </summary>
        public string CategoryColumnName => _categoryColumnName;
        
        /// <summary>
        /// 분류 컬럼 이름 (Headers[0])
        /// </summary>
        public string ClassificationColumnName => _classificationColumnName;
        
        /// <summary>
        /// 모든 분류 목록 (Public)
        /// </summary>
        public List<string> AllClassifications => _allClassifications ?? new List<string>();
        
        /// <summary>
        /// 특정 분류에 속하는 종류 목록 반환 (Public)
        /// </summary>
        public List<string> GetCategoriesForClassification(string classification)
        {
            return GetCategoriesByClassification(classification);
        }
        
        /// <summary>
        /// 전체 데이터 (Public - 전체 DB 저장용)
        /// </summary>
        public BoltSpecData FullData => _data;

        public DynamicUIManager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public void BuildUI(BoltSpecData data)
        {
            _data = data;
            _controls.Clear();
            _selectedPath.Clear();

            _canvas1 = _mainWindow.DynamicControlCanvas1;
            _canvas2 = _mainWindow.DynamicControlCanvas2;
            _canvas1.Children.Clear();
            _canvas2.Children.Clear();

            // ✅ 분류 컬럼 이름 저장 (Headers[0])
            if (data.Headers.Count > 0 && !string.IsNullOrWhiteSpace(data.Headers[0]))
            {
                _classificationColumnName = data.Headers[0];
                
                // ✅ 모든 분류 목록 수집
                _allClassifications = GetAllClassifications();
            }
            
            // ✅ 종류 컬럼 이름 저장 (Headers[1])
            if (data.Headers.Count > 1 && !string.IsNullOrWhiteSpace(data.Headers[1]))
            {
                _categoryColumnName = data.Headers[1];
            }
            
            // ✅ 분류/종류 콤보박스 표시
            DisplayClassificationAndCategoryComboBox();
            
            // ✅ 첫 번째 분류 선택 (종류는 분류 변경 이벤트에서 처리)
            if (_allClassifications != null && _allClassifications.Count > 0 && _classificationComboBox != null)
            {
                _classificationComboBox.SelectedIndex = 0;
                return; // SelectionChanged 이벤트에서 나머지 처리
            }
            
            // ✅ 분류가 없는 경우 기존 방식으로 처리
            if (_categoryColumnName != null)
            {
                _allCategories = GetAllCategories();
                if (_allCategories.Count > 0 && _categoryComboBox != null)
                {
                    _categoryComboBox.SelectedIndex = 0;
                    return;
                }
            }

            // ✅ 분류/종류가 없는 경우 기존 방식으로 처리
            BuildUIForCurrentCategory();
        }
        
        /// <summary>
        /// 모든 분류(Classification) 목록을 가져옴
        /// </summary>
        private List<string> GetAllClassifications()
        {
            var classifications = new HashSet<string>();
            
            if (string.IsNullOrWhiteSpace(_classificationColumnName))
                return new List<string>();
            
            foreach (var row in _data.DataRows)
            {
                if (row.CompleteValues.ContainsKey(_classificationColumnName))
                {
                    var value = row.CompleteValues[_classificationColumnName]?.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        classifications.Add(value);
                    }
                }
            }
            
            return classifications.OrderBy(c => c).ToList();
        }
        
        /// <summary>
        /// 현재 선택된 분류에 속하는 종류 목록을 가져옴
        /// </summary>
        private List<string> GetCategoriesByClassification(string classificationName)
        {
            var categories = new HashSet<string>();
            
            if (string.IsNullOrWhiteSpace(_categoryColumnName))
                return new List<string>();
            
            foreach (var row in _data.DataRows)
            {
                // 분류가 일치하는 행만 확인
                bool classificationMatch = string.IsNullOrWhiteSpace(_classificationColumnName) ||
                    (row.CompleteValues.ContainsKey(_classificationColumnName) && 
                     row.CompleteValues[_classificationColumnName]?.Trim() == classificationName);
                
                if (classificationMatch && row.CompleteValues.ContainsKey(_categoryColumnName))
                {
                    var value = row.CompleteValues[_categoryColumnName]?.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        categories.Add(value);
                    }
                }
            }
            
            return categories.OrderBy(c => c).ToList();
        }
        
        /// <summary>
        /// 모든 종류 목록을 가져옴 (분류와 무관하게)
        /// </summary>
        private List<string> GetAllCategories()
        {
            var categories = new HashSet<string>();
            
            if (string.IsNullOrWhiteSpace(_categoryColumnName))
                return new List<string>();
            
            foreach (var row in _data.DataRows)
            {
                if (row.CompleteValues.ContainsKey(_categoryColumnName))
                {
                    var value = row.CompleteValues[_categoryColumnName]?.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        categories.Add(value);
                    }
                }
            }
            
            return categories.OrderBy(c => c).ToList();
        }
        
        /// <summary>
        /// 선택된 종류에 해당하는 데이터만 필터링
        /// </summary>
        private BoltSpecData FilterDataByCategory(string category)
        {
            var filtered = new BoltSpecData
            {
                Headers = _data.Headers,
                NameCodeMap = _data.NameCodeMap  // ✅ Name_Code 매핑도 복사
            };
            
            foreach (var row in _data.DataRows)
            {
                if (row.CompleteValues.ContainsKey(_categoryColumnName))
                {
                    var value = row.CompleteValues[_categoryColumnName]?.Trim();
                    if (value == category)
                    {
                        filtered.DataRows.Add(row);
                    }
                }
            }
            
            // ✅ 디버그 로그: 필터링된 데이터 확인
            Console.WriteLine($"[FilterDataByCategory] 종류: {category}, 필터링된 행 수: {filtered.DataRows.Count}");
            
            // ✅ 국제산업표준 값들 확인
            if (filtered.Headers.Count > 2)
            {
                var stdColumnName = filtered.Headers[2];
                var stdValues = filtered.DataRows
                    .Select(r => r.CompleteValues.ContainsKey(stdColumnName) ? r.CompleteValues[stdColumnName] : "")
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct()
                    .ToList();
                Console.WriteLine($"[FilterDataByCategory] {stdColumnName} 값들: {string.Join(", ", stdValues)}");
            }
            
            return filtered;
        }
        
        /// <summary>
        /// 현재 선택된 종류에 대한 UI 구성
        /// </summary>
        private void BuildUIForCurrentCategory()
        {
            // 캔버스 초기화
            _canvas1.Children.Clear();
            _canvas2.Children.Clear();
            _controls.Clear();
            _selectedPath.Clear();
            
            // ✅ 사용할 데이터 결정 (필터링된 데이터 또는 전체 데이터)
            var dataToUse = _filteredData ?? _data;

            // ⭐ 계층형 컬럼 인덱스 (Excel 컬럼 인덱스 기준)
            // Headers[2]~Headers[8]: 타입, 규격(표준번호), 용도, 재질, 나사종류, 머리형식, 사이즈
            _hierarchyColumns = new List<int>();
            for (int i = 2; i <= Math.Min(dataToUse.Headers.Count - 1, 8); i++)
            {
                if (i < dataToUse.Headers.Count && !string.IsNullOrWhiteSpace(dataToUse.Headers[i]))
                {
                    _hierarchyColumns.Add(i);
                }
            }

            _treeBuilder = new HierarchyTreeBuilder(dataToUse, _hierarchyColumns);
            _hierarchyTree = _treeBuilder.BuildTree();
            
            // ✅ 디버그 로그: 트리의 첫 번째 계층 값들 확인
            if (_hierarchyTree != null && _hierarchyTree.Count > 0)
            {
                var firstLevelValues = _hierarchyTree.Select(n => n.Value).ToList();
                Console.WriteLine($"[BuildUIForCurrentCategory] 첫 번째 계층({_hierarchyColumns.FirstOrDefault()}) 값들: {string.Join(", ", firstLevelValues)}");
            }

            double currentX = 10;
            double currentY = 10;
            double maxWidth = 0;

            // ⭐ 계층형 컬럼들을 동적으로 생성 (타입부터 시작)
            foreach (var colIndex in _hierarchyColumns)
            {
                CreateDynamicControl(colIndex, _hierarchyColumns, ref currentX, ref currentY, ref maxWidth);
            }

            // ⭐ 리프 컬럼들 생성 (사이즈 다음 컬럼들)
            for (int i = 9; i < dataToUse.Headers.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(dataToUse.Headers[i]))
                {
                    CreateLeafControl(i, ref currentX, ref currentY, ref maxWidth);
                }
            }
            
            // ⭐ 모든 컴포넌트 생성 후 Canvas 크기를 자동으로 조정
            AdjustCanvasSize();
            
            // ✅ 현재 컨트롤 위치를 저장 (아직 저장되지 않은 것만)
            SaveCurrentControlPositionsIfNotExists();
            
            // ✅ 첫 번째 계층 컨트롤의 첫 번째 아이템 자동 선택 (연쇄 반응 시작)
            AutoSelectFirstHierarchyControl();
        }
        
        /// <summary>
        /// 현재 컨트롤 위치를 저장 (아직 저장되지 않은 것만)
        /// - 사용자가 Ctrl+M으로 이동한 위치는 유지
        /// - 새로 생성된 컨트롤만 기본 위치 저장
        /// </summary>
        private void SaveCurrentControlPositionsIfNotExists()
        {
            foreach (var kvp in _controls)
            {
                string columnName = kvp.Key;
                var controlInfo = kvp.Value;
                
                // ✅ 아직 저장되지 않았거나, IsUserModified가 false인 경우만 저장
                if (!_savedControlPositions.ContainsKey(columnName))
                {
                    _savedControlPositions[columnName] = new SavedControlPosition
                    {
                        Left = controlInfo.Left,
                        Top = controlInfo.Top,
                        CanvasNumber = controlInfo.CanvasNumber,
                        IsUserModified = false  // 기본 위치
                    };
                    Console.WriteLine($"[Position Init] {columnName}: Left={controlInfo.Left}, Top={controlInfo.Top}, Canvas={controlInfo.CanvasNumber}");
                }
            }
        }
        
        /// <summary>
        /// 첫 번째 계층 컨트롤의 첫 번째 아이템을 자동 선택
        /// </summary>
        private void AutoSelectFirstHierarchyControl()
        {
            if (_hierarchyColumns == null || _hierarchyColumns.Count == 0)
                return;
                
            var dataToUse = _filteredData ?? _data;
            int firstColIndex = _hierarchyColumns.First();
            string firstColumnName = dataToUse.Headers[firstColIndex];
            
            if (_controls.ContainsKey(firstColumnName))
            {
                var controlInfo = _controls[firstColumnName];
                
                if (controlInfo.Control is ComboBox comboBox && comboBox.Items.Count > 0)
                {
                    comboBox.SelectedIndex = 0;
                }
                else if (controlInfo.Control is ListBox listBox && listBox.Items.Count > 0)
                {
                    listBox.SelectedIndex = 0;
                }
            }
        }
        
        /// <summary>
        /// 최상단에 "분류" 및 "종류" 콤보박스 표시
        /// </summary>
        private void DisplayClassificationAndCategoryComboBox()
        {
            _mainWindow.CategoryPanel.Children.Clear();
            
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // ✅ 분류 콤보박스 (Headers[0])
            if (!string.IsNullOrWhiteSpace(_classificationColumnName) && 
                _allClassifications != null && _allClassifications.Count > 0)
            {
                var classLabel = new Label
                {
                    Content = $"{_classificationColumnName}:",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 51, 102)),
                    Padding = new Thickness(10, 5, 5, 5),
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                _classificationComboBox = new ComboBox
                {
                    Width = 120,
                    Height = 28,
                    FontSize = 13,
                    Margin = new Thickness(5, 5, 15, 5),
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                foreach (var classification in _allClassifications)
                {
                    _classificationComboBox.Items.Add(classification);
                }
                
                _classificationComboBox.SelectionChanged += OnClassificationSelectionChanged;
                
                stackPanel.Children.Add(classLabel);
                stackPanel.Children.Add(_classificationComboBox);
            }
            
            // ✅ 종류 콤보박스 (Headers[1])
            if (!string.IsNullOrWhiteSpace(_categoryColumnName))
            {
                var categoryLabel = new Label
                {
                    Content = $"{_categoryColumnName}:",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 102, 204)),
                    Padding = new Thickness(10, 5, 5, 5),
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                _categoryComboBox = new ComboBox
                {
                    Width = 200,
                    Height = 28,
                    FontSize = 13,
                    Margin = new Thickness(5, 5, 10, 5),
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                // 종류 목록은 분류 선택 후 채워짐
                _categoryComboBox.SelectionChanged += OnCategorySelectionChanged;
                
                stackPanel.Children.Add(categoryLabel);
                stackPanel.Children.Add(_categoryComboBox);
            }
            
            _mainWindow.CategoryPanel.Children.Add(stackPanel);
        }
        
        /// <summary>
        /// 분류 콤보박스 선택 변경 이벤트
        /// </summary>
        private void OnClassificationSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingClassification) return;
            
            var comboBox = sender as ComboBox;
            if (comboBox?.SelectedItem == null) return;
            
            _selectedClassification = comboBox.SelectedItem.ToString();
            Console.WriteLine($"[Classification Changed] {_selectedClassification}");
            
            // ✅ 해당 분류에 속하는 종류 목록 업데이트
            _isUpdatingClassification = true;
            try
            {
                _allCategories = GetCategoriesByClassification(_selectedClassification);
                
                if (_categoryComboBox != null)
                {
                    _categoryComboBox.Items.Clear();
                    foreach (var category in _allCategories)
                    {
                        _categoryComboBox.Items.Add(category);
                    }
                    
                    // 첫 번째 종류 자동 선택
                    if (_allCategories.Count > 0)
                    {
                        _categoryComboBox.SelectedIndex = 0;
                    }
                }
            }
            finally
            {
                _isUpdatingClassification = false;
            }
        }
        
        /// <summary>
        /// 종류 콤보박스 선택 변경 이벤트
        /// </summary>
        private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingCategory || _isUpdatingClassification) return;
            
            var comboBox = sender as ComboBox;
            if (comboBox?.SelectedItem == null) return;
            
            _selectedCategory = comboBox.SelectedItem.ToString();
            Console.WriteLine($"[Category Changed] {_selectedCategory}");
            
            // ✅ 선택된 종류로 데이터 필터링
            _filteredData = FilterDataByCategory(_selectedCategory);
            Console.WriteLine($"[Filtered Data] {_filteredData.DataRows.Count} rows");
            
            // ✅ UI 재구성
            _isUpdatingCategory = true;
            try
            {
                BuildUIForCurrentCategory();
            }
            finally
            {
                _isUpdatingCategory = false;
            }
        }

        /// <summary>
        /// Canvas 크기를 컴포넌트와 ScrollViewer 크기에 맞게 자동 조정
        /// </summary>
        private void AdjustCanvasSize()
        {
            AdjustSingleCanvasSize(_canvas1, _mainWindow.ScrollViewer1);
            AdjustSingleCanvasSize(_canvas2, _mainWindow.ScrollViewer2);
        }
        
        /// <summary>
        /// 외부에서 호출 가능한 Canvas 크기 재조정 메서드
        /// (데이터 보기 패널 토글 시 사용)
        /// </summary>
        public void RefreshCanvasSize()
        {
            // ScrollViewer의 크기 변경이 완전히 반영되도록 약간의 지연 후 실행
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Render,
                new Action(() =>
                {
                    // ✅ 먼저 전체 레이아웃을 강제로 업데이트
                    // 화면 밖으로 나갔던 컨트롤들의 크기 정보를 갱신
                    _mainWindow.UpdateLayout();
                    
                    // ScrollViewer와 Canvas의 레이아웃도 명시적으로 갱신
                    if (_mainWindow.ScrollViewer1 != null)
                    {
                        _mainWindow.ScrollViewer1.UpdateLayout();
                    }
                    if (_mainWindow.ScrollViewer2 != null)
                    {
                        _mainWindow.ScrollViewer2.UpdateLayout();
                    }
                    
                    if (_canvas1 != null)
                    {
                        _canvas1.UpdateLayout();
                        
                        // ✅ Canvas 내의 모든 컨트롤 레이아웃 강제 갱신
                        foreach (UIElement child in _canvas1.Children)
                        {
                            if (child is FrameworkElement element)
                            {
                                element.UpdateLayout();
                            }
                        }
                    }
                    
                    if (_canvas2 != null)
                    {
                        _canvas2.UpdateLayout();
                        
                        // ✅ Canvas 내의 모든 컨트롤 레이아웃 강제 갱신
                        foreach (UIElement child in _canvas2.Children)
                        {
                            if (child is FrameworkElement element)
                            {
                                element.UpdateLayout();
                            }
                        }
                    }
                    
                    // 모든 레이아웃 갱신 후 Canvas 크기 조정
                    AdjustCanvasSize();
                })
            );
        }
        
        private void AdjustSingleCanvasSize(Canvas canvas, ScrollViewer scrollViewer)
        {
            if (canvas == null || scrollViewer == null) return;
            
            // ScrollViewer의 실제 뷰포트 크기
            double viewportWidth = scrollViewer.ViewportWidth;
            double viewportHeight = scrollViewer.ViewportHeight;
            
            // Canvas 내 컴포넌트들의 최대 영역 계산
            double maxRight = 0;
            double maxBottom = 0;
            
            foreach (UIElement child in canvas.Children)
            {
                if (child is FrameworkElement element)
                {
                    double left = Canvas.GetLeft(element);
                    double top = Canvas.GetTop(element);
                    
                    // ✅ ActualWidth/Height가 0이면 DesiredSize 사용
                    // (컨트롤이 화면 밖으로 나갔다 들어올 때 ActualWidth가 0일 수 있음)
                    double width = element.ActualWidth;
                    double height = element.ActualHeight;
                    
                    if (width == 0 || height == 0)
                    {
                        // Measure를 호출하여 원하는 크기 계산
                        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        
                        if (width == 0) width = element.DesiredSize.Width;
                        if (height == 0) height = element.DesiredSize.Height;
                        
                        // DesiredSize도 0이면 저장된 초기 크기 사용 (최후의 수단)
                        if (width == 0 && element.Width > 0) width = element.Width;
                        if (height == 0 && element.Height > 0) height = element.Height;
                    }
                    
                    double right = left + width;
                    double bottom = top + height;
                    
                    if (right > maxRight) maxRight = right;
                    if (bottom > maxBottom) maxBottom = bottom;
                }
            }
            
            // 여백 추가 (50px)
            maxRight += 50;
            maxBottom += 50;
            
            // Canvas 크기 설정
            // - ScrollViewer보다 작으면: ScrollViewer 크기에 맞춤 (스크롤바 안 보임)
            // - 컴포넌트가 ScrollViewer를 벗어나면: 컴포넌트 영역에 맞춤 (스크롤바 보임)
            if (viewportWidth > 0)
            {
                canvas.Width = Math.Max(viewportWidth, Math.Max(maxRight, canvas.MinWidth));
            }
            if (viewportHeight > 0)
            {
                canvas.Height = Math.Max(viewportHeight, Math.Max(maxBottom, canvas.MinHeight));
            }
        }


        /// <summary>
        /// 동적으로 컨트롤 생성 (계층형 또는 리프)
        /// </summary>
        private void CreateDynamicControl(int columnIndex, List<int> hierarchyColumns, ref double currentX, ref double currentY, ref double maxWidth)
        {
            // ✅ 필터링된 데이터가 있으면 그것을 사용
            var dataToUse = _filteredData ?? _data;
            var columnName = dataToUse.Headers[columnIndex];
            
            // Canvas 선택 및 Y 위치 조정
            // Canvas1: 0~180px, Canvas2: 180px 이후부터 시작 (0부터 다시 시작)
            Canvas targetCanvas;
            double adjustedY;
            
            if (currentY <= 180)
            {
                targetCanvas = _canvas1;
                adjustedY = currentY;
            }
            else
            {
                targetCanvas = _canvas2;
                adjustedY = currentY - 180;  // Canvas2는 180px 이후부터 시작
            }

            ControlInfo controlInfo = null;
            
            // 계층형 컬럼인지 확인
            bool isHierarchyColumn = hierarchyColumns.Contains(columnIndex);
            
            if (isHierarchyColumn)
            {
                // 계층형 컬럼 (ComboBox 또는 ListBox)
                if (columnName.Contains("사이즈"))
                {
                    // 사이즈는 ListBox (다중 선택)
                    controlInfo = CreateControlWithLabel(columnName, ControlType.ListBox, currentX, adjustedY, targetCanvas);
                    var listBox = controlInfo.Control as ListBox;
                    
                    var availableValues = _treeBuilder.GetAvailableValues(_hierarchyTree, _selectedPath, columnIndex);
                    foreach (var value in availableValues)
                    {
                        listBox.Items.Add(value);
                    }
                    
                    // ⭐ 초기에는 비활성화 (첫 번째 계층 제외)
                    listBox.IsEnabled = columnIndex == hierarchyColumns.First() || availableValues.Count > 0;
                    
                    listBox.SelectionChanged += (s, e) =>
                    {
                        var selectedItems = listBox.SelectedItems.Cast<string>().ToList();
                        if (selectedItems.Count > 0)
                        {
                            OnHierarchySelectionChanged(columnIndex, selectedItems.First());
                        }
                        else
                        {
                            OnHierarchySelectionChanged(columnIndex, null);
                        }
                    };
                }
                else
                {
                    // 일반 계층형 ComboBox
                    controlInfo = CreateControlWithLabel(columnName, ControlType.ComboBox, currentX, adjustedY, targetCanvas);
                    var comboBox = controlInfo.Control as ComboBox;
                    
                    var availableValues = _treeBuilder.GetAvailableValues(_hierarchyTree, _selectedPath, columnIndex);
                    
                    // ✅ 디버그 로그: 콤보박스에 추가되는 값들
                    Console.WriteLine($"[CreateDynamicControl] {columnName}(인덱스:{columnIndex}): {availableValues.Count}개 값 - {string.Join(", ", availableValues)}");
                    
                    foreach (var value in availableValues)
                    {
                        comboBox.Items.Add(value);
                    }
                    
                    // ⭐ 첫 번째 계층(타입)은 항상 활성화, 나머지는 상위 선택 후 활성화
                    comboBox.IsEnabled = columnIndex == hierarchyColumns.First() || availableValues.Count > 0;
                    
                    comboBox.SelectionChanged += (s, e) =>
                    {
                        OnHierarchySelectionChanged(columnIndex, comboBox.SelectedItem?.ToString());
                    };
                }
            }
            else
            {
                // 리프 컬럼 (상위 선택에 따라 Enable/Disable)
                var sampleValue = GetSampleValue(columnIndex);
                
                if (sampleValue == "E")
                {
                    // TextBox (입력)
                    controlInfo = CreateControlWithLabel(columnName, ControlType.EditBox, currentX, adjustedY, targetCanvas);
                }
                else if (sampleValue == "C")
                {
                    // CheckBox
                    controlInfo = CreateControlWithLabel(columnName, ControlType.CheckBox, currentX, adjustedY, targetCanvas);
                }
                else if (columnName.Contains("사이즈"))
                {
                    // 사이즈는 ListBox
                    controlInfo = CreateControlWithLabel(columnName, ControlType.ListBox, currentX, adjustedY, targetCanvas);
                }
                else
                {
                    // ComboBox (기본)
                    controlInfo = CreateControlWithLabel(columnName, ControlType.ComboBox, currentX, adjustedY, targetCanvas);
                }
                
                // 초기에는 비활성화 (상위 선택 후 활성화)
                if (controlInfo.Control is ComboBox cb)
                    cb.IsEnabled = false;
                else if (controlInfo.Control is ListBox lb)
                    lb.IsEnabled = false;
                else if (controlInfo.Control is TextBox tb)
                    tb.IsEnabled = false;
                else if (controlInfo.Control is CheckBox chk)
                    chk.IsEnabled = false;
            }
            
            _controls[columnName] = controlInfo;
            currentY += controlInfo.Height + 10;
            if (controlInfo.Width > maxWidth) maxWidth = controlInfo.Width;
        }


        private void OnHierarchySelectionChanged(int columnIndex, string selectedValue)
        {
            if (string.IsNullOrEmpty(selectedValue))
            {
                _selectedPath.Remove(columnIndex);
            }
            else
            {
                _selectedPath[columnIndex] = selectedValue;
            }

            UpdateDependentControls(columnIndex);
        }

        private void UpdateDependentControls(int changedColumnIndex)
        {
            // ✅ 변경된 컬럼보다 하위의 모든 선택 경로 제거
            var keysToRemove = _selectedPath.Keys.Where(k => k > changedColumnIndex).ToList();
            foreach (var key in keysToRemove)
            {
                _selectedPath.Remove(key);
            }
            
            // ✅ 필터링된 데이터가 있으면 그것을 사용
            var dataToUse = _filteredData ?? _data;
            
            foreach (var kvp in _controls)
            {
                var controlColumnIndex = dataToUse.Headers.IndexOf(kvp.Key);
                if (controlColumnIndex > changedColumnIndex)
                {
                    var availableValues = _treeBuilder.GetAvailableValues(_hierarchyTree, _selectedPath, controlColumnIndex);
                    
                    if (kvp.Value.Control is ComboBox comboBox)
                    {
                        // ✅ 명시적으로 선택 해제
                        comboBox.SelectedIndex = -1;
                        comboBox.Items.Clear();
                        foreach (var value in availableValues)
                        {
                            comboBox.Items.Add(value);
                        }
                        comboBox.IsEnabled = availableValues.Count > 0;
                        
                        // ✅ 아이템이 있으면 자동으로 첫 번째 항목 선택
                        if (availableValues.Count > 0)
                        {
                            comboBox.SelectedIndex = 0;
                        }
                    }
                    else if (kvp.Value.Control is ListBox listBox)
                    {
                        // ✅ 명시적으로 선택 해제
                        listBox.SelectedIndex = -1;
                        listBox.Items.Clear();
                        foreach (var value in availableValues)
                        {
                            listBox.Items.Add(value);
                        }
                        listBox.IsEnabled = availableValues.Count > 0;
                        
                        // ✅ 아이템이 있으면 자동으로 첫 번째 항목 선택
                        if (availableValues.Count > 0)
                        {
                            listBox.SelectedIndex = 0;
                        }
                    }
                }
            }

            UpdateLeafControls();
        }

        private void CreateLeafControl(int columnIndex, ref double currentX, ref double currentY, ref double maxWidth)
        {
            // ✅ 필터링된 데이터가 있으면 그것을 사용
            var dataToUse = _filteredData ?? _data;
            var columnName = dataToUse.Headers[columnIndex];
            var sampleValue = GetSampleValue(columnIndex);

            Canvas targetCanvas;
            double adjustedY;
            
            if (currentY <= 180)
            {
                targetCanvas = _canvas1;
                adjustedY = currentY;
            }
            else
            {
                targetCanvas = _canvas2;
                adjustedY = currentY - 180;
            }

            ControlInfo controlInfo = null;

            if (sampleValue == "E")
            {
                controlInfo = CreateControlWithLabel(columnName, ControlType.EditBox, currentX, adjustedY, targetCanvas);
            }
            else if (sampleValue == "C")
            {
                controlInfo = CreateControlWithLabel(columnName, ControlType.CheckBox, currentX, adjustedY, targetCanvas);
            }
            else if (sampleValue == "X" || string.IsNullOrWhiteSpace(sampleValue))
            {
                controlInfo = CreateControlWithLabel(columnName, ControlType.ComboBox, currentX, adjustedY, targetCanvas);
                (controlInfo.Control as ComboBox).IsEnabled = false;
            }
            else if (columnName.Contains("사이즈"))
            {
                controlInfo = CreateControlWithLabel(columnName, ControlType.ListBox, currentX, adjustedY, targetCanvas);
            }
            else
            {
                controlInfo = CreateControlWithLabel(columnName, ControlType.ComboBox, currentX, adjustedY, targetCanvas);
            }

            _controls[columnName] = controlInfo;
            currentY += controlInfo.Height + 10;
            if (controlInfo.Width > maxWidth) maxWidth = controlInfo.Width;
        }

        private ControlInfo CreateControlWithLabel(string labelText, ControlType type, double x, double y, Canvas targetCanvas)
        {
            // ✅ 저장된 위치가 있으면 사용 (종류 변경 시에도 위치 유지)
            double finalX = x;
            double finalY = y;
            Canvas finalCanvas = targetCanvas;
            bool useSavedPosition = false;
            
            // ✅ 저장된 위치가 있으면 무조건 사용 (IsUserModified와 관계없이)
            if (_savedControlPositions.ContainsKey(labelText))
            {
                var savedPos = _savedControlPositions[labelText];
                finalX = savedPos.Left;
                finalY = savedPos.Top;
                finalCanvas = (savedPos.CanvasNumber == 1) ? _canvas1 : _canvas2;
                useSavedPosition = true;
                Console.WriteLine($"[Position Restored] {labelText}: Left={finalX}, Top={finalY}, Canvas={savedPos.CanvasNumber}, UserModified={savedPos.IsUserModified}");
            }
            
            var controlInfo = new ControlInfo
            {
                ColumnName = labelText,
                Type = type,
                Left = finalX,
                Top = finalY,
                Width = 210,
                // 초기 위치/크기 저장 (스케일링 기준)
                InitialLeft = finalX,
                InitialTop = finalY,
                InitialWidth = 210,
                // Canvas 번호 설정 (1 또는 2)
                CanvasNumber = (finalCanvas == _canvas1) ? 1 : 2
            };

            // Border로 Label과 Control을 그룹으로 묶기
            var groupBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(10, 0, 0, 0)),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5),
                Cursor = Cursors.Arrow,
                Tag = labelText // 나중에 식별하기 위해
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            // Label 생성
            var label = new Label
            {
                Content = labelText,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(0, 0, 0, 3),
                Foreground = new SolidColorBrush(Color.FromRgb(50, 50, 50))
            };
            stackPanel.Children.Add(label);

            // Control 생성
            Control control = null;
            switch (type)
            {
                case ControlType.ComboBox:
                    control = new ComboBox { Width = 200, Height = 25 };
                    controlInfo.Height = 60;
                    controlInfo.InitialHeight = 60;
                    break;
                case ControlType.ListBox:
                    control = new ListBox { Width = 200, Height = 120, SelectionMode = SelectionMode.Multiple };
                    controlInfo.Height = 155;
                    controlInfo.InitialHeight = 155;
                    break;
                case ControlType.EditBox:
                    control = new TextBox { Width = 200, Height = 25 };
                    controlInfo.Height = 60;
                    controlInfo.InitialHeight = 60;
                    break;
                case ControlType.CheckBox:
                    control = new CheckBox { Content = "", Height = 25 };
                    controlInfo.Height = 60;
                    controlInfo.InitialHeight = 60;
                    break;
            }

            stackPanel.Children.Add(control);
            groupBorder.Child = stackPanel;

            // ✅ 저장된 위치 또는 기본 위치 적용
            Canvas.SetLeft(groupBorder, finalX);
            Canvas.SetTop(groupBorder, finalY);
            
            // 드래그 이벤트를 Border에 연결
            groupBorder.MouseLeftButtonDown += Border_MouseLeftButtonDown;
            groupBorder.MouseMove += Border_MouseMove;
            groupBorder.MouseLeftButtonUp += Border_MouseLeftButtonUp;
            
            // 이동 모드일 때 시각적 피드백
            groupBorder.MouseEnter += (s, e) =>
            {
                if (_mainWindow.IsControlMoveMode)
                {
                    groupBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                    groupBorder.Background = new SolidColorBrush(Color.FromArgb(30, 0, 120, 215));
                    groupBorder.Cursor = Cursors.SizeAll;
                }
            };
            groupBorder.MouseLeave += (s, e) =>
            {
                if (_mainWindow.IsControlMoveMode && _draggedBorder != groupBorder)
                {
                    groupBorder.BorderBrush = Brushes.Transparent;
                    groupBorder.Background = new SolidColorBrush(Color.FromArgb(10, 0, 0, 0));
                    groupBorder.Cursor = Cursors.Arrow;
                }
            };
            
            // ✅ 저장된 Canvas 또는 기본 Canvas에 추가
            finalCanvas.Children.Add(groupBorder);
            
            controlInfo.Control = control;
            controlInfo.Label = label;  // ✅ Label 저장 추가
            controlInfo.GroupBorder = groupBorder;
            return controlInfo;
        }

        // 드래그 이벤트 핸들러
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_mainWindow.IsControlMoveMode)
            {
                var border = sender as Border;
                _draggedBorder = border;
                _dragStartPoint = e.GetPosition(border.Parent as Canvas);
                _isDragging = true;
                border.CaptureMouse();
                
                // 선택 표시
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                border.Background = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215));
                
                _mainWindow.SelectedControlForMove = border;
                e.Handled = true;
            }
        }

        private void Border_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _draggedBorder != null && _mainWindow.IsControlMoveMode)
            {
                var border = _draggedBorder;
                var currentCanvas = border.Parent as Canvas;
                
                // 윈도우 기준 마우스 위치 가져오기
                var mousePositionInWindow = e.GetPosition(_mainWindow);
                
                // Canvas1과 Canvas2의 윈도우 기준 위치 가져오기
                var canvas1Position = _canvas1.TransformToAncestor(_mainWindow).Transform(new Point(0, 0));
                var canvas2Position = _canvas2.TransformToAncestor(_mainWindow).Transform(new Point(0, 0));
                
                // 마우스가 어느 Canvas 영역에 있는지 확인
                var canvas1Bounds = new Rect(canvas1Position, new Size(_canvas1.ActualWidth, _canvas1.ActualHeight));
                var canvas2Bounds = new Rect(canvas2Position, new Size(_canvas2.ActualWidth, _canvas2.ActualHeight));
                
                Canvas targetCanvas = null;
                Point targetPosition = new Point();
                
                if (canvas1Bounds.Contains(mousePositionInWindow))
                {
                    targetCanvas = _canvas1;
                    targetPosition = e.GetPosition(_canvas1);
                }
                else if (canvas2Bounds.Contains(mousePositionInWindow))
                {
                    targetCanvas = _canvas2;
                    targetPosition = e.GetPosition(_canvas2);
                }
                
                // Canvas가 변경되었으면 부모 변경
                if (targetCanvas != null && targetCanvas != currentCanvas)
                {
                    currentCanvas.Children.Remove(border);
                    targetCanvas.Children.Add(border);
                    
                    // ⭐ Canvas 번호 업데이트
                    foreach (var kvp in _controls)
                    {
                        if (kvp.Value.GroupBorder == border)
                        {
                            kvp.Value.CanvasNumber = (targetCanvas == _canvas1) ? 1 : 2;
                            break;
                        }
                    }
                    
                    // 새 위치 설정 (드래그 시작 위치 재설정)
                    var newLeft = targetPosition.X - border.ActualWidth / 2;
                    var newTop = targetPosition.Y - border.ActualHeight / 2;
                    
                    // 경계 체크
                    newLeft = Math.Max(0, Math.Min(newLeft, targetCanvas.ActualWidth - border.ActualWidth));
                    newTop = Math.Max(0, Math.Min(newTop, targetCanvas.ActualHeight - border.ActualHeight));
                    
                    Canvas.SetLeft(border, newLeft);
                    Canvas.SetTop(border, newTop);
                    
                    _dragStartPoint = targetPosition;
                }
                else if (targetCanvas == currentCanvas)
                {
                    // 같은 Canvas 내에서 이동
                    var currentPoint = targetPosition;
                    
                    var offsetX = currentPoint.X - _dragStartPoint.X;
                    var offsetY = currentPoint.Y - _dragStartPoint.Y;
                    
                    var newLeft = Canvas.GetLeft(border) + offsetX;
                    var newTop = Canvas.GetTop(border) + offsetY;
                    
                    // 경계 체크
                    newLeft = Math.Max(0, Math.Min(newLeft, targetCanvas.ActualWidth - border.ActualWidth));
                    newTop = Math.Max(0, Math.Min(newTop, targetCanvas.ActualHeight - border.ActualHeight));
                    
                    Canvas.SetLeft(border, newLeft);
                    Canvas.SetTop(border, newTop);
                    
                    _dragStartPoint = currentPoint;
                }
            }
        }

        private void Border_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                var border = _draggedBorder;
                border.ReleaseMouseCapture();
                
                // ⭐ 드래그가 끝났을 때 새 위치를 InitialLeft/InitialTop으로 저장
                // 이렇게 하면 Window 크기 변경 시에도 새 위치를 기준으로 스케일링됨
                foreach (var kvp in _controls)
                {
                    if (kvp.Value.GroupBorder == border)
                    {
                        double newLeft = Canvas.GetLeft(border);
                        double newTop = Canvas.GetTop(border);
                        
                        kvp.Value.Left = newLeft;
                        kvp.Value.Top = newTop;
                        kvp.Value.InitialLeft = newLeft;
                        kvp.Value.InitialTop = newTop;
                        
                        // ✅ 종류 변경 시에도 위치 유지를 위해 별도 저장
                        _savedControlPositions[kvp.Key] = new SavedControlPosition
                        {
                            Left = newLeft,
                            Top = newTop,
                            CanvasNumber = kvp.Value.CanvasNumber,
                            IsUserModified = true
                        };
                        
                        Console.WriteLine($"[Position Saved] {kvp.Key}: Left={newLeft}, Top={newTop}, Canvas={kvp.Value.CanvasNumber}");
                        break;
                    }
                }
                
                // ⭐ 드래그 종료 후 Canvas 크기 재조정
                // 컴포넌트를 이동했으므로 Canvas 크기를 새로운 배치에 맞게 조정
                AdjustCanvasSize();
                
                _draggedBorder = null;
            }
        }

        private void UpdateLeafControls()
        {
            var leafValues = _treeBuilder.GetLeafValues(_hierarchyTree, _selectedPath);
            
            // ✅ 필터링된 데이터가 있으면 그것을 사용
            var dataToUse = _filteredData ?? _data;

            foreach (var kvp in _controls)
            {
                var columnName = kvp.Key;
                var control = kvp.Value.Control;
                
                // ✅ 계층형 컬럼인지 확인 (_hierarchyColumns 리스트 사용)
                var columnIndex = dataToUse.Headers.IndexOf(columnName);
                bool isHierarchyColumn = _hierarchyColumns != null && _hierarchyColumns.Contains(columnIndex);
                
                if (isHierarchyColumn)
                {
                    // 계층형 컬럼은 UpdateDependentControls에서 이미 처리됨
                    continue;
                }
                
                // 리프 컬럼 처리 (계층형이 아닌 컬럼들)
                if (leafValues.ContainsKey(columnName))
                {
                    var values = leafValues[columnName];
                    
                    // "X"나 빈값만 있는지 확인
                    var validValues = values.Where(v => !string.IsNullOrWhiteSpace(v) && v != "X" && v != "E" && v != "C").ToList();
                    bool hasValidData = validValues.Count > 0 || values.Contains("E") || values.Contains("C");
                    
                    if (control is ComboBox comboBox)
                    {
                        comboBox.Items.Clear();
                        foreach (var value in validValues)
                        {
                            comboBox.Items.Add(value);
                        }
                        // 데이터가 있으면 Enable, 없으면 Disable
                        comboBox.IsEnabled = comboBox.Items.Count > 0;
                    }
                    else if (control is ListBox listBox)
                    {
                        listBox.Items.Clear();
                        foreach (var value in validValues)
                        {
                            listBox.Items.Add(value);
                        }
                        listBox.IsEnabled = listBox.Items.Count > 0;
                    }
                    else if (control is TextBox textBox)
                    {
                        // "E" 값이 있으면 Enable
                        textBox.IsEnabled = values.Contains("E");
                    }
                    else if (control is CheckBox checkBox)
                    {
                        // "C" 값이 있으면 Enable
                        checkBox.IsEnabled = values.Contains("C");
                    }
                }
                else
                {
                    // leafValues에 없으면 Disable
                    if (control is ComboBox comboBox)
                    {
                        comboBox.IsEnabled = false;
                    }
                    else if (control is ListBox listBox)
                    {
                        listBox.IsEnabled = false;
                    }
                    else if (control is TextBox textBox)
                    {
                        textBox.IsEnabled = false;
                    }
                    else if (control is CheckBox checkBox)
                    {
                        checkBox.IsEnabled = false;
                    }
                }
            }
        }

        private string GetSampleValue(int columnIndex)
        {
            // ✅ 필터링된 데이터가 있으면 그것을 사용
            var dataToUse = _filteredData ?? _data;
            
            foreach (var row in dataToUse.DataRows)
            {
                var columnName = dataToUse.Headers[columnIndex];
                if (row.CompleteValues.ContainsKey(columnName))
                {
                    var value = row.CompleteValues[columnName];
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        var firstValue = value.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                 .FirstOrDefault()?.Trim();
                        return firstValue ?? "";
                    }
                }
            }
            return "";
        }

        public Dictionary<string, object> GetSelectedValues()
        {
            var result = new Dictionary<string, object>();

            foreach (var kvp in _controls)
            {
                var control = kvp.Value.Control;
                
                if (control is ComboBox comboBox && comboBox.SelectedItem != null)
                {
                    result[kvp.Key] = comboBox.SelectedItem.ToString();
                }
                else if (control is ListBox listBox && listBox.SelectedItems.Count > 0)
                {
                    result[kvp.Key] = listBox.SelectedItems.Cast<string>().ToList();
                }
                else if (control is TextBox textBox && !string.IsNullOrWhiteSpace(textBox.Text))
                {
                    result[kvp.Key] = textBox.Text;
                }
                else if (control is CheckBox checkBox)
                {
                    result[kvp.Key] = checkBox.IsChecked == true;
                }
            }

            return result;
        }

        public string GenerateSummary(Dictionary<string, object> selectedValues)
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine("=== 선택된 사양 ===");
            summary.AppendLine();

            foreach (var kvp in selectedValues)
            {
                if (kvp.Value is List<string> list)
                {
                    summary.AppendLine($"{kvp.Key}: {string.Join(", ", list)}");
                }
                else
                {
                    summary.AppendLine($"{kvp.Key}: {kvp.Value}");
                }
            }

            return summary.ToString();
        }

        public Dictionary<string, object> GetCurrentState()
        {
            var state = new Dictionary<string, object>();
            
            // ⭐ Window 크기 저장
            state["WindowWidth"] = _mainWindow.Width;
            state["WindowHeight"] = _mainWindow.Height;
            
            // 선택된 값들
            state["SelectedValues"] = GetSelectedValues();
            
            // 컨트롤 위치 정보 + Canvas 번호
            var positions = new Dictionary<string, Dictionary<string, object>>();
            foreach (var kvp in _controls)
            {
                if (kvp.Value.GroupBorder != null)
                {
                    positions[kvp.Key] = new Dictionary<string, object>
                    {
                        { "Left", Canvas.GetLeft(kvp.Value.GroupBorder) },
                        { "Top", Canvas.GetTop(kvp.Value.GroupBorder) },
                        { "CanvasNumber", kvp.Value.CanvasNumber }  // ⭐ Canvas 번호 저장
                    };
                }
            }
            state["ControlPositions"] = positions;
            
            return state;
        }

        public void LoadState(Dictionary<string, object> state)
        {
            // ⭐ Window 크기 복원
            if (state.ContainsKey("WindowWidth") && state.ContainsKey("WindowHeight"))
            {
                try
                {
                    double windowWidth = Convert.ToDouble(state["WindowWidth"]);
                    double windowHeight = Convert.ToDouble(state["WindowHeight"]);
                    
                    _mainWindow.Width = windowWidth;
                    _mainWindow.Height = windowHeight;
                }
                catch
                {
                    // 변환 실패 시 무시
                }
            }
            
            // 선택된 값 복원
            if (state.ContainsKey("SelectedValues") && state["SelectedValues"] is Newtonsoft.Json.Linq.JObject selectedValuesObj)
            {
                var selectedValues = selectedValuesObj.ToObject<Dictionary<string, object>>();
                foreach (var kvp in selectedValues)
                {
                    if (_controls.ContainsKey(kvp.Key))
                    {
                        var control = _controls[kvp.Key].Control;
                        
                        if (control is ComboBox comboBox)
                        {
                            comboBox.SelectedItem = kvp.Value;
                        }
                        else if (control is ListBox listBox && kvp.Value is Newtonsoft.Json.Linq.JArray jArray)
                        {
                            listBox.SelectedItems.Clear();
                            foreach (var item in jArray)
                            {
                                var itemStr = item.ToString();
                                if (listBox.Items.Contains(itemStr))
                                {
                                    listBox.SelectedItems.Add(itemStr);
                                }
                            }
                        }
                        else if (control is TextBox textBox)
                        {
                            textBox.Text = kvp.Value?.ToString() ?? "";
                        }
                        else if (control is CheckBox checkBox)
                        {
                            if (kvp.Value is bool boolValue)
                            {
                                checkBox.IsChecked = boolValue;
                            }
                            else if (bool.TryParse(kvp.Value?.ToString(), out var parsedBool))
                            {
                                checkBox.IsChecked = parsedBool;
                            }
                        }
                    }
                }
            }
            
            // 컨트롤 위치 + Canvas 복원
            if (state.ContainsKey("ControlPositions") && state["ControlPositions"] is Newtonsoft.Json.Linq.JObject positionsObj)
            {
                var positions = positionsObj.ToObject<Dictionary<string, Dictionary<string, object>>>();
                foreach (var kvp in positions)
                {
                    if (_controls.ContainsKey(kvp.Key) && _controls[kvp.Key].GroupBorder != null)
                    {
                        var controlInfo = _controls[kvp.Key];
                        var border = controlInfo.GroupBorder;
                        
                        double newLeft = Convert.ToDouble(kvp.Value["Left"]);
                        double newTop = Convert.ToDouble(kvp.Value["Top"]);
                        
                        // ⭐ Canvas 번호 복원
                        int canvasNumber = 1; // 기본값
                        if (kvp.Value.ContainsKey("CanvasNumber"))
                        {
                            canvasNumber = Convert.ToInt32(kvp.Value["CanvasNumber"]);
                        }
                        
                        // ⭐ 올바른 Canvas로 이동
                        Canvas targetCanvas = (canvasNumber == 1) ? _canvas1 : _canvas2;
                        Canvas currentCanvas = border.Parent as Canvas;
                        
                        if (currentCanvas != targetCanvas)
                        {
                            currentCanvas?.Children.Remove(border);
                            targetCanvas.Children.Add(border);
                        }
                        
                        controlInfo.CanvasNumber = canvasNumber;
                        
                        Canvas.SetLeft(border, newLeft);
                        Canvas.SetTop(border, newTop);
                        
                        // ⭐ InitialLeft/InitialTop도 업데이트하여 스케일링 기준점 변경
                        controlInfo.InitialLeft = newLeft;
                        controlInfo.InitialTop = newTop;
                        controlInfo.Left = newLeft;
                        controlInfo.Top = newTop;
                        
                        // ✅ 종류 변경 시에도 위치 유지를 위해 _savedControlPositions에 저장
                        _savedControlPositions[kvp.Key] = new SavedControlPosition
                        {
                            Left = newLeft,
                            Top = newTop,
                            CanvasNumber = canvasNumber,
                            IsUserModified = true  // 파일에서 로드된 위치는 사용자가 저장한 것으로 간주
                        };
                        Console.WriteLine($"[Position Loaded from File] {kvp.Key}: Left={newLeft}, Top={newTop}, Canvas={canvasNumber}");
                    }
                    else
                    {
                        // ✅ 컨트롤이 아직 생성되지 않았어도 _savedControlPositions에 저장
                        // 나중에 컨트롤이 생성될 때 이 위치를 사용
                        double newLeft = Convert.ToDouble(kvp.Value["Left"]);
                        double newTop = Convert.ToDouble(kvp.Value["Top"]);
                        int canvasNumber = 1;
                        if (kvp.Value.ContainsKey("CanvasNumber"))
                        {
                            canvasNumber = Convert.ToInt32(kvp.Value["CanvasNumber"]);
                        }
                        
                        _savedControlPositions[kvp.Key] = new SavedControlPosition
                        {
                            Left = newLeft,
                            Top = newTop,
                            CanvasNumber = canvasNumber,
                            IsUserModified = true
                        };
                        Console.WriteLine($"[Position Pre-Loaded] {kvp.Key}: Left={newLeft}, Top={newTop}, Canvas={canvasNumber}");
                    }
                }
                
                // ⭐ 위치 복원 후 Canvas 크기 재조정
                AdjustCanvasSize();
            }
        }

        public void ResetAll()
        {
            foreach (var controlInfo in _controls.Values)
            {
                var control = controlInfo.Control;
                
                if (control is ComboBox comboBox)
                {
                    comboBox.SelectedIndex = -1;
                }
                else if (control is ListBox listBox)
                {
                    listBox.SelectedItems.Clear();
                }
                else if (control is TextBox textBox)
                {
                    textBox.Clear();
                }
                else if (control is CheckBox checkBox)
                {
                    checkBox.IsChecked = false;
                }
            }

            _selectedPath.Clear();
        }

        /// <summary>
        /// 윈도우 크기 변경 시 모든 컨트롤의 위치와 크기를 스케일링
        /// </summary>
        public void ScaleControls(double scaleX, double scaleY)
        {
            // 모든 컨트롤 스케일링 (비례 방식)
            foreach (var controlInfo in _controls.Values)
            {
                if (controlInfo.GroupBorder == null)
                    continue;

                // ✅ 비례 스케일링: Window 크기에 따라 컴포넌트도 비례하여 확대/축소
                // 너무 작아지지 않도록 최소 스케일 제한 (0.7배까지만 축소)
                double effectiveScaleX = Math.Max(0.7, scaleX);
                double effectiveScaleY = Math.Max(0.7, scaleY);
                
                // 초기 위치를 기준으로 스케일링
                double newLeft = controlInfo.InitialLeft * effectiveScaleX;
                double newTop = controlInfo.InitialTop * effectiveScaleY;

                // 위치 업데이트
                Canvas.SetLeft(controlInfo.GroupBorder, newLeft);
                Canvas.SetTop(controlInfo.GroupBorder, newTop);

                // 현재 위치 정보 업데이트
                controlInfo.Left = newLeft;
                controlInfo.Top = newTop;

                // ✅ 컨트롤 너비도 비례 스케일링
                double newWidth = controlInfo.InitialWidth * effectiveScaleX;
                
                if (controlInfo.Control != null)
                {
                    // 최소 너비 120px, 최대 너비 제한 없음
                    controlInfo.Control.Width = Math.Max(120, newWidth - 10);
                }

                // ✅ Label 폰트 크기도 스케일링 (선택적)
                if (controlInfo.Label != null)
                {
                    // 기본 폰트 크기 12에서 스케일링
                    controlInfo.Label.FontSize = Math.Max(9, 12 * effectiveScaleY);
                }

                // ✅ Control 폰트 크기도 스케일링 (선택적)
                if (controlInfo.Control is Control control)
                {
                    // 기본 폰트 크기에서 스케일링
                    control.FontSize = Math.Max(9, 11 * effectiveScaleY);
                }
            }
            
            // ⭐ 컨트롤 위치 조정 후 Canvas 크기를 자동으로 재조정
            AdjustCanvasSize();
        }
    }
    
    /// <summary>
    /// 컨트롤 위치 저장용 클래스 (종류 변경 시에도 유지)
    /// </summary>
    public class SavedControlPosition
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public int CanvasNumber { get; set; }
        public bool IsUserModified { get; set; }  // 사용자가 Ctrl+M으로 이동했는지 여부
    }
}
