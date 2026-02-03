// ============================================================================
// UnitSpawnUseCase.cs
// 유닛 생성 요청을 처리하는 UseCase.
//
// 흐름:
//   1. GameBootstrapper(테스트용) 또는 생산 시스템(MVP)이 유닛 생성 요청
//   2. 지정 좌표에 타일이 존재하고 이동 가능한지 확인
//   3. UnitData 인스턴스 생성 (Id 자동 발급)
//   4. 내부 유닛 목록에 등록
//   5. GameEvents.OnUnitSpawned 이벤트 발행
//   6. UnitFactory(Infrastructure)가 이벤트를 받아 프리팹 인스턴스 생성
//
// 유닛 관리:
//   생성된 모든 유닛을 Dictionary<int, UnitData>로 관리.
//   Id로 유닛을 조회/삭제할 수 있음.
//
// 프로토타입에서는 GameBootstrapper가 테스트용으로 직접 SpawnUnit 호출.
// MVP에서는 배럭 건물의 자동/수동 생산 시스템이 호출.
//
// Application 레이어 — Domain에 의존.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public class UnitSpawnUseCase
    {
        // 타일 존재 여부 확인용 그리드 참조
        private readonly HexGrid _grid;

        // 생성된 모든 유닛을 Id로 인덱싱하여 관리.
        // UnitView 생성, 유닛 조회, 삭제 등에 사용.
        private readonly Dictionary<int, UnitData> _units = new Dictionary<int, UnitData>();

        /// <summary> 현재 존재하는 모든 유닛 목록 (읽기 전용). </summary>
        public IReadOnlyDictionary<int, UnitData> Units => _units;

        public UnitSpawnUseCase(HexGrid grid)
        {
            _grid = grid;
        }

        /// <summary>
        /// 유닛 생성 요청.
        ///
        /// 검증:
        ///   - 해당 좌표에 타일이 존재하는지
        ///   - 해당 타일이 이동 가능(IsWalkable)한지
        ///   (프로토타입에서는 인구수/자원 체크 생략)
        ///
        /// 성공 시: UnitData 생성 → 목록 등록 → 이벤트 발행 → UnitData 반환
        /// 실패 시: null 반환
        /// </summary>
        /// <param name="type">유닛 종류 (예: Pistoleer)</param>
        /// <param name="team">소속 팀</param>
        /// <param name="position">생성 위치 (헥스 좌표)</param>
        /// <returns>생성된 UnitData. 실패 시 null.</returns>
        public UnitData SpawnUnit(UnitType type, TeamId team, HexCoord position)
        {
            // 타일 존재 여부 확인
            HexTile tile = _grid.GetTile(position);
            if (tile == null || !tile.IsWalkable)
                return null;

            // UnitData 생성 (Id는 내부에서 자동 발급)
            var unit = new UnitData(type, team, position);

            // 내부 목록에 등록
            _units[unit.Id] = unit;

            // 타일을 유닛의 팀으로 점령
            _grid.SetOwner(position, team);

            // 이벤트 발행 → UnitFactory가 프리팹 생성, HexTileView가 색상 변경
            GameEvents.OnUnitSpawned.OnNext(new UnitSpawnedEvent(unit));
            GameEvents.OnTileOwnerChanged.OnNext(new TileOwnerChangedEvent(position, team));

            return unit;
        }

        /// <summary>
        /// Id로 유닛 조회.
        /// InputHandler에서 클릭된 타일 위의 유닛을 찾을 때 등에 사용.
        /// </summary>
        public UnitData GetUnit(int unitId)
        {
            _units.TryGetValue(unitId, out UnitData unit);
            return unit;
        }

        /// <summary>
        /// 특정 좌표에 있는 유닛을 찾아 반환.
        /// 타일 클릭 시 해당 위치의 유닛이 있는지 확인하는 데 사용.
        /// O(n) 탐색이지만 프로토타입 유닛 수가 적어 문제 없음.
        /// </summary>
        public UnitData GetUnitAt(HexCoord position)
        {
            foreach (var kvp in _units)
            {
                if (kvp.Value.Position == position)
                    return kvp.Value;
            }
            return null;
        }
    }
}
