# 런타임 로그 — GameLog / sink / RuntimeLogger 패턴

규칙 단일 소스: `Assets/_Project/Docs/LogRules.md`. **충돌 시 언제나 그 문서가 옳다.**

## 현재 구조 (2026-08-17 실측)

| 파일 | 레이어 | 역할 |
|---|---|---|
| `Application/GameLog.cs` | Application | 정적 facade. 중첩 클래스 `Ops`(운영) / `Dev`(개발)로 축 B가 갈림 |
| `Application/Interfaces/ILogSink.cs` | Application | `ILogSink` + Application 중립 `LogLevel` + `enum LogEvent`(멤버 32개) |
| `Infrastructure/Debug/ConsoleSink.cs` | Infrastructure | 콘솔 출력 |
| `Infrastructure/Debug/FileSink.cs` | Infrastructure | 파일 기록. 동작 **전체가 `#if UNITY_EDITOR`** |
| `Infrastructure/Debug/RuntimeLogger.cs` | Infrastructure | 실제 파일 쓰기 구현 + 축 B의 `임시` 로그용 직접 호출 대상 |
| `Bootstrap/GameBootstrapper.Setup.cs` | Bootstrap | `InitializeLogging()` / `ShutdownLogging()` — **유일한 sink 등록·세션 소유자** |

- **`IRuntimeLogSink` / `RuntimeLoggerSink` 는 삭제되었다**(2026-08-05). 존재하는 것처럼 쓰지 말 것.
- sink 등록은 **에디터=`FileSink` 하나 / 빌드=`ConsoleSink` 하나**. `RuntimeLogger.Log` 가 파일+콘솔 동시 출력이라 둘 다 등록하면 콘솔 이중 출력.

## API

- `GameLog.Ops.Info/Warn/Error(LogEvent, system, className, message, [exception], data=null)` — 릴리스에도 남음. **`LogEvent` 키 필수.**
- `GameLog.Dev.Info/Warn/Error(system, className, message, [exception], data=null)` — `[Conditional("UNITY_EDITOR")]` + `[Conditional("DEVELOPMENT_BUILD")]` 로 릴리스에서 통째로 사라짐. 키 없음.
- `RuntimeLogger.Log(LogLevel, system, className, message, data=null)` — 축 B `임시` 로그만 직접 호출.

## `RuntimeLogger.BeginSession` 시그니처 (2026-08-17 변경)

```csharp
RuntimeLogger.BeginSession(string folderPath, string purpose)
```

- **`role` 인자 제거됨.** 파일명은 **`RuntimeLog.txt` 단일** — `RuntimeLog_host.txt` / `_client.txt` 는 폐지.
  이유: `FileSink` 동작 전체가 `#if UNITY_EDITOR` 안 → 빌드는 파일을 아예 안 씀 → 파일 쓰는 프로세스는 항상 에디터 1개 → 충돌 불가.
  (이 프로젝트 테스트 구성은 **에디터 1 + 실기기 빌드 1이고 어느 쪽이 host 일지 정해져 있지 않다.** "에디터=host" 전제는 틀린 것으로 확정.)
- `purpose` 는 파일 헤더 1줄째 `=== {purpose} ===` 에 들어감(LogRules 1.4). 빈 값이면 `"런타임 로그"` 로 폴백.
- `FileSink` 는 `SessionPurpose = "에디터 상시 런타임 로그"` 상수를 넘긴다. 진단용 임시 세션은 그때의 작업명.
- 헤더 = 목적 줄 + `=== 세션 시작: yyyy-MM-dd HH:mm:ss ===` + **빈 줄 1줄**(본문 경계).
- `FileSink.BeginSession()` 도 무인자. `FileSink.RoleHost/RoleClient` 상수 **제거됨.**

## 세션 배선 (현행)

- **`GameBootstrapper.InitializeLogging()`(Awake)** 이 `_logFileSink.BeginSession()` 으로 열고,
  `ShutdownLogging()`(OnDestroy)이 `EndSession()` 으로 닫는다. **여닫는 주체는 이 클래스뿐.**
- `FileSink._sessionOwned` 소유권 가드 유지 — 과거 세션을 남이 닫아 로그가 통째로 유실된 사고 있음. **건드리지 말 것.**
- 파일 경로: `Assets/_Project/Docs/_Logs/_editor/{yyyy-MM-dd}/RuntimeLog.txt` — **일 단위 이어쓰기**, 플레이 모드 재진입 시 헤더가 실행 구분선이 됨.
  (작업 귀속 영구 보존물인 `_Logs/YYYY-MM-DD/HH_MM_작업명/` 과 다른 트리다.)
- ~~`NetworkCombatController.OnNetworkSpawn` 에서 역할별 세션을 연다~~ → 폐기. 그 자리에는 **로그 한 줄만** 남는다:
  `GameLog.Dev.Info("Network", nameof(NetworkCombatController), "네트워크 역할 확정", $"Role={(IsServer ? "host" : "client")}")`
  (역할은 `key=value` 여야 서버 전송 시 집계 필드가 된다. 파일명은 집계 축이 못 된다 — LogRules 1.4)

## 형식

`[HH:MM:SS.ms] [LEVEL] [System/Class] 메시지 | key=value, key=value`
- `key=value` 는 **서버 전송 시 그대로 구조화 필드**. 값은 집계 가능한 형태로, 키 이름은 고정. 자유 문장은 message 쪽.
- 민감 데이터: 이메일 **전면 금지**(부분 마스킹도 금지), UID/PlayerId 는 `GameLog.HashId`, 토큰 금지. 에디터 포함 항상 적용.
- 호출부 마스킹(1.6 1단) **미적용 15곳** — 코드에 `⚠️ 5단계(마스킹) 대상` 주석이 붙어 있어 grep 으로 찾을 수 있다.

## 네임스페이스 함정 (반복 발생, CS0234 3건 이력)

- `Hexiege.Application` 네임스페이스가 존재 → 수식 없는 `Application` 은 **`UnityEngine.Application` 이 아니다.**
  `UnityEngine.Application.dataPath` / `.logMessageReceived` 는 **반드시 완전 수식.**
- `LogLevel` 이 `Hexiege.Application` · `Hexiege.Infrastructure` 양쪽 존재.
  enclosing 네임스페이스 멤버가 `using` 보다 우선하므로 CS0104 는 안 나지만,
  **인터페이스 구현 시그니처는 `Hexiege.Application.LogLevel` 로 완전 수식해야** 구현으로 인정된다.

## 미구현 / 남은 것

- `Hexiege > Logcat > 오래된 에디터 로그 정리` 수동 메뉴 — 미구현. 현행 `Assets/Editor/Tools/LogcatCapture.cs` 는 `버퍼 비우기` / `파일로 저장` 2개뿐.
- 네트워크·인증 8파일 205건은 `GameLog` 이관 완료. **나머지 계층은 미이관**(`Debug.Log` 계열 잔존 234건).
  단 `Application/GameLog.cs` 의 9건은 sink 폴백 구현이라 이관 대상 아님.
