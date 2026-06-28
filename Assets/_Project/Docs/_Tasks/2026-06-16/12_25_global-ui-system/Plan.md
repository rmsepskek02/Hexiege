# Plan — 전역 UI 시스템 도입

## 이 작업으로 무엇을 만드는가

씬마다 중복 배치되어 있던 공통 UI(ConfirmPopup, LoadingIndicator, ToastUI)를
Login 씬에서 초기화하는 전역 UIManager로 통합한다.
동시에 Login 씬에 SplashOverlay UI를 추가하여 초기화 중 배경 이미지를 표시하고,
완료 후 "Tap to Start" 안내를 거쳐 페이드아웃 후 로그인 화면을 노출한다.

**프리팹 기준 씬** (Inspector에서 가장 완성된 버전):
- ConfirmPopup → Game.unity
- ToastUI, LoadingIndicator → Lobby.unity

---

## Splash 프로세스

```
앱 실행 → Login 씬 로드
     ↓
┌─────────────────────────┐
│                         │
│     [배경 이미지]        │
│                         │
│      "로딩 중..."        │  ← 초기화 진행 중 표시
│                         │
└─────────────────────────┘
     ↓ AudioManager + Firebase + UIManager 초기화 완료
┌─────────────────────────┐
│                         │
│     [배경 이미지]        │
│                         │
│    "Tap to Start"       │  ← alpha 0↔1 페이드 반복 (DOTween)
│                         │
└─────────────────────────┘
     ↓ 화면 탭
SplashOverlay 전체 페이드아웃 → 로그인 화면 노출
```

---

## 작업 단계

### STEP 1 — IUIManager 인터페이스 + UIManager 구현체

**신규**: `Assets/_Project/Scripts/Presentation/UI/Core/IUIManager.cs`

```csharp
namespace Hexiege.Presentation
{
    public interface IUIManager
    {
        void ShowConfirm(string message, System.Action onConfirm,
                         System.Action onCancel = null,
                         string confirmLabel = "확인", string cancelLabel = "취소");
        void ShowLoading(bool show);
    }
}
```

**신규**: `Assets/_Project/Scripts/Presentation/UI/UIManager.cs`

```csharp
// SingletonMonoBehaviour<UIManager> 상속 → DontDestroyOnLoad 자동 적용
// Inspector 참조: _confirmPopup(ConfirmPopup), _loadingIndicator(CanvasGroup)
// ShowConfirm / ShowLoading 구현
// IUIManager 인터페이스 구현
```

---

### STEP 2 — SplashOverlay UI (Login.unity에 추가)

**Login.unity에 SplashOverlay GameObject 추가**:

```
Canvas
└─ SplashOverlay (CanvasGroup, SortingOrder 최상위)
    ├─ Background (Image — 배경 이미지, 전체화면)
    ├─ StatusText (TextMeshProUGUI — "로딩 중...")
    └─ TapToStartText (TextMeshProUGUI — "Tap to Start", 초기 alpha=0)
```

**신규**: `Assets/_Project/Scripts/Presentation/UI/SplashOverlayView.cs`

```csharp
// 역할:
//   - SetStatus(string text): StatusText 문구 변경 ("로딩 중...")
//   - ShowTapToStart(): StatusText 숨김 + TapToStartText DOTween alpha 0↔1 반복 깜빡임
//   - FadeOut(System.Action onComplete): SplashOverlay 전체 CanvasGroup 페이드아웃
//   - 화면 탭(IPointerClickHandler): ShowTapToStart 상태일 때만 FadeOut 트리거
```

---

### STEP 3 — LoginBootstrapper 수정

**변경**: `Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs`

- `[SerializeField] private SplashOverlayView _splashOverlay;` 추가
- `[SerializeField] private GameObject _loadingIndicator;` 제거 (UIManager로 이동)
- `[SerializeField] private ConfirmPopup _confirmPopup;` 제거 (UIManager로 이동)
- 초기화 흐름 변경:
  ```
  Start()
    → _splashOverlay.SetStatus("로딩 중...")
    → AudioManager.Initialize()
    → UIManager.Instance 확인
    → Firebase 초기화
    → 자동 로그인 시도
    → 완료: _splashOverlay.ShowTapToStart()
    → 탭 → _splashOverlay.FadeOut() → 로그인 화면 노출 또는 Lobby 이동
  ```
- `ShowLoading(bool show)` 메서드 제거
- `UIManager.Instance`로 IUIManager 획득 → 필요한 View에 주입

---

### STEP 4 — 프리팹 추출

각 씬에서 완성된 버전을 프리팹으로 추출한다.

- **Game.unity → ConfirmPopup 프리팹** 추출 (구조 변경 없음)
  - 저장 경로: `Assets/_Project/Prefabs/UI/ConfirmPopup.prefab`
  - Canvas 없이 Canvas 하위 위젯으로 존재 → UIManager Canvas 안에 그대로 임베드

- **Lobby.unity → ToastUI 프리팹** 추출 (구조 변경 없음)
  - 저장 경로: `Assets/_Project/Prefabs/UI/ToastUI.prefab`
  - 자체 Canvas + DontDestroyOnLoad 내장 독립 오브젝트 → 씬 루트에 직접 배치

- **Lobby.unity → LoadingIndicator 프리팹** 추출 (**B안 — 비UI 컴포넌트 제거 후 저장**)
  - 저장 경로: `Assets/_Project/Prefabs/UI/LoadingIndicator.prefab`
  - 원본 오브젝트 이름: `LoadingScreen` (자체 Canvas + LoadingScreen.cs 보유)
  - 추출 시 제거: `Canvas`, `CanvasScaler`, `GraphicRaycaster`, `LoadingScreen.cs`
  - 유지되는 구조:
    ```
    LoadingIndicator (RectTransform + CanvasGroup)
    └─ RootPanel (Image — 반투명 배경)
        ├─ Spinner (Image — 회전 아이콘)
        └─ StatusText (TextMeshProUGUI)
    ```
  - `Spinner` 자식 오브젝트에 `SpinnerRotator` 컴포넌트 **자동 부착** (에디터 스크립트가 처리)
  - 씬은 변경 폐기(CloseScene true)로 원본 LoadingScreen 보존

---

### STEP 5 — UIManager 씬 배치 (Login.unity)

Login.unity에 UIManager 전용 GameObject 추가:

```
[UI Systems] GameObject (Login.unity)
└─ UIManager (SingletonMonoBehaviour → DontDestroyOnLoad)
    └─ UIManager Canvas (SortingOrder: 100)
        └─ SafeAreaContainer (SafeAreaFitter)
            ├─ ConfirmPopup      ← 프리팹 배치 (Canvas 없는 순수 UI 위젯)
            └─ LoadingIndicator  ← 프리팹 배치 (B안 — Canvas/LoadingScreen.cs 제거본)

Toast (씬 루트 — [UI Systems] 밖)
  ← ToastUI 프리팹 직접 배치 (자체 Canvas 내장)
  ← Awake()에서 SetParent(null) + DontDestroyOnLoad 자동 처리
```

> **Toast를 래퍼 Canvas 안에 넣지 않는 이유**:
> ToastUI.Awake()가 `transform.SetParent(null)`로 스스로 부모를 끊고 DontDestroyOnLoad를
> 적용한다. 래퍼 Canvas 안에 넣어도 런타임에 빠져나가므로 처음부터 씬 루트에 배치한다.

---

### STEP 6 — GameBootstrapper 정리

**변경**: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

- `[SerializeField] private ConfirmPopup _confirmPopup;` 제거
- `UIManager.Instance`로 IUIManager 획득 → 필요한 View에 주입

---

### STEP 7 — 씬 정리 (Inspector 작업)

**Login.unity**:
- 기존 LoadingIndicator GameObject 제거 (UIManager Canvas로 이동)
- 기존 ConfirmPopup GameObject 제거 (UIManager Canvas로 이동)
- [Audio] GameObject는 유지 (AudioManager DontDestroyOnLoad 기준점)

**Lobby.unity**:
- ConfirmPopup GameObject 제거 (프리팹 추출 후)
- Toast Canvas > ToastUI GameObject 제거 (프리팹 추출 후)
- LoadingIndicator GameObject 제거 (프리팹 추출 후)

**Game.unity**:
- ConfirmPopup GameObject 제거 (프리팹 추출 후)

---

## 파일 목록

```
[신규]
- Assets/_Project/Scripts/Presentation/UI/Core/IUIManager.cs
- Assets/_Project/Scripts/Presentation/UI/UIManager.cs
- Assets/_Project/Scripts/Presentation/UI/SplashOverlayView.cs
- Assets/_Project/Prefabs/UI/ConfirmPopup.prefab  (Game.unity에서 추출)
- Assets/_Project/Prefabs/UI/ToastUI.prefab       (Lobby.unity에서 추출)
- Assets/_Project/Prefabs/UI/LoadingIndicator.prefab (Lobby.unity에서 추출)

[변경]
- Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs
- Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs

[씬 수정 — Inspector 작업]
- Assets/_Project/Scenes/Login.unity  (SplashOverlay + UIManager Canvas 추가, 기존 중복 UI 제거)
- Assets/_Project/Scenes/Lobby.unity  (ConfirmPopup / ToastUI / LoadingIndicator 제거)
- Assets/_Project/Scenes/Game.unity   (ConfirmPopup 제거)
```

---

## 위험 요소

| 위험 | 대응 |
|--|--|
| Login/Game 씬을 직접 열어 테스트 시 UIManager.Instance = null | null-safe 호출 패턴 (`UIManager.Instance?.ShowConfirm(...)`) 사용 |
| 자동 로그인 성공 시 Tap to Start 없이 바로 Lobby로 이동 | FadeOut 후 GoToNextScene() 호출 — Tap 없이 자동 전환 |
| ToastUI Lobby → Login 이동 시 Lobby 직접 진입 테스트에서 ToastUI 없음 | null-safe `ToastUI.Show()` 이미 적용됨 — 안전하게 무시 |

---

## 작업 순서

1. IUIManager + UIManager 코드 작성
2. SplashOverlayView 코드 작성
3. LoginBootstrapper 수정
4. GameBootstrapper 수정
5. Game.unity에서 ConfirmPopup 프리팹 추출
6. Lobby.unity에서 ToastUI / LoadingIndicator 프리팹 추출
7. Login.unity에 SplashOverlay UI + UIManager Canvas 추가 + Inspector 연결 (에디터 작업)
8. Lobby / Game.unity 씬 중복 UI 제거 (에디터 작업)
