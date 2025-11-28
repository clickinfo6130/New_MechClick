using System;
using System.Collections.Generic;
using System.Linq;

namespace BoltSpecProgram
{
    /// <summary>
    /// 엑셀에서 읽은 볼트 사양 데이터를 저장하는 클래스
    /// </summary>
    public class BoltSpecData
    {
        public List<string> Headers { get; set; } = new List<string>();
        public List<BoltDataRow> DataRows { get; set; } = new List<BoltDataRow>();
    }

    /// <summary>
    /// 각 데이터 행을 저장하는 클래스
    /// </summary>
    public class BoltDataRow
    {
        public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>();
        
        // 상위 행의 값을 상속받아 완전한 행 데이터를 저장
        public Dictionary<string, string> CompleteValues { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// UI 컨트롤 정보를 저장하는 클래스
    /// </summary>
    public class ControlInfo
    {
        public string ColumnName { get; set; }
        public int ColumnIndex { get; set; }
        public ControlType Type { get; set; }
        public List<string> Items { get; set; } = new List<string>();
        public string SelectedValue { get; set; }
        public bool IsEnabled { get; set; } = true;
        
        // 컨트롤의 위치 정보 (저장/이동 기능을 위해)
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; } = 200;
        public double Height { get; set; } = 25;
        
        // 초기 위치/크기 (스케일링 기준)
        public double InitialLeft { get; set; }
        public double InitialTop { get; set; }
        public double InitialWidth { get; set; }
        public double InitialHeight { get; set; }
        
        // 어느 Canvas에 있는지 (1 또는 2)
        public int CanvasNumber { get; set; } = 1;
        
        // 실제 컨트롤 객체
        public System.Windows.Controls.Control Control { get; set; }
        
        // Label 객체 (폰트 크기 스케일링용)
        public System.Windows.Controls.Label Label { get; set; }
        
        // Label과 Control을 묶은 Border 그룹
        public System.Windows.Controls.Border GroupBorder { get; set; }
    }

    public enum ControlType
    {
        ComboBox,
        ListBox,
        EditBox,
        CheckBox,
        Label
    }

    /// <summary>
    /// 계층적 필터링을 위한 데이터 노드
    /// </summary>
    public class HierarchyNode
    {
        public int ColumnIndex { get; set; }
        public string ColumnName { get; set; }
        public string Value { get; set; }
        public List<HierarchyNode> Children { get; set; } = new List<HierarchyNode>();
        public Dictionary<string, string> LeafValues { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// 계층 트리를 구축하고 관리하는 클래스
    /// </summary>
    public class HierarchyTreeBuilder
    {
        private BoltSpecData _data;
        private List<int> _hierarchyColumns; // 계층 구조에 포함될 컬럼 인덱스들

        public HierarchyTreeBuilder(BoltSpecData data, List<int> hierarchyColumns)
        {
            _data = data;
            _hierarchyColumns = hierarchyColumns.OrderBy(x => x).ToList();
        }

        /// <summary>
        /// 계층 트리 구축
        /// </summary>
        public List<HierarchyNode> BuildTree()
        {
            var rootNodes = new List<HierarchyNode>();

            foreach (var row in _data.DataRows)
            {
                AddRowToTree(rootNodes, row, 0);
            }

            return rootNodes;
        }

        private void AddRowToTree(List<HierarchyNode> currentLevel, BoltDataRow row, int hierarchyIndex)
        {
            if (hierarchyIndex >= _hierarchyColumns.Count)
            {
                return;
            }

            int columnIndex = _hierarchyColumns[hierarchyIndex];
            string columnName = _data.Headers[columnIndex];
            string value = row.CompleteValues.ContainsKey(columnName) ? row.CompleteValues[columnName] : "";

            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            // 개행 문자로 구분된 여러 값 처리
            var values = value.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(v => v.Trim())
                              .Where(v => !string.IsNullOrEmpty(v))
                              .Distinct()
                              .ToList();

            foreach (var val in values)
            {
                var node = currentLevel.FirstOrDefault(n => n.Value == val && n.ColumnIndex == columnIndex);
                if (node == null)
                {
                    node = new HierarchyNode
                    {
                        ColumnIndex = columnIndex,
                        ColumnName = columnName,
                        Value = val
                    };
                    currentLevel.Add(node);
                }

                // 마지막 레벨이면 나머지 값들을 LeafValues에 저장
                if (hierarchyIndex == _hierarchyColumns.Count - 1)
                {
                    foreach (var kvp in row.CompleteValues)
                    {
                        if (!_hierarchyColumns.Contains(_data.Headers.IndexOf(kvp.Key)))
                        {
                            if (!node.LeafValues.ContainsKey(kvp.Key))
                            {
                                node.LeafValues[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                }
                else
                {
                    // 하위 레벨로 계속 진행
                    AddRowToTree(node.Children, row, hierarchyIndex + 1);
                }
            }
        }

        /// <summary>
        /// 선택된 경로에 따라 사용 가능한 값들을 가져옴
        /// </summary>
        public List<string> GetAvailableValues(List<HierarchyNode> tree, Dictionary<int, string> selectedPath, int targetColumnIndex)
        {
            var availableValues = new List<string>();

            // 선택된 경로를 따라 트리를 탐색
            var currentNodes = tree;
            foreach (var colIndex in _hierarchyColumns)
            {
                if (colIndex == targetColumnIndex)
                {
                    // 목표 컬럼에 도달하면 현재 레벨의 모든 값 반환
                    availableValues = currentNodes.Select(n => n.Value).Distinct().ToList();
                    break;
                }

                if (selectedPath.ContainsKey(colIndex))
                {
                    var selectedValue = selectedPath[colIndex];
                    var matchingNode = currentNodes.FirstOrDefault(n => n.Value == selectedValue);
                    if (matchingNode != null)
                    {
                        currentNodes = matchingNode.Children;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            return availableValues;
        }

        /// <summary>
        /// 선택된 경로에 해당하는 Leaf 값들을 가져옴
        /// </summary>
        public Dictionary<string, List<string>> GetLeafValues(List<HierarchyNode> tree, Dictionary<int, string> selectedPath)
        {
            var leafValues = new Dictionary<string, List<string>>();

            var currentNodes = tree;
            foreach (var colIndex in _hierarchyColumns)
            {
                if (selectedPath.ContainsKey(colIndex))
                {
                    var selectedValue = selectedPath[colIndex];
                    var matchingNodes = currentNodes.Where(n => n.Value == selectedValue).ToList();
                    
                    if (matchingNodes.Any())
                    {
                        if (colIndex == _hierarchyColumns.Last())
                        {
                            // 마지막 레벨이면 LeafValues 수집
                            foreach (var node in matchingNodes)
                            {
                                foreach (var kvp in node.LeafValues)
                                {
                                    if (!leafValues.ContainsKey(kvp.Key))
                                    {
                                        leafValues[kvp.Key] = new List<string>();
                                    }
                                    
                                    var values = kvp.Value.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                          .Select(v => v.Trim())
                                                          .Where(v => !string.IsNullOrEmpty(v))
                                                          .ToList();
                                    
                                    foreach (var val in values)
                                    {
                                        if (!leafValues[kvp.Key].Contains(val))
                                        {
                                            leafValues[kvp.Key].Add(val);
                                        }
                                    }
                                }
                            }
                            break;
                        }
                        else
                        {
                            // 다음 레벨로 진행
                            currentNodes = matchingNodes.SelectMany(n => n.Children).ToList();
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            return leafValues;
        }
    }
}
