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

**[Show 계열 — SetActive(true) 제거]**

| 메서드 | 현재 | 변경 후 |
|--------|------|---------|
| `PopupShow` | 첫 줄 `cg.gameObject.SetActive(true)` | 제거. `cg.alpha = 0`으로 초기화만 유지 |
| `SlideInFromBottom` | 첫 줄 `cg.gameObject.SetActive(true)` | 제거. `cg.alpha = 0`으로 초기화만 유지 |
| `SlideInFromTop` | 첫 줄 `cg.gameObject.SetActive(true)` | 제거. `cg.alpha = 0`으로 초기화만 유지 |

**[Hide 계열 — OnComplete의 SetActive(false) 제거, interactable 추가]**

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

---

### 3. Inspector 작업 (씬 오브젝트)

AnimatedPanel이 부착된 오브젝트들이 씬에서 초기 상태 SetActive=false로 설정되어 있다면,
**SetActive=true로 변경**해야 합니다 (초기 숨김은 EnsureInitialized의 CanvasGroup 설정으로 대체).

> 구현 담당 에이전트가 씬 오브젝트 상태를 확인 후 Editor 스크립트 또는 수동 Inspector 작업으로 처리.

---

## 변경하지 않는 것

- `ToastUI.cs`: 이미 CanvasGroup 방식으로 올바르게 구현됨. 수정 불필요.
- AnimatedPanel의 `Show()` / `Hide()` 공개 인터페이스: 호출부 코드 변경 없음.
- 애니메이션 타입, 지속 시간, Ease 등 모든 애니메이션 파라미터: 변경 없음.

---

## 예상 위험 요소

| 위험 | 대응 |
|------|------|
| 씬에서 오브젝트가 SetActive=false로 시작하면 EnsureInitialized가 호출되지 않아 초기 상태 미적용 | EnsureInitialized를 Show()/Hide() 양쪽에서 호출하는 구조가 이미 존재 — 문제 없음. 단 씬 오브젝트는 active로 전환 필요 |
| Hide 완료 후 alpha=0이지만 오브젝트가 active라 레이캐스트를 받을 수 있음 | blocksRaycasts=false, interactable=false로 완전 차단 |
| 빠른 Show→Hide 전환 시 _currentSeq?.Kill() 이후 CanvasGroup 상태 잔여 | Kill() 후 새 애니메이션 시작 시 alpha/blocksRaycasts를 즉시 재초기화하므로 문제 없음 |

---

## 작업 순서

1. `UIAnimator.cs` 수정 (Show 3개 + Hide 3개)
2. `AnimatedPanel.cs` 수정 (EnsureInitialized)
3. 씬 내 AnimatedPanel 부착 오브젝트 active 상태 확인 및 Inspector 작업
4. 사용자 테스트 (팝업 열기/닫기, 빠른 전환, timeScale=0 환경)
