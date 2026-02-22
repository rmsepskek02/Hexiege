// ============================================================================
// BuildingData.cs
// 건물 하나의 상태 데이터.
//
// Domain 레이어의 데이터 클래스로, Unity 컴포넌트가 아님.
// UnitData와 동일한 패턴: 자동 Id 발급, 불변 코어 속성.
//
// 현재 범위 (건물 배치만):
//   - Id, Type, Team, Position
//
// 향후 확장 (MVP):
//   - HP, Level, 생산 큐, 업그레이드 상태 등
//
// 사용 예시:
//   var building = new BuildingData(BuildingType.Barracks, TeamId.Blue, coord);
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

namespace Hexiege.Domain
{
    // IDamageable 인터페이스 구현
    public class BuildingData : IDamageable
    {
        /// <summary> 건물 고유 식별자. 생성 시 자동 발급, 변경 불가. </summary>
        public int Id { get; }

        /// <summary> 건물 종류 (Castle, Barracks, MiningPost). 변경 불가. </summary>
        public BuildingType Type { get; }

        /// <summary> 소속 팀. 변경 불가. </summary>
        public TeamId Team { get; }

        /// <summary> 배치 위치 (헥스 좌표). 변경 불가. </summary>
        public HexCoord Position { get; }

        // --- 전투 스탯 추가 ---
        public int MaxHp { get; }
        public int Hp { get; private set; }
        public bool IsAlive => Hp > 0;

        // 건물 Id 자동 발급용 정적 카운터.
        private static int _nextId;

        /// <summary>
        /// 건물 생성. Id는 _nextId에서 자동 발급.
        /// </summary>
        /// <param name="type">건물 종류</param>
        /// <param name="team">소속 팀</param>
        /// <param name="position">배치 위치 (헥스 좌표)</param>
        /// <param name="maxHp">최대 체력</param>
        public BuildingData(BuildingType type, TeamId team, HexCoord position, int maxHp)
        {
            Id = _nextId++;
            Type = type;
            Team = team;
            Position = position;
            MaxHp = maxHp;
            Hp = maxHp;
        }

        /// <summary>
        /// 건물 생성 (네트워크 동기화용). 서버에서 발급한 Id를 명시적으로 지정.
        /// 클라이언트에서 서버 Id와 동일한 BuildingData를 재생성할 때 사용.
        /// _nextId를 지정 Id 이상으로 갱신하여 이후 자동 발급 Id와 충돌 방지.
        /// </summary>
        /// <param name="id">서버에서 할당된 건물 Id</param>
        /// <param name="type">건물 종류</param>
        /// <param name="team">소속 팀</param>
        /// <param name="position">배치 위치 (헥스 좌표)</param>
        /// <param name="maxHp">최대 체력</param>
        public BuildingData(int id, BuildingType type, TeamId team, HexCoord position, int maxHp)
        {
            Id = id;
            Type = type;
            Team = team;
            Position = position;
            MaxHp = maxHp;
            Hp = maxHp;

            // 자동 발급 카운터가 이 Id보다 낮으면 충돌 방지를 위해 앞으로 당김
            if (_nextId <= id)
                _nextId = id + 1;
        }

        // IDamageable 인터페이스 메서드 구현
        public void TakeDamage(int damage)
        {
            if (!IsAlive) return;
            Hp -= damage;
            if (Hp < 0) Hp = 0;
        }
    }
}
