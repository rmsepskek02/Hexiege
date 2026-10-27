// ============================================================================
// EagleArcherArrowTracerSetup.cs
// [1회성 Editor 스크립트 — 2026-10-27 서버 권위 투사체 단계 E, 결정 E-A (가) + 팀 색 ②(가)]
//
// 무엇을 하나 (유니티 초급자용):
//   EagleArcher(독수리 궁수)는 지금까지 "날아가는 화살"이 없었다(트레이서 프리셋 빈칸 → 숫자만 바로 뜸).
//   사용자 결정(U-10)은 "유닛 몸(손)에 든 화살 메시를 복사해 투사체로 쓴다, 착탄 이펙트는 없다"이다.
//   이 스크립트는 메뉴 한 번으로 아래를 모두 만든다 — 사람이 Inspector에서 손으로 할 일이 없다.
//     ① 팀별 화살 트레이서 프리팹 2개
//        Assets/_Project/Prefabs/VFX/Units/vfx_eaglearcher_tracer_blue.prefab  (머티리얼 = Blue 유닛 화살과 같은 것)
//        Assets/_Project/Prefabs/VFX/Units/vfx_eaglearcher_tracer_red.prefab   (머티리얼 = Red 유닛 화살과 같은 것)
//        → 메시·머티리얼·그림자 설정·월드 크기를 실제 유닛 프리팹(Unit_EagleArcher_Blue/Red)의 자식 EagleArrow에서 그대로 읽어 온다.
//     ② 그 프리팹을 가리키는 트레이서 EffectPreset 2개
//        Assets/_Project/Resources/Config/EffectPresets/EffectPreset_EagleArcher_Tracer_Blue.asset / _Red.asset
//     ③ UnitEffectConfig.asset의 EagleArcher(24) 행에 연결
//        tracerPreset = Blue 프리셋(기본), redTeamTracerPreset = Red 프리셋(Red 팀 공격자 전용 — UnitEffectConfig.GetTracer(type, team)).
//        attackPreset · impactPreset · hitPreset · deathPreset 은 건드리지 않는다(착탄 이펙트 없음 — U-10).
//   다시 실행해도 된다(같은 경로의 프리팹·프리셋을 덮어쓴다 — 프리셋은 GUID가 유지되고, 프리팹도 같은 경로 덮어쓰기라 참조가 유지된다).
//
// 화살 방향(앞 = 화살촉)을 어떻게 정했나:
//   TracerProjectile은 "날아가는 방향 = 오브젝트 앞(+Z)"이 되도록 루트를 돌린다(DriveTo · BeginDriven).
//   그래서 화살촉이 루트의 +Z를 향하도록 자식("Arrow")을 돌려 둔다. 화살 길이 방향 = 메시 경계 상자의 가장 긴 축(자동).
//   길이 축의 어느 끝이 화살촉인지는 메시 정점을 읽어야 알 수 있는데, 이 FBX는 Read/Write가 꺼져 있어(isReadable 0)
//   에디터 스크립트가 정점을 읽지 않는다(UnitRecipientFitMeasureTool과 같은 원칙 — 임포트 설정은 바꾸지 않는다).
//   대신 2026-10-27 game-programmer가 EagleArrow.fbx를 직접 해석한 결과를 기본값으로 쓴다 [추정 — 사용자 육안 확인 필요]:
//     · FBX 좌표에서 길이 축 = X(−0.957 ~ +0.954). −X 끝은 얇고 넓적하며 끝으로 갈수록 좁아지는 칼날 모양(= 화살촉),
//       +X 끝은 두 방향(Y·Z)으로 모두 넓은 모양(= 깃).
//     · Unity FBX 가져오기는 오른손 좌표 → 왼손 좌표로 바꾸며 X 부호를 뒤집는다 → Unity 메시 로컬 좌표에서 화살촉 = +X 끝.
//   실제 게임에서 화살이 거꾸로(깃이 앞) 날아가면 메뉴 「… (Reversed Tip)」을 한 번 실행하면 된다(그것만 반대로 다시 만든다).
//
// 화살촉 위치: 화살촉 끝이 루트 원점에 오도록 자식 위치를 맞춘다. 서버 투사체는 착탄 순간 루트가 "대상 표면점"에 닿으므로,
//   화살촉이 표면에 닿고 몸통은 뒤에 남는다(화살이 대상 몸속으로 반쯤 들어가 보이지 않게).
//
// 이 스크립트가 하지 않는 것: 유닛 프리팹·FBX 임포트 설정·씬은 수정하지 않는다(읽기만 한다). 착탄 이펙트·발사 이펙트·SFX는 만들지 않는다.
//
// ⚠️ 1회성: 실행 완료 확인 뒤 WORKFLOW [5-2]·[4]의 비활성화·최종 삭제 절차를 따른다(재사용 의존 없음).
// ============================================================================

using System;
using System.Globalization;
using Hexiege.Application;   // GameLog
using Hexiege.Domain;
using Hexiege.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// EagleArcher 팀별 화살 트레이서 프리팹·프리셋을 만들고 UnitEffectConfig에 연결하는 1회성 메뉴 도구.
    /// </summary>
    public static class EagleArcherArrowTracerSetup
    {
        private const string LogClass = nameof(EagleArcherArrowTracerSetup);
        private const string DialogTitle = "EagleArcher Arrow Tracers";

        private const string ConfigPath = "Assets/_Project/Resources/Config/UnitEffectConfig.asset";
        private const string BlueUnitPrefabPath = "Assets/_Project/Prefabs/Units/Transcendence/Unit_EagleArcher_Blue.prefab";
        private const string RedUnitPrefabPath = "Assets/_Project/Prefabs/Units/Transcendence/Unit_EagleArcher_Red.prefab";
        /// <summary>유닛 프리팹 안에서 복사할 화살 자식 오브젝트 이름.</summary>
        private const string SourceArrowName = "EagleArrow";
        /// <summary>팀별 원본 화살 머티리얼(복사 결과 확인용 — 다른 머티리얼이 잡히면 중단한다).</summary>
        private const string BlueMaterialPath = "Assets/_Project/Materials/Units/EagleArcher/mat_eaglearrow_blue.mat";
        private const string RedMaterialPath = "Assets/_Project/Materials/Units/EagleArcher/mat_eaglearrow_red.mat";

        /// <summary>만들 트레이서 프리팹 경로. 검증 fixture는 이 상수를 참조하지 않고 같은 경로 문자열을 따로 적는다(1회성 스크립트 직접 의존 금지 — WORKFLOW [5-2]).</summary>
        private const string BlueTracerPrefabPath = "Assets/_Project/Prefabs/VFX/Units/vfx_eaglearcher_tracer_blue.prefab";
        /// <summary>만들 트레이서 프리팹 경로(Red).</summary>
        private const string RedTracerPrefabPath = "Assets/_Project/Prefabs/VFX/Units/vfx_eaglearcher_tracer_red.prefab";
        /// <summary>만들 트레이서 프리셋 경로(Blue — UnitEffectConfig type 24 tracerPreset).</summary>
        private const string BluePresetPath = "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_EagleArcher_Tracer_Blue.asset";
        /// <summary>만들 트레이서 프리셋 경로(Red — UnitEffectConfig type 24 redTeamTracerPreset).</summary>
        private const string RedPresetPath = "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_EagleArcher_Tracer_Red.asset";

        /// <summary>트레이서 프리팹 안 화살 메시 자식 이름.</summary>
        private const string ArrowChildName = "Arrow";

        /// <summary>
        /// 기본값: Unity 메시 로컬 좌표에서 화살촉이 길이 축의 "+" 끝에 있다(위 머리 주석의 FBX 해석 — [추정]).
        /// 「(Reversed Tip)」 메뉴는 이 값을 반대로 써서 다시 만든다.
        /// </summary>
        private const bool DefaultTipOnPositiveEnd = true;

        /// <summary>메뉴: 팀별 화살 트레이서를 만들고 연결한다(기본 방향).</summary>
        [MenuItem("Hexiege/Effects/Setup/Create EagleArcher Arrow Tracers")]
        public static void Create() => Run(DefaultTipOnPositiveEnd);

        /// <summary>메뉴: 화살이 거꾸로(깃이 앞) 날아갈 때만 — 화살촉 방향을 반대로 해서 다시 만든다.</summary>
        [MenuItem("Hexiege/Effects/Setup/Create EagleArcher Arrow Tracers (Reversed Tip)")]
        public static void CreateReversed() => Run(!DefaultTipOnPositiveEnd);

        // 원본 화살에서 읽어 온 값 묶음.
        private struct SourceArrow
        {
            public Mesh Mesh;
            public Material[] Materials;
            public ShadowCastingMode Shadows;
            public bool ReceiveShadows;
            public Vector3 WorldScale;
        }

        private static void Run(bool tipOnPositiveEnd)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(DialogTitle, "Play 모드에서는 실행할 수 없습니다. Play를 멈춘 뒤 다시 실행하세요.", "확인");
                return;
            }
            var config = AssetDatabase.LoadAssetAtPath<UnitEffectConfig>(ConfigPath);
            if (config == null) { Fail("UnitEffectConfig.asset 을 찾지 못했습니다:\n" + ConfigPath); return; }
            if (!EditorUtility.DisplayDialog(DialogTitle,
                    "EagleArcher 유닛 프리팹의 손 화살(EagleArrow)을 복사해 팀별 화살 트레이서를 만들고\n"
                    + "UnitEffectConfig 의 EagleArcher 행에 연결합니다.\n\n"
                    + "- 만드는 것: 트레이서 프리팹 2개 · 트레이서 프리셋 2개\n"
                    + "- 유닛 프리팹·FBX·씬은 수정하지 않습니다.\n"
                    + "- 화살촉 방향: " + (tipOnPositiveEnd == DefaultTipOnPositiveEnd ? "기본" : "반대(Reversed Tip)") + "\n\n진행할까요?",
                    "만들고 연결", "취소"))
                return;

            try
            {
                if (!TryReadSource(BlueUnitPrefabPath, BlueMaterialPath, out SourceArrow blue)) return;
                if (!TryReadSource(RedUnitPrefabPath, RedMaterialPath, out SourceArrow red)) return;

                GameObject bluePrefab = SaveTracerPrefab(blue, BlueTracerPrefabPath, "vfx_eaglearcher_tracer_blue", tipOnPositiveEnd);
                GameObject redPrefab = SaveTracerPrefab(red, RedTracerPrefabPath, "vfx_eaglearcher_tracer_red", tipOnPositiveEnd);
                if (bluePrefab == null || redPrefab == null) { Fail("트레이서 프리팹 저장에 실패했습니다. Console 로그를 확인하세요."); return; }

                EffectPreset bluePreset = SavePreset(BluePresetPath, bluePrefab);
                EffectPreset redPreset = SavePreset(RedPresetPath, redPrefab);
                if (!ConnectConfig(config, bluePreset, redPreset)) return;

                AssetDatabase.SaveAssets();
                GameLog.Dev.Info("Combat", LogClass, "EagleArcher 팀별 화살 트레이서 생성·연결 완료",
                    string.Format(CultureInfo.InvariantCulture,
                        "BluePrefab={0}, RedPrefab={1}, BluePreset={2}, RedPreset={3}, TipOnPositiveEnd={4}, Scale={5:0.###}",
                        BlueTracerPrefabPath, RedTracerPrefabPath, BluePresetPath, RedPresetPath, tipOnPositiveEnd, blue.WorldScale.x));
                EditorUtility.DisplayDialog(DialogTitle,
                    "완료했습니다.\n\n" + BlueTracerPrefabPath + "\n" + RedTracerPrefabPath + "\n" + BluePresetPath + "\n" + RedPresetPath
                    + "\n\nUnitEffectConfig EagleArcher 행: tracerPreset = Blue, redTeamTracerPreset = Red\n\n"
                    + "게임에서 화살이 거꾸로(깃이 앞) 날아가면\n「Hexiege/Effects/Setup/Create EagleArcher Arrow Tracers (Reversed Tip)」을 실행하세요.",
                    "확인");
            }
            catch (Exception exception)
            {
                Fail("예외로 중단했습니다: " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        /// <summary>유닛 프리팹에서 화살 자식의 메시·머티리얼·그림자·월드 크기를 읽는다(프리팹은 수정하지 않는다).</summary>
        private static bool TryReadSource(string unitPrefabPath, string expectedMaterialPath, out SourceArrow source)
        {
            source = default;
            GameObject contents = PrefabUtility.LoadPrefabContents(unitPrefabPath);
            if (contents == null) { Fail("유닛 프리팹을 열지 못했습니다:\n" + unitPrefabPath); return false; }
            try
            {
                Transform arrow = FindDeep(contents.transform, SourceArrowName);
                var filter = arrow != null ? arrow.GetComponent<MeshFilter>() : null;
                var renderer = arrow != null ? arrow.GetComponent<MeshRenderer>() : null;
                if (filter == null || filter.sharedMesh == null || renderer == null)
                { Fail("유닛 프리팹에서 " + SourceArrowName + " 메시를 찾지 못했습니다:\n" + unitPrefabPath); return false; }
                var expected = AssetDatabase.LoadAssetAtPath<Material>(expectedMaterialPath);
                Material[] materials = renderer.sharedMaterials;
                if (expected == null || materials.Length == 0 || materials[0] != expected)
                {
                    Fail("팀 화살 머티리얼이 예상과 다릅니다(팀 색이 섞이지 않게 중단합니다).\n프리팹: " + unitPrefabPath
                        + "\n예상: " + expectedMaterialPath
                        + "\n실제: " + (materials.Length > 0 && materials[0] != null ? AssetDatabase.GetAssetPath(materials[0]) : "(없음)"));
                    return false;
                }
                // LoadPrefabContents의 루트는 저장된 자기 Transform 그대로다(유닛 루트는 배율 1). 손 화살의 월드 크기(lossyScale)를
                //   그대로 쓰면 날아가는 화살이 손에 든 화살과 같은 크기로 보인다(게임 중 유닛 루트도 배율 1 — UnitFactory는 배율을 바꾸지 않는다).
                source = new SourceArrow
                {
                    Mesh = filter.sharedMesh,
                    Materials = materials,
                    Shadows = renderer.shadowCastingMode,
                    ReceiveShadows = renderer.receiveShadows,
                    WorldScale = arrow.lossyScale
                };
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// 화살 트레이서 프리팹 1개를 만든다: 루트(TracerProjectile, 회전 없음) + 자식 Arrow(메시).
        /// 자식은 화살촉이 루트 +Z를 향하고 화살촉 끝이 루트 원점에 오도록 회전·위치를 맞춘다.
        /// </summary>
        private static GameObject SaveTracerPrefab(SourceArrow source, string path, string rootName, bool tipOnPositiveEnd)
        {
            var root = new GameObject(rootName);
            try
            {
                // 트레이서 컴포넌트는 없으면 EffectManager가 자동으로 붙이지만, 다른 트레이서 프리팹과 같게 미리 붙여 둔다.
                //   서버 투사체(외부 구동)에서는 _speed를 쓰지 않는다(TracerProjectile 「외부 구동」 주석).
                root.AddComponent<TracerProjectile>();
                var child = new GameObject(ArrowChildName);
                child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = source.Mesh;
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.Materials;
                renderer.shadowCastingMode = source.Shadows;
                renderer.receiveShadows = source.ReceiveShadows;

                // 길이 축 = 메시 경계 상자의 가장 긴 축(로컬). 화살촉 = 그 축의 +끝(기본) 또는 −끝(Reversed Tip).
                Bounds bounds = source.Mesh.bounds;
                int axis = LongestAxis(bounds.size);
                Vector3 axisVector = Vector3.zero;
                axisVector[axis] = tipOnPositiveEnd ? 1f : -1f;
                Vector3 tipLocal = bounds.center + axisVector * bounds.extents[axis];

                Quaternion rotation = Quaternion.FromToRotation(axisVector, Vector3.forward);
                child.transform.localRotation = rotation;
                child.transform.localScale = source.WorldScale;
                // 화살촉 끝(메시 로컬 → 루트 기준)이 원점에 오도록 반대로 민다.
                child.transform.localPosition = -(rotation * Vector3.Scale(source.WorldScale, tipLocal));

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success || saved == null)
                {
                    GameLog.Dev.Warn("Combat", LogClass, "화살 트레이서 프리팹 저장 실패", "Path=" + path);
                    return null;
                }
                GameLog.Dev.Info("Combat", LogClass, "화살 트레이서 프리팹 저장",
                    string.Format(CultureInfo.InvariantCulture,
                        "Path={0}, LongestAxis={1}, TipOnPositiveEnd={2}, Length={3:0.###}m, Scale=({4:0.###},{5:0.###},{6:0.###})",
                        path, "XYZ"[axis], tipOnPositiveEnd, bounds.size[axis] * Mathf.Abs(source.WorldScale[axis]),
                        source.WorldScale.x, source.WorldScale.y, source.WorldScale.z));
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>트레이서 EffectPreset을 만들거나(없으면) 갱신해 프리팹을 연결한다. 위치·회전·배율 보정은 기본값(0·0·1).</summary>
        private static EffectPreset SavePreset(string path, GameObject prefab)
        {
            var preset = AssetDatabase.LoadAssetAtPath<EffectPreset>(path);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<EffectPreset>();
                AssetDatabase.CreateAsset(preset, path);
            }
            var serialized = new SerializedObject(preset);
            serialized.FindProperty("_vfxPrefab").objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(preset);
            return preset;
        }

        /// <summary>UnitEffectConfig의 EagleArcher 행(1줄)에 두 프리셋을 연결한다. 행이 없거나 2줄 이상이면 중단(새 줄을 만들지 않는다).</summary>
        private static bool ConnectConfig(UnitEffectConfig config, EffectPreset blue, EffectPreset red)
        {
            var serialized = new SerializedObject(config);
            SerializedProperty entries = serialized.FindProperty("_entries");
            SerializedProperty row = null;
            int rows = 0;
            for (int i = 0; entries != null && i < entries.arraySize; i++)
            {
                SerializedProperty candidate = entries.GetArrayElementAtIndex(i);
                // UnitType은 명시 숫자값을 쓰므로 enumValueIndex가 아니라 intValue로 비교한다(기존 검증과 같은 방식).
                if (candidate.FindPropertyRelative("unitType").intValue != (int)UnitType.EagleArcher) continue;
                rows++;
                row = candidate;
            }
            if (rows != 1 || row == null)
            { Fail("UnitEffectConfig 에 EagleArcher 행이 정확히 1줄이어야 합니다(지금 " + rows + "줄). 연결하지 않았습니다."); return false; }
            SerializedProperty tracer = row.FindPropertyRelative("tracerPreset");
            SerializedProperty redTracer = row.FindPropertyRelative("redTeamTracerPreset");
            if (tracer == null || redTracer == null)
            { Fail("UnitEffectConfig 행에 tracerPreset / redTeamTracerPreset 칸이 없습니다(스크립트 컴파일 상태를 확인하세요)."); return false; }
            tracer.objectReferenceValue = blue;
            redTracer.objectReferenceValue = red;
            serialized.ApplyModifiedProperties(); // Undo 기록 포함
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
            config.Initialize(); // 같은 에디터 세션의 조회 캐시도 새 연결로 맞춘다
            return true;
        }

        private static int LongestAxis(Vector3 size)
            => size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;

        // 비활성 자식까지 이름으로 찾는다(손 화살 EagleArrow는 프리팹에서 비활성 상태로 저장돼 있다).
        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static void Fail(string message)
        {
            GameLog.Dev.Warn("Combat", LogClass, "EagleArcher 화살 트레이서 생성 중단", message.Replace('\n', ' '));
            EditorUtility.DisplayDialog(DialogTitle, message, "확인");
        }
    }
}
