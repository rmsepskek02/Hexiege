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
        private sealed class Owner { public UnitData Target; public TimedDamageEffectPresentation State; public Transform Pose; public VfxPoolItem Item; public ulong Lease; public bool Pending; }
        private readonly Dictionary<int, Owner> _owners = new();
        private readonly List<Owner> _iteration = new();
        private readonly List<TimedDamageEffectChannel.Entry> _baseline = new();
        private TimedDamageEffectChannel _channel;
        private EffectManager _manager;
        private Func<UnitData, Transform> _pose;
        private IDisposable _end, _death;
        private bool _closed = true;
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
            _owners[state.TargetId] = new Owner { Target = target, State = state };
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
                    owner.Item = _manager?.StartTimedDamageEffect(owner.State, owner.Pose, _channel);
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
