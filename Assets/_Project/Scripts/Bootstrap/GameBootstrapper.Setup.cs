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
                Debug.LogError("[GameBootstrapper] UnitStatsConfig가 연결되지 않았습니다. " +
                               "Inspector의 Config 섹션에서 Assets/_Project/Resources/Config/UnitStatsConfig.asset을 연결해 주세요.");
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
                    HitFrameTimes = entry.hitFrameTimes
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

            Debug.Log($"[GameBootstrapper] UnitStats / UnitProductionStats 초기화 완료. " +
                      $"등록된 유닛 수: {statDict.Count}");
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
                Debug.LogError("[GameBootstrapper] BuildingStatsConfig가 연결되지 않았습니다. " +
                               "Inspector의 Config 섹션에서 Assets/_Project/Resources/Config/BuildingStatsConfig.asset을 연결해 주세요.");
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

            Debug.Log($"[GameBootstrapper] BuildingStats 초기화 완료. " +
                      $"등록된 (건물×종족) 엔트리 수: {dict.Count}");
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

            _unitCombat = new UnitCombatUseCase(
                _grid, _unitSpawn, _buildingPlacement, _positionProvider, hexMapper,
                sweepReach, sweepArcHalfAngle);

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

            Debug.Log($"[Network] 카메라 시작 위치 설정. 팀={localTeam}, 행={cameraRow}, " +
                      $"뷰반전={ViewConverter.IsFlipped}");
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
                // 비생산 건물 액션 패널(_buildingActionPanelUI)을 마지막 인자로 함께 주입.
                // 싱글/멀티 모드 모두 동일 — 멀티는 액션 패널 내부에서 ServerRpc 분기 처리.
                _inputHandler.Initialize(
                    _gridInteraction, _mainCamera,
                    _buildingPlacement, _buildingUI, _productionUI,
                    _buildingActionPanelUI);
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
                Debug.LogError("[GameBootstrapper] AIConfig.asset을 찾을 수 없습니다. " +
                               "메뉴 Hexiege/Setup/AIConfig 생성으로 만들어 주세요. AI를 비활성화합니다.");
                return;
            }

            // 1-A. AI On/Off 토글 점검 (구 _enableAI를 대체).
            //   AIConfig.enableAI = false이면 AI 컨트롤러를 만들지 않고 조기 반환한다.
            //   에러가 아닌 정상 동작이므로 LogError가 아닌 Log로 남긴다.
            if (!aiConfig.enableAI)
            {
                Debug.Log("[GameBootstrapper] AIConfig.enableAI = false — AI를 비활성화합니다.");
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
                Debug.LogError("[GameBootstrapper] AI 시나리오 에셋을 찾을 수 없습니다. " +
                               "AIScenarioConfig_Human.asset이 Resources/Config/에 있는지 확인하세요. " +
                               "AI를 비활성화합니다.");
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
                difficulty);

            Debug.Log($"[GameBootstrapper] AI 초기화 완료. 난이도={difficulty}, " +
                      $"시나리오={scenarioName}, 수입배율={aiParams.goldIncomeMultiplier}");
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
                    Debug.LogWarning($"[GameBootstrapper] 알 수 없는 AI 종족({aiRace})입니다. " +
                                     "시나리오를 로드할 수 없습니다.");
                    return (null, null);
            }

            var config = Resources.Load<AIScenarioConfig>(path);

            if (config == null)
            {
                Debug.LogWarning($"[GameBootstrapper] (종족={aiRace}) {path}.asset을 찾을 수 없습니다.");
                return (null, null);
            }

            if (config.scenarios == null || config.scenarios.Count == 0)
            {
                Debug.LogWarning($"[GameBootstrapper] (종족={aiRace}) {path}.asset의 " +
                                 "scenarios 배열이 비어 있습니다.");
                return (null, null);
            }

            int idx = UnityEngine.Random.Range(0, config.scenarios.Count);
            var bundle = config.scenarios[idx];
            Debug.Log($"[GameBootstrapper] 시나리오 선택: 종족={aiRace}, " +
                      $"{bundle.scenarioName} (인덱스 {idx})");
            return (bundle.steps, bundle.scenarioName);
        }
    }
}
