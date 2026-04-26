// ============================================================================
// HexFlowField.cs
// "플로우 필드(Flow Field)" — 한 목적지를 향한 전체 맵의 방향 정보를 담는 자료구조.
//
// 사용 배경:
//   기존 A* 방식은 유닛마다 매번 경로를 계산했다.
//   유닛 수가 늘어나면 계산량이 누적되고, 아군 점유 타일을 모두 차단(blocked)하는
//   탐색은 통로가 좁아지면 경로가 아예 막혀버려 유닛이 멈춘다.
//
// 플로우 필드 방식의 핵심:
//   1) 목적지 한 곳을 정해두고, 그 곳에서 출발해 BFS(너비 우선 탐색)로
//      모든 walkable 타일까지 거꾸로 한 번만 탐색한다.
//   2) 탐색 도중 "어느 타일에서 어느 타일로 가야 목적지에 한 칸 가까워지는가"를
//      기록해 둔다. (이것이 곧 "흐름(flow)" 정보다)
//   3) 이후 어떤 유닛이라도 자신의 현재 타일을 키로 _flow 사전을 조회만 하면
//      O(1)로 다음 이동 타일을 알 수 있다.
//
// 왜 BFS인가:
//   타일 간 이동 비용이 모두 같다고 가정하므로 다익스트라/A*가 필요 없다.
//   BFS만으로도 "최단 칸 수" 경로가 보장된다.
//
// 역방향 탐색(목적지 → 모든 타일)인 이유:
//   유닛이 N마리 있으면 정방향이면 N번 탐색해야 하지만,
//   목적지 한 곳에서 한 번만 BFS하면 모든 출발지에서의 경로가 동시에 완성된다.
//
// non-walkable 목적지 처리(예: 성·건물 타일):
//   목적지 자체는 유닛이 들어갈 수 없으므로, 인접한 walkable 타일들을
//   BFS 시작점으로 사용한다. 이렇게 하면 "성 바로 앞"까지 경로가 안내되고
//   GetPath() 호출 시 마지막에 목적지(성 타일)를 별도로 덧붙여 준다.
//
// Domain 레이어 — Unity 의존 없음, 순수 C#.
// (주의) using Hexiege.Core 절대 금지. HexMetrics 등 Core 타입을 사용하지 않는다.
// ============================================================================

using System.Collections.Generic;

namespace Hexiege.Domain
{
    public class HexFlowField
    {
        // 키: 어떤 타일 / 값: 그 타일에서 다음으로 이동해야 할 타일.
        // 즉 _flow[현재타일] = 다음타일. 목적지 자체는 보통 키로 들어가지 않거나
        // 자기 자신을 가리킨다(non-walkable 목적지는 키에 포함되지 않음).
        private readonly Dictionary<HexCoord, HexCoord> _flow;

        /// <summary>
        /// 이 플로우 필드의 최종 목적지(성, 랠리포인트 등).
        /// non-walkable 타일이라도 그대로 보관 — GetPath()가 마지막에 덧붙일 때 사용.
        /// </summary>
        public HexCoord Destination { get; }

        // 외부에서는 Compute() 정적 메서드로만 인스턴스를 생성하도록 제한.
        // 일반 생성자를 노출하면 _flow를 부주의하게 외부에서 만들 수 있어 캡슐화가 깨진다.
        private HexFlowField(HexCoord destination, Dictionary<HexCoord, HexCoord> flow)
        {
            Destination = destination;
            _flow = flow;
        }

        /// <summary>
        /// 목적지 타일을 기준으로 전체 맵을 BFS 역방향 탐색하여 플로우 필드를 만든다.
        ///
        /// 동작 절차:
        ///   1. 목적지가 walkable이면 목적지 한 곳을 BFS 큐의 시드(seed)로 사용.
        ///   2. 목적지가 non-walkable이면 목적지의 walkable 인접 타일 모두를 시드로 사용.
        ///      이 경우 시드 타일들은 _flow에 자기 자신을 가리키게 등록된다(루프 가드).
        ///   3. 큐에서 타일을 꺼낼 때마다 그 타일의 walkable 이웃 6방향을 검사한다.
        ///      방문 안 한 이웃은 _flow[이웃] = 꺼낸타일 로 기록(이웃이 한 칸 가야 할 다음 타일).
        ///      그 후 이웃을 큐에 넣어 같은 절차를 반복.
        ///   4. 큐가 비면 도달 가능한 모든 타일이 _flow에 채워진 상태가 된다.
        ///
        /// 반환:
        ///   완성된 HexFlowField 인스턴스. 도달 가능한 타일이 0개여도 빈 필드를 반환한다.
        /// </summary>
        /// <param name="grid">탐색 대상 그리드.</param>
        /// <param name="destination">목적지 타일 좌표(walkable 또는 non-walkable 무관).</param>
        public static HexFlowField Compute(HexGrid grid, HexCoord destination)
        {
            // BFS 결과를 담을 사전 — 키: 타일, 값: 그 타일이 가야 할 다음 타일.
            var flow = new Dictionary<HexCoord, HexCoord>();

            // BFS 큐 — 다음에 처리할 타일들이 대기하는 자리.
            var queue = new Queue<HexCoord>();

            // 1) 시드(BFS 시작점) 결정.
            HexTile destTile = grid.GetTile(destination);
            bool destWalkable = destTile != null && destTile.IsWalkable;

            if (destWalkable)
            {
                // 목적지가 walkable이면 그 자체가 유일한 시드.
                // _flow[목적지] = 목적지 (자기 자신). GetNextTile에서 도착 판정에 활용.
                flow[destination] = destination;
                queue.Enqueue(destination);
            }
            else
            {
                // 목적지가 non-walkable인 경우(성/건물 등):
                // 목적지의 walkable 인접 타일 전부를 시드로 사용하여
                // "건물 앞 한 칸"까지 안내한다.
                List<HexCoord> seeds = grid.GetWalkableNeighborCoords(destination);
                for (int i = 0; i < seeds.Count; i++)
                {
                    HexCoord seed = seeds[i];
                    if (!flow.ContainsKey(seed))
                    {
                        // 시드 타일도 자기 자신을 가리키도록 등록 — 무한 루프 방지(GetPath에서 종료 판정).
                        flow[seed] = seed;
                        queue.Enqueue(seed);
                    }
                }
            }

            // 2) BFS 본체.
            // 큐에서 한 타일을 꺼내고, 그 타일의 walkable 이웃을 검사하여
            // 아직 방문되지 않은 이웃은 _flow에 등록한 뒤 큐에 추가한다.
            while (queue.Count > 0)
            {
                HexCoord current = queue.Dequeue();

                // walkable 이웃 6방향 (그리드 밖이거나 막힌 타일은 제외됨)
                List<HexCoord> neighbors = grid.GetWalkableNeighborCoords(current);

                for (int i = 0; i < neighbors.Count; i++)
                {
                    HexCoord neighbor = neighbors[i];

                    // 이미 방문된 타일은 스킵 — BFS는 가장 먼저 도달한 경로가 곧 최단경로.
                    if (flow.ContainsKey(neighbor)) continue;

                    // 이웃이 한 칸 이동해야 할 다음 타일은 "지금 꺼낸 current".
                    // 이 한 줄이 플로우 필드의 본질이다.
                    flow[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }

            return new HexFlowField(destination, flow);
        }

        /// <summary>
        /// 현재 타일에서 한 칸 이동해야 할 다음 타일을 반환.
        /// 정보가 없으면(목적지에 도달 불가하거나 사전 외 타일이면) null.
        ///
        /// 시드 타일(목적지 자신 또는 non-walkable 목적지의 인접 walkable 타일)에서는
        /// 자기 자신을 반환한다. 호출 측은 이를 "도착했음" 신호로 해석할 수 있다.
        /// </summary>
        public HexCoord? GetNextTile(HexCoord current)
        {
            if (_flow.TryGetValue(current, out HexCoord next))
                return next;
            return null;
        }

        /// <summary>
        /// 시작 타일에서 목적지까지의 경로를 리스트로 만들어 반환.
        ///
        /// 동작:
        ///   1. start를 첫 원소로 넣고, GetNextTile을 반복 호출하며 다음 타일을 추가.
        ///   2. 다음 타일이 자기 자신과 같으면 시드(목적지 또는 그 인접)에 도달한 것 → 종료.
        ///   3. 다음 타일이 null이면 경로가 끊긴 것 → 종료.
        ///   4. 무한 루프 방지를 위해 최대 300스텝 제한.
        ///
        /// non-walkable 목적지 보정:
        ///   _flow는 walkable 타일까지만 안내하므로(성 자체는 들어갈 수 없음),
        ///   목적지가 non-walkable이라면 경로 끝에 Destination을 따로 추가한다.
        ///   → 호출 측(UnitView)은 마지막 타일 방향으로 Lerp 이동 후 공격을 시작할 수 있다.
        ///
        /// 반환:
        ///   start만 들어 있는 리스트(길이 1)부터 정상 경로(길이 N)까지 다양.
        ///   _flow에 start 정보가 없고 목적지도 non-walkable이 아니면 null.
        /// </summary>
        public List<HexCoord> GetPath(HexCoord start)
        {
            var path = new List<HexCoord>();

            // 시작 타일의 흐름 정보가 없는 경우:
            //   - non-walkable 목적지이고 start가 시드(인접) 타일도 아니면 정말 갈 길이 없다.
            //   - 단, start == Destination 같은 특수 상황도 처리해야 한다.
            if (!_flow.ContainsKey(start))
            {
                // start가 곧 목적지인 경우(이미 도착) — start만 담은 리스트 반환.
                if (start == Destination)
                {
                    path.Add(start);
                    return path;
                }

                // non-walkable 목적지인데 start가 시드 타일도 아니라면,
                // 목적지가 격리되어 있거나 start가 도달 불가 영역에 있는 것이다.
                return null;
            }

            // 일반 경로 추적.
            path.Add(start);
            HexCoord current = start;

            const int maxSteps = 300; // 무한 루프 방지 — 187타일 그리드에서 충분히 큰 값.
            int steps = 0;

            while (steps++ < maxSteps)
            {
                HexCoord? next = GetNextTile(current);

                // 흐름이 끊겼으면 즉시 종료(현재까지 쌓은 path 그대로 반환).
                if (!next.HasValue) break;

                HexCoord nextCoord = next.Value;

                // 시드 타일은 자기 자신을 가리킨다 → 도착 신호.
                // path에 중복 추가하지 않고 종료한다.
                if (nextCoord == current) break;

                path.Add(nextCoord);
                current = nextCoord;
            }

            // non-walkable 목적지인 경우 마지막에 목적지 좌표를 덧붙인다.
            // walkable 목적지는 BFS 시드가 곧 목적지였으므로 위에서 이미 path 끝에 들어가 있다.
            // 같은 좌표를 중복 추가하지 않도록 마지막 원소와 비교 후 추가.
            if (path.Count == 0 || path[path.Count - 1] != Destination)
            {
                HexTile destTileCheck = null;
                // _flow에 Destination 자체가 키로 등록되었는지를 walkable 여부 판정 근거로 사용.
                // walkable 목적지는 Compute()에서 flow[destination] = destination로 등록된다.
                if (!_flow.ContainsKey(Destination))
                {
                    // non-walkable 목적지로 간주 — 마지막에 목적지 좌표를 덧붙인다.
                    // (UnitView가 마지막 타일 방향으로 Lerp 이동 후 공격하도록 하기 위함)
                    path.Add(Destination);
                }
                // destTileCheck 변수는 의미 명확화를 위해 선언만 해 둔 자리표시 — 실제 사용 없음.
                _ = destTileCheck;
            }

            return path;
        }
    }
}
