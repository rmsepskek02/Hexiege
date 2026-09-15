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
        /// 이번 판의 맵을 만들고(<see cref="PrepareMap"/>) 곧바로 격자에 새긴다(<see cref="ProjectMap"/>).
        ///
        /// 🔴 이 함수는 <b>이름만 남은 래퍼</b>다(무작위 맵 3단계 D).
        ///    원래 한 함수 안에서 하던 "준비"와 "투영"을 두 함수로 갈랐는데,
        ///    <b>부르는 쪽(LoadMap)은 한 글자도 바뀌지 않게</b> 하려고 옛 이름을 그대로 남겼다.
        ///    가른 이유는 멀티플레이 때문이다 — 규칙 16 이 "해시 대조가 씬 전환보다 먼저"라고
        ///    정하므로, Host 는 <b>로비에서 준비</b>하고 <b>전투 씬에서 투영</b>해야 하고
        ///    Client 는 <b>준비 없이 투영만</b> 해야 한다. 즉 두 일이 다른 시점·다른 주체가 된다.
        ///    ⚠️ 그 배선(누가 언제 부르는가)은 아직 하지 않았다. 이 단계는 가르기만 하며,
        ///       싱글·멀티 모두 지금까지와 똑같이 이 래퍼를 통해 연달아 실행된다.
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
            //
            // 🔴 이 두 줄과 바로 아래 가드는 <b>일부러 래퍼에 남겨 두었다.</b>
            //    가르기 전과 실행 순서가 한 칸도 달라지면 안 되기 때문이다. 특히
            //    _mapProjection 을 비우는 일은 "맵 준비가 실패한 판"에서도 반드시 실행돼야 한다 —
            //    준비 실패로 일찍 돌아가면 투영은 아예 안 하는데, 그때 이 줄이 없으면
            //    <b>지난 판의 투영 결과가 그대로 남아</b> 성·시작 채굴소가 옛 자리에 배치된다.
            _mapPreparation = null;
            _mapProjection = null;

            // _config 는 준비에, _grid 는 투영에 필요하다. 둘 중 하나라도 없으면
            // 아무것도 하지 않는다 — 가르기 전과 같은 조건·같은 자리다.
            if (_config == null || _grid == null) return;

            // ── 멀티플레이: 준비하지 않고 「새기기만」 한다 (무작위 맵 3단계 I) ──────────
            //
            // 🔴 왜 멀티는 여기서 갈라지는가 (초급자용):
            //    멀티에서 맵을 정하는 권한은 Host 에게만 있다(규칙 12). Host 는 **로비에서**
            //    맵을 만들어 Client 에게 통째로 보내고, 양쪽 해시가 같을 때만 전투 씬으로
            //    넘어온다(규칙 16). 그러니 전투 씬에 도착한 시점에는 **이번 판의 맵이 이미
            //    확정돼 있다.** 여기서 또 만들면 그 확정된 맵과 다른 맵이 하나 더 생기고,
            //    둘이 갈라질 자리가 생긴다. 그래서 Host 도 Client 도 똑같이,
            //    로비에서 확정된 것을 MapHandoff 로 받아 **투영만** 한다.
            //
            // ⚠️ 싱글플레이는 이 분기에 들어오지 않는다 — 아래 기존 경로 그대로 준비 + 투영이다.
            if (IsNetworkMode())
            {
                ProjectHandedOverMap();
                return;
            }

            MapPreparationResult prepared = PrepareMap();

            // 준비가 실패했으면 여기서 끝난다(실패 로그는 PrepareMap 안에서 이미 남겼다).
            // 가르기 전의 "if (!prepared.IsSucceeded) { 로그; return; }" 와 완전히 같은 지점이다.
            if (prepared == null) return;

            ProjectMap(prepared);
        }

        /// <summary>
        /// 이번 판의 맵 설계도를 만든다(<see cref="MapPreparationUseCase"/>). <b>격자에는 손대지 않는다.</b>
        ///
        /// 하는 일은 세 가지다 — ① root seed 를 정해 맵을 만들고 ② 그 결과를 _mapPreparation 에
        /// 보관하고 ③ 규칙 12 의 로그를 한 줄 내보낸다.
        ///
        /// ⚠️ <b>전제</b>: 호출 전에 _config 가 null 이 아니어야 한다(안에서 _config 를 읽는다).
        ///    현재 유일한 호출부인 <see cref="PrepareAndProjectMap"/> 가 앞에서 확인하고 부른다.
        /// </summary>
        /// <returns>준비에 성공한 결과. 실패했으면 null(실패 로그는 이 안에서 남긴다)</returns>
        private MapPreparationResult PrepareMap()
        {
            // 폴백 템플릿은 Resources 에서 읽는다(Infrastructure 구현체).
            // Application 의 조정자는 인터페이스만 알고 Unity 를 모른다.
            var preparation = new MapPreparationUseCase(new ResourcesMapFallbackTemplateSource());

            ulong rootSeed = CreateRootSeed();

            // 🔴 2026-09-14: 종전에는 두 번째 인자로 _config.MapTestModeEnabled 를 함께 넘겼다.
            //    「맵 테스트 모드」가 규칙에서 삭제돼(GameSystemRules_RandomMap.md 규칙 3 아래
            //    2026-09-14 개정 블록) Prepare 의 인자가 root seed 하나로 줄었다.
            MapPreparationResult prepared = preparation.Prepare(rootSeed);

            // 🔴 결과를 반드시 보관한다.
            //    문제가 생긴 맵을 다시 만들어 보려면 그 판의 root seed 가 있어야 하는데,
            //    seed 는 매 판 새로 뽑히므로 여기서 놓치면 영영 재현할 수 없다.
            //    보관은 로그와 별개로 필요하다 — 로그에는 해시 앞 16자만 실리지만
            //    여기에는 최종 맵 바이트·해시 원본까지 통째로 남기 때문이다.
            //    ⚠️ 종전에는 이 자리에 「다른 코드가 GetLastMapPreparation() 으로 같은 판의
            //       결과를 다시 읽는다」고 적혀 있었으나 사실이 아니어서 고쳤다(2026-09-09).
            //       그 접근자의 호출자는 0건이고 3단계가 끝나도 0건으로 남는다 —
            //       확정된 맵을 전투 씬으로 넘기는 일은 그 접근자가 아니라
            //       Application/MapHandoff.cs(정적 홀더)가 맡기 때문이다.
            _mapPreparation = prepared;

            // ── 규칙 12 「로그 필수 항목」을 운영 로그로 내보낸다 (2단계 K) ──────
            //
            // 🔴 왜 여기(호출부)에서 내보내고 MapPreparationUseCase 안에서 내보내지 않는가:
            //    그 조정자는 **일부러** UnityEngine 을 참조하지 않는 순수 C# 으로 두었다.
            //    그래야 Unity 없이 그 파일만 따로 컴파일해서 "100회 실패 → 폴백" 같은
            //    좀처럼 안 걸리는 경로까지 실제로 돌려 볼 수 있다. 거기에 로그 호출을 넣으면
            //    그 검증 수단이 통째로 사라진다. 그래서 조정자는 값만 결과 객체에 담아 주고,
            //    실제 발신은 Unity 를 아는 이 자리에서 한 번만 한다.
            //
            // 🔴 결말마다 줄은 **정확히 하나**다(LogRules 1.14 금지 8·9).
            //    성공/폴백/실패는 서로 배타적이므로 한 판에 두 줄이 남지 않는다.
            string preparationData = BuildMapPreparationLogData(prepared);

            if (!prepared.IsSucceeded)
            {
                // [운영/Error] 폴백 템플릿까지 실패했다 = 경기를 진행할 수 없다(복구 경로 없음).
                //   ⚠️ 실패 사유(FailureReason)는 사람이 읽는 자유 문장이라 message 쪽에 넣는다.
                //      key=value 에 넣으면 안 된다 — 그 문장에는 ", " 가 들어 있어 구분자와
                //      충돌해 필드 하나가 둘로 쪼개진다(LogRules 1.4).
                //   ⚠️ 종전에는 여기서 개발 로그만 남겼다. "운영 키 신설이 K 단계 범위" 라서였고,
                //      그 키가 이번에 생겼으므로 운영으로 올린다. 개발 로그로 두면 릴리스에서
                //      컴파일 단계에 사라져(LogRules 1.7) 정작 필요한 출시본에 기록이 없다.
                GameLog.Ops.Error(LogEvent.MapPreparationFailed, MapLogSystem, nameof(GameBootstrapper),
                                  "맵 준비 실패 — 성/광산을 배치하지 않는다: " +
                                  (prepared.FailureReason ?? "사유 없음"), preparationData);
                return null;
            }

            if (prepared.UsedFallback)
            {
                // [운영/Warn] 생성 시도가 전부 거부되어 폴백 템플릿으로 경기를 연다.
                //   맵 자체는 검증을 통과한 정상 맵이라 경기는 그대로 진행된다 → 축 A 는 Warn.
                GameLog.Ops.Warn(LogEvent.MapPreparationUsedFallbackTemplate, MapLogSystem,
                                 nameof(GameBootstrapper),
                                 "맵 생성 시도가 모두 거부되어 폴백 템플릿을 사용했다", preparationData);
            }
            else
            {
                // [운영/Info] 정상 경로로 맵이 확정됐다. 경기당 한 줄이며,
                //   위 두 키의 발생률을 계산할 때의 분모가 된다.
                GameLog.Ops.Info(LogEvent.MapPreparationSucceeded, MapLogSystem,
                                 nameof(GameBootstrapper), "맵 준비 완료", preparationData);
            }

            return prepared;
        }

        /// <summary>
        /// [멀티플레이 전용] 로비에서 확정된 맵을 <see cref="MapHandoff"/> 에서 받아 그대로 새긴다.
        /// <b>맵을 만들지 않는다</b> — Host 든 Client 든 이 자리에서는 받아 쓰기만 한다.
        ///
        /// 🔴 인계가 비어 있으면(= 로비에서 확정된 맵이 넘어오지 않았다) <b>그 판은 성립하지 않는다.</b>
        ///    이때 "지난 판 맵"이나 "여기서 새로 만든 맵"으로 때우면 안 된다. 화면에는 멀쩡한 맵이
        ///    뜨고 경기도 굴러가므로 아무도 이상을 눈치채지 못하는데, 그것이 규칙 16
        ///    (*"전투 씬에서는 로비에서 확정한 맵 데이터만 사용해 맵을 구성한다"*)을 어기는
        ///    <b>가장 조용한 방식</b>이다. 최악의 경우 두 사람이 서로 다른 맵으로 싸우게 된다.
        ///    그래서 여기서는 <b>아무것도 새기지 않고 실패 로그만 남긴다</b> —
        ///    성·시작 광산이 배치되지 않으므로 "맵이 없다"는 사실이 화면에서도 곧바로 드러난다.
        ///    (지난 판 맵이 남는 일 자체는 MapHandoff.TryTake 의 「읽고 비운다」가 이미 막고 있다.)
        ///
        /// ⚠️ <b>전제</b>: 호출 전에 _grid 가 null 이 아니어야 한다.
        ///    현재 유일한 호출부인 <see cref="PrepareAndProjectMap"/> 가 앞에서 확인하고 부른다.
        /// </summary>
        private void ProjectHandedOverMap()
        {
            MapPreparationResult handedOver;

            if (!MapHandoff.TryTake(out handedOver))
            {
                // [운영/Error] 이 판은 진행할 수 없다(성·시작 광산을 배치하지 않는다).
                //
                // 🔴 왜 하필 MapPreparationFailed 키인가 — 키 선택의 근거를 남긴다.
                //    · 결과가 같다: 이 키의 정의가 "맵을 끝내 얻지 못해 **경기를 진행할 수 없다**
                //      (성·시작 광산을 배치하지 않는다)" 이고, 여기서 벌어지는 일이 정확히 그것이다.
                //    · MapProjectionFailed 는 쓰지 않는다: 그 키는 "맵은 있는데 격자에 못 새겼다"
                //      (격자 크기·헥스 방향 불일치)는 **설정 계통** 지표다. 여기서는 새길 맵 자체가
                //      없으므로 그 지표에 섞으면 "설정이 문제인가"를 영영 가릴 수 없게 된다.
                //    · 🔴 전송 결말 키 4종(MapTransferSucceeded / MapTransferFailed /
                //      MapHashMismatch / MapClientVerificationFailed)은 **절대 쓰지 않는다.**
                //      그 넷은 「한 번의 전송 회차는 그중 정확히 하나로 끝난다」는 배타 관계이고,
                //      NetworkMapTransfer 안에서 회차마다 한 줄만 나가도록 강제돼 있다.
                //      여기(전투 씬, 다른 객체)에서 같은 키를 한 줄 더 내보내면 그 배타성이
                //      깨지고 전송 성공/실패 집계가 조용히 망가진다.
                //
                // ⚠️ 필드에 ErrorCode 를 싣지 않는다 — MapPreparationErrorCode 는 "생성기·폴백
                //    템플릿이 왜 실패했나"의 코드라서, 생성 자체를 하지 않은 이 사건에 해당하는
                //    값이 없다. 없는 값을 억지로 끼워 넣으면 그 코드의 통계가 오염된다.
                //    대신 이 사건을 가리키는 고유 필드 Reason=MapHandoffEmpty 를 싣는다.
                GameLog.Ops.Error(LogEvent.MapPreparationFailed, MapLogSystem, nameof(GameBootstrapper),
                                  "멀티플레이인데 로비에서 확정된 맵이 전투 씬으로 인계되지 않았다 — " +
                                  "지난 판 맵이나 새로 만든 맵으로 대체하지 않고 아무것도 배치하지 않는다",
                                  "Reason=MapHandoffEmpty, NetworkMode=True" +
                                  ", GridWidth=" + _grid.Width +
                                  ", GridHeight=" + _grid.Height);
                return;
            }

            // 🔴 받은 결과도 반드시 보관한다. 싱글의 PrepareMap 과 같은 이유다 —
            //    문제가 생긴 판을 다시 만들어 보려면 그 판의 root seed·최종 바이트·해시가 있어야 한다.
            //    Client 는 맵을 만들지 않았으므로 이 값이 **그 판의 맵에 대한 유일한 기록**이다.
            _mapPreparation = handedOver;

            // 🔴 규칙 12 「로그 필수 항목」을 여기서 다시 내보내지 않는다.
            //    그 한 줄은 맵을 **확정한 자리**에서 이미 나갔다 — Host 는 로비의
            //    NetworkMapTransfer.BeginHostMapTransfer 에서, Client 는 같은 파일의
            //    전송 성공 결말(MapTransferSucceeded)에서. 여기서 또 내보내면 같은 판이
            //    두 번 세어진다(LogRules 1.14 금지 9 — 같은 사건을 두 줄로 남기지 않는다).

            ProjectMap(handedOver);
        }

        /// <summary>
        /// 이미 확정된 맵 설계도를 격자(_grid)에 실제로 새긴다(<see cref="MapProjectionUseCase"/>).
        /// <b>맵을 새로 만들지 않는다</b> — 받은 것을 그대로 쓴다.
        ///
        /// 이렇게 "받아서 새기기만" 하도록 갈라 둔 덕분에, 뒤 단계에서 Client 가
        /// <b>자기가 만들지 않은 맵</b>(Host 에게서 받은 맵)을 그대로 새길 수 있게 된다.
        ///
        /// ⚠️ <b>전제</b>: 호출 전에 _grid 가 null 이 아니어야 한다.
        ///    현재 유일한 호출부인 <see cref="PrepareAndProjectMap"/> 가 앞에서 확인하고 부른다.
        /// </summary>
        /// <param name="prepared">확정된 맵 준비 결과. 설계도(Definition)와 실패 로그 재료를 함께 들고 있다</param>
        private void ProjectMap(MapPreparationResult prepared)
        {
            // 설계도를 격자에 새긴다. 격자 크기(11x21)가 설계도와 다르면 여기서 실패한다.
            MapProjectionResult projected = MapProjectionUseCase.Project(prepared.Definition, _grid);
            _mapProjection = projected;

            if (!projected.IsSucceeded)
            {
                // [운영/Error] 크기·헥스 방향 불일치 등이다. GameConfig 의 격자 크기를 먼저 본다.
                //   맵 준비와는 별개의 사건이라 키를 나눴다(LogEvent.MapProjectionFailed 주석 참조).
                //   여기서는 11개 항목을 다시 싣지 않는다 — 바로 위에서 이미 한 줄로 남겼고,
                //   같은 값을 두 줄에 반복하면 집계에서 같은 판이 두 번 세어질 여지가 생긴다.
                //   이 줄에는 "어느 판인가"(MapVersion/Seed/Hash)와 "무엇과 안 맞았나"(격자 크기)만 싣는다.
                GameLog.Ops.Error(LogEvent.MapProjectionFailed, MapLogSystem, nameof(GameBootstrapper),
                                  "맵 투영 실패 — 성/광산을 배치하지 않는다: " +
                                  (projected.FailureReason ?? "사유 없음"),
                                  BuildMapProjectionLogData(prepared));
            }
        }

        // ====================================================================
        // 맵 로그의 구조화 필드 조립 (무작위 맵 2단계 K)
        //
        // LogRules 1.4: "| key=value, key=value" 부분은 사람이 읽으라고 붙인 꼬리표가 아니라
        // **서버로 보낼 때 그대로 구조화 필드가 되는 부분**이다. 그래서 아래 규약을 지킨다.
        //   · 키 이름과 값 표기를 그때그때 바꾸지 않는다(한 지표가 조용히 둘로 갈라진다).
        //   · 값에 구분자 ", " 를 넣지 않는다. 자유 문장은 message 쪽으로 보낸다.
        //   · 실수(float)를 값에 그대로 넣지 않는다 — 문화권에 따라 소수점이 ',' 가 되어
        //     기기마다 표기가 갈린다. 아래 값은 전부 정수·bool·enum 이름이라 이 문제가 없다.
        //
        // 🔴 이 헬퍼들에는 [Conditional] 을 붙이지 않는다(LogRules 1.14 금지 7).
        //    운영 로그의 재료이므로 릴리스 빌드에서도 살아 있어야 한다.
        // ====================================================================

        /// <summary>
        /// 맵 로그의 System 값. LogRules 1.4 는 System 을 "그 로그가 다루는 기능"으로 정하라고
        /// 규정한다. 맵 준비는 지형·광산을 **정하는** 기능이라 격자를 **그리는** HexGrid 와
        /// 다른 영역이므로 별도 값으로 둔다. 상수로 뽑아 둔 이유는 오타 하나로 같은 지표가
        /// 두 갈래로 갈라지는 것을 막기 위해서다.
        /// </summary>
        private const string MapLogSystem = "Map";

        /// <summary>
        /// 최종 맵 해시를 로그에 실을 때 쓰는 길이(16진수 자릿수).
        /// 원본은 32바이트(64자)라 그대로 실으면 로그 한 줄이 세 배로 길어진다.
        /// 앞 8바이트(16자)만 써도 "두 기기가 같은 맵을 받았는가"를 가리는 데는 충분하고,
        /// 이는 GameLog.HashId 가 UID 해시를 16자로 자르는 것과 같은 판단이다.
        /// 🔴 3단계의 Host/Client 해시 대조는 이 문자열이 아니라 **원본 32바이트**로 한다.
        ///    이 값은 사람이 읽고 집계하기 위한 지문일 뿐이다.
        /// </summary>
        private const int MapHashLogLength = 16;

        /// <summary>
        /// 규칙 12 가 요구하는 맵 준비 로그 항목을 "key=value, key=value" 로 조립한다.
        ///
        /// 담는 항목은 11가지다 — MapVersion · seed · 맵 유형 · 중립 광산 수 · 시작 광산 방향 ·
        /// 테스트 모드 표식 · 실제 초기 골드 · 생성 소요 시간 · 시도 횟수 · 폴백 사용 여부 ·
        /// 최종 맵 해시. 여기에 내부 error code 를 더해 12개 필드가 된다.
        /// 규칙 12 의 나머지 두 항목(전송/재전송 횟수 · Host/Client 해시 비교 결과)은
        /// 값이 3단계에서 처음 생기므로 **일부러 넣지 않는다**(항상 비는 필드가 되기 때문).
        /// </summary>
        /// <param name="prepared">맵 준비 결과(성공/실패 모두 받는다)</param>
        /// <returns>구조화 필드 문자열</returns>
        private static string BuildMapPreparationLogData(MapPreparationResult prepared)
        {
            // 실패했을 때도 같은 필드 집합을 낸다. 성공과 실패에서 필드가 달라지면
            // 서버 집계 쪽에서 "없는 필드"와 "값이 0인 필드"를 구분하는 처리가 또 필요해진다.
            return
                "MapVersion=" + prepared.MapVersion +
                ", Seed=" + prepared.RootSeed +
                ", MapType=" + prepared.MapType +
                ", NeutralMineCount=" + prepared.NeutralMineCount +
                ", StartingMineSide=" + prepared.StartingMineSide +
                // 🔴 2026-09-14 제거: 여기에 ", TestMode=" + prepared.MapTestModeEnabled 필드가 있었다.
                //    기록할 값이 없어졌다. ⚠️ LogEvent 키는 하나도 늘거나 줄지 않았다 —
                //    한 회차가 남기는 줄 수는 그대로이고 그 줄의 필드가 하나 빠질 뿐이다
                //    (키를 지우면 과거 로그와의 대조가 끊기므로 지우지 않는다).
                ", InitialGold=" + prepared.InitialGold +
                ", ElapsedMs=" + prepared.ElapsedMilliseconds +
                ", AttemptCount=" + prepared.AttemptCount +
                ", UsedFallback=" + prepared.UsedFallback +
                ", Hash=" + ToMapHashField(prepared.HashHex) +
                ", ErrorCode=" + prepared.ErrorCode;
        }

        /// <summary>
        /// 맵 투영 실패 로그의 구조화 필드를 조립한다.
        /// "어느 판의 맵인가"(MapVersion/Seed/Hash)와 "무엇과 안 맞았나"(격자 크기)만 담는다.
        /// </summary>
        /// <param name="prepared">방금 만들어진 맵의 준비 결과</param>
        /// <returns>구조화 필드 문자열</returns>
        private string BuildMapProjectionLogData(MapPreparationResult prepared)
        {
            // 격자 크기를 함께 싣는 이유: 이 실패의 가장 흔한 원인이 GameConfig 의 격자 크기와
            // 맵 정의 크기의 불일치이고, 그 두 숫자가 있어야 로그만 보고 바로 조치할 수 있다.
            // (_grid 는 이 메서드가 불리는 시점에 반드시 살아 있다 — PrepareAndProjectMap 가
            //  앞쪽에서 null 이면 이미 돌아갔다.)
            return
                "MapVersion=" + prepared.MapVersion +
                ", Seed=" + prepared.RootSeed +
                ", Hash=" + ToMapHashField(prepared.HashHex) +
                ", GridWidth=" + _grid.Width +
                ", GridHeight=" + _grid.Height;
        }

        /// <summary>
        /// 최종 맵 해시(16진수 64자)를 로그용 길이로 자른다.
        /// 값이 없으면 "-" 를 돌려준다 — 빈 값으로 두면 "key=" 뒤가 비어 파싱이 애매해진다.
        /// </summary>
        /// <param name="hashHex">최종 맵 해시의 16진수 문자열(없으면 null)</param>
        /// <returns>로그에 실을 해시 문자열</returns>
        private static string ToMapHashField(string hashHex)
        {
            if (string.IsNullOrEmpty(hashHex)) return "-";

            return hashHex.Length <= MapHashLogLength
                ? hashHex
                : hashHex.Substring(0, MapHashLogLength);
        }

        /// <summary>
        /// 이번 판의 64비트 root seed 를 정한다.
        ///
        /// 싱글플레이는 로컬이 권위이므로 매 판 새로 뽑는다(규칙 3).
        ///
        /// 🔴 <b>실제로 뽑는 계산은 이 파일에 없다.</b> <see cref="MapRootSeed.Create"/>(Domain)에 있다.
        ///    이유는 하나다 — 멀티플레이 Host 는 <b>로비에서</b> seed 를 뽑아야 하는데
        ///    (규칙 12 "Host 가 유일한 권위자"), 로비 씬에는 이 GameBootstrapper 가 없다.
        ///    그래서 같은 계산이 두 군데 필요해졌고, 복사해 두 벌로 만들면 언젠가 한쪽만
        ///    고쳐져 싱글과 멀티의 seed 생성 방식이 조용히 갈라진다. 한 곳에만 두고 양쪽이
        ///    그것을 불러 쓴다. (무작위 맵 3단계 I)
        ///
        /// ⚠️ 옮기면서 계산식은 한 글자도 바꾸지 않았다 — 「GUID 앞 8바이트 ^ UtcNow.Ticks」 그대로다.
        ///    즉 싱글플레이가 뽑는 값의 성질은 옮기기 전과 완전히 같다.
        /// </summary>
        /// <returns>이번 판의 root seed</returns>
        private ulong CreateRootSeed()
        {
            // ── [주석 비활성화] 멀티플레이 임시 고정 seed 분기 (무작위 맵 3단계 I) ──────
            //
            // 🔴 왜 지우지 않고 주석으로 남기는가:
            //    무작위 맵 3단계는 「멀티 경로 전체를 한 커밋으로 갈아끼우는」 작업이라
            //    문제가 생겼을 때 임시 고정 seed 경로로 되돌릴 수 있어야 한다.
            //    아래 두 줄의 주석을 푸는 것이 그 되돌리기의 전부가 되도록 남겨 둔다.
            //
            // 🔴 왜 지금 비활성화하는가 — <b>이 분기는 이미 죽은 코드다.</b>
            //    3단계에서 멀티의 seed 는 Host 가 **로비에서** 뽑아 맵을 통째로 Client 에게
            //    보낸다(NetworkMapTransfer). 그래서 멀티에서는 이 CreateRootSeed() 자체가
            //    호출되지 않는다 — Host 는 로비에서 이미 뽑았고, Client 는 아예 뽑지 않고
            //    받은 맵을 그대로 새기기만 한다(PrepareAndProjectMap 의 멀티 분기 참조).
            //    동작이 바뀌는 것이 아니라 **죽은 코드를 치우는 것**이다.
            //    그럼에도 굳이 치우는 이유는, 남겨 두면 다음 사람이 이 줄을 보고
            //    "멀티는 고정 seed 를 쓰는구나" 라고 **사실과 다르게** 읽기 때문이다.
            //
            // if (IsNetworkMode())
            //     return NetworkInterimRootSeed;

            // ── 싱글플레이: 매 판 다른 값 ───────────────────────────────────
            return MapRootSeed.Create();
        }
    }
}
