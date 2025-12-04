using System;
using System.Configuration;
using System.Threading.Tasks;
using Npgsql;

namespace BoltSpecProgram.Services
{
    /// <summary>
    /// PostgreSQL 데이터베이스 서비스 클래스
    /// PartSpec 테이블에 JSON 데이터 저장
    /// </summary>
    public class DatabaseService
    {
        private readonly string _connectionString;

        /// <summary>
        /// 생성자 - App.config에서 연결 문자열 로드
        /// </summary>
        public DatabaseService()
        {
          //  _connectionString = ConfigurationManager.ConnectionStrings["PostgresConnection"]?.ConnectionString
           //     ?? "Host=localhost;Port=5432;Database=Standard_Spec;Username=postgres;Password=postgres";
            _connectionString = "Host=192.168.0.17;Port=5432;Database=Standard_Core;Username=clickinfo;Password=info6130!!";
        }

        /// <summary>
        /// 생성자 - 연결 문자열 직접 지정
        /// </summary>
        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// 연결 테스트
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync().ConfigureAwait(false);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] 연결 테스트 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// PartSpec 테이블이 없으면 생성 (part_type 컬럼 포함)
        /// </summary>
        public async Task EnsureTableExistsAsync()
        {
            // 새 테이블 구조 (part_type 포함)
            const string createTableSql = @"
                CREATE TABLE IF NOT EXISTS PartSpec (
                    id          INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    part_type   TEXT NOT NULL,
                    part_code   TEXT NOT NULL UNIQUE,
                    part_name   TEXT NOT NULL,
                    spec_data   TEXT,
                    is_active   BOOLEAN DEFAULT TRUE
                );
            ";

            // 기존 테이블에 part_type 컬럼이 없으면 추가
            const string addColumnSql = @"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'partspec' AND column_name = 'part_type'
                    ) THEN
                        ALTER TABLE PartSpec ADD COLUMN part_type TEXT NOT NULL DEFAULT '';
                    END IF;
                END $$;
            ";

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                
                // 테이블 생성
                using (var cmd = new NpgsqlCommand(createTableSql, conn))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
                
                // 기존 테이블에 컬럼 추가 (있으면 무시)
                using (var cmd = new NpgsqlCommand(addColumnSql, conn))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            Console.WriteLine("[DB] PartSpec 테이블 확인/생성 완료 (part_type 포함)");
        }

        /// <summary>
        /// PartSpec 데이터 저장 (UPSERT: INSERT OR UPDATE)
        /// </summary>
        /// <param name="partType">파트 타입/분류 (예: 볼트류, 너트류, 와셔류)</param>
        /// <param name="partCode">파트 코드 (예: HBOLT)</param>
        /// <param name="partName">파트 이름 (예: 육각머리볼트)</param>
        /// <param name="specData">JSON 데이터</param>
        /// <returns>저장 성공 여부</returns>
        public async Task<bool> SavePartSpecAsync(string partType, string partCode, string partName, string specData)
        {
            if (string.IsNullOrWhiteSpace(partCode))
            {
                Console.WriteLine("[DB] 저장 실패: part_code가 비어 있습니다.");
                return false;
            }

            // PostgreSQL UPSERT (INSERT ... ON CONFLICT UPDATE)
            const string upsertSql = @"
                INSERT INTO PartSpec (part_type, part_code, part_name, spec_data, is_active)
                VALUES (@part_type, @part_code, @part_name, @spec_data, TRUE)
                ON CONFLICT (part_code)
                DO UPDATE SET
                    part_type = EXCLUDED.part_type,
                    part_name = EXCLUDED.part_name,
                    spec_data = EXCLUDED.spec_data,
                    is_active = TRUE;
            ";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // 테이블 존재 확인
                    await EnsureTableExistsAsync();

                    using (var cmd = new NpgsqlCommand(upsertSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@part_type", partType ?? "");
                        cmd.Parameters.AddWithValue("@part_code", partCode);
                        cmd.Parameters.AddWithValue("@part_name", partName);
                        cmd.Parameters.AddWithValue("@spec_data", (object)specData ?? DBNull.Value);

                        int affected = await cmd.ExecuteNonQueryAsync();
                        Console.WriteLine($"[DB] PartSpec 저장 완료: [{partType}] {partCode} ({partName}), 영향받은 행: {affected}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] 저장 실패: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// PartSpec 데이터 저장 (동기 버전)
        /// </summary>
        public bool SavePartSpec(string partType, string partCode, string partName, string specData)
        {
            return SavePartSpecAsync(partType, partCode, partName, specData).GetAwaiter().GetResult();
        }

        /// <summary>
        /// PartSpec 데이터 조회
        /// </summary>
        public async Task<PartSpecData> GetPartSpecAsync(string partCode)
        {
            const string selectSql = @"
                SELECT part_type, part_name, spec_data
                FROM PartSpec
                WHERE part_code = @part_code AND is_active = TRUE;
            ";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (var cmd = new NpgsqlCommand(selectSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@part_code", partCode);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new PartSpecData
                                {
                                    PartType = reader.GetString(0),
                                    PartName = reader.GetString(1),
                                    SpecData = reader.IsDBNull(2) ? null : reader.GetString(2)
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] 조회 실패: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 모든 활성 PartSpec 목록 조회
        /// </summary>
        public async Task<System.Collections.Generic.List<PartSpecInfo>> GetAllPartSpecsAsync()
        {
            var result = new System.Collections.Generic.List<PartSpecInfo>();

            const string selectSql = @"
                SELECT part_type, part_code, part_name
                FROM PartSpec
                WHERE is_active = TRUE
                ORDER BY part_type, part_name;
            ";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (var cmd = new NpgsqlCommand(selectSql, conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.Add(new PartSpecInfo
                                {
                                    PartType = reader.GetString(0),
                                    PartCode = reader.GetString(1),
                                    PartName = reader.GetString(2)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] 목록 조회 실패: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// PartSpec 비활성화 (소프트 삭제)
        /// </summary>
        public async Task<bool> DeactivatePartSpecAsync(string partCode)
        {
            const string updateSql = @"
                UPDATE PartSpec
                SET is_active = FALSE
                WHERE part_code = @part_code;
            ";

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (var cmd = new NpgsqlCommand(updateSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@part_code", partCode);
                        int affected = await cmd.ExecuteNonQueryAsync();
                        Console.WriteLine($"[DB] PartSpec 비활성화: {partCode}, 영향받은 행: {affected}");
                        return affected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] 비활성화 실패: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// PartSpec 정보 (목록용)
    /// </summary>
    public class PartSpecInfo
    {
        public string PartType { get; set; }
        public string PartCode { get; set; }
        public string PartName { get; set; }
    }

    /// <summary>
    /// PartSpec 데이터 (상세 조회용)
    /// </summary>
    public class PartSpecData
    {
        public string PartType { get; set; }
        public string PartName { get; set; }
        public string SpecData { get; set; }
    }
}
