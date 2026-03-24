# Research: Game UI Lifecycle Framework

## 작업 배경

게임 종료 시 생산 패널, 건물 배치 팝업 등 열려있는 UI들이 자동으로 닫히지 않는 문제.
앞으로 UI가 계속 추가될 예정이므로, 매번 개별 파일을 수정하지 않아도 되는 확장 가능한 프레임워크가 필요.

---

## 현재 UI 목록 및 게임 종료 대응 현황

| UI 클래스 | 파일 위치 | 현재 게임 종료 처리 | 보유 메서드 |
|-----------|----------|-------------------|------------|
| `GameEndUI` | `Presentation/UI/GameEndUI.cs` | ✅ OnGameEnd 구독 → 패널 표시 | `Hide()`, `Initialize()` |
| `GameHudUI` | `Presentation/UI/GameHudUI.cs` | ❌ 없음 | `Initialize()` (Update 폴링) |
| `ProductionPanelUI` | `Presentation/UI/ProductionPanelUI.cs` | ❌ 없음 | `Close()`, `Show()`, `Initialize()` |
| `BuildingPlacementUI` | `Presentation/UI/BuildingPlacementUI.cs` | ❌ 없음 | `Close()`, `Show()`, `Initialize()` |

---

## 현재 게임 종료 이벤트 흐름

```
[싱글] Castle 파괴
  → UnitCombatUseCase → GameEndUseCase
  → GameEvents.OnGameEnd.OnNext()
  → GameEndUI.OnGameEnd() → 패널 표시 + Time.timeScale = 0

[멀티] Castle 파괴 (서버)
  → GameEndUseCase → GameEvents.OnGameEnd
  → NetworkGameEndController.OnGameEndServer()
  → AnnounceWinnerClientRpc(winnerTeamIndex)
  → [모든 클라이언트] GameEndUI.ShowResult() → 패널 표시
```

**문제**: `GameEndUI`만 `OnGameEnd`를 구독하고 있음. 나머지 UI는 그대로 남음.

---

## 현재 초기화 흐름 (GameBootstrapper.LoadMap)

`GameBootstrapper.LoadMap()`에서 각 UI의 `Initialize()`를 직접 호출:

```
LoadMap()
  → _gameHudUI.Initialize(_resource, _population)     // 줄 307
  → _gameEndUI.Initialize()                           // 줄 311
  → _buildingUI.Initialize(...)                       // SetupBuildings() 내부
  → _productionUI.Initialize(...)                     // SetupProduction() 내부
```

**특이사항**: `GameBootstrapper`가 단일 조합 루트로, 모든 UI를 직접 보유하고 있음.

---

## 관련 GameEvents

| 이벤트 | 발행 위치 | 현재 구독자 |
|--------|---------|-----------|
| `OnGameEnd` | `GameEndUseCase` | `GameEndUI`, `NetworkGameEndController` |
| `OnProductionQueueChanged` | `UnitProductionUseCase` | `ProductionPanelUI` |
| `OnRallyPointChanged` | `UnitProductionUseCase` | `ProductionTicker` |

---

## 신규 추가 예정 이벤트 (계획)

| 이벤트 | 용도 |
|--------|------|
| `OnGameStarted` | 게임 시작/재시작 시 UI 초기 상태 세팅 |
| `OnGamePaused` | 일시정지 시 버튼 비활성화 등 |
| `OnGameResumed` | 재개 시 복원 |

---

## 영향 범위

### 신규 생성 파일
- `IGameUI.cs` — 인터페이스 정의 (Presentation 레이어)
- `GameUIManager.cs` — 등록/디스패치 매니저 (Presentation 레이어)

### 수정 파일
- `GameEvents.cs` — `OnGameStarted`, `OnGamePaused`, `OnGameResumed` Subject 추가
- `GameHudUI.cs` — `IGameUI` 구현 추가
- `ProductionPanelUI.cs` — `IGameUI` 구현 추가
- `BuildingPlacementUI.cs` — `IGameUI` 구현 추가
- `GameEndUI.cs` — `IGameUI` 구현 추가 (기존 OnGameEnd 구독은 유지)
- `GameBootstrapper.cs` — `GameUIManager` 참조 추가 + Register 호출

### 수정 불필요
- `NetworkGameEndController.cs` — 게임 종료 UI 표시는 여기서 직접 처리하므로 변경 없음
- 각 UseCase — 이벤트 발행 주체, 변경 없음

---

## 아키텍처 제약

- `IGameUI`, `GameUIManager`는 **Presentation 레이어** (`Hexiege.Presentation`)
- `GameEvents`는 **Application 레이어** — `GameUIManager`가 구독하여 Presentation에 전달하는 방향이 올바름
- `GameBootstrapper`는 Bootstrap 레이어 — 모든 레이어에 접근 가능, Register 호출 위치로 적합
