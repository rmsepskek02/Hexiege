# Plan — AI 시스템 구현

## 개요

싱글플레이 AI 상대(Red팀)를 5개 Phase로 나누어 구현한다.
Phase 1~2는 기존 파일의 소규모 수정(전제 조건)이고, Phase 3~4가 AI 핵심 로직이며, Phase 5는 로비 난이도 선택 UI다.
빌드오더 시나리오의 실제 수치(A/B/C)는 밸런싱 후 ScriptableObject에 채워 넣으며, 코드 구조만 완성한다.

근거 문서: `GameSystemRules_AI.md` 전체

---

## Phase 1 — 전제 조건 수정 (기존 파일)

### 1-1. UnitProducedEvent에 BarracksId 추가
**파일**: `Application/Events/GameEvents.cs`
**근거**: GameSystemRules_AI.md 규칙 12

- `UnitProducedEvent` 구조체에 `public readonly int BarracksId` 필드 추가
- 생성자에 `int barracksId` 파라미터 추가
- `UnitProductionUseCase.CompleteProduction()`에서 이벤트 발행 시 `barracksId` 전달
- 기존 구독자 `ProductionTicker`는 `BarracksId` 미사용이므로 영향 없음

### 1-2. ResourceUseCase — SetIncomeMultiplier 추가
**파일**: `Application/UseCases/ResourceUseCase.cs`
**근거**: GameSystemRules_AI.md 규칙 34

- `private readonly Dictionary<TeamId, float> _incomeMultipliers` 필드 추가 (기본값 1.0f)
- `public void SetIncomeMultiplier(TeamId team, float multiplier)` 메서드 추가
- `TickTeamIncome()`에서 채굴소 수입 계산 시 `_incomeMultipliers[team]`을 곱함
- Blue 팀은 항상 1.0이므로 영향 없음

### 1-3. LocalPlayerDifficulty 신규 생성
**파일**: `Infrastructure/LocalPlayerDifficulty.cs` (신규)
**근거**: GameSystemRules_AI.md 규칙 32

- `LocalPlayerRace.cs`와 동일한 정적 홀더 패턴
- `DifficultyLevel` enum (`Easy / Normal / Hard`) 포함
- `Current` (get, private set), `Set(DifficultyLevel)`, `Reset()` 구현
- 기본값: `DifficultyLevel.Normal`

---

## Phase 2 — AI ScriptableObject 구조 정의

### 2-1. AIConfig ScriptableObject
**파일**: `Infrastructure/Config/AIConfig.cs` (신규)
**근거**: GameSystemRules_AI.md 규칙 4~5

`DifficultyParams` 중첩 구조체 정의:
- `goldIncomeMultiplier` (float)
- `productionTimeMultiplier` (float)
- `decisionInterval` (float)
- `unitCountThreshold` (float)
- `goldExcessThreshold` (float)
- `buildOrderAccelCooldown` (float)
- `productionActivationCooldown` (float)
- `retryInterval` (float)
- `mineCheckInterval` (float)

`AIConfig`에 `easy`, `normal`, `hard` 세 개의 `DifficultyParams` 필드 포함.

### 2-2. AIScenarioConfig ScriptableObject
**파일**: `Infrastructure/Config/AIScenarioConfig.cs` (신규)
**근거**: GameSystemRules_AI.md 규칙 11

`BuildOrderStep` 직렬화 구조체:
- `ActionType` enum (`PlaceBuilding / UpgradeBuilding / StartProduction`)
- `BuildingType buildingType`
- `UnitType unitType`
- `int targetBuildingLine`
- `int phaseIndex`
- `float delaySecondsEasy / delaySecondsNormal / delaySecondsHard`

`AIScenarioConfig`에 `List<BuildOrderStep>[] phases` (4개 Phase) 포함.
Human 종족은 A/B/C 3개 시나리오를 배열로 담는다: `List<AIScenarioConfig> scenarios`.

### 2-3. Editor 자동 생성 스크립트 (1회성)
**파일**: `Editor/AIConfigSetup.cs` (신규, 1회 실행 후 삭제 가능)
- 메뉴 `Hexiege/Setup/AIConfig 생성`: `Resources/Config/AIConfig.asset` 기본값으로 생성
- 메뉴 `Hexiege/Setup/AIScenarioConfig_Human 생성`: placeholder 데이터로 생성

---

## Phase 3 — AIOpponentController (AI 핵심)

**파일**: `Application/Services/AIOpponentController.cs` (신규)
**근거**: GameSystemRules_AI.md 규칙 12~27, 28

### 주입받는 의존성
- `BuildingPlacementUseCase` — 건물 배치, 광산 조회
- `UnitProductionUseCase` — EnqueueUnit, SetRallyPoint, ClearRallyPoint
- `ResourceUseCase` — 골드 조회
- `HexGrid` — HasGoldMine 타일 조회
- `DifficultyParams` — AI 설정값
- `AIScenarioConfig` — 빌드오더 데이터

### 내부 상태
- `_lineProduction: Dictionary<int, UnitType>` — barracksId별 할당 유닛 타입
- `_currentPhase`, `_currentStepIndex` — 빌드오더 진행 위치
- `_stepTimer` — 현재 step delay 타이머
- `_retryTimer` — 실패 재시도 타이머
- `_r1CooldownTimer`, `_r2CooldownTimer` — R1/R2 쿨다운
- `_isTrackActive: bool[]` — Phase별 MiningPost 병행 트랙 활성화 여부
- `_mineTiles: List<HexCoord>` — 맵의 중립 광산 타일 목록 (초기화 시 조회)

### Tick() 구현 구조
```
public void Tick(float deltaTime)
{
    TickBuildOrder(deltaTime);    // 빌드오더 스크립트 실행
    TickMineTrack(deltaTime);     // MiningPost 병행 트랙
    TickReactionSystem(deltaTime); // R1/R2 반응 시스템
}
```

### 빌드오더 실행 (규칙 6~11)
- Phase별 step 순차 실행
- `delaySeconds` × 난이도 배율로 타이머 대기
- 실패 시 `retryInterval` 대기 후 재시도 (규칙 21)
- 골드 부족 첫 실패: 수동 생산 1개 취소 시도 (규칙 22)

### StartProduction 처리 (규칙 12)
- `_lineProduction[barracksId] = unitType` 기록
- `EnqueueUnit(barracksId, unitType)` 시드 호출
- `OnUnitProduced` 구독: Red 팀 유닛 생산 시 해당 배럭에 `EnqueueUnit` 재호출

### BFS 건물 배치 (규칙 26)
- Red 성채에서 BFS 탐색
- walkable + Red 소유 타일 중 최근접
- 기존 생산 건물의 인접 6타일 제외

### MiningPost 병행 트랙 (규칙 27)
- Phase 2/4 진입 시 `isTrackActive[phaseIndex] = true`
- `_mineTiles`에서 미점령 광산 타겟 선택
- 모든 생산 건물에 `SetRallyPoint` 설정
- `mineCheckInterval`마다 `PlaceMiningPost` 시도
- 성공 시 `ClearRallyPoint` + `isTrackActive = false`

### 반응 시스템 (규칙 13~20, R1/R2/R3)
- `decisionInterval`마다 R1 → R2 → R3 순으로 조건 확인
- **R1**: 유닛 수 열세 → `_lineProduction` 등록 배럭 전체에 `EnqueueUnit` (규칙 15~17)
- **R2**: 골드 과잉 → 빌드오더 타이머 즉시 만료 (규칙 18~20)
- **R3**: MiningPost 파괴 감지 → 병행 트랙 재시작 (`mineCheckInterval` 주기) (R3 규칙)

---

## Phase 4 — GameBootstrapper 연동

### 4-1. _enableAI 필드
**파일**: `Bootstrap/GameBootstrapper.cs`
**근거**: GameSystemRules_AI.md 규칙 30-A

```csharp
[Header("AI 설정")]
[Tooltip("AI 활성화 여부. false로 끄면 싱글플레이에서도 AI가 동작하지 않는다.")]
[SerializeField] private bool _enableAI = true;
```

### 4-2. InitializeAI() 헬퍼
**파일**: `Bootstrap/GameBootstrapper.Setup.cs`
**근거**: GameSystemRules_AI.md 규칙 30, 33, 34

```csharp
private void InitializeAI()
{
    // AIConfig 로드 → 난이도 파라미터 선택
    // AIScenarioConfig 로드 → Random.Range(0, 3)으로 시나리오 선택
    // ResourceUseCase.SetIncomeMultiplier(TeamId.Red, config.goldIncomeMultiplier)
    // AIOpponentController 생성 및 UseCase 주입
    // _aiController 런타임 필드에 보관
}
```

### 4-3. LoadMap()에 InitializeAI() 호출 추가
**파일**: `Bootstrap/GameBootstrapper.Map.cs`
**근거**: GameSystemRules_AI.md 규칙 30

```csharp
// SetupProduction() 직후
if (!isNetworkMode && _enableAI)
    InitializeAI();
```

### 4-4. Update()에 AI Tick 추가
**파일**: `Bootstrap/GameBootstrapper.cs`

```csharp
// 싱글플레이 전용 Update 블록에 추가
_aiController?.Tick(Time.deltaTime);
```

---

## Phase 5 — 로비 난이도 선택 UI

### 5-1. BattleScreen + CmdSelectDifficulty 추가
**파일**: `Presentation/UI/ViewModels/BattleViewModel.cs`
**근거**: GameSystemRules_AI.md 규칙 35~36

- `BattleScreen` enum에 `SingleplayDifficulty` 추가
- `CmdStartSingleplay` 동작 변경: `LoadSingleplayScene()` → `CurrentScreen = SingleplayDifficulty`
- `CmdSelectDifficulty: Subject<DifficultyLevel>` 추가
  - `LocalPlayerDifficulty.Current = level`
  - `LoadSingleplayScene()` 호출
- `NavigateBack()`에 `SingleplayDifficulty => Main` 케이스 추가

### 5-2. DifficultySelectView 신규 View
**파일**: `Presentation/UI/Views/Lobby/Battle/DifficultySelectView.cs` (신규)
**근거**: GameSystemRules_AI.md 규칙 35~36

- `IView<BattleViewModel>` 구현
- Inspector 참조: 쉬움/보통/어려움 버튼 3개, 뒤로가기 버튼
- `CurrentScreen == SingleplayDifficulty`일 때 CanvasGroup 표시 (Rule 5 패턴)
- 각 버튼 클릭 → `vm.CmdSelectDifficulty.OnNext(DifficultyLevel.X)`

### 5-3. BattleRootView에 DifficultySelectView 바인딩 추가
**파일**: `Presentation/UI/Views/Lobby/Battle/BattleRootView.cs`

- `[SerializeField] private DifficultySelectView _difficultySelectView;` 추가
- `Bind()` / `Unbind()`에 포함

---

## Inspector 작업 목록 (구현 완료 후 사용자 수행)

| 씬 | 작업 |
|-----|------|
| `Game.unity` | `GameBootstrapper` Inspector에 `AIConfig.asset` 연결 |
| `Game.unity` | `_enableAI` 체크박스 확인 (기본 true) |
| `Lobby.unity` | `DifficultySelectView` UI 오브젝트 생성 및 배치 |
| `Lobby.unity` | `BattleRootView`에 `DifficultySelectView` 연결 |

---

## 예상 위험 요소

| 위험 | 대응 |
|------|------|
| BFS가 성능 이슈를 일으킬 수 있음 | 건물 배치는 빌드오더 타이머마다 1회 실행이므로 매 프레임 실행 아님. 문제 발생 시 프로파일링 후 대응 |
| 광산 타일 순서 결정 모호 | Phase 2: 첫 번째 광산, Phase 4: 두 번째 광산. `_mineTiles` 정렬 기준을 Red 성채 거리 기준으로 결정 |
| 시나리오 A/B/C 수치가 placeholder | 밸런싱 전까지 임의 수치. 빌드오더 실행 자체는 테스트 가능 |
| `OnUnitProduced` 구독 시점 | `InitializeAI()` 내에서 구독 등록. `OnDestroy`/`ClearAll`에서 구독 해제 처리 필요 |

---

## 구현 위임

- **game-programmer** 에이전트: Phase 1~5 전체 코드 구현
- Phase 순서대로 진행 (각 Phase는 이전 Phase 완료 후)
