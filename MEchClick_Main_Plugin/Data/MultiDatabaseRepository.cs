// ============================================================================
// PartManager - MultiDatabaseRepository
// ============================================================================
// 파일명: MultiDatabaseRepository.cs
// 설명: Core DB와 업체별 DB를 통합 관리하는 Repository 클래스
// 버전: 1.0.0
// ============================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PartManager.Data
{
    /// <summary>
    /// 다중 데이터베이스 통합 관리 Repository
    /// - Core DB: 표준부품 + 마스터 데이터
    /// - Vendor DB: 업체별 상용부품 데이터 (ATTACH로 연결)
    /// </summary>
    public class MultiDatabaseRepository : IDisposable
    {
        #region Fields & Properties

        private SQLiteConnection _connection;
        private readonly string _coreDbPath;
        private readonly Dictionary<string, string> _attachedVendors = new Dictionary<string, string>();
        private bool _disposed = false;

        /// <summary>Core DB 연결 상태</summary>
        public bool IsConnected => _connection?.State == ConnectionState.Open;

        /// <summary>현재 연결된 업체 목록</summary>
        public IReadOnlyList<string> AttachedVendors => _attachedVendors.Keys.ToList();

        #endregion

        #region Constructor & Initialization

        /// <summary>
        /// MultiDatabaseRepository 생성자
        /// </summary>
        /// <param name="coreDbPath">Core DB 파일 경로 (standard_core.db)</param>
        public MultiDatabaseRepository(string coreDbPath)
        {
            _coreDbPath = coreDbPath ?? throw new ArgumentNullException(nameof(coreDbPath));

            if (!File.Exists(_coreDbPath))
            {
                throw new FileNotFoundException($"Core database not found: {_coreDbPath}");
            }

            InitializeConnection();
        }

        /// <summary>Core DB 연결 초기화</summary>
        private void InitializeConnection()
        {
            var connectionString = new SQLiteConnectionStringBuilder
            {
                DataSource = _coreDbPath,
                Version = 3,
                ForeignKeys = true,
                JournalMode = SQLiteJournalModeEnum.Wal,
                SyncMode = SynchronizationModes.Normal,
                CacheSize = 5000,
                PageSize = 4096
            }.ToString();

            _connection = new SQLiteConnection(connectionString);
            _connection.Open();

            // ATTACH 제한 확장 (기본 10개 → 30개)
            ExecuteNonQuery("PRAGMA max_attached = 30;");
        }

        #endregion

        #region Vendor Database Management

        /// <summary>
        /// 업체 DB를 Core DB에 ATTACH
        /// </summary>
        /// <param name="vendorCode">업체 코드 (SMC, THK 등)</param>
        /// <param name="vendorDbPath">업체 DB 파일 경로</param>
        /// <returns>성공 여부</returns>
        public bool AttachVendorDatabase(string vendorCode, string vendorDbPath)
        {
            if (string.IsNullOrEmpty(vendorCode))
                throw new ArgumentNullException(nameof(vendorCode));

            if (!File.Exists(vendorDbPath))
            {
                Console.WriteLine($"[Warning] Vendor database not found: {vendorDbPath}");
                return false;
            }

            // 이미 연결된 경우 스킵
            if (_attachedVendors.ContainsKey(vendorCode))
            {
                Console.WriteLine($"[Info] Vendor '{vendorCode}' already attached.");
                return true;
            }

            try
            {
                string alias = vendorCode.ToLower();
                string sql = $"ATTACH DATABASE '{vendorDbPath}' AS {alias};";
                ExecuteNonQuery(sql);

                _attachedVendors[vendorCode] = alias;
                Console.WriteLine($"[Success] Attached vendor database: {vendorCode} -> {alias}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to attach vendor database '{vendorCode}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 업체 DB를 Core DB에서 DETACH
        /// </summary>
        /// <param name="vendorCode">업체 코드</param>
        /// <returns>성공 여부</returns>
        public bool DetachVendorDatabase(string vendorCode)
        {
            if (!_attachedVendors.TryGetValue(vendorCode, out string alias))
            {
                Console.WriteLine($"[Warning] Vendor '{vendorCode}' is not attached.");
                return false;
            }

            try
            {
                string sql = $"DETACH DATABASE {alias};";
                ExecuteNonQuery(sql);

                _attachedVendors.Remove(vendorCode);
                Console.WriteLine($"[Success] Detached vendor database: {vendorCode}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to detach vendor database '{vendorCode}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 설정 파일 기반으로 업체 DB들을 일괄 연결
        /// </summary>
        /// <param name="config">AppConfig 객체</param>
        /// <param name="vendorDbFolder">업체 DB 폴더 경로</param>
        public void AttachVendorsFromConfig(AppConfig config, string vendorDbFolder)
        {
            if (config?.EnabledVendors == null) return;

            foreach (var vendorCode in config.EnabledVendors)
            {
                if (vendorCode == "STANDARD") continue; // Core DB에 포함

                string dbFileName = null;
                if (config.VendorFiles != null)
                {
                    config.VendorFiles.TryGetValue(vendorCode, out dbFileName);
                }
                if (string.IsNullOrEmpty(dbFileName))
                {
                    dbFileName = $"vendor_{vendorCode.ToLower()}.db";
                }

                string dbPath = Path.Combine(vendorDbFolder, dbFileName);
                AttachVendorDatabase(vendorCode, dbPath);
            }
        }

        #endregion

        #region Vendor Queries

        /// <summary>
        /// 모든 업체 목록 조회
        /// </summary>
        public List<VendorInfo> GetAllVendors()
        {
            const string sql = @"
                SELECT vendor_code, vendor_name, vendor_name_kr, country, 
                       db_file_name, is_standard, is_active, sort_order, description
                FROM Vendor
                WHERE is_active = 1
                ORDER BY sort_order, vendor_name_kr";

            return ExecuteQuery(sql, reader => new VendorInfo
            {
                VendorCode = reader.GetString(0),
                VendorName = reader.GetString(1),
                VendorNameKr = reader.IsDBNull(2) ? null : reader.GetString(2),
                Country = reader.IsDBNull(3) ? null : reader.GetString(3),
                DbFileName = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsStandard = reader.GetInt32(5) == 1,
                IsActive = reader.GetInt32(6) == 1,
                SortOrder = reader.GetInt32(7),
                Description = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }

        /// <summary>
        /// 활성화된 업체만 조회
        /// </summary>
        public List<VendorInfo> GetEnabledVendors()
        {
            var allVendors = GetAllVendors();

            // STANDARD는 항상 포함
            var result = allVendors.Where(v => v.IsStandard).ToList();

            // ATTACH된 업체만 추가
            result.AddRange(allVendors.Where(v => !v.IsStandard && _attachedVendors.ContainsKey(v.VendorCode)));

            return result.OrderBy(v => v.SortOrder).ToList();
        }

        #endregion

        #region Category Queries

        /// <summary>
        /// 카테고리 계층 구조 조회
        /// </summary>
        public List<CategoryInfo> GetCategoryHierarchy()
        {
            const string sql = @"
                SELECT category_id, category_code, category_name, category_name_kr,
                       parent_code, category_level, sort_order
                FROM Category
                WHERE is_active = 1
                ORDER BY category_level, sort_order";

            return ExecuteQuery(sql, reader => new CategoryInfo
            {
                CategoryId = reader.GetInt32(0),
                CategoryCode = reader.GetString(1),
                CategoryName = reader.GetString(2),
                CategoryNameKr = reader.IsDBNull(3) ? null : reader.GetString(3),
                ParentCode = reader.IsDBNull(4) ? null : reader.GetString(4),
                CategoryLevel = reader.GetInt32(5),
                SortOrder = reader.GetInt32(6)
            });
        }

        /// <summary>
        /// 특정 레벨의 카테고리 조회
        /// </summary>
        public List<CategoryInfo> GetCategoriesByLevel(int level)
        {
            const string sql = @"
                SELECT category_id, category_code, category_name, category_name_kr,
                       parent_code, category_level, sort_order
                FROM Category
                WHERE is_active = 1 AND category_level = @level
                ORDER BY sort_order";

            return ExecuteQuery(sql, reader => new CategoryInfo
            {
                CategoryId = reader.GetInt32(0),
                CategoryCode = reader.GetString(1),
                CategoryName = reader.GetString(2),
                CategoryNameKr = reader.IsDBNull(3) ? null : reader.GetString(3),
                ParentCode = reader.IsDBNull(4) ? null : reader.GetString(4),
                CategoryLevel = reader.GetInt32(5),
                SortOrder = reader.GetInt32(6)
            }, new SQLiteParameter("@level", level));
        }

        /// <summary>
        /// 하위 카테고리 조회
        /// </summary>
        public List<CategoryInfo> GetChildCategories(string parentCode)
        {
            const string sql = @"
                SELECT category_id, category_code, category_name, category_name_kr,
                       parent_code, category_level, sort_order
                FROM Category
                WHERE is_active = 1 AND parent_code = @parentCode
                ORDER BY sort_order";

            return ExecuteQuery(sql, reader => new CategoryInfo
            {
                CategoryId = reader.GetInt32(0),
                CategoryCode = reader.GetString(1),
                CategoryName = reader.GetString(2),
                CategoryNameKr = reader.IsDBNull(3) ? null : reader.GetString(3),
                ParentCode = reader.IsDBNull(4) ? null : reader.GetString(4),
                CategoryLevel = reader.GetInt32(5),
                SortOrder = reader.GetInt32(6)
            }, new SQLiteParameter("@parentCode", parentCode));
        }

        #endregion

        #region PartSpec Queries

        /// <summary>
        /// 부품 사양 목록 조회 (Core DB + ATTACH된 업체 DB 통합)
        /// </summary>
        /// <param name="vendorCode">업체 코드 (null이면 전체)</param>
        /// <param name="categoryCode">카테고리 코드 (null이면 전체)</param>
        public List<PartSpecInfo> GetPartSpecs(string vendorCode = null, string categoryCode = null)
        {
            var result = new List<PartSpecInfo>();

            // 1. Core DB (표준부품) 조회
            if (string.IsNullOrEmpty(vendorCode) || vendorCode == "STANDARD")
            {
                var coreSpecs = GetPartSpecsFromDb("main", vendorCode, categoryCode);
                result.AddRange(coreSpecs);
            }

            // 2. ATTACH된 업체 DB 조회
            foreach (var kvp in _attachedVendors)
            {
                if (!string.IsNullOrEmpty(vendorCode) && kvp.Key != vendorCode)
                    continue;

                var vendorSpecs = GetPartSpecsFromDb(kvp.Value, kvp.Key, categoryCode);
                result.AddRange(vendorSpecs);
            }

            return result.OrderBy(p => p.SortOrder).ThenBy(p => p.PartNameKr).ToList();
        }

        /// <summary>
        /// 특정 DB에서 부품 사양 조회
        /// </summary>
        private List<PartSpecInfo> GetPartSpecsFromDb(string dbAlias, string vendorCode, string categoryCode)
        {
            var conditions = new List<string> { "is_active = 1" };
            var parameters = new List<SQLiteParameter>();

            if (!string.IsNullOrEmpty(categoryCode))
            {
                conditions.Add("category_code = @categoryCode");
                parameters.Add(new SQLiteParameter("@categoryCode", categoryCode));
            }

            string whereClause = string.Join(" AND ", conditions);
            string sql = $@"
                SELECT part_id, part_code, part_name, part_name_kr, vendor_code, category_code,
                       filter_options, dimension_columns, standard_ref, is_active, sort_order, description
                FROM {dbAlias}.PartSpec
                WHERE {whereClause}
                ORDER BY sort_order, part_name_kr";

            try
            {
                return ExecuteQuery(sql, reader => new PartSpecInfo
                {
                    PartId = reader.GetInt32(0),
                    PartCode = reader.GetString(1),
                    PartName = reader.GetString(2),
                    PartNameKr = reader.IsDBNull(3) ? null : reader.GetString(3),
                    VendorCode = reader.GetString(4),
                    CategoryCode = reader.GetString(5),
                    FilterOptionsJson = reader.IsDBNull(6) ? null : reader.GetString(6),
                    DimensionColumnsJson = reader.IsDBNull(7) ? null : reader.GetString(7),
                    StandardRef = reader.IsDBNull(8) ? null : reader.GetString(8),
                    IsActive = reader.GetInt32(9) == 1,
                    SortOrder = reader.GetInt32(10),
                    Description = reader.IsDBNull(11) ? null : reader.GetString(11),
                    DbAlias = dbAlias
                }, parameters.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to query PartSpec from {dbAlias}: {ex.Message}");
                return new List<PartSpecInfo>();
            }
        }

        /// <summary>
        /// 부품 코드로 단일 부품 사양 조회
        /// </summary>
        public PartSpecInfo GetPartSpecByCode(string partCode)
        {
            // 1. Core DB에서 먼저 검색
            var result = GetPartSpecByCodeFromDb("main", partCode);
            if (result != null) return result;

            // 2. ATTACH된 업체 DB에서 검색
            foreach (var kvp in _attachedVendors)
            {
                result = GetPartSpecByCodeFromDb(kvp.Value, partCode);
                if (result != null) return result;
            }

            return null;
        }

        private PartSpecInfo GetPartSpecByCodeFromDb(string dbAlias, string partCode)
        {
            string sql = $@"
                SELECT part_id, part_code, part_name, part_name_kr, vendor_code, category_code,
                       filter_options, dimension_columns, standard_ref, is_active, sort_order, description
                FROM {dbAlias}.PartSpec
                WHERE part_code = @partCode AND is_active = 1";

            var results = ExecuteQuery(sql, reader => new PartSpecInfo
            {
                PartId = reader.GetInt32(0),
                PartCode = reader.GetString(1),
                PartName = reader.GetString(2),
                PartNameKr = reader.IsDBNull(3) ? null : reader.GetString(3),
                VendorCode = reader.GetString(4),
                CategoryCode = reader.GetString(5),
                FilterOptionsJson = reader.IsDBNull(6) ? null : reader.GetString(6),
                DimensionColumnsJson = reader.IsDBNull(7) ? null : reader.GetString(7),
                StandardRef = reader.IsDBNull(8) ? null : reader.GetString(8),
                IsActive = reader.GetInt32(9) == 1,
                SortOrder = reader.GetInt32(10),
                Description = reader.IsDBNull(11) ? null : reader.GetString(11),
                DbAlias = dbAlias
            }, new SQLiteParameter("@partCode", partCode));

            return results.FirstOrDefault();
        }

        #endregion

        #region PartDimension Queries

        /// <summary>
        /// 부품 치수 목록 조회
        /// </summary>
        /// <param name="partCode">부품 코드</param>
        /// <param name="specFilterKey">필터 키 (null이면 전체)</param>
        public List<PartDimensionInfo> GetPartDimensions(string partCode, string specFilterKey = null)
        {
            // 먼저 부품이 어느 DB에 있는지 확인
            var partSpec = GetPartSpecByCode(partCode);
            if (partSpec == null)
            {
                Console.WriteLine($"[Warning] Part not found: {partCode}");
                return new List<PartDimensionInfo>();
            }

            return GetPartDimensionsFromDb(partSpec.DbAlias, partCode, specFilterKey);
        }

        private List<PartDimensionInfo> GetPartDimensionsFromDb(string dbAlias, string partCode, string specFilterKey)
        {
            var conditions = new List<string> { "part_code = @partCode", "is_active = 1" };
            var parameters = new List<SQLiteParameter> { new SQLiteParameter("@partCode", partCode) };

            if (!string.IsNullOrEmpty(specFilterKey))
            {
                conditions.Add("spec_filter_key = @filterKey");
                parameters.Add(new SQLiteParameter("@filterKey", specFilterKey));
            }

            string whereClause = string.Join(" AND ", conditions);
            string sql = $@"
                SELECT dim_id, part_code, spec_filter_key, dim_key_1, dim_key_2, dim_key_3,
                       dimension_data, part_number, weight, material, surface_finish, remarks
                FROM {dbAlias}.PartDimension
                WHERE {whereClause}
                ORDER BY dim_key_1, dim_key_2, dim_key_3";

            return ExecuteQuery(sql, reader => new PartDimensionInfo
            {
                DimId = reader.GetInt32(0),
                PartCode = reader.GetString(1),
                SpecFilterKey = reader.GetString(2),
                DimKey1 = reader.IsDBNull(3) ? null : reader.GetString(3),
                DimKey2 = reader.IsDBNull(4) ? null : reader.GetString(4),
                DimKey3 = reader.IsDBNull(5) ? null : reader.GetString(5),
                DimensionDataJson = reader.GetString(6),
                PartNumber = reader.IsDBNull(7) ? null : reader.GetString(7),
                Weight = reader.IsDBNull(8) ? (double?)null : reader.GetDouble(8),
                Material = reader.IsDBNull(9) ? null : reader.GetString(9),
                SurfaceFinish = reader.IsDBNull(10) ? null : reader.GetString(10),
                Remarks = reader.IsDBNull(11) ? null : reader.GetString(11)
            }, parameters.ToArray());
        }

        /// <summary>
        /// 특정 치수 키로 단일 치수 데이터 조회
        /// </summary>
        public PartDimensionInfo GetPartDimension(string partCode, string specFilterKey, 
            string dimKey1, string dimKey2 = null, string dimKey3 = null)
        {
            var partSpec = GetPartSpecByCode(partCode);
            if (partSpec == null) return null;

            var conditions = new List<string> 
            { 
                "part_code = @partCode", 
                "spec_filter_key = @filterKey",
                "is_active = 1"
            };
            var parameters = new List<SQLiteParameter>
            {
                new SQLiteParameter("@partCode", partCode),
                new SQLiteParameter("@filterKey", specFilterKey)
            };

            if (!string.IsNullOrEmpty(dimKey1))
            {
                conditions.Add("dim_key_1 = @dimKey1");
                parameters.Add(new SQLiteParameter("@dimKey1", dimKey1));
            }
            if (!string.IsNullOrEmpty(dimKey2))
            {
                conditions.Add("dim_key_2 = @dimKey2");
                parameters.Add(new SQLiteParameter("@dimKey2", dimKey2));
            }
            if (!string.IsNullOrEmpty(dimKey3))
            {
                conditions.Add("dim_key_3 = @dimKey3");
                parameters.Add(new SQLiteParameter("@dimKey3", dimKey3));
            }

            string whereClause = string.Join(" AND ", conditions);
            string sql = $@"
                SELECT dim_id, part_code, spec_filter_key, dim_key_1, dim_key_2, dim_key_3,
                       dimension_data, part_number, weight, material, surface_finish, remarks
                FROM {partSpec.DbAlias}.PartDimension
                WHERE {whereClause}";

            var results = ExecuteQuery(sql, reader => new PartDimensionInfo
            {
                DimId = reader.GetInt32(0),
                PartCode = reader.GetString(1),
                SpecFilterKey = reader.GetString(2),
                DimKey1 = reader.IsDBNull(3) ? null : reader.GetString(3),
                DimKey2 = reader.IsDBNull(4) ? null : reader.GetString(4),
                DimKey3 = reader.IsDBNull(5) ? null : reader.GetString(5),
                DimensionDataJson = reader.GetString(6),
                PartNumber = reader.IsDBNull(7) ? null : reader.GetString(7),
                Weight = reader.IsDBNull(8) ? (double?)null : reader.GetDouble(8),
                Material = reader.IsDBNull(9) ? null : reader.GetString(9),
                SurfaceFinish = reader.IsDBNull(10) ? null : reader.GetString(10),
                Remarks = reader.IsDBNull(11) ? null : reader.GetString(11)
            }, parameters.ToArray());

            return results.FirstOrDefault();
        }

        /// <summary>
        /// 특정 필터 키에 대해 사용 가능한 DimKey 목록 조회
        /// </summary>
        public List<string> GetAvailableDimKeys(string partCode, string specFilterKey, int dimKeyLevel = 1)
        {
            var partSpec = GetPartSpecByCode(partCode);
            if (partSpec == null) return new List<string>();

            string dimKeyColumn;
            switch (dimKeyLevel)
            {
                case 1: dimKeyColumn = "dim_key_1"; break;
                case 2: dimKeyColumn = "dim_key_2"; break;
                case 3: dimKeyColumn = "dim_key_3"; break;
                default: dimKeyColumn = "dim_key_1"; break;
            }

            string sql = $@"
                SELECT DISTINCT {dimKeyColumn}
                FROM {partSpec.DbAlias}.PartDimension
                WHERE part_code = @partCode AND spec_filter_key = @filterKey 
                      AND {dimKeyColumn} IS NOT NULL AND is_active = 1
                ORDER BY CAST({dimKeyColumn} AS INTEGER), {dimKeyColumn}";

            return ExecuteQuery(sql, reader => reader.GetString(0),
                new SQLiteParameter("@partCode", partCode),
                new SQLiteParameter("@filterKey", specFilterKey));
        }

        #endregion

        #region UI Metadata Queries

        /// <summary>
        /// UI 메타데이터 조회
        /// </summary>
        public List<UIMetaInfo> GetUIMeta(string partCode)
        {
            var partSpec = GetPartSpecByCode(partCode);
            string dbAlias = partSpec?.DbAlias ?? "main";

            string sql = $@"
                SELECT meta_id, part_code, column_key, column_name, column_name_kr,
                       control_type, display_order, column_width, is_required, is_visible, default_value, tooltip
                FROM {dbAlias}.UIMeta
                WHERE part_code = @partCode
                ORDER BY display_order";

            return ExecuteQuery(sql, reader => new UIMetaInfo
            {
                MetaId = reader.GetInt32(0),
                PartCode = reader.GetString(1),
                ColumnKey = reader.GetString(2),
                ColumnName = reader.GetString(3),
                ColumnNameKr = reader.IsDBNull(4) ? null : reader.GetString(4),
                ControlType = reader.IsDBNull(5) ? "ComboBox" : reader.GetString(5),
                DisplayOrder = reader.GetInt32(6),
                ColumnWidth = reader.GetInt32(7),
                IsRequired = reader.GetInt32(8) == 1,
                IsVisible = reader.GetInt32(9) == 1,
                DefaultValue = reader.IsDBNull(10) ? null : reader.GetString(10),
                Tooltip = reader.IsDBNull(11) ? null : reader.GetString(11)
            }, new SQLiteParameter("@partCode", partCode));
        }

        #endregion

        #region AppConfig Queries

        /// <summary>
        /// 설정 값 조회
        /// </summary>
        public string GetConfig(string key, string defaultValue = null)
        {
            const string sql = "SELECT config_value FROM AppConfig WHERE config_key = @key";

            var results = ExecuteQuery(sql, reader => reader.IsDBNull(0) ? null : reader.GetString(0),
                new SQLiteParameter("@key", key));

            return results.FirstOrDefault() ?? defaultValue;
        }

        /// <summary>
        /// 설정 값 저장
        /// </summary>
        public void SetConfig(string key, string value, string configType = "string", string description = null)
        {
            const string sql = @"
                INSERT OR REPLACE INTO AppConfig (config_key, config_value, config_type, description, updated_at)
                VALUES (@key, @value, @type, @desc, datetime('now', 'localtime'))";

            ExecuteNonQuery(sql,
                new SQLiteParameter("@key", key),
                new SQLiteParameter("@value", value),
                new SQLiteParameter("@type", configType),
                new SQLiteParameter("@desc", (object)description ?? DBNull.Value));
        }

        /// <summary>
        /// 활성화된 업체 목록 설정 조회
        /// </summary>
        public List<string> GetEnabledVendorCodes()
        {
            string json = GetConfig("enabled_vendors", "[\"STANDARD\"]");
            try
            {
                return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string> { "STANDARD" };
            }
            catch
            {
                return new List<string> { "STANDARD" };
            }
        }

        /// <summary>
        /// 활성화된 업체 목록 설정 저장
        /// </summary>
        public void SetEnabledVendorCodes(List<string> vendorCodes)
        {
            // STANDARD는 항상 포함
            if (!vendorCodes.Contains("STANDARD"))
            {
                vendorCodes.Insert(0, "STANDARD");
            }

            string json = JsonConvert.SerializeObject(vendorCodes);
            SetConfig("enabled_vendors", json, "json", "활성화된 업체 목록");
        }

        #endregion

        #region Cross-Database Query (JOIN Example)

        /// <summary>
        /// 크로스 DB JOIN 쿼리 예시: 실린더 + 체결용 볼트 조회
        /// </summary>
        /// <param name="cylinderPartCode">실린더 부품 코드</param>
        /// <param name="boltSize">체결 볼트 사이즈 (M5, M6 등)</param>
        public List<PartDimensionInfo> GetCylinderWithBolts(string cylinderPartCode, string boltSize)
        {
            // 실린더가 있는 업체 DB 확인
            var cylinderSpec = GetPartSpecByCode(cylinderPartCode);
            if (cylinderSpec == null) return new List<PartDimensionInfo>();

            // 볼트는 항상 Core DB (main)에 있음
            string sql = $@"
                SELECT 
                    b.dim_id, b.part_code, b.spec_filter_key, 
                    b.dim_key_1, b.dim_key_2, b.dim_key_3,
                    b.dimension_data, b.part_number, b.weight, 
                    b.material, b.surface_finish, b.remarks
                FROM main.PartDimension b
                INNER JOIN main.PartSpec ps ON b.part_code = ps.part_code
                WHERE ps.category_code = 'BOLT' 
                  AND b.dim_key_1 = @boltSize
                  AND b.is_active = 1
                ORDER BY b.part_code, b.dim_key_2";

            return ExecuteQuery(sql, reader => new PartDimensionInfo
            {
                DimId = reader.GetInt32(0),
                PartCode = reader.GetString(1),
                SpecFilterKey = reader.GetString(2),
                DimKey1 = reader.IsDBNull(3) ? null : reader.GetString(3),
                DimKey2 = reader.IsDBNull(4) ? null : reader.GetString(4),
                DimKey3 = reader.IsDBNull(5) ? null : reader.GetString(5),
                DimensionDataJson = reader.GetString(6),
                PartNumber = reader.IsDBNull(7) ? null : reader.GetString(7),
                Weight = reader.IsDBNull(8) ? (double?)null : reader.GetDouble(8),
                Material = reader.IsDBNull(9) ? null : reader.GetString(9),
                SurfaceFinish = reader.IsDBNull(10) ? null : reader.GetString(10),
                Remarks = reader.IsDBNull(11) ? null : reader.GetString(11)
            }, new SQLiteParameter("@boltSize", boltSize));
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// SQL 실행 (Non-Query)
        /// </summary>
        private int ExecuteNonQuery(string sql, params SQLiteParameter[] parameters)
        {
            using (var cmd = new SQLiteCommand(sql, _connection))
            {
                if (parameters != null)
                    cmd.Parameters.AddRange(parameters);
                return cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// SQL 실행 (Query with mapping)
        /// </summary>
        private List<T> ExecuteQuery<T>(string sql, Func<SQLiteDataReader, T> mapper, params SQLiteParameter[] parameters)
        {
            var results = new List<T>();

            using (var cmd = new SQLiteCommand(sql, _connection))
            {
                if (parameters != null)
                    cmd.Parameters.AddRange(parameters);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(mapper(reader));
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// 스칼라 값 조회
        /// </summary>
        private T ExecuteScalar<T>(string sql, params SQLiteParameter[] parameters)
        {
            using (var cmd = new SQLiteCommand(sql, _connection))
            {
                if (parameters != null)
                    cmd.Parameters.AddRange(parameters);

                var result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value ? default(T) : (T)Convert.ChangeType(result, typeof(T));
            }
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // ATTACH된 DB 모두 DETACH
                    foreach (var vendorCode in _attachedVendors.Keys.ToList())
                    {
                        try { DetachVendorDatabase(vendorCode); } catch { }
                    }

                    // 연결 종료
                    _connection?.Close();
                    _connection?.Dispose();
                }

                _disposed = true;
            }
        }

        ~MultiDatabaseRepository()
        {
            Dispose(false);
        }

        #endregion
    }

    #region Data Models

    /// <summary>업체 정보</summary>
    public class VendorInfo
    {
        public string VendorCode { get; set; }
        public string VendorName { get; set; }
        public string VendorNameKr { get; set; }
        public string Country { get; set; }
        public string DbFileName { get; set; }
        public bool IsStandard { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public string Description { get; set; }
    }

    /// <summary>카테고리 정보</summary>
    public class CategoryInfo
    {
        public int CategoryId { get; set; }
        public string CategoryCode { get; set; }
        public string CategoryName { get; set; }
        public string CategoryNameKr { get; set; }
        public string ParentCode { get; set; }
        public int CategoryLevel { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>부품 사양 정보</summary>
    public class PartSpecInfo
    {
        public int PartId { get; set; }
        public string PartCode { get; set; }
        public string PartName { get; set; }
        public string PartNameKr { get; set; }
        public string VendorCode { get; set; }
        public string CategoryCode { get; set; }
        public string FilterOptionsJson { get; set; }
        public string DimensionColumnsJson { get; set; }
        public string StandardRef { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public string Description { get; set; }
        public string DbAlias { get; set; }

        // JSON 파싱 헬퍼
        public List<FilterOption> GetFilterOptions()
        {
            if (string.IsNullOrEmpty(FilterOptionsJson)) return new List<FilterOption>();
            try { return JsonConvert.DeserializeObject<List<FilterOption>>(FilterOptionsJson); }
            catch { return new List<FilterOption>(); }
        }

        public List<DimensionColumn> GetDimensionColumns()
        {
            if (string.IsNullOrEmpty(DimensionColumnsJson)) return new List<DimensionColumn>();
            try { return JsonConvert.DeserializeObject<List<DimensionColumn>>(DimensionColumnsJson); }
            catch { return new List<DimensionColumn>(); }
        }
    }

    /// <summary>필터 옵션 정의</summary>
    public class FilterOption
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("name_kr")]
        public string NameKr { get; set; }

        [JsonProperty("options")]
        public List<string> Options { get; set; }
    }

    /// <summary>치수 컬럼 정의</summary>
    public class DimensionColumn
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("name_kr")]
        public string NameKr { get; set; }

        [JsonProperty("unit")]
        public string Unit { get; set; }
    }

    /// <summary>부품 치수 정보</summary>
    public class PartDimensionInfo
    {
        public int DimId { get; set; }
        public string PartCode { get; set; }
        public string SpecFilterKey { get; set; }
        public string DimKey1 { get; set; }
        public string DimKey2 { get; set; }
        public string DimKey3 { get; set; }
        public string DimensionDataJson { get; set; }
        public string PartNumber { get; set; }
        public double? Weight { get; set; }
        public string Material { get; set; }
        public string SurfaceFinish { get; set; }
        public string Remarks { get; set; }

        // JSON 파싱 헬퍼
        public Dictionary<string, object> GetDimensionData()
        {
            if (string.IsNullOrEmpty(DimensionDataJson)) return new Dictionary<string, object>();
            try { return JsonConvert.DeserializeObject<Dictionary<string, object>>(DimensionDataJson); }
            catch { return new Dictionary<string, object>(); }
        }

        public T GetDimensionValue<T>(string key, T defaultValue = default)
        {
            var data = GetDimensionData();
            if (data.TryGetValue(key, out object value))
            {
                try { return (T)Convert.ChangeType(value, typeof(T)); }
                catch { return defaultValue; }
            }
            return defaultValue;
        }
    }

    /// <summary>UI 메타데이터 정보</summary>
    public class UIMetaInfo
    {
        public int MetaId { get; set; }
        public string PartCode { get; set; }
        public string ColumnKey { get; set; }
        public string ColumnName { get; set; }
        public string ColumnNameKr { get; set; }
        public string ControlType { get; set; }
        public int DisplayOrder { get; set; }
        public int ColumnWidth { get; set; }
        public bool IsRequired { get; set; }
        public bool IsVisible { get; set; }
        public string DefaultValue { get; set; }
        public string Tooltip { get; set; }
    }

    /// <summary>앱 설정 정보</summary>
    public class AppConfig
    {
        public string DatabasePath { get; set; }
        public string CoreDatabase { get; set; } = "standard_core.db";
        public string VendorDatabasePath { get; set; }
        public List<string> EnabledVendors { get; set; } = new List<string> { "STANDARD" };
        public Dictionary<string, string> VendorFiles { get; set; } = new Dictionary<string, string>();
        public string DefaultLanguage { get; set; } = "ko";
        public bool AutoUpdate { get; set; } = true;
    }

    #endregion
}
