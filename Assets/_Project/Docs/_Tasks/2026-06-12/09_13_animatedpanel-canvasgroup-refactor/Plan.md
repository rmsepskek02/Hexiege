# Plan — AnimatedPanel CanvasGroup 리팩토링

## 작업 목적 (자연어 설명)

AnimatedPanel과 UIAnimator에서 `SetActive` 호출을 제거하고,
CanvasGroup(alpha / blocksRaycasts / interactable)만으로 가시성을 제어하도록 변경합니다.
이를 통해 공통 UI 규칙 5("오브젝트를 비활성화하지 않는다")를 완전히 준수하고,
ToastUI와 동일한 방식으로 UI 가시성 제어를 통일합니다.

---

## 근거 규칙

- **공통 UI 규칙 5** (GameSystemRules_UI.md): "UI 요소를 숨길 때 SetActive(false) 대신 CanvasGroup을 사용한다."
  - 규칙 핵심 의도: 오브젝트가 비활성화되지 않도록 한다.
  - DOTween 애니메이션 오브젝트에 특히 적용되어야 하는 규칙.

---

## 변경 파일 및 수정 내용

### 1. `UIAnimator.cs`

**[Show 계열 — SetActive(true) 제거, interactable=true 추가]**

| 메서드 | 현재 | 변경 후 |
|--------|------|---------|
| `PopupShow` | 첫 줄 `cg.gameObject.SetActive(true)` | 제거. `cg.alpha = 0`, `cg.interactable = true` 설정 |
| `SlideInFromBottom` | 첫 줄 `cg.gameObject.SetActive(true)` | 제거. `cg.alpha = 0`, `cg.interactable = true` 설정 |
| `SlideInFromTop` | 첫 줄 `cg.gameObject.SetActive(true)` | 제거. `cg.alpha = 0`, `cg.interactable = true` 설정 |

> `interactable=true` 추가 이유: EnsureInitialized에서 `interactable=false`로 초기화된 이후,
> Show 계열이 복원하지 않으면 패널이 표시돼도 버튼 입력이 차단됨.

**[Hide 계열 — OnComplete의 SetActive(false) 제거, interactable=false 추가]**

| 메서드 | 현재 | 변경 후 |
|--------|------|---------|
| `PopupHide` | OnComplete: `blocksRaycasts=false`, `SetActive(false)` | `SetActive(false)` 제거. `interactable=false` 추가 |
| `SlideOutToBottom` | OnComplete: `blocksRaycasts=false`, `SetActive(false)` | `SetActive(false)` 제거. `interactable=false` 추가 |
| `SlideOutToTop` | OnComplete: `blocksRaycasts=false`, `SetActive(false)` | `SetActive(false)` 제거. `interactable=false` 추가 |

---

### 2. `AnimatedPanel.cs`

**[EnsureInitialized — CanvasGroup 초기 상태 명시 추가]**

현재 `IsVisible = false`만 설정하고, 오브젝트의 초기 숨김 상태를 씬의 SetActive=false에 의존.
변경 후 오브젝트는 항상 active이므로, 초기화 시 CanvasGroup 값을 명시적으로 설정.

```
추가:
  _cg.alpha = 0f;
  _cg.blocksRaycasts = false;
  _cg.interactable = false;
```

**[_backgroundOverlay — SetActive → CanvasGroup 방식으로 전환]**

`_backgroundOverlay` 필드는 이미 `CanvasGroup` 타입(86행)이므로 SetActive 없이 전환 가능.

| 위치 | 현재 | 변경 후 |
|------|------|---------|
| `Show()` 183행 | `_backgroundOverlay.gameObject.SetActive(true)` | `_backgroundOverlay.alpha = 1f` `_backgroundOverlay.blocksRaycasts = true` `_backgroundOverlay.interactable = true` |
| `Hide()` 229행 | `_backgroundOverlay.gameObject.SetActive(false)` | `_backgroundOverlay.alpha = 0f` `_backgroundOverlay.blocksRaycasts = false` `_backgroundOverlay.interactable = false` |

**[클래스/메서드 주석 갱신]**

- 19행: "Awake()에서 gameObject.SetActive(false) 호출" → EnsureInitialized에서 CanvasGroup으로 초기화하는 방식으로 설명 갱신
- 112~115행: "오브젝트가 비활성 상태에서 시작하면 Awake()가 호출되지 않으므로" → 씬에서 항상 active 상태로 시작하는 구조로 맥락 갱신

---

### 3. `ConfirmPopup.cs`

**[_panel.gameObject.SetActive(true) 제거]**

- 190행 `_panel.gameObject.SetActive(true)`: UIAnimator가 SetActive(true)를 호출한다는 전제로 작성된 방어 코드. 리팩토링 후 불필요하므로 제거.
- 30~32행, 185~192행 주석: SetActive(true) 호출 전제로 작성된 설명을 실제 동작에 맞게 갱신.

**[_blockingOverlay — 이번 작업 범위 제외]**

`_blockingOverlay`는 `GameObject` 타입(62행)으로 CanvasGroup이 부착되어 있지 않음.
전환하려면 CanvasGroup 컴포넌트 추가 및 Inspector 연결까지 필요하여 별도 작업으로 분리.
(Layout Group 밖에 있고 Update 실행이 불필요한 단순 입력 차단 오브젝트이므로
SetActive 사용이 기술적으로 즉각적인 문제를 일으키지 않음 — 아래 후속 작업 항목 참조)

---

### 4. Inspector 작업 (씬 오브젝트)

AnimatedPanel이 부착된 오브젝트들이 씬에서 초기 상태 SetActive=false로 설정되어 있다면,
**SetActive=true로 변경**해야 합니다 (초기 숨김은 EnsureInitialized의 CanvasGroup 설정으로 대체).

대상 추정 목록 (구현 에이전트가 씬 실물 확인 후 최종 확정):
- ProductionPanelUI, BuildingPlacementUI, BuildingActionPanelUI
- GameEndUI, RematchRequestPopup, ConfirmPopup, InGameSettingsUI

> ⚠️ 위 목록은 스크립트 참조 기반 추정이며, 씬 Inspector 실물 확인이 필수.

---

## 변경하지 않는 것

- `ToastUI.cs`: 이미 CanvasGroup 방식으로 올바르게 구현됨. 수정 불필요.
- AnimatedPanel의 `Show()` / `Hide()` 공개 인터페이스: 호출부 코드 변경 없음.
- 애니메이션 타입, 지속 시간, Ease 등 모든 애니메이션 파라미터: 변경 없음.
- `ConfirmPopup._blockingOverlay`: 별도 작업으로 분리 (아래 참조).

---

## 예상 위험 요소

| 위험 | 대응 |
|------|------|
| 씬에서 오브젝트가 SetActive=false로 시작하면 EnsureInitialized가 호출되지 않아 초기 상태 미적용 | EnsureInitialized를 Show()/Hide() 양쪽에서 호출하는 구조가 이미 존재 — 문제 없음. 단 씬 오브젝트는 active로 전환 필요 |
| Hide 완료 후 alpha=0이지만 오브젝트가 active라 레이캐스트를 받을 수 있음 | blocksRaycasts=false, interactable=false로 완전 차단 |
| 빠른 Show→Hide 전환 시 _currentSeq?.Kill() 이후 CanvasGroup 상태 잔여 | Kill() 후 새 애니메이션 시작 시 alpha/blocksRaycasts/interactable 즉시 재초기화하므로 문제 없음 |

---

## 작업 순서

1. `UIAnimator.cs` 수정 (Show 3개 + Hide 3개)
2. `AnimatedPanel.cs` 수정 (EnsureInitialized + _backgroundOverlay + 주석)
3. `ConfirmPopup.cs` 수정 (_panel.gameObject.SetActive(true) 제거 + 주석 갱신)
4. 씬 내 AnimatedPanel 부착 오브젝트 active 상태 확인 및 Inspector 작업
5. 사용자 테스트 (팝업 열기/닫기, 빠른 전환, timeScale=0 환경)

---

## ⏭️ 후속 작업 (이번 범위 외 — 별도 진행)

### ConfirmPopup._blockingOverlay CanvasGroup 전환

**대상 파일:** `Assets/_Project/Scripts/Presentation/UI/ConfirmPopup.cs`

**현재 상태:**
- `_blockingOverlay`가 `GameObject` 타입(62행)으로 선언됨
- Show() 182~183행: `_blockingOverlay.SetActive(true)`
- Hide() 202~203행: `_blockingOverlay.SetActive(false)`

**필요 작업:**
1. 씬의 _blockingOverlay 오브젝트에 `CanvasGroup` 컴포넌트 추가 (Inspector)
2. `ConfirmPopup.cs`의 `_blockingOverlay` 필드 타입을 `GameObject` → `CanvasGroup`으로 변경
3. SetActive 호출을 alpha/blocksRaycasts/interactable 방식으로 교체
4. Inspector에서 CanvasGroup 참조 재연결

**분리 이유:** CanvasGroup 컴포넌트 추가 및 Inspector 재연결이 필요해 이번 작업과 별개로 진행하는 것이 범위 명확성 유지에 유리함.
