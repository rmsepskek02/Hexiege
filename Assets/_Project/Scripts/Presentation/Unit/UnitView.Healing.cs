using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    public partial class UnitView
    {
        /// <summary>이 View와 같은 객체의 실제 의존성을 확보한다. 시각 조회나 권위 허가는 하지 않는다.</summary>
        private void EnsureHealNetworkUnit()
        {
            // Unity의 destroyed-object null 검사도 사용한다. 다른 객체의 cache는 재사용하지 않는다.
            // component가 없거나 아직 spawn 전이어도 만들거나 권위를 우회하지 않고 기존 guard에 맡긴다.
            if (_healNetworkUnit == null || _healNetworkUnit.gameObject != gameObject)
                _healNetworkUnit = GetComponent<NetworkUnit>();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveFirstHealEntry(bool accepted, bool cacheBefore)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // 회차 생성 전 증거다. 유효 cast를 위조하지 않고 lifetime당 승인/거부 각각 한 번만 남긴다.
            byte bit = accepted ? (byte)1 : (byte)2;
            if ((_healFirstEntryEvidence & bit) != 0) return;
            _healFirstEntryEvidence |= bit;
            bool hasComponent = _healNetworkUnit != null;
            bool spawned = hasComponent && _healNetworkUnit.IsSpawned;
            GameLog.Dev.Info("Combat", nameof(UnitView), "[BLOOM-HEAL] FirstEntry",
                System.FormattableString.Invariant($"UnitId={_unitData.Id}, Lifetime={_healLifetime}, CastAllocated=false, Accepted={accepted}, CacheBefore={cacheBefore}, CacheAfter={hasComponent}, SameObject={hasComponent && _healNetworkUnit.gameObject == gameObject}, NetworkActive={NetworkContext.IsNetworkActive}, NetworkServer={NetworkContext.IsNetworkServer}, Spawned={spawned}, ComponentServer={spawned && _healNetworkUnit.IsServer}, Enabled={isActiveAndEnabled}, Alive={_unitData.IsAlive}, LocalNow={Time.timeAsDouble:G17}, Frame={Time.frameCount}"));
#endif
        }

        private void UpdateHealFacing(float deltaTime)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _healProbeBeforeYaw = transform.eulerAngles.y;
#endif
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
            { ObserveHealFacingTransition(); return; }
            if (_healPresentation.Current.Active && _rootRotationOwnership.ActionOwnsRoot
                && _healPresentation.Current.Priority == 1 && _healFacingTarget != null
                && _healFacingTargetView != null && _healFacingTargetView.Data != null
                && _healFacingTargetView.Data.IsAlive)
                RotateActionTowards(_healFacingTarget.position, deltaTime);
            ObserveHealFacingTransition();
        }

        // 진단은 gameplay cache를 복구하거나 projector getter를 호출하지 않는다.
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveHealFacingTransition()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            var state = _healPresentation.Current;
            if (!state.IsValid || !state.Active || Time.timeAsDouble < _healProbeNextSample) return;
            _healProbeNextSample = Time.timeAsDouble + 0.25d;
            string reason = HealFacingProbeReason(state);
            // 1도는 관찰용 문턱이며 회전 허가/HP 판정에는 쓰지 않는다.
            bool drifted = Mathf.Abs(Mathf.DeltaAngle(_healProbeStartYaw, transform.eulerAngles.y)) > 1f;
            bool converged = false;
            if (_healFacingTarget != null)
            {
                Vector3 delta = _healFacingTarget.position - transform.position; delta.y = 0;
                converged = delta.sqrMagnitude >= 0.001f
                    && Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y,
                        Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg)) <= 1f;
            }
            if (reason == _healProbeReason && (!converged || _healProbeConverged)
                && drifted == _healProbeDrifted) return;
            _healProbeReason = reason;
            _healProbeDrifted = drifted;
            _healProbeConverged |= converged;
            if (++_healProbeTransitions <= 8) ObserveHealPose("FacingTransition", state);
            else if (_healProbeTransitions == 9)
                UnitHealCastDiagnostics.Event("FacingOverflow", state, "TransitionLimit=8, Coverage=Incomplete");
#endif
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private string HealFacingProbeReason(UnitHealCastPresentation state)
        {
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return "ClientNoWriter";
            if (!state.Active) return "Inactive";
            if (!_rootRotationOwnership.ActionOwnsRoot) return "NotActionOwner";
            if (state.Priority != 1) return "Priority";
            if (_healFacingTarget == null) return "CacheMissing";
            if (_healFacingTargetView == null || _healFacingTargetView.Data == null) return "TargetViewMissing";
            if (!_healFacingTargetView.Data.IsAlive) return "TargetDead";
            Vector3 delta = _healFacingTarget.position - transform.position; delta.y = 0;
            return delta.sqrMagnitude < 0.001f ? "NearZeroPreserveYaw" : "Eligible";
        }
#endif

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveHealPose(string stage, UnitHealCastPresentation state)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (!state.IsValid || _unitData == null || !_unitData.IsHealer) return;
            if (_healProbeSequence != state.Sequence || _healProbeLifetime != state.Lifetime)
            {
                _healProbeSequence = state.Sequence; _healProbeLifetime = state.Lifetime;
                _healProbeStartYaw = transform.eulerAngles.y; _healProbeCount = 0;
                _healProbeTransitions = 0; _healProbeReason = null; _healProbeConverged = false;
                _healProbeDrifted = false; _healProbeBaselineStage = stage;
                _healProbeNextSample = 0;
            }
            if (++_healProbeCount > 24)
            {
                if (_healProbeCount == 25) UnitHealCastDiagnostics.Event("PoseOverflow", state,
                    "CheckpointLimit=24, Coverage=Incomplete");
                return;
            }
            // lookup는 관찰용 local 변수이며 실제 회전 cache에는 쓰지 않는다.
            Transform target = GetTargetTransform(state.TargetId, true);
            Vector3 pos = transform.position;
            Vector3 delta = target != null ? target.position - pos : Vector3.zero; delta.y = 0;
            bool defined = state.TargetId != state.UnitId && target != null && delta.sqrMagnitude >= 0.001f;
            float yaw = transform.eulerAngles.y;
            float expectedAngle = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            string expected = defined ? expectedAngle.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) : "Undefined";
            string angleError = defined ? Mathf.DeltaAngle(yaw, expectedAngle).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) : "Undefined";
            UnitHealCastDiagnostics.Event("PoseRoot-" + stage, state, FormattableString.Invariant(
                $"Now={HealNow:G17}, Elapsed={HealNow - state.StartedAt:F6}, Self={state.UnitId == state.TargetId}, Lookup={target != null}, Cache={_healFacingTarget != null}, CacheMatch={target != null && target == _healFacingTarget}, Owner={_rootRotationOwnership.Owner}, Frozen={_isFrozenAnim}, Held={_isMovementHeldAnim}, Reason={HealFacingProbeReason(state)}, Yaw={yaw:F3}, Baseline={_healProbeStartYaw:F3}, Drift={Mathf.DeltaAngle(_healProbeStartYaw, yaw):F3}, ExpectedYaw={expected}"));
            if (stage == "FacingTransition")
                UnitHealCastDiagnostics.Event("FacingStep", state, FormattableString.Invariant(
                    $"BeforeYaw={_healProbeBeforeYaw:F3}, AfterYaw={yaw:F3}, Step={Mathf.DeltaAngle(_healProbeBeforeYaw, yaw):F3}, Converged={_healProbeConverged}, DriftThresholdDeg=1, Drifted={_healProbeDrifted}"));
            UnitHealCastDiagnostics.Event("PoseCompare-" + stage, state,
                $"AngleError={angleError}, BaselineStage={_healProbeBaselineStage}, CacheUnitId={(_healFacingTargetView != null && _healFacingTargetView.Data != null ? _healFacingTargetView.Data.Id : -1)}, CacheAlive={_healFacingTargetView != null && _healFacingTargetView.Data != null && _healFacingTargetView.Data.IsAlive}");
            UnitHealCastDiagnostics.Event("PoseVector-" + stage, state, FormattableString.Invariant(
                $"CasterXZ={pos.x:F3}/{pos.z:F3}, TargetXZ={(target != null ? target.position.x : float.NaN):F3}/{(target != null ? target.position.z : float.NaN):F3}, DeltaXZ={delta.x:F3}/{delta.z:F3}, Sqr={delta.sqrMagnitude:F6}, RootForward={transform.forward.x:F3}/{transform.forward.z:F3}, PayloadAim={state.AimX:F3}/{state.AimZ:F3}"));
            Transform visual = _visualRootProjector != null ? _visualRootProjector.ObservedVisualRoot : null;
            Transform model = _animator != null ? _animator.transform : null;
            var anim = _animator != null ? _animator.GetCurrentAnimatorStateInfo(0) : default;
            UnitHealCastDiagnostics.Event("PoseVisual-" + stage, state, FormattableString.Invariant(
                $"Visual={visual != null}, VisualYaw={(visual != null ? visual.eulerAngles.y : float.NaN):F3}, VisualLocalYaw={(visual != null ? visual.localEulerAngles.y : float.NaN):F3}, ModelProxy={model != null}, ModelForward={(model != null ? model.forward.x : float.NaN):F3}/{(model != null ? model.forward.z : float.NaN):F3}, Flip={ViewConverter.IsFlipped}, State={anim.fullPathHash}, Normalized={anim.normalizedTime:F6}, Speed={(_animator != null ? _animator.speed : float.NaN):F3}, Phase=CallbackSampleNotRenderedFrame"));
            UnitHealCastDiagnostics.Event("VisualForward-" + stage, state, FormattableString.Invariant(
                $"Forward={(visual != null ? visual.forward.x : float.NaN):F3}/{(visual != null ? visual.forward.z : float.NaN):F3}, LocalForward={(visual != null ? (visual.localRotation * Vector3.forward).x : float.NaN):F3}/{(visual != null ? (visual.localRotation * Vector3.forward).z : float.NaN):F3}"));
#endif
        }

        /// <summary>동일 힐 회차의 생명주기와 서버/싱글 권위가 대기 중에도 유지되는지 확인한다.</summary>
        private bool CanContinueHealWindup(UnitData caster, UnitHealCastPresentation cast, bool networkCast)
        {
            // View 재사용/회차 종료 뒤 기존 예약은 폐기하며, 종료된 Network 경기를 solo로 오인하지 않는다.
            var current = _healPresentation.Current;
            if (!isActiveAndEnabled || !ReferenceEquals(_unitData, caster) || caster == null || !caster.IsAlive
                || !current.IsValid || !current.Active || current.Lifetime != cast.Lifetime
                || current.Sequence != cast.Sequence || current.UnitId != cast.UnitId
                || current.TargetId != cast.TargetId)
                return false;
            if (!networkCast) return !NetworkContext.IsNetworkActive;
            return NetworkContext.IsNetworkActive && NetworkContext.IsNetworkServer
                && _healNetworkUnit != null && _healNetworkUnit.IsSpawned && _healNetworkUnit.IsServer;
        }

        private static bool IsFiniteHealTime(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        // 회차의 revision(우선순위)은 달라질 수 있지만 identity/수명/sequence는 바뀌면 안 된다.
        private bool OwnsHealScope(UnitData caster, UnitHealCastPresentation cast, ulong owner)
        {
            var current = _healPresentation.Current;
            return owner == _healLoopOwner && ReferenceEquals(_unitData, caster)
                && current.Lifetime == cast.Lifetime && current.Sequence == cast.Sequence
                && current.UnitId == cast.UnitId && current.TargetId == cast.TargetId;
        }

        private bool HasHealAuthority(UnitData caster, bool networkCast)
        {
            return isActiveAndEnabled && ReferenceEquals(_unitData, caster) && caster != null && caster.IsAlive
                && (networkCast
                    ? NetworkContext.IsNetworkActive && NetworkContext.IsNetworkServer
                        && _healNetworkUnit != null && _healNetworkUnit.IsSpawned && _healNetworkUnit.IsServer
                    : !NetworkContext.IsNetworkActive);
        }

        private bool HasOwnedHealCooldown(UnitData caster, bool networkCast)
        {
            return ReferenceEquals(_healCooldownCaster, caster) && _healCooldownCast.Lifetime == _healLifetime
                && _healCooldownNetwork == networkCast
                && (!networkCast || ReferenceEquals(_healCooldownNetworkOwner, _healNetworkUnit));
        }

        private void CloseOwnedHealLoop(UnitData caster, UnitHealCastPresentation cast, ulong owner, string reason)
        {
            // 이전 coroutine가 새 모션/회전/추격 상태를 정리하지 못하게 먼저 소유권을 검사한다.
            if (!OwnsHealScope(caster, cast, owner)) return;
            EndHealPresentation(reason, terminal: caster == null || !caster.IsAlive);
            _isInCombatPursuit = false;
        }

        /// <summary>
        /// [V3 신규] 힐러(BloomFairy) 전용 힐 루프 — 적 공격 흐름과 완전히 분리된 경로.
        ///
        /// 동작(설계 확정 6):
        ///   1) FindInjuredAllyToHeal로 힐 사거리 내 "가장 급한 부상 아군"을 선택.
        ///      없으면 Walk 애니메이션 재개 후 종료(→ 호출 측이 A* 이동을 재개).
        ///   2) 대상을 향해 회전 + 힐 애니메이션 재생(시전 중 이동 없음).
        ///   3) 힐 타격 타이밍(HitFrameTimes[0]) 대기 후 대상에 HoT(3초 20HP) 1회 부여(CastHeal).
        ///   4) 쿨다운(AttackCooldown, 3초) 대기.
        ///   5) 다시 재탐색(1로 반복). 대상이 풀피/사거리 이탈이면 자연히 다른 부상 아군으로 전환.
        ///
        /// 서버 권위: 이 코루틴은 서버(싱글=로컬 서버/멀티=서버)에서만 도는 MoveAlongPathV3에서만
        ///   호출되므로 CastHeal(HoT 부여)도 서버에서만 실행된다. 클라이언트는 HP/치유 텍스트를
        ///   NetworkHealthSync로 수신하고, 힐 모션은 별도 회차 atomic snapshot으로 복원한다.
        ///
        /// HoT 부여 타이밍(설계 결정):
        ///   실제 회복 발동은 애니메이션 이벤트(OnAttackHit)가 아니라 이 코루틴의 HitFrameTimes 타이머로
        ///   구동한다. 이유 — 기존 데미지 시스템도 애니 이벤트가 아니라 HitFrameTimes 타이머로 데미지를
        ///   적용하며(코드베이스 관례), 애니 이벤트 의존은 클립에 이벤트가 주입되기 전까지 회복이 아예
        ///   발동하지 않는 취약점이 있기 때문이다. 타이밍(=HitFrameTimes[0])은 동일하다.
        ///   (OnAttackHit 애니 이벤트는 힐 이펙트/사운드 연출 용도로만 남는다.)
        /// </summary>
        private IEnumerator EnterHealLoopV3(bool stationaryWatch = false)
        {
            // 필수 의존성 가드 — _positionProvider는 대상 방향 회전에 사용.
            if (_combatUseCase == null || _positionProvider == null || _unitData == null)
                yield break;

            UnitData loopCaster = _unitData;
            bool loopNetwork = NetworkContext.IsNetworkActive;
            ulong loopOwner = ++_healLoopOwner;
            // 이전 순서(실기 수용 전 비교용): clock getter가 먼저 cache를 채웠다고 가정했다.
            // if (!HasHealAuthority(loopCaster, loopNetwork)) yield break;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            bool cacheBefore = _healNetworkUnit != null;
#endif
            EnsureHealNetworkUnit();
            bool entryAccepted = HasHealAuthority(loopCaster, loopNetwork);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            ObserveFirstHealEntry(entryAccepted, cacheBefore);
#endif
            if (!entryAccepted) yield break;

            // IdleWatch도 이 공통 경계를 사용한다. 이동 writer가 멈춘 뒤 서버 Action이
            // 회전을 소유하게 하며 Client Root writer를 추가하지 않는다.
            _rootRotationOwnership.TransferToAction();
            _healStationaryWatch = stationaryWatch;
            // 직전 locomotion Held만 해제한다. Frozen flag와 실제 HP 정책은 유지한다.
            SetMovementHeldAnimation(false);

            // 힐 시전 중임을 표시 — IsInCombat()가 true를 반환하도록 하여, 건물 변화로 인한
            //   RepathAllAliveUnits → OnPathInvalidated가 힐 도중 코루틴을 끊지 않게 한다(추격과 동일 취지).
            //   Unity 코루틴은 try/finally가 동작하지 않으므로 각 yield break 직전에 false로 명시 리셋한다.
            _isInCombatPursuit = true;

            int? target = _combatUseCase.FindInjuredAllyToHeal(_unitData);

            while (_unitData != null && _unitData.IsAlive)
            {
                if (loopOwner != _healLoopOwner || !HasHealAuthority(loopCaster, loopNetwork)) yield break;
                // 1) 부상 아군 재탐색.
                if (!target.HasValue)
                {
                    if (stationaryWatch) SetMovementHeldAnimation(true);
                    EndHealPresentation("no-target");
                    // 더 힐할 아군 없음 → Walk 애니메이션 재개 후 종료(호출 측이 A* 이동 재개).
                    ResumeWalkAnimation();
                    if (NetworkContext.IsNetworkActive)
                        GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);
                    _isInCombatPursuit = false;
                    yield break;
                }

                int targetId = target.Value;

                // 재진입도 actual dispatch deadline을 존중한다. 전역 Remaining은 별도 기존 조건이다.
                // clock 모드/Network 객체가 바뀌면 오래된 clock의 deadline을 다른 clock에 비교하지 않는다.
                var entryScope = _healPresentation.Current;
                while (HasOwnedHealCooldown(loopCaster, loopNetwork) || loopCaster.AttackCooldownRemaining > 0f)
                {
                    if (!OwnsHealScope(loopCaster, entryScope, loopOwner)
                        || !HasHealAuthority(loopCaster, loopNetwork)) yield break;
                    double entryNow = HealNow;
                    if (!IsFiniteHealTime(entryNow))
                    {
                        UnitHealCastDiagnostics.Event("CooldownRejected", _healCooldownCast, "Reason=invalid-clock");
                        CloseOwnedHealLoop(loopCaster, entryScope, loopOwner, "invalid-time");
                        yield break;
                    }
                    if ((!HasOwnedHealCooldown(loopCaster, loopNetwork) || entryNow >= _healCooldownDue)
                        && loopCaster.AttackCooldownRemaining <= 0f)
                    {
                        if (HasOwnedHealCooldown(loopCaster, loopNetwork))
                        {
                            UnitHealCastDiagnostics.Event("CooldownComplete", _healCooldownCast,
                                System.FormattableString.Invariant($"Now={entryNow:G17}, Elapsed={entryNow - _healCooldownDispatchAt:G17}, Remaining={loopCaster.AttackCooldownRemaining:G17}, Reentry=true"));
                            _healCooldownCaster = null;
                        }
                        break;
                    }
                    yield return null;
                }
                if (!OwnsHealScope(loopCaster, entryScope, loopOwner)
                    || !HasHealAuthority(loopCaster, loopNetwork)) yield break;

                // 2) 대상을 향해 회전 + 힐 애니메이션 재생(시전 중 이동 없음).
                PlayHealCastAnimation(targetId);

                // 이전 준비 대기: 이 프레임의 deltaTime에는 시전 이전 시간도 포함될 수 있다.
                // 사용자 실기 수용 전 비교용으로 보존하며, 아래 주석 경로는 실행하지 않는다.
                /*
                float hitDelay = (_unitData.HitFrameTimes != null && _unitData.HitFrameTimes.Length > 0)
                    ? _unitData.HitFrameTimes[0]
                    : 0.2f;
                float t = 0f;
                while (t < hitDelay && _unitData != null && _unitData.IsAlive)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
                */

                // 3) 이미 게시한 힐 회차의 시계/시작점을 그대로 사용한다. 새 timer를 시작하거나
                // 애니메이션 Marker를 HP 입력으로 쓰지 않는다. 현재 시각이 절대 due에 도달한
                // 첫 유효 서버/싱글 프레임에만 아래 기존 writer로 한 번 진행한다.
                UnitData healCaster = _unitData;
                UnitHealCastPresentation healCast = _healPresentation.Current;
                bool networkHealCast = NetworkContext.IsNetworkActive;
                double healDue = healCast.StartedAt + healCast.MarkerOffset;
                double healActualNow = HealNow;
                if (!healCast.IsValid || double.IsNaN(healDue) || double.IsInfinity(healDue)
                    || double.IsNaN(healActualNow) || double.IsInfinity(healActualNow))
                {
                    UnitHealCastDiagnostics.Event("DispatchRejected", healCast, "Reason=invalid-time-or-state");
                    CloseOwnedHealLoop(healCaster, healCast, loopOwner, "invalid-time");
                    yield break;
                }

                while (CanContinueHealWindup(healCaster, healCast, networkHealCast)
                    && healActualNow < healDue)
                {
                    yield return null;
                    if (!OwnsHealScope(healCaster, healCast, loopOwner)) yield break;
                    if (!CanContinueHealWindup(healCaster, healCast, networkHealCast)) break;
                    healActualNow = HealNow;
                    // NaN 비교는 항상 false이므로 while 비교만으로는 조기 발동을 막지 못한다.
                    // invalid clock을 deltaTime 폴백으로 우회하지 않고 회차를 닫는다.
                    if (double.IsNaN(healActualNow) || double.IsInfinity(healActualNow))
                    {
                        UnitHealCastDiagnostics.Event("DispatchRejected", healCast, "Reason=invalid-clock");
                        CloseOwnedHealLoop(healCaster, healCast, loopOwner, "invalid-time");
                        yield break;
                    }
                }
                // HoT 1회 부여(서버 권위). 대상이 시전 도중 사망/제거됐으면 CastHeal 내부에서 무시된다.
                if (_unitData == null || !_unitData.IsAlive)
                { CloseOwnedHealLoop(healCaster, healCast, loopOwner, "death"); yield break; }
                // game-end/disable/despawn/회차 교체 뒤에는 이전 시전을 실행하지 않는다.
                // revision만 바뀐 동일 회차(Frozen/Held 포함)는 기존 정책대로 유지한다.
                if (!OwnsHealScope(healCaster, healCast, loopOwner)
                    || !CanContinueHealWindup(healCaster, healCast, networkHealCast))
                {
                    UnitHealCastDiagnostics.Event("DispatchRejected", healCast, "Reason=scope-or-authority-closed");
                    CloseOwnedHealLoop(healCaster, healCast, loopOwner, "scope-or-authority-closed");
                    yield break;
                }
                // 실제 호출 직전 시각이 기준이다. 준비 due나 첫 HP 업데이트 시각이 아니다.
                double dispatchAt = HealNow;
                float configuredCooldown = healCaster.AttackCooldown;
                double cooldownDue = dispatchAt + configuredCooldown;
                if (!IsFiniteHealTime(dispatchAt) || !IsFiniteHealTime(configuredCooldown)
                    || configuredCooldown <= 0f || !IsFiniteHealTime(cooldownDue) || dispatchAt < healDue)
                {
                    UnitHealCastDiagnostics.Event("DispatchRejected", healCast, "Reason=invalid-dispatch-time-or-cooldown");
                    CloseOwnedHealLoop(healCaster, healCast, loopOwner, "invalid-time");
                    yield break;
                }
                if (!OwnsHealScope(healCaster, healCast, loopOwner)
                    || !CanContinueHealWindup(healCaster, healCast, networkHealCast)) yield break;
                UnitHealCastDiagnostics.Event("CastDispatch", _healPresentation.Current,
                    System.FormattableString.Invariant($"Due={healDue:G17}, Now={dispatchAt:G17}, Elapsed={dispatchAt - healCast.StartedAt:G17}, DispatchAt={dispatchAt:G17}, ConfiguredCooldown={configuredCooldown:G17}, CooldownDue={cooldownDue:G17}"));
                _healCooldownCaster = healCaster;
                _healCooldownCast = healCast;
                _healCooldownNetwork = networkHealCast;
                _healCooldownNetworkOwner = _healNetworkUnit;
                _healCooldownDispatchAt = dispatchAt;
                _healCooldownDue = cooldownDue;
                _combatUseCase.CastHeal(_unitData, targetId, _healPresentation.Current);

                // 이전 counter-only 대기(실기 수용 전 비교용, 실행하지 않는다).
                /*
                _unitData.AttackCooldownRemaining = _unitData.AttackCooldown;
                while (_unitData != null && _unitData.IsAlive && _unitData.AttackCooldownRemaining > 0f)
                    yield return null;
                */
                if (!OwnsHealScope(healCaster, healCast, loopOwner)) yield break;
                // global ticker는 그대로 감소한다. deadline까지의 값을 counter에 매 프레임 덮어쓰지 않는다.
                healCaster.AttackCooldownRemaining = configuredCooldown;
                while (true)
                {
                    if (!OwnsHealScope(healCaster, healCast, loopOwner)) yield break;
                    if (!CanContinueHealWindup(healCaster, healCast, networkHealCast))
                    {
                        UnitHealCastDiagnostics.Event("CooldownCancelled", healCast, "Reason=scope-or-authority-closed");
                        CloseOwnedHealLoop(healCaster, healCast, loopOwner, healCaster.IsAlive ? "scope-or-authority-closed" : "death");
                        yield break;
                    }
                    double cooldownNow = HealNow;
                    if (!IsFiniteHealTime(cooldownNow))
                    {
                        UnitHealCastDiagnostics.Event("CooldownRejected", healCast, "Reason=invalid-clock");
                        CloseOwnedHealLoop(healCaster, healCast, loopOwner, "invalid-time");
                        yield break;
                    }
                    if (cooldownNow >= cooldownDue && healCaster.AttackCooldownRemaining <= 0f)
                    {
                        UnitHealCastDiagnostics.Event("CooldownComplete", healCast,
                            System.FormattableString.Invariant($"Now={cooldownNow:G17}, Elapsed={cooldownNow - dispatchAt:G17}, Remaining={healCaster.AttackCooldownRemaining:G17}"));
                        if (HasOwnedHealCooldown(healCaster, networkHealCast)
                            && _healCooldownCast.Sequence == healCast.Sequence) _healCooldownCaster = null;
                        break;
                    }
                    yield return null;
                }

                // 다음 대상이 있으면 같은 Action에서 새 sequence로 직접 이어간다.
                // inactive Walk/Held를 중간 게시하지 않는다. 대상 없음은 상단에서 닫는다.
                target = _combatUseCase.FindInjuredAllyToHeal(_unitData);
            }

            // while 자연 종료(_unitData null/사망) 안전망 리셋.
            if (loopOwner == _healLoopOwner && ReferenceEquals(_unitData, loopCaster))
            {
                EndHealPresentation("loop-exit", terminal: !loopCaster.IsAlive);
                _isInCombatPursuit = false;
            }
        }

        /// <summary>
        /// [이슈 2 수정 — 2026-07-18] 힐러(BloomFairy) 전용 유휴 감시 코루틴.
        ///
        /// 목적:
        ///   힐러가 이동 경로 끝(적 성 인접 등 더 이상 이동할 곳이 없는 지점)에 도달했을 때
        ///   코루틴을 영구히 끝내지 않고, "살아있는 동안 부상 아군을 계속 지켜보게" 한다.
        ///   이 코루틴은 MoveAlongPathV3의 cleanup에서 힐러일 때만 시작되며 _moveCoroutine으로 추적된다.
        ///
        /// 동작(짧은 주기 감시):
        ///   1) 힐 사거리 내 부상 아군이 있으면(ShouldEngage → HasInjuredAllyInHealRange) 즉시
        ///      EnterHealLoopV3(힐 루프)로 재진입한다. 힐 루프가 끝나면 다시 이 감시 상단으로 돌아온다.
        ///   2) 이동을 재개할 새 경로가 예약되면(_pendingPath — OnPathInvalidated가 건물 변화 등으로 예약)
        ///      그 경로로 MoveTo하여 기존 이동 로직으로 자연스럽게 복귀한다(감시 코루틴은 종료).
        ///   3) 둘 다 아니면 watchInterval(0.4초)마다 재확인하며 대기한다(매 프레임 탐색 비용 회피).
        ///
        /// 서버 권위 / 이중 틱 원칙:
        ///   이 감시 루프는 서버(싱글=로컬 서버/멀티=서버)에서만 도는 MoveAlongPathV3에서만 시작되며,
        ///   상태머신(코루틴) 레벨의 "지켜보기"일 뿐 회복을 직접 적용하지 않는다. 실제 힐 발동은
        ///   EnterHealLoopV3 → CastHeal(서버 권위) 경로가 그대로 담당한다(감시가 힐 로직을 바꾸지 않음).
        ///
        /// 기존 흐름과의 정합:
        ///   - 감시 중에는 _isInCombatPursuit이 false이므로 OnPathInvalidated가 정상적으로 _pendingPath를
        ///     예약할 수 있다(감시가 이를 소비해 이동 재개). 힐 루프 진입 중에는 EnterHealLoopV3가
        ///     _isInCombatPursuit=true로 두어 OnPathInvalidated가 힐을 끊지 않는다(추격과 동일 보호).
        ///   - 외부/공성 시스템이 새 MoveTo를 호출하면 _moveCoroutine이 교체되며 이 코루틴은 정지된다.
        /// </summary>
        private IEnumerator HealerIdleWatchV3()
        {
            SetMovementHeldAnimation(true);
            // 부상 아군 재확인 주기(초). 너무 짧으면 매 프레임 탐색 비용, 너무 길면 반응이 굼떠진다.
            const float watchInterval = 0.4f;

            while (_unitData != null && _unitData.IsAlive)
            {
                // (1) 부상 아군 감지 → 즉시 힐 루프 재진입. 힐 루프 종료 후에도 계속 지켜보기 위해 continue.
                if (ShouldEngage())
                {
                    yield return EnterHealLoopV3(stationaryWatch: true);
                    continue;
                }

                // (2) 이동 재개용 새 경로가 예약됐으면(OnPathInvalidated) 그 경로로 이동을 재개한다.
                //     MoveTo가 _moveCoroutine을 새 이동 코루틴으로 교체하므로 이 감시 코루틴은 여기서 종료한다.
                if (_pendingPath != null)
                {
                    List<HexCoord> resumePath = _pendingPath;
                    _pendingPath = null;
                    _rootRotationOwnership.ReleaseAction(resumeMovement: false);
                    SetMovementHeldAnimation(false);
                    MoveToInternal(
                        resumePath,
                        UnitMovementIntentReason.HealerResume,
                        startNewCommand: false);
                    yield break;
                }

                // (3) 대기 — watchInterval 동안 프레임을 넘기며 대기하되, 대기 중 새 경로가 예약되면
                //     즉시 상단으로 돌아가 (2)에서 이동을 재개하도록 안쪽 루프를 빠져나온다.
                float waited = 0f;
                while (waited < watchInterval && _unitData != null && _unitData.IsAlive)
                {
                    if (_pendingPath != null) break;
                    waited += Time.deltaTime;
                    yield return null;
                }
            }

            // 유닛 사망 등으로 감시가 자연 종료된 경우 — 이동 코루틴 추적을 해제한다.
            //   (이동 재개(2번 경로)로 빠져나갈 때는 위에서 yield break하므로 이 지점에 도달하지 않는다.
            //    따라서 여기서 _moveCoroutine을 비워도 새 이동 코루틴을 잘못 지우지 않는다.)
            if (_moveCoroutine != null) _moveCoroutine = null;
        }

        /// <summary>
        /// 힐러가 힐 시전을 시작할 때 호출 — 대상 방향으로 회전하고 힐 애니메이션을 재생한다.
        ///
        /// 애니메이션은 공격 유닛과 동일한 Attack 상태(BloomFairy의 Attack 클립 = 힐 모션)를 사용한다.
        ///   - 싱글플레이/호스트(서버): Animator를 직접 CrossFade(StartCombatAnimation과 동일 가드).
        ///   - 멀티플레이 클라이언트: 힐 전용 atomic snapshot(회차/서버 시각/active)을 복제해
        ///     spawn와 Initialize 뒤 현재 회차를 복원한다. 일반 Attack scalar는 시작하지 않는다.
        /// </summary>
        /// <param name="targetId">회복 대상 아군 유닛의 Id(회전 방향 계산에 사용).</param>
        private void PlayHealCastAnimation(int targetId)
        {
            // BloomFairy의 힐은 C3 공격 lease와 별도 경로다. 이전 공격 scope가 섞이지 않게 닫고,
            // OnAttackHit의 명시적인 BloomFairy 예외에서 기존 힐 연출만 보존한다.
            CloseAttackPhasePresentation("separate-heal-entry");

            _healFacingTarget = GetTargetTransform(targetId, true);
            _healFacingTargetView = _healFacingTarget != null ? _healFacingTarget.GetComponent<UnitView>() : null;

            // 승인된 점진 서버 회전은 Update의 공통 RotateTowards를 사용한다.
            // 이전 snap은 실기 검증 전 비활성화 보존한다.
            /*
            Vector3 targetPos = _positionProvider != null
                ? _positionProvider.GetUnitWorldPosition(targetId)
                : Vector3.zero;
            bool canWriteSimulationRoot =
                !NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer;
            if (canWriteSimulationRoot
                && _rootRotationOwnership.ActionOwnsRoot
                && targetPos != Vector3.zero)
            {
                float angle = CalculateAttackAngle(targetPos);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            else if (canWriteSimulationRoot
                && !_rootRotationOwnership.ActionOwnsRoot
                && targetPos != Vector3.zero)
            {
                UnitMovementAuthorityObserver.ObserveAttackWriterOwnershipConflict(
                    _unitData != null ? _unitData.Id : -1);
            }
#endif

            */
            float marker = _unitData.HitFrameTimes != null && _unitData.HitFrameTimes.Length > 0
                ? _unitData.HitFrameTimes[0] : 0.2f;
            float clipLength = _unitData.AttackCooldown;
            if (_animator != null && _animator.runtimeAnimatorController != null)
                foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
                    if (clip.name.EndsWith("_Attack", StringComparison.Ordinal)) { clipLength = clip.length; break; }
            var state = new UnitHealCastPresentation
            {
                Lifetime = _healLifetime, Sequence = ++_healSequence,
                Revision = _healPresentation.Current.Revision + 1,
                UnitId = _unitData.Id, TargetId = targetId, StartedAt = HealNow,
                AimX = transform.forward.x, AimZ = transform.forward.z,
                MarkerOffset = marker, ClipDuration = clipLength, Active = true,
                Priority = _isFrozenAnim ? (byte)3 : _isMovementHeldAnim ? (byte)4 : (byte)1
            };
            UnitHealCastDiagnostics.Event("Start", state);
            ObserveHealPose("Start", state);
            ApplyHealPresentation(state, state.StartedAt);
            if (NetworkContext.IsNetworkActive && NetworkContext.IsNetworkServer)
                GameEvents.OnUnitHealCastStarted.OnNext(state);
        }

        /// <summary>실제 힐 Client 적용 경계. 역할 데이터·객체 수명·준비 상태를 확인한다.</summary>
        public bool ApplyHealPresentation(UnitHealCastPresentation state, double now)
        {
            if (_unitData == null || !_unitData.IsHealer || !_unitData.IsAlive
                || state.UnitId != _unitData.Id || _animator == null) return false;
            if (!_healPresentation.Accept(state)) return false;
            ObserveHealPose("Snapshot", state);
            if (!state.Active)
            {
                _healClipHeld = false;
                UnitHealCastDiagnostics.Event("End", state);
                ObserveHealPose("Inactive", state);
                if (state.Priority == 3) FreezeWalkAnimation();
                else if (state.Priority == 4) HoldMovementAnimation();
                else if (state.EndReason == 1) _animator.speed = 0f;
                else ResumeWalkAnimation();
                return true;
            }
            MaintainHealPresentation(now);
            return true;
        }

        private void MaintainHealPresentation(double now)
        {
            var state = _healPresentation.Current;
            if (!state.Active || _unitData == null || !_unitData.IsAlive || _animator == null) return;
            bool newCycle = _healPresentation.PlayedSequence != state.Sequence;
            bool priorityChanged = _appliedHealPriority != state.Priority;
            if (state.Priority == 3) { _animator.speed = 0; _appliedHealPriority = 3; return; }
            if (state.Priority == 4) { HoldMovementAnimation(); _appliedHealPriority = 4; return; }
            if (!_healPresentation.IsDue(now)) return;
            if (newCycle || priorityChanged)
            {
                float offset = _healPresentation.Offset(now);
                _animator.speed = 1;
                _animator.CrossFadeInFixedTime(StateAttack, _healCastBlend, 0, offset);
                _currentAnimStateHash = StateAttack;
                _healPresentation.MarkPlayed(now);
                _appliedHealPriority = state.Priority;
                _healClipHeld = false;
                UnitHealCastDiagnostics.Event("Play", state,
                    $"Now={now:G17}, Offset={offset:F6}, CatchUp={offset > 0}");
                ObserveHealPose("Play", state);
            }
            // 4초 clip의 후반 동작이 회차를 채운다. 일반 endpoint hold는 비활성화 보존한다.
            // 실제 Frozen/Held 정지는 위의 priority 경계에서 유지한다.
            /* if (!_healClipHeld && now - state.StartedAt >= state.ClipDuration)
            {
                // 3초 looping clip을 4초 주기로 계속 돌리면 4초 marker가 중복된다.
                // 한 clip 끝 자세만 보존한다. HP 타이머와 3초 쿨다운은 그대로 진행한다.
                _animator.Play(StateAttack, 0, 0.999999f);
                _animator.speed = 0;
                _healClipHeld = true;
                UnitHealCastDiagnostics.Event("MotionEnd", state);
            } */
            if (!_healClipHeld && now - state.StartedAt >= state.ClipDuration)
            {
                _healClipHeld = true; // 관찰 플래그만 설정한다. Animator pose/speed는 쓰지 않는다.
                UnitHealCastDiagnostics.Event("MotionEnd", state);
                ObserveHealPose("MotionEnd", state);
            }
        }

        /// <summary>모션 수명을 닫는다. 기존 HoT writer와 효과 수명에는 관여하지 않는다.</summary>
        public void EndHealPresentation(string reason, bool terminal = false)
        {
            ++_healLoopOwner;
            // 현재 lifecycle이 닫는 힐러의 pursuit도 여기서 정리한다. 중지된 coroutine에
            // 맡기면 cleanup이 실행되지 않는다. stale coroutine은 CloseOwnedHealLoop에서 차단된다.
            // 일반 공격 유닛의 pursuit 정책에는 손대지 않는다.
            if (_unitData != null && _unitData.IsHealer) _isInCombatPursuit = false;
            if (terminal && ReferenceEquals(_healCooldownCaster, _unitData)
                && _healCooldownCast.Lifetime == _healLifetime) _healCooldownCaster = null;
            ObserveHealPose("End-" + reason, _healPresentation.Current);
            _healFacingTarget = null;
            _healFacingTargetView = null;
            var state = _healPresentation.Current;
            if (state.Active)
            {
                state.Active = false;
                state.Revision++;
                state.EndedAt = HealNow;
                state.EndReason = reason == "cycle-complete" ? (byte)1 : reason == "no-target" ? (byte)2
                    : reason == "movement-replaced" ? (byte)3 : reason == "death" ? (byte)4
                    : reason == "game-end" ? (byte)5 : (byte)6;
                _healPresentation.Accept(state);
                UnitHealCastDiagnostics.Event("End", state, "Reason=" + reason);
                if (NetworkContext.IsNetworkActive && NetworkContext.IsNetworkServer)
                    GameEvents.OnUnitHealCastEnded.OnNext(state);
            }
            if (terminal) _healPresentation.Close();
            if (reason != "cycle-complete" && _unitData != null && _unitData.IsHealer)
                _rootRotationOwnership.ReleaseAction(resumeMovement: !terminal
                    && reason == "no-target" && !_healStationaryWatch && _unitData.IsAlive);
            _healClipHeld = false;
        }

    }
}
