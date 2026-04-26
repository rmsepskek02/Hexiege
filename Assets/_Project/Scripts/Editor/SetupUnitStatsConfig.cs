// ============================================================================
// SetupUnitStatsConfig.cs
// UnitStatsConfig(ScriptableObject) 에셋을 자동 생성하는 1회성 에디터 스크립트.
//
// 메뉴: Hexiege/Setup/UnitStatsConfig 생성
//
// 동작:
//   1) Assets/_Project/Resources/Config/ 폴더가 없으면 자동 생성.
//   2) Assets/_Project/Resources/Config/UnitStatsConfig.asset 존재 여부 확인.
//      - 있으면 덮어쓰지 않고 경고만 표시 (사용자 수정 보호).
//      - 없으면 신규 .asset 생성 후 현재 코드 기준 스탯값을 자동 입력.
//   3) 생성된 에셋을 Ping(Project 창 하이라이트)하여 찾기 쉽게 안내.
//
// 여기 입력되는 모든 수치는 기존 UnitStats.cs / UnitProductionStats.cs에서
// switch 표현식으로 하드코딩돼 있던 값과 1:1 동일.
//
// Editor 전용 — 빌드에 포함되지 않음.
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Infrastructure.EditorTools
{
    public static class SetupUnitStatsConfig
    {
        // 생성 경로 — GameBootstrapper가 Inspector SerializedField로 로드하므로
        // Resources.Load 경로가 아니어도 상관없다.
        private const string FolderPath = "Assets/_Project/Resources/Config";
        private const string AssetPath = FolderPath + "/UnitStatsConfig.asset";

        [MenuItem("Hexiege/Setup/UnitStatsConfig 생성")]
        public static void CreateAsset()
        {
            // 1) 폴더 존재 보장 — 없으면 단계별로 생성.
            EnsureFolderExists(FolderPath);

            // 2) 기존 에셋이 있으면 덮어쓰지 않고 경고만.
            var existing = AssetDatabase.LoadAssetAtPath<UnitStatsConfig>(AssetPath);
            if (existing != null)
            {
                Debug.LogWarning($"[SetupUnitStatsConfig] 이미 에셋이 존재합니다: {AssetPath}\n" +
                                 "새로 만들려면 먼저 기존 파일을 삭제해 주세요.");
                EditorGUIUtility.PingObject(existing);
                Selection.activeObject = existing;
                return;
            }

            // 3) ScriptableObject 인스턴스 생성 → 기본 스탯값 주입 → .asset 저장.
            var config = ScriptableObject.CreateInstance<UnitStatsConfig>();
            PopulateDefaults(config);

            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SetupUnitStatsConfig] 에셋 생성 완료: {AssetPath}\n" +
                      "Inspector에서 GameBootstrapper의 Unit Stats Config 필드에 이 에셋을 연결해 주세요.");

            EditorGUIUtility.PingObject(config);
            Selection.activeObject = config;
        }

        /// <summary>
        /// "Assets/A/B/C"처럼 중첩된 폴더를 한 단계씩 확인하며 없는 폴더를 생성.
        /// AssetDatabase.CreateFolder는 한 단계씩만 생성 가능하므로 루프가 필요하다.
        /// </summary>
        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string[] parts = folderPath.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        /// <summary>
        /// 현재 코드에서 사용 중인 스탯값을 config._stats에 채운다.
        /// UnitStatsConfig._stats는 private 필드이므로 SerializedObject로 접근.
        /// </summary>
        private static void PopulateDefaults(UnitStatsConfig config)
        {
            // 기본 스탯 테이블 — UnitStats.cs / UnitProductionStats.cs의 기존 switch 값과 동일.
            // 다중 히트 유닛(FlameSpirit 6히트, LionKnight 2히트)의 hitFrameTimes는 오름차순 배열.
            // OccupancySize 기준 (한 타일 합계 상한 = 3):
            //   소형(1): Pistoleer / Assault / Sniper / EmberSpirit / FoxMagician
            //   중형(2): FlameSpirit / InfernoSpirit / LionKnight
            //   대형(3): BearGuard
            var entries = new List<UnitStatEntry>
            {
                // ── 인간계 ───────────────────────────────────────────────
                BuildEntry(UnitType.Pistoleer, maxHp:30, atk:6,  atkRange:1.0f, detectRange:1.0f, moveSpeed:0.5f,
                           atkCooldown:2.0f, hitFrames:new[] { 0.833f },
                           prodTime:5f,  gold:50,  pop:1, occupancy:1f),

                BuildEntry(UnitType.Assault,   maxHp:50, atk:1,  atkRange:2.0f, detectRange:2.0f, moveSpeed:1.0f,
                           atkCooldown:0.2f, hitFrames:new[] { 0.133f },
                           prodTime:10f, gold:100, pop:1, occupancy:1f),

                BuildEntry(UnitType.Sniper,    maxHp:30, atk:10, atkRange:5.0f, detectRange:5.0f, moveSpeed:0.25f,
                           atkCooldown:3.0f, hitFrames:new[] { 2.000f },
                           prodTime:15f, gold:200, pop:1, occupancy:1f),

                // ── 정령계 ───────────────────────────────────────────────
                BuildEntry(UnitType.FlameSpirit,  maxHp:50,  atk:2,  atkRange:0.5f, detectRange:1.0f, moveSpeed:2.0f,
                           atkCooldown:3.0f,
                           hitFrames:new[] { 0.667f, 1.167f, 1.433f, 1.667f, 1.933f, 2.100f },
                           prodTime:15f, gold:200, pop:1, occupancy:2f),

                BuildEntry(UnitType.EmberSpirit,   maxHp:30,  atk:5,  atkRange:0.5f, detectRange:1.0f, moveSpeed:0.5f,
                           atkCooldown:2.33f, hitFrames:new[] { 1.000f },
                           prodTime:5f,  gold:50,  pop:1, occupancy:1f),

                BuildEntry(UnitType.InfernoSpirit, maxHp:100, atk:25, atkRange:4.0f, detectRange:4.0f, moveSpeed:1.0f,
                           atkCooldown:3.0f, hitFrames:new[] { 1.250f },
                           prodTime:30f, gold:500, pop:1, occupancy:2f),

                // ── 초월계 ───────────────────────────────────────────────
                BuildEntry(UnitType.BearGuard,   maxHp:200, atk:10, atkRange:0.5f, detectRange:1.0f, moveSpeed:1.0f,
                           atkCooldown:1.33f, hitFrames:new[] { 0.667f },
                           prodTime:25f, gold:400, pop:1, occupancy:3f),

                BuildEntry(UnitType.FoxMagician, maxHp:20,  atk:8,  atkRange:3.0f, detectRange:3.0f, moveSpeed:0.5f,
                           atkCooldown:4.0f,  hitFrames:new[] { 2.417f },
                           prodTime:5f,  gold:50,  pop:1, occupancy:1f),

                BuildEntry(UnitType.LionKnight,  maxHp:50,  atk:9,  atkRange:0.5f, detectRange:1.0f, moveSpeed:2.0f,
                           atkCooldown:3.0f,  hitFrames:new[] { 0.733f, 1.267f },
                           prodTime:15f, gold:200, pop:1, occupancy:2f),
            };

            // private _stats 필드에 SerializedObject로 값 주입 + dirty 처리.
            var so = new SerializedObject(config);
            var listProp = so.FindProperty("_stats");
            listProp.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                SerializedProperty elem = listProp.GetArrayElementAtIndex(i);
                WriteEntryToProperty(elem, entries[i]);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        /// <summary>
        /// UnitStatEntry 한 건을 만드는 헬퍼. 이름 있는 인수로 호출해 가독성 확보.
        /// </summary>
        private static UnitStatEntry BuildEntry(
            UnitType type,
            int maxHp, int atk, float atkRange, float detectRange, float moveSpeed,
            float atkCooldown, float[] hitFrames,
            float prodTime, int gold, int pop,
            float occupancy)
        {
            return new UnitStatEntry
            {
                unitType = type,
                maxHp = maxHp,
                attackPower = atk,
                attackRange = atkRange,
                detectRange = detectRange,
                moveSpeed = moveSpeed,
                attackCooldown = atkCooldown,
                hitFrameTimes = hitFrames,
                productionTime = prodTime,
                goldCost = gold,
                populationCost = pop,
                occupancySize = occupancy
            };
        }

        /// <summary>
        /// SerializedProperty(UnitStatEntry 한 슬롯)에 entry의 필드값을 하나씩 기록.
        /// struct 필드 이름은 UnitStatEntry의 public 필드명과 반드시 일치해야 함.
        /// </summary>
        private static void WriteEntryToProperty(SerializedProperty elem, UnitStatEntry entry)
        {
            elem.FindPropertyRelative("unitType").enumValueIndex = (int)entry.unitType;
            elem.FindPropertyRelative("maxHp").intValue = entry.maxHp;
            elem.FindPropertyRelative("attackPower").intValue = entry.attackPower;
            elem.FindPropertyRelative("attackRange").floatValue = entry.attackRange;
            elem.FindPropertyRelative("detectRange").floatValue = entry.detectRange;
            elem.FindPropertyRelative("moveSpeed").floatValue = entry.moveSpeed;
            elem.FindPropertyRelative("attackCooldown").floatValue = entry.attackCooldown;

            var hitFramesProp = elem.FindPropertyRelative("hitFrameTimes");
            hitFramesProp.arraySize = entry.hitFrameTimes?.Length ?? 0;
            if (entry.hitFrameTimes != null)
            {
                for (int i = 0; i < entry.hitFrameTimes.Length; i++)
                {
                    hitFramesProp.GetArrayElementAtIndex(i).floatValue = entry.hitFrameTimes[i];
                }
            }

            elem.FindPropertyRelative("productionTime").floatValue = entry.productionTime;
            elem.FindPropertyRelative("goldCost").intValue = entry.goldCost;
            elem.FindPropertyRelative("populationCost").intValue = entry.populationCost;

            // OccupancySize 필드 — 한 타일에 들어갈 수 있는 유닛 수 한도(상한 3) 대비 차지하는 양.
            elem.FindPropertyRelative("occupancySize").floatValue = entry.occupancySize;
        }
    }
}
#endif
