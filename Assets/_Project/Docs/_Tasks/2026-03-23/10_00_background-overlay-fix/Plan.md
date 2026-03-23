# Plan — 반투명 배경 오버레이 애니메이션 버그 수정

**작업일:** 2026-03-23
**작업자:** Claude
**상태:** 승인 대기

---

## 목표

각 패널의 Background 오브젝트를 AnimatedPanel 슬라이드 대상에서 분리하여,
배경이 애니메이션 없이 즉시 표시/숨김되도록 수정.

---

## 구현 방법

### 공유 Background 방식 (단순화)

어차피 한 번에 하나의 패널만 열리므로, Canvas 직속에 이미 있는 `Background` 하나를
모든 패널이 공유하는 방식으로 단순화.

**변경 전:**
```
[UI] Canvas
  ├─ Background  ← Canvas 직속 (기존에 이미 있었음, 미연결 상태)
  ├─ BuildingPopup (AnimatedPanel)
  │   └─ Background  ← 자식 → 슬라이드 따라감 (삭제 대상)
  ├─ ProductionPopup (AnimatedPanel)
  │   └─ Background  ← 자식 → 슬라이드 따라감 (삭제 대상)
  └─ GameEndPanel (AnimatedPanel)
      └─ Background  ← 자식 → 슬라이드 따라감 (삭제 대상, 있는 경우)
```

**변경 후:**
```
[UI] Canvas
  ├─ Background  ← CanvasGroup 부착, SetActive(false), 모든 패널이 공유
  ├─ BuildingPopup (AnimatedPanel) → _backgroundOverlay = 공유 Background
  ├─ ProductionPopup (AnimatedPanel) → _backgroundOverlay = 공유 Background
  └─ GameEndPanel (AnimatedPanel) → _backgroundOverlay = 공유 Background
```

---

## 작업 목록

### [1] SharedBackgroundButton.cs 신규 스크립트

공유 Background에 부착. 현재 열린 패널의 Close 콜백을 등록/해제.

```
Assets/_Project/Scripts/Presentation/UI/Common/SharedBackgroundButton.cs
```

- `Register(Action onClose)` : 패널 Show 시 호출 → 클릭 시 해당 패널 닫기
- `Unregister()` : 패널 Hide 시 호출 → 클릭 무효화
- `OnClick()` : Button onClick에 연결

### [2] BuildingPlacementUI.cs / ProductionPanelUI.cs 수정

- `_backgroundButton` 필드 → `_sharedBackground` (SharedBackgroundButton) 필드로 교체
- `Initialize()` 내 버튼 리스너 등록 제거
- `Show()` 또는 `_popup.Show()` 전후에 `_sharedBackground.Register(Close)` 추가
- `Close()` 내에 `_sharedBackground.Unregister()` 추가

### [3] 에디터 1회성 스크립트 실행

스크립트: `Assets/Editor/FixBackgroundOverlay.cs`
메뉴: `Hexiege → UI → Fix Background Overlay`

수행 내용:
1. `[UI]/Background` CanvasGroup 추가 (없으면) + SetActive(false)
2. BuildingPopup / ProductionPopup / GameEndPanel 각각:
   - 자식 `Background` 삭제
   - AnimatedPanel `_backgroundOverlay` → 공유 Background CanvasGroup 연결
3. `[UI]/Background`에 Button 컴포넌트 추가 (없으면) + SharedBackgroundButton 연결

### [2] UIGuidelines.md 계층 예시 수정

Section 3 씬 계층 예시를 공유 Background 구조로 수정:

```
[UI] Canvas
  ├─ Background           ← CanvasGroup 부착, 초기 비활성, 모든 패널 공유
  ├─ ProductionPopup      ← AnimatedPanel, _backgroundOverlay → Background
  └─ BuildingPopup        ← AnimatedPanel, _backgroundOverlay → Background
```

---

## 코드 변경

### AnimatedPanel.cs — Hide() 내 배경 비활성화 타이밍 변경

**변경 전:** `_backgroundOverlay.SetActive(false)`가 Hide 애니메이션 완료 콜백에 위치
→ 슬라이드 아웃이 끝나고 나서 배경이 사라짐

**변경 후:** `Hide()` 호출 즉시 `_backgroundOverlay.SetActive(false)` 처리
→ 슬라이드 시작과 동시에 배경이 즉시 사라짐

```
// 변경 전 (Hide 내부)
System.Action wrappedComplete = _backgroundOverlay != null
    ? () => { _backgroundOverlay.gameObject.SetActive(false); onComplete?.Invoke(); }
    : onComplete;

// 변경 후 (Hide 내부)
if (_backgroundOverlay != null)
    _backgroundOverlay.gameObject.SetActive(false);  // 즉시 비활성화
// wrappedComplete 래핑 불필요 → onComplete 그대로 전달
```

- `UIAnimator.cs` 수정 불필요
- 각 UI 스크립트 (.cs) 수정 불필요

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| Background의 Button(닫기 기능)이 ProductionPanelUI에서 `_backgroundButton`으로 참조됨 | 이동 후 Inspector 참조 재연결 필수 |
| Background 앵커가 부모 기준이라 이동 후 크기가 달라질 수 있음 | Canvas 직속 이동 후 Stretch 앵커(0,0,1,1) 재확인 |
| 레이어 순서(Sorting Order)가 바뀌어 Background가 콘텐츠 위로 올라올 수 있음 | Background를 Canvas 자식 목록에서 XxxPopup보다 앞에 배치 |

---

## 에디터 1회성 스크립트

Inspector 작업이므로 에디터 스크립트 불필요.
직접 Hierarchy 재구성 후 Inspector에서 참조 재연결.

---

## 예상 결과

- 패널 등장 시: Background 즉시 표시 → 패널 슬라이드 애니메이션 재생
- 패널 퇴장 시: 패널 슬라이드 아웃 완료 후 Background 즉시 숨김
- Background 슬라이드 없음
