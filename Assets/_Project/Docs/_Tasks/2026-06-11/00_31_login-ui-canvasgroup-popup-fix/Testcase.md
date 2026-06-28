# Testcase.md — Login UI CanvasGroup 전환 + NetworkErrorPopup 분리

**작성일**: 2026-06-11
**대상 파일**:
- `Assets/_Project/Scripts/Presentation/UI/Views/Login/LoginRootView.cs`
- `Assets/_Project/Scripts/Editor/LoginUiSetup.cs`
- `Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs` (호환성 확인)
- `Assets/_Project/Scripts/Presentation/UI/ConfirmPopup.cs` (Show() 시그니처 교차 검증)

---

## QA 정적 분석

### 1. CanvasGroup Rule 5 준수 여부

| 검사 항목 | 결과 | 근거 |
|-----------|------|------|
| `SetActive(false/true)` 잔존 여부 | PASS | `LoginRootView.cs` 전체 grep 결과 `GameObject.SetActive()` 호출 없음. `SetActivePanel`은 내부 메서드명이며 주석(327행)에 `SetActive(false)를 쓰지 않아도`라는 설명 문구만 존재 |
| `HideAll()` 구현 | PASS | 5개 CanvasGroup 전부 `HideGroup()` 호출 후 `_currentPanel = LoginPanel.None` 설정 (175-183행) |
| `ShowGroup()` 구현 | PASS | `alpha=1f`, `blocksRaycasts=true`, `interactable=true` 3개 속성 모두 설정 (316-322행) |
| `HideGroup()` 구현 | PASS | `alpha=0f`, `blocksRaycasts=false`, `interactable=false` 3개 속성 모두 설정 (328-335행) |
| null 안전 처리 | PASS | `ShowGroup()`, `HideGroup()` 모두 `if (cg == null) return;` 가드 존재 |

### 2. `SetActivePanel()` 로직 검증

| 검사 항목 | 결과 | 근거 |
|-----------|------|------|
| `HideAll()` 후 `_currentPanel` 재설정 | PASS | 293-309행: `HideAll()` 호출 → `_currentPanel = panel` 로 덮어씀. 주석(296-298행)에 의도 명시 |
| switch 5개 케이스 전부 처리 | PASS | `LoginSelect`, `EmailLogin`, `SignUp`, `EmailVerify`, `PasswordReset` 5개 케이스 모두 존재 (303-309행) |
| `LoginPanel.None` 케이스 처리 | PASS | `None` 케이스는 switch에 없으나 의도된 설계 — `None` 패널은 표시 대상이 없으므로 `HideAll()` 상태 유지가 올바름 |
| `PushCurrentToStack()`에서 `None` push 방지 | PASS | 284-287행: `if (_currentPanel != LoginPanel.None)` 가드로 `None` push 차단 |
| `HandleBack()` pop 후 재push 없음 | PASS | 195-199행: `_backStack.Pop()` → `SetActivePanel(prev)` 호출. `PushCurrentToStack()` 미호출 — Back 방향 이동은 스택에 쌓이지 않는 단방향 동작으로 올바름 |

### 3. `_headerText` 완전 제거 여부

| 검사 항목 | 결과 | 근거 |
|-----------|------|------|
| `_headerText` 필드 선언 제거 | PASS | `LoginRootView.cs` 전체 grep 결과 `_headerText` 문자열 없음 |
| `[Header]`, `[Tooltip]` 잔존 여부 | PASS | 관련 어트리뷰트 없음 |
| `using TMPro` 잔존 여부 | PASS | 21-24행 using 목록에 `TMPro` 없음 |

### 4. `LoginUiSetup.cs` 로직 검증

| 검사 항목 | 결과 | 근거 |
|-----------|------|------|
| 패널 이름 배열 5개 | PASS | `PanelObjectNames` 5개: `LoginSelectPanel`, `EmailLoginPanel`, `SignUpPanel`, `EmailVerifyPanel`, `PasswordResetPanel` (33-39행) |
| SerializedProperty 이름 배열 5개 | PASS | `PanelPropertyNames` 5개: `_loginSelectPanel`, `_emailLoginPanel`, `_signUpPanel`, `_emailVerifyPanel`, `_passwordResetPanel` (42-49행) |
| 배열 1:1 매칭 여부 | PASS | 순서 및 개수 일치. `LoginRootView.cs` 필드명(57-69행)과 완전 일치 |
| 초기 표시 상태 설정 | PASS | `InitiallyVisiblePanel = "LoginSelectPanel"` (53행). `isVisible` 분기로 `LoginSelectPanel`만 `alpha=1, blocksRaycasts=true, interactable=true`, 나머지 `alpha=0, false, false` (135-139행) |
| NetworkErrorPopup 복제 후 부모 지정 | PASS | `Instantiate(confirmPopupGo)` 후 `networkPopupGo.transform.SetParent(safeAreaGo.transform, worldPositionStays: false)` (184행). `worldPositionStays: false`로 RectTransform 로컬 좌표 유지 |
| `_networkErrorPopup` 슬롯 타입 일치 | PASS | `LoginRootView._networkErrorPopup`은 `ConfirmPopup` 타입(79행). `LoginUiSetup`이 `GetComponent<ConfirmPopup>()`으로 가져온 후 연결(193-200행) — 타입 완전 일치 |
| `_networkErrorPopup` 슬롯 연결 | PASS | `rootViewSo.FindProperty("_networkErrorPopup")` 후 `objectReferenceValue = networkPopup` (197-200행) |
| `ApplyModifiedProperties()` 호출 위치 | PASS | 219행에서 모든 루프 및 NetworkErrorPopup 슬롯 연결 완료 후 단 1회 호출. 순서 정확 |
| 에러 처리 — 패널 미발견 시 | PASS | `continue`로 스킵하고 `LogWarning` (119-122행). 전체 흐름 중단 없음 |
| 에러 처리 — ConfirmPopup 미발견 시 | PASS | `LogWarning` 후 NetworkErrorPopup 생성 전체 건너뜀 (163-165행) |
| 에러 처리 — SafeAreaContainer 미발견 시 | 주의 | 부모 지정 실패 시 `LogWarning`만 출력하고 `networkPopupGo`는 씬 루트에 생성된 채 남음 (187-190행). 씬 루트에 팝업이 배치되면 SafeArea 밖에 위치할 수 있음 |
| `EditorSceneManager.SaveScene()` 호출 | PASS | 228행에서 씬 저장. Dirty 마킹(225행) 후 저장 순서 올바름 |

### 5. LoginBootstrapper 호환성

| 검사 항목 | 결과 | 근거 |
|-----------|------|------|
| 패널 슬롯 직접 참조 여부 | PASS | `LoginBootstrapper.cs`에서 `CanvasGroup`, `_loginSelectPanel`, `_emailLoginPanel` 등 패널 슬롯 참조 없음. `_rootView` 단일 참조만 사용 |
| `ShowLoading()`에서 `SetActive` 사용 | 정보 | 209-212행: `_loadingIndicator.SetActive(show)` 는 로딩 인디케이터 전용이며 패널 전환과 무관. CanvasGroup Rule 5 위반 아님 |

### 6. ConfirmPopup.Show() 시그니처 일치 검증

`ConfirmPopup.Show()` 시그니처 (`ConfirmPopup.cs` 153-154행):
```
public void Show(string message, string confirmLabel, string cancelLabel, Action onConfirm, Action onCancel)
```

| 호출 위치 | 전달 인자 | 결과 |
|-----------|----------|------|
| `ShowQuitConfirm()` (231-236행) | message, confirmLabel, cancelLabel, onConfirm, onCancel 5개 모두 전달 | PASS |
| `ShowNetworkErrorPopup()` (269-274행) | `cancelLabel: string.Empty`, `onConfirm: null`, `onCancel: null` 5개 모두 전달 | PASS |

**주의 (Minor)**: `ConfirmPopup.Show()`는 `cancelLabel`이 `string.Empty`이더라도 취소 버튼을 숨기는 로직이 없음 (`ConfirmPopup.cs` 175-179행). 네트워크 오류 팝업 표시 시 빈 텍스트를 가진 취소 버튼이 시각적으로 노출될 수 있음. 실기 확인 필요.

### 종합 정적 분석 판정

모든 CanvasGroup Rule 5 항목 준수. `_headerText` 완전 제거 확인. `LoginBootstrapper` 호환성 이상 없음. `ConfirmPopup.Show()` 시그니처 일치.

Minor 이슈 2건:
1. `SafeAreaContainer` 미발견 시 팝업이 씬 루트에 생성되는 가능성
2. `ShowNetworkErrorPopup()`에서 `cancelLabel: string.Empty` 전달 시 취소 버튼이 빈 텍스트로 노출될 가능성

**정적 분석 판정: CONDITIONAL PASS**
- Critical/Major 버그 없음
- Minor 2건 (실기 확인 필요)

---

## 실기 테스트 케이스

### SINGLE-LOGIN-001: 에디터 스크립트 실행 후 씬 상태 확인

**전제:** Unity 에디터에서 Login.unity 씬이 열려 있고, LoginRootView 컴포넌트가 씬에 존재한다. 5개 패널 오브젝트(`LoginSelectPanel`, `EmailLoginPanel`, `SignUpPanel`, `EmailVerifyPanel`, `PasswordResetPanel`)와 `ConfirmPopup`, `SafeAreaContainer`가 씬에 존재한다.

**동작:**
1. Unity 메뉴에서 `Hexiege > Setup > Login UI — CanvasGroup + NetworkErrorPopup 설정`을 실행한다.
2. 완료 다이얼로그에서 `확인`을 누른다.
3. 씬 Hierarchy에서 각 패널 오브젝트를 선택하여 Inspector를 확인한다.
4. `LoginRootView` 컴포넌트의 Inspector를 확인한다.
5. `SafeAreaContainer` 하위에서 `NetworkErrorPopup`의 생성 여부를 확인한다.

**기댓값:**
- 완료 다이얼로그가 표시된다.
- `LoginSelectPanel`: CanvasGroup `alpha=1`, `blocksRaycasts=true`, `interactable=true`.
- `EmailLoginPanel`, `SignUpPanel`, `EmailVerifyPanel`, `PasswordResetPanel`: CanvasGroup `alpha=0`, `blocksRaycasts=false`, `interactable=false`.
- `LoginRootView`의 5개 패널 슬롯(`_loginSelectPanel` ~ `_passwordResetPanel`)에 해당 CanvasGroup이 연결되어 있다.
- `SafeAreaContainer` 하위에 `NetworkErrorPopup` 오브젝트가 생성되고, `LoginRootView._networkErrorPopup` 슬롯에 연결되어 있다.
- Console에 오류(Error) 로그가 없다.

**결과:** CONDITIONAL PASS (실기 실행 필요)

---

### SINGLE-LOGIN-002: 로그인 선택 화면 → 이메일 로그인 화면 전환

**전제:** Login.unity 씬에서 에디터 플레이모드 진입. 자동 로그인 실패 또는 Firebase 미연결 상태로 로그인 선택 화면(`LoginSelectPanel`)이 표시된 상태이다. SINGLE-LOGIN-001 완료 후.

**동작:**
1. 플레이모드에서 로그인 선택 화면의 이메일 로그인으로 이어지는 버튼을 클릭한다.
2. 화면 전환 후 현재 활성 패널을 확인한다.

**기댓값:**
- `LoginSelectPanel`의 CanvasGroup `alpha`가 `0`으로 변경되고 입력이 차단된다.
- `EmailLoginPanel`의 CanvasGroup `alpha`가 `1`로 변경되고 입력이 활성화된다.
- 레이아웃이 유지된다 — 패널 크기/위치가 변경되지 않는다. `SetActive`를 쓰지 않으므로 레이아웃 재계산 없음.
- Console에 null 참조 오류가 없다.

**결과:** CONDITIONAL PASS (실기 실행 필요)

---

### SINGLE-LOGIN-003: 이메일 로그인 화면 → 뒤로가기 → 로그인 선택 화면 복귀

**전제:** SINGLE-LOGIN-002 완료 후 `EmailLoginPanel`이 표시된 상태이다. Back 스택에 `LoginSelect`가 push되어 있다.

**동작:**
1. 키보드 ESC를 1회 누른다.
2. 현재 표시 패널을 확인한다.
3. Back 스택이 비어 있음을 간접 확인하기 위해 ESC를 1.5초 이내 1회 더 누른다.

**기댓값:**
- 첫 번째 ESC 입력 후 `EmailLoginPanel`이 숨겨지고(`alpha=0`) `LoginSelectPanel`이 표시된다(`alpha=1`).
- 현재 패널 상태가 `LoginSelect`로 설정된다.
- Back 스택이 비어 있으므로 이후 1.5초 이내 ESC를 1회 더 누르면 종료 확인 팝업이 표시된다.
- Console에 null 참조 오류가 없다.

**결과:** CONDITIONAL PASS (실기 실행 필요)

---

### SINGLE-LOGIN-004: ESC 2회 입력 → 앱 종료 팝업 (네트워크 오류 팝업과 독립 동작 확인)

**전제:** 로그인 선택 화면이 표시된 상태이다. Back 스택이 비어 있다. `_confirmPopup`과 `_networkErrorPopup`이 각각 별도 오브젝트로 연결되어 있다.

**동작:**
1. 키보드 ESC를 1회 누른다.
2. 1.5초 이내에 ESC를 1회 더 누른다.
3. 표시된 팝업을 확인한다.
4. 팝업에서 취소를 눌러 닫는다.
5. Hierarchy에서 `NetworkErrorPopup` 오브젝트가 `ConfirmPopup`과 별개인지 확인한다.

**기댓값:**
- 첫 번째 ESC 입력 후 팝업이 표시되지 않는다.
- 두 번째 ESC 입력 후 `앱을 종료하시겠습니까?` 팝업이 표시된다.
- 팝업에 `종료`와 `취소` 버튼이 표시된다.
- 취소를 눌러 팝업이 닫힌다.
- Hierarchy에서 `NetworkErrorPopup` 오브젝트가 `ConfirmPopup`과 별개로 `SafeAreaContainer` 하위에 존재한다.
- 앱 종료 팝업과 네트워크 오류 팝업이 서로 다른 오브젝트에 연결되어 있어 하나의 동작이 다른 팝업에 영향을 미치지 않는다.

**결과:** CONDITIONAL PASS (실기 실행 필요)

---

### SINGLE-LOGIN-005: 로딩 인디케이터 표시/숨김

**전제:** Login.unity 씬에서 에디터 플레이모드 진입. `_loadingIndicator` 오브젝트가 `LoginBootstrapper`에 연결되어 있다.

**동작:**
1. 플레이모드 진입 직후 로딩 인디케이터 상태를 확인한다. `InitializeAndDispatchAsync()` 시작 시 `ShowLoading(true)` 호출됨.
2. Firebase 초기화 완료 또는 실패 후 로그인 선택 화면이 표시될 때 로딩 인디케이터 상태를 확인한다. `ShowLoading(false)` 호출 시점.

**기댓값:**
- 플레이모드 진입 직후 로딩 인디케이터가 표시된다. `SetActive(true)` 적용.
- 로그인 선택 화면이 표시될 때 로딩 인디케이터가 숨겨진다. `SetActive(false)` 적용.
- 로딩 인디케이터는 CanvasGroup 패턴이 아닌 `SetActive` 방식을 사용하는데, 이는 패널 전환이 아닌 완전 비활성화 목적이므로 CanvasGroup Rule 5 대상 외이다.

**결과:** CONDITIONAL PASS (실기 실행 필요)

---

### SINGLE-LOGIN-006: 네트워크 오류 팝업 표시 시 취소 버튼 노출 여부 확인

**전제:** 로그인 선택 화면 또는 이메일 로그인 화면이 표시된 상태이다. 네트워크 오류를 트리거할 수 있는 Firebase 연결 실패 상황이거나, 직접 `_rootView.ShowNetworkErrorPopup()`을 에디터에서 호출 가능한 상태이다.

**동작:**
1. 네트워크 오류 상황을 발생시키거나 에디터 스크립트/Inspector에서 `ShowNetworkErrorPopup()`을 직접 호출한다.
2. 팝업이 표시된 후 버튼 영역을 확인한다.

**기댓값:**
- 팝업에 `네트워크 설정을 확인하고 다시 시도하세요.` 메시지가 표시된다.
- `확인` 버튼이 표시된다.
- 취소 버튼이 표시되지 않거나, 표시되더라도 빈 텍스트 버튼이 아닌 숨김 처리된 상태여야 한다.

**비고:** 정적 분석에서 `ConfirmPopup.Show()`는 `cancelLabel: string.Empty` 전달 시 취소 버튼을 숨기지 않음이 확인됨. 취소 버튼이 빈 텍스트로 노출될 수 있음 (Minor 이슈). 실기에서 버튼 오브젝트의 초기 활성 상태(씬 Prefab 설정)에 따라 결과가 달라질 수 있으므로 실기 확인 필수.

**결과:** CONDITIONAL PASS (실기 실행 필요 — 취소 버튼 노출 여부 직접 확인 필요)

---

## 버그 목록

| 심각도 | 설명 | 위치 |
|--------|------|------|
| Minor | `SafeAreaContainer`를 씬에서 찾지 못하면 `NetworkErrorPopup`이 씬 루트에 생성된 채 저장됨. `LogWarning`만 출력되므로 사용자가 인지하지 못하면 팝업 배치 오류로 이어질 수 있음 | `LoginUiSetup.cs` 186-190행 |
| Minor | `ShowNetworkErrorPopup()`이 `cancelLabel: string.Empty`를 전달하지만 `ConfirmPopup.Show()`는 취소 버튼을 숨기지 않아 빈 텍스트 버튼이 노출될 수 있음 | `LoginRootView.cs` 272행 / `ConfirmPopup.cs` 160-162행, 175-179행 |

---

## 종합 판정

**정적 분석 판정: CONDITIONAL PASS**

- CanvasGroup Rule 5 준수: `SetActive` 패널 전환 없음, 3개 속성 모두 정확히 처리
- `_headerText` 완전 제거: 선언, 어트리뷰트, `using TMPro` 모두 제거됨
- `SetActivePanel()` 분기 로직: 5개 케이스 처리, `None` push 방지, `_currentPanel` 재설정 순서 정확
- `LoginUiSetup.cs`: 배열 1:1 매칭, 초기 상태 설정, 슬롯 연결, `ApplyModifiedProperties()` 순서 모두 정확. `ConfirmPopup` 타입 일치
- `LoginBootstrapper` 호환성: 패널 슬롯 직접 참조 없어 타입 변경(GameObject → CanvasGroup) 영향 없음
- Minor 이슈 2건: `SafeAreaContainer` 미발견 시 NetworkErrorPopup 씬 루트 생성 가능성 / 네트워크 오류 팝업 취소 버튼 빈 텍스트 노출 가능성

**실기 테스트 판정: CONDITIONAL PASS** (6개 TC 모두 에디터 플레이모드 실행 필요)
