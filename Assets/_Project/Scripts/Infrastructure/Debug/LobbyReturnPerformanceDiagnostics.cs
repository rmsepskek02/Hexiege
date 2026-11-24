#if UNITY_ANDROID && !UNITY_EDITOR && !DEVELOPMENT_BUILD && HEXIEGE_LOBBY_RETURN_MEASUREMENT
#define LOBBY_RETURN_RELEASE_MEASUREMENT
#endif
using System;
using System.IO;
using Hexiege.Application;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
#if LOBBY_RETURN_RELEASE_MEASUREMENT
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Profiling.LowLevel.Unsafe;
#endif

namespace Hexiege.Infrastructure
{
    /// <summary>명시적 Android 측정의 로비 복귀 구간만 관측한다. Dev 진단은 유지하고 Release는 별도 opt-in 계측만 추가한다.</summary>
    public sealed class LobbyReturnPerformanceDiagnostics : MonoBehaviour
    {
        public struct UiSnapshot
        {
            public bool LoadingPresent, LoadingHidden, AllBlockersClear;
        }
        private enum Phase
        {
            BeginReturn, LobbyReady, LoadingHidden, InputReady, Abort,
            SceneDelayBegin, SceneDelayEnd, SceneLoadCallBegin, SceneLoadCallReturned, LobbyStartEntered, LobbyStartCompleted
        }
        public enum Checkpoint
        {
            SceneDelayBegin = 5, SceneDelayEnd, SceneLoadCallBegin, SceneLoadCallReturned, LobbyStartEntered, LobbyStartCompleted
        }
        private enum Reason { None, Reentered, Timeout, OwnerDisabled, UnexpectedScene, MissingLoading, ObserverFault }
        private static bool _enabled;
        private static long _nextAttempt;
        private static LobbyReturnPerformanceDiagnostics _current;
        public static bool Enabled => _enabled;
        private Func<UiSnapshot> _readUi;
        private long _attempt;
        private double _started;
        private bool _finished, _lobbyReady, _sawLoading, _hiddenRecorded;
        private int _sceneHandle = int.MinValue;
        private string _sceneName;
#if (UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR) || LOBBY_RETURN_RELEASE_MEASUREMENT
        private bool _hasUiSnapshot;
        private UiSnapshot _lastUi;
#endif

#if LOBBY_RETURN_RELEASE_MEASUREMENT
        private const int MaximumSamples = 4096;
        private struct FrameSample
        {
            public int frame, frameGap, kind; // 0=head partial, 1=Update interval, 2=end-crossing interval
            public double startMs, endMs;
            public int collections;
            public long usedBytes, reservedBytes;
        }
        private struct PhaseSample { public Phase phase; public Reason reason; public int frame; public double realtimeMs; }
        [Serializable] private sealed class MetricReceipt
        {
            public string name, category = "Memory", unit = "Bytes", status;
            public int sampledReads;
        }
        [Serializable] private sealed class ReadyReceipt
        {
            public string schema = "release-lobby-return-ready-v1", mode = "ReleaseOptIn";
            public string buildGuid, unity, version, sessionUtc;
            public bool isDebugBuild, gcUsedDiscovered, gcReservedDiscovered;
            public string recorderRuntimeSupport = "NotYetVerified; description discovery only, valid/running/samples checked per attempt";
        }
        [Serializable] private sealed class ReleaseReceipt
        {
            public string schema = "release-lobby-return-measurement-v1", mode = "ReleaseOptIn";
            public string buildGuid, unity, version, backend, sessionUtc, role, endReason;
            public string csvSha256, csvName = "frames.csv", endCrossingStatus, managedCollections;
            public string readySha256;
            public string fullUpdateAverageMs, fullUpdateP95Ms, fullUpdateMaxMs;
            public string gcAllocatedBytes = "Unavailable", gcTimeMs = "Unavailable", cpuGpuTimeMs = "UnavailableWithCurrentSettings";
            public bool isDebugBuild, networkActive, overflow, phaseComplete, comparisonEligible;
            public bool uiReady, sawLoading, lastUiSnapshotKnown, lastLoadingHidden, lastBlockersClear;
            public bool sceneDelayBeginObserved, sceneDelayEndObserved, sceneLoadCallBeginObserved,
                sceneLoadCallReturnedObserved, lobbyStartEnteredObserved, lobbyStartCompletedObserved;
            public string checkpointSemantics = "Presence only; missing checkpoints are NotObserved, not proof of failure. Ordering may overlap. LobbyStartCompleted does not await UGS initialization.";
            public int targetFrameRate, vSyncCount, beginFrame, endFrame, rows, fullUpdateIntervals, frameGapIntervals;
            public double beginRealtimeMs, endRealtimeMs, durationMs, setupMs, bufferPreparationMs, readyPreparationMs;
            public string setupManagedCollections, bufferStartupManagedCollections;
            public double headPartialMs, endPartialLowerBoundMs, endCrossingIntervalMs, endCrossingOutsideWindowMs;
            public double worstObservedIntervalIncludingBoundariesMs, savePreparationMs;
            public string timingSemantics = "Update cadence including waits; not CPU/render time. Partial/gapped/crossing rows excluded from fullUpdate stats; inspect boundaries separately.";
            public string counterSemantics = "Latest completed recorder sample at read frame; not assigned to the current Update interval. GC.CollectionCount reports counts, not GC time/allocations.";
            public string setupSemantics = "Buffers allocated at opt-in startup. Component/recorder setup occurs before Begin; setup may still affect later heap/GC state. Scene name is cached per handle; a transition lookup may allocate. Allocation-free runtime is not claimed.";
            public string saveSemantics = "All serialization/file IO occurs after InputReady/Abort; save-receipt.csv records file-save elapsed separately. Abrupt kill or missing END/readback invalidates custody.";
            public MetricReceipt[] metrics;
        }
        private static FrameSample[] _sampleBuffer;
        private static PhaseSample[] _phaseBuffer;
        private static string _runRoot, _sessionUtc, _buildGuid, _unity, _version, _readyHash;
        private static double _bufferPreparationMs;
        private static double _readyPreparationMs;
        private static int _bufferStartupCollections;
        private static bool _usedAvailable, _reservedAvailable;
        private static LobbyReturnPerformanceDiagnostics _pendingSave;
        private ProfilerRecorder _usedRecorder, _reservedRecorder;
        private int _sampleCount, _phaseCount, _beginFrame, _endFrame, _lastReadFrame, _usedReads, _reservedReads, _checkpointMask;
        private int _beginCollection, _endCollection, _setupCollectionDelta, _targetFrameRate, _vSyncCount;
        private double _lastObserved, _ended, _setupMs, _endPartialMs;
        private bool _overflow, _awaitingEndUpdate, _releaseSaved, _networkActive;
        private string _role;
        private Reason _endReason;

        private static void InitializeReleaseStorage()
        {
            double begin = Time.realtimeSinceStartupAsDouble;
            int initialCollections = ReadCollectionCount();
            _sampleBuffer = new FrameSample[MaximumSamples];
            _phaseBuffer = new PhaseSample[32];
            _sessionUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            _runRoot = Path.Combine(UnityEngine.Application.persistentDataPath, "PerformanceMeasurements",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
            _buildGuid = UnityEngine.Application.buildGUID; _unity = UnityEngine.Application.unityVersion;
            _version = UnityEngine.Application.version;
            _usedAvailable = _reservedAvailable = false;
            try
            {
                var handles = new List<ProfilerRecorderHandle>();
                ProfilerRecorderHandle.GetAvailable(handles);
                foreach (var handle in handles)
                {
                    if (!handle.Valid) continue;
                    var description = ProfilerRecorderHandle.GetDescription(handle);
                    if (description.UnitType != ProfilerMarkerDataUnit.Bytes || description.Category.Name != ProfilerCategory.Memory.Name) continue;
                    if (description.Name == "GC Used Memory") _usedAvailable = true;
                    if (description.Name == "GC Reserved Memory") _reservedAvailable = true;
                }
            }
            catch { _usedAvailable = _reservedAvailable = false; }
            _bufferPreparationMs = (Time.realtimeSinceStartupAsDouble - begin) * 1000d;
            double readyStart = Time.realtimeSinceStartupAsDouble;
            Directory.CreateDirectory(_runRoot);
            string readyPath = Path.Combine(_runRoot, "ready.json");
            using (var stream = new FileStream(readyPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(JsonUtility.ToJson(new ReadyReceipt { buildGuid = _buildGuid, unity = _unity,
                    version = _version, sessionUtc = _sessionUtc, isDebugBuild = Debug.isDebugBuild,
                    gcUsedDiscovered = _usedAvailable, gcReservedDiscovered = _reservedAvailable }, true));
            _readyHash = HashFile(readyPath);
            _readyPreparationMs = (Time.realtimeSinceStartupAsDouble - readyStart) * 1000d;
            int finalCollections = ReadCollectionCount();
            _bufferStartupCollections = initialCollections >= 0 && finalCollections >= initialCollections ? finalCollections - initialCollections : -1;
        }

        private void PrepareRelease(double begin, int collections)
        {
            _networkActive = NetworkContext.IsNetworkActive;
            _role = !_networkActive ? "Single" : NetworkContext.IsNetworkServer ? "Host" : "Client";
            _targetFrameRate = UnityEngine.Application.targetFrameRate; _vSyncCount = QualitySettings.vSyncCount;
            ReadSceneName(); // 처음 이름 조회는 Begin 이전 준비 비용으로 둔다.
            try { if (_usedAvailable) _usedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory", 1); } catch { }
            try { if (_reservedAvailable) _reservedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory", 1); } catch { }
            _beginCollection = ReadCollectionCount();
            _setupCollectionDelta = collections >= 0 && _beginCollection >= collections ? _beginCollection - collections : -1;
            _setupMs = (Time.realtimeSinceStartupAsDouble - begin) * 1000d;
        }

        private static int ReadCollectionCount() { try { return GC.CollectionCount(0); } catch { return -1; } }
        private static long ReadCounter(ref ProfilerRecorder recorder, ref int reads)
        {
            try
            {
                if (recorder.Valid && recorder.IsRunning && recorder.Count > 0)
                { long value = recorder.LastValue; if (value >= 0) { reads++; return value; } }
            }
            catch { }
            return -1;
        }

        private void CaptureReleaseSample(bool endCrossing)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (_sampleCount >= MaximumSamples) { _overflow = true; return; }
            int index = _sampleCount++;
            _sampleBuffer[index] = new FrameSample
            {
                frame = Time.frameCount, frameGap = Time.frameCount - _lastReadFrame,
                kind = endCrossing ? 2 : index == 0 ? 0 : 1,
                startMs = _lastObserved * 1000d, endMs = now * 1000d,
                collections = ReadCollectionCount(),
                usedBytes = ReadCounter(ref _usedRecorder, ref _usedReads),
                reservedBytes = ReadCounter(ref _reservedRecorder, ref _reservedReads)
            };
            _lastObserved = now; _lastReadFrame = Time.frameCount;
        }

        private void ReleaseDiscard()
        {
            _releaseSaved = true; _awaitingEndUpdate = false;
            try { _usedRecorder.Dispose(); } catch { }
            try { _reservedRecorder.Dispose(); } catch { }
        }

        private static string Value(long value) => value < 0 ? "Unavailable" : value.ToString(CultureInfo.InvariantCulture);
        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string HashFile(string path)
        {
            using var hash = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
        }
        private void SaveRelease(bool crossingAvailable)
        {
            if (_releaseSaved) return;
            ReleaseDiscard();
            if (ReferenceEquals(_pendingSave, this)) _pendingSave = null;
            try
            {
                double saveStart = Time.realtimeSinceStartupAsDouble;
                var receipt = new ReleaseReceipt
                {
                    buildGuid = _buildGuid, unity = _unity, version = _version, sessionUtc = _sessionUtc,
                    readySha256 = _readyHash,
#if ENABLE_IL2CPP
                    backend = "IL2CPP",
#else
                    backend = "Mono",
#endif
                    role = _role, networkActive = _networkActive, isDebugBuild = Debug.isDebugBuild,
                    endReason = _endReason.ToString(), phaseComplete = _endReason == Reason.None,
                    comparisonEligible = _endReason == Reason.None && !_overflow && crossingAvailable,
                    uiReady = _lobbyReady, sawLoading = _sawLoading, lastUiSnapshotKnown = _hasUiSnapshot,
                    lastLoadingHidden = _lastUi.LoadingHidden, lastBlockersClear = _lastUi.AllBlockersClear,
                    sceneDelayBeginObserved = (_checkpointMask & 1) != 0,
                    sceneDelayEndObserved = (_checkpointMask & 2) != 0,
                    sceneLoadCallBeginObserved = (_checkpointMask & 4) != 0,
                    sceneLoadCallReturnedObserved = (_checkpointMask & 8) != 0,
                    lobbyStartEnteredObserved = (_checkpointMask & 16) != 0,
                    lobbyStartCompletedObserved = (_checkpointMask & 32) != 0,
                    overflow = _overflow, targetFrameRate = _targetFrameRate, vSyncCount = _vSyncCount,
                    beginFrame = _beginFrame, endFrame = _endFrame, rows = _sampleCount,
                    beginRealtimeMs = _started * 1000d, endRealtimeMs = _ended * 1000d, durationMs = (_ended - _started) * 1000d,
                    setupMs = _setupMs, bufferPreparationMs = _bufferPreparationMs, setupManagedCollections = Value(_setupCollectionDelta),
                    readyPreparationMs = _readyPreparationMs,
                    bufferStartupManagedCollections = Value(_bufferStartupCollections),
                    managedCollections = Value(_beginCollection >= 0 && _endCollection >= _beginCollection ? _endCollection - _beginCollection : -1),
                    endPartialLowerBoundMs = _endPartialMs,
                    endCrossingStatus = crossingAvailable && !_overflow ? "CapturedOutsideEnd" : "Unavailable",
                    metrics = new[] {
                        new MetricReceipt { name = "GC Used Memory", status = _usedReads > 0 ? "AvailableLatestCompletedSample" : "Unavailable", sampledReads = _usedReads },
                        new MetricReceipt { name = "GC Reserved Memory", status = _reservedReads > 0 ? "AvailableLatestCompletedSample" : "Unavailable", sampledReads = _reservedReads } }
                };
                string directory = Path.Combine(_runRoot, "attempt-" + _attempt.ToString(CultureInfo.InvariantCulture));
                Directory.CreateDirectory(directory);
                string csv = Path.Combine(directory, receipt.csvName);
                var full = new List<double>();
                using (var stream = new FileStream(csv, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.WriteLine("# Update cadence, not CPU/render time. kind 0=head partial 1=Update interval 2=end-crossing (includes outside-window time). Counter values are latest completed samples at read time.");
                    writer.WriteLine("readFrame,kind,frameGap,startRealtimeMs,endRealtimeMs,observedIntervalMs,managedCollectionCount0,gcUsedLatestCompletedBytes,gcReservedLatestCompletedBytes");
                    for (int i = 0; i < _sampleCount; i++)
                    {
                        FrameSample row = _sampleBuffer[i]; double interval = row.endMs - row.startMs;
                        receipt.worstObservedIntervalIncludingBoundariesMs = Math.Max(receipt.worstObservedIntervalIncludingBoundariesMs, interval);
                        if (row.kind == 0) receipt.headPartialMs = interval;
                        else if (row.kind == 2)
                        { receipt.endCrossingIntervalMs = interval; receipt.endCrossingOutsideWindowMs = Math.Max(0d, row.endMs - receipt.endRealtimeMs); }
                        else if (row.frameGap == 1) full.Add(interval);
                        else receipt.frameGapIntervals++;
                        writer.WriteLine(row.frame + "," + row.kind + "," + row.frameGap + "," + N(row.startMs) + "," + N(row.endMs) + "," + N(interval)
                            + "," + Value(row.collections) + "," + Value(row.usedBytes) + "," + Value(row.reservedBytes));
                    }
                    writer.WriteLine("# PHASE,phase,reason,frame,realtimeMs");
                    for (int i = 0; i < _phaseCount; i++)
                    { var phase = _phaseBuffer[i]; writer.WriteLine("# PHASE," + phase.phase + "," + phase.reason + "," + phase.frame + "," + N(phase.realtimeMs)); }
                    writer.WriteLine("# END,rows=" + _sampleCount + ",overflow=" + _overflow + ",phaseComplete=" + receipt.phaseComplete);
                }
                full.Sort(); receipt.fullUpdateIntervals = full.Count;
                double sum = 0; foreach (double value in full) sum += value;
                receipt.fullUpdateAverageMs = full.Count > 0 ? N(sum / full.Count) : "Unavailable";
                receipt.fullUpdateMaxMs = full.Count > 0 ? N(full[full.Count - 1]) : "Unavailable";
                receipt.fullUpdateP95Ms = full.Count > 0 ? N(full[(int)Math.Ceiling(full.Count * .95d) - 1]) : "Unavailable";
                receipt.worstObservedIntervalIncludingBoundariesMs = Math.Max(receipt.worstObservedIntervalIncludingBoundariesMs, _endPartialMs);
                receipt.csvSha256 = HashFile(csv);
                receipt.savePreparationMs = (Time.realtimeSinceStartupAsDouble - saveStart) * 1000d;
                string json = Path.Combine(directory, "receipt.json");
                using (var stream = new FileStream(json, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(JsonUtility.ToJson(receipt, true));
                // 이 마지막 영수증이 없으면 저장 완주로 간주하지 않는다. 자기 자신의 쓰기 시간은 포함하지 않는다.
                using (var stream = new FileStream(Path.Combine(directory, "save-receipt.csv"), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    writer.WriteLine("END,receiptSha256=" + HashFile(json) + ",saveElapsedBeforeFinalReceiptMs=" + N((Time.realtimeSinceStartupAsDouble - saveStart) * 1000d));
            }
            catch { /* 부분 파일/최종 END 부재는 수집 검증에서 실패로 남긴다. 게임 로그/흐름은 바꾸지 않는다. */ }
            finally { try { Destroy(this); } catch { } }
        }
#endif

        private static class Markers
        {
            internal static readonly ProfilerMarker Begin = new("Hexiege.LobbyReturn.BeginReturn");
            internal static readonly ProfilerMarker Ready = new("Hexiege.LobbyReturn.LobbyReady");
            internal static readonly ProfilerMarker Hidden = new("Hexiege.LobbyReturn.LoadingHidden");
            internal static readonly ProfilerMarker Input = new("Hexiege.LobbyReturn.InputReady");
            internal static readonly ProfilerMarker Abort = new("Hexiege.LobbyReturn.Abort");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _enabled = false;
#if LOBBY_RETURN_RELEASE_MEASUREMENT
            if (_pendingSave != null)
            {
                _pendingSave.ReleaseDiscard();
                try { Destroy(_pendingSave); } catch { }
            }
            _pendingSave = null;
            if (_current != null) _current.ReleaseDiscard();
            _sampleBuffer = null;
            _phaseBuffer = null;
#endif
            if (_current != null)
            {
                _current._finished = true;
                _current._readUi = null;
                try { Destroy(_current); } catch { }
            }
            _current = null;
            _nextAttempt = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ReadOptInOnce()
        {
#if (UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR) || LOBBY_RETURN_RELEASE_MEASUREMENT
            try
            {
                _enabled = File.Exists(Path.Combine(UnityEngine.Application.persistentDataPath,
                    "PerformanceMeasurements/EnableLobbyReturnBaseline"));
#if LOBBY_RETURN_RELEASE_MEASUREMENT
                if (Debug.isDebugBuild) _enabled = false;
                if (_enabled) InitializeReleaseStorage();
#endif
            }
            catch { _enabled = false; }
#endif
        }

        public static void Begin(MonoBehaviour owner, Func<UiSnapshot> readUi)
        {
            if (!_enabled) return;
            LobbyReturnPerformanceDiagnostics observer = null;
            try
            {
#if LOBBY_RETURN_RELEASE_MEASUREMENT
                if (_pendingSave != null) _pendingSave.SaveRelease(false);
#endif
                if (_current != null) _current.Finish(Reason.Reentered);
                if (owner == null || readUi == null) return;
#if LOBBY_RETURN_RELEASE_MEASUREMENT
                double setupBegin = Time.realtimeSinceStartupAsDouble;
                int setupCollections = ReadCollectionCount();
#endif
                observer = owner.gameObject.AddComponent<LobbyReturnPerformanceDiagnostics>();
                observer._attempt = ++_nextAttempt;
#if LOBBY_RETURN_RELEASE_MEASUREMENT
                observer.PrepareRelease(setupBegin, setupCollections);
#endif
                observer._started = Time.realtimeSinceStartupAsDouble;
#if LOBBY_RETURN_RELEASE_MEASUREMENT
                observer._lastObserved = observer._started;
                observer._lastReadFrame = Time.frameCount;
                observer._beginFrame = Time.frameCount;
#endif
                observer._readUi = readUi;
                _current = observer;
                observer.Record(Phase.BeginReturn);
            }
            catch
            {
                if (observer != null) observer.Finish(Reason.ObserverFault);
                else if (_current != null) _current.Finish(Reason.ObserverFault);
            }
        }

        /// <summary>기존 활성 시도만 캡처한다. 종료 후 새 로딩은 이전 시도의 ticket을 물려받지 않는다.</summary>
        public static long CaptureAttemptTicket()
        {
            try { return _current != null && !_current._finished ? _current._attempt : 0L; }
            catch { return 0L; }
        }

        /// <summary>이미 캡처한 동일 시도만 관측한다. pending 저장에 추가해도 측정 End를 연장하지 않는다.</summary>
        public static void Stamp(Checkpoint checkpoint, long ticket)
        {
            if (ticket == 0L || checkpoint < Checkpoint.SceneDelayBegin || checkpoint > Checkpoint.LobbyStartCompleted) return;
            try
            {
                LobbyReturnPerformanceDiagnostics observer = _current;
                if (observer == null || observer._finished || observer._attempt != ticket) observer = null;
#if LOBBY_RETURN_RELEASE_MEASUREMENT
                if (observer == null && _pendingSave != null && _pendingSave._awaitingEndUpdate && _pendingSave._attempt == ticket)
                    observer = _pendingSave;
#endif
                if (observer == null) return;
#if LOBBY_RETURN_RELEASE_MEASUREMENT
                observer._checkpointMask |= 1 << ((int)checkpoint - (int)Checkpoint.SceneDelayBegin);
#endif
                observer.Record((Phase)(int)checkpoint);
            }
            catch { /* wait/load/bind의 기존 예외/return을 계측이 바꾸지 않는다. */ }
        }

        public static void LobbyReady()
        {
            if (_current == null || _current._finished) return;
            try
            {
                if (_current._lobbyReady) return;
                _current._lobbyReady = true;
                _current.Record(Phase.LobbyReady);
                _current.Evaluate();
            }
            catch { if (_current != null) _current.Finish(Reason.ObserverFault); }
        }

        public static void LoadingChanged(bool show)
        {
            if (_current == null || _current._finished) return;
            try
            {
                if (show) _current._sawLoading = true;
                _current.Evaluate();
            }
            catch { if (_current != null) _current.Finish(Reason.ObserverFault); }
        }

        private void Update()
        {
#if LOBBY_RETURN_RELEASE_MEASUREMENT
            if (_awaitingEndUpdate)
            {
                try
                {
                    if (Time.frameCount > _endFrame) { CaptureReleaseSample(true); SaveRelease(true); }
                }
                catch { SaveRelease(false); }
                return;
            }
#endif
            if (_finished) return;
            try
            {
#if LOBBY_RETURN_RELEASE_MEASUREMENT
                CaptureReleaseSample(false);
#endif
                Evaluate();
            }
            catch { Finish(Reason.ObserverFault); }
        }

        private void Evaluate()
        {
            if (_finished) return;
            if (Time.realtimeSinceStartupAsDouble - _started >= 60d) { Finish(Reason.Timeout); return; }
            string scene = ReadSceneName();
            if (!string.IsNullOrEmpty(scene) && scene != "Game" && scene != "Lobby")
            { Finish(Reason.UnexpectedScene); return; }
            UiSnapshot ui = _readUi();
#if (UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR) || LOBBY_RETURN_RELEASE_MEASUREMENT
            _lastUi = ui;
            _hasUiSnapshot = true;
#endif
            if (_lobbyReady && !ui.LoadingPresent) { Finish(Reason.MissingLoading); return; }
            // 시작 시 이미 숨겨진 overlay를 복귀 완료로 세지 않는다.
            if (!_sawLoading || !ui.LoadingHidden) return;
            if (!_hiddenRecorded) { _hiddenRecorded = true; Record(Phase.LoadingHidden); }
            if (_lobbyReady && scene == "Lobby" && ui.AllBlockersClear) Finish(Reason.None);
        }

        private string ReadSceneName()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.handle != _sceneHandle)
            {
                _sceneHandle = scene.handle;
                _sceneName = scene.name;
            }
            return _sceneName;
        }

        private void Finish(Reason reason)
        {
            if (_finished) return;
            _finished = true;
            if (ReferenceEquals(_current, this)) _current = null;
            Record(reason == Reason.None ? Phase.InputReady : Phase.Abort, reason);
            _readUi = null;
#if LOBBY_RETURN_RELEASE_MEASUREMENT
            _ended = Time.realtimeSinceStartupAsDouble;
            _endPartialMs = Math.Max(0d, (_ended - _lastObserved) * 1000d);
            _endFrame = Time.frameCount;
            _endReason = reason;
            _endCollection = ReadCollectionCount();
            if (reason == Reason.None)
            {
                _awaitingEndUpdate = true;
                _pendingSave = this;
                return;
            }
            SaveRelease(false);
            return;
#else
            try { Destroy(this); } catch { }
#endif
        }
        private void OnDisable()
        {
#if LOBBY_RETURN_RELEASE_MEASUREMENT
            if (_awaitingEndUpdate) { SaveRelease(false); return; }
#endif
            Finish(Reason.OwnerDisabled);
        }
        private void OnDestroy() { OnDisable(); }

        private void Record(Phase phase, Reason reason = Reason.None)
        {
#if LOBBY_RETURN_RELEASE_MEASUREMENT
            if (_phaseCount < _phaseBuffer.Length)
                _phaseBuffer[_phaseCount++] = new PhaseSample { phase = phase, reason = reason,
                    frame = Time.frameCount, realtimeMs = Time.realtimeSinceStartupAsDouble * 1000d };
            else _overflow = true;
#endif
#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
            try
            {
                if (phase <= Phase.Abort)
                {
                    ProfilerMarker marker = phase == Phase.BeginReturn ? Markers.Begin : phase == Phase.LobbyReady ? Markers.Ready
                        : phase == Phase.LoadingHidden ? Markers.Hidden : phase == Phase.InputReady ? Markers.Input : Markers.Abort;
                    using (marker.Auto()) { } // 구간 경계 pulse다. 별개 프레임의 구간 전체 CPU 시간은 아니다.
                }
                double realtime = Time.realtimeSinceStartupAsDouble;
                string data = FormattableString.Invariant($"Attempt={_attempt}, Phase={phase}, Reason={reason}, Frame={Time.frameCount}, RealtimeMs={realtime * 1000d:G17}, ElapsedMs={(realtime - _started) * 1000d:F3}, UiReady={_lobbyReady}, SawLoading={_sawLoading}, LastUiSnapshotKnown={_hasUiSnapshot}, LastLoadingHidden={_lastUi.LoadingHidden}, LastBlockersClear={_lastUi.AllBlockersClear}, DetailedDiagnostics=Unchanged");
                string line = GameLog.Compose(Hexiege.Application.LogLevel.Info, "Performance", nameof(LobbyReturnPerformanceDiagnostics), "[LOBBY-RETURN-PERF]", data);
                if (!RuntimeLogger.TryArchiveDeviceRecord(line, LogType.Log, true, null))
                    Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", line);
            }
            catch { /* 계측/파일/콘솔 실패가 복귀로 전파되거나 재귀 로그를 만들지 않는다. */ }
#endif
        }
    }
}
