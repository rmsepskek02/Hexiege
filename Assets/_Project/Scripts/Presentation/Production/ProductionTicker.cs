// ============================================================================
// ProductionTicker.cs
// 매 프레임 생산 타이머를 진행시키고, 채굴소 수입을 처리하는 브릿지 컴포넌트.
//
// 역할:
//   1. UnitProductionUseCase.Tick(dt) 호출 → 생산 타이머 진행
//   2. ResourceUseCase.TickIncome(dt) 호출 → 채굴소 골드 수입 처리
//   3. OnUnitProduced 이벤트 수신 → 생산된 유닛을 랠리포인트로 자동 이동
//   4. OnBuildingPlaced 이벤트 수신 → 배럭 등록
//   5. OnEntityDied 이벤트 수신 → 배럭 파괴 시 해제 + 마커 제거
//   6. OnRallyPointChanged 이벤트 수신 → 마커 생성/이동/제거
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

        // ====================================================================
        // 랠리포인트 마커 관리
        // ====================================================================

        /// <summary> 배럭 Id → 마커 GameObject. </summary>
        private readonly Dictionary<int, GameObject> _rallyMarkers = new Dictionary<int, GameObject>();

        /// <summary> 3초 자동 숨김 코루틴 참조. </summary>
        private Coroutine _autoHideCoroutine;

        /// <summary> 마커 위치 오프셋. 스프라이트가 타일 위에 자연스럽게 보이도록 조정. </summary>
        private static readonly Vector3 RallyMarkerOffset = new Vector3(0.05f, 0.15f, 0f);

        /// <summary> 랠리포인트 설정 후 자동 숨김까지 표시 시간. </summary>
        private const float RallyMarkerShowDuration = 3f;

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
            GameConfig config)
        {
            _productionUseCase = production;
            _resourceUseCase = resource;
            _unitMovement = unitMovement;
            _buildingPlacement = buildingPlacement;
            _unitFactory = unitFactory;
            _config = config;

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

            // 엔티티 사망 → 배럭 파괴 시 해제 + 마커 제거
            GameEvents.OnEntityDied
                .Subscribe(OnEntityDied)
                .AddTo(this);

            // 랠리포인트 변경 → 마커 생성/이동/제거
            GameEvents.OnRallyPointChanged
                .Subscribe(OnRallyPointChanged)
                .AddTo(this);
        }

        // ====================================================================
        // 매 프레임 업데이트
        // ====================================================================

        private void Update()
        {
            float dt = Time.deltaTime;

            // 생산 타이머 진행
            _productionUseCase?.Tick(dt);

            // 채굴소 수입 처리
            if (_resourceUseCase != null && _buildingPlacement != null && _config != null)
            {
                _resourceUseCase.TickIncome(dt, _buildingPlacement, _config.MiningGoldPerSecond);
            }
        }

        // ====================================================================
        // 이벤트 핸들러
        // ====================================================================

        /// <summary>
        /// 유닛 생산 완료 시 랠리포인트가 있으면 자동 이동.
        /// 랠리포인트 타일이 점유 중이면 인접 이동 가능 타일을 탐색.
        /// </summary>
        private void OnUnitProduced(UnitProducedEvent e)
        {
            if (!e.RallyPoint.HasValue) return;
            if (_unitFactory == null || _unitMovement == null) return;

            // UnitFactory에서 생성된 GameObject의 UnitView 찾기
            var unitObj = _unitFactory.GetUnitObject(e.Unit.Id);
            if (unitObj == null) return;

            var unitView = unitObj.GetComponent<UnitView>();
            if (unitView == null || unitView.IsMoving) return;

            // 랠리포인트로 경로 시도
            HexCoord rallyTarget = e.RallyPoint.Value;
            List<HexCoord> path = _unitMovement.RequestMove(e.Unit, rallyTarget);

            // 랠리포인트 타일이 점유 중이면 BFS로 가장 가까운 빈 타일 탐색
            if (path == null)
            {
                path = FindPathToNearestEmptyTile(e.Unit, rallyTarget);
            }

            if (path != null)
            {
                unitView.MoveTo(path);
            }
        }

        /// <summary>
        /// 랠리포인트 변경 시 마커 생성/이동/제거.
        /// </summary>
        private void OnRallyPointChanged(RallyPointChangedEvent e)
        {
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
        /// 건물 배치 시 배럭이면 ProductionState 등록.
        /// </summary>
        private void OnBuildingPlaced(BuildingPlacedEvent e)
        {
            if (_productionUseCase == null) return;

            if (e.Building.Type == BuildingType.Barracks)
            {
                _productionUseCase.RegisterBarracks(e.Building);
            }
        }

        /// <summary>
        /// 엔티티 사망 시 배럭이면 ProductionState 해제 + 마커 제거.
        /// </summary>
        private void OnEntityDied(EntityDiedEvent e)
        {
            if (_productionUseCase == null) return;

            // IDamageable이 BuildingData인지 확인
            if (e.Entity is BuildingData building && building.Type == BuildingType.Barracks)
            {
                _productionUseCase.UnregisterBarracks(building.Id);
                DestroyMarker(building.Id);
            }
        }

        // ====================================================================
        // 랠리포인트 마커 관리
        // ====================================================================

        /// <summary>
        /// 마커 생성 또는 기존 마커 위치 이동.
        /// </summary>
        private void CreateOrMoveMarker(int barracksId, HexCoord coord)
        {
            Vector3 worldPos = HexMetrics.HexToWorld(coord) + RallyMarkerOffset;

            if (_rallyMarkers.TryGetValue(barracksId, out var existing))
            {
                // 기존 마커 위치 이동
                existing.transform.position = worldPos;
                return;
            }

            // 새 마커 생성
            if (_config == null || _config.RallyPointPrefab == null) return;

            GameObject marker = Instantiate(_config.RallyPointPrefab, worldPos, Quaternion.identity);

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
