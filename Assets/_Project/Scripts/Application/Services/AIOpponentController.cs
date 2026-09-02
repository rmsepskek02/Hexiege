// ============================================================================
// AIOpponentController.cs
// 싱글플레이 AI 상대(Red 팀)의 두뇌. 빌드오더 스크립트 + 반응 시스템을 구동한다.
//
// 두 계층 구조 (GameSystemRules_AI.md 규칙 3):
//   1) 빌드오더 스크립트 — 사전 정의된 시간표대로 건물 짓고/업글하고/생산 시작 (TickBuildOrder)
//   2) 반응 시스템     — 매 decisionInterval마다 상태 점검 후 보조 행동 (TickReactionSystem)
//   + MiningPost 병행 트랙 — Phase 2/4 진입 시 금광 점령을 독립적으로 처리 (TickMineTrack)
//
// 동작 원칙:
//   - Red 팀만 제어. Blue(플레이어)에는 관여하지 않는다 (규칙 2).
//   - UseCase를 통해서만 게임 상태를 변경. Domain을 직접 조작하지 않는다 (규칙 28).
//   - 멀티플레이에서는 절대 동작하지 않는다. GameBootstrapper가 싱글플레이에서만 생성한다 (규칙 1).
//
// 유닛 연속 생산 (규칙 12):
//   StartProduction 단계가 실행되면 _lineProduction[barracksId]=unitType를 기록하고
//   EnqueueUnit 1회로 시드한다. 이후 GameEvents.OnUnitProduced 구독 콜백에서
//   같은 배럭에 EnqueueUnit을 재호출하여 타이머 없이 생산이 끊기지 않게 유지한다.
//
// Application 레이어 — Domain UseCase에 의존.
//   설정 데이터(DifficultyParams / AIScenarioConfig)는 Infrastructure ScriptableObject지만,
//   GameBootstrapper(composition root)가 값으로 주입하므로 AI는 데이터 컨테이너로만 사용한다.
//   (기존 LoginUseCase도 Application에서 Infrastructure를 참조하는 선례가 있다.)
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Infrastructure;

namespace Hexiege.Application.Services
{
    /// <summary>
    /// 싱글플레이 AI(Red 팀) 컨트롤러. 매 프레임 Tick()으로 구동된다.
    /// </summary>
    public class AIOpponentController : IDisposable
    {
        // ====================================================================
        // 주입 의존성
        // ====================================================================

        private readonly BuildingPlacementUseCase _buildingPlacement;
        private readonly UnitProductionUseCase _unitProduction;
        private readonly ResourceUseCase _resource;
        private readonly UnitSpawnUseCase _unitSpawn;
        private readonly HexGrid _grid;

        // [Phase 5] 연구소 강화 UseCase(선택 주입). StartResearch 스텝 실행에 사용.
        // null이면 연구 스텝은 조용히 무시(하위호환 — 연구 미도입 빌드에서도 안전).
        private readonly UnitUpgradeUseCase _upgrade;

        // 난이도 파라미터(값 복사본)와 시나리오 데이터.
        private readonly DifficultyParams _params;
        // 선택된 시나리오의 빌드오더 항목 목록.
        private readonly IReadOnlyList<BuildOrderStep> _scenarioSteps;
        // 선택된 시나리오 이름 (로깅용).
        private readonly string _scenarioName;
        private readonly DifficultyLevel _difficulty;

        // AI가 제어하는 팀. 현재는 항상 Red.
        private const TeamId AiTeam = TeamId.Red;

        // ====================================================================
        // 빌드오더 진행 상태
        // ====================================================================

        // phaseIndex로 그룹핑한 빌드오더. _phases[phase] = 그 Phase의 항목 리스트(순서 보존).
        private readonly List<List<BuildOrderStep>> _phases = new List<List<BuildOrderStep>>();

        // 현재 진행 중인 Phase / 그 Phase 내 항목 인덱스.
        private int _currentPhaseIndex;
        private int _currentStepIndex;

        // 현재 항목까지의 누적 대기 타이머. Phase 시작 후 흐른 시간 누적.
        private float _stepTimer;

        // 현재 항목 실패 후 재시도 대기 타이머 (규칙 21).
        private float _retryTimer;

        // 현재 항목에서 골드 부족 시 수동 생산 취소를 이미 1회 시도했는지 (규칙 22).
        private bool _cancelTriedThisStep;

        // 빌드오더가 모두 끝났는지.
        private bool _buildOrderComplete;

        // 라인 인덱스(0/1/2) → 그 라인의 현재 생산 건물 barracksId.
        // PlaceBuilding/UpgradeBuilding 성공 시 갱신. UpgradeBuilding은 새 Id로 교체된다.
        private readonly Dictionary<int, int> _lineBarracks = new Dictionary<int, int>();

        // ====================================================================
        // 연속 생산 상태 (규칙 12)
        // ====================================================================

        // barracksId → 그 배럭이 자동으로 이어서 생산할 유닛 타입.
        private readonly Dictionary<int, UnitType> _lineProduction = new Dictionary<int, UnitType>();

        // ====================================================================
        // 반응 시스템 상태
        // ====================================================================

        // decisionInterval 폴링 타이머.
        private float _decisionTimer;

        // R1 / R2 쿨다운 잔여 시간.
        private float _r1CooldownTimer;
        private float _r2CooldownTimer;

        // ====================================================================
        // MiningPost 병행 트랙 상태 (규칙 27)
        // ====================================================================

        // Phase 2 / Phase 4 트랙 활성화 여부.
        private bool _isPhase2MineTrackActive;
        private bool _isPhase4MineTrackActive;

        // mineCheckInterval 폴링 타이머 (트랙 공용).
        private float _mineTrackTimer;

        // 맵의 모든 금광 타일 좌표 (초기화 시 1회 조회).
        private readonly List<HexCoord> _mineTiles = new List<HexCoord>();

        // 현재 트랙이 노리는 금광 타일. 트랙 비활성화 시 default.
        private HexCoord _phase2TargetMine;
        private HexCoord _phase4TargetMine;
        private bool _hasPhase2Target;
        private bool _hasPhase4Target;

        // ====================================================================
        // 이벤트 구독 해제용
        // ====================================================================

        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        // ====================================================================
        // 생성자
        // ====================================================================

        /// <summary>
        /// AI 컨트롤러 생성. GameBootstrapper.InitializeAI()에서 호출.
        /// </summary>
        /// <param name="buildingPlacement">건물 배치/업그레이드 UseCase.</param>
        /// <param name="unitProduction">유닛 생산 큐 UseCase.</param>
        /// <param name="resource">골드 조회/관리 UseCase.</param>
        /// <param name="unitSpawn">유닛 조회용 UseCase (유닛 수 비교에 사용).</param>
        /// <param name="grid">금광 타일 조회용 그리드.</param>
        /// <param name="aiParams">선택된 난이도의 파라미터.</param>
        /// <param name="scenarioSteps">사용할 빌드오더 시나리오의 항목 목록.</param>
        /// <param name="scenarioName">선택된 시나리오 이름 (로깅용).</param>
        /// <param name="difficulty">현재 난이도 단계 (delaySeconds 선택에 사용).</param>
        public AIOpponentController(
            BuildingPlacementUseCase buildingPlacement,
            UnitProductionUseCase unitProduction,
            ResourceUseCase resource,
            UnitSpawnUseCase unitSpawn,
            HexGrid grid,
            DifficultyParams aiParams,
            IReadOnlyList<BuildOrderStep> scenarioSteps,
            string scenarioName,
            DifficultyLevel difficulty,
            UnitUpgradeUseCase upgrade = null)
        {
            _buildingPlacement = buildingPlacement;
            _unitProduction = unitProduction;
            _resource = resource;
            _unitSpawn = unitSpawn;
            _grid = grid;
            _upgrade = upgrade;
            _params = aiParams;
            _scenarioSteps = scenarioSteps;
            _scenarioName  = scenarioName;
            _difficulty = difficulty;

            BuildPhaseGroups();
            CacheMineTiles();

            // 유닛 생산 완료 콜백 구독 — 연속 생산 유지 (규칙 12).
            GameEvents.OnUnitProduced
                .Subscribe(OnUnitProduced)
                .AddTo(_disposables);

            // MiningPost 파괴 감지 — R3 재건 트리거 (규칙 R3).
            GameEvents.OnBuildingDied
                .Subscribe(OnBuildingDied)
                .AddTo(_disposables);
        }

        // ====================================================================
        // 초기화 헬퍼
        // ====================================================================

        /// <summary>
        /// 시나리오의 평탄 리스트를 phaseIndex별로 그룹핑하여 _phases에 채운다.
        /// 같은 Phase 내 순서는 리스트 등장 순서를 그대로 보존한다 (규칙 7).
        /// </summary>
        private void BuildPhaseGroups()
        {
            // Phase는 0~3(4개) 고정. 비어 있어도 인덱스 접근이 안전하도록 미리 채운다.
            for (int i = 0; i < 4; i++)
                _phases.Add(new List<BuildOrderStep>());

            if (_scenarioSteps == null) return;

            foreach (var step in _scenarioSteps)
            {
                int p = step.phaseIndex;
                if (p < 0 || p >= _phases.Count) continue; // 잘못된 phaseIndex는 무시
                _phases[p].Add(step);
            }
        }

        /// <summary>
        /// 맵의 모든 금광 타일을 1회 조회하여 캐시한다 (규칙 27 — 고정 좌표 미사용).
        /// Red 성채 거리 기준으로 정렬하여, Phase 2는 가까운 금광부터 노리도록 한다.
        /// </summary>
        private void CacheMineTiles()
        {
            _mineTiles.Clear();
            if (_grid == null) return;

            foreach (var kvp in _grid.Tiles)
            {
                if (kvp.Value.MineKind != MineKind.None)
                    _mineTiles.Add(kvp.Key);
            }

            // Red 성채 위치 기준 가까운 순으로 정렬.
            HexCoord castle = FindAiCastlePosition();
            _mineTiles.Sort((a, b) =>
                HexCoord.Distance(a, castle).CompareTo(HexCoord.Distance(b, castle)));
        }

        /// <summary>
        /// AI(Red) 성채의 위치를 찾는다. 없으면 (0,0) 폴백.
        /// </summary>
        private HexCoord FindAiCastlePosition()
        {
            foreach (var kvp in _buildingPlacement.Buildings)
            {
                if (kvp.Value.Team == AiTeam && kvp.Value.Type == BuildingType.Castle)
                    return kvp.Value.Position;
            }
            return new HexCoord(0, 0);
        }

        // ====================================================================
        // 매 프레임 Tick
        // ====================================================================

        /// <summary>
        /// 매 프레임 GameBootstrapper.Update()에서 호출.
        /// 빌드오더 → MiningPost 트랙 → 반응 시스템 순으로 갱신한다.
        /// </summary>
        /// <param name="deltaTime">직전 프레임과의 시간 간격(초).</param>
        public void Tick(float deltaTime)
        {
            TickBuildOrder(deltaTime);
            TickMineTrack(deltaTime);
            TickReactionSystem(deltaTime);

            // 쿨다운 감소 (음수로 내려가도 무방 — 0 이하면 사용 가능으로 판정).
            if (_r1CooldownTimer > 0f) _r1CooldownTimer -= deltaTime;
            if (_r2CooldownTimer > 0f) _r2CooldownTimer -= deltaTime;
        }

        // ====================================================================
        // 빌드오더 실행 (규칙 6~11, 21~22)
        // ====================================================================

        /// <summary>
        /// 빌드오더 타이머를 진행하고, 현재 항목의 대기 시간이 지나면 실행을 시도한다.
        /// 실패하면 retryInterval 대기 후 재시도(규칙 21). Phase 스킵 없음(규칙 8).
        /// </summary>
        private void TickBuildOrder(float deltaTime)
        {
            if (_buildOrderComplete) return;

            // 현재 Phase의 항목 리스트.
            List<BuildOrderStep> phase = _phases[_currentPhaseIndex];

            // 빈 Phase면 즉시 다음 Phase로.
            if (_currentStepIndex >= phase.Count)
            {
                AdvancePhase();
                return;
            }

            BuildOrderStep step = phase[_currentStepIndex];

            // 재시도 대기 중이면 retryTimer만 소진.
            if (_retryTimer > 0f)
            {
                _retryTimer -= deltaTime;
                return;
            }

            // 대기 시간 누적.
            _stepTimer += deltaTime;
            if (_stepTimer < step.GetDelaySeconds(_difficulty))
                return;

            // 실행 시도.
            bool success = ExecuteStep(step);
            if (success)
            {
                AdvanceStep();
            }
            else
            {
                // 실패 → retryInterval 대기 후 재시도 (규칙 21).
                _retryTimer = _params.retryInterval;
            }
        }

        /// <summary>
        /// 현재 항목 인덱스를 다음으로 진행. Phase 끝이면 Phase를 넘긴다.
        /// 항목/Phase 전환 시 타이머와 1회성 플래그를 초기화한다.
        /// </summary>
        private void AdvanceStep()
        {
            _currentStepIndex++;
            _stepTimer = 0f;
            _retryTimer = 0f;
            _cancelTriedThisStep = false;

            if (_currentStepIndex >= _phases[_currentPhaseIndex].Count)
                AdvancePhase();
        }

        /// <summary>
        /// 다음 Phase로 진행. Phase 2/4 진입 시 MiningPost 병행 트랙을 활성화한다(규칙 27).
        /// 마지막 Phase를 넘기면 빌드오더 완료.
        /// </summary>
        private void AdvancePhase()
        {
            _currentPhaseIndex++;
            _currentStepIndex = 0;
            _stepTimer = 0f;
            _retryTimer = 0f;
            _cancelTriedThisStep = false;

            if (_currentPhaseIndex >= _phases.Count)
            {
                _buildOrderComplete = true;
                return;
            }

            // Phase 2(인덱스 1) 진입 → 첫 금광 트랙 시작.
            if (_currentPhaseIndex == 1)
                ActivatePhase2MineTrack();

            // Phase 4(인덱스 3) 진입 → 두 번째 금광 트랙 시작.
            if (_currentPhaseIndex == 3)
                ActivatePhase4MineTrack();
        }

        /// <summary>
        /// 빌드오더 한 항목을 실행한다.
        /// 성공 시 true. 골드 부족/타일 없음 등으로 실패하면 false(재시도 대상).
        /// </summary>
        private bool ExecuteStep(BuildOrderStep step)
        {
            switch (step.actionType)
            {
                case AIActionType.PlaceBuilding:
                    return ExecutePlaceBuilding(step);
                case AIActionType.UpgradeBuilding:
                    return ExecuteUpgradeBuilding(step);
                case AIActionType.StartProduction:
                    return ExecuteStartProduction(step);
                case AIActionType.StartResearch:
                    return ExecuteStartResearch(step);
                default:
                    return true; // 알 수 없는 액션은 건너뜀(스킵으로 처리)
            }
        }

        /// <summary>
        /// [Phase 5] 연구소 강화 착수 항목 실행. AI가 보유한 연구소에서 지정 트랙을 1레벨 연구한다.
        ///
        /// 성공 조건: 살아있는 AI 소유 연구소가 존재 + 트랙이 연구 가능(최대레벨 미만·진행중 아님) + 골드 충분.
        /// 하나라도 안 되면 false를 반환하여 재시도 대상으로 둔다(예: 연구소 건설 완료 전이면 다음 틱 재시도).
        /// _upgrade 미주입(연구 미도입 빌드)이면 조용히 성공 처리하여 스텝을 넘긴다(하위호환).
        /// </summary>
        private bool ExecuteStartResearch(BuildOrderStep step)
        {
            // 연구 시스템 미주입 빌드에서는 스텝을 소비만 하고 넘어간다(막힘 방지).
            if (_upgrade == null) return true;

            // AI 소유의 살아있는 연구소를 찾는다(연구는 연구소 종속이 아니지만, 취소·환불 기준으로 Id를 기록).
            int labId = FindOwnedResearchLabId();
            if (labId < 0) return false; // 연구소가 아직 없음 → 다음 틱 재시도(선행 PlaceBuilding 필요).

            // 트랙이 연구 가능한지(최대레벨/진행중) + 골드 충분 여부 사전 확인 → 착수.
            if (!_upgrade.CanResearch(AiTeam, step.upgradeGroup, step.upgradeStat)) return true; // 이미 진행중/완료면 스텝 소비.

            int cost = _upgrade.GetNextLevelCost(AiTeam, step.upgradeGroup, step.upgradeStat);
            if (cost < 0) return true;                         // 최대 레벨 → 스텝 소비.
            if (!_resource.CanAfford(AiTeam, cost)) return false; // 골드 부족 → 재시도.

            // 착수(내부에서 골드 검증·차감·타이머 시작). 싱글플레이 서버 권위 흐름과 동일.
            return _upgrade.TryStartResearch(AiTeam, step.upgradeGroup, step.upgradeStat, labId, _resource);
        }

        /// <summary>
        /// AI(Red) 소유의 살아있는 연구소(BuildingType.Research) 하나의 Id를 반환한다. 없으면 -1.
        /// </summary>
        private int FindOwnedResearchLabId()
        {
            foreach (var kvp in _buildingPlacement.Buildings)
            {
                BuildingData b = kvp.Value;
                if (b == null) continue;
                if (b.Team != AiTeam) continue;
                if (b.Type != BuildingType.Research) continue;
                if (!b.IsAlive) continue;
                return b.Id;
            }
            return -1;
        }

        /// <summary>
        /// 건물 배치 항목 실행. BFS로 배치 타일을 찾아 PlaceBuilding을 호출한다(규칙 26).
        /// 골드 부족 시 규칙 22(수동 생산 취소 1회) 적용 후 실패 반환.
        /// </summary>
        private bool ExecutePlaceBuilding(BuildOrderStep step)
        {
            int cost = BuildingStats.GetGoldCost(step.buildingType, GameRaceForAi());

            // 골드 부족 → 규칙 22: 수동 생산 1개 취소 1회 시도 후 실패 처리.
            if (!_resource.CanAfford(AiTeam, cost))
            {
                TryCancelOneManualProduction();
                return false;
            }

            HexCoord? tile = FindPlacementTile();
            if (!tile.HasValue)
            {
                // ★★★ 임시 진단 로그 — 무작위 맵 1단계 회귀 확인용 (2026-09-02) ★★★
                //   목적: Plan.md §5 회귀 확인 7번("AI 가 계속 건물을 배치하는가").
                //         AI 의 후보 타일 판정은 IsWalkable 을 읽는다. 계산 프로퍼티로 바뀐 뒤에도
                //         후보를 찾는지 / 아예 못 찾는지를 로그만으로 갈라 준다.
                //   제거 시점: 회귀 확인 1·2·5·6·7 이 전부 PASS 로 확인되면 이 블록을 지운다.
                //   ⚠️ 로그만 추가했다 — 아래 return 과 조건은 원래 그대로다.
                GameLog.Dev.Info("HexGrid", nameof(AIOpponentController), "진단: AI 건물 배치 시도",
                    $"Diag=RandomMapPhase1, BuildingType={step.buildingType}, Result=NoCandidateTile");
                return false; // 타일 없음 → 대기 후 재시도 (규칙 24)
            }

            BuildingData placed = _buildingPlacement.PlaceBuilding(
                step.buildingType, AiTeam, tile.Value, GameRaceForAi());

            // ★★★ 임시 진단 로그 — 무작위 맵 1단계 회귀 확인용 (2026-09-02) ★★★
            //   후보 타일을 찾은 뒤 실제 배치가 성사됐는지(Placed)/거부됐는지(Rejected)를 남긴다.
            //   제거 시점: 위와 동일.
            GameLog.Dev.Info("HexGrid", nameof(AIOpponentController), "진단: AI 건물 배치 시도",
                $"Diag=RandomMapPhase1, BuildingType={step.buildingType}, " +
                $"Q={tile.Value.Q}, R={tile.Value.R}, " +
                $"Result={(placed == null ? "Rejected" : "Placed")}");

            if (placed == null)
                return false;

            // 생산 건물이면 라인-배럭 매핑 등록 + ProductionState 등록.
            if (BuildingTypeHelper.IsProductionBuilding(placed.Type))
            {
                _lineBarracks[step.targetBuildingLine] = placed.Id;
                _unitProduction.RegisterBarracks(placed);
            }
            return true;
        }

        /// <summary>
        /// 건물 업그레이드 항목 실행. 라인의 현재 배럭을 다음 단계로 업그레이드한다.
        /// 업그레이드 후 새 Id로 라인 매핑과 연속 생산 매핑을 갱신한다.
        /// </summary>
        private bool ExecuteUpgradeBuilding(BuildOrderStep step)
        {
            // 라인에 등록된 배럭이 없으면 (앞선 PlaceBuilding 미완료) 실패 → 재시도.
            if (!_lineBarracks.TryGetValue(step.targetBuildingLine, out int oldId))
                return false;

            BuildingData old = _buildingPlacement.GetBuilding(oldId);
            if (old == null)
                return false;

            // 업그레이드 비용 검증 (규칙 22 — 골드 부족 시 취소 1회 시도).
            int upgradeCost = BuildingStats.GetUpgradeCost(old.Type);
            if (!_resource.CanAfford(AiTeam, upgradeCost))
            {
                TryCancelOneManualProduction();
                return false;
            }

            // 골드 차감은 UseCase 외부 책임(GameSystemRules — UpgradeBuilding 검증은 호출자).
            // AI는 직접 골드를 차감한 뒤 도메인 업그레이드를 실행한다.
            if (!_resource.SpendGold(AiTeam, upgradeCost))
                return false;

            BuildingData upgraded = _buildingPlacement.UpgradeBuilding(oldId, GameRaceForAi());
            if (upgraded == null)
            {
                // 업그레이드 실패 시 차감한 골드 환불.
                _resource.AddGold(AiTeam, upgradeCost);
                return false;
            }

            // 배럭 Id가 바뀌었으므로 생산 상태/매핑을 새 Id로 이전한다.
            MigrateBarracksId(oldId, upgraded);
            _lineBarracks[step.targetBuildingLine] = upgraded.Id;
            return true;
        }

        /// <summary>
        /// 유닛 생산 시작 항목 실행 (규칙 12).
        /// 라인의 배럭에 _lineProduction을 기록하고 EnqueueUnit으로 1회 시드한다.
        /// 시드가 골드 부족으로 실패해도 매핑은 남으므로, 다음 골드 확보 시점에
        /// OnUnitProduced 콜백 또는 R1이 다시 큐를 채운다 → 시드 자체는 성공으로 간주.
        /// </summary>
        private bool ExecuteStartProduction(BuildOrderStep step)
        {
            if (!_lineBarracks.TryGetValue(step.targetBuildingLine, out int barracksId))
                return false; // 해당 라인 배럭이 아직 없음 → 재시도

            // 라인의 연속 생산 타입 등록/갱신.
            _lineProduction[barracksId] = step.unitType;

            // 초기 큐 시드 (성공 여부와 무관하게 매핑은 유지된다).
            _unitProduction.EnqueueUnit(barracksId, step.unitType);
            return true;
        }

        // ====================================================================
        // 연속 생산 콜백 (규칙 12)
        // ====================================================================

        /// <summary>
        /// 유닛 생산 완료 시 호출. Red 팀 유닛이고 해당 배럭에 연속 생산 타입이 등록돼 있으면
        /// 즉시 같은 타입을 다시 EnqueueUnit하여 생산을 끊김 없이 유지한다.
        /// </summary>
        private void OnUnitProduced(UnitProducedEvent e)
        {
            // 다른 팀(플레이어) 유닛이거나 배럭 미지정(-1)이면 무시.
            if (e.Unit == null || e.Unit.Team != AiTeam) return;
            if (e.BarracksId < 0) return;

            if (_lineProduction.TryGetValue(e.BarracksId, out UnitType type))
            {
                // 골드 부족 시 EnqueueUnit이 false를 반환하지만, 그 경우 다음 완료 시점에 다시 시도되므로
                // 별도 재시도 로직이 필요 없다.
                _unitProduction.EnqueueUnit(e.BarracksId, type);
            }
        }

        // ====================================================================
        // MiningPost 파괴 감지 (규칙 R3)
        // ====================================================================

        /// <summary>
        /// 건물 파괴 시 호출. AI 소유 MiningPost가 파괴되면 해당 Phase 트랙을 재활성화하여
        /// 금광을 다시 점령하도록 한다.
        /// </summary>
        private void OnBuildingDied(BuildingDiedEvent e)
        {
            BuildingData b = e.Building;
            if (b == null || b.Team != AiTeam || b.Type != BuildingType.MiningPost) return;

            // 어느 트랙의 타겟이 파괴됐는지 확인하여 그 트랙만 재활성화.
            if (_hasPhase2Target && b.Position == _phase2TargetMine)
                _isPhase2MineTrackActive = true;
            else if (_hasPhase4Target && b.Position == _phase4TargetMine)
                _isPhase4MineTrackActive = true;
        }

        // ====================================================================
        // MiningPost 병행 트랙 (규칙 27)
        // ====================================================================

        /// <summary>
        /// Phase 2 금광 트랙을 활성화하고, 가장 가까운 미점령 금광을 타겟으로 정한다.
        /// 모든 생산 건물에 랠리포인트를 설정해 유닛이 금광 방향으로 행군하게 한다.
        /// </summary>
        private void ActivatePhase2MineTrack()
        {
            HexCoord? target = SelectUncapturedMine();
            if (!target.HasValue) return;

            _phase2TargetMine = target.Value;
            _hasPhase2Target = true;
            _isPhase2MineTrackActive = true;

            SetRallyToMine(_phase2TargetMine);
        }

        /// <summary>
        /// Phase 4 금광 트랙을 활성화하고, Phase 2와 다른 미점령 금광을 타겟으로 정한다.
        /// </summary>
        private void ActivatePhase4MineTrack()
        {
            HexCoord? target = SelectUncapturedMine();
            if (!target.HasValue) return;

            _phase4TargetMine = target.Value;
            _hasPhase4Target = true;
            _isPhase4MineTrackActive = true;

            SetRallyToMine(_phase4TargetMine);
        }

        /// <summary>
        /// 활성화된 트랙이 있으면 mineCheckInterval마다 PlaceMiningPost를 시도한다.
        /// 성공하면 랠리포인트를 해제하고 트랙을 비활성화한다.
        /// </summary>
        private void TickMineTrack(float deltaTime)
        {
            if (!_isPhase2MineTrackActive && !_isPhase4MineTrackActive) return;

            _mineTrackTimer += deltaTime;
            if (_mineTrackTimer < _params.mineCheckInterval) return;
            _mineTrackTimer = 0f;

            if (_isPhase2MineTrackActive && _hasPhase2Target)
                TryCaptureMine(_phase2TargetMine, ref _isPhase2MineTrackActive);

            if (_isPhase4MineTrackActive && _hasPhase4Target)
                TryCaptureMine(_phase4TargetMine, ref _isPhase4MineTrackActive);
        }

        /// <summary>
        /// 지정 금광에 MiningPost 배치를 시도한다.
        /// 성공 시 랠리포인트 해제 + 트랙 비활성화. 실패 시 다음 주기에 재시도.
        /// </summary>
        private void TryCaptureMine(HexCoord mine, ref bool trackActive)
        {
            // 이미 AI 소유 건물이 있으면(직전 주기에 점령됨) 트랙 종료 처리.
            BuildingData existing = _buildingPlacement.GetBuildingAt(mine);
            if (existing != null && existing.Team == AiTeam && existing.Type == BuildingType.MiningPost)
            {
                ClearAllRally();
                trackActive = false;
                return;
            }

            BuildingData placed = _buildingPlacement.PlaceMiningPost(AiTeam, mine, GameRaceForAi());
            if (placed != null)
            {
                ClearAllRally();
                trackActive = false;
            }
            // 실패(인접 타일 미점령/Blue 점령 중) → 유닛들이 계속 행군하며 자연 점령을 기다린다.
        }

        /// <summary>
        /// AI MiningPost가 아직 없는 금광 중 Red 성채에 가장 가까운 것을 선택한다.
        /// _mineTiles는 이미 성채 거리순 정렬되어 있으므로 앞에서부터 첫 미점령 금광을 고른다.
        /// 이미 다른 트랙이 노리는 타겟은 제외한다.
        /// </summary>
        private HexCoord? SelectUncapturedMine()
        {
            foreach (var mine in _mineTiles)
            {
                // 다른 트랙이 이미 노리는 금광은 제외 (Phase 2/4가 서로 다른 금광을 점령하도록).
                if (_hasPhase2Target && mine == _phase2TargetMine) continue;
                if (_hasPhase4Target && mine == _phase4TargetMine) continue;

                BuildingData b = _buildingPlacement.GetBuildingAt(mine);
                // AI가 이미 점령한 금광은 제외.
                if (b != null && b.Team == AiTeam && b.Type == BuildingType.MiningPost) continue;

                return mine;
            }
            return null;
        }

        /// <summary>
        /// AI의 모든 생산 건물에 지정 금광을 랠리포인트로 설정한다.
        /// 유닛이 금광 방향으로 행군하며 인접 타일을 점령하도록 유도한다(규칙 27).
        /// </summary>
        private void SetRallyToMine(HexCoord mine)
        {
            foreach (var barracksId in CollectAiBarracksIds())
                _unitProduction.SetRallyPoint(barracksId, mine);
        }

        /// <summary>
        /// AI의 모든 생산 건물의 랠리포인트를 해제한다.
        /// </summary>
        private void ClearAllRally()
        {
            foreach (var barracksId in CollectAiBarracksIds())
                _unitProduction.ClearRallyPoint(barracksId);
        }

        // ====================================================================
        // 반응 시스템 (규칙 13~20)
        // ====================================================================

        /// <summary>
        /// decisionInterval마다 R1 → R2 순으로 점검하고, 조건+쿨다운을 만족하는
        /// 첫 번째 규칙 하나만 실행한다(규칙 13~14). R3는 OnBuildingDied 콜백이 담당한다.
        /// </summary>
        private void TickReactionSystem(float deltaTime)
        {
            _decisionTimer += deltaTime;
            if (_decisionTimer < _params.decisionInterval) return;
            _decisionTimer = 0f;

            // R1: 유닛 수 열세 시 긴급 보충.
            if (_r1CooldownTimer <= 0f && TryReactionR1())
                return;

            // R2: 골드 과잉 시 빌드오더 가속.
            if (_r2CooldownTimer <= 0f && TryReactionR2())
                return;
        }

        /// <summary>
        /// 규칙 R1 — AI 유닛 수 &lt; 플레이어 유닛 수 × unitCountThreshold이면
        /// _lineProduction에 등록된 모든 배럭에 즉시 EnqueueUnit으로 긴급 보충(규칙 15~17).
        /// 실행하면 productionActivationCooldown 쿨다운을 걸고 true 반환.
        /// </summary>
        private bool TryReactionR1()
        {
            int aiUnits = CountUnits(AiTeam);
            int playerUnits = CountUnits(TeamId.Blue);

            if (aiUnits >= playerUnits * _params.unitCountThreshold)
                return false; // 열세 아님

            // 등록된 모든 라인 배럭에 한 개씩 긴급 보충.
            bool acted = false;
            foreach (var kvp in _lineProduction)
            {
                int barracksId = kvp.Key;
                UnitType type = kvp.Value;
                // 골드/큐가 가득 차면 EnqueueUnit이 자동으로 false 반환 — 상태 확인 불필요(규칙 16).
                if (_unitProduction.EnqueueUnit(barracksId, type))
                    acted = true;
            }

            // 보충을 시도했으면(매핑이 하나라도 있으면) 쿨다운을 건다.
            // 큐가 모두 가득 차 acted=false여도 쿨다운을 걸어 매 사이클 반복 점검을 막는다.
            if (_lineProduction.Count > 0)
            {
                _r1CooldownTimer = _params.productionActivationCooldown;
                return true;
            }

            return acted;
        }

        /// <summary>
        /// 규칙 R2 — AI 골드 ≥ goldExcessThreshold이고 빌드오더에 대기 항목이 남아있으며
        /// 그 항목의 delay 타이머가 아직 안 찼으면, stepTimer를 즉시 만료시켜 가속한다(규칙 18~20).
        /// 실행하면 buildOrderAccelCooldown 쿨다운을 걸고 true 반환.
        /// </summary>
        private bool TryReactionR2()
        {
            if (_buildOrderComplete) return false;

            // 골드 과잉 조건.
            if (_resource.GetGold(AiTeam) < _params.goldExcessThreshold) return false;

            // 현재 대기 중인 다음 항목이 존재해야 함.
            List<BuildOrderStep> phase = _phases[_currentPhaseIndex];
            if (_currentStepIndex >= phase.Count) return false;

            BuildOrderStep step = phase[_currentStepIndex];

            // delay 타이머가 아직 만료되지 않았고, 재시도 대기 중도 아닐 때만 가속 의미가 있다.
            if (_retryTimer > 0f) return false;
            if (_stepTimer >= step.GetDelaySeconds(_difficulty)) return false;

            // 타이머를 즉시 만료시켜 다음 TickBuildOrder에서 실행되게 한다.
            _stepTimer = step.GetDelaySeconds(_difficulty);

            _r2CooldownTimer = _params.buildOrderAccelCooldown;
            return true;
        }

        // ====================================================================
        // 건물 배치 타일 선택 — BFS (규칙 26)
        // ====================================================================

        /// <summary>
        /// Red 성채에서 BFS로 가장 가까운 배치 가능 타일을 찾는다(규칙 26).
        /// 조건:
        ///   - Red 소유 + IsWalkable=true
        ///   - 기존 Red 생산 건물의 인접 6타일이 아님 (스폰 블로킹 방지)
        /// 조건을 만족하는 첫 번째 타일을 반환. 없으면 null.
        /// </summary>
        private HexCoord? FindPlacementTile()
        {
            HexCoord start = FindAiCastlePosition();

            // 기존 Red 생산 건물의 인접 타일 집합 (제외 대상) 구성.
            var reservedTiles = new HashSet<HexCoord>();
            foreach (var kvp in _buildingPlacement.Buildings)
            {
                BuildingData b = kvp.Value;
                if (b.Team != AiTeam || !BuildingTypeHelper.IsProductionBuilding(b.Type)) continue;

                foreach (var neighbor in _grid.GetNeighbors(b.Position))
                    reservedTiles.Add(neighbor.Coord);
            }

            // BFS 탐색.
            var visited = new HashSet<HexCoord> { start };
            var queue = new Queue<HexCoord>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                HexCoord current = queue.Dequeue();

                // 시작점(성채 타일) 자체는 배치 후보가 아니므로 이웃만 검사.
                foreach (var neighbor in _grid.GetNeighbors(current))
                {
                    HexCoord nc = neighbor.Coord;
                    if (visited.Contains(nc)) continue;
                    visited.Add(nc);

                    // 배치 가능 후보 판정: Red 소유 + walkable + 예약 타일 아님.
                    bool placeable = neighbor.IsWalkable
                                     && neighbor.Owner == AiTeam
                                     && !reservedTiles.Contains(nc);

                    if (placeable)
                        return nc;

                    // 탐색 확장: Red 소유 영역 안에서만 BFS를 넓힌다.
                    // (적/중립 영역으로 새어나가지 않도록 — 거기엔 배치할 수 없으므로)
                    if (neighbor.Owner == AiTeam)
                        queue.Enqueue(nc);
                }
            }

            return null;
        }

        // ====================================================================
        // 골드 부족 시 수동 생산 취소 1회 (규칙 22)
        // ====================================================================

        /// <summary>
        /// 현재 빌드오더 항목에서 아직 취소를 시도하지 않았다면,
        /// AI 배럭의 수동 생산 항목(IsCharged=false 슬롯) 1개를 취소해 골드를 확보 시도한다(규칙 22).
        /// 항목당 1회만 허용된다.
        ///
        /// 참고: AI는 자동생산을 사용하지 않으며(규칙 23) StartProduction 시드는 EnqueueUnit으로
        /// 즉시 차감(IsCharged=true)된다. IsCharged=false 미차감 항목은 골드 부족으로 슬롯 승격에
        /// 실패해 대기 중인 항목을 의미하므로, 이를 취소해도 이미 차감된 골드에는 영향이 없다.
        /// </summary>
        private void TryCancelOneManualProduction()
        {
            if (_cancelTriedThisStep) return;
            _cancelTriedThisStep = true;

            foreach (var barracksId in CollectAiBarracksIds())
            {
                ProductionState state = _unitProduction.GetState(barracksId);
                if (state == null) continue;

                // PendingQueue에서 미차감(IsCharged=false) 슬롯을 뒤에서부터 찾아 1개 취소.
                // 슬롯 인덱스: 슬롯1=PendingQueue[0]→CancelQueueAt(.,1), 슬롯2=PendingQueue[1]→CancelQueueAt(.,2).
                for (int i = state.PendingQueue.Count - 1; i >= 0; i--)
                {
                    if (state.PendingQueue[i].IsCharged) continue;

                    // PendingQueue 인덱스 i → 슬롯 번호 (i+1). 슬롯 0(CurrentProducing)은 대상 아님.
                    int slotNumber = i + 1;
                    if (_unitProduction.CancelQueueAt(barracksId, slotNumber))
                        return; // 1회 취소 성공 — 종료
                }
            }
        }

        // ====================================================================
        // 공통 헬퍼
        // ====================================================================

        /// <summary>
        /// AI(Red) 소유의 모든 생산 건물 barracksId 목록을 수집한다.
        /// </summary>
        private List<int> CollectAiBarracksIds()
        {
            var result = new List<int>();
            foreach (var kvp in _buildingPlacement.Buildings)
            {
                BuildingData b = kvp.Value;
                if (b.Team == AiTeam && BuildingTypeHelper.IsProductionBuilding(b.Type))
                    result.Add(b.Id);
            }
            return result;
        }

        /// <summary>
        /// 특정 팀의 살아있는 유닛 수를 센다 (R1 유닛 수 비교용).
        /// </summary>
        private int CountUnits(TeamId team)
        {
            int count = 0;
            foreach (var kvp in _unitSpawn.Units)
            {
                if (kvp.Value.Team == team && kvp.Value.IsAlive)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 업그레이드로 배럭 Id가 바뀌었을 때, 연속 생산 매핑(_lineProduction)을 새 Id로 이전하고
        /// 새 배럭의 ProductionState를 등록한다.
        /// </summary>
        private void MigrateBarracksId(int oldId, BuildingData newBuilding)
        {
            // 연속 생산 타입 매핑 이전.
            if (_lineProduction.TryGetValue(oldId, out UnitType type))
            {
                _lineProduction.Remove(oldId);
                _lineProduction[newBuilding.Id] = type;
            }

            // 새 배럭 ProductionState 등록 (업그레이드 시 새 Id로 생성됨).
            _unitProduction.RegisterBarracks(newBuilding);

            // 업그레이드 직후 새 배럭에 연속 생산을 즉시 시드하여 생산이 끊기지 않게 한다.
            if (_lineProduction.TryGetValue(newBuilding.Id, out UnitType seedType))
                _unitProduction.EnqueueUnit(newBuilding.Id, seedType);
        }

        /// <summary>
        /// AI(Red) 팀의 종족을 반환한다.
        /// Application 레이어가 GameRaceContext(Infrastructure)에 직접 의존하지 않도록
        /// 종족은 생성자에서 받지 않고, 건물 배치 시점에 GameRaceContext.RedRace를 참조한다.
        ///
        /// 주: GameRaceContext는 Infrastructure지만 본 컨트롤러는 이미 Infrastructure config를
        /// 참조하므로(데이터 컨테이너), 종족 조회도 같은 선상에서 허용된다.
        /// </summary>
        private RaceId GameRaceForAi()
        {
            return GameRaceContext.RedRace;
        }

        // ====================================================================
        // 정리
        // ====================================================================

        /// <summary>
        /// 이벤트 구독을 해제한다. 재경기/씬 전환 시 GameBootstrapper가 호출.
        /// </summary>
        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
