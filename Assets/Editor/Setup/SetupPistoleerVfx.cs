// ============================================================================
// SetupPistoleerVfx.cs (Editor 전용, 1회성 도구)
// Pistoleer(권총병) 유닛의 공격/사망 EffectPreset 에셋을 자동 생성하고,
// UnitEffectConfig의 Pistoleer 항목에 연결하는 메뉴.
//
// 배경:
//   EffectManager 시스템의 첫 실기 테스트 대상으로 Pistoleer를 사용한다.
//   필요한 VFX 프리팹 / SFX 클립은 이미 프로젝트에 준비되어 있으므로,
//   이 스크립트는 그것들을 묶는 EffectPreset 에셋을 만들고 Config에 연결만 한다.
//
// 이 스크립트가 하는 일:
//   1) EffectPreset 에셋 2개 생성
//        - EffectPreset_Pistoleer_Attack
//            vfxPrefab = vfx_pistoleer_attack.prefab
//            sfxClip   = sfx_pistoleer_attack.wav
//        - EffectPreset_Pistoleer_Death
//            vfxPrefab = vfx_unit_death.prefab   (사망 VFX는 공용)
//            sfxClip   = sfx_unit_death.wav       (사망 SFX는 공용)
//   2) UnitEffectConfig.asset의 Pistoleer 항목 attackPreset/deathPreset에 위 2개 연결
//
// 사전 조건:
//   먼저 "Hexiege/Setup/UnitEffectConfig 생성" 메뉴로 UnitEffectConfig.asset을 만들어야 한다.
//   (이 스크립트는 Pistoleer 항목을 "찾아서 채우는" 역할이며, Config 자체는 만들지 않는다.)
//
// 사용법:
//   Unity 상단 메뉴 → Hexiege/Setup/Pistoleer VFX 프리셋 생성 및 연결
//
// 구현 메모:
//   - EffectPreset의 _vfxPrefab/_sfxClip/_sfxVolume는 private이므로
//     SerializedObject로 안전하게 채운다(Inspector 직렬화와 일관).
//   - 이미 같은 경로에 EffectPreset 에셋이 있으면 새로 만들지 않고 기존 것을 재사용한다.
//     (메뉴를 여러 번 눌러도 안전 — 멱등성 보장.)
//
// 이 스크립트는 1회성 도구이므로 작업 완료 후 삭제해도 무방하다.
// ============================================================================

using UnityEditor;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Presentation;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// Pistoleer용 EffectPreset 2종을 생성하고 UnitEffectConfig에 연결하는 Editor 메뉴.
    /// </summary>
    public static class SetupPistoleerVfx
    {
        // EffectPreset 에셋이 저장될 폴더.
        private const string PresetFolder = "Assets/_Project/Resources/Config/EffectPresets";

        // UnitEffectConfig 에셋 경로 (SetupEffectConfigs가 생성하는 위치와 동일).
        private const string UnitConfigPath = "Assets/_Project/Resources/Config/UnitEffectConfig.asset";

        // 원본 에셋 경로 (Research/Plan 문서 기준 — 이미 프로젝트에 존재).
        private const string VfxAttackPath = "Assets/_Project/Prefabs/VFX/Units/vfx_pistoleer_attack.prefab";
        private const string VfxDeathPath = "Assets/_Project/Prefabs/VFX/Units/vfx_unit_death.prefab";
        private const string SfxAttackPath = "Assets/_Project/Audio/SFX/Units/sfx_pistoleer_attack.wav";
        private const string SfxDeathPath = "Assets/_Project/Audio/SFX/Units/sfx_unit_death.wav";

        // 생성할 EffectPreset 에셋 경로.
        private const string AttackPresetPath = PresetFolder + "/EffectPreset_Pistoleer_Attack.asset";
        private const string DeathPresetPath = PresetFolder + "/EffectPreset_Pistoleer_Death.asset";

        [MenuItem("Hexiege/Setup/Pistoleer VFX 프리셋 생성 및 연결")]
        public static void SetupPistoleer()
        {
            // ----------------------------------------------------------------
            // [1] 원본 VFX/SFX 에셋 로드 — 하나라도 없으면 중단.
            // ----------------------------------------------------------------
            GameObject vfxAttack = LoadRequired<GameObject>(VfxAttackPath);
            GameObject vfxDeath = LoadRequired<GameObject>(VfxDeathPath);
            AudioClip sfxAttack = LoadRequired<AudioClip>(SfxAttackPath);
            AudioClip sfxDeath = LoadRequired<AudioClip>(SfxDeathPath);

            // LoadRequired가 null을 반환했다면 이미 에러 로그를 남겼으므로 조용히 중단.
            if (vfxAttack == null || vfxDeath == null || sfxAttack == null || sfxDeath == null)
                return;

            // ----------------------------------------------------------------
            // [2] EffectPreset 에셋 2종 생성(또는 기존 것 재사용).
            // ----------------------------------------------------------------
            EnsurePresetFolder();

            EffectPreset attackPreset = GetOrCreatePreset(AttackPresetPath, vfxAttack, sfxAttack, 1f);
            EffectPreset deathPreset = GetOrCreatePreset(DeathPresetPath, vfxDeath, sfxDeath, 1f);

            AssetDatabase.SaveAssets();

            // ----------------------------------------------------------------
            // [3] UnitEffectConfig의 Pistoleer 항목에 두 프리셋 연결.
            // ----------------------------------------------------------------
            UnitEffectConfig config = AssetDatabase.LoadAssetAtPath<UnitEffectConfig>(UnitConfigPath);
            if (config == null)
            {
                Debug.LogError(
                    "[SetupPistoleerVfx] UnitEffectConfig.asset을 찾지 못했습니다.\n" +
                    "먼저 'Hexiege/Setup/UnitEffectConfig 생성' 메뉴로 Config를 만든 뒤 다시 실행하세요.\n" +
                    $"기대 경로: {UnitConfigPath}");
                return;
            }

            bool linked = LinkPistoleerEntry(config, attackPreset, deathPreset);
            if (!linked)
            {
                Debug.LogError(
                    "[SetupPistoleerVfx] UnitEffectConfig에 Pistoleer 항목이 없습니다.\n" +
                    "'Hexiege/Setup/UnitEffectConfig 생성'으로 만든 기본 Config에는 Pistoleer가 포함되어야 합니다.\n" +
                    "Config를 다시 생성하거나 Inspector에서 Pistoleer 항목을 추가하세요.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log(
                "[SetupPistoleerVfx] 완료 — Pistoleer 공격/사망 프리셋 생성 및 연결.\n" +
                $"  공격 프리셋: {AttackPresetPath}\n" +
                $"  사망 프리셋: {DeathPresetPath}\n" +
                $"  연결 대상  : {UnitConfigPath} (Pistoleer 항목)");
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

        /// <summary>
        /// UnitEffectConfig의 _entries 리스트에서 Pistoleer 항목을 찾아
        /// attackPreset/deathPreset 필드를 채운다. SerializedObject로 안전하게 수정.
        /// </summary>
        /// <param name="config">대상 UnitEffectConfig.</param>
        /// <param name="attackPreset">공격 프리셋.</param>
        /// <param name="deathPreset">사망 프리셋.</param>
        /// <returns>Pistoleer 항목을 찾아 연결했으면 true, 항목이 없으면 false.</returns>
        private static bool LinkPistoleerEntry(
            UnitEffectConfig config, EffectPreset attackPreset, EffectPreset deathPreset)
        {
            SerializedObject so = new SerializedObject(config);
            SerializedProperty entries = so.FindProperty("_entries");

            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty elem = entries.GetArrayElementAtIndex(i);
                SerializedProperty unitTypeProp = elem.FindPropertyRelative("unitType");

                // enum의 "선택 인덱스(enumValueIndex)"가 아닌 "정수 값(intValue)"으로 비교해야
                // UnitType 값이 비연속(0,1,...,7,10,...)이어도 정확히 매칭된다.
                if (unitTypeProp.intValue != (int)UnitType.Pistoleer)
                    continue;

                elem.FindPropertyRelative("attackPreset").objectReferenceValue = attackPreset;
                elem.FindPropertyRelative("deathPreset").objectReferenceValue = deathPreset;
                so.ApplyModifiedPropertiesWithoutUndo();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 지정 경로의 EffectPreset을 로드하고, 없으면 새로 생성한다(멱등성).
        /// 어느 경우든 VFX/SFX/볼륨 값을 최신 인자로 다시 채워 넣는다.
        /// </summary>
        private static EffectPreset GetOrCreatePreset(
            string path, GameObject vfxPrefab, AudioClip sfxClip, float volume)
        {
            EffectPreset preset = AssetDatabase.LoadAssetAtPath<EffectPreset>(path);
            bool isNew = preset == null;

            if (isNew)
            {
                preset = ScriptableObject.CreateInstance<EffectPreset>();
                AssetDatabase.CreateAsset(preset, path);
            }

            // private 필드(_vfxPrefab/_sfxClip/_sfxVolume)는 SerializedObject로 채운다.
            SerializedObject so = new SerializedObject(preset);
            so.FindProperty("_vfxPrefab").objectReferenceValue = vfxPrefab;
            so.FindProperty("_sfxClip").objectReferenceValue = sfxClip;
            so.FindProperty("_sfxVolume").floatValue = volume;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[SetupPistoleerVfx] EffectPreset {(isNew ? "생성" : "갱신")}: {path}");
            return preset;
        }

        /// <summary>
        /// EffectPreset 저장 폴더가 없으면 만든다(AssetDatabase 기준).
        /// </summary>
        private static void EnsurePresetFolder()
        {
            if (AssetDatabase.IsValidFolder(PresetFolder))
                return;

            // 상위 폴더(Resources/Config)는 SetupEffectConfigs 사용 시점에 이미 존재하지만,
            // 단독 실행에도 안전하도록 단계별로 생성한다.
            const string parent = "Assets/_Project/Resources/Config";
            if (!AssetDatabase.IsValidFolder(parent))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources"))
                    AssetDatabase.CreateFolder("Assets/_Project", "Resources");
                AssetDatabase.CreateFolder("Assets/_Project/Resources", "Config");
            }
            AssetDatabase.CreateFolder(parent, "EffectPresets");
        }

        /// <summary>
        /// 지정 경로의 에셋을 로드한다. 없으면 에러 로그를 남기고 null 반환.
        /// </summary>
        private static T LoadRequired<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                Debug.LogError($"[SetupPistoleerVfx] 필수 에셋을 찾지 못했습니다: {path}");
            return asset;
        }
    }
}
