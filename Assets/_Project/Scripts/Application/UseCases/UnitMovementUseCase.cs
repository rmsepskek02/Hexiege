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
using UniRx;
using Hexiege.Domain;

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

        // 타일별 점유량 추적 — 좁은 타일에 너무 많이 모이는 것을 막는 데 사용.
        // null 허용(레거시 호환). null이면 점유 체크가 무력화되어 기존과 동일한 동작.
        private readonly TileOccupancyManager _occupancyManager;

        // 사망/제거 이벤트 구독 해제용 — 점유 정보 정리에 필요.
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();

        public UnitMovementUseCase(HexGrid grid, UnitSpawnUseCase unitSpawn,
            FlowFieldService flowFieldService,
            TileOccupancyManager occupancyManager = null)
        {
            _grid = grid;
            _unitSpawn = unitSpawn;
            _flowFieldService = flowFieldService;
            _occupancyManager = occupancyManager;

            // 유닛 사망 시 점유 자동 해제 — UnitView가 명시적으로 해제하지 않아도 누수 방지.
            // 클라이언트 코드 흐름에서 빠지더라도 도메인 이벤트만 도착하면 정합성이 유지된다.
            if (_occupancyManager != null)
            {
                _subscriptions.Add(GameEvents.OnEntityDied.Subscribe(evt =>
                {
                    if (evt.Entity is UnitData unit)
                    {
                        float size = GetOccupancySize(unit.Type);
                        _occupancyManager.OnUnitRemoved(unit.Position, size);
                    }
                }));
            }
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
            // [4차 개선 — 2026-04-26] FROM 타일 점유 해제.
            // RegisterOccupancyMove는 TO+1만 예약했으므로, 여기서 FROM-1을 처리한다.
            // 유닛이 Lerp를 완료하여 실제로 FROM을 떠난 시점이므로 타이밍이 정확하다.
            //
            // 처리 조건:
            //   - from != to: 같은 타일 이동(Phase 2 스냅 등)은 점유 변동이 없어야 함.
            //     RegisterOccupancyMove 측도 TO==FROM이면 결과적으로 +1만 발생하지만,
            //     Phase 2 스냅 같은 케이스에서는 RegisterOccupancyMove를 별도 호출하므로
            //     여기서 FROM 해제까지 함께 일어나면 점유가 누락된다.
            //   - _occupancyManager != null: 서버에서만 동작, null 방어.
            //   - OnUnitRemoved 내부에 IsInvalid 체크 있음 → 스폰(from=default) 안전.
            if (from != to && _occupancyManager != null)
            {
                _occupancyManager.OnUnitRemoved(from, GetOccupancySize(unit.Type));
            }

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

        /// <summary>
        /// [4차 개선 — 2026-04-26]
        /// 다음 타일로의 점유를 Lerp 시작 직전에 "예약"만 한다(TO+1).
        /// FROM 타일 해제는 더 이상 이 메서드에서 하지 않는다.
        ///
        /// 변경 이유 (3차 → 4차):
        ///   3차 개선에서는 Lerp 직전에 OnUnitMoved(from, to)를 호출하여 FROM-1, TO+1을
        ///   동시에 처리했다. 그러나 유닛이 Lerp 도중 적을 만나 이동을 중단하면
        ///   FROM 점유는 이미 빠진 상태이지만 유닛은 물리적으로 아직 FROM 타일 위에 있다.
        ///   → 다른 유닛들이 "FROM이 비었다"고 잘못 판정하여 몰려들고, 한 타일에
        ///     OccupancySize 합계가 3을 초과해 시각적으로 완전히 겹치는 현상이 다시 나타났다.
        ///
        /// 4차 개선의 분리 방식:
        ///   - Lerp 시작 직전 (이 메서드): TO 타일에만 +1 (도착 예약).
        ///   - Lerp 완료 직후 (ProcessStep): FROM 타일에서 -1 (실제로 떠난 시점).
        ///   이렇게 분리하면 "이동을 중단해도 FROM은 여전히 점유 중"이라는 사실이 유지되어
        ///   다른 유닛이 잘못 몰려드는 현상이 사라진다.
        ///
        /// 호출 약속:
        ///   - 시그니처(from 파라미터)는 UnitView 호출부 호환을 위해 유지하지만 실제로는 사용하지 않는다.
        ///   - to는 FindAvailableTile로 결정된 actualTo.
        ///   - to == default(HexCoord)이면 ReserveOccupancy 내부 IsInvalid 가드로 무시된다.
        /// </summary>
        public void RegisterOccupancyMove(HexCoord from, HexCoord to, UnitType type)
        {
            if (_occupancyManager == null) return;
            float size = GetOccupancySize(type);
            // TO 타일만 예약 — FROM 해제는 ProcessStep에서 처리한다.
            _occupancyManager.ReserveOccupancy(to, size);
        }

        /// <summary>
        /// [3차 개선 — 2026-04-25]
        /// 등록된 점유를 해제. 사망/이동 중단/감지 인터럽트 등으로 Lerp가 완료되지 못한 경우
        /// UnitView가 "이 타일은 더 이상 안 들어간다"고 신고하여 누수를 방지한다.
        ///
        /// 호출 약속:
        ///   - tile은 RegisterOccupancyMove의 to로 등록한 그 타일이어야 한다.
        ///   - 같은 타일을 두 번 해제하지 않도록 호출 측에서 _pendingOccupancyTile을 default로 리셋해야 함.
        /// </summary>
        public void ReleaseOccupancy(HexCoord tile, UnitType type)
        {
            if (_occupancyManager == null) return;
            float size = GetOccupancySize(type);
            _occupancyManager.OnUnitRemoved(tile, size);
        }

        /// <summary>
        /// 유닛 타입의 OccupancySize 조회.
        /// UnitView에서 다음 타일 진입 가능 여부를 판단할 때 사용한다.
        /// UnitStats를 통해 ScriptableObject(UnitStatsConfig) 값이 반환된다.
        /// </summary>
        public float GetOccupancySize(UnitType type)
        {
            return UnitStats.GetOccupancySize(type);
        }

        /// <summary>
        /// preferred 타일에 unitSize를 받을 수 있는지 확인 후, 불가능하면
        /// BFS로 가장 가까운 빈 walkable 타일을 찾아 반환.
        /// 모두 가득 차면 null — 호출 측은 다음 프레임에 재시도해야 한다.
        ///
        /// OccupancyManager가 주입되지 않았다면(레거시 호환) 항상 preferred 그대로 반환 —
        /// 점유 체크가 비활성화된 것과 동일한 효과.
        ///
        /// destination 미지정 오버로드 — forward 필터가 적용되지 않아 "가장 가까운 빈 타일"이
        /// 그대로 반환된다(기존 동작). 호환을 위해 유지.
        /// </summary>
        /// <param name="preferred">원래 가고 싶었던 타일 좌표.</param>
        /// <param name="unitSize">이동하려는 유닛의 OccupancySize.</param>
        /// <returns>들어갈 수 있는 타일 좌표 또는 null.</returns>
        public HexCoord? FindAvailableTile(HexCoord preferred, float unitSize)
        {
            if (_occupancyManager == null) return preferred;
            return _occupancyManager.FindAvailableTile(preferred, unitSize, _grid);
        }

        /// <summary>
        /// [3차 개선 — 2026-04-25]
        /// destination 파라미터를 받는 오버로드.
        ///
        /// preferred에 들어갈 수 없을 때 BFS 우회 후보를 고를 때, 단순히 가장 가까운 빈 타일이 아니라
        /// "최종 목적지(destination)에서 더 멀어지지 않는" 타일을 우선 선택한다.
        /// 이로써 우회 후 후방으로 튕겨나가는 현상(팅김)이 줄어든다.
        ///
        /// destination을 default(HexCoord) 또는 preferred와 동일하게 넘기면 forward 필터가 비활성화되어
        /// 위의 단일 파라미터 오버로드와 같은 결과가 된다.
        /// </summary>
        public HexCoord? FindAvailableTile(HexCoord preferred, float unitSize, HexCoord destination)
        {
            if (_occupancyManager == null) return preferred;
            return _occupancyManager.FindAvailableTile(preferred, unitSize, _grid, destination);
        }
    }
}
