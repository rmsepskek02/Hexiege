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

        /// <summary> 공격 사거리 (월드 단위). </summary>
        public float AttackRange { get; }

        /// <summary>
        /// 적 감지 사거리 (타일 단위).
        /// 근접유닛은 AttackRange보다 크게 설정되어 있으며(예: 0.5 → 1.0),
        /// 인접 타일의 적을 감지한 뒤 공격 사거리 내로 접근한다.
        /// 원거리유닛은 AttackRange와 동일 — 감지와 공격 사거리가 일치.
        /// </summary>
        public float DetectRange { get; }

        /// <summary> 타일 이동 속도 (칸/초). 높을수록 빠름. </summary>
        public float MoveSpeed { get; }

        /// <summary> 공격 1회 쿨다운(초). UnitFactory에서 Attack 클립 길이로 설정. </summary>
        public float AttackCooldown { get; set; }

        /// <summary> 남은 공격 쿨다운(초). 0 이하이면 공격 가능. 0으로 시작(즉시 공격 가능). </summary>
        public float AttackCooldownRemaining { get; set; }

        /// <summary>
        /// 공격 애니메이션에서 타격 프레임(OnAttackHit)까지의 시간 (초) 배열.
        /// 서버가 애니메이션 RPC 전송 후 배열의 각 시간마다 데미지를 개별 적용하는 기준값.
        /// UnitStats.GetHitFrameTimes()에서 타입별 기본값을 복사.
        ///
        /// 단일 히트 유닛은 원소가 1개. 다중 히트 유닛(FlameSpirit 6, LionKnight 2)은
        /// 각 히트 프레임을 오름차순으로 나열한 배열을 가진다.
        /// </summary>
        public float[] HitFrameTimes { get; set; }

        /// <summary> 유닛이 살아있는지 여부. </summary>
        public bool IsAlive => Hp > 0;

        /// <summary>
        /// [2026-05-11 비활성화 — 슬롯 시스템 폐기]
        /// 이동 중 선점한 타일 좌표. 새 규칙에서는 더 이상 경로탐색 차단에 사용되지 않습니다.
        /// 외부 코드(UnitView 등)에서 set/get 호출은 그대로 남아 있으나 동작에는 영향이 없습니다.
        /// 향후 완전 제거 예정. 시그니처 호환을 위해 프로퍼티 자체는 보존합니다.
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
        /// <param name="attackRange">공격 사거리 (월드 단위)</param>
        /// <param name="detectRange">적 감지 사거리 (타일 단위). 근접유닛은 AttackRange보다 크게 설정.</param>
        /// <param name="moveSpeed">타일 이동 속도 (칸/초). 높을수록 빠름.</param>
        /// <param name="facing">초기 바라보는 방향 (기본: 동쪽)</param>
        public UnitData(UnitType type, TeamId team, HexCoord position,
            int maxHp, int attackPower, float attackRange, float detectRange,
            float moveSpeed = 1.0f,
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
            DetectRange = detectRange;
            MoveSpeed = moveSpeed;
            AttackCooldown = UnitStats.GetAttackCooldown(type);
            AttackCooldownRemaining = 0f;
            HitFrameTimes = UnitStats.GetHitFrameTimes(type);
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
        /// <param name="attackRange">공격 사거리 (월드 단위)</param>
        /// <param name="detectRange">적 감지 사거리 (타일 단위). 근접유닛은 AttackRange보다 크게 설정.</param>
        /// <param name="moveSpeed">타일 이동 속도 (칸/초). 높을수록 빠름.</param>
        /// <param name="facing">초기 바라보는 방향 (기본: 동쪽)</param>
        public UnitData(int id, UnitType type, TeamId team, HexCoord position,
            int maxHp, int attackPower, float attackRange, float detectRange,
            float moveSpeed = 1.0f,
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
            DetectRange = detectRange;
            MoveSpeed = moveSpeed;
            AttackCooldown = UnitStats.GetAttackCooldown(type);
            AttackCooldownRemaining = 0f;
            HitFrameTimes = UnitStats.GetHitFrameTimes(type);
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
