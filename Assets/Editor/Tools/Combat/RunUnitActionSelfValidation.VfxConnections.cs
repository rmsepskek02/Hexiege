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
            ValidateTimedDamageWidthFit(units); // [2026-10-26 상태 이펙트 폭 맞춤]
            ValidateRecipientViewPull(units);   // [2026-10-26 D7 후속 — 상태 이펙트를 몸 앞에 그리기]
            ValidateTimedDamageFlippedFollow(units, buildings); // [2026-10-26 Plan 10 — 뒤집힌 Client 따라가기 직전 바로잡기]
            ValidateRecipientFitTable(units);
            ValidateWriterSnapshotVfx(units, buildings);
            ValidateRuneSpireBoltEndpoint(units, buildings);
            ValidateCasterOwnedChargeVfx(units, buildings);
            ValidateLegacyTracerArrivalImpact(units, buildings); // [R5 임시 — C 방식(서버 투사체) 구현 시 교체]
            ValidateChargeUnitLaunchTiming(units, buildings);    // [T-1 보강 — 2026-10-19 차지 유닛 발사 시점]
        }

        /// <summary>
        /// [T-1 보강 — 2026-10-19 차지 유닛 발사 시점] 실제 EffectManager로 두 차지 유닛(FoxMagician·InfernoSpirit)의
        /// 「차지 시작 → 발사」 화면 표현을 확인한다. (메인 파일의 Fox fixture는 EffectManager를 격리해 lease 수명만 본다.)
        ///   ① 정상: 차지 marker = charge VFX 1개·투사체 없음 / 같은 회차 두 번째 차지 = 추가 재생 없음 /
        ///      타격 marker = lease 소비만(로컬 트레이서·피격 신호 없음 — 두 유닛은 서버 투사체 유닛) · charge 재생 없음.
        ///   ② 차지 도중 취소(허가 차단): charge 즉시 정지, 이후 타격에서 투사체·charge 없음 (사용자 결정 D2).
        ///   ③ Stop이 타격보다 먼저(SourceOnly): charge 즉시 정지, 이후 타격에서 투사체·charge 없음 (D2).
        ///   ④ 싱글(lease 없음): 차지 = charge 1개, 타격 = 로컬 트레이서 없음·charge 재생 없음.
        ///   서버 발사 기록으로 그리는 투사체·취소 통보로 끄는 차지는 ServerProjectiles의 D-6이 확인한다.
        /// 발사음(SFX)은 AudioManager를 격리했으므로 여기서 관측하지 않는다
        ///   (발사음은 서버 발사 기록이 화면에 나올 때 UnitView.PlayServerProjectileLaunchCue가 낸다 — 실기 확인 항목).
        /// </summary>
        private static void ValidateChargeUnitLaunchTiming(UnitEffectConfig units, BuildingEffectConfig buildings)
        {
            Require(!UnityEngine.Application.isPlaying, "Charge-unit launch fixture must not run in a match.");
            var effectSingleton = typeof(EffectManager).GetField(
                "<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var audioSingleton = typeof(SingletonMonoBehaviour<AudioManager>).GetField(
                "<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            Require(effectSingleton != null && audioSingleton != null,
                "Charge-unit launch fixture seams (EffectManager/AudioManager singletons) must exist.");
            object oldEffect = effectSingleton.GetValue(null), oldAudio = audioSingleton.GetValue(null);
            bool oldActive = NetworkContext.IsNetworkActive, oldServer = NetworkContext.IsNetworkServer;
            var root = new GameObject("ChargeUnitLaunchFixture") { hideFlags = HideFlags.HideAndDontSave };
            var casters = new List<GameObject>();
            IDisposable localHitSubscription = null;
            int localHits = 0;
            try
            {
                var manager = root.AddComponent<EffectManager>();
                typeof(EffectManager).GetField("_vfxContainer", VfxFields).SetValue(manager, root.transform);
                manager.Initialize(units, buildings, null);
                // UnitView는 EffectManager.Instance를 쓴다 → 이 fixture 동안만 fixture manager로 바꾸고 finally에서 원래 값으로 되돌린다.
                effectSingleton.SetValue(null, manager); audioSingleton.SetValue(null, null);
                localHitSubscription = GameEvents.OnLocalAttackHit.Subscribe(_ => localHits++);
                int Active(GameObject prefab)
                {
                    int count = 0;
                    foreach (var item in root.GetComponentsInChildren<VfxPoolItem>(true))
                        if (item.SourcePrefab == prefab && item.gameObject.activeSelf) count++;
                    return count;
                }
                // 트레이서는 VfxPoolItem이 아니라 TracerProjectile 전용 풀로 관리된다(EffectManager.GetOrCreateTracer).
                //   그래서 활성 TracerProjectile을 세고, 두 유닛의 트레이서를 섞지 않도록 프리팹 이름(복제본 이름의 앞부분)으로 거른다.
                bool IsTracerOf(TracerProjectile t, GameObject prefab)
                    => t.gameObject.activeSelf && t.gameObject.name.StartsWith(prefab.name, StringComparison.Ordinal);
                int ActiveTracers(GameObject prefab)
                {
                    int count = 0;
                    foreach (var t in root.GetComponentsInChildren<TracerProjectile>(true))
                        if (IsTracerOf(t, prefab)) count++;
                    return count;
                }

                foreach (var (type, chargeTime, hitTime) in new[]
                         {
                             (UnitType.FoxMagician, 1f, 85f / 30f),
                             (UnitType.InfernoSpirit, 5f / 30f, 1.5f)
                         })
                {
                    var charge = units.GetAttack(type);
                    var tracer = units.GetTracer(type);
                    var impact = units.GetImpact(type);
                    Require(charge?.VfxPrefab != null && tracer?.VfxPrefab != null && impact?.VfxPrefab != null,
                        type + " charge, tracer and impact presets are wired for the launch fixture.");
                    var casterGo = new GameObject("ChargeUnitLaunch" + type) { hideFlags = HideFlags.HideAndDontSave };
                    casters.Add(casterGo);
                    var view = casterGo.AddComponent<UnitView>();
                    var data = new UnitData(98100 + (int)type, type, TeamId.Blue, new HexCoord(0, 0), 1000, 10, 3f, 3f);
                    typeof(UnitView).GetField("_unitData", VfxFields).SetValue(view, data);
                    // Animator가 없는 fixture라 클립 이벤트 판별(P2-1)을 계산할 수 없다 → 판별값만 직접 연다(클립 집합은 T-4가 고정).
                    typeof(UnitView).GetField("_hasAttackChargeMarker", VfxFields).SetValue(view, true);
                    typeof(UnitView).GetField("_attackChargeMarkerCacheResolved", VfxFields).SetValue(view, true);
                    void Commit(ulong revision)
                    {
                        // 서버 커밋 1회 흉내: 수락 회차 + source lease 1회분 + LegacyFallback(두 차지 유닛의 현재 모드).
                        var lease = new AttackPresentationSourceMarkerLease();
                        Require(lease.TryArm(revision, 1), "Fixture source lease must arm.");
                        typeof(UnitView).GetField("_attackPresentationSourceMarkerLease", VfxFields).SetValue(view, lease);
                        typeof(UnitView).GetField("_attackPresentationImpactMode", VfxFields)
                            .SetValue(view, AttackPresentationImpactMode.LegacyFallback);
                        typeof(UnitView).GetField("_lastAttackPresentationRevision", VfxFields).SetValue(view, revision);
                    }
                    var chargeEvent = new AnimationEvent { time = chargeTime };
                    var hitEvent = new AnimationEvent { time = hitTime };

                    // ① 정상 (Client 화면)
                    NetworkContext.Set(false, true);
                    Commit(1UL);
                    view.OnAttackCharge(chargeEvent);
                    Require(Active(charge.VfxPrefab) == 1 && ActiveTracers(tracer.VfxPrefab) == 0,
                        type + " charge marker plays exactly one charge VFX and launches nothing.");
                    view.OnAttackCharge(chargeEvent);
                    Require(Active(charge.VfxPrefab) == 1,
                        type + " second charge marker in the same revision does not replay the charge VFX.");
                    int tracersBefore = ActiveTracers(tracer.VfxPrefab);
                    // 두 차지 유닛은 서버 투사체 유닛이다. 타격 marker는 lease만 소비하고 로컬 트레이서를 쏘지 않는다 —
                    //   투사체는 서버 발사 기록을 화면 소비자(ProjectilePresentationPresenter)가 그리고,
                    //   착탄 VFX·숫자는 서버 착탄 결과로 낸다(단계 D fixture D-6·소스 게이트).
                    int hitsBefore = localHits;
                    view.OnAttackHit(hitEvent);
                    Require(ActiveTracers(tracer.VfxPrefab) == tracersBefore && Active(charge.VfxPrefab) == 1 && localHits == hitsBefore,
                        type + " launch marker (server projectile) fires no local tracer, raises no local hit and does not replay the charge VFX.");
                    Require(manager.StopCasterOwnedAttackEffects(view, "fixture-cleanup") == 1,
                        type + " the single charge VFX of the attack is owned by its caster.");

                    // ② 차지 도중 취소: 허가표가 닫히면 charge가 즉시 꺼지고 타격은 아무것도 쏘지 않는다.
                    Commit(2UL);
                    view.OnAttackCharge(chargeEvent);
                    Require(Active(charge.VfxPrefab) == 1, type + " charge plays for the cancel case.");
                    tracersBefore = ActiveTracers(tracer.VfxPrefab); hitsBefore = localHits;
                    view.SuppressPendingAttackImpactPresentation();
                    Require(Active(charge.VfxPrefab) == 0,
                        type + " cancelling the attack mid-charge stops the charge VFX immediately (D2).");
                    view.OnAttackHit(hitEvent);
                    Require(ActiveTracers(tracer.VfxPrefab) == tracersBefore && Active(charge.VfxPrefab) == 0 && localHits == hitsBefore,
                        type + " a cancelled attack launches no tracer and replays no charge (D2).");

                    // ③ Stop이 타격보다 먼저(SourceOnly): charge 즉시 정지, 투사체 없음.
                    Commit(3UL);
                    view.OnAttackCharge(chargeEvent);
                    Require(Active(charge.VfxPrefab) == 1, type + " charge plays for the stop case.");
                    tracersBefore = ActiveTracers(tracer.VfxPrefab); hitsBefore = localHits;
                    view.StopCombatAnimation();
                    Require(Active(charge.VfxPrefab) == 0,
                        type + " a Stop that precedes the launch stops the charge VFX immediately (D2).");
                    view.OnAttackHit(hitEvent);
                    Require(ActiveTracers(tracer.VfxPrefab) == tracersBefore && Active(charge.VfxPrefab) == 0 && localHits == hitsBefore,
                        type + " a SourceOnly launch of a charge unit launches no tracer and replays no charge (D2).");

                    // ④ 싱글(lease 없음)
                    NetworkContext.Set(false, false);
                    view.OnAttackCharge(chargeEvent);
                    Require(Active(charge.VfxPrefab) == 1 && ActiveTracers(tracer.VfxPrefab) == tracersBefore,
                        type + " SOLO charge marker plays one charge VFX and launches nothing.");
                    hitsBefore = localHits;
                    view.OnAttackHit(hitEvent);
                    Require(ActiveTracers(tracer.VfxPrefab) == tracersBefore && Active(charge.VfxPrefab) == 1 && localHits == hitsBefore,
                        type + " SOLO launch marker (server projectile) fires no local tracer and does not replay the charge VFX.");
                    manager.StopCasterOwnedAttackEffects(view, "fixture-cleanup");
                }
            }
            finally
            {
                localHitSubscription?.Dispose();
                effectSingleton.SetValue(null, oldEffect); audioSingleton.SetValue(null, oldAudio);
                NetworkContext.Set(oldServer, oldActive);
                foreach (var go in casters) if (go != null) UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// [2026-10-13 R2-1] 기 모으기(charge) 공격 이펙트가 시전자 사망·제거 시 즉시 멈추는지 확인한다.
        /// (a) 설정 계약: FoxMagician·InfernoSpirit 공격 프리셋에 시전자 귀속이 켜져 있고, 켜진 프리셋은 전부 charge 프리팹이며,
        ///     사망·트레이서·착탄·지속(화상/독)·힐 프리셋에는 켜져 있지 않다.
        /// (b) 실제 EffectManager 경로: 시전자별 즉시 정지(남은 파티클 삭제 + 풀 반환), 다른 시전자·소유자 없는 재생·
        ///     사망 이펙트는 영향 없음, 매 프레임 안전망(사망/파괴 감지), 풀 재사용 시 옛 귀속이 남지 않음.
        /// EditMode에서는 Update가 자동으로 돌지 않으므로 안전망 메서드를 리플렉션으로 직접 호출한다.
        /// </summary>
        private static void ValidateCasterOwnedChargeVfx(UnitEffectConfig units, BuildingEffectConfig buildings)
        {
            // (a) 설정 계약
            var fox = units.GetAttack(UnitType.FoxMagician);
            var inferno = units.GetAttack(UnitType.InfernoSpirit);
            Require(fox?.VfxPrefab != null && inferno?.VfxPrefab != null
                && AssetDatabase.GetAssetPath(fox.VfxPrefab).EndsWith("vfx_foxmagician_charge.prefab")
                && AssetDatabase.GetAssetPath(inferno.VfxPrefab).EndsWith("vfx_infernospirit_charge.prefab"),
                "FoxMagician/InfernoSpirit attack presets use the actual charge prefabs.");
            Require(fox.StopWhenCasterDies && inferno.StopWhenCasterDies,
                "FoxMagician and InfernoSpirit charge presets are caster-owned (stop when the caster dies).");
            foreach (UnitType type in Enum.GetValues(typeof(UnitType)))
            {
                var attack = units.GetAttack(type);
                if (attack != null && attack.StopWhenCasterDies)
                    Require(attack.VfxPrefab != null && AssetDatabase.GetAssetPath(attack.VfxPrefab).Contains("_charge"),
                        "Only charge-type attack prefabs are caster-owned: " + type);
                foreach (var detached in new[] { units.GetDeath(type), units.GetTracer(type), units.GetImpact(type),
                    units.GetTimedDamage(type), units.GetHeal(type), units.GetHit(type) })
                    Require(detached == null || !detached.StopWhenCasterDies,
                        "Death/tracer/impact/timed/heal/hit presets are never caster-owned: " + type);
            }

            // (b) 실제 EffectManager 재생·정지 경로
            var root = new GameObject("CasterOwnedChargeFixture") { hideFlags = HideFlags.HideAndDontSave };
            var foxGo = new GameObject("CasterOwnedFox") { hideFlags = HideFlags.HideAndDontSave };
            var infernoGo = new GameObject("CasterOwnedInferno") { hideFlags = HideFlags.HideAndDontSave };
            var removedGo = new GameObject("CasterOwnedRemoved") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var manager = root.AddComponent<EffectManager>();
                typeof(EffectManager).GetField("_vfxContainer", VfxFields).SetValue(manager, root.transform);
                manager.Initialize(units, buildings, null);
                var poll = typeof(VfxPoolItem).GetMethod("PollCasterOwnership", VfxFields);
                Require(poll != null, "Caster-owned per-frame safety-net method exists for the lifecycle fixture.");
                UnitView Caster(GameObject go, UnitData data)
                {
                    var view = go.AddComponent<UnitView>();
                    typeof(UnitView).GetField("_unitData", VfxFields).SetValue(view, data);
                    return view;
                }
                var foxData = VfxUnit(98020, UnitType.FoxMagician, TeamId.Blue);
                var infernoData = VfxUnit(98021, UnitType.InfernoSpirit, TeamId.Blue);
                var foxView = Caster(foxGo, foxData);
                var infernoView = Caster(infernoGo, infernoData);
                // 방금 재생해 활성 상태인 아이템 중 아직 고르지 않은 것을 원본 프리팹으로 찾는다.
                var picked = new List<VfxPoolItem>();
                VfxPoolItem Latest(GameObject prefab)
                {
                    foreach (var candidate in root.GetComponentsInChildren<VfxPoolItem>(true))
                        if (candidate.SourcePrefab == prefab && candidate.gameObject.activeSelf && !picked.Contains(candidate))
                        { picked.Add(candidate); return candidate; }
                    return null;
                }
                void FillParticles(VfxPoolItem item)
                { foreach (var ps in item.GetComponentsInChildren<ParticleSystem>(true)) ps.Emit(3); }
                bool Cleared(VfxPoolItem item)
                {
                    foreach (var ps in item.GetComponentsInChildren<ParticleSystem>(true)) if (ps.particleCount != 0) return false;
                    return true;
                }

                manager.PlayUnitAttack(UnitType.FoxMagician, Vector3.zero, Quaternion.identity, foxView);
                var foxItem = Latest(fox.VfxPrefab);
                manager.PlayUnitAttack(UnitType.InfernoSpirit, Vector3.one, Quaternion.identity, infernoView);
                var infernoItem = Latest(inferno.VfxPrefab);
                // 소유자 없이 부른 재생(에디터 테스트 스포너 경로)은 예전처럼 귀속되지 않는다.
                manager.PlayUnitAttack(UnitType.FoxMagician, Vector3.zero, Quaternion.identity);
                var unownedFox = Latest(fox.VfxPrefab);
                var deathPreset = units.GetDeath(UnitType.FoxMagician);
                Require(deathPreset?.VfxPrefab != null, "FoxMagician death preset exists for the detached-effect check.");
                manager.PlayUnitDeath(UnitType.FoxMagician, Vector3.zero);
                var deathItem = Latest(deathPreset.VfxPrefab);
                Require(foxItem != null && foxItem.CasterOwned && infernoItem != null && infernoItem.CasterOwned,
                    "Charge playback is owned by the caster that played it.");
                Require(unownedFox != null && !unownedFox.CasterOwned && deathItem != null && !deathItem.CasterOwned,
                    "Caster-less charge and death VFX are not caster-owned.");
                FillParticles(foxItem); // 남은 파티클까지 지우는지 보려고 정지 전에 입자를 채워 둔다.

                // 즉시 정지: 해당 시전자의 charge만, 남은 파티클까지 지우고 풀로 반환한다.
                int stopped = manager.StopCasterOwnedAttackEffects(foxView, "CasterDeath");
                Require(stopped == 1 && !foxItem.gameObject.activeSelf && !foxItem.CasterOwned && Cleared(foxItem),
                    "Caster death immediately stops its charge, clears particles and returns it to the pool.");
                Require(infernoItem.gameObject.activeSelf && infernoItem.CasterOwned,
                    "Another caster's charge keeps playing.");
                Require(unownedFox.gameObject.activeSelf && deathItem.gameObject.activeSelf,
                    "Caster-less charge and the death VFX are not affected by the caster stop.");
                Require(manager.StopCasterOwnedAttackEffects(foxView, "CasterRemoved") == 0,
                    "Repeated stop (death then removal) is a no-op.");

                // 안전망: 살아 있는 시전자는 유지, 시전자 사망을 매 프레임 확인에서 감지하면 스스로 정지한다.
                Require(!(bool)poll.Invoke(infernoItem, null) && infernoItem.CasterOwned, "Live caster keeps its charge.");
                infernoData.TakeDamage(99999);
                Require((bool)poll.Invoke(infernoItem, null) && !infernoItem.gameObject.activeSelf && !infernoItem.CasterOwned,
                    "Per-frame safety net stops the charge of a dead caster.");

                // 풀 재사용 + 시전자 GameObject 파괴(디스폰) 감지. 같은 아이템이 재사용돼도 옛 귀속은 남지 않는다.
                var removedView = Caster(removedGo, VfxUnit(98022, UnitType.InfernoSpirit, TeamId.Red));
                picked.Remove(infernoItem);
                manager.PlayUnitAttack(UnitType.InfernoSpirit, Vector3.zero, Quaternion.identity, removedView);
                var reusedItem = Latest(inferno.VfxPrefab);
                Require(ReferenceEquals(reusedItem, infernoItem) && reusedItem.CasterOwned
                    && !reusedItem.IsOwnedByCaster(infernoView) && reusedItem.IsOwnedByCaster(removedView),
                    "Pooled charge reuse carries only the new caster ownership.");
                UnityEngine.Object.DestroyImmediate(removedGo);
                Require((bool)poll.Invoke(reusedItem, null) && !reusedItem.gameObject.activeSelf,
                    "Per-frame safety net stops the charge when the caster object is destroyed (despawn).");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(foxGo);
                UnityEngine.Object.DestroyImmediate(infernoGo);
                if (removedGo != null) UnityEngine.Object.DestroyImmediate(removedGo);
            }
        }

        /// <summary>
        /// [R5 임시 — C 방식(서버 투사체) 구현 시 교체]
        /// (a) 설정: StreamSpirit/Tank/CannonCart의 착탄 이펙트는 공격자 기준 impact 슬롯에 있고,
        ///     "맞은 유닛 타입" 기준인 hit 슬롯은 비어 있다. StreamSpirit 트레이서 프리팹 루트에는
        ///     TracerProjectile이 미리 붙어 있어 프리팹의 느린 속도(_speed)가 실제로 쓰인다.
        ///     (이 프리셋들은 서버 투사체 화면 — ProjectilePresentationPresenter — 이 그대로 꺼내 쓴다.)
        /// (b) 실제 EffectManager의 로컬 트레이서(PlayTracer) 일반 계약: 트레이서를 날리고 도착하면 콜백을 1회 실행하며
        ///     착탄 이펙트는 만들지 않는다. 트레이서가 없는 타입은 콜백을 즉시 실행한다(폴백).
        ///     [P5 정리] 2026-10-19 R5 임시 동작(도착 순간 착탄 이펙트 — playImpactOnArrive) 검사는 그 코드와 함께 삭제했다.
        /// EditMode에서는 Update가 돌지 않으므로 도착(Arrive)을 리플렉션으로 직접 호출한다.
        /// </summary>
        private static void ValidateLegacyTracerArrivalImpact(UnitEffectConfig units, BuildingEffectConfig buildings)
        {
            // (a) 설정 계약
            var streamImpact = AssetDatabase.LoadAssetAtPath<EffectPreset>(
                "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_StreamSpirit_Hit.asset");
            var cannonImpact = AssetDatabase.LoadAssetAtPath<EffectPreset>(
                "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_Cannon_Impact.asset");
            Require(streamImpact?.VfxPrefab != null && cannonImpact?.VfxPrefab != null
                && AssetDatabase.GetAssetPath(streamImpact.VfxPrefab).EndsWith("vfx_streamspirit_impact.prefab")
                && AssetDatabase.GetAssetPath(cannonImpact.VfxPrefab).EndsWith("vfx_cannon_impact.prefab"),
                "[R5] StreamSpirit/Cannon impact presets point to the actual impact prefabs.");
            Require(units.GetImpact(UnitType.StreamSpirit) == streamImpact && units.GetHit(UnitType.StreamSpirit) == null,
                "[R5] StreamSpirit splash is attacker-keyed (impact slot), not victim-keyed (hit slot).");
            // [2026-10-20 서버 권위 투사체 단계 B] Tank·CannonCart는 이제 R5 임시 경로가 아니라 서버 투사체 경로다.
            //   같은 설정 계약(착탄 = 공격자 기준 impact 슬롯, hit 슬롯 비움)에 트레이서 프리셋 연결까지 함께 확인한다 —
            //   서버 투사체 화면 소비자가 이 두 프리셋(EffectPreset_Cannon_Tracer · EffectPreset_Cannon_Impact)을 그린다.
            var cannonTracer = AssetDatabase.LoadAssetAtPath<EffectPreset>(
                "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_Cannon_Tracer.asset");
            Require(cannonTracer?.VfxPrefab != null
                && units.GetImpact(UnitType.Tank) == cannonImpact && units.GetHit(UnitType.Tank) == null
                && units.GetImpact(UnitType.CannonCart) == cannonImpact && units.GetHit(UnitType.CannonCart) == null
                && units.GetTracer(UnitType.Tank) == cannonTracer && units.GetTracer(UnitType.CannonCart) == cannonTracer,
                "[SERVER-PROJECTILE] Tank/CannonCart draw EffectPreset_Cannon_Tracer in flight and EffectPreset_Cannon_Impact (attacker-keyed impact slot) on impact; victim-keyed hit slot stays empty.");
            var streamTracer = units.GetTracer(UnitType.StreamSpirit);
            Require(streamTracer?.VfxPrefab != null, "[R5] StreamSpirit interim tracer preset is wired.");
            var tracerComponent = streamTracer.VfxPrefab.GetComponent<TracerProjectile>();
            Require(tracerComponent != null,
                "[R5] StreamSpirit tracer prefab root carries TracerProjectile (EffectManager reuses it instead of adding a default 14 speed).");
            float speed = new SerializedObject(tracerComponent).FindProperty("_speed").floatValue;
            Require(speed > 0f && speed < 14f,
                "[R5] StreamSpirit tracer prefab speed is positive and slower than the default so the flight is visible: " + speed);

            // (b) 실제 EffectManager 경로
            var root = new GameObject("LegacyTracerArrivalImpactFixture") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var manager = root.AddComponent<EffectManager>();
                typeof(EffectManager).GetField("_vfxContainer", VfxFields).SetValue(manager, root.transform);
                manager.Initialize(units, buildings, null);
                var arrive = typeof(TracerProjectile).GetMethod("Arrive", VfxFields);
                Require(arrive != null, "[R5] TracerProjectile arrival method exists for the fixture.");
                // 지금 활성 상태인, 특정 프리팹에서 만든 착탄 이펙트 개수.
                int ActiveImpacts(GameObject prefab)
                {
                    int count = 0;
                    foreach (var item in root.GetComponentsInChildren<VfxPoolItem>(true))
                        if (item.SourcePrefab == prefab && item.gameObject.activeSelf) count++;
                    return count;
                }
                // 지금 비행 중(활성)인 트레이서 1개를 찾는다.
                TracerProjectile ActiveTracer()
                {
                    foreach (var t in root.GetComponentsInChildren<TracerProjectile>(true))
                        if (t.gameObject.activeSelf) return t;
                    return null;
                }

                Vector3 start = new Vector3(0f, 0.5f, 0f);
                Vector3 target = new Vector3(2f, 0f, 0f);

                // ② 로컬 트레이서 호출은 트레이서를 날리고, 도착하면 콜백을 1회 실행하며, 착탄 이펙트는 만들지 않는다.
                int plainCallbacks = 0;
                manager.PlayTracer(UnitType.StreamSpirit, start, target, () => plainCallbacks++);
                TracerProjectile tracer = ActiveTracer();
                Require(tracer != null, "[R5] Plain tracer call launches a tracer.");
                arrive.Invoke(tracer, null);
                Require(plainCallbacks == 1 && ActiveImpacts(streamImpact.VfxPrefab) == 0,
                    "[R5] A tracer call without the arrival-impact flag keeps the old behavior (no splash).");

                // ③ 트레이서가 없는 타입: 콜백은 즉시 실행, 착탄 이펙트는 재생하지 않는다(폴백 유지).
                //   트레이서 프리셋이 없는 비투사체 유닛 Pistoleer로 폴백 계약을 확인한다.
                if (units.GetTracer(UnitType.Pistoleer)?.VfxPrefab == null)
                {
                    int fallbackCallbacks = 0;
                    var pistoleerImpact = units.GetImpact(UnitType.Pistoleer);
                    manager.PlayTracer(UnitType.Pistoleer, start, target, () => fallbackCallbacks++);
                    Require(fallbackCallbacks == 1 && ActiveTracer() == null
                        && (pistoleerImpact?.VfxPrefab == null || ActiveImpacts(pistoleerImpact.VfxPrefab) == 0),
                        "[R5] No tracer prefab: callback runs immediately and no arrival impact is played.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>두 벡터가 부동소수 오차 안에서 같은지(검증 전용).</summary>
        private static bool VfxApprox(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-8f;

        /// <summary>
        /// [2026-10-13 C] 상태 이펙트 크기 표의 계산 규칙과 실제 에셋 표의 상태를 확인한다.
        /// 표가 비어 있거나 일부 유닛이 빠진 것은 "실패"가 아니라 경고다 — 빠진 유닛은 배율 1(예전 크기)로
        /// 안전하게 재생되고, 표는 사용자가 측정 메뉴를 실행해야 채워지기 때문이다(K-11).
        /// </summary>
        private static void ValidateRecipientFitTable(UnitEffectConfig units)
        {
            // (1) 계산 규칙: 원본 에셋을 건드리지 않도록 임시 config 인스턴스에 표를 넣어 확인한다.
            var probe = ScriptableObject.CreateInstance<UnitEffectConfig>();
            probe.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var rows = new List<UnitRecipientFitEntry>
                {
                    new UnitRecipientFitEntry { unitType = UnitType.LittleKnight, height = UnitEffectConfig.RecipientReferenceHeight },
                    new UnitRecipientFitEntry { unitType = UnitType.Tank, height = 2f, footOffset = new Vector3(0f, 0.1f, 0f), radius = 0.4f },
                    // [2026-10-26 폭 맞춤] 키는 정상이고 반경만 잘못된 줄 → 배율은 그대로 쓰고 반경만 0(폭 맞춤 없음).
                    new UnitRecipientFitEntry { unitType = UnitType.LionKnight, height = 0.5f, radius = float.NaN },
                    new UnitRecipientFitEntry { unitType = UnitType.BloomFairy, height = 0.5f, manualScale = 1.5f },
                    new UnitRecipientFitEntry { unitType = UnitType.Sniper, height = float.NaN },
                    new UnitRecipientFitEntry { unitType = UnitType.Assault, height = 100f },
                };
                typeof(UnitEffectConfig).GetField("_recipientFits", VfxFields).SetValue(probe, rows);
                probe.Initialize();
                Require(probe.TryGetRecipientFit(UnitType.LittleKnight, out var reference)
                    && Mathf.Approximately(reference.UniformScale, 1f),
                    "Reference-height recipient keeps fit scale 1 (approved burn shape preserved).");
                Require(probe.TryGetRecipientFit(UnitType.Tank, out var tall) && Mathf.Approximately(tall.UniformScale, 2f)
                    && VfxApprox(tall.SafeFootOffset, new Vector3(0f, 0.1f, 0f)),
                    "Default scale is height ÷ reference height with foot offset.");
                Require(Mathf.Approximately(tall.SafeRadius, 0.4f) && Mathf.Approximately(reference.SafeRadius, 0f),
                    "Recipient fit carries the table radius (missing radius = 0) without changing the scale.");
                Require(probe.TryGetRecipientFit(UnitType.LionKnight, out var badRadius)
                    && Mathf.Approximately(badRadius.UniformScale, 0.5f) && Mathf.Approximately(badRadius.SafeRadius, 0f),
                    "Invalid radius keeps the height scale and disables only the width fit.");
                Require(probe.TryGetRecipientFit(UnitType.BloomFairy, out var manual) && Mathf.Approximately(manual.UniformScale, 1.5f),
                    "Positive manualScale overrides the height ratio.");
                Require(!probe.TryGetRecipientFit(UnitType.Sniper, out var invalid) && Mathf.Approximately(invalid.UniformScale, 1f),
                    "Invalid height row falls back to identity scale 1.");
                Require(probe.TryGetRecipientFit(UnitType.Assault, out var huge)
                    && Mathf.Approximately(huge.UniformScale, RecipientFit.MaxScale),
                    "Fit scale is clamped to the safe maximum.");
                Require(!probe.TryGetRecipientFit(UnitType.Pistoleer, out var missing) && Mathf.Approximately(missing.UniformScale, 1f)
                    && missing.SafeFootOffset == Vector3.zero,
                    "Missing unit type falls back to identity (scale 1, no offset).");
                Require(Mathf.Approximately(default(RecipientFit).UniformScale, 1f), "default(RecipientFit) is identity.");

                // [2026-10-13 R2-2] 타워 번개 가슴 높이 = 크기 표 키(균등 배율 × 기준 키) × 비율(기본 0.6), 표에 없으면 0.6m.
                Require(Mathf.Approximately(probe.ChestHeightRatio, UnitEffectConfig.DefaultChestHeightRatio),
                    "Fresh config uses the default chest height ratio.");
                Require(Mathf.Approximately(probe.GetChestHeight(UnitType.Tank), 2f * UnitEffectConfig.RecipientReferenceHeight
                    * UnitEffectConfig.DefaultChestHeightRatio), "Chest height = recipient fit height × ratio.");
                Require(Mathf.Approximately(probe.GetChestHeight(UnitType.LittleKnight), UnitEffectConfig.RecipientReferenceHeight
                    * UnitEffectConfig.DefaultChestHeightRatio), "Reference-height recipient chest height is 0.6 × reference height.");
                Require(Mathf.Approximately(probe.GetChestHeight(UnitType.Pistoleer), UnitEffectConfig.FallbackChestHeight)
                    && Mathf.Approximately(probe.GetChestHeight(UnitType.Sniper), UnitEffectConfig.FallbackChestHeight),
                    "Missing/invalid fit row uses the 0.6 world-unit fallback chest height.");
                typeof(UnitEffectConfig).GetField("_chestHeightRatio", VfxFields).SetValue(probe, float.NaN);
                Require(Mathf.Approximately(probe.ChestHeightRatio, UnitEffectConfig.DefaultChestHeightRatio),
                    "Invalid Inspector chest ratio falls back to the default ratio.");
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
            Require(units.ChestHeightRatio >= 0f, "Actual UnitEffectConfig chest height ratio is usable.");

            // (2) 실제 에셋 표: 들어 있는 줄은 쓸 수 있는 값이어야 하고(실패), 빠진 유닛은 경고만 한다.
            var missingTypes = new List<string>();
            foreach (UnitType type in Enum.GetValues(typeof(UnitType)))
            {
                if (!units.HasRecipientFitEntry(type)) { missingTypes.Add(type.ToString()); continue; }
                Require(units.TryGetRecipientFit(type, out var fit) && fit.UniformScale >= RecipientFit.MinScale
                    && fit.UniformScale <= RecipientFit.MaxScale && fit.SafeFootOffset == fit.FootOffset,
                    "Recipient fit row has a usable height/manual scale and finite foot offset: " + type);
            }
            if (missingTypes.Count > 0)
                GameLog.Dev.Warn("Combat", "RunUnitActionSelfValidation",
                    "상태 이펙트 크기 표에 없는 유닛이 있다 — 해당 유닛은 배율 1(예전 크기)로 재생된다. 메뉴 Hexiege/Effects/Measure Unit Recipient Fit 실행 필요",
                    "Missing=" + missingTypes.Count + ", Types=" + string.Join("|", missingTypes));

            // (3) [2026-10-13 스케일 버그] 실제 에셋 표에 비정상 크기(키 > 1.2m / 반경 > 0.43m / 키 < 0.1m)가
            //     남아 있으면 경고한다. 실패(Require)로 두지 않는 이유: 표는 사용자가 측정 메뉴를 다시 돌려야 고쳐지므로,
            //     수정 직후 아직 재측정 전인 상태에서 검증 전체를 막으면 안 된다. 대신 "다시 돌려야 한다"를 눈에 띄게 알린다.
            WarnImplausibleRecipientFitRows(units);

            // (4) 측정 도구 자체 회귀 검사: 실제 프리팹을 같은 코드로 재서 크기가 말이 되는지 확인한다.
            ValidateRecipientFitMeasurement();
        }

        /// <summary>
        /// 실제 UnitEffectConfig 표의 측정값(height/radius) 중 비정상 범위인 줄을 모아 경고 1줄로 남긴다.
        /// manualScale 과 무관하게 "측정값" 자체를 본다(측정 도구 회귀를 알리는 것이 목적).
        /// </summary>
        private static void WarnImplausibleRecipientFitRows(UnitEffectConfig units)
        {
            // 원시 표는 private 필드라 읽기 전용 SerializedObject 로 본다(값을 바꾸거나 저장하지 않는다).
            SerializedProperty table = new SerializedObject(units).FindProperty("_recipientFits");
            if (table == null || !table.isArray) return;
            var suspicious = new List<string>();
            for (int i = 0; i < table.arraySize; i++)
            {
                SerializedProperty row = table.GetArrayElementAtIndex(i);
                float height = row.FindPropertyRelative("height").floatValue;
                float radius = row.FindPropertyRelative("radius").floatValue;
                if (height > Hexiege.EditorTools.UnitRecipientFitMeasureTool.SuspiciousMaxHeight
                    || radius > Hexiege.EditorTools.UnitRecipientFitMeasureTool.SuspiciousMaxRadius
                    || (height > 0f && height < Hexiege.EditorTools.UnitRecipientFitMeasureTool.SuspiciousMinHeight))
                    suspicious.Add(((UnitType)row.FindPropertyRelative("unitType").intValue).ToString());
            }
            if (suspicious.Count > 0)
                GameLog.Dev.Warn("Combat", "RunUnitActionSelfValidation",
                    "상태 이펙트 크기 표에 비정상 크기 줄이 있다 — 측정 도구 수정 후 메뉴 Hexiege/Effects/Measure Unit Recipient Fit 재실행 필요",
                    "Suspicious=" + suspicious.Count + ", Types=" + string.Join("|", suspicious));
        }

        /// <summary>
        /// [2026-10-13] 측정 도구가 스킨드 유닛의 중간 스케일(Unit_xxx_Mesh 0.2~0.3)을 반영하는지 실제 프리팹으로 확인한다.
        /// - Pistoleer(스킨드 경로): 화면 키 ≈ 0.34m. 0.2~0.6m 범위 밖이면 실패(예전 버그는 1.71m).
        /// - Tank(MeshRenderer 경로) 가 Pistoleer 보다 커야 한다 → 두 경로가 같은 좌표계로 재는지 교차 확인.
        /// Play 모드에서는 프리팹 격리 로드를 피하려고 건너뛴다(경고 없이 정보 로그만).
        /// </summary>
        private static void ValidateRecipientFitMeasurement()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                GameLog.Dev.Info("Combat", "RunUnitActionSelfValidation",
                    "Play 모드라 유닛 몸 크기 측정 검사를 건너뛴다", "Check=RecipientFitMeasurement");
                return;
            }
            var pistoleerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Units/Human/Unit_Pistoleer_Blue.prefab");
            var tankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Units/Human/Unit_Tank_Blue.prefab");
            Require(pistoleerPrefab != null && tankPrefab != null, "Pistoleer/Tank Blue production prefabs exist for recipient fit measurement.");

            var pistoleer = Hexiege.EditorTools.UnitRecipientFitMeasureTool.Measure(pistoleerPrefab);
            var tank = Hexiege.EditorTools.UnitRecipientFitMeasureTool.Measure(tankPrefab);
            Require(pistoleer.Ok && tank.Ok, "Recipient fit measurement succeeds for Pistoleer and Tank: "
                + pistoleer.Failure + "/" + tank.Failure);
            Require(pistoleer.Height >= 0.2f && pistoleer.Height <= 0.6f,
                "Skinned recipient height includes intermediate mesh scale (Pistoleer expected about 0.34m, got "
                + pistoleer.Height.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ").");
            Require(!Hexiege.EditorTools.UnitRecipientFitMeasureTool.IsImplausible(pistoleer, out string reason),
                "Pistoleer measured size is within unit sanity bounds: " + reason);
            Require(tank.Height > pistoleer.Height,
                "Skinned and MeshRenderer measurement paths share one space (Tank taller than Pistoleer).");
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
                Vector3 authoredScale = instance.transform.localScale;
                var item = instance.AddComponent<VfxPoolItem>();
                int returned = 0; item.Setup(preset.VfxPrefab, _ => returned++);
                var state = channel.Upsert(VfxUnit(98002), 1, TimedDamageVisual.Poison, 3f);
                item.PlayTimedDamage(state, root.transform, preset, channel);
                // [C] 크기 맞춤 없이 부르면 예전과 같은 크기(원본 × 프리셋 배율).
                Require(VfxApprox(instance.transform.localScale, Vector3.Scale(authoredScale, preset.ScaleMultiplier)),
                    "Timed damage without recipient fit keeps authored × preset scale.");
                var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                Require(systems.Length > 0, "Native recipient systems exist.");
                foreach (var ps in systems) { Require(ps.isPlaying, "Each recursive native PS starts."); ps.Emit(1); }
                ulong lease = item.DamageLease;
                state.Revision++; item.RefreshTimedDamage(lease, state);
                Require(item.DamageLease == lease, "Same owner refresh does not reacquire native playback.");
                item.StopTimedDamage(lease + 1, "OldLease");
                Require(!(bool)typeof(VfxPoolItem).GetField("_damageStopped", VfxFields).GetValue(item), "Stale lease cannot stop new owner.");
                // 실패 메시지에 어느 프리셋·프리팹인지 붙인다(이 함수는 독·화상 두 프리셋으로 불린다 — ValidateVfxConnections 70·71줄).
                string label = preset.name + "/" + preset.VfxPrefab.name;
                item.StopTimedDamage(lease, "Expired"); item.Return();
                Require(returned == 0, "Live particles block early return: " + label);
                // [2026-10-27 옛 줄 — 기록용] Prewarm이 켜진 화상 v2에서 "다 사라진 뒤 반환"이 재현되지 않아 아래 도우미로 바꿨다.
                // foreach (var ps in systems)
                // {
                //     ps.Simulate(30f, false, false, false);
                //     Require(ps.particleCount == 0, "Natural simulation removes particles before stop reassertion.");
                //     // Unity Simulate는 paused로 끝난다. test-only NoClear로 실제 stopped 상태를 재현한다.
                //     ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                // }
                DrainTimedDamageFixtureSystems(systems, label);
                item.Return(); item.Return();
                Require(returned == 1, "Natural drain returns lease exactly once: " + label);

                // [C] 같은 아이템 재사용 + 크기 맞춤(배율 2, 발밑 보정): 재생 배율·위치, refresh 유지, 반환 복원.
                var pose = new GameObject("DamageFitPose") { hideFlags = HideFlags.HideAndDontSave };
                pose.transform.SetParent(root.transform, false);
                pose.transform.SetPositionAndRotation(new Vector3(3f, 0.5f, -2f), Quaternion.Euler(0f, 70f, 0f));
                var fit = new RecipientFit(2f, new Vector3(0.05f, 0.02f, -0.1f));
                var fitState = channel.Upsert(VfxUnit(98009), 1, TimedDamageVisual.Burn, 3f);
                item.PlayTimedDamage(fitState, pose.transform, preset, channel, fit);
                Vector3 fittedScale = Vector3.Scale(authoredScale, preset.ScaleMultiplier) * 2f;
                Require(VfxApprox(instance.transform.localScale, fittedScale)
                    && VfxApprox(instance.transform.position, pose.transform.position
                        + pose.transform.rotation * (preset.LocalPositionOffset + fit.FootOffset)),
                    "Timed damage recipient fit applies uniform scale and foot offset at Play.");
                ulong fitLease = item.DamageLease;
                fitState.Revision++; item.RefreshTimedDamage(fitLease, fitState);
                pose.transform.position += new Vector3(1f, 0f, 1f); item.FollowTimedDamage(fitLease, pose.transform);
                Require(item.DamageLease == fitLease && VfxApprox(instance.transform.localScale, fittedScale),
                    "Same-owner refresh/follow keeps the Play-time fit scale without restart.");
                item.StopTimedDamage(fitLease, "Expired");
                // [2026-10-27 옛 줄 — 기록용] foreach (var ps in systems) { ps.Simulate(30f, false, false, false); ps.Stop(false, ParticleSystemStopBehavior.StopEmitting); }
                DrainTimedDamageFixtureSystems(systems, label + " (fit)");
                item.Return();
                Require(returned == 2 && VfxApprox(instance.transform.localScale, authoredScale),
                    "Fitted timed damage natural return restores the authored scale: " + label);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); channel.CloseVisuals("FixtureEnd"); }
        }

        // ====================================================================
        // [2026-10-27 화상 v2 Prewarm — 자연 소멸 fixture 보정]
        //
        // 무엇이 문제였나:
        //   EditMode에서는 Unity가 파티클을 저절로 갱신하지 않는다. 그래서 fixture는 "남은 입자가 다 사라진 뒤"를
        //   ① Simulate(30초)로 남은 입자를 다 없애고(→ Unity Simulate는 '일시정지(paused)' 상태로 끝난다)
        //   ② StopEmitting을 한 번 더 걸어 '멈춤(stopped)' 상태로 만들어 재현해 왔다.
        //   Prewarm이 꺼진 독·옛 화상·힐 프리팹은 이렇게 하면 IsAlive(false)가 false가 되어 통과했다(지난 스위트 PASS).
        //   Prewarm이 켜진 화상 v2(루트 + 4 자식 모두 Prewarm ON)는 입자 수가 0인데도 반환이 막혀
        //   「Natural drain returns lease exactly once」가 실패했다(2026-10-27 사용자 스위트).
        //   → 반환을 막는 것은 VfxPoolItem.DamageCanReturn의 IsAlive(false)뿐이므로, ②를 거친 뒤에도
        //     IsAlive(false)가 true로 남는 시스템이 있다는 뜻이다. 네 프리팹의 직렬화 값 중 v2만 다른 것은 Prewarm이다.
        //     (Unity 내부에서 Prewarm이 이 '일시정지 → StopEmitting' 전이에 어떻게 끼어드는지는 Unity를 돌려 보지 못해 [추정]이다.)
        //
        // 실제 게임에서는 문제없다:
        //   플레이 중에는 Unity가 매 프레임 갱신하며 "StopEmitting + 입자 0"을 스스로 멈춤 상태로 바꾼다.
        //   실기 로그(2026-10-27 00:40·04:15)에서 v2 화상은 Drain(NaturalDrain=True) → Return이 정상 기록됐다.
        //   그래서 제품 코드(VfxPoolItem)는 바꾸지 않고, fixture가 그 "멈춤 상태"를 재현하는 방법만 보강한다.
        //
        // 보강 방법(검사의 뜻은 그대로):
        //   ①·② 는 예전과 똑같이 한다. ②를 거친 뒤에도 IsAlive(false)가 true인 시스템에만,
        //   "입자 수 0"을 먼저 확인한 다음 StopEmittingAndClear로 멈춤 상태를 마저 만든다.
        //   입자가 0개라 Clear가 지우는 입자는 없다 → "살아 있는 입자가 있으면 반환이 막힌다"(이 도우미보다 앞의 검사)와
        //   "다 사라지면 정확히 1번 반환된다"(이 도우미 뒤의 검사)의 뜻은 바뀌지 않는다.
        //   보강이 필요했던 시스템은 상태와 함께 로그로 남긴다 → 다음 스위트 실행에서 [추정]을 사실로 확인하는 근거가 된다.
        // ====================================================================

        /// <summary>
        /// 화상·독 fixture에서 "남은 입자가 다 사라지고 시스템이 멈춘" 런타임 상태를 EditMode로 재현한다.
        /// 호출 전에 반드시 StopTimedDamage(StopEmitting)가 걸려 있어야 한다(그래야 Simulate 중에 새 입자가 나오지 않는다).
        /// </summary>
        /// <param name="systems">이펙트 인스턴스의 모든 ParticleSystem(루트 포함, GetComponentsInChildren 결과).</param>
        /// <param name="label">실패·진단 메시지에 붙일 이름(프리셋/프리팹).</param>
        private static void DrainTimedDamageFixtureSystems(ParticleSystem[] systems, string label)
        {
            string completed = null; // StopEmittingAndClear로 멈춤을 마저 만든 시스템 기록(없으면 로그 안 남김)
            foreach (var ps in systems)
            {
                // ① 남은 입자 소멸. Simulate는 일시정지로 끝난다(Unity 문서: 시뮬레이션 뒤 pause).
                ps.Simulate(30f, false, false, false);
                Require(ps.particleCount == 0, "Natural simulation removes particles before stop reassertion: " + label + "/" + ps.name);
                // ② 예전 재현 방식 그대로 — 입자를 지우지 않는 StopEmitting으로 멈춤 상태를 다시 건다.
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                if (!ps.IsAlive(false)) continue; // Prewarm 꺼진 시스템은 여기서 끝(예전과 같은 결과)
                // ③ 입자는 0인데 아직 "살아 있음"으로 남은 시스템(화상 v2의 Prewarm 시스템으로 추정).
                //    입자 0을 위에서 확인했으므로 Clear는 아무 입자도 지우지 않고 멈춤 상태만 마저 만든다.
                string before = DescribeFixtureParticleState(ps);
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                Require(!ps.IsAlive(false), "Zero-particle system reaches the stopped state for natural return: "
                    + label + " before=" + before + " after=" + DescribeFixtureParticleState(ps));
                completed = (completed == null ? string.Empty : completed + " | ") + before;
            }
            if (completed != null)
                GameLog.Dev.Info("Combat", "RunUnitActionSelfValidation",
                    "자연 소멸 fixture — 입자 0개인데 Simulate+StopEmitting 뒤에도 IsAlive가 true여서 StopEmittingAndClear로 멈춤을 마저 만든 시스템(fixture 재현 한계, 제품 코드 무관)",
                    "Label=" + label + "; Systems=" + completed);
        }

        /// <summary>fixture 진단용 — 파티클 시스템 하나의 재생 상태를 한 줄로 만든다.</summary>
        private static string DescribeFixtureParticleState(ParticleSystem ps)
            => ps.name + "{prewarm=" + ps.main.prewarm + ",playing=" + ps.isPlaying + ",paused=" + ps.isPaused
                + ",stopped=" + ps.isStopped + ",emitting=" + ps.isEmitting + ",count=" + ps.particleCount
                + ",alive=" + ps.IsAlive(false) + "}";

        // ====================================================================
        // [2026-10-26 Plan 10 — 뒤집힌 Client 따라가기 직전 바로잡기] (Plan _Tasks/2026-10-26/18_34_status-effect-width-fit 10-4 4번)
        //   실제 TimedDamageEffectPresenter + 실제 EffectManager + 실제 화상·독 프리셋으로 다음을 확인한다.
        //   ① 뒤집힌 Client(NetworkContext Client + ViewConverter.IsFlipped): Simulation Root를 돌려 자식 Visual Root를 "바로잡기 전 틀린 자리"로
        //      만든 뒤(=VisualRootProjector.LateUpdate가 아직 안 돈 상태), presenter LateUpdate만 부른다 → 이펙트가 바로잡힌 화면 위치에 있어야 한다.
        //      재생 시작 경로(화상 시작 보류 뒤 시작)와 따라가기 경로를 모두 본다.
        //   ② Host: 같은 회전에서 결과가 Root pose 그대로(바뀌지 않음)이고, 진단 계수가 늘지 않는다.
        //   EditMode라 Update/LateUpdate가 저절로 돌지 않으므로 presenter LateUpdate는 리플렉션으로 직접 부른다(= "presenter가 먼저 도는" 순서 재현).
        //   이 검사는 실행 순서 문제의 기하(자식이 Root와 함께 휘둘림)를 재현할 뿐, 실기 NetworkTransform 타이밍 자체를 재현하지는 않는다.
        // ====================================================================
        private static void ValidateTimedDamageFlippedFollow(UnitEffectConfig units, BuildingEffectConfig buildings)
        {
            Require(!UnityEngine.Application.isPlaying, "Flipped follow fixture must not run in a match.");
            var lateUpdate = typeof(TimedDamageEffectPresenter).GetMethod("LateUpdate", VfxFields);
            var ownersField = typeof(TimedDamageEffectPresenter).GetField("_owners", VfxFields);
            var visualRootField = typeof(VisualRootProjector).GetField("_visualRoot", VfxFields);
            var offsetField = typeof(VfxPoolItem).GetField("_damageOffset", VfxFields);
            var eulerField = typeof(VfxPoolItem).GetField("_damageEuler", VfxFields);
            var pullField = typeof(VfxPoolItem).GetField("_damageViewPull", VfxFields);
            Require(lateUpdate != null && ownersField != null && visualRootField != null && offsetField != null && eulerField != null && pullField != null,
                "Flipped follow fixture seams (presenter LateUpdate/_owners, projector _visualRoot, item follow offsets) exist.");
            bool oldActive = NetworkContext.IsNetworkActive, oldServer = NetworkContext.IsNetworkServer;
            var root = new GameObject("FlippedFollowFixture") { hideFlags = HideFlags.HideAndDontSave };
            var channel = new TimedDamageEffectChannel();
            double clock = 0d;
            try
            {
                var manager = root.AddComponent<EffectManager>();
                typeof(EffectManager).GetField("_vfxContainer", VfxFields).SetValue(manager, root.transform);
                manager.Initialize(units, buildings, null);
                // 유닛 흉내: Simulation Root(projector) → 직계 자식 Visual Root(보이는 몸). 실제 프리팹 구조(NET-ROOT-004)와 같다.
                var sim = new GameObject("FlippedFollowSim") { hideFlags = HideFlags.HideAndDontSave };
                sim.transform.SetParent(root.transform, false);
                var visual = new GameObject("FlippedFollowVisual") { hideFlags = HideFlags.HideAndDontSave };
                visual.transform.SetParent(sim.transform, false);
                var projector = sim.AddComponent<VisualRootProjector>();
                visualRootField.SetValue(projector, visual.transform);
                var presenter = root.AddComponent<TimedDamageEffectPresenter>();
                // 시작 보류 함수: clock < 화상 시작 시각이면 보류. 보류 중에 Root가 돌면 "Pose는 앞 프레임에 얻고 시작은 나중" 상황이 된다.
                presenter.SetProjectileImpactAlignment(() => clock, (_, _) => false);
                channel.Clock = () => 100d;
                UnitData current = null;
                // 생산 조합 루트와 같이 getter(PresentationTransform)로 Pose를 얻는다.
                presenter.Initialize(channel, manager, t => ReferenceEquals(t, current) ? projector.PresentationTransform : null);

                object OwnerOf(UnitData target)
                {
                    var owners = (IDictionary)ownersField.GetValue(presenter);
                    return owners.Contains(target.Id) ? owners[target.Id] : null;
                }
                T OwnerField<T>(object owner, string name) => (T)owner.GetType().GetField(name).GetValue(owner);
                void Tick() => lateUpdate.Invoke(presenter, null);
                // 이번 프레임의 올바른 화면 pose(ProjectNow와 같은 식): 뒤집힌 Client면 점대칭 위치 + 180° 회전, 아니면 Root 그대로.
                bool Flipped() => NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer && ViewConverter.IsFlipped;
                Vector3 ScreenPos() => Flipped() ? ViewConverter.ToView(sim.transform.position) : sim.transform.position;
                Quaternion ScreenRot() => Flipped() ? Quaternion.Euler(0f, 180f, 0f) * sim.transform.rotation : sim.transform.rotation;
                void RequireAtScreenPose(VfxPoolItem item, string label)
                {
                    Vector3 offset = (Vector3)offsetField.GetValue(item), euler = (Vector3)eulerField.GetValue(item), pull = (Vector3)pullField.GetValue(item);
                    Vector3 expected = ScreenPos() + ScreenRot() * offset + pull;
                    Require((item.transform.position - expected).sqrMagnitude < 1e-6f
                            && Quaternion.Angle(item.transform.rotation, ScreenRot() * Quaternion.Euler(euler)) < .01f,
                        label + $" — expected={expected}, actual={item.transform.position}.");
                }
                // NetworkTransform이 Update 중 Root를 돌리는 것 흉내: 30fps 한 프레임(270°/s → 9°) + 약간의 이동. 자식은 Root를 따라 휘둘린다.
                void TurnRoot() { sim.transform.Rotate(0f, 9f, 0f, Space.World); sim.transform.position += new Vector3(.05f, 0f, .03f); }

                // ── ① 뒤집힌 Client ──
                NetworkContext.Set(false, true);
                ViewConverter.Setup(true, new Vector3(10f, 0f, 20f));
                sim.transform.SetPositionAndRotation(new Vector3(16f, 0f, -2f), Quaternion.Euler(0f, 30f, 0f)); // 맵 중심에서 먼 유닛
                projector.ProjectNow();
                int nextId = 98300;
                foreach (var kind in new[] { TimedDamageVisual.Burn, TimedDamageVisual.Poison })
                {
                    current = VfxUnit(nextId++);
                    // [2026-10-27 단계 E] 독(MushroomBomber)도 화면 착탄 정렬 대상이 됐으므로 "시작 보류 → 바로잡힌 pose로 시작" 경로를
                    //   독에도 똑같이 돌린다. 판단은 운영 코드와 같은 함수(IsProjectileAligned)를 쓴다.
                    bool holdable = ProjectileAlignedTimedDamageStart.IsProjectileAligned(kind);
                    clock = holdable ? 99d : 1000d;
                    channel.Upsert(current, 1, kind, 3f);
                    Tick();
                    object owner = OwnerOf(current);
                    Require(owner != null, kind + " flipped fixture owner exists.");
                    if (holdable)
                    {
                        // 시작 경로: 보류 프레임에 Pose만 얻고, Root가 돈 다음 프레임에 시작한다.
                        Require(OwnerField<VfxPoolItem>(owner, "Item") == null && OwnerField<Transform>(owner, "Pose") == visual.transform,
                            kind + " start is held while the Pose is already acquired (start-path fixture precondition).");
                        TurnRoot();
                        Require(Vector3.Distance(visual.transform.position, ScreenPos()) > .5f,
                            "Fixture reproduces the stale child pose before the start-path correction.");
                        clock = 1000d;
                        Tick();
                        var started = OwnerField<VfxPoolItem>(owner, "Item");
                        Require(started != null, kind + " starts after the hold is released.");
                        RequireAtScreenPose(started, "[Plan 10] Flipped Client " + kind + " START reads the corrected screen pose, not the stale child pose");
                    }
                    var item = OwnerField<VfxPoolItem>(owner, "Item");
                    Require(item != null && OwnerField<VisualRootProjector>(owner, "Projector") == projector,
                        kind + " playback started and the presenter stored the pose's own projector.");
                    TurnRoot();
                    Require(Vector3.Distance(visual.transform.position, ScreenPos()) > .5f,
                        "Fixture reproduces the stale child pose before the follow correction (presenter-first order).");
                    int overBefore = OwnerField<int>(owner, "PoseOverCount");
                    Tick();
                    RequireAtScreenPose(item, "[Plan 10] Flipped Client " + kind + " FOLLOW after a root turn equals the corrected screen pose");
                    Require(OwnerField<int>(owner, "PoseOverCount") == overBefore + 1,
                        "[Plan 10 J5] The flipped Client diagnostic counts the corrected stale frame.");
                    channel.CloseAttachment(current, "FixtureNext");
                }

                // ── ② Host: 뒤집힘 설정이 남아 있어도 서버 쪽은 투영이 Root 그대로 → 결과 불변 ──
                NetworkContext.Set(true, true);
                projector.ProjectNow();
                current = VfxUnit(nextId++);
                clock = 1000d;
                channel.Upsert(current, 1, TimedDamageVisual.Poison, 3f);
                Tick();
                object hostOwner = OwnerOf(current);
                var hostItem = hostOwner != null ? OwnerField<VfxPoolItem>(hostOwner, "Item") : null;
                Require(hostItem != null, "Host fixture playback started.");
                TurnRoot();
                Require(VfxApprox(visual.transform.position, sim.transform.position),
                    "Host Visual Root already equals the Root pose before the correction (nothing to fix).");
                Tick();
                RequireAtScreenPose(hostItem, "[Plan 10] Host FOLLOW is unchanged (Root pose)");
                Require(VfxApprox(visual.transform.position, sim.transform.position) && OwnerField<int>(hostOwner, "PoseOverCount") == 0,
                    "[Plan 10] Host correction is a no-op and records no diagnostic.");
                channel.CloseAttachment(current, "FixtureEnd");
            }
            finally
            {
                channel.CloseVisuals("FixtureEnd"); // presenter Close(구독 해제·재생 정지)까지 이어진다.
                UnityEngine.Object.DestroyImmediate(root);
                NetworkContext.Set(oldServer, oldActive);
                // ViewConverter는 맵 중심을 읽는 API가 없어 이전 값을 그대로 되돌릴 수 없다 → EditMode 기본값으로 초기화한다
                //   (RunAuthoritativeAttackPresentationValidation의 뒤집힘 fixture와 같은 처리).
                ViewConverter.Reset();
            }
        }

        // ====================================================================
        // [2026-10-26 상태 이펙트 폭 맞춤] (Plan _Tasks/2026-10-26/18_34_status-effect-width-fit 3-6, 사용자 결정 D6)
        //   폭 맞춤이 바꾸는 파티클 모듈 값(Shape 범위 · 초당 방출 수 · 최대 입자 수)을 한 번에 비교하기 위한 스냅샷.
        //   Burst 개수·방출 켜짐도 함께 담아 "건드리면 안 되는 값"이 그대로인지도 본다.
        // ====================================================================
        private struct WidthFitModules
        {
            public bool EmissionEnabled, ShapeEnabled;
            public ParticleSystemShapeType ShapeType;
            public float ShapeRadius, RadiusThickness, RateMultiplier;
            public Vector3 ShapeRotation;
            public int MaxParticles, BurstCount;
        }

        private static WidthFitModules[] SnapshotWidthFitModules(ParticleSystem[] systems)
        {
            var result = new WidthFitModules[systems.Length];
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                result[i] = new WidthFitModules
                {
                    EmissionEnabled = ps.emission.enabled, ShapeEnabled = ps.shape.enabled, ShapeType = ps.shape.shapeType,
                    ShapeRadius = ps.shape.radius, RadiusThickness = ps.shape.radiusThickness,
                    RateMultiplier = ps.emission.rateOverTimeMultiplier, ShapeRotation = ps.shape.rotation,
                    MaxParticles = ps.main.maxParticles, BurstCount = ps.emission.burstCount,
                };
            }
            return result;
        }

        /// <summary>두 스냅샷이 완전히 같은지. 원복은 저장해 둔 값을 그대로 다시 쓰므로 float도 정확히 같아야 한다.</summary>
        private static bool SameWidthFitModules(WidthFitModules[] a, WidthFitModules[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].EmissionEnabled != b[i].EmissionEnabled || a[i].ShapeEnabled != b[i].ShapeEnabled
                    || a[i].ShapeType != b[i].ShapeType || a[i].ShapeRadius != b[i].ShapeRadius
                    || a[i].RadiusThickness != b[i].RadiusThickness || a[i].RateMultiplier != b[i].RateMultiplier
                    || a[i].ShapeRotation != b[i].ShapeRotation || a[i].MaxParticles != b[i].MaxParticles
                    || a[i].BurstCount != b[i].BurstCount) return false;
            return true;
        }

        // [2026-10-26 화상 VFX v2 연결] 화상 프리팹 경로.
        //   v2 = 지금 실제 화상 프리셋이 쓰는 새 프리팹(몸 둘레 방출을 프리팹 스스로 한다 → 폭 맞춤·당김 꺼짐).
        //   옛 프리팹 = 롤백용으로 남겨 둔 예전 화상 프리팹(가운데 한 점에서 Flame_Haze 1장 방출).
        //     폭 맞춤 기능은 지금 어떤 실제 프리셋에서도 켜져 있지 않으므로, "기능 자체가 여전히 맞게 동작하는지"는
        //     이 옛 프리팹 + 메모리에만 만든 시험용 프리셋(폭 맞춤 ON)으로 확인한다. 옛 프리팹을 지우면 이 검사가 실패하며,
        //     그때는 폭 맞춤 fixture가 쓸 다른 "한 점 방출" 프리팹을 정해 이 경로를 바꿔야 한다.
        private const string BurnV2PrefabPath = "Assets/_Project/Prefabs/VFX/Units/vfx_infernospirit_burn_v2.prefab";
        private const string LegacyBurnPrefabPath = "Assets/_Project/Prefabs/VFX/Units/vfx_infernospirit_burn.prefab";

        /// <summary>
        /// 실제 화상 프리셋을 복제해 메모리에만 존재하는 시험용 프리셋을 만든다(에셋으로 저장하지 않음).
        /// 복제(Instantiate)는 위치·회전·배율 보정 같은 직렬화 값을 그대로 가져오므로, 시험에 필요한 값만 리플렉션으로 바꾼다.
        /// 호출한 쪽이 finally에서 DestroyImmediate로 반드시 지운다.
        /// </summary>
        private static EffectPreset CloneFixturePreset(EffectPreset source, GameObject prefab, bool widthFit, float viewPullRatio)
        {
            var clone = UnityEngine.Object.Instantiate(source);
            clone.hideFlags = HideFlags.HideAndDontSave; // 씬·에셋에 남지 않게 숨김 + 저장 안 함
            typeof(EffectPreset).GetField("_vfxPrefab", VfxFields).SetValue(clone, prefab);
            typeof(EffectPreset).GetField("_widthFitEnabled", VfxFields).SetValue(clone, widthFit);
            typeof(EffectPreset).GetField("_widthFitMaxCount", VfxFields).SetValue(clone, EffectPreset.DefaultWidthFitMaxCount);
            typeof(EffectPreset).GetField("_viewPullRadiusRatio", VfxFields).SetValue(clone, viewPullRatio);
            return clone;
        }

        /// <summary>
        /// [2026-10-26 상태 이펙트 폭 맞춤] 화상 불꽃을 몸통 폭에 맞추는 기능을 확인한다.
        ///   (1) 실제 프리셋 설정: 화상·독·힐 모두 꺼짐. 화상은 2026-10-26 v2 프리팹 연결 때 꺼졌다
        ///       (v2는 프리팹이 몸 둘레 Circle 방출을 직접 하므로, 폭 맞춤이 그 Shape를 덮어쓰면 안 된다 — 사용자 승인).
        ///       화상 프리셋이 실제 v2 프리팹을 가리키는지도 함께 본다. 결정이 바뀌면 이 줄도 함께 바꾼다.
        ///   (2) 순수 공식: 반올림(D3, 2.5 → 3) · 윤곽 안 반경(D4) · 상한(D2) · 잘못된 입력은 폭 맞춤 없음.
        ///   (3) 시험용 프리셋(옛 화상 프리팹 + 폭 맞춤 ON·상한 3, 메모리에만 존재): 넓은 대상 → 방출 켜진 시스템만 범위·개수가 공식대로,
        ///       Burst·방출 꺼진 루트는 그대로, 같은 대상 갱신은 재계산 없음, 자연 반환 뒤 원본 복원, 넓은 → 좁은/Identity 재사용 시 원본 그대로.
        ///       (v2 연결 전에는 실제 화상 프리셋으로 하던 검사다. 기능 코드는 그대로 남아 있으므로 같은 조건으로 계속 확인한다.)
        ///   (4) 실제 크기 표로 시험용 프리셋의 유닛별 결과를 로그에 남긴다 — 다시 켤 때의 참고값(실제 화면 값이 아님).
        ///   (5) 재생 실패 거부 · 강제 정리 경로 원복, 원복이 빠진 채 다음 재생이 시작돼도 먼저 원본으로 돌아감(이중 안전장치).
        ///   (6) 옵션이 꺼진 실제 독 프리셋: 넓은 대상이어도 모든 값이 원본 그대로.
        ///   (7) 옵션이 꺼진 실제 화상 프리셋(v2): 넓은 대상이어도 v2가 직접 정한 Shape·개수가 원본 그대로.
        ///   힐(옵션 꺼짐)의 "넓은 대상이어도 그대로"는 기존 Bloom fixture(RunUnitActionSelfValidation.cs)에 덧붙였다.
        /// EditMode에서는 OnDisable이 자동으로 불리지 않으므로 기존 fixture처럼 리플렉션으로 직접 호출한다.
        /// </summary>
        private static void ValidateTimedDamageWidthFit(UnitEffectConfig units)
        {
            // actualBurn = 게임이 실제로 쓰는 화상 프리셋(v2, 폭 맞춤 꺼짐).
            // burn = 아래 (3)~(5)에서 폭 맞춤 기능을 시험하기 위한 메모리 전용 복제본(옛 프리팹 + 폭 맞춤 ON). try 직전에 만든다.
            var actualBurn = units.GetTimedDamage(UnitType.InfernoSpirit);
            var poison = units.GetTimedDamage(UnitType.MushroomBomber);
            var heal = units.GetHeal(UnitType.BloomFairy);
            Require(actualBurn?.VfxPrefab != null && poison?.VfxPrefab != null && heal?.VfxPrefab != null,
                "Burn, poison and heal recipient presets exist for the width-fit fixture.");

            // (1) 실제 프리셋 설정 — 화상은 v2 연결로 꺼짐(2026-10-26), 독·힐은 D1대로 꺼짐.
            //     경로 비교는 GUID로 연결된 프리팹이 실제로 v2 파일인지 확인한다(옛 프리팹으로 되돌아가 있으면 실패).
            Require(AssetDatabase.GetAssetPath(actualBurn.VfxPrefab) == BurnV2PrefabPath,
                "Burn preset uses the v2 burn prefab (2026-10-26). path=" + AssetDatabase.GetAssetPath(actualBurn.VfxPrefab));
            Require(!actualBurn.WidthFitEnabled,
                "Burn preset keeps width fit off — the v2 prefab spreads its own emission around the body (2026-10-26).");
            Require(!poison.WidthFitEnabled && !heal.WidthFitEnabled,
                "Poison and heal presets keep width fit off (user decision D1 — current behavior preserved).");

            // (2) 순수 공식. 기대값은 Plan 1절 표의 계산과 같은 방식(불꽃 폭 = 0.55 × 키)으로 직접 적었다.
            //     0.55는 옛 화상 프리팹 Flame_Haze의 시작 크기다. 프리팹을 읽지 않는 순수 계산 검사라 v2 연결과 무관하게 그대로 둔다.
            void Formula(float radius, float width, int cap, int expectCount, float expectRadius, string message)
            {
                bool ok = VfxPoolItem.TryComputeWidthFit(radius, width, cap, out int n, out float r);
                Require(ok && n == expectCount && Mathf.Abs(r - expectRadius) < 1e-4f,
                    message + " (count=" + n + ", radius=" + r + ")");
            }
            Formula(.413f, .55f * .521f, 3, 3, .413f - .55f * .521f * .5f, "Tank-like wide body gets 3 flames inside its outline.");
            Formula(.22f, .55f * .53f, 3, 2, .22f - .55f * .53f * .5f, "BearGuard-like body gets 2 flames.");
            Formula(.167f, .55f * .416f, 3, 1, .167f - .55f * .416f * .5f, "StreamSpirit-like body stays 1 flame (round, not ceil).");
            Formula(.089f, .55f * .356f, 3, 1, 0f, "RabbitTrickster-like narrow body stays 1 flame with zero spread.");
            Formula(.3125f, .25f, 3, 3, .1875f, "Exact x.5 rounds half up (2.5 → 3), not to even.");
            Formula(1f, .2f, 3, 3, .9f, "Flame count is capped by the preset maximum.");
            Formula(1f, .2f, 0, 1, .9f, "A cap below 1 is treated as 1 (no extra flames).");
            Require(!VfxPoolItem.TryComputeWidthFit(0f, .3f, 3, out int c0, out _) && c0 == 1
                && !VfxPoolItem.TryComputeWidthFit(float.NaN, .3f, 3, out _, out _)
                && !VfxPoolItem.TryComputeWidthFit(float.PositiveInfinity, .3f, 3, out _, out _)
                && !VfxPoolItem.TryComputeWidthFit(.4f, 0f, 3, out _, out _),
                "Unknown/invalid radius or flame width means no width fit (count 1).");
            Require(Mathf.Approximately(default(RecipientFit).SafeRadius, 0f)
                && Mathf.Approximately(new RecipientFit(1f, Vector3.zero).SafeRadius, 0f)
                && Mathf.Approximately(new RecipientFit(1f, Vector3.zero, -1f).SafeRadius, 0f)
                && Mathf.Approximately(new RecipientFit(1f, Vector3.zero, .3f).SafeRadius, .3f),
                "RecipientFit radius defaults to 0 (no width fit) and rejects invalid values.");

            // 시험용 프리셋: 옛 화상 프리팹(롤백용으로 남김, 가운데 한 점 방출) + 폭 맞춤 ON·상한 3 + 당김 0.
            //   예전 실제 화상 프리셋과 같은 조건이라 (3)~(5)의 기대값·의미가 v2 연결 전과 똑같다.
            //   당김을 0으로 두는 이유: 이 검사는 파티클 모듈 값만 보며, 위치 당김은 ValidateRecipientViewPull이 따로 본다.
            var legacyBurnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyBurnPrefabPath);
            Require(legacyBurnPrefab != null,
                "Legacy burn prefab (kept for rollback) exists for the width-fit feature fixture: " + LegacyBurnPrefabPath);
            var burn = CloneFixturePreset(actualBurn, legacyBurnPrefab, true, 0f);
            var root = new GameObject("WidthFitFixture") { hideFlags = HideFlags.HideAndDontSave };
            var channel = new TimedDamageEffectChannel();
            try
            {
                // 복제본 확인은 try 안에서 한다 — 실패해도 finally가 복제본을 지우게.
                Require(burn.WidthFitEnabled && burn.WidthFitMaxCount == EffectPreset.DefaultWidthFitMaxCount
                    && burn.VfxPrefab == legacyBurnPrefab && burn.ViewPullRadiusRatio == 0f,
                    "Width-fit fixture preset enables width fit (max 3) on the legacy burn prefab with no camera pull.");
                var pose = new GameObject("WidthFitPose") { hideFlags = HideFlags.HideAndDontSave };
                pose.transform.SetParent(root.transform, false);
                pose.transform.SetPositionAndRotation(new Vector3(2f, 0f, -1f), Quaternion.Euler(0f, 40f, 0f));
                int nextUnit = 98100;

                // ── (3) 시험용 프리셋(옛 화상 프리팹 + 폭 맞춤 ON) ──
                var instance = UnityEngine.Object.Instantiate(burn.VfxPrefab, root.transform);
                instance.SetActive(false);
                var item = instance.AddComponent<VfxPoolItem>();
                int returned = 0; item.Setup(burn.VfxPrefab, _ => returned++);
                var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                var authored = SnapshotWidthFitModules(systems);
                TimedDamageEffectPresentation PlayBurn(RecipientFit fit)
                {
                    var s = channel.Upsert(VfxUnit(nextUnit++), 1, TimedDamageVisual.Burn, 3f);
                    item.PlayTimedDamage(s, pose.transform, burn, channel, fit);
                    return s;
                }
                // 기존 ValidateDamageParticles와 같은 자연 drain 재현: Stop → 시뮬레이션으로 입자 소멸 → 멈춤 상태 재확인 → Return.
                void DrainBurn()
                {
                    item.StopTimedDamage(item.DamageLease, "Expired");
                    foreach (var ps in systems) { ps.Simulate(30f, false, false, false); ps.Stop(false, ParticleSystemStopBehavior.StopEmitting); }
                    item.Return();
                }

                const float wideRadius = .413f;
                var wideState = PlayBurn(new RecipientFit(.521f, Vector3.zero, wideRadius)); // 탱크와 같은 키·반경
                int fitted = 0;
                for (int i = 0; i < systems.Length; i++)
                {
                    var ps = systems[i];
                    if (!authored[i].EmissionEnabled)
                    {
                        Require(SameWidthFitModules(new[] { SnapshotWidthFitModules(systems)[i] }, new[] { authored[i] }),
                            "Non-emitting burn system keeps its authored modules: " + ps.name);
                        continue;
                    }
                    Require(ps.main.scalingMode == ParticleSystemScalingMode.Hierarchy,
                        "Burn emitting system uses Hierarchy scaling (width-fit radius conversion premise): " + ps.name);
                    float scaleX = Mathf.Abs(ps.transform.lossyScale.x);
                    float width = (ps.main.startSize3D ? ps.main.startSizeX : ps.main.startSize).constantMax * scaleX;
                    Require(VfxPoolItem.TryComputeWidthFit(wideRadius, width, burn.WidthFitMaxCount, out int n, out float r),
                        "Burn flame width is measurable: " + ps.name);
                    if (n <= 1)
                    {
                        Require(SameWidthFitModules(new[] { SnapshotWidthFitModules(systems)[i] }, new[] { authored[i] }),
                            "Single-flame result leaves the system untouched: " + ps.name);
                        continue;
                    }
                    fitted++;
                    var shape = ps.shape;
                    Require(shape.enabled && shape.shapeType == ParticleSystemShapeType.Circle
                        && Mathf.Abs(shape.radius * scaleX * Mathf.Abs(shape.scale.x) - r) < 1e-4f
                        && Mathf.Approximately(shape.radiusThickness, VfxPoolItem.WidthFitRadiusThickness)
                        // Euler 값은 같은 회전도 표기가 다를 수 있어(-90 / 270) 회전 각도 차이로 비교한다.
                        && Quaternion.Angle(Quaternion.Euler(shape.rotation), Quaternion.Euler(-90f, 0f, 0f)) < .01f,
                        "Wide recipient spreads emission on a horizontal circle of world radius R: " + ps.name);
                    Require(n <= burn.WidthFitMaxCount
                        && Mathf.Approximately(ps.emission.rateOverTimeMultiplier, authored[i].RateMultiplier * n)
                        && ps.main.maxParticles == authored[i].MaxParticles * n
                        && ps.emission.burstCount == authored[i].BurstCount,
                        "Wide recipient multiplies rate/max particles by N (≤ cap) and leaves bursts unchanged: " + ps.name);
                    Require(r + width * .5f <= wideRadius + 1e-4f,
                        "Flame outer edge stays inside the body outline (D4): " + ps.name);
                }
                Require(fitted > 0, "A Tank-sized recipient needs at least 2 burn flames on an emitting system "
                    + "(if the legacy burn prefab flame was resized, re-check the expected width-fit result).");
                var widened = SnapshotWidthFitModules(systems);
                wideState.Revision++; item.RefreshTimedDamage(item.DamageLease, wideState);
                Require(SameWidthFitModules(SnapshotWidthFitModules(systems), widened),
                    "Same-owner refresh does not recompute or restore the width fit.");
                DrainBurn();
                Require(returned == 1 && SameWidthFitModules(SnapshotWidthFitModules(systems), authored),
                    "Natural return restores the authored burn modules.");

                PlayBurn(new RecipientFit(.356f, Vector3.zero, .089f)); // 래빗과 같은 키·반경 → 1장
                Require(SameWidthFitModules(SnapshotWidthFitModules(systems), authored),
                    "Wide → narrow reuse plays with authored modules (no leftover spread/count).");
                DrainBurn();
                PlayBurn(RecipientFit.Identity);
                Require(SameWidthFitModules(SnapshotWidthFitModules(systems), authored),
                    "Unknown recipient (identity fit, radius 0) keeps authored modules.");
                DrainBurn();
                Require(returned == 3, "Narrow and identity burn plays return naturally.");

                // ── (4) 실제 크기 표로 유닛별 결과 기록(통과 조건은 상한·원복만 — 표는 사용자가 다시 잴 수 있다) ──
                //   [2026-10-26 v2 연결] 시험용 프리셋(옛 프리팹 + 폭 맞춤 ON) 기준 값이다. 실제 화상(v2)은 폭 맞춤이 꺼져 있어
                //   게임 화면과는 다르다 — 로그 문구에 그 점을 적어 사용자가 실기 화면과 대조하다 헷갈리지 않게 한다.
                string report = null;
                foreach (var type in new[] { UnitType.Tank, UnitType.CannonCart, UnitType.BearGuard, UnitType.StreamSpirit, UnitType.RabbitTrickster })
                {
                    PlayBurn(units.GetRecipientFit(type));
                    string entry = type + ":1";
                    for (int i = 0; i < systems.Length; i++)
                    {
                        if (!authored[i].EmissionEnabled || authored[i].MaxParticles <= 0) continue;
                        int n = systems[i].main.maxParticles / authored[i].MaxParticles;
                        Require(n >= 1 && n <= burn.WidthFitMaxCount, "Actual table width fit stays within the cap: " + type);
                        if (n > 1)
                            entry = type + ":" + n + "@" + (systems[i].shape.radius * Mathf.Abs(systems[i].transform.lossyScale.x)
                                * Mathf.Abs(systems[i].shape.scale.x)).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                    }
                    report = report == null ? entry : report + "|" + entry;
                    DrainBurn();
                    Require(SameWidthFitModules(SnapshotWidthFitModules(systems), authored), "Actual table play restores authored modules: " + type);
                }
                GameLog.Dev.Info("Combat", "RunUnitActionSelfValidation",
                    "폭 맞춤 기능 검사(시험용: 옛 화상 프리팹 + 폭 맞춤 ON. 실제 화상 v2는 꺼짐) — 실제 크기 표 기준 유닛별 불꽃 개수@방출 반경(m). "
                    + "1 = 한 점 방출", "WidthFitFixture=" + report);

                // ── (5) 재생 실패 거부 · 강제 정리 · 이중 안전장치 ──
                PlayBurn(new RecipientFit(.521f, Vector3.zero, wideRadius));
                Require(!SameWidthFitModules(SnapshotWidthFitModules(systems), authored), "Wide play changed modules before the reject check.");
                int beforeReject = returned;
                item.RejectTimedDamageAcquire();
                Require(returned == beforeReject + 1 && SameWidthFitModules(SnapshotWidthFitModules(systems), authored),
                    "Rejected acquire restores the authored modules before returning to the pool.");

                PlayBurn(new RecipientFit(.521f, Vector3.zero, wideRadius));
                var onDisable = typeof(VfxPoolItem).GetMethod("OnDisable", VfxFields);
                Require(onDisable != null, "VfxPoolItem forced-teardown method exists for the width-fit fixture.");
                onDisable.Invoke(item, null);
                Require(SameWidthFitModules(SnapshotWidthFitModules(systems), authored),
                    "Forced teardown (OnDisable) restores the authored modules.");

                // 원복이 빠진 상황을 일부러 만든다: 넓게 재생한 뒤 "재생 중" 표시만 지우고(원복 없이) 좁은 대상으로 다시 재생.
                PlayBurn(new RecipientFit(.521f, Vector3.zero, wideRadius));
                typeof(VfxPoolItem).GetField("_active", VfxFields).SetValue(item, false);
                typeof(VfxPoolItem).GetField("_damageLoop", VfxFields).SetValue(item, false);
                PlayBurn(new RecipientFit(.356f, Vector3.zero, .089f));
                Require(SameWidthFitModules(SnapshotWidthFitModules(systems), authored),
                    "Play start restores authored modules even if a previous restore was missed (double safety).");
                DrainBurn();

                // ── (6) 옵션이 꺼진 실제 독 프리셋: 넓은 대상이어도 원본 그대로 ──
                var poisonInstance = UnityEngine.Object.Instantiate(poison.VfxPrefab, root.transform);
                poisonInstance.SetActive(false);
                var poisonItem = poisonInstance.AddComponent<VfxPoolItem>();
                int poisonReturned = 0; poisonItem.Setup(poison.VfxPrefab, _ => poisonReturned++);
                var poisonSystems = poisonInstance.GetComponentsInChildren<ParticleSystem>(true);
                var poisonAuthored = SnapshotWidthFitModules(poisonSystems);
                poisonItem.PlayTimedDamage(channel.Upsert(VfxUnit(nextUnit++), 1, TimedDamageVisual.Poison, 3f), pose.transform, poison, channel,
                    new RecipientFit(.521f, Vector3.zero, wideRadius));
                Require(SameWidthFitModules(SnapshotWidthFitModules(poisonSystems), poisonAuthored),
                    "Width-fit-off poison keeps every authored module even on a wide recipient.");
                poisonItem.StopTimedDamage(poisonItem.DamageLease, "Expired");
                foreach (var ps in poisonSystems) { ps.Simulate(30f, false, false, false); ps.Stop(false, ParticleSystemStopBehavior.StopEmitting); }
                poisonItem.Return();
                Require(poisonReturned == 1 && SameWidthFitModules(SnapshotWidthFitModules(poisonSystems), poisonAuthored),
                    "Width-fit-off poison natural return keeps authored modules.");

                // ── (7) [2026-10-26 v2 연결] 옵션이 꺼진 실제 화상 프리셋(v2): 넓은 대상이어도 원본 그대로 ──
                //   v2는 각 레이어(Flame_Tongues·Embers·Base_Glow·Smoke_Wisp)가 몸 둘레 Circle 방출 반경·개수를 프리팹에서 직접 정한다.
                //   폭 맞춤이 켜져 있으면 방출이 켜진 레이어 전부의 Shape 반경·회전·개수를 덮어써 그 모양이 깨지므로,
                //   "탱크 크기 대상에서도 한 값도 바뀌지 않는다"를 실제 프리셋·실제 프리팹으로 확인한다.
                var v2Instance = UnityEngine.Object.Instantiate(actualBurn.VfxPrefab, root.transform);
                v2Instance.SetActive(false);
                var v2Item = v2Instance.AddComponent<VfxPoolItem>();
                int v2Returned = 0; v2Item.Setup(actualBurn.VfxPrefab, _ => v2Returned++);
                var v2Systems = v2Instance.GetComponentsInChildren<ParticleSystem>(true);
                var v2Authored = SnapshotWidthFitModules(v2Systems);
                v2Item.PlayTimedDamage(channel.Upsert(VfxUnit(nextUnit++), 1, TimedDamageVisual.Burn, 3f), pose.transform, actualBurn, channel,
                    new RecipientFit(.521f, Vector3.zero, wideRadius));
                Require(SameWidthFitModules(SnapshotWidthFitModules(v2Systems), v2Authored),
                    "Width-fit-off actual burn (v2) keeps every authored module even on a wide recipient.");
                v2Item.StopTimedDamage(v2Item.DamageLease, "Expired");
                // [2026-10-27 옛 줄 — 기록용] v2는 Prewarm ON이라 이 재현으로는 반환되지 않았다(DrainTimedDamageFixtureSystems 머리 주석).
                // foreach (var ps in v2Systems) { ps.Simulate(30f, false, false, false); ps.Stop(false, ParticleSystemStopBehavior.StopEmitting); }
                DrainTimedDamageFixtureSystems(v2Systems, "WidthFit(7)/" + actualBurn.VfxPrefab.name);
                v2Item.Return();
                Require(v2Returned == 1 && SameWidthFitModules(SnapshotWidthFitModules(v2Systems), v2Authored),
                    "Width-fit-off actual burn (v2) natural return keeps authored modules.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(burn); // 메모리 전용 시험용 프리셋 정리(에셋 아님)
                UnityEngine.Object.DestroyImmediate(root);
                channel.CloseVisuals("FixtureEnd");
            }
        }

        /// <summary>
        /// [2026-10-26 D7 후속 — 상태 이펙트를 몸 앞에 그리기] (Plan _Tasks/2026-10-26/18_34_status-effect-width-fit 8-4 6번, 사용자 결정 D7-4)
        /// 상태 이펙트를 카메라 쪽으로 (몸통 반경 × 프리셋 비율)만큼 당기는 기능을 확인한다.
        ///   (1) 실제 프리셋 설정: 화상·독·힐 모두 0(꺼짐). 화상은 2026-10-26 v2 프리팹 연결 때 1.75 → 0으로 꺼졌다
        ///       (뒤쪽 불꽃이 몸에 가려지는 차이가 "몸을 감싸는 느낌"을 만든다 — 당기면 사라진다. 사용자 승인). 독·힐은 D7-3대로 꺼짐.
        ///       결정이 바뀌면 이 줄도 함께 바꾼다.
        ///   [2026-10-26 v2 연결] 아래 (3)~(6)의 "화상"은 실제 화상 프리셋을 메모리에서 복제하고 비율만 1.75로 바꾼 시험용 프리셋이다
        ///       (프리팹은 실제 화상 프리팹 v2 그대로). 당김 기능 코드는 남아 있으므로 v2 연결 전과 같은 조건으로 계속 확인한다.
        ///       실제 화상 프리셋(꺼짐)이 위치를 바꾸지 않는지는 (7)에서 독과 함께 본다.
        ///   (2) 순수 공식: 당김 = 카메라 반대 방향 × 비율 × 반경. 비율·반경 0·NaN·무한대, 카메라 방향 0·NaN이면 당김 없음.
        ///   (3) 로그 값: 옵션 꺼짐 = null(독·힐 로그 불변) · 반경 없음 "NoRadius" · 카메라 없음 "NoCamera" · 정상 = 거리.
        ///   (4) 시험용 화상 프리셋(실제 v2 프리팹 + 비율 1.75) + 시험용 직교 카메라(55°): 위치 = 기존 공식 + 당김, 카메라 화면 기준 좌표(오른쪽·위쪽 성분)는
        ///       당기기 전과 같다. 같은 대상 갱신·따라붙기·대상 회전 변경 뒤에도 당김 벡터·방향이 그대로다.
        ///   (5) 반경 0 fit(기존 2인자 fixture와 같은 값) → 기존 공식 그대로. 넓은 → 반경 0 / Identity 재사용 → 당김 0.
        ///   (6) 자연 반환 · 재생 실패 거부 · 강제 정리(OnDisable)에서 당김이 0으로 돌아온다. 카메라가 없으면 당김 0, 예외 없음.
        ///   (7) 옵션이 꺼진 실제 독 프리셋 · 실제 화상 프리셋(v2): 반경·카메라가 있어도 위치가 기존 공식 그대로.
        ///   (8) 힐 경로: 실제 힐 프리셋(꺼짐)은 당김 0. 시험용 복제 프리셋(비율 1.75, 메모리에만 존재)으로 힐 경로의 당김·원복을 확인.
        ///   (9) 실제 크기 표로 유닛별 당김 거리를 로그에 남긴다 — 사용자 육안 확인과 대조용(통과 조건 아님).
        ///       [2026-10-26 v2 연결] 실제 화상 프리셋 기준이라 지금은 모든 유닛이 "Off"로 찍히는 것이 정상이다.
        /// 시험용 카메라는 Camera 컴포넌트를 꺼 둔 숨김 오브젝트라 에디터 화면에 아무것도 그리지 않는다.
        /// VfxPoolItem.ViewPullCameraOverride는 finally에서 반드시 null로 되돌린다(다른 검사가 실제 Camera.main을 쓰게).
        /// </summary>
        private static void ValidateRecipientViewPull(UnitEffectConfig units)
        {
            // actualBurn = 게임이 실제로 쓰는 화상 프리셋(v2, 당김 꺼짐).
            // burn = (3)~(6)에서 당김 기능을 시험하기 위한 메모리 전용 복제본(같은 v2 프리팹 + 비율 1.75). try 직전에 만든다.
            var actualBurn = units.GetTimedDamage(UnitType.InfernoSpirit);
            var poison = units.GetTimedDamage(UnitType.MushroomBomber);
            var heal = units.GetHeal(UnitType.BloomFairy);
            Require(actualBurn?.VfxPrefab != null && poison?.VfxPrefab != null && heal?.VfxPrefab != null,
                "Burn, poison and heal recipient presets exist for the view-pull fixture.");

            // (1) 실제 프리셋 설정 — 화상은 v2 연결로 꺼짐(2026-10-26), 독·힐은 D7-3대로 꺼짐.
            Require(actualBurn.ViewPullRadiusRatio == 0f,
                "Burn preset keeps the camera pull off — rear flames hidden by the body give the v2 wrap-around look (2026-10-26). ratio="
                + actualBurn.ViewPullRadiusRatio);
            Require(poison.ViewPullRadiusRatio == 0f && heal.ViewPullRadiusRatio == 0f,
                "Poison and heal presets keep the camera pull off (user decision D7-3 — current behavior preserved).");

            // (2) 순수 공식
            Vector3 tiltForward = Quaternion.Euler(55f, 0f, 0f) * Vector3.forward; // 실제 게임 카메라와 같은 기울기
            Require(VfxPoolItem.TryComputeViewPull(1.75f, .22f, tiltForward, out Vector3 bearPull)
                && VfxApprox(bearPull, -tiltForward * (1.75f * .22f)) && Mathf.Abs(bearPull.magnitude - .385f) < 1e-4f,
                "View pull = opposite camera forward × ratio × radius (BearGuard-like 0.22 × 1.75 = 0.385).");
            Require(VfxPoolItem.TryComputeViewPull(1f, .5f, tiltForward * 7f, out Vector3 unitPull) && Mathf.Abs(unitPull.magnitude - .5f) < 1e-4f,
                "View pull normalizes the camera direction (forward length does not change the distance).");
            Require(!VfxPoolItem.TryComputeViewPull(0f, .3f, tiltForward, out Vector3 z0) && z0 == Vector3.zero
                && !VfxPoolItem.TryComputeViewPull(-1f, .3f, tiltForward, out _)
                && !VfxPoolItem.TryComputeViewPull(float.NaN, .3f, tiltForward, out _)
                && !VfxPoolItem.TryComputeViewPull(float.PositiveInfinity, .3f, tiltForward, out _)
                && !VfxPoolItem.TryComputeViewPull(1.75f, 0f, tiltForward, out _)
                && !VfxPoolItem.TryComputeViewPull(1.75f, float.NaN, tiltForward, out _)
                && !VfxPoolItem.TryComputeViewPull(1.75f, .3f, Vector3.zero, out _)
                && !VfxPoolItem.TryComputeViewPull(1.75f, .3f, new Vector3(float.NaN, 0f, 1f), out Vector3 zNaN) && zNaN == Vector3.zero,
                "Invalid ratio, radius or camera direction means no pull (zero vector).");

            var resolve = typeof(VfxPoolItem).GetMethod("ResolveViewPull", BindingFlags.Static | BindingFlags.NonPublic);
            var damagePullField = typeof(VfxPoolItem).GetField("_damageViewPull", VfxFields);
            var bloomPullField = typeof(VfxPoolItem).GetField("_bloomViewPull", VfxFields);
            Require(resolve != null && damagePullField != null && bloomPullField != null,
                "VfxPoolItem view-pull members exist for the fixture.");
            string Resolve(EffectPreset preset, RecipientFit fit, out Vector3 pull)
            {
                var args = new object[] { preset, fit, null };
                var text = (string)resolve.Invoke(null, args);
                pull = (Vector3)args[2];
                return text;
            }
            Vector3 DamagePull(VfxPoolItem i) => (Vector3)damagePullField.GetValue(i);
            Vector3 BloomPull(VfxPoolItem i) => (Vector3)bloomPullField.GetValue(i);

            // 시험용 화상 프리셋: 실제 화상 프리셋 복제 + 비율 1.75(옛 실제 값 = 권장값). 프리팹은 실제 v2 그대로 두고 폭 맞춤은 끈다
            //   (당김은 transform 위치만 옮기므로 프리팹 모양과 무관하다. 폭 맞춤까지 켜면 이 검사가 볼 필요 없는 모듈 값이 바뀐다).
            var burn = CloneFixturePreset(actualBurn, actualBurn.VfxPrefab, false, EffectPreset.RecommendedViewPullRadiusRatio);
            var root = new GameObject("ViewPullFixture") { hideFlags = HideFlags.HideAndDontSave };
            var channel = new TimedDamageEffectChannel();
            EffectPreset pulledHeal = null;
            try
            {
                // 복제본 확인은 try 안에서 한다 — 실패해도 finally가 복제본을 지우게.
                Require(Mathf.Approximately(burn.ViewPullRadiusRatio, EffectPreset.RecommendedViewPullRadiusRatio)
                    && !burn.WidthFitEnabled && burn.VfxPrefab == actualBurn.VfxPrefab,
                    "View-pull fixture preset pulls by radius × 1.75 on the actual burn prefab with width fit off.");
                // 시험용 직교 카메라 — 실제 게임 카메라(Game.unity: 직교, X축 55°)와 같은 방향. 꺼 둔 컴포넌트라 아무것도 그리지 않는다.
                var camGo = new GameObject("ViewPullFixtureCamera") { hideFlags = HideFlags.HideAndDontSave };
                camGo.transform.SetParent(root.transform, false);
                camGo.transform.SetPositionAndRotation(new Vector3(0f, 10f, -7f), Quaternion.Euler(55f, 0f, 0f));
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false; cam.orthographic = true;
                Camera fixtureCamera = cam;
                VfxPoolItem.ViewPullCameraOverride = () => fixtureCamera;
                Vector3 toward = -cam.transform.forward;

                // (3) 로그 값
                var bearFit = new RecipientFit(.53f, new Vector3(0f, .01f, .02f), .22f);
                Require(Resolve(poison, bearFit, out Vector3 offPull) == null && offPull == Vector3.zero,
                    "Pull-off preset (poison) adds no log text and no pull.");
                Require(Resolve(actualBurn, bearFit, out Vector3 actualBurnPull) == null && actualBurnPull == Vector3.zero,
                    "Pull-off actual burn preset (v2) adds no log text and no pull (no ViewPull= in the burn play log).");
                Require(Resolve(burn, new RecipientFit(.53f, Vector3.zero), out Vector3 noRadiusPull) == "NoRadius" && noRadiusPull == Vector3.zero,
                    "Unknown body radius logs ViewPull=NoRadius and does not pull.");
                string expectedBear = (burn.ViewPullRadiusRatio * .22f).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                Require(Resolve(burn, bearFit, out Vector3 logPull) == expectedBear && VfxApprox(logPull, toward * (burn.ViewPullRadiusRatio * .22f)),
                    "Burn on a BearGuard-sized body logs the pull distance (ViewPull=" + expectedBear + ").");
                cam.orthographic = false;
                Require(Resolve(burn, bearFit, out _) == expectedBear + "@Perspective",
                    "A perspective camera is flagged in the log (orthographic premise broken).");
                cam.orthographic = true;
                fixtureCamera = null;
                Require(Resolve(burn, bearFit, out Vector3 noCamPull) == "NoCamera" && noCamPull == Vector3.zero,
                    "No camera logs ViewPull=NoCamera and does not pull (no exception).");
                fixtureCamera = cam;

                var pose = new GameObject("ViewPullPose") { hideFlags = HideFlags.HideAndDontSave };
                pose.transform.SetParent(root.transform, false);
                pose.transform.SetPositionAndRotation(new Vector3(2f, 0f, -1f), Quaternion.Euler(0f, 40f, 0f));
                int nextUnit = 98200;
                Vector3 BasePosition(EffectPreset preset, RecipientFit fit) => pose.transform.position
                    + pose.transform.rotation * (preset.LocalPositionOffset + fit.SafeFootOffset);

                // ── (4) 시험용 화상 프리셋(실제 v2 프리팹 + 비율 1.75) ──
                var instance = UnityEngine.Object.Instantiate(burn.VfxPrefab, root.transform);
                instance.SetActive(false);
                var item = instance.AddComponent<VfxPoolItem>();
                int returned = 0; item.Setup(burn.VfxPrefab, _ => returned++);
                var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                TimedDamageEffectPresentation PlayBurn(RecipientFit fit)
                {
                    var s = channel.Upsert(VfxUnit(nextUnit++), 1, TimedDamageVisual.Burn, 3f);
                    item.PlayTimedDamage(s, pose.transform, burn, channel, fit);
                    return s;
                }
                void DrainBurn()
                {
                    item.StopTimedDamage(item.DamageLease, "Expired");
                    // [2026-10-27 옛 줄 — 기록용] 이 fixture의 화상은 실제 v2 프리팹(Prewarm ON)이라 이 재현으로는 반환되지 않았다.
                    // foreach (var ps in systems) { ps.Simulate(30f, false, false, false); ps.Stop(false, ParticleSystemStopBehavior.StopEmitting); }
                    DrainTimedDamageFixtureSystems(systems, "ViewPull/" + burn.VfxPrefab.name);
                    item.Return();
                }

                float bearDistance = burn.ViewPullRadiusRatio * .22f;
                var bearState = PlayBurn(bearFit);
                Vector3 basePos = BasePosition(burn, bearFit);
                Vector3 moved = instance.transform.position - basePos;
                Require(VfxApprox(instance.transform.position, basePos + toward * bearDistance),
                    "Burn position = pose + pose rotation × (preset offset + foot offset) + camera pull.");
                Require(Mathf.Abs(Vector3.Dot(moved, cam.transform.right)) < 1e-4f && Mathf.Abs(Vector3.Dot(moved, cam.transform.up)) < 1e-4f
                    && Mathf.Abs(Vector3.Dot(moved, toward) - bearDistance) < 1e-4f,
                    "Orthographic screen coordinates (camera right/up components) stay unchanged; only depth moves toward the camera.");
                Vector3 playPull = DamagePull(item);
                bearState.Revision++; item.RefreshTimedDamage(item.DamageLease, bearState);
                pose.transform.SetPositionAndRotation(pose.transform.position + new Vector3(1f, 0f, .5f), Quaternion.Euler(0f, 200f, 0f));
                item.FollowTimedDamage(item.DamageLease, pose.transform);
                Require(VfxApprox(DamagePull(item), playPull)
                    && VfxApprox(instance.transform.position, BasePosition(burn, bearFit) + toward * bearDistance),
                    "Refresh/follow/target rotation keep the Play-time world pull (direction does not turn with the unit).");
                DrainBurn();
                Require(returned == 1 && DamagePull(item) == Vector3.zero, "Natural return clears the camera pull.");

                // (5) 반경 0 fit(기존 위치 검사 fixture와 같은 2인자 생성자) → 기존 공식 그대로
                var radiusZeroFit = new RecipientFit(2f, new Vector3(.05f, .02f, -.1f));
                PlayBurn(radiusZeroFit);
                Require(DamagePull(item) == Vector3.zero && VfxApprox(instance.transform.position, BasePosition(burn, radiusZeroFit)),
                    "Radius-0 fit (existing position fixture value) keeps the old position formula.");
                DrainBurn();
                // 넓은 대상 → Identity 재사용: 이전 당김이 남지 않는다(매 재생 덮어쓰기)
                PlayBurn(new RecipientFit(.521f, Vector3.zero, .413f));
                Require(DamagePull(item) != Vector3.zero, "Wide recipient pulls before the reuse check.");
                // 원복이 빠진 상황을 일부러 만든다: 재생 중 표시만 지우고(원복 없이) Identity로 다시 재생.
                typeof(VfxPoolItem).GetField("_active", VfxFields).SetValue(item, false);
                typeof(VfxPoolItem).GetField("_damageLoop", VfxFields).SetValue(item, false);
                PlayBurn(RecipientFit.Identity);
                Require(DamagePull(item) == Vector3.zero && VfxApprox(instance.transform.position, BasePosition(burn, RecipientFit.Identity)),
                    "Wide → identity reuse plays with zero pull even if a previous clear was missed (overwrite at Play).");
                DrainBurn();

                // (6) 재생 실패 거부 · 강제 정리 · 카메라 없음
                PlayBurn(bearFit);
                Require(DamagePull(item) != Vector3.zero, "Pulled play before the reject check.");
                int beforeReject = returned;
                item.RejectTimedDamageAcquire();
                Require(returned == beforeReject + 1 && DamagePull(item) == Vector3.zero, "Rejected acquire clears the camera pull.");
                PlayBurn(bearFit);
                var onDisable = typeof(VfxPoolItem).GetMethod("OnDisable", VfxFields);
                Require(onDisable != null, "VfxPoolItem forced-teardown method exists for the view-pull fixture.");
                onDisable.Invoke(item, null);
                Require(DamagePull(item) == Vector3.zero, "Forced teardown (OnDisable) clears the camera pull.");
                fixtureCamera = null;
                PlayBurn(bearFit); // 예외 없이 재생돼야 한다
                Require(DamagePull(item) == Vector3.zero && VfxApprox(instance.transform.position, BasePosition(burn, bearFit)),
                    "No camera → zero pull, old position, no exception.");
                DrainBurn();
                fixtureCamera = cam;

                // (7) 옵션이 꺼진 실제 독 프리셋
                var poisonInstance = UnityEngine.Object.Instantiate(poison.VfxPrefab, root.transform);
                poisonInstance.SetActive(false);
                var poisonItem = poisonInstance.AddComponent<VfxPoolItem>();
                int poisonReturned = 0; poisonItem.Setup(poison.VfxPrefab, _ => poisonReturned++);
                var poisonSystems = poisonInstance.GetComponentsInChildren<ParticleSystem>(true);
                poisonItem.PlayTimedDamage(channel.Upsert(VfxUnit(nextUnit++), 1, TimedDamageVisual.Poison, 3f), pose.transform, poison, channel, bearFit);
                Require(DamagePull(poisonItem) == Vector3.zero && VfxApprox(poisonInstance.transform.position, BasePosition(poison, bearFit)),
                    "Pull-off poison keeps the old position even with a radius and a camera.");
                poisonItem.StopTimedDamage(poisonItem.DamageLease, "Expired");
                foreach (var ps in poisonSystems) { ps.Simulate(30f, false, false, false); ps.Stop(false, ParticleSystemStopBehavior.StopEmitting); }
                poisonItem.Return();
                Require(poisonReturned == 1, "Pull-off poison returns naturally.");
                // [2026-10-26 v2 연결] 실제 화상 프리셋(당김 꺼짐): (4)와 같은 v2 인스턴스를 실제 프리셋으로 재생해 위치가 기존 공식 그대로인지 본다.
                //   같은 프리팹이므로 풀 재사용과 같은 상황이며, 직전 시험용 재생의 당김이 남지 않는 것도 함께 확인된다.
                fixtureCamera = cam;
                item.PlayTimedDamage(channel.Upsert(VfxUnit(nextUnit++), 1, TimedDamageVisual.Burn, 3f), pose.transform, actualBurn, channel, bearFit);
                Require(DamagePull(item) == Vector3.zero && VfxApprox(instance.transform.position, BasePosition(actualBurn, bearFit)),
                    "Pull-off actual burn (v2) keeps the old position even with a radius and a camera.");
                DrainBurn();

                // ── (8) 힐 경로 ──
                var healChannel = new BloomHealEffectChannel();
                var healInstance = UnityEngine.Object.Instantiate(heal.VfxPrefab, root.transform);
                healInstance.SetActive(false);
                var healItem = healInstance.AddComponent<VfxPoolItem>();
                int healReturned = 0; healItem.Setup(heal.VfxPrefab, _ => healReturned++);
                var healSystems = healInstance.GetComponentsInChildren<ParticleSystem>(true);
                var update = typeof(VfxPoolItem).GetMethod("Update", VfxFields);
                Require(update != null, "VfxPoolItem Update exists for the heal view-pull fixture.");
                void DrainHeal(ulong lease)
                {
                    healItem.StopBloom(lease, "FixtureStop");
                    foreach (var ps in healSystems) { ps.Simulate(30f, false, false, false); ps.Stop(false, ParticleSystemStopBehavior.StopEmitting); }
                    update.Invoke(healItem, null);
                }
                // 실제 힐 프리셋(꺼짐): 반경·카메라가 있어도 당김 0
                Require(healItem.BeginBloomAcquire(), "Heal fixture checkout begins.");
                ulong healLease = healItem.PlayBloomLoop(healChannel.Grant(VfxUnit(nextUnit++)), pose.transform, heal, bearFit, new BloomHealEffectDiagnostics());
                Require(healLease != 0 && BloomPull(healItem) == Vector3.zero
                    && VfxApprox(healInstance.transform.position, BasePosition(heal, bearFit)),
                    "Pull-off heal keeps the old position even with a radius and a camera.");
                DrainHeal(healLease);
                Require(healReturned == 1, "Pull-off heal returns naturally.");
                // 시험용 복제 프리셋(메모리에만 존재, 에셋 저장 없음) — 같은 힐 프리팹 + 비율 1.75
                pulledHeal = ScriptableObject.CreateInstance<EffectPreset>();
                pulledHeal.hideFlags = HideFlags.HideAndDontSave;
                typeof(EffectPreset).GetField("_vfxPrefab", VfxFields).SetValue(pulledHeal, heal.VfxPrefab);
                typeof(EffectPreset).GetField("_viewPullRadiusRatio", VfxFields).SetValue(pulledHeal, EffectPreset.RecommendedViewPullRadiusRatio);
                Require(healItem.BeginBloomAcquire(), "Heal fixture second checkout begins.");
                var pulledState = healChannel.Grant(VfxUnit(nextUnit++));
                healLease = healItem.PlayBloomLoop(pulledState, pose.transform, pulledHeal, bearFit, new BloomHealEffectDiagnostics());
                float healDistance = EffectPreset.RecommendedViewPullRadiusRatio * .22f;
                Require(healLease != 0 && VfxApprox(healInstance.transform.position, BasePosition(pulledHeal, bearFit) + toward * healDistance),
                    "Heal path with a pull ratio adds the same world camera pull.");
                Vector3 healPlayPull = BloomPull(healItem);
                var healRefresh = pulledState; healRefresh.Revision++;
                healItem.RefreshBloom(healLease, healRefresh);
                pose.transform.SetPositionAndRotation(pose.transform.position + new Vector3(-.5f, 0f, 1f), Quaternion.Euler(0f, 95f, 0f));
                healItem.FollowBloomPose(healLease, pose.transform);
                Require(VfxApprox(BloomPull(healItem), healPlayPull)
                    && VfxApprox(healInstance.transform.position, BasePosition(pulledHeal, bearFit) + toward * healDistance),
                    "Heal refresh/follow/target rotation keep the Play-time world pull.");
                DrainHeal(healLease);
                Require(healReturned == 2 && BloomPull(healItem) == Vector3.zero, "Heal natural return clears the camera pull.");
                Require(healItem.BeginBloomAcquire(), "Heal fixture reject checkout begins.");
                bloomPullField.SetValue(healItem, Vector3.one); // 거부 경로가 남은 당김을 비우는지 보기 위해 일부러 채운다
                healItem.RejectBloomAcquire();
                Require(healReturned == 3 && BloomPull(healItem) == Vector3.zero, "Rejected heal acquire clears the camera pull.");

                // ── (9) 실제 크기 표로 유닛별 당김 거리 기록(통과 조건 아님 — 표·비율은 사용자가 조정할 수 있다) ──
                string report = null;
                foreach (var type in new[] { UnitType.BearGuard, UnitType.Tank, UnitType.CannonCart, UnitType.StreamSpirit,
                    UnitType.LittleKnight, UnitType.RabbitTrickster })
                {
                    // 실제 화상 프리셋 기준(지금은 당김 꺼짐 → 모든 유닛 "Off"가 정상).
                    string entry = type + ":" + (Resolve(actualBurn, units.GetRecipientFit(type), out _) ?? "Off");
                    report = report == null ? entry : report + "|" + entry;
                }
                GameLog.Dev.Info("Combat", "RunUnitActionSelfValidation",
                    "화상 카메라 쪽 당김 — 실제 화상 프리셋·크기 표 기준 유닛별 당김 거리(m) = 몸통 반경 × 프리셋 비율. Off = 꺼짐(v2 연결 후 정상)",
                    "ViewPull=" + report);
            }
            finally
            {
                VfxPoolItem.ViewPullCameraOverride = null;
                UnityEngine.Object.DestroyImmediate(burn); // 메모리 전용 시험용 화상 프리셋 정리(에셋 아님)
                if (pulledHeal != null) UnityEngine.Object.DestroyImmediate(pulledHeal);
                UnityEngine.Object.DestroyImmediate(root);
                channel.CloseVisuals("FixtureEnd");
            }
        }

        /// <summary>
        /// [2026-10-13 B] 정령 타워(RuneSpire) 번개: 프리팹 계층 계약(D-4), 거리 맞춤, 원래 대상 추적,
        /// 사망/교체 시 영구 고정(ID 재사용 방어), 반환 시 원본 복원, Human 타워 무추적을 확인한다.
        /// EditMode에서는 LateUpdate가 자동으로 돌지 않으므로 기존 fixture처럼 리플렉션으로 직접 호출한다.
        /// </summary>
        private static void ValidateRuneSpireBoltEndpoint(UnitEffectConfig units, BuildingEffectConfig buildings)
        {
            var rune = buildings.GetAttack(BuildingType.AutoTower, RaceId.Spirit);
            // (a) 실제 preset 프리팹 계층: Bolt는 루트 직속 자식, Impact_Sparks는 Bolt의 자식(로컬 Z=1).
            Transform authoredBolt = rune.VfxPrefab.transform.Find("Bolt");
            Require(authoredBolt != null && authoredBolt.Find("Impact_Sparks") != null
                && rune.VfxPrefab.transform.Find("Impact_Sparks") == null
                && Mathf.Approximately(authoredBolt.Find("Impact_Sparks").localPosition.z, 1f),
                "RuneSpire hierarchy contract (D-4): Impact_Sparks is a child of Bolt at local Z=1.");
            Vector3 authoredBoltScale = authoredBolt.localScale;

            var root = new GameObject("RuneSpireBoltFixture") { hideFlags = HideFlags.HideAndDontSave };
            var targetGo = new GameObject("RuneSpireBoltTarget") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var manager = root.AddComponent<EffectManager>();
                typeof(EffectManager).GetField("_vfxContainer", VfxFields).SetValue(manager, root.transform);
                manager.Initialize(units, buildings, null);
                var view = targetGo.AddComponent<UnitView>();
                var data = VfxUnit(98010, UnitType.LittleKnight, TeamId.Red);
                typeof(UnitView).GetField("_unitData", VfxFields).SetValue(view, data);
                targetGo.transform.position = new Vector3(3f, 0f, 4f);
                var target = new TowerBoltTarget(targetGo.transform, view, data, null);
                var lateUpdate = typeof(VfxPoolItem).GetMethod("LateUpdate", VfxFields);
                Require(lateUpdate != null, "Tower bolt per-frame endpoint method exists for the lifecycle fixture.");

                var s = new AttackEffectPresentation { Match = 1, Lifetime = 1, Sequence = 1, AttackerId = 98011, TargetId = data.Id,
                    Visual = AttackEffectVisual.Tower, Race = RaceId.Spirit, Source = Vector3.zero, Impact = new Vector3(0f, 0f, 2f), ImpactTime = 1d };
                Require(manager.PlayAttackEffect(s, s.Source, s.Impact, out string outcome, target)
                    && outcome.Contains("Endpoint=Tracked"), "Spirit tower bolt resolves the original target and tracks it.");
                var item = root.GetComponentsInChildren<VfxPoolItem>(true)[0];
                var bolt = item.transform.Find("Bolt");
                // [2026-10-13 R2-2 비활성화] 높이를 무시한 XZ 기대값. 아래 3D 기대값으로 대체. 최종 삭제는 사용자 테스트 통과 후.
                // Quaternion Facing(Vector3 endpoint) { var d = endpoint - s.Source; d.y = 0f; return Quaternion.LookRotation(d); }
                // Vector3 RootAt(Vector3 endpoint) => s.Source + Facing(endpoint) * rune.LocalPositionOffset;
                // Vector3 ForwardAt(Vector3 endpoint) => Facing(endpoint) * Quaternion.Euler(rune.EulerRotationOffset) * Vector3.forward;
                // float LengthAt(Vector3 endpoint)
                // { var span = endpoint - RootAt(endpoint); span.y = 0f; return span.magnitude / item.transform.lossyScale.z; }
                // bool BoltMatches(Vector3 endpoint) => Mathf.Abs(bolt.localScale.z - LengthAt(endpoint)) < 1e-4f
                //     && VfxApprox(item.transform.forward, ForwardAt(endpoint)) && VfxApprox(item.transform.position, RootAt(endpoint));

                // [R2-2] 기대값은 실제 preset 보정값(크리스탈 높이 offset 등)과 실제 config의 가슴 높이를 그대로 반영해 계산한다.
                //   → 사용자가 나중에 preset·비율·크기 표를 조정해도 "계약"(시작=타워+yaw 보정, 끝=발밑+가슴, 3D 조준, 길이=3D 거리)만 검사한다.
                //   대상은 LittleKnight라 크기 표 값(또는 0.6m 대체값)이 그대로 쓰인다. 해석 실패(대상 데이터 없음)는 0.6m 대체값.
                float trackedChest = units.GetChestHeight(data.Type);
                Vector3 Chest(Vector3 feet, float h) => feet + Vector3.up * h;
                Quaternion Yaw(Vector3 endpoint) { var d = endpoint - s.Source; d.y = 0f; return Quaternion.LookRotation(d); }
                Vector3 RootAt(Vector3 endpoint) => s.Source + Yaw(endpoint) * rune.LocalPositionOffset;
                Quaternion Aim(Vector3 endpoint) => Quaternion.LookRotation(endpoint - RootAt(endpoint));
                Vector3 ForwardAt(Vector3 endpoint) => Aim(endpoint) * Quaternion.Euler(rune.EulerRotationOffset) * Vector3.forward;
                float LengthAt(Vector3 endpoint) => (endpoint - RootAt(endpoint)).magnitude / item.transform.lossyScale.z;
                bool BoltMatchesEndpoint(Vector3 endpoint) => Mathf.Abs(bolt.localScale.z - LengthAt(endpoint)) < 1e-4f
                    && VfxApprox(item.transform.forward, ForwardAt(endpoint)) && VfxApprox(item.transform.position, RootAt(endpoint));
                // 추적 대상(LittleKnight)의 발밑 위치를 넣으면 가슴 높이 끝점으로 바꿔 검사한다.
                bool BoltMatches(Vector3 feet) => BoltMatchesEndpoint(Chest(feet, trackedChest));

                // [R2-2] preset이 시작점을 크리스탈 높이로 올리고, 그 보정은 yaw로만 돌아 Y가 그대로 월드 높이가 된다.
                Require(rune.LocalPositionOffset.y > 0f, "RuneSpire preset raises the bolt source to the crystal height.");
                Require(trackedChest > 0f, "Tracked recipient chest height is above the feet.");

                // 시작 시점: 스냅샷 Impact(0,0,2)가 아니라 대상의 현재 위치(3,0,4)의 가슴 높이까지 맞춘다.
                Vector3 first = targetGo.transform.position;
                Require(item.TowerBoltTracking && BoltMatches(first)
                    && Mathf.Approximately(bolt.localScale.x, authoredBoltScale.x) && Mathf.Approximately(bolt.localScale.y, authoredBoltScale.y),
                    "Bolt Z scale equals 3D crystal-to-chest distance and only Z changes; root aims at the chest.");
                Require(Mathf.Abs(item.transform.position.y - (s.Source.y + rune.LocalPositionOffset.y)) < 1e-4f,
                    "Bolt source height = tower position + preset offset Y (yaw-only offset rotation).");

                // (b) 대상이 움직이면 길이·방향이 따라간다. 이번에는 높이 차(대상 Y=1)까지 3D로 반영되어야 한다.
                targetGo.transform.position = new Vector3(0f, 1f, -6f);
                Vector3 moved = targetGo.transform.position;
                lateUpdate.Invoke(item, null);
                Require(item.TowerBoltTracking && BoltMatches(moved),
                    "Moving original target updates bolt 3D length/direction; source stays fixed.");
                Vector3 movedAim = Chest(moved, trackedChest) - RootAt(Chest(moved, trackedChest));
                Require(Mathf.Abs(movedAim.y) < 1e-4f || Mathf.Sign(item.transform.forward.y) == Mathf.Sign(movedAim.y),
                    "Bolt root pitches toward the chest when the endpoint height differs from the crystal.");

                // (c) 대상 사망 → 마지막 끝점에서 영구 고정, 이후 이동해도 따라가지 않는다.
                data.TakeDamage(99999);
                lateUpdate.Invoke(item, null);
                targetGo.transform.position = new Vector3(5f, 0f, 0f);
                lateUpdate.Invoke(item, null);
                Require(!data.IsAlive && !item.TowerBoltTracking && BoltMatches(moved),
                    "Dead target freezes the bolt at the last known endpoint.");

                // (d) Return → Bolt 원본 스케일 복원, 대상 참조 해제.
                item.Return();
                Require(VfxApprox(bolt.localScale, authoredBoltScale) && !item.TowerBoltTracking
                    && ((TowerBoltTarget)typeof(VfxPoolItem).GetField("_boltTarget", VfxFields).GetValue(item)).View == null,
                    "Pool return restores the authored Bolt scale and drops the target reference.");

                // (c') ID 재사용 방어: 같은 View에 같은 ID의 "다른" UnitData가 들어오면 따라가지 않고 고정한다.
                var reused = VfxUnit(98012, UnitType.LittleKnight, TeamId.Red);
                typeof(UnitView).GetField("_unitData", VfxFields).SetValue(view, reused);
                targetGo.transform.position = new Vector3(0f, 0f, 3f);
                var s2 = s; s2.Sequence = 2; s2.TargetId = reused.Id;
                Require(manager.PlayAttackEffect(s2, s2.Source, s2.Impact, out outcome, new TowerBoltTarget(targetGo.transform, view, reused, null))
                    && ReferenceEquals(root.GetComponentsInChildren<VfxPoolItem>(true)[0], item), "Pooled bolt item is reused.");
                Vector3 reusedEndpoint = targetGo.transform.position;
                Require(item.TowerBoltTracking && BoltMatches(reusedEndpoint), "Reused bolt has no leftover length from the previous play.");
                typeof(UnitView).GetField("_unitData", VfxFields).SetValue(view, VfxUnit(reused.Id, UnitType.LittleKnight, TeamId.Red));
                targetGo.transform.position = new Vector3(0f, 0f, 8f);
                lateUpdate.Invoke(item, null);
                Require(!item.TowerBoltTracking && BoltMatches(reusedEndpoint),
                    "Replaced UnitData on the same View (ID reuse) freezes instead of re-acquiring.");
                item.Return();

                // (e) 해석 실패 → 추적 없이 스냅샷 Impact까지 길이만 맞춘다.
                var s3 = s; s3.Sequence = 3;
                // [R2-2] 대상 데이터가 없으니 가슴 높이는 0.6m 대체값을 쓴다.
                Require(manager.PlayAttackEffect(s3, s3.Source, s3.Impact, out outcome, default, "TargetViewMissing")
                    && outcome.Contains("Endpoint=Untracked(TargetViewMissing)") && !item.TowerBoltTracking
                    && BoltMatchesEndpoint(Chest(s3.Impact, UnitEffectConfig.FallbackChestHeight)),
                    "Unresolved target plays to the snapshot Impact at the fallback chest height without tracking.");
                item.Return();

                // (f) Human 타워는 추적·길이 맞춤 경로를 타지 않는다(현행 유지).
                var human = s; human.Sequence = 4; human.Race = RaceId.Human;
                Require(manager.PlayAttackEffect(human, human.Source, human.Impact, out outcome, target)
                    && !outcome.Contains("Endpoint"), "Human tower attack effect ignores endpoint tracking.");
                foreach (var pooled in root.GetComponentsInChildren<VfxPoolItem>(true))
                    Require(!pooled.TowerBoltTracking, "No pooled item tracks after the Human tower play.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
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
                    "The actual Mushroom tracer prefab is wired (the server projectile presentation draws it).");
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
                // ────────────────────────────────────────────────────────────────
                // [RM-8 — 2026-10-27 서버 권위 투사체 단계 E, 결정 E-D (가)] MushroomBomber는 서버 투사체 유닛이라
                //   ExecuteAttack(= 서버 착탄 순간)은 "피해 순간 스냅샷"을 발행하지 않는다. 포자 비행·착탄 이펙트는 서버 발사/착탄 기록
                //   경로(ProjectilePresentationPresenter)가 그리고, 그 경로는 ServerProjectiles 검증 E-2가 확인한다.
                //   그래서 이 fixture의 Mushroom 부분은 "스냅샷 발행 0건 · 독 출처(origin)는 그대로 채워짐"을 본다
                //   (발행 0건은 옛 발행이 다시 들어오는 것을 막는 재발 방지 단언이다).
                //   스냅샷 큐 자체(중복 거부 · 잘못된 값 거부 · 수명 종료 뒤 무시)는 Spirit RuneSpire Tower 스냅샷이 계속 쓰므로
                //   아래 타워 공격 뒤에서 Tower 스냅샷으로 확인한다.
                //   [P5 정리] 큐의 MushroomImpact 재생 갈래(옛 0.1초 트레이서 · 옛 due 착탄)를 코드에서 삭제해 그 재생 단언도 지웠다.
                // ────────────────────────────────────────────────────────────────
                Require(ServerProjectileDeliveryPolicy.IsServerProjectile(UnitType.MushroomBomber),
                    "[E-2 RM-8] Fixture precondition: MushroomBomber is on the server projectile switch.");
                Require(!snapshot.IsValid, "[E-2 RM-8] A switched-on Mushroom impact publishes no legacy writer snapshot (no double impact VFX).");
                var pending = (IList)typeof(HitPresentationQueue).GetField("_attackEffectPending", VfxFields).GetValue(queue);
                // 독 출처: 발행은 끊었지만 스냅샷 "생성"(damageOrigin)은 그대로라, 독 상태가 공격 출처 키를 그대로 가진다.
                Require(combat.TimedDamageEffects.TryGet(target, out var effect) && effect.Visual == TimedDamageVisual.Poison
                        && effect.AttackSequence != 0UL && effect.AttackMatch != 0UL,
                    "[E-2 RM-8] The poison keeps its attack provenance (damageOrigin is still created, only the publish is off).");
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
                // [RM-8 — 단계 E] 위에서 옮겨 온 스냅샷 큐 확인 — 이제 Mushroom 스냅샷이 없으므로 RuneSpire Tower 스냅샷으로 본다.
                //   (Tower 스냅샷은 비행 없이 큐에 1건 대기하다가 due에 착탄 표현을 낸다 — 중복이면 다시 쌓이지 않아야 한다.)
                int towerPending = pending.Count;
                Require(towerPending == 1, "[E-2 RM-8] The RuneSpire Tower snapshot is queued once by the actual queue.");
                GameEvents.OnAttackEffectConfirmed.OnNext(snapshot);
                Require(pending.Count == towerPending, "Duplicate snapshot is not queued (or played) twice.");
                var bad = snapshot; bad.Visual = (AttackEffectVisual)255; bad.Sequence++;
                Require(!bad.IsValid, "Invalid attack enum rejected.");
                bad = snapshot; bad.Visual = AttackEffectVisual.Tower; bad.Race = (RaceId)255;
                Require(!bad.IsValid, "Invalid tower race rejected.");
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
