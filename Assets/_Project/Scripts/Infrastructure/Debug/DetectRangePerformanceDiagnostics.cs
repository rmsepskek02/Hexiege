#if UNITY_ANDROID && !UNITY_EDITOR && !DEVELOPMENT_BUILD && HEXIEGE_DETECT_RANGE_COMPARISON
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Hexiege.Application;
using Hexiege.Domain;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace Hexiege.Infrastructure
{
    /// <summary>명시적 Release 측정 한 경기에서만 읽기 전용 감지 쌍을 저장한다. 게임 결과는 원래 optimized 반환이다.</summary>
    internal sealed class DetectRangePerformanceDiagnostics : MonoBehaviour
    {
        private const int Capacity = 256, Stride = 256;
        private struct Row { internal int frame; internal bool candidate; internal double realtimeMs; internal UnitCombatUseCase.DetectComparisonSample sample; }
        [Serializable] private sealed class Header
        {
            public string schema = "detect-range-comparison-v1", buildGuid, utc, readySha256, startSha256, csvSha256, reason, role;
            public string mode = "ReleaseOptInDetectComparison", unity, version, backend;
            public string define = "HEXIEGE_DETECT_RANGE_COMPARISON";
            public string semantics = "Frozen-coordinate query kernels only; capture separately. Not FPS, Transform-cost saving, CPU self or GC time. Startup buffers and diagnostics can perturb gameplay timing.";
            public string statusMeaning = "0=equal,1=hex-fallback-skipped,2=bool-mismatch,3=snapshot-invalid-or-capacity,4=observer-fault";
            public bool isDebugBuild, networkActive, started, completed;
            public int stride = Stride, capacity = Capacity, rows, queries, detected, notDetected;
            public int snapshotCapacity = 2048;
            public string samplingSemantics = "Starts on first true enemy detection; later false queries included. Warmup rows0/1 excluded from paired timing comparisons. Inspections include friendly/dead entries before filters. Capture queries count live provider reads; paired queries count snapshot accesses. Capture ticks include full scan, coordinate reads and snapshot population, not isolated Transform time. Role captured at first true detection only.";
            public long stopwatchFrequency = System.Diagnostics.Stopwatch.Frequency;
            public double startRealtimeMs, stopRealtimeMs, maximumSeconds = 60;
        }
        private static DetectRangePerformanceDiagnostics _owner;
        private HostBattleSession _host;
        private readonly Row[] _rows = new Row[Capacity];
        private readonly UnitCombatUseCase.DetectComparisonSnapshot _snapshot = new UnitCombatUseCase.DetectComparisonSnapshot();
        private Header _header;
        private string _directory;
        private int _count, _queries, _detected;
        private bool _started, _samplingClosed, _gameEnded, _saved, _busy;
        private double _start, _stop;
        private UnitCombatUseCase _combat;
        private IDisposable _endSubscription;
        private Action<UnitCombatUseCase, UnitData, Vector3, bool, bool> _observer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            try { if (_owner != null) _owner.Detach(); } catch { }
            UnitCombatUseCase.DetectComparisonObserver = null; _owner = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            GameObject go = null;
            DetectRangePerformanceDiagnostics created = null;
            try
            {
                string root = Path.Combine(UnityEngine.Application.persistentDataPath, "PerformanceMeasurements");
                bool hostMode = File.Exists(Path.Combine(root, "EnableHostBattleCapture"));
                if (!hostMode && !File.Exists(Path.Combine(root, "EnableDetectRangeComparison"))) return;
                go = new GameObject("DetectRangeComparisonObserver");
                DontDestroyOnLoad(go);
                var owner = go.AddComponent<DetectRangePerformanceDiagnostics>();
                created = owner;
                if (hostMode)
                {
                    owner._host = new HostBattleSession(owner, root);
                    owner._host.Initialize(); _owner = owner;
                    return;
                }
                owner._directory = Path.Combine(root, "detect-range-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(owner._directory);
                owner._header = new Header { buildGuid = UnityEngine.Application.buildGUID,
                    utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), isDebugBuild = Debug.isDebugBuild,
                    unity = UnityEngine.Application.unityVersion, version = UnityEngine.Application.version,
#if ENABLE_IL2CPP
                    backend = "IL2CPP"
#else
                    backend = "Mono"
#endif
                };
                owner.WriteNew("ready.json", JsonUtility.ToJson(owner._header, true));
                owner._header.readySha256 = owner.Hash("ready.json");
                owner._observer = owner.Observe;
                owner._endSubscription = GameEvents.OnGameEnd.Subscribe(_ => owner._gameEnded = true);
                UnitCombatUseCase.DetectComparisonObserver = owner._observer;
                _owner = owner;
            }
            catch (Exception error) { created?._host?.FailedStartup(error.GetType().Name); if (go != null) Destroy(go); }
        }

        private void Observe(UnitCombatUseCase combat, UnitData unit, Vector3 candidate, bool useOverride, bool live)
        {
            if (_host != null) { _host.Observe(combat, unit, candidate, useOverride, live); return; }
            if (_samplingClosed || _busy || _gameEnded) return;
            if (!_started && !live) return;
            try
            {
                _busy = true;
                double now = Time.realtimeSinceStartupAsDouble;
                if (!_started)
                {
                    _started = true; _start = now; _combat = combat;
                    _header.started = true; _header.startRealtimeMs = now * 1000;
                    _header.networkActive = NetworkContext.IsNetworkActive;
                    _header.role = !_header.networkActive ? "Single" : NetworkContext.IsNetworkServer ? "Host" : "Client";
                    if (_header.role == "Client") { StopSampling("ClientRejected", now); return; }
                }
                if (!ReferenceEquals(_combat, combat)) { StopSampling("MatchChanged", now); return; }
                if (now - _start >= 60) { StopSampling("TimeLimit", now); return; }
                _queries++; if (live) _detected++;
                if ((_queries - 1) % Stride != 0) return;
                var sample = combat.MeasureDetectComparison(unit, candidate, useOverride, live, (_count & 1) != 0, _snapshot);
                _rows[_count++] = new Row { frame = Time.frameCount, candidate = useOverride, realtimeMs = now * 1000, sample = sample };
                if (_count >= Capacity) StopSampling("PairLimit", Time.realtimeSinceStartupAsDouble);
            }
            catch { StopSampling("ObserverFault", Time.realtimeSinceStartupAsDouble); }
            finally { _busy = false; }
        }

        private void StopSampling(string reason, double now)
        {
            if (_samplingClosed) return;
            _samplingClosed = true; _stop = now; _header.reason = reason;
            if (UnitCombatUseCase.DetectComparisonObserver == _observer) UnitCombatUseCase.DetectComparisonObserver = null;
        }

        private void Update()
        {
            if (_host != null) { _host.Update(); return; }
            if (_saved) return;
            if (_started && !_samplingClosed && Time.realtimeSinceStartupAsDouble - _start >= 60)
                StopSampling("TimeLimit", Time.realtimeSinceStartupAsDouble);
            // 표본 상한만으로 경기 중에 파일을 쓰지 않는다. 종료 이벤트 뒤에만 직렬화한다.
            if (_gameEnded)
            {
                StopSampling(_started ? "GameEnd" : "NotExercised", Time.realtimeSinceStartupAsDouble);
                Save();
            }
        }

        private void Save()
        {
            if (_saved) return;
            _saved = true;
            try
            {
                _header.rows = _count; _header.queries = _queries; _header.detected = _detected;
                _header.notDetected = _queries - _detected; _header.stopRealtimeMs = _stop * 1000;
                // 시작 기록도 메모리에만 두었다가 경기 종료 뒤 저장한다.
                WriteNew("start.json", JsonUtility.ToJson(_header, true));
                _header.startSha256 = Hash("start.json");
                var csv = new StringBuilder("index,warmup,frame,candidate,realtimeMs,status,order,units,buildings,live,baseline,optimized,captureQueries,baselineQueries,optimizedQueries,baselineInspections,optimizedInspections,captureTicks,baselineTicks,optimizedTicks,snapshotOverflow\n");
                for (int i = 0; i < _count; i++)
                {
                    Row row = _rows[i]; var s = row.sample;
                    csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4:R},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}\n",
                        i, i < 2 ? 1 : 0, row.frame, row.candidate ? 1 : 0, row.realtimeMs, s.status, s.order, s.units, s.buildings,
                        s.live ? 1 : 0, s.baseline ? 1 : 0, s.optimized ? 1 : 0, s.captureQueries, s.baselineQueries, s.optimizedQueries,
                        s.baselineInspections, s.optimizedInspections, s.captureTicks, s.baselineTicks, s.optimizedTicks, s.snapshotOverflow ? 1 : 0);
                }
                csv.Append("# END,rows=").Append(_count).Append('\n');
                WriteNew("pairs.csv", csv.ToString()); _header.csvSha256 = Hash("pairs.csv");
                _header.completed = true;
                WriteNew("completed.json", JsonUtility.ToJson(_header, true));
                WriteNew("END.json", "{\"completedSha256\":\"" + Hash("completed.json") + "\"}");
            }
            catch { /* END 없는 자료는 불완전하다. 게임에 예외나 재귀 로그를 보내지 않는다. */ }
            finally { Detach(); }
        }
        private void WriteNew(string name, string text)
        {
            using (var stream = new FileStream(Path.Combine(_directory, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) { writer.Write(text); writer.Flush(); }
        }
        private string Hash(string name)
        { using (var hash = SHA256.Create()) using (var file = File.OpenRead(Path.Combine(_directory, name))) return BitConverter.ToString(hash.ComputeHash(file)).Replace("-", ""); }
        private void Detach()
        {
            _host?.Dispose();
            if (UnitCombatUseCase.DetectComparisonObserver == _observer) UnitCombatUseCase.DetectComparisonObserver = null;
            _endSubscription?.Dispose(); _endSubscription = null; _snapshot.Clear(); enabled = false;
        }
        private void OnDisable() { _host?.RestoreBloom(); }
        private void OnDestroy() { Detach(); if (_owner == this) _owner = null; }
        internal struct HostNccTicket { internal long ticks; internal int frame; internal double realtimeMs; }
        internal static HostNccTicket BeginHostNccTick(NetworkCombatController source)
        {
            try { return _owner != null && _owner._host != null ? _owner._host.BeginTick(source) : default; }
            catch { return default; }
        }
        internal static void EndHostNccTick(NetworkCombatController source, HostNccTicket ticket)
        {
            if (ticket.ticks == 0) return;
            try { if (_owner != null) _owner._host?.EndTick(source, ticket); } catch { }
        }

        /// <summary>v1과 파일/명령을 공유하지 않는 한 경기 Host 측정 상태다.</summary>
        private sealed class HostBattleSession : IDisposable
        {
            private const int FrameCapacity = 8192, TickCapacity = 4096, MetricCount = 7;
            [Serializable] internal sealed class MetricReport { public string requested, actual, category, unit, status; }
            [Serializable] private sealed class HostReport
            {
                public string protocol = "host-battle-v2", mode = "ReleaseOptInHostBattle", readyNonce, captureToken, buildGuid, unity, version, backend, utc, observedUtc, mapHash, reason, readySha256, framesSha256, nccSha256, pairsSha256;
                public string define = "HEXIEGE_DETECT_RANGE_COMPARISON", codeProtocol = "host-battle-v2/r2-purpose";
                public string flag = "EnableHostBattleCapture", legacyIsolation = "Host flag takes precedence; legacy v1 flag/commands never arm v2.";
                public string sourceProof = "Runtime build GUID plus external installed APK/prebuild source-hash receipt required; protocol revision is not an exact source digest.";
                public string semantics = "Frame rows describe the preceding Update interval including waits. NCC rows have explicit current frame/start/end/wall ticks; not CPU self. Optional recorder values are latest completed samples, not assigned to that preceding interval. Rosters/target counts cached at1Hz; qualifying bins are sampled load observations, not continuous battle seconds or maximum-density proof. Paired kernels only. Normal TimeLimit saves after GameEnd/scene exit. Aborts close the window then save immediately outside capture and are not performance-acceptance evidence. No full Release Dev log or callstack guarantee.";
                public bool ready, requiresRestart, writeReadback, host, client, server, listening, gameStarted, probeComplete, started, completed, aborted, capacityClipped, loadEligible, loadCriteriaMet;
                public ulong networkObjectId, clientNetworkId;
                public string probeCommandId, startCommandId;
                public int mapVersion, remotePeers, aliveBlue, aliveRed, targetedUnits, minAlivePerTeam = 12, requiredLoadBins = 30, qualifyingLoadBins, frameRows, nccRows, pairRows, queryCount, trueQueries, falseQueries;
                public double realtimeMs, serverTime, damageAgeMs, windowStartMs, windowEndMs, durationSeconds = 90, saveMs;
                public long stopwatchFrequency = System.Diagnostics.Stopwatch.Frequency, damageEvents;
                public string purpose = "StrictBattle";
                public bool renderStartEligible, technicalComplete, preparationGpuObserved;
                public int timingPositiveGpuRows;
                public bool bloomExploration, bloomRestored, bloomExternalProfileChange, bloomSupportReady, bloomCompletedCycle;
                public string bloomSupportStatus = "NotChecked-NeedGame";
                public string bloomStatus = "NotRequested", bloomPhaseSha256, bloomAHash, bloomBHash;
                public int bloomPhaseRows;
                public bool bloomAResolvedKnown;
                public float bloomExpectedAResolvedIntensity;
                public string bloomSemantics = "ABBA4x8s maximum; natural early end is partial. Read-time groups only: GPU source phase Unknown. First2s of each read phase excluded heuristically, not a bound on GPU delay. Resolved Bloom is observed stack state and may lag render. No causal owner or improvement-percent verdict.";
                public MetricReport[] metrics;
                public RenderEnvironment environment, startEnvironment, endEnvironment;
                public string timingSchema = "frame-timing-observations-v1", timingStatus = "NotProbed", timingClockStatus = "Unavailable-NotProbed", timingsSha256;
                public string timingSemantics = "Raw FrameTiming CPU-clock identity with delayed read frame; never assigned to current cadence. Durations ms, not CPU self. CPU frequency zero means clock conversion unavailable even with raw timestamps. Timestamp frequency is FrameTiming CPU timer, not Stopwatch/realtime epoch. Zero fields are not proof of zero work. No post-window drain; delayed head/tail may be outside/missing. Optional observation overhead is not subtracted.";
                public bool timingFeatureEnabled, timingCapacityClipped;
                public ulong timingCpuFrequency;
                public int timingRows, timingInvalidIdentity, timingDuplicateReads, timingNoDataReads, timingReadBatchCapacity = 16, timingCapacity = 8192;

            }
            [Serializable] private sealed class RenderEnvironment
            {
                public string status = "Unavailable", graphicsApi, graphicsDevice, quality, pipeline, renderer;
                public int screenWidth, screenHeight, qualityIndex, targetFrameRate, vSyncCount, cameraCount, cameraPixelWidth, cameraPixelHeight, cameraStackCount;
                public double refreshHz, observedRealtimeMs;
                public float renderScale, dynamicWidthScale, dynamicHeightScale, orthographicSize;
                public bool frameTimingEnabled, cameraFound, orthographic, hdr, msaa, dynamicResolution, postProcessing, renderShadows;
            }
            private static RenderEnvironment ReadRenderEnvironment()
            {
                var e = new RenderEnvironment();
                try
                {
                    e.graphicsApi = SystemInfo.graphicsDeviceType.ToString(); e.graphicsDevice = SystemInfo.graphicsDeviceName;
                    e.qualityIndex = QualitySettings.GetQualityLevel(); var names = QualitySettings.names;
                    e.quality = e.qualityIndex >= 0 && e.qualityIndex < names.Length ? names[e.qualityIndex] : "Unknown";
                    e.screenWidth = Screen.width; e.screenHeight = Screen.height; e.refreshHz = Screen.currentResolution.refreshRateRatio.value;
                    e.targetFrameRate = UnityEngine.Application.targetFrameRate; e.vSyncCount = QualitySettings.vSyncCount;
                    e.frameTimingEnabled = FrameTimingManager.IsFeatureEnabled(); e.observedRealtimeMs = Time.realtimeSinceStartupAsDouble * 1000;
                    e.dynamicWidthScale = ScalableBufferManager.widthScaleFactor; e.dynamicHeightScale = ScalableBufferManager.heightScaleFactor;
                    var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                    e.pipeline = pipeline != null ? pipeline.name : "BuiltIn";
                    if (pipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp) e.renderScale = urp.renderScale;
                    e.cameraCount = Camera.allCamerasCount; var camera = Camera.main; e.cameraFound = camera != null;
                    if (camera != null)
                    {
                        e.cameraPixelWidth = camera.pixelWidth; e.cameraPixelHeight = camera.pixelHeight;
                        e.orthographic = camera.orthographic; e.orthographicSize = camera.orthographicSize;
                        e.hdr = camera.allowHDR; e.msaa = camera.allowMSAA; e.dynamicResolution = camera.allowDynamicResolution;
                        if (camera.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(out var data))
                        { e.postProcessing = data.renderPostProcessing; e.renderShadows = data.renderShadows; e.cameraStackCount = data.cameraStack?.Count ?? 0; e.renderer = data.scriptableRenderer?.GetType().Name; }
                    }
                    e.status = "ObservedSnapshotNotContinuous";
                }
                catch { e.status = "UnavailableOrPartial-ReadFault"; }
                return e;
            }
            private struct TimingObservation { internal int readFrame, validMask, readPhase; internal bool excludedTransition, resolvedKnown; internal float resolvedIntensity, cameraSize, cameraX, cameraZ; internal double phaseAgeMs; internal double readRealtimeMs; internal FrameTiming timing; }
            private readonly TimingObservation[] _timingRows = new TimingObservation[8192];
            private readonly FrameTiming[] _timingReadBuffer = new FrameTiming[16];
            private readonly System.Collections.Generic.HashSet<ulong> _timingSeen = new System.Collections.Generic.HashSet<ulong>(8192);
            private int _timingCount, _environmentScene = int.MinValue;
            private bool _timingFaulted;
            private void ProbeTiming()
            {
                try
                {
                    _report.timingFeatureEnabled = FrameTimingManager.IsFeatureEnabled();
                    _report.timingStatus = _report.timingFeatureEnabled ? "Enabled-NoDistinctSampleYet" : "Unavailable-FeatureDisabled";
                    if (_report.timingFeatureEnabled) _report.timingCpuFrequency = FrameTimingManager.GetCpuTimerFrequency();
                    _report.timingClockStatus = _report.timingCpuFrequency > 0 ? "RawCpuFrequencyAvailable-EpochUnmapped" : "Unavailable-ZeroCpuFrequency";
                }
                catch { _timingFaulted = true; _report.timingStatus = "Unavailable-ProbeFault"; }
            }
            private static bool FiniteNonnegative(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
            private void ObserveTiming(bool retain)
            {
                if (!_report.timingFeatureEnabled || _timingFaulted || _report.timingCapacityClipped) return;
                try
                {
                    uint count = FrameTimingManager.GetLatestTimings(16, _timingReadBuffer);
                    if (count > 16) { _timingFaulted = true; _report.timingStatus = "Unavailable-InvalidBatchCount"; return; }
                    if (count == 0 && retain) _report.timingNoDataReads++;
                    for (int i = 0; i < count; i++)
                    {
                        var t = _timingReadBuffer[i];
                        if (!retain)
                        {
                            if (t.frameStartTimestamp != 0 && FiniteNonnegative(t.gpuFrameTime) && t.gpuFrameTime > 0)
                                _report.preparationGpuObserved = true;
                            continue;
                        }
                        if (t.frameStartTimestamp == 0) { _report.timingInvalidIdentity++; continue; }
                        if (_timingSeen.Contains(t.frameStartTimestamp)) { _report.timingDuplicateReads++; continue; }
                        if (_timingCount >= _timingRows.Length) { _report.timingCapacityClipped = true; _report.timingStatus = "Incomplete-Capacity"; break; }
                        _timingSeen.Add(t.frameStartTimestamp);
                        int mask = 0;
                        if (FiniteNonnegative(t.cpuFrameTime) && t.cpuFrameTime > 0) mask |= 1;
                        if (FiniteNonnegative(t.cpuMainThreadFrameTime) && t.cpuMainThreadFrameTime > 0) mask |= 2;
                        if (FiniteNonnegative(t.cpuRenderThreadFrameTime) && t.cpuRenderThreadFrameTime > 0) mask |= 4;
                        if (FiniteNonnegative(t.cpuMainThreadPresentWaitTime)) mask |= 8;
                        if (FiniteNonnegative(t.gpuFrameTime) && t.gpuFrameTime > 0) { mask |= 16; _report.timingPositiveGpuRows++; }
                        var observation = new TimingObservation { readFrame = Time.frameCount, readRealtimeMs = Time.realtimeSinceStartupAsDouble * 1000, timing = t, validMask = mask };
                        AddBloomReadContext(ref observation, Time.realtimeSinceStartupAsDouble); _timingRows[_timingCount++] = observation;
                        _report.timingStatus = "ObservedDistinctTimestamp-DelayedUnmapped";
                    }
                    FrameTimingManager.CaptureFrameTimings();
                }
                catch { _timingFaulted = true; _report.timingStatus = "Incomplete-ReadFault"; }
            }
            private void SaveTimings()
            {
                var text = new StringBuilder("readUpdateFrame,readRealtimeMs,frameStartCpuTimestamp,firstSubmitCpuTimestamp,presentCalledCpuTimestamp,frameCompleteCpuTimestamp,cpuFrameMs,cpuMainMs,cpuRenderMs,cpuPresentWaitMs,gpuMs,validMask,widthScale,heightScale,syncInterval,sourceGpuPhase,readPhase,phaseAgeMs,excludedTransition,resolvedBloomKnown,resolvedBloomIntensity,cameraOrthoSize,cameraX,cameraZ\n");
                for (int i = 0; i < _timingCount; i++)
                {
                    var row = _timingRows[i]; var t = row.timing;
                    text.AppendFormat(CultureInfo.InvariantCulture, "{0},{1:R},{2},{3},{4},{5},{6:R},{7:R},{8:R},{9:R},{10:R},{11},{12:R},{13:R},{14},Unknown,{15},{16:R},{17},{18},{19:R},{20:R},{21:R},{22:R}\n", row.readFrame, row.readRealtimeMs, t.frameStartTimestamp, t.firstSubmitTimestamp, t.cpuTimePresentCalled, t.cpuTimeFrameComplete, t.cpuFrameTime, t.cpuMainThreadFrameTime, t.cpuRenderThreadFrameTime, t.cpuMainThreadPresentWaitTime, t.gpuFrameTime, row.validMask, t.widthScale, t.heightScale, t.syncInterval, row.readPhase, row.phaseAgeMs, row.excludedTransition ? 1 : 0, row.resolvedKnown ? 1 : 0, row.resolvedIntensity, row.cameraSize, row.cameraX, row.cameraZ);
                }
                text.Append("# END,rows=").Append(_timingCount).Append('\n');
                _owner.WriteNew("frame-timings.csv", text.ToString()); _report.timingRows = _timingCount; _report.timingsSha256 = _owner.Hash("frame-timings.csv");
            }
            private struct BloomPhase { internal int phase, frame, blue, red; internal long damage; internal double realtimeMs; internal float cameraSize, cameraX, cameraZ; }
            private readonly BloomPhase[] _bloomPhases = new BloomPhase[5];
            private int _bloomPhaseCount, _bloomReadPhase = -1;
            private double _bloomPhaseStarted;
            private Camera _bloomCamera;
            private UnityEngine.Rendering.Universal.UniversalAdditionalCameraData _bloomCameraData;
            private UnityEngine.Rendering.Volume _bloomVolume;
            private UnityEngine.Rendering.VolumeProfile _bloomPrevious, _bloomShared, _bloomClone;
            private UnityEngine.Rendering.Universal.Bloom _bloomComponent, _bloomOriginal;
            private float _bloomOriginalValue;
            private bool _bloomOriginalOverride, _bloomOriginalActive, _bloomHadInstance, _bloomAssigned, _bloomReachedScheduledEnd;
            private static string TextHash(string text)
            { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", ""); }
            private UnityEngine.Rendering.VolumeProfile InspectBloomTarget()
            {
                _bloomCamera = Camera.main;
                if (_bloomCamera == null || Camera.allCamerasCount != 1 || !_bloomCamera.TryGetComponent(out _bloomCameraData) || !_bloomCameraData.renderPostProcessing) throw new InvalidOperationException();
                var volumes = UnityEngine.Rendering.VolumeManager.instance.GetVolumes(_bloomCameraData.volumeLayerMask);
                int active = 0;
                foreach (var volume in volumes)
                    if (volume != null && volume.isActiveAndEnabled && volume.weight > 0) { active++; _bloomVolume = volume; }
                if (active != 1 || !_bloomVolume.isGlobal || _bloomVolume.weight != 1) throw new InvalidOperationException();
                _bloomHadInstance = _bloomVolume.HasInstantiatedProfile();
                _bloomPrevious = _bloomHadInstance ? _bloomVolume.profile : null; _bloomShared = _bloomVolume.sharedProfile;
                var original = _bloomHadInstance ? _bloomPrevious : _bloomShared;
                if (original == null || !original.TryGet(out _bloomOriginal) || !_bloomOriginal.active || _bloomOriginal.intensity.value <= 0) throw new InvalidOperationException();
                return original;
            }
            private void CheckBloomSupport()
            {
                try { InspectBloomTarget(); _report.bloomSupportReady = true; _report.bloomSupportStatus = "Ready-CurrentGameSnapshot-StartRevalidates"; }
                catch { _report.bloomSupportReady = false; _report.bloomSupportStatus = "Unavailable-NeedSingleCameraSingleGlobalVolumeActiveBloom"; }
            }
            private bool PrepareBloom()
            {
                try
                {
                    _report.bloomExploration = true; _report.bloomStatus = "Preparing";
                    var original = InspectBloomTarget();
                    var originalStack = _bloomCameraData.volumeStack ?? UnityEngine.Rendering.VolumeManager.instance.stack;
                    var originalResolved = originalStack?.GetComponent<UnityEngine.Rendering.Universal.Bloom>();
                    _report.bloomAResolvedKnown = originalResolved != null;
                    _report.bloomExpectedAResolvedIntensity = originalResolved != null && originalResolved.active ? originalResolved.intensity.value : 0;
                    _bloomOriginalValue = _bloomOriginal.intensity.value; _bloomOriginalOverride = _bloomOriginal.intensity.overrideState; _bloomOriginalActive = _bloomOriginal.active;
                    // 프로필뿐 아니라 컴포넌트까지 복제해 공유 자산과 기존 runtime 인스턴스를 건드리지 않는다.
                    _bloomClone = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
                    foreach (var component in original.components) _bloomClone.components.Add(UnityEngine.Object.Instantiate(component));
                    if (!_bloomClone.TryGet(out _bloomComponent)) throw new InvalidOperationException();
                    _report.bloomAHash = TextHash(JsonUtility.ToJson(_bloomClone, false) + JsonUtility.ToJson(_bloomComponent, false));
                    _bloomComponent.intensity.overrideState = true; _bloomComponent.intensity.value = 0;
                    _report.bloomBHash = TextHash(JsonUtility.ToJson(_bloomClone, false) + JsonUtility.ToJson(_bloomComponent, false));
                    _bloomComponent.intensity.overrideState = _bloomOriginalOverride; _bloomComponent.intensity.value = _bloomOriginalValue;
                    _bloomVolume.profile = _bloomClone; _bloomAssigned = true; _report.bloomStatus = "Prepared-ExploratoryOnly";
                    return true;
                }
                catch { _report.bloomStatus = "Unavailable-Preparation"; RestoreBloom(); return false; }
            }
            private void RecordBloomPhase(int phase, double now)
            {
                if (_bloomPhaseCount >= _bloomPhases.Length) return;
                var position = _bloomCamera != null ? _bloomCamera.transform.position : default;
                _bloomPhases[_bloomPhaseCount++] = new BloomPhase { phase = phase, frame = Time.frameCount, realtimeMs = now * 1000, blue = _report.aliveBlue, red = _report.aliveRed, damage = _damages,
                    cameraSize = _bloomCamera != null ? _bloomCamera.orthographicSize : 0, cameraX = position.x, cameraZ = position.z };
            }
            private void UpdateBloom(double now)
            {
                if (!_report.bloomExploration || !_bloomAssigned) return;
                try
                {
                    if (_bloomVolume == null || !_bloomVolume.HasInstantiatedProfile() || !ReferenceEquals(_bloomVolume.profile, _bloomClone) || !ReferenceEquals(_bloomVolume.sharedProfile, _bloomShared))
                    { _report.bloomExternalProfileChange = true; _report.bloomStatus = "Incomplete-ExternalProfileChanged"; RestoreBloom(); Stop("BloomExternalProfileChanged", now, true); return; }
                    int phase = Math.Min(4, (int)((now - _start) / 8));
                    // 프레임 정지로 구간을 건너뛰면 가짜 전이를 만들지 않는다. 실제 관측 전이만 남긴다.
                    if (phase == 4)
                    {
                        _bloomReachedScheduledEnd = true;
                        RestoreBloom(); Stop("BloomExplorationComplete", _start + 32, false); return;
                    }
                    if (phase != _bloomReadPhase)
                    {
                        _bloomReadPhase = phase; _bloomPhaseStarted = now;
                        bool off = phase == 1 || phase == 2;
                        _bloomComponent.intensity.overrideState = off || _bloomOriginalOverride;
                        _bloomComponent.intensity.value = off ? 0 : _bloomOriginalValue;
                        RecordBloomPhase(phase, now);
                    }
                }
                catch { _report.bloomStatus = "Incomplete-PhaseFault"; RestoreBloom(); Stop("BloomScopeFault", now, true); }
            }
            internal void RestoreBloom()
            {
                if (!_report.bloomExploration || _bloomClone == null) return;
                try
                {
                    if (_bloomAssigned)
                    {
                        bool owns = _bloomVolume != null && _bloomVolume.HasInstantiatedProfile() && ReferenceEquals(_bloomVolume.profile, _bloomClone);
                        if (owns)
                        {
                            _bloomVolume.profile = _bloomPrevious;
                            _report.bloomRestored = _bloomVolume.HasInstantiatedProfile() == _bloomHadInstance && (!_bloomHadInstance || ReferenceEquals(_bloomVolume.profile, _bloomPrevious))
                                && _bloomOriginal != null && _bloomOriginal.intensity.value == _bloomOriginalValue && _bloomOriginal.intensity.overrideState == _bloomOriginalOverride && _bloomOriginal.active == _bloomOriginalActive;
                        }
                        else { _report.bloomRestored = false; _report.bloomExternalProfileChange = true; }
                        _report.bloomCompletedCycle = _bloomReachedScheduledEnd && Time.realtimeSinceStartupAsDouble - _start >= 32 && !_ended && _capturing && _bloomPhaseCount == 4 && _bloomPhases[0].phase == 0 && _bloomPhases[1].phase == 1 && _bloomPhases[2].phase == 2 && _bloomPhases[3].phase == 3;
                        RecordBloomPhase(4, Time.realtimeSinceStartupAsDouble);
                        for (int i = 0; _report.bloomCompletedCycle && i < 4; i++)
                            if (_bloomPhases[i + 1].realtimeMs - _bloomPhases[i].realtimeMs < 2000) _report.bloomCompletedCycle = false;
                        _bloomReadPhase = 4;
                    }
                }
                catch { _report.bloomRestored = false; _report.bloomStatus = "Incomplete-RestoreFault"; }
                finally
                {
                    _bloomAssigned = false;
                    // 소유한 복제 컴포넌트만 정리한다. 외부 프로필로 교체됐으면 원참조를 덮어쓰지 않는다.
                    if (_bloomClone != null)
                    { foreach (var component in _bloomClone.components) if (component != null) try { UnityEngine.Object.Destroy(component); } catch { _report.bloomStatus = "Incomplete-CloneCleanupFault"; }
                        try { UnityEngine.Object.Destroy(_bloomClone); } catch { _report.bloomStatus = "Incomplete-CloneCleanupFault"; } }
                    _bloomClone = null;
                }
            }
            private void AddBloomReadContext(ref TimingObservation row, double now)
            {
                row.readPhase = -1; row.phaseAgeMs = -1; row.excludedTransition = true;
                if (!_report.bloomExploration) return;
                row.readPhase = _bloomReadPhase; row.phaseAgeMs = (now - _bloomPhaseStarted) * 1000;
                row.excludedTransition = row.readPhase < 0 || row.readPhase > 3 || row.phaseAgeMs < 2000;
                if (_bloomCamera != null) { var position = _bloomCamera.transform.position; row.cameraSize = _bloomCamera.orthographicSize; row.cameraX = position.x; row.cameraZ = position.z; }
                try
                {
                    var stack = _bloomCameraData != null ? _bloomCameraData.volumeStack : null;
                    if (stack == null && Camera.allCamerasCount == 1) stack = UnityEngine.Rendering.VolumeManager.instance.stack;
                    var bloom = stack?.GetComponent<UnityEngine.Rendering.Universal.Bloom>();
                    if (bloom != null) { row.resolvedKnown = true; row.resolvedIntensity = bloom.active ? bloom.intensity.value : 0; }
                }
                catch { row.resolvedKnown = false; }
            }
            private void SaveBloomPhases()
            {
                var text = new StringBuilder("phase,eventFrame,eventRealtimeMs,aliveBlueCached1Hz,aliveRedCached1Hz,damageEvents,cameraOrthoSize,cameraX,cameraZ\n");
                for (int i = 0; i < _bloomPhaseCount; i++) { var row = _bloomPhases[i]; text.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2:R},{3},{4},{5},{6:R},{7:R},{8:R}\n", row.phase, row.frame, row.realtimeMs, row.blue, row.red, row.damage, row.cameraSize, row.cameraX, row.cameraZ); }
                text.Append("# END,rows=").Append(_bloomPhaseCount).Append('\n'); _owner.WriteNew("bloom-phases.csv", text.ToString()); _report.bloomPhaseRows = _bloomPhaseCount; _report.bloomPhaseSha256 = _owner.Hash("bloom-phases.csv");
            }
            [Serializable] private sealed class Command
            { public string protocol, action, commandId, captureToken, readyNonce, readySha256, buildGuid, mapHash, purpose; public ulong networkObjectId, clientNetworkId; public int mapVersion; }
            [Serializable] private sealed class Ack
            { public string protocol = "host-battle-v2", commandId, captureToken, readyNonce, reason, utc, purpose; public bool accepted, startsCapture, requiresRestart; public double realtimeMs, serverTime; }
            private struct FrameRow { internal int frame, kind, mask, blue, red, targets, collections; internal long damage; internal double startMs, endMs, serverTime; }
            private struct TickRow { internal int frame, targets; internal long ticks; internal double startMs, endMs; }
            private sealed class Metric { internal MetricReport report; internal ProfilerRecorder recorder; }
            private readonly DetectRangePerformanceDiagnostics _owner;
            private readonly string _root;
            private readonly FrameRow[] _frames = new FrameRow[FrameCapacity];
            private readonly TickRow[] _ticks = new TickRow[TickCapacity];
            private readonly long[] _values = new long[FrameCapacity * MetricCount];
            private readonly Metric[] _metrics = new Metric[MetricCount];
            private HostReport _report;
            private NetworkCombatController _ncc;
            private IGameServices _services;
            private MapPreparationResult _map;
            private IDisposable _damageSubscription, _endSubscription;
            private bool _capturing, _started, _stopped, _saved, _ended, _boundary, _disposed;
            private int _frameCount, _tickCount, _pairCount, _queries, _true, _scene, _loadBins;
            private long _damages, _damageAtBin;
            private double _created, _nextPoll, _lastRoster, _lastDamage = double.NegativeInfinity, _start, _end, _previous;
            private string _lastCommand;
            private string _probedToken, _probedPurpose;
            private static bool TryPurpose(string value, out string purpose)
            {
                purpose = string.IsNullOrEmpty(value) ? "StrictBattle" : value;
                if (purpose == "StrictBattle" || purpose == "RenderDiagnostic" || purpose == "RenderBloomExploration") return true;
                purpose = "Invalid"; return false;
            }

            internal HostBattleSession(DetectRangePerformanceDiagnostics owner, string root) { _owner = owner; _root = root; }
            internal void FailedStartup(string type)
            {
                try
                {
                    if (_report == null) _report = new HostReport();
                    _report.ready = false; _report.requiresRestart = true; _report.reason = "StartupUnavailable-" + type;
                    if (_owner._directory != null) WriteReplace("readiness.json", JsonUtility.ToJson(_report, true));
                }
                catch { /* 저장소 자체가 고장 나면 readiness 부재가 실패 근거다. */ }
            }
            internal void Initialize()
            {
                _created = Time.realtimeSinceStartupAsDouble;
                _report = new HostReport { readyNonce = Guid.NewGuid().ToString("N"), buildGuid = UnityEngine.Application.buildGUID,
                    unity = UnityEngine.Application.unityVersion, version = UnityEngine.Application.version,
                    utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), reason = "WaitingForHost",
#if ENABLE_IL2CPP
                    backend = "IL2CPP"
#else
                    backend = "Mono"
#endif
                };
                _owner._directory = Path.Combine(_root, "host-battle-v2-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + _report.readyNonce);
                Directory.CreateDirectory(_owner._directory);
                _owner.WriteNew("write-readback-probe.txt", _report.readyNonce);
                _report.writeReadback = File.ReadAllText(Path.Combine(_owner._directory, "write-readback-probe.txt")) == _report.readyNonce;
                if (!_report.writeReadback) throw new IOException("ProbeMismatch");
                InitializeMetrics();
                ProbeTiming(); _report.environment = ReadRenderEnvironment();
                _owner.WriteNew("ready.json", JsonUtility.ToJson(_report, true));
                _report.readySha256 = _owner.Hash("ready.json");
                _damageSubscription = GameEvents.OnEntityDamaged.Subscribe(_ => { if (!_stopped) { _lastDamage = Time.realtimeSinceStartupAsDouble; _damages++; } });
                _endSubscription = GameEvents.OnGameEnd.Subscribe(_ => { _ended = true; if (_capturing) Stop("GameEnd", Time.realtimeSinceStartupAsDouble, true); });
                _owner._observer = _owner.Observe;
                UnitCombatUseCase.DetectComparisonObserver = _owner._observer;
            }
            private void InitializeMetrics()
            {
                string[][] names = { new[] { "CPU Main Thread Frame Time", "Main Thread" }, new[] { "CPU Render Thread Frame Time", "Render Thread" }, new[] { "GPU Frame Time" }, new[] { "GC Used Memory" }, new[] { "GC Reserved Memory" }, new[] { "GC Allocated In Frame" }, new[] { "GC.Collect" } };
                var handles = new System.Collections.Generic.List<ProfilerRecorderHandle>();
                try { ProfilerRecorderHandle.GetAvailable(handles); } catch { }
                _report.metrics = new MetricReport[MetricCount];
                for (int i = 0; i < MetricCount; i++)
                {
                    var metric = new Metric { report = new MetricReport { requested = names[i][0], status = "Unavailable-NoDescription" } };
                    _metrics[i] = metric; _report.metrics[i] = metric.report;
                    foreach (string name in names[i])
                    {
                        if (metric.report.actual != null) break;
                        foreach (var handle in handles)
                        {
                            try
                            {
                                var d = ProfilerRecorderHandle.GetDescription(handle);
                                if (d.Name != name) continue;
                                bool bytes = i >= 3 && i <= 5;
                                if (bytes ? d.UnitType != ProfilerMarkerDataUnit.Bytes : d.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds) continue;
                                metric.report.actual = d.Name; metric.report.category = d.Category.Name; metric.report.unit = d.UnitType.ToString();
                                metric.recorder = ProfilerRecorder.StartNew(d.Category, d.Name, 1);
                                metric.report.status = "DescriptionFound-RuntimeProbePending"; break;
                            }
                            catch { metric.report.status = "Unavailable-RecorderFault"; }
                        }
                    }
                }
            }
            private bool ReadMetric(int index, out long value)
            {
                value = 0;
                try { var r = _metrics[index].recorder; if (r.Valid && r.IsRunning && r.Count > 0) { value = r.LastValue; return true; } } catch { }
                return false;
            }
            private void PollReadiness(double now)
            {
                var nm = NetworkManager.Singleton;
                _report.host = nm != null && nm.IsHost; _report.server = nm != null && nm.IsServer;
                _report.client = nm != null && nm.IsClient; _report.listening = nm != null && nm.IsListening;
                _report.remotePeers = nm != null && nm.IsServer ? Math.Max(0, nm.ConnectedClientsIds.Count - 1) : 0;
                _services = GameServicesLocator.Current;
                _map = (_services as IReadOnlyMapPreparationEvidence)?.GetLastMapPreparation();
                _ncc = UnityEngine.Object.FindFirstObjectByType<NetworkCombatController>();
                _report.gameStarted = _services != null && _services.IsNetworkGameStarted;
                _report.ready = !_ended && _report.host && _report.server && _report.client && _report.listening && _report.remotePeers >= 1 && _report.gameStarted && _map != null && _ncc != null && _ncc.IsSpawned && ReferenceEquals(_ncc.NetworkManager, nm);
                _report.reason = _report.ready ? "ReadyForProbeAndArmyPreparation" : "ExpectedConnectedHostAndGameIdentity";
                if (_report.ready) { _report.mapHash = _map.HashHex; _report.mapVersion = _map.MapVersion; _report.networkObjectId = _ncc.NetworkObjectId; }
                int environmentScene = SceneManager.GetActiveScene().handle;
                if (_environmentScene != environmentScene || _report.ready && !_report.environment.cameraFound)
                { _environmentScene = environmentScene; _report.environment = ReadRenderEnvironment(); }
                ObserveTiming(false);
                _report.probeComplete = now - _created >= 1;
                for (int i = 0; i < MetricCount; i++)
                    if (_metrics[i].report.actual != null && _report.probeComplete)
                        _metrics[i].report.status = ReadMetric(i, out _) ? "ObservedLatestCompletedSample" : "Unavailable-NoRuntimeSamples";
                UpdateRoster(now);
                _report.observedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                _report.realtimeMs = now * 1000; _report.serverTime = nm != null && nm.IsListening ? nm.ServerTime.Time : 0;
                _report.damageAgeMs = double.IsInfinity(_lastDamage) ? -1 : (now - _lastDamage) * 1000;
                _report.loadEligible = _report.ready && _report.aliveBlue >= 12 && _report.aliveRed >= 12 && now - _lastDamage <= 5;
                if (_report.ready) CheckBloomSupport();
                _report.renderStartEligible = _report.ready && _report.aliveBlue >= 1 && _report.aliveRed >= 1 && now - _lastDamage <= 5;
                WriteReplace("readiness.json", JsonUtility.ToJson(_report, true));
            }
            private void UpdateRoster(double now)
            {
                if (now - _lastRoster < 1) return;
                _lastRoster = now; int blue = 0, red = 0;
                var spawn = _services?.GetUnitSpawn();
                if (spawn != null) foreach (var unit in spawn.Units.Values) if (unit.IsAlive) { if (unit.Team == TeamId.Blue) blue++; else if (unit.Team == TeamId.Red) red++; }
                _report.aliveBlue = blue; _report.aliveRed = red;
                _report.targetedUnits = _ncc != null && _ncc.IsSpawned ? _ncc.HostMeasurementTargetCount : 0;
                if (_capturing && blue >= 12 && red >= 12 && _damages > _damageAtBin) _loadBins++;
                _damageAtBin = _damages;
            }
            private void PollCommand(double now)
            {
                string path = Path.Combine(_owner._directory, "start-command.json");
                if (!File.Exists(path) || new FileInfo(path).Length > 4096) return;
                Command command;
                try { command = JsonUtility.FromJson<Command>(File.ReadAllText(path)); } catch { return; }
                if (command == null || !Guid.TryParseExact(command.commandId, "N", out _) || command.commandId == _lastCommand) return;
                _lastCommand = command.commandId;
                var ack = new Ack { commandId = command.commandId, captureToken = command.captureToken, readyNonce = _report.readyNonce,
                    realtimeMs = now * 1000, serverTime = _report.serverTime, utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), reason = "IdentityOrRoleNotReady" };
                bool purposeValid = TryPurpose(command.purpose, out string purpose);
                ack.purpose = purpose;
                if (!purposeValid) ack.reason = "UnsupportedPurpose";
                bool identity = purposeValid && command.protocol == "host-battle-v2" && Guid.TryParseExact(command.captureToken, "N", out _) && command.readyNonce == _report.readyNonce && command.readySha256 == _report.readySha256
                    && command.buildGuid == _report.buildGuid && string.Equals(command.mapHash, _report.mapHash, StringComparison.OrdinalIgnoreCase) && command.mapVersion == _report.mapVersion && command.networkObjectId == _report.networkObjectId && ConnectedPeer(command.clientNetworkId) && _report.ready && _report.probeComplete;
                if (identity && command.action == "probe" && (purpose != "RenderBloomExploration" || _report.bloomSupportReady))
                {
                    ack.accepted = EmitAnchor("Host battle probe anchor", command.captureToken, command.commandId);
                    ack.reason = ack.accepted ? "ProbeReadWriteReady" : "ProbeConsoleUnavailable";
                    _probedToken = ack.accepted ? command.captureToken : null;
                    _probedPurpose = ack.accepted ? purpose : null;
                    _report.clientNetworkId = command.clientNetworkId; _report.probeCommandId = command.commandId;
                }
                else if (identity && command.action == "probe") ack.reason = "BloomScopeUnavailable";
                if (identity && command.action == "start" && command.captureToken == _probedToken && command.clientNetworkId == _report.clientNetworkId && purpose == _probedPurpose
                    && (purpose == "StrictBattle" ? _report.loadEligible : _report.renderStartEligible))
                { ack.accepted = true; ack.startsCapture = true; ack.reason = "StartingAfterAckFlush"; }
                else if (identity && command.action == "start") ack.reason = purpose == "StrictBattle" ? "MatchingPurposeProbeAndStrictLoadRequired" : "MatchingPurposeProbeAndRecentBattleRequired";
                if (ack.startsCapture && purpose == "RenderBloomExploration" && !PrepareBloom())
                { ack.accepted = false; ack.startsCapture = false; ack.reason = "BloomScopeUnavailable"; }
                WriteReplace("start-ack.json", JsonUtility.ToJson(ack, true));
                if (!ack.startsCapture) return;
                // ACK 쓰기/flush 뒤에만 측정 창을 연다. 같은 명령은 다시 소비하지 않는다.
                _report.purpose = purpose;
                _report.captureToken = command.captureToken; _report.startCommandId = command.commandId;
                _report.startEnvironment = ReadRenderEnvironment();
                _start = _previous = Time.realtimeSinceStartupAsDouble;
                _scene = SceneManager.GetActiveScene().handle; _started = _capturing = true;
                _report.started = true; _report.windowStartMs = _start * 1000; _damages = _damageAtBin = 0;
                UpdateBloom(_start);
            }
            private bool ConnectedPeer(ulong id)
            {
                var nm = NetworkManager.Singleton;
                if (nm == null || !nm.IsHost || id == nm.LocalClientId) return false;
                foreach (ulong peer in nm.ConnectedClientsIds) if (peer == id) return true;
                return false;
            }
            internal void Observe(UnitCombatUseCase combat, UnitData unit, Vector3 candidate, bool useOverride, bool live)
            {
                if (!_capturing) return;
                if (!ReferenceEquals(combat, _services?.GetCombatUseCase())) { Stop("CombatOwnerChanged", Time.realtimeSinceStartupAsDouble, true); return; }
                if (Time.realtimeSinceStartupAsDouble - _start >= 90) return;
                _queries++; if (live) _true++;
                if (_pairCount >= Capacity) return;
                if ((_queries - 1) % Stride != 0) return;
                var sample = combat.MeasureDetectComparison(unit, candidate, useOverride, live, (_pairCount & 1) != 0, _owner._snapshot);
                _owner._rows[_pairCount++] = new Row { frame = Time.frameCount, candidate = useOverride, realtimeMs = Time.realtimeSinceStartupAsDouble * 1000, sample = sample };
            }
            internal HostNccTicket BeginTick(NetworkCombatController source)
            {
                if (!_capturing) return default;
                if (source != _ncc) { Stop("NccReferenceChanged", Time.realtimeSinceStartupAsDouble, true); return default; }
                if (!source.IsSpawned || Time.realtimeSinceStartupAsDouble - _start >= 90) return default;
                return new HostNccTicket { frame = Time.frameCount, ticks = System.Diagnostics.Stopwatch.GetTimestamp(), realtimeMs = Time.realtimeSinceStartupAsDouble * 1000 };
            }
            internal void EndTick(NetworkCombatController source, HostNccTicket ticket)
            {
                long end = System.Diagnostics.Stopwatch.GetTimestamp();
                if (!_started || _saved || source != _ncc || (_stopped && ticket.realtimeMs > _end * 1000)) return;
                if (_tickCount >= TickCapacity) { _report.capacityClipped = true; Stop("NccCapacity", Time.realtimeSinceStartupAsDouble, true); return; }
                _ticks[_tickCount++] = new TickRow { frame = ticket.frame, startMs = ticket.realtimeMs, endMs = Time.realtimeSinceStartupAsDouble * 1000, ticks = end - ticket.ticks, targets = source.HostMeasurementTargetCount };
            }
            private void Stop(string reason, double now, bool aborted)
            {
                if (_stopped) return;
                _capturing = false; _stopped = true; _boundary = true; _end = now;
                RestoreBloom();
                _report.reason = reason; _report.aborted = aborted; _report.windowEndMs = now * 1000;
            }
            internal void Update()
            {
                if (_saved || _disposed) return;
                double now = Time.realtimeSinceStartupAsDouble;
                try
                {
                    if (!_started)
                    {
                        if (_ended) { Stop("NotStarted-GameEnd", now, true); _report.requiresRestart = true; Save(); return; }
                        // 대기 시간만으로 관측자를 폐기하지 않는다. 실제 경기와 수동 명령의 수명으로 제한한다.
                        if (now >= _nextPoll) { _nextPoll = now + 1; PollReadiness(now); PollCommand(now); }
                        return;
                    }
                    var nm = NetworkManager.Singleton;
                    bool sceneChanged = SceneManager.GetActiveScene().handle != _scene;
                    if (_stopped && _report.aborted) { _boundary = false; Save(); return; }
                    if (_capturing)
                    {
                        if (nm == null || !nm.IsHost || !nm.IsServer || !nm.IsClient || !nm.IsListening || nm.ConnectedClientsIds.Count < 2 || !ConnectedPeer(_report.clientNetworkId)) Stop("DisconnectedOrRoleChanged", now, true);
                        else if (sceneChanged) Stop("SceneChanged", now, true);
                        else if (!ReferenceEquals(GameServicesLocator.Current, _services) || _ncc == null || !_ncc.IsSpawned || _ncc.NetworkObjectId != _report.networkObjectId || !ReferenceEquals((_services as IReadOnlyMapPreparationEvidence)?.GetLastMapPreparation(), _map)) Stop("GameIdentityChanged", now, true);
                        else if (now - _start >= 90) Stop("TimeLimit", _start + 90, false);
                    }
                    if (_capturing || _boundary)
                    {
                        if (_capturing) { UpdateBloom(now); if (_capturing) ObserveTiming(true); }
                        UpdateRoster(now);
                        if (_frameCount >= FrameCapacity) { _report.capacityClipped = true; Stop("FrameCapacity", now, true); _boundary = false; }
                        else
                        {
                            var row = new FrameRow { frame = Time.frameCount, kind = _boundary ? 2 : _frameCount == 0 ? 0 : 1, startMs = _previous * 1000, endMs = now * 1000,
                                blue = _report.aliveBlue, red = _report.aliveRed, targets = _report.targetedUnits, damage = _damages, collections = GC.CollectionCount(0), serverTime = nm != null && nm.IsListening ? nm.ServerTime.Time : 0 };
                            for (int i = 0; i < MetricCount; i++) if (ReadMetric(i, out long value)) { row.mask |= 1 << i; _values[_frameCount * MetricCount + i] = value; }
                            _frames[_frameCount++] = row; _previous = now; _boundary = false;
                        }
                    }
                    // timeout 자체는 저장하지 않는다. 정상 종료/로비 전환 뒤에만 파일을 쓴다.
                    if (_stopped && (_report.aborted || _ended || sceneChanged)) Save();
                }
                catch
                {
                    Stop("ObserverFault", now, true);
                    if (_started) { _boundary = false; Save(); }
                    else { _report.requiresRestart = true; Dispose(); _owner.enabled = false; }
                }
            }
            private void Save()
            {
                if (_saved) return;
                _saved = true; long began = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    RestoreBloom();
                    _report.endEnvironment = ReadRenderEnvironment();
                    if (_report.bloomExploration) try { SaveBloomPhases(); } catch { _report.bloomPhaseSha256 = null; _report.bloomStatus = "Unavailable-PhaseArtifact"; }
                    // 추가 계측 실패 때문에 기존 전투 CSV까지 잃지 않는다.
                    try { SaveTimings(); }
                    catch { _report.timingsSha256 = null; _report.timingStatus = "Unavailable-ArtifactWriteOrHashFault"; }
                    _report.frameRows = _frameCount; _report.nccRows = _tickCount; _report.pairRows = _pairCount;
                    _report.queryCount = _queries; _report.trueQueries = _true; _report.falseQueries = _queries - _true;
                    _report.technicalComplete = _started && !_report.aborted && !_report.capacityClipped && _report.reason == "TimeLimit" && _frameCount > 0 && _tickCount > 0;
                    _report.damageEvents = _damages; _report.qualifyingLoadBins = _loadBins; _report.loadCriteriaMet = _loadBins >= 30 && _frameCount > 0 && _tickCount > 0 && !_report.capacityClipped && !_report.aborted;
                    var text = new StringBuilder("frame,kind,previousUpdateStartMs,readUpdateEndMs,serverTime,blueAlive,redAlive,activeTargetEntries,damageEventsCumulative,gcCollectionCount0,metricValidMask,mainRecorder,renderRecorder,gpuRecorder,gcUsedBytes,gcReservedBytes,gcAllocatedInFrameBytes,gcCollectRecorder\n");
                    for (int k = 0; k < _frameCount; k++)
                    {
                        var row = _frames[k]; text.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2:R},{3:R},{4:R},{5},{6},{7},{8},{9},{10}", row.frame, row.kind, row.startMs, row.endMs, row.serverTime, row.blue, row.red, row.targets, row.damage, row.collections, row.mask);
                        for (int i = 0; i < MetricCount; i++) text.Append(',').Append(_values[k * MetricCount + i]); text.Append('\n');
                    }
                    text.Append("# END,rows=").Append(_frameCount).Append('\n'); _owner.WriteNew("frames.csv", text.ToString());
                    text.Clear().Append("frame,nccStartRealtimeMs,nccEndRealtimeMs,wallTicks,activeTargetEntries\n");
                    for (int i = 0; i < _tickCount; i++) { var row = _ticks[i]; text.AppendFormat(CultureInfo.InvariantCulture, "{0},{1:R},{2:R},{3},{4}\n", row.frame, row.startMs, row.endMs, row.ticks, row.targets); }
                    text.Append("# END,rows=").Append(_tickCount).Append('\n'); _owner.WriteNew("ncc.csv", text.ToString());
                    text.Clear().Append("index,warmup,frame,candidate,realtimeMs,status,order,units,buildings,live,baseline,optimized,captureQueries,baselineQueries,optimizedQueries,baselineInspections,optimizedInspections,captureTicks,baselineTicks,optimizedTicks,snapshotOverflow\n");
                    for (int i = 0; i < _pairCount; i++) { var row = _owner._rows[i]; var s = row.sample; text.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4:R},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}\n", i, i < 2 ? 1 : 0, row.frame, row.candidate ? 1 : 0, row.realtimeMs, s.status, s.order, s.units, s.buildings, s.live ? 1 : 0, s.baseline ? 1 : 0, s.optimized ? 1 : 0, s.captureQueries, s.baselineQueries, s.optimizedQueries, s.baselineInspections, s.optimizedInspections, s.captureTicks, s.baselineTicks, s.optimizedTicks, s.snapshotOverflow ? 1 : 0); }
                    text.Append("# END,rows=").Append(_pairCount).Append('\n'); _owner.WriteNew("pairs.csv", text.ToString());
                    _report.framesSha256 = _owner.Hash("frames.csv"); _report.nccSha256 = _owner.Hash("ncc.csv"); _report.pairsSha256 = _owner.Hash("pairs.csv");
                    _report.completed = true; _report.saveMs = (System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    _owner.WriteNew("completed.json", JsonUtility.ToJson(_report, true));
                    _owner.WriteNew("END.json", "{\"protocol\":\"host-battle-v2\",\"completedSha256\":\"" + _owner.Hash("completed.json") + "\"}");
                    EmitAnchor("Host battle end anchor", _report.captureToken, _report.startCommandId);
                }
                catch { /* END가 없으면 회수 실패/부분 자료로만 판정한다. */ }
                finally { Dispose(); _owner.enabled = false; }
            }
            private void WriteReplace(string name, string text)
            {
                string path = Path.Combine(_owner._directory, name), temp = path + ".tmp";
                File.WriteAllText(temp, text, new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path); File.Move(temp, path);
            }
            private bool EmitAnchor(string message, string token, string command)
            {
                // 임시 측정 앵커는 창 밖에서만 출력한다. 운영 사건 키를 빌려 집계를 오염시키지 않는다.
                try { RuntimeLogger.Log(LogLevel.Info, "Performance", "HostBattleCapture", message,
                    "Capture=" + token + ", ReadyNonce=" + _report.readyNonce + ", Command=" + command); return true; }
                catch { return false; }
            }
            public void Dispose()
            {
                RestoreBloom();
                if (_disposed) return; _disposed = true; _capturing = false;
                _damageSubscription?.Dispose(); _endSubscription?.Dispose(); _damageSubscription = _endSubscription = null;
                if (UnitCombatUseCase.DetectComparisonObserver == _owner._observer) UnitCombatUseCase.DetectComparisonObserver = null;
                foreach (var metric in _metrics) if (metric != null) try { metric.recorder.Dispose(); } catch { }
            }
        }
        // 강제 종료 때 저장하지 않는다. 회수 시 END가 없으면 완전한 기록으로 수용하지 않는다.
    }
}
#endif
