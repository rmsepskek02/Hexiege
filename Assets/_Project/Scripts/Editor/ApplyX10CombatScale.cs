// ============================================================================
// ApplyX10CombatScale.cs  (에디터 전용 · 1회성 밸런스 스케일 스크립트)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  상단 메뉴  Hexiege > Balance > Apply x10 Scale  실행.                     │
// │  확인 대화상자에서 "적용"을 눌러야 실제로 반영된다.                          │
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 하는가 (전투 스탯 ×10 스케일 개편 — Plan 항목 0):
//   1) UnitStatsConfig.asset      : 각 유닛 엔트리의 maxHp · attackPower 를 ×10.
//   2) BuildingStatsConfig.asset  : 각 건물 엔트리의 종족별 HP(human/spirit/transcendenceMaxHp)와
//                                    종족별 공격력(human/spirit/transcendenceAttackPower)을 ×10.
//   3) SpecialAttackConfig.asset  : DoT/힐 수치를 ×10 확정값으로 "설정"(set)한다.
//        · _blastDotPerSecond   → 20   (MushroomBomber 착탄 DoT, 2→20/s)
//        · _infernoDotPerSecond → 50   (InfernoSpirit DoT, 5→50/s)
//        · _bloomHealAmount     → 200  (BloomFairy 힐, 20→200)
//        · _waveHeal            → 100  (TorrentSpirit 아군 힐, 10→100)
//
// 무엇을 건드리지 않는가 (불변 — 매우 중요):
//   사거리 · 감지거리 · 이동속도 · 공격 쿨다운 · 생산/연구 시간 · 모든 골드 비용 ·
//   방어력(신규, 0 유지) · 스플래시/부채꼴/파도 비율 · 반경 · 시간 필드는 전부 그대로 둔다.
//   (UnitStatEntry.defense / attackRange / detectRange / moveSpeed / attackCooldown /
//    productionTime / goldCost / populationCost / BuildingTypeEntry.*GoldCost /
//    *AttackCooldown / *AttackRange / upgradeCost / SpecialAttackConfig 의 반경·비율·시간 필드)
//
// 왜 코드가 아니라 .asset 을 고치는가:
//   Inspector(ScriptableObject) 값이 코드 폴백보다 우선한다(.claude/MEMORY.md 교훈).
//   코드 폴백 기본값만 바꾸면 이미 값이 저장된 .asset 에는 반영되지 않으므로,
//   실제 .asset 파일의 값을 SerializedObject 로 재설정해야 한다.
//   (SpecialAttackConfig 의 blast/inferno/bloom 필드는 .asset 에 아직 키가 없어 코드 폴백을 쓰지만,
//    이 스크립트가 명시값으로 set + 저장하면 .asset 에도 확정값이 기록된다.)
//
// ⚠️ 멱등 주의 (두 번 실행하면 ×100):
//   UnitStatsConfig / BuildingStatsConfig 는 "곱하기(×10)" 라서 두 번 실행하면 값이 ×100 이 된다.
//   그래서 ① 실행 전 확인 대화상자, ② 이미 실행한 기록(EditorPrefs) 시 추가 경고,
//   ③ unit 0(LittleKnight) HP 가 이미 커져 있으면 heuristic 경고를 띄운다.
//   (SpecialAttackConfig 는 "설정(set)" 이라 여러 번 실행해도 안전하다.)
//
// Editor 전용 — 런타임 빌드에 포함되지 않는다(Editor 폴더).
// ============================================================================

using UnityEditor;
using UnityEngine;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 전투 스탯 ×10 스케일을 config .asset 3종에 1회 적용하는 에디터 스크립트.
    /// </summary>
    public static class ApplyX10CombatScale
    {
        private const string UnitStatsPath = "Assets/_Project/Resources/Config/UnitStatsConfig.asset";
        private const string BuildingStatsPath = "Assets/_Project/Resources/Config/BuildingStatsConfig.asset";
        private const string SpecialAttackPath = "Assets/_Project/Resources/Config/SpecialAttackConfig.asset";

        // 이미 실행했는지 기록하는 EditorPrefs 키(프로젝트 경로별로 구분). 재실행 시 추가 경고에 사용.
        private const string PrefKey = "Hexiege.Balance.X10Applied";

        private const int ScaleFactor = 10;

        // SpecialAttackConfig 확정 목표값(×10 반영). 곱셈이 아니라 명시 설정이라 멱등하다.
        private const float BlastDotPerSecondTarget = 20f;
        private const float InfernoDotPerSecondTarget = 50f;
        private const float BloomHealAmountTarget = 200f;
        private const float WaveHealTarget = 100f;

        [MenuItem("Hexiege/Balance/Apply x10 Scale")]
        public static void Run()
        {
            // ── 확인 대화상자 (멱등 가드) ───────────────────────────────────
            bool alreadyMarked = EditorPrefs.GetBool(PrefKey, false);
            string warning =
                "전투 스탯 ×10 스케일을 config 에셋에 적용합니다.\n\n" +
                "· UnitStatsConfig: maxHp · attackPower ×10\n" +
                "· BuildingStatsConfig: 종족별 HP · 공격력 ×10\n" +
                "· SpecialAttackConfig: DoT/힐 값을 ×10 확정값으로 설정\n\n" +
                "⚠️ Unit/Building 은 '곱하기' 이므로 두 번 실행하면 ×100 이 됩니다.";

            if (alreadyMarked)
            {
                warning += "\n\n‼️ 이 프로젝트에서 이미 한 번 실행한 기록이 있습니다.\n" +
                           "정말 다시 실행하면 값이 ×100 이 됩니다. 신중히 결정하세요.";
            }

            if (IsUnitStatsLikelyAlreadyScaled(out int sampleHp))
            {
                warning += $"\n\n🔎 감지: 첫 유닛(LittleKnight)의 maxHp 가 이미 {sampleHp} 입니다.\n" +
                           "이미 ×10 이 적용된 상태일 수 있습니다(중복 실행 주의).";
            }

            bool proceed = EditorUtility.DisplayDialog(
                "전투 스탯 ×10 스케일 적용", warning, "적용", "취소");
            if (!proceed)
            {
                Debug.Log("[Balance] ×10 스케일 적용이 취소되었습니다.");
                return;
            }

            // ── 실제 적용 ──────────────────────────────────────────────────
            int unitCount = ScaleUnitStats();
            int buildingCount = ScaleBuildingStats();
            bool specialOk = SetSpecialAttackValues();

            // 변경 저장.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 실행 기록 남김(재실행 경고용).
            EditorPrefs.SetBool(PrefKey, true);

            Debug.Log(
                "[Balance] ×10 스케일 적용 완료.\n" +
                $"  · UnitStatsConfig: {unitCount}개 유닛 엔트리(maxHp·attackPower) ×10\n" +
                $"  · BuildingStatsConfig: {buildingCount}개 건물 엔트리(종족 HP·공격력) ×10\n" +
                $"  · SpecialAttackConfig: DoT/힐 확정값 설정 {(specialOk ? "완료" : "실패(에셋 없음)")}\n" +
                "  ※ 값이 올바른지 각 .asset 을 Inspector 에서 확인하세요.");
        }

        // ====================================================================
        // 1) UnitStatsConfig — maxHp · attackPower ×10
        // ====================================================================

        private static int ScaleUnitStats()
        {
            var config = AssetDatabase.LoadAssetAtPath<Object>(UnitStatsPath);
            if (config == null)
            {
                Debug.LogWarning($"[Balance] UnitStatsConfig 에셋을 찾지 못했습니다: {UnitStatsPath}");
                return 0;
            }

            var so = new SerializedObject(config);
            SerializedProperty list = so.FindProperty("_stats");
            if (list == null || !list.isArray)
            {
                Debug.LogError("[Balance] UnitStatsConfig 에 '_stats' 배열 프로퍼티가 없습니다.");
                return 0;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                MultiplyInt(entry, "maxHp");
                MultiplyInt(entry, "attackPower");
                // defense · attackRange · detectRange · moveSpeed · attackCooldown ·
                // productionTime · goldCost · populationCost 는 불변(손대지 않는다).
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return list.arraySize;
        }

        // ====================================================================
        // 2) BuildingStatsConfig — 종족별 HP · 공격력 ×10
        // ====================================================================

        private static int ScaleBuildingStats()
        {
            var config = AssetDatabase.LoadAssetAtPath<Object>(BuildingStatsPath);
            if (config == null)
            {
                Debug.LogWarning($"[Balance] BuildingStatsConfig 에셋을 찾지 못했습니다: {BuildingStatsPath}");
                return 0;
            }

            var so = new SerializedObject(config);
            SerializedProperty list = so.FindProperty("_stats");
            if (list == null || !list.isArray)
            {
                Debug.LogError("[Balance] BuildingStatsConfig 에 '_stats' 배열 프로퍼티가 없습니다.");
                return 0;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                // HP 3종족.
                MultiplyInt(entry, "humanMaxHp");
                MultiplyInt(entry, "spiritMaxHp");
                MultiplyInt(entry, "transcendenceMaxHp");
                // 공격력(타워) 3종족.
                MultiplyInt(entry, "humanAttackPower");
                MultiplyInt(entry, "spiritAttackPower");
                MultiplyInt(entry, "transcendenceAttackPower");
                // 골드비용 · 쿨다운 · 사거리 · upgradeCost 는 불변(손대지 않는다).
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return list.arraySize;
        }

        // ====================================================================
        // 3) SpecialAttackConfig — DoT/힐 값 명시 설정(멱등)
        // ====================================================================

        private static bool SetSpecialAttackValues()
        {
            var config = AssetDatabase.LoadAssetAtPath<Object>(SpecialAttackPath);
            if (config == null)
            {
                Debug.LogWarning($"[Balance] SpecialAttackConfig 에셋을 찾지 못했습니다: {SpecialAttackPath}");
                return false;
            }

            var so = new SerializedObject(config);
            // 곱셈이 아니라 확정값 "설정" — 여러 번 실행해도 안전(멱등).
            SetFloat(so, "_blastDotPerSecond", BlastDotPerSecondTarget);
            SetFloat(so, "_infernoDotPerSecond", InfernoDotPerSecondTarget);
            SetFloat(so, "_bloomHealAmount", BloomHealAmountTarget);
            SetFloat(so, "_waveHeal", WaveHealTarget);
            // 반경·비율·시간(_quakeRadius/_quakeSplashRatio/_sweepReach/_blastRadius/
            // _blastDotDuration/_infernoDotDuration/_bloomHealDuration/_waveWidth/_waveLength/
            // _waveTravelTime/_sweepArcHalfAngle)은 불변(손대지 않는다).

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return true;
        }

        // ====================================================================
        // 헬퍼
        // ====================================================================

        /// <summary> entry.relativeName(int)을 ×ScaleFactor 한다. 프로퍼티가 없으면 조용히 건너뛴다. </summary>
        private static void MultiplyInt(SerializedProperty entry, string relativeName)
        {
            SerializedProperty p = entry.FindPropertyRelative(relativeName);
            if (p == null)
            {
                Debug.LogWarning($"[Balance] 프로퍼티 '{relativeName}' 를 찾지 못했습니다(스킵).");
                return;
            }
            p.intValue *= ScaleFactor;
        }

        /// <summary> so 의 float 프로퍼티를 지정 값으로 설정한다. 프로퍼티가 없으면 조용히 건너뛴다. </summary>
        private static void SetFloat(SerializedObject so, string name, float value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null)
            {
                Debug.LogWarning($"[Balance] SpecialAttackConfig 프로퍼티 '{name}' 를 찾지 못했습니다(스킵).");
                return;
            }
            p.floatValue = value;
        }

        /// <summary>
        /// 첫 유닛(LittleKnight, unitType 0)의 maxHp 가 이미 커져 있는지로 "이미 ×10?" 를 추정한다.
        /// 기준: 원본 30 → ×10 은 300. 200 이상이면 이미 스케일됐을 가능성이 높다고 본다(정보성 경고용).
        /// </summary>
        private static bool IsUnitStatsLikelyAlreadyScaled(out int sampleHp)
        {
            sampleHp = 0;
            var config = AssetDatabase.LoadAssetAtPath<Object>(UnitStatsPath);
            if (config == null) return false;

            var so = new SerializedObject(config);
            SerializedProperty list = so.FindProperty("_stats");
            if (list == null || !list.isArray || list.arraySize == 0) return false;

            SerializedProperty first = list.GetArrayElementAtIndex(0);
            SerializedProperty hp = first.FindPropertyRelative("maxHp");
            if (hp == null) return false;

            sampleHp = hp.intValue;
            return sampleHp >= 200;
        }
    }
}
