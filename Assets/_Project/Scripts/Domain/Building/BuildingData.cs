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
//   var building = new BuildingData(BuildingType.TrainingCamp, TeamId.Blue, coord);
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

        /// <summary>
        /// 건물의 단계 번호(1/2/3). 비생산건물(Castle/MiningPost 등)은 0.
        /// BuildingType에서 파생되므로 별도 저장 필드 없음.
        /// </summary>
        public int Stage => BuildingTypeHelper.GetStage(Type);

        // --- 전투 스탯 추가 ---
        public int MaxHp { get; }
        public int Hp { get; private set; }
        public bool IsAlive => Hp > 0;

        /// <summary>
        /// 방어력. 데미지 감쇄 공식(DamageCalculator.ApplyDefense)을 유닛·건물에 통일 적용하기 위해
        /// 건물에도 두는 필드다. 단 건물은 방어력 업그레이드 트랙이 없어 **항상 0**(실질 무감쇄)이며,
        /// 건물 방어 트랙은 이번 범위 밖(향후 확장 보류. GameSystemRules_Upgrade.md 규칙 5).
        /// </summary>
        public int Defense => 0;

        /// <summary>
        /// 방어 타워(AutoTower)의 남은 공격 쿨다운(초).
        /// 0 이하면 공격 가능, 공격 직후 AttackCooldown 값으로 리셋된다.
        ///
        /// 생성 시 0으로 초기화 — 배치 직후 사거리 안에 적이 있으면 딜레이 없이
        /// 즉시 첫 공격이 가능하다(방어 타워 시스템 규칙 4).
        ///
        /// UnitData.AttackCooldownRemaining과 동일한 패턴.
        /// 비타워 건물에서는 항상 0이며 아무 의미가 없다(TowerCombatUseCase가 타워만 순회).
        /// </summary>
        public float AttackCooldownRemaining { get; set; }

        // 건물 Id 자동 발급용 정적 카운터.
        private static int _nextId;

        /// <summary>
        /// 건물 생성. Id는 _nextId에서 자동 발급.
        /// ID 지정 생성자에 위임한다.
        /// </summary>
        /// <param name="type">건물 종류</param>
        /// <param name="team">소속 팀</param>
        /// <param name="position">배치 위치 (헥스 좌표)</param>
        /// <param name="maxHp">최대 체력</param>
        public BuildingData(BuildingType type, TeamId team, HexCoord position, int maxHp)
            : this(_nextId++, type, team, position, maxHp)
        {
            // 자동 Id 발급 후 ID 지정 생성자에 위임.
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

        /// <summary>
        /// 건물 체력을 회복시킨다(MistShrine 물안개 힐 등 건물 회복 경로 전용).
        ///
        /// 왜 이 메서드가 필요한가(초급자용 설명):
        ///   Hp는 `private set`이라 이 클래스 바깥에서는 직접 값을 넣을 수 없다.
        ///   지금까지 건물은 "맞아서 깎이기만" 했기 때문에 감소 경로(TakeDamage)만 있었는데,
        ///   MistShrine 물안개 힐이 아군 건물도 회복시키므로(GameSystemRules_Buildings.md
        ///   MistShrine 규칙 4·24) 증가 경로가 필요해졌다.
        ///
        /// 최대 체력 클램프를 왜 여기(도메인 안)에 두는가:
        ///   UnitData.Heal과 같은 위치·같은 형태로 맞춘다. 클램프가 도메인 안에 있으면
        ///   호출자가 어디든(싱글 서버 / 멀티 서버 / 멀티 클라 동기화) 최대 체력을 넘길 수 없어
        ///   "최대 체력 대상은 회복되지 않는다"(MistShrine 규칙 5)가 자동으로 성립한다.
        ///
        /// 파괴된 건물(Hp == 0)은 회복 대상이 아니므로 조용히 무시한다(부활 금지).
        /// </summary>
        /// <param name="amount">회복량(양수). 0 이하이면 아무 일도 하지 않는다.</param>
        public void Heal(int amount)
        {
            if (!IsAlive) return;
            if (amount <= 0) return;
            Hp += amount;
            if (Hp > MaxHp) Hp = MaxHp;
        }
    }
}
