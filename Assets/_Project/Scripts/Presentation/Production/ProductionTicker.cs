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
//   2. 랠리 도착 → OnMoveComplete 콜백 → 적 Castle 방향 BFS 이동
//   3. Castle 근처 도착 → siege 목록에 등록
//   4. Update에서 주기적으로(1초) siege 유닛 검사:
//      - 이동 중이 아니고, 현재보다 Castle에 더 가까운 빈 타일이 있으면 이동
//      - Castle 인접 타일 도착 시 목록에서 제거
//   5. 유닛 사망 시 siege 목록에서 제거
//
// 랠리포인트 마커 표시 규칙:
//   - 랠리포인트 설정 직후 → 3초간 표시 → 자동 숨김
//   - 배럭 선택(팝업 열림) → 마커 표시
//   - 팝업 닫힘 / 다른 오브젝트 클릭 → 마커 숨김
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
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Application.Services;
using Hexiege.Infrastructure;
using Hexiege.Core;

namespace Hexiege.Presentation
{
    public class ProductionTicker : MonoBehaviour
    {
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
        // [2026-05-15] 혼잡도 기반 경로 분산 시스템 (v2).
        //
        //   _grid                   — CongestionAwarePathfinder가 walkable 여부를 확인할 때 사용.
        //   _congestionMap          — 타일별 혼잡도 누적/감쇠를 관리.
        //   _congestionPathfinder   — 혼잡도를 반영한 A* 경로 탐색기.
        //   _decayTimer             — DecayInterval 주기로 _congestionMap.Decay()를 호출하기 위한 누적 타이머.
        //   _buildingChangeSubs     — 건물 배치/파괴 이벤트 구독 해제용. siege 유닛 경로 재계산을 트리거.
        //
        // 혼잡도 튜닝 값(DecayInterval / CongestionWeight)은 별도 ScriptableObject가 아닌
        // GameConfig(_config.CongestionDecayInterval / _config.CongestionWeight)에서 직접 읽는다.
        // ────────────────────────────────────────────────────────────────────
        private HexGrid _grid;
        private CongestionMap _congestionMap;
        private CongestionAwarePathfinder _congestionPathfinder;
        private float _decayTimer;
        private CompositeDisposable _buildingChangeSubs;

        // ====================================================================
        // 랠리포인트 마커 관리
        // ====================================================================

        /// <summary> 배럭 Id → 마커 GameObject. </summary>
        private readonly Dictionary<int, GameObject> _rallyMarkers = new Dictionary<int, GameObject>();

        /// <summary> 3초 자동 숨김 코루틴 참조. </summary>
        private Coroutine _autoHideCoroutine;

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

            // 혼잡도 시스템 의존성 (v2). 모두 null이어도 SubscribeEvents 이후 정상 동작 — 폴백 경로가 동작.
            _grid = grid;
            _congestionMap = congestionMap;
            _congestionPathfinder = congestionPathfinder;
            _decayTimer = 0f;

            SubscribeEvents();
        }

        /// <summary>
        /// 이벤트 구독.
        /// </summary>
        private void SubscribeEvents()
        {
            // 생산 완료 → 랠리포인트 자동 이동
            GameEvents.OnUnitProduced
                .Subscribe(OnUnitProduced)
                .AddTo(this);

            // 건물 배치 → 배럭이면 등록
            GameEvents.OnBuildingPlaced
                .Subscribe(OnBuildingPlaced)
                .AddTo(this);

            // 건물 업그레이드 → 기존 ProductionState 제거 후 새 건물로 재등록
            // (2/3단계 건물도 랠리포인트·마커가 정상 동작하도록)
            GameEvents.OnBuildingUpgraded
                .Subscribe(OnBuildingUpgraded)
                .AddTo(this);

            // 사망 이벤트는 분리된 두 채널을 각각 구독한다.
            //   OnBuildingDied  → 생산건물(배럭)이라면 ProductionState 해제 + 랠리 마커 제거
            //   OnUnitDied      → siege 목록(_siegeUnits)에서 해당 유닛 제거
            // 강타입 DTO 분리(BuildingDiedEvent/UnitDiedEvent) 덕분에 핸들러도 두 개로 깔끔히 나뉜다.
            GameEvents.OnBuildingDied
                .Subscribe(OnBuildingDied)
                .AddTo(this);

            GameEvents.OnUnitDied
                .Subscribe(OnUnitDied)
                .AddTo(this);

            // 랠리포인트 변경 → 마커 생성/이동/제거
            GameEvents.OnRallyPointChanged
                .Subscribe(OnRallyPointChanged)
                .AddTo(this);

            // ────────────────────────────────────────────────────────────────
            // [2026-05-15] 혼잡도 시스템 — 건물 배치/파괴로 walkable이 바뀌면
            // siege 유닛들이 들고 있던 경로가 부적합해질 수 있으므로 즉시 새 path를 발급해
            // 다음 코루틴 사이클에 반영되도록 트리거한다.
            //
            // 기존 GameBootstrapper.SetupEagerRepathOnBuildingChanges()는
            // 살아있는 모든 유닛에 OnPathInvalidated()를 호출해 UnitView가 새 path를 받게 한다.
            // 본 핸들러는 siege 등록 상태 자체에 추가로 영향을 주진 않지만, 향후 v2 전용 후처리
            // (예: 혼잡도 부분 리셋)를 둘 위치로 활용하기 위해 별도로 둔다.
            // ────────────────────────────────────────────────────────────────
            _buildingChangeSubs?.Dispose();
            _buildingChangeSubs = new CompositeDisposable();

            GameEvents.OnBuildingPlaced
                .Subscribe(_ => OnWalkableChanged())
                .AddTo(_buildingChangeSubs);

            // 건물 사망만 walkable에 영향을 주므로 OnBuildingDied만 구독한다.
            // (유닛 사망은 walkable과 무관하여 OnUnitDied 구독 불필요.)
            // 건물 전용 강타입 이벤트라 BuildingData 캐스트 분기가 필요 없다.
            GameEvents.OnBuildingDied
                .Subscribe(_ => OnWalkableChanged())
                .AddTo(_buildingChangeSubs);
        }

        /// <summary>
        /// 건물 배치 또는 파괴로 walkable이 변했을 때 호출되는 후처리 훅.
        /// 현재는 v2 전용 추가 처리가 없지만, 향후 혼잡도 부분 리셋이나
        /// siege 경로 즉시 재발급 등을 둘 위치로 예약해 둔다.
        /// </summary>
        private void OnWalkableChanged()
        {
            // 향후 확장 지점. 현재는 GameBootstrapper.RepathAllAliveUnits가
            // UnitView.OnPathInvalidated()를 통해 모든 유닛 경로를 다시 발급하므로 별도 작업 불요.
        }

        /// <summary>
        /// 컴포넌트 파괴 시 직접 관리하는 CompositeDisposable을 명시적으로 해제한다.
        /// AddTo(this)로 묶인 구독은 자동 해제되지만, _buildingChangeSubs는 별도이므로 수동 처리.
        /// </summary>
        private void OnDestroy()
        {
            _buildingChangeSubs?.Dispose();
            _buildingChangeSubs = null;
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
                _resourceUseCase.TickIncome(dt, _buildingPlacement,
                    _config.MiningGoldPerSecond, _config.BaseGoldPerSecond);
            }

            // ────────────────────────────────────────────────────────────────
            // [2026-05-15] 혼잡도 감쇠 — 설정된 주기(CongestionDecayInterval)마다 한 번씩
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
            if (unitView == null || unitView.IsMoving) return;

            if (e.RallyPoint.HasValue)
            {
                // 랠리포인트로 경로 시도
                HexCoord rallyTarget = e.RallyPoint.Value;
                List<HexCoord> path = _unitMovement.RequestMove(e.Unit, rallyTarget);

                // 랠리포인트 타일이 점유 중이면 BFS로 가장 가까운 빈 타일 탐색
                if (path == null)
                    path = FindPathToNearestEmptyTile(e.Unit, rallyTarget);

                if (path != null)
                {
                    // 랠리 도착 후 → 적 Castle 방향 이동 콜백 등록
                    unitView.OnMoveComplete = () => MoveTowardEnemyCastle(e.Unit, unitView);
                    unitView.MoveTo(path);
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
            // 멀티플레이 중이면 로컬 플레이어의 팀에 해당하는 이벤트만 처리한다.
            // 호스트(서버) = Blue팀, 클라이언트(비서버) = Red팀.
            // 상대 팀 배럭의 랠리포인트 변경 이벤트는 깃발 표시 없이 건너뛴다.
            // 싱글플레이에서는 NetworkContext.IsNetworkActive=false이므로 이 블록을 건너뛰어 기존과 동일하게 동작한다.
            // Application 레이어의 NetworkContext 정적 홀더로 Unity.Netcode 의존성 제거.
            if (NetworkContext.IsNetworkActive)
            {
                TeamId localTeam = NetworkContext.IsNetworkServer ? TeamId.Blue : TeamId.Red;
                if (e.Team != localTeam) return;
            }

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
            if (_rallyMarkers.TryGetValue(barracksId, out var marker))
            {
                Destroy(marker);
                _rallyMarkers.Remove(barracksId);
            }
        }

        /// <summary>
        /// 랠리포인트 설정 직후 3초간 마커 표시 후 자동 숨김.
        /// </summary>
        private void ShowMarkerTemporary(int barracksId)
        {
            if (!_rallyMarkers.TryGetValue(barracksId, out var marker)) return;

            // 기존 자동 숨김 코루틴 취소
            if (_autoHideCoroutine != null)
                StopCoroutine(_autoHideCoroutine);

            marker.SetActive(true);
            _autoHideCoroutine = StartCoroutine(AutoHideMarker(barracksId));
        }

        /// <summary>
        /// 3초 후 마커 자동 숨김 코루틴.
        /// </summary>
        private IEnumerator AutoHideMarker(int barracksId)
        {
            yield return new WaitForSeconds(RallyMarkerShowDuration);

            if (_rallyMarkers.TryGetValue(barracksId, out var marker))
                marker.SetActive(false);

            _autoHideCoroutine = null;
        }

        /// <summary>
        /// 특정 배럭의 마커 표시. ProductionPanelUI에서 배럭 선택 시 호출.
        /// </summary>
        public void ShowRallyMarker(int barracksId)
        {
            // 기존 자동 숨김 코루틴 취소 (팝업이 열려있는 동안은 숨기지 않음)
            if (_autoHideCoroutine != null)
            {
                StopCoroutine(_autoHideCoroutine);
                _autoHideCoroutine = null;
            }

            if (_rallyMarkers.TryGetValue(barracksId, out var marker))
                marker.SetActive(true);
        }

        /// <summary>
        /// 모든 마커 숨김. ProductionPanelUI에서 팝업 닫힐 때 호출.
        /// </summary>
        public void HideAllRallyMarkers()
        {
            // 자동 숨김 코루틴도 취소
            if (_autoHideCoroutine != null)
            {
                StopCoroutine(_autoHideCoroutine);
                _autoHideCoroutine = null;
            }

            foreach (var marker in _rallyMarkers.Values)
            {
                if (marker != null)
                    marker.SetActive(false);
            }
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
        /// [2026-05-15] v1(CastleApproachManager)에서 v2(CongestionAwarePathfinder)로 교체.
        ///   - v1: 인접 6타일을 카운트해 가장 덜 배정된 타일을 유닛별로 다르게 부여.
        ///   - v2: 같은 목적지(=성 좌표)를 쓰되, 경로 비용 = 1 + 혼잡도 × CongestionWeight 적용.
        ///        유닛이 진입한 타일에 혼잡도가 누적되어 후속 유닛이 자연스럽게 우회한다.
        /// 폴백: v2 경로 계산 실패 시 기존 FindPathToNearestEmptyTile(BFS) 사용.
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
            // v2: 혼잡도 인식 경로 우선 시도.
            //   - _congestionPathfinder / _congestionMap / _config가 모두 와이어링된 경우에만 시도.
            //   - 결과 path가 비어 있거나 null이면 v1 폴백(FindPathToNearestEmptyTile)로 진입.
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
                // 이동 완료 후 siege 등록 콜백 — 성 좌표만 저장(v1의 ApproachTile은 폐기).
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
        /// </summary>
        /// <remarks>
        /// [2026-05-15] v1의 approachTile 파라미터 제거 — 모든 유닛이 성 좌표를 목적지로 공유한다.
        /// 분산은 CongestionMap이 경로 단계에서 처리한다.
        /// </remarks>
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
                if (unitView.IsMoving) continue;

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

                // [2026-05-15] v1의 ApproachTile은 폐기되어 모든 유닛이 성 좌표를 이동 목표로 사용한다.
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
    }
}
