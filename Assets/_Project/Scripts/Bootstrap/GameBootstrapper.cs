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
    // IGameServices를 구현하여 GameServicesLocator에 등록한다.
    // Infrastructure/Network 파일들이 Bootstrap에 직접 의존하는 대신
    // GameServicesLocator.Current(IGameServices)를 통해 접근하도록 한다.
    public partial class GameBootstrapper : MonoBehaviour, IGameServices
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

        [Tooltip("특수 공격(도끼병 휩쓸기 등) 튜닝값 ScriptableObject. 미연결 시 코드 기본값(1.0/120) 사용.")]
        [SerializeField] private SpecialAttackConfig _specialAttackConfig;

        [Tooltip("스킬 건물 종족별 로드아웃(슬롯 1~5) ScriptableObject. 미연결 시 스킬 발동/패널이 비활성(빈 로드아웃).")]
        [SerializeField] private SkillLoadoutConfig _skillLoadoutConfig;

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

        [Tooltip("연구소(Research) 강화 패널 UI (연구소 클릭 시 표시). 프리팹/씬 배선은 사용자 Unity 작업.")]
        [SerializeField] private ResearchPanelUI _researchPanelUI;

        [Tooltip("스킬 건물(FlightFacility/MagicBuilding) 전용 스킬 패널 UI. 프리팹/씬 배선은 사용자 Unity 작업.")]
        [SerializeField] private BuildingSkillPanelUI _buildingSkillPanelUI;

        [Tooltip("스킬 지점 조준 컨트롤러(press→드래그→엣지스크롤→release). 프리팹/씬 배선은 사용자 Unity 작업.")]
        [SerializeField] private SkillAimController _skillAimController;

        [Tooltip("MistShrine(HealShrine) 전용 물안개 힐 패널 UI. 프리팹/씬 배선은 에디터 셋업 스크립트가 처리.")]
        [SerializeField] private MistShrinePanelUI _mistShrinePanelUI;

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

        [Tooltip("네트워크 연구소 강화 컨트롤러 (씬에 NetworkUpgradeController NetworkObject 배치 후 연결)")]
        [SerializeField] private Hexiege.Infrastructure.NetworkUpgradeController _networkUpgradeController;

        [Tooltip("네트워크 스킬 발동 컨트롤러 (씬에 NetworkSkillController NetworkObject 배치 후 연결)")]
        [SerializeField] private Hexiege.Infrastructure.NetworkSkillController _networkSkillController;

        [Tooltip("네트워크 MistShrine 물안개 힐 컨트롤러 (씬에 NetworkMistShrineController NetworkObject 배치 후 연결)")]
        [SerializeField] private Hexiege.Infrastructure.NetworkMistShrineController _networkMistShrineController;

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

        // ====================================================================
        // UseCase 인스턴스 (런타임 생성)
        // ====================================================================

        private HexGrid _grid;

        private GridInteractionUseCase _gridInteraction;
        private UnitMovementUseCase _unitMovement;
        private UnitSpawnUseCase _unitSpawn;
        private UnitCombatUseCase _unitCombat;
        private TowerCombatUseCase _towerCombat;
        private UnitUpgradeUseCase _unitUpgrade;
        private SkillActivationUseCase _skillActivation;
        private StatusEffectSystem _statusEffectSystem;
        // [MistShrine] 물안개 힐(시전·물안개 수명·회복·쿨다운·자동 모드). 서버 권위 UseCase.
        private MistShrineUseCase _mistShrine;

        private BuildingPlacementUseCase _buildingPlacement;
        private ResourceUseCase _resource;
        private PopulationUseCase _population;
        private UnitProductionUseCase _unitProduction;
        private GameEndUseCase _gameEnd;
        private IEntityPositionProvider _positionProvider;

        // [피격 표현 큐] 피격 연출(HP 텍스트·VFX·타격 반응)을 공격자의 로컬 타격 프레임에 맞춰 방출.
        // 씬 수동 배치 없이 LoadMap()에서 이 GameObject에 AddComponent 후 Initialize한다.
        // 맵 재로드 시 중복 부착을 막기 위해 참조를 캐시하여 재사용한다. (Phase 2 — 축 3)
        private HitPresentationQueue _hitPresentationQueue;

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
        // 혼잡도 기반 분산 시스템 (v2) — 핵심 인스턴스.
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

        // [Phase 4] 연구소 파괴 시 진행 중 연구 취소·환불 구독(서버/싱글 전용). ClearAll에서 정리.
        private System.IDisposable _labDestroyedSub;

        // [MistShrine] 신전 파괴 시 물안개·자동 모드·쿨다운 정리 구독(서버/싱글 전용). ClearAll에서 정리.
        private System.IDisposable _mistShrineDestroyedSub;

        /// <summary>
        /// StartNetworkGame() 중복 호출 방지 플래그.
        /// NetworkGameFlow가 재스폰될 경우 LoadMap이 재실행되는 것을 막음.
        /// </summary>
        private bool _networkGameStarted = false;

        // ────────────────────────────────────────────────────────────────────
        // 로그 시스템 (GameLog) — LogRules.md 1.8 "sink 구조"
        //
        //   GameLog(Application의 정적 facade)는 "로그를 남긴다"까지만 담당하고,
        //   "어디에 쓸 것인가"는 sink 구현체가 담당한다.
        //   sink 등록은 조합 루트인 이 클래스가 유일하게 수행한다.
        //
        //   두 sink 는 **동시에 등록하지 않는다.**
        //     · 에디터 : FileSink  — RuntimeLogger 를 재사용하며, 파일 + 콘솔에 동시 출력한다.
        //     · 빌드   : ConsoleSink — 콘솔(Logcat)에만 출력한다(빌드에서는 파일 쓰기가 없다).
        //   둘을 함께 등록하면 에디터 콘솔에 같은 줄이 두 번 찍히므로,
        //   InitializeLogging()에서 #if UNITY_EDITOR 로 갈라 하나만 등록한다.
        // ────────────────────────────────────────────────────────────────────

        // 두 필드를 #if 로 갈라 선언하는 이유:
        //   해당 환경에서 쓰이지 않는 필드를 남겨 두면
        //   "대입은 하는데 아무도 읽지 않는 필드"라는 컴파일러 경고가 뜬다.
        //   등록하는 sink 가 환경마다 하나뿐이므로 필드도 하나만 존재하게 만든다.

#if UNITY_EDITOR
        /// <summary>에디터 전용 파일 로그 sink. 빌드에서는 아예 선언되지 않는다.</summary>
        private Hexiege.Infrastructure.FileSink _logFileSink;
#else
        /// <summary>빌드 전용 콘솔 로그 sink. 에디터에서는 아예 선언되지 않는다.</summary>
        private Hexiege.Infrastructure.ConsoleSink _logConsoleSink;
#endif

        /// <summary>
        /// 전역 예외 훅이 직전에 전달한 예외 메시지(중복 수집 방지용).
        /// 우리가 남긴 로그가 훅으로 되돌아오는 경우를 걸러 낸다.
        /// 자세한 이유는 OnUnityLogMessageReceived() 주석 참조.
        /// </summary>
        private string _lastHookCondition;

        /// <summary>
        /// 전역 예외 훅이 직전에 전달한 스택 트레이스(중복 수집 방지용).
        /// _lastHookCondition 과 짝으로 비교한다.
        /// </summary>
        private string _lastHookStackTrace;

        // ────────────────────────────────────────────────────────────────────
        // 새 규칙 4 — 건물 변경 시 즉시 모든 유닛 경로 재계산(eager).
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
        /// 현재 UnitUpgradeUseCase 반환(연구소 강화 팀별 상태).
        /// NetworkUpgradeController에서 서버 측 연구 요청/타이머/브로드캐스트에 사용.
        /// 맵 로드 전이면 null.
        /// </summary>
        public UnitUpgradeUseCase GetUpgradeUseCase() => _unitUpgrade;

        /// <summary>
        /// 현재 SkillActivationUseCase 반환(스킬 발동/글로벌 쿨다운).
        /// NetworkSkillController에서 서버 측 스킬 발동 재검증/실행/쿨다운 브로드캐스트에 사용.
        /// 맵 로드 전이면 null.
        /// </summary>
        public SkillActivationUseCase GetSkillActivationUseCase() => _skillActivation;

        /// <summary>
        /// 현재 StatusEffectSystem 반환(타입 C 스킬 상태효과).
        /// 서버(NetworkCombatController)가 상태 틱 구동에, NetworkSkillController가 멀티 클라 재현에 사용.
        /// 맵 로드 전이면 null.
        /// </summary>
        public StatusEffectSystem GetStatusEffectSystem() => _statusEffectSystem;

        /// <summary>
        /// 현재 MistShrineUseCase 반환(물안개 힐 시전·쿨다운·자동 모드).
        /// NetworkMistShrineController에서 서버 측 시전 재검증·자동 토글·브로드캐스트에 사용.
        /// 맵 로드 전이면 null.
        /// </summary>
        public MistShrineUseCase GetMistShrineUseCase() => _mistShrine;

        /// <summary>
        /// UnitFactory를 IUnitFactory 인터페이스로 반환.
        /// IGameServices 계약 상 IUnitFactory를 반환하므로,
        /// Infrastructure(UnitFactory)에 Application이 직접 의존하지 않는다.
        /// </summary>
        public IUnitFactory GetUnitFactory() => _unitFactory;

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
        // Unity 생명주기 — Awake / Start / Update / OnDestroy
        // ====================================================================

        /// <summary>
        /// IGameServices를 Application 계층 로케이터에 등록한다.
        /// Awake()에서 등록하는 이유: NGO가 NetworkObject를 스폰할 때
        /// OnNetworkSpawn()이 호출되는데, 이는 Start()보다 나중에 일어난다.
        /// Awake()에서 미리 등록해 두면 OnNetworkSpawn()에서 바로 꺼낼 수 있다.
        /// 기존 초기화(맵 로드 등)는 Start()에 그대로 두어 순서를 보존한다.
        ///
        /// 로그 초기화(InitializeLogging)를 맨 앞에 두는 이유:
        ///   로그는 그 뒤에 일어나는 모든 초기화를 관측할 수 있어야 한다.
        ///   기존 초기화 순서를 바꾸지 않고 **앞에 한 줄 덧붙이기만** 한다.
        /// </summary>
        private void Awake()
        {
            // [로그] sink 등록 + 전역 예외 훅 등록. 다른 초기화보다 먼저 살아 있어야 한다.
            InitializeLogging();

            GameServicesLocator.Register(this);
        }

        /// <summary>
        /// 씬이 언로드될 때 로케이터 등록을 해제한다.
        /// 해제하지 않으면 씬 전환 후 파괴된 GameBootstrapper가 Current에 남아
        /// 다음 씬의 NetworkXxx 파일이 stale 참조를 사용하게 된다.
        ///
        /// 로그 정리(ShutdownLogging)는 맨 뒤에 둔다 —
        /// 그 앞에서 일어나는 해제 과정의 로그까지 파일에 남기기 위해서다.
        /// </summary>
        private void OnDestroy()
        {
            GameServicesLocator.Unregister();

            // [로그] 전역 예외 훅 해제 + 파일 세션 종료 + sink 등록 해제.
            ShutdownLogging();
        }

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
                // TorrentSpirit 파도(이동 전선)를 전진시키고 닿은 유닛에 피해/힐을 1회 적용.
                // 싱글플레이 전용 — 멀티플레이에서는 NetworkCombatController가 서버 틱에서 호출한다.
                _unitCombat.TickWaves(Time.deltaTime);
                // BloomFairy HoT(지속 회복) 등 시간 지속 효과를 진행. 파도와 동일하게 서버 권위 틱.
                // 싱글플레이 전용 — 멀티플레이에서는 NetworkCombatController가 호출(이중 틱 금지).
                _unitCombat.TickTimedEffects(Time.deltaTime);
                // [Phase 3] 자연회복(초월 전용 상시 HoT) — BloomFairy 힐과 분리된 독립 채널. 서버 권위 틱.
                _unitCombat.TickNaturalRegen(Time.deltaTime);
            }

            // [Phase 4] 연구 진행 타이머(연구소 강화). 싱글플레이 전용 — 멀티는 NetworkCombatController가 구동.
            //   완료 시 UnitUpgradeUseCase가 레벨을 올리고 OnUpgradeChanged를 발행하여 UI가 갱신된다.
            if (!IsNetworkMode() && _unitUpgrade != null)
            {
                _unitUpgrade.TickResearch(Time.deltaTime);
            }

            // ────────────────────────────────────────────────────────────────
            // [MistShrine] 틱 호출 순서: 쿨다운 감소 → 물안개 진행 → 자동 시전.
            //   이 순서는 멀티 서버(NetworkCombatController.TickCombat)와 **동일**하며, 반드시 지켜야 한다.
            //
            //   왜 쿨다운을 먼저 돌리는가(규칙 18 — "쿨다운이 끝나는 즉시" 자동 시전):
            //     ① 쿨다운을 먼저 깎으면, 이번 프레임에 0이 된 건물은 곧바로 "사용 가능" 상태가 되고
            //        바로 뒤의 TickAutoCast가 그것을 보고 **같은 프레임에** 자동 시전한다.
            //        (반대 순서였다면 TickAutoCast가 감소 이전 값을 보므로 시전이 다음 프레임으로
            //         한 프레임(약 16ms) 밀린다.)
            //     ② TickAutoCast는 내부에서 Activate()를 호출해 **새 쿨다운을 가득 채워 건다**.
            //        만약 쿨다운 감소가 뒤에 있으면, 방금 세운 새 쿨다운을 같은 프레임에 한 번 더
            //        깎아버려(예: 20초 → 19.98초) 시전 주기가 매번 조금씩 짧아진다.
            //        쿨다운을 먼저 돌리면 이 자기 잠식이 원천적으로 사라진다.
            //
            //   ※ 아래 두 블록은 가드 조건이 서로 다르다. 의도된 차이이므로 합치지 말 것.
            // ────────────────────────────────────────────────────────────────

            // [MistShrine ①] 시전 쿨다운 감소.
            //   쿨다운만 가드 형태가 다르다 — 멀티 순수 클라도 쿨다운 오버레이가 서버와 같은 남은 시간을
            //   보여줘야 하므로 "표시용 로컬 미러"로 클라에서도 감소시킨다(스킬 쿨다운과 동일한 검증된 형태).
            //   멀티 서버(호스트)는 NetworkCombatController가 감소시키므로 여기선 스킵한다(이중 틱 금지).
            if (_mistShrine != null &&
                (!IsNetworkMode() || !NetworkContext.IsNetworkServer))
            {
                _mistShrine.TickCooldowns(Time.deltaTime);
            }

            // [MistShrine ②③] 물안개 진행(회복) + 자동 시전 — 서버 권위 틱.
            //   - 싱글: 여기서만 돈다(권위).
            //   - 멀티 서버(호스트): NetworkCombatController.TickCombat이 돌린다 → 여기선 스킵.
            //   - 멀티 순수 클라: 아예 돌지 않는다(HP는 NetworkHealthSync로 받는다).
            //   가드가 !IsNetworkMode() 하나뿐인 이유: 회복은 서버에서만 일어나야 하고,
            //   클라이언트가 함께 돌리면 같은 회복이 두 번 적용되기 때문이다(이중 틱 금지).
            //   MistShrineUseCase 내부에도 NetworkContext 가드가 있어 2중으로 막힌다.
            if (!IsNetworkMode() && _mistShrine != null)
            {
                _mistShrine.TickMists(Time.deltaTime);
                _mistShrine.TickAutoCast(Time.deltaTime);
            }

            // [스킬] 건물 글로벌 쿨다운 감소(규칙 3).
            //   - 싱글: 여기서 감소(권위).
            //   - 멀티 서버(호스트): NetworkCombatController.TickCombat이 감소(권위) → 여기선 스킵.
            //   - 멀티 순수 클라: 서버 전투 틱이 없으므로 오버레이 표시용 로컬 미러를 여기서 감소.
            //   가드: 싱글(!IsNetworkMode) 또는 순수 클라(!IsNetworkServer)일 때만 → 이중 틱 금지.
            if (_skillActivation != null &&
                (!IsNetworkMode() || !NetworkContext.IsNetworkServer))
            {
                _skillActivation.TickCooldowns(Time.deltaTime);
            }

            // [스킬 - 타입 C] 상태효과(버프/디버프/제어) 지속시간 감소.
            //   - 싱글: 여기서 감소(권위).
            //   - 멀티 서버(호스트): NetworkCombatController.TickCombat이 감소(권위) → 여기선 스킵.
            //   - 멀티 순수 클라: 서버 전투 틱이 없으므로 재현된 상태 미러를 여기서 감소(만료 동기화).
            //   가드는 쿨다운 틱과 동일(이중 틱 금지).
            if (_statusEffectSystem != null &&
                (!IsNetworkMode() || !NetworkContext.IsNetworkServer))
            {
                _statusEffectSystem.Tick(Time.deltaTime);
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
        /// NetworkContext.IsNetworkActive로 단일화되어 있어 Update 매 프레임 호출 비용이 낮다.
        /// </summary>
        private bool IsNetworkMode()
        {
            return NetworkContext.IsNetworkActive;
        }
    }
}
