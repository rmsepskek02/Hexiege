using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Hexiege.Application;
using Hexiege.Bootstrap;
using Hexiege.Domain;
using Hexiege.Infrastructure;
using Hexiege.Presentation;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UniRx;

namespace Hexiege.Editor.Combat
{
    public static partial class RunUnitActionSelfValidation
    {
        private static readonly BindingFlags RefactoringRuntimeFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>실제 초기화·전역 이벤트·정리 메서드를 실행하고 관측값을 대조한다.</summary>
        private static void RunRefactoringRuntimeOfflineCases()
        {
            RunRefactoringCase("EDITOR-UI-LIFETIME", ValidateRefactoringPanelLifetime);
            RunRefactoringCase("EDITOR-TICKER-LIFETIME", ValidateRefactoringTickerLifetime);
            RunRefactoringCase("EDITOR-BOOTSTRAP-LIFETIME", ValidateRefactoringBootstrapLifetime);
        }

        private static FieldInfo RefactoringRuntimeField(object target, string name)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, RefactoringRuntimeFlags | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(target.GetType().Name, name);
        }

        private static T RefactoringRead<T>(object target, string name)
            => (T)RefactoringRuntimeField(target, name).GetValue(target);

        private static void RefactoringWrite(object target, string name, object value)
            => RefactoringRuntimeField(target, name).SetValue(target, value);

        private static object RefactoringInvoke(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, RefactoringRuntimeFlags);
            Require(method != null, "The actual production method must exist: " + name);
            return method.Invoke(target, arguments);
        }

        private static void ValidateRefactoringPanelLifetime()
        {
            var go = new GameObject("RefactoringPanelFixture") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var panel = go.AddComponent<ProductionPanelUI>();
                panel.Initialize(null, null, null, null);
                panel.Initialize(null, null, null, null);
                Require(RefactoringRead<int>(panel, "_refactoringInitializeCalls") == 2
                    && RefactoringRead<int>(panel, "_refactoringBindingCreations") == 1,
                    "Two actual initializations must bind panel inputs once.");
                int queueBefore = RefactoringRead<int>(panel, "_refactoringQueueRefreshes");
                int resourceBefore = RefactoringRead<int>(panel, "_refactoringResourceRefreshes");
                GameEvents.OnProductionQueueChanged.OnNext(new ProductionQueueChangedEvent(-92001));
                GameEvents.OnResourceChanged.OnNext(new ResourceChangedEvent(TeamId.Blue, 1));
                Require(RefactoringRead<int>(panel, "_refactoringQueueRefreshes") - queueBefore == 1
                    && RefactoringRead<int>(panel, "_refactoringResourceRefreshes") - resourceBefore == 1,
                    "One real event must invoke each refresh once after repeated initialization.");

                // 열린 패널에서 누르고 있던 상태를 꾸민 뒤 실제 비활성화 수명을 통과시킨다.
                // 입력 판정 조건을 복제하지 않고 실제 OnDisable/PointerUp의 결과를 관찰한다.
                RefactoringWrite(panel, "_isPointerDown", true);
                RefactoringWrite(panel, "_longPressTriggered", true);
                int taps = RefactoringRead<int>(panel, "_refactoringTapCalls");
                go.SetActive(false);
                Require(!RefactoringRead<bool>(panel, "_isPointerDown")
                    && !RefactoringRead<bool>(panel, "_longPressTriggered"),
                    "Disabling the actual panel must clear held and long-press state.");
                RefactoringInvoke(panel, "OnUnitPointerUp");
                Require(RefactoringRead<int>(panel, "_refactoringTapCalls") == taps,
                    "Pointer release after disable must not enter the tap action.");
                queueBefore = RefactoringRead<int>(panel, "_refactoringQueueRefreshes");
                resourceBefore = RefactoringRead<int>(panel, "_refactoringResourceRefreshes");
                UnityEngine.Object.DestroyImmediate(go);
                GameEvents.OnProductionQueueChanged.OnNext(new ProductionQueueChangedEvent(-92001));
                GameEvents.OnResourceChanged.OnNext(new ResourceChangedEvent(TeamId.Blue, 2));
                Require(RefactoringRead<int>(panel, "_refactoringQueueRefreshes") == queueBefore
                    && RefactoringRead<int>(panel, "_refactoringResourceRefreshes") == resourceBefore,
                    "Destroyed panel callbacks must not receive later global events.");
            }
            finally { if (go != null) UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void ValidateRefactoringTickerLifetime()
        {
            var go = new GameObject("RefactoringTickerFixture") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var ticker = go.AddComponent<ProductionTicker>();
                ticker.Initialize(null, null, null, null, null, null, null, null, null);
                ticker.Initialize(null, null, null, null, null, null, null, null, null);
                var subscriptions = RefactoringRead<CompositeDisposable>(ticker, "_eventSubscriptions");
                Require(RefactoringRead<int>(ticker, "_refactoringTickerBinds") == 2
                    && subscriptions.Count == 8 && !subscriptions.IsDisposed,
                    "Repeated actual ticker initialization must replace, rather than accumulate, eight subscriptions.");
                UnityEngine.Object.DestroyImmediate(go);
                Require(subscriptions.IsDisposed, "Actual ticker destruction must dispose its subscriptions.");
            }
            finally { if (go != null) UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void ValidateRefactoringBootstrapLifetime()
        {
            var go = new GameObject("RefactoringBootstrapFixture") { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false); // Awake/Start의 씬 배선은 실행하지 않는다.
            GameBootstrapper bootstrap = null;
            try
            {
                bootstrap = go.AddComponent<GameBootstrapper>();
                RefactoringInvoke(bootstrap, "SetupEagerRepathOnBuildingChanges");
                RefactoringInvoke(bootstrap, "SetupEagerRepathOnBuildingChanges");
                var subscriptions = RefactoringRead<CompositeDisposable>(bootstrap, "_eagerRepathSubscriptions");
                Require(subscriptions.Count == 2, "Rebinding must retain exactly two eager-repath subscriptions.");
                int before = RefactoringRead<int>(bootstrap, "_refactoringRepathCalls");
                GameEvents.OnBuildingPlaced.OnNext(default);
                GameEvents.OnBuildingRemoved.OnNext(default);
                Require(RefactoringRead<int>(bootstrap, "_refactoringRepathCalls") - before == 2,
                    "Two actual building events must reach the rebound bootstrap once each.");
                // 한 번도 활성화하지 않은 오브젝트의 OnDestroy는 Unity가 보장하지 않는다.
                // 정확한 production 정리 본문을 직접 호출하고 다음 이벤트의 비전달까지 확인한다.
                RefactoringInvoke(bootstrap, "OnDestroy");
                Require(subscriptions.IsDisposed
                    && RefactoringRead<CompositeDisposable>(bootstrap, "_eagerRepathSubscriptions") == null,
                    "Actual cleanup body must dispose and clear the subscription owner.");
                before = RefactoringRead<int>(bootstrap, "_refactoringRepathCalls");
                GameEvents.OnBuildingPlaced.OnNext(default);
                GameEvents.OnBuildingRemoved.OnNext(default);
                Require(RefactoringRead<int>(bootstrap, "_refactoringRepathCalls") == before,
                    "After cleanup, global events must not reenter the previous bootstrap.");
            }
            finally
            {
                if (bootstrap != null) RefactoringRead<CompositeDisposable>(bootstrap, "_eagerRepathSubscriptions")?.Dispose();
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private sealed class RefactoringRequestStep
        {
            public string Id;
            public Action Send;
            public object CounterOwner;
            public string CounterField;
        }

        private static List<RefactoringRequestStep> _refactoringRequestSteps;
        private static int _refactoringRequestIndex, _refactoringRequestCounter;
        private static double _refactoringRequestDeadline;
        private static string _refactoringRequestSnapshot;
        private static bool _refactoringRequestActive;
        private static float _refactoringOriginalTimeScale;
        private static IGameServices _refactoringRequestServices;
        private static ProductionState[] _refactoringRequestStates;

        /// <summary>테스트 경기의 실제 서버 요청을 보내 거부와 상태 보존을 확인한다.</summary>
        [MenuItem("Hexiege/Combat/Verification/Run Server Request Checks")]
        public static void RunRefactoringServerRequestChecks()
        {
            if (_refactoringVerificationRunning) return;
            BeginRefactoringVerification("ServerRequestsNegativeOnly");
            try
            {
                var network = NetworkManager.Singleton;
                var services = GameServicesLocator.Current;
                var production = UnityEngine.Object.FindFirstObjectByType<NetworkProductionController>();
                var upgrade = UnityEngine.Object.FindFirstObjectByType<NetworkUpgradeController>();
                if (!UnityEngine.Application.isPlaying || network == null || !network.IsHost || !network.IsListening
                    || services?.GetBuildingPlacement() == null || services.GetUnitProduction() == null
                    || services.GetResource() == null || services.GetUpgradeUseCase() == null
                    || production == null || upgrade == null || !production.IsSpawned || !upgrade.IsSpawned)
                {
                    RefactoringReceipt("RPC-ENVIRONMENT", "NOT_RUN", "RequiresListeningEditorHostAndReadyServices");
                    EndRefactoringVerification("ServerRequestsNegativeOnly");
                    return;
                }
                var combat = UnityEngine.Object.FindFirstObjectByType<NetworkCombatController>();
                var combatUseCase = services.GetCombatUseCase();
                // 서버 시계로 진행되는 투사체는 시간 배율 0으로 멈추지 않는다.
                // 실제 예약·비행 수와 파도 발사 예약까지 비어 있을 때만 검사를 시작한다.
                if (combat == null || combatUseCase == null
                    || RefactoringRead<IDictionary>(combat, "_unitCombatTargets").Count != 0
                    || (int)RefactoringInvoke(combatUseCase, "CountActiveProjectiles") != 0
                    || RefactoringRead<int>(combatUseCase, "_torrentLaunchCount") != 0)
                {
                    RefactoringReceipt("RPC-ENVIRONMENT", "NOT_RUN", "RequiresNoCombatOrProjectileOrWaveLaunchBeforeTimeFreeze");
                    EndRefactoringVerification("ServerRequestsNegativeOnly");
                    return;
                }
                var buildings = services.GetBuildingPlacement().Buildings.Values.Where(b => b.IsAlive).ToArray();
                var own = buildings.FirstOrDefault(b => b.Team == TeamId.Blue
                    && services.GetUnitProduction().GetState(b.Id)?.CurrentProducing != null);
                var foreign = buildings.FirstOrDefault(b => b.Team == TeamId.Red
                    && services.GetUnitProduction().GetState(b.Id)?.CurrentProducing != null);
                var ownLab = buildings.FirstOrDefault(b => b.Team == TeamId.Blue && b.Type == BuildingType.Research
                    && services.GetUpgradeUseCase().TryGetActiveResearchByBuilding(b.Id, out _, out _));
                var foreignLab = buildings.FirstOrDefault(b => b.Team == TeamId.Red && b.Type == BuildingType.Research
                    && services.GetUpgradeUseCase().TryGetActiveResearchByBuilding(b.Id, out _, out _));
                if (own == null || foreign == null || ownLab == null || foreignLab == null)
                {
                    RefactoringReceipt("RPC-ENVIRONMENT", "NOT_RUN", "BothTeamsNeedActiveProductionAndActiveResearch");
                    EndRefactoringVerification("ServerRequestsNegativeOnly");
                    return;
                }
                _refactoringRequestServices = services;
                _refactoringRequestStates = buildings.Select(b => services.GetUnitProduction().GetState(b.Id))
                    .Where(s => s != null).ToArray();
                _refactoringRequestSteps = new List<RefactoringRequestStep>();
                void Add(string id, Action send, object owner, string counter)
                    => _refactoringRequestSteps.Add(new RefactoringRequestStep
                    { Id = id, Send = send, CounterOwner = owner, CounterField = counter });
                const string productionCounter = "_refactoringRequestRejections";
                const string researchCounter = "_refactoringResearchRejections";
                Add("RPC-ENQUEUE-OWNER", () => production.RequestEnqueueServerRpc(foreign.Id, (int)UnitType.Pistoleer, 0), production, productionCounter);
                Add("RPC-CANCEL-OWNER", () => production.CancelSlotServerRpc(foreign.Id, 0, 0), production, productionCounter);
                Add("RPC-RALLY-OWNER", () => production.SetRallyPointServerRpc(foreign.Id, 0, 0, 0), production, productionCounter);
                Add("RPC-AUTO-OWNER", () => production.ToggleAutoServerRpc(foreign.Id, (int)UnitType.Pistoleer, 0), production, productionCounter);
                Add("RPC-CANCEL-NEGATIVE", () => production.CancelSlotServerRpc(own.Id, -1, 0), production, productionCounter);
                Add("RPC-CANCEL-MINVALUE", () => production.CancelSlotServerRpc(own.Id, int.MinValue, 0), production, productionCounter);
                foreach (var item in new[] { ("OWNER", foreignLab.Id), ("TYPE", own.Id), ("MISSING", int.MinValue) })
                {
                    string reason = item.Item1;
                    int id = item.Item2;
                    Add("RPC-RESEARCH-START-" + reason,
                        () => upgrade.RequestResearchServerRpc(0, 0, id, 0), upgrade, researchCounter);
                    Add("RPC-RESEARCH-CANCEL-" + reason,
                        () => upgrade.RequestCancelResearchServerRpc(id, 0), upgrade, researchCounter);
                }
                _refactoringOriginalTimeScale = Time.timeScale;
                _refactoringRequestActive = true;
                Time.timeScale = 0f; // 검사 중 생산·수입·연구 타이머만 멈추고 모든 종료 경로에서 원래 배율로 되돌린다.
                EditorApplication.update += UpdateRefactoringRequestChecks;
                EditorApplication.playModeStateChanged += RefactoringRequestPlayStateChanged;
                AssemblyReloadEvents.beforeAssemblyReload += CancelRefactoringRequestChecks;
                _refactoringRequestIndex = 0;
                StartRefactoringRequestStep();
            }
            catch (Exception exception)
            {
                RunRefactoringCase("RPC-HARNESS", () => throw exception);
                FinishRefactoringRequestChecks();
            }
        }

        private static void StartRefactoringRequestStep()
        {
            var step = _refactoringRequestSteps[_refactoringRequestIndex];
            _refactoringRequestSnapshot = RefactoringRequestStateSnapshot();
            _refactoringRequestCounter = RefactoringRead<int>(step.CounterOwner, step.CounterField);
            _refactoringRequestDeadline = EditorApplication.timeSinceStartup + 3d;
            RefactoringReceipt(step.Id, "STARTED", "ActualServerRpc;StateSnapshotCaptured");
            step.Send();
        }

        private static void UpdateRefactoringRequestChecks()
        {
            if (!_refactoringRequestActive) return;
            try
            {
                if (!UnityEngine.Application.isPlaying || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost
                    || !NetworkManager.Singleton.IsListening || GameServicesLocator.Current != _refactoringRequestServices)
                { CancelRefactoringRequestChecks(); return; }
                var step = _refactoringRequestSteps[_refactoringRequestIndex];
                int counter = RefactoringRead<int>(step.CounterOwner, step.CounterField);
                if (counter == _refactoringRequestCounter && EditorApplication.timeSinceStartup < _refactoringRequestDeadline) return;
                int failuresBefore = _refactoringFailures;
                RunRefactoringCase(step.Id, () =>
                {
                    Require(counter == _refactoringRequestCounter + 1,
                        "Actual server rejection receipt was absent, duplicated, or timed out.");
                    Require(RefactoringRequestStateSnapshot() == _refactoringRequestSnapshot,
                        "An invalid request changed queue, rally, auto mode, gold, or research state.");
                });
                if (_refactoringFailures != failuresBefore || ++_refactoringRequestIndex == _refactoringRequestSteps.Count)
                { FinishRefactoringRequestChecks(); return; }
                StartRefactoringRequestStep();
            }
            catch (Exception exception)
            {
                RunRefactoringCase("RPC-HARNESS", () => throw exception);
                FinishRefactoringRequestChecks();
            }
        }

        private static string RefactoringRequestStateSnapshot()
        {
            var parts = new List<string>();
            foreach (var state in _refactoringRequestStates.OrderBy(s => s.BarracksId))
            {
                parts.Add($"{state.BarracksId}:{state.CurrentProducing}:{state.CurrentIsAuto}:{state.AutoCycleIndex}:"
                    + state.ElapsedTime.ToString("R", CultureInfo.InvariantCulture) + ":"
                    + state.RequiredTime.ToString("R", CultureInfo.InvariantCulture) + ":" + state.RallyPoint);
                parts.Add(string.Join(";", state.AutoTypes.Select(t => (int)t)));
                parts.Add(string.Join(";", state.PendingQueue.Select(q => $"{q.Type}/{q.IsAuto}/{q.IsCharged}")));
            }
            var resource = _refactoringRequestServices.GetResource();
            parts.Add($"Gold:{resource.GetGold(TeamId.Blue)}:{resource.GetGold(TeamId.Red)}");
            var active = RefactoringRead<IDictionary>(_refactoringRequestServices.GetUpgradeUseCase(), "_active");
            foreach (DictionaryEntry entry in active)
            {
                object research = entry.Value;
                parts.Add($"Research:{entry.Key}:{RefactoringRead<int>(research, "BuildingId")}:"
                    + RefactoringRead<float>(research, "Total").ToString("R", CultureInfo.InvariantCulture) + ":"
                    + RefactoringRead<float>(research, "Remaining").ToString("R", CultureInfo.InvariantCulture) + ":"
                    + RefactoringRead<int>(research, "Cost"));
            }
            return string.Join("|", parts);
        }

        private static void RefactoringRequestPlayStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) CancelRefactoringRequestChecks();
        }

        private static void CancelRefactoringRequestChecks()
        {
            if (!_refactoringRequestActive) return;
            RunRefactoringCase("RPC-CANCELLED", () => throw new InvalidOperationException("Play mode, session, or script generation changed during verification."));
            FinishRefactoringRequestChecks();
        }

        private static void FinishRefactoringRequestChecks()
        {
            EditorApplication.update -= UpdateRefactoringRequestChecks;
            EditorApplication.playModeStateChanged -= RefactoringRequestPlayStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= CancelRefactoringRequestChecks;
            if (_refactoringRequestActive) Time.timeScale = _refactoringOriginalTimeScale;
            _refactoringRequestActive = false;
            _refactoringRequestSteps = null;
            _refactoringRequestServices = null;
            _refactoringRequestStates = null;
            EndRefactoringVerification("ServerRequestsNegativeOnly");
        }

        /// <summary>종료된 실제 서버에서 늦은 진입을 한 번 보내 상태가 보존되는지 확인한다.</summary>
        [MenuItem("Hexiege/Combat/Verification/Run Ended Combat Check")]
        public static void RunRefactoringEndedCombatCheck()
        {
            if (_refactoringVerificationRunning) return;
            BeginRefactoringVerification("EndedCombat");
            try
            {
                var controller = UnityEngine.Object.FindFirstObjectByType<NetworkCombatController>();
                var services = GameServicesLocator.Current;
                if (!UnityEngine.Application.isPlaying || controller == null || !controller.IsSpawned || !controller.IsServer
                    || !RefactoringRead<bool>(controller, "_combatStopped") || services?.GetUnitSpawn() == null)
                { RefactoringReceipt("END-ENVIRONMENT", "NOT_RUN", "RequiresStoppedSpawnedEditorHost"); return; }
                UnitData actor = services.GetUnitSpawn().Units.Values.FirstOrDefault(u => u.IsAlive
                    && services.GetUnitFactory()?.GetUnitObject(u.Id)?.GetComponent<NetworkUnit>() != null);
                BuildingData target = actor == null ? null : services.GetBuildingPlacement()?.Buildings.Values
                    .FirstOrDefault(b => b.IsAlive && b.Team != actor.Team);
                if (actor == null || target == null)
                { RefactoringReceipt("END-ENVIRONMENT", "NOT_RUN", "RequiresLivingActorAndEnemyBuildingAfterForfeit"); return; }
                var native = services.GetUnitFactory().GetUnitObject(actor.Id).GetComponent<NetworkUnit>();
                RunRefactoringCase("END-LATE-ENTRY", () =>
                {
                    Require(RefactoringRead<bool>(native, "_attackPhaseEnded"), "Native attack lifetime must already be terminally closed.");
                    string[] fields = { "_unitCombatTargets", "_attackPresentationRevisions", "_attackPresentationEpochServerTimes", "_committedAttackPhases" };
                    var snapshots = fields.ToDictionary(f => f, f =>
                    {
                        var copy = new List<DictionaryEntry>();
                        var dictionary = RefactoringRead<IDictionary>(controller, f);
                        IDictionaryEnumerator entries = dictionary.GetEnumerator();
                        while (entries.MoveNext()) copy.Add(entries.Entry);
                        return copy.ToArray();
                    });
                    int before = RefactoringRead<int>(controller, "_refactoringStoppedEntries");
                    RefactoringInvoke(controller, "OnUnitEnteredCombatHandler", new UnitEnteredCombatEvent(actor.Id, target.Id, false));
                    Require(RefactoringRead<int>(controller, "_refactoringStoppedEntries") == before + 1,
                        "The real spawned/server stopped branch must be exercised.");
                    foreach (string field in fields)
                    {
                        var current = RefactoringRead<IDictionary>(controller, field);
                        Require(current.Count == snapshots[field].Length && snapshots[field].All(e =>
                            current.Contains(e.Key) && Equals(current[e.Key], e.Value)),
                            "Ended entry changed target, epoch, revision, or committed phase state.");
                    }
                    Require(RefactoringRead<bool>(native, "_attackPhaseEnded"), "Native attack lifetime must remain closed.");
                });
            }
            catch (Exception exception) { RunRefactoringCase("END-HARNESS", () => throw exception); }
            finally { EndRefactoringVerification("EndedCombat"); }
        }

        /// <summary>도달 확인 대기 중 새 신호를 실제 감시 루프에 전달한다.</summary>
        [MenuItem("Hexiege/Combat/Verification/Run Result Heartbeat Check")]
        public static void RunRefactoringResultHeartbeatCheck()
        {
            if (_refactoringVerificationRunning) return;
            BeginRefactoringVerification("ResultHeartbeat");
            try
            {
                var controller = UnityEngine.Object.FindFirstObjectByType<NetworkGameEndController>();
                if (!UnityEngine.Application.isPlaying || controller == null || !controller.IsSpawned
                    || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening
                    || RefactoringRead<Coroutine>(controller, "_resultScreenWatchCoroutine") == null
                    || RefactoringRead<bool>(controller, "_resultScreenLeaveJudged")
                    || RefactoringRead<bool>(controller, "_opponentLeftJudged"))
                { RefactoringReceipt("HB-ENVIRONMENT", "NOT_RUN", "RequiresConnectedResultScreenWithActiveWatch"); return; }
                RunRefactoringCase("HB-FRESH-DURING-PROBE", () => ValidateRefactoringResultHeartbeat(controller));
            }
            catch (Exception exception) { RunRefactoringCase("HB-HARNESS", () => throw exception); }
            finally { EndRefactoringVerification("ResultHeartbeat"); }
        }

        private static void ValidateRefactoringResultHeartbeat(NetworkGameEndController controller)
        {
            string[] fields = { "_lastOpponentSignalRealtime", "_nextReachabilityProbeRealtime", "_lastHeartbeatSentRealtime", "_reachabilityProbe", "_refactoringReachabilityProbe" };
            var saved = fields.ToDictionary(f => f, f => RefactoringRuntimeField(controller, f).GetValue(controller));
            var pending = new TaskCompletionSource<InternetReachabilityResult>();
            int probes = 0, leftEvents = 0;
            int freshBefore = RefactoringRead<int>(controller, "_refactoringFreshSignals");
            IEnumerator loop = null;
            var subscription = GameEvents.OnNetworkOpponentLeft.Subscribe(_ => leftEvents++);
            try
            {
                RefactoringWrite(controller, "_refactoringReachabilityProbe", (Func<Task<InternetReachabilityResult>>)(() => { probes++; return pending.Task; }));
                RefactoringWrite(controller, "_lastOpponentSignalRealtime", Time.realtimeSinceStartup - 31f);
                RefactoringWrite(controller, "_nextReachabilityProbeRealtime", 0f);
                RefactoringWrite(controller, "_lastHeartbeatSentRealtime", Time.realtimeSinceStartup);
                loop = (IEnumerator)RefactoringInvoke(controller, "ResultScreenLeaveWatchLoop");
                Require(loop.MoveNext() && loop.Current is WaitForSecondsRealtime, "Actual watcher must begin with its unchanged realtime interval.");
                Require(loop.MoveNext() && loop.Current == null && probes == 1 && !pending.Task.IsCompleted,
                    "Actual watcher must suspend on the pending reachability task.");
                RefactoringInvoke(controller, "MarkOpponentSignalReceived");
                pending.SetResult(InternetReachabilityResult.Reachable);
                Require(loop.MoveNext() && loop.Current is WaitForSecondsRealtime,
                    "Actual watcher must resume waiting after receiving a new opponent signal.");
                Require(probes == 1 && leftEvents == 0
                    && !RefactoringRead<bool>(controller, "_resultScreenLeaveJudged")
                    && !RefactoringRead<bool>(controller, "_opponentLeftJudged")
                    && RefactoringRead<int>(controller, "_refactoringFreshSignals") == freshBefore + 1
                    && controller.IsSpawned && NetworkManager.Singleton.IsListening,
                    "Fresh signal must preserve the connection and exercise the actual post-probe recheck.");
            }
            finally
            {
                (loop as IDisposable)?.Dispose();
                subscription.Dispose();
                foreach (var item in saved) RefactoringWrite(controller, item.Key, item.Value);
            }
        }
    }
}
