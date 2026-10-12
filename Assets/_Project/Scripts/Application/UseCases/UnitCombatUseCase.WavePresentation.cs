using System;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public partial class UnitCombatUseCase
    {
        private Func<double> _waveClock = () => Time.timeAsDouble;
        private ulong _nextWaveDeliveryId;
        public ulong WaveEpoch { get; private set; }
            = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0) | 1UL;
        public double WaveServerTime => _waveClock();
        public double WavePresentationBaseline { get; private set; }
        public ulong WavePresentationBaselineId { get; private set; }
        public bool WavePresentationOwned { get; private set; }
        public bool WaveDeliveryEnabled { get; private set; }
        /// <summary>로컬 렌더러와 독립된 권위 delivery 계약을 조합 루트에서 활성화한다.</summary>
        public void SetWaveDeliveryEnabled(bool enabled) => WaveDeliveryEnabled = enabled;
        /// <summary>네트워크 권위는 로컬 표시 준비에 의존하지 않는다. Single fallback은 기존 표현을 유지한다.</summary>
        public bool CanIssueWaveDelivery => WaveDeliveryEnabled
            && (NetworkContext.IsNetworkActive || WavePresentationOwned);
        /// <summary>조합 루트의 준비된 표현 소비자만 legacy 표현 소유권을 넘겨받는다.</summary>
        public void SetWavePresentationOwned(bool enabled) => WavePresentationOwned = enabled;
        /// <summary>현재 살아 있는 wave 이전까지의 전달 ID만 snapshot retirement cut으로 제공한다.</summary>
        public ulong WaveRetirementBaselineId
        {
            get
            {
                ulong cut = _nextWaveDeliveryId;
                foreach (var wave in _activeWaves)
                    if (wave.Key.IsValid && wave.Key.Id <= cut) cut = wave.Key.Id - 1;
                return cut;
            }
        }
        public event Action<WavePresentationSpawn> WaveSpawned;
        public event Action<WavePresentationContact> WaveContactConfirmed;

        /// <summary>조합 루트에서 gameplay와 표현이 함께 읽을 시계를 주입한다.</summary>
        public void SetWaveClock(Func<double> clock) => _waveClock = clock ?? throw new ArgumentNullException(nameof(clock));

        /// <summary>새 controller 수명이 전달한 서버 epoch만 허용한다. 공격/HP는 변경하지 않는다.</summary>
        public void SetRemoteWaveEpoch(ulong epoch, double baseline = 0d, ulong baselineId = 0)
        {
            if (epoch != 0 && !NetworkContext.IsNetworkServer && !double.IsNaN(baseline) && !double.IsInfinity(baseline))
            { WaveEpoch = epoch; WavePresentationBaseline = Math.Max(WavePresentationBaseline, baseline);
              WavePresentationBaselineId = Math.Max(WavePresentationBaselineId, baselineId); }
        }

        /// <summary>늦은 참가의 현재 파도 복원용 읽기 전용 snapshot이다. 기존 접촉/피해는 재전송하지 않는다.</summary>
        public void CopyActiveWaveSpawns(System.Collections.Generic.List<WavePresentationSpawn> output)
        {
            output.Clear();
            foreach (var wave in _activeWaves)
                if (wave.Key.IsValid)
                output.Add(new WavePresentationSpawn(wave.Key, wave.CasterId, wave.Origin, wave.Forward,
                    wave.HalfWidth * 2f, wave.Length, wave.TravelTime, wave.SpawnTime));
        }

        /// <summary>서버 메시지를 로컬 표현 소비자에게 전달한다. 파도 판정 목록에는 넣지 않는다.</summary>
        public void ReceiveWaveSpawn(WavePresentationSpawn spawn)
        {
            if (spawn.IsValid && spawn.Key.Epoch == WaveEpoch) WaveSpawned?.Invoke(spawn);
        }

        /// <summary>확정 접촉만 전달하며 사망한 공격자나 피해자 View를 조회하지 않는다.</summary>
        public void ReceiveWaveContact(WavePresentationContact contact)
        {
            if (contact.IsValid && contact.Key.Epoch == WaveEpoch) WaveContactConfirmed?.Invoke(contact);
        }

        // HP는 읽기만 한다. Publish의 기존 event-after 계산·호출 순서는 유지한다.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void WaveWriterDiagnostic(ActiveWave wave, IDamageable victim, int before, int after,
            int requested, int effective, bool heal, string stage)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                int max = victim is UnitData unit ? unit.MaxHp : victim is BuildingData building ? building.MaxHp : -1;
                string reason = victim == null ? "null" : !victim.IsAlive && stage == "writer-skipped" ? "dead"
                    : heal && requested <= 0 ? "nonpositive-heal"
                    : heal && before >= max && max >= 0 ? "fullHP"
                    : !heal && effective == 0 ? "zero-damage"
                    : before == after ? "Unknown" : "changed";
                _waveDiagnostics.Record(stage, wave.Key, WaveDiagnosticClock, target: victim != null ? victim.Id : -1,
                    kind: victim is UnitData ? "unit" : "building",
                    detail: $"effect={(heal ? "heal" : "damage")};before={before};after={after};maxHp={max};requested={requested};effective={effective};aliveAfter={victim != null && victim.IsAlive};delta={after - before};zeroReason={reason}", localWave: wave.DiagnosticLocalId);
                if (stage == "writer-applied") _waveDiagnostics.Count(before == after ? "zero-" + reason : (heal ? "hp-healed" : "hp-damaged"), wave.Key, wave.DiagnosticLocalId);
            }
            catch { _waveDiagnostics.ObserveFault("writer"); }
#endif
        }

        /// <summary>종료 시 gameplay 목록은 유지하고 관측 pending만 보고·해제한다.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void FinishWaveDiagnostics(string reason, bool final)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try {
                _waveDiagnostics.Finish(reason, WaveDiagnosticClock, $"serverActive={_activeWaves.Count}", final);
                if (final) _waveDiagnosticsEpoch = 0;
            }
            catch { _waveDiagnostics.ObserveFault("server-end"); }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 추가 clock 읽기는 실패해도 gameplay의 기존 clock 호출을 대체하지 않는다.
        private double WaveDiagnosticClock
        {
            get { try { return WaveServerTime; } catch { _waveDiagnostics.ObserveFault("server-clock"); return double.NaN; } }
        }
#endif

        private void PublishWaveContact(ActiveWave wave, IDamageable victim, Vector3 position,
            int before, int after, bool heal)
        {
            int applied = heal ? after - before : before - after;
            if (applied <= 0 || !wave.Key.IsValid)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                try
                {
                    _waveDiagnostics.Record("publish-skipped", wave.Key, WaveDiagnosticClock, target: victim.Id,
                        kind: victim is UnitData ? "unit" : "building",
                        detail: $"reason={(applied <= 0 ? "nonpositive-delta" : "invalid-key")};before={before};eventAfter={after};applied={applied};heal={heal}", localWave: wave.DiagnosticLocalId);
                }
                catch (Exception exception)
                { WaveContactFault(wave, wave.ContactOrdinal, victim.Id, "diagnostic-skipped", -1, exception); }
#endif
                return;
            }
            // 표현 실패에도 ordinal은 한 번만 소비한다. HP writer를 재실행하지 않는다.
            int ordinal = ++wave.ContactOrdinal;
            int victimId = -1;
            WavePresentationContact contact;
            string stage = "snapshot";
            try
            {
                victimId = victim.Id;
                stage = "clock";
                // 표현용 읽기만 격리하며 SpawnWave/TickWaves의 실제 시계 계약은 유지한다.
                double time = WaveServerTime;
                if (double.IsNaN(time) || double.IsInfinity(time))
                    throw new InvalidOperationException("Wave contact clock is not finite");
                stage = "snapshot";
                contact = new WavePresentationContact(wave.Key, ordinal,
                    victimId, victim is UnitData, heal, victim.Team, position, after, applied, time);
                if (!contact.IsValid)
                    throw new InvalidOperationException("Wave contact snapshot violates delivery contract");
            }
            catch (Exception exception)
            {
                WaveContactFault(wave, ordinal, victimId, stage, -1, exception);
                return; // 표현만 닫는다. 사망·제거와 다음 대상 처리는 계속한다.
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                _waveDiagnostics.Record("publish-contact", wave.Key, contact.ContactTime,
                    contact.Ordinal, victim.Id, victim is UnitData ? "unit" : "building",
                    $"contact={contact.ContactTime:F6};x={position.x:F3};y={position.y:F3};z={position.z:F3};before={before};eventAfter={after};applied={applied};heal={heal};subscriber={WaveContactConfirmed != null}", false, wave.DiagnosticLocalId);
            }
            catch (Exception exception)
            { WaveContactFault(wave, ordinal, victimId, "diagnostic", -1, exception); }
#endif
            Delegate[] subscribers;
            try { subscribers = WaveContactConfirmed?.GetInvocationList(); }
            catch (Exception exception)
            { WaveContactFault(wave, ordinal, victimId, "subscriber-snapshot", -1, exception); return; }
            if (subscribers == null) return;
            // 목록을 한 번 고정한다. 콜백 중 구독 변경은 다음 접촉부터 반영한다.
            for (int i = 0; i < subscribers.Length; i++)
            {
                try { ((Action<WavePresentationContact>)subscribers[i])(contact); }
                catch (Exception exception)
                { WaveContactFault(wave, ordinal, victimId, "subscriber", i, exception); }
            }
        }

        private void WaveContactFault(ActiveWave wave, int ordinal, int victimId,
            string stage, int subscriberIndex, Exception exception)
        {
            // 고정 단계와 숫자 식별자로 context를 제한하고 원래 예외 객체를 전달한다.
            // 처리한 예외마다 Ops 한 건. 표현 성공이나 gameplay 실패로 판정하지 않는다.
            try
            {
                GameLog.Ops.Warn(LogEvent.UnhandledException, "Combat", nameof(UnitCombatUseCase),
                    "Wave contact presentation degraded; authoritative processing continues", exception,
                    $"Stage={stage}, Wave={wave.Key}, Ordinal={ordinal}, Victim={victimId}, SubscriberIndex={subscriberIndex}, GameplayContinues=True, PresentationDegraded=True");
            }
            catch (Exception)
            { /* logger 자체 실패. 재귀 기록·시계 재조회·권위 처리로 재전파하지 않는다. */ }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                _waveDiagnostics.ObserveFault("contact-" + stage);
                _waveDiagnostics.Record("contact-fault", wave.Key, double.NaN, ordinal, victimId,
                    detail: $"stage={stage};subscriberIndex={subscriberIndex};gameplayContinues=True;presentationDegraded=True",
                    warning: true, localWave: wave.DiagnosticLocalId);
            }
            catch (Exception)
            { /* Ops는 이미 시도했다. fault 기록에 재귀 폴백을 두지 않는다. */ }
#endif
        }
    }
}
