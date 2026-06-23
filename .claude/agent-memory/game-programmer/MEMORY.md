# Game Programmer Agent Memory

## CRITICAL — GIT 명령 절대 금지
- **모든 git 명령은 사용자가 명시적으로 직접 언급하지 않는 한 절대 실행 금지**
- 2026-03-03 사고: git restore 무단 실행 → 커밋 안 된 작업 전체 삭제 (복구 불가)
- 코드 상태 확인 필요 시 Read/Grep 도구만 사용

## CRITICAL — 구현 시 필수 확인 제약

### 레이어 제약
- Domain: `using Hexiege.Core` 절대 금지 → HexOrientationContext 등 정적 홀더 패턴
- NetworkBehaviour: Infrastructure 레이어에만 (Presentation/Application 금지)
- Application: Unity.Netcode 직접 참조 금지 → NetworkContext 정적 홀더 패턴
- GameBootstrapper = 유일한 의존성 조합 루트
- Assembly Definition 없음 — 네임스페이스 규약만

### NGO API 제약
- ServerRpc/ClientRpc 메서드명: 반드시 `ServerRpc`/`ClientRpc`로 끝나야 함
- NGO 2.9.2, Enable Scene Management = ON
- NetworkBehaviour는 씬에 NetworkObject로 배치해야 RPC 작동
- RPC 파라미터: 직렬화 가능 타입만 (INetworkSerializable 또는 기본 타입/enum)
- 클라이언트 전용 분기: `NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer`
- **NetworkObject.Despawn(destroy: true)**: 서버에서만 호출 가능. `Destroy(gameObject)`는 NGO 클라이언트 전파 불보장 — 반드시 Despawn 명시 호출 (2026-06-08 확인)
- **유닛 사망 패턴**: NetworkCombatController.OnUnitDied()에서 EntityDiedClientRpc 발행 후 `NetworkObject.Despawn(true)` 호출. 클라이언트는 `NetworkUnit.OnNetworkDespawn()`에서 이펙트 재생. UnitView(Presentation)는 Unity.Netcode 직접 참조 금지 — NetworkContext 홀더 패턴만 사용

## 최근 작업

### 로그인 팝업 CloseButton 무반응 수정 (2026-06-23) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-23/04_49_cancel-button-fix/`

**원인 패턴**: CloseButton GO가 씬에 활성화 상태로 존재해도 C# 코드에 `[SerializeField] private Button _closeButton` 필드가 없으면 Inspector 연결 자체가 불가 → 클릭 리스너 등록 안 됨 → 무반응.

**수정 내용**:
- `AnonymousWarningPopup.cs`: `_closeButton` 추가 + `OnCloseButtonClicked()` → `Hide()`. `SetInteractable()`에 포함 (로그인 진행 중 취소 방지).
- `NetworkErrorPopup.cs`: `_closeButton` 추가 + `OnCloseButtonClicked()` → `Hide()`. 기존 `_confirmButton`(ConfirmButton GO)은 유지.

**씬 구조 확인 패턴**: Login.unity 내 팝업 Inspector 연결 상태는 씬 파일에서 MonoBehaviour 섹션의 SerializeField 값(`{fileID: 0}` = 미연결)으로 확인 가능.

---

### LoadingIndicator 전수 적용 + 관련 버그 수정 (2026-06-22~23) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-22/16_37_loading-indicator-full-coverage/`

**핵심 패턴**:
- `SceneLoader` 정적 유틸리티 신규 (`Hexiege.Presentation`): 모든 씬 전환 단일 진입점. `UIManager.LoadSceneWithDelay` 위임 — ShowLoading(true) 즉시 실행 → 1초 대기 → LoadScene.
- **ShowLoading 호출 위치**: 코루틴 외부에서 동기 실행 필수. 코루틴 내부에 두면 다음 프레임에 실행되어 텍스트 지연 발생.
- **ShowLoading(false) 책임자(규칙 L-3)**: Login=LoginBootstrapper, Lobby=LobbyRootView.Start(), Game=GameBootstrapper.LoadMap().
- **Infrastructure→Presentation 직접 참조 금지**: `NetworkGameManager`가 `SceneLoader`를 직접 호출하면 레이어 위반. `GameEvents.OnNetworkBackToLobby`(Subject<string>) 이벤트 경유 → `GameEndUI`(Presentation)가 구독해 `SceneLoader.Load` 호출.
- **재경기 로딩**: `NetworkGameEndController` → `GameEvents.OnNetworkRematchStarting` 발행 → `GameEndUI` 구독해 ShowLoading(true). 동일 패턴.
- **초기 메시지 누락 주의**: `ShowLoading(true)` 메시지 없이 호출하면 배경/스피너만 켜지고 텍스트 공백. 반드시 메시지 함께 전달.

**수정된 파일 목록**:
- `SceneLoader.cs` (신규)
- `IUIManager.cs` (`LoadSceneWithDelay` 추가)
- `UIManager.cs` (`LoadSceneWithDelay`, `LoadSceneRoutine` 추가 — ShowLoading 코루틴 외부 이동)
- `LoginBootstrapper.cs` (ShowLoading(false) 위치 수정)
- `GameEvents.cs` (`OnNetworkRematchStarting`, `OnNetworkBackToLobby` Subject 추가)
- `NetworkGameEndController.cs` (`NotifyRematchStartingClientRpc` 추가)
- `NetworkGameManager.cs` (`using Hexiege.Presentation` 제거, `GameEvents.OnNetworkBackToLobby.OnNext` 발행)
- `GameEndUI.cs` (OnNetworkRematchStarting/OnNetworkBackToLobby 구독)
- `InGameSettingsUI.cs` (멀티 포기 분기만 ShowLoading)
- `ProfileView.cs` (로그아웃 ShowLoading)
- `LobbyRootView.cs` (ShowLoading(false))
- `GameBootstrapper.Map.cs` (ShowLoading(false))
- `AnonymousWarningPopup.cs` ("로그인 중..." 초기 메시지 추가)
- `NetworkStatusUI.cs` (`_returnSceneName` 기본값 → `SceneLoader.Lobby`)

---

### ConfirmPopup z-order 버그 수정 (2026-06-22) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-22/15_09_ingame-settings-confirm-popup-zorder/`

**근본 원인**: ConfirmPopup 프리팹 루트에 자체 Canvas가 없어, 부모 UIManager Canvas(SO=100)를 그대로 따라감.  
InGameSettings 패널 하위 `Panel` GO에는 Canvas Override(SO=200)가 있어 200 > 100으로 ConfirmPopup이 항상 뒤에 렌더링됨.

**수정**: ConfirmPopup.prefab 루트에 Canvas(Override Sorting=true, SortingOrder=250) + GraphicRaycaster 추가 (Inspector 직접 작업).

**Canvas SortingOrder 최종 구조** (전체 확정):
```
SO 0   → [UI] Canvas (Game 씬 HUD)
SO 100 → UIManager Canvas (BlockingOverlay)
SO 200 → 각 패널 Canvas Override (BuildingPopup, BuildingActionPanel, InGameSettings, GameEndPanel, ProductionPopup)
SO 250 → ConfirmPopup 독립 Canvas (모달 팝업 — 항상 패널 위)
SO 300 → LoadingIndicator 독립 Canvas
```

**에디터 스크립트**: `Assets/Editor/Fix/Fix_AddCanvasToConfirmPopup.cs`  
(메뉴: `Hexiege/Fix/Add Canvas To ConfirmPopup (SO=250)`)  
`LoadPrefabContents` 환경에서는 직접 프로퍼티 대입이 직렬화에 반영되지 않는 경우가 있음 — 반드시 `SerializedObject.FindProperty + ApplyModifiedPropertiesWithoutUndo` 방식 사용.

**참조 문서**: `Assets/_Project/Docs/GameSystemRules/GameSystemRules_CanvasSortingOrder.md`

---

### Canvas SortingOrder + BlockingOverlay 렌더링 수정 (2026-06-22) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-22/09_32_canvas-sorting-order-fix/`

**근본 원인**: UIManager GameObject가 Login.unity에서 `[UI Systems]` 하위 자식으로 배치되어 있었음.  
`DontDestroyOnLoad`는 **루트 GameObject에만 작동** — 자식 오브젝트에는 적용되지 않음.  
→ 씬 전환 시 UIManager가 파괴되어 Game 씬에서 `UIManager.Instance == null`.  
→ 모든 패널의 `Show()`에서 `UIManager.Instance?.ShowBlockingOverlay(...)` 호출이 null-safe 스킵됨.

**수정**: Login.unity Hierarchy에서 UIManager를 `[UI Systems]` 밖 루트 레벨로 이동 (씬 Inspector 작업).

**Canvas SortingOrder 최종 구조**:
```
SO 0   → [UI] Canvas (Game 씬 HUD)
SO 100 → UIManager Canvas (BlockingOverlay + ConfirmPopup)
SO 200 → 각 패널 Canvas Override (BuildingPopup, BuildingActionPanel, InGameSettings, GameEndPanel, ProductionPopup)
SO 300 → LoadingIndicator 독립 Canvas
```

**Game.unity 씬 작업**: 5개 패널 GO에 Canvas(Override Sorting=true, SO=200) + GraphicRaycaster 추가.

**핵심 교훈**:
- `DontDestroyOnLoad`는 반드시 루트 GO에만 작동. 자식 배치 시 씬 전환마다 재생성+즉시파괴 반복.
- 런타임 로그로 확인: `ApplyBlockingOverlayVisibility` 반복 호출 = UIManager 매번 새로 생성되는 신호.
- 게임 씬 패널이 UIManager보다 높은 SO 필요 시 Canvas Override 사용 (GameSystemRules_UI Rule 4).
- `Hexiege.Application` 네임스페이스가 `UnityEngine.Application`을 가림 → `Application.dataPath` 등 UnityEngine.Application 멤버는 반드시 `UnityEngine.Application.xxx` 명시.

---


### LoadingIndicator 최소 표시 시간 + ConfirmPopup/NetworkErrorPopup 버그 수정 (2026-06-22) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-22/05_50_loading-indicator-min-duration/`

**핵심**:
- `UIManager.ShowLoading(false)` 호출 시 최소 표시 시간(`_loadingMinDuration`, 기본 1f초)이 지나지 않았으면 `WaitForSecondsRealtime`으로 지연 후 숨김
- `_loadingShowTime` 기록 + `_hideLoadingCoroutine` 코루틴 관리 (중복 hide 방지)
- AnonymousWarningPopup(SortingOrder=200)에 가리는 문제: LoadingIndicator에 독립 Canvas(SortingOrder=300) 추가 (`LoginUiSetup.cs` 에디터 스크립트 메뉴 항목 추가)

**ConfirmPopup + NetworkErrorPopup 버그 원인 및 수정**:
- **ConfirmPopup 패널 미표시**: 씬 프리팹 인스턴스 오버라이드로 루트 CanvasGroup alpha=0, interactable=0 강제됨 → Inspector에서 alpha=1, interactable=true로 수정
- **NetworkErrorPopup 패널 미표시**: `_panel` 슬롯 null → `Show()/Hide()`에서 `if (_panel != null)` 분기가 모두 건너뜀. 컴포넌트 교체 시 슬롯 재연결 필수
- **버튼 클릭 무반응**: `NetworkErrorPopup.Initialize()`가 호출되지 않아 버튼 콜백 미등록 → LoginBootstrapper의 `_networkErrorPopup` 슬롯 연결 누락이 원인
- **주의**: NetworkErrorPopup은 LoginRootView + **LoginBootstrapper** 두 곳 모두 슬롯 연결 필요. LoginBootstrapper가 `Initialize()` 호출, LoginRootView가 `Show()` 호출

### Lobby 패널 CanvasGroup 에디터 사전 부착 + LobbyRootView 단순화 (2026-06-22) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-22/05_59_lobby-canvasgroup-preattach/`

**핵심**: 런타임 `AddComponent` 방식(EnsureCanvasGroup)을 에디터 사전 부착 방식으로 전환.
- `Assets/Editor/Setup/SetupLobbyPanelCanvasGroups.cs` 에디터 스크립트 신규 (`Hexiege/Setup/Lobby 패널 CanvasGroup 설정` 메뉴)
  - BattlePanel/ShopPanel/ProfilePanel/RankingPanel 4개 모두 `SetActive(true)`
  - 4개 패널에 CanvasGroup 부착 (없는 경우에만)
  - BattlePanel: alpha=1, blocksRaycasts=true, interactable=true
  - ShopPanel/ProfilePanel/RankingPanel: alpha=0, blocksRaycasts=false, interactable=false
- `LobbyRootView.Awake()`: `EnsureCanvasGroup()` → `GetComponent<CanvasGroup>()` 교체
- `EnsureCanvasGroup()` 헬퍼 메서드 제거

**원칙**: 컴포넌트 부착은 런타임 코드가 아닌 에디터에서 미리 해두는 것이 원칙 (GameSystemRules_UI.md Rule 5).

### ProfileView 로그아웃 버튼 추가 (2026-06-22) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-22/04_34_lobby-profile-logout-button/`

**핵심**: Firebase 익명 로그인 성공 후 로비에서 로그아웃 가능한 임시 버튼 추가.
- `Assets/Editor/Setup/AddLogoutButtonToProfileView.cs` 에디터 스크립트 신규 (`Hexiege/Setup/ProfileView 로그아웃 버튼 추가` 메뉴)
  - ProfilePanel에 ProfileView 컴포넌트 부착 (없는 경우)
  - LogoutButton GO 생성 (Button + Image + TextMeshProUGUI "로그아웃", Maplestory Bold SDF)
  - SerializedObject로 `ProfileView._logoutButton` 필드 자동 연결
  - 임시 RectTransform: 하단 고정 앵커 (Rule 2 임시 — 추후 ProfileView 전체 재설계 시 수정 예정)
- 코드 변경 없음 — ProfileView._logoutButton + OnLogoutClicked()는 이미 구현 완료 상태였음.

### BlockingOverlay UIManager 단일 소유 통합 (2026-06-21) — 코드 완료, 씬/실기 테스트 대기
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-21/07_30_blocking-overlay-canvasgroup-fix/Plan.md`
**핵심**: 각 팝업이 개별 소유하던 반투명 배경 오버레이를 UIManager 단일 소유로 통합 (SafeArea 갇힘 문제 해결).
- `IUIManager`: `ShowBlockingOverlay(System.Action onTap = null)` + `HideBlockingOverlay()` 추가.
- `UIManager`: `_blockingOverlay`(CanvasGroup) + `_blockingOverlayButton`(Button) 필드. Modal(onTap=null, 입력 차단만) / Popup(onTap!=null, 터치 시 콜백) 2모드. **중첩은 `_blockingOverlayRefCount` 참조 카운터**로 관리 — 0일 때만 실제 숨김. 카운터 언더플로 가드 있음(`if(>0)`).
- 호출부(Modal): ConfirmPopup, AnonymousWarningPopup, RematchRequestPopup. (Popup): InGameSettingsUI, BuildingPlacementUI, BuildingPanelBase(=ProductionPanelUI/BuildingActionPanelUI 상속).
- **RematchRequestPopup 주의**: 요청→거절 패널 전환 시 ShowRequest 후 ShowDeclined가 둘 다 +1 하면 Hide 1회로 카운터 0 안됨 → `_overlayShown` bool 가드 + `ShowOverlayOnce()`/`HideOverlayOnce()` 헬퍼로 항상 0/1만 점유.
- 기존 로직(`_blockingOverlay`/`_sharedBackground`/`_overlay`/`_overlayCg`/`_overlayFade`)은 삭제 금지, 전부 주석 처리(`[구로직 — 테스트 통과 후 삭제]`)로 보존.
- 씬 작업([10])은 별도(.unity Inspector): UIManager Canvas 직속 BlockingOverlay GameObject 생성 필요 — 이번 코드 작업 범위 외.
- 규칙 문서: `GameSystemRules_UI.md` 공통 규칙 4(SafeArea)/5(CanvasGroup)에 BlockingOverlay 단일 소유 패턴 명문화.

### 전역 UIManager + SplashOverlayView (2026-06-16) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-16/12_25_global-ui-system/`
**테스트**: 사용자 실기 PASS (2026-06-18, TC-01~07 전체)

**핵심 설계**: 씬마다 중복 배치되던 공통 UI(ConfirmPopup/LoadingIndicator/ToastUI)를 Login 씬에서 1회 생성하는 전역 UIManager로 통합. UIManager는 `SingletonMonoBehaviour<UIManager>` + `IUIManager`, DontDestroyOnLoad로 전 씬 공유.

**신규 파일**:
- `Presentation/UI/Core/IUIManager.cs` — `ShowConfirm(message, onConfirm, onCancel=null, confirmLabel="확인", cancelLabel="취소")` + `ShowLoading(bool show, string message = "")`. ShowLoading은 모든 로딩 사유(씬 전환/Firebase/매칭 등)를 단일 API로 통합.
- `Presentation/UI/UIManager.cs` — IUIManager 구현체. _confirmPopup(ConfirmPopup)/_loadingIndicator 참조 보유. ConfirmPopup/LoadingIndicator는 UIManager Canvas(SortingOrder 100) 하위에 임베드.
- `Presentation/UI/SplashOverlayView.cs` — SetStatus("로딩 중...")/ShowTapToStart(DOTween alpha 0↔1 깜빡임)/FadeOut(onComplete). 전용 Canvas(SortingOrder 200), Background(SafeArea 밖) + SafeAreaContainer(텍스트 안). 자동 로그인 성공 시 탭 없이 FadeOut→Lobby.
- `Presentation/UI/Common/SpinnerRotator.cs` — LoadingIndicator Spinner 회전. 프리팹 추출 시 에디터 스크립트가 자동 부착.
- `Debug/UIManagerTestButtonHandler.cs` — 임시 테스트용.
- `Assets/Editor/Setup/ExtractUIManagerPrefabs.cs` — ConfirmPopup(Game.unity)/ToastUI·LoadingIndicator(Lobby.unity) 프리팹 추출. LoadingIndicator는 B안(Canvas/CanvasScaler/GraphicRaycaster/LoadingScreen.cs 제거 후 추출).
- `Assets/Editor/Setup/SetupUIManagerInScene.cs` — Login.unity에 UIManager Canvas + 프리팹 배치.
- `Assets/Editor/Setup/AddUIManagerTestButton.cs` — 임시 테스트용.

**수정 파일**:
- `Bootstrap/LoginBootstrapper.cs` — SplashOverlayView 연동, ShowLoading을 UIManager로 위임. 흐름: SetStatus→초기화→ShowTapToStart→탭→FadeOut→로그인/Lobby.
- `Bootstrap/GameBootstrapper.cs` — 미사용 `_confirmPopup` SerializeField 제거(UIManager로 이전).
- `Presentation/UI/ViewModels/BattleViewModel.cs` — LoadingScreen → `UIManager.ShowLoading()` 전환.
- `Presentation/UI/InGameSettingsUI.cs` — ConfirmPopup → `UIManager.ShowConfirm()`.
- `Presentation/UI/Views/Login/LoginRootView.cs` — ConfirmPopup/networkErrorPopup → UIManager, 입력 → New Input System.
- `Presentation/UI/Views/Lobby/Profile/ProfileView.cs` — ConfirmPopup → `UIManager.ShowConfirm()`.

**ToastUI 배치**: 자체 Canvas 내장 + Awake에서 SetParent(null)+DontDestroyOnLoad → UIManager Canvas 밖, 씬 루트에 직접 배치.

**null-safe**: UIManager 미생성 씬(Lobby/Game) 단독 실행 시 `UIManager.Instance?.` 패턴으로 안전 무시(TC-07 PASS).

### Lobby.unity 규칙 전수 점검 및 추가 수정 (2026-06-15) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-15/18_12_lobby-rule-violations/`
- **Rule 5**: `LobbyUI.cs` `_lobbyPanel` 타입 `GameObject`→`CanvasGroup`. Show: alpha=1/blocksRaycasts=true/interactable=true. Hide: alpha=0/false/false. (LobbyUI는 현재 씬 미배치 — 코드만 수정)
- **Rule 6**: `LoadingScreen>SafeAreaContainer>StatusText` TMP 폰트 `LiberationSans SDF`→`Maplestory Light SDF` 교체. `FixLobbyRuleViolations.cs` 에디터 스크립트(메뉴 `Hexiege/Setup/Lobby 규칙 위반 수정`) 실행 완료.
- **클린업**: `BattleRootView.cs` 미사용 `using System;` 제거.
- **AnonymousWarningPopup._blockingOverlay**: Login.unity 소속으로 확인 → Login 씬 작업으로 이관.
- **YAML 전수 점검 결과**: Rule 1(Canvas Scaler 1080×1920 ScaleWithScreenSize) ✅, Rule 2(sizeDelta 위반 없음) ✅, Rule 4(SafeAreaFitter 3곳 모두 부착) ✅, Rule 5(LobbyUI 코드 수정 완료) ✅, Rule 6(Maplestory 폰트로 교체) ✅.

### AnimatedPanel/UIAnimator/ConfirmPopup SetActive→CanvasGroup 리팩토링 (2026-06-13~15) ✅ 완료
**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-12/09_13_animatedpanel-canvasgroup-refactor/`
- `UIAnimator.cs`: 이미 이전 세션에서 SetActive 제거 완료 상태였음(Show=interactable/blocksRaycasts=true, Hide OnComplete=interactable/blocksRaycasts=false). 추가 변경 없음.
- `AnimatedPanel.cs`: EnsureInitialized()에 `_cg.alpha=0/blocksRaycasts=false/interactable=false` 명시 추가. `_backgroundOverlay`(CanvasGroup 타입) Show=alpha1/raycast/interactable=true, Hide=0/false/false. SetActive 호출 전부 제거 + 주석 갱신.
- `ConfirmPopup.cs`: `_blockingOverlay` 타입 GameObject→CanvasGroup 변경. Show()에서 alpha=1/blocksRaycasts/interactable=true, Hide()에서 alpha=0/false/false. `_panel.gameObject.SetActive(true)` 제거.
- `RematchRequestPopup.cs`: 모든 SetActive 제거. Awake()에서 CanvasGroup 초기값 설정. FadeIn/FadeOut에서 interactable 제어 추가.
- `ProductionPanelUI.cs`: `_unitLockIndicators` List<GameObject>→List<CanvasGroup> 변경. `_unitBorderOverlays` CanvasGroup 캐시 추가. SetActive→CanvasGroup alpha 제어.
- `InGameSettingsUI.cs`: Show()에서 `_panel.gameObject.SetActive(true)` 제거(AnimatedPanel 항상 active이므로 불필요).
- `AnonymousWarningPopup.cs`: `_panel.gameObject.SetActive(true)` 제거. `_blockingOverlay.SetActive()` 유지(Lobby 씬 별도 작업 예정).
- **씬 작업**: `FixRule5Violations.cs`(메뉴 `Hexiege/Setup/규칙5 위반 수정`) 에디터 스크립트 실행 완료. AnimatedPanel 오브젝트 전부 활성화, _unitLockIndicators Inspector 재배선 완료. Game.unity 비활성 오브젝트 2개(Background CanvasGroup 의도적, NetworkManager 비UI)만 남음 — 위반 없음.
- **AnimatedPanel GUID**: `b97e76d0453d56e4b961752cd52c6eb6`. 씬 YAML에서 m_IsActive 조회: MonoBehaviour(114) 중 GUID 매칭 → m_GameObject fileID → 해당 GO body의 m_IsActive 확인.

### 사운드 시스템 (AudioManager + SFX/BGM 분리) (2026-06-10) 🔵 코드 완료 / Inspector 작업 + 실기 테스트 예정

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-10/09_28_sound-system/`
**브랜치**: `claude/sound-system-review-itwt0t`

**핵심 설계**: EffectManager=VFX 전용, AudioManager=BGM+SFX 전담 (GameSystemRules_Sound 규칙 1). VFX+SFX 쌍은 같은 호출지점에서 두 매니저를 연달아 호출(규칙 15).

**신규 파일**:
- `Infrastructure/Config/SoundConfig.cs` — BGM 4종(login/lobby/battle/gameEnd)+crossfadeDuration, UnitSoundEntry/BuildingSoundEntry List→Dict 캐싱(UnitEffectConfig 패턴). Domain 타입(UnitType/BuildingType)만 키. **이전 세션에서 이미 생성됨**.
- `Presentation/Audio/AudioManager.cs` — `SingletonMonoBehaviour<AudioManager>` 상속(DontDestroyOnLoad). `enum BgmType{Login,Lobby,Battle,GameEnd}` + `struct UiSoundEntry{UiEffectKey,clip,volume}`(규칙 4 — UI SFX는 SoundConfig가 아닌 AudioManager 직접 보유). BGM 크로스페이드(AudioSource A/B 번갈아, unscaledDeltaTime — timeScale=0 무관), SFX 풀(EffectManager에서 이전, spatialBlend=0, sfxGroup 연결, 동시 8개), 볼륨 PlayerPrefs("MasterVolume"/"BGMVolume"/"SFXVolume", 0~1→`Log10(Max(v,0.0001))*20` dB). Awake에서 SceneManager.activeSceneChanged+OnGameStarted+OnGameEnd 구독, Initialize()에서 현재 씬 BGM 즉시 재생(규칙 7).

**수정 파일 (SFX 코드는 주석 비활성화 — WORKFLOW 규칙, "SOUND_SYSTEM_REFACTOR" 마커. 실기 PASS 후 삭제)**:
- `Presentation/Effects/EffectPreset.cs` — _sfxClip/_sfxVolume/SfxClip/SfxVolume 주석 처리. VfxPrefab만 활성.
- `Presentation/Effects/EffectManager.cs` — _sfxPool/_activeSfxCount/_sfxContainer/_maxConcurrentSfx + GetOrCreateSfx/CreateSfxSource/ReturnSfxAfterPlay + Play() SFX 블록 + Initialize SFX 풀 생성 전부 주석. `using System.Collections;`는 주석코드용이라 잔존(미사용이나 무해).
- `Presentation/Unit/UnitView.cs` — OnAttackHit(공격)/OnUnitDied 핸들러에 `AudioManager.Instance?.PlayUnitAttackSfx/PlayUnitDeathSfx` 추가(VFX 호출 바로 아래).
- `Infrastructure/Network/NetworkUnit.cs` — OnNetworkDespawn 클라이언트 사망에 PlayUnitDeathSfx 추가.
- `Bootstrap/LoginBootstrapper.cs` — `_soundConfig` SerializeField + Start() 맨앞 `AudioManager.Instance?.Initialize(_soundConfig)`.
- `Presentation/UI/InGameSettingsUI.cs` — _volumePanelGroup/_masterSlider/_bgmSlider/_sfxSlider 추가. _soundButton→볼륨패널 토글(CanvasGroup alpha, UI 규칙 5). 슬라이더 초기값 GetXxxVolume, onChange SetXxxVolume.

**Inspector 작업 필요 (코드로 불가)**:
1. AudioMixer.mixer 에셋 생성(`Assets/_Project/Audio/`) — Master→BGM/SFX 그룹, Exposed `MasterVolume`/`BGMVolume`/`SFXVolume`(0dB).
2. Login.unity `[Audio]` GO에 AudioManager + BGM AudioSource A/B 자식 + SFX Container 자식 + 믹서 그룹 연결.
3. SoundConfig.asset 생성(`Resources/Config/`), LoginBootstrapper._soundConfig 연결.
4. InGameSettingsUI 볼륨 패널 CanvasGroup+슬라이더3 생성/연결.
5. 기존 EffectPreset .asset의 SfxClip → SoundConfig로 수동 이전.

**주의**: SoundConfig는 이전 세션이 이미 작성 완료 상태였음(스펙 일치). AudioManager 외 코드는 본 세션에서 작성.

---

### AI 시나리오 ScriptableObject 종족별 재구조화 (2026-06-10) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-10/01_06_ai-scenario-scriptableobject-restructure/`

**핵심**: `DifficultyLevel`(enum) / `BuildOrderStep`(struct) / `AIActionType`(enum)을 Infrastructure → **Domain 레이어로 이동**.
- `Domain/AI/DifficultyLevel.cs`, `Domain/AI/BuildOrderStep.cs` (AIActionType 포함) 신규
- Domain은 UnityEngine 참조 금지 → BuildOrderStep에서 [Tooltip]/[Header] 제거, [Serializable](System)만 유지
- Infrastructure(`AIScenarioConfig.cs`/`LocalPlayerDifficulty.cs`/`AIConfig.cs`)는 중복 정의 삭제 후 `using Hexiege.Domain;`로 참조
- 참조 파일 전부(AIOpponentController/BattleViewModel/DifficultySelectView)에 `using Hexiege.Domain;` 확인. AIOpponentController는 DifficultyParams/GameRaceContext 때문에 `using Hexiege.Infrastructure;` 유지

**시나리오 에셋 구조 변경**: 종족당 단일 에셋 + 3시나리오 묶음.
- 레거시 `AIScenarioConfig_Human_A/B/C.asset` 폐기 → `AIScenarioConfig_{Human|Spirit|Transcendence}.asset` (각 `scenarios[0/1/2]` ScenarioBundle 배열)
- `GameBootstrapper.Setup.cs` `LoadScenarioBundleForRace()`: `GameRaceContext.RedRace` 기반 switch로 종족별 경로 결정 후 `Random.Range`로 1개 선택. (구 `LoadRandomHumanScenario` 제거됨)
- 타이밍: `GameRaceContext.Set`이 `InitializeAI`보다 먼저 실행되어 RedRace 확정 보장
- `AIScenarioConfig.cs`는 레거시 호환용 `scenarioName`/`_steps` 필드를 아직 보유(향후 제거 가능)

### 전체 유닛 사망 VFX 적용 (2026-06-08) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-08/19_08_unit-death-vfx/`

**배경**: EffectManager.PlayUnitDeath()는 UnitEffectConfig.GetDeath(type) → preset이 null이면 즉시 반환. Pistoleer(unitType=0)만 deathPreset이 연결되어 있고 나머지 23종은 null이었음. 코드 흐름(UnitView 싱글/서버 481줄, NetworkUnit.OnNetworkDespawn 클라이언트 183줄)은 이미 정상 구현 상태.

**해결**: 코드 변경 없음 — 에셋 작업만으로 완료.
- `EffectPreset_Unit_Death_Common.asset` 신규 생성 (vfx_unit_death.prefab + 사망 SFX, 볼륨 1.0)
- 1회성 에디터 스크립트 `SetUnitDeathVfxAll.cs` — 메뉴 `Hexiege/Setup/Set Unit Death VFX (All Units)`
  - GUID 기반 VFX 프리팹/SFX 클립 로드 → EffectPreset 생성 → UnitEffectConfig 전체 24종 deathPreset 일괄 연결
- 기존 `EffectPreset_Pistoleer_Death.asset` 삭제 (참조 없음 확인 후 제거)

**에디터 스크립트 패턴**:
- `ScriptableObject.CreateInstance<T>()` → `AssetDatabase.CreateAsset()` → `SerializedObject`로 private 필드 설정
- `AssetDatabase.GUIDToAssetPath(guid)` → `LoadAssetAtPath<T>()` 로 GUID 기반 에셋 로드 (파일 이동/이름변경에 안정적)

---

### 유닛 VFX 디테일 개선 (2026-06-08) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-08/14_44_vfx-scaling-mode-fix/`

**작업 1 — ScalingMode 수정**
- 유닛 VFX 프리팹 3개(`vfx_pistoleer_attack`, `vfx_tank_attack`, `vfx_unit_death`)의 모든 ParticleSystem이 `scalingMode: 1`(Local) 상태 — Unity 기본값이 Local이라 의도치 않게 방치된 것
- `scalingMode: 0`(Hierarchy)로 변경하는 1회성 에디터 스크립트 `VfxScalingModeFixer.cs` 작성 후 실행
- 이후 루트 Transform Scale로 이펙트 전체 크기 조절 가능해짐

**작업 2 — VfxSpawnPoint 스폰 위치/회전 수정**
- `UnitView.cs`: `[SerializeField] Transform _vfxSpawnPoint` 추가. `OnAttackHit()`에서 위치는 `_vfxSpawnPoint.position`, 회전은 `Quaternion.LookRotation(transform.forward)` 사용
- `EffectManager.cs`: `PlayUnitAttack(UnitType, Vector3, Quaternion)` 시그니처로 확장
- **핵심 교훈 — VfxSpawnPoint가 스켈레톤 본 하위에 있을 때**: `_vfxSpawnPoint.rotation`은 본 회전(약 0,-90,-90도)이 섞여 VFX가 엉뚱한 방향으로 발사됨. 위치(`position`)는 본 덕분에 정확하므로 그대로 사용하되, **회전은 반드시 `Quaternion.LookRotation(transform.forward)`로 대체**

**작업 3 — vfx_unit_death 퍼짐 효과 제거**
- 3개 ParticleSystem의 `startSpeed`를 모두 0으로 설정 (YAML 직접 수정)
- 루트 PS: scalar 0.2→0, Lingerer: 0.3→0, PuffBurst: scalar 2.6/minScalar 1.8→ 0/0

---

### 멀티플레이 유닛 사망 NGO Despawn 버그 수정 (2026-06-08) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-08/02_27_networkobject-invalid-destroy/`

**증상**: 멀티플레이에서 유닛 사망 시 클라이언트 화면에 GO가 사라지지 않고 사망 이펙트도 재생되지 않음.

**근본 원인**: 서버 `UnitView.OnUnitDied()`에서 `Destroy(gameObject)` 호출이 NGO Despawn 메시지를 클라이언트에 전파하지 않음.

**핵심 교훈**: NGO에서 클라이언트로 GO 파괴를 전파하려면 서버에서 반드시 `NetworkObject.Despawn(destroy: true)`를 명시 호출해야 한다. `Destroy(gameObject)` 방식은 NGO 전파 불보장.

**수정 내용**:
- `NetworkCombatController.cs` — `OnUnitDied()` 서버 핸들러: `EntityDiedClientRpc` 발행 후 `UnitFactory.GetUnitObject(unitId)`로 GO 조회 → `NetworkObject.Despawn(true)` 명시 호출
- `UnitView.cs` — `Unity.Netcode.NetworkObject` / `NetworkManager.Singleton` 직접 참조 완전 제거 (레이어 규칙 위반 수정). `NetworkContext.IsNetworkActive` / `IsNetworkServer` 홀더 패턴으로 교체
- `NetworkUnit.cs` — `OnNetworkDespawn()` 클라이언트: 이펙트 재생 로직 유지 (임시 진단 코드 제거)
- `EffectManager.cs` — 임시 진단 로그 코드(DiagLog/DeathLog) 완전 제거

**레이어 규칙 재확인**: `NetworkBehaviour` 및 Unity.Netcode 직접 참조 → Infrastructure 레이어 전용. Presentation(UnitView)에서 Unity.Netcode 참조는 절대 금지.

**런타임 로그 검증**: 13킬 전체에서 `OnNetworkDespawn` 게임플레이 중 발생(씬 언로드 시만 발생하던 이전과 대조) + 이펙트 재생 완료 확인.

---

### EffectManager VFX/SFX 통합 시스템 (2026-06-08) 🔵 코드 완료 / Inspector 작업 + 실기 테스트 예정

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-08/00_20_effect-manager-system/`

**신규 생성 파일 (전부 `Presentation/Effects/`, namespace Hexiege.Presentation)**:
- `EffectPreset.cs` — VFX 프리팹 + SFX 클립 + 볼륨 묶는 ScriptableObject. CreateAssetMenu "Hexiege/Effects/EffectPreset"
- `UnitEffectConfig.cs` — UnitType별 attack/death 프리셋. List<Entry> + Initialize()에서 Dictionary 캐싱 (UnitStatsConfig 패턴)
- `BuildingEffectConfig.cs` — BuildingType별 destroy/upgrade 프리셋. 동일 패턴
- `UiEffectConfig.cs` — UiEffectKey(enum 7종) → preset. enum은 이 파일 상단에 정의
- `VfxPoolItem.cs` — ParticleSystem.IsAlive(true) false 시 자동 Pool 반환. EffectManager가 Instantiate 시 없으면 AddComponent (프리팹 사전 준비 불필요)
- `EffectManager.cs` — static Instance (DontDestroyOnLoad 없음, Game씬 전용). VFX Pool(Dictionary<GameObject,Queue> 프리팹별, 무제한) + SFX Pool(공유 Queue<AudioSource>, 동시 8개 제한, spatialBlend=0 2D)
- `Assets/Editor/Setup/SetupEffectConfigs.cs` — 메뉴 3종(UnitEffectConfig/BuildingEffectConfig/UiEffectConfig 생성). SerializedObject로 private _entries 채움

**수정 파일**:
- `Presentation/Unit/UnitView.cs` — `OnAttackHit()`에 `EffectManager.Instance?.PlayUnitAttack` + OnUnitDied 구독 블록 Destroy 직전 `PlayUnitDeath` 추가
- `Bootstrap/GameBootstrapper.cs` — `_effectManager/_unitEffectConfig/_buildingEffectConfig/_uiEffectConfig` SerializeField 추가 (Floating HP Text 헤더 아래)
- `Bootstrap/GameBootstrapper.Map.cs` — FloatingHpTextSpawner Initialize 직후 `_effectManager?.Initialize(...)` 호출
- `Presentation/Unit/UnitEffectView.cs` — DEPRECATED 주석 + 전체 /* */ 비활성화 (프리팹 컴포넌트 제거 + 파일 삭제는 실기 PASS 후)

**핵심 설계 결정**:
- **공격 VFX는 반드시 OnAttackHit() Animation Event 기반** — `GameEvents.OnEntityAttacked`는 서버 전용이라 멀티 클라이언트 미도달. AnimationEventRelay.OnAttackHit() → UnitView.OnAttackHit()는 모든 클라이언트 로컬 실행이라 멀티에서도 정상
- **static Instance 채택 이유**: 프리팹 Animation Event 호출이라 DI 불가 → `EffectManager.Instance?.` 직접 접근. SingletonMonoBehaviour는 DontDestroyOnLoad 포함이라 Game씬 전용에 부적합
- **enum SerializedProperty 주의**: UnitType/BuildingType은 값이 비연속(0,1..7,10..)이므로 `enumValueIndex`에 정수값 직접 대입 금지 → enumNames 순회하며 intValue 일치 인덱스 탐색 (SetupEffectConfigs.EnumIndexOf 헬퍼)
- **VFX Pool은 프리팹별 lazy 생성** — init 시점에 어떤 프리팹 쓸지 모름. `_initialPoolSizePerType`은 향후 워밍업용 (현재 미사용, SerializeField라 CS0414 경고 없음)
- **SFX 8개 제한**: `_activeSfxCount < _maxConcurrentSfx` 검사, 초과 시 무시. Coroutine으로 clip.length+0.1초 후 반환

**Inspector 작업 (사용자 수행 필요)**:
1. 메뉴 `Hexiege/Setup/UnitEffectConfig 생성` / `BuildingEffectConfig 생성` / `UiEffectConfig 생성` 3종 실행 → Resources/Config/*.asset 생성
2. Game.unity에 EffectManager GameObject 배치 + VFX_Container / SFX_Container 빈 자식 생성 후 연결
3. GameBootstrapper Inspector에 EffectManager + Config 3종 연결
4. EffectPreset 에셋 만들어 Config에 연결 (VFX 프리팹 / SFX 클립)

---

### 싱글플레이 AI 시스템 Phase 1~5 + UI (2026-06-07) 🔵 코드 완료 / AI 시나리오 작업 후 실기 테스트 예정

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-07/16_40_ai-system-implementation/`

**신규 생성 파일**:
- `Infrastructure/LocalPlayerDifficulty.cs` — DifficultyLevel enum(Easy/Normal/Hard) + 정적 홀더 패턴 (LocalPlayerRace와 동일)
- `Infrastructure/Config/AIConfig.cs` — DifficultyParams 중첩 구조체 × Easy/Normal/Hard ScriptableObject + `public bool enableAI = true` On/Off 필드
- `Infrastructure/Config/AIScenarioConfig.cs` — BuildOrderStep(ActionType enum + BuildingType/UnitType/targetBuildingLine/delay 3종) 플랫 리스트 ScriptableObject
- `Application/Services/AIOpponentController.cs` — Tick() 기반 AI 핵심. 빌드오더 스크립트(Phase 1~4), 반응 시스템(R1 유닛열세/R2 골드과잉/R3 채굴소 파괴), BFS 건물 배치, MiningPost 병행 트랙
- `Presentation/UI/Views/Lobby/Battle/DifficultySelectView.cs` — BattleScreen.SingleplayDifficulty 상태 시 표시. 쉬움/보통/어려움 버튼 → vm.CmdSelectDifficulty
- `Assets/Editor/AIConfigSetup.cs` — `Hexiege/Setup/AIConfig 생성` + `AIScenarioConfig_Human_A/B/C 생성` 메뉴
- `Assets/Editor/FixDifficultySelectViewLayout.cs` — `Hexiege/Fix/DifficultySelectView 레이아웃 수정` 메뉴. 기존 씬의 ButtonArea 제거 + VLG를 DifficultySelectView 루트에 이전 (스프라이트·색상 보존)

**수정 파일**:
- `Application/Events/GameEvents.cs` — `UnitProducedEvent`에 `BarracksId` 필드 추가 (AI 콜백 기반 연속 생산용)
- `Application/UseCases/ResourceUseCase.cs` — `SetIncomeMultiplier(TeamId, float)` + `_incomeMultipliers Dictionary` 추가. TickTeamIncome()에 배율 적용
- `Bootstrap/GameBootstrapper.cs` — `_enableAI` SerializeField 주석 처리(AIConfig.enableAI로 이전, 테스트 통과 후 삭제 예정). `_aiController?.Tick(Time.deltaTime)` in Update() 유지
- `Bootstrap/GameBootstrapper.Setup.cs` — `InitializeAI()`: AIConfig 로드 → `if (!aiConfig.enableAI) return;` 조기 반환 → 난이도 파라미터 선택 → Random.Range(0,3)으로 시나리오 A/B/C 선택 → SetIncomeMultiplier 호출 → AIOpponentController 생성
- `Bootstrap/GameBootstrapper.Map.cs` — `SetupProduction()` 직후 `if (!NetworkContext.IsNetworkActive) InitializeAI();` (enableAI 체크는 InitializeAI() 내부에서 수행)
- `Presentation/UI/ViewModels/BattleViewModel.cs` — `BattleScreen.SingleplayDifficulty` 추가, `CmdSelectDifficulty Subject<DifficultyLevel>` 추가, `CmdStartSingleplay` → 난이도 화면 전환으로 변경, `NavigateBack()` 케이스 추가
- `Presentation/UI/Views/Lobby/Battle/BattleRootView.cs` — `_difficultySelectView SerializeField` 추가, `Bind/Unbind` 포함
- `Assets/Editor/SetupDifficultySelectView.cs` — VLG childForceExpandHeight=false, LayoutElement preferredHeight=100 적용 (개선)

**AI On/Off 설정 위치**:
- `Resources/Config/AIConfig.asset` Inspector → `Enable AI` 체크박스 (Project 창에서 직접 접근 가능)
- `GameBootstrapper._enableAI` SerializeField는 주석 처리됨 (Game.unity 씬 접근 불필요)

**AI 설계 핵심 패턴**:
- **콜백 기반 연속 생산**: `StartProduction` 실행 시 `_lineProduction[barracksId]=unitType` 기록 + 시드 1회 `EnqueueUnit` → `GameEvents.OnUnitProduced` 구독에서 Red팀 유닛 생산 시 해당 배럭에 `EnqueueUnit` 재호출 (자동생산 미사용, 규칙 23)
- **BFS 배치**: Red 성채 BFS → walkable + Red 소유 + 기존 생산 건물 인접 6타일 제외 → 최근접 타일
- **MiningPost 병행 트랙**: Phase 2/4 진입 시 활성화 → `_mineTiles`(HasGoldMine 기반) 중 미점령 타겟 → 모든 배럭 SetRallyPoint → mineCheckInterval 주기 PlaceMiningPost → 성공 시 ClearRallyPoint + 트랙 종료

**DifficultySelectView UI 구조 (Lobby.unity)**:
- BattlePanel 상단 절반(anchorMin.y=0.5~1.0) 배치 — 다른 서브뷰(BattleMainPanel 등)와 동일
- VLG: Padding Top/Bottom=60, Spacing=20, childForceExpandHeight=false (BattleMainPanel 동일)
- 버튼 4개: LayoutElement preferredHeight=100, CanvasGroup 패턴(Rule 5)

**Inspector 작업 (사용자 수행 필요)**:
1. `Hexiege/Setup/AIConfig 생성` 메뉴 실행 → `Resources/Config/AIConfig.asset` 생성
2. `Hexiege/Setup/AIScenarioConfig_Human_A/B/C 생성` 메뉴 실행
3. (Lobby.unity 열린 상태) `Hexiege/Fix/DifficultySelectView 레이아웃 수정` 메뉴 실행 — ButtonArea 제거, 기존 스프라이트·색상 보존

---

### NetworkGameManager 고아 필드 + Game씬 NGM 제거 (2026-06-06) ✅ 완료 (싱글+멀티 실기 PASS)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-06/00_08_networkgamemanager-cleanup/`

**수정 파일**:

- `Bootstrap/GameBootstrapper.cs` — `_networkGameManager` SerializeField 3줄 제거 (고아 필드)
- `Assets/Editor/RemoveGameSceneNGM.cs` — Game씬 NGM 제거 1회성 Editor 스크립트 (실행 완료)

**근본 원인**: NetworkGameManager는 Lobby에서 생성 후 DontDestroyOnLoad로 유지되는 구조인데 Game.unity에도 별도 NGM 배치 → 씬 전환 시 인스턴스 중복. GameBootstrapper의 `_networkGameManager` 필드는 코드 어디에서도 미사용 고아 상태.

**씬 NGM 제거 Editor 스크립트 패턴**:

- `FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)`
- `go.scene.name == "Game"`으로 Lobby NGM과 Game NGM 구분 (씬 소속 필터링)
- Additive 임시 로드 → `Undo.DestroyObjectImmediate` → `EditorSceneManager.SaveScene` → CloseScene

**교훈**: DontDestroyOnLoad 오브젝트는 생성 씬(Lobby) 하나에만 배치. Game씬 중복 배치 금지. GameBootstrapper SerializeField는 실제 사용하는 필드만 유지.

---

### 신규 유닛 프리팹 컴포넌트 자동 부착 에디터 스크립트 (2026-06-05) ✅ 스크립트 작성 완료 (실기 테스트 예정)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-05/22_14_unit-prefab-component-setup/`

**생성 파일**: `Assets/Editor/Setup/SetupNewUnitPrefabs.cs`  
**메뉴**: `Hexiege/Setup/신규 유닛 컴포넌트 부착`

**배경**: 32개 신규 유닛 프리팹에 아트 완성 후 컴포넌트 부착이 필요. Root에 UnitView/NetworkObject/NetworkTransform/NetworkUnit, _Mesh 자식에 AnimationEventRelay.

**구현 패턴**:

- `PrefabUtility.LoadPrefabContents` → 처리 → `SaveAsPrefabAsset` → `UnloadPrefabContents`
- UnitView/NetworkTransform Inspector 값은 `SerializedObject.FindProperty` → `ApplyModifiedPropertiesWithoutUndo()`
- `GetComponent == null` 후 AddComponent (멱등성 보장)
- `_Mesh` 키워드로 직속 자식 탐색 → AnimationEventRelay 부착

**주의**: Animation Event(OnAttackHit 타이밍) / UnitFactory 등록 / UnitStatsConfig 추가는 별도 작업.

---

### BuildingView Missing Script 정리 (2026-06-05) ✅ 완료 (실기 PASS)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-05/22_42_missing-buildingview-script/`

**수정 파일**: `Assets/Editor/RemoveMissingScripts.cs` (1회성 — 실행 후 삭제)

**원인**: `BuildingView` 스크립트(GUID: `c178b6f3e086351409b946635cbfae71`)가 건물 철거 시스템 구현 시 의도적으로 삭제됐으나, Spirit/Transcendence 계열 프리팹 8개에 Missing 참조가 잔존.

**영향 프리팹 (8개)**: Spirit(ManaRift, SpiritNexus) × Blue/Red, Transcendence(ElderTree, FungalNode) × Blue/Red

**해결**: Editor 스크립트 → 메뉴 `Hexiege/Setup/Missing Script 제거` → `PrefabUtility.LoadPrefabContents` + `GameObjectUtility.RemoveMonoBehavioursWithMissingScript`.

**교훈**: 스크립트 삭제 시 해당 스크립트가 붙어있던 모든 프리팹의 Missing Script 참조를 함께 정리해야 함.

---

### 자동생산 재등록 슬롯 버그 구조 개선 (2026-06-05) ✅ 완료 (정적분석+실기 PASS)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-05/11_31_auto-unregister-currentisauto-fix/`

**수정 파일**:
- `Domain/Building/ProductionState.cs` — `CurrentIsAuto` 파생 계산 getter
- `Application/UseCases/UnitProductionUseCase.cs` — reset 2곳 제거, `RegisterAutoType` 조건 1행 추가
- `Docs/GameSystemRules/GameSystemRules_UI.md` — 규칙 20 보완

**근본 원인**: `CurrentIsAuto`가 수동 관리 필드 → 자동 해제 시 reset 누락 → `TryConvertCurrentToAuto` 잘못 거부 → 슬롯 중복/누락

**구조 개선 패턴**: `IsAutoMode`와 동일. backing field(`_currentIsAutoFlag`) + 파생 getter:
```csharp
get => _currentIsAutoFlag && CurrentProducing.HasValue && AutoTypes.Contains(CurrentProducing.Value);
```

**`PendingQueue.Count == 0` 조건**: `TryConvertCurrentToAuto` 적용을 큐가 비어있을 때만 허용. 큐에 다른 항목이 있으면 슬롯3에 추가 (중복이 아닌 순환 큐). GameSystemRules 규칙 20에 명시됨.

**setter 호환**: 기존 `state.CurrentIsAuto = true/false` 코드 전체 유지 (`_currentIsAutoFlag` 갱신만 함).

---

### 자동생산 완료 사이클 슬롯2 깜빡임 수정 (2026-06-05) ✅ 완료 (실기 PASS)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-05/10_59_auto-production-cycle-flicker/`

**수정 파일**: `Application/UseCases/UnitProductionUseCase.cs` — `CompleteProduction` 1곳

**버그**: 자동생산 완료 시 재순환 항목이 슬롯2에 1프레임 표시되었다가 사라지는 깜빡임.
**근본 원인**: `CompleteProduction`이 `ChargeVisibleSlots` + `OnProductionQueueChanged` 발행 후 `TryStartNext`를 다음 프레임에 위임하는 1프레임 갭.

**수정**: `ChargeVisibleSlots` 제거 + `OnProductionQueueChanged` 직접 발행 제거 → `OnUnitProduced` 후 즉시 `TryStartNext(state)` 호출. fallback: `!CurrentProducing.HasValue`이면 이벤트 수동 발행.

**패턴**: AddNewAutoSlot 2026-04-19 수정과 동일. CompleteProduction 경로가 미처 처리되지 않았던 것.

---

### 방어 타워(AutoTower) 공격 기능 구현 (2026-06-01) ✅ 완료 (실기 PASS)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-06-01/05_01_defense-tower-implementation/`

**신규 파일**: `Application/UseCases/TowerCombatUseCase.cs`
- `Tick(float dt)`: AutoTower 순회 → 쿨다운 감소 → 0 이하 시 적 탐색 → 데미지 적용
- 타겟 선택: `Vector3.Distance` 기준 가장 가까운 적 유닛 (건물 제외)
- 멀티플레이 가드: `NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer`이면 Tick 조기 반환

**팀→종족 변환 패턴**: Domain에서 GameRaceContext(Infrastructure) 직접 참조 불가 → `Func<TeamId, RaceId>` 델리게이트를 GameBootstrapper에서 주입

---

### Human CannonTower 초기 방향 설정 (2026-06-02) ✅ 완료 (실기 PASS)

**수정 파일**: `Infrastructure/Factories/BuildingFactory.cs`

**`GetInitialRotation(RaceId race, BuildingType type, TeamId team)`**:
- Human + AutoTower 조합일 때만 분기
- `ViewConverter.IsFlipped`로 로컬 플레이어 팀 판별 (`IsFlipped ? TeamId.Red : TeamId.Blue`)
- 내 포탑: `Quaternion.identity` / 상대 포탑: `Quaternion.Euler(0f, 180f, 0f)`

**핵심 원칙**: 팀 색깔(Blue/Red) 기준이 아닌 "내 진영 vs 상대 진영" 기준으로 회전 결정. ViewConverter가 위치만 반전하고 회전은 변환하지 않으므로 상대 포탑에 180도 적용.

---

### UnitStatsConfig 미사용 필드 제거 (2026-06-02) ✅ 완료

- `AttackKind` enum, `StatValues.Kind`, `GetAttackKind()` 제거 (2026-05-11 비활성화된 미사용 코드)
- `attackKind`, `occupancySize` 필드 제거 (`occupancySize`: TileOccupancyManager 클래스 자체가 없음)
- 미사용 필드 확인 시 주석 언급만 믿지 말고 코드베이스 전체 Grep 필수

---

### Lobby UI 규칙 준수 수정 — 에디터 스크립트 4종 (2026-05-30) ✅ 완료 (실기 PASS)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-30/12_24_lobby-ui-rule-compliance/`

**에디터 스크립트**: `Assets/Editor/FixLobbyUiGroupA~D.cs` — 메뉴 `Hexiege/Fix/LobbyUI/GroupA~D`

**수정 내용**: Lobby.unity 씬의 25건 UI 규칙 위반(GameSystemRules.md 규칙 1/2/5) 일괄 수정.
- GroupA: Toast Canvas CanvasScaler 추가(규칙 1), Background/Message 앵커 비율화(규칙 2), CanvasGroup 초기값 정렬(규칙 5)
- GroupB: LoadingScreen Spinner/StatusText, CodeInput Text Area 앵커 비율화
- GroupC: TabBar(anchorMax.y=0.073) / ContentArea(anchorMin.y=0.073) 앵커 비율화
- GroupD: 5개 VLG 패널(BattleMainPanel/CustomGamePanel/CustomHostPanel/CustomJoinPanel/RandomMatchPanel) 내 16개 자식 처리

**VLG 자식 고정 픽셀 → 앵커 기반 전환 패턴 (중요)**:
- VLG: `childControlHeight = true`, `childForceExpandHeight = false`
- 각 자식 LayoutElement: `preferredHeight = 원래_SizeDelta.y`, `flexibleHeight = 0`, 나머지 -1
- 자식 sizeDelta = (0, 0)
- ⚠️ `childForceExpandHeight = true`로 설정하면 VLG 패널 전체 높이를 자식들이 채워버려 버튼이 비정상적으로 커짐
- ⚠️ `flexibleHeight > 0`이면 preferredHeight 이후 남는 공간을 추가 분배받아 크기 변동 발생

**앵커 계산값 검증 필수**: Plan.md에 명시된 앵커 계산값과 에디터 스크립트 코드값을 반드시 일치시킬 것. 이번 작업에서 GroupA Toast Background y값 불일치(0.04 vs 0.5) 발견 → 실기 시 Toast 위치가 중앙이 아닌 최하단으로 배치되는 버그 발생.

---

### 건물 업그레이드 생산 상태 처리 오류 수정 (2026-05-31) ✅ 완료 (실기 PASS)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-31/21_03_upgrade-production-state-fix/`

**수정 파일**: `Presentation/Production/ProductionTicker.cs` — `OnBuildingUpgraded()` 1곳만

**버그 1 (골드 환불 누락)**:
- 원인: `UnregisterBarracks(oldId)`는 `_states.Remove()` 1줄만 수행 — 환불 없음
- 수정: `CancelAllQueue(oldId)`로 교체. 내부에서 ① CurrentProducing 환불 ② PendingQueue IsCharged=true 항목 환불 ③ UnregisterBarracks 포함
- 근거: GameSystemRules.md — 건물 철거 시스템 규칙 5

**버그 2 (랠리포인트 초기화)**:
- 원인: `RegisterBarracks(newBuilding)`이 새 빈 `ProductionState` 생성 → 기존 `RallyPoint(HexCoord?)` 유실
- 수정: `CancelAllQueue` 호출 **전에** `GetState(oldId)?.RallyPoint` 저장 → `RegisterBarracks` 후 `SetRallyPoint(newId, saved)` 복원

**수정 순서 (순서 바꾸면 버그 재발)**:
1. `savedRallyPoint = GetState(oldId)?.RallyPoint` — CancelAllQueue가 ClearRallyPoint를 호출하기 전에 저장
2. `CancelAllQueue(oldId)` — 환불 + 제거(UnregisterBarracks 내장)
3. `RegisterBarracks(newBuilding)` — 새 상태
4. `if (savedRallyPoint.HasValue) SetRallyPoint(newId, savedRallyPoint.Value)` — 복원

---

### Production 잠금 유닛 Lock Icon + 초상화 디밍 (2026-05-31) ✅ 완료 (실기 PASS)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-31/19_48_production-lock-icon/`

**목적**: ProductionPopup에서 업그레이드 단계 미달로 잠긴 유닛 버튼에 (1)초상화 디밍 (2)자물쇠 아이콘 배지를 표시.

**슬롯 구조 (중요)**: ProductionPopup 유닛 버튼 슬롯 3개 = 1/2/3단계 유닛.
- 슬롯0(index 0): 1단계 유닛 — 항상 해금 → LockIndicator 불필요
- 슬롯1(index 1): 2단계 유닛 — 잠길 수 있음 → LockIndicator 필요
- 슬롯2(index 2): 3단계 유닛 — 잠길 수 있음 → LockIndicator 필요
- 따라서 `_unitLockIndicators`는 슬롯1/슬롯2만 대응 (2개). 인덱스 매핑: `_unitLockIndicators[0]`→슬롯1, `[1]`→슬롯2.

**코드 변경** (`Presentation/UI/ProductionPanelUI.cs` — `UpdateLockIndicators()`):
- 인덱스 매핑 보정: 루프 안에서 `int slotIndex = i + 1` 추가 → `_activeUnitLocks`/`_activeUnitTypes`/`_unitButtonPortraits` 접근 시 모두 `slotIndex` 사용
- 잠금 시 `_unitButtonPortraits[slotIndex].color = new Color(0.35f,0.35f,0.35f,1f)`, 해금 시 `Color.white`
- 안전 가드: `_unitButtonPortraits != null && slotIndex < Count && [slotIndex] != null`
- 충돌 없음 확인: `UpdateButtonPortraits()`는 `.sprite`만 변경, `.color`는 아무도 건드리지 않음
- 2유닛 배치(`twoUnitLayout`, list.Count==2: [유닛1][빈][유닛2])에서도 slotIndex=2가 두 번째 실유닛을 가리켜 올바르게 동작

**에디터 스크립트** (`Assets/Editor/AddLockIcons.cs`) — 전면 재작성:
- 메뉴: `Hexiege/Setup/잠금 아이콘 추가`
- 구방식 문제: `_unitLockIndicators`가 비어 있으면 아무것도 못 함. 신방식: `_unitButtons`에서 Slot GO를 찾아 LockIndicator를 직접 생성 후 `_unitLockIndicators`에 연결
- `Object.FindObjectOfType<ProductionPanelUI>(true)` → `SerializedObject`로 `_unitButtons`(List<Button>) 읽기
- 대상 슬롯 인덱스 = {1, 2}만 (슬롯0 스킵). `buttonsProp.GetArrayElementAtIndex(slot).objectReferenceValue as Button` → `button.gameObject`(= Slot GO)
- Slot GO 하위에 "LockIndicator" GO 생성 (이미 있으면 재사용). 생성 GO 컴포넌트: RectTransform + LayoutElement + Image
- **LayoutElement.ignoreLayout = true 필수**: Slot GO에 HorizontalLayoutGroup이 있어 무시 안 하면 자물쇠가 다른 자식과 가로로 나란히 배치됨 (BorderOverlay GO와 동일 패턴)
- RectTransform: anchorMin(0.6,0)~anchorMax(1,0.4), pivot(1,0), anchoredPosition=0, sizeDelta=0 → 우측 하단 40% 비율 배치 (Rule 2 준수)
- Image: 스프라이트 `Assets/_Project/Sprites/UI/Icons/ui_icon_lock.png`, raycastTarget=false, preserveAspect=true, color=white
- 초기 상태 `SetActive(false)` — 런타임 코드가 잠금 시 켜줌
- 생성/재사용 GO를 슬롯 순서(슬롯1→슬롯2)대로 `_unitLockIndicators`에 연결: `indicatorsProp.arraySize = N` 후 `GetArrayElementAtIndex(i).objectReferenceValue = go`
- Undo: `RegisterCompleteObjectUndo(panel)`(직렬화 필드용) + `RegisterCreatedObjectUndo`/`SetTransformParent`(GO 생성용) + `so.ApplyModifiedProperties` + MarkSceneDirty + SaveScene

**패턴 메모**: 에디터 스크립트에서 다른 컴포넌트의 private `[SerializeField]` 리스트에 접근할 때는 `new SerializedObject(comp).FindProperty(name)` → `GetArrayElementAtIndex(i).objectReferenceValue` 사용 (리플렉션보다 안전). 리스트에 **쓰기**도 가능: `arraySize` 설정 후 각 요소에 `objectReferenceValue` 할당 → `ApplyModifiedProperties()`.

---

### 패널 버튼 크기 불일치 수정 (2026-05-31) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-31/13_18_panel-button-size-inconsistency/`

**문제**: BuildingPopup / BuildingActionPanel / ProductionPopup 세 패널에서 Row별 버튼 높이와 Row 내 Slot 너비가 불균등하게 표시됨.

**근본 원인**: 슬롯 내부 아이콘 Image의 스프라이트 native size가 VLG의 preferredHeight 배분 단계(Phase 2)에서 Row별 불균등을 발생. `childForceExpandHeight=True`는 Phase 3(flexible)에서만 작동하여 Phase 2의 불균등을 해소하지 못함.

**해결**: `Assets/Editor/FixPanelRowLayout.cs` Editor 스크립트 신규 작성
- 메뉴: `Hexiege/Fix Panel Row Layout`
- 3개 패널 × 3개 Row = 9개 Row에 `LayoutElement(preferredHeight=0, flexibleHeight=1)` 추가
- 3개 패널 × 9개 Slot = 27개 Slot에 `LayoutElement(preferredWidth=0, flexibleWidth=1)` 추가
- ProductionPopup Row1 Rallypoint HLG 패딩 L125 R125 → L60 R60 (IgnoreLayout 아이콘이라 시각 변화 없음)
- ProductionPopup Row1 슬롯(Rallypoint/Slot5/Destroy) LayoutElement minWidth=0 추가 (CostContainer 부재로 인한 natural min 차이 해소)
- `Undo.RecordObject` + `EditorUtility.SetDirty` + `EditorSceneManager.SaveScene` 적용

**GameSystemRules 준수**: Rule 2 (앵커 기반 배치, 균등 분배 원칙) — LayoutElement는 Rule 2가 의도하는 균등 분배가 스프라이트 크기에 방해받지 않도록 보완하는 것으로 위반 없음.

**수정 결과**: Row0/Row1/Row2 모두 218.45px, 모든 Slot 283.73px (런타임 로그 확인)

---

### 유닛 초상화 자동 할당 에디터 스크립트 (2026-05-31) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-31/09_32_production-panel-portrait-auto-assign/`

**문제**: `ProductionPanelUI` 유닛 버튼에 런타임에서 초상화 이미지가 표시되지 않는 버그.

**근본 원인 — 데이터 소스 불일치**:
- 씬뷰 미리보기: `_unitButtonPortraits[i]` (Image 컴포넌트 Source Image 슬롯에 직접 할당)
- 런타임 소스: `_buildingUnitMappings[x].blueUnits[i].portrait` Sprite 필드
- `UpdateButtonPortraits()`가 패널 열릴 때 `_unitButtonPortraits[i].sprite = list[i].portrait` 로 덮어씀
- `_buildingUnitMappings`의 `portrait` 슬롯이 null → 이미지 사라짐

**해결**: `Assets/Editor/AssignUnitPortraits.cs` 에디터 유틸리티 신규 생성
- 메뉴: `Hexiege/Setup/유닛 초상화 자동 할당`
- `_buildingUnitMappings`의 모든 `blueUnits`/`redUnits` 배열 순회
- `UnitType.ToString().ToLower()` → `{name}_portrait_{blue|red}` 파일명 패턴 생성
- `AssetDatabase.FindAssets` → Sprite 로드 → `portrait` 슬롯 할당
- 기존 할당값 유지 (null 슬롯만 채움)
- 실행 결과: 142개 portrait 전부 할당 완료, null 0개

**스프라이트 파일명 규칙 (확인 완료)**:
- 패턴: `{UnitType.ToString().ToLower()}_portrait_{blue|red}.png`
- 경로: `Assets/_Project/Sprites/Units/{Human|Spirit|Transcendence}/{UnitName}/`
- `UnitType.ToString().ToLower()` 변환 결과가 폴더명/파일명 prefix와 정확히 일치

---

### BuildingActionPanelUI 씬 계층 재설계 + 런타임 슬롯 제어 (2026-05-29) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-29/building-action-panel-rebuild/`

**핵심 변경**:
- `BuildingActionPanelUI.cs`: `_allSlotButtons`(9개 전체) + `_activeSlotButtons`(활성만) 필드 추가. `BuildSlotCanvasGroups()`에서 CanvasGroup 캐시·초기값 alpha=0. `OnShow()` 오버라이드에서 전체 숨김 후 활성 버튼만 alpha=1 — **BuildingPlacementUI._buttonCanvasGroups 패턴과 동일**.
- Game.unity: BuildingActionPanel 3x3 VLG+HLG 그리드 구조. 래퍼 anchoredPosition/sizeDelta 오프셋 제거. CancelButton anchor=(0.883,0.852)~(0.993,0.97) (BuildingPlacementUI와 통일). HeaderText 순수 앵커: (0.096,0.826)~(0.867,1.006).
- 에디터 스크립트 2개 사용 후 삭제: `RebuildBuildingActionPanel.cs`, `FixHeaderTextAnchor.cs`

**설계 포인트**:
- 빈 슬롯 숨김은 에디터 고정이 아닌 런타임 `OnShow()`에서 처리 → 나중에 건물 타입별 다른 버튼 표시 확장 가능
- `BuildingPanelBase.OnShow()` 훅을 오버라이드하는 구조이므로 베이스 Show() 흐름(헤더갱신·애니메이션·환불텍스트)은 변경 없음

---

### BuildingPlacementUI 씬 계층 재설계 (2026-05-29) ✅ 에디터 구성 완료 / 실기 재검증 필요

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-29/building-placement-ui-rebuild/`

**문제**: 기존 BuildingPlacementUI가 패널 높이 부족, 버튼 테두리 침범, 골드 아이콘 Y축 정렬 불일치로 FAIL. 수치 조정만으로는 해결 불가 → 씬 계층 전면 재구성.

**핵심 변경**:
- `Game.unity`: BuildingPanel anchor=(0,0)~(1,0.4). GridContainer anchor=(0.08,0.123)~(0.92,0.864). CancelButton anchor=(0.883,0.852)~(0.993,0.97). 모두 순수 앵커 기반(anchoredPosition=0, sizeDelta=0).
- 버튼 그리드: GridLayoutGroup 대신 VerticalLayoutGroup→Row별 HorizontalLayoutGroup 중첩 (GameSystemRules Rule 2 supplement). 3행×3열 = 9슬롯.
- 각 버튼 내부: HLG(childControlWidth=true) → IconImage(flexibleWidth=6) + CostContainer(flexibleWidth=4, VLG) → GoldIcon(ui_icon_gold, min/preferred=44) + CostText(Maplestory Light SDF).
- `BuildingPlacementUI.cs`: `_buildingGoldIcons` 필드 추가.
- `GameSystemRules.md`: Rule 2에 Layout Group 반응형 패턴 추가.
- 구 `BuildingButtons` 컨테이너 씬에서 제거.

**1회성 셋업 스크립트**: `Assets/Editor/RebuildBuildingPlacementUI.cs`
- 메뉴: `Hexiege/Setup/BuildingPlacementUI 재구성`
- anchoredPosition/sizeDelta를 0으로 맞추려면 앵커 변경 후 `rt.anchoredPosition = Vector2.zero` 명시 필수 (offsetMin/offsetMax만 설정하면 anchoredPosition 오프셋이 남음)
- 버튼 내부 재구성 시 기존 자식 전체 먼저 삭제 필수 (구 Slot 등 잔여 오브젝트가 LayoutElement와 충돌)
- GoldIcon에 AspectRatioFitter가 있으면 반드시 제거 (preferredHeight 무시 원인)

**GameSystemRules 준수 검증 완료** (2026-05-29): Rule 2/4/5/6 모두 준수 확인.

---

### 멀티플레이 포기 시 호스트 GameEndUI 미표시 버그 수정 (2026-05-27) ✅ 실기 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-27/16_52_game-end-ui-bugfix/`

**버그**: 멀티플레이에서 포기(Forfeit) 시 호스트 측 게임 종료 UI가 표시되지 않는 버그. 클라이언트 측은 정상 표시.

**근본 원인**: `AnnounceWinnerClientRpc` 내부의 `!IsServer` 가드는 정상 종료(Castle 파괴) 흐름에서 서버가 이미 `GameEndUseCase`를 통해 `OnGameEnd`를 발행했으므로 중복 발행을 방지하는 코드다. 그러나 포기 흐름(`ForfeitServerRpc`)은 `GameEndUseCase`를 전혀 거치지 않고 `AnnounceWinnerClientRpc`를 직접 호출한다. 결과적으로 서버(호스트)에서 `OnGameEnd`가 한 번도 발행되지 않아 `GameEndUI`가 표시되지 않음.

**수정** (`Infrastructure/Network/NetworkGameEndController.cs` — `ForfeitServerRpc()`):
- `_announced = true` 설정 후, `AnnounceWinnerClientRpc` 호출 **직전**에 `GameEvents.OnGameEnd.OnNext(new GameEndEvent(winnerTeam))` 1줄 추가.
- 안전성: `_announced=true` 상태이므로 이 발행을 `OnGameEndServer`가 받아도 153행 가드(`if (_announced) return`)에 의해 즉시 return → 중복 처리 없음.

**흐름 비교**:
- 정상 종료: `GameEndUseCase → OnGameEnd(서버) → OnGameEndServer → AnnounceWinnerClientRpc` → 클라이언트만 OnGameEnd 재발행
- 포기(수정 전): `ForfeitServerRpc → AnnounceWinnerClientRpc` → 호스트 OnGameEnd 없음 (버그)
- 포기(수정 후): `ForfeitServerRpc → OnGameEnd(서버 직접) → AnnounceWinnerClientRpc` → 클라이언트도 OnGameEnd 발행

**Canvas Hierarchy BUG-002 동시 수정** (코드 변경 아님):
- 문제: `RematchRequestPopup`이 `SafeAreaContainer`보다 앞 인덱스 → `GameEndPanel`에 가려짐
- 수정: Inspector에서 `SafeAreaContainer`와 `RematchRequestPopup` 순서를 교환 → RematchRequestPopup이 SafeAreaContainer 위에 렌더링
- `AnimatedPanel.Show()`에 `SetAsLastSibling()` 없음 확인 → Inspector 순서가 영구 적용됨

---

### 로비 배경 Safe Area 수정 — FixLobbyBackground.cs 에디터 스크립트 (2026-05-26) ✅ 실기 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-26/safe-area-lobby-bg/`

**문제**: 노치/홈바 기기에서 로비 배경이 Safe Area 경계에서 잘리는 현상. 원인: `LobbyRoot`의 Image 컴포넌트(남색 배경, r:0.059 g:0.059 b:0.102 a:1)가 `SafeAreaContainer` 안에 위치하여 Safe Area 크기만큼만 그려짐 (GameSystemRules.md Rule 4 위반).

**수정 구조** (Rule 4 준수):
```
Canvas
├── LobbyBackground  ← 신규 생성. Image(전체화면 stretch). SafeAreaContainer보다 앞 배치. raycastTarget=false
└── SafeAreaContainer  ← SafeAreaFitter 기존 그대로
    └── LobbyRoot  ← Image 컴포넌트 enabled=false로 비활성화
```

**에디터 스크립트**: `Assets/Editor/FixLobbyBackground.cs`
- 메뉴: `Hexiege/Setup/로비 배경 Safe Area 수정`
- `LobbyRootView` 컴포넌트로 계층 역추적 (LobbyRoot → SafeAreaContainer → Canvas)
- `Undo.RegisterCreatedObjectUndo`, `Undo.SetTransformParent`, `Undo.AddComponent<Image>`, `Undo.RecordObject` 패턴으로 Ctrl+Z 지원
- 실행 후 Ctrl+Z로 실수로 되돌리지 않도록 주의 — Undo 스택에 등록된 변경이 취소될 수 있음

**Safe Area 전체화면 배경 설계 원칙**:
- 전체화면 배경은 반드시 `SafeAreaContainer` 밖(`Canvas` 직속)에 배치 (Rule 4)
- RectTransform: anchorMin=(0,0) / anchorMax=(1,1) / offsetMin=offsetMax=Vector2.zero
- `raycastTarget=false` 필수 — 배경이 터치 이벤트 차단하지 않도록
- Hierarchy 순서: 배경 오브젝트가 `SafeAreaContainer`보다 위(=먼저 그려짐=뒤에 표시됨)에 위치해야 함

---

### 로비 SetActive→CanvasGroup 전환 (2026-05-25) ✅ 실기 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-25/lobby-canvasgroup-refactor/`

**수정 대상**: 로비 7개 뷰 (LobbyRootView, MainLobbyView, BattleMainView, BattleRootView, ProfileView, RankingView, ShopView 포함)

**변경 패턴**:
- `gameObject.SetActive(false)` → `_canvasGroup.alpha=0; _canvasGroup.blocksRaycasts=false; _canvasGroup.interactable=false`
- `gameObject.SetActive(true)` → `_canvasGroup.alpha=1; _canvasGroup.blocksRaycasts=true; _canvasGroup.interactable=true`

**이유** (GameSystemRules.md Rule 5):
- `SetActive(false)` 상태의 오브젝트는 `LayoutGroup`에서 완전 제외 → 재활성화 시 레이아웃 깨짐 버그
- `DontDestroyOnLoad` 오브젝트에서 `SetActive(false)` 후 씬 전환 시 Awake 미호출 버그
- CanvasGroup 패턴: 오브젝트는 항상 활성 상태 유지, 시각적으로만 숨김

**신규 UI 뷰 추가 시 체크리스트**:
1. `CanvasGroup` 컴포넌트 부착 필수
2. `_canvasGroup` SerializeField 연결
3. Show/Hide를 `SetActive` 대신 반드시 CanvasGroup 패턴으로 구현

---

### 코드 리팩토링 Group 3/5/6 완료 (2026-05-20) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-19/10_46_code-refactoring/Plan.md`

**Group 3 (레이어 의존 제거) 완료 카테고리**:
- A: NetworkContext 교체 — ProductionTicker, GameEndUI에서 `NetworkManager.Singleton` 직접 호출 제거
- B: NGM 주입화 — LobbyUI, NetworkStatusUI에서 `OnClientConnectedCallback` 직접 구독 제거
  - NGM에 `OnAllPlayersReady(int)`, `OnServerDisconnected`, `GetCurrentRttMs()`, `IsNetworkRunning`, `ShutdownNetwork()` 추가
- D: ServerRpc 래퍼화 — BuildingPanelBase, BuildingPlacementUI, ProductionPanelUI에서 `*ServerRpc` 직접 호출 제거
  - NetworkBuildingController에 `RequestBuild`, `RequestDemolish`, `RequestUpgrade` 래퍼 추가
  - NetworkProductionController에 `RequestEnqueue`, `RequestCancelSlot`, `RequestSetRallyPoint`, `RequestToggleAuto` 래퍼 추가
- E (Combat): NetworkCombatController가 UnitView GetComponent 직접 호출 → GameEvents 발행으로 교체
  - GameEvents.OnNetworkCombatStarted/TargetChanged/Stopped, OnNetworkWalkStarted 4개 신규 추가
  - UnitView가 멀티플레이 분기에서 위 이벤트 구독
- E (GameEnd): NetworkGameEndController가 GameEndUI/RematchRequestPopup/GameUIManager 직접 호출 → GameEvents 발행
  - GameEvents.OnNetworkRematchAvailable/Requested/Declined, OnLocalRematchRequested/Accepted/Declined 추가
  - `[SerializeField] _gameEndUI/_rematchRequestPopup/_uiManager` 모두 제거
  - **IForfeitService 인터페이스 신규** (`Application/Interfaces/IForfeitService.cs`)
    - GameEndUseCase가 구현 (싱글), NetworkGameEndController가 구현 (멀티)
    - InGameSettingsUI에서 `FindFirstObjectByType<NetworkGameEndController>()` 제거 → 주입으로 변경
    - GameBootstrapper.Map.cs에서 `NetworkContext.IsNetworkActive` 분기로 적합한 구현체 선택 주입

**Group 5 (O(n) 캐시화) 완료**:
- UnitSpawnUseCase: `_unitsByPosition: Dictionary<HexCoord, List<UnitData>>` 추가, `NotifyUnitMoved(unit, from, to)` 신규
  - UnitMovementUseCase.ProcessStep이 호출해 위치 갱신 단일 진입점 보장
- BuildingPlacementUseCase: `_buildingsByPosition: Dictionary<HexCoord, BuildingData>` 추가
  - GetBuildingAt O(n) → O(1)
- HexGrid: `_ownedTileCounts: Dictionary<TeamId, int>` 추가, Generate에서 Neutral=총타일수로 초기화
  - SetOwner 호출 시 이전/새 팀 ±1 갱신, CountTilesOwnedBy O(187) → O(1)
- PopulationUseCase: `_usedPopulationByTeam` 추가, OnUnit*/OnBuilding* 이벤트 구독으로 증감 갱신
  - IDisposable 구현, GameBootstrapper.Map.cs ClearAll에서 `_population?.Dispose()` 추가

**Group 6 완료**:
- 6-1: BuildingType enum 0~31 명시값 부여 (기존 순서 보존, 직렬화 영향 없음)
- 6-2: UnitData/BuildingData 일반 생성자 → `:this(...)` 위임 패턴
- 6-7: OnUnitEnteredTile `Action<int, HexCoord>` → `Subject<UnitEnteredTileEvent>` 통일
  - GameBootstrapper.cs ActionDisposable 내부 클래스 제거
- 6-8: TODO 토스트 연결 — GameEvents.OnToastRequested 신규, ToastUI 구독
  - NetworkBuildingController/NetworkProductionController에서 reason 문자열 → ToastKey 매핑하여 발행
- 6-13: GameBootstrapper.IsNetworkMode → NetworkContext.IsNetworkActive로 단일화, using Unity.Netcode 제거
- 6-15: ToastKey Presentation → Application/Events로 이동 (네임스페이스 `Hexiege.Application`)
  - ToastMessageConfig: `using Hexiege.Presentation` → `using Hexiege.Application`
  - IUnitView 인터페이스 신규 (`Application/Interfaces/IUnitView.cs`)
  - UnitView가 IUnitView 구현, UnitFactory가 `GetComponent<IUnitView>()` 사용
  - UnitFactory.cs `using Hexiege.Presentation` 제거 — Infrastructure→Presentation 의존 완전 제거

**검증 결과**:
- Presentation 레이어 `using Unity.Netcode` 0건 (Grep 검증 완료)
- Infrastructure 레이어 `using Hexiege.Presentation` 0건 (Grep 검증 완료, 1건은 주석만)
- ServerRpc 직접 호출 Presentation에서 0건 (Grep 검증 완료, 4건은 주석만)

**Unity Inspector 수동 처리 필요**:
1. NetworkGameEndController에서 `_gameEndUI`, `_rematchRequestPopup`, `_uiManager` 3개 SerializeField 제거됨
   → 씬 Inspector에서 NetworkGameEndController 오브젝트의 위 슬롯이 비어 있음 (제거되었으므로 무관)
2. GameEndUI에 `_networkGameManager` SerializeField 신규 추가 → 씬 Inspector에서 NetworkGameManager 오브젝트 연결 필요
3. NetworkStatusUI에 `_networkGameManager` SerializeField 신규 추가 → 동일하게 연결 필요
4. ToastKey.cs 파일이 `Application/Events/`로 이동됨 → Unity Editor에서 .meta 갱신 자동 처리되지만, 이동된 파일 위치 확인 권장

**미처리/유의 사항**:
- IUnitView.SetDependencies가 Infrastructure 구체 타입(UnitFactory/BuildingFactory)을 인자로 받음
  - 엄격 정리 시 IUnitFactory/IBuildingFactory 별도 추출 필요, 본 리팩토링 범위에서 1차 의존만 제거
- Group 2/2-B/4/7은 이전 에이전트가 처리 완료한 상태 그대로 유지

---

### 건물 배치 팝업 3행 버튼 레이아웃 버그 수정 (2026-05-19) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-19/14_00_building-slot-layout-fix/`

**핵심 변경** (`Presentation/UI/BuildingPlacementUI.cs`):
- `List<CanvasGroup> _buttonCanvasGroups` 필드 추가
- `Awake()` 추가: `_buildingButtons` 순회 → `TryGetComponent<CanvasGroup>` → 없으면 `AddComponent`. 초기 alpha=0/interactable=false/blocksRaycasts=false 설정
- `Show()`: `SetActive(false/true)` → CanvasGroup alpha/interactable/blocksRaycasts 0↔1 전환으로 교체
- `UpdateCostTextColors()`: `!gameObject.activeSelf` 조건 → `_buttonCanvasGroups[i].alpha < 0.5f` 조건으로 교체

**근본 원인**: `HorizontalLayoutGroup(ChildForceExpandWidth=1)`은 `SetActive(false)` 자식을 레이아웃에서 완전 제외 → 7개 건물(Human/Spirit) 시 3행의 슬롯 하나만 남아 전체 가로폭 채움.

**해결 원칙**: CanvasGroup.alpha=0으로 숨기면 GameObject 활성 상태 유지 → RectTransform 공간 보존 → 레이아웃 균일.

---

### 인게임 설정 메뉴 + 게임 포기 기능 (2026-05-18~19) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-18/23_36_ingame-settings-forfeit/`

**핵심 변경**:
- `InGameSettingsUI.cs` (`Presentation/UI/`) 신규: `IGameUI` 구현. `Show()` — 싱글플레이 `Time.timeScale=0`(`_pausedBySettings=true`), SharedBackground 등록. `Hide()` — `_pausedBySettings`이면 `timeScale` 복원, `_confirmPopup?.Hide()`. 포기 흐름: ConfirmPopup 표시 → `OnForfeitConfirmed()` → `NetworkContext.IsNetworkActive` 분기 → 멀티 `RequestForfeit()` / 싱글 `GameEndUseCase.Forfeit()`.
- `ConfirmPopup.cs` (`Presentation/UI/`) 신규: 범용 확인 팝업. `Show(message, confirmLabel, cancelLabel, onConfirm, onCancel)`. `BlockingOverlay`(CanvasGroup) — Show 시 alpha=1/blocksRaycasts/interactable=true, Hide 시 alpha=0/false/false.
- `GameEndUseCase.Forfeit()` 신규: `IsGameOver=true` 설정 → `GameEvents.OnGameEnd(TeamId.Red)` 발행 (싱글플레이 포기).
- `NetworkGameEndController.RequestForfeit()` + `ForfeitServerRpc`: `RequireOwnership=false`. Host=ClientId0=Blue, Client=Red. 기존 `_announced` 플래그 재사용, `AnnounceWinnerClientRpc` 재사용.
- `GameHudUI`: `_settingsButton`, `_settingsUI` 필드 추가, `OnSettingsClicked()` 메서드 추가.
- `GameBootstrapper`: `_inGameSettingsUI`, `_confirmPopup` SerializeField 추가, `LoadMap()`에 UIManager 등록 + Initialize 호출.
- `SetupInGameSettingsUI.cs` (Editor): HUD 재배치(StatsPanel + 4 Row) + 설정 패널 생성 + 필드 배선 자동화.

**설계 포인트**:
- `AnimatedPanel._backgroundOverlay`(CanvasGroup) → `[UI]/Background`의 CanvasGroup 연결 필수. 미연결 시 반투명 배경 미표시.
- `ConfirmPopup.BlockingOverlay`: ConfirmPopup 열릴 때 Settings 패널의 SharedBackground 클릭 차단 (의도치 않은 패널 닫힘 방지).
- `ConfirmPopup.Show()` — `_panel.Show()` 직접 호출. AnimatedPanel은 항상 active 상태이므로 `SetActive(true)` 선호출 불필요.
- 싱글플레이 일시정지: `Time.timeScale=0`, UIAnimator `SetUpdate(true)` 적용으로 DOTween이 timeScale=0 중에도 동작.

---

### 비생산 건물 공용 액션 패널 UI — BuildingActionPanelUI (2026-05-18~19) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-18/17_00_building-action-panel-ui/`

**핵심 변경**:
- `BuildingPanelBase` (`Presentation/UI/`) 추상 베이스 신규: protected SerializeField 6개(`_popup`, `_sharedBackground`, `_headerText`, `_cancelButton`, `_demolishButton`, `_demolishRefundText`). `InitializeBase()`, `Show()`/`Close()` virtual, `OnDemolishButtonClick()` 공통 철거 흐름 (싱글/멀티 분기). Template Method 패턴 — `OnShow()` / `OnBeforeClose()` / `BeforeDemolish()` 훅.
- `BuildingActionPanelUI` (`Presentation/UI/`) 신규: `BuildingPanelBase` 상속. `Initialize` 1개만 구현. 비생산 건물(채굴소/타워 등) 클릭 시 표시.
- `ProductionPanelUI` 리팩토링: `BuildingPanelBase` 상속. 공통 필드/메서드 제거. `Show`→`OnShow`, `Close`→`OnBeforeClose`, `OnDemolishButtonClick`→`BeforeDemolish` 훅으로 분해.
- `BuildingTypeHelper.CanShowActionPanel(BuildingType)` 추가: `!IsProductionBuilding && type != Castle`
- `InputHandler`: `_actionPanelUI` 필드 추가, Initialize 시그니처 확장, ClosedFrame 체크 + 건물 클릭 분기에 CanShowActionPanel 추가.
- `GameBootstrapper`: `_buildingActionPanelUI` SerializeField 추가, UIManager 등록, SetupBuildings Initialize, SetupInput 인자 추가, 비생산 건물 환불 캐시 루프 추가.
- `SetupBuildingActionPanelUI.cs` (Editor): ProductionPanelUI GO 복제 → 생산 전용 자식 GO 제거 → BuildingActionPanelUI 컴포넌트 교체 → 공유 필드 6개 자동 배선 → GameBootstrapper 슬롯 자동 연결.

**설계 포인트**:
- SharedBackgroundButton이 **비활성 GO**에 부착 → `FindFirstObjectByType<SharedBackgroundButton>(FindObjectsInactive.Include)` 필수
- 비생산 건물 환불 캐시 루프 누락 시 `GetTotalInvestedCost` → 0 반환 버그

---

### OnEntityDied 이벤트 분리 리팩토링 (2026-05-18) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-18/15_00_entity-died-event-split/`

**핵심 변경**:
- `EntityDiedEvent` + `OnEntityDied` 완전 삭제
- `UnitDiedEvent(UnitData Unit)` / `BuildingDiedEvent(BuildingData Building)` 강타입 struct 신규
- `GameEvents.OnUnitDied` / `GameEvents.OnBuildingDied` Subject 신규
- 발행 측 4곳 교체: `UnitCombatUseCase.TryAttack` (타입 분기), `BuildingPlacementUseCase.DemolishBuilding`, `NetworkCombatController.HandleUnitDied` / `HandleBuildingDied`
- 구독 측 9곳 교체: `BuildingFactory`, `UnitView`, `ProductionTicker`(핸들러 2개 분리), `GameEndUseCase`, `FlowFieldService`, `GameBootstrapper`, `NetworkCombatController`(서버 구독 2개 분리 + OnNetworkDespawn Dispose 2개), `HexGridRenderer`
- **RPC 시그니처 유지**: `EntityDiedClientRpc(int entityId, bool isUnit)` 그대로 — 와이어 포맷 호환성

**설계 원칙**:
- 분리 완료 후 `OnEntityDied`/`EntityDiedEvent` .cs 전체에서 0건 검증 필수
- `NetworkCombatController._diedSubscription` 단일 → `_unitDiedSubscription` + `_buildingDiedSubscription` 2개 — OnNetworkDespawn에서 둘 다 Dispose
- 사망 이벤트 발행 순서: RemoveUnit/RemoveBuilding **직전** 발행 유지 (구독자가 도메인 Dict 접근 가능해야 함)

---

### 건물 철거 시스템 (2026-05-18) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-17/18_15_building-demolish/`

**핵심 변경**:
- `UnitProductionUseCase.CancelAllQueue(barracksId)` 신규: ① ClearRallyPoint(barracksId) — UnregisterBarracks 이전 필수 ② CurrentProducing 환불 ③ PendingQueue IsCharged=true 항목 환불, false 항목은 환불 없이 제거 ④ 상태 초기화 ⑤ OnProductionQueueChanged 발행 ⑥ UnregisterBarracks 호출
- `BuildingPlacementUseCase.DemolishBuilding(buildingId)` 신규: `GameEvents.OnEntityDied.OnNext(new EntityDiedEvent(building))` → `RemoveBuilding(buildingId)` 반환
- `ProductionPanelUI.OnDemolishButtonClick()`: 싱글 → CancelAllQueue + AddGold(TotalInvestedCost/2) + DemolishBuilding → Close(). 멀티 → RequestDemolishServerRpc → Close()
- `NetworkBuildingController.RequestDemolishServerRpc`: 소유권(팀) + Castle 아님 + 건물 존재 검증 → CancelAllQueue + AddGold + DemolishBuilding → DemolishBuildingClientRpc
- `DemolishBuildingClientRpc`: `if (IsServer) return;` (호스트 이중 처리 방지) → buildingPlacement.DemolishBuilding()
- `BuildingFactory.Awake()` — OnEntityDied 구독 추가(B방식): `if (e.Entity is not BuildingData building) return;` 유닛 필터링 → `_buildingObjects.TryGetValue` → Destroy(go)
- `BuildingView.cs` 삭제 (prefab에 미부착, 책임 BuildingFactory로 이전)
- `MiningEffectView.cs` 삭제 (미사용, BuildingView 의존)

**건물 프리팹 구조 확인**:
- Root GO(Transform ONLY) + Child GO(MeshFilter/MeshRenderer) — BuildingView 미부착이 원인
- BuildingFactory가 `_buildingObjects` Dict로 Id→GO 관리 → 이 Dict로 GO 직접 파괴

**골드 환불 조회 API**: `BuildingStats.GetTotalInvestedCost(type, race)` / 2

**범위 조정**: 채굴소(MiningPost) 철거 UI(MiningPostPanelUI + InputHandler 분기)는 별도 작업으로 연기

---

### 건물 업그레이드 시스템 + 단계별 생산건물 (2026-05-17) ✅ 코드 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-17/02_16_building-upgrade-system/`

**핵심 변경**:
- `BuildingType` 열거형에서 단일 `Barracks` 제거 → 종족별 라인 × 단계(1/2/3) 26종으로 확장
- `BuildingTypeHelper` (Domain) 신설: `IsProductionBuilding`, `GetStage`, `GetNextStage`, `CanUpgrade`
- `BuildingData.Stage` 파생 프로퍼티 (BuildingType에서 도출, 별도 저장 없음)
- `BuildingStats.GetUpgradeCost(BuildingType)` — 종족 무관 단일 값. Initialize 시 모든 종족 엔트리에 동일 값 주입.
- `BuildingStatsConfig.BuildingTypeEntry`에 `upgradeCost` 필드 추가
- `GameEvents.OnBuildingUpgraded` (`BuildingUpgradedEvent: OldBuildingId, NewBuilding`) 추가
- `BuildingPlacementUseCase.UpgradeBuilding(id, race)` / `UpgradeBuildingWithId(...)` — 기존 BuildingData 제거 후 next stage로 교체. 타일 IsWalkable/Owner 유지.
- `BuildingFactory.UpgradeBuildingObject` — **새 GO 먼저 생성 → 기존 GO Destroy** 순서로 빈 타일 방지
- `ProductionPanelUI`: `BuildingUnitMapping` (BuildingType → 유닛 라인업) Inspector 구조 도입. 단계별 잠금: `_activeUnitLocks[i]` 추가, `_unitLockIndicators` 활성화. 잠금 유닛 탭 시 `ToastKey.UpgradeRequired`. 업그레이드 버튼(`_upgradeButton`/`_upgradeCostText`) 신규.
- 기존 6개 종족 고정 리스트는 **주석 처리** (테스트 통과 후 삭제 예정)
- `NetworkBuildingController.RequestUpgradeServerRpc` / `UpgradeBuildingClientRpc` 신규 — 소유권/골드 재검증 후 클라이언트 동기화
- `GameBootstrapper.InitializeBuildingStatsFromConfig`에 UpgradeCost 주입 추가. `_productionUI.Initialize`에 `BuildingPlacementUseCase`, `NetworkBuildingController` 인자 2개 추가.

**Barracks→IsProductionBuilding 치환 위치**:
- `UnitProductionUseCase.RegisterBarracks`
- `ProductionTicker.OnBuildingPlaced`, `OnEntityDied`
- `InputHandler` 타일 클릭 분기

**SetupBuildingStatsConfig.cs** (Editor): Barracks 1행 → 24행(생산건물 전체)로 확장. 기본값 정책 = 1단계 30HP/100G/80U, 2단계 45HP/150G/120U, 3단계 60HP/200G/0U (Trans HP ×1.6~2).

**Inspector 작업 전체 완료 (2026-05-18)**:
- `BuildingFactory` 프리팹 리스트 — ✅ **완료**: 각 BuildingType별 Blue/Red 프리팹 연결
- `BuildingPlacementUI` 6개 종족별 리스트 — ✅ **완료**: 각 라인의 1단계 건물로 재구성
- `ProductionPanelUI._buildingUnitMappings` — ✅ **완료 (2026-05-18)**: 각 BuildingType별 유닛 라인업 + requiredStage 설정
- `ProductionPanelUI._unitLockIndicators` — ✅ **완료**: 각 버튼 위 잠금 오버레이 GO 생성/연결
- `ProductionPanelUI._upgradeButton` + `_upgradeCostText` — ✅ **완료**: UI 추가
- `BuildingStatsConfig.asset` — ✅ **완료 (2026-05-18)**: 32개 BuildingType 전체 항목 채움. StatsReference.md 기준 HP/비용/공격력/업그레이드비용 전종 적용. AutoTower 종족별 AttackCooldown(Human 5.0s, Spirit 3.5s, Trans 5.0s) 적용.
- `ToastMessageConfig.asset` — ✅ **완료 (2026-05-18)**: key 3 `UpgradeRequired` 추가 (message: "건물 업그레이드가 필요합니다", duration: 1 — Unity 자동 정규화)

**주의 — 직렬화 영향**: `BuildingType` 열거형 순서 변경 → 씬/에셋의 기존 Barracks=1 인덱스 직렬화 데이터가 다른 enum 값으로 덮어쓰임. 개발 단계에서 허용했으나 Inspector 모든 항목 재검토 필요.

---

### 건물 스탯 확정 + Config 32종 항목 채움 + AttackCooldown 필드 추가 (2026-05-18) ✅ 완료

**변경 파일**:
- `Infrastructure/Config/BuildingStatsConfig.cs` — `BuildingTypeEntry` struct에 `humanAttackCooldown`, `spiritAttackCooldown`, `transcendenceAttackCooldown` (float) 3개 필드 추가
- `Domain/Building/BuildingStats.cs` — `StatValues`에 `AttackCooldown (float)` 추가. `GetAttackCooldown(BuildingType, RaceId)` 메서드 신규 추가.
- `Bootstrap/GameBootstrapper.cs` — `InitializeBuildingStatsFromConfig()`에 `AttackCooldown` 주입 추가
- `Resources/Config/BuildingStatsConfig.asset` — 3개 항목 → 32개 BuildingType 전체 항목으로 확장. StatsReference.md 기준 값 적용.

**핵심 수치 (AutoTower, buildingType: 2)**:
- Human (CannonTower): HP 50, 비용 150, 공격력 15, 쿨다운 5.0s
- Spirit (RuneSpire): HP 150, 비용 200, 공격력 15, 쿨다운 3.5s
- Transcendence (VineTower): HP 100, 비용 175, 공격력 15, 쿨다운 5.0s

**API**: `BuildingStats.GetAttackCooldown(type, race)` — 타워 구현 시 쿨다운 조회에 사용. 비타워 건물은 0f 반환.

---

### ProductionPopup UI 레이아웃 재구성 + 2/3단계 건물 랠리 마커 버그 수정 (2026-05-17~18) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-17/15_30_production-popup-ui-layout/`

**수정 파일**:
- `Presentation/UI/ProductionPanelUI.cs`
- `Editor/SetupProductionPopupUI.cs`
- `Domain/Building/BuildingStats.cs`
- `Bootstrap/GameBootstrapper.cs`
- `Presentation/Production/ProductionTicker.cs`

**변경 내용 요약**:

1. **BuildingIconEntry 구조체 블루/레드 분리**
   - `icon (Sprite)` 단일 필드 → `blueIcon`, `redIcon` 2개 필드로 분리
   - `GetBuildingIcon(BuildingType, TeamId)` — 팀에 맞는 Sprite 반환

2. **철거 환불 누적 계산**
   - 기존: 현재 건물의 건설비만 기준
   - 변경: 1단계 건설비 + 모든 업그레이드비 합산의 50%
   - `BuildingStats._totalInvestedCostCache` 딕셔너리 추가 (`SetTotalInvestedCost` / `GetTotalInvestedCost`)
   - `GameBootstrapper` 초기화 시 단계별 체인 순회하여 캐시 채움

3. **2유닛 건물 레이아웃 [유닛1][빈슬롯][유닛2]**
   - `_unitButtonGroups (List<CanvasGroup>)` 필드 추가
   - `BindButtonUnitTypes()`: 2유닛 시 슬롯1을 CanvasGroup alpha=0으로 숨겨 레이아웃 공간 유지
   - `_activeUnitTypes`를 3개(슬롯0/더미/슬롯2)로 확장하여 IndexOutOfRange 방지

4. **HeaderText 건물 이름 동적 표시**
   - `_headerText (TextMeshProUGUI)` 필드 추가
   - `Show()` 내 `_headerText.text = barracks.Type.ToString()` 갱신

5. **UpdateButtonPortraits() 2유닛 슬롯 매핑 수정**
   - 2유닛 시: slot0=list[0], slot1=스킵(더미), slot2=list[1]
   - 기존 코드는 list[i]→portrait[i] 직접 대응으로 슬롯2 갱신 누락 → 이전 건물 초상화 잔존 버그 수정

6. **2/3단계 건물 랠리 마커 미표시 버그 수정** ← 핵심
   - 원인: `ProductionTicker`가 `OnBuildingPlaced`만 구독 → 업그레이드 시 새 건물이 `_states`에 미등록 → 마커 생성 안 됨
   - 수정: `SubscribeEvents()`에 `GameEvents.OnBuildingUpgraded` 구독 추가
   - `OnBuildingUpgraded` 핸들러: `UnregisterBarracks(e.OldBuildingId)` + `RegisterBarracks(e.NewBuilding)`
   - 전 종족(Human/Spirit/Transcendence) 테스트 통과 (2026-05-18)

**주의 — Sprite 명명 규칙**:
- 경로: `Assets/_Project/Sprites/Buildings/`
- 규칙: `bld_{buildingtype_소문자}_blue.png` / `bld_{buildingtype_소문자}_red.png`
- 에디터 스크립트 Step [4]: `AssetDatabase.FindAssets()`로 자동 탐색 후 `_buildingUpgradeIcons` 자동 채우기

---

### Rule 20 슬롯0 확장 (2026-05-17) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-17/00_21_production-rule20-slot0-extension/`

**수정 파일**: `Application/UseCases/UnitProductionUseCase.cs`, `Docs/GameSystemRules.md`

**변경 내용**:
- `ToggleAutoProduction`에 슬롯0 체크 블록 추가 (AutoTypes 상한 체크 직후, Rule 2-1 직전)
  - 조건: `CurrentProducing.HasValue && CurrentProducing.Value == type && !CurrentIsAuto`
  - 처리: `CurrentIsAuto = true` + `AutoTypes.Add(type)` + `NormalizeAutoCycleIndex` + 이벤트 발행
- `GameSystemRules.md` 규칙 20 문구에 "슬롯0 수동 생산 중" 케이스 추가

**설계 의도**:
- 슬롯0에서 수동 A 생산 중 A 자동등록 → 슬롯1에 중복 추가 없이 슬롯0 자체를 자동으로 전환
- 완료 시 `wasAuto=true` → 자동 순환 자연 시작
- BUG-15(CurrentIsAuto=true 케이스)와 조건 상호 배타 → 충돌 없음

---

### 건물 생성/파괴 시 유닛 이동 멈춤 수정 (2026-05-17) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-16/17_00_building-repath-freeze-fix/`

**수정 파일**: `Presentation/Unit/UnitView.cs`

**문제**: 건물 생성/파괴 시 `RepathAllAliveUnits → OnPathInvalidated → MoveTo` 흐름으로 코루틴이 즉시 재시작되어 1~2 프레임 유닛 멈춤 발생.

**수정 내용**:
- **필드 2개 추가**: `_pendingPath (List<HexCoord>)`, `_currentNextTileCoord (HexCoord?)`
- **`OnPathInvalidated()` 분기 추가**:
  - 현재 Lerp 중인 다음 타일(`_currentNextTileCoord`)에 건물이 생긴 경우 → 기존처럼 즉시 `MoveTo()` (건물 뚫고 지나가기 방지)
  - 그 외 → `_pendingPath = newPath` 저장만 (코루틴 유지, 멈춤 없음)
- **`MoveAlongPathV3()` 수정**: 각 타일 Lerp 시작 직전 `_currentNextTileCoord` set, 완료 직후 null. 타일 도착 직후 `_pendingPath` 소비 → 현재 위치로 새 path 슬라이스 후 외부 while 재진입. 인덱스 못 찾으면 `MoveTo()` 안전망.
- **`MoveTo()` 수정**: 진입 시 `_pendingPath = null`, `_currentNextTileCoord = null` 초기화.
- **`MoveCleanupAndCompleteV3()` 수정**: 종료 시 두 필드 모두 null 초기화.

**핵심 설계**:
- "부드러운 교체(예약) = 기본, 즉시 재시작 = 예외(앞 타일 막힌 경우만)"
- GameBootstrapper/FlowFieldService 변경 없음. UnitView.cs 단독 수정.

---

### 건물 배치 패널 실패 피드백 + UI 개선 (2026-05-16) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-16/20_10_building-placement-fail-feedback/`

**수정 파일**:
- `BuildingPlacementUI.cs` — 3가지 변경:
  1. **UpdateCostTextColors()** 신규 private 메서드 추가. `_buildingCostTexts[i]`를 순회하며 현재 골드와 건설 비용 비교, 부족 시 `Color.red`, 충분 시 `Color.white`.
  2. **Show()** 마지막에 `UpdateCostTextColors()` 즉시 호출 + `GameEvents.OnResourceChanged` 구독(`_resourceSubscription: IDisposable`). 팝업 열린 동안만 실시간 갱신.
  3. **Close()** 앞에 `_resourceSubscription?.Dispose()` + 비용 텍스트 전체 `Color.white` 초기화.
  4. **PlaceAndClose() 싱글플레이 분기** — 골드 부족 시 `ToastUI.Show(ToastKey.GoldInsufficient)` 호출 후 `return`(팝업 유지).

**핵심 설계**:
- 멀티플레이 분기는 수정하지 않음 (범위 밖).
- `IDisposable _resourceSubscription` 패턴으로 Show/Close 생명주기에 이벤트 구독을 한정.
- `GetBuildingList(_currentTeam, race)` 기존 메서드 재사용으로 버튼-텍스트 인덱스 일치 보장.

---

### ToastUI SetActive 버그 수정 (2026-05-16) ✅ 완료

**수정 파일**: `Presentation/UI/Common/ToastUI.cs`

**버그**: `ClearAll()` / `FinishCurrent()` 에서 `_canvasGroup.gameObject.SetActive(false)` 호출 → `OnGameStarted`로 ClearAll 실행 시 루트 비활성화 → `Update()` 정지 → 이후 토스트 큐 완전히 동작 불가.

**수정**: 3곳에서 `SetActive(false/true)` 제거:
- `TryShowNext()`: `SetActive(true)` → `blocksRaycasts=true, interactable=true`
- `FinishCurrent()`: `SetActive(false)` → `blocksRaycasts=false, interactable=false`
- `ClearAll()`: `SetActive(false)` → `blocksRaycasts=false, interactable=false`

**원칙**: Toast 루트 GameObject는 항상 활성 상태. 숨김은 `alpha=0 + blocksRaycasts=false`만으로 처리.

---

### 건물 비용 텍스트 'G' 접미사 제거 (2026-05-16) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-16/21_30_building-cost-g-removal/`

**수정**: `BuildingPlacementUI.cs` 2곳 — `$"{cost}G"` → `$"{cost}"`.
생산 패널(원래부터 숫자만)과 동일한 표기로 통일.

---

### 유닛 생산 실패 피드백 시스템 (2026-05-16) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-16/15_09_production-fail-feedback/`

**신규 파일 4개**:
- `Infrastructure/Config/ToastMessageConfig.cs` — ScriptableObject. ToastEntry struct(key/message/duration). TryGet().
- `Presentation/UI/Common/ToastKey.cs` — enum: GoldInsufficient=0, PopulationFull=1, ProductionQueueFull=2.
- `Presentation/UI/Common/ToastUI.cs` — 싱글턴 MonoBehaviour. IPointerClickHandler 구현. 정적 진입점 `ToastUI.Show(ToastKey)`. Queue<ToastKey> 방식. DontDestroyOnLoad 독립 Canvas. CanvasGroup DOTween 페이드아웃. GameEvents.OnGameStarted/OnGameEnd 구독으로 자동 정리.
- `Editor/SetupToastUI.cs` — 1회성 에디터 스크립트. Toast를 씬 루트 오브젝트(부모 없음)로 생성. 자체 Canvas(ScreenSpaceOverlay, sortingOrder=100) + GraphicRaycaster + CanvasGroup + ToastUI.

**핵심 주의사항**:
- **DontDestroyOnLoad = 루트 오브젝트 전용**: Toast를 [UI] Canvas 자식으로 배치하면 씬 전환 시 파괴됨. 반드시 씬 루트(부모 없음)에 배치.
- **SetActive(false) 사용 금지**: 비활성 상태에서 Awake() 미호출 → DontDestroyOnLoad 미등록. 숨김은 CanvasGroup.alpha=0으로 처리.
- **골드 텍스트 색상**: `_goldText`(보유 골드 표시)는 변경 안 함. `_unitCostTexts[i]`(각 유닛 생산 비용)만 개별 평가하여 빨간색 전환.

**수정 파일**:
- `ProductionPanelUI.cs` — `ProductionFailReason` enum 추가. `OnUnitTap()` 사전 검증 + HandleProductionFail(). `UpdateInfoBar()` 유닛별 비용 텍스트 색상 조건 추가.
- `GameHudUI.cs` — `_lastPopFull` nullable 캐시 필드. `UpdateDisplay()` 인구수 텍스트 `used >= max` 조건 색상 전환.
- `UnitProductionUseCase.cs` — `TryStartNext()` 자동 생산 자원 부족 시 재시도 → 즉시 취소(IsCharged=false만, IsCharged=true는 Rule 2 유지).

---

### 랠리포인트 깃발 팀별 표시 분리 (2026-05-16) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-16/15_30_rally-point-flag-visibility/`

**버그**: 클라이언트가 랠리포인트 설정 시 호스트 화면에도 깃발이 표시되던 현상.

**원인**: `RallyPointChangedEvent`에 팀 정보가 없어, `ProductionTicker`가 상대 팀 이벤트도 무조건 처리.
호스트가 RPC 핸들러에서 `SetRallyPoint()`를 실행하면 호스트 측에서도 `OnRallyPointChanged` 발생.

**수정 파일 (3개)**:
- `GameEvents.cs` — `RallyPointChangedEvent`에 `TeamId Team` 필드 추가, 생성자 파라미터 추가
- `UnitProductionUseCase.cs` — `SetRallyPoint()` / `ClearRallyPoint()` 이벤트 발행 시 `state.Team` 전달
- `ProductionTicker.cs` — `OnRallyPointChanged()` 진입부에 팀 필터 추가. `IsServer → Blue`, 아니면 `Red`. 싱글플레이(NetworkManager=null) 시 필터 건너뜀.

**설계 원칙**: 이벤트가 자기 완결적이 되도록(누구 팀 것인지 이벤트 자체에 포함), 필터링 책임은 Presentation 레이어(ProductionTicker)에 위치.

---

### 혼잡도 기반 유닛 분산 시스템 (2026-05-15) ✅ 완료

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-15/17_29_congestion-based-spread/`

**문제**: 모든 유닛이 성 방향으로 세로 줄 이동 현상. v1(CastleApproachManager — 성 인접 타일 배정)은 경로가 거의 동일해 시각 효과 없었음.

**신규 파일**:
- `Application/Services/CongestionMap.cs` — 타일별 혼잡도 관리 (Increment/Decay/Clear). 순수 C#.
- `Application/Services/CongestionAwarePathfinder.cs` — 혼잡도 가중 A*. 타일 비용=1+(혼잡도×CongestionWeight). 목적지 non-walkable이면 walkable 인접 자동 대체.

**삭제 파일**:
- `Application/Services/CastleApproachManager.cs` — v1 전체 삭제 (테스트 완료 후)
- `Infrastructure/Config/CongestionConfig.cs` — 필요 없어 미생성 (GameConfig에 통합)

**수정 파일**:
- `GameConfig.cs` — `CongestionDecayInterval=5f`, `CongestionWeight=3f` 필드 추가 (Header "Congestion Spread")
- `GameEvents.cs` — `OnUnitEnteredTile: Action<int, HexCoord>` 추가
- `UnitView.cs` — `_isAStarMoving` bool 필드. A* 이동 시 true, 전투 추격 시 false. 타일 전환 완료 시 `_isAStarMoving=true`이면 OnUnitEnteredTile 발행.
- `ProductionTicker.cs` — CongestionMap/Pathfinder 주입. 감쇠 타이머(`_decayTimer`). MoveTowardEnemyCastle에서 A* 우선, 실패 시 BFS 폴백.
- `GameBootstrapper.cs` — CongestionMap/Pathfinder 생성. OnUnitEnteredTile 구독(서버 가드: `if NetworkActive && !IsServer return`). ClearAll에 Clear 추가.

**핵심 설계 결정**:
- CongestionConfig ScriptableObject 미생성 → GameConfig.asset에 2필드 통합 (ScriptableObject 낭비 방지)
- reactive congestion: 유닛이 실제 타일 진입 시점에 혼잡도 증가 (사전 등록 아님) — 같은 건물에서 동시 생산 불가이므로 반응형으로 충분

---

### 로비 캐릭터 잘못 표시 버그 — 로그 추가 + 원인 확정 (2026-05-15) ✅ 완료

**작업 내용**: 랜덤 매칭 후 Red 클라이언트의 캐러셀에 선택한 종족 대신 Human이 잠깐 표시되는 버그 추적.

**로그 추가 파일**: `Presentation/UI/Views/Lobby/Battle/RaceSelectionView.cs`
- `ApplyCarouselPositions()`: Inspector 위치값, 캐릭터 배열 수, 각 캐릭터별 현재위치/목표위치 로그 추가
- `KillAllCharacterTweens()`: 호출 시각 로그 추가

**원인 확정**: CharPreview_Human/Spirit/Transcendence가 실제 유닛 프리팹(Unit_Pistoleer_Blue 등) 인스턴스 → NetworkTransform이 Host 캐러셀 위치를 Red 클라이언트로 동기화하여 DOTween 위치를 덮어씀. 코드 수정 없이 Unity Editor 작업으로 해결.

**수정 (에디터 작업)**: Lobby.unity에서 CharPreview 3종 Unpack Completely → UnitView, AnimationEventRelay, NetworkUnit, NetworkTransform, NetworkObject 컴포넌트 제거.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-15/02_57_character-display-bug/`

---

### 유닛 회전 시스템 수정 + MovementLogger 삭제 (2026-05-14) ✅ 완료

**수정 파일**:
- `Presentation/Unit/UnitView.cs`
  - `[SerializeField] private float _rotationSpeed = 270f` (기존 `const CombatRotationSpeed = 270f` 교체)
  - A* 이동 방향 계산: `FacingDirection.FromCoords(from, to)` → `CalculateAttackAngle(toPos)` (현재 월드 위치→목적지 Atan2)
  - A* Lerp 루프 내 매 프레임 `Quaternion.RotateTowards(현재, targetRot, _rotationSpeed * Time.deltaTime)` 추가
  - 정렬(Align) 단계 방향 계산: 동일하게 `CalculateAttackAngle(alignView)` 교체
  - 정렬 Lerp 루프 내 동일하게 RotateTowards 추가
  - `ApplyDirection()` 호출부(2곳) 제거 (메서드 자체는 유지)
  - `MovementLogger.Log()` 29개 호출 전체 제거
- `Application/Services/MovementLogger.cs` — **파일 삭제**
- `Bootstrap/GameBootstrapper.cs` — `MovementLogger.SessionStart()` 제거
- `Application/Services/AttackPositionManager.cs` — `MovementLogger.Log()` 3개 제거

**핵심 설계 결정**:
- `CalculateAttackAngle`이 이미 Atan2 기반 정확한 각도 계산을 하므로 A*/정렬 회전에도 동일 메서드 재사용
- `_rotationSpeed` 단일 필드로 모든 회전(이동/정렬/추격/공격) 통일 — Inspector 조정 가능
- `ApplyDirection()` 메서드는 현재 호출처 없으나 코드에 남겨둠 (삭제는 별도 작업)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-14/14_30_unit-rotation-system-fix/`

---

### 유닛 이동/전투 시스템 재설계 (2026-05-11) ✅ 완료

슬롯 기반 분산 방식 전면 폐기 → 겹침 허용 단순 구조로 전환. 근접/원거리 동일 상태 머신.

**비활성화(주석 처리) 항목** (2026-05-11 당시):
- `GameBootstrapper.cs` — TileMoveSlotManager / TileOccupancyManager / AttackPositionManager 생성 및 주입 코드
- `Presentation/Unit/UnitView.cs` — 슬롯/점유 관련 필드(`_moveSlotManager`, `_attackPositionManager`, `_v2MoveSlotTile`, `_v2AttackSlotTargetCoord`, `_pendingOccupancyTile`, `_v2InStationaryCombat`) 및 메서드(`ReleaseV2MoveSlotIfClaimed`, `ReleaseV2AttackSlotIfClaimed`)
- `Application/UseCases/UnitMovementUseCase.cs` — `_occupancyManager`, `RegisterOccupancyMove()`, `ReleaseOccupancy()`, `FindForwardAvailable()`
- `Domain/Unit/UnitData.cs` — `ClaimedTile` 필드
- `Domain/Unit/UnitStats.cs` — `OccupancySize` 필드 및 `GetOccupancySize()` 메서드

**✅ 완전 제거 완료 (2026-05-16 dead-code-cleanup)**:
- `Application/Services/TileMoveSlotManager.cs` — **파일 삭제** (+ .meta)
- `Application/Services/TileOccupancyManager.cs` — 비활성 메서드 5개 제거 (`OnUnitMoved`, `OnUnitRemoved`, `ReserveOccupancy`, `BfsFindAvailable`, `FindForwardAvailable`). 클래스 자체는 유지.
- `Application/UseCases/UnitMovementUseCase.cs` — `RegisterOccupancyMove()`, `ReleaseOccupancy()`, `GetOccupancySize()` 제거
- `Domain/Unit/UnitData.cs` — `ClaimedTile` 프로퍼티 제거
- `Domain/Unit/UnitStats.cs` — `OccupancySize` 필드, `GetOccupancySize()` 제거
- `Presentation/Unit/UnitView.cs` — `ClaimedTile` 참조 7곳 제거
- `Bootstrap/GameBootstrapper.cs` — TileMoveSlotManager getter 및 OccupancySize 할당 라인 제거
- `Domain/Hex/HexPathfinder.cs` — `FindPathToNeighbor()` 제거 (호출처 없음)
- `Application/Events/GameEvents.cs` — `OnGamePaused`, `OnGameResumed` Subject 제거 (발행 코드 없음)
- `Presentation/UI/GameUIManager.cs` — OnGamePaused/OnGameResumed 구독 코드 및 Notify 메서드 제거
- `Presentation/UI/Core/IGameUI.cs` — `OnGamePaused()`, `OnGameResumed()` default 메서드 제거

**신규 구현**:
- `UnitView.cs` — `MoveAlongPathV3()` 새 상태 머신 (근접/원거리 동일):
  - Phase 0(A* Lerp) → HasEnemyInDetectRange 감지 → Phase 1(월드 직선 추격) → HasEnemyInRange 진입 → 공격 → FindForwardClosestTile → Phase 0 재개
- `UnitCombatUseCase.cs` — `FindFirstEnemyInDetectRange()` 내 isMelee 분기 제거, 모든 유닛 `DetectRange × TileHeight` 통일
- UnitStatsConfig Inspector — 원거리 유닛 DetectRange를 AttackRange보다 크게 설정

**BUG-001 (2026-05-12)**: 전투 추격 중 건물 생성/파괴 시 유닛 멈춤
- `_isInCombatPursuit` bool 필드 추가
- `IsInCombat()` → `_combatTargetTransform != null || _isInCombatPursuit`

**BUG-002 (2026-05-13)**: 전투 종료 후 약 1타일 순간이동
- `ResumeFromForwardTileV3()` 내 즉시 스냅(`transform.position = forwardView`) 제거
- `MoveAlongPathV3()` 전투 종료 직후 정렬 Lerp 추가 (동일 이동 속도로 걸어서 이동)
- 정렬 Lerp 내 매 프레임 적 감지 체크 (중단 시 전투 이동 재진입)

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-11/23_19_unit-movement-redesign/`

---

### 이동 슬롯 오프셋 Inspector 조정 기능 추가 (2026-05-11) ✅ 사용자 확인 완료

**수정 파일**:
- `Application/Services/TileMoveSlotManager.cs` — `private const float SlotForwardRatio/SlotSideRatio` → `private readonly float`. 기본값 0.30f를 유지하는 생성자 파라미터 추가. `GetSlotWorldPositionInternal`을 `static` → 인스턴스 메서드로 전환(readonly 필드 접근을 위해).
- `Bootstrap/GameBootstrapper.cs` — `[Header("이동 슬롯 오프셋")]` + `[SerializeField] private float _slotForwardRatio/SideRatio = 0.30f` 추가. `new TileMoveSlotManager()` → `new TileMoveSlotManager(_slotForwardRatio, _slotSideRatio)`.

**핵심 설계 결정**:
- TileMoveSlotManager는 순수 C# 클래스(MonoBehaviour 아님) → [SerializeField] 직접 불가. GameBootstrapper(MonoBehaviour)에 SerializeField 배치 후 생성자로 값 전달.
- 기본값 0.30f 유지 → 기존 동작과 동일, 행동 변화 없음.
- 런타임 중 Inspector 수정은 적용 안 됨(생성 시 1회 주입). 플레이 시작 전 설정 필요.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-05-11/10_49_slot-ratio-inspector/`

---

### UI 종족/팀 초상화 및 생산 연동 시스템 정비 (2026-04-30) ✅ 구현 완료

**수정 파일**: `Presentation/UI/ProductionPanelUI.cs`, `Presentation/UI/BuildingPlacementUI.cs`, `Bootstrap/GameBootstrapper.cs`

**변경 내용**:
- **UI Skinning 로직 제거**: 프로젝트 방향에 따라 배경 색상 변경 등 비주얼 스킨 필드 및 코드를 모두 제거하여 인스펙터를 단순화.
- **데이터 기반 바인딩**: 종족별 데이터 리스트(`UnitPortraitEntry`, `BuildingPortraitEntry`)를 사용하여 버튼에 `UnitType`/`BuildingType`과 스프라이트를 동시 바인딩.
- **생산 타입 동기화 보장**: UI에서 보이는 초상화와 실제 생성되는 프리팹이 1:1로 일치하도록 버튼 클릭 시 리스트에 매핑된 타입을 정확히 전달.
- **비용 텍스트 동적 갱신**: `UnitProductionStats` 및 `BuildingStats`를 참조하여 종족/유닛별 골드 비용을 UI에 실시간 반영.
- **Initialize 정리**: `ProductionPanelUI.Initialize`에서 더 이상 사용하지 않는 `GameConfig` 파라미터 제거.

**핵심 설계 결정**:
- **데이터 우선 원칙**: 복잡한 스킨 시스템보다 플레이어가 선택한 종족의 데이터가 정확히 UI와 게임 플레이(생산)에 반영되는 정합성을 최우선으로 함.
- **인스펙터 최적화**: 불필요한 설정 칸을 줄여 데이터 입력 실수를 방지하고, 향후 종족 추가 시 데이터 리스트만 채우면 되도록 확장성 확보.

---

### Phase 2 후방 스냅 수정 — 7차 개선 Step 4 (2026-04-29) ✅ 구현 완료

**수정 파일**: `Presentation/Unit/UnitView.cs` (Phase 2 영역, 라인 1438~1545)

**변경 1 — Phase 2 forward 타일 우선 선택 (Step 4-A)**:
- `nearestTile == _unitData.Position`(= T0)인 경우, T0의 6방향 인접 타일을 순회하여 forward neighbor(`HexCoord.Distance(neighbor, finalTarget) < currentDist`) 중 현재 위치(domainPos)에서 2D 거리(dx²+dz²)가 가장 가까운 타일을 nearestTile로 교체.
- API: `HexDirectionExtensions.Count` + `((HexDirection)i).Neighbor(origin)` 패턴 사용 (HexMetrics.GetNeighbors 부재).
- walkability 체크 생략 — Phase 0 A* 재계산이 실제 경로를 다시 잡음.
- 폴백: 앞쪽 후보가 없으면 T0 그대로 유지(`bestForward != nearestTile` 조건으로만 교체).

**변경 2 — Phase 2 Lerp 중 적 감지 (Step 4-B)**:
- Phase 2 Lerp while 루프(`Vector3.Lerp(snapStart, tileCenter, t)` 직후)에 적 감지 블록 추가.
- 조건: `HasEnemyInDetectRange && !HasEnemyInRange && snapEnemyIsForward`(Step 2 forward filter 동일 적용).
- forward 판정: `HexCoord.Distance(snapDetectCoord, finalTarget) <= HexCoord.Distance(snapCurrentTile, finalTarget)` (≤ 조건 — 동거리 적은 앞쪽 간주).
- forward 적 감지 시 break → 루프 직후 `transform.position = tileCenter` 강제 스냅 → ProcessStep 정상 실행 → 외부 while로 복귀해 A* 재계산 + Phase 0 첫 감지 체크에서 즉시 Phase 1 재진입.

**핵심 설계 결정**:
- **HexCoord 인접 탐색 패턴**: `HexMetrics.GetNeighbors`는 부재. `HexGrid.GetNeighbors`는 `List<HexTile>` 반환이라 부적합. 순수 좌표 인접 탐색에는 `HexDirectionExtensions.Count` + `((HexDirection)i).Neighbor(coord)`이 표준 패턴.
- **`<=` (동거리 forward 포함)**: Step 2/4-B 모두 동일. 동거리 적은 앞쪽으로 간주해야 잡을 수 있는 적을 놓치지 않음. `>`로 하면 잠재적 누락.
- **forward filter 일관성**: Phase 0 Lerp 중 감지(라인 811), Phase 0 스텝 완료 후 감지(라인 992), Phase 1 최초 타겟(라인 1042), Phase 1 타겟 사망 재선택, Phase 1 전투 종료 재선택, Phase 2 Lerp 중 감지(이번 변경) 모두 동일한 forward 판정 패턴 사용.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-27/01_17_phase2-backward-snap-fix/` (Step 4)

---

### Mesh Y Offset 제거 및 DirectionAngles 수정 (2026-04-29) ✅ 사용자 확인 완료

**수정 파일**: `Presentation/Unit/UnitView.cs`

**변경 내용**:
- `DirectionAngles` 수정: `{0,60,120,180,240,300}` → `{60,120,180,240,300,0}`
  - FlatTop 헥스에서 각 방향의 실제 Unity 월드 각도(atan2 기반)
  - NW(5)=0°: FlatTop NW(Q=0, R-1)의 월드 delta=(x:0, z:+1) → atan2(0,1)=0°
  - 기존 시스템: DirectionAngles + 메시자식Y(30°) = 올바른 월드 각도였음. 메시 자식 제거 후 DirectionAngles가 직접 올바른 값을 담아야 함
- `_meshYOffset` SerializeField 제거
- `CalculateAttackAngle()` 반환에서 `- _meshYOffset` 제거

**핵심 설계 결정**:
- **DirectionAngles 부호 주의**: 메시 자식 Y를 제거할 때 DirectionAngles를 -30°가 아닌 +30° 조정해야 함. 기존값{30,...,330}+30={60,...,0}이 정답. -30°로 적용하면({0,...,300}) 이동 방향과 시각 방향이 60° 어긋남.
- **CalculateAttackAngle 독립성**: DirectionAngles를 사용하지 않고 Atan2 직접 계산 → 이동 방향 변경에 무관. 메시 Y=0이므로 추가 보정값 불필요.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-28/14_30_mesh-offset-cleanup/`

---

### 근접 유닛 뭉침 개선 — 18슬롯 + 슬롯도달후 직진 (2026-04-27) ✅ 구현 완료

**수정 파일**:
- `Application/Services/AttackPositionManager.cs` — 6슬롯 → 18슬롯 재작성. 인접 타일 N개당 (중심 + 좌측경계 + 우측경계) 3위치 생성. 좌/우 경계는 N>=2일 때만. 데이터 구조 `Dictionary<HexCoord, Dictionary<int, HexCoord>>` → `Dictionary<HexCoord, Dictionary<int, Vector3>>` (도메인 좌표 보관). 점유 카운트는 Vector3.Distance < 0.01f로 동등 비교. `_candidateBuffer` 재사용 + `AddCandidateUnique`로 중복 위치 방지.
- `Presentation/Unit/UnitView.cs` (Phase 1 루프 ~Line 1245) — moveTarget 결정에 `reachedSlot` 분기 추가. `Vector2.Distance` 기준 0.15f 이내면 `enemyViewPos`로 전환.

**버그 원인**: 슬롯 위치(0.866f 또는 0.75f)가 전투 사거리(유닛 0.3f / 건물 0.5f)보다 멀어, `moveTarget = _currentAttackPos`로 유지하면 슬롯 도달 시 `dist < 0.01f`에 걸려 유닛이 그대로 멈춤 → `HasEnemyInRange` FALSE → 전투 시작 안 됨.

**핵심 설계 결정**:
- **도메인 좌표로 점유 추적**: `HexMetrics.HexToWorld` 기반 도메인 좌표를 `_assignments`에 보관. 뷰 좌표는 팀별 ViewConverter로 회전될 수 있어 카운트 기준이 흔들리기 때문. `unitViewPos`와의 거리 비교 시점에만 `ToView`로 변환.
- **AddCandidateUnique 중복 방지**: 인접 타일이 6개 미만(맵 가장자리)일 때 같은 좌/우 경계 위치가 두 번 계산될 수 있음. `Vector3.Distance < SamePositionEpsilon(0.01f)`로 중복 흡수.
- **단방향 전환 (reachedSlot)**: 한 번 슬롯 도달 후 `enemyViewPos`로 전환되면 같은 Phase 1 루프 내에서 슬롯으로 되돌아가지 않음 → 진동 방지.
- **Y축 무시 거리 판정**: `Vector2.Distance(transform.position.xz, _currentAttackPos.xz)` — UnitYOffset 차이로 인한 도달 판정 오차 제거.
- **MaxUnitsPerSlot=2 fallback 유지**: 36개 유닛 동시 공격까지 분산 가능. 초과해도 가장 적은 위치로 fallback.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-26/18_30_melee-spread/`

---

### 타일 소유권 실시간 감지 시스템 구현 (2026-04-26) ✅ 구현 완료

**신규 파일**: `Application/Services/TileOwnershipService.cs` — Pull 모델. 매 프레임 모든 살아있는 유닛의 viewPos를 받아 ViewConverter.FromView → HexMetrics.WorldToHex로 헥스 좌표 역산 후 `Dictionary<HexCoord, HashSet<TeamId>>`에 누적. 한 팀만 있는 타일에 한해 `_grid.GetOwner != claimingTeam`일 때만 SetOwner + OnTileOwnerChanged 발행. HashSet 풀(`Queue<HashSet<TeamId>>`)로 GC 최소화.

**수정 파일**:
- `Domain/Hex/HexGrid.cs` — `GetOwner(HexCoord)` 신규. `_tiles.TryGetValue` → `tile.Owner` 또는 Neutral.
- `Bootstrap/GameBootstrapper.cs` — `using Hexiege.Application.Services;`, `_tileOwnership` 필드, `CreateUseCases()`의 `_unitCombat` 직후 인스턴스 생성, `Update()`에 가드 `(!NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer)` 후 `Tick()`.

**핵심 설계 결정**:
- **HexCoord.IsInvalid 부재** → 그리드 경계 검증은 `_grid.HasTile(tile)`로 대체 (TileOccupancyManager의 `IsInvalid`는 (0,0) 약속 기반의 사설 헬퍼이므로 점령 판정에는 부적합 — (0,0)이 일반 타일).
- **점령 규칙**: 한 팀만 있을 때만 갱신, 양 팀 동시면 유지(분쟁지), 비어있으면 유지(점령 영구화). `teams.Count != 1` 분기로 처리.
- **서버 가드**: 싱글(`!IsNetworkActive`) + Host(`IsNetworkServer`) 통과, 순수 Client 차단. 클라이언트는 `_grid.SetOwner` 직접 호출 시 도메인-뷰 불일치 위험 — 별도 동기화 경로(NetworkTileSync 등)로 결과만 수신.
- **이벤트 중복 발행 방지**: `_grid.GetOwner(tile) == claimingTeam`이면 SetOwner/OnNext 모두 생략. 같은 팀이 계속 차지 중인 타일에서 매 프레임 이벤트가 발행되어 HexTileView가 불필요하게 반응하는 것 차단.
- **Application/Services 경로**: 메모리에는 TileOccupancyManager가 Application 직속으로 적혀 있었으나 실제로는 Application/Services에 있음 → 신규 파일도 같은 폴더에 생성.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-26/17_00_tile-ownership-detection/`

---

### 근접 유닛 뒷무빙 수정 5차 개선 (2026-04-26) ✅ 사용자 확인 완료

**수정 파일**: `Presentation/Unit/UnitView.cs` (3곳 수정)

**Step 1 — Phase 1 타겟 사망 시 즉시 재선택**: Phase 1 이동 중 `GetUnitWorldPosition == Vector3.zero`(타겟 파괴) 감지 시 무조건 Phase 2 진입 대신, `_combatUseCase.HasEnemyInDetectRange` + `FindNearestEnemyInDetectRange`로 다음 적 재선택 → 있으면 `continue`(Phase 1 유지), 없으면 `break`(Phase 2 진입).

**Step 2 — 전투 루프 종료 후 다음 타겟 선택**: 전투 종료(`break`) 직후 `HasEnemyInDetectRange` 재확인 → 적 있으면 `FindNearestEnemyInDetectRange`로 타겟 전환 후 `continue`(Phase 1 재개), 없으면 Phase 2 진입.

**Step 3 — Phase 2 후방 스냅 방지**: Phase 2 진입 시 `HexCoord.Distance(nearestTile, finalTarget) > HexCoord.Distance(_unitData.Position, finalTarget)`이면 `nearestTile = _unitData.Position` 유지(후방 스냅 차단). `nearestTile == _unitData.Position`이면 `RegisterOccupancyMove` 생략(점유 누수 방지).

**핵심 설계 결정**:
- **뒷무빙 근본 원인**: Phase 1 타겟 사망 → 무조건 Phase 2 진입 → 현재 물리 위치에서 가장 가까운 타일(=후방일 수 있음)로 스냅.
- **거리 비교 기준**: 월드 거리(float) 대신 `HexCoord.Distance`(도메인 정수 거리) 사용 → 팀 관점(ViewConverter) 무관, 부동소수점 오차 없음.
- **4차 개선 RegisterOccupancyMove 연동**: `nearestTile == _unitData.Position`이면 실제 이동 없음 → `RegisterOccupancyMove` 생략으로 TO+1 중복 방지. FROM-1은 이후 `ProcessStep`에서만 발생하므로 점유 정합성 유지.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-26/15_00_phase1-target-reselect/`

---

### 패스파인딩 4차 개선 — FROM 타일 점유 해제 타이밍 수정 (2026-04-26) ✅ 실기 완료

**수정 파일**:
- `Application/Services/TileOccupancyManager.cs` — `ReserveOccupancy(HexCoord tile, float unitSize)` public 메서드 추가. `Increase(tile, unitSize)` 래퍼. IsInvalid 가드 포함.
- `Application/UseCases/UnitMovementUseCase.cs` — `RegisterOccupancyMove`: `OnUnitMoved(from, to)` → `ReserveOccupancy(to, size)` 변경(TO+1만 예약, from 파라미터 유지). `ProcessStep`: 첫 줄에 `from != to && _occupancyManager != null` 조건으로 `OnUnitRemoved(from, GetOccupancySize(unit.Type))` 추가(Lerp 완료 후 FROM 해제).

**핵심 설계 결정**:
- **FROM 해제 타이밍 분리**: Lerp 시작 전 RegisterOccupancyMove → TO+1만. Lerp 완료 후 ProcessStep → FROM-1. 유닛이 물리적으로 FROM에 있는 동안 FROM 점유가 유지되어 다른 유닛의 잘못된 진입 차단.
- **부가 수정**: death-during-Lerp 이중 해제 버그 동시 해결. FROM은 ProcessStep에서만 감소하므로 사망 시 OnEntityDied → OnUnitRemoved(FROM) 1회만 적용.
- **Phase 2 from==to**: `from != to` 조건으로 Phase 2 스냅(from==to) 시 OnUnitRemoved 미호출. 올바른 동작.
- **스폰(from=default)**: OnUnitRemoved 내부 IsInvalid 체크가 default coord를 skip. 안전.

**실기 결과**:
- 권총병(원거리) 유닛 분산 개선 확인 (PASS)
- 근접 유닛(EmberSpirit) 뭉침은 구조적 한계 — 별도 작업 필요
- 뒷무빙 현상(Phase 1 타겟 재선택 미비) 발견 → `_Tasks/2026-04-26/15_00_phase1-target-reselect/`

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-26/11_00_occupancy-from-fix/`

---

### 패스파인딩 3차 개선 — 뭉침/팅김 해결 (2026-04-25) ✅ 구현 완료

**수정 파일**:
- `Application/Services/TileOccupancyManager.cs` — `FindAvailableTile(preferred, size, grid, destination)` 오버로드 추가. forward 필터 BFS: `Distance(candidate, destination) <= Distance(preferred, destination) + 1` 조건 충족 타일만 반환. fallback으로 필터 없이 재BFS. 기존 단일 파라미터 오버로드는 default destination 위임으로 유지.
- `Application/UseCases/UnitMovementUseCase.cs` — `ProcessStep`에서 `_occupancyManager.OnUnitMoved` 호출 제거(도메인 로직만 담당). `RegisterOccupancyMove(from, to, type)` 신규 추가(Lerp 시작 직전 호출용). `ReleaseOccupancy(tile, type)` 신규 추가(중단 경로 누수 방지). `FindAvailableTile(preferred, size, destination)` 오버로드 추가.
- `Presentation/Unit/UnitView.cs` — `_pendingOccupancyTile` 필드 추가(default = 미등록). `ReleaseOccupancyIfPending()` 헬퍼 추가. Phase 0 루프 진입 전 `prevActualTile = _unitData.Position` 초기화. for 루프 내 `from = prevActualTile`로 변경(기존 `path[i-1]` 폐기). FindAvailableTile에 `finalTarget` 전달. Lerp 시작 직전 RegisterOccupancyMove 호출. 정상 도착 시 `prevActualTile = to; _pendingOccupancyTile = default;` 갱신. 우회 발생(`actualTo != to`) 시 `detouredNeedsRepath = true` + for break → 외부 while에서 RequestMove 재호출 후 continue. interruptedByDetect/StopMovement/사망 핸들러에 `ReleaseOccupancyIfPending()` 추가. Phase 2 스냅 후 `RegisterOccupancyMove(_unitData.Position, nearestTile, type)` 명시 호출.

**핵심 설계 결정**:
- **점유 갱신 타이밍**: ProcessStep(Lerp 후) → Lerp 시작 직전. 같은 프레임 내 다른 유닛이 즉시 "이 타일 차 있음" 인식 → Race Condition 해결.
- **prevActualTile 추적**: 우회 발생 시에도 `from`이 항상 실제 이전 도착 타일을 가리켜 OnUnitMoved의 from 감소가 올바른 타일에 적용됨.
- **우회 시 즉시 re-path**: 원래 path는 actualTo와 무관하므로 그대로 이어가면 측면/후방 지그재그(팅김) 발생. for break + RequestMove로 현재 위치 기준 새 플로우 필드 경로 받음.
- **forward 필터 +1 여유**: 헥스 그리드 특성상 측면 타일이 같은 거리이거나 +1이 될 수 있어 너무 엄격하면 모든 측면 차단됨. fallback BFS로 극단 상황도 처리.
- **_pendingOccupancyTile = default 약속**: HexCoord(0,0)이 일반 타일일 수 있지만 기존 `TileOccupancyManager.IsInvalid` 약속과 동일하게 "미등록" 의미로만 사용.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-25/10_05_pathfinding-improvement/`

---

### 유닛/건물 스탯 ScriptableObject 전환 (2026-04-25) ✅ 구현 완료

**신규 파일**:
- `Infrastructure/Config/UnitStatsConfig.cs` — `UnitStatEntry` 구조체(전투+생산 스탯 통합) + `UnitStatsConfig : ScriptableObject`
- `Infrastructure/Config/BuildingStatsConfig.cs` — `BuildingTypeEntry` 구조체(B방식: 건물타입별 3종족 값 묶음) + `BuildingStatsConfig : ScriptableObject`
- `Editor/SetupUnitStatsConfig.cs` — 메뉴: `Hexiege/Setup/UnitStatsConfig 생성`. 9종 유닛 기본값 자동 주입.
- `Editor/SetupBuildingStatsConfig.cs` — 메뉴: `Hexiege/Setup/BuildingStatsConfig 생성`. Castle/Barracks/MiningPost 기본값 자동 주입.

**수정 파일**:
- `Domain/Unit/UnitStats.cs` — switch 표현식 → `Dictionary<UnitType, StatValues>`. `Initialize(IReadOnlyDictionary<UnitType, StatValues>)` 추가. miss → 폴백 반환.
- `Domain/Unit/UnitProductionStats.cs` — 동일 패턴. `Dictionary<UnitType, ProductionValues>`, `Initialize()` 추가.
- `Domain/Building/BuildingStats.cs` — switch 표현식 → `Dictionary<(BuildingType, RaceId), StatValues>`, `Initialize()` 추가. `GetGoldCost(type, race)`, `GetAttackPower(type, race)` 신규 메서드.
- `Bootstrap/GameBootstrapper.cs` — `[SerializeField] _unitStatsConfig`, `[SerializeField] _buildingStatsConfig` 추가. `InitializeUnitStatsFromConfig()`, `InitializeBuildingStatsFromConfig()` 메서드 추가.
- `Presentation/UI/BuildingPlacementUI.cs` — `GetBuildingCost()` → `BuildingStats.GetGoldCost(type, race)` 사용으로 변경.

**에셋 경로**: `Assets/_Project/Resources/Config/UnitStatsConfig.asset`, `Assets/_Project/Resources/Config/BuildingStatsConfig.asset`

**핵심 설계 결정**:
- Domain 순수성 유지: Domain 내부 C# 구조체(`StatValues`, `ProductionValues`)를 직접 정의. Infrastructure → Domain 의존 없음.
- GameBootstrapper가 SO → Domain 구조체 변환 담당 (단일 책임).
- Play Mode 중 SO 수정 → Dictionary는 Start() 복사본이므로 다음 Play Mode 진입까지 미반영 (의도된 동작).
- `GameConfig.BarracksCost/MiningPostCost` 필드는 유지 (참조 제거 최소화).

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-25/01_35_unit-stats-scriptableobject/`

---

### 싱글플레이 AI 종족 랜덤 결정 (2026-04-24) ✅ 실기 완료

**수정 파일**:
- `Bootstrap/GameBootstrapper.cs` — 283번째 줄 `GameRaceContext.Set(LocalPlayerRace.Current, RaceId.Human)` → `Enum.GetValues` + `Random.Range`로 교체
- `Presentation/UI/ViewModels/BattleViewModel.cs` — `LoadSingleplayScene()`에서 중복된 `GameRaceContext.Set()` 호출 및 주석 제거

**핵심 설계 결정**:
- `(RaceId[])System.Enum.GetValues(typeof(RaceId))` 패턴 — 새 종족 추가 시 자동으로 랜덤 풀에 포함
- `GameRaceContext` 설정 책임은 `GameBootstrapper.cs` 단독 (BattleViewModel 이중 설정 제거)
- `LoadMap()` 이전 설정 순서 유지

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-24/23_06_random-opponent-race/`

---

### 다중 히트 데미지 구현 (2026-04-24) ✅ 실기 완료

**수정 파일**:
- `Domain/Unit/UnitStats.cs` — `GetHitFrameTime()` 제거 → `GetHitFrameTimes()` 추가 (반환형 `float[]`), LionKnight AttackCooldown 2.33f → 3.0f 수정
- `Domain/Unit/UnitData.cs` — `HitFrameTime: float` → `HitFrameTimes: float[]` 교체 (생성자 2개 모두)
- `Application/UseCases/UnitCombatUseCase.cs` — `PendingHit` struct + `_pendingHits` List + `TickPendingHits(float dt)` 추가. `TryAttack()`에서 각 히트 프레임마다 PendingHit enqueue, 쿨다운 리셋은 TryAttack에서 1회만.
- `Infrastructure/Network/NetworkCombatController.cs` — `ExecuteAttack()`에서 `HitFrameTimes` foreach로 `DelayedAttackDamage` 코루틴 N개 실행
- `Bootstrap/GameBootstrapper.cs` — `Update()`에 `_unitCombat.TickPendingHits(Time.deltaTime)` 추가

**핵심 설계 결정**:
- 쿨다운은 공격 사이클 시작 시 1회만 리셋 — 히트 횟수와 무관
- 싱글플레이: MonoBehaviour 아님 → 코루틴 불가 → `_pendingHits` 타이머 리스트 방식 (TickCooldowns와 동일 패턴)
- 멀티플레이: `DelayedAttackDamage` 코루틴을 히트 수만큼 병렬 실행
- 타겟 사망 시 잔여 히트 자동 취소 — `ApplyAttackDamage` 내 `IsAlive` 체크로 처리
- `ApplyAttackDamage()`에서 쿨다운 리셋 제거 (다중 히트 시 마지막 히트에서 재리셋 방지)

**다중 히트 유닛 타이밍 (StatsReference.md 기준, 30fps)**:
- FlameSpirit (6히트, 쿨다운 3.0s): 0.667 / 1.167 / 1.433 / 1.667 / 1.933 / 2.100s
- LionKnight (2히트, 쿨다운 3.0s): 0.733 / 1.267s

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-24/16_31_multi-hit-damage/`

---

### 근접유닛 추적 중 회전 개선 (2026-04-24) ✅ 실기 완료

**수정 파일**:
- `Presentation/Unit/UnitView.cs` — Phase 1 직선 이동 블록 (850~866 라인) 회전 로직 추가

**문제**: Phase 1(월드 좌표 직선 추적) 중 `transform.rotation` 업데이트 없음 → 이전 타일 이동 방향 회전 고정.

**수정**: `if (dist > 0.01f)` 블록 내 이동 전에 `CalculateAttackAngle(enemyViewPos)` + `Quaternion.RotateTowards(CombatRotationSpeed * deltaTime)` 추가.
전투 중 타겟 추적 회전(`Update()`)과 동일한 패턴 사용.

**멀티플레이**: `MoveAlongPath` 코루틴 가드(`NetworkContext.IsNetworkActive && !IsNetworkServer`)로 서버만 실행 → NetworkTransform이 클라이언트에 보간 전달.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-24/15_45_melee-pursuit-rotation/`

---

### 랠리포인트 Client 무시 버그 수정 (2026-04-19) ✅ 실기 완료

**수정 파일**:
- `Infrastructure/Network/NetworkProductionController.cs` — `SetRallyPointServerRpc` 신규 추가 (약 695~738행)
- `Presentation/UI/ProductionPanelUI.cs` — `CompleteRallyPointSetting()` 네트워크 분기 추가

**버그**: 멀티플레이 Client(Red팀)에서 랠리포인트를 설정해도 생산된 유닛이 랠리포인트를 무시하고 이동.

**원인**: `CompleteRallyPointSetting()`이 `_production.SetRallyPoint()`를 직접 호출 → 클라이언트 로컬 `ProductionState`만 갱신. 서버의 `state.RallyPoint`는 null → `SpawnUnitClientRpc`에 `hasRally=false` 전송.

**수정**:
- `SetRallyPointServerRpc(barracksId, q, r, teamIndex)` 추가 — 기존 ServerRpc 패턴 그대로 (팀 소유권 검증 → `production.SetRallyPoint()`)
- `CompleteRallyPointSetting()`에 네트워크 분기 추가:
  - 네트워크 모드: `SetRallyPointServerRpc` 호출(서버 반영) + 로컬 `_production.SetRallyPoint()`(마커 표시)
  - 싱글/Host: 기존대로 직접 호출
- ClientRpc 불필요: 서버 생산 완료 시 `state.RallyPoint`를 읽어 `SpawnUnitClientRpc`로 전달되므로 서버 상태만 정확하면 충분

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-19/18_54_rally-point-ignored/`

---

### 생산 슬롯 깜빡임 버그 수정 (2026-04-19) ✅ 싱글 실기 완료

**수정 파일**: `Application/UseCases/UnitProductionUseCase.cs` — `ToggleAutoProduction()` 284~288행

**버그**: 큐가 완전히 비어있을 때 자동 생산 타입을 등록하면 1프레임 동안 슬롯1에 표시됐다가 슬롯0으로 이동하는 깜빡임 발생.

**원인**: `canShow = CurrentProducing.HasValue && ChargedPendingCount() < 2` 조건에서 큐가 비어있으면 `HasValue=false` → `canShow=false` → 아이템이 `PendingQueue[0]`(슬롯1)에 미차감 추가. 다음 Tick의 `TryStartNext`가 슬롯0으로 올리기 때문에 1프레임 지연 발생.

**수정**: `PendingQueue.Add + AutoTypes.Add + NormalizeAutoCycleIndex` 이후, `!state.CurrentProducing.HasValue`이면 즉시 `TryStartNext(state)` 호출 후 Early Return. TryStartNext 내부에서 이벤트 발행 처리.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-19/17_49_production-slot-flicker/`

---

### 타겟 고정(Target Lock) 데미지 불일치 버그 수정 (2026-04-18) ✅ 멀티 실기 완료

**수정 파일**: `Infrastructure/Network/NetworkCombatController.cs` — `TickCombat()` 253~297행

**버그**: 유닛 A가 B를 공격 중 더 가까운 C가 접근 시, 애니메이션은 B를 바라보지만 데미지가 C에게 적용되는 문제.

**원인**: `IsCurrentTargetStillValid(B) = true` → `_unitCombatTargets` 미변경(애니메이션 B 유지) 했으나, `ExecuteAttack`은 항상 `TryFindTarget`이 반환한 `targetId`(C)를 사용.

**수정**: `damageTargetId` / `damageTargetIsUnit` 지역 변수 추가.
- `IsCurrentTargetStillValid = true` → `else` 분기: `damageTargetId = prev.targetId` (기존 타겟 B 유지)
- `IsCurrentTargetStillValid = false` → 기존 흐름 유지 (새 타겟 C로 교체 + RPC 전송)
- `ExecuteAttack(unit, damageTargetId, damageTargetIsUnit)` 호출

**교훈**: Target Lock에서 애니메이션 타겟(`_unitCombatTargets`)과 데미지 타겟(`targetId`)은 항상 일치해야 함. `IsCurrentTargetStillValid` 가드로 애니메이션을 유지한다면, 데미지도 같은 타겟에게 적용해야 함.

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-17/22_29_target-lock-damage-bug/`

---

### 피격 시 부유 HP 텍스트 (2026-04-12~13, 2026-04-17 World Space 전환) ✅ 싱글/멀티 실기 완료

**신규 파일**:
- `Presentation/UI/Common/FloatingHpText.cs` — 단일 부유 텍스트. TextMeshPro(3D World Space). DOTween Sequence(LocalMoveY OutCubic + TMP DOFade 동시, duration초). Play(text, worldPosition, scale=1f, color). OnComplete → SetActive(false) + 풀 반환 콜백. OnDestroy → Kill.
- `Presentation/UI/FloatingHpTextSpawner.cs` — GameEvents.OnEntityDamaged 구독(AddTo). Queue<FloatingHpText> 풀 10개 사전 생성. Initialize(positionProvider, container, prefab) — null 체크 포함. 팀별 색상: `[SerializeField] Color _blueTeamColor` / `_redTeamColor`. evt.Entity.Team switch → Play()에 전달.
- `Prefabs/UI/FloatingHpText.prefab` — SetupFloatingHpText 에디터 스크립트로 자동 생성.
- `Editor/SetupFloatingHpText.cs` — 프리팹 생성 + 씬 배치 + GameBootstrapper 슬롯 자동 연결. 메뉴: `Hexiege/Setup/FloatingHpText 설정`

**변경 파일**:
- `Bootstrap/GameBootstrapper.cs` — `_floatingHpTextSpawner`, `_floatingHpTextPrefab`, `_floatingTextContainer(Transform)` SerializedField 추가. `_positionProvider` 로컬→필드 승격. `LoadMap()`에서 Initialize 호출.
- `Infrastructure/Network/NetworkHealthSync.cs` — `SyncUnitHealth`/`SyncBuildingHealth`에서 TakeDamage 후 `GameEvents.OnEntityDamaged.OnNext()` 재발행 (클라이언트에서 FloatingHpTextSpawner가 반응하도록).

**Inspector 설정값 (FloatingHpText 프리팹)**:
- `Rise Distance` (default=0.5f): 위로 이동 거리 (월드 단위, 픽셀 아님)
- `Duration` (default=1.2f): 전체 애니메이션 시간(초)
- **폰트 크기**: TMP 컴포넌트(자식 Text)에서 직접 수정
- **Material Preset**: 반드시 독립 .mat 파일(`Maplestory Light SDF FloatingHpText Material.mat`) 지정 — 폰트 에셋 내장 sub-asset 지정 시 Outline 등 편집이 .asset 파일 자체를 오염시킴

**Inspector 설정값 (FloatingHpTextSpawner)**:
- `Y Offset` (default=1.2f): 피격 오브젝트 머리 위 월드 Y 오프셋

**핵심 설계 결정**:
- **World Space TextMeshPro**: Screen Space Canvas 전환. 월드 좌표 직접 사용 → 좌표 변환 코드 없음.
- **scale = 1f 고정**: `orthoSize/referenceSize` 수식 폐기. 줌아웃 시 유닛은 작아지는데 텍스트만 커지는 비율 어긋남 방지. 텍스트가 다른 월드 오브젝트와 동일하게 줌 비례 동작.
- **빌보드 회전**: `Quaternion.LookRotation(-Camera.main.transform.forward, Camera.main.transform.up)`. 카메라 forward를 그대로 쓰면 텍스트가 카메라에 등을 보임.
- **좌우 반전 보정**: `LookRotation(-forward, up)`은 텍스트 로컬 X축을 -cameraRight로 만들어 텍스트가 좌우 반전됨. `localScale = new Vector3(-s, s, s)` (X 음수)로 한번 더 뒤집어 복원. TMP 3D 기본 머티리얼이 Cull Off(양면 렌더링)이므로 음수 스케일 정상 표시.
- **클라이언트 이벤트 재발행**: NetworkHealthSync에서 diff>0인 경우에만 → HP 이미 동기화 시 중복 표시 없음
- **팀 색상 (기본값)**: Blue=연두(120,230,80), Red=노랑(255,220,30) — Inspector 조정 가능

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-12/18_03_floating-hp-text/` (초기), `Assets/_Project/Docs/_Tasks/2026-04-13/17_50_floating-text-worldspace/` (World Space 전환)

---

### 유닛/건물 스탯 적용 + UI 골드 비용 표기 (2026-04-12~13) ✅ 싱글/멀티 실기 완료

**변경 파일**:
- `Domain/Unit/UnitStats.cs` — Pistoleer MoveSpeed 1.0→0.5, Spirit/Transcendence 6종 HP/ATK 확정값 적용
- `Domain/Unit/UnitProductionStats.cs` — Spirit/Transcendence 6종 생산시간/비용 확정값 적용
- `Domain/Building/BuildingStats.cs` — `GetMaxHp(type, RaceId race)` 오버로드 추가. Transcendence: Castle=200/Barracks=50/MiningPost=40, 나머지: 100/30/20. 단일 파라미터 버전은 `RaceId.Human`으로 위임.
- `Application/UseCases/BuildingPlacementUseCase.cs` — `PlaceBuilding`/`PlaceMiningPost`/`PlaceMiningPostDirect`/`PlaceBuildingWithId`/`PlaceBuildingInternal`에 `RaceId race = RaceId.Human` 파라미터 추가. Application 레이어 위반 없음 (GameRaceContext 직접 참조 없음).
- `Bootstrap/GameBootstrapper.cs` — Castle/mine 배치에 `GameRaceContext.BlueRace`/`RedRace` 전달.
- `Infrastructure/Network/NetworkBuildingController.cs` — ServerRpc/ClientRpc에 race 전달.
- `Presentation/UI/BuildingPlacementUI.cs` — HP 텍스트 필드 제거. `_barracksCostText`/`_miningPostCostText` 추가. 골드 숫자만 표시(G 없음).
- `Presentation/UI/ProductionPanelUI.cs` — `_slot1/2/3CostText` 추가. Spirit 슬롯 순서 확정(EmberSpirit→FlameSpirit→InfernoSpirit). 골드 숫자만 표시.
- `Editor/SetupStatCostTexts.cs` (신규) — 기존 GoldText 오브젝트를 SerializedField에 자동 연결. 메뉴: `Hexiege/Setup/스탯 비용 텍스트 연결`

**핵심 설계 결정**:
- Transcendence 건물 HP는 RaceId 파라미터로 분기 — UnitCombatUseCase/BuildingData에 Race 필드 추가하지 않음
- Application 레이어에 GameRaceContext 참조 없음 (호출자에서 race 파라미터로 전달)
- UI 골드 비용: 숫자만, "G" 없음

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-12/06_42_stats-apply/`

---

### 건물/유닛 초상화 종족+팀 기반 표시 (2026-04-12) ✅ 실기 완료

**변경 파일**:
- `Presentation/UI/BuildingPlacementUI.cs` — `BuildingPortraitSet` → `BuildingRacePortraitSet`(barracks+miningPost 필드)으로 교체, Inspector 팀×종족 6세트 필드 추가, `UpdateButtonPortraits()`에 `GameRaceContext` 조회 추가, `GetBuildingPortraitSet()` 신규 메서드
- `Presentation/UI/ProductionPanelUI.cs` — `BindButtonUnitTypes()` 슬롯 순서 변경 (Spirit: EmberSpirit→FlameSpirit→InfernoSpirit / Transcendence: FoxMagician→BearGuard→LionKnight)

**핵심 설계 결정**:
- `BuildingRacePortraitSet` 필드명은 BuildingType(barracks/miningPost) 기준 — 종족별 외형명(SummoningAltar 등) 아님. UpdateButtonPortraits에서 종족 무관하게 `set.barracks`로 통일 접근 가능
- `ProductionPanelUI.GetPortraitSet()` 패턴과 동일하게 팀×종족 switch 6분기
- `GameRaceContext`(Infrastructure 정적 홀더)는 Presentation에서 참조 허용 — 레이어 위반 없음

**Inspector 연결 확정 슬롯 순서**:
- Spirit: slot1=EmberSpirit, slot2=FlameSpirit, slot3=InfernoSpirit
- Transcendence: slot1=FoxMagician, slot2=BearGuard, slot3=LionKnight

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-11/building-portrait-race-support/`

### 원거리 유닛 공격 중 회전 추적 + 폴리싱 (2026-04-11~12) ✅ 실기 완료

**변경 파일**:
- `Presentation/Unit/UnitView.cs` — `_combatTargetTransform` Transform 참조 저장 + `Update()` RotateTowards(270°/s) + 방어적 백업 ID 필드(`_combatTargetId`, `_combatTargetIsUnit`) + `ChangeTarget()` 즉시 스냅 제거
- `Application/UseCases/UnitCombatUseCase.cs` — `IsCurrentTargetStillValid(attacker, targetId, targetIsUnit)` public 메서드 추가 (내부적으로 `FindTargetById` + `IsTargetInRange` 조합)
- `Infrastructure/Network/NetworkCombatController.cs` — `TickCombat` 타겟 교체 2곳에 `IsCurrentTargetStillValid` 가드 추가

**핵심 설계 결정**:
- Transform 참조 직접 저장 → 팩토리 딕셔너리 매 프레임 조회 없음
- 서버에서만 rotation 갱신 — 클라이언트 가드: `NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer`
- RotateTowards(270°/s): DORotate 폐기 이유(이중 보간)와 달리 서버가 직접 값을 변경하므로 NetworkTransform 딜레이만 발생
- `StartCombatAnimation()` 즉시 스냅 유지, `ChangeTarget()` 즉시 스냅 제거 — Update()가 자연스럽게 전환
- 타겟 고착성: 현재 타겟 생존+사거리 내이면 더 가까운 새 유닛이 진입해도 교체 안 함

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-11/ranged-unit-rotation-tracking/` (TC MULTI-001~007 전체 PASS)

### 근접 공격 거리 다듬기 (2026-04-11) ✅ 실기 완료

**변경 파일**:
- `Application/UseCases/UnitCombatUseCase.cs` — `MeleeContactDist = 0.3f`, `BuildingDetectionRadius = 0.2f` 상수 추가. `FindFirstEnemyTarget`에서 `unitMaxDist`/`buildingMaxDist` 분리. `IsTargetInRange`에서 동일 분기 적용.

**핵심 설계 결정**:
- 근접(range < 1.0) vs 유닛: 0.35f / vs 건물: 0.55f
- 원거리(range ≥ 1.0): 기존 `AttackRange * TileHeight + Epsilon` 유지
- `isMelee = attacker.AttackRange < 1.0f` 분기로 완전 보호

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-11/melee-attack-distance/`

### UnitType 개편 + 근접 사거리 시스템 (2026-04-10~11) ✅ 싱글/멀티 실기 완료

**변경 파일**:
- `Domain/Unit/UnitType.cs` — Pistoleer=0~LionKnight=8, 9종 유닛 독립 enum
- `Domain/Unit/UnitStats.cs` — Spirit/Transcendence 6종 스탯 추가 (HP/ATK 미정, Range/Cooldown/HitFrameTime 확정)
- `Domain/Hex/HexPathfinder.cs` — `FindPathToNeighbor()` 추가: goal의 인접 walkable 타일 중 start에서 가장 가까운 타일까지 경로 반환
- `Infrastructure/Factories/UnitFactory.cs` — `UnitTeamPrefabSet` → `List<UnitPrefabEntry>(type, blue, red)` 구조로 변경
- `Application/UseCases/UnitMovementUseCase.cs` — RequestMove에 non-walkable 목표 처리 추가, `path.Count >= 1` 조건
- `Presentation/Unit/UnitView.cs` — 마지막 non-walkable 타일: ProcessStep 생략 + ClaimedTile 설정 생략
- `Presentation/UI/ProductionPanelUI.cs` — 종족별 UnitType 동적 바인딩 (`BindButtonUnitTypes`), 6세트 초상화 필드
- `Editor/SetupUnitFactoryPrefabs.cs` — List<UnitPrefabEntry> 구조에 맞게 재작성

**핵심 설계 결정**:
- 근접 유닛(range=0.5): maxDist = 0.483f 유지 + 경로에 Castle 타일 추가 → Lerp 이동 연장으로 접근
- **ClaimedTile non-walkable 타일 예외**: 마지막 타일이 non-walkable이면 ClaimedTile 설정 안 함 — 설정 시 공격 루프 내내 Castle이 blocked로 유지되어 후속 유닛 접근 차단
- `FindPathToNeighbor` start==bestCandidate → count=1 반환 → `>= 1` 조건으로 Castle 타일 추가 보장

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-10/16_09_melee-unit-attack-range/`

### 중립 광산 오브젝트 표시 제어 (2026-04-08) ✅ 싱글 실기 완료

**변경 파일**:
- `Presentation/Grid/HexGridRenderer.cs` — `_goldMineObjects` List→Dictionary, `RenderGoldMines()` 초기 숨김, `HideGoldMine()`/`ShowGoldMine()` 추가, `SubscribeGoldMineEvents()` 추가
- `Application/UseCases/BuildingPlacementUseCase.cs` — `RemoveBuilding()` 내 MiningPost 파괴 시 타일 Owner Neutral 복원 + OnTileOwnerChanged 발행

**핵심 설계 결정**:
- 초기 숨김 판별: `RenderGoldMines()` 내 `tile.Owner != TeamId.Neutral` 조건 (PlaceMiningPostDirect 이후 호출 순서 보장)
- 이벤트 구독: `OnBuildingPlaced` → HideGoldMine / `OnEntityDied(MiningPost)` → ShowGoldMine
- 타일 소유권 복원: `RemoveBuilding()`에서 처리 — 싱글(UnitCombatUseCase)/멀티(NetworkCombatController) 모두 이 메서드를 거치므로 단일 수정으로 양쪽 커버

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-07/23_45_goldmine-hide/`

### 종족 인게임 적용 (2026-04-07) ✅ 싱글/멀티 실기 완료

**변경 파일**:
- `Infrastructure/Factories/UnitFactory.cs` — 종족별 6세트 프리팹(`_humanBlue/Red`, `_spiritBlue/Red`, `_transcendenceBlue/Red`), GameRaceContext 조회 후 switch 선택, 오브젝트명=`{prefab.name}_{id}`
- `Infrastructure/Factories/BuildingFactory.cs` — 동일 종족별 6세트 패턴, BuildingTeamPrefabSet에 `miningPost` 필드 추가
- `Bootstrap/GameBootstrapper.cs` — 싱글플레이 Start()에 `GameRaceContext.Set(LocalPlayerRace.Current, RaceId.Human)` 추가
- `Editor/SetupUnitFactoryPrefabs.cs` (신규) — 유닛 18개 + 건물 12개 자동 프리팹 연결 에디터 메뉴

**핵심 설계 결정**:
- GameRaceContext(Infrastructure 정적 홀더)를 UnitFactory/BuildingFactory에서 직접 참조 — 레이어 위반 없음
- UnitData에 Race 필드 추가하지 않음 — 스폰 시점에 GameRaceContext에서 직접 조회
- MiningPost: BuildingTeamPrefabSet.miningPost 필드로 종족별 분기

**건물 종족 매핑 (확정)**:
| BuildingType | Human | Spirit | Transcendence |
|---|---|---|---|
| Castle | Building_Castle | Building_SpiritNexus | Building_ElderTree |
| Barracks | Building_Barracks | Building_SummoningAltar | Building_HunterPlant |
| MiningPost | Building_MiningPost | Building_ManaRift | Building_FungalNode |

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-07/09_00_faction-ingame-apply/`

### 전투 애니메이션 시스템 전면 재정비 (2026-04-03~04) ✅ 완료

**핵심 변경 파일**:
- `NetworkCombatController.cs` — 3-신호 RPC, TickCombat elapsed 수정, _combatAnimationSent, ExecuteAttack 동시 호출
- `UnitView.cs` — Walk CrossFade 1회 제한, _attackToWalkBlend, StopCombatAnimation 빈 메서드
- `NetworkUnit.cs` — WaitForUnitId 폴링 → OnValueChanged 콜백 교체
- `UnitCombatUseCase.cs` — TryAttack 네트워크 완전 차단 (HOST 이중 데미지 방지)
- `UnitStats.cs` — GetAttackCooldown 실제 클립 길이로 업데이트 (Assault=0.2, Pistoleer=2.0, Sniper=3.0)

**핵심 설계 결정**:
- StartCombatClientRpc: OnUnitEnteredCombatHandler 단독 전송 (TickCombat에서 제거)
- AttackCooldown = 클립 길이 — Animator 상태 읽기 없이 순수 타이머로 사이클 판단
- StopCombatAnimation() = 빈 메서드 — Walk는 StartWalkAnimationClientRpc 타이밍에만 전환
- `_combatAnimationSent` HashSet — TickCombat/코루틴 실행 순서 경쟁 조건 방지용 RPC 전송 추적

**버그 패턴 교훈**:
- TickCombat(Update)은 코루틴(yield return null)보다 먼저 실행 → 같은 프레임에 Dictionary 먼저 등록 가능
- RPC 전송 여부 추적은 타겟 추적 Dictionary와 반드시 분리
- ExecuteAttack을 핸들러에서 즉시 호출해야 서버 공격 사이클 T=0 = 애니메이션 루프 T=0 동기화

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-03/10_00_combat-animation-overhaul/`

### 유닛 NGO NetworkObject 전환 + 이동/전투/회전 동기화 (2026-03-26~29) ✅ 완료

**핵심 설계 결정 (2026-03-29 최종)**:
- 유닛 위치 동기화: NGO NetworkTransform (서버 position → 클라이언트 자동 보간)
- **유닛 회전 동기화: NetworkTransform SyncRotAngleY=true (서버 즉시 스냅 → 클라이언트 보간)**
- Walk/공격/사망 동기화: ClientRpc (이벤트 기반)
- Red 클라이언트 좌표+회전 보정: NetworkUnit.LateUpdate() (위치 반전 + Y축 +180°)
- NGO NetworkObject 부모 제약: 씬 루트에 생성 (일반 GameObject 하위 불가)
- 클라이언트 등록 타이밍: WaitForUnitId 폴링 + ApplyStartWalkWithRetry로 등록 지연 대응

**폐기된 패턴 (2026-03-29)**:
- ~~클라이언트 LateUpdate 델타 기반 회전 (Atan2 + RotateTowards)~~ → NetworkTransform rotation 동기화로 대체
- ~~TurnToFaceClientRpc + DORotate 보간~~ → 서버 즉시 스냅 + NetworkTransform 보간으로 대체
- ~~_isPreRotating / SetPreRotating / SetAttackRotating~~ → 전면 제거
- ~~_isWalkPending~~ → 공격 중 Walk 무시 가드(`if (_attackCoroutine != null) return`)로 교체
- ~~HasReceivedTurnToFace / MarkTurnToFaceReceived~~ → 전면 제거
- ~~ResetMovementTracking / ResetPositionTracking~~ → 전면 제거
- ~~UnitView의 DOKill/DORotate~~ → Quaternion.Euler 즉시 스냅으로 교체 (using DG.Tweening 제거)
- ~~GameEvents.OnUnitFacingChanged / UnitFacingChangedEvent~~ → 전면 제거
- ~~NetworkCombatController.TurnToFaceClientRpc~~ → 전면 제거

**이중 보간 문제 교훈**:
서버 DORotate(0.3초) + NetworkTransform 보간(0.1초) = ~1초 딜레이.
서버에서 즉시 스냅하면 NetworkTransform 보간만 적용되어 자연스러운 회전.

### 공격 타이밍 정밀화 (2026-03-27) ✅ 실기 테스트 완료

**구현 내용**:
- **타격 프레임 데미지**: 서버가 애니메이션 RPC 즉시 전송 → HitFrameTime 후 데미지 적용
- **타겟 고정(Target Lock)**: ApplyAttackDamage에서 IsInRange 체크 제거 — 공격 모션 시작 시 타겟 확정
- **쿨다운 통일**: UnitView.Update() 쿨다운 제거 → GameBootstrapper.Update() → TickCooldowns()

**신규 메서드 (UnitCombatUseCase)**:
- `TryFindTarget(UnitData)`: 타겟 탐색만, 데미지/쿨다운 없음 (멀티플레이 서버용)
- `ApplyAttackDamage(UnitData, int, bool)`: 딜레이 후 호출, IsAlive만 재확인 (IsInRange 없음)
- `TickCooldowns(float dt)`: 싱글플레이 전용 일괄 쿨다운 감소
- `FindTargetById(int, bool)`: Id로 Units/Buildings Dictionary 탐색

**HitFrameTime 값 (UnitStats.GetHitFrameTime)**:
- Assault: 0.133f (0:04, 4프레임/30fps)
- Pistoleer: 0.833f (0:25, 25프레임/30fps)
- Sniper: 2.000f (2:00)

**NetworkCombatController.TickCombat() 변경**:
- TryAttack() → TryFindTarget() 교체
- 성공 시: RPC 즉시 전송 + 쿨다운 리셋 + DelayedAttackDamage 코루틴 시작

**DelayedAttackDamage 코루틴**:
- HitFrameTime > 0: WaitForSeconds(delay)
- HitFrameTime = 0: yield return null (최소 1프레임 안전망)
- 이후 ApplyAttackDamage() 호출

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-03-27/11_00_attack-timing-precision/`

### 이동 전 회전 타이밍 수정 (2026-03-27) ✅ 실기 테스트 완료

**문제**: DOTween(Update) vs NetworkUnit.LateUpdate 충돌 — LateUpdate가 매 프레임 DOTween rotation을 덮어씌워 프리-회전 무효화
**해결**: `_isPreRotating` 플래그로 DORotate 실행 중 LateUpdate 델타 회전 차단

**수정 파일**:
- `Infrastructure/Network/NetworkUnit.cs`:
  - `_isPreRotating` (bool) 필드 추가
  - `SetPreRotating(bool)` public 메서드 추가
  - `ResetMovementTracking()`에 `_isPreRotating = false` 안전망 추가 (DOKill 중단 시 플래그 고착 방지)
  - LateUpdate 델타 회전 조건: `if (!_isPreRotating && _hasInitialPosition)`
- `Infrastructure/Network/NetworkCombatController.cs`:
  - `TurnToFaceClientRpc`에 `networkUnit?.SetPreRotating(true)` 추가
  - DORotate에 `.OnComplete(() => networkUnit?.SetPreRotating(false))` 추가

**핵심 패턴**: DOTween이 활성 중일 때 LateUpdate rotation 차단이 필요하면 `_isPreRotating` 패턴 사용

### Game UI Lifecycle Framework (2026-03-24) ✅ 실기 테스트 완료

**신규 파일**:
- `Presentation/UI/Core/IGameUI.cs` — UI 생명주기 인터페이스 (OnGameStarted/OnGameEnded/OnGamePaused/OnGameResumed, 모두 default 빈 구현)
- `Presentation/UI/GameUIManager.cs` — 등록/디스패치 매니저 (MonoBehaviour, [Managers] 하위 배치)

**수정 파일**:
- `Application/Events/GameEvents.cs` — OnGameStarted, OnGamePaused, OnGameResumed Subject<Unit> 추가
- `GameHudUI.cs` / `ProductionPanelUI.cs` / `BuildingPlacementUI.cs` / `GameEndUI.cs` — IGameUI 구현
- `GameBootstrapper.cs` — `_uiManager` SerializeField + LoadMap() 맨 앞에 Register/Initialize, 맨 끝에 OnGameStarted 발행
- `NetworkGameEndController.cs` — `_uiManager` 필드 추가, AnnounceWinnerClientRpc에서 `_uiManager?.NotifyGameEnded()` 호출 추가

**핵심 패턴**:
- `GameUIManager.Register()` — 중복 등록 방지 포함, LoadMap() 재호출 시 안전
- `GameUIManager.Initialize()` — CompositeDisposable로 중복 구독 방지
- `GameEndUI`는 OnGameEnded() 호출 제외 (ReferenceEquals 비교)
- **BUG-1 (멀티플레이 클라이언트 팝업 미닫힘)**: 클라이언트는 GameEvents.OnGameEnd 미발행 설계 → AnnounceWinnerClientRpc에서 직접 NotifyGameEnded() 호출로 수정

**새 UI 추가 시 체크리스트**:
1. `IGameUI` 인터페이스 구현 (필요한 메서드만 override)
2. `GameBootstrapper.LoadMap()` 앞부분에 `_uiManager.Register(새UI)` 1줄 추가
3. Inspector 참조 연결

### 반투명 배경 오버레이 구조 개선 (2026-03-23) ✅ 실기 테스트 완료

**변경 내용**:
- `AnimatedPanel.cs`: Hide() 내 `_backgroundOverlay.SetActive(false)` 타이밍 변경 — OnComplete 콜백 → Hide() 호출 즉시
- `SharedBackgroundButton.cs` (신규, `Presentation/UI/Common/`): Canvas 직속 공유 Background에 부착
  - `Register(Action onClose)` / `Unregister()` / `OnClick()` 3개 메서드
- `BuildingPlacementUI.cs` / `ProductionPanelUI.cs`: `_backgroundButton(Button)` 제거 → `_sharedBackground(SharedBackgroundButton)` 교체
  - Show()에서 `_sharedBackground?.Register(Close)`, Close()에서 `_sharedBackground?.Unregister()`

**씬 구조 변경 (Game.unity)**:
- `[UI]/Background` 하나를 ProductionPopup/BuildingPopup/GameEndPanel이 공유
- 각 팝업 자식 Background 삭제됨

### 유닛 생산 패널 전면 재작성 (2026-04-19) ✅ 실기 완료

**수정 파일**:
- `Domain/Building/ProductionState.cs` — QueueSlot struct 추가, PendingQueue/AutoTypes/AutoCycleIndex/CurrentIsAuto 추가, IsAutoMode → 읽기 전용 프로퍼티(`AutoTypes.Count > 0`)
- `Application/UseCases/UnitProductionUseCase.cs` — EnqueueUnit/ToggleAutoProduction/CancelQueueAt/TryStartNext/CompleteProduction/ChargeVisibleSlots 전면 재작성. CancelAutoTypeIfNeeded 헬퍼 추가.
- `Presentation/UI/ProductionPanelUI.cs` — UpdateQueueSlots 단순화, OnQueueSlotClicked fallback 제거
- `Infrastructure/Network/NetworkProductionController.cs` — SyncQueueStateClientRpc 파라미터 포맷 변경

**핵심 구조 (PendingQueue 단일 큐)**:
- `QueueSlot { Type, IsAuto, IsCharged }` — 단일 구조체로 수동/자동 통합
- `PendingQueue[0]=슬롯1, PendingQueue[1]=슬롯2` 불변식 — UI는 이 순서 그대로 읽으면 됨
- `AutoTypes: List<UnitType>` — 자동 등록 타입 목록 (인디케이터 + 순환 대상)
- `IsAutoMode = AutoTypes.Count > 0` — 필드 아님, 항상 AutoTypes 상태에서 계산

**전역 규칙**:
- Rule 1: 슬롯 클릭 취소 → 항상 전액 환불 (IsCharged=true인 경우)
- Rule 2: 자동 취소 시 IsCharged=true 항목은 수동 이관 (환불 없이 생산 계속)
- Rule 2-1: 자동 등록 타입이 PendingQueue 마지막 수동 항목과 같으면 IsAuto=true로 전환 (중복 추가 금지)
- Rule 3: 수동 추가 시 자동 모드 전체 해제 (IsCharged=false 자동 항목 제거, IsCharged=true는 수동 이관)
- Rule 4: CurrentProducing + IsCharged=true PendingQueue 합산 ≤ MaxQueueSize(3)
- Rule 5: 골드 차감 = 수동은 등록 시, 자동은 슬롯1/2 진입 시 (ChargeVisibleSlots)

**슬롯 클릭 = 생산 취소 + 자동 항목이면 AutoTypes에서도 제거**:
- `CancelAutoTypeIfNeeded(state, type)` — AutoTypes 제거 + 잔여 IsAuto 항목 Rule 2 처리 + NormalizeAutoCycleIndex
- slotIndex==0: `wasAuto = state.CurrentIsAuto` 를 `CurrentIsAuto=false` 초기화 전에 캡처 필수

**미해결 이슈**: 큐 비어있을 때 자동 등록 시 슬롯1에 1프레임 깜빡임 → 별도 점검 예정

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-19/production-panel-rewrite/`

---

### 자동/수동 생산 하이브리드 시스템 완성 (2026-03-23) [재작성 예정으로 무효화]

**핵심 설계**: AutoEntry(UnitType + IsCharged) 기반 골드 차감 시점 추적

**수정 파일**:
- `Domain/Building/ProductionState.cs` — AutoEntry 구조체, AutoEntries(List<AutoEntry>), AutoContains/AutoIndexOf 등 편의 접근자
- `Application/UseCases/UnitProductionUseCase.cs` — ToggleAutoProduction, EnqueueUnit, TryStartNext, CancelQueueAt, CanAutoEntryShowInSlot, TryPreChargeAutoEntries
- `Presentation/UI/ProductionPanelUI.cs` — UpdateQueueSlots 혼용 표시, 버튼 탭/롱프레스 분기
- `Infrastructure/NetworkProductionController.cs` — AutoEntries 참조 갱신

**핵심 패턴**:
- `CanAutoEntryShowInSlot`: AutoIndex 위치(슬롯0) 항목을 shownCount에서 **반드시 제외** (BUG-12)
  ```csharp
  for (int i = 0; i < state.AutoEntries.Count; i++)
  {
      if (i == state.AutoIndex) continue; // 슬롯0 제외
      if (state.AutoEntries[i].IsCharged) shownCount++;
  }
  ```
- `UpdateQueueSlots` 슬롯2: manualCount==1 && isNormalAutoState일 때 autoCount >= 2 필수 (BUG-13)
  - autoCount==1이면 그 항목이 슬롯0과 동일 → 슬롯2=null
- `ToggleAutoProduction` 취소 경로: 환불 없음, IsCharged=true && 슬롯1~2면 ManualQueue.Add (Rule 2)
- `TryStartNext` 자동 경로: IsCharged=false면 이 시점에 골드 차감 후 IsCharged=true 갱신
- `CompleteProduction` 자동 순환: AutoIndex 순환 **직전**에 완료된 항목의 IsCharged를 false로 리셋 (BUG-20 수정)
  ```csharp
  // AutoIndex 순환 전 IsCharged 리셋 — 다음 순환 시 골드 재차감을 위해
  var completedEntry = state.AutoEntries[state.AutoIndex];
  state.AutoEntries[state.AutoIndex] = new AutoEntry(completedEntry.Type, false);
  state.AutoIndex = (state.AutoIndex + 1) % state.AutoEntries.Count;
  ```
  - 리셋 안 하면 IsCharged=true 유지 → TryStartNext/TryPreChargeAutoEntries 모두 건너뜀 → 첫 등록 시만 골드 소모

**전역 규칙 참조**: `GameDesignDocument.md` → "생산 패널 운영 규칙" 섹션

### 코드 정리 (2026-03-20) ✅ 테스트 완료
- **TeamAssigner.cs 삭제**: Player Prefab=None으로 스폰 안 됨, NetworkGameFlow로 완전 대체 확인 후 삭제
- **LocalPlayerTeam.cs 주석 정리**: "TeamAssigner에서 호출" → "NetworkGameFlow에서 호출" (5곳)
- **NetworkGameFlow.cs 주석 정리**: L12 "TeamAssigner 준비 대기" → "IsHost 기반으로 팀 직접 결정"
- **GameBootstrapper.cs IsNetworkMode() 헬퍼 추출**: `NetworkManager.Singleton != null && (IsHost || IsClient)` 4곳 중복 → private 메서드 통합

### 싱글플레이 ViewConverter 초기화 버그 수정 (2026-03-20) ✅ 테스트 완료
- **증상**: Red팀 싱글플레이에서 내 진영이 화면 하단이 아닌 상단에 표시
- **원인**: `ViewConverter.Reset()`이 LocalPlayerTeam.Current 무시하고 항상 Blue 관점 고정
- **수정**: `GameBootstrapper.LoadMap()` — `ViewConverter.Reset()` 제거, `ApplyConfig()` 직후 LocalPlayerTeam 기반 Setup:
  ```csharp
  if (!isNetworkMode)
  {
      Vector3 mapCenter = HexMetrics.GridCenter(oc.GridWidth, oc.GridHeight);
      bool isRed = (LocalPlayerTeam.Current == TeamId.Red);
      ViewConverter.Setup(isRed, mapCenter);
  }
  ```
- **주의**: `ApplyConfig()` 이후에 호출해야 HexMetrics 준비 완료 후 GridCenter 계산 가능
- **카메라 초기 위치는 변경 없음** — 맵 중앙 유지 (SetCameraStartPositionForTeam 호출 금지)

### 카메라 줌 DOTween 보간 (2026-03-19) ✅ 테스트 완료
- **CameraController.cs**: HandleZoom() 즉시 적용 → DOTween 보간으로 교체
  - `_targetZoom` (float): 입력 시 Clamp된 목표값 누적
  - `_zoomTween` (Tweener): Kill() 후 새 Tween 시작 — 연속 스크롤 시 부드럽게 목표 갱신
  - `DOTween.To(() => _cam.orthographicSize, x => _cam.orthographicSize = x, _targetZoom, _zoomDuration).SetEase(Ease.OutCubic)`
  - `_zoomDuration` (SerializeField, default=0.25f): Inspector 조정 가능
  - `Awake()`에서 `_targetZoom = _cam.orthographicSize` 초기화
  - `OnDestroy()`에서 `_zoomTween?.Kill()` 정리
  - `using DG.Tweening` 추가
- ClampPosition()은 매 프레임 orthographicSize 읽으므로 수정 불필요

### 건물 인근 이동/공격 불가 버그 수정 (2026-03-18) ✅ 테스트 완료
- **HexPathfinder.cs**: `FindPath()` goal blocked 체크 제거 — 목표 타일이 ClaimedTile에 선점되어도 경로 탐색 가능
  - 이전: `if (blockedCoords.Contains(goal)) return null;` → 인근 타일 모두 선점 시 교착 상태
  - 이후: blocked는 경로 중간 타일에만 적용, 목표 도착 충돌은 ProcessStep에서 처리
- **UnitCombatUseCase.cs**: maxDist에 `Epsilon=0.05f` 추가
  - Pistoleer maxDist(0.866) = FlatTop 인접 거리(0.866) 경계 케이스 → 부동소수점 오차로 공격 실패
  - `float maxDist = attacker.AttackRange * HexMetrics.TileHeight + Epsilon;`

### 랜덤매칭 재경기 지원 (2026-03-18) ✅
- **GameEndUI.cs**: `SetupRematchButton()`에서 `isRandomMatch==true`일 때 버튼 숨기는 분기 제거
  - 랜덤매칭도 커스텀게임과 동일 흐름: 양측 동의 재경기 팝업 + NGO SceneManager.LoadScene("Game")

### 로비 종족 선택 UI — 캐러셀 방식 (2026-04-04~06) ✅ 테스트 완료

**신규/수정 파일**:
- `Domain/Common/RaceId.cs` — enum Human=0, Spirit=1, Transcendence=2 (자연→초월 변경)
- `Infrastructure/LocalPlayerRace.cs` — 로컬 플레이어 종족 정적 홀더 (Set/Current/Reset)
- `Infrastructure/GameRaceContext.cs` — BlueRace/RedRace 정적 홀더 (멀티플레이 수신용)
- `Presentation/UI/ViewModels/RaceSelectionViewModel.cs` — UniRx ReactiveProperty, CmdPrev/CmdNext, LocalPlayerRace.Set 연동
- `Presentation/UI/Views/Lobby/Battle/RaceSelectionView.cs` — 캐러셀 DOTween, Animator CrossFade 1초, IView 패턴
- `Presentation/UI/Views/Lobby/Battle/BattleMainView.cs` — BindRace() 메서드 추가, RaceSelectionView 항상 표시(독립 토글 제거)
- `Editor/RaceSelectionPreviewSetup.cs` — 씬 자동 구성 에디터 스크립트 (CharacterPreview 레이어, RT 512×512, 카메라 Z=-2, FOV=45)
- `Animations/Units/Pistoleer/Pistoleer.controller` — Idle 상태 m_Speed 0→1 수정

**핵심 설계**:
- RaceSelectionView는 BattlePanel(BattleRootView) 직속 자식, anchorMin=(0,0) anchorMax=(1,0.5) — BattleMainPanel과 sibling
- BattleMainPanel: 상단 50% (anchorMin.y=0.5, anchorMax.y=1.0)
- RaceSelectionView 항상 표시 — BattleMainPanel(버튼 영역)만 CurrentScreen에 따라 토글
- RaceSelectionViewModel은 BattleRootView에서 생성/Dispose, BattleMainView.BindRace()로 전달
- CharacterPreview 레이어 격리 → RenderTexture → RawImage(CharacterDisplay)
- AnimBlendTime = 1.0f (_moveDuration과 동일), offset 0(중앙)=Walk, offset 1,2(좌우)=Idle

**캐러셀 위치 (씬 확정값)**:
- CenterPos: (1000, 0.35, 2), LeftPos: (999.7, 0.1, 5), RightPos: (1000.3, 0.1, 5)
- 카메라: (1000, 1.5, -2), Rotation: Euler(12, 0, 0), FOV=10

**Pistoleer Idle 버그 교훈**:
- Animator Controller 상태의 m_Speed 값 직접 확인 필수 (Editor에서 설정하지 않으면 0이 될 수 있음)
- m_Speed: 0이면 애니메이션이 첫 프레임에서 동결됨

**Android URP RenderTexture 잔상 버그 교훈 (2026-04-06)**:
- 근본 원인: RT 에셋 파일(`m_AntiAliasing: 2`)과 카메라 설정(`allowMSAA=false`, 1 sample) 간 sample count 불일치
- 에러: `Attachment 0 was created with 1 samples but 2 samples were requested`
- 현상: sample count 충돌 → URP Render Pass clear 실패 → 이전 프레임 타일 메모리 로드 → 잔상
- 수정 체크리스트 (RenderTexture 전용 카메라 설정):
  - RT 에셋: `m_AntiAliasing: 1` (YAML 직접 확인 필수 — EnsureRenderTexture 코드 수정만으로 반영 안 될 수 있음)
  - Camera: `allowMSAA = false`, `allowHDR = false` (기본값 true라 명시적으로 꺼야 함)
  - Camera: `backgroundColor.alpha = 1` (alpha=0이면 일부 Android GPU 드라이버 clear 생략)
  - URP: `urpData.antialiasing = AntialiasingMode.None`
  - URP: `urpData.renderType = CameraRenderType.Base`
  - URP: `urpData.renderShadows = false`

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-04/21_00_race-selection-ui/`

### 재경기 초기화 버그 수정 (2026-04-04) ✅ 테스트 완료

**증상**: 재경기 시 이전 게임 유닛/건물이 씬에 잔존
**원인**: NGO SceneManager.LoadScene(Single)으로 같은 씬 재로드 시 동적 스폰 NetworkObject 자동 Despawn 미보장
**수정**: `NetworkGameEndController.StartRematch()`에서 LoadScene() 직전 SpawnManager.SpawnedObjects 순회 → 동적 NetworkObject 명시적 Despawn

**핵심 패턴**:
- `SpawnedObjects.Values`를 `List<NetworkObject>` 복사본으로 순회 (Despawn 중 컬렉션 변경 방지)
- `IsSceneObject == false`만 Despawn (씬 배치 오브젝트 자동 제외)
- `IsSpawned == true` / `IsSceneObject == false` — NGO 2.9.x에서 bool? (nullable) 비교 방식 필수

**교훈**:
- `DestroyWithScene = true`는 같은 씬 재로드 시나리오에서 동작 불보장
- 같은 씬 재로드 전에는 반드시 동적 NetworkObject를 명시적으로 Despawn해야 함
- `Active Scene Synchronization`은 씬 전환용 설정 — 같은 씬 재로드와 무관

**task 문서**: `Assets/_Project/Docs/_Tasks/2026-04-03/20_00_rematch-initialization-bug/`

### 커스텀게임 재경기(Rematch) 시스템 (2026-03-17) ✅ 테스트 완료
- **NetworkGameManager.cs**: `_isRandomMatchmaking` bool 필드 + `IsRandomMatchmaking` 속성 추가
  - StartMatchmakingAsync → true, CancelMatchmakingAsync/DisconnectAsync → false
- **NetworkGameEndController.cs**: 재경기 RPC 시스템 전면 교체
  - `AnnounceWinnerClientRpc(int, bool isRandomMatch)` — 파라미터 2개로 변경
  - `_rematchRequesterId` (ulong.MaxValue=없음) — 첫 요청자 추적, 양측 요청 시 즉시 재경기
  - RPC: RequestRematchServerRpc, AcceptRematchServerRpc, DeclineRematchServerRpc
  - ClientRpc: NotifyRematchRequestedClientRpc(targeted), NotifyRematchDeclinedClientRpc(targeted)
  - `StartRematch()`: NGO SceneManager.LoadScene("Game") — 네트워크 유지 상태 씬 재로드
  - `_lobbySceneName` 제거, `OnMultiplayerRestart()` 제거
  - `_rematchRequestPopup` SerializeField 추가 (Inspector 연결 필요)
- **GameEndUI.cs**: `OverrideRestartForMultiplayer()` → `SetupRematchButton(bool, Action)` + `RestoreRematchButton()` 교체
  - `_restartButtonText` SerializeField 추가 (Inspector 연결 필요)
  - ~~랜덤매칭: 다시하기 버튼 숨김~~ → 2026-03-18 제거, 랜덤매칭도 재경기 지원
  - 커스텀게임: 요청/대기/복원 UI 상태 관리
- **RematchRequestPopup.cs** (신규): `Presentation/UI/Common/` — `_overlay`+수락/거절 팝업+거절 알림 팝업
  - Inspector 연결 필요: _overlay, _requestPanel, _acceptButton, _declineButton, _declinedPanel, _declinedConfirmButton
  - **루트 오브젝트는 Active 유지 필수** — FindFirstObjectByType은 비활성 오브젝트 탐색 불가
  - Hide()/Show*()에서 _overlay도 함께 제어 (overlay 별도 필드로 관리)
- **FindFirstObjectByType 교훈**: 비활성 오브젝트 포함 탐색 시 `FindObjectsInactive.Include` 인자 필요

### 멀티플레이 로비 복귀 버그 수정 (2026-03-17)
- **근본 원인**: `NetworkGameEndController._lobbySceneName` Inspector="Game" → 게임 씬 재로드
- **GameEndUI.cs**: `ReturnToLobby()` (NGM.Shutdown + LoadScene("Lobby")), `CountdownCoroutine()` (WaitForSecondsRealtime 기반 30초)
- Inspector 연결 필요: `_countdownText` (TextMeshProUGUI), `_autoReturnSeconds` (default=30f)

### 전역 로딩 스크린 구현 (2026-03-17)
- `LoadingScreen.cs` (`Presentation/UI/Common/`): 싱글턴, DontDestroyOnLoad, CanvasGroup DOFade 페이드 인/아웃
- `BattleViewModel.cs`: 싱글플레이 `LoadSingleplayScene()` → async void + `await Task.Delay(2000)` + Show/Hide
- 커스텀 호스트/참가: `LoadGameScene()` 직전 `LoadingScreen.Instance?.Show()`
- 랜덤매칭: `NetworkGameManager.StartMatchmakingAsync`에 `onMatchFound` 콜백 추가 → matchId 확보 직후 Show()
- sceneLoaded 이벤트로 모든 케이스 자동 Hide() (NGO 씬 전환 포함)

### 랜덤 매칭 버그 수정 (2026-03-16) — [random-matching-bugfix.md](random-matching-bugfix.md)
- string.GetHashCode() 크로스-프로세스 비결정성 → GetStableHash() 대체
- NetworkGameManager: OnClientConnectedCallback 등록을 StartNetworkHost() 이전으로 이동

### Animation Event 타격 반응 (2026-03-14) — [rendering-and-animation.md](rendering-and-animation.md)
- AnimationEventRelay → UnitView.OnAttackHit() → scale punch 시각 효과

### 유닛 확정 스탯 (2026-03-14) — [unit-stats-and-combat.md](unit-stats-and-combat.md)
- Pistoleer/Assault/Sniper 3종 스탯 확정, AttackRange int→float 변경

## 토픽 파일 인덱스

### 네트워크
- [network-infra.md](network-infra.md) — Phase 1~8 상세 (UGS, NGO, 동기화, UI/UX, 팀 할당, 승패)
- [network-todo.md](network-todo.md) — 네트워크 미완성 항목
- [random-matching-bugfix.md](random-matching-bugfix.md) — 2026-03-16 랜덤 매칭 버그 수정

### 전투 & 유닛
- [unit-stats-and-combat.md](unit-stats-and-combat.md) — 스탯, IEntityPositionProvider, 쿨다운, 클라이언트 시각 동기화
- [combat-fixes.md](combat-fixes.md) — ClaimedTile 기반 공격 위치 보정, UnitView 부드러운 회전
- [attack-direction-refactor.md](attack-direction-refactor.md) — 공격 방향 리팩터링 (2D→3D)

### 렌더링 & 뷰
- [rendering-and-animation.md](rendering-and-animation.md) — UnitView 애니메이션, Shader Graph, HexTileView, 팀 프리팹
- [3d-transition.md](3d-transition.md) — XZ 좌표계 전환, 렌더링 전환, Phase별 수정 파일
- [camera-and-view.md](camera-and-view.md) — 카메라 틸트, ViewConverter, 경계 클램프, 건물 위치 버그

### 게임플레이
- [gameplay-systems.md](gameplay-systems.md) — 랠리포인트, 초상화 동적 업데이트

## 핵심 패턴 요약

### 정적 홀더 패턴 (레이어 간 의존성 우회)
- `HexOrientationContext` — Domain에서 Core의 Orientation 접근
- `NetworkContext` — Application에서 NetworkManager 상태 접근
- `LocalPlayerTeam` — 현재 플레이어 팀 (싱글=Blue, 네트워크 시 갱신)
- `ViewConverter` — Red팀 좌표/방향 반전

### GameBootstrapper Start() 분기
- NetworkManager null 또는 IsHost/IsClient=false → 싱글플레이 (LoadMap 즉시)
- 네트워크 → 맵 로드 건너뜀, NetworkGameFlow가 StartNetworkGame() 대기
- C# LangVersion 9.0 (switch expression 사용 가능)

### 팀 매핑
- TeamId: Neutral=0, Blue=1, Red=2
- Host→Blue, Client→Red
- TeamAssigner는 삭제됨 (2026-03-20) — NetworkGameFlow.WaitForTeamAndSendReady()에서 IsHost?Blue:Red로 직접 할당

### 동기화 타이밍
- NetworkSync 스폰 시 HexGrid/ResourceUseCase null 가능 → null 방어 필수
- ResourceUseCase 생성자는 OnResourceChanged 미발행 → SyncInitialGold() 필요
- ViewConverter.Setup()은 LoadMap() 이전에 호출해야 함

### 유닛 애니메이션 핵심
- Animator.Play() 직접 호출 (트랜지션 우회)
- 파라미터: IsDead(bool) 1개만
- Root Motion 반드시 OFF
- **Animator Controller 상태 m_Speed 주의**: 기본값 0이면 애니메이션 첫 프레임 동결. 새 상태 추가 시 m_Speed=1 확인 필수
