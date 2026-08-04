// ============================================================================
// SkillSetup_CreateDataAssets.cs  (에디터 전용 · 1회성 셋업)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  상단 메뉴  Hexiege > Skill > 1. Create Skill Data Assets  실행.            │
// │  (씬이 열려 있지 않아도 됨 — 순수 에셋 생성 작업이다.)                        │
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 하는가(유니티 초급자 기준):
//   스킬 건물 시스템 Phase 1을 실기로 테스트할 수 있도록, "플레이스홀더(임시)" 스킬 데이터 에셋을
//   자동 생성한다. 프로그래머가 손으로 하나씩 만들 필요 없이 이 메뉴 한 번이면 된다.
//     1) SkillDefinition(.asset) 6개 — 종족 3종 × (타입 A 폭격 + 타입 B 장판 DoT).
//     2) SkillLoadoutConfig(.asset) 1개 — 종족(RaceId) 키로 위 스킬들을 슬롯 1~2에 매핑.
//   생성 위치: Assets/_Project/Resources/Config/Skills/ (기존 Config 관례를 따른다).
//
// ⚠️ 수치는 전부 "플레이스홀더"다(최종값 아님):
//   전투 스탯이 ×10 스케일이므로 피해도 ×10축으로 넣는다(예: 폭격 150, 장판 초당 40).
//   반경 2.0(월드) / 쿨다운 10~12초 등은 테스트용 임의값이며, 최종 밸런스는 후속 데이터 작업에서 확정한다.
//
// 멱등성(여러 번 실행해도 안전):
//   에셋이 이미 있으면 재사용하고, 값만 다시 세팅한다. 로드아웃 항목도 종족별 1개로 갱신한다.
//
// 이 스크립트가 하지 않는 것(사용자 후속):
//   · 실제 아이콘 스프라이트 지정(플레이스홀더는 아이콘 null → 버튼은 색만 표시).
//   · GameBootstrapper 연결/씬 배선 → 메뉴 "2. Setup Scene ..."이 담당.
//   · 최종 스킬 목록·수치·타입 C(전역 상태변경) 스킬.
// ============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Hexiege.Domain;         // RaceId, SkillAimType, SkillMechanicType
using Hexiege.Infrastructure; // SkillDefinition, SkillLoadoutConfig

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 플레이스홀더 SkillDefinition 6개 + SkillLoadoutConfig 1개를 멱등하게 생성하는 1회성 에디터 스크립트.
    /// </summary>
    public static class SkillSetup_CreateDataAssets
    {
        // 스킬 에셋을 모아 둘 폴더(Resources 하위 — 런타임 로드도 가능하나 주 목적은 Inspector 참조).
        private const string SkillsFolder = "Assets/_Project/Resources/Config/Skills";
        private const string LoadoutAssetPath = SkillsFolder + "/SkillLoadoutConfig.asset";

        /// <summary>
        /// 한 스킬의 플레이스홀더 값 묶음(에셋 파일명 + 필드값).
        /// </summary>
        private struct SkillSpec
        {
            public string fileName;
            public SkillAimType aim;
            public SkillMechanicType mechanic;
            public float cooldown;
            public float radius;
            public float duration;
            public int totalDamage;      // 타입 A(즉발) — ×10 스케일 플레이스홀더.
            public float damagePerSecond; // 타입 B(장판 DoT) — ×10 스케일 플레이스홀더.

            // ── 타입 C(전역 상태변경) 필드 — Phase 2 ──
            public int statusKind;       // StatusEffectKind 정수 코드(2=공격력 배율 등). 타입 A·B는 0.
            public float magnitude;      // 강도(배율·회복량 등). 타입 A·B는 0.
            public bool targetsAllies;   // true=아군(버프)/false=적(디버프). 타입 A·B는 false.
        }

        // 종족별 스킬 정의(각 종족: 타입 A 폭격 1 + 타입 B 장판 DoT 1). 전부 플레이스홀더 수치.
        private static readonly Dictionary<RaceId, SkillSpec[]> RaceSkills = new Dictionary<RaceId, SkillSpec[]>
        {
            [RaceId.Human] = new[]
            {
                new SkillSpec { fileName = "Skill_Human_Bomb", aim = SkillAimType.PointTarget, mechanic = SkillMechanicType.InstantAreaDamage, cooldown = 10f, radius = 2.0f, duration = 0f,  totalDamage = 150, damagePerSecond = 0f },
                new SkillSpec { fileName = "Skill_Human_Dot",  aim = SkillAimType.PointTarget, mechanic = SkillMechanicType.AreaDotDamage,     cooldown = 12f, radius = 2.0f, duration = 4f,  totalDamage = 0,   damagePerSecond = 40f },
                // 타입 C 플레이스홀더 — 전역 아군 공격력 버프(즉시 발동, 조준 없음). statusKind 2 = AttackPowerMul, magnitude 1.5 = 공격력 1.5배.
                new SkillSpec { fileName = "Skill_Human_Buff", aim = SkillAimType.Instant, mechanic = SkillMechanicType.GlobalStatusChange, cooldown = 15f, radius = 0f, duration = 8f, totalDamage = 0, damagePerSecond = 0f, statusKind = (int)StatusEffectKind.AttackPowerMul, magnitude = 1.5f, targetsAllies = true },
            },
            [RaceId.Spirit] = new[]
            {
                new SkillSpec { fileName = "Skill_Spirit_Bomb", aim = SkillAimType.PointTarget, mechanic = SkillMechanicType.InstantAreaDamage, cooldown = 10f, radius = 2.0f, duration = 0f,  totalDamage = 150, damagePerSecond = 0f },
                new SkillSpec { fileName = "Skill_Spirit_Dot",  aim = SkillAimType.PointTarget, mechanic = SkillMechanicType.AreaDotDamage,     cooldown = 12f, radius = 2.0f, duration = 4f,  totalDamage = 0,   damagePerSecond = 40f },
                // 타입 C 플레이스홀더 — 전역 아군 공격력 버프.
                new SkillSpec { fileName = "Skill_Spirit_Buff", aim = SkillAimType.Instant, mechanic = SkillMechanicType.GlobalStatusChange, cooldown = 15f, radius = 0f, duration = 8f, totalDamage = 0, damagePerSecond = 0f, statusKind = (int)StatusEffectKind.AttackPowerMul, magnitude = 1.5f, targetsAllies = true },
            },
            [RaceId.Transcendence] = new[]
            {
                new SkillSpec { fileName = "Skill_Trans_Bomb", aim = SkillAimType.PointTarget, mechanic = SkillMechanicType.InstantAreaDamage, cooldown = 10f, radius = 2.0f, duration = 0f,  totalDamage = 150, damagePerSecond = 0f },
                new SkillSpec { fileName = "Skill_Trans_Dot",  aim = SkillAimType.PointTarget, mechanic = SkillMechanicType.AreaDotDamage,     cooldown = 12f, radius = 2.0f, duration = 4f,  totalDamage = 0,   damagePerSecond = 40f },
                // 타입 C 플레이스홀더 — 전역 아군 공격력 버프.
                new SkillSpec { fileName = "Skill_Trans_Buff", aim = SkillAimType.Instant, mechanic = SkillMechanicType.GlobalStatusChange, cooldown = 15f, radius = 0f, duration = 8f, totalDamage = 0, damagePerSecond = 0f, statusKind = (int)StatusEffectKind.AttackPowerMul, magnitude = 1.5f, targetsAllies = true },
            },
        };

        [MenuItem("Hexiege/Skill/1. Create Skill Data Assets")]
        public static void Run()
        {
            EnsureFolderExists(SkillsFolder);

            // ── 1) 종족별 SkillDefinition 에셋 생성/갱신 ──────────────────────
            var loadout = new Dictionary<RaceId, List<SkillDefinition>>();
            int createdDefs = 0, reusedDefs = 0;

            foreach (var kv in RaceSkills)
            {
                RaceId race = kv.Key;
                var defsForRace = new List<SkillDefinition>();

                foreach (SkillSpec spec in kv.Value)
                {
                    string path = $"{SkillsFolder}/{spec.fileName}.asset";
                    SkillDefinition def = EnsureSkillDefinition(path, out bool created);
                    if (created) createdDefs++; else reusedDefs++;

                    ApplySkillSpec(def, spec);
                    defsForRace.Add(def);
                }

                loadout[race] = defsForRace;
            }

            // ── 2) SkillLoadoutConfig 에셋 생성/갱신 ─────────────────────────
            SkillLoadoutConfig config = EnsureLoadoutConfig(LoadoutAssetPath, out bool loadoutCreated);
            ApplyLoadout(config, loadout);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── 3) 리포트 ─────────────────────────────────────────────────
            Debug.Log(
                "[Skill Setup] 스킬 데이터 에셋 생성 완료(플레이스홀더 수치).\n" +
                $"  · SkillDefinition: 생성 {createdDefs}개 / 재사용 {reusedDefs}개  ({SkillsFolder}/)\n" +
                $"  · SkillLoadoutConfig: {(loadoutCreated ? "생성" : "재사용")}  ({LoadoutAssetPath})\n" +
                "  · 다음 단계: 메뉴 'Hexiege/Skill/2. Setup Scene ...' 로 GameBootstrapper·UI·네트워크를 배선하세요.\n" +
                "  · ⚠️ 수치·아이콘은 플레이스홀더입니다. 최종 밸런스/아트는 후속 작업에서 채웁니다.");

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        // ====================================================================
        // SkillDefinition
        // ====================================================================

        /// <summary> 지정 경로의 SkillDefinition을 확보(없으면 생성). </summary>
        private static SkillDefinition EnsureSkillDefinition(string path, out bool created)
        {
            created = false;
            var existing = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            if (existing != null) return existing;

            var instance = ScriptableObject.CreateInstance<SkillDefinition>();
            AssetDatabase.CreateAsset(instance, path);
            created = true;
            return AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
        }

        /// <summary>
        /// SkillDefinition의 private [SerializeField] 값들을 SerializedObject로 세팅한다.
        /// (SkillDefinition.cs의 필드명과 정확히 일치해야 한다.)
        /// </summary>
        private static void ApplySkillSpec(SkillDefinition def, SkillSpec spec)
        {
            var so = new SerializedObject(def);
            SetEnum(so, "_aimType", (int)spec.aim);
            SetEnum(so, "_mechanic", (int)spec.mechanic);
            SetFloat(so, "_cooldown", spec.cooldown);
            SetFloat(so, "_radius", spec.radius);
            SetFloat(so, "_duration", spec.duration);
            SetInt(so, "_totalDamage", spec.totalDamage);
            SetFloat(so, "_damagePerSecond", spec.damagePerSecond);
            // 타입 C(상태변경) 필드 — 타입 A·B는 0/false, 타입 C 버프는 위 스펙 값(공격력 배율 등).
            SetInt(so, "_statusKind", spec.statusKind);
            SetFloat(so, "_magnitude", spec.magnitude);
            SetBool(so, "_targetsAllies", spec.targetsAllies);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(def);
        }

        // ====================================================================
        // SkillLoadoutConfig
        // ====================================================================

        /// <summary> 지정 경로의 SkillLoadoutConfig을 확보(없으면 생성). </summary>
        private static SkillLoadoutConfig EnsureLoadoutConfig(string path, out bool created)
        {
            created = false;
            var existing = AssetDatabase.LoadAssetAtPath<SkillLoadoutConfig>(path);
            if (existing != null) return existing;

            var instance = ScriptableObject.CreateInstance<SkillLoadoutConfig>();
            AssetDatabase.CreateAsset(instance, path);
            created = true;
            return AssetDatabase.LoadAssetAtPath<SkillLoadoutConfig>(path);
        }

        /// <summary>
        /// SkillLoadoutConfig의 _entries(List&lt;RaceLoadoutEntry&gt;)를 종족별 스킬 배열로 채운다.
        /// RaceLoadoutEntry의 public 필드명(race, skills)과 정확히 일치해야 한다.
        /// </summary>
        private static void ApplyLoadout(SkillLoadoutConfig config, Dictionary<RaceId, List<SkillDefinition>> loadout)
        {
            var so = new SerializedObject(config);
            SerializedProperty entries = so.FindProperty("_entries");
            if (entries == null)
            {
                Debug.LogError("[Skill Setup] SkillLoadoutConfig에 '_entries' 필드가 없습니다. 필드명을 확인하세요.");
                return;
            }

            entries.arraySize = loadout.Count;

            int i = 0;
            foreach (var kv in loadout)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                SerializedProperty raceProp = entry.FindPropertyRelative("race");
                SerializedProperty skillsProp = entry.FindPropertyRelative("skills");

                if (raceProp != null) raceProp.enumValueIndex = (int)kv.Key;

                if (skillsProp != null)
                {
                    skillsProp.arraySize = kv.Value.Count;
                    for (int s = 0; s < kv.Value.Count; s++)
                        skillsProp.GetArrayElementAtIndex(s).objectReferenceValue = kv.Value[s];
                }
                i++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        // ====================================================================
        // 공통 헬퍼
        // ====================================================================

        private static void SetEnum(SerializedObject so, string field, int enumIndex)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[Skill Setup] 필드 '{field}' 없음."); return; }
            p.enumValueIndex = enumIndex;
        }

        private static void SetFloat(SerializedObject so, string field, float value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[Skill Setup] 필드 '{field}' 없음."); return; }
            p.floatValue = value;
        }

        private static void SetInt(SerializedObject so, string field, int value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[Skill Setup] 필드 '{field}' 없음."); return; }
            p.intValue = value;
        }

        private static void SetBool(SerializedObject so, string field, bool value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[Skill Setup] 필드 '{field}' 없음."); return; }
            p.boolValue = value;
        }

        /// <summary> "Assets/A/B/C" 폴더를 단계별로 확인·생성(AssetDatabase 추적). </summary>
        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string[] parts = folderPath.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
