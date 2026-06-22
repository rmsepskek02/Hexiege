# Plan — Canvas SortingOrder 통일 (BlockingOverlay 렌더링 순서 수정)

## 이 작업이 하는 일

UIManager의 BlockingOverlay(반투명 배경)가 게임씬 패널 뒤에 표시되도록 렌더링 순서를 올바르게 수정한다.

문제 원인: 게임씬의 모든 UI(HUD + 패널)가 단일 `[UI]` Canvas(SortingOrder=0)에 포함되어 있어,
BlockingOverlay(UIManager Canvas SortingOrder=100)가 패널보다 위에 렌더링됨.

해결 방향: 패널들에 개별 Canvas(Override Sorting=true, SortingOrder=200)를 추가하여
BlockingOverlay(100) 위에 렌더링되도록 한다. 단, HUD는 `[UI]` Canvas(SortingOrder=0)에 유지하여
BlockingOverlay가 HUD를 가릴 수 있도록 한다.

---

## 현재 잘못된 수정 롤백

이전에 잘못 적용한 수정을 되돌린다.

### Lobby.unity — `[UI] Canvas` SortingOrder 200 → 0 (롤백)
- 로비씬은 패널이 별도 독립 Canvas를 가질 필요 없으면 0 유지

### Game.unity — `[UI]` Canvas SortingOrder 200 → 0 (롤백)
- HUD는 BlockingOverlay(100) 아래에 있어야 하므로 0으로 복원

---

## 올바른 수정 — Game.unity 패널별 Canvas 추가

각 패널 GO에 Canvas 컴포넌트(Override Sorting=true, SortingOrder=200)를 추가한다.
GameSystemRules_UI.md 규칙 4에 따라 SafeAreaFitter도 함께 추가한다.

대상 패널:
1. BuildingPopup
2. BuildingActionPanel
3. Panel (InGameSettingsPanel 내부 실제 패널 GO, line ~10719)
4. GameEndPanel
5. ProductionPopup

각 패널 GO에 추가할 컴포넌트:
- `Canvas`: Override Sorting = true, Sorting Order = 200
- `GraphicRaycaster`: 패널 내 버튼 입력 처리용 (Canvas Override 시 필수)

> SafeAreaFitter 적용 여부: 이 패널들은 SafeAreaContainer 자식으로 이미 SafeArea 범위 내에 배치됨.
> 독립 Canvas이지만 RectTransform은 부모 계층에서 결정되므로 SafeAreaFitter 별도 추가 불필요.

---

## 최종 SortingOrder 구조 (수정 후)

```
SortingOrder 0   → [UI] Canvas (HUD 등 기본 게임 UI — 오버레이가 가릴 수 있음)
SortingOrder 100 → UIManager Canvas (BlockingOverlay + ConfirmPopup)
SortingOrder 200 → 각 패널 GO의 Canvas Override (BuildingPopup, BuildingActionPanel 등)
SortingOrder 300 → LoadingIndicator 독립 Canvas
```

---

## 검증 항목

| 씬 | 확인 항목 |
|----|---------|
| Game | BuildingPopup 열릴 때 반투명 배경이 HUD 위·패널 뒤에 표시됨 |
| Game | BuildingActionPanel 열릴 때 반투명 배경이 HUD 위·패널 뒤에 표시됨 |
| Game | InGameSettingsPanel 열릴 때 반투명 배경이 HUD 위·패널 뒤에 표시됨 |
| Game | GameEndPanel, ProductionPopup 동일 확인 |
| Lobby | 기존 동작 유지 (회귀 없음) |
| Login | 기존 동작 유지 (회귀 없음) |
