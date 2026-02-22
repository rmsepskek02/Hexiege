// ============================================================================
// UnitData.cs
// 유닛 한 마리의 상태 데이터.
//
// Domain 레이어의 데이터 클래스로, Unity 컴포넌트가 아님.
// Presentation 레이어의 UnitView가 이 데이터를 참조하여 화면에 표시.
//
// 프로토타입 범위:
//   - 위치, 타입, 팀, 방향만 관리
//   - HP, 공격력 등 전투 관련 스탯은 MVP에서 추가
//
// Id는 정적 카운터(_nextId)로 자동 발급.
// 각 유닛이 고유한 Id를 가지므로 Dictionary 키, 이벤트 식별 등에 사용 가능.
//
// 사용 예시:
//   var unit = new UnitData(UnitType.Pistoleer, TeamId.Blue, startCoord);
//   unit.Position = newCoord;  // 이동
//   unit.Facing = HexDirection.SE;  // 방향 전환
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

namespace Hexiege.Domain
{
    // IDamageable 인터페이스 구현
    public class UnitData : IDamageable
    {
        /// <summary> 유닛 고유 식별자. 생성 시 자동 발급, 변경 불가. </summary>
        public int Id { get; }

        /// <summary> 유닛 종류 (Pistoleer 등). 생성 시 결정, 변경 불가. </summary>
        public UnitType Type { get; }

        /// <summary> 소속 팀 (Blue/Red). 생성 시 결정, 변경 불가. </summary>
        public TeamId Team { get; }

        /// <summary> 현재 위치 (헥스 좌표). 이동 시 업데이트. </summary>
        public HexCoord Position { get; set; }

        /// <summary>
        /// 현재 바라보는 방향 (HexDirection).
        /// 이동 시 이전→현재 타일 방향으로 업데이트.
        /// FacingDirection.FromHexDirection()으로 아트 방향+flipX 변환.
        /// </summary>
        public HexDirection Facing { get; set; }

        // ====================================================================
        // 전투 스탯
        // ====================================================================

        /// <summary> 최대 체력. </summary>
        public int MaxHp { get; }

        /// <summary> 현재 체력. 0 이하면 사망. </summary>
        public int Hp { get; private set; }

        /// <summary> 공격력. </summary>
        public int AttackPower { get; }

        /// <summary> 공격 사거리 (타일 단위). 권총병 = 1. </summary>
        public int AttackRange { get; }

        /// <summary> 타일 1칸 이동 소요 시간(초). 작을수록 빠름. </summary>
        public float MoveSeconds { get; }

        /// <summary> 유닛이 살아있는지 여부. </summary>
        public bool IsAlive => Hp > 0;

        /// <summary>
        /// 이동 중 선점한 타일 좌표. Lerp 시작 전에 설정, 완료 후 해제.
        /// 같은 팀 유닛의 경로탐색 시 이동 불가 타일로 사용.
        /// 적 팀에게는 영향 없음 (전투로 해결).
        /// </summary>
        public HexCoord? ClaimedTile { get; set; }

        // 유닛 Id 자동 발급용 정적 카운터.
        // 첫 유닛은 Id=0, 다음은 Id=1, ...
        private static int _nextId;

        /// <summary>
        /// 유닛 생성.
        /// </summary>
        /// <param name="type">유닛 종류</param>
        /// <param name="team">소속 팀</param>
        /// <param name="position">초기 위치 (헥스 좌표)</param>
        /// <param name="maxHp">최대 체력</param>
        /// <param name="attackPower">공격력</param>
        /// <param name="attackRange">공격 사거리</param>
        /// <param name="moveSeconds">타일 1칸 이동 소요 시간(초)</param>
        /// <param name="facing">초기 바라보는 방향 (기본: 동쪽)</param>
        public UnitData(UnitType type, TeamId team, HexCoord position,
            int maxHp, int attackPower, int attackRange,
            float moveSeconds = 0.3f,
            HexDirection facing = HexDirection.E)
        {
            Id = _nextId++;
            Type = type;
            Team = team;
            Position = position;
            MaxHp = maxHp;
            Hp = maxHp;
            AttackPower = attackPower;
            AttackRange = attackRange;
            MoveSeconds = moveSeconds;
            Facing = facing;
        }

        /// <summary>
        /// 네트워크 클라이언트 측 재생성 전용 생성자.
        /// 서버에서 발급된 Id를 그대로 사용하여 양쪽 Id가 동일하게 유지됨.
        /// _nextId를 id+1 이상으로 갱신하여 이후 자동 발급 Id와의 충돌을 방지.
        /// </summary>
        /// <param name="id">서버에서 발급된 유닛 Id</param>
        /// <param name="type">유닛 종류</param>
        /// <param name="team">소속 팀</param>
        /// <param name="position">초기 위치 (헥스 좌표)</param>
        /// <param name="maxHp">최대 체력</param>
        /// <param name="attackPower">공격력</param>
        /// <param name="attackRange">공격 사거리</param>
        /// <param name="moveSeconds">타일 1칸 이동 소요 시간(초)</param>
        /// <param name="facing">초기 바라보는 방향 (기본: 동쪽)</param>
        public UnitData(int id, UnitType type, TeamId team, HexCoord position,
            int maxHp, int attackPower, int attackRange,
            float moveSeconds = 0.3f,
            HexDirection facing = HexDirection.E)
        {
            Id = id;
            Type = type;
            Team = team;
            Position = position;
            MaxHp = maxHp;
            Hp = maxHp;
            AttackPower = attackPower;
            AttackRange = attackRange;
            MoveSeconds = moveSeconds;
            Facing = facing;

            // 지정 Id 이후로 자동 발급 카운터를 앞당겨 충돌 방지
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
