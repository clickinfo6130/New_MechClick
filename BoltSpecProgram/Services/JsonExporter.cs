using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BoltSpecProgram.Services
{
    /// <summary>
    /// Excel 데이터를 계층 구조 JSON으로 변환하여 저장하는 클래스
    /// </summary>
    public class JsonExporter
    {
        #region JSON 구조 클래스

        /// <summary>
        /// 최상위 JSON 구조
        /// </summary>
        public class RootData
        {
            [JsonProperty("Series")]
            public List<SeriesItem> Series { get; set; } = new List<SeriesItem>();
        }

        /// <summary>
        /// 시리즈 항목 (예: 육각머리볼트)
        /// </summary>
        public class SeriesItem
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("option")]
            public List<OptionItem> Options { get; set; } = new List<OptionItem>();
        }

        /// <summary>
        /// 옵션 항목 (예: 타입, 규격, 용도 등)
        /// </summary>
        public class OptionItem
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("default_Value")]
            public string DefaultValue { get; set; } = "0";

            [JsonProperty("values")]
            public List<OptionValue> Values { get; set; } = new List<OptionValue>();

            [JsonProperty("type")]
            public string Type { get; set; } = "COMBO";
        }

        /// <summary>
        /// 옵션 값 항목
        /// </summary>
        public class OptionValue
        {
            [JsonProperty("enumid")]
            public int EnumId { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("desc")]
            public string Desc { get; set; } = "";

            [JsonProperty("filter")]
            public List<string> Filter { get; set; } = new List<string>();

            [JsonProperty("filter_Values")]
            public List<List<string>> FilterValues { get; set; } = new List<List<string>>();
        }

        #endregion

        #region 내부 데이터 구조

        /// <summary>
        /// 값과 부모 관계를 저장하는 내부 구조
        /// </summary>
        private class ValueRelation
        {
            public string Value { get; set; }
            public int EnumId { get; set; }
            public HashSet<string> ParentValues { get; set; } = new HashSet<string>();
        }

        #endregion

        private readonly BoltSpecData _data;
        private readonly List<int> _hierarchyColumns;  // 계층 컬럼 인덱스 (2~8)
        private readonly List<int> _leafColumns;       // 리프 컬럼 인덱스 (9+)

        /// <summary>
        /// 생성자
        /// </summary>
        /// <param name="data">Excel에서 읽은 데이터</param>
        public JsonExporter(BoltSpecData data)
        {
            _data = data;
            
            // 계층 컬럼 인덱스 (Headers[2] ~ Headers[8])
            _hierarchyColumns = new List<int>();
            for (int i = 2; i <= Math.Min(data.Headers.Count - 1, 8); i++)
            {
                if (!string.IsNullOrWhiteSpace(data.Headers[i]))
                {
                    _hierarchyColumns.Add(i);
                }
            }

            // 리프 컬럼 인덱스 (Headers[9] 이후)
            _leafColumns = new List<int>();
            for (int i = 9; i < data.Headers.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(data.Headers[i]))
                {
                    _leafColumns.Add(i);
                }
            }
        }

        /// <summary>
        /// JSON 파일로 내보내기
        /// </summary>
        /// <param name="filePath">저장할 파일 경로</param>
        public void Export(string filePath)
        {
            var rootData = BuildJsonStructure();
            
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };

            string json = JsonConvert.SerializeObject(rootData, settings);
            File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// JSON 문자열 반환 (미리보기용)
        /// </summary>
        public string ExportToString()
        {
            var rootData = BuildJsonStructure();
            
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };

            return JsonConvert.SerializeObject(rootData, settings);
        }

        /// <summary>
        /// JSON 구조 생성
        /// </summary>
        private RootData BuildJsonStructure()
        {
            var rootData = new RootData();

            // 시리즈 생성 (종류 - Headers[1])
            var series = new SeriesItem
            {
                Id = 1,
                Name = GetSeriesName()
            };

            // 옵션 ID 매핑 (컬럼 인덱스 → 옵션 ID)
            var columnToOptionId = new Dictionary<int, int>();
            int optionId = 0;

            // 계층 컬럼들에 대한 옵션 생성
            foreach (var colIndex in _hierarchyColumns)
            {
                var option = BuildHierarchyOption(colIndex, optionId, columnToOptionId);
                if (option != null && option.Values.Count > 0)
                {
                    series.Options.Add(option);
                    columnToOptionId[colIndex] = optionId;
                    optionId++;
                }
            }

            // 리프 컬럼들에 대한 옵션 생성
            foreach (var colIndex in _leafColumns)
            {
                var option = BuildLeafOption(colIndex, optionId, columnToOptionId);
                if (option != null && option.Values.Count > 0)
                {
                    series.Options.Add(option);
                    columnToOptionId[colIndex] = optionId;
                    optionId++;
                }
            }

            rootData.Series.Add(series);
            return rootData;
        }

        /// <summary>
        /// 시리즈 이름 추출 (종류)
        /// </summary>
        private string GetSeriesName()
        {
            if (_data.Headers.Count > 1 && _data.DataRows.Count > 0)
            {
                var categoryColumn = _data.Headers[1];
                if (_data.DataRows[0].CompleteValues.ContainsKey(categoryColumn))
                {
                    return _data.DataRows[0].CompleteValues[categoryColumn];
                }
            }
            return "Unknown";
        }

        /// <summary>
        /// 계층 옵션 생성
        /// </summary>
        private OptionItem BuildHierarchyOption(int colIndex, int optionId, Dictionary<int, int> columnToOptionId)
        {
            string columnName = _data.Headers[colIndex];
            
            var option = new OptionItem
            {
                Id = optionId,
                Name = columnName,
                DefaultValue = "0",
                Type = colIndex == 8 ? "LISTBOX" : "COMBO"  // 사이즈(8)는 LISTBOX
            };

            // 이 컬럼의 모든 고유값과 부모 관계 수집
            var valueRelations = CollectValueRelations(colIndex);

            // 부모 옵션 ID 결정
            int parentColIndex = GetParentColumnIndex(colIndex);
            string parentOptionId = parentColIndex >= 0 && columnToOptionId.ContainsKey(parentColIndex)
                ? columnToOptionId[parentColIndex].ToString()
                : "-1";

            // 부모 값들의 enumId 매핑
            var parentValueToEnumId = new Dictionary<string, int>();
            if (parentColIndex >= 0)
            {
                var parentValues = CollectUniqueValues(parentColIndex);
                int enumId = 0;
                foreach (var val in parentValues)
                {
                    parentValueToEnumId[val] = enumId++;
                }
            }

            // 옵션 값 생성
            int valueEnumId = 0;
            foreach (var relation in valueRelations)
            {
                var optionValue = new OptionValue
                {
                    EnumId = valueEnumId++,
                    Name = relation.Value,
                    Desc = ""
                };

                // Filter 설정
                if (parentOptionId == "-1")
                {
                    // 첫 번째 계층 (타입)
                    optionValue.Filter = new List<string> { "-1" };
                    optionValue.FilterValues = new List<List<string>> { new List<string> { "-1" } };
                }
                else
                {
                    // 상위 옵션에 의존
                    optionValue.Filter = new List<string> { parentOptionId };
                    
                    // 부모 값들의 enumId 리스트
                    var parentEnumIds = new List<string>();
                    foreach (var parentVal in relation.ParentValues)
                    {
                        if (parentValueToEnumId.ContainsKey(parentVal))
                        {
                            parentEnumIds.Add(parentValueToEnumId[parentVal].ToString());
                        }
                    }
                    
                    if (parentEnumIds.Count == 0)
                    {
                        parentEnumIds.Add("-1");
                    }
                    
                    optionValue.FilterValues = new List<List<string>> { parentEnumIds };
                }

                option.Values.Add(optionValue);
            }

            return option;
        }

        /// <summary>
        /// 리프 옵션 생성
        /// </summary>
        private OptionItem BuildLeafOption(int colIndex, int optionId, Dictionary<int, int> columnToOptionId)
        {
            string columnName = _data.Headers[colIndex];
            
            // 특수 값 확인 (E: EditBox, C: CheckBox, X: 제외)
            var firstValue = GetFirstNonEmptyValue(colIndex);
            string controlType = DetermineControlType(firstValue);
            
            if (controlType == "EXCLUDE")
                return null;

            var option = new OptionItem
            {
                Id = optionId,
                Name = columnName,
                DefaultValue = "0",
                Type = controlType
            };

            // 리프 컬럼은 마지막 계층 컬럼(사이즈)에 의존
            int lastHierarchyColIndex = _hierarchyColumns.LastOrDefault();
            string parentOptionId = columnToOptionId.ContainsKey(lastHierarchyColIndex)
                ? columnToOptionId[lastHierarchyColIndex].ToString()
                : "-1";

            // 고유 값 수집
            var uniqueValues = CollectUniqueValues(colIndex);

            int valueEnumId = 0;
            foreach (var value in uniqueValues)
            {
                // 특수 값은 건너뜀
                if (value == "E" || value == "C" || value == "X")
                    continue;

                var optionValue = new OptionValue
                {
                    EnumId = valueEnumId++,
                    Name = value,
                    Desc = "",
                    Filter = new List<string> { parentOptionId },
                    FilterValues = new List<List<string>> { new List<string> { "-1" } }  // 리프는 모든 사이즈에 적용
                };

                option.Values.Add(optionValue);
            }

            return option;
        }

        /// <summary>
        /// 컬럼의 값과 부모 관계 수집
        /// </summary>
        private List<ValueRelation> CollectValueRelations(int colIndex)
        {
            var relations = new Dictionary<string, ValueRelation>();
            string columnName = _data.Headers[colIndex];
            
            int parentColIndex = GetParentColumnIndex(colIndex);
            string parentColumnName = parentColIndex >= 0 ? _data.Headers[parentColIndex] : null;

            foreach (var row in _data.DataRows)
            {
                if (!row.CompleteValues.ContainsKey(columnName))
                    continue;

                string cellValue = row.CompleteValues[columnName];
                if (string.IsNullOrWhiteSpace(cellValue))
                    continue;

                // 쉼표 또는 개행으로 분리
                var values = cellValue.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(v => v.Trim())
                                      .Where(v => !string.IsNullOrWhiteSpace(v));

                // 부모 값 가져오기
                string parentValue = null;
                if (parentColumnName != null && row.CompleteValues.ContainsKey(parentColumnName))
                {
                    parentValue = row.CompleteValues[parentColumnName]?.Trim();
                    // 부모가 여러 값이면 첫 번째만 (일반적으로 단일 값)
                    if (!string.IsNullOrEmpty(parentValue) && parentValue.Contains("\n"))
                    {
                        parentValue = parentValue.Split('\n')[0].Trim();
                    }
                    if (!string.IsNullOrEmpty(parentValue) && parentValue.Contains(","))
                    {
                        parentValue = parentValue.Split(',')[0].Trim();
                    }
                }

                foreach (var val in values)
                {
                    if (!relations.ContainsKey(val))
                    {
                        relations[val] = new ValueRelation
                        {
                            Value = val,
                            EnumId = relations.Count
                        };
                    }

                    if (!string.IsNullOrEmpty(parentValue))
                    {
                        relations[val].ParentValues.Add(parentValue);
                    }
                }
            }

            return relations.Values.ToList();
        }

        /// <summary>
        /// 컬럼의 고유 값 수집
        /// </summary>
        private List<string> CollectUniqueValues(int colIndex)
        {
            var uniqueValues = new HashSet<string>();
            string columnName = _data.Headers[colIndex];

            foreach (var row in _data.DataRows)
            {
                if (!row.CompleteValues.ContainsKey(columnName))
                    continue;

                string cellValue = row.CompleteValues[columnName];
                if (string.IsNullOrWhiteSpace(cellValue))
                    continue;

                // 쉼표 또는 개행으로 분리
                var values = cellValue.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(v => v.Trim())
                                      .Where(v => !string.IsNullOrWhiteSpace(v));

                foreach (var val in values)
                {
                    uniqueValues.Add(val);
                }
            }

            return uniqueValues.ToList();
        }

        /// <summary>
        /// 부모 컬럼 인덱스 반환
        /// </summary>
        private int GetParentColumnIndex(int colIndex)
        {
            int currentPosition = _hierarchyColumns.IndexOf(colIndex);
            if (currentPosition <= 0)
                return -1;  // 첫 번째 계층이거나 찾을 수 없음

            return _hierarchyColumns[currentPosition - 1];
        }

        /// <summary>
        /// 컬럼의 첫 번째 비어있지 않은 값 반환
        /// </summary>
        private string GetFirstNonEmptyValue(int colIndex)
        {
            string columnName = _data.Headers[colIndex];

            foreach (var row in _data.DataRows)
            {
                if (row.CompleteValues.ContainsKey(columnName))
                {
                    string value = row.CompleteValues[columnName]?.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }

            return null;
        }

        /// <summary>
        /// 컨트롤 타입 결정
        /// </summary>
        private string DetermineControlType(string firstValue)
        {
            if (string.IsNullOrWhiteSpace(firstValue))
                return "COMBO";

            switch (firstValue.ToUpper())
            {
                case "E":
                    return "EDITBOX";
                case "C":
                    return "CHECKBOX";
                case "X":
                    return "EXCLUDE";
                default:
                    return "COMBO";
            }
        }
    }
}
