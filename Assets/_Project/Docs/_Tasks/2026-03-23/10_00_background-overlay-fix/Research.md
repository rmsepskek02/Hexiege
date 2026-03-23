# Research — 반투명 배경 오버레이 애니메이션 버그

**작업일:** 2026-03-23
**작업자:** Claude
**상태:** 완료

---

## 버그 증상

각종 UI 패널(생산 팝업, 건물 배치 팝업, 게임 종료 패널)의 반투명 배경(Overlay)이
패널 콘텐츠와 함께 슬라이드 애니메이션을 따라 움직임.

**기대 동작:** 배경은 애니메이션 없이 즉시 나타나고 사라져야 함.
**실제 동작:** 배경이 패널과 함께 위/아래로 슬라이드됨.

---

## 원인 분석

### 현재 씬 계층 구조 (문제)

```
ProductionPopup  ← AnimatedPanel 부착 (SlideFromBottom)
  ├─ Background  ← 자식 오브젝트 → 부모 슬라이드를 그대로 따라감 (버그)
  ├─ CancelButton
  └─ ProductionPanel
```

```
BuildingPopup  ← AnimatedPanel 부착 (SlideFromBottom)
  ├─ Background  ← 자식 오브젝트 → 부모 슬라이드를 그대로 따라감 (버그)
  ├─ CancelButton
  └─ BuildingPanel
```

```
GameEndPanel  ← AnimatedPanel 부착 (SlideFromTop)
  ├─ Background  ← 자식 오브젝트 → 부모 슬라이드를 그대로 따라감 (버그)
  ├─ ResultText
  └─ RestartButton
```

### 근본 원인

Unity UI에서 자식 오브젝트는 부모의 `anchoredPosition` 변화를 그대로 따라가므로,
AnimatedPanel이 슬라이드할 때 Background도 함께 이동함.

### 코드 레벨 분석 (AnimatedPanel.cs)

`AnimatedPanel._backgroundOverlay` 필드가 이미 올바른 해결책을 지원하고 있음:
- `Show()`: `_backgroundOverlay.gameObject.SetActive(true)` 즉시 (애니메이션 없음)
- `Hide()` 완료 콜백에서: `_backgroundOverlay.gameObject.SetActive(false)` 즉시

즉, **코드 변경 없이 Inspector 계층 재구성만으로 해결 가능**.

---

## UIGuidelines.md 오류 발견

현재 UIGuidelines.md Section 3 씬 계층 예시:

```
[UI] Canvas
  └─ ProductionPopup          ← AnimatedPanel 부착, BackgroundOverlay 연결
      ├─ Overlay              ← CanvasGroup 부착 (자식으로 표기 — 잘못됨)
      └─ ContentPanel
```

Overlay가 ProductionPopup의 자식으로 표기되어 있어서 슬라이드를 따라가는 구조.
→ **문서도 함께 수정 필요**

---

## 영향 범위

| 패널 | 파일 | 오버레이 오브젝트명 |
|------|------|-----------------|
| ProductionPopup | ProductionPanelUI.cs | Background |
| BuildingPopup | BuildingPlacementUI.cs | Background |
| GameEndPanel | GameEndUI.cs | Background |

---

## 변경 불필요한 것들

- `AnimatedPanel.cs` — 코드 수정 없음 (이미 올바르게 구현됨)
- `UIAnimator.cs` — 코드 수정 없음
- 각 UI 스크립트 (.cs 파일들) — `_popup.Show()` / `_popup.Hide()` 호출 방식 그대로
