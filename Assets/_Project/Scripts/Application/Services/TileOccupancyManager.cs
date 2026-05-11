// ============================================================================
// TileOccupancyManager.cs
// 타일별 "유닛이 실제로 차지하는 양"을 추적하여, 좁은 타일에 너무 많은 유닛이
// 몰려 시각적으로 완전히 겹치는 현상을 방지하는 매니저.
//
// 사용 배경 (2026-04-25 2차 개선):
//   플로우 필드 도입 후 같은 방향으로 향하는 유닛이 모두 동일한 경로를 따라가
//   한 타일에 많은 유닛이 겹쳐 모이는 현상이 생겼다.
//   기존 TileSlotManager는 6개 슬롯밖에 없어 7번째 유닛부터 모두 슬롯 0(중심)으로
//   몰려 시각적으로 한 점에 뭉친다.
//
//   TileOccupancyManager는 "이 타일은 이미 합계 3을 차지 중이라 더 못 들어옴"이라는
//   판정을 도입하여, 초과되면 BFS로 이웃 타일을 찾아 우회시킨다.
//
// 핵심 설계 원칙:
//   1) 예약 없음 — 실제 유닛의 도메인 Position에 기반한 점유만 추적.
//      (예약 기반은 "이동 시작은 했는데 끝나지 않은" 상태가 누적되면 데드락 위험.)
//   2) 타일당 최대 점유 합계 = 3 (= 대형 유닛 1마리 = 소형 유닛 3마리).
//   3) 이동 시작 직전에만 체크 — Lerp 도중에는 체크하지 않음.
//   4) 초과 시 BFS로 walkable 인접 타일 → 그 인접 타일 순으로 탐색하여 빈자리 찾음.
//   5) 모든 타일이 가득 찼으면 호출 측에서 yield return null로 대기.
//      (게임 인구 한계가 보유 타일 수와 같으므로 이론상 거의 발생하지 않음.)
//
// TileSlotManager와의 차이:
//   TileSlotManager  — 시각적 위치 분산 전용 (오프셋 Vector3 반환).
//   TileOccupancyManager — 도메인 점유량 추적 (이동 가능 여부 판정).
//   서로 독립적으로 동작하며, 같은 타일에 들어온 유닛은 두 시스템 모두에 등록된다.
//
// 멀티플레이 정책:
//   서버에서만 사용. 클라이언트는 NetworkTransform이 위치를 보간 동기화하므로
//   별도의 점유 추적이 필요하지 않다.
//
// Application 레이어 — Domain에 의존, Unity에 직접 의존하지 않음.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public class TileOccupancyManager
    {
        // 한 타일이 받을 수 있는 점유 합계의 상한.
        // 소형=1, 중형=2, 대형=3 기준이며,
        // 대형 1마리 또는 중형 1+소형 1 또는 소형 3마리 등이 가능하다.
        public const float MaxOccupancy = 3f;

        // 타일별 누적 점유량.
        // 키: 타일 좌표 / 값: 현재까지 등록된 유닛들의 OccupancySize 합계.
        private readonly Dictionary<HexCoord, float> _occupancy
            = new Dictionary<HexCoord, float>();

        /// <summary>
        /// 해당 타일에 unitSize만큼의 점유를 추가할 수 있는지 확인.
        /// 부동소수점 누적 오차로 0.999... 같은 값이 합쳐졌을 때 false 처리되지 않도록
        /// 비교에 작은 여유(epsilon)를 둔다.
        /// </summary>
        /// <param name="tile">확인할 타일 좌표.</param>
        /// <param name="unitSize">추가하려는 유닛의 OccupancySize.</param>
        /// <returns>점유 합계가 MaxOccupancy 이내면 true.</returns>
        public bool CanFit(HexCoord tile, float unitSize)
        {
            // GetValueOrDefault: 타일이 _occupancy에 없으면 0 반환 — 빈 타일 처리.
            float current = _occupancy.TryGetValue(tile, out float value) ? value : 0f;
            const float epsilon = 0.0001f;
            return current + unitSize <= MaxOccupancy + epsilon;
        }

        /// <summary>
        /// 유닛이 from 타일에서 to 타일로 이동했음을 반영.
        ///
        /// 동작:
        ///   - from이 default(HexCoord)면 "최초 스폰" 신호로 보고 감소를 건너뛴다.
        ///     (default(HexCoord) = (0,0)을 일반 타일로 쓸 수도 있지만, 스폰 시점에는
        ///      이전 위치가 없으므로 호출 측이 default를 넘긴다는 약속.)
        ///   - from == to면 같은 타일 내 재호출이므로 변동 없음.
        ///
        /// 호출 측은 ProcessStep 내부 등 이동이 도메인적으로 확정된 시점에 호출.
        /// </summary>
        /// <param name="from">이동 전 타일 좌표 (스폰이라면 default).</param>
        /// <param name="to">이동 후 타일 좌표.</param>
        /// <param name="unitSize">이동한 유닛의 OccupancySize.</param>
        public void OnUnitMoved(HexCoord from, HexCoord to, float unitSize)
        {
            // 같은 타일이면 변경 없음 (Phase 2 ProcessStep에서 from == to로 호출되는 경우 등).
            if (from == to) return;

            // from이 무효값(스폰 직후)이 아닐 때만 감소.
            // default(HexCoord) == new HexCoord(0,0)이 일반 좌표일 수도 있지만,
            // OnUnitSpawned 같은 별도 함수를 만들기보다 호출 약속(스폰 시 default 전달)으로 처리.
            if (!IsInvalid(from))
            {
                Decrease(from, unitSize);
            }

            Increase(to, unitSize);
        }

        /// <summary>
        /// 유닛이 사망/제거됨을 반영. 해당 타일의 점유량을 감소시킨다.
        /// 등록되지 않은 타일은 무시 (방어적 처리).
        /// </summary>
        /// <param name="tile">유닛이 있던 타일 좌표.</param>
        /// <param name="unitSize">제거된 유닛의 OccupancySize.</param>
        public void OnUnitRemoved(HexCoord tile, float unitSize)
        {
            if (IsInvalid(tile)) return;
            Decrease(tile, unitSize);
        }

        /// <summary>
        /// Lerp 시작 전에 도착 타일(to)의 점유를 미리 예약한다.
        ///
        /// [4차 개선 — 2026-04-26] RegisterOccupancyMove와의 분리:
        ///   기존 OnUnitMoved(from, to)는 FROM-1, TO+1을 동시에 처리했다.
        ///   그러나 Lerp 시작 직전에는 유닛이 물리적으로 아직 FROM에 있으므로
        ///   FROM을 즉시 감소시키면 "FROM이 비어있다"는 거짓 판정을 유발한다.
        ///   이 메서드는 TO+1만 수행하고, FROM-1은 Lerp 완료 후 ProcessStep에서 처리한다.
        ///
        /// 이렇게 분리해야 같은 프레임 안에서 다음 두 가지가 모두 정확해진다:
        ///   - 다른 유닛은 TO 타일이 즉시 "차 있음"으로 판정 → 같은 칸으로 몰리지 않음.
        ///   - 다른 유닛은 FROM 타일이 여전히 "차 있음"으로 판정 → 이동자가 아직 거기 있는데
        ///     "비었다"고 보고 몰려드는 현상이 사라짐.
        /// </summary>
        /// <param name="tile">예약할 도착 타일 좌표.</param>
        /// <param name="unitSize">이동하려는 유닛의 OccupancySize.</param>
        public void ReserveOccupancy(HexCoord tile, float unitSize)
        {
            // 무효 좌표(스폰 직전 등) 또는 default 처리 — 기존 OnUnitRemoved와 동일한 가드.
            if (IsInvalid(tile)) return;
            Increase(tile, unitSize);
        }

        /// <summary>
        /// FindForwardAvailable의 내부 BFS 본체.
        /// 후보 타일이 목적지에서 더 멀어지지 않을 때만(거리 +1 여유) 반환 후보로 인정한다.
        /// 큐에는 항상 추가하므로 외곽 탐색은 계속 진행된다.
        ///
        /// 새 규칙(GameSystemRules.md 규칙 5)에서는 "뒤로 우회 절대 금지, 앞쪽이 없으면 대기"이므로
        /// useForwardFilter=false fallback은 더 이상 호출되지 않는다(파라미터는 제거됨).
        ///
        /// [BUG-008 — 2026-05-06] currentTile 파라미터:
        ///   호출 유닛의 현재 논리 타일을 visited에 미리 등록하여 우회 후보로 반환되는 것을 차단한다.
        ///   currentTile == default(HexCoord)이면 추가 제외 없음.
        /// </summary>
        private HexCoord? BfsFindAvailable(HexCoord preferred, float unitSize, HexGrid grid,
            HexCoord destination, int preferredDist, HexCoord currentTile)
        {
            // visited는 "이미 큐에 넣었거나 검사한 타일"을 기록하여 중복 방문 방지.
            // 시작 좌표 자체도 visited에 넣어 둠 (이미 위에서 실패함).
            var visited = new HashSet<HexCoord> { preferred };

            // [BUG-008] 현재 유닛 위치 타일을 visited에 추가 — 우회 결과로 자기 자신을 반환하는 일을 차단.
            //   default(HexCoord)는 "정보 없음" 약속이므로 그 경우엔 추가하지 않는다.
            //   IsInvalid 체크는 의도적으로 사용하지 않는다 — 일반 (0,0) 타일에 위치한 유닛도 보호받아야 한다.
            //   (단, 현재 약속상 호출 측에서 의미 있는 좌표만 넘겨준다는 전제하에)
            if (!(currentTile == default(HexCoord)))
            {
                visited.Add(currentTile);
            }

            var queue = new Queue<HexCoord>();
            queue.Enqueue(preferred);

            // 탐색 깊이/노드 수 상한 — 187타일 그리드에서 충분히 큰 값.
            // 이론적으로 모든 타일이 가득 차는 상황은 거의 없지만, 게임 안전망으로 둔다.
            const int maxNodes = 256;
            int processed = 0;

            while (queue.Count > 0 && processed++ < maxNodes)
            {
                HexCoord current = queue.Dequeue();
                List<HexCoord> neighbors = grid.GetWalkableNeighborCoords(current);

                for (int i = 0; i < neighbors.Count; i++)
                {
                    HexCoord nb = neighbors[i];
                    if (visited.Contains(nb)) continue;
                    visited.Add(nb);

                    // 빈자리 후보 발견.
                    if (CanFit(nb, unitSize))
                    {
                        // forward 필터: 목적지에서 너무 멀어지지 않는 타일만 반환.
                        // 헥스 거리 기준으로 preferred보다 +1까지는 측면으로 간주하고 허용.
                        int candidateDist = HexCoord.Distance(nb, destination);
                        if (candidateDist <= preferredDist + 1)
                            return nb;
                        // 필터 미통과 — 후보로는 반환하지 않지만 큐에는 추가하여 외곽 탐색 계속.
                    }

                    // 들어갈 수 없어도(또는 forward 필터 미통과여도) 더 외곽으로 탐색을 확장하기 위해 큐에 추가.
                    queue.Enqueue(nb);
                }
            }

            // 가득 차서 후보가 전혀 없음(또는 forward 조건을 만족하는 타일이 없음).
            return null;
        }

        // ====================================================================
        // [2026-04-30] 새 이동/전투 규칙(GameSystemRules.md 규칙 5) — "앞쪽 또는 대기"
        // ====================================================================

        /// <summary>
        /// [2026-04-30] 새 규칙용 우회 메서드.
        /// preferred 타일에 들어갈 수 있으면 그 좌표를 반환.
        /// 들어갈 수 없으면 BFS로 "앞쪽(목적지에 가까워지는) walkable 타일"만 후보로 인정해 반환.
        /// 앞쪽에 빈 타일이 없으면 null — 호출 측은 "현재 위치에서 대기"한다.
        ///
        /// 새 규칙 5와의 차이:
        ///   기존 FindAvailableTile(...)은 forward 필터 실패 시 fallback BFS(필터 OFF)로 다시
        ///   시도해 어떤 빈 타일이라도 찾아주었다. 그러나 새 규칙은 "뒤로 우회 절대 금지,
        ///   앞쪽이 없으면 대기"이므로 fallback BFS 자체를 호출하지 않는다.
        ///
        /// destination이 default(HexCoord)이거나 preferred와 같으면 forward 필터를 활성화할 수
        /// 없어 의미가 없는 호출이다. 이 경우 안전을 위해 null을 반환한다(호출 측에서 destination을
        /// 반드시 의미 있는 값으로 넘기는 것을 강제).
        /// </summary>
        /// <param name="preferred">원래 가고 싶었던 타일 좌표.</param>
        /// <param name="unitSize">이동하려는 유닛의 OccupancySize.</param>
        /// <param name="grid">walkable 여부 판정 + 이웃 탐색에 사용할 그리드.</param>
        /// <param name="destination">최종 목적지 좌표(우회 방향 결정에 사용). default 시 null 반환.</param>
        /// <param name="currentTile">
        /// [BUG-008 — 2026-05-06] 호출 유닛의 현재 논리 타일 좌표(prevActualTile).
        /// BFS visited 초기 집합에 추가하여 "현재 위치 타일"이 우회 후보로 반환되는 것을 차단한다.
        /// 호출 측이 의미 있는 값을 모르거나 폴백이 필요하면 default(HexCoord)를 넘긴다(이 경우 추가 제외 없음).
        /// </param>
        /// <returns>들어갈 수 있는 앞쪽 타일 좌표 또는 null(앞쪽이 모두 가득 → 대기).</returns>
        public HexCoord? FindForwardAvailable(HexCoord preferred, float unitSize, HexGrid grid, HexCoord destination, HexCoord currentTile)
        {
            if (grid == null) return null;

            // 1차: preferred가 가능하면 즉시 반환.
            //   ※ currentTile == preferred인 경우 — 같은 타일이라 의미상 "이미 도착"이지만
            //   아래 forward 필터가 destination==preferred를 비활성 케이스로 처리하므로 그대로 두어도 된다.
            HexTile preferredTile = grid.GetTile(preferred);
            bool preferredWalkable = preferredTile != null && preferredTile.IsWalkable;
            if (preferredWalkable && CanFit(preferred, unitSize))
                return preferred;

            // forward 필터 활성화 가능 여부 확인 — 새 규칙은 이게 항상 활성화돼야 한다.
            // destination이 의미 없는 값이면 호출 자체가 잘못된 것으로 간주.
            bool useForwardFilter = !(destination == default(HexCoord)) && destination != preferred;
            if (!useForwardFilter)
            {
                return null;
            }

            int preferredDist = HexCoord.Distance(preferred, destination);

            // forward 필터 BFS — 통과 후보 없으면 null(=대기).
            // 기존 FindAvailableTile의 fallback BFS는 호출하지 않는다(새 규칙 핵심).
            return BfsFindAvailable(preferred, unitSize, grid,
                destination: destination, preferredDist: preferredDist,
                currentTile: currentTile);
        }

        /// <summary>
        /// 모든 점유 정보를 초기화. 맵 전환/재경기 시 호출.
        /// </summary>
        public void Clear()
        {
            _occupancy.Clear();
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

        /// <summary>
        /// HexCoord 무효값(스폰 직전 등) 판정.
        /// default(HexCoord)는 (Q=0, R=0)이지만, 호출 약속에 의해 "스폰 직전"으로 해석.
        /// 일반 (0,0) 타일과 구분이 필요한 경우 호출 측에서 default를 의도적으로 넘기지 않으면 된다.
        /// </summary>
        private static bool IsInvalid(HexCoord coord)
        {
            return coord.Q == 0 && coord.R == 0;
        }

        /// <summary>
        /// 점유량 누적 — 같은 타일에 여러 유닛이 등록되면 합계가 늘어난다.
        /// </summary>
        private void Increase(HexCoord tile, float amount)
        {
            if (_occupancy.TryGetValue(tile, out float current))
                _occupancy[tile] = current + amount;
            else
                _occupancy[tile] = amount;
        }

        /// <summary>
        /// 점유량 차감 — 0 이하가 되면 사전에서 항목 자체를 제거(메모리 정리).
        /// 미등록 타일이거나 차감 후 음수가 되면 0으로 클램프.
        /// </summary>
        private void Decrease(HexCoord tile, float amount)
        {
            if (!_occupancy.TryGetValue(tile, out float current)) return;

            float next = current - amount;
            const float epsilon = 0.0001f;
            if (next <= epsilon)
                _occupancy.Remove(tile);
            else
                _occupancy[tile] = next;
        }
    }
}
