using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Core;
using Hexiege.Domain;
using Hexiege.Infrastructure;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UniRx;

namespace Hexiege.Editor.Combat
{
    /// <summary>
    /// Tracer A의 순수 C# 계약을 Unity Editor에서 즉시 점검하는 도구다.
    /// 씬, 프리팹, Animator, NGO 실행 환경 없이도 행동 단계 순서, 공격 회차 증가,
    /// 각도 히스테리시스, 결과 중복 제거와 역순 정렬이 계약대로 동작하는지 확인한다.
    /// 메뉴에서 Hexiege/Combat/Run Full Regression Suite을 선택해 실행한다.
    /// </summary>
    public static partial class RunUnitActionSelfValidation
    {
        // Service wiring only: production controller still owns revision/E/receipt and
        // production View/NetworkUnit own actual animation/application. No fabricated success.
        private sealed class NativeHostServices : IGameServices, IUnitFactory
        {
            public GameObject Unit;
            public UnitCombatUseCase Combat;
            public int CombatReads, UnitReads;
            public UnitSpawnUseCase RetrySpawn;
            public IUnitFactory RetryFactory;
            public GameObject GetUnitObject(int id) { UnitReads++; return Unit; }
            public void RegisterUnitObject(int id, GameObject obj) => Unit = obj;
            public bool InitializeUnitView(UnitData data) { Unit.GetComponent<Hexiege.Presentation.UnitView>().Initialize(data); return true; }
            public IUnitFactory GetUnitFactory() => RetryFactory ?? this;
            public HexGrid GetGrid() => null;
            public ResourceUseCase GetResource() => null;
            public BuildingPlacementUseCase GetBuildingPlacement() => null;
            public UnitProductionUseCase GetUnitProduction() => null;
            public UnitSpawnUseCase GetUnitSpawn() => RetrySpawn;
            public PopulationUseCase GetPopulation() => null;
            public UnitMovementUseCase GetMovement() => null;
            public UnitCombatUseCase GetCombatUseCase() { CombatReads++; return Combat; }
            public TowerCombatUseCase GetTowerCombat() => null;
            public UnitUpgradeUseCase GetUpgradeUseCase() => null;
            public SkillActivationUseCase GetSkillActivationUseCase() => null;
            public StatusEffectSystem GetStatusEffectSystem() => null;
            public MistShrineUseCase GetMistShrineUseCase() => null;
            public void StartNetworkGame(TeamId team) { }
            public bool IsNetworkGameStarted => true;
        }

        public static void ValidatePistoleerHostPublicationChain()
        {
            bool active = NetworkContext.IsNetworkActive, server = NetworkContext.IsNetworkServer;
            GameObject root = null, owner = null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                NetworkContext.Set(true, true);
                root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Units/Human/Unit_Pistoleer_Blue.prefab"));
                root.hideFlags = HideFlags.HideAndDontSave;
                var services = new NativeHostServices { Unit = root };
                var view = root.GetComponent<Hexiege.Presentation.UnitView>();
                var network = root.GetComponent<NetworkUnit>();
                var animator = root.GetComponentInChildren<Animator>();
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/Units/Pistoleer/Pistoleer_Attack.anim");
                var data = new UnitData(905, UnitType.Pistoleer, TeamId.Blue, new HexCoord(0, 0), 100, 10, 4, 2);
                UnitFactory.ApplyAttackClipCooldown(data, UnitFactory.GetAttackClipLength(clip));
                data.HitFrameTimes = UnitFactory.GetHitFrameTimes(clip);
                services.InitializeUnitView(data); animator.Rebind(); animator.Update(0);
                typeof(Hexiege.Presentation.UnitView).GetMethod("BeginServerActionRotation", flags)
                    .Invoke(view, new object[] { 906, true });
                double clock = 73.010231;
                typeof(NetworkUnit).GetField("_attackPhaseFixtureClock", flags).SetValue(network, (Func<double>)(() => clock));
                typeof(NetworkUnit).GetField("_attackPhaseLifetime", flags).SetValue(network, 1UL);
                owner = new GameObject("NativeHostPublicationChain") { hideFlags = HideFlags.HideAndDontSave };
                owner.AddComponent<Unity.Netcode.NetworkObject>();
                var controller = owner.AddComponent<NetworkCombatController>();
                typeof(NetworkCombatController).GetField("_services", flags).SetValue(controller, services);
                typeof(NetworkCombatController).GetField("_attackPresentationFixtureClock", flags).SetValue(controller, (Func<double>)(() => clock));
                int published = 0;
                UnitAttackPhasePresentation wire = default;
                typeof(NetworkUnit).GetField("_attackPhaseFixturePublish", flags).SetValue(network,
                    (Action<UnitAttackPhasePresentation>)(value => {
                        Require(view.HasNativeAttackEpoch(value.Lifetime, value.EpochId)
                            && view.IsServerCombatPresentationSatisfied(value.TargetId, value.TargetIsUnit, value.PresentationRevision),
                            "Transport cannot precede actual Host native application.");
                        wire = value; published++;
                    }));
                var begin = typeof(NetworkCombatController).GetMethod("BeginProvisionalAttackPresentation", flags);
                var retire = typeof(NetworkCombatController).GetMethod("RetireLogicalCombatPresentation", flags);
                var follow = typeof(Hexiege.Presentation.UnitView).GetMethod("FollowAttackPhase", flags);
                Action enter = () => begin.Invoke(controller, new object[] { 905, 906, true, true });
                Action<double, int> pump = (start, count) => {
                    for (int i = 0; i < count; i++) { clock = start + i * .02; follow.Invoke(view, new object[] { .02d }); animator.Update(.02f); }
                };
                enter(); animator.Update(0);
                Require(published == 1 && view.ConfirmServerAttackEntryHandoff(wire.PresentationRevision), "NCC first entry must publish/ACK after actual application.");
                double epoch = wire.EpochServerTime; ulong epochId = wire.EpochId;
                pump(73.15, 95);
                Require(animator.GetCurrentAnimatorStateInfo(0).normalizedTime * data.AttackCooldown > 1.8, "Controller must really reach the trace's old native phase.");
                retire.Invoke(controller, new object[] { 905 });
                float normalized = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                clock = 75.042976; enter(); animator.Update(0);
                Require(published == 2 && wire.EpochId == epochId && wire.EpochServerTime == epoch
                    && animator.GetCurrentAnimatorStateInfo(0).normalizedTime == normalized && !animator.IsInTransition(0),
                    "Logical normal reentry keeps E/native progress, without the 19-gap new-E/old-phase mismatch.");
                var approved = wire; approved.Mode = AttackPresentationImpactMode.Scoped;
                approved.PresentationRevision = (ulong)typeof(NetworkCombatController).GetMethod("NextAttackPresentationRevision", flags)
                    .Invoke(controller, new object[] { 905 });
                approved.ActionRevision = 1; approved.CommitServerTime = clock;
                approved.Scope = new AttackPresentationScope(new AttackerInstanceId(44), new AttackSequenceId(2), 0);
                approved.Hits = new[] { new AttackSourceOccurrence { HitIndex = 0, Loop = 1, MarkerOffset = data.HitFrameTimes[0],
                    MarkerServerTime = epoch + 2 + data.HitFrameTimes[0], ImpactServerTime = epoch + 2 + data.HitFrameTimes[0] } };
                Require(network.PublishAttackPhase(approved), "Next actual Host scope must bind on the same native epoch.");
                var conflict = approved; conflict.ActionRevision++;
                Require(!network.PublishAttackPhase(conflict), "Same revision conflicting ActionRevision must not publish.");
                conflict = approved; conflict.Hits = (AttackSourceOccurrence[])approved.Hits.Clone();
                conflict.Hits[0].ImpactServerTime += .01;
                Require(!network.PublishAttackPhase(conflict), "Same revision conflicting occurrence must not publish.");
                int accepted = published;
                Require(network.PublishAttackPhase(approved) && published == accepted + 1, "Exact retransmission is immutable/idempotent locally.");
                int emits = view.AttackPhaseNativeEmissions;
                pump(75.15, 65);
                Require(view.AttackPhaseNativeEmissions == emits + 1, "NCC reentry's approved native source emits exactly once, not zero or a later scope.");
                view.HoldMovementAnimation();
                Require(!network.TryGetLiveNativeAttackEpoch(out _), "Held direct close invalidates native epoch.");
                var stale = approved; stale.PresentationRevision++; stale.EpochId++;
                Require(!network.PublishAttackPhase(stale), "Held close also retires old exact sequence.");
                view.StartWalkAnimation(); animator.Update(0);
                retire.Invoke(controller, new object[] { 905 });
                clock = 80; enter(); animator.Update(0);
                Require(wire.EpochServerTime == 80 && wire.EpochId != epochId, "True Walk reentry creates a fresh first-applied epoch.");
                var current = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
                Require(current.normalizedTime * data.AttackCooldown < .001, "Fresh reentry is aligned before remote publication.");
                var receipt = (System.Collections.IDictionary)typeof(NetworkCombatController).GetField("_completedAttackEntryReceipts", flags).GetValue(controller);
                Require(receipt.Contains(905), "NCC records receipt only after successful application/publication.");
            }
            finally
            {
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                NetworkContext.Set(server, active);
            }
        }

        // Execute the same IEnumerator used by SpawnUnitClientRpc. Fake services supply
        // dependencies only; all lifetime and exact-object checks are production code.
        public static void ValidateUnitViewInitializationRetry()
        {
            Require(NetworkProductionController.CanResumeUnitViewRetry(false, true, true)
                && !NetworkProductionController.CanResumeUnitViewRetry(true, true, true)
                && !NetworkProductionController.CanResumeUnitViewRetry(false, false, true)
                && !NetworkProductionController.CanResumeUnitViewRetry(false, true, false),
                "Enable resumes only a spawned, live match; GameEnd stays closed.");
            foreach (string scenario in new[] { "dead", "retired", "identity", "generation", "controller", "session",
                "services", "spawn", "factory", "success", "timeout", "dispose", "initial-dead", "same-service-rematch", "invalid-clock", "reenable", "timeout-dead", "timeout-replaced" })
            {
                var factory = new RetryFixtureFactory();
                var unit = new UnitData(UnitType.Sniper, TeamId.Blue, new HexCoord(0, 0), 100, 10, 1, 1, 1);
                var spawn = new UnitSpawnUseCase(null);
                var units = (Dictionary<int, UnitData>)typeof(UnitSpawnUseCase)
                    .GetField("_units", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spawn);
                units.Add(unit.Id, unit);
                var services = new NativeHostServices { RetrySpawn = spawn, RetryFactory = factory };
                IGameServices current = services;
                long generation = 1; bool controllerAlive = true, session = true;
                string terminal = null; int terminals = 0;
                var owner = new NetworkProductionController.UnitViewRetryOwner(1, services, spawn, factory, unit,
                    () => generation, () => controllerAlive, () => session, () => current,
                    (reason, elapsed) => { terminal = reason; terminals++; });
                var iterator = NetworkProductionController.RunUnitViewInitializationRetry(owner,
                    () => scenario == "invalid-clock" ? float.NaN : 1f);
                // 마지막 실패 시도 중 객체가 닫히는 경우에도 timeout 직전 exact owner
                // 검사가 적용되는지 실제 IEnumerator에서 확인한다.
                factory.AfterAttempt = () =>
                {
                    if (factory.Calls != 4) return;
                    if (scenario == "timeout-dead") unit.TakeDamage(100);
                    if (scenario == "timeout-replaced") units[unit.Id] = new UnitData(
                        UnitType.Sniper, TeamId.Blue, new HexCoord(0, 0), 100, 10, 1, 1, 1);
                };
                if (scenario == "initial-dead") unit.TakeDamage(100);
                bool first = iterator.MoveNext();
                if (scenario == "initial-dead")
                    Require(!first && factory.Calls == 0 && terminal == "unit-dead", "Dead initial owner never initializes.");
                else
                {
                    Require(first && factory.Calls == 1, "Live initial attempt precedes the first yield.");
                    switch (scenario)
                    {
                        case "dead": unit.TakeDamage(100); break;
                        case "retired": units.Remove(unit.Id); break;
                        case "identity": units[unit.Id] = new UnitData(UnitType.Sniper, TeamId.Blue, new HexCoord(0, 0), 100, 10, 1, 1, 1); break;
                        case "generation": generation++; break;
                        case "reenable": controllerAlive = false; generation++; controllerAlive = true; break;
                        case "controller": controllerAlive = false; break;
                        case "session": session = false; break;
                        case "services": current = new NativeHostServices(); break;
                        case "spawn": services.RetrySpawn = new UnitSpawnUseCase(null); break;
                        case "factory": services.RetryFactory = new RetryFixtureFactory(); break;
                        case "same-service-rematch": owner.Finish("game-end"); generation++; session = true; break;
                        case "success": factory.Success = true; break;
                    }
                    if (scenario == "dispose") ((IDisposable)iterator).Dispose();
                    else if (scenario.StartsWith("timeout", StringComparison.Ordinal)) { while (iterator.MoveNext()) { } }
                    else Require(!iterator.MoveNext(), "Ended owner cannot keep waiting.");
                    string expected = scenario == "dead" || scenario == "timeout-dead" ? "unit-dead" : scenario == "retired" ? "unit-retired"
                        : scenario == "identity" || scenario == "timeout-replaced" ? "unit-identity-replaced" : scenario == "generation" || scenario == "reenable" ? "owner-generation-replaced"
                        : scenario == "controller" ? "controller-retired" : scenario == "session" ? "session-ended"
                        : scenario == "services" ? "services-replaced" : scenario == "spawn" ? "spawn-service-replaced"
                        : scenario == "factory" ? "factory-replaced" : scenario == "success" ? "initialized"
                        : scenario == "dispose" ? "coroutine-disposed" : scenario == "same-service-rematch" ? "game-end"
                        : scenario == "invalid-clock" ? "invalid-wait-clock" : "timeout";
                    Require(terminal == expected && factory.Calls == (scenario == "success" ? 2 : scenario.StartsWith("timeout", StringComparison.Ordinal) ? 4 : 1),
                        "Production retry reason/call count mismatch: " + scenario);
                }
                ((IDisposable)iterator).Dispose(); owner.Finish("stale-cleanup");
                Require(terminals == 1 && owner.Finished, "Terminal is exactly once, including stale cleanup.");
                if (scenario == "reenable" || scenario == "same-service-rematch")
                {
                    int nextTerminals = 0;
                    var nextOwner = new NetworkProductionController.UnitViewRetryOwner(generation,
                        services, spawn, factory, unit, () => generation, () => controllerAlive,
                        () => session, () => current, (reason, elapsed) =>
                        { Require(reason == "initialized", "New owner must initialize normally."); nextTerminals++; });
                    factory.Success = true;
                    var nextIterator = NetworkProductionController.RunUnitViewInitializationRetry(nextOwner, () => 1f);
                    owner.Finish("old-cleanup-after-new-owner");
                    Require(!nextIterator.MoveNext() && nextTerminals == 1 && terminals == 1,
                        "Re-enable/rematch new owner survives stale old cleanup with reused services.");
                    ((IDisposable)nextIterator).Dispose();
                }
            }
        }

        private sealed class RetryFixtureFactory : IUnitFactory
        {
            public int Calls;
            public bool Success;
            public Action AfterAttempt;
            public bool InitializeUnitView(UnitData data) { Calls++; AfterAttempt?.Invoke(); return Success; }
            public GameObject GetUnitObject(int id) => null;
            public void RegisterUnitObject(int id, GameObject obj) { }
        }

        // Regression prerequisite: exercise the Host Start -> native receive boundary with
        // the real prefab/controller/relay. No Animator.Play, normalized-time assignment or
        // private phase/lease substitution is used to manufacture the 1.9s old playback.
        public static void ValidatePistoleerHostReentryBoundary()
        {
            bool active = NetworkContext.IsNetworkActive, server = NetworkContext.IsNetworkServer;
            GameObject root = null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                NetworkContext.Set(true, true);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Units/Human/Unit_Pistoleer_Blue.prefab");
                Require(prefab != null, "Host reentry requires the production prefab.");
                root = UnityEngine.Object.Instantiate(prefab);
                root.hideFlags = HideFlags.HideAndDontSave;
                var view = root.GetComponent<Hexiege.Presentation.UnitView>();
                var network = root.GetComponent<NetworkUnit>();
                var animator = root.GetComponentInChildren<Animator>();
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/_Project/Animations/Units/Pistoleer/Pistoleer_Attack.anim");
                Require(view != null && network != null && animator != null && clip != null,
                    "Host reentry must retain production components.");
                var data = new UnitData(903, UnitType.Pistoleer, TeamId.Blue,
                    new HexCoord(0, 0), 100, 10, 4, 2);
                UnitFactory.ApplyAttackClipCooldown(data, UnitFactory.GetAttackClipLength(clip));
                data.HitFrameTimes = UnitFactory.GetHitFrameTimes(clip);
                view.Initialize(data);
                animator.Rebind(); animator.Update(0);
                // Invoke the real action-ownership transition, never substitute its field.
                typeof(Hexiege.Presentation.UnitView).GetMethod("BeginServerActionRotation", flags)
                    .Invoke(view, new object[] { 904, true });
                Require(view.IsServerCombatActionReady(), "Host fixture must own Action Root.");
                double clock = 73.143528;
                typeof(NetworkUnit).GetField("_attackPhaseFixtureClock", flags)
                    .SetValue(network, (Func<double>)(() => clock));
                typeof(NetworkUnit).GetField("_attackPhaseLifetime", flags).SetValue(network, 1UL);
                var receive = typeof(NetworkUnit).GetMethod("ReceiveAttackPhase", flags);
                var follow = typeof(Hexiege.Presentation.UnitView).GetMethod("FollowAttackPhase", flags);
                var phase = new UnitAttackPhasePresentation {
                    Lifetime = 1, EpochId = unchecked((ulong)BitConverter.DoubleToInt64Bits(73.010231)),
                    Active = true, UnitId = 903, TargetId = 904, TargetIsUnit = true,
                    PresentationRevision = 1, EpochServerTime = 73.010231,
                    Duration = data.AttackCooldown, Mode = AttackPresentationImpactMode.Suppressed };
                receive.Invoke(network, new object[] { phase }); animator.Update(0);
                for (int step = 0; step < 95; step++)
                {
                    clock = 73.15 + step * 0.02;
                    follow.Invoke(view, new object[] { 0.02d }); animator.Update(0.02f);
                }
                var state = animator.GetCurrentAnimatorStateInfo(0);
                Require(state.fullPathHash == Animator.StringToHash("Base Layer.Attack")
                    && state.normalizedTime * data.AttackCooldown > 1.8,
                    "Old playback must actually advance through the production controller.");
                // Logical normal stop can leave native Attack on screen. This is not a
                // forced Walk or a terminal close; the exact old owner is closed by NetworkUnit.
                typeof(NetworkUnit).GetMethod("CloseAttackPhase", flags)
                    .Invoke(network, new object[] { "server-state-exit", false });
                clock = 75.042976;
                Require(view.TryApplyHostCombatPresentation(904, true, false, 2,
                    AttackPresentationImpactMode.Suppressed, default),
                    "Old Host preapply must succeed at the real Root-owned boundary.");
                phase.EpochServerTime = clock;
                phase.EpochId = unchecked((ulong)BitConverter.DoubleToInt64Bits(clock));
                phase.PresentationRevision = 2;
                clock = 75.143235;
                receive.Invoke(network, new object[] { phase }); animator.Update(0);
                state = animator.IsInTransition(0)
                    ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
                double target = clock - AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds
                    - phase.EpochServerTime;
                Require(Math.Abs(state.normalizedTime * phase.Duration - target) < 0.001,
                    "Host reentry RED: new epoch first apply must align old native playback; same-revision preapply currently skips it.");
                int before = view.AttackPhaseNativeEmissions;
                phase.PresentationRevision = 3; phase.ActionRevision = 3;
                phase.Mode = AttackPresentationImpactMode.Scoped; phase.CommitServerTime = clock;
                phase.Scope = new AttackPresentationScope(new AttackerInstanceId(43), new AttackSequenceId(2), 0);
                phase.Hits = new[] { new AttackSourceOccurrence { HitIndex = 0, Loop = 0,
                    MarkerOffset = data.HitFrameTimes[0],
                    MarkerServerTime = phase.EpochServerTime + data.HitFrameTimes[0],
                    ImpactServerTime = phase.EpochServerTime + data.HitFrameTimes[0] } };
                receive.Invoke(network, new object[] { phase });
                for (int step = 0; step < 65; step++)
                {
                    clock = 75.15 + step * 0.02;
                    follow.Invoke(view, new object[] { 0.02d }); animator.Update(0.02f);
                }
                Require(view.AttackPhaseNativeEmissions == before + 1,
                    "Host reentry must emit the approved source once through the native relay/VFX callsite.");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                NetworkContext.Set(server, active);
            }
        }

        // 실제 native callback이 이미 지난 marker의 허가를 다음 loop로 옮기는지 확인한다.
        // 이 fixture는 수학 helper가 아니라 production controller/relay/View를 실행한다.
        public static void ValidatePistoleerNativeOccurrenceBoundary()
        {
            bool active = NetworkContext.IsNetworkActive, server = NetworkContext.IsNetworkServer;
            var root = new GameObject("PistoleerNativeOccurrence") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                NetworkContext.Set(false, true);
                var view = root.AddComponent<Hexiege.Presentation.UnitView>();
                var visual = new GameObject("VisualRoot"); visual.transform.SetParent(root.transform, false);
                var animator = visual.AddComponent<Animator>();
                visual.AddComponent<Hexiege.Presentation.AnimationEventRelay>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/_Project/Animations/Units/Pistoleer/Pistoleer.controller");
                animator.applyRootMotion = false; animator.Rebind();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var data = new UnitData(901, UnitType.Pistoleer, TeamId.Blue, new HexCoord(0, 0), 100, 10, 4, 2);
                // UnitData alone has the uninitialized UnitStats fallback (1s). Production
                // UnitFactory replaces it with the native Attack clip length. Reproduce that
                // metadata mapping here; do not mutate the clip or relax the eligibility gate.
                var nativeClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/_Project/Animations/Units/Pistoleer/Pistoleer_Attack.anim");
                Require(nativeClip != null && nativeClip.length == 2f && nativeClip.events.Length == 1
                    && nativeClip.events[0].functionName == "OnAttackHit" && nativeClip.events[0].time == 0.8f,
                    "Production Pistoleer native asset metadata changed.");
                UnitFactory.ApplyAttackClipCooldown(data, UnitFactory.GetAttackClipLength(nativeClip));
                data.HitFrameTimes = UnitFactory.GetHitFrameTimes(nativeClip);
                typeof(Hexiege.Presentation.UnitView).GetField("_unitData", flags).SetValue(view, data);
                typeof(Hexiege.Presentation.UnitView).GetField("_animator", flags).SetValue(view, animator);
                typeof(Hexiege.Presentation.UnitView).GetField("_currentAnimStateHash", flags).SetValue(view, Animator.StringToHash("Attack"));
                animator.Play("Attack", 0, 0.45f); animator.Update(0);
                view.StartCombatAnimation(902, true, false, 1, AttackPresentationImpactMode.Scoped,
                    new AttackPresentationScope(new AttackerInstanceId(41), new AttackSequenceId(7), 0));
                animator.Update(2f);
                var lease = (AttackPresentationImpactLease)typeof(Hexiege.Presentation.UnitView)
                    .GetField("_attackPresentationImpactLease", flags).GetValue(view);
                Require(lease.Status == AttackPresentationImpactLeaseStatus.Consumed,
                    "Trace-shaped RED control: legacy count permission should reproduce next-loop consumption.");
                int oldEmits = view.AttackPhaseNativeEmissions;
                root.AddComponent<Unity.Netcode.NetworkObject>();
                var network = root.AddComponent<NetworkUnit>();
                double clock = 1.05;
                typeof(NetworkUnit).GetField("_attackPhaseFixtureClock", flags).SetValue(network, (Func<double>)(() => clock));
                typeof(NetworkUnit).GetField("_attackPhaseLifetime", flags).SetValue(network, 1UL);
                var receive = typeof(NetworkUnit).GetMethod("ReceiveAttackPhase", flags);
                var scope = new AttackPresentationScope(new AttackerInstanceId(41), new AttackSequenceId(8), 0);
                var phase = new UnitAttackPhasePresentation { Lifetime = 1, EpochId = 1, Active = true,
                    UnitId = 901, TargetId = 902, TargetIsUnit = true, PresentationRevision = 2, ActionRevision = 2,
                    EpochServerTime = 0, Duration = data.AttackCooldown, CommitServerTime = 0.2,
                    Mode = AttackPresentationImpactMode.Scoped, Scope = scope,
                    Hits = new[] { new AttackSourceOccurrence { HitIndex = 0, Loop = 0, MarkerOffset = 0.8f,
                        MarkerServerTime = 0.8, ImpactServerTime = 0.8 } } };
                // Actual receive-before-ready seam: pending is not consumed until View succeeds.
                typeof(Hexiege.Presentation.UnitView).GetField("_unitData", flags).SetValue(view, null);
                receive.Invoke(network, new object[] { phase });
                Require((ulong)typeof(NetworkUnit).GetField("_appliedAttackPhaseRevision", flags).GetValue(network) == 0,
                    "Receive-before-ready must remain pending.");
                typeof(Hexiege.Presentation.UnitView).GetField("_unitData", flags).SetValue(view, data);
                Require(view.CanUseNativeAttackPhase(data), "Production native metadata must qualify before pending retry.");
                network.RetryAttackPhaseViewBinding();
                Require((ulong)typeof(NetworkUnit).GetField("_appliedAttackPhaseRevision", flags).GetValue(network) == 2,
                    "Ready retry must apply exactly once.");
                network.RetryAttackPhaseViewBinding();
                animator.Update(2f);
                Require(view.AttackPhaseNativeEmissions == oldEmits, "GREEN: past source occurrence must not become next-loop VFX.");
                var stale = phase; stale.Active = false; stale.Lifetime = 99;
                receive.Invoke(network, new object[] { stale });
                Require(((UnitAttackPhasePresentation)typeof(NetworkUnit).GetField("_pendingAttackPhase", flags).GetValue(network)).Active,
                    "Wrong-lifetime inactive cannot mutate current pending.");
                stale = phase; stale.Active = false; stale.PresentationRevision = 1;
                receive.Invoke(network, new object[] { stale });
                Require(((UnitAttackPhasePresentation)typeof(NetworkUnit).GetField("_pendingAttackPhase", flags).GetValue(network)).Active,
                    "Stale-revision inactive cannot close current epoch.");
                phase.Active = false; receive.Invoke(network, new object[] { phase });
                phase.Active = true; phase.EpochId = 2; phase.EpochServerTime = 10;
                phase.PresentationRevision = 3; phase.CommitServerTime = 10.2;
                phase.Hits[0].MarkerServerTime = phase.Hits[0].ImpactServerTime = 10.8;
                clock = 10.3; receive.Invoke(network, new object[] { phase });
                // Logical scope closure retains native playback, not permission for seq8.
                // OLD: !pending.Active conflated the native owner with scoped admission.
                var closedPending = (UnitAttackPhasePresentation)typeof(NetworkUnit).GetField("_pendingAttackPhase", flags).GetValue(network);
                Require(closedPending.Active && !closedPending.Scope.IsValid
                    && closedPending.Mode == AttackPresentationImpactMode.Suppressed
                    && closedPending.Hits == null && closedPending.EpochId == 1
                    && closedPending.PresentationRevision == 2
                    && (ulong)typeof(NetworkUnit).GetField("_appliedAttackPhaseRevision", flags).GetValue(network) == 2
                    && (ulong)typeof(NetworkUnit).GetField("_closedAttackInstance", flags).GetValue(network) == 41
                    && (ulong)typeof(NetworkUnit).GetField("_closedAttackSequence", flags).GetValue(network) == 8,
                    "Closed seq8 must not reopen with a new epoch/revision.");
                phase.Scope = new AttackPresentationScope(new AttackerInstanceId(41), new AttackSequenceId(9), 0);
                clock = 10.3; receive.Invoke(network, new object[] { phase });
                Require((ulong)typeof(NetworkUnit).GetField("_appliedAttackPhaseRevision", flags).GetValue(network) == 3,
                    "New epoch after valid close must bind; old closed epoch must not reopen.");
                animator.Update(0);
                var follow = typeof(Hexiege.Presentation.UnitView).GetMethod("FollowAttackPhase", flags);
                var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
                Require(Math.Abs(state.normalizedTime * phase.Duration - 0.2) < 0.001,
                    "First entry offset is seconds, not normalized time.");
                clock = 10.8;
                follow.Invoke(view, new object[] { 0.02d });
                Require(animator.speed >= 0 && animator.speed <= 2.001f, "Active native advance must remain forward and bounded.");
                clock = 10.7; follow.Invoke(view, new object[] { 0.02d });
                Require(animator.speed == 0, "Fresh raw backward clock must hold without rewind.");
                // 실제 Follow 경계가 매 native step을 소유한다. 임의 speed=1로 우회하지 않는다.
                for (int step = 0; step < 40; step++)
                {
                    clock = 10.81 + step * 0.02;
                    follow.Invoke(view, new object[] { 0.02d });
                    animator.Update(0.02f);
                }
                Require(view.AttackPhaseNativeEmissions == oldEmits + 1,
                    "Native relay must reach the production VFX emission callsite exactly once for a future occurrence.");
                animator.Update(2f);
                Require(view.AttackPhaseNativeEmissions == oldEmits + 1, "Duplicate native loop must not emit again.");
                // Leave the duplicate probe's deliberately advanced native loop before the
                // continuous-owner case. A new epoch starts from the production seconds offset.
                view.CloseAttackPhasePresentation("fixture-probe-close");
                phase.EpochId = 3; phase.EpochServerTime = 20; phase.PresentationRevision = 4;
                phase.CommitServerTime = 20.2; phase.Scope = new AttackPresentationScope(new AttackerInstanceId(41), new AttackSequenceId(10), 0);
                phase.Hits = new[] { new AttackSourceOccurrence { HitIndex = 0, Loop = 0, MarkerOffset = 0.8f, MarkerServerTime = 20.8, ImpactServerTime = 20.8 } };
                Action<double, int> pump = (start, count) =>
                {
                    for (int step = 0; step < count; step++)
                    { clock = start + step * 0.02; follow.Invoke(view, new object[] { 0.02d }); animator.Update(0.02f); }
                };
                var provisional = phase; provisional.Mode = AttackPresentationImpactMode.Suppressed;
                provisional.Scope = default; provisional.Hits = null;
                clock = 20.3; receive.Invoke(network, new object[] { provisional }); animator.Update(0);
                pump(20.31, 10);
                Require(view.AttackPhaseNativeEmissions == oldEmits + 1, "Initial provisional follows phase without source permission.");
                float provisionalTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                phase.PresentationRevision = 5; clock = 20.51; receive.Invoke(network, new object[] { phase }); animator.Update(0);
                Require(animator.GetCurrentAnimatorStateInfo(0).normalizedTime == provisionalTime && !animator.IsInTransition(0),
                    "Provisional-to-commit on the same epoch must not restart native Attack.");
                pump(20.52, 65);
                Require(view.AttackPhaseNativeEmissions == oldEmits + 2, "Continuous owner first cycle emits once.");
                phase.PresentationRevision = 6; phase.CommitServerTime = 22.2;
                phase.Scope = new AttackPresentationScope(new AttackerInstanceId(41), new AttackSequenceId(11), 0);
                phase.Hits = new[] { new AttackSourceOccurrence { HitIndex = 0, Loop = 1, MarkerOffset = 0.8f, MarkerServerTime = 22.8, ImpactServerTime = 22.8 } };
                state = animator.GetCurrentAnimatorStateInfo(0);
                float beforeCommit = state.normalizedTime;
                clock = 22.3; receive.Invoke(network, new object[] { phase }); animator.Update(0);
                Require(animator.GetCurrentAnimatorStateInfo(0).normalizedTime == beforeCommit && !animator.IsInTransition(0),
                    "Same-epoch commit must not restart, crossfade or rewind native Attack.");
                pump(22.31, 65);
                Require(view.AttackPhaseNativeEmissions == oldEmits + 3, "Same owner next approved loop emits once.");
                phase.PresentationRevision = 7; phase.CommitServerTime = 24.2;
                phase.Scope = new AttackPresentationScope(new AttackerInstanceId(41), new AttackSequenceId(12), 0);
                phase.Hits = new[] { new AttackSourceOccurrence { HitIndex = 0, Loop = 2, MarkerOffset = 0.8f, MarkerServerTime = 24.8, ImpactServerTime = 24.8 } };
                clock = 24.3; receive.Invoke(network, new object[] { phase });
                var close = phase; close.Active = false; receive.Invoke(network, new object[] { close });
                var occurrence = (AttackSourceOccurrenceLease)typeof(Hexiege.Presentation.UnitView).GetField("_attackOccurrenceLease", flags).GetValue(view);
                Require(occurrence.SourceOnly && occurrence.Remaining == 1, "Normal close preserves only the already-approved occurrence.");
                pump(24.31, 65);
                Require(view.AttackPhaseNativeEmissions == oldEmits + 4 && occurrence.Remaining == 0,
                    "SourceOnly native relay emits once, without moving permission to a later loop.");
                phase.EpochId = 4; phase.EpochServerTime = 30; phase.PresentationRevision = 8; phase.CommitServerTime = 30.2;
                phase.Scope = new AttackPresentationScope(new AttackerInstanceId(41), new AttackSequenceId(13), 0);
                phase.Hits = new[] { new AttackSourceOccurrence { HitIndex = 0, Loop = 0, MarkerOffset = 0.8f, MarkerServerTime = 30.8, ImpactServerTime = 30.8 } };
                var shadow = (Unity.Netcode.NetworkVariable<NetworkUnitActionShadowState>)typeof(NetworkUnit)
                    .GetField("_attackShadowState", flags).GetValue(network);
                shadow.Value = new NetworkUnitActionShadowState { AttackerInstanceId = 41, SequenceId = 13, Revision = 8, LastImpactHitIndex = 0 };
                clock = 30.3; receive.Invoke(network, new object[] { phase }); animator.Update(0);
                Require(occurrence.Remaining == 0, "Last confirmed baseline must skip exactly the confirmed native occurrence.");
                pump(30.31, 65);
                Require(view.AttackPhaseNativeEmissions == oldEmits + 4, "Baseline cannot replay a confirmed source on a later loop.");
                phase.EpochId = 5; phase.EpochServerTime = 32; phase.PresentationRevision = 9; phase.CommitServerTime = 32.2;
                phase.Scope = new AttackPresentationScope(new AttackerInstanceId(41), new AttackSequenceId(14), 0);
                phase.Hits = new[] { new AttackSourceOccurrence { HitIndex = 0, Loop = 0, MarkerOffset = 0.8f, MarkerServerTime = 32.8, ImpactServerTime = 32.8 } };
                clock = 32.3; receive.Invoke(network, new object[] { phase }); animator.Update(0);
                Require(occurrence.Remaining == 1, "A newer sequence does not inherit the previous sequence baseline.");
                // 실제 UnitView Retire→Start/Bind→Animator relay→OnAttackHit→VFX callsite 회귀.
                // RED는 교정 전의 무조건 Close를 같은 production 경계에서 재현한다.
                // manager 없는 fixture이므로 emitter 도달/문맥 증거이며 particle pixel PASS는 아니다.
                int turnoverEmits = view.AttackPhaseNativeEmissions;
                typeof(Hexiege.Presentation.UnitView).GetMethod("RetireAndCloseAttackPresentationScope", flags)
                    .Invoke(view, new object[] { "turnover-red-original-close" });
                phase.PresentationRevision = 10; phase.Scope = new AttackPresentationScope(new AttackerInstanceId(41), new AttackSequenceId(15), 0);
                phase.Hits = new[] { new AttackSourceOccurrence { HitIndex = 0, Loop = 1, MarkerOffset = .8f,
                    MarkerServerTime = 34.8, ImpactServerTime = 34.8 } };
                clock = 32.4; receive.Invoke(network, new object[] { phase }); animator.Update(0);
                pump(32.41, 40);
                Require(view.AttackPhaseNativeEmissions == turnoverEmits,
                    "RED actual native relay: original full close loses old occurrence before the new loop.");
                view.CloseAttackPhasePresentation("turnover-red-end");
                phase.EpochId = 6; phase.EpochServerTime = 36; phase.CommitServerTime = 36.2;
                phase.PresentationRevision = 11; phase.Scope = new AttackPresentationScope(new AttackerInstanceId(41), new AttackSequenceId(16), 0);
                phase.Hits = new[] { new AttackSourceOccurrence { HitIndex = 0, Loop = 0, MarkerOffset = .8f,
                    MarkerServerTime = 36.8, ImpactServerTime = 36.8 } };
                clock = 36.3; receive.Invoke(network, new object[] { phase }); animator.Update(0);
                var frozenContext = typeof(Hexiege.Presentation.UnitView).GetField("_nativeOccurrenceContext", flags).GetValue(view);
                var frozenRotation = (Quaternion)frozenContext.GetType().GetField("Rotation").GetValue(frozenContext);
                var frozenTargetPosition = new Vector3(11, 0, 13);
                frozenContext.GetType().GetField("TargetPosition").SetValue(frozenContext, frozenTargetPosition);
                float turnoverNativeTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                var retainedScope = phase.Scope;
                phase.PresentationRevision = 12; phase.Scope = new AttackPresentationScope(new AttackerInstanceId(41), new AttackSequenceId(17), 0);
                phase.Hits = new[] { new AttackSourceOccurrence { HitIndex = 0, Loop = 1, MarkerOffset = .8f,
                    MarkerServerTime = 38.8, ImpactServerTime = 38.8 } };
                clock = 36.4; receive.Invoke(network, new object[] { phase }); animator.Update(0);
                Require(animator.GetCurrentAnimatorStateInfo(0).normalizedTime == turnoverNativeTime,
                    "Actual same-epoch handoff must not restart/rewind native playback.");
                // 새 mutable target cache를 오염시켜도 실제 emitter는 bind 당시 old 문맥을 쓴다.
                typeof(Hexiege.Presentation.UnitView).GetField("_combatTargetId", flags).SetValue(view, 999);
                root.transform.rotation = Quaternion.Euler(0, 90, 0);
                pump(36.41, 40);
                Require(view.AttackPhaseNativeEmissions == turnoverEmits + 1
                    && view.AttackPhaseLastEmissionScope.Equals(retainedScope)
                    && view.AttackPhaseLastEmissionTarget == 902
                    && view.AttackPhaseLastEmissionTargetPosition == frozenTargetPosition
                    && Quaternion.Angle(view.AttackPhaseLastEmissionRotation, frozenRotation) < .001f,
                    "GREEN actual VFX callsite must emit the pending old exact scope and immutable target, not latest cache.");
                occurrence = (AttackSourceOccurrenceLease)typeof(Hexiege.Presentation.UnitView).GetField("_attackOccurrenceLease", flags).GetValue(view);
                Require(occurrence.Remaining == 1, "Old marker cannot consume the new occurrence/count cursor.");
                pump(37.21, 100);
                Require(view.AttackPhaseNativeEmissions == turnoverEmits + 2
                    && view.AttackPhaseLastEmissionScope.Equals(phase.Scope),
                    "New owner still emits its own exact native loop after pending-old routing.");
                animator.Update(2f);
                Require(view.AttackPhaseNativeEmissions == turnoverEmits + 2, "No old/new next-loop reuse after one-shot emission.");
                view.CloseAttackPhasePresentation("turnover-green-end");
                phase.EpochId = 7; phase.EpochServerTime = 42; phase.CommitServerTime = 42.2;
                for (int pending = 0; pending <= 9; pending++)
                {
                    phase.PresentationRevision = (ulong)(13 + pending);
                    phase.Scope = new AttackPresentationScope(new AttackerInstanceId(41), new AttackSequenceId((ulong)(18 + pending)), 0);
                    phase.Hits = new[] { new AttackSourceOccurrence { HitIndex = 0, Loop = pending, MarkerOffset = .8f,
                        MarkerServerTime = 42.8 + pending * 2, ImpactServerTime = 42.8 + pending * 2 } };
                    clock = 42.3; receive.Invoke(network, new object[] { phase }); animator.Update(0);
                    var pendingOwners = (System.Collections.ICollection)typeof(Hexiege.Presentation.UnitView)
                        .GetField("_pendingNativeOwners", flags).GetValue(view);
                    Require(pendingOwners.Count == (pending <= 8 ? pending : 0),
                        "Actual UnitView handoff accepts cap8 and closes old permissions fail-closed on ninth overflow.");
                }
                view.CloseAttackPhasePresentation("combat-target-change");
                pump(42.31, 70);
                Require(view.AttackPhaseNativeEmissions == turnoverEmits + 2,
                    "Overflow/semantic cancel cannot revive a closed old scope on a native callback.");
                oldEmits += 2;
                // Re-arm a genuinely unconsumed permission; do not test death against an already consumed lease.
                phase.PresentationRevision = 23; phase.EpochId = 8; phase.EpochServerTime = 46;
                phase.CommitServerTime = 46.2;
                phase.Scope = new AttackPresentationScope(new AttackerInstanceId(41), new AttackSequenceId(28), 0);
                phase.Hits = new[] { new AttackSourceOccurrence { HitIndex = 0, Loop = 0, MarkerOffset = .8f,
                    MarkerServerTime = 46.8, ImpactServerTime = 46.8 } };
                clock = 46.3; receive.Invoke(network, new object[] { phase }); animator.Update(0);
                occurrence = (AttackSourceOccurrenceLease)typeof(Hexiege.Presentation.UnitView)
                    .GetField("_attackOccurrenceLease", flags).GetValue(view);
                Require(occurrence.Remaining == 1, "Actual death regression starts with an unconsumed native permission.");
                data.TakeDamage(100);
                view.CloseAttackPhasePresentation("attacker-death", true);
                Require(occurrence.Remaining == 0 && !occurrence.SourceOnly, "Death closes source ownership even when preservation is requested.");
                animator.Update(2f);
                Require(view.AttackPhaseNativeEmissions == oldEmits + 4, "Dead attacker cannot emit its pending native source.");
                typeof(NetworkUnit).GetMethod("CloseAttackPhase", flags).Invoke(network, new object[] { "game-end", true });
                phase.PresentationRevision = 24; phase.EpochId = 9; receive.Invoke(network, new object[] { phase });
                Require(!((UnitAttackPhasePresentation)typeof(NetworkUnit).GetField("_pendingAttackPhase", flags).GetValue(network)).Active,
                    "Game-end cannot reopen a late active payload.");
                Require(!UnitAttackShadowProfileResolver.CanUseNativeAttackPhase(UnitType.FoxMagician)
                    && !UnitAttackShadowProfileResolver.CanUseNativeAttackPhase(UnitType.TorrentSpirit)
                    && !UnitAttackShadowProfileResolver.CanUseNativeAttackPhase(UnitType.BloomFairy), "Excluded policies remain Legacy.");
                view.CloseAttackPhasePresentation("fixture-end");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); NetworkContext.Set(server, active); }
            // 같은 기존 메뉴에서 실제 production multihit/controller/relay를 추가 실행한다.
            bool wasActive = NetworkContext.IsNetworkActive, wasServer = NetworkContext.IsNetworkServer;
            var multiRoot = new GameObject("LionKnightNativeOccurrence") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                NetworkContext.Set(false, true);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var view = multiRoot.AddComponent<Hexiege.Presentation.UnitView>();
                var visual = new GameObject("VisualRoot"); visual.transform.SetParent(multiRoot.transform, false);
                var animator = visual.AddComponent<Animator>();
                visual.AddComponent<Hexiege.Presentation.AnimationEventRelay>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/_Project/Animations/Units/LionKnight/LionKnight.controller");
                animator.applyRootMotion = false; animator.Rebind();
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/Units/LionKnight/LionKnight_Attack.anim");
                var data = new UnitData(903, UnitType.LionKnight, TeamId.Blue, new HexCoord(0, 0), 100, 10, 4, 2);
                UnitFactory.ApplyAttackClipCooldown(data, UnitFactory.GetAttackClipLength(clip));
                data.HitFrameTimes = UnitFactory.GetHitFrameTimes(clip);
                Require(data.AttackCooldown == 2f && data.HitFrameTimes.Length == 2
                    && data.HitFrameTimes[0] == 0.6f && data.HitFrameTimes[1] == 1.4333333f,
                    "Production LionKnight multihit metadata changed.");
                typeof(Hexiege.Presentation.UnitView).GetField("_unitData", flags).SetValue(view, data);
                typeof(Hexiege.Presentation.UnitView).GetField("_animator", flags).SetValue(view, animator);
                multiRoot.AddComponent<Unity.Netcode.NetworkObject>();
                var network = multiRoot.AddComponent<NetworkUnit>();
                double clock = 40.3;
                typeof(NetworkUnit).GetField("_attackPhaseFixtureClock", flags).SetValue(network, (Func<double>)(() => clock));
                typeof(NetworkUnit).GetField("_attackPhaseLifetime", flags).SetValue(network, 1UL);
                Require(view.CanUseNativeAttackPhase(data), "Production multihit metadata must qualify.");
                var productionTimes = data.HitFrameTimes;
                data.HitFrameTimes = new[] { 0f, 0f };
                Require(!view.CanUseNativeAttackPhase(data), "Same-zero metadata stays on the existing Legacy route before publish.");
                data.HitFrameTimes = new[] { productionTimes[0], productionTimes[0] };
                Require(!view.CanUseNativeAttackPhase(data), "Same-time metadata is not promoted to unique native occurrences.");
                data.HitFrameTimes = productionTimes;
                Require(view.CanUseNativeAttackPhase(data), "Restore actual Factory metadata without changing the asset.");
                var receive = typeof(NetworkUnit).GetMethod("ReceiveAttackPhase", flags);
                var follow = typeof(Hexiege.Presentation.UnitView).GetMethod("FollowAttackPhase", flags);
                var shadow = (Unity.Netcode.NetworkVariable<NetworkUnitActionShadowState>)typeof(NetworkUnit).GetField("_attackShadowState", flags).GetValue(network);
                var phase = new UnitAttackPhasePresentation { Lifetime = 1, EpochId = 1, Active = true,
                    UnitId = 903, TargetId = 904, TargetIsUnit = true, PresentationRevision = 1, ActionRevision = 1,
                    EpochServerTime = 40, Duration = data.AttackCooldown, CommitServerTime = 40.2,
                    Mode = AttackPresentationImpactMode.Scoped,
                    Scope = new AttackPresentationScope(new AttackerInstanceId(42), new AttackSequenceId(1), 0) };
                Action bindHits = () =>
                {
                    phase.Hits = new AttackSourceOccurrence[2];
                    for (int hit = 0; hit < 2; hit++) phase.Hits[hit] = new AttackSourceOccurrence {
                        HitIndex = hit, Loop = 0, MarkerOffset = data.HitFrameTimes[hit],
                        MarkerServerTime = phase.EpochServerTime + data.HitFrameTimes[hit],
                        ImpactServerTime = phase.EpochServerTime + data.HitFrameTimes[hit] };
                };
                Action<double, int> pump = (start, count) => {
                    for (int step = 0; step < count; step++) {
                        clock = start + step * 0.02; follow.Invoke(view, new object[] { 0.02d }); animator.Update(0.02f);
                    }
                };
                bindHits();
                shadow.Value = new NetworkUnitActionShadowState { AttackerInstanceId = 42, SequenceId = 1, Revision = 1, LastImpactHitIndex = 0 };
                receive.Invoke(network, new object[] { phase }); animator.Update(0);
                pump(40.31, 70);
                var exactScope = (AttackPresentationScope)typeof(Hexiege.Presentation.UnitView).GetField("_pistoleerTimingScope", flags).GetValue(view);
                Require(view.AttackPhaseNativeEmissions == 1 && exactScope.HitIndex == 1,
                    "Native baseline skips hit0 and emits exact hit1, not the old count cursor.");
                view.CloseAttackPhasePresentation("multihit-baseline-end");
                phase.EpochId = 2; phase.PresentationRevision = phase.ActionRevision = 2; phase.EpochServerTime = 44;
                phase.CommitServerTime = 44.2; phase.Scope = new AttackPresentationScope(new AttackerInstanceId(42), new AttackSequenceId(2), 0);
                bindHits(); clock = 44.3; receive.Invoke(network, new object[] { phase }); animator.Update(0);
                pump(44.31, 25);
                exactScope = (AttackPresentationScope)typeof(Hexiege.Presentation.UnitView).GetField("_pistoleerTimingScope", flags).GetValue(view);
                Require(view.AttackPhaseNativeEmissions == 2 && exactScope.HitIndex == 0, "First native marker emits hit0 once.");
                pump(44.81, 45);
                exactScope = (AttackPresentationScope)typeof(Hexiege.Presentation.UnitView).GetField("_pistoleerTimingScope", flags).GetValue(view);
                Require(view.AttackPhaseNativeEmissions == 3 && exactScope.HitIndex == 1, "Second native marker emits hit1 once.");
                animator.Update(2f);
                Require(view.AttackPhaseNativeEmissions == 3, "Multihit duplicate loop cannot reuse either marker.");
                view.CloseAttackPhasePresentation("multihit-duplicate-end");
                // 실제 같은 clip/hash/loop outgoing marker를 새 epoch hit0와 충돌시킨다.
                // Play는 이전 native state의 재현 초기값뿐이며, 새 전환은 production Apply가 소유한다.
                animator.speed = 1; animator.Play("Attack", 0, 0.275f); animator.Update(0);
                phase.EpochId = 3; phase.PresentationRevision = phase.ActionRevision = 3; phase.EpochServerTime = 48;
                phase.CommitServerTime = 48.2; phase.Scope = new AttackPresentationScope(new AttackerInstanceId(42), new AttackSequenceId(3), 0);
                bindHits(); clock = 48.3; receive.Invoke(network, new object[] { phase }); animator.Update(0);
                Require(animator.IsInTransition(0), "Production new epoch must create a real native self-crossfade.");
                int rejected = view.AttackPhaseNativeRejections;
                pump(48.31, 4);
                var occurrence = (AttackSourceOccurrenceLease)typeof(Hexiege.Presentation.UnitView).GetField("_attackOccurrenceLease", flags).GetValue(view);
                Require(view.AttackPhaseNativeRejections > rejected && view.AttackPhaseNativeEmissions == 3 && occurrence.Remaining == 2,
                    "Actual same-clip/hash/loop outgoing callback rejects before consuming new epoch permission.");
                pump(48.39, 70);
                Require(view.AttackPhaseNativeEmissions == 5 && occurrence.Remaining == 0,
                    "Incoming native state still emits each approved marker exactly once after outgoing rejection.");
                view.CloseAttackPhasePresentation("multihit-fixture-end");
            }
            finally { UnityEngine.Object.DestroyImmediate(multiRoot); NetworkContext.Set(wasServer, wasActive); }
        }

        public static void ValidatePistoleerTimingDiagnostics()
        {
            var evidenceKey = new WaveDeliveryKey(ulong.MaxValue, ulong.MaxValue);
            var futureSpawn = new WavePresentationSpawn(evidenceKey, int.MaxValue, Vector3.zero, Vector3.forward, 1, 1, 1, 20.3);
            string spawnProbe = FormattableString.Invariant(WavePresentationCoordinator.SpawnRejectionDetail(futureSpawn, 20));
            Require(spawnProbe.Contains("firstFailed=future") && spawnProbe.Contains("invalidMask=0"),
                "Actual wave rejection detail must distinguish a valid future spawn from payload corruption.");
            var badSpawn = new WavePresentationSpawn(evidenceKey, -1, Vector3.zero, Vector3.forward, 1, 1, 3, 20.3);
            Require(FormattableString.Invariant(WavePresentationCoordinator.SpawnRejectionDetail(badSpawn, 20)).Contains("firstFailed=payload"),
                "Diagnostic first predicate must preserve production short-circuit priority.");
            var futureContact = new WavePresentationContact(evidenceKey, 256, int.MaxValue, true, false, TeamId.Blue, Vector3.zero, 10, 1, 20.3);
            Require(FormattableString.Invariant(WavePresentationCoordinator.ContactRejectionDetail(futureContact, 20)).Contains("firstFailed=future"),
                "Actual contact detail must separate payload validity and future-time rejection.");
            string expiryProbe = UnitAttackShadowObserver.FormatExpiryEstimateDiagnostic(true, 10.1, 11, 20, 10, 21.25);
            Require(expiryProbe.Contains("receiveAnchorAdvanceEstimate=1") && expiryProbe.Contains("anchoredJumpEstimate=0.25")
                && UnitAttackShadowObserver.FormatExpiryEstimateDiagnostic(false, 0, 11, double.NaN, 0, 21).Contains("anchoredJumpEstimate=unknown"),
                "Actual expiry formatter must keep local extrapolation estimates and missing evidence distinct.");
            const BindingFlags diagnosticFlags = BindingFlags.Static | BindingFlags.NonPublic;
            Func<string, object> diagnosticState = name => typeof(PistoleerTimingDiagnostics).GetField(name, diagnosticFlags).GetValue(null);
            var diagnosticUnit = new UnitData(901, UnitType.Pistoleer, TeamId.Blue, new HexCoord(0, 0), 100, 10, 4, 2);
            PistoleerTimingDiagnostics.Begin("quota-fixture", "closed-scopes", true, 0);
            for (ulong sequence = 1; sequence <= 1100; sequence++)
            {
                var scope = new AttackPresentationScope(new AttackerInstanceId(41), new AttackSequenceId(sequence), 0);
                PistoleerTimingDiagnostics.Register(diagnosticUnit, scope, sequence);
                PistoleerTimingDiagnostics.Event(diagnosticUnit, "Retire", scope, sequence,
                    "reason=missing-source-fixture remaining=1 sourceOnly=0");
            }
            Require(((System.Collections.IDictionary)diagnosticState("Rows")).Count == 0
                && (long)diagnosticState("activeOverflow") == 0,
                "Missing-source retire must reclaim ACTIVE beyond cumulative quota/capacity.");
            Require((int)diagnosticState("blue") == 32
                && ((System.Collections.IDictionary)diagnosticState("Tombstones")).Count == PistoleerTimingDiagnostics.ActiveLimit
                && (long)diagnosticState("tombstoneEvictions") == 76,
                "Historical quota and bounded retired evidence are separate populations.");
            PistoleerTimingDiagnostics.Event(diagnosticUnit, "Decision",
                new AttackPresentationScope(new AttackerInstanceId(41), new AttackSequenceId(1), 0), 1100,
                "status=Ready impact=1 delay=0.1 revision=1 victim=2 kind=1 effect=0 ordinal=0");
            Require((long)diagnosticState("omitted") == 1 && (long)diagnosticState("sampleExcluded") == 0,
                "Evicted known-Pistoleer late event is coverage unknown, never quota-excluded.");
            PistoleerTimingDiagnostics.End(1100, "closed-quota-fixture");
            PistoleerTimingDiagnostics.Begin("quota-fixture", "active-scopes", true, 0);
            for (ulong sequence = 1; sequence <= PistoleerTimingDiagnostics.ActiveLimit + 1UL; sequence++)
                PistoleerTimingDiagnostics.Register(diagnosticUnit,
                    new AttackPresentationScope(new AttackerInstanceId(41), new AttackSequenceId(sequence), 0), sequence);
            Require(((System.Collections.IDictionary)diagnosticState("Rows")).Count == PistoleerTimingDiagnostics.ActiveLimit
                && (long)diagnosticState("activeOverflow") == 1,
                "ActiveLimit must report overflow, never evict an unclosed active scope.");
            var repeatScope = new AttackPresentationScope(new AttackerInstanceId(41), new AttackSequenceId(1), 0);
            for (int repeat = 0; repeat < 100; repeat++)
                PistoleerTimingDiagnostics.Event(diagnosticUnit, "RawMarker", repeatScope, repeat,
                    "normalized=0.1 continuity=1 renderedAnchor=0");
            PistoleerTimingDiagnostics.Event(diagnosticUnit, "PhaseClockRegression", repeatScope, 100, "reason=fresh-ngo-backward");
            Require((long)diagnosticState("sampleOmissions") == 99 && (long)diagnosticState("omitted") == 0,
                "Repeated sampled marker omission is not a critical evidence drop.");
            PistoleerTimingDiagnostics.End(1100, "missing-retire-fixture");
            Require(((System.Collections.IDictionary)diagnosticState("Rows")).Count == 0,
                "END records missing-retire state and reclaims active ownership.");
            string source = File.ReadAllText("Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs");
            int before = source.IndexOf("\"WriterBefore\"", StringComparison.Ordinal);
            int take = source.IndexOf("target.TakeDamage(finalDamage)", before, StringComparison.Ordinal);
            int after = source.IndexOf("\"WriterAfter\"", before, StringComparison.Ordinal);
            if (before < 0 || take < before || after < take) throw new InvalidOperationException("Actual writer hook ordering");
            foreach (var item in new[] {
                "Infrastructure/Network/NetworkCombatController.cs|Schedule|Dispatch|WriterResult|Result|Cancel|Unavailable",
                "Presentation/Unit/UnitView.cs|Command|RenderedStart|RawMarker|MarkerSuppressed|VfxAttempt|VfxResult|SourceStop|TracerDecision",
                "Infrastructure/Network/NetworkUnit.cs|Receive", "Infrastructure/Network/UnitAttackShadowObserver.cs|Decision" })
            {
                string[] fields = item.Split('|'); source = File.ReadAllText("Assets/_Project/Scripts/" + fields[0]);
                for (int i = 1; i < fields.Length; i++)
                    if (!source.Contains("\"" + fields[i] + "\"")) throw new InvalidOperationException("Missing production hook " + fields[i]);
            }
            string directory = Path.GetFullPath("Temp/pistoleer-timing-fixtures");
            GeneratePistoleerTimingFixtures(directory);
            string python = Environment.GetEnvironmentVariable("HEXIEGE_PYTHON") ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe");
            var start = new System.Diagnostics.ProcessStartInfo(python,
                "\"" + Path.GetFullPath("Tools/analyze_pistoleer_timing.py") + "\" --fixtures \"" + directory + "\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = System.Diagnostics.Process.Start(start))
            {
                if (!process.WaitForExit(30000)) { process.Kill(); throw new InvalidOperationException("Timing CLI timeout"); }
                string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                File.WriteAllText(Path.Combine(directory, "parser-report.json"), output);
                if (process.ExitCode != 0) throw new InvalidOperationException("Actual CLI gate failed: " + output);
            }
        }

        // Synthetic inputs through the production serializer. This is NOT a network/runtime test.
        public static void GeneratePistoleerTimingFixtures(string directory)
        {
            Directory.CreateDirectory(directory);
            foreach (string scenario in new[] { "normal", "assault", "sniper", "snipermissingnative", "late", "cancel", "missing", "duplicates", "truncated", "regression", "arrival", "drop", "schema", "terminal", "format", "quota", "mixed", "miss", "sourceonly", "axes", "payload", "victim", "rebased", "phaseclock", "endnumeric", "evicted", "paused", "badinf", "reentry", "episode", "exactcoverage", "lease", "leaseinvalid", "leaseoverflow", "route", "routeinvalid", "routeoverflow" })
            foreach (string role in new[] { "Host", "Client" })
            {
                var lines = new List<string>(); double now = 10;
                Action<string, string> add = (stage, detail) =>
                {
                    double clock = now; now += 0.001;
                    // Same production serializer, now exercising the short 5/30s Assault
                    // timeline as well as the historical Pistoleer cases. Synthetic, not live timing.
                    if (scenario == "assault")
                        detail = detail.Replace("due=20.8", "due=20.166666666666668")
                            .Replace("impact=20.8", "impact=20.166666666666668")
                            .Replace("nominal=0.8", "nominal=0.16666666666666666")
                            .Replace("effective=0.8", "effective=0.16666666666666666")
                            .Replace("cooldown=2", "cooldown=0.3333333333333333")
                            .Replace("anchorLength=2", "anchorLength=0.3333333333333333")
                            .Replace("length=2", "length=0.3333333333333333")
                            .Replace("normalized=0.4", "normalized=0.5");
                    if (scenario.StartsWith("sniper", StringComparison.Ordinal))
                        detail = detail.Replace("due=20.8", "due=21.733333333333334")
                            .Replace("impact=20.8", "impact=21.733333333333334")
                            .Replace("nominal=0.8", "nominal=1.7333333333333334")
                            .Replace("effective=0.8", "effective=1.7333333333333334")
                            .Replace("cooldown=2", "cooldown=3")
                            .Replace("anchorLength=2", "anchorLength=3")
                            .Replace("length=2", "length=3")
                            .Replace("normalized=0.4", "normalized=0.5777777777777777");
                    string raw = PistoleerTimingDiagnostics.Format("fixture-session", role + "-fixture", role,
                        stage, stage == "Begin" || stage == "End" ? 0UL : 41UL, 7UL,
                        scenario == "arrival" && stage == "Receive" ? 0UL : scenario == "axes" ? (role == "Host" ? 3UL : 2UL) : 9UL, 0, 17, 1, clock,
                        stage == "WriterBefore" || stage == "WriterAfter" ? double.NaN : clock + 10, detail,
                        scenario == "assault" ? (int)UnitType.Assault : scenario.StartsWith("sniper", StringComparison.Ordinal) ? (int)UnitType.Sniper : -1);
                    if (Encoding.UTF8.GetByteCount(raw) > 780) throw new InvalidOperationException("Formatter cap");
                    lines.Add(raw);
                };
                add("Begin", "limit=64");
                if (scenario.StartsWith("route", StringComparison.Ordinal))
                {
                    var routeOwner = new UnitAttackPhasePresentation { EpochId = ulong.MaxValue, Lifetime = ulong.MaxValue,
                        TargetId = int.MaxValue, TargetIsUnit = true };
                    var latest = new AttackPresentationScope(new AttackerInstanceId(ulong.MaxValue), new AttackSequenceId(ulong.MaxValue), 0);
                    string route = PistoleerTimingDiagnostics.NativeRouteDetail(routeOwner, "handoff", "none", -1, float.NaN,
                        true, 8, 64, false, latest);
                    add("NativeRoute", scenario == "routeinvalid" ? route.Replace("remaining=64", "missingRemaining=64")
                        : scenario == "routeoverflow" ? route + " oversized=" + new string('x', 900) : route);
                    add("NativeRoute", PistoleerTimingDiagnostics.NativeRouteDetail(routeOwner, "consumed", "none", long.MaxValue,
                        .16666667f, true, 8, 0, false, latest));
                }
                if (scenario.StartsWith("lease", StringComparison.Ordinal))
                {
                    var owner = new UnitAttackPhasePresentation { Active = true, EpochId = ulong.MaxValue, Lifetime = ulong.MaxValue,
                        Scope = new AttackPresentationScope(new AttackerInstanceId(ulong.MaxValue), new AttackSequenceId(ulong.MaxValue), 0),
                        CommitServerTime = 20, Hits = new AttackSourceOccurrence[64] };
                    for (int i = 0; i < owner.Hits.Length; i++) owner.Hits[i] = new AttackSourceOccurrence {
                        HitIndex = i, Loop = long.MaxValue, MarkerOffset = 0.16666667f, MarkerServerTime = 20.3 + i };
                    var lease = new AttackSourceOccurrenceLease();
                    Require(lease.Bind(owner, 20.1, 0, out int skipped) && skipped == 1 && lease.Remaining == 63,
                        "Production lease must retain exactly 64 occurrences and baseline skip without diagnostic writes.");
                    string detail = PistoleerTimingDiagnostics.NativeLeaseDetail("bind-after", "bind-after", "bind-raw",
                        lease.DiagnosticOwner, lease.IsBound, lease.SourceOnly, lease.Remaining, lease.DiagnosticHitCount);
                    add("NativeLease", scenario == "leaseinvalid" ? detail.Replace("remaining=63", "remaining=oops")
                        : scenario == "leaseoverflow" ? detail + " oversized=" + new string('x', 900) : detail);
                    for (int i = 0; i < 64; i++)
                    {
                        Require(lease.ReadDiagnosticHit(i, out var hit, out bool consumed), "Snapshot getter must expose all valid hit indexes.");
                        add("NativeLeaseHit", PistoleerTimingDiagnostics.NativeLeaseHitDetail("bind-after:" + i, true, hit, consumed, -1, float.NaN));
                    }
                    Require(lease.Remaining == 63, "Snapshot formatting must not consume the production lease.");
                    lease.Close();
                    add("NativeLeaseHit", PistoleerTimingDiagnostics.NativeLeaseHitDetail("unbound:0", false, default, false, -1, float.NaN));
                }
                if (scenario == "arrival") add("Receive", "outcome=HitApplied impact=20.8 amount=10 hp=90 victim=3 kind=1 effect=0 ordinal=0 revision=9");
                add("Command", "crossFade=0 continuity=1 renderedAnchor=" + (scenario == "normal" ? "NaN" : "9.8")
                    + " anchorNormalized=0 anchorLength=2 anchorSpeed=1 normalized=0.1 length=2 speed="
                    + (scenario == "paused" ? "0 stateLength=Infinity paused=1 stateRate=1" : scenario == "badinf" ? "1 stateLength=Infinity paused=0 stateRate=1" : "1") + " delay=0.1");
                if (scenario == "paused") add("RenderedStart", "continuity=1 normalized=0.1 length=2 speed=0 stateLength=Infinity paused=1 stateRate=1");
                if (scenario == "reentry")
                {
                    add("EpochLifetime", "reason=native-reentry action=create epoch=1 epochTime=20");
                    add("NativeEpochApply", "epoch=1 generation=1 epochTime=20 offset=0 first=1");
                }
                if (scenario == "episode")
                    for (int episode = 1; episode <= 2; episode++)
                    {
                        add("PhaseHold", $"episode={episode} epoch=1 reason=phase-ahead-or-rate actual=1.9 target=0 dt=0.02 rate=1");
                        add("PhaseResume", $"episode={episode} epoch=1 reason=phase-ahead-or-rate actual=1.9 target=2 dt=0.02");
                    }
                if (scenario == "normal") add("RenderedStart", "continuity=1 normalized=0.1 length=2 speed=1");
                if (role == "Host") add("Schedule", "commit=20 due=20.8 nominal=0.8 effective=0.8 anchor=19.8 overshoot=0 cooldown=2");
                if (scenario == "format") add("Schedule", new string('x', 900));
                if (scenario == "cancel") add(role == "Host" ? "Cancel" : "SourceStop", "reason=cancel sourceOnly=1");
                else
                {
                    now = scenario == "assault" ? 10d + 5d / 30d : scenario.StartsWith("sniper", StringComparison.Ordinal) ? 10d + 52d / 30d : scenario == "late" ? 11.05 : 10.8;
                    if (scenario != "missing" && scenario != "exactcoverage")
                    {
                        if (scenario == "sourceonly") add("SourceStop", "sourceOnly=1 noTracer=1");
                        if (scenario != "snipermissingnative") add("RawMarker", "normalized=0.4 continuity=1 renderedAnchor=9.8");
                        add("Marker", "sourceOnly=" + (scenario == "sourceonly" ? "1" : "0"));
                        if (scenario == "truncated") lines[lines.Count - 1] = lines[lines.Count - 1].Replace(" end=1", " end=");
                        add("VfxAttempt", "coldPool=unknown"); add("VfxResult", "status=Started systems=1 active=1 manager=1");
                        if (scenario == "duplicates") add("VfxResult", "status=Started systems=1 active=1 manager=1");
                    }
                    if (role == "Host")
                    {
                        add("Dispatch", "due=20.8 authorization=Applied");
                        if (scenario != "miss")
                        {
                            add("WriterBefore", "hp=100 damage=10 victim=3 kind=1 effect=0 ordinal=0 clock=local-only");
                            add("WriterAfter", "hp=90 before=100 damage=10 victim=3 kind=1 effect=0 ordinal=0 clock=local-only");
                        }
                        add("WriterResult", "status=" + (scenario == "miss" ? "TargetUnavailable" : "Applied") + " due=20.8");
                        add("Result", "outcome=HitApplied impact=20.8 victim=3 kind=1 effect=0 ordinal=0 revision=9 amount=10 hp=90");
                    }
                    if (scenario != "arrival") add("Receive", "outcome=HitApplied impact=20.8 amount=10 hp=90 victim=3 kind=1 effect=0 ordinal=0 revision=9");
                    add("Decision", "status=Ready impact=20.8 delay=0.1 revision=9 victim=3 kind=1 effect=0 ordinal=0");
                }
                if (scenario == "regression") now -= 0.1;
                if (scenario == "phaseclock") add("PhaseClockRegression", "reason=fresh-ngo-backward");
                if (scenario == "evicted") add("Unregistered", "coverageUnknown=1 reason=retired-evidence-evicted event=Decision");
                if (scenario == "rebased")
                    lines.Add(PistoleerTimingDiagnostics.Format("fixture-session", role + "-fixture", role,
                        "PhaseBind", 41, 7, 9, 0, 17, 1, now, 1, "reason=rebased-estimate"));
                add("End", "sampled=1 omitted=" + (scenario == "drop" ? "1" : "0") + " sampleExcluded=" + (scenario == "quota" ? "100" : "0")
                    + " unclassifiedScoped=" + (scenario == "mixed" ? "100" : "0") + " formatFailures=0 coverage=sample-only");
                if (scenario == "schema")
                    for (int i = 0; i < lines.Count; i++) lines[i] = lines[i].Replace(" due=20.8", "").Replace(" impact=20.8", "");
                if (scenario == "terminal") lines[lines.Count - 1] = lines[lines.Count - 1].Replace("omitted=0", "omitted=not-a-number");
                if (scenario == "endnumeric") lines[lines.Count - 1] = lines[lines.Count - 1].Replace("coverage=sample-only", "activeOverflow=not-a-number peak=-1 sampleOmissions=NaN coverage=sample-only");
                if (scenario == "miss")
                    for (int i = 0; i < lines.Count; i++) lines[i] = lines[i].Replace("outcome=HitApplied", "outcome=Miss").Replace("amount=10", "amount=0").Replace("hp=90", "hp=-1");
                if (role == "Client" && (scenario == "payload" || scenario == "victim"))
                    for (int i = 0; i < lines.Count; i++)
                        if (lines[i].Contains("stage=Receive")) lines[i] = scenario == "payload"
                            ? lines[i].Replace("hp=90", "hp=89") : lines[i].Replace("victim=3", "victim=4");
                File.WriteAllLines(Path.Combine(directory, scenario + "-" + role + ".txt"), lines);
            }
            string fallback = PistoleerTimingDiagnostics.Format(new string('s', 64), new string('r', 32), "Host", "Schedule",
                ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 0, int.MaxValue, 1, double.MaxValue, double.MaxValue, new string('x', 900));
            if (!fallback.Contains("formatIncomplete=1") || Encoding.UTF8.GetByteCount(fallback) > 780)
                throw new InvalidOperationException("Unsafe fallback");
            foreach (string prefix in new[] { new string('s', 4096), new string('\uD55C', 4096) })
            {
                string oversized = PistoleerTimingDiagnostics.Format(prefix, prefix, "Client", "Schedule",
                    ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, int.MaxValue, int.MaxValue, 1,
                    double.MaxValue, double.MaxValue, new string('\uD55C', 4096));
                // RuntimeLogger envelope 예산 120 byte를 포함한 실제 UTF-8 길이다.
                if (!oversized.Contains("formatIncomplete=1") || Encoding.UTF8.GetByteCount(oversized) + 120 > 900)
                    throw new InvalidOperationException("Unsafe fallback plus envelope");
            }
        }

        [MenuItem("Hexiege/Combat/Run Full Regression Suite")]
        public static void Run()
        {
            ValidateUnitViewInitializationRetry();
            ValidatePhaseVocabularyAndOrdinals();
            ValidateMonotonicSequences();
            ValidateMovementHysteresis();
            ValidateB2MovementReducer();
            ValidateB3PreparedMovementTransition();
            ValidateB2MovementEndpointAdapter();
            ValidateB2MovementManifestChunking();
            ValidateB3MovementManifestPreflight();
            ValidateB3ClientReplicationClassification();
            ValidateB3MovementPipelineModeLatch();
            ValidateB3RotationWriterOwnership();
            ValidateB3ClientAttackEntryPresentationOrder();
            ValidateB3PathStartArrivalCommit();
            ValidateB3BlockedAuthoritativeStartEgressPathfinding();
            ValidateB3CombatApproachAndRejoinPathfinding();
            ValidateB3SpatialTransitionPolicy();
            ValidateB3SpatialTransitionApplicationPreflight();
            ValidateB3CorridorSamplePolicy();
            ValidateB3CorridorTerminalEvidence();
            ValidateB3TrajectoryCorridorResolver();
            ValidateSoloPursuitCheckpointProgress();
            ValidateSoloSpatialRecoveryBoundary();
            ValidateSoloTimingAuthority();
            ValidateB3ContinuousLocomotionPlanner();
            ValidateB3FiniteRepathGuard();
            ValidateB3PostCombatRecoveryPlanner();
            ValidateB3PostCombatImmediateReentry();
            ValidateAttackHysteresis();
            ValidateDuplicateAndReorderedResults();
            ValidateA1ContractsAndReducer();
            ValidateC1AttackPresentationContinuityPolicy();
            ValidateAttackSourceMarkerLease();
            ValidateC1HostAtomicHandoffContract();
            ValidateC1ProductionAttackStartGate();
            ValidateQuakeSpiritProductionTimeline();
            ValidateBattleAxeProductionTimeline();
            ValidateFlameSpiritProductionTimeline();
            ValidateBoulderSpiritProductionTimeline();
            ValidateTideSpiritProductionTimeline();
            ValidateBearGuardProductionTimeline();
            ValidatePistoleerProductionTimeline();
            ValidateAssaultProductionTimeline();
            ValidateSniperProductionTimeline();
            ValidatePistoleerHostReentryBoundary();
            ValidatePistoleerHostPublicationChain();
            ValidatePistoleerNativeOccurrenceBoundary();
            ValidatePistoleerTimingDiagnostics();
            ValidateBloomFairyProductionTimeline();
            ValidateBloomHealPresentationBoundary();
            ValidateBloomHoTEffectBoundary();
            ValidateVfxConnections();
            ValidateLionKnightProductionTimeline();
            ValidateSpearManProductionTimeline();
            ValidateEmberSpiritProductionTimeline();
            ValidateDustSpiritProductionTimeline();
            ValidateRabbitTricksterProductionTimeline();
            ValidateTorrentSpiritProductionTimeline();
            Hexiege.EditorTools.TorrentWaveProductionValidation.Run();
            ValidateStreamSpiritProductionTimeline();
            ValidateStreamSpiritProductionTimelineDiagnostics();
            ValidateServerAuthoritativeProjectiles(); // [2026-10-20 서버 권위 투사체 — 단계 A]
            ValidateFoxMagicianProductionTimeline();
            ValidateFoxMagicianProductionTimelineDiagnostics();
            ValidateFoxMagicianPrimaryPresentation();
            ValidateInfernoSpiritProductionTimeline();
            ValidateChargeUnitClipSetAndChargeLength(); // [T-4·T-5 — 2026-10-19 차지 유닛 발사 시점]
            ValidateInfernoAnimatorBodyApiAbsence();
            ValidateInfernoSpiritAnimatedFacingEvidence();
            ValidateInfernoSpiritMotionJumpEvidence();
            ValidateC1AttackShadowManifestAndCoordinator();
            ValidateC1AttackShadowAtomicReplication();
            ValidateC2ImpactResultShadow();
            ValidateC2LegacyOvershootAndAuthorizationOutcome();
            ValidateC3ContinuousAuthoritativeTimeline();
            ValidateC3AuthoritativeResultPresentationShadow();
            RunAuthoritativeAttackPresentationValidation.Run();
            Debug.Log("[UAS-DIAG][BLOOM-HEAL-TIMELINE] self-validation PASS: BloomFairy exact production clip/controller/Blue-Red/scene binding, single 30fps 1:00 marker, 1s preparation plus 3s post-cast cooldown, SeparateHealPipeline/N/A; server heal policy unchanged.");

            Debug.Log("[UAS-DIAG] self-validation PASS: A1 contracts/reducer, A2 pure pose sample, B2 movement reducer command/segment scope and 10/15 hysteresis, endpoint NoIntent normalization/lifecycle, target-acquire priority, duplicate/stale/invalid fail-closed, Android-safe lossless coverage manifest chunks and full-line UTF-8 terminal preflight, B3 match-fixed movement pipeline mode/rollback, publish-staged reducer prepare/commit atomicity, route checkpoint/spatial Root transition separation, trajectory and non-trajectory reducer-facing synchronization, allocation-stable corridor validation seam, gameplay-gated movement-to-action rotation ownership, retry-stable deferred movement scope commit, arrival-committed adjacent path-start transition, blocked authoritative-start deterministic FlowField egress, center-reached checkpoint consumption, building-safe combat approach and Unit64-shaped same-walkable-tile center rejoin, path[0] start-tile egress confinement, bounded terminal corridor Root/Domain/UnitData/path/sample evidence, bounded v13 pre-Action target staging, Root-stop-before-Start prevention and pure-Client rendered attack-entry ordering, bounded center checkpoint error and Chase direct-safe/path evidence, bounded UnitView adapter failure evidence with post-GameEnd suppression, bounded server-checkpoint/client-final-root rotation replication evidence and compact ROOT terminal with full-line UTF-8 preflight, typed v9 candidate probe/final-stage semantics with staged-versus-commit accounting, recoverable-history retention, Unit30-shaped unchanged spatial recovery blocking/stale duplicate suppression/death retire, reduced/stationary/repath fallback and fatal separation, no same-frame retry, center-to-center 60-degree outgoing turn/150-degree reverse/held-progress invariants, objective-scoped finite repath frame/no-progress/environment-reset and duplicate-command suppression, shared production adapter, reducer-direction rotation source, attack-range NoIntent lifecycle, client replication identity/scope monotonic classification and lifecycle retire/reuse including recoverable observer history, C1 atomic Host-local provisional Attack handoff before remote RPC with precommit impact suppression and continuous marker-window commit without clip restart, staged candidate/provisional Start/active Change target publication separation, production stationary/5-degree attack-start gate, bounded source-marker Stop-before-marker preservation without target transfer or loop replay, exhaustive 25-type attack manifest, production-boundary sequence allocation, per-Legacy-cycle target scope including overlapping impacts, pending-reservation StopCombat isolation, sequence-first per-cycle revision reset classification shared by server/client, reservation-bound dispatch consistency independent of display-target lifecycle, fail-closed correlation, target lock/multi-hit lifecycle and atomic latest-scope replication classification, C2 single-source Legacy overshoot cooldown/impact offsets including same-zero multi-hit order, exact attacker/target unavailable authorization-result binding and both-unavailable rejection, writer-owned damage observation, dispatch/completion reservation lifecycle, attacker-death future non-persistent hit cancellation before dispatch/result/observer with authorized completion preservation, reliable full-key ImpactResult shadow and bounded duplicate/conflict/retire classification, phase vocabulary/ordinal, 5/8 attack, cancel/dead, multi-hit/result confirmation, bounded dedupe/reorder, v2 range divergence.");
            Debug.Log("[UAS-DIAG][PRODUCTION-TIMELINE] self-validation PASS: FlameSpirit 6-hit and EmberSpirit explicit scene/Attack clip binding, DustSpirit explicit scene/Attack clip binding and Attack2-first selection, RabbitTrickster explicit scene/Attack clip binding and Attack3-first selection, QuakeSpirit, BattleAxe, TorrentSpirit launch, StreamSpirit, FoxMagician, and InfernoSpirit exact production timeline contracts; charge units (exactly FoxMagician/InfernoSpirit) start the caster-owned charge at OnAttackCharge with a check-only lease, consume it at OnAttackHit where the server projectile launches (tracer from the server launch record, SFX at its display — stage D), charge prefabs end by the launch; StreamSpirit bounded read-only production flow diagnostics.");
            Debug.Log("[UAS-DIAG][C2-AUTHORITY] self-validation PASS: absolute server-time due gate, authorization-gated single Legacy damage writer, authoritative combat-condition Miss, and canonical result classification.");
            Debug.Log("[UAS-DIAG][C3-PRESENTATION-SHADOW] self-validation PASS: match-fixed mode/schema/hash, v12 Host/Client contract gate, marker-time immutable replication evidence, normal timing/recovery timing separation with timeout-zero gate, atomic commit presentation scope independent of replicated-state arrival, continuous every-commit scope publication without Attack clip restart, one-shot production marker lease with exact old-sequence retire and late-result suppression, bounded source-only completion when Stop precedes a committed marker, all-manifest Supported scoped fail-closed versus Unresolved Legacy VFX preservation without fabricated scope, immutable ranged scope capture, bounded InfernoSpirit rendered-Hips start/impact/end evidence with exact-key dedupe, signed-yaw invalid rejection, and forbidden Animator body member source gate, bounded read-only InfernoSpirit rendered-anchor motion-jump allowance/classification with rotation-arc prediction, residual boundary, compact Android-safe full-line preflight, initialization no-event, first severe Walk/Attack transition reservation, exact-key/overflow/lifecycle reset, peer-gated client-only replication classification, production renderer/rootBone/Hips fail-closed contract, per-attacker-sequence 64-entry reorder isolation, named 8-degree direction boundary with exact-revision classification, bounded structural failure evidence, one causal Error plus one axis terminal Error aggregation, independent C2 result/C3 presentation/overall verdicts, exact-key scheduling, duplicate/conflict, both arrival orders, catch-up/expiry, retire, damage-writer-owned victim presentation snapshot, optional hit-VFX asset classification, victim presentation spatial proof, and read-only writer/emitter boundary. Android warning-zero remains a runtime device gate and is not claimed here.");
        }

        /// <summary>
        /// FlameSpirit의 실제 생산 등록부터 양 팀 Attack motion, 여섯 타격 시각까지 확인한다.
        /// 씬은 preview로만 열어 현재 편집 중인 씬을 바꾸거나 저장하지 않는다.
        /// </summary>
        private static void ValidateFlameSpiritProductionTimeline()
        {
            const string root = "Assets/_Project/";
            const string clipPath = root + "Animations/Units/FlameSpirit/FlameSpirit_Attack.anim";
            const string controllerPath = root + "Animations/Units/FlameSpirit/FlameSpirit.controller";
            const string bluePath = root + "Prefabs/Units/Spirit/Unit_FlameSpirit_Blue.prefab";
            const string redPath = root + "Prefabs/Units/Spirit/Unit_FlameSpirit_Red.prefab";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            GameObject blue = AssetDatabase.LoadAssetAtPath<GameObject>(bluePath);
            GameObject red = AssetDatabase.LoadAssetAtPath<GameObject>(redPath);
            Require(clip != null && controller != null && blue != null && red != null
                    && AssetDatabase.AssetPathToGUID(clipPath) == "63b9b44d031f68f468d186399d915ee7"
                    && AssetDatabase.AssetPathToGUID(controllerPath) == "ff8c21c56cd90ab449d7ddd2a4eb4351"
                    && AssetDatabase.AssetPathToGUID(bluePath) == "30850a40f0ca8b940a1afaf12c1d82c1"
                    && AssetDatabase.AssetPathToGUID(redPath) == "4c3b5c6f718fbf142afc55d5b3e004fb",
                "FlameSpirit production clip, controller, and Blue/Red prefabs must retain their verified identities.");

            int attackStates = 0;
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != "Base Layer") continue;
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state.name != "Attack") continue;
                    attackStates++;
                    Require(child.state.motion == clip && Mathf.Approximately(child.state.speed, 1f)
                            && !child.state.speedParameterActive,
                        "FlameSpirit Base Layer/Attack must play the bound clip at speed 1.");
                }
            }
            Require(attackStates == 1, "FlameSpirit must have exactly one Base Layer/Attack state.");

            float[] expected = { 0.6666667f, 1.1666667f, 1.4333334f, 1.6666667f, 1.9f, 2.1f };
            float[] markers = UnitFactory.GetHitFrameTimes(clip);
            Require(Mathf.Abs(clip.frameRate - 30f) < 0.0001f
                    && Mathf.Abs(UnitFactory.GetAttackClipLength(clip) - 3f) < 0.0001f
                    && markers.Length == expected.Length,
                "FlameSpirit Attack must remain 30fps/3s with exactly six OnAttackHit markers.");
            for (int index = 0; index < expected.Length; index++)
                Require(Mathf.Abs(markers[index] - expected[index]) < 0.0001f
                        && (index == 0 || markers[index] > markers[index - 1]),
                    "FlameSpirit OnAttackHit markers must retain the six ordered production offsets.");

            UnitStatsConfig config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(
                root + "Resources/Config/UnitStatsConfig.asset");
            Require(config != null, "FlameSpirit production stats must exist.");
            int statRows = 0;
            foreach (UnitStatEntry entry in config.Stats)
            {
                if (entry.unitType != UnitType.FlameSpirit) continue;
                statRows++;
                Require(Mathf.Abs(entry.attackCooldown - 3f) < 0.0001f
                        && entry.hitFrameTimes != null && entry.hitFrameTimes.Length == expected.Length,
                    "FlameSpirit config must retain its 3s fallback and six impacts.");
                for (int index = 0; index < expected.Length; index++)
                    Require(Mathf.Abs(entry.hitFrameTimes[index] - expected[index]) < 0.0001f,
                        "FlameSpirit config impact offsets must match the Attack markers.");
            }
            Require(statRows == 1, "FlameSpirit must have exactly one production stats row.");

            foreach (GameObject prefab in new[] { blue, red })
            {
                Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
                Require(animators.Length == 1 && animators[0].runtimeAnimatorController == controller
                        && !animators[0].applyRootMotion
                        && animators[0].GetComponent<Hexiege.Presentation.AnimationEventRelay>() != null,
                    "Both FlameSpirit prefabs need the verified controller and same-object event relay without root motion.");
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(root + "Scenes/Game.unity");
            try
            {
                int factories = 0;
                int entries = 0;
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                foreach (UnitFactory factory in sceneRoot.GetComponentsInChildren<UnitFactory>(true))
                {
                    factories++;
                    var serialized = new SerializedObject(factory);
                    SerializedProperty rows = serialized.FindProperty("_spiritPrefabs");
                    Require(rows != null && rows.isArray, "Game UnitFactory must serialize Spirit registrations.");
                    for (int index = 0; index < rows.arraySize; index++)
                    {
                        SerializedProperty row = rows.GetArrayElementAtIndex(index);
                        if (row.FindPropertyRelative("type").intValue != (int)UnitType.FlameSpirit) continue;
                        entries++;
                        Require(row.FindPropertyRelative("attackTimelineClip").objectReferenceValue == clip
                                && row.FindPropertyRelative("blue").objectReferenceValue == blue
                                && row.FindPropertyRelative("red").objectReferenceValue == red,
                            "Game UnitFactory FlameSpirit entry must bind the Attack clip and both production prefabs.");
                    }
                }
                Require(factories == 1 && entries == 1,
                    "Game scene must contain exactly one UnitFactory and one FlameSpirit registration.");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }

            // 명시 클립은 Controller의 클립 나열 순서나 누락과 무관하게 선택되어야 한다.
            Require(UnitFactory.SelectAttackTimelineClip(clip, controller.animationClips) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, new AnimationClip[0]) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, null) == clip,
                "FlameSpirit explicit Attack clip must win controller enumeration and empty-list cases.");
            string source = File.ReadAllText(root + "Scripts/Infrastructure/Factories/UnitFactory.cs");
            foreach (string call in new[] { "GetAttackTimelineClip(unitData, animator)",
                         "GetAttackClipLength(attackClip)", "GetHitFrameTimes(attackClip)" })
                Require(source.Split(new[] { call }, StringSplitOptions.None).Length - 1 == 2,
                    "Host and Client UnitFactory paths must share FlameSpirit selection and extraction: " + call);
            Require(UnitAttackShadowProfileResolver.TryResolve(UnitType.FlameSpirit, out var profile)
                    && profile.Support == UnitAttackShadowSupport.Supported
                    && profile.ExpectedImpactCount == expected.Length,
                "FlameSpirit production resolver must remain Supported with six impacts.");
        }

        /// <summary>
        /// BoulderSpirit의 실제 생산 연결과 1:10(40/30초) 단일 타격·4초 주기를 함께 확인한다.
        /// 현재 편집 중인 씬을 바꾸지 않도록 Game 씬은 preview로 열고 닫는다.
        /// </summary>
        private static void ValidateBoulderSpiritProductionTimeline()
        {
            const string root = "Assets/_Project/";
            const string clipPath = root + "Animations/Units/BoulderSpirit/BoulderSpirit_Attack.anim";
            const string controllerPath = root + "Animations/Units/BoulderSpirit/BoulderSpirit.controller";
            const string bluePath = root + "Prefabs/Units/Spirit/Unit_BoulderSpirit_Blue.prefab";
            const string redPath = root + "Prefabs/Units/Spirit/Unit_BoulderSpirit_Red.prefab";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            GameObject blue = AssetDatabase.LoadAssetAtPath<GameObject>(bluePath);
            GameObject red = AssetDatabase.LoadAssetAtPath<GameObject>(redPath);
            Require(clip != null && controller != null && blue != null && red != null
                    && AssetDatabase.AssetPathToGUID(clipPath) == "a08b07c6564fb5741ace267fd675b087"
                    && AssetDatabase.AssetPathToGUID(controllerPath) == "834665be97ae7284b87a3eb1eae7d3a2"
                    && AssetDatabase.AssetPathToGUID(bluePath) == "ed8f7c54de615284ead6cca046630693"
                    && AssetDatabase.AssetPathToGUID(redPath) == "37aeff26a5cd70148bb93cb5b56c30fe",
                "BoulderSpirit production clip, controller, and Blue/Red prefabs must retain their verified identities.");

            int attackStates = 0;
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != "Base Layer") continue;
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state.name != "Attack") continue;
                    attackStates++;
                    Require(child.state.motion == clip && Mathf.Approximately(child.state.speed, 1f)
                            && !child.state.speedParameterActive,
                        "BoulderSpirit Base Layer/Attack must play the bound clip at speed 1.");
                }
            }
            Require(attackStates == 1, "BoulderSpirit must have exactly one Base Layer/Attack state.");
            float[] markers = UnitFactory.GetHitFrameTimes(clip);
            Require(Mathf.Abs(clip.frameRate - 30f) < 0.0001f
                    && Mathf.Abs(UnitFactory.GetAttackClipLength(clip) - 4f) < 0.0001f
                    && clip.isLooping && markers.Length == 1
                    && Mathf.Abs(markers[0] - 1.3333333f) < 0.0001f,
                "BoulderSpirit Attack must remain a looping 30fps/4s clip with one OnAttackHit at 40/30s.");

            UnitStatsConfig config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(
                root + "Resources/Config/UnitStatsConfig.asset");
            Require(config != null, "BoulderSpirit production stats must exist.");
            int statRows = 0;
            foreach (UnitStatEntry entry in config.Stats)
            {
                if (entry.unitType != UnitType.BoulderSpirit) continue;
                statRows++;
                Require(Mathf.Abs(entry.attackCooldown - 4f) < 0.0001f
                        && entry.hitFrameTimes != null && entry.hitFrameTimes.Length == 1
                        && Mathf.Abs(entry.hitFrameTimes[0] - 1.3333333f) < 0.0001f,
                    "BoulderSpirit config must retain cooldown 4s and one fallback impact at 40/30s.");
            }
            Require(statRows == 1, "BoulderSpirit must have exactly one production stats row.");

            foreach (GameObject prefab in new[] { blue, red })
            {
                Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
                Require(animators.Length == 1 && animators[0].runtimeAnimatorController == controller
                        && !animators[0].applyRootMotion
                        && animators[0].GetComponent<Hexiege.Presentation.AnimationEventRelay>() != null,
                    "Both BoulderSpirit prefabs need the verified controller and same-object event relay without root motion.");
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(root + "Scenes/Game.unity");
            try
            {
                int factories = 0;
                int entries = 0;
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                foreach (UnitFactory factory in sceneRoot.GetComponentsInChildren<UnitFactory>(true))
                {
                    factories++;
                    var serialized = new SerializedObject(factory);
                    SerializedProperty rows = serialized.FindProperty("_spiritPrefabs");
                    Require(rows != null && rows.isArray, "Game UnitFactory must serialize Spirit registrations.");
                    for (int index = 0; index < rows.arraySize; index++)
                    {
                        SerializedProperty row = rows.GetArrayElementAtIndex(index);
                        if (row.FindPropertyRelative("type").intValue != (int)UnitType.BoulderSpirit) continue;
                        entries++;
                        Require(row.FindPropertyRelative("attackTimelineClip").objectReferenceValue == clip
                                && row.FindPropertyRelative("blue").objectReferenceValue == blue
                                && row.FindPropertyRelative("red").objectReferenceValue == red,
                            "Game UnitFactory BoulderSpirit entry must bind the Attack clip and both production prefabs.");
                    }
                }
                Require(factories == 1 && entries == 1,
                    "Game scene must contain exactly one UnitFactory and one BoulderSpirit Spirit registration.");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }

            // 명시된 Attack 클립은 Controller의 클립 나열 순서나 누락에 영향받지 않아야 한다.
            Require(UnitFactory.SelectAttackTimelineClip(clip, controller.animationClips) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, new AnimationClip[0]) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, null) == clip,
                "BoulderSpirit explicit Attack clip must win controller enumeration and empty-list cases.");
        }

        /// <summary>
        /// TideSpirit의 실제 생산 에셋과 30fps 1:15(1.5초) 타격, 3초 공격 주기를 함께 확인한다.
        /// Game 씬은 미리보기로 열어 사용자가 편집 중인 씬을 바꾸거나 저장하지 않는다.
        /// </summary>
        private static void ValidateTideSpiritProductionTimeline()
        {
            const string root = "Assets/_Project/";
            const string clipPath = root + "Animations/Units/TideSpirit/TideSpirit_Attack.anim";
            const string controllerPath = root + "Animations/Units/TideSpirit/TideSpirit.controller";
            const string bluePath = root + "Prefabs/Units/Spirit/Unit_TideSpirit_Blue.prefab";
            const string redPath = root + "Prefabs/Units/Spirit/Unit_TideSpirit_Red.prefab";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            GameObject blue = AssetDatabase.LoadAssetAtPath<GameObject>(bluePath);
            GameObject red = AssetDatabase.LoadAssetAtPath<GameObject>(redPath);
            Require(clip != null && controller != null && blue != null && red != null
                    && AssetDatabase.AssetPathToGUID(clipPath) == "436a2847db5341848804090de50cc4ea"
                    && AssetDatabase.AssetPathToGUID(controllerPath) == "d92b523229b755a418d1904c88d75a67"
                    && AssetDatabase.AssetPathToGUID(bluePath) == "1ee5280e90b88604e90425408b79b5a7"
                    && AssetDatabase.AssetPathToGUID(redPath) == "86b911b71c9cfd74dad1b99ead59680a",
                "TideSpirit production clip, controller, and Blue/Red prefabs must retain their verified identities.");

            int attackStates = 0;
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != "Base Layer") continue;
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state.name != "Attack") continue;
                    attackStates++;
                    Require(child.state.motion == clip && Mathf.Approximately(child.state.speed, 1f)
                            && !child.state.speedParameterActive,
                        "TideSpirit Base Layer/Attack must play the bound clip at speed 1.");
                }
            }
            Require(attackStates == 1, "TideSpirit must have exactly one Base Layer/Attack state.");
            float[] markers = UnitFactory.GetHitFrameTimes(clip);
            Require(Mathf.Abs(clip.frameRate - 30f) < 0.0001f
                    && Mathf.Abs(UnitFactory.GetAttackClipLength(clip) - 3f) < 0.0001f
                    && clip.isLooping && clip.events.Length == 1 && markers.Length == 1
                    && Mathf.Abs(markers[0] - 1.5f) < 0.0001f,
                "TideSpirit Attack must remain a looping 30fps/3s clip with one OnAttackHit at 1.5s.");

            UnitStatsConfig config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(
                root + "Resources/Config/UnitStatsConfig.asset");
            Require(config != null, "TideSpirit production stats must exist.");
            int statRows = 0;
            foreach (UnitStatEntry entry in config.Stats)
            {
                if (entry.unitType != UnitType.TideSpirit) continue;
                statRows++;
                Require(Mathf.Abs(entry.attackCooldown - 3f) < 0.0001f
                        && entry.hitFrameTimes != null && entry.hitFrameTimes.Length == 1
                        && Mathf.Abs(entry.hitFrameTimes[0] - 1.5f) < 0.0001f,
                    "TideSpirit config must retain cooldown 3s and one fallback impact at 1.5s.");
            }
            Require(statRows == 1, "TideSpirit must have exactly one production stats row.");

            foreach (GameObject prefab in new[] { blue, red })
            {
                Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
                Require(animators.Length == 1 && animators[0].runtimeAnimatorController == controller
                        && !animators[0].applyRootMotion
                        && animators[0].GetComponent<Hexiege.Presentation.AnimationEventRelay>() != null,
                    "Both TideSpirit prefabs need the verified controller and same-object event relay without root motion.");
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(root + "Scenes/Game.unity");
            try
            {
                int factories = 0;
                int entries = 0;
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                foreach (UnitFactory factory in sceneRoot.GetComponentsInChildren<UnitFactory>(true))
                {
                    factories++;
                    var serialized = new SerializedObject(factory);
                    SerializedProperty rows = serialized.FindProperty("_spiritPrefabs");
                    Require(rows != null && rows.isArray, "Game UnitFactory must serialize Spirit registrations.");
                    for (int index = 0; index < rows.arraySize; index++)
                    {
                        SerializedProperty row = rows.GetArrayElementAtIndex(index);
                        // enumValueIndex는 선언 순서다. 실제 저장된 UnitType 숫자 16으로 행을 찾는다.
                        if (row.FindPropertyRelative("type").intValue != (int)UnitType.TideSpirit) continue;
                        entries++;
                        Require(row.FindPropertyRelative("attackTimelineClip").objectReferenceValue == clip
                                && row.FindPropertyRelative("blue").objectReferenceValue == blue
                                && row.FindPropertyRelative("red").objectReferenceValue == red,
                            "Game UnitFactory TideSpirit entry must bind the Attack clip and both production prefabs.");
                    }
                }
                Require(factories == 1 && entries == 1,
                    "Game scene must contain exactly one UnitFactory and one TideSpirit Spirit registration.");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }

            // 명시 클립이 있으면 Controller의 클립 나열 순서나 빈 목록보다 그것을 우선한다.
            Require(UnitFactory.SelectAttackTimelineClip(clip, controller.animationClips) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, new AnimationClip[0]) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, null) == clip,
                "TideSpirit explicit Attack clip must win controller enumeration and empty-list cases.");
        }

        /// <summary>
        /// BloomFairy의 생산 클립과 30fps 1:00 힐 연출 표식을 확인한다.
        /// 실제 회복은 서버 타이머가 담당하며 준비 1초 뒤 쿨다운 3초인 힐러 예외를 보존한다.
        /// </summary>
        /// <summary>기존 메뉴의 production channel/presenter/pool 회귀. 저장/asset 수정은 하지 않는다.</summary>
        public static void ValidateBloomHoTEffectBoundary()
        {
            Require(!UnityEngine.Application.isPlaying, "Bloom HoT fixture must run outside a live match.");
            const BindingFlags f = BindingFlags.Instance | BindingFlags.NonPublic;
            var config = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.UnitEffectConfig>("Assets/_Project/Resources/Config/UnitEffectConfig.asset");
            config.Initialize();
            var preset = config.GetHeal(UnitType.BloomFairy);
            Require(preset != null && config.GetAttack(UnitType.BloomFairy) == null, "Bloom heal slot is independent; attack remains null.");
            Require(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(preset.VfxPrefab)) == "ff068a5e9b80f3043a0517ebb1cfb00f", "Authored Bloom GUID must match.");
            var authored = preset.VfxPrefab.GetComponentsInChildren<ParticleSystem>(true);
            // [R-3 비활성화 — 2026-10-13] 개수 5 고정은 옛 프리팹(Bloom_Ring 포함) 가정이었다. 새 프리팹은 4개.
            //   최종 삭제는 사용자 테스트 통과 후(WORKFLOW [4]).
            // Require(authored.Length == 5 && preset.VfxPrefab.GetComponent<ParticleSystem>() != null, "Recursive5/root1 are separate metrics.");
            // 개수는 "실제 프리팹에서 센 값(1개 이상)"으로 바꾸고, 아래 시스템별 계약 단언은 그대로 둔다(검증 강도 유지).
            Require(authored.Length >= 1 && preset.VfxPrefab.GetComponent<ParticleSystem>() != null,
                "Recursive authored PS count>=1 and root PS presence are separate metrics.");
            foreach (var ps in authored)
                Require(ps.main.loop && !ps.main.useUnscaledTime && ps.main.stopAction == ParticleSystemStopAction.None
                    && ps.main.simulationSpace == ParticleSystemSimulationSpace.Local && ps.emission.burstCount == 0,
                    "Every authored PS loop/local/scaled/None/burst0 contract must remain unchanged.");
            Func<UnitData> targetFactory = () => new UnitData(9901, UnitType.LittleKnight, TeamId.Blue, new HexCoord(0, 0), 100, 10, 1, 1);
            var target = targetFactory();
            var channel = new BloomHealEffectChannel();
            var state = channel.Grant(target);
            Require(state.IsValid && state.Active, "Grant must create a valid recipient owner.");
            var old = state;
            channel.Refresh(ref state, target);
            Require(state.SameOwner(old) && state.Revision > old.Revision, "Refresh retains owner, increases revision.");
            var cursor = new BloomHealEffectCursor();
            Require(cursor.TryAccept(state) && !cursor.TryAccept(state) && !cursor.TryAccept(old), "Cursor rejects duplicate/reverse order.");
            var conflict = state; conflict.Active = !state.Active;
            Require(cursor.Classify(state) == BloomHealAdmission.Duplicate
                && cursor.Classify(old) == BloomHealAdmission.Stale
                && cursor.Classify(conflict) == BloomHealAdmission.Conflict
                && !cursor.TryAccept(conflict) && cursor.Current.Equals(state),
                "Equal revision altered payload is conflict, not duplicate; rejected state is unchanged.");
            channel.Stop(ref state, target, BloomHealStopReason.Expired);
            Require(cursor.TryAccept(state), "Terminal current is accepted.");
            var resurrect = state; resurrect.Active = true; resurrect.Revision++;
            Require(!cursor.TryAccept(resurrect), "Closed instance cannot reactivate.");
            var replacement = targetFactory();
            Require(!channel.Accept(old, replacement) && !channel.BindRecipient(replacement, old.MatchGeneration, old.TargetLifetime), "ID alone cannot rebind an old lifetime.");
            channel.CloseAttachment(target, "FixtureDespawn");
            var entries = new List<BloomHealEffectChannel.Recipient>(); channel.CopyCurrent(entries);
            Require(entries.Count == 0 && !channel.TryGet(target, out _), "Retire removes UnitData references, not just emits a visual event.");
            Require(!channel.BindRecipient(replacement, old.MatchGeneration, old.TargetLifetime), "Retired lifetime tombstone protects ID reuse.");
            var next = channel.Grant(replacement);
            Require(next.TargetLifetime > old.TargetLifetime && next.EffectInstanceId > old.EffectInstanceId, "New local object gets a new lifetime/instance.");

            GameObject root = null, pose = null, itemGo = null;
            var previousManager = Hexiege.Presentation.EffectManager.Instance;
            try
            {
                var rejection = new BloomHealEffectChannel(); var rejectedState = rejection.Grant(target);
                Require(!rejection.Accept(rejectedState, target), "Duplicate channel input rejects.");
                var changedPayload = rejectedState; changedPayload.Active = false;
                Require(!rejection.Accept(changedPayload, target), "Same revision conflicting channel input rejects.");
                rejection.Refresh(ref rejectedState, target);
                Require(!rejection.Accept(changedPayload, target), "Older conflicting payload is stale.");
                Require(!rejection.Accept(default, target), "Invalid payload rejects visibly.");
                Require(rejection.Diagnostics.Count("Duplicate") == 1 && rejection.Diagnostics.Count("Conflict") == 1
                    && rejection.Diagnostics.Count("Stale") == 1 && rejection.Diagnostics.Count("Drop") == 1,
                    "Production reject counters distinguish duplicate/conflict/stale/invalid drop.");
                rejection.CloseVisuals("FixtureRejections");
                pose = new GameObject("BloomFixturePose") { hideFlags = HideFlags.HideAndDontSave };
                pose.transform.SetPositionAndRotation(new Vector3(7, 2, -3), Quaternion.Euler(0, 110, 0));
                itemGo = UnityEngine.Object.Instantiate(preset.VfxPrefab); itemGo.hideFlags = HideFlags.HideAndDontSave;
                var item = itemGo.AddComponent<Hexiege.Presentation.VfxPoolItem>();
                Vector3 authoredScale = itemGo.transform.localScale;
                int returns = 0; item.Setup(preset.VfxPrefab, _ => returns++);
                // Edit Mode는 Awake 호출을 보장하지 않는다. production Setup이 Awake와
                // 같은 초기화 경로를 사용한 뒤 실제 root1/원본 scale 캐시를 먼저 검증한다.
                Require(itemGo.GetComponent<ParticleSystem>() != null && item.ParticleSystemCount == 1
                    && (Vector3)typeof(Hexiege.Presentation.VfxPoolItem).GetField("_originalLocalScale", f).GetValue(item) == authoredScale,
                    "Production setup initializes the actual root PS and authored scale in Edit Mode.");
                var proof = new BloomHealEffectDiagnostics();
                Require(item.BeginBloomAcquire(), "Pool checkout can begin.");
                ulong lease = item.PlayBloomLoop(next, pose.transform, preset, proof);
                // [R-3] 옛 단언: item.BloomRecursiveSystemCount == 5 — "lease가 재생한 개수 == authored 개수"로 대체.
                Require(lease != 0 && item.BloomRecursiveSystemCount == authored.Length && item.ParticleSystemCount == 1,
                    "All authored PS lease starts (played count equals authored count).");
                // [C] 크기 맞춤 없는 기존 호출은 배율 1 — 예전과 같은 크기(원본 × 프리셋 배율, pose 스케일 1).
                Require(VfxApprox(item.transform.localScale, Vector3.Scale(authoredScale, preset.ScaleMultiplier)),
                    "Bloom without recipient fit keeps the authored × preset scale.");
                var systems = itemGo.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in systems) ps.Simulate(.25f, false, false, false);
                float time = systems[0].time;
                var refresh = next; refresh.Revision++;
                Require(item.RefreshBloom(lease, refresh) && item.BloomLease == lease && systems[0].time == time, "Refresh noRestart/noClear.");
                foreach (var ps in systems) ps.Clear(false);
                var update = typeof(Hexiege.Presentation.VfxPoolItem).GetMethod("Update", f);
                update.Invoke(item, null);
                Require(item.BloomLoopActive && returns == 0, "Active particle gap/pause cannot return a loop.");
                foreach (var ps in systems) ps.Emit(1);
                var terminal = refresh; terminal.Active = false; terminal.Revision++; terminal.StopReason = BloomHealStopReason.FullHP;
                Require(item.StopBloom(lease, terminal) && item.BloomSnapshot.Equals(terminal), "Pool stop captures exact terminal revision.");
                Vector3 frozen = item.transform.position; Quaternion frozenRotation = item.transform.rotation; Vector3 frozenScale = item.transform.lossyScale;
                pose.transform.position += Vector3.one * 20; item.FollowBloomPose(lease, pose.transform);
                Require(item.transform.position == frozen && item.transform.rotation == frozenRotation && item.transform.lossyScale == frozenScale, "Stop freezes last valid world pose.");
                proof.Finish("FixtureEnd");
                Require(!proof.Ended && proof.PendingTails == 1, "END waits for scaled tails, pending is not success.");
                item.Return();
                Require(returns == 0, "External Return cannot truncate living stopped particles.");
                update.Invoke(item, null);
                Require(returns == 0, "No elapsed simulation means paused drain remains pending.");
                foreach (var ps in systems) ps.Simulate(30f, false, false, false);
                // Simulate ends paused, unlike the runtime's continuing StopEmitting tail.
                // Prove natural particle expiry BEFORE restoring the stopped native state; never Clear.
                foreach (var ps in systems)
                    Require(ps.particleCount == 0 && !ps.isEmitting, "All authored PS simulation expires particles before StopEmitting reassertion.");
                foreach (var ps in systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                foreach (var ps in systems)
                    Require(ps.particleCount == 0 && ps.isStopped && !ps.IsAlive(false), "All authored PS NoClear reassertion retains zero particles and closes native state.");
                update.Invoke(item, null); update.Invoke(item, null);
                Require(returns == 1 && proof.Ended && proof.PendingTails == 0 && item.BloomSnapshot.Equals(terminal), "All authored PS drain/return once retains terminal key.");
                Require(item.BeginBloomAcquire(), "Returned item can be checked out again.");
                // [C] 재사용 lease는 크기 맞춤(배율 2, 발밑 보정)으로 재생해 배율·위치·refresh 유지·반환 복원을 확인한다.
                var bloomFit = new Hexiege.Presentation.RecipientFit(2f, new Vector3(0.1f, 0.05f, -0.2f));
                ulong lease2 = item.PlayBloomLoop(next, pose.transform, preset, bloomFit, new BloomHealEffectDiagnostics());
                Vector3 fittedBloomScale = Vector3.Scale(authoredScale, preset.ScaleMultiplier) * 2f;
                Require(VfxApprox(item.transform.localScale, fittedBloomScale)
                    && VfxApprox(item.transform.position, pose.transform.position
                        + pose.transform.rotation * (preset.LocalPositionOffset + bloomFit.FootOffset)),
                    "Bloom recipient fit applies uniform scale and foot offset at Play.");
                var fitRefresh = next; fitRefresh.Revision++;
                Require(item.RefreshBloom(lease2, fitRefresh), "Fitted Bloom same-owner refresh is accepted.");
                pose.transform.position += new Vector3(1f, 0f, 2f); item.FollowBloomPose(lease2, pose.transform);
                Require(VfxApprox(item.transform.localScale, fittedBloomScale),
                    "Bloom refresh/follow keeps the Play-time fit scale (no per-frame recompute).");
                Require(lease2 > lease && !item.StopBloom(lease, "StaleCallback"), "Stale old lease cannot stop a reused item.");
                item.StopBloom(lease2, "LocalAttachmentClose");
                Require(item.BloomSnapshot.Active, "Local attachment close does not fake server Active=false.");
                foreach (var ps in systems) ps.Simulate(30f, false, false, false);
                foreach (var ps in systems)
                    Require(ps.particleCount == 0 && !ps.isEmitting, "Reused lease simulation expires particles before StopEmitting reassertion.");
                foreach (var ps in systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                update.Invoke(item, null);
                // [C] 자연 drain 반환 후에는 맞춤 배율이 남지 않고 프리팹 원본 스케일로 돌아와야 한다.
                Require(!item.BloomLoopActive && VfxApprox(item.transform.localScale, authoredScale),
                    "Fitted Bloom natural return restores the authored scale for the next reuse.");
                var bad = systems[0].main; bad.loop = false; // instance only, never authored asset.
                Require(item.BeginBloomAcquire() && item.PlayBloomLoop(next, pose.transform, preset, proof) == 0, "Malformed authored PS contract fails before Play.");
                // [A] 개수 하한 회귀 방지: 파티클 시스템이 0개인 임시 오브젝트는 재생 전에 거부(0 반환)돼야 한다.
                //   원본 프리팹·config는 건드리지 않는 임시 오브젝트다.
                var zeroGo = new GameObject("BloomZeroPSFixture") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    var zero = zeroGo.AddComponent<Hexiege.Presentation.VfxPoolItem>();
                    int zeroReturns = 0; zero.Setup(preset.VfxPrefab, _ => zeroReturns++);
                    Require(zero.BeginBloomAcquire() && zero.PlayBloomLoop(next, pose.transform, preset, proof) == 0
                        && !zero.BloomLoopActive, "Zero authored PS Bloom item is rejected before Play.");
                    zero.RejectBloomAcquire();
                    Require(zeroReturns == 1, "Zero PS rejected acquire returns exactly once.");
                }
                finally { UnityEngine.Object.DestroyImmediate(zeroGo); }
                int beforeReject = returns; item.RejectBloomAcquire(); item.RejectBloomAcquire();
                Require(returns == beforeReject + 1 && !item.gameObject.activeSelf && !item.BloomLoopActive, "Failed acquire clears/deactivates/returns exactly once.");
                foreach (var ps in systems) Require(!ps.IsAlive(false), "Failed acquire leaves no visible/orphan loop.");

                root = new GameObject("BloomFixturePresenter") { hideFlags = HideFlags.HideAndDontSave };
                var presenter = root.AddComponent<Hexiege.Presentation.BloomHealEffectPresenter>();
                var manager = root.AddComponent<Hexiege.Presentation.EffectManager>();
                typeof(Hexiege.Presentation.EffectManager).GetField("_vfxContainer", f).SetValue(manager, root.transform);
                manager.Initialize(config, null, null);
                // 기존 manager의 실제 checkout -> Play 실패 -> 풀 반환 경로도 통과한다.
                // malformed인 것은 임시 인스턴스뿐이며 원본 prefab/config를 변경하지 않는다.
                var pools = (Dictionary<GameObject, Queue<Hexiege.Presentation.VfxPoolItem>>)
                    typeof(Hexiege.Presentation.EffectManager).GetField("_vfxPools", f).GetValue(manager);
                var returnToManager = typeof(Hexiege.Presentation.EffectManager).GetMethod("OnVfxReturn", f);
                item.Setup(preset.VfxPrefab, returned => returnToManager.Invoke(manager, new object[] { returned }));
                var failedQueue = new Queue<Hexiege.Presentation.VfxPoolItem>(); failedQueue.Enqueue(item);
                pools.Add(preset.VfxPrefab, failedQueue);
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    Require(manager.StartBloomEffect(next, pose.transform, new BloomHealEffectDiagnostics()) == null
                        && failedQueue.Count == 1 && ReferenceEquals(failedQueue.Peek(), item)
                        && !item.gameObject.activeSelf && !item.BloomLoopActive,
                        "Production malformed checkout returns once without a successful/orphan lease.");
                    foreach (var ps in systems) Require(!ps.IsAlive(false), "Production failed checkout clears every PS.");
                }
                pools.Remove(preset.VfxPrefab); // 나머지 fixture는 정상 authored prefab으로 새 checkout한다.
                var present = typeof(Hexiege.Presentation.BloomHealEffectPresenter).GetMethod("LateUpdate", f);
                var local = new BloomHealEffectChannel(); Transform ready = null;
                presenter.Initialize(local, manager, _ => ready);
                var single = local.Grant(target);
                present.Invoke(presenter, null);
                local.Stop(ref single, target, BloomHealStopReason.FullHP);
                ready = pose.transform; present.Invoke(presenter, null);
                Require(root.GetComponentsInChildren<Hexiege.Presentation.VfxPoolItem>(true).Length == 0, "Stop before pose ready never Plays.");
                single = local.Grant(target); local.Stop(ref single, target, BloomHealStopReason.Expired); present.Invoke(presenter, null);
                Require(root.GetComponentsInChildren<Hexiege.Presentation.VfxPoolItem>(true).Length == 0, "Same-frame grant-stop current coalesces without Play.");
                single = local.Grant(target); present.Invoke(presenter, null);
                var emitted = root.GetComponentsInChildren<Hexiege.Presentation.VfxPoolItem>(true);
                Require(emitted.Length == 1 && emitted[0].BloomLoopActive, "Sole presenter emits one current owner.");
                ulong hostLease = emitted[0].BloomLease;
                local.Refresh(ref single, target); present.Invoke(presenter, null);
                Require(emitted[0].BloomLease == hostLease && root.GetComponentsInChildren<Hexiege.Presentation.VfxPoolItem>(true).Length == 1, "Host refresh retains one emitter/lease.");
                local.CloseAttachment(target, "Despawn"); pose.transform.position += Vector3.one * 40;
                present.Invoke(presenter, null); local.CopyCurrent(entries);
                Require(entries.Count == 0 && !emitted[0].BloomLoopActive, "Local retire closes pending/follow and removes dead reference.");
                var map = new BloomHealEffectChannel(); var mapState = map.Grant(replacement);
                presenter.Initialize(map, manager, _ => pose.transform); present.Invoke(presenter, null);
                Require(local.IsClosed && root.GetComponentsInChildren<Hexiege.Presentation.VfxPoolItem>(true).Length == 2, "New map current restores after old channel closes; old tail stays independent.");
                Require(!local.Accept(single, target), "Closed map never accepts obsolete current.");
                var formatted = BloomHealEffectDiagnostics.Format("Return", terminal, lease, new string('한', 1500));
                Require(formatted.Contains("Peer=") && formatted.Contains("Frame=") && formatted.Contains("FormatIncomplete=true")
                    && Encoding.UTF8.GetByteCount(GameLog.Compose(Hexiege.Application.LogLevel.Info, "Combat", nameof(BloomHealEffectDiagnostics), "[BLOOM-EFFECT]", formatted)) < 1000,
                    "Fallback full envelope preserves exact peer/key under budget.");
                map.CloseVisuals("FixtureTeardown");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (itemGo != null) UnityEngine.Object.DestroyImmediate(itemGo);
                if (pose != null) UnityEngine.Object.DestroyImmediate(pose);
                channel.CloseVisuals("FixtureEnd");
                typeof(Hexiege.Presentation.EffectManager).GetProperty("Instance").SetValue(null, previousManager);
            }
            ValidateBloomHoTWriterAndNetwork();
        }

        private static void ValidateBloomHoTWriterAndNetwork()
        {
            const BindingFlags f = BindingFlags.Instance | BindingFlags.NonPublic;
            var spawn = new UnitSpawnUseCase(null);
            var units = (Dictionary<int, UnitData>)typeof(UnitSpawnUseCase).GetField("_units", f).GetValue(spawn);
            var target = new UnitData(9902, UnitType.LittleKnight, TeamId.Blue, new HexCoord(0, 0), 100, 10, 1, 1);
            target.TakeDamage(50); units[target.Id] = target;
            var healer = new UnitData(9903, UnitType.BloomFairy, TeamId.Blue, new HexCoord(0, 0), 100, 0, 4, 4);
            var other = new UnitData(9904, UnitType.TorrentSpirit, TeamId.Blue, new HexCoord(0, 0), 100, 0, 4, 4);
            var combat = new UnitCombatUseCase(null, spawn, null, null, new HexMetricsCoordinateMapper());
            var upsert = typeof(UnitCombatUseCase).GetMethod("AddOrRefreshTimedEffect", f);
            Action<bool, UnitCombatUseCase.TimedEffectKind> grant = (bloom, kind) => upsert.Invoke(combat,
                new object[] { healer, target, kind, 20, 3f, 0f, 0, default(UnitHealCastPresentation), bloom, TimedDamageVisual.None, default(AttackEffectPresentation) });
            combat.CastHeal(other, target.Id);
            Require(!combat.BloomHealEffects.TryGet(target, out _), "Non-Bloom actual CastHeal never grants Bloom VFX.");
            combat.CastHeal(healer, target.Id);
            Require(combat.BloomHealEffects.TryGet(target, out var current) && current.Active, "Actual Bloom CastHeal upsert grants recipient effect.");
            var initial = current;
            combat.CastHeal(healer, target.Id);
            Require(combat.BloomHealEffects.TryGet(target, out current) && current.SameOwner(initial) && current.Revision > initial.Revision, "Actual writer refresh retains visual instance.");
            grant(false, UnitCombatUseCase.TimedEffectKind.Heal);
            Require(combat.BloomHealEffects.TryGet(target, out current) && !current.Active && current.StopReason == BloomHealStopReason.NonBloomOverwrite, "Generic Heal overwrite closes Bloom visual only.");
            grant(false, UnitCombatUseCase.TimedEffectKind.Damage);
            Require(combat.BloomHealEffects.TryGet(target, out current) && !current.Active, "DoT provenance does not create Bloom.");
            // Remove only fixture DoT so this independent test does not publish fake damage/death events.
            var timed = (System.Collections.IList)typeof(UnitCombatUseCase).GetField("_activeTimedEffects", f).GetValue(combat);
            timed.Clear();
            grant(true, UnitCombatUseCase.TimedEffectKind.Heal);
            healer.TakeDamage(1000);
            int hp = target.Hp; combat.TickTimedEffects(0);
            Require(combat.BloomHealEffects.TryGet(target, out current) && current.Active && target.Hp == hp, "Caster death and zero scaled progress do not cancel granted HoT.");
            target.Heal(1000); combat.TickTimedEffects(0);
            Require(combat.BloomHealEffects.TryGet(target, out current) && !current.Active && current.StopReason == BloomHealStopReason.FullHP, "Paused/zero-dt tick still removes FullHP.");
            target.TakeDamage(30); grant(true, UnitCombatUseCase.TimedEffectKind.Heal);
            units.Remove(target.Id); combat.TickTimedEffects(0);
            Require(!combat.BloomHealEffects.TryGet(target, out _), "Unavailable actual writer removal retires strong recipient reference.");
            units[target.Id] = target; grant(true, UnitCombatUseCase.TimedEffectKind.Heal);

            GameObject root = null;
            var previousServices = GameServicesLocator.Current;
            bool netActive = NetworkContext.IsNetworkActive, netServer = NetworkContext.IsNetworkServer;
            try
            {
                root = new GameObject("BloomNetworkReadyFixture") { hideFlags = HideFlags.HideAndDontSave };
                var view = root.AddComponent<Hexiege.Presentation.UnitView>();
                var network = root.AddComponent<NetworkUnit>();
                var services = new NativeHostServices { Combat = combat, Unit = root };
                GameServicesLocator.Register(services);
                NetworkContext.Set(true, true);
                // Only NGO envelope readiness is substituted; real NetworkUnit baseline/recipient code runs.
                typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsSpawned").SetValue(network, true);
                typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsServer").SetValue(network, true);
                var id = (Unity.Netcode.NetworkVariable<int>)typeof(NetworkUnit).GetField("_unitId", f).GetValue(network);
                id.Value = target.Id;
                typeof(NetworkUnit).GetField("_bloomCursor", f).SetValue(network, new BloomHealEffectCursor());
                typeof(NetworkUnit).GetField("_bloomReadyPending", f).SetValue(network, true);
                var apply = typeof(NetworkUnit).GetMethod("ApplyCurrentBloomEffect", f);
                apply.Invoke(network, null);
                Require(!network.BloomEffectState.IsValid, "Host cannot publish a baseline before UnitView recipient binding.");
                typeof(Hexiege.Presentation.UnitView).GetField("_unitData", f).SetValue(view, target);
                apply.Invoke(network, null);
                Require(combat.BloomHealEffects.TryGet(target, out current) && network.BloomEffectState.Equals(current), "Host view-ready retries and publishes existing current baseline.");
                int reads = services.CombatReads;
                for (int i = 0; i < 64; i++) apply.Invoke(network, null);
                Require(services.CombatReads == reads && services.UnitReads == 0, "Applied readiness has zero repeated services/factory lookup.");
                var client = new BloomHealEffectChannel();
                typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsServer").SetValue(network, false);
                NetworkContext.Set(true, false);
                typeof(NetworkUnit).GetField("_bloomCursor", f).SetValue(network, new BloomHealEffectCursor());
                typeof(NetworkUnit).GetField("_bloomTarget", f).SetValue(network, null);
                typeof(NetworkUnit).GetField("_bloomChannel", f).SetValue(network, client);
                typeof(NetworkUnit).GetField("_bloomReadyPending", f).SetValue(network, true);
                apply.Invoke(network, null);
                Require(client.TryGet(target, out var received) && received.Equals(current), "Client current binds cursor and Application expected lifetime.");
                var impostor = new UnitData(target.Id, target.Type, target.Team, target.Position, 100, 10, 1, 1);
                typeof(Hexiege.Presentation.UnitView).GetField("_unitData", f).SetValue(view, impostor);
                typeof(NetworkUnit).GetField("_bloomReadyPending", f).SetValue(network, true);
                typeof(NetworkUnit).GetField("_bloomAppliedRevision", f).SetValue(network, 0UL);
                apply.Invoke(network, null);
                Require(!client.TryGet(impostor, out _), "Same spawned cursor cannot apply old payload to a new UnitData with same ID.");
                client.CloseVisuals("MapClosed");
                var nextCombat = new UnitCombatUseCase(null, spawn, null, null, new HexMetricsCoordinateMapper());
                var nextCurrent = nextCombat.BloomHealEffects.Grant(impostor);
                services.Combat = nextCombat;
                var wire = (Unity.Netcode.NetworkVariable<NetworkBloomHealEffectState>)typeof(NetworkUnit).GetField("_bloomEffectState", f).GetValue(network);
                wire.Value = new NetworkBloomHealEffectState { Value = nextCurrent };
                typeof(NetworkUnit).GetMethod("OnBloomEffectChanged", f).Invoke(network,
                    new object[] { new NetworkBloomHealEffectState { Value = current }, new NetworkBloomHealEffectState { Value = nextCurrent } });
                Require(nextCombat.BloomHealEffects.TryGet(impostor, out received) && received.Equals(nextCurrent)
                    && ReferenceEquals(typeof(NetworkUnit).GetField("_bloomTarget", f).GetValue(network), impostor),
                    "New revision/new match rebinds after old channel closes; no old lifetime is restored.");
            }
            finally
            {
                if (root != null)
                {
                    var network = root.GetComponent<NetworkUnit>();
                    typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsSpawned").SetValue(network, false);
                    UnityEngine.Object.DestroyImmediate(root);
                }
                GameServicesLocator.Register(previousServices); NetworkContext.Set(netActive, netServer);
                combat.BloomHealEffects.CloseVisuals("FixtureEnd");
            }
        }

        private static void ValidateBloomHealPresentationBoundary()
        {
            var root = new GameObject("BloomHealBoundary") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var child = new GameObject("VisualRoot");
                child.transform.SetParent(root.transform, false);
                var animator = child.AddComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/_Project/Animations/Units/BloomFairy/BloomFairy.controller");
                animator.applyRootMotion = false;
                animator.Rebind();
                animator.Update(0);
                var view = root.AddComponent<Hexiege.Presentation.UnitView>();
                var network = root.AddComponent<NetworkUnit>();
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                var data = new UnitData(901, UnitType.BloomFairy, TeamId.Blue,
                    new HexCoord(0, 0), 100, 0, 4, 4);
                typeof(UnitData).GetField("<IsHealer>k__BackingField", fields).SetValue(data, true);
                typeof(Hexiege.Presentation.UnitView).GetField("_unitData", fields).SetValue(view, data);
                typeof(Hexiege.Presentation.UnitView).GetField("_animator", fields).SetValue(view, animator);
                var hash = typeof(Hexiege.Presentation.UnitView).GetField("_currentAnimStateHash", fields);
                var cursorField = typeof(Hexiege.Presentation.UnitView).GetField("_healPresentation", fields);
                var liveCursor = (UnitHealCastPresentationCursor)cursorField.GetValue(view);
                view.StartWalkAnimation();
                // 실제 old production Client 경계: replicated Attack만 주면 policy=false라 Walk다.
                typeof(NetworkUnit).GetMethod("ApplyAnimState", fields).Invoke(network, new object[] { (byte)2 });
                Require((int)hash.GetValue(view) == Animator.StringToHash("Walk"),
                    "Old healer Attack scalar must reproduce the missing-start seam without weakening generic policy.");
                var state = new UnitHealCastPresentation
                {
                    Lifetime = 91, Sequence = 1, Revision = 1, UnitId = 901, TargetId = 902,
                    StartedAt = network.HealServerTime - 0.05, AimX = 1, MarkerOffset = 1,
                    ClipDuration = 4, Active = true, Priority = 1
                };
                double anchor = state.StartedAt;
                Vector3 position = root.transform.position;
                Quaternion rotation = root.transform.rotation;
                // 실제 atomic 직렬화와 Client current-value 경계를 통과한다. 준비 전 호출은
                // 재생 성공으로 처리하지 않아야 하며 같은 현재 값 재적용이 성공해야 한다.
                typeof(Hexiege.Presentation.UnitView).GetField("_unitData", fields).SetValue(view, null);
                ApplyBloomNetworkFixture(network, state);
                Require(view.HealPresentation.Sequence == 0, "Uninitialized View must not consume a heal start.");
                typeof(Hexiege.Presentation.UnitView).GetField("_unitData", fields).SetValue(view, data);
                typeof(NetworkUnit).GetMethod("ApplyCurrentHealCast", fields).Invoke(network, null);
                Require(view.HealPresentation.Sequence == state.Sequence
                    && (int)hash.GetValue(view) == Animator.StringToHash("Attack"),
                    "Actual NetworkUnit current-value adapter must recover a start after late View initialization.");
                // Update가 호출하는 실제 힐 facing 경계: 소유권 없음은 무변경,
                // Action 인계 뒤 0.1초는 27도만 회전하며 새 target 방향도 점진 추적한다.
                var facingTarget = new GameObject("HealFacingTarget");
                facingTarget.transform.SetParent(root.transform, false);
                facingTarget.transform.position = root.transform.position + Vector3.right * 4;
                var targetView = facingTarget.AddComponent<Hexiege.Presentation.UnitView>();
                var targetData = new UnitData(902, UnitType.LittleKnight, TeamId.Blue,
                    new HexCoord(0, 0), 100, 0, 4, 4);
                typeof(Hexiege.Presentation.UnitView).GetField("_unitData", fields).SetValue(targetView, targetData);
                typeof(Hexiege.Presentation.UnitView).GetField("_healFacingTarget", fields).SetValue(view, facingTarget.transform);
                typeof(Hexiege.Presentation.UnitView).GetField("_healFacingTargetView", fields).SetValue(view, targetView);
                var facing = typeof(Hexiege.Presentation.UnitView).GetMethod("UpdateHealFacing", fields);
                var ownership = (UnitRootRotationOwnership)typeof(Hexiege.Presentation.UnitView)
                    .GetField("_rootRotationOwnership", fields).GetValue(view);
                facing.Invoke(view, new object[] { 0.1f });
                Require(root.transform.rotation == rotation, "No Action ownership must suppress the heal Root writer.");
                ownership.TransferToAction();
                facing.Invoke(view, new object[] { 0.1f });
                Require(Mathf.Abs(Quaternion.Angle(rotation, root.transform.rotation) - 27) < 0.001f,
                    "Actual heal facing must advance 270 degrees/sec, not snap to the 90-degree target.");
                facingTarget.transform.position = root.transform.position + Vector3.forward * 4;
                facing.Invoke(view, new object[] { 0.1f });
                Require(Quaternion.Angle(rotation, root.transform.rotation) < 0.001f,
                    "Actual heal facing must keep following a changed target direction.");
                ownership.ReleaseAction(false);
                root.transform.rotation = rotation;
                Require(!view.ApplyHealPresentation(state, anchor + 0.5), "Duplicate heal must not restart.");
                view.StartWalkAnimation();
                Require((int)hash.GetValue(view) == Animator.StringToHash("Attack"), "Walk must not overwrite active heal.");
                state.Priority = 4; state.Revision++;
                Require(view.ApplyHealPresentation(state, anchor + 0.6) && animator.speed == 0, "Held priority must survive heal.");
                state.Priority = 3; state.Revision++;
                Require(view.ApplyHealPresentation(state, anchor + 0.7) && animator.speed == 0, "Frozen takes priority.");
                state.Priority = 1; state.Revision++;
                Require(view.ApplyHealPresentation(state, anchor - 0.1) && animator.speed == 0,
                    "Priority release before due must stay pending, not consume resume.");
                typeof(Hexiege.Presentation.UnitView).GetMethod("MaintainHealPresentation", fields)
                    .Invoke(view, new object[] { anchor + 0.8 });
                Require(animator.speed == 1, "Pending same-sequence resume must apply once due.");

                state.Sequence++; state.Revision++;
                state.StartedAt = network.HealServerTime + 100;
                ulong playedBeforeFuture = liveCursor.PlayedSequence;
                ApplyBloomNetworkFixture(network, state);
                Require(view.HealPresentation.Active && liveCursor.PlayedSequence == playedBeforeFuture,
                    "Actual NetworkUnit receive must store a future sequence without immediate playback.");
                typeof(Hexiege.Presentation.UnitView).GetMethod("MaintainHealPresentation", fields)
                    .Invoke(view, new object[] { state.StartedAt });
                Require(liveCursor.PlayedSequence == state.Sequence && animator.speed == 1
                    && (int)hash.GetValue(view) == Animator.StringToHash("Attack"),
                    "Continuous new sequence starts when due without intermediate Walk/Held.");
                typeof(Hexiege.Presentation.UnitView).GetMethod("MaintainHealPresentation", fields)
                    .Invoke(view, new object[] { state.StartedAt + 4.1 });
                Require(animator.speed == 1, "Ordinary 4s motion end must not force speed zero.");
                state.Active = false; state.Revision++;
                state.Priority = 4;
                ApplyBloomNetworkFixture(network, state);
                Require(!view.HealPresentation.Active && animator.speed == 0,
                    "Network end restores actual stationary Held.");
                state.Active = true; state.Revision++;
                Require(!view.ApplyHealPresentation(state, 104), "Ended same sequence must never resurrect.");
                state.Sequence++; state.Revision++;
                state.StartedAt = network.HealServerTime - 0.05;
                state.Priority = 1;
                state.EndReason = 0; state.EndedAt = 0;
                ApplyBloomNetworkFixture(network, state);
                Require(liveCursor.PlayedSequence == state.Sequence,
                    "Actual receive restarts a fresh due sequence even in Attack state.");
                state.Sequence++; state.Revision++;
                state.StartedAt = network.HealServerTime + 100;
                ApplyBloomNetworkFixture(network, state);
                ulong beforeDeath = liveCursor.PlayedSequence;
                view.EndHealPresentation("validation-death", terminal: true);
                typeof(Hexiege.Presentation.UnitView).GetMethod("MaintainHealPresentation", fields)
                    .Invoke(view, new object[] { state.StartedAt + 1 });
                Require(liveCursor.PlayedSequence == beforeDeath, "Terminal close cancels a pending future start.");
                state.Sequence++; state.Revision++;
                Require(!view.ApplyHealPresentation(state, 105), "Terminal lifecycle rejects all future starts.");
                Require(root.transform.position == position && root.transform.rotation == rotation,
                    "Client heal adapter must never write Simulation Root.");
                var cursor = new UnitHealCastPresentationCursor();
                state.StartedAt = 100;
                Require(cursor.Accept(state) && cursor.PlayedSequence == 0, "Receive is not successful playback.");
                cursor.MarkPlayed(100.4);
                Require(cursor.ConsumeMarker() && !cursor.ConsumeMarker(), "One source marker per heal cycle.");
                state.Sequence++; state.Revision++;
                Require(cursor.Accept(state), "Fresh sequence must be accepted.");
                cursor.MarkPlayed(102);
                Require(!cursor.ConsumeMarker() && Math.Abs(cursor.Offset(102) - 2) < 0.0001,
                    "Late initialization catches up without replaying an elapsed marker.");

                data.AttackCooldown = 3;
                for (int peer = 0; peer < 2; peer++) UnitFactory.ApplyAttackClipCooldown(data, 4);
                Require(data.AttackCooldown == 3 && data.AttackCooldown + 1 == 4,
                    "Both factory paths preserve healer post-cast 3s, not clip-derived 4s.");
                var ordinary = new UnitData(903, UnitType.LittleKnight, TeamId.Blue,
                    new HexCoord(0, 0), 100, 0, 4, 4);
                UnitFactory.ApplyAttackClipCooldown(ordinary, 2);
                Require(ordinary.AttackCooldown == 2, "Ordinary clip-derived cooldown must remain unchanged.");
                var curve = new AnimationCurve(new Keyframe(0, 0, 0, 2),
                    new Keyframe(0.7f, 1, -1, 0.5f), new Keyframe(1.7f, 2, 1, -2), new Keyframe(3, 0, 0, 0));
                var retimed = BloomHealRetimingValidation.RetimeCurve(curve);
                Require(Mathf.Abs(retimed.keys[retimed.length - 1].time - 4) < 0.0001f,
                    "Native retime helper must move the tail, not merely extend the last pose.");
                for (int sample = 0; sample <= 120; sample++)
                    Require(Mathf.Abs(curve.Evaluate(sample / 120f) - retimed.Evaluate(sample / 120f)) < 0.00002f,
                        "Split-boundary retime must preserve the entire first second.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void ApplyBloomNetworkFixture(NetworkUnit network, UnitHealCastPresentation state)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            using var writer = new Unity.Netcode.FastBufferWriter(256, Unity.Collections.Allocator.Temp);
            writer.WriteNetworkSerializable(new NetworkUnitHealCastState { Value = state });
            using var reader = new Unity.Netcode.FastBufferReader(writer, Unity.Collections.Allocator.Temp);
            reader.ReadNetworkSerializable(out NetworkUnitHealCastState decoded);
            Require(decoded.Value.Equals(state), "Heal snapshot atomic serialization must preserve every field.");
            typeof(NetworkUnit).GetField("_unitId", fields).SetValue(network,
                new Unity.Netcode.NetworkVariable<int>(state.UnitId));
            typeof(NetworkUnit).GetField("_healCastState", fields).SetValue(network,
                new Unity.Netcode.NetworkVariable<NetworkUnitHealCastState>(decoded));
            typeof(NetworkUnit).GetMethod("OnHealCastChanged", fields).Invoke(network,
                new object[] { default(NetworkUnitHealCastState), decoded });
        }

        private static void ValidateBloomFairyProductionTimeline()
        {
            const string root = "Assets/_Project/";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                root + "Animations/Units/BloomFairy/BloomFairy_Attack.anim");
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                root + "Animations/Units/BloomFairy/BloomFairy.controller");
            GameObject blue = AssetDatabase.LoadAssetAtPath<GameObject>(
                root + "Prefabs/Units/Transcendence/Unit_BloomFairy_Blue.prefab");
            GameObject red = AssetDatabase.LoadAssetAtPath<GameObject>(
                root + "Prefabs/Units/Transcendence/Unit_BloomFairy_Red.prefab");
            Require(clip != null && controller != null && blue != null && red != null,
                "BloomFairy production clip, controller and both prefabs must exist.");
            int attackStates = 0;
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != "Base Layer") continue;
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state.name != "Attack") continue;
                    attackStates++;
                    Require(child.state.motion == clip && Mathf.Approximately(child.state.speed, 1f)
                            && !child.state.speedParameterActive,
                        "BloomFairy Base Layer/Attack must play the production clip at speed 1.");
                }
            }
            Require(attackStates == 1, "BloomFairy must have exactly one Base Layer/Attack state.");
            float[] markers = UnitFactory.GetHitFrameTimes(clip);
            Require(Mathf.Abs(clip.frameRate - 30f) < 0.0001f
                    && Mathf.Abs(clip.length - 4f) < 0.0001f && clip.isLooping
                    && markers.Length == 1 && Mathf.Abs(markers[0] - 1f) < 0.0001f,
                "BloomFairy must use a looping 30fps/4s clip with one OnAttackHit at 1s.");
            UnitStatsConfig config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(
                root + "Resources/Config/UnitStatsConfig.asset");
            Require(config != null, "BloomFairy production stats must exist.");
            int statRows = 0;
            foreach (UnitStatEntry entry in config.Stats)
            {
                if (entry.unitType != UnitType.BloomFairy) continue;
                statRows++;
                Require(entry.isHealer && Mathf.Abs(entry.attackCooldown - 3f) < 0.0001f
                        && entry.hitFrameTimes != null && entry.hitFrameTimes.Length == 1
                        && Mathf.Abs(entry.hitFrameTimes[0] - 1f) < 0.0001f,
                    "BloomFairy must retain healer=true, 1s preparation and 3s post-cast cooldown.");
            }
            Require(statRows == 1, "BloomFairy must have exactly one production stats row.");
            Require(UnitAttackShadowProfileResolver.TryResolve(UnitType.BloomFairy, out var profile)
                    && profile.Support == UnitAttackShadowSupport.NotApplicableAttack
                    && profile.LegacyExecution == LegacyAttackExecutionKind.SeparateHealPipeline,
                "BloomFairy must remain N/A with a separate server heal pipeline.");
            foreach (GameObject prefab in new[] { blue, red })
            {
                var viewProperties = new SerializedObject(prefab.GetComponent<Hexiege.Presentation.UnitView>());
                Require(Mathf.Abs(viewProperties.FindProperty("_healCastBlend").floatValue - 0.15f) < 0.0001f
                    && Mathf.Abs(viewProperties.FindProperty("_toAttackBlend").floatValue - 0.08f) < 0.0001f,
                    "Both Bloom prefabs must store heal-only 0.15s blend; ordinary Attack blend stays 0.08s.");
                Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
                Require(animators.Length == 1 && animators[0].runtimeAnimatorController == controller
                        && !animators[0].applyRootMotion
                        && animators[0].GetComponent<Hexiege.Presentation.AnimationEventRelay>() != null,
                    "Both BloomFairy prefabs need the production controller and same-object relay without root motion.");
            }
            // 미리보기로 저장된 생산 등록만 읽고 현재 편집 중인 씬은 바꾸지 않는다.
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(root + "Scenes/Game.unity");
            try
            {
                int factories = 0;
                int entries = 0;
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                foreach (UnitFactory factory in sceneRoot.GetComponentsInChildren<UnitFactory>(true))
                {
                    factories++;
                    var serialized = new SerializedObject(factory);
                    SerializedProperty rows = serialized.FindProperty("_transcendencePrefabs");
                    Require(rows != null && rows.isArray, "Game UnitFactory needs Transcendence registrations.");
                    for (int index = 0; index < rows.arraySize; index++)
                    {
                        SerializedProperty row = rows.GetArrayElementAtIndex(index);
                        // 희소 enum은 목록 순서가 아니라 실제 저장 숫자 27로 비교한다.
                        if (row.FindPropertyRelative("type").intValue != (int)UnitType.BloomFairy) continue;
                        entries++;
                        Require(row.FindPropertyRelative("attackTimelineClip").objectReferenceValue == clip
                                && row.FindPropertyRelative("blue").objectReferenceValue == blue
                                && row.FindPropertyRelative("red").objectReferenceValue == red,
                            "Game UnitFactory BloomFairy must bind the exact Attack clip and both prefabs.");
                    }
                }
                Require(factories == 1 && entries == 1, "Game needs one UnitFactory and one BloomFairy registration.");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// <summary>
        /// Pistoleer의 실제 생산 연결과 30fps 0:24(0.8초) 표식 및 2초 주기를 확인한다.
        /// 로컬 Animation Event는 표현 표식이며 기존 서버 피해 권한을 바꾸지 않는다.
        /// </summary>
        private static void ValidatePistoleerProductionTimeline()
        {
            const string root = "Assets/_Project/";
            const string clipPath = root + "Animations/Units/Pistoleer/Pistoleer_Attack.anim";
            const string controllerPath = root + "Animations/Units/Pistoleer/Pistoleer.controller";
            const string bluePath = root + "Prefabs/Units/Human/Unit_Pistoleer_Blue.prefab";
            const string redPath = root + "Prefabs/Units/Human/Unit_Pistoleer_Red.prefab";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            GameObject blue = AssetDatabase.LoadAssetAtPath<GameObject>(bluePath);
            GameObject red = AssetDatabase.LoadAssetAtPath<GameObject>(redPath);
            Require(clip != null && controller != null && blue != null && red != null
                    && AssetDatabase.AssetPathToGUID(clipPath) == "3dc19d88c3a013c46af811597796c70a"
                    && AssetDatabase.AssetPathToGUID(controllerPath) == "f65c79390b6c2734fae59aa4d0a48098"
                    && AssetDatabase.AssetPathToGUID(bluePath) == "7bb22226c2f892a4b861224ca882519a"
                    && AssetDatabase.AssetPathToGUID(redPath) == "0fb1fc8960816a449a56977bd4ca2c78",
                "Pistoleer production clip, controller, and Blue/Red prefabs must retain their verified identities.");

            int attackStates = 0;
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != "Base Layer") continue;
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state.name != "Attack") continue;
                    attackStates++;
                    Require(child.state.motion == clip && Mathf.Approximately(child.state.speed, 1f)
                            && !child.state.speedParameterActive,
                        "Pistoleer Base Layer/Attack must play the bound clip at speed 1.");
                }
            }
            Require(attackStates == 1, "Pistoleer must have exactly one Base Layer/Attack state.");
            float[] markers = UnitFactory.GetHitFrameTimes(clip);
            Require(Mathf.Abs(clip.frameRate - 30f) < 0.0001f
                    && Mathf.Abs(UnitFactory.GetAttackClipLength(clip) - 2f) < 0.0001f
                    && clip.isLooping && clip.events.Length == 1 && markers.Length == 1
                    && Mathf.Abs(markers[0] - 24f / 30f) < 0.0001f,
                "Pistoleer Attack must remain a looping 30fps/2s clip with one OnAttackHit at 24/30s.");

            UnitStatsConfig config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(
                root + "Resources/Config/UnitStatsConfig.asset");
            Require(config != null, "Pistoleer production stats must exist.");
            int statRows = 0;
            foreach (UnitStatEntry entry in config.Stats)
            {
                if (entry.unitType != UnitType.Pistoleer) continue;
                statRows++;
                Require(Mathf.Abs(entry.attackCooldown - 2f) < 0.0001f
                        && entry.hitFrameTimes != null && entry.hitFrameTimes.Length == 1
                        && Mathf.Abs(entry.hitFrameTimes[0] - 24f / 30f) < 0.0001f,
                    "Pistoleer config must retain cooldown 2s and one fallback impact at 24/30s.");
            }
            Require(statRows == 1, "Pistoleer must have exactly one production stats row.");
            foreach (GameObject prefab in new[] { blue, red })
            {
                Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
                Require(animators.Length == 1 && animators[0].runtimeAnimatorController == controller
                        && !animators[0].applyRootMotion
                        && animators[0].GetComponent<Hexiege.Presentation.AnimationEventRelay>() != null,
                    "Both Pistoleer prefabs need the verified controller and same-object event relay without root motion.");
            }

            // Inspect production registrations without modifying or saving the user's open scene.
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(root + "Scenes/Game.unity");
            try
            {
                int factories = 0;
                int entries = 0;
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                foreach (UnitFactory factory in sceneRoot.GetComponentsInChildren<UnitFactory>(true))
                {
                    factories++;
                    var serialized = new SerializedObject(factory);
                    SerializedProperty rows = serialized.FindProperty("_humanPrefabs");
                    Require(rows != null && rows.isArray, "Game UnitFactory must serialize Human registrations.");
                    for (int index = 0; index < rows.arraySize; index++)
                    {
                        SerializedProperty row = rows.GetArrayElementAtIndex(index);
                        if (row.FindPropertyRelative("type").intValue != (int)UnitType.Pistoleer) continue;
                        entries++;
                        Require(row.FindPropertyRelative("attackTimelineClip").objectReferenceValue == clip
                                && row.FindPropertyRelative("blue").objectReferenceValue == blue
                                && row.FindPropertyRelative("red").objectReferenceValue == red,
                            "Game UnitFactory Pistoleer entry must bind the Attack clip and both production prefabs.");
                    }
                }
                Require(factories == 1 && entries == 1,
                    "Game scene must contain exactly one UnitFactory and one Pistoleer Human registration.");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
            Require(UnitFactory.SelectAttackTimelineClip(clip, controller.animationClips) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, new AnimationClip[0]) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, null) == clip,
                "Pistoleer explicit Attack clip must win controller enumeration and empty-list cases.");
        }

        /// <summary>
        /// Assault의 사용자 기준 30fps 0:05를 실제 생산 자산과 두 생성 경로의 공통 helper에서
        /// 검사한다. 설정 주기 0.2초와 clip-derived 0:10을 구분하고 주기를 임의 변경하지 않는다.
        /// </summary>
        private static void ValidateSniperProductionTimeline()
        {
            const string root = "Assets/_Project/";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(root + "Animations/Units/Sniper/Sniper_Attack.anim");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(root + "Animations/Units/Sniper/Sniper.controller");
            var blue = AssetDatabase.LoadAssetAtPath<GameObject>(root + "Prefabs/Units/Human/Unit_Sniper_Blue.prefab");
            var red = AssetDatabase.LoadAssetAtPath<GameObject>(root + "Prefabs/Units/Human/Unit_Sniper_Red.prefab");
            Require(clip != null && controller != null && blue != null && red != null,
                "Sniper production assets must exist.");
            int attackStates = 0;
            foreach (var layer in controller.layers)
            {
                if (layer.name != "Base Layer") continue;
                foreach (var child in layer.stateMachine.states)
                {
                    if (child.state.name != "Attack") continue;
                    attackStates++;
                    Require(child.state.motion == clip && Mathf.Approximately(child.state.speed, 1f)
                        && !child.state.speedParameterActive, "Sniper exact Attack motion must play at speed 1.");
                }
            }
            var hits = UnitFactory.GetHitFrameTimes(clip);
            Require(attackStates == 1 && clip.isLooping && Mathf.Abs(clip.frameRate - 30f) < 0.0001f
                && Mathf.Abs(clip.length - 3f) < 0.0001f
                && clip.events.Length == 1 && hits.Length == 1 && Mathf.Abs(hits[0] - 52f / 30f) < 0.0001f,
                "Sniper must retain 30fps/3:00 duration and one 1:22 impact marker.");
            var config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(root + "Resources/Config/UnitStatsConfig.asset");
            Require(config != null, "Sniper stats must exist.");
            int statRows = 0;
            foreach (var entry in config.Stats)
            {
                if (entry.unitType != UnitType.Sniper) continue;
                statRows++;
                Require(Mathf.Abs(entry.attackCooldown - 3f) < 0.0001f && entry.hitFrameTimes != null
                    && entry.hitFrameTimes.Length == 1 && Mathf.Abs(entry.hitFrameTimes[0] - 52f / 30f) < 0.0001f,
                    "Sniper fallback hit must be 1:22; existing fallback cooldown must remain unchanged.");
            }
            Require(statRows == 1, "Sniper must have exactly one stats row.");
            foreach (var prefab in new[] { blue, red })
            {
                var animators = prefab.GetComponentsInChildren<Animator>(true);
                Require(animators.Length == 1 && animators[0].runtimeAnimatorController == controller
                    && !animators[0].applyRootMotion
                    && animators[0].GetComponent<Hexiege.Presentation.AnimationEventRelay>() != null,
                    "Sniper Blue/Red must share the verified controller and same-object relay without root motion.");
            }
            // Preview is read-only. Never run unrelated setup, normalize, or save an open user scene.
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(root + "Scenes/Game.unity");
            try
            {
                int factories = 0, entries = 0;
                foreach (var sceneRoot in scene.GetRootGameObjects())
                foreach (var factory in sceneRoot.GetComponentsInChildren<UnitFactory>(true))
                {
                    factories++;
                    var rows = new SerializedObject(factory).FindProperty("_humanPrefabs");
                    Require(rows != null && rows.isArray, "Human registrations must exist.");
                    for (int i = 0; i < rows.arraySize; i++)
                    {
                        var row = rows.GetArrayElementAtIndex(i);
                        if (row.FindPropertyRelative("type").intValue != (int)UnitType.Sniper) continue;
                        entries++;
                        Require(row.FindPropertyRelative("attackTimelineClip").objectReferenceValue == clip
                            && row.FindPropertyRelative("blue").objectReferenceValue == blue
                            && row.FindPropertyRelative("red").objectReferenceValue == red,
                            "Sniper production registration must explicitly bind the exact clip and Blue/Red prefabs.");
                    }
                }
                Require(factories == 1 && entries == 1, "Exactly one production Sniper registration is required.");
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
            Require(UnitFactory.SelectAttackTimelineClip(clip, controller.animationClips) == clip
                && UnitFactory.SelectAttackTimelineClip(clip, null) == clip
                && UnitFactory.ShouldUseAttackClipHitFrameTimes(UnitType.Sniper, hits.Length),
                "Sniper must use its explicit timeline independent of controller enumeration.");
            var data = new UnitData(907, UnitType.Sniper, TeamId.Blue, new HexCoord(0, 0), 400, 10, 2, 2);
            data.AttackCooldown = 3f;
            UnitFactory.ApplyAttackClipCooldown(data, UnitFactory.GetAttackClipLength(clip));
            Require(Mathf.Abs(data.AttackCooldown - 3f) < 0.0001f,
                "Sniper runtime and fallback cooldown both remain 3 seconds.");
            Require(PistoleerTimingDiagnostics.Observes(UnitType.Sniper)
                && PistoleerTimingDiagnostics.Observes(UnitType.Pistoleer)
                && !PistoleerTimingDiagnostics.Observes(UnitType.TorrentSpirit)
                && !PistoleerTimingDiagnostics.Observes(UnitType.BloomFairy),
                "Timing observation extension must not change other type policies.");
            // Production Register/Event/Scoped paths, not just a handwritten log string.
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var scope = new AttackPresentationScope(new AttackerInstanceId(42), new AttackSequenceId(8), 0);
            PistoleerTimingDiagnostics.Begin("sniper-fixture", "typed-observation", true, 0);
            PistoleerTimingDiagnostics.Register(data, scope, 9, 17);
            PistoleerTimingDiagnostics.Event(data, "Schedule", scope, 0, "nominal=1.7333333");
            PistoleerTimingDiagnostics.Scoped("Receive", scope, 1.8, "outcome=HitApplied");
            var rowsField = (System.Collections.IDictionary)typeof(PistoleerTimingDiagnostics).GetField("Rows", flags).GetValue(null);
            object observation = rowsField[scope];
            Require(observation != null && (int)observation.GetType().GetField("Type").GetValue(observation) == 2,
                "Sniper hooks must retain exact type identity through scoped observations.");
            // Exercise production retention after the sample quota, without requiring new
            // native playback on every logical commit in a continuous epoch.
            observation.GetType().GetField("Sampled").SetValue(observation, false);
            PistoleerTimingDiagnostics.Event(data, "RenderedStart", scope, 0, "normalized=0 length=3 speed=1");
            PistoleerTimingDiagnostics.Event(data, "RawMarker", scope, 1.8, "normalized=0.5777778 continuity=1 renderedAnchor=0");
            var retained = (HashSet<string>)observation.GetType().GetField("Critical").GetValue(observation);
            Require(retained.Contains("RenderedStart") && retained.Contains("RawMarker"),
                "Sniper native timing stages must survive sample exclusion once per scope.");
            string raw = PistoleerTimingDiagnostics.Format("fixture", "run", "Host", "Schedule", 42, 8, 9, 0,
                data.Id, (int)data.Team, 0, 0, "nominal=1.7333333", (int)data.Type);
            Require(raw.Contains(" type=2 ") && Encoding.UTF8.GetByteCount(raw) <= 780,
                "Sniper production formatter must emit type identity within the existing byte budget.");
            PistoleerTimingDiagnostics.End(1.8, "sniper-fixture");
            Debug.Log("[UAS-DIAG][SNIPER-TIMELINE] PASS: 30fps 1:22, explicit production clip, 3s cycle, typed diagnostic hooks and native retention.");
        }

        private static void ValidateAssaultProductionTimeline()
        {
            const string root = "Assets/_Project/";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(root + "Animations/Units/Assault/Assault_Attack.anim");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(root + "Animations/Units/Assault/Assault.controller");
            var blue = AssetDatabase.LoadAssetAtPath<GameObject>(root + "Prefabs/Units/Human/Unit_Assault_Blue.prefab");
            var red = AssetDatabase.LoadAssetAtPath<GameObject>(root + "Prefabs/Units/Human/Unit_Assault_Red.prefab");
            Require(clip != null && controller != null && blue != null && red != null,
                "Assault production assets must exist.");
            int attackStates = 0;
            foreach (var layer in controller.layers)
            {
                if (layer.name != "Base Layer") continue;
                foreach (var child in layer.stateMachine.states)
                {
                    if (child.state.name != "Attack") continue;
                    attackStates++;
                    Require(child.state.motion == clip && Mathf.Approximately(child.state.speed, 1f)
                        && !child.state.speedParameterActive, "Assault exact Attack motion must play at speed 1.");
                }
            }
            var hits = UnitFactory.GetHitFrameTimes(clip);
            Require(attackStates == 1 && clip.isLooping && Mathf.Abs(clip.frameRate - 30f) < 0.0001f
                && Mathf.Abs(clip.length - 10f / 30f) < 0.0001f
                && clip.events.Length == 1 && hits.Length == 1 && Mathf.Abs(hits[0] - 5f / 30f) < 0.0001f,
                "Assault must retain 30fps/0:10 duration and one 0:05 impact marker.");
            var config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(root + "Resources/Config/UnitStatsConfig.asset");
            Require(config != null, "Assault stats must exist.");
            int statRows = 0;
            foreach (var entry in config.Stats)
            {
                if (entry.unitType != UnitType.Assault) continue;
                statRows++;
                Require(Mathf.Abs(entry.attackCooldown - 0.2f) < 0.0001f && entry.hitFrameTimes != null
                    && entry.hitFrameTimes.Length == 1 && Mathf.Abs(entry.hitFrameTimes[0] - 5f / 30f) < 0.0001f,
                    "Assault fallback hit must be 0:05; existing fallback cooldown must remain unchanged.");
            }
            Require(statRows == 1, "Assault must have exactly one stats row.");
            foreach (var prefab in new[] { blue, red })
            {
                var animators = prefab.GetComponentsInChildren<Animator>(true);
                Require(animators.Length == 1 && animators[0].runtimeAnimatorController == controller
                    && !animators[0].applyRootMotion
                    && animators[0].GetComponent<Hexiege.Presentation.AnimationEventRelay>() != null,
                    "Assault Blue/Red must share the verified controller and same-object relay without root motion.");
            }
            // Preview is read-only. Never run unrelated setup, normalize, or save an open user scene.
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(root + "Scenes/Game.unity");
            try
            {
                int factories = 0, entries = 0;
                foreach (var sceneRoot in scene.GetRootGameObjects())
                foreach (var factory in sceneRoot.GetComponentsInChildren<UnitFactory>(true))
                {
                    factories++;
                    var rows = new SerializedObject(factory).FindProperty("_humanPrefabs");
                    Require(rows != null && rows.isArray, "Human registrations must exist.");
                    for (int i = 0; i < rows.arraySize; i++)
                    {
                        var row = rows.GetArrayElementAtIndex(i);
                        if (row.FindPropertyRelative("type").intValue != (int)UnitType.Assault) continue;
                        entries++;
                        Require(row.FindPropertyRelative("attackTimelineClip").objectReferenceValue == clip
                            && row.FindPropertyRelative("blue").objectReferenceValue == blue
                            && row.FindPropertyRelative("red").objectReferenceValue == red,
                            "Assault production registration must explicitly bind the exact clip and Blue/Red prefabs.");
                    }
                }
                Require(factories == 1 && entries == 1, "Exactly one production Assault registration is required.");
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
            Require(UnitFactory.SelectAttackTimelineClip(clip, controller.animationClips) == clip
                && UnitFactory.SelectAttackTimelineClip(clip, null) == clip
                && UnitFactory.ShouldUseAttackClipHitFrameTimes(UnitType.Assault, hits.Length),
                "Assault must use its explicit timeline independent of controller enumeration.");
            var data = new UnitData(907, UnitType.Assault, TeamId.Blue, new HexCoord(0, 0), 400, 10, 2, 2);
            data.AttackCooldown = 0.2f;
            UnitFactory.ApplyAttackClipCooldown(data, UnitFactory.GetAttackClipLength(clip));
            Require(Mathf.Abs(data.AttackCooldown - 10f / 30f) < 0.0001f,
                "Assault normal runtime cooldown remains clip-derived 0:10, not fallback 0.2s.");
            Require(PistoleerTimingDiagnostics.Observes(UnitType.Assault)
                && PistoleerTimingDiagnostics.Observes(UnitType.Pistoleer)
                && !PistoleerTimingDiagnostics.Observes(UnitType.TorrentSpirit)
                && !PistoleerTimingDiagnostics.Observes(UnitType.BloomFairy),
                "Timing observation extension must not change other type policies.");
            // Production Register/Event/Scoped paths, not just a handwritten log string.
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var scope = new AttackPresentationScope(new AttackerInstanceId(42), new AttackSequenceId(8), 0);
            PistoleerTimingDiagnostics.Begin("assault-fixture", "typed-observation", true, 0);
            PistoleerTimingDiagnostics.Register(data, scope, 9, 17);
            PistoleerTimingDiagnostics.Event(data, "Schedule", scope, 0, "nominal=0.16666667");
            PistoleerTimingDiagnostics.Scoped("Receive", scope, 0.2, "outcome=HitApplied");
            var rowsField = (System.Collections.IDictionary)typeof(PistoleerTimingDiagnostics).GetField("Rows", flags).GetValue(null);
            object observation = rowsField[scope];
            Require(observation != null && (int)observation.GetType().GetField("Type").GetValue(observation) == 1,
                "Assault hooks must retain exact type identity through scoped observations.");
            string raw = PistoleerTimingDiagnostics.Format("fixture", "run", "Host", "Schedule", 42, 8, 9, 0,
                data.Id, (int)data.Team, 0, 0, "nominal=0.16666667", (int)data.Type);
            Require(raw.Contains(" type=1 ") && Encoding.UTF8.GetByteCount(raw) <= 780,
                "Assault production formatter must emit type identity within the existing byte budget.");
            PistoleerTimingDiagnostics.End(0.2, "assault-fixture");
            Debug.Log("[UAS-DIAG][ASSAULT-TIMELINE] PASS: 30fps 0:05, explicit production clip, fallback/runtime separation, typed diagnostic hooks.");
        }

        /// <summary>
        /// BearGuard의 실제 생산 연결과 30fps 0:13(13/30초) 단일 타격을 확인한다.
        /// 설정의 쿨다운 1.2초는 이번 타격 시각 교정 대상이 아니므로 기존 값을 그대로 검사한다.
        /// </summary>
        private static void ValidateBearGuardProductionTimeline()
        {
            const string root = "Assets/_Project/";
            const string clipPath = root + "Animations/Units/BearGuard/BearGuard_Attack.anim";
            const string controllerPath = root + "Animations/Units/BearGuard/BearGuard.controller";
            const string bluePath = root + "Prefabs/Units/Transcendence/Unit_BearGuard_Blue.prefab";
            const string redPath = root + "Prefabs/Units/Transcendence/Unit_BearGuard_Red.prefab";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            GameObject blue = AssetDatabase.LoadAssetAtPath<GameObject>(bluePath);
            GameObject red = AssetDatabase.LoadAssetAtPath<GameObject>(redPath);
            Require(clip != null && controller != null && blue != null && red != null
                    && AssetDatabase.AssetPathToGUID(clipPath) == "6cd4a1668d01f24448599b56306f2b13"
                    && AssetDatabase.AssetPathToGUID(controllerPath) == "df3dfff57716a3e4caea2d278b2eff9e"
                    && AssetDatabase.AssetPathToGUID(bluePath) == "31b30650a4fe9034c8e0706bfe301d54"
                    && AssetDatabase.AssetPathToGUID(redPath) == "6c1185ed8d58ee34380e485d0bf76027",
                "BearGuard production clip, controller, and Blue/Red prefabs must retain their verified identities.");

            int attackStates = 0;
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != "Base Layer") continue;
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state.name != "Attack") continue;
                    attackStates++;
                    Require(child.state.motion == clip && Mathf.Approximately(child.state.speed, 1f)
                            && !child.state.speedParameterActive,
                        "BearGuard Base Layer/Attack must play the bound clip at speed 1.");
                }
            }
            Require(attackStates == 1, "BearGuard must have exactly one Base Layer/Attack state.");
            float[] markers = UnitFactory.GetHitFrameTimes(clip);
            Require(Mathf.Abs(clip.frameRate - 30f) < 0.0001f
                    && Mathf.Abs(UnitFactory.GetAttackClipLength(clip) - 1.6666666f) < 0.0001f
                    && clip.isLooping && clip.events.Length == 1 && markers.Length == 1
                    && Mathf.Abs(markers[0] - 13f / 30f) < 0.0001f,
                "BearGuard Attack must remain a looping 30fps/1.6666666s clip with one OnAttackHit at 13/30s.");

            UnitStatsConfig config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(
                root + "Resources/Config/UnitStatsConfig.asset");
            Require(config != null, "BearGuard production stats must exist.");
            int statRows = 0;
            foreach (UnitStatEntry entry in config.Stats)
            {
                if (entry.unitType != UnitType.BearGuard) continue;
                statRows++;
                Require(Mathf.Abs(entry.attackCooldown - 1.2f) < 0.0001f
                        && entry.hitFrameTimes != null && entry.hitFrameTimes.Length == 1
                        && Mathf.Abs(entry.hitFrameTimes[0] - 13f / 30f) < 0.0001f,
                    "BearGuard config must retain cooldown 1.2s and one fallback impact at 13/30s.");
            }
            Require(statRows == 1, "BearGuard must have exactly one production stats row.");

            foreach (GameObject prefab in new[] { blue, red })
            {
                Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
                Require(animators.Length == 1 && animators[0].runtimeAnimatorController == controller
                        && !animators[0].applyRootMotion
                        && animators[0].GetComponent<Hexiege.Presentation.AnimationEventRelay>() != null,
                    "Both BearGuard prefabs need the verified controller and same-object event relay without root motion.");
            }

            // 미리보기 씬에서만 생산 등록을 읽어 현재 편집 중인 씬을 바꾸거나 저장하지 않는다.
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(root + "Scenes/Game.unity");
            try
            {
                int factories = 0;
                int entries = 0;
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                foreach (UnitFactory factory in sceneRoot.GetComponentsInChildren<UnitFactory>(true))
                {
                    factories++;
                    var serialized = new SerializedObject(factory);
                    SerializedProperty rows = serialized.FindProperty("_transcendencePrefabs");
                    Require(rows != null && rows.isArray,
                        "Game UnitFactory must serialize Transcendence registrations.");
                    for (int index = 0; index < rows.arraySize; index++)
                    {
                        SerializedProperty row = rows.GetArrayElementAtIndex(index);
                        // enumValueIndex가 아닌 실제 직렬화된 UnitType 숫자 20으로 행을 고른다.
                        if (row.FindPropertyRelative("type").intValue != (int)UnitType.BearGuard) continue;
                        entries++;
                        Require(row.FindPropertyRelative("attackTimelineClip").objectReferenceValue == clip
                                && row.FindPropertyRelative("blue").objectReferenceValue == blue
                                && row.FindPropertyRelative("red").objectReferenceValue == red,
                            "Game UnitFactory BearGuard entry must bind the Attack clip and both production prefabs.");
                    }
                }
                Require(factories == 1 && entries == 1,
                    "Game scene must contain exactly one UnitFactory and one BearGuard Transcendence registration.");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }

            // 명시 클립은 Controller의 클립 나열 순서나 빈 목록에 좌우되지 않아야 한다.
            Require(UnitFactory.SelectAttackTimelineClip(clip, controller.animationClips) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, new AnimationClip[0]) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, null) == clip,
                "BearGuard explicit Attack clip must win controller enumeration and empty-list cases.");
        }

        /// <summary>
        /// LionKnight의 실제 생산 연결과 두 번의 타격을 30fps 0:18, 1:13 순서로 확인한다.
        /// 두 Animation Event는 타격 시각 표식이며 실제 피해는 기존 서버 타이머가 적용한다.
        /// </summary>
        private static void ValidateLionKnightProductionTimeline()
        {
            const string root = "Assets/_Project/";
            const string clipPath = root + "Animations/Units/LionKnight/LionKnight_Attack.anim";
            const string controllerPath = root + "Animations/Units/LionKnight/LionKnight.controller";
            const string bluePath = root + "Prefabs/Units/Transcendence/Unit_LionKnight_Blue.prefab";
            const string redPath = root + "Prefabs/Units/Transcendence/Unit_LionKnight_Red.prefab";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            GameObject blue = AssetDatabase.LoadAssetAtPath<GameObject>(bluePath);
            GameObject red = AssetDatabase.LoadAssetAtPath<GameObject>(redPath);
            Require(clip != null && controller != null && blue != null && red != null
                    && AssetDatabase.AssetPathToGUID(clipPath) == "45c00f46f91a76047a76e774cfa0f6ac"
                    && AssetDatabase.AssetPathToGUID(controllerPath) == "85ce28fd23241d24b84dc9149050c194"
                    && AssetDatabase.AssetPathToGUID(bluePath) == "fb844688f9e49504584a5c95f185365b"
                    && AssetDatabase.AssetPathToGUID(redPath) == "60882e4b6ee1f20409867db71e9f6248",
                "LionKnight production clip, controller, and Blue/Red prefabs must retain their verified identities.");

            int attackStates = 0;
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != "Base Layer") continue;
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state.name != "Attack") continue;
                    attackStates++;
                    Require(child.state.motion == clip && Mathf.Approximately(child.state.speed, 1f)
                            && !child.state.speedParameterActive,
                        "LionKnight Base Layer/Attack must play the bound clip at speed 1.");
                }
            }
            Require(attackStates == 1, "LionKnight must have exactly one Base Layer/Attack state.");

            float firstHit = 18f / 30f;
            float secondHit = 43f / 30f;
            AnimationEvent[] events = clip.events;
            float[] markers = UnitFactory.GetHitFrameTimes(clip);
            Require(Mathf.Abs(clip.frameRate - 30f) < 0.0001f
                    && Mathf.Abs(UnitFactory.GetAttackClipLength(clip) - 2f) < 0.0001f
                    && clip.isLooping && events.Length == 2 && markers.Length == 2
                    && events[0].functionName == "OnAttackHit"
                    && events[1].functionName == "OnAttackHit"
                    && Mathf.Abs(events[0].time - firstHit) < 0.0001f
                    && Mathf.Abs(events[1].time - secondHit) < 0.0001f
                    && markers[0] < markers[1]
                    && Mathf.Abs(markers[0] - firstHit) < 0.0001f
                    && Mathf.Abs(markers[1] - secondHit) < 0.0001f,
                "LionKnight Attack must remain a looping 30fps/2s clip with two ordered OnAttackHit markers at frames 18 and 43.");

            UnitStatsConfig config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(
                root + "Resources/Config/UnitStatsConfig.asset");
            Require(config != null, "LionKnight production stats must exist.");
            int statRows = 0;
            foreach (UnitStatEntry entry in config.Stats)
            {
                if (entry.unitType != UnitType.LionKnight) continue;
                statRows++;
                Require(Mathf.Abs(entry.attackCooldown - 3f) < 0.0001f
                        && entry.hitFrameTimes != null && entry.hitFrameTimes.Length == 2
                        && entry.hitFrameTimes[0] < entry.hitFrameTimes[1]
                        && Mathf.Abs(entry.hitFrameTimes[0] - firstHit) < 0.0001f
                        && Mathf.Abs(entry.hitFrameTimes[1] - secondHit) < 0.0001f,
                    "LionKnight config must retain cooldown 3s and ordered fallback impacts at frames 18 and 43.");
            }
            Require(statRows == 1, "LionKnight must have exactly one production stats row.");

            foreach (GameObject prefab in new[] { blue, red })
            {
                Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
                Require(animators.Length == 1 && animators[0].runtimeAnimatorController == controller
                        && !animators[0].applyRootMotion
                        && animators[0].GetComponent<Hexiege.Presentation.AnimationEventRelay>() != null,
                    "Both LionKnight prefabs need the verified controller and same-object event relay without root motion.");
            }

            // 미리보기 씬에서 실제 UnitFactory 등록만 읽고 사용자가 편집 중인 씬은 변경하지 않는다.
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(root + "Scenes/Game.unity");
            try
            {
                int factories = 0;
                int entries = 0;
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                foreach (UnitFactory factory in sceneRoot.GetComponentsInChildren<UnitFactory>(true))
                {
                    factories++;
                    var serialized = new SerializedObject(factory);
                    SerializedProperty rows = serialized.FindProperty("_transcendencePrefabs");
                    Require(rows != null && rows.isArray,
                        "Game UnitFactory must serialize Transcendence registrations.");
                    for (int index = 0; index < rows.arraySize; index++)
                    {
                        SerializedProperty row = rows.GetArrayElementAtIndex(index);
                        // enumValueIndex는 선언 순서다. 실제 직렬화된 UnitType 숫자 22로 행을 고른다.
                        if (row.FindPropertyRelative("type").intValue != (int)UnitType.LionKnight) continue;
                        entries++;
                        Require(row.FindPropertyRelative("attackTimelineClip").objectReferenceValue == clip
                                && row.FindPropertyRelative("blue").objectReferenceValue == blue
                                && row.FindPropertyRelative("red").objectReferenceValue == red,
                            "Game UnitFactory LionKnight entry must bind the Attack clip and both production prefabs.");
                    }
                }
                Require(factories == 1 && entries == 1,
                    "Game scene must contain exactly one UnitFactory and one LionKnight Transcendence registration.");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }

            // 명시된 Attack 클립이 Controller 나열 순서나 빈 목록보다 우선해야 두 히트가 흔들리지 않는다.
            Require(UnitFactory.SelectAttackTimelineClip(clip, controller.animationClips) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, new AnimationClip[0]) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, null) == clip,
                "LionKnight explicit Attack clip must win controller enumeration and empty-list cases.");
        }

        /// <summary>
        /// SpearMan의 실제 생산 연결과 30fps 0:25 단일 타격 표식을 확인한다.
        /// Animation Event는 표현 시점이며 서버 권위 피해 경로는 변경하지 않는다.
        /// </summary>
        private static void ValidateSpearManProductionTimeline()
        {
            const string root = "Assets/_Project/";
            const string clipPath = root + "Animations/Units/SpearMan/SpearMan_Attack.anim";
            const string controllerPath = root + "Animations/Units/SpearMan/SpearMan.controller";
            const string bluePath = root + "Prefabs/Units/Human/Unit_SpearMan_Blue.prefab";
            const string redPath = root + "Prefabs/Units/Human/Unit_SpearMan_Red.prefab";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            GameObject blue = AssetDatabase.LoadAssetAtPath<GameObject>(bluePath);
            GameObject red = AssetDatabase.LoadAssetAtPath<GameObject>(redPath);
            Require(clip != null && controller != null && blue != null && red != null
                    && AssetDatabase.AssetPathToGUID(clipPath) == "4772066a52e78bc4cb4890c02068d0ea"
                    && AssetDatabase.AssetPathToGUID(controllerPath) == "302eb96e50e71a64cb6b5624f11960b2"
                    && AssetDatabase.AssetPathToGUID(bluePath) == "0ccbe86e05acded448d0dab522d79418"
                    && AssetDatabase.AssetPathToGUID(redPath) == "73b703117e11b2a4da17ddb7ee460bd9",
                "SpearMan production clip, controller, and Blue/Red prefabs must retain their verified identities.");

            int attackStates = 0;
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != "Base Layer") continue;
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state.name != "Attack") continue;
                    attackStates++;
                    Require(child.state.motion == clip && Mathf.Approximately(child.state.speed, 1f)
                            && !child.state.speedParameterActive,
                        "SpearMan Base Layer/Attack must play the bound clip at speed 1.");
                }
            }
            Require(attackStates == 1, "SpearMan must have exactly one Base Layer/Attack state.");

            float hitTime = 25f / 30f;
            AnimationEvent[] events = clip.events;
            float[] markers = UnitFactory.GetHitFrameTimes(clip);
            Require(Mathf.Abs(clip.frameRate - 30f) < 0.0001f
                    && Mathf.Abs(UnitFactory.GetAttackClipLength(clip) - 2f) < 0.0001f
                    && clip.isLooping && events.Length == 1 && markers.Length == 1
                    && events[0].functionName == "OnAttackHit"
                    && Mathf.Abs(events[0].time - hitTime) < 0.0001f
                    && Mathf.Abs(markers[0] - hitTime) < 0.0001f,
                "SpearMan Attack must remain a looping 30fps/2s clip with one OnAttackHit at frame 25.");

            UnitStatsConfig config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(
                root + "Resources/Config/UnitStatsConfig.asset");
            Require(config != null, "SpearMan production stats must exist.");
            int statRows = 0;
            foreach (UnitStatEntry entry in config.Stats)
            {
                if (entry.unitType != UnitType.SpearMan) continue;
                statRows++;
                Require(Mathf.Abs(entry.attackCooldown - 2f) < 0.0001f
                        && entry.hitFrameTimes != null && entry.hitFrameTimes.Length == 1
                        && Mathf.Abs(entry.hitFrameTimes[0] - hitTime) < 0.0001f,
                    "SpearMan config must retain cooldown 2s and one fallback impact at frame 25.");
            }
            Require(statRows == 1, "SpearMan must have exactly one production stats row.");

            foreach (GameObject prefab in new[] { blue, red })
            {
                Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
                Require(animators.Length == 1 && animators[0].runtimeAnimatorController == controller
                        && !animators[0].applyRootMotion
                        && animators[0].GetComponent<Hexiege.Presentation.AnimationEventRelay>() != null,
                    "Both SpearMan prefabs need the verified controller and same-object event relay without root motion.");
            }

            // Preview Scene에서 생산 등록을 읽으므로 현재 열린 씬을 바꾸거나 저장하지 않는다.
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(root + "Scenes/Game.unity");
            try
            {
                int factories = 0;
                int entries = 0;
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                foreach (UnitFactory factory in sceneRoot.GetComponentsInChildren<UnitFactory>(true))
                {
                    factories++;
                    var serialized = new SerializedObject(factory);
                    SerializedProperty rows = serialized.FindProperty("_humanPrefabs");
                    Require(rows != null && rows.isArray,
                        "Game UnitFactory must serialize Human registrations.");
                    for (int index = 0; index < rows.arraySize; index++)
                    {
                        SerializedProperty row = rows.GetArrayElementAtIndex(index);
                        // enum 순서가 아니라 직렬화된 UnitType 숫자 4로 생산 행을 선택한다.
                        if (row.FindPropertyRelative("type").intValue != (int)UnitType.SpearMan) continue;
                        entries++;
                        Require(row.FindPropertyRelative("attackTimelineClip").objectReferenceValue == clip
                                && row.FindPropertyRelative("blue").objectReferenceValue == blue
                                && row.FindPropertyRelative("red").objectReferenceValue == red,
                            "Game UnitFactory SpearMan entry must bind the Attack clip and both production prefabs.");
                    }
                }
                Require(factories == 1 && entries == 1,
                    "Game scene must contain exactly one UnitFactory and one SpearMan Human registration.");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }

            // 명시 클립이 Controller 목록 순서와 빈 목록보다 우선해야 타격 시점이 고정된다.
            Require(UnitFactory.SelectAttackTimelineClip(clip, controller.animationClips) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, new AnimationClip[0]) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, null) == clip,
                "SpearMan explicit Attack clip must win controller enumeration and empty-list cases.");
        }

        /// <summary>
        /// 실제 Game 씬의 EmberSpirit 등록, 양 팀 Animator와 1초 타격 표식을 함께 확인한다.
        /// 설정의 2.2초는 기존 폴백 값이고, 생성 시에는 선택된 2.6666667초 클립 길이가 우선한다.
        /// 씬은 preview로 열고 반드시 닫아 사용자가 열어 둔 씬을 교체하거나 저장하지 않는다.
        /// </summary>
        private static void ValidateEmberSpiritProductionTimeline()
        {
            const string root = "Assets/_Project/";
            const string clipPath = root + "Animations/Units/EmberSpirit/EmberSpirit_Attack.anim";
            const string controllerPath = root + "Animations/Units/EmberSpirit/EmberSpirit.controller";
            const string bluePath = root + "Prefabs/Units/Spirit/Unit_EmberSpirit_Blue.prefab";
            const string redPath = root + "Prefabs/Units/Spirit/Unit_EmberSpirit_Red.prefab";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            GameObject blue = AssetDatabase.LoadAssetAtPath<GameObject>(bluePath);
            GameObject red = AssetDatabase.LoadAssetAtPath<GameObject>(redPath);
            Require(clip != null && controller != null && blue != null && red != null
                    && AssetDatabase.AssetPathToGUID(clipPath) == "335daaffd649791409558085159725aa"
                    && AssetDatabase.AssetPathToGUID(controllerPath) == "c604ed991e03ad24a90acbf0fb938b9d"
                    && AssetDatabase.AssetPathToGUID(bluePath) == "65437a9ab49692a44b94f8c5d97a93b4"
                    && AssetDatabase.AssetPathToGUID(redPath) == "aaff65119146e2248a29f23c479cd35f",
                "EmberSpirit production clip, controller, and both prefabs must retain their verified identities.");

            int attackStates = 0;
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != "Base Layer") continue;
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state.name != "Attack") continue;
                    attackStates++;
                    Require(child.state.motion == clip && Mathf.Approximately(child.state.speed, 1f)
                            && !child.state.speedParameterActive,
                        "EmberSpirit Base Layer/Attack must play the explicitly bound clip at speed 1.");
                }
            }
            Require(attackStates == 1, "EmberSpirit must have exactly one Base Layer/Attack state.");
            float[] markers = UnitFactory.GetHitFrameTimes(clip);
            Require(Mathf.Abs(clip.frameRate - 30f) < 0.0001f
                    && Mathf.Abs(UnitFactory.GetAttackClipLength(clip) - 2.6666667f) < 0.0001f
                    && markers.Length == 1 && Mathf.Abs(markers[0] - 1f) < 0.0001f,
                "EmberSpirit Attack must remain 30fps, 2.6666667s, with one OnAttackHit at 1s.");

            UnitStatsConfig config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(
                root + "Resources/Config/UnitStatsConfig.asset");
            Require(config != null, "EmberSpirit production stats must exist.");
            int statRows = 0;
            foreach (UnitStatEntry entry in config.Stats)
            {
                if (entry.unitType != UnitType.EmberSpirit) continue;
                statRows++;
                Require(Mathf.Abs(entry.attackCooldown - 2.2f) < 0.0001f
                        && entry.hitFrameTimes != null && entry.hitFrameTimes.Length == 1
                        && Mathf.Abs(entry.hitFrameTimes[0] - markers[0]) < 0.0001f,
                    "EmberSpirit config must retain its 2.2s fallback cooldown and one 1s impact.");
            }
            Require(statRows == 1, "EmberSpirit must have exactly one production stats row.");

            // 명시 참조가 있으면 목록이 비어도 선택되며, 이름 기반 폴백을 사용하지 않는다.
            Require(UnitFactory.SelectAttackTimelineClip(clip, null) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, new AnimationClip[0]) == clip,
                "EmberSpirit explicit Attack clip must take priority over controller enumeration.");
            foreach (GameObject prefab in new[] { blue, red })
            {
                Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
                Require(animators.Length == 1 && animators[0].runtimeAnimatorController == controller
                        && !animators[0].applyRootMotion,
                    "Both EmberSpirit prefabs must use the verified controller without root motion.");
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(root + "Scenes/Game.unity");
            try
            {
                int factories = 0;
                int entries = 0;
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                foreach (UnitFactory factory in sceneRoot.GetComponentsInChildren<UnitFactory>(true))
                {
                    factories++;
                    var serialized = new SerializedObject(factory);
                    SerializedProperty rows = serialized.FindProperty("_spiritPrefabs");
                    Require(rows != null && rows.isArray, "Game UnitFactory must serialize Spirit registrations.");
                    for (int index = 0; index < rows.arraySize; index++)
                    {
                        SerializedProperty row = rows.GetArrayElementAtIndex(index);
                        if (row.FindPropertyRelative("type").intValue != (int)UnitType.EmberSpirit) continue;
                        entries++;
                        Require(row.FindPropertyRelative("attackTimelineClip").objectReferenceValue == clip
                                && row.FindPropertyRelative("blue").objectReferenceValue == blue
                                && row.FindPropertyRelative("red").objectReferenceValue == red,
                            "Game UnitFactory EmberSpirit registration must bind its Attack clip and both production prefabs.");
                    }
                }
                Require(factories == 1 && entries == 1,
                    "Game scene must contain exactly one UnitFactory and one EmberSpirit registration.");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }

            // 서버/싱글과 Client의 두 생성 경로가 같은 선택 결과에서 주기와 표식을 읽어야 한다.
            string source = File.ReadAllText(root + "Scripts/Infrastructure/Factories/UnitFactory.cs");
            foreach (string call in new[] { "GetAttackTimelineClip(unitData, animator)",
                         "GetAttackClipLength(attackClip)", "GetHitFrameTimes(attackClip)" })
                Require(source.Split(new[] { call }, StringSplitOptions.None).Length - 1 == 2,
                    "Host and Client UnitFactory paths must share EmberSpirit selection and extraction: " + call);
        }

        /// <summary>
        /// 실제 Game 씬의 DustSpirit 등록과 양 팀 Animator의 Attack motion을 대조한다.
        /// 별도 Attack2를 앞에 둔 목록도 같은 생산 선택 함수에 넣어 순서 의존 재발을 잡는다.
        /// 씬은 preview로 열고 반드시 닫아 사용자가 열어 둔 씬을 교체하거나 저장하지 않는다.
        /// </summary>
        private static void ValidateDustSpiritProductionTimeline()
        {
            const string root = "Assets/_Project/";
            const string clipPath = root + "Animations/Units/DustSpirit/DustSpirit_Attack.anim";
            const string controllerPath = root + "Animations/Units/DustSpirit/DustSpirit.controller";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            AnimationClip alternate = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                root + "Animations/Units/DustSpirit/DustSpirit_Attack2.anim");
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            Require(clip != null && alternate != null && controller != null
                    && AssetDatabase.AssetPathToGUID(clipPath) == "961cafd21c1fa13429c74aa65a3a6345"
                    && AssetDatabase.AssetPathToGUID(controllerPath) == "c6b047497cf8c3b41bd1f1757edd7ff0",
                "DustSpirit production Attack clip and controller must retain their verified identities.");

            int attackStates = 0;
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != "Base Layer") continue;
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state.name != "Attack") continue;
                    attackStates++;
                    Require(child.state.motion == clip && Mathf.Approximately(child.state.speed, 1f)
                            && !child.state.speedParameterActive,
                        "DustSpirit Base Layer/Attack must play the explicitly bound clip at speed 1.");
                }
            }
            Require(attackStates == 1, "DustSpirit must have exactly one Base Layer/Attack state.");
            float[] markers = UnitFactory.GetHitFrameTimes(clip);
            Require(Mathf.Abs(UnitFactory.GetAttackClipLength(clip) - 3f) < 0.0001f
                    && markers.Length == 1 && Mathf.Abs(markers[0] - 1.04f) < 0.0001f,
                "DustSpirit production extraction must retain cooldown 3s and one impact at 1.04s.");
            UnitStatsConfig config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(
                root + "Resources/Config/UnitStatsConfig.asset");
            Require(config != null, "DustSpirit production stats must exist.");
            int statRows = 0;
            foreach (UnitStatEntry entry in config.Stats)
            {
                if (entry.unitType != UnitType.DustSpirit) continue;
                statRows++;
                Require(Mathf.Abs(entry.attackCooldown - 3f) < 0.0001f
                        && entry.hitFrameTimes != null && entry.hitFrameTimes.Length == 1
                        && Mathf.Abs(entry.hitFrameTimes[0] - markers[0]) < 0.0001f,
                    "DustSpirit configured fallback must agree with the production Attack timeline.");
            }
            Require(statRows == 1, "DustSpirit must have exactly one production stats row.");

            var reversed = new[] { alternate, clip };
            Require(UnitFactory.SelectAttackTimelineClip(null, reversed) == alternate
                    && UnitFactory.GetAttackClipLength(alternate) > 4.6f
                    && UnitFactory.GetHitFrameTimes(alternate).Length == 0,
                "DustSpirit fixture must reproduce the old Attack2-first cooldown/marker mismatch.");
            Require(UnitFactory.SelectAttackTimelineClip(clip, reversed) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, new[] { clip, alternate }) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, null) == clip,
                "DustSpirit explicit production clip must win regardless of controller enumeration order.");

            GameObject blue = AssetDatabase.LoadAssetAtPath<GameObject>(
                root + "Prefabs/Units/Spirit/Unit_DustSpirit_Blue.prefab");
            GameObject red = AssetDatabase.LoadAssetAtPath<GameObject>(
                root + "Prefabs/Units/Spirit/Unit_DustSpirit_Red.prefab");
            foreach (GameObject prefab in new[] { blue, red })
            {
                Require(prefab != null, "Both DustSpirit production prefabs must exist.");
                Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
                Require(animators.Length == 1 && animators[0].runtimeAnimatorController == controller
                        && !animators[0].applyRootMotion,
                    "Both DustSpirit prefabs must use the verified controller without root motion.");
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(root + "Scenes/Game.unity");
            try
            {
                int factories = 0;
                int entries = 0;
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                foreach (UnitFactory factory in sceneRoot.GetComponentsInChildren<UnitFactory>(true))
                {
                    factories++;
                    var serialized = new SerializedObject(factory);
                    SerializedProperty rows = serialized.FindProperty("_spiritPrefabs");
                    Require(rows != null && rows.isArray, "Game UnitFactory must serialize Spirit registrations.");
                    for (int index = 0; index < rows.arraySize; index++)
                    {
                        SerializedProperty row = rows.GetArrayElementAtIndex(index);
                        if (row.FindPropertyRelative("type").intValue != (int)UnitType.DustSpirit) continue;
                        entries++;
                        Require(row.FindPropertyRelative("attackTimelineClip").objectReferenceValue == clip
                                && row.FindPropertyRelative("blue").objectReferenceValue == blue
                                && row.FindPropertyRelative("red").objectReferenceValue == red,
                            "Game UnitFactory DustSpirit registration must bind its actual Attack clip and both production prefabs.");
                    }
                }
                Require(factories == 1 && entries == 1,
                    "Game scene must contain exactly one UnitFactory and one DustSpirit registration.");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }

            // 실제 두 생성 경로 모두 같은 선택 결과에서 주기와 marker를 읽는지 확인한다.
            string source = File.ReadAllText(root + "Scripts/Infrastructure/Factories/UnitFactory.cs");
            foreach (string call in new[] { "GetAttackTimelineClip(unitData, animator)",
                         "GetAttackClipLength(attackClip)", "GetHitFrameTimes(attackClip)" })
                Require(source.Split(new[] { call }, StringSplitOptions.None).Length - 1 == 2,
                    "Host and Client UnitFactory paths must share DustSpirit selection and extraction: " + call);
        }

        /// <summary>
        /// 실제 Game 씬의 RabbitTrickster 등록과 양 팀 Animator의 Attack motion을 대조한다.
        /// 별도 Attack3를 앞에 둔 목록도 같은 생산 선택 함수에 넣어 순서 의존 재발을 잡는다.
        /// 씬은 preview로 열고 반드시 닫아 사용자가 열어 둔 씬을 교체하거나 저장하지 않는다.
        /// </summary>
        private static void ValidateRabbitTricksterProductionTimeline()
        {
            const string root = "Assets/_Project/";
            const string clipPath = root + "Animations/Units/RabbitTrickster/RabbitTrickster_Attack.anim";
            const string controllerPath = root + "Animations/Units/RabbitTrickster/RabbitTrickster.controller";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            AnimationClip alternate = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                root + "Animations/Units/RabbitTrickster/RabbitTrickster_Attack3.anim");
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            Require(clip != null && alternate != null && controller != null
                    && AssetDatabase.AssetPathToGUID(clipPath) == "8d21000f295b9774a9cb2540a71b8ce7"
                    && AssetDatabase.AssetPathToGUID(controllerPath) == "5b68d127286ca944f9aa795362e846d2",
                "RabbitTrickster production Attack clip and controller must retain their verified identities.");

            int attackStates = 0;
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != "Base Layer") continue;
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state.name != "Attack") continue;
                    attackStates++;
                    Require(child.state.motion == clip && Mathf.Approximately(child.state.speed, 1f)
                            && !child.state.speedParameterActive,
                        "RabbitTrickster Base Layer/Attack must play the explicitly bound clip at speed 1.");
                }
            }
            Require(attackStates == 1, "RabbitTrickster must have exactly one Base Layer/Attack state.");
            float[] markers = UnitFactory.GetHitFrameTimes(clip);
            Require(Mathf.Abs(UnitFactory.GetAttackClipLength(clip) - 2f) < 0.0001f
                    && markers.Length == 1 && Mathf.Abs(markers[0] - 0.6666667f) < 0.0001f,
                "RabbitTrickster production extraction must retain cooldown 2s and one impact at 0.6666667s (frame 20 at 30fps).");
            UnitStatsConfig config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(
                root + "Resources/Config/UnitStatsConfig.asset");
            Require(config != null, "RabbitTrickster production stats must exist.");
            int statRows = 0;
            foreach (UnitStatEntry entry in config.Stats)
            {
                if (entry.unitType != UnitType.RabbitTrickster) continue;
                statRows++;
                Require(Mathf.Abs(entry.attackCooldown - 2f) < 0.0001f
                        && entry.hitFrameTimes != null && entry.hitFrameTimes.Length == 1
                        && Mathf.Abs(entry.hitFrameTimes[0] - markers[0]) < 0.0001f,
                    "RabbitTrickster configured fallback must agree with the production Attack timeline.");
            }
            Require(statRows == 1, "RabbitTrickster must have exactly one production stats row.");

            var reversed = new[] { alternate, clip };
            Require(UnitFactory.SelectAttackTimelineClip(null, reversed) == alternate
                    && Mathf.Abs(UnitFactory.GetAttackClipLength(alternate) - 1.1f) < 0.0001f
                    && UnitFactory.GetHitFrameTimes(alternate).Length == 0,
                "RabbitTrickster fixture must reproduce the old Attack3-first cooldown/marker mismatch.");
            Require(UnitFactory.SelectAttackTimelineClip(clip, reversed) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, new[] { clip, alternate }) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, null) == clip,
                "RabbitTrickster explicit production clip must win regardless of controller enumeration order.");

            GameObject blue = AssetDatabase.LoadAssetAtPath<GameObject>(
                root + "Prefabs/Units/Transcendence/Unit_RabbitTrickster_Blue.prefab");
            GameObject red = AssetDatabase.LoadAssetAtPath<GameObject>(
                root + "Prefabs/Units/Transcendence/Unit_RabbitTrickster_Red.prefab");
            foreach (GameObject prefab in new[] { blue, red })
            {
                Require(prefab != null, "Both RabbitTrickster production prefabs must exist.");
                Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
                Require(animators.Length == 1 && animators[0].runtimeAnimatorController == controller
                        && !animators[0].applyRootMotion,
                    "Both RabbitTrickster prefabs must use the verified controller without root motion.");
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(root + "Scenes/Game.unity");
            try
            {
                int factories = 0;
                int entries = 0;
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                foreach (UnitFactory factory in sceneRoot.GetComponentsInChildren<UnitFactory>(true))
                {
                    factories++;
                    var serialized = new SerializedObject(factory);
                    SerializedProperty rows = serialized.FindProperty("_transcendencePrefabs");
                    Require(rows != null && rows.isArray, "Game UnitFactory must serialize Transcendence registrations.");
                    for (int index = 0; index < rows.arraySize; index++)
                    {
                        SerializedProperty row = rows.GetArrayElementAtIndex(index);
                        if (row.FindPropertyRelative("type").intValue != (int)UnitType.RabbitTrickster) continue;
                        entries++;
                        Require(row.FindPropertyRelative("attackTimelineClip").objectReferenceValue == clip
                                && row.FindPropertyRelative("blue").objectReferenceValue == blue
                                && row.FindPropertyRelative("red").objectReferenceValue == red,
                            "Game UnitFactory RabbitTrickster registration must bind its actual Attack clip and both production prefabs.");
                    }
                }
                Require(factories == 1 && entries == 1,
                    "Game scene must contain exactly one UnitFactory and one RabbitTrickster registration.");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }

            // 실제 두 생성 경로 모두 같은 선택 결과에서 주기와 marker를 읽는지 확인한다.
            string source = File.ReadAllText(root + "Scripts/Infrastructure/Factories/UnitFactory.cs");
            foreach (string call in new[] { "GetAttackTimelineClip(unitData, animator)",
                         "GetAttackClipLength(attackClip)", "GetHitFrameTimes(attackClip)" })
                Require(source.Split(new[] { call }, StringSplitOptions.None).Length - 1 == 2,
                    "Host and Client UnitFactory paths must share RabbitTrickster selection and extraction: " + call);
        }

        private static void ValidateInfernoAnimatorBodyApiAbsence()
        {
            string unitViewPath = Path.Combine(
                UnityEngine.Application.dataPath,
                "_Project/Scripts/Presentation/Unit/UnitView.cs");
            string observerPath = Path.Combine(
                UnityEngine.Application.dataPath,
                "_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs");
            Require(File.Exists(unitViewPath) && File.Exists(observerPath),
                "InfernoSpirit forbidden Animator member source gate requires both production source files.");

            string productionSource = File.ReadAllText(unitViewPath)
                + "\n" + File.ReadAllText(observerPath);
            string forbiddenPositionMember = string.Concat(".", "bodyPosition");
            string forbiddenRotationMember = string.Concat(".", "bodyRotation");
            Require(productionSource.IndexOf(forbiddenPositionMember, StringComparison.Ordinal) < 0
                    && productionSource.IndexOf(forbiddenRotationMember, StringComparison.Ordinal) < 0,
                "InfernoSpirit diagnostics must not access Animator body position/rotation members outside their permitted IK callbacks.");
        }

        /// <summary>
        /// 실제 결함 순서였던 Stop→Animation Event와 정상 Marker→Stop을 순수 상태로 재현한다.
        /// 일반 Stop은 이미 커밋된 공격자의 발사 표현만 정확히 남기고, 명시적 취소와
        /// 반복 marker는 아무 표현도 만들지 않아야 한다.
        /// </summary>
        private static void ValidateAttackSourceMarkerLease()
        {
            var stopBeforeLegacyMarker = new AttackPresentationSourceMarkerLease();
            Require(stopBeforeLegacyMarker.TryArm(11UL, 1)
                    && stopBeforeLegacyMarker.PreserveSourceOnly()
                    && stopBeforeLegacyMarker.TryConsume(out bool legacySourceOnly)
                    && legacySourceOnly
                    && UnitAttackPresentationPolicy.ResolveNetworkAttackMarkerEmission(
                        AttackPresentationImpactMode.Suppressed,
                        consumedValidScope: false,
                        consumedSourceMarker: true,
                        sourceOnly: true)
                        == AttackPresentationMarkerEmission.SourceOnly
                    && !stopBeforeLegacyMarker.TryConsume(out _),
                "A committed Legacy marker must survive an earlier Stop as source-only exactly once.");

            var markerBeforeStop = new AttackPresentationSourceMarkerLease();
            Require(markerBeforeStop.TryArm(12UL, 1)
                    && markerBeforeStop.TryConsume(out bool normalLegacySourceOnly)
                    && !normalLegacySourceOnly
                    && UnitAttackPresentationPolicy.ResolveNetworkAttackMarkerEmission(
                        AttackPresentationImpactMode.LegacyFallback,
                        consumedValidScope: false,
                        consumedSourceMarker: true,
                        sourceOnly: false)
                        == AttackPresentationMarkerEmission.Full
                    && !markerBeforeStop.PreserveSourceOnly()
                    && !markerBeforeStop.TryConsume(out _),
                "A Legacy marker consumed before Stop must emit fully once and never revive after Stop.");

            var scoped = new AttackPresentationSourceMarkerLease();
            Require(scoped.TryArm(13UL, 1)
                    && scoped.TryConsume(out bool scopedSourceOnly)
                    && !scopedSourceOnly
                    && UnitAttackPresentationPolicy.ResolveNetworkAttackMarkerEmission(
                        AttackPresentationImpactMode.Scoped,
                        consumedValidScope: true,
                        consumedSourceMarker: true,
                        sourceOnly: false)
                        == AttackPresentationMarkerEmission.Full
                    && UnitAttackPresentationPolicy.ResolveNetworkAttackMarkerEmission(
                        AttackPresentationImpactMode.Scoped,
                        consumedValidScope: false,
                        consumedSourceMarker: true,
                        sourceOnly: false)
                        == AttackPresentationMarkerEmission.Suppressed,
                "A Scoped marker must require both the bounded source reservation and exact scope consumption.");

            var explicitCancellation = new AttackPresentationSourceMarkerLease();
            Require(explicitCancellation.TryArm(14UL, 1),
                "An explicit-cancellation fixture must arm before cancellation.");
            explicitCancellation.Close();
            Require(!explicitCancellation.TryConsume(out _)
                    && UnitAttackPresentationPolicy.ResolveNetworkAttackMarkerEmission(
                        AttackPresentationImpactMode.Suppressed,
                        consumedValidScope: false,
                        consumedSourceMarker: false,
                        sourceOnly: false)
                        == AttackPresentationMarkerEmission.Suppressed,
                "Explicit suppression or attacker cancellation must discard every remaining marker.");

            var replacement = new AttackPresentationSourceMarkerLease();
            Require(replacement.TryArm(15UL, 2)
                    && replacement.RemainingMarkerCount == 2
                    && replacement.TryArm(16UL, 1)
                    && replacement.PresentationRevision == 16UL
                    && replacement.RemainingMarkerCount == 1
                    && replacement.TryConsume(out _)
                    && !replacement.TryConsume(out _),
                "A new presentation revision must atomically replace the old marker reservation.");

            var multiHitStop = new AttackPresentationSourceMarkerLease();
            Require(multiHitStop.TryArm(17UL, 2)
                    && multiHitStop.PreserveSourceOnly()
                    && multiHitStop.TryConsume(out bool firstSourceOnly)
                    && firstSourceOnly
                    && multiHitStop.TryConsume(out bool secondSourceOnly)
                    && secondSourceOnly
                    && !multiHitStop.TryConsume(out _),
                "A multi-hit Stop may preserve only its bounded remaining source markers and must reject overflow.");

            var invalid = new AttackPresentationSourceMarkerLease();
            Require(!invalid.TryArm(0UL, 1)
                    && !invalid.TryArm(18UL, 0)
                    && !invalid.TryArm(18UL, 65),
                "Source marker reservations must reject zero revisions and invalid marker counts.");
        }

        private static void ValidateC1AttackPresentationContinuityPolicy()
        {
            Require(UnitAttackPresentationPolicy.Evaluate(true, false, false)
                    == UnitAttackPresentationDecision.BeginProvisionalAttack,
                "Initial C1 alignment must start a provisional Attack presentation with impacts suppressed.");
            Require(UnitAttackPresentationPolicy.Evaluate(true, true, false)
                    == UnitAttackPresentationDecision.KeepAttackAndSuppressImpact,
                "An in-range retarget or realignment must keep Attack while suppressing precommit impacts.");
            Require(UnitAttackPresentationPolicy.Evaluate(true, true, true)
                    == UnitAttackPresentationDecision.BeginCommittedAttack,
                "A ready server gate must begin the committed Attack cycle.");
            Require(UnitAttackPresentationPolicy.Evaluate(false, true, false)
                    == UnitAttackPresentationDecision.EndAttack
                && UnitAttackPresentationPolicy.Evaluate(false, false, false)
                    == UnitAttackPresentationDecision.NoChange,
                "Attack presentation may end only when no in-range enemy remains.");
            Require(UnitAttackPresentationPolicy.ResolveTargetPublication(
                        serverActionReady: false,
                        provisionalStartCarriesTarget: false)
                    == CombatTargetPublicationDecision.StageCandidateOnly,
                "A pre-Action combat target must remain a server candidate and must not publish ChangeTarget.");
            Require(UnitAttackPresentationPolicy.ResolveTargetPublication(
                        serverActionReady: true,
                        provisionalStartCarriesTarget: true)
                    == CombatTargetPublicationDecision.PublishWithProvisionalStart,
                "A provisional Start must carry its target atomically without a duplicate ChangeTarget publication.");
            Require(UnitAttackPresentationPolicy.ResolveTargetPublication(
                        serverActionReady: true,
                        provisionalStartCarriesTarget: false)
                    == CombatTargetPublicationDecision.PublishTargetChange,
                "An active Action-owned retarget must publish exactly one target change.");
            Require(UnitAttackPresentationPolicy.TryAcceptRevision(1UL, 0UL, out ulong firstRevision)
                && firstRevision == 1UL
                && !UnitAttackPresentationPolicy.TryAcceptRevision(1UL, firstRevision, out _)
                && !UnitAttackPresentationPolicy.TryAcceptRevision(0UL, firstRevision, out _)
                && UnitAttackPresentationPolicy.TryAcceptRevision(2UL, firstRevision, out ulong secondRevision)
                && secondRevision == 2UL,
                "C1 presentation revisions must accept increasing values once and reject zero, duplicate and stale events.");
            for (int hitIndex = 0; hitIndex < 8; hitIndex++)
            {
                Require(!UnitAttackPresentationPolicy.ShouldEmitLocalImpact(true, false),
                    "Every precommit local hit marker must remain presentation-only suppressed.");
            }
            Require(UnitAttackPresentationPolicy.ShouldEmitLocalImpact(true, true)
                && UnitAttackPresentationPolicy.ShouldEmitLocalImpact(false, false),
                "Committed multiplayer cycles and legacy single-player presentation must still emit local impacts.");

            AttackPresentationCommitWindow currentCycle =
                UnitAttackPresentationPolicy.ResolveCommitWindow(
                    10d, 10.1d, 1d, 0.2d);
            AttackPresentationCommitWindow passedMarker =
                UnitAttackPresentationPolicy.ResolveCommitWindow(
                    10d, 10.3d, 1d, 0.2d);
            AttackPresentationCommitWindow nextCycle =
                UnitAttackPresentationPolicy.ResolveCommitWindow(
                    10d, 11.05d, 1d, 0.2d);
            Require(currentCycle.IsValid
                && currentCycle.ShouldCommit
                && Math.Abs(currentCycle.OvershootSeconds - 0.1d) < 0.000001d
                && passedMarker.IsValid
                && !passedMarker.ShouldCommit
                && Math.Abs(passedMarker.WaitSeconds - 0.7d) < 0.000001d
                && nextCycle.IsValid
                && nextCycle.ShouldCommit
                && Math.Abs(nextCycle.OvershootSeconds - 0.05d) < 0.000001d,
                "A provisional Attack must use only an unpassed marker in the current visual cycle and otherwise wait without rewinding for the next cycle.");
            Require(UnitAttackPresentationPolicy.ShouldCrossFadeAttack(true, false, false)
                && !UnitAttackPresentationPolicy.ShouldCrossFadeAttack(true, true, false)
                && !UnitAttackPresentationPolicy.ShouldCrossFadeAttack(true, false, true)
                && UnitAttackPresentationPolicy.ShouldCrossFadeAttack(false, true, true),
                "A multiplayer provisional-to-commit transition must preserve the active Attack clip while single-player keeps its legacy start boundary.");

            var provisional = new NetworkCombatStartedEvent(
                1, 2, true, false, 1UL, AttackPresentationImpactMode.Suppressed);
            var committedScope = new AttackPresentationScope(
                new AttackerInstanceId(91UL), new AttackSequenceId(7UL), 0);
            var committed = new NetworkCombatStartedEvent(
                1, 2, true, false, 2UL, AttackPresentationImpactMode.Scoped, committedScope);
            Require(!provisional.ImpactEnabled
                && !provisional.PresentationScope.IsValid
                && !provisional.RestartAttackCycle
                && committed.ImpactEnabled
                && committed.PresentationScope.Equals(committedScope)
                && !committed.RestartAttackCycle
                && committed.PresentationRevision > provisional.PresentationRevision,
                "The initial Align RPC payload must atomically suppress impact, while the 5-degree commit enables the continuous Attack without restarting it.");

            var unresolvedLegacy = new NetworkCombatStartedEvent(
                17,
                2,
                true,
                false,
                3UL,
                AttackPresentationImpactMode.LegacyFallback);
            Require(!unresolvedLegacy.ImpactEnabled
                && unresolvedLegacy.ImpactMode
                    == AttackPresentationImpactMode.LegacyFallback
                && !unresolvedLegacy.PresentationScope.IsValid
                && UnitAttackPresentationPolicy.ShouldEmitNetworkAttackMarker(
                    unresolvedLegacy.ImpactMode,
                    consumedValidScope: false)
                && !UnitAttackPresentationPolicy.ShouldEmitNetworkAttackMarker(
                    AttackPresentationImpactMode.Suppressed,
                    consumedValidScope: false)
                && UnitAttackPresentationPolicy.ShouldEmitNetworkAttackMarker(
                    AttackPresentationImpactMode.Scoped,
                    consumedValidScope: true)
                && !UnitAttackPresentationPolicy.ShouldEmitNetworkAttackMarker(
                    AttackPresentationImpactMode.Scoped,
                    consumedValidScope: false),
                "Unresolved profiles must preserve one Legacy marker without fabricating a scope, while Supported profiles remain fail-closed.");

            foreach (UnitType manifestType in
                UnitAttackShadowProfileResolver.AllManifestTypes)
            {
                Require(UnitAttackShadowProfileResolver.TryResolve(
                        manifestType, out UnitAttackShadowProfile manifestProfile),
                    "Every manifest type must resolve before selecting its presentation marker mode.");
                AttackPresentationImpactMode withoutScope =
                    UnitAttackPresentationPolicy.ResolveImpactMode(
                        manifestProfile.Support,
                        hasValidScope: false);
                AttackPresentationImpactMode withScope =
                    UnitAttackPresentationPolicy.ResolveImpactMode(
                        manifestProfile.Support,
                        hasValidScope: true);
                if (manifestProfile.Support == UnitAttackShadowSupport.Supported)
                {
                    Require(withoutScope == AttackPresentationImpactMode.Suppressed
                        && withScope == AttackPresentationImpactMode.Scoped,
                        "Every Supported unit type must require a valid scope before emitting an attack marker.");
                }
                else if (manifestProfile.Support == UnitAttackShadowSupport.Unresolved)
                {
                    Require(withoutScope == AttackPresentationImpactMode.LegacyFallback
                        && withScope == AttackPresentationImpactMode.LegacyFallback,
                        "Every Unresolved unit type must preserve Legacy presentation without claiming scoped C3 support.");
                }
                else
                {
                    Require(withoutScope == AttackPresentationImpactMode.Suppressed
                        && withScope == AttackPresentationImpactMode.Suppressed,
                        "Non-attack profiles must remain presentation-suppressed.");
                }
            }
            Require(!UnitAttackPresentationPolicy.ShouldStartAttackFromReplicatedLevelState(),
                "A separate NGO Attack level must wait for the atomic presentation command instead of opening a clip with stale impact state.");

            // 실제 전투는 같은 Attack 클립이 계속 루프하는 동안 여러 서버 공격 회차를
            // 커밋한다. 최초 표현 상태와 후속 scope 발행을 다시 하나의 가드로 묶지 않도록
            // 세 번의 연속 커밋을 독립 명령으로 검증한다.
            ulong acceptedContinuousRevision = 0UL;
            for (int sequence = 1; sequence <= 3; sequence++)
            {
                bool attackPresentationAlreadyActive = sequence > 1;
                AttackPresentationCommitDispatch dispatch =
                    UnitAttackPresentationPolicy.ResolveCommitDispatch(
                        attackPresentationAlreadyActive);
                var scope = new AttackPresentationScope(
                    new AttackerInstanceId(91UL),
                    new AttackSequenceId((ulong)sequence),
                    0);
                var command = new NetworkCombatStartedEvent(
                    1,
                    2,
                    true,
                    dispatch.RestartAttackCycle,
                    (ulong)(sequence + 2),
                    AttackPresentationImpactMode.Scoped,
                    scope);

                ulong nextContinuousRevision = acceptedContinuousRevision;
                Require(dispatch.ShouldPublishScope
                    && !dispatch.RestartAttackCycle
                    && dispatch.BeginsAttackPresentation == !attackPresentationAlreadyActive
                    && command.ImpactEnabled
                    && command.PresentationScope.IsValid
                    && command.PresentationScope.SequenceId.Value == (ulong)sequence
                    && command.PresentationScope.HitIndex == 0
                    && UnitAttackPresentationPolicy.TryAcceptRevision(
                        command.PresentationRevision,
                        acceptedContinuousRevision,
                        out nextContinuousRevision),
                    "Every committed attack cycle must publish a fresh hit-zero scope without restarting the active Attack clip.");
                acceptedContinuousRevision = nextContinuousRevision;
            }

            Require(!UnitAttackPresentationPolicy.TryAcceptRevision(
                    acceptedContinuousRevision,
                    acceptedContinuousRevision,
                    out _)
                && !UnitAttackPresentationPolicy.ShouldCrossFadeAttack(
                    true,
                    attackPresentationActive: true,
                    restartRequested: false),
                "A duplicate continuous-attack command must not rewind the latest scope or CrossFade the active Attack clip.");

            // 실제 UnitView 호출 모양: 한 번 받은 scope보다 Animation Event가 더 많이 반복된다.
            // dispatch 발행만 검사하면 마지막 HitIndex 뒤 emitter가 열린 채 남는 결함을 놓치므로,
            // production이 직접 사용할 동일 seam에서 scope의 시작과 종료를 함께 검증한다.
            var markerLease = new AttackPresentationImpactLease();
            Require(!markerLease.TryConsume(out _),
                "A closed presentation lease must suppress every provisional marker.");
            var firstMarkerScope = new AttackPresentationScope(
                new AttackerInstanceId(91UL),
                new AttackSequenceId(11UL),
                0);
            Require(markerLease.TryArm(firstMarkerScope, 1)
                && markerLease.TryConsume(out AttackPresentationScope consumedFirst)
                && consumedFirst.Equals(firstMarkerScope)
                && markerLease.Status == AttackPresentationImpactLeaseStatus.Consumed
                && !markerLease.TryConsume(out _),
                "A single-hit presentation scope must authorize exactly one marker and close immediately after consumption.");

            var multiMarkerScope = new AttackPresentationScope(
                new AttackerInstanceId(91UL),
                new AttackSequenceId(12UL),
                1);
            AttackPresentationScope multiHitTwo = default;
            Require(markerLease.TryArm(multiMarkerScope, 3)
                && markerLease.TryConsume(out AttackPresentationScope multiHitOne)
                && multiHitOne.HitIndex == 1
                && markerLease.Status == AttackPresentationImpactLeaseStatus.Armed
                && markerLease.TryConsume(out multiHitTwo)
                && multiHitTwo.HitIndex == 2
                && markerLease.Status == AttackPresentationImpactLeaseStatus.Consumed
                && !markerLease.TryConsume(out _),
                "A multi-hit presentation scope must consume its remaining HitIndexes once in order and then close.");

            AttackPresentationScope capturedRangedScope = multiHitTwo;
            Require(markerLease.TryGetSequenceScope(
                    out AttackPresentationScope retireScope)
                && retireScope.AttackerInstanceId == capturedRangedScope.AttackerInstanceId
                && retireScope.SequenceId == capturedRangedScope.SequenceId,
                "A consumed lease must preserve its exact sequence until UnitView publishes the lifecycle retire signal.");
            markerLease.Close();
            Require(capturedRangedScope.IsValid
                && capturedRangedScope.SequenceId.Value == 12UL
                && capturedRangedScope.HitIndex == 2
                && markerLease.Status == AttackPresentationImpactLeaseStatus.Closed
                && !markerLease.TryArm(default, 1),
                "A ranged tracer must retain its immutable launch scope while clear and invalid arm requests remain fail-closed.");
        }

        private static void ValidateC1ProductionAttackStartGate()
        {
            var target = new EntityRef(EntityKind.Unit, 1202);
            Require(WorldPointXZ.TryCreate(0d, 0d, out WorldPointXZ origin),
                "C1 production gate origin must be valid.");

            double outsideRadians = 5.001d * Math.PI / 180d;
            Require(UnitActionPoseSample.TryCreate(
                    target,
                    0d, 0d,
                    Math.Cos(outsideRadians), Math.Sin(outsideRadians),
                    1d, 0d,
                    false, 0d, 0d,
                    out UnitActionPoseSample outside),
                "C1 5.001-degree production pose must be valid.");
            Require(UnitAttackStartGate.Evaluate(
                    outside, true, true, false, default)
                    == UnitAttackStartGateResult.AwaitingStationarySample,
                "A first production pose must wait for a second sample before starting Legacy attack.");
            Require(UnitAttackStartGate.Evaluate(
                    outside, true, true, true, origin)
                    == UnitAttackStartGateResult.Misaligned,
                "A stationary 5.001-degree pose must not start Legacy attack.");

            double boundaryRadians = 5d * Math.PI / 180d;
            Require(UnitActionPoseSample.TryCreate(
                    target,
                    0d, 0d,
                    Math.Cos(boundaryRadians), Math.Sin(boundaryRadians),
                    1d, 0d,
                    false, 0d, 0d,
                    out UnitActionPoseSample boundary),
                "C1 exact 5-degree production pose must be valid.");
            Require(UnitAttackStartGate.Evaluate(
                    boundary, true, true, true, origin)
                    == UnitAttackStartGateResult.Ready,
                "A stationary exact 5-degree pose must start Legacy attack.");

            Require(UnitActionPoseSample.TryCreate(
                    target,
                    0.01d, 0d,
                    1d, 0d,
                    1d, 0d,
                    false, 0d, 0d,
                    out UnitActionPoseSample moving),
                "C1 moving production pose must be valid.");
            Require(UnitAttackStartGate.Evaluate(
                    moving, true, true, true, origin)
                    == UnitAttackStartGateResult.Moving,
                "A moving pose must not start Legacy attack even when facing is aligned.");
            Require(UnitAttackStartGate.Evaluate(
                    boundary, false, true, true, origin)
                    == UnitAttackStartGateResult.InvalidTarget
                && UnitAttackStartGate.Evaluate(
                    boundary, true, false, true, origin)
                    == UnitAttackStartGateResult.InvalidTarget,
                "A dead or invalid target must fail closed before Legacy attack starts.");
        }

        private static void ValidateB3CombatApproachAndRejoinPathfinding()
        {
            var grid = new HexGrid(5, 5, HexOrientation.FlatTop);
            var flowFields = new FlowFieldService();
            flowFields.Initialize(grid);
            try
            {
                var mapper = new HexMetricsCoordinateMapper();
                var movement = new UnitMovementUseCase(
                    grid,
                    unitSpawn: null,
                    flowFieldService: flowFields,
                    mapper: mapper);
                var start = new HexCoord(0, 2);
                var target = new HexCoord(4, 0);
                var unit = new UnitData(
                    -2,
                    UnitType.Pistoleer,
                    TeamId.Blue,
                    start,
                    maxHp: 1,
                    attackPower: 1,
                    attackRange: 1f,
                    detectRange: 1f);

                List<HexCoord> directBaseline = movement.RequestCombatApproachPath(
                    unit,
                    target);
                Require(directBaseline != null && directBaseline.Count >= 3,
                    "Combat approach fixture must begin with a multi-tile route.");
                HexCoord blockedMiddle = directBaseline[1];
                grid.GetTile(blockedMiddle).HasBuilding = true;
                flowFields.InvalidateAll();

                List<HexCoord> detour = movement.RequestCombatApproachPath(
                    unit,
                    target);
                Require(detour != null
                        && detour.Count >= 3
                        && detour[0] == start
                        && detour[detour.Count - 1] == target
                        && !detour.Contains(blockedMiddle),
                    "Combat approach must route around a newly blocked intermediate building tile.");

                Vector3 startWorld = mapper.HexToWorld(start);
                Vector3 unit64OffCenter = startWorld + new Vector3(0.12f, 0f, 0.08f);
                Require(movement.TryFindReachableForwardRejoinTile(
                            unit,
                            unit64OffCenter,
                            start,
                            out HexCoord arrivalRejoin)
                        && arrivalRejoin == start,
                    "Unit64-shaped post-combat recovery must treat the same walkable authoritative tile as a valid center rejoin even when A* is empty.");
                grid.GetTile(start).HasBuilding = true;
                Require(!movement.TryFindReachableForwardRejoinTile(
                            unit,
                            unit64OffCenter,
                            start,
                            out _),
                    "A blocked authoritative arrival tile must remain fail-closed instead of becoming a same-tile rejoin.");
                grid.GetTile(start).HasBuilding = false;

                Require(movement.TryFindReachableForwardRejoinTile(
                            unit,
                            startWorld,
                            target,
                            out HexCoord rejoin)
                        && HexCoord.Distance(start, rejoin) == 1
                        && HexCoord.Distance(rejoin, target)
                            < HexCoord.Distance(start, target)
                        && movement.IsWalkable(rejoin),
                    "Post-combat rejoin must choose a reachable walkable adjacent center with forward progress.");

                foreach (HexTile neighbor in grid.GetNeighbors(start))
                {
                    if (HexCoord.Distance(neighbor.Coord, target)
                        < HexCoord.Distance(start, target))
                    {
                        neighbor.HasBuilding = true;
                    }
                }
                flowFields.InvalidateAll();
                Require(!movement.TryFindReachableForwardRejoinTile(
                            unit,
                            startWorld,
                            target,
                            out _),
                    "Post-combat rejoin must fail closed instead of returning a blocked or unreachable nearest tile.");
            }
            finally
            {
                flowFields.Dispose();
            }
        }

        private static void ValidateB3BlockedAuthoritativeStartEgressPathfinding()
        {
            var grid = new HexGrid(5, 5, HexOrientation.FlatTop);
            foreach (HexTile tile in grid.Tiles.Values)
                tile.HasBuilding = true;

            var start = new HexCoord(1, 0);
            var longerCoordinateFirst = new HexCoord(0, 1);
            var coordinateFirst = new HexCoord(1, 1);
            var coordinateSecond = new HexCoord(2, 0);
            var destination = new HexCoord(2, 1);
            grid.GetTile(longerCoordinateFirst).HasBuilding = false;
            grid.GetTile(coordinateFirst).HasBuilding = false;
            grid.GetTile(coordinateSecond).HasBuilding = false;
            grid.GetTile(destination).HasBuilding = false;

            var flowFields = new FlowFieldService();
            flowFields.Initialize(grid);
            try
            {
                var movement = new UnitMovementUseCase(
                    grid,
                    unitSpawn: null,
                    flowFieldService: flowFields,
                    mapper: null);

                Require(movement.RequestMoveFrom(start, destination) == null,
                    "A non-authoritative staged blocked start must remain fail-closed.");

                var authoritativeUnit = new UnitData(
                    -1,
                    UnitType.Pistoleer,
                    TeamId.Blue,
                    start,
                    maxHp: 1,
                    attackPower: 1,
                    attackRange: 1f,
                    detectRange: 1f);
                List<HexCoord> egress = movement.RequestMove(
                    authoritativeUnit, destination);
                Require(egress != null
                        && egress.Count == 3
                        && egress[0] == start
                        && egress[1] == coordinateFirst
                        && egress[2] == destination,
                    "A blocked authoritative start must egress through the deterministic shortest coordinate-first route.");
                Require(HexCoord.Distance(egress[0], egress[1]) == 1,
                    "Blocked-start egress must preserve an adjacent first spatial edge.");
                for (int index = 1; index < egress.Count - 1; index++)
                {
                    Require(grid.GetTile(egress[index]) != null
                            && grid.GetTile(egress[index]).IsWalkable,
                        "Blocked-start egress must exclude blocked intermediate tiles.");
                }

                List<HexCoord> repeated = movement.RequestMove(
                    authoritativeUnit, destination);
                Require(PathsEqual(egress, repeated),
                    "Equal-length blocked-start egress routes must choose the smaller Q deterministically.");

                Require(movement.RequestMove(authoritativeUnit, start) == null,
                    "A blocked authoritative start already at its destination must remain no movement.");

                grid.GetTile(coordinateFirst).HasBuilding = true;
                grid.GetTile(coordinateSecond).HasBuilding = true;
                grid.GetTile(longerCoordinateFirst).HasBuilding = true;
                flowFields.InvalidateAll();
                Require(movement.RequestMove(authoritativeUnit, destination) == null,
                    "A blocked authoritative start without a reachable walkable neighbor must fail closed.");

                grid.GetTile(start).HasBuilding = false;
                grid.GetTile(longerCoordinateFirst).HasBuilding = false;
                grid.GetTile(coordinateFirst).HasBuilding = false;
                grid.GetTile(coordinateSecond).HasBuilding = false;
                flowFields.InvalidateAll();
                List<HexCoord> expectedNormal = flowFields
                    .GetOrCompute(destination)
                    .GetPath(start);
                List<HexCoord> actualNormal = movement.RequestMoveFrom(
                    start, destination);
                Require(PathsEqual(expectedNormal, actualNormal),
                    "A normal walkable start must retain the existing flow-field path unchanged.");
                Require(movement.RequestMoveFrom(start, start) == null,
                    "An already-at-destination request must retain the existing no-movement result.");

                foreach (HexTile tile in grid.Tiles.Values)
                    tile.HasBuilding = true;
                var equalQStart = new HexCoord(1, 1);
                var smallerR = new HexCoord(2, 0);
                var largerR = new HexCoord(2, 1);
                var equalQDestination = new HexCoord(3, 0);
                grid.GetTile(smallerR).HasBuilding = false;
                grid.GetTile(largerR).HasBuilding = false;
                grid.GetTile(equalQDestination).HasBuilding = false;
                authoritativeUnit.Position = equalQStart;
                flowFields.InvalidateAll();

                List<HexCoord> equalQEgress = movement.RequestMove(
                    authoritativeUnit, equalQDestination);
                Require(equalQEgress != null
                        && equalQEgress.Count == 3
                        && equalQEgress[0] == equalQStart
                        && equalQEgress[1] == smallerR
                        && equalQEgress[2] == equalQDestination,
                    "Equal-length blocked-start egress routes with equal Q must choose the smaller R deterministically.");

                // 건물 배치 이벤트에서 eager repath가 cache 구독보다 먼저 실행되는 순서를
                // 재현한다. 호출자가 관측한 newer revision이 오래된 field를 강제 폐기해야
                // 막힌 직선 대신 실제로 존재하는 우회 경로가 반환된다.
                foreach (HexTile tile in grid.Tiles.Values)
                    tile.HasBuilding = false;
                flowFields.InvalidateAll();
                var mapper = new HexMetricsCoordinateMapper();
                var revisionMovement = new UnitMovementUseCase(
                    grid,
                    unitSpawn: null,
                    flowFieldService: flowFields,
                    mapper: mapper);
                var detourStart = new HexCoord(0, 0);
                var detourGoal = new HexCoord(3, 1);
                authoritativeUnit.Position = detourStart;
                List<HexCoord> staleStraightPath = flowFields
                    .GetOrCompute(detourGoal)
                    .GetPath(detourStart);
                Require(staleStraightPath != null && staleStraightPath.Count >= 3,
                    "B3 latest-environment detour fixture must first cache a valid direct route.");
                HexCoord newlyBlocked = staleStraightPath[1];
                grid.GetTile(newlyBlocked).HasBuilding = true;
                ulong observedNewRevision = flowFields.WalkabilityRevision + 1UL;
                UnitPathRequestResult detour = revisionMovement.RequestAuthoritativeRepath(
                    authoritativeUnit,
                    mapper.HexToWorld(detourStart),
                    detourGoal,
                    observedNewRevision);
                Require(detour.IsSuccess
                        && detour.Status == UnitPathRequestStatus.Success
                        && detour.Path[0] == detourStart
                        && !PathContains(detour.Path, newlyBlocked)
                        && detour.EnvironmentRevision >= observedNewRevision,
                    "A reachable detour must be recomputed from authoritative start/Simulation Root at the latest walkability revision instead of returning InvalidPath.");

                UnitPathRequestResult sameTile = revisionMovement.RequestAuthoritativeRepath(
                    authoritativeUnit,
                    mapper.HexToWorld(detourStart),
                    detourStart,
                    observedNewRevision);
                UnitPathRequestResult invalidGoal = revisionMovement.RequestAuthoritativeRepath(
                    authoritativeUnit,
                    mapper.HexToWorld(detourStart),
                    new HexCoord(99, 99),
                    observedNewRevision);
                UnitPathRequestResult invalidStart = revisionMovement.RequestAuthoritativeRepath(
                    authoritativeUnit,
                    mapper.HexToWorld(new HexCoord(4, 4)),
                    detourGoal,
                    observedNewRevision);
                Require(sameTile.Status == UnitPathRequestStatus.EmptySameTile
                        && invalidGoal.Status == UnitPathRequestStatus.InvalidGoal
                        && invalidStart.Status == UnitPathRequestStatus.InvalidStart,
                    "B3 path request must preserve typed same-tile, invalid-goal, and authoritative-Root invalid-start evidence.");

                foreach (HexTile tile in grid.Tiles.Values)
                    tile.HasBuilding = true;
                grid.GetTile(detourStart).HasBuilding = false;
                grid.GetTile(detourGoal).HasBuilding = false;
                UnitPathRequestResult unreachable = revisionMovement.RequestAuthoritativeRepath(
                    authoritativeUnit,
                    mapper.HexToWorld(detourStart),
                    detourGoal,
                    observedNewRevision + 1UL);
                Require(unreachable.Status == UnitPathRequestStatus.Unreachable
                        && !unreachable.IsSuccess,
                    "A genuinely unreachable objective must remain Blocked instead of being forged as a successful detour.");
            }
            finally
            {
                flowFields.Dispose();
            }
        }

        private static bool PathsEqual(
            IReadOnlyList<HexCoord> left,
            IReadOnlyList<HexCoord> right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Count != right.Count)
                return false;

            for (int index = 0; index < left.Count; index++)
            {
                if (left[index] != right[index])
                    return false;
            }
            return true;
        }

        private static bool PathContains(
            IReadOnlyList<HexCoord> path,
            HexCoord coord)
        {
            if (path == null) return false;
            for (int index = 0; index < path.Count; index++)
            {
                if (path[index] == coord) return true;
            }
            return false;
        }

        private static void ValidateB3SpatialTransitionPolicy()
        {
            var start = new HexCoord(0, 0);
            var first = new HexCoord(1, 0);
            var second = new HexCoord(2, 0);
            var terminal = new HexCoord(3, 0);
            var route = new[] { start, first, second, terminal };
            var output = new HexCoord[4];

            UnitSpatialTransitionPlanStatus status = UnitSpatialTransitionPolicy.TryBuild(
                start,
                new[] { start, start, first, first, second },
                5,
                route,
                false,
                default,
                output,
                out int count);
            Require(status == UnitSpatialTransitionPlanStatus.Ready
                    && count == 2
                    && output[0] == first
                    && output[1] == second,
                "Duplicate Root samples must compact into ordered adjacent transitions.");

            var checkpoint = new UnitPathCheckpointTracker();
            Require(checkpoint.TryConsume(1, true),
                "The route checkpoint fixture must consume its first waypoint.");
            status = UnitSpatialTransitionPolicy.TryBuild(
                start,
                new[] { start, start },
                2,
                route,
                false,
                default,
                output,
                out count);
            Require(status == UnitSpatialTransitionPlanStatus.NoTransition && count == 0,
                "A consumed route checkpoint must not create a spatial transition before Root crosses a boundary.");

            status = UnitSpatialTransitionPolicy.TryBuild(
                start,
                new[] { start, first, terminal },
                3,
                route,
                false,
                default,
                output,
                out count);
            Require(status == UnitSpatialTransitionPlanStatus.NonAdjacentSample && count == 0,
                "A skipped sampled tile must fail closed without partial output.");

            status = UnitSpatialTransitionPolicy.TryBuild(
                start,
                new[] { start, first, second, terminal },
                4,
                route,
                true,
                terminal,
                output,
                out count);
            Require(status == UnitSpatialTransitionPlanStatus.TerminalContact
                    && count == 2
                    && output[0] == first
                    && output[1] == second,
                "A non-walkable logical final tile must remain terminal contact, not an occupied tile.");

            status = UnitSpatialTransitionPolicy.TryBuild(
                first,
                new[] { first, start },
                2,
                route,
                false,
                default,
                output,
                out count);
            Require(status == UnitSpatialTransitionPlanStatus.OutsideLogicalPath && count == 0,
                "A backward A-star spatial transition must fail closed.");

            status = UnitSpatialTransitionPolicy.TryBuild(
                start,
                new[] { start, first, second },
                3,
                route,
                false,
                default,
                new HexCoord[1],
                out count);
            Require(status == UnitSpatialTransitionPlanStatus.InsufficientCapacity && count == 0,
                "An undersized caller buffer must fail closed without partial output.");
        }

        private static void ValidateB3PreparedMovementTransition()
        {
            Require(ActionDirectionXZ.TryCreate(
                    0d, 1d, out ActionDirectionXZ forward),
                "Prepared movement fixture direction must be valid.");
            var reducer = new UnitMovementReducer();
            UnitMovementSnapshot before = reducer.Snapshot;

            UnitMovementReducerStatus preparedStatus = reducer.Prepare(
                before.Revision,
                1UL,
                1UL,
                UnitMovementIntentReason.AStarPath,
                true,
                forward,
                forward,
                false,
                1d,
                out UnitMovementPreparedTransition prepared);
            Require(preparedStatus == UnitMovementReducerStatus.Accepted
                    && prepared.IsPrepared
                    && reducer.Snapshot.Revision == before.Revision
                    && !reducer.Snapshot.HasAcceptedObservation,
                "Prepare must produce an accepted transition without mutating the reducer.");

            UnitMovementReducerStatus failedStatus = reducer.Prepare(
                before.Revision,
                1UL,
                1UL,
                UnitMovementIntentReason.AStarPath,
                true,
                forward,
                forward,
                false,
                double.NaN,
                out UnitMovementPreparedTransition failed);
            Require(failedStatus == UnitMovementReducerStatus.InvalidTime
                    && !failed.IsPrepared
                    && reducer.Snapshot.Revision == before.Revision
                    && !reducer.Snapshot.HasAcceptedObservation,
                "Failed and abandoned prepares must leave the reducer unchanged.");

            Require(reducer.CanCommitPrepared(prepared)
                    && reducer.TryCommitPrepared(prepared)
                    && reducer.Snapshot.Revision == before.Revision + 1UL
                    && reducer.Snapshot.HasAcceptedObservation
                    && !reducer.TryCommitPrepared(prepared),
                "A prepared transition must commit exactly once.");

            UnitMovementSnapshot beforeDuplicate = reducer.Snapshot;
            UnitMovementReducerStatus duplicateStatus = reducer.Prepare(
                beforeDuplicate.Revision,
                1UL,
                1UL,
                UnitMovementIntentReason.AStarPath,
                true,
                forward,
                forward,
                false,
                1d,
                out UnitMovementPreparedTransition duplicate);
            Require(duplicateStatus == UnitMovementReducerStatus.Duplicate
                    && reducer.CanCommitPrepared(duplicate)
                    && reducer.TryCommitPrepared(duplicate)
                    && reducer.Snapshot.Revision == beforeDuplicate.Revision,
                "A duplicate prepared transition must commit as a no-op.");
        }

        private static void ValidateB3SpatialTransitionApplicationPreflight()
        {
            var start = new HexCoord(0, 0);
            var first = new HexCoord(1, 0);
            var second = new HexCoord(2, 0);
            var adjacent = new[] { first, second };

            Require(UnitMovementUseCase.IsValidSpatialTransitionChain(
                    start, adjacent, adjacent.Length),
                "Application spatial preflight must accept an ordered adjacent chain.");
            Require(UnitMovementUseCase.IsValidSpatialTransitionChain(
                    start, adjacent, 0),
                "Application spatial preflight must accept a zero-transition no-op.");
            Require(!UnitMovementUseCase.IsValidSpatialTransitionChain(
                    start, new[] { second }, 1),
                "Application spatial preflight must reject a chain that skips the first boundary.");
            Require(!UnitMovementUseCase.IsValidSpatialTransitionChain(
                    start, adjacent, adjacent.Length + 1),
                "Application spatial preflight must reject an out-of-bounds caller count.");

            UnitSpatialPreflightResult missing =
                UnitMovementUseCase.ClassifySpatialTile(
                    tileExists: false,
                    tileWalkable: false,
                    second,
                    transitionIndex: 1);
            UnitSpatialPreflightResult nonWalkable =
                UnitMovementUseCase.ClassifySpatialTile(
                    tileExists: true,
                    tileWalkable: false,
                    first,
                    transitionIndex: 0);
            var invalidChain = new UnitSpatialPreflightResult(
                UnitSpatialPreflightStatus.InvalidChain, second, 0);
            Require(missing.IsRecoverable
                    && missing.OffendingTile == second
                    && missing.OffendingTransitionIndex == 1
                    && nonWalkable.IsRecoverable
                    && nonWalkable.OffendingTile == first
                    && invalidChain.IsFatal,
                "Missing/non-walkable tiles must retain typed recoverable evidence while invalid chains remain fatal.");
            Require(UnitSpatialCandidatePolicy.Classify(
                        UnitSpatialTransitionPlanStatus.OutsideLogicalPath,
                        UnitSpatialPreflightResult.Success())
                    == UnitSpatialCandidateVerdict.CandidateUnsafe
                    && UnitSpatialCandidatePolicy.Classify(
                        UnitSpatialTransitionPlanStatus.Ready,
                        invalidChain)
                    == UnitSpatialCandidateVerdict.Fatal,
                "OutsideLogicalPath must enter resolver fallback/repath while fatal Application contracts reject.");

            int heldPlanned = UnitSpatialDiagnosticAccountingPolicy
                .PlannedDeltaAtCandidateStage(transitionCount: 2);
            int successfulPlanned = UnitSpatialDiagnosticAccountingPolicy
                .PlannedDeltaAtCommitAttempt(transitionCount: 2);
            int successfulCommitted = UnitSpatialDiagnosticAccountingPolicy
                .CommittedDeltaAtCommitResult(transitionCount: 2, succeeded: true);
            int failedPlanned = UnitSpatialDiagnosticAccountingPolicy
                .PlannedDeltaAtCommitAttempt(transitionCount: 1);
            int failedCommitted = UnitSpatialDiagnosticAccountingPolicy
                .CommittedDeltaAtCommitResult(transitionCount: 1, succeeded: false);
            Require(heldPlanned == 0
                    && successfulPlanned == 2
                    && successfulCommitted == 2
                    && failedPlanned == 1
                    && failedCommitted == 0
                    && UnitSpatialDiagnosticAccountingPolicy.CommitFailureDelta(false) == 1,
                "Accepted staged-but-Held candidates must not count as planned; only actual commit attempts may create planned/committed divergence.");
            Require(!UnitSpatialDiagnosticAccountingPolicy
                        .ClearsRecoverableHistory(transitionCount: 0, succeeded: true)
                    && !UnitSpatialDiagnosticAccountingPolicy
                        .ClearsRecoverableHistory(transitionCount: 2, succeeded: false)
                    && UnitSpatialDiagnosticAccountingPolicy
                        .ClearsRecoverableHistory(transitionCount: 2, succeeded: true),
                "No-transition Accepted stages between repeated failures must retain recoverable history until a real spatial commit succeeds.");
        }

        private static void ValidateB3PathStartArrivalCommit()
        {
            var logicalStart = new HexCoord(0, 0);
            var stagedStart = new HexCoord(1, 0);
            var finalTarget = new HexCoord(2, 0);
            var route = new List<HexCoord> { stagedStart, finalTarget };

            Require(UnitPathStartTransitionPolicy.TryBuildArrivalCommittedPath(
                        logicalStart,
                        stagedStart,
                        route,
                        out List<HexCoord> path),
                "An adjacent forward start must produce an arrival-committed transition path.");
            Require(path.Count == 3
                    && path[0] == logicalStart
                    && path[1] == stagedStart
                    && path[2] == finalTarget,
                "The logical start must remain path[0] until the staged tile is actually reached.");
            Require(!UnitPathStartTransitionPolicy.TryBuildArrivalCommittedPath(
                        logicalStart,
                        finalTarget,
                        new List<HexCoord> { finalTarget },
                        out _),
                "A non-adjacent staged start must fail closed instead of creating a logical jump.");
            Require(!UnitPathStartTransitionPolicy.TryBuildArrivalCommittedPath(
                        logicalStart,
                        stagedStart,
                        new List<HexCoord> { finalTarget },
                        out _),
                "A route that does not begin at the staged start must fail closed.");
        }

        private static void ValidateB3CorridorSamplePolicy()
        {
            Require(UnitTrajectoryCorridorSamplePolicy.Evaluate(
                        pathCount: 4,
                        waypointIndex: 1,
                        matchedPathIndex: 0,
                        isWalkable: false)
                    == UnitTrajectoryCorridorSampleResult.StartTileEgress,
                "A non-walkable path[0] must be a legal origin only while leaving the first segment.");
            Require(UnitTrajectoryCorridorSamplePolicy.Evaluate(
                        pathCount: 4,
                        waypointIndex: 1,
                        matchedPathIndex: 1,
                        isWalkable: false)
                    == UnitTrajectoryCorridorSampleResult.Blocked,
                "A non-walkable first waypoint must remain blocked.");
            Require(UnitTrajectoryCorridorSamplePolicy.Evaluate(
                        pathCount: 4,
                        waypointIndex: 2,
                        matchedPathIndex: 0,
                        isWalkable: false)
                    == UnitTrajectoryCorridorSampleResult.OutsideCorridor,
                "The start-tile exception must not permit re-entry after the first segment.");
            Require(UnitTrajectoryCorridorSamplePolicy.Evaluate(
                        pathCount: 4,
                        waypointIndex: 1,
                        matchedPathIndex: -1,
                        isWalkable: true)
                    == UnitTrajectoryCorridorSampleResult.OutsideCorridor,
                "A walkable tile outside the logical corridor must remain rejected.");
            Require(UnitTrajectoryCorridorSamplePolicy.Evaluate(
                        pathCount: 4,
                        waypointIndex: 2,
                        matchedPathIndex: 2,
                        isWalkable: true)
                    == UnitTrajectoryCorridorSampleResult.Walkable,
                "A normal walkable corridor sample must remain accepted.");
            Require(UnitTrajectoryCorridorSamplePolicy.Evaluate(
                        pathCount: 4,
                        waypointIndex: 3,
                        matchedPathIndex: 3,
                        isWalkable: false)
                    == UnitTrajectoryCorridorSampleResult.FinalDestination,
                "The existing non-walkable final destination contract must remain accepted.");
        }

        private static void ValidateB3CorridorTerminalEvidence()
        {
            const BindingFlags flags = BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Static
                | BindingFlags.Instance;
            System.Type viewType = typeof(Hexiege.Presentation.UnitView);
            System.Type evidenceType = viewType.GetNestedType(
                "CorridorRepathEvidence", BindingFlags.NonPublic);
            MethodInfo formatter = viewType.GetMethod(
                "FormatCorridorRepathEvidence",
                BindingFlags.NonPublic | BindingFlags.Static);
            Require(evidenceType != null && formatter != null,
                "The bounded corridor terminal evidence formatter must remain connected to UnitView.");

            object emptyEvidence = System.Activator.CreateInstance(evidenceType);
            string empty = formatter.Invoke(null, new[] { emptyEvidence }) as string;
            Require(empty == "unavailable",
                "Missing corridor terminal evidence must fail closed as unavailable.");

            object evidence = System.Activator.CreateInstance(evidenceType);
            evidenceType.GetField("IsAvailable", flags)?.SetValue(evidence, true);
            evidenceType.GetField("Frame", flags)?.SetValue(evidence, 17);
            evidenceType.GetField("PathCount", flags)?.SetValue(evidence, 3);
            evidenceType.GetField("WaypointIndex", flags)?.SetValue(evidence, 1);
            evidenceType.GetField("MatchedPathIndex", flags)?.SetValue(evidence, -1);
            evidenceType.GetField("FirstAllowedPathIndex", flags)?.SetValue(evidence, 0);
            evidenceType.GetField("LastAllowedPathIndex", flags)?.SetValue(evidence, 2);
            evidenceType.GetField("SampleResult", flags)?.SetValue(
                evidence, UnitTrajectoryCorridorSampleResult.OutsideCorridor);
            string formatted = formatter.Invoke(null, new[] { evidence }) as string;
            Require(!string.IsNullOrEmpty(formatted)
                    && formatted.Contains("frame:17|")
                    && formatted.Contains("rootView:")
                    && formatted.Contains("rootDomain:")
                    && formatted.Contains("rootHex:")
                    && formatted.Contains("unitDataPosition:")
                    && formatted.Contains("sourcePathStart:")
                    && formatted.Contains("sourceWaypointIndex:1/2|")
                    && formatted.Contains("sampleHex:")
                    && formatted.Contains("matchedPathIndex:-1|")
                    && formatted.Contains("allowedPathIndices:0-2|")
                    && formatted.EndsWith("sampleResult:OutsideCorridor"),
                "Corridor terminal evidence must remain a lossless single-line Root/Domain/UnitData/path/sample record.");
        }

        private sealed class SoloTimingMapper : IHexCoordinateMapper
        {
            public float TileHeight => .866f;
            public Vector3 HexToWorld(HexCoord h) => new Vector3(h.Q, 0, h.R);
            public HexCoord WorldToHex(Vector3 p) => new HexCoord((int)p.x, (int)p.z);
            public Vector3 NormalizeToDomainPosition(Vector3 p) => p;
        }

        public static void ValidateSoloTimingAuthority()
        {
            Require(!UnityEngine.Application.isPlaying, "SOLO timing fixture must run outside a live match.");
            ValidateSoloTimingAuthorityCore();
        }

        // CLR runner는 native isPlaying 호출 없이 이 경계를 사용한다. Editor 메뉴는
        // 위 guard를 반드시 거치며, 모든 변경한 전역 상태/구독은 finally에서 복원한다.
        internal static void ValidateSoloTimingAuthorityCore()
        {
            var dataField = typeof(UnitStats).GetField("_data", BindingFlags.Static | BindingFlags.NonPublic);
            var oldStats = (IReadOnlyDictionary<UnitType, UnitStats.StatValues>)dataField.GetValue(null);
            bool oldActive = NetworkContext.IsNetworkActive, oldServer = NetworkContext.IsNetworkServer;
            var idField = typeof(UnitData).GetField("_nextId", BindingFlags.Static | BindingFlags.NonPublic);
            object oldId = idField.GetValue(null);
            UnitSpawnUseCase spawn = null; UnitCombatUseCase combat = null;
            UnitData attacker = null, victim = null;
            double now = 0; int starts = 0, changes = 0;
            var hits = new List<double>();
            IDisposable start = null, changed = null, damage = null;
            try
            {
                NetworkContext.Set(false, false);
                UnitStats.Initialize(new Dictionary<UnitType, UnitStats.StatValues> {
                    { UnitType.SpearMan, new UnitStats.StatValues { MaxHp = 100000, AttackPower = 1,
                        AttackRange = 5, DetectRange = 5, MoveSpeed = 1, AttackCooldown = 2,
                        HitFrameTimes = new[] { .5f, 1.5f } } }
                });
                Action<float[]> reset = offsets => {
                    var grid = new HexGrid(3, 3, HexOrientation.FlatTop);
                    spawn = new UnitSpawnUseCase(grid);
                    attacker = spawn.SpawnUnitWithId(9900, UnitType.SpearMan, TeamId.Blue, new HexCoord(0, 0));
                    victim = spawn.SpawnUnitWithId(9901, UnitType.SpearMan, TeamId.Red, new HexCoord(1, 0));
                    attacker.HitFrameTimes = offsets;
                    combat = new UnitCombatUseCase(grid, spawn, new BuildingPlacementUseCase(grid), null, new SoloTimingMapper());
                    now = 0; starts = changes = 0; hits.Clear();
                };
                // 실제 production Start callback -> 재생 epoch 확정 API 경계다.
                // Animator 자체를 만들거나 native 실행 성공을 가정하지 않는다.
                start = GameEvents.OnCombatStarted.Subscribe(e => {
                    if (attacker != null && e.UnitId == attacker.Id) {
                        starts++; combat.ConfirmSoloAttackPlayback(attacker, now);
                    }
                });
                changed = GameEvents.OnCombatTargetChanged.Subscribe(e => { if (attacker != null && e.UnitId == attacker.Id) changes++; });
                damage = GameEvents.OnEntityDamaged.Subscribe(e => hits.Add(now));
                Action<double> pump = at => {
                    float dt = (float)(at - now); now = at;
                    combat.TickCooldowns(dt, now); combat.TickPendingHits(dt, now);
                };

                foreach (int fps in new[] { 30, 60, 45 })
                {
                    reset(new[] { .8333333f });
                    Require(combat.TryAttack(attacker, now).HasValue, "SOLO first production attack must commit.");
                    for (int frame = 1; frame <= fps * 120; frame++) { pump((double)frame / fps); combat.TryAttack(attacker, now); }
                    Require(starts == 1 && changes == 0 && hits.Count == 100000 - victim.Hp,
                        "SOLO repeated native-start callback count and direct HP writer count must agree.");
                    for (int i = 0; i < hits.Count; i++)
                        Require(hits[i] >= i * 2d + .8333333f && hits[i] - (i * 2d + .8333333f) <= 1d / fps + 1e-6,
                            "SOLO repeated occurrences may be one frame late, never cumulatively late or early.");
                }

                reset(new[] { .5f, 1.5f }); combat.TryAttack(attacker, now); pump(.5);
                UnitData oldTarget = victim; oldTarget.Position = new HexCoord(100, 100);
                victim = spawn.SpawnUnitWithId(9902, UnitType.SpearMan, TeamId.Red, new HexCoord(1, 0));
                pump(2.25); Require(combat.TryAttack(attacker, now).HasValue, "SOLO retarget commits a future occurrence.");
                pump(2.49); Require(victim.Hp == 100000, "Retarget must not receive retroactive/old target damage.");
                pump(2.5); Require(victim.Hp == 99999 && oldTarget.Hp == 99998 && starts == 1 && changes == 1,
                    "SOLO retarget keeps playback anchor 0, old reservations and next occurrence 2.5; no fresh playback epoch.");

                reset(new[] { .5f, .5f, 1.5f }); combat.TryAttack(attacker, now); pump(.5);
                Require(victim.Hp == 99998, "SOLO positive duplicate markers retain two distinct HitIndex writes.");
                pump(1.5); Require(victim.Hp == 99997, "SOLO final multihit remains exactly one write.");

                reset(new[] { .5f, 1.5f }); combat.TryAttack(attacker, now); pump(10.75);
                Require(victim.Hp == 99998 && combat.TryAttack(attacker, now).HasValue
                    && !combat.TryAttack(attacker, now).HasValue, "SOLO long frame drains finite old reservations and commits at most once.");
                pump(11.49); Require(victim.Hp == 99998, "SOLO missed uncommitted marker is not backfilled.");
                pump(11.5); Require(victim.Hp == 99997, "SOLO long frame chooses future original HitIndex occurrence.");

                foreach (StatusEffectKind kind in new[] { StatusEffectKind.Freeze, StatusEffectKind.AttackDisabled })
                {
                    reset(new[] { .5f, 1.5f }); combat.TryAttack(attacker, now);
                    var status = new StatusEffectSystem(); combat.SetStatusEffectSystem(status);
                    status.Apply(attacker.Id, new StatusEffect(kind, 1, 10, TeamId.Red));
                    combat.ClearCombatState(attacker.Id); pump(1.5);
                    Require(victim.Hp == 99998 && !combat.TryAttack(attacker, now).HasValue,
                        "SOLO Freeze/CC/Stop do not cancel or pause committed HP; CC blocks only new commits.");
                }

                reset(new[] { .5f, 1.5f }); combat.TryAttack(attacker, now);
                combat.ObserveSoloAttackPause(attacker, .2, true); pump(2);
                Require(victim.Hp == 99998 && !combat.TryAttack(attacker, now).HasValue,
                    "SOLO authority-commanded playback pause keeps committed DueAt unchanged.");
                combat.ObserveSoloAttackPause(attacker, 3.2, false); pump(5);
                Require(combat.TryAttack(attacker, now).HasValue, "SOLO resume permits the next future occurrence.");
                pump(5.5); Require(victim.Hp == 99997, "SOLO future phase shifts by pause duration without retroactive HP.");

                reset(new[] { .5f }); combat.TryAttack(attacker, now);
                combat.CloseSoloAttackPlayback(attacker, terminal: true); pump(.5);
                Require(victim.Hp == 99999, "SOLO View-disable/rebind-equivalent cannot retire a current alive Domain HP owner.");

                reset(new[] { .5f }); combat.TryAttack(attacker, now); attacker.TakeDamage(100000); pump(.6);
                Require(victim.Hp == 100000, "SOLO Domain death discards unexecuted hits.");

                reset(new[] { .5f }); combat.TryAttack(attacker, now); spawn.RemoveUnit(attacker.Id);
                attacker = spawn.SpawnUnitWithId(9900, UnitType.SpearMan, TeamId.Blue, new HexCoord(0, 0));
                pump(.6); Require(victim.Hp == 100000, "SOLO reused attacker ID cannot inherit old owner's hit.");
                Require(combat.TryAttack(attacker, now).HasValue && starts == 2, "SOLO replacement UnitData opens an actual new Start callback.");
                pump(1.1); Require(victim.Hp == 99999, "SOLO new lifetime hits normally; retiring old owner cannot remove current owner.");

                reset(new[] { .5f }); combat.TryAttack(attacker, now); spawn.RemoveUnit(victim.Id);
                victim = spawn.SpawnUnitWithId(9901, UnitType.SpearMan, TeamId.Red, new HexCoord(1, 0));
                pump(.6); Require(victim.Hp == 100000, "SOLO reused target ID never receives previous captured target's reservation.");

                reset(new[] { 0f }); combat.TryAttack(attacker, now);
                double last = 0; int commits = 1;
                for (int frame = 1; frame <= 1800; frame++)
                {
                    pump(frame / 30d);
                    if (combat.TryAttack(attacker, now).HasValue) {
                        Require(now - last >= 2 && now - last < 2.1, "SOLO zero-marker fallback preserves existing cadence, never halves it.");
                        last = now; commits++;
                    }
                }
                Require(!SoloAttackPlaybackClock.Supports(2, new[] { 0f }) && commits >= 29
                    && hits.Count == 100000 - victim.Hp, "Zero marker is explicit preserved Legacy fallback, not corrected occurrence PASS.");
                reset(new[] { .5f });
                Require(!combat.TryAttack(attacker, double.NaN).HasValue
                    && !combat.TryAttack(attacker, double.PositiveInfinity).HasValue
                    && !combat.TryAttack(attacker, -1).HasValue && victim.Hp == 100000,
                    "SOLO invalid authority clock cannot commit or invent HP.");
                var clock = new SoloAttackPlaybackClock();
                Require(!clock.Restart(-1, 2) && !clock.Restart(double.NaN, 2)
                    && !clock.Restart(0, double.PositiveInfinity) && clock.Restart(0, 2)
                    && clock.TryCommit(0, new[] { .5f }, out _)
                    && !clock.TryCommit(0, new[] { .5f }, out _)
                    && clock.TryCommit(2, new[] { .5f }, out _)
                    && !clock.TryCommit(1, new[] { .5f }, out _),
                    "SOLO clock rejects invalid/backward/same-frame commits without new occurrence.");
                Require(!SoloAttackPlaybackClock.Supports(2, new[] { -.1f })
                    && !SoloAttackPlaybackClock.Supports(2, new[] { float.NaN })
                    && !SoloAttackPlaybackClock.Supports(2, new[] { 1.5f, .5f }),
                    "Invalid/unsorted markers are explicit unsupported Legacy fallback, not corrected coverage.");
            }
            finally
            {
                start?.Dispose(); changed?.Dispose(); damage?.Dispose();
                UnitStats.Initialize(oldStats); idField.SetValue(null, oldId);
                NetworkContext.Set(oldServer, oldActive);
            }
        }

        /// <summary>
        /// 실제 SOLO View writer를 통해 A* 중간 경계→추격→복귀 경로의 공간 커밋을 검사한다.
        /// GameObject/Transform 실행이 필요하므로 CLR 정책 검사와 native 실행을 구분한다.
        /// </summary>
        public static void ValidateSoloSpatialRecoveryBoundary()
        {
            bool oldActive = NetworkContext.IsNetworkActive;
            bool oldServer = NetworkContext.IsNetworkServer;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var idField = typeof(UnitData).GetField("_nextId", BindingFlags.Static | BindingFlags.NonPublic);
            object oldId = idField.GetValue(null);
            var grid = new HexGrid(16, 16, HexOrientation.FlatTop);
            var flow = new FlowFieldService(); flow.Initialize(grid);
            var spawn = new UnitSpawnUseCase(grid);
            var mapper = new HexMetricsCoordinateMapper();
            var movement = new UnitMovementUseCase(grid, spawn, flow, mapper);
            GameObject go = null;
            IDisposable moved = null, entered = null;
            try
            {
                NetworkContext.Set(false, false);
                var route = new List<HexCoord> { new HexCoord(0, 2), new HexCoord(1, 2),
                    new HexCoord(2, 2), new HexCoord(3, 2), new HexCoord(4, 2) };
                UnitData unit = spawn.SpawnUnit(UnitType.RabbitTrickster, TeamId.Blue, route[0]);
                Require(unit != null, "SOLO spatial fixture must create a registered living unit.");
                go = new GameObject("SoloSpatialRecoveryFixture");
                var view = go.AddComponent<Hexiege.Presentation.UnitView>();
                Type type = view.GetType();
                type.GetField("_unitData", flags).SetValue(view, unit);
                type.GetField("_movementUseCase", flags).SetValue(view, movement);
                type.GetField("_isAStarMoving", flags).SetValue(view, true);
                MethodInfo writer = type.GetMethod("ApplyMovementAuthorityFrame", flags);
                MethodInfo resume = type.GetMethod("ResumeFromForwardTileV3", flags);
                int moves = 0, enters = 0;
                moved = GameEvents.OnUnitMoved.Subscribe(e => { if (e.UnitId == unit.Id) moves++; });
                entered = GameEvents.OnUnitEnteredTile.Subscribe(e => { if (e.UnitId == unit.Id) enters++; });
                go.transform.position = ViewConverter.ToView(mapper.HexToWorld(route[0]));
                for (int i = 1; i <= 3; i++)
                {
                    type.GetField("_isAStarMoving", flags).SetValue(view, i == 1);
                    // 첫 edge는 A* 중간 경계에서 추격으로 전환한다. 중심 도착 갱신으로
                    // 우연히 정합되는 테스트를 피하려고 먼저 75% 지점에서 커밋을 확인한다.
                    Vector3 center = ViewConverter.ToView(mapper.HexToWorld(route[i]));
                    Vector3 candidate = i == 1 ? Vector3.Lerp(go.transform.position, center, .75f) : center;
                    object result = writer.Invoke(view, new object[] { center, candidate, Quaternion.identity,
                        i == 1 ? UnitMovementIntentReason.AStarPath : UnitMovementIntentReason.Chase,
                        false, null, 100f, route, i, null, null });
                    Require(result.ToString() == "Advanced" && unit.Position == route[i]
                        && spawn.GetUnitAt(route[i]) == unit && spawn.GetUnitAt(route[i - 1]) == null,
                        "SOLO actual writer must commit each crossed adjacent boundary and reverse index before checkpoint arrival.");
                }
                Require(moves == 3 && enters == 1,
                    "SOLO spatial move events occur on all edges; congestion entry events occur only while A* navigating.");
                Require(movement.RequestAuthoritativeRepath(unit,
                    ViewConverter.FromView(go.transform.position), route[4], 0UL).IsSuccess,
                    "SOLO chase over multiple tiles must leave an accepted authoritative repath start.");
                type.GetField("_isAStarMoving", flags).SetValue(view, true);
                object resumed = resume.Invoke(view, new object[] { route[4], unit.Position });
                Require(resumed is List<HexCoord> && moves == 3 && enters == 1,
                    "SOLO resume after an already committed center must not duplicate logical moves or tile events.");
                grid.GetTile(route[4]).HasBuilding = true;
                Vector3 before = go.transform.position;
                // 이동 불가 타일을 중간 경유로 둔다. 최종 non-walkable contact 예외와 다르다.
                var unsafeRoute = new List<HexCoord> { route[3], route[4], new HexCoord(5, 2) };
                object rejected = writer.Invoke(view, new object[] { ViewConverter.ToView(mapper.HexToWorld(route[4])),
                    ViewConverter.ToView(mapper.HexToWorld(route[4])), Quaternion.identity,
                    UnitMovementIntentReason.Chase, false, null, 100f, unsafeRoute, 1, null, null });
                Require(rejected.ToString() != "Advanced" && go.transform.position == before
                    && unit.Position == route[3] && moves == 3 && enters == 1,
                    "SOLO unsafe candidate must leave Root, logical position, index and events unchanged.");
                var terminalRoute = new List<HexCoord> { route[3], route[4] };
                Vector3 terminal = ViewConverter.ToView(mapper.HexToWorld(route[4]));
                for (int pass = 0; pass < 2; pass++)
                {
                    object contact = writer.Invoke(view, new object[] { terminal, terminal, Quaternion.identity,
                        UnitMovementIntentReason.AStarPath, false, null, 100f, terminalRoute, 1, null, null });
                    Require(contact.ToString() == "Advanced" && unit.Position == route[3]
                        && go.transform.position == terminal && moves == 3 && enters == 1,
                        "SOLO nonwalkable final contact must remain unoccupied and allow its repeated endpoint without a logical jump.");
                }
            }
            finally
            {
                moved?.Dispose(); entered?.Dispose();
                if (go != null) { var v = go.GetComponent<Hexiege.Presentation.UnitView>();
                    v.GetType().GetField("_unitData", flags).SetValue(v, null); UnityEngine.Object.DestroyImmediate(go); }
                flow.Dispose(); idField.SetValue(null, oldId); NetworkContext.Set(oldServer, oldActive);
            }
        }

        public static void ValidateSoloPursuitCheckpointProgress()
        {
            // 생산 후보 계산 -> 실제 도착 결과 -> 생산 checkpoint 소비 조합이다.
            // 첫 중간 지점을 지나야 두 번째 타겟 지점으로 진행할 수 있다.
            var checkpoint = new UnitPathCheckpointTracker();
            double x = 0d, z = 0d;
            int index = 1;
            for (int frame = 0; frame < 4; frame++)
            {
                UnitSoloPursuitStep.Plan(x, z, 1d, 0d, .25d, out x, out z);
                bool reached = UnitSoloPursuitStep.Reached(x, z, 1d, 0d, .25d);
                if (UnitSoloPursuitStep.TryConsume(checkpoint, index, true, reached,
                        acquired: false, direct: false, routeCurrent: true)) index++;
            }
            Require(index == 2 && checkpoint.NextWaypointIndex == 2,
                "SOLO first nonterminal pursuit waypoint must be consumed after four real movement frames.");
            Require(!UnitSoloPursuitStep.TryConsume(checkpoint, 1, true, true, false, false, true),
                "SOLO a consumed index cannot advance twice.");
            UnitSoloPursuitStep.Plan(x, z, 2d, 0d, 8d, out x, out z);
            Require(x == 2d && z == 0d
                && UnitSoloPursuitStep.TryConsume(checkpoint, 2, true,
                    UnitSoloPursuitStep.Reached(x, z, 2d, 0d, 8d), false, false, true),
                "SOLO second waypoint clamps remaining distance and consumes exactly once.");
            checkpoint.Reset();
            Require(!UnitSoloPursuitStep.TryConsume(checkpoint, 1, false, true, false, false, true)
                && !UnitSoloPursuitStep.TryConsume(checkpoint, 1, true, true, true, false, true)
                && !UnitSoloPursuitStep.TryConsume(checkpoint, 1, true, true, false, true, true)
                && !UnitSoloPursuitStep.TryConsume(checkpoint, 1, true, true, false, false, false)
                && !UnitSoloPursuitStep.TryConsume(checkpoint, 2, true, true, false, false, true)
                && checkpoint.NextWaypointIndex == 1,
                "SOLO Held/acquired/direct/stale-route/skipped index never consumes a checkpoint.");
            // 타겟 타일/환경 변경 때 production이 Reset한 새 경로의 첫 지점만 소비한다.
            checkpoint.Reset();
            Require(!UnitSoloPursuitStep.TryConsume(checkpoint, 2, true, true, false, false, true)
                && UnitSoloPursuitStep.TryConsume(checkpoint, 1, true, true, false, false, true),
                "SOLO target/environment route replacement rejects the old index and starts at one.");
            foreach (double budget in new[] { 0d, -1d, double.NaN, double.PositiveInfinity })
            {
                UnitSoloPursuitStep.Plan(0d, 0d, 1d, 1d, budget, out x, out z);
                Require(x == 0d && z == 0d
                    && !UnitSoloPursuitStep.Reached(1d, 1d, 1d, 1d, budget),
                    "SOLO frozen/invalid travel budget neither moves nor reports arrival.");
            }
            UnitSoloPursuitStep.Plan(.9999d, .9999d, 1d, 1d, 10d, out x, out z);
            Require(x == 1d && z == 1d && UnitSoloPursuitStep.Reached(x, z, 1d, 1d, 10d),
                "SOLO short diagonal final distance cannot overshoot or oscillate.");
            Require(!UnitSoloPursuitStep.Reached(double.NaN, 0d, 1d, 1d, 1d),
                "SOLO invalid pose cannot report arrival.");
            int emitted = 0, omitted = 0, capMarkers = 0;
            for (int attempt = 0; attempt < 70; attempt++)
            {
                bool accepted = UnitSoloPursuitStep.TakeEvidence(ref emitted, ref omitted,
                    out bool firstOmission);
                if (firstOmission) capMarkers++;
                Require(accepted == (attempt < 64), "SOLO evidence detail cap must be exact.");
            }
            Require(emitted == 64 && omitted == 6 && capMarkers == 1,
                "SOLO evidence cap has one incomplete marker and preserves omitted count, never fake PASS.");
            Require(UnitSoloPursuitStep.FitsEvidence(new string('a', 600))
                && !UnitSoloPursuitStep.FitsEvidence(new string('a', 601))
                && UnitSoloPursuitStep.FitsEvidence(new string('한', 200))
                && !UnitSoloPursuitStep.FitsEvidence(new string('한', 201)),
                "SOLO evidence preflight counts UTF8 bytes, preserves envelope budget and rejects oversized records.");
        }

        private static void ValidateB3TrajectoryCorridorResolver()
        {
            WorldPointXZ current = default;
            ActionDirectionXZ facing = default;
            WorldPointXZ waypoint = default;
            WorldPointXZ next = default;
            Require(WorldPointXZ.TryCreate(0d, 0d, out current),
                "Corridor resolver current point fixture must be valid.");
            Require(ActionDirectionXZ.TryCreate(1d, 0d, out facing),
                "Corridor resolver facing fixture must be valid.");
            Require(WorldPointXZ.TryCreate(10d, 0d, out waypoint),
                "Corridor resolver waypoint fixture must be valid.");
            Require(WorldPointXZ.TryCreate(10d, 10d, out next),
                "Corridor resolver next waypoint fixture must be valid.");

            UnitTrajectoryCorridorResolution reduced =
                UnitTrajectoryCorridorResolver.Resolve(
                    current,
                    facing,
                    waypoint,
                    hasNextWaypoint: true,
                    next,
                    maximumTravelDistanceWorld: 1d,
                    maximumTurnDegrees: 90d,
                    cornerLookAheadDistanceWorld: 20d,
                    step => step.CandidatePosition.X <= 0.250001d,
                    out UnitTrajectoryStep reducedStep);
            Require(reduced == UnitTrajectoryCorridorResolution.ReducedSmooth
                    && reducedStep.IsValid
                    && reducedStep.ConsumedDistanceWorld > 0d
                    && reducedStep.ConsumedDistanceWorld <= 0.250001d
                    && !reducedStep.RequiresStationaryAlignment,
                "A full corridor rejection must first shrink the smooth non-zero movement instead of requesting A* again.");

            UnitTrajectoryCorridorResolution alignment =
                UnitTrajectoryCorridorResolver.Resolve(
                    current,
                    facing,
                    waypoint,
                    hasNextWaypoint: true,
                    next,
                    maximumTravelDistanceWorld: 1d,
                    maximumTurnDegrees: 4.5d,
                    cornerLookAheadDistanceWorld: 20d,
                    step => step.ConsumedDistanceWorld
                        <= UnitMovementEvaluationAdapter.PositionTolerance,
                    out UnitTrajectoryStep alignmentStep);
            Require(alignment
                        == UnitTrajectoryCorridorResolution.StationaryAlignment
                    && alignmentStep.IsValid
                    && alignmentStep.RequiresStationaryAlignment
                    && alignmentStep.ConsumedDistanceWorld == 0d
                    && alignmentStep.CandidatePosition.Equals(current),
                "When every non-zero candidate leaves the corridor, the safe current pose must rotate in place instead of consuming repath budget.");

            UnitTrajectoryCorridorResolution blocked =
                UnitTrajectoryCorridorResolver.Resolve(
                    current,
                    facing,
                    waypoint,
                    hasNextWaypoint: true,
                    next,
                    maximumTravelDistanceWorld: 1d,
                    maximumTurnDegrees: 4.5d,
                    cornerLookAheadDistanceWorld: 20d,
                    step => false,
                    out UnitTrajectoryStep blockedStep);
            Require(blocked == UnitTrajectoryCorridorResolution.RepathRequired
                    && !blockedStep.IsValid,
                "A current pose outside the corridor must remain fail-closed and request a real A* repath.");

            int typedReducedCalls = 0;
            UnitTrajectoryCorridorResolution typedReduced =
                UnitTrajectoryCorridorResolver.Resolve(
                    current,
                    facing,
                    waypoint,
                    hasNextWaypoint: true,
                    next,
                    maximumTravelDistanceWorld: 1d,
                    maximumTurnDegrees: 90d,
                    cornerLookAheadDistanceWorld: 20d,
                    new Func<UnitTrajectoryStep, UnitSpatialCandidateVerdict>(step =>
                    {
                        typedReducedCalls++;
                        return step.CandidatePosition.X <= 0.250001d
                            ? UnitSpatialCandidateVerdict.Accepted
                            : UnitSpatialCandidateVerdict.CandidateUnsafe;
                    }),
                    out UnitTrajectoryStep typedReducedStep,
                    out UnitSpatialCandidateVerdict typedReducedVerdict);
            Require(typedReduced == UnitTrajectoryCorridorResolution.ReducedSmooth
                    && typedReducedVerdict == UnitSpatialCandidateVerdict.Accepted
                    && typedReducedStep.ConsumedDistanceWorld > 0d
                    && typedReducedCalls > 1,
                "Typed OutsideLogicalPath candidates must remain side-effect-free probes until reduced movement is accepted.");

            int typedStationaryCalls = 0;
            UnitTrajectoryCorridorResolution typedStationary =
                UnitTrajectoryCorridorResolver.Resolve(
                    current,
                    facing,
                    waypoint,
                    hasNextWaypoint: true,
                    next,
                    maximumTravelDistanceWorld: 1d,
                    maximumTurnDegrees: 4.5d,
                    cornerLookAheadDistanceWorld: 20d,
                    new Func<UnitTrajectoryStep, UnitSpatialCandidateVerdict>(step =>
                    {
                        typedStationaryCalls++;
                        return step.ConsumedDistanceWorld
                                <= UnitMovementEvaluationAdapter.PositionTolerance
                            ? UnitSpatialCandidateVerdict.Accepted
                            : UnitSpatialCandidateVerdict.CandidateUnsafe;
                    }),
                    out UnitTrajectoryStep typedStationaryStep,
                    out UnitSpatialCandidateVerdict typedStationaryVerdict);
            Require(typedStationary
                        == UnitTrajectoryCorridorResolution.StationaryAlignment
                    && typedStationaryVerdict == UnitSpatialCandidateVerdict.Accepted
                    && typedStationaryStep.RequiresStationaryAlignment
                    && typedStationaryCalls > 1,
                "Typed unsafe movement candidates must be allowed to fall back to a safe stationary alignment.");

            int typedUnsafeCalls = 0;
            UnitTrajectoryCorridorResolution typedUnsafe =
                UnitTrajectoryCorridorResolver.Resolve(
                    current,
                    facing,
                    waypoint,
                    hasNextWaypoint: true,
                    next,
                    maximumTravelDistanceWorld: 1d,
                    maximumTurnDegrees: 4.5d,
                    cornerLookAheadDistanceWorld: 20d,
                    new Func<UnitTrajectoryStep, UnitSpatialCandidateVerdict>(_ =>
                    {
                        typedUnsafeCalls++;
                        return UnitSpatialCandidateVerdict.CandidateUnsafe;
                    }),
                    out UnitTrajectoryStep typedUnsafeStep,
                    out UnitSpatialCandidateVerdict typedUnsafeVerdict);
            Require(typedUnsafe == UnitTrajectoryCorridorResolution.RepathRequired
                    && typedUnsafeVerdict
                        == UnitSpatialCandidateVerdict.CandidateUnsafe
                    && !typedUnsafeStep.IsValid
                    && typedUnsafeCalls > 1,
                "OutsideLogicalPath candidates must exhaust bounded fallbacks and then request repath without staging a pose.");

            int routeInvalidatedCalls = 0;
            UnitTrajectoryCorridorResolution routeInvalidated =
                UnitTrajectoryCorridorResolver.Resolve(
                    current,
                    facing,
                    waypoint,
                    hasNextWaypoint: true,
                    next,
                    maximumTravelDistanceWorld: 1d,
                    maximumTurnDegrees: 90d,
                    cornerLookAheadDistanceWorld: 20d,
                    new Func<UnitTrajectoryStep, UnitSpatialCandidateVerdict>(_ =>
                    {
                        routeInvalidatedCalls++;
                        return UnitSpatialCandidateVerdict.RouteInvalidated;
                    }),
                    out UnitTrajectoryStep routeInvalidatedStep,
                    out UnitSpatialCandidateVerdict routeInvalidatedVerdict);
            Require(routeInvalidated
                        == UnitTrajectoryCorridorResolution.RepathRequired
                    && routeInvalidatedVerdict
                        == UnitSpatialCandidateVerdict.RouteInvalidated
                    && !routeInvalidatedStep.IsValid
                    && routeInvalidatedCalls == 1,
                "Missing/non-walkable route evidence must request repath immediately without reduced or same-frame retry probes.");

            int fatalCalls = 0;
            UnitTrajectoryCorridorResolution fatal =
                UnitTrajectoryCorridorResolver.Resolve(
                    current,
                    facing,
                    waypoint,
                    hasNextWaypoint: true,
                    next,
                    maximumTravelDistanceWorld: 1d,
                    maximumTurnDegrees: 90d,
                    cornerLookAheadDistanceWorld: 20d,
                    new Func<UnitTrajectoryStep, UnitSpatialCandidateVerdict>(_ =>
                    {
                        fatalCalls++;
                        return UnitSpatialCandidateVerdict.Fatal;
                    }),
                    out UnitTrajectoryStep fatalStep,
                    out UnitSpatialCandidateVerdict fatalVerdict);
            Require(fatal == UnitTrajectoryCorridorResolution.Invalid
                    && fatalVerdict == UnitSpatialCandidateVerdict.Fatal
                    && !fatalStep.IsValid
                    && fatalCalls == 1,
                "Fatal dependency/chain/capacity evidence must reject immediately and remain distinct from recoverable repath.");
        }

        private static void ValidateB3RotationWriterOwnership()
        {
            var ownership = new UnitRootRotationOwnership();
            Require(ownership.Owner == UnitRootRotationWriter.None
                    && ownership.TryAcquireMovement()
                    && ownership.MovementOwnsRoot
                    && !ownership.ActionOwnsRoot,
                "A navigation start must select Movement as the sole Simulation Root rotation writer.");

            Require(ownership.TransferToAction()
                    && ownership.ActionOwnsRoot
                    && !ownership.MovementOwnsRoot
                    && !ownership.TryAcquireMovement(),
                "Movement-to-action entry must atomically transfer ownership and reject movement reacquisition during combat.");

            ownership.ReleaseAction(resumeMovement: true);
            Require(ownership.MovementOwnsRoot
                    && !ownership.ActionOwnsRoot,
                "Action exit with a live navigation objective must return ownership to Movement.");

            Require(!UnitActionRotationEventPolicy.ShouldAcceptTargetEvent(
                        networkActive: true,
                        networkServer: true,
                        actionOwnsRoot: ownership.ActionOwnsRoot)
                    && UnitActionRotationEventPolicy.ShouldAcceptTargetEvent(
                        networkActive: true,
                        networkServer: false,
                        actionOwnsRoot: false)
                    && UnitActionRotationEventPolicy.ShouldAcceptTargetEvent(
                        networkActive: false,
                        networkServer: false,
                        actionOwnsRoot: false)
                    && !UnitActionRotationEventPolicy.ShouldAcceptStopEvent(
                        networkActive: true,
                        networkServer: true)
                    && UnitActionRotationEventPolicy.ShouldAcceptStopEvent(
                        networkActive: true,
                        networkServer: false)
                    && UnitActionRotationEventPolicy.ShouldAcceptStopEvent(
                        networkActive: false,
                        networkServer: false),
                "Delayed server target/stop events must not revive or release gameplay action rotation, while pure clients may retain presentation event handling.");

            Require(ownership.TransferToAction(),
                "A resumed movement writer must be transferable to a later action.");
            ownership.ReleaseAction(resumeMovement: false);
            Require(ownership.Owner == UnitRootRotationWriter.None,
                "Action exit without navigation must release Simulation Root rotation ownership.");

            Require(UnitMovementPresentationPolicy.ShouldHoldWalk(
                        decisionValid: true,
                        allowsMovement: false,
                        commitsAcquireCandidate: false,
                        targetAcquirePriority: false)
                    && !UnitMovementPresentationPolicy.ShouldHoldWalk(
                        decisionValid: true,
                        allowsMovement: true,
                        commitsAcquireCandidate: false,
                        targetAcquirePriority: false)
                    && !UnitMovementPresentationPolicy.ShouldHoldWalk(
                        decisionValid: true,
                        allowsMovement: false,
                        commitsAcquireCandidate: true,
                        targetAcquirePriority: true)
                    && !UnitMovementPresentationPolicy.ShouldHoldWalk(
                        decisionValid: true,
                        allowsMovement: false,
                        commitsAcquireCandidate: false,
                        targetAcquirePriority: true)
                    && !UnitMovementPresentationPolicy.ShouldHoldWalk(
                        decisionValid: false,
                        allowsMovement: false,
                        commitsAcquireCandidate: false,
                        targetAcquirePriority: false),
                "Walk must stop for ordinary stationary states but never insert Held between target acquisition and provisional Attack.");

            Require(UnitMovementScopePolicy.TryPrepare(
                        currentCommandRevision: 1UL,
                        currentSegmentRevision: 7UL,
                        pendingCommand: false,
                        pendingSegment: true,
                        out ulong preparedCommand,
                        out ulong preparedSegment)
                    && preparedCommand == 1UL
                    && preparedSegment == 8UL
                    && UnitMovementScopePolicy.TryPrepare(
                        currentCommandRevision: 1UL,
                        currentSegmentRevision: 7UL,
                        pendingCommand: false,
                        pendingSegment: true,
                        out ulong retriedCommand,
                        out ulong retriedSegment)
                    && retriedCommand == preparedCommand
                    && retriedSegment == preparedSegment,
                "A planner failure before reducer evaluation must leave the pending segment retry on the same next revision.");

            Require(UnitMovementScopePolicy.TryPrepare(
                        currentCommandRevision: 0UL,
                        currentSegmentRevision: 0UL,
                        pendingCommand: true,
                        pendingSegment: false,
                        out ulong firstCommand,
                        out ulong firstSegment)
                    && firstCommand == 1UL
                    && firstSegment == 1UL,
                "A unit with no accepted movement observation must always present command/segment 1/1 to its first reducer evaluation.");
        }

        private static void ValidateB3ClientAttackEntryPresentationOrder()
        {
            var handoffFailures = new UnitAttackEntryHandoffFailureEpisodes();
            Require(handoffFailures.ObserveFailure(6)
                    && !handoffFailures.ObserveFailure(6)
                    && handoffFailures.ActiveCount == 1,
                "Repeated handoff retries without an intervening success must count as one causal failure episode.");
            Require(handoffFailures.ObserveSuccess(6)
                    && handoffFailures.ActiveCount == 0
                    && handoffFailures.ObserveFailure(6),
                "A successful handoff must close the prior episode so a later independent failure can be counted.");
            handoffFailures.Retire(6);
            Require(handoffFailures.ActiveCount == 0,
                "Unit lifecycle retirement must remove any unfinished handoff failure episode.");

            Require(UnitAttackEntryCommitOrderPolicy.RequiresPresentationHandoff(
                        UnitMovementIntentReason.Chase,
                        commitsAcquireCandidate: true)
                    && !UnitAttackEntryCommitOrderPolicy.RequiresPresentationHandoff(
                        UnitMovementIntentReason.Chase,
                        commitsAcquireCandidate: false)
                    && !UnitAttackEntryCommitOrderPolicy.RequiresPresentationHandoff(
                        UnitMovementIntentReason.PostCombatResume,
                        commitsAcquireCandidate: true),
                "Every Chase AcquireTarget commit, including direct-in-range, must require presentation handoff while detect-range resume remains unchanged.");

            Require(!UnitAttackEntryCommitOrderPolicy.CanCommitRootStop(
                        requiresAttackPresentationHandoff: true,
                        attackPresentationHandoffCompleted: false)
                    && UnitAttackEntryCommitOrderPolicy.CanCommitRootStop(
                        requiresAttackPresentationHandoff: true,
                        attackPresentationHandoffCompleted: true)
                    && UnitAttackEntryCommitOrderPolicy.CanCommitRootStop(
                        requiresAttackPresentationHandoff: false,
                        attackPresentationHandoffCompleted: false),
                "An attack-entry Root stop must fail closed until provisional presentation handoff completes, while ordinary movement remains unchanged.");

            Require(WorldPointXZ.TryCreate(0d, 0d, out WorldPointXZ origin),
                "Client attack-entry order fixture origin must be valid.");
            Require(WorldPointXZ.TryCreate(1d, 0d, out WorldPointXZ moved),
                "Client attack-entry order fixture moved position must be valid.");

            Require(AttackEntryHandoffPolicy.Classify(
                        presentationMarkedActive: false,
                        completedReceiptMatchesTarget: false,
                        hostPresentationMatchesReceipt: false)
                    == AttackEntryHandoffDisposition.Begin
                && AttackEntryHandoffPolicy.Classify(
                        presentationMarkedActive: true,
                        completedReceiptMatchesTarget: true,
                        hostPresentationMatchesReceipt: true)
                    == AttackEntryHandoffDisposition.AcknowledgeSatisfied
                && AttackEntryHandoffPolicy.Classify(
                        presentationMarkedActive: true,
                        completedReceiptMatchesTarget: false,
                        hostPresentationMatchesReceipt: true)
                    == AttackEntryHandoffDisposition.DeferConflict,
                "Attack-entry handoff must ACK only an exact completed receipt and must not restart or falsely ACK conflicting active presentation.");

            var badOrder = new ClientAttackEntryPresentationOrderTracker();
            badOrder.ObserveFrame(origin, walkPresentationVisible: true,
                attackPresentationVisible: false, UnitMovementPhase.AlignToMove,
                1UL, 1UL, 1UL);
            badOrder.ObserveFrame(moved, walkPresentationVisible: true,
                attackPresentationVisible: false, UnitMovementPhase.Move,
                1UL, 2UL, 2UL);
            badOrder.ObserveFrame(moved, walkPresentationVisible: true,
                attackPresentationVisible: false, UnitMovementPhase.NoIntent,
                1UL, 3UL, 3UL);
            Require(badOrder.ClassifyAttackPresentationStarted(
                        out int transportStationaryFrames)
                        == ClientAttackEntryGapKind.TransportOrInterpolation
                    && transportStationaryFrames == 1,
                "A one-frame client Root/Attack arrival gap must remain visible evidence but be separated from a gameplay pause.");

            var causalPause = new ClientAttackEntryPresentationOrderTracker();
            causalPause.ObserveFrame(origin, walkPresentationVisible: true,
                attackPresentationVisible: false, UnitMovementPhase.AlignToMove,
                1UL, 1UL, 1UL);
            causalPause.ObserveFrame(moved, walkPresentationVisible: true,
                attackPresentationVisible: false, UnitMovementPhase.Move,
                1UL, 2UL, 2UL);
            for (int frame = 0;
                frame <= ClientAttackEntryPresentationOrderTracker
                    .MaximumTransportOrInterpolationFrames;
                frame++)
            {
                causalPause.ObserveFrame(moved, walkPresentationVisible: true,
                    attackPresentationVisible: false, UnitMovementPhase.NoIntent,
                    1UL, 3UL, 3UL);
            }
            Require(causalPause.ClassifyAttackPresentationStarted(
                        out int causalStationaryFrames)
                        == ClientAttackEntryGapKind.CausalPause
                    && causalStationaryFrames
                        == ClientAttackEntryPresentationOrderTracker
                            .MaximumTransportOrInterpolationFrames + 1,
                "A stationary Walk gap beyond the named transport/interpolation budget must remain a causal gameplay failure.");

            var sameFrameOrder = new ClientAttackEntryPresentationOrderTracker();
            sameFrameOrder.ObserveFrame(origin, walkPresentationVisible: true,
                attackPresentationVisible: false, UnitMovementPhase.AlignToMove,
                1UL, 1UL, 1UL);
            sameFrameOrder.ObserveFrame(moved, walkPresentationVisible: true,
                attackPresentationVisible: false, UnitMovementPhase.Move,
                1UL, 2UL, 2UL);
            Require(sameFrameOrder.ClassifyAttackPresentationStarted(
                        out int sameFrameStationaryFrames)
                        == ClientAttackEntryGapKind.None
                    && sameFrameStationaryFrames == 0,
                "A provisional Attack handoff completed before the first stationary client frame must pass.");

            var staleScope = new ClientAttackEntryPresentationOrderTracker();
            staleScope.ObserveFrame(origin, true, false,
                UnitMovementPhase.AlignToMove, 1UL, 1UL, 1UL);
            staleScope.ObserveFrame(moved, true, false,
                UnitMovementPhase.Move, 1UL, 2UL, 2UL);
            staleScope.ObserveFrame(moved, true, false,
                UnitMovementPhase.NoIntent, 1UL, 3UL, 3UL);
            staleScope.ObserveFrame(moved, true, false,
                UnitMovementPhase.NoIntent, 2UL, 1UL, 4UL);
            for (int frame = 0; frame < 283; frame++)
            {
                staleScope.ObserveFrame(moved, true, false,
                    UnitMovementPhase.NoIntent, 2UL, 1UL, 4UL);
            }
            Require(staleScope.ClassifyAttackPresentationStarted(out int staleFrames)
                    == ClientAttackEntryGapKind.None
                && staleFrames == 0,
                "A later command must retire the old moving-Walk candidate instead of fabricating a 283-frame attack-entry pause.");
        }

        private static void ValidateB3FiniteRepathGuard()
        {
            var pathA = new List<HexCoord>
            {
                new HexCoord(0, 0),
                new HexCoord(1, 0),
                new HexCoord(2, 0)
            };
            var pathAClone = new List<HexCoord>
            {
                new HexCoord(0, 0),
                new HexCoord(1, 0),
                new HexCoord(2, 0)
            };
            var pathB = new List<HexCoord>
            {
                new HexCoord(0, 0),
                new HexCoord(1, -1),
                new HexCoord(2, -1)
            };
            var pathC = new List<HexCoord>
            {
                new HexCoord(0, 0),
                new HexCoord(0, 1),
                new HexCoord(1, 1)
            };
            var invalidPath = new List<HexCoord> { new HexCoord(0, 0) };
            UnitPathSignature signatureB = default;

            Require(UnitPathSignature.TryCreate(pathA, out UnitPathSignature signatureA)
                && UnitPathSignature.TryCreate(pathAClone, out UnitPathSignature signatureAClone)
                && UnitPathSignature.TryCreate(pathB, out signatureB)
                && signatureA == signatureAClone,
                "Equal ordered HexCoord paths must produce the same deterministic signature.");

            var guard = new UnitRepathProgressGuard(pathA);
            Require(guard.Evaluate(10, pathAClone)
                    == UnitRepathDecision.AcceptedNextFrame,
                "A repeated path returned once must defer to the next frame instead of terminating the navigation objective.");
            Require(guard.Evaluate(10, pathB)
                    == UnitRepathDecision.RejectedFrameBudget,
                "A repeated-path defer must consume the frame budget so another repath cannot run synchronously.");
            Require(guard.AcceptedInFrame == 1
                    && guard.AcceptedWithoutProgress == 1,
                "A repeated-path defer must consume one bounded no-progress observation.");

            // 2026-08-24 Android Host Unit 30: spatial recoverable 뒤 pathfinder가
            // active path와 같은 path를 돌려줬고, 이를 새 회복으로 적용해 같은 corridor가
            // 다시 실패했다. 일반 재평가와 달리 이미 unsafe로 판정된 동일 path는 환경이
            // 바뀔 때까지 재적용하지 않아야 한다.
            var unit30RecoverableGuard = new UnitRepathProgressGuard(pathA);
            Require(unit30RecoverableGuard.Evaluate(
                        300,
                        pathAClone,
                        UnitRepathEvaluationContext.SpatialRecoverable)
                    == UnitRepathDecision.RejectedRepeatedPath
                    && unit30RecoverableGuard.AcceptedInFrame == 0
                    && unit30RecoverableGuard.AcceptedWithoutProgress == 0,
                "Unit 30-shaped spatial recovery must block an unchanged active path without consuming or replaying it.");
            Require(unit30RecoverableGuard.Evaluate(
                        301,
                        pathB,
                        UnitRepathEvaluationContext.SpatialRecoverable)
                    == UnitRepathDecision.AcceptedNextFrame
                    && unit30RecoverableGuard.CurrentPath == signatureB
                    && unit30RecoverableGuard.AcceptedInFrame == 1
                    && unit30RecoverableGuard.AcceptedWithoutProgress == 1,
                "Spatial recovery must still accept one valid path that differs from the unsafe active path.");
            Require(guard.Evaluate(11, pathB)
                    == UnitRepathDecision.AcceptedNextFrame,
                "A distinct path must be accepted after the frame boundary.");
            Require(guard.Evaluate(12, pathA)
                    == UnitRepathDecision.AcceptedNextFrame,
                "An A-to-B-to-A observation must remain bounded without immediately terminating the objective.");
            Require(guard.Evaluate(11, invalidPath)
                    == UnitRepathDecision.RejectedInvalidPath,
                "An invalid path must fail closed.");
            Require(guard.Evaluate(13, pathC)
                    == UnitRepathDecision.AcceptedNextFrame,
                "A distinct path must be accepted after the frame boundary.");
            Require(guard.ObserveProgress(pathC)
                && guard.AcceptedInFrame == 1
                && guard.AcceptedWithoutProgress == 0
                && guard.Evaluate(14, pathA)
                    == UnitRepathDecision.AcceptedNextFrame,
                "Committed progress must reset cycle history without reopening the same-frame budget.");

            var boundedGuard = new UnitRepathProgressGuard(pathA);
            for (int attempt = 0;
                attempt < UnitRepathProgressGuard.MaximumAcceptedWithoutProgress;
                attempt++)
            {
                var uniquePath = new List<HexCoord>
                {
                    new HexCoord(0, 0),
                    new HexCoord(10 + attempt, -attempt),
                    new HexCoord(20 + attempt, -attempt)
                };
                Require(boundedGuard.Evaluate(100 + attempt, uniquePath)
                        == UnitRepathDecision.AcceptedNextFrame,
                    "Distinct paths must remain bounded while allowing finite recovery attempts.");
            }

            var overflowPath = new List<HexCoord>
            {
                new HexCoord(0, 0),
                new HexCoord(99, -99),
                new HexCoord(100, -99)
            };
            Require(boundedGuard.Evaluate(200, overflowPath)
                    == UnitRepathDecision.RejectedNoProgressBudget,
                "Distinct repaths without committed progress must eventually fail closed.");

            var objective = new UnitNavigationObjective(
                new HexCoord(5, 0), pathA, 3UL);
            Require(objective.IsActive
                    && objective.State == UnitNavigationObjectiveState.Navigating
                    && objective.SuppressesDuplicateCommand(
                        new HexCoord(5, 0), 3UL)
                    && !objective.SuppressesDuplicateCommand(
                        new HexCoord(6, 0), 3UL),
                "An active objective must suppress only the same destination in the same environment.");
            objective.MarkWaitingRepath();
            objective.MarkBlocked();
            Require(objective.State == UnitNavigationObjectiveState.Blocked
                    && objective.SuppressesDuplicateCommand(
                        new HexCoord(5, 0), 3UL)
                    && !objective.CanQueueSuppressedPath(
                        new HexCoord(5, 0), 3UL),
                "A blocked objective must suppress a same-environment ticker without waking on its stale path.");
            Require(objective.NotifyEnvironmentChanged(4UL)
                    && objective.State == UnitNavigationObjectiveState.WaitingRepath
                    && objective.RepathGuard.AcceptedWithoutProgress == 0
                    && objective.CanQueueSuppressedPath(
                        new HexCoord(5, 0), 4UL)
                    && !objective.SuppressesDuplicateCommand(
                        new HexCoord(5, 0), 5UL),
                "A newer walkability revision must reopen the same objective without accepting future revisions.");
            Require(objective.RepathGuard.Evaluate(
                        400,
                        pathAClone,
                        UnitRepathEvaluationContext.General)
                    == UnitRepathDecision.AcceptedNextFrame
                    && objective.RepathGuard.Evaluate(
                        400,
                        pathAClone,
                        UnitRepathEvaluationContext.General)
                    == UnitRepathDecision.RejectedFrameBudget
                    && objective.RepathGuard.AcceptedInFrame == 1
                    && objective.RepathGuard.AcceptedWithoutProgress == 1,
                "A newer environment must accept the unchanged best path exactly once and reject a same-frame replay.");
            Require(!UnitSpatialDiagnosticAccountingPolicy
                    .IsUnexpectedRecoverableCleanup(
                        recoverablePending: true,
                        unitAlive: false)
                    && UnitSpatialDiagnosticAccountingPolicy
                    .IsUnexpectedRecoverableCleanup(
                        recoverablePending: true,
                        unitAlive: true),
                "Death lifecycle retirement must not be reported as live-objective recoverable cleanup.");
            objective.MarkNavigating();
            Require(objective.ObserveProgress(pathB)
                    && objective.State == UnitNavigationObjectiveState.Navigating,
                "Committed movement must keep the objective active and clear no-progress history.");
            objective.Complete();
            Require(!objective.IsActive
                    && objective.State == UnitNavigationObjectiveState.Completed
                    && !objective.SuppressesDuplicateCommand(
                        new HexCoord(5, 0), 4UL),
                "A completed objective must allow a future command to create a new lifecycle.");
        }

        private static void ValidateB3ContinuousLocomotionPlanner()
        {
            // Keep every fixture definitely assigned even when an earlier validation short-circuits.
            // This does not relax the fixture gate: Require still fails unless every TryCreate succeeds.
            WorldPointXZ origin = default;
            WorldPointXZ corner = default;
            WorldPointXZ next = default;
            ActionDirectionXZ north = default;
            Require(WorldPointXZ.TryCreate(0d, 0d, out origin)
                && WorldPointXZ.TryCreate(0d, 1d, out corner)
                && WorldPointXZ.TryCreate(
                    Math.Sin(Math.PI / 3d),
                    1d + Math.Cos(Math.PI / 3d),
                    out next)
                && ActionDirectionXZ.TryCreate(
                    0d, 1d, out north),
                "Continuous locomotion validation inputs must be valid.");

            WorldPointXZ position = origin;
            ActionDirectionXZ facing = north;
            bool reachedCorner = false;
            bool observedCenterAlignmentHold = false;
            for (int frame = 0; frame < 480 && !reachedCorner; frame++)
            {
                UnitTrajectoryStep step = default;
                Require(UnitServerTrajectoryPlanner.TryPlan(
                        position,
                        facing,
                        corner,
                        true,
                        next,
                        maximumTravelDistanceWorld: 0.01d,
                        maximumTurnDegrees: 4.5d,
                        cornerLookAheadDistanceWorld: 0.35d,
                        out step)
                    && step.IsValid
                    && (step.RequiresStationaryAlignment
                        ? step.ConsumedDistanceWorld == 0d
                        : step.ConsumedDistanceWorld > 0d),
                    "A center-to-center incoming segment must either advance or explicitly hold for alignment.");

                if (step.RequiresStationaryAlignment)
                {
                    observedCenterAlignmentHold = true;
                    Require(step.CandidatePosition.Equals(position),
                        "A center-alignment hold must not consume spatial progress.");
                    facing = step.TrajectoryDirection;
                    continue;
                }

                double movementX = step.CandidatePosition.X - position.X;
                double movementZ = step.CandidatePosition.Z - position.Z;
                ActionDirectionXZ velocity = default;
                Require(ActionDirectionXZ.TryCreate(
                        movementX, movementZ, out velocity)
                    && UnitActionPoseSample.GetYawDegrees(
                        velocity, step.TrajectoryDirection) <= 1d,
                    "Trajectory velocity and SimulationFacing must agree within one degree.");

                position = step.CandidatePosition;
                facing = step.TrajectoryDirection;
                reachedCorner = step.ReachedCurrentWaypoint;
            }
            double cornerDx = position.X - corner.X;
            double cornerDz = position.Z - corner.Z;
            double cornerDistance = Math.Sqrt(
                cornerDx * cornerDx + cornerDz * cornerDz);
            Require(reachedCorner
                    && cornerDistance
                        <= UnitMovementEvaluationAdapter.PositionTolerance,
                "A 60-degree path corner must consume its incoming waypoint only at the center.");
            Require(!observedCenterAlignmentHold
                    || cornerDistance
                        <= UnitMovementEvaluationAdapter.PositionTolerance,
                "Any safety alignment hold must still converge to the same center checkpoint.");

            // 중심을 소비한 다음 선분에서 60도 방향 전환을 수행한다. 중심 이전에 다음
            // 선분을 섞어 코너를 자르지 않으면서도, 정상 60도 전환은 정지하지 않고
            // 이동 방향과 SimulationFacing을 함께 회전해야 한다.
            bool reachedNext = false;
            bool observedTurningMove = false;
            bool observedSecondSegmentHold = false;
            for (int frame = 0; frame < 480 && !reachedNext; frame++)
            {
                UnitTrajectoryStep step = default;
                Require(UnitServerTrajectoryPlanner.TryPlan(
                        position,
                        facing,
                        next,
                        false,
                        default,
                        maximumTravelDistanceWorld: 0.01d,
                        maximumTurnDegrees: 4.5d,
                        cornerLookAheadDistanceWorld: 0.35d,
                        out step)
                    && step.IsValid
                    && (step.RequiresStationaryAlignment
                        ? step.ConsumedDistanceWorld == 0d
                        : step.ConsumedDistanceWorld > 0d),
                    "A center-to-center 60-degree outgoing segment must produce a valid movement or explicit alignment frame.");

                if (step.RequiresStationaryAlignment)
                {
                    observedSecondSegmentHold = true;
                    facing = step.TrajectoryDirection;
                    continue;
                }

                double movementX = step.CandidatePosition.X - position.X;
                double movementZ = step.CandidatePosition.Z - position.Z;
                ActionDirectionXZ velocity = default;
                Require(ActionDirectionXZ.TryCreate(
                        movementX, movementZ, out velocity)
                    && UnitActionPoseSample.GetYawDegrees(
                        velocity, step.TrajectoryDirection) <= 1d,
                    "Outgoing trajectory velocity and SimulationFacing must agree within one degree.");
                if (UnitActionPoseSample.GetYawDegrees(
                        facing, step.TrajectoryDirection) > 0.01d)
                {
                    observedTurningMove = true;
                }

                position = step.CandidatePosition;
                facing = step.TrajectoryDirection;
                reachedNext = step.ReachedCurrentWaypoint;
            }

            double nextDx = position.X - next.X;
            double nextDz = position.Z - next.Z;
            double nextDistance = Math.Sqrt(nextDx * nextDx + nextDz * nextDz);
            Require(reachedNext
                    && observedTurningMove
                    && !observedSecondSegmentHold
                    && nextDistance
                        <= UnitMovementEvaluationAdapter.PositionTolerance,
                "A normal 60-degree center transition must turn while moving on the outgoing segment without a stop.");

            var checkpoints = new UnitPathCheckpointTracker();
            Require(!checkpoints.TryConsume(1, reachedWaypoint: false)
                && !checkpoints.TryConsume(2, reachedWaypoint: true)
                && checkpoints.TryConsume(1, reachedWaypoint: true)
                && !checkpoints.TryConsume(1, reachedWaypoint: true)
                && checkpoints.TryConsume(2, reachedWaypoint: true)
                && checkpoints.NextWaypointIndex == 3,
                "Waypoint checkpoint must reject early/skip/duplicate commits and consume each tile once.");

            WorldPointXZ reverseTarget = default;
            UnitTrajectoryStep reverse = default;
            Require(WorldPointXZ.TryCreate(0d, -1d, out reverseTarget)
                && UnitServerTrajectoryPlanner.TryPlan(
                    origin,
                    north,
                    reverseTarget,
                    false,
                    default,
                    maximumTravelDistanceWorld: 0.1d,
                    maximumTurnDegrees: 4.5d,
                    cornerLookAheadDistanceWorld: 0.35d,
                    out reverse)
                && reverse.IsValid
                && reverse.RequiresStationaryAlignment
                && reverse.ConsumedDistanceWorld == 0d
                && reverse.CandidatePosition.Equals(origin),
                "A 150-degree-or-greater reverse must hold position and consume zero progress.");
        }

        private static void ValidateB3ClientReplicationClassification()
        {
            Type observerType = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                observerType = assemblies[index].GetType(
                    "Hexiege.Infrastructure.UnitMovementAuthorityObserver",
                    throwOnError: false);
                if (observerType != null)
                    break;
            }

            Require(observerType != null,
                "B3 movement authority observer type was not loaded.");
            MethodInfo classify = observerType.GetMethod(
                "ClassifyClientReplicatedStateForValidation",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo retire = observerType.GetMethod(
                "RetireUnitLifecycleForValidation",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo shouldObserve = observerType.GetMethod(
                "ShouldObserveClientReplicatedState",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo adapterBounds = observerType.GetMethod(
                "ValidateAdapterFailureStateForValidation",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo postGameAdapter = observerType.GetMethod(
                "ValidatePostGameAdapterFailureForValidation",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo terminalPreflight = observerType.GetMethod(
                "ValidateTerminalPreflightForValidation",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo productionTerminal = observerType.GetMethod(
                "ValidateProductionTerminalRecordsForValidation",
                BindingFlags.Static | BindingFlags.NonPublic);

            Type rootObserverType = null;
            for (int index = 0; index < assemblies.Length; index++)
            {
                rootObserverType = assemblies[index].GetType(
                    "Hexiege.Infrastructure.UnitRootPoseConsistencyObserver",
                    throwOnError: false);
                if (rootObserverType != null)
                    break;
            }
            MethodInfo rotationEvidencePreflight = rootObserverType?.GetMethod(
                "ValidateRotationEvidenceForValidation",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo rootTerminalPreflight = rootObserverType?.GetMethod(
                "ValidateTerminalSummaryForValidation",
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(classify != null && retire != null && shouldObserve != null
                    && adapterBounds != null && postGameAdapter != null
                    && terminalPreflight != null
                    && productionTerminal != null
                    && rotationEvidencePreflight != null
                    && rootTerminalPreflight != null,
                "B3 client replication classification seam is incomplete.");

            string Classify(
                bool hasPrevious,
                int previousUnitId,
                int previousPhase,
                ulong previousCommand,
                ulong previousSegment,
                ulong previousSemantic,
                int unitId,
                int phase,
                ulong command,
                ulong segment,
                ulong semantic)
                => (string)classify.Invoke(
                    null,
                    new object[]
                    {
                        hasPrevious, previousUnitId, previousPhase, previousCommand,
                        previousSegment, previousSemantic, unitId, phase,
                        command, segment, semantic
                    });

            int move = (int)UnitMovementPhase.Move;
            int align = (int)UnitMovementPhase.AlignToMove;
            Require(Classify(false, 0, 0, 0UL, 0UL, 0UL,
                    7, move, 5UL, 9UL, 6UL) == "None",
                "A coalesced first client snapshot must be accepted without requiring scope 1/1.");
            Require(Classify(true, 7, move, 1UL, 1UL, 4UL,
                    7, move, 1UL, 1UL, 4UL) == "Duplicate",
                "Same revision and same snapshot must be a valid duplicate.");
            Require(Classify(true, 7, move, 1UL, 1UL, 4UL,
                    7, align, 1UL, 1UL, 4UL)
                    .Contains("SameRevisionConflict"),
                "Same revision with a different snapshot must fail as conflict.");
            string identityConflict = Classify(
                true, 7, move, 1UL, 1UL, 4UL,
                8, move, 1UL, 1UL, 4UL);
            Require(identityConflict.Contains("IdentityConflict")
                && identityConflict.Contains("SameRevisionConflict"),
                "A unitId-only change in the same lifecycle must fail closed as identity conflict.");
            Require(Classify(true, 7, move, 1UL, 1UL, 4UL,
                    7, move, 1UL, 1UL, 3UL)
                    .Contains("RevisionRegression"),
                "A decreased semantic revision must fail as regression.");
            Require(Classify(false, 0, 0, 0UL, 0UL, 0UL,
                    7, move, 1UL, 1UL, 0UL)
                    .Contains("RevisionZero"),
                "A zero semantic revision must have its own failure cause.");
            Require(Classify(false, 0, 0, 0UL, 0UL, 0UL,
                    7, (int)UnitMovementPhase.Invalid, 1UL, 1UL, 1UL)
                    .Contains("InvalidPhase"),
                "An invalid phase must have its own failure cause.");
            Require(Classify(false, 0, 0, 0UL, 0UL, 0UL,
                    -1, move, 1UL, 1UL, 1UL)
                    .Contains("UnitUninitialized"),
                "An uninitialized unit must have its own failure cause.");
            Require(Classify(false, 0, 0, 0UL, 0UL, 0UL,
                    7, move, 0UL, 1UL, 1UL)
                    .Contains("InvalidScope"),
                "A zero command/segment scope must fail closed.");
            Require(Classify(true, 7, move, 3UL, 5UL, 7UL,
                    7, move, 2UL, 99UL, 8UL)
                    .Contains("ScopeRegression"),
                "A decreased command scope must fail as scope regression.");
            Require(Classify(true, 7, move, 3UL, 5UL, 7UL,
                    7, move, 3UL, 4UL, 8UL)
                    .Contains("ScopeRegression"),
                "A decreased segment in the same command must fail as scope regression.");
            Require(Classify(true, 7, move, 3UL, 5UL, 7UL,
                    7, move, 9UL, 27UL, 12UL) == "None",
                "Coalesced command/segment jumps must remain valid when scope is monotonic.");
            Require((string)retire.Invoke(null, null) == "PASS",
                "Retire must remove matching/mismatched completed lifecycle baselines and permit object-id reuse.");
            Require(!(bool)shouldObserve.Invoke(
                    null, new object[] { -1, 9UL })
                && (bool)shouldObserve.Invoke(
                    null, new object[] { 7, 9UL }),
                "A coalesced snapshot must defer while unitId is uninitialized and intake after registration.");
            Require((string)adapterBounds.Invoke(null, new object[] { 64 })
                        == "details=64,overflow=0,adapter=64,failure=True,reset=True"
                    && (string)adapterBounds.Invoke(null, new object[] { 65 })
                        == "details=64,overflow=1,adapter=65,failure=True,reset=True",
                "UnitView adapter failures must exercise real observer counters, fail verdict, bounded detail overflow and Reset.");
            Require((string)postGameAdapter.Invoke(null, null) == "PASS",
                "A post-GameEnd adapter callback must be counted separately without failing the gameplay movement contract.");
            string exactTerminalBudget = (string)terminalPreflight.Invoke(
                null,
                new object[] { 26, new string('x', 700) });
            Require(exactTerminalBudget.StartsWith(
                    "valid=True,lines=32,maxBytes=",
                    StringComparison.Ordinal),
                "26 manifest chunks plus five summary parts and END must fit the 32-line terminal budget.");
            Require((string)terminalPreflight.Invoke(
                    null,
                    new object[] { 27, new string('x', 700) })
                        == "valid=False,lines=33,maxBytes=0",
                "A terminal record set above 32 lines must fail closed before emission.");
            string utf8Overflow = (string)terminalPreflight.Invoke(
                null,
                new object[] { 1, new string('가', 300) });
            Require(utf8Overflow.StartsWith(
                    "valid=False,lines=7,maxBytes=",
                    StringComparison.Ordinal),
                "Terminal full-line limits must count UTF-8 bytes including identity and logger prefixes.");
            string productionTerminalResult = (string)productionTerminal.Invoke(null, null);
            Require(productionTerminalResult.StartsWith(
                        "valid=True,lines=32,maxBytes=",
                        StringComparison.Ordinal)
                    && productionTerminalResult.EndsWith(
                        ",reset=True",
                        StringComparison.Ordinal),
                "Production terminal manifest, five summary parts and compact END must all fit full-line UTF-8 and reset cleanly.");
            string rotationEvidenceResult =
                (string)rotationEvidencePreflight.Invoke(null, null);
            Require(rotationEvidenceResult.StartsWith(
                        "valid=True,fields=True,maxBytes=",
                        StringComparison.Ordinal),
                "Rotation replication evidence must preserve identity/time/tick/phase/revision/root fields and fit one Android UTF-8 line.");
            string rootTerminalResult =
                (string)rootTerminalPreflight.Invoke(null, null);
            Require(rootTerminalResult.StartsWith(
                        "valid=True,fields=True,maxBytes=",
                        StringComparison.Ordinal),
                "Root terminal must preserve every CrossAudit contract field and fit one Android UTF-8 line.");
        }

        private static void ValidateB3MovementPipelineModeLatch()
        {
            var host = new UnitMovementPipelineModeLatch();
            var client = new UnitMovementPipelineModeLatch();

            UnitMovementPipelineMode replicatedServerMode =
                UnitMovementPipelineMode.ReducerAuthoritative;
            Require(host.TryBeginMatch(replicatedServerMode)
                && client.TryBeginMatch(replicatedServerMode)
                && host.ActiveMode == UnitMovementPipelineMode.ReducerAuthoritative
                && client.ActiveMode == host.ActiveMode,
                "Host/client must latch the same server-published movement mode.");
            Require(host.TryBeginMatch(UnitMovementPipelineMode.ReducerAuthoritative)
                && !host.TryBeginMatch(UnitMovementPipelineMode.Legacy),
                "Movement mode must reject mid-match mutation and mixed writers.");

            host.EndMatch();
            client.EndMatch();
            Require(host.TryBeginMatch(UnitMovementPipelineMode.Legacy)
                && client.TryBeginMatch(UnitMovementPipelineMode.Legacy),
                "Rollback must select Legacy for the next complete match only.");

            var invalid = new UnitMovementPipelineModeLatch();
            Require(!invalid.TryBeginMatch((UnitMovementPipelineMode)99)
                && !invalid.IsMatchActive,
                "Unknown movement mode must reject match start without silent fallback.");

            var reducer = new UnitMovementReducer();
            UnitMovementEvaluation align = UnitMovementEvaluationAdapter.Evaluate(
                reducer,
                1UL,
                1UL,
                UnitMovementIntentReason.AStarPath,
                0d,
                0d,
                0d,
                1d,
                1d,
                0d,
                false,
                0.1d,
                1d);
            Require(align.ReducerInvoked
                && align.Status == UnitMovementReducerStatus.Accepted
                && align.Decision.Phase == UnitMovementPhase.AlignToMove
                && !align.Decision.AllowsMovement,
                "Shared production adapter must stop position while aligning.");

            double nineDegrees = 9d * (Math.PI / 180d);
            UnitMovementEvaluation move = UnitMovementEvaluationAdapter.Evaluate(
                reducer,
                1UL,
                1UL,
                UnitMovementIntentReason.AStarPath,
                0d,
                0d,
                Math.Sin(nineDegrees),
                Math.Cos(nineDegrees),
                0d,
                1d,
                false,
                0.1d,
                2d);
            Require(move.Status == UnitMovementReducerStatus.Accepted
                && move.Decision.Phase == UnitMovementPhase.Move
                && move.Decision.AllowsMovement,
                "Shared production adapter must release movement at the 10-degree gate.");

            UnitMovementEvaluation acquireStop = UnitMovementEvaluationAdapter.Evaluate(
                reducer,
                1UL,
                1UL,
                UnitMovementIntentReason.Chase,
                0d,
                0d,
                0d,
                1d,
                0d,
                2d,
                true,
                0d,
                3d);
            Require(acquireStop.Status == UnitMovementReducerStatus.Accepted
                && acquireStop.Decision.Phase == UnitMovementPhase.NoIntent
                && acquireStop.Decision.IsTargetAcquirePriority
                && !acquireStop.Decision.AllowsMovement,
                "Attack-range acquisition must publish NoIntent instead of stale Move/Align.");
            Require(Math.Abs(move.DesiredMoveDirection.X) < 0.000001d
                && Math.Abs(move.DesiredMoveDirection.Z - 1d) < 0.000001d,
                "Authoritative rotation source must be the reducer-approved desired direction.");
        }

        private static void ValidateB3MovementManifestPreflight()
        {
            IReadOnlyList<string> chunks;
            Require(UnitMovementManifestChunker.TryBuild(
                    new[] { "12345", "67890" },
                    payloadUtf8Limit: 5,
                    maximumChunks: 2,
                    reservedTerminalLines: 4,
                    nonManifestTerminalLines: 2,
                    out chunks)
                && chunks.Count == 2,
                "B3 manifest must accept the exact chunk/terminal boundary.");

            Require(!UnitMovementManifestChunker.TryBuild(
                    new[] { "12345", "67890" },
                    payloadUtf8Limit: 5,
                    maximumChunks: 1,
                    reservedTerminalLines: 4,
                    nonManifestTerminalLines: 2,
                    out chunks),
                "B3 manifest must reject a chunk-count overflow.");
            Require(!UnitMovementManifestChunker.TryBuild(
                    new[] { "12345", "67890" },
                    payloadUtf8Limit: 5,
                    maximumChunks: 2,
                    reservedTerminalLines: 3,
                    nonManifestTerminalLines: 2,
                    out chunks),
                "B3 manifest must reserve summary and END terminal lines.");
            Require(!UnitMovementManifestChunker.TryBuild(
                    new[] { "123456" },
                    payloadUtf8Limit: 5,
                    maximumChunks: 2,
                    reservedTerminalLines: 4,
                    nonManifestTerminalLines: 2,
                    out chunks),
                "B3 manifest must fail closed for an oversized entry.");
            Require(!UnitMovementManifestChunker.TryBuild(
                    new[] { "가나" },
                    payloadUtf8Limit: 5,
                    maximumChunks: 2,
                    reservedTerminalLines: 4,
                    nonManifestTerminalLines: 2,
                    out chunks),
                "B3 manifest limits must be measured in UTF-8 bytes.");
        }

        /// <summary>
        /// 상태머신의 모든 전이를 검증하는 테스트가 아니다.
        /// Tracer A가 합의한 phase 이름과 직렬화 순번만 바뀌지 않았는지 확인한다.
        /// </summary>
        private static void ValidatePhaseVocabularyAndOrdinals()
        {
            UnitActionPhase[] expected =
            {
                UnitActionPhase.Idle,
                UnitActionPhase.Navigate,
                UnitActionPhase.AlignToMove,
                UnitActionPhase.Move,
                UnitActionPhase.AcquireTarget,
                UnitActionPhase.Chase,
                UnitActionPhase.AlignToAttack,
                UnitActionPhase.Windup,
                UnitActionPhase.Impact,
                UnitActionPhase.Recovery,
                UnitActionPhase.Dead
            };

            Require(Enum.GetValues(typeof(UnitActionPhase)).Length == expected.Length,
                "UnitActionPhase member count changed unexpectedly.");

            for (int index = 0; index < expected.Length; index++)
            {
                Require((int)expected[index] == index,
                    $"UnitActionPhase ordering changed at index {index}.");
            }
        }

        private static void ValidateMonotonicSequences()
        {
            var allocator = new AttackSequenceAllocator();
            var firstInstance = new AttackerInstanceId(7UL);
            var secondInstance = new AttackerInstanceId(9UL);
            AttackSequenceId attackerSevenFirst = allocator.Next(firstInstance);
            AttackSequenceId attackerSevenSecond = allocator.Next(firstInstance);
            AttackSequenceId attackerNineFirst = allocator.Next(secondInstance);

            Require(attackerSevenFirst.IsValid, "First sequence must be valid.");
            Require(attackerSevenSecond.CompareTo(attackerSevenFirst) > 0,
                "Sequence must increase for the same attacker.");
            Require(attackerNineFirst.Value == 1UL,
                "Sequence counters must be independent per attacker.");
        }

        private static void ValidateMovementHysteresis()
        {
            Require(UnitActionAngleHysteresis.AllowsMovement(10d, false),
                "Movement must enter at 10 degrees.");
            Require(!UnitActionAngleHysteresis.AllowsMovement(10.001d, false),
                "Movement must not enter above 10 degrees.");
            Require(UnitActionAngleHysteresis.AllowsMovement(15d, true),
                "Movement must remain active at 15 degrees.");
            Require(!UnitActionAngleHysteresis.AllowsMovement(15.001d, true),
                "Movement must stop above 15 degrees.");
            Require(UnitActionAngleHysteresis.AllowsMovement(350d, false),
                "350 degrees must normalize to the 10-degree movement entry boundary.");
            Require(!UnitActionAngleHysteresis.AllowsMovement(double.PositiveInfinity, true),
                "Infinite movement error must fail closed.");
        }

        /// <summary>
        /// B2 이동 reducer가 Unity나 NGO 없이 명령·구간 회차, 10°/15° 경계,
        /// 타겟 획득 우선권과 잘못된 입력의 무변경 거부를 지키는지 검증한다.
        /// </summary>
        private static void ValidateB2MovementReducer()
        {
            Require(ActionDirectionXZ.TryCreate(0d, 1d, out ActionDirectionXZ forward),
                "B2 forward fixture must be valid.");
            ActionDirectionXZ yaw10 = CreateMovementDirection(10d);
            ActionDirectionXZ yaw10Over = CreateMovementDirection(10.001d);
            ActionDirectionXZ yaw12 = CreateMovementDirection(12d);
            ActionDirectionXZ yaw15 = CreateMovementDirection(15d);
            ActionDirectionXZ yaw15Over = CreateMovementDirection(15.001d);

            var entry = new UnitMovementReducer();
            Require(entry.Snapshot.Revision == 1UL
                && entry.Snapshot.Phase == UnitMovementPhase.NoIntent
                && !entry.Snapshot.HasAcceptedObservation,
                "B2 reducer must start at revision 1 with no accepted movement intent.");
            Require(entry.Evaluate(
                    entry.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, yaw10, false, 1d,
                    out UnitMovementDecision entryAtTen)
                == UnitMovementReducerStatus.Accepted,
                "B2 movement must enter exactly at 10 degrees.");
            Require(entryAtTen.IsValid
                && entryAtTen.Phase == UnitMovementPhase.Move
                && entryAtTen.AllowsMovement
                && entryAtTen.HasYawError
                && Math.Abs(entryAtTen.YawErrorDegrees - 10d) < 0.000001d,
                "The 10-degree B2 decision must be a valid Move.");

            UnitMovementSnapshot beforeDuplicate = entry.Snapshot;
            Require(entry.Evaluate(
                    beforeDuplicate.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, yaw10, false, 1d,
                    out UnitMovementDecision duplicateDecision)
                == UnitMovementReducerStatus.Duplicate,
                "An exact same-time B2 observation must be classified as duplicate.");
            Require(duplicateDecision.IsValid
                && duplicateDecision.Phase == UnitMovementPhase.Move
                && entry.Snapshot.Revision == beforeDuplicate.Revision,
                "A duplicate must reproduce the current decision without mutation.");

            Require(entry.Evaluate(
                    entry.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, yaw15, false, 2d,
                    out UnitMovementDecision remainAtFifteen)
                == UnitMovementReducerStatus.Accepted
                && remainAtFifteen.Phase == UnitMovementPhase.Move,
                "An active B2 Move must remain active exactly at 15 degrees.");
            Require(entry.Evaluate(
                    entry.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, yaw15Over, false, 3d,
                    out UnitMovementDecision exitAboveFifteen)
                == UnitMovementReducerStatus.Accepted
                && exitAboveFifteen.Phase == UnitMovementPhase.AlignToMove
                && !exitAboveFifteen.AllowsMovement,
                "An active B2 Move must stop above 15 degrees.");

            var aboveEntry = new UnitMovementReducer();
            Require(aboveEntry.Evaluate(
                    aboveEntry.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.Chase, true,
                    forward, yaw10Over, false, 1d,
                    out UnitMovementDecision alignAboveTen)
                == UnitMovementReducerStatus.Accepted
                && alignAboveTen.Phase == UnitMovementPhase.AlignToMove
                && !alignAboveTen.AllowsMovement,
                "A new B2 movement scope must align above 10 degrees.");
            Require(aboveEntry.Evaluate(
                    aboveEntry.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.Chase, true,
                    forward, yaw10, false, 2d,
                    out UnitMovementDecision enterAfterAlign)
                == UnitMovementReducerStatus.Accepted
                && enterAfterAlign.Phase == UnitMovementPhase.Move,
                "AlignToMove must enter Move once error reaches 10 degrees.");

            Require(aboveEntry.Evaluate(
                    aboveEntry.Snapshot.Revision, 1UL, 2UL,
                    UnitMovementIntentReason.PostCombatResume, true,
                    forward, yaw12, false, 3d,
                    out UnitMovementDecision newSegment)
                == UnitMovementReducerStatus.Accepted
                && newSegment.Phase == UnitMovementPhase.AlignToMove,
                "A new segment must use the 10-degree entry threshold instead of inheriting Move.");
            Require(aboveEntry.Snapshot.CommandRevision == 1UL
                && aboveEntry.Snapshot.SegmentRevision == 2UL
                && aboveEntry.Snapshot.IntentReason == UnitMovementIntentReason.PostCombatResume,
                "B2 snapshot must retain exact command, segment, and intent-reason scope.");

            Require(aboveEntry.Evaluate(
                    aboveEntry.Snapshot.Revision, 1UL, 2UL,
                    UnitMovementIntentReason.PostCombatResume, true,
                    forward, forward, true, 4d,
                    out UnitMovementDecision targetPriority)
                == UnitMovementReducerStatus.Accepted
                && targetPriority.IsValid
                && targetPriority.IsTargetAcquirePriority
                && targetPriority.Phase == UnitMovementPhase.NoIntent
                && !targetPriority.AllowsMovement
                && !targetPriority.HasYawError,
                "Target acquisition must override even a perfectly aligned movement intent.");

            Require(aboveEntry.Evaluate(
                    aboveEntry.Snapshot.Revision, 2UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, 5d,
                    out UnitMovementDecision nextCommand)
                == UnitMovementReducerStatus.Accepted
                && nextCommand.Phase == UnitMovementPhase.Move
                && aboveEntry.Snapshot.CommandRevision == 2UL
                && aboveEntry.Snapshot.SegmentRevision == 1UL,
                "The next command must restart at segment 1 and evaluate independently.");

            UnitMovementSnapshot stable = aboveEntry.Snapshot;
            Require(aboveEntry.Evaluate(
                    stable.Revision - 1UL, 2UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, 6d, out UnitMovementDecision staleRevision)
                == UnitMovementReducerStatus.StaleRevision
                && staleRevision.Phase == UnitMovementPhase.Invalid
                && !staleRevision.IsValid,
                "A stale reducer revision must fail closed.");
            Require(aboveEntry.Evaluate(
                    stable.Revision, 1UL, 2UL,
                    UnitMovementIntentReason.PostCombatResume, true,
                    forward, forward, false, 6d, out _)
                == UnitMovementReducerStatus.StaleCommandRevision,
                "An old command revision must be rejected.");
            Require(aboveEntry.Evaluate(
                    stable.Revision, 2UL, 0UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, 6d, out _)
                == UnitMovementReducerStatus.InvalidInput,
                "Segment revision zero must be rejected.");
            Require(aboveEntry.Evaluate(
                    stable.Revision, 2UL, 3UL,
                    UnitMovementIntentReason.PendingRepath, true,
                    forward, forward, false, 6d, out _)
                == UnitMovementReducerStatus.InvalidInput,
                "A skipped segment revision must fail closed.");
            Require(aboveEntry.Evaluate(
                    stable.Revision, 4UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, 6d, out _)
                == UnitMovementReducerStatus.InvalidInput,
                "A skipped command revision must fail closed.");

            var staleSegmentReducer = new UnitMovementReducer();
            Require(staleSegmentReducer.Evaluate(
                    staleSegmentReducer.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, 1d, out _)
                == UnitMovementReducerStatus.Accepted
                && staleSegmentReducer.Evaluate(
                    staleSegmentReducer.Snapshot.Revision, 1UL, 2UL,
                    UnitMovementIntentReason.PendingRepath, true,
                    forward, forward, false, 2d, out _)
                == UnitMovementReducerStatus.Accepted
                && staleSegmentReducer.Evaluate(
                    staleSegmentReducer.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, 3d, out _)
                == UnitMovementReducerStatus.StaleSegmentRevision,
                "An old segment in the current command must be rejected.");

            UnitMovementSnapshot beforeInvalid = aboveEntry.Snapshot;
            Require(aboveEntry.Evaluate(
                    beforeInvalid.Revision, 2UL, 1UL,
                    (UnitMovementIntentReason)999, true,
                    forward, forward, false, 6d,
                    out UnitMovementDecision unknownReason)
                == UnitMovementReducerStatus.InvalidInput
                && unknownReason.Phase == UnitMovementPhase.Invalid
                && !unknownReason.AllowsMovement,
                "An unknown movement intent reason must fail closed.");
            Require(aboveEntry.Evaluate(
                    beforeInvalid.Revision, 2UL, 1UL,
                    UnitMovementIntentReason.None, true,
                    forward, forward, false, 6d, out _)
                == UnitMovementReducerStatus.InvalidInput,
                "A present intent cannot use the None reason.");
            Require(aboveEntry.Evaluate(
                    beforeInvalid.Revision, 2UL, 1UL,
                    UnitMovementIntentReason.None, false,
                    forward, forward, false, 6d, out _)
                == UnitMovementReducerStatus.InvalidInput,
                "NoIntent must not carry a desired direction.");
            Require(aboveEntry.Evaluate(
                    beforeInvalid.Revision, 2UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    default, forward, false, 6d, out _)
                == UnitMovementReducerStatus.InvalidInput,
                "An invalid SimulationFacing must fail closed.");
            Require(aboveEntry.Evaluate(
                    beforeInvalid.Revision, 2UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, default, false, 6d, out _)
                == UnitMovementReducerStatus.InvalidInput,
                "An invalid DesiredMoveDirection must fail closed.");
            Require(aboveEntry.Evaluate(
                    beforeInvalid.Revision, 2UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, double.NaN, out _)
                == UnitMovementReducerStatus.InvalidTime,
                "NaN server time must fail closed.");
            Require(aboveEntry.Evaluate(
                    beforeInvalid.Revision, 2UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, yaw10, false, 5d,
                    out UnitMovementDecision sameTimeChangedPose)
                == UnitMovementReducerStatus.Accepted
                && sameTimeChangedPose.Phase == UnitMovementPhase.Move,
                "Distinct pre/post writer samples at the same server time must both be accepted.");
            UnitMovementSnapshot afterSameTimeChangedPose = aboveEntry.Snapshot;
            Require(aboveEntry.Evaluate(
                    afterSameTimeChangedPose.Revision, 2UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, 4.999d, out _)
                == UnitMovementReducerStatus.InvalidTime,
                "Backward server time must fail closed.");
            Require(aboveEntry.Snapshot.Revision == afterSameTimeChangedPose.Revision
                && aboveEntry.Snapshot.CommandRevision == afterSameTimeChangedPose.CommandRevision
                && aboveEntry.Snapshot.SegmentRevision == afterSameTimeChangedPose.SegmentRevision
                && aboveEntry.Snapshot.Phase == afterSameTimeChangedPose.Phase,
                "Every rejected B2 observation must leave the latest accepted snapshot unchanged.");

            var noIntent = new UnitMovementReducer();
            Require(noIntent.Evaluate(
                    noIntent.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.None, false,
                    forward, default, false, 1d,
                    out UnitMovementDecision noIntentDecision)
                == UnitMovementReducerStatus.Accepted
                && noIntentDecision.Phase == UnitMovementPhase.NoIntent
                && noIntentDecision.IsValid
                && !noIntentDecision.AllowsMovement,
                "An explicit absence of movement intent must be a valid movement-forbidden decision.");

            UnitMovementReducer exhausted =
                UnitMovementReducer.CreateForValidation(ulong.MaxValue);
            Require(exhausted.Evaluate(
                    exhausted.Snapshot.Revision, 1UL, 1UL,
                    UnitMovementIntentReason.AStarPath, true,
                    forward, forward, false, 1d, out _)
                == UnitMovementReducerStatus.Exhausted
                && exhausted.Snapshot.Revision == ulong.MaxValue
                && !exhausted.Snapshot.HasAcceptedObservation,
                "B2 reducer revision exhaustion must fail closed without wrapping.");
        }

        private static void ValidateB2MovementManifestChunking()
        {
            Type observerType = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                observerType = assemblies[index].GetType(
                    "Hexiege.Infrastructure.UnitMovementShadowObserver",
                    throwOnError: false);
                if (observerType != null)
                    break;
            }

            Require(observerType != null,
                "B2 movement shadow observer type was not loaded.");
            const BindingFlags flags =
                BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo buildPayloads = observerType.GetMethod(
                "BuildCoverageManifestPayloadsForValidation",
                flags);
            MethodInfo buildCompletedLine = observerType.GetMethod(
                "BuildCoverageManifestCompletedLineForValidation",
                flags);
            PropertyInfo payloadLimitProperty = observerType.GetProperty(
                "CoverageManifestPayloadLimitForValidation",
                flags);
            PropertyInfo chunkLimitProperty = observerType.GetProperty(
                "MaximumCoverageManifestChunksForValidation",
                flags);
            Require(buildPayloads != null
                && buildCompletedLine != null
                && payloadLimitProperty != null
                && chunkLimitProperty != null,
                "B2 movement manifest validation seam is incomplete.");

            int payloadLimit = (int)payloadLimitProperty.GetValue(null);
            int maximumChunks = (int)chunkLimitProperty.GetValue(null);
            List<string> expectedEntries =
                BuildRealMatchMovementManifestFixture(4);
            var payloads = (IReadOnlyList<string>)buildPayloads.Invoke(
                null,
                new object[] { expectedEntries });
            Require(payloads.Count > 1 && payloads.Count <= maximumChunks,
                "Four-unit-type manifest exceeded reserved terminal capacity.");

            var reconstructed = new List<string>(expectedEntries.Count);
            for (int index = 0; index < payloads.Count; index++)
            {
                string payload = payloads[index];
                int payloadBytes = Encoding.UTF8.GetByteCount(payload);
                Require(payload.Length <= payloadLimit
                    && payloadBytes <= payloadLimit,
                    "Coverage manifest payload exceeded its safe limit.");
                Require(
                    payload.Contains($"chunk={index + 1}/{payloads.Count}"),
                    "Coverage manifest chunk ordinal is invalid.");
                Require(
                    payload.Contains($"payloadChars={payload.Length}"),
                    "Coverage manifest character metadata is invalid.");
                Require(
                    payload.Contains($"payloadUtf8Bytes={payloadBytes}"),
                    "Coverage manifest UTF-8 metadata is invalid.");

                string completedLine = (string)buildCompletedLine.Invoke(
                    null,
                    new object[] { payload });
                Require(Encoding.UTF8.GetByteCount(completedLine) < 1000,
                    "Completed Android coverage-manifest line reached 1000 UTF-8 bytes.");

                const string entriesMarker = ", entries=";
                int entriesIndex = payload.IndexOf(
                    entriesMarker,
                    StringComparison.Ordinal);
                Require(entriesIndex >= 0,
                    "Coverage manifest entries marker is missing.");
                string entries = payload.Substring(
                    entriesIndex + entriesMarker.Length);
                reconstructed.AddRange(entries.Split(';'));
            }

            Require(reconstructed.Count == expectedEntries.Count,
                "Coverage manifest reconstruction changed the entry count.");
            for (int index = 0; index < expectedEntries.Count; index++)
            {
                Require(
                    string.Equals(
                        reconstructed[index],
                        expectedEntries[index],
                        StringComparison.Ordinal),
                    $"Coverage manifest entry {index} was lost, duplicated, or reordered.");
            }

            var emptyPayloads = (IReadOnlyList<string>)buildPayloads.Invoke(
                null,
                new object[] { Array.Empty<string>() });
            Require(emptyPayloads.Count == 1
                && emptyPayloads[0].EndsWith(
                    ", entries=none",
                    StringComparison.Ordinal)
                && Encoding.UTF8.GetByteCount(
                    (string)buildCompletedLine.Invoke(
                        null,
                        new object[] { emptyPayloads[0] })) < 1000,
                "Empty coverage manifest is not Android-safe.");

            var fiveTypePayloads =
                (IReadOnlyList<string>)buildPayloads.Invoke(
                    null,
                    new object[]
                    {
                        BuildRealMatchMovementManifestFixture(5)
                    });
            Require(fiveTypePayloads.Count <= maximumChunks,
                "A five-unit-type manifest that fits the terminal budget was rejected.");

            var exactBoundaryEntries = new List<string>();
            for (int index = 0; index < maximumChunks; index++)
            {
                exactBoundaryEntries.Add(
                    $"stats|BoundaryUnit{index:D2}|Blue|AStarPath|" +
                    new string('x', 580));
            }

            var exactBoundaryPayloads =
                (IReadOnlyList<string>)buildPayloads.Invoke(
                    null,
                    new object[] { exactBoundaryEntries });
            Require(exactBoundaryPayloads.Count == maximumChunks,
                "The exact reserved-terminal boundary must produce 29 chunks.");
            for (int index = 0; index < exactBoundaryPayloads.Count; index++)
            {
                string payload = exactBoundaryPayloads[index];
                int payloadBytes = Encoding.UTF8.GetByteCount(payload);
                Require(
                    payload.Contains(
                        $"chunk={index + 1}/{maximumChunks}"),
                    "Boundary coverage manifest chunk ordinal is invalid.");
                Require(
                    payload.Contains($"payloadChars={payload.Length}"),
                    "Boundary coverage manifest character metadata is invalid.");
                Require(
                    payload.Contains($"payloadUtf8Bytes={payloadBytes}"),
                    "Boundary coverage manifest UTF-8 metadata is invalid.");
                Require(payload.Length <= payloadLimit
                    && payloadBytes <= payloadLimit,
                    "Boundary coverage manifest payload exceeded its safe limit.");
                string completedLine = (string)buildCompletedLine.Invoke(
                    null,
                    new object[] { payload });
                Require(Encoding.UTF8.GetByteCount(completedLine) < 1000,
                    "Boundary coverage-manifest line reached 1000 UTF-8 bytes.");
            }

            var oversizedEntries = new List<string>();
            for (int index = 0; index < maximumChunks + 1; index++)
            {
                oversizedEntries.Add(
                    $"stats|OverflowUnit{index:D2}|Blue|AStarPath|" +
                    new string('x', 580));
            }

            bool oversizedRejected = false;
            try
            {
                buildPayloads.Invoke(
                    null,
                    new object[] { oversizedEntries });
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is InvalidOperationException)
            {
                oversizedRejected = true;
            }

            Require(oversizedRejected,
                "A manifest requiring more than the reserved chunks must fail closed.");
            RequireB2ManifestRejected(
                buildPayloads,
                new List<string> { new string('x', payloadLimit + 1) },
                "A single oversized coverage entry must fail closed.");
            RequireB2ManifestRejected(
                buildPayloads,
                null,
                "A null coverage entry list must fail closed.");
            RequireB2ManifestRejected(
                buildPayloads,
                new List<string> { string.Empty },
                "An empty coverage entry must fail closed.");
            RequireB2ManifestRejected(
                buildPayloads,
                new List<string> { "coverage|Unit;injected" },
                "A coverage entry containing a semicolon must fail closed.");
            RequireB2ManifestRejected(
                buildPayloads,
                new List<string> { "coverage|Unit\rinjected" },
                "A coverage entry containing CR must fail closed.");
            RequireB2ManifestRejected(
                buildPayloads,
                new List<string> { "coverage|Unit\ninjected" },
                "A coverage entry containing LF must fail closed.");
        }

        private static void ValidateB2MovementEndpointAdapter()
        {
            Type observerType = FindMovementShadowObserverType();
            const BindingFlags staticFlags =
                BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo evaluateEndpoint = observerType.GetMethod(
                "EvaluateEndpointAdapterForValidation",
                staticFlags);
            MethodInfo evaluateLifecycle = observerType.GetMethod(
                "EvaluateEndpointLifecycleForValidation",
                staticFlags);
            PropertyInfo toleranceProperty = observerType.GetProperty(
                "PositionDeltaToleranceForValidation",
                staticFlags);
            Require(evaluateEndpoint != null
                && evaluateLifecycle != null
                && toleranceProperty != null,
                "B2 endpoint adapter validation seam is incomplete.");
            float tolerance = (float)toleranceProperty.GetValue(null);

            object cannonEndpoint = evaluateEndpoint.Invoke(
                null,
                new object[] { 4.5f, -12.99f, 4.5f, -12.99f, 0f, false });
            Require(
                ReadValidationProperty<UnitMovementReducerStatus>(
                    cannonEndpoint, "Status")
                    == UnitMovementReducerStatus.Accepted
                && ReadValidationProperty<UnitMovementPhase>(
                    cannonEndpoint, "Phase")
                    == UnitMovementPhase.NoIntent
                && ReadValidationProperty<bool>(
                    cannonEndpoint, "DecisionIsValid")
                && ReadValidationProperty<bool>(
                    cannonEndpoint, "EndpointNormalized")
                && ReadValidationProperty<UnitMovementIntentReason>(
                    cannonEndpoint, "EvaluatedIntentReason")
                    == UnitMovementIntentReason.None
                && !ReadValidationProperty<bool>(
                    cannonEndpoint, "EvaluatedHasIntent")
                && !ReadValidationProperty<bool>(
                    cannonEndpoint, "EvaluatedDesiredDirectionIsValid")
                && ReadValidationProperty<ulong>(
                    cannonEndpoint, "RevisionAfterInitial") == 2UL
                && ReadValidationProperty<ulong>(
                    cannonEndpoint, "CommandRevisionAfterInitial") == 1UL
                && ReadValidationProperty<ulong>(
                    cannonEndpoint, "SegmentRevisionAfterInitial") == 1UL,
                "Captured CannonCart endpoint must normalize to explicit NoIntent.");

            object targetEndpoint = evaluateEndpoint.Invoke(
                null,
                new object[] { 4.5f, -12.99f, 4.5f, -12.99f, 0f, true });
            Require(
                ReadValidationProperty<UnitMovementReducerStatus>(
                    targetEndpoint, "TargetStatus")
                    == UnitMovementReducerStatus.Accepted
                && ReadValidationProperty<UnitMovementPhase>(
                    targetEndpoint, "TargetPhase")
                    == UnitMovementPhase.NoIntent
                && ReadValidationProperty<bool>(
                    targetEndpoint, "TargetDecisionIsValid")
                && ReadValidationProperty<bool>(
                    targetEndpoint, "TargetAcquirePriority")
                && ReadValidationProperty<UnitMovementIntentReason>(
                    targetEndpoint, "TargetSnapshotIntentReason")
                    == UnitMovementIntentReason.None
                && !ReadValidationProperty<bool>(
                    targetEndpoint, "TargetSnapshotHasIntent")
                && !ReadValidationProperty<bool>(
                    targetEndpoint, "TargetSnapshotDesiredDirectionIsValid"),
                "Endpoint target acquisition must preserve None/false/default input shape.");

            object normalMovement = evaluateEndpoint.Invoke(
                null,
                new object[] { 0f, 0f, 0f, 1f, 0.1f, false });
            Require(
                ReadValidationProperty<UnitMovementReducerStatus>(
                    normalMovement, "Status")
                    == UnitMovementReducerStatus.Accepted
                && ReadValidationProperty<UnitMovementPhase>(
                    normalMovement, "Phase")
                    == UnitMovementPhase.Move
                && !ReadValidationProperty<bool>(
                    normalMovement, "EndpointNormalized")
                && ReadValidationProperty<UnitMovementIntentReason>(
                    normalMovement, "EvaluatedIntentReason")
                    == UnitMovementIntentReason.AStarPath
                && ReadValidationProperty<bool>(
                    normalMovement, "EvaluatedHasIntent")
                && ReadValidationProperty<bool>(
                    normalMovement, "EvaluatedDesiredDirectionIsValid"),
                "Normal movement input shape must remain unchanged.");

            object zeroExpectedFarTarget = evaluateEndpoint.Invoke(
                null,
                new object[] { 0f, 0f, 0f, 1f, 0f, false });
            Require(
                ReadValidationProperty<UnitMovementReducerStatus>(
                    zeroExpectedFarTarget, "Status")
                    == UnitMovementReducerStatus.Accepted
                && ReadValidationProperty<UnitMovementPhase>(
                    zeroExpectedFarTarget, "Phase")
                    == UnitMovementPhase.Move
                && !ReadValidationProperty<bool>(
                    zeroExpectedFarTarget, "EndpointNormalized"),
                "A distant target with zero writer delta must remain a valid movement intent.");

            object toleranceBoundary = evaluateEndpoint.Invoke(
                null,
                new object[]
                {
                    0f, 0f, tolerance, 0f, tolerance, false
                });
            Require(
                ReadValidationProperty<UnitMovementReducerStatus>(
                    toleranceBoundary, "Status")
                    == UnitMovementReducerStatus.Accepted
                && ReadValidationProperty<UnitMovementPhase>(
                    toleranceBoundary, "Phase")
                    == UnitMovementPhase.NoIntent
                && ReadValidationProperty<bool>(
                    toleranceBoundary, "EndpointNormalized"),
                "Both endpoint values exactly at tolerance must normalize.");

            RequireEndpointAdapterInvalid(
                evaluateEndpoint,
                new object[]
                {
                    0f, 0f, tolerance * 0.5f, 0f,
                    tolerance * 2f, false
                },
                "Near endpoint with material expected delta must fail closed.");
            RequireEndpointAdapterInvalid(
                evaluateEndpoint,
                new object[] { 0f, 0f, 0f, 0f, tolerance * 2f, false },
                "Exact endpoint with material expected delta must fail closed.");
            RequireEndpointAdapterInvalid(
                evaluateEndpoint,
                new object[] { 0f, 0f, 0f, 0f, float.NaN, false },
                "NaN expected delta must fail closed.");
            RequireEndpointAdapterInvalid(
                evaluateEndpoint,
                new object[] { 0f, 0f, 0f, 0f, float.PositiveInfinity, false },
                "Infinite expected delta must fail closed.");
            RequireEndpointAdapterInvalid(
                evaluateEndpoint,
                new object[] { 0f, 0f, 0f, 0f, -0.001f, false },
                "Negative expected delta must fail closed.");

            object lifecycle = evaluateLifecycle.Invoke(null, null);
            UnitMovementReducerStatus moveStatus =
                ReadValidationProperty<UnitMovementReducerStatus>(
                    lifecycle, "MoveStatus");
            UnitMovementPhase movePhase =
                ReadValidationProperty<UnitMovementPhase>(
                    lifecycle, "MovePhase");
            UnitMovementReducerStatus endpointStatus =
                ReadValidationProperty<UnitMovementReducerStatus>(
                    lifecycle, "EndpointStatus");
            UnitMovementPhase endpointPhase =
                ReadValidationProperty<UnitMovementPhase>(
                    lifecycle, "EndpointPhase");
            bool endpointNormalized =
                ReadValidationProperty<bool>(
                    lifecycle, "EndpointNormalized");
            UnitMovementReducerStatus duplicateEndpointStatus =
                ReadValidationProperty<UnitMovementReducerStatus>(
                    lifecycle, "DuplicateEndpointStatus");
            UnitMovementReducerStatus priorityStatus =
                ReadValidationProperty<UnitMovementReducerStatus>(
                    lifecycle, "PriorityStatus");
            UnitMovementPhase priorityPhase =
                ReadValidationProperty<UnitMovementPhase>(
                    lifecycle, "PriorityPhase");
            bool priorityIsTargetAcquire =
                ReadValidationProperty<bool>(
                    lifecycle, "PriorityIsTargetAcquire");
            UnitMovementReducerStatus resumeAlignStatus =
                ReadValidationProperty<UnitMovementReducerStatus>(
                    lifecycle, "ResumeAlignStatus");
            UnitMovementPhase resumeAlignPhase =
                ReadValidationProperty<UnitMovementPhase>(
                    lifecycle, "ResumeAlignPhase");
            double resumeAlignYaw =
                ReadValidationProperty<double>(
                    lifecycle, "ResumeAlignYawErrorDegrees");
            UnitMovementReducerStatus resumeMoveStatus =
                ReadValidationProperty<UnitMovementReducerStatus>(
                    lifecycle, "ResumeMoveStatus");
            UnitMovementPhase resumeMovePhase =
                ReadValidationProperty<UnitMovementPhase>(
                    lifecycle, "ResumeMovePhase");
            double resumeMoveYaw =
                ReadValidationProperty<double>(
                    lifecycle, "ResumeMoveYawErrorDegrees");
            ulong revisionAfterMove =
                ReadValidationProperty<ulong>(
                    lifecycle, "RevisionAfterMove");
            ulong revisionAfterEndpoint =
                ReadValidationProperty<ulong>(
                    lifecycle, "RevisionAfterEndpoint");
            ulong revisionAfterDuplicate =
                ReadValidationProperty<ulong>(
                    lifecycle, "RevisionAfterDuplicate");
            ulong revisionAfterPriority =
                ReadValidationProperty<ulong>(
                    lifecycle, "RevisionAfterPriority");
            ulong revisionAfterResumeAlign =
                ReadValidationProperty<ulong>(
                    lifecycle, "RevisionAfterResumeAlign");
            ulong revisionAfterResumeMove =
                ReadValidationProperty<ulong>(
                    lifecycle, "RevisionAfterResumeMove");
            ulong finalCommandRevision =
                ReadValidationProperty<ulong>(
                    lifecycle, "FinalCommandRevision");
            ulong finalSegmentRevision =
                ReadValidationProperty<ulong>(
                    lifecycle, "FinalSegmentRevision");
            string lifecycleValues =
                $"move={moveStatus}/{movePhase}, " +
                $"endpoint={endpointStatus}/{endpointPhase}/normalized={endpointNormalized}, " +
                $"duplicate={duplicateEndpointStatus}, " +
                $"priority={priorityStatus}/{priorityPhase}/acquire={priorityIsTargetAcquire}, " +
                $"resumeAlign={resumeAlignStatus}/{resumeAlignPhase}/yaw={resumeAlignYaw:F9}, " +
                $"resumeMove={resumeMoveStatus}/{resumeMovePhase}/yaw={resumeMoveYaw:F9}, " +
                $"revisions={revisionAfterMove}/{revisionAfterEndpoint}/" +
                $"{revisionAfterDuplicate}/{revisionAfterPriority}/" +
                $"{revisionAfterResumeAlign}/{revisionAfterResumeMove}, " +
                $"scope={finalCommandRevision}/{finalSegmentRevision}";
            Require(
                moveStatus == UnitMovementReducerStatus.Accepted
                && movePhase == UnitMovementPhase.Move
                && endpointStatus == UnitMovementReducerStatus.Accepted
                && endpointPhase == UnitMovementPhase.NoIntent
                && endpointNormalized
                && duplicateEndpointStatus
                    == UnitMovementReducerStatus.Duplicate
                && priorityStatus == UnitMovementReducerStatus.Accepted
                && priorityPhase == UnitMovementPhase.NoIntent
                && priorityIsTargetAcquire
                && resumeAlignStatus
                    == UnitMovementReducerStatus.Accepted
                && resumeAlignPhase == UnitMovementPhase.AlignToMove
                && resumeMoveStatus == UnitMovementReducerStatus.Accepted
                && resumeMovePhase == UnitMovementPhase.Move,
                "Move -> endpoint -> priority -> same-scope resume lifecycle is invalid. "
                + lifecycleValues);
            Require(
                revisionAfterMove == 2UL
                && revisionAfterEndpoint == 3UL
                && revisionAfterDuplicate == 3UL
                && revisionAfterPriority == 4UL
                && revisionAfterResumeAlign == 5UL
                && revisionAfterResumeMove == 6UL
                && finalCommandRevision == 1UL
                && finalSegmentRevision == 1UL
                && Math.Abs(resumeAlignYaw - 12d) < 0.001d
                && Math.Abs(resumeMoveYaw - 9d) < 0.001d,
                "Endpoint lifecycle revisions, scope, or 12-to-9 degree entry restart changed. "
                + lifecycleValues);
        }

        private static void RequireEndpointAdapterInvalid(
            MethodInfo evaluateEndpoint,
            object[] arguments,
            string message)
        {
            object result = evaluateEndpoint.Invoke(null, arguments);
            Require(
                ReadValidationProperty<UnitMovementReducerStatus>(
                    result, "Status")
                    == UnitMovementReducerStatus.InvalidInput
                && !ReadValidationProperty<bool>(
                    result, "DecisionIsValid")
                && !ReadValidationProperty<bool>(
                    result, "EndpointNormalized"),
                message);
        }

        private static T ReadValidationProperty<T>(
            object value,
            string propertyName)
        {
            PropertyInfo property = value.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(property != null,
                $"Validation property {propertyName} is missing.");
            return (T)property.GetValue(value);
        }

        private static Type FindMovementShadowObserverType()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type type = assemblies[index].GetType(
                    "Hexiege.Infrastructure.UnitMovementShadowObserver",
                    throwOnError: false);
                if (type != null)
                    return type;
            }

            throw new InvalidOperationException(
                "B2 movement shadow observer type was not loaded.");
        }

        private static void RequireB2ManifestRejected(
            MethodInfo buildPayloads,
            IReadOnlyList<string> entries,
            string message)
        {
            bool rejected = false;
            try
            {
                buildPayloads.Invoke(
                    null,
                    new object[] { entries });
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is InvalidOperationException)
            {
                rejected = true;
            }

            Require(rejected, message);
        }

        private static List<string> BuildRealMatchMovementManifestFixture(
            int unitTypeCount)
        {
            string[] unitTypes =
            {
                "LittleKnight",
                "Pistoleer",
                "SpearMan",
                "RhinoBreaker",
                "AncientGuardian"
            };
            string[] teams = { "Blue", "Red" };
            string[] reasons =
            {
                "AStarPath",
                "Chase",
                "PendingRepath",
                "PostCombatResume"
            };
            var entries = new List<string>();
            for (int unitIndex = 0; unitIndex < unitTypeCount; unitIndex++)
            {
                for (int teamIndex = 0; teamIndex < teams.Length; teamIndex++)
                {
                    for (int reasonIndex = 0;
                        reasonIndex < reasons.Length;
                        reasonIndex++)
                    {
                        string key =
                            $"{unitTypes[unitIndex]}|{teams[teamIndex]}|" +
                            reasons[reasonIndex];
                        entries.Add("coverage|" + key);
                        entries.Add(
                            $"stats|{key}|frames=31693,align=4364,move=27329," +
                            "targetPriority=107,writerExpectedAdvance=24259," +
                            "writerExpectedNoAdvance=3070,legacyMovedWhileAlign=1294," +
                            "legacyStoppedWhileMove=0");
                        if (reasonIndex != 1)
                            entries.Add("target-priority|" + key);
                    }
                }
            }

            entries.Sort(StringComparer.Ordinal);
            return entries;
        }

        private static ActionDirectionXZ CreateMovementDirection(double yawDegrees)
        {
            double radians = yawDegrees * (Math.PI / 180d);
            Require(ActionDirectionXZ.TryCreate(
                    Math.Sin(radians), Math.Cos(radians),
                    out ActionDirectionXZ direction),
                $"B2 direction fixture at {yawDegrees} degrees must be valid.");
            return direction;
        }

        private static void ValidateAttackHysteresis()
        {
            Require(UnitActionAngleHysteresis.AllowsAttackAlignment(-5d, false),
                "Attack alignment must enter at an absolute error of 5 degrees.");
            Require(!UnitActionAngleHysteresis.AllowsAttackAlignment(5.001d, false),
                "Attack alignment must not enter above 5 degrees.");
            Require(UnitActionAngleHysteresis.AllowsAttackAlignment(-8d, true),
                "Attack alignment must remain active at an absolute error of 8 degrees.");
            Require(!UnitActionAngleHysteresis.AllowsAttackAlignment(8.001d, true),
                "Attack alignment must exit above 8 degrees.");
            Require(!UnitActionAngleHysteresis.AllowsAttackAlignment(double.NaN, true),
                "NaN must fail closed.");
            Require(UnitActionAngleHysteresis.AllowsAttackAlignment(355d, false),
                "355 degrees must normalize to the 5-degree attack entry boundary.");
            Require(UnitActionAngleHysteresis.AllowsAttackAlignment(352d, true),
                "352 degrees must normalize to the 8-degree attack exit boundary.");
            Require(!UnitActionAngleHysteresis.AllowsAttackAlignment(double.NegativeInfinity, true),
                "Infinite attack error must fail closed.");
        }

        private static void ValidateDuplicateAndReorderedResults()
        {
            var instance = new AttackerInstanceId(7UL);
            var sequence = new AttackSequenceId(4UL);
            var buffer = new AttackResultOrderBuffer(instance, sequence, 100d);
            var first = new AttackResultKey(instance, sequence, 0, 1, 30, 2, 0);
            var second = new AttackResultKey(instance, sequence, 1, 1, 30, 2, 0);
            var laterOrdinal = new AttackResultKey(instance, sequence, 1, 1, 30, 2, 1);
            var firstCopy = new AttackResultKey(instance, sequence, 0, 1, 30, 2, 0);
            var otherInstance = new AttackResultKey(new AttackerInstanceId(8UL), sequence, 0, 1, 30, 2, 0);

            Require(first.Equals(firstCopy) && first.GetHashCode() == firstCopy.GetHashCode(),
                "The canonical identity fields must produce equal keys and hashes.");
            Require(!first.Equals(otherInstance) && first.CompareTo(otherInstance) != 0,
                "Attacker instance must participate in identity and ordering.");

            Require(buffer.TryAdd(laterOrdinal, 100.1d) == AttackResultBufferAddStatus.Accepted,
                "First arrival must be accepted.");
            Require(buffer.TryAdd(second, 100.2d) == AttackResultBufferAddStatus.Accepted,
                "Reordered hit must be accepted.");
            Require(buffer.TryAdd(first, 100.3d) == AttackResultBufferAddStatus.Accepted,
                "Earlier reordered hit must be accepted.");
            Require(buffer.TryAdd(second, 100.4d) == AttackResultBufferAddStatus.Duplicate,
                "Duplicate result key must be rejected.");
            Require(buffer.Count == 3, "Duplicate result changed buffer count.");
            Require(buffer.GetAt(0).Equals(first), "First hit was not restored to deterministic order.");
            Require(buffer.GetAt(1).Equals(second), "Second hit was not restored to deterministic order.");
            Require(buffer.GetAt(2).Equals(laterOrdinal), "Result ordinal was not restored to deterministic order.");

            var otherScope = new AttackResultKey(instance, new AttackSequenceId(5UL), 0, 1, 31, 2, 0);
            Require(buffer.TryAdd(otherScope, 100.5d) == AttackResultBufferAddStatus.ScopeMismatch,
                "A result from another attack sequence must be rejected.");
            Require(buffer.TryAdd(otherInstance, 100.5d) == AttackResultBufferAddStatus.ScopeMismatch,
                "A result from another attacker instance must be rejected.");

            buffer.Clear();
            Require(buffer.Count == 0, "Clear must remove all buffered results.");
            Require(buffer.TryAdd(first, 100.6d) == AttackResultBufferAddStatus.Accepted,
                "The fixed scope must remain usable after Clear.");

            var capacityBuffer = new AttackResultOrderBuffer(instance, sequence, 200d);
            for (int ordinal = 0; ordinal < AttackResultOrderBuffer.MaximumResultCount; ordinal++)
            {
                var key = new AttackResultKey(instance, sequence, 0, 1, ordinal, 2, ordinal);
                Require(capacityBuffer.TryAdd(key, 200.5d) == AttackResultBufferAddStatus.Accepted,
                    $"Result {ordinal} within the 64-result limit must be accepted.");
            }

            var overflow = new AttackResultKey(instance, sequence, 0, 1, 999, 2, 64);
            Require(capacityBuffer.TryAdd(overflow, 200.5d) == AttackResultBufferAddStatus.CapacityExceeded,
                "The 65th unique result must be rejected.");

            var expiryBuffer = new AttackResultOrderBuffer(instance, sequence, 300d);
            Require(expiryBuffer.TryAdd(first, 301d) == AttackResultBufferAddStatus.Accepted,
                "A result arriving before expiry must be buffered.");
            Require(expiryBuffer.TryAdd(second, 302d) == AttackResultBufferAddStatus.Expired,
                "A result arriving at the two-second lifetime boundary must be rejected.");
            Require(expiryBuffer.Count == 0,
                "Expiry must discard results that were buffered before the lifetime boundary.");
        }

        /// <summary>
        /// A1에서 추가한 순수 값 계약과 reducer의 핵심 불변을 한 장면이나 네트워크 없이 검증한다.
        /// 실제 피해 성공은 만들지 않고 외부 writer가 돌려준 결과를 확인하는 단계까지만 다룬다.
        /// </summary>
        private static void ValidateA1ContractsAndReducer()
        {
            ValidateA1ValueContractsAndRange();
            ValidateA1ReducerRevisionCommitAndMultiHit();
            ValidateA1ReducerCancellationAndDeath();
            ValidateA1OverflowAndAtomicReset();
            CheckA2PoseContracts();
        }

        /// <summary>A2 pose 값은 Unity 없이 원점·정규화·거리·yaw와 optional 이동 방향을 검증한다.</summary>
        private static void CheckA2PoseContracts()
        {
            var target = new EntityRef(EntityKind.Unit, 0);
            Require(UnitActionPoseSample.TryCreate(
                    target,
                    0d, 0d,
                    0d, 2d,
                    0d, 2d,
                    false, double.NaN, double.NaN,
                    out UnitActionPoseSample withoutDesired),
                "World origin attacker and optional missing desired direction must remain valid.");
            Require(withoutDesired.IsValid
                && !withoutDesired.HasDesiredMoveDirection
                && !withoutDesired.DesiredMoveDirection.IsValid
                && Math.Abs(withoutDesired.SimulationFacing.Z - 1d) < 0.000001d
                && Math.Abs(withoutDesired.TargetAimDirection.Z - 1d) < 0.000001d
                && Math.Abs(withoutDesired.TargetSquaredDistance - 4d) < 0.000001d
                && Math.Abs(withoutDesired.FacingToAimYawDegrees) < 0.000001d,
                "Pose must normalize facing/aim and calculate squared distance and zero yaw.");

            Require(UnitActionPoseSample.TryCreate(
                    target,
                    0d, 0d,
                    0d, 1d,
                    2d, 0d,
                    true, 3d, 4d,
                    out UnitActionPoseSample withDesired),
                "Finite pose with an explicit desired move target must be valid.");
            Require(withDesired.HasDesiredMoveDirection
                && Math.Abs(withDesired.DesiredMoveDirection.X - 0.6d) < 0.000001d
                && Math.Abs(withDesired.DesiredMoveDirection.Z - 0.8d) < 0.000001d
                && Math.Abs(withDesired.FacingToAimYawDegrees - 90d) < 0.000001d
                && Math.Abs(withDesired.TargetSquaredDistance - 4d) < 0.000001d,
                "Desired direction must normalize and facing-to-aim yaw must be 90 degrees.");

            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 0d, 1d, 0d, false, 0d, 0d, out _),
                "Zero facing must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 0d, 0d, false, 0d, 0d, out _),
                "A target at the attacker XZ must fail closed because aim is undefined.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, double.NaN, 1d, false, 0d, 0d, out _),
                "NaN target position must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 1d, 0d, true, 0d, 0d, out _),
                "An explicitly present zero desired direction must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    EntityRef.None, 0d, 0d, 0d, 1d, 1d, 0d, false, 0d, 0d, out _)
                && !UnitActionPoseSample.TryCreate(
                    new EntityRef((EntityKind)999, 1), 0d, 0d, 0d, 1d, 1d, 0d, false, 0d, 0d, out _),
                "None and unknown target kinds must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, double.NaN, 0d, 0d, 1d, 1d, 0d, false, 0d, 0d, out _)
                && !UnitActionPoseSample.TryCreate(
                    target, double.PositiveInfinity, 0d, 0d, 1d, 1d, 0d, false, 0d, 0d, out _),
                "NaN and infinite attacker coordinates must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 1e200d, 1e200d, false, 0d, 0d, out _),
                "Squared target distance overflow must fail closed.");
            Require(!UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 1d, 0d, true, double.NaN, 1d, out _)
                && !UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 1d, 0d, true, double.PositiveInfinity, 1d, out _),
                "Present desired target NaN and infinity must fail closed.");
            Require(UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 0d, 1d, 0d, -2d, false, 0d, 0d,
                    out UnitActionPoseSample opposite)
                && Math.Abs(opposite.FacingToAimYawDegrees - 180d) < 0.000001d,
                "Opposite facing-to-aim yaw must be 180 degrees.");
        }

        private static void ValidateA1ValueContractsAndRange()
        {
            var zeroIdUnit = new EntityRef(EntityKind.Unit, 0);
            Require(zeroIdUnit.IsValid && !zeroIdUnit.IsNone, "Entity ID 0 must remain valid.");
            Require(EntityRef.None.IsNone && !EntityRef.None.IsValid, "None must be represented by EntityKind.None.");

            Require(!ActionDirectionXZ.TryCreate(0d, 0d, out _), "Zero direction must be rejected.");
            Require(!ActionDirectionXZ.TryCreate(double.NaN, 1d, out _), "NaN direction must be rejected.");
            Require(!ActionDirectionXZ.TryCreate(double.PositiveInfinity, 1d, out _), "Infinite direction must be rejected.");
            Require(ActionDirectionXZ.TryCreate(3d, 4d, out ActionDirectionXZ direction),
                "Finite non-zero direction must be created.");
            Require(Math.Abs(direction.X - 0.6d) < 0.000001d && Math.Abs(direction.Z - 0.8d) < 0.000001d,
                "Direction must be normalized.");
            Require(WorldPointXZ.TryCreate(0d, 0d, out WorldPointXZ origin) && origin.IsValid,
                "World origin must be a valid point.");
            Require(!WorldPointXZ.TryCreate(double.NaN, 0d, out _), "NaN point must be rejected.");

            double[] sourceOffsets = { 0.2d, 0.8d };
            Require(AttackTimelinePlan.TryCreate(2d, 1.2d, sourceOffsets, out AttackTimelinePlan timeline),
                "A valid multi-hit timeline must be created.");
            sourceOffsets[0] = 1.5d;
            Require(Math.Abs(timeline.GetImpactOffset(0) - 0.2d) < 0.000001d,
                "Timeline must defensively copy marker offsets.");
            Require(!AttackTimelinePlan.TryCreate(2d, 1.2d, new[] { 0.8d, 0.8d }, out _),
                "Duplicate timeline offsets must be rejected.");
            Require(!AttackTimelinePlan.TryCreate(2d, 1.2d, new[] { 0.8d, 0.2d }, out _),
                "Descending timeline offsets must be rejected.");
            Require(!AttackTimelinePlan.TryCreate(1d, 1d, new[] { 1d }, out _),
                "A marker at the cooldown boundary must be rejected.");
            Require(!AttackTimelinePlan.TryCreate(2d, 0.7d, new[] { 0.8d }, out _),
                "Recovery before the last marker must be rejected.");

            Require(AttackRangeProfile.TryCreate(0.5d, 1d, out AttackRangeProfile melee),
                "The v2 melee range profile must be created.");
            Require(melee.UsesMeleeContact, "AttackRange 0.5 must use v2 MeleeContact.");
            Require(Math.Abs(melee.GetMaximumDistance(EntityKind.Unit) - 0.68d) < 0.000001d,
                "The v2 unit melee boundary must be 0.63 + 0.05.");
            Require(Math.Abs(melee.GetMaximumDistance(EntityKind.Building) - 0.88d) < 0.000001d,
                "The v2 building melee boundary must also include radius 0.20.");
            Require(melee.ContainsSquaredDistance(0.50d * 0.50d, EntityKind.Unit)
                && !(0.50d * 0.50d <= 0.35d * 0.35d),
                "Distance 0.50 must expose v2 true versus Legacy unit 0.35 false.");
            Require(melee.ContainsSquaredDistance(0.70d * 0.70d, EntityKind.Building)
                && !(0.70d * 0.70d <= 0.55d * 0.55d),
                "Distance 0.70 must expose v2 true versus Legacy building 0.55 false.");
            Require(AttackRangeProfile.TryCreate(0.5001d, 1d, out AttackRangeProfile classifierConflict)
                && !classifierConflict.UsesMeleeContact && 0.5001d < 1d,
                "AttackRange 0.5001 must expose the v2 <=0.5 versus Legacy <1 classifier conflict.");
            Require(AttackRangeProfile.TryCreate(1d, 1d, out AttackRangeProfile spearRange)
                && !spearRange.UsesMeleeContact
                && Math.Abs(spearRange.GetMaximumDistance(EntityKind.Unit) - 1.05d) < 0.000001d,
                "Actual SpearMan AttackRange 1 must use the scaled-center v2 range mode.");
            Require(!AttackRangeProfile.TryCreate(double.MaxValue, 2d, out _),
                "Range multiplication overflow must fail closed.");
            Require(!AttackRangeProfile.TryCreate(1e200d, 1d, out _),
                "Range squared-distance overflow must fail closed.");

            var unsupported = new UnitActionSequencer(new AttackerInstanceId(100UL), 0);
            var target = new AttackTargetBinding(AttackTargetMode.TargetLocked, zeroIdUnit);
            UnitActionSnapshot initial = unsupported.Snapshot;
            Require(unsupported.BeginAttackAlignment(
                    initial.Revision, target, AttackDeliveryKind.ProjectileImpact,
                    timeline, melee, direction, 1d) == UnitActionReducerStatus.UnsupportedDelivery,
                "A1 reducer must reject unsupported ProjectileImpact delivery.");
            Require(unsupported.Snapshot.Revision == initial.Revision && initial.Phase == UnitActionPhase.Idle,
                "Rejected delivery must not mutate the immutable initial snapshot.");

            var lockedPoint = new AttackTargetBinding(AttackTargetMode.LockedPoint, zeroIdUnit);
            var homing = new AttackTargetBinding(AttackTargetMode.Homing, zeroIdUnit);
            Require(lockedPoint.IsValid && homing.IsValid,
                "Future target binding vocabulary must remain representable by the contract.");
            Require(unsupported.BeginAttackAlignment(
                    initial.Revision, lockedPoint, AttackDeliveryKind.MeleeContact,
                    timeline, melee, direction, 1d) == UnitActionReducerStatus.UnsupportedTargetBinding,
                "A1 reducer must explicitly reject LockedPoint binding.");
            Require(unsupported.BeginAttackAlignment(
                    initial.Revision, homing, AttackDeliveryKind.MeleeContact,
                    timeline, melee, direction, 1d) == UnitActionReducerStatus.UnsupportedTargetBinding,
                "A1 reducer must explicitly reject Homing binding.");
        }

        private static void ValidateA1ReducerRevisionCommitAndMultiHit()
        {
            Require(ActionDirectionXZ.TryCreate(1d, 0d, out ActionDirectionXZ facing),
                "Test facing must be valid.");
            Require(AttackTimelinePlan.TryCreate(2d, 1.2d, new[] { 0.2d, 0.8d }, out AttackTimelinePlan timeline),
                "Test timeline must be valid.");
            Require(AttackRangeProfile.TryCreate(0.5d, 1d, out AttackRangeProfile range),
                "Test range must be valid.");

            var target = new AttackTargetBinding(
                AttackTargetMode.TargetLocked, new EntityRef(EntityKind.Unit, 0));
            var otherTarget = new AttackTargetBinding(
                AttackTargetMode.TargetLocked, new EntityRef(EntityKind.Unit, 1));
            var reducer = new UnitActionSequencer(new AttackerInstanceId(200UL), 0);
            UnitActionSnapshot initial = reducer.Snapshot;

            Require(initial.Revision == 1UL && initial.LastConfirmedHitIndex == -1,
                "Initial revision must be 1 and confirmed prefix must be -1.");
            Require(reducer.BeginAttackAlignment(
                    initial.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 10d) == UnitActionReducerStatus.Accepted,
                "Alignment must be accepted.");
            UnitActionSnapshot aligned = reducer.Snapshot;
            Require(aligned.Revision == 2UL && aligned.Phase == UnitActionPhase.AlignToAttack,
                "Accepted alignment must increment revision exactly once.");
            Require(initial.Revision == 1UL && initial.Phase == UnitActionPhase.Idle,
                "Previously captured snapshot must remain immutable.");

            Require(reducer.BeginAttackAlignment(
                    1UL, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 10d) == UnitActionReducerStatus.StaleRevision,
                "Stale revision must fail closed.");
            Require(reducer.Advance(aligned.Revision, 10d) == UnitActionReducerStatus.NoChange
                && reducer.Snapshot.Revision == aligned.Revision,
                "No-op must not increment revision.");
            Require(reducer.Advance(aligned.Revision, 9.9d) == UnitActionReducerStatus.InvalidInput,
                "Backward observed time must fail closed.");
            Require(reducer.Advance(aligned.Revision, double.NaN) == UnitActionReducerStatus.InvalidInput,
                "NaN observed time must fail closed.");
            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, double.NaN, 10d, 10.1d)
                == UnitActionReducerStatus.InvalidInput,
                "NaN yaw must fail closed.");
            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, 5d, 10d, double.PositiveInfinity)
                == UnitActionReducerStatus.InvalidInput,
                "Infinite observed time must fail closed.");
            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, 5d, 9.9d, 10.1d)
                == UnitActionReducerStatus.InvalidInput,
                "Commit time before last accepted time must fail closed.");
            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, 5d, 10.2d, 10.1d)
                == UnitActionReducerStatus.InvalidInput,
                "Commit time after observed now must fail closed.");
            Require(reducer.CommitAttack(
                    aligned.Revision, otherTarget, true, true, true, 0.25d, 5d, 10d, 10.1d)
                == UnitActionReducerStatus.ScopeMismatch,
                "Commit must not replace the aligned target.");
            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, 5.001d, 10d, 10.1d)
                == UnitActionReducerStatus.InvalidInput,
                "Commit above the 5-degree boundary must fail.");
            Require(reducer.Snapshot.Revision == aligned.Revision,
                "Rejected commit attempts must not mutate revision.");

            Require(reducer.CommitAttack(
                    aligned.Revision, target, true, true, true, 0.25d, 5d, 10d, 10.1d)
                == UnitActionReducerStatus.Accepted,
                "Commit at the 5-degree boundary must succeed.");
            UnitActionSnapshot committed = reducer.Snapshot;
            Require(committed.SequenceId.IsValid && committed.CooldownConsumed
                && committed.TargetBinding == target && committed.Phase == UnitActionPhase.Windup,
                "Commit must consume cooldown, allocate one sequence, and lock the target.");

            Require(reducer.Advance(committed.Revision, 10.9d) == UnitActionReducerStatus.Accepted,
                "A time leap must mark every elapsed hit due in one revision.");
            UnitActionSnapshot due = reducer.Snapshot;
            Require(due.DueHitMask == 3UL, "Both multi-hit markers must become due.");

            Require(reducer.EvaluateImpact(
                    due.Revision, 0, 7, 11, otherTarget, true, true, 0.25d, 8d, facing, 10.91d, out _)
                == UnitActionReducerStatus.ScopeMismatch
                && reducer.Snapshot.TargetBinding == target,
                "Committed TargetLocked binding must reject target transfer without mutation.");

            Require(reducer.EvaluateImpact(
                    due.Revision, 1, 8, 12, target, true, true, 0.25d, 8d, facing, 10.91d, out _)
                == UnitActionReducerStatus.OutOfOrder,
                "Due hits must be decided in HitIndex order.");
            Require(reducer.EvaluateImpact(
                    due.Revision, 0, 7, 11, target, true, true, 0.25d, 8d, facing, 10.91d,
                    out ImpactAuthorization hitAuthorization)
                == UnitActionReducerStatus.Accepted
                && hitAuthorization.Outcome == ImpactAuthorizationOutcome.AuthorizedHit
                && hitAuthorization.Key.Equals(new AttackResultKey(
                    committed.AttackerInstanceId, committed.SequenceId, 0,
                    (int)EntityKind.Unit, 0, 7, 11)),
                "Impact at the 8-degree boundary must authorize a hit.");
            UnitActionSnapshot hitZeroDecided = reducer.Snapshot;
            Require(reducer.EvaluateImpact(
                    hitZeroDecided.Revision, 0, 7, 11, target, true, true, 0.25d, 8d, facing, 10.92d, out _)
                == UnitActionReducerStatus.Duplicate,
                "The same hit must not be authorized twice.");
            Require(reducer.EvaluateImpact(
                    hitZeroDecided.Revision, 1, 8, 12, target, true, true, 0.25d, 8.001d, facing, 10.92d,
                    out ImpactAuthorization missAuthorization)
                == UnitActionReducerStatus.Accepted
                && missAuthorization.Outcome == ImpactAuthorizationOutcome.AuthorizedMiss
                && missAuthorization.MissReason
                    == ImpactAuthorizationMissReason.CombatConditionFailed,
                "A later hit above 8 degrees must miss independently.");
            UnitActionSnapshot bothDecided = reducer.Snapshot;
            Require(bothDecided.Phase == UnitActionPhase.Recovery && bothDecided.DecidedHitMask == 3UL,
                "All independently decided hits must enter Recovery.");

            var hitZeroKey = new AttackResultKey(
                committed.AttackerInstanceId, committed.SequenceId, 0,
                (int)EntityKind.Unit, 0, 7, 11);
            var hitOneKey = new AttackResultKey(
                committed.AttackerInstanceId, committed.SequenceId, 1,
                (int)EntityKind.Unit, 0, 8, 12);
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.HitApplied, 10, 40, out AttackImpactResult hitResult),
                "External writer must be able to create a valid applied result.");
            Require(AttackImpactResult.TryCreate(
                    missAuthorization.ActionRevision, hitOneKey, 10.8d, facing, false, default,
                    AttackImpactOutcome.Miss, 0, -1, out AttackImpactResult missResult),
                "External writer must be able to create a valid miss result.");
            Require(!AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.HitApplied, 0, 40, out _),
                "A successful result without applied amount must be rejected.");
            var invalidKey = new AttackResultKey(
                committed.AttackerInstanceId, committed.SequenceId, 0, 0, 0, 0, 0);
            Require(!AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, invalidKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.Miss, 0, -1, out _),
                "AttackImpactResult must reject an invalid canonical key.");
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.Evaded, 0, -1, out _),
                "Evaded outcome with zero amount must be valid.");
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.Immune, 0, -1, out _),
                "Immune outcome with zero amount must be valid.");
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.StatusEffectApplied, 0, 40, out _),
                "A status effect may be applied without direct damage.");
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.Cancelled, 0, -1, out _),
                "Cancelled outcome with zero amount must remain representable.");
            Require(!AttackImpactResult.TryCreate(
                    0UL, hitZeroKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.Evaded, 0, -1, out _),
                "Result action revision 0 must be rejected.");

            var wrongEffectKey = new AttackResultKey(
                committed.AttackerInstanceId, committed.SequenceId, 0,
                (int)EntityKind.Unit, 0, 70, 11);
            var wrongOrdinalKey = new AttackResultKey(
                committed.AttackerInstanceId, committed.SequenceId, 0,
                (int)EntityKind.Unit, 0, 7, 110);
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, wrongEffectKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.HitApplied, 10, 40, out AttackImpactResult wrongEffectResult),
                "EffectKind mutation fixture must remain a structurally valid result.");
            Require(AttackImpactResult.TryCreate(
                    hitAuthorization.ActionRevision, wrongOrdinalKey, 10.2d, facing, false, default,
                    AttackImpactOutcome.HitApplied, 10, 40, out AttackImpactResult wrongOrdinalResult),
                "ResultOrdinal mutation fixture must remain a structurally valid result.");
            Require(reducer.ConfirmImpactResult(bothDecided.Revision, wrongEffectResult, 10.95d)
                == UnitActionReducerStatus.ScopeMismatch
                && reducer.Snapshot.Revision == bothDecided.Revision,
                "A result with the wrong EffectKind must be rejected without mutation.");
            Require(reducer.ConfirmImpactResult(bothDecided.Revision, wrongOrdinalResult, 10.96d)
                == UnitActionReducerStatus.ScopeMismatch
                && reducer.Snapshot.Revision == bothDecided.Revision,
                "A result with the wrong ResultOrdinal must be rejected without mutation.");

            Require(reducer.ConfirmImpactResult(bothDecided.Revision, missResult, 11d)
                == UnitActionReducerStatus.Accepted && reducer.Snapshot.LastConfirmedHitIndex == -1,
                "Confirming hit 1 first must keep the contiguous prefix at -1.");
            UnitActionSnapshot hitOneConfirmed = reducer.Snapshot;
            Require(reducer.ConfirmImpactResult(hitOneConfirmed.Revision, hitResult, 11.1d)
                == UnitActionReducerStatus.Accepted && reducer.Snapshot.LastConfirmedHitIndex == 1,
                "Confirming hit 0 later must advance the contiguous prefix through hit 1.");
            UnitActionSnapshot confirmed = reducer.Snapshot;
            Require(reducer.ConfirmImpactResult(confirmed.Revision, hitResult, 11.2d)
                == UnitActionReducerStatus.Duplicate,
                "A confirmed result must be idempotent.");
            Require(reducer.Advance(confirmed.Revision, 11.9d) == UnitActionReducerStatus.NoChange,
                "Recovery must wait for the later of cooldown and recovery end.");
            Require(reducer.Advance(confirmed.Revision, 12d) == UnitActionReducerStatus.Accepted
                && reducer.Snapshot.Phase == UnitActionPhase.AcquireTarget,
                "Recovery must finish exactly at max(cooldownEnd,recoveryEnd).");
            UnitActionSnapshot completed = reducer.Snapshot;
            Require(completed.TargetBinding == AttackTargetBinding.None
                && completed.Delivery == AttackDeliveryKind.None
                && !completed.SequenceId.IsValid
                && completed.Timeline == null
                && !completed.RangeProfile.IsValid
                && !completed.SimulationFacing.IsValid
                && !completed.HasSimulationAimDirection
                && !completed.SimulationAimDirection.IsValid,
                "Action end must clear target, plan, sequence, facing, and aim atomically.");
            Require(completed.StartServerTime == 0d
                && completed.CommitServerTime == 0d
                && completed.CooldownEndServerTime == 0d
                && completed.RecoveryEndServerTime == 0d
                && !completed.CooldownConsumed
                && completed.DueHitMask == 0UL
                && completed.DecidedHitMask == 0UL
                && completed.ConfirmedHitMask == 0UL
                && completed.LastConfirmedHitIndex == -1
                && completed.PhaseStartServerTime == 12d,
                "Action end must clear all timing and hit-cycle data while recording the new phase start.");
            Require(reducer.BeginAttackAlignment(
                    completed.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 12.1d) == UnitActionReducerStatus.Accepted,
                "A completed reducer must start the next alignment.");
            UnitActionSnapshot nextAlignment = reducer.Snapshot;
            Require(nextAlignment.Phase == UnitActionPhase.AlignToAttack
                && nextAlignment.StartServerTime == 12.1d
                && nextAlignment.CommitServerTime == 0d
                && nextAlignment.DueHitMask == 0UL
                && nextAlignment.DecidedHitMask == 0UL
                && nextAlignment.ConfirmedHitMask == 0UL
                && nextAlignment.LastConfirmedHitIndex == -1
                && !nextAlignment.HasSimulationAimDirection,
                "The next Align must not inherit any timing, hit, or aim state from the old cycle.");
        }

        private static void ValidateA1ReducerCancellationAndDeath()
        {
            Require(ActionDirectionXZ.TryCreate(1d, 0d, out ActionDirectionXZ facing),
                "Test facing must be valid.");
            Require(AttackTimelinePlan.TryCreate(2d, 1.2d, new[] { 0.2d, 0.8d }, out AttackTimelinePlan timeline),
                "Test timeline must be valid.");
            Require(AttackRangeProfile.TryCreate(0.5d, 1d, out AttackRangeProfile range),
                "Test range must be valid.");
            var target = new AttackTargetBinding(
                AttackTargetMode.TargetLocked, new EntityRef(EntityKind.Unit, 3));

            var preCommit = new UnitActionSequencer(new AttackerInstanceId(300UL), 3);
            Require(preCommit.BeginAttackAlignment(
                    preCommit.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 1d) == UnitActionReducerStatus.Accepted,
                "Pre-commit alignment must start.");
            UnitActionSnapshot beforeUnknownCancel = preCommit.Snapshot;
            Require(preCommit.CancelPreCommit(
                    beforeUnknownCancel.Revision, (PreCommitCancelReason)999, true, 1.05d)
                == UnitActionReducerStatus.InvalidInput,
                "Unknown pre-commit cancellation reason must fail closed.");
            Require(preCommit.Snapshot.Revision == beforeUnknownCancel.Revision
                && preCommit.Snapshot.Phase == beforeUnknownCancel.Phase
                && preCommit.Snapshot.TargetBinding == beforeUnknownCancel.TargetBinding,
                "Unknown cancellation reason must not mutate state.");
            Require(preCommit.CancelPreCommit(
                    preCommit.Snapshot.Revision, PreCommitCancelReason.AttackRangeExited, true, 1.1d)
                == UnitActionReducerStatus.Accepted,
                "AttackRange exit inside LoseRange must enter Chase.");
            Require(preCommit.Snapshot.Phase == UnitActionPhase.Chase
                && preCommit.Snapshot.TargetBinding == target
                && !preCommit.Snapshot.SequenceId.IsValid
                && !preCommit.Snapshot.CooldownConsumed,
                "Pre-commit Chase must retain target without sequence or cooldown.");
            Require(preCommit.BeginAttackAlignment(
                    preCommit.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 1.2d) == UnitActionReducerStatus.Accepted,
                "Chase must be able to re-enter alignment.");
            Require(preCommit.CancelPreCommit(
                    preCommit.Snapshot.Revision, PreCommitCancelReason.TargetDead, false, 1.3d)
                == UnitActionReducerStatus.Accepted,
                "Dead target must cancel pre-commit action.");
            Require(preCommit.Snapshot.Phase == UnitActionPhase.AcquireTarget
                && preCommit.Snapshot.TargetBinding == AttackTargetBinding.None
                && !preCommit.Snapshot.CooldownConsumed,
                "Invalid pre-commit target must clear without refund concerns.");

            var committedCancel = new UnitActionSequencer(new AttackerInstanceId(301UL), 3);
            Require(committedCancel.BeginAttackAlignment(
                    committedCancel.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 20d) == UnitActionReducerStatus.Accepted,
                "Committed-cancel fixture must align.");
            Require(committedCancel.CommitAttack(
                    committedCancel.Snapshot.Revision, target, true, true, true,
                    0.25d, 5d, 20d, 20.1d) == UnitActionReducerStatus.Accepted,
                "Committed-cancel fixture must commit.");
            AttackSequenceId committedSequence = committedCancel.Snapshot.SequenceId;
            Require(committedCancel.CancelCommitted(committedCancel.Snapshot.Revision, 20.2d)
                == UnitActionReducerStatus.Accepted,
                "Committed action must support future-hit cancellation.");
            Require(committedCancel.Snapshot.Phase == UnitActionPhase.Recovery
                && committedCancel.Snapshot.CooldownConsumed
                && committedCancel.Snapshot.SequenceId == committedSequence
                && committedCancel.Snapshot.DecidedHitMask == timeline.AllImpactMask,
                "Committed cancellation must suppress future hits without refund or target transfer.");

            var dead = new UnitActionSequencer(new AttackerInstanceId(302UL), 3);
            Require(dead.BeginAttackAlignment(
                    dead.Snapshot.Revision, target, AttackDeliveryKind.Hitscan,
                    timeline, range, facing, 30d) == UnitActionReducerStatus.Accepted,
                "Dead fixture must align.");
            Require(dead.CommitAttack(
                    dead.Snapshot.Revision, target, true, true, true,
                    0.25d, 5d, 30d, 30.1d) == UnitActionReducerStatus.Accepted,
                "Dead fixture must commit.");
            UnitActionSnapshot deadCommitted = dead.Snapshot;
            Require(dead.Advance(deadCommitted.Revision, 30.2d) == UnitActionReducerStatus.Accepted,
                "Dead fixture hit 0 must become due before death.");
            Require(dead.EvaluateImpact(
                    dead.Snapshot.Revision, 0, 9, 13, target, true, true, 0.25d, 0d,
                    facing, 30.21d, out ImpactAuthorization preDeathAuthorization)
                == UnitActionReducerStatus.Accepted,
                "Hit authorized before death must be retained.");
            var preDeathKey = new AttackResultKey(
                deadCommitted.AttackerInstanceId, deadCommitted.SequenceId, 0,
                (int)EntityKind.Unit, 3, 9, 13);
            Require(AttackImpactResult.TryCreate(
                    preDeathAuthorization.ActionRevision, preDeathKey, 30.21d,
                    facing, false, default, AttackImpactOutcome.HitApplied, 5, 45,
                    out AttackImpactResult preDeathResult),
                "Exact pre-death authorization result must be constructible.");
            Require(dead.MarkDead(dead.Snapshot.Revision) == UnitActionReducerStatus.Accepted,
                "MarkDead must be accepted without a time input.");
            UnitActionSnapshot deadSnapshot = dead.Snapshot;
            Require(deadSnapshot.Phase == UnitActionPhase.Dead
                && deadSnapshot.EndReason == UnitActionEndReason.AttackerDead
                && deadSnapshot.TargetBinding == target,
                "Dead must be a terminal snapshot with an explicit reason.");
            Require(dead.ConfirmImpactResult(deadSnapshot.Revision, preDeathResult, 30.3d)
                == UnitActionReducerStatus.Accepted,
                "An exact result authorized before death must still be confirmed after death.");
            var futureKey = new AttackResultKey(
                deadCommitted.AttackerInstanceId, deadCommitted.SequenceId, 1,
                (int)EntityKind.Unit, 3, 9, 14);
            Require(AttackImpactResult.TryCreate(
                    preDeathAuthorization.ActionRevision, futureKey, 30.3d,
                    facing, false, default, AttackImpactOutcome.Miss, 0, -1,
                    out AttackImpactResult unauthorizedFutureResult),
                "Future result fixture must be structurally valid.");
            Require(dead.ConfirmImpactResult(dead.Snapshot.Revision, unauthorizedFutureResult, 30.4d)
                == UnitActionReducerStatus.NotDue,
                "Death must reject a future hit that never received authorization.");
            deadSnapshot = dead.Snapshot;
            Require(dead.Advance(deadSnapshot.Revision, double.NaN) == UnitActionReducerStatus.DeadTerminal,
                "Dead terminal must reject later reducer operations before reading time.");
            Require(dead.MarkDead(deadSnapshot.Revision) == UnitActionReducerStatus.NoChange
                && dead.Snapshot.Revision == deadSnapshot.Revision,
                "Repeated MarkDead must be a no-op without revision growth.");
        }

        private static void ValidateA1OverflowAndAtomicReset()
        {
            Require(ActionDirectionXZ.TryCreate(1d, 0d, out ActionDirectionXZ facing),
                "Overflow fixture facing must be valid.");
            Require(AttackTimelinePlan.TryCreate(2d, 1.2d, new[] { 0.2d }, out AttackTimelinePlan timeline),
                "Overflow fixture timeline must be valid.");
            Require(AttackRangeProfile.TryCreate(0.5d, 1d, out AttackRangeProfile range),
                "Overflow fixture range must be valid.");
            var target = new AttackTargetBinding(
                AttackTargetMode.TargetLocked, new EntityRef(EntityKind.Unit, 8));

            var revisionExhausted = UnitActionSequencer.CreateForValidation(
                new AttackerInstanceId(400UL), 8, ulong.MaxValue, 0UL);
            UnitActionSnapshot revisionBefore = revisionExhausted.Snapshot;
            Require(revisionExhausted.BeginAttackAlignment(
                    revisionBefore.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 1d) == UnitActionReducerStatus.Exhausted,
                "Revision maximum must fail closed instead of wrapping.");
            Require(revisionExhausted.Snapshot.Revision == revisionBefore.Revision
                && revisionExhausted.Snapshot.Phase == revisionBefore.Phase,
                "Revision exhaustion must leave state unchanged.");

            var sequenceExhausted = UnitActionSequencer.CreateForValidation(
                new AttackerInstanceId(401UL), 8, 1UL, ulong.MaxValue);
            Require(sequenceExhausted.BeginAttackAlignment(
                    sequenceExhausted.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 2d) == UnitActionReducerStatus.Accepted,
                "Sequence exhaustion fixture must align.");
            UnitActionSnapshot sequenceBeforeCommit = sequenceExhausted.Snapshot;
            Require(sequenceExhausted.CommitAttack(
                    sequenceBeforeCommit.Revision, target, true, true, true,
                    0.25d, 0d, 2d, 2.1d) == UnitActionReducerStatus.Exhausted,
                "Sequence maximum must fail closed instead of throwing or wrapping.");
            UnitActionSnapshot sequenceAfterCommit = sequenceExhausted.Snapshot;
            Require(sequenceAfterCommit.Revision == sequenceBeforeCommit.Revision
                && sequenceAfterCommit.Phase == sequenceBeforeCommit.Phase
                && !sequenceAfterCommit.SequenceId.IsValid
                && !sequenceAfterCommit.CooldownConsumed,
                "Sequence exhaustion must leave the aligned action unchanged.");

            var allocator = new AttackSequenceAllocator(ulong.MaxValue);
            Require(!allocator.TryNext(new AttackerInstanceId(402UL), out AttackSequenceId exhaustedId)
                && !exhaustedId.IsValid,
                "Allocator TryNext must expose exhaustion without an exception.");

            Require(!UnitActionSequencer.TryCreateShadowCycle(
                    AttackerInstanceId.None, 8, new AttackSequenceId(1UL), out _)
                && !UnitActionSequencer.TryCreateShadowCycle(
                    new AttackerInstanceId(403UL), -1, new AttackSequenceId(1UL), out _)
                && !UnitActionSequencer.TryCreateShadowCycle(
                    new AttackerInstanceId(403UL), 8, AttackSequenceId.None, out _),
                "Runtime shadow factory must reject every invalid identity input.");
            Require(UnitActionSequencer.TryCreateShadowCycle(
                    new AttackerInstanceId(404UL), 8, new AttackSequenceId(1UL), out UnitActionSequencer firstCycle)
                && firstCycle.BeginAttackAlignment(
                    firstCycle.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 3d) == UnitActionReducerStatus.Accepted
                && firstCycle.CommitAttack(
                    firstCycle.Snapshot.Revision, target, true, true, true,
                    0.25d, 0d, 3d, 3d) == UnitActionReducerStatus.Accepted
                && firstCycle.Snapshot.SequenceId.Value == 1UL,
                "Runtime shadow factory must allocate exact sequence 1.");
            Require(UnitActionSequencer.TryCreateShadowCycle(
                    new AttackerInstanceId(405UL), 8, new AttackSequenceId(ulong.MaxValue),
                    out UnitActionSequencer maximumCycle)
                && maximumCycle.BeginAttackAlignment(
                    maximumCycle.Snapshot.Revision, target, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 4d) == UnitActionReducerStatus.Accepted
                && maximumCycle.CommitAttack(
                    maximumCycle.Snapshot.Revision, target, true, true, true,
                    0.25d, 0d, 4d, 4d) == UnitActionReducerStatus.Accepted
                && maximumCycle.Snapshot.SequenceId.Value == ulong.MaxValue,
                "Runtime shadow factory must allocate exact maximum sequence without overflow.");
        }

        /// <summary>
        /// QuakeSpirit을 Supported로 분류하기 전에 실제 게임이 읽는 설정과 Animator 연결을 확인한다.
        /// 테스트용으로 만든 타임라인이 아니라 production 에셋 자체를 읽으므로, 설정이나 이벤트가
        /// 다시 과거 placeholder로 돌아가면 전체 self-validation이 즉시 실패한다.
        /// </summary>
        private static void ValidateQuakeSpiritProductionTimeline()
        {
            const string configPath = "Assets/_Project/Resources/Config/UnitStatsConfig.asset";
            const string controllerPath = "Assets/_Project/Animations/Units/QuakeSpirit/QuakeSpirit.controller";
            const string clipPath = "Assets/_Project/Animations/Units/QuakeSpirit/QuakeSpirit_Attack.anim";
            const string bluePrefabPath = "Assets/_Project/Prefabs/Units/Spirit/Unit_QuakeSpirit_Blue.prefab";
            const string redPrefabPath = "Assets/_Project/Prefabs/Units/Spirit/Unit_QuakeSpirit_Red.prefab";
            const float impactTime = 1.667f;
            const float tolerance = 0.0001f;

            UnitStatsConfig config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(configPath);
            Require(config != null, "QuakeSpirit production UnitStatsConfig asset must exist.");

            int quakeRows = 0;
            UnitStatEntry quakeEntry = default;
            IReadOnlyList<UnitStatEntry> stats = config.Stats;
            for (int index = 0; index < stats.Count; index++)
            {
                if (stats[index].unitType != UnitType.QuakeSpirit) continue;
                quakeRows++;
                quakeEntry = stats[index];
            }
            Require(quakeRows == 1,
                "UnitStatsConfig must contain exactly one QuakeSpirit entry.");
            Require(quakeEntry.hitFrameTimes != null
                && quakeEntry.hitFrameTimes.Length == 1
                && Mathf.Abs(quakeEntry.hitFrameTimes[0] - impactTime) <= tolerance,
                "QuakeSpirit runtime hitFrameTimes must contain only the verified 1.667-second impact.");

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            Require(controller != null && attackClip != null,
                "QuakeSpirit production AnimatorController and Attack clip must exist.");

            int baseLayerCount = 0;
            AnimatorControllerLayer baseLayer = default;
            AnimatorControllerLayer[] layers = controller.layers;
            for (int index = 0; index < layers.Length; index++)
            {
                if (layers[index].name != "Base Layer") continue;
                baseLayerCount++;
                baseLayer = layers[index];
            }
            Require(baseLayerCount == 1 && baseLayer.stateMachine != null,
                "QuakeSpirit controller must contain exactly one Base Layer.");

            int attackStateCount = 0;
            AnimatorState attackState = null;
            ChildAnimatorState[] states = baseLayer.stateMachine.states;
            for (int index = 0; index < states.Length; index++)
            {
                if (states[index].state == null || states[index].state.name != "Attack") continue;
                attackStateCount++;
                attackState = states[index].state;
            }
            Require(attackStateCount == 1
                && attackState.motion == attackClip
                && Mathf.Abs(attackState.speed - 1f) <= tolerance,
                "QuakeSpirit Base Layer/Attack must use the exact verified clip at speed 1.");
            Require(Mathf.Abs(attackClip.length - 5f) <= tolerance
                && Mathf.Abs(attackClip.frameRate - 30f) <= tolerance,
                "QuakeSpirit Attack clip must remain 5 seconds at 30 fps.");

            int hitEventCount = 0;
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(attackClip);
            for (int index = 0; index < events.Length; index++)
            {
                if (events[index].functionName != "OnAttackHit") continue;
                hitEventCount++;
                Require(Mathf.Abs(events[index].time - impactTime) <= tolerance,
                    "QuakeSpirit OnAttackHit must remain at the verified 1.667-second impact.");
            }
            Require(hitEventCount == 1,
                "QuakeSpirit Attack clip must contain exactly one OnAttackHit event.");

            ValidateQuakeSpiritPrefabController(bluePrefabPath, controller);
            ValidateQuakeSpiritPrefabController(redPrefabPath, controller);
        }

        /// <summary>
        /// BattleAxe의 서버 타격 시점이 실제 production Attack marker와 계속 일치하는지 확인한다.
        /// 설정, Controller 상태, clip, 양 진영 프리팹을 한 번에 읽어 어느 한 연결이라도
        /// 달라지면 Supported 상태를 정상으로 통과시키지 않는다.
        /// </summary>
        private static void ValidateBattleAxeProductionTimeline()
        {
            const string configPath = "Assets/_Project/Resources/Config/UnitStatsConfig.asset";
            const string controllerPath = "Assets/_Project/Animations/Units/BattleAxe/BattleAxe.controller";
            const string clipPath = "Assets/_Project/Animations/Units/BattleAxe/BattleAxe_Attack.anim";
            const string bluePrefabPath = "Assets/_Project/Prefabs/Units/Human/Unit_BattleAxe_Blue.prefab";
            const string redPrefabPath = "Assets/_Project/Prefabs/Units/Human/Unit_BattleAxe_Red.prefab";
            const string controllerGuid = "4c3402b2e6bfbf843965a0fe1b11bbd4";
            const string clipGuid = "c97327687cff891418b039f23b5b214b";
            const float impactTime = 1.02f;
            const float tolerance = 0.0001f;

            UnitStatsConfig config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(configPath);
            Require(config != null, "BattleAxe production UnitStatsConfig asset must exist.");

            int battleAxeRows = 0;
            UnitStatEntry battleAxeEntry = default;
            IReadOnlyList<UnitStatEntry> stats = config.Stats;
            for (int index = 0; index < stats.Count; index++)
            {
                if (stats[index].unitType != UnitType.BattleAxe) continue;
                battleAxeRows++;
                battleAxeEntry = stats[index];
            }
            Require(battleAxeRows == 1,
                "UnitStatsConfig must contain exactly one BattleAxe entry.");
            Require(battleAxeEntry.hitFrameTimes != null
                && battleAxeEntry.hitFrameTimes.Length == 1
                && Mathf.Abs(battleAxeEntry.hitFrameTimes[0] - impactTime) <= tolerance,
                "BattleAxe runtime hitFrameTimes must contain only the verified 1.02-second impact.");

            Require(AssetDatabase.AssetPathToGUID(controllerPath) == controllerGuid,
                "BattleAxe production controller GUID must remain exact.");
            Require(AssetDatabase.AssetPathToGUID(clipPath) == clipGuid,
                "BattleAxe production Attack clip GUID must remain exact.");

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            Require(controller != null && attackClip != null,
                "BattleAxe production AnimatorController and Attack clip must exist.");

            int baseLayerCount = 0;
            AnimatorControllerLayer baseLayer = default;
            AnimatorControllerLayer[] layers = controller.layers;
            for (int index = 0; index < layers.Length; index++)
            {
                if (layers[index].name != "Base Layer") continue;
                baseLayerCount++;
                baseLayer = layers[index];
            }
            Require(baseLayerCount == 1 && baseLayer.stateMachine != null,
                "BattleAxe controller must contain exactly one Base Layer.");

            int attackStateCount = 0;
            AnimatorState attackState = null;
            ChildAnimatorState[] states = baseLayer.stateMachine.states;
            for (int index = 0; index < states.Length; index++)
            {
                if (states[index].state == null || states[index].state.name != "Attack") continue;
                attackStateCount++;
                attackState = states[index].state;
            }
            Require(attackStateCount == 1
                && attackState.motion == attackClip
                && Mathf.Abs(attackState.speed - 1f) <= tolerance,
                "BattleAxe Base Layer/Attack must use the exact verified clip at speed 1.");
            Require(Mathf.Abs(attackClip.length - 3.1666667f) <= tolerance
                && Mathf.Abs(attackClip.frameRate - 30f) <= tolerance,
                "BattleAxe Attack clip must remain 3.1666667 seconds at 30 fps.");

            int hitEventCount = 0;
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(attackClip);
            for (int index = 0; index < events.Length; index++)
            {
                if (events[index].functionName != "OnAttackHit") continue;
                hitEventCount++;
                Require(Mathf.Abs(events[index].time - impactTime) <= tolerance,
                    "BattleAxe OnAttackHit must remain at the verified 1.02-second impact.");
            }
            Require(hitEventCount == 1,
                "BattleAxe Attack clip must contain exactly one OnAttackHit event.");

            ValidateBattleAxePrefabController(bluePrefabPath, controller, controllerGuid);
            ValidateBattleAxePrefabController(redPrefabPath, controller, controllerGuid);

            Require(UnitAttackShadowProfileResolver.TryResolve(
                    UnitType.BattleAxe, out UnitAttackShadowProfile battleAxe)
                && battleAxe.Support == UnitAttackShadowSupport.Supported
                && battleAxe.Delivery == AttackDeliveryKind.MeleeContact
                && battleAxe.LegacyExecution == LegacyAttackExecutionKind.TimerImpactWithSecondaryEffect
                && battleAxe.ExpectedImpactCount == 1
                && battleAxe.HasSecondaryResults
                && battleAxe.Reason == "primary-direct-supported-secondary-observed-only",
                "BattleAxe must remain an explicit supported single-impact MeleeContact row with secondary results.");
        }

        private static void ValidateBattleAxePrefabController(
            string prefabPath,
            RuntimeAnimatorController expectedController,
            string expectedControllerGuid)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Require(prefab != null, $"BattleAxe production prefab must exist: {prefabPath}");
            Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
            Require(animators.Length == 1 && animators[0].runtimeAnimatorController == expectedController,
                $"BattleAxe prefab must use the verified controller exactly once: {prefabPath}");
            Require(AssetDatabase.AssetPathToGUID(
                        AssetDatabase.GetAssetPath(animators[0].runtimeAnimatorController))
                    == expectedControllerGuid,
                $"BattleAxe prefab controller GUID must remain exact: {prefabPath}");
        }

        /// <summary>
        /// TorrentSpirit의 2:10 표식은 피해 시각이 아니라 파도 발사 시각이다.
        /// 사용자가 저장한 클립은 수정하지 않고, 실제 생산 경로가 그 표식과 동일한
        /// 70/30초 서버 예약을 쓰는지만 확인한다. 접촉별 피해/힐과 이동 시간은 별개다.
        /// </summary>
        private static void ValidateTorrentSpiritProductionTimeline()
        {
            const string root = "Assets/_Project/";
            const string clipPath = root + "Animations/Units/TorrentSpirit/TorrentSpirit_Attack.anim";
            const string controllerPath = root + "Animations/Units/TorrentSpirit/TorrentSpirit.controller";
            const string bluePath = root + "Prefabs/Units/Spirit/Unit_TorrentSpirit_Blue.prefab";
            const string redPath = root + "Prefabs/Units/Spirit/Unit_TorrentSpirit_Red.prefab";
            const float launchTime = 70f / 30f;
            const float tolerance = 0.0001f;

            Require(AssetDatabase.AssetPathToGUID(clipPath) == "0396c9df8348dd04a99ba18bfb31485a"
                    && AssetDatabase.AssetPathToGUID(controllerPath) == "f5935335d7dd91640bf4215bc5f16611"
                    && AssetDatabase.AssetPathToGUID(bluePath) == "4bbf44175a9e285488f1f9114ba304a6"
                    && AssetDatabase.AssetPathToGUID(redPath) == "d4deb64f4b4a8734db61eea3bc19334a",
                "TorrentSpirit production clip, controller, and both team prefabs must retain their identities.");
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            GameObject blue = AssetDatabase.LoadAssetAtPath<GameObject>(bluePath);
            GameObject red = AssetDatabase.LoadAssetAtPath<GameObject>(redPath);
            Require(clip != null && controller != null && blue != null && red != null,
                "TorrentSpirit production assets must all exist.");

            int baseLayers = 0;
            int attackStates = 0;
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.name != "Base Layer") continue;
                baseLayers++;
                Require(layer.stateMachine != null,
                    "TorrentSpirit Base Layer must retain its Attack state machine.");
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state == null || child.state.name != "Attack") continue;
                    attackStates++;
                    Require(child.state.motion == clip
                            && Mathf.Abs(child.state.speed - 1f) < tolerance
                            && !child.state.speedParameterActive,
                        "TorrentSpirit Base Layer/Attack must play the production clip at speed 1.");
                }
            }
            Require(baseLayers == 1 && attackStates == 1,
                "TorrentSpirit controller must have exactly one Base Layer/Attack state.");

            // 이벤트는 연출 발사 표식이다. 서버는 같은 오프셋에 파도를 생성하지만
            // 대상별 HP 변경은 이후 서버 전선이 닿을 때 발생한다.
            float[] markers = UnitFactory.GetHitFrameTimes(clip);
            Require(Mathf.Abs(clip.frameRate - 30f) < tolerance
                    && clip.length > launchTime
                    && markers.Length == 1
                    && Mathf.Abs(markers[0] - launchTime) < tolerance,
                "TorrentSpirit Attack needs one OnAttackHit launch marker at 2:10 (70/30s, 30fps).");

            UnitStatsConfig config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(
                root + "Resources/Config/UnitStatsConfig.asset");
            Require(config != null, "TorrentSpirit production UnitStatsConfig must exist.");
            int statRows = 0;
            foreach (UnitStatEntry entry in config.Stats)
            {
                if (entry.unitType != UnitType.TorrentSpirit) continue;
                statRows++;
                Require(Mathf.Abs(entry.attackCooldown - 4f) < tolerance
                        && entry.hitFrameTimes != null
                        && entry.hitFrameTimes.Length == 1
                        && Mathf.Abs(entry.hitFrameTimes[0] - launchTime) < tolerance
                        && Mathf.Abs(entry.hitFrameTimes[0] - markers[0]) < tolerance,
                    "TorrentSpirit config must retain its 4s cycle and match the 2:10 launch marker.");
            }
            Require(statRows == 1, "TorrentSpirit must have exactly one production stats row.");

            foreach (GameObject prefab in new[] { blue, red })
            {
                Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
                Require(animators.Length == 1
                        && animators[0].runtimeAnimatorController == controller
                        && !animators[0].applyRootMotion
                        && animators[0].GetComponent<Hexiege.Presentation.AnimationEventRelay>() != null,
                    "Both TorrentSpirit prefabs must use the production controller and receive Attack events.");
            }

            // PreviewScene만 열어 사용자가 편집 중인 씬을 바꾸거나 저장하지 않는다.
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(root + "Scenes/Game.unity");
            try
            {
                int factories = 0;
                int entries = 0;
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                foreach (UnitFactory factory in sceneRoot.GetComponentsInChildren<UnitFactory>(true))
                {
                    factories++;
                    var serialized = new SerializedObject(factory);
                    SerializedProperty rows = serialized.FindProperty("_spiritPrefabs");
                    Require(rows != null && rows.isArray,
                        "Game UnitFactory must serialize Spirit prefab registrations.");
                    for (int index = 0; index < rows.arraySize; index++)
                    {
                        SerializedProperty row = rows.GetArrayElementAtIndex(index);
                        // UnitType의 선언 순서가 아니라 실제 직렬화 ID를 비교한다.
                        if (row.FindPropertyRelative("type").intValue != (int)UnitType.TorrentSpirit)
                            continue;
                        entries++;
                        Require(row.FindPropertyRelative("attackTimelineClip").objectReferenceValue == clip
                                && row.FindPropertyRelative("blue").objectReferenceValue == blue
                                && row.FindPropertyRelative("red").objectReferenceValue == red,
                            "Game TorrentSpirit registration must bind the exact clip and both team prefabs.");
                    }
                }
                Require(factories == 1 && entries == 1,
                    "Game must have exactly one UnitFactory and one TorrentSpirit registration.");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }

            Require(UnitFactory.SelectAttackTimelineClip(clip, controller.animationClips) == clip
                    && UnitFactory.SelectAttackTimelineClip(clip, null) == clip
                    && UnitFactory.ShouldUseAttackClipHitFrameTimes(UnitType.TorrentSpirit, markers.Length),
                "TorrentSpirit production must select the explicit clip for server and Client timelines.");
            Require(UnitAttackShadowProfileResolver.TryResolve(
                    UnitType.TorrentSpirit, out UnitAttackShadowProfile profile)
                    && profile.Support == UnitAttackShadowSupport.Unresolved
                    && profile.Delivery == AttackDeliveryKind.TravelingArea
                    && profile.LegacyExecution == LegacyAttackExecutionKind.ActiveTravelingArea
                    && profile.ExpectedImpactCount == 1
                    && profile.HasSecondaryResults
                    && profile.Reason == "traveling-area-sequence-unresolved",
                "TorrentSpirit must remain Unresolved TravelingArea with its Legacy wave path.");
        }

        /// <summary>
        /// StreamSpirit의 현행 production은 권위 발사체가 아니라 TimerImpact 폴백이다.
        /// 서버 타격 시각과 실제 Attack marker를 0.50초로 맞추되, 발사체가 없는 상태를
        /// Supported로 가장하거나 과거처럼 Unresolved Legacy VFX를 차단하지 않도록 전체
        /// production 연결과 resolver 경계를 함께 확인한다.
        /// </summary>
        private static void ValidateStreamSpiritProductionTimeline()
        {
            const string configPath = "Assets/_Project/Resources/Config/UnitStatsConfig.asset";
            const string controllerPath = "Assets/_Project/Animations/Units/StreamSpirit/StreamSpirit.controller";
            const string clipPath = "Assets/_Project/Animations/Units/StreamSpirit/StreamSpirit_Attack.anim";
            const string bluePrefabPath = "Assets/_Project/Prefabs/Units/Spirit/Unit_StreamSpirit_Blue.prefab";
            const string redPrefabPath = "Assets/_Project/Prefabs/Units/Spirit/Unit_StreamSpirit_Red.prefab";
            const string effectConfigPath = "Assets/_Project/Resources/Config/UnitEffectConfig.asset";
            const string attackPresetPath = "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_StreamSpirit_Attack.asset";
            const string controllerGuid = "ea42bd46fe134a44eb7f261b9e969663";
            const string clipGuid = "98d5134cc0c7a3249aeb355be217dcec";
            const float impactTime = 0.50f;
            const float tolerance = 0.0001f;

            UnitStatsConfig config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(configPath);
            Require(config != null, "StreamSpirit production UnitStatsConfig asset must exist.");
            int streamRows = 0;
            UnitStatEntry streamEntry = default;
            IReadOnlyList<UnitStatEntry> stats = config.Stats;
            for (int index = 0; index < stats.Count; index++)
            {
                if (stats[index].unitType != UnitType.StreamSpirit) continue;
                streamRows++;
                streamEntry = stats[index];
            }
            Require(streamRows == 1
                && streamEntry.hitFrameTimes != null
                && streamEntry.hitFrameTimes.Length == 1
                && Mathf.Abs(streamEntry.hitFrameTimes[0] - impactTime) <= tolerance,
                "StreamSpirit runtime hitFrameTimes must contain only the verified 0.50-second impact.");

            Require(AssetDatabase.AssetPathToGUID(controllerPath) == controllerGuid,
                "StreamSpirit production controller GUID must remain exact.");
            Require(AssetDatabase.AssetPathToGUID(clipPath) == clipGuid,
                "StreamSpirit production Attack clip GUID must remain exact.");
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            Require(controller != null && attackClip != null,
                "StreamSpirit production AnimatorController and Attack clip must exist.");

            AnimatorControllerLayer baseLayer = default;
            int baseLayerCount = 0;
            for (int index = 0; index < controller.layers.Length; index++)
            {
                if (controller.layers[index].name != "Base Layer") continue;
                baseLayerCount++;
                baseLayer = controller.layers[index];
            }
            Require(baseLayerCount == 1 && baseLayer.stateMachine != null,
                "StreamSpirit controller must contain exactly one Base Layer.");

            AnimatorState attackState = null;
            int attackStateCount = 0;
            ChildAnimatorState[] states = baseLayer.stateMachine.states;
            for (int index = 0; index < states.Length; index++)
            {
                if (states[index].state == null || states[index].state.name != "Attack") continue;
                attackStateCount++;
                attackState = states[index].state;
            }
            Require(attackStateCount == 1
                && attackState.motion == attackClip
                && Mathf.Abs(attackState.speed - 1f) <= tolerance,
                "StreamSpirit Base Layer/Attack must use the exact verified clip at speed 1.");
            Require(Mathf.Abs(attackClip.length - 1.5f) <= tolerance
                && Mathf.Abs(attackClip.frameRate - 30f) <= tolerance
                && attackClip.isLooping,
                "StreamSpirit Attack clip must remain 1.5 seconds at 30 fps and loop.");

            int hitEventCount = 0;
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(attackClip);
            for (int index = 0; index < events.Length; index++)
            {
                if (events[index].functionName != "OnAttackHit") continue;
                hitEventCount++;
                Require(Mathf.Abs(events[index].time - impactTime) <= tolerance,
                    "StreamSpirit OnAttackHit must remain at the verified 0.50-second impact.");
            }
            Require(hitEventCount == 1,
                "StreamSpirit Attack clip must contain exactly one OnAttackHit event.");

            ValidateProductionAttackPrefab(
                "StreamSpirit", bluePrefabPath, controller, controllerGuid, false, default);
            ValidateProductionAttackPrefab(
                "StreamSpirit", redPrefabPath, controller, controllerGuid, false, default);

            var effectConfig = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.UnitEffectConfig>(effectConfigPath);
            var expectedAttackPreset = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.EffectPreset>(attackPresetPath);
            Require(effectConfig != null && expectedAttackPreset != null,
                "StreamSpirit production UnitEffectConfig and attack preset must exist.");
            SerializedObject serializedEffects = new SerializedObject(effectConfig);
            SerializedProperty entries = serializedEffects.FindProperty("_entries");
            Require(entries != null && entries.isArray,
                "StreamSpirit production UnitEffectConfig entries must be serialized.");
            int effectRows = 0;
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty row = entries.GetArrayElementAtIndex(index);
                SerializedProperty rowUnitType = row.FindPropertyRelative("unitType");
                // UnitType은 명시값을 사용하므로 enumValueIndex가 아니라 실제 직렬화 intValue를 비교한다.
                if (rowUnitType == null || rowUnitType.intValue != (int)UnitType.StreamSpirit) continue;
                effectRows++;
                Require(row.FindPropertyRelative("attackPreset").objectReferenceValue == expectedAttackPreset,
                    "StreamSpirit must retain the verified production attack preset.");
                // [R5 비활성화 — 2026-10-19 사용자 승인으로 임시 트레이서 연결. C 방식(서버 투사체) 구현 시 재검토]
                //   예전에는 "트레이서 미구현"을 고정했지만, 사용자가 VFX 확인용으로 Legacy 트레이서를
                //   임시 연결하기로 승인했다. 원래 검사는 지우지 않고 아래에 주석으로 남긴다.
                // Require(row.FindPropertyRelative("tracerPreset").objectReferenceValue == null,
                //     "StreamSpirit tracer is not implemented; wiring one requires a separate authoritative projectile design.");
                // [R5 임시 — C 방식(서버 투사체) 구현 시 교체]
                //   트레이서 = 물폭탄 프리셋, 착탄 = 물보라 프리셋(공격자 기준 impact 슬롯),
                //   피격(hit) 슬롯은 "맞은 유닛 타입" 기준이라 비워 둔다(공격자 이펙트가 엉뚱한 유닛에 뜨지 않게).
                var expectedTracerPreset = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.EffectPreset>(
                    "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_StreamSpirit_Tracer.asset");
                var expectedImpactPreset = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.EffectPreset>(
                    "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_StreamSpirit_Hit.asset");
                Require(expectedTracerPreset != null && expectedImpactPreset != null
                    && row.FindPropertyRelative("tracerPreset").objectReferenceValue == expectedTracerPreset
                    && row.FindPropertyRelative("impactPreset").objectReferenceValue == expectedImpactPreset
                    && row.FindPropertyRelative("hitPreset").objectReferenceValue == null,
                    "[R5 interim] StreamSpirit uses the interim Legacy tracer + attacker impact preset, and its victim-keyed hit slot stays empty.");
            }
            Require(effectRows == 1, "UnitEffectConfig must contain exactly one StreamSpirit row.");

            SerializedObject serializedPreset = new SerializedObject(expectedAttackPreset);
            SerializedProperty vfxPrefab = serializedPreset.FindProperty("_vfxPrefab");
            Require(vfxPrefab != null && vfxPrefab.objectReferenceValue != null,
                "StreamSpirit production attack preset must retain a non-null VFX prefab.");

            Require(UnitAttackShadowProfileResolver.TryResolve(
                    UnitType.StreamSpirit, out UnitAttackShadowProfile stream)
                && stream.Support == UnitAttackShadowSupport.Unresolved
                && stream.TargetMode == AttackTargetMode.TargetLocked
                && stream.Delivery == AttackDeliveryKind.ProjectileImpact
                && stream.LegacyExecution == LegacyAttackExecutionKind.TimerImpact
                && stream.ExpectedImpactCount == 1
                && !stream.HasSecondaryResults
                && stream.Reason == "projectile-timeline-unresolved"
                && UnitAttackShadowProfileResolver.ClassifyAuditEligibility(
                    stream, runtimeContractValid: false)
                    == AttackPresentationAuditEligibility.KnownUnresolved,
                "StreamSpirit must remain explicit KnownUnresolved ProjectileImpact with Legacy TimerImpact fallback.");
        }

        /// <summary>
        /// 실기 진단이 다른 유닛이나 provisional 시작을 섞지 않고, 정해진 개수에서 반드시
        /// 멈추며, gameplay writer를 소유하지 않는지 확인한다. 또한 실제 production의 세 경계
        /// (StartCombatAnimation, OnAttackHit/VFX, 서버 TimerImpact)에 관측 호출이 남아 있는지
        /// 소스 수준에서 fail-closed한다. 이 검증은 게임을 실행하거나 상태를 변경하지 않는다.
        /// </summary>
        private static void ValidateStreamSpiritProductionTimelineDiagnostics()
        {
            Require(UnitAttackShadowObserver.StreamSpiritTimelineSchema
                    == "streamspirit-production-timeline-v1",
                "StreamSpirit production timeline diagnostic schema must remain exact.");
            Require(UnitAttackShadowObserver.ShouldObserveStreamSpiritTimelineForValidation(
                        UnitType.StreamSpirit,
                        AttackPresentationImpactMode.LegacyFallback)
                    && !UnitAttackShadowObserver.ShouldObserveStreamSpiritTimelineForValidation(
                        UnitType.StreamSpirit,
                        AttackPresentationImpactMode.Suppressed)
                    && !UnitAttackShadowObserver.ShouldObserveStreamSpiritTimelineForValidation(
                        UnitType.InfernoSpirit,
                        AttackPresentationImpactMode.LegacyFallback),
                "StreamSpirit timeline diagnostics must filter to production StreamSpirit LegacyFallback starts only.");

            int capacity = UnitAttackShadowObserver.StreamSpiritTimelineCapacityForValidation();
            Require(capacity == 64
                    && UnitAttackShadowObserver.ClassifyStreamSpiritTimelineAdmissionForValidation(
                        0, false, capacity) == StreamSpiritTimelineAdmission.Accepted
                    && UnitAttackShadowObserver.ClassifyStreamSpiritTimelineAdmissionForValidation(
                        capacity - 1, false, capacity) == StreamSpiritTimelineAdmission.Accepted
                    && UnitAttackShadowObserver.ClassifyStreamSpiritTimelineAdmissionForValidation(
                        capacity, false, capacity) == StreamSpiritTimelineAdmission.Overflow
                    && UnitAttackShadowObserver.ClassifyStreamSpiritTimelineAdmissionForValidation(
                        1, true, capacity) == StreamSpiritTimelineAdmission.Duplicate
                    && UnitAttackShadowObserver.ClassifyStreamSpiritTimelineAdmissionForValidation(
                        -1, false, capacity) == StreamSpiritTimelineAdmission.Invalid,
                "StreamSpirit timeline diagnostics must remain bounded with explicit duplicate and overflow classification.");
            Require(UnitAttackShadowObserver.StreamSpiritTimelineObserverIsReadOnlyForValidation(),
                "StreamSpirit production timeline diagnostics must remain read-only.");

            string unitViewPath = Path.Combine(
                UnityEngine.Application.dataPath,
                "_Project/Scripts/Presentation/Unit/UnitView.cs");
            string controllerPath = Path.Combine(
                UnityEngine.Application.dataPath,
                "_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs");
            Require(File.Exists(unitViewPath) && File.Exists(controllerPath),
                "StreamSpirit timeline diagnostic source gate requires both production call-site files.");
            string unitViewSource = File.ReadAllText(unitViewPath);
            string controllerSource = File.ReadAllText(controllerPath);
            Require(unitViewSource.Contains("RecordStreamSpiritTimelineStart(")
                    && unitViewSource.Contains("RecordStreamSpiritTimelineMarker(")
                    && unitViewSource.Contains("RecordStreamSpiritTimelineVfx(")
                    && controllerSource.Contains("RecordStreamSpiritTimerImpact("),
                "StreamSpirit diagnostics must remain wired to attack start, marker/VFX, and server TimerImpact boundaries.");
        }

        /// <summary>
        /// 공통 주 피해 writer의 이벤트를 구독해 스코프 없는 여우마법사만 즉시 표시하는지 검사한다.
        /// 실제 경기를 실행하거나 피해 타이머를 당기지 않는다.
        /// </summary>
        private static void ValidateFoxMagicianPrimaryPresentation()
        {
            // 실제 피해 writer가 발행한 이벤트를 검사한다. 실행 중 경기의 전역 이벤트에
            // 가짜 피해가 들어가지 않도록 반드시 Edit 모드에서만 수행한다.
            Require(!UnityEngine.Application.isPlaying,
                "FoxMagician damage-event validation must run outside Play mode.");
            MethodInfo execute = typeof(UnitCombatUseCase).GetMethod(
                "ExecuteAttack", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(execute != null, "Production primary damage entry must exist.");
            var combat = new UnitCombatUseCase(null, null, null, null,
                new HexMetricsCoordinateMapper());
            foreach (UnitType type in new[] { UnitType.FoxMagician, UnitType.DustSpirit, UnitType.LionKnight })
            foreach (bool scoped in new[] { false, true })
            foreach (bool building in new[] { false, true })
            {
                var attacker = new UnitData(-101, type, TeamId.Blue, new HexCoord(0, 0),
                    1000, 10, 5f, 5f);
                IDamageable victim = building
                    ? (IDamageable)new BuildingData(-102, BuildingType.Castle, TeamId.Red,
                        new HexCoord(1, 0), 1000)
                    : new UnitData(-102, UnitType.DustSpirit, TeamId.Red,
                        new HexCoord(1, 0), 1000, 1, 1f, 1f);
                // 유효 키 보존 분기는 키 자체의 유효성을 검사한다. 이 fixture는 결과
                // Coordinator에 등록하지 않으며 실제 회차/피해자 상관 검증을 주장하지 않는다.
                var key = scoped ? new AttackResultKey(new AttackerInstanceId(1UL),
                    new AttackSequenceId(1UL), 0, building ? 2 : 1, 102, 1, 0) : default;
                Require(key.IsValid == scoped, "Fixture result-key validity must be explicit.");
                int events = 0;
                using (GameEvents.OnEntityDamaged.Subscribe(evt =>
                {
                    if (!ReferenceEquals(evt.Entity, victim)) return;
                    events++;
                    Require(evt.CurrentHp == victim.Hp && evt.CurrentHp == 990,
                        "Presentation must observe already-applied damage, not apply it again.");
                    // [V-4 비활성화 — 2026-10-21 서버 권위 투사체 단계 D, RM-5 · 사용자 승인 F-4(규칙 19 Fox 예외 삭제)]
                    //   옛 검사(Fox만 결과 키 없으면 즉시 표시)는 반대 검사가 됐다. 지우지 않고 주석으로 남긴다:
                    // Require(evt.ImmediatePresentation == (type == UnitType.FoxMagician && !scoped),
                    //     "Only unscoped FoxMagician primary damage may bypass the next marker.");
                    Require(!evt.ImmediatePresentation,
                        "[RM-5] No primary damage bypasses presentation anymore: the Fox rule-19 immediate exception is removed (Fox numbers show at the projectile impact).");
                    Require(evt.PresentationResultKey.Equals(key)
                            && evt.AttackerId == attacker.Id && evt.AttackerIsUnit
                            && evt.IsUnit == !building,
                        "Primary event identity and scoped result key must remain unchanged.");
                }))
                {
                    execute.Invoke(combat, new object[] { attacker, victim, false, key });
                }
                Require(events == 1 && victim.Hp == 990,
                    "Primary writer must apply and publish exactly once for units and buildings.");
            }
        }

        /// <summary>
        /// 기존 여우마법사 시간축 진단의 LegacyFallback 한정, 용량 제한과 읽기 전용 계약을 검사한다.
        /// </summary>
        private static void ValidateFoxMagicianProductionTimelineDiagnostics()
        {
            Require(UnitAttackShadowObserver.FoxMagicianTimelineSchema
                    == "foxmagician-production-timeline-v2",
                "FoxMagician production timeline diagnostic schema must remain exact.");
            Require(UnitAttackShadowObserver.ShouldObserveFoxMagicianTimelineForValidation(
                        UnitType.FoxMagician,
                        AttackPresentationImpactMode.LegacyFallback)
                    && !UnitAttackShadowObserver.ShouldObserveFoxMagicianTimelineForValidation(
                        UnitType.FoxMagician,
                        AttackPresentationImpactMode.Suppressed)
                    && !UnitAttackShadowObserver.ShouldObserveFoxMagicianTimelineForValidation(
                        UnitType.StreamSpirit,
                        AttackPresentationImpactMode.LegacyFallback),
                "FoxMagician timeline diagnostics must admit only FoxMagician LegacyFallback production starts.");

            int capacity = UnitAttackShadowObserver.FoxMagicianTimelineCapacityForValidation();
            Require(capacity == 256
                    && UnitAttackShadowObserver.ClassifyFoxMagicianTimelineAdmissionForValidation(
                        0, false, capacity) == FoxMagicianTimelineAdmission.Accepted
                    && UnitAttackShadowObserver.ClassifyFoxMagicianTimelineAdmissionForValidation(
                        capacity - 1, false, capacity) == FoxMagicianTimelineAdmission.Accepted
                    && UnitAttackShadowObserver.ClassifyFoxMagicianTimelineAdmissionForValidation(
                        capacity, false, capacity) == FoxMagicianTimelineAdmission.Overflow
                    && UnitAttackShadowObserver.ClassifyFoxMagicianTimelineAdmissionForValidation(
                        1, true, capacity) == FoxMagicianTimelineAdmission.Duplicate
                    && UnitAttackShadowObserver.ClassifyFoxMagicianTimelineAdmissionForValidation(
                        -1, false, capacity) == FoxMagicianTimelineAdmission.Invalid,
                "FoxMagician timeline diagnostics must remain bounded with explicit duplicate and overflow classification.");
            Require(!UnitAttackShadowObserver.FoxMagicianTimelineIncompleteExpiredForValidation(7.999d, 0d)
                    && UnitAttackShadowObserver.FoxMagicianTimelineIncompleteExpiredForValidation(8d, 0d)
                    && !UnitAttackShadowObserver.FoxMagicianTimelineIncompleteExpiredForValidation(double.NaN, 0d),
                "FoxMagician incomplete diagnostic flows must expire after eight seconds without fabricating completion.");
            Require(UnitAttackShadowObserver.FoxMagicianTimelineObserverIsReadOnlyForValidation(),
                "FoxMagician production timeline diagnostics must remain read-only.");
            Require(UnitAttackShadowObserver.FoxMagicianCorrelationBindingsHaveCapacityForValidation(
                        0, 0, capacity)
                    && UnitAttackShadowObserver.FoxMagicianCorrelationBindingsHaveCapacityForValidation(
                        capacity - 1, capacity - 1, capacity)
                    && !UnitAttackShadowObserver.FoxMagicianCorrelationBindingsHaveCapacityForValidation(
                        capacity, capacity, capacity)
                    && !UnitAttackShadowObserver.FoxMagicianCorrelationBindingsHaveCapacityForValidation(
                        1, 0, capacity),
                "FoxMagician exact ticket dictionaries must remain paired and inside the bounded flow capacity.");
            Require(Math.Abs(UnitAttackShadowObserver.FoxMagicianExpectedImpactSecondsForValidation() - 85d / 30d)
                        < 0.000001d
                    && UnitAttackShadowObserver.FoxMagicianCorrelationTicketMatchesForValidation(17UL, 17UL)
                    && !UnitAttackShadowObserver.FoxMagicianCorrelationTicketMatchesForValidation(17UL, 18UL)
                    && !UnitAttackShadowObserver.FoxMagicianCorrelationTicketMatchesForValidation(0UL, 0UL)
                    && UnitAttackShadowObserver.FoxMagicianTimerImpactBoundaryIsExactForValidation(
                        1, 85d / 30d, 4d)
                    && !UnitAttackShadowObserver.FoxMagicianTimerImpactBoundaryIsExactForValidation(
                        2, 85d / 30d, 4d)
                    && !UnitAttackShadowObserver.FoxMagicianTimerImpactBoundaryIsExactForValidation(
                        1, 4d, 4d),
                "FoxMagician TimerImpact retains one exact ticket at 85/30 seconds inside the 4-second cycle; serialized floats are compared with tolerance.");

            string unitViewPath = Path.Combine(
                UnityEngine.Application.dataPath,
                "_Project/Scripts/Presentation/Unit/UnitView.cs");
            string controllerPath = Path.Combine(
                UnityEngine.Application.dataPath,
                "_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs");
            Require(File.Exists(unitViewPath) && File.Exists(controllerPath),
                "FoxMagician timeline diagnostic source gate requires both production call-site files.");
            string unitViewSource = File.ReadAllText(unitViewPath);
            string controllerSource = File.ReadAllText(controllerPath);
            Require(unitViewSource.Contains("RecordFoxMagicianTimelineStart(")
                    && unitViewSource.Contains("RecordFoxMagicianTimelineMarker(")
                    && unitViewSource.Contains("RecordFoxMagicianNativeImpactMarker(")
                    && unitViewSource.Contains("RecordFoxMagicianTimelineVfxAttempt(")
                    && unitViewSource.Contains("RecordFoxMagicianTimelineVfxResult(")
                    && controllerSource.Contains("ReserveFoxMagicianTimelineCorrelationTicket(")
                    && controllerSource.Contains("BindFoxMagicianTimelineCorrelationTicket(")
                    && controllerSource.Contains("RecordFoxMagicianTimerImpact("),
                "FoxMagician diagnostics must remain wired to start, marker, VFX attempt/result, exact revision ticket binding, and server TimerImpact boundaries.");

            string terminal = UnitAttackShadowObserver.BuildFoxMagicianTimelineTerminalDataForValidation();
            string[] requiredTerminalFields =
            {
                "schema=foxmagician-production-timeline-v2", "starts=", "charges=",
                "vfxAttempts=", "vfxStarted=", "vfxFailures=", "timerImpacts=",
                "particlePositive=", "playbackActive=", "maxParticleSystems=",
                "complete=", "incomplete=", "unmatchedMarker=", "unmatchedVfxAttempt=",
                "unmatchedVfxResult=", "unmatchedTimer=", "duplicates=", "overflow=",
                "dropped=", "expired=", "cappedDetails=",
                "maxStartMarker=", "maxStartTimer=", "maxMarkerTimerDelta=",
                "expectedImpactOffset=2.833", "gameplayWrites=0", "verdict="
            };
            for (int index = 0; index < requiredTerminalFields.Length; index++)
                Require(terminal.Contains(requiredTerminalFields[index]),
                    $"FoxMagician terminal must contain {requiredTerminalFields[index]}.");
            MethodInfo foxTerminalPreflight = typeof(UnitAttackShadowObserver).GetMethod(
                "ValidateTerminalPreflightForValidation",
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(foxTerminalPreflight != null,
                "FoxMagician diagnostics require the production full-line UTF-8 preflight seam.");
            string preflight = (string)foxTerminalPreflight.Invoke(null, null);
            Require(preflight.Contains("valid=True") && preflight.Contains("maxBytes="),
                "FoxMagician terminal must remain inside the existing UTF-8 preflight.");
        }

        private static void ValidateProductionAttackPrefab(
            string unitName,
            string prefabPath,
            RuntimeAnimatorController expectedController,
            string expectedControllerGuid,
            bool validateVfxSpawnLocalPosition,
            Vector3 expectedVfxSpawnLocalPosition)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Require(prefab != null, $"{unitName} production prefab must exist: {prefabPath}");
            Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
            Require(animators.Length == 1
                    && animators[0].runtimeAnimatorController == expectedController
                    && !animators[0].applyRootMotion,
                $"{unitName} prefab must use the verified controller exactly once with ApplyRootMotion disabled: {prefabPath}");
            Require(AssetDatabase.AssetPathToGUID(
                        AssetDatabase.GetAssetPath(animators[0].runtimeAnimatorController))
                    == expectedControllerGuid,
                $"{unitName} prefab controller GUID must remain exact: {prefabPath}");

            Hexiege.Presentation.UnitView[] unitViews =
                prefab.GetComponentsInChildren<Hexiege.Presentation.UnitView>(true);
            Require(unitViews.Length == 1 && unitViews[0].transform == prefab.transform,
                $"{unitName} prefab must contain exactly one root UnitView: {prefabPath}");

            Transform vfxSpawnPoint = null;
            int vfxSpawnPointCount = 0;
            Transform[] transforms = prefab.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name != "VfxSpawnPoint") continue;
                vfxSpawnPointCount++;
                vfxSpawnPoint = transforms[index];
            }
            Require(vfxSpawnPointCount == 1 && vfxSpawnPoint != null,
                $"{unitName} prefab must contain exactly one VfxSpawnPoint: {prefabPath}");
            if (validateVfxSpawnLocalPosition)
            {
                Require(Vector3.Distance(
                            vfxSpawnPoint.localPosition,
                            expectedVfxSpawnLocalPosition) <= 0.000001f,
                    $"{unitName} VfxSpawnPoint local position must remain exact: {prefabPath}");
            }
            SerializedObject serializedUnitView = new SerializedObject(unitViews[0]);
            SerializedProperty serializedVfxSpawnPoint = serializedUnitView.FindProperty("_vfxSpawnPoint");
            Require(serializedVfxSpawnPoint != null
                    && serializedVfxSpawnPoint.objectReferenceValue == vfxSpawnPoint,
                $"{unitName} UnitView must reference the verified VfxSpawnPoint exactly: {prefabPath}");
        }

        // [T-6 — 2026-10-19 차지 유닛 발사 시점] 옛 요약(기록용 보존):
        //   "FoxMagician의 현재 production은 권위 발사체나 tracer 없이 TimerImpact를 사용한다.
        //    charge 1.00초와 서버 피해 30fps의 2:25를 분리하고, projectile 계약을 Supported로
        //    승격하지 않도록 production 연결과 Unresolved 경계를 함께 확인한다."
        /// <summary>
        /// FoxMagician의 현재 production은 권위 발사체 없이 서버 TimerImpact(30fps 2:25 = 85/30초)를 사용한다.
        /// 화면에서는 1.00초 OnAttackCharge에서 charge VFX를 켜고, 2:25 OnAttackHit에서 화면 표현용 Legacy 트레이서(마법탄)를
        /// 발사하며 공격 SFX를 낸다. charge와 서버 피해 시점을 분리하고, projectile 계약을 Supported로
        /// 승격하지 않도록 production 연결과 Unresolved 경계를 함께 확인한다.
        /// </summary>
        private static void ValidateFoxMagicianProductionTimeline()
        {
            const string configPath = "Assets/_Project/Resources/Config/UnitStatsConfig.asset";
            const string controllerPath = "Assets/_Project/Animations/Units/FoxMagician/FoxMagician.controller";
            const string clipPath = "Assets/_Project/Animations/Units/FoxMagician/FoxMagician_Attack.anim";
            const string bluePrefabPath = "Assets/_Project/Prefabs/Units/Transcendence/Unit_FoxMagician_Blue.prefab";
            const string redPrefabPath = "Assets/_Project/Prefabs/Units/Transcendence/Unit_FoxMagician_Red.prefab";
            const string effectConfigPath = "Assets/_Project/Resources/Config/UnitEffectConfig.asset";
            const string attackPresetPath = "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_FoxMagician_Attack.asset";
            const string vfxPrefabPath = "Assets/_Project/Prefabs/VFX/Units/vfx_foxmagician_charge.prefab";
            const string controllerGuid = "9373b8c7c17dbaa48b16b2a710a847b4";
            const string clipGuid = "2a61dae4330eb7c418f33f773a87978f";
            const string vfxPrefabGuid = "8da00a8b0eb6c154395554f8b3604771";
            const float vfxMarkerTime = 1.00f;
            const float damageImpactTime = 85f / 30f;
            const float tolerance = 0.0001f;

            UnitStatsConfig config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(configPath);
            Require(config != null, "FoxMagician production UnitStatsConfig asset must exist.");
            int foxRows = 0;
            UnitStatEntry foxEntry = default;
            IReadOnlyList<UnitStatEntry> stats = config.Stats;
            for (int index = 0; index < stats.Count; index++)
            {
                if ((int)stats[index].unitType != (int)UnitType.FoxMagician) continue;
                foxRows++;
                foxEntry = stats[index];
            }
            Require(foxRows == 1
                    && (int)foxEntry.unitType == 21
                    && foxEntry.hitFrameTimes != null
                    && foxEntry.hitFrameTimes.Length == 1
                    && Mathf.Abs(foxEntry.hitFrameTimes[0] - damageImpactTime) <= tolerance
                    && vfxMarkerTime < damageImpactTime
                    && damageImpactTime < foxEntry.attackCooldown,
                "FoxMagician type21 retains charge at 1 second and authoritative impact at 85/30 seconds.");
            Require(UnitFactory.ShouldUseAttackClipHitFrameTimes(UnitType.FoxMagician, 1)
                    && UnitFactory.ShouldUseAttackClipHitFrameTimes(UnitType.StreamSpirit, 1)
                    && !UnitFactory.ShouldUseAttackClipHitFrameTimes(UnitType.StreamSpirit, 0),
                "FoxMagician and other units use typed OnAttackHit markers; charge never overrides damage.");
            string factoryPath = Path.Combine(UnityEngine.Application.dataPath,
                "_Project/Scripts/Infrastructure/Factories/UnitFactory.cs");
            Require(File.Exists(factoryPath), "FoxMagician production UnitFactory source must exist.");
            string factorySource = File.ReadAllText(factoryPath);
            const string factoryGuardCall = "ShouldUseAttackClipHitFrameTimes(unitData.Type, hitFrameTimes.Length)";
            Require(factorySource.Split(new[] { factoryGuardCall }, StringSplitOptions.None).Length - 1 == 2,
                "Both server/SOLO and Client Factory paths must apply the extracted damage marker.");

            Require(AssetDatabase.AssetPathToGUID(controllerPath) == controllerGuid,
                "FoxMagician production controller GUID must remain exact.");
            Require(AssetDatabase.AssetPathToGUID(clipPath) == clipGuid,
                "FoxMagician production Attack clip GUID must remain exact.");
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            Require(controller != null && attackClip != null,
                "FoxMagician production AnimatorController and Attack clip must exist.");

            AnimatorControllerLayer baseLayer = default;
            int baseLayerCount = 0;
            for (int index = 0; index < controller.layers.Length; index++)
            {
                if (controller.layers[index].name != "Base Layer") continue;
                baseLayerCount++;
                baseLayer = controller.layers[index];
            }
            Require(baseLayerCount == 1 && baseLayer.stateMachine != null,
                "FoxMagician controller must contain exactly one Base Layer.");

            AnimatorState attackState = null;
            int attackStateCount = 0;
            ChildAnimatorState[] states = baseLayer.stateMachine.states;
            for (int index = 0; index < states.Length; index++)
            {
                if (states[index].state == null || states[index].state.name != "Attack") continue;
                attackStateCount++;
                attackState = states[index].state;
            }
            Require(attackStateCount == 1
                    && attackState.motion == attackClip
                    && Mathf.Abs(attackState.speed - 1f) <= tolerance,
                "FoxMagician Base Layer/Attack must use the exact verified clip at speed 1.");
            Require(Mathf.Abs(attackClip.length - 4.00f) <= tolerance
                    && Mathf.Abs(attackClip.frameRate - 30f) <= tolerance
                    && attackClip.isLooping,
                "FoxMagician Attack clip must remain 4.00 seconds at 30 fps and loop.");

            int hitEventCount = 0;
            int chargeEventCount = 0;
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(attackClip);
            for (int index = 0; index < events.Length; index++)
            {
                if (events[index].functionName == "OnAttackCharge")
                {
                    chargeEventCount++;
                    Require(Mathf.Abs(events[index].time - vfxMarkerTime) <= tolerance,
                        "FoxMagician source-only charge must remain at 1 second.");
                    continue;
                }
                if (events[index].functionName != "OnAttackHit") continue;
                hitEventCount++;
                Require(Mathf.Abs(events[index].time - damageImpactTime) <= tolerance,
                    "FoxMagician OnAttackHit must retain the user's 85/30-second marker.");
            }
            float[] extracted = UnitFactory.GetHitFrameTimes(attackClip);
            Require(hitEventCount == 1 && chargeEventCount == 1 && extracted.Length == 1
                    && Mathf.Abs(extracted[0] - damageImpactTime) <= tolerance,
                "Fox has one charge and one hit event, but Factory extracts only the damage event.");
            ValidateFoxMagicianTimingAuthorityCore(foxEntry, extracted);
            ValidateFoxMagicianMarkerCallbacks();

            var expectedVfxSpawnLocalPosition = new Vector3(0f, 0f, 0.011f);
            ValidateProductionAttackPrefab(
                "FoxMagician",
                bluePrefabPath,
                controller,
                controllerGuid,
                true,
                expectedVfxSpawnLocalPosition);
            ValidateProductionAttackPrefab(
                "FoxMagician",
                redPrefabPath,
                controller,
                controllerGuid,
                true,
                expectedVfxSpawnLocalPosition);

            var effectConfig = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.UnitEffectConfig>(effectConfigPath);
            var expectedAttackPreset = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.EffectPreset>(attackPresetPath);
            GameObject expectedVfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(vfxPrefabPath);
            Require(effectConfig != null && expectedAttackPreset != null && expectedVfxPrefab != null,
                "FoxMagician production UnitEffectConfig, attack preset, and VFX prefab must exist.");
            Require(AssetDatabase.AssetPathToGUID(vfxPrefabPath) == vfxPrefabGuid,
                "FoxMagician production VFX prefab GUID must remain exact.");

            SerializedObject serializedEffects = new SerializedObject(effectConfig);
            SerializedProperty entries = serializedEffects.FindProperty("_entries");
            Require(entries != null && entries.isArray,
                "FoxMagician production UnitEffectConfig entries must be serialized.");
            int effectRows = 0;
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty row = entries.GetArrayElementAtIndex(index);
                SerializedProperty rowUnitType = row.FindPropertyRelative("unitType");
                // UnitType은 명시값을 사용하므로 enumValueIndex가 아니라 실제 직렬화 intValue를 비교한다.
                if (rowUnitType == null
                    || rowUnitType.intValue != (int)UnitType.FoxMagician) continue;
                effectRows++;
                Require(rowUnitType.intValue == 21,
                    "FoxMagician UnitEffectConfig row must retain explicit unitType=21.");
                Require(row.FindPropertyRelative("attackPreset").objectReferenceValue == expectedAttackPreset,
                    "FoxMagician must retain the verified production attack preset.");
                // [V-3 비활성화 — 2026-10-19 차지 유닛 발사 시점, 사용자 승인] 예전에는 「Fox 트레이서 미구현」을 고정했다.
                //   이제 Fox도 InfernoSpirit(R5 임시)과 같은 방식으로 화면 표현용 Legacy 트레이서(마법탄)와 착탄 이펙트를 연결한다.
                //   서버 판정은 여전히 85/30초 TimerImpact이며 실제 권위 투사체(C 방식)는 아니다. 원래 검사는 주석으로 남긴다.
                // Require(row.FindPropertyRelative("tracerPreset").objectReferenceValue == null,
                //     "FoxMagician tracer is not implemented; wiring one requires a separate authoritative projectile design.");
                // [T-2] 트레이서 = 마법탄 프리셋, 착탄 = 공격자 기준 impact 슬롯. 피격(hit) 슬롯은 「맞은 유닛 타입」 기준이라 비워 둔다.
                //   프리셋 파일 이름만 맞고 GUID가 다른 사본을 연결하는 실수를 막으려고 GUID도 확인한다(사용자 커밋 683e1566의 에셋).
                const string foxTracerPresetPath = "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_FoxMagician_Tracer.asset";
                const string foxImpactPresetPath = "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_FoxMagician_Impact.asset";
                var expectedTracerPreset = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.EffectPreset>(foxTracerPresetPath);
                var expectedImpactPreset = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.EffectPreset>(foxImpactPresetPath);
                Require(expectedTracerPreset != null && expectedImpactPreset != null
                        && AssetDatabase.AssetPathToGUID(foxTracerPresetPath) == "025332764a8d09e4d97973e8c76a487e"
                        && AssetDatabase.AssetPathToGUID(foxImpactPresetPath) == "bbc25dda207ccb84a9068c4a63392e73"
                        && row.FindPropertyRelative("tracerPreset").objectReferenceValue == expectedTracerPreset
                        && row.FindPropertyRelative("impactPreset").objectReferenceValue == expectedImpactPreset
                        && row.FindPropertyRelative("hitPreset").objectReferenceValue == null,
                    "FoxMagician uses the wired Legacy tracer + attacker impact presets (exact GUIDs), and its victim-keyed hit slot stays empty.");
                Require(expectedTracerPreset.VfxPrefab != null && expectedImpactPreset.VfxPrefab != null
                        && AssetDatabase.GetAssetPath(expectedTracerPreset.VfxPrefab).EndsWith("vfx_foxmagician_tracer.prefab")
                        && AssetDatabase.GetAssetPath(expectedImpactPreset.VfxPrefab).EndsWith("vfx_foxmagician_impact.prefab"),
                    "FoxMagician tracer/impact presets point to the actual tracer/impact prefabs.");
                // 트레이서 프리팹 루트에 TracerProjectile이 있어야 프리팹의 비행 속도(_speed)가 쓰인다(없으면 기본 14로 새로 붙는다).
                var foxTracerComponent = expectedTracerPreset.VfxPrefab.GetComponent<Hexiege.Presentation.TracerProjectile>();
                Require(foxTracerComponent != null,
                    "FoxMagician tracer prefab root carries TracerProjectile (EffectManager reuses it instead of adding a default 14 speed).");
                float foxTracerSpeed = new SerializedObject(foxTracerComponent).FindProperty("_speed").floatValue;
                Require(foxTracerSpeed > 0f, "FoxMagician tracer prefab speed must be positive: " + foxTracerSpeed);
                // 이미 손을 떠난 마법탄·착탄은 시전자가 죽어도 끝까지 재생돼야 하므로 시전자 귀속이 꺼져 있어야 한다.
                Require(!expectedTracerPreset.StopWhenCasterDies && !expectedImpactPreset.StopWhenCasterDies,
                    "FoxMagician tracer/impact presets must not be caster-owned (Stop When Caster Dies OFF).");
            }
            Require(effectRows == 1, "UnitEffectConfig must contain exactly one FoxMagician row.");

            SerializedObject serializedPreset = new SerializedObject(expectedAttackPreset);
            SerializedProperty vfxPrefab = serializedPreset.FindProperty("_vfxPrefab");
            Require(vfxPrefab != null && vfxPrefab.objectReferenceValue == expectedVfxPrefab,
                "FoxMagician production attack preset must retain the exact VFX prefab.");
            Require(expectedVfxPrefab.GetComponentsInChildren<ParticleSystem>(true).Length > 0,
                "FoxMagician production VFX prefab must contain at least one ParticleSystem.");

            Require(UnitAttackShadowProfileResolver.TryResolve(
                    UnitType.FoxMagician, out UnitAttackShadowProfile fox)
                && fox.Support == UnitAttackShadowSupport.Unresolved
                && fox.TargetMode == AttackTargetMode.TargetLocked
                && fox.Delivery == AttackDeliveryKind.ProjectileImpact
                && fox.LegacyExecution == LegacyAttackExecutionKind.TimerImpact
                && fox.ExpectedImpactCount == 1
                && !fox.HasSecondaryResults
                && fox.Reason == "projectile-system-unresolved"
                && UnitAttackShadowProfileResolver.ClassifyAuditEligibility(
                    fox, runtimeContractValid: false)
                    == AttackPresentationAuditEligibility.KnownUnresolved,
                "FoxMagician must remain explicit KnownUnresolved ProjectileImpact with Legacy TimerImpact fallback.");
        }

        // AssetDatabase 입력은 위 production gate가 제공한다. CLR 호출은 읽은 데이터의
        // 실제 UseCase/HP 경계만 검증하며 native asset/Animator 실행으로 분류하지 않는다.
        internal static void ValidateFoxMagicianTimingAuthorityCore(UnitStatEntry entry, float[] extracted)
        {
            var dataField = typeof(UnitStats).GetField("_data", BindingFlags.Static | BindingFlags.NonPublic);
            var oldData = (IReadOnlyDictionary<UnitType, UnitStats.StatValues>)dataField.GetValue(null);
            var idField = typeof(UnitData).GetField("_nextId", BindingFlags.Static | BindingFlags.NonPublic);
            object oldId = idField.GetValue(null);
            bool oldActive = NetworkContext.IsNetworkActive, oldServer = NetworkContext.IsNetworkServer;
            IDisposable damage = null;
            try
            {
                NetworkContext.Set(false, false);
                UnitStats.Initialize(new Dictionary<UnitType, UnitStats.StatValues> {
                    { UnitType.FoxMagician, new UnitStats.StatValues {
                        MaxHp = entry.maxHp, AttackPower = entry.attackPower,
                        AttackRange = entry.attackRange, DetectRange = entry.detectRange,
                        MoveSpeed = entry.moveSpeed, AttackCooldown = entry.attackCooldown,
                        HitFrameTimes = entry.hitFrameTimes, Defense = entry.defense, IsHealer = entry.isHealer
                    } }
                });
                var grid = new HexGrid(3, 3, HexOrientation.FlatTop);
                var spawn = new UnitSpawnUseCase(grid);
                UnitData attacker = spawn.SpawnUnitWithId(9900, UnitType.FoxMagician, TeamId.Blue, new HexCoord(0, 0));
                UnitData victim = spawn.SpawnUnitWithId(9901, UnitType.FoxMagician, TeamId.Red, new HexCoord(1, 0));
                if (UnitFactory.ShouldUseAttackClipHitFrameTimes(attacker.Type, extracted.Length))
                    attacker.HitFrameTimes = extracted;
                Require(attacker.HitFrameTimes.Length == 1
                    && Math.Abs(attacker.HitFrameTimes[0] - 85f / 30f) <= 0.000001d,
                    "Production Fox asset/Factory input must select the serialized 85/30 marker.");
                var combat = new UnitCombatUseCase(grid, spawn, new BuildingPlacementUseCase(grid), null, new SoloTimingMapper());
                // [V-5 — 2026-10-21 서버 권위 투사체 단계 D] Fox는 이제 서버 투사체 유닛이다.
                //   85/30초(= 직렬화된 float marker)는 "피해 시각"이 아니라 "발사 시각"이고, 피해는 착탄 시각에 투사체 번호표와 함께 1회다.
                //   옛 본문(PendingHit 타이머가 85/30초에 즉시 표시 피해를 주던 검사)은 반대 검사가 되어 아래에 주석으로 보존한다:
                // double now = 0; int events = 0; bool immediate = false;
                // damage = GameEvents.OnEntityDamaged.Subscribe(e => {
                //     if (!ReferenceEquals(e.Entity, victim)) return;
                //     events++; immediate = e.ImmediatePresentation;
                //     Require(Math.Abs(now - 85f / 30f) <= 0.000001d,
                //         "Authority compares serialized float due, not the exact rational double.");
                // });
                // int hpBefore = victim.Hp;
                // Require(combat.TryAttack(attacker, now).HasValue, "Actual Fox TryAttack must reserve once.");
                // now = 2.25d; combat.TickCooldowns(2.25f, now); combat.TickPendingHits(2.25f, now);
                // Require(victim.Hp == hpBefore && events == 0, "Old decimal 2.25 must not apply Fox damage.");
                // double due = attacker.HitFrameTimes[0];
                // now = due - 0.000002d;
                // combat.TickPendingHits((float)(now - 2.25d), now);
                // Require(victim.Hp == hpBefore && events == 0, "Fox cannot hit before the serialized due.");
                // now = due; combat.TickPendingHits(0.000002f, now);
                // Require(victim.Hp < hpBefore && events == 1 && immediate,
                //     "Actual writer applies one captured-target hit and keeps default-key immediate presentation.");
                // combat.TickPendingHits(0f, now);
                // Require(events == 1, "Repeated due processing must not duplicate authoritative damage.");
                var launches = new List<ProjectileLaunchPresentation>();
                combat.ProjectileLaunched += launches.Add;
                double now = 0; int events = 0; double damageAt = double.NaN; EntityDamagedEvent hit = default;
                damage = GameEvents.OnEntityDamaged.Subscribe(e => {
                    if (!ReferenceEquals(e.Entity, victim)) return;
                    events++; damageAt = now; hit = e;
                });
                int hpBefore = victim.Hp;
                Require(combat.TryAttack(attacker, now).HasValue && combat.HasPendingProjectileLaunch(attacker),
                    "Actual Fox TryAttack must reserve one server projectile launch.");
                void Pump(double t) { now = t; combat.TickPendingHits(0f, now); combat.TickProjectiles(now); }
                Pump(2.25d);
                Require(victim.Hp == hpBefore && events == 0 && launches.Count == 0, "Old decimal 2.25 neither launches nor damages.");
                double due = attacker.HitFrameTimes[0];
                Pump(due - 0.000002d);
                Require(launches.Count == 0 && events == 0, "Fox cannot launch before the serialized 85/30 due.");
                Pump(due);
                Require(launches.Count == 1 && Math.Abs(launches[0].LaunchTime - due) <= 0.000001d
                        && victim.Hp == hpBefore && events == 0,
                    "Fox launches exactly at the serialized float due with no damage at launch.");
                // 착탄 시각은 서버가 발사 때 고정한 값을 그대로 쓴다(검증이 따로 계산하지 않는다 — ac05e189 교훈).
                double impactAt = launches[0].ImpactTime;
                Pump(impactAt - 0.000002d);
                Require(events == 0, "Fox deals no damage before the authoritative impact time.");
                Pump(impactAt);
                Require(victim.Hp < hpBefore && events == 1 && Math.Abs(damageAt - impactAt) <= 0.000001d
                        && !hit.ImmediatePresentation && hit.ProjectilePresentationKey.Equals(launches[0].Key),
                    "Actual writer applies one captured-target hit at impact with the projectile key and no immediate presentation (RM-5).");
                Pump(impactAt + 1d);
                Require(events == 1, "Repeated processing must not duplicate authoritative damage.");
            }
            finally
            {
                damage?.Dispose(); UnitStats.Initialize(oldData); idField.SetValue(null, oldId);
                NetworkContext.Set(oldServer, oldActive);
            }
        }

        private static void ValidateFoxMagicianMarkerCallbacks()
        {
            Require(!UnityEngine.Application.isPlaying, "Fox callback fixture must not run in a match.");
            // 실제 emitter 진입은 확인하지만 기존 씬의 pool/audio는 사용하지 않는다.
            // 등록 참조만 잠깐 격리하고 finally에서 동일 참조를 복원하며 씬 객체는 건드리지 않는다.
            var effectSingleton = typeof(Hexiege.Presentation.EffectManager).GetField(
                "<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var audioSingleton = typeof(SingletonMonoBehaviour<Hexiege.Presentation.AudioManager>).GetField(
                "<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            Require(effectSingleton != null && audioSingleton != null, "Fixture singleton isolation seam must exist.");
            object oldEffect = effectSingleton.GetValue(null), oldAudio = audioSingleton.GetValue(null);
            bool oldActive = NetworkContext.IsNetworkActive, oldServer = NetworkContext.IsNetworkServer;
            var idField = typeof(UnitData).GetField("_nextId", BindingFlags.Static | BindingFlags.NonPublic);
            object oldId = idField.GetValue(null);
            var obj = new GameObject("FoxMarkerCallbackFixture") { hideFlags = HideFlags.HideAndDontSave }; obj.SetActive(false);
            IDisposable localHit = null, damage = null;
            try
            {
                effectSingleton.SetValue(null, null); audioSingleton.SetValue(null, null);
                var view = obj.AddComponent<Hexiege.Presentation.UnitView>();
                var unit = new UnitData(9900, UnitType.FoxMagician, TeamId.Blue, new HexCoord(0, 0), 200, 150, 3, 3);
                typeof(Hexiege.Presentation.UnitView).GetField("_unitData", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, unit);
                var sourceField = typeof(Hexiege.Presentation.UnitView).GetField("_attackPresentationSourceMarkerLease", BindingFlags.Instance | BindingFlags.NonPublic);
                var modeField = typeof(Hexiege.Presentation.UnitView).GetField("_attackPresentationImpactMode", BindingFlags.Instance | BindingFlags.NonPublic);
                int localHits = 0, hpEvents = 0;
                localHit = GameEvents.OnLocalAttackHit.Subscribe(_ => localHits++);
                damage = GameEvents.OnEntityDamaged.Subscribe(_ => hpEvents++);
                var rotationProperty = typeof(Hexiege.Presentation.UnitView).GetProperty("AttackPhaseLastEmissionRotation");
                Require(rotationProperty?.GetSetMethod(true) != null, "Fixture rotation seed seam must exist.");
                // ── [T-1 — 2026-10-19 차지 유닛 발사 시점] 새 기준 ──────────────────────────────
                //   차지(OnAttackCharge) = lease 「확인만」(source·impact 둘 다 소비 없음) + 회차당 1회 + charge VFX만.
                //   타격(OnAttackHit) = lease 소비 + 투사체 발사 경로 진입(charge VFX 재생 없음 — 실제 EffectManager 검증은
                //   VfxConnections의 ValidateChargeUnitLaunchTiming이 담당한다. 이 fixture는 EffectManager·AudioManager를 격리한다).
                //   이 fixture의 UnitView에는 Animator가 없어 production 캐시(클립 이벤트 판별 — P2-1)를 계산할 수 없다.
                //   그래서 판별값만 직접 연다. 실제 클립에 차지 이벤트가 있는지는 T-3/T-4(ValidateChargeUnitClipSet)가 고정한다.
                var viewType = typeof(Hexiege.Presentation.UnitView);
                var chargeUnitField = viewType.GetField("_hasAttackChargeMarker", BindingFlags.Instance | BindingFlags.NonPublic);
                var chargeResolvedField = viewType.GetField("_attackChargeMarkerCacheResolved", BindingFlags.Instance | BindingFlags.NonPublic);
                var chargeRevisionField = viewType.GetField("_lastChargePresentedRevision", BindingFlags.Instance | BindingFlags.NonPublic);
                var acceptedRevisionField = viewType.GetField("_lastAttackPresentationRevision", BindingFlags.Instance | BindingFlags.NonPublic);
                var impactLeaseField = viewType.GetField("_attackPresentationImpactLease", BindingFlags.Instance | BindingFlags.NonPublic);
                var targetProperty = viewType.GetProperty("AttackPhaseLastEmissionTarget");
                Require(chargeUnitField != null && chargeResolvedField != null && chargeRevisionField != null
                        && acceptedRevisionField != null && impactLeaseField != null
                        && targetProperty?.GetSetMethod(true) != null,
                    "Charge-unit fixture seams (charge cache, revision fields, impact lease, emission target) must exist.");
                chargeUnitField.SetValue(view, true); chargeResolvedField.SetValue(view, true);
                const int unreachedTarget = 4242; // emitter가 실행되면 -1(차지는 recipient 없음)로 덮인다 → early return 증거
                AttackPresentationSourceMarkerLease Source() => (AttackPresentationSourceMarkerLease)sourceField.GetValue(view);
                AttackPresentationImpactLease Impact() => (AttackPresentationImpactLease)impactLeaseField.GetValue(view);
                void Commit(ulong revision, AttackPresentationImpactMode mode)
                {
                    // 서버 커밋 1회를 흉내 낸다: 수락 회차 + source lease 1회분(HitFrameTimes 1개) + 모드.
                    var armed = new AttackPresentationSourceMarkerLease();
                    Require(armed.TryArm(revision, 1), "Fixture source lease must arm.");
                    sourceField.SetValue(view, armed); modeField.SetValue(view, mode);
                    acceptedRevisionField.SetValue(view, revision);
                    impactLeaseField.SetValue(view, default(AttackPresentationImpactLease));
                }
                void SeedTarget() => targetProperty.GetSetMethod(true).Invoke(view, new object[] { unreachedTarget });

                foreach (Quaternion seed in new[] { default(Quaternion), Quaternion.identity })
                foreach (bool server in new[] { true, false })
                {
                    NetworkContext.Set(server, true);
                    Commit(1UL, AttackPresentationImpactMode.LegacyFallback);
                    chargeRevisionField.SetValue(view, 0UL);
                    rotationProperty.GetSetMethod(true).Invoke(view, new object[] { seed });
                    // [V-1 비활성화 — 2026-10-19 차지 유닛 발사 시점] 옛 기준(Fox 타격은 lease를 보존하고 아무것도 방출하지 않음).
                    //   새 기준에서 Fox 타격은 lease를 소비하고 투사체를 쏜다. 원래 검사는 지우지 않고 주석으로 남긴다.
                    // int remainingBefore = ((AttackPresentationSourceMarkerLease)sourceField.GetValue(view)).RemainingMarkerCount;
                    // var scopeBefore = view.AttackPhaseLastEmissionScope;
                    // int targetBefore = view.AttackPhaseLastEmissionTarget;
                    // Vector3 positionBefore = view.AttackPhaseLastEmissionTargetPosition;
                    // Quaternion before = view.AttackPhaseLastEmissionRotation;
                    // view.OnAttackHit(new AnimationEvent { time = 85f / 30f });
                    // // 2026-10-19 Fox fixture: zero Quaternion에도 정확한 성분 불변을 검사한다.
                    // Require(((AttackPresentationSourceMarkerLease)sourceField.GetValue(view)).RemainingMarkerCount == remainingBefore,
                    //     "Fox impact must preserve the charge lease count.");
                    // Require(view.AttackPhaseLastEmissionScope.Equals(scopeBefore)
                    //         && view.AttackPhaseLastEmissionTarget == targetBefore
                    //         && view.AttackPhaseLastEmissionTargetPosition.Equals(positionBefore)
                    //         && view.AttackPhaseLastEmissionRotation.Equals(before),
                    //     "Fox impact must preserve emission metadata components.");
                    // view.OnAttackCharge(new AnimationEvent { time = 1f });
                    // Require(((AttackPresentationSourceMarkerLease)sourceField.GetValue(view)).RemainingMarkerCount == 0
                    //         && view.AttackPhaseLastEmissionTarget == -1,
                    //     "Fox charge consumes its existing Host/Client source lease without a recipient.");
                    // view.OnAttackCharge(new AnimationEvent { time = 1f });
                    // Require(((AttackPresentationSourceMarkerLease)sourceField.GetValue(view)).RemainingMarkerCount == 0,
                    //     "Repeated charge cannot reacquire a consumed lease.");

                    // ① 차지: 확인만 한다(소비 없음) — 그래도 charge emitter까지는 도달한다(recipient -1).
                    SeedTarget();
                    view.OnAttackCharge(new AnimationEvent { time = 1f });
                    Require(Source().IsArmed && Source().RemainingMarkerCount == 1,
                        "Charge only checks the Host/Client source lease; the launch keeps the commit's single allowance.");
                    Require(view.AttackPhaseLastEmissionTarget == -1 && (ulong)chargeRevisionField.GetValue(view) == 1UL,
                        "Charge reaches the charge-VFX emitter without a recipient and records its presented revision.");
                    // ② 같은 회차 두 번째 차지(루프): 아무것도 방출·소비하지 않는다. emitter 앞 early return의 증거로
                    //    metadata 성분이 정확히 그대로인지 Equals로 본다(zero Quaternion seed 포함 — Quaternion == 는 쓰지 않는다).
                    rotationProperty.GetSetMethod(true).Invoke(view, new object[] { seed });
                    SeedTarget();
                    var scopeBefore = view.AttackPhaseLastEmissionScope;
                    Vector3 positionBefore = view.AttackPhaseLastEmissionTargetPosition;
                    view.OnAttackCharge(new AnimationEvent { time = 1f });
                    Require(Source().RemainingMarkerCount == 1
                            && view.AttackPhaseLastEmissionScope.Equals(scopeBefore)
                            && view.AttackPhaseLastEmissionTarget == unreachedTarget
                            && view.AttackPhaseLastEmissionTargetPosition.Equals(positionBefore)
                            && view.AttackPhaseLastEmissionRotation.Equals(seed),
                        "A second charge in the same revision emits nothing and consumes nothing.");
                    // ③ 타격: lease를 소비하고 발사 경로로 들어간다.
                    // [V-2 비활성화 — 2026-10-19] 옛 기준 「localHits == 0」(Fox 타격은 피격 신호 없음)은 아래 루프 밖에 주석으로 남겼다.
                    //   EffectManager가 격리돼 트레이서를 만들 수 없으므로, production은 같은 타격 순간 즉시 로컬 피격 신호로 폴백한다.
                    //   이 신호가 1회 나온 것이 「source-only return을 지나 발사 경로에 진입했다」는 증거다.
                    int hitsBefore = localHits;
                    view.OnAttackHit(new AnimationEvent { time = 85f / 30f });
                    Require(Source().RemainingMarkerCount == 0 && !Source().IsArmed,
                        "Fox launch consumes the commit's single source allowance.");
                    // [V-6 비활성화 — 2026-10-21 서버 권위 투사체 단계 D] Fox는 서버 투사체 유닛이라 타격 marker가 lease만 소비하고
                    //   로컬 트레이서·피격 신호를 내지 않는다(투사체는 서버 발사 기록이, 숫자는 서버 착탄 결과가 낸다). 옛 검사(주석 보존):
                    // Require(localHits == hitsBefore + 1,
                    //     "Fox launch enters the tracer/local-hit path once (EffectManager absent: immediate signal fallback).");
                    Require(localHits == hitsBefore,
                        "Fox launch marker (server projectile) consumes the lease but emits no local tracer/hit signal.");
                    // ④ 소비된 lease로는 다음 루프의 차지도 타격도 통과하지 못한다.
                    SeedTarget();
                    view.OnAttackCharge(new AnimationEvent { time = 1f });
                    view.OnAttackHit(new AnimationEvent { time = 85f / 30f });
                    // [V-6] 옛 조건 localHits == hitsBefore + 1(위 ③에서 1회 냈던 것)을 서버 투사체 기준 hitsBefore로 바꿨다.
                    Require(localHits == hitsBefore && view.AttackPhaseLastEmissionTarget == unreachedTarget,
                        "A consumed lease admits neither another charge nor another launch.");
                }

                // ⑤ Scoped(정규 scope) 경로에서도 차지는 impact lease를 소비하지 않는다 — R-3에서 확인한 두 번째 소비 지점.
                //   현재 두 차지 유닛은 LegacyFallback이라 impact lease를 쓰지 않지만, 나중에 Supported가 돼도 깨지지 않게 고정한다.
                NetworkContext.Set(false, true);
                Commit(2UL, AttackPresentationImpactMode.Scoped);
                var scopedImpact = new AttackPresentationImpactLease();
                Require(scopedImpact.TryArm(new AttackPresentationScope(
                        new AttackerInstanceId(77UL), new AttackSequenceId(5UL), 0), 1),
                    "Fixture impact lease must arm.");
                impactLeaseField.SetValue(view, scopedImpact);
                chargeRevisionField.SetValue(view, 0UL);
                view.OnAttackCharge(new AnimationEvent { time = 1f });
                Require(Impact().IsArmed && Source().RemainingMarkerCount == 1,
                    "Charge leaves both the source and the impact lease unconsumed (Scoped).");
                int scopedHitsBefore = localHits;
                view.OnAttackHit(new AnimationEvent { time = 85f / 30f });
                // [V-6] 옛 조건 localHits == scopedHitsBefore + 1 → 서버 투사체 유닛은 로컬 피격 신호 없음(scopedHitsBefore).
                Require(!Impact().IsArmed && Impact().Status == AttackPresentationImpactLeaseStatus.Consumed
                        && Source().RemainingMarkerCount == 0 && localHits == scopedHitsBefore,
                    "The launch, not the charge, consumes the Scoped impact HitIndex and the source allowance.");

                // ⑥ Stop이 먼저 온 경우(사용자 결정 D2): 차지를 시작하지 않고, SourceOnly 타격은 아무것도 내지 않는다.
                Commit(3UL, AttackPresentationImpactMode.LegacyFallback);
                chargeRevisionField.SetValue(view, 0UL);
                view.StopCombatAnimation(); // Client Stop → 남은 marker를 SourceOnly로 보존
                Require(Source().IsSourceOnly, "Client Stop preserves the committed marker as SourceOnly.");
                SeedTarget();
                view.OnAttackCharge(new AnimationEvent { time = 1f });
                Require(view.AttackPhaseLastEmissionTarget == unreachedTarget && Source().RemainingMarkerCount == 1,
                    "No charge starts after the attack was already stopped.");
                int sourceOnlyHitsBefore = localHits;
                view.OnAttackHit(new AnimationEvent { time = 85f / 30f });
                Require(Source().RemainingMarkerCount == 0 && localHits == sourceOnlyHitsBefore,
                    "A SourceOnly launch of a charge unit consumes its allowance but launches nothing.");

                NetworkContext.Set(false, false);
                int soloHitsBefore = localHits;
                view.OnAttackCharge(new AnimationEvent { time = 1f });
                view.OnAttackHit(new AnimationEvent { time = 85f / 30f });
                // [V-2 비활성화 — 2026-10-19 차지 유닛 발사 시점] 옛 검사: Fox 콜백은 피격 신호를 전혀 내지 않는다.
                // Require(localHits == 0 && hpEvents == 0 && unit.Hp == 200,
                //     "Actual SOLO/Host/Client callbacks never publish recipient/local-hit or apply HP.");
                // [V-6 비활성화 — 2026-10-21 단계 D] 옛 검사(주석 보존):
                // Require(localHits == soloHitsBefore + 1,
                //     "SOLO Fox launch (no lease) also enters the launch path at the hit marker, not at the charge.");
                Require(localHits == soloHitsBefore,
                    "SOLO Fox (server projectile) charge/hit markers emit no local hit signal; the server launch record draws the projectile.");
                Require(hpEvents == 0 && unit.Hp == 200,
                    "Actual SOLO/Host/Client charge/launch callbacks never apply HP (damage stays with the server timer).");
            }
            finally
            {
                localHit?.Dispose(); damage?.Dispose(); UnityEngine.Object.DestroyImmediate(obj);
                effectSingleton.SetValue(null, oldEffect); audioSingleton.SetValue(null, oldAudio);
                idField.SetValue(null, oldId); NetworkContext.Set(oldServer, oldActive);
            }
        }

        // [T-6 — 2026-10-19 차지 유닛 발사 시점] 옛 요약(기록용 보존):
        //   "InfernoSpirit의 현재 production은 별도 tracer 없이 30fps 1:15(1.50초) marker에서 공격 VFX와
        //    피격 표현을 함께 시작한다. 서버 fallback 설정과 marker가 다시 어긋나거나, 실제 투사체가
        //    없는 상태를 Supported로 가장하면 즉시 실패하도록 production 연결 전체를 확인한다."
        /// <summary>
        /// InfernoSpirit의 현재 production: 30fps 0:05(0.1667초) OnAttackCharge에서 기 모으기(charge) VFX를 켜고,
        /// 30fps 1:15(1.50초) OnAttackHit에서 화면 표현용 Legacy 트레이서(R5 임시)를 발사하며 공격 SFX를 낸다.
        /// 서버 피해는 OnAttackHit 1.50초만 추출한 TimerImpact다. 서버 fallback 설정과 marker가 다시 어긋나거나,
        /// 실제 권위 투사체가 없는 상태를 Supported로 가장하면 즉시 실패하도록 production 연결 전체를 확인한다.
        /// </summary>
        private static void ValidateInfernoSpiritProductionTimeline()
        {
            const string configPath = "Assets/_Project/Resources/Config/UnitStatsConfig.asset";
            const string controllerPath = "Assets/_Project/Animations/Units/InfernoSpirit/InfernoSpirit.controller";
            const string clipPath = "Assets/_Project/Animations/Units/InfernoSpirit/InfernoSpirit_Attack.anim";
            const string bluePrefabPath = "Assets/_Project/Prefabs/Units/Spirit/Unit_InfernoSpirit_Blue.prefab";
            const string redPrefabPath = "Assets/_Project/Prefabs/Units/Spirit/Unit_InfernoSpirit_Red.prefab";
            const string effectConfigPath = "Assets/_Project/Resources/Config/UnitEffectConfig.asset";
            const string attackPresetPath = "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_InfernoSpirit_Attack.asset";
            const string specialConfigPath = "Assets/_Project/Resources/Config/SpecialAttackConfig.asset";
            const string controllerGuid = "a683311d0333a3a4b8ec6aca335e1e2f";
            const string clipGuid = "aa08d04a7746a8c47a25e9ad6f9b7993";
            const float impactTime = 1.50f;
            const float tolerance = 0.0001f;

            UnitStatsConfig config = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(configPath);
            Require(config != null, "InfernoSpirit production UnitStatsConfig asset must exist.");
            int infernoRows = 0;
            UnitStatEntry infernoEntry = default;
            IReadOnlyList<UnitStatEntry> stats = config.Stats;
            for (int index = 0; index < stats.Count; index++)
            {
                if (stats[index].unitType != UnitType.InfernoSpirit) continue;
                infernoRows++;
                infernoEntry = stats[index];
            }
            Require(infernoRows == 1
                && infernoEntry.hitFrameTimes != null
                && infernoEntry.hitFrameTimes.Length == 1
                && Mathf.Abs(infernoEntry.hitFrameTimes[0] - impactTime) <= tolerance,
                "InfernoSpirit runtime hitFrameTimes must contain only the approved 1.50-second (30fps 1:15) impact.");

            Require(AssetDatabase.AssetPathToGUID(controllerPath) == controllerGuid,
                "InfernoSpirit production controller GUID must remain exact.");
            Require(AssetDatabase.AssetPathToGUID(clipPath) == clipGuid,
                "InfernoSpirit production Attack clip GUID must remain exact.");
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            Require(controller != null && attackClip != null,
                "InfernoSpirit production AnimatorController and Attack clip must exist.");

            AnimatorControllerLayer baseLayer = default;
            int baseLayerCount = 0;
            for (int index = 0; index < controller.layers.Length; index++)
            {
                if (controller.layers[index].name != "Base Layer") continue;
                baseLayerCount++;
                baseLayer = controller.layers[index];
            }
            Require(baseLayerCount == 1 && baseLayer.stateMachine != null,
                "InfernoSpirit controller must contain exactly one Base Layer.");

            AnimatorState attackState = null;
            int attackStateCount = 0;
            ChildAnimatorState[] states = baseLayer.stateMachine.states;
            for (int index = 0; index < states.Length; index++)
            {
                if (states[index].state == null || states[index].state.name != "Attack") continue;
                attackStateCount++;
                attackState = states[index].state;
            }
            Require(attackStateCount == 1 && attackState.motion == attackClip
                && Mathf.Abs(attackState.speed - 1f) <= tolerance,
                "InfernoSpirit Base Layer/Attack must use the exact verified clip at speed 1.");
            Require(Mathf.Abs(attackClip.length - 3f) <= tolerance
                && Mathf.Abs(attackClip.frameRate - 30f) <= tolerance
                && attackClip.isLooping,
                "InfernoSpirit Attack clip must remain 3 seconds at 30 fps and loop.");
            SerializedObject serializedAttackClip = new SerializedObject(attackClip);
            SerializedProperty clipSettings = serializedAttackClip.FindProperty("m_AnimationClipSettings");
            SerializedProperty orientationOffset = clipSettings != null
                ? clipSettings.FindPropertyRelative("m_OrientationOffsetY")
                : null;
            Require(orientationOffset != null
                && Mathf.Abs(orientationOffset.floatValue - (-58f)) <= tolerance,
                "InfernoSpirit Attack clip orientation offset must remain the currently approved -58-degree controlled comparison value.");

            int hitEventCount = 0;
            // [T-3 — 2026-10-19 차지 유닛 발사 시점] 「차지 시작」 신호 OnAttackCharge @ 30fps 0:05(= 5/30 = 0.1667초) 1개.
            //   Fox 타임라인 검사(OnAttackCharge @ 1초)와 같은 방식이다. 차지 VFX는 이 신호에서, 화염구 발사·발사음은 1.5초 OnAttackHit에서 난다.
            const float chargeMarkerTime = 5f / 30f;
            int chargeEventCount = 0;
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(attackClip);
            for (int index = 0; index < events.Length; index++)
            {
                if (events[index].functionName == "OnAttackCharge")
                {
                    chargeEventCount++;
                    Require(Mathf.Abs(events[index].time - chargeMarkerTime) <= tolerance,
                        "InfernoSpirit OnAttackCharge must remain at the approved 30fps 0:05 (0.1667-second) charge start.");
                    continue;
                }
                if (events[index].functionName != "OnAttackHit") continue;
                hitEventCount++;
                Require(Mathf.Abs(events[index].time - impactTime) <= tolerance,
                    "InfernoSpirit OnAttackHit must remain at the approved 1.50-second (30fps 1:15) impact.");
            }
            Require(hitEventCount == 1,
                "InfernoSpirit Attack clip must contain exactly one OnAttackHit event.");
            // 서버 피해 시점이 바뀌지 않았다는 확인: Factory가 추출하는 타격 시간은 [1.5] 하나뿐이고 차지 신호는 포함되지 않는다.
            float[] infernoExtracted = UnitFactory.GetHitFrameTimes(attackClip);
            Require(chargeEventCount == 1 && chargeMarkerTime < impactTime
                    && infernoExtracted.Length == 1 && Mathf.Abs(infernoExtracted[0] - impactTime) <= tolerance,
                "InfernoSpirit has one charge and one hit event, but Factory extracts only the 1.50-second damage event.");
            ValidateInfernoSpiritPrefabController(
                bluePrefabPath, controller, controllerGuid, expectedVfxRelativeX: -0.0085951f);
            ValidateInfernoSpiritPrefabController(
                redPrefabPath, controller, controllerGuid, expectedVfxRelativeX: -0.00859513f);

            // 현재 production에는 공격 VFX는 있지만 tracer가 없다. 이 사실을 명시적으로
            // 검증해야 1.50초 즉시 피격 폴백과 향후 실제 projectile 추가를 혼동하지 않는다.
            // [R5 임시 — 2026-10-19] 위 설명은 원래 계약이다. 지금은 사용자 승인으로 화면 표현용 Legacy
            //   트레이서/착탄 프리셋이 임시 연결되어 있고, 아래 행 검사가 그 임시 연결을 확인한다.
            //   서버 판정은 여전히 1.50초 TimerImpact 폴백이며 실제 권위 투사체(C 방식)는 아니다.
            var effectConfig = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.UnitEffectConfig>(effectConfigPath);
            var expectedAttackPreset = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.EffectPreset>(attackPresetPath);
            Require(effectConfig != null && expectedAttackPreset != null,
                "InfernoSpirit production UnitEffectConfig and attack preset must exist.");
            SerializedObject serializedEffects = new SerializedObject(effectConfig);
            SerializedProperty entries = serializedEffects.FindProperty("_entries");
            int effectRows = 0;
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty row = entries.GetArrayElementAtIndex(index);
                SerializedProperty rowUnitType = row.FindPropertyRelative("unitType");
                // enumValueIndex는 enum에 선언된 항목의 순서이므로, UnitType처럼 명시값 사이에 공백이 있으면
                // 실제 에셋에 직렬화된 숫자와 달라질 수 있다. intValue로 실제 직렬화 enum 값을 읽어야
                // InfernoSpirit의 명시값과 정확히 같은 effect 행만 선택할 수 있다.
                if (rowUnitType.intValue != (int)UnitType.InfernoSpirit) continue;
                effectRows++;
                Require(row.FindPropertyRelative("attackPreset").objectReferenceValue == expectedAttackPreset,
                    "InfernoSpirit must retain the verified production attack preset.");
                // [R5 비활성화 — 2026-10-19 사용자 승인으로 임시 트레이서 연결. C 방식(서버 투사체) 구현 시 재검토]
                //   예전에는 "트레이서 미구현"을 고정했지만, 사용자가 VFX 확인용으로 Legacy 트레이서(불덩이)와
                //   착탄(불꽃 폭발)을 임시 연결하기로 승인했다(StreamSpirit R5와 같은 방식).
                //   원래 검사는 지우지 않고 아래에 주석으로 남긴다.
                // Require(row.FindPropertyRelative("tracerPreset").objectReferenceValue == null,
                //     "InfernoSpirit tracer is not implemented; a newly wired tracer requires a separate Launch/Impact design.");
                // [R5 임시 — C 방식(서버 투사체) 구현 시 교체]
                //   트레이서 = 불덩이 프리셋, 착탄 = 불꽃 폭발 프리셋(공격자 기준 impact 슬롯),
                //   피격(hit) 슬롯은 "맞은 유닛 타입" 기준이라 비워 둔다(공격자 이펙트가 엉뚱한 유닛에 뜨지 않게).
                //   이 연결은 화면 표현만 바꾼다. 피해량·HP·화상(DoT)·서버 타격 시각(1.50초)은 그대로다.
                var expectedTracerPreset = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.EffectPreset>(
                    "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_InfernoSpirit_Tracer.asset");
                var expectedImpactPreset = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.EffectPreset>(
                    "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_InfernoSpirit_Impact.asset");
                Require(expectedTracerPreset != null && expectedImpactPreset != null
                    && row.FindPropertyRelative("tracerPreset").objectReferenceValue == expectedTracerPreset
                    && row.FindPropertyRelative("impactPreset").objectReferenceValue == expectedImpactPreset
                    && row.FindPropertyRelative("hitPreset").objectReferenceValue == null,
                    "[R5 interim] InfernoSpirit uses the interim Legacy tracer + attacker impact preset, and its victim-keyed hit slot stays empty.");
                // 프리셋 파일 이름만 맞고 안에 다른 프리팹이 들어가 있는 실수를 막으려고, 실제 프리팹 경로도 확인한다.
                Require(expectedTracerPreset.VfxPrefab != null && expectedImpactPreset.VfxPrefab != null
                    && AssetDatabase.GetAssetPath(expectedTracerPreset.VfxPrefab).EndsWith("vfx_infernospirit_tracer.prefab")
                    && AssetDatabase.GetAssetPath(expectedImpactPreset.VfxPrefab).EndsWith("vfx_infernospirit_impact.prefab"),
                    "[R5 interim] InfernoSpirit tracer/impact presets point to the actual tracer/impact prefabs.");
                // 트레이서 프리팹 루트에 TracerProjectile이 미리 붙어 있어야 그 프리팹의 비행 속도(_speed)가 쓰인다.
                //   붙어 있지 않으면 EffectManager가 실행 중에 기본값(14)으로 새로 붙이므로, 의도한 속도가 무시된다.
                var infernoTracerComponent = expectedTracerPreset.VfxPrefab
                    .GetComponent<Hexiege.Presentation.TracerProjectile>();
                Require(infernoTracerComponent != null,
                    "[R5 interim] InfernoSpirit tracer prefab root carries TracerProjectile (EffectManager reuses it instead of adding a default 14 speed).");
                // _speed는 private 직렬화 필드라 SerializedObject로 인스펙터 값을 그대로 읽는다.
                //   0 이하이면 TracerProjectile이 비행 시간을 0으로 계산해 날아가는 모습 없이 바로 도착한다.
                float infernoTracerSpeed = new SerializedObject(infernoTracerComponent).FindProperty("_speed").floatValue;
                Require(infernoTracerSpeed > 0f,
                    "[R5 interim] InfernoSpirit tracer prefab speed must be positive: " + infernoTracerSpeed);
                // 시전자 귀속(Stop When Caster Dies)은 기 모으기(charge) 공격 이펙트 전용이다.
                //   트레이서는 이미 손을 떠나 날아가는 불덩이이므로, 시전자가 죽어도 끝까지 날아가야 해서 OFF여야 한다.
                //   (attackPreset의 ON 검사는 VfxConnections의 ValidateCasterOwnedChargeVfx가 그대로 담당한다.)
                Require(!expectedTracerPreset.StopWhenCasterDies,
                    "[R5 interim] InfernoSpirit tracer preset must not be caster-owned (Stop When Caster Dies OFF).");
            }
            Require(effectRows == 1, "UnitEffectConfig must contain exactly one InfernoSpirit row.");

            SpecialAttackConfig specialConfig = AssetDatabase.LoadAssetAtPath<SpecialAttackConfig>(specialConfigPath);
            Require(specialConfig != null
                && Mathf.Abs(specialConfig.InfernoDotPerSecond - 50f) <= tolerance
                && Mathf.Abs(specialConfig.InfernoDotDuration - 3f) <= tolerance,
                "InfernoSpirit production DoT must remain explicitly serialized as 50 per second for 3 seconds.");

            Require(UnitAttackShadowProfileResolver.TryResolve(
                    UnitType.InfernoSpirit, out UnitAttackShadowProfile inferno)
                && inferno.Support == UnitAttackShadowSupport.Unresolved
                && inferno.Delivery == AttackDeliveryKind.ProjectileImpact
                && inferno.LegacyExecution == LegacyAttackExecutionKind.TimerImpactWithSecondaryEffect
                && inferno.ExpectedImpactCount == 1
                && inferno.HasSecondaryResults
                && inferno.Reason == "projectile-and-periodic-result-unresolved",
                "InfernoSpirit must remain explicitly Unresolved ProjectileImpact with periodic secondary results.");
        }

        /// <summary>
        /// [T-4·T-5 — 2026-10-19 차지 유닛 발사 시점]
        /// T-4: 「차지 유닛」은 코드의 타입 목록이 아니라 공격 클립의 OnAttackCharge 이벤트로 판별된다(UnitView P2-1).
        ///      그래서 이벤트를 빠뜨리거나 엉뚱한 클립에 넣으면 조용히 일반 유닛처럼 동작한다. 이 검사가 그 집합을
        ///      {FoxMagician_Attack, InfernoSpirit_Attack} 두 개로 고정하고, 두 유닛의 charge 프리셋이 시전자 귀속(정지 가능)인지 확인한다.
        /// T-5: charge VFX는 「차지 시작」부터 「발사」까지 점점 모이다가 발사 순간 끝나야 한다. 각 파티클 시스템이
        ///      실제로 끝나는 시각(시작 지연 + 마지막 방출 시각 + 최대 수명)의 최댓값이 차지 구간(클립의 hit − charge)
        ///      이하이면서, 구간 끝 1프레임(1/30초) 안까지는 이어지는지 확인한다.
        ///      ※ Plan T-5 문구 「최대 Duration + 최대 Lifetime」은 연속 방출 시스템의 보수적 상한이다. 시작 순간 burst만 쓰는
        ///        시스템(두 프리팹의 루트)은 마지막 방출이 0초라 실제 끝 = Lifetime이므로, 방출 방식별 실제 끝 시각으로 계산한다.
        /// </summary>
        private static void ValidateChargeUnitClipSetAndChargeLength()
        {
            const string foxClipPath = "Assets/_Project/Animations/Units/FoxMagician/FoxMagician_Attack.anim";
            const string infernoClipPath = "Assets/_Project/Animations/Units/InfernoSpirit/InfernoSpirit_Attack.anim";

            // ── T-4: OnAttackCharge가 있는 「_Attack」 클립 집합 ──
            var chargeClipPaths = new List<string>();
            string[] clipGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/_Project/Animations/Units" });
            for (int g = 0; g < clipGuids.Length; g++)
            {
                string path = AssetDatabase.GUIDToAssetPath(clipGuids[g]);
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var clip = asset as AnimationClip;
                    // FBX 안의 미리보기 클립(__preview__)은 UnitView가 고르는 공격 클립이 아니다.
                    if (clip == null || clip.name.StartsWith("__preview__", StringComparison.Ordinal)
                        || !clip.name.EndsWith("_Attack", StringComparison.Ordinal)) continue;
                    foreach (AnimationEvent clipEvent in AnimationUtility.GetAnimationEvents(clip))
                        if (clipEvent.functionName == "OnAttackCharge")
                        {
                            if (!chargeClipPaths.Contains(path)) chargeClipPaths.Add(path);
                            break;
                        }
                }
            }
            Require(chargeClipPaths.Count == 2 && chargeClipPaths.Contains(foxClipPath) && chargeClipPaths.Contains(infernoClipPath),
                "Charge units (Attack clips carrying OnAttackCharge) must be exactly FoxMagician and InfernoSpirit: "
                + string.Join(", ", chargeClipPaths));

            ValidateChargeUnitChargeLength("FoxMagician", foxClipPath,
                "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_FoxMagician_Attack.asset",
                "vfx_foxmagician_charge.prefab");
            ValidateChargeUnitChargeLength("InfernoSpirit", infernoClipPath,
                "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_InfernoSpirit_Attack.asset",
                "vfx_infernospirit_charge.prefab");
        }

        /// <summary>
        /// [T-4·T-5] 한 차지 유닛의 charge 프리셋(시전자 귀속)과 charge 프리팹 길이를 클립의 차지 구간과 비교한다.
        /// </summary>
        private static void ValidateChargeUnitChargeLength(string unitName, string clipPath, string attackPresetPath, string prefabFileName)
        {
            const float tolerance = 0.001f;   // float 직렬화 오차(1ms)만 허용한다
            const float oneFrame = 1f / 30f;  // 차지는 발사 1프레임 전까지는 보여야 한다
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            Require(clip != null, unitName + " Attack clip must exist for the charge-length check.");
            float chargeTime = float.NaN, hitTime = float.NaN;
            foreach (AnimationEvent clipEvent in AnimationUtility.GetAnimationEvents(clip))
            {
                if (clipEvent.functionName == "OnAttackCharge") chargeTime = clipEvent.time;
                else if (clipEvent.functionName == "OnAttackHit") hitTime = clipEvent.time;
            }
            Require(!float.IsNaN(chargeTime) && !float.IsNaN(hitTime) && chargeTime < hitTime,
                unitName + " charge marker must precede its launch (OnAttackHit) marker.");
            float window = hitTime - chargeTime;

            var preset = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.EffectPreset>(attackPresetPath);
            Require(preset != null && preset.VfxPrefab != null
                    && AssetDatabase.GetAssetPath(preset.VfxPrefab).EndsWith(prefabFileName, StringComparison.Ordinal),
                unitName + " attack preset must use the actual charge prefab " + prefabFileName + ".");
            // 취소·사망 때 즉시 끌 수 있는 것은 시전자 귀속 프리셋뿐이다(UnitView.StopOwnedChargeEffects → EffectManager).
            Require(preset.StopWhenCasterDies,
                unitName + " charge preset must be caster-owned so a cancelled or dead caster's charge stops at once.");

            float latestEnd = 0f;
            foreach (ParticleSystem ps in preset.VfxPrefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                float end = ChargeParticleSystemEndSeconds(unitName, ps);
                Require(end <= window + tolerance,
                    $"{unitName} charge particle system '{ps.name}' must end by the launch: end={end:F4}s, window={window:F4}s.");
                latestEnd = Mathf.Max(latestEnd, end);
            }
            Require(latestEnd >= window - oneFrame - tolerance,
                $"{unitName} charge must keep building until the launch frame: latestEnd={latestEnd:F4}s, window={window:F4}s.");
        }

        /// <summary>
        /// [T-5] 파티클 시스템 하나가 재생 시작부터 마지막 입자가 사라질 때까지 걸리는 시간(초)을 직렬화 값으로 계산한다.
        /// 끝 = (시작 지연 + 마지막 방출 시각 + 최대 수명) ÷ 재생 속도.
        /// 마지막 방출 시각: 초당/거리당 방출이 있으면 Duration 끝까지, burst만 있으면 마지막 burst 시각.
        /// 루프·무한 반복 burst·곡선(Curve) 수명처럼 이 계산으로 끝을 정할 수 없는 설정은 실패로 알린다(검사를 고쳐야 한다는 신호).
        /// </summary>
        private static float ChargeParticleSystemEndSeconds(string unitName, ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            Require(!main.loop, $"{unitName} charge particle system '{ps.name}' must not loop (it has to end at the launch).");
            bool ConstantMode(ParticleSystem.MinMaxCurve curve)
                => curve.mode == ParticleSystemCurveMode.Constant || curve.mode == ParticleSystemCurveMode.TwoConstants;
            Require(ConstantMode(main.startDelay) && ConstantMode(main.startLifetime),
                $"{unitName} charge particle system '{ps.name}' start delay/lifetime must be constants for the length check.");
            Require(main.simulationSpeed > 0f, $"{unitName} charge particle system '{ps.name}' must have a positive simulation speed.");
            float duration = main.duration;
            float lastEmission = 0f;
            ParticleSystem.EmissionModule emission = ps.emission;
            if (emission.enabled)
            {
                float RateScale(ParticleSystem.MinMaxCurve curve)
                    => ConstantMode(curve) ? curve.constantMax : curve.curveMultiplier;
                if (RateScale(emission.rateOverTime) > 0f || RateScale(emission.rateOverDistance) > 0f)
                    lastEmission = duration;
                for (int index = 0; index < emission.burstCount; index++)
                {
                    ParticleSystem.Burst burst = emission.GetBurst(index);
                    Require(burst.cycleCount >= 1,
                        $"{unitName} charge particle system '{ps.name}' burst {index} must not repeat forever.");
                    float burstLast = burst.time + (burst.cycleCount - 1) * burst.repeatInterval;
                    Require(burst.time <= duration + 0.0001f,
                        $"{unitName} charge particle system '{ps.name}' burst {index} must fire inside its Duration.");
                    lastEmission = Mathf.Max(lastEmission, Mathf.Min(burstLast, duration));
                }
            }
            return (main.startDelay.constantMax + lastEmission + main.startLifetime.constantMax) / main.simulationSpeed;
        }

        private static void ValidateInfernoSpiritAnimatedFacingEvidence()
        {
            Vector3 target = Vector3.forward;
            Require(UnitAttackShadowObserver.TryCalculateAnimatedFacingSignedYawForValidation(
                        target, Vector3.forward, out double zeroYaw)
                    && Math.Abs(zeroYaw) <= 0.001d,
                "InfernoSpirit body-facing evidence must classify equal target/forward directions as zero yaw.");

            Vector3 plus45 = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;
            Vector3 minus45 = Quaternion.Euler(0f, -45f, 0f) * Vector3.forward;
            Vector3 opposite = Quaternion.Euler(0f, 180f, 0f) * Vector3.forward;
            Require(UnitAttackShadowObserver.TryCalculateAnimatedFacingSignedYawForValidation(
                        target, plus45, out double plus45Yaw)
                    && Math.Abs(plus45Yaw - 45d) <= 0.001d
                    && UnitAttackShadowObserver.TryCalculateAnimatedFacingSignedYawForValidation(
                        target, minus45, out double minus45Yaw)
                    && Math.Abs(minus45Yaw - (-45d)) <= 0.001d
                    && UnitAttackShadowObserver.TryCalculateAnimatedFacingSignedYawForValidation(
                        target, opposite, out double oppositeYaw)
                    && Math.Abs(Math.Abs(oppositeYaw) - 180d) <= 0.001d,
                "InfernoSpirit body-facing signed yaw must preserve +45/-45 signs and the 180-degree boundary.");

            Require(!UnitAttackShadowObserver.TryCalculateAnimatedFacingSignedYawForValidation(
                        Vector3.zero, Vector3.forward, out _)
                    && !UnitAttackShadowObserver.TryCalculateAnimatedFacingSignedYawForValidation(
                        new Vector3(float.NaN, 0f, 1f), Vector3.forward, out _)
                    && !UnitAttackShadowObserver.TryCalculateAnimatedFacingSignedYawForValidation(
                        Vector3.forward, new Vector3(float.PositiveInfinity, 0f, 1f), out _)
                    && !UnitAttackShadowObserver.IsAnimatedRenderedHipsEvidenceValidForValidation(
                        false, true, true)
                    && UnitAttackShadowObserver.IsAnimatedRenderedHipsEvidenceValidForValidation(
                        true, true, true),
                "InfernoSpirit rendered-Hips facing evidence must fail closed for zero/non-finite vectors and unavailable cached rootBone data.");

            Require(UnitAttackShadowObserver.IsAnimatedAttackFacingStageForValidation(
                        AnimatedAttackFacingStage.AttackStartPose)
                    && UnitAttackShadowObserver.IsAnimatedAttackFacingStageForValidation(
                        AnimatedAttackFacingStage.ImpactMarker)
                    && UnitAttackShadowObserver.IsAnimatedAttackFacingStageForValidation(
                        AnimatedAttackFacingStage.AttackEndPose)
                    && !UnitAttackShadowObserver.IsAnimatedAttackFacingStageForValidation(
                        (AnimatedAttackFacingStage)0)
                    && !UnitAttackShadowObserver.IsAnimatedAttackFacingStageForValidation(
                        (AnimatedAttackFacingStage)4),
                "InfernoSpirit body-facing evidence must accept exactly the start/impact/end stages.");

            var keys = new HashSet<string>();
            Require(UnitAttackShadowObserver.TryAcceptAnimatedFacingEvidenceKeyForValidation(
                        keys, "attack-1:start", 2)
                    && !UnitAttackShadowObserver.TryAcceptAnimatedFacingEvidenceKeyForValidation(
                        keys, "attack-1:start", 2)
                    && UnitAttackShadowObserver.TryAcceptAnimatedFacingEvidenceKeyForValidation(
                        keys, "attack-1:impact", 2)
                    && !UnitAttackShadowObserver.TryAcceptAnimatedFacingEvidenceKeyForValidation(
                        keys, "attack-1:end", 2),
                "InfernoSpirit body-facing evidence must suppress exact duplicates and fail closed at its bounded key capacity.");
        }

        private static void ValidateInfernoSpiritMotionJumpEvidence()
        {
            Require(UnitAttackShadowObserver.TryCalculateInfernoMotionAllowanceForValidation(
                        2f, 0.02f, 0.05f,
                        out float rawAllowance,
                        out float nominalAllowance,
                        out bool normalFrameIsLong)
                    && Mathf.Abs(rawAllowance - 0.09f) <= 0.0001f
                    && Mathf.Abs(nominalAllowance - 0.09f) <= 0.0001f
                    && !normalFrameIsLong
                    && !UnitAttackShadowObserver.TryCalculateInfernoMotionAllowanceForValidation(
                        float.NaN, 0.02f, 0.05f, out _, out _, out _)
                    && !UnitAttackShadowObserver.TryCalculateInfernoMotionAllowanceForValidation(
                        2f, 0f, 0.05f, out _, out _, out _),
                "InfernoSpirit motion-jump allowance must use speed*deltaTime+tolerance and reject non-finite/non-positive timing evidence.");

            Require(UnitAttackShadowObserver.ClassifyInfernoMotionJumpForValidation(
                        true, true, 0.05f, true, 0.05f, true, 0.05f,
                        0.09f, 0.09f, 0.05f, false,
                        false, false, false, false)
                    == InfernoMotionJumpClassification.None,
                "InfernoSpirit motion-jump evidence must emit no event for a normal frame within allowance.");
            Require(UnitAttackShadowObserver.ClassifyInfernoMotionJumpForValidation(
                        true, true, 0.20f, true, 0.20f, true, 0.20f,
                        0.09f, 0.09f, 0.05f, false,
                        false, false, false, false)
                    == InfernoMotionJumpClassification.AuthoritativeRootJump,
                "InfernoSpirit Host Simulation Root excess must classify as authoritative-root-jump.");
            Require(UnitAttackShadowObserver.ClassifyInfernoMotionJumpForValidation(
                        true, true, 0.05f, true, 0.20f, true, 0.20f,
                        0.09f, 0.09f, 0.05f, false,
                        false, false, false, false)
                    == InfernoMotionJumpClassification.VisualProjectionJump,
                "InfernoSpirit Visual-only excess must classify as visual-projection-jump.");
            Require(UnitAttackShadowObserver.ClassifyInfernoMotionJumpForValidation(
                        true, true, 0.05f, true, 0.05f, true, 0.20f,
                        0.09f, 0.09f, 0.05f, false,
                        false, false, false, false)
                    == InfernoMotionJumpClassification.PresentationAnchorJump,
                "InfernoSpirit rendered-anchor-only excess must classify as presentation-anchor-jump.");

            Require(UnitAttackShadowObserver.TryCalculateInfernoMotionAllowanceForValidation(
                        3f, 0.20f, 0.05f,
                        out float longRawAllowance,
                        out float longNominalAllowance,
                        out bool longFrame)
                    && longFrame
                    && UnitAttackShadowObserver.ClassifyInfernoMotionJumpForValidation(
                        true, true, 0.30f, true, 0.30f, true, 0.30f,
                        longRawAllowance, longNominalAllowance, 0.05f, true,
                        false, false, false, false)
                    == InfernoMotionJumpClassification.LongFrameCatchUp,
                "InfernoSpirit large delta inside raw long-frame allowance must remain long-frame-catch-up instead of a jump.");

            Require(UnitAttackShadowObserver.ClassifyInfernoMotionJumpForValidation(
                        true, true, 0.20f, true, 0.20f, true, 0.20f,
                        0.09f, 0.09f, 0.05f, false,
                        true, false, false, false)
                    == InfernoMotionJumpClassification.UnavailableAmbiguous
                    && UnitAttackShadowObserver.ClassifyInfernoMotionJumpForValidation(
                        true, true, 0.20f, true, 0.20f, true, 0.20f,
                        0.09f, 0.09f, 0.05f, false,
                        true, true, false, true)
                    == InfernoMotionJumpClassification.ClientOnlyReplicationGap
                    && UnitAttackShadowObserver.ClassifyInfernoMotionJumpForValidation(
                        false, true, 0.20f, false, float.NaN, false, float.NaN,
                        0.09f, 0.09f, 0.05f, false,
                        false, false, false, false)
                    == InfernoMotionJumpClassification.UnavailableAmbiguous,
                "InfernoSpirit client-only classification must require peer plus replication-gap evidence and invalid samples must fail closed as unavailable-ambiguous.");

            Require(UnitAttackShadowObserver.IsInfernoRenderedAnchorEvidenceValidForValidation(
                        true, true, true, true, true)
                    && !UnitAttackShadowObserver.IsInfernoRenderedAnchorEvidenceValidForValidation(
                        false, true, true, true, true)
                    && !UnitAttackShadowObserver.IsInfernoRenderedAnchorEvidenceValidForValidation(
                        true, true, false, true, true)
                    && !UnitAttackShadowObserver.IsInfernoRenderedAnchorEvidenceValidForValidation(
                        true, true, true, false, true)
                    && !UnitAttackShadowObserver.IsInfernoRenderedAnchorEvidenceValidForValidation(
                        true, true, true, true, false),
                "InfernoSpirit rendered anchor must require exactly one renderer, a finite rootBone, and exact Animator Hips identity.");
            Require(!UnitAttackShadowObserver.ShouldRecordInfernoMotionPairForValidation(
                        false, false, true)
                    && !UnitAttackShadowObserver.ShouldRecordInfernoMotionPairForValidation(
                        true, false, true)
                    && UnitAttackShadowObserver.ShouldRecordInfernoMotionPairForValidation(
                        true, true, true)
                    && UnitAttackShadowObserver.ShouldRecordInfernoMotionPairForValidation(
                        true, true, false),
                "InfernoSpirit first valid rendered-anchor sample must establish a baseline without emitting an event or consuming budget.");
            Require(UnitAttackShadowObserver.InfernoMotionObserverIsReadOnlyForValidation(),
                "InfernoSpirit motion-jump observer must remain a read-only evidence boundary.");

            Require(UnitAttackShadowObserver.TryCalculateInfernoArcEvidenceForValidation(
                        1f, 0f, 0f, 0.05f,
                        out float zeroArc, out float zeroResidual, out bool zeroMatches)
                    && Mathf.Abs(zeroArc) <= 0.0001f
                    && Mathf.Abs(zeroResidual) <= 0.0001f
                    && zeroMatches
                    && UnitAttackShadowObserver.TryCalculateInfernoArcEvidenceForValidation(
                        1f, 90f, Mathf.Sqrt(2f), 0.0001f,
                        out float rightAngleArc, out _, out bool rightAngleMatches)
                    && Mathf.Abs(rightAngleArc - Mathf.Sqrt(2f)) <= 0.0001f
                    && rightAngleMatches
                    && UnitAttackShadowObserver.TryCalculateInfernoArcEvidenceForValidation(
                        1f, 180f, 2f, 0.0001f,
                        out float oppositeArc, out _, out bool oppositeMatches)
                    && Mathf.Abs(oppositeArc - 2f) <= 0.0001f
                    && oppositeMatches,
                "InfernoSpirit arc prediction must implement 2*r*sin(delta/2) at 0/90/180-degree boundaries.");
            Require(!UnitAttackShadowObserver.TryCalculateInfernoArcEvidenceForValidation(
                        float.NaN, 90f, 1f, 0.05f, out _, out _, out _)
                    && !UnitAttackShadowObserver.TryCalculateInfernoArcEvidenceForValidation(
                        1f, float.PositiveInfinity, 1f, 0.05f, out _, out _, out _)
                    && !UnitAttackShadowObserver.TryCalculateInfernoArcEvidenceForValidation(
                        1f, 181f, 1f, 0.05f, out _, out _, out _)
                    && !UnitAttackShadowObserver.TryCalculateInfernoArcEvidenceForValidation(
                        1f, 90f, -1f, 0.05f, out _, out _, out _),
                "InfernoSpirit arc evidence must fail closed for non-finite, negative, or out-of-range inputs.");
            Require(UnitAttackShadowObserver.TryCalculateInfernoArcEvidenceForValidation(
                        1f, 0f, 0.05f, 0.05f, out _, out float boundaryResidual, out bool boundaryMatches)
                    && Mathf.Abs(boundaryResidual - 0.05f) <= 0.0001f
                    && boundaryMatches
                    && UnitAttackShadowObserver.TryCalculateInfernoArcEvidenceForValidation(
                        1f, 0f, 0.0501f, 0.05f, out _, out _, out bool beyondMatches)
                    && !beyondMatches,
                "InfernoSpirit residual matching must accept the named tolerance boundary and reject values immediately beyond it.");
            Require(UnitAttackShadowObserver.TryCalculatePlanarRotationDeltaForValidation(
                        Quaternion.identity,
                        Quaternion.Euler(0f, 90f, 0f),
                        out float planarRightAngle)
                    && Mathf.Abs(planarRightAngle - 90f) <= 0.001f
                    && !UnitAttackShadowObserver.TryCalculatePlanarRotationDeltaForValidation(
                        new Quaternion(float.NaN, 0f, 0f, 1f),
                        Quaternion.identity,
                        out _),
                "InfernoSpirit planar rotation evidence must preserve yaw and reject non-finite quaternions.");

            string compactWorstCase = UnitAttackShadowObserver.BuildInfernoMotionCompactEventForValidation(
                "presentation-anchor-jump", "client", int.MinValue.ToString(), ulong.MaxValue.ToString(),
                ulong.MaxValue.ToString(), "2147483646->2147483647",
                "9999999999.999999->9999999999.999999", "WaitingRepath", "True",
                "999999.9999", "999999.9999", "999999.9999", "999999.9999",
                "180.0000", "180.0000", "999999.9999", "999999.9999", "+999999.9999", "False",
                "arcObserved=999999.9999, offsetNow=999999.9999, boundsD=999999.9999, long=True, nomAllow=999999.9999, cmd=18446744073709551615, seg=18446744073709551615, sem=18446744073709551615");
            string[] requiredCompactFields =
            {
                "classification=", "role=", "unit=", "network=", "lifecycle=", "frame=", "time=", "phase=",
                "walkAttackTransition=", "allowance=", "simDelta=", "visualDelta=", "anchorDelta=",
                "simRotationDelta=", "visualRotationDelta=", "rootToAnchorOffset=", "predictedArc=", "residual=", "arcMatch="
            };
            int previousFieldIndex = -1;
            bool compactFieldsPresentInOrder = true;
            for (int index = 0; index < requiredCompactFields.Length; index++)
            {
                int fieldIndex = compactWorstCase.IndexOf(requiredCompactFields[index], StringComparison.Ordinal);
                compactFieldsPresentInOrder &= fieldIndex > previousFieldIndex;
                previousFieldIndex = fieldIndex;
            }
            Require(compactFieldsPresentInOrder
                    && UnitAttackShadowObserver.GetInfernoMotionCompactWorstCaseUtf8BytesForValidation(compactWorstCase) < 1000,
                "InfernoSpirit compact motion event must keep every required field in front-order and the worst-case full Android line below 1000 UTF-8 bytes.");

            var limiter = new UnitAttackShadowObserver.InfernoMotionJumpLimiterForValidation(
                unitCapacity: 1,
                perUnitCapacity: 2,
                keyCapacity: 2);
            Require(limiter.TryAccept(12, 1UL, 100, InfernoMotionJumpClassification.PresentationAnchorJump)
                        == "Accepted"
                    && limiter.TryAccept(12, 1UL, 100, InfernoMotionJumpClassification.PresentationAnchorJump)
                        == "Duplicate"
                    && limiter.TryAccept(12, 1UL, 101, InfernoMotionJumpClassification.VisualProjectionJump)
                        == "Overflow"
                    && limiter.TryAccept(
                        12, 1UL, 102,
                        InfernoMotionJumpClassification.PresentationAnchorJump,
                        walkAttackTransition: true) == "Accepted"
                    && limiter.TryAccept(-1, 1UL, 103, InfernoMotionJumpClassification.PresentationAnchorJump)
                        == "Invalid",
                "InfernoSpirit motion-jump limiter must dedupe exact keys, enforce bounded overflow, and preserve the first severe Walk/Attack transition after general capacity is exhausted.");
            limiter.Retire(12, 1UL);
            Require(limiter.UnitCount == 0 && limiter.KeyCount == 0
                    && limiter.TryAccept(12, 2UL, 100, InfernoMotionJumpClassification.PresentationAnchorJump)
                        == "Accepted",
                "InfernoSpirit motion-jump retire must remove prior lifecycle state before unit reuse.");
            limiter.Reset();
            Require(limiter.UnitCount == 0 && limiter.KeyCount == 0,
                "InfernoSpirit motion-jump session reset must clear all bounded state.");

            // Production 상한(64 units × unit별 8 events, 전체 key 128)을 그대로 사용한다.
            // 한 유닛의 일반 사건 7건이 먼저 와도 8번째 일반 사건은 거부되고, 뒤늦게 도착한 첫
            // Walk↔Attack severe 사건은 예약된 마지막 한 칸에 반드시 들어가야 한다.
            var productionLimiter =
                new UnitAttackShadowObserver.InfernoMotionJumpLimiterForValidation(
                    unitCapacity: 64,
                    perUnitCapacity: 8,
                    keyCapacity: 128);
            bool sevenGeneralAccepted = true;
            for (int index = 0; index < 7; index++)
            {
                sevenGeneralAccepted &= productionLimiter.TryAccept(
                        12,
                        1UL,
                        100 + index,
                        InfernoMotionJumpClassification.VisualProjectionJump)
                    == "Accepted";
            }
            Require(sevenGeneralAccepted
                    && productionLimiter.TryAccept(
                        12, 1UL, 107,
                        InfernoMotionJumpClassification.VisualProjectionJump) == "Overflow"
                    && productionLimiter.TryAccept(
                        12, 1UL, 108,
                        InfernoMotionJumpClassification.PresentationAnchorJump,
                        walkAttackTransition: true) == "Accepted"
                    && productionLimiter.KeyCount == 8,
                "Production motion-jump limiter must reserve one per-unit slot: seven general events accepted, eighth general overflow, then first severe transition accepted.");

            bool remainingSevereAccepted = true;
            for (int index = 0; index < 63; index++)
            {
                remainingSevereAccepted &= productionLimiter.TryAccept(
                        100 + index,
                        1UL,
                        200 + index,
                        InfernoMotionJumpClassification.PresentationAnchorJump,
                        walkAttackTransition: true)
                    == "Accepted";
            }
            bool remainingGeneralAccepted = true;
            for (int index = 0; index < 57; index++)
            {
                remainingGeneralAccepted &= productionLimiter.TryAccept(
                        100 + index,
                        1UL,
                        300 + index,
                        InfernoMotionJumpClassification.VisualProjectionJump)
                    == "Accepted";
            }
            Require(remainingSevereAccepted
                    && remainingGeneralAccepted
                    && productionLimiter.UnitCount == 64
                    && productionLimiter.KeyCount == 128
                    && productionLimiter.TryAccept(
                        100, 1UL, 999,
                        InfernoMotionJumpClassification.VisualProjectionJump) == "Overflow"
                    && productionLimiter.KeyCount == 128,
                "Production motion-jump limiter must preserve first severe transitions while keeping the global exact-key count bounded at 128.");
        }

        /// <summary>InfernoSpirit 양 진영 prefab의 production Controller 연결을 확인한다.</summary>
        private static void ValidateInfernoSpiritPrefabController(
            string prefabPath,
            RuntimeAnimatorController expectedController,
            string expectedControllerGuid,
            float expectedVfxRelativeX)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Require(prefab != null, $"InfernoSpirit production prefab must exist: {prefabPath}");
            Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
            Require(animators.Length == 1 && animators[0].runtimeAnimatorController == expectedController,
                $"InfernoSpirit prefab must use the verified controller exactly once: {prefabPath}");
            Require(AssetDatabase.AssetPathToGUID(
                        AssetDatabase.GetAssetPath(animators[0].runtimeAnimatorController))
                    == expectedControllerGuid,
                $"InfernoSpirit prefab controller GUID must remain exact: {prefabPath}");

            // 순간이동 진단은 실제 스킨 메시의 rootBone을 화면 기준점으로 사용한다. renderer가
            // 여러 개이거나 rootBone이 Humanoid Hips와 다르면 어느 것을 대표값으로 골라도 추정이
            // 되므로, production 에셋을 완화하지 않고 즉시 실패시킨다.
            SkinnedMeshRenderer[] renderers =
                prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Require(renderers.Length == 1,
                $"InfernoSpirit prefab must contain exactly one SkinnedMeshRenderer: {prefabPath}");
            Require(renderers[0] != null && renderers[0].rootBone != null,
                $"InfernoSpirit production renderer must have a non-null rootBone: {prefabPath}");
            Require(animators[0].isHuman,
                $"InfernoSpirit production Animator must remain Humanoid: {prefabPath}");
            Transform animatorHips = animators[0].GetBoneTransform(HumanBodyBones.Hips);
            Require(animatorHips != null && renderers[0].rootBone == animatorHips,
                $"InfernoSpirit renderer rootBone must be the production Animator Hips: {prefabPath}");

            // InfernoSpirit의 화면 점프는 이 메시 루트가 회전축에서 약 0.813만큼 옆으로
            // 벗어난 상태에서 Root가 회전해 큰 원호를 그린 것이 직접 원인이었다. 다른 자식의
            // 좌표를 우연히 검사해 통과하지 않도록, 위에서 확정한 유일 Animator와 renderer가
            // 실제 production 메시 계층을 이루는지 먼저 확인한 뒤 그 Transform만 검사한다.
            Transform meshTransform = animators[0].transform;
            Require(meshTransform.name == prefab.name + "_Mesh"
                    && meshTransform.parent != null
                    && meshTransform.parent.name == "VisualRoot"
                    && renderers[0].transform.IsChildOf(meshTransform),
                $"InfernoSpirit Animator and renderer must remain under the verified production mesh hierarchy: {prefabPath}");

            const float meshLocalPositionTolerance = 0.0001f;
            Require(Mathf.Abs(meshTransform.localPosition.x) <= meshLocalPositionTolerance,
                $"InfernoSpirit production mesh local X must remain zero: {prefabPath}");

            // 공격 VFX의 생성점은 VisualRoot 아래에서 메시와 같은 좌표계를 사용해야 한다.
            // 메시를 중앙으로 옮길 때 생성점만 예전 절대 X에 남으면, 게임 로직과 공격 판정은
            // 정상이어도 불꽃만 유닛 옆에서 나타난다. 이름이 같은 다른 Transform을 우연히
            // 찾거나 UnitView가 다른 Transform을 참조해도 검사가 통과하지 않도록, production
            // 계층과 실제 직렬화 참조를 모두 먼저 확정한다.
            Hexiege.Presentation.UnitView[] unitViews =
                prefab.GetComponentsInChildren<Hexiege.Presentation.UnitView>(true);
            Require(unitViews.Length == 1 && unitViews[0].transform == prefab.transform,
                $"InfernoSpirit prefab must contain exactly one root UnitView: {prefabPath}");

            Transform vfxSpawnPoint = null;
            int vfxSpawnPointCount = 0;
            Transform[] transforms = prefab.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name != "VfxSpawnPoint") continue;
                vfxSpawnPointCount++;
                vfxSpawnPoint = transforms[index];
            }

            Require(vfxSpawnPointCount == 1
                    && vfxSpawnPoint != null
                    && vfxSpawnPoint.parent == meshTransform.parent
                    && vfxSpawnPoint.parent.name == "VisualRoot",
                $"InfernoSpirit VfxSpawnPoint must be the unique direct VisualRoot sibling of the mesh: {prefabPath}");

            SerializedObject serializedUnitView = new SerializedObject(unitViews[0]);
            SerializedProperty serializedVfxSpawnPoint = serializedUnitView.FindProperty("_vfxSpawnPoint");
            Require(serializedVfxSpawnPoint != null
                    && serializedVfxSpawnPoint.objectReferenceValue == vfxSpawnPoint,
                $"InfernoSpirit UnitView must reference the verified VfxSpawnPoint exactly: {prefabPath}");

            // 이 값은 과거 메시와 생성점 사이에 의도되어 있던 작은 상대 간격이다. 메시의
            // 현재 X=0을 기준으로 상대 X를 검사하므로, 이후 어느 한쪽만 다시 옮겨지는 실수도
            // 즉시 발견한다. Y/Z, 회전, 크기는 이번 교정 대상이 아니므로 변경하거나 보정하지 않는다.
            const float vfxRelativePositionTolerance = 0.000001f;
            float actualVfxRelativeX = vfxSpawnPoint.localPosition.x - meshTransform.localPosition.x;
            Require(Mathf.Abs(actualVfxRelativeX - expectedVfxRelativeX) <= vfxRelativePositionTolerance,
                $"InfernoSpirit VfxSpawnPoint must preserve the verified mesh-relative X "
                + $"({expectedVfxRelativeX:R}): {prefabPath}");
        }

        private static void ValidateQuakeSpiritPrefabController(
            string prefabPath,
            RuntimeAnimatorController expectedController)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Require(prefab != null, $"QuakeSpirit production prefab must exist: {prefabPath}");
            Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
            Require(animators.Length == 1 && animators[0].runtimeAnimatorController == expectedController,
                $"QuakeSpirit prefab must use the verified controller exactly once: {prefabPath}");
        }

        private static void ValidateC1AttackShadowManifestAndCoordinator()
        {
            Require(UnitAttackShadowProfileResolver.ValidateManifest(out string manifestReason),
                $"Tracer C 25-type manifest must be exhaustive: {manifestReason}");

            int supported = 0;
            int unresolved = 0;
            int notApplicable = 0;
            var values = new HashSet<int>();
            IReadOnlyList<UnitType> types = UnitAttackShadowProfileResolver.AllManifestTypes;
            Require(types.Count == 25, "Tracer C manifest must contain exactly 25 UnitTypes.");
            for (int index = 0; index < types.Count; index++)
            {
                Require(UnitAttackShadowProfileResolver.TryResolve(
                        types[index], out UnitAttackShadowProfile profile)
                    && profile.IsValid
                    && values.Add(profile.UnitTypeValue),
                    "Every Tracer C manifest row must be unique and valid.");
                switch (profile.Support)
                {
                    case UnitAttackShadowSupport.Supported: supported++; break;
                    case UnitAttackShadowSupport.Unresolved: unresolved++; break;
                    case UnitAttackShadowSupport.NotApplicableAttack: notApplicable++; break;
                }
                if (profile.CanRunSequencer)
                {
                    Require(profile.Delivery == AttackDeliveryKind.MeleeContact
                        || profile.Delivery == AttackDeliveryKind.Hitscan,
                        "Only MeleeContact/Hitscan profiles may run the current sequencer.");
                }
            }
            Require(supported == 16 && unresolved == 8 && notApplicable == 1,
                "Tracer C support partition must remain 16 supported, 8 unresolved, 1 healer N/A.");
            Require(UnitAttackShadowProfileResolver.TryResolve(
                    UnitType.BloomFairy, out UnitAttackShadowProfile bloom)
                && bloom.Support == UnitAttackShadowSupport.NotApplicableAttack
                && bloom.LegacyExecution == LegacyAttackExecutionKind.SeparateHealPipeline,
                "BloomFairy must remain an explicit SeparateHealPipeline/N/A row.");
            Require(UnitAttackShadowProfileResolver.TryResolve(
                    UnitType.TorrentSpirit, out UnitAttackShadowProfile torrent)
                && torrent.Support == UnitAttackShadowSupport.Unresolved
                && torrent.Delivery == AttackDeliveryKind.TravelingArea,
                "TorrentSpirit must not be coerced into the direct-hit sequencer.");
            Require(UnitAttackShadowProfileResolver.TryResolve(
                    UnitType.QuakeSpirit, out UnitAttackShadowProfile quake)
                && quake.Support == UnitAttackShadowSupport.Supported
                && quake.Delivery == AttackDeliveryKind.MeleeContact
                && quake.LegacyExecution == LegacyAttackExecutionKind.TimerImpactWithSecondaryEffect
                && quake.ExpectedImpactCount == 1
                && quake.HasSecondaryResults
                && quake.Reason == "primary-direct-supported-secondary-observed-only",
                "QuakeSpirit must be an explicit supported single-impact MeleeContact row with secondary results.");
            Require(UnitAttackShadowProfileResolver.TryResolve(
                    UnitType.RhinoBreaker, out UnitAttackShadowProfile rhino)
                && rhino.Support == UnitAttackShadowSupport.Supported
                && rhino.Delivery == AttackDeliveryKind.MeleeContact
                && rhino.ExpectedImpactCount == 1
                && !rhino.HasSecondaryResults
                && rhino.Reason == "supported",
                "RhinoBreaker must be an explicit supported single-hit MeleeContact row.");

            var unit = new UnitData(
                901, UnitType.SpearMan, TeamId.Blue, new HexCoord(0, 0),
                maxHp: 100, attackPower: 10, attackRange: 0.5f, detectRange: 1f);
            unit.AttackCooldown = 1f;
            unit.HitFrameTimes = new[] { 0.2f };
            var target = new EntityRef(EntityKind.Unit, 902);
            var binding = new AttackTargetBinding(AttackTargetMode.TargetLocked, target);
            bool beginPoseValid = UnitActionPoseSample.TryCreate(
                    target,
                    0d, 0d,
                    0d, 1d,
                    0d, 0.5d,
                    false, 0d, 0d,
                    out UnitActionPoseSample beginSample);
            bool commitPoseValid = UnitActionPoseSample.TryCreate(
                    target,
                    0d, 0d,
                    1d, 0d,
                    0.5d, 0d,
                    false, 0d, 0d,
                    out UnitActionPoseSample alignedSample);
            Require(beginPoseValid && commitPoseValid,
                "Tracer C Begin/Commit facing fixtures must be valid.");

            var coordinator = new UnitAttackShadowCoordinator();
            var instance = new AttackerInstanceId(901UL);
            UnitAttackShadowCoordinator.IntentObservation first = coordinator.ObserveIntent(
                instance, unit, binding, beginSample, true, true, 1d);
            Require(first.Snapshot != null
                && first.Snapshot.Phase == UnitActionPhase.AlignToAttack
                && !first.Snapshot.SequenceId.IsValid,
                "First C1 pose must begin Align without externally allocating a sequence.");

            LegacyAttackToken beforeCommitToken = coordinator.ScheduleLegacyAttack(
                instance, binding, 1, 1.01d, out UnitActionSnapshot beforeCommitSnapshot);
            Require(!beforeCommitToken.IsValid
                && beforeCommitSnapshot != null
                && !beforeCommitSnapshot.SequenceId.IsValid,
                "A Legacy reservation must fail closed until the exact production boundary owns a committed Shadow sequence.");

            UnitAttackShadowCoordinator.IntentObservation committed = coordinator.PrepareLegacyAttack(
                instance, unit, binding, alignedSample, true, true, 1.05d);
            Require(committed.CommittedNow
                && committed.Snapshot.SequenceId.Value == 1UL
                && committed.Snapshot.Phase == UnitActionPhase.Windup
                && committed.ShouldPublish
                && committed.Publication.SimulationFacing.X > 0.99d
                && Math.Abs(committed.Publication.SimulationFacing.Z) < 0.01d,
                "Commit publication must allocate sequence 1 and project the latest pose facing, not stale Begin facing.");

            LegacyAttackToken committedToken = coordinator.ScheduleLegacyAttack(
                instance, binding, 1, 1.06d, out UnitActionSnapshot committedSnapshot);
            Require(committedToken.IsValid
                && committedToken != beforeCommitToken
                && committedSnapshot.SequenceId.Value == 1UL,
                "Legacy reservations must use independent monotonic tokens while retaining the committed Shadow scope.");

            var otherBinding = new AttackTargetBinding(
                AttackTargetMode.TargetLocked, new EntityRef(EntityKind.Unit, 903));
            Require(UnitActionPoseSample.TryCreate(
                    otherBinding.Target,
                    0d, 0d,
                    1d, 0d,
                    0.5d, 0d,
                    false, 0d, 0d,
                    out UnitActionPoseSample otherSample),
                "Tracer C alternate target pose fixture must be valid.");
            UnitAttackShadowCoordinator.IntentObservation locked = coordinator.ObserveIntent(
                instance, unit, otherBinding, otherSample, true, true, 1.1d);
            Require(locked.Snapshot.TargetBinding == binding
                && locked.Snapshot.SequenceId.Value == 1UL,
                "A committed Shadow sequence must not transfer to a new display target.");

            // 실기 BUG-C1-LIFE-002 회귀: 표시 전투 타겟이 먼저 정리되어 StopCombat이
            // 호출돼도 이미 예약된 Legacy Impact는 자기 회차와 TargetId를 Impact까지 보유해야 한다.
            // 여기서 committed Sequencer를 취소하면 실제 Legacy writer가 Applied를 반환할 수 있는
            // 순간 Shadow만 InvalidPhase가 되어 Host terminal이 FAIL한다.
            UnitActionSnapshot displayStopped = coordinator.StopCombat(instance, 1.11d);
            Require(displayStopped != null
                && displayStopped.Phase == UnitActionPhase.Windup
                && displayStopped.SequenceId.Value == 1UL
                && displayStopped.TargetBinding == binding,
                "Display StopCombat must not cancel a committed cycle while a Legacy Impact reservation is pending.");

            double dueImpactAt = committed.Snapshot.CommitServerTime
                + (double)unit.HitFrameTimes[0];
            UnitAttackShadowCoordinator.DispatchObservation dispatch = coordinator.DispatchLegacyImpact(
                committedToken, 0, alignedSample, true, true, dueImpactAt);
            Require(dispatch.AdvanceStatus == UnitActionReducerStatus.Accepted
                && dispatch.EvaluateStatus == UnitActionReducerStatus.Accepted
                && dispatch.Authorization.IsValid
                && dispatch.Authorization.Key.HitIndex == 0
                && dispatch.ShouldPublish
                && dispatch.Publication.LastEvaluatedHitIndex == 0,
                $"A due committed Legacy token must evaluate exactly one matching Shadow impact. " +
                $"advance={dispatch.AdvanceStatus}, evaluate={dispatch.EvaluateStatus}, " +
                $"reason={dispatch.Reason}, dueImpactAt={dueImpactAt:R}");
            Require(NetworkUnitActionShadowState.TryCreate(
                    dispatch.Publication, out NetworkUnitActionShadowState impactState)
                && impactState.LastImpactHitIndex == 0
                && impactState.SimulationFacingX > 0.99f,
                "Atomic Impact publication must retain the accepted hit index and current dispatch facing.");
            UnitAttackShadowCoordinator.DispatchObservation duplicate = coordinator.DispatchLegacyImpact(
                committedToken, 0, alignedSample, true, true, dueImpactAt + 0.01d);
            Require(duplicate.EvaluateStatus == UnitActionReducerStatus.Duplicate
                && coordinator.PendingLegacyAttackCount == 1,
                "A dispatched reservation must remain pending for result completion and reject duplicate dispatch.");
            AttackDamageObservation legacyApplied = AppliedDamage(10, 90);
            UnitAttackShadowCoordinator.ResultObservation completed =
                coordinator.CompleteLegacyImpact(
                    committedToken, 0, legacyApplied, dueImpactAt + 0.02d);
            Require(completed.CompletionStatus == UnitActionReducerStatus.Accepted
                && completed.HasResult
                && completed.Result.Outcome == AttackImpactOutcome.HitApplied
                && completed.Result.AppliedAmount == 10
                && completed.Result.ResultingHp == 90
                && completed.Result.HasImpactPosition
                && completed.Result.ImpactPosition.Equals(legacyApplied.ImpactPosition)
                && coordinator.PendingLegacyAttackCount == 0,
                "A Legacy reservation must retire only after its exact result is confirmed.");
            Require(coordinator.CompleteLegacyImpact(
                        committedToken, 0, legacyApplied, dueImpactAt + 0.03d)
                    .CompletionStatus == UnitActionReducerStatus.InvalidInput,
                "A completed and retired Legacy result must fail closed when repeated.");
            UnitAttackShadowCoordinator.IntentObservation nextCycle = coordinator.ObserveIntent(
                instance, unit, binding, alignedSample, true, true, 2.2d);
            Require(nextCycle.ShouldPublish
                && nextCycle.Publication.Snapshot.SequenceId.Value == 2UL
                && nextCycle.Publication.LastEvaluatedHitIndex == -1,
                "A newly accepted Align/Commit cycle must clear the previous sequence Impact index.");

            var overlapCoordinator = new UnitAttackShadowCoordinator();
            UnitAttackShadowCoordinator.IntentObservation overlapFirst
                = overlapCoordinator.PrepareLegacyAttack(
                    instance, unit, binding, alignedSample, true, true, 3d);
            LegacyAttackToken overlapFirstToken = overlapCoordinator.ScheduleLegacyAttack(
                instance, binding, 1, 3d, out _);
            UnitAttackShadowCoordinator.IntentObservation overlapSecond
                = overlapCoordinator.PrepareLegacyAttack(
                    instance, unit, otherBinding, otherSample, true, true, 3.1d);
            LegacyAttackToken overlapSecondToken = overlapCoordinator.ScheduleLegacyAttack(
                instance, otherBinding, 1, 3.1d, out _);
            UnitAttackShadowCoordinator.DispatchObservation lateFirst
                = overlapCoordinator.DispatchLegacyImpact(
                    overlapFirstToken, 0, alignedSample, true, true, 3.21d);
            UnitAttackShadowCoordinator.DispatchObservation currentSecond
                = overlapCoordinator.DispatchLegacyImpact(
                    overlapSecondToken, 0, otherSample, true, true, 3.31d);
            Require(overlapFirst.CommittedNow
                && overlapSecond.CommittedNow
                && overlapFirst.Snapshot.SequenceId.Value == 1UL
                && overlapSecond.Snapshot.SequenceId.Value == 2UL
                && lateFirst.EvaluateStatus == UnitActionReducerStatus.Accepted
                && lateFirst.Snapshot.TargetBinding == binding
                && !lateFirst.ShouldPublish
                && currentSecond.EvaluateStatus == UnitActionReducerStatus.Accepted
                && currentSecond.Snapshot.TargetBinding == otherBinding
                && currentSecond.ShouldPublish,
                $"Overlapping Legacy cycles must retain independent Shadow target scopes while only the latest scope publishes. " +
                $"firstCommit={overlapFirst.CommittedNow}, secondCommit={overlapSecond.CommittedNow}, " +
                $"firstEvaluate={lateFirst.EvaluateStatus}, firstPublish={lateFirst.ShouldPublish}, " +
                $"secondEvaluate={currentSecond.EvaluateStatus}, secondPublish={currentSecond.ShouldPublish}");
        }

        private static void ValidateC1AttackShadowAtomicReplication()
        {
            Require(ActionDirectionXZ.TryCreate(1d, 0d, out ActionDirectionXZ facing),
                "C1 replication facing fixture must be valid.");
            Require(AttackTimelinePlan.TryCreate(
                    1d, 1d, new[] { 0.2d }, out AttackTimelinePlan timeline),
                "C1 replication timeline must be valid.");
            Require(AttackRangeProfile.TryCreate(
                    0.5d, 1d, out AttackRangeProfile range),
                "C1 replication range must be valid.");
            var binding = new AttackTargetBinding(
                AttackTargetMode.TargetLocked, new EntityRef(EntityKind.Unit, 77));
            var reducer = new UnitActionSequencer(new AttackerInstanceId(77UL), 77);
            Require(reducer.BeginAttackAlignment(
                    reducer.Snapshot.Revision, binding, AttackDeliveryKind.MeleeContact,
                    timeline, range, facing, 1d) == UnitActionReducerStatus.Accepted
                && reducer.CommitAttack(
                    reducer.Snapshot.Revision, binding, true, true, true,
                    0.25d, 5d, 1d, 1d) == UnitActionReducerStatus.Accepted,
                "C1 replication fixture must commit at the exact 5-degree boundary.");
            var publication = new UnitAttackShadowPublication(reducer.Snapshot, facing, -1);
            Require(NetworkUnitActionShadowState.TryCreate(
                    publication, out NetworkUnitActionShadowState state)
                && state.IsValid
                && state.AttackerInstanceId == 77UL
                && state.SequenceId == 1UL
                && state.TargetId == 77
                && state.Phase == (byte)UnitActionPhase.Windup,
                "UnitActionSnapshot must flatten into one valid atomic NGO value.");
            Require(!NetworkUnitActionShadowState.TryCreate(
                    new UnitAttackShadowPublication(reducer.Snapshot, default, -1), out _),
                "An invalid publication facing must fail closed instead of flattening to a valid zero vector.");

            var classifier = new UnitAttackShadowReplicationClassifier();
            Require(classifier.Classify(12UL, state) == UnitAttackShadowReplicationStatus.Accepted
                && classifier.Classify(12UL, state) == UnitAttackShadowReplicationStatus.Duplicate,
                "C1 client classifier must accept once and deduplicate the same atomic value.");
            NetworkUnitActionShadowState sameRevisionNeutral = state;
            sameRevisionNeutral.SequenceId = 0UL;
            sameRevisionNeutral.Phase = (byte)UnitActionPhase.AlignToAttack;
            Require(classifier.Classify(12UL, sameRevisionNeutral)
                    == UnitAttackShadowReplicationStatus.Accepted
                && classifier.Classify(12UL, sameRevisionNeutral)
                    == UnitAttackShadowReplicationStatus.Duplicate,
                "A same-revision neutral Align projection must be accepted once without lowering the issued sequence ceiling.");

            // 실기 BUG-C1-SEQ-001 회귀: 각 공격 회차는 독립 reducer라 revision이 다시
            // 시작한다. 따라서 순서는 revision 단독이 아니라 (AttackSequenceId, Revision)이며,
            // neutral은 마지막 양수 sequence 상한을 보존하는 후보 생명주기다.
            var cycleClassifier = new UnitAttackShadowReplicationClassifier();
            NetworkUnitActionShadowState firstImpact = state;
            firstImpact.Revision = 4UL;
            firstImpact.Phase = (byte)UnitActionPhase.Impact;
            NetworkUnitActionShadowState betweenCycles = state;
            betweenCycles.SequenceId = 0UL;
            betweenCycles.Revision = 7UL;
            betweenCycles.Phase = (byte)UnitActionPhase.AlignToAttack;
            NetworkUnitActionShadowState secondCommit = state;
            secondCommit.SequenceId = 2UL;
            secondCommit.Revision = 3UL;
            secondCommit.Phase = (byte)UnitActionPhase.Windup;
            NetworkUnitActionShadowState secondImpact = secondCommit;
            secondImpact.Revision = 4UL;
            secondImpact.Phase = (byte)UnitActionPhase.Impact;
            NetworkUnitActionShadowState lateFirstCycle = firstImpact;
            lateFirstCycle.Revision = 5UL;
            Require(cycleClassifier.Classify(12UL, firstImpact)
                    == UnitAttackShadowReplicationStatus.Accepted
                && cycleClassifier.Classify(12UL, betweenCycles)
                    == UnitAttackShadowReplicationStatus.Accepted
                && cycleClassifier.Classify(12UL, secondCommit)
                    == UnitAttackShadowReplicationStatus.Accepted
                && cycleClassifier.Classify(12UL, secondImpact)
                    == UnitAttackShadowReplicationStatus.Accepted
                && cycleClassifier.Classify(12UL, lateFirstCycle)
                    == UnitAttackShadowReplicationStatus.SequenceRegression,
                "A new attack sequence must accept its reset revision after neutral, while a late prior sequence remains rejected.");
            NetworkUnitActionShadowState stale = state;
            stale.Revision--;
            Require(classifier.Classify(12UL, stale) == UnitAttackShadowReplicationStatus.StaleRevision,
                "C1 client classifier must reject a stale revision.");
            NetworkUnitActionShadowState regressed = state;
            regressed.Revision++;
            regressed.SequenceId = 0UL;
            Require(classifier.Classify(12UL, regressed)
                    == UnitAttackShadowReplicationStatus.SequenceRegression,
                "C1 client classifier must reject sequence regression even with a newer revision.");
            classifier.Retire();
            Require(classifier.Classify(12UL, state)
                    == UnitAttackShadowReplicationStatus.InstanceRetired,
                "C1 client classifier must reject packets after lifecycle retire.");

            MethodInfo preflight = typeof(UnitAttackShadowObserver).GetMethod(
                "ValidateTerminalPreflightForValidation",
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(preflight != null,
                "C1 observer must expose its production full-line UTF-8 preflight seam.");
            string result = (string)preflight.Invoke(null, null);
            Require(result.Contains("valid=True")
                && result.Contains("manifest=True")
                && result.Contains($"schema={UnitAttackShadowObserver.ProductionSchema}"),
                $"The current attack-shadow compact terminal must pass Android-safe full-line UTF-8 preflight. {result}");
            Require(UnitAttackShadowObserver.ClassifyLocalVerdictForValidation(1, 0, 0, 0, 0, 0)
                    == "EVIDENCE"
                && UnitAttackShadowObserver.ClassifyLocalVerdictForValidation(0, 0, 0, 0, 0, 0)
                    == "INCONCLUSIVE"
                && UnitAttackShadowObserver.ClassifyLocalVerdictForValidation(1, 1, 0, 0, 0, 0)
                    == "FAIL"
                && UnitAttackShadowObserver.ClassifyLocalVerdictForValidation(1, 0, 1, 0, 0, 0)
                    == "FAIL"
                && UnitAttackShadowObserver.ClassifyLocalVerdictForValidation(1, 0, 0, 1, 0, 0)
                    == "FAIL"
                && UnitAttackShadowObserver.ClassifyLocalVerdictForValidation(1, 0, 0, 0, 1, 0)
                    == "FAIL"
                && UnitAttackShadowObserver.ClassifyLocalVerdictForValidation(1, 0, 0, 0, 0, 0, 1)
                    == "FAIL"
                && UnitAttackShadowObserver.ClassifyLocalVerdictForValidation(1, 0, 0, 0, 0, 0, 0, 1)
                    == "FAIL",
                "C1 END verdict must fail closed including pre-alignment or uncorrelated Legacy starts, distinguish no evidence, and preserve healthy local evidence.");

            // 실기 진단 오탐 회귀: 표시 후보는 예약 Impact보다 먼저 사라질 수 있다.
            // 예약 Shadow/Legacy의 target, sequence, 판정과 적용 결과가 일치하면
            // displayTarget=-1은 정상이며 gameplay mismatch로 집계하면 안 된다.
            Require(!UnitAttackShadowObserver.ClassifyDispatchMismatchForValidation(
                    -1, 15, 15, 5UL, 5UL,
                    UnitActionReducerStatus.Accepted,
                    ImpactAuthorizationOutcome.AuthorizedHit,
                    AttackDamageApplyStatus.Applied),
                "A committed reservation with matching Shadow/Legacy target and result must remain healthy after the display target is cleared.");
            Require(UnitAttackShadowObserver.ClassifyDispatchMismatchForValidation(
                    15, 15, 16, 5UL, 5UL,
                    UnitActionReducerStatus.Accepted,
                    ImpactAuthorizationOutcome.AuthorizedHit,
                    AttackDamageApplyStatus.Applied)
                && UnitAttackShadowObserver.ClassifyDispatchMismatchForValidation(
                    -1, 15, 15, 5UL, 4UL,
                    UnitActionReducerStatus.Accepted,
                    ImpactAuthorizationOutcome.AuthorizedHit,
                    AttackDamageApplyStatus.Applied)
                && UnitAttackShadowObserver.ClassifyDispatchMismatchForValidation(
                    -1, 15, 15, 5UL, 5UL,
                    UnitActionReducerStatus.InvalidPhase,
                    ImpactAuthorizationOutcome.None,
                    AttackDamageApplyStatus.Applied)
                && UnitAttackShadowObserver.ClassifyDispatchMismatchForValidation(
                    -1, 15, 15, 5UL, 5UL,
                    UnitActionReducerStatus.Accepted,
                    ImpactAuthorizationOutcome.AuthorizedMiss,
                    AttackDamageApplyStatus.Applied),
                "C1 dispatch classification must still fail closed for target, sequence, phase, or result divergence.");
        }

        private static void ValidateC2ImpactResultShadow()
        {
            Require(AppliedDamage(12, 88).IsValid
                && AppliedDamage(0, 100).IsValid
                && AttackDamageObservation.Unavailable(
                        AttackDamageApplyStatus.TargetUnavailable).IsValid
                && AttackDamageObservation.Unavailable(
                        AttackDamageApplyStatus.CombatConditionFailed).IsValid
                && AttackDamageObservation.Unavailable(
                        AttackDamageApplyStatus.AuthorizationUnavailable).IsValid
                && !new AttackDamageObservation(
                        AttackDamageApplyStatus.AttackerUnavailable, 1, -1).IsValid
                && !new AttackDamageObservation(
                        (AttackDamageApplyStatus)255, 0, -1).IsValid,
                "C2 Legacy writer observations must preserve applied amount/HP and fail closed for malformed or unknown unavailable results.");

            Require(ActionDirectionXZ.TryCreate(1d, 0d, out ActionDirectionXZ aim),
                "C2 result aim fixture must be valid.");
            var key = new AttackResultKey(
                new AttackerInstanceId(700UL),
                new AttackSequenceId(2UL),
                0, (int)EntityKind.Unit, 701, 1, 0);
            AttackImpactResult result = null;
            NetworkAttackImpactShadowResult networkResult = default;
            bool resultCreated = AttackImpactResult.TryCreate(
                    4UL, key, 10.25d, aim,
                    false, default,
                    AttackImpactOutcome.HitApplied,
                    12, 88,
                    out result);
            bool networkResultCreated = resultCreated
                && NetworkAttackImpactShadowResult.TryCreate(
                    result,
                    AttackDeliveryKind.MeleeContact,
                    out networkResult);
            Require(resultCreated
                && networkResultCreated
                && networkResult.IsValid
                && networkResult.Key.Equals(key)
                && networkResult.ActionRevision == 4UL
                && networkResult.AppliedAmount == 12
                && networkResult.ResultingHp == 88,
                "C2 must flatten the full result key, authoritative aim, outcome and HP into one valid network value.");

            var classifier = new UnitAttackShadowImpactReplicationClassifier();
            Require(classifier.Classify(99UL, networkResult)
                    == UnitAttackShadowImpactReplicationStatus.Accepted
                && classifier.Classify(99UL, networkResult)
                    == UnitAttackShadowImpactReplicationStatus.Duplicate,
                "C2 result classifier must accept one payload and deduplicate an exact reliable retry.");

            NetworkAttackImpactShadowResult conflict = networkResult;
            conflict.ResultingHp = 87;
            Require(conflict.IsValid
                && classifier.Classify(99UL, conflict)
                    == UnitAttackShadowImpactReplicationStatus.Conflict,
                "The same C2 result key with different HP must fail closed as a conflict.");

            NetworkAttackImpactShadowResult earlierSequence = networkResult;
            earlierSequence.SequenceId = 1UL;
            earlierSequence.VictimId = 702;
            Require(earlierSequence.IsValid
                && classifier.Classify(99UL, earlierSequence)
                    == UnitAttackShadowImpactReplicationStatus.Accepted,
                "Reliable C2 results may complete out of sequence and must be keyed rather than FIFO-gated.");

            NetworkAttackImpactShadowResult invalid = networkResult;
            invalid.AimX = 0f;
            invalid.AimZ = 0f;
            Require(classifier.Classify(99UL, invalid)
                    == UnitAttackShadowImpactReplicationStatus.Invalid,
                "A non-normalized C2 authoritative aim must fail closed.");
            classifier.Retire();
            Require(classifier.Classify(99UL, networkResult)
                    == UnitAttackShadowImpactReplicationStatus.InstanceRetired,
                "C2 results arriving after NetworkObject lifecycle retire must be rejected.");
        }

        private static void ValidateC2LegacyOvershootAndAuthorizationOutcome()
        {
            var unit = new UnitData(
                951, UnitType.FlameSpirit, TeamId.Blue, new HexCoord(0, 0),
                maxHp: 100, attackPower: 10, attackRange: 0.5f, detectRange: 1f);
            unit.AttackCooldown = 1f;
            // production resolver의 FlameSpirit 6-hit manifest와 같은 개수를 사용한다.
            // 지원 manifest를 위반한 임의 3-hit SpearMan은 timeline 산술에 도달하기 전에
            // runtime-impact-count-mismatch로 거부되므로 overshoot 회귀 fixture가 될 수 없다.
            unit.HitFrameTimes = new[] { 0.02f, 0.05f, 0.2f, 0.3f, 0.4f, 0.5f };
            var target = new EntityRef(EntityKind.Unit, 952);
            var binding = new AttackTargetBinding(AttackTargetMode.TargetLocked, target);
            Require(UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 1d, 0d, 0.5d, 0d,
                    false, 0d, 0d, out UnitActionPoseSample aligned),
                "C2 overshoot aligned pose fixture must be valid.");

            var adjustedCoordinator = new UnitAttackShadowCoordinator();
            var adjustedInstance = new AttackerInstanceId(951UL);
            // production의 overshoot와 HitFrameTimes는 모두 float다. 여기서 double
            // 리터럴 0.05d를 쓰면 (double)(float)0.05와 다른 값이 되어 정확한 0 경계를
            // 재현하지 못한다. 실제 호출처럼 float를 만든 뒤 coordinator 인자로 승격한다.
            float productionOvershoot = 0.05f;
            double expectedEffectiveCooldown = (double)unit.AttackCooldown
                - productionOvershoot;
            double expectedThirdOffset = (double)unit.HitFrameTimes[2]
                - productionOvershoot;
            UnitAttackShadowCoordinator.IntentObservation adjusted =
                adjustedCoordinator.PrepareLegacyAttack(
                    adjustedInstance, unit, binding, aligned,
                    true, true, 10d, productionOvershoot);
            Require(adjusted.CommittedNow,
                $"C2 production overshoot fixture must commit a manifest-valid FlameSpirit cycle; status={adjusted.Status}, reason={adjusted.Reason}.");
            Require(adjusted.Snapshot != null && adjusted.Snapshot.Timeline != null
                && adjusted.Snapshot.Timeline.ImpactCount == unit.HitFrameTimes.Length,
                "C2 committed production snapshot must preserve the manifest-valid six-impact timeline.");
            Require(Math.Abs(adjusted.Snapshot.CommitServerTime - 10d) < 0.000001d,
                "C2 production overshoot must keep the observed server time as CommitServerTime.");
            Require(adjusted.Snapshot.Timeline.CooldownSeconds == expectedEffectiveCooldown,
                "C2 production overshoot must subtract the float-domain overshoot from cooldown exactly once.");
            Require(adjusted.Snapshot.Timeline.GetImpactOffset(0) == 0d
                && adjusted.Snapshot.Timeline.GetImpactOffset(1) == 0d,
                "C2 production overshoot must preserve same-zero multi-hit offsets in HitIndex order.");
            Require(adjusted.Snapshot.Timeline.GetImpactOffset(2) == expectedThirdOffset,
                "C2 production overshoot must subtract the float-domain overshoot from a later impact exactly once.");
            for (int hitIndex = 3; hitIndex < unit.HitFrameTimes.Length; hitIndex++)
            {
                double expectedOffset = (double)unit.HitFrameTimes[hitIndex]
                    - productionOvershoot;
                Require(adjusted.Snapshot.Timeline.GetImpactOffset(hitIndex) == expectedOffset,
                    $"C2 production overshoot must preserve adjusted impact {hitIndex} without a second subtraction.");
            }

            float justBelowHitBoundary = productionOvershoot - 0.000001f;
            float justAboveHitBoundary = productionOvershoot + 0.000001f;
            UnitAttackShadowCoordinator.IntentObservation belowBoundary =
                new UnitAttackShadowCoordinator().PrepareLegacyAttack(
                    new AttackerInstanceId(961UL), unit, binding, aligned,
                    true, true, 11d, justBelowHitBoundary);
            UnitAttackShadowCoordinator.IntentObservation aboveBoundary =
                new UnitAttackShadowCoordinator().PrepareLegacyAttack(
                    new AttackerInstanceId(962UL), unit, binding, aligned,
                    true, true, 12d, justAboveHitBoundary);
            Require(belowBoundary.CommittedNow
                && belowBoundary.Snapshot.Timeline.GetImpactOffset(1) > 0d
                && aboveBoundary.CommittedNow
                && aboveBoundary.Snapshot.Timeline.GetImpactOffset(1) == 0d,
                "C2 float-domain hit boundary must remain positive just below and clamp to zero just above the production overshoot.");
            Require(new UnitAttackShadowCoordinator().PrepareLegacyAttack(
                        new AttackerInstanceId(953UL), unit, binding, aligned,
                        true, true, 20d, 0d).CommittedNow,
                "Zero overshoot must preserve the full production timeline.");
            Require(new UnitAttackShadowCoordinator().PrepareLegacyAttack(
                        new AttackerInstanceId(954UL), unit, binding, aligned,
                        true, true, 20d, -0.001d).Status
                    == UnitActionReducerStatus.InvalidInput
                && new UnitAttackShadowCoordinator().PrepareLegacyAttack(
                        new AttackerInstanceId(955UL), unit, binding, aligned,
                        true, true, 20d, double.NaN).Status
                    == UnitActionReducerStatus.InvalidInput
                && new UnitAttackShadowCoordinator().PrepareLegacyAttack(
                        new AttackerInstanceId(956UL), unit, binding, aligned,
                        true, true, 20d, 1d).Status
                    == UnitActionReducerStatus.InvalidInput,
                "Negative, non-finite, or cooldown-sized overshoot must fail closed before a Shadow cycle is committed.");

            LegacyAttackToken missToken = adjustedCoordinator.ScheduleLegacyAttack(
                adjustedInstance, binding, unit.HitFrameTimes.Length, 10d, out _);
            UnitAttackShadowCoordinator.DispatchObservation missDispatch =
                adjustedCoordinator.DispatchLegacyImpact(
                    missToken, 0, aligned, false, false, 10d);
            UnitAttackShadowCoordinator.DispatchObservation sameDueSecond =
                adjustedCoordinator.DispatchLegacyImpact(
                    missToken, 1, aligned, false, false, 10.0001d);
            Require(missDispatch.EvaluateStatus == UnitActionReducerStatus.Accepted
                && missDispatch.Authorization.Outcome == ImpactAuthorizationOutcome.AuthorizedMiss
                && missDispatch.Authorization.MissReason
                    == ImpactAuthorizationMissReason.TargetUnavailable
                && sameDueSecond.EvaluateStatus == UnitActionReducerStatus.Accepted
                && sameDueSecond.Authorization.Key.HitIndex == 1,
                "Unavailable same-zero-offset multi-hit impacts must remain ordered by HitIndex without NotDue or OutOfOrder.");
            UnitAttackShadowCoordinator.ResultObservation missResult =
                adjustedCoordinator.CompleteLegacyImpact(
                    missToken, 0,
                    AttackDamageObservation.Unavailable(AttackDamageApplyStatus.TargetUnavailable),
                    10.01d);
            Require(missResult.CompletionStatus == UnitActionReducerStatus.Accepted
                && missResult.Result.Outcome == AttackImpactOutcome.Miss
                && !UnitAttackShadowObserver.ClassifyImpactResultMismatchForValidation(
                    AttackDamageObservation.Unavailable(
                        AttackDamageApplyStatus.TargetUnavailable),
                    missResult.CompletionStatus,
                    missResult.Result),
                "AuthorizedMiss plus TargetUnavailable must confirm as the canonical Miss outcome.");

            // 실제 production에서는 제거된 타겟의 Transform을 더 이상 읽을 수 없다.
            // pose를 위조하지 않고 생명주기 상태만으로 같은 token/hit를 Miss로 닫는
            // coordinator 공개 경계를 검증한다.
            var removedTargetCoordinator = new UnitAttackShadowCoordinator();
            var removedTargetInstance = new AttackerInstanceId(964UL);
            UnitAttackShadowCoordinator.IntentObservation removedTargetCommit =
                removedTargetCoordinator.PrepareLegacyAttack(
                    removedTargetInstance, unit, binding, aligned,
                    true, true, 31d, 0d);
            LegacyAttackToken removedTargetToken =
                removedTargetCoordinator.ScheduleLegacyAttack(
                    removedTargetInstance, binding,
                    unit.HitFrameTimes.Length, 31d, out _);
            double removedTargetImpactTime = removedTargetCommit.Snapshot.CommitServerTime
                + removedTargetCommit.Snapshot.Timeline.GetImpactOffset(0);
            UnitAttackShadowCoordinator.DispatchObservation removedTargetDispatch =
                removedTargetCoordinator.DispatchLegacyUnavailableImpact(
                    removedTargetToken, 0,
                    AttackDamageApplyStatus.TargetUnavailable,
                    removedTargetImpactTime);
            UnitAttackShadowCoordinator.ResultObservation removedTargetResult =
                removedTargetCoordinator.CompleteLegacyImpact(
                    removedTargetToken, 0,
                    AttackDamageObservation.Unavailable(
                        AttackDamageApplyStatus.TargetUnavailable),
                    removedTargetImpactTime + 0.001d);
            UnitAttackShadowCoordinator.DispatchObservation removedTargetDuplicate =
                removedTargetCoordinator.DispatchLegacyUnavailableImpact(
                    removedTargetToken, 0,
                    AttackDamageApplyStatus.TargetUnavailable,
                    removedTargetImpactTime + 0.002d);
            Require(removedTargetDispatch.EvaluateStatus == UnitActionReducerStatus.Accepted
                && removedTargetDispatch.Authorization.MissReason
                    == ImpactAuthorizationMissReason.TargetUnavailable
                && removedTargetResult.CompletionStatus == UnitActionReducerStatus.Accepted
                && removedTargetResult.Result != null
                && removedTargetResult.Result.Outcome == AttackImpactOutcome.Miss
                && removedTargetDuplicate.EvaluateStatus == UnitActionReducerStatus.Duplicate
                && !removedTargetDuplicate.Authorization.IsValid,
                "A removed committed target must complete exactly once as canonical TargetUnavailable Miss without a pose sample.");

            Require(AttackImpactPoseCaptureClassifier.Classify(
                        true, true, true, true, true, true, false)
                    == AttackImpactPoseCaptureStatus.Captured
                && AttackImpactPoseCaptureClassifier.Classify(
                        true, true, false, false, false, false, false)
                    == AttackImpactPoseCaptureStatus.TargetUnavailable
                && AttackImpactPoseCaptureClassifier.Classify(
                        false, true, true, false, false, false, false)
                    == AttackImpactPoseCaptureStatus.AttackerUnavailable
                && AttackImpactPoseCaptureClassifier.Classify(
                        false, true, false, false, false, false, false)
                    == AttackImpactPoseCaptureStatus.AmbiguousUnavailable
                && AttackImpactPoseCaptureClassifier.Classify(
                        true, false, false, false, false, false, false)
                    == AttackImpactPoseCaptureStatus.TargetStateUnavailable
                && AttackImpactPoseCaptureClassifier.Classify(
                        true, true, true, false, false, false, false)
                    == AttackImpactPoseCaptureStatus.PoseSourceUnavailable
                && AttackImpactPoseCaptureClassifier.Classify(
                        true, true, true, true, false, false, false)
                    == AttackImpactPoseCaptureStatus.CaptureFailed
                && AttackImpactPoseCaptureClassifier.Classify(
                        true, true, true, true, true, false, false)
                    == AttackImpactPoseCaptureStatus.InvalidSample
                && AttackImpactPoseCaptureClassifier.Classify(
                        true, true, true, true, true, true, true)
                    == AttackImpactPoseCaptureStatus.InfrastructureFailure,
                "C2 Impact pose capture must preserve lifecycle, source, capture, sample, and infrastructure failure causes.");

            var livePoseFailureCoordinator = new UnitAttackShadowCoordinator();
            var livePoseFailureInstance = new AttackerInstanceId(965UL);
            UnitAttackShadowCoordinator.IntentObservation livePoseFailureCommit =
                livePoseFailureCoordinator.PrepareLegacyAttack(
                    livePoseFailureInstance, unit, binding, aligned,
                    true, true, 33d, 0d);
            LegacyAttackToken livePoseFailureToken =
                livePoseFailureCoordinator.ScheduleLegacyAttack(
                    livePoseFailureInstance, binding,
                    unit.HitFrameTimes.Length, 33d, out _);
            UnitAttackShadowCoordinator.ResultObservation livePoseFailureResult =
                livePoseFailureCoordinator.CompleteLegacyImpact(
                    livePoseFailureToken, 0,
                    AttackDamageObservation.Unavailable(
                        AttackDamageApplyStatus.PoseUnavailable),
                    livePoseFailureCommit.Snapshot.CommitServerTime + 0.1d);
            Require(livePoseFailureResult.CompletionStatus
                    == UnitActionReducerStatus.InvalidInput
                && livePoseFailureResult.Reason == "impact-pose-capture-failure"
                && !livePoseFailureResult.HasResult
                && AttackDamageObservation.Unavailable(
                    AttackDamageApplyStatus.PoseUnavailable).IsValid,
                "A live-target pose failure must remain damage-zero typed fatal evidence and must not be classified as NotDue or Miss.");
            UnitAttackShadowCoordinator.ResultObservation wrongUnavailableReason =
                adjustedCoordinator.CompleteLegacyImpact(
                    missToken, 1,
                    AttackDamageObservation.Unavailable(AttackDamageApplyStatus.AttackerUnavailable),
                    10.02d);
            Require(wrongUnavailableReason.CompletionStatus == UnitActionReducerStatus.InvalidInput
                && !wrongUnavailableReason.HasResult,
                "TargetUnavailable authorization must reject an AttackerUnavailable Legacy outcome with no result.");

            var attackerUnavailableCoordinator = new UnitAttackShadowCoordinator();
            var attackerUnavailableInstance = new AttackerInstanceId(959UL);
            UnitAttackShadowCoordinator.IntentObservation attackerUnavailableCommit =
                attackerUnavailableCoordinator.PrepareLegacyAttack(
                    attackerUnavailableInstance, unit, binding, aligned,
                    true, true, 25d, 0d);
            LegacyAttackToken attackerUnavailableToken =
                attackerUnavailableCoordinator.ScheduleLegacyAttack(
                    attackerUnavailableInstance, binding, unit.HitFrameTimes.Length, 25d, out _);
            UnitAttackShadowCoordinator.DispatchObservation attackerUnavailableDispatch =
                attackerUnavailableCoordinator.DispatchLegacyImpact(
                    attackerUnavailableToken, 0, aligned,
                    false, true, true,
                    attackerUnavailableCommit.Snapshot.CommitServerTime
                        + unit.HitFrameTimes[0]);
            UnitAttackShadowCoordinator.ResultObservation attackerUnavailableResult =
                attackerUnavailableCoordinator.CompleteLegacyImpact(
                    attackerUnavailableToken, 0,
                    AttackDamageObservation.Unavailable(AttackDamageApplyStatus.AttackerUnavailable),
                    25.03d);
            Require(attackerUnavailableDispatch.EvaluateStatus == UnitActionReducerStatus.Accepted
                && attackerUnavailableDispatch.Authorization.MissReason
                    == ImpactAuthorizationMissReason.AttackerUnavailable
                && attackerUnavailableResult.CompletionStatus == UnitActionReducerStatus.Accepted
                && attackerUnavailableResult.Result.Outcome == AttackImpactOutcome.Miss,
                "AttackerUnavailable authorization must accept only the same Legacy unavailable reason as Miss.");

            var bothUnavailableCoordinator = new UnitAttackShadowCoordinator();
            var bothUnavailableInstance = new AttackerInstanceId(960UL);
            UnitAttackShadowCoordinator.IntentObservation bothUnavailableCommit =
                bothUnavailableCoordinator.PrepareLegacyAttack(
                    bothUnavailableInstance, unit, binding, aligned,
                    true, true, 27d, 0d);
            LegacyAttackToken bothUnavailableToken = bothUnavailableCoordinator.ScheduleLegacyAttack(
                bothUnavailableInstance, binding, unit.HitFrameTimes.Length, 27d, out _);
            UnitAttackShadowCoordinator.DispatchObservation bothUnavailableDispatch =
                bothUnavailableCoordinator.DispatchLegacyImpact(
                    bothUnavailableToken, 0, aligned,
                    false, false, false,
                    bothUnavailableCommit.Snapshot.CommitServerTime
                        + unit.HitFrameTimes[0]);
            UnitAttackShadowCoordinator.ResultObservation bothUnavailableResult =
                bothUnavailableCoordinator.CompleteLegacyImpact(
                    bothUnavailableToken, 0,
                    AttackDamageObservation.Unavailable(AttackDamageApplyStatus.TargetUnavailable),
                    27.03d);
            Require(bothUnavailableDispatch.EvaluateStatus == UnitActionReducerStatus.InvalidInput
                && !bothUnavailableDispatch.Authorization.IsValid
                && bothUnavailableResult.CompletionStatus == UnitActionReducerStatus.InvalidInput
                && bothUnavailableResult.Reason
                    == "completion-without-accepted-authorization"
                && !bothUnavailableResult.HasResult,
                "Simultaneous attacker/target absence must fail closed without choosing an unavailable reason or producing a result.");

            var earlyCompletionCoordinator = new UnitAttackShadowCoordinator();
            var earlyCompletionInstance = new AttackerInstanceId(966UL);
            UnitAttackShadowCoordinator.IntentObservation earlyCompletionCommit =
                earlyCompletionCoordinator.PrepareLegacyAttack(
                    earlyCompletionInstance, unit, binding, aligned,
                    true, true, 35d, 0d);
            LegacyAttackToken earlyCompletionToken =
                earlyCompletionCoordinator.ScheduleLegacyAttack(
                    earlyCompletionInstance, binding,
                    unit.HitFrameTimes.Length, 35d, out _);
            UnitAttackShadowCoordinator.DispatchObservation earlyDispatch =
                earlyCompletionCoordinator.DispatchLegacyImpact(
                    earlyCompletionToken, 0, aligned,
                    true, true,
                    earlyCompletionCommit.Snapshot.CommitServerTime);
            UnitAttackShadowCoordinator.ResultObservation rejectedCompletion =
                earlyCompletionCoordinator.CompleteLegacyImpact(
                    earlyCompletionToken, 0,
                    AttackDamageObservation.Unavailable(
                        AttackDamageApplyStatus.AuthorizationUnavailable),
                    earlyCompletionCommit.Snapshot.CommitServerTime + 0.001d);
            Require(earlyDispatch.EvaluateStatus == UnitActionReducerStatus.NotDue
                    && rejectedCompletion.CompletionStatus
                        == UnitActionReducerStatus.InvalidInput
                    && rejectedCompletion.Reason
                        == "completion-without-accepted-authorization",
                "NotDue must describe only the early authorization boundary; completion without authorization must be a typed terminal failure.");

            Require(!UnitAttackShadowCoordinator.HasReachedLegacyImpactTime(50.199999d, 50.2d)
                && UnitAttackShadowCoordinator.HasReachedLegacyImpactTime(50.2d, 50.2d)
                && UnitAttackShadowCoordinator.HasReachedLegacyImpactTime(50.200001d, 50.2d)
                && !UnitAttackShadowCoordinator.HasReachedLegacyImpactTime(double.NaN, 50.2d)
                && !UnitAttackShadowCoordinator.HasReachedLegacyImpactTime(
                    50.2d, double.PositiveInfinity),
                "C2 production dispatch must wait for the exact finite authoritative server-time boundary.");

            Require(UnitActionPoseSample.TryCreate(
                    target, 0d, 0d, 1d, 0d, 2d, 0d,
                    false, 0d, 0d, out UnitActionPoseSample outOfRange),
                "C2 out-of-range Impact fixture must be valid.");
            var combatConditionCoordinator = new UnitAttackShadowCoordinator();
            var combatConditionInstance = new AttackerInstanceId(963UL);
            UnitAttackShadowCoordinator.IntentObservation combatConditionCommit =
                combatConditionCoordinator.PrepareLegacyAttack(
                    combatConditionInstance, unit, binding, aligned,
                    true, true, 29d, 0d);
            LegacyAttackToken combatConditionToken =
                combatConditionCoordinator.ScheduleLegacyAttack(
                    combatConditionInstance, binding,
                    unit.HitFrameTimes.Length, 29d, out _);
            UnitAttackShadowCoordinator.DispatchObservation combatConditionDispatch =
                combatConditionCoordinator.DispatchLegacyImpact(
                    combatConditionToken, 0, outOfRange, true, true,
                    combatConditionCommit.Snapshot.CommitServerTime
                        + unit.HitFrameTimes[0]);
            Require(combatConditionDispatch.EvaluateStatus == UnitActionReducerStatus.Accepted
                && combatConditionDispatch.Authorization.Outcome
                    == ImpactAuthorizationOutcome.AuthorizedMiss
                && combatConditionDispatch.Authorization.MissReason
                    == ImpactAuthorizationMissReason.CombatConditionFailed
                && UnitAttackShadowCoordinator.ResolveLegacyWriterAuthorization(
                    combatConditionDispatch) == AttackDamageApplyStatus.CombatConditionFailed
                && UnitAttackShadowCoordinator.ResolveLegacyWriterAuthorization(default)
                    == AttackDamageApplyStatus.AuthorizationUnavailable,
                "C2 must turn an out-of-range authoritative Impact into a no-damage Legacy writer authorization.");
            AttackDamageObservation combatConditionLegacy = AttackDamageObservation.Unavailable(
                AttackDamageApplyStatus.CombatConditionFailed);
            UnitAttackShadowCoordinator.ResultObservation combatConditionResult =
                combatConditionCoordinator.CompleteLegacyImpact(
                    combatConditionToken, 0, combatConditionLegacy, 29.03d);
            Require(combatConditionResult.CompletionStatus == UnitActionReducerStatus.Accepted
                && combatConditionResult.Result.Outcome == AttackImpactOutcome.Miss
                && !UnitAttackShadowObserver.ClassifyImpactResultMismatchForValidation(
                    combatConditionLegacy,
                    combatConditionResult.CompletionStatus,
                    combatConditionResult.Result),
                "CombatConditionFailed must apply no damage and complete as one canonical Miss without observer failure.");

            var conflictCoordinator = new UnitAttackShadowCoordinator();
            var conflictInstance = new AttackerInstanceId(957UL);
            UnitAttackShadowCoordinator.IntentObservation conflictCommit =
                conflictCoordinator.PrepareLegacyAttack(
                    conflictInstance, unit, binding, aligned,
                    true, true, 30d, 0d);
            LegacyAttackToken conflictToken = conflictCoordinator.ScheduleLegacyAttack(
                conflictInstance, binding, unit.HitFrameTimes.Length, 30d, out _);
            UnitAttackShadowCoordinator.DispatchObservation hitDispatch =
                conflictCoordinator.DispatchLegacyImpact(
                    conflictToken, 0, aligned, true, true,
                    conflictCommit.Snapshot.CommitServerTime + unit.HitFrameTimes[0]);
            Require(hitDispatch.Authorization.Outcome == ImpactAuthorizationOutcome.AuthorizedHit
                && conflictCoordinator.CompleteLegacyImpact(
                        conflictToken, 0,
                        AttackDamageObservation.Unavailable(AttackDamageApplyStatus.AttackerUnavailable),
                        30.03d).CompletionStatus == UnitActionReducerStatus.InvalidInput,
                "AuthorizedHit plus an unavailable Legacy outcome must fail closed rather than being forged as Miss or Cancelled.");

            var zeroCoordinator = new UnitAttackShadowCoordinator();
            var zeroInstance = new AttackerInstanceId(958UL);
            UnitAttackShadowCoordinator.IntentObservation zeroCommit = zeroCoordinator.PrepareLegacyAttack(
                zeroInstance, unit, binding, aligned, true, true, 40d, 0d);
            LegacyAttackToken zeroToken = zeroCoordinator.ScheduleLegacyAttack(
                zeroInstance, binding, unit.HitFrameTimes.Length, 40d, out _);
            zeroCoordinator.DispatchLegacyImpact(
                zeroToken, 0, aligned, true, true,
                zeroCommit.Snapshot.CommitServerTime + unit.HitFrameTimes[0]);
            UnitAttackShadowCoordinator.ResultObservation zeroResult = zeroCoordinator.CompleteLegacyImpact(
                zeroToken, 0,
                AppliedDamage(0, 100),
                40.03d);
            Require(zeroResult.CompletionStatus == UnitActionReducerStatus.Accepted
                && zeroResult.Result.Outcome == AttackImpactOutcome.StatusEffectApplied,
                "AuthorizedHit plus a valid zero-damage Legacy application must remain StatusEffectApplied.");

            // Android Host 회귀: 사망 전에 이미 authorization된 hit는 정상 완료하되,
            // 아직 dispatch되지 않은 같은 multi-hit 예약은 writer/result/observer 전에 취소한다.
            var deathCancellationCoordinator = new UnitAttackShadowCoordinator();
            var deathCancellationInstance = new AttackerInstanceId(967UL);
            UnitAttackShadowCoordinator.IntentObservation deathCancellationCommit =
                deathCancellationCoordinator.PrepareLegacyAttack(
                    deathCancellationInstance, unit, binding, aligned,
                    true, true, 60d, 0d);
            LegacyAttackToken deathCancellationToken =
                deathCancellationCoordinator.ScheduleLegacyAttack(
                    deathCancellationInstance, binding,
                    unit.HitFrameTimes.Length, 60d, out _);
            UnitAttackShadowCoordinator.DispatchObservation dispatchedBeforeDeath =
                deathCancellationCoordinator.DispatchLegacyImpact(
                    deathCancellationToken, 0, aligned, true, true,
                    deathCancellationCommit.Snapshot.CommitServerTime
                        + unit.HitFrameTimes[0]);
            deathCancellationCoordinator.MarkDead(deathCancellationInstance);
            Require(dispatchedBeforeDeath.EvaluateStatus == UnitActionReducerStatus.Accepted
                && !deathCancellationCoordinator.TryConsumeLegacyImpactCancellation(
                    deathCancellationToken, 0)
                && deathCancellationCoordinator.TryConsumeLegacyImpactCancellation(
                    deathCancellationToken, 1)
                && deathCancellationCoordinator.TryConsumeLegacyImpactCancellation(
                    deathCancellationToken, 1),
                "Attacker death must preserve an already authorized hit and idempotently cancel every undispatched non-persistent future hit.");
            UnitAttackShadowCoordinator.ResultObservation completedAfterDeath =
                deathCancellationCoordinator.CompleteLegacyImpact(
                    deathCancellationToken,
                    0,
                    AppliedDamage(1, 99),
                    deathCancellationCommit.Snapshot.CommitServerTime
                        + unit.HitFrameTimes[0] + 0.001d);
            Require(completedAfterDeath.CompletionStatus == UnitActionReducerStatus.Accepted
                && completedAfterDeath.HasResult,
                "An impact authorized before attacker death must retain its one legitimate completion while cancelled future hits produce none.");
        }

        private static void ValidateB3PostCombatRecoveryPlanner()
        {
            var route = new List<HexCoord>
            {
                new HexCoord(5, 3),
                new HexCoord(5, 2),
                new HexCoord(6, 1),
                new HexCoord(6, 0),
                new HexCoord(6, -2)
            };
            var planner = new UnitPostCombatRecoveryPlanner();
            var probed = new List<HexCoord>();

            UnitPostCombatRecoveryResult Probe(
                long frame,
                ulong environmentRevision,
                HexCoord root,
                Func<HexCoord, IReadOnlyList<HexCoord>> request)
                => planner.Evaluate(
                    frame,
                    environmentRevision,
                    root,
                    route,
                    nextWaypointIndex: 1,
                    finalGoal: route[route.Count - 1],
                    candidate =>
                    {
                        probed.Add(candidate);
                        return request(candidate);
                    });

            UnitPostCombatRecoveryResult first = Probe(
                100L,
                1UL,
                route[0],
                _ => null);
            Require(first.Status == UnitPostCombatRecoveryStatus.Waiting
                && probed.Count == 1
                && probed[0] == route[1],
                "A Unit84-shaped first post-combat Unreachable must wait after probing exactly one forward checkpoint, not become Blocked.");

            UnitPostCombatRecoveryResult sameFrame = Probe(
                100L,
                1UL,
                route[0],
                _ => throw new InvalidOperationException("same-frame request must not run"));
            Require(sameFrame.Status == UnitPostCombatRecoveryStatus.DeferredSameFrame
                && probed.Count == 1,
                "Post-combat recovery must issue at most one path request in a Unity frame.");

            IReadOnlyList<HexCoord> recoveredPath = new List<HexCoord>
            {
                route[0],
                route[2]
            };
            UnitPostCombatRecoveryResult recovered = Probe(
                101L,
                1UL,
                route[0],
                candidate => candidate == route[2] ? recoveredPath : null);
            Require(recovered.Status == UnitPostCombatRecoveryStatus.Recovered
                && ReferenceEquals(recovered.Path, recoveredPath)
                && probed.Count == 2
                && probed[1] == route[2],
                "Post-combat recovery must choose the first actually reachable forward checkpoint in route order.");
            planner.AcceptStagedPath();

            // 실제 공간 진전은 과거 실패 이력을 무효화한다. 새 Root에서 첫 실패가 다시
            // Waiting이어야 하며 이전 위치의 소진 횟수로 즉시 Blocked가 되면 안 된다.
            UnitPostCombatRecoveryResult progressed = Probe(
                102L,
                1UL,
                route[1],
                _ => null);
            Require(progressed.Status == UnitPostCombatRecoveryStatus.Waiting,
                "Spatial Root progress must reset post-combat exhaustion history.");

            var blockedPlanner = new UnitPostCombatRecoveryPlanner();
            int candidateCount = route.Count - 1;
            UnitPostCombatRecoveryResult terminal = default;
            long frameToken = 200L;
            for (int probe = 0; probe < candidateCount * 2; probe++)
            {
                terminal = blockedPlanner.Evaluate(
                    frameToken++,
                    7UL,
                    route[0],
                    route,
                    1,
                    route[route.Count - 1],
                    _ => null);
                if (probe < candidateCount * 2 - 1)
                {
                    Require(terminal.Status == UnitPostCombatRecoveryStatus.Waiting,
                        "One incomplete or first complete recovery sweep must remain Waiting.");
                }
            }
            Require(terminal.Status == UnitPostCombatRecoveryStatus.ConfirmedBlocked,
                "Only two complete same-objective, same-environment, no-progress recovery sweeps may confirm Blocked.");

            UnitPostCombatRecoveryResult environmentChanged = blockedPlanner.Evaluate(
                frameToken,
                8UL,
                route[0],
                route,
                1,
                route[route.Count - 1],
                _ => null);
            Require(environmentChanged.Status == UnitPostCombatRecoveryStatus.Waiting,
                "A walkability revision change must reset confirmed post-combat exhaustion.");
        }

        private static void ValidateB3PostCombatImmediateReentry()
        {
            var flow = new UnitPostCombatReentryFlow();

            UnitPostCombatReentryResult initialRecovery = flow.BeginRecovery(100L);
            Require(initialRecovery.Action == UnitPostCombatReentryAction.RecalculateRecovery
                    && initialRecovery.RecoveryGeneration == 1UL
                    && !initialRecovery.AllowsObjectiveCompletion,
                "Post-combat recovery must begin as a live navigation phase, not as an already completed objective.");

            UnitPostCombatReentryResult firstReentry = flow.ObserveTargetAcquired(100L);
            Require(firstReentry.Action == UnitPostCombatReentryAction.EnterPursuit
                    && firstReentry.RecoveryGeneration == 1UL
                    && !firstReentry.AllowsObjectiveCompletion,
                "A target reacquired during post-combat recovery must enter pursuit in the same coroutine lifetime without completing the objective.");

            Require(!flow.TryCompleteRecovery(),
                "Recovery must not complete while the reacquired-target pursuit owns the flow.");

            UnitPostCombatReentryResult pursuitEnded = flow.ObservePursuitCompleted(
                100L,
                unitAlive: true,
                isHealer: false,
                combatPursuitRequiresRepath: false);
            Require(pursuitEnded.Action
                        == UnitPostCombatReentryAction.DeferRecoveryUntilNextFrame
                    && pursuitEnded.RecoveryGeneration == 1UL
                    && !pursuitEnded.AllowsObjectiveCompletion,
                "A completed reentry pursuit must schedule fresh recovery instead of falling through to movement cleanup.");

            UnitPostCombatReentryResult sameFrameRetry = flow.BeginRecovery(100L);
            Require(sameFrameRetry.Action
                        == UnitPostCombatReentryAction.DeferRecoveryUntilNextFrame
                    && sameFrameRetry.RecoveryGeneration == 1UL,
                "Post-combat reentry must not create a recursive or unbounded same-frame recovery loop.");

            UnitPostCombatReentryResult freshRecovery = flow.BeginRecovery(101L);
            Require(freshRecovery.Action == UnitPostCombatReentryAction.RecalculateRecovery
                    && freshRecovery.RecoveryGeneration == 2UL
                    && !freshRecovery.AllowsObjectiveCompletion,
                "The frame after pursuit must start a new recovery generation so stale forward-tile and alignment values cannot be reused.");

            UnitPostCombatReentryResult secondReentry = flow.ObserveTargetAcquired(101L);
            UnitPostCombatReentryResult secondPursuitEnded = flow.ObservePursuitCompleted(
                101L,
                unitAlive: true,
                isHealer: false,
                combatPursuitRequiresRepath: false);
            UnitPostCombatReentryResult secondFreshRecovery = flow.BeginRecovery(102L);
            Require(secondReentry.Action == UnitPostCombatReentryAction.EnterPursuit
                    && secondPursuitEnded.Action
                        == UnitPostCombatReentryAction.DeferRecoveryUntilNextFrame
                    && secondFreshRecovery.Action
                        == UnitPostCombatReentryAction.RecalculateRecovery
                    && secondFreshRecovery.RecoveryGeneration == 3UL,
                "Repeated recovery-to-pursuit transitions must stay iterative and advance one bounded generation per completed pursuit.");

            Require(flow.TryCompleteRecovery(),
                "A live recovery generation may complete only after no reentry pursuit remains active.");

            var repathFlow = new UnitPostCombatReentryFlow();
            repathFlow.BeginRecovery(200L);
            repathFlow.ObserveTargetAcquired(200L);
            UnitPostCombatReentryResult repathBeforeRecovery =
                repathFlow.ObservePursuitCompleted(
                    200L,
                    unitAlive: true,
                    isHealer: false,
                    combatPursuitRequiresRepath: true);
            Require(repathBeforeRecovery.Action
                        == UnitPostCombatReentryAction.RepathBeforeRecovery
                    && !repathBeforeRecovery.AllowsObjectiveCompletion,
                "A normal unit whose reentry pursuit invalidated its spatial route must repath before post-combat recovery can resume.");

            var healerFlow = new UnitPostCombatReentryFlow();
            healerFlow.BeginRecovery(300L);
            healerFlow.ObserveTargetAcquired(300L);
            UnitPostCombatReentryResult healerRecovery =
                healerFlow.ObservePursuitCompleted(
                    300L,
                    unitAlive: true,
                    isHealer: true,
                    combatPursuitRequiresRepath: true);
            Require(healerRecovery.Action
                        == UnitPostCombatReentryAction.DeferRecoveryUntilNextFrame
                    && !healerRecovery.AllowsObjectiveCompletion,
                "A healer reentry must preserve its heal-loop recovery contract and must not consume the combat-pursuit repath branch.");
        }

        private static void ValidateC3ContinuousAuthoritativeTimeline()
        {
            double[] littleKnightMarkers = { 0.25d, 1.15d };

            // 첫 marker가 지난 phase에서는 이전 marker를 소급 승인하거나 Animator를
            // restart하지 않고 다음 cycle까지 기다려야 한다.
            AttackPresentationTimelineWindow passedFirst =
                UnitAttackPresentationPolicy.ResolveContinuousTimeline(
                    presentationEpochServerTime: 10d,
                    observedServerTime: 10.30d,
                    cycleDurationSeconds: 2d,
                    impactOffsetsSeconds: littleKnightMarkers);
            Require(passedFirst.IsValid
                && !passedFirst.ShouldCommit
                && Math.Abs(passedFirst.WaitSeconds - 1.70d) < 0.000001d,
                "A continuous LittleKnight cycle must wait after its first marker instead of rewinding or retroactively approving it.");

            // 다음 cycle 0.10초 phase에서 커밋하면 실제 다음 marker occurrences는
            // observed+0.15, observed+1.05다. Legacy damage와 Shadow timeline이 이
            // 동일 effective offset 배열을 원자적으로 사용해야 한다.
            AttackPresentationTimelineWindow aligned =
                UnitAttackPresentationPolicy.ResolveContinuousTimeline(
                    presentationEpochServerTime: 10d,
                    observedServerTime: 12.10d,
                    cycleDurationSeconds: 2d,
                    impactOffsetsSeconds: littleKnightMarkers);
            Require(aligned.IsValid
                && aligned.ShouldCommit
                && aligned.ImpactCount == 2
                && Math.Abs(aligned.OvershootSeconds - 0.10d) < 0.000001d
                && Math.Abs(aligned.EffectiveCooldownSeconds - 1.90d) < 0.000001d
                && Math.Abs(aligned.GetEffectiveImpactOffset(0) - 0.15d) < 0.000001d
                && Math.Abs(aligned.GetEffectiveImpactOffset(1) - 1.05d) < 0.000001d
                && Math.Abs(12.10d + aligned.GetEffectiveImpactOffset(0) - 12.25d)
                    < 0.000001d
                && Math.Abs(12.10d + aligned.GetEffectiveImpactOffset(1) - 13.15d)
                    < 0.000001d,
                "Continuous authoritative timing must bind both LittleKnight HitIndexes to the actual next marker occurrences from one epoch/phase calculation.");

            AttackPresentationTimelineWindow exactBoundary =
                UnitAttackPresentationPolicy.ResolveContinuousTimeline(
                    10d,
                    12.25d,
                    2d,
                    littleKnightMarkers);
            Require(exactBoundary.IsValid
                && exactBoundary.ShouldCommit
                && exactBoundary.GetEffectiveImpactOffset(0) == 0d
                && Math.Abs(exactBoundary.GetEffectiveImpactOffset(1) - 0.90d)
                    < 0.000001d,
                "The exact first-marker boundary must schedule HitIndex zero now and subtract phase exactly once from later hits.");

            Require(UnitAttackPresentationPolicy.ShouldAlignToPresentationEpoch(
                    attackPresentationActive: true,
                    commitPending: false)
                && UnitAttackPresentationPolicy.ShouldAlignToPresentationEpoch(
                    attackPresentationActive: false,
                    commitPending: true)
                && !UnitAttackPresentationPolicy.ShouldAlignToPresentationEpoch(
                    attackPresentationActive: false,
                    commitPending: false),
                "Every active continuous Attack commit, not only a restart-pending retarget, must use the authoritative presentation epoch.");
        }

        private static void ValidateC3AuthoritativeResultPresentationShadow()
        {
            var latch = new CombatPipelineContractLatch();
            Require(latch.TryBeginMatch(CombatPipelineMode.PresentationShadow, 1, "hash-a")
                && latch.TryBeginMatch(CombatPipelineMode.PresentationShadow, 1, "hash-a")
                && !latch.TryBeginMatch(CombatPipelineMode.Legacy, 1, "hash-a")
                && !latch.TryBeginMatch(CombatPipelineMode.PresentationShadow, 2, "hash-a")
                && !latch.TryBeginMatch(CombatPipelineMode.PresentationShadow, 1, "hash-b"),
                "C3 combat mode/schema/profile hash must be match-fixed and idempotent only for the same contract.");
            Require(UnitAttackShadowProfileResolver.TryComputePresentationProfileHash(
                    out string firstHash, out _)
                && UnitAttackShadowProfileResolver.TryComputePresentationProfileHash(
                    out string secondHash, out _)
                && firstHash.Length == 64 && firstHash == secondHash,
                "C3 25-type profile hash must be deterministic and include unresolved manifest rows.");

            Require(UnitAttackShadowProfileResolver.TryResolve(
                    UnitType.StreamSpirit,
                    out UnitAttackShadowProfile unresolvedStream)
                && UnitAttackShadowProfileResolver.ClassifyAuditEligibility(
                    unresolvedStream,
                    runtimeContractValid: false)
                    == AttackPresentationAuditEligibility.KnownUnresolved
                && UnitAttackShadowProfileResolver.TryResolve(
                    UnitType.LittleKnight,
                    out UnitAttackShadowProfile supportedKnight)
                && UnitAttackShadowProfileResolver.ClassifyAuditEligibility(
                    supportedKnight,
                    runtimeContractValid: true)
                    == AttackPresentationAuditEligibility.Required
                && UnitAttackShadowProfileResolver.ClassifyAuditEligibility(
                    supportedKnight,
                    runtimeContractValid: false)
                    == AttackPresentationAuditEligibility.RequiredButMissing,
                "C3 audit eligibility must exclude declared KnownUnresolved profiles from the normal invalid denominator while supported runtime contract loss remains a failure.");

            int eligibilityClassifications = 0;
            PresentationShadowClassification lastEligibilityClassification = default;
            void CountEligibilityClassification(PresentationShadowClassification classification)
            {
                eligibilityClassifications++;
                lastEligibilityClassification = classification;
            }
            UnitAttackResultPresentationShadowBridge.Classified +=
                CountEligibilityClassification;
            Require(UnitAttackResultPresentationShadowBridge.IsPresentationSpatialMatch(
                        10d, 20d, 10.5d, 20.5d, out double nearSpatialDelta)
                    && nearSpatialDelta < UnitAttackResultPresentationShadowBridge
                        .MaximumPresentationVictimMotionEnvelope
                && !UnitAttackResultPresentationShadowBridge.IsPresentationSpatialMatch(
                        10d, 20d, -10d, -20d, out double flippedSpatialDelta)
                && flippedSpatialDelta > UnitAttackResultPresentationShadowBridge
                    .MaximumPresentationVictimMotionEnvelope,
                "C3 spatial proof must tolerate bounded victim motion but reject a large missing/double view flip.");
            UnitAttackResultPresentationShadowBridge.BeginMatch(
                active: true,
                synchronizedServerTime: 100d,
                localMonotonicTime: 10d);
            UnitAttackResultPresentationShadowBridge.RegisterAuditEligibility(
                attackerUnitId: 15,
                AttackPresentationAuditEligibility.KnownUnresolved);
            UnitAttackResultPresentationShadowBridge.ObserveLegacy(
                new LegacyPresentationObservation(
                    LegacyPresentationObservationKind.Enqueued,
                    15,
                    1,
                    20,
                    90,
                    10.1d,
                    false,
                    default));
            Require(eligibilityClassifications == 0,
                "KnownUnresolved legacy gameplay/VFX must continue without entering the supported C3 invalid denominator.");

            UnitAttackResultPresentationShadowBridge.RegisterAuditEligibility(
                attackerUnitId: 16,
                AttackPresentationAuditEligibility.RequiredButMissing);
            UnitAttackResultPresentationShadowBridge.ObserveLegacy(
                new LegacyPresentationObservation(
                    LegacyPresentationObservationKind.Enqueued,
                    16,
                    1,
                    20,
                    90,
                    10.2d,
                    false,
                    default));
            Require(eligibilityClassifications == 1,
                "A supported profile whose runtime contract is missing must fail closed instead of being hidden as unresolved coverage.");
            UnitAttackResultPresentationShadowBridge.RegisterAuditEligibility(
                attackerUnitId: 17,
                AttackPresentationAuditEligibility.Required);
            UnitAttackResultPresentationShadowBridge.ObserveStructuralFailure(
                new LegacyPresentationObservation(
                    LegacyPresentationObservationKind.StructuralCapacityExceeded,
                    17,
                    0,
                    -1,
                    -1,
                    10.3d,
                    false,
                    default,
                    default,
                    new AttackPresentationScope(
                        new AttackerInstanceId(170UL),
                        new AttackSequenceId(17UL),
                        0)));
            Require(eligibilityClassifications == 2
                && lastEligibilityClassification.Status
                    == PresentationShadowResultStatus.Invalid
                && lastEligibilityClassification.ObservationKind
                    == LegacyPresentationObservationKind.StructuralCapacityExceeded,
                "C3 CapacityExceeded/Invalid fallback must emit separate structural failure evidence instead of becoming a healthy presentation PASS.");
            UnitAttackResultPresentationShadowBridge.EndMatch();
            UnitAttackResultPresentationShadowBridge.Classified -=
                CountEligibilityClassification;

            ActionDirectionXZ.TryCreate(1d, 0d, out ActionDirectionXZ east);
            WorldPointXZ.TryCreate(1d, 2d, out WorldPointXZ impactPoint);
            AttackResultPresentationInput CreateInput(
                ulong instance, ulong sequence, int ordinal, double impact, int hp,
                ulong revision = 1UL)
            {
                return new AttackResultPresentationInput(
                    7, revision,
                    new AttackResultKey(new AttackerInstanceId(instance),
                        new AttackSequenceId(sequence), 0, 1, 11, 0, ordinal),
                    AttackDeliveryKind.Hitscan, impact, east, true, impactPoint,
                    AttackImpactOutcome.HitApplied, 10, hp,
                    (int)UnitType.SpearMan, (int)TeamId.Red);
            }
            LegacyPresentationObservation Observe(
                AttackResultPresentationInput input,
                LegacyPresentationObservationKind kind,
                double time,
                bool exactResult)
                => new LegacyPresentationObservation(
                    kind,
                    input.AttackerUnitId,
                    input.Key.VictimKind,
                    input.Key.VictimId,
                    exactResult ? input.ResultingHp : -1,
                    time,
                    kind == LegacyPresentationObservationKind.Marker
                        || kind == LegacyPresentationObservationKind.TracerImpact,
                    east,
                    exactResult ? input.Key : default,
                    AttackPresentationScope.FromResultKey(input.Key));

            var resultFirst = new UnitAttackResultPresentationShadowScheduler();
            AttackResultPresentationInput first = CreateInput(1001UL, 1UL, 0, 10d, 90);
            Require(resultFirst.ObserveResult(first, 9.9d).Status
                    == PresentationShadowResultStatus.Scheduled
                && resultFirst.ObserveResult(first, 9.91d).Status
                    == PresentationShadowResultStatus.Duplicate
                && resultFirst.ObserveResult(CreateInput(1001UL, 1UL, 0, 10d, 90, 2UL), 9.92d).Status
                    == PresentationShadowResultStatus.Conflict
                && resultFirst.ObserveLegacy(Observe(
                    first, LegacyPresentationObservationKind.EmittedMarker,
                    10.1d, true)).Status
                    == PresentationShadowResultStatus.Matched,
                "C3 result-first flow must schedule once, reject conflict and converge on one Legacy emit.");

            var legacyFirst = new UnitAttackResultPresentationShadowScheduler();
            AttackResultPresentationInput second = CreateInput(1002UL, 2UL, 0, 20d, 80);
            Require(legacyFirst.ObserveLegacy(Observe(
                        second, LegacyPresentationObservationKind.Enqueued,
                        20.1d, true)).Status
                    == PresentationShadowResultStatus.Scheduled
                && legacyFirst.ObserveResult(second, 19.9d).Status
                    == PresentationShadowResultStatus.Matched,
                "C3 Legacy-first flow must retain bounded evidence and converge when the exact result arrives.");

            var overlapping = new UnitAttackResultPresentationShadowScheduler();
            AttackResultPresentationInput overlapA = CreateInput(1003UL, 3UL, 0, 30d, 70);
            AttackResultPresentationInput overlapB = CreateInput(1003UL, 3UL, 1, 30d, 70);
            overlapping.ObserveResult(overlapA, 29.9d);
            overlapping.ObserveResult(overlapB, 29.9d);
            Require(overlapping.ObserveLegacy(Observe(
                        overlapA, LegacyPresentationObservationKind.Enqueued,
                        30.1d, true)).Status == PresentationShadowResultStatus.Matched
                && overlapping.ObserveLegacy(Observe(
                        overlapA, LegacyPresentationObservationKind.Enqueued,
                        30.11d, true)).Status == PresentationShadowResultStatus.Duplicate
                && overlapping.ObserveLegacy(Observe(
                        overlapB, LegacyPresentationObservationKind.Enqueued,
                        30.12d, true)).Status == PresentationShadowResultStatus.Matched,
                "C3 must use the full result key for overlapping same-attacker/same-victim results and consume each stage once.");

            var staged = new UnitAttackResultPresentationShadowScheduler();
            AttackResultPresentationInput stagedInput = CreateInput(1007UL, 7UL, 0, 35d, 65);
            staged.ObserveResult(stagedInput, 34.9d);
            Require(staged.ObserveLegacy(Observe(
                        stagedInput, LegacyPresentationObservationKind.Marker,
                        35d, false)).Status == PresentationShadowResultStatus.Matched
                && staged.ObserveLegacy(Observe(
                        stagedInput, LegacyPresentationObservationKind.Marker,
                        35.01d, false)).Status == PresentationShadowResultStatus.Duplicate
                && staged.ObserveLegacy(Observe(
                        stagedInput, LegacyPresentationObservationKind.EmittedMarker,
                        35.1d, true)).Status == PresentationShadowResultStatus.Matched
                && staged.ObserveLegacy(Observe(
                        stagedInput, LegacyPresentationObservationKind.EmittedTimeout,
                        35.2d, true)).Status == PresentationShadowResultStatus.Duplicate,
                "C3 marker and terminal emit stages must each consume a canonical scope/result at most once.");

            var victimlessMarker = new UnitAttackResultPresentationShadowScheduler();
            AttackResultPresentationInput victimlessInput =
                CreateInput(1012UL, 12UL, 0, 36d, 63);
            victimlessMarker.ObserveResult(victimlessInput, 35.9d);
            Require(victimlessMarker.ObserveLegacy(
                        new LegacyPresentationObservation(
                            LegacyPresentationObservationKind.Marker,
                            victimlessInput.AttackerUnitId,
                            1,
                            -1,
                            -1,
                            36d,
                            true,
                            east,
                            default,
                            AttackPresentationScope.FromResultKey(
                                victimlessInput.Key))).Status
                    == PresentationShadowResultStatus.Matched,
                "C3 scope-only Marker must match its exact attack scope even when Stop/target loss already cleared the current victim Id.");
            var strictResultObservation = new UnitAttackResultPresentationShadowScheduler();
            strictResultObservation.ObserveResult(victimlessInput, 35.9d);
            Require(strictResultObservation.ObserveLegacy(
                        new LegacyPresentationObservation(
                            LegacyPresentationObservationKind.Enqueued,
                            victimlessInput.AttackerUnitId,
                            victimlessInput.Key.VictimKind,
                            victimlessInput.Key.VictimId + 1,
                            victimlessInput.ResultingHp,
                            36d,
                            false,
                            default,
                            victimlessInput.Key,
                            AttackPresentationScope.FromResultKey(
                                victimlessInput.Key))).Status
                    == PresentationShadowResultStatus.Scheduled,
                "C3 ResultKey Enqueue/Emit evidence must keep strict victim/HP identity instead of inheriting the scope-only Marker relaxation.");

            var resultFirstRendezvous =
                new AttackPresentationImpactRendezvous<int>();
            var releasedPayloads = new List<int>();
            AttackResultKey rendezvousResultKey = victimlessInput.Key;
            AttackPresentationScope rendezvousScope =
                AttackPresentationScope.FromResultKey(rendezvousResultKey);
            var wrongRendezvousScope = new AttackPresentationScope(
                rendezvousScope.AttackerInstanceId,
                new AttackSequenceId(rendezvousScope.SequenceId.Value + 1UL),
                rendezvousScope.HitIndex);
            Require(resultFirstRendezvous.ObserveResult(
                        victimlessInput.AttackerUnitId,
                        rendezvousResultKey,
                        10,
                        40d,
                        1d,
                        releasedPayloads)
                    == AttackPresentationRendezvousStatus.Buffered
                && resultFirstRendezvous.ObserveSignal(
                        victimlessInput.AttackerUnitId,
                        wrongRendezvousScope,
                        40.01d,
                        releasedPayloads)
                    == AttackPresentationRendezvousStatus.Buffered
                && releasedPayloads.Count == 0
                && resultFirstRendezvous.ObserveSignal(
                        victimlessInput.AttackerUnitId,
                        rendezvousScope,
                        40.02d,
                        releasedPayloads)
                    == AttackPresentationRendezvousStatus.Released
                && releasedPayloads.Count == 1
                && releasedPayloads[0] == 10,
                "C3 result-first presentation must release only on the exact scope and never on the next attack sequence.");

            var markerFirstRendezvous =
                new AttackPresentationImpactRendezvous<int>();
            releasedPayloads.Clear();
            Require(markerFirstRendezvous.ObserveSignal(
                        victimlessInput.AttackerUnitId,
                        rendezvousScope,
                        50d,
                        releasedPayloads)
                    == AttackPresentationRendezvousStatus.Buffered
                && markerFirstRendezvous.ObserveResult(
                        victimlessInput.AttackerUnitId,
                        rendezvousResultKey,
                        20,
                        50.01d,
                        1d,
                        releasedPayloads)
                    == AttackPresentationRendezvousStatus.Released
                && releasedPayloads.Count == 1
                && releasedPayloads[0] == 20,
                "C3 marker-first presentation must retain the exact pulse and emit the same-scope result on arrival.");

            // 한 marker scope 아래 여러 피해자가 생기는 AoE는 marker를 첫 피해자에서 닫지 않는다.
            // 피해자별 전체 결과 키는 서로 달라도 같은 scope라면 같은 타격 순간에 방출된다.
            AttackResultKey aoeA = new AttackResultKey(
                rendezvousScope.AttackerInstanceId,
                rendezvousScope.SequenceId,
                rendezvousScope.HitIndex,
                1,
                21,
                0,
                0);
            AttackResultKey aoeB = new AttackResultKey(
                rendezvousScope.AttackerInstanceId,
                rendezvousScope.SequenceId,
                rendezvousScope.HitIndex,
                1,
                22,
                0,
                1);
            releasedPayloads.Clear();
            Require(markerFirstRendezvous.ObserveResult(
                        victimlessInput.AttackerUnitId, aoeA, 21, 50.02d, 1d,
                        releasedPayloads) == AttackPresentationRendezvousStatus.Released
                && markerFirstRendezvous.ObserveResult(
                        victimlessInput.AttackerUnitId, aoeB, 22, 50.03d, 1d,
                        releasedPayloads) == AttackPresentationRendezvousStatus.Released
                && releasedPayloads.Count == 2
                && releasedPayloads[0] == 21
                && releasedPayloads[1] == 22
                && markerFirstRendezvous.ObserveResult(
                        victimlessInput.AttackerUnitId, aoeB, 22, 50.04d, 1d,
                        releasedPayloads) == AttackPresentationRendezvousStatus.Duplicate
                && releasedPayloads.Count == 2,
                "C3 exact-scope rendezvous must release every AoE result once while rejecting duplicate result keys.");

            var timeoutRendezvous =
                new AttackPresentationImpactRendezvous<int>();
            releasedPayloads.Clear();
            Require(timeoutRendezvous.ObserveResult(
                        victimlessInput.AttackerUnitId,
                        rendezvousResultKey,
                        30,
                        60d,
                        0.5d,
                        releasedPayloads)
                    == AttackPresentationRendezvousStatus.Buffered
                && timeoutRendezvous.ObserveSignal(
                        victimlessInput.AttackerUnitId,
                        wrongRendezvousScope,
                        60.1d,
                        releasedPayloads)
                    == AttackPresentationRendezvousStatus.Buffered,
                "C3 timeout fixture must keep a wrong-scope marker from consuming the pending result.");
            timeoutRendezvous.CollectExpired(60.499d, releasedPayloads);
            Require(releasedPayloads.Count == 0,
                "C3 exact-scope result must remain pending before its timeout boundary.");
            timeoutRendezvous.CollectExpired(60.5d, releasedPayloads);
            Require(releasedPayloads.Count == 1 && releasedPayloads[0] == 30,
                "C3 timeout must release only the still-unmatched exact-scope result and remain a diagnostic recovery.");

            var retiredSignalRendezvous =
                new AttackPresentationImpactRendezvous<int>();
            releasedPayloads.Clear();
            Require(retiredSignalRendezvous.ObserveSignal(
                        victimlessInput.AttackerUnitId,
                        rendezvousScope,
                        61d,
                        releasedPayloads) == AttackPresentationRendezvousStatus.Buffered
                && retiredSignalRendezvous.RetireSequence(
                        victimlessInput.AttackerUnitId,
                        rendezvousScope) == AttackPresentationRendezvousStatus.Released
                && retiredSignalRendezvous.ObserveResult(
                        victimlessInput.AttackerUnitId,
                        rendezvousResultKey,
                        31,
                        61.1d,
                        0.5d,
                        releasedPayloads) == AttackPresentationRendezvousStatus.Retired
                && releasedPayloads.Count == 0,
                "C3 target-change/death/stop retire must remove signal-only state and reject a late old-scope result without emission.");
            retiredSignalRendezvous.CollectExpired(62d, releasedPayloads);
            Require(releasedPayloads.Count == 0,
                "A retired old-scope result must never reappear through timeout recovery.");

            var lifecycleRendezvous =
                new AttackPresentationImpactRendezvous<int>();
            AttackResultKey hitOneKey = new AttackResultKey(
                rendezvousScope.AttackerInstanceId,
                rendezvousScope.SequenceId,
                1,
                rendezvousResultKey.VictimKind,
                rendezvousResultKey.VictimId,
                rendezvousResultKey.EffectKind,
                rendezvousResultKey.ResultOrdinal);
            AttackPresentationScope hitOneScope =
                AttackPresentationScope.FromResultKey(hitOneKey);
            releasedPayloads.Clear();
            lifecycleRendezvous.ObserveResult(
                victimlessInput.AttackerUnitId,
                rendezvousResultKey,
                40,
                70d,
                1d,
                releasedPayloads);
            lifecycleRendezvous.ObserveResult(
                victimlessInput.AttackerUnitId,
                hitOneKey,
                41,
                70d,
                1d,
                releasedPayloads);
            Require(lifecycleRendezvous.ObserveSignal(
                        victimlessInput.AttackerUnitId,
                        rendezvousScope,
                        70.1d,
                        releasedPayloads)
                    == AttackPresentationRendezvousStatus.Released
                && releasedPayloads.Count == 1
                && releasedPayloads[0] == 40
                && lifecycleRendezvous.ObserveSignal(
                        victimlessInput.AttackerUnitId,
                        hitOneScope,
                        70.2d,
                        releasedPayloads)
                    == AttackPresentationRendezvousStatus.Released
                && releasedPayloads.Count == 2
                && releasedPayloads[1] == 41,
                "C3 multi-hit presentation must keep adjacent HitIndex scopes independent.");

            var flushRendezvous =
                new AttackPresentationImpactRendezvous<int>();
            releasedPayloads.Clear();
            flushRendezvous.ObserveResult(
                victimlessInput.AttackerUnitId,
                rendezvousResultKey,
                50,
                80d,
                1d,
                releasedPayloads);
            flushRendezvous.ObserveResult(
                victimlessInput.AttackerUnitId,
                hitOneKey,
                51,
                80d,
                1d,
                releasedPayloads);
            flushRendezvous.FlushWhere(value => value == 50, releasedPayloads);
            Require(releasedPayloads.Count == 1 && releasedPayloads[0] == 50,
                "C3 target-death recovery must flush only matching exact pending results.");
            flushRendezvous.FlushAttacker(
                victimlessInput.AttackerUnitId,
                releasedPayloads);
            Require(releasedPayloads.Count == 2
                    && releasedPayloads[1] == 51
                    && flushRendezvous.ScopeCountForValidation == 0,
                "C3 attacker stop/death recovery must flush and retire every remaining exact scope without crossing attackers.");

            var multiPending = new UnitAttackResultPresentationShadowScheduler();
            AttackResultPresentationInput pendingInput = CreateInput(1008UL, 8UL, 0, 37d, 55);
            multiPending.ObserveLegacy(Observe(
                pendingInput, LegacyPresentationObservationKind.Marker, 37d, false));
            multiPending.ObserveLegacy(Observe(
                pendingInput, LegacyPresentationObservationKind.Enqueued, 37.01d, true));
            PresentationShadowClassification pendingPrimary =
                multiPending.ObserveResult(pendingInput, 36.9d);
            Require(pendingPrimary.Status == PresentationShadowResultStatus.Matched
                && multiPending.TryDequeueClassification(
                    out PresentationShadowClassification pendingAdditional)
                && pendingAdditional.Status == PresentationShadowResultStatus.Matched
                && !multiPending.TryDequeueClassification(out _),
                "C3 result arrival must drain every already-buffered exact stage without losing one classification.");

            var boundaries = new UnitAttackResultPresentationShadowScheduler();
            Require(boundaries.ObserveResult(CreateInput(1004UL, 4UL, 0, 40d, 60), 40.6d).Status
                    == PresentationShadowResultStatus.CatchUp
                && boundaries.ObserveResult(CreateInput(1005UL, 5UL, 0, 50d, 50), 50.601d).Status
                    == PresentationShadowResultStatus.Expired,
                "C3 must subtract the 0.100 second presentation delay, allow a 0.500 second catch-up, and expire 0.501 second late results.");

            var reorderBoundary = new UnitAttackResultPresentationShadowScheduler();
            AttackResultPresentationInput boundaryInput = CreateInput(1010UL, 10UL, 0, 60d, 30);
            reorderBoundary.ObserveResult(boundaryInput, 60d);
            Require(reorderBoundary.ObserveLegacy(Observe(
                        boundaryInput, LegacyPresentationObservationKind.EmittedMarker,
                        62d, true)).Status
                    == PresentationShadowResultStatus.Matched,
                "C3 must allow the exact 2.000 second reorder boundary.");
            AttackResultPresentationInput outsideBoundary = CreateInput(1011UL, 11UL, 0, 70d, 20);
            reorderBoundary.ObserveResult(outsideBoundary, 70d);
            Require(reorderBoundary.ObserveLegacy(Observe(
                        outsideBoundary, LegacyPresentationObservationKind.EmittedMarker,
                        72.001d, true)).Status
                    == PresentationShadowResultStatus.Scheduled,
                "C3 must reject evidence outside the 2.000 second reorder boundary.");

            var concurrentSequences = new UnitAttackResultPresentationShadowScheduler();
            for (int index = 0; index <= UnitAttackResultPresentationShadowScheduler.MaximumBufferedResults; index++)
            {
                concurrentSequences.ObserveResult(
                    CreateInput((ulong)(2000 + index), (ulong)(100 + index), 0,
                        1000d, 100 - index),
                    999.9d);
            }
            Require(concurrentSequences.BufferedResultCountForValidation
                    == UnitAttackResultPresentationShadowScheduler.MaximumBufferedResults + 1,
                "C3 must not let 65 concurrent attacker sequences evict one another through a global 64-entry cap.");
            var boundedSequence = new UnitAttackResultPresentationShadowScheduler();
            for (int index = 0; index <= UnitAttackResultPresentationShadowScheduler.MaximumBufferedResults; index++)
            {
                boundedSequence.ObserveResult(
                    CreateInput(3000UL, 300UL, index, 1100d, 100 - index),
                    1099.9d);
            }
            Require(boundedSequence.BufferedResultCountForValidation
                    == UnitAttackResultPresentationShadowScheduler.MaximumBufferedResults
                && boundedSequence.TryDequeueClassification(
                    out PresentationShadowClassification sequenceOverflow)
                && sequenceOverflow.Status == PresentationShadowResultStatus.Unmatched,
                "C3 result reorder storage must enforce and expose the 64-entry bound per attacker sequence.");
            var retired = new UnitAttackResultPresentationShadowScheduler();
            AttackResultPresentationInput retiredInput = CreateInput(1006UL, 6UL, 0, 60d, 40);
            retired.Retire(retiredInput.Key.AttackerInstanceId);
            Require(retired.ObserveResult(retiredInput, 59.9d).Status
                    == PresentationShadowResultStatus.InstanceRetired,
                "C3 must reject late results after attacker instance retirement.");

            Require(ActionDirectionXZ.TryCreate(-1d, 0d, out ActionDirectionXZ redViewForward)
                && UnitAttackResultPresentationShadowScheduler.FlipDirection180(redViewForward)
                    .Equals(east),
                "C3 must convert a Red view-space marker direction back to the authoritative domain direction.");
            Require(!UnitAttackShadowObserver.IsPresentationAimMismatchForValidation(8.000d)
                && UnitAttackShadowObserver.IsPresentationAimMismatchForValidation(8.001d),
                "C3 presentation direction must share the named 8.000/8.001-degree gameplay alignment boundary.");
            Require(UnitAttackShadowObserver.ClassifyDirectionRevisionForValidation(
                        resultActionRevision: 9UL,
                        resultInstanceId: 1006UL,
                        resultSequenceId: 6UL,
                        hasReplicatedState: true,
                        replicatedInstanceId: 1006UL,
                        replicatedSequenceId: 6UL,
                        replicatedRevision: 9UL,
                        aimDeltaDegrees: 8.001d)
                    == PresentationDirectionRevisionClassification.SameRevisionVisualMismatch
                && UnitAttackShadowObserver.ClassifyDirectionRevisionForValidation(
                        9UL, 1006UL, 6UL,
                        true, 1006UL, 6UL, 8UL, 8.001d)
                    == PresentationDirectionRevisionClassification.RevisionLag
                && UnitAttackShadowObserver.ClassifyDirectionRevisionForValidation(
                        9UL, 1006UL, 6UL,
                        true, 1007UL, 6UL, 9UL, 8.001d)
                    == PresentationDirectionRevisionClassification.ScopeMismatch
                && UnitAttackShadowObserver.ClassifyDirectionRevisionForValidation(
                        9UL, 1006UL, 6UL,
                        true, 1006UL, 6UL, 9UL, 7d)
                    == PresentationDirectionRevisionClassification.SameRevisionMatch,
                "C3 bounded direction evidence must distinguish same-revision Visual mismatch, replication lag, wrong scope, and a healthy exact revision without adding a Client Root writer.");
            var markerSnapshotScheduler = new UnitAttackResultPresentationShadowScheduler();
            AttackResultPresentationInput snapshotInput =
                CreateInput(1013UL, 13UL, 0, 80d, 10, revision: 5UL);
            markerSnapshotScheduler.ObserveResult(snapshotInput, 79.9d);
            var markerTimeSnapshot = new AttackPresentationReplicatedSnapshot(
                1013UL, 13UL, 4UL);
            PresentationShadowClassification snapshotClassification =
                markerSnapshotScheduler.ObserveLegacy(
                    new LegacyPresentationObservation(
                        LegacyPresentationObservationKind.Marker,
                        snapshotInput.AttackerUnitId,
                        1,
                        -1,
                        -1,
                        80d,
                        true,
                        east,
                        default,
                        AttackPresentationScope.FromResultKey(snapshotInput.Key),
                        markerTimeSnapshot));
            Require(snapshotClassification.Status == PresentationShadowResultStatus.Matched
                && snapshotClassification.ReplicatedSnapshot.Revision == 4UL
                && UnitAttackShadowObserver.ClassifyDirectionRevisionForValidation(
                        snapshotClassification.ActionRevision,
                        snapshotClassification.AttackScope.AttackerInstanceId.Value,
                        snapshotClassification.AttackScope.SequenceId.Value,
                        snapshotClassification.ReplicatedSnapshot.IsValid,
                        snapshotClassification.ReplicatedSnapshot.AttackerInstanceId,
                        snapshotClassification.ReplicatedSnapshot.SequenceId,
                        snapshotClassification.ReplicatedSnapshot.Revision,
                        snapshotClassification.AimDeltaDegrees)
                    == PresentationDirectionRevisionClassification.RevisionLag,
                "C3 direction classification must preserve the marker-time replicated snapshot instead of reading a later latest state.");
            Require(UnitAttackShadowObserver.ClassifyTimingEvidenceForValidation(
                        LegacyPresentationObservationKind.EmittedMarker)
                    == PresentationTimingEvidenceKind.NormalImpact
                && UnitAttackShadowObserver.ClassifyTimingEvidenceForValidation(
                        LegacyPresentationObservationKind.EmittedTimeout)
                    == PresentationTimingEvidenceKind.TimeoutRecovery
                && UnitAttackShadowObserver.ClassifyTimingEvidenceForValidation(
                        LegacyPresentationObservationKind.EmittedTargetDeath)
                    == PresentationTimingEvidenceKind.TargetDeathRecovery
                && UnitAttackShadowObserver.ClassifyTimingEvidenceForValidation(
                        LegacyPresentationObservationKind.EmittedAttackerStop)
                    == PresentationTimingEvidenceKind.AttackerStopRecovery,
                "C3 normal marker timing and timeout/death/stop recovery timing must use separate evidence categories without widening tolerance.");
            Require(UnitAttackShadowObserver.IsConfirmedPresentationDirectionFailureForValidation(
                        PresentationDirectionRevisionClassification.SameRevisionVisualMismatch)
                && !UnitAttackShadowObserver.IsConfirmedPresentationDirectionFailureForValidation(
                        PresentationDirectionRevisionClassification.RevisionLag)
                && !UnitAttackShadowObserver.IsConfirmedPresentationDirectionFailureForValidation(
                        PresentationDirectionRevisionClassification.ReplicatedStateAhead)
                && !UnitAttackShadowObserver.IsConfirmedPresentationDirectionFailureForValidation(
                        PresentationDirectionRevisionClassification.ScopeMismatch),
                "C3 direction verdict must fail only exact same-revision Visual mismatch while preserving asynchronous replication classifications as unresolved evidence.");
            bool causalErrorEmitted = false;
            Require(UnitAttackShadowObserver.ClassifyAggregatedFailureLevelForValidation(
                        true, ref causalErrorEmitted) == "Error"
                    && causalErrorEmitted
                    && UnitAttackShadowObserver.ClassifyAggregatedFailureLevelForValidation(
                        true, ref causalErrorEmitted) == "Warn"
                    && UnitAttackShadowObserver.ClassifyAggregatedFailureLevelForValidation(
                        false, ref causalErrorEmitted) == "Info",
                "C3 repeated causal failures must retain every payload while emitting only one Error stack per axis; later failures are Warn and healthy evidence remains Info.");
            Require(UnitAttackShadowObserver.ClassifyLocalVerdictForValidation(
                    1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1) == "FAIL",
                "C3 terminal verdict must fail closed for unmatched, expired, direction, or timing evidence.");
            Require(UnitAttackShadowObserver.ClassifyResultVerdictForValidation(
                        1, 0, 0, 0) == "EVIDENCE"
                    && UnitAttackShadowObserver.ClassifyPresentationVerdictForValidation(
                        1, 1, 0) == "FAIL"
                    && UnitAttackShadowObserver.ClassifyLocalVerdictForValidation(
                        1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1) == "FAIL",
                "C3 presentation failure must not contaminate a healthy C2 result verdict while overall remains failed.");
            Require(UnitAttackShadowObserver.ClassifyResultVerdictForValidation(
                        1, 1, 0, 0) == "FAIL"
                    && UnitAttackShadowObserver.ClassifyPresentationVerdictForValidation(
                        1, 0, 0) == "EVIDENCE",
                "C2 result failure must not contaminate a healthy C3 presentation verdict.");
            Require(UnitAttackShadowObserver.ClassifyPresentationVerdictForValidation(
                        1, 0, 0, unresolvedDirectionCount: 1) == "INCONCLUSIVE"
                    && UnitAttackShadowObserver.ClassifyLocalVerdictForValidation(
                        1, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                        presentationFailureCount: 0,
                        presentationUnresolvedCount: 1) == "INCONCLUSIVE",
                "C3 replication-lag/scope direction evidence must not become a confirmed FAIL or be disguised as healthy EVIDENCE.");
            Require(UnitAttackShadowObserver.ClassifyResultVerdictForValidation(
                        0, 0, 0, 0) == "INCONCLUSIVE"
                    && UnitAttackShadowObserver.ClassifyPresentationVerdictForValidation(
                        0, 0, 0) == "INCONCLUSIVE"
                    && UnitAttackShadowObserver.ClassifyResultVerdictForValidation(
                        1, 0, 0, 1) == "FAIL"
                    && UnitAttackShadowObserver.ClassifyPresentationVerdictForValidation(
                        1, 0, 1) == "FAIL",
                "C2/C3 terminals must distinguish missing evidence and both fail closed on terminal preflight failure.");
            Require(UnitAttackShadowObserver.ClassifySessionContractCompatibilityForValidation(
                        UnitAttackShadowObserver.ProductionSchema,
                        UnitAttackShadowObserver.ProductionSchema,
                        CombatPipelineMode.PresentationShadow,
                        CombatPipelineMode.PresentationShadow,
                        1,
                        1,
                        "session-a",
                        "session-a") == "COMPATIBLE"
                    && UnitAttackShadowObserver.ClassifySessionContractCompatibilityForValidation(
                        "c3-authoritative-result-presentation-shadow-v2",
                        UnitAttackShadowObserver.ProductionSchema,
                        CombatPipelineMode.PresentationShadow,
                        CombatPipelineMode.PresentationShadow,
                        1,
                        1,
                        "session-a",
                        "session-a") == "INCONCLUSIVE(schema-mismatch)"
                    && UnitAttackShadowObserver.ClassifySessionContractCompatibilityForValidation(
                        UnitAttackShadowObserver.ProductionSchema,
                        UnitAttackShadowObserver.ProductionSchema,
                        CombatPipelineMode.Legacy,
                        CombatPipelineMode.PresentationShadow,
                        0,
                        1,
                        "session-a",
                        "session-a") == "INCONCLUSIVE(contract-mismatch)"
                    && UnitAttackShadowObserver.ClassifySessionContractCompatibilityForValidation(
                        UnitAttackShadowObserver.ProductionSchema,
                        UnitAttackShadowObserver.ProductionSchema,
                        CombatPipelineMode.PresentationShadow,
                        CombatPipelineMode.PresentationShadow,
                        1,
                        1,
                        "session-a",
                        "session-b") == "INCONCLUSIVE(session-mismatch)",
                "C3 Host/Client comparison gate must reject schema, combat contract, and session mismatches before comparing counters.");
        }

        private static AttackDamageObservation AppliedDamage(
            int appliedAmount,
            int resultingHp,
            double impactX = 1d,
            double impactZ = 2d)
        {
            if (!WorldPointXZ.TryCreate(impactX, impactZ, out WorldPointXZ impactPosition))
                throw new InvalidOperationException("Invalid AppliedDamage fixture position.");
            return new AttackDamageObservation(
                AttackDamageApplyStatus.Applied,
                appliedAmount,
                resultingHp,
                (int)UnitType.SpearMan,
                (int)TeamId.Red,
                true,
                impactPosition);
        }

        private static void ValidateC1HostAtomicHandoffContract()
        {
            Type viewType = typeof(Hexiege.Presentation.UnitView);
            Require(viewType.GetMethod(
                    "TryApplyHostCombatPresentation",
                    BindingFlags.Instance | BindingFlags.Public) != null
                && viewType.GetMethod(
                    "TryApplyServerCombatTarget",
                    BindingFlags.Instance | BindingFlags.Public) != null
                && viewType.GetMethod(
                    "IsServerCombatActionReady",
                    BindingFlags.Instance | BindingFlags.Public) != null
                && viewType.GetMethod(
                    "ConfirmServerAttackEntryHandoff",
                    BindingFlags.Instance | BindingFlags.Public) != null
                && viewType.GetMethod(
                    "TryBeginServerAttackEntryBeforeRootStop",
                    BindingFlags.Instance | BindingFlags.NonPublic) != null,
                "C1 Host attack entry must expose readiness, local presentation, target handoff and pre-Root-stop completion acknowledgement seams.");
            Require(UnitMovementAuthorityObserver.ProductionSchema
                    == "b3-movement-authority-v15",
                "B3 observer schema must include pure-Client rendered attack-entry order failures.");
        }

        private static class BloomHealRetimingValidation
        {
            /// <summary>0~1초 Hermite를 보존하고 1~3초의 동작을 1~4초로 늘린다.</summary>
            public static AnimationCurve RetimeCurve(AnimationCurve curve)
            {
                Check(curve != null && curve.length > 0, "Missing curve.");
                var keys = new List<Keyframe>(curve.keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    Keyframe k = keys[i];
                    Check(k.weightedMode == WeightedMode.None && Finite(k.time) && Finite(k.value)
                        && Finite(k.inTangent) && Finite(k.outTangent), "Weighted/step/nonfinite curve needs explicit support.");
                    Check(k.time >= 0 && k.time <= 3.0001f && (i == 0 || k.time > keys[i - 1].time),
                        "Curve times outside source or duplicate keys.");
                    Check(AnimationUtility.GetKeyLeftTangentMode(curve, i) == AnimationUtility.TangentMode.Free
                        && AnimationUtility.GetKeyRightTangentMode(curve, i) == AnimationUtility.TangentMode.Free,
                        "Automatic tangent recalculation is unsupported; preserve source explicitly.");
                }
                bool boundary = keys.Exists(k => k.time == 1f);
                if (!boundary)
                {
                    // 경계를 덮지 않는 curve는 외삽 보존을 별도 입증해야 하므로 fail closed.
                    Check(keys[0].time <= 1 && keys[keys.Count - 1].time >= 1,
                        "Source must cover the split boundary.");
                    int right = keys.FindIndex(k => k.time > 1);
                    Keyframe a = keys[right - 1], b = keys[right];
                    float h = b.time - a.time, u = (1 - a.time) / h;
                    float derivative = ((6 * u * u - 6 * u) * a.value
                        + (3 * u * u - 4 * u + 1) * h * a.outTangent
                        + (-6 * u * u + 6 * u) * b.value
                        + (3 * u * u - 2 * u) * h * b.inTangent) / h;
                    keys.Insert(right, new Keyframe(1, curve.Evaluate(1), derivative, derivative));
                }
                for (int i = 0; i < keys.Count; i++)
                {
                    Keyframe k = keys[i];
                    if (k.time > 1) { k.time = 1 + (k.time - 1) * 1.5f; k.inTangent /= 1.5f; }
                    if (k.time >= 1) k.outTangent /= 1.5f;
                    keys[i] = k;
                }
                var result = new AnimationCurve(keys.ToArray()) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
                for (int i = 0; i < result.length; i++)
                {
                    AnimationUtility.SetKeyBroken(result, i, true);
                    AnimationUtility.SetKeyLeftTangentMode(result, i, AnimationUtility.TangentMode.Free);
                    AnimationUtility.SetKeyRightTangentMode(result, i, AnimationUtility.TangentMode.Free);
                }
                ValidateSamples(curve, result);
                return result;
            }

            private static void ValidateSamples(AnimationCurve source, AnimationCurve result)
            {
                Check(result != null, "Curve application failed.");
                for (int i = 0; i <= 360; i++)
                {
                    float t = i / 120f;
                    float mapped = t <= 1 ? t : 1 + (t - 1) * 1.5f;
                    float expected = source.Evaluate(t);
                    Check(Mathf.Abs(expected - result.Evaluate(mapped)) <= 0.00002f * Mathf.Max(1, Mathf.Abs(expected)),
                        $"Retime changed a source sample at {t:R}.");
                }
                foreach (Keyframe k in source.keys)
                {
                    float mapped = k.time <= 1 ? k.time : 1 + (k.time - 1) * 1.5f;
                    Check(Mathf.Abs(result.Evaluate(mapped) - k.value) < 0.00002f * Mathf.Max(1, Mathf.Abs(k.value)),
                        "Retime changed a source key.");
                }
            }

            private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
            private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException($"[UAS-DIAG] self-validation FAIL: {message}");
        }
    }
}
