# 인게임 연결 끊김 실측 — Plan

## 무엇을 왜 하나요

경기 도중 한쪽 연결이 끊겼을 때 **실제로 무슨 일이 몇 초 뒤에 일어나는지**를 기기 두 대로 재기 위한 계획입니다. 조사 결과([Research](Research.md))로는 「중계 서버는 10초면 끊는다」는 공식 문서와 「우리 설정은 60초」가 서로 어긋날 수 있고, 어느 쪽이 실제로 작동하는지는 재 봐야 압니다.

그래서 이번에는 **게임 동작을 한 줄도 바꾸지 않고**, 끊김과 관련된 순간(연결 끊김 감지, 30초 대기 시작·종료, 앱을 내리고 올린 순간, 결과 화면이 뜬 순간 등)마다 **기록(진단 로그)만 한 줄씩 남기게** 합니다. 그다음 사용자가 기기 두 대로 6가지 상황을 직접 만들고, 남은 로그의 시각을 읽어 다음 두 가지를 정합니다.

- **R1** — 끊김을 확정하는 시간을 몇 초로 할지
- **R4** — `GameSystemRules_Network.md` 규칙 2 확정 E의 문구를 어떻게 고칠지

전적 기록은 이 측정이 끝난 뒤 **별도 작업**으로 진행합니다.

> 🔴 **기존 로직 제거/비활성화: 없음.** 측정은 **현재 동작 그대로** 재야 하므로, 30초 대기·강제 승리·끊김 처리 등 어떤 기존 처리도 끄거나 바꾸지 않는다. 추가되는 것은 기록(로그) 줄과, 기록만 하는 받는 자리(앱 일시정지·복귀 등)뿐이다. 기존 로그 줄에 값(`key=value`)을 덧붙이는 경우도 **조건문·호출 순서·반환은 그대로** 둔다.

- 구현 담당: **game-programmer**(이 문서는 설계표만 정한다. 실제 수정 파일·API 사용 가능 여부는 game-programmer가 코드를 보고 보고한다).
- 상태: **Plan 작성 — 사용자 승인 대기** (2026-11-26).

---

## 1. 근거 규칙

| 근거 | 내용 | 이번 작업과의 관계 |
|---|---|---|
| `GameSystemRules_Network.md` 「🔴 최상위 원칙」 | 화면·승패·이탈 처리가 Host/Client 역할로 갈리면 결함 | 모든 측정을 **Host 쪽 끊김 / Client 쪽 끊김 각각** 잰다. 모든 로그에 `Role`을 넣는다 |
| 같은 문서 「네트워크 동작 규칙」 규칙 1 | 인게임 이탈의 승패 기록(회선 끊김·앱 강제 종료 → 남은 쪽 60초 뒤 승리), 「인게임 60초는 전송 계층이 끊김을 통보하는 시점과 같은 시계」 | 「끊김 통보가 실제로 몇 초에 오는가」를 잰다(측정 질문 (1)) |
| 같은 절 규칙 2 확정 D | 인게임 판정도 양쪽이 각자 돌린다(미구현) | Host가 사라졌을 때 Client 쪽에서 실제로 무엇이 관측되는지 기록한다(Research 1-4) |
| 같은 절 규칙 2 확정 E | `t<60` 구간이 재접속 허용 시간, `t=60`에서 승부 결정 | 🔴 **충돌 가능성 — 실측으로 판정**(Research 3절 4). 이번에 문구는 고치지 않는다(R4는 측정 후) |
| `GameSystemRules_MatchFlow.md` 규칙 17 | 결과 화면 이탈 감시(30초 자체 감시)는 인게임과 다른 시계 | 결과 화면 진입 시각만 기록하고, 결과 화면 감시 동작은 건드리지 않는다 |
| WORKFLOW [4] 「테스트 항목 진단 로그 규칙」 | 테스트 항목마다 로그로 판정할 수 있게, 대응표를 Plan에 둔다 | 아래 4절 |
| [LogRules](../../../LogRules.md) §1.2 · §1.4 · §1.5 · §1.6 · §1.14 | 두 축, `key=value` 형식, 이벤트 키, 민감 데이터, 금지 사항 | 아래 2절 |

---

## 2. 진단 로그 공통 형식

- **경유:** `GameLog.Dev.Info` (예상 밖 상황은 `GameLog.Dev.Warn`). raw `Debug.Log` 금지(LogRules §1.14 금지 1).
- **두 축(§1.2):** 심각도 — 측정용 관찰이므로 기본 `Info`, 끊김·전송 실패처럼 「예상 밖이지만 게임은 계속」인 순간은 `Warn`. 존속 — **`개발` 제안**(측정 뒤 전적 task 검증에도 다시 쓸 수 있으므로). 측정만 하고 지울지(`임시`)는 **테스트 후 사용자가 정한다.** `임시`로 정해지면 §1.14 금지 10에 따라 코드를 제거한다.
- **카테고리(§1.4):** `[Network/<출력하는 클래스>]`. 앱 일시정지처럼 UI가 아닌 것도 「끊김 측정」 기능이므로 `Network`로 둔다(System은 기능 기준).
- **이벤트 키(§1.5):** `개발` 로그라 `LogEvent` enum 키는 받지 않는다. 대신 `Event=` 필드 값으로 사건을 가르며, 이름은 「무엇이 일어났는지」로 짓고 한 번 정하면 바꾸지 않는다.
- **회차 구분 접두어·버전 키:** 새 줄의 메시지 앞에 `[DISC-DIAG] <Event>`. 모든 줄(기존 줄에 덧붙이는 경우 포함)의 첫 키를 `Diag=Disconnect, DiagRev=1, Event=…, Role=Host|Client` 로 둔다. **검색어는 `Diag=Disconnect`** 하나로 전부 잡힌다. 진단 내용을 바꾸면 `DiagRev`를 올린다.
- **시간 키:** 모든 줄에 `UtcMs`(기기 시계, 1970 기준 ms — 두 기기 비교용)와 `RtMs`(앱 시작 후 실제 경과 ms). 가능하면 `MatchSec`(경기 시작 기준 경과초, 소수 1자리)도 넣는다. 경기 시작 기준점을 어느 계층에 둘지는 game-programmer가 아키텍처 제약(`.claude/MEMORY.md` 「아키텍처 핵심 제약」)에 맞춰 정한다 — 둘 수 없는 클래스는 `UtcMs`만 남기고 분석 때 `MatchStart` 줄과의 차로 계산한다.
  - ⚠️ Android에서 앱이 멈춘 동안 `RtMs`가 흐르는지는 **미확인**이다. 그래서 일시정지 구간 길이는 `UtcMs` 차로 잰다.
- **값 표기(§1.4):** 값에 `, `를 넣지 않는다. `DisconnectReason` 문자열처럼 자유 문장이 올 수 있는 값은 `, `를 `;`로 바꾸고 120자로 자른다. 비어 있으면 `-`.
- **민감 데이터(§1.6 · §1.14 금지 4):** **PlayerId·UID·이메일·인증 토큰·Relay Join Code·Lobby 코드·Allocation ID·IP 주소를 넣지 않는다.** NGO `ClientId`(경기마다 0·1·2처럼 매기는 세션 번호)는 계정 식별자가 아니므로 넣는다.
- **빈도(§1.14 금지 8):** 상태가 바뀔 때만 남긴다. 매 프레임·매 틱 금지. 경기당 예상 줄 수는 수십 줄 이하.
- **같은 사건 두 번 금지(§1.14 금지 9):** 기존 로그 줄이 이미 그 순간을 찍고 있으면 **새 줄을 만들지 않고 기존 줄에 `Diag` 키를 덧붙이는 것**을 기본으로 한다(아래 표의 「기존 줄」 표시). 메시지 문장은 바꾸지 않는다.

---

## 3. 로그 지점 설계

경로 접두 `Assets/_Project/Scripts/`. 「새 줄 / 기존 줄」 — 기존 줄은 이미 있는 `GameLog` 호출에 키만 덧붙인다.

| # | 기록할 순간 | 위치(후보) | 역할 | Event · 핵심 키 | 새 줄 / 기존 줄 | 심각도 |
|---|---|---|---|---|---|---|
| ⓪ | 경기 시작 — 측정 기준점 | 게임 씬 네트워크 시작 직후 1회(위치는 game-programmer 결정) | 양쪽 | `MatchStart` `MatchNo`(앱 실행 후 몇 번째 경기), `DisconnectTimeoutMs`, `HeartbeatTimeoutMs`, `MaxConnectAttempts`(실제 실행 중 Transport 값), `RunInBackground`, `AppVersion` | 새 줄 | Info |
| ① | 연결 끊김 콜백 | `NetworkManager.OnClientDisconnectCallback`을 구독하는 **기록 전용 관찰자**(Host·Client 양쪽에서 동작) | 양쪽 | `PeerDisconnected` `ClientId`, `LocalClientId`, `IsSelf`, `Reason`(NGO `DisconnectReason`), `MatchSec`, `SincePeerSignalSec`(⑥이 가능할 때), `LastRttMs`, `PausedRecently`(직전 일시정지 이후 복귀 몇 초 이내인지), `DisconnectNo` | 새 줄 | Warn |
| ② | 전송 계층 실패 | `NetworkManager.OnTransportFailure` 구독(같은 관찰자) | 양쪽 | `TransportFailure` `MatchSec`, `SincePeerSignalSec` | 새 줄 | Warn |
| ③-a | 30초 대기 시작 | `Infrastructure/Network/ReconnectionHandler.cs` `OnClientDisconnected`의 「클라이언트 연결 끊김 — 재접속 대기 시작」 | Host | `GraceStart` `ClientId`, `WaitSeconds`, `MatchSec` | **기존 줄** | Warn(기존 그대로) |
| ③-b | 30초 대기 취소 | 같은 파일 `OnClientReconnected`의 「클라이언트 재접속 확인 — 강제 승리 대기 취소」 | Host | `GraceCancel` `ClientId`, `MatchSec` | **기존 줄** | Info |
| ③-c | 30초 대기 만료 → 강제 승리 | 같은 파일 `WaitAndForceWin`의 「재접속 타임아웃 — 강제 승리 처리」 + `Infrastructure/Network/NetworkGameEndController.cs` `ForceWin`의 「ForceWin 호출 — …」 | Host | `GraceExpired` `WinnerTeam`, `GraceElapsedSec`(대기 시작부터 실제 걸린 초 — 백그라운드로 멈춘 시간이 섞였는지 보기 위함), `MatchSec` / `ForceWin` `WinnerTeamIndex` | **기존 줄** | Info |
| ④ | 연결(재접속) 콜백 | `NetworkManager.OnClientConnectedCallback`(①과 같은 관찰자, 경기 시작 이후에만) | 양쪽 | `PeerConnected` `ClientId`, `IsSelf`, `SameAsLastDisconnected`(Host: 마지막으로 끊긴 ClientId와 같은가), `MatchSec` | 새 줄 | Info |
| ⑤ | 앱 일시정지·복귀·포커스 | 게임 씬에 **기록만 하는 받는 자리 신설**(`OnApplicationPause(bool)` · `OnApplicationFocus(bool)`) | 양쪽 | `AppPause` `Paused=True\|False`, `MatchSec`, 복귀 때 `PausedForSec`(`UtcMs` 차), `IsListening`, `IsConnectedClient`, `ConnectedCount` / `AppFocus` `Focused` | 새 줄 | Info |
| ⑥ | 상대 신호 끊김·회복 | **방법 미정 — game-programmer 확인 항목**(아래 3-1) | 양쪽 | `PeerSilenceStart` `MatchSec`, `LastRttMs` / `PeerSilenceEnd` `SilenceSec` | 새 줄 | Info |
| ⑦ | Client 쪽 서버 끊김 감지 | `Infrastructure/Network/NetworkGameManager.cs` `HandleClientDisconnected`의 「클라이언트 측 서버 연결 끊김 감지 — OnServerDisconnected 발행」 | Client | `ServerLostRaised` `ClientId`, `MatchSec` | **기존 줄** | Warn(기존 그대로) |
| ⑧ | 결과 화면이 뜬 순간 | `Presentation/UI/GameEndUI.cs` `OnGameEnd`(결과 표시 직후) | 양쪽 | `ResultShown` `IsWin`, `MatchSec` | 새 줄 | Info |
| ⑨ | 경기 요약 | 게임 씬을 떠날 때 1회(위치는 game-programmer 결정) | 양쪽 | `MatchSummary` `DisconnectNo`, `ConnectNo`, `PauseNo`, `TransportFailureNo`, `ResultShown=True\|False`, `MatchSec` | 새 줄 | Info |

- ①과 ⑦은 Client에서 같은 콜백에서 나온다. ①은 「끊김을 받았다」, ⑦은 「그 결과 `OnServerDisconnected`를 발행했다」(맵 준비 중이면 ⑦ 대신 맵 준비 실패로 간다)라 사실이 다르다. game-programmer가 보고 사실상 같은 순간이면 ⑦을 ①에 합치고 보고한다.
- ①의 Host 쪽과 ③-a도 같은 순간이지만 ③-a는 「대기를 시작했다」(이미 강제 승리가 났으면 시작하지 않음)라 다른 사실이다.
- 관찰자의 구독·해제는 기존 처리기의 구독 순서·해제에 영향을 주지 않게 별도로 한다. 관찰자가 예외를 던져도 다른 처리기가 계속 돌도록 본문을 `try`로 감싸는 것을 권장(game-programmer 판단).

### 3-1. ⑥ 「마지막 상대 신호 수신 시각」을 얻는 방법 — game-programmer 확인 항목

동작에 영향이 없는 방법만 쓴다. 아래는 후보이며 **사용 가능 여부는 모두 미확인**이다(로컬에 패키지 소스가 없어 확인하지 못함).

| 후보 | 방식 | 확인할 것 |
|---|---|---|
| A | Transport/NGO가 공개한 「마지막 수신 시각」 값이 있으면 읽는다 | NGO 2.9.2 / Transport 2.6.0에 그런 공개 API가 있는가 |
| B | 1초에 한 번 RTT(`NetworkGameManager.GetCurrentRttMs`와 같은 `UnityTransport.GetCurrentRtt`)를 **읽기만** 하고, 값이 2초 이상 그대로면 `PeerSilenceStart` 1줄, 다시 바뀌면 `PeerSilenceEnd` 1줄 | RTT가 「상대에게서 패킷을 받을 때만」 갱신되는가. 읽기가 부작용이 없는가. 1초 확인 자체는 로그를 남기지 않는다(금지 8 준수) |
| C | 상대가 주기적으로 보내는 기존 동기화 메시지가 있으면 그 수신 시각을 기록 | 경기 중 항상 오가는 기존 메시지가 있는가. 수신 처리 순서를 바꾸지 않는가 |

- 셋 다 안전하지 않으면 ⑥은 넣지 않고, 4절 표에 「로그로 판정 불가 — 끊김 콜백 시각과 조작 시각으로만 잰다」로 적은 뒤 사용자에게 알린다.
- `SincePeerSignalSec`(① · ②)는 ⑥이 가능할 때만 채운다. 불가능하면 `-`.

### 3-2. 수정 파일 후보 (확정은 game-programmer 보고)

| 파일 | 할 일 |
|---|---|
| (신설 후보) `Infrastructure/Network/` 아래 기록 전용 관찰자 1개 | ⓪ · ① · ② · ④ · ⑤ · ⑥ · ⑨. 게임 씬에서 양쪽 다 동작 |
| `Infrastructure/Network/ReconnectionHandler.cs` | ③-a · ③-b · ③-c 기존 줄에 키 덧붙이기. 동작 변경 금지 |
| `Infrastructure/Network/NetworkGameEndController.cs` | ③-c `ForceWin` 기존 줄에 키 덧붙이기 |
| `Infrastructure/Network/NetworkGameManager.cs` | ⑦ 기존 줄에 키 덧붙이기 |
| `Presentation/UI/GameEndUI.cs` | ⑧ 새 줄 |
| `Scenes/Game.unity` | 관찰자를 씬에 둬야 하는 방식이면 배치(Inspector 작업이 생기면 WORKFLOW [5-2] 절차). 코드만으로 붙일 수 있으면 씬 수정 없음 — game-programmer가 방식과 함께 보고 |

---

## 4. 테스트 항목 ↔ 진단 로그

이번 테스트는 「맞다/틀리다」를 가리는 것이 아니라 **시각을 재는 것**이므로 마지막 열을 「정상 판정 값」 대신 **「측정값으로 읽을 값」**으로 둔다. 조작 시각(`t=0`)은 사용자가 따로 재지 않는다 — 끊긴 쪽 기기의 로그(`AppPause Paused=True`의 `UtcMs`, 강제 종료는 마지막 줄)로 정한다.

| 테스트 항목 | 로그 이름 (`Diag=Disconnect` + Event) | 남기는 시점 | 측정값으로 읽을 값 |
|---|---|---|---|
| 측정 기준·설정 확인 (전 시나리오) | `MatchStart` | 경기 시작 1회 | 양쪽 `DisconnectTimeoutMs=60000`, `HeartbeatTimeoutMs=500`, `Role`이 한쪽 Host·한쪽 Client인지. 다르면 그 회차 측정 무효 |
| (1) 남은 쪽이 끊김을 아는 시각 | 남은 쪽 `PeerDisconnected` · `TransportFailure` · `ServerLostRaised`(Client) | 콜백 발생 1회 | `남은 쪽 UtcMs − 끊긴 쪽 t=0 UtcMs` = 몇 초(약 10초 근처인가, 60초 근처인가). `Reason` 문자열 |
| (1) Host가 남았을 때 강제 승리까지 | `GraceStart` → `GraceExpired` → `ForceWin` → `ResultShown` | 각 1회 | `GraceStart − t=0`, `GraceElapsedSec`(30 근처인가), `ResultShown − t=0`(총 몇 초 만에 결과 화면) |
| (1)·(4) Client가 남았을 때 (Host 끊김) | Client `PeerDisconnected` · `ServerLostRaised` · `ResultShown` 유무 | 각 1회 | 끊김 통보 시각, **`ResultShown` 줄이 있는가**(Research 1-4대로라면 없을 것으로 보이나 미확인), `MatchSummary ResultShown=` |
| (2) 10초 넘게 끊겼다 돌아온 쪽 | 돌아온 쪽 `AppPause Paused=False`의 `PausedForSec`·`IsListening`·`IsConnectedClient` → 그 뒤 `PeerDisconnected`(IsSelf) · `TransportFailure` · `PeerSilenceEnd` | 복귀·콜백 각 1회 | 복귀 직후 아직 연결로 보이는가, 복귀 후 몇 초에 끊김을 받는가, 끊기지 않고 이어지면 `PeerSilenceEnd SilenceSec` |
| (2) 재접속 취소가 일어나는가 | Host `PeerConnected` · `GraceCancel` | 각 1회 | `PeerConnected`가 오는가, `SameAsLastDisconnected`(새 ClientId면 False), `GraceCancel` 0줄인가 |
| (3) 백그라운드 시 실제 끊김 시점 | 내린 쪽 `AppPause` / 남은 쪽 `PeerSilenceStart` · `PeerDisconnected` | 각 1회 | 5초·20초·70초 각각에서 남은 쪽이 끊김을 받았는가, 받았다면 `t=0` 뒤 몇 초인가 |
| (4) 역할별 비교 | 위 전부 `Role` 필드 | — | 같은 시나리오의 Host 끊김 회차와 Client 끊김 회차 값을 나란히 비교 |
| 측정이 동작을 바꾸지 않았는가 | `MatchSummary` + 기존 로그 | 경기 1회 | 기존 ReconnectionHandler 줄의 순서·횟수가 진단 추가 전과 같은 형태인가(`GraceStart` 1회 → `GraceExpired` 1회). 경기당 `Diag=Disconnect` 줄 수가 수십 줄 이하인가 |
| S1 앱 강제 종료의 `t=0` | (로그로 정밀 판정 불가) | — | 🔴 밀어서 종료하면 `OnApplicationQuit`가 불리지 않는다(공식 문서). 종료 직전 `AppPause Paused=True` 줄이 남으면 그 `UtcMs`를 `t=0`으로 쓰고, 남지 않으면 끊긴 쪽 **마지막 로그 줄의 시각**을 근사값으로 쓴다(1초 안팎 오차). 사용자에게 이 한계를 알린다 |

- 두 기기의 `UtcMs`를 빼는 측정이므로 **두 기기 모두 「날짜 및 시간 자동 설정」이 켜져 있어야 한다**(기기 시계 차이가 그대로 오차가 된다). 초 단위 판정에는 충분하다.

---

## 5. 기기 2대 테스트 시나리오

### 5-1. 준비

- 기기 A·B 둘 다 이번 빌드(Development 빌드)를 설치한다. 두 기기 모두 시계 자동 설정을 켠다.
- **역할을 고정하기 위해 커스텀 방으로 경기한다.** 코드상 방을 만드는 쪽(`NetworkGameManager.HostGameAsync`)이 Host, 방 코드로 들어가는 쪽(`JoinGameAsync`)이 Client다. 랜덤 매칭은 누가 Host가 될지 고를 수 없다. 커스텀과 랜덤 매칭은 둘 다 같은 Relay 접속 경로(`RelayManager.CreateRelayAsync` / `JoinRelayAsync`)와 같은 전송 설정을 쓰므로 측정 결과를 랜덤 매칭에도 적용할 수 있다고 보지만, 그 밖의 차이는 미확인이다. 로그의 `Role`로 실제 역할을 다시 확인한다.
  - ⚠️ 화면의 실제 버튼 이름(「방 만들기」 등)은 이 문서에서 확인하지 못했다. 메인 세션이 테스트 안내 전에 화면 문구를 확인해 적는다.
- **각 시나리오를 2회** 한다 — 1회차: **끊는 쪽 = Host**(방 만든 기기), 2회차: **끊는 쪽 = Client**(들어간 기기). 합계 6 × 2 = 12판.
- 한 판마다: 경기가 시작되고 **30초쯤 지나 양쪽 화면이 정상으로 움직이는 것을 본 뒤** 조작한다. 조작 뒤에는 **최소 2분** 기다리며 두 화면을 본다(60초 설정 + 30초 대기를 넘기기 위함). 한 판이 끝나면 양쪽 다 로비로 돌아가 다음 판을 시작한다(끊긴 쪽 앱이 꺼졌으면 다시 켠다).

### 5-2. 시나리오

| # | 끊는 쪽 기기에서 할 조작 (쉬운 말) | 화면에서 볼 것 (눈으로 본 이상만 알려 주면 됨) |
|---|---|---|
| S1 | **앱 강제 종료.** 최근 앱 목록(네모 버튼 또는 화면 아래에서 위로 밀어 멈추기)을 열고 Hexiege 화면을 위로 밀어 없앤다 | 남은 쪽: 결과 화면이 뜨는가, 뜬다면 대략 몇 초 뒤인가(시계를 볼 필요는 없다 — 「1분쯤」, 「안 뜸」 정도). 상대 유닛이 멈추는가 |
| S2 | **잠깐 내리기 5초.** 홈 버튼(또는 홈으로 가는 제스처)으로 게임을 내리고, 속으로 5초 센 뒤 최근 앱 목록에서 Hexiege를 눌러 돌아온다 | 양쪽: 경기가 그대로 이어지는가, 멈췄다가 따라잡는가, 결과 화면이 뜨는가 |
| S3 | **내리기 20초.** S2와 같고 20초 기다렸다 돌아온다 | 양쪽: 위와 같음. 돌아온 쪽 화면이 멈춰 있거나 이상한가 |
| S4 | **내리기 70초.** S2와 같고 70초(1분 10초) 기다렸다 돌아온다 | 양쪽: 위와 같음. 남은 쪽은 기다리는 동안 결과 화면이 뜨는가 |
| S5 | **인터넷 끊기 20초.** 빠른 설정(화면 위에서 아래로 내리기)에서 와이파이와 모바일 데이터를 **둘 다** 끄고, 20초 뒤 다시 켠다. 게임은 내리지 않는다 | 양쪽: 끊긴 동안·다시 켠 뒤 경기가 이어지는가, 결과 화면이 뜨는가 |
| S6 | **인터넷 끊기 70초 유지.** S5와 같고 70초 동안 꺼 둔다(그 뒤 다시 켜도 된다) | 양쪽: 위와 같음 |

- 남은 쪽 기기는 아무것도 하지 않고 화면만 본다. 시간을 잴 필요는 없다 — 시각은 로그로 잰다.
- 와이파이만 끄면 데이터로 자동 전환될 수 있으므로 S5·S6은 **둘 다** 끈다.
- 기기에 따라 버튼 위치가 다르다. 위 조작이 안 되면 그 판은 건너뛰고 알려 준다.

### 5-3. 로그 올리기

- **작업명: `ingame-disconnect-measure`** (task 폴더 `05_28_ingame-disconnect-measure`에서 시각을 뺀 이름. 25자, 영문 소문자·하이픈만 — 통합 수집 메뉴의 1~40자 제한 안).
- 기기가 끼는 테스트이므로 **통합 수집 메뉴** `Hexiege/Logs/Collect Device Logs and Create ZIP` 창에서 세션을 고르고 「선택 앱 로그+Logcat 수집·ZIP」으로 수집한다 — 그래야 기기 쪽 `Dev.Info` 진단(이번 로그 전부)이 들어온다(WORKFLOW [6] 「테스트 로그 올리기」). **기기 A·B 둘 다** 수집해야 한다.
- 재테스트도 같은 작업명을 쓴다(회차는 도구가 붙인다). 매달 첫 업로드 처리와 보관 규칙은 WORKFLOW [6]과 LogRules §1.10을 따른다.
- 로그 판정은 메인 세션이 한다. 사용자는 위 조작을 하고 눈으로 본 이상만 알린다.

---

## 6. 측정 뒤 결정할 것 (이번 구현 범위 아님)

| 항목 | 측정에서 읽을 근거 |
|---|---|
| R1 끊김 확정 시간 | 4절 (1)·(3)의 「남은 쪽이 끊김을 아는 시각」 분포, (2)의 「돌아와서 이어질 수 있는 최대 시간」 |
| R4 규칙 2 확정 E 문구 | `t<60` 동안 연결이 실제로 붙들려 있었는가(S3·S5에서 이어졌는가), `t=60` 통보가 실제로 60초에 오는가 |
| 진단 로그 존속 | 사용자가 `개발` 유지 / `임시`(제거) 중 선택 |

규칙 문서(`GameSystemRules_Network.md` 규칙 1·2)의 수정은 이 결정 뒤 후속 task에서 한다(R3 「앱 정상 종료」 행 합치기 포함 — Research 6절).

---

## 7. 위험 요소

| 위험 | 대응 |
|---|---|
| 🔴 **진단 로그가 동작을 바꿈** — 측정 자체가 무의미해진다 | 기존 처리기의 조건문·호출 순서·반환·구독 순서를 바꾸지 않는다. 관찰자는 별도 구독, 본문 예외가 밖으로 나가지 않게 한다. ⑥ 후보 B·C는 「읽기만」이 확인될 때만 넣는다. 30초 대기(`_reconnectWaitSeconds`, 씬 값 30)·60초 설정은 건드리지 않는다 |
| 일시정지 받는 자리 신설이 기존 일시정지 처리와 겹침 | 게임 씬에는 기존 `OnApplicationPause`·`OnApplicationFocus` 처리가 없다(Research 1-5 — `InputFormController`에만 있음). 새 자리는 로그만 남기고 아무 상태도 바꾸지 않는다 |
| 로그 과다 | 상태 변화 때만. ⑥의 1초 확인은 로그를 남기지 않고 전환 때만 1줄. 경기당 수십 줄 이하 목표 |
| 민감 정보 유출 | PlayerId·UID·Join Code·Lobby 코드·Allocation ID·IP·토큰 금지(2절). `Reason` 문자열에 이런 값이 섞일 수 있는지 game-programmer가 확인하고, 섞일 수 있으면 길이·종류만 남긴다 |
| Android Development 빌드의 `Dev.Info`는 기기 앱 로그 파일에만 남는다 | 통합 수집 메뉴로 기기 앱 로그를 수집한다(5-3). 에디터용 메뉴만 쓰면 기기 쪽 로그가 빠진다 |
| 강제 종료(S1)의 `t=0`이 정확하지 않음 | 4절 마지막 행 — 근사값임을 결과에 함께 적는다 |
| 두 기기 시계 차이 | 시계 자동 설정. 초 단위 판정만 한다 |
| 커스텀 방과 랜덤 매칭의 차이 | 같은 Relay 경로·전송 설정임을 코드로 확인. 그 밖의 차이는 미확인으로 남긴다(5-1) |
| 아키텍처 제약(계층 방향) | 경기 시작 기준점·관찰자 위치를 game-programmer가 `.claude/MEMORY.md` 「아키텍처 핵심 제약」에 맞춰 정한다. Presentation(`GameEndUI`)은 `GameLog`(Application)만 쓴다 |

---

## 8. game-programmer 확인 항목 (구현 보고에 포함)

1. NGO 2.9.2 / Transport 2.6.0에서 `NetworkManager.OnTransportFailure`, `NetworkManager.DisconnectReason`을 쓸 수 있는가, Host·Client 각각에서 어떤 값이 오는가.
2. 3-1의 ⑥ 후보 A·B·C 중 동작에 영향 없이 쓸 수 있는 것.
3. Client가 Host 끊김을 받을 때 콜백의 `clientId` 값(자기 ID인가, 서버 ID인가) — 로그로도 남는다.
4. 관찰자 배치 방식(코드만 / 씬 배치)과 `Scenes/Game.unity` 수정 여부.
5. 실제 수정 파일 목록, 테스트 항목별 로그 이름·검색어(`Diag=Disconnect`)·읽을 값 — 메인 세션이 [6] 테스트 안내에 옮긴다(WORKFLOW [4]).
