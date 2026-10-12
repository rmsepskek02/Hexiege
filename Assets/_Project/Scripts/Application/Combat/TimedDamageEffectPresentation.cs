using System;
using System.Collections.Generic;
using Hexiege.Domain;
using Hexiege.Core;

namespace Hexiege.Application
{
    public enum TimedDamageVisual : byte { None, Poison, Burn }
    public struct TimedDamageEffectPresentation : IEquatable<TimedDamageEffectPresentation>
    {
        public ulong Match, Lifetime, Instance, Revision;
        public ulong AttackMatch, AttackLifetime, AttackSequence;
        public int TargetId, SourceId;
        public TimedDamageVisual Visual;
        public bool Active;
        public double StartTime, EndTime;
        public bool IsValid => Match != 0 && Lifetime != 0 && Instance != 0 && Revision != 0 && TargetId >= 0
            && (Visual == TimedDamageVisual.None || Visual == TimedDamageVisual.Poison || Visual == TimedDamageVisual.Burn)
            && !double.IsNaN(StartTime) && !double.IsInfinity(StartTime) && !double.IsNaN(EndTime) && !double.IsInfinity(EndTime) && EndTime >= StartTime;
        public bool SameOwner(TimedDamageEffectPresentation b) => Match == b.Match && Lifetime == b.Lifetime && Instance == b.Instance && TargetId == b.TargetId;
        public bool Equals(TimedDamageEffectPresentation b) => SameOwner(b) && Revision == b.Revision && Visual == b.Visual && Active == b.Active && SourceId == b.SourceId && StartTime == b.StartTime && EndTime == b.EndTime
            && AttackMatch == b.AttackMatch && AttackLifetime == b.AttackLifetime && AttackSequence == b.AttackSequence;
    }
    // A bounded, reference-bound current state; the actual Damage record is the only lifetime writer.
    public sealed class TimedDamageEffectChannel
    {
        public sealed class Entry { public UnitData Target; public TimedDamageEffectPresentation State; }
        private readonly Dictionary<int, Entry> _current = new();
        private readonly Dictionary<int, (ulong Match, ulong Lifetime)> _retired = new();
        private readonly ulong _match = NewId();
        private ulong _lifetime, _instance;
        private int _logs;
        private int _overflow, _faults;
        public Func<double> Clock = () => UnityEngine.Time.timeAsDouble;
        public bool Closed { get; private set; }
        public event Action<TimedDamageEffectPresentation, UnitData> Changed;
        public event Action<UnitData, string> AttachmentClosed;
        public event Action<string> VisualsClosed;
        private static ulong NewId() { var n = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0); return n == 0 ? 1UL : n; }
        public void CopyCurrent(List<Entry> list) { list.Clear(); list.AddRange(_current.Values); }
        public bool TryGet(UnitData target, out TimedDamageEffectPresentation state)
        { state = default; if (target == null || !_current.TryGetValue(target.Id, out var e) || !ReferenceEquals(e.Target, target)) return false; state = e.State; return state.IsValid; }
        public TimedDamageEffectPresentation Upsert(UnitData target, int sourceId, TimedDamageVisual visual, float duration, AttackEffectPresentation origin = default)
        {
            if (Closed || target == null) return default;
            if (!_current.TryGetValue(target.Id, out var e) || !ReferenceEquals(e.Target, target))
            {
                if (_current.Count >= 1024) { Record("Overflow", default, "CurrentCapacity"); return default; }
                if (e != null) CloseAttachment(e.Target, "LifetimeReplaced");
                e = new Entry { Target = target, State = new TimedDamageEffectPresentation { Match = _match, Lifetime = ++_lifetime, TargetId = target.Id } };
                _current[target.Id] = e;
            }
            var s = e.State;
            bool refresh = s.IsValid && s.Active;
            if (!refresh) s.Instance = ++_instance;
            var old = s.Visual;
            s.Revision++; s.SourceId = sourceId; s.Visual = visual; s.Active = true;
            s.AttackMatch = origin.Match; s.AttackLifetime = origin.Lifetime; s.AttackSequence = origin.Sequence;
            s.StartTime = Clock(); s.EndTime = s.StartTime + duration;
            e.State = s;
            Record(refresh ? old == visual ? "Refresh" : "Overwrite" : "Grant", s, "Writer=Damage");
            Notify(s, target); return s;
        }
        public void Stop(ref TimedDamageEffectPresentation state, UnitData target, string reason)
        {
            // 오래된 record의 cleanup이 현재 동종 Damage owner를 종료하지 못하게 한다.
            if (Closed || !state.IsValid || !state.Active || !TryGet(target, out var current)
                || !current.Equals(state)) { Record("Drop", state, "StaleStopOrClosed"); return; }
            state.Active = false; state.Revision++;
            _current[target.Id].State = state;
            Record("Stop", state, reason); Notify(state, target);
        }
        public bool Receive(TimedDamageEffectPresentation s, UnitData target)
        {
            if (Closed || !s.IsValid || target == null || target.Id != s.TargetId) { Record("Drop", s, "BindingInvalid"); return false; }
            if (_retired.TryGetValue(s.TargetId, out var r) && r.Match == s.Match && s.Lifetime <= r.Lifetime) { Record("Drop", s, "RetiredLifetime"); return false; }
            if (_current.TryGetValue(s.TargetId, out var e))
            {
                if (!ReferenceEquals(e.Target, target) || e.State.Match != s.Match || e.State.Lifetime != s.Lifetime) { Record("Drop", s, "OwnerMismatch"); return false; }
                var old = e.State;
                if (s.Revision <= old.Revision || s.Instance < old.Instance || (s.Instance == old.Instance && !old.Active && s.Active))
                { Record("Drop", s, s.Equals(old) ? "Duplicate" : "StaleOrConflict"); return false; }
            }
            else
            {
                if (_current.Count >= 1024) { Record("Overflow", s, "CurrentCapacity"); return false; }
                e = new Entry { Target = target }; _current[s.TargetId] = e;
            }
            e.State = s; Record("Receive", s, "CurrentValue"); Notify(s, target); return true;
        }
        private void Notify(TimedDamageEffectPresentation s, UnitData target)
        {
            if (Changed == null) return;
            foreach (Action<TimedDamageEffectPresentation, UnitData> handler in Changed.GetInvocationList())
                try { handler(s, target); } catch (Exception ex) { Record("Fault", s, ex.ToString()); }
        }
        public void CloseAttachment(UnitData target, string reason)
        {
            if (target == null || !_current.TryGetValue(target.Id, out var e) || !ReferenceEquals(e.Target, target)) return;
            if (_retired.Count < 4096) _retired[target.Id] = (e.State.Match, e.State.Lifetime);
            else { Record("Overflow", e.State, "TombstoneCapacity"); CloseVisuals("TombstoneOverflow"); return; }
            _current.Remove(target.Id); Record("AttachmentClose", e.State, reason);
            if (AttachmentClosed != null) foreach (Action<UnitData, string> handler in AttachmentClosed.GetInvocationList())
                try { handler(target, reason); } catch (Exception ex) { Record("Fault", e.State, ex.Message); }
        }
        public void CloseVisuals(string reason)
        {
            if (Closed) return; Closed = true;
            if (VisualsClosed != null) foreach (Action<string> handler in VisualsClosed.GetInvocationList())
                try { handler(reason); } catch (Exception ex) { Record("Fault", default, ex.Message); }
            _current.Clear();
            string role = !NetworkContext.IsNetworkActive ? "Solo" : NetworkContext.IsNetworkServer ? "Server" : "Client";
            GameLog.Dev.Info("Combat", nameof(TimedDamageEffectChannel), $"Stage=END, Match={_match}, Role={role}, Reason={reason}, DetailOverflow={_overflow}, Faults={_faults}, Completeness={(_overflow == 0 && _faults == 0 ? "Bounded" : "Unknown")}, NaturalDrain=Unverified");
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void Record(string stage, TimedDamageEffectPresentation s, string reason)
        {
            if (stage == "Fault" || stage == "Overflow") _faults++;
            if (_logs++ >= 2048) { _overflow++; if (_overflow == 1) GameLog.Dev.Warn("Combat", nameof(TimedDamageEffectChannel), "DetailLimit=2048; Completeness=Unknown"); return; }
            string role = !NetworkContext.IsNetworkActive ? "Solo" : NetworkContext.IsNetworkServer ? "Server" : "Client";
            reason = (reason ?? "None").Replace('\r', ' ').Replace('\n', ' ').Replace(", ", ";");
            if (reason.Length > 160) reason = reason.Substring(0, 160) + "[truncated]";
            double now = double.NaN;
            try { now = Clock(); } catch { /* 진단 시계 실패가 실제 Damage writer로 전파되지 않게 한다. */ }
            string line = System.FormattableString.Invariant($"Stage={stage}, Match={s.Match}, Lifetime={s.Lifetime}, Effect={s.Instance}, Revision={s.Revision}, Target={s.TargetId}, Source={s.SourceId}, Visual={s.Visual}, Active={s.Active}, Start={s.StartTime:G17}, End={s.EndTime:G17}, Now={now:G17}, Role={role}, Authority={(!NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer)}, Reason={reason}");
            line += $", AttackKey={s.AttackMatch}:{s.AttackLifetime}:{s.AttackSequence}, Writer=Damage";
            if (stage == "Fault" || stage == "Overflow") GameLog.Dev.Warn("Combat", nameof(TimedDamageEffectChannel), line + "; GameplayContinues=True; Recovery=RefreshOrReinitialize; Completeness=Unknown");
            else if (stage == "Drop") GameLog.Dev.Warn("Network", nameof(TimedDamageEffectChannel), line);
            else GameLog.Dev.Info(stage == "Publish" || stage == "Receive" ? "Network" : "Combat", nameof(TimedDamageEffectChannel), line);
        }
    }
}
