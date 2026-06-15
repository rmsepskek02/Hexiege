# Research — AnimatedPanel CanvasGroup 리팩토링

## 작업 목적 (자연어 설명)

현재 AnimatedPanel과 UIAnimator는 팝업을 숨길 때 `SetActive(false)`를 사용합니다.
그런데 공통 UI 규칙 5의 핵심 의도는 "오브젝트가 비활성화되지 않도록 한다"이며,
이는 DOTween 애니메이션이 걸린 팝업에 오히려 더 필요한 규칙입니다.
이번 작업은 AnimatedPanel/UIAnimator에서 SetActive 호출을 제거하고,
ToastUI와 동일하게 CanvasGroup(alpha, blocksRaycasts, interactable)만으로
가시성을 제어하도록 통일하는 리팩토링입니다.

---

## 현재 코드 상태

### AnimatedPanel.cs
- 경로: `Assets/_Project/Scripts/Presentation/UI/Common/AnimatedPanel.cs`
- `EnsureInitialized()`: `IsVisible = false`만 설정. SetActive 호출 없음.
- `Show()`: UIAnimator 메서드를 호출 → UIAnimator 내부에서 `SetActive(true)` 호출
- `Hide()`: UIAnimator 메서드를 호출 → UIAnimator 내부 OnComplete에서 `SetActive(false)` 호출

### UIAnimator.cs
- 경로: `Assets/_Project/Scripts/Presentation/UI/Common/UIAnimator.cs`
- **Show 계열 3개**: 모두 첫 줄에서 `cg.gameObject.SetActive(true)` 호출
  - `PopupShow` (line 56)
  - `SlideInFromBottom` (line 114)
  - `SlideInFromTop` (line 175)
- **Hide 계열 3개**: 모두 OnComplete 내부에서 `cg.gameObject.SetActive(false)` 호출
  - `PopupHide` (line 91)
  - `SlideOutToBottom` (line 150)
  - `SlideOutToTop` (line 211)

### ToastUI.cs (비교 대상 — 이미 규칙 5 준수)
- SetActive 호출 없음
- 가시성을 `alpha / blocksRaycasts / interactable` 3개 값으로만 제어
- 코드 주석에 이유 명시: "루트가 비활성화되면 Update()가 멈춰 큐가 정지함"

---

## 규칙 위반 분석

### 공통 UI 규칙 5 (GameSystemRules_UI.md)
> "UI 요소를 숨길 때 SetActive(false) 대신 CanvasGroup을 사용한다."

- 규칙의 핵심 의도: **오브젝트가 비활성화되지 않도록 한다**
- Layout Group 공간 사라짐, Update 정지는 SetActive 사용 시 발생하는 부작용 설명이지 적용 범위 조건이 아님
- DOTween을 사용하는 오브젝트일수록 SetActive 대신 CanvasGroup이 필요

### 현재 방식의 문제
- Hide 애니메이션 완료 후 오브젝트가 실제로 비활성화됨 → 규칙 5 위반
- Show 시 SetActive(true) 호출이 필요한 구조 → 비활성화를 전제로 설계된 흐름

---

## 변경 방향

### 핵심 변경
오브젝트를 항상 active 상태로 유지하고, CanvasGroup 값만으로 가시성 제어.

| 상태 | alpha | blocksRaycasts | interactable | SetActive |
|------|-------|----------------|--------------|-----------|
| 숨김 (초기/Hide 완료) | 0 | false | false | 변경 없음 (항상 true) |
| 표시 (Show 완료) | 1 | true | true | 변경 없음 |

### 수정이 필요한 파일
1. **UIAnimator.cs**: Show 계열 SetActive(true) 제거, Hide 계열 SetActive(false) 제거
2. **AnimatedPanel.cs**: EnsureInitialized()에서 초기 CanvasGroup 상태 설정 추가

### 수정이 필요 없는 파일
- ToastUI.cs: 이미 CanvasGroup 방식으로 올바르게 구현됨

---

## 영향 범위

AnimatedPanel이 부착된 오브젝트 전체에 영향. 단, 동작 방식(Show/Hide 호출부)은 변경 없음.

| 대상 | AnimationType |
|------|--------------|
| ProductionPanelUI | SlideFromBottom |
| BuildingPlacementUI | SlideFromBottom |
| BuildingActionPanelUI | SlideFromBottom |
| GameEndUI | SlideFromTop |
| RematchRequestPopup | PopupFade |
| ConfirmPopup | PopupFade |
| InGameSettingsUI | SlideFromBottom |

---

## 주의 사항

- 현재 AnimatedPanel이 부착된 오브젝트들은 씬에서 **초기 상태가 비활성(SetActive=false)**으로 설정되어 있을 가능성 있음
- 코드 변경 후 씬에서 해당 오브젝트들을 **활성 상태(SetActive=true)**로 변경하고, CanvasGroup의 alpha=0으로 초기 숨김 상태를 대체해야 함
- Inspector 작업 (씬 오브젝트 상태 변경) 이 필요할 수 있음
