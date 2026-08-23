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
using Hexiege.Domain;
using Hexiege.Application.Combat.Sequencing;

namespace Hexiege.Application
{
    public class UnitMovementUseCase : IDisposable
    {
        // 경로 계산 및 타일 점령에 사용할 그리드 참조
        private readonly HexGrid _grid;

        // 플로우 필드 캐시 서비스 — 목적지별 BFS 결과를 공유한다.
        private readonly FlowFieldService _flowFieldService;

        // 월드↔헥스 좌표 변환기. Core(HexMetrics) 의존을 피하기 위한 인터페이스 주입.
        private readonly IHexCoordinateMapper _mapper;

        // 이동 시 위치 역인덱스 동기화를 위해 UnitSpawnUseCase 참조 보관.
        // ProcessStep에서 unit.Position 갱신 직후 _unitSpawn.NotifyUnitMoved(from, to) 호출.
        private readonly UnitSpawnUseCase _unitSpawn;

        public UnitMovementUseCase(HexGrid grid, UnitSpawnUseCase unitSpawn,
            FlowFieldService flowFieldService, IHexCoordinateMapper mapper)
        {
            _grid = grid;
            _unitSpawn = unitSpawn;
            _flowFieldService = flowFieldService;
            _mapper = mapper;
        }

        /// <summary>
        /// 구독 해제. 게임 종료/재경기 시 GameBootstrapper가 호출.
        /// 현재 구독 대상이 없으므로 본문은 비어 있으나, IDisposable 계약과
        /// 외부 호출부(_unitMovement?.Dispose()) 호환을 위해 메서드는 유지한다.
        /// </summary>
        public void Dispose()
        {
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
            if (unit == null) return null;
            return RequestMoveFrom(unit.Position, target);
        }

        /// <summary>
        /// 도메인 유닛 상태를 변경하지 않고 명시한 출발 타일에서 목표까지 경로만 계산한다.
        /// 이동 중 Root보다 앞선 타일의 후속 경로를 준비할 때 사용하며, 이 메서드 호출만으로
        /// UnitData.Position, 위치 역인덱스 또는 이동 이벤트가 바뀌어서는 안 된다.
        /// 실제 위치 커밋은 waypoint 소비와 독립적으로, UnitView가 관측한 Root 경계 통과를
        /// 공용 spatial transition API에 전달해 수행한다.
        /// </summary>
        /// <param name="start">경로 계산에만 사용할 명시적 출발 타일</param>
        /// <param name="target">목표 타일 좌표</param>
        /// <returns>start를 포함한 경로. 이동 불가 또는 이미 도착한 경우 null.</returns>
        public List<HexCoord> RequestMoveFrom(HexCoord start, HexCoord target)
        {
            // FlowFieldService 미주입 방어 — Bootstrap 와이어링 누락 시 명시적 null 반환.
            if (_flowFieldService == null) return null;

            HexFlowField field = _flowFieldService.GetOrCompute(target);
            if (field == null) return null;

            List<HexCoord> path = field.GetPath(start);

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
        /// 호출자 버퍼에 담긴 공간 전이가 현재 커밋 타일부터 끊김 없이 인접하는지 검사한다.
        /// 컬렉션을 생성하거나 유닛 상태를 변경하지 않는 순수 검증 seam이다.
        /// </summary>
        public static bool IsValidSpatialTransitionChain(
            HexCoord committedTile,
            IReadOnlyList<HexCoord> transitions,
            int transitionCount)
        {
            if (transitions == null
                || transitionCount < 0
                || transitionCount > transitions.Count)
            {
                return false;
            }

            HexCoord previous = committedTile;
            for (int index = 0; index < transitionCount; index++)
            {
                HexCoord next = transitions[index];
                if (HexCoord.Distance(previous, next) != 1)
                    return false;

                previous = next;
            }

            return true;
        }

        /// <summary>
        /// 공간 전이 전체를 커밋 전에 검사한다. 실패하거나 성공해도 이 단계에서는
        /// UnitData, 위치 인덱스, 이동 이벤트를 변경하지 않는다.
        /// </summary>
        public bool TryPreflightSpatialTransitions(
            UnitData unit,
            IReadOnlyList<HexCoord> transitions,
            int transitionCount)
            => PreflightSpatialTransitions(
                unit, transitions, transitionCount).IsSuccess;

        public UnitSpatialPreflightResult PreflightSpatialTransitions(
            UnitData unit,
            IReadOnlyList<HexCoord> transitions,
            int transitionCount)
        {
            if (unit == null || !unit.IsAlive || _grid == null || _unitSpawn == null)
            {
                return new UnitSpatialPreflightResult(
                    UnitSpatialPreflightStatus.InvalidUnitOrDependency,
                    default,
                    -1);
            }
            if (transitions == null
                || transitionCount < 0
                || transitionCount > transitions.Count)
            {
                return new UnitSpatialPreflightResult(
                    UnitSpatialPreflightStatus.InvalidChain,
                    default,
                    -1);
            }

            HexCoord previous = unit.Position;
            for (int index = 0; index < transitionCount; index++)
            {
                HexCoord next = transitions[index];
                if (HexCoord.Distance(previous, next) != 1)
                {
                    return new UnitSpatialPreflightResult(
                        UnitSpatialPreflightStatus.InvalidChain,
                        next,
                        index);
                }
                previous = next;
            }

            for (int index = 0; index < transitionCount; index++)
            {
                HexTile tile = _grid.GetTile(transitions[index]);
                UnitSpatialPreflightResult tileResult = ClassifySpatialTile(
                    tile != null,
                    tile != null && tile.IsWalkable,
                    transitions[index],
                    index);
                if (!tileResult.IsSuccess)
                    return tileResult;
            }

            return UnitSpatialPreflightResult.Success();
        }

        /// <summary>
        /// grid 조회 자체와 typed 의미를 분리한 순수 seam이다. Missing/NonWalkable의
        /// offending tile/index 보존을 Unity scene 없이 결정적으로 검증할 수 있다.
        /// </summary>
        public static UnitSpatialPreflightResult ClassifySpatialTile(
            bool tileExists,
            bool tileWalkable,
            HexCoord tile,
            int transitionIndex)
        {
            if (!tileExists)
            {
                return new UnitSpatialPreflightResult(
                    UnitSpatialPreflightStatus.MissingTile,
                    tile,
                    transitionIndex);
            }
            if (!tileWalkable)
            {
                return new UnitSpatialPreflightResult(
                    UnitSpatialPreflightStatus.NonWalkable,
                    tile,
                    transitionIndex);
            }
            return UnitSpatialPreflightResult.Success();
        }

        /// <summary>
        /// 바로 앞에서 성공한 preflight 결과를 커밋한다. 모든 Position/위치 역인덱스를 먼저
        /// 순서대로 반영한 뒤 OnUnitMoved 이벤트를 두 번째 순회에서 발행한다.
        /// 입력 실패 반환 경로는 없으며 예상 밖 예외는 호출자가 기록하고 다시 던진다.
        /// </summary>
        public void CommitPreflightedSpatialTransitionsPreservingFacing(
            UnitData unit,
            IReadOnlyList<HexCoord> transitions,
            int transitionCount)
        {
            HexCoord source = unit.Position;
            HexDirection preservedFacing = unit.Facing;
            try
            {
                for (int index = 0; index < transitionCount; index++)
                {
                    HexCoord from = index == 0
                        ? source
                        : transitions[index - 1];
                    HexCoord to = transitions[index];
                    unit.Position = to;
                    _unitSpawn.NotifyUnitMoved(unit, from, to);
                }

                for (int index = 0; index < transitionCount; index++)
                {
                    HexCoord from = index == 0
                        ? source
                        : transitions[index - 1];
                    HexCoord to = transitions[index];
                    GameEvents.OnUnitMoved.OnNext(
                        new UnitMovedEvent(unit.Id, from, to));
                }
            }
            finally
            {
                unit.Facing = preservedFacing;
            }
        }

        /// <summary>
        /// Legacy waypoint 의미로 경로 상의 타일 하나를 이동 처리한다.
        /// ReducerAuthoritative 경로는 이 메서드가 아니라 공간 전이 preflight/commit API를 사용한다.
        ///
        /// 처리 내용:
        ///   1. 이동 방향 계산 → UnitData.Facing 업데이트
        ///   2. UnitData.Position 업데이트
        ///   3. 이동 이벤트 발행
        ///
        /// 타일 소유권 갱신은 TileOwnershipService.Tick()이 매 프레임 물리 위치 기반으로 처리한다.
        /// </summary>
        /// <param name="unit">이동 중인 유닛</param>
        /// <param name="from">출발 타일 좌표</param>
        /// <param name="to">도착 타일 좌표</param>
        public void ProcessStep(UnitData unit, HexCoord from, HexCoord to)
        {
            // 이동 방향 계산 → 스프라이트 방향 전환에 사용
            HexDirection dir = FacingDirection.FromCoords(from, to);
            unit.Facing = dir;

            // 유닛 위치 업데이트
            unit.Position = to;

            // 위치 역인덱스 동기화 — UnitSpawnUseCase._unitsByPosition을 from→to로 갱신.
            // GetUnitAt(coord) O(1) 조회를 위해 두 자료구조의 무결성을 보장한다.
            _unitSpawn?.NotifyUnitMoved(unit, from, to);

            // 이벤트 발행 → UnitView가 스프라이트 이동
            // 타일 소유권 갱신은 TileOwnershipService.Tick()이 매 프레임 물리 위치 기반으로 처리한다.
            GameEvents.OnUnitMoved.OnNext(new UnitMovedEvent(unit.Id, from, to));
        }

        // ====================================================================
        // 새 규칙 11/15 — 전투 종료 후 "앞쪽 가장 가까운 타일" 찾기
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
            HexCoord nearestTile = _mapper.WorldToHex(unitWorldPosDomain);

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
                Vector3 neighborWorld = _mapper.HexToWorld(neighbor);
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
