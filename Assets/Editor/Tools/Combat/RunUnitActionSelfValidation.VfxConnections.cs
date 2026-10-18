using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Core;
using Hexiege.Domain;
using Hexiege.Infrastructure;
using Hexiege.Presentation;
using UnityEditor;
using UnityEngine;
using UniRx;

namespace Hexiege.Editor.Combat
{
    // 새 메뉴/검증기가 아니라 기존 두 메뉴에서 호출하는 생산 경로 회귀 절이다.
    public static partial class RunUnitActionSelfValidation
    {
        private const BindingFlags VfxFields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static UnitData VfxUnit(int id, UnitType type = UnitType.LittleKnight, TeamId team = TeamId.Red)
            => new UnitData(id, type, team, new HexCoord(0, 0), 1000, 10, 1, 1);

        /// <summary>writer/current value/lease 및 Unresolved snapshot 표현의 실제 연결을 검사한다.</summary>
        public static void ValidateVfxConnections()
        {
            var units = AssetDatabase.LoadAssetAtPath<UnitEffectConfig>("Assets/_Project/Resources/Config/UnitEffectConfig.asset");
            var buildings = AssetDatabase.LoadAssetAtPath<BuildingEffectConfig>("Assets/_Project/Resources/Config/BuildingEffectConfig.asset");
            Require(units != null && buildings != null, "VFX production configs exist.");
            units.Initialize(); buildings.Initialize();
            var human = buildings.GetAttack(BuildingType.AutoTower, RaceId.Human);
            var rune = buildings.GetAttack(BuildingType.AutoTower, RaceId.Spirit);
            // 서로 같은 잘못된 에셋을 참조해도 통과하지 않도록 실제 Tank/Assault 프리팹을 기준으로 검사한다.
            // 유닛의 공유 preset은 유지하되 Human 타워의 독립 연출 설정은 합치지 않는다.
            var tankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX/Units/vfx_tank_attack.prefab");
            var assaultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX/Units/vfx_pistoleer_attack.prefab");
            var tank = units.GetAttack(UnitType.Tank);
            var cannonCart = units.GetAttack(UnitType.CannonCart);
            var assault = units.GetAttack(UnitType.Assault);
            Require(tankPrefab != null && assaultPrefab != null && tank != null && cannonCart != null && assault != null && human != null,
                "Tank, CannonCart, Assault and Human tower production presets and actual prefabs exist.");
            Require(tank.VfxPrefab == tankPrefab && cannonCart.VfxPrefab == tankPrefab,
                "Tank and CannonCart preserve the actual Tank prefab.");
            Require(assault.VfxPrefab == assaultPrefab && human.VfxPrefab == assaultPrefab,
                "Assault and Human tower share the actual Assault prefab.");
            Require(human != assault && human != tank && human != cannonCart,
                "Human tower preserves its separate preset from all three unit attack presets.");
            Require(rune != null && AssetDatabase.GetAssetPath(rune.VfxPrefab).EndsWith("vfx_runespire_attack.prefab")
                && buildings.GetAttack(BuildingType.AutoTower, RaceId.Transcendence) == null, "Race selector preserves unspecified tower.");
            var impact = units.GetImpact(UnitType.MushroomBomber);
            Require(impact != null && units.GetTracer(UnitType.MushroomBomber) != null, "Mushroom split configured.");
            foreach (var ps in impact.VfxPrefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                Require(!ps.main.loop && ps.main.stopAction == ParticleSystemStopAction.None && ps.main.duration <= .5f,
                    "Impact burst emission window and pooling contract.");
                if (ps.emission.enabled) Require(ps.main.duration + ps.main.startLifetime.constantMax <= .801f,
                    "Longest normal impact tail is initially bounded to approximately .8 seconds.");
            }
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/Units/MushroomBomber/MushroomBomber_Attack.anim");
            var markers = AnimationUtility.GetAnimationEvents(clip);
            Require(markers.Length == 1 && markers[0].functionName == "OnAttackHit" && Mathf.Abs(markers[0].time - 1f) < .00001f
                && Mathf.Abs(clip.length - 3f) < .00001f, "Mushroom canonical 30fps marker1/cycle3, unchanged cycle.");
            var stats = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>("Assets/_Project/Resources/Config/UnitStatsConfig.asset");
            bool found = false;
            foreach (var row in stats.Stats) if (row.unitType == UnitType.MushroomBomber)
            { found = row.hitFrameTimes.Length == 1 && row.hitFrameTimes[0] == 1f && row.attackCooldown == 3f; }
            Require(found, "Protected Mushroom Stats agree with canonical marker/cycle.");
            ValidateDamageCurrentState();
            ValidateDamageNetworkCurrentValue();
            ValidateDamageParticles(units.GetTimedDamage(UnitType.MushroomBomber));
            ValidateDamageParticles(units.GetTimedDamage(UnitType.InfernoSpirit));
            ValidateWriterSnapshotVfx(units, buildings);
        }

        private static void ValidateDamageCurrentState()
        {
            double now = 10d;
            var channel = new TimedDamageEffectChannel { Clock = () => now };
            var target = VfxUnit(98001);
            var first = channel.Upsert(target, 1, TimedDamageVisual.Poison, 3f);
            now = 11d;
            var refreshed = channel.Upsert(target, 2, TimedDamageVisual.Poison, 3f);
            Require(refreshed.SameOwner(first) && refreshed.Revision > first.Revision && refreshed.EndTime == 14d, "Actual refresh keeps effect owner.");
            channel.Stop(ref first, target, "OldCleanup");
            Require(channel.TryGet(target, out var current) && current.Equals(refreshed), "Stale cleanup cannot stop refreshed owner.");
            var burn = channel.Upsert(target, 3, TimedDamageVisual.Burn, 3f);
            Require(burn.SameOwner(refreshed) && burn.Visual == TimedDamageVisual.Burn, "Single Damage identity overwritten, not stacked.");
            var receiver = new TimedDamageEffectChannel();
            Require(receiver.Receive(burn, target) && !receiver.Receive(refreshed, target) && !receiver.Receive(burn, target), "Receive guards old revision and duplicate.");
            channel.Stop(ref burn, target, "Expired");
            Require(receiver.Receive(burn, target), "Terminal revision reaches recipient.");
            var invalid = burn; invalid.Visual = (TimedDamageVisual)255;
            Require(!invalid.IsValid && !receiver.Receive(invalid, target), "Invalid recipient enum cannot fall back to poison.");
            receiver.CloseAttachment(target, "LocalDeath");
            Require(!receiver.Receive(burn, target), "Retired lifetime cannot be restored.");
            var next = channel.Upsert(target, 4, TimedDamageVisual.None, 3f);
            Require(next.Instance != burn.Instance && next.Visual == TimedDamageVisual.None, "Other Damage replaces visual provenance.");
            channel.CloseVisuals("FixtureEnd"); receiver.CloseVisuals("FixtureEnd");
        }

        private static void ValidateDamageNetworkCurrentValue()
        {
            var previous = GameServicesLocator.Current;
            bool active = NetworkContext.IsNetworkActive, server = NetworkContext.IsNetworkServer;
            var root = new GameObject("DamageCurrentNetworkFixture") { hideFlags = HideFlags.HideAndDontSave };
            var network = root.AddComponent<NetworkUnit>(); var view = root.AddComponent<UnitView>();
            var combat = new UnitCombatUseCase(null, new UnitSpawnUseCase(null), null, null, new HexMetricsCoordinateMapper());
            var services = new NativeHostServices { Combat = combat, Unit = root };
            var target = VfxUnit(98008);
            var current = combat.TimedDamageEffects.Upsert(target, 1, TimedDamageVisual.Burn, 3);
            var apply = typeof(NetworkUnit).GetMethod("ApplyCurrentTimedDamage", VfxFields);
            var wire = (Unity.Netcode.NetworkVariable<NetworkTimedDamageEffectState>)typeof(NetworkUnit).GetField("_damageState", VfxFields).GetValue(network);
            try
            {
                GameServicesLocator.Register(services); NetworkContext.Set(true, true);
                typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsSpawned").SetValue(network, true);
                typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsServer").SetValue(network, true);
                ((Unity.Netcode.NetworkVariable<int>)typeof(NetworkUnit).GetField("_unitId", VfxFields).GetValue(network)).Value = target.Id;
                apply.Invoke(network, null);
                Require(!wire.Value.Value.IsValid, "Host baseline waits for actual recipient binding.");
                typeof(UnitView).GetField("_unitData", VfxFields).SetValue(view, target);
                apply.Invoke(network, null);
                Require(wire.Value.Value.Equals(current), "Actual NetworkUnit publishes current Damage baseline.");
                int reads = services.CombatReads;
                apply.Invoke(network, null); apply.Invoke(network, null);
                Require(reads == services.CombatReads, "Bound baseline has no repeated combat-service lookup.");
                // 별도 Client channel과 같은 생산 어댑터로 준비 이후 current value 복원을 확인한다.
                var client = new UnitCombatUseCase(null, new UnitSpawnUseCase(null), null, null, new HexMetricsCoordinateMapper());
                var clientServices = new NativeHostServices { Combat = client, Unit = root };
                GameServicesLocator.Register(clientServices);
                typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsServer").SetValue(network, false);
                NetworkContext.Set(false, true);
                typeof(UnitView).GetField("_unitData", VfxFields).SetValue(view, null);
                apply.Invoke(network, null);
                Require(!client.TimedDamageEffects.TryGet(target, out _), "Unready Client does not consume state.");
                typeof(UnitView).GetField("_unitData", VfxFields).SetValue(view, target);
                apply.Invoke(network, null);
                Require(client.TimedDamageEffects.TryGet(target, out var restored) && restored.Equals(current), "Late-init Client restores actual current value.");
                client.TimedDamageEffects.CloseVisuals("MatchEnd");
                var next = new UnitCombatUseCase(null, new UnitSpawnUseCase(null), null, null, new HexMetricsCoordinateMapper());
                clientServices.Combat = next;
                apply.Invoke(network, null);
                Require(!next.TimedDamageEffects.TryGet(target, out _), "Closed old match value is not restored into new service owner.");
                // 새 server current value를 Client channel과 별도인 producer에서 준비한다.
                var producer = new TimedDamageEffectChannel(); var fresh = producer.Upsert(target, 2, TimedDamageVisual.Poison, 3);
                wire.Value = new NetworkTimedDamageEffectState { Value = fresh };
                var newRecipient = new UnitCombatUseCase(null, new UnitSpawnUseCase(null), null, null, new HexMetricsCoordinateMapper());
                clientServices.Combat = newRecipient; next.TimedDamageEffects.CloseVisuals("ServiceReplace");
                apply.Invoke(network, null);
                Require(newRecipient.TimedDamageEffects.TryGet(target, out restored) && restored.Equals(fresh), "Fresh match revision restores after old owner closes.");
                newRecipient.TimedDamageEffects.CloseVisuals("FixtureEnd"); producer.CloseVisuals("FixtureEnd");
            }
            finally
            {
                typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsSpawned").SetValue(network, false);
                UnityEngine.Object.DestroyImmediate(root); GameServicesLocator.Register(previous); NetworkContext.Set(server, active);
                combat.TimedDamageEffects.CloseVisuals("FixtureEnd");
            }
        }

        private static void ValidateDamageParticles(EffectPreset preset)
        {
            Require(preset?.VfxPrefab != null, "Recipient preset exists.");
            var root = new GameObject("DamageLeaseFixture") { hideFlags = HideFlags.HideAndDontSave };
            var instance = UnityEngine.Object.Instantiate(preset.VfxPrefab, root.transform);
            var channel = new TimedDamageEffectChannel();
            try
            {
                instance.SetActive(false);
                var item = instance.AddComponent<VfxPoolItem>();
                int returned = 0; item.Setup(preset.VfxPrefab, _ => returned++);
                var state = channel.Upsert(VfxUnit(98002), 1, TimedDamageVisual.Poison, 3f);
                item.PlayTimedDamage(state, root.transform, preset, channel);
                var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                Require(systems.Length > 0, "Native recipient systems exist.");
                foreach (var ps in systems) { Require(ps.isPlaying, "Each recursive native PS starts."); ps.Emit(1); }
                ulong lease = item.DamageLease;
                state.Revision++; item.RefreshTimedDamage(lease, state);
                Require(item.DamageLease == lease, "Same owner refresh does not reacquire native playback.");
                item.StopTimedDamage(lease + 1, "OldLease");
                Require(!(bool)typeof(VfxPoolItem).GetField("_damageStopped", VfxFields).GetValue(item), "Stale lease cannot stop new owner.");
                item.StopTimedDamage(lease, "Expired"); item.Return();
                Require(returned == 0, "Live particles block early return.");
                foreach (var ps in systems)
                {
                    ps.Simulate(30f, false, false, false);
                    Require(ps.particleCount == 0, "Natural simulation removes particles before stop reassertion.");
                    // Unity Simulate는 paused로 끝난다. test-only NoClear로 실제 stopped 상태를 재현한다.
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                }
                item.Return(); item.Return();
                Require(returned == 1, "Natural drain returns lease exactly once.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); channel.CloseVisuals("FixtureEnd"); }
        }

        private static void ValidateWriterSnapshotVfx(UnitEffectConfig units, BuildingEffectConfig buildings)
        {
            bool active = NetworkContext.IsNetworkActive, server = NetworkContext.IsNetworkServer;
            var oldManager = EffectManager.Instance;
            var buildingStatsData = typeof(BuildingStats).GetField("_data", BindingFlags.Static | BindingFlags.NonPublic);
            var oldBuildingStatsData = buildingStatsData.GetValue(null);
            var root = new GameObject("WriterSnapshotFixture") { hideFlags = HideFlags.HideAndDontSave };
            HitPresentationQueue queue = null;
            IDisposable capture = null;
            try
            {
                NetworkContext.Set(false, false);
                var manager = root.AddComponent<EffectManager>();
                typeof(EffectManager).GetField("_vfxContainer", VfxFields).SetValue(manager, root.transform);
                manager.Initialize(units, buildings, null);
                // Edit Mode의 AddComponent는 일반 MonoBehaviour의 Awake 호출을 보장하지 않는다.
                // 실제 생산 등록 메서드를 실행해 큐가 임시 manager를 조회하도록 수명을 구성한다.
                typeof(EffectManager).GetMethod("Awake", VfxFields).Invoke(manager, null);
                Require(EffectManager.Instance == manager, "Writer snapshot fixture registers its actual manager lifecycle.");
                Require(units.GetTracer(UnitType.MushroomBomber)?.VfxPrefab != null,
                    "Writer snapshot fixture resolves the actual Mushroom tracer prefab before the event.");
                queue = root.AddComponent<HitPresentationQueue>(); queue.Initialize(null, null, null, null, null, null);
                double now = 100d; queue.SetAttackEffectClock(() => now);
                var spawn = new UnitSpawnUseCase(null);
                var dictionary = (Dictionary<int, UnitData>)typeof(UnitSpawnUseCase).GetField("_units", VfxFields).GetValue(spawn);
                var attacker = VfxUnit(98003, UnitType.MushroomBomber, TeamId.Blue); var target = VfxUnit(98004);
                dictionary[attacker.Id] = attacker; dictionary[target.Id] = target;
                var combat = new UnitCombatUseCase(null, spawn, new BuildingPlacementUseCase(null), null, new HexMetricsCoordinateMapper());
                combat.SetWaveClock(() => now); combat.TimedDamageEffects.Clock = () => now;
                AttackEffectPresentation snapshot = default;
                capture = GameEvents.OnAttackEffectConfirmed.Subscribe(s => snapshot = s);
                typeof(UnitCombatUseCase).GetMethod("ExecuteAttack", VfxFields).Invoke(combat, new object[] { attacker, target, true, default(AttackResultKey) });
                Require(snapshot.IsValid && !snapshot.ResultKey.IsValid && snapshot.ImpactTime == now, "Unresolved writer owns LegacyIndependent immutable snapshot, no fake C3 key.");
                var tracers = root.GetComponentsInChildren<TracerProjectile>(true);
                Require(tracers.Length == 1 && tracers[0].gameObject.activeSelf, "Actual Unresolved writer event reaches actual tracer play, no committedSchedule dependency.");
                int hp = target.Hp;
                typeof(TracerProjectile).GetMethod("Arrive", VfxFields).Invoke(tracers[0], null);
                Require(target.Hp == hp && root.GetComponentsInChildren<VfxPoolItem>(true).Length == 0, "Tracer callback only returns object, no HP/impact release authority.");
                now += AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds;
                typeof(HitPresentationQueue).GetMethod("EmitAttackEffects", VfxFields).Invoke(queue, null);
                Require(root.GetComponentsInChildren<VfxPoolItem>(true).Length == 1 && target.Hp == hp, "Same immutable snapshot emits due impact without changing HP.");
                var pending = (IList)typeof(HitPresentationQueue).GetField("_attackEffectPending", VfxFields).GetValue(queue);
                GameEvents.OnAttackEffectConfirmed.OnNext(snapshot);
                Require(pending.Count == 0, "Duplicate snapshot emits neither flight nor impact twice.");
                var bad = snapshot; bad.Visual = (AttackEffectVisual)255; bad.Sequence++;
                Require(!bad.IsValid, "Invalid attack enum rejected.");
                bad = snapshot; bad.Visual = AttackEffectVisual.Tower; bad.Race = (RaceId)255;
                Require(!bad.IsValid, "Invalid tower race rejected.");
                Require(combat.TimedDamageEffects.TryGet(target, out var effect) && effect.Visual == TimedDamageVisual.Poison
                    && effect.AttackSequence == snapshot.Sequence && effect.AttackMatch == snapshot.Match, "Actual Damage writer provenance joins exact attack snapshot.");
                combat.ApplyDamageOverTime(attacker, target, 5, 3, 1, TimedDamageVisual.Burn);
                Require(combat.TimedDamageEffects.TryGet(target, out var overwritten) && overwritten.SameOwner(effect) && overwritten.Visual == TimedDamageVisual.Burn,
                    "Actual same target Damage record resets, poison/burn never stack.");
                // 메뉴 검증에는 Bootstrap의 정적 건물 Stats 초기화가 없을 수 있다.
                // 임의 공격값 대신 실제 에셋의 Spirit AutoTower 행을 임시 주입한다.
                var buildingStats = AssetDatabase.LoadAssetAtPath<BuildingStatsConfig>("Assets/_Project/Resources/Config/BuildingStatsConfig.asset");
                Require(buildingStats != null, "Tower writer fixture resolves the actual BuildingStatsConfig.");
                bool towerStatsFound = false;
                foreach (var row in buildingStats.Stats)
                {
                    if (row.buildingType != BuildingType.AutoTower) continue;
                    BuildingStats.Initialize(new Dictionary<(BuildingType, RaceId), BuildingStats.StatValues>
                    {
                        [(BuildingType.AutoTower, RaceId.Spirit)] = new BuildingStats.StatValues
                        {
                            MaxHp = row.spiritMaxHp, GoldCost = row.spiritGoldCost,
                            AttackPower = row.spiritAttackPower, AttackCooldown = row.spiritAttackCooldown,
                            AttackRange = row.spiritAttackRange, UpgradeCost = row.upgradeCost
                        }
                    });
                    towerStatsFound = true;
                    break;
                }
                Require(towerStatsFound, "Tower writer fixture has the actual Spirit AutoTower stats row.");
                var tower = new TowerCombatUseCase(null, spawn, new HexMetricsCoordinateMapper(), null, _ => RaceId.Spirit);
                tower.SetEffectClock(() => now);
                var towerData = new BuildingData(98005, BuildingType.AutoTower, TeamId.Blue, new HexCoord(0, 0), 1000);
                var victim = VfxUnit(98006); dictionary[victim.Id] = victim;
                int before = victim.Hp;
                typeof(TowerCombatUseCase).GetMethod("ExecuteTowerAttack", VfxFields).Invoke(tower, new object[] { towerData, victim });
                Require(snapshot.Visual == AttackEffectVisual.Tower && snapshot.Race == RaceId.Spirit
                    && snapshot.AppliedDamage == before - victim.Hp && snapshot.ResultHp == victim.Hp, "Tower writer snapshot correlates actual HP result and race.");
                using (GameEvents.OnAttackEffectConfirmed.Subscribe(_ => throw new InvalidOperationException("fixture subscriber")))
                {
                    var dead = VfxUnit(98007); dead.TakeDamage(999); dictionary[dead.Id] = dead;
                    int towerPower = BuildingStats.GetAttackPower(BuildingType.AutoTower, RaceId.Spirit);
                    Require(towerPower > 0 && dead.IsAlive && DamageCalculator.ApplyDefense(towerPower, dead.Defense) >= dead.Hp,
                        "Actual tower power after defense is lethal before the throwing-subscriber death test.");
                    typeof(TowerCombatUseCase).GetMethod("ExecuteTowerAttack", VfxFields).Invoke(tower, new object[] { towerData, dead });
                    Require(!dead.IsAlive && !dictionary.ContainsKey(dead.Id), "Expression subscriber exception cannot prevent death/remove workflow.");
                }
                queue.Dispose();
                GameEvents.OnAttackEffectReceived.OnNext(snapshot);
                Require(pending.Count == 0, "Disposed lifetime suppresses late snapshot.");
                combat.TimedDamageEffects.CloseVisuals("FixtureEnd");
            }
            finally
            {
                // 사본을 재구성하지 않고 null을 포함한 원래 딕셔너리 참조 자체를 복원한다.
                buildingStatsData.SetValue(null, oldBuildingStatsData);
                capture?.Dispose(); queue?.Dispose(); UnityEngine.Object.DestroyImmediate(root);
                typeof(EffectManager).GetProperty("Instance").SetValue(null, oldManager);
                NetworkContext.Set(server, active);
            }
        }
    }
}
