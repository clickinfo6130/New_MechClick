using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OfficeOpenXml;

namespace BoltSpecProgram
{
    /// <summary>
    /// EPPlus를 사용하여 엑셀 파일을 읽는 클래스
    /// </summary>
    public class ExcelReader
    {
        public BoltSpecData ReadExcel(string filePath)
        {
            var data = new BoltSpecData();

            // EPPlus 라이센스 설정 (비상업적 용도)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = package.Workbook.Worksheets[0]; // 첫 번째 시트
                
                if (worksheet.Dimension == null)
                {
                    throw new Exception("엑셀 파일이 비어 있습니다.");
                }

                int rowCount = worksheet.Dimension.Rows;
                int colCount = worksheet.Dimension.Columns;

                // 첫 번째 행은 헤더
                for (int col = 1; col <= colCount; col++)
                {
                    var cellValue = worksheet.Cells[1, col].Value;
                    data.Headers.Add(cellValue?.ToString() ?? "");
                }

                // 2행부터 데이터
                var previousRow = new Dictionary<string, string>();
                
                for (int row = 2; row <= rowCount; row++)
                {
                    var dataRow = new BoltDataRow();
                    var currentRow = new Dictionary<string, string>();

                    for (int col = 1; col <= colCount; col++)
                    {
                        if (col - 1 >= data.Headers.Count) continue;
                        
                        var header = data.Headers[col - 1];
                        if (string.IsNullOrWhiteSpace(header)) continue;

                        var cellValue = worksheet.Cells[row, col].Value;
                        var value = cellValue?.ToString() ?? "";

                        dataRow.Values[header] = value;

                        // CompleteValues 구축: 빈 값이면 이전 행의 값 사용
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            if (previousRow.ContainsKey(header))
                            {
                                currentRow[header] = previousRow[header];
                            }
                            else
                            {
                                currentRow[header] = "";
                            }
                        }
                        else
                        {
                            currentRow[header] = value;
                        }
                    }

                    dataRow.CompleteValues = new Dictionary<string, string>(currentRow);
                    data.DataRows.Add(dataRow);

                    // 다음 행을 위해 현재 행 저장
                    previousRow = currentRow;
                }
            }

            return data;
        }

        /// <summary>
        /// 규격(표준번호) 컬럼에서 타입 값들을 추출
        /// 예: "KS B 1002:2016" -> "KS"
        /// </summary>
        public List<string> ExtractTypes(BoltSpecData data, string standardColumnName = "규격(표준번호)")
        {
            var types = new HashSet<string>();

            foreach (var row in data.DataRows)
            {
                if (row.CompleteValues.ContainsKey(standardColumnName))
                {
                    var standardValue = row.CompleteValues[standardColumnName];
                    if (!string.IsNullOrWhiteSpace(standardValue))
                    {
                        // 첫 공백 전까지의 문자열 추출
                        var parts = standardValue.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            types.Add(parts[0]);
                        }
                    }
                }
            }

            return types.OrderBy(t => t).ToList();
        }

        /// <summary>
        /// 특정 타입에 해당하는 규격(표준번호) 값들을 가져옴
        /// </summary>
        public List<string> GetStandardsByType(BoltSpecData data, string type, string standardColumnName = "규격(표준번호)")
        {
            var standards = new HashSet<string>();

            foreach (var row in data.DataRows)
            {
                if (row.CompleteValues.ContainsKey(standardColumnName))
                {
                    var standardValue = row.CompleteValues[standardColumnName];
                    if (!string.IsNullOrWhiteSpace(standardValue) && standardValue.StartsWith(type + " "))
                    {
                        standards.Add(standardValue);
                    }
                }
            }

            return standards.OrderBy(s => s).ToList();
        }
    }
}
