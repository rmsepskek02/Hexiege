// ============================================================================
// GameBootstrapper.Map.cs (partial 파일)
//
// 본 파일은 GameBootstrapper의 "맵 로드/전환" 코드를 분리해 둔 partial이다.
// Inspector 필드 / 생명주기 메서드는 메인 파일(GameBootstrapper.cs)에 있다.
//
// 담당 영역:
//   1. LoadMap        — 현재 맵 정리 → 그리드 생성 → 무작위 맵 준비/투영 → UseCase 생성 →
//                        카메라/입력/건물/생산 와이어링 → Castle/시작 채굴소 배치 → 게임 시작 이벤트 발행
//   2. ClearAll       — 이전 게임의 유닛/건물/혼잡도/구독 모두 정리 (재경기 안전성)
//   3. PlaceCastles   — 투영 결과가 알려 준 자리에 양 팀 Castle 자동 배치
//   4. PlaceGoldMines — 투영 결과가 알려 준 시작 광산 위에 채굴소 자동 건설
//   5. PrepareAndProjectMap / CreateRootSeed
//                     — 이번 판의 맵을 만들고(MapPreparationUseCase) 격자에 새긴다(MapProjectionUseCase)
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
                // 스킬 건물 전용 패널도 IGameUI(BuildingPanelBase) 구현체 — 게임 시작/종료 시 자동 닫힘 보장.
                //   (미등록 시 OnGameStarted/OnGameEnded가 호출되지 않아 시작/종료 후에도 스킬 패널이 남는다.)
                _uiManager.Register(_buildingSkillPanelUI);
                // 연구소 강화 패널도 IGameUI(BuildingPanelBase) 구현체 — 동일하게 게임 시작/종료 시 자동 닫힘 보장.
                _uiManager.Register(_researchPanelUI);
                // MistShrine 전용 패널도 IGameUI(BuildingPanelBase) 구현체 — 게임 시작/종료 시 자동 닫힘 보장
                //   (닫히면서 OnBeforeClose가 회복 범위 원까지 함께 지운다).
                _uiManager.Register(_mistShrinePanelUI);
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
                // [개발] Inspector 배선 누락 = 설정 오류다.
                //   LogRules.md 1.3 분류 원칙 3 의 단서에 따라 Error 가 아니라 Warn + 개발로 낮춘다.
                //   플레이어 기기에서 벌어지는 일이 아니라 개발 환경의 문제이고, 빌드 전에 잡히기 때문이다.
                GameLog.Dev.Warn("Bootstrap", nameof(GameBootstrapper),
                                 "GameConfig 가 Inspector 에 연결되지 않아 맵을 로드할 수 없다");
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

            // 3-A. 무작위 맵 준비 + 투영 (무작위 맵 2단계 H).
            //   여기서 이번 판의 지형(TileKind)과 광산(MineKind)이 격자에 실제로 새겨진다.
            //   반드시 "그리드 생성 뒤 · 타일 렌더링(5번) 앞"이어야 한다 —
            //   렌더러는 격자의 현재 상태를 읽어 그리므로, 투영이 늦으면 옛 상태가 그려진다.
            PrepareAndProjectMap();

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

            // 11. Castle 자동 배치 (좌표는 3-A 의 투영 결과에서 온다)
            PlaceCastles(orientation, oc);

            // 12. 시작 채굴소 자동 건설 (광산 타일 자체는 3-A 에서 이미 새겨졌다)
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

            // [MistShrine] 신전 파괴 구독 해제 + 물안개/자동 모드/쿨다운 전체 초기화
            //   (재경기·맵 전환 시 이전 게임의 물안개나 쿨다운이 남지 않도록).
            _mistShrineDestroyedSub?.Dispose();
            _mistShrineDestroyedSub = null;
            _mistShrine?.ClearAll();

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
        /// 좌표는 이번 판의 맵 설계도(MapDefinition)를 투영한 결과에서 온다.
        /// </summary>
        /// <param name="orientation">헥스 방향(옛 하드코딩 경로를 되살릴 때 쓰인다)</param>
        /// <param name="oc">해당 방향의 격자 설정(옛 하드코딩 경로를 되살릴 때 쓰인다)</param>
        private void PlaceCastles(HexOrientation orientation, OrientationConfig oc)
        {
            if (_buildingPlacement == null) return;

            // ────────────────────────────────────────────────────────────────
            // [2단계 대체 대기] 무작위 맵 이전의 "좌표를 코드에 박아 두는" 배치 경로.
            //   맵마다 성 자리가 달라졌으므로 더 이상 쓰지 않는다. 아래 무작위 맵
            //   경로가 실기로 검증될 때까지 되돌릴 수단으로 남겨 둔다(WORKFLOW.md [4]).
            //   최종 삭제 시 이 표식을 grep 으로 한 번에 찾는다.
            // ────────────────────────────────────────────────────────────────
            //
            // // Blue Castle: 하단 중앙
            // // 종족에 따라 Castle HP가 다르므로 GameRaceContext에서 종족을 조회하여 전달
            // HexCoord bluePos = HexGrid.OffsetToCube(
            //     oc.GridWidth / 2, oc.GridHeight - 2, orientation);
            // _buildingPlacement.PlaceBuilding(BuildingType.Castle, TeamId.Blue, bluePos,
            //     GameRaceContext.BlueRace);
            //
            // // Red Castle: 상단 중앙
            // HexCoord redPos = HexGrid.OffsetToCube(
            //     oc.GridWidth / 2, 1, orientation);
            // _buildingPlacement.PlaceBuilding(BuildingType.Castle, TeamId.Red, redPos,
            //     GameRaceContext.RedRace);

            // ── 무작위 맵 경로 ──────────────────────────────────────────────
            // 맵 준비/투영이 실패했다면 성을 세울 자리를 모른다. 이때 임의의 자리에
            // 세우면 "맵이 이상하다"가 아니라 "게임이 이상하다"로 보이게 되므로,
            // 아무것도 하지 않는다. 실패 사유는 PrepareAndProjectMap 이 이미 남겼다.
            if (_mapProjection == null || !_mapProjection.IsSucceeded) return;

            foreach (MapTeamPlacement castle in _mapProjection.Castles)
            {
                // 종족에 따라 Castle HP가 다르므로 GameRaceContext에서 종족을 조회하여 전달.
                RaceId race = (castle.Team == TeamId.Blue)
                    ? GameRaceContext.BlueRace
                    : GameRaceContext.RedRace;

                _buildingPlacement.PlaceBuilding(BuildingType.Castle, castle.Team, castle.Coord, race);
            }
        }

        /// <summary>
        /// 시작 채굴소 자동 건설.
        ///
        /// 금광 타일 자체(MineKind)는 이제 이 메서드가 찍지 않는다.
        /// 3-A 의 투영(MapProjectionUseCase)이 설계도의 광산 목록을 보고 이미 새겨 두었다.
        /// 여기 남는 일은 "각 팀 시작 광산 위에 채굴소를 세우는 것" 하나뿐이다.
        /// </summary>
        /// <param name="orientation">헥스 방향(옛 하드코딩 경로를 되살릴 때 쓰인다)</param>
        /// <param name="oc">해당 방향의 격자 설정(옛 하드코딩 경로를 되살릴 때 쓰인다)</param>
        private void PlaceGoldMines(HexOrientation orientation, OrientationConfig oc)
        {
            if (_grid == null) return;

            // ────────────────────────────────────────────────────────────────
            // [2단계 대체 대기] 무작위 맵 이전의 "좌표를 코드에 박아 두는" 금광 배치 경로.
            //   광산 자리가 맵마다 달라졌으므로 더 이상 쓰지 않는다. 아래 무작위 맵
            //   경로가 실기로 검증될 때까지 되돌릴 수단으로 남겨 둔다(WORKFLOW.md [4]).
            //   최종 삭제 시 이 표식을 grep 으로 한 번에 찾는다.
            //
            //   🔴 1단계에서 지우지 못했던 "숨은 참조"가 바로 이 구간이다.
            //      아래 시작 채굴소 자동 건설이 좌표 배열을 직접 읽고 있었기 때문에
            //      배열만 지우면 채굴소 건설이 함께 깨졌다. 이번에는 그 연결을 끊어
            //      채굴소 좌표를 투영 결과(설계도의 시작 광산 목록)에서 받는다.
            // ────────────────────────────────────────────────────────────────
            //
            // int centerCol = oc.GridWidth / 2; // 맵 중앙 열
            // int blueRow = oc.GridHeight - 2;  // Blue Castle 행
            // int redRow = 1;                   // Red Castle 행
            // int midRow = oc.GridHeight / 2;   // 맵 중앙 행
            //
            // // 시작 금광 (각 팀 Castle 횡 2칸, 채굴소 자동 건설)
            // int[][] startingMines = new int[][]
            // {
            //     new int[] { centerCol - 2, blueRow }, // Blue 시작 금광
            //     new int[] { centerCol - 2, redRow },  // Red 시작 금광
            // };
            //
            // // 중립 금광 (맵 중앙 부근 2개)
            // int[][] neutralMines = new int[][]
            // {
            //     new int[] { 2, midRow },
            //     new int[] { 8, midRow },
            // };
            //
            // // 금광 타일 설정.
            // // 예전에는 "금광이 있다" 플래그와 "이동 불가" 플래그를 나란히 두 번 대입했지만,
            // // 이제는 MineKind 하나만 설정하면 이동 가능 여부가 자동으로 계산된다.
            // // 어떤 팀의 광산인지(BlueStart/RedStart/Neutral)까지 구분해 넣기 위해
            // // 매개변수로 MineKind를 받는다.
            // void SetGoldMine(int col, int row, MineKind mineKind)
            // {
            //     HexCoord coord = HexGrid.OffsetToCube(col, row, orientation);
            //     HexTile tile = _grid.GetTile(coord);
            //     if (tile != null)
            //     {
            //         tile.MineKind = mineKind;
            //     }
            // }
            //
            // // 시작 금광은 팀별로 MineKind가 다르므로 배열 순회 대신 하나씩 명시적으로 호출한다.
            // // 좌표는 위 배열을 그대로 사용해 좌표 계산이 두 벌로 갈라지지 않게 한다.
            // SetGoldMine(startingMines[0][0], startingMines[0][1], MineKind.BlueStart);
            // SetGoldMine(startingMines[1][0], startingMines[1][1], MineKind.RedStart);
            //
            // // 중립 금광은 전부 같은 종류이므로 기존처럼 순회한다.
            // foreach (var m in neutralMines) SetGoldMine(m[0], m[1], MineKind.Neutral);
            //
            // // 시작 채굴소 자동 건설 (금광 타일 위에 직접 배치)
            // if (_buildingPlacement != null)
            // {
            //     // Blue 시작 채굴소
            //     // 종족에 따라 MiningPost HP가 다르므로 GameRaceContext에서 종족을 조회하여 전달
            //     HexCoord blueMinePos = HexGrid.OffsetToCube(
            //         startingMines[0][0], startingMines[0][1], orientation);
            //     _buildingPlacement.PlaceMiningPostDirect(TeamId.Blue, blueMinePos,
            //         GameRaceContext.BlueRace);
            //
            //     // Red 시작 채굴소
            //     HexCoord redMinePos = HexGrid.OffsetToCube(
            //         startingMines[1][0], startingMines[1][1], orientation);
            //     _buildingPlacement.PlaceMiningPostDirect(TeamId.Red, redMinePos,
            //         GameRaceContext.RedRace);
            // }

            // ── 무작위 맵 경로 ──────────────────────────────────────────────
            if (_buildingPlacement == null) return;
            if (_mapProjection == null || !_mapProjection.IsSucceeded) return;

            foreach (MapTeamPlacement mine in _mapProjection.StartingMines)
            {
                // 종족에 따라 MiningPost HP가 다르므로 GameRaceContext에서 종족을 조회하여 전달.
                RaceId race = (mine.Team == TeamId.Blue)
                    ? GameRaceContext.BlueRace
                    : GameRaceContext.RedRace;

                // PlaceMiningPostDirect 는 "인접 타일이 우리 팀인가" 조건을 건너뛴다.
                // 경기 시작 시점에는 아직 아무 영토도 없으므로 그 조건을 만족할 수 없다.
                _buildingPlacement.PlaceMiningPostDirect(mine.Team, mine.Coord, race);
            }
        }

        // ====================================================================
        // 무작위 맵 준비 + 투영 (2단계 H)
        // ====================================================================

        /// <summary>
        /// 이번 판의 맵을 만들고(MapPreparationUseCase) 격자에 새긴다(MapProjectionUseCase).
        ///
        /// 🔴 동기 호출이다. 코루틴으로 바꾸지 않았다.
        ///    실측(데스크톱, 300회)에서 맵 준비는 중앙값 0ms · 95번째 0ms · 최대 7ms 였고,
        ///    첫 시도에서 검증을 통과하는 비율이 사실상 100% 라 실제로는 "한 번 만들고 끝"이다.
        ///    최악의 경우(100번 재시도)에도 로딩 화면에서 한 번 걸리는 수준이다.
        ///
        /// 💡 이상적으로는 이 함수를 부르기 직전에 한 프레임을 넘기는 것이 좋다.
        ///    그래야 로딩 UI 가 화면에 실제로 그려진 뒤에 계산이 시작된다.
        ///    지금은 LoadMap 전체가 동기 함수라 그 자리가 없다. 프레임을 넘기려면
        ///    LoadMap 을 코루틴으로 바꿔야 하는데, 그러면 LoadMap 을 부르는 모든 곳이
        ///    함께 흔들린다. H 단계는 이미 화면이 바뀌는 가장 위험한 단계라
        ///    그 개조를 겹치지 않기로 했다(별도 작업으로 제안).
        /// </summary>
        private void PrepareAndProjectMap()
        {
            // 이전 판의 결과가 남아 새 판에 섞여 들지 않도록 먼저 비운다.
            _mapPreparation = null;
            _mapProjection = null;

            if (_config == null || _grid == null) return;

            // 폴백 템플릿은 Resources 에서 읽는다(Infrastructure 구현체).
            // Application 의 조정자는 인터페이스만 알고 Unity 를 모른다.
            var preparation = new MapPreparationUseCase(new ResourcesMapFallbackTemplateSource());

            ulong rootSeed = CreateRootSeed();

            // 테스트 모드 표식은 로컬 GameConfig 가 권위다(규칙 3 — 싱글 권위).
            MapPreparationResult prepared = preparation.Prepare(rootSeed, _config.MapTestModeEnabled);

            // 🔴 결과를 반드시 보관한다.
            //    문제가 생긴 맵을 다시 만들어 보려면 그 판의 root seed 가 있어야 하는데,
            //    seed 는 매 판 새로 뽑히므로 여기서 놓치면 영영 재현할 수 없다.
            //    K 단계가 이 값을 꺼내 로그로 남긴다(GetLastMapPreparation).
            _mapPreparation = prepared;

            if (!prepared.IsSucceeded)
            {
                // [개발] 맵 준비 실패 = 폴백 템플릿까지 실패한 상태다. 경기를 진행할 수 없다.
                //   운영 로그 키 신설은 K 단계 범위라 여기서는 개발 로그로만 남긴다.
                GameLog.Dev.Error("Map", nameof(GameBootstrapper),
                                  "맵 준비 실패 — 성/광산을 배치하지 않는다", prepared.ToString());
                return;
            }

            // 설계도를 격자에 새긴다. 격자 크기(11x21)가 설계도와 다르면 여기서 실패한다.
            MapProjectionResult projected = MapProjectionUseCase.Project(prepared.Definition, _grid);
            _mapProjection = projected;

            if (!projected.IsSucceeded)
            {
                // [개발] 크기·헥스 방향 불일치 등 설정 오류다. GameConfig 의 격자 크기를 먼저 본다.
                GameLog.Dev.Error("Map", nameof(GameBootstrapper),
                                  "맵 투영 실패 — 성/광산을 배치하지 않는다", projected.ToString());
            }
        }

        /// <summary>
        /// 이번 판의 64비트 root seed 를 정한다.
        ///
        /// 싱글플레이는 로컬이 권위이므로 매 판 새로 뽑는다(규칙 3).
        /// 🔴 UnityEngine.Random 이나 GetHashCode 를 쓰지 않는다 — 맵 생성 계통에서
        ///    그것들을 쓰지 않기로 한 약속(TDD)과 혼동될 여지를 남기지 않기 위해서다.
        ///    여기서 필요한 것은 "매 판 다른 64비트 값"뿐이므로 시각과 GUID 를 섞는다.
        /// </summary>
        /// <returns>이번 판의 root seed</returns>
        private ulong CreateRootSeed()
        {
            // ── 멀티플레이 임시 처리 ────────────────────────────────────────
            // 🔴 Host 가 정한 seed 를 클라이언트에게 보내는 일은 3단계 범위다.
            //    지금 양쪽이 각자 seed 를 뽑으면 두 사람이 서로 다른 맵을 보게 된다.
            //    그래서 멀티에서는 고정 seed 를 쓴다 — 같은 seed 면 같은 맵이므로
            //    (규칙 12) 전송 없이도 양쪽이 반드시 같은 맵을 얻는다.
            //    3단계에서 Host 권위 seed 전송이 들어오면 이 분기는 사라진다.
            if (IsNetworkMode())
                return NetworkInterimRootSeed;

            // ── 싱글플레이: 매 판 다른 값 ───────────────────────────────────
            // GUID 는 앞 8바이트만 써도 충분히 흩어지고, 시각을 섞어 같은 프로세스에서
            // 연속으로 시작해도 값이 겹치지 않게 한다.
            byte[] guidBytes = System.Guid.NewGuid().ToByteArray();

            ulong entropy = 0UL;
            for (int i = 0; i < 8; i++)
                entropy = (entropy << 8) | guidBytes[i];

            return entropy ^ (ulong)System.DateTime.UtcNow.Ticks;
        }
    }
}
