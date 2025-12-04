using System.Collections.Generic;

namespace DimensionManager.Models
{
    /// <summary>
    /// 엑셀에서 읽은 치수 데이터 Import 결과
    /// </summary>
    public class DimensionImportResult
    {
        public string PartCode { get; set; }
        public string Standard { get; set; }
        
        // 컬럼 정보 (4행)
        public List<ColumnInfo> Columns { get; set; }
        
        // 키 필드 정보
        public List<KeyFieldInfo> KeyFields { get; set; }
        
        // 치수 필드 정보 (메타데이터)
        public List<DimensionMeta> DimensionMetas { get; set; }
        
        // 키값 옵션들
        public List<DimensionKeyOption> KeyOptions { get; set; }
        
        // 실제 치수 데이터
        public List<PartDimension> Dimensions { get; set; }
        
        // 원본 데이터 (DataGrid 표시용)
        public List<Dictionary<string, object>> RawData { get; set; }

        public DimensionImportResult()
        {
            Columns = new List<ColumnInfo>();
            KeyFields = new List<KeyFieldInfo>();
            DimensionMetas = new List<DimensionMeta>();
            KeyOptions = new List<DimensionKeyOption>();
            Dimensions = new List<PartDimension>();
            RawData = new List<Dictionary<string, object>>();
        }
    }

    /// <summary>
    /// 엑셀 컬럼 정보
    /// </summary>
    public class ColumnInfo
    {
        public int Index { get; set; }
        public string FieldName { get; set; }           // 유니크한 필드명 (중복 시 _2, _3 추가)
        public string OriginalFieldName { get; set; }   // 원본 필드명
        public string DisplayName { get; set; }         // Row 2 값 (한글 표시명)
        public string SubDisplayName { get; set; }      // Row 3 값 (부가 설명)
        public bool IsKeyField { get; set; }            // 키 필드 여부
        public int KeyLevel { get; set; }               // 키 레벨 (1, 2, 3...)
    }

    /// <summary>
    /// 키 필드 정보
    /// </summary>
    public class KeyFieldInfo
    {
        public int Level { get; set; }
        public string FieldName { get; set; }
        public string DisplayName { get; set; }
        public List<string> UniqueValues { get; set; }

        public KeyFieldInfo()
        {
            UniqueValues = new List<string>();
        }
    }
}
