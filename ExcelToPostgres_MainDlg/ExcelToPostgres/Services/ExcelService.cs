using System;
using System.Collections.ObjectModel;
using System.IO;
using ExcelToPostgres.Models;
using OfficeOpenXml;

namespace ExcelToPostgres.Services
{
    public class ExcelLoadResult
    {
        public ObservableCollection<MainCategory> MainCategories { get; set; }
        public ObservableCollection<SubCategory> SubCategories { get; set; }
        public ObservableCollection<MidCategory> MidCategories { get; set; }
        public ObservableCollection<PartType> PartTypes { get; set; }
        public ObservableCollection<PartSeries> PartSeriesList { get; set; }

        public ExcelLoadResult()
        {
            MainCategories = new ObservableCollection<MainCategory>();
            SubCategories = new ObservableCollection<SubCategory>();
            MidCategories = new ObservableCollection<MidCategory>();
            PartTypes = new ObservableCollection<PartType>();
            PartSeriesList = new ObservableCollection<PartSeries>();
        }
    }

    public class ExcelService
    {
        static ExcelService()
        {
            // EPPlus 5.x 이상 라이선스 설정
            OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
        }

        public ExcelService()
        {
        }

        public ExcelLoadResult LoadFromExcel(string filePath)
        {
            var result = new ExcelLoadResult();

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                // MainCategory 시트 읽기
                var wsMain = package.Workbook.Worksheets["MainCategory"];
                if (wsMain != null)
                {
                    for (int row = 2; row <= wsMain.Dimension.End.Row; row++)
                    {
                        var item = new MainCategory
                        {
                            MainCatCode = GetCellValue(wsMain, row, 1),
                            MainCatName = GetCellValue(wsMain, row, 2),
                            MainCatNameKr = GetCellValue(wsMain, row, 3),
                            IsStandard = ParseBool(GetCellValue(wsMain, row, 4)),
                            ColorCode = GetCellValue(wsMain, row, 5),
                            SortOrder = ParseInt(GetCellValue(wsMain, row, 6)),
                            IsActive = ParseBool(GetCellValue(wsMain, row, 7)),
                            Description = GetCellValue(wsMain, row, 8)
                        };
                        if (!string.IsNullOrEmpty(item.MainCatCode))
                            result.MainCategories.Add(item);
                    }
                }

                // SubCategory 시트 읽기
                var wsSub = package.Workbook.Worksheets["SubCategory"];
                if (wsSub != null)
                {
                    for (int row = 2; row <= wsSub.Dimension.End.Row; row++)
                    {
                        var item = new SubCategory
                        {
                            SubCatCode = GetCellValue(wsSub, row, 1),
                            SubCatName = GetCellValue(wsSub, row, 2),
                            SubCatNameKr = GetCellValue(wsSub, row, 3),
                            MainCatCode = GetCellValue(wsSub, row, 4),
                            IsVendor = ParseBool(GetCellValue(wsSub, row, 5)),
                            VendorCode = GetCellValue(wsSub, row, 6),
                            Country = GetCellValue(wsSub, row, 7),
                            SortOrder = ParseInt(GetCellValue(wsSub, row, 8)),
                            IsActive = ParseBool(GetCellValue(wsSub, row, 9)),
                            Description = GetCellValue(wsSub, row, 10)
                        };
                        if (!string.IsNullOrEmpty(item.SubCatCode))
                            result.SubCategories.Add(item);
                    }
                }

                // MidCategory 시트 읽기
                var wsMid = package.Workbook.Worksheets["MidCategory"];
                if (wsMid != null)
                {
                    for (int row = 2; row <= wsMid.Dimension.End.Row; row++)
                    {
                        var item = new MidCategory
                        {
                            MidCatCode = GetCellValue(wsMid, row, 1),
                            MidCatName = GetCellValue(wsMid, row, 2),
                            MidCatNameKr = GetCellValue(wsMid, row, 3),
                            SubCatCode = GetCellValue(wsMid, row, 4),
                            SortOrder = ParseInt(GetCellValue(wsMid, row, 5)),
                            IsActive = ParseBool(GetCellValue(wsMid, row, 6)),
                            Description = GetCellValue(wsMid, row, 7)
                        };
                        if (!string.IsNullOrEmpty(item.MidCatCode))
                            result.MidCategories.Add(item);
                    }
                }

                // PartType 시트 읽기
                var wsPartType = package.Workbook.Worksheets["PartType"];
                if (wsPartType != null)
                {
                    for (int row = 2; row <= wsPartType.Dimension.End.Row; row++)
                    {
                        var item = new PartType
                        {
                            PartTypeCode = GetCellValue(wsPartType, row, 1),
                            PartTypeName = GetCellValue(wsPartType, row, 2),
                            PartTypeNameKr = GetCellValue(wsPartType, row, 3),
                            SubCatCode = GetCellValue(wsPartType, row, 4),
                            MidCatCode = GetCellValue(wsPartType, row, 5),
                            VendorCode = GetCellValue(wsPartType, row, 6),
                            HasSeries = ParseBool(GetCellValue(wsPartType, row, 7)),
                            SortOrder = ParseInt(GetCellValue(wsPartType, row, 8)),
                            IsActive = ParseBool(GetCellValue(wsPartType, row, 9)),
                            Description = GetCellValue(wsPartType, row, 10)
                        };
                        if (!string.IsNullOrEmpty(item.PartTypeCode))
                            result.PartTypes.Add(item);
                    }
                }

                // PartSeries 시트 읽기
                var wsSeries = package.Workbook.Worksheets["PartSeries"];
                if (wsSeries != null)
                {
                    for (int row = 2; row <= wsSeries.Dimension.End.Row; row++)
                    {
                        var item = new PartSeries
                        {
                            SeriesCode = GetCellValue(wsSeries, row, 1),
                            SeriesName = GetCellValue(wsSeries, row, 2),
                            SeriesNameKr = GetCellValue(wsSeries, row, 3),
                            PartTypeCode = GetCellValue(wsSeries, row, 4),
                            VendorCode = GetCellValue(wsSeries, row, 5),
                            ModelPrefix = GetCellValue(wsSeries, row, 6),
                            SortOrder = ParseInt(GetCellValue(wsSeries, row, 7)),
                            IsActive = ParseBool(GetCellValue(wsSeries, row, 8)),
                            Description = GetCellValue(wsSeries, row, 9)
                        };
                        if (!string.IsNullOrEmpty(item.SeriesCode))
                            result.PartSeriesList.Add(item);
                    }
                }
            }

            return result;
        }

        private string GetCellValue(ExcelWorksheet ws, int row, int col)
        {
            var value = ws.Cells[row, col].Value;
            if (value == null) return null;
            string str = value.ToString().Trim();
            if (string.IsNullOrEmpty(str)) return null;
            return str;
        }

        private bool ParseBool(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.ToUpper() == "TRUE" || value == "1";
        }

        private int ParseInt(string value)
        {
            int result;
            if (int.TryParse(value, out result))
                return result;
            return 0;
        }
    }
}
