# Plan — Canvas SortingOrder 통일 (BlockingOverlay 렌더링 순서 수정)

## 이 작업이 하는 일

UIManager의 BlockingOverlay(반투명 배경)가 게임씬과 로비씬에서 패널보다 위에 렌더링되어
패널이 백그라운드에 가려지는 문제를 수정한다.

Login 씬에서 이미 올바르게 동작하는 구조(배경=0, UIManager=100, 팝업=200)를
Game·Lobby 씬에도 동일하게 적용한다.

세부적으로는 두 가지 변경을 수행한다:
1. Game/Lobby 씬의 메인 Canvas를 SortingOrder=200으로 올린다.
2. Game 씬에서 이제 사용하지 않는 로컬 _backgroundOverlay 연결 3곳을 해제한다.

---

## 변경 목록

### 1. Lobby.unity — `[UI] Canvas` SortingOrder 수정

- **파일**: `Assets/_Project/Scenes/Lobby.unity`
- **대상**: `[UI] Canvas` GameObject
- **변경**: `m_SortingOrder: 0` → `m_SortingOrder: 200`

### 2. Game.unity — `[UI]` Canvas SortingOrder 수정

- **파일**: `Assets/_Project/Scenes/Game.unity`
- **대상**: `[UI]` Canvas GameObject
- **변경**: `m_SortingOrder: 0` → `m_SortingOrder: 200`

### 3. Game.unity — Background GO AnimatedPanel._backgroundOverlay 연결 해제

- **파일**: `Assets/_Project/Scenes/Game.unity`
- **대상**: BuildingPopup, BuildingActionPanel, InGameSettingsPanel 3곳의 AnimatedPanel 컴포넌트
- **변경**: `_backgroundOverlay` 필드 참조(`{fileID: 543183959}`) → `{fileID: 0}` (연결 해제)
- **이유**: UIManager BlockingOverlay 통일 방향에 따라 로컬 오버레이는 더 이상 사용하지 않음.
  `Background` GO는 이미 `m_IsActive: 0` (비활성 상태)이므로 연결만 해제하면 됨.

---

## 최종 SortingOrder 구조 (3씬 통일)

```
SortingOrder 0   → 씬 배경 Canvas (로그인 뷰, 로비 패널, 게임 HUD)
SortingOrder 100 → UIManager Canvas (BlockingOverlay + ConfirmPopup) — 변경 없음
SortingOrder 200 → 씬별 팝업/패널 Canvas (게임 패널들, 로비 Canvas, 로그인 팝업들 — 이미 완료)
SortingOrder 300 → LoadingIndicator 독립 Canvas — 이미 완료
```

---

## 구현 순서

1. Lobby.unity 열기 → `[UI] Canvas` SortingOrder 0 → 200 수정 → 저장
2. Game.unity 열기 → `[UI]` Canvas SortingOrder 0 → 200 수정 → 저장
3. Game.unity → BuildingPopup, BuildingActionPanel, InGameSettingsPanel AnimatedPanel 컴포넌트에서 `_backgroundOverlay` 연결 해제 → 저장

---

## 검증 항목

| 씬 | 확인 항목 |
|----|---------|
| Game | BuildingPopup 열릴 때 반투명 배경이 패널 뒤에 표시됨 |
| Game | BuildingActionPanel 열릴 때 반투명 배경이 패널 뒤에 표시됨 |
| Game | InGameSettingsPanel 열릴 때 반투명 배경이 패널 뒤에 표시됨 |
| Game | ConfirmPopup 열릴 때 BlockingOverlay가 게임HUD 위·패널 뒤에 표시됨 |
| Lobby | ConfirmPopup 열릴 때 BlockingOverlay가 로비 패널 뒤에 표시됨 |
| Login | 기존 동작 유지 (회귀 없음) |
