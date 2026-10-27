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
// [2026-10-21 단계 C — 인간 방어타워(CannonTower) 투사체 전환 · RM-10]
//   사용자 결정(Q10 · Buildings 규칙 12 개정 F-8): Human CannonTower는 서버 권위 투사체로 피해를 준다.
//     · 공격 순간 = 발사 순간(타워에는 애니메이션 marker가 없다). 대포 입구(포구)에서 발사한다.
//     · 피해는 착탄 시각에 이 파일의 타워 writer(ApplyTowerProjectileImpact)가 1회 준다.
//     · 비행 중 타워가 파괴돼도 맞으면 피해를 준다(공격자 사망과 같다).
//   Spirit RuneSpire · Transcendence VineTower는 바뀌지 않는다 — 아래 ExecuteTowerAttack의 기존 즉발 경로 그대로다.
//   분기 기준은 ServerProjectileDeliveryPolicy.IsServerProjectileTower(이행 스위치 RM-9 포함)다.
//   ⚠️ RM-10: 인간 타워의 옛 즉발 피해·Tower 발사 스냅샷은 지우지 않았다 — 같은 코드를 RuneSpire가 계속 쓰고,
//      스위치를 끄면 인간 타워도 다시 그 경로를 탄다. 모든 대상 테스트 PASS 뒤 스위치 제거(P5) 때 정리 범위를 정한다.
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
    public class TowerCombatUseCase : IBuildingProjectileImpactWriter
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

        // [Phase 2] 연구소 강화 UseCase(생성 후 주입). 타워→유닛 방어 감쇄를 "피격 유닛 팀"의
        //   방어 연구 레벨로 조회하기 위해 사용한다. 미주입 시 스냅샷 필드(target.Defense)로 폴백(하위호환).
        private UnitUpgradeUseCase _upgrade;

        // [단계 C] 서버 권위 투사체 수명 관리자(발사·비행·착탄·닫기). 조합 루트가 SetProjectileLauncher로 주입한다.
        //   null이면(주입 전·Editor fixture) 인간 타워도 기존 즉발 경로를 탄다 — 아래 ExecuteTowerAttack 참고.
        private UnitCombatUseCase _projectiles;
        private readonly AttackEffectIdentity _effectIdentity = new AttackEffectIdentity();
        private Func<double> _effectClock = () => Time.timeAsDouble;
        public void SetEffectClock(Func<double> clock) { _effectClock = clock ?? throw new ArgumentNullException(nameof(clock)); }

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
        /// 팀별 강화 레벨 UseCase를 주입한다(GameBootstrapper에서 1회). 미주입 시 방어 감쇄는 스냅샷 0으로 동작.
        /// </summary>
        /// <param name="upgrade">팀별 강화 상태 UseCase.</param>
        public void SetUpgradeUseCase(UnitUpgradeUseCase upgrade)
        {
            _upgrade = upgrade;
        }

        /// <summary>
        /// [단계 C] 서버 권위 투사체 수명 관리자(UnitCombatUseCase)를 주입한다(GameBootstrapper에서 1회 — 맵마다 새로).
        /// 주입되면 투사체 타워(인간 CannonTower)의 공격이 즉발 대신 "발사 → 비행 → 착탄 피해"가 된다.
        /// </summary>
        /// <param name="projectiles">같은 경기의 전투 UseCase(투사체 기록 소유자).</param>
        public void SetProjectileLauncher(UnitCombatUseCase projectiles)
        {
            _projectiles = projectiles;
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
                // [단계 C] 투사체 타워는 여기서 "발사"만 하고 피해는 착탄 때 준다. 발사를 못 했으면(투사체 용량 초과·
                //   서비스 닫힘) 공격 자체가 없었던 것이므로 쿨다운을 시작하지 않는다(유닛 투사체 커밋 거절과 같은 원칙).
                //   즉발 타워(RuneSpire·VineTower)는 항상 true라 동작이 예전과 같다.
                if (!ExecuteTowerAttack(building, target)) continue;

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
        /// <returns>
        /// 공격이 일어났으면 true(즉발 피해를 줬거나 투사체를 발사했다). [단계 C] 투사체 타워가 발사를 못 했으면 false.
        /// ⚠️ 이 메서드 이름·매개변수(BuildingData, UnitData)는 Editor 검증(VfxConnections)이 리플렉션으로 부르므로 바꾸지 않는다.
        /// </returns>
        private bool ExecuteTowerAttack(BuildingData tower, UnitData target)
        {
            // [단계 C] 투사체 타워(인간 CannonTower) — 즉발 피해 대신 서버 발사 기록을 만든다(RM-10).
            //   Tower 발사 스냅샷(OnAttackEffectConfirmed)도 내지 않는다: 발사 이펙트는 화면 소비자
            //   (ProjectilePresentationPresenter)가 서버 발사 기록을 꺼내는 순간 포구 위치에 낸다(이중 재생 방지).
            RaceId projectileRace = ResolveRace(tower.Team);
            if (_projectiles != null && ServerProjectileDeliveryPolicy.IsServerProjectileTower(tower.Type, projectileRace))
            {
                int launchPower = BuildingStats.GetAttackPower(tower.Type, projectileRace);
                bool launched = _projectiles.TryLaunchTowerProjectile(tower, projectileRace, target, launchPower, this);
                if (launched) _projectiles.RecordProjectileTowerAttackPath(projectileRace, true);   // [진단] 판정 무관
                return launched;
            }
            // 아래는 기존 즉발 경로(RuneSpire·VineTower, 그리고 스위치가 꺼졌거나 launcher가 없을 때의 인간 타워) — 동작 무변경.
            if (_projectiles != null) _projectiles.RecordProjectileTowerAttackPath(projectileRace, false);   // [진단] 즉발 경로 수(RuneSpire 회귀 확인용)
            Vector3 effectSource = default, effectImpact = default;
            double effectTime = double.NaN;
            // 표현 좌표/시계의 실패도 기존 피해 및 사망 workflow를 막아서는 안 된다.
            try
            {
                effectSource = _positionProvider != null ? _positionProvider.GetBuildingWorldPosition(tower.Id) : Vector3.zero;
                effectImpact = _positionProvider != null ? _positionProvider.GetUnitWorldPosition(target.Id) : Vector3.zero;
                if (effectSource == Vector3.zero) effectSource = _mapper.HexToWorld(tower.Position);
                if (effectImpact == Vector3.zero) effectImpact = _mapper.HexToWorld(target.Position);
                effectTime = _effectClock();
            }
            catch (Exception ex) { AttackEffectIdentity.Record("Fault", default, "TowerSnapshot:" + ex.Message); }
            RaceId race = ResolveRace(tower.Team);
            int rawDamage = BuildingStats.GetAttackPower(tower.Type, race);

            // 데미지 적용. 타워→유닛 피해도 유닛 방어력 감쇄 대상이다(규칙 44 / 규칙 5).
            // [Phase 2] 방어력은 "피격 유닛 팀"의 방어 연구 레벨로 조회한다(_upgrade 미주입 시 스냅샷 0 폴백).
            // (공격자는 유닛이 아니라 타워이므로 Tank/CannonCart 건물 2배 판정과는 무관.)
            int defense = _upgrade != null
                ? _upgrade.GetDefense(target.Team, target.Type)
                : target.Defense;
            int damage = DamageCalculator.ApplyDefense(rawDamage, defense);
            int effectHpBefore = target.Hp;
            target.TakeDamage(damage);
            // Sound15: tower fire SFX is unspecified. This snapshot has no damage authority.
            try
            {
                var visual = _effectIdentity.Create(tower, target, AttackEffectVisual.Tower, race, effectSource, effectImpact, effectTime);
                visual.AppliedDamage = Mathf.Max(0, effectHpBefore - target.Hp); visual.ResultHp = target.Hp;
                if (visual.IsValid)
                { AttackEffectIdentity.Record("ServerConfirm", visual, now: effectTime); GameEvents.OnAttackEffectConfirmed.OnNext(visual); }
            }
            catch (Exception ex) { AttackEffectIdentity.Record("Fault", default, "TowerPublish:" + ex.Message); }

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
            return true;
        }

        /// <summary>
        /// [2026-10-21 단계 C] 투사체 타워(인간 CannonTower)의 착탄 피해 1회 — 타워 피해의 단일 writer.
        /// UnitCombatUseCase가 착탄 시각에 부른다(IBuildingProjectileImpactWriter). 서버/싱글 전용.
        ///
        /// 계산·이벤트·사망 처리는 즉발 경로(ExecuteTowerAttack 아래쪽)와 같다. 다른 점은 세 가지뿐이다.
        ///   ① 공격력은 발사 때 저장한 값(attackPowerAtLaunch)이다. 방어 감쇄는 "지금(착탄 순간)" 피격 유닛 팀의 연구 레벨로 한다
        ///      — 유닛 투사체도 착탄 때 같은 writer(ComputeFinalDamage)로 감쇄하므로 같은 원칙이다.
        ///   ② 타워가 이미 파괴됐어도 진행한다(사용자 결정 U-6 — 공격자 사망과 같다). 타워가 살아 있는지 보지 않는다.
        ///   ③ 피격 이벤트에 투사체 번호표(key)를 싣는다 → 화면은 숫자를 착탄 순간에 1회만 낸다(Q7).
        ///      Tower 발사 스냅샷은 내지 않는다(발사 이펙트는 화면 소비자가 발사 때 이미 냈다).
        /// </summary>
        /// <param name="tower">쏜 타워(파괴됐을 수 있음).</param>
        /// <param name="target">원래 대상 유닛(호출자가 착탄 순간 살아 있는 같은 객체임을 확인했다).</param>
        /// <param name="attackPowerAtLaunch">발사 때 저장한 원래 공격력.</param>
        /// <param name="key">투사체 번호표.</param>
        /// <returns>Applied(적용량·남은 HP) 또는 대상 무효 시 TargetUnavailable.</returns>
        public AttackDamageObservation ApplyTowerProjectileImpact(BuildingData tower, UnitData target,
            int attackPowerAtLaunch, ProjectileDeliveryKey key)
        {
            if (tower == null || target == null || !target.IsAlive || _unitSpawn == null)
                return AttackDamageObservation.Unavailable(AttackDamageApplyStatus.TargetUnavailable);

            // 데미지 적용 — 즉발 경로와 같은 식(규칙 44 / 규칙 5 방어 감쇄, 타워는 건물 2배 무관).
            int defense = _upgrade != null
                ? _upgrade.GetDefense(target.Team, target.Type)
                : target.Defense;
            int damage = DamageCalculator.ApplyDefense(Math.Max(0, attackPowerAtLaunch), defense);
            int hpBefore = target.Hp;
            target.TakeDamage(damage);
            int applied = Math.Max(0, hpBefore - target.Hp);

            // 공격 이벤트(공격자=타워 — 파괴된 타워 객체여도 그대로 싣는다. 구독자는 Id·종류만 읽는다).
            GameEvents.OnEntityAttacked.OnNext(new EntityAttackedEvent(tower, target));

            // 피격 이벤트 — NetworkHealthSync가 HP를 Client에 동기화하고 투사체 번호표도 함께 보낸다.
            //   번호표가 있으므로 피격 표현 큐는 "타워 즉시 방출"이 아니라 화면 착탄 순간 방출 경로를 탄다.
            GameEvents.OnEntityDamaged.OnNext(
                new EntityDamagedEvent(target, target.Hp, true,
                    attackerId: tower.Id, attackerIsUnit: false,
                    immediatePresentation: false, presentationResultKey: default,
                    projectilePresentationKey: key));

            // 타겟 사망 처리 — 즉발 경로와 같은 순서(사망 이벤트 → 유닛 목록에서 제거).
            if (!target.IsAlive)
            {
                GameEvents.OnUnitDied.OnNext(new UnitDiedEvent(target));
                _unitSpawn.RemoveUnit(target.Id);
            }
            return new AttackDamageObservation(AttackDamageApplyStatus.Applied, applied, Math.Max(0, target.Hp));
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
