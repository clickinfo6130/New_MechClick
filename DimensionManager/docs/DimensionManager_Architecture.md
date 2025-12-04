# DimensionManager 프로젝트 문서

## 1. 프로젝트 개요

### 1.1 목적
**DimensionManager**는 CAD 시스템에서 사용되는 기계 부품의 치수 데이터를 관리하기 위한 WPF 애플리케이션입니다. 
엑셀 파일에서 치수 데이터를 읽어 PostgreSQL 데이터베이스에 저장하고, 추후 스펙 선택 UI에서 해당 치수 데이터를 조회하여 CAD 도면 작성에 활용합니다.

### 1.2 주요 기능
- 엑셀 파일에서 치수 데이터 Import
- 가변적인 키 필드 지원 (Standard, List, 용도, 재질 등)
- 동적 컬럼 구조 지원 (부품마다 다른 치수 필드)
- PostgreSQL 데이터베이스 저장/조회
- JSON 기반 유연한 데이터 구조

### 1.3 기술 스택
| 구분 | 기술 |
|------|------|
| 프레임워크 | .NET Framework 4.8 |
| UI | WPF (Windows Presentation Foundation) |
| 엑셀 처리 | EPPlus 6.2.10 |
| 데이터베이스 | PostgreSQL |
| DB 드라이버 | Npgsql 8.0.0 |

---

## 2. 시스템 아키텍처

### 2.1 전체 구조도

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           DimensionManager                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐       │
│  │   Presentation   │    │    Business      │    │      Data        │       │
│  │      Layer       │───▶│     Layer        │───▶│     Layer        │       │
│  │                  │    │                  │    │                  │       │
│  │  - MainWindow    │    │  - ExcelService  │    │ - PostgresService│       │
│  │  - DbSettings    │    │  - Models        │    │                  │       │
│  │    Dialog        │    │                  │    │                  │       │
│  └──────────────────┘    └──────────────────┘    └──────────────────┘       │
│           │                       │                       │                  │
└───────────┼───────────────────────┼───────────────────────┼──────────────────┘
            │                       │                       │
            ▼                       ▼                       ▼
    ┌──────────────┐       ┌──────────────┐       ┌──────────────┐
    │    User      │       │  Excel File  │       │  PostgreSQL  │
    │  Interface   │       │   (*.xlsx)   │       │   Database   │
    └──────────────┘       └──────────────┘       └──────────────┘
```

### 2.2 데이터 흐름도

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│             │     │             │     │             │     │             │
│  Excel File │────▶│ ExcelService│────▶│ ImportResult│────▶│  Postgres   │
│   (치수)    │     │   (파싱)    │     │  (모델링)   │     │  Service    │
│             │     │             │     │             │     │   (저장)    │
└─────────────┘     └─────────────┘     └─────────────┘     └─────────────┘
                                                                   │
                                                                   ▼
                                              ┌─────────────────────────────┐
                                              │        PostgreSQL DB        │
                                              ├─────────────────────────────┤
                                              │  - DimensionMeta            │
                                              │  - DimensionKeyOption       │
                                              │  - PartDimension            │
                                              └─────────────────────────────┘
```

### 2.3 레이어별 책임

| 레이어 | 구성 요소 | 책임 |
|--------|-----------|------|
| **Presentation** | MainWindow, DbSettingsDialog | 사용자 인터페이스, 이벤트 처리 |
| **Business** | ExcelService, Models | 비즈니스 로직, 데이터 파싱/변환 |
| **Data** | PostgresService | 데이터베이스 연결, CRUD 작업 |

---

## 3. 프로젝트 구조

### 3.1 폴더 구조

```
DimensionManager/
│
├── App.xaml                          # 애플리케이션 정의 및 전역 스타일
├── App.xaml.cs                       # 애플리케이션 진입점
│
├── MainWindow.xaml                   # 메인 화면 XAML
├── MainWindow.xaml.cs                # 메인 화면 코드비하인드
│
├── DbSettingsDialog.xaml             # DB 설정 다이얼로그 XAML
├── DbSettingsDialog.xaml.cs          # DB 설정 다이얼로그 코드
│
├── DimensionManager.csproj           # 프로젝트 파일
│
├── Models/                           # 데이터 모델
│   ├── DimensionMeta.cs              # 치수 필드 메타데이터
│   ├── DimensionKeyOption.cs         # 키값 옵션
│   ├── PartDimension.cs              # 치수 데이터
│   └── DimensionImportResult.cs      # Import 결과 컨테이너
│
└── Services/                         # 서비스 레이어
    ├── ExcelService.cs               # 엑셀 파일 처리
    └── PostgresService.cs            # PostgreSQL 연결/저장
```

### 3.2 클래스 다이어그램

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                                 Models                                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌─────────────────────┐   ┌─────────────────────┐   ┌─────────────────────┐│
│  │   DimensionMeta     │   │ DimensionKeyOption  │   │   PartDimension     ││
│  ├─────────────────────┤   ├─────────────────────┤   ├─────────────────────┤│
│  │ - Id                │   │ - Id                │   │ - Id                ││
│  │ - PartCode          │   │ - PartCode          │   │ - PartCode          ││
│  │ - FieldName         │   │ - KeyFieldName      │   │ - KeyComposite      ││
│  │ - DisplayName       │   │ - KeyLevel          │   │ - KeyValuesJson     ││
│  │ - DataType          │   │ - KeyValue          │   │ - DimensionDataJson ││
│  │ - IsKeyField        │   │ - ParentKey         │   │ - IsActive          ││
│  │ - DisplayOrder      │   │ - SortOrder         │   │                     ││
│  │ - CadParamName      │   │ - IsActive          │   │                     ││
│  └─────────────────────┘   └─────────────────────┘   └─────────────────────┘│
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────────┐│
│  │                        DimensionImportResult                             ││
│  ├─────────────────────────────────────────────────────────────────────────┤│
│  │ - PartCode: string                                                       ││
│  │ - Standard: string                                                       ││
│  │ - Columns: List<ColumnInfo>                                              ││
│  │ - KeyFields: List<KeyFieldInfo>                                          ││
│  │ - DimensionMetas: List<DimensionMeta>                                    ││
│  │ - KeyOptions: List<DimensionKeyOption>                                   ││
│  │ - Dimensions: List<PartDimension>                                        ││
│  │ - RawData: List<Dictionary<string, object>>                              ││
│  └─────────────────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                                Services                                      │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────────────────────┐   ┌──────────────────────────────────┐│
│  │          ExcelService            │   │        PostgresService           ││
│  ├──────────────────────────────────┤   ├──────────────────────────────────┤│
│  │ + ReadDimensionExcel()           │   │ + SetConnection()                ││
│  │ - ReadColumnInfo()               │   │ + TestConnectionAsync()          ││
│  │ - IdentifyKeyFields()            │   │ + CreateTablesIfNotExistsAsync() ││
│  │ - ReadData()                     │   │ + SaveDimensionDataAsync()       ││
│  │ - GenerateDimensionMetas()       │   │ + GetKeyFieldsAsync()            ││
│  │ - GenerateKeyOptions()           │   │ + GetKeyOptionsAsync()           ││
│  │ - GeneratePartDimensions()       │   │ + GetDimensionDataAsync()        ││
│  │ - DictToJson()                   │   │ - InsertDimensionMetasAsync()    ││
│  └──────────────────────────────────┘   │ - InsertKeyOptionsAsync()        ││
│                                         │ - InsertPartDimensionsAsync()    ││
│                                         └──────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. 데이터베이스 스키마

### 4.1 ERD (Entity Relationship Diagram)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            Database: Standard_Core                           │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌─────────────────────┐                                                    │
│  │   DimensionMeta     │                                                    │
│  │   (치수 필드 정의)   │                                                    │
│  ├─────────────────────┤         ┌─────────────────────┐                    │
│  │ PK id               │         │ DimensionKeyOption  │                    │
│  │    part_code  ──────┼────┐    │  (키값 옵션 목록)    │                    │
│  │    field_name       │    │    ├─────────────────────┤                    │
│  │    display_name     │    │    │ PK id               │                    │
│  │    display_name_en  │    ├───▶│    part_code        │                    │
│  │    data_type        │    │    │    key_field_name   │                    │
│  │    decimal_places   │    │    │    key_level        │                    │
│  │    unit             │    │    │    key_value        │                    │
│  │    display_order    │    │    │    parent_key       │                    │
│  │    is_key_field     │    │    │    sort_order       │                    │
│  │    is_display_field │    │    │    is_active        │                    │
│  │    column_width     │    │    └─────────────────────┘                    │
│  │    cad_param_name   │    │                                               │
│  │    is_active        │    │    ┌─────────────────────┐                    │
│  └─────────────────────┘    │    │   PartDimension     │                    │
│                             │    │  (실제 치수 데이터)  │                    │
│  UNIQUE(part_code,          │    ├─────────────────────┤                    │
│         field_name)         │    │ PK id               │                    │
│                             └───▶│    part_code        │                    │
│                                  │    key_composite    │                    │
│                                  │    key_values (JSON)│                    │
│                                  │    dimension_data   │                    │
│                                  │      (JSON)         │                    │
│                                  │    is_active        │                    │
│                                  └─────────────────────┘                    │
│                                                                              │
│                                  UNIQUE(part_code,                          │
│                                         key_composite)                      │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 4.2 테이블 상세 명세

#### 4.2.1 DimensionMeta (치수 필드 메타데이터)

| 컬럼명 | 데이터타입 | NULL | 기본값 | 설명 |
|--------|------------|------|--------|------|
| id | INTEGER | NO | IDENTITY | 고유 ID (PK) |
| part_code | TEXT | NO | - | 부품 코드 (예: HBOLT) |
| field_name | TEXT | NO | - | 필드명 (예: M, P1, H) |
| display_name | TEXT | NO | - | 한글 표시명 |
| display_name_en | TEXT | YES | NULL | 영문 표시명 |
| data_type | TEXT | NO | 'DECIMAL' | 데이터 타입 |
| decimal_places | INTEGER | NO | 2 | 소수점 자릿수 |
| unit | TEXT | YES | NULL | 단위 (mm) |
| display_order | INTEGER | NO | - | 표시 순서 |
| is_key_field | BOOLEAN | NO | FALSE | 키 필드 여부 |
| is_active | BOOLEAN | NO | TRUE | 활성화 여부 |
| created_at | TIMESTAMP | NO | NOW() | 생성일시 |
| updated_at | TIMESTAMP | NO | NOW() | 수정일시 |

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

**제약조건**: 은 없음... part_code / field_name도 동일한 내용이 존재 할수 있기에 ( field_name은 추후 고민이 필요할듯..)

#### 4.2.2 DimensionKeyOption (키값 옵션)

| 컬럼명 | 데이터타입 | NULL | 기본값 | 설명 |
|--------|------------|------|--------|------|
| id | INTEGER | NO | IDENTITY | 고유 ID (PK) |
| part_code | TEXT | NO | - | 부품 코드 |
| key_field_name | TEXT | NO | - | 키 필드명 (Standard, List, 용도) |
| key_level | INTEGER | NO | - | 키 레벨 (선택 순서) |
| key_value | TEXT | NO | - | 키 값 (KS B 1002, M10) |
| parent_key | TEXT | YES | NULL | 상위 키 (계층 필터링용) |
| sort_order | INTEGER | NO | 0 | 정렬 순서 |
| is_active | BOOLEAN | NO | TRUE | 활성화 여부 |
| created_at | TIMESTAMP | NO | NOW() | 생성일시 |

` -- DimensionKeyOption 테이블 (키값별 옵션 목록)'
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

**제약조건**: 은 없음

#### 4.2.3 PartDimension (치수 데이터)

| 컬럼명 | 데이터타입 | NULL | 기본값 | 설명 |
|--------|------------|------|--------|------|
| id | INTEGER | NO | IDENTITY | 고유 ID (PK) |
| part_code | TEXT | NO | - | 부품 코드 |
| key_composite | TEXT | NO | - | 키 조합 문자열 |
| key_values | JSONB | NO | - | 키값 JSON |
| dimension_data | JSONB | NO | - | 치수 데이터 JSON |
| is_active | BOOLEAN | NO | TRUE | 활성화 여부 |
| created_at | TIMESTAMP | NO | NOW() | 생성일시 |
| updated_at | TIMESTAMP | NO | NOW() | 수정일시 |

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


**제약조건**: `UNIQUE(key_composite)`

### 4.3 인덱스

```sql
-- DimensionMeta 인덱스
CREATE INDEX idx_dim_meta_part ON DimensionMeta(part_code);

-- DimensionKeyOption 인덱스
CREATE INDEX idx_dim_key_part ON DimensionKeyOption(part_code);
CREATE INDEX idx_dim_key_field ON DimensionKeyOption(part_code, key_field_name);

-- PartDimension 인덱스
CREATE INDEX idx_part_dim_part ON PartDimension(part_code);
CREATE INDEX idx_part_dim_composite ON PartDimension(part_code, key_composite);
CREATE INDEX idx_part_dim_keys ON PartDimension USING GIN (key_values);
```

---

## 5. 데이터 구조 예시

### 5.1 엑셀 데이터 구조

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ Row 2 (표시명)  │       │       │ 구분  │사이즈│보통나사│가는나사│육각머리...│
│ Row 3 (부가설명)│       │       │       │     │(거친)  │(고은)  │          │
│ Row 4 (필드명)  │ Name  │Standard│ List │ 용도│   M   │P1(UNC) │P2(UNF)│ H │
├─────────────────────────────────────────────────────────────────────────────┤
│ Row 5 (데이터)  │ HBOLT │KS B1002│  M3  │기계용│   3   │  0.5   │   0   │ 2 │
│ Row 6           │ HBOLT │KS B1002│  M4  │기계용│   4   │  0.7   │   0   │2.8│
│ Row 7           │ HBOLT │KS B1002│  M5  │기계용│   5   │  0.8   │   0   │3.5│
│ ...             │  ...  │  ...   │ ...  │ ... │  ...  │  ...   │  ...  │...│
└─────────────────────────────────────────────────────────────────────────────┘

     ◀─────── 키 필드 ───────▶  ◀────────── 치수 필드 ──────────▶
```

### 5.2 변환 후 데이터 구조

#### DimensionMeta 예시
| part_code | field_name | display_name | is_key_field | display_order |
|-----------|------------|--------------|--------------|---------------|
| HBOLT | Name | Name | TRUE | 1 |
| HBOLT | Standard | Standard | TRUE | 2 |
| HBOLT | List | List | TRUE | 3 |
| HBOLT | 용도 | 구분 | TRUE | 4 |
| HBOLT | M | 사이즈(d) | FALSE | 5 |
| HBOLT | P1 | 보통나사 | FALSE | 6 |
| HBOLT | H | 육각머리 높이 | FALSE | 7 |

#### DimensionKeyOption 예시
| part_code | key_field_name | key_level | key_value | parent_key |
|-----------|----------------|-----------|-----------|------------|
| HBOLT | Name | 1 | HBOLT | NULL |
| HBOLT | Standard | 2 | KS B 1002 | NULL |
| HBOLT | List | 3 | M3 | NULL |
| HBOLT | List | 3 | M4 | NULL |
| HBOLT | List | 3 | M5 | NULL |
| HBOLT | 용도 | 4 | 기계용 | NULL |
| HBOLT | 용도 | 4 | PC용 | NULL |

#### PartDimension 예시
| part_code | key_composite | key_values | dimension_data |
|-----------|---------------|------------|----------------|
| HBOLT | HBOLT\|KS B 1002\|M3\|기계용 | {"Name":"HBOLT","Standard":"KS B 1002","List":"M3","용도":"기계용"} | {"M":3,"P1":0.5,"P2":0,"H":2,"H1":0,"B1":5.5,...} |
| HBOLT | HBOLT\|KS B 1002\|M4\|기계용 | {"Name":"HBOLT","Standard":"KS B 1002","List":"M4","용도":"기계용"} | {"M":4,"P1":0.7,"P2":0,"H":2.8,...} |

---

## 6. 주요 프로세스

### 6.1 엑셀 Import 프로세스

```
┌──────────────────────────────────────────────────────────────────────────┐
│                        Excel Import Process                               │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                           │
│  1. 파일 열기                                                             │
│     └──▶ ExcelPackage 생성                                               │
│                                                                           │
│  2. 컬럼 정보 읽기 (Row 2, 3, 4)                                          │
│     ├──▶ FieldName (Row 4)                                               │
│     ├──▶ DisplayName (Row 2)                                             │
│     └──▶ SubDisplayName (Row 3)                                          │
│                                                                           │
│  3. 키 필드 식별                                                          │
│     └──▶ _keyFieldNames와 비교하여 IsKeyField 설정                        │
│     └──▶ 키 레벨 순서 부여 (1, 2, 3...)                                   │
│                                                                           │
│  4. 데이터 읽기 (Row 5 ~)                                                 │
│     ├──▶ 각 행을 Dictionary<string, object>로 변환                       │
│     └──▶ 키 필드 고유값 수집                                              │
│                                                                           │
│  5. 메타데이터 생성                                                       │
│     └──▶ List<DimensionMeta> 생성                                        │
│                                                                           │
│  6. 키 옵션 생성                                                          │
│     └──▶ List<DimensionKeyOption> 생성                                   │
│                                                                           │
│  7. 치수 데이터 생성                                                      │
│     ├──▶ key_composite 생성 (키값들을 | 로 연결)                          │
│     ├──▶ key_values JSON 생성                                            │
│     └──▶ dimension_data JSON 생성                                        │
│                                                                           │
│  8. DimensionImportResult 반환                                           │
│                                                                           │
└──────────────────────────────────────────────────────────────────────────┘
```

### 6.2 DB 저장 프로세스

```
┌──────────────────────────────────────────────────────────────────────────┐
│                          DB Save Process                                  │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                           │
│  1. 트랜잭션 시작                                                         │
│                                                                           │
│  2. 기존 데이터 삭제 (part_code 기준)                                     │
│     ├──▶ DELETE FROM PartDimension WHERE part_code = ?                   │
│     ├──▶ DELETE FROM DimensionKeyOption WHERE part_code = ?              │
│     └──▶ DELETE FROM DimensionMeta WHERE part_code = ?                   │
│                                                                           │
│  3. 새 데이터 삽입                                                        │
│     ├──▶ INSERT INTO DimensionMeta (...)                                 │
│     ├──▶ INSERT INTO DimensionKeyOption (...)                            │
│     └──▶ INSERT INTO PartDimension (...)                                 │
│                                                                           │
│  4. 트랜잭션 커밋                                                         │
│     └──▶ 오류 시 롤백                                                    │
│                                                                           │
└──────────────────────────────────────────────────────────────────────────┘
```

### 6.3 치수 조회 프로세스 (향후 사용)

```
┌──────────────────────────────────────────────────────────────────────────┐
│                      Dimension Query Process                              │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                           │
│  [스펙 선택 UI]                                                           │
│                                                                           │
│  1. 키 필드 목록 조회                                                     │
│     SELECT * FROM DimensionMeta                                          │
│     WHERE part_code = 'HBOLT' AND is_key_field = TRUE                    │
│     ORDER BY display_order                                               │
│     ──▶ [Standard, List, 용도]                                           │
│                                                                           │
│  2. 첫 번째 키 옵션 로드 (Standard)                                       │
│     SELECT DISTINCT key_value FROM DimensionKeyOption                    │
│     WHERE part_code = 'HBOLT' AND key_field_name = 'Standard'            │
│     ──▶ [KS B 1002, JIS B 1180, DIN 931]                                 │
│                                                                           │
│  3. 사용자가 'KS B 1002' 선택                                             │
│     ──▶ 다음 키 옵션 로드 (List)                                         │
│                                                                           │
│  4. 사용자가 'M10' 선택                                                   │
│     ──▶ 다음 키 옵션 로드 (용도)                                         │
│                                                                           │
│  5. 사용자가 '기계용' 선택                                                │
│     ──▶ 치수 데이터 조회                                                 │
│                                                                           │
│  6. 치수 데이터 조회                                                      │
│     SELECT dimension_data FROM PartDimension                             │
│     WHERE part_code = 'HBOLT'                                            │
│       AND key_composite = 'HBOLT|KS B 1002|M10|기계용'                   │
│     ──▶ {"M":10, "P1":1.5, "H":6.4, "B1":17, ...}                        │
│                                                                           │
│  7. JSON 파싱 후 CAD 파라미터로 전달                                      │
│                                                                           │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 7. 키 필드 자동 인식

### 7.1 기본 키 필드 목록

ExcelService에서 다음 컬럼명은 자동으로 키 필드로 인식됩니다:

```csharp
private readonly HashSet<string> _keyFieldNames = new HashSet<string>
{
    "Name",           // 부품명
    "Standard",       // 규격
    "List",           // 사이즈/목록
    "용도", "Usage",   // 용도
    "Material", "재질", // 재질
    "Grade", "강도등급", // 강도등급
    "Surface", "표면처리", // 표면처리
    "Type", "형태"      // 형태
};
```

### 7.2 키 필드 추가 방법

새로운 키 필드를 추가하려면 `ExcelService.cs`의 `_keyFieldNames`에 추가:

```csharp
_keyFieldNames.Add("머리형태");
_keyFieldNames.Add("HeadType");
```

---

## 8. API 사용 예시

### 8.1 C# 코드 예시

```csharp
// PostgresService 인스턴스 생성
var dbService = new PostgresService();
dbService.SetConnection("localhost", 5432, "Standard_Core", "postgres", "password");

// 1. 키 필드 목록 조회
var keyFields = await dbService.GetKeyFieldsAsync("HBOLT");
// 결과: [DimensionMeta {FieldName="Standard"}, DimensionMeta {FieldName="List"}, ...]

// 2. 특정 키 필드의 옵션 조회
var standards = await dbService.GetKeyOptionsAsync("HBOLT", "Standard");
// 결과: ["KS B 1002", "JIS B 1180", "DIN 931"]

var sizes = await dbService.GetKeyOptionsAsync("HBOLT", "List");
// 결과: ["M3", "M4", "M5", "M6", "M8", "M10", ...]

// 3. 치수 데이터 조회
string dimJson = await dbService.GetDimensionDataAsync("HBOLT", "HBOLT|KS B 1002|M10|기계용");
// 결과: {"M":10,"P1":1.5,"P2":0,"H":6.4,"H1":0,"B1":17,"B2":0,"C1":18.9,...}

// 4. JSON 파싱 (Newtonsoft.Json 사용)
var dimensions = JsonConvert.DeserializeObject<Dictionary<string, decimal>>(dimJson);
decimal headHeight = dimensions["H"];    // 6.4
decimal headWidth = dimensions["B1"];    // 17
decimal pitch = dimensions["P1"];        // 1.5
```

### 8.2 SQL 직접 조회 예시

```sql
-- 특정 부품의 모든 키 필드 조회
SELECT field_name, display_name, display_order
FROM DimensionMeta
WHERE part_code = 'HBOLT' AND is_key_field = TRUE
ORDER BY display_order;

-- 특정 키 필드의 옵션 목록 조회
SELECT DISTINCT key_value
FROM DimensionKeyOption
WHERE part_code = 'HBOLT' AND key_field_name = 'List'
ORDER BY sort_order;

-- 특정 조합의 치수 데이터 조회
SELECT dimension_data
FROM PartDimension
WHERE part_code = 'HBOLT' AND key_composite = 'HBOLT|KS B 1002|M10|기계용';

-- JSON 필드 직접 접근 (PostgreSQL JSONB)
SELECT 
    dimension_data->>'M' as size,
    dimension_data->>'H' as head_height,
    dimension_data->>'B1' as head_width
FROM PartDimension
WHERE part_code = 'HBOLT' AND key_values->>'List' = 'M10';
```

---

## 9. 확장 가이드

### 9.1 새로운 부품 추가

1. 엑셀 파일 준비 (동일한 형식)
2. DimensionManager에서 파일 로드
3. DB에 저장

기존 테이블 구조 변경 없이 새로운 부품 추가 가능

### 9.2 새로운 키 필드 추가

1. `ExcelService.cs`의 `_keyFieldNames`에 추가
2. 엑셀 파일에 해당 컬럼 추가
3. 데이터 재Import

### 9.3 계층적 키 필터링 구현

`parent_key` 필드를 활용하여 상위 키 선택에 따른 하위 키 옵션 필터링:

```csharp
// 예: Standard가 'KS B 1002'일 때의 List 옵션만 조회
var sizes = await dbService.GetKeyOptionsAsync("HBOLT", "List", "KS B 1002");
```

---

## 10. 문서 이력

| 버전 | 날짜 | 작성자 | 변경 내용 |
|------|------|--------|-----------|
| 1.0 | 2024-12-03 | Claude | 최초 작성 |

---

## 부록 A: 테이블 생성 SQL

```sql
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
    is_display_field BOOLEAN DEFAULT TRUE,
    column_width    INTEGER DEFAULT 80,
    cad_param_name  TEXT,
    is_active       BOOLEAN DEFAULT TRUE,
    created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (part_code, field_name)
);

-- DimensionKeyOption 테이블 (키값별 옵션 목록)
CREATE TABLE IF NOT EXISTS DimensionKeyOption (
    id              INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    part_code       TEXT NOT NULL,
    key_field_name  TEXT NOT NULL,
    key_level       INTEGER NOT NULL,
    key_value       TEXT NOT NULL,
    parent_key      TEXT,
    sort_order      INTEGER DEFAULT 0,
    is_active       BOOLEAN DEFAULT TRUE,
    created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(part_code, key_field_name, key_value, parent_key)
);

-- PartDimension 테이블 (실제 치수 데이터)
CREATE TABLE IF NOT EXISTS PartDimension (
    id              INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    part_code       TEXT NOT NULL,
    key_composite   TEXT NOT NULL,
    key_values      JSONB NOT NULL,
    dimension_data  JSONB NOT NULL,
    is_active       BOOLEAN DEFAULT TRUE,
    created_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at      TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(part_code, key_composite)
);

-- 인덱스 생성
CREATE INDEX IF NOT EXISTS idx_dim_meta_part ON DimensionMeta(part_code);
CREATE INDEX IF NOT EXISTS idx_dim_key_part ON DimensionKeyOption(part_code);
CREATE INDEX IF NOT EXISTS idx_dim_key_field ON DimensionKeyOption(part_code, key_field_name);
CREATE INDEX IF NOT EXISTS idx_part_dim_part ON PartDimension(part_code);
CREATE INDEX IF NOT EXISTS idx_part_dim_composite ON PartDimension(part_code, key_composite);
CREATE INDEX IF NOT EXISTS idx_part_dim_keys ON PartDimension USING GIN (key_values);
```
