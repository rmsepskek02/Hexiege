// ============================================================================
// GameBootstrapper.Setup.cs (partial 파일)
//
// 본 파일은 GameBootstrapper의 "초기화 헬퍼" 코드를 분리해 둔 partial이다.
// Inspector 필드 / 생명주기 메서드는 메인 파일(GameBootstrapper.cs)에 있다.
//
// 담당 영역:
//   1. ScriptableObject → Domain 정적 Dictionary 주입
//      - InitializeUnitStatsFromConfig
//      - InitializeBuildingStatsFromConfig
//   2. orientation별 HexMetrics 설정 적용 (ApplyConfig)
//   3. UseCase 인스턴스 일괄 생성 (CreateUseCases)
//   4. 카메라 초기 위치/경계/줌 설정 (SetupCamera, SetCameraStartPositionForTeam)
//   5. 입력 핸들러 의존성 주입 (SetupInput)
//   6. 건물/생산 시스템 초기화 (SetupBuildings, SetupProduction)
//
// 로그 시스템 초기화는 더 이상 이 파일에 없다:
//   기존 InitializeLogging / ShutdownLogging / OnUnityLogMessageReceived 는
//   Infrastructure/Debug/LogSessionOwner.cs 로 이동했다(정적 소유자).
//   씬마다 부트스트래퍼가 달라도 로그 세션이 하나로 유지되게 하기 위해서다.
//
// 규칙:
//   * [SerializeField] 필드를 본 파일에 추가하지 않는다 — Inspector 추적성 보장.
//   * Unity 생명주기 메서드를 본 파일에 두지 않는다 — 중복 정의 방지.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;
using Hexiege.Application.Services;
using Hexiege.Infrastructure;

namespace Hexiege.Bootstrap
{
    public partial class GameBootstrapper
    {
        // ====================================================================
        // UnitStats / UnitProductionStats 초기화
        // ====================================================================

        /// <summary>
        /// Inspector에 연결된 UnitStatsConfig(ScriptableObject)를 읽어
        /// Domain의 UnitStats / UnitProductionStats 정적 Dictionary에 주입한다.
        ///
        /// 왜 여기서?
        ///   Domain 레이어는 Unity(ScriptableObject)에 의존하면 안 된다.
        ///   따라서 ScriptableObject → Domain용 순수 C# 구조체로의 변환을
        ///   Bootstrap(유일하게 전체를 아는 곳)에서 수행한다.
        ///
        /// Config가 연결돼 있지 않으면 에러 로그만 남기고 스킵 —
        /// UnitStats 내부 폴백 값이 사용되지만, 당연히 잘못된 수치이므로
        /// 반드시 Inspector에서 _unitStatsConfig 필드를 연결해야 한다.
        /// </summary>
        private void InitializeUnitStatsFromConfig()
        {
            if (_unitStatsConfig == null)
            {
                // [개발] Inspector 배선 누락 = 설정 오류다(LogRules.md 1.3 분류 원칙 3 의 단서).
                //   원본은 LogError 였지만, 설정 오류는 플레이어 기기의 문제가 아니라
                //   개발 환경의 문제이고 빌드 전에 잡히므로 Warn + 개발로 낮춘다.
                GameLog.Dev.Warn("Bootstrap", nameof(GameBootstrapper),
                                 "UnitStatsConfig 미연결 — Inspector 의 Config 섹션에서 " +
                                 "Assets/_Project/Resources/Config/UnitStatsConfig.asset 을 연결할 것",
                                 "Config=UnitStatsConfig");
                return;
            }

            // Config의 각 항목(UnitStatEntry)을 Domain용 StatValues / ProductionValues로 변환.
            // 동일한 UnitType이 여러 번 등장하면 나중 항목이 이전 항목을 덮어씀.
            var statDict = new System.Collections.Generic.Dictionary<UnitType, UnitStats.StatValues>();
            var prodDict = new System.Collections.Generic.Dictionary<UnitType, UnitProductionStats.ProductionValues>();

            foreach (var entry in _unitStatsConfig.Stats)
            {
                statDict[entry.unitType] = new UnitStats.StatValues
                {
                    MaxHp = entry.maxHp,
                    AttackPower = entry.attackPower,
                    AttackRange = entry.attackRange,
                    DetectRange = entry.detectRange,
                    MoveSpeed = entry.moveSpeed,
                    AttackCooldown = entry.attackCooldown,
                    HitFrameTimes = entry.hitFrameTimes,
                    // 역할 플래그(방식 A) — 힐러(BloomFairy) 여부를 Domain 스탯으로 전달.
                    IsHealer = entry.isHealer,
                    // 방어력(신규) — 전 유닛 0(Phase 1). 구 .asset은 필드가 없어 자동으로 0 폴백.
                    Defense = entry.defense
                };

                prodDict[entry.unitType] = new UnitProductionStats.ProductionValues
                {
                    ProductionTime = entry.productionTime,
                    GoldCost = entry.goldCost,
                    PopulationCost = entry.populationCost
                };
            }

            UnitStats.Initialize(statDict);
            UnitProductionStats.Initialize(prodDict);

            // [개발] 초기화 완료 통보. 축 A: 정상 흐름 → Info / 축 B: 에디터 재현 가능 → 개발.
            //   "등록된 유닛 수"는 자유 문장이 아니라 집계 가능한 값이므로 key=value 로 옮겼다(1.4).
            GameLog.Dev.Info("Bootstrap", nameof(GameBootstrapper),
                             "UnitStats / UnitProductionStats 초기화 완료",
                             $"UnitCount={statDict.Count}");
        }

        // ====================================================================
        // BuildingStats 초기화
        // ====================================================================

        /// <summary>
        /// Inspector에 연결된 BuildingStatsConfig(ScriptableObject)를 읽어
        /// Domain의 BuildingStats 정적 Dictionary에 주입한다.
        ///
        /// 동작 순서:
        ///   1. Config의 각 BuildingTypeEntry를 순회.
        ///   2. 한 entry당 Human / Spirit / Transcendence 3종족의 StatValues를 추출.
        ///   3. (BuildingType, RaceId) 튜플을 키로 하는 Dictionary에 저장.
        ///   4. BuildingStats.Initialize(dict)로 일괄 주입.
        ///
        /// Config 미연결 시: 에러 로그만 남기고 스킵.
        /// 이 경우 BuildingStats 내부 폴백 값이 사용되지만 밸런싱 의도와 다를 수 있으므로
        /// 반드시 Inspector에서 _buildingStatsConfig 필드를 연결해야 한다.
        /// </summary>
        private void InitializeBuildingStatsFromConfig()
        {
            if (_buildingStatsConfig == null)
            {
                // [개발] 위 UnitStatsConfig 와 같은 성격의 Inspector 배선 누락이다.
                //   같은 종류의 사건은 같은 축 값을 써야 지표가 갈라지지 않는다 → Warn + 개발.
                GameLog.Dev.Warn("Bootstrap", nameof(GameBootstrapper),
                                 "BuildingStatsConfig 미연결 — Inspector 의 Config 섹션에서 " +
                                 "Assets/_Project/Resources/Config/BuildingStatsConfig.asset 을 연결할 것",
                                 "Config=BuildingStatsConfig");
                return;
            }

            // (건물 타입, 종족) → 스탯 Dictionary 구성.
            // 한 건물 타입 항목당 3종족이 동시에 등록된다.
            var dict = new System.Collections.Generic.Dictionary<(BuildingType, RaceId), BuildingStats.StatValues>();

            foreach (var entry in _buildingStatsConfig.Stats)
            {
                // UpgradeCost는 BuildingType당 단일 값. 모든 종족 엔트리에 동일 값 주입.
                // (BuildingStats.GetUpgradeCost는 어떤 종족 키로도 같은 값을 반환하도록 동작)
                int upgrade = entry.upgradeCost;

                // Human
                dict[(entry.buildingType, RaceId.Human)] = new BuildingStats.StatValues
                {
                    MaxHp = entry.humanMaxHp,
                    GoldCost = entry.humanGoldCost,
                    AttackPower = entry.humanAttackPower,
                    AttackCooldown = entry.humanAttackCooldown,
                    AttackRange = entry.humanAttackRange,
                    UpgradeCost = upgrade
                };

                // Spirit
                dict[(entry.buildingType, RaceId.Spirit)] = new BuildingStats.StatValues
                {
                    MaxHp = entry.spiritMaxHp,
                    GoldCost = entry.spiritGoldCost,
                    AttackPower = entry.spiritAttackPower,
                    AttackCooldown = entry.spiritAttackCooldown,
                    AttackRange = entry.spiritAttackRange,
                    UpgradeCost = upgrade
                };

                // Transcendence
                dict[(entry.buildingType, RaceId.Transcendence)] = new BuildingStats.StatValues
                {
                    MaxHp = entry.transcendenceMaxHp,
                    GoldCost = entry.transcendenceGoldCost,
                    AttackPower = entry.transcendenceAttackPower,
                    AttackCooldown = entry.transcendenceAttackCooldown,
                    AttackRange = entry.transcendenceAttackRange,
                    UpgradeCost = upgrade
                };
            }

            BuildingStats.Initialize(dict);

            // 환불 캐시를 채울 대상 종족 목록. 생산건물/비생산건물 양쪽 루프에서 공유한다.
            var refundRaces = new[] { RaceId.Human, RaceId.Spirit, RaceId.Transcendence };

            // ── 철거 환불용 누적 투자 비용 계산 및 캐싱 ─────────────────────────
            // 각 생산건물 라인의 1단계 건물에서 시작해 체인을 순방향으로 순회한다.
            // 단계를 거칠수록 이전 단계의 업그레이드 비용이 누적된다.
            // 팝업이 열릴 때마다 계산하지 않도록 게임 시작 시 1회만 계산하여 캐싱한다.

            // 1단계 생산건물 목록 — BuildingTypeHelper lookup table에서 자동 파생.
            // 손으로 나열하지 않으므로, 신규 1단계 생산건물 추가 시 여기를 수정할 필요가 없다.
            // (BuildingTypeHelper._buildingTable에 한 줄 추가하면 이 목록에도 자동 반영된다.)
            var stage1Buildings = Array.FindAll(
                (BuildingType[])Enum.GetValues(typeof(BuildingType)),
                t => BuildingTypeHelper.GetStage(t) == 1);

            foreach (var race in refundRaces)
            {
                foreach (var stage1 in stage1Buildings)
                {
                    // 1단계: 누적 비용 = 1단계 건설비
                    BuildingType currentType = stage1;
                    int accumulated = BuildingStats.GetGoldCost(currentType, race);
                    BuildingStats.SetTotalInvestedCost(currentType, race, accumulated);

                    // 다음 단계가 있는 동안 순방향으로 체인을 순회한다.
                    // 각 단계에서 현재 단계의 업그레이드 비용을 더해 다음 단계의 누적값을 구한다.
                    BuildingType? nextType;
                    while ((nextType = BuildingTypeHelper.GetNextStage(currentType)).HasValue)
                    {
                        accumulated += BuildingStats.GetUpgradeCost(currentType);
                        BuildingStats.SetTotalInvestedCost(nextType.Value, race, accumulated);
                        currentType = nextType.Value;
                    }
                }
            }

            // ── 비생산 건물 환불 캐시 ───────────────────────────────────────────
            // 비생산 건물은 단계 개념이 없으므로 최초 건설 비용 자체가 누적 투자 비용이 된다.
            // 액션 패널(BuildingActionPanelUI)이 GetTotalInvestedCost()로 환불액(50%)을 계산하므로,
            // 여기서 미리 캐시를 채워두지 않으면 환불액이 0으로 표시되는 버그가 생긴다.
            // (Castle은 철거 불가이므로 캐시 불필요 — 넣어도 무해하지만 명시적으로 제외)
            //
            // 대상 enum: BuildingType.cs의 "비생산 건물" 섹션과 1:1 일치(Castle 제외):
            //   MiningPost, AutoTower, FlightFacility, Research, MagicBuilding, HealShrine
            // 비생산 건물 목록(Castle 제외) — lookup table에서 자동 파생.
            // Castle은 철거 불가이므로 환불 캐시가 불필요하여 명시적으로 제외한다.
            var nonProductionBuildings = Array.FindAll(
                (BuildingType[])Enum.GetValues(typeof(BuildingType)),
                t => !BuildingTypeHelper.IsProductionBuilding(t) && t != BuildingType.Castle);
            foreach (var race in refundRaces)
            {
                foreach (var type in nonProductionBuildings)
                {
                    int cost = BuildingStats.GetGoldCost(type, race);
                    BuildingStats.SetTotalInvestedCost(type, race, cost);
                }
            }

            // [개발] 초기화 완료 통보 — UnitStats 쪽과 동일한 판정(Info + 개발).
            GameLog.Dev.Info("Bootstrap", nameof(GameBootstrapper), "BuildingStats 초기화 완료",
                             $"EntryCount={dict.Count}");
        }

        // ====================================================================
        // 설정 적용
        // ====================================================================

        /// <summary>
        /// HexMetrics와 HexOrientationContext에 orientation별 설정 적용.
        /// </summary>
        private void ApplyConfig(HexOrientation orientation, OrientationConfig oc)
        {
            HexMetrics.Orientation = orientation;
            HexOrientationContext.Current = orientation;

            HexMetrics.TileWidth = oc.TileWidth;
            HexMetrics.TileHeight = oc.TileHeight;
            HexMetrics.UnitYOffset = _config.UnitYOffset;
        }

        // ====================================================================
        // UseCase 생성
        // ====================================================================

        /// <summary>
        /// Application 레이어의 UseCase 인스턴스들을 생성하고 그리드 참조 주입.
        /// IHexCoordinateMapper 구현체(HexMetricsCoordinateMapper)를 1회 생성하여
        /// 좌표 변환이 필요한 UseCase들에 공유 주입한다.
        /// </summary>
        private void CreateUseCases()
        {
            // 좌표 변환 매퍼 — Application 레이어가 Core(HexMetrics/ViewConverter)에
            // 직접 의존하지 않도록 인터페이스 뒤로 숨긴다.
            // 한 게임 내에서는 같은 인스턴스를 모든 UseCase가 공유해도 무방.
            IHexCoordinateMapper hexMapper = new HexMetricsCoordinateMapper();

            _gridInteraction = new GridInteractionUseCase(_grid, hexMapper);
            _unitSpawn = new UnitSpawnUseCase(_grid);

            // 플로우 필드 서비스 — 그리드 참조 저장 + walkable 변경 이벤트 구독.
            // UnitMovementUseCase 생성 전에 만들어야 주입이 가능하다.
            // 재경기/맵 전환 시 같은 인스턴스라면 Initialize 내부에서 기존 구독을 정리한다.
            if (_flowFieldService == null)
                _flowFieldService = new FlowFieldService();
            _flowFieldService.Initialize(_grid);

            // 이전 UseCase의 이벤트 구독을 정리한 뒤 새로 생성 (재경기 시 중복 구독 방지).
            _unitMovement?.Dispose();
            _unitMovement = new UnitMovementUseCase(_grid, _unitSpawn, _flowFieldService, hexMapper);
            _buildingPlacement = new BuildingPlacementUseCase(_grid);
            _positionProvider = new UnitWorldPositionProvider(_unitFactory, _buildingFactory);

            // 특수 공격(도끼병 휩쓸기) 튜닝값 — SpecialAttackConfig(SO)에서 float로 읽어 주입.
            // SO가 연결되지 않았으면 코드 기본값(반경 1.0 / 반각 120°)을 폴백으로 사용하여
            // 미주입 상태에서도 휩쓸기가 동작하게 한다. (Application이 SO를 직접 참조하지 않음)
            float sweepReach = _specialAttackConfig != null ? _specialAttackConfig.SweepReach : 1.0f;
            float sweepArcHalfAngle = _specialAttackConfig != null ? _specialAttackConfig.SweepArcHalfAngle : 120f;

            // TorrentSpirit 파도 튜닝값 — 마찬가지로 SO에서 float로 읽어 주입(미연결 시 코드 폴백).
            float waveWidth = _specialAttackConfig != null ? _specialAttackConfig.WaveWidth : 3f;
            float waveLength = _specialAttackConfig != null ? _specialAttackConfig.WaveLength : 3f;
            float waveTravelTime = _specialAttackConfig != null ? _specialAttackConfig.WaveTravelTime : 0.5f;
            float waveHeal = _specialAttackConfig != null ? _specialAttackConfig.WaveHeal : 100f;

            // BloomFairy 지속 회복(HoT) 튜닝값 — SO에서 float로 읽어 주입(미연결 시 코드 폴백).
            // 총 회복량 200HP(×10 스케일) / 지속 3초가 기본값(설계 확정값).
            float bloomHealAmount = _specialAttackConfig != null ? _specialAttackConfig.BloomHealAmount : 200f;
            float bloomHealDuration = _specialAttackConfig != null ? _specialAttackConfig.BloomHealDuration : 3f;

            // MushroomBomber 착탄 DoT 튜닝값 — SO에서 float로 읽어 주입(미연결 시 코드 폴백).
            // 착탄 반경 1.0(인접 1칸) / 초당 피해 20(×10 스케일) / 지속 3초가 기본값(설계 확정값).
            float blastRadius = _specialAttackConfig != null ? _specialAttackConfig.BlastRadius : 1.0f;
            float blastDotPerSecond = _specialAttackConfig != null ? _specialAttackConfig.BlastDotPerSecond : 20f;
            float blastDotDuration = _specialAttackConfig != null ? _specialAttackConfig.BlastDotDuration : 3f;

            // InfernoSpirit 단일 대상 DoT 튜닝값 — SO에서 float로 읽어 주입(미연결 시 코드 폴백).
            // 초당 피해 50(×10 스케일) / 지속 3초(총 150)가 기본값(설계 확정값). MushroomBomber(20/3)와 별개 값.
            float infernoDotPerSecond = _specialAttackConfig != null ? _specialAttackConfig.InfernoDotPerSecond : 50f;
            float infernoDotDuration = _specialAttackConfig != null ? _specialAttackConfig.InfernoDotDuration : 3f;

            // QuakeSpirit 착탄 즉발 스플래시 튜닝값 — SO에서 float로 읽어 주입(미연결 시 코드 폴백).
            // 착탄 반경 1.0(인접 1칸) / 스플래시 비율 0.5(공격력의 50%, 올림)가 기본값(설계 확정값).
            float quakeRadius = _specialAttackConfig != null ? _specialAttackConfig.QuakeRadius : 1.0f;
            float quakeSplashRatio = _specialAttackConfig != null ? _specialAttackConfig.QuakeSplashRatio : 0.5f;

            // MistShrine 물안개 힐 튜닝값 — SO에서 float로 읽어 주입(미연결 시 코드 폴백).
            // ⚠️ 아래 5개는 전부 "임시값 — 밸런싱 미확정"이다(MistShrine 규칙 16 / UI 규칙 9).
            //    밸런싱이 확정되면 SpecialAttackConfig.asset만 고치면 되고 코드 변경은 필요 없다.
            float mistHealPerSecond = _specialAttackConfig != null ? _specialAttackConfig.MistHealPerSecond : 10f;
            float mistDuration = _specialAttackConfig != null ? _specialAttackConfig.MistDuration : 10f;
            float mistCooldown = _specialAttackConfig != null ? _specialAttackConfig.MistCooldown : 20f;
            float mistRadius = _specialAttackConfig != null ? _specialAttackConfig.MistRadius : 3f;
            float mistHealTextInterval = _specialAttackConfig != null ? _specialAttackConfig.MistHealTextInterval : 3f;

            _unitCombat = new UnitCombatUseCase(
                _grid, _unitSpawn, _buildingPlacement, _positionProvider, hexMapper,
                sweepReach, sweepArcHalfAngle,
                waveWidth, waveLength, waveTravelTime, waveHeal,
                bloomHealAmount, bloomHealDuration,
                blastRadius, blastDotPerSecond, blastDotDuration,
                infernoDotPerSecond, infernoDotDuration,
                quakeRadius, quakeSplashRatio);

            // 방어 타워 전투 UseCase.
            // Application 레이어가 GameRaceContext(Infrastructure)에 직접 의존하지 않도록,
            // composition root인 여기서 "팀 → 종족" 변환 함수를 주입한다.
            // 팀별 종족 매핑은 BuildingFactory/UnitFactory와 동일하게
            // Blue → BlueRace, Red → RedRace 규칙을 따른다.
            _towerCombat = new TowerCombatUseCase(
                _buildingPlacement,
                _unitSpawn,
                hexMapper,
                _positionProvider,
                team => team == TeamId.Blue
                    ? GameRaceContext.BlueRace
                    : GameRaceContext.RedRace);

            // ────────────────────────────────────────────────────────────
            // [Phase 2] 연구소 유닛 강화 UseCase — 팀별 트랙 레벨/진행 상태 보관.
            //   데미지(공격보너스·방어감쇄)·이동배율·힐 스케일·자연회복 조회를 전투/이동/타워가 참조하도록 주입.
            //   (B) 방식: 유닛 스냅샷은 그대로 두고 사용 지점에서 팀 레벨을 곱한다 → 소급 강화 자동 성립.
            // ────────────────────────────────────────────────────────────
            _unitUpgrade = new UnitUpgradeUseCase();
            _unitCombat.SetUpgradeUseCase(_unitUpgrade);
            _towerCombat.SetUpgradeUseCase(_unitUpgrade);

            // ────────────────────────────────────────────────────────────
            // [스킬 - 타입 C] 상태효과 시스템 — 유닛별 버프/디버프/제어(빙결·둔화·공격 배율) 보관·틱.
            //   유효 스탯 접근자(EffectiveAttack/GetUnitMoveSpeedMultiplier)와 CanAttack 게이트가 참조하도록 주입.
            //   미주입 시 전투는 기존과 완전히 동일(무상태=배율 1·공격 가능) — 회귀 안전.
            //   틱: 싱글=GameBootstrapper.Update / 멀티 서버=NetworkCombatController / 멀티 클라 미러=Update.
            // ────────────────────────────────────────────────────────────
            _statusEffectSystem = new StatusEffectSystem();
            _unitCombat.SetStatusEffectSystem(_statusEffectSystem);

            // ────────────────────────────────────────────────────────────
            // [스킬 건물] 스킬 발동 UseCase — 발동 재검증·실행·글로벌 쿨다운 보관(서버 권위).
            //   데이터 제공자(_skillLoadoutConfig)는 미연결 시 null → Activate가 조용히 실패(안전).
            //   팀 → 종족 변환은 TowerCombatUseCase와 동일 규칙(Blue→BlueRace, Red→RedRace).
            //   피해/DoT 실제 적용은 _unitCombat의 "건물/스킬 출처 전용 경로"가 담당한다.
            // ────────────────────────────────────────────────────────────
            _skillActivation = new SkillActivationUseCase(
                _buildingPlacement,
                // 조준 연속 좌표(도메인 월드)가 맵 경계 안 점인지 재검증(규칙 22·26).
                //   좌표화 이후 HasTile(정수 타일) 대신 맵 월드 경계(최외곽 타일 바깥선) 판정을 쓴다.
                //   람다가 현재 _grid를 읽으므로 맵 재로드에도 최신 그리드 크기를 반영한다.
                aimWorld => _grid != null && HexMetrics.IsWithinMapBounds(aimWorld, _grid.Width, _grid.Height),
                _unitCombat,
                _skillLoadoutConfig, // ISkillDataProvider (SkillLoadoutConfig SO). null 허용.
                team => team == TeamId.Blue
                    ? GameRaceContext.BlueRace
                    : GameRaceContext.RedRace);

            // ────────────────────────────────────────────────────────────
            // [MistShrine] 물안개 힐 UseCase — 시전 재검증·물안개 수명·1초 회복·쿨다운·자동 모드(서버 권위).
            //   기존 HoT/DoT 시스템(_activeTimedEffects)을 쓰지 않는 독립 채널이다(규칙 8-2·14).
            //   좌표 변환은 hexMapper(IHexCoordinateMapper)로 주입해 Application이 Core를 모르게 한다.
            // ────────────────────────────────────────────────────────────
            _mistShrine = new MistShrineUseCase(
                _buildingPlacement,
                _unitSpawn,
                hexMapper,
                mistHealPerSecond,
                mistDuration,
                mistCooldown,
                mistRadius,
                mistHealTextInterval);

            // TileOwnershipService 초기화.
            // _grid, _unitSpawn, _positionProvider, hexMapper가 모두 준비된 직후에 생성한다.
            // 매 프레임 GameBootstrapper.Update()에서 Tick()이 호출되며,
            // 유닛 이동 방식(Phase 0/1/2)에 무관하게 시각 위치를 기준으로 타일을 점령한다.
            _tileOwnership = new TileOwnershipService(_grid, _unitSpawn, _positionProvider, hexMapper);

            // ────────────────────────────────────────────────────────────
            // 혼잡도 시스템 인스턴스 초기화.
            //   - 튜닝 값(DecayInterval / CongestionWeight)은 GameConfig(_config)에 통합되어 있어
            //     별도 ScriptableObject 로드가 필요 없다. ProductionTicker가 _config를 직접 참조.
            //   - _congestionMap / _congestionPathfinder: 매 LoadMap()마다 새로 만들어 잔여 혼잡도 차단.
            //   - _congestionSub: OnUnitEnteredTile 구독 — 서버에서만 누적되도록 가드.
            // ────────────────────────────────────────────────────────────
            _congestionMap = new CongestionMap();
            _congestionPathfinder = new CongestionAwarePathfinder();

            // 기존 구독이 있다면 정리(재경기/맵 전환 시 중복 구독 방지).
            _congestionSub?.Dispose();
            // UniRx Subscribe로 구독하고, 반환된 IDisposable로 해제한다.
            _congestionSub = GameEvents.OnUnitEnteredTile.Subscribe(e =>
            {
                // 서버(또는 싱글플레이)에서만 누적. 클라이언트는 시각 동기화만 받으므로 누적 불요.
                if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;
                _congestionMap?.Increment(e.Coord);
            });

            // 생산 시스템
            _resource = new ResourceUseCase(_config.StartingGold);
            _population = new PopulationUseCase(_grid, _unitSpawn, _buildingPlacement);
            _unitProduction = new UnitProductionUseCase(
                _grid, _unitSpawn, _resource, _population, _buildingPlacement);

            // 게임 종료 판정
            _gameEnd = new GameEndUseCase();

            // ────────────────────────────────────────────────────────────
            // [Phase 4] 연구소(Research) 파괴 시 진행 중 연구 취소 + 투입 골드 100% 환불(규칙 8).
            //   서버(또는 싱글플레이)에서만 처리 — 클라이언트는 골드/레벨을 동기화로 수신하므로 이중 환불 금지.
            //   연구는 특정 연구소에 종속되지 않지만, 각 진행 연구는 착수한 연구소 Id를 기억하므로
            //   그 연구소가 파괴되면 해당 연구만 취소·환불한다(다른 연구소의 병렬 연구는 유지).
            // ────────────────────────────────────────────────────────────
            _labDestroyedSub?.Dispose();
            _labDestroyedSub = GameEvents.OnBuildingDied.Subscribe(e =>
            {
                if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;
                if (e.Building == null || e.Building.Type != BuildingType.Research) return;
                _unitUpgrade?.OnLabDestroyed(e.Building.Id, _resource);
            });

            // ────────────────────────────────────────────────────────────
            // [MistShrine] 신전 파괴·철거 시 상태 정리(규칙 12·25).
            //   ① 그 건물이 만든 물안개 즉시 제거 ② 자동 모드 제거 ③ 쿨다운 제거.
            //   서버(또는 싱글플레이)에서만 처리한다 — 클라이언트는 물안개를 갖고 있지 않다.
            //   패널 자동 닫힘은 BuildingPanelBase가 이미 공통으로 처리하므로 여기서 다루지 않는다.
            //   (연구소 파괴 처리와 완전히 동일한 패턴: 서버 가드 + 타입 필터 + 재구독 전 Dispose.)
            // ────────────────────────────────────────────────────────────
            _mistShrineDestroyedSub?.Dispose();
            _mistShrineDestroyedSub = GameEvents.OnBuildingDied.Subscribe(e =>
            {
                if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;
                if (e.Building == null || e.Building.Type != BuildingType.HealShrine) return;
                _mistShrine?.OnShrineDestroyed(e.Building.Id);
            });
        }

        // ====================================================================
        // 카메라 설정
        // ====================================================================

        /// <summary>
        /// 카메라 초기 위치를 맵 중심으로 설정하고, 이동 경계를 지정.
        /// XZ 평면 기반: X, Z를 맵 이동축으로 사용.
        /// 틸트 각도를 적용하고, 틸트로 인한 Z 오프셋을 보정.
        /// </summary>
        private void SetupCamera(HexOrientation orientation, OrientationConfig oc)
        {
            if (_cameraController == null || _config == null) return;

            // 틸트 적용 (CameraController.Start()에서도 호출되지만, SetupCamera가 먼저 실행될 수 있음)
            _cameraController.ApplyTilt();

            // 맵 중심 계산 (XZ 평면)
            Vector3 center = HexMetrics.GridCenter(oc.GridWidth, oc.GridHeight);

            // 틸트 보정: 틸트된 카메라가 정면을 바라보도록 Z 오프셋 적용.
            // 카메라가 X축으로 틸트되면, 화면 중앙이 카메라 직하가 아닌 앞쪽을 가리킴.
            // zOffset = height / tan(tiltAngle) — 카메라 높이에서 XZ 평면까지의 수평 거리.
            Vector3 cameraPos = center;
            float tiltAngle = _cameraController.TiltAngle;
            if (tiltAngle > 0f && tiltAngle < 90f)
            {
                float cameraHeight = _mainCamera != null ? _mainCamera.transform.position.y : 15f;
                float zOffset = cameraHeight / Mathf.Tan(tiltAngle * Mathf.Deg2Rad);
                cameraPos.z -= zOffset;
            }
            _cameraController.SetPosition(cameraPos);

            // 맵 경계 설정 (여유분 포함, XZ 평면)
            Vector3 topLeft = HexMetrics.HexToWorld(
                HexGrid.OffsetToCube(0, 0, orientation));
            Vector3 bottomRight = HexMetrics.HexToWorld(
                HexGrid.OffsetToCube(oc.GridWidth - 1, oc.GridHeight - 1, orientation));

            float margin = 2f;
            Vector3 size = new Vector3(
                Mathf.Abs(bottomRight.x - topLeft.x) + margin * 2,
                0f, // Y축은 높이 — 경계 불필요
                Mathf.Abs(topLeft.z - bottomRight.z) + margin * 2);

            _cameraController.SetBounds(center, size);

            // 기본 줌 레벨 설정
            if (_mainCamera != null)
            {
                _mainCamera.orthographicSize = _config.CameraZoomDefault;
            }
        }

        /// <summary>
        /// 로컬 플레이어 팀에 맞춰 카메라 초기 위치를 설정.
        /// Blue 팀: 맵 하단(자신의 Castle 근처), Red 팀: 반전된 뷰 기준 하단.
        /// ViewConverter가 설정된 후 호출되어야 함.
        /// XZ 평면 기반: X, Z로 맵 위치, Y는 카메라 높이(CameraController가 유지).
        /// </summary>
        private void SetCameraStartPositionForTeam(TeamId localTeam, OrientationConfig oc)
        {
            if (_cameraController == null) return;

            // 양 팀 모두 '자기 진영 = 화면 하단' 규칙 → 카메라는 항상 맵 하단 행을 향함
            // Red팀은 ViewConverter가 반전하므로 도메인 좌표에서는 상단(Red Castle 근처)을 지정
            int cameraRow;
            if (localTeam == TeamId.Red)
            {
                // Red 팀: 도메인 좌표에서 맵 상단 (ViewConverter가 뷰에서 하단으로 반전)
                cameraRow = 2;
            }
            else
            {
                // Blue 팀(또는 기본): 맵 하단 (Blue Castle 근처)
                cameraRow = oc.GridHeight - 3;
            }

            HexCoord cameraTargetCoord = HexGrid.OffsetToCube(
                oc.GridWidth / 2, cameraRow, HexOrientation.FlatTop);
            Vector3 startPos = HexMetrics.HexToWorld(cameraTargetCoord);
            // 카메라 위치도 뷰 좌표계로 변환 (Red팀이면 반전)
            startPos = ViewConverter.ToView(startPos);

            // 틸트 보정: 카메라가 틸트되어 있으면 목표 지점이 화면 중앙에 오도록 Z 오프셋 적용
            float tiltAngle = _cameraController.TiltAngle;
            if (tiltAngle > 0f && tiltAngle < 90f)
            {
                float cameraHeight = _mainCamera != null ? _mainCamera.transform.position.y : 15f;
                float zOffset = cameraHeight / Mathf.Tan(tiltAngle * Mathf.Deg2Rad);
                startPos.z -= zOffset;
            }

            // Y는 카메라 높이 — CameraController.SetPosition()이 기존 Y를 유지
            _cameraController.SetPosition(startPos);

            // [개발] 카메라 배치 결과 확인용 흐름 추적. 축 A: 정상 흐름 → Info / 축 B: 개발.
            //
            // ⚠️ 참고 — 이 메서드는 2026-08-18 실측 기준 **호출부가 한 곳도 없다.**
            //    (StartNetworkGame 은 "싱글플레이와 동일하게 맵 중심에서 시작" 방침이라 부르지 않는다.)
            //    즉 이 로그는 현재 실행되지 않는다. 그래도 형식만 이관해 두는 이유는,
            //    나중에 팀별 카메라 시작 위치가 되살아났을 때 이 자리만 옛 방식으로 남는 것을 막기 위해서다.
            GameLog.Dev.Info("Bootstrap", nameof(GameBootstrapper), "카메라 시작 위치 설정",
                             $"Team={localTeam}, Row={cameraRow}, ViewFlipped={ViewConverter.IsFlipped}");
        }

        // ====================================================================
        // 입력 연결
        // ====================================================================

        /// <summary>
        /// InputHandler에 UseCase 의존성을 주입.
        /// </summary>
        private void SetupInput()
        {
            if (_inputHandler != null)
            {
                // 비생산 건물 액션 패널(_buildingActionPanelUI)과 연구소 강화 패널(_researchPanelUI)을 함께 주입.
                // 싱글/멀티 모드 모두 동일 — 멀티는 각 패널 내부에서 ServerRpc 분기 처리.
                // _researchPanelUI 가 씬에 미배선(null)이면 연구소 클릭은 기존 액션 패널로 폴백된다.
                _inputHandler.Initialize(
                    _gridInteraction, _mainCamera,
                    _buildingPlacement, _buildingUI, _productionUI,
                    _buildingActionPanelUI, _researchPanelUI,
                    // 스킬 건물 라우팅 패널 + 조준 컨트롤러(미배선 시 null → 기존 액션 패널로 폴백/조준 억제 없음).
                    _buildingSkillPanelUI, _skillAimController,
                    // MistShrine 라우팅 패널(미배선 시 null → 기존 액션 패널로 폴백).
                    _mistShrinePanelUI);
            }

            // 스킬 지점 조준 컨트롤러 초기화(있을 때만 — 프리팹/씬 배선은 사용자 Unity 작업).
            //   좌표화 이후: 타일 유효성(HasTile) 대신 "연속 좌표 clamp + 맵 경계 안 점 판정"을 주입한다.
            //   두 람다 모두 현재 _grid를 읽으므로 맵 재로드 시에도 최신 그리드 크기를 반영한다.
            if (_skillAimController != null)
            {
                _skillAimController.Initialize(
                    _mainCamera,
                    _cameraController,
                    // 연속 도메인 좌표를 맵 경계 안으로 clamp(규칙 22 — 최외곽 타일 바깥선 기준).
                    domain => _grid != null ? HexMetrics.ClampToMapBounds(domain, _grid.Width, _grid.Height) : domain,
                    // 연속 도메인 좌표가 맵 경계 안인지 판정(기본 조준 위치 결정용).
                    domain => _grid == null || HexMetrics.IsWithinMapBounds(domain, _grid.Width, _grid.Height));
            }
        }

        // ====================================================================
        // 건물 시스템
        // ====================================================================

        /// <summary>
        /// 건물 시스템 초기화. BuildingFactory에 설정 전달, BuildingPlacementUI 초기화.
        /// 멀티플레이 모드라면 NetworkBuildingController를 UI에 주입하여 ServerRpc 경유 배치 활성화.
        /// </summary>
        private void SetupBuildings()
        {
            if (_buildingFactory != null)
                _buildingFactory.SetBuildingYOffset(_config.BuildingYOffset);

            if (_buildingUI != null)
            {
                // 네트워크 모드 여부에 따라 컨트롤러를 주입 (싱글플레이 시 null 전달)
                bool isNetworkMode = IsNetworkMode();

                Hexiege.Infrastructure.NetworkBuildingController controller =
                    isNetworkMode ? _networkBuildingController : null;

                _buildingUI.Initialize(_buildingPlacement, _resource, _config, controller);
            }

            // ────────────────────────────────────────────────────────────
            // 비생산 건물 공용 액션 패널 초기화.
            //   MiningPost / AutoTower / FlightFacility / Research / MagicBuilding / HealShrine 등
            //   "유닛 생산 UI가 필요 없는" 건물을 클릭했을 때 표시되는 간이 팝업.
            //   현재 지원 동작: 건물 이름(헤더) + 철거(환불 골드 자동 지급).
            //   멀티플레이 시 NetworkBuildingController를 주입해 ServerRpc 경유 철거 수행.
            // ────────────────────────────────────────────────────────────
            if (_buildingActionPanelUI != null)
            {
                bool isNetworkMode = IsNetworkMode();
                Hexiege.Infrastructure.NetworkBuildingController controller =
                    isNetworkMode ? _networkBuildingController : null;
                _buildingActionPanelUI.Initialize(_buildingPlacement, _resource, controller);
            }

            // ────────────────────────────────────────────────────────────
            // [Phase 4] 연구소 강화 패널 초기화(있을 때만 — 프리팹/씬 배선은 사용자 Unity 작업).
            //   멀티플레이면 NetworkUpgradeController를 주입해 ServerRpc 경유 연구 착수.
            //   연구소 클릭 → _researchPanelUI.Open(building) 라우팅은 InputHandler/액션 패널에서 배선 필요(사용자 작업).
            // ────────────────────────────────────────────────────────────
            if (_researchPanelUI != null)
            {
                bool isNetworkMode = IsNetworkMode();
                Hexiege.Infrastructure.NetworkUpgradeController upgradeController =
                    isNetworkMode ? _networkUpgradeController : null;
                // 연구 패널이 BuildingPanelBase를 상속하므로 철거(하단 버튼)용 의존성도 함께 주입한다.
                //   - _buildingPlacement: 건물 제거(철거).
                //   - networkBuildingController: 멀티플레이 철거 요청 중계(싱글은 null).
                Hexiege.Infrastructure.NetworkBuildingController buildingController =
                    isNetworkMode ? _networkBuildingController : null;
                _researchPanelUI.Initialize(_unitUpgrade, _resource, upgradeController,
                    _buildingPlacement, buildingController);
            }

            // ────────────────────────────────────────────────────────────
            // [스킬 건물] 전용 스킬 패널 초기화(있을 때만 — 프리팹/씬 배선은 사용자 Unity 작업).
            //   멀티플레이면 NetworkSkillController(발동 중계)·NetworkBuildingController(철거 중계)를 주입한다.
            //   스킬 로드아웃(_skillLoadoutConfig)은 ISkillDataProvider로 주입되어 슬롯 1~5를 채운다.
            // ────────────────────────────────────────────────────────────
            if (_buildingSkillPanelUI != null)
            {
                bool isNetworkMode = IsNetworkMode();
                Hexiege.Infrastructure.NetworkSkillController skillController =
                    isNetworkMode ? _networkSkillController : null;
                Hexiege.Infrastructure.NetworkBuildingController buildingControllerForSkill =
                    isNetworkMode ? _networkBuildingController : null;

                _buildingSkillPanelUI.Initialize(
                    _buildingPlacement,
                    _resource,
                    buildingControllerForSkill,
                    _skillActivation,
                    _skillLoadoutConfig, // ISkillDataProvider (null 허용).
                    _skillAimController,
                    skillController);     // INetworkSkillController (null=싱글).
            }

            // ────────────────────────────────────────────────────────────
            // [MistShrine] 전용 물안개 힐 패널 초기화(있을 때만 — 프리팹/씬 배선은 에디터 셋업 스크립트).
            //   멀티플레이면 NetworkMistShrineController(시전·자동 토글 중계)와
            //   NetworkBuildingController(철거 중계)를 함께 주입한다. 싱글은 둘 다 null.
            //   패널이 미배선(null)이면 MistShrine 클릭은 기존 공용 액션 패널로 폴백된다(안전망).
            // ────────────────────────────────────────────────────────────
            if (_mistShrinePanelUI != null)
            {
                bool isNetworkMode = IsNetworkMode();
                Hexiege.Infrastructure.NetworkMistShrineController mistController =
                    isNetworkMode ? _networkMistShrineController : null;
                Hexiege.Infrastructure.NetworkBuildingController buildingControllerForMist =
                    isNetworkMode ? _networkBuildingController : null;

                _mistShrinePanelUI.Initialize(
                    _buildingPlacement,
                    _resource,
                    buildingControllerForMist,
                    _mistShrine,
                    mistController);      // INetworkMistShrineController (null=싱글).
            }
        }

        // ====================================================================
        // 생산 시스템
        // ====================================================================

        /// <summary>
        /// 생산 시스템 초기화. UnitFactory 의존성 주입, 생산 UI, 생산 티커.
        /// 멀티플레이 모드라면 NetworkProductionController를 생산 UI에 주입하여 ServerRpc 경유 큐 등록 활성화.
        /// </summary>
        private void SetupProduction()
        {
            // UnitFactory에 런타임 의존성 주입 (생산된 유닛에 자동 적용).
            // _positionProvider는 UnitView의 월드 좌표 직선 추적/회전에서 사용.
            if (_unitFactory != null)
                _unitFactory.SetDependencyReferences(_unitMovement, _unitCombat,
                    _unitFactory, _buildingFactory, _positionProvider);

            // 생산 티커 초기화 (ProductionPanelUI보다 먼저 — UI에서 마커 참조 필요).
            // 혼잡도 시스템(v2) 인스턴스를 함께 주입한다.
            //   _grid / _congestionMap / _congestionPathfinder는 모두 null일 수 있고,
            //   그 경우 ProductionTicker가 BFS 폴백 경로로 동작한다.
            //   혼잡도 튜닝 값은 _config(GameConfig)에 통합되어 별도 인자가 필요 없다.
            if (_productionTicker != null)
                _productionTicker.Initialize(
                    _unitProduction, _resource, _unitMovement,
                    _buildingPlacement, _unitFactory, _config,
                    _grid, _congestionMap, _congestionPathfinder);

            // 네트워크 모드 여부에 따라 NetworkProductionController 주입 (싱글플레이 시 null)
            bool isNetworkMode = IsNetworkMode();

            Hexiege.Infrastructure.NetworkProductionController productionController =
                isNetworkMode ? _networkProductionController : null;

            // 생산 패널 UI 초기화 (네트워크 컨트롤러 포함)
            // 업그레이드 기능을 위해 BuildingPlacementUseCase + NetworkBuildingController도 함께 주입.
            Hexiege.Infrastructure.NetworkBuildingController buildingControllerForProduction =
                isNetworkMode ? _networkBuildingController : null;
            if (_productionUI != null)
                _productionUI.Initialize(
                    _unitProduction,
                    _resource,
                    _population,
                    _productionTicker,
                    productionController,
                    _buildingPlacement,
                    buildingControllerForProduction);
        }

        // ====================================================================
        // AI 시스템 초기화 (GameSystemRules_AI.md 규칙 30~34)
        // ====================================================================

        /// <summary>
        /// 싱글플레이 AI(Red 팀)를 초기화한다.
        /// LoadMap()의 SetupProduction() 직후, 싱글플레이일 때 호출된다.
        /// AI On/Off는 이 메서드 내부에서 AIConfig.enableAI를 점검해 결정한다.
        ///
        /// 동작:
        ///   1. Resources에서 AIConfig / AIScenarioConfig 에셋 로드
        ///   2. LocalPlayerDifficulty.Current에 해당하는 DifficultyParams 선택
        ///   3. Human 시나리오 A/B/C 중 무작위 1개 선택 (규칙 11 — 다양성)
        ///   4. ResourceUseCase에 Red 팀 수입 배율 적용 (규칙 34)
        ///   5. AIOpponentController 생성 + 의존성 주입 → _aiController에 보관
        ///
        /// 에셋이 없으면 에러 로그 후 AI를 생성하지 않는다(게임은 정상 진행, AI만 비활성).
        /// </summary>
        private void InitializeAI()
        {
            // 재경기 안전성: 이전 AI 컨트롤러가 남아있으면 구독 해제 후 폐기.
            _aiController?.Dispose();
            _aiController = null;

            // 1. AIConfig 로드 (Resources/Config/AIConfig)
            AIConfig aiConfig = Resources.Load<AIConfig>("Config/AIConfig");
            if (aiConfig == null)
            {
                // [개발] Resources 에셋 누락도 Inspector 배선 누락과 같은 "설정 오류"다.
                //   프로젝트에 에셋이 있으면 모든 기기에서 있고, 없으면 모든 기기에서 없다 —
                //   플레이어 기기에서만 벌어지는 일이 아니므로 축 B 는 개발이다(1.3 원칙 3 단서).
                //   축 A: AI 만 꺼지고 게임은 정상 진행된다 → 복구됨 → Warn.
                GameLog.Dev.Warn("Bootstrap", nameof(GameBootstrapper),
                                 "AIConfig.asset 을 찾을 수 없어 AI 를 비활성화한다 — " +
                                 "메뉴 Hexiege/Setup/AIConfig 생성으로 만들 것",
                                 "Asset=Config/AIConfig");
                return;
            }

            // 1-A. AI On/Off 토글 점검 (구 _enableAI를 대체).
            //   AIConfig.enableAI = false이면 AI 컨트롤러를 만들지 않고 조기 반환한다.
            //   에러가 아닌 정상 동작이므로 LogError가 아닌 Log로 남긴다.
            if (!aiConfig.enableAI)
            {
                // [개발] 설정대로 동작한 것이므로 에러가 아니다 → 축 A Info / 축 B 개발.
                GameLog.Dev.Info("Bootstrap", nameof(GameBootstrapper),
                                 "AIConfig.enableAI = false — AI 를 비활성화한다",
                                 "EnableAI=False");
                return;
            }

            // 2. 난이도 파라미터 선택
            DifficultyLevel difficulty = LocalPlayerDifficulty.Current;
            DifficultyParams aiParams = aiConfig.GetParams(difficulty);

            // 3. AI(Red 팀) 종족에 맞는 시나리오 에셋을 로드하고 3개 중 하나를 무작위 선택.
            //    종족 결정은 GameRaceContext.RedRace를 따른다 (LoadScenarioBundleForRace 내부).
            var (scenarioSteps, scenarioName) = LoadScenarioBundleForRace();
            if (scenarioSteps == null)
            {
                // [개발] AIConfig 누락(위)과 같은 성격의 에셋 설정 오류 → Warn + 개발.
                //   실제 누락 사유(경로 없음 / scenarios 배열이 빔)는 LoadScenarioBundleForRace 가
                //   종족과 함께 이미 남기므로, 여기서는 "AI 를 껐다"는 결과만 남긴다.
                GameLog.Dev.Warn("Bootstrap", nameof(GameBootstrapper),
                                 "AI 시나리오 에셋을 찾을 수 없어 AI 를 비활성화한다 — " +
                                 "AIScenarioConfig_(종족).asset 이 Resources/Config/ 에 있는지 확인할 것",
                                 $"AiRace={GameRaceContext.RedRace}");
                return;
            }

            // 4. Red 팀 채굴소 수입 배율 적용 (규칙 34 — 게임 시작 시 1회).
            _resource.SetIncomeMultiplier(TeamId.Red, aiParams.goldIncomeMultiplier);

            // 5. AIOpponentController 생성 + 주입.
            _aiController = new AIOpponentController(
                _buildingPlacement,
                _unitProduction,
                _resource,
                _unitSpawn,
                _grid,
                aiParams,
                scenarioSteps,
                scenarioName,
                difficulty,
                _unitUpgrade); // [Phase 5] 연구 스텝(StartResearch) 실행용. null이면 연구 스텝은 스킵.

            // [개발] 초기화 완료 통보 → Info + 개발.
            GameLog.Dev.Info("Bootstrap", nameof(GameBootstrapper), "AI 초기화 완료",
                             $"Difficulty={difficulty}, Scenario={scenarioName}, " +
                             $"GoldIncomeMultiplier={aiParams.goldIncomeMultiplier}");
        }

        /// <summary>
        /// AI(Red 팀)의 종족에 맞는 시나리오 에셋을 로드하고,
        /// 그 안에 담긴 3개 시나리오 중 하나를 무작위로 선택해 반환한다.
        ///
        /// 종족 결정:
        ///   GameRaceContext.RedRace(현재 AI 팀 종족)에 따라 로드할 에셋 경로가 달라진다.
        ///     - RaceId.Human         → "Config/AIScenarioConfig_Human"
        ///     - RaceId.Spirit        → "Config/AIScenarioConfig_Spirit"
        ///     - RaceId.Transcendence → "Config/AIScenarioConfig_Transcendence"
        ///
        /// 타이밍 안전성:
        ///   GameRaceContext.Set(...)이 InitializeAI()보다 먼저 실행되므로,
        ///   이 메서드 호출 시점에는 RedRace 값이 이미 확정되어 있다.
        /// </summary>
        /// <returns>선택된 BuildOrderStep 목록과 시나리오 이름. 에셋 없으면 (null, null).</returns>
        private (IReadOnlyList<BuildOrderStep> steps, string name) LoadScenarioBundleForRace()
        {
            // AI(Red 팀)의 종족을 기준으로 로드할 에셋 경로를 결정한다.
            RaceId aiRace = GameRaceContext.RedRace;

            // 종족 → Resources 경로(확장자 제외) 매핑.
            // 새 종족이 추가되면 이 switch에 한 줄만 더하면 된다.
            string path;
            switch (aiRace)
            {
                case RaceId.Human:
                    path = "Config/AIScenarioConfig_Human";
                    break;
                case RaceId.Spirit:
                    path = "Config/AIScenarioConfig_Spirit";
                    break;
                case RaceId.Transcendence:
                    path = "Config/AIScenarioConfig_Transcendence";
                    break;
                default:
                    // [개발] 새 종족을 RaceId 에 추가하고 이 switch 를 갱신하지 않았을 때만 도달한다.
                    //   즉 코드 버그이며 에디터에서 그대로 재현된다 → 축 B ①이 "아니오" → 개발.
                    //   축 A: AI 만 꺼지고 게임은 진행된다 → Warn.
                    GameLog.Dev.Warn("Bootstrap", nameof(GameBootstrapper),
                                     "알 수 없는 AI 종족 — 시나리오를 로드할 수 없다",
                                     $"AiRace={aiRace}");
                    return (null, null);
            }

            var config = Resources.Load<AIScenarioConfig>(path);

            if (config == null)
            {
                // [개발] Resources 에셋 누락 = 설정 오류 → Warn + 개발(1.3 원칙 3 단서).
                GameLog.Dev.Warn("Bootstrap", nameof(GameBootstrapper),
                                 "AI 시나리오 에셋을 찾을 수 없다",
                                 $"AiRace={aiRace}, Asset={path}");
                return (null, null);
            }

            if (config.scenarios == null || config.scenarios.Count == 0)
            {
                // [개발] 에셋은 있는데 내용이 비어 있는 경우 — 위와 같은 에셋 설정 오류다.
                //   원인이 "없음"이 아니라 "비어 있음"이라 조치가 다르므로 메시지를 나눠 둔다.
                GameLog.Dev.Warn("Bootstrap", nameof(GameBootstrapper),
                                 "AI 시나리오 에셋의 scenarios 배열이 비어 있다",
                                 $"AiRace={aiRace}, Asset={path}");
                return (null, null);
            }

            int idx = UnityEngine.Random.Range(0, config.scenarios.Count);
            var bundle = config.scenarios[idx];
            // [개발] 무작위 선택 결과 추적용 → Info + 개발.
            GameLog.Dev.Info("Bootstrap", nameof(GameBootstrapper), "AI 시나리오 선택",
                             $"AiRace={aiRace}, Scenario={bundle.scenarioName}, Index={idx}");
            return (bundle.steps, bundle.scenarioName);
        }
    }
}
