# Research — Canvas SortingOrder 통일 (BlockingOverlay 렌더링 순서 수정)

## 이 작업이 하는 일

UIManager가 단일 소유하는 BlockingOverlay(반투명 배경)가 Game·Lobby 씬에서
패널보다 위에 렌더링되어 패널을 가리는 문제를 수정한다.

Login 씬에서 이미 올바르게 동작하는 구조(배경=0, UIManager=100, 팝업=200)를
Game·Lobby 씬에도 동일하게 적용한다.

---

## 현재 Canvas SortingOrder 현황

### UIManager Canvas (DontDestroyOnLoad — 모든 씬에서 유지)

| 오브젝트 | SortingOrder |
|----------|-------------|
| UIManager Canvas | **100** |
| └─ BlockingOverlay | (Canvas 상속, 100) |
| └─ ConfirmPopup | (Canvas 상속, 100) |
| └─ LoadingIndicator | 독립 Canvas **300** (별도 추가됨) |

### Login 씬 — ✅ 올바른 구조

| Canvas | SortingOrder | 포함 내용 |
|--------|-------------|---------|
| Login 메인 Canvas | 0 | LoginSelectView, EmailLoginView 등 뷰 전체 |
| UIManager Canvas | 100 | BlockingOverlay, ConfirmPopup |
| NetworkErrorPopup 독립 Canvas | 200 | NetworkErrorPopup 패널 |
| AnonymousWarningPopup 독립 Canvas | 200 | AnonymousWarningPopup 패널 |
| SplashOverlay Canvas | 200 | SplashOverlay |

→ 팝업(200) > UIManager BlockingOverlay(100) > 배경(0) 순서로 올바름.

### Lobby 씬 — ❌ 문제

| Canvas | SortingOrder | 포함 내용 |
|--------|-------------|---------|
| `[UI] Canvas` | **0** | LobbyRootView + 모든 패널 (BattlePanel, ShopPanel 등) |
| UIManager Canvas | 100 | BlockingOverlay, ConfirmPopup |

→ UIManager BlockingOverlay(100)가 Lobby 패널들(0)보다 위에 렌더링.
→ ConfirmPopup 표시 시 BlockingOverlay가 로비 UI 전체를 가림.
  (현재 Lobby에서 ConfirmPopup 사용처 확인 필요 — 로그아웃 확인 등)

### Game 씬 — ❌ 문제

| Canvas | SortingOrder | 포함 내용 |
|--------|-------------|---------|
| `[UI]` Canvas | **0** | SafeAreaContainer → 게임 패널 전체 |
| UIManager Canvas | 100 | BlockingOverlay, ConfirmPopup |

→ SafeAreaContainer 하위에 있는 패널들:
  - BuildingPopup (BuildingPlacementUI)
  - BuildingActionPanel (BuildingActionPanelUI)
  - InGameSettingsPanel (InGameSettingsUI)
  - ProductionPanel (ProductionPanelUI)
  - GameHUD

→ UIManager BlockingOverlay(100)가 이 모든 패널들(0)보다 위에 렌더링.
→ BuildingPlacementUI, InGameSettingsUI 등 패널 열릴 때 BlockingOverlay가 패널을 가림.

---

## AnimatedPanel._backgroundOverlay 현황

Game 씬에 `Background` GO(fileID: 543183959)가 있으며 **m_IsActive: 0 (비활성)**.
BuildingActionPanel, BuildingPopup, InGameSettingsPanel 3곳의 AnimatedPanel._backgroundOverlay에 연결됨.
UIManager BlockingOverlay 통일 방향에 따라 이 로컬 오버레이 연결을 해제해야 함.

---

## 목표 SortingOrder 구조

```
SortingOrder 0   → 씬 배경 Canvas (로그인뷰, 로비패널, 게임HUD 등 기본 UI)
SortingOrder 100 → UIManager Canvas (BlockingOverlay + ConfirmPopup)
SortingOrder 200 → 씬별 팝업/패널 Canvas (게임 패널, 로비 없음, 로그인 팝업들 이미 200)
SortingOrder 300 → LoadingIndicator 독립 Canvas (이미 완료)
```

---

## 영향 범위

| 파일/씬 | 변경 유형 |
|---------|---------|
| `Game.unity` | `[UI]` Canvas SortingOrder 0 → 200 |
| `Lobby.unity` | `[UI] Canvas` SortingOrder 0 → 200 |
| `Game.unity` | `Background` GO AnimatedPanel._backgroundOverlay 연결 해제 (3곳) |

Login 씬은 팝업들이 이미 200이므로 변경 불필요.
