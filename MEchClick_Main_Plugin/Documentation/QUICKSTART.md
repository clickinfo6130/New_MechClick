# PartManager - 빠른 시작 가이드

## 🚀 설치 및 실행

### 1. 사전 요구사항

- Windows 10/11 (64-bit)
- AutoCAD 2022 이상
- Visual Studio 2019/2022 (빌드 시)

### 2. 빌드 방법

```bash
# 1. Visual Studio에서 솔루션 열기
PartManager.sln

# 2. NuGet 패키지 복원
도구 → NuGet 패키지 관리자 → 솔루션용 NuGet 패키지 관리
→ 복원 클릭

# 3. 빌드 구성 선택
구성: Debug 또는 Release
플랫폼: x64

# 4. 빌드
Ctrl + Shift + B
```

### 3. AutoCAD에서 로드

```
1. AutoCAD 시작
2. 명령줄에 NETLOAD 입력
3. PartManager.dll 선택
4. "Part Manager Plugin 로드 완료" 메시지 확인
```

---

## ⌨️ 기본 사용법

### 명령어 목록

| 명령어 | 단축 | 설명 |
|--------|------|------|
| `SHOWUI` | - | 부품 선택 패널 열기 |
| `HIDEUI` | - | 패널 닫기 |
| `TOGGLEUI` | - | 패널 열기/닫기 전환 |
| `DOCKUI` | - | 도킹 위치 설정 |

### 패널 도킹

```
> DOCKUI
→ L: 왼쪽 도킹
→ R: 오른쪽 도킹
→ F: 플로팅 (떠 있는 창)
```

---

## 🎯 부품 선택 방법

### 표준부품 선택 (4단계)

```
1단계: VENDOR → 표준부품 클릭
2단계: CATEGORY → 체결류 클릭
3단계: 체결류 → 볼트 클릭
4단계: 볼트 → 육각머리볼트 클릭
5단계: [부품 선택 →] 버튼 클릭
```

### 상용부품 선택 (3~4단계)

```
1단계: VENDOR → 실린더 클릭
2단계: VENDOR → SMC 클릭
3단계: SMC → 표준실린더 클릭
4단계: 표준실린더 → CJ1 클릭 (시리즈가 있는 경우)
5단계: [부품 선택 →] 버튼 클릭
```

---

## 📐 반응형 UI

패널 너비에 따라 자동으로 레이아웃이 변경됩니다:

| 너비 | 모드 | 특징 |
|------|------|------|
| < 650px | Accordion | 세로 스택, 접히는 패널 |
| ≥ 650px | Grid | 4열 가로 배치 |

### Accordion 모드 (좁은 화면)

- 선택한 항목은 자동으로 접힘
- 헤더에 선택된 값이 Badge로 표시
- 헤더 클릭으로 수동 펼치기/접기 가능

### Grid 모드 (넓은 화면)

- 4개 열이 동시에 표시
- 모든 카테고리를 한눈에 확인 가능

---

## 🔧 문제 해결

### 플러그인이 로드되지 않음

```
원인: AutoCAD 버전 불일치
해결: PartManager.csproj에서 AutoCAD DLL 경로 수정
     C:\Program Files\Autodesk\AutoCAD 20XX\
```

### 데이터가 표시되지 않음

```
원인: DB 파일 누락
해결: bin\Debug\ 폴더에 다음 파일 확인
     - Database\standard_core.db
     - Database\vendors\vendor_*.db
```

### IPC 연결 실패

```
원인: C++ ARX 서버가 실행되지 않음
확인: IPCSTATUS 명령으로 상태 확인
해결: IPCRECONNECT 명령으로 재연결 시도
```

---

## 📁 프로젝트 구조 요약

```
PartManager/
├── PluginLoader.cs        # 진입점
├── Commands/              # CAD 명령어
├── Data/                  # DB 접근
├── Database/              # SQLite 파일
├── IPC/                   # Named Pipe
├── UI/                    # 팔레트 관리
└── Views/                 # WPF UI
```

---

## 📞 추가 지원

- 상세 문서: `Documentation/README.md`
- 설계 문서: `Documentation/DESIGN.md`
