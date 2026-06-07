// ============================================================================
// AIConfigSetup.cs (Editor 전용, 1회성 생성 도구)
// AIConfig / AIScenarioConfig ScriptableObject 에셋을 메뉴에서 기본값과 함께 생성한다.
//
// 사용법:
//   Unity 상단 메뉴 → Hexiege/Setup/
//     - AIConfig 생성
//     - AIScenarioConfig_Human_A 생성
//     - AIScenarioConfig_Human_B 생성
//     - AIScenarioConfig_Human_C 생성
//
// 동작:
//   Resources/Config/ 아래에 .asset을 생성하고 기본 수치를 채운다.
//   이미 같은 경로에 에셋이 존재하면 덮어쓰지 않고 선택만 한다(안전).
//
// 왜 Editor 스크립트로?
//   ScriptableObject는 코드만으로는 .asset 파일이 생기지 않는다.
//   AssetDatabase.CreateAsset으로 명시적으로 파일을 만들어야 하며,
//   이는 에디터 전용 API라서 Assets/Editor 폴더에 둔다(런타임 빌드에서 제외).
//
// 주의:
//   시나리오 수치(delaySeconds 등)는 밸런싱 전 placeholder다.
//   A/B/C 모두 동일한 기본 빌드오더(시나리오 A)로 채운다.
//   추후 game-design-lead가 Inspector에서 B/C 수치를 차별화한다.
// ============================================================================

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Hexiege.Domain;
using Hexiege.Infrastructure;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// AIConfig / AIScenarioConfig 에셋 생성 메뉴 모음.
    /// </summary>
    public static class AIConfigSetup
    {
        // Resources.Load로 런타임에 불러올 수 있도록 Resources/Config 아래에 둔다.
        private const string ConfigDir = "Assets/_Project/Resources/Config";

        // ====================================================================
        // AIConfig 생성
        // ====================================================================

        /// <summary>
        /// AIConfig.asset을 기본 난이도 수치와 함께 생성한다.
        /// 수치 출처: GameSystemRules_AI.md 규칙 5 표.
        /// </summary>
        [MenuItem("Hexiege/Setup/AIConfig 생성")]
        public static void CreateAIConfig()
        {
            string path = $"{ConfigDir}/AIConfig.asset";

            // 이미 있으면 덮어쓰지 않고 선택만 — 기존 밸런싱 값 보호.
            var existing = AssetDatabase.LoadAssetAtPath<AIConfig>(path);
            if (existing != null)
            {
                Debug.LogWarning($"[AIConfigSetup] 이미 존재합니다: {path} (덮어쓰지 않음)");
                Selection.activeObject = existing;
                return;
            }

            var config = ScriptableObject.CreateInstance<AIConfig>();

            // 쉬움: AI가 느리고 약하게 (수입 0.7, 생산 1.3배 느림, 결정 주기 20초)
            config.easy = new DifficultyParams
            {
                goldIncomeMultiplier = 0.7f,
                productionTimeMultiplier = 1.3f,
                decisionInterval = 20f,
                unitCountThreshold = 0.7f,
                goldExcessThreshold = 300f,
                buildOrderAccelCooldown = 20f,
                productionActivationCooldown = 15f,
                retryInterval = 10f,
                mineCheckInterval = 20f
            };

            // 보통: 기준값 (배율 1.0)
            config.normal = new DifficultyParams
            {
                goldIncomeMultiplier = 1.0f,
                productionTimeMultiplier = 1.0f,
                decisionInterval = 10f,
                unitCountThreshold = 1.0f,
                goldExcessThreshold = 200f,
                buildOrderAccelCooldown = 20f,
                productionActivationCooldown = 15f,
                retryInterval = 10f,
                mineCheckInterval = 20f
            };

            // 어려움: AI가 빠르고 강하게 (수입 1.3, 생산 0.7배 빠름, 결정 주기 5초)
            config.hard = new DifficultyParams
            {
                goldIncomeMultiplier = 1.3f,
                productionTimeMultiplier = 0.7f,
                decisionInterval = 5f,
                unitCountThreshold = 1.3f,
                goldExcessThreshold = 150f,
                buildOrderAccelCooldown = 20f,
                productionActivationCooldown = 15f,
                retryInterval = 10f,
                mineCheckInterval = 20f
            };

            CreateAssetAt(config, path);
        }

        // ====================================================================
        // AIScenarioConfig (Human A/B/C) 생성
        // ====================================================================

        [MenuItem("Hexiege/Setup/AIScenarioConfig_Human_A 생성")]
        public static void CreateScenarioHumanA()
        {
            CreateHumanScenario("Human_A");
        }

        [MenuItem("Hexiege/Setup/AIScenarioConfig_Human_B 생성")]
        public static void CreateScenarioHumanB()
        {
            CreateHumanScenario("Human_B");
        }

        [MenuItem("Hexiege/Setup/AIScenarioConfig_Human_C 생성")]
        public static void CreateScenarioHumanC()
        {
            CreateHumanScenario("Human_C");
        }

        /// <summary>
        /// 지정한 이름의 Human 시나리오 에셋을 시나리오 A 기본 빌드오더로 생성한다.
        /// (A/B/C 모두 동일 데이터 — 밸런싱 후 Inspector에서 차별화 예정)
        /// </summary>
        private static void CreateHumanScenario(string suffix)
        {
            string path = $"{ConfigDir}/AIScenarioConfig_{suffix}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<AIScenarioConfig>(path);
            if (existing != null)
            {
                Debug.LogWarning($"[AIConfigSetup] 이미 존재합니다: {path} (덮어쓰지 않음)");
                Selection.activeObject = existing;
                return;
            }

            var scenario = ScriptableObject.CreateInstance<AIScenarioConfig>();
            scenario.scenarioName = suffix;

            // 기본 빌드오더(시나리오 A) — SerializeField private 리스트에 직접 주입하기 위해
            // SerializedObject로 _steps를 채운다.
            var steps = BuildHumanScenarioASteps();
            ApplySteps(scenario, steps);

            CreateAssetAt(scenario, path);
        }

        /// <summary>
        /// Human 시나리오 A(물량형)의 빌드오더 항목을 코드로 구성한다.
        /// 라인 인덱스: 0=근거리A, 1=총기류, 2=탈것류.
        /// 수치(delay)는 GameSystemRules_AI.md / Plan.md의 placeholder 표를 따른다.
        /// </summary>
        private static List<BuildOrderStep> BuildHumanScenarioASteps()
        {
            var list = new List<BuildOrderStep>();

            // ── Phase 1 (초반) ──────────────────────────────────────────────
            list.Add(MakePlace(BuildingType.TrainingCamp, line: 0, phase: 0, delay: 5f));
            list.Add(MakeProduce(UnitType.LittleKnight, line: 0, phase: 0, delay: 20f));

            // ── Phase 2 (중반) ──────────────────────────────────────────────
            list.Add(MakePlace(BuildingType.Gunsmith, line: 1, phase: 1, delay: 20f));
            list.Add(MakeProduce(UnitType.Pistoleer, line: 1, phase: 1, delay: 35f));

            // ── Phase 3 (중후반) ────────────────────────────────────────────
            list.Add(MakeUpgrade(BuildingType.WarAcademy, line: 0, phase: 2, delay: 5f));
            list.Add(MakeProduce(UnitType.SpearMan, line: 0, phase: 2, delay: 30f));
            list.Add(MakeUpgrade(BuildingType.Armory, line: 1, phase: 2, delay: 60f));
            list.Add(MakeProduce(UnitType.Assault, line: 1, phase: 2, delay: 90f));

            // ── Phase 4 (후반) ──────────────────────────────────────────────
            list.Add(MakeUpgrade(BuildingType.HumanBarracks, line: 0, phase: 3, delay: 15f));
            list.Add(MakeProduce(UnitType.BattleAxe, line: 0, phase: 3, delay: 45f));
            list.Add(MakeUpgrade(BuildingType.WeaponForge, line: 1, phase: 3, delay: 50f));
            list.Add(MakeProduce(UnitType.Sniper, line: 1, phase: 3, delay: 80f));

            return list;
        }

        // ── BuildOrderStep 생성 헬퍼 ──────────────────────────────────────────
        // delay는 쉬움/보통/어려움 동일값(placeholder)으로 채운다. 밸런싱 시 분리.

        private static BuildOrderStep MakePlace(BuildingType type, int line, int phase, float delay)
        {
            return new BuildOrderStep
            {
                actionType = AIActionType.PlaceBuilding,
                buildingType = type,
                unitType = default,
                targetBuildingLine = line,
                phaseIndex = phase,
                delaySecondsEasy = delay,
                delaySecondsNormal = delay,
                delaySecondsHard = delay
            };
        }

        private static BuildOrderStep MakeUpgrade(BuildingType type, int line, int phase, float delay)
        {
            return new BuildOrderStep
            {
                actionType = AIActionType.UpgradeBuilding,
                buildingType = type,
                unitType = default,
                targetBuildingLine = line,
                phaseIndex = phase,
                delaySecondsEasy = delay,
                delaySecondsNormal = delay,
                delaySecondsHard = delay
            };
        }

        private static BuildOrderStep MakeProduce(UnitType unit, int line, int phase, float delay)
        {
            return new BuildOrderStep
            {
                actionType = AIActionType.StartProduction,
                buildingType = default,
                unitType = unit,
                targetBuildingLine = line,
                phaseIndex = phase,
                delaySecondsEasy = delay,
                delaySecondsNormal = delay,
                delaySecondsHard = delay
            };
        }

        /// <summary>
        /// AIScenarioConfig의 private SerializeField 리스트(_steps)에 항목을 채운다.
        /// public 접근자가 읽기 전용(IReadOnlyList)이므로 SerializedObject로 직접 기록한다.
        /// </summary>
        private static void ApplySteps(AIScenarioConfig scenario, List<BuildOrderStep> steps)
        {
            var so = new SerializedObject(scenario);
            SerializedProperty list = so.FindProperty("_steps");
            list.arraySize = steps.Count;

            for (int i = 0; i < steps.Count; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                BuildOrderStep s = steps[i];

                element.FindPropertyRelative("actionType").enumValueIndex = (int)s.actionType;
                element.FindPropertyRelative("buildingType").enumValueIndex = (int)s.buildingType;
                element.FindPropertyRelative("unitType").enumValueIndex = (int)s.unitType;
                element.FindPropertyRelative("targetBuildingLine").intValue = s.targetBuildingLine;
                element.FindPropertyRelative("phaseIndex").intValue = s.phaseIndex;
                element.FindPropertyRelative("delaySecondsEasy").floatValue = s.delaySecondsEasy;
                element.FindPropertyRelative("delaySecondsNormal").floatValue = s.delaySecondsNormal;
                element.FindPropertyRelative("delaySecondsHard").floatValue = s.delaySecondsHard;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ====================================================================
        // 공통 — 에셋 파일 생성
        // ====================================================================

        /// <summary>
        /// ScriptableObject 인스턴스를 지정 경로에 .asset으로 저장하고 선택 표시한다.
        /// 대상 폴더가 없으면 생성한다.
        /// </summary>
        private static void CreateAssetAt(Object asset, string path)
        {
            // 폴더 보장: Assets/_Project/Resources/Config 가 없으면 단계별 생성.
            EnsureFolder(ConfigDir);

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"[AIConfigSetup] 생성 완료: {path}");
        }

        /// <summary>
        /// "Assets/A/B/C" 형태의 폴더 경로가 존재하도록 단계별로 생성한다.
        /// AssetDatabase.CreateFolder는 한 단계씩만 만들 수 있으므로 분할 처리.
        /// </summary>
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string[] parts = folder.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
