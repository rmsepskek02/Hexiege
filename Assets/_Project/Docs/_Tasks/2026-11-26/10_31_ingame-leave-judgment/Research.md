# 인게임 연결 끊김 판정 — Research

## 무엇을 왜 하나요

경기 도중 연결이 끊기는 일은 여러 방식으로 생깁니다 — 와이파이가 끊기거나, 홈 버튼으로 앱을 내리거나, 최근 앱 목록에서 밀어서 강제로 끄는 경우입니다. 지금은 **누가 방장(Host)이고 누가 참가자(Client)인지에 따라** 이때 벌어지는 일이 완전히 다릅니다. 직전 측정 작업에서 그 결과, 나간 쪽이 아니라 남은 쪽이 지거나, 한쪽 기기에서 혼자 게임이 계속되거나, 로비로 돌아갈 수 없는 문제가 실제로 확인됐습니다.

이번 작업은 이것을 **누가 Host든 Client든 같은 방식**으로 처리하도록 고칩니다.

- 상대 신호가 잠깐 끊기면(지연) 양쪽 화면을 반투명하게 덮고 가운데에 「네트워크 지연」을 보여 주며, 그동안은 게임 조작을 받지 않습니다. 게임 자체는 멈추지 않고 뒤에서 계속 진행됩니다. 설정 버튼만 덮개 위에 남아 설정을 열거나 포기할 수 있습니다.
- 15초 안에 신호가 돌아오면 그대로 이어서 합니다.
- 15초를 넘기면 「내 인터넷이 되는지」를 먼저 확인해서, 되면 상대가 나간 것(내 승리), 안 되면 내가 끊긴 것(내 이탈)으로 판정하고 결과 화면을 거쳐 로비로 돌아갑니다.

> **2026-11-26 1차 실기 뒤 바뀐 점:** 위 첫 줄의 「양쪽 화면을 덮고 조작을 받지 않는다」는 2차 수정에서 **「문제가 생긴 쪽만 막고 피해 쪽은 막지 않는다」** 로 바뀌었고, 인터넷 확인도 15초에 한 번이 아니라 끊긴 동안 여러 번 합니다. 1차 실기 결과는 [9절](#9-1차-실기-결과-2026-11-26), 바뀐 계획은 [Plan 12절](Plan.md#12-2차-수정-2026-11-26)에 있습니다.

이 문서는 그렇게 고치기 위해 **지금 코드가 어떻게 되어 있는지, 어디를 고쳐야 하는지**를 조사한 결과입니다. 전적(승·패) 기록을 서버에 남기는 일은 이번 범위가 아니며 다음 작업에서 합니다.

> 이 문서와 [Plan](Plan.md)만 이번에 작성합니다. 규칙 문서(`GameSystemRules_*.md`)는 테스트를 마친 뒤 WORKFLOW [11] 단계에서 고칩니다 — 고칠 내용은 Plan 「9. 규칙 개정안」에 미리 적어 둡니다.

**표기:** 경로 접두는 `Assets/_Project/`. 위치는 파일·메서드 이름으로 가리킵니다. 「**직접 확인**」은 이 문서 작성자가 코드를 열어 본 사실, 「**game-programmer 조사 전달값**」은 메인 세션을 거쳐 전달받았고 이 문서 작성자가 다시 열어 보지 않은 사실입니다.

---

## 1. 출발점 — 측정에서 드러난 문제

직전 task [`05_28_ingame-disconnect-measure` Research 8절](../05_28_ingame-disconnect-measure/Research.md#8-측정-결과-2026-11-26)의 요약입니다(원시 로그 출처는 그 문서 8-1).

- 끊김을 남은 쪽이 아는 시각은 **어떤 방식으로 끊겨도 약 60초**(59.6~59.7초, 전송 계층 `ProtocolTimeout`)였다.
- 재접속(끊겼다가 새로 들어와 이어지는 것)은 **0건**이었다.
- 앱 내리기 40초 이내는 경기가 이어졌고, 70초 넘게 내리면 이어지지 않았다. 와이파이는 약 6~10초는 복구, 약 11초·19초는 복구되지 않았다.

| # | 드러난 문제 (8-5) | 어긋나는 규칙 |
|---|---|---|
| 1 | 나간 쪽이 아니라 **남은 Host가 진다** — 상대가 나간 뒤에도 상대 유닛이 Host 화면에서 계속 싸워 Host 성이 파괴됐다 | `GameSystemRules_Network.md` 「네트워크 동작 규칙」 규칙 1(나간 쪽 패배) |
| 2 | Host가 사라지면 Client는 60초 멈춘 뒤 **혼자 진행 모드**로 바뀌고 결과가 그 기기에서만 정해진다 | 같은 문서 「🔴 최상위 원칙」, 규칙 2 확정 D |
| 3 | Host와 끊긴 Client는 **포기가 동작하지 않아** 로비로 돌아갈 수 없다 | 최상위 원칙(③ 이탈·종료 처리) |
| 4 | 70초 넘게 내렸다 돌아오면 그 기기는 **아직 연결된 줄 알고 멈춘다.** 4-b: Host가 내려가 있던 시간만큼 Host의 감지가 늦어진다 | 최상위 원칙 |

그 밖에 Client 쪽 `ServerLostRaised` 줄이 끊김 1회에 2~3번 찍혔다(원인 미조사였음 — 아래 2-3에서 확인).

---

## 2. 현재 코드 상태

### 2-1. 「혼자 진행 모드」로 넘어가는 경로 (문제 2의 원인)

- **직접 확인:** `Scripts/Infrastructure/Network/NetworkCombatController.cs`
  - `OnNetworkSpawn`에서 `NetworkContext.Set(isServer: IsServer, isActive: true)`.
  - `OnNetworkDespawn`에서 `NetworkContext.Reset()` — 주석 「연결 해제 시 NetworkContext를 싱글플레이 기본값으로 초기화」.
  - 프로젝트 스크립트 전체에서 `NetworkContext.Set` / `NetworkContext.Reset` 호출은 **이 두 곳뿐**이다.
- **직접 확인:** `NetworkContext.IsNetworkActive`를 읽는 줄은 스크립트 전체에서 **218줄**(grep 줄 수, 주석 포함). game-programmer 조사 전달값은 「211곳」 — 세는 기준(주석 포함 여부 등)이 달라 보이며, 어느 쪽이든 **고쳐야 할 분기를 하나하나 바꾸는 방식은 현실적이지 않다**는 결론은 같다.
- **직접 확인:** `Scripts/Bootstrap/GameBootstrapper.cs` `Update`에 `!NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer`일 때만 도는 서버·싱글 전용 처리가 있다(예: 타일 점령 갱신). `IsNetworkMode()`도 `NetworkContext.IsNetworkActive`를 그대로 돌려준다.
- **직접 확인:** `Scripts/Application/UseCases/GameEndUseCase.cs` `OnBuildingDied` — `IsGameOver`가 아니면 `IsGameOver = true`로 만든 뒤, `NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer`(멀티 Client)면 발행을 건너뛰고, 그 밖(싱글 또는 서버)이면 `GameEvents.OnGameEnd`를 **직접 발행**한다.
- **직접 확인:** `UAS-SOLO` 문자열이 들어 있는 파일은 `Scripts/Presentation/Unit/UnitView.MovementAuthority.cs`와 `Scripts/Application/UseCases/UnitCombatUseCase.cs` 두 곳이다.
- **game-programmer 조사 전달값:** 연결이 끊기면 NGO가 이 컴포넌트를 디스폰 → `Reset()` → `IsNetworkActive=false` → 위 싱글 분기(전투·투사체·파도·연구 등)와 `UnitView.*`의 싱글 경로가 켜진다 → Client 기기에서 혼자 성이 파괴되면 `GameEndUseCase`가 싱글처럼 결과를 낸다. 또 `GameEndUseCase.IsGameOver`는 **네트워크로 받은 `OnGameEnd`로는 켜지지 않는다**(즉 결과가 이미 났어도 로컬에서 성이 무너지면 다시 판정할 여지가 있다).

**정리:** 끊긴 순간 「멀티 경기」라는 표시 자체가 꺼지는 것이 원인이다. 그래서 고치는 쪽은 211(또는 218)곳의 분기가 아니라 **표시를 끄는 시점(`OnNetworkDespawn`의 `Reset`)** 과 **그 전에 경기를 끝내는 판정**이다.

### 2-2. Host 쪽 30초 유예 — `ReconnectionHandler` (문제 1·4-b와 관련)

**직접 확인:** `Scripts/Infrastructure/Network/ReconnectionHandler.cs`

- `OnNetworkSpawn`: 서버가 아니면 꺼진다. 서버면 `NetworkManager.OnClientDisconnectCallback += OnClientDisconnected`, `OnClientConnectedCallback += OnClientReconnected`를 구독한다. `OnNetworkDespawn`에서 같은 두 구독을 해제한다.
- `OnClientDisconnected` → `WaitAndForceWin` 코루틴 → `_reconnectWaitSeconds` 대기 후 `NetworkGameEndController.ForceWin`.
- `_reconnectWaitSeconds`: 코드 기본값 30, **`Scenes/Game.unity` 직렬화 값도 30**(씬 파일에서 직접 확인).
- 측정에서 이 30초는 전송 계층 60초 **뒤에** 붙어 총 약 90초가 걸렸고(직전 Research 8-3 S1), 그 30초 사이에 남은 Host가 성을 잃는 일이 생겼다(8-2 S4·S6).
- 재접속 취소(`OnClientReconnected`)는 측정에서 한 번도 일어나지 않았다(8-4).

### 2-3. Client 쪽 끊김 처리와 구독 누적 — `NetworkGameManager` (문제 2·3, `ServerLostRaised` 2~3회)

**직접 확인:** `Scripts/Infrastructure/Network/NetworkGameManager.cs`

- `HandleClientDisconnected`: 맵 준비 중이면 맵 준비 실패로 보낸다. **서버면 아무것도 하지 않는다**(주석 「상대 클라 끊김은 ReconnectionHandler 담당」). Client면 `ServerLostRaised` 줄을 남기고 `OnServerDisconnected`를 발행한다.
- `OnServerDisconnected`의 구독자는 맵 준비 대기 중일 때만 반응한다(직전 Research 1-4). 즉 **경기 중 Client 쪽에는 승패·화면을 바꾸는 경로가 없다.**
- **구독 누적 — `ServerLostRaised`가 여러 번 찍힌 원인:**
  - `OnClientDisconnectCallback += HandleClientDisconnected`가 방 만들기·참가·랜덤 매칭 Host·랜덤 매칭 Client 경로마다 한 번씩 있다(각 경로의 시작 함수 안, StartHost/StartClient 직전).
  - 해제(`-=`)는 시작 실패 시, `DisconnectSessionAsync`, `OnDestroy`에만 있다.
  - **`BackToLobby`는 `OnClientConnectedCallback`만 해제하고 `OnClientDisconnectCallback`은 해제하지 않는다.** `BackToLobby`가 부르는 `ShutdownNetworkManager`도 `Shutdown()`만 하고 해제하지 않는다.
  - `NetworkGameManager`는 `DontDestroyOnLoad`라 사라지지 않는다. 그래서 결과 화면 → 로비 버튼(`BackToLobby`) → 다음 경기 참가를 반복할 때마다 같은 처리기가 한 번씩 더 붙는다. **측정에서 끊김 1회에 2~3번 찍힌 것과 들어맞는다**(측정 세션이 여러 판을 이어서 했다). 단 「그 측정에서 실제로 몇 번 붙어 있었는가」는 로그만으로 세지 않았다.

### 2-4. 결과 화면 이탈 감시 — 본뜰 수 있는 것과 없는 것

**직접 확인:** `Scripts/Infrastructure/Network/NetworkGameEndController.cs` `StartResultScreenLeaveWatch` / `ResultScreenLeaveWatchLoop` / `ConfirmOpponentLeftAfterSilence` / `SendResultScreenHeartbeat`

- 1초 틱(`WaitForSecondsRealtime`), 3초마다 신호 송신, 30초 침묵이면 `InternetReachabilityProbe`로 내 인터넷 확인 → 도달 가능이면 이탈 확정.
- 역할 무관 구조다. 역할로 갈리는 곳은 RPC 방향(서버는 ClientRpc, 클라이언트는 ServerRpc)뿐이다.
- 확정 순서는 **「화면 알림(`GameEvents.OnNetworkOpponentLeft`) → 연결 종료(`NetworkGameManager.ShutdownNetwork`)」**. 주석: 연결 종료가 먼저면 디스폰으로 뒤 코드가 실행되지 않을 수 있다. `NetworkBehaviour` 안에서 `Shutdown`을 직접 부르지 않고 `NetworkGameManager`에 맡긴다.
- 간격·한도가 모두 `const`(30초, 3초, 1초, 재시도 10초). 이유는 주석에 「씬 직렬화 값이 코드 기본값을 이기는 함정」.

**그대로 쓸 수 없는 이유(직접 확인 + game-programmer 조사 전달값):**

| 차이 | 결과 화면 | 인게임에 필요한 것 |
|---|---|---|
| 도달 확인 실패 시 | **연결을 끊지 않고 기다린다**(MatchFlow 규칙 17 ②) | **기다리지 않고 「내 이탈」로 판정**(사용자 결정) |
| 감시 루프가 사는 곳 | `NetworkBehaviour` 안 코루틴 — 디스폰되면 같이 사라진다(루프 첫 가드가 `!IsSpawned`면 종료) | 디스폰과 무관하게 끝까지 판정해야 한다 |
| 신호 간격 | 3초 | 1초 (지연 표시 2초 기준을 잴 수 있어야 함) |
| 왕복 시간(RTT) | 재지 않는다 | 「RTT 800ms 이상 3초」 조건에 필요 |
| 침묵 한도 | 30초 | 15초 |

### 2-5. 결과 화면 — `GameEndUI`

**직접 확인:** `Scripts/Presentation/UI/GameEndUI.cs`

- `OnGameEnd`: `NetworkContext.IsNetworkActive`면 `LocalPlayerTeam.Current`, 아니면 `Blue`를 내 팀으로 보고 승패를 정한다. 그 뒤 `DisconnectDiag.ReportResultShown`(측정 task가 넣은 `ResultShown` 줄).
- 자동 복귀 카운트다운은 `ShouldStartAutoReturnCountdown => NetworkContext.IsNetworkActive`일 때만 시작한다. **연결이 끊겨 `Reset`된 뒤에 `OnGameEnd`가 오면 카운트다운이 돌지 않는다.**
- 카운트다운 길이: 평시 `_autoReturnSeconds = 60f`(직렬화 필드), 상대 이탈 시 `OpponentLeftCountdownSeconds = 30f`(const). 상대 이탈 문구 `OpponentLeftCountdownFormat` = 「상대방이 떠났습니다.\n{0}초 뒤 로비로 이동합니다.」, 알림 팝업 문구 「상대방이 떠났습니다.」.
- `GameEvents.OnNetworkOpponentLeft` 구독 하나가 결과 화면의 「상대 이탈」 처리(문구 교체·30초 재시작·다시하기 비활성)를 맡는다.
- `ReturnToLobby`: `NetworkContext.IsNetworkActive && _networkGameManager != null`일 때만 `NetworkGameManager.BackToLobby`, 아니면 `SceneLoader.Load(Lobby)`만 한다. **끊긴 뒤에는 `BackToLobby`의 정리(Lobby 퇴장, `MapHandoff.Clear`, 맵 전송 상태 비우기 등 — `BackToLobby` 본문 직접 확인)를 건너뛴다.** 그 결과 다음 경기에 어떤 영향이 있는지는 미확인.
- 「연결이 끊겼습니다」 문구는 현재 코드에 없다(이번 신설 대상).

### 2-6. 포기 흐름 (문제 3)

**직접 확인:**

1. 설정 → 포기 → 확인 팝업(`InGameSettingsUI`, 확인 시 `OnForfeitConfirmed`).
2. `OnForfeitConfirmed`: `_forfeitService.RequestForfeit()`를 부르고 **그 뒤에** `Hide()`. 주입된 `_forfeitService`는 `Bootstrap/GameBootstrapper.Map.cs`에서 `NetworkContext.IsNetworkActive`면 `NetworkGameEndController`, 아니면 `GameEndUseCase`.
3. `NetworkGameEndController.RequestForfeit` → 「포기 요청 전송」 로그 → `ForfeitServerRpc()`.
4. `ForfeitServerRpc`(서버에서): `_announced` 가드 → 포기한 쪽 패배로 `OnGameEnd` 직접 발행 → `AnnounceWinnerClientRpc` → `BeginResultScreenLeaveWatchClientRpc`.
5. 결과 화면(패배) → 로비 버튼 / 자동 복귀.

- Client가 Host와 끊긴 상태에서는 3번의 ServerRpc가 **받을 서버가 없어** 아무 일도 일어나지 않는다(측정 8-2 S5·S6에서 「포기 요청 전송」만 남음과 일치).
- `RequestForfeit`가 예외를 던지면 `Hide()`가 실행되지 않아 설정 창이 닫히지 않을 수 있다(game-programmer 추정 — 실제로 예외가 나는 경로는 확인하지 않음).
- **경기 중에는 로비 버튼이 없다.** 규칙 1 표의 「로비 버튼/앱 정상 종료」 행은 현재 화면 구성과 맞지 않는다. 또 Android에서 최근 앱 목록 스와이프 종료 시 `OnApplicationQuit`가 불리지 않는다(직전 Research 2절, Unity 공식 문서). **직접 확인:** 스크립트에서 `OnApplicationQuit`를 쓰는 곳을 이번 조사에서 찾지 못했다.

### 2-7. 공용 반투명 막 — `UIManager.ShowBlockingOverlay`

**직접 확인:** `Scripts/Presentation/UI/UIManager.cs`

- `ShowBlockingOverlay(Action onTap = null)`은 **참조 횟수**를 1 올리고 `onTap`을 하나의 칸(`_legacyOverlayTap`)에 **덮어쓴다.** `HideBlockingOverlay`는 1 내리고, 0이 되면 `onTap`을 비운다.
- 이 막은 `InGameSettingsUI`의 표시(`ShowBlockingOverlay(Hide)` — 막을 누르면 설정 창이 닫힘), `ConfirmPopup`, 건물 패널(`BuildingPanelBase`), 건물 배치 UI 등이 **같이 쓴다.**
- `GameSystemRules_CanvasSortingOrder.md` 「전역 SortingOrder 대역 규칙」: BlockingOverlay는 UIManager Canvas(**100**) 소속, 패널 본체는 **200**, 모달 팝업 250, 로딩 300. 인게임 HUD는 0.

**그래서 지연 화면에 이 공용 막을 그대로 쓰면 생기는 문제(이 문서 작성자의 코드 읽기 결론 — 구현 확인 항목):**

- 지연 중 설정을 열면 설정 창이 `onTap`을 자기 `Hide`로 덮어쓴다. 설정을 닫으면 참조 횟수가 1 줄어 지연 막만 남는데 「막을 누르면 설정 창 닫기」가 아니라 빈 칸이 된다 — 이것은 문제가 아닐 수 있으나, 반대로 지연 막을 먼저 내리면 설정 창 쪽 막이 남는 등 **순서에 따라 결과가 달라진다.**
- 공용 막은 「뒤에 있는 화면을 어둡게 가리는 배경」 역할이라 **가운데 글자(「네트워크 지연」)** 를 둘 자리가 없다.
- 설정 **여는 버튼**은 HUD(정렬 0)에 있을 것으로 보이며, 정렬 100의 막보다 아래라 그대로는 눌리지 않는다. 설정 여는 버튼의 실제 위치(어느 Canvas인지)는 이번에 확인하지 못했다.

→ Plan에서는 **전용 지연 오버레이(씬 배치)** 를 기본안으로 두고, 공용 막 재사용 가능성은 game-programmer 확인 항목으로 둔다.

### 2-8. 플레이어 조작 요청 입구 — 12곳

**직접 확인(공개 메서드 존재):**

| 파일 (`Scripts/Infrastructure/Network/`) | 메서드 |
|---|---|
| `NetworkBuildingController.cs` | `RequestBuild`, `RequestUpgrade`, `RequestDemolish` |
| `NetworkProductionController.cs` | `RequestEnqueue`, `RequestCancelSlot`, `RequestSetRallyPoint`, `RequestToggleAuto` |
| `NetworkUpgradeController.cs` | `RequestResearch`, `RequestCancelResearch` |
| `NetworkSkillController.cs` | `RequestActivateSkill` |
| `NetworkMistShrineController.cs` | `RequestActivate`, `RequestToggleAuto` |

- 각 메서드가 ServerRpc로 서버에 요청을 보낸다(메서드 이름·짝이 되는 `…ServerRpc` 존재로 확인, 본문 전체는 읽지 않음).
- 이 12곳을 부른 뒤 UI가 어떤 상태가 되는지(예: 버튼이 「요청 중」으로 바뀐 채 남는지)는 **미확인**.

### 2-9. 인터넷 확인기 — `InternetReachabilityProbe`

**직접 확인:** `Scripts/Infrastructure/Cloud/InternetReachabilityProbe.cs`

- 결과 3가지: `Reachable`(실제 요청 성공), `Unreachable`(요청 실패 — 「확인하지 못했다」), `Skipped`(UGS 미초기화·로그인 전이라 요청 자체를 안 보냄).
- 요청 한도 `ProbeTimeoutMilliseconds = 10000`(10초). 그래서 판정은 「15초 침묵 + 최대 10초 확인」으로 **15~25초**가 걸릴 수 있다.
- 사용처는 현재 결과 화면 감시 1곳.

### 2-10. 측정 task가 넣은 진단 로그

**직접 확인:** `Scripts/Infrastructure/Network/DisconnectDiag.cs`, `InGameDisconnectObserver.cs`

- 공통 키 `Diag=Disconnect, DiagRev=1, Event=…, Role=…, UtcMs, RtMs, MatchSec`. 접두어 `[DISC-DIAG]`. 현재 `DiagRevision = 1`.
- 기존 이벤트: `MatchStart`, `PeerDisconnected`, `TransportFailure`, `GraceStart`/`GraceCancel`/`GraceExpired`, `ForceWin`, `PeerConnected`, `AppPause`(복귀 때 `PausedForSec` — `UtcMs` 차), `AppFocus`, `ServerLostRaised`, `ResultShown`(`IsWin`), `MatchSummary`.
- `InGameDisconnectObserver`는 `NetworkCombatController.OnNetworkSpawn`의 **개발 빌드 전용 블록(`#if`)** 안에서 `BeginMatch`로 만들어진다. NetworkObject가 아닌 빈 GameObject를 Game 씬으로 옮겨 붙이므로 **NGO 디스폰과 무관하게 씬이 내려갈 때까지 산다.**
- 🔴 **이번 판정 기능은 릴리스 빌드에서도 동작해야 한다.** 그래서 새 감시기는 이 관찰자처럼 「빈 GameObject에 붙이는 방식」은 본뜨되, `#if` 블록 **밖**에서 만들어야 한다(로그만 개발 전용).

### 2-11. 전송 계층 설정

직전 Research 1-1(직접 확인된 값): `Game.unity`·`Lobby.unity` 모두 `m_DisconnectTimeoutMS: 60000`, `m_HeartbeatTimeoutMS: 500`. 이번 작업에서 **바꾸지 않는다**(3절 결정).

---

## 3. 사용자 확정 결정 (2026-11-26, 이번 대화)

메인 세션이 전달한 사용자 결정입니다. 모두 Plan의 근거로 씁니다.

| # | 항목 | 결정 |
|---|---|---|
| U-1 | 원칙 | 원인(와이파이·앱 내림·강제 종료)과 역할(Host·Client)에 관계없이 같은 규칙 |
| U-2 | 일시정지 | 게임 일시정지 없음(사용자: 「완성도가 낮은 방향」). Host 백그라운드 시 경기 전체가 멈추는 것은 P2P 한계로 받아들인다(전제: 악의적으로 멈추지 않음) |
| U-3 | 판정 시간 | **N = 15초**(마지막 상대 신호부터). 넘으면 규칙 1 이탈 판정 절차(`InternetReachabilityProbe`): 도달 가능 = 상대 이탈 = 내 승리, 불가 = 내 이탈. 인게임은 도달 불가여도 더 기다리지 않음 |
| U-4 | 지연 표시 조건 | 켜기: 신호 무응답 **2.0초 이상** 또는 RTT **800ms 이상이 3.0초 연속**. 끄기: 무응답 1.0초 미만 + RTT 500ms 미만이 **1.0초 유지**, 최소 표시 **1.0초**. 신호 간격 **1초**. 모두 임시값 **[2026-11-26 2차 수정 — 신호 0.5초 간격, 무응답 1.0초에 표시, 신호가 다시 오면 즉시 해제. RTT 지연만으로는 조작을 막지 않음. Plan 12절]** |
| U-5 | 지연 화면 | 반투명 배경으로 게임 화면을 덮고 가운데 「네트워크 지연」. 동시에 게임 조작 입력 차단. **설정 버튼만** 오버레이 위에 남겨 사용 가능(설정·포기는 플레이에 지장 없음). 게임은 뒤에서 계속 진행. Host·Client 같은 조건. 지연 중 누른 조작은 반영하지 않고 재전송하지 않음. 공용 `UIManager.ShowBlockingOverlay` 재사용 가능성은 구현 확인 항목 **[2026-11-26 2차 수정 — 「Host·Client 같은 조건」의 차단을 「문제가 생긴 쪽만 차단」으로 변경. 피해 쪽 Host는 막지 않는 상단 알림 띠. Plan 12절]** |
| U-6 | 돌아온 기기 | 앱 정지 시간(`UtcNow` 차) **13초 이상**(N−2초 여유 — 양쪽이 동시에 승리를 주장하는 경계를 피함)이면 스스로 패배 처리. 미만이면 같은 오버레이에 「연결 복구 중」(최소 0.5초) 후 Host 정보로 이어감 |
| U-7 | 끊긴 기기의 화면 | 판정에서 「내 이탈」이거나 스스로 패배 처리된 기기: **승패 글자 없이 「연결이 끊겼습니다」만** 표시 → 로비. 최종 결과는 다음 task(전적)에서 다음 로비 진입 때 「지난 경기 결과」 팝업 |
| U-8 | 남은 쪽 화면 | 「승리」 + 기존 「상대방이 떠났습니다」 문구 재사용. 결과 화면 자동 복귀 30초, 다시하기 비활성, 로비 버튼 활성 |
| U-9 | 연결 없는 상태의 포기 | 지연 중 포함. 서버 응답을 기다리지 않고 그 기기에서 즉시 처리 → 결과 화면(끊긴 기기 문구) → 로비. 규칙 1 표의 「로비 버튼/앱 정상 종료」 행은 「포기」 행 + 「끊김」 행으로 재편 |
| U-10 | 대기 중 성 파괴 | 게임 결과 그대로 인정(끊김 판정이 덮어쓰지 않음) |
| U-11 | 기존 유예 | `ReconnectionHandler`의 30초 추가 유예 제거. Client 혼자 진행 모드 금지 |
| U-12 | 전송 계층 60초 | **유지.** 판정 시계가 아니라 안전망. 15초로 줄이면 전송 계층이 판정보다 먼저 끊고, 결과 화면 30초 감시 전제(`GameSystemRules_MatchFlow.md` 규칙 17 「전송 계층 연결 timeout은 이 자체 이탈 판정보다 길어야 한다」)와 충돌 |
| U-13 | 인터넷 확인 못 함(`Skipped`) | 도달 불가로 취급(내 이탈) + 로그 |
| U-14 | 로그 | 개발(Dev) 로그. `Diag=Disconnect` 확장, `DiagRev=2` |
| U-15 | 주기 신호 | 경기 중 전용 신호 RPC 신설(결과 화면 신호는 그대로) |
| U-16 | 범위 포함 | `NetworkGameManager`의 `OnClientDisconnectCallback` 구독 누적 수정(2-3) |
| U-17 | 지연 UI 배치 | 씬 배치(기존 패널 복제 관례) → WORKFLOW [5-2] 에디터 1회성 스크립트로 사용자가 실행 |
| U-18 | 범위 밖 | 전적 기록(서버 검증)은 다음 task |

---

## 4. 현행 규칙 문서와 결정의 차이

| 규칙 (문서 · 절 · 번호) | 현행 문구(요지) | 이번 결정 | 처리 |
|---|---|---|---|
| `GameSystemRules_Network.md` 「네트워크 동작 규칙」 규칙 1 | 회선 끊김·강제 종료 → 남은 쪽 **60초 뒤** 승리. 「인게임60초는 전송 계층이 끊김을 통보하는 시점과 같은 시계」 | 마지막 상대 신호부터 **15초** 자체 판정. 전송 계층 60초는 안전망 | 테스트 후 개정(Plan 9) |
| 같은 규칙 1 표 「로비 버튼/앱 정상 종료」 행 | 나가기 직전 스스로 패배 기록 → 상대 즉시 승리 | 경기 중 로비 버튼 없음, 스와이프 종료 시 `OnApplicationQuit` 미호출 → 「포기」 행 + 「끊김」 행으로 재편 | 테스트 후 개정 |
| 같은 규칙 1 「이탈 판정 절차」 | 도달 불가 → 내 이탈 | 같음. 단 「인게임은 도달 불가여도 기다리지 않는다, 판정 뒤 뒤집지 않는다」 추가 | 테스트 후 개정 |
| 같은 절 규칙 2 확정 E | `t=60`에 승부, `t<60`이 재접속 허용 시간 | 마지막 상대 신호부터 `t=15`, 30초 유예 제거, 재접속 미지원 | 테스트 후 개정 |
| `GameSystemRules_MatchFlowUI.md` 「경기 종료 후 상대 이탈 UI」 D-2 | 다시하기 비활성 조건: 상대 이탈 또는 연결 끊김 실패 | 끊긴 기기(내 이탈) 화면도 비활성 | 테스트 후 개정 |
| 같은 문서 D-4 | 멀티 결과 화면 60초, 이탈 판정 시 30초 | 판정 결과 화면은 처음부터 30초 | 테스트 후 개정 |
| 같은 문서 D-7 상태 줄 표 | 「연결이 끊겼습니다」 상태 없음 | 신설 | 테스트 후 개정 |
| `GameSystemRules_SettingsUI.md` 규칙 4 「포기 결과」 | 멀티: 포기를 요청한 클라이언트의 팀이 패배 | 연결 없는 상태의 포기는 그 기기에서 즉시 처리 | 테스트 후 개정 |
| `GameSystemRules_CanvasSortingOrder.md` 「전역 SortingOrder 대역 규칙」 | 지연 오버레이 대역 없음 | 오버레이·설정 버튼 순서 추가 | 구현 확인 후 개정 |
| `GameSystemRules_MatchFlow.md` 규칙 17 | 결과 화면 30초 자체 감시 | 「인게임 침묵 시간은 이어받지 않는다」 추가 | 테스트 후 개정 |

---

## 5. 설계 방향 (game-programmer 제안 — 조사 전달값)

1. **상태기계 `InGameLinkMonitor`** (Application 계층, 순수 C#): 정상 → 지연 → 판정 중 → 종료. Unity 없이 시간·신호를 넣어 시험할 수 있게 한다.
2. **감시기 `InGameLinkWatch`** (Infrastructure, 일반 MonoBehaviour): NetworkObject가 아닌 빈 GameObject(2-10의 관찰자 생성 방식 선례). 1초 틱, 신호 송수신(`NetworkGameEndController`에 `PresenceHeartbeatServerRpc/ClientRpc(sentAt, echo)` 신설 — RTT는 되돌아온 값으로 계산, 비신뢰 전송 권장), `OnClientDisconnectCallback` 구독(경기 중 끊김 통보는 즉시 「판정 중」으로), 앱 일시정지 처리, 도달 확인 → 판정.
3. **판정 결과 처리**: `NetworkGameEndController`에 역할 무관 「로컬 결론」 메서드 신설 → 중복 깃발 → `OnGameEnd`(멀티 표시가 켜진 상태에서) → 사유 이벤트 → `NetworkGameManager.ShutdownNetwork()` → 결과 화면 → 로비. Host 전용 `ForceWin`은 이 로컬 결론으로 모은다.
4. **혼자 진행 막기**: 주 방어 = 15초 판정이 전송 계층 60초보다 먼저 경기를 끝낸다. 안전망 = 경기 중 예상 밖 디스폰이면 `NetworkContext.Reset`을 씬이 내려갈 때로 미루고 판정으로 보낸다. 이때 디스폰 뒤에도 자동으로 RPC를 보내려는 지점(예: `NetworkCombatController.Projectiles.cs`의 `RequestProjectileSnapshotServerRpc` 호출부 — **직접 확인: 호출부 존재**)을 전수 점검해야 한다. `GameEndUseCase`가 `OnGameEnd`를 받으면 `IsGameOver`를 잠근다.
5. **`ReconnectionHandler`**: 클래스·컴포넌트는 유지하고 `OnNetworkSpawn`의 두 구독과 `OnNetworkDespawn`의 해제 줄을 주석으로 비활성화(WORKFLOW [4] 「기존 로직 제거 규칙」).
6. **포기**: `RequestForfeit`에서 「서버와 끊김」(감시기가 지연·판정 중, 또는 `!IsSpawned || !IsListening || (!IsServer && !IsConnectedClient)`)이면 로컬 패배 경로, 아니면 기존 ServerRpc.
7. **조작 차단**: Application 정적 홀더 `NetworkLinkState.BlocksPlayerCommands`(`NetworkContext`와 같은 패턴) + 2-8의 12곳 입구 확인. 오버레이가 1차 차단, 입구가 2차 방어.
8. **결과 화면**: 끊긴 뒤에도 카운트다운이 돌고 로비 복귀 정리가 되도록 `GameEndUI`의 조건을 바꾼다(2-5).
9. **앱 정지 시간**: `DateTimeOffset.UtcNow` 차로 잰다(`realtimeSinceStartup`이 백그라운드 동안 흐르는지 미확인). 복귀 프레임에서 「내가 얼마나 멈췄나」 검사를 「상대가 얼마나 조용했나」 검사보다 **먼저** 한다(멈춘 쪽이 상대를 침묵으로 오판하지 않게).

---

## 6. 영향 범위

- **네트워크 경기 전체:** 감시기는 모든 멀티 경기(랜덤·커스텀·재경기)에 켜진다. 싱글에는 켜지지 않아야 한다.
- **`NetworkContext.Reset` 시점 변경:** 218줄의 `IsNetworkActive` 읽기가 「디스폰 직후 ~ 씬 전환」 구간에서 종전과 다른 값을 보게 된다. 그 구간에 도는 코드가 NGO가 내려간 상태에서 RPC를 보내려 하면 오류가 난다(5-4 전수 점검).
- **결과 화면(`GameEndUI`):** 카운트다운·로비 복귀 조건과 새 문구. 기존 상대 이탈(결과 화면 규칙 17 경로) 동작이 바뀌지 않아야 한다.
- **포기:** 연결 있는 경우의 기존 흐름이 바뀌지 않아야 한다.
- **`NetworkGameManager` 구독 정리:** 방 만들기·참가·랜덤 매칭·맵 준비 실패 경로가 같은 처리기를 쓴다(2-3). 해제 위치를 옮기면 맵 준비 실패 감지에 영향이 없는지 확인해야 한다.
- **UI 정렬:** 지연 오버레이가 결과 화면·확인 팝업·로딩보다 위에 있으면 안 된다(정렬 순서 결정 필요).

---

## 7. 위험 요소

| 위험 | 내용 |
|---|---|
| 양쪽 모두 승리 주장 | Relay 경로만 끊기고 양쪽 인터넷은 되는 경우, 양쪽이 서로 「상대 이탈」로 판정한다. 화면은 양쪽 「승리」. 기록 정합은 전적 task(서버 대조) 몫 |
| 양쪽 모두 패배 | 반대로 양쪽 다 도달 확인에 실패하면 양쪽 「연결이 끊겼습니다」. 같은 이유로 전적 task 몫 |
| 판정까지 걸리는 시간 | 15초 + 도달 확인 최대 10초 = 15~25초 |
| 경기 시작 직후 오판 | 씬 로딩 차이로 첫 신호가 늦게 올 수 있다 → 「첫 신호를 받은 뒤부터」 침묵을 재는 것이 권장(game-programmer). 첫 신호가 끝내 오지 않는 경우의 처리는 Plan에서 정한다 |
| Host 백그라운드 | Host가 내려간 동안 Client 쪽 게임이 함께 멈춘다(U-2에서 수용). Client 화면은 지연 오버레이, 15초 넘으면 Client 승리 |
| 백그라운드 13~15초 경계 | 돌아온 기기는 13초 이상이면 스스로 패배, 남은 쪽은 15초에 판정 → 13~15초 사이 복귀면 **양쪽 다 「끝」이지만 남은 쪽은 아직 판정 전**일 수 있다. 남은 쪽은 이후 신호가 끊긴 채(돌아온 기기가 연결을 내리므로) 판정 → 승리. 결과는 일치하나 남은 쪽은 몇 초 더 오버레이를 본다 |
| 혼자 진행 안전망의 넓이 | `Reset` 시점을 미루면 그 사이 「멀티인데 연결은 없는」 상태를 처음 만든다. 이 상태를 전제로 짜이지 않은 코드가 있을 수 있다(game-programmer 「B2 영향 범위」 위험 전달) |
| 신호 RPC의 전송 방식 | 비신뢰(손실 허용) 전송이 NGO 2.9.2에서 어떤 속성으로 지정되는지 미확인 |

---

## 8. 확인하지 못한 것

- NGO 2.9.2의 비신뢰 RPC 지정 방법(속성 이름·사용 가능 여부) — 로컬에 패키지 소스가 없다(직전 Research 7절).
- `UnityTransport`의 끊김 시간(`DisconnectTimeoutMS`)을 코드에서 읽는 공개 속성 유무.
- 앱이 복귀하는 프레임에서 `OnApplicationPause(false)`와 `Update`·코루틴의 실행 순서.
- Client 쪽에서 「끊김 콜백」과 「디스폰」 중 무엇이 먼저 오는지.
- 상대가 정상적으로 연결을 내렸을 때(`Shutdown`) 끊김 통보가 남은 쪽에 즉시 오는지 — 측정은 모두 `ProtocolTimeout`(60초)였다.
- 끊긴 뒤 `SceneLoader.Load`만으로 로비에 돌아갈 때 남는 상태(Lobby 퇴장 안 함, `MapHandoff` 미정리)가 다음 경기에 주는 영향.
- 12개 조작 입구를 차단했을 때 그 UI의 상태(「요청 중」으로 남는지 등).
- 설정 여는 버튼이 어느 Canvas·정렬 순서에 있는지.
- `IsNetworkActive` 읽기 개수의 정확한 기준(직접 grep 218줄 / 전달값 211곳).
- `Application.realtimeSinceStartup`이 Android 백그라운드 동안 흐르는지(직전 Research부터 미확인).

---

## 9. 1차 실기 결과 (2026-11-26)

### 9-1. 무엇을 확인했나요

1차 구현을 실제 기기와 에디터로 돌려 본 결과입니다. 결론부터 말하면 **8개 시나리오가 모두 계획한 대로 동작했습니다**(사용자: 「기대값에 맞게 잘 구현됐어」). 다만 와이파이를 껐다 켠 첫 판에서 **양쪽이 모두 「승리」** 를 본 일이 한 번 있었고, 끊길 때 덮개(오버레이)가 게임이 멈춘 뒤 1~2초 늦게 뜨는 점이 드러났습니다. 이 두 가지와 사용자의 추가 우려가 [Plan 12절 「2차 수정」](Plan.md#12-2차-수정-2026-11-26)의 출발점입니다.

- **로그 원본:** private 저장소 `rmsepskek02/Hexiege-Logs`, Release `logs-2026-11`의 `2026-11-26_23_52_ingame-leave-judgment_01.zip`(asset id 796372217).
- **분석:** 아래 수치는 모두 **메인 세션이 이 ZIP을 분석해 전달한 값**이다. 이 문서 작성자가 로그를 다시 열어 세지 않았다.
- **범위:** 사용자 테스트는 **1회차(실기기 = Host, 에디터 = Client)만** 했다. 2회차(실기기 = Client)는 하지 않았다.

### 9-2. 시나리오별 결과 (1회차)

| # | 한 일 | 실제 결과 (메인 세션 분석값) | 판정 |
|---|---|---|---|
| S1 | 실기기 강제 종료 | 실기기 정지 23:41:05.4 → 에디터 지연 표시(`LinkDelayEnter`) **+1.4초**(`SilenceSec=2.0`) → 판정 시작(`LinkJudgeStart`) 15.0 → `Probe=Reachable` → 결과 화면 `ResultShown IsWin=True Cause=OpponentLeft`(정지 후 **14.7초**) | 기대대로 |
| S2 | 4.9초 백그라운드 | 실기기 `LinkRecovering` → 1.2초 뒤 해제. 에디터는 지연 표시 6.1초 뒤 해제 | 기대대로 |
| S3 | 10.7초 백그라운드 | 실기기 `LinkRecovering` → 1.8초 뒤 해제. 에디터는 지연 표시 11.5초 뒤 해제 | 기대대로 |
| S4 | 20.1초 백그라운드 | 에디터 15초 판정 승리(23:43:32.5). 실기기 복귀 즉시 `SelfPauseDefeat` → `ResultShown Cause=SelfPauseDefeat` | 기대대로 |
| S5 | 와이파이·데이터 끄고 잠시 뒤 켬(계획 5초) | 재시도 판에서 7.0초 끊김 **2회 모두 복구**. 🔴 **첫 시도 판(23:45:27 경기)은 복구되지 않았고**, 양쪽 모두 15초 판정에서 `Probe=Reachable`(실기기 373ms) → **양쪽 「승리」.** 와이파이를 다시 켜서 인터넷은 돌아왔지만 게임 연결은 끊긴 채였다 | 첫 시도는 위험 요소(7절 「양쪽 모두 승리 주장」)가 실제로 일어난 경우 |
| S6 | 와이파이·데이터 끄고 판정 넘김 | 실기기 `Probe=Unreachable`(57ms) → `SelfDisconnected`. 에디터 승리 | 기대대로 |
| S7 | 끊긴 상태에서 포기 | `OfflineForfeit Reason=Delay` → 실기기 `Cause=OfflineForfeit`. 에디터 15초 판정 승리 | 기대대로 |
| S8 | 지연 중 조작 시도 | 오버레이가 터치 **18회** 차단, `BlockedCommands=0`(서버로 나간 조작 0건) | 기대대로 |

### 9-3. 모든 판에 공통으로 확인된 것

| 항목 | 결과 | 대응하는 1차 목표 |
|---|---|---|
| 30초 유예 | `GraceStart` **0줄** | X-1 유예 제거 |
| 끊김 처리기 구독 | `DisconnectCallbackSub` 판마다 `Add Count=1`, 로비 복귀 때 `Remove`. `ServerLostRaised`는 끊김당 **1줄** | 2-3 구독 누적 해결 |
| 혼자 진행 | 경기 중 `UAS-SOLO` **0줄** | 문제 2 |
| Host 판정 대기 중 생산 | 예외 0건 | — |
| ERROR 줄 | Relay/Lobby 방 만들기·참가 실패와 진단 요약뿐 — 이번 기능과 무관 | — |

### 9-4. 아직 설명되지 않은 것

| 항목 | 내용 | 상태 |
|---|---|---|
| S5 첫 시도 양쪽 승리 | 와이파이를 다시 켠 뒤 인터넷은 되지만 게임 연결은 돌아오지 않은 경우, 15초 시점의 한 번뿐인 인터넷 확인은 성공해 버린다. 끊겨 있던 동안의 「진짜 인터넷 상태」를 보지 못한다 | Plan 12절 ① 인터넷 확인 반복으로 대응 |
| S4 판 결과 화면 직후 `UAS-SOLO` 66줄 | 23:43:32, **0.05초 동안 66줄.** 결과 화면이 뜬 직후(경기 종료 후)다 | **원인 미확인** — 2차에서도 진단 로그로 계속 본다 |
| 표시 순서 | 끊길 때 오버레이가 게임 정지보다 **1.4~2초 늦게** 뜨고, 복구 때는 게임 재개보다 **1.2~1.8초 늦게** 사라진다. 사용자: 「UI가 먼저 작동하고 게임진행이 되는게 더 좋을거같아」 | Plan 12절 ③ 표시 시점으로 대응 |
| 2회차(실기기 Client) | 하지 않았다 — Host가 피해 쪽일 때의 화면은 실측이 없다 | 2차 테스트에서 함께 본다 |
