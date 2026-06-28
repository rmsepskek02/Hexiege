# Plan — Login UI CanvasGroup 전환 + 에러 팝업 분리 + UI 레이어 버그 수정

## 이 작업이 하는 일

로그인 화면에서 다섯 가지를 수정한다.
1. 패널 전환 방식을 `SetActive` → CanvasGroup 패턴으로 교체 (프로젝트 규칙 준수) — **✅ 완료**
2. 앱 종료 확인 팝업과 네트워크 에러 팝업을 별도 오브젝트로 분리 — **✅ 완료**
3. 선언만 되고 사용하지 않는 `_headerText` 변수 제거 — **✅ 완료**
4. LoadingIndicator를 SafeAreaContainer 밖으로 이동하여 전체화면 커버 복구 (BUG-A)
5. AnonymousWarningPopup / NetworkErrorPopup이 UIManager의 BlockingOverlay에 가려지지 않도록 수정 (BUG-B)

---

## 규칙 근거

| 수정 항목 | 근거 규칙 |
|-----------|----------|
| SetActive → CanvasGroup 전환 | `GameSystemRules_UI.md` — 공통 UI 규칙 5 |
| 팝업 오브젝트 분리 | `GameSystemRules_UI.md` — 공통 UI 규칙 10 (팝업 중첩 안전성) |
| 미사용 변수 제거 | CLAUDE.md 규칙 6 (작업 범위 최소화, 불필요 코드 제거) |
| LoadingIndicator SafeArea 밖으로 이동 (BUG-A) | `GameSystemRules_UI.md` — 공통 UI 규칙 4 (전체화면 요소는 SafeAreaContainer 밖) |
| 팝업에 독립 Canvas 추가 (BUG-B) | `GameSystemRules_UI.md` — 규칙 4 (ToastUI처럼 독립 Canvas를 가진 UI 허용), 규칙 5 (UIManager BlockingOverlay 단일 소유 유지) |

---

## BUG-B 해결 설계

### 문제
AnonymousWarningPopup과 NetworkErrorPopup은 Login Canvas(SortingOrder=0) 안에 있다.
UIManager.ShowBlockingOverlay()를 호출하면 UIManager Canvas(SortingOrder=100)의 BlockingOverlay가 화면에 표시되는데,
이것이 Login Canvas 전체를 덮기 때문에 두 팝업이 BlockingOverlay 뒤에 가려진다.

### 해결 방향
두 팝업을 Login Canvas에 그대로 유지하되, 각 팝업 오브젝트에 **독립 Canvas 컴포넌트**를 추가하여
SortingOrder를 UIManager Canvas(100)보다 높게 설정한다. (SortingOrder=200)

Unity에서 GameObject에 Canvas 컴포넌트를 직접 붙이면 부모 Canvas의 SortingOrder를 무시하고
자신의 SortingOrder로 독립 렌더링된다. ToastUI가 동일한 방식을 사용하며, GameSystemRules_UI.md 규칙 4에도 명시된 패턴이다.

### 렌더링 순서 (수정 후)
```
(SortingOrder   0) Login Canvas          — 로그인 패널들
(SortingOrder 100) UIManager Canvas      — BlockingOverlay (화면 어둡게 + 입력 차단)
(SortingOrder 200) 팝업 자체 Canvas     — AnonymousWarningPopup / NetworkErrorPopup (BlockingOverlay 위에 표시)
```

### UIManager BlockingOverlay 단일 소유 원칙 유지
- 두 팝업이 자체 BlockingOverlay를 갖지 않는다 (자식 BlockingOverlay 오브젝트 삭제)
- UIManager.ShowBlockingOverlay() 호출 구조 그대로 유지

---

## 목표 씬 구조

```
Login Canvas (SortingOrder: 0)
└─ SafeAreaContainer (SafeAreaFitter)
    ├─ LoginRoot (LoginRootView)
    │   ├─ LoginSelectPanel (CanvasGroup)
    │   ├─ EmailLoginPanel (CanvasGroup)
    │   ├─ SignUpPanel (CanvasGroup)
    │   ├─ EmailVerifyPanel (CanvasGroup)
    │   └─ PasswordResetPanel (CanvasGroup)
    ├─ AnonymousWarningPopup [Canvas(200) + GraphicRaycaster]  ← 독립 Canvas 추가, BlockingOverlay 자식 삭제
    └─ NetworkErrorPopup     [Canvas(200) + GraphicRaycaster]  ← 독립 Canvas 추가, BlockingOverlay 자식 삭제

UIManager Canvas (SortingOrder: 100)
├─ BlockingOverlay
├─ LoadingIndicator (CanvasGroup)  ← BUG-A: SafeAreaContainer 밖으로 이동
│   ├─ Background (Image)
│   └─ SafeAreaContainer (SafeAreaFitter)
│       ├─ Spinner
│       └─ StatusText
└─ SafeAreaContainer (SafeAreaFitter)
    └─ ConfirmPopup
```

> **AnonymousWarningPopup 현재 위치 복원 필요**: 이전 잘못된 작업으로 UIManager Canvas로 이동되어 있음.
> Login Canvas > SafeAreaContainer로 되돌린 뒤 독립 Canvas를 추가한다.

---

## Step 1. `NetworkErrorPopup.cs` — 전용 스크립트 신규 작성 ✅ 미완료

**파일**: `Assets/_Project/Scripts/Presentation/UI/Views/Login/NetworkErrorPopup.cs`

AnonymousWarningPopup.cs를 참고하여 동일한 구조로 작성한다.

- `UIManager.Instance?.ShowBlockingOverlay()` — Modal 모드 (Show 시 호출)
- `UIManager.Instance?.HideBlockingOverlay()` — Hide 시 호출
- NetworkErrorPopup에 표시할 메시지와 확인 버튼만 있는 단순 구조

---

## Step 2. `LoginRootView.cs` — ShowNetworkErrorPopup() 수정 ✅ 미완료

**파일**: `Assets/_Project/Scripts/Presentation/UI/Views/Login/LoginRootView.cs`

현재 `ShowNetworkErrorPopup()`이 `UIManager.Instance?.ShowConfirm()`을 호출하고 있다.
NetworkErrorPopup 전용 스크립트가 생기므로, NetworkErrorPopup을 직접 Show()하도록 변경한다.

```csharp
// 변경 전
public void ShowNetworkErrorPopup()
{
    UIManager.Instance?.ShowConfirm(
        message: "네트워크 설정을 확인하고 다시 시도하세요.",
        ...);
}

// 변경 후
[SerializeField] private NetworkErrorPopup _networkErrorPopup;

public void ShowNetworkErrorPopup()
{
    _networkErrorPopup?.Show();
}
```

---

## Step 3. 에디터 스크립트 — Inspector 작업 자동화 ✅ 미완료

**파일**: `Assets/_Project/Scripts/Editor/LoginUiSetup.cs` (기존 파일 교체)

### 메뉴 항목 1: `Hexiege/Setup/Login UI — BUG-A: LoadingIndicator 이동`

1. UIManager Canvas 하위 SafeAreaContainer에서 LoadingIndicator를 찾는다
2. UIManager Canvas 직속으로 부모 변경 (Undo 등록)
3. RectTransform: anchorMin(0,0) / anchorMax(1,1) / offset(0,0) 전체화면 stretch

### 메뉴 항목 2: `Hexiege/Setup/Login UI — BUG-B: AnonymousWarningPopup 복원 및 Canvas 추가`

1. 현재 UIManager Canvas > SafeAreaContainer에서 AnonymousWarningPopup을 찾는다
2. Login Canvas > SafeAreaContainer로 부모 변경 (Undo 등록)
3. RectTransform: anchorMin(0,0) / anchorMax(1,1) / offset(0,0)
4. AnonymousWarningPopup에 Canvas 컴포넌트 추가 (SortingOrder=200, overrideSorting=true)
5. GraphicRaycaster 컴포넌트 추가
6. 자식 BlockingOverlay 오브젝트 삭제
7. LoginRootView의 `_anonymousWarningPopup` 슬롯 재연결

### 메뉴 항목 3: `Hexiege/Setup/Login UI — BUG-B: NetworkErrorPopup Canvas 추가`

1. Login Canvas > SafeAreaContainer에서 NetworkErrorPopup을 찾는다
2. Canvas 컴포넌트 추가 (SortingOrder=200, overrideSorting=true)
3. GraphicRaycaster 컴포넌트 추가
4. 자식 BlockingOverlay 오브젝트 삭제
5. NetworkErrorPopup에 NetworkErrorPopup.cs 컴포넌트 추가 (기존 ConfirmPopup 컴포넌트 제거)
6. LoginRootView의 `_networkErrorPopup` 슬롯 연결

---

## 구현 순서

```
[1] NetworkErrorPopup.cs 신규 작성 (Step 1)
      ↓
[2] LoginRootView.cs 수정 (Step 2)
      ↓
[3] LoginUiSetup.cs 에디터 스크립트 작성 (Step 3)
      ↓
[4] 사용자: Hexiege/Setup 메뉴 순서대로 실행
      ↓
[5] Login.unity 저장 후 플레이모드 테스트
```

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| LoadingIndicator가 DontDestroyOnLoad 오브젝트라 이름으로 못 찾을 수 있음 | `Resources.FindObjectsOfTypeAll<UIManager>()` 를 통해 UIManager 컴포넌트 → Canvas → LoadingIndicator 탐색 |
| AnonymousWarningPopup 참조가 씬에서 두 곳(LoginRootView 외 1곳) 확인됨 | 에디터 스크립트에서 모든 LoginRootView 컴포넌트를 탐색하여 재연결 |
| NetworkErrorPopup의 기존 ConfirmPopup 컴포넌트 제거 시 Inspector 참조 깨짐 | 에디터 스크립트에서 컴포넌트 교체 후 새 컴포넌트로 슬롯 재연결 |

---

## 구현 제외 항목

| 항목 | 이유 |
|------|------|
| ConfirmPopup 자체 구조 변경 | 현재 코드 정상 동작, 이 작업 범위 아님 |
| LoginSelectView / EmailLoginView 등 하위 View 수정 | CanvasGroup 전환은 LoginRootView가 담당 — 하위 View는 불변 |
