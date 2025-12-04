# BoltSpecProgram 업데이트 내역

## 변경 사항 요약

### 1. 분류(Classification) 최상위 관리
- **이전**: Headers[1] (종류)가 최상위
- **변경 후**: Headers[0] (분류)가 최상위, 종류는 그 하위
- UI에 분류 콤보박스 추가 (종류 콤보박스 앞에 표시)

### 2. Name_Code Sheet 처리 - CMD 필드 추가
- Excel의 `Name_Code` sheet를 읽어 종류 이름과 CMD 코드 매핑
- JSON 내보내기 시 각 Series에 `"CMD"` 필드 자동 추가

**예시:**
```json
{
  "Classification": [
    {
      "name": "볼트류",
      "id": 1,
      "Series": [
        {
          "name": "육각머리볼트",
          "CMD": "HBOLT",    // ← 추가됨
          "id": 1,
          "option": [...]
        }
      ]
    }
  ]
}
```

### 3. PostgreSQL DB 저장 기능
- 메뉴: `파일 → DB에 저장`
- 현재 선택된 종류의 JSON 데이터를 PartSpec 테이블에 저장
- UPSERT 방식 (있으면 업데이트, 없으면 삽입)

**테이블 구조:**
```sql
CREATE TABLE IF NOT EXISTS PartSpec (
    id          INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    part_code   TEXT NOT NULL UNIQUE,    -- 예: HBOLT
    part_name   TEXT NOT NULL,           -- 예: 육각머리볼트
    spec_data   TEXT,                    -- JSON 데이터
    is_active   BOOLEAN DEFAULT TRUE
);
```

---

## 수정된 파일 목록

| 파일 | 변경 내용 |
|------|----------|
| `BoltSpecData.cs` | `NameCodeInfo` 클래스 및 `NameCodeMap` 추가 |
| `ExcelReader.cs` | `Name_Code` sheet 읽기 기능 추가 |
| `JsonExporter.cs` | Classification 최상위 구조, CMD 필드 추가, DB용 메서드 추가 |
| `DynamicUIManager.cs` | 분류 콤보박스 추가, 종류와 연동 |
| `MainWindow.xaml` | DB 저장 메뉴 추가 |
| `MainWindow.xaml.cs` | `SaveToDatabase_Click` 메서드 추가 |
| `App.config` | PostgreSQL 연결 문자열 추가 |

---

## 사용 방법

### JSON 내보내기
1. Excel 파일 열기
2. 분류 선택 (예: 볼트류)
3. 종류 선택 (예: 육각머리볼트)
4. `파일 → JSON으로 내보내기`

### DB 저장
1. Excel 파일 열기
2. 종류 선택
3. `파일 → DB에 저장`
4. 확인 대화상자에서 저장 정보 확인 후 저장

### DB 연결 설정
`App.config`에서 연결 문자열 수정:
```xml
<connectionStrings>
    <add name="PostgresConnection" 
         connectionString="Host=localhost;Port=5432;Database=Standard_Spec;Username=postgres;Password=postgres" 
         providerName="Npgsql" />
</connectionStrings>
```

---

## Name_Code Sheet 형식

| Name_Code | NAME | ENAME |
|-----------|------|-------|
| HBOLT | 육각머리볼트 | Hexa Head Bolt |
| HSBOLT | 육각구멍붙이볼트 | Hexa Socket Head Bolt |
| SQBOLT | 사각볼트 | Square Bolt |
| ... | ... | ... |

- `Name_Code`: CMD 코드
- `NAME`: 종류 이름 (규격정리 sheet의 종류 컬럼과 매칭)
- `ENAME`: 영문 이름 (선택사항)
