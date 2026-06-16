# Plan — 전역 UI 시스템 도입

## 이 작업으로 무엇을 만드는가

씬마다 중복 배치되어 있던 공통 UI(ConfirmPopup, LoadingIndicator, ToastUI)를
Splash 씬에서 초기화하는 전역 UIManager로 통합한다.
동시에 Splash 씬을 신설하여 전역 시스템(UIManager, AudioManager, Firebase 등) 초기화를
LoginBootstrapper에서 분리한다.

**프리팹 기준 씬** (Inspector에서 가장 완성된 버전):
- ConfirmPopup → Game.unity
- ToastUI, LoadingIndicator → Lobby.unity

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
        void ShowAlert(string message, System.Action onConfirm = null,
                       string confirmLabel = "확인");
        void ShowLoading(bool show);
    }
}
```

**신규**: `Assets/_Project/Scripts/Presentation/UI/UIManager.cs`

```csharp
// SingletonMonoBehaviour<UIManager> 상속 → DontDestroyOnLoad 자동 적용
// Inspector 참조: _confirmPopup(ConfirmPopup), _alertPopup(AlertPopup), _loadingIndicator(CanvasGroup)
// ShowConfirm / ShowAlert / ShowLoading 구현
// IUIManager 인터페이스 구현
```

---

### STEP 2 — 프리팹 추출

각 씬에서 완성된 버전을 프리팹으로 추출한다.

- **Game.unity → ConfirmPopup 프리팹** 추출
  - 저장 경로: `Assets/_Project/Prefabs/UI/ConfirmPopup.prefab`
- **Lobby.unity → ToastUI 프리팹** 추출
  - 저장 경로: `Assets/_Project/Prefabs/UI/ToastUI.prefab`
- **Lobby.unity → LoadingIndicator 프리팹** 추출
  - 저장 경로: `Assets/_Project/Prefabs/UI/LoadingIndicator.prefab`

---

### STEP 3 — Splash.unity 씬 + SplashBootstrapper

**신규**: `Assets/_Project/Scenes/Splash.unity`

구조:
```
[Global Systems] GameObject
├─ UIManager (SingletonMonoBehaviour)
│   └─ UIManager Canvas (SortingOrder: 100)
│       └─ SafeAreaContainer (SafeAreaFitter)
│           ├─ ConfirmPopup   ← Game.unity에서 추출한 프리팹 배치
│           └─ LoadingIndicator ← Lobby.unity에서 추출한 프리팹 배치
└─ Toast Canvas (SortingOrder: 200)
    └─ ToastUI               ← Lobby.unity에서 추출한 프리팹 배치

[Audio] GameObject
└─ AudioManager (SingletonMonoBehaviour)

[Bootstrap] GameObject
└─ SplashBootstrapper
```

**신규**: `Assets/_Project/Scripts/Bootstrap/SplashBootstrapper.cs`

```csharp
// 역할:
//   1. AudioManager.Initialize(soundConfig)
//   2. Firebase SDK 초기화 (현재 LoginBootstrapper에서 수행 중인 것 이동)
//   3. 스플래시 로고/애니메이션 표시 (최소 N초)
//   4. 초기화 완료 → SceneManager.LoadScene("Login")
```

**빌드 세팅**: Splash → Login → Lobby → Game 순서로 등록

---

### STEP 4 — LoginBootstrapper 정리

**변경**: `Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs`

- `[SerializeField] private ConfirmPopup _confirmPopup;` 제거
- `[SerializeField] private GameObject _loadingIndicator;` 제거 (UIManager로 이동)
- Firebase 초기화 코드 → SplashBootstrapper로 이동
- AudioManager.Initialize() 제거 (SplashBootstrapper로 이동)
- `UIManager.Instance`로 IUIManager 획득 → 필요한 View에 주입
- `ShowLoading(bool show)` 메서드 제거 → `UIManager.Instance.ShowLoading()` 직접 호출

---

### STEP 5 — GameBootstrapper 정리

**변경**: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

- `[SerializeField] private ConfirmPopup _confirmPopup;` 제거
- `UIManager.Instance`로 IUIManager 획득 → 필요한 View에 주입

---

### STEP 6 — 씬 정리 (Inspector 작업)

**Login.unity**:
- ConfirmPopup GameObject 제거
- LoadingIndicator GameObject 제거
- [Audio] GameObject 제거 (Splash로 이동)

**Lobby.unity**:
- ConfirmPopup GameObject 제거
- Toast Canvas > ToastUI GameObject 제거 (Splash로 이동, 프리팹 추출 후)
- LoadingIndicator GameObject 제거 (프리팹 추출 후)

**Game.unity**:
- ConfirmPopup GameObject 제거 (프리팹 추출 후)

---

## 파일 목록

```
[신규]
- Assets/_Project/Scripts/Presentation/UI/Core/IUIManager.cs
- Assets/_Project/Scripts/Presentation/UI/UIManager.cs
- Assets/_Project/Scripts/Bootstrap/SplashBootstrapper.cs
- Assets/_Project/Scenes/Splash.unity
- Assets/_Project/Prefabs/UI/ConfirmPopup.prefab  (Game.unity에서 추출)
- Assets/_Project/Prefabs/UI/ToastUI.prefab       (Lobby.unity에서 추출)
- Assets/_Project/Prefabs/UI/LoadingIndicator.prefab (Lobby.unity에서 추출)

[변경]
- Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs
- Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs

[씬 수정 — Inspector 작업]
- Assets/_Project/Scenes/Login.unity
- Assets/_Project/Scenes/Lobby.unity
- Assets/_Project/Scenes/Game.unity
- Assets/_Project/Scenes/Splash.unity (신규)
```

---

## 위험 요소

| 위험 | 대응 |
|--|--|
| Splash 씬 없이 Login/Game 씬을 직접 열어 테스트 시 UIManager.Instance = null | null-safe 호출 패턴 (`UIManager.Instance?.ShowConfirm(...)`) 사용 |
| AnonymousWarningPopup rename으로 기존 씬 참조 깨짐 | 씬에서 제거하므로 문제 없음. 코드 참조는 Find & Replace |
| Firebase 초기화 시점 변경으로 Login 씬에서 Firebase 미준비 상태 | SplashBootstrapper에서 await로 완료 대기 후 Login 전환 |
| ToastUI Lobby → Splash 이동 시 Lobby 직접 진입 테스트에서 ToastUI 없음 | null-safe `ToastUI.Show()` 이미 적용됨 — 안전하게 무시 |

---

## 작업 순서

1. IUIManager + UIManager 코드 작성
2. SplashBootstrapper 코드 작성
3. LoginBootstrapper / GameBootstrapper 코드 수정
4. Game.unity에서 ConfirmPopup 프리팹 추출
5. Lobby.unity에서 ToastUI / LoadingIndicator 프리팹 추출
6. Splash.unity 씬 생성 + 프리팹 배치 + Inspector 연결 (에디터 작업)
7. Login / Lobby / Game.unity 씬에서 중복 UI 제거 (에디터 작업)
8. 빌드 세팅 등록 (Splash를 index 0으로)
