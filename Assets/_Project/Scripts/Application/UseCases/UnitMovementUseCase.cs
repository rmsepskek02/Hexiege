// ============================================================================
// UnitMovementUseCase.cs
// 유닛 이동 명령을 처리하는 UseCase.
//
// 흐름:
//   1. InputHandler가 "유닛을 선택한 뒤 타일을 클릭"하면 이 UseCase 호출
//   2. FlowFieldService에서 목적지에 해당하는 플로우 필드 조회 (캐시 또는 즉시 계산)
//   3. 플로우 필드의 GetPath(start)로 목적지까지의 경로 리스트를 받는다
//   4. Presentation 레이어(UnitView)가 경로를 받아 시각적 이동 처리
//
// 플로우 필드 도입(2026-04-25):
//   기존: 유닛마다 A* 호출 + 아군 Position/ClaimedTile 차단으로 좁은 통로에서 막힘
//   변경: 같은 목적지로 향하는 유닛들이 하나의 플로우 필드를 공유 (O(1) 조회)
//        아군 점유 타일은 차단하지 않음 → 시각적 겹침은 TileSlotManager가 분산
//
// 이 UseCase는 논리적 이동만 담당 (데이터 변경 + 이벤트 발행).
// 실제 화면상 Lerp 이동 애니메이션은 UnitView(Presentation)가 처리.
//
// Application 레이어 — Domain에 의존, Unity에 직접 의존하지 않음.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Core;

namespace Hexiege.Application
{
    public class UnitMovementUseCase : IDisposable
    {
        // 경로 계산 및 타일 점령에 사용할 그리드 참조
        private readonly HexGrid _grid;

        // 적 유닛 좌표 조회 등을 위한 참조 (현 시점에서는 사용처 없음, 향후 확장 대비 유지).
        private readonly UnitSpawnUseCase _unitSpawn;

        // 플로우 필드 캐시 서비스 — 목적지별 BFS 결과를 공유한다.
        private readonly FlowFieldService _flowFieldService;

        // [2026-05-11 비활성화 — 슬롯/점유 시스템 폐기]
        // _occupancyManager는 항상 null이 주입됩니다. 새 규칙(GameSystemRules.md)에서는
        // 타일 점유 추적을 사용하지 않습니다. 시그니처는 호출부 호환을 위해 유지합니다.
        private readonly TileOccupancyManager _occupancyManager;

        // 사망/제거 이벤트 구독 해제용 — 점유 정보 정리에 사용하던 것이지만 폐기됨.
        // _subscriptions 자체는 향후 확장을 위해 유지하지만 현재는 구독 추가가 없습니다.
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();

        public UnitMovementUseCase(HexGrid grid, UnitSpawnUseCase unitSpawn,
            FlowFieldService flowFieldService,
            TileOccupancyManager occupancyManager = null)
        {
            _grid = grid;
            _unitSpawn = unitSpawn;
            _flowFieldService = flowFieldService;
            _occupancyManager = occupancyManager;

            // [2026-05-11 비활성화 — 점유 시스템 폐기]
            // OnEntityDied → OnUnitRemoved 자동 구독은 점유 시스템 폐기로 제거됐습니다.
            // 기존 코드는 git 히스토리에서 확인 가능합니다.
            //
            // if (_occupancyManager != null)
            // {
            //     _subscriptions.Add(GameEvents.OnEntityDied.Subscribe(evt =>
            //     {
            //         if (evt.Entity is UnitData unit)
            //         {
            //             float size = GetOccupancySize(unit.Type);
            //             _occupancyManager.OnUnitRemoved(unit.Position, size);
            //         }
            //     }));
            // }
        }

        /// <summary>
        /// 구독 해제. 게임 종료/재경기 시 GameBootstrapper가 호출.
        /// </summary>
        public void Dispose()
        {
            _subscriptions?.Dispose();
        }

        /// <summary>
        /// 유닛에게 목표 타일로 이동 명령을 내림.
        ///
        /// 동작:
        ///   1) FlowFieldService에서 target에 해당하는 플로우 필드를 가져온다.
        ///      (캐시 미존재 시 BFS 1회 계산, 187타일 기준 1ms 미만)
        ///   2) field.GetPath(unit.Position)로 시작점부터 목적지까지의 경로를 만든다.
        ///   3) 경로가 유효(길이 ≥ 2)하면 그대로 반환.
        ///
        /// 반환:
        ///   이동 경로 리스트 (시작점 포함). 이동 불가 시 null.
        ///   목적지가 non-walkable(성/건물)이라도 GetPath()가 마지막에 목적지를 덧붙여 주므로
        ///   UnitView는 마지막 타일 방향으로 Lerp 이동 → 공격을 시작할 수 있다.
        ///
        /// 차단 목록:
        ///   플로우 필드는 walkable 여부만 본다. 아군 점유 타일을 차단하지 않으므로
        ///   유닛이 많아져도 경로가 막혀 멈추는 현상이 사라진다.
        ///   대신 같은 타일에 여러 유닛이 모일 수 있으므로 TileSlotManager가
        ///   시각적 위치를 분산시킨다.
        /// </summary>
        /// <param name="unit">이동할 유닛 데이터</param>
        /// <param name="target">목표 타일 좌표</param>
        /// <returns>경로 리스트(시작점 포함). 이동 불가 시 null.</returns>
        public List<HexCoord> RequestMove(UnitData unit, HexCoord target)
        {
            // FlowFieldService 미주입 방어 — Bootstrap 와이어링 누락 시 명시적 null 반환.
            if (_flowFieldService == null) return null;

            HexFlowField field = _flowFieldService.GetOrCompute(target);
            if (field == null) return null;

            List<HexCoord> path = field.GetPath(unit.Position);

            // 경로가 없거나 1칸짜리(이미 도착)라면 이동 불가로 처리.
            // 단, target이 non-walkable이고 경로 끝에 target이 덧붙여진 경우(길이 2 이상)는 정상.
            if (path == null || path.Count < 2) return null;

            return path;
        }

        /// <summary>
        /// 특정 좌표의 타일이 이동 가능(walkable)한지 확인.
        /// UnitView에서 경로 마지막 타일이 건물 타일인지 판단하는 데 사용.
        /// 그리드에 존재하지 않는 좌표는 false 반환 (이동 불가).
        /// </summary>
        /// <param name="coord">확인할 타일 좌표</param>
        /// <returns>타일이 존재하고 walkable이면 true, 아니면 false</returns>
        public bool IsWalkable(HexCoord coord)
        {
            HexTile tile = _grid.GetTile(coord);
            return tile != null && tile.IsWalkable;
        }

        /// <summary>
        /// 경로 상의 타일 하나를 이동 처리.
        /// UnitView의 코루틴에서 타일→타일 Lerp 이동이 끝날 때마다 호출.
        ///
        /// 처리 내용:
        ///   1. [4차 개선] FROM 타일 점유 해제 (실제로 떠난 시점)
        ///   2. 이동 방향 계산 → UnitData.Facing 업데이트
        ///   3. UnitData.Position 업데이트
        ///   4. 도착 타일을 유닛의 팀으로 점령
        ///   5. 이동/점령 이벤트 발행
        ///
        /// [3차 개선 — 2026-04-25]
        /// 점유량 갱신(OnUnitMoved) 호출을 ProcessStep에서 분리하여 RegisterOccupancyMove로 옮겼다.
        ///
        /// [4차 개선 — 2026-04-26]
        /// FROM 타일 점유 해제만 ProcessStep으로 다시 가져온다(분리된 형태).
        /// RegisterOccupancyMove는 TO+1만 예약하고, 실제로 FROM을 떠난 이 시점에 FROM-1을 처리해야
        /// 이동이 도중에 중단되더라도 FROM 점유가 유닛 위치보다 먼저 사라지지 않는다.
        /// </summary>
        /// <param name="unit">이동 중인 유닛</param>
        /// <param name="from">출발 타일 좌표</param>
        /// <param name="to">도착 타일 좌표</param>
        public void ProcessStep(UnitData unit, HexCoord from, HexCoord to)
        {
            // [2026-05-11 비활성화 — 점유 시스템 폐기]
            // FROM 타일 점유 해제(_occupancyManager.OnUnitRemoved) 호출은 제거됐습니다.
            // 새 규칙에서는 점유 추적이 없으므로 도메인 상태(Position, Facing)만 갱신하고
            // OnUnitMoved 이벤트를 발행합니다.
            //
            // 기존 코드:
            // if (from != to && _occupancyManager != null)
            // {
            //     _occupancyManager.OnUnitRemoved(from, GetOccupancySize(unit.Type));
            // }

            // 이동 방향 계산 → 스프라이트 방향 전환에 사용
            HexDirection dir = FacingDirection.FromCoords(from, to);
            unit.Facing = dir;

            // 유닛 위치 업데이트
            unit.Position = to;

            // 이벤트 발행 → UnitView가 스프라이트 이동
            // 타일 소유권 갱신은 TileOwnershipService.Tick()이 매 프레임 물리 위치 기반으로 처리한다.
            GameEvents.OnUnitMoved.OnNext(new UnitMovedEvent(unit.Id, from, to));
        }

        // ====================================================================
        // [2026-04-30] 새 규칙 5 — "앞쪽 빈 타일 찾기 (또는 대기)" 래퍼
        // ====================================================================

        /// <summary>
        /// 새 규칙 5 (점유가 가득 찬 타일 처리)에 맞춘 forward-only 래퍼.
        /// 기존 FindAvailableTile은 forward 필터 실패 시 fallback BFS(필터 OFF)로 측후방까지 후보를
        /// 넓혀 어떤 빈 타일이라도 찾아주었다. 그러나 새 규칙은 "뒤로 우회 절대 금지, 앞쪽이 없으면
        /// 대기"가 명시이므로, fallback 없이 forward-only BFS만 호출한다.
        ///
        /// 동작:
        ///   1) preferred가 들어갈 수 있으면 그대로 반환.
        ///   2) preferred가 막혀 있으면, destination 방향으로 더 가까운(또는 같은) walkable 타일 중
        ///      빈 타일을 BFS로 탐색하여 가장 가까운 후보를 반환.
        ///   3) 앞쪽에 빈 타일이 없으면 null — 호출 측은 다음 프레임에 다시 시도하여 빈자리가 생길 때까지 대기.
        ///
        /// occupancyManager가 주입되지 않은 경우(레거시) 항상 preferred 반환 — 점유 체크 비활성화 동등 효과.
        /// </summary>
        /// <param name="preferred">원래 진입하고 싶었던 타일.</param>
        /// <param name="unitSize">유닛의 OccupancySize.</param>
        /// <param name="destination">최종 목적지(우회 방향 결정용). default 시 null이 반환된다.</param>
        /// <param name="currentTile">
        /// [BUG-008 — 2026-05-06] 호출 유닛의 현재 논리 타일 좌표(prevActualTile).
        /// BFS visited에 미리 등록되어 "현재 위치 타일"이 우회 후보로 반환되는 것을 차단한다.
        /// 호출 측이 모를 때는 default(HexCoord)를 전달(이 경우 추가 제외 없음 — 기존 동작).
        /// </param>
        /// <returns>들어갈 수 있는 앞쪽 타일 좌표 또는 null(=대기).</returns>
        public HexCoord? FindForwardAvailable(HexCoord preferred, float unitSize, HexCoord destination, HexCoord currentTile)
        {
            // [2026-05-11 비활성화 — 점유 시스템 폐기]
            // 항상 preferred 그대로 반환 — 새 규칙에서는 우회를 수행하지 않습니다.
            // 호출부 호환을 위해 시그니처는 유지합니다.
            return preferred;
        }

        // ====================================================================
        // [2026-04-30] 새 규칙 11/15 — 전투 종료 후 "앞쪽 가장 가까운 타일" 찾기
        // ====================================================================

        /// <summary>
        /// 유닛의 현재 월드 위치를 기준으로 finalTarget 방향 앞쪽(=목적지 쪽)에서
        /// 가장 가까운 walkable 타일을 반환한다. 근접 유닛이 공격 슬롯을 풀고 A*를 재개할 때 사용.
        ///
        /// 동작:
        ///   1) 현재 월드 좌표(unitWorldPosDomain)에서 가장 가까운 타일을 nearestTile로 잡는다.
        ///      (HexMetrics.WorldToHex 사용 — 도메인 좌표 기준)
        ///   2) HexCoord.Distance(nearestTile, finalTarget) 보다 더 가까운(또는 같은) walkable 타일을
        ///      6방향 인접 후보 중에서 찾는다.
        ///   3) 후보가 있으면 그 중 unitWorldPosDomain에서 가장 가까운 타일(중심간 거리)을 반환.
        ///   4) 없으면 nearestTile 그대로 반환 (이미 앞쪽이거나, 그리드 끝).
        ///
        /// 좌표 인자는 도메인 좌표(ViewConverter.FromView로 미리 변환된 값)를 받는다.
        /// 이 메서드 내부에선 ViewConverter를 호출하지 않으므로, 호출 측에서 적절히 변환할 것.
        /// </summary>
        /// <param name="unitWorldPosDomain">유닛의 현재 도메인 월드 좌표 (XZ 평면, Y 무시).</param>
        /// <param name="finalTarget">최종 목적지 좌표 (예: 적 성).</param>
        /// <returns>앞쪽 가장 가까운 walkable 타일 좌표.</returns>
        public HexCoord FindForwardClosestTile(Vector3 unitWorldPosDomain, HexCoord finalTarget)
        {
            // 1) 현재 위치에서 가장 가까운 타일.
            HexCoord nearestTile = HexMetrics.WorldToHex(unitWorldPosDomain);

            // 그리드에 존재하지 않는 좌표인 경우 (스폰 직후 등 극단적인 상황) 안전하게 폴백.
            if (_grid == null || !_grid.HasTile(nearestTile))
            {
                return nearestTile;
            }

            int currentDist = HexCoord.Distance(nearestTile, finalTarget);

            // 2) 6방향 인접 후보 중 forward(같은 거리 또는 더 가까운) walkable 타일 후보 수집.
            //    HexMetrics.GetNeighbors는 부재 → HexDirectionExtensions로 직접 순회.
            HexCoord bestTile = nearestTile;
            float bestDistSq = float.MaxValue;
            bool foundForward = false;

            for (int i = 0; i < HexDirectionExtensions.Count; i++)
            {
                HexCoord neighbor = ((HexDirection)i).Neighbor(nearestTile);

                // 그리드 밖이거나 walkable이 아니면 후보 제외.
                if (!_grid.HasTile(neighbor)) continue;
                HexTile tile = _grid.GetTile(neighbor);
                if (tile == null || !tile.IsWalkable) continue;

                // 새 규칙 15: 뒤쪽 타일 절대 금지 → "현재 거리 미만"만 허용 (strict less).
                // HexCoord.Distance는 정수 도메인 거리이므로 부동소수점 오차 없음.
                int neighborDist = HexCoord.Distance(neighbor, finalTarget);
                if (neighborDist >= currentDist) continue;

                // 후보 중 unitWorldPosDomain에서 가장 가까운 타일 우선 (XZ 평면 거리).
                Vector3 neighborWorld = HexMetrics.HexToWorld(neighbor);
                float dx = neighborWorld.x - unitWorldPosDomain.x;
                float dz = neighborWorld.z - unitWorldPosDomain.z;
                float distSq = dx * dx + dz * dz;

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestTile = neighbor;
                    foundForward = true;
                }
            }

            // 3) forward 후보 없으면 nearestTile 그대로 (이미 앞쪽이거나 그리드 끝).
            return foundForward ? bestTile : nearestTile;
        }
    }
}
