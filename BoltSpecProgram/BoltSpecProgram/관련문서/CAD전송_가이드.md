# CAD 전송 기능 구현 가이드

## 📋 개요

WPF 볼트 사양 선택 프로그램에서 "확인" 버튼을 클릭하면, 선택한 데이터가 Named Pipe를 통해 CAD(AutoCAD/BricsCAD)로 전송됩니다.

---

## 🔧 구현 구조

```
┌─────────────────────┐         Named Pipe          ┌─────────────────────┐
│   WPF Application   │ =========================> │   CAD Application   │
│   (C# Client)       │        JSON Data           │   (C++ Server)      │
│                     │ <========================= │                     │
│   BoltSpecProgram   │        Response            │   ObjectARX/BRX     │
└─────────────────────┘                            └─────────────────────┘
```

### 파일 구조
```
BoltSpecProgram/
├── Services/
│   └── CadPipeClient.cs      ← Named Pipe 클라이언트 (WPF → CAD)
├── MainWindow.xaml.cs         ← 확인 버튼 핸들러
└── ...

CadServer/                      ← CAD 측 예시 코드
├── BoltSpecPipeServer.h       ← C++ ObjectARX 서버
└── TestPipeServer.cs          ← 테스트용 C# 콘솔 서버
```

---

## 📡 통신 프로토콜

### Named Pipe 설정
- **Pipe 이름**: `\\.\pipe\BoltSpecCADPipe`
- **통신 방향**: 양방향 (Duplex)
- **데이터 형식**: JSON (UTF-8)
- **타임아웃**: 5초 (설정 가능)

### 전송 JSON 형식
```json
{
  "Command": "BoltSpec",
  "Data": {
    "종류": "육각머리볼트",
    "타입": "KS",
    "규격(표준번호)": "KS B 1002:2016",
    "용도": "기계용",
    "재질": "S10C",
    "나사종류(Pich)": "보통나사",
    "머리형식(Type)": "기본",
    "사이즈": ["M10", "M12", "M14"],
    "표면처리": "아연도금",
    "볼트끝단": "평면",
    "전체길이": "50",
    "유효길이": "30"
  },
  "Timestamp": "2024-11-24 13:45:30.123"
}
```

### 응답 JSON 형식
```json
{
  "Success": true,
  "Message": "데이터를 성공적으로 수신했습니다.",
  "Data": null
}
```

---

## 💻 WPF 클라이언트 (C#)

### CadPipeClient.cs 주요 메서드

```csharp
/// <summary>
/// 볼트 사양 데이터를 CAD로 전송
/// </summary>
public CadSendResult SendBoltSpec(Dictionary<string, object> selectedValues)
{
    return SendCommand("BoltSpec", selectedValues);
}

/// <summary>
/// CAD 서버가 실행 중인지 확인
/// </summary>
public bool IsServerAvailable()
{
    try
    {
        using (var pipeClient = new NamedPipeClientStream(".", _pipeName, 
            PipeDirection.InOut, PipeOptions.None))
        {
            pipeClient.Connect(500);  // 0.5초 타임아웃
            return pipeClient.IsConnected;
        }
    }
    catch { return false; }
}
```

### Confirm_Click 이벤트 핸들러

```csharp
private void Confirm_Click(object sender, RoutedEventArgs e)
{
    // 1. 선택된 데이터 수집
    var selectedData = _uiManager.GetSelectedValues();
    
    // 2. 종류 정보 추가
    selectedData["종류"] = _data.DataRows[0].CompleteValues["종류"];
    
    // 3. 확인 대화상자
    var confirmResult = MessageBox.Show(
        summary + "\n\n이 사양을 CAD로 전송하시겠습니까?",
        "CAD 전송 확인", MessageBoxButton.YesNo);
    
    if (confirmResult != MessageBoxResult.Yes)
        return;
    
    // 4. CAD로 전송
    var result = _cadPipeClient.SendBoltSpec(selectedData);
    
    if (result.Success)
    {
        MessageBox.Show("CAD로 전송되었습니다.");
    }
    else
    {
        MessageBox.Show($"전송 실패: {result.ErrorMessage}");
    }
}
```

---

## 🖥️ CAD 서버 (C++ ObjectARX)

### BoltSpecPipeServer.h 사용법

```cpp
#include "BoltSpecPipeServer.h"

// 전역 서버 인스턴스
static BoltSpec::BoltSpecPipeServer* g_pPipeServer = nullptr;

// 콜백 함수 - 데이터 수신 시 호출
void OnBoltSpecReceived(const BoltSpec::BoltSpecData& data)
{
    // CAD 명령 실행
    acutPrintf(_T("\n종류: %s"), data.Category.c_str());
    acutPrintf(_T("\n타입: %s"), data.Type.c_str());
    acutPrintf(_T("\n규격: %s"), data.Standard.c_str());
    
    // 사이즈 출력
    for (const auto& size : data.Sizes)
    {
        acutPrintf(_T("\n사이즈: %s"), size.c_str());
    }
    
    // TODO: 실제 볼트 작도 로직
    // DrawBolt(data);
}

// 서버 시작
void StartPipeServer()
{
    if (!g_pPipeServer)
    {
        g_pPipeServer = new BoltSpec::BoltSpecPipeServer();
        g_pPipeServer->SetCallback(OnBoltSpecReceived);
    }
    g_pPipeServer->Start();
}

// 서버 중지
void StopPipeServer()
{
    if (g_pPipeServer)
    {
        g_pPipeServer->Stop();
        delete g_pPipeServer;
        g_pPipeServer = nullptr;
    }
}
```

### ObjectARX 명령어 등록

```cpp
// CAD 명령어: BOLTSPEC_START, BOLTSPEC_STOP
acedRegCmds->addCommand(
    _T("BOLTSPEC_CMDS"),
    _T("BOLTSPEC_START"),
    _T("BOLTSPEC_START"),
    ACRX_CMD_MODAL,
    StartPipeServer
);
```

---

## 🧪 테스트 방법

### 테스트 서버 사용 (CAD 없이 테스트)

1. **테스트 서버 빌드**
   - Visual Studio에서 새 콘솔 프로젝트 생성
   - `CadServer/TestPipeServer.cs` 코드 사용
   - NuGet: Newtonsoft.Json 설치
   - 빌드 및 실행

2. **WPF 클라이언트 테스트**
   - BoltSpecProgram 실행
   - 볼트 사양 선택
   - [확인] 버튼 클릭
   - 테스트 서버 콘솔에서 수신 데이터 확인

### 테스트 서버 출력 예시
```
========================================
  BoltSpec Named Pipe 테스트 서버
========================================
Pipe 이름: \\.\pipe\BoltSpecCADPipe
종료하려면 'q'를 입력하세요.
========================================

[대기] 클라이언트 연결 대기 중...
[연결] 클라이언트가 연결되었습니다.

[수신] JSON 데이터:
----------------------------------------
Command: BoltSpec
Timestamp: 2024-11-24 13:45:30.123

Data:
  종류: 육각머리볼트
  타입: KS
  규격(표준번호): KS B 1002:2016
  용도: 기계용
  재질: S10C
  사이즈: [M10, M12, M14]
----------------------------------------

[응답] 성공 응답 전송됨
[연결] 클라이언트 연결 종료
```

---

## ⚠️ 주의사항

### 1. 실행 순서
```
CAD (서버) 먼저 시작 → WPF (클라이언트) 나중에 실행
```

### 2. 에러 처리

**연결 타임아웃 시:**
```
CAD 연결 타임아웃 (5000ms)
CAD 프로그램이 실행 중인지 확인하세요.
Pipe 이름: BoltSpecCADPipe
```

**서버가 실행되지 않은 경우:**
```
CAD 연결 실패: 파이프의 다른 쪽 끝에 프로세스가 없습니다.
```

### 3. 멀티 인스턴스
- 여러 WPF 클라이언트가 동시 접속 가능
- CAD 서버는 순차적으로 처리

### 4. 한글 인코딩
- JSON은 UTF-8로 인코딩
- C++ 서버에서 UTF-8 → wstring 변환 필요

---

## 🔧 설정 변경

### Pipe 이름 변경

**WPF (MainWindow.xaml.cs):**
```csharp
private const string CAD_PIPE_NAME = "MyCustomPipeName";
```

**C++ (BoltSpecPipeServer.h):**
```cpp
BoltSpecPipeServer(L"\\\\.\\pipe\\MyCustomPipeName")
```

### 타임아웃 변경

**WPF:**
```csharp
_cadPipeClient = new CadPipeClient(CAD_PIPE_NAME, 10000);  // 10초
```

---

## 📊 시퀀스 다이어그램

```
WPF Client                    Named Pipe                    CAD Server
    │                             │                              │
    │  [확인] 버튼 클릭            │                              │
    │──────────────────────────────>                              │
    │                             │                              │
    │  CreateNamedPipeClient()    │                              │
    │  Connect(5000ms)            │                              │
    │─────────────────────────────>│                              │
    │                             │    ConnectNamedPipe()        │
    │                             │<─────────────────────────────│
    │                             │                              │
    │  WriteFile(JSON)            │                              │
    │─────────────────────────────>│                              │
    │                             │    ReadFile()                │
    │                             │─────────────────────────────>│
    │                             │                              │
    │                             │    JSON 파싱                  │
    │                             │    OnBoltSpecReceived()      │
    │                             │                              │
    │                             │    WriteFile(Response)       │
    │                             │<─────────────────────────────│
    │  ReadFile()                 │                              │
    │<─────────────────────────────│                              │
    │                             │                              │
    │  결과 표시                   │                              │
    │──────────────────────────────>                              │
```

---

## 🚀 향후 확장

### 1. 양방향 통신
```csharp
// CAD에서 WPF로 데이터 요청
public BoltSpecData RequestCurrentSpec()
{
    var result = SendCommand("GetCurrentSpec", null);
    return JsonConvert.DeserializeObject<BoltSpecData>(result.Response);
}
```

### 2. 이벤트 기반 통신
```csharp
// WPF에서 CAD 이벤트 수신
_cadPipeClient.OnMessageReceived += (sender, message) =>
{
    // CAD에서 보낸 메시지 처리
    UpdateUI(message);
};
```

### 3. 다중 명령 지원
```csharp
// 다양한 CAD 명령 전송
_cadPipeClient.SendCommand("DrawBolt", boltData);
_cadPipeClient.SendCommand("DeleteBolt", boltId);
_cadPipeClient.SendCommand("ModifyBolt", modifyData);
```

---

## 📁 파일 목록

| 파일 | 설명 |
|------|------|
| `Services/CadPipeClient.cs` | WPF Named Pipe 클라이언트 |
| `MainWindow.xaml.cs` | 확인 버튼 핸들러 |
| `CadServer/BoltSpecPipeServer.h` | C++ ObjectARX 서버 |
| `CadServer/TestPipeServer.cs` | 테스트용 C# 콘솔 서버 |

---

## ✅ 체크리스트

### WPF 클라이언트
- [x] Named Pipe 클라이언트 클래스 구현
- [x] 연결 타임아웃 처리
- [x] JSON 직렬화
- [x] 응답 파싱
- [x] 에러 처리 및 사용자 알림

### CAD 서버
- [x] Named Pipe 서버 클래스 (C++)
- [x] JSON 파싱 (JsonCpp)
- [x] 콜백 메커니즘
- [x] 응답 전송

### 테스트
- [x] 테스트용 콘솔 서버
- [x] 연결 테스트
- [x] 데이터 전송 테스트

---

**문의사항이 있으시면 말씀해주세요!**
