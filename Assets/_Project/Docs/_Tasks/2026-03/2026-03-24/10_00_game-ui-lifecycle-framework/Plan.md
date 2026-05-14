# Plan: Game UI Lifecycle Framework

## 목표

게임 상태 변화(시작, 종료, 일시정지 등)에 따라 UI들을 일괄 제어할 수 있는 프레임워크 구축.
새 UI 추가 시 인터페이스 구현 + Bootstrapper에 Register 1줄만 추가하면 자동 적용.

---

## 설계

### 1. `IGameUI` 인터페이스

**파일**: `Assets/_Project/Scripts/Presentation/UI/Core/IGameUI.cs`
**네임스페이스**: `Hexiege.Presentation`

```
interface IGameUI
  - OnGameStarted()   // 게임 시작/재시작 시 호출 — default 빈 구현
  - OnGameEnded()     // 게임 종료 시 호출 — default 빈 구현
  - OnGamePaused()    // 일시정지 시 호출 — default 빈 구현 (현재는 미사용, 확장용)
  - OnGameResumed()   // 재개 시 호출 — default 빈 구현 (현재는 미사용, 확장용)
```

각 UI는 **필요한 메서드만** override. 불필요한 메서드는 default로 처리되어 구현 강제 없음.

---

### 2. `GameUIManager`

**파일**: `Assets/_Project/Scripts/Presentation/UI/GameUIManager.cs`
**네임스페이스**: `Hexiege.Presentation`
**MonoBehaviour**: 씬의 `[Managers]` 하위에 배치

**역할**:
- `IGameUI` 구현체 목록 보관
- `GameEvents` 구독 → 전체 UI에 생명주기 이벤트 전달
- `GameEndUI` 예외 처리: `OnGameEnded` 호출 대상에서 제외 (GameEndUI는 종료 시 표시되는 UI)

**주요 메서드**:
```
Register(IGameUI ui)      — 등록 (GameBootstrapper에서 호출)
Initialize()              — GameEvents 구독 시작 (GameBootstrapper.LoadMap에서 호출)
```

**이벤트 구독 → 디스패치 흐름**:
```
GameEvents.OnGameEnd      → 등록된 모든 UI의 OnGameEnded() 호출
GameEvents.OnGameStarted  → 등록된 모든 UI의 OnGameStarted() 호출 (추후 활용)
GameEvents.OnGamePaused   → 등록된 모든 UI의 OnGamePaused() 호출 (추후 활용)
GameEvents.OnGameResumed  → 등록된 모든 UI의 OnGameResumed() 호출 (추후 활용)
```

---

### 3. `GameEvents` 확장

**파일**: `Assets/_Project/Scripts/Application/Events/GameEvents.cs`

추가할 Subject:
```
OnGameStarted  — Subject<Unit>   게임 시작/재시작 시 발행
OnGamePaused   — Subject<Unit>   일시정지 시 발행 (추후 구현 시 활용)
OnGameResumed  — Subject<Unit>   재개 시 발행 (추후 구현 시 활용)
```

`OnGameStarted` 발행 위치: `GameBootstrapper.LoadMap()` 마지막 단계.

---

### 4. 각 UI 수정 내용

#### `GameHudUI`
```
IGameUI 구현:
  OnGameEnded()   → 처리 없음 (게임 종료 시 그대로 유지)
  OnGameStarted() → 캐시 초기화 + 표시 상태 보장 (재시작 시 초기 상태로 복원)
```

#### `ProductionPanelUI`
```
IGameUI 구현:
  OnGameEnded()   → Close() 호출 (팝업 닫기 + SharedBackground 해제)
  OnGameStarted() → Close() 호출 (재시작 시 혹시 열려있을 경우 대비 닫기)
```

#### `BuildingPlacementUI`
```
IGameUI 구현:
  OnGameEnded()   → Close() 호출 (팝업 닫기 + SharedBackground 해제)
  OnGameStarted() → Close() 호출 (재시작 시 혹시 열려있을 경우 대비 닫기)
```

#### `GameEndUI`
```
IGameUI 구현:
  OnGameStarted() → Hide() 호출 (재시작 시 패널 숨기기 — 기존 Initialize()와 동일)
  OnGameEnded()   → 호출 안 함 (GameUIManager에서 제외)
```

> ⚠️ `GameEndUI`의 기존 `GameEvents.OnGameEnd` 직접 구독은 **유지**.
> `GameUIManager`는 게임 종료 시 `GameEndUI.OnGameEnded()`를 호출하지 않음 (표시해야 하는 UI이므로).

---

### 5. `GameBootstrapper` 수정 내용

**추가 필드**:
```csharp
[SerializeField] private GameUIManager _uiManager;
```

**LoadMap() 수정**:
```
기존 개별 Initialize() 호출 → 유지 (각 UI의 UseCase 주입은 기존대로)

추가:
  // LoadMap() 맨 앞: UI 등록 (최초 1회)
  _uiManager.Register(_gameHudUI);
  _uiManager.Register(_productionUI);
  _uiManager.Register(_buildingUI);
  _uiManager.Register(_gameEndUI);
  _uiManager.Initialize();  // GameEvents 구독 시작

  // LoadMap() 맨 마지막:
  GameEvents.OnGameStarted.OnNext(Unit.Default);  // 초기화 완료 알림
```

> ⚠️ `Register`는 중복 등록 방지 처리 필요 (LoadMap이 재시작 시 재호출될 수 있음).

---

## 파일별 변경 요약

| 파일 | 변경 유형 | 내용 |
|------|---------|------|
| `IGameUI.cs` | **신규** | 인터페이스 정의 |
| `GameUIManager.cs` | **신규** | 등록/디스패치 매니저 |
| `GameEvents.cs` | **수정** | OnGameStarted, OnGamePaused, OnGameResumed Subject 추가 |
| `GameHudUI.cs` | **수정** | IGameUI 구현 |
| `ProductionPanelUI.cs` | **수정** | IGameUI 구현 |
| `BuildingPlacementUI.cs` | **수정** | IGameUI 구현 |
| `GameEndUI.cs` | **수정** | IGameUI 구현 (OnGameStarted만) |
| `GameBootstrapper.cs` | **수정** | GameUIManager 참조 + Register + OnGameStarted 발행 |

---

## 버그 수정 (실기 테스트 후 발견)

### BUG-1: 멀티플레이 클라이언트에서 생산 패널/건물 팝업이 닫히지 않음

**원인**:
- 클라이언트에서 `GameEndUseCase`가 `GameEvents.OnGameEnd`를 의도적으로 발행하지 않음
  (발행 시 `GameEndUI.OnGameEnd()`가 Blue팀 고정 잘못된 결과를 표시하기 때문)
- 대신 `NetworkGameEndController.AnnounceWinnerClientRpc` → `GameEndUI.ShowResult()` 직접 호출
- `GameUIManager`는 `GameEvents.OnGameEnd`만 구독하므로 클라이언트에서 알림을 받지 못함

**수정**:
- `GameUIManager.NotifyGameEnded()` → `public`으로 변경
- `NetworkGameEndController`에 `GameUIManager _uiManager` 필드 추가
  - `OnNetworkSpawn()`에서 `FindFirstObjectByType<GameUIManager>()` 탐색
- `AnnounceWinnerClientRpc()`에서 `ShowResult()` 호출 전 `_uiManager?.NotifyGameEnded()` 추가

**Host에서 중복 호출 여부**: 무해.
- Host는 `GameEvents.OnGameEnd` 구독으로 이미 `NotifyGameEnded()` 1회 호출
- `AnnounceWinnerClientRpc`에서 1회 더 호출되지만, `Close()`는 이미 닫힌 팝업에 재호출해도 안전

**수정 파일**:
- `GameUIManager.cs` — `NotifyGameEnded()` public으로 변경
- `NetworkGameEndController.cs` — `_uiManager` 필드 추가 + `AnnounceWinnerClientRpc`에서 호출

---

## 위험 요소 및 주의사항

| 위험 | 대응 |
|------|------|
| LoadMap() 재호출 시 Register 중복 | GameUIManager에서 이미 등록된 항목 skip |
| GameEndUI가 OnGameEnded에 포함되어 패널이 닫힐 가능성 | GameUIManager에서 명시적으로 제외 |
| OnGameStarted 발행 타이밍이 너무 이르면 UseCase가 null | LoadMap() 맨 마지막 단계에 발행 |
| GameHudUI는 게임 종료 시 숨기지 않음 | OnGameStarted()에서만 초기화. 재시작 시 캐시 리셋으로 충분 |

---

## 구현 순서

1. `IGameUI.cs` 신규 생성
2. `GameUIManager.cs` 신규 생성
3. `GameEvents.cs` — Subject 3개 추가
4. `GameHudUI`, `ProductionPanelUI`, `BuildingPlacementUI`, `GameEndUI` — IGameUI 구현
5. `GameBootstrapper.cs` — uiManager 필드 추가 + Register + OnGameStarted 발행
6. Inspector: `[Managers]`에 `GameUIManager` 컴포넌트 추가 + 참조 연결
