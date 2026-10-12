using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Hexiege.Application;
using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    public partial class NetworkCombatController
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private WavePresentationDiagnostics _waveDiagnostics = new("transport");
#endif
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveWave(string stage, WaveDeliveryKey key, int ordinal = 0, int target = -1,
            string kind = "none", FormattableString detail = null, bool warning = false)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try { _waveDiagnostics.Record(stage, key,
                _waveTransportCombat != null ? _waveTransportCombat.WaveServerTime : NetworkManager != null ? NetworkManager.ServerTime.Time : 0d,
                ordinal, target, kind, detail, warning); }
            catch { _waveDiagnostics.ObserveFault("transport-clock"); }
#endif
        }
        private UnitCombatUseCase _waveTransportCombat;
        private ulong _remoteWaveEpoch;
        private double _remoteWaveBaseline;
        private ulong _remoteWaveBaselineId;
        private bool _waveSnapshotRequested;
        private int _waveTransportFailures;
        private const int WaveTransportCapacity = 8192;
        private readonly HashSet<ulong> _waveSnapshotClients = new();
        private readonly List<WavePresentationSpawn> _waveSnapshotBuffer = new();
        private readonly Queue<WavePresentationSpawn> _pendingWaveSpawns = new();
        private readonly Queue<WavePresentationContact> _pendingWaveContacts = new();

        private void UpdateWaveTransport()
        {
            if (!IsSpawned || NetworkManager == null || !NetworkManager.IsListening) return;
            var combat = (_services ?? GameServicesLocator.Current)?.GetCombatUseCase();
            if (combat == null) return;
            if (_waveTransportCombat != combat)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _waveDiagnostics.Finish("combat-rebind", 0d, $"pendingSpawns={_pendingWaveSpawns.Count};pendingContacts={_pendingWaveContacts.Count}");
                _waveDiagnostics = new("transport");
                try { _waveDiagnostics.Begin(IsServer ? combat.WaveEpoch : _remoteWaveEpoch, combat.WaveServerTime); }
                catch { _waveDiagnostics.ObserveFault("transport-bind"); }
#endif
                DisposeWaveSubscriptions();
                _waveTransportCombat = combat;
                if (IsServer)
                {
                    BeginWaveEpochClientRpc(combat.WaveEpoch, combat.WaveServerTime, combat.WaveRetirementBaselineId);
                    combat.WaveSpawned += SendWaveSpawn;
                    combat.WaveContactConfirmed += SendWaveContact;
                }
            }
            if (IsServer)
            {
                foreach (ulong client in _waveSnapshotClients)
                {
                    var rpc = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { client } } };
                    BeginWaveEpochClientRpc(combat.WaveEpoch, combat.WaveServerTime, combat.WaveRetirementBaselineId, rpc);
                    combat.CopyActiveWaveSpawns(_waveSnapshotBuffer);
                    foreach (var spawn in _waveSnapshotBuffer)
                    {
                        ObserveWave("send-snapshot-spawn", spawn.Key, detail: $"peer={client};spawn={spawn.SpawnTime:F6};delivery=rpc-call");
                        SpawnWaveClientRpc(spawn.Key.Epoch, spawn.Key.Id, spawn.CasterId, spawn.Origin,
                            spawn.Forward, spawn.Width, spawn.Length, spawn.Duration, spawn.SpawnTime, rpc);
                    }
                }
                _waveSnapshotClients.Clear();
                return;
            }
            if (!_waveSnapshotRequested)
            { _waveSnapshotRequested = true; RequestWaveSnapshotServerRpc(); }
            if (_remoteWaveEpoch == 0) return;
            combat.SetRemoteWaveEpoch(_remoteWaveEpoch, _remoteWaveBaseline, _remoteWaveBaselineId);
            while (_pendingWaveSpawns.Count > 0)
            {
                var spawn = _pendingWaveSpawns.Dequeue();
                ObserveWave("dispatch-spawn", spawn.Key, detail: $"spawn={spawn.SpawnTime:F6};pending={_pendingWaveSpawns.Count}");
                combat.ReceiveWaveSpawn(spawn);
            }
            while (_pendingWaveContacts.Count > 0)
            {
                var contact = _pendingWaveContacts.Dequeue();
                ObserveWave("dispatch-contact", contact.Key, contact.Ordinal, contact.VictimId,
                    contact.VictimIsUnit ? "unit" : "building", $"contact={contact.ContactTime:F6};pending={_pendingWaveContacts.Count}");
                combat.ReceiveWaveContact(contact);
            }
        }

        private void DisposeWaveSubscriptions()
        {
            if (_waveTransportCombat == null) return;
            _waveTransportCombat.WaveSpawned -= SendWaveSpawn;
            _waveTransportCombat.WaveContactConfirmed -= SendWaveContact;
            _waveTransportCombat = null;
        }

        private void DisposeWaveTransport()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _waveTransportCombat?.FinishWaveDiagnostics("network-despawn", true);
            double diagnosticTime = 0d;
            try { if (_waveTransportCombat != null) diagnosticTime = _waveTransportCombat.WaveServerTime; }
            catch { _waveDiagnostics.ObserveFault("transport-dispose-clock"); diagnosticTime = double.NaN; }
            _waveDiagnostics.Finish("network-despawn", diagnosticTime,
                $"pendingSpawns={_pendingWaveSpawns.Count};pendingContacts={_pendingWaveContacts.Count};transportFailures={_waveTransportFailures};snapshotClients={_waveSnapshotClients.Count}");
            _waveDiagnostics = new("transport");
#endif
            DisposeWaveSubscriptions();
            _remoteWaveEpoch = 0;
            _remoteWaveBaselineId = 0;
            _remoteWaveBaseline = 0; _waveSnapshotRequested = false; _waveTransportFailures = 0;
            _waveSnapshotClients.Clear(); _waveSnapshotBuffer.Clear();
            _pendingWaveSpawns.Clear();
            _pendingWaveContacts.Clear();
        }

        private void SendWaveSpawn(WavePresentationSpawn spawn)
        {
            if (!IsSpawned || !IsServer || _combatStopped || !spawn.IsValid)
            { ObserveWave("send-spawn-rejected", spawn.Key, detail: $"spawned={IsSpawned};server={IsServer};stopped={_combatStopped};valid={spawn.IsValid}"); return; }
            ObserveWave("send-normal-spawn", spawn.Key, detail: $"spawn={spawn.SpawnTime:F6};delivery=rpc-call");
            SpawnWaveClientRpc(spawn.Key.Epoch, spawn.Key.Id, spawn.CasterId, spawn.Origin,
                spawn.Forward, spawn.Width, spawn.Length, spawn.Duration, spawn.SpawnTime);
        }

        private void SendWaveContact(WavePresentationContact contact)
        {
            // 확정 접촉은 종료 flag로 버리지 않는다. HP는 별도 기존 RPC에서 즉시 수렴한다.
            if (!IsSpawned || !IsServer || !contact.IsValid)
            { ObserveWave("send-contact-rejected", contact.Key, contact.Ordinal, contact.VictimId, contact.VictimIsUnit ? "unit" : "building", $"spawned={IsSpawned};server={IsServer};valid={contact.IsValid}"); return; }
            ObserveWave("send-contact", contact.Key, contact.Ordinal, contact.VictimId, contact.VictimIsUnit ? "unit" : "building",
                $"contact={contact.ContactTime:F6};hp={contact.ResultingHp};applied={contact.AppliedAmount};heal={contact.Heal};delivery=rpc-call");
            WaveContactClientRpc(contact.Key.Epoch, contact.Key.Id, contact.Ordinal,
                contact.VictimId, contact.VictimIsUnit, contact.Heal, (int)contact.Team,
                contact.Position, contact.ResultingHp, contact.AppliedAmount, contact.ContactTime);
        }

        [ClientRpc]
        private void BeginWaveEpochClientRpc(ulong epoch, double baseline, ulong baselineId, ClientRpcParams rpc = default)
        {
            if (IsServer || epoch == 0 || double.IsNaN(baseline) || double.IsInfinity(baseline) || baseline < 0) return;
            if (_remoteWaveEpoch != 0 && _remoteWaveEpoch != epoch)
            {
                GameLog.Dev.Warn("Combat", nameof(NetworkCombatController), "Wave epoch conflict");
                return;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_remoteWaveEpoch == 0) _waveDiagnostics.Begin(epoch, baseline);
#endif
            ObserveWave("receive-epoch", new WaveDeliveryKey(epoch, 0), detail: $"baseline={baseline:F6};baselineId={baselineId}");
            _remoteWaveEpoch = epoch;
            _remoteWaveBaseline = baseline;
            _remoteWaveBaselineId = baselineId;
            UpdateWaveTransport();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestWaveSnapshotServerRpc(ServerRpcParams rpc = default)
        {
            if (!IsSpawned || !IsServer || _waveSnapshotClients.Count >= 16) return;
            _waveSnapshotClients.Add(rpc.Receive.SenderClientId);
        }

        [ClientRpc]
        private void SpawnWaveClientRpc(ulong epoch, ulong id, int caster, Vector3 origin,
            Vector3 forward, float width, float length, float duration, double time, ClientRpcParams rpc = default)
        {
            if (IsServer || epoch != _remoteWaveEpoch)
            { if (!IsServer) ObserveWave("receive-spawn-rejected", new WaveDeliveryKey(epoch, id), detail: $"reason=epoch;expectedEpoch={_remoteWaveEpoch:x16}"); return; }
            ObserveWave("receive-spawn", new WaveDeliveryKey(epoch, id), detail: $"spawn={time:F6};mode=normal-or-snapshot");
            var spawn = new WavePresentationSpawn(new WaveDeliveryKey(epoch, id), caster,
                origin, forward, width, length, duration, time);
            if (!spawn.IsValid) { ObserveWave("receive-spawn-rejected", spawn.Key, detail: $"reason=payload"); return; }
            if (_pendingWaveSpawns.Count + _pendingWaveContacts.Count >= WaveTransportCapacity)
            { ObserveWave("receive-spawn-rejected", spawn.Key, detail: $"reason=capacity", warning: true); WaveTransportCapacityFailure(); return; }
            _pendingWaveSpawns.Enqueue(spawn);
            ObserveWave("queue-spawn", spawn.Key, detail: $"pendingSpawns={_pendingWaveSpawns.Count};pendingContacts={_pendingWaveContacts.Count}");
            UpdateWaveTransport();
        }

        [ClientRpc]
        private void WaveContactClientRpc(ulong epoch, ulong id, int ordinal, int victim,
            bool unit, bool heal, int team, Vector3 position, int hp, int applied, double time)
        {
            if (IsServer || epoch != _remoteWaveEpoch)
            { if (!IsServer) ObserveWave("receive-contact-rejected", new WaveDeliveryKey(epoch, id), ordinal, victim, unit ? "unit" : "building", $"reason=epoch;expectedEpoch={_remoteWaveEpoch:x16}"); return; }
            ObserveWave("receive-contact", new WaveDeliveryKey(epoch, id), ordinal, victim, unit ? "unit" : "building",
                $"contact={time:F6};hp={hp};applied={applied};heal={heal}");
            var contact = new WavePresentationContact(new WaveDeliveryKey(epoch, id), ordinal,
                victim, unit, heal, (TeamId)team, position, hp, applied, time);
            if (!contact.IsValid) { ObserveWave("receive-contact-rejected", contact.Key, ordinal, victim, unit ? "unit" : "building", $"reason=payload"); return; }
            if (_pendingWaveSpawns.Count + _pendingWaveContacts.Count >= WaveTransportCapacity)
            { ObserveWave("receive-contact-rejected", contact.Key, ordinal, victim, unit ? "unit" : "building", $"reason=capacity", true); WaveTransportCapacityFailure(); return; }
            _pendingWaveContacts.Enqueue(contact);
            ObserveWave("queue-contact", contact.Key, ordinal, victim, unit ? "unit" : "building",
                $"pendingSpawns={_pendingWaveSpawns.Count};pendingContacts={_pendingWaveContacts.Count}");
            UpdateWaveTransport();
        }

        private void WaveTransportCapacityFailure()
        {
            _waveTransportFailures++;
            if (_waveTransportFailures == 1 || _waveTransportFailures % 256 == 0)
                GameLog.Dev.Warn("Combat", nameof(NetworkCombatController), "Wave transport capacity exceeded; presentation rejected (HP unchanged)",
                    $"capacity={WaveTransportCapacity}, failures={_waveTransportFailures}");
        }
    }
}
