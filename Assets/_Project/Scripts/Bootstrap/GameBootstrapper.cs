// ============================================================================
// GameBootstrapper.cs
// 씬 진입점. 게임 시작 시 모든 시스템을 초기화하고 연결(와이어링)하는 컴포넌트.
//
// 부착 위치: [Managers]/GameBootstrapper
//
// 역할:
//   1. GameConfig에서 설정 읽기 → HexMetrics에 타일 크기 적용
//   2. HexGrid(Domain) 생성 (7×17, 모바일 9:16 기준)
//   3. UseCase 인스턴스 생성 (GridInteraction, UnitMovement, UnitSpawn)
//   4. HexGridRenderer에 그리드 데이터 전달 → 타일 렌더링
//   5. CameraController에 맵 경계 설정 + 초기 위치
//   6. InputHandler에 UseCase 의존성 주입
//
// "와이어링"이란?
//   각 컴포넌트가 서로 필요한 참조를 연결하는 과정.
//   Clean Architecture에서는 최상위 진입점 하나에서 모든 의존성을 주입.
//   각 레이어는 자신이 필요한 것을 "받기만" 하고 직접 생성하지 않음.
//
// 실행 순서 보장:
//   Awake()가 아닌 Start()에서 초기화. 다른 컴포넌트의 Awake()가 먼저 실행된 후.
//
// Bootstrap 레이어 — 모든 레이어에 의존 (유일하게 전체를 아는 곳).
// ============================================================================

using Unity.Netcode;
using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;
using Hexiege.Application.Services;
using Hexiege.Infrastructure;
using Hexiege.Presentation;

namespace Hexiege.Bootstrap
{
    public class GameBootstrapper : MonoBehaviour
    {
        // ====================================================================
        // Inspector에서 설정할 참조
        // ====================================================================

        [Header("Config")]
        [Tooltip("전역 설정 ScriptableObject")]
        [SerializeField] private GameConfig _config;

        [Tooltip("유닛 전투/생산 수치 ScriptableObject. UnitStats / UnitProductionStats의 소스.")]
        [SerializeField] private UnitStatsConfig _unitStatsConfig;

        [Tooltip("건물 HP/골드비용/공격력 ScriptableObject. BuildingStats의 소스.")]
        [SerializeField] private BuildingStatsConfig _buildingStatsConfig;

        // [Phase 2] UnitAnimationData 제거 — Animator(Mecanim)가 대체

        [Header("Scene References")]
        [Tooltip("[World]/HexGrid 오브젝트의 HexGridRenderer")]
        [SerializeField] private HexGridRenderer _gridRenderer;

        [Tooltip("Main Camera의 CameraController")]
        [SerializeField] private CameraController _cameraController;

        [Tooltip("[Input]/InputHandler")]
        [SerializeField] private InputHandler _inputHandler;

        [Tooltip("UnitFactory 컴포넌트")]
        [SerializeField] private UnitFactory _unitFactory;

        [Tooltip("BuildingFactory 컴포넌트")]
        [SerializeField] private BuildingFactory _buildingFactory;

        [Tooltip("건물 선택 팝업 UI")]
        [SerializeField] private BuildingPlacementUI _buildingUI;

        [Tooltip("생산 패널 UI")]
        [SerializeField] private ProductionPanelUI _productionUI;

        [Tooltip("생산 티커")]
        [SerializeField] private ProductionTicker _productionTicker;

        [Tooltip("메인 카메라")]
        [SerializeField] private Camera _mainCamera;

        [Tooltip("게임 종료 UI")]
        [SerializeField] private GameEndUI _gameEndUI;

        [Tooltip("골드/인구 HUD")]
        [SerializeField] private GameHudUI _gameHudUI;

        [Header("Network")]
        [Tooltip("네트워크 게임 세션 관리 컴포넌트 (씬에 NetworkGameManager GameObject 배치 후 연결)")]
        [SerializeField] private Hexiege.Infrastructure.NetworkGameManager _networkGameManager;

        [Tooltip("네트워크 게임 시작 흐름 총괄 컴포넌트 (씬에 NetworkGameFlow NetworkObject 배치 후 연결)")]
        [SerializeField] private Hexiege.Infrastructure.NetworkGameFlow _networkGameFlow;

        [Tooltip("네트워크 건물 배치 컨트롤러 (씬에 NetworkBuildingController NetworkObject 배치 후 연결)")]
        [SerializeField] private Hexiege.Infrastructure.NetworkBuildingController _networkBuildingController;

        [Tooltip("네트워크 유닛 생산 컨트롤러 (씬에 NetworkProductionController NetworkObject 배치 후 연결)")]
        [SerializeField] private Hexiege.Infrastructure.NetworkProductionController _networkProductionController;

        [Tooltip("네트워크 유닛 이동 컨트롤러 (씬에 NetworkUnitMovementController NetworkObject 배치 후 연결)")]
        [SerializeField] private Hexiege.Infrastructure.NetworkUnitMovementController _networkUnitMovement;

        [Tooltip("네트워크 전투 컨트롤러 (씬에 NetworkCombatController NetworkObject 배치 후 연결)")]
        [SerializeField] private Hexiege.Infrastructure.NetworkCombatController _networkCombat;

        [Tooltip("네트워크 HP 동기화 컨트롤러 (씬에 NetworkHealthSync NetworkObject 배치 후 연결)")]
        [SerializeField] private Hexiege.Infrastructure.NetworkHealthSync _networkHealthSync;

        [Tooltip("네트워크 승패 판정 동기화 컨트롤러 (씬에 NetworkGameEndController NetworkObject 배치 후 연결)")]
        [SerializeField] private Hexiege.Infrastructure.NetworkGameEndController _networkGameEnd;

        [Tooltip("재접속 대기 + 강제 승리 판정 컨트롤러 (씬에 ReconnectionHandler NetworkObject 배치 후 연결)")]
        [SerializeField] private Hexiege.Infrastructure.ReconnectionHandler _reconnectionHandler;

        [Header("Floating HP Text")]
        [Tooltip("피격 시 남은 HP를 머리 위에 표시하는 스포너 컴포넌트")]
        [SerializeField] private FloatingHpTextSpawner _floatingHpTextSpawner;

        [Tooltip("FloatingHpText 프리팹. TextMeshProUGUI + CanvasGroup 포함.")]
        [SerializeField] private FloatingHpText _floatingHpTextPrefab;

        [Tooltip("부유 텍스트 오브젝트들의 부모 컨테이너 (씬의 FloatingTexts 빈 GameObject)")]
        [SerializeField] private Transform _floatingTextContainer;

        [Header("UI Manager")]
        [Tooltip("게임 UI 생명주기 매니저. 게임 시작/종료 시 등록된 모든 UI에 콜백 호출.")]
        [SerializeField] private GameUIManager _uiManager;

        // [2026-05-11 비활성화 — 슬롯 시스템 폐기]
        // 이동 슬롯 오프셋(TileMoveSlotManager)은 새 이동/전투 규칙(GameSystemRules.md)에 따라
        // 더 이상 사용하지 않으므로 Inspector 노출 SerializeField를 주석 처리합니다.
        // 새 규칙: 유닛은 단일 상태 머신(A* 이동 → 전투 이동 → 공격)을 따르며,
        // 같은 타일에 모이는 유닛을 슬롯으로 분산시키지 않습니다.
        // [Header("이동 슬롯 오프셋 (TileMoveSlotManager)")]
        // [Tooltip("슬롯 1번(앞 중앙)이 타일 중심에서 성 방향으로 떨어지는 비율. 0~0.5 권장. 기본값 0.30.")]
        // [SerializeField] private float _slotForwardRatio = 0.30f;
        //
        // [Tooltip("슬롯 2번(뒤좌)·3번(뒤우)이 타일 중심에서 좌/우로 떨어지는 비율. 0~0.5 권장. 기본값 0.30.")]
        // [SerializeField] private float _slotSideRatio = 0.30f;

        // ====================================================================
        // UseCase 인스턴스 (런타임 생성)
        // ====================================================================

        private HexGrid _grid;

        /// <summary>
        /// 현재 로드된 HexGrid 반환.
        /// NetworkTileSync에서 클라이언트 측 타일 도메인 상태 동기화에 사용.
        /// 맵 로드 전이면 null 반환.
        /// </summary>
        public HexGrid GetGrid() => _grid;

        /// <summary>
        /// 현재 ResourceUseCase 반환.
        /// NetworkResourceSync에서 클라이언트 측 골드 UI 갱신에 사용.
        /// 맵 로드 전이면 null 반환.
        /// </summary>
        public ResourceUseCase GetResource() => _resource;

        /// <summary>
        /// 현재 BuildingPlacementUseCase 반환.
        /// NetworkBuildingController에서 서버 측 건물 배치 실행 및
        /// 클라이언트 측 건물 재생성에 사용.
        /// 맵 로드 전이면 null 반환.
        /// </summary>
        public BuildingPlacementUseCase GetBuildingPlacement() => _buildingPlacement;

        /// <summary>
        /// GameConfig 반환.
        /// NetworkBuildingController에서 서버 측 건물 비용 검증에 사용.
        /// </summary>
        public GameConfig GetConfig() => _config;

        /// <summary>
        /// 현재 UnitProductionUseCase 반환.
        /// NetworkProductionController에서 서버 측 생산 큐 등록에 사용.
        /// 맵 로드 전이면 null 반환.
        /// </summary>
        public UnitProductionUseCase GetUnitProduction() => _unitProduction;

        /// <summary>
        /// 현재 UnitSpawnUseCase 반환.
        /// NetworkProductionController에서 클라이언트 측 유닛 재생성에 사용.
        /// 맵 로드 전이면 null 반환.
        /// </summary>
        public UnitSpawnUseCase GetUnitSpawn() => _unitSpawn;

        /// <summary>
        /// 현재 PopulationUseCase 반환.
        /// NetworkProductionController에서 서버 측 인구 검증에 사용.
        /// 맵 로드 전이면 null 반환.
        /// </summary>
        public PopulationUseCase GetPopulation() => _population;

        /// <summary>
        /// 현재 UnitMovementUseCase 반환.
        /// NetworkUnitMovementController에서 서버 측 경로 계산 및
        /// 클라이언트 예측 이동에 사용.
        /// 맵 로드 전이면 null 반환.
        /// </summary>
        public UnitMovementUseCase GetMovement() => _unitMovement;

        /// <summary>
        /// 현재 UnitCombatUseCase 반환.
        /// NetworkCombatController에서 서버 측 전투 처리에 사용.
        /// 맵 로드 전이면 null 반환.
        /// </summary>
        public UnitCombatUseCase GetCombatUseCase() => _unitCombat;

        /// <summary>
        /// UnitFactory 반환.
        /// NetworkUnitMovementController에서 UnitView 조회(GetUnitObject)에 사용.
        /// </summary>
        public UnitFactory GetUnitFactory() => _unitFactory;

        /// <summary>
        /// GameEndUI 반환.
        /// NetworkGameEndController에서 멀티플레이 결과 표시 및 재시작 동작 교체에 사용.
        /// </summary>
        public GameEndUI GetGameEndUI() => _gameEndUI;

        private GridInteractionUseCase _gridInteraction;
        private UnitMovementUseCase _unitMovement;
        private UnitSpawnUseCase _unitSpawn;
        private UnitCombatUseCase _unitCombat;
        private BuildingPlacementUseCase _buildingPlacement;
        private ResourceUseCase _resource;
        private PopulationUseCase _population;
        private UnitProductionUseCase _unitProduction;
        private GameEndUseCase _gameEnd;
        private IEntityPositionProvider _positionProvider;

        // ────────────────────────────────────────────────────────────────────
        // 패스파인딩 인프라 (2026-04-25 플로우 필드 도입)
        //   _flowFieldService: 목적지별 BFS 결과를 캐싱·관리.
        //                       UnitMovementUseCase 생성 시 주입.
        // ────────────────────────────────────────────────────────────────────
        private FlowFieldService _flowFieldService;

        // [2026-05-11 비활성화 — 슬롯 시스템 폐기]
        // 아래 세 매니저(TileMoveSlotManager / AttackPositionManager / TileOccupancyManager)는
        // 새 이동/전투 규칙(GameSystemRules.md)에 따라 더 이상 사용하지 않습니다.
        // 필드 자체는 주석으로 보존하여 향후 롤백/참고 시 위치를 명확히 합니다.
        //
        // [2026-04-30] 이동/전투 재설계 — 새 이동 슬롯 매니저.
        // private TileMoveSlotManager _moveSlotManager;
        //
        // 근접 유닛 Phase 1 추적 시 타겟 주변 인접 타일을 유닛별로 배정.
        // private AttackPositionManager _attackPositionManager;
        //
        // 타일별 점유량 추적 — 같은 타일에 너무 많은 유닛이 모이는 것을 방지.
        // private TileOccupancyManager _occupancyManager;

        // TileOwnershipService — 매 프레임 모든 유닛의 물리 위치를 확인하여 타일 소유권을 갱신.
        // 유닛 이동 방식(Phase 0 타일 Lerp / Phase 1 월드 좌표 추적 / Phase 2 스냅)과 무관하게
        // "현재 보이는 위치" 기준으로 점령이 반영되도록 한다.
        // 서버(또는 싱글플레이)에서만 Tick을 호출 — 클라이언트는 별도 동기화 경로로 점령 결과 수신.
        private TileOwnershipService _tileOwnership;

        // CastleApproachManager — 적 성 인접 타일별 배정 카운트를 관리.
        // ProductionTicker가 새 유닛을 성으로 이동시킬 때 가장 덜 배정된 인접 타일을 받아
        // 유닛별로 다른 목적지를 사용 → 같은 FlowField 공유로 인한 "줄지어 이동" 현상 방지.
        // CreateUseCases()에서 _grid 초기화 직후에 인스턴스를 만든다.
        private CastleApproachManager _castleApproachManager;

        /// <summary>
        /// FlowFieldService 반환.
        /// 외부에서 캐시 무효화 등을 직접 호출할 필요가 있는 경우(예: 디버깅) 사용.
        /// 일반적인 경우 UnitMovementUseCase 내부에서만 사용된다.
        /// </summary>
        public FlowFieldService GetFlowFieldService() => _flowFieldService;

        // [2026-05-11 비활성화 — 슬롯 시스템 폐기]
        // 슬롯/점유 매니저는 더 이상 생성되지 않으므로 외부 노출 getter는 항상 null을 반환합니다.
        // 외부 호출부(있다면)에서 NullReference가 나지 않도록 시그니처는 유지합니다.

        /// <summary>
        /// [2026-05-11 비활성화] TileMoveSlotManager는 폐기되었습니다. 항상 null 반환.
        /// </summary>
        public TileMoveSlotManager GetTileMoveSlotManager() => null;

        /// <summary>
        /// [2026-05-11 비활성화] AttackPositionManager는 폐기되었습니다. 항상 null 반환.
        /// </summary>
        public AttackPositionManager GetAttackPositionManager() => null;

        /// <summary>
        /// [2026-05-11 비활성화] TileOccupancyManager는 폐기되었습니다. 항상 null 반환.
        /// </summary>
        public TileOccupancyManager GetTileOccupancyManager() => null;

        /// <summary>
        /// StartNetworkGame() 중복 호출 방지 플래그.
        /// NetworkGameFlow가 재스폰될 경우 LoadMap이 재실행되는 것을 막음.
        /// </summary>
        private bool _networkGameStarted = false;

        // ────────────────────────────────────────────────────────────────────
        // [2026-04-30] 새 규칙 4 — 건물 변경 시 즉시 모든 유닛 경로 재계산(eager).
        //
        //   FlowFieldService가 OnBuildingPlaced / OnEntityDied(building)에서 InvalidateAll로
        //   캐시를 비워주지만, 이건 lazy 동작 — 다음 RequestMove 시점에 재계산된다.
        //   새 규칙은 "변경 시점에 모든 살아있는 유닛이 즉시 새 경로로 갱신"을 요구한다.
        //
        //   Application 레이어(UnitMovementUseCase)는 Presentation(UnitView)에 직접 접근할 수
        //   없으므로, composition root(여기 GameBootstrapper)가 이 트리거를 담당한다.
        //
        //   구독은 LoadMap()에서 1회 시작하고 ClearAll()/Dispose 시점에 정리한다.
        // ────────────────────────────────────────────────────────────────────
        private CompositeDisposable _eagerRepathSubscriptions;

        /// <summary>
        /// 네트워크 게임이 이미 시작되었는지 여부.
        /// NetworkGameFlow.OnNetworkSpawn()에서 재스폰 감지용으로 사용.
        /// </summary>
        public bool IsNetworkGameStarted => _networkGameStarted;

        // ====================================================================
        // Update — 싱글플레이 쿨다운 관리
        // ====================================================================

        /// <summary>
        /// 싱글플레이 전용: 매 프레임 모든 유닛의 공격 쿨다운을 감소시킴.
        /// 이전에는 각 UnitView.Update()에서 개별적으로 처리했으나,
        /// 쿨다운 관리를 한 곳에서 통일하기 위해 GameBootstrapper로 이동.
        ///
        /// 멀티플레이에서는 NetworkCombatController.TickCombat()이
        /// 서버 Tick 주기(_attackInterval)로 쿨다운을 감소시키므로,
        /// 이 Update()에서는 호출하지 않음 (이중 감소 방지).
        /// </summary>
        private void Update()
        {
            // 싱글플레이에서만 쿨다운 감소 및 다중 히트 예약 타이머 처리.
            // 멀티플레이에서는 NetworkCombatController가 서버 Tick 기반으로 이 역할을 수행.
            if (!IsNetworkMode() && _unitCombat != null)
            {
                _unitCombat.TickCooldowns(Time.deltaTime);
                // 각 PendingHit의 타이머를 감소시키고 만료된 항목의 데미지를 적용.
                // 다중 히트 유닛(FlameSpirit 6히트, LionKnight 2히트)의 2번째 이후 히트가 여기서 처리됨.
                _unitCombat.TickPendingHits(Time.deltaTime);
            }

            // ────────────────────────────────────────────────────────────────
            // TileOwnershipService: 유닛 물리 위치 기반 타일 소유권 실시간 갱신.
            // 서버(또는 싱글플레이)에서만 실행 — 클라이언트는 동기화로 점령 결과를 받음.
            //
            // 가드 조건:
            //   - 싱글플레이: NetworkContext.IsNetworkActive == false → 통과
            //   - Host/서버: IsNetworkActive == true && IsNetworkServer == true → 통과
            //   - 순수 Client: IsNetworkActive == true && IsNetworkServer == false → 차단
            // ────────────────────────────────────────────────────────────────
            if (_tileOwnership != null &&
                (!NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer))
            {
                _tileOwnership.Tick();
            }
        }

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// 게임 시작 시 기본 맵 로드.
        /// Start()를 사용하는 이유: 다른 컴포넌트의 Awake()가 먼저 실행되도록 보장.
        /// 네트워크 모드(Host/Client)라면 NetworkGameFlow에 맵 로드를 위임하고,
        /// 싱글플레이 모드라면 기존처럼 즉시 로드.
        /// </summary>
        private void Start()
        {
            // ────────────────────────────────────────────────────────────
            // UnitStats / UnitProductionStats를 ScriptableObject 설정값으로 초기화.
            // 이 시점 이후 생성되는 모든 UnitData에 SO 수치가 적용됨.
            // 네트워크 모드/싱글 모드 분기보다 반드시 먼저 실행해야 함 —
            // NetworkGameFlow가 StartNetworkGame()을 늦게 호출하더라도 그 전에
            // 생성되는 유닛이 기본값을 참조하지 않도록.
            // ────────────────────────────────────────────────────────────
            InitializeUnitStatsFromConfig();

            // BuildingStats도 동일 이유로 여기서 초기화.
            // PlaceCastles / PlaceMiningPostDirect 등이 GetMaxHp를 참조하므로
            // 맵 로드 전에 Dictionary가 채워져 있어야 한다.
            InitializeBuildingStatsFromConfig();

            // 네트워크 모드 확인: NetworkManager가 활성화되어 있으면 네트워크 게임
            bool isNetworkMode = IsNetworkMode();

            if (isNetworkMode)
            {
                // 네트워크 모드: NetworkGameFlow가 StartGameClientRpc를 통해
                // StartNetworkGame()을 호출하므로 여기서는 맵 로드를 건너뜀
                Debug.Log("[Network] GameBootstrapper: 네트워크 모드 감지. 맵 로드는 NetworkGameFlow에 위임.");
            }
            else
            {
                // 싱글플레이: 로비에서 선택한 종족을 Blue 팀에 적용.
                // Red 팀(AI)은 RaceId에 정의된 모든 종족 중 무작위로 결정한다.
                // 새 종족이 추가되어도 자동으로 후보에 포함됨.
                // UnitFactory.CreateUnitObject()에서 GameRaceContext를 참조하여
                // 종족에 맞는 프리팹 세트를 선택하므로, LoadMap() 호출 전에 반드시 설정해야 함.
                RaceId[] allRaces = (RaceId[])System.Enum.GetValues(typeof(RaceId));
                RaceId opponentRace = allRaces[UnityEngine.Random.Range(0, allRaces.Length)];
                GameRaceContext.Set(LocalPlayerRace.Current, opponentRace);

                // 싱글플레이 모드: 기존 로직 그대로 실행
                LoadMap(HexOrientation.FlatTop);
            }
        }

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
                    HitFrameTimes = entry.hitFrameTimes,
                    // OccupancySize: 타일 점유 크기 (소형 1 / 중형 2 / 대형 3).
                    // SO에서 0으로 비어 있으면 UnitStats.GetOccupancySize()에서 1f로 폴백.
                    OccupancySize = entry.occupancySize,
                    // Kind: 근접/원거리 분기.
                    // 새 이동/전투 규칙(GameSystemRules.md 13/15)에서
                    //  "감지 시 직선 추적" vs "사거리 진입 시 즉시 공격"을 결정.
                    // SO Inspector에서 명시적으로 선택해야 하며, 기본값(enum 0=Melee)이
                    // 실제 의도와 다를 수 있으므로 Config 항목별로 반드시 확인할 것.
                    Kind = entry.attackKind
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
                // Human
                dict[(entry.buildingType, RaceId.Human)] = new BuildingStats.StatValues
                {
                    MaxHp = entry.humanMaxHp,
                    GoldCost = entry.humanGoldCost,
                    AttackPower = entry.humanAttackPower
                };

                // Spirit
                dict[(entry.buildingType, RaceId.Spirit)] = new BuildingStats.StatValues
                {
                    MaxHp = entry.spiritMaxHp,
                    GoldCost = entry.spiritGoldCost,
                    AttackPower = entry.spiritAttackPower
                };

                // Transcendence
                dict[(entry.buildingType, RaceId.Transcendence)] = new BuildingStats.StatValues
                {
                    MaxHp = entry.transcendenceMaxHp,
                    GoldCost = entry.transcendenceGoldCost,
                    AttackPower = entry.transcendenceAttackPower
                };
            }

            BuildingStats.Initialize(dict);

            Debug.Log($"[GameBootstrapper] BuildingStats 초기화 완료. " +
                      $"등록된 (건물×종족) 엔트리 수: {dict.Count}");
        }

        // ====================================================================
        // 런타임 맵 로드/전환
        // ====================================================================

        /// <summary>
        /// 런타임에서 맵을 로드/전환.
        /// orientation에 따라 전체 시스템 재초기화.
        /// 외부에서 호출하여 PointyTop ↔ FlatTop 전환 가능.
        /// </summary>
        public void LoadMap(HexOrientation orientation)
        {
            // UI 매니저에 모든 게임 UI 등록 + 이벤트 구독 초기화.
            // 중복 등록 방지는 GameUIManager.Register() 내부에서 처리하므로 매번 호출해도 안전.
            // Initialize()는 기존 구독을 Dispose 후 재구독하므로 중복 구독도 방지됨.
            if (_uiManager != null)
            {
                _uiManager.Register(_gameHudUI);
                _uiManager.Register(_productionUI);
                _uiManager.Register(_buildingUI);
                _uiManager.Register(_gameEndUI);
                _uiManager.Initialize();
            }

            // 게임 오버 상태에서 재시작 시 시간 복원
            Time.timeScale = 1f;

            bool isNetworkMode = IsNetworkMode();

            if (_config == null)
            {
                Debug.LogError("[GameBootstrapper] GameConfig가 설정되지 않았습니다.");
                return;
            }

            OrientationConfig oc = (orientation == HexOrientation.FlatTop)
                ? _config.FlatTop : _config.PointyTop;

            // 1. 기존 유닛/건물 제거
            ClearAll();

            // 2. 설정 적용
            ApplyConfig(orientation, oc);

            // 싱글플레이: LocalPlayerTeam 기반으로 ViewConverter 초기화.
            // ApplyConfig() 이후에 호출해야 HexMetrics가 준비되어 GridCenter 계산이 정확함.
            // 멀티플레이는 StartNetworkGame()에서 LoadMap() 전에 이미 설정하므로 여기서는 건너뜀.
            if (!isNetworkMode)
            {
                Vector3 mapCenter = HexMetrics.GridCenter(oc.GridWidth, oc.GridHeight);
                bool isRed = (LocalPlayerTeam.Current == TeamId.Red);
                ViewConverter.Setup(isRed, mapCenter);
            }

            // 3. 그리드 생성
            _grid = new HexGrid(oc.GridWidth, oc.GridHeight, orientation);

            // 4. UseCase 생성
            CreateUseCases();

            // 5. 타일 렌더링
            if (_gridRenderer != null)
                _gridRenderer.RenderGrid(_grid);

            // 6. 카메라 설정
            SetupCamera(orientation, oc);

            // 7. 입력 연결
            SetupInput();

            // 8. 건물 시스템 초기화
            SetupBuildings();

            // 9. 생산 시스템 초기화
            SetupProduction();

            // 10. HUD 초기화
            if (_gameHudUI != null)
                _gameHudUI.Initialize(_resource, _population);

            // 10-1. 게임 종료 UI 초기화
            if (_gameEndUI != null)
                _gameEndUI.Initialize();

            // 10-2. 부유 HP 텍스트 스포너 초기화
            // OnEntityDamaged 이벤트 구독 → 피격 시 남은 HP를 머리 위에 표시
            if (_floatingHpTextSpawner != null)
                _floatingHpTextSpawner.Initialize(_positionProvider, _floatingTextContainer, _floatingHpTextPrefab);

            // 11. Castle 자동 배치
            PlaceCastles(orientation, oc);

            // 12. 금광 배치
            PlaceGoldMines(orientation, oc);

            // 13. 금광 렌더링
            if (_gridRenderer != null)
                _gridRenderer.RenderGoldMines(_grid);

            // 14. 게임 시작 이벤트 발행 — 모든 UI에 초기화 완료 알림.
            // 맵 로드의 맨 마지막에 발행하여, 모든 시스템이 준비된 상태에서
            // UI가 OnGameStarted() 콜백을 안전하게 처리할 수 있도록 보장.
            GameEvents.OnGameStarted.OnNext(Unit.Default);

            // 15. [2026-04-30] 새 규칙 4 — 건물 변경 시 모든 유닛 경로 즉시 재계산.
            //     이 시점이면 _unitFactory / _unitMovement / _flowFieldService 모두 준비됨.
            SetupEagerRepathOnBuildingChanges();

        }

        // ====================================================================
        // [2026-04-30] 새 규칙 4 — eager 경로 재계산 트리거 설정
        // ====================================================================

        /// <summary>
        /// 건물 배치 / 건물 사망 이벤트를 구독하여
        /// 살아있는 모든 유닛에게 OnPathInvalidated() 호출 → 동일 destination으로
        /// 새 경로를 받아 이동을 즉시 재시작시킨다.
        ///
        /// 구독 해제는 ClearAll() 또는 OnDestroy()에서 처리.
        /// 멀티플레이에서는 서버에서만 트리거되어야 하므로 가드 적용.
        /// </summary>
        private void SetupEagerRepathOnBuildingChanges()
        {
            // 기존 구독 정리 (재경기/맵 전환 시 중복 구독 방지).
            _eagerRepathSubscriptions?.Dispose();
            _eagerRepathSubscriptions = new CompositeDisposable();

            // 건물 배치: walkable이 false로 바뀌어 기존 경로가 부정확해질 수 있다.
            GameEvents.OnBuildingPlaced
                .Subscribe(_ => RepathAllAliveUnits())
                .AddTo(_eagerRepathSubscriptions);

            // 엔티티 사망: 건물 사망만 walkable에 영향 → 그때만 재계산.
            GameEvents.OnEntityDied
                .Subscribe(e =>
                {
                    if (e.Entity is BuildingData) RepathAllAliveUnits();
                })
                .AddTo(_eagerRepathSubscriptions);
        }

        /// <summary>
        /// 모든 살아있는 유닛에 대해 OnPathInvalidated()를 호출.
        /// 서버(또는 싱글플레이)에서만 동작 — 클라이언트는 NetworkTransform이 결과를 동기화한다.
        /// </summary>
        private void RepathAllAliveUnits()
        {
            // 서버 가드 — 싱글(IsNetworkActive==false) 또는 Host(IsNetworkServer==true) 통과.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;
            if (_unitSpawn == null || _unitFactory == null) return;

            // _unitSpawn.Units는 IReadOnlyDictionary<int, UnitData>.
            // 살아있는 유닛만 처리 (사망한 유닛은 OnEntityDied에서 OnUnitRemoved가 호출됨).
            foreach (var kv in _unitSpawn.Units)
            {
                UnitData unit = kv.Value;
                if (unit == null || !unit.IsAlive) continue;

                GameObject obj = _unitFactory.GetUnitObject(unit.Id);
                if (obj == null) continue;

                Hexiege.Presentation.UnitView view = obj.GetComponent<Hexiege.Presentation.UnitView>();
                if (view == null) continue;

                // UnitView 내부에서 destination 보유 여부와 NetworkContext 가드를 다시 한 번 체크.
                view.OnPathInvalidated();
            }
        }

        // ====================================================================
        // 네트워크 게임 진입점
        // ====================================================================

        /// <summary>
        /// 네트워크 모드에서 게임을 시작. NetworkGameFlow.StartGameClientRpc()가 호출.
        /// FlatTop 맵을 로드하고, 로컬 플레이어 팀에 맞춰 ViewConverter를 설정.
        /// Blue 팀(Host) → 기본 뷰, Red 팀(Client) → 맵 중심 기준 반전 뷰.
        /// </summary>
        /// <param name="localTeam">이 클라이언트에 할당된 팀.</param>
        public void StartNetworkGame(TeamId localTeam)
        {
            // NetworkGameFlow가 재스폰되어 중복 호출되는 경우 방지
            if (_networkGameStarted)
            {
                Debug.LogWarning("[Network] StartNetworkGame: 이미 시작됨. 중복 호출 무시.");
                return;
            }
            _networkGameStarted = true;

            Debug.Log($"[Network] StartNetworkGame 호출. 로컬 팀={localTeam}");

            // ViewConverter를 LoadMap() 이전에 설정.
            // LoadMap() 내부에서 PlaceCastles(), RenderGrid() 등이 ViewConverter.ToView()를 호출하므로
            // 반드시 먼저 초기화해야 Red팀 건물/타일이 올바른 반전 위치에 렌더링됨.
            //
            // 주의: HexMetrics.GridCenter()는 HexMetrics.Orientation / TileWidth / TileHeight를
            // 참조하므로, GridCenter() 호출 전에 반드시 FlatTop 설정을 HexMetrics에 적용해야 함.
            // ApplyConfig()는 LoadMap() 내부에서 실행되므로, 여기서 먼저 적용.
            if (_config != null)
            {
                OrientationConfig oc = _config.FlatTop;

                // GridCenter() 호출 전에 HexMetrics를 FlatTop 기준으로 사전 설정.
                // 이렇게 하지 않으면 기본값(PointyTop)으로 중심 좌표가 계산되어
                // Red팀 반전 위치가 한 칸 이상 틀어지는 버그가 발생.
                HexMetrics.Orientation = HexOrientation.FlatTop;
                HexOrientationContext.Current = HexOrientation.FlatTop;
                HexMetrics.TileWidth = oc.TileWidth;
                HexMetrics.TileHeight = oc.TileHeight;

                Vector3 mapCenter = HexMetrics.GridCenter(oc.GridWidth, oc.GridHeight);
                bool isRed = (localTeam == TeamId.Red);
                ViewConverter.Setup(isRed, mapCenter);
                Debug.Log($"[Network] ViewConverter 사전 설정 완료. isRed={isRed}, mapCenter={mapCenter}");
            }

            // 맵 로드 (FlatTop 고정) — ViewConverter 설정 완료 후 실행
            // LoadMap() 내부의 SetupCamera()가 카메라를 맵 중심에 배치함.
            // 싱글플레이와 동일하게 맵 중심에서 시작하도록 팀별 카메라 이동은 수행하지 않음.
            LoadMap(HexOrientation.FlatTop);
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
        /// </summary>
        private void CreateUseCases()
        {
            _gridInteraction = new GridInteractionUseCase(_grid);
            _unitSpawn = new UnitSpawnUseCase(_grid);

            // 플로우 필드 서비스 — 그리드 참조 저장 + walkable 변경 이벤트 구독.
            // UnitMovementUseCase 생성 전에 만들어야 주입이 가능하다.
            // 재경기/맵 전환 시 같은 인스턴스라면 Initialize 내부에서 기존 구독을 정리한다.
            if (_flowFieldService == null)
                _flowFieldService = new FlowFieldService();
            _flowFieldService.Initialize(_grid);

            // ────────────────────────────────────────────────────────────
            // [2026-05-11 비활성화 — 슬롯 시스템 폐기]
            // 새 이동/전투 규칙(GameSystemRules.md)에 따라 슬롯/점유 매니저는 더 이상
            // 생성하지 않습니다. UnitView의 새 상태 머신은 슬롯/점유 분기 없이
            // A* 이동 → 전투 이동 → 공격의 단일 흐름으로 동작합니다.
            //
            // 기존 코드(롤백 참고용):
            //   if (_moveSlotManager == null)
            //       _moveSlotManager = new TileMoveSlotManager(_slotForwardRatio, _slotSideRatio);
            //   _moveSlotManager.Clear();
            //
            //   if (_attackPositionManager == null)
            //       _attackPositionManager = new AttackPositionManager();
            //   _attackPositionManager.Clear();
            //
            //   if (_occupancyManager == null)
            //       _occupancyManager = new TileOccupancyManager();
            //   _occupancyManager.Clear();
            // ────────────────────────────────────────────────────────────

            // 이전 UseCase의 이벤트 구독을 정리한 뒤 새로 생성 (재경기 시 중복 구독 방지).
            // [2026-05-11 비활성화] occupancyManager 인자는 null을 전달 — UnitMovementUseCase 내부에서
            // null이면 점유 체크가 모두 무력화되어 새 규칙과 동일한 동작이 됩니다.
            _unitMovement?.Dispose();
            _unitMovement = new UnitMovementUseCase(_grid, _unitSpawn, _flowFieldService, null);
            _buildingPlacement = new BuildingPlacementUseCase(_grid);
            _positionProvider = new UnitWorldPositionProvider(_unitFactory, _buildingFactory);
            _unitCombat = new UnitCombatUseCase(_grid, _unitSpawn, _buildingPlacement, _positionProvider);

            // TileOwnershipService 초기화.
            // _grid, _unitSpawn, _positionProvider가 모두 준비된 직후에 생성한다.
            // 매 프레임 GameBootstrapper.Update()에서 Tick()이 호출되며,
            // 유닛 이동 방식(Phase 0/1/2)에 무관하게 시각 위치를 기준으로 타일을 점령한다.
            _tileOwnership = new TileOwnershipService(_grid, _unitSpawn, _positionProvider);

            // CastleApproachManager 초기화.
            // 재경기/맵 전환 시 같은 인스턴스를 재사용하지 않고 새로 만든다.
            // 이렇게 하면 이전 게임의 _assignedCounts/_unitAssignments가 자동으로 사라진다.
            // (만약 추후 인스턴스 재사용이 필요해지면 여기서 Clear()를 호출하는 방식으로 바꾸면 된다.)
            _castleApproachManager = new CastleApproachManager(_grid);

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
                _inputHandler.Initialize(
                    _gridInteraction, _mainCamera,
                    _buildingPlacement, _buildingUI, _productionUI);
            }
        }

        // ====================================================================
        // 유닛 관리
        // ====================================================================

        /// <summary>
        /// 기존 유닛/건물 전체 제거. 맵 전환 시 호출.
        /// </summary>
        private void ClearAll()
        {
            if (_unitFactory != null)
                _unitFactory.DestroyAllUnits();

            if (_buildingFactory != null)
                _buildingFactory.DestroyAllBuildings();

            _buildingPlacement?.Clear();

            // 성 접근 타일 배정 상태도 초기화.
            // CreateUseCases()에서 어차피 새 인스턴스를 만들지만, ClearAll 직후
            // 외부에서 매니저를 참조하는 일이 생기더라도 깨끗한 상태를 보장한다.
            _castleApproachManager?.Clear();

            // 이전 게임 종료 UseCase 정리
            _gameEnd?.Dispose();
            _gameEnd = null;

            // 게임 종료 UI 숨김
            if (_gameEndUI != null)
                _gameEndUI.Hide();

            // [2026-04-30] eager 재경로 트리거 구독 정리. 다음 LoadMap에서 다시 구독한다.
            _eagerRepathSubscriptions?.Dispose();
            _eagerRepathSubscriptions = null;
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

            // 생산 티커 초기화 (ProductionPanelUI보다 먼저 — UI에서 마커 참조 필요)
            // CastleApproachManager를 함께 주입하여 유닛이 성 인접 타일을 분산 배정받도록 한다.
            if (_productionTicker != null)
                _productionTicker.Initialize(
                    _unitProduction, _resource, _unitMovement,
                    _buildingPlacement, _unitFactory, _config,
                    _castleApproachManager);

            // 네트워크 모드 여부에 따라 NetworkProductionController 주입 (싱글플레이 시 null)
            bool isNetworkMode = IsNetworkMode();

            Hexiege.Infrastructure.NetworkProductionController productionController =
                isNetworkMode ? _networkProductionController : null;

            // 생산 패널 UI 초기화 (네트워크 컨트롤러 포함)
            if (_productionUI != null)
                _productionUI.Initialize(_unitProduction, _resource, _population, _productionTicker, productionController);
            }

        /// <summary>
        /// 양 팀 Castle 자동 배치. 게임 시작 시 호출.
        /// Blue: 맵 하단 중앙, Red: 맵 상단 중앙.
        /// </summary>
        private void PlaceCastles(HexOrientation orientation, OrientationConfig oc)
        {
            if (_buildingPlacement == null) return;

            // Blue Castle: 하단 중앙
            // 종족에 따라 Castle HP가 다르므로 GameRaceContext에서 종족을 조회하여 전달
            HexCoord bluePos = HexGrid.OffsetToCube(
                oc.GridWidth / 2, oc.GridHeight - 2, orientation);
            _buildingPlacement.PlaceBuilding(BuildingType.Castle, TeamId.Blue, bluePos,
                GameRaceContext.BlueRace);

            // Red Castle: 상단 중앙
            HexCoord redPos = HexGrid.OffsetToCube(
                oc.GridWidth / 2, 1, orientation);
            _buildingPlacement.PlaceBuilding(BuildingType.Castle, TeamId.Red, redPos,
                GameRaceContext.RedRace);
        }

        /// <summary>
        /// 맵에 금광 배치 + 시작 채굴소 건설.
        /// 금광은 중립 오브젝트: IsWalkable=false, Owner=Neutral.
        /// 각 팀 Castle 횡 2칸 위치에 금광+채굴소 자동 건설.
        /// 맵 중앙에 중립 금광 2개 배치.
        /// </summary>
        private void PlaceGoldMines(HexOrientation orientation, OrientationConfig oc)
        {
            if (_grid == null) return;

            int centerCol = oc.GridWidth / 2; // 5
            int blueRow = oc.GridHeight - 2;  // 27 (Blue Castle row)
            int redRow = 1;                    // Red Castle row
            int midRow = oc.GridHeight / 2;   // 14 (중앙)

            // 시작 금광 (각 팀 Castle 횡 2칸, 채굴소 자동 건설)
            int[][] startingMines = new int[][]
            {
                new int[] { centerCol - 2, blueRow }, // Blue 시작 금광
                new int[] { centerCol - 2, redRow },  // Red 시작 금광
            };

            // 중립 금광 (맵 중앙 부근 2개)
            int[][] neutralMines = new int[][]
            {
                new int[] { 3, midRow },
                new int[] { 7, midRow },
            };

            // 모든 금광 타일 설정 (HasGoldMine + IsWalkable=false)
            void SetGoldMine(int col, int row)
            {
                HexCoord coord = HexGrid.OffsetToCube(col, row, orientation);
                HexTile tile = _grid.GetTile(coord);
                if (tile != null)
                {
                    tile.HasGoldMine = true;
                    tile.IsWalkable = false;
                }
            }

            foreach (var m in startingMines) SetGoldMine(m[0], m[1]);
            foreach (var m in neutralMines) SetGoldMine(m[0], m[1]);

            // 시작 채굴소 자동 건설 (금광 타일 위에 직접 배치)
            if (_buildingPlacement != null)
            {
                // Blue 시작 채굴소
                // 종족에 따라 MiningPost HP가 다르므로 GameRaceContext에서 종족을 조회하여 전달
                HexCoord blueMinePos = HexGrid.OffsetToCube(
                    startingMines[0][0], startingMines[0][1], orientation);
                _buildingPlacement.PlaceMiningPostDirect(TeamId.Blue, blueMinePos,
                    GameRaceContext.BlueRace);

                // Red 시작 채굴소
                HexCoord redMinePos = HexGrid.OffsetToCube(
                    startingMines[1][0], startingMines[1][1], orientation);
                _buildingPlacement.PlaceMiningPostDirect(TeamId.Red, redMinePos,
                    GameRaceContext.RedRace);
            }
        }

        // ====================================================================
        // 유틸리티
        // ====================================================================

        /// <summary>
        /// 현재 네트워크 모드(멀티플레이)로 실행 중인지 확인합니다.
        /// Host 또는 Client로 연결된 경우 true를 반환합니다.
        /// </summary>
        private bool IsNetworkMode()
        {
            return NetworkManager.Singleton != null &&
                (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient);
        }

    }
}
