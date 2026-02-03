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

        [Tooltip("메인 카메라")]
        [SerializeField] private Camera _mainCamera;

        // ====================================================================
        // UseCase 인스턴스 (런타임 생성)
        // ====================================================================

        private HexGrid _grid;
        private GridInteractionUseCase _gridInteraction;
        private UnitMovementUseCase _unitMovement;
        private UnitSpawnUseCase _unitSpawn;

        // ====================================================================
        // 초기화 (Start에서 실행)
        // ====================================================================

        /// <summary>
        /// 게임 시작 시 전체 시스템 초기화.
        /// Start()를 사용하는 이유: 다른 컴포넌트의 Awake()가 먼저 실행되도록 보장.
        /// </summary>
        private void Start()
        {
            InitializeConfig();
            CreateGrid();
            CreateUseCases();
            RenderGrid();
            SetupCamera();
            SetupInput();
            SpawnTestUnits();
        }

        // ====================================================================
        // 1단계: 설정 적용
        // ====================================================================

        /// <summary>
        /// GameConfig의 타일 크기를 HexMetrics에 적용.
        /// HexMetrics는 static 클래스이므로 직접 값을 복사.
        /// </summary>
        private void InitializeConfig()
        {
            if (_config == null)
            {
                Debug.LogError("[GameBootstrapper] GameConfig가 설정되지 않았습니다.");
                return;
            }

            HexMetrics.TileWidth = _config.TileWidth;
            HexMetrics.TileHeight = _config.TileHeight;
            HexMetrics.UnitYOffset = _config.UnitYOffset;
        }

        // ====================================================================
        // 2단계: Domain 그리드 생성
        // ====================================================================

        /// <summary>
        /// 7×17 HexGrid 인스턴스 생성. 119개 타일이 Dictionary에 저장됨.
        /// </summary>
        private void CreateGrid()
        {
            _grid = new HexGrid(_config.GridWidth, _config.GridHeight);
        }

        // ====================================================================
        // 3단계: UseCase 생성
        // ====================================================================

        /// <summary>
        /// Application 레이어의 UseCase 인스턴스들을 생성하고 그리드 참조 주입.
        /// </summary>
        private void CreateUseCases()
        {
            _gridInteraction = new GridInteractionUseCase(_grid);
            _unitMovement = new UnitMovementUseCase(_grid);
            _unitSpawn = new UnitSpawnUseCase(_grid);
        }

        // ====================================================================
        // 4단계: 그리드 렌더링
        // ====================================================================

        /// <summary>
        /// HexGridRenderer에 그리드 데이터를 전달하여 화면에 타일 배치.
        /// </summary>
        private void RenderGrid()
        {
            if (_gridRenderer != null)
            {
                _gridRenderer.RenderGrid(_grid);
            }
        }

        // ====================================================================
        // 5단계: 카메라 설정
        // ====================================================================

        /// <summary>
        /// 카메라 초기 위치를 맵 중심으로 설정하고, 이동 경계를 지정.
        /// </summary>
        private void SetupCamera()
        {
            if (_cameraController == null || _config == null) return;

            // 맵 중심 계산
            Vector3 center = HexMetrics.GridCenter(_config.GridWidth, _config.GridHeight);
            _cameraController.SetPosition(center);

            // 맵 경계 설정 (여유분 포함)
            Vector3 topLeft = HexMetrics.HexToWorld(HexGrid.OffsetToCube(0, 0));
            Vector3 bottomRight = HexMetrics.HexToWorld(
                HexGrid.OffsetToCube(_config.GridWidth - 1, _config.GridHeight - 1));

            float margin = 2f; // 경계 여유분
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
        // 6단계: 입력 연결
        // ====================================================================

        /// <summary>
        /// InputHandler에 UseCase 의존성을 주입.
        /// </summary>
        private void SetupInput()
        {
            if (_inputHandler != null)
            {
                _inputHandler.Initialize(_gridInteraction, _unitMovement, _unitSpawn, _mainCamera);
            }
        }

        // ====================================================================
        // 7단계: 테스트 유닛 스폰
        // ====================================================================

        /// <summary>
        /// 프로토타입 검증용 테스트 유닛 스폰.
        /// Blue 팀 1마리, Red 팀 1마리를 맵 양쪽에 배치.
        ///
        /// 스폰 후 UnitView에 의존성 주입 (animData, config, movementUseCase).
        /// </summary>
        private void SpawnTestUnits()
        {
            if (_unitSpawn == null) return;

            // Blue 팀: 맵 하단 중앙 (row=27, col=3)
            HexCoord bluePos = HexGrid.OffsetToCube(3, 27);
            UnitData blueUnit = _unitSpawn.SpawnUnit(UnitType.Pistoleer, TeamId.Blue, bluePos);

            // Red 팀: 맵 상단 중앙 (row=2, col=3)
            HexCoord redPos = HexGrid.OffsetToCube(3, 2);
            UnitData redUnit = _unitSpawn.SpawnUnit(UnitType.Pistoleer, TeamId.Red, redPos);

            // 생성된 유닛의 UnitView에 의존성 주입
            InjectUnitViewDependencies();
        }

        /// <summary>
        /// 씬에 존재하는 모든 UnitView에 의존성(animData, config, movementUseCase)을 주입.
        /// UnitFactory가 프리팹을 생성한 뒤 호출.
        /// </summary>
        private void InjectUnitViewDependencies()
        {
            var unitViews = FindObjectsByType<UnitView>(FindObjectsSortMode.None);
            foreach (var view in unitViews)
            {
                view.SetDependencies(_pistoleerAnimData, _config, _unitMovement);
            }
        }
    }
}
