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
        ///      타격 marker = 투사체(트레이서) 1발·charge 재생 없음 / 도착 = 로컬 피격 신호 1회 + 착탄 VFX.
        ///   ② 차지 도중 취소(허가 차단): charge 즉시 정지, 이후 타격에서 투사체·charge 없음 (사용자 결정 D2).
        ///   ③ Stop이 타격보다 먼저(SourceOnly): charge 즉시 정지, 이후 타격에서 투사체·charge 없음 (D2).
        ///   ④ 싱글(lease 없음): 차지 = charge 1개, 타격 = 투사체 1발·charge 재생 없음.
        /// [2026-10-21 서버 권위 투사체 단계 D] 두 유닛이 서버 투사체로 바뀌어 ①·④의 "타격 marker = 트레이서 1발 · 도착 = 피격 신호"는
        ///   "타격 marker = lease 소비만(로컬 트레이서·피격 신호 없음)"으로 바뀌었다(옛 검사는 V-7 주석으로 보존).
        ///   서버 발사 기록으로 그리는 투사체·취소 통보로 끄는 차지는 ServerProjectiles의 D-6이 확인한다.
        /// 발사음(SFX)은 AudioManager를 격리했으므로 여기서 관측하지 않는다(코드 위치는 UnitView R-4 주석 — 실기 확인 항목).
        /// EditMode에서는 Update가 돌지 않으므로 트레이서 도착(Arrive)은 리플렉션으로 직접 호출한다.
        /// </summary>
        private static void ValidateChargeUnitLaunchTiming(UnitEffectConfig units, BuildingEffectConfig buildings)
        {
            Require(!UnityEngine.Application.isPlaying, "Charge-unit launch fixture must not run in a match.");
            var effectSingleton = typeof(EffectManager).GetField(
                "<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var audioSingleton = typeof(SingletonMonoBehaviour<AudioManager>).GetField(
                "<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var arrive = typeof(TracerProjectile).GetMethod("Arrive", VfxFields);
            Require(effectSingleton != null && audioSingleton != null && arrive != null,
                "Charge-unit launch fixture seams (EffectManager/AudioManager singletons, tracer arrival) must exist.");
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
                TracerProjectile ActiveTracer(GameObject prefab)
                {
                    foreach (var t in root.GetComponentsInChildren<TracerProjectile>(true))
                        if (IsTracerOf(t, prefab)) return t;
                    return null;
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
                    // [V-7 비활성화 — 2026-10-21 서버 권위 투사체 단계 D] 두 차지 유닛은 이제 서버 투사체 유닛이다.
                    //   타격 marker는 lease만 소비하고 로컬 트레이서를 쏘지 않는다 — 투사체는 서버 발사 기록을 화면 소비자
                    //   (ProjectilePresentationPresenter)가 그리고, 착탄 VFX·숫자는 서버 착탄 결과로 낸다(단계 D fixture D-6·소스 게이트).
                    //   옛 검사(주석 보존 — C 사용자 테스트 PASS 뒤 정리):
                    // view.OnAttackHit(hitEvent);
                    // Require(ActiveTracers(tracer.VfxPrefab) == tracersBefore + 1 && Active(charge.VfxPrefab) == 1,
                    //     type + " launch marker fires one tracer and does not replay the charge VFX.");
                    // TracerProjectile flying = ActiveTracer(tracer.VfxPrefab);
                    // Require(flying != null, type + " launched tracer is flying.");
                    // int hitsBefore = localHits, impactsBefore = Active(impact.VfxPrefab);
                    // arrive.Invoke(flying, null);
                    // Require(localHits == hitsBefore + 1 && Active(impact.VfxPrefab) == impactsBefore + 1,
                    //     type + " tracer arrival raises one local-hit signal and plays one attacker impact VFX.");
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
                    // [V-7 비활성화 — 2026-10-21 단계 D] 옛 싱글 검사(로컬 트레이서 1발 → 도착 시 피격 신호 1회) — 주석 보존:
                    // view.OnAttackHit(hitEvent);
                    // Require(ActiveTracers(tracer.VfxPrefab) == tracersBefore + 1 && Active(charge.VfxPrefab) == 1,
                    //     type + " SOLO launch fires one tracer and does not replay the charge VFX.");
                    // TracerProjectile soloFlying = ActiveTracer(tracer.VfxPrefab);
                    // Require(soloFlying != null, type + " SOLO launched tracer is flying.");
                    // hitsBefore = localHits;
                    // arrive.Invoke(soloFlying, null);
                    // Require(localHits == hitsBefore + 1 && ActiveTracers(tracer.VfxPrefab) == tracersBefore,
                    //     type + " SOLO tracer arrival raises one local-hit signal and returns the tracer to its pool.");
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
        /// Legacy 트레이서가 "도착한 순간" 공격자 타입의 착탄 이펙트(impactPreset)를 도착 지점에서 터뜨리는지 확인한다.
        /// (a) 설정: StreamSpirit/Tank/CannonCart의 착탄 이펙트는 공격자 기준 impact 슬롯에 있고,
        ///     "맞은 유닛 타입" 기준인 hit 슬롯은 비어 있다. StreamSpirit 트레이서 프리팹 루트에는
        ///     TracerProjectile이 미리 붙어 있어 프리팹의 느린 속도(_speed)가 실제로 쓰인다.
        /// (b) 실제 EffectManager 경로: 비행 중에는 착탄 이펙트 없음 → 도착하면 기존 콜백이 먼저 1회 실행되고
        ///     그 다음 착탄 이펙트 1개가 도착 지점에 재생된다. 플래그를 안 넘긴 호출, 트레이서가 없는 타입(즉시 콜백 폴백),
        ///     MushroomBomber(서버 확정 스냅샷 경로가 착탄을 단독 소유)는 착탄 이펙트를 만들지 않는다.
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
            //   옛 단언(R5, 기록용 — 주석 보존):
            //   Require(units.GetImpact(UnitType.Tank) == cannonImpact && units.GetHit(UnitType.Tank) == null
            //       && units.GetImpact(UnitType.CannonCart) == cannonImpact && units.GetHit(UnitType.CannonCart) == null,
            //       "[R5] Tank/CannonCart impact is attacker-keyed (impact slot), victim-keyed hit slot is empty.");
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

                // ① StreamSpirit + 플래그 true: 비행 중엔 없음 → 도착 시 콜백 먼저, 그 다음 착탄 1개.
                Vector3 start = new Vector3(0f, 0.5f, 0f);
                Vector3 target = new Vector3(2f, 0f, 0f);
                int callbackCount = 0, impactsSeenInCallback = -1;
                manager.PlayTracer(UnitType.StreamSpirit, start, target,
                    () => { callbackCount++; impactsSeenInCallback = ActiveImpacts(streamImpact.VfxPrefab); },
                    playImpactOnArrive: true);
                var tracer = ActiveTracer();
                Require(tracer != null && callbackCount == 0 && ActiveImpacts(streamImpact.VfxPrefab) == 0,
                    "[R5] While the StreamSpirit tracer is flying there is no splash and no arrival callback yet.");
                arrive.Invoke(tracer, null);
                Require(callbackCount == 1 && impactsSeenInCallback == 0 && ActiveImpacts(streamImpact.VfxPrefab) == 1,
                    "[R5] On arrival the existing callback runs once first, then exactly one attacker splash plays.");
                VfxPoolItem splash = null;
                foreach (var item in root.GetComponentsInChildren<VfxPoolItem>(true))
                    if (item.SourcePrefab == streamImpact.VfxPrefab && item.gameObject.activeSelf) splash = item;
                Quaternion flight = Quaternion.LookRotation(new Vector3(target.x - start.x, 0f, target.z - start.z));
                Require(splash != null && VfxApprox(splash.transform.position, target + flight * streamImpact.LocalPositionOffset),
                    "[R5] Splash plays at the tracer arrival point (target) with the preset offset applied.");
                splash.Return();

                // ② 플래그를 넘기지 않은 호출(기존 호출 방식)은 착탄 이펙트를 만들지 않는다.
                int plainCallbacks = 0;
                manager.PlayTracer(UnitType.StreamSpirit, start, target, () => plainCallbacks++);
                tracer = ActiveTracer();
                Require(tracer != null, "[R5] Plain tracer call launches a tracer.");
                arrive.Invoke(tracer, null);
                Require(plainCallbacks == 1 && ActiveImpacts(streamImpact.VfxPrefab) == 0,
                    "[R5] A tracer call without the arrival-impact flag keeps the old behavior (no splash).");

                // ③ 트레이서가 없는 타입: 콜백은 즉시 실행, 착탄 이펙트는 재생하지 않는다(폴백 유지).
                //   [2026-10-20 단계 B] 옛 예시 Tank는 트레이서 프리셋이 연결돼(EffectPreset_Cannon_Tracer) 이 블록이 실행되지 않았고,
                //   이제 서버 투사체 유닛이라 R5 경로를 쓰지 않는다. 트레이서가 없는 Pistoleer로 같은 폴백 계약을 확인한다.
                //   옛 블록(기록용 — 주석 보존):
                //   if (units.GetTracer(UnitType.Tank)?.VfxPrefab == null)
                //   {
                //       int tankCallbacks = 0;
                //       manager.PlayTracer(UnitType.Tank, start, target, () => tankCallbacks++, playImpactOnArrive: true);
                //       Require(tankCallbacks == 1 && ActiveImpacts(cannonImpact.VfxPrefab) == 0 && ActiveTracer() == null,
                //           "[R5] No tracer prefab: callback runs immediately and no arrival impact is played.");
                //   }
                if (units.GetTracer(UnitType.Pistoleer)?.VfxPrefab == null)
                {
                    int fallbackCallbacks = 0;
                    var pistoleerImpact = units.GetImpact(UnitType.Pistoleer);
                    manager.PlayTracer(UnitType.Pistoleer, start, target, () => fallbackCallbacks++, playImpactOnArrive: true);
                    Require(fallbackCallbacks == 1 && ActiveTracer() == null
                        && (pistoleerImpact?.VfxPrefab == null || ActiveImpacts(pistoleerImpact.VfxPrefab) == 0),
                        "[R5] No tracer prefab: callback runs immediately and no arrival impact is played.");
                }

                // ④ MushroomBomber: 플래그가 true여도 Legacy 도착 착탄을 만들지 않는다(스냅샷 경로가 단독 소유).
                var mushroomImpact = units.GetImpact(UnitType.MushroomBomber);
                Require(mushroomImpact?.VfxPrefab != null && units.GetTracer(UnitType.MushroomBomber)?.VfxPrefab != null,
                    "[R5] Mushroom impact/tracer presets exist for the exclusion check.");
                manager.PlayTracer(UnitType.MushroomBomber, start, target, null, playImpactOnArrive: true);
                tracer = ActiveTracer();
                Require(tracer != null, "[R5] Mushroom tracer launches through the generic call for the exclusion check.");
                arrive.Invoke(tracer, null);
                Require(ActiveImpacts(mushroomImpact.VfxPrefab) == 0,
                    "[R5] MushroomBomber never gets a Legacy arrival impact (confirmed snapshot path owns its impact).");
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
                foreach (var ps in systems) { ps.Simulate(30f, false, false, false); ps.Stop(false, ParticleSystemStopBehavior.StopEmitting); }
                item.Return();
                Require(returned == 2 && VfxApprox(instance.transform.localScale, authoredScale),
                    "Fitted timed damage natural return restores the authored scale.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); channel.CloseVisuals("FixtureEnd"); }
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

        /// <summary>
        /// [2026-10-26 상태 이펙트 폭 맞춤] 화상 불꽃을 몸통 폭에 맞추는 기능을 확인한다.
        ///   (1) 실제 프리셋 설정: 화상만 켜짐(상한 3), 독·힐은 꺼짐 — 사용자 결정 D1·D2. 결정이 바뀌면 이 줄도 함께 바꾼다.
        ///   (2) 순수 공식: 반올림(D3, 2.5 → 3) · 윤곽 안 반경(D4) · 상한(D2) · 잘못된 입력은 폭 맞춤 없음.
        ///   (3) 실제 화상 프리팹: 넓은 대상 → 방출 켜진 시스템만 범위·개수가 공식대로, Burst·방출 꺼진 루트는 그대로,
        ///       같은 대상 갱신은 재계산 없음, 자연 반환 뒤 원본 복원, 넓은 → 좁은/Identity 재사용 시 원본 그대로.
        ///   (4) 실제 크기 표로 유닛별 결과를 로그에 남긴다(곰·탱크·대포수레·스트림·래빗) — 사용자 육안 확인과 대조용.
        ///   (5) 재생 실패 거부 · 강제 정리 경로 원복, 원복이 빠진 채 다음 재생이 시작돼도 먼저 원본으로 돌아감(이중 안전장치).
        ///   (6) 옵션이 꺼진 실제 독 프리셋: 넓은 대상이어도 모든 값이 원본 그대로.
        ///   힐(옵션 꺼짐)의 "넓은 대상이어도 그대로"는 기존 Bloom fixture(RunUnitActionSelfValidation.cs)에 덧붙였다.
        /// EditMode에서는 OnDisable이 자동으로 불리지 않으므로 기존 fixture처럼 리플렉션으로 직접 호출한다.
        /// </summary>
        private static void ValidateTimedDamageWidthFit(UnitEffectConfig units)
        {
            var burn = units.GetTimedDamage(UnitType.InfernoSpirit);
            var poison = units.GetTimedDamage(UnitType.MushroomBomber);
            var heal = units.GetHeal(UnitType.BloomFairy);
            Require(burn?.VfxPrefab != null && poison?.VfxPrefab != null && heal?.VfxPrefab != null,
                "Burn, poison and heal recipient presets exist for the width-fit fixture.");

            // (1) 사용자 결정 D1(화상만 켬, 독·힐 현행 유지) · D2(상한 3)
            Require(burn.WidthFitEnabled && burn.WidthFitMaxCount == EffectPreset.DefaultWidthFitMaxCount,
                "Burn preset enables recipient width fit with max count 3 (user decision D1/D2, 2026-10-26).");
            Require(!poison.WidthFitEnabled && !heal.WidthFitEnabled,
                "Poison and heal presets keep width fit off (user decision D1 — current behavior preserved).");

            // (2) 순수 공식. 기대값은 Plan 1절 표의 계산과 같은 방식(불꽃 폭 = 0.55 × 키)으로 직접 적었다.
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

            var root = new GameObject("WidthFitFixture") { hideFlags = HideFlags.HideAndDontSave };
            var channel = new TimedDamageEffectChannel();
            try
            {
                var pose = new GameObject("WidthFitPose") { hideFlags = HideFlags.HideAndDontSave };
                pose.transform.SetParent(root.transform, false);
                pose.transform.SetPositionAndRotation(new Vector3(2f, 0f, -1f), Quaternion.Euler(0f, 40f, 0f));
                int nextUnit = 98100;

                // ── (3) 실제 화상 프리팹 ──
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
                    + "(if the burn prefab flame was resized, re-check the expected width-fit result).");
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
                    "화상 폭 맞춤 — 실제 크기 표 기준 유닛별 불꽃 개수@방출 반경(m). 1 = 예전과 같은 한 점 방출", "WidthFit=" + report);

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
            }
            finally { UnityEngine.Object.DestroyImmediate(root); channel.CloseVisuals("FixtureEnd"); }
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
