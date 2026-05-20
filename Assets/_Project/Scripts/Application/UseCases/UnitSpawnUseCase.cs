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

        // ====================================================================
        // [2026-05-20] 위치 기반 역인덱스 (성능 최적화)
        // ====================================================================
        //
        // 동일 좌표에 여러 유닛이 겹칠 수 있으므로(슬롯 시스템 폐기로 겹침 허용),
        // Dictionary<HexCoord, List<UnitData>> 형태로 같은 타일 위 유닛들을 묶어 보관한다.
        // GetUnitAt(coord)는 List의 첫 항목을 반환 — InputHandler 등 "임의의 하나" 정책 호출자 보존.
        //
        // 동기화 책임:
        //   - SpawnUnit / SpawnUnitWithId: 등록 시 추가
        //   - RemoveUnit: 제거
        //   - 이동: UnitMovementUseCase.ProcessStep 이후 NotifyUnitMoved(unit, from, to)를 호출해야 한다
        //     (이중 자료구조 무결성을 보장하기 위한 단일 진입점)
        private readonly Dictionary<HexCoord, List<UnitData>> _unitsByPosition
            = new Dictionary<HexCoord, List<UnitData>>();

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

            // 이미 유닛이 있는 타일에는 생성 불가
            if (GetUnitAt(position) != null)
                return null;

            // UnitData 생성 (타입별 기본 스탯은 UnitStats에서 결정)
            // DetectRange: 근접유닛은 인접 타일(1.0) 감지, 원거리유닛은 AttackRange와 동일.
            var unit = new UnitData(
                type, team, position,
                UnitStats.GetMaxHp(type),
                UnitStats.GetAttackPower(type),
                UnitStats.GetAttackRange(type),
                UnitStats.GetDetectRange(type),
                UnitStats.GetMoveSpeed(type));

            // 내부 목록에 등록 + 위치 역인덱스 동기화
            _units[unit.Id] = unit;
            AddToPositionIndex(unit, position);

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
        ///
        /// [2026-05-20] 위치 역인덱스 도입으로 O(n) → O(1)로 단축.
        /// 동일 좌표에 여러 유닛이 있을 경우 List의 첫 항목을 반환 — 기존 "임의의 하나" 정책 유지.
        /// </summary>
        public UnitData GetUnitAt(HexCoord position)
        {
            if (_unitsByPosition.TryGetValue(position, out var list) && list.Count > 0)
                return list[0];
            return null;
        }

        /// <summary>
        /// 유닛을 목록에서 제거 (사망 처리 시).
        /// UnitCombatUseCase에서 HP가 0 이하가 된 유닛에 대해 호출.
        ///
        /// [2026-05-20] 위치 역인덱스도 함께 정리한다.
        /// </summary>
        public bool RemoveUnit(int unitId)
        {
            if (_units.TryGetValue(unitId, out var unit))
            {
                RemoveFromPositionIndex(unit, unit.Position);
            }
            return _units.Remove(unitId);
        }

        /// <summary>
        /// 유닛 이동 시 호출 — 위치 역인덱스 동기화 단일 진입점.
        /// UnitMovementUseCase.ProcessStep이 unit.Position을 갱신한 직후 반드시 호출해야 한다.
        ///
        /// from에서 제거 → to에 추가 순서. unit 인스턴스는 동일하므로 _units 본 컬렉션은 갱신 불필요.
        /// </summary>
        /// <param name="unit">이동한 유닛 (Position은 이미 to로 갱신됨)</param>
        /// <param name="from">이동 전 좌표</param>
        /// <param name="to">이동 후 좌표 (unit.Position과 동일해야 함)</param>
        public void NotifyUnitMoved(UnitData unit, HexCoord from, HexCoord to)
        {
            if (unit == null) return;
            if (from == to) return;
            RemoveFromPositionIndex(unit, from);
            AddToPositionIndex(unit, to);
        }

        // ====================================================================
        // 내부 헬퍼 — 위치 역인덱스 갱신
        // ====================================================================

        /// <summary>
        /// 지정 좌표의 유닛 리스트에 유닛 추가. 리스트가 없으면 생성한다.
        /// </summary>
        private void AddToPositionIndex(UnitData unit, HexCoord coord)
        {
            if (!_unitsByPosition.TryGetValue(coord, out var list))
            {
                list = new List<UnitData>(1);
                _unitsByPosition[coord] = list;
            }
            // 중복 방지 — 같은 인스턴스가 두 번 들어가지 않도록
            if (!list.Contains(unit))
                list.Add(unit);
        }

        /// <summary>
        /// 지정 좌표의 유닛 리스트에서 유닛 제거. 리스트가 비면 키도 제거한다.
        /// </summary>
        private void RemoveFromPositionIndex(UnitData unit, HexCoord coord)
        {
            if (!_unitsByPosition.TryGetValue(coord, out var list)) return;
            list.Remove(unit);
            if (list.Count == 0)
                _unitsByPosition.Remove(coord);
        }

        /// <summary>
        /// 네트워크 클라이언트 측 재생성 전용.
        /// 서버에서 발급된 unitId를 그대로 사용하여 UnitData를 생성하고
        /// GameEvents.OnUnitSpawned를 발행한다.
        /// 검증(IsWalkable, 중복 유닛)은 서버에서 이미 통과했으므로 생략.
        /// </summary>
        /// <param name="unitId">서버에서 발급된 유닛 Id</param>
        /// <param name="type">유닛 종류</param>
        /// <param name="team">소속 팀</param>
        /// <param name="position">생성 위치 (헥스 좌표)</param>
        /// <returns>생성된 UnitData. 타일이 그리드에 없으면 null.</returns>
        public UnitData SpawnUnitWithId(int unitId, UnitType type, TeamId team, HexCoord position)
        {
            // 타일 존재 여부만 확인 (서버에서 이미 검증 완료)
            HexTile tile = _grid.GetTile(position);
            if (tile == null)
            {
                UnityEngine.Debug.LogWarning($"[Network] SpawnUnitWithId: 타일 없음. coord={position}");
                return null;
            }

            // 서버 발급 Id로 UnitData 재생성
            // DetectRange: 근접유닛은 인접 타일(1.0) 감지, 원거리유닛은 AttackRange와 동일.
            var unit = new UnitData(
                unitId, type, team, position,
                UnitStats.GetMaxHp(type),
                UnitStats.GetAttackPower(type),
                UnitStats.GetAttackRange(type),
                UnitStats.GetDetectRange(type),
                UnitStats.GetMoveSpeed(type));

            _units[unit.Id] = unit;
            // 위치 역인덱스 동기화 (싱글플레이 SpawnUnit과 동일 처리)
            AddToPositionIndex(unit, position);

            // 타일 소유권 설정
            _grid.SetOwner(position, team);

            // 이벤트 발행 → UnitFactory 프리팹 생성, HexTileView 색상 갱신
            GameEvents.OnUnitSpawned.OnNext(new UnitSpawnedEvent(unit));
            GameEvents.OnTileOwnerChanged.OnNext(new TileOwnerChangedEvent(position, team));

            return unit;
        }
    }
}
