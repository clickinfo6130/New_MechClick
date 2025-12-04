using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DimensionManager.Models;
using Npgsql;

namespace DimensionManager.Services
{
    public class PostgresService
    {
        private string _connectionString = "";

        public string ConnectionString
        {
            get { return _connectionString; }
            set { _connectionString = value; }
        }

        public void SetConnection(string host, int port, string database, string username, string password)
        {
            _connectionString = string.Format("Host={0};Port={1};Database={2};Username={3};Password={4}",
                host, port, database, username, password);
        }

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
            catch
            {
                return false;
            }
        }

        public async Task<string> GetServerVersionAsync()
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                return conn.ServerVersion;
            }
        }

        private object ToDbValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return DBNull.Value;
            return value;
        }

        private string Safe(string value)
        {
            return value ?? "";
        }

        #region Create Tables

        public async Task CreateTablesIfNotExistsAsync()
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);

                var sql = @"
                    -- DimensionMeta 테이블 (치수 필드 정의)
                    CREATE TABLE IF NOT EXISTS DimensionMeta (
                        id              INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                        part_code       TEXT NOT NULL,
                        field_name      TEXT NOT NULL,
                        display_name    TEXT NOT NULL,
                        display_name_en TEXT,
                        data_type       TEXT DEFAULT 'DECIMAL',
                        decimal_places  INTEGER DEFAULT 2,
                        unit            TEXT,
                        display_order   INTEGER NOT NULL,
                        is_key_field    BOOLEAN DEFAULT FALSE,
                        is_active       BOOLEAN DEFAULT TRUE,
                        created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        updated_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP                      
                    );

                    -- DimensionKeyOption 테이블 (키값별 옵션 목록)
                    CREATE TABLE IF NOT EXISTS DimensionKeyOption (
                        id              INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                        part_code       TEXT NOT NULL,
                        key_field_name  TEXT NOT NULL,
                        key_level       INTEGER NOT NULL,
                        key_value       TEXT NOT NULL,
                        parent_key      TEXT UNIQUE,
                        sort_order      INTEGER DEFAULT 0,
                        is_active       BOOLEAN DEFAULT TRUE,
                        created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP                       
                    );

                    -- PartDimension 테이블 (실제 치수 데이터)
                    CREATE TABLE IF NOT EXISTS PartDimension (
                        id              INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                        part_code       TEXT NOT NULL,
                        key_composite   TEXT NOT NULL UNIQUE,
                        key_values      JSONB NOT NULL,
                        dimension_data  JSONB NOT NULL,
                        is_active       BOOLEAN DEFAULT TRUE,
                        created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        updated_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP                        
                    );

                    -- 인덱스 생성
                    CREATE INDEX IF NOT EXISTS idx_dim_meta_part ON DimensionMeta(part_code);
                    CREATE INDEX IF NOT EXISTS idx_dim_key_part ON DimensionKeyOption(part_code);
                    CREATE INDEX IF NOT EXISTS idx_dim_key_field ON DimensionKeyOption(part_code, key_field_name);
                    CREATE INDEX IF NOT EXISTS idx_part_dim_part ON PartDimension(part_code);
                    CREATE INDEX IF NOT EXISTS idx_part_dim_composite ON PartDimension(part_code, key_composite);
                    CREATE INDEX IF NOT EXISTS idx_part_dim_keys ON PartDimension USING GIN (key_values);
                ";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region Save Dimension Data

        public async Task SaveDimensionDataAsync(DimensionImportResult importResult, bool deleteExisting = true)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);

                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string partCode = importResult.PartCode;

                        if (deleteExisting && !string.IsNullOrEmpty(partCode))
                        {
                            // 기존 데이터 삭제
                            await DeletePartDataAsync(conn, transaction, partCode).ConfigureAwait(false);
                        }

                        // DimensionMeta 저장
                        await InsertDimensionMetasAsync(conn, transaction, importResult.DimensionMetas).ConfigureAwait(false);

                        // DimensionKeyOption 저장
                        await InsertKeyOptionsAsync(conn, transaction, importResult.KeyOptions).ConfigureAwait(false);

                        // PartDimension 저장
                        await InsertPartDimensionsAsync(conn, transaction, importResult.Dimensions).ConfigureAwait(false);

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("DB 저장 오류: " + ex.Message, ex);
                    }
                }
            }
        }

        private async Task DeletePartDataAsync(NpgsqlConnection conn, NpgsqlTransaction transaction, string partCode)
        {
            // PartDimension 삭제
            using (var cmd = new NpgsqlCommand("DELETE FROM PartDimension WHERE part_code = @partCode", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@partCode", partCode);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            // DimensionKeyOption 삭제
            using (var cmd = new NpgsqlCommand("DELETE FROM DimensionKeyOption WHERE part_code = @partCode", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@partCode", partCode);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            // DimensionMeta 삭제
            using (var cmd = new NpgsqlCommand("DELETE FROM DimensionMeta WHERE part_code = @partCode", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@partCode", partCode);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        private async Task InsertDimensionMetasAsync(NpgsqlConnection conn, NpgsqlTransaction transaction, List<DimensionMeta> items)
        {
            var sql = @"INSERT INTO DimensionMeta 
                (part_code, field_name, display_name, display_name_en, data_type, decimal_places, 
                 unit, display_order, is_key_field, is_active)
                VALUES (@partCode, @fieldName, @displayName, @displayNameEn, @dataType, @decimalPlaces,
                        @unit, @displayOrder, @isKeyField, @isActive)";

            foreach (var item in items)
            {
                using (var cmd = new NpgsqlCommand(sql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@partCode", Safe(item.PartCode));
                    cmd.Parameters.AddWithValue("@fieldName", Safe(item.FieldName));
                    cmd.Parameters.AddWithValue("@displayName", Safe(item.DisplayName));
                    cmd.Parameters.AddWithValue("@displayNameEn", ToDbValue(item.DisplayNameEn));
                    cmd.Parameters.AddWithValue("@dataType", Safe(item.DataType));
                    cmd.Parameters.AddWithValue("@decimalPlaces", item.DecimalPlaces);
                    cmd.Parameters.AddWithValue("@unit", ToDbValue(item.Unit));
                    cmd.Parameters.AddWithValue("@displayOrder", item.DisplayOrder);
                    cmd.Parameters.AddWithValue("@isKeyField", item.IsKeyField);
                    cmd.Parameters.AddWithValue("@isActive", item.IsActive);

                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task InsertKeyOptionsAsync(NpgsqlConnection conn, NpgsqlTransaction transaction, List<DimensionKeyOption> items)
        {
            var sql = @"INSERT INTO DimensionKeyOption 
                (part_code, key_field_name, key_level, key_value, parent_key, sort_order, is_active)
                VALUES (@partCode, @keyFieldName, @keyLevel, @keyValue, @parentKey, @sortOrder, @isActive)";

            foreach (var item in items)
            {
                using (var cmd = new NpgsqlCommand(sql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@partCode", Safe(item.PartCode));
                    cmd.Parameters.AddWithValue("@keyFieldName", Safe(item.KeyFieldName));
                    cmd.Parameters.AddWithValue("@keyLevel", item.KeyLevel);
                    cmd.Parameters.AddWithValue("@keyValue", Safe(item.KeyValue));
                    cmd.Parameters.AddWithValue("@parentKey", ToDbValue(item.ParentKey));
                    cmd.Parameters.AddWithValue("@sortOrder", item.SortOrder);
                    cmd.Parameters.AddWithValue("@isActive", item.IsActive);

                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task InsertPartDimensionsAsync(NpgsqlConnection conn, NpgsqlTransaction transaction, List<PartDimension> items)
        {
            var sql = @"INSERT INTO PartDimension 
                (part_code, key_composite, key_values, dimension_data, is_active)
                VALUES (@partCode, @keyComposite, @keyValues::jsonb, @dimensionData::jsonb, @isActive)";

            foreach (var item in items)
            {
                using (var cmd = new NpgsqlCommand(sql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@partCode", Safe(item.PartCode));
                    cmd.Parameters.AddWithValue("@keyComposite", Safe(item.KeyComposite));
                    cmd.Parameters.AddWithValue("@keyValues", Safe(item.KeyValuesJson));
                    cmd.Parameters.AddWithValue("@dimensionData", Safe(item.DimensionDataJson));
                    cmd.Parameters.AddWithValue("@isActive", item.IsActive);

                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// 특정 부품의 키 필드 목록 조회
        /// </summary>
        public async Task<List<DimensionMeta>> GetKeyFieldsAsync(string partCode)
        {
            var result = new List<DimensionMeta>();

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);

                var sql = @"SELECT * FROM DimensionMeta 
                            WHERE part_code = @partCode AND is_key_field = TRUE 
                            ORDER BY display_order";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@partCode", partCode);

                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            result.Add(ReadDimensionMeta(reader));
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 특정 키 필드의 옵션 목록 조회
        /// </summary>
        public async Task<List<string>> GetKeyOptionsAsync(string partCode, string keyFieldName, string parentKey = null)
        {
            var result = new List<string>();

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);

                string sql;
                if (string.IsNullOrEmpty(parentKey))
                {
                    sql = @"SELECT DISTINCT key_value FROM DimensionKeyOption 
                            WHERE part_code = @partCode AND key_field_name = @keyFieldName
                            ORDER BY sort_order, key_value";
                }
                else
                {
                    sql = @"SELECT DISTINCT key_value FROM DimensionKeyOption 
                            WHERE part_code = @partCode AND key_field_name = @keyFieldName
                              AND (parent_key = @parentKey OR parent_key IS NULL)
                            ORDER BY sort_order, key_value";
                }

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@partCode", partCode);
                    cmd.Parameters.AddWithValue("@keyFieldName", keyFieldName);
                    if (!string.IsNullOrEmpty(parentKey))
                    {
                        cmd.Parameters.AddWithValue("@parentKey", parentKey);
                    }

                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            result.Add(reader.GetString(0));
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 치수 데이터 조회
        /// </summary>
        public async Task<string> GetDimensionDataAsync(string partCode, string keyComposite)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);

                var sql = @"SELECT dimension_data FROM PartDimension 
                            WHERE part_code = @partCode AND key_composite = @keyComposite";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@partCode", partCode);
                    cmd.Parameters.AddWithValue("@keyComposite", keyComposite);

                    var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                    return result?.ToString();
                }
            }
        }

        private DimensionMeta ReadDimensionMeta(NpgsqlDataReader reader)
        {
            return new DimensionMeta
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                PartCode = reader.GetString(reader.GetOrdinal("part_code")),
                FieldName = reader.GetString(reader.GetOrdinal("field_name")),
                DisplayName = reader.GetString(reader.GetOrdinal("display_name")),
                DisplayNameEn = reader.IsDBNull(reader.GetOrdinal("display_name_en")) ? null : reader.GetString(reader.GetOrdinal("display_name_en")),
                DataType = reader.GetString(reader.GetOrdinal("data_type")),
                DecimalPlaces = reader.GetInt32(reader.GetOrdinal("decimal_places")),
                Unit = reader.IsDBNull(reader.GetOrdinal("unit")) ? null : reader.GetString(reader.GetOrdinal("unit")),
                DisplayOrder = reader.GetInt32(reader.GetOrdinal("display_order")),
                IsKeyField = reader.GetBoolean(reader.GetOrdinal("is_key_field")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active"))
            };
        }

        #endregion
    }
}
