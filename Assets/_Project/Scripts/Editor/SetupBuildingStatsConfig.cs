// ============================================================================
// SetupBuildingStatsConfig.cs
// BuildingStatsConfig(ScriptableObject) 에셋을 자동 생성하는 1회성 에디터 스크립트.
//
// 메뉴: Hexiege/Setup/BuildingStatsConfig 생성
//
// 동작:
//   1) Assets/_Project/Resources/Config/ 폴더가 없으면 자동 생성.
//   2) Assets/_Project/Resources/Config/BuildingStatsConfig.asset 존재 여부 확인.
//      - 있으면 덮어쓰지 않고 경고만 표시 (사용자 수정 보호).
//      - 없으면 신규 .asset 생성 후 현재 코드 기준 스탯값을 자동 입력.
//   3) 생성된 에셋을 Ping(Project 창 하이라이트)하여 찾기 쉽게 안내.
//
// 여기 입력되는 모든 수치는 기존 BuildingStats.cs (switch 표현식)와
// GameConfig.BarracksCost / MiningPostCost 에서 가져온 값과 1:1 동일.
//
//   | BuildingType | Human HP | Spirit HP | Trans HP | Human Gold | Spirit Gold | Trans Gold | ATK |
//   |--------------|----------|-----------|----------|------------|-------------|------------|-----|
//   | Castle       | 100      | 100       | 200      | 0          | 0           | 0          | 0   |
//   | Barracks     | 30       | 30        | 50       | 100        | 100         | 100        | 0   |
//   | MiningPost   | 20       | 20        | 40       | 50         | 50          | 50         | 0   |
//
// Editor 전용 — 빌드에 포함되지 않음.
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Infrastructure.EditorTools
{
    public static class SetupBuildingStatsConfig
    {
        // 생성 경로 — GameBootstrapper가 Inspector SerializedField로 로드하므로
        // Resources.Load 경로가 아니어도 상관없다.
        private const string FolderPath = "Assets/_Project/Resources/Config";
        private const string AssetPath = FolderPath + "/BuildingStatsConfig.asset";

        [MenuItem("Hexiege/Setup/BuildingStatsConfig 생성")]
        public static void CreateAsset()
        {
            // 1) 폴더 존재 보장 — 없으면 단계별로 생성.
            EnsureFolderExists(FolderPath);

            // 2) 기존 에셋이 있으면 덮어쓰지 않고 경고만.
            var existing = AssetDatabase.LoadAssetAtPath<BuildingStatsConfig>(AssetPath);
            if (existing != null)
            {
                Debug.LogWarning($"[SetupBuildingStatsConfig] 이미 에셋이 존재합니다: {AssetPath}\n" +
                                 "새로 만들려면 먼저 기존 파일을 삭제해 주세요.");
                EditorGUIUtility.PingObject(existing);
                Selection.activeObject = existing;
                return;
            }

            // 3) ScriptableObject 인스턴스 생성 → 기본 스탯값 주입 → .asset 저장.
            var config = ScriptableObject.CreateInstance<BuildingStatsConfig>();
            PopulateDefaults(config);

            AssetDatabase.CreateAsset(config, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SetupBuildingStatsConfig] 에셋 생성 완료: {AssetPath}\n" +
                      "Inspector에서 GameBootstrapper의 Building Stats Config 필드에 이 에셋을 연결해 주세요.");

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
        /// BuildingStatsConfig._stats는 private 필드이므로 SerializedObject로 접근.
        /// </summary>
        private static void PopulateDefaults(BuildingStatsConfig config)
        {
            // 기본 스탯 테이블 — 생산건물은 단계(1/2/3)별로 HP/비용/업그레이드 비용을 차등.
            // 모든 값은 운영 중 Inspector에서 자유롭게 조정 가능. 여기서는 합리적 기본값만 제공.
            //
            // 정책 요약:
            //   - 1단계 건물: HP 30(Trans 50), GoldCost 100, UpgradeCost 80
            //   - 2단계 건물: HP 45(Trans 75), GoldCost 150, UpgradeCost 120
            //   - 3단계 건물(라인 최고): HP 60(Trans 100), GoldCost 200, UpgradeCost 0(업그레이드 없음)
            //   - 2단계 라인 종점(Garage→VehicleBay, SporePatch→FloralNursery):
            //     VehicleBay/FloralNursery 도 UpgradeCost 0.
            var entries = new List<BuildingTypeEntry>
            {
                // ── 비생산 건물 ────────────────────────────────────────
                BuildEntry(BuildingType.Castle,
                    humanHp:   100, spiritHp:   100, transHp:   200,
                    humanGold: 0,   spiritGold: 0,   transGold: 0,
                    humanAtk:  0,   spiritAtk:  0,   transAtk:  0,
                    upgrade:   0),

                BuildEntry(BuildingType.MiningPost,
                    humanHp:   20,  spiritHp:   20,  transHp:   40,
                    humanGold: 50,  spiritGold: 50,  transGold: 50,
                    humanAtk:  0,   spiritAtk:  0,   transAtk:  0,
                    upgrade:   0),

                // ── Human — 근거리A 라인 ────────────────────────────────
                BuildEntry(BuildingType.TrainingCamp,
                    humanHp: 30, spiritHp: 30, transHp: 50,
                    humanGold: 100, spiritGold: 100, transGold: 100,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 80),
                BuildEntry(BuildingType.WarAcademy,
                    humanHp: 45, spiritHp: 45, transHp: 75,
                    humanGold: 150, spiritGold: 150, transGold: 150,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 120),
                BuildEntry(BuildingType.HumanBarracks,
                    humanHp: 60, spiritHp: 60, transHp: 100,
                    humanGold: 200, spiritGold: 200, transGold: 200,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 0),

                // ── Human — 총기류 라인 ────────────────────────────────
                BuildEntry(BuildingType.Gunsmith,
                    humanHp: 30, spiritHp: 30, transHp: 50,
                    humanGold: 100, spiritGold: 100, transGold: 100,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 80),
                BuildEntry(BuildingType.Armory,
                    humanHp: 45, spiritHp: 45, transHp: 75,
                    humanGold: 150, spiritGold: 150, transGold: 150,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 120),
                BuildEntry(BuildingType.WeaponForge,
                    humanHp: 60, spiritHp: 60, transHp: 100,
                    humanGold: 200, spiritGold: 200, transGold: 200,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 0),

                // ── Human — 탈것류 라인 (1→2) ─────────────────────────
                BuildEntry(BuildingType.Garage,
                    humanHp: 30, spiritHp: 30, transHp: 50,
                    humanGold: 100, spiritGold: 100, transGold: 100,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 80),
                BuildEntry(BuildingType.VehicleBay,
                    humanHp: 45, spiritHp: 45, transHp: 75,
                    humanGold: 150, spiritGold: 150, transGold: 150,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 0),

                // ── Spirit — 불 라인 ──────────────────────────────────
                BuildEntry(BuildingType.FireSpire,
                    humanHp: 30, spiritHp: 30, transHp: 50,
                    humanGold: 100, spiritGold: 100, transGold: 100,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 80),
                BuildEntry(BuildingType.BlazeConduit,
                    humanHp: 45, spiritHp: 45, transHp: 75,
                    humanGold: 150, spiritGold: 150, transGold: 150,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 120),
                BuildEntry(BuildingType.InfernoCore,
                    humanHp: 60, spiritHp: 60, transHp: 100,
                    humanGold: 200, spiritGold: 200, transGold: 200,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 0),

                // ── Spirit — 물 라인 ──────────────────────────────────
                BuildEntry(BuildingType.AquaSpring,
                    humanHp: 30, spiritHp: 30, transHp: 50,
                    humanGold: 100, spiritGold: 100, transGold: 100,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 80),
                BuildEntry(BuildingType.TidalNexus,
                    humanHp: 45, spiritHp: 45, transHp: 75,
                    humanGold: 150, spiritGold: 150, transGold: 150,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 120),
                BuildEntry(BuildingType.OceanicHeart,
                    humanHp: 60, spiritHp: 60, transHp: 100,
                    humanGold: 200, spiritGold: 200, transGold: 200,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 0),

                // ── Spirit — 땅 라인 ──────────────────────────────────
                BuildEntry(BuildingType.StoneMound,
                    humanHp: 30, spiritHp: 30, transHp: 50,
                    humanGold: 100, spiritGold: 100, transGold: 100,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 80),
                BuildEntry(BuildingType.TerraForge,
                    humanHp: 45, spiritHp: 45, transHp: 75,
                    humanGold: 150, spiritGold: 150, transGold: 150,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 120),
                BuildEntry(BuildingType.GaeaSanctum,
                    humanHp: 60, spiritHp: 60, transHp: 100,
                    humanGold: 200, spiritGold: 200, transGold: 200,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 0),

                // ── Transcendence — 동물A 라인 ────────────────────────
                BuildEntry(BuildingType.PrimalAltar,
                    humanHp: 30, spiritHp: 30, transHp: 50,
                    humanGold: 100, spiritGold: 100, transGold: 100,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 80),
                BuildEntry(BuildingType.PrimalDen,
                    humanHp: 45, spiritHp: 45, transHp: 75,
                    humanGold: 150, spiritGold: 150, transGold: 150,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 120),
                BuildEntry(BuildingType.PrimalSanctuary,
                    humanHp: 60, spiritHp: 60, transHp: 100,
                    humanGold: 200, spiritGold: 200, transGold: 200,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 0),

                // ── Transcendence — 동물B 라인 ────────────────────────
                BuildEntry(BuildingType.FeralAltar,
                    humanHp: 30, spiritHp: 30, transHp: 50,
                    humanGold: 100, spiritGold: 100, transGold: 100,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 80),
                BuildEntry(BuildingType.FeralDen,
                    humanHp: 45, spiritHp: 45, transHp: 75,
                    humanGold: 150, spiritGold: 150, transGold: 150,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 120),
                BuildEntry(BuildingType.FeralSanctuary,
                    humanHp: 60, spiritHp: 60, transHp: 100,
                    humanGold: 200, spiritGold: 200, transGold: 200,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 0),

                // ── Transcendence — 식물 라인 (1→2) ───────────────────
                BuildEntry(BuildingType.SporePatch,
                    humanHp: 30, spiritHp: 30, transHp: 50,
                    humanGold: 100, spiritGold: 100, transGold: 100,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 80),
                BuildEntry(BuildingType.FloralNursery,
                    humanHp: 45, spiritHp: 45, transHp: 75,
                    humanGold: 150, spiritGold: 150, transGold: 150,
                    humanAtk: 0, spiritAtk: 0, transAtk: 0,
                    upgrade: 0),
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
        /// BuildingTypeEntry 한 건을 만드는 헬퍼.
        /// 이름 있는 인수로 호출해 건물 타입별 종족 값을 한눈에 비교 가능하게 한다.
        /// upgrade: 다음 단계로 업그레이드 시 드는 골드 비용. 최고 단계 또는 비생산건물은 0.
        /// </summary>
        private static BuildingTypeEntry BuildEntry(
            BuildingType type,
            int humanHp, int spiritHp, int transHp,
            int humanGold, int spiritGold, int transGold,
            int humanAtk, int spiritAtk, int transAtk,
            int upgrade = 0)
        {
            return new BuildingTypeEntry
            {
                buildingType = type,

                humanMaxHp = humanHp,
                spiritMaxHp = spiritHp,
                transcendenceMaxHp = transHp,

                humanGoldCost = humanGold,
                spiritGoldCost = spiritGold,
                transcendenceGoldCost = transGold,

                humanAttackPower = humanAtk,
                spiritAttackPower = spiritAtk,
                transcendenceAttackPower = transAtk,

                upgradeCost = upgrade
            };
        }

        /// <summary>
        /// SerializedProperty(BuildingTypeEntry 한 슬롯)에 entry의 필드값을 하나씩 기록.
        /// struct 필드 이름은 BuildingTypeEntry의 public 필드명과 반드시 일치해야 함.
        /// </summary>
        private static void WriteEntryToProperty(SerializedProperty elem, BuildingTypeEntry entry)
        {
            elem.FindPropertyRelative("buildingType").enumValueIndex = (int)entry.buildingType;

            elem.FindPropertyRelative("humanMaxHp").intValue = entry.humanMaxHp;
            elem.FindPropertyRelative("spiritMaxHp").intValue = entry.spiritMaxHp;
            elem.FindPropertyRelative("transcendenceMaxHp").intValue = entry.transcendenceMaxHp;

            elem.FindPropertyRelative("humanGoldCost").intValue = entry.humanGoldCost;
            elem.FindPropertyRelative("spiritGoldCost").intValue = entry.spiritGoldCost;
            elem.FindPropertyRelative("transcendenceGoldCost").intValue = entry.transcendenceGoldCost;

            elem.FindPropertyRelative("humanAttackPower").intValue = entry.humanAttackPower;
            elem.FindPropertyRelative("spiritAttackPower").intValue = entry.spiritAttackPower;
            elem.FindPropertyRelative("transcendenceAttackPower").intValue = entry.transcendenceAttackPower;

            elem.FindPropertyRelative("upgradeCost").intValue = entry.upgradeCost;
        }
    }
}
#endif
