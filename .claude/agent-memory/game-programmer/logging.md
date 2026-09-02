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
| `Infrastructure/Debug/LogSessionOwner.cs` | Infrastructure | **유일한 sink 등록·세션·전역 예외 훅 소유자 (전부 `static`)**. 2026-08-17 신설 |

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

## 세션 배선 (2026-08-17 전면 개편 — 씬 무관 로그 수집)

> 개편 전: `GameBootstrapper.InitializeLogging()`(Awake) 이 열고 `ShutdownLogging()`(OnDestroy) 이 닫았다.
> `GameBootstrapper` 가 **Game.unity 에만** 있어 로그인·로비 로그 73건이 파일에 안 남던 것이 개편 이유.

- **여는 쪽은 여럿, 닫는 쪽은 하나.**
  - `LogSessionOwner.EnsureInitialized()` — **public 유일 진입점.** 각 씬 부트스트래퍼 `Awake()` **가장 앞줄**에서 호출.
    현재 호출처 2곳: `LoginBootstrapper.Awake` / `GameBootstrapper.Awake`.
  - `LogSessionOwner.Shutdown()` — **private.** 외부에서 못 닫게 막아 "씬 전환 때 닫는 코드"가 재발하지 않게 한다.
    `UnityEngine.Application.quitting` + (에디터) `EditorApplication.playModeStateChanged`의 `EnteredEditMode` 에 자체 배선.
  - `LogSessionOwner.IsInitialized` — 검증용 읽기 전용.
- **⚠️ 상태를 `static` 으로 모은 것이 이 설계의 본질.** sink / `_initialized` / 훅 중복방지 캐시 전부 static.
  인스턴스 필드였을 때는 씬이 바뀌면 인스턴스가 새로 생겨 "이미 열었는가?" 판단이 **씬 경계를 못 건넜다.**
  → `GameLog.ClearSinks()` 로 앞 sink 가 빠지고 `RuntimeLogger.BeginSession`(진입 즉시 `EndSession()` 호출)이 앞 스트림을 닫아
  **2026-08-10 사고(헤더만 남고 본문 빈 파일)가 그대로 재현**된다.
- **`GameLog.ClearSinks()` 는 반드시 멱등 가드 *뒤*에.** 가드 앞에 두면 씬마다 sink 목록이 비워져 로그 유실.
- `_initialized = true` 는 **실제 초기화 작업 전에** 세운다(초기화 중 예외/로그로 재진입해도 이중 열기 방지).
- `FileSink._sessionOwned` 소유권 가드 유지 — 과거 세션을 남이 닫아 로그가 통째로 유실된 사고 있음. **건드리지 말 것.**
  정적 가드는 그 위에 얹는 한 겹이지 대체가 아니다.
- **`GameBootstrapper.OnDestroy` 에서 로그를 닫지 않는다.** 닫으면 Game 씬 이탈만으로 Login 부터 이어 온 파일이 끊긴다.
  (2026-08-17 현재 `// ShutdownLogging();` 로 주석 처리됨 — 사용자 테스트 통과 후 삭제 예정.)
- 클래스명을 `LogSessionBootstrap`(계획서 가칭) 이 아니라 **`LogSessionOwner`** 로 한 이유:
  `~Bootstrap` 은 `Hexiege.Bootstrap` 레이어의 조합 루트를 연상시켜 "세 번째 부트스트래퍼" 로 오독된다.
  이 클래스는 남에게 의존성을 주입하지 않고 자기 sink 만 소유한다.
- 파일 경로: `Assets/_Project/Docs/_Logs/_editor/{yyyy-MM-dd}/RuntimeLog.txt` — **일 단위 이어쓰기**, 플레이 모드 재진입 시 헤더가 실행 구분선이 됨.
  (작업 귀속 영구 보존물인 `_Logs/YYYY-MM-DD/HH_MM_작업명/` 과 다른 트리다.)
- ~~`NetworkCombatController.OnNetworkSpawn` 에서 역할별 세션을 연다~~ → 폐기. 그 자리에는 **로그 한 줄만** 남는다:
  `GameLog.Dev.Info("Network", nameof(NetworkCombatController), "네트워크 역할 확정", $"Role={(IsServer ? "host" : "client")}")`
  (역할은 `key=value` 여야 서버 전송 시 집계 필드가 된다. 파일명은 집계 축이 못 된다 — LogRules 1.4)

## 전역 로그 훅 — 수집 범위 확대 (2026-08-18, `LogSessionOwner.OnUnityLogMessageReceived`)

**훅은 이제 `LogType.Exception` **과** `LogType.Error` 를 함께 수집한다.** (`Assert`·`Warning`·`Log` 는 제외)

- 옛 서술("Exception 만 수집")은 폐기. 배제 근거였던 *"우리 raw `Debug.LogError` 를 이중 집계한다"* 는
  386건 이관 완료로 **전제가 소멸**했다(런타임 raw 호출 0건). 되돌리면 엔진·NGO 오류가 다시 전부 유실된다.
- **방어 4겹**(순서 그대로):
  ① 타입 필터(`Exception || Error`) → ② **`RuntimeLogger.IsEmittingToConsole`(신설)** →
  ③ `GameLog.IsEmitting` → ④ 직전 `condition`+`stackTrace` 동일 무시
- ⚠️ **방어 ④ 는 우리 자신의 출력을 못 잡는다.** `RuntimeLogger.cs` 가 만드는 줄에 `[HH:mm:ss.fff]` 가
  들어가 메아리마다 문자열이 달라지기 때문. 그래서 ②(플래그)가 실질적인 자기출력 차단막이다.
- `RuntimeLogger._isEmittingToConsole` 는 **콘솔 `switch` 구간만** `try/finally` 로 감싼다.
  파일 쓰기는 훅을 발화시키지 않으므로 감싸지 않는다. **`finally` 누락 시 훅이 영구히 죽는다.**
- **스로틀**: 같은 `condition` 1초 1건. 억제분은 다음 통과 줄에 `Suppressed=n`.
  `Dictionary` **상한 32개, 초과 시 `Clear()`**(LRU 아님 — 훑는 비용을 피하려고). 시계는 `Stopwatch`(단조 증가).
  `Shutdown()` 에서 표도 비운다.
- ⚠️ `Suppressed=` 는 **스로틀이 버린 횟수만** 센다. 방어 ④가 먼저 버린 줄은 세지 않는다 → 발생 횟수의 하한.
- 키 분기: `Exception` → `UnhandledException` / `Error` → **`UnhandledEngineError`(신설, 36→37)**.
  `LogType=` 같은 잉여 필드는 두지 않는다. `Source=UnityLogHook` 유지.

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

## 이관 진행 상황

계획서: `Assets/_Project/Docs/_Tasks/2026-08-17/17_19_remaining-layers-log-migration/Plan.md`
판정 선례 982줄: `_Tasks/2026-08-13/07_13_network-auth-log-cleanup/LogAudit.md` — **애매하면 여기서 유사 사례를 먼저 찾는다.**

| 배치 | 대상 | 상태 |
|:-:|---|---|
| 선행 | 네트워크·인증 8파일 205건 (개발 120 / 운영 85) | 완료 |
| **1-A** | `Network` 상위 6파일 65건 (`NetworkHealthSync` 14 · `RelayManager` 13 · `NetworkCombatController` 11 · `NetworkGameFlow` 10 · `NetworkUnitMovementController` 9 · `NetworkTileSync` 8) | **완료 — 개발 35 / 운영 29 / 비활성화 1** |
| **1-B** | `Network` 나머지 8파일 30건 | **완료 — 실제 이관 대상은 23건뿐** (개발 17 / 운영 6). 나머지 7건은 **주석 안의 죽은 코드**였다: `NetworkGameManager` 4건은 `/* */` 블록 주석(666~718행) 안, `UnityServicesInitializer` 3건은 `//` 로 비활성화된 구 로직 2건 + 산문 속 `Debug.LogException` 낱말 1건. **grep 건수 ≠ 이관 대상 건수** — 착수 전 반드시 주석 여부를 확인한다 |
| **2** | `Bootstrap` 26 + `Application` 9 + `Cloud` 8 + `Factories` 7 | **완료 — 이관 50 (개발 42 / 운영 8) + 신규 추가 3(전부 운영). 신설 키 4개(32→36)** |
| **3** | `Presentation` 39건 / 20파일 | **완료 — 개발 38 / 운영 1. 신설 키 0개** |
| **4** | `Debug/UIManagerTestButtonHandler.cs` 4건 | **완료 — 전부 개발** |
| — | `Editor/` 4파일 16건 | **이관하지 않기로 결정**(빌드 미포함 → 서버 수집 대상이 될 수 없음) |
| — | `GameLog.cs` 9 · `ILogSink.cs` 2 · `Infrastructure/Debug/` 19 | 이관 대상 아님(로그 시스템 자체) |

- `Hexiege > Logcat > 3. 오래된 에디터 로그 정리` 수동 메뉴는 **구현·실기 검증 완료**(커밋 `675203ae`).
  ~~"미구현"~~ 이라는 옛 기록은 폐기.

## 이관 시 재사용하는 판정 선례 (배치 1-A 에서 확인)

같은 사건은 **반드시 같은 키**를 쓴다. 새 키를 만들기 전에 이 표를 먼저 본다.

| 코드 패턴 | 축 A | 축 B | 키 |
|---|:-:|:-:|---|
| `OnNetworkSpawn` 에서 `GameServicesLocator.Current == null` (스폰은 계속) | Warn | 운영 | `NetworkControllerSpawnedWithoutGameServices` |
| 같은 상황인데 **즉시 return** 해 기능이 죽음 (`NetworkGameFlow`) | **Error** | 운영 | 위와 같은 키 (사건은 같고 축 A 만 다름) |
| ServerRpc 안에서 `_services`/UseCase 가 null | Error | 운영 | `ServerRpcGameServicesMissing` |
| ClientRpc 안에서 `_services` 가 null (사망·타일 등 1회성 이벤트) | Error | 운영 | `ClientRpcGameServicesMissing` |
| ClientRpc 안에서 UseCase 가 null + **"맵 로드 전일 수 있음"** (다음 이벤트가 재동기화) | **Warn** | 운영 | 위와 같은 키 |
| 서버가 팀·소유권 불일치로 요청 거부 | Warn | 운영 | `ServerRejectedUnauthorizedRequest` (+`Reason=Ownership`) |
| 서버에 요청 대상이 없음/이미 사망 | Warn | 운영 | `ServerRejectedTargetNotFound` |
| Relay 할당·참가·JoinCode 실패 (`catch` 로 예외를 쥔 자리) | Error | 운영 | `RelaySetupFailed` (+`Stage=Allocate\|Join\|CodeMissing`) |
| `NetworkManager.Singleton == null` | Error | 운영 | `NetworkManagerSingletonMissing` |
| 호출부 계약 위반(인자가 빈 문자열) | Error | 운영 | 그 흐름의 기존 키 재사용 (선례: `UgsBridgeMissingFirebaseUid`) |
| 스폰/디스폰/구독완료/RPC 수신 덤프/성공 통보 | Info | **개발** | — |
| Inspector·프리팹 **설정 오류** (컴포넌트 미부착 등) | **Warn** | **개발** | — (LogRules 1.3 원칙 3 단서) |
| 코드 버그로만 도달하는 분기(타입 불일치 등) | Warn | **개발** | — (축 B ① 이 "아니오") |
| 클라이언트가 **거부 알림 ClientRpc** 를 수신 (`Reason=` 문자열 보유) | Warn | **개발** | — (선례: `NetworkProductionController.EnqueueFailedClientRpc` · `NetworkUpgradeController.ResearchFailedClientRpc`) |
| **씬에 배치돼야 할 오브젝트**를 `FindFirstObjectByType` 으로 못 찾음 | **Warn** | **개발** | — (설정 오류 단서. **네트워크 스폰 순서 문제가 아니다** — `FindFirstObjectByType` 은 스폰 여부와 무관하게 씬의 활성 오브젝트를 찾으므로 null = "씬에 없다") |
| 클라이언트가 서버 상태를 **1회성으로 반영**하는 자리에서 `_services`/factory 가 null (재시도 없음 → 영구 누락) | **Error** | 운영 | `ClientRpcGameServicesMissing` (문자 그대로의 ClientRpc 가 아니어도 **사건이 같으면 같은 키**) |
| 클라이언트 골드/HP 를 서버 값으로 재보정 | Info | **개발** | — |
| **Resources 에셋 누락**(`Resources.Load` 가 null) | Warn | **개발** | — (Inspector 배선 누락과 같은 "설정 오류". 있으면 모든 기기에 있고 없으면 모든 기기에 없다 → 축 B ① 이 항상 "아니오") |
| 부트스트래퍼가 **하위 계층 실패의 결과(bool/enum)만** 받아 남기는 로그 | 원본 레벨 유지 | **개발** | — (원인 계층이 이미 운영. `Dev.Error(…, e)` 는 예외 오버로드가 **있다**) |
| UGS Cloud Save 호출 실패 (Load/Save) | Warn | 운영 | `CloudSaveOperationFailed` (+`Operation=LoadProfile\|SaveNickname`) |
| Cloud Save 값 타입 변환 실패 → 폴백 | Warn | 운영 | `CloudSaveValueParseFailed` (+`Key=`, `Type=`, `Fallback=`) |
| UGS Leaderboards 조회 실패 → 빈 목록 | Warn | 운영 | `LeaderboardQueryFailed` |
| 랭킹 Metadata(JSON) 파싱 실패 → 칸 비움 | Warn | 운영 | `LeaderboardMetadataParseFailed` |
| "아직 등재 안 됨"이 예외로 오는 자리(신규 유저 정상 경로) | Info | **개발** | — (운영으로 올리면 신규 유저 트래픽이 지표를 덮는다) |

| **UI 가 하위 계층의 예외를 `catch` 해 화면에 안내만 하는 자리** (닉네임 저장/변경 등) | 원본 레벨 유지 | **개발** | — (원인 계층이 이미 운영. 예외 객체를 넘기려면 `Dev.Error(…, e, …)`) |
| **UI 가 NGM `OnError`/`OnServerDisconnected` 같은 *가공된 통지*를 받는 자리** | Warn/Info | **개발** | — (예외 타입도 원인도 도달하지 않음 → 최종 처리 지점 아님) |
| **UI 가 `FindFirstObjectByType` 으로 `NetworkGameManager` 를 못 찾음** | **Warn** | **개발** | — (원본이 `LogError` 여도 설정 오류로 하향. `LobbyUI`·`LobbyRootView`·`GameEndUI` 3곳 동일 판정) |
| **Inspector/Resources/의존성 주입 미배선 전반** (`CanvasGroup`, `AudioMixerGroup`, UseCase 미주입, 프리팹 슬롯, `Resources.Load` null) | **Warn** | **개발** | — (원본이 `LogError` 여도 하향. 1.3 원칙 3 단서) |
| **`AudioMixer.SetFloat` 이 false 반환** (Exposed Parameter 이름 불일치) | Warn | **개발** | — (에셋 설정 오류. 배선 누락과 같은 부류) |
| **클라이언트 사전 검증에서 골드 부족으로 요청을 안 보냄** | Warn | **개발** | — (1.2 조합표가 이미 판정. 서버가 거부한 경우만 운영) |
| **`UIManager.Instance == null`** (씬 직접 진입) | Warn | **개발** | — (플레이어 빌드는 항상 Login 부터 시작 → 축 B ① 이 "아니오") |
| **UI 의 로그아웃 `catch`** (`ProfileView.OnLogoutClicked`) | Warn | **운영** | `FirebaseAuthOperationFailed` (+`Operation=SignOut`) — **Presentation 유일의 운영 건**. 아래 참조 |

**축 A 승격/하향 판단의 실제 기준:** *"다음 이벤트가 다시 맞춰 주는가."*
HP·골드처럼 **절대값을 계속 재동기화**하는 값은 Warn, 사망·타일 소유권처럼 **그때 한 번만 오는 이벤트**는 Error.

## `key=value` 표기 규약 (실측 기준 — 새로 만들지 말고 이걸 따를 것)

`Request=` `Reason=` `ClientId=` `Team=` `ExpectedTeam=` `RequestedTeam=` `BuildingTeam=`
`UnitId=` `BuildingId=` `BarracksId=` `UnitType=` `BuildingType=` `Q=` `R=` `TargetQ=` `TargetR=`
`IsServer=` `Role=Host|Client`(대문자 고정) `Stage=` `Flow=` `MatchId=` `LobbyId=` `LobbyCode=` `RelayJoinCode=`
`Uid=`/`PlayerId=`(반드시 `GameLog.HashId`) `Attempt=` `MaxRetries=`

- 배치 1-A 에서 새로 도입: `AppliedDamage=` `AppliedHeal=` `Hp=` `PathLength=` `EntityId=` `ReadyCount=` `BlueGold=` `RedGold=` `UnitTeam=`
- 배치 1-B 에서 새로 도입(**전부 `개발` 로그 전용** — 서버 집계에 올라가지 않는다): `WaitSeconds=` `WinnerTeam=` `PreviousGold=` `ServerGold=` `Diff=`
- 연구(Upgrade) 흐름에서 도입(2026-08-18): `Group=`(`UpgradeGroup` enum 이름) · `Stat=`(`UnitUpgradeStat` enum 이름) ·
  `Level=`(정수) · `TotalSeconds=`(**정수** — `Mathf.RoundToInt`) · `Suppressed=`(정수, 훅 전용)
- ⚠️ **`float` 를 값에 그대로 넣지 않는다.** 문화권에 따라 소수 구분자가 `,` 가 되어 표기가 기기마다 갈린다. 정수로 고정하거나 `:F2` 로 못 박는다.
- ⚠️ **구조체를 통째로 값에 넣지 않는다.** `HexCoord.ToString()` 은 `(4, 15)` 라 구분자 `, ` 가 값 안에 들어간다.
  → **`Q=` / `R=` 로 쪼갠다.** (`NetworkProductionController:286` 의 `Pos={unit.Position}` 위반을 2026-08-18 에 이렇게 고쳤다.)
- `Reason=` 값 표기 고정: 팀 불일치는 **`Reason=Team`**(`TeamMismatch` 아님), 소유권은 `Reason=Ownership`
- **값 표기까지 고정**한다. `Role=host` / `Role=Host` 가 갈려 한 지표가 둘로 쪼개진 사고 이력 있음(커밋 `73574a23`).
- Relay Join Code · Lobby Code · ticketId 는 **민감 데이터 아님**(LogRules 1.6 규정 3항목에 없음) — 평문 유지가 선례.

## `RelaySetupFailed` 중복 집계 — 배치 1-B 에서 해소 (2026-08-18)

`LogAudit.md` §4-3 **Q-2** 의 잠정 판정("`NetworkGameManager` 호출부를 `운영` 으로 유지")은
*"원인을 쥔 `RelayManager` 가 그 task 범위 밖이라 이관되지 않는다"* 는 **전제 하나**에만 기대고 있었다.
배치 1-A 에서 `RelayManager` 가 이관되며 그 전제가 소멸 → 1-B 에서 호출부 2곳을 `GameLog.Dev.Error` 로 내렸다.

**⚠️ `RelaySetupFailed` 를 쓰는 `Ops` 호출의 정상 상태는 `RelayManager` 5곳 + `NetworkGameManager` **1곳** = **6곳**이다.**
`NetworkGameManager` 의 `Stage=CodeMissing, Flow=Join` 자리(“Lobby Data 에 Relay Join Code 가 없다”)는
**중복이 아니다** — 그 분기는 `return` 으로 끝나 `RelayManager.JoinRelayAsync` 를 아예 호출하지 않으므로
하위 계층에 대응 로그가 존재할 수 없다. "5곳만 남아야 한다"고 적힌 검증 기준은 틀렸다.

## `system` 문자열 — 확정된 매핑 (실측 + 배치 2 확정)

| 영역 | `system` | 비고 |
|---|---|---|
| `Infrastructure/Network/*` | `"Network"` | 기존 최다(256) |
| `Infrastructure/Auth/*` · `Application/UseCases/LoginUseCase` · `AccountLinkUseCase` | `"Auth"` | 계정/인증 사건 계열 |
| `Infrastructure/Debug/LogSessionOwner`(미처리 예외) | `"Runtime"` | |
| **`Bootstrap/*` (5파일)** | **`"Bootstrap"`** | 배치 2 신설 |
| **`Infrastructure/Cloud/*`** | **`"Cloud"`** | 배치 2 신설 |
| **`Infrastructure/Factories/*`** | **`"Factory"`** | 배치 2 신설 |
| `Application/UseCases/UnitSpawnUseCase` | `"Network"` | 이 로그 자리는 멀티 클라 전용 경로다(원본 태그도 `[Network]`) |
| **`Presentation/UI/**` 전체 (17파일)** | **`"UI"`** | 배치 3 확정. `GameLog.cs` 헤더 주석의 공식 예시(`GameLog.Dev.Warn("UI", nameof(ProductionPanelUI), …)`)를 그대로 재사용 |
| **`Presentation/Audio/*`** | **`"Audio"`** | 배치 3 신설 |
| **`Presentation/Input/*`** | **`"Input"`** | 배치 3 신설 |
| **`Presentation/Grid/*`** | **`"HexGrid"`** | 배치 3 신설. LogRules 1.4 가 `[HexGrid/HexTileView]` 를 예시로 이미 규정 |
| **`Scripts/Debug/UIManagerTestButtonHandler`** | **`"UI"`** | 배치 4. 4건 전부 UI 컴포넌트(UIManager·LoginRootView·팝업)에 관한 로그이고, 이 파일에는 UI 말고 다른 기능 영역이 없다 |

- ⚠️ **`LobbyUI` · `NetworkStatusUI` · `GameEndUI` · `LobbyRootView` 는 원본 태그가 `[Network]` 였지만 `"UI"` 로 통일했다.**
  근거: `system` 은 **발생 지점(코드 영역)** 필드이고(1.4), 한 폴더 안에서 값이 갈리면
  같은 지표가 조용히 둘로 쪼개진다(1.4 값 표기 일관성). "무엇에 관한 로그인가"는 클래스명·메시지가 담는다.

- **`system` 은 "무슨 기능인가"가 아니라 "어느 코드 영역인가"로 정한다.** LogRules 1.4 가 `[System/Class]` 를 **발생 지점 필드**로 규정하기 때문이다.

  **[🔴 2026-09-02 correction — original sentence kept above]** This line contradicts the current
  `LogRules.md` **1.4** body text and must not be applied as written. The 2026-08-18 revision added a
  sentence to 1.4 saying the exact opposite: *"System 은 그 로그가 다루는 「기능」으로 정한다. 파일이
  놓인 폴더로 정하지 않는다"*, with `Presentation/UI/LobbyUI` 의 방 참가 실패 → **`Network`** as the
  worked example, and it names commit `589c2fcf` as a revert of a folder-based reassignment.
  **`LogRules.md` is the single source and wins.** The batch-3 UI unification above is still correct as a
  result (those files' logs really are about UI), but its stated *reason* ("코드 영역") is the wrong rule.
  → Rule of thumb: pick System by **what the log is about**; only fall back to the folder to break a tie
  inside one functional area (that is all the batch-3 note actually established).

## 임시(축 B) 진단 로그를 쓸 때의 확정 패턴 (2026-09-02)

버그 추적·회귀 확인용으로 **곧 지울** 로그를 넣을 때의 형태. `LogRules.md` **1.11** 은 `임시` 로그가
`RuntimeLogger` 직접 호출이어도 된다고 허용하지만, 아래 이유로 **`GameLog.Dev` 를 쓴다.**

| 왜 `Ops` 가 아닌가 | 축 B 판정 1문 *"플레이어 기기에서만 벌어지는가"* 가 **아니오** 다 — 에디터 Play Mode 검증이 목적이므로 `운영` 자격이 없다(1.2). 따라서 **`LogEvent` 키를 새로 만들지 않는다.** 곧 지울 로그에 이름을 만들면 1.5 의 *"이름은 한 번 정하면 바꾸지 않는다"* 와 `ILogSink.cs` enum 주석의 *"선언은 됐는데 아무도 안 쓰는 키"* 경고에 정면으로 걸린다 |
| 왜 `RuntimeLogger` 직접 호출이 아닌가 | ① 1.14 금지 1 을 `GameLog` 경유로 확실히 만족 ② **`[Conditional]` 스트리핑이 공짜로 붙어**, 제거를 깜빡해도 릴리스에 새지 않는다 ③ 1.9/1.11 이 지적한 **전역 훅 방어 ③ 우회 구멍**을 타지 않는다 |

- 🔴 **문자열 조립·반복문이 들어가면 그 코드를 `private void` 헬퍼로 빼고 헬퍼 자체에
  `[System.Diagnostics.Conditional("UNITY_EDITOR")]` + `[...("DEVELOPMENT_BUILD")]` 를 붙인다.**
  `GameLog.Dev` 호출만 스트리핑되면 **바깥의 조립 코드는 릴리스에 그대로 남는다.** `[Conditional]` 은
  반환형이 `void` 여야 하므로 "문자열을 만들어 돌려주는 헬퍼"는 이 보호를 받지 못한다 — 반드시 void.
- **모든 임시 줄에 `Diag=<작업식별자>` 를 첫 `key=value` 로 넣는다.** 분석 시 한 번에 골라내고,
  제거 후 `grep -rn "Diag=<식별자>"` 가 0건이면 1.14 금지 10 을 지켰다는 기계적 증거가 된다.
- 코드에는 **"임시 진단 로그" 라는 말과 제거 시점**을 주석으로 못박는다(MistShrine 선례: `848d891`→`cfe73bb` 전량 제거).

## 배치 2 에서 확인한 사실 (2026-08-18)

- **sink 등록 시점 전수 확인 결과: 배치 2 의 61자리 전부 `LogSessionOwner.EnsureInitialized()` 이후다.**
  - `GameBootstrapper.Awake:450` 이 sink 를 등록하고, `Setup.cs` 13건은 전부 `Start()` → `LoadMap()` 아래에 있다.
  - `LoginBootstrapper.Awake` 는 **첫 줄이 `EnsureInitialized()`** 이고 GPGS 로그가 그 다음 줄이다.
  - **부트스트래퍼에서 sink 등록보다 앞서 실행되는 로그 자리는 존재하지 않는다.**
- ⚠️ **`GameBootstrapper.SetCameraStartPositionForTeam` 은 호출부가 0건이다**(private + 참조 없음).
  `StartNetworkGame` 이 "싱글과 동일하게 맵 중심에서 시작" 방침이라 부르지 않는다. 로그는 이관했지만 실행되지 않는다.
- ⚠️ **중복 로깅 지점 2곳** (지금은 하위/상위 중 한쪽만 운영으로 두어 해소):
  - `UnitSpawnUseCase.SpawnUnitWithId` 타일 없음 → **개발**. 호출부 `NetworkProductionController:521` 이 이미 `ClientStateSyncApplyFailed` 를 운영으로 남긴다.
  - `PlayerProfileService.SaveNicknameAsync` catch → **운영**. 상위 `NicknameSetupView`(배치 3) 이관 시 **개발로 내려야** 집계가 두 배가 안 된다.
- **`GetInt`/`GetBool` 의 폴백 파싱은 바깥 `catch` 에서만 로그한다.** 안쪽 `catch` 에만 달면
  `int.TryParse` 가 **예외 없이 false 를 돌려주는 경로**가 통째로 누락된다(가장 흔한 실패인데도).
- `catch { }` → `catch (Exception e)` 는 예외 객체를 로그에 넘기기 위한 최소 변경이며 동작을 바꾸지 않는다.

## 배치 3·4 에서 확인한 사실 (2026-08-18) — Presentation / Debug

- **`Presentation` 39건 중 운영은 단 1건**(`ProfileView.OnLogoutClicked`). 나머지 38건이 개발이다.
  **결과로 몰아간 것이 아니라 축 B 2문을 실제로 물어 나온 값**이다 — 대상의 절반이 Inspector 배선 누락이고,
  나머지 대부분이 하위 계층이 이미 운영으로 남긴 사건의 상위 호출부다.
- **⭐ Cloud 중복 집계 해소 결과 — 3곳이 중복, 1곳은 중복 아님.**
  | 자리 | 하위 운영 로그 | 판정 |
  |---|---|---|
  | `NicknameSetupView.OnConfirmClicked` | `PlayerProfileService.SaveNicknameAsync` catch → `CloudSaveOperationFailed` 후 `throw;` | **개발로 하향** |
  | `NicknameSetupView.OnSkipClicked` | `SaveAutoNicknameAsync` → 같은 메서드 경유 | **개발로 하향** |
  | `NicknameChangePopup.OnConfirmClicked` | `ChangeNicknameAsync` → `SaveNicknameAsync(3인자 오버로드)` + `LoadProfileAsync` 둘 다 운영 기록 | **개발로 하향** |
  | `ProfileView.OnLogoutClicked` | **없음** — `LoginUseCase.SignOutAsync` 는 **UGS SignOut 만** try 로 감싸고,
    그 뒤 `_authService.SignOutAsync()`(=`FirebaseAuthService`, catch 없음)는 무방비다 | **운영 유지** |
  | `RankingView` | 해당 없음 — 유일한 로그가 Inspector 배선 누락이다 | 개발 |
  → **"Cloud 를 호출하니 중복일 것"이라는 추정은 4곳 중 1곳에서 틀렸다. 반드시 호출 경로를 끝까지 따라간다.**
- **`ProfileView` 의 키는 신설하지 않고 `FirebaseAuthOperationFailed` 를 재사용**(+`Operation=SignOut`).
  그 키는 이미 `Operation=` 으로 작업 종류를 가르도록 설계돼 있어(`FirebaseAuthService.ConvertException`)
  집계가 섞이지 않고 조치도 같은 부류다 → 신설 조건 2개를 모두 충족하지 못한다.
- **`Debug/UIManagerTestButtonHandler.cs` 는 `Assets/Editor/` 가 아니라 `Scripts/Debug/` 라 빌드에 포함**된다.
  개발 축으로 옮기면 `[Conditional]` 로 릴리스에서 문자열 조립까지 사라지는 이득이 생긴다.
- ⚠️ **`[Conditional]` 메서드를 람다에 넣을 때는 반드시 블록 본문 `() => { GameLog…; }` 로 쓴다.**
  C# 은 **문(statement) 자리의 호출만** 제거하므로 식 본문 람다 `() => GameLog…` 는 릴리스에서도 남는다.
- ⚠️ **`key=value` 값에 `, ` 를 넣지 않는다.** `string.Join(", ", …)` 을 그대로 쓰면 구분자와 충돌해
  파싱이 깨진다 → `|` 로 잇는다 (`UnwiredSlots=0|3|5`, `Field=_rowPrefab|_content`).

## 이관 작업 시 자기 검증 (매 배치 실행)

```bash
# 파일별 원래 건수 = GameLog.Dev + GameLog.Ops 인지, 그리고 로그 외 코드를 안 건드렸는지
git show HEAD:<path> | grep -c 'Debug\.Log'
grep -c 'GameLog\.Dev\.' <path>; grep -c 'GameLog\.Ops\.' <path>
# 구조 불변 증거: 단독행 중괄호 / return / await 수가 작업 전후 동일
grep -cE '^[[:space:]]*[{}][[:space:]]*$' <path>
grep -nE '(^|[^.a-zA-Z_])Application\.' <path>   # 0건이어야 함
```

- ⚠️ **`{` 총 개수로 비교하면 안 된다.** `$"{e.Message}"` 같은 문자열 보간이 섞여 오탐이 난다.
- ⚠️ **단독행 중괄호 카운트도 믿지 마라.** `catch { return fallback; }` · `};` 같은 인라인 중괄호가 있는 파일에서는
  원래부터 불균형이라 "MISMATCH" 가 뜬다(배치 2 에서 11파일 중 6파일이 오탐).
  → **주석·문자열 리터럴을 걷어낸 뒤 `{`/`}` 를 세는 파이썬 스크립트**를 쓰는 것이 유일하게 신뢰할 수 있는 방법이다.
- ⚠️ **주석에 `return` / `await` / `Debug.Log` / `GameLog.Dev.` / `GameLog.Ops.` 라는 낱말을 쓰지 마라.**
  검증 grep 이 그대로 오탐한다. (배치 1-A 3번, 배치 2 2번, **배치 3 에서도 3번** 걸렸다.)
  → "개발 축의 Warn 메서드", "옛 방식", "이관 전 원본도 Info 레벨" 처럼 우회 표현을 쓴다.

## 전 배치 완료 시점의 잔존 실측 (2026-08-18 · 배치 4 종료 직후)

`grep -rn "Debug\.Log" Assets/_Project/Scripts --include=*.cs | wc -l` → **54**

- **실제 코드 46** = 이관 대상 아님 30(`GameLog.cs` 9 + `ILogSink.cs` 2 + `Infrastructure/Debug/` 19) + `Editor/` 16
- **주석 안의 죽은 코드/산문 8** — `NetworkGameManager` 4(`/* */` 블록, 686·696·709·714행) ·
  `UnityServicesInitializer` 3(`//` 비활성화 2 + 산문 속 `Debug.LogException` 낱말 1) ·
  `NetworkCombatController` 1(150행 비활성화)
- ⚠️ **계획서 §8-2 의 "최종 46" 은 grep 총계가 아니라 "실행 코드" 기준이다.** grep 은 54 가 정상값이다.
