using System;
using System.Collections.Generic;

namespace Hexiege.Application
{
    /// <summary>독립 wave 표현의 제한 버퍼다. HP/타겟/Animation Event를 읽거나 쓰지 않는다.</summary>
    public sealed class WavePresentationCoordinator
    {
        public const double Delay = Hexiege.Application.Combat.Sequencing.AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds;
        public const int MaxWaves = 512;
        public const int MaxPendingContactsPerWave = 256;
        public const int MaxPendingContacts = 8192;
        public const int MaxRetiredGaps = 4096;
        public const double CatchupAge = .50d;
        public const double ClockPendingLifetime = 2d;
        private sealed class Receipt
        {
            public double Local, Raw;
            public bool ClockPending, Admitted;
        }
        private sealed class Slot
        {
            public WavePresentationSpawn Spawn;
            public bool HasSpawn, SpawnConsumed;
            public bool SpawnChannelFailed;
            public double Expires;
            public Receipt SpawnReceipt;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            public long SpawnAttempts, TextAttempts;
#endif
            public readonly Dictionary<int, WavePresentationContact> Contacts = new();
            public readonly HashSet<int> Consumed = new();
            public readonly HashSet<int> ChannelFailed = new();
            public readonly Dictionary<int, Receipt> Receipts = new();
        }
        private readonly Dictionary<ulong, Slot> _slots = new();
        private readonly List<ulong> _retire = new();
        private readonly HashSet<ulong> _retiredGaps = new();
        private ulong _epoch, _retiredThrough;
        private double _baseline;
        private double _lastClock;
        private int _pending;
        private double _lastLocal;
        private bool _clockInvalid;
        public int ClockPending { get; private set; }
        public int ClockRecovered { get; private set; }
        public int ClockExpired { get; private set; }
        public int ClockAnomalies { get; private set; }
        public int Failures { get; private set; }
        public int Duplicates { get; private set; }
        public int Expired { get; private set; }
        public int ChannelFailures { get; private set; }
        public bool Ended { get; set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private WavePresentationDiagnostics _diagnostics = new("coordinator");
#endif
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void Observe(string stage, WaveDeliveryKey key, double now, int ordinal = 0,
            int target = -1, string kind = "none", FormattableString detail = null, bool warning = false)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _diagnostics.Record(stage, key, now, ordinal, target, kind, detail, warning);
#endif
        }
        /// <summary>reset 전에 현재 pending과 실패를 관측한다. 게임 상태는 변경하지 않는다.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void ObserveEnd(string reason, double now, bool final)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try {
                if (final)
                    foreach (var pair in _slots)
                    {
                        var slot = pair.Value;
                        Observe("cleanup-wave-pending", new WaveDeliveryKey(_epoch, pair.Key), now,
                            detail: $"reason={reason};hasSpawn={slot.HasSpawn};spawnConsumed={slot.SpawnConsumed};pending={slot.Contacts.Count - slot.Consumed.Count};spawnAttempts={slot.SpawnAttempts};textAttempts={slot.TextAttempts}");
                        foreach (var entry in slot.Contacts)
                            if (!slot.Consumed.Contains(entry.Key))
                                Observe("cleanup-contact-pending", entry.Value.Key, now, entry.Value.Ordinal, entry.Value.VictimId,
                                    entry.Value.VictimIsUnit ? "unit" : "building", $"reason={reason};contact={entry.Value.ContactTime:F6}");
                    }
                _diagnostics.Finish(reason, now, $"slots={_slots.Count};pending={_pending};expired={Expired};channelFailures={ChannelFailures};failures={Failures};duplicates={Duplicates};clockPending={ClockPending};clockRecovered={ClockRecovered};clockExpired={ClockExpired};clockAnomalies={ClockAnomalies}", final);
            } catch { _diagnostics.ObserveFault("coordinator-end"); }
#endif
        }

        /// <summary>신규 경기 수명에서만 기억과 baseline을 초기화한다.</summary>
        public void Reset(ulong epoch, double baseline, ulong baselineId = 0)
        {
            ObserveEnd("reset", _lastClock, true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _diagnostics = new("coordinator"); _diagnostics.Begin(epoch, baseline); _clockInvalid = false;
#endif
            _slots.Clear(); _retire.Clear(); _retiredGaps.Clear(); _epoch = epoch; _baseline = baseline;
            _lastClock = baseline;
            _lastLocal = 0d; _clockInvalid = false;
            ClockPending = ClockRecovered = ClockExpired = ClockAnomalies = 0;
            _retiredThrough = baselineId; _pending = 0; Failures = Duplicates = Expired = ChannelFailures = 0; Ended = false;
            if (!Finite(baseline) || baseline < 0 || epoch == 0) { _epoch = 0; Failures++; }
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        /// <summary>소유자 분리 시 snapshot 버퍼를 즉시 해제하고 다음 Reset까지 재방출을 막는다.</summary>
        public void Close()
        {
            _slots.Clear(); _retire.Clear(); _retiredGaps.Clear();
            _epoch = 0; _pending = 0; ClockPending = 0; Ended = true;
        }
        /// <summary>재연결 baseline은 앞으로만 이동하고 dedupe 기억을 초기화하지 않는다.</summary>
        public void AdvanceBaseline(double baseline, ulong baselineId = 0)
        {
            if (Finite(baseline) && baseline >= 0) _baseline = Math.Max(_baseline, baseline);
            else Failures++;
            if (baselineId > _retiredThrough)
            {
                _retiredThrough = baselineId;
                _retiredGaps.RemoveWhere(id => id <= baselineId);
            }
        }
        private bool UsableClock(double now) => Finite(now) && now >= 0 && now >= _lastClock;
        private bool ValidateClock(double now, double local)
        {
            if (!UsableClock(now))
            {
                if (!_clockInvalid)
                {
                    ClockAnomalies++;
                    Observe("clock-invalid", new WaveDeliveryKey(_epoch, 0), now,
                        detail: $"previous={_lastClock:G17};finite={Finite(now)};negative={now < 0};backward={now < _lastClock};local={local:G17}", warning: true);
                }
                _clockInvalid = true;
                return false;
            }
            if (_clockInvalid) Observe("clock-recovered", new WaveDeliveryKey(_epoch, 0), now);
            _clockInvalid = false;
            _lastClock = now; return true;
        }

        private Receipt NewReceipt(double now, double local, double payload, WaveDeliveryKey key,
            int ordinal = 0, int victim = -1, string kind = "none")
        {
            var receipt = new Receipt { Local = local, Raw = now };
            if (!UsableClock(now) || payload > now + .25d)
                Defer(receipt, key, payload, now, local, ordinal, victim, kind);
            return receipt;
        }
        private void Defer(Receipt receipt, WaveDeliveryKey key, double payload, double now, double local,
            int ordinal, int victim, string kind)
        {
            if (receipt.ClockPending) return;
            receipt.ClockPending = true; ClockPending++;
            Observe("clock-pending", key, now, ordinal, victim, kind,
                $"reason={(!Finite(now) ? "nonfinite" : now < 0 ? "negative" : now < _lastClock ? "backstep" : "future")};receiptLocal={receipt.Local:G17};receiptRaw={receipt.Raw:G17};local={local:G17};previous={_lastClock:G17};payload={payload:G17};futureDelta={payload - now:G17};wait={local - receipt.Local:G17}", true);
        }
        private void CloseReceipt(Receipt receipt)
        {
            if (receipt == null || !receipt.ClockPending) return;
            receipt.ClockPending = false; ClockPending--;
        }
        // 로컬 시각은 기다리는 자원의 기한에만 쓰며 서버 시각이나 payload를 진행시키지 않는다.
        private bool ResolveReceipt(Receipt receipt, WaveDeliveryKey key, double payload, double now,
            double local, bool clockValid, out bool expired, int ordinal = 0, int victim = -1, string kind = "none")
        {
            expired = false;
            if (receipt.Admitted) return clockValid;
            if (!clockValid || payload > now + .25d)
                Defer(receipt, key, payload, now, local, ordinal, victim, kind);
            if (receipt.ClockPending && local - receipt.Local >= ClockPendingLifetime)
            {
                Observe("clock-pending-expired", key, now, ordinal, victim, kind,
                    $"receiptLocal={receipt.Local:G17};receiptRaw={receipt.Raw:G17};local={local:G17};previous={_lastClock:G17};payload={payload:G17};futureDelta={payload - now:G17};wait={local - receipt.Local:G17}", true);
                CloseReceipt(receipt); ClockExpired++; Expired++; Failures++; expired = true;
                return false;
            }
            if (!clockValid || payload > now + .25d) return false;
            if (receipt.ClockPending)
            {
                Observe("clock-pending-recovered", key, now, ordinal, victim, kind,
                    $"receiptLocal={receipt.Local:G17};receiptRaw={receipt.Raw:G17};local={local:G17};payload={payload:G17};futureDelta={payload - now:G17};wait={local - receipt.Local:G17}");
                CloseReceipt(receipt); ClockRecovered++;
            }
            receipt.Admitted = true;
            return true;
        }

        private Slot GetSlot(WaveDeliveryKey key, double now)
        {
            if (!key.IsValid || key.Epoch != _epoch) { Observe("slot-rejected", key, now, detail: $"reason=epoch-or-key", warning: true); Failures++; return null; }
            if (_slots.TryGetValue(key.Id, out Slot slot)) return slot;
            if (key.Id <= _retiredThrough || _retiredGaps.Contains(key.Id)) { Observe("retired-duplicate", key, now); Duplicates++; return null; }
            if (_retiredGaps.Count >= MaxRetiredGaps) { Observe("slot-rejected", key, now, detail: $"reason=retired-gap-capacity", warning: true); Failures++; return null; }
            if (_slots.Count >= MaxWaves) { Observe("slot-rejected", key, now, detail: $"reason=wave-capacity", warning: true); Failures++; return null; }
            slot = new Slot { Expires = (UsableClock(now) ? now : _lastClock) + 2.6d };
            _slots.Add(key.Id, slot);
            return slot;
        }

        /// <summary>생성된 전달체만 수락하며 같은 키의 다른 payload는 충돌이다.</summary>
        public void Accept(WavePresentationSpawn spawn, double now, double? receiptLocal = null)
        {
            double local = receiptLocal ?? now; // 기존 순수 fixture는 같은 단조 입력을 명시적으로 사용한다.
            if (!spawn.IsValid || spawn.Duration > 2d) { ObserveSpawnRejection(spawn, now); Failures++; return; }
            if (!Finite(local) || local < _lastLocal || local < 0)
            { Observe("spawn-rejected", spawn.Key, now, detail: $"reason=receipt-clock;local={local:G17}", warning: true); Failures++; return; }
            Slot slot = GetSlot(spawn.Key, now);
            if (slot == null) return;
            if (slot.HasSpawn)
            {
                if (slot.Spawn.CasterId != spawn.CasterId || slot.Spawn.Origin != spawn.Origin || slot.Spawn.Forward != spawn.Forward
                    || slot.Spawn.SpawnTime != spawn.SpawnTime || slot.Spawn.Width != spawn.Width
                    || slot.Spawn.Length != spawn.Length || slot.Spawn.Duration != spawn.Duration)
                    Failures++;
                else Duplicates++;
                Observe("spawn-repeat", spawn.Key, now, detail: $"failures={Failures};duplicates={Duplicates}");
                return;
            }
            Observe("spawn-accepted", spawn.Key, now, detail: $"spawn={spawn.SpawnTime:F6};duration={spawn.Duration:F6};delay={Delay:F6}");
            slot.Spawn = spawn; slot.HasSpawn = true;
            slot.SpawnReceipt = NewReceipt(now, local, spawn.SpawnTime, spawn.Key);
            if (!slot.SpawnReceipt.ClockPending)
                slot.Expires = Math.Max(slot.Expires, spawn.SpawnTime + spawn.Duration + Delay + 2d);
        }

        /// <summary>확정 접촉은 spawn보다 먼저 와도 exact delivery 키에만 결합한다.</summary>
        public void Accept(WavePresentationContact contact, double now, double? receiptLocal = null)
        {
            double local = receiptLocal ?? now;
            if (!contact.IsValid) { ObserveContactRejection(contact, now); Failures++; return; }
            if (!Finite(local) || local < _lastLocal || local < 0)
            { Observe("contact-rejected", contact.Key, now, contact.Ordinal, contact.VictimId,
                contact.VictimIsUnit ? "unit" : "building", $"reason=receipt-clock;local={local:G17}", true); Failures++; return; }
            Slot slot = GetSlot(contact.Key, now);
            if (slot == null) return;
            if (slot.Contacts.TryGetValue(contact.Ordinal, out var previous))
            {
                if (previous.VictimId != contact.VictimId || previous.VictimIsUnit != contact.VictimIsUnit
                    || previous.Heal != contact.Heal || previous.Team != contact.Team || previous.Position != contact.Position
                    || previous.ContactTime != contact.ContactTime || previous.AppliedAmount != contact.AppliedAmount
                    || previous.ResultingHp != contact.ResultingHp) Failures++;
                else Duplicates++;
                Observe("contact-repeat", contact.Key, now, contact.Ordinal, contact.VictimId, contact.VictimIsUnit ? "unit" : "building",
                    $"failures={Failures};duplicates={Duplicates}");
                return;
            }
            if (_pending >= MaxPendingContacts || slot.Contacts.Count >= MaxPendingContactsPerWave)
            { Observe("contact-rejected", contact.Key, now, contact.Ordinal, contact.VictimId, contact.VictimIsUnit ? "unit" : "building", $"reason=contact-capacity", true); Failures++; return; }
            Observe("contact-accepted", contact.Key, now, contact.Ordinal, contact.VictimId,
                contact.VictimIsUnit ? "unit" : "building", $"contact={contact.ContactTime:F6};hp={contact.ResultingHp};applied={contact.AppliedAmount};heal={contact.Heal}");
            slot.Contacts.Add(contact.Ordinal, contact); _pending++;
            slot.Receipts.Add(contact.Ordinal, NewReceipt(now, local, contact.ContactTime, contact.Key,
                contact.Ordinal, contact.VictimId, contact.VictimIsUnit ? "unit" : "building"));
        }

        // 거부 근거만 포맷한다. 비교 결과를 admission 우회에 사용하지 않는다.
        public static FormattableString SpawnRejectionDetail(WavePresentationSpawn s, double now)
        {
            int invalid = (!s.Key.IsValid ? 1 : 0) | (s.CasterId < 0 ? 2 : 0)
                | (!WavePresentationSpawn.Finite(s.Origin) ? 4 : 0) | (!WavePresentationSpawn.Finite(s.Forward) ? 8 : 0)
                | (!(Math.Abs(s.Forward.sqrMagnitude - 1f) < .001f) ? 16 : 0)
                | (!(Math.Abs(s.Forward.y) < .001f) ? 32 : 0)
                | (!(s.Width > 0 && WavePresentationSpawn.Finite(s.Width)) ? 64 : 0)
                | (!(s.Length > 0 && WavePresentationSpawn.Finite(s.Length)) ? 128 : 0)
                | (!(s.Duration > 0 && WavePresentationSpawn.Finite(s.Duration)) ? 256 : 0)
                | (!(Finite(s.SpawnTime) && s.SpawnTime >= 0) ? 512 : 0);
            return $"reason=payload-or-time;firstFailed={(!s.IsValid ? "payload" : s.Duration > 2d ? "duration" : "future")};invalidMask={invalid};payloadValid={s.IsValid};durationOver={s.Duration > 2d};futureOver={s.SpawnTime > now + .25d};spawn={s.SpawnTime:G17};duration={s.Duration:G17};futureLimit={now + .25d:G17};durationLimit=2;caster={s.CasterId};width={s.Width:G17};length={s.Length:G17};forwardSq={s.Forward.sqrMagnitude:G17};forwardY={s.Forward.y:G17}";
        }
        public static FormattableString ContactRejectionDetail(WavePresentationContact c, double now)
        {
            int invalid = (!c.Key.IsValid ? 1 : 0) | (!(c.Ordinal > 0 && c.Ordinal <= 256) ? 2 : 0)
                | (c.VictimId < 0 ? 4 : 0) | (c.ResultingHp < 0 ? 8 : 0) | (c.AppliedAmount <= 0 ? 16 : 0)
                | (!(c.Team == Hexiege.Domain.TeamId.Blue || c.Team == Hexiege.Domain.TeamId.Red) ? 32 : 0)
                | (c.Heal && !c.VictimIsUnit ? 64 : 0) | (!WavePresentationSpawn.Finite(c.Position) ? 128 : 0)
                | (!(Finite(c.ContactTime) && c.ContactTime >= 0) ? 256 : 0);
            return $"reason=payload-or-time;firstFailed={(!c.IsValid ? "payload" : "future")};invalidMask={invalid};payloadValid={c.IsValid};futureOver={c.ContactTime > now + .25d};contact={c.ContactTime:G17};futureLimit={now + .25d:G17};hp={c.ResultingHp};applied={c.AppliedAmount};team={(int)c.Team};heal={c.Heal}";
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveSpawnRejection(WavePresentationSpawn spawn, double now)
        {
            Observe("spawn-rejected", spawn.Key, now, detail: SpawnRejectionDetail(spawn, now), warning: true);
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveContactRejection(WavePresentationContact contact, double now)
        {
            Observe("contact-rejected", contact.Key, now, contact.Ordinal, contact.VictimId,
                contact.VictimIsUnit ? "unit" : "building", ContactRejectionDetail(contact, now), true);
        }

        /// <summary>시계상 due 표현만 한 번 호출한다. 반환 false는 다음 frame의 제한된 재시도를 허용한다.</summary>
        public void Tick(double now, Func<WavePresentationSpawn, double, bool> spawnEmitter,
            Func<WavePresentationContact, bool> contactEmitter, double? localNow = null)
        {
            double local = localNow ?? now;
            if (!Finite(local) || local < 0 || local < _lastLocal)
            { Observe("tick-rejected", new WaveDeliveryKey(_epoch, 0), now, detail: $"reason=receipt-clock;local={local:G17}", warning: true); Failures++; return; }
            _lastLocal = local;
            bool clockValid = ValidateClock(now, local);
            double presentation = now - Delay;
            _retire.Clear();
            foreach (var pair in _slots)
            {
                Slot slot = pair.Value;
                bool spawnReady = false;
                if (slot.HasSpawn && !slot.SpawnConsumed)
                {
                    if (Ended)
                    {
                        CloseReceipt(slot.SpawnReceipt); slot.SpawnConsumed = true; Expired++;
                        if (slot.SpawnChannelFailed) { ChannelFailures++; Failures++; }
                        Observe("spawn-expired", slot.Spawn.Key, now, detail: $"reason=game-end;channelFailed={slot.SpawnChannelFailed}");
                    }
                    else
                    {
                        spawnReady = ResolveReceipt(slot.SpawnReceipt, slot.Spawn.Key, slot.Spawn.SpawnTime,
                            now, local, clockValid, out bool expired);
                        if (expired) slot.SpawnConsumed = true;
                        if (spawnReady) slot.Expires = Math.Max(slot.Expires, slot.Spawn.SpawnTime + slot.Spawn.Duration + Delay + 2d);
                    }
                }
                if (spawnReady && presentation >= slot.Spawn.SpawnTime)
                {
                    double age = presentation - slot.Spawn.SpawnTime;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (!Ended && age < slot.Spawn.Duration) slot.SpawnAttempts++;
#endif
                    if (Ended || age >= slot.Spawn.Duration)
                    {
                        Observe("spawn-expired", slot.Spawn.Key, now, detail: $"reason={(Ended ? "game-end" : "age")};age={age:F6};channelFailed={slot.SpawnChannelFailed}");
                        slot.SpawnConsumed = true; Expired++;
                        if (slot.SpawnChannelFailed) { ChannelFailures++; Failures++; }
                    }
                    else if (spawnEmitter(slot.Spawn, age))
                    {
                        Observe("spawn-consumed", slot.Spawn.Key, now, detail: $"age={age:F6};recovered={slot.SpawnChannelFailed}");
                        slot.SpawnConsumed = true;
                    }
                    else
                    {
                        if (!slot.SpawnChannelFailed) Observe("spawn-channel-first-failure", slot.Spawn.Key, now, warning: true);
                        slot.SpawnChannelFailed = true;
                    }
                }
                foreach (var entry in slot.Contacts)
                {
                    if (slot.Consumed.Contains(entry.Key)) continue;
                    var contact = entry.Value;
                    if (!ResolveReceipt(slot.Receipts[entry.Key], contact.Key, contact.ContactTime, now, local,
                        clockValid, out bool expired, contact.Ordinal, contact.VictimId, contact.VictimIsUnit ? "unit" : "building"))
                    {
                        if (expired) { slot.Consumed.Add(entry.Key); _pending--; }
                        continue;
                    }
                    if (contact.ContactTime < _baseline || presentation - contact.ContactTime > CatchupAge)
                    {
                        Observe("contact-expired", contact.Key, now, contact.Ordinal, contact.VictimId, contact.VictimIsUnit ? "unit" : "building",
                            $"reason={(contact.ContactTime < _baseline ? "baseline" : "catchup-age")};contact={contact.ContactTime:F6};presentation={presentation:F6};channelFailed={slot.ChannelFailed.Contains(entry.Key)}");
                        slot.Consumed.Add(entry.Key); _pending--; Expired++;
                        if (slot.ChannelFailed.Contains(entry.Key)) { ChannelFailures++; Failures++; }
                        continue;
                    }
                    if (presentation < contact.ContactTime) continue;
                    // 확정 접촉은 spawn 유실/지연과 무관한 self-contained 결과다. 수신 HP는 절대 변경하지 않는다.
                    // spawn이 있으면 접촉 시각이 해당 wave의 서버 tick envelope 밖인지 검증한다.
                    if (slot.HasSpawn && contact.ContactTime < slot.Spawn.SpawnTime)
                    { Observe("contact-rejected", contact.Key, now, contact.Ordinal, contact.VictimId, contact.VictimIsUnit ? "unit" : "building", $"reason=before-spawn", true); Failures++; slot.Consumed.Add(entry.Key); _pending--; continue; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    slot.TextAttempts++;
#endif
                    if (contactEmitter(contact))
                    {
                        Observe("contact-consumed", contact.Key, now, contact.Ordinal, contact.VictimId, contact.VictimIsUnit ? "unit" : "building",
                            $"contact={contact.ContactTime:F6};presentation={presentation:F6};lag={presentation - contact.ContactTime:F6};recovered={slot.ChannelFailed.Contains(entry.Key)}");
                        slot.Consumed.Add(entry.Key); _pending--;
                    }
                    else
                    {
                        if (!slot.ChannelFailed.Contains(entry.Key))
                            Observe("text-channel-first-failure", contact.Key, now, contact.Ordinal, contact.VictimId, contact.VictimIsUnit ? "unit" : "building", warning: true);
                        slot.ChannelFailed.Add(entry.Key);
                    }
                }
                if (clockValid && now > slot.Expires) _retire.Add(pair.Key);
            }
            foreach (ulong id in _retire)
            {
                var slot = _slots[id];
                Observe("slot-retiring", new WaveDeliveryKey(_epoch, id), now,
                    detail: $"reason=slot-age;hasSpawn={slot.HasSpawn};spawnConsumed={slot.SpawnConsumed};contacts={slot.Contacts.Count};consumed={slot.Consumed.Count};pending={slot.Contacts.Count - slot.Consumed.Count};spawnChannelFailed={slot.SpawnChannelFailed};failedContacts={slot.ChannelFailed.Count}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _diagnostics.Retire(new WaveDeliveryKey(_epoch, id), now, $"spawnAttempts={slot.SpawnAttempts};textAttempts={slot.TextAttempts}");

#endif
                _pending -= slot.Contacts.Count - slot.Consumed.Count;
                CloseReceipt(slot.SpawnReceipt);
                foreach (var receipt in slot.Receipts.Values) CloseReceipt(receipt);
                _slots.Remove(id); _retiredGaps.Add(id);
                // global delivery ID는 생성 성공마다 1부터 증가한다. gap을 건너뛰어 과거 활성 wave를 폐기하지 않는다.
                while (_retiredThrough < ulong.MaxValue && _retiredGaps.Remove(_retiredThrough + 1)) _retiredThrough++;
            }
        }
    }
}
