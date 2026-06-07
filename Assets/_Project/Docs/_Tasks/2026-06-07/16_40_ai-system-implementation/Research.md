# Research — AI 시스템 구현

## 개요

이 작업은 싱글플레이 전용 AI 상대(Red팀)를 구현하는 작업이다.
AI는 사전에 설계된 빌드오더 스크립트(Phase 1~4)에 따라 건물을 짓고 유닛을 생산하며,
게임 상태를 주기적으로 감지하여 보조 행동(유닛 긴급 보충, 골드 과잉 가속, 채굴소 재건)을 수행한다.
난이도(쉬움/보통/어려움)는 인스펙터에서 ScriptableObject로 설정하고, 로비 UI에서 선택하여 게임 씬으로 전달한다.
설계 기준 문서: `GameSystemRules_AI.md`

---

## 현재 상태 파악

### 신규 생성 필요 파일 (없음)

| 파일 | 역할 |
|------|------|
| `Infrastructure/LocalPlayerDifficulty.cs` | 로비→게임 난이도 전달 정적 홀더 |
| `Infrastructure/Config/AIConfig.cs` | 난이도 파라미터 ScriptableObject |
| `Infrastructure/Config/AIScenarioConfig.cs` | 빌드오더 시나리오 ScriptableObject |
| `Application/Services/AIOpponentController.cs` | AI 메인 컨트롤러 |
| `Presentation/UI/Views/Lobby/Battle/DifficultySelectView.cs` | 난이도 선택 화면 View |
| Editor 스크립트 (1회성) | AIConfig / AIScenarioConfig asset 생성 메뉴 |

### 수정 필요 파일

| 파일 | 변경 내용 |
|------|----------|
| `Application/Events/GameEvents.cs` | `UnitProducedEvent`에 `BarracksId` 필드 추가 |
| `Application/UseCases/ResourceUseCase.cs` | `SetIncomeMultiplier(TeamId, float)` 메서드 추가 |
| `Presentation/UI/ViewModels/BattleViewModel.cs` | `BattleScreen.SingleplayDifficulty` + `CmdSelectDifficulty` 추가, `CmdStartSingleplay` 동작 변경 |
| `Presentation/UI/Views/Lobby/Battle/BattleRootView.cs` | `DifficultySelectView` 참조 + 바인딩 추가 |
| `Bootstrap/GameBootstrapper.cs` | `_enableAI` Inspector 필드 추가 |
| `Bootstrap/GameBootstrapper.Setup.cs` | `InitializeAI()` 헬퍼 추가 |
| `Bootstrap/GameBootstrapper.Map.cs` | `LoadMap()` 내 `InitializeAI()` 호출 추가 |

---

## 코드 파악

### UnitProducedEvent (GameEvents.cs:297)
```
현재: Unit(UnitData) + RallyPoint(HexCoord?) 두 필드만 있음.
BarracksId 없음 → AI가 어느 배럭에서 생산됐는지 콜백으로 알 수 없음.
추가 필요.
```
`BarracksId`가 있는 건 `ProductionQueueChangedEvent`(line 313)로, `UnitProducedEvent`와 별개의 구조체다.

### ResourceUseCase.TickIncome (ResourceUseCase.cs:95)
```
현재: Blue/Red 동일 비율로 채굴소 수입 처리.
goldIncomeMultiplier 적용 로직 없음.
SetIncomeMultiplier() 메서드와 내부 배율 필드 추가 필요.
```

### LocalPlayerRace 패턴 (LocalPlayerRace.cs)
```
Current(get, private set), IsAssigned, Set(race), Reset() 구조.
LocalPlayerDifficulty도 동일 패턴으로 생성.
기본값: DifficultyLevel.Normal
```

### BattleViewModel (BattleViewModel.cs:35)
```
BattleScreen enum: Main / CustomGame / CustomHost / CustomJoin / RandomMatch
CmdStartSingleplay → LoadSingleplayScene() 직접 호출 (씬 전환).
변경: CmdStartSingleplay → CurrentScreen = SingleplayDifficulty 로 전환.
추가: CmdSelectDifficulty(DifficultyLevel) → LocalPlayerDifficulty.Current 설정 후 씬 로드.
```

### GameBootstrapper.Map.cs — LoadMap() 순서
```
4. CreateUseCases()
5. 그리드 렌더링
6. 카메라 설정
7. 입력 연결
8. SetupBuildings()
9. SetupProduction()
10. HUD 초기화
11. Castle 자동 배치
...
InitializeAI() 삽입 위치: SetupProduction() 직후 (UseCase가 모두 준비된 시점)
```

### HexGrid.Tiles + HasGoldMine
```
HexGrid.Tiles: IReadOnlyDictionary<HexCoord, HexTile> (public property)
HexTile.HasGoldMine: bool (GameBootstrapper 맵 초기화 시 설정됨)
AI가 광산 타일 목록 조회: _grid.Tiles.Values.Where(t => t.HasGoldMine)
BuildingPlacementUseCase도 동일한 HexGrid를 주입받아 사용 → 같은 방식으로 접근 가능
```

### Infrastructure/Config ScriptableObject 패턴 (UnitStatsConfig.cs)
```
[CreateAssetMenu] 어트리뷰트로 메뉴 생성 지원.
Editor 스크립트(별도)로 Resources/Config/ 경로에 자동 생성.
AIConfig, AIScenarioConfig 동일 패턴 적용.
```

### GameBootstrapper 필드 규칙
```
[SerializeField] 필드는 GameBootstrapper.cs(메인 파일)에만 추가 (주석에 명시).
_enableAI 필드도 메인 파일에 추가.
InitializeAI() 헬퍼는 GameBootstrapper.Setup.cs에 추가.
```

---

## 영향 범위

- **구독자 영향**: `UnitProducedEvent` 변경 시 기존 구독자 `ProductionTicker` 확인 필요.
  → `ProductionTicker`는 `Unit`과 `RallyPoint`만 사용. `BarracksId` 추가는 구조체 확장이므로 하위 호환 문제 없음.
- **씬 영향**: 로비 씬(`Lobby.unity`)에 `DifficultySelectView` GameObject 추가 및 연결 필요 (Inspector 작업).
- **게임 씬(`Game.unity`)**: `GameBootstrapper`에 `_enableAI` 체크박스 및 `AIConfig` asset 연결 필요 (Inspector 작업).

---

## 부가 이슈

- `AIScenarioConfig`의 실제 빌드오더 수치(시나리오 A/B/C)는 밸런싱 후 채워야 함. 구조(ScriptableObject 필드)만 구현하고 수치는 placeholder로 유지.
- 시나리오 선택 방식: 균등 33% 무작위 (`Random.Range(0, 3)`).
