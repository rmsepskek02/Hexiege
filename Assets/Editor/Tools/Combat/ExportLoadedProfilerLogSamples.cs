using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace Hexiege.EditorTools
{
    /// <summary>현재 선택한 저장 프레임의 로그 근거만 읽는다. 원시 출력은 Library 안에 보관한다.</summary>
    internal static class ExportLoadedProfilerLogSamples
    {
        private const int MaximumThreads = 256;
        private const int MaximumExportedSamples = 10000;

        [MenuItem("Hexiege/Combat/Diagnostics/Export Loaded Profiler Log Samples")]
        private static void Export() => ExportSamples(false);

        [MenuItem("Hexiege/Combat/Diagnostics/Export Loaded Profiler Object Samples")]
        private static void ExportObjects() => ExportSamples(true);

        private static string ExportSamples(bool objectSamples, int? explicitRawFrame = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || ProfilerDriver.enabled)
            {
                Debug.LogWarning("Profiler export requires stopped Play Mode and stopped recording.");
                return null;
            }
            Type windowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProfilerWindow");
            UnityEngine.Object[] windows = windowType == null ? Array.Empty<UnityEngine.Object>()
                : Resources.FindObjectsOfTypeAll(windowType);
            PropertyInfo selection = windowType?.GetProperty("selectedFrameIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (windows.Length != 1 || (!explicitRawFrame.HasValue && selection == null))
            {
                Debug.LogWarning("Open one Profiler window and select the saved frame before exporting.");
                return null;
            }
            long selected = explicitRawFrame.HasValue ? explicitRawFrame.Value
                : Convert.ToInt64(selection.GetValue(windows[0]), CultureInfo.InvariantCulture);
            if (selected < ProfilerDriver.firstFrameIndex || selected > ProfilerDriver.lastFrameIndex
                || selected < 0 || selected > int.MaxValue)
            {
                Debug.LogWarning("Profiler selected frame is outside the loaded recording.");
                return null;
            }
            int frame = (int)selected;
            string directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                "../Library/ProfilerDiagnostics"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, (objectSamples ? "object-samples-" : "log-samples-") + frame + "-"
                + Guid.NewGuid().ToString("N") + ".txt");
            int exported = 0;
            bool limited = false;
            bool completed = false;
            int scannedThreads = 0;
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("schema=loaded-profiler-log-samples-v1 rawZeroBasedFrame=" + frame
                    + " first=" + ProfilerDriver.firstFrameIndex + " last=" + ProfilerDriver.lastFrameIndex);
                writer.WriteLine("Capture filename, APK identity and network role are NOT inferred by this export.");
                var addresses = new List<ulong>();
                try
                {
                    for (int thread = 0; thread < MaximumThreads; thread++)
                    {
                        using (RawFrameDataView data = ProfilerDriver.GetRawFrameDataView(frame, thread))
                        {
                            if (data == null || !data.valid) break;
                            scannedThreads++;
                            writer.WriteLine("thread=" + thread + " name=" + Escape(data.threadName)
                                + " samples=" + data.sampleCount + " frameMs="
                                + data.frameTimeMs.ToString("R", CultureInfo.InvariantCulture));
                            var parents = new List<int>();
                            var ends = new List<int>();
                            for (int sample = 0; sample < data.sampleCount; sample++)
                            {
                                while (ends.Count > 0 && sample > ends[ends.Count - 1])
                                {
                                    ends.RemoveAt(ends.Count - 1);
                                    parents.RemoveAt(parents.Count - 1);
                                }
                                string name = data.GetSampleName(sample) ?? "<unnamed>";
                                if (objectSamples ? IsObjectSample(name) :
                                    (name.IndexOf("LogStringToConsole", StringComparison.Ordinal) >= 0
                                    || name.IndexOf("UnitView.OnDisable", StringComparison.Ordinal) >= 0))
                                {
                                    if (exported >= MaximumExportedSamples) { limited = true; break; }
                                    exported++;
                                    writer.WriteLine("sample=" + sample + " name=" + Escape(name)
                                        + " startMs=" + data.GetSampleStartTimeMs(sample).ToString("R", CultureInfo.InvariantCulture)
                                        + " ms=" + data.GetSampleTimeMs(sample).ToString("R", CultureInfo.InvariantCulture));
                                    foreach (int parent in parents)
                                        writer.WriteLine(" ancestorByRecursiveIndex=" + parent + " " + Escape(data.GetSampleName(parent)));
                                    var metadataInfo = data.GetMarkerMetadataInfo(data.GetSampleMarkerId(sample));
                                    int metadata = data.GetSampleMetadataCount(sample);
                                    writer.WriteLine(" metadataCount=" + metadata);
                                    for (int field = 0; field < metadata; field++)
                                    {
                                        // 문자열 metadata만 본문 후보로 읽고, 다른 형식을 문자열로 강제 해석하지 않는다.
                                        try
                                        {
                                            System.Reflection.MethodInfo typeReader = typeof(RawFrameDataView).GetMethod(
                                                "GetSampleMetadataType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                            string type = typeReader?.Invoke(data, new object[] { sample, field })?.ToString()
                                                ?? "Unavailable";
                                            writer.WriteLine(" metadataType[" + field + "]=" + type);
                                            string metadataName = metadataInfo != null && field < metadataInfo.Length
                                                ? metadataInfo[field].name : "<unavailable>";
                                            writer.WriteLine(" metadataName[" + field + "]=" + Escape(metadataName));
                                            if (type == "Int32" || type == "UInt32")
                                            {
                                                int value = data.GetSampleMetadataAsInt(sample, field);
                                                writer.WriteLine(" metadataInt[" + field + "]=" + value);
                                                if (objectSamples && IsObjectIdMetadata(metadataName))
                                                    WriteUnityObject(writer, data, (UnityEngine.EntityId)value);
                                            }
                                            if (objectSamples && type == "InstanceId")
                                            {
                                                var ids = data.GetSampleMetadataAsSpan<UnityEngine.EntityId>(sample, field);
                                                writer.WriteLine(" metadataEntityIdCount[" + field + "]=" + ids.Length);
                                                if (ids.Length == 1) WriteUnityObject(writer, data, ids[0]);
                                            }
                                            if (type == "Int64" || type == "UInt64")
                                                writer.WriteLine(" metadataLong[" + field + "]="
                                                    + data.GetSampleMetadataAsLong(sample, field));
                                            // 공개 enum에 없는 legacy type 8의 Name만 원시 UTF-8 후보로 보관한다.
                                            // 이 후보 문자열만으로 scene/prefab 소유자를 확정하지 않는다.
                                            if (objectSamples && type == "8" && metadataName == "Name")
                                            {
                                                var bytes = data.GetSampleMetadataAsSpan<byte>(sample, field);
                                                int length = Math.Min(bytes.Length, 16384);
                                                writer.WriteLine(" metadataUtf8Candidate[" + field + "]="
                                                    + Escape(Encoding.UTF8.GetString(bytes.Slice(0, length).ToArray()).TrimEnd('\0')));
                                                if (length != bytes.Length) writer.WriteLine(" metadataPayloadTruncated=True");
                                            }
                                            if (type == "String16")
                                                writer.WriteLine(" metadata[" + field + "]="
                                                    + Escape(data.GetSampleMetadataAsString(sample, field)));
                                        }
                                        catch (Exception error) { writer.WriteLine(" metadataUnavailable=" + error.GetType().Name); }
                                    }
                                    addresses.Clear();
                                    data.GetSampleCallstack(sample, addresses);
                                    writer.WriteLine(" callstackCount=" + addresses.Count);
                                    foreach (ulong address in addresses)
                                    {
                                        FrameDataView.MethodInfo method = data.ResolveMethodInfo(address);
                                        writer.WriteLine(" stack=0x" + address.ToString("X", CultureInfo.InvariantCulture)
                                            + " " + Escape(method.methodName) + " " + Escape(method.sourceFileName)
                                            + ":" + method.sourceFileLine);
                                    }
                                }
                                int children = data.GetSampleChildrenCountRecursive(sample);
                                if (children > 0) { parents.Add(sample); ends.Add(sample + children); }
                            }
                        }
                        if (limited) break;
                        if (thread == MaximumThreads - 1) limited = true;
                    }
                    completed = !limited && scannedThreads > 0;
                }
                catch (Exception error)
                {
                    writer.WriteLine("EXPORT-ERROR type=" + error.GetType().Name);
                }
                finally
                {
                    writer.WriteLine("Ancestry uses recursive-child contiguous index intervals; inspect timing/sample indices when joining UI hierarchy.");
                    writer.WriteLine("END exported=" + exported + " completed=" + completed + " scannedThreads=" + scannedThreads + " limited=" + limited
                    + " missingMetadataOrStackCannotBeReconstructed=true");
                }
            }
            Debug.Log("Profiler sample export: frame=" + frame + " samples=" + exported
                + " completed=" + completed + " scannedThreads=" + scannedThreads + " limited=" + limited + " privateFile=" + path);
            return path;
        }

        private static bool IsObjectSample(string name) =>
            name.IndexOf("Instantiate", StringComparison.Ordinal) >= 0
            || name.IndexOf("AddComponent", StringComparison.Ordinal) >= 0
            || name.IndexOf("Loading", StringComparison.Ordinal) >= 0
            || name.IndexOf("Preload", StringComparison.Ordinal) >= 0
            || name.IndexOf("Unload", StringComparison.Ordinal) >= 0
            || name.IndexOf("LoadLevel", StringComparison.Ordinal) >= 0
            || name.IndexOf("AwakeFromLoad", StringComparison.Ordinal) >= 0
            || name.IndexOf("ProBuilder", StringComparison.Ordinal) >= 0
            || name.IndexOf("GarbageCollectAssetsProfile", StringComparison.Ordinal) >= 0
            || name.IndexOf("GC.Collect", StringComparison.Ordinal) >= 0;

        private static bool IsObjectIdMetadata(string name) => name != null
            && name.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0
            && (name.IndexOf("instance", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("object", StringComparison.OrdinalIgnoreCase) >= 0);

        private static void WriteUnityObject(StreamWriter writer, RawFrameDataView data, UnityEngine.EntityId id)
        {
            if (!data.GetUnityObjectInfo(id, out FrameDataView.UnityObjectInfo info))
            { writer.WriteLine(" objectInfoUnavailable=True instanceId=" + id); return; }
            writer.WriteLine(" objectInstanceId=" + id + " objectName=" + Escape(info.name));
            if (data.GetUnityObjectNativeTypeInfo(info.nativeTypeIndex,
                out FrameDataView.UnityObjectNativeTypeInfo type))
                writer.WriteLine(" objectNativeType=" + Escape(type.name));
            if (info.relatedGameObjectEntityId != default
                && data.GetUnityObjectInfo(info.relatedGameObjectEntityId, out var owner))
                writer.WriteLine(" relatedGameObject=" + Escape(owner.name));
        }

        private static readonly string[] SummaryMarkers =
        {
            "Hexiege.UnitView.Disable.SpawnFacing", "Hexiege.UnitView.Disable.SoloPlayback",
            "Hexiege.UnitView.Disable.SoloPursuit", "Hexiege.UnitView.Disable.SoloRecovery",
            "Hexiege.UnitView.Disable.TimingUnavailable", "Hexiege.UnitView.Disable.HealEnd",
            "Hexiege.UnitView.Disable.CasterEffects", "Hexiege.UnitView.Disable.PostCombat",
            "Hexiege.UnitView.Disable.InfernoRetire",
            "Hexiege.Console.Host", "Hexiege.Console.Client", "Hexiege.Console.Server",
            "Hexiege.Console.NetworkInactiveUnknown", "Hexiege.Console.NetworkStartingUnknown",
            "Hexiege.Console.Heal", "Hexiege.Console.Timing", "Hexiege.Console.Wave",
            "Hexiege.Console.PostCombat", "Hexiege.Console.Other",
            "Hexiege.Console.InfoDefaultStackPolicy", "Hexiege.Console.InfoNoStack",
            "Hexiege.Console.WarnDefaultStackPolicy", "Hexiege.Console.ErrorDefaultStackPolicy",
            "Hexiege.Console.Exception",
            // 원래 Android NCC 측정의 8개 marker와 저장 캡처에 존재하는 정확한 엔진 sample명.
            // 없는 sample은 0이며, 서로 포함되는 inclusive 열을 합쳐 전체 비용으로 쓰면 안 된다.
            "Hexiege.NetworkCombat.TickCombat", "Hexiege.NetworkCombat.TargetQueries",
            "Hexiege.NetworkCombat.TickProjectiles", "Hexiege.UnitView.ShouldEngage",
            "Hexiege.UnitView.ShouldEngageAt", "Hexiege.Bootstrap.RepathAllAliveUnits",
            "Hexiege.ProjectileView.Update", "Hexiege.Economy.TickIncome",
            "Instantiate", "Instantiate.Awake", "GameObject.AddComponent",
            "Unity.ProBuilder.dll!UnityEngine.ProBuilder::ProBuilderMesh.Awake() [Invoke]",
            "Unity.ProBuilder.dll!UnityEngine.ProBuilder::ProBuilderMesh.OnDestroy() [Invoke]",
            "GC.Alloc", "GC.Collect", "EarlyUpdate.UpdatePreloading", "Loading.UpdatePreloading",
            "UpdatePreloading", "Preload Single Step", "Application.LoadLevelAsync Integrate",
            "Application.WaitForAsyncOperationToComplete", "UnloadScene", "GarbageCollectAssetsProfile",
            "Loading.AwakeFromLoad", "Destroy", "GameObject.Deactivate",
            "TimeUpdate.WaitForLastPresentationAndUpdateTime", "WaitForLastPresent",
            "PostLateUpdate.FinishFrameRendering", "Gfx.WaitForPresentOnGfxThread", "WaitForTargetFPS",
            "NetworkEarlyUpdate", "NetworkPostLateUpdate",
            "NetworkMessageManager.DeserializeAndHandle.ServerRpcMessage", "LogStringToConsole",
            "Hexiege.Log.ArchiveWrite", "Hexiege.Log.ArchiveCallerStack", "Hexiege.Log.FileOnlyInfo", "Hexiege.Log.FileOnlyDiagnosticWarning",
            "Hexiege.LobbyReturn.BeginReturn", "Hexiege.LobbyReturn.LobbyReady", "Hexiege.LobbyReturn.LoadingHidden",
            "Hexiege.LobbyReturn.InputReady", "Hexiege.LobbyReturn.Abort",
            "Hexiege.Log.ArchiveConsoleFallback", "Hexiege.Log.ArchiveFailure"
        };

        [MenuItem("Hexiege/Combat/Diagnostics/Export Loaded Profiler Frame Summary")]
        private static void ExportFrameSummary()
        {
            ExportFrameSummaryFile();
        }

        private static string ExportFrameSummaryFile()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || ProfilerDriver.enabled)
            {
                Debug.LogWarning("Profiler summary requires stopped Play Mode and stopped recording.");
                return null;
            }
            Type windowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProfilerWindow");
            if (windowType == null || Resources.FindObjectsOfTypeAll(windowType).Length != 1
                || ProfilerDriver.firstFrameIndex < 0 || ProfilerDriver.lastFrameIndex < ProfilerDriver.firstFrameIndex)
            {
                Debug.LogWarning("Open one Profiler window with a loaded recording before exporting its summary.");
                return null;
            }
            const int maximumFrames = 20000;
            int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
            long requested = (long)last - first + 1;
            bool limited = requested > maximumFrames, completed = false;
            int scanned = 0, invalid = 0;
            var frames = new List<KeyValuePair<int, double>>();
            var markerIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < SummaryMarkers.Length; i++) markerIndices.Add(SummaryMarkers[i], i);
            string directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/ProfilerDiagnostics"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "frame-summary-" + Guid.NewGuid().ToString("N") + ".csv");
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("# schema=loaded-profiler-frame-summary-v1,rawFirst=" + first + ",rawLast=" + last);
                writer.WriteLine("# Main Thread recorded frame duration includes waits; phase/role/APK identity are not inferred.");
                writer.WriteLine("# Marker times are inclusive and nested; never sum columns as independent CPU work.");
                writer.Write("rawZeroBasedFrame,mainThreadFrameMs");
                foreach (string marker in SummaryMarkers) writer.Write("," + marker + ".calls," + marker + ".inclusiveMs");
                writer.WriteLine();
                try
                {
                    for (long offset = 0; offset < Math.Min(requested, maximumFrames); offset++)
                    {
                        int frame = (int)(first + offset);
                        scanned++;
                        using (RawFrameDataView data = FindMainThread(frame))
                        {
                            if (data == null) { invalid++; continue; }
                            double duration = data.frameTimeMs;
                            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0)
                            { invalid++; continue; }
                            var calls = new int[SummaryMarkers.Length];
                            var times = new double[SummaryMarkers.Length];
                            for (int sample = 0; sample < data.sampleCount; sample++)
                            {
                                string name = data.GetSampleName(sample);
                                if (name == null || !markerIndices.TryGetValue(name, out int marker)) continue;
                                calls[marker]++;
                                times[marker] += data.GetSampleTimeMs(sample);
                            }
                            frames.Add(new KeyValuePair<int, double>(frame, duration));
                            writer.Write(frame.ToString(CultureInfo.InvariantCulture) + "," + Number(duration));
                            for (int i = 0; i < calls.Length; i++) writer.Write("," + calls[i] + "," + Number(times[i]));
                            writer.WriteLine();
                        }
                    }
                    completed = !limited && scanned == requested && frames.Count > 0;
                }
                catch (Exception error) { writer.WriteLine("# EXPORT-ERROR,type=" + error.GetType().Name); }
                finally
                {
                    WriteFrameStatistics(writer, frames);
                    writer.WriteLine("# END,completed=" + completed + ",scanned=" + scanned
                        + ",valid=" + frames.Count + ",invalidSkipped=" + invalid + ",limited=" + limited);
                }
            }
            Debug.Log("Profiler frame summary: completed=" + completed + " frames=" + frames.Count
                + " invalidSkipped=" + invalid + " limited=" + limited + " privateFile=" + path);
            return path;
        }

        private static readonly string[] SplitCaptureNames =
        {
            "Hexiege_2026-10-29_14-57-46.data", "Hexiege_2026-10-29_14-58-48.data",
            "Hexiege_2026-10-29_15-00-23.data", "Hexiege_2026-10-29_15-02-13.data",
            "Hexiege_2026-10-29_15-02-40.data", "Hexiege_2026-10-29_15-05-42.data",
            "Hexiege_2026-10-29_15-08-26.data", "Hexiege_2026-10-29_15-08-54.data"
        };
        private static readonly string[] DeathPolicyCaptureNames =
        {
            "Hexiege_2026-10-29_17-30-46.data", "Hexiege_2026-10-29_17-32-15.data",
            "Hexiege_2026-10-29_17-33-44.data", "Hexiege_2026-10-29_17-34-05.data"
        };
        private static readonly string[] DeviceArchiveCaptureNames =
        {
            "Hexiege_2026-10-30_01-53-51.data", "Hexiege_2026-10-30_01-56-01.data"
        };
        private static string[] _splitSources = SplitCaptureNames;
        private static int _splitIndex;
        private static string _splitReceipt;
        private static bool _splitRunning;
        private static bool _splitDetails;
        private static bool _splitHotspots;
        private static readonly int[] DetailSourceIndices = { 0, 7, 7 };
        private static readonly int[] DetailRawFrames = { 1789, 134, 1029 };
        private static readonly bool[] DetailObjects = { true, false, true };
        private static readonly int[] DeathDetailSourceIndices = { 3, 3, 3 };
        private static readonly int[] DeathDetailRawFrames = { 1453, 1533, 1859 };
        private static readonly bool[] DeathDetailObjects = { false, false, false };
        private static int[] _activeDetailSourceIndices = DetailSourceIndices;
        private static int[] _activeDetailRawFrames = DetailRawFrames;
        private static bool[] _activeDetailObjects = DetailObjects;
        private static readonly int[] HotspotSourceIndices = { 0, 7 };
        private static readonly int[] HotspotRawFrames = { 1789, 1029 };
        private static readonly int[] ArchiveDetailSourceIndices = { 0, 0, 0, 0, 0 };
        private static readonly int[] ArchiveDetailRawFrames = { 1237, 1345, 1894, 1895, 1226 };
        private static readonly bool[] ArchiveDetailObjects = { false, false, false, false, false };
        private static readonly int[] ArchiveHotspotSourceIndices = { 1, 1 };
        private static readonly int[] ArchiveHotspotRawFrames = { 1438, 1518 };
        private static int[] _activeHotspotSourceIndices = HotspotSourceIndices;
        private static int[] _activeHotspotRawFrames = HotspotRawFrames;

        [MenuItem("Hexiege/Combat/Diagnostics/Export Eight Split Captures 2026-10-29")]
        private static void ExportEightSplitCaptures() => StartSplitExport(false);

        [MenuItem("Hexiege/Combat/Diagnostics/Export Three Split Capture Details 2026-10-29")]
        private static void ExportThreeSplitDetails() => StartSplitExport(true);

        [MenuItem("Hexiege/Combat/Diagnostics/Export Two Split Frame Hotspots 2026-10-29")]
        private static void ExportTwoSplitHotspots() => StartSplitExport(false, true);

        [MenuItem("Hexiege/Combat/Diagnostics/Export Four Death Policy Captures 2026-10-29")]
        private static void ExportFourDeathPolicyCaptures() => StartSplitExport(false, false, true);

        [MenuItem("Hexiege/Combat/Diagnostics/Export Three Death Policy Log Details 2026-10-29")]
        private static void ExportThreeDeathPolicyDetails() => StartSplitExport(true, false, true, true);

        [MenuItem("Hexiege/Combat/Diagnostics/Export Two Device Archive Captures 2026-10-30")]
        private static void ExportTwoDeviceArchiveCaptures() => StartSplitExport(false, summarySources: DeviceArchiveCaptureNames);

        [MenuItem("Hexiege/Combat/Diagnostics/Export Five Device Archive Log Details 2026-10-30")]
        private static void ExportFiveDeviceArchiveDetails() => StartSplitExport(true,
            summarySources: DeviceArchiveCaptureNames, archiveDiagnostics: true);

        [MenuItem("Hexiege/Combat/Diagnostics/Export Two Device Archive Frame Hotspots 2026-10-30")]
        private static void ExportTwoDeviceArchiveHotspots() => StartSplitExport(false, hotspots: true,
            summarySources: DeviceArchiveCaptureNames, archiveDiagnostics: true);

        [MenuItem("Hexiege/Combat/Diagnostics/Export Two Combat Work Frame Hotspots 2026-10-30")]
        private static void ExportTwoCombatWorkHotspots() => StartSplitExport(false, hotspots: true,
            summarySources: new[] { ResumedCaptureName }, hotspotSourceIndices: new[] { 0, 0 },
            hotspotRawFrames: new[] { 1510, 1419 }, receiptPrefix: "combat-work-hotspots-receipt-");

        private static void StartSplitExport(bool details, bool hotspots = false, bool deathPolicy = false, bool deathDetails = false,
            string[] summarySources = null, bool archiveDiagnostics = false,
            int[] hotspotSourceIndices = null, int[] hotspotRawFrames = null, string receiptPrefix = null)
        {
            if (_splitRunning) { Debug.LogWarning("Split export is already running."); return; }
            if (!SplitExportSafe()) { Debug.LogWarning("Split export requires one Profiler window, stopped Play and recording."); return; }
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            string[] sources = summarySources ?? (deathPolicy ? DeathPolicyCaptureNames : SplitCaptureNames);
            int[] detailIndices = archiveDiagnostics ? ArchiveDetailSourceIndices : deathDetails ? DeathDetailSourceIndices : DetailSourceIndices;
            int[] hotspotIndices = hotspotSourceIndices ?? (archiveDiagnostics ? ArchiveHotspotSourceIndices : HotspotSourceIndices);
            int[] hotspotFrames = hotspotRawFrames ?? (archiveDiagnostics ? ArchiveHotspotRawFrames : HotspotRawFrames);
            if (hotspots && hotspotIndices.Length != hotspotFrames.Length)
            { Debug.LogWarning("Hotspot source/frame counts differ."); return; }
            int count = hotspots ? hotspotIndices.Length : details ? detailIndices.Length : sources.Length;
            for (int index = 0; index < count; index++)
            {
                string name = sources[hotspots ? hotspotIndices[index] : details ? detailIndices[index] : index];
                if (!File.Exists(Path.Combine(root, "ProfilerCaptures", name)))
                { Debug.LogWarning("Split capture is missing: " + name); return; }
            }
            string directory = Path.Combine(root, "Library/ProfilerDiagnostics");
            Directory.CreateDirectory(directory);
            _splitReceipt = Path.Combine(directory, (receiptPrefix ?? (archiveDiagnostics ? (hotspots ? "device-archive-hotspots-receipt-" : "device-archive-log-details-receipt-") : summarySources != null ? "device-archive-summary-receipt-" : deathDetails ? "death-policy-details-receipt-" : deathPolicy ? "death-policy-summary-receipt-" : hotspots ? "split-hotspots-receipt-" : details ? "split-details-receipt-" : "split-summary-receipt-")) + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(_splitReceipt, "BEGIN count=" + count + " sourceReadOnly=True append=False\n", new UTF8Encoding(false));
            _splitIndex = 0; _splitRunning = true; _splitDetails = details; _splitHotspots = hotspots; _splitSources = sources;
            _activeDetailSourceIndices = detailIndices;
            _activeDetailRawFrames = archiveDiagnostics ? ArchiveDetailRawFrames : deathDetails ? DeathDetailRawFrames : DetailRawFrames;
            _activeDetailObjects = archiveDiagnostics ? ArchiveDetailObjects : deathDetails ? DeathDetailObjects : DetailObjects;
            _activeHotspotSourceIndices = hotspotIndices;
            _activeHotspotRawFrames = hotspotFrames;
            EditorApplication.update += ExportNextSplitCapture;
            AssemblyReloadEvents.beforeAssemblyReload += StopSplitOnReload;
            Debug.Log("Split capture export started; private receipt=" + _splitReceipt);
        }

        private const string ResumedCaptureName = "Hexiege_2026-10-30_14-10-35.data";
        private static string _resumedReceipt, _resumedSourceHash;
        private static long _resumedSourceBytes;
        private static int _resumedStep;
        private static List<KeyValuePair<int, double>> _resumedTopFrames;

        [MenuItem("Hexiege/Combat/Diagnostics/Export Resumed Capture Summary And Top Three Hotspots 2026-10-30")]
        private static void ExportResumedCapture()
        {
            if (_splitRunning || !SplitExportSafe())
            { Debug.LogWarning("Capture export requires idle export, one Profiler window, stopped Play and recording."); return; }
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            if (!File.Exists(Path.Combine(root, "ProfilerCaptures", ResumedCaptureName)))
            { Debug.LogWarning("Resumed capture is missing."); return; }
            string directory = Path.Combine(root, "Library/ProfilerDiagnostics");
            Directory.CreateDirectory(directory);
            _resumedReceipt = Path.Combine(directory, "resumed-capture-receipt-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(_resumedReceipt, "BEGIN source=" + ResumedCaptureName
                + " sourceReadOnly=True append=False expectedExports=4\n", new UTF8Encoding(false));
            _resumedStep = 0; _resumedTopFrames = null; _splitRunning = true;
            EditorApplication.update += ExportNextResumedCapture;
            AssemblyReloadEvents.beforeAssemblyReload += StopResumedOnReload;
            Debug.Log("Resumed capture export started; private receipt=" + _resumedReceipt);
        }

        private static void ExportNextResumedCapture()
        {
            try
            {
                if (!SplitExportSafe()) { FinishResumedCapture("ABORT guard-changed"); return; }
                string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
                string source = Path.Combine(root, "ProfilerCaptures", ResumedCaptureName);
                long bytes = new FileInfo(source).Length;
                string sha;
                using (var hash = System.Security.Cryptography.SHA256.Create())
                using (var stream = File.OpenRead(source)) sha = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
                if (_resumedStep == 0) { _resumedSourceHash = sha; _resumedSourceBytes = bytes; }
                else if (sha != _resumedSourceHash || bytes != _resumedSourceBytes)
                    throw new InvalidOperationException("Source capture changed during export.");
                // update 사이에 다른 기록을 열었더라도 각 결과는 이 원본을 다시 로드한 뒤 만든다.
                bool loaded = ProfilerDriver.LoadProfile(source, false);
                int? raw = _resumedStep == 0 ? (int?)null : _resumedTopFrames[_resumedStep - 1].Key;
                string output = loaded ? (raw.HasValue ? ExportFrameHotspots(raw.Value) : ExportFrameSummaryFile()) : null;
                bool complete = false;
                if (output != null)
                    foreach (string line in File.ReadLines(output))
                        if (line.StartsWith("# END,completed=True,", StringComparison.Ordinal)) complete = true;
                File.AppendAllText(_resumedReceipt, "FILE step=" + _resumedStep + " source=" + ResumedCaptureName
                    + " bytes=" + bytes + " sha256=" + sha + " load=" + loaded
                    + (raw.HasValue ? " raw=" + raw.Value + " hotspots=" : " summary=")
                    + (output ?? "<none>") + " completed=" + complete + "\n");
                if (!complete) { FinishResumedCapture("ABORT incomplete-export step=" + _resumedStep); return; }
                if (_resumedStep == 0) _resumedTopFrames = ReadTopThreeFrames(output);
                _resumedStep++;
                if (_resumedStep == 4) FinishResumedCapture("END processed=4 allCompleted=True");
            }
            catch (Exception error) { FinishResumedCapture("ABORT step=" + _resumedStep + " error=" + error.GetType().Name); }
        }

        private static List<KeyValuePair<int, double>> ReadTopThreeFrames(string summary)
        {
            var frames = new List<KeyValuePair<int, double>>();
            foreach (string line in File.ReadLines(summary))
            {
                if (!line.StartsWith("# TOP,rank=", StringComparison.Ordinal)) continue;
                string[] fields = line.Split(',');
                if (fields.Length != 4 || !fields[1].StartsWith("rank=", StringComparison.Ordinal)
                    || !int.TryParse(fields[1].Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out int rank))
                    throw new InvalidOperationException("Malformed summary TOP rank.");
                if (rank > 3) continue;
                const string rawPrefix = "rawZeroBasedFrame=", msPrefix = "ms=";
                if (rank != frames.Count + 1 || !fields[2].StartsWith(rawPrefix, StringComparison.Ordinal)
                    || !fields[3].StartsWith(msPrefix, StringComparison.Ordinal)
                    || !int.TryParse(fields[2].Substring(rawPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int raw)
                    || !double.TryParse(fields[3].Substring(msPrefix.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out double ms)
                    || raw < 0 || double.IsNaN(ms) || double.IsInfinity(ms) || ms < 0
                    || (frames.Count > 0 && ms > frames[frames.Count - 1].Value)
                    || frames.Exists(frame => frame.Key == raw))
                    throw new InvalidOperationException("Malformed summary TOP frame.");
                frames.Add(new KeyValuePair<int, double>(raw, ms));
            }
            if (frames.Count != 3) throw new InvalidOperationException("Summary requires three valid TOP frames.");
            return frames;
        }

        private static void StopResumedOnReload() => FinishResumedCapture("ABORT assembly-reload");
        private static void FinishResumedCapture(string status)
        {
            EditorApplication.update -= ExportNextResumedCapture;
            AssemblyReloadEvents.beforeAssemblyReload -= StopResumedOnReload;
            _splitRunning = false;
            try { File.AppendAllText(_resumedReceipt, status + "\n"); }
            catch (Exception error) { Debug.LogError("Resumed receipt finalization failed: " + error.GetType().Name); }
            Debug.Log("Resumed capture export " + status + " privateReceipt=" + _resumedReceipt);
        }

        private static string _resumedClockReceipt;

        [MenuItem("Hexiege/Combat/Diagnostics/Export Resumed Capture Frame Clocks 2026-10-30")]
        private static void ExportResumedFrameClocks()
        {
            if (_splitRunning || !SplitExportSafe())
            { Debug.LogWarning("Frame clock export requires idle export, one Profiler window, stopped Play and recording."); return; }
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            if (!File.Exists(Path.Combine(root, "ProfilerCaptures", ResumedCaptureName)))
            { Debug.LogWarning("Resumed capture is missing."); return; }
            string directory = Path.Combine(root, "Library/ProfilerDiagnostics");
            Directory.CreateDirectory(directory);
            _resumedClockReceipt = Path.Combine(directory, "resumed-frame-clocks-receipt-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(_resumedClockReceipt, "BEGIN source=" + ResumedCaptureName
                + " sourceReadOnly=True append=False sample0ClockIsNotWallClock=True\n", new UTF8Encoding(false));
            _splitRunning = true;
            EditorApplication.update += ExportResumedFrameClocksOnUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += StopResumedFrameClocksOnReload;
            Debug.Log("Frame clock export scheduled; private receipt=" + _resumedClockReceipt);
        }

        private static void ExportResumedFrameClocksOnUpdate()
        {
            // 한 번만 실행한다. Reload/guard 실패도 공통 종료에서 등록을 해제한다.
            EditorApplication.update -= ExportResumedFrameClocksOnUpdate;
            try
            {
                if (!SplitExportSafe()) { FinishResumedFrameClocks("ABORT guard-changed"); return; }
                string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
                string source = Path.Combine(root, "ProfilerCaptures", ResumedCaptureName);
                long bytes = new FileInfo(source).Length;
                string sourceHash;
                using (var hash = System.Security.Cryptography.SHA256.Create())
                using (var stream = File.OpenRead(source)) sourceHash = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
                bool loaded = ProfilerDriver.LoadProfile(source, false);
                int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
                File.AppendAllText(_resumedClockReceipt, "SOURCE bytes=" + bytes + " sha256=" + sourceHash
                    + " load=" + loaded + " rawFirst=" + first + " rawLast=" + last + "\n");
                if (!loaded || first < 0 || last < first)
                { FinishResumedFrameClocks("ABORT unavailable-recording"); return; }
                long requested = (long)last - first + 1;
                bool limited = requested > 20000, complete = false;
                int scanned = 0, invalid = 0, valid = 0;
                string path = Path.Combine(root, "Library/ProfilerDiagnostics/resumed-frame-clocks-" + Guid.NewGuid().ToString("N") + ".csv");
                using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
                {
                    writer.WriteLine("# Main Thread sample 0 GetSampleStartTimeMs; not wall-clock, and no logcat epoch equivalence is assumed.");
                    writer.WriteLine("rawZeroBasedFrame,mainThreadSample0StartMs,mainThreadFrameMs");
                    try
                    {
                        for (long offset = 0; offset < Math.Min(requested, 20000); offset++)
                        {
                            int raw = (int)(first + offset);
                            scanned++;
                            using (RawFrameDataView data = FindMainThread(raw))
                            {
                                if (data == null || data.sampleCount == 0) { invalid++; continue; }
                                double start = data.GetSampleStartTimeMs(0), duration = data.frameTimeMs;
                                if (double.IsNaN(start) || double.IsInfinity(start) || start < 0
                                    || double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0)
                                { invalid++; continue; }
                                writer.WriteLine(raw.ToString(CultureInfo.InvariantCulture) + "," + Number(start) + "," + Number(duration));
                                valid++;
                            }
                        }
                        complete = !limited && scanned == requested && invalid == 0;
                    }
                    catch (Exception error) { writer.WriteLine("# EXPORT-ERROR,type=" + error.GetType().Name); }
                    finally
                    {
                        writer.WriteLine("# END,completed=" + complete + ",scanned=" + scanned + ",valid=" + valid
                            + ",invalidSkipped=" + invalid + ",limited=" + limited + ",rawFirst=" + first + ",rawLast=" + last);
                    }
                }
                string csvHash;
                using (var hash = System.Security.Cryptography.SHA256.Create())
                using (var stream = File.OpenRead(path)) csvHash = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
                File.AppendAllText(_resumedClockReceipt, "FILE csv=" + path + " csvSha256=" + csvHash
                    + " csvBytes=" + new FileInfo(path).Length + " completed=" + complete + " scanned=" + scanned
                    + " valid=" + valid + " invalid=" + invalid + " rawFirst=" + first + " rawLast=" + last + " limited=" + limited + "\n");
                FinishResumedFrameClocks("END completed=" + complete);
            }
            catch (Exception error) { FinishResumedFrameClocks("ABORT error=" + error.GetType().Name); }
        }

        private static void StopResumedFrameClocksOnReload() => FinishResumedFrameClocks("ABORT assembly-reload");
        private static void FinishResumedFrameClocks(string status)
        {
            EditorApplication.update -= ExportResumedFrameClocksOnUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= StopResumedFrameClocksOnReload;
            _splitRunning = false;
            try { File.AppendAllText(_resumedClockReceipt, status + "\n"); }
            catch (Exception error) { Debug.LogError("Frame clock receipt finalization failed: " + error.GetType().Name); }
            Debug.Log("Frame clock export " + status + " privateReceipt=" + _resumedClockReceipt);
        }

        private static bool SplitExportSafe()
        {
            Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProfilerWindow");
            return !EditorApplication.isPlayingOrWillChangePlaymode && !ProfilerDriver.enabled
                && !EditorApplication.isCompiling && !EditorApplication.isUpdating
                && type != null && Resources.FindObjectsOfTypeAll(type).Length == 1;
        }

        private static void ExportNextSplitCapture()
        {
            try
            {
                if (!SplitExportSafe()) { FinishSplitExport("ABORT guard-changed"); return; }
                string name = _splitSources[_splitHotspots ? _activeHotspotSourceIndices[_splitIndex] : _splitDetails ? _activeDetailSourceIndices[_splitIndex] : _splitIndex];
                string source = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../ProfilerCaptures", name));
                string sha;
                using (var stream = File.OpenRead(source))
                using (var hash = System.Security.Cryptography.SHA256.Create())
                    sha = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
                long length = new FileInfo(source).Length;
                bool loaded = ProfilerDriver.LoadProfile(source, false);
                string csv = loaded ? (_splitHotspots ? ExportFrameHotspots(_activeHotspotRawFrames[_splitIndex])
                    : _splitDetails ? ExportSamples(_activeDetailObjects[_splitIndex], _activeDetailRawFrames[_splitIndex])
                    : ExportFrameSummaryFile()) : null;
                bool complete = false;
                if (csv != null)
                    foreach (string line in File.ReadLines(csv))
                        if (_splitDetails ? (line.StartsWith("END exported=", StringComparison.Ordinal)
                            && line.Contains(" completed=True ") && line.Contains(" limited=False "))
                            : line.StartsWith("# END,completed=True,", StringComparison.Ordinal)) complete = true;
                File.AppendAllText(_splitReceipt, "FILE index=" + _splitIndex + " source=" + name + " bytes=" + length
                    + " sha256=" + sha + " load=" + loaded + (_splitHotspots ? " raw=" + _activeHotspotRawFrames[_splitIndex] + " hotspots=" : _splitDetails ? " raw=" + _activeDetailRawFrames[_splitIndex]
                        + " objects=" + _activeDetailObjects[_splitIndex] + " export=" : " csv=") + (csv ?? "<none>")
                    + " completed=" + complete + "\n");
                _splitIndex++;
                int count = _splitHotspots ? _activeHotspotSourceIndices.Length : _splitDetails ? _activeDetailSourceIndices.Length : _splitSources.Length;
                if (_splitIndex == count) FinishSplitExport("END processed=" + count);
            }
            catch (Exception error)
            {
                FinishSplitExport("ABORT index=" + _splitIndex + " error=" + error.GetType().Name);
                Debug.LogError("Split capture export failed: " + error);
            }
        }

        private static void StopSplitOnReload() => FinishSplitExport("ABORT assembly-reload");
        private static void FinishSplitExport(string status)
        {
            EditorApplication.update -= ExportNextSplitCapture;
            AssemblyReloadEvents.beforeAssemblyReload -= StopSplitOnReload;
            _splitRunning = false;
            try { File.AppendAllText(_splitReceipt, status + "\n"); }
            catch (Exception error) { Debug.LogError("Split receipt finalization failed: " + error.GetType().Name); }
            Debug.Log("Split export " + status + " privateReceipt=" + _splitReceipt);
        }

        private static RawFrameDataView FindMainThread(int frame)
        {
            for (int thread = 0; thread < MaximumThreads; thread++)
            {
                RawFrameDataView data = ProfilerDriver.GetRawFrameDataView(frame, thread);
                if (data == null) return null;
                if (!data.valid) { data.Dispose(); return null; }
                if (data.threadName == "Main Thread") return data;
                data.Dispose();
            }
            return null;
        }

        private sealed class Hotspot
        {
            public string Name;
            public int Calls;
            public double Inclusive, Self;
        }

        private static string ExportFrameHotspots(int frame)
        {
            if (!SplitExportSafe() || frame < ProfilerDriver.firstFrameIndex || frame > ProfilerDriver.lastFrameIndex)
                throw new InvalidOperationException("Hotspot frame/recording guard failed.");
            using (RawFrameDataView data = FindMainThread(frame))
            {
                if (data == null) throw new InvalidOperationException("Main Thread is unavailable.");
                var durations = new double[data.sampleCount];
                var childCounts = new int[data.sampleCount];
                for (int sample = 0; sample < data.sampleCount; sample++)
                {
                    double duration = data.GetSampleTimeMs(sample);
                    if (double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0)
                        throw new InvalidOperationException("Invalid raw sample duration.");
                    durations[sample] = duration;
                    childCounts[sample] = data.GetSampleChildrenCountRecursive(sample);
                }
                double[] childrenDurations = DirectChildTimes(durations, childCounts);
                var grouped = new Dictionary<string, Hotspot>(StringComparer.Ordinal);
                int negative = 0;
                double minimumNegative = 0, negativeMagnitude = 0;
                for (int sample = 0; sample < data.sampleCount; sample++)
                {
                    string name = data.GetSampleName(sample) ?? "<unnamed>";
                    if (!grouped.TryGetValue(name, out Hotspot group))
                    { group = new Hotspot { Name = name }; grouped.Add(name, group); }
                    double self = durations[sample] - childrenDurations[sample];
                    if (self < 0)
                    {
                        negative++; minimumNegative = Math.Min(minimumNegative, self); negativeMagnitude -= self;
                        self = 0;
                    }
                    group.Calls++; group.Inclusive += durations[sample]; group.Self += self;
                }
                string directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/ProfilerDiagnostics"));
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "frame-hotspots-" + frame + "-" + Guid.NewGuid().ToString("N") + ".csv");
                using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
                {
                    writer.WriteLine("# schema=loaded-frame-hotspots-v1,rawFrame=" + frame + ",thread=Main Thread,frameMs=" + Number(data.frameTimeMs));
                    writer.WriteLine("# Main Thread only; inclusive times overlap. Self=sample-duration minus direct-child durations using validated recursive preorder intervals.");
                    writer.WriteLine("# Self negatives are clamped to zero; float precision/overlapping timing can prevent exact CPU accounting. No phase/owner/metadata inference.");
                    writer.WriteLine("# negativeSelfSamples=" + negative + ",minimumNegativeMs=" + Number(minimumNegative)
                        + ",clampedMagnitudeMs=" + Number(negativeMagnitude));
                    writer.WriteLine("ranking,rank,sampleName,calls,inclusiveMs,selfFromDirectChildrenMs");
                    var groups = new List<Hotspot>(grouped.Values);
                    groups.Sort((a, b) => b.Inclusive.CompareTo(a.Inclusive));
                    WriteHotspotRows(writer, groups, "inclusive");
                    groups.Sort((a, b) => b.Self.CompareTo(a.Self));
                    WriteHotspotRows(writer, groups, "self");
                    writer.WriteLine("# END,completed=True,samples=" + data.sampleCount + ",uniqueNames=" + groups.Count + ",maximumRows=200");
                }
                return path;
            }
        }

        private static double[] DirectChildTimes(double[] durations, int[] childCounts)
        {
            if (durations.Length != childCounts.Length) throw new InvalidOperationException("Sample arrays differ.");
            var childrenDurations = new double[durations.Length];
            var parents = new List<int>();
            var ends = new List<int>();
            // 깊이 우선 연속 구간에서 직근 부모에만 더한다. 손자 시간을 조상에서 다시 빼지 않는다.
            for (int sample = 0; sample < durations.Length; sample++)
            {
                while (ends.Count > 0 && sample > ends[ends.Count - 1])
                { ends.RemoveAt(ends.Count - 1); parents.RemoveAt(parents.Count - 1); }
                if (parents.Count > 0) childrenDurations[parents[parents.Count - 1]] += durations[sample];
                int children = childCounts[sample];
                if (children < 0 || (long)sample + children >= durations.Length
                    || (ends.Count > 0 && sample + children > ends[ends.Count - 1]))
                    throw new InvalidOperationException("Raw recursive-child intervals are not nested.");
                if (children > 0) { parents.Add(sample); ends.Add(sample + children); }
            }
            return childrenDurations;
        }

        private static void WriteHotspotRows(StreamWriter writer, List<Hotspot> groups, string ranking)
        {
            for (int i = 0; i < Math.Min(100, groups.Count); i++)
            {
                var group = groups[i];
                writer.WriteLine(ranking + "," + (i + 1) + ",\"" + group.Name.Replace("\"", "\"\"").Replace("\r", "\\r").Replace("\n", "\\n")
                    + "\"," + group.Calls + "," + Number(group.Inclusive) + "," + Number(group.Self));
            }
        }

        private static void WriteFrameStatistics(StreamWriter writer, List<KeyValuePair<int, double>> frames)
        {
            if (frames.Count == 0) { writer.WriteLine("# STATS,N=0,unavailable=True"); return; }
            double sum = 0;
            int over33 = 0, over50 = 0, over100 = 0;
            foreach (var frame in frames)
            {
                sum += frame.Value;
                if (frame.Value > 33.3d) over33++;
                if (frame.Value > 50d) over50++;
                if (frame.Value > 100d) over100++;
            }
            frames.Sort((left, right) => left.Value.CompareTo(right.Value));
            double p95 = frames[(int)Math.Ceiling(frames.Count * 0.95d) - 1].Value;
            writer.WriteLine("# STATS,N=" + frames.Count + ",averageMs=" + Number(sum / frames.Count)
                + ",p95NearestRankMs=" + Number(p95) + ",maxMs=" + Number(frames[frames.Count - 1].Value)
                + ",over33.3=" + over33 + ",over50=" + over50 + ",over100=" + over100);
            for (int rank = 0; rank < Math.Min(10, frames.Count); rank++)
            {
                var frame = frames[frames.Count - 1 - rank];
                writer.WriteLine("# TOP,rank=" + (rank + 1) + ",rawZeroBasedFrame=" + frame.Key + ",ms=" + Number(frame.Value));
            }
        }

        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string Escape(string text) => (text ?? "<null>")
            .Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    }
}
