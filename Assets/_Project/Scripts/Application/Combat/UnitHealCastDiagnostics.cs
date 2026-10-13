using System.Diagnostics;
using System.Globalization;
using Hexiege.Application.Combat.Sequencing;

namespace Hexiege.Application
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>One peer/run budget shared by writer, transport, presenter and pool. Never a production cap.</summary>
    public sealed class BloomHealEffectDiagnostics
    {
        public const int DetailLimit = 2048, OwnerLimit = 256, EnvelopeLimit = 1000;
        private readonly System.Collections.Generic.HashSet<(ulong, ulong, ulong)> _owners = new();
        private readonly System.Collections.Generic.Dictionary<string, long> _counts = new();
        private long _details, _suppressed, _overflow, _faults;
        private bool _begun, _ended;
        private BloomHealEffectPresentation _last;
        private readonly System.Collections.Generic.HashSet<(ulong, ulong, ulong, ulong)> _leases = new();
        private string _finishReason;
        private bool _teardown;
        private long _untrackedLeases;
        public void Record(string stage, BloomHealEffectPresentation value, ulong lease = 0, string detail = "")
        {
            try
            {
                if (_ended) return;
                if (!value.IsValid && stage != "Drop") return;
                if (value.IsValid) _last = value;
                var leaseKey = (value.MatchGeneration, value.TargetLifetime, value.EffectInstanceId, lease);
                if (stage == "Play" && lease != 0)
                {
                    if (_leases.Count < OwnerLimit) _leases.Add(leaseKey);
                    else { _untrackedLeases++; _overflow++; }
                }
                if (stage == "Return" || stage == "TeardownClear")
                    if (!_leases.Remove(leaseKey) && _untrackedLeases > 0) _untrackedLeases--;
                if (stage == "TeardownClear") _teardown = true;
                if (!_counts.ContainsKey(stage) && _counts.Count >= 32) stage = "Other";
                if (!_begun) { _begun = true; Write("BEGIN", value, 0, "ClockDomain=scaled-writer;DetailLimit=2048;OwnerLimit=256"); }
                _counts.TryGetValue(stage, out var count); _counts[stage] = count + 1;
                var owner = (value.MatchGeneration, value.TargetLifetime, value.EffectInstanceId);
                if (value.IsValid && !_owners.Contains(owner))
                {
                    if (_owners.Count >= OwnerLimit) { _overflow++; _suppressed++; return; }
                    _owners.Add(owner);
                }
                if (_details >= DetailLimit) { _suppressed++; return; }
                _details++; Write(stage, value, lease, detail);
                if (_finishReason != null && PendingTails == 0) Finish(_finishReason);
            }
            catch (System.Exception e) { _faults++; BloomHealEffectChannel.Fault(e, "EffectDiagnostics"); }
            finally { if (_finishReason != null && PendingTails == 0) Finish(_finishReason); }
        }
        public void Finish(string reason)
        {
            if (!_begun || _ended) return;
            _finishReason = reason;
            if (PendingTails != 0) return; // Scaled tail drain is not prematurely certified complete.
            try
            {
                _ended = true;
                var counts = new System.Text.StringBuilder();
                foreach (var pair in _counts) counts.Append(';').Append(pair.Key).Append('=').Append(pair.Value);
                Write("END", _last, 0, $"Reason={reason};Details={_details};Suppressed={_suppressed};Overflow={_overflow};DiagnosticFaults={_faults};PendingTails={PendingTails};Teardown={_teardown};Unknown={_suppressed != 0 || _faults != 0 || _overflow != 0 || _teardown || Count("Fault") != 0 || Count("MissingAsset") != 0 || Count("Conflict") != 0 || Count("Drop") != 0}{counts}");
            }
            catch (System.Exception e) { BloomHealEffectChannel.Fault(e, "EffectDiagnosticsEnd"); }
        }
        public static string Format(string stage, BloomHealEffectPresentation value, ulong lease, string detail)
        {
            string key = $"Peer={(NetworkContext.IsNetworkActive ? (NetworkContext.IsNetworkServer ? "Host" : "Client") : "Single")}, Frame={UnityEngine.Time.frameCount}, Stage={stage}, MatchGeneration={value.MatchGeneration}, TargetLifetime={value.TargetLifetime}, EffectInstanceId={value.EffectInstanceId}, TargetId={value.TargetId}, Revision={value.Revision}, Active={value.Active}, BloomHealKind={value.BloomHealKind}, StopReason={value.StopReason}, PoolLease={lease}";
            string payload = key + ", " + detail.Replace(";", ", ");
            if (System.Text.Encoding.UTF8.GetByteCount(GameLog.Compose(LogLevel.Info, "Combat", nameof(BloomHealEffectDiagnostics), "[BLOOM-EFFECT]", payload)) >= EnvelopeLimit)
                payload = key + ", FormatIncomplete=true, Unknown=true";
            // The exact key itself is bounded scalars. Fallback is also preflighted, never silently truncated.
            if (System.Text.Encoding.UTF8.GetByteCount(GameLog.Compose(LogLevel.Info, "Combat", nameof(BloomHealEffectDiagnostics), "[BLOOM-EFFECT]", payload)) >= EnvelopeLimit)
                throw new System.InvalidOperationException("Bloom diagnostic key exceeds envelope");
            return payload;
        }
        private static void Write(string stage, BloomHealEffectPresentation value, ulong lease, string detail)
        { GameLog.Dev.Info("Combat", nameof(BloomHealEffectDiagnostics), "[BLOOM-EFFECT]", Format(stage, value, lease, detail)); }
        public long PendingTails => _leases.Count + _untrackedLeases;
        public long Count(string stage) => _counts.TryGetValue(stage, out var count) ? count : 0;
        public bool Ended => _ended;
    }
#endif
    /// <summary>회차별 개발 진단. 저장 상태는 각 View/effect의 수명에 붙으므로 전역 누적 사전이 없다.</summary>
    public static class UnitHealCastDiagnostics
    {
        /// <summary>상태 전이만 기존 GameLog/FileSink/RuntimeLogger에 남긴다. 릴리스 인자 평가도 제거한다.</summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Event(string stage, UnitHealCastPresentation state, string detail = "")
        {
            if (!state.IsValid) return;
            string key = $"Peer={(NetworkContext.IsNetworkActive ? (NetworkContext.IsNetworkServer ? "Host" : "Client") : "Single")}, Lifetime={state.Lifetime}, UnitId={state.UnitId}, HealSequence={state.Sequence}, Revision={state.Revision}, TargetId={state.TargetId}, Active={state.Active}, Priority={state.Priority}, EndReason={state.EndReason}, StartedAt={state.StartedAt.ToString("G17", CultureInfo.InvariantCulture)}, EndedAt={state.EndedAt.ToString("G17", CultureInfo.InvariantCulture)}, LocalNow={UnityEngine.Time.timeAsDouble.ToString("G17", CultureInfo.InvariantCulture)}, Frame={UnityEngine.Time.frameCount}";
            // 각 줄에 full key를 유지하고 field 경계에서만 나눈다. Android 잘림 방지.
            string[] fields = detail.Split(',');
            string part = "";
            int ordinal = 1;
            foreach (string field in fields)
            {
                if (part.Length + field.Length > 220 && part.Length > 0)
                {
                    WritePart(stage, key, ordinal++, part);
                    part = "";
                }
                part += (part.Length == 0 ? "" : ",") + field;
            }
            WritePart(stage, key, ordinal, part);
        }

        private static void WritePart(string stage, string key, int ordinal, string detail)
        {
            string message = "[BLOOM-HEAL] " + stage;
            string data = key + $", Part={ordinal}, " + detail;
            // sink envelope 여유 120byte를 남긴다. 실패한 detail을 잘라 정상 증거로 만들지 않는다.
            if (System.Text.Encoding.UTF8.GetByteCount(message + data) > 780)
                data = key + $", Part={ordinal}, FormatIncomplete=true, DetailBytes={System.Text.Encoding.UTF8.GetByteCount(detail)}";
            GameLog.Dev.Info("Combat", nameof(UnitHealCastDiagnostics), message, data);
        }
    }
}
