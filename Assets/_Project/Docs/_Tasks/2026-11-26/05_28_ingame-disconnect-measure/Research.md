# 인게임 연결 끊김 실측 — Research

## 무엇을 왜 하나요

다음 큰 작업은 **전적(승·패) 기록**입니다. 전적을 정확히 남기려면 「경기 도중 한쪽 연결이 끊기면 누가 이긴 것으로 할지, 그리고 그것을 **언제** 정할지」가 먼저 정해져야 합니다.

그런데 지금 코드와 Unity 공식 문서를 함께 읽어 보니, 끊김이 실제로 **언제** 양쪽에 알려지는지는 문서만으로 답이 나오지 않았습니다. 예를 들어 공식 문서는 「중계 서버(Relay)가 10초 동안 신호가 없으면 그 사람을 끊는다」고 하는데, 우리 게임 설정에는 「60초 동안 아무것도 안 오면 끊김」이라고 적혀 있습니다. 둘 중 어느 쪽이 먼저 작동하는지, 남은 사람이 그것을 몇 초 뒤에 알게 되는지는 **실제로 재 봐야** 압니다.

그래서 이번 작업은 **게임 동작을 하나도 바꾸지 않고, 끊김과 관련된 순간마다 기록(진단 로그)만 남기게** 한 뒤, 기기 두 대로 여러 상황(앱 강제 종료, 홈 버튼으로 내리기, 와이파이 끄기)을 직접 만들어 시간을 잽니다. 그 측정 결과로 두 가지를 정하고, 전적 기록은 그다음 별도 작업에서 진행합니다.

- **R1** — 끊김을 「확정」하는 시간을 몇 초로 할지
- **R4** — 확정 규칙 문서(`GameSystemRules_Network.md` 규칙 2 확정 E)의 문구를 어떻게 고칠지

> 이 문서는 조사 결과만 적습니다. 규칙 문서(`GameSystemRules_Network.md` 등)는 이번에 고치지 않습니다.

---

## 1. 현재 구현 상태 (코드에서 직접 확인)

경로 접두 `Assets/_Project/`. 위치는 파일·메서드 이름으로 가리킵니다.

### 1-1. 전송 계층 설정 (씬 직렬화 값)

| 씬 | `m_HeartbeatTimeoutMS` | `m_ConnectTimeoutMS` | `m_MaxConnectAttempts` | `m_DisconnectTimeoutMS` |
|---|---|---|---|---|
| `Scenes/Game.unity` | 500 | 1000 | 60 | **60000** |
| `Scenes/Lobby.unity` | 500 | 1000 | 60 | **60000** |

- 두 씬의 UnityTransport 값이 같다. 즉 「60초 동안 상대에게서 아무것도 받지 못하면 끊김 이벤트」가 우리 쪽 설정이다.
- 패키지 버전(`Packages/packages-lock.json`): `com.unity.netcode.gameobjects` **2.9.2**, `com.unity.transport` **2.6.0**(NGO 2.9.2의 의존성으로 들어옴). 아래 공식 문서 중 Transport 문서는 2.5 / 1.4 판이므로 **2.6.0에서도 같은지는 미확인**이다.
- `ProjectSettings/ProjectSettings.asset` `runInBackground: 0`.

### 1-2. Host 쪽 — `Scripts/Infrastructure/Network/ReconnectionHandler.cs`

- `OnNetworkSpawn`: **서버(Host)일 때만** 동작한다. 서버가 아니면 `enabled = false`로 꺼진다. 서버면 `NetworkManager.OnClientDisconnectCallback` → `OnClientDisconnected`, `OnClientConnectedCallback` → `OnClientReconnected`를 구독한다.
- `OnClientDisconnected`: 자기 자신(`LocalClientId`)이 끊긴 경우는 무시. 끊긴 상대의 ClientId를 기억하고 `WaitAndForceWin` 코루틴을 시작한다.
- `WaitAndForceWin`: `WaitForSeconds(_reconnectWaitSeconds)` 대기 후 `NetworkGameEndController.ForceWin(내 팀)` 호출.
  - `_reconnectWaitSeconds`의 코드 기본값은 30, **`Game.unity` 씬 직렬화 값도 30**이다. `ReconnectionHandler` 오브젝트는 `Game.unity`에만 배치돼 있다(스크립트 guid 검색).
  - `WaitForSeconds`는 게임 시간(`Time.timeScale`)의 영향을 받는다. 멀티 경기 중에는 `Presentation/UI/InGameSettingsUI.cs` `Show`가 「멀티플레이는 timeScale을 0으로 만들지 않는다」고 분기하므로, 경기 중에는 30초가 그대로 흐른다. 단 **Host 앱 자체가 백그라운드로 내려가 Unity가 멈춰 있으면 이 대기도 함께 멈춘다**(아래 2절 Android 항목과 결합된 추론 — 실측 대상).
- `OnClientReconnected`: 새로 연결된 ClientId가 **기억해 둔 끊긴 ClientId와 같을 때만** 대기를 취소한다.
- 기존 로그(전부 `GameLog.Dev`): 「서버 모드로 재접속 감시 시작」(`WaitSeconds`), 「클라이언트 연결 끊김 — 재접속 대기 시작」(Warn, `ClientId`, `WaitSeconds`), 「클라이언트 재접속 확인 — 강제 승리 대기 취소」, 「강제 승리 대기 코루틴 시작」, 「재접속 타임아웃 — 강제 승리 처리」(`WinnerTeam`). **경기 경과 시간·끊김 사유·역할 같은 측정용 값은 없다.**

### 1-3. 강제 승리 → 결과 화면

- `Scripts/Infrastructure/Network/NetworkGameEndController.cs` `ForceWin`: 서버에서만, 한 번만(`_announced`). 서버 쪽에서 `GameEvents.OnGameEnd`를 직접 발행하고 `AnnounceWinnerClientRpc(승자, false)`를 보낸다. 결과 화면 이탈 감시(`BeginResultScreenLeaveWatchClientRpc`)는 **이 경로에서는 일부러 켜지 않는다**(주석에 이유 기록).
- `AnnounceWinnerClientRpc`: 「AnnounceWinnerClientRpc 수신」 Dev 로그 후, 서버가 아니면 `OnGameEnd` 발행.
- `Scripts/Presentation/UI/GameEndUI.cs` `OnGameEnd`: 승/패 텍스트 → `ShowResultWithInputBlock` → `Time.timeScale = 0`. **결과 화면이 실제로 뜬 시점을 남기는 로그는 확인되지 않았다**(이 메서드 안에서 `GameLog` 호출을 찾지 못함).

### 1-4. Client 쪽 — `Scripts/Infrastructure/Network/NetworkGameManager.cs` `HandleClientDisconnected`

- `OnClientDisconnectCallback`의 통합 처리기. 맵 준비가 끝나지 않았으면 맵 준비 실패로 보낸다. **서버(Host)면 아무것도 하지 않고 돌아간다**(주석: 「상대 클라 끊김은 ReconnectionHandler 담당」).
- Client면 Dev.Warn 「클라이언트 측 서버 연결 끊김 감지 — OnServerDisconnected 발행」(`ClientId`만) 후 `OnServerDisconnected` 이벤트를 발행한다. 주석에 따르면 **스스로 로비로 나갈 때도** 이 줄이 찍힌다(구독을 해제하지 않으므로).
- `OnServerDisconnected` 구독자:
  - `Scripts/Presentation/UI/ViewModels/BattleViewModel.cs` `OnServerDisconnected` → `EnterMapPreparationFailed` — **맵 준비를 기다리는 중(`_awaitingMapPreparation`)일 때만** 반응한다. 주석: 이 ViewModel은 로비 씬과 함께 사라진다.
  - `Scripts/Presentation/UI/NetworkStatusUI.cs` — **씬·프리팹 어디에도 배치돼 있지 않다**(스크립트 guid `adc83ceead3e36246ba137d2ceede0a6` 검색 0건).
- **결론: 경기 도중 Host가 사라지면 Client 쪽에는 승패를 정하거나 화면을 바꾸는 경로가 없다.** Client 화면에 실제로 무엇이 보이는지(멈춤? 그대로?)는 미확인 — 측정 대상이다.

### 1-5. 그 밖에 확인한 것

- **`NetworkManager.OnTransportFailure` 구독: 프로젝트 스크립트 전체에서 0건.**
- **`DisconnectReason`을 읽는 코드: 0건.**
- 경기 중 다시 들어오는(rejoin) 절차 코드: 찾지 못함. 끊긴 쪽이 같은 경기로 돌아오는 화면·흐름이 없다.
- `OnApplicationPause` / `OnApplicationFocus`: `Scripts/Presentation/UI/InputFormController.cs`에만 있다. 게임 씬에는 앱이 내려가고 올라오는 순간을 받는 자리가 없다.
- `Scripts/Infrastructure/Cloud/InternetReachabilityProbe.cs`(Cloud Save 읽기로 「내 인터넷이 실제로 되는가」 확인): 사용처는 `NetworkGameEndController`의 결과 화면 감시뿐이다(`new InternetReachabilityProbe()` 1곳). 인게임에서는 쓰이지 않는다.
- RTT 조회: `NetworkGameManager.GetCurrentRttMs`가 `UnityTransport.GetCurrentRtt(ServerClientId)`를 돌려준다. 호출부는 배치되지 않은 `NetworkStatusUI`뿐이다.
- `NetworkGameManager`는 `DontDestroyOnLoad`로 씬 전환에도 남는다.

---

## 2. 공식 문서가 말하는 것 (메인 세션이 확인한 출처)

| 사실 | 출처 |
|---|---|
| Relay는 **10초** 동안 신호가 없는 클라이언트를 끊는다. 호스트 혼자 남아 있을 때만 60초 | https://docs.unity.com/en-us/mps-sdk/networking/relay-servers , https://docs.unity.com/en-us/mps-sdk/advanced-config/relay-message-protocol |
| NGO를 쓰면 Relay keep-alive는 자동으로 보낸다. Transport `RelayConnectionTimeMS` 기본 3초 | https://docs.unity3d.com/Packages/com.unity.transport@2.5/api/Unity.Networking.Transport.Relay.RelayNetworkParameter.html |
| 호스트가 Relay에서 끊기면 다른 플레이어도 끊기고 Lobby에서 제거된다 | https://docs.unity.com/en-us/mps-sdk/networking/relay-integration |
| Android에서 앱이 실제로 백그라운드로 가면 Unity는 멈춘다(`runInBackground`와 무관) | https://docs.unity3d.com/ScriptReference/Application-runInBackground.html |
| Android에서 최근 앱 목록에서 밀어서 종료하면 `OnApplicationQuit`가 불리지 않는다. `OnApplicationPause(true)`가 마지막으로 믿을 수 있는 신호 | https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnApplicationQuit.html |
| `disconnectTimeoutMS` = 그 시간 동안 아무것도 받지 못하면 끊김 이벤트 | https://docs.unity3d.com/Packages/com.unity.transport@1.4/api/Unity.Networking.Transport.NetworkConfigParameter.disconnectTimeoutMS.html |
| 재접속하면 NGO는 **새 ClientId**를 준다 | https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.8/manual/advanced-topics/session-management.html |

> 문서 판(2.5·1.4·2.8)과 이 프로젝트의 실제 버전(Transport 2.6.0, NGO 2.9.2)이 다르다. 판 사이에 동작이 바뀌었는지는 미확인이다.

---

## 3. 문서와 구현의 차이 · 충돌 가능성

1. **「60초」가 실제로 작동하는가.** 우리 설정은 60초지만, Relay가 10초 만에 한쪽을 끊으면 그쪽 소식이 남은 쪽에 더 빨리(또는 다른 방식으로) 도착할 수 있다. 어느 쪽이 먼저인지 문서로는 모른다.
2. **재접속 취소가 성립하는가.** `OnClientReconnected`는 같은 ClientId일 때만 취소하는데, 공식 문서상 재접속하면 새 ClientId가 나온다. 게다가 경기 중 다시 들어오는 절차 코드 자체가 없다. 즉 「30초 대기 중 재접속하면 취소」는 **현재 구조에서 실제로 일어날 수 없을 가능성이 있다**(실측으로 확인).
3. **Host가 사라지면 Client 쪽 판정이 없다**(1-4절). 이는 `GameSystemRules_Network.md` 최상위 원칙(역할과 무관하게 같은 동작)과 규칙 2 확정 D(양쪽이 각자 판정)가 이미 「미구현」으로 적어 둔 상태와 같다. 이번 측정은 그 미구현 상태에서 실제로 화면에 무엇이 보이는지를 기록한다.
4. 🔴 **규칙 2 확정 E와의 충돌 가능성 — 실측으로 판정.** 확정 E는 「전송 계층이 연결을 붙드는 `t<60` 구간이 재접속 허용 시간」이고 「일시적 끊김은 전송 계층의 재시도 구간에 포함된다」고 전제한다. 그런데 공식 문서대로 Relay가 10초 만에 끊는다면 `t<60` 동안 연결이 붙들려 있지 않을 수 있고, 10초 넘게 끊겼다 돌아온 쪽은 이미 Relay에서 쫓겨나 돌아올 수 없을 수 있다. **이번에는 충돌 「가능성」으로만 적고 규칙 문서는 고치지 않는다.** 문구 수정은 측정 후 R4로 결정한다.
5. 백그라운드: Android에서 앱을 내리면 Unity가 멈추므로, 내린 쪽은 신호를 보내지 못한다(문서). 내린 쪽이 Host라면 30초 대기 코루틴도 멈춘다(1-2절과 결합한 추론). 실제 끊김 시점은 미확인이다.

---

## 4. 이번에 측정으로 답할 질문

| # | 질문 | 왜 문서로 안 되나 |
|---|---|---|
| (1) | Relay가 한쪽을 끊을 때 **남은 쪽은 언제 알게 되는가** — 즉시(약 10초 무렵)인가, 우리 설정 60초인가 | Relay의 10초와 Transport의 60초가 어떻게 겹치는지 문서에 없다 |
| (2) | **10초 넘게 끊겼다가 돌아온 쪽은 어떻게 되는가** — 경기가 이어지는가, 끊김 이벤트를 받는가, 화면은 어떤가 | Relay에서 제거된 뒤 돌아온 경우의 동작이 문서에 없다 |
| (3) | **백그라운드로 내렸을 때 실제 끊김 시점** — 5초·20초·70초 내렸다 올렸을 때 각각 무슨 일이 일어나는가 | Unity가 멈춘 동안 OS·Relay가 연결을 어떻게 다루는지 문서에 없다 |
| (4) | 위 (1)~(3)을 **Host 쪽이 끊긴 경우 / Client 쪽이 끊긴 경우 각각** | 1-2절·1-4절대로 두 역할의 처리가 다르다 |

측정 설계·시나리오는 [Plan](Plan.md)에 있다.

---

## 5. 후속 작업과의 관계 (배경만)

- 후속 task: **전적 기록**. 방식은 **서버 검증 방식**(양쪽이 각자 결과를 보고하고 Cloud Code가 대조) — 사용자 결정. 대상은 **랜덤 매칭만(재경기 포함)**, 싱글·커스텀 매치는 제외.
- 이번 측정 결과(R1 확정 시간, R4 문구)가 전적 task의 「끊김 시 누구에게 승·패를 기록하고 언제 확정하는가」의 입력이 된다.
- 이번 task는 전적 기록을 **구현하지 않는다**.

---

## 6. 사용자 결정 기록 (2026-11-26, 이번 대화)

| 항목 | 결정 |
|---|---|
| 전적 기록 방식 | 서버 검증 방식(2번: 양쪽 보고를 Cloud Code가 대조). 랜덤 매칭만(재경기 포함), 싱글·커스텀 제외. — 후속 task 범위 |
| R1 끊김 확정 시간 | **실측 후 결정** |
| R2 백그라운드 | **별도 처리 없음** — 내려서 끊기면 패배 경로를 그대로 탄다 |
| R3 규칙 1 「로비 버튼/앱 정상 종료」 행 | 「앱 정상 종료」는 60초(끊김) 경로로 합친다 — **후속 task에서 문서 반영**(이번에는 규칙 문서 수정 없음) |
| R4 규칙 2 확정 E 문구 | **실측 후 수정** |

---

## 7. 확인하지 못한 것 (미확인)

- NGO 2.9.2 / Transport 2.6.0에서 `NetworkManager.OnTransportFailure`, `NetworkManager.DisconnectReason`의 존재·동작 — 로컬에 패키지 소스가 없어(`Library/PackageCache` 없음) 확인하지 못함. game-programmer가 코드에서 확인한다.
- 「상대에게서 마지막으로 무언가를 받은 시각」을 동작에 영향 없이 얻을 공개 API가 있는지 — 미확인.
- Client가 Host 끊김을 받을 때 `OnClientDisconnectCallback`에 들어오는 clientId가 자기 ID인지 서버 ID인지 — 미확인(로그로 기록).
- 경기 중 Host가 사라졌을 때 Client 화면의 실제 모습 — 미확인(측정 대상).
- `Game.unity`에서 실제로 쓰이는 NetworkManager가 Lobby 씬에서 넘어온 것인지 — 미확인. 두 씬의 전송 설정 값이 같아 이번 측정에는 영향이 없다.
