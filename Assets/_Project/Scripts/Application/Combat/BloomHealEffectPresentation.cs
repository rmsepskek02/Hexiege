using System;
using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public enum BloomHealStopReason : byte { None, Expired, FullHP, TargetUnavailable, TargetDead, NonBloomOverwrite, TargetLifetimeChanged }
    public enum BloomHealAdmission : byte { Accepted, Duplicate, Stale, Conflict, Invalid, OwnerMismatch, ClosedInstance, BindingRejected, ChannelClosed }

    /// <summary>Recipient HoT identity. No clock is a presentation lifetime authority.</summary>
    public struct BloomHealEffectPresentation : IEquatable<BloomHealEffectPresentation>
    {
        public ulong MatchGeneration, TargetLifetime, EffectInstanceId, Revision;
        public int TargetId;
        public byte BloomHealKind;
        public bool Active;
        public BloomHealStopReason StopReason;
        public bool IsValid => MatchGeneration != 0 && TargetLifetime != 0 && EffectInstanceId != 0
            && Revision != 0 && TargetId >= 0 && BloomHealKind == 1;
        public bool SameOwner(BloomHealEffectPresentation other) => MatchGeneration == other.MatchGeneration
            && TargetLifetime == other.TargetLifetime && EffectInstanceId == other.EffectInstanceId && TargetId == other.TargetId;
        public bool Equals(BloomHealEffectPresentation other) => SameOwner(other) && Revision == other.Revision
            && Active == other.Active && BloomHealKind == other.BloomHealKind && StopReason == other.StopReason;
    }

    /// <summary>A spawn-bound cursor. Closed instances cannot reactivate, even at a higher revision.</summary>
    public sealed class BloomHealEffectCursor
    {
        public BloomHealEffectPresentation Current { get; private set; }
        public bool TryAccept(BloomHealEffectPresentation value)
        {
            if (Classify(value) != BloomHealAdmission.Accepted) return false;
            Current = value;
            return true;
        }
        // 분류는 상태를 바꾸지 않는다. 동일 revision의 같은 payload는 Duplicate,
        // 다른 payload는 Conflict다. 둘 모두 거부하며 기존 gameplay gate는 완화하지 않는다.
        public BloomHealAdmission Classify(BloomHealEffectPresentation value)
        {
            if (!value.IsValid) return BloomHealAdmission.Invalid;
            if (Current.IsValid)
            {
                if (value.MatchGeneration != Current.MatchGeneration || value.TargetLifetime != Current.TargetLifetime
                    || value.TargetId != Current.TargetId) return BloomHealAdmission.OwnerMismatch;
                if (value.Revision == Current.Revision)
                    return value.Equals(Current) ? BloomHealAdmission.Duplicate : BloomHealAdmission.Conflict;
                if (value.Revision < Current.Revision || value.EffectInstanceId < Current.EffectInstanceId) return BloomHealAdmission.Stale;
                if (value.EffectInstanceId == Current.EffectInstanceId && !Current.Active && value.Active) return BloomHealAdmission.ClosedInstance;
            }
            return BloomHealAdmission.Accepted;
        }
    }

    /// <summary>
    /// Presentation-only current-state store. UnitData reference is the local lifetime binding;
    /// neither IDs reused by the factory nor Dev diagnostic quotas can replace this binding.
    /// The HoT writer owns Grant/Refresh/Stop; transport only accepts current values on clients.
    /// </summary>
    public sealed class BloomHealEffectChannel
    {
        public sealed class Recipient
        {
            public UnitData Target;
            public readonly BloomHealEffectCursor Cursor = new BloomHealEffectCursor();
            public ulong ExpectedMatch, ExpectedLifetime;
        }
        private readonly Dictionary<int, Recipient> _current = new Dictionary<int, Recipient>();
        // Scalar tombstones keep retired lifetimes from binding to a new object; no dead UnitData retention.
        private readonly Dictionary<int, (ulong Match, ulong Lifetime)> _retired = new Dictionary<int, (ulong, ulong)>();
        private ulong _nextLifetime, _nextInstance;
        public bool IsClosed { get; private set; }
        public readonly ulong MatchGeneration = NewGeneration();
        public event Action<BloomHealEffectPresentation, UnitData> Changed;
        public event Action<UnitData, string> AttachmentClosed;
        public event Action<string> VisualsClosed;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public readonly BloomHealEffectDiagnostics Diagnostics = new BloomHealEffectDiagnostics();
#endif
        private static ulong NewGeneration()
        { ulong value = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0); return value == 0 ? 1UL : value; }
        public bool TryGet(UnitData target, out BloomHealEffectPresentation value)
        {
            value = default;
            if (target == null || !_current.TryGetValue(target.Id, out var entry) || !ReferenceEquals(entry.Target, target)) return false;
            value = entry.Cursor.Current; return value.IsValid;
        }
        public void CopyCurrent(List<Recipient> destination)
        { destination.Clear(); destination.AddRange(_current.Values); }
        public bool BindRecipient(UnitData target, ulong match, ulong lifetime)
        {
            if (IsClosed || target == null || match == 0 || lifetime == 0) return false;
            if (_retired.TryGetValue(target.Id, out var closed) && closed.Match == match && lifetime <= closed.Lifetime) return false;
            if (_current.TryGetValue(target.Id, out var entry))
            {
                if (ReferenceEquals(entry.Target, target)) return entry.ExpectedMatch == match && entry.ExpectedLifetime == lifetime;
                if (entry.ExpectedMatch == match && lifetime <= entry.ExpectedLifetime) return false;
                CloseAttachment(entry.Target, "RecipientReplaced");
            }
            _current[target.Id] = new Recipient { Target = target, ExpectedMatch = match, ExpectedLifetime = lifetime };
            return true;
        }

        public BloomHealEffectPresentation Grant(UnitData target)
        {
            if (!_current.TryGetValue(target.Id, out var entry) || !ReferenceEquals(entry.Target, target))
            {
                if (!BindRecipient(target, MatchGeneration, ++_nextLifetime)) return default;
                entry = _current[target.Id];
            }
            var prior = entry.Cursor.Current;
            var value = new BloomHealEffectPresentation {
                MatchGeneration = MatchGeneration, TargetLifetime = entry.ExpectedLifetime,
                EffectInstanceId = ++_nextInstance, Revision = prior.IsValid ? prior.Revision + 1 : 1,
                TargetId = target.Id, BloomHealKind = 1, Active = true };
            Record("Grant", value);
            Accept(value, target);
            return value;
        }
        public void Refresh(ref BloomHealEffectPresentation value, UnitData target)
        { if (!value.Active) return; value.Revision++; Record("Refresh", value); Accept(value, target); }
        public void Stop(ref BloomHealEffectPresentation value, UnitData target, BloomHealStopReason reason)
        {
            if (!value.IsValid || !value.Active) return;
            value.Active = false; value.Revision++; value.StopReason = reason;
            Record("Stop", value);
            if (!Accept(value, target)) Notify(value, target); // Actual writer removal may follow local despawn.
        }
        public bool Accept(BloomHealEffectPresentation value, UnitData target)
        {
            if (IsClosed) { RecordRejection(value, BloomHealAdmission.ChannelClosed, "ChannelAccept"); return false; }
            if (target == null || target.Id != value.TargetId || !value.IsValid)
            { RecordRejection(value, BloomHealAdmission.Invalid, "ChannelAccept"); return false; }
            if (!_current.TryGetValue(target.Id, out var entry) || !ReferenceEquals(entry.Target, target)
                || entry.ExpectedMatch != value.MatchGeneration || entry.ExpectedLifetime != value.TargetLifetime)
            { RecordRejection(value, BloomHealAdmission.BindingRejected, "ChannelAccept"); return false; }
            var admission = entry.Cursor.Classify(value);
            if (admission != BloomHealAdmission.Accepted)
            { RecordRejection(value, admission, "ChannelAccept"); return false; }
            entry.Cursor.TryAccept(value);
            Notify(value, target);
            return true;
        }
        private void Notify(BloomHealEffectPresentation value, UnitData target)
        {
            // Each observer gets its own exception boundary: one broken visual must not prevent transport
            // or, more importantly, interrupt the existing authoritative HoT loop.
            var handlers = Changed;
            if (handlers != null)
                foreach (Action<BloomHealEffectPresentation, UnitData> handler in handlers.GetInvocationList())
                    try { handler(value, target); } catch (Exception e) { Fault(e, "Changed"); }
        }
        public void CloseAttachment(UnitData target, string reason)
        {
            if (target == null) return;
            if (_current.TryGetValue(target.Id, out var entry) && ReferenceEquals(entry.Target, target))
            {
                _retired[target.Id] = (entry.ExpectedMatch, entry.ExpectedLifetime);
                _current.Remove(target.Id);
            }
            var handlers = AttachmentClosed;
            if (handlers != null)
                foreach (Action<UnitData, string> handler in handlers.GetInvocationList())
                    try { handler(target, reason); } catch (Exception e) { Fault(e, "AttachmentClosed"); }
        }
        public void CloseVisuals(string reason)
        {
            if (IsClosed) return;
            IsClosed = true;
            var handlers = VisualsClosed;
            if (handlers != null)
                foreach (Action<string> handler in handlers.GetInvocationList())
                    try { handler(reason); } catch (Exception e) { Fault(e, "VisualsClosed"); }
            _current.Clear(); _retired.Clear(); // Match teardown only, not HP/HoT mutation.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Diagnostics.Finish(reason);
#endif
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void RecordRejection(BloomHealEffectPresentation value, BloomHealAdmission admission, string boundary)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string stage = admission == BloomHealAdmission.Duplicate ? "Duplicate"
                : admission == BloomHealAdmission.Stale ? "Stale" : admission == BloomHealAdmission.Conflict ? "Conflict" : "Drop";
            Diagnostics.Record(stage, value, 0, "Reason=" + admission + ", Boundary=" + boundary);
#endif
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void Record(string stage, BloomHealEffectPresentation value)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Diagnostics.Record(stage, value);
#endif
        }
        public static void Fault(Exception e, string boundary)
        { GameLog.Ops.Warn(LogEvent.UnhandledException, "Combat", nameof(BloomHealEffectChannel),
            "Bloom presentation fault isolated; authoritative HoT unchanged", e, "Boundary=" + boundary); }
    }
}
