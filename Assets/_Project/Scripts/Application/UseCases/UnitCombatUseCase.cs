// ============================================================================
// UnitCombatUseCase.cs
// 유닛 전투(공격/피격/사망)를 처리하는 UseCase.
//
// 흐름:
//   1. 유닛 이동 완료 후 UnitView가 TryAttack() 호출
//   2. 인접 타일에서 적 유닛 또는 건물을 탐색 (IDamageable)
//   3. 적이 있으면 공격 방향 계산 → 데미지 적용 → 이벤트 발행
//   4. 타겟 HP가 0 이하면 사망 이벤트 발행 (삭제는 각 Manager/UseCase가 처리)
//
// Application 레이어 — Domain에 의존.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 유닛 전투(공격/피격/사망)를 처리하는 UseCase.
    /// NetworkManager에 직접 의존하지 않고 NetworkContext 정적 홀더를 통해
    /// 멀티플레이 서버 전용 분기를 처리 (Application → Infrastructure 역방향 의존 방지).
    /// </summary>
    public class UnitCombatUseCase
    {
        private readonly HexGrid _grid;
        private readonly UnitSpawnUseCase _unitSpawn;
        private readonly BuildingPlacementUseCase _buildingPlacement;

        public UnitCombatUseCase(
            HexGrid grid, 
            UnitSpawnUseCase unitSpawn, 
            BuildingPlacementUseCase buildingPlacement)
        {
            _grid = grid;
            _unitSpawn = unitSpawn;
            _buildingPlacement = buildingPlacement;
        }

        /// <summary>
        /// 유닛의 사거리 내에 적이 있으면 공격을 실행.
        /// 이동 완료 후 UnitView에서 호출.
        ///
        /// 멀티플레이 모드에서는 서버만 전투를 처리하여 권위 있는 판정 보장.
        /// 클라이언트는 UnitView 애니메이션(시각 효과)만 처리하고 데미지는 서버에 위임.
        /// </summary>
        /// <returns>공격이 발생했으면 true</returns>
        public bool TryAttack(UnitData attacker)
        {
            if (attacker == null || !attacker.IsAlive) return false;

            // 멀티플레이 모드에서는 서버만 전투 처리.
            // NetworkManager에 직접 의존하는 대신 NetworkContext 정적 홀더를 사용.
            // NetworkCombatController.OnNetworkSpawn()에서 NetworkContext.Set()을 호출하여 값 주입.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return false;

            // IDamageable을 구현하는 모든 적을 찾도록 로직 변경
            IDamageable target = FindFirstEnemyTarget(attacker);
            if (target == null) return false;

            ExecuteAttack(attacker, target);
            return true;
        }

        /// <summary>
        /// 사거리 내에 적이 존재하는지 판정만 수행 (데미지 없음, 네트워크 권한 체크 없음).
        /// 클라이언트 측 UnitView Lerp에서 시각적 전투 대기에 사용.
        /// 서버 권위 전투와 무관하게 적 존재 여부만 반환.
        /// </summary>
        public bool HasEnemyInRange(UnitData attacker)
        {
            if (attacker == null || !attacker.IsAlive) return false;
            return FindFirstEnemyTarget(attacker) != null;
        }

        /// <summary>
        /// 공격자의 사거리 내에서 가장 가까운 적(유닛 또는 건물)을 탐색.
        /// 모든 적 유닛과 건물을 대상으로 거리를 계산하여 가장 가까운 대상을 반환.
        /// </summary>
        private IDamageable FindFirstEnemyTarget(UnitData attacker)
        {
            IDamageable closestTarget = null;
            int minDistance = int.MaxValue;

            // 1. 모든 적 유닛 탐색
            foreach (var unit in _unitSpawn.Units.Values)
            {
                if (unit.Team == attacker.Team || !unit.IsAlive) continue;

                int distance = HexCoord.Distance(attacker.Position, unit.Position);

                if (distance <= attacker.AttackRange && distance < minDistance)
                {
                    minDistance = distance;
                    closestTarget = unit;
                }
            }

            // 2. 모든 적 건물 탐색
            foreach (var building in _buildingPlacement.Buildings.Values)
            {
                if (building.Team == attacker.Team || !building.IsAlive) continue;

                int distance = HexCoord.Distance(attacker.Position, building.Position);

                if (distance <= attacker.AttackRange && distance < minDistance)
                {
                    minDistance = distance;
                    closestTarget = building;
                }
            }

            return closestTarget;
        }

        /// <summary>
        /// 공격 실행. 데미지 적용 후 이벤트 발행.
        /// </summary>
        private void ExecuteAttack(UnitData attacker, IDamageable target)
        {
            // 공격 방향 계산
            HexDirection attackDir = FacingDirection.FromCoords(attacker.Position, target.Position);
            attacker.Facing = attackDir;

            // 데미지 적용 (인터페이스의 메서드 호출)
            target.TakeDamage(attacker.AttackPower);

            // 일반화된 공격 이벤트 발행
            GameEvents.OnEntityAttacked.OnNext(new EntityAttackedEvent(attacker, target));

            // 피격 이벤트 발행 — NetworkHealthSync가 구독하여 HP를 모든 클라이언트에 동기화
            bool targetIsUnit = target is UnitData;
            GameEvents.OnEntityDamaged.OnNext(
                new EntityDamagedEvent(target, target.Hp, targetIsUnit));

            // 타겟 사망 처리
            if (!target.IsAlive)
            {
                // 사망 이벤트 발행 → UnitView/BuildingView가 GameObject 파괴
                GameEvents.OnEntityDied.OnNext(new EntityDiedEvent(target));

                // 데이터 정리 — Dictionary에서 제거
                if (target is UnitData u)
                    _unitSpawn.RemoveUnit(u.Id);
                else if (target is BuildingData b)
                    _buildingPlacement.RemoveBuilding(b.Id);
            }
        }
    }
}
