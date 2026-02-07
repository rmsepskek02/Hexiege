// ============================================================================
// UnitCombatUseCase.cs
// 유닛 전투(공격/피격/사망)를 처리하는 UseCase.
//
// 흐름:
//   1. 유닛 이동 완료 후 UnitView가 TryAttack() 호출
//   2. 인접 타일에서 적 유닛 탐색 (사거리 내)
//   3. 적이 있으면 공격 방향 계산 → 데미지 적용 → 이벤트 발행
//   4. 타겟 HP가 0 이하면 사망 이벤트 발행
//
// Application 레이어 — Domain에 의존.
// ============================================================================

using Hexiege.Domain;

namespace Hexiege.Application
{
    public class UnitCombatUseCase
    {
        private readonly HexGrid _grid;
        private readonly UnitSpawnUseCase _unitSpawn;

        public UnitCombatUseCase(HexGrid grid, UnitSpawnUseCase unitSpawn)
        {
            _grid = grid;
            _unitSpawn = unitSpawn;
        }

        /// <summary>
        /// 유닛의 사거리 내에 적이 있으면 공격을 실행.
        /// 이동 완료 후 UnitView에서 호출.
        /// </summary>
        /// <returns>공격이 발생했으면 true</returns>
        public bool TryAttack(UnitData attacker)
        {
            if (attacker == null || !attacker.IsAlive) return false;

            UnitData target = FindEnemyInRange(attacker);
            if (target == null) return false;

            ExecuteAttack(attacker, target);
            return true;
        }

        /// <summary>
        /// 공격자의 사거리 내에서 가장 가까운 적 유닛을 탐색.
        /// 현재는 사거리 1 (인접 타일)만 지원.
        /// </summary>
        private UnitData FindEnemyInRange(UnitData attacker)
        {
            // 인접 6방향 타일에서 적 유닛 탐색
            for (int i = 0; i < HexDirectionExtensions.Count; i++)
            {
                HexDirection dir = (HexDirection)i;
                HexCoord neighborCoord = dir.Neighbor(attacker.Position);

                if (!_grid.HasTile(neighborCoord)) continue;

                UnitData unitAtTile = _unitSpawn.GetUnitAt(neighborCoord);
                if (unitAtTile != null && unitAtTile.IsAlive && unitAtTile.Team != attacker.Team)
                {
                    return unitAtTile;
                }
            }

            return null;
        }

        /// <summary>
        /// 공격 실행. 데미지 적용 후 이벤트 발행.
        /// </summary>
        private void ExecuteAttack(UnitData attacker, UnitData target)
        {
            // 공격 방향 계산
            HexDirection attackDir = FacingDirection.FromCoords(attacker.Position, target.Position);
            attacker.Facing = attackDir;

            // 데미지 적용
            int damage = attacker.AttackPower;
            target.Hp -= damage;

            // 공격 이벤트 발행 → UnitView가 공격 애니메이션 재생
            GameEvents.OnUnitAttack.OnNext(new UnitAttackEvent(
                attacker.Id, target.Id, damage, attackDir));

            // 타겟 사망 처리
            if (!target.IsAlive)
            {
                _unitSpawn.RemoveUnit(target.Id);
                GameEvents.OnUnitDied.OnNext(new UnitDiedEvent(target.Id));
            }
        }
    }
}
