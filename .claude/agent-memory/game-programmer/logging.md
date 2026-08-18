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
| 2 | `Bootstrap` 26 + `Application` 9 + `Cloud` 8 + `Factories` 7 | 미착수 |
| 3 | `Presentation` 39건 / 20파일 | 미착수 |
| 4 | `Debug/UIManagerTestButtonHandler.cs` 4건 | 미착수 |
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

**축 A 승격/하향 판단의 실제 기준:** *"다음 이벤트가 다시 맞춰 주는가."*
HP·골드처럼 **절대값을 계속 재동기화**하는 값은 Warn, 사망·타일 소유권처럼 **그때 한 번만 오는 이벤트**는 Error.

## `key=value` 표기 규약 (실측 기준 — 새로 만들지 말고 이걸 따를 것)

`Request=` `Reason=` `ClientId=` `Team=` `ExpectedTeam=` `RequestedTeam=` `BuildingTeam=`
`UnitId=` `BuildingId=` `BarracksId=` `UnitType=` `BuildingType=` `Q=` `R=` `TargetQ=` `TargetR=`
`IsServer=` `Role=Host|Client`(대문자 고정) `Stage=` `Flow=` `MatchId=` `LobbyId=` `LobbyCode=` `RelayJoinCode=`
`Uid=`/`PlayerId=`(반드시 `GameLog.HashId`) `Attempt=` `MaxRetries=`

- 배치 1-A 에서 새로 도입: `AppliedDamage=` `AppliedHeal=` `Hp=` `PathLength=` `EntityId=` `ReadyCount=` `BlueGold=` `RedGold=` `UnitTeam=`
- 배치 1-B 에서 새로 도입(**전부 `개발` 로그 전용** — 서버 집계에 올라가지 않는다): `WaitSeconds=` `WinnerTeam=` `PreviousGold=` `ServerGold=` `Diff=`
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

## 이관 작업 시 자기 검증 (매 배치 실행)

```bash
# 파일별 원래 건수 = GameLog.Dev + GameLog.Ops 인지, 그리고 로그 외 코드를 안 건드렸는지
git show HEAD:<path> | grep -c 'Debug\.Log'
grep -c 'GameLog\.Dev\.' <path>; grep -c 'GameLog\.Ops\.' <path>
# 구조 불변 증거: 단독행 중괄호 / return / await 수가 작업 전후 동일
grep -cE '^[[:space:]]*[{}][[:space:]]*$' <path>
grep -nE '(^|[^.a-zA-Z_])Application\.' <path>   # 0건이어야 함
```

- ⚠️ **`{` 총 개수로 비교하면 안 된다.** `$"{e.Message}"` 같은 문자열 보간이 섞여 오탐이 난다. **단독행 중괄호**로 세라.
- ⚠️ **주석에 `return` / `await` / `Debug.Log` 라는 낱말을 쓰지 마라.** 검증 grep 이 그대로 오탐한다. (배치 1-A 에서 3번 걸렸다)
