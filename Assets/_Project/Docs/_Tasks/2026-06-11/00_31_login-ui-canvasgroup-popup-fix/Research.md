# Research — Login UI CanvasGroup 전환 + 에러 팝업 분리 + UI 레이어 버그 수정

## 이 작업이 하는 일

로그인 화면의 패널 전환 방식이 프로젝트 규칙(Rule 5)을 어기고 있어서 수정하는 작업이다.
또한 앱 종료 팝업과 네트워크 에러 팝업이 같은 오브젝트를 공유하는 구조를 분리하고,
선언만 되어 있고 사용하지 않는 변수를 정리한다.

2026-06-22 추가: LoadingIndicator가 SafeArea 제약을 받는 문제(BUG-A)와
AnonymousWarningPopup이 UIManager의 BlockingOverlay에 가려지는 문제(BUG-B)를 이 작업에 포함한다.

---

## 현재 상태 분석

### 문제 1: LoginRootView — SetActive 직접 사용 (Rule 5 위반)

**파일**: `Assets/_Project/Scripts/Presentation/UI/Views/Login/LoginRootView.cs`

`HideAll()` (라인 183~188)과 `SetActivePanel()` (라인 300~307)에서 5개 패널에 대해
`SafeSetActive(go, bool)` → 내부적으로 `go.SetActive(active)` 를 직접 호출하고 있다.

```csharp
// HideAll() — 현재 코드
SafeSetActive(_loginSelectPanel, false);
SafeSetActive(_emailLoginPanel, false);
SafeSetActive(_signUpPanel, false);
SafeSetActive(_emailVerifyPanel, false);
SafeSetActive(_passwordResetPanel, false);

// SetActivePanel() — 현재 코드
SafeSetActive(_loginSelectPanel, panel == LoginPanel.LoginSelect);
// ... 5개 패널 전부 동일 패턴
```

**문제**: GameSystemRules_UI.md 규칙 5는 `SetActive(false)` 대신
`CanvasGroup.alpha=0 + blocksRaycasts=false + interactable=false` 패턴 사용을 요구한다.

**Login.unity 확인 결과**: 5개 패널 어디에도 CanvasGroup 컴포넌트가 없다.
CanvasGroup 컴포넌트 추가 + 코드 변경이 모두 필요하다.

---

### 문제 2: _confirmPopup / _networkErrorPopup 동일 오브젝트 공유

**파일**: `Assets/_Project/Scenes/Login.unity` — LoginRootView 컴포넌트
**Inspector 확인**: `_confirmPopup`(fileID: 422375806)과 `_networkErrorPopup`(fileID: 422375806)이 동일 오브젝트를 참조한다.

`ConfirmPopup.Show()` 코드(라인 156~192)를 보면 호출 시마다 메시지·라벨·콜백을 덮어쓴다.
앱 종료 팝업이 열린 상태에서 네트워크 오류가 발생하면 메시지와 onConfirm 콜백이 덮어씌워진다.

로그인 씬의 특성상 동시 발생 확률이 낮지만, 구조적으로 안전하지 않아 분리한다.

---

### 문제 3: _headerText 미사용 변수

**파일**: `Assets/_Project/Scripts/Presentation/UI/Views/Login/LoginRootView.cs` 라인 85

```csharp
[Header("공통 UI 요소")]
[Tooltip("모든 화면 공통의 헤더 텍스트(선택). null 허용.")]
[SerializeField] private TextMeshProUGUI _headerText;
```

`LoginRootView.cs` 전체에서 `_headerText`를 읽거나 쓰는 코드가 없다.
Inspector에서도 null(fileID: 0)이다. 사용하지 않는 필드이므로 제거한다.

---

## 영향 범위

| 파일 | 변경 유형 |
|------|----------|
| `LoginRootView.cs` | 코드 수정 (CanvasGroup 패턴, _headerText 제거) |
| `NetworkErrorPopup.cs` | 신규 생성 |
| `Login.unity` | Inspector 수정 (CanvasGroup 추가, 새 팝업 오브젝트 추가) |

---

## 참고: 로비 씬의 CanvasGroup 적용 사례

로비 씬은 2026-05-25 작업에서 동일한 SetActive → CanvasGroup 전환을 완료했다.
로그인 씬도 동일한 패턴을 따른다.

---

## 참고: 신규 NetworkErrorPopup 설계 방향

`ShowNetworkErrorPopup()`(LoginRootView.cs 라인 267~281)이 호출하는 팝업은
- 확인 버튼 1개 (cancelLabel: string.Empty로 취소 버튼 없음)
- onConfirm: null (버튼 클릭 시 팝업만 닫힘)

기존 `ConfirmPopup`과 UI 구조가 동일하므로 씬에 ConfirmPopup 오브젝트를 하나 더 추가하고
`_networkErrorPopup` 슬롯에 연결하는 방식으로 처리한다.
별도 스크립트를 새로 만들 필요 없다.

---

## 구현 완료 항목 (2026-06-22 기준 씬 직접 파악)

| 항목 | 상태 |
|------|------|
| LoginRootView CanvasGroup 전환 (코드) | ✅ 완료 |
| 5개 패널 Inspector CanvasGroup 슬롯 연결 | ✅ 완료 |
| NetworkErrorPopup 오브젝트 씬에 추가 | ✅ 완료 |
| `_networkErrorPopup` Inspector 슬롯 연결 | ❌ 미연결 |
| `_headerText` 미사용 변수 제거 | ✅ 완료 |

---

## 문제 4 (BUG-A): LoadingIndicator SafeArea 제약 문제

### 현재 씬 구조

```
UIManager Canvas (SortingOrder: 100)
└─ SafeAreaContainer (SafeAreaFitter)   ← LoadingIndicator가 이 안에 있음
    ├─ ConfirmPopup
    └─ LoadingIndicator (CanvasGroup)   ← ⚠️ 문제 위치
        ├─ Background (Image)
        └─ SafeAreaContainer (SafeAreaFitter)
            ├─ Spinner
            └─ StatusText
```

### 원인

LoadingIndicator가 UIManager Canvas의 SafeAreaContainer 안에 배치되어 있다.
SafeAreaFitter가 SafeAreaContainer를 기기의 Safe Area(노치/홈바 제외 영역)에 맞게 축소하기 때문에,
그 안에 있는 LoadingIndicator도 전체 화면을 커버하지 못하고 노치/홈바 영역이 노출된다.

### 적용 규칙

`GameSystemRules_UI.md` 규칙 4:
전체화면 배경/오버레이는 SafeAreaContainer 밖(Canvas 직속)에 두어야 한다.
BlockingOverlay가 Canvas 직속에 있는 것과 동일한 원칙.

### 해결 방향

LoadingIndicator를 `UIManager Canvas > SafeAreaContainer` 밖으로 꺼내
`UIManager Canvas` 직속으로 이동한다. (Inspector 작업)

---

## 문제 5 (BUG-B): AnonymousWarningPopup이 BlockingOverlay에 가려지는 문제

### 현재 씬 구조

```
Login Canvas (SortingOrder: 0)
└─ SafeAreaContainer (SafeAreaFitter)   ← fileID: 1438250021
    ├─ LoginRoot (LoginRootView)
    │   ├─ LoginSelectPanel
    │   ├─ EmailLoginPanel
    │   ├─ SignUpPanel
    │   ├─ EmailVerifyPanel
    │   └─ PasswordResetPanel
    ├─ AnonymousWarningPopup             ← ⚠️ Login Canvas(0) 안에 있음
    └─ NetworkErrorPopup

UIManager Canvas (SortingOrder: 100)
└─ SafeAreaContainer (SafeAreaFitter)
    ├─ ConfirmPopup
    └─ LoadingIndicator
```

### 원인

AnonymousWarningPopup.cs의 Show()는 `UIManager.Instance?.ShowBlockingOverlay()`를 호출한다.
UIManager의 BlockingOverlay는 UIManager Canvas(SortingOrder=100) 직속에 있다.
AnonymousWarningPopup은 Login Canvas(SortingOrder=0) 안에 있으므로,
UIManager Canvas의 BlockingOverlay가 화면에 표시되면 Login Canvas 전체가 가려진다.
결과적으로 AnonymousWarningPopup을 띄워도 BlockingOverlay 뒤에 숨어서 보이지 않는다.

ConfirmPopup은 UIManager Canvas > SafeAreaContainer 안에 있기 때문에 동일 문제가 없다.

### 컴포넌트 확인 (씬 파일 직접 파싱)

AnonymousWarningPopup GameObject에는 `Hexiege.Presentation.AnonymousWarningPopup` 컴포넌트가 붙어 있다.
ConfirmPopup 컴포넌트가 아님. Inspector 참조 필드: `_panel`, `_warningText`, `_createAccountButton`, `_continueAnonymousButton`.

Inspector에서 `_anonymousWarningPopup` 슬롯은 두 곳에서 참조 중:
- LoginRootView 컴포넌트 (fileID: 1904661451)
- 별도 컴포넌트 라인 11045 (fileID: 1904661451)

### 해결 방향

AnonymousWarningPopup 오브젝트를 Login Canvas의 SafeAreaContainer에서
UIManager Canvas의 SafeAreaContainer 안으로 이동한다.
이동 후 `_anonymousWarningPopup` Inspector 참조 슬롯을 재연결한다.
