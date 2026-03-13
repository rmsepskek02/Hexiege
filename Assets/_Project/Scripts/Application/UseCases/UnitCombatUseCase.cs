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
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Core;

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
        private readonly IEntityPositionProvider _positionProvider;

        public UnitCombatUseCase(
            HexGrid grid,
            UnitSpawnUseCase unitSpawn,
            BuildingPlacementUseCase buildingPlacement,
            IEntityPositionProvider positionProvider = null)
        {
            _grid = grid;
            _unitSpawn = unitSpawn;
            _buildingPlacement = buildingPlacement;
            _positionProvider = positionProvider;
        }

        /// <summary>
        /// 유닛의 사거리 내에 적이 있으면 공격을 실행.
        /// 이동 완료 후 UnitView에서 호출.
        ///
        /// 멀티플레이 모드에서는 서버만 전투를 처리하여 권위 있는 판정 보장.
        /// 클라이언트는 UnitView 애니메이션(시각 효과)만 처리하고 데미지는 서버에 위임.
        /// </summary>
        /// <returns>공격 성공 시 (타겟 Id, 타겟이 유닛인지 여부) 튜플, 실패 시 null</returns>
        public (int id, bool isUnit)? TryAttack(UnitData attacker)
        {
            if (attacker == null || !attacker.IsAlive) return null;

            // 멀티플레이 모드에서는 서버만 전투 처리.
            // NetworkManager에 직접 의존하는 대신 NetworkContext 정적 홀더를 사용.
            // NetworkCombatController.OnNetworkSpawn()에서 NetworkContext.Set()을 호출하여 값 주입.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return null;

            // 쿨다운 중이면 공격 불가
            if (attacker.AttackCooldownRemaining > 0f) return null;

            // IDamageable을 구현하는 모든 적을 찾도록 로직 변경
            IDamageable target = FindFirstEnemyTarget(attacker);
            if (target == null) return null;

            ExecuteAttack(attacker, target);

            // 공격 성공 → 쿨다운 시작
            attacker.AttackCooldownRemaining = attacker.AttackCooldown;

            return (target.Id, target is UnitData);
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
        ///
        /// _positionProvider가 있으면 월드 좌표(Vector3.Distance) 기반으로 판정.
        /// Lerp 중에도 실시간 Transform.position을 사용하므로 HexCoord 갱신 지연 문제 해결.
        /// _positionProvider가 null이면 기존 HexCoord.Distance 폴백.
        /// </summary>
        private IDamageable FindFirstEnemyTarget(UnitData attacker)
        {
            // _positionProvider가 없으면 기존 HexCoord 기반 판정으로 폴백
            if (_positionProvider == null)
                return FindFirstEnemyTargetByHexCoord(attacker);

            // 공격자 월드 좌표 취득
            Vector3 attackerWorldPos = _positionProvider.GetUnitWorldPosition(attacker.Id);

            // Vector3.zero는 GameObject 미등록 상태 → HexCoord 폴백
            if (attackerWorldPos == Vector3.zero)
                return FindFirstEnemyTargetByHexCoord(attacker);

            // FlatTop 인접 타일 월드 거리 = TileHeight(0.866f)
            // AttackRange × TileHeight: 타일 중심 간 정확한 거리 기준 (epsilon 없음)
            float maxDist = attacker.AttackRange * HexMetrics.TileHeight;

            IDamageable closestTarget = null;
            float minWorldDist = float.MaxValue;

            // 1. 모든 적 유닛 탐색 (월드 좌표 기반)
            foreach (var unit in _unitSpawn.Units.Values)
            {
                if (unit.Team == attacker.Team || !unit.IsAlive) continue;

                Vector3 targetPos = _positionProvider.GetUnitWorldPosition(unit.Id);

                // 미등록 유닛은 HexCoord → 월드 좌표 변환으로 폴백
                if (targetPos == Vector3.zero)
                    targetPos = HexMetrics.HexToWorld(unit.Position);

                float dist = Vector3.Distance(attackerWorldPos, targetPos);

                if (dist <= maxDist && dist < minWorldDist)
                {
                    minWorldDist = dist;
                    closestTarget = unit;
                }
            }

            // 2. 모든 적 건물 탐색 (건물은 이동하지 않으므로 월드 좌표 사용 가능)
            foreach (var building in _buildingPlacement.Buildings.Values)
            {
                if (building.Team == attacker.Team || !building.IsAlive) continue;

                Vector3 targetPos = _positionProvider.GetBuildingWorldPosition(building.Id);

                // 미등록 건물은 HexCoord → 월드 좌표 변환으로 폴백
                if (targetPos == Vector3.zero)
                    targetPos = HexMetrics.HexToWorld(building.Position);

                float dist = Vector3.Distance(attackerWorldPos, targetPos);

                if (dist <= maxDist && dist < minWorldDist)
                {
                    minWorldDist = dist;
                    closestTarget = building;
                }
            }

            return closestTarget;
        }

        /// <summary>
        /// HexCoord.Distance 기반 폴백 판정.
        /// _positionProvider가 null이거나 공격자 월드 좌표를 얻을 수 없을 때 사용.
        /// </summary>
        private IDamageable FindFirstEnemyTargetByHexCoord(UnitData attacker)
        {
            IDamageable closestTarget = null;
            int minDistance = int.MaxValue;

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
