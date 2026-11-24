// ============================================================================
// UnitWorldPositionProvider.cs
// IEntityPositionProvider 구현체.
// UnitFactory/BuildingFactory의 GameObject Dictionary를 통해
// 유닛/건물의 실시간 Transform.position을 반환.
//
// Lerp 중에도 정확한 시각 위치를 반환하므로
// HexCoord 기반 판정의 "타일 완료 후에만 갱신" 문제를 해결.
//
// Infrastructure 레이어 — Unity 의존 (GameObject, Transform).
// ============================================================================

using UnityEngine;
using System;
using System.Collections.Generic;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// UnitFactory/BuildingFactory를 통해 유닛/건물의 월드 좌표를 제공.
    /// </summary>
    public class UnitWorldPositionProvider : IEntityPositionProvider, ICombatUnitCandidateSource
    {
        private readonly UnitFactory _unitFactory;
        private readonly BuildingFactory _buildingFactory;

        public UnitWorldPositionProvider(UnitFactory unitFactory, BuildingFactory buildingFactory)
        {
            _unitFactory = unitFactory;
            _buildingFactory = buildingFactory;
            _entryOrder = CompareEntryOrder;
            _unitFactory?.BindCombatCandidateSource(this);
        }

        /// <summary>
        /// 유닛의 현재 월드 좌표 반환. GameObject가 파괴되었으면 Vector3.zero.
        /// </summary>
        public Vector3 GetUnitWorldPosition(int unitId)
        {
            var obj = _unitFactory != null ? _unitFactory.GetUnitObject(unitId) : null;
            return obj != null ? obj.transform.position : Vector3.zero;
        }

        /// <summary>
        /// 건물의 현재 월드 좌표 반환. GameObject가 파괴되었으면 Vector3.zero.
        /// </summary>
        public Vector3 GetBuildingWorldPosition(int buildingId)
        {
            var obj = _buildingFactory != null ? _buildingFactory.GetBuildingObject(buildingId) : null;
            return obj != null ? obj.transform.position : Vector3.zero;
        }
        // 이 인덱스는 실제 Root writer 통지만 사용한다. frame/tile snapshot으로 위치를 추측하지 않는다.
        private sealed class SpatialEntry
        {
            internal UnitData Unit;
            internal int Order, Binding, Slot;
            internal bool Overflow;
            internal (int X, int Z) Cell;
        }
        private readonly Dictionary<int, SpatialEntry> _entries = new Dictionary<int, SpatialEntry>();
        private readonly Dictionary<(int X, int Z), List<SpatialEntry>> _cells =
            new Dictionary<(int X, int Z), List<SpatialEntry>>();
        private readonly Stack<List<SpatialEntry>> _emptyCells = new Stack<List<SpatialEntry>>();
        private readonly List<SpatialEntry> _overflow = new List<SpatialEntry>();
        private readonly List<SpatialEntry> _gather = new List<SpatialEntry>();
        private readonly List<UnitData> _candidateUnits = new List<UnitData>();
        private readonly List<int> _removedIds = new List<int>();
        private readonly Comparison<SpatialEntry> _entryOrder;
        private ulong _rosterRevision;
        private bool _hasRoster, _leased, _faulted, _endObserved;
        private double _cellWidth;
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly string _combatIndexScope = Guid.NewGuid().ToString("N");
        private long _queries, _indexedGlobalUnits, _fallbackGlobalUnits, _candidateCount;
        private long _maintenanceReads, _poseUpdates, _fullScanFallbacks, _rosterRebuilds, _rosterVisits;
        private int _comparisonSamples, _comparisonMismatches, _comparisonFaults;
        private readonly int[] _sampleCategories = new int[8];
        private long _indexedInspections, _baselineInspections;
        private int _firstMismatchIndexed = -1, _firstMismatchBaseline = -1;
#endif
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void CountQuery() {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _queries++;
#endif
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void CountFallback(int global) {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _fullScanFallbacks++; _fallbackGlobalUnits += global;
#endif
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void CountIndexed(int global, int candidates) {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _indexedGlobalUnits += global; _candidateCount += candidates;
#endif
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void CountMaintenance(bool read) {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (read) _maintenanceReads++; else _poseUpdates++;
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void CountRoster(int count)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _rosterRebuilds++; _rosterVisits += count;
#endif
        }
        public void RecordQueryComparison(bool matched, int indexedInspections, int baselineInspections,
            bool faulted, int indexedId, int baselineId, int category)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _comparisonSamples++;
            if (category >= 0 && category < _sampleCategories.Length) _sampleCategories[category]++;
            _indexedInspections += indexedInspections; _baselineInspections += baselineInspections;
            if (faulted) _comparisonFaults++;
            else if (!matched)
            {
                if (_comparisonMismatches++ == 0) { _firstMismatchIndexed = indexedId; _firstMismatchBaseline = baselineId; }
            }
#endif
        }

        private static int CompareEntryOrder(SpatialEntry left, SpatialEntry right) => left.Order.CompareTo(right.Order);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private bool OwnsFactory => _unitFactory != null && _unitFactory.IsCombatCandidateSource(this);

        private bool TryCell(Vector3 position, out (int X, int Z) cell)
        {
            cell = default;
            if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z)
                || !( _cellWidth > 0d)) return false;
            double x = Math.Floor(position.x / _cellWidth), z = Math.Floor(position.z / _cellWidth);
            if (x <= int.MinValue + 2d || x >= int.MaxValue - 2d
                || z <= int.MinValue + 2d || z >= int.MaxValue - 2d) return false;
            cell = ((int)x, (int)z);
            return true;
        }

        private void RemoveLocation(SpatialEntry entry)
        {
            List<SpatialEntry> list = entry.Overflow ? _overflow
                : (_cells.TryGetValue(entry.Cell, out var bucket) ? bucket : null);
            if (list == null || entry.Slot < 0 || entry.Slot >= list.Count
                || !ReferenceEquals(list[entry.Slot], entry)) return;
            int last = list.Count - 1;
            if (entry.Slot != last) { list[entry.Slot] = list[last]; list[entry.Slot].Slot = entry.Slot; }
            list.RemoveAt(last);
            if (!entry.Overflow && list.Count == 0)
            {
                _cells.Remove(entry.Cell);
                if (_emptyCells.Count < _entries.Count + 1) _emptyCells.Push(list);
            }
            entry.Slot = -1;
        }

        private void Place(SpatialEntry entry, Vector3 position, bool known)
        {
            RemoveLocation(entry);
            entry.Overflow = !known || position == Vector3.zero || !TryCell(position, out var cell);
            if (entry.Overflow) { entry.Slot = _overflow.Count; _overflow.Add(entry); return; }
            TryCell(position, out cell);
            entry.Cell = cell;
            if (!_cells.TryGetValue(cell, out var list))
            {
                list = _emptyCells.Count > 0 ? _emptyCells.Pop() : new List<SpatialEntry>();
                _cells.Add(cell, list);
            }
            entry.Slot = list.Count;
            list.Add(entry);
        }

        private void RefreshBinding(SpatialEntry entry)
        {
            var obj = _unitFactory.GetUnitObject(entry.Unit.Id);
            CountMaintenance(true);
            entry.Binding = obj != null ? obj.GetInstanceID() : 0;
            Place(entry, obj != null ? obj.transform.position : default, obj != null);
        }

        private void RebuildRoster(IReadOnlyDictionary<int, UnitData> units, ulong revision)
        {
            _removedIds.Clear();
            foreach (var pair in _entries)
                if (!units.TryGetValue(pair.Key, out var live) || !ReferenceEquals(live, pair.Value.Unit))
                    _removedIds.Add(pair.Key);
            CountRoster(_entries.Count + units.Count + _removedIds.Count);
            foreach (int id in _removedIds) { RemoveLocation(_entries[id]); _entries.Remove(id); }
            int ordinal = 0;
            // 삭제 slot 재사용까지 포함한 현재 Dictionary 열거 순서다. ID/생성 순서로 대체하지 않는다.
            foreach (var unit in units.Values)
            {
                if (!_entries.TryGetValue(unit.Id, out var entry))
                {
                    entry = new SpatialEntry { Unit = unit, Slot = -1 };
                    _entries.Add(unit.Id, entry);
                    RefreshBinding(entry);
                }
                entry.Order = ordinal++;
            }
            while (_emptyCells.Count > _entries.Count) _emptyCells.Pop();
            _rosterRevision = revision;
            _hasRoster = true;
        }

        public bool TryBeginUnitCandidates(IReadOnlyDictionary<int, UnitData> units, ulong rosterRevision,
            Vector3 center, float radius, out IReadOnlyList<UnitData> candidates)
        {
            candidates = null;
            CountQuery();
            // provider 불일치·재진입·비정상 수치는 기존 getter의 전체 검색으로 돌아간다. 판정값은 바꾸지 않는다.
            if (!OwnsFactory || _faulted || _leased || units == null
                || (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) || !Finite(radius) || !(radius > 0f)
                || !Finite(center.x) || !Finite(center.y) || !Finite(center.z)
                || (double)radius * radius < 1.1754943508222875e-38d)
            { CountFallback(units != null ? units.Count : 0); return false; }
            try
            {
                double width = HexMetrics.TileHeight;
                if (!(width > 0d) || double.IsNaN(width) || double.IsInfinity(width))
                { CountFallback(units != null ? units.Count : 0); return false; }
                                // 최종 거리는 원래 Vector3.Distance만 사용한다. 2배 AABB는 float 경계를 포함하는 보수적 후보 범위다.
                double extent = (double)radius * 2d;
                double minXd = Math.Floor((center.x - extent) / width), maxXd = Math.Floor((center.x + extent) / width);
                double minZd = Math.Floor((center.z - extent) / width), maxZd = Math.Floor((center.z + extent) / width);
                double cellCount = (maxXd - minXd + 1d) * (maxZd - minZd + 1d);
                if (minXd <= int.MinValue || maxXd >= int.MaxValue || minZd <= int.MinValue || maxZd >= int.MaxValue
                    || cellCount > Math.Max(1, units.Count) || !(cellCount > 0d))
                { CountFallback(units != null ? units.Count : 0); return false; }
                if (_cellWidth != width) { ClearIndex(); _cellWidth = width; }
                if (!_hasRoster || _rosterRevision != rosterRevision) RebuildRoster(units, rosterRevision);
                _gather.Clear(); _candidateUnits.Clear();
                for (int x = (int)minXd; x <= (int)maxXd; x++)
                    for (int z = (int)minZd; z <= (int)maxZd; z++)
                        if (_cells.TryGetValue((x, z), out var bucket)) _gather.AddRange(bucket);
                _gather.AddRange(_overflow);
                if (_gather.Count >= units.Count)
                {
                    _gather.Clear(); CountFallback(units.Count); return false;
                }
                _gather.Sort(_entryOrder);
                foreach (var entry in _gather) _candidateUnits.Add(entry.Unit);
                CountIndexed(units.Count, _candidateUnits.Count);
                _leased = true;
                candidates = _candidateUnits;
                return true;
            }
            catch { _faulted = true; CountFallback(units != null ? units.Count : 0); ClearIndex(); return false; }
        }

        public void EndUnitCandidates()
        {
            _candidateUnits.Clear(); _gather.Clear(); _leased = false;
        }
        public void UnitPoseChanging(UnitData unit, int rootInstanceId)
        {
            if (!OwnsFactory || unit == null) return;
            try
            {
                if (_entries.TryGetValue(unit.Id, out var entry) && ReferenceEquals(entry.Unit, unit)
                    && entry.Binding == rootInstanceId) Place(entry, default, false);
            }
            catch { _faulted = true; ClearIndex(); }
        }
        public void UnitPoseChanged(UnitData unit, int rootInstanceId, Vector3 worldPosition)
        {
            if (!OwnsFactory || unit == null) return;
            try
            {
                if (_entries.TryGetValue(unit.Id, out var entry) && ReferenceEquals(entry.Unit, unit)
                    && entry.Binding == rootInstanceId) { CountMaintenance(false); Place(entry, worldPosition, true); }
            }
            catch { _faulted = true; ClearIndex(); }
        }
        public void UnitBindingChanged(int unitId)
        {
            if (!OwnsFactory) return;
            try { if (_entries.TryGetValue(unitId, out var entry)) RefreshBinding(entry); }
            catch { _faulted = true; ClearIndex(); }
        }
        private void ClearIndex()
        {
            _cells.Clear(); _entries.Clear(); _emptyCells.Clear(); _overflow.Clear();
            _candidateUnits.Clear(); _gather.Clear(); _removedIds.Clear(); _hasRoster = false;
        }
        public void ClearUnitBindings()
        {
            ObserveCombatIndexEnd(); ClearIndex();
        }
        public void ObserveCombatIndexEnd()
        {
            if (_endObserved) return;
            _endObserved = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                GameLog.Dev.Info("Combat", nameof(UnitWorldPositionProvider), "[COMBAT-SPATIAL] WORK",
                    $"scope={_combatIndexScope};policy=live-unit-index-v1;queries={_queries};indexedGlobalUnits={_indexedGlobalUnits};fallbackGlobalUnits={_fallbackGlobalUnits};candidateListEntries={_candidateCount};candidateMeaning=not-loop-inspections;maintenanceReads={_maintenanceReads};poseUpdates={_poseUpdates};fullScanFallbacks={_fullScanFallbacks};building=fullscan;indexFaulted={_faulted};rosterRebuilds={_rosterRebuilds};rosterVisits={_rosterVisits};nativeMs=unmeasured");
                GameLog.Dev.Info("Combat", nameof(UnitWorldPositionProvider), "[COMBAT-SPATIAL] COMPARE",
                    $"scope={_combatIndexScope};policy=live-unit-index-v1;runtimeBaselineSamples={_comparisonSamples};sampleKindsDetectFirstFound={string.Join(",", _sampleCategories)};sampleInspectionsIndexed={_indexedInspections};sampleInspectionsBaseline={_baselineInspections};sampleMismatches={_comparisonMismatches};sampleFaults={_comparisonFaults};firstMismatchIndexedId={_firstMismatchIndexed};firstMismatchBaselineId={_firstMismatchBaseline};sampleCost=extra-baseline-excluded-from-index-work");
            }
            catch { }
#endif
        }

    }
}
