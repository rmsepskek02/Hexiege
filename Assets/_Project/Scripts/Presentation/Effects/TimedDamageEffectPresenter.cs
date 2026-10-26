using System;
using System.Collections.Generic;
using Hexiege.Application;
using Hexiege.Domain;
using UnityEngine;
using UniRx;
namespace Hexiege.Presentation
{
    public sealed class TimedDamageEffectPresenter : MonoBehaviour
    {
        // [2026-10-26 수정안 (a)] ReceivedLocal·AlignHeld 추가 — 화상 VFX 시작을 투사체 화면 착탄까지 미루는 판정용(아래 ShouldHoldStart).
        //   옛 줄(기록용): private sealed class Owner { public UnitData Target; public TimedDamageEffectPresentation State; public Transform Pose; public VfxPoolItem Item; public ulong Lease; public bool Pending; }
        private sealed class Owner { public UnitData Target; public TimedDamageEffectPresentation State; public Transform Pose; public VfxPoolItem Item; public ulong Lease; public bool Pending; public double ReceivedLocal = double.NaN; public bool AlignHeld; public bool AlignCounted; }
        private readonly Dictionary<int, Owner> _owners = new();
        private readonly List<Owner> _iteration = new();
        private readonly List<TimedDamageEffectChannel.Entry> _baseline = new();
        private TimedDamageEffectChannel _channel;
        private EffectManager _manager;
        private Func<UnitData, Transform> _pose;
        private IDisposable _end, _death;
        private bool _closed = true;
        // ────────────────────────────────────────────────────────────────
        // [2026-10-26 수정안 (a)] 화상(InfernoSpirit Burn) VFX 시작을 투사체 화면 착탄에 맞춘다.
        //   서버는 투사체가 닿는 순간 화상을 건다. Client 화면은 투사체를 0.10초 늦게 그리므로, 받자마자 켜면 불이 먼저 붙어 보였다.
        //   그래서 "화면 시각(서버 시각 − 표시 지연) ≥ 화상 시작 시각" 그리고 "같은 공격자의 투사체 피해가 화면 착탄을 기다리는 중이 아님"이
        //   될 때까지 시작만 미룬다(상한 0.3초 — ProjectileAlignedTimedDamageStart). 화상 피해·틱·지속 시간은 그대로다.
        //   조합 루트가 SetProjectileImpactAlignment로 두 함수를 넣지 않으면 예전처럼 바로 시작한다.
        // ────────────────────────────────────────────────────────────────
        private Func<double> _presentationClock;
        private Func<UnitData, int, bool> _heldProjectileHit;

        /// <summary>
        /// [2026-10-26 수정안 (a)] 화상 VFX 시작 보류에 쓸 두 함수를 넣는다(조합 루트 전용).
        /// </summary>
        /// <param name="presentationClock">이 화면의 표시 시각(서버 시계 − Client 표시 지연, 싱글·Host는 지연 0).</param>
        /// <param name="heldProjectileHit">(대상, 공격자 Id) → 그 공격자의 투사체 피해가 대상에게 아직 화면 착탄 대기 중인가.</param>
        public void SetProjectileImpactAlignment(Func<double> presentationClock, Func<UnitData, int, bool> heldProjectileHit)
        {
            _presentationClock = presentationClock;
            _heldProjectileHit = heldProjectileHit;
        }

        // 화상 VFX 시작을 이번 프레임 미뤄야 하는가. 판단은 순수 함수(ProjectileAlignedTimedDamageStart)가 한다.
        private bool ShouldHoldStart(Owner owner)
        {
            if (owner.State.Visual != TimedDamageVisual.Burn || _presentationClock == null
                || !ServerProjectileDeliveryPolicy.IsServerProjectile(UnitType.InfernoSpirit)) return false;
            double now = Time.realtimeSinceStartupAsDouble;
            if (double.IsNaN(owner.ReceivedLocal)) owner.ReceivedLocal = now;
            double presentationNow;
            try { presentationNow = _presentationClock(); } catch { presentationNow = double.NaN; }
            bool held = false;
            try { held = _heldProjectileHit != null && _heldProjectileHit(owner.Target, owner.State.SourceId); } catch { held = false; }
            double waited = now - owner.ReceivedLocal;
            bool wait = ProjectileAlignedTimedDamageStart.ShouldWait(owner.State.Visual, owner.State.StartTime, presentationNow, held, waited);
            if (wait)
            {
                if (!owner.AlignHeld) { owner.AlignHeld = true; ProjectileAlignedTimedDamageDiagnostics.RecordHeld(); }
                return true;
            }
            // [진단] 한 화상에 1번만 센다(프리셋이 없어 시작이 매 프레임 재시도되는 경우에도 중복으로 세지 않게).
            if (!owner.AlignCounted)
            {
                owner.AlignCounted = true;
                if (owner.AlignHeld) ProjectileAlignedTimedDamageDiagnostics.RecordReleased(waited >= ProjectileAlignedTimedDamageStart.MaxHoldSeconds);
                else ProjectileAlignedTimedDamageDiagnostics.RecordImmediate();
            }
            return false;
        }
        public void Initialize(TimedDamageEffectChannel channel, EffectManager manager, Func<UnitData, Transform> pose)
        {
            Close("Reinitialize"); _channel = channel; _manager = manager; _pose = pose;
            if (channel == null || channel.Closed) return;
            _closed = false; channel.Changed += Current; channel.AttachmentClosed += Attachment; channel.VisualsClosed += Close;
            _end = GameEvents.OnGameEnd.Subscribe(_ => channel.CloseVisuals("GameEnd"));
            _death = GameEvents.OnUnitDied.Subscribe(e => channel.CloseAttachment(e.Unit, "LocalDeath"));
            channel.CopyCurrent(_baseline);
            foreach (var entry in _baseline) Current(entry.State, entry.Target);
            _baseline.Clear();
        }
        private void Current(TimedDamageEffectPresentation state, UnitData target)
        {
            if (_closed || !state.IsValid || target == null) return;
            if (_owners.TryGetValue(state.TargetId, out var owner))
            {
                if (owner.State.SameOwner(state) && state.Revision <= owner.State.Revision) return;
                if (owner.State.SameOwner(state) && ReferenceEquals(owner.Target, target) && owner.State.Visual == state.Visual && state.Active)
                { owner.State = state; owner.Item?.RefreshTimedDamage(owner.Lease, state); return; }
                if (!state.Active && owner.State.SameOwner(state)) owner.State = state;
                Stop(owner, state.Active ? "Overwrite" : "ServerStop"); _owners.Remove(state.TargetId);
            }
            if (!state.Active || state.Visual == TimedDamageVisual.None)
            { _channel.Record("NotStarted", state, state.Active ? "UnspecifiedProvenance" : "StopBeforeReady"); return; }
            // [수정안 (a)] 받은 실제 시각을 기록한다(화상 VFX 시작 보류의 상한 0.3초 기준).
            _owners[state.TargetId] = new Owner { Target = target, State = state, ReceivedLocal = Time.realtimeSinceStartupAsDouble };
        }
        private void LateUpdate()
        {
            if (_closed) return;
            _iteration.Clear(); _iteration.AddRange(_owners.Values);
            foreach (var owner in _iteration)
            {
                try
                {
                    if (!owner.Target.IsAlive) { _channel.CloseAttachment(owner.Target, "LocalDeath"); continue; }
                    if (owner.Pose == null) owner.Pose = _pose?.Invoke(owner.Target);
                    if (owner.Pose == null || !owner.Pose.gameObject.activeInHierarchy)
                    {
                        if (owner.Item != null) { owner.Item.StopTimedDamage(owner.Lease, "PoseUnavailable"); owner.Item = null; }
                        owner.Pose = null;
                        if (!owner.Pending) { _channel.Record("Pending", owner.State, "PoseUnavailable"); owner.Pending = true; }
                        continue;
                    }
                    if (owner.Item != null) { owner.Item.FollowTimedDamage(owner.Lease, owner.Pose); continue; }
                    // [2026-10-26 수정안 (a)] 화상은 투사체가 화면에서 닿을 때까지(상한 0.3초) 시작을 미룬다. 독·이미 켠 이펙트는 해당 없음.
                    if (ShouldHoldStart(owner)) continue;
                    // [2026-10-13 C] 맞은 유닛 타입을 넘겨 화상·독 이펙트를 그 유닛 크기에 맞춘다(표에 없으면 배율 1).
                    owner.Item = _manager?.StartTimedDamageEffect(owner.State, owner.Pose, _channel, owner.Target.Type);
                    if (owner.Item != null) { owner.Lease = owner.Item.DamageLease; owner.Pending = false; }
                    else if (!owner.Pending) { _channel.Record("Skip", owner.State, "MissingPreset; Pending=True"); owner.Pending = true; }
                }
                catch (Exception ex) { _channel.Record("Fault", owner.State, ex.GetType().Name + ": " + ex.Message); Stop(owner, "PlaybackFault"); _owners.Remove(owner.State.TargetId); }
            }
            _iteration.Clear();
        }
        private void Stop(Owner owner, string reason)
        { if (owner.Item != null) { owner.Item.RefreshTimedDamage(owner.Lease, owner.State); owner.Item.StopTimedDamage(owner.Lease, reason); } else _channel.Record("NotStarted", owner.State, reason); owner.Item = null; owner.Target = null; owner.Pose = null; }
        private void Attachment(UnitData target, string reason)
        { if (target != null && _owners.TryGetValue(target.Id, out var owner) && ReferenceEquals(owner.Target, target)) { Stop(owner, reason); _owners.Remove(target.Id); } }
        private void Close(string reason)
        {
            if (_closed) return; _closed = true;
            foreach (var owner in _owners.Values) Stop(owner, reason);
            _owners.Clear(); _iteration.Clear(); _baseline.Clear();
            _channel.Changed -= Current; _channel.AttachmentClosed -= Attachment; _channel.VisualsClosed -= Close;
            _end?.Dispose(); _death?.Dispose(); _end = _death = null; _pose = null;
        }
        private void OnDestroy() { _channel?.CloseVisuals("SceneTeardown"); Close("SceneTeardown"); }
    }
}
