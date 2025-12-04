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
    /// 분류(Classification)를 최상위로, 종류(Series)에 CMD 코드 포함
    /// </summary>
    public class JsonExporter
    {
        #region JSON 구조 클래스

        /// <summary>
        /// 최상위 JSON 구조 - 분류(Classification) 포함
        /// </summary>
        public class RootData
        {
            [JsonProperty("Classification")]
            public List<ClassificationItem> Classification { get; set; } = new List<ClassificationItem>();
        }
        
        /// <summary>
        /// 분류 항목 (예: 볼트류, 너트류, 와셔류)
        /// </summary>
        public class ClassificationItem
        {
            [JsonProperty("name")]
            public string Name { get; set; }
            
            [JsonProperty("id")]
            public int Id { get; set; }
            
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
            
            [JsonProperty("CMD")]
            public string CMD { get; set; }

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
        
        // ✅ 분류 컬럼 인덱스 (0)
        private readonly int _classificationColumnIndex = 0;
        // ✅ 종류 컬럼 인덱스 (1)
        private readonly int _categoryColumnIndex = 1;

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
        /// 특정 종류(Series)만 JSON 문자열로 반환 (DB 저장용)
        /// </summary>
        public string ExportSeriesToString(string categoryName)
        {
            var rootData = BuildJsonStructure();
            
            // 해당 종류만 찾기
            SeriesItem targetSeries = null;
            foreach (var classification in rootData.Classification)
            {
                var series = classification.Series.FirstOrDefault(s => s.Name == categoryName);
                if (series != null)
                {
                    targetSeries = series;
                    break;
                }
            }
            
            if (targetSeries == null)
            {
                // 전체 중 첫 번째 Series 반환
                if (rootData.Classification.Count > 0 && rootData.Classification[0].Series.Count > 0)
                {
                    targetSeries = rootData.Classification[0].Series[0];
                }
            }
            
            if (targetSeries == null)
                return null;
            
            // 단일 Series를 포함하는 구조 생성
            var singleSeriesData = new
            {
                Series = new List<SeriesItem> { targetSeries }
            };
            
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };

            return JsonConvert.SerializeObject(singleSeriesData, settings);
        }
        
        /// <summary>
        /// 특정 종류의 CMD 코드 반환
        /// </summary>
        public string GetCMDCodeForCategory(string categoryName)
        {
            return GetCMDCode(categoryName);
        }

        /// <summary>
        /// JSON 구조 생성 - 분류(Classification)를 최상위로
        /// </summary>
        private RootData BuildJsonStructure()
        {
            var rootData = new RootData();
            
            // ✅ 분류(Classification) 수집
            var classifications = GetAllClassifications();
            int classificationId = 1;
            
            foreach (var classificationName in classifications)
            {
                var classification = new ClassificationItem
                {
                    Name = classificationName,
                    Id = classificationId++
                };
                
                // ✅ 해당 분류에 속하는 종류(Series) 수집
                var categories = GetCategoriesByClassification(classificationName);
                int seriesId = 1;
                
                foreach (var categoryName in categories)
                {
                    var series = BuildSeriesItem(categoryName, seriesId++);
                    if (series != null)
                    {
                        classification.Series.Add(series);
                    }
                }
                
                rootData.Classification.Add(classification);
            }

            return rootData;
        }
        
        /// <summary>
        /// 모든 분류(Classification) 목록 수집
        /// </summary>
        private List<string> GetAllClassifications()
        {
            var classifications = new HashSet<string>();
            
            if (_data.Headers.Count <= _classificationColumnIndex)
                return new List<string> { "Unknown" };
                
            string columnName = _data.Headers[_classificationColumnIndex];
            
            foreach (var row in _data.DataRows)
            {
                if (row.CompleteValues.ContainsKey(columnName))
                {
                    var value = row.CompleteValues[columnName]?.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        classifications.Add(value);
                    }
                }
            }
            
            return classifications.OrderBy(c => c).ToList();
        }
        
        /// <summary>
        /// 특정 분류에 속하는 종류(Category) 목록 수집
        /// </summary>
        private List<string> GetCategoriesByClassification(string classificationName)
        {
            var categories = new HashSet<string>();
            
            if (_data.Headers.Count <= _categoryColumnIndex)
                return categories.ToList();
                
            string classColumnName = _data.Headers[_classificationColumnIndex];
            string catColumnName = _data.Headers[_categoryColumnIndex];
            
            foreach (var row in _data.DataRows)
            {
                if (row.CompleteValues.ContainsKey(classColumnName) &&
                    row.CompleteValues.ContainsKey(catColumnName))
                {
                    var classValue = row.CompleteValues[classColumnName]?.Trim();
                    var catValue = row.CompleteValues[catColumnName]?.Trim();
                    
                    if (classValue == classificationName && !string.IsNullOrWhiteSpace(catValue))
                    {
                        categories.Add(catValue);
                    }
                }
            }
            
            return categories.OrderBy(c => c).ToList();
        }
        
        /// <summary>
        /// 종류(Series) 항목 생성
        /// </summary>
        private SeriesItem BuildSeriesItem(string categoryName, int seriesId)
        {
            // ✅ 해당 종류에 속하는 데이터만 필터링
            var filteredData = FilterDataByCategory(categoryName);
            if (filteredData.DataRows.Count == 0)
                return null;
            
            var series = new SeriesItem
            {
                Id = seriesId,
                Name = categoryName,
                CMD = GetCMDCode(categoryName)  // ✅ Name_Code에서 CMD 가져오기
            };

            // 옵션 ID 매핑 (컬럼 인덱스 → 옵션 ID)
            var columnToOptionId = new Dictionary<int, int>();
            int optionId = 0;

            // 계층 컬럼들에 대한 옵션 생성
            foreach (var colIndex in _hierarchyColumns)
            {
                var option = BuildHierarchyOption(filteredData, colIndex, optionId, columnToOptionId);
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
                var option = BuildLeafOption(filteredData, colIndex, optionId, columnToOptionId);
                if (option != null && option.Values.Count > 0)
                {
                    series.Options.Add(option);
                    columnToOptionId[colIndex] = optionId;
                    optionId++;
                }
            }

            return series;
        }
        
        /// <summary>
        /// 종류 이름에서 CMD 코드 가져오기
        /// </summary>
        private string GetCMDCode(string categoryName)
        {
            if (_data.NameCodeMap != null && _data.NameCodeMap.ContainsKey(categoryName))
            {
                return _data.NameCodeMap[categoryName].Code;
            }
            return "";  // 매핑이 없으면 빈 문자열
        }
        
        /// <summary>
        /// 종류별로 데이터 필터링
        /// </summary>
        private BoltSpecData FilterDataByCategory(string categoryName)
        {
            var filtered = new BoltSpecData
            {
                Headers = _data.Headers,
                NameCodeMap = _data.NameCodeMap
            };
            
            if (_data.Headers.Count <= _categoryColumnIndex)
                return filtered;
                
            string catColumnName = _data.Headers[_categoryColumnIndex];
            
            foreach (var row in _data.DataRows)
            {
                if (row.CompleteValues.ContainsKey(catColumnName))
                {
                    var value = row.CompleteValues[catColumnName]?.Trim();
                    if (value == categoryName)
                    {
                        filtered.DataRows.Add(row);
                    }
                }
            }
            
            return filtered;
        }

        /// <summary>
        /// 계층 옵션 생성
        /// </summary>
        private OptionItem BuildHierarchyOption(BoltSpecData data, int colIndex, int optionId, Dictionary<int, int> columnToOptionId)
        {
            string columnName = data.Headers[colIndex];
            
            var option = new OptionItem
            {
                Id = optionId,
                Name = columnName,
                DefaultValue = "0",
                Type = colIndex == 8 ? "LISTBOX" : "COMBO"  // 사이즈(8)는 LISTBOX
            };

            // 이 컬럼의 모든 고유값과 부모 관계 수집
            var valueRelations = CollectValueRelations(data, colIndex);

            // 부모 옵션 ID 결정
            int parentColIndex = GetParentColumnIndex(colIndex);
            string parentOptionId = parentColIndex >= 0 && columnToOptionId.ContainsKey(parentColIndex)
                ? columnToOptionId[parentColIndex].ToString()
                : "-1";

            // 부모 값들의 enumId 매핑
            var parentValueToEnumId = new Dictionary<string, int>();
            if (parentColIndex >= 0)
            {
                var parentValues = CollectUniqueValues(data, parentColIndex);
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
        private OptionItem BuildLeafOption(BoltSpecData data, int colIndex, int optionId, Dictionary<int, int> columnToOptionId)
        {
            string columnName = data.Headers[colIndex];
            
            // 특수 값 확인 (E: EditBox, C: CheckBox, X: 제외)
            var firstValue = GetFirstNonEmptyValue(data, colIndex);
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
            var uniqueValues = CollectUniqueValues(data, colIndex);

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
        private List<ValueRelation> CollectValueRelations(BoltSpecData data, int colIndex)
        {
            var relations = new Dictionary<string, ValueRelation>();
            string columnName = data.Headers[colIndex];
            
            int parentColIndex = GetParentColumnIndex(colIndex);
            string parentColumnName = parentColIndex >= 0 ? data.Headers[parentColIndex] : null;

            foreach (var row in data.DataRows)
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
        private List<string> CollectUniqueValues(BoltSpecData data, int colIndex)
        {
            var uniqueValues = new HashSet<string>();
            string columnName = data.Headers[colIndex];

            foreach (var row in data.DataRows)
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
        private string GetFirstNonEmptyValue(BoltSpecData data, int colIndex)
        {
            string columnName = data.Headers[colIndex];

            foreach (var row in data.DataRows)
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
