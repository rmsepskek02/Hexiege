# 인게임 연결 끊김 실측 — Research

## 무엇을 왜 하나요

다음 큰 작업은 **전적(승·패) 기록**입니다. 전적을 정확히 남기려면 「경기 도중 한쪽 연결이 끊기면 누가 이긴 것으로 할지, 그리고 그것을 **언제** 정할지」가 먼저 정해져야 합니다.

그런데 지금 코드와 Unity 공식 문서를 함께 읽어 보니, 끊김이 실제로 **언제** 양쪽에 알려지는지는 문서만으로 답이 나오지 않았습니다. 예를 들어 공식 문서는 「중계 서버(Relay)가 10초 동안 신호가 없으면 그 사람을 끊는다」고 하는데, 우리 게임 설정에는 「60초 동안 아무것도 안 오면 끊김」이라고 적혀 있습니다. 둘 중 어느 쪽이 먼저 작동하는지, 남은 사람이 그것을 몇 초 뒤에 알게 되는지는 **실제로 재 봐야** 압니다.

그래서 이번 작업은 **게임 동작을 하나도 바꾸지 않고, 끊김과 관련된 순간마다 기록(진단 로그)만 남기게** 한 뒤, 기기 두 대로 여러 상황(앱 강제 종료, 홈 버튼으로 내리기, 와이파이 끄기)을 직접 만들어 시간을 잽니다. 그 측정 결과로 두 가지를 정하고, 전적 기록은 그다음 별도 작업에서 진행합니다.

- **R1** — 끊김을 「확정」하는 시간을 몇 초로 할지
- **R4** — 확정 규칙 문서(`GameSystemRules_Network.md` 규칙 2 확정 E)의 문구를 어떻게 고칠지

> 이 문서는 조사 결과만 적습니다. 규칙 문서(`GameSystemRules_Network.md` 등)는 이번에 고치지 않습니다.
>
> **측정 결과는 [8절](#8-측정-결과-2026-11-26)에 있습니다** *(2026-11-26 추가)*. 한 줄 요약: 어떤 방식으로 끊겨도 남은 쪽은 **약 60초 뒤**에 알았고, 다시 이어진 경우는 없었으며, 역할에 따라 결과가 달라지는 문제가 드러났습니다. 고치는 방향은 아직 정하지 않았습니다.

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
- `Scripts/Presentation/UI/GameEndUI.cs` `OnGameEnd`: 승/패 텍스트 → `ShowResultWithInputBlock` → `Time.timeScale = 0`. **결과 화면이 실제로 뜬 시점을 남기는 로그는 확인되지 않았다**(이 메서드 안에서 `GameLog` 호출을 찾지 못함). *(2026-11-26 — 조사 당시 상태. 이번 task 구현에서 `ResultShown` 진단 줄이 추가됐다 — Plan 9절)*

### 1-4. Client 쪽 — `Scripts/Infrastructure/Network/NetworkGameManager.cs` `HandleClientDisconnected`

- `OnClientDisconnectCallback`의 통합 처리기. 맵 준비가 끝나지 않았으면 맵 준비 실패로 보낸다. **서버(Host)면 아무것도 하지 않고 돌아간다**(주석: 「상대 클라 끊김은 ReconnectionHandler 담당」).
- Client면 Dev.Warn 「클라이언트 측 서버 연결 끊김 감지 — OnServerDisconnected 발행」(`ClientId`만) 후 `OnServerDisconnected` 이벤트를 발행한다. 주석에 따르면 **스스로 로비로 나갈 때도** 이 줄이 찍힌다(구독을 해제하지 않으므로).
- `OnServerDisconnected` 구독자:
  - `Scripts/Presentation/UI/ViewModels/BattleViewModel.cs` `OnServerDisconnected` → `EnterMapPreparationFailed` — **맵 준비를 기다리는 중(`_awaitingMapPreparation`)일 때만** 반응한다. 주석: 이 ViewModel은 로비 씬과 함께 사라진다.
  - `Scripts/Presentation/UI/NetworkStatusUI.cs` — **씬·프리팹 어디에도 배치돼 있지 않다**(스크립트 guid `adc83ceead3e36246ba137d2ceede0a6` 검색 0건).
- **결론: 경기 도중 Host가 사라지면 Client 쪽에는 승패를 정하거나 화면을 바꾸는 경로가 없다.** Client 화면은 측정 결과 **약 60초 멈춘 뒤 「혼자 진행 모드」(`UAS-SOLO-*` 로그)로 바뀌어, 네트워크 없이 그 기기에서만 결과 화면(`ResultShown IsWin=False`)이 떴다**(8-2 S1·S4). *(2026-11-26 측정으로 고침 — 종전 「미확인 — 측정 대상」. 위 「경로가 없다」는 `OnServerDisconnected` 구독자를 읽은 결론이고, 혼자 진행 모드로 넘어가는 경로는 그와 별개로 존재한다. 그 코드 경로는 이 문서에서 조사하지 않았다.)*

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

1. **「60초」가 실제로 작동하는가.** 우리 설정은 60초지만, Relay가 10초 만에 한쪽을 끊으면 그쪽 소식이 남은 쪽에 더 빨리(또는 다른 방식으로) 도착할 수 있다. 어느 쪽이 먼저인지 문서로는 모른다. *(2026-11-26 측정: 모든 판에서 60초 쪽이 작동했다 — 8-4 (1))*
2. **재접속 취소가 성립하는가.** `OnClientReconnected`는 같은 ClientId일 때만 취소하는데, 공식 문서상 재접속하면 새 ClientId가 나온다. 게다가 경기 중 다시 들어오는 절차 코드 자체가 없다. 즉 「30초 대기 중 재접속하면 취소」는 **현재 구조에서 실제로 일어날 수 없을 가능성이 있다**(실측으로 확인). *(2026-11-26 측정: 재접속 0건 — `PeerConnected`·`GraceCancel` 줄 0건, 30초 대기 중 복구된 판 없음. 8-4)*
3. **Host가 사라지면 Client 쪽 판정이 없다**(1-4절). 이는 `GameSystemRules_Network.md` 최상위 원칙(역할과 무관하게 같은 동작)과 규칙 2 확정 D(양쪽이 각자 판정)가 이미 「미구현」으로 적어 둔 상태와 같다. 이번 측정은 그 미구현 상태에서 실제로 화면에 무엇이 보이는지를 기록한다.
4. 🔴 **규칙 2 확정 E와의 충돌 가능성 — 실측으로 판정.** 확정 E는 「전송 계층이 연결을 붙드는 `t<60` 구간이 재접속 허용 시간」이고 「일시적 끊김은 전송 계층의 재시도 구간에 포함된다」고 전제한다. 그런데 공식 문서대로 Relay가 10초 만에 끊는다면 `t<60` 동안 연결이 붙들려 있지 않을 수 있고, 10초 넘게 끊겼다 돌아온 쪽은 이미 Relay에서 쫓겨나 돌아올 수 없을 수 있다. **이번에는 충돌 「가능성」으로만 적고 규칙 문서는 고치지 않는다.** 문구 수정은 측정 후 R4로 결정한다. *(2026-11-26 측정: 「Relay 10초에 끊김」은 백그라운드 39.8초 판에서도 일어나지 않았다. 대신 와이파이는 약 11초·19초 끊김에서 복구되지 않았다 — 8-4. R4 문구는 아직 정하지 않았다.)*
5. 백그라운드: Android에서 앱을 내리면 Unity가 멈추므로, 내린 쪽은 신호를 보내지 못한다(문서). 내린 쪽이 Host라면 30초 대기 코루틴도 멈춘다(1-2절과 결합한 추론). 실제 끊김 시점은 미확인이다. *(2026-11-26 측정: 남은 쪽은 내린 지 59.6초에 감지. 내렸던 Host 자신은 복귀 뒤 60초에야 상대 끊김을 감지했다 — 8-5 4-b)*

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
| R1 끊김 확정 시간 | **실측 후 결정** — 측정은 끝났고(8절) 결정은 아직이다 *(2026-11-26 갱신)* |
| R2 백그라운드 | **별도 처리 없음** — 내려서 끊기면 패배 경로를 그대로 탄다 |
| R3 규칙 1 「로비 버튼/앱 정상 종료」 행 | 「앱 정상 종료」는 60초(끊김) 경로로 합친다 — **후속 task에서 문서 반영**(이번에는 규칙 문서 수정 없음) |
| R4 규칙 2 확정 E 문구 | **실측 후 수정** — 측정은 끝났고(8절) 문구는 아직 정하지 않았다 *(2026-11-26 갱신)* |

---

## 7. 확인하지 못한 것 (미확인)

- NGO 2.9.2 / Transport 2.6.0에서 `NetworkManager.OnTransportFailure`, `NetworkManager.DisconnectReason`의 존재·동작 — 로컬에 패키지 소스가 없어(`Library/PackageCache` 없음) 확인하지 못함. game-programmer가 코드에서 확인한다.
- 「상대에게서 마지막으로 무언가를 받은 시각」을 동작에 영향 없이 얻을 공개 API가 있는지 — 미확인. *(2026-11-26 — 구현 단계에서 RTT 읽기·주기 메시지 방식은 판정에 쓸 수 없다고 보고 해당 로그(Plan ⑥)를 넣지 않았다. 공개 API 유무 자체는 이 문서에 전달되지 않아 미확인 그대로 둔다 — Plan 3-1)*
- Client가 Host 끊김을 받을 때 `OnClientDisconnectCallback`에 들어오는 clientId가 자기 ID인지 서버 ID인지 — 미확인(로그로 기록).
- 경기 중 Host가 사라졌을 때 Client 화면의 실제 모습 — 측정됨: 약 60초 멈춘 뒤 혼자 진행 모드로 바뀜(8-2). *(2026-11-26 고침 — 종전 「미확인(측정 대상)」)*
- `Game.unity`에서 실제로 쓰이는 NetworkManager가 Lobby 씬에서 넘어온 것인지 — 미확인. 두 씬의 전송 설정 값이 같아 이번 측정에는 영향이 없다.

---

## 8. 측정 결과 (2026-11-26)

### 쉬운 요약

- 상대가 사라졌다는 사실은 **어떤 방식으로 끊겨도(앱 강제 종료·앱 내리기·와이파이 끄기) 약 60초 뒤**에야 남은 쪽에 알려졌습니다. 공식 문서가 말하는 「중계 서버 10초」만에 알려진 경우는 한 번도 없었습니다.
- 앱을 **40초 이내**로 내렸다 돌아오면 경기가 그대로 이어졌습니다. **70초 넘게** 내리면 상대가 이미 연결을 끊은 뒤라 돌아와도 이어지지 않았습니다. 와이파이는 약 6~10초 끊김은 다시 이어졌고, 약 11초·19초 끊김은 이어지지 않았습니다.
- 한 번 끊긴 뒤 다시 들어와 이어진 경우(재접속)는 **한 번도 없었습니다.**
- 방장(Host)이 끊겼는지 참가자(Client)가 끊겼는지에 따라 결과가 크게 달랐습니다. **규칙과 반대로 남은 쪽이 지는 경우**, **한쪽 기기에서만 결과가 정해지는 경우**, **로비로 돌아갈 수 없는 경우**가 드러났습니다(8-5).
- 이것을 어떻게 고칠지는 **아직 정하지 않았습니다**(Plan 6절).

### 8-1. 측정 조건과 로그 출처

- **로그 원본:** private 저장소 `rmsepskek02/Hexiege-Logs`, Release `logs-2026-11`의 `2026-11-26_17_36_android-logs_02.zip`(asset id 794979119, 1,246,915 bytes)과 manifest(asset id 794979058).
  - 아래 시각·값은 **메인 세션이 이 원시 로그에서 직접 읽어 전달한 것**이다. 이 문서 작성자는 ZIP을 다시 열어 확인하지 않았다.
- **작업명:** 사용자가 `android-logs`로 올렸다. Plan 5-3에서 안내한 이름 `ingame-disconnect-measure`와 다르다(사실로만 기록).
- **ZIP 안 구성:** 에디터 일별 로그 `Assets/_Project/Docs/_Logs/_editor/2026-11-26/RuntimeLog.txt`(진단 줄 `Diag=Disconnect` 79개), 실기기 세션 RuntimeLog 5개(진단 줄 3·52·19·17·0개), Logcat 1개. **통합 수집 ZIP에 에디터 로그도 함께 들어 있어** 별도 에디터 ZIP은 필요 없었다.
- **구성:** **에디터 1 + 실기기 1대**(사용자 결정 — Plan 5-1의 「기기 2대」에서 바뀜). 끊는 쪽은 **항상 실기기**. 1회차는 실기기 = Host(에디터 = Client), 2회차는 실기기 = Client(에디터 = Host). 커스텀 방.
- **시각:** 한국 시각(KST), 로그의 `UtcMs` 기준. 「t=0」(조작 시각)은 끊는 쪽 실기기의 마지막 `AppPause Paused=True` 줄 시각.
- **에디터 쪽 특성:** 에디터는 창 포커스를 잃어도 로그가 계속 찍혔다(멈추지 않음). 실행 중 `RunInBackground=True`. (1-1의 `runInBackground: 0`은 프로젝트 설정 파일 값이고, 에디터 실행 중 실제 값은 이렇게 달랐다.)

### 8-2. 1회차 — 실기기가 Host(끊는 쪽), 에디터가 Client(남은 쪽)

**S1 앱 강제 종료**
- 실기기 마지막 줄 `AppPause Paused=True` 17:12:41.08 → 에디터 `ServerLostRaised`·`PeerDisconnected`(`Reason=ProtocolTimeout`) 17:13:40.78. **59.7초.**
- 그 뒤 에디터에 `UAS-SOLO-*` 로그가 많이 찍혔다(혼자 진행 모드). 네트워크 없이 `ResultShown IsWin=False` 17:14:52.99(끊김 감지 72초 뒤).
- 사용자 관찰: 강제 종료 순간부터 에디터의 유닛이 제자리 애니메이션만 하고 타격이 없었다(멈춤). 시간이 지나자 다시 진행됐다.

**S2·S3·S4 앱 내리기** — 한 경기 안에서 이어서 했다(실기기 `MatchNo=1`, 17:15:16 시작).
- 내린 시간 `PausedForSec` 5.3 · 5.5 · 18.3초 → **모두 그대로 이어졌다.** Host가 멈춘 동안 양쪽이 함께 멈췄다(사용자 관찰과 일치).
- 네 번째, **73.2초**(17:17:14.21 내림 → 17:18:27.40 복귀):
  - 에디터(Client)가 17:18:13.86(내린 지 59.6초)에 `ProtocolTimeout` → 혼자 진행 모드.
  - 실기기는 복귀 때 `IsConnectedClient=True`·`ConnectedCount=2` — 끊긴 줄 몰랐다.
  - 실기기(Host)의 `PeerDisconnected`는 17:19:27.55(`SinceResumeSec=60.2` — **복귀 뒤 60초**).
  - 그 사이 양쪽이 각자 진행했다. 에디터 `ResultShown IsWin=False` 17:18:56.34. 실기기는 17:19:26.01 `OnBuildingDied` → `AnnounceWinner WinnerTeam=Red`(Host 성이 파괴되어 Host 패배). `GraceStart`는 그 뒤인 17:19:27.56.
  - 사용자 관찰: 두 화면이 서로 달랐고 맞지 않았다. 결과 화면 — 실기기에는 안내 문구가 있었고, 에디터에는 없었다(싱글 경기처럼).

**S5 와이파이 끄기 20초** (실기기 `MatchNo=2`, 17:20:04 시작)
- 와이파이를 끈 시각: **약 17:20:26 — 추정**(빠른 설정을 연 `AppFocus` 줄과, 끊김 감지 시각에서 60초를 거꾸로 뺀 값으로 맞춤).
- 실기기(Host)는 끊김을 감지하기 전인 17:21:22.47에 사용자가 포기했다(포기 처리 `ForfeiterClientId=0`).
- 에디터: 17:21:26.69 「포기 요청 전송」 → 17:21:26.84 `ProtocolTimeout` → 17:21:30.57 「포기 요청 전송」. 처리·결과 줄은 없다(받아 줄 서버가 없음) → **로비로 돌아갈 수 없어 재실행.**
- 와이파이를 다시 켠 뒤에도 복구되지 않았다.

**S6 와이파이 끄기 70초** (실기기 `MatchNo=3`, 17:22:44 시작)
- 에디터 `ProtocolTimeout` 17:24:41.63, 실기기(Host) `PeerDisconnected` 17:24:42.02 → `GraceStart`.
- 그런데 17:24:59.96 Host 성 파괴(`OnBuildingDied` → `WinnerTeam=Red`) — **30초 대기 끝(ForceWin) 전에 Host 패배.**
- 에디터는 17:25:05.88 「포기 요청 전송」만 있고 결과가 없다.

### 8-3. 2회차 — 에디터가 Host(남은 쪽), 실기기가 Client(끊는 쪽)

**S1 앱 강제 종료** (실기기 `MatchNo=4`, 17:26:26 시작)
- 실기기 마지막 `AppPause` 17:26:53.27 → 에디터 `PeerDisconnected` 17:27:52.89(**59.6초**) → `GraceStart` → `GraceExpired`(`GraceElapsedSec=30.0`) 17:28:22.88 → `ForceWin` → `ResultShown IsWin=True` 17:28:22.90.
- 조작부터 결과 화면까지 **총 89.6초**(60초 + 30초). Plan이 예상한 경로 그대로다.

**S2·S3·S4 앱 내리기** (실기기 세션 `082723`, 17:29:04 시작)
- `PausedForSec` 10.8 · 39.8 · 5.0초 → **모두 이어졌다**(사용자: 복귀 때 순간적으로 맞춰짐).
- **70.1초**(17:31:24.34 → 17:32:34.43): 에디터 `PeerDisconnected` 17:32:23.96(59.6초) → `GraceStart` → `ForceWin`·`ResultShown IsWin=True` 17:32:53.96.
- 실기기는 복귀 때 `IsConnectedClient=True`, `ServerLostRaised` 0건 — **멈춘 채 로비로 돌아갈 수 없었다**(사용자 관찰과 일치).

**S5 와이파이 끄기** (실기기 세션 `083323`, 17:33:48 시작)
- 빠른 설정을 열고 닫은 시각(`AppFocus`): 17:34:06–08, 13–20, 26–27, 31–32, 50–52, 35:01–04.
- 앞쪽 끄기들은 복구됐다(**약 6~10초**). 마지막 끄기(**약 17:34:51 끔 → 17:35:02~04 켬, 약 11초**)는 복구되지 않았다. 끈·켠 시각은 `AppFocus` 줄로 맞춘 **추정**이다.
- 양쪽 `ProtocolTimeout` 17:35:51.50(Host)·17:35:51.92(Client). 에디터 Host는 17:35:55.05 사용자 포기로 끝났다(`IsWin=False`).
- 사용자: 세 번째 재연결 도중 끊겼다. **S6은 이 판으로 갈음**했다(2회차 S6 별도 판 없음).

### 8-4. 측정 질문(4절)의 답

| # | 답 |
|---|---|
| (1) 남은 쪽이 끊김을 아는 시각 | **모든 판 59.6~59.7초**, `Reason=ProtocolTimeout`. 더 일찍 알려진 경우 0건. 예외: 남은 쪽(Host) 자신이 백그라운드에 있었으면 **복귀 뒤 60초**(8-2 S4) |
| (2) 10초 넘게 끊겼다 돌아온 쪽 | 앱 내리기 5.0~39.8초는 모두 이어짐. 70초 이상은 상대가 이미 끊은 뒤라 복구 없음. 와이파이 약 6~10초는 복구, 약 11초·19초는 복구 안 됨 — 원인(기기 주소가 바뀌어서인지, 중계 서버 시간 제한 때문인지)은 **로그로 판정할 수 없다** |
| (3) 백그라운드 시 실제 끊김 시점 | 남은 쪽 감지는 내린 지 59.6초 |
| (4) 역할별 비교 | Host가 남으면 60초 + 30초 뒤 정상 승리(8-3). Client가 남거나, Host가 내렸다 돌아오면 문제가 생김 — 8-5 |

그 밖에 확인된 것:
- 공식 문서의 「Relay가 10초 동안 신호가 없으면 끊는다」는 **백그라운드 39.8초 판에서 일어나지 않았다**(문서와 실측의 차이 — 3절 1·4).
- **재접속 0건:** `PeerConnected`·`GraceCancel` 줄 0건. 30초 대기 중 복구된 판 없음(3절 2).
- Client 쪽 `ServerLostRaised` 줄이 끊김 1회에 2~3번 찍혔다. 원인은 조사하지 않았다.

### 8-5. 드러난 문제

1. **나간 쪽이 아니라 남은 Host가 진다.** 상대가 나간 뒤에도 상대 유닛이 Host 화면에서 계속 싸워, 남은 Host가 성을 잃고 패배했다(1회차 S4 17:19:26, S6 17:24:59). `GameSystemRules_Network.md` 「네트워크 동작 규칙」 규칙 1(나간 쪽 패배)과 반대다.
2. **Host가 사라지면 Client는 60초 멈춘 뒤 혼자 진행 모드(`UAS-SOLO-*`)로 바뀌고, 결과가 그 기기에서만 정해진다.** 결과 화면에 안내 문구도 없다(1회차 S1·S4). 같은 문서 「🔴 최상위 원칙」(역할과 무관하게 같은 동작) 위반.
3. **Host와 끊긴 Client는 포기가 동작하지 않아 로비로 돌아갈 수 없다**(1회차 S5·S6 — 「포기 요청 전송」만 남고 처리되지 않음).
4. **70초 넘게 내렸다 돌아오면 그 기기는 아직 연결된 줄 알고 멈춘다**(2회차 S4 — `IsConnectedClient=True`, `ServerLostRaised` 0건).
   - 4-b. **Host가 내려가 있던 시간만큼 Host의 감지가 늦어진다**(1회차 S4 — `SinceResumeSec=60.2`, 내린 지 133초 뒤에야 감지). 그 사이에 문제 1이 일어났다.

> 고치는 방향은 이 문서에서 정하지 않는다 — 교정 방향 논의 중, 미확정(Plan 6절).
