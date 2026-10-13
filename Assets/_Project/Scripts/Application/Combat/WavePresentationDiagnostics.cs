using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Hexiege.Application
{
    /// <summary>기존 GameLog를 쓰는 개발 전용 수동 관측. 오류가 gameplay로 전파되지 않는다.</summary>
    public sealed class WavePresentationDiagnostics
    {
        public const string Prefix = "[WAVE-DIAG]";
        public const string LogClass = "WavePresentationDiagnostics";
        public const int ByteLimit = 900;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly string _scope;
        private ulong _epoch;
        private string _role;
        private bool _begun;
        private long _sequence, _faults, _overflow;
        private bool _closed;
        private readonly Dictionary<string, long> _counts = new();
        private readonly Dictionary<(WaveDeliveryKey, long), Dictionary<string, long>> _waves = new();
        private readonly Dictionary<(WaveDeliveryKey, long), Dictionary<long, int>> _targets = new();
        private int _targetCount;
#endif
        /// <summary>관측 스코프를 만든다. 릴리스에는 진단 상태를 저장하지 않는다.</summary>
        public WavePresentationDiagnostics(string scope)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _scope = scope;
#endif
        }

        /// <summary>발행 계약을 바꾸지 않고 새 관측 수명만 시작한다.</summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void Begin(ulong epoch, double clock)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try { Finish("begin-replace", clock); Clear(); _epoch = epoch; _closed = false; _begun = true; CaptureRole(); }
            catch { _faults++; }
#endif
        }

        /// <summary>모든 causal event를 즉시 출력한다. 집계 용량은 사건 출력의 cap이 아니다.</summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void Record(string stage, WaveDeliveryKey key, double clock, int ordinal = 0,
            int target = -1, string kind = "none", FormattableString detail = null, bool warning = false, long localWave = 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                EnsureEpoch(key, clock);
                if (_closed) return;
                CountEvent(stage, key, localWave);
                Write(stage, key, clock, ordinal, target, kind, detail?.ToString(CultureInfo.InvariantCulture), warning, localWave);
            }
            catch { _faults++; }
#endif
        }

        /// <summary>동일 제외 사유의 hot loop에서는 문자열·배열·boxing을 만들지 않는다.</summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void Transition(WaveDeliveryKey key, long localWave, double clock, int target, bool unit, int reason,
            string reasonName, bool alive, int team, int hp, bool hasPosition, Vector3 position,
            float p, float lateral, float front, float halfWidth, float length)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                EnsureEpoch(key, clock); // 상태 삽입 이전에 이전 epoch를 마감한다.
                if (_closed) return;
                var stateKey = (key, localWave);
                if (!_targets.TryGetValue(stateKey, out var states))
                {
                    if (_targets.Count >= 512) { Overflow(key, localWave, clock, "target-waves"); return; }
                    states = new(); _targets.Add(stateKey, states);
                }
                long identity = ((long)target << 1) | (unit ? 1L : 0L);
                if (states.TryGetValue(identity, out int previous)) { if (previous == reason) return; }
                else if (states.Count >= 1024 || _targetCount >= 8192) { Overflow(key, localWave, clock, "target-states"); return; }
                else _targetCount++;
                states[identity] = reason;
                Record("candidate-transition", key, clock, 0, target, unit ? "unit" : "building",
                    $"reason={reasonName};alive={alive};team={team};hp={hp};hasXZ={hasPosition};x={position.x:F3};y={position.y:F3};z={position.z:F3};p={p:F3};lateral={lateral:F3};front={front:F3};halfWidth={halfWidth:F3};length={length:F3}",
                    localWave: localWave);
            }
            catch { _faults++; }
#endif
        }

        /// <summary>wave 완료 시 관측 상태만 해제한다. delivery/hit-set은 건드리지 않는다.</summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void Retire(WaveDeliveryKey key, double clock, FormattableString detail = null, long localWave = 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                Record("retire", key, clock, detail: detail, localWave: localWave);
                var stateKey = (key, localWave);
                if (_waves.TryGetValue(stateKey, out var counts)) { Chunks("wave-summary", key, clock, counts, localWave); _waves.Remove(stateKey); }
                if (_targets.TryGetValue(stateKey, out var targets)) { _targetCount -= targets.Count; _targets.Remove(stateKey); }
            }
            catch { _faults++; }
#endif
        }

        /// <summary>호출 경계의 관측 실패도 coverage에 반영하며 예외를 다시 던지지 않는다.</summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void ObserveFault(string boundary)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try { _faults++; Increment(_counts, "observer-fault-" + boundary); } catch { }
#endif
        }

        /// <summary>추가 사유별 집계. 임의 대상 이력을 저장하지 않는다.</summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void Count(string metric, WaveDeliveryKey key, long localWave = 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try { if (!_closed) CountEvent(metric, key, localWave); } catch { _faults++; }
#endif
        }

        /// <summary>GameEnd는 snapshot, final만 pending 기록 후 관측 상태를 초기화한다.</summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void Finish(string reason, double clock, FormattableString detail = null, bool final = true)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                if (_closed || (_sequence == 0 && _counts.Count == 0)) return;
                var key = new WaveDeliveryKey(_epoch, 0);
                Write(final ? "final" : "snapshot", key, clock, 0, -1, "none",
                    FormattableString.Invariant($"reason={reason};activeWaveCounters={_waves.Count};targetStates={_targetCount};faults={_faults};overflowProbes={_overflow};coverageComplete={_faults == 0 && _overflow == 0};{detail?.ToString(CultureInfo.InvariantCulture)}"), _faults != 0 || _overflow != 0, 0);
                Chunks(final ? "terminal-counts" : "snapshot-counts", key, clock, _counts, 0);
                if (final) { Clear(); _closed = true; }
            }
            catch { _faults++; }
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void EnsureEpoch(WaveDeliveryKey key, double clock)
        {
            // 명시 Begin/Reset이 수명을 소유한다. 거부된 외부 epoch는 현재 집계를 초기화하지 않는다.
            if (_begun) return;
            _epoch = key.Epoch; _begun = true; _closed = false; CaptureRole();
        }
        private void CaptureRole()
        { _role = NetworkContext.IsNetworkActive ? NetworkContext.IsNetworkServer ? "server" : "client" : "single"; }
        private void Clear()
        { _counts.Clear(); _waves.Clear(); _targets.Clear(); _targetCount = 0; _sequence = _faults = _overflow = 0; }
        private void CountEvent(string stage, WaveDeliveryKey key, long localWave)
        {
            Increment(_counts, stage);
            // transport/presenter/pool은 scope 집계만 유지한다. 살아 있는 server/coordinator만 wave 집계를 보유한다.
            if (_scope != "server" && _scope != "coordinator") return;
            var stateKey = (key, localWave);
            if (!_waves.TryGetValue(stateKey, out var counts))
            {
                if (_waves.Count < 512) { counts = new(); _waves.Add(stateKey, counts); }
                else { Overflow(key, localWave, 0, "wave-counters"); return; }
            }
            Increment(counts, stage);
        }
        private void Overflow(WaveDeliveryKey key, long localWave, double clock, string area)
        {
            if (++_overflow == 1)
                Write("observer-overflow", key, clock, 0, -1, "none",
                    $"area={area};waveLimit=512;perWaveTargets=1024;totalTargets=8192;causalEventsUnaffected=true", true, localWave);
        }
        private static void Increment(Dictionary<string, long> counts, string metric)
        { counts.TryGetValue(metric, out long count); counts[metric] = count + 1; }
        private void Chunks(string stage, WaveDeliveryKey key, double clock, Dictionary<string, long> counts, long localWave)
        {
            int chunk = 0;
            foreach (var count in counts)
                Write(stage, key, clock, 0, -1, "none",
                    $"chunk={++chunk};chunks={counts.Count};counter={count.Key};count={count.Value}", false, localWave);
        }
        private void Write(string stage, WaveDeliveryKey key, double clock, int ordinal, int target, string kind,
            string detail, bool warning, long localWave)
        {
            string head = FormattableString.Invariant($"scope={_scope}, role={_role}, epoch={_epoch:x16}, deliveryEpoch={key.Epoch:x16}, wave={key.Id}, validDelivery={key.IsValid}, localWave={localWave}, event={++_sequence}, stage={stage}, clockDomain={(_role == "single" ? "simulation-local" : "synced-server")}, now={clock:G17}, impact={(ordinal > 0 ? ordinal.ToString(CultureInfo.InvariantCulture) : "none")}, target={target}, kind={kind}");
            var level = warning ? LogLevel.Warn : LogLevel.Info;
            var pieces = new List<string>();
            // 기존 parser의 필드 구분은 ", ". 값 안에 raw 구분자는 두지 않는다.
            foreach (string field in (detail ?? "").Split(';'))
            {
                if (field.Length == 0) continue;
                string probe = head + ", part=2147483647, parts=2147483647, " + field;
                if (!field.Contains(", ") && Encoding.UTF8.GetByteCount(GameLog.Compose(level, "Combat", LogClass, Prefix, probe)) <= ByteLimit)
                { pieces.Add(field); continue; }
                // 한 필드가 너무 크거나 구분자를 포함하면 UTF8→Base64로 보존한다. 조용히 drop하지 않는다.
                string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(field));
                int parts = (encoded.Length + 127) / 128;
                for (int i = 0; i < parts; i++)
                    pieces.Add($"encoding=utf8-base64, fieldPart={i + 1}, fieldParts={parts}, fragment={encoded.Substring(i * 128, Math.Min(128, encoded.Length - i * 128))}");
            }
            var chunks = new List<string>();
            string current = "";
            foreach (string field in pieces)
            {
                string candidate = current.Length == 0 ? field : current + ", " + field;
                if (Encoding.UTF8.GetByteCount(GameLog.Compose(level, "Combat", LogClass, Prefix,
                    head + ", part=2147483647, parts=2147483647, " + candidate)) > ByteLimit)
                { if (current.Length > 0) chunks.Add(current); current = field; }
                else current = candidate;
            }
            if (current.Length > 0 || chunks.Count == 0) chunks.Add(current);
            for (int i = 0; i < chunks.Count; i++)
            {
                string payload = head + $", part={i + 1}, parts={chunks.Count}" + (chunks[i].Length == 0 ? "" : ", " + chunks[i]);
                // 실제 최종 prefix/metadata까지 검사한다. 예상 밖 포맷은 조용히 유실시키지 않는다.
                int bytes = Encoding.UTF8.GetByteCount(GameLog.Compose(level, "Combat", LogClass, Prefix, payload));
                if (bytes > ByteLimit)
                {
                    _faults++;
                    GameLog.Dev.Warn("Combat", LogClass, Prefix,
                        FormattableString.Invariant($"scope=format, epoch={_epoch:x16}, deliveryEpoch={key.Epoch:x16}, wave={key.Id}, localWave={localWave}, event={_sequence}, impact={ordinal}, target={target}, stage=format-incomplete, reason=byte-overflow, originalBytes={bytes}, part={i + 1}, parts={chunks.Count}, coverageComplete=false"));
                    continue;
                }
                if (warning) GameLog.Dev.Warn("Combat", LogClass, Prefix, payload);
                else GameLog.Dev.Info("Combat", LogClass, Prefix, payload);
            }
        }
#endif
    }
}
