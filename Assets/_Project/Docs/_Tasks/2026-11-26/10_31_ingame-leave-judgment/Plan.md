# 인게임 연결 끊김 판정 — Plan

## ⚠️ 기존 로직 비활성화 (WORKFLOW [4] 「기존 로직 제거 규칙」 — 최상단 명시)

이번 작업은 아래 기존 동작을 **지우지 않고 끄거나(주석 처리) 시점을 바꿉니다.** 최종 삭제는 [6] 사용자 테스트 통과 뒤, [7] 문서/메모리 업데이트 전에 사용자 승인을 받아 합니다.

| # | 대상 | 방식 | 안전 근거 |
|---|---|---|---|
| X-1 | `Scripts/Infrastructure/Network/ReconnectionHandler.cs` — `OnNetworkSpawn`의 `OnClientDisconnectCallback += OnClientDisconnected`, `OnClientConnectedCallback += OnClientReconnected` 두 구독과, `OnNetworkDespawn`의 해제 두 줄 | **주석으로 비활성화.** 클래스·컴포넌트·`Game.unity`의 오브젝트와 직렬화 값(`_reconnectWaitSeconds: 30`)은 그대로 둔다. 비활성화를 알리는 `GraceDisabled` 진단 줄 1개 추가 | ① 사용자 결정 U-11(30초 유예 제거). ② 측정에서 재접속 취소가 0건이었고(직전 Research 8-4), 이 30초가 남은 Host 패배(문제 1)의 시간창이었다. ③ 같은 역할(상대 끊김 → 승리)은 새 감시기 `InGameLinkWatch`가 역할 무관으로 대신한다. ④ 구독만 끄므로 컴포넌트를 지울 때 생기는 씬 참조 깨짐이 없다 |
| X-2 | `Scripts/Infrastructure/Network/NetworkCombatController.cs` `OnNetworkDespawn`의 `NetworkContext.Reset()` | **경기 중 예상 밖 디스폰일 때만** 실행을 Game 씬이 내려갈 때로 **미룬다.** 정상 경로(결과 화면 → 로비, 재경기)는 종전과 같은 시점에 `Reset` | ① 혼자 진행 모드(문제 2)의 직접 원인(Research 2-1). ② 미루는 동안의 처리는 판정 경로가 결과 화면 → 로비로 끝낸다. ③ 씬 전환 때 반드시 `Reset`되므로 다음 경기·싱글에 「멀티 표시」가 남지 않는다 — 이 점을 `SoloModeBlocked`·`LinkWatchStop` 줄로 확인 |
| X-3 | 혼자 진행 분기 — `Bootstrap/GameBootstrapper.cs` `Update`의 싱글 분기, `Application/UseCases/GameEndUseCase.cs` `OnBuildingDied`의 싱글 발행, `Presentation/Unit/UnitView.*` 싱글 경로 | **코드 분기는 고치지 않는다.** X-2로 「멀티 표시」가 경기 중 꺼지지 않게 해서 이 분기들로 들어가지 않게 막는다. 추가로 `GameEndUseCase`는 `OnGameEnd`를 받으면 `IsGameOver`를 잠근다(결과 확정 뒤 로컬 재판정 방지) | ① 분기가 218줄에 걸쳐 있어 하나씩 바꾸면 놓칠 위험이 크다(Research 2-1). ② 들어가는 입구 하나(X-2)만 막으면 분기 자체는 싱글에서 종전대로 쓰인다 |
| X-4 | `Scripts/Infrastructure/Network/NetworkGameEndController.cs` `ForceWin`(Host 전용 강제 승리) | 호출하던 곳(X-1)이 꺼져 **호출되지 않게 된다.** 메서드 자체는 남긴다. 새 「로컬 결론」 메서드가 같은 역할을 역할 무관으로 맡는다 | X-1과 같음. 다른 호출부가 있는지는 game-programmer가 확인해 보고(11절) |

---

## 무엇을 왜 하나요

경기 중 연결이 끊겼을 때(와이파이·앱 내림·강제 종료), **누가 Host든 Client든 같은 방식**으로 처리하도록 정리합니다.

- 상대 신호가 잠깐 끊기면 양쪽 화면을 반투명하게 덮고 가운데에 「네트워크 지연」을 보여 주며 게임 조작을 막습니다. 게임 자체는 멈추지 않고 계속 진행됩니다. 설정 버튼만 덮개 위에 남겨 설정과 포기는 할 수 있습니다.
- 15초 안에 돌아오면 이어서 합니다. 15초를 넘기면 「내 인터넷이 되는가」를 확인해, 되면 상대가 나간 것(내 승리), 안 되면 내가 끊긴 것(내 이탈)으로 판정하고 결과 화면 → 로비로 갑니다.
- 앱을 13초 이상 내렸다가 돌아온 기기는 이미 상대 쪽에서 판정이 났을 것이므로 스스로 패배 처리하고 「연결이 끊겼습니다」를 보여 준 뒤 로비로 갑니다.
- 연결이 없는 상태에서 포기하면 서버 대답을 기다리지 않고 그 자리에서 끝냅니다.

직전 측정 task(`05_28_ingame-disconnect-measure` Research 8절)에서 드러난 문제 1~4(남은 Host가 짐, Client 혼자 진행, 끊긴 뒤 포기 불가, 오래 내렸다 돌아오면 멈춤)를 이것으로 고칩니다. 조사 내용은 [Research](Research.md)에 있습니다. **전적 기록(서버 검증)은 다음 task**입니다.

---

## 1. 근거 규칙

`Assets/_Project/Docs/GameSystemRules.md` 인덱스를 읽고 「접속·이탈·승패의 역할 일관성」·「경기 준비 실패·재경기·결과 화면」·「공통 UI」 진입점의 원본을 열어 확인했습니다.

| 근거 (문서 · 절 · 번호) | 현행 문구 요지 (직접 인용·요약) | 이번 작업에서의 쓰임 |
|---|---|---|
| `GameSystemRules_Network.md` 「🔴 최상위 원칙」 | 「무슨 상황이든 화면도 승패 기록도 역할과 무관하게 항상 같게 동작해야 한다」, 적용 범위 ① 화면 ② 승패 기록 ③ 이탈·종료 처리 | 모든 항목을 Host·Client 같은 코드 경로로. 모든 로그에 `Role` |
| 같은 문서 「네트워크 동작 규칙」 규칙 1 | 나간 쪽 패배·남은 쪽 승리, 역할로 갈리지 않음. **이탈 판정 절차:** 상대 신호가 없으면 인터넷 도달을 먼저 확인, 가능 → 상대 이탈 → 내 승리, 불가 → 내 이탈 → 내 패배 | 판정 절차(2-3)를 그대로 쓴다. 「60초」·표는 개정 대상(9절) |
| 같은 절 규칙 2 확정 D | 「Host와 Client가 각자 자기 쪽에서 판정한다」, 상대 신호 상실 시 규칙 1 도달 판정 | 감시기를 양쪽에서 같은 코드로 돌린다 |
| 같은 절 규칙 2 확정 E | 「그 뒤의 유예는 없앤다」, `_reconnectWaitSeconds`·`WaitAndForceWin()`·`OnClientReconnected()`는 정리 대상, 씬 직렬화 값 대조 필요 | X-1의 근거. `t=60`은 개정 대상(U-3 → 15초) |
| `GameSystemRules_MatchFlow.md` 규칙 17 | 결과 화면 이탈 판정. 「전송 계층 연결 timeout은 이 자체 이탈 판정보다 길어야 한다」. ② 도달 불가면 연결을 끊지 않고 기다림 | 전송 계층 60초 유지(U-12)의 근거. 결과 화면 감시는 **건드리지 않는다**(인게임만 「기다리지 않음」) |
| `GameSystemRules_MatchFlowUI.md` 「경기 종료 후 상대 이탈 UI」 D-2 · D-3 · D-4 · D-7 | D-2 다시하기 비활성 조건 표, D-3 로비 버튼 항상 활성, D-4 멀티 60초 · 이탈 판정 시 30초, D-7 상태 줄 문구 표(상대 이탈: 「상대방이 떠났습니다.」+줄바꿈+「n초 뒤 로비로 이동합니다.」) | 남은 쪽 화면은 D-7 상대 이탈 문구 재사용(U-8). 끊긴 쪽 문구는 신설 → 개정안 C-5 |
| `GameSystemRules_SettingsUI.md` 규칙 4 「포기 결과」 | 「포기 플레이어는 패배 처리된다」 「멀티플레이: 포기를 요청한 클라이언트의 팀이 패배」 | 연결 없는 포기도 포기한 쪽 패배 — 처리 위치만 그 기기로(U-9) |
| `GameSystemRules_CanvasSortingOrder.md` 「전역 SortingOrder 대역 규칙」 | 0 HUD / 100 UIManager Canvas(BlockingOverlay) / 200 패널 / 250 모달 / 300 로딩 | 지연 오버레이의 정렬 위치 결정(3단계 6) |
| WORKFLOW [4] 「기존 로직 제거 규칙」 · 「테스트 항목 진단 로그 규칙」, [5-2] | 비활성화 우선·최상단 명시 / 테스트 항목마다 로그 대응표 / Inspector 작업은 1회성 에디터 스크립트 | 최상단 표, 6절, 8절 |
| [LogRules](../../../LogRules.md) §1.2 · §1.4 · §1.5 · §1.6 · §1.14 | 두 축, `key=value`, 이벤트 키, 민감 정보 금지, 매 틱 로그 금지 | 5절 |

사용자 결정 U-1~U-18은 [Research 3절](Research.md#3-사용자-확정-결정-2026-11-26-이번-대화)에 있으며, 아래 각 항목에 번호로 적습니다.

---

## 2. 동작 정의

### 2-1. 값 (모두 임시값 — U-4, U-3, U-6)

코드 상수(`const`)로 둡니다. 씬 직렬화 값이 코드 값을 이기는 함정(결과 화면 감시 주석, Research 2-4)을 피하기 위해 `[SerializeField]`로 빼지 않습니다.

| 이름(설명용) | 값 | 뜻 |
|---|---|---|
| 신호 간격 | 1.0초 | 양쪽이 서로에게 「여기 있다」 신호를 보내는 간격 |
| 지연 진입 — 무응답 | 2.0초 이상 | 마지막 상대 신호 이후 이 시간 넘게 아무것도 없으면 지연 |
| 지연 진입 — 느림 | RTT 800ms 이상이 3.0초 연속 | 신호는 오지만 왕복이 계속 느린 경우 |
| 지연 해제 | 무응답 1.0초 미만 **그리고** RTT 500ms 미만이 1.0초 유지 | 두 조건을 함께 1초 동안 만족 |
| 최소 표시 | 1.0초 | 지연 표시를 켰으면 최소 이만큼은 보인다(깜빡임 방지) |
| 판정 시작 N | 15초 | 마지막 상대 신호부터. 넘으면 인터넷 도달 확인 |
| 스스로 패배 기준 | 앱 정지 13초 이상 | 돌아온 기기가 `UtcNow` 차로 잰 정지 시간 |
| 「연결 복구 중」 최소 표시 | 0.5초 | 13초 미만으로 돌아온 기기 |
| 결과 화면 자동 복귀 | 30초 | 판정으로 끝난 결과 화면(양쪽) |

### 2-2. 상태 (Host·Client 같은 코드 — 최상위 원칙, 규칙 2 확정 D)

| 상태 | 들어가는 조건 | 화면 | 조작 |
|---|---|---|---|
| 대기 | 경기 시작 ~ 상대 신호 첫 수신 | 평소 | 평소 |
| 정상 | 첫 신호 수신 이후 | 평소 | 평소 |
| 지연 | 2-1 진입 조건 | 오버레이 + 「네트워크 지연」 | 차단(설정 버튼만 가능) |
| 판정 중 | 무응답 15초 **또는** 경기 중 끊김 통보(`OnClientDisconnectCallback`) 수신 | 오버레이 유지 | 차단 |
| 종료 | 판정 결과·스스로 패배·연결 없는 포기·정상 경기 종료 | 결과 화면 | — |

- **경기 시작 직후 오판 방지:** 침묵은 「첫 신호를 받은 뒤부터」 잰다(Research 7절). 대기 상태가 길게 이어질 때(첫 신호가 오지 않을 때)의 처리는 game-programmer가 정해 보고한다(11절) — 끊김 통보가 오면 그때 판정 중으로 가는 것은 같다.
- **정상 경기 종료가 우선:** 지연·판정 중에 성이 파괴되어 정상 결과가 나면 그 결과를 쓴다. 판정은 결과를 덮어쓰지 않는다(U-10). 중복 깃발 하나로 먼저 난 결론만 쓴다.

### 2-3. 판정 (규칙 1 「이탈 판정 절차」 + U-3 · U-13)

1. 판정 중에 들어가면 `InternetReachabilityProbe`로 한 번 확인한다(최대 10초 — Research 2-9).
2. `Reachable` → **상대 이탈 → 내 승리.**
3. `Unreachable` 또는 `Skipped` → **내 이탈.** `Skipped`는 별도 로그 값을 남긴다(U-13).
4. 확인이 예외로 끝나면 `Unreachable`과 같게 다룬다(결과 화면 감시와 달리 「기다림」이 없다 — U-3).
5. 판정은 다시 뒤집지 않는다. 확인을 기다리는 동안 상대 신호가 돌아와도 판정을 취소하지 **않는다** — *이 점은 사용자 결정 원문에 직접 없는 세부다. 「인게임은 도달 불가여도 더 기다리지 않음」과 game-design-lead 개정안 「판정 후 뒤집지 않음」에서 이 문서가 끌어낸 것이므로, Plan 공유 때 사용자 확인을 받는다.*

### 2-4. 판정 결과 처리 순서 (결과 화면 감시의 「알림 → 연결 종료」 선례 — Research 2-4)

1. 중복 깃발(이미 결론이 났으면 아무것도 안 함).
2. `GameEvents.OnGameEnd` 발행 — **멀티 표시(`NetworkContext`)가 켜진 상태에서.** 승자는 「내 승리」면 내 팀, 「내 이탈」이면 상대 팀.
3. 결과 사유 이벤트 발행(상대 이탈 / 내 끊김) → `GameEndUI`가 문구를 고른다.
4. `NetworkGameManager.ShutdownNetwork()`로 연결 종료.
5. 결과 화면 30초 카운트다운 → 로비(로비 버튼으로도 가능 — D-3).

### 2-5. 돌아온 기기 (U-6)

- `OnApplicationPause(false)` 프레임에서 **자기 정지 시간 검사를 침묵 검사보다 먼저** 한다(Research 5-9).
- 정지 시간 13초 이상 → 스스로 패배 → 2-4와 같은 순서(사유 = 내 끊김).
- 13초 미만 → 오버레이 문구를 「연결 복구 중」으로 바꾸고 최소 0.5초 → 상대 신호가 오고 지연 해제 조건을 만족하면 평소 화면. 화면 상태는 Host가 보내는 정보로 이어진다(별도 복구 처리 없음 — 측정에서 40초 이내 복귀는 그대로 이어졌다).
- 13초 미만으로 돌아왔는데 상대 쪽에서 이미 판정이 끝나 연결이 내려가 있으면, 끊김 통보를 받아 판정 중 → 판정으로 간다.

### 2-6. 화면 (U-5, U-7, U-8)

| 기기 | 결과 화면 제목 | 상태 줄 | 다시하기 | 로비 버튼 |
|---|---|---|---|---|
| 남은 쪽(내 승리) | 「승리」 | 「상대방이 떠났습니다.」+줄바꿈+「n초 뒤 로비로 이동합니다.」(D-7 상대 이탈 문구 재사용) | 비활성 | 활성 |
| 끊긴 쪽(내 이탈·스스로 패배·연결 없는 포기) | **승패 글자 없음** | 「연결이 끊겼습니다」 + 남은 초 표시(정확한 줄바꿈·문장은 9절 개정안 C-5를 따른다) | 비활성 | 활성 |

- 지연 오버레이: 반투명 배경 + 가운데 「네트워크 지연」(복귀 중이면 「연결 복구 중」). 배경이 게임 화면 입력을 막는다. **설정 여는 버튼과 설정 창·확인 팝업만** 오버레이 위에 온다. 결과 화면이 뜨면 오버레이는 내려간다.
- 지연 중 누른 조작은 반영하지 않고, 지연이 끝난 뒤 다시 보내지도 않는다(U-5).

### 2-7. 포기 (U-9, SettingsUI 규칙 4)

| 연결 상태 | 처리 |
|---|---|
| 연결 있음(정상 상태) | **종전 그대로** — `ForfeitServerRpc` → 결과 화면(패배) |
| 연결 없음(감시기가 지연·판정 중이거나, `!IsSpawned` · `!IsListening` · Client인데 `!IsConnectedClient`) | 그 기기에서 즉시 「로컬 결론(내 이탈)」 → 2-4 순서 → 끊긴 쪽 화면 → 로비. 상대 쪽은 신호가 끊긴 것으로 보고 15초 뒤 판정 → 승리 |

- *지연 중 포기를 「연결 없음」으로 보는 것은 U-9 「연결 없는 상태(지연 중 포함)의 포기」 원문을 따른다.* 지연 중이지만 실제로는 RPC가 닿는 경우, 상대에게 정상 포기 통보가 가지 않으므로 상대는 15초 판정으로 승리한다 — 결과(포기한 쪽 패배)는 같다.

---

## 3. 구현 순서

game-programmer가 아래 순서로 진행하고 단계마다 컴파일 확인. 계층 방향은 `.claude/MEMORY.md` 「아키텍처 핵심 제약」을 따른다.

| 단계 | 내용 | 근거 |
|---|---|---|
| 1 | **상태기계 `InGameLinkMonitor`** (Application, 순수 C#) — 2-1 값, 2-2 상태, 지연 진입·해제·최소 표시, 판정 시작, 첫 신호 기준. 시간을 밖에서 넣어 시험 가능하게. **정적 홀더 `NetworkLinkState`** (Application) — `BlocksPlayerCommands`, 현재 상태 | 최상위 원칙, 규칙 2 확정 D, U-4 |
| 2 | **신호 RPC** — `NetworkGameEndController`에 경기 중 전용 `PresenceHeartbeatServerRpc` / `PresenceHeartbeatClientRpc`(보낸 시각, 되돌림 값) 신설. RTT는 되돌아온 값으로 계산. 결과 화면 신호는 건드리지 않는다. 비신뢰 전송 지정 가능 여부 확인 | U-15, 규칙 17(결과 화면 유지) |
| 3 | **감시기 `InGameLinkWatch`** (Infrastructure, 일반 MonoBehaviour, NetworkObject 아닌 빈 GameObject — `InGameDisconnectObserver.BeginMatch` 생성 방식 선례. **`#if` 개발 블록 밖에서 생성** — 릴리스에서도 동작). 1초 틱(`WaitForSecondsRealtime` 또는 `unscaledTime`), 신호 송수신, `OnClientDisconnectCallback` 구독(경기 중 끊김 → 판정 중), `OnApplicationPause`(2-5), 도달 확인 → 판정. 싱글에서는 만들지 않는다 | 규칙 1 판정 절차, U-3·U-6·U-13 |
| 4 | **로컬 결론** — `NetworkGameEndController`에 역할 무관 메서드 신설(2-4). `_announced`와 같은 중복 깃발 공유. 결과 사유 이벤트는 `Application/Events/GameEvents`에 추가 | 최상위 원칙, U-7·U-8·U-10 |
| 5 | **기존 로직 비활성화** — X-1(`ReconnectionHandler` 구독 주석), X-2(`NetworkContext.Reset` 미루기 — 경기 중 예상 밖 디스폰만), X-3(`GameEndUseCase`가 `OnGameEnd` 수신 시 `IsGameOver` 잠금). 디스폰 뒤 자동으로 RPC를 보내려는 지점(예: `RequestProjectileSnapshotServerRpc` 호출부) 전수 점검 | 규칙 2 확정 E, U-11 |
| 6 | **지연 오버레이 UI** (Presentation) — `GameEvents`/`NetworkLinkState`를 구독해 표시·문구 전환·최소 표시. 씬 배치는 8절 에디터 스크립트. 정렬 순서: 오버레이는 HUD·일반 패널 위, 설정 여는 버튼·설정 창·확인 팝업·결과 화면·로딩 아래 — 공용 `UIManager.ShowBlockingOverlay` 재사용 가능성 확인(Research 2-7의 참조 횟수·`onTap` 덮어쓰기 문제) | U-5, U-17, CanvasSortingOrder 대역 규칙 |
| 7 | **조작 차단 2차 방어** — Research 2-8의 12개 입구 맨 앞에서 `NetworkLinkState.BlocksPlayerCommands`면 요청을 보내지 않고 `CommandBlocked` 로그. 그 UI 상태가 어색하게 남는지 확인 | U-5 |
| 8 | **포기** — `RequestForfeit`에서 2-7 분기. `InGameSettingsUI.OnForfeitConfirmed`는 예외 시에도 창이 닫히는지 확인(필요 시만 수정) | U-9, SettingsUI 규칙 4 |
| 9 | **결과 화면 `GameEndUI`** — 끊긴 뒤에도 카운트다운이 돌게(`ShouldStartAutoReturnCountdown` 조건), 판정 결과 화면 30초, 사유별 문구(2-6), 다시하기 비활성, 로비 복귀 시 정리(`BackToLobby` 또는 그에 준하는 정리)가 빠지지 않게(`ReturnToLobby` 조건) | U-7·U-8, D-2·D-3·D-4·D-7 |
| 10 | **구독 누적 수정** — `NetworkGameManager`의 `OnClientDisconnectCallback`이 `BackToLobby`·`ShutdownNetworkManager` 경로에서 해제되지 않는 문제(Research 2-3). 맵 준비 실패 감지에 영향이 없도록 | U-16 |
| 11 | **진단 로그** — 5절. `DisconnectDiag.DiagRevision`을 2로 | U-14, WORKFLOW [4] 진단 로그 규칙 |

### 수정 파일 (예상 — 확정은 game-programmer 보고)

경로 접두 `Assets/_Project/Scripts/`.

| 파일 | 신규/수정 | 내용 |
|---|---|---|
| `Application/…/InGameLinkMonitor.cs` | 신규 | 상태기계(위치 폴더는 game-programmer 결정) |
| `Application/NetworkLinkState.cs` | 신규 | 정적 홀더 |
| `Application/Events/GameEvents.cs` | 수정 | 링크 상태·결과 사유 이벤트 |
| `Application/UseCases/GameEndUseCase.cs` | 수정 | `OnGameEnd` 수신 시 `IsGameOver` 잠금 |
| `Infrastructure/Network/InGameLinkWatch.cs` | 신규 | 감시기 |
| `Infrastructure/Network/NetworkGameEndController.cs` | 수정 | 신호 RPC, 로컬 결론, 포기 분기 |
| `Infrastructure/Network/ReconnectionHandler.cs` | 수정 | X-1 주석 비활성화 |
| `Infrastructure/Network/NetworkCombatController.cs` | 수정 | X-2, 감시기 시작 |
| `Infrastructure/Network/NetworkGameManager.cs` | 수정 | 구독 누적 |
| `Infrastructure/Network/NetworkBuildingController.cs`, `NetworkProductionController.cs`, `NetworkUpgradeController.cs`, `NetworkSkillController.cs`, `NetworkMistShrineController.cs` | 수정 | 12개 입구 차단 |
| `Infrastructure/Network/DisconnectDiag.cs` | 수정 | `DiagRev=2`, 새 이벤트 |
| `Presentation/UI/…/NetworkLagOverlayUI.cs`(이름 예시) | 신규 | 지연 오버레이 |
| `Presentation/UI/GameEndUI.cs` | 수정 | 결과 화면 |
| `Presentation/UI/InGameSettingsUI.cs` | 필요 시 | 포기 후 창 닫기 |
| `Editor/…`(1회성) | 신규 | 오버레이 씬 배치(8절) |
| `Scenes/Game.unity` | 에디터 스크립트로 변경 | 오버레이 오브젝트 |

---

## 4. 하지 않는 것

- 전적 기록·「지난 경기 결과」 팝업(다음 task — U-7·U-18).
- 앱 강제 종료 대비 「진행 중 표시」 저장(규칙 1 본문) — 전적 task 몫.
- 전송 계층 `DisconnectTimeoutMS` 60초 변경(U-12).
- 결과 화면 이탈 감시(규칙 17)의 동작 변경.
- 재접속(끊긴 기기가 같은 경기로 다시 들어오는 기능).
- 게임 일시정지(U-2).
- 규칙 문서 수정 — 9절 개정안으로만 두고 테스트 후 [11]에서 반영.

---

## 5. 진단 로그

- **공통 형식:** 측정 task와 같다(직전 Plan 2절). `GameLog.Dev` 경유, `[Network/<클래스>]`, 접두어 `[DISC-DIAG] <Event>`, 첫 키 `Diag=Disconnect, DiagRev=2, Event=…, Role=Host|Client`, 시간 키 `UtcMs`·`RtMs`·`MatchSec`. 검색어 `Diag=Disconnect`, 이번 회차만 보려면 `DiagRev=2`.
- **존속 제안:** `개발`(U-14). 테스트 후 유지·삭제는 사용자가 정한다.
- **빈도:** 상태가 바뀔 때만(신호 송수신마다 남기지 않음 — LogRules §1.14 금지 8).
- **민감 정보:** 측정 task와 같다(ClientId만 허용, IP·코드·계정 식별자 금지).
- 측정 task의 기존 이벤트(`MatchStart`, `PeerDisconnected`, `AppPause`, `ServerLostRaised`, `ResultShown`, `MatchSummary` 등)는 그대로 두고 `DiagRev`만 2가 된다.

### 새 이벤트 (키 이름은 제안 — 확정은 game-programmer 보고)

| Event | 남기는 시점 | 주요 키 |
|---|---|---|
| `LinkWatchStart` | 감시기 시작(경기 시작) | `HeartbeatSec=1.0`, `DelaySilenceSec=2.0`, `JudgeSec=15`, `SelfPauseSec=13` |
| `LinkFirstSignal` | 상대 첫 신호 수신 | `SinceStartSec` |
| `LinkDelayEnter` | 지연 진입 | `Trigger=Silence\|Rtt`, `SilenceSec`, `RttMs` |
| `LinkDelayExit` | 지연 해제 | `ShownSec`, `RttMs` |
| `LinkRecovering` | 「연결 복구 중」 표시 | `PausedForSec` |
| `LinkJudgeStart` | 판정 중 진입 | `Trigger=Silence\|DisconnectCallback`, `SilenceSec` |
| `LinkProbeResult` | 도달 확인 끝 | `Probe=Reachable\|Unreachable\|Skipped\|Faulted`, `ProbeMs` |
| `LinkVerdict` | 결론 | `Verdict=OpponentLeft\|SelfLeft`, `Probe=…`, `SinceLastSignalSec` |
| `LinkVerdictSkipped` | 이미 결론이 나 있어 무시 | `Existing=Normal\|Forfeit\|…` |
| `SelfPauseDefeat` | 돌아온 기기 스스로 패배 | `PausedForSec` |
| `OfflineForfeit` | 연결 없는 포기 | `Reason=Delay\|Judging\|NotSpawned\|NotListening\|NotConnected` |
| `CommandBlocked` | 12개 입구에서 차단 | `Command=RequestBuild 등`, `State` (같은 명령 연속은 묶어서 1줄) |
| `LagOverlayTap` | 오버레이가 조작 터치를 막음 | `TapCount`(지연 1회당 첫 1줄 + 해제 때 합계) |
| `SoloModeBlocked` | 경기 중 예상 밖 디스폰에서 `Reset`을 미룸 | `Deferred=True` |
| `NetworkContextReset` | 미뤘던 `Reset` 실행 | `At=SceneUnload` |
| `GraceDisabled` | `ReconnectionHandler` 비활성 확인(스폰 시 1줄) | `Disabled=True` |
| `LinkWatchStop` | 감시기 정리 | `Reason=SceneUnload\|MatchEnd` |
| `ResultShown`(기존 확장) | 결과 화면 표시 | `IsWin`, **`Cause=Normal\|Forfeit\|OpponentLeft\|SelfDisconnected\|SelfPauseDefeat\|OfflineForfeit`**, `Countdown=30` |
| `DisconnectCallbackSub`(신규) | `NetworkGameManager` 끊김 처리기 구독·해제 | `Op=Add\|Remove`, `Count` |

---

## 6. 테스트 항목 ↔ 진단 로그

로그 판정은 메인 세션이 합니다(WORKFLOW [6]). 「양쪽」은 에디터·실기기 두 로그 모두.

| 테스트 항목 | 로그 이름 | 남기는 시점 | 정상 판정 값 |
|---|---|---|---|
| T-1 감시가 양쪽에서 시작 | `LinkWatchStart`, `LinkFirstSignal` | 경기 시작, 첫 신호 | 양쪽에 각 1줄. `Role=Host` 1줄 + `Role=Client` 1줄 |
| T-2 30초 유예가 꺼짐 | `GraceDisabled`, `GraceStart` | Host 스폰 | `GraceDisabled Disabled=True` 1줄, `GraceStart` **0줄** |
| T-3 지연 표시 켜짐 | `LinkDelayEnter` | 지연 진입 | 끊긴 쪽이 아닌 기기: `Trigger=Silence`, `SilenceSec`가 2.0 이상 3.0 미만 |
| T-4 지연 표시 꺼짐 | `LinkDelayExit` | 해제 | `ShownSec` 1.0 이상. 복귀 기기에 `LinkRecovering`(13초 미만) |
| T-5 15초 판정 | `LinkJudgeStart`, `LinkProbeResult`, `LinkVerdict` | 판정 | `Trigger=Silence`, `SilenceSec` 15.0~16.0, 남은 쪽 `Probe=Reachable` `Verdict=OpponentLeft` |
| T-6 끊긴 쪽 판정 | `LinkVerdict` 또는 `SelfPauseDefeat` | 판정·복귀 | 와이파이: `Probe=Unreachable` `Verdict=SelfLeft`. 백그라운드: `SelfPauseDefeat PausedForSec>=13` |
| T-7 결과 화면 | `ResultShown` | 표시 | 남은 쪽 `IsWin=True Cause=OpponentLeft Countdown=30`, 끊긴 쪽 `Cause=SelfDisconnected\|SelfPauseDefeat\|OfflineForfeit Countdown=30` |
| T-8 혼자 진행 없음 | `UAS-SOLO`, `SoloModeBlocked`, `NetworkContextReset` | 끊김 이후 | 경기 중 `UAS-SOLO` **0줄**. 예상 밖 디스폰이 있었으면 `SoloModeBlocked` 뒤 씬 전환 때 `NetworkContextReset At=SceneUnload` |
| T-9 판정 하나만 | `LinkVerdict`, `LinkVerdictSkipped`, `ResultShown` | 결론 | 기기당 `ResultShown` **1줄** |
| T-10 지연 중 조작 차단 | `LagOverlayTap`, `CommandBlocked` | 터치·요청 | 지연 중 터치에 `LagOverlayTap TapCount>=1`. 서버 쪽에 그 조작의 요청 처리 줄 없음. `CommandBlocked`는 0줄이어도 정상(오버레이가 1차로 막으므로) |
| T-11 연결 없는 포기 | `OfflineForfeit`, `ResultShown` | 포기 | `OfflineForfeit` 1줄 → `ResultShown Cause=OfflineForfeit`. 「포기 요청 전송」만 남고 끝나는 경우 **없음** |
| T-12 로비 복귀 | 기존 「로비 복귀 시작」, `LinkWatchStop` | 복귀 | 결과 표시 후 30초 이내(버튼이면 즉시) `LinkWatchStop` |
| T-13 끊김 처리기 중복 없음 | `DisconnectCallbackSub`, `ServerLostRaised` | 구독·끊김 | 여러 판 뒤에도 `Count=1`. 끊김 1회당 `ServerLostRaised` **최대 1줄** |
| T-14 13초 미만 복귀는 이어짐 | `AppPause`, `LinkRecovering`, `LinkDelayExit` | 복귀 | `PausedForSec<13`, `LinkVerdict` 0줄, `ResultShown` 0줄(그 시점) |
| T-15 화면 모습(반투명·글자 위치·설정 버튼만 위) | — | — | **로그로 판정 불가** — 배치·겹침은 눈으로 본다. 사용자에게 알린다 |

---

## 7. 테스트 시나리오

### 7-1. 준비

- **구성:** 에디터 1 + 실기기 1대. **끊는 쪽은 항상 실기기.** 1회차는 실기기 = Host(에디터 = Client), 2회차는 실기기 = Client(에디터 = Host). 커스텀 방. 각 시나리오는 새 경기에서 시작(한 판에서 끝나는 시나리오가 대부분이다).
- 에디터 Console의 **Error Pause를 끈다.** 테스트 중 에디터의 다른 창을 누르지 않는다.
- 실기기 시간은 **자동 설정**(두 기기 `UtcMs` 비교용).
- 빌드: Development 빌드(진단 로그가 개발 로그이므로). WORKFLOW [6] 「빌드 시도」 절차를 따른다.

### 7-2. 시나리오 (각각 1회차·2회차 모두)

| # | 실기기에서 할 일 | 기대 결과 — 실기기 | 기대 결과 — 에디터 |
|---|---|---|---|
| S1 | 경기 중 최근 앱 목록에서 밀어 **강제 종료** | (앱 꺼짐) | 약 2초 뒤 「네트워크 지연」 오버레이 → 약 15~25초에 「승리」 + 「상대방이 떠났습니다」, 30초 뒤 로비. 혼자 진행 없음 |
| S2 | 홈 버튼으로 **5초** 내렸다 복귀 | 복귀 때 「연결 복구 중」 잠깐 → 이어서 진행 | 지연 오버레이 → 신호가 돌아오면 사라지고 이어서 진행. 1회차(실기기 Host)는 그동안 에디터 화면도 멈춰 있을 수 있다(U-2) |
| S3 | **10초** 내렸다 복귀 | S2와 같음 | S2와 같음 |
| S4 | **20초** 내렸다 복귀 | 복귀 즉시 「연결이 끊겼습니다」(승패 없음) → 30초 뒤 또는 로비 버튼으로 로비 | 15초에 판정 → 「승리」 + 「상대방이 떠났습니다」 → 로비 |
| S5 | 와이파이·모바일 데이터를 모두 끄고 **5초** 뒤 켬 | 지연 오버레이 → 사라지고 이어짐 | 지연 오버레이 → 사라지고 이어짐 |
| S6 | 와이파이·**모바일 데이터 모두** 끄고 **30초**(15초 판정을 넘긴 뒤 켬) | 약 15~25초에 「연결이 끊겼습니다」 → 로비(인터넷이 없으므로 로비 화면이 정상 동작하지 않을 수 있다 — 관찰만) | 약 15~25초에 「승리」 + 「상대방이 떠났습니다」 → 로비 |
| S7 | 와이파이·데이터를 끄고 지연 오버레이가 뜨면 **설정 열기 → 포기 → 확인** | 즉시 「연결이 끊겼습니다」 → 로비 | 15초 판정 → 「승리」 |
| S8 | 와이파이·데이터를 끄고 지연 오버레이가 뜬 상태에서 **건물 짓기·생산 버튼 등 조작 시도** 후 5초 안에 켬 | 오버레이가 터치를 막음. 켠 뒤 그 조작이 **반영되지 않음** | 실기기 조작이 반영되지 않음 |

- S6에서 모바일 데이터를 켜 둔 채면 실기기가 데이터로 인터넷 확인에 성공해 **양쪽 모두 「승리」** 가 될 수 있다(Research 7절 「양쪽 모두 승리 주장」). 기대 결과를 분명히 하려고 데이터도 끈다.
- 눈으로 함께 볼 것(T-15): 오버레이가 반투명이고 글자가 가운데인지, 설정 버튼만 눌리는지.

### 7-3. 로그 올리기

- **로그 올리기: 작업명 `ingame-leave-judgment`** (21자 — 통합 수집 메뉴의 1~40자·영문 소문자·숫자·하이픈 제한 안. task 폴더 `10_31_ingame-leave-judgment`에서 시각을 뺀 이름).
- 메뉴 `Hexiege/Logs/Collect Device Logs and Create ZIP` → 세션 선택 → 「선택 앱 로그+Logcat 수집·ZIP」. 측정 task에서 이 ZIP에 에디터 로그도 함께 들어 있음을 확인했다(직전 Research 8-1) — 별도 에디터 ZIP 불필요.
- 업로드·보관은 LogRules §1.10 「원시 테스트 로그 공유·보관」을 따른다.

---

## 8. [5-2] 에디터 1회성 스크립트 — 지연 오버레이 씬 배치

- 지연 오버레이는 `Scenes/Game.unity`에 오브젝트로 배치한다(U-17, 기존 패널 복제 관례 — 예: `GameSystemRules_CanvasSortingOrder.md` Game.unity 표의 「MistShrinePanel — BuildingActionPanel 복제로 생성」).
- game-programmer가 메뉴 `Hexiege/...` 형태의 1회성 에디터 스크립트를 만들고, **사용자가 실행**한 뒤 다음 단계로 간다.
- 정렬 순서(Canvas Override 값)와 설정 버튼을 오버레이 위로 올리는 방법은 스크립트 보고에 적는다.
- 실행 완료 후의 스크립트 처리(비활성화·최종 삭제)는 WORKFLOW [5-2] 「에디터 스크립트 최소 유지 기준」과 [4] 절차를 따른다.

---

## 9. 규칙 개정안 (테스트 후 WORKFLOW [11]에서 반영)

game-design-lead 개정안 요지입니다. **이번에는 문서를 고치지 않습니다.** 테스트 결과로 값·문구가 바뀌면 그에 맞춰 고친 뒤 반영합니다.

### 9-1. `GameSystemRules_Network.md`

- **「🔴 최상위 원칙」:** 「끊김 처리(지연 표시·판정·결과 화면)도 적용 대상」 추가. 「Host 백그라운드 정지로 경기 전체가 멈추는 것은 확정 H의 한계」 명시.
- **「네트워크 동작 규칙」 규칙 1:**
  - 제목에 「2026-11-26 개정」.
  - 「60초」 → **N = 15초(마지막 상대 신호부터)**. 「인게임60초는 전송 계층이 끊김을 통보하는 시점과 같은 시계」 문장은 「전송 계층 60초는 안전망이며 판정 시계가 아니다」로.
  - 표 재편: **포기**(포기한 쪽 패배·상대 승리) / **끊김**(회선·앱 내림·강제 종료 — 15초 판정) / **연결 없는 상태의 포기**(그 기기에서 즉시 패배, 상대는 15초 판정으로 승리). 「로비 버튼/앱 정상 종료」 행 삭제(경기 중 로비 버튼 없음, 스와이프 종료 시 `OnApplicationQuit` 미호출).
  - 이탈 판정 절차에 「인게임은 도달 불가여도 기다리지 않는다」 「판정 뒤 뒤집지 않는다」 「확인하지 못한 경우(`Skipped`)는 도달 불가로 본다」.
  - 「대기(지연·판정 중) 중 성이 파괴되면 게임 결과를 그대로 인정한다」 예외.
  - 「판정 결과는 전적 task의 입력이다」.
  - 남은 한계: 양쪽 패배·양쪽 승리 주장은 전적 task(서버 대조)에서 다룬다.
- **규칙 2:**
  - 확정 D에 「Client 혼자 진행 금지」.
  - 확정 E 제목을 「인게임 판정 시점은 마지막 상대 신호로부터 t=N(15초)이며 30초 유예는 없앤다」로. 재접속 미지원 명시. N=15의 대가(백그라운드 15초 초과 시 패배) 명시.
  - 신규 **확정 F** 지연 중에도 게임은 진행, 지연 중 조작 미반영·재전송 없음, 양쪽 표시. **확정 G** 돌아온 기기 자기 판정(정지 13초 이상 → 스스로 패배). **확정 H** Host 백그라운드 정지 수용(P2P 한계, 악의 없음 전제). **확정 I** 연결 없는 상태의 포기.

### 9-2. `GameSystemRules_MatchFlowUI.md` — 신규 절 「인게임 연결 상태 UI」

| 규칙 | 내용 |
|---|---|
| C-1 | 지연 표시 조건(2-1 값)과 모양 — 반투명 오버레이 + 가운데 「네트워크 지연」 |
| C-2 | 설정 버튼만 오버레이 위, 그 밖의 게임 조작 입력 차단 |
| C-3 | 「연결 복구 중」(13초 미만 복귀, 최소 0.5초) |
| C-4 | 판정 결과 화면 — 남은 쪽: 「승리」 + 「상대방이 떠났습니다」 |
| C-5 | 끊긴 쪽: 승패 없이 「연결이 끊겼습니다」 + 「n초 뒤 로비로 이동합니다」 |
| C-6 | 연결 없는 포기 흐름 |

- 기존 D-7 상태 줄 표에 끊긴 쪽 상태 추가와 우선순위, D-2 다시하기 비활성 조건에 「내가 끊김」 추가, D-4에 「판정 결과 화면은 30초」.

### 9-3. 그 밖

- `GameSystemRules_SettingsUI.md` 규칙 4 「포기 결과」: 「연결이 없으면 그 기기에서 즉시 처리한다」 한 줄.
- `GameSystemRules_CanvasSortingOrder.md` 「전역 SortingOrder 대역 규칙」·「Game.unity」 표: 지연 오버레이와 설정 버튼의 정렬 값(구현 확인 후).
- `GameSystemRules_MatchFlow.md` 규칙 17: 「인게임 침묵 시간은 결과 화면 감시로 이어받지 않는다」.
- ※ 지연 중 조작 시 토스트(「NetworkLagInputBlocked」)안은 사용자 결정(오버레이로 차단)으로 불필요해 넣지 않는다.

---

## 10. 위험·미확인

| 항목 | 내용 | 다루는 곳 |
|---|---|---|
| 양쪽 승리 / 양쪽 패배 | Relay 경로만 끊김 또는 양쪽 도달 확인 실패 | 전적 task(Research 7절) |
| 판정 시간 15~25초 | 도달 확인 최대 10초 | 수용. 로그 `ProbeMs`로 실측 |
| 경기 시작 직후 오판 | 첫 신호 기준 | 2-2, game-programmer 보고 |
| `Reset` 미루기의 영향 | 디스폰~씬 전환 구간의 RPC 자동 송신 | 3단계 5 전수 점검 |
| 공용 막 재사용 | 참조 횟수·`onTap` 덮어쓰기, 가운데 글자 자리 없음 | 3단계 6 |
| 끊긴 뒤 로비 정리 | `SceneLoader.Load`만으로 복귀할 때 남는 상태 | 3단계 9 |
| 12개 입구 차단 뒤 UI 상태 | 「요청 중」 등이 남는지 | 3단계 7 |
| 비신뢰 RPC 속성 | NGO 2.9.2 확인 필요 | 3단계 2 |
| `DisconnectTimeoutMS` 공개 속성 | `LinkWatchStart`에 실제 값을 남기려면 필요 | 11절 |
| 복귀 프레임 순서 | `OnApplicationPause(false)`와 틱의 선후 | 2-5 |
| Client 끊김 콜백·디스폰 선후 | X-2 판정 조건에 영향 | 11절 |
| 상대 정상 `Shutdown` 시 통보 즉시성 | 즉시 오면 판정이 15초보다 빨라짐(정상) | 테스트로 관찰 |
| 2-3 5번(판정 뒤 신호 회복 무시) | 사용자 결정 원문에 없는 세부 | **Plan 공유 때 사용자 확인** |

---

## 11. game-programmer 확인 항목 (구현 보고에 포함)

1. 최종 수정 파일 표와 새 클래스의 위치(폴더).
2. 비신뢰 RPC 지정 가능 여부와 실제로 쓴 전송 방식.
3. 대기 상태(첫 신호 미수신)가 길어질 때의 처리.
4. X-2의 「경기 중 예상 밖 디스폰」 판별 조건, 디스폰 뒤 RPC 자동 송신 지점 전수 점검 결과.
5. `ForceWin`의 다른 호출부 유무(X-4).
6. 지연 오버레이의 정렬 값과 설정 버튼을 위로 올린 방법, 공용 막 재사용 여부와 이유.
7. 12개 입구 차단 뒤 각 UI의 상태.
8. 끊긴 뒤 로비 복귀 정리 방식.
9. `NetworkGameManager` 구독 정리 위치와 맵 준비 실패 감지 영향.
10. 테스트 항목별 로그 이름·검색어·정상 판정 값(6절과 다르면 그 차이).
11. 에디터 1회성 스크립트 메뉴 경로.
