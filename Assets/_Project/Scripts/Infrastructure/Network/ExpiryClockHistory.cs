#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Globalization;
using System.Text;
using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 숫자만 저장하는 개발 전용 유한 이력이다. 실제 시각 공급/구독은 Observer가 맡고,
    /// 이 타입은 clock/HP/Coordinator를 조회하거나 변경하지 않아 CLR 대조에도 사용한다.
    /// </summary>
    public sealed class ExpiryClockHistory
    {
        public const int Capacity = 1024, WindowCapacity = 128, MaximumWindows = 4;
        public const int MaximumDumpLines = 258, MaximumSessionLines = 1033;
        public const int MaximumSessionBytes = 1033000;
        public struct Record
        {
            public ulong Generation, Sequence, Ticket, Operation;
            public int Frame;
            public uint Flags;
            public double Local, Ngo;
            public AttackClockHistorySample Sample;
        }
        private Record[] _ring, _frozen;
        private int[] _counts;
        private bool[] _unknown;
        private ulong[] _triggers;
        private ulong[] _first, _last;
        private int[] _overwrittenAtFreeze;
        private bool[] _truncated;
        private int _write, _count;
        private ulong _next, _operation, _openOperation, _trigger, _triggerOperation;
        private bool _endOperation;
        private bool _outputAttempted;
        public bool Enabled => _ring != null;
        public int Count => _count;
        public int WindowCount { get; private set; }
        public int Overwritten { get; private set; }
        public int Suppressed { get; private set; }
        public bool Saturated { get; private set; }
        public bool CounterSaturated { get; private set; }
        public int FormatFailures { get; private set; }
        public int OutputLines { get; private set; }
        public int OutputBytes { get; private set; }
        public ulong Generation { get; private set; }
        public int DiagnosticFaults { get; private set; }

        /// <summary>비활성은 배열을 만들지 않으며 이전 session의 숫자 참조도 모두 해제한다.</summary>
        public void Reset(bool enabled)
        {
            if (enabled && Generation == ulong.MaxValue) { Reset(false); Saturated = true; return; }
            if (enabled) Generation++;
            _ring = enabled ? new Record[Capacity] : null;
            _frozen = enabled ? new Record[WindowCapacity * MaximumWindows] : null;
            _counts = enabled ? new int[MaximumWindows] : null;
            _unknown = enabled ? new bool[MaximumWindows] : null;
            _triggers = enabled ? new ulong[MaximumWindows] : null;
            _first = enabled ? new ulong[MaximumWindows] : null;
            _last = enabled ? new ulong[MaximumWindows] : null;
            _overwrittenAtFreeze = enabled ? new int[MaximumWindows] : null;
            _truncated = enabled ? new bool[MaximumWindows] : null;
            _write = _count = WindowCount = Overwritten = Suppressed = 0;
            _next = _operation = _openOperation = _trigger = _triggerOperation = 0;
            _overwrittenBacking = 0;
            _endOperation = Saturated = CounterSaturated = false;
            FormatFailures = OutputLines = OutputBytes = 0;
            DiagnosticFaults = 0;
            _outputAttempted = false;
        }

        /// <summary>overflow를 wrap하지 않고 수집만 멈춘다. gameplay 판단에는 관여하지 않는다.</summary>
        public ulong Add(AttackClockHistorySample sample, double local, double ngo, int frame,
            ulong ticket = 0, bool associationKnown = false)
        {
            if (!Enabled || Saturated) return 0;
            if (_next == ulong.MaxValue || _operation == ulong.MaxValue)
            { Saturated = true; return 0; }
            if (sample.Stage == AttackClockHistoryStage.TickEnter || sample.Stage == AttackClockHistoryStage.EndEnter)
            {
                // 빠진 이전 Exit가 있으면 현재 context에 재결합하지 않는다.
                if (_trigger != 0) Freeze(true);
                _openOperation = ++_operation;
                _endOperation = sample.Stage == AttackClockHistoryStage.EndEnter;
            }
            var record = new Record { Generation = Generation, Sequence = ++_next, Ticket = ticket,
                Operation = _openOperation, Frame = frame, Local = local, Ngo = ngo, Sample = sample };
            record.Flags = (sample.Key.IsValid ? 1U : 0U) | (TimeValid(local) ? 2U : 0U)
                | (TimeValid(ngo) ? 4U : 0U) | (TimeValid(sample.ArgumentServer) ? 8U : 0U)
                | (TimeValid(sample.ArgumentLocal) ? 16U : 0U)
                | (sample.HasAnchor && TimeValid(sample.AnchorServer) && TimeValid(sample.AnchorLocal) ? 32U : 0U)
                | (TimeValid(sample.CurrentTime) ? 64U : 0U) | (sample.HasAnchor ? 128U : 0U)
                | (associationKnown ? 0U : 256U)
                | (TimeValid(sample.Impact) ? 512U : 0U)
                | (TimeValid(sample.Intended) ? 1024U : 0U)
                | (TimeValid(sample.Observed) ? 2048U : 0U)
                // 4096이면 Ngo 필드는 별도 fresh 조회가 아니라 실제 Tick 공급 표본이다.
                | ((sample.Stage == AttackClockHistoryStage.TickEnter || sample.Stage == AttackClockHistoryStage.TickExit
                    || sample.Stage == AttackClockHistoryStage.Frame || sample.Stage == AttackClockHistoryStage.Decision)
                    && TimeValid(sample.ArgumentServer) ? 4096U : 0U);
            if (_count == Capacity) Increment(ref _overwrittenBacking); else _count++;
            Overwritten = _overwrittenBacking;
            _ring[_write] = record; _write = (_write + 1) % Capacity;
            if (sample.Stage == AttackClockHistoryStage.Decision
                && sample.Status == (int)AuthoritativePresentationStatus.Expired && _trigger == 0)
            {
                _trigger = record.Sequence; _triggerOperation = _openOperation;
            }
            if (sample.Stage == AttackClockHistoryStage.TickExit || sample.Stage == AttackClockHistoryStage.EndExit)
            {
                if (_trigger != 0) Freeze(_triggerOperation == 0 || _triggerOperation != _openOperation
                    || _endOperation || sample.Stage != AttackClockHistoryStage.TickExit);
                _openOperation = 0; _endOperation = false;
            }
            return record.Sequence;
        }
        private int _overwrittenBacking;
        private void Increment(ref int counter) { if (counter < int.MaxValue) counter++; else CounterSaturated = true; }
        private static bool TimeValid(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;

        /// <summary>RPC entry 숫자가 같은 저장소에 실제로 남아 있어야만 key를 연결한다.</summary>
        public bool HasRpcTicket(ulong ticket, int unit)
        {
            if (!Enabled || ticket == 0) return false;
            for (int i = 0; i < _count; i++)
            {
                var row = Read(i);
                if (row.Sequence == ticket) return row.Sample.Stage == AttackClockHistoryStage.RpcEntry
                    && row.Sample.UnitId == unit;
            }
            return false;
        }

        /// <summary>EndSession만 호출한다. 정상 Tick 이외 종료에서는 coverage unknown을 강제한다.</summary>
        public void FinishPending() { if (_trigger != 0) Freeze(true); }
        private void Freeze(bool unknown)
        {
            if (WindowCount >= MaximumWindows) { int n = Suppressed; Increment(ref n); Suppressed = n; }
            else
            {
                int count = Math.Min(_count, WindowCapacity), start = _count - count;
                for (int i = 0; i < count; i++) _frozen[WindowCount * WindowCapacity + i] = Read(start + i);
                _counts[WindowCount] = count; _triggers[WindowCount] = _trigger;
                _first[WindowCount] = count > 0 ? Read(start).Sequence : 0;
                _last[WindowCount] = count > 0 ? Read(_count - 1).Sequence : 0;
                _overwrittenAtFreeze[WindowCount] = Overwritten;
                _truncated[WindowCount] = start > 0 || Overwritten > 0;
                _unknown[WindowCount] = unknown || count == 0 || _first[WindowCount] > _trigger
                    || _truncated[WindowCount] || Saturated || DiagnosticFaults > 0
                    || UnitAttackResultPresentationShadowBridge.ClockHistoryFaults > 0;
                WindowCount++;
            }
            _trigger = _triggerOperation = 0;
        }
        private Record Read(int index) => _ring[(_write - _count + index + Capacity) % Capacity];
        /// <summary>검증은 저장한 값을 읽기만 하고 writer나 순서를 바꾸지 않는다.</summary>
        public Record ReadForValidation(int index) => Read(index);
        public bool WindowCoverageUnknown(int window) => _unknown[window];
        public int WindowRecordCount(int window) => _counts[window];
        public ulong WindowFirstSequence(int window) => _first[window];
        public ulong WindowLastSequence(int window) => _last[window];
        public bool WindowTruncated(int window) => _truncated[window];
        public int WindowOverwrittenAtFreeze(int window) => _overwrittenAtFreeze[window];
        /// <summary>진단 예외는 bounded 숫자로만 표시하고 gameplay 예외로 전파하지 않는다.</summary>
        public void RecordDiagnosticFault() { int n = DiagnosticFaults; Increment(ref n); DiagnosticFaults = n; }

        private static string N(double value) => value.ToString("G17", CultureInfo.InvariantCulture);
        /// <summary>실제 sink가 사용하는 공통 formatter이며 prefix를 별도로 추정하지 않는다.</summary>
        public static string FullLine(string data) => GameLog.Compose(
            Hexiege.Application.LogLevel.Warn, "Network", nameof(UnitAttackShadowObserver), "[UAS-EXPIRY-CLOCK]", data);
        /// <summary>실제 logger envelope까지 검사하며 초과 문자열을 잘라내지 않는다.</summary>
        public static bool IsLineSafe(string data) => Encoding.UTF8.GetByteCount(FullLine(data)) < 1000;
        /// <summary>최악 숫자 폭도 같은 production formatter로 검증할 수 있는 표시다.</summary>
        public static string FormatRecord(Record row, int part)
        {
            var s = row.Sample; var k = s.Key;
            string head = $"generation={row.Generation}, seq={row.Sequence}, ticket={row.Ticket}, op={row.Operation}, part={part}, frame={row.Frame}, stage={(int)s.Stage}, flags={row.Flags}";
            return part == 0 ? head + $", unit={s.UnitId}, instance={k.AttackerInstanceId.Value}, sequence={k.SequenceId.Value}, hit={k.HitIndex}, kind={k.VictimKind}, victim={k.VictimId}, effect={k.EffectKind}, ordinal={k.ResultOrdinal}, rev={s.ActionRevision}, local={N(row.Local)}, ngoEstimate={N(row.Ngo)}, argServer={N(s.ArgumentServer)}, argLocal={N(s.ArgumentLocal)}"
                : head + $", anchorSource={s.AnchorSource}, anchorRev={s.AnchorRevision}, anchorServer={N(s.AnchorServer)}, anchorLocal={N(s.AnchorLocal)}, current={N(s.CurrentTime)}, impact={N(s.Impact)}, intended={N(s.Intended)}, observed={N(s.Observed)}, status={s.Status}";
        }
        private string Preflight(string identity, string data)
        {
            if (IsLineSafe(identity + data)) return identity + data;
            int failures = FormatFailures; Increment(ref failures); FormatFailures = failures;
            string fallback = identity + $"generation={Generation}, formatIncomplete=true, coverageUnknown=true";
            // identity 자체가 잘못된 경우도 줄을 잘라내거나 다른 run으로 위장하지 않는다.
            // 실제 identity는 고정 길이 hash/run 값이므로 이 경로는 손상/fixture 입력이다.
            if (!IsLineSafe(fallback)) { RecordDiagnosticFault(); return null; }
            return fallback;
        }
        private void Emit(Action<string> sink, string identity, string data)
        {
            string line = Preflight(identity, data);
            if (line == null) return;
            int bytes = Encoding.UTF8.GetByteCount(FullLine(line));
            if (OutputLines >= MaximumSessionLines || OutputBytes + bytes > MaximumSessionBytes) return;
            OutputLines++; OutputBytes += bytes;
            try { sink(line); } catch (Exception) { RecordDiagnosticFault(); }
        }
        /// <summary>session 종료 때 한 번만 문자열을 만들고 기존 terminal 예산을 소비하지 않는다.</summary>
        public void WriteAtEnd(Action<string> sink, string identity)
        {
            if (!Enabled || _outputAttempted) return;
            _outputAttempted = true;
            FinishPending();
            bool coverageUnknown = false;
            for (int w = 0; w < WindowCount; w++)
            {
                coverageUnknown |= _unknown[w];
                Emit(sink, identity, $"generation={Generation}, dump={w}, boundary=begin, trigger={_triggers[w]}, rows={_counts[w]}, first={_first[w]}, last={_last[w]}, windowTruncated={_truncated[w]}, overwrittenAtFreeze={_overwrittenAtFreeze[w]}, coverageUnknown={_unknown[w]}");
                for (int i = 0; i < _counts[w]; i++)
                    for (int p = 0; p < 2; p++) Emit(sink, identity, FormatRecord(_frozen[w * WindowCapacity + i], p));
                Emit(sink, identity, $"generation={Generation}, dump={w}, boundary=end, coverageUnknown={_unknown[w]}");
            }
            Emit(sink, identity, $"generation={Generation}, boundary=session-end, windows={WindowCount}, overwritten={Overwritten}, suppressed={Suppressed}, saturated={Saturated}, counterSaturated={CounterSaturated}, formatFailures={FormatFailures}, diagnosticFaults={DiagnosticFaults}, bridgeFaults={UnitAttackResultPresentationShadowBridge.ClockHistoryFaults}, pendingOperation={_openOperation}, coverageUnknown={coverageUnknown || Saturated || CounterSaturated || _openOperation != 0 || Suppressed > 0 || DiagnosticFaults > 0 || UnitAttackResultPresentationShadowBridge.ClockHistoryFaults > 0 || FormatFailures > 0 || Overwritten > 0}, gameplayWrites=0");
        }
    }
}
#endif
