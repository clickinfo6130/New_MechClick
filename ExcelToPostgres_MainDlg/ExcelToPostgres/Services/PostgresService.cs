using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExcelToPostgres.Models;
using Npgsql;

namespace ExcelToPostgres.Services
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

        // ★ NULL 허용 필드용: null이나 빈 문자열이면 DBNull.Value 반환
        private object ToDbValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return DBNull.Value;
            return value;
        }

        // ★ NOT NULL 필드용: null이면 빈 문자열 반환
        private string Safe(string value)
        {
            return value ?? "";
        }

        #region Save All

        public async Task SaveAllAsync(
            List<MainCategory> mainCategories,
            List<SubCategory> subCategories,
            List<MidCategory> midCategories,
            List<PartType> partTypes,
            List<PartSeries> partSeriesList)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);

                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 하위 테이블부터 삭제 (FK 제약 조건 때문)
                        using (var cmd = new NpgsqlCommand("DELETE FROM PartSeries", conn, transaction))
                        {
                            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                        using (var cmd = new NpgsqlCommand("DELETE FROM PartType", conn, transaction))
                        {
                            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                        using (var cmd = new NpgsqlCommand("DELETE FROM MidCategory", conn, transaction))
                        {
                            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                        using (var cmd = new NpgsqlCommand("DELETE FROM SubCategory", conn, transaction))
                        {
                            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                        using (var cmd = new NpgsqlCommand("DELETE FROM MainCategory", conn, transaction))
                        {
                            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }

                        // 상위 테이블부터 삽입
                        await InsertMainCategoriesAsync(conn, transaction, mainCategories).ConfigureAwait(false);
                        await InsertSubCategoriesAsync(conn, transaction, subCategories).ConfigureAwait(false);
                        await InsertMidCategoriesAsync(conn, transaction, midCategories).ConfigureAwait(false);
                        await InsertPartTypesAsync(conn, transaction, partTypes).ConfigureAwait(false);
                        await InsertPartSeriesAsync(conn, transaction, partSeriesList).ConfigureAwait(false);

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

        private async Task InsertMainCategoriesAsync(NpgsqlConnection conn, NpgsqlTransaction transaction, List<MainCategory> items)
        {
            var sql = @"INSERT INTO MainCategory 
                (main_cat_code, main_cat_name, main_cat_name_kr, is_standard, color_code, sort_order, is_active, description)
                VALUES (@code, @name, @nameKr, @isStandard, @colorCode, @sortOrder, @isActive, @description)";

            foreach (var item in items)
            {
                string code = Safe(item.MainCatCode);
                if (string.IsNullOrEmpty(code)) continue;

                using (var cmd = new NpgsqlCommand(sql, conn, transaction))
                {
                    // NOT NULL 필드
                    cmd.Parameters.AddWithValue("@code", code);
                    cmd.Parameters.AddWithValue("@name", Safe(item.MainCatName));
                    
                    // NULL 허용 필드
                    cmd.Parameters.AddWithValue("@nameKr", ToDbValue(item.MainCatNameKr));
                    cmd.Parameters.AddWithValue("@isStandard", item.IsStandard);
                    cmd.Parameters.AddWithValue("@colorCode", ToDbValue(item.ColorCode));
                    cmd.Parameters.AddWithValue("@sortOrder", item.SortOrder);
                    cmd.Parameters.AddWithValue("@isActive", item.IsActive);
                    cmd.Parameters.AddWithValue("@description", ToDbValue(item.Description));
                    
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task InsertSubCategoriesAsync(NpgsqlConnection conn, NpgsqlTransaction transaction, List<SubCategory> items)
        {
            var sql = @"INSERT INTO SubCategory 
                (sub_cat_code, sub_cat_name, sub_cat_name_kr, main_cat_code, is_vendor, vendor_code, country, sort_order, is_active, description)
                VALUES (@code, @name, @nameKr, @mainCatCode, @isVendor, @vendorCode, @country, @sortOrder, @isActive, @description)";

            foreach (var item in items)
            {
                string code = Safe(item.SubCatCode);
                if (string.IsNullOrEmpty(code)) continue;

                using (var cmd = new NpgsqlCommand(sql, conn, transaction))
                {
                    // NOT NULL 필드
                    cmd.Parameters.AddWithValue("@code", code);
                    cmd.Parameters.AddWithValue("@name", Safe(item.SubCatName));
                    cmd.Parameters.AddWithValue("@mainCatCode", Safe(item.MainCatCode));
                    
                    // NULL 허용 필드 - DBNull.Value 사용
                    cmd.Parameters.AddWithValue("@nameKr", ToDbValue(item.SubCatNameKr));
                    cmd.Parameters.AddWithValue("@isVendor", item.IsVendor);
                    cmd.Parameters.AddWithValue("@vendorCode", ToDbValue(item.VendorCode));
                    cmd.Parameters.AddWithValue("@country", ToDbValue(item.Country));
                    cmd.Parameters.AddWithValue("@sortOrder", item.SortOrder);
                    cmd.Parameters.AddWithValue("@isActive", item.IsActive);
                    cmd.Parameters.AddWithValue("@description", ToDbValue(item.Description));
                    
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task InsertMidCategoriesAsync(NpgsqlConnection conn, NpgsqlTransaction transaction, List<MidCategory> items)
        {
            var sql = @"INSERT INTO MidCategory 
                (mid_cat_code, mid_cat_name, mid_cat_name_kr, sub_cat_code, sort_order, is_active, description)
                VALUES (@code, @name, @nameKr, @subCatCode, @sortOrder, @isActive, @description)";

            foreach (var item in items)
            {
                string code = Safe(item.MidCatCode);
                if (string.IsNullOrEmpty(code)) continue;

                using (var cmd = new NpgsqlCommand(sql, conn, transaction))
                {
                    // NOT NULL 필드
                    cmd.Parameters.AddWithValue("@code", code);
                    cmd.Parameters.AddWithValue("@name", Safe(item.MidCatName));
                    cmd.Parameters.AddWithValue("@subCatCode", Safe(item.SubCatCode));
                    
                    // NULL 허용 필드
                    cmd.Parameters.AddWithValue("@nameKr", ToDbValue(item.MidCatNameKr));
                    cmd.Parameters.AddWithValue("@sortOrder", item.SortOrder);
                    cmd.Parameters.AddWithValue("@isActive", item.IsActive);
                    cmd.Parameters.AddWithValue("@description", ToDbValue(item.Description));
                    
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task InsertPartTypesAsync(NpgsqlConnection conn, NpgsqlTransaction transaction, List<PartType> items)
        {
            var sql = @"INSERT INTO PartType 
                (part_type_code, part_type_name, part_type_name_kr, sub_cat_code, mid_cat_code, has_series, sort_order, is_active, description)
                VALUES (@code, @name, @nameKr, @subCatCode, @midCatCode, @hasSeries, @sortOrder, @isActive, @description)";

            foreach (var item in items)
            {
                string code = Safe(item.PartTypeCode);
                if (string.IsNullOrEmpty(code)) continue;

                using (var cmd = new NpgsqlCommand(sql, conn, transaction))
                {
                    // NOT NULL 필드
                    cmd.Parameters.AddWithValue("@code", code);
                    cmd.Parameters.AddWithValue("@name", Safe(item.PartTypeName));
                    
                    // NULL 허용 필드
                    cmd.Parameters.AddWithValue("@nameKr", ToDbValue(item.PartTypeNameKr));
                    cmd.Parameters.AddWithValue("@subCatCode", ToDbValue(item.SubCatCode));
                    cmd.Parameters.AddWithValue("@midCatCode", ToDbValue(item.MidCatCode));
                    cmd.Parameters.AddWithValue("@hasSeries", item.HasSeries ? 1 : 0);
                    cmd.Parameters.AddWithValue("@sortOrder", item.SortOrder);
                    cmd.Parameters.AddWithValue("@isActive", item.IsActive);
                    cmd.Parameters.AddWithValue("@description", ToDbValue(item.Description));
                    
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task InsertPartSeriesAsync(NpgsqlConnection conn, NpgsqlTransaction transaction, List<PartSeries> items)
        {
            var sql = @"INSERT INTO PartSeries 
                (series_code, series_name, series_name_kr, part_type_code, vendor_code, sort_order, is_active, description)
                VALUES (@code, @name, @nameKr, @partTypeCode, @vendorCode, @sortOrder, @isActive, @description)";

            foreach (var item in items)
            {
                string code = Safe(item.SeriesCode);
                if (string.IsNullOrEmpty(code)) continue;

                using (var cmd = new NpgsqlCommand(sql, conn, transaction))
                {
                    // NOT NULL 필드
                    cmd.Parameters.AddWithValue("@code", code);
                    cmd.Parameters.AddWithValue("@name", Safe(item.SeriesName));
                    cmd.Parameters.AddWithValue("@partTypeCode", Safe(item.PartTypeCode));
                    
                    // NULL 허용 필드
                    cmd.Parameters.AddWithValue("@nameKr", ToDbValue(item.SeriesNameKr));
                    cmd.Parameters.AddWithValue("@vendorCode", ToDbValue(item.VendorCode));
                    cmd.Parameters.AddWithValue("@sortOrder", item.SortOrder);
                    cmd.Parameters.AddWithValue("@isActive", item.IsActive);
                    cmd.Parameters.AddWithValue("@description", ToDbValue(item.Description));
                    
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region Create Tables

        public async Task CreateTablesIfNotExistsAsync()
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);

                var sql = @"
                    -- MainCategory 테이블
                    CREATE TABLE IF NOT EXISTS MainCategory (
                        main_cat_id     INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                        main_cat_code   TEXT NOT NULL UNIQUE,
                        main_cat_name   TEXT NOT NULL,
                        main_cat_name_kr TEXT,
                        is_standard     BOOLEAN DEFAULT FALSE,   
                        color_code      TEXT,
                        sort_order      INTEGER DEFAULT 0,
                        is_active       BOOLEAN DEFAULT TRUE,
                        description     TEXT,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );

                    -- SubCategory 테이블
                    CREATE TABLE IF NOT EXISTS SubCategory (
                        sub_cat_id      INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                        sub_cat_code    TEXT NOT NULL UNIQUE,
                        sub_cat_name    TEXT NOT NULL, 
                        sub_cat_name_kr TEXT,
                        main_cat_code   TEXT NOT NULL,
                        is_vendor       BOOLEAN DEFAULT FALSE,
                        vendor_code     TEXT,
                        country         TEXT,
                        color_code      TEXT,
                        sort_order      INTEGER DEFAULT 0,
                        is_active       BOOLEAN DEFAULT TRUE,
                        description     TEXT,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (main_cat_code) REFERENCES MainCategory(main_cat_code)
                    );

                    -- MidCategory 테이블
                    CREATE TABLE IF NOT EXISTS MidCategory (
                        mid_cat_id      INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                        mid_cat_code    TEXT NOT NULL UNIQUE,
                        mid_cat_name    TEXT NOT NULL,
                        mid_cat_name_kr TEXT,
                        sub_cat_code    TEXT NOT NULL,                        
                        sort_order      INTEGER DEFAULT 0,
                        is_active       BOOLEAN DEFAULT TRUE,
                        description     TEXT,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (sub_cat_code) REFERENCES SubCategory(sub_cat_code)
                    );

                    -- PartType 테이블
                    CREATE TABLE IF NOT EXISTS PartType (
                        part_type_id    INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                        part_type_code  TEXT NOT NULL UNIQUE,
                        part_type_name  TEXT NOT NULL,
                        part_type_name_kr TEXT,
                        sub_cat_code    TEXT,
                        mid_cat_code    TEXT,
                        has_series      INTEGER DEFAULT 0,
                        sort_order      INTEGER DEFAULT 0,
                        is_active       BOOLEAN DEFAULT TRUE,
                        description     TEXT,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (sub_cat_code) REFERENCES SubCategory(sub_cat_code),
                        FOREIGN KEY (mid_cat_code) REFERENCES MidCategory(mid_cat_code)
                    );

                    -- PartSeries 테이블
                    CREATE TABLE IF NOT EXISTS PartSeries (
                        series_id       INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                        series_code     TEXT NOT NULL UNIQUE,
                        series_name     TEXT NOT NULL,
                        series_name_kr  TEXT,
                        part_type_code  TEXT NOT NULL,
                        vendor_code     TEXT,
                        db_table_name   TEXT,
                        sort_order      INTEGER DEFAULT 0,
                        is_active       BOOLEAN DEFAULT TRUE,
                        description     TEXT,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (part_type_code) REFERENCES PartType(part_type_code)
                    );

                    -- PartSpec 테이블
                    CREATE TABLE IF NOT EXISTS PartSpec (
                        id              INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                        part_code       TEXT NOT NULL UNIQUE,
                        part_name       TEXT NOT NULL,
                        spec_data       TEXT,
                        is_active       BOOLEAN DEFAULT TRUE
                    );

                    -- DimensionMeta 테이블
                    CREATE TABLE IF NOT EXISTS DimensionMeta (
                        id              INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                        part_code       TEXT NOT NULL,
                        field_name      TEXT NOT NULL,
                        display_name    TEXT NOT NULL,
                        display_name_en TEXT,
                        data_type       TEXT NOT NULL,
                        decimal_places  INTEGER DEFAULT 2,
                        unit            TEXT,
                        display_order   INTEGER NOT NULL,
                        is_key_field    INTEGER DEFAULT 0,
                        is_display_field INTEGER DEFAULT 1,
                        column_width    INTEGER DEFAULT 80,
                        cad_param_name  TEXT,
                        is_active       BOOLEAN DEFAULT TRUE,
                        UNIQUE (part_code, field_name)
                    );

                    -- DimensionOptions 테이블
                    CREATE TABLE IF NOT EXISTS DimensionOptions (
                        id              INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                        part_code       TEXT NOT NULL,
                        spec_filter_key TEXT NOT NULL,
                        option_level    INTEGER NOT NULL,
                        option_name     TEXT,
                        parent_value    TEXT,
                        valid_values    TEXT NOT NULL,
                        value_count     INTEGER NOT NULL,
                        is_active       BOOLEAN DEFAULT TRUE,
                        UNIQUE(part_code, spec_filter_key, option_level, parent_value)
                    );

                    -- PartDimension 테이블
                    CREATE TABLE IF NOT EXISTS PartDimension (
                        id              INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                        part_code       TEXT NOT NULL,
                        spec_filter_key TEXT NOT NULL,
                        dim_key_1       TEXT NOT NULL,
                        dim_key_2       TEXT,
                        dim_key_3       TEXT,
                        dimension_data  TEXT NOT NULL,
                        part_number     TEXT NOT NULL,
                        is_active       BOOLEAN DEFAULT TRUE,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        UNIQUE (part_code, spec_filter_key, dim_key_1, dim_key_2, dim_key_3)
                    );

                    -- 인덱스 생성
                    CREATE INDEX IF NOT EXISTS idx_sub_cat_main ON SubCategory(main_cat_code);
                    CREATE INDEX IF NOT EXISTS idx_mid_cat_sub ON MidCategory(sub_cat_code);
                    CREATE INDEX IF NOT EXISTS idx_part_type_mid ON PartType(mid_cat_code);
                    CREATE INDEX IF NOT EXISTS idx_part_series_type ON PartSeries(part_type_code);
                ";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        #endregion
    }
}
