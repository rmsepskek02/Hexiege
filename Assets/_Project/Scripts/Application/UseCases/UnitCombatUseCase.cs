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

        /// <summary>
        /// 싱글플레이 전용 전투 상태 추적.
        /// key=유닛Id, value=(타겟Id, 타겟이 유닛인지).
        /// 전투 중이 아닌 유닛은 Dictionary에 없음.
        /// 멀티플레이에서는 NetworkCombatController._unitCombatTargets가 이 역할을 수행.
        /// </summary>
        private readonly Dictionary<int, (int targetId, bool isUnit)> _combatTargets
            = new Dictionary<int, (int targetId, bool isUnit)>();

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

            // 멀티플레이 모드에서는 서버/클라이언트 구분 없이 TryAttack 완전 차단.
            // 네트워크 모드에서 데미지는 NetworkCombatController가 단독으로 처리하며,
            // HitFrameTime 타이머 후 ApplyAttackDamage를 호출하는 것이 규칙.
            // HOST(서버)도 TryAttack을 통해 즉시 데미지를 주면 규칙 2 위반(이중 데미지 + 타이밍 불일치).
            if (NetworkContext.IsNetworkActive) return null;

            // 쿨다운 중이면 공격 불가
            if (attacker.AttackCooldownRemaining > 0f) return null;

            // IDamageable을 구현하는 모든 적을 찾도록 로직 변경
            IDamageable target = FindFirstEnemyTarget(attacker);
            if (target == null) return null;

            int targetId = target.Id;
            bool targetIsUnit = target is UnitData;

            // --- 싱글플레이 전투 상태 변화 감지 및 이벤트 발행 ---
            // 멀티플레이에서는 NetworkCombatController가 _unitCombatTargets Dictionary와
            // StartCombat/ChangeTarget/StopCombat ClientRpc로 이 역할을 수행.
            if (!NetworkContext.IsNetworkActive)
            {
                if (_combatTargets.TryGetValue(attacker.Id, out var prev))
                {
                    // 이전에 전투 중이었고 타겟이 다르면 → ChangeTarget 이벤트
                    if (prev.targetId != targetId)
                    {
                        _combatTargets[attacker.Id] = (targetId, targetIsUnit);
                        GameEvents.OnCombatTargetChanged.OnNext(
                            new CombatTargetChangedEvent(attacker.Id, targetId, targetIsUnit));
                    }
                    // 같은 타겟이면 이벤트 없음 — Attack 루프가 이미 재생 중
                }
                else
                {
                    // 이전에 전투 중이 아니었으면 → StartCombat 이벤트
                    _combatTargets[attacker.Id] = (targetId, targetIsUnit);
                    GameEvents.OnCombatStarted.OnNext(
                        new CombatStartedEvent(attacker.Id, targetId, targetIsUnit));
                }
            }

            ExecuteAttack(attacker, target);

            // 공격 성공 → 쿨다운 시작
            attacker.AttackCooldownRemaining = attacker.AttackCooldown;

            return (targetId, targetIsUnit);
        }

        /// <summary>
        /// 타겟 탐색만 수행. 데미지 없음, 쿨다운 리셋 없음.
        /// 멀티플레이 서버(NetworkCombatController)가 애니메이션 RPC를 전송하기 전에
        /// 타겟 유효성을 확인하는 용도.
        /// 타겟이 발견되면 (타겟 Id, 타겟이 유닛인지 여부) 튜플을 반환.
        ///
        /// Guard 조건은 TryAttack()과 동일:
        ///   - 공격자가 null이거나 사망 → null
        ///   - 멀티플레이 클라이언트(서버가 아닌 쪽) → null (서버 권위 보장)
        ///   - 공격 쿨다운 중 → null
        /// </summary>
        /// <param name="attacker">공격을 시도하는 유닛</param>
        /// <returns>공격 가능한 타겟이 있으면 (타겟 Id, 타겟이 유닛인지), 없으면 null</returns>
        public (int id, bool isUnit)? TryFindTarget(UnitData attacker)
        {
            if (attacker == null || !attacker.IsAlive) return null;

            // 멀티플레이 모드에서는 서버만 전투 처리.
            // NetworkContext 정적 홀더로 Application → Infrastructure 역방향 의존 방지.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return null;

            // 쿨다운 중이면 공격 불가
            if (attacker.AttackCooldownRemaining > 0f) return null;

            // 사거리 내 가장 가까운 적 탐색 (데미지 미적용)
            IDamageable target = FindFirstEnemyTarget(attacker);
            if (target == null) return null;

            return (target.Id, target is UnitData);
        }

        /// <summary>
        /// 쿨다운과 무관하게 사거리 내 가장 가까운 적을 반환.
        /// 공격 권한(서버/클라이언트) 체크 없이 순수하게 적 탐색만 수행.
        ///
        /// 사용처:
        ///   1. OnUnitEnteredCombatHandler — 쿨다운 중 적 감지 시 타겟 ID 확보 (StartCombatClientRpc 전송)
        ///   2. TickCombat — 쿨다운 중 타겟 변경 감지 (ChangeTargetClientRpc 전송)
        ///
        /// TryFindTarget과의 차이:
        ///   TryFindTarget: 서버 권한 체크 + 쿨다운 체크 포함 → 쿨다운 중이면 null
        ///   FindNearestEnemy: 쿨다운 체크 없음 → 사거리 내 적이 있으면 항상 반환
        /// </summary>
        /// <param name="attacker">탐색 기준이 되는 유닛</param>
        /// <returns>사거리 내 가장 가까운 적이 있으면 (타겟 Id, 타겟이 유닛인지), 없으면 null</returns>
        public (int id, bool isUnit)? FindNearestEnemy(UnitData attacker)
        {
            if (attacker == null || !attacker.IsAlive) return null;

            IDamageable target = FindFirstEnemyTarget(attacker);
            if (target == null) return null;

            return (target.Id, target is UnitData);
        }

        /// <summary>
        /// 실제 데미지를 적용하는 메서드.
        /// TryFindTarget() 성공 후, hitFrameTime(타격 프레임) 딜레이 뒤에 호출.
        ///
        /// 타겟 고정(Target Lock) 설계:
        ///   공격 모션을 시작한 순간 타겟이 확정됨.
        ///   딜레이 중 타겟이 사거리를 벗어나도 데미지 적용.
        ///   단, 아래 두 경우에만 취소:
        ///   1. 공격자가 딜레이 중 사망
        ///   2. 타겟이 딜레이 중 사망 (다른 유닛에게 먼저 처치됨)
        /// </summary>
        /// <param name="attacker">공격하는 유닛</param>
        /// <param name="targetId">타겟 엔티티의 Id</param>
        /// <param name="targetIsUnit">true=유닛, false=건물</param>
        public void ApplyAttackDamage(UnitData attacker, int targetId, bool targetIsUnit)
        {
            // 딜레이 동안 공격자가 사망했을 수 있으므로 재확인
            if (attacker == null || !attacker.IsAlive) return;

            // 타겟을 Id로 재탐색 — 딜레이 동안 다른 유닛에게 먼저 처치되었을 수 있음
            IDamageable target = FindTargetById(targetId, targetIsUnit);
            if (target == null || !target.IsAlive) return;

            // 사거리 체크 없음 — 타겟 고정 설계.
            // 공격 모션 시작 시 타겟이 확정되므로, 이후 이탈해도 데미지 적용.

            // 데미지 적용 + 이벤트 발행
            ExecuteAttack(attacker, target);

            // 쿨다운 리셋 — TryFindTarget()에서 하지 않으므로 여기서 처리.
            attacker.AttackCooldownRemaining = attacker.AttackCooldown;
        }

        /// <summary>
        /// 싱글플레이 전용 쿨다운 감소.
        /// GameBootstrapper.Update()에서 매 프레임 호출하여 모든 살아있는 유닛의 쿨다운을 감소.
        ///
        /// 멀티플레이에서는 NetworkCombatController.TickCombat()이 서버 Tick 주기로
        /// 직접 쿨다운을 감소시키므로, 이 메서드를 호출하면 안 됨 (이중 감소 발생).
        ///
        /// 기존 UnitView.Update()에 분산되어 있던 쿨다운 감소를 한 곳에서 일괄 처리.
        /// </summary>
        /// <param name="dt">경과 시간 (초). 일반적으로 Time.deltaTime을 전달.</param>
        public void TickCooldowns(float dt)
        {
            foreach (var unit in _unitSpawn.Units.Values)
            {
                if (unit.AttackCooldownRemaining > 0f)
                    unit.AttackCooldownRemaining = Mathf.Max(0f, unit.AttackCooldownRemaining - dt);
            }
        }

        /// <summary>
        /// 싱글플레이 전용: 유닛의 전투 상태를 해제하고 OnCombatStopped 이벤트를 발행.
        /// MoveAlongPath에서 HasEnemyInRange가 false가 되어 전투 루프를 탈출할 때 호출.
        /// 멀티플레이에서는 NetworkCombatController가 TickCombat에서 StopCombatClientRpc를 전송하므로 불필요.
        /// </summary>
        /// <param name="unitId">전투를 종료하는 유닛의 Id</param>
        public void ClearCombatState(int unitId)
        {
            // Dictionary에서 제거 성공 = 실제로 전투 중이었음 → 이벤트 발행
            if (_combatTargets.Remove(unitId))
            {
                GameEvents.OnCombatStopped.OnNext(unitId);
            }
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
            // AttackRange × TileHeight: 타일 중심 간 정확한 거리 기준
            // Epsilon: 인접 타일 거리(0.866) = Pistoleer maxDist(0.866) 경계 케이스 부동소수점 오차 방지
            const float Epsilon = 0.05f;
            float maxDist = attacker.AttackRange * HexMetrics.TileHeight + Epsilon;

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
        ///
        /// HexCoord.Distance는 정수(int) 반환이므로, AttackRange가 1.0 미만(예: 0.5)인
        /// 근접 유닛은 distance <= 0.5 조건이 항상 false가 되는 버그 발생.
        /// 이를 방지하기 위해 rangeThreshold = Max(1, CeilToInt(AttackRange))로 보정.
        /// range 0.5 → threshold 1 (인접 타일까지 폴백 탐색 가능).
        /// </summary>
        private IDamageable FindFirstEnemyTargetByHexCoord(UnitData attacker)
        {
            // HexCoord.Distance는 정수이므로 float AttackRange를 정수 threshold로 변환.
            // range < 1.0인 근접 유닛도 최소 인접 타일(distance=1)까지 탐색 가능하도록 보정.
            int rangeThreshold = Mathf.Max(1, Mathf.CeilToInt(attacker.AttackRange));

            IDamageable closestTarget = null;
            int minDistance = int.MaxValue;

            foreach (var unit in _unitSpawn.Units.Values)
            {
                if (unit.Team == attacker.Team || !unit.IsAlive) continue;

                int distance = HexCoord.Distance(attacker.Position, unit.Position);

                if (distance <= rangeThreshold && distance < minDistance)
                {
                    minDistance = distance;
                    closestTarget = unit;
                }
            }

            foreach (var building in _buildingPlacement.Buildings.Values)
            {
                if (building.Team == attacker.Team || !building.IsAlive) continue;

                int distance = HexCoord.Distance(attacker.Position, building.Position);

                if (distance <= rangeThreshold && distance < minDistance)
                {
                    minDistance = distance;
                    closestTarget = building;
                }
            }

            return closestTarget;
        }

        /// <summary>
        /// Id와 타입으로 타겟 엔티티를 탐색.
        /// ApplyAttackDamage()에서 딜레이 후 타겟을 재확인하는 데 사용.
        /// targetIsUnit이면 UnitSpawnUseCase의 Units에서, 아니면 BuildingPlacementUseCase의 Buildings에서 탐색.
        /// </summary>
        /// <param name="targetId">찾을 엔티티의 Id</param>
        /// <param name="targetIsUnit">true=유닛 Dictionary 탐색, false=건물 Dictionary 탐색</param>
        /// <returns>해당 Id의 엔티티. 없으면 null.</returns>
        private IDamageable FindTargetById(int targetId, bool targetIsUnit)
        {
            if (targetIsUnit)
            {
                // 유닛 Dictionary에서 탐색
                _unitSpawn.Units.TryGetValue(targetId, out UnitData unit);
                return unit;
            }
            else
            {
                // 건물 Dictionary에서 탐색
                _buildingPlacement.Buildings.TryGetValue(targetId, out BuildingData building);
                return building;
            }
        }

        /// <summary>
        /// 공격자의 사거리 내에 타겟이 있는지 판정.
        /// ApplyAttackDamage()에서 딜레이 후 사거리 재확인에 사용.
        /// FindFirstEnemyTarget()과 동일한 거리 판정 기준 (월드 좌표 또는 HexCoord 폴백).
        /// </summary>
        /// <param name="attacker">공격하는 유닛</param>
        /// <param name="target">사거리 확인 대상</param>
        /// <returns>사거리 내에 있으면 true</returns>
        private bool IsTargetInRange(UnitData attacker, IDamageable target)
        {
            // FlatTop 인접 타일 월드 거리 = TileHeight(0.866f)
            // AttackRange x TileHeight: 타일 중심 간 정확한 거리 기준
            // Epsilon: 경계 케이스 부동소수점 오차 방지 (Pistoleer 0.866 = FlatTop 인접 거리)
            const float Epsilon = 0.05f;
            float maxDist = attacker.AttackRange * HexMetrics.TileHeight + Epsilon;

            // _positionProvider가 있으면 월드 좌표 기반 판정 (Lerp 중에도 정확)
            if (_positionProvider != null)
            {
                Vector3 attackerWorldPos = _positionProvider.GetUnitWorldPosition(attacker.Id);

                // 공격자 월드 좌표를 얻을 수 있으면 월드 거리 판정
                if (attackerWorldPos != Vector3.zero)
                {
                    // 타겟이 유닛이면 유닛 월드 좌표, 건물이면 건물 월드 좌표 사용
                    Vector3 targetPos;
                    if (target is UnitData)
                        targetPos = _positionProvider.GetUnitWorldPosition(target.Id);
                    else
                        targetPos = _positionProvider.GetBuildingWorldPosition(target.Id);

                    // 미등록 엔티티는 HexCoord → 월드 좌표 변환으로 폴백
                    if (targetPos == Vector3.zero)
                        targetPos = HexMetrics.HexToWorld(target.Position);

                    return Vector3.Distance(attackerWorldPos, targetPos) <= maxDist;
                }
            }

            // 폴백: HexCoord.Distance 기반 판정.
            // HexCoord.Distance는 정수이므로 range < 1.0 근접 유닛도 인접 타일까지 탐색 가능하도록 보정.
            int fallbackThreshold = Mathf.Max(1, Mathf.CeilToInt(attacker.AttackRange));
            return HexCoord.Distance(attacker.Position, target.Position) <= fallbackThreshold;
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

                // 싱글플레이: 사망한 유닛의 전투 상태 정리.
                // 사망한 유닛 자체가 전투 중이었다면 Dictionary에서 제거.
                // StopCombat 이벤트는 발행하지 않음 — EntityDiedClientRpc가 사망 처리를 담당.
                if (!NetworkContext.IsNetworkActive && target is UnitData deadUnit)
                {
                    _combatTargets.Remove(deadUnit.Id);
                }

                // 싱글플레이: 사망한 타겟을 공격 중이던 유닛들의 전투 상태는 제거하지 않음.
                // → 다음 TryAttack() 호출 시 새 타겟을 탐색하거나,
                //   HasEnemyInRange가 false가 되어 MoveAlongPath에서 ClearCombatState() 호출.
                // 이렇게 하면 사망 직후 즉시 StopCombat 이벤트가 발행되지 않고,
                // 자연스러운 타이밍(다음 프레임)에 전투 종료/새 타겟 전환이 발생.

                // 데이터 정리 — Dictionary에서 제거
                if (target is UnitData u)
                    _unitSpawn.RemoveUnit(u.Id);
                else if (target is BuildingData b)
                    _buildingPlacement.RemoveBuilding(b.Id);
            }
        }
    }
}
