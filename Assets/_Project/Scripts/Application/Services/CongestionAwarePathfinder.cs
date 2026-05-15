// ============================================================================
// CongestionAwarePathfinder.cs
// 혼잡도(CongestionMap)를 반영한 A* 경로 탐색기 (순수 C# 클래스).
//
// 무엇을 해결하는가:
//   기존 FlowField는 "같은 목적지면 같은 경로"를 산출하기 때문에 모든 유닛이 한 줄로
//   늘어서서 이동하는 문제가 발생한다. 이 클래스는 동일한 목적지여도 이미 다른 유닛이
//   많이 지나간 타일에 비용 페널티를 주어, 후속 유닛들이 자연스럽게 다른 길을 선택하게 한다.
//
// 핵심 공식:
//   타일 비용  = 1 (기본) + 혼잡도 × congestionWeight
//   휴리스틱   = HexCoord.Distance(현재, 목적지)  ← 정수 hex 거리(허용 가능, A* 최적 보장)
//   f-score    = g-score + 휴리스틱 (우선순위 큐 정렬 키)
//
//   congestionWeight가 클수록 혼잡 타일을 더 강하게 피하고,
//   0이면 일반 A*와 같아진다.
//
// non-walkable 목적지 처리:
//   목적지 타일이 성/건물처럼 walkable이 아닐 수도 있다. 이 경우 목적지 인접 6타일 중
//   walkable이면서 목적지에 가장 가까운 타일을 실제 도착점으로 잡아 경로를 계산한다.
//   (기존 FlowField가 마지막에 목적지를 덧붙이던 방식과 의미적으로 같다.)
//
// 우선순위 큐:
//   .NET 6 이상에서는 PriorityQueue<TElement, TPriority>가 표준이지만, Unity 환경에서는
//   사용 가능 여부가 버전에 따라 차이가 있다. 안전을 위해 작은 SortedSet 기반 큐를 직접 구현한다.
//   동률(같은 f-score) 처리를 위해 tie-breaker로 삽입 순번을 더해 결정성을 보장한다.
//
// 레이어:
//   Application — UnityEngine 의존 없음. Domain의 HexCoord/HexGrid/HexTile/HexDirection만 사용.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application.Services
{
    /// <summary>
    /// 혼잡도 페널티를 반영한 A* 경로 탐색기.
    /// 외부에서 <see cref="FindPath"/> 한 번 호출하면 시작점부터 목적지까지의 좌표 리스트를 돌려준다.
    /// </summary>
    public class CongestionAwarePathfinder
    {
        // ────────────────────────────────────────────────────────────────────
        // 내부 자료구조 — 노드별 g-score, parent, 개방 상태를 보관한다.
        // 노드 수가 많지 않으므로(맵 7×17=119) 단순 Dictionary로 충분하다.
        // ────────────────────────────────────────────────────────────────────

        // 시작점에서 해당 노드까지의 누적 비용(g-score).
        private readonly Dictionary<HexCoord, float> _gScore = new Dictionary<HexCoord, float>();

        // 경로 재구성을 위한 부모 노드 추적.
        private readonly Dictionary<HexCoord, HexCoord> _cameFrom = new Dictionary<HexCoord, HexCoord>();

        // 이미 확정 처리된(닫힌) 노드 집합 — 중복 평가를 막는다.
        private readonly HashSet<HexCoord> _closed = new HashSet<HexCoord>();

        // 오픈 셋(열려 있는 노드들). SortedSet으로 우선순위 큐를 흉내낸다.
        // 정렬 기준은 OpenNode.CompareTo가 정의한다.
        private readonly SortedSet<OpenNode> _open = new SortedSet<OpenNode>();

        // 같은 노드를 중복 추가하지 않기 위한 검색용 보조 인덱스.
        // SortedSet은 같은 좌표여도 fScore가 다르면 별개 원소로 들어가므로 별도 추적이 필요.
        private readonly Dictionary<HexCoord, OpenNode> _openLookup = new Dictionary<HexCoord, OpenNode>();

        // 삽입 순번 — 동률(같은 fScore) 시 결정적 순서를 보장하는 tie-breaker.
        private int _insertOrder;

        /// <summary>
        /// 혼잡도를 반영한 경로 반환. 경로가 없으면 빈 리스트 반환.
        /// </summary>
        /// <param name="start">시작 타일 좌표 (유닛의 현재 위치).</param>
        /// <param name="destination">목적지 타일 좌표 (적 성 등).</param>
        /// <param name="grid">walkable 여부 확인용 그리드 참조.</param>
        /// <param name="congestion">타일별 혼잡도 조회용 매니저.</param>
        /// <param name="congestionWeight">혼잡도 1당 비용 추가량(예: 3). 0이면 일반 A*.</param>
        /// <returns>시작점을 포함한 경로 리스트. 경로 없음/입력 오류 시 빈 리스트.</returns>
        public List<HexCoord> FindPath(
            HexCoord start,
            HexCoord destination,
            HexGrid grid,
            CongestionMap congestion,
            float congestionWeight)
        {
            // 안전 가드 — 그리드/혼잡도 매니저가 없으면 즉시 빈 리스트 반환.
            if (grid == null || congestion == null)
                return new List<HexCoord>();

            // 시작점이 그리드 밖이거나 비정상이면 경로 없음 처리.
            if (!grid.HasTile(start))
                return new List<HexCoord>();

            // ────────────────────────────────────────────────────────────────
            // 목적지가 walkable이 아니면(예: 성/건물 타일), 인접 6타일 중
            // walkable이면서 destination에 가장 가까운 타일을 실제 도착점으로 잡는다.
            // 이렇게 해야 유닛이 성에 인접한 곳까지 경로를 그릴 수 있다.
            // (도착 후 마지막 1칸은 UnitView가 공격 거리에 들어가면 알아서 멈춘다.)
            // ────────────────────────────────────────────────────────────────
            HexCoord effectiveTarget = destination;
            HexTile destTile = grid.GetTile(destination);
            if (destTile == null || !destTile.IsWalkable)
            {
                // walkable 인접 후보 중 destination에 가장 가까운(중심거리 동률은 좌표 dictionary 순) 타일을 선택.
                HexCoord? walkableAdjacent = FindWalkableAdjacent(destination, grid);
                if (!walkableAdjacent.HasValue)
                {
                    // 폴백 후보가 전혀 없으면 경로 작성 불가.
                    return new List<HexCoord>();
                }
                effectiveTarget = walkableAdjacent.Value;
            }

            // 시작점과 효과 목적지가 같으면(이미 도착) 한 노드짜리 경로 반환.
            if (start.Equals(effectiveTarget))
            {
                return new List<HexCoord> { start };
            }

            // ────────────────────────────────────────────────────────────────
            // 내부 상태 초기화 — 매 호출마다 깨끗하게 시작.
            // (한 번에 한 유닛만 호출되므로 인스턴스 공용 컬렉션 재사용 가능.)
            // ────────────────────────────────────────────────────────────────
            _gScore.Clear();
            _cameFrom.Clear();
            _closed.Clear();
            _open.Clear();
            _openLookup.Clear();
            _insertOrder = 0;

            // 시작 노드 등록.
            _gScore[start] = 0f;
            EnqueueOpen(start, HexCoord.Distance(start, effectiveTarget));

            // ────────────────────────────────────────────────────────────────
            // 메인 루프 — 오픈 셋이 빌 때까지 가장 유망한 노드(f-score 최소)를 꺼내 확장.
            // ────────────────────────────────────────────────────────────────
            while (_open.Count > 0)
            {
                // SortedSet.Min — 가장 작은 fScore 노드(동률이면 더 일찍 들어온 노드).
                OpenNode currentNode = _open.Min;
                _open.Remove(currentNode);
                _openLookup.Remove(currentNode.Coord);

                HexCoord current = currentNode.Coord;

                // 목적지 도달 시 경로 재구성하여 반환.
                if (current.Equals(effectiveTarget))
                {
                    return ReconstructPath(current);
                }

                // 닫힌 셋에 추가 — 같은 노드를 다시 평가하지 않는다.
                _closed.Add(current);

                float currentG = _gScore[current];

                // 6방향 이웃 순회 — HexMetrics.GetNeighbors는 없으므로 표준 패턴 사용.
                for (int i = 0; i < HexDirectionExtensions.Count; i++)
                {
                    HexCoord neighbor = ((HexDirection)i).Neighbor(current);

                    // 그리드 밖이면 스킵.
                    if (!grid.HasTile(neighbor)) continue;

                    // 이미 확정된 노드면 스킵.
                    if (_closed.Contains(neighbor)) continue;

                    // walkable 체크 — 단, 효과 목적지 자체는 항상 통과 허용.
                    // (effectiveTarget을 walkable 인접 타일로 이미 보정했으므로 사실상 always walkable.)
                    HexTile nbrTile = grid.GetTile(neighbor);
                    if (nbrTile == null) continue;
                    if (!nbrTile.IsWalkable && !neighbor.Equals(effectiveTarget)) continue;

                    // ────────────────────────────────────────────────────
                    // 이웃 타일 비용 = 1 + 혼잡도 × congestionWeight.
                    // 혼잡도가 0이면 비용은 1(일반 A*와 동일).
                    // 혼잡도가 클수록 비용이 커져 다른 경로로 우회하게 된다.
                    // ────────────────────────────────────────────────────
                    int congestionValue = congestion.Get(neighbor);
                    float stepCost = 1f + congestionValue * congestionWeight;
                    float tentativeG = currentG + stepCost;

                    // 기존 g-score가 있고, 더 비싼 경로면 스킵.
                    if (_gScore.TryGetValue(neighbor, out float existingG) && tentativeG >= existingG)
                        continue;

                    // 더 좋은 경로 발견 — 갱신.
                    _cameFrom[neighbor] = current;
                    _gScore[neighbor] = tentativeG;

                    // 휴리스틱은 정수 hex 거리(허용 가능 휴리스틱이라 A* 최적성 유지).
                    float fScore = tentativeG + HexCoord.Distance(neighbor, effectiveTarget);

                    // 오픈 셋에 같은 좌표가 이미 있으면 더 좋은 fScore로 교체.
                    if (_openLookup.TryGetValue(neighbor, out OpenNode existing))
                    {
                        _open.Remove(existing);
                        _openLookup.Remove(neighbor);
                    }

                    EnqueueOpen(neighbor, fScore);
                }
            }

            // 오픈 셋이 비었지만 목적지 미도달 → 경로 없음.
            return new List<HexCoord>();
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

        /// <summary>
        /// 시작점부터 끝까지의 경로를 _cameFrom 체인을 따라 재구성하여 반환한다.
        /// 반환 리스트는 시작점을 포함하며 도착 순서대로 정렬되어 있다.
        /// </summary>
        private List<HexCoord> ReconstructPath(HexCoord end)
        {
            var path = new List<HexCoord>();
            HexCoord current = end;

            // _cameFrom 체인을 거꾸로 따라가며 시작점에 도달할 때까지 좌표를 모은다.
            path.Add(current);
            while (_cameFrom.TryGetValue(current, out HexCoord parent))
            {
                current = parent;
                path.Add(current);
            }

            // 모인 좌표는 끝→시작 순서이므로 뒤집어서 시작→끝 순서로 만든다.
            path.Reverse();
            return path;
        }

        /// <summary>
        /// 오픈 셋에 노드를 새로 삽입한다.
        /// 동률 시 결정성을 위해 _insertOrder를 tie-breaker로 함께 보관한다.
        /// </summary>
        private void EnqueueOpen(HexCoord coord, float fScore)
        {
            var node = new OpenNode(coord, fScore, _insertOrder++);
            _open.Add(node);
            _openLookup[coord] = node;
        }

        /// <summary>
        /// 목적지가 walkable이 아닐 때, 인접 6타일 중 walkable이면서
        /// 목적지에 가장 가까운(여기서는 인접이라 거리=1) 첫 타일을 반환한다.
        /// 후보가 전혀 없으면 null.
        /// </summary>
        private HexCoord? FindWalkableAdjacent(HexCoord around, HexGrid grid)
        {
            for (int i = 0; i < HexDirectionExtensions.Count; i++)
            {
                HexCoord neighbor = ((HexDirection)i).Neighbor(around);
                if (!grid.HasTile(neighbor)) continue;
                HexTile tile = grid.GetTile(neighbor);
                if (tile != null && tile.IsWalkable)
                {
                    return neighbor;
                }
            }
            return null;
        }

        // ====================================================================
        // 오픈 셋 노드 — SortedSet에 들어갈 비교 가능한 노드 정의
        // ====================================================================

        /// <summary>
        /// 오픈 셋에 들어가는 노드. fScore 오름차순으로 정렬되며,
        /// 동률 시 _insertOrder(삽입 순번)로 결정성을 보장한다.
        /// 좌표는 비교 동등성 판단에는 사용되지 않지만, 노드 식별에 쓰인다.
        /// </summary>
        private class OpenNode : System.IComparable<OpenNode>
        {
            public readonly HexCoord Coord;
            public readonly float FScore;
            public readonly int Order;

            public OpenNode(HexCoord coord, float fScore, int order)
            {
                Coord = coord;
                FScore = fScore;
                Order = order;
            }

            /// <summary>
            /// 비교: fScore 오름차순 → 동률이면 Order 오름차순.
            /// SortedSet은 비교가 0이면 "같은 원소"로 간주하여 중복 삽입이 막히므로,
            /// 좌표가 달라도 fScore와 Order가 모두 같지 않은 한 별개 원소로 인식되도록 한다.
            /// </summary>
            public int CompareTo(OpenNode other)
            {
                if (other == null) return 1;

                int cmp = FScore.CompareTo(other.FScore);
                if (cmp != 0) return cmp;

                // Order는 _insertOrder가 ++로 증가하므로 노드마다 항상 다르다.
                // → 동일 노드(같은 Coord + 같은 FScore + 같은 Order)일 때만 0이 반환됨.
                return Order.CompareTo(other.Order);
            }
        }
    }
}
