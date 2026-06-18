// ============================================================================
// GameBootstrapper.cs (메인 파일)
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
//
// ── partial class 분할 안내 ─────────────────────────────────────────
// 본 클래스는 1300줄 규모로 커져 가독성을 위해 4개의 partial 파일로 분리되어 있다.
//   - GameBootstrapper.cs        : 본 파일. [SerializeField] 필드 / 런타임 필드 /
//                                   Unity 생명주기(Start, Update) / Getter / 내부 헬퍼
//   - GameBootstrapper.Setup.cs  : 초기화 헬퍼(스탯 주입, UseCase 생성, 카메라 설정,
//                                   입력 연결, 건물/생산 시스템 초기화)
//   - GameBootstrapper.Map.cs    : 맵 로드/전환(LoadMap, ClearAll, Castle/GoldMine 배치)
//   - GameBootstrapper.Network.cs: 네트워크 게임 진입(StartNetworkGame),
//                                   eager 경로 재계산 트리거
//
// 규칙(반드시 지켜야 함):
//   * [SerializeField] 필드는 본 파일에만 추가한다 — Inspector 항목 위치 추적을 위해.
//   * Unity 생명주기 메서드(Start/Update/OnDestroy 등)는 본 파일에만 둔다 — 중복 정의 방지.
//   * partial 파일들은 private 헬퍼만 담는다.
// ============================================================================

// [2026-05-20] using Unity.Netcode 제거 — NetworkManager 직접 호출은 NetworkContext.IsNetworkActive로 단일화됨.
//   Inspector SerializeField로 노출된 NetworkBehaviour 타입(NetworkGameManager 등)은
//   Hexiege.Infrastructure 네임스페이스에서 import되므로 Unity.Netcode를 직접 import할 필요가 없다.
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
    public partial class GameBootstrapper : MonoBehaviour
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

        // [AIConfig 이전] _enableAI는 AIConfig.enableAI 필드로 이전되었다.
        //   이제 AI On/Off는 Resources/Config/AIConfig.asset의 enableAI 값으로 결정한다.
        //   (Project 창에서 씬을 열지 않고도 토글 가능 → 테스트 편의성)
        //   검증 안전을 위해 삭제 대신 주석 처리. 사용자 테스트 통과 후 제거 예정.
        // [Header("AI 설정")]
        // [Tooltip("AI 활성화 여부. false로 끄면 싱글플레이에서도 AI가 동작하지 않는다. (테스트용 토글)")]
        // [SerializeField] private bool _enableAI = true;

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

        [Tooltip("비생산 건물 공용 액션 패널 UI (MiningPost / Tower / 특수건물 클릭 시 표시).")]
        [SerializeField] private BuildingActionPanelUI _buildingActionPanelUI;

        [Tooltip("생산 티커")]
        [SerializeField] private ProductionTicker _productionTicker;

        [Tooltip("메인 카메라")]
        [SerializeField] private Camera _mainCamera;

        [Tooltip("게임 종료 UI")]
        [SerializeField] private GameEndUI _gameEndUI;

        [Tooltip("골드/인구 HUD")]
        [SerializeField] private GameHudUI _gameHudUI;

        [Header("Network")]
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

        [Header("Effect Manager")]
        [Tooltip("VFX/SFX를 통합 관리하는 이펙트 매니저. 씬에 배치된 EffectManager 오브젝트 연결.")]
        [SerializeField] private EffectManager _effectManager;

        [Tooltip("유닛 타입별 공격/사망 이펙트 설정 ScriptableObject")]
        [SerializeField] private UnitEffectConfig _unitEffectConfig;

        [Tooltip("건물 타입별 파괴/업그레이드 이펙트 설정 ScriptableObject")]
        [SerializeField] private BuildingEffectConfig _buildingEffectConfig;

        [Tooltip("UI 이펙트 설정 ScriptableObject")]
        [SerializeField] private UiEffectConfig _uiEffectConfig;

        [Header("UI Manager")]
        [Tooltip("게임 UI 생명주기 매니저. 게임 시작/종료 시 등록된 모든 UI에 콜백 호출.")]
        [SerializeField] private GameUIManager _uiManager;

        [Header("인게임 설정 메뉴")]
        [Tooltip("인게임 설정 메뉴 팝업 (사운드/포기 버튼).")]
        [SerializeField] private InGameSettingsUI _inGameSettingsUI;

        // [2026-06-18] _confirmPopup SerializeField 제거.
        //   기존: GameBootstrapper가 ConfirmPopup을 직접 들고 있었으나 어디에도 주입되지 않는 死(dead) 참조였다.
        //   확인 팝업이 필요한 View(InGameSettingsUI)는 자체 _confirmPopup을 보유하고 있으며,
        //   전역 확인 팝업이 필요한 경우 UIManager.Instance(IUIManager)를 통해 호출한다.
        //   (InGameSettingsUI.Initialize는 IUIManager를 받지 않으므로 파라미터를 추가하지 않는다.)

        // ====================================================================
        // UseCase 인스턴스 (런타임 생성)
        // ====================================================================

        private HexGrid _grid;

        private GridInteractionUseCase _gridInteraction;
        private UnitMovementUseCase _unitMovement;
        private UnitSpawnUseCase _unitSpawn;
        private UnitCombatUseCase _unitCombat;
        private TowerCombatUseCase _towerCombat;
        private BuildingPlacementUseCase _buildingPlacement;
        private ResourceUseCase _resource;
        private PopulationUseCase _population;
        private UnitProductionUseCase _unitProduction;
        private GameEndUseCase _gameEnd;
        private IEntityPositionProvider _positionProvider;

        // [AI 시스템] 싱글플레이 AI 컨트롤러. InitializeAI()에서 생성, 매 프레임 Update()에서 Tick.
        // 싱글플레이 + AIConfig.enableAI=true일 때만 생성된다. 멀티플레이에서는 항상 null.
        private AIOpponentController _aiController;

        // ────────────────────────────────────────────────────────────────────
        // 패스파인딩 인프라 (2026-04-25 플로우 필드 도입)
        //   _flowFieldService: 목적지별 BFS 결과를 캐싱·관리.
        //                       UnitMovementUseCase 생성 시 주입.
        // ────────────────────────────────────────────────────────────────────
        private FlowFieldService _flowFieldService;

        // TileOwnershipService — 매 프레임 모든 유닛의 물리 위치를 확인하여 타일 소유권을 갱신.
        // 유닛 이동 방식(Phase 0 타일 Lerp / Phase 1 월드 좌표 추적 / Phase 2 스냅)과 무관하게
        // "현재 보이는 위치" 기준으로 점령이 반영되도록 한다.
        // 서버(또는 싱글플레이)에서만 Tick을 호출 — 클라이언트는 별도 동기화 경로로 점령 결과 수신.
        private TileOwnershipService _tileOwnership;

        // ────────────────────────────────────────────────────────────────────
        // [2026-05-15] 혼잡도 기반 분산 시스템 (v2) — 핵심 인스턴스.
        //
        //   _congestionMap          — 타일별 혼잡도 누적/감쇠 데이터.
        //   _congestionPathfinder   — 혼잡도 가중 A* 탐색기.
        //   _congestionSubs         — OnUnitEnteredTile 구독 해제용. ClearAll에서 정리.
        //
        // 튜닝 값(DecayInterval / CongestionWeight)은 별도 ScriptableObject 대신
        // GameConfig(_config)에 통합되어 있다. ProductionTicker가 _config를 통해 직접 읽는다.
        //
        // 생성/소유 정책:
        //   _congestionMap / _congestionPathfinder는 매 LoadMap()마다 새로 만들어 이전 게임의 잔여 혼잡도가
        //   다음 게임으로 새지 않도록 보장한다.
        // ────────────────────────────────────────────────────────────────────
        private CongestionMap _congestionMap;
        private CongestionAwarePathfinder _congestionPathfinder;
        private System.IDisposable _congestionSub;

        /// <summary>
        /// StartNetworkGame() 중복 호출 방지 플래그.
        /// NetworkGameFlow가 재스폰될 경우 LoadMap이 재실행되는 것을 막음.
        /// </summary>
        private bool _networkGameStarted = false;

        // ────────────────────────────────────────────────────────────────────
        // [2026-04-30] 새 규칙 4 — 건물 변경 시 즉시 모든 유닛 경로 재계산(eager).
        //
        //   FlowFieldService가 OnBuildingPlaced / OnBuildingDied 에서 InvalidateAll로
        //   캐시를 비워주지만, 이건 lazy 동작 — 다음 RequestMove 시점에 재계산된다.
        //   새 규칙은 "변경 시점에 모든 살아있는 유닛이 즉시 새 경로로 갱신"을 요구한다.
        //
        //   Application 레이어(UnitMovementUseCase)는 Presentation(UnitView)에 직접 접근할 수
        //   없으므로, composition root(여기 GameBootstrapper)가 이 트리거를 담당한다.
        //
        //   구독은 LoadMap()에서 1회 시작하고 ClearAll()/Dispose 시점에 정리한다.
        // ────────────────────────────────────────────────────────────────────
        private CompositeDisposable _eagerRepathSubscriptions;

        // ====================================================================
        // Getter 메서드 — 외부 NetworkBehaviour 등에서 UseCase / 상태 접근용
        // ====================================================================

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
        /// 현재 TowerCombatUseCase 반환.
        /// NetworkCombatController에서 서버 측 방어 타워 공격 처리에 사용.
        /// 맵 로드 전이면 null 반환.
        /// </summary>
        public TowerCombatUseCase GetTowerCombat() => _towerCombat;

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

        /// <summary>
        /// FlowFieldService 반환.
        /// 외부에서 캐시 무효화 등을 직접 호출할 필요가 있는 경우(예: 디버깅) 사용.
        /// 일반적인 경우 UnitMovementUseCase 내부에서만 사용된다.
        /// </summary>
        public FlowFieldService GetFlowFieldService() => _flowFieldService;

        /// <summary>
        /// 네트워크 게임이 이미 시작되었는지 여부.
        /// NetworkGameFlow.OnNetworkSpawn()에서 재스폰 감지용으로 사용.
        /// </summary>
        public bool IsNetworkGameStarted => _networkGameStarted;

        // ====================================================================
        // Unity 생명주기 — Start / Update
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
            // 방어 타워 전투: 싱글플레이에서만 여기서 Tick.
            // 멀티플레이에서는 NetworkCombatController가 서버 Tick에서 호출하므로
            // 여기서 호출하면 이중 데미지가 발생한다 → IsNetworkMode 가드로 차단.
            // (TowerCombatUseCase.Tick 내부에도 클라이언트 차단 가드가 있지만,
            //  Host에서 이중 호출되는 것을 막기 위해 싱글에서만 호출한다.)
            // ────────────────────────────────────────────────────────────────
            if (!IsNetworkMode() && _towerCombat != null)
            {
                _towerCombat.Tick(Time.deltaTime);
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

            // ────────────────────────────────────────────────────────────────
            // [AI 시스템] 싱글플레이 AI 컨트롤러 구동.
            // _aiController는 싱글플레이 + AIConfig.enableAI일 때만 생성되므로(InitializeAI),
            // 여기서는 null 체크만으로 충분하다 (멀티플레이에서는 항상 null).
            // ────────────────────────────────────────────────────────────────
            _aiController?.Tick(Time.deltaTime);
        }

        // ====================================================================
        // 유틸리티
        // ====================================================================

        /// <summary>
        /// 현재 네트워크 모드(멀티플레이)로 실행 중인지 확인합니다.
        /// Host 또는 Client로 연결된 경우 true를 반환합니다.
        ///
        /// [2026-05-20] NetworkManager.Singleton 직접 호출 → NetworkContext.IsNetworkActive로 단일화.
        /// Update 매 프레임에서 호출되는 경로(라인 345)의 비용도 줄어든다.
        /// </summary>
        private bool IsNetworkMode()
        {
            return NetworkContext.IsNetworkActive;
        }

        // [2026-05-20] ActionDisposable 내부 클래스 제거.
        // OnUnitEnteredTile이 Action → Subject로 통일되면서 UniRx의 IDisposable이 직접 반환되어,
        // 별도의 IDisposable 래퍼 래핑이 불필요해졌다.
    }
}
