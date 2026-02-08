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
//   7. 테스트용 유닛 스폰 (프로토타입 검증용)
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

        [Tooltip("메인 카메라")]
        [SerializeField] private Camera _mainCamera;

        // ====================================================================
        // UseCase 인스턴스 (런타임 생성)
        // ====================================================================

        private HexGrid _grid;
        private GridInteractionUseCase _gridInteraction;
        private UnitMovementUseCase _unitMovement;
        private UnitSpawnUseCase _unitSpawn;
        private UnitCombatUseCase _unitCombat;
        private BuildingPlacementUseCase _buildingPlacement;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// 게임 시작 시 기본 맵 로드.
        /// Start()를 사용하는 이유: 다른 컴포넌트의 Awake()가 먼저 실행되도록 보장.
        /// </summary>
        private void Start()
        {
            LoadMap(HexOrientation.FlatTop);
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

            // 9. Castle 자동 배치
            PlaceCastles(orientation, oc);

            // 10. 테스트 유닛 스폰
            SpawnTestUnits(orientation, oc);
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
            _unitMovement = new UnitMovementUseCase(_grid);
            _unitSpawn = new UnitSpawnUseCase(_grid);
            _unitCombat = new UnitCombatUseCase(_grid, _unitSpawn);
            _buildingPlacement = new BuildingPlacementUseCase(_grid);
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
                    _buildingPlacement, _buildingUI);
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
        }

        // ====================================================================
        // 건물 시스템
        // ====================================================================

        /// <summary>
        /// 건물 시스템 초기화. BuildingFactory에 설정 전달, BuildingPlacementUI 초기화.
        /// </summary>
        private void SetupBuildings()
        {
            if (_buildingFactory != null)
                _buildingFactory.SetBuildingYOffset(_config.BuildingYOffset);

            if (_buildingUI != null)
                _buildingUI.Initialize(_buildingPlacement);
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
        /// 프로토타입 검증용 테스트 유닛 스폰.
        /// Blue 팀 1마리, Red 팀 1마리를 맵 양쪽에 배치.
        /// </summary>
        private void SpawnTestUnits(HexOrientation orientation, OrientationConfig oc)
        {
            if (_unitSpawn == null) return;

            // Blue 팀: 맵 하단 중앙
            HexCoord bluePos = HexGrid.OffsetToCube(3, oc.GridHeight - 3, orientation);
            _unitSpawn.SpawnUnit(UnitType.Pistoleer, TeamId.Blue, bluePos);

            // Red 팀: 맵 상단 중앙
            HexCoord redPos = HexGrid.OffsetToCube(3, 2, orientation);
            _unitSpawn.SpawnUnit(UnitType.Pistoleer, TeamId.Red, redPos);

            // 생성된 유닛의 UnitView에 의존성 주입
            InjectUnitViewDependencies();
        }

        /// <summary>
        /// 씬에 존재하는 모든 UnitView에 의존성을 주입.
        /// </summary>
        private void InjectUnitViewDependencies()
        {
            var unitViews = FindObjectsByType<UnitView>(FindObjectsSortMode.None);
            foreach (var view in unitViews)
            {
                view.SetDependencies(_pistoleerAnimData, _config, _unitMovement, _unitCombat);
            }
        }
    }
}
