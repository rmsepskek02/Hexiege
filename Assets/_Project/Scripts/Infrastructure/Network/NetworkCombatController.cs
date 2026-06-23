// ============================================================================
// NetworkCombatController.cs
// 네트워크 모드에서 유닛 전투 처리를 서버 권위로 수행하고 사망을 모든 클라이언트에 동기화.
//
// 역할:
//   - 서버 Update에서 매 프레임 UnitCombatUseCase를 통해 전투 Tick 수행
//     (싱글플레이에서는 UnitView.MoveAlongPath의 코루틴이 TryAttack을 직접 호출)
//     (멀티플레이에서는 UnitCombatUseCase.TryAttack이 클라이언트에서 return false이므로
//      이 컨트롤러가 서버 Update에서 모든 살아있는 유닛에 대해 TryAttack을 호출)
//   - OnUnitDied / OnBuildingDied 이벤트 구독 → EntityDiedClientRpc로 사망 전파
//   - 클라이언트에서 도메인 데이터 정리 + GameEvents.OnUnitDied / OnBuildingDied 재발행
//     → GameEndUseCase가 Castle 파괴 감지 → GameEndUI 반응
//
// 전투 Tick 주기:
//   UnitView.MoveAlongPath에서는 Lerp 이동 중 매 프레임 TryAttack을 호출.
//   멀티플레이에서는 클라이언트가 TryAttack을 호출해도 즉시 return false됨.
//   대신 이 컨트롤러가 서버에서 _attackInterval마다 모든 유닛을 일괄 처리.
//
// 배치:
//   씬에 빈 GameObject "NetworkCombatController" 생성.
//   NetworkObject 컴포넌트 + 이 스크립트를 부착.
//   NetworkManager의 씬 오브젝트로 자동 스폰.
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UniRx;
using Hexiege.Core;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 전투 네트워크 컨트롤러.
    /// 서버에서 전투를 처리하고 사망 이벤트를 모든 클라이언트에 전파.
    /// </summary>
    public class NetworkCombatController : NetworkBehaviour
    {
        // ====================================================================
        // Inspector 설정
        // ====================================================================

        [Header("전투 설정")]
        [Tooltip("전투 처리 간격 (초). 0.05 = 초당 20회 전투 판정. 첫 공격까지의 최대 지연을 줄이기 위해 낮게 설정.")]
        [SerializeField] private float _attackInterval = 0.05f;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>GameBootstrapper 참조. UseCase 접근에 사용.</summary>
        private Hexiege.Bootstrap.GameBootstrapper _bootstrapper;

        /// <summary>OnUnitDied 구독 해제용 Disposable. 서버 전용(유닛 사망 → 클라이언트 RPC 전파).</summary>
        private System.IDisposable _unitDiedSubscription;

        /// <summary>OnBuildingDied 구독 해제용 Disposable. 서버 전용(건물 사망 → 클라이언트 RPC 전파).</summary>
        private System.IDisposable _buildingDiedSubscription;

        /// <summary>OnUnitWalkStarted 구독 해제용 Disposable. 서버 전용.</summary>
        private System.IDisposable _walkStartedSubscription;

        /// <summary>OnUnitEnteredCombat 구독 해제용 Disposable. 서버 전용.</summary>
        private System.IDisposable _enteredCombatSubscription;

        /// <summary>
        /// 유닛별 현재 전투 타겟 추적. key=유닛Id, value=(타겟Id, 타겟이 유닛인지).
        /// 전투 중이 아닌 유닛은 Dictionary에 없음.
        /// TickCombat에서 이전 프레임 상태와 비교하여 상태 변화 시에만 RPC 전송.
        /// </summary>
        private readonly System.Collections.Generic.Dictionary<int, (int targetId, bool isUnit)> _unitCombatTargets
            = new System.Collections.Generic.Dictionary<int, (int targetId, bool isUnit)>();

        /// <summary>
        /// StartCombatClientRpc를 전송한 유닛 Id 집합.
        /// _unitCombatTargets와 분리하여 관리하는 이유:
        ///   TickCombat(Update)이 코루틴(MoveAlongPath)보다 먼저 실행되면,
        ///   같은 프레임 내에서 TickCombat이 _unitCombatTargets에 먼저 등록하고
        ///   OnUnitEnteredCombatHandler의 ContainsKey 가드가 true를 반환하여
        ///   StartCombatClientRpc가 전송되지 않는 경쟁 조건이 발생한다.
        ///   → RPC 전송 여부는 이 Set으로만 판단한다.
        /// </summary>
        private readonly System.Collections.Generic.HashSet<int> _combatAnimationSent
            = new System.Collections.Generic.HashSet<int>();

        /// <summary>다음 전투 판정까지 남은 시간.</summary>
        private float _attackTimer = 0f;

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 GameBootstrapper를 탐색하고
        /// 서버라면 OnUnitDied / OnBuildingDied 이벤트를 구독하여 사망 전파 준비.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();
            if (_bootstrapper == null)
            {
                Debug.LogWarning("[Network] NetworkCombatController: GameBootstrapper를 찾을 수 없습니다.");
            }

            // NetworkContext에 네트워크 상태 주입.
            // UnitCombatUseCase가 NetworkManager에 직접 의존하지 않도록
            // Application 레이어용 정적 홀더를 업데이트.
            NetworkContext.Set(isServer: IsServer, isActive: true);
            Debug.Log($"[Network] NetworkCombatController 스폰. IsServer={IsServer}. NetworkContext 설정 완료.");

            // 서버만 사망/Walk 이벤트를 구독하여 클라이언트에 동기화
            if (IsServer)
            {
                // 사망 이벤트는 OnUnitDied / OnBuildingDied 두 갈래로 분리되었으므로
                // 서버 측에서도 각각 구독한다.
                // 분리 이유: 구독 측에서 매번 is 캐스트로 타입을 분기하지 않도록 강타입 DTO를 사용.
                _unitDiedSubscription = GameEvents.OnUnitDied
                    .Subscribe(OnUnitDied);
                _buildingDiedSubscription = GameEvents.OnBuildingDied
                    .Subscribe(OnBuildingDied);

                // 서버 UnitView의 Walk 시작/정지 이벤트를 수신하여 클라이언트에 전파.
                // UnitView.MoveAlongPath()는 서버에서만 실행되므로,
                // Walk 애니메이션 상태를 ClientRpc로 클라이언트에 동기화.
                _walkStartedSubscription = GameEvents.OnUnitWalkStarted
                    .Subscribe(unitId => StartWalkAnimationClientRpc(unitId));

                // MoveAlongPath에서 적 감지 즉시 StartCombatClientRpc 전송 (TickCombat 50ms 대기 없이)
                _enteredCombatSubscription = GameEvents.OnUnitEnteredCombat
                    .Subscribe(OnUnitEnteredCombatHandler);

                Debug.Log("[Network] NetworkCombatController: 서버 측 OnUnitDied/OnBuildingDied/Walk/EnteredCombat 이벤트 구독 완료.");
            }
        }

        /// <summary>
        /// 네트워크 디스폰 시 이벤트 구독 해제.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            // 사망 이벤트 구독을 모두 해제 (Unit/Building 두 종류 모두).
            // 어느 한 쪽이라도 누수되면 다음 게임에서 동일 핸들러가 중복 실행되어
            // EntityDiedClientRpc가 두 번 전송되는 문제로 이어진다.
            _unitDiedSubscription?.Dispose();
            _unitDiedSubscription = null;

            _buildingDiedSubscription?.Dispose();
            _buildingDiedSubscription = null;

            // Walk 이벤트 구독 해제 (서버에서만 구독했으므로 null일 수 있음)
            _walkStartedSubscription?.Dispose();
            _walkStartedSubscription = null;

            // EnteredCombat 이벤트 구독 해제 (서버에서만 구독했으므로 null일 수 있음)
            _enteredCombatSubscription?.Dispose();
            _enteredCombatSubscription = null;

            // 전투 상태 초기화 — 씬 전환 시 이전 게임의 상태가 남지 않도록
            _unitCombatTargets.Clear();
            _combatAnimationSent.Clear();

            // 연결 해제 시 NetworkContext를 싱글플레이 기본값으로 초기화
            NetworkContext.Reset();
        }

        // ====================================================================
        // 서버 전투 Update
        // ====================================================================

        /// <summary>
        /// 서버 전용 Update: 주기적으로 모든 살아있는 유닛의 전투를 처리.
        ///
        /// 싱글플레이: UnitView.MoveAlongPath의 코루틴이 TryAttack을 직접 호출.
        /// 멀티플레이: TryAttack이 클라이언트에서 false 반환되므로
        ///             이 서버 Update에서 일괄 처리.
        ///
        /// _attackInterval마다 Tick을 발행하여 매 프레임 처리 비용을 줄임.
        ///
        /// 타이밍 정확도:
        ///   _attackTimer -= _attackInterval 으로 오버슈트를 다음 Tick에 이월.
        ///   (0f로 리셋하면 오버슈트가 버려져 Tick 간격이 평균적으로 길어짐)
        ///   쿨다운 감소는 _attackInterval(고정값)이 아닌 실제 경과 시간(elapsed)으로 처리.
        ///   → 프레임레이트(30fps/60fps)에 무관하게 공격 주기가 애니메이션 클립 길이와 일치.
        /// </summary>
        private void Update()
        {
            // 멀티플레이 서버만 실행
            if (!IsServer) return;

            _attackTimer += Time.deltaTime;
            if (_attackTimer < _attackInterval) return;

            // 실제 경과 시간 캡처 후 오버슈트 이월
            // (예: 60fps에서 16.7ms 프레임 3개 = 50.1ms → elapsed=50.1ms, 다음 Tick까지 0.1ms 이월)
            float elapsed = _attackTimer;
            _attackTimer -= _attackInterval;

            TickCombat(elapsed);
        }

        /// <summary>
        /// 살아있는 모든 유닛에 대해 타겟 탐색 + 상태 변화 감지 + 딜레이 데미지 처리.
        ///
        /// _unitCombatTargets Dictionary로 유닛별 전투 상태(현재 타겟)를 추적하고,
        /// 상태 변화 시에만 3종 RPC(StartCombat/ChangeTarget/StopCombat)를 전송.
        /// 같은 타겟을 계속 공격 중이면 RPC 없이 데미지 코루틴만 실행.
        ///
        /// 이렇게 하면 매 쿨다운 사이클마다 Attack RPC를 반복 전송하지 않아
        /// 클라이언트의 Attack 루프(Loop Time=ON)가 끊기지 않고 자연스럽게 재생됨.
        /// </summary>
        /// <param name="elapsed">이번 Tick의 실제 경과 시간 (초). 쿨다운 감소에 사용.</param>
        private void TickCombat(float elapsed)
        {
            // _bootstrapper는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            if (_bootstrapper == null) return;

            UnitSpawnUseCase unitSpawn = _bootstrapper.GetUnitSpawn();
            UnitCombatUseCase combat = _bootstrapper.GetCombatUseCase();

            if (unitSpawn == null || combat == null) return;

            // ────────────────────────────────────────────────────────────────
            // 방어 타워 전투(서버 권위 — 방어 타워 시스템 규칙 9).
            // 이 Update()는 진입부에서 IsServer 가드로 클라이언트를 차단하므로,
            // 여기서 타워 Tick을 호출하면 서버에서만 데미지가 적용된다.
            // 타워가 유닛을 처치하면 TowerCombatUseCase가 OnUnitDied를 발행하고,
            // 그 이벤트를 이 컨트롤러가 이미 구독(OnUnitDied)하고 있어
            // EntityDiedClientRpc로 클라이언트에 자동 전파된다.
            // ────────────────────────────────────────────────────────────────
            TowerCombatUseCase tower = _bootstrapper.GetTowerCombat();
            tower?.Tick(elapsed);

            // Dictionary를 순회하면서 타겟 탐색
            // 전투 중 RemoveUnit이 호출될 수 있으므로 키를 미리 복사
            var unitIds = new System.Collections.Generic.List<int>(unitSpawn.Units.Keys);

            // 이번 Tick에서 전투 중인 유닛 Id를 수집 (Tick 후 Dictionary 정리용)
            // HashSet을 사용하여 O(1) Contains 체크
            var activeThisTick = new System.Collections.Generic.HashSet<int>();

            foreach (int id in unitIds)
            {
                // 순회 도중 사망 처리로 제거되었을 수 있으므로 재확인
                if (!unitSpawn.Units.TryGetValue(id, out UnitData unit)) continue;
                if (!unit.IsAlive) continue;

                // 쿨다운 감소 — 고정값(_attackInterval)이 아닌 실제 경과 시간(elapsed)으로 감소.
                // 프레임레이트가 낮아도 공격 주기가 애니메이션 클립 길이와 일치.
                if (unit.AttackCooldownRemaining > 0f)
                    unit.AttackCooldownRemaining = Mathf.Max(0f, unit.AttackCooldownRemaining - elapsed);

                // TryFindTarget: 쿨다운이 0이고 사거리 내 적이 있을 때만 타겟 반환.
                // HasEnemyInRange: 쿨다운과 무관하게 사거리 내 적 존재 여부만 체크.
                //
                // 두 가지를 분리하는 이유:
                //   TryFindTarget이 null을 반환하는 원인이
                //   "적이 없어서"일 수도 있고, "쿨다운 중이어서"일 수도 있음.
                //   StopCombat은 오직 "적이 없을 때"만 전송해야 하므로
                //   HasEnemyInRange로 실제 적 존재 여부를 별도 확인.
                var attackResult = combat.TryFindTarget(unit);
                bool hasEnemy = attackResult.HasValue || combat.HasEnemyInRange(unit);

                if (hasEnemy)
                {
                    // 사거리 내 적이 있음 → 전투 상태 유지
                    activeThisTick.Add(id);

                    if (attackResult.HasValue)
                    {
                        // 쿨다운 만료 + 적 있음: 타겟 변경 감지 + 데미지 실행
                        int targetId = attackResult.Value.id;
                        bool isUnit = attackResult.Value.isUnit;

                        // 실제 데미지를 적용할 타겟 — 기본값은 TryFindTarget이 반환한 가장 가까운 적.
                        // IsCurrentTargetStillValid가 true이면 기존 타겟으로 덮어씌워짐.
                        int damageTargetId = targetId;
                        bool damageTargetIsUnit = isUnit;

                        // --- 상태 변화 감지: 이전 타겟과 비교하여 RPC 전송 여부 결정 ---
                        if (_unitCombatTargets.TryGetValue(id, out var prev))
                        {
                            if (prev.targetId != targetId)
                            {
                                // 현재 타겟이 아직 살아있고 사거리 내에 있으면 교체하지 않는다.
                                // 새 유닛이 더 가까이 들어왔더라도 현재 타겟을 유지하여 공격 집중도를 보장한다.
                                // 현재 타겟이 사망했거나 사거리를 벗어난 경우에만 교체를 허용한다.
                                if (!combat.IsCurrentTargetStillValid(unit, prev.targetId, prev.isUnit))
                                {
                                    // 기존 타겟이 유효하지 않음 → 새 타겟으로 교체
                                    _unitCombatTargets[id] = (targetId, isUnit);
                                    ChangeTargetClientRpc(id, targetId, isUnit);
                                }
                                else
                                {
                                    // 기존 타겟이 아직 유효 → 애니메이션과 데미지 모두 기존 타겟 유지
                                    // (TryFindTarget이 반환한 가까운 적이 아닌 기존 타겟에게 데미지 적용)
                                    damageTargetId = prev.targetId;
                                    damageTargetIsUnit = prev.isUnit;
                                }
                            }
                            // else: 같은 타겟 → RPC 없음 (Attack 루프 유지), damageTargetId = targetId (변경 없음)
                        }
                        else
                        {
                            // 새로 전투 진입 — Dictionary 등록만 수행.
                            // StartCombatClientRpc는 OnUnitEnteredCombatHandler에서 단독 담당.
                            _unitCombatTargets[id] = (targetId, isUnit);
                        }

                        // 데미지 코루틴 실행 (쿨다운 0일 때만 여기까지 도달)
                        // damageTargetId: 기존 타겟이 유효하면 기존 타겟, 아니면 새 타겟
                        ExecuteAttack(unit, damageTargetId, damageTargetIsUnit);
                    }
                    else
                    {
                        // 쿨다운 중 + 적 있음 — 데미지 없음, Attack 루프 유지.
                        // 단, 쿨다운 중에도 타겟이 변경되었을 수 있으므로 (규칙 5-2)
                        // FindNearestEnemy로 현재 가장 가까운 적을 확인하여 방향 전환 RPC 전송.
                        if (_unitCombatTargets.TryGetValue(id, out var prev))
                        {
                            var nearestResult = combat.FindNearestEnemy(unit);
                            if (nearestResult.HasValue && nearestResult.Value.id != prev.targetId)
                            {
                                // 쿨다운 중에도 타겟 교체 전 동일하게 현재 타겟 유효성을 확인한다.
                                // 현재 타겟이 살아있고 사거리 내에 있으면 회전 방향도 유지한다.
                                if (!combat.IsCurrentTargetStillValid(unit, prev.targetId, prev.isUnit))
                                {
                                    _unitCombatTargets[id] = (nearestResult.Value.id, nearestResult.Value.isUnit);
                                    ChangeTargetClientRpc(id, nearestResult.Value.id, nearestResult.Value.isUnit);
                                }
                            }
                        }
                    }
                }
            }

            // 이전 Tick에서 전투 중이었지만 이번 Tick에서 타겟을 찾지 못한 유닛 → StopCombat RPC
            // Dictionary 순회 중 수정 방지를 위해 제거할 키를 미리 수집
            var toRemove = new System.Collections.Generic.List<int>();
            foreach (var kvp in _unitCombatTargets)
            {
                if (!activeThisTick.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (int id in toRemove)
            {
                _unitCombatTargets.Remove(id);
                _combatAnimationSent.Remove(id); // RPC 전송 추적도 함께 정리
                StopCombatClientRpc(id);
            }
        }

        /// <summary>
        /// hitFrameTime만큼 대기 후 실제 데미지를 적용하는 코루틴.
        ///
        /// 서버가 애니메이션 RPC를 전송한 직후 시작되며,
        /// 클라이언트에서 공격 애니메이션의 타격 프레임(OnAttackHit)에 도달할 시점에
        /// 서버에서 데미지를 적용 → HP NetworkVariable 업데이트 → 클라이언트에 HP 감소 전달.
        ///
        /// 딜레이 동안 공격자/타겟 상태가 변할 수 있으므로
        /// ApplyAttackDamage() 내부에서 모든 조건(생존, 사거리)을 재확인.
        ///
        /// delay가 0이면 최소 1프레임 대기 (Inspector 미설정 시 안전망).
        /// </summary>
        /// <param name="attacker">공격하는 유닛의 데이터</param>
        /// <param name="targetId">타겟 엔티티의 Id</param>
        /// <param name="targetIsUnit">true=유닛, false=건물</param>
        /// <param name="delay">데미지 적용까지의 대기 시간 (초). Attack.anim의 타격 프레임 시간.</param>
        private IEnumerator DelayedAttackDamage(UnitData attacker, int targetId, bool targetIsUnit, float delay)
        {
            // hitFrameTime > 0이면 해당 시간만큼 대기
            // hitFrameTime == 0이면 최소 1프레임 대기 (Inspector 미설정 시 즉시 적용 방지)
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            else
                yield return null;

            // 딜레이 후 UseCase를 다시 가져와서 데미지 적용
            // _bootstrapper가 파괴되었을 수 있으므로 null 체크
            UnitCombatUseCase combat = _bootstrapper?.GetCombatUseCase();
            if (combat == null) yield break;

            // ApplyAttackDamage 내부에서 공격자 생존, 타겟 생존, 사거리를 모두 재확인
            combat.ApplyAttackDamage(attacker, targetId, targetIsUnit);
        }

        /// <summary>
        /// 타겟을 향한 데미지를 실행. 쿨다운 리셋 + 데미지 코루틴 시작.
        /// 애니메이션 RPC는 TickCombat의 상태 변화 감지 로직에서 별도로 전송.
        /// </summary>
        /// <param name="unit">공격하는 유닛의 데이터</param>
        /// <param name="targetId">타겟 엔티티의 Id</param>
        /// <param name="targetIsUnit">true=유닛, false=건물</param>
        private void ExecuteAttack(UnitData unit, int targetId, bool targetIsUnit)
        {
            // 1. 쿨다운 즉시 리셋 — TryFindTarget()은 쿨다운을 건드리지 않으므로 여기서 처리
            unit.AttackCooldownRemaining = unit.AttackCooldown;

            // 2. 각 히트 프레임 시간마다 독립 코루틴을 실행하여 데미지 타이밍 동기화.
            // 단일 히트 유닛: HitFrameTimes 원소가 1개 → 기존과 동일하게 코루틴 1개 실행.
            // 다중 히트 유닛(FlameSpirit 6히트, LionKnight 2히트): 원소 수만큼 코루틴 실행.
            // 각 코루틴은 독립적으로 동작하며, 타겟 사망 시 ApplyAttackDamage 내 IsAlive 체크로 자동 취소.
            foreach (float hitTime in unit.HitFrameTimes)
                StartCoroutine(DelayedAttackDamage(unit, targetId, targetIsUnit, hitTime));
        }

        // ====================================================================
        // 이벤트 핸들러 (서버 전용)
        // ====================================================================

        /// <summary>
        /// MoveAlongPath에서 적을 처음 감지했을 때 호출.
        /// TickCombat 인터벌(최대 50ms)을 기다리지 않고 즉시 StartCombatClientRpc 전송.
        ///
        /// ※ 가드 조건: _combatAnimationSent로 판단 (_unitCombatTargets 미사용).
        ///   이유: TickCombat(Update)이 코루틴(MoveAlongPath)보다 먼저 실행될 경우,
        ///   같은 프레임에 TickCombat이 _unitCombatTargets를 먼저 등록하여
        ///   ContainsKey 가드가 true를 반환 → RPC 미전송 버그 발생.
        ///   → RPC 전송 여부를 _combatAnimationSent로 분리하여 이 경쟁 조건을 해소.
        ///
        /// ※ ExecuteAttack 동시 호출 (쿨다운=0인 경우):
        ///   StartCombatClientRpc와 함께 바로 ExecuteAttack을 실행하면
        ///   서버 공격 사이클이 T=0에서 시작 → 애니메이션 루프(T=0)와 동기화.
        ///   그렇지 않으면 다음 TickCombat(T≈50ms)에서 ExecuteAttack이 처음 실행되어
        ///   서버 사이클이 T=0.05에서 시작 → 쿨다운 만료 T=3.05 ≠ 애니메이션 루프 경계 T=3.0.
        /// </summary>
        private void OnUnitEnteredCombatHandler(int unitId)
        {
            // _bootstrapper는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            if (_bootstrapper == null) return;

            UnitSpawnUseCase unitSpawn = _bootstrapper.GetUnitSpawn();
            UnitCombatUseCase combat = _bootstrapper.GetCombatUseCase();
            if (unitSpawn == null || combat == null) return;

            if (!unitSpawn.Units.TryGetValue(unitId, out UnitData unit)) return;
            if (!unit.IsAlive) return;

            // StartCombatClientRpc를 이미 전송한 유닛이면 중복 전송 방지.
            // _unitCombatTargets 대신 _combatAnimationSent를 사용하는 이유:
            //   TickCombat(Update)과 코루틴(MoveAlongPath) 실행 순서 경쟁 조건 방지.
            if (_combatAnimationSent.Contains(unitId)) return;

            // 타겟 탐색 — 쿨다운과 무관하게 사거리 내 첫 타겟 탐색
            // TryFindTarget은 쿨다운 체크 포함이므로 쿨다운 중이어도 FindNearestEnemy로 확인
            var attackResult = combat.TryFindTarget(unit);
            if (!attackResult.HasValue)
            {
                // TryFindTarget이 null인 경우: 쿨다운 중이거나 적이 없음.
                // FindNearestEnemy로 쿨다운 없이 재탐색하여 실제 적 존재 여부와 타겟 ID 확인.
                var nearestResult = combat.FindNearestEnemy(unit);
                if (!nearestResult.HasValue) return; // 실제로 적이 없으면 종료

                // 쿨다운 중이지만 적은 있음 → 즉시 StartCombatClientRpc 전송.
                // 규칙 1: 적 감지 즉시 클라이언트에 알려야 하며, 쿨다운은 데미지 타이밍과 무관.
                _unitCombatTargets[unitId] = (nearestResult.Value.id, nearestResult.Value.isUnit);
                _combatAnimationSent.Add(unitId);
                StartCombatClientRpc(unitId, nearestResult.Value.id, nearestResult.Value.isUnit);
                return;
            }

            int targetId = attackResult.Value.id;
            bool targetIsUnit = attackResult.Value.isUnit;

            // 전투 상태 등록 + StartCombatClientRpc 즉시 전송
            _unitCombatTargets[unitId] = (targetId, targetIsUnit);
            _combatAnimationSent.Add(unitId);
            StartCombatClientRpc(unitId, targetId, targetIsUnit);

            // ExecuteAttack 즉시 실행 — 서버 공격 사이클을 애니메이션 시작(T=0)과 동기화.
            // TickCombat에서 처음 실행되면 T≈50ms 오프셋이 생겨 쿨다운 만료 타이밍이
            // 애니메이션 루프 경계와 어긋남 → 타겟 소멸 후 애니메이션이 루프 직후 도중에 전환되는 현상.
            ExecuteAttack(unit, targetId, targetIsUnit);
        }

        /// <summary>
        /// 서버에서 유닛 사망 이벤트를 수신하여 모든 클라이언트에 사망 전파.
        /// 클라이언트에도 동일한 이벤트 체인을 재현하기 위해 ClientRpc로 전파.
        ///
        /// 강타입 DTO(UnitDiedEvent) 사용으로 얻는 이점:
        ///   - is 캐스트 분기 불필요. UnitDiedEvent.Unit이 이미 UnitData 강타입.
        ///   - 알 수 없는 엔티티 타입 분기 불필요 (DTO 자체가 유닛 전용).
        /// </summary>
        /// <param name="e">사망한 유닛 정보가 담긴 이벤트.</param>
        private void OnUnitDied(UnitDiedEvent e)
        {
            // 서버만 클라이언트에 전파한다. 클라이언트에서 도착하는 OnUnitDied는
            // EntityDiedClientRpc → HandleUnitDied 가 재발행한 것이므로 무시.
            if (!IsServer) return;
            if (e.Unit == null) return;

            int unitId = e.Unit.Id;

            // 사망한 유닛의 전투 상태를 Dictionary에서 제거.
            // StopCombatClientRpc는 전송하지 않음 — EntityDiedClientRpc가 사망 처리를 담당.
            // 사망한 타겟을 공격 중이던 유닛들의 전투 상태는 제거하지 않음.
            // → 다음 TickCombat에서 TryFindTarget이 다른 타겟을 찾거나 null을 반환.
            //   ChangeTargetClientRpc 또는 StopCombatClientRpc가 자연스럽게 발행됨.
            _unitCombatTargets.Remove(unitId);
            _combatAnimationSent.Remove(unitId);

            Debug.Log($"[Network] 서버: 유닛 사망. Id={unitId}");

            // 모든 클라이언트에 사망 전파. RPC 시그니처는 유지(isUnit=true).
            // EntityDiedClientRpc는 클라이언트의 도메인 데이터 정리 + OnUnitDied 재발행을 담당한다.
            EntityDiedClientRpc(unitId, true);

            // ────────────────────────────────────────────────────────────────
            // [핵심 수정 — 2026-06-08] NGO를 통한 클라이언트 GameObject 파괴.
            //
            // 왜 필요한가:
            //   서버 UnitView의 OnUnitDied 구독자가 Destroy(gameObject)를 호출해도
            //   그 파괴는 서버 로컬에서만 일어나며 NGO를 통해 클라이언트로 전파되지 않는다.
            //   결과적으로 클라이언트 화면에는 죽은 유닛의 GameObject가 그대로 남는다.
            //
            //   NGO에서 클라이언트의 네트워크 오브젝트를 함께 파괴하려면
            //   반드시 서버에서 NetworkObject.Despawn(destroy: true)를 명시적으로 호출해야 한다.
            //   이렇게 하면:
            //     1) NGO가 모든 클라이언트에 Despawn 메시지를 보낸다.
            //     2) 클라이언트에서 NetworkUnit.OnNetworkDespawn()이 호출되어
            //        사망 이펙트를 재생한 뒤 GameObject가 자동으로 파괴된다.
            //     3) 서버(Host)에서도 destroy: true에 의해 GameObject가 파괴되므로
            //        UnitView가 별도로 Destroy를 호출할 필요가 없다.
            //
            // 주의: 이 처리는 Infrastructure 레이어 안에서만 NGO API를 사용하므로
            //       레이어 규칙(Presentation에서 Unity.Netcode 직접 참조 금지)을 위반하지 않는다.
            // ────────────────────────────────────────────────────────────────
            if (_bootstrapper != null)
            {
                UnitFactory unitFactory = _bootstrapper.GetUnitFactory();
                GameObject unitObj = unitFactory != null
                    ? unitFactory.GetUnitObject(unitId)
                    : null;

                if (unitObj != null)
                {
                    NetworkObject netObj = unitObj.GetComponent<NetworkObject>();
                    // 아직 스폰 상태인 NetworkObject만 Despawn 가능.
                    // (이미 다른 경로로 Despawn된 경우 중복 호출 시 NGO가 경고를 출력하므로 가드)
                    if (netObj != null && netObj.IsSpawned)
                    {
                        netObj.Despawn(destroy: true);
                    }
                }
            }
        }

        /// <summary>
        /// 서버에서 건물 사망 이벤트를 수신하여 모든 클라이언트에 사망 전파.
        /// GameEndUseCase가 동일한 OnBuildingDied를 구독하므로 서버에서 이미 게임 종료 판정됨.
        /// 클라이언트에서도 EntityDiedClientRpc → HandleBuildingDied → OnBuildingDied 재발행으로
        /// 동일한 이벤트 체인을 재현한다.
        /// </summary>
        /// <param name="e">사망한 건물 정보가 담긴 이벤트.</param>
        private void OnBuildingDied(BuildingDiedEvent e)
        {
            if (!IsServer) return;
            if (e.Building == null) return;

            int buildingId = e.Building.Id;
            Debug.Log($"[Network] 서버: 건물 사망. Id={buildingId}");

            // 모든 클라이언트에 사망 전파. RPC 시그니처는 유지(isUnit=false).
            EntityDiedClientRpc(buildingId, false);
        }

        // ====================================================================
        // ClientRpc — 서버 → 모든 클라이언트
        // ====================================================================

        /// <summary>
        /// 서버에서 엔티티 사망 후 모든 클라이언트에 사망 처리 명령 전송.
        /// 클라이언트:
        ///   1. 도메인 데이터(Unit/Building) Dictionary에서 제거
        ///   2. GameEvents.OnUnitDied 또는 GameEvents.OnBuildingDied 발행
        ///      → 유닛 GameObject 파괴는 서버의 NetworkObject.Despawn(destroy:true)가
        ///        NGO를 통해 자동 처리(NetworkUnit.OnNetworkDespawn에서 이펙트 재생).
        ///      → 건물은 BuildingFactory가 OnBuildingDied 구독으로 GameObject 파괴.
        ///      → GameEndUseCase(클라이언트)가 Castle 사망 감지 → GameEndUI 반응
        /// </summary>
        /// <param name="entityId">사망한 엔티티 Id</param>
        /// <param name="isUnit">true=유닛, false=건물</param>
        [ClientRpc]
        private void EntityDiedClientRpc(int entityId, bool isUnit)
        {
            // 서버는 이미 UseCase에서 처리 완료 → 중복 방지
            if (IsServer) return;

            Debug.Log($"[Network] EntityDiedClientRpc 수신. Id={entityId}, IsUnit={isUnit}");

            // _bootstrapper는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            if (_bootstrapper == null)
            {
                Debug.LogError("[Network] EntityDiedClientRpc: GameBootstrapper를 찾을 수 없습니다.");
                return;
            }

            if (isUnit)
            {
                HandleUnitDied(entityId);
            }
            else
            {
                HandleBuildingDied(entityId);
            }
        }

        // ====================================================================
        // 전투 상태 ClientRpc — 서버 → 모든 클라이언트
        // ====================================================================

        /// <summary>
        /// 유닛이 전투 상태에 진입. 클라이언트에서 Attack 루프 시작 + 타겟 방향 회전.
        /// TickCombat에서 유닛이 처음 타겟을 발견했을 때 전송.
        /// 서버(Host)도 수신하여 동일한 애니메이션 처리.
        ///
        /// GameEvents.OnNetworkCombatStarted를 발행하면 UnitView가 자신의 Id에 해당하는
        /// 이벤트만 구독해 처리한다. 이렇게 하여 Infrastructure → Presentation 역방향 의존을 피한다.
        /// </summary>
        /// <param name="unitId">공격하는 유닛의 Id</param>
        /// <param name="targetId">타겟 엔티티의 Id</param>
        /// <param name="targetIsUnit">true=유닛, false=건물</param>
        [ClientRpc]
        private void StartCombatClientRpc(int unitId, int targetId, bool targetIsUnit)
        {
            // 주의: IsServer 분기를 추가하지 않는다.
            // 서버(Host)도 이 RPC를 수신하여 동일한 애니메이션 처리가 필요하기 때문.
            //
            // 이벤트 발행만 수행. UnitView가 OnNetworkCombatStarted를 구독해 자기 Id에 해당하면
            // StartCombatAnimation을 호출한다. 유닛 생성 직후 RPC가 도착해 UnitView가 아직 구독하지
            // 못한 경우라도, UnitView 측에서 OnEnable/OnNetworkSpawn 시점에 늦게 구독을 등록한 뒤
            // 다음 이벤트부터 처리한다(첫 Combat 이벤트는 다음 TickCombat에서 다시 발행됨).
            GameEvents.OnNetworkCombatStarted.OnNext(new NetworkCombatStartedEvent(unitId, targetId, targetIsUnit));
        }

        /// <summary>
        /// 전투 중 타겟 변경. 클라이언트에서 회전만 업데이트, 애니메이션 재시작 없음.
        /// TickCombat에서 유닛의 타겟이 이전과 다를 때 전송.
        /// </summary>
        /// <param name="unitId">공격하는 유닛의 Id</param>
        /// <param name="newTargetId">새 타겟 엔티티의 Id</param>
        /// <param name="newTargetIsUnit">true=유닛, false=건물</param>
        [ClientRpc]
        private void ChangeTargetClientRpc(int unitId, int newTargetId, bool newTargetIsUnit)
        {
            GameEvents.OnNetworkCombatTargetChanged.OnNext(
                new NetworkCombatTargetChangedEvent(unitId, newTargetId, newTargetIsUnit));
        }

        /// <summary>
        /// 유닛이 전투 상태 종료. 클라이언트에서 Attack → Idle 전환.
        /// TickCombat에서 유닛의 타겟이 사라졌을 때 전송.
        /// Idle로 전환하는 이유: 이동 재개 시 StartWalkAnimationClientRpc가 별도로 도착하므로.
        /// </summary>
        /// <param name="unitId">전투를 종료하는 유닛의 Id</param>
        [ClientRpc]
        private void StopCombatClientRpc(int unitId)
        {
            GameEvents.OnNetworkCombatStopped.OnNext(new NetworkCombatStoppedEvent(unitId));
        }

        /// <summary>
        /// 서버가 유닛 Walk 시작을 감지하여 모든 클라이언트에 Walk 애니메이션 재생 명령 전송.
        /// 서버(HOST)는 MoveAlongPath에서 이미 Animator를 직접 제어하므로 스킵.
        /// 클라이언트는 NetworkTransform으로 위치만 동기화받고,
        /// Walk 애니메이션은 이 RPC를 통해 별도로 동기화.
        ///
        /// 서버(HOST) 스킵 분기는 RPC 차원에서 유지하여 호스트의 중복 애니메이션 호출을 막는다.
        /// </summary>
        /// <param name="unitId">Walk를 시작한 유닛의 Id</param>
        [ClientRpc]
        private void StartWalkAnimationClientRpc(int unitId)
        {
            // 서버(HOST)는 MoveAlongPath에서 이미 Walk 애니메이션을 직접 제어함 → 중복 방지
            if (IsServer) return;

            GameEvents.OnNetworkWalkStarted.OnNext(new NetworkWalkStartedEvent(unitId));
        }

        // StopWalkAnimationClientRpc 제거 — Idle 상태가 없으므로 Walk 정지 RPC 불필요.
        // Walk→Attack: StartCombatClientRpc에서 Attack CrossFade 직접 전환.
        // Walk→Walk: StartWalkAnimationClientRpc에서 처리.

        // ====================================================================
        // 사망 처리 헬퍼 (클라이언트 전용)
        // ====================================================================

        /// <summary>
        /// 클라이언트 측 유닛 사망 처리.
        /// UnitData를 Dictionary에서 제거하고 GameEvents.OnUnitDied 발행.
        /// UnitView가 이를 구독하여 GameObject를 파괴.
        ///
        /// 사망 이벤트 채널은 유닛 전용 OnUnitDied / 건물 전용 OnBuildingDied 둘로 나뉜다.
        /// </summary>
        private void HandleUnitDied(int unitId)
        {
            UnitSpawnUseCase unitSpawn = _bootstrapper.GetUnitSpawn();
            if (unitSpawn == null)
            {
                Debug.LogWarning("[Network] HandleUnitDied: UnitSpawnUseCase가 null.");
                return;
            }

            UnitData unit = unitSpawn.GetUnit(unitId);
            if (unit == null)
            {
                // 이미 처리되었거나 없는 유닛 (조용히 무시)
                return;
            }

            // HP가 0이 아니라면 강제로 0으로 맞춤 (HP 동기화 패킷보다 사망 패킷이 먼저 도착한 경우)
            if (unit.IsAlive)
            {
                unit.TakeDamage(unit.Hp);
            }

            // 도메인 Dictionary에서 제거
            unitSpawn.RemoveUnit(unitId);

            // GameEvents.OnUnitDied 재발행 → UnitView 구독자가 GameObject를 파괴.
            GameEvents.OnUnitDied.OnNext(new UnitDiedEvent(unit));

            Debug.Log($"[Network] 클라이언트: 유닛 사망 처리 완료. UnitId={unitId}");
        }

        /// <summary>
        /// 클라이언트 측 건물 사망 처리.
        /// BuildingData를 Dictionary에서 제거하고 GameEvents.OnBuildingDied 발행.
        /// BuildingFactory가 이를 구독하여 GameObject를 파괴.
        /// GameEndUseCase도 OnBuildingDied를 구독 → Castle 파괴 시 게임 종료 판정.
        ///
        /// 사망 이벤트 채널은 유닛 전용 OnUnitDied / 건물 전용 OnBuildingDied 둘로 나뉜다.
        /// </summary>
        private void HandleBuildingDied(int buildingId)
        {
            BuildingPlacementUseCase buildingPlacement = _bootstrapper.GetBuildingPlacement();
            if (buildingPlacement == null)
            {
                Debug.LogWarning("[Network] HandleBuildingDied: BuildingPlacementUseCase가 null.");
                return;
            }

            BuildingData building = buildingPlacement.GetBuilding(buildingId);
            if (building == null)
            {
                // 이미 처리되었거나 없는 건물 (조용히 무시)
                return;
            }

            // HP 강제 소진 (클라이언트 HP 동기화보다 사망 패킷이 먼저 도착한 경우 대비)
            if (building.IsAlive)
            {
                building.TakeDamage(building.Hp);
            }

            // 도메인 Dictionary에서 제거
            buildingPlacement.RemoveBuilding(buildingId);

            // GameEvents.OnBuildingDied 재발행 → BuildingFactory, GameEndUseCase 등 구독자가 후속 처리.
            GameEvents.OnBuildingDied.OnNext(new BuildingDiedEvent(building));

            Debug.Log($"[Network] 클라이언트: 건물 사망 처리 완료. BuildingId={buildingId}");
        }
    }
}
