-- ============================================================================
-- PartManager - Updated Category Schema (v2.1)
-- ============================================================================
-- 표준부품: 대분류(체결류) → 중분류(볼트) → 부품(육각머리볼트)
-- 상용부품: 업체(SMC) → 부품종류(표준실린더) → 시리즈(CJ1)
-- ============================================================================

PRAGMA encoding = 'UTF-8';
PRAGMA foreign_keys = ON;

-- ============================================================================
-- 기존 테이블 삭제
-- ============================================================================
DROP TABLE IF EXISTS PartSeries;
DROP TABLE IF EXISTS PartType;
DROP TABLE IF EXISTS MidCategory;
DROP TABLE IF EXISTS SubCategory;
DROP TABLE IF EXISTS MainCategory;

-- ============================================================================
-- 1. 메인 카테고리 (MainCategory) - 1열
-- ============================================================================
CREATE TABLE IF NOT EXISTS MainCategory (
    main_cat_id     INTEGER PRIMARY KEY AUTOINCREMENT,
    main_cat_code   TEXT NOT NULL UNIQUE,
    main_cat_name   TEXT NOT NULL,
    main_cat_name_kr TEXT,
    is_standard     INTEGER DEFAULT 0,
    icon_char       TEXT,
    color_code      TEXT,
    sort_order      INTEGER DEFAULT 0,
    is_active       INTEGER DEFAULT 1,
    description     TEXT,
    created_at      TEXT DEFAULT (datetime('now', 'localtime')),
    updated_at      TEXT DEFAULT (datetime('now', 'localtime'))
);

-- ============================================================================
-- 2. 서브 카테고리 (SubCategory) - 표준부품 대분류 / 상용부품 업체
-- ============================================================================
CREATE TABLE IF NOT EXISTS SubCategory (
    sub_cat_id      INTEGER PRIMARY KEY AUTOINCREMENT,
    sub_cat_code    TEXT NOT NULL UNIQUE,
    sub_cat_name    TEXT NOT NULL,
    sub_cat_name_kr TEXT,
    main_cat_code   TEXT NOT NULL,
    is_vendor       INTEGER DEFAULT 0,
    vendor_code     TEXT,
    country         TEXT,
    icon_char       TEXT,
    color_code      TEXT,
    sort_order      INTEGER DEFAULT 0,
    is_active       INTEGER DEFAULT 1,
    description     TEXT,
    created_at      TEXT DEFAULT (datetime('now', 'localtime')),
    updated_at      TEXT DEFAULT (datetime('now', 'localtime')),
    FOREIGN KEY (main_cat_code) REFERENCES MainCategory(main_cat_code)
);

CREATE INDEX IF NOT EXISTS idx_subcat_main ON SubCategory(main_cat_code);

-- ============================================================================
-- 3. 중분류 (MidCategory) - 표준부품 전용 (체결류 → 볼트, 나사, 와셔)
-- ============================================================================
CREATE TABLE IF NOT EXISTS MidCategory (
    mid_cat_id      INTEGER PRIMARY KEY AUTOINCREMENT,
    mid_cat_code    TEXT NOT NULL UNIQUE,
    mid_cat_name    TEXT NOT NULL,
    mid_cat_name_kr TEXT,
    sub_cat_code    TEXT NOT NULL,
    icon_char       TEXT,
    sort_order      INTEGER DEFAULT 0,
    is_active       INTEGER DEFAULT 1,
    description     TEXT,
    created_at      TEXT DEFAULT (datetime('now', 'localtime')),
    updated_at      TEXT DEFAULT (datetime('now', 'localtime')),
    FOREIGN KEY (sub_cat_code) REFERENCES SubCategory(sub_cat_code)
);

CREATE INDEX IF NOT EXISTS idx_midcat_subcat ON MidCategory(sub_cat_code);

-- ============================================================================
-- 4. 부품 타입 (PartType) - 표준부품 상세 / 상용부품 종류
-- ============================================================================
CREATE TABLE IF NOT EXISTS PartType (
    part_type_id    INTEGER PRIMARY KEY AUTOINCREMENT,
    part_type_code  TEXT NOT NULL UNIQUE,
    part_type_name  TEXT NOT NULL,
    part_type_name_kr TEXT,
    -- 표준부품: mid_cat_code 사용, 상용부품: sub_cat_code 사용
    sub_cat_code    TEXT,
    mid_cat_code    TEXT,
    icon_char       TEXT,
    has_series      INTEGER DEFAULT 0,
    sort_order      INTEGER DEFAULT 0,
    is_active       INTEGER DEFAULT 1,
    description     TEXT,
    created_at      TEXT DEFAULT (datetime('now', 'localtime')),
    updated_at      TEXT DEFAULT (datetime('now', 'localtime')),
    FOREIGN KEY (sub_cat_code) REFERENCES SubCategory(sub_cat_code),
    FOREIGN KEY (mid_cat_code) REFERENCES MidCategory(mid_cat_code)
);

CREATE INDEX IF NOT EXISTS idx_parttype_subcat ON PartType(sub_cat_code);
CREATE INDEX IF NOT EXISTS idx_parttype_midcat ON PartType(mid_cat_code);

-- ============================================================================
-- 5. 시리즈 (PartSeries) - 상용부품 전용
-- ============================================================================
CREATE TABLE IF NOT EXISTS PartSeries (
    series_id       INTEGER PRIMARY KEY AUTOINCREMENT,
    series_code     TEXT NOT NULL UNIQUE,
    series_name     TEXT NOT NULL,
    series_name_kr  TEXT,
    part_type_code  TEXT NOT NULL,
    vendor_code     TEXT,
    db_table_name   TEXT,
    sort_order      INTEGER DEFAULT 0,
    is_active       INTEGER DEFAULT 1,
    description     TEXT,
    created_at      TEXT DEFAULT (datetime('now', 'localtime')),
    updated_at      TEXT DEFAULT (datetime('now', 'localtime')),
    FOREIGN KEY (part_type_code) REFERENCES PartType(part_type_code)
);

CREATE INDEX IF NOT EXISTS idx_series_parttype ON PartSeries(part_type_code);

-- ============================================================================
-- 샘플 데이터 삽입
-- ============================================================================

-- 1. 메인 카테고리
INSERT INTO MainCategory (main_cat_code, main_cat_name, main_cat_name_kr, is_standard, icon_char, color_code, sort_order) VALUES
('STANDARD', 'Standard Parts', '표준부품', 1, '🔩', '#3498DB', 1),
('CYLINDER', 'Cylinder', '실린더', 0, '▰', '#E74C3C', 2),
('LM_GUIDE', 'LM Guide', 'LM가이드', 0, '═', '#2ECC71', 3),
('MOTOR', 'Motor', '모터', 0, '⚡', '#F39C12', 4),
('CABLE_BEAR', 'Cable Bearer', '케이블베어', 0, '⛓', '#9B59B6', 5);

-- 2. 서브 카테고리 - 표준부품 대분류
INSERT INTO SubCategory (sub_cat_code, sub_cat_name, sub_cat_name_kr, main_cat_code, is_vendor, sort_order) VALUES
('STD_FASTENER', 'Fastener', '체결류', 'STANDARD', 0, 1),
('STD_STEEL', 'Steel Shape', '형강', 'STANDARD', 0, 2),
('STD_SPRING', 'Spring', '스프링', 'STANDARD', 0, 3),
('STD_BEARING', 'Bearing', '베어링', 'STANDARD', 0, 4),
('STD_PIPE', 'Pipe & Fitting', '배관', 'STANDARD', 0, 5);

-- 2. 서브 카테고리 - 실린더 업체
INSERT INTO SubCategory (sub_cat_code, sub_cat_name, sub_cat_name_kr, main_cat_code, is_vendor, vendor_code, country, sort_order) VALUES
('CYL_SMC', 'SMC', 'SMC', 'CYLINDER', 1, 'SMC', 'JP', 1),
('CYL_FESTO', 'FESTO', 'FESTO', 'CYLINDER', 1, 'FESTO', 'DE', 2),
('CYL_TPC', 'TPC', 'TPC', 'CYLINDER', 1, 'TPC', 'KR', 3),
('CYL_KCC', 'KCC', 'KCC', 'CYLINDER', 1, 'KCC', 'KR', 4);

-- 2. 서브 카테고리 - LM가이드 업체
INSERT INTO SubCategory (sub_cat_code, sub_cat_name, sub_cat_name_kr, main_cat_code, is_vendor, vendor_code, country, sort_order) VALUES
('LM_THK', 'THK', 'THK', 'LM_GUIDE', 1, 'THK', 'JP', 1),
('LM_HIWIN', 'HIWIN', 'HIWIN', 'LM_GUIDE', 1, 'HIWIN', 'TW', 2),
('LM_NSK', 'NSK', 'NSK', 'LM_GUIDE', 1, 'NSK', 'JP', 3);

-- 2. 서브 카테고리 - 모터 업체
INSERT INTO SubCategory (sub_cat_code, sub_cat_name, sub_cat_name_kr, main_cat_code, is_vendor, vendor_code, country, sort_order) VALUES
('MOT_ORIENTAL', 'Oriental Motor', '오리엔탈모터', 'MOTOR', 1, 'ORIENTAL', 'JP', 1),
('MOT_MITSUBISHI', 'Mitsubishi', '미쓰비시', 'MOTOR', 1, 'MITSUBISHI', 'JP', 2);

-- 3. 중분류 - 체결류
INSERT INTO MidCategory (mid_cat_code, mid_cat_name, mid_cat_name_kr, sub_cat_code, sort_order) VALUES
('BOLT', 'Bolt', '볼트', 'STD_FASTENER', 1),
('SCREW', 'Screw', '나사', 'STD_FASTENER', 2),
('WASHER', 'Washer', '와셔', 'STD_FASTENER', 3),
('BEARING_F', 'Bearing', '베어링', 'STD_FASTENER', 4),
('NUT', 'Nut', '너트', 'STD_FASTENER', 5),
('PIN', 'Pin', '핀', 'STD_FASTENER', 6),
('KEY', 'Key', '키', 'STD_FASTENER', 7);

-- 3. 중분류 - 형강
INSERT INTO MidCategory (mid_cat_code, mid_cat_name, mid_cat_name_kr, sub_cat_code, sort_order) VALUES
('H_BEAM', 'H Beam', 'H빔', 'STD_STEEL', 1),
('CHANNEL', 'Channel', '채널', 'STD_STEEL', 2),
('ANGLE', 'Angle', '앵글', 'STD_STEEL', 3),
('FLAT_BAR', 'Flat Bar', '플랫바', 'STD_STEEL', 4);

-- 3. 중분류 - 스프링
INSERT INTO MidCategory (mid_cat_code, mid_cat_name, mid_cat_name_kr, sub_cat_code, sort_order) VALUES
('COMP_SPRING', 'Compression Spring', '압축스프링', 'STD_SPRING', 1),
('EXT_SPRING', 'Extension Spring', '인장스프링', 'STD_SPRING', 2),
('TOR_SPRING', 'Torsion Spring', '비틀림스프링', 'STD_SPRING', 3);

-- 4. 부품타입 - 볼트 상세
INSERT INTO PartType (part_type_code, part_type_name, part_type_name_kr, mid_cat_code, has_series, sort_order) VALUES
('HEX_BOLT', 'Hexagon Head Bolt', '육각머리볼트', 'BOLT', 0, 1),
('T_SLOT_BOLT', 'T-Slot Bolt', 'T홈볼트', 'BOLT', 0, 2),
('WING_BOLT', 'Wing Bolt', '나비볼트', 'BOLT', 0, 3),
('U_BOLT', 'U Bolt', 'U볼트', 'BOLT', 0, 4),
('EYE_BOLT', 'Eye Bolt', '아이볼트', 'BOLT', 0, 5),
('SOCKET_BOLT', 'Socket Head Cap Screw', '소켓볼트', 'BOLT', 0, 6),
('FLANGE_BOLT', 'Flange Bolt', '플랜지볼트', 'BOLT', 0, 7);

-- 4. 부품타입 - 나사 상세
INSERT INTO PartType (part_type_code, part_type_name, part_type_name_kr, mid_cat_code, has_series, sort_order) VALUES
('MACHINE_SCREW', 'Machine Screw', '기계나사', 'SCREW', 0, 1),
('SET_SCREW', 'Set Screw', '멈춤나사', 'SCREW', 0, 2),
('TAPPING_SCREW', 'Tapping Screw', '탭핑나사', 'SCREW', 0, 3),
('WOOD_SCREW', 'Wood Screw', '나무나사', 'SCREW', 0, 4);

-- 4. 부품타입 - 와셔 상세
INSERT INTO PartType (part_type_code, part_type_name, part_type_name_kr, mid_cat_code, has_series, sort_order) VALUES
('FLAT_WASHER', 'Flat Washer', '평와셔', 'WASHER', 0, 1),
('SPRING_WASHER', 'Spring Washer', '스프링와셔', 'WASHER', 0, 2),
('LOCK_WASHER', 'Lock Washer', '록와셔', 'WASHER', 0, 3);

-- 4. 부품타입 - 너트 상세
INSERT INTO PartType (part_type_code, part_type_name, part_type_name_kr, mid_cat_code, has_series, sort_order) VALUES
('HEX_NUT', 'Hexagon Nut', '육각너트', 'NUT', 0, 1),
('LOCK_NUT', 'Lock Nut', '록너트', 'NUT', 0, 2),
('FLANGE_NUT', 'Flange Nut', '플랜지너트', 'NUT', 0, 3),
('CAP_NUT', 'Cap Nut', '캡너트', 'NUT', 0, 4);

-- 4. 부품타입 - SMC 실린더 종류
INSERT INTO PartType (part_type_code, part_type_name, part_type_name_kr, sub_cat_code, has_series, sort_order) VALUES
('SMC_STD_CYL', 'Standard Cylinder', '표준실린더', 'CYL_SMC', 1, 1),
('SMC_COMPACT_CYL', 'Compact Cylinder', '박형실린더', 'CYL_SMC', 1, 2),
('SMC_CLAMP_CYL', 'Clamp Cylinder', '클램프실린더', 'CYL_SMC', 1, 3),
('SMC_TABLE_CYL', 'Table Cylinder', '테이블실린더', 'CYL_SMC', 1, 4);

-- 4. 부품타입 - THK LM가이드 종류
INSERT INTO PartType (part_type_code, part_type_name, part_type_name_kr, sub_cat_code, has_series, sort_order) VALUES
('THK_LM_GUIDE', 'LM Guide', 'LM가이드', 'LM_THK', 1, 1),
('THK_BALL_SCREW', 'Ball Screw', '볼스크류', 'LM_THK', 1, 2),
('THK_ACTUATOR', 'Linear Actuator', '리니어액추에이터', 'LM_THK', 1, 3);

-- 5. 시리즈 - SMC 표준실린더
INSERT INTO PartSeries (series_code, series_name, series_name_kr, part_type_code, vendor_code, sort_order) VALUES
('SMC_CJ1', 'CJ1', 'CJ1', 'SMC_STD_CYL', 'SMC', 1),
('SMC_CJP', 'CJP', 'CJP', 'SMC_STD_CYL', 'SMC', 2),
('SMC_CJP2', 'CJP2', 'CJP2', 'SMC_STD_CYL', 'SMC', 3),
('SMC_CJ2', 'CJ2', 'CJ2', 'SMC_STD_CYL', 'SMC', 4),
('SMC_CM2', 'CM2', 'CM2', 'SMC_STD_CYL', 'SMC', 5),
('SMC_CM3', 'CM3', 'CM3', 'SMC_STD_CYL', 'SMC', 6);

-- 5. 시리즈 - SMC 박형실린더
INSERT INTO PartSeries (series_code, series_name, series_name_kr, part_type_code, vendor_code, sort_order) VALUES
('SMC_CQ2', 'CQ2', 'CQ2', 'SMC_COMPACT_CYL', 'SMC', 1),
('SMC_CDQ2', 'CDQ2', 'CDQ2', 'SMC_COMPACT_CYL', 'SMC', 2);

-- 5. 시리즈 - THK LM가이드
INSERT INTO PartSeries (series_code, series_name, series_name_kr, part_type_code, vendor_code, sort_order) VALUES
('THK_SHS', 'SHS', 'SHS', 'THK_LM_GUIDE', 'THK', 1),
('THK_SSR', 'SSR', 'SSR', 'THK_LM_GUIDE', 'THK', 2),
('THK_SRS', 'SRS', 'SRS', 'THK_LM_GUIDE', 'THK', 3);

-- 5. 시리즈 - THK 볼스크류
INSERT INTO PartSeries (series_code, series_name, series_name_kr, part_type_code, vendor_code, sort_order) VALUES
('THK_BNK', 'BNK', 'BNK', 'THK_BALL_SCREW', 'THK', 1),
('THK_BTK', 'BTK', 'BTK', 'THK_BALL_SCREW', 'THK', 2);
