using System;
using System.Collections.Generic;
using Hexiege.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Hexiege.EditorTools
{
    /// <summary>Bloom 전용 native clip 후반 retime. 모든 입력/샘플 검증 뒤에만 에셋을 저장한다.</summary>
    public static class RetimingBloomFairyHealPresentation
    {
        private const string Root = "Assets/_Project/";
        private const string ClipPath = Root + "Animations/Units/BloomFairy/BloomFairy_Attack.anim";
        private static readonly string[] PrefabPaths = {
            Root + "Prefabs/Units/Transcendence/Unit_BloomFairy_Blue.prefab",
            Root + "Prefabs/Units/Transcendence/Unit_BloomFairy_Red.prefab"
        };

        /// <summary>메인이 Edit Mode에서 실행한다. 재실행 시 이미 4초인 clip은 다시 늘리지 않는다.</summary>
        [MenuItem("Hexiege/Setup/Retiming BloomFairy Heal Presentation (4s)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Bloom retime requires Edit Mode.");
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            Check(clip != null && clip.isHumanMotion && clip.isLooping
                && Mathf.Abs(clip.frameRate - 30) < 0.0001f, "Expected native looping 30fps humanoid clip.");
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            Check(events.Length == 1 && events[0].functionName == "OnAttackHit"
                && Mathf.Abs(events[0].time - 1) < 0.0001f, "Single marker must stay at 1s.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                Root + "Animations/Units/BloomFairy/BloomFairy.controller");
            Check(controller != null, "Missing production controller.");
            int attacks = 0;
            foreach (var layer in controller.layers)
                foreach (var child in layer.stateMachine.states)
                    if (layer.name == "Base Layer" && child.state.name == "Attack")
                    {
                        attacks++;
                        Check(child.state.motion == clip && Mathf.Approximately(child.state.speed, 1),
                            "Production Attack binding/speed mismatch.");
                    }
            Check(attacks == 1, "Expected one production Attack state.");
            var prefabs = new List<GameObject>();
            var previousBlends = new List<float>();
            foreach (string path in PrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(prefab != null && prefab.GetComponent<UnitView>() != null, "Missing Bloom prefab/View.");
                Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
                Check(animators.Length == 1 && animators[0].runtimeAnimatorController == controller
                    && !animators[0].applyRootMotion, "Prefab controller/root motion mismatch.");
                Check(new SerializedObject(prefab.GetComponent<UnitView>()).FindProperty("_healCastBlend") != null,
                    "Reload latest UnitView before running setup.");
                prefabs.Add(prefab);
                previousBlends.Add(new SerializedObject(prefab.GetComponent<UnitView>())
                    .FindProperty("_healCastBlend").floatValue);
            }

            bool needsRetime = Mathf.Abs(clip.length - 3) < 0.0001f;
            Check(needsRetime || Mathf.Abs(clip.length - 4) < 0.0001f, "Only source 3s or already-retimed 4s is accepted.");
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            Check(bindings.Length > 0 && AnimationUtility.GetObjectReferenceCurveBindings(clip).Length == 0,
                "Unsupported empty/object-reference clip.");
            var source = new AnimationCurve[bindings.Length];
            var retimed = new AnimationCurve[bindings.Length];
            AnimationClip staged = UnityEngine.Object.Instantiate(clip);
            bool commitStarted = false;
            try
            {
                // 먼저 memory clone 전체에 적용한다. 불완전 curve/metadata는 원본에 쓰기 전에 거부한다.
                for (int i = 0; i < bindings.Length; i++)
                {
                    source[i] = AnimationUtility.GetEditorCurve(clip, bindings[i]);
                    if (!needsRetime) continue;
                    retimed[i] = RetimeCurve(source[i]);
                    AnimationUtility.SetEditorCurve(staged, bindings[i], retimed[i]);
                    ValidateSamples(source[i], AnimationUtility.GetEditorCurve(staged, bindings[i]));
                }
                if (needsRetime) SetStopTime(staged, 4);
                Check(Mathf.Abs(staged.length - 4) < 0.0001f && staged.isLooping,
                    "Native curve and clip stop-time must both end at 4s.");

                Undo.RegisterCompleteObjectUndo(clip, "Retime Bloom heal after marker");
                commitStarted = true;
                if (needsRetime)
                {
                    for (int i = 0; i < bindings.Length; i++)
                        AnimationUtility.SetEditorCurve(clip, bindings[i], AnimationUtility.GetEditorCurve(staged, bindings[i]));
                    SetStopTime(clip, 4);
                    AnimationUtility.SetAnimationEvents(clip, events);
                    for (int i = 0; i < bindings.Length; i++)
                        ValidateSamples(source[i], AnimationUtility.GetEditorCurve(clip, bindings[i]));
                    EditorUtility.SetDirty(clip);
                }
                foreach (GameObject prefab in prefabs)
                {
                    UnitView view = prefab.GetComponent<UnitView>();
                    Undo.RecordObject(view, "Bloom heal-only blend");
                    var serialized = new SerializedObject(view);
                    serialized.FindProperty("_healCastBlend").floatValue = 0.15f;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(view);
                    PrefabUtility.SavePrefabAsset(prefab);
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"[BloomHealSetup] native retime PASS: changed={needsRetime}, curves={bindings.Length}, "
                    + "front/tail samples preserved, clip=4s, marker=1s, both heal blends=0.15s. Not visual PASS.");
            }
            catch
            {
                // 변환 검증이 예외를 내면 저장 전 원본 curve/metadata만 복구한다.
                if (commitStarted && needsRetime)
                {
                    for (int i = 0; i < bindings.Length; i++)
                        if (source[i] != null) AnimationUtility.SetEditorCurve(clip, bindings[i], source[i]);
                    SetStopTime(clip, 3);
                    AnimationUtility.SetAnimationEvents(clip, events);
                }
                if (commitStarted)
                    for (int i = 0; i < prefabs.Count; i++)
                    {
                        var restored = new SerializedObject(prefabs[i].GetComponent<UnitView>());
                        restored.FindProperty("_healCastBlend").floatValue = previousBlends[i];
                        restored.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SavePrefabAsset(prefabs[i]);
                    }
                throw;
            }
            finally { UnityEngine.Object.DestroyImmediate(staged); }
        }

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

        private static void SetStopTime(AnimationClip clip, float time)
        {
            var serialized = new SerializedObject(clip);
            var settings = serialized.FindProperty("m_AnimationClipSettings");
            Check(settings != null && settings.FindPropertyRelative("m_StopTime") != null, "Native clip metadata unavailable.");
            settings.FindPropertyRelative("m_StopTime").floatValue = time;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    }
}
