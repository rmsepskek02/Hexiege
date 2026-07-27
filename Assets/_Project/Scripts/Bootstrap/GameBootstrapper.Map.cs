// ============================================================================
// GameBootstrapper.Map.cs (partial 파일)
//
// 본 파일은 GameBootstrapper의 "맵 로드/전환" 코드를 분리해 둔 partial이다.
// Inspector 필드 / 생명주기 메서드는 메인 파일(GameBootstrapper.cs)에 있다.
//
// 담당 영역:
//   1. LoadMap        — 현재 맵 정리 → 그리드 생성 → UseCase 생성 → 카메라/입력/건물/생산 와이어링 →
//                        Castle/금광 자동 배치 → 게임 시작 이벤트 발행
//   2. ClearAll       — 이전 게임의 유닛/건물/혼잡도/구독 모두 정리 (재경기 안전성)
//   3. PlaceCastles   — Blue 하단, Red 상단 Castle 자동 배치
//   4. PlaceGoldMines — 시작 금광 2개 + 채굴소 자동 건설 + 중립 금광 2개
//
// 규칙:
//   * [SerializeField] 필드를 본 파일에 추가하지 않는다 — Inspector 추적성 보장.
//   * Unity 생명주기 메서드를 본 파일에 두지 않는다 — 중복 정의 방지.
// ============================================================================

using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;
using Hexiege.Infrastructure;
using Hexiege.Presentation; // UIManager(전역 로딩 인디케이터) 호출용

namespace Hexiege.Bootstrap
{
    public partial class GameBootstrapper
    {
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
                // 비생산 건물 공용 액션 패널도 IGameUI 구현체이므로 함께 등록한다.
                // 게임 시작/종료 시 자동으로 패널이 닫히도록 보장.
                _uiManager.Register(_buildingActionPanelUI);
                // 인게임 설정 메뉴도 IGameUI — 재경기/게임 종료 시 자동 닫힘 보장을 위해 등록.
                _uiManager.Register(_inGameSettingsUI);
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

            // 9-A. AI 시스템 초기화 (싱글플레이 전용)
            // SetupProduction() 직후 — UseCase가 모두 준비된 시점이어야 한다.
            // 멀티플레이에서는 AI를 생성하지 않는다.
            // AI On/Off(enableAI)는 InitializeAI() 내부에서 AIConfig 로드 후 점검한다.
            if (!NetworkContext.IsNetworkActive)
                InitializeAI();

            // 10. HUD 초기화
            if (_gameHudUI != null)
                _gameHudUI.Initialize(_resource, _population);

            // 10-1. 게임 종료 UI 초기화
            if (_gameEndUI != null)
                _gameEndUI.Initialize();

            // 10-1-1. 인게임 설정 메뉴 초기화.
            // _gameEnd(GameEndUseCase)는 CreateUseCases() 내부에서 생성되므로 이 시점이면 준비됨.
            // 싱글플레이 포기 시 _gameEnd.Forfeit()을 호출하도록 주입한다.
            //
            // IForfeitService 주입:
            //   싱글플레이: _gameEnd가 IForfeitService를 구현 (RequestForfeit → Forfeit 위임)
            //   멀티플레이: _networkGameEnd가 IForfeitService를 구현 (RequestForfeit → ForfeitServerRpc)
            //   InGameSettingsUI는 FindFirstObjectByType<NetworkGameEndController> 호출이 사라진다.
            if (_inGameSettingsUI != null)
            {
                IForfeitService forfeitService = NetworkContext.IsNetworkActive
                    ? (IForfeitService)_networkGameEnd
                    : (IForfeitService)_gameEnd;
                _inGameSettingsUI.Initialize(_gameEnd, forfeitService);
            }

            // 10-2. 부유 HP 텍스트 스포너 초기화
            // 표시의 진입점은 public ShowDamage() — 아래 10-4의 HitPresentationQueue가 방출 시점에 호출한다.
            // (예전처럼 OnEntityDamaged를 직접 구독하지 않는다 — 이중 표시 방지, Phase 2 — 축 3)
            if (_floatingHpTextSpawner != null)
                _floatingHpTextSpawner.Initialize(_positionProvider, _floatingTextContainer, _floatingHpTextPrefab);

            // 10-3. EffectManager 초기화 — VFX/SFX Pool 구성 및 Config 연결
            // 각 Config의 List → Dictionary 변환과 SFX Pool 사전 생성이 여기서 수행된다.
            if (_effectManager != null)
                _effectManager.Initialize(_unitEffectConfig, _buildingEffectConfig, _uiEffectConfig);

            // 10-4. 피격 표현 큐 초기화 (Phase 2 — 축 3).
            //   피격 연출(HP 텍스트·피격 VFX·타격 반응)을 공격자의 로컬 타격 프레임(OnAttackHit)에 맞춰 방출.
            //   씬에 수동 배치하지 않고 조합 루트인 이 GameObject에 AddComponent하여 Inspector 작업을 없앤다.
            //   맵 재로드 시 이미 부착돼 있으면 재사용(중복 AddComponent 방지)하고 Initialize만 다시 호출한다.
            //   EffectManager.Initialize 이후에 두어야 GetHit(피격 프리셋 조회)가 정상 동작한다.
            if (_hitPresentationQueue == null)
                _hitPresentationQueue = gameObject.AddComponent<HitPresentationQueue>();
            _hitPresentationQueue.Initialize(_floatingHpTextSpawner, _unitFactory, _buildingFactory, _unitSpawn, _buildingPlacement);

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

            // 15. 새 규칙 4 — 건물 변경 시 모든 유닛 경로 즉시 재계산.
            //     이 시점이면 _unitFactory / _unitMovement / _flowFieldService 모두 준비됨.
            SetupEagerRepathOnBuildingChanges();

            // 16. Game 씬이 완전히 준비된 시점이다.
            //     게임 시작/재경기 등으로 다른 곳에서 켜둔 전역 로딩 인디케이터를 여기서 끈다(UI 규칙 L-3).
            //     어디서 켰든 목적지 씬(Game)이 준비되면 자동으로 꺼지도록 책임을 일원화한다.
            UIManager.Instance?.ShowLoading(false);
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

            // ────────────────────────────────────────────────────────────
            // 혼잡도 시스템 정리.
            //   - 누적된 혼잡도 비우기: 다음 게임이 0에서 시작하도록.
            //   - OnUnitEnteredTile 구독 해제: 다음 LoadMap()의 CreateUseCases가 새로 구독한다.
            // ────────────────────────────────────────────────────────────
            _congestionMap?.Clear();

            _congestionSub?.Dispose();
            _congestionSub = null;

            // [Phase 4] 연구소 파괴 구독 해제 + 연구 강화 상태 리셋(재경기/맵 전환 시 잔여 레벨 차단).
            _labDestroyedSub?.Dispose();
            _labDestroyedSub = null;
            _unitUpgrade?.Reset();

            // 이전 게임 종료 UseCase 정리
            _gameEnd?.Dispose();
            _gameEnd = null;

            // 인구 UseCase 이벤트 구독 해제 — 재경기 시 누적 카운트 방지.
            _population?.Dispose();
            _population = null;

            // 게임 종료 UI 숨김
            if (_gameEndUI != null)
                _gameEndUI.Hide();

            // eager 재경로 트리거 구독 정리. 다음 LoadMap에서 다시 구독한다.
            _eagerRepathSubscriptions?.Dispose();
            _eagerRepathSubscriptions = null;
        }

        // ====================================================================
        // Castle / 금광 자동 배치
        // ====================================================================

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

            int centerCol = oc.GridWidth / 2; // 맵 중앙 열
            int blueRow = oc.GridHeight - 2;  // Blue Castle 행
            int redRow = 1;                   // Red Castle 행
            int midRow = oc.GridHeight / 2;   // 맵 중앙 행

            // 시작 금광 (각 팀 Castle 횡 2칸, 채굴소 자동 건설)
            int[][] startingMines = new int[][]
            {
                new int[] { centerCol - 2, blueRow }, // Blue 시작 금광
                new int[] { centerCol - 2, redRow },  // Red 시작 금광
            };

            // 중립 금광 (맵 중앙 부근 2개)
            int[][] neutralMines = new int[][]
            {
                new int[] { 2, midRow },
                new int[] { 8, midRow },
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
    }
}
