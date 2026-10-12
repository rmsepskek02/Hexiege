using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Hexiege.EditorTools
{
    /// <summary>원본을 저장하지 않고 고정 seed의 실제 입자 경로와 크기를 측정한다.</summary>
    public static class TorrentWaveCalibrationTool
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/VFX/Units/vfx_torrentspirit_attack.prefab";
        private const string PresetPath = "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_TorrentSpirit_Attack.asset";
        private static readonly uint[] Seeds = { 1701, 2903, 4517, 7919 };
        private static readonly uint[] HoldoutSeeds = { 10007, 13001, 19001, 23003 };
        private const float Step = 1f / 120f;
        private const float Duration = .5f;
        private const float Distance = 3f;
        private static readonly ParticleSystemBakeMeshOptions[] BakeOptions = {
            (ParticleSystemBakeMeshOptions)0, ParticleSystemBakeMeshOptions.BakePosition,
            ParticleSystemBakeMeshOptions.BakeRotationAndScale,
            ParticleSystemBakeMeshOptions.BakePosition | ParticleSystemBakeMeshOptions.BakeRotationAndScale };

        /// <summary>알려진 단일 입자로 bake 옵션별 좌표계와 Transform 갱신 반응을 진단한다.</summary>
        [MenuItem("Hexiege/Combat/Torrent Wave/Diagnose Bake Coordinates")]
        public static void DiagnoseBakeCoordinates()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Coordinate diagnosis requires Edit Mode.");
            string hash = HashSources();
            GameObject instance = null, cameraObject = null;
            Mesh mesh = null;
            var csv = new StringBuilder("scaling,options,pose,refresh,vertexCount,rawCenterX,rawCenterY,rawCenterZ,expectedX,expectedY,expectedZ,worldError,translationOnlyError,rotationTranslationError,transformPointError,rootX,rootY,rootZ,psX,psY,psZ,systemTime,particleCount\n");
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Missing production prefab.");
                instance = UnityEngine.Object.Instantiate(prefab);
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                    child.gameObject.hideFlags = HideFlags.HideAndDontSave;
                ParticleSystem control = null;
                foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    if (ps.name == "Main_Water_Surge") control = ps;
                }
                if (control == null) throw new InvalidOperationException("Missing Main_Water_Surge coordinate control.");
                var renderer = control.GetComponent<ParticleSystemRenderer>();
                if (renderer.renderMode != ParticleSystemRenderMode.Billboard || renderer.pivot != Vector3.zero)
                    throw new InvalidOperationException("Coordinate control must be centered production billboard.");
                cameraObject = new GameObject("Torrent coordinate camera") { hideFlags = HideFlags.HideAndDontSave };
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 8f; camera.aspect = 9f / 16f;
                camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
                camera.transform.position = -camera.transform.forward * 20f;
                mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                Vector3 knownPosition = new Vector3(.371f, .293f, .617f);
                var particle = new ParticleSystem.Particle {
                    position = knownPosition, velocity = Vector3.zero, startLifetime = 10f,
                    remainingLifetime = 10f, startSize = .2f, startColor = Color.white, randomSeed = 1701u };
                var main = control.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                // 복제본만 조작한다. 원본 prefab의 curve/renderer/shape는 저장하지 않는다.
                foreach (var scaling in new[] { ParticleSystemScalingMode.Local, ParticleSystemScalingMode.Hierarchy })
                {
                    main.scalingMode = scaling;
                    foreach (var options in BakeOptions)
                    {
                        SetProbePose(instance.transform, control.transform, 0);
                        control.Simulate(0f, false, true, false);
                        control.SetParticles(new[] { particle }, 1);
                        for (int pose = 0; pose < 6; pose++)
                        {
                            SetProbePose(instance.transform, control.transform, pose);
                            // 갱신 전/후를 둘 다 기록해 renderer cache와 로컬 좌표를 구분한다.
                            DiagnoseSnapshot(csv, control, renderer, camera, mesh, options, scaling, pose, "none", knownPosition);
                            RefreshWithoutParticleChange(control);
                            DiagnoseSnapshot(csv, control, renderer, camera, mesh, options, scaling, pose, "simulate-zero", knownPosition);
                        }
                    }
                }
                string folder = Path.GetFullPath("Library/TorrentWaveMeasurements");
                Directory.CreateDirectory(folder);
                string output = Path.Combine(folder, "coordinates-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".csv");
                File.WriteAllText(output, csv.ToString(), new UTF8Encoding(false));
                Debug.Log("[TORRENT-COORD] Known production billboard particle; Local/Hierarchy x four bake options x six poses x before/after zero-step refresh. CSV=" + output
                    + ". Zero-step refresh verified unchanged particle count/position/velocity/lifetime/size/rotation/color/seed and system time. Compare candidate conversion errors across ALL poses, not identity pose alone. Existing raw-world width/front calibration remains UNVERIFIED; no numeric calibration is installed.");
            }
            finally
            {
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (hash != HashSources()) throw new InvalidOperationException("Source changed during coordinate diagnosis.");
            }
        }

        private static void SetProbePose(Transform root, Transform ps, int pose)
        {
            root.SetPositionAndRotation(new Vector3(1.7f, .8f, 2.3f), Quaternion.identity);
            root.localScale = Vector3.one;
            ps.localPosition = Vector3.zero; ps.localRotation = Quaternion.identity; ps.localScale = Vector3.one;
            if (pose == 1) root.position += new Vector3(.731f, .193f, .917f);
            if (pose == 2) ps.position += new Vector3(.419f, .127f, .683f);
            if (pose == 3) root.rotation = Quaternion.Euler(0f, 37f, 0f);
            if (pose == 4) root.localScale = new Vector3(.5f, 1f, 1f);
            if (pose == 5)
            {
                root.rotation = Quaternion.Euler(0f, 37f, 0f);
                root.localScale = new Vector3(.5f, 1.2f, .8f);
                ps.localPosition = new Vector3(.2f, .1f, .3f);
                ps.localRotation = Quaternion.Euler(0f, 17f, 0f);
            }
        }

        private static void DiagnoseSnapshot(StringBuilder csv, ParticleSystem ps, ParticleSystemRenderer renderer,
            Camera camera, Mesh mesh, ParticleSystemBakeMeshOptions options, ParticleSystemScalingMode scaling,
            int pose, string refresh, Vector3 knownPosition)
        {
            mesh.Clear();
            renderer.BakeMesh(mesh, camera, options);
            var vertices = new List<Vector3>(); mesh.GetVertices(vertices);
            if (vertices.Count != 4) throw new InvalidOperationException("Coordinate control must bake exactly one quad; vertices=" + vertices.Count);
            Vector3 rawCenter = Vector3.zero;
            foreach (var v in vertices) rawCenter += v;
            rawCenter /= vertices.Count;
            // 정상화된 단일 입자 좌표는 별도 입력값이다. bake 결과로 expected를 만들지 않는다.
            Vector3 expected = ps.transform.TransformPoint(knownPosition);
            float worldError = Vector3.Distance(rawCenter, expected);
            float positionError = Vector3.Distance(rawCenter + ps.transform.position, expected);
            float rotationError = Vector3.Distance(ps.transform.position + ps.transform.rotation * rawCenter, expected);
            float transformError = Vector3.Distance(ps.transform.TransformPoint(rawCenter), expected);
            csv.AppendFormat(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5:F8},{6:F8},{7:F8},{8:F8},{9:F8},{10:F8},{11:F8},{12:F8},{13:F8},{14:F8},{15:F8},{16:F8},{17:F8},{18:F8},{19:F8},{20:F8},{21:F8},{22}\n",
                scaling, (int)options, pose, refresh, vertices.Count, rawCenter.x, rawCenter.y, rawCenter.z,
                expected.x, expected.y, expected.z, worldError, positionError, rotationError, transformError,
                ps.transform.parent.position.x, ps.transform.parent.position.y, ps.transform.parent.position.z,
                ps.transform.position.x, ps.transform.position.y, ps.transform.position.z, ps.time, ps.particleCount);
            if (pose == 0 || pose == 5)
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[TORRENT-COORD] scaling={0} options={1} pose={2} refresh={3} raw={4} expected={5} errors world={6:F8} +position={7:F8} rotation+position={8:F8} TransformPoint={9:F8}; single production billboard control, NOT visual bounds.",
                    scaling, (int)options, pose, refresh, rawCenter, expected, worldError, positionError, rotationError, transformError));
        }

        private static void RefreshWithoutParticleChange(ParticleSystem ps)
        {
            var before = new ParticleSystem.Particle[Mathf.Max(1, ps.particleCount)];
            int count = ps.GetParticles(before);
            float time = ps.time;
            ps.Simulate(0f, false, false, false);
            var after = new ParticleSystem.Particle[Mathf.Max(1, ps.particleCount)];
            if (ps.GetParticles(after) != count || !ps.time.Equals(time))
                throw new InvalidOperationException("Zero-step refresh changed particle count/time: " + ps.name);
            for (int i = 0; i < count; i++)
            {
                var a = before[i]; var b = after[i];
                if (!a.position.Equals(b.position) || !a.velocity.Equals(b.velocity)
                    || !a.remainingLifetime.Equals(b.remainingLifetime) || !a.startLifetime.Equals(b.startLifetime)
                    || !a.startSize3D.Equals(b.startSize3D) || !a.rotation3D.Equals(b.rotation3D)
                    || !a.angularVelocity3D.Equals(b.angularVelocity3D) || !a.startColor.Equals(b.startColor)
                    || a.randomSeed != b.randomSeed)
                    throw new InvalidOperationException("Zero-step refresh changed particle state: " + ps.name + " index=" + i);
            }
        }

        // 병행 구현이 아직 컴파일되지 않았어도 기존 baseline 메뉴가 사라지지 않게 한다.
        // reflection은 생산 메서드 호출에만 사용하며 대체 입자 보정식을 만들지 않는다.
        private sealed class RuntimeContract
        {
            internal readonly Type Pool, Key, Spawn, Calibration, Coordinator, View;
            internal readonly MethodInfo Play, Advance, Awake;
            internal readonly object Production;
            internal RuntimeContract()
            {
                Pool = RequireType("Hexiege.Presentation.VfxPoolItem");
                Key = RequireType("Hexiege.Application.WaveDeliveryKey");
                Spawn = RequireType("Hexiege.Application.WavePresentationSpawn");
                Calibration = RequireType("Hexiege.Presentation.WaveVisualCalibration");
                Coordinator = RequireType("Hexiege.Application.WavePresentationCoordinator");
                View = RequireType("Hexiege.Core.ViewConverter");
                Play = Pool.GetMethod("PlayWave", new[] { Spawn, typeof(Func<double>), typeof(Vector3), typeof(Vector3), typeof(Vector3), Calibration });
                Advance = Pool.GetMethod("AdvanceWaveToAge", new[] { typeof(float) });
                Awake = Pool.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                Production = Calibration.GetProperty("Production", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (Play == null || Advance == null || Awake == null || Production == null)
                    throw new InvalidOperationException("Production wave helper contract unavailable. Compile runtime first; no approximation is used.");
            }
            internal object Candidate(float lateral, float shift, bool hierarchy = true)
            {
                return Activator.CreateInstance(Calibration, lateral, -shift, hierarchy);
            }
            internal Component Start(GameObject instance, uint seed, object calibration, Vector3 viewForward)
            {
                Vector3 authoredScale = instance.transform.localScale;
                AssertNonzeroScale(authoredScale, "before AddComponent");
                var pool = instance.GetComponent(Pool) ?? instance.AddComponent(Pool);
                // EditMode는 MonoBehaviour.Awake를 보장하지 않는다. 실제 초기화 메서드를 명시 호출한다.
                Awake.Invoke(pool, null);
                var scaleCache = Pool.GetField("_originalLocalScale", BindingFlags.Instance | BindingFlags.NonPublic);
                if (scaleCache == null || !((Vector3)scaleCache.GetValue(pool)).Equals(authoredScale))
                    throw new InvalidOperationException("EditMode pool Awake did not initialize authored scale cache; runtime measurement refused.");
                AssertNonzeroScale((Vector3)scaleCache.GetValue(pool), "after Awake cache");
                object key = Activator.CreateInstance(Key, 1UL, (ulong)seed);
                bool flipped = (bool)View.GetProperty("IsFlipped").GetValue(null);
                // 뷰 원점에 보이도록 canonical 원점을 역변환한다. Red의 비영점 mapCenter도 실제로 거친다.
                Vector3 origin = (Vector3)View.GetMethod("FromView").Invoke(null, new object[] { Vector3.zero });
                Vector3 convertedOrigin = (Vector3)View.GetMethod("ToView").Invoke(null, new object[] { origin });
                if (convertedOrigin.sqrMagnitude > .00000001f)
                    throw new InvalidOperationException("Production view-origin round trip failed.");
                object spawn = Activator.CreateInstance(Spawn, key, 0, origin, flipped ? -viewForward : viewForward,
                    3f, Distance, Duration, 0d);
                Func<double> clock = () => 0d;
                Play.Invoke(pool, new object[] { spawn, clock, new Vector3(0f, .05f, .4f), Vector3.zero, Vector3.one, calibration });
                AssertNonzeroScale(instance.transform.localScale, "after production PlayWave");
                if (Vector3.Dot(instance.transform.forward, viewForward) < .9999f)
                    throw new InvalidOperationException("Production wave facing differs from requested view direction; possible double flip.");
                return pool;
            }
            private static void AssertNonzeroScale(Vector3 scale, string stage)
            {
                if (scale.x == 0f || scale.y == 0f || scale.z == 0f
                    || float.IsNaN(scale.x) || float.IsNaN(scale.y) || float.IsNaN(scale.z)
                    || float.IsInfinity(scale.x) || float.IsInfinity(scale.y) || float.IsInfinity(scale.z))
                    throw new InvalidOperationException("Invalid EditMode pool scale " + stage + ": " + scale);
            }
            private static Type RequireType(string name)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType(name);
                    if (type != null) return type;
                }
                throw new InvalidOperationException("Runtime type not compiled: " + name);
            }
        }

        private struct Envelope
        {
            internal bool Valid;
            internal Vector3 Min, Max;
            internal void Include(Vector3 p)
            {
                if (!Valid) { Min = Max = p; Valid = true; }
                else { Min = Vector3.Min(Min, p); Max = Vector3.Max(Max, p); }
            }
            internal void Include(Envelope b)
            {
                if (b.Valid) { Include(b.Min); Include(b.Max); }
            }
        }

        private sealed class Result
        {
            internal float Width, Height, HalfWidth;
            internal float FrontMin = float.PositiveInfinity, FrontMax = float.NegativeInfinity;
            internal int CoreSamples;
            internal float FirstCoreTime = float.PositiveInfinity;
            internal float RateMin = float.PositiveInfinity, RateMax = float.NegativeInfinity;
            private float _previousTime = -1f, _previousFront;
            internal void Sample(Envelope union, Envelope core, float time)
            {
                if (union.Valid)
                {
                    Width = Mathf.Max(Width, union.Max.x - union.Min.x);
                    Height = Mathf.Max(Height, union.Max.y - union.Min.y);
                    HalfWidth = Mathf.Max(HalfWidth, Mathf.Max(Mathf.Abs(union.Min.x), Mathf.Abs(union.Max.x)));
                }
                if (!core.Valid || time > Duration) return;
                float residual = core.Max.z - Distance * Mathf.Clamp01(time / Duration);
                FrontMin = Mathf.Min(FrontMin, residual);
                FrontMax = Mathf.Max(FrontMax, residual);
                FirstCoreTime = Mathf.Min(FirstCoreTime, time);
                // 시드 경계에서는 시간이 되감기므로 서로 다른 재생 사이 속도를 계산하지 않는다.
                if (_previousTime >= 0f && time > _previousTime)
                {
                    float rate = (core.Max.z - _previousFront) / (time - _previousTime);
                    RateMin = Mathf.Min(RateMin, rate);
                    RateMax = Mathf.Max(RateMax, rate);
                }
                _previousTime = time;
                _previousFront = core.Max.z;
                CoreSamples++;
            }
        }

        /// <summary>숨겨진 임시 인스턴스만 만들며 scene/prefab/clip을 저장하지 않는다.</summary>
        [MenuItem("Hexiege/Combat/Torrent Wave/Measure Seeded Production Particles")]
        public static void Measure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Torrent measurement requires Edit Mode.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Torrent production prefab is missing.");
            ValidatePreset();
            string sourceHash = HashSources();
            var csv = new StringBuilder("phase,seed,time,system,space,renderMode,alignment,scalingMode,count,minX,maxX,minY,maxY,minZ,maxZ,rootZ,coreFrontResidual,boundsKind\n");
            GameObject cameraObject = null;
            Result original, corrected, holdoutOriginal, holdoutCorrected;
            float widthScale = 1f, frontShift;
            try
            {
                // 현재 Login 씬 카메라 대신 게임의 55도 직교 관점을 고정한다. 씬은 저장하지 않는다.
                cameraObject = new GameObject("Torrent measurement camera") { hideFlags = HideFlags.HideAndDontSave };
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 8f;
                camera.aspect = 9f / 16f;
                camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
                camera.transform.position = new Vector3(0f, 0f, 1.5f) - camera.transform.forward * 20f;
                original = Run(prefab, camera, Seeds, "original-fit", 1f, 0f, csv);
                // 카메라 방향을 따르는 billboard 크기는 root X 배율에 단순 비례하지 않는다.
                // 따라서 3/폭을 확정값으로 쓰지 않고 실제 재시뮬레이션으로 후보를 반복 측정한다.
                var fit = original;
                for (int i = 0; i < 6; i++)
                {
                    if (fit.HalfWidth <= 0f) throw new InvalidOperationException("Empty union measurement.");
                    widthScale *= Mathf.Clamp(1.5f / fit.HalfWidth, .1f, 2f);
                    fit = Run(prefab, camera, Seeds, "width-fit-" + i, widthScale, 0f, null);
                }
                if (fit.CoreSamples == 0) throw new InvalidOperationException("Front_Foam_Crest has no measured geometry.");
                // 장식 spray가 아닌 crest 전선의 최대/최소 잔차 중간을 원점으로 맞춘다.
                // 상수 offset만으로 시간별 잔차가 사라지지는 않는다. 서버 속도 6은 바꾸지 않는다.
                frontShift = -(fit.FrontMin + fit.FrontMax) * .5f;
                corrected = Run(prefab, camera, Seeds, "corrected-fit", widthScale, frontShift, csv);
                holdoutOriginal = Run(prefab, camera, HoldoutSeeds, "original-holdout", 1f, 0f, csv);
                holdoutCorrected = Run(prefab, camera, HoldoutSeeds, "corrected-holdout", widthScale, frontShift, csv);
            }
            finally
            {
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (sourceHash != HashSources()) throw new InvalidOperationException("Torrent source hash changed during measurement; results invalid.");
            }
            string folder = Path.GetFullPath("Library/TorrentWaveMeasurements");
            Directory.CreateDirectory(folder);
            string output = Path.Combine(folder, "calibrated-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".csv");
            File.WriteAllText(output, csv.ToString(), new UTF8Encoding(false));
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[TORRENT-MEASURE] static-root-candidate scale=({0:F6},1,1) offset=(0,0.05,{1:F6}) travel=3/0.5 speed=6; sourceHashUnchanged={2}; CSV={3}. NOT runtime particle-remap validation; no alpha silhouette/shader displacement/trail measurement; fixed camera=55deg ortho8 aspect9:16; sampled at 120Hz for 1s; source local-deltaTime travel is modelled with fixed steps; fitSeeds=1701/2903/4517/7919 holdoutSeeds=10007/13001/19001/23003; holdout NOT used for fit.",
                widthScale, .4f + frontShift, sourceHash, output));
            Report("original-fit", original);
            Report("corrected-fit", corrected);
            Report("original-holdout", holdoutOriginal);
            Report("corrected-holdout", holdoutCorrected);
        }

        /// <summary>실제 생산 파도 helper의 identity·후보·현재 Production 설정을 독립 시드로 측정한다.</summary>
        [MenuItem("Hexiege/Combat/Torrent Wave/Measure Production Runtime Helper")]
        public static void MeasureRuntimeHelper()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Torrent measurement requires Edit Mode.");
            ValidatePreset();
            var runtime = new RuntimeContract();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Missing production prefab.");
            string hash = HashSources(true);
            var csv = new StringBuilder("phase,seed,time,system,space,renderMode,alignment,scalingMode,count,minX,maxX,minY,maxY,minZ,maxZ,rootZ,coreFrontResidual,boundsKind\n");
            GameObject cameraObject = null;
            try
            {
                cameraObject = new GameObject("Torrent runtime measurement camera") { hideFlags = HideFlags.HideAndDontSave };
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 8f; camera.aspect = 9f / 16f;
                camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
                camera.transform.position = new Vector3(0f, 0f, 1.5f) - camera.transform.forward * 20f;
                // 원본은 authored Local 모드를 보존한다. 후보만 생산 계약의 size scaling을 요청한다.
                object identity = Activator.CreateInstance(runtime.Calibration, 1f, 0f, false);
                // before는 기존 authored 시뮬레이션, after는 반드시 생산 진입점이다.
                var baseline = RunOriginal(prefab, camera, Seeds, "before-original-fit", csv);
                var originalHoldout = RunOriginal(prefab, camera, HoldoutSeeds, "before-original-holdout", csv);
                var runtimeIdentity = RunProduction(prefab, camera, Seeds, "runtime-identity-fit", csv, runtime, identity);
                var hierarchyIdentity = RunProduction(prefab, camera, Seeds, "hierarchy-identity-fit", csv, runtime, runtime.Candidate(1f, 0f, true));
                // 동일 X 배율로만 비교한다. offset 보정이나 재fit을 섞지 않는 대조 측정이다.
                if (runtimeIdentity.HalfWidth <= 0f) throw new InvalidOperationException("Empty runtime comparison geometry.");
                float comparisonScale = 1.5f / runtimeIdentity.HalfWidth;
                var localComparison = RunProduction(prefab, camera, Seeds, "local-x-comparison-fit", csv, runtime, runtime.Candidate(comparisonScale, 0f, false));
                var hierarchyComparison = RunProduction(prefab, camera, Seeds, "hierarchy-x-comparison-fit", csv, runtime, runtime.Candidate(comparisonScale, 0f, true));
                var localComparisonHoldout = RunProduction(prefab, camera, HoldoutSeeds, "local-x-comparison-holdout", csv, runtime, runtime.Candidate(comparisonScale, 0f, false));
                var hierarchyComparisonHoldout = RunProduction(prefab, camera, HoldoutSeeds, "hierarchy-x-comparison-holdout", csv, runtime, runtime.Candidate(comparisonScale, 0f, true));
                Report("hierarchy-identity-fit", hierarchyIdentity);
                Report("local-x-comparison-fit", localComparison); Report("hierarchy-x-comparison-fit", hierarchyComparison);
                Report("local-x-comparison-holdout", localComparisonHoldout); Report("hierarchy-x-comparison-holdout", hierarchyComparisonHoldout);
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[TORRENT-MEASURE] Local-vs-Hierarchy controlled comparison scaleX={0:F6} frontAnchor=0; same seeds/clock/camera/runtime helpers. fit unionWidth Local={1:F6} Hierarchy={2:F6}; holdout Local={3:F6} Hierarchy={4:F6}. scaleX=1 widths Local={5:F6} Hierarchy={6:F6}; NO assets/Production calibration changed; no claim of visual acceptance.",
                    comparisonScale, localComparison.Width, hierarchyComparison.Width, localComparisonHoldout.Width,
                    hierarchyComparisonHoldout.Width, runtimeIdentity.Width, hierarchyIdentity.Width));
                float lateral = 1f;
                var fit = runtimeIdentity;
                bool widthResponded = false;
                for (int i = 0; i < 6; i++)
                {
                    if (fit.HalfWidth <= 0f) throw new InvalidOperationException("Empty runtime geometry.");
                    float previousHalfWidth = fit.HalfWidth;
                    lateral *= Mathf.Clamp(1.5f / fit.HalfWidth, .1f, 2f);
                    fit = RunProduction(prefab, camera, Seeds, "runtime-width-fit-" + i, csv, runtime, runtime.Candidate(lateral, 0f));
                    Report("runtime-width-fit-" + i, fit);
                    // 배율 변경이 측정 X 경계에 반영되지 않는 경로에서 후보를 계속 줄이지 않는다.
                    // 이 값은 응답 진단용일 뿐, 폭 달성 gate의 허용 오차가 아니다.
                    bool responded = Mathf.Abs(fit.HalfWidth - previousHalfWidth) > .00001f;
                    widthResponded |= responded;
                    if (!responded && fit.HalfWidth > 1.5f) break;
                }
                if (fit.CoreSamples == 0) throw new InvalidOperationException("No runtime Crest evidence.");
                float shift = -(fit.FrontMin + fit.FrontMax) * .5f;
                object candidate = runtime.Candidate(lateral, shift);
                var corrected = RunProduction(prefab, camera, Seeds, "runtime-candidate-fit", csv, runtime, candidate);
                var holdoutOriginal = RunProduction(prefab, camera, HoldoutSeeds, "runtime-identity-holdout", csv, runtime, identity);
                var holdout = RunProduction(prefab, camera, HoldoutSeeds, "runtime-candidate-holdout", csv, runtime, candidate);
                var productionFit = RunProduction(prefab, camera, Seeds, "after-production-fit", csv, runtime, runtime.Production);
                var production = RunProduction(prefab, camera, HoldoutSeeds, "after-production-holdout", csv, runtime, runtime.Production);
                if (hash != HashSources(true)) throw new InvalidOperationException("Runtime helper or source changed during measurement; output invalid.");
                string folder = Path.GetFullPath("Library/TorrentWaveMeasurements");
                Directory.CreateDirectory(folder);
                string output = Path.Combine(folder, "runtime-helper-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".csv");
                File.WriteAllText(output, csv.ToString(), new UTF8Encoding(false));
                Report("before-original-fit", baseline); Report("before-original-holdout", originalHoldout);
                Report("runtime-identity-fit", runtimeIdentity); Report("runtime-identity-holdout", holdoutOriginal);
                Report("runtime-candidate-fit", corrected); Report("runtime-candidate-holdout", holdout);
                Report("after-production-fit", productionFit); Report("after-production-holdout", production);
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[TORRENT-MEASURE] realHelper=VfxPoolItem.PlayWave/AdvanceWaveToAge candidate WaveVisualCalibration(lateralScale={0:F6},frontAnchor={1:F6},hierarchySizeScaling=true); fitWidthAchieved={2} holdoutWidthAchieved={3} (maxAbsX<=1.5; NO tolerance inflation); sourceAndRuntimeHashUnchanged={4}; CSV={5}; loadedRuntimeModule={6}. Production setting is measured separately, NOT overwritten. Height multiplier=1; same camera/seeds/120Hz as baseline. Geometry is NOT alpha-visible silhouette. Helper implementation determines whether remap exists; tool never substitutes one.",
                    lateral, -shift, corrected.HalfWidth <= 1.5f, holdout.HalfWidth <= 1.5f, hash, output, runtime.Pool.Module.ModuleVersionId));
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[TORRENT-MEASURE] widthRespondedToCandidate={0}; before/production authored-height fit={1:F6}/{2:F6} holdout={3:F6}/{4:F6}; productionWidthAchieved fit={5} holdout={6}. Height is camera55deg render-geometry evidence, NOT server volume; no Y target or height rescaling is fitted; original and runtime identity stay distinct. Candidate is not approved or installed by tool.",
                    widthResponded, baseline.Height, productionFit.Height, originalHoldout.Height, production.Height,
                    productionFit.HalfWidth <= 1.5f, production.HalfWidth <= 1.5f));
            }
            finally { if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject); }
        }

        private static Result RunOriginal(GameObject prefab, Camera camera, uint[] seeds, string phase, StringBuilder csv)
        {
            return Run(prefab, camera, seeds, phase, 1f, 0f, csv);
        }

        private static Result RunProduction(GameObject prefab, Camera camera, uint[] seeds, string phase,
            StringBuilder csv, RuntimeContract runtime, object calibration, Vector3? viewForward = null)
        {
            if (runtime == null || calibration == null) throw new InvalidOperationException("No production helper/calibration; after measurement refused.");
            return Run(prefab, camera, seeds, phase, 1f, 0f, csv, runtime, calibration, viewForward);
        }

        /// <summary>Production 값을 고정하고 다섯 방향만 측정한다. 방향별 보정값 재fit은 하지 않는다.</summary>
        [MenuItem("Hexiege/Combat/Torrent Wave/Measure Production Directions")]
        public static void MeasureProductionDirections()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Direction measurement requires Edit Mode.");
            ValidatePreset();
            var runtime = new RuntimeContract();
            bool originalFlipped = (bool)runtime.View.GetProperty("IsFlipped").GetValue(null);
            var mapCenterField = runtime.View.GetField("_mapCenter", BindingFlags.Static | BindingFlags.NonPublic);
            var setupView = runtime.View.GetMethod("Setup", new[] { typeof(bool), typeof(Vector3) });
            if (mapCenterField == null || setupView == null)
                throw new InvalidOperationException("View state restoration contract unavailable; measurement refused.");
            Vector3 originalMapCenter = (Vector3)mapCenterField.GetValue(null);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Missing production prefab.");
            string hash = HashSources(true);
            var csv = new StringBuilder("phase,seed,time,system,space,renderMode,alignment,scalingMode,count,minLateral,maxLateral,minY,maxY,minForward,maxForward,rootForward,coreFrontResidual,boundsKind\n");
            GameObject cameraObject = null;
            try
            {
                cameraObject = new GameObject("Torrent directional camera") { hideFlags = HideFlags.HideAndDontSave };
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 8f; camera.aspect = 9f / 16f;
                camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
                camera.transform.position = new Vector3(0f, 0f, 1.5f) - camera.transform.forward * 20f;
                object identity = runtime.Candidate(1f, 0f, false);
                Debug.Log("[TORRENT-DIRECTION] fixedProduction lateralScale="
                    + runtime.Calibration.GetField("LateralScale").GetValue(runtime.Production)
                    + " frontAnchor=" + runtime.Calibration.GetField("FrontAnchor").GetValue(runtime.Production)
                    + " hierarchySizeScaling=" + runtime.Calibration.GetField("HierarchySizeScaling").GetValue(runtime.Production));
                foreach (bool flipped in new[] { false, true })
                {
                    // 양쪽 role에서 같은 화면 방향을 비교하되 canonical 방향은 생산 Start에서 한 번만 반전한다.
                    // 비영점 pivot은 Red 위치 변환까지 검증한다. 카메라/표본 뷰 원점은 그대로다.
                    setupView.Invoke(null, new object[] { flipped, new Vector3(7.25f, 0f, -3.5f) });
                    foreach (float yaw in new[] { 0f, 90f, 45f, -45f, 180f })
                    {
                        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                        string label = (flipped ? "Red-" : "Blue-") + "yaw" + yaw.ToString("F0", CultureInfo.InvariantCulture);
                        // 같은 방향/시드의 identity와 고정 Production을 비교한다. 후보 조정은 전혀 없다.
                        var before = RunProduction(prefab, camera, Seeds, label + "-identity-fit", csv, runtime, identity, forward);
                        var after = RunProduction(prefab, camera, Seeds, label + "-production-fit", csv, runtime, runtime.Production, forward);
                        var beforeHoldout = RunProduction(prefab, camera, HoldoutSeeds, label + "-identity-holdout", csv, runtime, identity, forward);
                        var afterHoldout = RunProduction(prefab, camera, HoldoutSeeds, label + "-production-holdout", csv, runtime, runtime.Production, forward);
                        Report(label + "-identity-fit", before); Report(label + "-production-fit", after);
                        Report(label + "-identity-holdout", beforeHoldout); Report(label + "-production-holdout", afterHoldout);
                        Debug.Log(string.Format(CultureInfo.InvariantCulture,
                            "[TORRENT-DIRECTION] role=" + (flipped ? "Red" : "Blue") + " viewYaw={0:F0} viewForward=({1:F6},0,{2:F6}) lateralWidthAchieved fit={3} holdout={4}; centeredExcess fit={5:F6} holdout={6:F6}; authoredHeight identity/Production fit={7:F6}/{8:F6} holdout={9:F6}/{10:F6}; NO refit. Front residual bounds are reported separately, NOT exact contact synchronization.",
                            yaw, forward.x, forward.z, after.HalfWidth <= 1.5f, afterHoldout.HalfWidth <= 1.5f,
                            Mathf.Max(0f, 2f * after.HalfWidth - 3f), Mathf.Max(0f, 2f * afterHoldout.HalfWidth - 3f),
                            before.Height, after.Height, beforeHoldout.Height, afterHoldout.Height));
                    }
                }
                if (hash != HashSources(true)) throw new InvalidOperationException("Sources/helper changed during directional measurement.");
                string folder = Path.GetFullPath("Library/TorrentWaveMeasurements"); Directory.CreateDirectory(folder);
                string output = Path.Combine(folder, "directions-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".csv");
                File.WriteAllText(output, csv.ToString(), new UTF8Encoding(false));
                Debug.Log("[TORRENT-DIRECTION] CSV=" + output + "; real PlayWave/AdvanceWaveToAge, Production fixed; loadedRuntimeModule="
                    + runtime.Pool.Module.ModuleVersionId + "; sourceAndRuntimeHashUnchanged=" + hash
                    + "; axes=lateral/up/forward relative to each requested view aim, NOT world X/Z; roles=Blue(false)/Red(true), mapCenter=(7.25,0,-3.5), canonical-origin roundtrip and single direction flip checked"
                    + "; camera55deg ortho8 aspect9:16 fixed. Only these two view roles, yaw0/90/+45/-45/180 and these eight seeds are sampled; arbitrary directions/other camera/network Host-Client role swaps NOT guaranteed; geometric bounds NOT alpha silhouette; no numeric refit or asset writes.");
            }
            finally
            {
                try { setupView.Invoke(null, new object[] { originalFlipped, originalMapCenter }); }
                finally { if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject); }
            }
        }

        private static Result Run(GameObject prefab, Camera camera, uint[] seeds, string phase,
            float widthScale, float frontShift, StringBuilder csv, RuntimeContract runtime = null, object calibration = null,
            Vector3? requestedViewForward = null)
        {
            var result = new Result();
            Vector3 viewForward = requestedViewForward ?? Vector3.forward;
            Quaternion toWaveFrame = Quaternion.Inverse(Quaternion.LookRotation(viewForward));
            foreach (uint seed in seeds)
            {
                GameObject instance = null;
                Mesh mesh = null;
                try
                {
                    instance = UnityEngine.Object.Instantiate(prefab);
                    foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                        child.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    instance.transform.rotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.Scale(instance.transform.localScale, new Vector3(widthScale, 1f, 1f));
                    instance.transform.position = new Vector3(0f, .05f, .4f + frontShift);
                    var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                    if (systems.Length != 5) throw new InvalidOperationException("Production sibling system count changed; review playback contract.");
                    mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    var vertices = new List<Vector3>();
                    var probed = new HashSet<ParticleSystem>();
                    for (int i = 0; runtime == null && i < systems.Length; i++)
                    {
                        systems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                        systems[i].useAutoRandomSeed = false;
                        systems[i].randomSeed = seed + (uint)i * 101u;
                        systems[i].Simulate(0f, false, true, false);
                    }
                    Component driver = runtime?.Start(instance, seed, calibration, viewForward);
                    for (int frame = 0; frame <= 120; frame++)
                    {
                        float time = frame * Step;
                        if (runtime != null) runtime.Advance.Invoke(driver, new object[] { time });
                        else instance.transform.position = new Vector3(0f, .05f, .4f + frontShift + Distance * Mathf.Clamp01(time / Duration));
                        Envelope union = default, core = default;
                        int totalCount = 0;
                        foreach (var ps in systems)
                        {
                            if (runtime == null && frame > 0) ps.Simulate(Step, false, false, false);
                            var renderer = ps.GetComponent<ParticleSystemRenderer>();
                            if (renderer == null || !renderer.enabled) throw new InvalidOperationException("Missing active particle renderer.");
                            int count = ps.particleCount;
                            Envelope bounds = default;
                            if (count > 0)
                            {
                                BakeWorldVertices(ps, renderer, camera, mesh, vertices);
                                if (vertices.Count == 0) throw new InvalidOperationException("Live particles baked no geometry: " + ps.name);
                                foreach (var vertex in vertices)
                                {
                                    if (float.IsNaN(vertex.x) || float.IsNaN(vertex.y) || float.IsNaN(vertex.z)
                                        || float.IsInfinity(vertex.x) || float.IsInfinity(vertex.y) || float.IsInfinity(vertex.z))
                                        throw new InvalidOperationException("Nonfinite baked geometry.");
                                    bounds.Include(toWaveFrame * vertex);
                                }
                                if (csv != null && seed == seeds[0] && (phase == "original-fit" || phase == "before-original-fit" || phase == "runtime-identity-fit")
                                    && ps.main.simulationSpace == ParticleSystemSimulationSpace.Local && probed.Add(ps))
                                    ProbeWorldCoordinates(instance.transform, ps, renderer, camera, mesh, vertices, bounds);
                            }
                            union.Include(bounds);
                            totalCount += count;
                            if (ps.name == "Front_Foam_Crest") core.Include(bounds);
                            WriteRow(csv, phase, seed, time, ps.name, ps.main.simulationSpace.ToString(),
                                renderer.renderMode.ToString(), renderer.alignment.ToString(), ps.main.scalingMode.ToString(), count, bounds,
                                (toWaveFrame * instance.transform.position).z, ps.name == "Front_Foam_Crest");
                        }
                        result.Sample(union, core, time);
                        WriteRow(csv, phase, seed, time, "ALL_SYSTEM_UNION", "mixed", "mixed", "mixed", "mixed", totalCount,
                            union, (toWaveFrame * instance.transform.position).z, false);
                    }
                }
                finally
                {
                    if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                    if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                }
            }
            return result;
        }

        private static void BakeWorldVertices(ParticleSystem ps, ParticleSystemRenderer renderer, Camera camera,
            Mesh mesh, List<Vector3> worldVertices)
        {
            // 2026-10-01 coordinates-20261001-043730-837.csv: flags=0은 Local/Hierarchy
            // 모두 여섯 pose에서 PS 로컬 기준점이며 TransformPoint 1회 오차가 0이다.
            // flags를 포함한 bake 결과에 다시 TransformPoint하는 이중 변환은 하지 않는다.
            if (ps.main.simulationSpace != ParticleSystemSimulationSpace.Local)
                throw new InvalidOperationException("Bake coordinate mapping not validated for non-Local simulation: " + ps.name);
            RefreshWithoutParticleChange(ps);
            mesh.Clear();
            renderer.BakeMesh(mesh, camera, (ParticleSystemBakeMeshOptions)0);
            worldVertices.Clear();
            mesh.GetVertices(worldVertices);
            for (int i = 0; i < worldVertices.Count; i++)
                worldVertices[i] = ps.transform.TransformPoint(worldVertices[i]);
        }

        private static void ProbeWorldCoordinates(Transform root, ParticleSystem ps, ParticleSystemRenderer renderer,
            Camera camera, Mesh mesh, List<Vector3> originalVertices, Envelope originalBounds)
        {
            Vector3 position = root.position, scale = root.localScale;
            Vector3 delta = new Vector3(.731f, .193f, .917f);
            var shifted = new List<Vector3>();
            var scaled = new List<Vector3>();
            float error = 0f;
            Envelope scaledBounds = default;
            try
            {
                // simulation은 진행하지 않는다. Local 입자에서 root 평행이동만 했을 때
                // 변환 완료 정점이 같은 delta로 변해야 선택한 로컬→월드 경로가 유효하다는 근거가 된다.
                root.position = position + delta;
                BakeWorldVertices(ps, renderer, camera, mesh, shifted);
                if (shifted.Count != originalVertices.Count) throw new InvalidOperationException("Coordinate probe vertex count changed.");
                for (int i = 0; i < shifted.Count; i++)
                    error = Mathf.Max(error, (shifted[i] - originalVertices[i] - delta).magnitude);
                if (error > .0001f)
                    throw new InvalidOperationException("Bake world-coordinate translation probe failed: " + ps.name + " error=" + error);
                root.position = position;
                root.localScale = new Vector3(scale.x * .5f, scale.y, scale.z);
                BakeWorldVertices(ps, renderer, camera, mesh, scaled);
                foreach (var vertex in scaled) scaledBounds.Include(vertex);
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[TORRENT-MEASURE] coordinate-probe system={0} space={1} scaling={2} rootShift=(0.731,0.193,0.917) maxVertexTranslationError={3:F8} rootXHalfWidthBefore={4:F6} after={5:F6}; BakeFlags=0 + PS.TransformPoint ONCE; zero-step transform refresh, particle state/time equality verified; geometry diagnostic only, NOT visual acceptance; camera55deg; deltaNorm=1.1884860117.",
                    ps.name, ps.main.simulationSpace, ps.main.scalingMode, error,
                    originalBounds.Max.x - originalBounds.Min.x, scaledBounds.Max.x - scaledBounds.Min.x));
            }
            finally
            {
                root.position = position;
                root.localScale = scale;
                RefreshWithoutParticleChange(ps);
            }
        }

        private static void WriteRow(StringBuilder csv, string phase, uint seed, float time, string name,
            string space, string mode, string alignment, string scaling, int count, Envelope b, float rootZ, bool core)
        {
            if (csv == null) return;
            string residual = core && b.Valid ? (b.Max.z - Distance * Mathf.Clamp01(time / Duration)).ToString("F6", CultureInfo.InvariantCulture) : "";
            string geometry = b.Valid ? string.Format(CultureInfo.InvariantCulture, "{0:F6},{1:F6},{2:F6},{3:F6},{4:F6},{5:F6}",
                b.Min.x, b.Max.x, b.Min.y, b.Max.y, b.Min.z, b.Max.z) : ",,,,,";
            csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2:F6},{3},{4},{5},{6},{7},{8},{9},{10:F6},{11},bake-flags0-PS-local-to-world-once-wave-frame-not-alpha-silhouette\n",
                phase, seed, time, name, space, mode, alignment, scaling, count, geometry, rootZ, residual);
        }

        private static void Report(string label, Result r)
        {
            Debug.Log("[TORRENT-MEASURE] coordinateMapping=v2: flags0 + PS.TransformPoint once; previous raw-world calibration is not accepted evidence.");
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[TORRENT-MEASURE] {0} unionWidth={1:F6} authoredHeight={2:F6} maxAbsX={3:F6} centeredWidthExcess={4:F6} crestFrontResidual=[{5:F6},{6:F6}] coreSamples={7} firstCoreTime={8:F6} crestEdgeRate=[{9:F6},{10:F6}]; geometry bounds only; heightScale=1 (no server Y volume); crest extrema rate is NOT individual particle speed; no core evidence before firstCoreTime.",
                label, r.Width, r.Height, r.HalfWidth, Mathf.Max(0f, 2f * r.HalfWidth - 3f), r.FrontMin, r.FrontMax, r.CoreSamples,
                r.FirstCoreTime, r.RateMin, r.RateMax));
        }

        private static void ValidatePreset()
        {
            // 측정 조건과 현재 직렬화된 프리셋 값이 다르면 오래된 조건의 결과를 내지 않는다.
            var preset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PresetPath);
            if (preset == null) throw new InvalidOperationException("Torrent production preset is missing.");
            var serialized = new SerializedObject(preset);
            if (serialized.FindProperty("_localPositionOffset").vector3Value != new Vector3(0f, .05f, .4f)
                || serialized.FindProperty("_scaleMultiplier").vector3Value != Vector3.one
                || serialized.FindProperty("_eulerRotationOffset").vector3Value != Vector3.zero
                || !Mathf.Approximately(serialized.FindProperty("_forwardTravelDistance").floatValue, Distance)
                || !Mathf.Approximately(serialized.FindProperty("_forwardTravelDuration").floatValue, Duration)
                || AssetDatabase.GetAssetPath(serialized.FindProperty("_vfxPrefab").objectReferenceValue) != PrefabPath)
                throw new InvalidOperationException("Production preset changed; review measurement contract first.");
        }

        private static string HashSources(bool includeRuntime = false)
        {
            // 프리팹·프리셋과 모든 의존 에셋/메타를 전후 비교한다. 저장 API는 호출하지 않는다.
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string source in new[] { PrefabPath, PresetPath })
                foreach (string dependency in AssetDatabase.GetDependencies(source, true))
                {
                    if (File.Exists(dependency)) paths.Add(dependency);
                    if (File.Exists(dependency + ".meta")) paths.Add(dependency + ".meta");
                }
            if (includeRuntime)
                foreach (string path in new[] {
                    "Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.cs",
                    "Assets/_Project/Scripts/Presentation/Effects/WaveVisualCalibration.cs",
                    "Assets/_Project/Scripts/Application/Combat/WavePresentationContracts.cs" })
                {
                    paths.Add(path);
                    if (File.Exists(path + ".meta")) paths.Add(path + ".meta");
                }
            using (var sha = SHA256.Create())
            {
                foreach (string path in paths)
                {
                    byte[] label = Encoding.UTF8.GetBytes(path + "\n");
                    sha.TransformBlock(label, 0, label.Length, label, 0);
                    byte[] bytes = File.ReadAllBytes(path);
                    sha.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", "");
            }
        }
    }
}
