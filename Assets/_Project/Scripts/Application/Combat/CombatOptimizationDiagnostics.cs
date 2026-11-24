#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Domain;
using UniRx;
using UnityEngine;

namespace Hexiege.Application
{
    /// <summary>개발 Android에서만 두 최적화의 호출량·순수 결과를 확인한다. 시간·바이트 측정은 아니다.</summary>
    internal static class CombatOptimizationDiagnostics
    {
        internal delegate bool BundleReplay(int attacker, AttackImpactResult primary,
            AttackDeliveryKind delivery, AttackDamagePresentationBundleObservation observation,
            out AttackResultPresentationInput[] results);

        private const int SampleLimit = 16;
        private static bool _initialized, _ready, _ended, _busy;
        private static string _session;
        private static int _round = 1;
        private static long _bundleCalls, _bundleSuccess, _bundleFailure, _bundleThrows, _copiedElements;
        private static long _movementCalls, _oldTileVisits, _newNeighborLookups;
        private static int _bundleSamples, _movementSamples, _bundleMismatches, _movementMismatches, _faults;

        private static void EnsureReady()
        {
            if (!_initialized)
            {
                _session = Guid.NewGuid().ToString("N");
                GameEvents.OnGameStarted.Subscribe(_ => NextRound());
                GameEvents.OnGameEnd.Subscribe(_ => EndRound("GameEnd", true));
                _initialized = true;
            }
            if (_ready || _ended) return;
            _ready = true;
            string role = !NetworkContext.IsNetworkActive ? "Single"
                : NetworkContext.IsNetworkServer ? "ServerOrHost" : "Client";
            GameLog.Dev.Info("Performance", nameof(CombatOptimizationDiagnostics), "[COMBAT-OPT] READY",
                $"Protocol=1; Session={_session}; Round={_round}; Role={role}; Build=ArchiveHeader; Limit={SampleLimit}; Metrics=StructuralCountsNotTimeOrBytes");
        }

        private static void NextRound()
        {
            try
            {
                if (_ready && !_ended) EndRound("RestartWithoutEnd", false);
                _round++; _ready = false; _ended = false;
                _bundleCalls = _bundleSuccess = _bundleFailure = _bundleThrows = _copiedElements = 0;
                _movementCalls = _oldTileVisits = _newNeighborLookups = 0;
                _bundleSamples = _movementSamples = _bundleMismatches = _movementMismatches = _faults = 0;
            }
            catch { _faults++; }
        }

        private static void EndRound(string reason, bool complete)
        {
            try
            {
                if (_ended) return;
                _ended = true;
                if (!_ready) return;
                // 성공 호출에 한정한 구조적 제거량이다. 순수 비교 자체의 진단 할당이나 net GC 절감량은 포함하지 않는다.
                GameLog.Dev.Info("Performance", nameof(CombatOptimizationDiagnostics), "[COMBAT-OPT] SUMMARY",
                    $"Protocol=1; Session={_session}; Round={_round}; Complete={complete}; Reason={reason}; " +
                    $"BundleCalls={_bundleCalls}; BundleSuccess={_bundleSuccess}; BundleFailure={_bundleFailure}; BundleThrows={_bundleThrows}; " +
                    $"AvoidedListObjects={_bundleSuccess}; AvoidedBackingArrays={_bundleSuccess}; AvoidedCopyOperations={_bundleSuccess}; AvoidedCopyElements={_copiedElements}; " +
                    $"BundleSamples={_bundleSamples}; DiagnosticBundleBaselineCalls={_bundleSamples}; DiagnosticSampleCap={SampleLimit}; BundleMismatches={_bundleMismatches}; BundleCoverage={(_bundleSamples > 0 ? "Observed" : "NotExercised")}; " +
                    $"MovementFallbackCalls={_movementCalls}; OldTileVisits={_oldTileVisits}; NewNeighborLookups={_newNeighborLookups}; " +
                    $"MovementSamples={_movementSamples}; DiagnosticMovementBaselineCalls={_movementSamples}; MovementMismatches={_movementMismatches}; MovementCoverage={(_movementSamples > 0 ? "Observed" : "NotExercised")}; " +
                    $"Faults={_faults}; Semantics=StructuralNotNetAllocOrMs; Selection=OrderedCandidatesOnlyNoAStarReplay");
            }
            catch { _faults++; }
        }

        internal static void ObserveBundle(int attacker, AttackImpactResult primary, AttackDeliveryKind delivery,
            AttackDamagePresentationBundleObservation observation, AttackResultPresentationInput[] actual,
            bool threw, BundleReplay replay)
        {
            if (_busy || _ended) return;
            _busy = true;
            try
            {
                EnsureReady();
                _bundleCalls++;
                if (threw) { _bundleThrows++; return; }
                bool success = actual != null;
                if (success) { _bundleSuccess++; _copiedElements += actual.Length; }
                else _bundleFailure++;
                if (_bundleSamples >= SampleLimit) return;
                _bundleSamples++;
                bool baseline = replay(attacker, primary, delivery, observation, out var expected);
                bool equal = baseline == success && (expected == null) == (actual == null);
                if (equal && success)
                {
                    equal = !ReferenceEquals(expected, actual) && expected.Length == actual.Length;
                    for (int i = 0; equal && i < expected.Length; i++) equal = expected[i].Equals(actual[i]);
                }
                if (!equal) _bundleMismatches++;
            }
            catch { _faults++; }
            finally { _busy = false; }
        }

        internal static void ObserveMovement(HexGrid grid, IHexCoordinateMapper mapper, HexCoord nearest,
            HexCoord goal, Vector3 world, List<(HexCoord coord, float distanceSquared)> actual)
        {
            if (_busy || _ended) return;
            _busy = true;
            try
            {
                EnsureReady();
                _movementCalls++; _oldTileVisits += grid.Tiles.Count; _newNeighborLookups += HexDirectionExtensions.Count;
                if (_movementSamples >= SampleLimit) return;
                _movementSamples++;
                int distance = HexCoord.Distance(nearest, goal);
                var expected = new List<(HexCoord coord, float distanceSquared)>();
                foreach (var pair in grid.Tiles)
                {
                    if (pair.Value == null || !pair.Value.IsWalkable || HexCoord.Distance(nearest, pair.Key) != 1
                        || HexCoord.Distance(pair.Key, goal) >= distance) continue;
                    Vector3 center = mapper.HexToWorld(pair.Key);
                    float dx = center.x - world.x, dz = center.z - world.z;
                    expected.Add((pair.Key, dx * dx + dz * dz));
                }
                expected.Sort((left, right) =>
                {
                    int order = left.distanceSquared.CompareTo(right.distanceSquared);
                    if (order != 0) return order;
                    order = left.coord.Q.CompareTo(right.coord.Q);
                    return order != 0 ? order : left.coord.R.CompareTo(right.coord.R);
                });
                bool equal = expected.Count == actual.Count;
                for (int i = 0; equal && i < expected.Count; i++)
                    equal = expected[i].coord == actual[i].coord && expected[i].distanceSquared.Equals(actual[i].distanceSquared);
                if (!equal) _movementMismatches++;
            }
            catch { _faults++; }
            finally { _busy = false; }
        }
    }
}
#endif
