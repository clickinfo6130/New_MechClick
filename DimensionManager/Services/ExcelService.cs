using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DimensionManager.Models;
using OfficeOpenXml;

namespace DimensionManager.Services
{
    public class ExcelService
    {
        // 키 필드로 인식할 컬럼명들
        private readonly HashSet<string> _keyFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Name", "Standard", "List", "용도", "Usage", "Material", "재질", 
            "Grade", "강도등급", "Surface", "표면처리", "Type", "형태"
        };

        static ExcelService()
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
        }

        /// <summary>
        /// 엑셀 파일에서 치수 데이터 읽기
        /// </summary>
        /// <param name="filePath">엑셀 파일 경로</param>
        /// <param name="headerRow">헤더 행 번호 (기본값: 3)</param>
        /// <param name="displayNameRow">표시명 행 번호 (기본값: 2)</param>
        /// <param name="dataStartRow">데이터 시작 행 번호 (기본값: 4)</param>
        public DimensionImportResult ReadDimensionExcel(string filePath, int headerRow = 3, int displayNameRow = 2, int dataStartRow = 4)
        {
            var result = new DimensionImportResult();

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var ws = package.Workbook.Worksheets[0];
                if (ws == null)
                    throw new Exception("시트가 없습니다.");

                int maxCol = ws.Dimension.End.Column;
                int maxRow = ws.Dimension.End.Row;

                // 1. 컬럼 정보 읽기 (중복 컬럼명 처리 포함)
                ReadColumnInfo(ws, result, headerRow, displayNameRow, maxCol);

                // 2. 키 필드 식별 및 레벨 설정
                IdentifyKeyFields(result);

                // 3. 데이터 읽기
                ReadData(ws, result, dataStartRow, maxRow, maxCol);

                // 4. PartCode 추출 (Name 컬럼에서)
                ExtractPartCode(result);

                // 5. 메타데이터 생성
                GenerateDimensionMetas(result);

                // 6. 키 옵션 생성
                GenerateKeyOptions(result);

                // 7. PartDimension 생성
                GeneratePartDimensions(result);
            }

            return result;
        }

        private void ReadColumnInfo(ExcelWorksheet ws, DimensionImportResult result, int headerRow, int displayNameRow, int maxCol)
        {
            // 중복 컬럼명 처리를 위한 딕셔너리
            var columnNameCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int col = 1; col <= maxCol; col++)
            {
                var rawFieldName = GetCellValue(ws, headerRow, col);
                
                // 빈 컬럼명 처리
                if (string.IsNullOrEmpty(rawFieldName))
                {
                    rawFieldName = "Column" + col;
                }

                // 중복 컬럼명 처리
                string fieldName = rawFieldName;
                if (columnNameCount.ContainsKey(rawFieldName))
                {
                    columnNameCount[rawFieldName]++;
                    fieldName = rawFieldName + "_" + columnNameCount[rawFieldName];
                }
                else
                {
                    columnNameCount[rawFieldName] = 1;
                }

                var displayName = GetCellValue(ws, displayNameRow, col);
                if (string.IsNullOrEmpty(displayName))
                {
                    displayName = rawFieldName;
                }

                var colInfo = new ColumnInfo
                {
                    Index = col,
                    FieldName = fieldName.Trim(),
                    OriginalFieldName = rawFieldName.Trim(),  // 원본 필드명 저장
                    DisplayName = displayName.Replace("\n", " ").Trim(),
                    SubDisplayName = null,
                    IsKeyField = _keyFieldNames.Contains(rawFieldName.Trim())
                };

                result.Columns.Add(colInfo);
            }
        }

        private void IdentifyKeyFields(DimensionImportResult result)
        {
            int keyLevel = 1;
            foreach (var col in result.Columns.Where(c => c.IsKeyField))
            {
                col.KeyLevel = keyLevel++;

                var keyField = new KeyFieldInfo
                {
                    Level = col.KeyLevel,
                    FieldName = col.FieldName,
                    DisplayName = col.DisplayName
                };
                result.KeyFields.Add(keyField);
            }
        }

        private void ReadData(ExcelWorksheet ws, DimensionImportResult result, int startRow, int maxRow, int maxCol)
        {
            for (int row = startRow; row <= maxRow; row++)
            {
                var rowData = new Dictionary<string, object>();
                bool hasData = false;

                // 첫 번째 컬럼(Name)이 비어있으면 스킵
                var firstColValue = ws.Cells[row, 1].Value;
                if (firstColValue == null || string.IsNullOrWhiteSpace(firstColValue.ToString()))
                {
                    continue;
                }

                foreach (var col in result.Columns)
                {
                    var value = ws.Cells[row, col.Index].Value;
                    if (value != null)
                    {
                        hasData = true;
                        rowData[col.FieldName] = value;

                        // 키 필드의 고유값 수집
                        if (col.IsKeyField)
                        {
                            var keyField = result.KeyFields.FirstOrDefault(k => k.FieldName == col.FieldName);
                            if (keyField != null)
                            {
                                string strValue = value.ToString().Trim();
                                if (!string.IsNullOrEmpty(strValue) && !keyField.UniqueValues.Contains(strValue))
                                {
                                    keyField.UniqueValues.Add(strValue);
                                }
                            }
                        }
                    }
                }

                if (hasData)
                {
                    result.RawData.Add(rowData);
                }
            }
        }

        private void ExtractPartCode(DimensionImportResult result)
        {
            if (result.RawData.Count > 0)
            {
                // Name 컬럼 찾기
                var nameCol = result.Columns.FirstOrDefault(c => 
                    c.OriginalFieldName.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
                    c.FieldName.Equals("Name", StringComparison.OrdinalIgnoreCase));
                
                if (nameCol != null && result.RawData[0].ContainsKey(nameCol.FieldName))
                {
                    result.PartCode = result.RawData[0][nameCol.FieldName]?.ToString() ?? "";
                }

                // Standard 추출
                var stdCol = result.Columns.FirstOrDefault(c => 
                    c.OriginalFieldName.Equals("Standard", StringComparison.OrdinalIgnoreCase) ||
                    c.FieldName.Equals("Standard", StringComparison.OrdinalIgnoreCase));
                
                if (stdCol != null && result.RawData[0].ContainsKey(stdCol.FieldName))
                {
                    result.Standard = result.RawData[0][stdCol.FieldName]?.ToString() ?? "";
                }
            }
        }

        private void GenerateDimensionMetas(DimensionImportResult result)
        {
            int order = 1;
            foreach (var col in result.Columns)
            {
                var meta = new DimensionMeta
                {
                    PartCode = result.PartCode,
                    FieldName = col.FieldName,
                    DisplayName = string.IsNullOrEmpty(col.DisplayName) ? col.FieldName : col.DisplayName,
                    DisplayNameEn = col.OriginalFieldName ?? col.FieldName,
                    DataType = DetermineDataType(result.RawData, col.FieldName),
                    DecimalPlaces = 2,
                    Unit = "mm",
                    DisplayOrder = order++,
                    IsKeyField = col.IsKeyField,
                    IsDisplayField = true,
                    ColumnWidth = col.IsKeyField ? 100 : 80,
                    IsActive = true
                };

                result.DimensionMetas.Add(meta);
            }
        }

        private string DetermineDataType(List<Dictionary<string, object>> data, string fieldName)
        {
            if (data.Count == 0) return "TEXT";

            foreach (var row in data.Take(10))
            {
                if (row.ContainsKey(fieldName) && row[fieldName] != null)
                {
                    var value = row[fieldName];
                    if (value is double || value is decimal || value is float)
                        return "DECIMAL";
                    if (value is int || value is long)
                        return "INTEGER";
                    
                    // 문자열인 경우 숫자로 변환 시도
                    string strVal = value.ToString();
                    double d;
                    int i;
                    if (int.TryParse(strVal, out i))
                        return "INTEGER";
                    if (double.TryParse(strVal, out d))
                        return "DECIMAL";
                }
            }
            return "TEXT";
        }

        private void GenerateKeyOptions(DimensionImportResult result)
        {
            foreach (var keyField in result.KeyFields)
            {
                int sortOrder = 1;
                foreach (var value in keyField.UniqueValues)
                {
                    var option = new DimensionKeyOption
                    {
                        PartCode = result.PartCode,
                        KeyFieldName = keyField.FieldName,
                        KeyLevel = keyField.Level,
                        KeyValue = value,
                        ParentKey = null,
                        SortOrder = sortOrder++,
                        IsActive = true
                    };
                    result.KeyOptions.Add(option);
                }
            }
        }

        private void GeneratePartDimensions(DimensionImportResult result)
        {
            var keyFieldNames = result.KeyFields.Select(k => k.FieldName).ToList();
            var dimFieldNames = result.Columns.Where(c => !c.IsKeyField).Select(c => c.FieldName).ToList();

            foreach (var row in result.RawData)
            {
                // 키 컴포지트 생성
                var keyParts = new List<string>();
                var keyValuesDict = new Dictionary<string, string>();

                foreach (var keyFieldName in keyFieldNames)
                {
                    string keyValue = "";
                    if (row.ContainsKey(keyFieldName) && row[keyFieldName] != null)
                    {
                        keyValue = row[keyFieldName].ToString().Trim();
                    }
                    keyParts.Add(keyValue);
                    keyValuesDict[keyFieldName] = keyValue;
                }

                string keyComposite = string.Join("|", keyParts);

                // 치수 데이터 JSON 생성
                var dimDict = new Dictionary<string, object>();
                foreach (var fieldName in dimFieldNames)
                {
                    if (row.ContainsKey(fieldName) && row[fieldName] != null)
                    {
                        dimDict[fieldName] = row[fieldName];
                    }
                }

                var partDim = new PartDimension
                {
                    PartCode = result.PartCode,
                    KeyComposite = keyComposite,
                    KeyValuesJson = DictToJson(keyValuesDict),
                    DimensionDataJson = DictToJson(dimDict),
                    IsActive = true
                };

                result.Dimensions.Add(partDim);
            }
        }

        private string DictToJson(Dictionary<string, string> dict)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            bool first = true;
            foreach (var kvp in dict)
            {
                if (!first) sb.Append(",");
                sb.AppendFormat("\"{0}\":\"{1}\"", EscapeJson(kvp.Key), EscapeJson(kvp.Value));
                first = false;
            }
            sb.Append("}");
            return sb.ToString();
        }

        private string DictToJson(Dictionary<string, object> dict)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            bool first = true;
            foreach (var kvp in dict)
            {
                if (!first) sb.Append(",");
                
                string valueStr;
                if (kvp.Value is string)
                {
                    valueStr = "\"" + EscapeJson(kvp.Value.ToString()) + "\"";
                }
                else if (kvp.Value is bool)
                {
                    valueStr = (bool)kvp.Value ? "true" : "false";
                }
                else
                {
                    valueStr = kvp.Value?.ToString() ?? "null";
                }
                
                sb.AppendFormat("\"{0}\":{1}", EscapeJson(kvp.Key), valueStr);
                first = false;
            }
            sb.Append("}");
            return sb.ToString();
        }

        private string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private string GetCellValue(ExcelWorksheet ws, int row, int col)
        {
            if (row < 1) return null;
            var value = ws.Cells[row, col].Value;
            if (value == null) return null;
            string str = value.ToString().Trim();
            return string.IsNullOrEmpty(str) ? null : str;
        }
    }
}
