# Research — 전역 UI 시스템 도입

## 이 작업이 왜 필요한가

현재 ConfirmPopup, LoadingIndicator 등 공통 UI가 씬마다 중복 배치되어 있다.
디자인 변경 시 모든 씬을 열어 수동으로 맞춰야 하고, 코드 중복도 발생한다.
전역 UIManager를 도입해 공통 UI를 한 곳에서 관리하고, Splash 씬에서 초기화하는 구조로 개선한다.

---

## 현재 상태 분석

### 씬별 중복 배치 현황

| UI | Login | Lobby | Game | 비고 |
|--|--|--|--|--|
| ConfirmPopup | ✅ | ✅ | ✅ | 3개 씬 모두 동일한 컴포넌트 중복 배치 |
| AnonymousWarningPopup | ✅ | - | - | Login 전용이지만 역할(단순 확인)은 범용 |
| LoadingIndicator | ✅ | - | - | LoginBootstrapper.ShowLoading()이 SetActive로 제어 중 (Rule 5 위반) |
| ToastUI | - | ✅ | ✅ | Lobby.unity에서 DontDestroyOnLoad로 이미 전역 동작 중 |

### ToastUI 현황 (중요)
`ToastUI.cs`는 이미 `DontDestroyOnLoad` 패턴으로 전역 동작 중이다.
Lobby.unity의 [UI] Canvas 하위에 배치되어 있고, 정적 `ToastUI.Show(ToastKey)`로 어느 씬에서든 호출 가능하다.
**Splash 씬으로 이동만 하면 전역 UI 시스템과 통합된다.**

### ConfirmPopup 현황
- `LoginBootstrapper.cs` 라인 175: `[SerializeField] private ConfirmPopup _confirmPopup;`
- `GameBootstrapper.cs` 라인 175: `[SerializeField] private ConfirmPopup _confirmPopup;`
- 두 Bootstrapper 모두 Inspector에서 씬 내 ConfirmPopup을 직접 주입 중

### AudioManager 현황 (참고 패턴)
- `SingletonMonoBehaviour<AudioManager>` 상속 → DontDestroyOnLoad 자동 적용
- Login.unity의 [Audio] GameObject에 부착
- `AudioManager.Initialize(soundConfig)` 를 LoginBootstrapper에서 호출
- 외부 호출: `AudioManager.Instance?.PlaySfx(...)` — null-safe 정적 접근
- **UIManager도 동일한 패턴 기반으로 설계하되, Splash 씬으로 이동**

---

## 전역화 대상 UI 확정

| UI | 프리팹 기준 씬 | 역할 |
|--|--|--|
| ConfirmPopup | Game.unity | Y/N 선택 팝업 |
| LoadingIndicator | Lobby.unity | 비동기 요청 대기 스피너 |
| ToastUI | Lobby.unity | 가이던스/알림 메시지 — 이미 전역 동작 중, Splash로 이동만 필요 |

**AnonymousWarningPopup — 전역화 제외**:
버튼이 "계정 만들기" / "익명으로 계속" 두 개이며 각 버튼에 Login 씬 전용 로직이 하드코딩되어 있다.
범용 팝업이 아닌 Login 씬 전용 UI이므로 전역화 대상에서 제외한다.

---

## 아키텍처 결정사항

### 1. 초기화 시점 — Splash 씬 신규 추가
```
Splash.unity → UIManager / AudioManager / Firebase 초기화 → Login.unity
```
- 부트 씬 + 스플래시 로고를 동시에 처리
- 전역 시스템이 Splash.unity 한 곳에 집중됨
- AudioManager도 Login.unity에서 Splash.unity로 이동

### 2. Canvas 구조 — 2개 분리
```
UIManager Canvas (SortingOrder: 100)    ← ConfirmPopup, AlertPopup, LoadingIndicator
Toast Canvas     (SortingOrder: 200)    ← ToastUI 전용 (항상 최상위)
```
- Canvas를 최소화하여 드로우콜 절감
- ToastUI는 팝업 위에도 표시되어야 하므로 별도 Canvas 필요

### 3. 레이어 귀속 — Presentation 레이어
- `IUIManager` 인터페이스: Presentation 레이어
- `UIManager` 구현체: Presentation 레이어 (`SingletonMonoBehaviour<UIManager>` 상속)
- Application(UseCase)은 UIManager를 모름 — 레이어 의존성 방향 준수
- 로딩 표시 필요 시: UseCase가 이벤트/결과 반환 → View가 UIManager 호출

### 4. 참조 방식 — Bootstrap에서만 Instance 접근
```csharp
// Bootstrap (모든 레이어 접근 가능한 Composition Root)
var uiManager = UIManager.Instance;
_loginRootView.Initialize(_loginUseCase, uiManager);

// View (IUIManager 인터페이스로만 사용)
_uiManager.ShowConfirm("정말 포기하시겠습니까?", onConfirm: () => { ... });
```

---

## 영향 범위

| 파일 | 변경 내용 |
|--|--|
| AnonymousWarningPopup.cs | AlertPopup.cs로 rename + 범용화 |
| LoginBootstrapper.cs | ConfirmPopup SerializeField 제거, UIManager 주입으로 교체. AudioManager.Initialize 제거(Splash로 이동) |
| GameBootstrapper.cs | ConfirmPopup SerializeField 제거, UIManager 주입으로 교체 |
| Login.unity | ConfirmPopup, AnonymousWarningPopup, LoadingIndicator, [Audio] GameObject 제거 |
| Lobby.unity | ConfirmPopup, ToastUI Canvas 제거 |
| Game.unity | ConfirmPopup 제거 |
| ToastUI.cs | Lobby.unity → Splash.unity로 이동 (DontDestroyOnLoad 구조는 유지) |
| AudioManager.cs | Login.unity → Splash.unity로 이동 |

**신규 파일**:
- `Assets/_Project/Scripts/Presentation/UI/UIManager.cs`
- `Assets/_Project/Scripts/Presentation/UI/Core/IUIManager.cs`
- `Assets/_Project/Scripts/Bootstrap/SplashBootstrapper.cs`
- `Assets/_Project/Scenes/Splash.unity`
