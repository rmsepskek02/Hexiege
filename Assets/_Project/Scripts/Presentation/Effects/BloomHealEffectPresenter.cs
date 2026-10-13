using System;
using System.Collections.Generic;
using Hexiege.Application;
using Hexiege.Domain;
using Hexiege.Core;
using UnityEngine;
using UniRx;

namespace Hexiege.Presentation
{
    /// <summary>서버/싱글/Client 공통 sole emitter. HP와 HoT writer를 변경하지 않는 표현 소유자.</summary>
    public sealed class BloomHealEffectPresenter : MonoBehaviour
    {
        private sealed class Owner
        {
            public UnitData Target;
            public BloomHealEffectPresentation State;
            public Transform Pose;
            public VfxPoolItem Item;
            public ulong Lease;
            public bool PendingReported, AssetReported;
        }
        private readonly Dictionary<int, Owner> _owners = new();
        private readonly List<Owner> _iteration = new();
        private readonly List<BloomHealEffectChannel.Recipient> _baseline = new();
        private BloomHealEffectChannel _channel;
        private EffectManager _manager;
        private Func<UnitData, Transform> _resolvePose;
        private IDisposable _gameEnd, _death;
        private bool _closed = true;

        public void Initialize(BloomHealEffectChannel channel, EffectManager manager, Func<UnitData, Transform> resolvePose)
        {
            _channel?.CloseVisuals("MapReplaced");
            CloseLocal("Reinitialize");
            _channel = channel; _manager = manager; _resolvePose = resolvePose;
            if (channel == null || channel.IsClosed) return;
            _closed = false;
            channel.Changed += OnCurrent; channel.AttachmentClosed += OnAttachmentClosed;
            channel.VisualsClosed += CloseLocal;
            _gameEnd = GameEvents.OnGameEnd.Subscribe(_ => channel.CloseVisuals("GameEnd"));
            _death = GameEvents.OnUnitDied.Subscribe(evt =>
            {
                // 사망한 객체의 표현 참조만 retire. gameplay list 제거 정책은 그대로다.
                channel.CloseAttachment(evt.Unit, "LocalDeath");
            });
            channel.CopyCurrent(_baseline);
            foreach (var entry in _baseline) OnCurrent(entry.Cursor.Current, entry.Target);
            _baseline.Clear();
        }
        private void OnCurrent(BloomHealEffectPresentation state, UnitData target)
        {
            if (_closed || !state.IsValid || target == null) return;
            if (_owners.TryGetValue(state.TargetId, out var owner))
            {
                if (owner.State.SameOwner(state) && ReferenceEquals(owner.Target, target))
                {
                    if (state.Revision <= owner.State.Revision)
                    {
                        _channel.RecordRejection(state, state.Revision < owner.State.Revision ? BloomHealAdmission.Stale
                            : state.Equals(owner.State) ? BloomHealAdmission.Duplicate : BloomHealAdmission.Conflict, "PresenterCurrent");
                        return;
                    }
                    owner.State = state;
                    if (!state.Active)
                    {
                        owner.Item?.StopBloom(owner.Lease, state);
                        Stop(owner, "ServerStop"); _owners.Remove(state.TargetId);
                    }
                    else owner.Item?.RefreshBloom(owner.Lease, state);
                    return;
                }
                Stop(owner, "OwnerReplaced"); _owners.Remove(state.TargetId);
            }
            if (!state.Active) { Record("NotStarted", state, "Reason=StopBeforeReady"); return; }
            _owners[state.TargetId] = new Owner { Target = target, State = state };
            // LateUpdate consumes only the latest state: same-frame grant/stop coalesces before Play.
        }
        private void LateUpdate()
        {
            if (_closed || _owners.Count == 0) return;
            _iteration.Clear(); _iteration.AddRange(_owners.Values);
            foreach (var owner in _iteration)
            {
                try
                {
                    if (!owner.Target.IsAlive)
                    { _channel.CloseAttachment(owner.Target, "LocalDeath"); continue; }
                    if (owner.Item != null)
                    {
                        if (owner.Pose == null || !owner.Pose.gameObject.activeInHierarchy)
                        { _channel.CloseAttachment(owner.Target, "LocalPoseRemoved"); continue; }
                        owner.Item.FollowBloomPose(owner.Lease, owner.Pose);
                        continue;
                    }
                    if (owner.Pose == null) owner.Pose = _resolvePose?.Invoke(owner.Target);
                    if (owner.Pose == null || !owner.Pose.gameObject.activeInHierarchy)
                    {
                        if (!owner.PendingReported) { owner.PendingReported = true; Record("Pending", owner.State, "Reason=TargetPoseUnavailable"); }
                        continue; // Never spawn at origin and never infer server Stop from missing pose.
                    }
                    if (_manager == null || !_manager.BloomPresentationReady)
                    {
                        if (!owner.AssetReported) { owner.AssetReported = true; Record("MissingAsset", owner.State, "Pending=True"); }
                        continue;
                    }
                    owner.Item = _manager.StartBloomEffect(owner.State, owner.Pose
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        , _channel.Diagnostics
#endif
                        );
                    if (owner.Item != null) owner.Lease = owner.Item.BloomLease;
                    else { Record("Fault", owner.State, "Reason=RecursivePSContract"); Stop(owner, "InvalidAsset"); _owners.Remove(owner.State.TargetId); }
                }
                catch (Exception e) { Record("Fault", owner.State, "Reason=PresenterException"); BloomHealEffectChannel.Fault(e, "Presenter"); Stop(owner, "Fault"); _owners.Remove(owner.State.TargetId); }
            }
            _iteration.Clear(); // no old UnitData references survive this frame after retire.
        }
        private void OnAttachmentClosed(UnitData target, string reason)
        {
            if (target != null && _owners.TryGetValue(target.Id, out var owner) && ReferenceEquals(owner.Target, target))
            { Record("AttachmentClose", owner.State, "Reason=" + reason); Stop(owner, reason); _owners.Remove(target.Id); }
        }
        private void Stop(Owner owner, string reason)
        {
            if (owner.Item != null) owner.Item.StopBloom(owner.Lease, reason);
            else Record("NotStarted", owner.State, "Reason=" + reason);
            owner.Item = null; owner.Pose = null; owner.Target = null;
        }
        private void CloseLocal(string reason)
        {
            if (_closed) return;
            _closed = true;
            foreach (var owner in _owners.Values) Stop(owner, reason);
            _owners.Clear(); _iteration.Clear(); _baseline.Clear();
            _channel.Changed -= OnCurrent; _channel.AttachmentClosed -= OnAttachmentClosed; _channel.VisualsClosed -= CloseLocal;
            _gameEnd?.Dispose(); _death?.Dispose(); _gameEnd = _death = null;
            _resolvePose = null;
            // Tail leases keep their numeric diagnostics until drain/return or container TeardownClear.
        }
        private void OnDestroy() { _channel?.CloseVisuals("SceneTeardown"); CloseLocal("SceneTeardown"); }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void Record(string stage, BloomHealEffectPresentation state, string detail)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _channel?.Diagnostics.Record(stage, state, 0, detail);
#endif
        }
    }
}
