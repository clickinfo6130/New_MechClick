// ============================================================================
// PartManager - DatabaseManager
// ============================================================================
// 파일명: DatabaseManager.cs
// 설명: 데이터베이스 초기화, 설정 로드, 업체 DB 동적 연결 관리
// 버전: 1.0.0
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PartManager.Data
{
    /// <summary>
    /// 데이터베이스 매니저
    /// - 설정 파일 기반 초기화
    /// - 업체 DB 동적 연결 관리
    /// - 싱글톤 패턴 구현
    /// </summary>
    public class DatabaseManager : IDisposable
    {
        #region Singleton

        private static readonly Lazy<DatabaseManager> _instance = 
            new Lazy<DatabaseManager>(() => new DatabaseManager());

        public static DatabaseManager Instance => _instance.Value;

        private DatabaseManager() { }

        #endregion

        #region Fields & Properties

        private MultiDatabaseRepository _repository;
        private AppConfig _config;
        private bool _initialized = false;
        private bool _disposed = false;

        /// <summary>Repository 인스턴스</summary>
        public MultiDatabaseRepository Repository => _repository;

        /// <summary>현재 설정</summary>
        public AppConfig Config => _config;

        /// <summary>초기화 완료 여부</summary>
        public bool IsInitialized => _initialized;

        /// <summary>설정 파일 경로</summary>
        public string ConfigFilePath { get; private set; }

        #endregion

        #region Events

        /// <summary>초기화 완료 이벤트</summary>
        public event EventHandler<InitializedEventArgs> Initialized;

        /// <summary>업체 연결 변경 이벤트</summary>
        public event EventHandler<VendorChangedEventArgs> VendorChanged;

        /// <summary>오류 발생 이벤트</summary>
        public event EventHandler<ErrorEventArgs> ErrorOccurred;

        #endregion

        #region Initialization

        /// <summary>
        /// 데이터베이스 초기화
        /// </summary>
        /// <param name="configFilePath">설정 파일 경로 (config.json)</param>
        public async Task InitializeAsync(string configFilePath = null)
        {
            if (_initialized)
            {
                Console.WriteLine("[Info] Database already initialized.");
                return;
            }

            try
            {
                // 1. 설정 파일 경로 결정
                ConfigFilePath = configFilePath ?? GetDefaultConfigPath();

                // 2. 설정 로드
                _config = LoadConfig(ConfigFilePath);
                Console.WriteLine($"[Success] Config loaded from: {ConfigFilePath}");

                // 3. Core DB 연결
                string coreDbPath = Path.Combine(_config.DatabasePath, _config.CoreDatabase);
                
                if (!File.Exists(coreDbPath))
                {
                    throw new FileNotFoundException($"Core database not found: {coreDbPath}");
                }

                _repository = new MultiDatabaseRepository(coreDbPath);
                Console.WriteLine($"[Success] Connected to Core DB: {coreDbPath}");

                // 4. 활성화된 업체 DB 연결
                await Task.Run(() => AttachEnabledVendors());

                _initialized = true;

                // 5. 이벤트 발생
                Initialized?.Invoke(this, new InitializedEventArgs
                {
                    Success = true,
                    CoreDbPath = coreDbPath,
                    AttachedVendors = _repository.AttachedVendors.ToList()
                });

                Console.WriteLine("[Success] Database initialization completed.");
            }
            catch (Exception ex)
            {
                _initialized = false;
                Console.WriteLine($"[Error] Database initialization failed: {ex.Message}");

                Initialized?.Invoke(this, new InitializedEventArgs
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });

                ErrorOccurred?.Invoke(this, new ErrorEventArgs { Exception = ex });
                throw;
            }
        }

        /// <summary>
        /// 동기 초기화 (UI 스레드에서 호출 시)
        /// </summary>
        public void Initialize(string configFilePath = null)
        {
            if (_initialized)
            {
                Console.WriteLine("[Info] Database already initialized.");
                return;
            }

            try
            {
                // 1. 설정 파일 경로 결정
                ConfigFilePath = configFilePath ?? GetDefaultConfigPath();

                // 2. 설정 로드
                _config = LoadConfig(ConfigFilePath);
                Console.WriteLine($"[Success] Config loaded from: {ConfigFilePath}");

                // 3. Core DB 연결
                string coreDbPath = Path.Combine(_config.DatabasePath, _config.CoreDatabase);
                
                if (!File.Exists(coreDbPath))
                {
                    throw new FileNotFoundException($"Core database not found: {coreDbPath}");
                }

                _repository = new MultiDatabaseRepository(coreDbPath);
                Console.WriteLine($"[Success] Connected to Core DB: {coreDbPath}");

                // 4. 활성화된 업체 DB 연결 (동기 방식)
                AttachEnabledVendors();

                _initialized = true;

                // 5. 이벤트 발생
                Initialized?.Invoke(this, new InitializedEventArgs
                {
                    Success = true,
                    CoreDbPath = coreDbPath,
                    AttachedVendors = _repository.AttachedVendors.ToList()
                });

                Console.WriteLine("[Success] Database initialization completed.");
            }
            catch (Exception ex)
            {
                _initialized = false;
                Console.WriteLine($"[Error] Database initialization failed: {ex.Message}");

                Initialized?.Invoke(this, new InitializedEventArgs
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });

                throw;
            }
        }

        /// <summary>
        /// 기본 설정 파일 경로 반환
        /// </summary>
        private string GetDefaultConfigPath()
        {
            // 실행 파일 위치 기준
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string configPath = Path.Combine(exeDir, "config.json");

            if (File.Exists(configPath))
                return configPath;

            // 상위 폴더 확인
            string parentDir = Directory.GetParent(exeDir)?.FullName;
            if (parentDir != null)
            {
                configPath = Path.Combine(parentDir, "config.json");
                if (File.Exists(configPath))
                    return configPath;
            }

            // 기본 경로 반환 (생성 필요)
            return Path.Combine(exeDir, "config.json");
        }

        #endregion

        #region Config Management

        /// <summary>
        /// 설정 파일 로드
        /// </summary>
        private AppConfig LoadConfig(string configFilePath)
        {
            if (File.Exists(configFilePath))
            {
                string json = File.ReadAllText(configFilePath);
                var config = JsonConvert.DeserializeObject<AppConfig>(json);
                
                // 경로가 상대 경로인 경우 절대 경로로 변환
                string baseDir = Path.GetDirectoryName(configFilePath);
                
                if (!Path.IsPathRooted(config.DatabasePath))
                    config.DatabasePath = Path.GetFullPath(Path.Combine(baseDir, config.DatabasePath));
                
                if (!string.IsNullOrEmpty(config.VendorDatabasePath) && !Path.IsPathRooted(config.VendorDatabasePath))
                    config.VendorDatabasePath = Path.GetFullPath(Path.Combine(baseDir, config.VendorDatabasePath));
                else if (string.IsNullOrEmpty(config.VendorDatabasePath))
                    config.VendorDatabasePath = Path.Combine(config.DatabasePath, "vendors");

                return config;
            }
            else
            {
                // 기본 설정 생성
                var config = CreateDefaultConfig(configFilePath);
                SaveConfig(config, configFilePath);
                return config;
            }
        }

        /// <summary>
        /// 기본 설정 생성
        /// </summary>
        private AppConfig CreateDefaultConfig(string configFilePath)
        {
            string baseDir = Path.GetDirectoryName(configFilePath);

            return new AppConfig
            {
                DatabasePath = Path.Combine(baseDir, "Database"),
                CoreDatabase = "standard_core.db",
                VendorDatabasePath = Path.Combine(baseDir, "Database", "vendors"),
                EnabledVendors = new List<string> { "STANDARD" },
                VendorFiles = new Dictionary<string, string>
                {
                    { "SMC", "vendor_smc.db" },
                    { "CKD", "vendor_ckd.db" },
                    { "FESTO", "vendor_festo.db" },
                    { "THK", "vendor_thk.db" },
                    { "HIWIN", "vendor_hiwin.db" },
                    { "NSK", "vendor_nsk.db" },
                    { "ORIENTAL", "vendor_oriental.db" },
                    { "MITSUBISHI", "vendor_mitsubishi.db" }
                },
                DefaultLanguage = "ko",
                AutoUpdate = true
            };
        }

        /// <summary>
        /// 설정 파일 저장
        /// </summary>
        public void SaveConfig(AppConfig config = null, string configFilePath = null)
        {
            config = config ?? _config;
            configFilePath = configFilePath ?? ConfigFilePath;

            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configFilePath, json);

            Console.WriteLine($"[Success] Config saved to: {configFilePath}");
        }

        /// <summary>
        /// 설정 다시 로드
        /// </summary>
        public void ReloadConfig()
        {
            if (!string.IsNullOrEmpty(ConfigFilePath))
            {
                _config = LoadConfig(ConfigFilePath);
                Console.WriteLine("[Success] Config reloaded.");
            }
        }

        #endregion

        #region Vendor Management

        /// <summary>
        /// 활성화된 업체 DB 연결
        /// </summary>
        private void AttachEnabledVendors()
        {
            if (_config?.EnabledVendors == null || _repository == null)
                return;

            foreach (var vendorCode in _config.EnabledVendors)
            {
                if (vendorCode == "STANDARD")
                    continue; // Core DB에 포함

                AttachVendor(vendorCode);
            }
        }

        /// <summary>
        /// 업체 DB 연결
        /// </summary>
        public bool AttachVendor(string vendorCode)
        {
            if (_repository == null)
            {
                Console.WriteLine("[Error] Repository not initialized.");
                return false;
            }

            // DB 파일명 결정
            string dbFileName = null;
            if (_config.VendorFiles != null)
            {
                _config.VendorFiles.TryGetValue(vendorCode, out dbFileName);
            }
            if (string.IsNullOrEmpty(dbFileName))
            {
                dbFileName = $"vendor_{vendorCode.ToLower()}.db";
            }

            string dbPath = Path.Combine(_config.VendorDatabasePath, dbFileName);

            if (!File.Exists(dbPath))
            {
                Console.WriteLine($"[Warning] Vendor database not found: {dbPath}");
                return false;
            }

            bool success = _repository.AttachVendorDatabase(vendorCode, dbPath);

            if (success)
            {
                // 설정 업데이트
                if (!_config.EnabledVendors.Contains(vendorCode))
                {
                    _config.EnabledVendors.Add(vendorCode);
                }

                VendorChanged?.Invoke(this, new VendorChangedEventArgs
                {
                    VendorCode = vendorCode,
                    Action = VendorAction.Attached
                });
            }

            return success;
        }

        /// <summary>
        /// 업체 DB 연결 해제
        /// </summary>
        public bool DetachVendor(string vendorCode)
        {
            if (_repository == null)
            {
                Console.WriteLine("[Error] Repository not initialized.");
                return false;
            }

            bool success = _repository.DetachVendorDatabase(vendorCode);

            if (success)
            {
                // 설정 업데이트
                _config.EnabledVendors.Remove(vendorCode);

                VendorChanged?.Invoke(this, new VendorChangedEventArgs
                {
                    VendorCode = vendorCode,
                    Action = VendorAction.Detached
                });
            }

            return success;
        }

        /// <summary>
        /// 업체 활성화 상태 변경
        /// </summary>
        public bool SetVendorEnabled(string vendorCode, bool enabled)
        {
            if (enabled)
                return AttachVendor(vendorCode);
            else
                return DetachVendor(vendorCode);
        }

        /// <summary>
        /// 사용 가능한 업체 목록 조회 (DB 파일 존재 여부 포함)
        /// </summary>
        public List<VendorAvailability> GetAvailableVendors()
        {
            var result = new List<VendorAvailability>();

            if (_repository == null)
                return result;

            var allVendors = _repository.GetAllVendors();

            foreach (var vendor in allVendors)
            {
                var availability = new VendorAvailability
                {
                    VendorCode = vendor.VendorCode,
                    VendorName = vendor.VendorName,
                    VendorNameKr = vendor.VendorNameKr,
                    IsStandard = vendor.IsStandard,
                    IsEnabled = vendor.IsStandard || _repository.AttachedVendors.Contains(vendor.VendorCode)
                };

                if (!vendor.IsStandard && !string.IsNullOrEmpty(vendor.DbFileName))
                {
                    string dbPath = Path.Combine(_config.VendorDatabasePath, vendor.DbFileName);
                    availability.DbFileExists = File.Exists(dbPath);
                    availability.DbFilePath = dbPath;
                }
                else
                {
                    availability.DbFileExists = true; // STANDARD는 Core DB에 포함
                }

                result.Add(availability);
            }

            return result;
        }

        #endregion

        #region Data Access Shortcuts

        /// <summary>모든 업체 조회</summary>
        public List<VendorInfo> GetVendors() => _repository?.GetAllVendors() ?? new List<VendorInfo>();

        /// <summary>활성화된 업체 조회</summary>
        public List<VendorInfo> GetEnabledVendors() => _repository?.GetEnabledVendors() ?? new List<VendorInfo>();

        /// <summary>카테고리 계층 조회</summary>
        public List<CategoryInfo> GetCategories() => _repository?.GetCategoryHierarchy() ?? new List<CategoryInfo>();

        /// <summary>부품 사양 조회</summary>
        public List<PartSpecInfo> GetPartSpecs(string vendorCode = null, string categoryCode = null) 
            => _repository?.GetPartSpecs(vendorCode, categoryCode) ?? new List<PartSpecInfo>();

        /// <summary>부품 코드로 사양 조회</summary>
        public PartSpecInfo GetPartSpec(string partCode) => _repository?.GetPartSpecByCode(partCode);

        /// <summary>부품 치수 조회</summary>
        public List<PartDimensionInfo> GetDimensions(string partCode, string filterKey = null)
            => _repository?.GetPartDimensions(partCode, filterKey) ?? new List<PartDimensionInfo>();

        /// <summary>특정 치수 조회</summary>
        public PartDimensionInfo GetDimension(string partCode, string filterKey, string dimKey1, string dimKey2 = null)
            => _repository?.GetPartDimension(partCode, filterKey, dimKey1, dimKey2);

        /// <summary>UI 메타데이터 조회</summary>
        public List<UIMetaInfo> GetUIMeta(string partCode) 
            => _repository?.GetUIMeta(partCode) ?? new List<UIMetaInfo>();

        #endregion

        #region IDisposable

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
                    _repository?.Dispose();
                    _repository = null;
                }

                _initialized = false;
                _disposed = true;
            }
        }

        #endregion
    }

    #region Event Args

    public class InitializedEventArgs : EventArgs
    {
        public bool Success { get; set; }
        public string CoreDbPath { get; set; }
        public List<string> AttachedVendors { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class VendorChangedEventArgs : EventArgs
    {
        public string VendorCode { get; set; }
        public VendorAction Action { get; set; }
    }

    public enum VendorAction
    {
        Attached,
        Detached
    }

    public class ErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
    }

    public class VendorAvailability
    {
        public string VendorCode { get; set; }
        public string VendorName { get; set; }
        public string VendorNameKr { get; set; }
        public bool IsStandard { get; set; }
        public bool IsEnabled { get; set; }
        public bool DbFileExists { get; set; }
        public string DbFilePath { get; set; }
    }

    #endregion
}
