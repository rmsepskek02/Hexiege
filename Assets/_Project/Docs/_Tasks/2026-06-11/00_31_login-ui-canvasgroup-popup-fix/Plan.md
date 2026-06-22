# Plan — Login UI CanvasGroup 전환 + 에러 팝업 분리 + UI 레이어 버그 수정

## 이 작업이 하는 일

로그인 화면에서 다섯 가지를 수정한다.
1. 패널 전환 방식을 `SetActive` → CanvasGroup 패턴으로 교체 (프로젝트 규칙 준수) — **✅ 완료**
2. 앱 종료 확인 팝업과 네트워크 에러 팝업을 별도 오브젝트로 분리 — **✅ 완료 (Inspector 연결만 미완)**
3. 선언만 되고 사용하지 않는 `_headerText` 변수 제거 — **✅ 완료**
4. LoadingIndicator를 SafeAreaContainer 밖으로 이동하여 전체화면 커버 복구 (BUG-A)
5. AnonymousWarningPopup을 UIManager Canvas 안으로 이동하여 BlockingOverlay에 가려지지 않도록 수정 (BUG-B)

---

## 규칙 근거

| 수정 항목 | 근거 규칙 |
|-----------|----------|
| SetActive → CanvasGroup 전환 | `GameSystemRules_UI.md` — 공통 UI 규칙 5 |
| 팝업 오브젝트 분리 | `GameSystemRules_UI.md` — 공통 UI 규칙 10 (팝업 중첩 안전성) |
| 미사용 변수 제거 | CLAUDE.md 규칙 6 (작업 범위 최소화, 불필요 코드 제거) |
| LoadingIndicator SafeArea 밖으로 이동 (BUG-A) | `GameSystemRules_UI.md` — 공통 UI 규칙 4 (전체화면 요소는 SafeAreaContainer 밖) |
| AnonymousWarningPopup UIManager Canvas로 이동 (BUG-B) | `GameSystemRules_UI.md` — 공통 UI 규칙 4, 8 (Canvas SortingOrder 계층 일치) |

---

## Step 1. `LoginRootView.cs` — CanvasGroup 패턴 전환 + _headerText 제거

**파일**: `Assets/_Project/Scripts/Presentation/UI/Views/Login/LoginRootView.cs`

### 1-1. SerializeField 타입 변경

5개 패널 참조를 `GameObject` → `CanvasGroup`으로 변경한다.
Inspector에서 재연결이 필요하므로 에디터 스크립트(Step 3)로 처리한다.

```csharp
// 변경 전
[SerializeField] private GameObject _loginSelectPanel;
// ... 5개

// 변경 후
[SerializeField] private CanvasGroup _loginSelectPanel;
// ... 5개
```

### 1-2. _headerText 제거

아래 4줄을 삭제한다. Inspector에서도 null 상태였으므로 씬 파일 영향 없음.

```csharp
// 삭제
[Header("공통 UI 요소")]
[Tooltip("모든 화면 공통의 헤더 텍스트(선택). null 허용.")]
[SerializeField] private TextMeshProUGUI _headerText;
```

`using TMPro;` 선언도 _headerText가 유일한 TMP 사용처인 경우 함께 제거한다.
(ConfirmPopup 등 다른 using 여부 확인 후 제거)

### 1-3. SafeSetActive 헬퍼 → ShowGroup / HideGroup 헬퍼로 교체

```csharp
// 삭제
private static void SafeSetActive(GameObject go, bool active)
{
    if (go != null) go.SetActive(active);
}

// 추가
private static void ShowGroup(CanvasGroup cg)
{
    if (cg == null) return;
    cg.alpha = 1f;
    cg.blocksRaycasts = true;
    cg.interactable = true;
}

private static void HideGroup(CanvasGroup cg)
{
    if (cg == null) return;
    cg.alpha = 0f;
    cg.blocksRaycasts = false;
    cg.interactable = false;
}
```

### 1-4. HideAll() 수정

```csharp
public void HideAll()
{
    HideGroup(_loginSelectPanel);
    HideGroup(_emailLoginPanel);
    HideGroup(_signUpPanel);
    HideGroup(_emailVerifyPanel);
    HideGroup(_passwordResetPanel);
    _currentPanel = LoginPanel.None;
}
```

### 1-5. SetActivePanel() 수정

```csharp
private void SetActivePanel(LoginPanel panel)
{
    // 전체 숨김 후 지정 패널만 표시
    HideAll();  // _currentPanel이 None으로 초기화되므로 별도 처리
    _currentPanel = panel;

    switch (panel)
    {
        case LoginPanel.LoginSelect:   ShowGroup(_loginSelectPanel);   break;
        case LoginPanel.EmailLogin:    ShowGroup(_emailLoginPanel);    break;
        case LoginPanel.SignUp:        ShowGroup(_signUpPanel);        break;
        case LoginPanel.EmailVerify:   ShowGroup(_emailVerifyPanel);   break;
        case LoginPanel.PasswordReset: ShowGroup(_passwordResetPanel); break;
    }
}
```

> **주의**: `HideAll()` 내부에서 `_currentPanel = LoginPanel.None`을 설정하므로
> `SetActivePanel()` 에서는 HideAll() 호출 후 `_currentPanel`을 panel 값으로 재설정해야 한다.

---

## Step 2. 에디터 스크립트 — CanvasGroup 추가 + NetworkErrorPopup 생성

### 목적

- 5개 패널 GameObject에 CanvasGroup 컴포넌트 추가
- NetworkErrorPopup 전용 ConfirmPopup 오브젝트를 씬에 추가
- LoginRootView Inspector 슬롯 재연결 (GameObject → CanvasGroup)

### 파일

`Assets/_Project/Scripts/Editor/LoginUiSetup.cs` (1회성 실행 후 삭제 가능)

### 메뉴 경로

`Hexiege/Setup/Login UI — CanvasGroup + NetworkErrorPopup 설정`

### 스크립트가 수행할 작업

1. `Login.unity` 씬이 열려 있는지 확인
2. 5개 패널 GameObject를 이름으로 찾아 `CanvasGroup` 컴포넌트 추가
   - 이미 있으면 건너뜀 (중복 방지)
   - 초기 상태 설정: LoginSelectPanel만 표시(alpha=1, blocksRaycasts=true, interactable=true), 나머지 숨김(alpha=0, false, false)
3. LoginRootView 컴포넌트의 패널 슬롯을 새로 추가된 CanvasGroup으로 재연결
4. ConfirmPopup 오브젝트(앱 종료용)를 기준으로 동일 구조의 NetworkErrorPopup 오브젝트를 복제하여 씬에 추가
   - GameObject 이름: `NetworkErrorPopup`
   - SafeAreaContainer 하위에 배치
5. LoginRootView의 `_networkErrorPopup` 슬롯에 새 NetworkErrorPopup 연결
6. `EditorSceneManager.MarkSceneDirty()` + `AssetDatabase.SaveAssets()`

### 대상 오브젝트 이름 (Login.unity 씬 기준)

| 슬롯 | 패널 이름 |
|------|----------|
| _loginSelectPanel | LoginSelectPanel |
| _emailLoginPanel | EmailLoginPanel |
| _signUpPanel | SignUpPanel |
| _emailVerifyPanel | EmailVerifyPanel |
| _passwordResetPanel | PasswordResetPanel |
| 기존 ConfirmPopup | (fileID: 422375806, 이름으로 탐색) |
| SafeAreaContainer | SafeAreaContainer |

---

## Step 3. 사용자 — 에디터 스크립트 실행

Unity Editor에서 메뉴 `Hexiege/Setup/Login UI — CanvasGroup + NetworkErrorPopup 설정`을 실행한다.
완료 후 Login.unity를 저장한다.

---

## Step 3. 에디터 스크립트 — _networkErrorPopup 슬롯 연결 (미완료 항목)

**파일**: `Assets/_Project/Scripts/Editor/LoginUiSetup.cs` (1회성 실행 후 삭제)

**메뉴 경로**: `Hexiege/Setup/Login UI — NetworkErrorPopup 슬롯 연결`

씬에는 NetworkErrorPopup 오브젝트가 이미 존재하지만 LoginRootView의 `_networkErrorPopup` 슬롯이 연결되어 있지 않다.
에디터 스크립트에서 NetworkErrorPopup 오브젝트를 이름으로 찾아 `_networkErrorPopup` 슬롯에 연결한다.

---

## Step 4. 에디터 스크립트 — BUG-A: LoadingIndicator 이동

**파일**: 동일 에디터 스크립트에 메뉴 항목 추가

**메뉴 경로**: `Hexiege/Setup/Login UI — BUG-A LoadingIndicator 이동`

### 수행할 작업

1. UIManager Canvas 하위에서 LoadingIndicator 오브젝트를 찾는다
2. LoadingIndicator의 부모를 `SafeAreaContainer`에서 `UIManager Canvas` 직속으로 변경한다
3. RectTransform을 전체화면 stretch로 설정한다 (anchorMin: 0,0 / anchorMax: 1,1 / offsetMin: 0,0 / offsetMax: 0,0)

### 목표 구조

```
UIManager Canvas (SortingOrder: 100)
├─ BlockingOverlay                    ← 기존 위치 유지
├─ LoadingIndicator (CanvasGroup)     ← SafeAreaContainer 밖으로 이동
│   ├─ Background (Image)
│   └─ SafeAreaContainer (SafeAreaFitter)
│       ├─ Spinner
│       └─ StatusText
└─ SafeAreaContainer (SafeAreaFitter)
    └─ ConfirmPopup
```

> **주의**: LoadingIndicator 내부의 SafeAreaContainer는 그대로 유지한다.
> Spinner와 StatusText는 SafeArea 범위 안에 표시되어야 하며, Background만 전체화면을 커버하면 된다.
> Background는 LoadingIndicator 직속이므로 LoadingIndicator가 전체화면이 되면 자동으로 전체화면 커버가 된다.

---

## Step 5. 에디터 스크립트 — BUG-B: AnonymousWarningPopup 이동

**메뉴 경로**: `Hexiege/Setup/Login UI — BUG-B AnonymousWarningPopup 이동`

### 수행할 작업

1. Login Canvas의 SafeAreaContainer에서 AnonymousWarningPopup 오브젝트를 찾는다
2. UIManager Canvas의 SafeAreaContainer 하위로 이동한다
3. RectTransform을 전체화면 stretch로 설정한다 (anchorMin: 0,0 / anchorMax: 1,1)
4. `_anonymousWarningPopup` Inspector 슬롯 참조를 재연결한다
   - LoginRootView 컴포넌트에서 AnonymousWarningPopup을 참조하는 슬롯을 새 위치의 오브젝트로 재연결

### 목표 구조

```
Login Canvas (SortingOrder: 0)
└─ SafeAreaContainer (SafeAreaFitter)
    ├─ LoginRoot (LoginRootView)
    │   └─ (5개 패널)
    └─ NetworkErrorPopup              ← 기존 위치 유지

UIManager Canvas (SortingOrder: 100)
├─ BlockingOverlay
└─ SafeAreaContainer (SafeAreaFitter)
    ├─ ConfirmPopup
    ├─ LoadingIndicator               ← BUG-A 수정 후 이 위치가 아닌 Canvas 직속
    └─ AnonymousWarningPopup          ← BUG-B: 여기로 이동
```

### 위험 요소

| 위험 | 대응 |
|------|------|
| AnonymousWarningPopup이 LoginRootView 외에 다른 곳에서도 참조될 가능성 | 씬 파싱에서 `_anonymousWarningPopup` 참조가 두 곳(fileID 기준) 확인됨 — 에디터 스크립트에서 모든 참조를 재연결 |
| 이동 후 RectTransform 초기화 필요 | 에디터 스크립트에서 명시적으로 설정 |

---

## 구현 순서

```
[1] LoginRootView.cs 수정 → ✅ 완료
      ↓
[2] 에디터 스크립트 작성 (Step 2 원본 + Step 3~5 추가)
      ↓
[3] 사용자가 에디터 스크립트 실행 (Step 3: NetworkErrorPopup 연결)
      ↓
[4] 사용자가 에디터 스크립트 실행 (Step 4: BUG-A LoadingIndicator 이동)
      ↓
[5] 사용자가 에디터 스크립트 실행 (Step 5: BUG-B AnonymousWarningPopup 이동)
      ↓
[6] 플레이모드에서 전체 동작 확인
```

---

## 위험 요소 (원본 Step 1~2)

| 위험 | 대응 |
|------|------|
| CanvasGroup 초기 alpha 설정 누락 | 에디터 스크립트에서 초기 상태 명시적으로 설정 |
| PushCurrentToStack에서 LoginPanel.None이 push될 가능성 | 기존 코드(라인 292)에 이미 `if (_currentPanel != LoginPanel.None)` 가드 존재 — 문제없음 |
| SetActivePanel → HideAll 연쇄 호출 시 _currentPanel 초기화 | Step 1-5에서 HideAll() 후 즉시 _currentPanel = panel 재설정으로 처리 |

---

## 구현 제외 항목

| 항목 | 이유 |
|------|------|
| ConfirmPopup 자체 구조 변경 | 현재 코드 정상 동작, 이 작업 범위 아님 |
| LoginSelectView / EmailLoginView 등 하위 View 수정 | CanvasGroup 전환은 LoginRootView가 담당 — 하위 View는 불변 |
