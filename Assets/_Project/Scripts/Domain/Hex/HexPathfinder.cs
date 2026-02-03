// ============================================================================
// HexPathfinder.cs
// 육각형 그리드 위에서 A* 알고리즘으로 최단 경로를 탐색.
//
// A* 알고리즘 개요:
//   1. 시작 노드를 오픈셋(탐색 후보)에 넣음
//   2. 오픈셋에서 F값(= G + H)이 가장 낮은 노드를 꺼냄
//   3. 이 노드가 목표면 경로 완성 → 역추적하여 반환
//   4. 아니면 이웃 6방향을 탐색하여 오픈셋에 추가
//   5. 2~4를 반복. 오픈셋이 비면 경로 없음.
//
//   G값: 시작점에서 이 노드까지의 실제 이동 비용 (타일 수)
//   H값: 이 노드에서 목표까지의 추정 거리 (HexCoord.Distance, 휴리스틱)
//   F값: G + H → 이 값이 낮은 노드를 우선 탐색 (최적 경로 보장)
//
// 반환값:
//   List<HexCoord>: 시작점 포함 ~ 목표점까지의 경로 좌표 리스트
//   null: 경로가 없음 (막혀있거나 목표가 이동 불가 타일)
//
// 187개 타일(11×17)이라 성능 문제 없음. 외부 라이브러리 없이 커스텀 구현.
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

using System.Collections.Generic;

namespace Hexiege.Domain
{
    public static class HexPathfinder
    {
        /// <summary>
        /// start에서 goal까지의 최단 경로를 A*로 탐색.
        /// </summary>
        /// <param name="grid">탐색할 헥스 그리드</param>
        /// <param name="start">출발 좌표</param>
        /// <param name="goal">도착 좌표</param>
        /// <returns>경로 좌표 리스트 (시작~목표 포함). 경로 없으면 null.</returns>
        public static List<HexCoord> FindPath(HexGrid grid, HexCoord start, HexCoord goal)
        {
            // 출발 = 도착이면 즉시 반환
            if (start == goal) return new List<HexCoord> { start };

            // 도착 타일이 없거나 이동 불가면 경로 없음
            if (grid.GetTile(goal) == null || !grid.GetTile(goal).IsWalkable) return null;

            // ----------------------------------------------------------------
            // A* 자료구조 초기화
            // ----------------------------------------------------------------
            // openSet: 탐색 후보 노드들. F값 기준 오름차순 정렬.
            var openSet = new SortedSet<Node>(NodeComparer.Instance);
            // gScore: 시작점에서 각 노드까지의 실제 이동 비용
            var gScore = new Dictionary<HexCoord, int>();
            // cameFrom: 각 노드가 어디서 왔는지 역추적용
            var cameFrom = new Dictionary<HexCoord, HexCoord>();
            // closedSet: 이미 탐색 완료된 노드 (다시 방문하지 않음)
            var inClosedSet = new HashSet<HexCoord>();

            // 시작 노드: G=0, H=시작↔목표 거리
            gScore[start] = 0;
            openSet.Add(new Node(start, 0, HexCoord.Distance(start, goal)));

            // ----------------------------------------------------------------
            // A* 메인 루프
            // ----------------------------------------------------------------
            while (openSet.Count > 0)
            {
                // F값이 가장 낮은 노드를 꺼냄 (SortedSet.Min)
                Node current = openSet.Min;
                openSet.Remove(current);

                // 목표 도달 → 경로 역추적하여 반환
                if (current.Coord == goal)
                    return ReconstructPath(cameFrom, goal);

                // 현재 노드를 탐색 완료 처리
                inClosedSet.Add(current.Coord);

                // 이동 가능한 인접 6방향 탐색
                List<HexCoord> neighbors = grid.GetWalkableNeighborCoords(current.Coord);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    HexCoord neighbor = neighbors[i];

                    // 이미 탐색 완료된 노드는 건너뜀
                    if (inClosedSet.Contains(neighbor)) continue;

                    // 이웃까지의 G값 = 현재 G + 1 (타일 간 이동 비용은 항상 1)
                    int tentativeG = gScore[current.Coord] + 1;

                    // 기존에 더 좋은 경로가 있으면 건너뜀
                    if (gScore.TryGetValue(neighbor, out int existingG) && tentativeG >= existingG)
                        continue;

                    // 더 좋은 경로 발견 → 기록 갱신
                    cameFrom[neighbor] = current.Coord;
                    gScore[neighbor] = tentativeG;

                    int h = HexCoord.Distance(neighbor, goal);
                    openSet.Add(new Node(neighbor, tentativeG, h));
                }
            }

            // 오픈셋이 비었으면 경로 없음 (목표까지 도달 불가)
            return null;
        }

        /// <summary>
        /// cameFrom 딕셔너리를 역추적하여 시작→목표 순서의 경로 리스트 생성.
        /// </summary>
        private static List<HexCoord> ReconstructPath(Dictionary<HexCoord, HexCoord> cameFrom, HexCoord current)
        {
            var path = new List<HexCoord> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Add(current);
            }
            path.Reverse(); // 역순(목표→시작)을 정순(시작→목표)으로 뒤집기
            return path;
        }

        // ====================================================================
        // Node: A* 탐색용 내부 구조체
        // 좌표 + G값(실제 비용) + F값(G+H, 우선순위)
        // ====================================================================
        private readonly struct Node
        {
            public readonly HexCoord Coord;
            public readonly int G;   // 시작점 → 이 노드까지 실제 이동 비용
            public readonly int F;   // G + H (추정 총 비용, 낮을수록 우선 탐색)

            public Node(HexCoord coord, int g, int h)
            {
                Coord = coord;
                G = g;
                F = g + h;
            }
        }

        // ====================================================================
        // NodeComparer: SortedSet에서 Node 정렬 기준 정의.
        // 1차: F값 오름차순 (최적 경로 우선)
        // 2차: G값 오름차순 (같은 F면 실제 비용이 낮은 쪽)
        // 3차/4차: Q, R 좌표 비교 (SortedSet은 중복 불허이므로 동일 좌표가 아니면 다른 노드로 취급)
        // ====================================================================
        private class NodeComparer : IComparer<Node>
        {
            public static readonly NodeComparer Instance = new NodeComparer();

            public int Compare(Node a, Node b)
            {
                int cmp = a.F.CompareTo(b.F);
                if (cmp != 0) return cmp;
                cmp = a.G.CompareTo(b.G);
                if (cmp != 0) return cmp;
                cmp = a.Coord.Q.CompareTo(b.Coord.Q);
                if (cmp != 0) return cmp;
                return a.Coord.R.CompareTo(b.Coord.R);
            }
        }
    }
}
