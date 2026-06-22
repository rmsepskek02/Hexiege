# Plan — LoadingIndicator 전수 적용

> ✅ **구현 완료 (2026-06-22)** — 미해결 질문 1·2·3 모두 확정 후 반영.
> - 미해결 1(싱글 포기): 옵션 (a) 채택 — 싱글 포기는 로딩 미표시, 멀티 분기에만 표시.
> - 미해결 2(ShowLoading(false) 책임자): 각 목적지 씬 Bootstrapper로 일원화 (규칙 L-3).
>   Login=LoginBootstrapper.ShowLoginSelect, Lobby=LobbyRootView.Start(), Game=GameBootstrapper.LoadMap().
> - 미해결 3(재경기 클라이언트 표시 지점): NetworkGameEndController.NotifyRematchStartingClientRpc
>   → GameEvents.OnNetworkRematchStarting 발행 → GameEndUI 구독해 ShowLoading(true).
> - GameSystemRules_UI.md에 로딩 인디케이터 규칙 L-1~L-4 신설 완료.

## 1. 무엇을 어떻게 고칠 것인가 (자연어 설명)

씬이 바뀌거나 서버와 통신하는 모든 순간에 사용자가 "멈춘 화면"을 보지 않도록, 로딩 인디케이터(`UIManager.ShowLoading(true, "...")`)를 빠짐없이 띄운다. 구체적으로는 **로그아웃, 게임 포기, 로비 복귀, 연결 끊김 복귀, 재경기** 다섯 가지 상황에서 씬 전환 직전에 로딩 표시를 추가한다.

핵심 원칙은 "**전환 직전에 켜기, 새 씬 도착 후 끄기**"다. 로딩을 켠 채로 씬을 넘기면, 새 씬에서 초기화가 끝났을 때 끄면 된다. `UIManager`는 씬이 바뀌어도 사라지지 않는(DontDestroyOnLoad) 전역 객체라서 이 방식이 가능하다.

또한 앞으로 같은 누락이 반복되지 않도록, UI 규칙 문서에 "씬 전환·비동기 작업에는 반드시 로딩 인디케이터를 띄운다"는 규칙을 새로 적는다.

> **근거 규칙**: 현재 `GameSystemRules.md` 및 `GameSystemRules_UI.md`에는 로딩 인디케이터 사용 규칙이 없다(Research 4장). 따라서 본 작업은 "기존 규칙 준수"가 아니라 **규칙 신설 + 신설 규칙에 맞춘 코드 정합화**다. 규칙 초안은 4장 참조.

> **이 문서는 계획이다. 사용자 승인 전까지 코드/문서를 수정하지 않는다.**

---

## 2. 기존 로직 제거 여부 (최상단 고지)

- **기존 로직 제거 없음.** 본 작업은 전부 `ShowLoading(true, "...")` 호출 **추가**다. 기존 씬 전환/Shutdown 로직은 그대로 두고 그 직전에 한 줄을 더하는 형태이므로, 제거/주석 처리 대상이 없다.

---

## 3. 수정이 필요한 위치 목록 (우선순위 순)

각 항목은 Research 3장의 식별자(A~F)에 대응한다.

### [P1 — 높음] 사용자가 명시한 핵심 케이스

**수정 1 (항목 A) — 로그아웃**
- 파일: `Presentation/UI/Views/Lobby/Profile/ProfileView.cs`, `OnLogoutClicked()` (294~301)
- 변경: `SetInteractable(false)` 직후 `UIManager.Instance?.ShowLoading(true, "로그아웃 중...")` 추가. `await SignOutAsync()` → `LoadScene("Login")` 순서는 유지.
- 예외(catch) 경로: 로그아웃 실패 시 `ShowLoading(false)` 추가 (씬 전환이 일어나지 않으므로 반드시 꺼야 함).

**수정 2 (항목 C) — 게임 포기**
- 파일: `Presentation/UI/InGameSettingsUI.cs`, `OnForfeitConfirmed()` (334~351)
- 변경: `_forfeitService.RequestForfeit()` 호출 분기에서 `UIManager.Instance?.ShowLoading(true, "게임을 종료하는 중...")` 추가 후 `Hide()`.
- 주의: 싱글은 즉시 GameEnd 이벤트→GameEndUI 표시로 이어지고, 멀티는 서버 RPC 왕복 후 결과가 온다. 두 경우 모두 "포기 확정 시점"에 표시하는 것이 안전. **단, 싱글의 경우 GameEndUI가 곧바로 위에 뜨므로 로딩을 언제 끌지 확정 필요** → 5장 미해결 질문 참조.

**수정 3 (항목 B) — 게임 종료 후 로비 복귀**
- 파일: `Presentation/UI/GameEndUI.cs`, `ReturnToLobby()` (306~320)
- 변경: `Hide()` 이후, 분기(NGM 위임 / `LoadScene("Lobby")`) 직전에 `UIManager.Instance?.ShowLoading(true, "로비로 돌아가는 중...")` 추가.
- 이 한 곳에서 표시하면 멀티 경로(항목 D, `NetworkGameManager.BackToLobby`)도 함께 커버된다.

### [P2 — 중간] 네트워크 비정상/재경기 경로

**수정 4 (항목 E) — 연결 끊김 복귀**
- 파일: `Presentation/UI/NetworkStatusUI.cs`, `OnReturnButtonClicked()` (225~237)
- 변경: `Time.timeScale = 1f` 이후, `ShutdownNetwork()` / `LoadScene(_returnSceneName)` 직전에 `UIManager.Instance?.ShowLoading(true, "로비로 돌아가는 중...")` 추가.

**수정 5 (항목 F) — 재경기**
- 파일: 재경기 씬 전환은 서버 권위(`NetworkGameEndController.StartRematch`, Infrastructure)에서 일어나지만, **모든 클라이언트**가 씬 재전환을 겪는다.
- 권장 방식: `StartRematch`의 NGO `LoadScene` 직전에서 서버만 호출해선 클라이언트 화면에 안 뜬다. 클라이언트 측에 로딩을 띄우려면 **재경기 확정을 모든 클라이언트가 인지하는 지점**(예: 재경기 수락 ClientRpc 수신 핸들러 / `GameEvents.OnNetworkRematch...` 구독부)에서 `ShowLoading(true, "재경기 준비 중...")`를 호출해야 한다.
- → 정확한 호출 지점은 game-programmer가 재경기 이벤트 흐름(`GameEvents.OnNetworkRematch*` 구독처)을 확인 후 확정. 본 Plan에서는 "재경기 확정을 클라이언트가 받는 지점에서 표시"로 명시.

### [수정 불필요 — 확인만]
- 항목 D(`NetworkGameManager.BackToLobby`)는 수정 3에서 커버. 단 `BackToLobby`가 GameEndUI 외 다른 경로에서도 호출되는지 game-programmer가 호출처 전수 확인 → 다른 진입점이 있으면 그 지점에도 추가.

---

## 4. `GameSystemRules_UI.md`에 추가할 규칙 초안

"공통 UI 규칙" 섹션 안, 규칙 5(CanvasGroup 숨김/표시)와 규칙 6(폰트) 사이에 **"### 로딩 인디케이터"** 하위 섹션을 신설한다. 규칙 번호는 삽입 위치에 맞춰 재정렬하거나 말번호로 추가(문서 컨벤션은 작성 시 결정).

초안:

```markdown
### 로딩 인디케이터

**규칙 N. 로딩 인디케이터 표시 의무**
씬 전환 또는 사용자가 결과를 기다려야 하는 비동기 작업이 발생하는 모든 지점에서는
반드시 로딩 인디케이터를 표시한다. 표시는 단일 API `UIManager.ShowLoading(bool, string)`로만 한다.

대상 상황(예외 없음):
- 씬 전환: SceneManager.LoadScene / NGO SceneManager.LoadScene 직전
  (게임 시작, 로비 복귀, 로그아웃→Login, 재경기, 연결 끊김 복귀 등)
- 외부 서버 비동기 작업: Firebase/UGS 로그인·로그아웃, 매칭, Relay/Lobby 연결
- 게임 포기 확정 후 게임 종료/씬 전환까지의 대기

**규칙 N+1. 표시/숨김 시점**
- 켜기: 전환/비동기 작업을 시작하기 "직전"에 ShowLoading(true, 안내문구)를 호출한다.
- 끄기: 작업이 정상 완료되어 새 화면이 준비된 시점, 또는 예외로 전환이 무산된 시점에 ShowLoading(false)를 호출한다.
- 비동기 작업의 예외(catch) 경로에서는 씬 전환이 일어나지 않으므로 반드시 ShowLoading(false)로 명시적으로 숨긴다.

**규칙 N+2. null-safe 호출**
항상 `UIManager.Instance?.ShowLoading(...)` 패턴을 사용한다
(Lobby/Game 씬 단독 실행 시 Instance가 null일 수 있음).
Login 씬은 LoginBootstrapper.ShowLoading 위임 래퍼를 사용한다.

**규칙 N+3. 안내 문구**
ShowLoading(true) 호출 시 사용자에게 현재 무엇을 기다리는지 알리는 한국어 문구를 함께 전달한다.
(예: "게임 로딩 중...", "로비로 돌아가는 중...", "로그아웃 중...", "재경기 준비 중...")

**규칙 N+4. 최소 표시 시간**
ShowLoading(false) 호출 시 최소 표시 시간(UIManager._loadingMinDuration, 기본 1초)이
지나지 않았으면 남은 시간만큼 대기 후 숨긴다(깜빡임 방지). 이 처리는 UIManager가 담당하므로 호출부는 신경 쓰지 않는다.
```

> 규칙 번호(N)는 기존 규칙 1~10에 이어 부여하거나, 공통 규칙 내 하위 섹션 번호 체계에 맞춰 game-programmer/문서 담당이 확정한다.

---

## 5. 미해결 질문 / 사용자 확인 필요 사항

코드 수정 전 아래를 확정해야 한다 (추정 금지 — 사용자/구현 에이전트 확인 필요).

1. **싱글플레이 포기(수정 2)에서 로딩을 언제 끌 것인가?**
   포기 확정 → GameEnd 이벤트 → GameEndUI가 즉시 위에 뜬다. 이 경우 로딩을 켜면 GameEndUI를 가릴 수 있다. 옵션:
   - (a) 싱글 포기는 즉시 결과창이 뜨므로 로딩을 띄우지 않는다(멀티만 띄움).
   - (b) GameEndUI 표시 시점에 ShowLoading(false)를 호출해 자연스럽게 교체.
   → 어느 쪽으로 할지 확인 필요.

2. **씬 전환 후 ShowLoading(false)의 책임자.**
   Game/Lobby 씬 진입 후 누가 로딩을 끄는가? 현재 `LoadSingleplayScene`은 켜기만 하고, 새 씬 초기화 완료 시점의 끄기 주체가 명시돼 있지 않다. 각 씬의 Bootstrapper 초기화 완료 지점에서 `ShowLoading(false)`를 호출하는 공통 패턴을 둘지 확정 필요.

3. **재경기(수정 5) 클라이언트 표시 지점.**
   `GameEvents.OnNetworkRematch*` 구독 흐름 확인 후 정확한 호출 지점 확정 필요 (game-programmer 조사).

---

## 6. Inspector 작업 필요 여부

- **불필요.** 본 작업은 코드 호출 추가 + 문서 수정뿐이다. LoadingIndicator 자체(독립 Canvas, SortingOrder 300)는 이미 UIManager 프리팹에 구성되어 동작 중이므로 신규 Inspector 작업이 없다.

---

## 7. 아키텍처 제약 점검

- `UIManager`는 Presentation 레이어. 호출부 중 Infrastructure(NetworkGameManager, NetworkGameEndController)에서 직접 `UIManager`를 참조하면 레이어 방향이 어색해질 수 있다 → 가능하면 **Presentation 진입점**(GameEndUI, NetworkStatusUI, ProfileView, InGameSettingsUI)에서 표시하도록 배치(수정 1~4가 이 원칙을 따름). 재경기(수정 5)만 Infrastructure 흐름이 얽히므로 game-programmer가 Presentation 측 구독 지점에서 표시하도록 설계.
- 모든 호출은 `UIManager.Instance?.ShowLoading(...)` null-safe 패턴 준수.

---

## 8. 테스트 체크리스트 (구현 후 사용자 실기용)

- [ ] 로비 ProfileView에서 로그아웃 → 로딩 표시 후 Login 씬 진입 (수정 1)
- [ ] 로그아웃 실패(네트워크 차단 등) 시 로딩이 사라지고 상호작용 복구 (수정 1 예외)
- [ ] 싱글 게임에서 포기 확정 → (확정된 정책대로) 로딩/결과창 표시 (수정 2 + 미해결 1)
- [ ] 멀티 게임에서 포기 확정 → 로딩 표시 후 결과/씬 전환 (수정 2)
- [ ] 게임 종료 후 "로비로" → 로딩 표시 후 Lobby 진입 (싱글/멀티 양쪽) (수정 3)
- [ ] 멀티 연결 끊김 팝업 "확인" → 로딩 표시 후 복귀 (수정 4)
- [ ] 커스텀게임 재경기 수락 → 양 클라이언트 모두 로딩 표시 후 Game 재진입 (수정 5)
- [ ] 모든 경로에서 새 씬 도착 후 로딩이 정상적으로 사라짐 (미해결 2 정책 확정 후)

---

## 9. SceneLoader 도입 — 씬 전환 로딩 인디케이터 자동화 (2026-06-22 추가)

### 9.1 무엇을 왜 하는가 (자연어 설명)

위 3~8장은 씬 전환 직전마다 `UIManager.Instance?.ShowLoading(true, "...")`를 **개발자가 직접 한 줄씩 추가**하는 방식이었다. 이 방식은 새로운 씬 전환 코드를 작성할 때마다 로딩 표시를 빠뜨릴 위험이 있다.

그래서 **모든 씬 전환을 단일 진입점(`SceneLoader.Load`)으로 통일**한다. 이 클래스는 씬을 로드하기 직전에 로딩 인디케이터를 자동으로 켜주므로, 개발자가 로딩 호출을 빠뜨릴 수 없다. 씬 이름도 상수(`SceneLoader.Login/Lobby/Game`)로 관리해 문자열 오타를 방지한다.

### 9.2 생성 위치 및 역할

- **신규 파일**: `Assets/_Project/Scripts/Presentation/UI/SceneLoader.cs`
- **네임스페이스**: `Hexiege.Presentation`
- **역할**: 정적 유틸리티. `Load(sceneName, message = null)` 호출 시 ① `UIManager.Instance?.ShowLoading(true, msg)` 자동 호출 → ② `SceneManager.LoadScene(sceneName)` 수행.
- 씬 이름 상수: `Login` / `Lobby` / `Game`.
- 기본 메시지: 씬 이름별 기본 안내 문구 제공(message 미지정 시). Login="로그인 화면으로 이동 중...", Lobby="로비로 이동 중...", Game="게임을 불러오는 중...".
- 로딩을 끄는 책임은 기존과 동일하게 **목적지 씬 Bootstrapper**(규칙 L-3): LoginBootstrapper.ShowLoginSelect / LobbyRootView.Start / GameBootstrapper.Map.LoadMap.

### 9.3 교체한 파일 목록 (기존 `SceneManager.LoadScene` → `SceneLoader.Load`)

| 파일 | 위치 | 기존 | 변경 |
|------|------|------|------|
| `Presentation/UI/ViewModels/BattleViewModel.cs` | `LoadSingleplayScene()` | `SceneManager.LoadScene("Game")` | `SceneLoader.Load(SceneLoader.Game, "게임 로딩 중...")` (2초 대기 UX 때문에 선행 ShowLoading은 유지) |
| `Presentation/UI/Views/Lobby/Profile/ProfileView.cs` | `OnLogoutClicked()` | `SceneManager.LoadScene(_loginSceneName)` | `SceneLoader.Load(_loginSceneName, "로그아웃 중...")` (비동기 대기 위한 선행 ShowLoading 유지) |
| `Presentation/UI/NetworkStatusUI.cs` | `OnReturnButtonClicked()` | 선행 `ShowLoading(true,...)` + `SceneManager.LoadScene(_returnSceneName)` | 선행 ShowLoading 제거, `SceneLoader.Load(_returnSceneName, "로비로 이동 중...")` |
| `Presentation/UI/GameEndUI.cs` | `ReturnToLobby()` | `SceneManager.LoadScene("Lobby")` | `SceneLoader.Load(SceneLoader.Lobby, "로비로 이동 중...")` (NGM 분기 커버 위한 선행 ShowLoading 유지) |
| `Bootstrap/LoginBootstrapper.cs` | `GoToNextScene()` | `SceneManager.LoadScene(_nextSceneName)` | `SceneLoader.Load(_nextSceneName)` |
| `Infrastructure/Network/NetworkGameManager.cs` | `BackToLobby()` | `SceneManager.LoadScene(lobbySceneName)` | `SceneLoader.Load(lobbySceneName)` (+ `using Hexiege.Presentation` 추가) |

**using 정리**: 위 파일 중 `SceneManager`가 더 이상 코드에서 쓰이지 않게 된 BattleViewModel / ProfileView / NetworkStatusUI / GameEndUI / LoginBootstrapper는 `using UnityEngine.SceneManagement` 제거. NetworkGameManager는 NGO `LoadSceneMode` 사용 때문에 해당 using 유지.

### 9.4 NGO SceneManager는 교체 제외 — 이유

- `Infrastructure/Network/NetworkGameEndController.cs:470` 의 `NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single)` 은 **교체하지 않았다**.
- 이유: 이것은 NGO(Netcode for GameObjects)의 네트워크 씬 매니저로, 서버가 씬을 로드하면 모든 클라이언트에 자동 동기화하는 별도 메커니즘이다. 일반 `UnityEngine.SceneManagement.SceneManager.LoadScene`(로컬 단독 전환)과 동작 방식이 근본적으로 다르므로 `SceneLoader`가 담당하지 않는다.

### 9.5 아키텍처 점검

- `SceneLoader`는 Presentation 레이어. Infrastructure의 `NetworkGameManager`가 이를 참조하지만, 동일 레이어 파일(`NetworkUnit`, `NetworkGameEndController`)이 이미 `Hexiege.Presentation`(UIManager/EffectManager)을 참조하고 있어 기존 의존 방향과 일치한다. 새로운 레이어 위반 없음.
