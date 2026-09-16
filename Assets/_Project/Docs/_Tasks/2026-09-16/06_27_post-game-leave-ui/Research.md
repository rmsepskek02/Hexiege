# Research — 경기 종료 후 상대 이탈 UI·프로세스

작성일: 2026-09-16 / 작업 폴더: `_Tasks/2026-09-16/06_27_post-game-leave-ui/`

---

## 1. 이 작업이 무엇이고 왜 하는가 (자연어 설명)

경기가 끝나면 승패를 알리는 **결과 화면**이 뜨고, 거기서 「다시하기(재경기)」를 누르거나 「로비로」를 눌러 나갈 수 있습니다.
지금 이 화면에는 **사람을 가둬 두는 문제**와 **아무것도 알려 주지 않는 문제**가 함께 있습니다.

**첫째, 재경기를 요청하면 스스로 화면을 빠져나갈 수 없습니다.**
「다시하기」를 누르는 순간 **다시하기 버튼과 로비 복귀 버튼이 둘 다 꺼집니다.**
상대가 수락하지도 거절하지도 않으면 두 버튼 모두 꺼진 채로 남고, 사용자가 할 수 있는 일이 하나도 없습니다.
그 사이에도 자동 로비 복귀 카운트다운은 계속 돌아서, 결국 **자기가 보낸 요청의 답을 듣지 못한 채 화면 밖으로 밀려납니다.**

**둘째, 상대가 나가도 그 사실이 화면에 전달되지 않습니다.**
상대가 로비로 돌아가든, 앱을 강제로 종료하든, 회선이 끊기든 **내 화면은 아무 변화가 없습니다.**
그래서 사용자는 응답을 기다리다가 시간이 다 되어 로비로 끌려나가고, **왜 그렇게 됐는지 끝까지 알 수 없습니다.**

이번 작업은 이 두 가지를 고칩니다. 구체적으로는 ① 로비 복귀 버튼은 **어떤 상황에서도 끄지 않고**,
② 상대가 사라졌다는 사실을 **화면 안에서 말로 알려 주며**, ③ 그 뒤에는 **스스로 나가든 기다려서 나가든 사용자가 고를 수 있게** 합니다.

🔴 **사양은 이번 회차(2026-09-16)에 이미 규칙 문서에 기록됐습니다.**
이 문서는 새 사양을 만드는 자리가 아니라, **그 규칙을 구현하려면 지금 코드가 어떤 상태인지**를 확인해 적는 자리입니다.
규칙의 단일 소스는 `GameSystemRules/GameSystemRules_UI.md` 「공통 UI 규칙」의 규칙 D-1~D-6 · 8 · 9 · M-3 · M-4,
`GameSystemRules/GameSystemRules_RandomMap.md` 규칙 17 · 18 · 19,
그리고 `TechnicalDesignDocument.md` 「결과 화면 이탈 판정·통보 구조」 절입니다. **여기에 사양을 옮겨 적지 않습니다.**

---

## 2. 현재 상태 (갈래별)

### 2-1. 결과 화면 — `Assets/_Project/Scripts/Presentation/UI/GameEndUI.cs`

| 항목 | 현재 상태 |
|---|---|
| 카운트다운 시작 | 결과 화면이 뜨는 `OnGameEnd` 에서 `CountdownCoroutine` 을 시작한다 |
| 카운트다운 길이 | 필드 `_autoReturnSeconds` 의 **코드 기본값 `30f`**, `Game.unity` 의 직렬화 값도 **`30`** |
| 카운트다운 문구 | `CountdownCoroutine` 안에서 `{올림한 남은 초}초 후 로비로 돌아갑니다.` 한 줄만 쓴다 |
| 타이머 정지 | `StopCountdown()` 호출부는 `OnDestroy` · `OnRestartClicked` · `Hide` · `ReturnToLobby` |
| 재경기 요청 시 | `SetupRematchButton(bool)` 이 다시하기 버튼 리스너를 통째로 교체하고, 그 리스너가 버튼 텍스트를 `요청 중...` 으로 바꾼 뒤 **`_restartButton.interactable = false` 와 `_backToLobbyButton.interactable = false` 를 둘 다 실행**한다. 🔴 **그 리스너에는 `StopCountdown()` 이 없다** |
| 버튼 복원 | `RestoreRematchButton()` 이 두 버튼을 다시 켠다 |

- 🔴 **「갇힘」의 직접 원인은 위 표의 `_backToLobbyButton.interactable = false` 한 줄이다.** 규칙 D-3 과 정면으로 반대다.
- 🔴 **카운트다운 문구는 규칙 D-1 이 정한 `상대방이 떠났습니다. n초 뒤 로비로 이동합니다.` 와 문자열 자체가 다르다.**
  따라서 이번 작업은 기존 문구의 「수정」이 아니라 **평시 문구와 이탈 문구를 가르는 분기 + 새 문자열 추가**다.
- `CountdownCoroutine` 은 `WaitForSecondsRealtime` 기반이라 `Time.timeScale = 0` 인 결과 화면에서도 계속 돈다.

**`GameEndUI` 가 구독하는 `GameEvents` 목록 (직접 실측):**

- `OnGameEnd`
- `OnNetworkRematchAvailable` → `SetupRematchButton(...)`
- `OnNetworkRematchDeclined` → `RestoreRematchButton()`
- `OnNetworkRematchMapFailed` → `RestoreRematchButton()`
- `OnNetworkRematchStarting` → 로딩 표시
- `OnNetworkBackToLobby` → 씬 전환

🔴 **재경기 관련 채널은 위 4개이며, 「상대가 나갔다」를 전달하는 채널은 하나도 없다.**
규칙 D-1 · D-2 · D-4 의 이탈 재시작을 화면에 반영하려면 **채널 자체를 새로 만들어야 한다.**

### 2-2. 공용 팝업 — `Presentation/UI/UIManager.cs` · `Presentation/UI/ConfirmPopup.cs`

| 항목 | 현재 상태 |
|---|---|
| 공용 팝업 API | `ShowConfirm(message, onConfirm, onCancel, confirmLabel, cancelLabel)` **하나뿐**이며 `ShowAlert` 는 **없다** |
| 인터페이스 | `Presentation/UI/Core/IUIManager.cs` 가 `ShowConfirm` 을 선언한다 |
| `ConfirmPopup` 의 직렬화 필드 | `_panel` · `_messageText` · `_confirmButton` · `_cancelButton` · `_confirmButtonText` · `_cancelButtonText` |
| 타이틀 | **필드가 없다** |
| 버튼 수 | **2개 고정** |
| 배경 오버레이 | `UIManager` 가 단일 소유하며 `ShowBlockingOverlay(Action onTap = null)` · `HideBlockingOverlay()` 로 참조 계수 방식으로 켜고 끈다 |

🔴 **규칙 D-5 가 요구하는 「타이틀 + 본문 + 버튼 1개」는 지금의 `ConfirmPopup` 구조로는 만들 수 없다.**
타이틀을 담을 자리가 없고, 버튼 하나를 숨기는 것만으로는 규칙이 요구하는 구성이 되지 않는다.

**`ShowConfirm` 실호출처 (주석 줄을 걸러낸 실측, 선언부 제외):** **5건**

- `Presentation/UI/Views/Lobby/Profile/ProfileView.cs`
- `Presentation/UI/Views/Login/LoginRootView.cs`
- `Presentation/UI/Views/Login/EmailVerifyView.cs`
- `Presentation/UI/InGameSettingsUI.cs`
- `Debug/UIManagerTestButtonHandler.cs`

이 5건은 **이번 작업의 대상이 아니다.** 알림 팝업을 만들면서 이 호출부의 동작이 달라지면 안 된다.

### 2-3. 네트워크 — `Infrastructure/Network/`

**`NetworkGameManager.BackToLobby(string lobbySceneName = "Lobby")`** — 주석에 번호가 붙은 실제 순서:

1. `OnClientConnectedCallback` 구독 해제
1-A. 맵 전송 구독 정리 · `_mapTransferInProgress = false` · `ClearMapTransferRoundActions()` · `MapHandoff.Clear()`
2. `StopHeartbeat()`
3. `_lobbyManager?.LeaveLobbyAsync()` (fire-and-forget)
4. `ShutdownNetworkManager()`
5. `GameEvents.OnNetworkBackToLobby.OnNext(lobbySceneName)`

🔴 **이 순서 어디에도 「상대에게 나간다고 알리는」 단계가 없다.** 규칙 17 의 정상 퇴장 통보가 들어갈 자리는 **3(Lobby 퇴장)과 4(Shutdown) 사이**다.
⚠️ **RPC 를 보내고 같은 프레임에 `ShutdownNetworkManager()` 를 부르면 그 메시지가 실제로 나가기 전에 버려질 수 있다 — 실기 확인이 필요한 항목이다.**

**`NetworkGameEndController`** — 재경기 요청·수락·거절과 결과 발표를 담당한다.

- `RequestRematchServerRpc` 가 `_rematchRequesterId` 로 첫 요청만 접수하고, 두 번째 요청을 받으면 재경기를 시작한다.
- `ForceWin(int)` 은 **맨 앞에서 `if (_announced) return;`** 으로 막힌다. 결과 화면이 떠 있다는 것은 이미 승자가 발표됐다는 뜻이므로 그 시점의 `_announced` 는 `true` 다.

**`ReconnectionHandler`**

- `_reconnectWaitSeconds` 의 코드 기본값은 **`30f`**.
- `OnClientDisconnected(ulong)` 의 가드는 **`clientId == NetworkManager.LocalClientId` 와 `_forceWinTriggered` 둘뿐이며, 경기 종료 여부를 보지 않는다.**
- 🔴 **그래서 결과 화면 위에서 상대가 끊겨도 재접속 대기 코루틴이 그대로 시작된다.** 다만 30초 뒤 부르는 `ForceWin()` 이 위 `_announced` 가드에 막히므로 **아무 일도 하지 않는 죽은 대기**다.
- ⚠️ **이탈 판정을 새로 넣으면 같은 30초짜리 시계가 두 개 동시에 돈다.** 동작은 갈리지 않지만 **로그를 읽을 때 두 시계를 같은 것으로 착각하기 쉽다.**

**`NetworkMapTransfer`**

- `NetworkManager.OnClientDisconnectCallback` 을 구독해 `HandleClientDisconnected(ulong)` 에서
  `FailHostRound(MapTransferErrorCode.Disconnected, LogEvent.MapTransferFailed, ...)` 로 **그 회차를 즉시 실패시킨다.**
- `MapTransferErrorCode.Disconnected` 는 값 `10` 으로 이미 정의돼 있다.
- 🔴 **즉 규칙 18 이 요구하는 처리 중 「감지와 실패 처리」는 이미 있다.** 남은 것은 **그 사유를 화면 문구로 가르는 것뿐**이다.

### 2-4. 전송 계층 (씬 직렬화 값)

| 값 | `Game.unity` | `Lobby.unity` |
|---|---|---|
| `m_DisconnectTimeoutMS` | **30000** | **30000** |
| `m_HeartbeatTimeoutMS` | 500 | 500 |

🔴 **규칙 17 과 `TechnicalDesignDocument.md` 가 정한 값은 60000 이며, 두 씬 모두 아직 30000 이다.**
🔴 **한 씬만 고치면 로비와 인게임의 타임아웃이 갈린다.**

---

## 3. 영향 범위

### 3-1. 이번 작업에서 건드릴 것

| 파일 / 자산 | 왜 |
|---|---|
| `Presentation/UI/GameEndUI.cs` | 로비 버튼 잠금 제거 · 카운트다운 길이와 문구 분기 · 이탈 채널 구독 |
| `Presentation/UI/UIManager.cs` | 알림 팝업 표시 API 신설 |
| `Presentation/UI/Core/IUIManager.cs` | 위 API 선언 추가 |
| 알림 팝업 컴포넌트 (신설 또는 `ConfirmPopup` 확장) | 규칙 D-5 의 「타이틀 + 본문 + 버튼 1개」 구조 |
| `Infrastructure/Network/NetworkGameManager.cs` | 정상 퇴장 통보를 보내는 자리 |
| `Infrastructure/Network/NetworkGameEndController.cs` | 이탈 통보 RPC 와 무반응 자체 감시, 재경기 요청 팝업 정리 |
| `Application/.../GameEvents` | 「상대가 나갔다」를 Presentation 으로 전달할 채널 |
| `Game.unity` · `Lobby.unity` | `m_DisconnectTimeoutMS` 30000 → 60000 (두 씬 모두) |
| `Game.unity` | `_autoReturnSeconds` 30 → 60 (Inspector 값이 코드 기본값보다 우선하므로 씬도 고쳐야 한다) |

### 3-2. 건드리지 않을 것

| 대상 | 왜 |
|---|---|
| `ShowConfirm` 실호출처 5건 | 이번 작업의 대상이 아니다. 알림 팝업을 만들면서 이들의 동작이 달라지면 안 된다 |
| `ConfirmPopup` 의 기존 2버튼 동작 | 위와 같은 이유 |
| `NetworkMapTransfer` 의 실패 판정 로직 | 규칙 18 이 「기존 실패 경로를 그대로 쓴다」고 정했다. 새 이벤트도 새 로그 키도 만들지 않는다 |
| `ReconnectionHandler` 의 인게임 재접속 로직 | 별건이며 `ROADMAP.md` 「재접속 실제 구현」 행이 따로 다룬다 |
| 규칙 번호 자체 | 코드 주석과 과거 Task 문서가 참조하고 있어 재배열하면 연결이 끊긴다 |

---

## 4. 🔴 시스템 한계 — 왜 「자체 판정 30초」가 필요한가

상대가 사라지는 방식은 **통보를 보낼 수 있는 경우**와 **보낼 수 없는 경우**로 갈린다.

| 갈래 | 통보를 보낼 수 있는가 | 내 쪽이 알아채는 속도 |
|---|---|---|
| 상대가 로비 복귀 버튼을 눌러 스스로 나간다 | **보낼 수 있다.** 나가는 쪽이 아직 살아 있고 연결도 살아 있다 | **즉시** |
| 앱 강제 종료 · 프로세스 종료 | **보낼 수 없다.** 코드가 실행될 기회 없이 사라진다 | 전송 계층이 「응답이 없다」고 판단할 때까지 |
| 회선 장애 · 기기 절전 · 터널 단절 | **보낼 수 없다.** 코드는 살아 있을 수 있으나 패킷이 나가지 못한다 | 위와 같다 |

- 🔴 **즉 「나간다」는 말을 들을 수 있는 것은 정상 퇴장 하나뿐이고, 나머지는 「말이 없다」는 사실로만 짐작해야 한다.**
- 말이 없다는 사실을 **전송 계층에 맡기면 판정 시점을 이 게임이 제어할 수 없다.** 전송 계층은 자기 기준으로 연결을 끊고,
  그 뒤에는 **통보를 보낼 연결 자체가 없어져** 화면에 사유를 띄울 방법이 사라진다.
- 🔴 **그래서 규칙 17 이 「30초 무반응」이라는 자체 감시를 따로 두고, 전송 계층 타임아웃을 그보다 길게(60초) 두라고 정한 것이다.**
  순서가 뒤집히면 **이유 없이 끊긴 화면**만 남는다.
- ⚠️ **이 한계는 구현으로 없앨 수 없다.** 강제 종료한 쪽이 「내가 나갔다」를 알릴 방법은 없으므로,
  무반응 이탈은 **최대 30초의 지연을 안고 판정되는 것이 사양 그대로의 동작**이다.

---

## 5. 🔴 직접 열어 확인한 것과 인계받은 것 (CLAUDE.md 규칙 10)

### 5-1. 이 문서를 쓰면서 직접 열어 확인한 것

- `GameEndUI.cs` — `_autoReturnSeconds = 30f` · 카운트다운 문구 `{n}초 후 로비로 돌아갑니다.` ·
  `SetupRematchButton` 의 두 버튼 잠금과 그 리스너에 `StopCountdown()` 이 없다는 것 · `StopCountdown()` 호출부 4곳 ·
  구독 목록 6개(재경기 관련 4개 포함) · `RestoreRematchButton()` 이 두 버튼을 함께 켠다는 것
- `UIManager.cs` — `ShowConfirm` 시그니처 · `ShowAlert` 부재 · `ShowBlockingOverlay` / `HideBlockingOverlay`
- `ConfirmPopup.cs` — 직렬화 필드 6개 · 타이틀 필드 부재 · 버튼 2개
- `IUIManager.cs` — `ShowConfirm` 선언 위치
- `ShowConfirm` 실호출처 5건 (주석 줄을 걸러낸 뒤의 수)
- `NetworkGameManager.cs` — `BackToLobby` 의 단계 구성과 통보 단계 부재
- `NetworkGameEndController.cs` — `ForceWin` 의 `if (_announced) return;` · `RequestRematchServerRpc` 의 `_rematchRequesterId` 분기
- `ReconnectionHandler.cs` — `_reconnectWaitSeconds = 30f` · `OnClientDisconnected` 의 가드 2개와 경기 종료 가드 부재
- `NetworkMapTransfer.cs` — `HandleClientDisconnected` → `FailHostRound(MapTransferErrorCode.Disconnected, ...)` · `Disconnected = 10`
- `Game.unity` · `Lobby.unity` — `m_DisconnectTimeoutMS: 30000` (둘 다) · `m_HeartbeatTimeoutMS: 500` · `Game.unity` 의 `_autoReturnSeconds: 30`
- 규칙 원문 — `GameSystemRules_UI.md` 규칙 8 · 9 · M-1~M-4 · D-1~D-6, `GameSystemRules_RandomMap.md` 규칙 16 · 17 · 18 · 19
- `TechnicalDesignDocument.md` 「결과 화면 이탈 판정·통보 구조」 절 전문
- `_Tasks/2026-09-14/12_17_rematch-map-selection/Plan.md` §14-4 · §14-6

### 5-2. 인계받았고 이 문서에서 재확인하지 않은 것

- 🔴 **실기 로그에서 나온 수치 일체** — 재경기 맵 준비 `ElapsedMs=0~3`, 전송 99~134ms, 멀티 7판 연속 PASS 등.
  런타임을 돌릴 수 없으므로 **당시 세션이 측정한 값을 그대로 인용한다.**
- 🔴 **`ReconnectionHandler` 의 죽은 대기가 「무해하다」는 결론** — 코드상 `_announced` 가드에 막힌다는 것은 직접 확인했으나,
  **실기에서 그 30초가 실제로 아무 일도 하지 않는 것을 관측한 적은 없다.**
- 씬 재로드 구간의 길이 「0.2초 남짓」 — 규칙 19 의 기술이며 이 문서에서 재측정하지 않았다.
- 커밋 해시(`0608f85` · `8c83317` · `20ba0ab` 등) — git 명령이 금지되어 있어 대조하지 않았다.

### 5-3. ⚠️ 인계 내용과 실측이 어긋난 자리 (고치지 않고 적어 둔다)

| 자리 | 인계 내용 | 실측 |
|---|---|---|
| `BackToLobby()` 의 단계 | 「5단계 — 콜백 해제 → 맵 상태 정리 → Lobby 퇴장 → Shutdown → 씬 전환」 | 그 사이에 **`StopHeartbeat()`(2번)** 이 하나 더 있다. **통보를 넣을 자리(Lobby 퇴장과 Shutdown 사이)는 그대로 성립**하므로 결론은 바뀌지 않는다 |
| `RestoreRematchButton()` 호출 경로 | `ROADMAP.md` 의 2026-09-14 (3차) 행은 *"`OnNetworkRematchDeclined` 구독 한 곳에서만 호출된다"* 고 적었다 | 현재는 **`OnNetworkRematchDeclined` 와 `OnNetworkRematchMapFailed` 두 구독**에서 호출된다. 2026-09-15 의 C 단계로 `OnNetworkRematchMapFailed` 구독이 추가된 뒤 그 행이 갱신되지 않은 것으로 보인다. 🔴 **이 문서에서는 고치지 않는다** — 이번 회차는 새 파일 2개 외에 아무것도 건드리지 않는다 |

---

## 6. 함께 걸려 있는 미결 항목

| 항목 | 상태 |
|---|---|
| **14-4 — 실패 경로 실기 검증** | `_Tasks/2026-09-14/12_17_rematch-map-selection/Plan.md` §14-4. 강제 실패를 일으킬 **임시 조작 코드가 필요하고 사용자 승인 사항**이다. 🔴 **3단계에 이어 두 회차 연속 미종결**이며, 이번에 하지 않으면 규칙 D-1~D-6 과 M-3 · M-4 의 **실패 화면을 한 번도 보지 못한 채 끝난다** |
| **14-6 — 규칙 12 와 규칙 14 의 문언 충돌** | ✅ **이번 회차에 이미 닫혔다.** 규칙 12 · 16 에 「최초 경기」 한정이 명문화됐다. 기록만 남긴다 |
| **규칙 M-3 · M-4 의 팝업 문구와 버튼 라벨** | ⚠️ **여전히 미정.** 이번에 정해진 것은 「팝업이 뜨는가」와 「어느 타입인가」뿐이다 |

---

## 7. 이 작업이 참조하는 문서

| 문서 | 무엇의 단일 소스인가 |
|---|---|
| `GameSystemRules/GameSystemRules_UI.md` 「공통 UI 규칙」 | 화면 문구 · 버튼 · 타이머 · 팝업 타입 (규칙 8 · 9 · M-1~M-4 · D-1~D-6) |
| `GameSystemRules/GameSystemRules_RandomMap.md` | 이탈 판정과 통보 (규칙 17) · 전송 중 이탈 (규칙 18) · 범위 밖 판단 (규칙 19) · 맵 전송과 실패 복구 (규칙 16) |
| `TechnicalDesignDocument.md` 「결과 화면 이탈 판정·통보 구조」 | 계층 배치 · 메시지 흐름 · 전송 계층 타임아웃 값 60000 |
| `.claude/MEMORY.md` | 아키텍처 제약 (NetworkBehaviour 위치 · Application 의 Netcode 직접 참조 금지 · NGO RPC 명명 · UIManager null-safe · Inspector 우선) |
| `Assets/_Project/Docs/WORKFLOW.md` | 작업 사이클과 기존 로직 제거 규칙 |
