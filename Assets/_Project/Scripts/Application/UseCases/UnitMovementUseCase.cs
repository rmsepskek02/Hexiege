// ============================================================================
// UnitMovementUseCase.cs
// 유닛 이동 명령을 처리하는 UseCase.
//
// 흐름:
//   1. InputHandler가 "유닛을 선택한 뒤 타일을 클릭"하면 이 UseCase 호출
//   2. HexPathfinder로 현재 위치 → 목표 타일 A* 경로 계산
//   3. 경로가 있으면 경로의 각 타일에 대해:
//      a. UnitData.Position 업데이트
//      b. UnitData.Facing 방향 업데이트
//      c. 타일 점령 (HexGrid.SetOwner)
//      d. GameEvents.OnUnitMoved 이벤트 발행
//      e. GameEvents.OnTileOwnerChanged 이벤트 발행
//   4. Presentation 레이어(UnitView)가 이벤트를 받아 시각적 이동 처리
//
// 이 UseCase는 논리적 이동만 담당 (데이터 변경 + 이벤트 발행).
// 실제 화면상 Lerp 이동 애니메이션은 UnitView(Presentation)가 처리.
//
// Application 레이어 — Domain에 의존, Unity에 직접 의존하지 않음.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public class UnitMovementUseCase
    {
        // 경로 계산 및 타일 점령에 사용할 그리드 참조
        private readonly HexGrid _grid;

        // 적 유닛 좌표를 차단 목록에 추가하기 위한 참조
        private readonly UnitSpawnUseCase _unitSpawn;

        public UnitMovementUseCase(HexGrid grid, UnitSpawnUseCase unitSpawn)
        {
            _grid = grid;
            _unitSpawn = unitSpawn;
        }

        /// <summary>
        /// 유닛에게 목표 타일로 이동 명령을 내림.
        ///
        /// 반환값: 이동 경로 (시작점 포함). 이동 불가 시 null.
        /// Presentation 레이어에서 이 경로를 받아 시각적 이동 처리.
        ///
        /// 경로 탐색 시 다른 유닛(아군/적군 무관)이 점유 중인 타일은 이동 불가로 처리하여 우회.
        /// </summary>
        /// <param name="unit">이동할 유닛 데이터</param>
        /// <param name="target">목표 타일 좌표</param>
        /// <returns>A* 경로 리스트. 이동 불가 시 null.</returns>
        public List<HexCoord> RequestMove(UnitData unit, HexCoord target)
        {
            // 차단 목록 구성:
            // - 모든 다른 유닛의 현재 Position (아군/적군 무관)
            // - 같은 팀 유닛의 ClaimedTile (이동 중 선점 타일, 아군끼리 겹침 방지)
            // - 적 팀의 ClaimedTile은 포함하지 않음 (전투로 해결)
            var blocked = new HashSet<HexCoord>();
            foreach (var other in _unitSpawn.Units.Values)
            {
                if (other.Id != unit.Id && other.IsAlive)
                {
                    blocked.Add(other.Position);

                    // 같은 팀의 ClaimedTile만 차단 (적 팀은 무시)
                    if (other.Team == unit.Team && other.ClaimedTile.HasValue)
                        blocked.Add(other.ClaimedTile.Value);
                }
            }

            // A* 경로 계산 (유닛 점유 타일 우회)
            List<HexCoord> path = HexPathfinder.FindPath(_grid, unit.Position, target, blocked);

            // 경로 없음 (목표가 이동 불가이거나 막혀있음)
            if (path == null || path.Count < 2)
                return null;

            return path;
        }

        /// <summary>
        /// 특정 타일이 같은 팀 유닛에 의해 차단되었는지 확인.
        /// MoveAlongPath에서 각 스텝 시작 전에 호출하여 실시간 검증.
        ///
        /// 체크 대상 (같은 팀만):
        ///   - 다른 유닛의 Position (이미 해당 타일에 위치)
        ///   - 다른 유닛의 ClaimedTile (해당 타일로 이동 중)
        /// 적 팀은 체크하지 않음 — 전투로 해결.
        /// </summary>
        public bool IsTileBlockedBySameTeam(UnitData unit, HexCoord target)
        {
            foreach (var other in _unitSpawn.Units.Values)
            {
                if (other.Id != unit.Id && other.IsAlive && other.Team == unit.Team)
                {
                    if (other.Position == target)
                        return true;
                    if (other.ClaimedTile.HasValue && other.ClaimedTile.Value == target)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 경로 상의 타일 하나를 이동 처리.
        /// UnitView의 코루틴에서 타일→타일 Lerp 이동이 끝날 때마다 호출.
        ///
        /// 처리 내용:
        ///   1. 이동 방향 계산 → UnitData.Facing 업데이트
        ///   2. UnitData.Position 업데이트
        ///   3. 도착 타일을 유닛의 팀으로 점령
        ///   4. 이동/점령 이벤트 발행
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

            // 타일 점령: 이동한 타일을 유닛의 팀 색상으로 변경
            _grid.SetOwner(to, unit.Team);

            // 이벤트 발행 → UnitView가 스프라이트 이동, HexTileView가 색상 변경
            GameEvents.OnUnitMoved.OnNext(new UnitMovedEvent(unit.Id, from, to));
            GameEvents.OnTileOwnerChanged.OnNext(new TileOwnerChangedEvent(to, unit.Team));
        }
    }
}
