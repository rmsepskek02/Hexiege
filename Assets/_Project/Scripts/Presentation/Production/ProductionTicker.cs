// ============================================================================
// ProductionTicker.cs
// 매 프레임 생산 타이머를 진행시키고, 채굴소 수입을 처리하는 브릿지 컴포넌트.
//
// 역할:
//   1. UnitProductionUseCase.Tick(dt) 호출 → 생산 타이머 진행
//   2. ResourceUseCase.TickIncome(dt) 호출 → 채굴소 골드 수입 처리
//   3. OnUnitProduced 이벤트 수신 → 생산된 유닛을 랠리포인트로 자동 이동
//   4. OnBuildingPlaced 이벤트 수신 → 배럭 등록
//   5. OnBuildingDied / OnUnitDied 이벤트 수신 → 배럭 파괴 시 해제 + 마커 제거 / siege 목록 정리
//   6. OnRallyPointChanged 이벤트 수신 → 마커 생성/이동/제거
//   7. Siege 시스템: 랠리→Castle 자동 이동 + 지속 접근 탐색
//
// Siege 시스템 흐름:
//   1. 유닛 생산 완료 → 랠리포인트로 이동
//      (랠리 칸에 갈 수 없으면 거리 3 이내의 가장 가까운 걸어갈 수 있는 칸으로,
//       그런 칸도 없으면 랠리를 건너뛰고 곧장 2번의 적 Castle 방향 이동으로)
//   2. 랠리 도착 → OnMoveComplete 콜백 → 적 Castle 방향 BFS 이동
//   3. Castle 근처 도착 → siege 목록에 등록
//   4. Update에서 주기적으로(1초) siege 유닛 검사:
//      - 이동 중이 아니고, 현재보다 Castle에 더 가까운 빈 타일이 있으면 이동
//      - Castle 인접 타일 도착 시 목록에서 제거
//   5. 유닛 사망 시 siege 목록에서 제거
//
// 랠리포인트 마커 표시 규칙:
//   - 내 팀 배럭의 랠리포인트만 마커로 표시 (싱글플레이의 AI 깃발도 표시하지 않음)
//   - 랠리포인트 설정 직후 → 3초간 표시 → 자동 숨김 (3초 타이머는 배럭마다 따로 돈다)
//   - 배럭 선택(팝업 열림) → 그 배럭 마커만 표시 (다른 배럭 마커는 숨김, 모든 3초 타이머 중지)
//   - 팝업 닫힘 / 다른 오브젝트 클릭(맵 클릭) → 모든 마커 숨김
//   - 배럭 파괴 → 마커 Destroy
//   - 배럭 자신의 타일에 랠리포인트 설정 → 마커 Destroy (해제)
//
// 부착 위치: [Managers]/ProductionTicker
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, Update).
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Profiling;
#endif
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Application.Services;
using Hexiege.Infrastructure;
using Hexiege.Core;

namespace Hexiege.Presentation
{
    public class ProductionTicker : MonoBehaviour
    {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 고정 이름으로 동기 호출 구간만 측정한다. 릴리스에는 계측 참조·필드·호출이 남지 않는다.
        private static readonly ProfilerMarker _incomeTickProfilerMarker = new ProfilerMarker("Hexiege.Economy.TickIncome");
#endif
        // ====================================================================
        // 외부 의존성 (GameBootstrapper에서 주입)
        // ====================================================================

        private UnitProductionUseCase _productionUseCase;
        private ResourceUseCase _resourceUseCase;
        private UnitMovementUseCase _unitMovement;
        private BuildingPlacementUseCase _buildingPlacement;
        private UnitFactory _unitFactory;
        private GameConfig _config;

        // ────────────────────────────────────────────────────────────────────
        // 혼잡도 기반 경로 분산 시스템.
        //
        //   _grid                   — CongestionAwarePathfinder가 walkable 여부를 확인할 때 사용.
        //   _congestionMap          — 타일별 혼잡도 누적/감쇠를 관리.
        //   _congestionPathfinder   — 혼잡도를 반영한 A* 경로 탐색기.
        //   _decayTimer             — DecayInterval 주기로 _congestionMap.Decay()를 호출하기 위한 누적 타이머.
        //   _eventSubscriptions     — 맵 재초기화 때 전체 생산/건물/유닛 이벤트 구독을 교체한다.
        //
        // 혼잡도 튜닝 값(DecayInterval / CongestionWeight)은 별도 ScriptableObject가 아닌
        // GameConfig(_config.CongestionDecayInterval / _config.CongestionWeight)에서 직접 읽는다.
        // ────────────────────────────────────────────────────────────────────
        private HexGrid _grid;
        private CongestionMap _congestionMap;
        private CongestionAwarePathfinder _congestionPathfinder;
        private float _decayTimer;
        private readonly CompositeDisposable _eventSubscriptions = new CompositeDisposable();
        private bool _eventSubscriptionsBoundToLifetime;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private int _refactoringTickerBinds;
#endif

        // ====================================================================
        // 랠리포인트 마커 관리
        // ====================================================================

        /// <summary> 배럭 Id → 마커 GameObject. </summary>
        private readonly Dictionary<int, GameObject> _rallyMarkers = new Dictionary<int, GameObject>();


        /// <summary>
        /// 배럭 Id → 그 배럭 깃발의 「3초 뒤 자동 숨김」 코루틴.
        ///
        /// 왜 배럭마다 따로 두나요?
        ///   타이머가 하나뿐이면, 한 배럭의 타이머를 새로 시작할 때 다른 배럭의 타이머가 끊겨
        ///   그 배럭 깃발이 숨겨지지 않고 남는다. 배럭마다 따로 두면 서로 간섭하지 않는다.
        ///
        /// 항목 관리 규칙:
        ///   - 타이머가 끝까지 돌면 코루틴이 스스로 자기 항목을 지운다.
        ///   - 타이머를 도중에 멈출 때(StopCoroutine)는 코루틴의 끝부분이 실행되지 않으므로,
        ///     멈추는 쪽이 반드시 항목을 지운다(StopAutoHide / StopAllAutoHide).
        /// </summary>
        private readonly Dictionary<int, Coroutine> _autoHideByBarracks = new Dictionary<int, Coroutine>();

        /// <summary> 마커 위치 오프셋. GameConfig.RallyMarkerOffset으로 Inspector에서 조정. </summary>

        /// <summary> 랠리포인트 설정 후 자동 숨김까지 표시 시간. </summary>
        private const float RallyMarkerShowDuration = 3f;

        // ====================================================================
        // Siege 시스템 (Castle 접근 지속 탐색)
        // ====================================================================

        /// <summary>
        /// Siege 유닛 정보. Castle 근처에서 더 가까운 빈 타일을 지속 탐색.
        /// </summary>
        private class SiegeEntry
        {
            public int UnitId;
            public TeamId Team;
            public HexCoord CastlePos;
        }

        /// <summary> siege 대상 유닛 목록. unitId → SiegeEntry. </summary>
        private readonly Dictionary<int, SiegeEntry> _siegeUnits = new Dictionary<int, SiegeEntry>();

        /// <summary> siege 탐색 주기 (초). </summary>
        private const float SiegeCheckInterval = 1f;

        /// <summary> siege 탐색 타이머. </summary>
        private float _siegeTimer;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// GameBootstrapper에서 호출. UseCase 참조 설정 및 이벤트 구독.
        /// </summary>
        public void Initialize(
            UnitProductionUseCase production,
            ResourceUseCase resource,
            UnitMovementUseCase unitMovement,
            BuildingPlacementUseCase buildingPlacement,
            UnitFactory unitFactory,
            GameConfig config,
            HexGrid grid,
            CongestionMap congestionMap,
            CongestionAwarePathfinder congestionPathfinder)
        {
            _productionUseCase = production;
            _resourceUseCase = resource;
            _unitMovement = unitMovement;
            _buildingPlacement = buildingPlacement;
            _unitFactory = unitFactory;
            _config = config;

            // 혼잡도 시스템 의존성. 모두 null이어도 SubscribeEvents 이후 정상 동작 — 폴백 경로가 동작.
            _grid = grid;
            _congestionMap = congestionMap;
            _congestionPathfinder = congestionPathfinder;
            _decayTimer = 0f;

            SubscribeEvents();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_refactoringTickerBinds < int.MaxValue) _refactoringTickerBinds++;
            if (_refactoringTickerBinds <= 8)
                GameLog.Dev.Info("UI", nameof(ProductionTicker), "[REFACTOR-VERIFY]",
                    $"Generation=refactor-verify-v1, Case=UI, Status=EVIDENCE, Event=ticker-bound, Bind={_refactoringTickerBinds}, Subscriptions={_eventSubscriptions.Count}, Instance={GetInstanceID()}");
#endif
        }

        /// <summary>
        /// 이벤트 구독.
        /// </summary>
        private void SubscribeEvents()
        {
            // 컴포넌트 파괴 수명에는 한 번만 연결하고, 맵 재초기화 때 이전 구독을 전부 비운다.
            if (!_eventSubscriptionsBoundToLifetime)
            {
                _eventSubscriptions.AddTo(this);
                _eventSubscriptionsBoundToLifetime = true;
            }
            _eventSubscriptions.Clear();

            // 생산 완료 → 랠리포인트 자동 이동
            GameEvents.OnUnitProduced
                .Subscribe(OnUnitProduced)
                .AddTo(_eventSubscriptions);

            // 건물 배치 → 배럭이면 등록
            GameEvents.OnBuildingPlaced
                .Subscribe(OnBuildingPlaced)
                .AddTo(_eventSubscriptions);

            // 건물 업그레이드 → 기존 ProductionState 제거 후 새 건물로 재등록
            // (2/3단계 건물도 랠리포인트·마커가 정상 동작하도록)
            GameEvents.OnBuildingUpgraded
                .Subscribe(OnBuildingUpgraded)
                .AddTo(_eventSubscriptions);

            // 사망 이벤트는 분리된 두 채널을 각각 구독한다.
            //   OnBuildingDied  → 생산건물(배럭)이라면 ProductionState 해제 + 랠리 마커 제거
            //   OnUnitDied      → siege 목록(_siegeUnits)에서 해당 유닛 제거
            // 강타입 DTO 분리(BuildingDiedEvent/UnitDiedEvent) 덕분에 핸들러도 두 개로 깔끔히 나뉜다.
            GameEvents.OnBuildingDied
                .Subscribe(OnBuildingDied)
                .AddTo(_eventSubscriptions);

            GameEvents.OnUnitDied
                .Subscribe(OnUnitDied)
                .AddTo(_eventSubscriptions);

            // 랠리포인트 변경 → 마커 생성/이동/제거
            GameEvents.OnRallyPointChanged
                .Subscribe(OnRallyPointChanged)
                .AddTo(_eventSubscriptions);

            // ────────────────────────────────────────────────────────────────
            // 혼잡도 시스템 — 건물 배치/파괴로 walkable이 바뀌면
            // siege 유닛들이 들고 있던 경로가 부적합해질 수 있으므로 즉시 새 path를 발급해
            // 다음 코루틴 사이클에 반영되도록 트리거한다.
            //
            // GameBootstrapper.SetupEagerRepathOnBuildingChanges()는
            // 살아있는 모든 유닛에 OnPathInvalidated()를 호출해 UnitView가 새 path를 받게 한다.
            // 본 핸들러는 siege 등록 상태 자체에 추가로 영향을 주진 않지만, 향후 혼잡도 부분 리셋 등의
            // 후처리를 둘 위치로 활용하기 위해 별도로 둔다.
            // ────────────────────────────────────────────────────────────────
            GameEvents.OnBuildingPlaced
                .Subscribe(_ => OnWalkableChanged())
                .AddTo(_eventSubscriptions);

            // 건물 사망만 walkable에 영향을 주므로 OnBuildingDied만 구독한다.
            // (유닛 사망은 walkable과 무관하여 OnUnitDied 구독 불필요.)
            // 건물 전용 강타입 이벤트라 BuildingData 캐스트 분기가 필요 없다.
            GameEvents.OnBuildingDied
                .Subscribe(_ => OnWalkableChanged())
                .AddTo(_eventSubscriptions);
        }

        /// <summary>
        /// 건물 배치 또는 파괴로 walkable이 변했을 때 호출되는 후처리 훅.
        /// 현재는 추가 처리가 없지만, 향후 혼잡도 부분 리셋이나
        /// siege 경로 즉시 재발급 등을 둘 위치로 예약해 둔다.
        /// </summary>
        private void OnWalkableChanged()
        {
            // 향후 확장 지점. 현재는 GameBootstrapper.RepathAllAliveUnits가
            // UnitView.OnPathInvalidated()를 통해 모든 유닛 경로를 다시 발급하므로 별도 작업 불요.
        }

        /// <summary>
        /// 컴포넌트 파괴 시 전체 이벤트 구독을 명시적으로 해제한다.
        /// 컴포넌트 수명에 연결한 자동 해제와 겹쳐도 Dispose는 안전하게 한 번만 정리한다.
        /// </summary>
        private void OnDestroy()
        {
            _eventSubscriptions.Dispose();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameLog.Dev.Info("UI", nameof(ProductionTicker), "[REFACTOR-VERIFY]",
                $"Generation=refactor-verify-v1, Case=UI, Status=EVIDENCE, Event=ticker-disposed, Disposed={_eventSubscriptions.IsDisposed}, Instance={GetInstanceID()}");
#endif
        }

        // ====================================================================
        // 매 프레임 업데이트
        // ====================================================================

        private void Update()
        {
            float dt = Time.deltaTime;

            // 멀티플레이 모드에서는 서버만 생산 Tick과 수입 처리를 실행.
            // 클라이언트는 생산 타이머를 진행하면 서버와 상태가 어긋나므로 스킵.
            // 싱글플레이(NetworkContext.IsNetworkActive=false) 또는 서버이면 기존 로직 실행.
            // Application 레이어의 NetworkContext 정적 홀더를 통해 Unity.Netcode 의존성 제거.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
            {
                // 멀티플레이 클라이언트: UseCase Tick(생산 로직) 및 수입 Tick 생략
                // 단, 프로그레스 바 시각 갱신을 위해 클라이언트 로컬 ElapsedTime만 진행
                TickClientProgress(dt);

                // Siege 시스템은 클라이언트에서도 유닛 시각 이동을 처리해야 하므로 실행 유지
                TickSiege(dt);
                return;
            }

            // 싱글플레이 또는 서버: 기존 로직 그대로 실행

            // 생산 타이머 진행
            _productionUseCase?.Tick(dt);

            // 채굴소 수입 처리
            if (_resourceUseCase != null && _buildingPlacement != null && _config != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                using var incomeTickSample = _incomeTickProfilerMarker.Auto();
#endif
                _resourceUseCase.TickIncome(dt, _buildingPlacement,
                    _config.MiningGoldPerSecond, _config.BaseGoldPerSecond);
            }

            // ────────────────────────────────────────────────────────────────
            // 혼잡도 감쇠 — 설정된 주기(CongestionDecayInterval)마다 한 번씩
            // 모든 타일의 혼잡도를 1씩 감소시킨다. 0 이하는 Map에서 제거되어 자연스럽게 사라진다.
            // 서버(또는 싱글플레이)에서만 실행한다. _congestionMap이 주입되고 _config의 감쇠 간격이 0보다 큰 경우에만 동작.
            // ────────────────────────────────────────────────────────────────
            if (_congestionMap != null && _config != null && _config.CongestionDecayInterval > 0f)
            {
                _decayTimer += dt;
                if (_decayTimer >= _config.CongestionDecayInterval)
                {
                    _decayTimer = 0f;
                    _congestionMap.Decay();
                }
            }

            // Siege 유닛 주기적 탐색
            TickSiege(dt);
        }

        // ====================================================================
        // 이벤트 핸들러
        // ====================================================================

        /// <summary>
        /// 유닛 생산 완료 시 랠리포인트가 있으면 자동 이동.
        /// 랠리 도착 후 적 Castle 방향으로 자동 이동 (siege 체인).
        /// 랠리포인트가 없으면 바로 적 Castle 방향으로 이동.
        /// </summary>
        private void OnUnitProduced(UnitProducedEvent e)
        {
            if (_unitFactory == null || _unitMovement == null) return;

            var unitObj = _unitFactory.GetUnitObject(e.Unit.Id);
            if (unitObj == null) return;

            var unitView = unitObj.GetComponent<UnitView>();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // 이동 시작 전에 정확한 막힘 해제 유닛 하나에만 개발용 원시 관측을 연결한다.
            if (!NetworkContext.IsNetworkActive && unitView != null
                && ReferenceEquals(unitView.Data, e.Unit) && _productionUseCase != null
                && _productionUseCase.TryClaimBlockedSpawnFacingTrace(e.Unit, out int traceBarracks, out int traceAttempts))
                unitView.BeginBlockedSpawnFacingTrace(traceBarracks, traceAttempts);
#endif
            if (unitView == null || unitView.IsMoving) return;

            if (e.RallyPoint.HasValue)
            {

                // 랠리 칸(또는 그 근처의 걸어갈 수 있는 칸)까지의 경로를 고른다.
                //   경로가 있으면 → 그 경로로 걸어간 뒤, 도착하면 적 성으로 진군한다.
                //   현재 권위 타일을 후보로 골랐거나 주변 후보가 없으면 랠리를 생략한다.
                //   경로 공급 실패는 이 정책과 구별해 정상 완료로 연결하지 않는다.
                UnitRallyPathResult rally = ResolveRallyPath(e.Unit, e.RallyPoint.Value);
                if (rally.Status == UnitRallyPathStatus.PathFound)
                {
                    // 랠리 도착 후 → 적 Castle 방향 이동 콜백 등록
                    unitView.OnMoveComplete = () => MoveTowardEnemyCastle(e.Unit, unitView);
                    unitView.MoveToRally(new List<HexCoord>(rally.Request.Path), e.RallyPoint.Value);
                }
                else if (rally.EndsRally)
                {
                    MoveTowardEnemyCastle(e.Unit, unitView);
                }
            }
            else
            {
                // 랠리포인트 없으면 바로 적 Castle 방향 이동
                MoveTowardEnemyCastle(e.Unit, unitView);
            }
        }

        /// <summary>
        /// 랠리포인트 변경 시 마커 생성/이동/제거.
        /// </summary>
        private void OnRallyPointChanged(RallyPointChangedEvent e)
        {

            // 내 팀 배럭의 랠리포인트 변경만 깃발로 표시한다(상대 팀 깃발은 보이지 않는다 — 랠리포인트 규칙 1).
            //   - 멀티플레이: 호스트(서버) = Blue팀, 클라이언트(비서버) = Red팀. (옛 방식과 같은 식)
            //   - 싱글플레이: 플레이어는 항상 Blue, AI는 항상 Red다.
            //                 게임 종료 화면·HUD도 싱글에서 같은 「Blue 고정」 판정을 쓴다.
            //   이렇게 걸러도 AI 배럭의 랠리 좌표 자체는 생산 상태에 그대로 저장되므로,
            //   AI 유닛이 랠리로 이동하는 동작에는 영향이 없다(깃발 표시만 건너뛴다).
            //   Application 레이어의 NetworkContext 정적 홀더로 Unity.Netcode 의존성 없이 판별한다.
            TeamId localTeam = NetworkContext.IsNetworkActive
                ? (NetworkContext.IsNetworkServer ? TeamId.Blue : TeamId.Red)
                : TeamId.Blue;
            if (e.Team != localTeam) return;

            if (e.Coord.HasValue)
            {
                // 마커 생성 또는 이동
                CreateOrMoveMarker(e.BarracksId, e.Coord.Value);

                // 3초간 표시 후 자동 숨김
                ShowMarkerTemporary(e.BarracksId);
            }
            else
            {
                // 랠리포인트 해제 → 마커 파괴
                DestroyMarker(e.BarracksId);
            }
        }

        /// <summary>
        /// 건물 배치 시 생산건물이면 ProductionState 등록.
        /// 종족·라인·단계와 무관하게 BuildingTypeHelper.IsProductionBuilding으로 인식한다.
        /// </summary>
        private void OnBuildingPlaced(BuildingPlacedEvent e)
        {
            if (_productionUseCase == null) return;

            if (BuildingTypeHelper.IsProductionBuilding(e.Building.Type))
            {
                _productionUseCase.RegisterBarracks(e.Building);
            }
        }

        /// <summary>
        /// 건물 업그레이드 시 호출.
        /// 기존 BuildingId(업그레이드 이전)의 ProductionState를 제거하고,
        /// 새 BuildingData(업그레이드 이후)로 재등록한다.
        /// 이렇게 하지 않으면 업그레이드된 2/3단계 건물은 _productionUseCase._states에
        /// 등록되지 않아 랠리포인트 설정 시 마커가 표시되지 않는다.
        /// </summary>
        private void OnBuildingUpgraded(BuildingUpgradedEvent e)
        {
            if (_productionUseCase == null) return;

            // 업그레이드된 건물이 생산건물인지 확인
            if (!BuildingTypeHelper.IsProductionBuilding(e.NewBuilding.Type)) return;

            // 업그레이드 전 랠리포인트 좌표를 먼저 저장해 둔다.
            // CancelAllQueue가 랠리포인트를 초기화하므로 반드시 그 전에 읽어야 한다.
            HexCoord? savedRallyPoint = _productionUseCase.GetState(e.OldBuildingId)?.RallyPoint;

            // 생산 중이거나 골드가 차감된 대기 항목을 환불하고 기존 상태를 제거한다.
            // (UnregisterBarracks 대신 CancelAllQueue를 사용 — 내부에서 UnregisterBarracks까지 수행)
            // 근거: GameSystemRules.md — 건물 철거 시스템 규칙 5
            _productionUseCase.CancelAllQueue(e.OldBuildingId);

            // 새 건물로 빈 생산 상태를 등록한다.
            _productionUseCase.RegisterBarracks(e.NewBuilding);

            // 저장해 둔 랠리포인트가 있으면 새 건물 상태에 복원한다.
            if (savedRallyPoint.HasValue)
                _productionUseCase.SetRallyPoint(e.NewBuilding.Id, savedRallyPoint.Value);
        }

        /// <summary>
        /// 건물 사망 시 생산건물이면 ProductionState를 해제하고 랠리 마커도 제거.
        /// 비생산 건물(Castle/MiningPost 등)이면 아무 동작도 하지 않는다.
        ///
        /// 사망 이벤트가 유닛/건물로 강타입 분리(OnUnitDied/OnBuildingDied)되어
        /// 한 메서드 안에서 is 캐스트로 분기할 필요 없이 핸들러 자체를 둘로 나누게 되었다.
        /// </summary>
        /// <param name="e">사망한 건물 정보가 담긴 이벤트.</param>
        private void OnBuildingDied(BuildingDiedEvent e)
        {
            if (_productionUseCase == null) return;
            if (e.Building == null) return;

            // 생산건물(배럭/2·3단계 생산건물)만 ProductionState/마커가 등록되어 있으므로
            // BuildingTypeHelper.IsProductionBuilding 으로 한 번 더 필터링.
            if (BuildingTypeHelper.IsProductionBuilding(e.Building.Type))
            {
                _productionUseCase.UnregisterBarracks(e.Building.Id);
                DestroyMarker(e.Building.Id);
            }
        }

        /// <summary>
        /// 유닛 사망 시 siege(공성) 자동 이동 목록에서 해당 유닛을 제거.
        /// _siegeUnits에 등록된 유닛이 아니면 Remove는 false를 반환하지만 부작용은 없다.
        /// </summary>
        /// <param name="e">사망한 유닛 정보가 담긴 이벤트.</param>
        private void OnUnitDied(UnitDiedEvent e)
        {
            if (e.Unit == null) return;
            _siegeUnits.Remove(e.Unit.Id);
        }

        // ====================================================================
        // 랠리포인트 마커 관리
        // ====================================================================

        /// <summary>
        /// 마커 생성 또는 기존 마커 위치 이동.
        /// </summary>
        private void CreateOrMoveMarker(int barracksId, HexCoord coord)
        {
            // 도메인 좌표 → 뷰 좌표 변환 (Red팀이면 맵 중심 기준 반전)
            Vector3 worldPos = ViewConverter.ToView(HexMetrics.HexToWorld(coord)) + _config.RallyMarkerOffset;
            Quaternion rotation = Quaternion.Euler(_config.RallyMarkerEuler);

            if (_rallyMarkers.TryGetValue(barracksId, out var existing))
            {
                // 기존 마커 위치/회전 갱신
                existing.transform.position = worldPos;
                existing.transform.rotation = rotation;
                return;
            }

            // 새 마커 생성
            if (_config == null || _config.RallyPointPrefab == null) return;

            GameObject marker = Instantiate(_config.RallyPointPrefab, worldPos, rotation);

            // 기본 숨김 상태 (ShowMarkerTemporary로 일시 표시)
            marker.SetActive(false);

            _rallyMarkers[barracksId] = marker;
        }

        /// <summary>
        /// 마커 파괴 (배럭 파괴 또는 랠리포인트 해제 시).
        /// </summary>
        private void DestroyMarker(int barracksId)
        {
            // 그 배럭의 3초 타이머가 돌고 있으면 먼저 멈춘다.
            //   (마커를 파괴한 뒤 타이머가 끝나 없는 마커를 찾는 일이 없도록, 사전 항목도 함께 지운다.)
            StopAutoHide(barracksId);

            if (_rallyMarkers.TryGetValue(barracksId, out var marker))
            {
                Destroy(marker);
                _rallyMarkers.Remove(barracksId);
            }
        }

        /// <summary>
        /// 랠리포인트 설정 직후 3초간 마커 표시 후 자동 숨김.
        /// 이 배럭의 타이머만 처음부터 다시 시작한다. 다른 배럭의 마커와 타이머는 건드리지 않는다.
        /// </summary>
        private void ShowMarkerTemporary(int barracksId)
        {
            if (!_rallyMarkers.TryGetValue(barracksId, out var marker)) return;


            // 이 배럭의 이전 타이머만 멈춘다(같은 배럭에 연달아 지정하면 마지막 지정 시점부터 3초).
            StopAutoHide(barracksId);

            marker.SetActive(true);
            _autoHideByBarracks[barracksId] = StartCoroutine(AutoHideMarker(barracksId));
        }

        /// <summary>
        /// 3초 후 마커 자동 숨김 코루틴.
        /// 끝까지 돌면 사전에서 자기 항목을 스스로 지운다.
        /// </summary>
        private IEnumerator AutoHideMarker(int barracksId)
        {
            yield return new WaitForSeconds(RallyMarkerShowDuration);

            if (_rallyMarkers.TryGetValue(barracksId, out var marker) && marker != null)
                marker.SetActive(false);


            // 같은 배럭에 새 타이머를 걸 때는 항상 이전 타이머를 먼저 멈추므로(ShowMarkerTemporary),
            //   여기까지 끝까지 돈 코루틴은 언제나 「사전에 등록된 현재 타이머」다. 안심하고 지운다.
            _autoHideByBarracks.Remove(barracksId);
        }

        /// <summary>
        /// 특정 배럭의 마커 표시. ProductionPanelUI에서 배럭 선택(팝업 열림) 시 호출.
        /// 팝업이 열려 있는 동안은 자동으로 숨기지 않도록 모든 배럭의 3초 타이머를 멈추고,
        /// 다른 배럭의 마커는 모두 끈 뒤 이 배럭의 마커만 켠다(열린 배럭의 깃발만 보이게).
        /// </summary>
        /// <param name="barracksId">팝업이 열린 생산 건물의 Id</param>
        public void ShowRallyMarker(int barracksId)
        {

            // 모든 배럭의 3초 타이머를 멈춘다(자기 것 포함 — 팝업이 열려 있는 동안은 숨기지 않는다).
            StopAllAutoHide();

            // 다른 배럭의 마커를 모두 끈다 → 앞서 다른 배럭에서 꽂은 깃발이 남지 않는다.
            foreach (var pair in _rallyMarkers)
            {
                if (pair.Key != barracksId && pair.Value != null)
                    pair.Value.SetActive(false);
            }

            if (_rallyMarkers.TryGetValue(barracksId, out var marker) && marker != null)
                marker.SetActive(true);
        }

        /// <summary>
        /// 모든 마커 숨김. ProductionPanelUI에서 팝업 닫힐 때, 그리고 맵을 클릭할 때 호출.
        /// 돌고 있던 모든 배럭의 3초 타이머도 함께 멈춘다.
        /// </summary>
        public void HideAllRallyMarkers()
        {

            // 모든 배럭의 자동 숨김 타이머 취소 (어차피 지금 모두 숨기므로 더 기다릴 필요가 없다)
            StopAllAutoHide();

            foreach (var marker in _rallyMarkers.Values)
            {
                if (marker != null)
                    marker.SetActive(false);
            }
        }

        /// <summary>
        /// 한 배럭의 3초 자동 숨김 타이머를 멈추고 사전에서 지운다. 타이머가 없으면 아무 일도 하지 않는다.
        /// StopCoroutine으로 멈춘 코루틴은 끝부분(자기 항목 지우기)을 실행하지 않으므로 여기서 직접 지운다.
        /// </summary>
        /// <param name="barracksId">타이머를 멈출 생산 건물의 Id</param>
        private void StopAutoHide(int barracksId)
        {
            if (_autoHideByBarracks.TryGetValue(barracksId, out var running))
            {
                if (running != null)
                    StopCoroutine(running);
                _autoHideByBarracks.Remove(barracksId);
            }
        }

        /// <summary>
        /// 모든 배럭의 3초 자동 숨김 타이머를 멈추고 사전을 비운다.
        /// </summary>
        private void StopAllAutoHide()
        {
            foreach (var running in _autoHideByBarracks.Values)
            {
                if (running != null)
                    StopCoroutine(running);
            }
            _autoHideByBarracks.Clear();
        }

        // ====================================================================
        // 클라이언트 프로그레스 바 시뮬레이션
        // ====================================================================

        /// <summary>
        /// 멀티플레이 클라이언트 전용: ProductionState의 ElapsedTime만 진행.
        /// 실제 생산 로직(스폰, 큐 처리)은 서버가 담당하고,
        /// 클라이언트는 프로그레스 바 시각 갱신 목적으로만 타이머를 진행.
        /// RequiredTime 도달 시 캡 처리 (서버 SpawnUnitClientRpc가 리셋함).
        /// </summary>
        private void TickClientProgress(float dt)
        {
            if (_productionUseCase == null) return;

            // 전체 ProductionState를 순회하여 CurrentProducing이 있는 상태의 타이머 진행
            // GetAllStates가 없으므로 UnitProductionUseCase에 접근자 추가 필요
            // → 대안: _productionUseCase.TickProgressOnly(dt) 메서드 추가
            _productionUseCase.TickProgressOnly(dt);
        }

        // ====================================================================
        // Siege 시스템
        // ====================================================================

        /// <summary>
        /// 유닛을 적 Castle 방향으로 이동시키고 siege 목록에 등록.
        /// 랠리포인트 도착 콜백 또는 랠리 미설정 시 직접 호출.
        /// </summary>
        /// <remarks>
        /// 모든 유닛이 같은 목적지(=성 좌표)를 쓰되, 경로 비용 = 1 + 혼잡도 × CongestionWeight 적용.
        /// 유닛이 진입한 타일에 혼잡도가 누적되어 후속 유닛이 자연스럽게 우회한다.
        /// 폴백: 혼잡도 경로 계산 실패 시 FindPathToNearestEmptyTile(BFS) 사용.
        /// </remarks>
        private void MoveTowardEnemyCastle(UnitData unit, UnitView unitView)
        {
            if (!unit.IsAlive || _buildingPlacement == null) return;

            // 적 Castle 위치 찾기 — 모든 유닛이 같은 좌표를 목적지로 사용한다.
            // 분산은 경로 단계의 혼잡도 가중치로 달성한다.
            HexCoord? enemyCastle = FindEnemyCastlePos(unit.Team);
            if (!enemyCastle.HasValue) return;

            HexCoord moveTarget = enemyCastle.Value;

            // ────────────────────────────────────────────────────────────
            // 혼잡도 인식 경로 우선 시도.
            //   - _congestionPathfinder / _congestionMap / _config가 모두 와이어링된 경우에만 시도.
            //   - 결과 path가 비어 있거나 null이면 폴백(FindPathToNearestEmptyTile)로 진입.
            //   - 혼잡도 가중치는 _config.CongestionWeight(GameConfig)에서 읽는다.
            // ────────────────────────────────────────────────────────────
            List<HexCoord> path = null;
            if (_congestionPathfinder != null && _congestionMap != null && _config != null
                && _unitMovement != null)
            {
                List<HexCoord> congestionPath = _congestionPathfinder.FindPath(
                    unit.Position,
                    moveTarget,
                    _grid,
                    _congestionMap,
                    _config.CongestionWeight);

                if (congestionPath != null && congestionPath.Count >= 2)
                {
                    path = congestionPath;
                }
            }

            // 혼잡도 경로 실패 시 기존 BFS 폴백.
            if (path == null)
                path = FindPathToNearestEmptyTile(unit, moveTarget);

            if (path != null)
            {
                // 이동 완료 후 siege 등록 콜백 — 성 좌표만 저장.
                unitView.OnMoveComplete = () => RegisterSiege(unit, enemyCastle.Value);
                unitView.MoveTo(path);
            }
            else
            {
                // 경로 없어도 siege 등록 (추후 빈 타일 생기면 이동)
                RegisterSiege(unit, enemyCastle.Value);
            }
        }

        /// <summary>
        /// siege 목록에 유닛 등록.
        /// 모든 유닛이 성 좌표를 목적지로 공유하며, 분산은 CongestionMap이 경로 단계에서 처리한다.
        /// </summary>
        private void RegisterSiege(UnitData unit, HexCoord castlePos)
        {
            if (!unit.IsAlive) return;

            // Castle 인접 타일에 이미 도착했으면 등록하지 않음
            if (HexCoord.Distance(unit.Position, castlePos) <= 1)
                return;

            _siegeUnits[unit.Id] = new SiegeEntry
            {
                UnitId = unit.Id,
                Team = unit.Team,
                CastlePos = castlePos
            };
        }

        /// <summary>
        /// 주기적으로 siege 유닛들이 Castle에 더 가까운 빈 타일로 이동할 수 있는지 확인.
        /// </summary>
        private void TickSiege(float dt)
        {
            if (_siegeUnits.Count == 0 || _unitFactory == null || _unitMovement == null) return;

            _siegeTimer += dt;
            if (_siegeTimer < SiegeCheckInterval) return;
            _siegeTimer = 0f;

            // 순회 중 제거를 위해 키 복사
            var keys = new List<int>(_siegeUnits.Keys);

            foreach (int unitId in keys)
            {
                if (!_siegeUnits.TryGetValue(unitId, out var entry)) continue;

                var unitObj = _unitFactory.GetUnitObject(unitId);
                if (unitObj == null)
                {
                    _siegeUnits.Remove(unitId);
                    continue;
                }

                var unitView = unitObj.GetComponent<UnitView>();
                if (unitView == null || unitView.Data == null || !unitView.Data.IsAlive)
                {
                    _siegeUnits.Remove(unitId);
                    continue;
                }

                // 이동 중이면 스킵
                if (unitView.IsMoving || unitView.HasActiveNavigationObjective) continue;

                UnitData unit = unitView.Data;

                // siege 완료 판정은 항상 "성 본체와의 거리"를 기준으로 한다.
                // 접근 타일에 도달했더라도 성과 인접하지 않으면 계속 추격해야 함.
                int currentDistToCastle = HexCoord.Distance(unit.Position, entry.CastlePos);

                // Castle 인접 도착 → siege 완료
                if (currentDistToCastle <= 1)
                {
                    _siegeUnits.Remove(unitId);
                    continue;
                }

                // 모든 유닛이 성 좌표를 이동 목표로 사용한다.
                HexCoord moveTarget = entry.CastlePos;

                // "지금 위치에서 성까지의 거리" 기준으로 더 가까운 빈 타일로 재이동할지 결정한다.
                int currentDistToTarget = HexCoord.Distance(unit.Position, moveTarget);

                // 성 방향 BFS로 더 가까운 빈 타일 탐색
                List<HexCoord> path = FindPathToNearestEmptyTile(unit, moveTarget);
                if (path != null)
                {
                    // 새 경로의 도착점이 현재보다 성에 더 가까운지 확인
                    HexCoord destination = path[path.Count - 1];
                    int newDist = HexCoord.Distance(destination, moveTarget);

                    if (newDist < currentDistToTarget)
                    {
                        unitView.OnMoveComplete = () =>
                        {
                            // 도착 후 Castle 인접이면 siege 해제
                            if (unit.IsAlive && HexCoord.Distance(unit.Position, entry.CastlePos) <= 1)
                                _siegeUnits.Remove(unitId);
                        };
                        unitView.MoveTo(path);
                    }
                }
            }
        }

        /// <summary>
        /// 유닛 팀의 적 Castle 위치를 찾아 반환.
        /// </summary>
        private HexCoord? FindEnemyCastlePos(TeamId team)
        {
            if (_buildingPlacement == null) return null;

            foreach (var building in _buildingPlacement.Buildings.Values)
            {
                if (building.Type == BuildingType.Castle && building.IsAlive && building.Team != team)
                    return building.Position;
            }
            return null;
        }

        // ====================================================================
        // 유틸리티
        // ====================================================================

        /// <summary>
        /// BFS로 랠리포인트에서 가장 가까운 빈 타일을 탐색하여 경로 반환.
        /// Ring 0(랠리포인트 자체)부터 바깥으로 확산하며 이동 가능한 첫 타일을 찾음.
        /// </summary>
        /// <param name="unit">이동할 유닛</param>
        /// <param name="target">랠리포인트 좌표</param>
        /// <param name="maxRange">최대 탐색 범위 (타일 거리). 기본 3.</param>
        private List<HexCoord> FindPathToNearestEmptyTile(UnitData unit, HexCoord target, int maxRange = 3)
        {
            var visited = new HashSet<HexCoord>();
            var queue = new Queue<HexCoord>();

            queue.Enqueue(target);
            visited.Add(target);

            while (queue.Count > 0)
            {
                HexCoord current = queue.Dequeue();

                // 이 타일로 이동 가능한지 시도
                List<HexCoord> path = _unitMovement.RequestMove(unit, current);
                if (path != null)
                    return path;

                // 최대 범위 초과 시 이웃 확장하지 않음
                if (HexCoord.Distance(target, current) >= maxRange)
                    continue;

                // 6방향 이웃을 큐에 추가
                for (int i = 0; i < HexDirectionExtensions.Count; i++)
                {
                    HexCoord neighbor = ((HexDirection)i).Neighbor(current);
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return null; // 범위 내 빈 타일 없음
        }

        // ====================================================================
        // 랠리 경로 결정 (생산 직후 1회)
        // ====================================================================

        /// <summary>
        /// 생산된 유닛이 랠리포인트(집결 깃발)로 갈 때 실제로 걸어갈 경로를 고른다.
        ///
        /// 고르는 순서:
        ///   1. 랠리 칸 자체가 걸어갈 수 있는 칸이면 그 칸으로 가는 경로.
        ///      (이미 그 칸이면 현재 타일 선택 결과 — 생산 직후에는 곧장 적 성으로 간다.)
        ///   2. 랠리 칸에 갈 수 없으면(건물·금광이 있는 칸이거나 길이 막힘)
        ///      랠리 칸에서 거리 1 → 2 → 3 의 고리를 차례로 살펴, 걸어갈 수 있는 칸 중
        ///      경로가 가장 짧은 칸으로 가는 경로. 한 고리에서 찾으면 더 바깥 고리는 보지 않는다.
        ///      경로 길이가 같으면 먼저 살펴본 칸(방향 순서 고정)을 고른다 → 매번 같은 결과.
        ///   3. 그래도 없으면 후보 없음 결과 — 랠리를 건너뛰고 곧장 적 성으로 간다.
        ///
        /// 왜 「걸어갈 수 있는 칸」만 목적지로 삼나요?
        ///   이동 UseCase의 RequestMove는 목적지가 건물 칸처럼 이동 불가이면 경로 끝에 그 칸을 덧붙여 준다
        ///   (전투 접근용 계약이라 바꾸지 않는다). 그 경로를 랠리 이동에 쓰면 유닛이 건물 칸으로 들어가려다 멈춘다.
        ///   그래서 여기서는 목적지를 처음부터 걸어갈 수 있는 칸으로만 골라, 덧붙임이 일어나지 않게 한다.
        ///
        /// ⚠️ 바로 위 유틸리티 절의 BFS 빈 칸 탐색 메서드(공성 이동이 쓰는 것)는 일부러 재사용하지 않는다.
        ///    그 메서드는 이동 불가 칸에 대해서도 덧붙임 경로를 그대로 받아들이기 때문이다(공성 쪽은 변경하지 않음).
        /// </summary>
        /// <param name="unit">방금 생산된 유닛</param>
        /// <param name="rally">배럭에 지정된 랠리포인트 좌표</param>
        /// <returns>이동 경로, 현재 타일 선택, 후보 없음, 공급 실패를 구별하는 결과.</returns>
        private UnitRallyPathResult ResolveRallyPath(UnitData unit, HexCoord rally)
        {
            return _unitMovement.RequestRallyPath(unit, rally);
        }

    }
}
