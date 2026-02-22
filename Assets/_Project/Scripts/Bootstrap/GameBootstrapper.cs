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
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;
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

        [Tooltip("권총병 애니메이션 데이터")]
        [SerializeField] private UnitAnimationData _pistoleerAnimData;

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

        /// <summary>
        /// StartNetworkGame() 중복 호출 방지 플래그.
        /// NetworkGameFlow가 재스폰될 경우 LoadMap이 재실행되는 것을 막음.
        /// </summary>
        private bool _networkGameStarted = false;

        /// <summary>
        /// 네트워크 게임이 이미 시작되었는지 여부.
        /// NetworkGameFlow.OnNetworkSpawn()에서 재스폰 감지용으로 사용.
        /// </summary>
        public bool IsNetworkGameStarted => _networkGameStarted;

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
            // 네트워크 모드 확인: NetworkManager가 활성화되어 있으면 네트워크 게임
            bool isNetworkMode = NetworkManager.Singleton != null &&
                                 (NetworkManager.Singleton.IsHost ||
                                  NetworkManager.Singleton.IsClient);

            if (isNetworkMode)
            {
                // 네트워크 모드: NetworkGameFlow가 StartGameClientRpc를 통해
                // StartNetworkGame()을 호출하므로 여기서는 맵 로드를 건너뜀
                Debug.Log("[Network] GameBootstrapper: 네트워크 모드 감지. 맵 로드는 NetworkGameFlow에 위임.");
            }
            else
            {
                // 싱글플레이 모드: 기존 로직 그대로 실행
                LoadMap(HexOrientation.FlatTop);
            }
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
            // 게임 오버 상태에서 재시작 시 시간 복원
            Time.timeScale = 1f;

            // ViewConverter 리셋 — 싱글플레이 전용.
            // 네트워크 모드에서는 StartNetworkGame()이 LoadMap() 호출 전에
            // ViewConverter.Setup()을 완료하므로 여기서 리셋하지 않음.
            bool isNetworkMode = NetworkManager.Singleton != null &&
                                 (NetworkManager.Singleton.IsHost ||
                                  NetworkManager.Singleton.IsClient);
            if (!isNetworkMode)
            {
                ViewConverter.Reset();
            }

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

            // 11. Castle 자동 배치
            PlaceCastles(orientation, oc);

            // 12. 금광 배치
            PlaceGoldMines(orientation, oc);

            // 13. 금광 렌더링
            if (_gridRenderer != null)
                _gridRenderer.RenderGoldMines(_grid);

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
            LoadMap(HexOrientation.FlatTop);

            // 팀에 따른 카메라 시작 위치 조정
            if (_cameraController != null && _config != null)
            {
                OrientationConfig oc = _config.FlatTop;
                SetCameraStartPositionForTeam(localTeam, oc);
            }
        }

        /// <summary>
        /// 로컬 플레이어 팀에 맞춰 카메라 초기 위치를 설정.
        /// Blue 팀: 맵 하단(자신의 Castle 근처), Red 팀: 반전된 뷰 기준 하단.
        /// ViewConverter가 설정된 후 호출되어야 함.
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
            startPos.z = 0f;
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
            _unitMovement = new UnitMovementUseCase(_grid, _unitSpawn);
            _buildingPlacement = new BuildingPlacementUseCase(_grid);
            _unitCombat = new UnitCombatUseCase(_grid, _unitSpawn, _buildingPlacement);

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
        /// </summary>
        private void SetupCamera(HexOrientation orientation, OrientationConfig oc)
        {
            if (_cameraController == null || _config == null) return;

            // 맵 중심 계산
            Vector3 center = HexMetrics.GridCenter(oc.GridWidth, oc.GridHeight);
            _cameraController.SetPosition(center);

            // 맵 경계 설정 (여유분 포함)
            Vector3 topLeft = HexMetrics.HexToWorld(
                HexGrid.OffsetToCube(0, 0, orientation));
            Vector3 bottomRight = HexMetrics.HexToWorld(
                HexGrid.OffsetToCube(oc.GridWidth - 1, oc.GridHeight - 1, orientation));

            float margin = 2f;
            Vector3 size = new Vector3(
                Mathf.Abs(bottomRight.x - topLeft.x) + margin * 2,
                Mathf.Abs(topLeft.y - bottomRight.y) + margin * 2,
                0f);

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
                    _gridInteraction, _unitMovement, _unitSpawn, _mainCamera,
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

            // 이전 게임 종료 UseCase 정리
            _gameEnd?.Dispose();
            _gameEnd = null;

            // 게임 종료 UI 숨김
            if (_gameEndUI != null)
                _gameEndUI.Hide();
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
                bool isNetworkMode = Unity.Netcode.NetworkManager.Singleton != null &&
                                     (Unity.Netcode.NetworkManager.Singleton.IsHost ||
                                      Unity.Netcode.NetworkManager.Singleton.IsClient);

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
            // UnitFactory에 런타임 의존성 주입 (생산된 유닛에 자동 적용)
            if (_unitFactory != null)
                _unitFactory.SetDependencyReferences(_pistoleerAnimData, _config, _unitMovement, _unitCombat);

            // 생산 티커 초기화 (ProductionPanelUI보다 먼저 — UI에서 마커 참조 필요)
            if (_productionTicker != null)
                _productionTicker.Initialize(
                    _unitProduction, _resource, _unitMovement,
                    _buildingPlacement, _unitFactory, _config);

            // 네트워크 모드 여부에 따라 NetworkProductionController 주입 (싱글플레이 시 null)
            bool isNetworkMode = Unity.Netcode.NetworkManager.Singleton != null &&
                                 (Unity.Netcode.NetworkManager.Singleton.IsHost ||
                                  Unity.Netcode.NetworkManager.Singleton.IsClient);

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
            HexCoord bluePos = HexGrid.OffsetToCube(
                oc.GridWidth / 2, oc.GridHeight - 2, orientation);
            _buildingPlacement.PlaceBuilding(BuildingType.Castle, TeamId.Blue, bluePos);

            // Red Castle: 상단 중앙
            HexCoord redPos = HexGrid.OffsetToCube(
                oc.GridWidth / 2, 1, orientation);
            _buildingPlacement.PlaceBuilding(BuildingType.Castle, TeamId.Red, redPos);
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
                HexCoord blueMinePos = HexGrid.OffsetToCube(
                    startingMines[0][0], startingMines[0][1], orientation);
                _buildingPlacement.PlaceMiningPostDirect(TeamId.Blue, blueMinePos);

                // Red 시작 채굴소
                HexCoord redMinePos = HexGrid.OffsetToCube(
                    startingMines[1][0], startingMines[1][1], orientation);
                _buildingPlacement.PlaceMiningPostDirect(TeamId.Red, redMinePos);
            }
        }

    }
}
