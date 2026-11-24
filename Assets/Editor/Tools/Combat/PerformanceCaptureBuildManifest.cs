using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Collections.Generic;

namespace Hexiege.EditorTools
{
    /// <summary>Development 또는 명시적 Release 측정 빌드 직전 원문/설정을 Library에 보관한다.</summary>
    internal sealed class PerformanceCaptureBuildManifest : IPreprocessBuildWithReport
    {
        public int callbackOrder => int.MaxValue;
        private const string MeasurementDefine = "HEXIEGE_LOBBY_RETURN_MEASUREMENT";
        private const string MeasurementMenu = "Hexiege/Performance/Toggle Release Lobby Return Measurement Define";
        private const string DetectDefine = "HEXIEGE_DETECT_RANGE_COMPARISON";
        private const string DetectMenu = "Hexiege/Performance/Toggle Release Detect Range Comparison Define";

        private static bool HasDetectDefine(string defines)
            => Array.Exists((defines ?? "").Split(';'), value => value.Trim() == DetectDefine);
        private static string ToggleDetectDefine(string defines)
        {
            var tokens = new List<string>((defines ?? "").Split(';'));
            if (HasDetectDefine(defines)) tokens.RemoveAll(value => value.Trim() == DetectDefine);
            else tokens.Add(DetectDefine);
            return string.Join(";", tokens);
        }
        [MenuItem(DetectMenu)]
        private static void ToggleDetectComparison()
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, ToggleDetectDefine(defines));
            Debug.Log("Android release detect-range comparison define toggled. Build options and other defines were not changed.");
        }
        [MenuItem(DetectMenu, true)]
        private static bool ValidateToggleDetectComparison()
        {
            Menu.SetChecked(DetectMenu, HasDetectDefine(PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)));
            return !EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static bool HasMeasurementDefine(string defines)
            => Array.Exists((defines ?? "").Split(';'), value => value.Trim() == MeasurementDefine);

        private static string ToggleMeasurementDefine(string defines)
        {
            bool remove = HasMeasurementDefine(defines);
            var tokens = new List<string>((defines ?? "").Split(';'));
            if (remove) tokens.RemoveAll(value => value.Trim() == MeasurementDefine);
            else tokens.Add(MeasurementDefine);
            return string.Join(";", tokens);
        }

        [MenuItem(MeasurementMenu)]
        private static void ToggleReleaseMeasurement()
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, ToggleMeasurementDefine(defines));
            Debug.Log("Android release lobby-return measurement define toggled. Build options/quality/Frame Timing Stats were not changed.");
        }

        [MenuItem(MeasurementMenu, true)]
        private static bool ValidateToggleReleaseMeasurement()
        {
            Menu.SetChecked(MeasurementMenu, HasMeasurementDefine(PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)));
            return !EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void ValidateMeasurementBuild(BuildTarget target, BuildOptions options, bool measurement)
        {
            const BuildOptions forbidden = BuildOptions.Development | BuildOptions.AllowDebugging
                | BuildOptions.ConnectWithProfiler | BuildOptions.EnableDeepProfilingSupport;
            if (measurement && (target != BuildTarget.Android || (options & forbidden) != 0))
                throw new BuildFailedException("Release lobby-return measurement requires Android non-development without script debugging, profiler connection or deep profiling. Build options were not changed.");
        }
        private static readonly string[] Sources =
        {
            "Assets/_Project/Scripts/Application/Combat/Sequencing/AuthoritativeAttackPresentationCoordinator.cs",
            "Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs",
            "Assets/_Project/Scripts/Application/Combat/WavePresentationCoordinator.cs",
            "Assets/_Project/Scripts/Application/Combat/WavePresentationDiagnostics.cs",
            "Assets/_Project/Scripts/Application/GameLog.cs",
            "Assets/_Project/Scripts/Infrastructure/Debug/ConsoleSink.cs",
            "Assets/_Project/Scripts/Infrastructure/Debug/RuntimeLogger.cs",
            "Assets/_Project/Scripts/Infrastructure/Debug/LogSessionOwner.cs",
            "Assets/_Project/Scripts/Infrastructure/Debug/CustomRoomCreationDiagnostics.cs",
            "Assets/_Project/Scripts/Infrastructure/Debug/LobbyReturnPerformanceDiagnostics.cs",
            "Assets/_Project/Scripts/Infrastructure/Debug/DetectRangePerformanceDiagnostics.cs",
            "Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs",
            "Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs",
            "Assets/_Project/Scripts/Application/Combat/CombatOptimizationDiagnostics.cs",
            "Assets/_Project/Scripts/Infrastructure/Network/NetworkAttackPresentationBundle.cs",
            "Assets/_Project/Scripts/Application/Interfaces/IGameServices.cs",
            "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs",
            "Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs",
            "Assets/_Project/Scripts/Presentation/UI/UIManager.cs",
            "Assets/_Project/Scripts/Presentation/UI/Views/Lobby/LobbyRootView.cs",
            "Assets/_Project/Scripts/Infrastructure/Network/NetworkGameManager.cs",
            "Assets/_Project/Scripts/Infrastructure/Network/RelayManager.cs",
            "Assets/_Project/Scripts/Infrastructure/Network/LobbyManager.cs",
            "Assets/_Project/Scripts/Presentation/UI/ViewModels/BattleViewModel.cs",
            "Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs",
            "Assets/_Project/Scripts/Presentation/Effects/ProjectilePresentationPresenter.cs",
            "Assets/_Project/Scripts/Presentation/Effects/WavePresentationPresenter.cs",
            "Assets/_Project/Scripts/Presentation/Unit/UnitView.cs",
            "Assets/_Project/Scripts/Presentation/UI/GameEndUI.cs",
            "Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Ranking/RankingView.cs",
            "Assets/_Project/Prefabs/Tiles/HexTile.prefab",
            "Assets/_Project/Prefabs/Tiles/HexTile_RuntimeMesh.asset",
            "Assets/Editor/Tools/Combat/ExportLoadedProfilerLogSamples.cs",
            "ProjectSettings/QualitySettings.asset", "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/ProjectSettings.asset",
            "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/Mobile_Renderer.asset", "Assets/Settings/SampleSceneProfile.asset", "Assets/_Project/Scenes/Game.unity",
            "Assets/Editor/Tools/Combat/PerformanceCaptureBuildManifest.cs"
            , "Assets/Editor/Tools/Combat/ExportHostBattleClientReadiness.cs"
        };

        [Serializable] private sealed class SourceHash { public string path; public string sha256; }
        [Serializable] private sealed class Manifest
        {
            public string schema = "performance-source-prebuild-v1";
            public string stage = "prebuild-not-build-success-or-installed-app-proof";
            public string utc, buildGuid, unity, target, options, packageId, version;
            public string androidDefines;
            public bool releaseLobbyReturnMeasurement, frameTimingStats;
            public bool releaseDetectRangeComparison;
            public string hostBattleProtocol = "host-battle-v2; EnableHostBattleCapture selects manual Host mode before legacy v1";
            public string[] detectRangeMetrics;
            public string[] requestedMetrics;
            public SourceHash[] sources;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
            bool measurement = report.summary.platform == BuildTarget.Android && HasMeasurementDefine(defines);
            bool detectComparison = report.summary.platform == BuildTarget.Android && HasDetectDefine(defines);
            // 수용 조건 위반은 아래 catch에서 warning으로 삼키지 않는다.
            ValidateMeasurementBuild(report.summary.platform, report.summary.options, measurement || detectComparison);
            if (!measurement && !detectComparison && (report.summary.options & BuildOptions.Development) == 0) return;
            try
            {
                string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
                var manifest = new Manifest
                {
                    utc = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                    buildGuid = report.summary.guid.ToString(),
                    unity = UnityEngine.Application.unityVersion,
                    target = report.summary.platform.ToString(),
                    options = report.summary.options.ToString(),
                    packageId = PlayerSettings.applicationIdentifier,
                    version = PlayerSettings.bundleVersion,
                    androidDefines = defines,
                    releaseLobbyReturnMeasurement = measurement,
                    releaseDetectRangeComparison = detectComparison,
                    detectRangeMetrics = detectComparison ? new[] { "Host v2: preceding Update cadence including waits", "NCC TickCombat wall ticks/current frame; not CPU self", "Alive Blue/Red counts at1Hz and damage-event counts", "Optional CPU/GPU/GC/memory recorder runtime probe; unsupported explicit", "Supplementary frozen-query pairs; no FPS improvement claim" } : Array.Empty<string>(),
                    frameTimingStats = PlayerSettings.enableFrameTimingStats,
                    requestedMetrics = measurement ? new[] { "Update realtime interval (includes waits)", "GC.CollectionCount(0)", "GC Used Memory (if available)", "GC Reserved Memory (if available)" } : Array.Empty<string>(),
                    sources = new SourceHash[Sources.Length]
                };
                using (SHA256 hash = SHA256.Create())
                    for (int i = 0; i < Sources.Length; i++)
                        manifest.sources[i] = new SourceHash
                        {
                            path = Sources[i],
                            sha256 = BitConverter.ToString(hash.ComputeHash(
                                File.ReadAllBytes(Path.Combine(root, Sources[i])))).Replace("-", "").ToLowerInvariant()
                        };
                string directory = Path.Combine(root, "Library/ProfilerDiagnostics");
                Directory.CreateDirectory(directory);
                // 같은 GUID라도 시도별로 구분하여 기존 근거 파일을 덮어쓰지 않는다.
                string path = Path.Combine(directory, "source-prebuild-" + manifest.buildGuid + "-"
                    + DateTime.UtcNow.Ticks + ".json");
                File.WriteAllText(path, JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
                Debug.Log("Performance source prebuild manifest recorded (not build success): " + path);
            }
            catch (Exception error)
            {
                if (measurement || detectComparison) throw new BuildFailedException("Release measurement source manifest unavailable: " + error.GetType().Name);
                Debug.LogWarning("Performance source manifest unavailable: " + error.GetType().Name);
            }
        }
    }
}
