# Research — LoadingIndicator 전수 적용 조사

## 1. 이 작업이 무엇이고 왜 하는가 (자연어 설명)

게임을 하다 보면 화면이 전환되거나(예: 로비 → 게임), 서버와 통신하느라(예: 로그인, 매칭) 잠깐 멈추는 순간이 있다. 이때 아무 표시도 없으면 사용자는 "앱이 멈췄나?" 하고 오해하거나, 이미 누른 버튼을 또 누르는 등의 문제가 생긴다. 이를 막기 위해 화면 전체를 덮는 **로딩 인디케이터**(빙글빙글 도는 표시 + "게임 로딩 중..." 같은 안내 문구)를 띄운다.

현재 Hexiege에는 이 로딩 인디케이터를 띄우는 단일 통로(`UIManager.ShowLoading`)가 이미 만들어져 있고, **일부 상황(로그인, 매칭, 싱글플레이 시작 등)에서는 잘 동작**한다. 그러나 **로그아웃, 게임 포기, 로비 복귀, 재경기, 연결 끊김 복귀** 같은 다른 씬 전환 상황에서는 로딩 인디케이터가 빠져 있어, 사용자가 버튼을 누른 뒤 씬이 바뀌기 전까지 검은/멈춘 화면을 보게 된다.

이 작업은 (1) 로딩 인디케이터가 빠진 모든 지점을 코드 근거와 함께 찾아내고, (2) 어디를 어떻게 고칠지 계획을 세우며, (3) 앞으로 새 기능을 만들 때 같은 실수를 반복하지 않도록 `GameSystemRules_UI.md`에 "씬 전환/비동기 작업에는 반드시 로딩 인디케이터를 띄운다"는 규칙을 추가하는 것이 목적이다.

이 문서(Research.md)는 조사 결과만 담는다. 실제 수정 방법은 Plan.md에서 다룬다. **이 단계에서 코드는 수정하지 않는다.**

---

## 2. 현재 `ShowLoading` 호출 위치 목록 (이미 적용된 곳)

`ShowLoading`은 두 경로로 호출된다.
- `UIManager.Instance?.ShowLoading(...)` — 전역 직접 호출 (Lobby/Game 씬)
- `_bootstrapper.ShowLoading(...)` — `LoginBootstrapper`가 `UIManager.Instance?.ShowLoading`으로 위임 (Login 씬)

| # | 파일 : 라인 | 상황 | show/hide |
|---|-------------|------|-----------|
| 1 | `Bootstrap/LoginBootstrapper.cs:272-274` | ShowLoading 위임 래퍼 (UIManager로 전달) | 래퍼 |
| 2 | `Presentation/UI/UIManager.cs:169` | ShowLoading 본체 구현 | 본체 |
| 3 | `Presentation/UI/Views/Login/EmailLoginView.cs:117 / 141` | 이메일 로그인 비동기 작업 | show / hide |
| 4 | `Presentation/UI/Views/Login/EmailVerifyView.cs:106 / 126` | 이메일 인증 비동기 작업 | show / hide |
| 5 | `Presentation/UI/Views/Login/EmailVerifyView.cs:142 / 155` | 인증 재전송 등 비동기 작업 | show / hide |
| 6 | `Presentation/UI/Views/Login/LoginSelectView.cs:102 / 118` | 로그인 방식 선택 비동기 작업 | show / hide |
| 7 | `Presentation/UI/Views/Login/AnonymousWarningPopup.cs:159 / 189` | 익명 로그인 진행 비동기 작업 | show / hide |
| 8 | `Presentation/UI/Views/Login/SignUpView.cs:133 / 159` | 회원가입 비동기 작업 | show / hide |
| 9 | `Presentation/UI/ViewModels/BattleViewModel.cs:153` | 호스팅 시작 실패 시 | hide (예외) |
| 10 | `Presentation/UI/ViewModels/BattleViewModel.cs:161` | 게임 참가 실패 시 | hide (예외) |
| 11 | `Presentation/UI/ViewModels/BattleViewModel.cs:181 / 188 / 194` | 랜덤 매칭 — 매칭 성사/취소/실패 | show / hide / hide |
| 12 | `Presentation/UI/ViewModels/BattleViewModel.cs:223` (`LoadSingleplayScene`) | 싱글플레이 Game 씬 로드 직전 | show |
| 13 | `Presentation/UI/ViewModels/BattleViewModel.cs:246` (`JoinGame`) | 코드로 게임 참가 | show |
| 14 | `Presentation/UI/ViewModels/BattleViewModel.cs:300` (`OnClientConnected`) | 2명 접속 완료 → Game 씬 로드 직전 | show |

요약: **로그인 흐름 전체 + 전투(Battle) 진입 흐름**에는 로딩 인디케이터가 잘 적용되어 있다.

---

## 3. LoadingIndicator가 빠진 상황 목록 (수정 필요)

씬 전환(`SceneManager.LoadScene`, NGO `SceneManager.LoadScene`) 또는 네트워크 종료를 동반하지만 `ShowLoading(true)`가 호출되지 않는 지점들이다. (에디터 전용 스크립트의 `EditorSceneManager`는 런타임과 무관하므로 제외.)

| # | 파일 : 라인 | 상황 | 심각도 | 비고 |
|---|-------------|------|--------|------|
| A | `Presentation/UI/Views/Lobby/Profile/ProfileView.cs:294-301` (`OnLogoutClicked`) | **로그아웃** — `SignOutAsync()`(Firebase/UGS 비동기) 후 `LoadScene("Login")` | 높음 | 비동기 + 씬 전환 둘 다인데 로딩 표시 전혀 없음. 사용자 요청에 명시된 케이스 |
| B | `Presentation/UI/GameEndUI.cs:306-320` (`ReturnToLobby`) ← `OnBackToLobbyClicked:239` | **게임 종료 후 로비 복귀** — NGM 위임 또는 `LoadScene("Lobby")` | 높음 | 네트워크 Shutdown + 씬 전환. 로딩 표시 없음 |
| C | `Presentation/UI/InGameSettingsUI.cs:334-351` (`OnForfeitConfirmed`) | **게임 포기** — `RequestForfeit()` 후 `Hide()`. 싱글=즉시 종료, 멀티=서버 RPC 후 씬 전환 | 높음 | 포기 확정~게임 종료/씬 전환 사이 피드백 없음. 사용자 요청에 명시된 케이스 |
| D | `Infrastructure/Network/NetworkGameManager.cs:607-625` (`BackToLobby`) | **멀티 로비 복귀 내부 처리** — Shutdown 후 `LoadScene(lobbySceneName)` | 높음 | B의 멀티 경로 실제 종착점. B에서 표시하면 커버되나, 직접 호출 경로 확인 필요 |
| E | `Presentation/UI/NetworkStatusUI.cs:225-237` (`OnReturnButtonClicked`) | **연결 끊김 → 로비 복귀** — `ShutdownNetwork()` 후 `LoadScene(_returnSceneName)` | 중간 | 비정상 종료 복귀 경로. 로딩 표시 없음 |
| F | `Infrastructure/Network/NetworkGameEndController.cs:430-463` (`StartRematch`) | **재경기** — 동적 NetworkObject Despawn 후 NGO `LoadScene("Game")` | 중간 | 서버에서만 호출되나 모든 클라이언트가 씬 재전환됨. 양측 로딩 표시 필요 |
| G | `Infrastructure/Network/NetworkGameManager.cs:625` (위 D와 동일 라인) | 위 D 항목과 동일 (`BackToLobby`의 LoadScene) | — | D에 통합 |

### 추가 관찰 사항
- `LoadSingleplayScene`(BattleViewModel.cs:221)은 `ShowLoading(true)` 후 의도적으로 `await Task.Delay(2000)`를 둔다 — 로딩 표시를 확실히 보여주기 위한 패턴. 다른 씬 전환에도 동일한 "표시 → 전환" 순서를 적용해야 함.
- `UIManager.ShowLoading(false)`에는 최소 표시 시간(`_loadingMinDuration`, 기본 1초) 보장 로직이 있다 (`UIManager.cs:162-181`). 즉 씬 전환 직전 `ShowLoading(true)`만 호출하면, 새 씬에서 로딩이 자연스럽게 정리되는 구조와 함께 동작한다. (단, `UIManager`는 DontDestroyOnLoad이므로 씬 전환 후 누가 `ShowLoading(false)`를 호출하는지 Plan에서 확정해야 함.)
- 멀티플레이 경로(B, C-멀티, D, E, F)는 NGO 씬 동기화/Shutdown 타이밍이 얽혀 있어, 단순히 호출만 추가하면 되는 싱글 경로(A, C-싱글)보다 검증 부담이 크다.

---

## 4. `GameSystemRules_UI.md` 현재 LoadingIndicator 규칙 현황

파일: `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UI.md`

- **결론: LoadingIndicator / ShowLoading에 대한 규칙이 전혀 없다.**
- "공통 UI 규칙"(규칙 1~10)에 반응형, SafeArea, CanvasGroup 숨김/표시, 폰트, 팝업 타입 등은 있으나, **로딩 인디케이터를 언제 띄워야 하는지에 대한 규칙은 부재**.
- BlockingOverlay(반투명 배경 오버레이)는 규칙 5에 상세히 기술되어 있으나, 이는 팝업 뒤 입력 차단용이며 로딩 인디케이터(SortingOrder 300, 독립 Canvas)와는 별개 시스템이다.
- Canvas SortingOrder 구조는 별도 문서 `GameSystemRules_CanvasSortingOrder.md`로 분리되어 있다(규칙 본문 상단 링크). LoadingIndicator는 SO 300으로 알려져 있으나, 본 UI 규칙 문서에는 사용 규칙이 없다.

→ Plan.md에서 "공통 UI 규칙"에 **로딩 인디케이터 규칙 신설**이 필요하다.

---

## 5. 조사 범위 메모

- 런타임 씬 전환 코드는 `SceneManager.LoadScene` / NGO `SceneManager.LoadScene` 기준 전수 확인 완료.
- `Assets/Editor/**`, `Assets/_Project/Scripts/Editor/**`의 `EditorSceneManager.*`는 에디터 셋업 1회성 스크립트로 런타임 무관 → 제외.
- `AudioManager.cs`의 `SceneManager.activeSceneChanged` / `GetActiveScene`은 씬 전환을 일으키는 것이 아니라 구독/조회용 → 제외.
- Firebase/UGS 비동기 시작점: 로그인 흐름(2장)은 적용됨, 로그아웃(`SignOutAsync`)만 누락(항목 A).
