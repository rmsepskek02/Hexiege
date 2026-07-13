// ============================================================================
// TowerCombatUseCase.cs
// 방어 타워(AutoTower)의 자동 공격을 처리하는 UseCase.
//
// 유닛 전투(UnitCombatUseCase)와의 핵심 차이:
//   - 타워는 이동하지 않는다 (고정 건물). A* 이동/추격/3단계 상태 머신이 없다.
//   - 동작은 "사거리 안 가장 가까운 적 유닛 탐색 → 쿨다운마다 데미지" 단순 흐름.
//   - 타겟은 적 유닛만 (건물/아군 제외). GameSystemRules 방어 타워 규칙 1.
//
// 동작 흐름 (Tick 1회):
//   1. 모든 건물 중 AutoTower 타입만 순회
//   2. 각 타워의 AttackCooldownRemaining을 dt만큼 감소 (규칙 5: 쿨다운 중에도 탐색 계속)
//   3. 쿨다운이 0 이하면 → 사거리 내 가장 가까운 적 유닛 탐색 (월드 좌표 기준, 규칙 2/3)
//   4. 적이 있으면 → 데미지 적용 + 쿨다운 리셋 + 사망 처리 (규칙 5/6)
//   5. 타워가 파괴(IsAlive=false)되면 순회에서 건너뜀 (규칙 7)
//
// 멀티플레이 (규칙 9 — 서버 권위):
//   서버에서만 Tick이 호출되어야 한다.
//   - 싱글플레이: GameBootstrapper.Update()에서 Tick(Time.deltaTime) 호출
//   - 멀티플레이: NetworkCombatController 서버 Tick에서만 Tick(elapsed) 호출
//   Tick 초입에서 "네트워크 활성 + 서버 아님"이면 조기 반환하여
//   클라이언트가 실수로 호출해도 이중 데미지가 발생하지 않도록 방어한다.
//
// 데미지/사망 이벤트:
//   UnitCombatUseCase.ExecuteAttack과 동일한 이벤트(OnEntityAttacked / OnEntityDamaged
//   / OnUnitDied)를 발행한다. 따라서 멀티플레이에서 서버가 이 이벤트를 발행하면
//   기존에 연결된 NetworkHealthSync(HP 동기화) / NetworkCombatController(사망 RPC 전파)가
//   자동으로 클라이언트에 결과를 전달한다 — 별도 RPC 작성 불필요.
//
// Application 레이어 — Domain에 의존. Infrastructure(NetworkManager/GameRaceContext)에
//   직접 의존하지 않기 위해 NetworkContext 정적 홀더와 race 해석 델리게이트를 사용한다.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 방어 타워(AutoTower)의 자동 공격을 처리하는 UseCase.
    /// 매 Tick마다 모든 타워를 순회하며 사거리 내 가장 가까운 적 유닛에게 데미지를 준다.
    /// </summary>
    public class TowerCombatUseCase
    {
        // 부동소수점 경계 오차 방지용 여유값. UnitCombatUseCase와 동일한 의도.
        private const float Epsilon = 0.05f;

        private readonly BuildingPlacementUseCase _buildingPlacement;
        private readonly UnitSpawnUseCase _unitSpawn;
        private readonly IHexCoordinateMapper _mapper;
        private readonly IEntityPositionProvider _positionProvider;

        // 팀 → 종족 해석 델리게이트.
        // BuildingData는 Team만 보유하므로, 종족별 사거리/공격력/쿨다운을 조회하려면
        // 팀을 종족으로 변환해야 한다. GameRaceContext(Infrastructure)에 직접 의존하지 않도록
        // composition root(GameBootstrapper)에서 변환 함수를 주입받는다.
        private readonly Func<TeamId, RaceId> _raceResolver;

        /// <summary>
        /// TowerCombatUseCase 생성. 모든 의존성을 생성자 주입으로 받는다.
        /// </summary>
        /// <param name="buildingPlacement">타워(건물) 목록 조회용.</param>
        /// <param name="unitSpawn">적 유닛 목록 조회용.</param>
        /// <param name="mapper">타일↔월드 좌표 변환 + TileHeight (사거리 환산용).</param>
        /// <param name="positionProvider">유닛/타워의 실제 Transform 위치 (Lerp 중 정확도).</param>
        /// <param name="raceResolver">팀 → 종족 변환 함수. 타워 스탯 조회에 사용.</param>
        public TowerCombatUseCase(
            BuildingPlacementUseCase buildingPlacement,
            UnitSpawnUseCase unitSpawn,
            IHexCoordinateMapper mapper,
            IEntityPositionProvider positionProvider,
            Func<TeamId, RaceId> raceResolver)
        {
            _buildingPlacement = buildingPlacement;
            _unitSpawn = unitSpawn;
            _mapper = mapper;
            _positionProvider = positionProvider;
            _raceResolver = raceResolver;
        }

        /// <summary>
        /// 매 프레임(싱글) 또는 서버 Tick(멀티)마다 호출.
        /// 모든 타워를 순회하며 쿨다운을 감소시키고, 쿨다운이 끝난 타워는 적을 공격한다.
        ///
        /// 멀티플레이에서 클라이언트가 호출하면 즉시 반환 — 서버 권위 보장(규칙 9).
        /// </summary>
        /// <param name="dt">경과 시간(초). 싱글은 Time.deltaTime, 멀티는 서버 Tick의 실제 경과 시간.</param>
        public void Tick(float dt)
        {
            // 멀티플레이 클라이언트는 타워 공격을 처리하지 않는다(서버 권위).
            // NetworkContext: Application → Infrastructure 역방향 의존을 막기 위한 정적 홀더.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;

            if (_buildingPlacement == null || _unitSpawn == null) return;

            // 순회 도중 적 유닛 사망으로 _buildings가 직접 변하지는 않지만,
            // 방어적으로 건물 Id 목록을 미리 복사하여 안전하게 순회한다.
            // (적 유닛 사망 시 _unitSpawn.Units만 변하고 _buildings는 그대로지만,
            //  타워끼리 서로를 죽이지 않으므로 건물 컬렉션은 안정적이다.)
            foreach (var building in _buildingPlacement.Buildings.Values)
            {
                // 타워가 아니면 스킵 (적 건물/아군 건물/생산건물 모두 제외).
                if (building.Type != BuildingType.AutoTower) continue;

                // 규칙 7: 파괴된 타워는 모든 동작 중단.
                if (!building.IsAlive) continue;

                // 규칙 5: 쿨다운 감소. 쿨다운 중에도 아래 탐색은 계속 수행한다.
                if (building.AttackCooldownRemaining > 0f)
                    building.AttackCooldownRemaining =
                        Mathf.Max(0f, building.AttackCooldownRemaining - dt);

                // 아직 쿨다운이 남아 있으면 이번 Tick에서는 공격하지 않는다.
                if (building.AttackCooldownRemaining > 0f) continue;

                // 규칙 1/2/3: 사거리 내 가장 가까운 적 유닛 탐색 (월드 좌표 기준).
                UnitData target = FindNearestEnemyUnit(building);
                if (target == null) continue;

                // 데미지 적용 + 이벤트 발행 + 사망 처리.
                ExecuteTowerAttack(building, target);

                // 규칙 5: 공격 직후 쿨다운 시작.
                // 종족별 쿨다운 값을 조회하여 리셋.
                RaceId race = ResolveRace(building.Team);
                building.AttackCooldownRemaining =
                    BuildingStats.GetAttackCooldown(building.Type, race);
            }
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

        /// <summary>
        /// 타워의 사거리 내에서 월드 거리가 가장 가까운 적 유닛을 탐색한다(규칙 1/2/3).
        /// 적 유닛만 대상으로 하며, 건물·아군은 제외한다.
        ///
        /// 사거리는 종족별 AttackRange(타일 단위)에 TileHeight를 곱해 월드 단위로 환산한다.
        /// UnitCombatUseCase의 원거리 유닛 사거리 계산(AttackRange × TileHeight)과 동일한 방식.
        /// </summary>
        /// <param name="tower">탐색 기준이 되는 타워.</param>
        /// <returns>사거리 내 가장 가까운 적 유닛. 없으면 null.</returns>
        private UnitData FindNearestEnemyUnit(BuildingData tower)
        {
            RaceId race = ResolveRace(tower.Team);
            float rangeTiles = BuildingStats.GetAttackRange(tower.Type, race);

            // 사거리가 0 이하면 공격 불가 — Config 미설정 방어.
            if (rangeTiles <= 0f) return null;

            // 타일 단위 사거리 → 월드 단위로 환산 (+Epsilon: 경계 오차 방지).
            float maxWorldDist = rangeTiles * _mapper.TileHeight + Epsilon;

            // 타워의 월드 위치 취득. positionProvider가 없거나 미등록(zero)이면
            // HexCoord → 월드 좌표 변환으로 폴백한다(타워는 고정이므로 폴백이 정확).
            Vector3 towerWorldPos = Vector3.zero;
            if (_positionProvider != null)
                towerWorldPos = _positionProvider.GetBuildingWorldPosition(tower.Id);
            if (towerWorldPos == Vector3.zero)
                towerWorldPos = _mapper.HexToWorld(tower.Position);

            UnitData closest = null;
            float minDist = float.MaxValue;

            foreach (var unit in _unitSpawn.Units.Values)
            {
                // 규칙 1: 아군 유닛 제외 + 사망 유닛 제외.
                if (unit.Team == tower.Team || !unit.IsAlive) continue;

                // 유닛의 실제 월드 위치 (Lerp 중에도 정확). 미등록이면 HexCoord 폴백.
                Vector3 unitPos = Vector3.zero;
                if (_positionProvider != null)
                    unitPos = _positionProvider.GetUnitWorldPosition(unit.Id);
                if (unitPos == Vector3.zero)
                    unitPos = _mapper.HexToWorld(unit.Position);

                float dist = Vector3.Distance(towerWorldPos, unitPos);

                // 규칙 2/3: 사거리 안 + 현재까지 최단 거리면 타겟 후보로 갱신.
                if (dist <= maxWorldDist && dist < minDist)
                {
                    minDist = dist;
                    closest = unit;
                }
            }

            return closest;
        }

        /// <summary>
        /// 타워의 공격을 실행한다. 데미지 적용 + 이벤트 발행 + 타겟 사망 처리.
        /// UnitCombatUseCase.ExecuteAttack의 건물→유닛 버전이며, 발행 이벤트가 동일하므로
        /// 멀티플레이에서 서버가 이 메서드를 호출하면 기존 동기화 경로로 클라이언트에 자동 반영된다.
        /// </summary>
        /// <param name="tower">공격하는 타워.</param>
        /// <param name="target">피격당하는 적 유닛.</param>
        private void ExecuteTowerAttack(BuildingData tower, UnitData target)
        {
            RaceId race = ResolveRace(tower.Team);
            int damage = BuildingStats.GetAttackPower(tower.Type, race);

            // 데미지 적용.
            target.TakeDamage(damage);

            // 공격 이벤트 발행 (공격자=타워, 타겟=유닛).
            // EntityAttackedEvent는 IDamageable 기반이라 건물도 공격자가 될 수 있다.
            GameEvents.OnEntityAttacked.OnNext(new EntityAttackedEvent(tower, target));

            // 피격 이벤트 발행 — NetworkHealthSync가 구독하여 HP를 모든 클라이언트에 동기화.
            // 공격자는 타워(건물)이므로 AttackerId=tower.Id, AttackerIsUnit=false를 실어 보낸다.
            // 타워는 타격 애니메이션 프레임이 없으므로, 피격 표현 큐가 이 값을 보고
            // 보류 없이 "즉시 방출"(안전망 ⓒ) 경로를 타게 된다. (Phase 2 — 축 3)
            GameEvents.OnEntityDamaged.OnNext(
                new EntityDamagedEvent(target, target.Hp, true,
                    attackerId: tower.Id, attackerIsUnit: false));

            // 타겟 사망 처리.
            if (!target.IsAlive)
            {
                // 사망 이벤트 발행 — 데이터 정리 직전에 발행하여 구독자가
                // 도메인 Dictionary에 유닛이 아직 존재하는 상태에서 후속 처리를 할 수 있도록 한다.
                // (멀티플레이에서는 서버가 발행한 OnUnitDied를 NetworkCombatController가
                //  구독하여 EntityDiedClientRpc로 클라이언트에 전파한다.)
                GameEvents.OnUnitDied.OnNext(new UnitDiedEvent(target));

                // 데이터 정리 — 유닛 Dictionary에서 제거.
                _unitSpawn.RemoveUnit(target.Id);
            }
        }

        /// <summary>
        /// 팀 → 종족 변환. 주입된 _raceResolver를 사용하되,
        /// null이면 안전하게 Human을 폴백으로 반환한다(스탯 조회 실패 방지).
        /// </summary>
        /// <param name="team">변환할 팀.</param>
        /// <returns>해당 팀의 종족.</returns>
        private RaceId ResolveRace(TeamId team)
        {
            if (_raceResolver != null)
                return _raceResolver(team);
            return RaceId.Human;
        }
    }
}
