using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Hexiege.Application;
using Hexiege.Core;
using Hexiege.Domain;
using Hexiege.Presentation;
using UnityEditor;
using UnityEngine;
using UniRx;

namespace Hexiege.EditorTools
{
    /// <summary>실제 서버 wave writer와 표현 adapter의 경계 회귀를 기존 self-validation에서 실행한다.</summary>
    public static class TorrentWaveProductionValidation
    {
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Torrent wave: " + message); }
        private static WavePresentationSpawn Spawn(ulong id, double time = 10d, int caster = 7)
            => new(new WaveDeliveryKey(9, id), caster, Vector3.zero, Vector3.forward, 3, 3, .5f, time);
        private static WavePresentationContact Contact(ulong id, int ordinal = 1, double time = 10.1d)
            => new(new WaveDeliveryKey(9, id), ordinal, 17, true, false, TeamId.Red,
                new Vector3(0, 0, 1), 90, 10, time);

        /// <summary>파도 판정 수치·원본 에셋·Unresolved 상태를 변경하지 않고 production 계약을 점검한다.</summary>
        public static void Run()
        {
            Require(!UnityEngine.Application.isPlaying, "validation is Edit Mode only");
            ValidateClockRecovery(); ValidateCoordinator(); ValidateServerClockAndWriter(); ValidateContactAuthorityIsolation(); ValidateProductionAdapter();
            // [R4] 원본 에셋이 바뀌지 않았는지 해시로 고정 검사한다.
            // 기대값은 저장소에 커밋된 LF 줄바꿈 원본 기준이다. Windows(autocrlf=true)에서는 디스크 파일이
            // CRLF로 바뀌어 있으므로, 줄바꿈을 LF로 정규화한 뒤 해시해 운영체제와 무관하게 같은 값을 얻는다.
            // Require(Hash("Assets/_Project/Prefabs/VFX/Units/vfx_torrentspirit_attack.prefab")
            //     == "7F9C9314C04B0D5FF54EC28362EB7897F3E0B54E5E0F24AC5CAF9523018F1ED3", "source VFX hash changed");
            // Require(Hash("Assets/_Project/Animations/Units/TorrentSpirit/TorrentSpirit_Attack.anim")
            //     == "E8B96CDF405944BC27DA5DEDBED17D1697106A05CB5DFCA4125B70F7A0145421", "source clip hash changed");
            Require(ContentHashWithNormalizedLineEndings("Assets/_Project/Prefabs/VFX/Units/vfx_torrentspirit_attack.prefab")
                == "7F9C9314C04B0D5FF54EC28362EB7897F3E0B54E5E0F24AC5CAF9523018F1ED3", "source VFX hash changed");
            Require(ContentHashWithNormalizedLineEndings("Assets/_Project/Animations/Units/TorrentSpirit/TorrentSpirit_Attack.anim")
                == "E8B96CDF405944BC27DA5DEDBED17D1697106A05CB5DFCA4125B70F7A0145421", "source clip hash changed");
            Debug.Log("[TORRENT-WAVE] production validation PASS: actual writer newborn/stall/one-hit/keyed damage/heal, dedupe/conflict/reorder/retire/clock/capacity/channel/end, Blue/Red adapter, source hashes. Seeded particle/silhouette and device visual acceptance are separate gates.");
        }

        // 실제 Coordinator 수신 뒤 회복 Tick만으로 원래 snapshot이 살아남는지 먼저 검사한다.
        private static void ValidateClockRecovery()
        {
            var c = new WavePresentationCoordinator();
            int launches = 0, contacts = 0;
            var order = new List<int>();
            Func<WavePresentationSpawn, double, bool> launch = (_, _) => { launches++; return true; };
            Func<WavePresentationContact, bool> hit = value => { contacts++; order.Add(value.Ordinal); return true; };
            c.Reset(9, 178);
            var spawn79 = Spawn(79, 179.75334852315484);
            c.Accept(spawn79, 179.43359007921069, 100);
            Require(c.ClockPending == 1, "future wave79 not retained as ClockPending");
            c.Tick(179.60, launch, hit, 100.1);
            Require(launches == 0 && c.ClockRecovered == 1, "admission bypassed normal due time");
            c.Tick(179.90, launch, hit, 100.45);
            Require(launches == 1, "future wave79 must survive receipt clock rejection and recover without resubmission");
            c.Accept(spawn79, 179.91, 100.46); c.Tick(179.92, launch, hit, 100.47);
            Require(launches == 1 && c.Duplicates == 1 && c.Failures == 0, "recovered spawn repeated");
            c.Reset(9, 263);
            c.Tick(264.21710649158808, launch, hit, 200);
            var a = RecordedContact(242, 1, 11, 1800, 264.351501);
            var b = RecordedContact(242, 2, 13, 1000, 264.351501);
            var d = RecordedContact(243, 1, 13, 800, 264.473209);
            c.Accept(a, 264.05003504324702, 200.1);
            c.Accept(b, 264.05003504324702, 200.1);
            c.Accept(d, 264.15039709415288, 200.2);
            Require(c.ClockPending == 3, "recorded backstep/future contacts not retained");
            c.Tick(264.15039709415288, launch, hit, 200.3);
            Require(contacts == 0 && c.ClockAnomalies == 1, "backstep emitted or anomaly hidden");
            c.Tick(264.60, launch, hit, 200.6);
            Require(contacts == 3 && order.Count == 3 && order[0] == 1 && order[1] == 2 && order[2] == 1
                && c.ClockRecovered == 3 && c.ClockPending == 0 && c.Failures == 0,
                "backstep contacts must recover together without resubmission in original ordinal order");
            c.Accept(a, 264.61, 200.61); c.Accept(b, 264.61, 200.61); c.Accept(d, 264.61, 200.61);
            c.Tick(264.62, launch, hit, 200.62);
            Require(contacts == 3 && c.Duplicates == 3, "recovered contact repeated");
            ValidateClockExpiry(); ValidateClockCapacity();
        }

        private static WavePresentationContact RecordedContact(ulong wave, int ordinal, int victim, int hp, double time)
            => new(new WaveDeliveryKey(9, wave), ordinal, victim, false, false, TeamId.Blue, Vector3.zero, hp, 200, time);

        private static void ValidateClockExpiry()
        {
            var c = new WavePresentationCoordinator();
            int launches = 0, contacts = 0;
            Func<WavePresentationSpawn, double, bool> launch = (_, _) => { launches++; return true; };
            Func<WavePresentationContact, bool> hit = _ => { contacts++; return true; };
            c.Reset(9, 9);
            var future = Spawn(1, 1000);
            c.Accept(future, 10, 20); c.Accept(future, 10.1, 21.99);
            c.Tick(10.2, launch, hit, 22);
            Require(c.ClockExpired == 1 && c.ClockPending == 0 && c.Duplicates == 1 && c.Failures == 1,
                "malicious future or duplicate extended first receipt deadline");
            c.Accept(future, 1000, 22.1); c.Tick(1000.11, launch, hit, 22.2);
            Require(launches == 0, "expired spawn reactivated");
            c.Reset(9, 9);
            var contact = Contact(1);
            c.Accept(contact, double.NaN, 30);
            c.Tick(double.NaN, launch, hit, 31.99);
            Require(contacts == 0 && c.ClockPending == 1, "nonfinite clock allowed expression");
            c.Tick(double.NaN, launch, hit, 32);
            Require(c.ClockExpired == 1 && c.ClockPending == 0, "no-recovery wait exceeded two local seconds");
            c.Accept(contact, 10.2, 32.1); c.Tick(10.3, launch, hit, 32.2);
            Require(contacts == 0 && c.Duplicates == 1, "expired ordinal tombstone lost");
            c.Accept(Contact(1, 1, 10.2), 10.3, 32.3);
            Require(c.Failures == 2, "expired ordinal conflict reactivated");

            c.Reset(9, 9);
            c.Accept(Spawn(1, 9.24), 9, 40); c.Tick(9, launch, hit, 40);
            c.Tick(9, launch, hit, 42.2);
            Require(c.ClockPending == 0 && c.ClockExpired == 0, "normal future due incorrectly got two-second expiry");
            c.Tick(9.35, launch, hit, 42.3);
            Require(launches == 1, "normal due or existing slot lifetime shortened");
            c.Reset(9, 9); launches = 0;
            c.Accept(Spawn(1, 10), 10.2, 50);
            c.Accept(Contact(1, 1, 10.1), 10.2, 50);
            c.Tick(10.09, launch, hit, 50.1);
            Require(c.ClockAnomalies == 0 && launches == 0 && contacts == 0, "receipt advanced Tick high-water or bypassed delay");
            c.Tick(10.21, launch, hit, 50.2);
            Require(launches == 1 && contacts == 1, "normal receipt/Tick ordering changed");
            c.Reset(9, 9); launches = contacts = 0;
            c.Accept(Contact(1), 9, 60); c.Tick(10.700001, launch, hit, 60.2);
            Require(contacts == 0 && c.Expired == 1 && c.ClockRecovered == 1, "recovery bypassed genuine catch-up expiry");
            c.Reset(9, 9);
            c.Accept(Spawn(1), 9, 70); c.Accept(Contact(1), 9, 70); c.Ended = true;
            c.Tick(10.21, launch, hit, 70.2);
            Require(launches == 0 && contacts == 1 && c.ClockPending == 0, "GameEnd clock pending cancellation/contact preservation failed");
            c.Reset(9, 9); c.Accept(contact, 9, 80); c.Reset(10, 9);
            contacts = 0; c.Tick(10.3, launch, hit, 80.2); c.Accept(contact, 10.3, 80.3);
            Require(contacts == 0 && c.ClockPending == 0 && c.Failures == 1, "epoch reset replayed an old pending owner");
            c.Reset(9, 9); c.Accept(contact, 9, 90); c.Close();
            c.Tick(10.3, launch, hit, 90.2); c.Accept(contact, 10.3, 90.3);
            Require(contacts == 0 && c.ClockPending == 0 && c.Ended && c.Failures == 1,
                "detached owner retained or replayed pending snapshots");
            c.Reset(10, 9);
            var next = new WavePresentationContact(new WaveDeliveryKey(10, 1), 1, 17, true, false,
                TeamId.Red, Vector3.zero, 90, 10, 10.1);
            c.Accept(next, 10.1, 91); c.Tick(10.21, launch, hit, 91.1);
            Require(contacts == 1 && !c.Ended && c.Failures == 0, "new owner did not resume after detach");
        }

        private static void ValidateClockCapacity()
        {
            var c = new WavePresentationCoordinator();
            c.Reset(9, 9);
            for (ulong wave = 1; wave <= WavePresentationCoordinator.MaxWaves; wave++)
                c.Accept(Spawn(wave, 1000), 10, 10);
            c.Accept(Spawn(513, 1000), 10, 10);
            Require(c.ClockPending == 512 && c.Failures == 1, "ClockPending bypassed wave capacity");
            c.Reset(9, 9);
            for (ulong wave = 1; wave <= 32; wave++)
                for (int ordinal = 1; ordinal <= 256; ordinal++) c.Accept(Contact(wave, ordinal, 1000), 10, 10);
            c.Accept(Contact(1, 257, 1000), 10, 10);
            c.Accept(Contact(33, 1, 1000), 10, 10);
            Require(c.ClockPending == 8192 && c.Failures == 2, "ClockPending bypassed per-wave/global contact capacity");
            c.Reset(9, 9);
            for (ulong wave = 2; wave <= 4097; wave++)
            {
                double time = 10 + wave * 3;
                c.Accept(Spawn(wave, time), time, time);
                c.Tick(time + 3, (_, _) => true, _ => true, time + 3);
            }
            c.Accept(Spawn(4098, 13000), 13000, 13000);
            Require(c.Failures == 1, "ClockPending changes bypassed retired gap capacity");
        }

        private static void ValidateCoordinator()
        {
            var c = new WavePresentationCoordinator();
            int launches = 0, contacts = 0;
            Func<WavePresentationSpawn, double, bool> launch = (_, _) => { launches++; return true; };
            Func<WavePresentationContact, bool> hit = _ => { contacts++; return true; };
            c.Reset(9, 9); c.Accept(Contact(1), 10.1); c.Accept(Contact(1), 10.1);
            c.Tick(10.21, launch, hit);
            Require(contacts == 1 && c.Duplicates == 1, "self-contained contact-first must emit exactly once");
            c.Accept(Spawn(1), 10.21); c.Accept(Spawn(1), 10.21); c.Tick(10.3, launch, hit);
            Require(launches == 1 && contacts == 1, "spawn/result ordering must not duplicate");
            c.Accept(Spawn(1, 10, 8), 10.3); Require(c.Failures == 1, "caster conflict not detected");
            c.Accept(Contact(1), double.NaN); c.Tick(10.2, launch, hit);
            Require(c.Failures == 3, "nonfinite/backwards clock must fail closed");

            c.Reset(9, 9); launches = contacts = 0;
            c.Accept(Spawn(2), 10); c.Tick(10.1, launch, hit);
            c.Accept(Contact(1, 1, 12.7), 12.7); c.Tick(12.7, launch, hit);
            // newer ID 2 has retired but ID 1 has an existing delayed slot.
            c.Accept(Spawn(1, 12.7), 12.7); c.Accept(Contact(1, 2, 12.7), 12.7);
            c.Tick(12.81, launch, hit);
            Require(launches == 2 && contacts == 2 && c.Failures == 0, "newer retirement invalidated active older slot");

            c.Reset(9, 9); contacts = 0;
            for (ulong wave = 1; wave <= 12; wave++)
                for (int ordinal = 1; ordinal <= 64; ordinal++) c.Accept(Contact(wave, ordinal), 10.1);
            c.Tick(10.21, launch, hit);
            Require(contacts == 768 && c.Failures == 0, "64-entry global regression under simultaneous waves");
            c.Reset(9, 9); c.Accept(Spawn(1), 10); c.Accept(Contact(1), 10.1);
            c.Tick(10.21, (_, _) => false, _ => false); c.Tick(10.8, (_, _) => false, _ => false);
            Require(c.ChannelFailures == 2 && c.Failures == 2, "channel failures hidden as normal expiry");
            c.Reset(9, 9); c.Accept(Spawn(1), 10); c.Accept(Contact(1), 10.1); c.Ended = true;
            contacts = 0; c.Tick(10.21, launch, hit);
            Require(contacts == 1, "GameEnd swallowed already-confirmed contact");
            Require(!Spawn(1, 10, -1).IsValid, "negative caster accepted");
            var invalidTeam = new WavePresentationContact(new WaveDeliveryKey(9, 1), 1, 0, true,
                false, (TeamId)999, Vector3.zero, 90, 10, 10);
            Require(!invalidTeam.IsValid, "invalid team accepted");
            // 초기 수신 이전 ID 1은 존재하지 않아도 서버 snapshot cut 이후 4097번째 파도가 정상 수락된다.
            c.Reset(9, 0, 1); launches = 0;
            for (ulong id = 2; id <= 4098; id++)
            {
                double time = id * 4d;
                c.Accept(Spawn(id, time), time);
                c.Tick(time + .11d, launch, hit);
                c.Tick(time + 3d, launch, hit);
            }
            Require(launches == 4097 && c.Failures == 0, "missing initial ID poisoned retirement after 4096 waves");
            c.AdvanceBaseline(20000, 5000);
            c.Accept(Spawn(5001, 20000), 20000); c.Tick(20000.11, launch, hit);
            Require(launches == 4098 && c.Failures == 0, "new baseline cut failed");
        }

        private sealed class Positions : IEntityPositionProvider
        {
            public Vector3 GetUnitWorldPosition(int id) => new Vector3(0, 0, id == 0 ? 0 : id == 3 ? 2.9f : .1f);
            public Vector3 GetBuildingWorldPosition(int id) => new Vector3(0, 0, 1);
        }

        private static void ValidateServerClockAndWriter()
        {
            double now = 100;
            var units = new UnitSpawnUseCase(null);
            var buildings = new BuildingPlacementUseCase(null);
            var combat = new UnitCombatUseCase(null, units, buildings, new Positions(), new HexMetricsCoordinateMapper());
            combat.SetWaveClock(() => now);
            Require(!combat.WavePresentationOwned, "fallback unexpectedly owns legacy presentation");
            combat.SetWaveDeliveryEnabled(true);
            combat.SetWavePresentationOwned(true);
            var caster = new UnitData(0, UnitType.TorrentSpirit, TeamId.Blue, new HexCoord(0, 0), 1000, 10, 3, 4);
            var victim = new UnitData(1, UnitType.DustSpirit, TeamId.Red, new HexCoord(0, 0), 1000, 1, 1, 1);
            var ally = new UnitData(2, UnitType.DustSpirit, TeamId.Blue, new HexCoord(0, 0), 1000, 1, 1, 1);
            var far = new UnitData(3, UnitType.DustSpirit, TeamId.Red, new HexCoord(0, 0), 1000, 1, 1, 1);
            ally.TakeDamage(100);
            var storage = (Dictionary<int, UnitData>)typeof(UnitSpawnUseCase).GetField("_units", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(units);
            storage.Add(0, caster); storage.Add(1, victim); storage.Add(2, ally); storage.Add(3, far);
            WaveDeliveryKey key = default; int confirmed = 0, damaged = 0, healed = 0;
            bool spawnMatches = false, contactsMatch = true, damageKeysMatch = true, healKeysMatch = true;
            combat.WaveSpawned += s => { key = s.Key; spawnMatches = s.SpawnTime == 100 && s.Width == 3 && s.Length == 3 && s.Duration == .5f; };
            combat.WaveContactConfirmed += h => { confirmed++; contactsMatch &= h.Key.Equals(key) && h.AppliedAmount == 10; };
            using var damageSubscription = GameEvents.OnEntityDamaged.Subscribe(e =>
            { if (ReferenceEquals(e.Entity, victim) || ReferenceEquals(e.Entity, far)) { damaged++; damageKeysMatch &= e.WavePresentationKey.Equals(key); } });
            using var healSubscription = GameEvents.OnEntityHealed.Subscribe(e =>
            { if (ReferenceEquals(e.Entity, ally)) { healed++; healKeysMatch &= e.WavePresentationKey.Equals(key); } });
            var spawn = typeof(UnitCombatUseCase).GetMethod("SpawnWave", BindingFlags.Instance | BindingFlags.NonPublic);
            spawn.Invoke(combat, new object[] { new WaveSpawnRequest(caster, Vector3.zero, Vector3.forward, 3, 3, .5f, 10) });
            Require(spawnMatches, "spawn payload differs from server geometry");
            combat.TickWaves(5f);
            Require(victim.Hp == 1000 && confirmed == 0, "newborn consumed retroactive frame/tick elapsed");
            now = 100.02; combat.TickWaves(.00001f);
            Require(victim.Hp == 990 && ally.Hp == 910 && confirmed == 2, "clock age/contact/writer rates diverged");
            combat.TickWaves(10f);
            Require(victim.Hp == 990 && confirmed == 2, "same clock dt reapplied target");
            now = 100.8; combat.TickWaves(.00001f);
            Require(far.Hp == 990 && confirmed == 3 && damaged == 2 && healed == 1, "stall failed final front or one-target gate");
            now = 102; combat.TickWaves(1);
            Require(confirmed == 3, "completed wave retained extra contacts");
            // callback 예외 격리가 assertion 실패를 숨기지 않도록 반드시 밖에서 판정한다.
            Require(contactsMatch, "writer contact key/actual amount");
            Require(damageKeysMatch && healKeysMatch, "damage/heal ownership lost");
        }

        private sealed class ContactFaultSink : ILogSink
        {
            public readonly List<string> Context = new();
            public readonly List<Exception> Exceptions = new();
            public bool Bounded = true;
            public void Write(LogLevel level, string system, string className, string message, string data, Exception exception)
            {
                if (message != "Wave contact presentation degraded; authoritative processing continues") return;
                Context.Add(data); Exceptions.Add(exception);
                Bounded &= level == LogLevel.Warn && exception != null
                    && System.Text.Encoding.UTF8.GetByteCount(GameLog.Compose(level, system, className, message, data)) < 900;
            }
        }

        private static void ValidateContactAuthorityIsolation()
        {
            foreach (string mode in new[] { "normal", "subscriber", "clock", "nonfinite", "none" })
                ValidateContactAuthorityIsolation(mode);
        }

        private static void ValidateContactAuthorityIsolation(string mode)
        {
            var unitCounter = typeof(UnitData).GetField("_nextId", BindingFlags.Static | BindingFlags.NonPublic);
            var buildingCounter = typeof(BuildingData).GetField("_nextId", BindingFlags.Static | BindingFlags.NonPublic);
            int previousUnitId = (int)unitCounter.GetValue(null), previousBuildingId = (int)buildingCounter.GetValue(null);
            double now = 100;
            var units = new UnitSpawnUseCase(null);
            var grid = new HexGrid(1, 1);
            var buildings = new BuildingPlacementUseCase(grid);
            var combat = new UnitCombatUseCase(null, units, buildings, new Positions(), new HexMetricsCoordinateMapper());
            combat.SetWaveClock(() => now);
            combat.SetWaveDeliveryEnabled(true); combat.SetWavePresentationOwned(true);
            var caster = new UnitData(0, UnitType.TorrentSpirit, TeamId.Blue, new HexCoord(0, 0), 1000, 10, 3, 4);
            var lethal = new UnitData(1, UnitType.DustSpirit, TeamId.Red, new HexCoord(0, 0), 5, 1, 1, 1);
            var survivor = new UnitData(4, UnitType.DustSpirit, TeamId.Red, new HexCoord(0, 0), 1000, 1, 1, 1);
            var ally = new UnitData(2, UnitType.DustSpirit, TeamId.Blue, new HexCoord(0, 0), 1000, 1, 1, 1);
            var full = new UnitData(5, UnitType.DustSpirit, TeamId.Blue, new HexCoord(0, 0), 1000, 1, 1, 1);
            ally.TakeDamage(100);
            var unitStorage = (Dictionary<int, UnitData>)typeof(UnitSpawnUseCase).GetField("_units", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(units);
            foreach (var unit in new[] { caster, lethal, survivor, ally, full }) unitStorage.Add(unit.Id, unit);
            var building = new BuildingData(7, BuildingType.TrainingCamp, TeamId.Red, new HexCoord(0, 0), 5);
            var buildingStorage = (Dictionary<int, BuildingData>)typeof(BuildingPlacementUseCase).GetField("_buildings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(buildings);
            buildingStorage.Add(building.Id, building);
            grid.GetTile(building.Position).HasBuilding = true;
            WaveDeliveryKey key = default;
            Action<WavePresentationSpawn> onSpawn = s => key = s.Key;
            var snapshots = new List<WavePresentationContact>();
            var thrownSnapshots = new List<WavePresentationContact>();
            var order = new List<string>();
            bool snapshotBeforeRemoval = true, damageKeys = true, healKeys = true;
            int deaths = 0, buildingDeaths = 0, damages = 0, heals = 0, contactClockReads = 0;
            var injected = new InvalidOperationException("intentional contact " + mode + " fixture");
            Action<WavePresentationContact> throwing = h => { thrownSnapshots.Add(h); order.Add("throw:" + h.Ordinal); throw injected; };
            Action<WavePresentationContact> healthy = h =>
            {
                snapshots.Add(h); order.Add("contact:" + h.Ordinal);
                snapshotBeforeRemoval &= h.VictimIsUnit ? unitStorage.ContainsKey(h.VictimId) : buildingStorage.ContainsKey(h.VictimId);
            };
            var sink = new ContactFaultSink();
            combat.WaveSpawned += onSpawn;
            if (mode == "subscriber") combat.WaveContactConfirmed += throwing;
            if (mode != "none") combat.WaveContactConfirmed += healthy;
            GameLog.AddSink(sink);
            using var damageSubscription = GameEvents.OnEntityDamaged.Subscribe(e =>
            {
                if (ReferenceEquals(e.Entity, lethal) || ReferenceEquals(e.Entity, survivor) || ReferenceEquals(e.Entity, building))
                { damages++; damageKeys &= e.WavePresentationKey.Equals(key); order.Add("damage:" + e.Entity.Id); }
            });
            using var healSubscription = GameEvents.OnEntityHealed.Subscribe(e =>
            { if (ReferenceEquals(e.Entity, ally)) { heals++; healKeys &= e.WavePresentationKey.Equals(key); order.Add("heal:" + ally.Id); } });
            using var deathSubscription = GameEvents.OnUnitDied.Subscribe(e =>
            { if (ReferenceEquals(e.Unit, lethal)) { deaths++; order.Add("unit-death"); snapshotBeforeRemoval &= unitStorage.ContainsKey(lethal.Id); } });
            using var buildingDeathSubscription = GameEvents.OnBuildingDied.Subscribe(e =>
            { if (ReferenceEquals(e.Building, building)) { buildingDeaths++; order.Add("building-death"); snapshotBeforeRemoval &= buildingStorage.ContainsKey(building.Id); } });
            try
            {
                typeof(UnitCombatUseCase).GetMethod("SpawnWave", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(combat, new object[] { new WaveSpawnRequest(caster, Vector3.zero, Vector3.forward, 3, 3, .5f, 10) });
                // 호출 횟수에 의존하지 않고 production contact 발행 stack에만 실패를 주입한다.
                combat.SetWaveClock(() =>
                {
                    bool publishing = false, diagnostic = false;
                    foreach (var frame in new System.Diagnostics.StackTrace().GetFrames())
                    {
                        publishing |= frame.GetMethod().Name == "PublishWaveContact";
                        diagnostic |= frame.GetMethod().Name == "get_WaveDiagnosticClock";
                    }
                    if (publishing && !diagnostic && (mode == "clock" || mode == "nonfinite"))
                    { contactClockReads++; if (mode == "clock") throw injected; return double.NaN; }
                    return now;
                });
                now = 100.02; combat.TickWaves(.001f);
                now = 100.2; combat.TickWaves(.001f);
                Require(lethal.Hp == 0 && building.Hp == 0 && survivor.Hp == 990 && ally.Hp == 910 && full.Hp == 1000,
                    mode + ": presentation failure changed authority amounts/remaining victims");
                Require(deaths == 1 && buildingDeaths == 1 && !unitStorage.ContainsKey(lethal.Id)
                    && !buildingStorage.ContainsKey(building.Id) && !grid.GetTile(building.Position).HasBuilding,
                    mode + ": lethal death/remove workflow interrupted");
                Require(damages == 3 && heals == 1 && damageKeys && healKeys && snapshotBeforeRemoval,
                    mode + ": authority event ownership/order changed");
                if (mode == "subscriber" || mode == "normal")
                {
                    Require(snapshots.Count == 4 && thrownSnapshots.Count == (mode == "subscriber" ? 4 : 0), "healthy subscriber not reached exactly once");
                    for (int i = 0; i < snapshots.Count; i++)
                    {
                        var h = snapshots[i];
                        Require((mode != "subscriber" || h.Equals(thrownSnapshots[i])) && h.Key.Equals(key) && h.Ordinal == i + 1 && h.IsValid,
                            "subscriber snapshot identity/ordinal differs");
                        Require(mode != "subscriber" || order.IndexOf("throw:" + h.Ordinal) + 1 == order.IndexOf("contact:" + h.Ordinal),
                            "subscriber registration order changed");
                        Require(h.ContactTime == (i < 3 ? 100.02 : 100.2) && h.AppliedAmount == (i == 0 || i == 3 ? 5 : 10)
                            && h.ResultingHp == (i == 0 || i == 3 ? 0 : i == 1 ? 990 : 910) && h.Heal == (i == 2),
                            "immutable actual HP/contact snapshot changed");
                        Require(h.VictimId == (i == 0 ? 1 : i == 1 ? 4 : i == 2 ? 2 : 7) && h.VictimIsUnit == (i < 3)
                            && h.Team == (i == 2 ? TeamId.Blue : TeamId.Red)
                            && h.Position.Equals(new Vector3(0, 0, i < 3 ? .1f : 1f)),
                            "immutable victim identity/team/position changed");
                    }
                    Require(order.IndexOf("damage:1") < order.IndexOf("contact:1") && order.IndexOf("contact:1") < order.IndexOf("unit-death")
                        && order.IndexOf("damage:7") < order.IndexOf("contact:4") && order.IndexOf("contact:4") < order.IndexOf("building-death"),
                        "contact must remain between HP event and lethal cleanup");
                }
                else Require(snapshots.Count == 0, mode + ": failed/no-subscriber expression counted as delivery");
                int expectedFaults = mode == "none" || mode == "normal" ? 0 : 4;
                Require(sink.Context.Count == expectedFaults && sink.Bounded, mode + ": missing/duplicate/unbounded operational faults");
                for (int i = 0; i < sink.Context.Count; i++)
                {
                    Require(sink.Context[i].Contains("Wave=" + key) && sink.Context[i].Contains("Ordinal=" + (i + 1))
                        && sink.Context[i].Contains("Victim=" + (i == 0 ? 1 : i == 1 ? 4 : i == 2 ? 2 : 7))
                        && sink.Context[i].Contains("Stage=" + (mode == "subscriber" ? "subscriber" : "clock"))
                        && sink.Context[i].Contains("SubscriberIndex=" + (mode == "subscriber" ? "0" : "-1")),
                        mode + ": fault identity/stage context lost");
                    if (mode != "nonfinite") Require(ReferenceEquals(sink.Exceptions[i], injected), "original exception object lost");
                }
                if (mode == "clock" || mode == "nonfinite") Require(contactClockReads == 4, "contact clock fault not actually injected");
                now = 100.3; combat.TickWaves(10f);
                now = 100.8; combat.TickWaves(10f);
                Require(survivor.Hp == 990 && ally.Hp == 910 && full.Hp == 1000 && damages == 3 && heals == 1
                    && deaths == 1 && buildingDeaths == 1 && sink.Context.Count == expectedFaults,
                    mode + ": presentation retry duplicated damage/heal/cleanup");

                // 새 파도에서 실제 gameplay 시계 예외가 숨겨지지 않는지도 확인한다.
                combat.SetWaveClock(() => now);
                typeof(UnitCombatUseCase).GetMethod("SpawnWave", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(combat, new object[] { new WaveSpawnRequest(caster, Vector3.zero, Vector3.forward, 3, 3, .5f, 10) });
                if (mode == "normal")
                {
                    // private 발행 경계의 음수 대조군은 HP writer를 호출하지 않는다.
                    var waves = (System.Collections.IList)typeof(UnitCombatUseCase).GetField("_activeWaves", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(combat);
                    object wave = waves[0];
                    var keyField = wave.GetType().GetField("Key");
                    var ordinalField = wave.GetType().GetField("ContactOrdinal");
                    var publish = typeof(UnitCombatUseCase).GetMethod("PublishWaveContact", BindingFlags.Instance | BindingFlags.NonPublic);
                    WaveDeliveryKey originalKey = (WaveDeliveryKey)keyField.GetValue(wave);
                    try
                    {
                        keyField.SetValue(wave, default(WaveDeliveryKey));
                        publish.Invoke(combat, new object[] { wave, survivor, Vector3.zero, 10, 0, false });
                    }
                    finally { keyField.SetValue(wave, originalKey); }
                    publish.Invoke(combat, new object[] { wave, survivor, Vector3.zero, 10, 10, false });
                    publish.Invoke(combat, new object[] { wave, full, Vector3.zero, 1000, 1000, true });
                    Require((int)ordinalField.GetValue(wave) == 0 && snapshots.Count == 4 && sink.Context.Count == 0,
                        "invalid key/nonpositive/full-HP contact consumed ordinal or emitted");
                    publish.Invoke(combat, new object[] { wave, survivor, new Vector3(float.NaN, 0, 0), 10, 0, false });
                    Require((int)ordinalField.GetValue(wave) == 1 && snapshots.Count == 4 && sink.Context.Count == 1
                        && sink.Context[0].Contains("Stage=snapshot") && sink.Context[0].Contains("Wave=" + originalKey),
                        "invalid snapshot was delivered or failed without keyed evidence");
                    Require(survivor.Hp == 990 && full.Hp == 1000 && damages == 3 && heals == 1,
                        "publication negative controls wrote gameplay HP/events");
                }
                combat.SetWaveClock(() => throw injected);
                bool gameplayThrow = false;
                try { combat.TickWaves(.01f); }
                catch (InvalidOperationException e) { gameplayThrow = ReferenceEquals(e, injected); }
                Require(gameplayThrow && survivor.Hp == 990 && ally.Hp == 910, "genuine gameplay clock was swallowed/reapplied");
            }
            finally
            {
                combat.SetWaveClock(() => now);
                combat.WaveSpawned -= onSpawn;
                combat.WaveContactConfirmed -= throwing; combat.WaveContactConfirmed -= healthy;
                GameLog.RemoveSink(sink);
                unitCounter.SetValue(null, previousUnitId); buildingCounter.SetValue(null, previousBuildingId);
            }
        }

        private static void ValidateProductionAdapter()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX/Units/vfx_torrentspirit_attack.prefab");
            bool flipped = ViewConverter.IsFlipped;
            var center = (Vector3)typeof(ViewConverter).GetField("_mapCenter", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            foreach (bool red in new[] { false, true })
            {
                GameObject instance = null;
                try
                {
                    ViewConverter.Setup(red, new Vector3(10, 0, 10));
                    instance = UnityEngine.Object.Instantiate(prefab); instance.hideFlags = HideFlags.HideAndDontSave;
                    var item = instance.AddComponent<VfxPoolItem>();
                    // Edit Mode AddComponent는 runtime Awake를 보장하지 않는다. 실제 pool의 Setup과 동일한 수명을 준비한다.
                    typeof(VfxPoolItem).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(item, null);
                    int returned = 0;
                    item.Setup(prefab, returnedItem => { returned++; returnedItem.gameObject.SetActive(false); });
                    double clock = 10.35;
                    item.PlayWave(Spawn(1), () => clock, new Vector3(0, .05f, .4f), Vector3.zero,
                        Vector3.one, new WaveVisualCalibration(1, .4f, false));
                    Vector3 expected = ViewConverter.ToView(new Vector3(0, .05f, 1.5f));
                    Require(Vector3.Distance(expected, instance.transform.position) < .001f,
                        "Blue/Red catch-up uses wrong origin/direction/clock or doubles preset offset");
                    item.Return();
                    Require(instance.transform.localScale == Vector3.one && !instance.activeSelf && returned == 1, "pool cleanup failed");
                }
                finally
                {
                    if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                    ViewConverter.Setup(flipped, center);
                }
            }
        }
        // [R4 이전 구현 — 디스크 원본 바이트를 그대로 해시했다]
        // 문제: Windows에서 git 설정 core.autocrlf=true 이면 체크아웃 시 텍스트 파일의 줄바꿈이
        //       LF("\n") → CRLF("\r\n")로 바뀌어 디스크에 저장된다. git status는 깨끗한데(git은 내부적으로
        //       LF로 비교) 디스크 바이트가 달라져 해시가 달라지고, "source VFX hash changed"로 오탐 실패했다.
        // private static string Hash(string path)
        // {
        //     using var hash = SHA256.Create();
        //     return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
        // }

        /// <summary>
        /// 줄바꿈을 LF로 정규화한 "내용 해시"(SHA256, 대문자 16진수)를 계산한다.
        /// 파일을 읽은 뒤 CRLF("\r\n")만 LF("\n")로 바꾸고 해시하므로, 같은 내용이면
        /// Windows(autocrlf=true, CRLF 체크아웃)와 Linux/macOS(LF 체크아웃)에서 같은 값이 나온다.
        /// 기대값 상수는 저장소에 커밋된 LF 원본 기준이므로 그대로 유지된다.
        /// </summary>
        /// <param name="path">프로젝트 루트 기준 에셋 경로(예: "Assets/...prefab").</param>
        /// <returns>줄바꿈 정규화 후 SHA256 해시 문자열("-" 없는 대문자 16진수 64자).</returns>
        private static string ContentHashWithNormalizedLineEndings(string path)
        {
            byte[] normalized = NormalizeCrLfToLf(File.ReadAllBytes(path));
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(normalized)).Replace("-", "");
        }

        /// <summary>
        /// 바이트 배열에서 "\r\n"(0x0D 0x0A) 쌍의 "\r"만 제거해 "\n"으로 만든다.
        /// 단독 "\r"(뒤에 "\n"이 없는 경우)은 그대로 둔다. 이유:
        /// 1) git의 autocrlf 변환은 LF↔CRLF만 바꾸고 단독 CR을 만들지 않는다.
        /// 2) 단독 CR까지 바꾸면 실제 내용 변경(예: 문자열 값 안의 CR)을 놓쳐 검사가 약해진다.
        /// 문자열 디코딩 없이 바이트 단위로 처리하므로 BOM·UTF-8 인코딩에 영향받지 않는다.
        /// (UTF-8에서 0x0D/0x0A 바이트는 다른 문자의 일부로 나타나지 않으므로 안전하다.)
        /// </summary>
        /// <param name="bytes">디스크에서 읽은 원본 바이트.</param>
        /// <returns>CRLF가 LF로 바뀐 새 바이트 배열(CRLF가 없으면 원본과 같은 내용).</returns>
        private static byte[] NormalizeCrLfToLf(byte[] bytes)
        {
            const byte CarriageReturn = 0x0D; // '\r'
            const byte LineFeed = 0x0A;       // '\n'
            var result = new byte[bytes.Length]; // 결과는 원본보다 길어질 수 없으므로 같은 크기로 시작
            int written = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                // 바로 뒤가 '\n'인 '\r'만 건너뛴다 → "\r\n"이 "\n"이 된다.
                bool isCrBeforeLf = bytes[i] == CarriageReturn
                    && i + 1 < bytes.Length && bytes[i + 1] == LineFeed;
                if (isCrBeforeLf) continue;
                result[written++] = bytes[i];
            }
            Array.Resize(ref result, written); // 실제로 쓴 길이만큼 잘라낸다
            return result;
        }
    }
}
