# BoltSpecProgram & PartSpecViewer 업데이트 내역

## ⭐ 새로운 테이블 구조 (part_type 추가)

```sql
CREATE TABLE IF NOT EXISTS PartSpec (
    id          INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    part_type   TEXT NOT NULL,           -- ⭐ 분류 (볼트류, 너트류, 와셔류)
    part_code   TEXT NOT NULL UNIQUE,    -- HBOLT
    part_name   TEXT NOT NULL,           -- 육각머리볼트
    spec_data   TEXT,                    -- JSON 데이터
    is_active   BOOLEAN DEFAULT TRUE
);
```

---

## BoltSpecProgram 변경 사항

### 1. DatabaseService.cs
- **테이블 생성**: `part_type` 컬럼 포함
- **기존 테이블 마이그레이션**: `part_type` 컬럼이 없으면 자동 추가
- **SavePartSpecAsync**: `partType` 파라미터 추가

### 2. MainWindow.xaml.cs
- **SaveToDatabase_Click**: 현재 선택된 분류(SelectedClassification)를 `part_type`으로 저장
- **SaveAllToDatabase_Click**: 각 종류별로 해당 분류를 `part_type`으로 저장

### 저장 예시
| 필드 | 값 | 설명 |
|------|-----|------|
| part_type | 볼트류 | 엑셀 분류 컬럼 |
| part_code | HBOLT | CMD 코드 |
| part_name | 육각머리볼트 | 종류 이름 |

---

## PartSpecViewer 변경 사항

### 1. Models/PartSpecModels.cs
- `PartSpecRecord` 클래스에 `PartType` 속성 추가

### 2. Services/DatabaseService.cs
- `GetPartSpecByCodeAsync`: `part_type` 컬럼 조회
- `GetAllPartSpecsAsync`: `part_type` 기준 정렬

### 3. MainWindow.xaml.cs
- Part 콤보박스에 분류 표시: `[볼트류] HBOLT - 육각머리볼트`

---

## 사용 방법

### BoltSpecProgram - DB 저장
1. Excel 파일 열기
2. 분류/종류 선택
3. `파일 → DB에 저장 (현재 선택)` 또는 `파일 → 전체 DB 저장 (자동)`

저장 확인 대화상자:
```
Part Type (분류): 볼트류
Part Code: HBOLT
Part Name: 육각머리볼트
```

### PartSpecViewer - DB 조회
1. 프로그램 실행
2. Part Code 콤보박스에서 선택: `[볼트류] HBOLT - 육각머리볼트`
3. 해당 Part의 UI 자동 생성

---

## 기존 테이블 마이그레이션

기존 테이블에 `part_type` 컬럼이 없으면 프로그램이 자동으로 추가합니다:
```sql
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'partspec' AND column_name = 'part_type'
    ) THEN
        ALTER TABLE PartSpec ADD COLUMN part_type TEXT NOT NULL DEFAULT '';
    END IF;
END $$;
```

---

## DB 연결 설정

`App.config`:
```xml
<connectionStrings>
    <add name="PostgresConnection" 
         connectionString="Host=192.168.0.17;Port=5432;Database=Standard_Core;Username=clickinfo;Password=info6130!!" 
         providerName="Npgsql" />
</connectionStrings>
```
