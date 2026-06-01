// ============================================================================
// SetupAutoTowerStats.cs
// BuildingStatsConfig.asset에 AutoTower(자동 방어 포탑) 종족별 스탯을
// 자동으로 입력하는 1회성 에디터 유틸리티.
//
// 왜 필요한가?
//   BuildingTypeEntry 구조체에 attackRange(공격 사거리) 필드가 새로 추가되면서,
//   기존 .asset에는 이 필드 값이 비어 있다. AutoTower 항목의 전체 스탯
//   (HP / 건설비용 / 공격력 / 사거리 / 쿨다운)을 StatsReference.md 기준값으로
//   일괄 기록하기 위한 스크립트다.
//
// 동작 방식:
//   - private [SerializeField] List<BuildingTypeEntry> _stats 에 직접 접근할 수 없으므로
//     SerializedObject / SerializedProperty 로 안전하게 읽고 쓴다. (리플렉션보다 안전)
//   - 기존에 buildingType == AutoTower 인 항목이 있으면 그 항목을 덮어쓰고,
//     없으면 리스트 맨 끝에 새 항목을 추가한다.
//
// 사용법:
//   Unity 상단 메뉴 → Hexiege/Setup/AutoTower 스탯 입력
//
// ⚠️ 이 스크립트는 1회성 셋업 도구다. 실행 후 삭제해도 무방하다.
//    (.asset 파일에 값이 기록되고 나면 더 이상 필요 없음)
// ============================================================================

using UnityEditor;
using UnityEngine;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// BuildingStatsConfig.asset 의 AutoTower 스탯을 일괄 입력하는 1회성 에디터 도구.
    /// </summary>
    public static class SetupAutoTowerStats
    {
        // ----- 입력 대상 에셋 경로 -----
        private const string ConfigAssetPath =
            "Assets/_Project/Resources/Config/BuildingStatsConfig.asset";

        // BuildingType.AutoTower 의 정수 값. (BuildingType 열거형에서 AutoTower = 2)
        // BuildingTypeEntry.buildingType 은 enum 으로 직렬화되므로 SerializedProperty 에서는
        // intValue 로 읽고 쓴다.
        private const int AutoTowerTypeValue = (int)Hexiege.Domain.BuildingType.AutoTower;

        // ----- StatsReference.md 기준 AutoTower 스탯 값 -----
        // HP
        private const int HumanMaxHp = 50;
        private const int SpiritMaxHp = 150;
        private const int TranscendenceMaxHp = 100;

        // 건설 비용 (골드)
        private const int HumanGoldCost = 150;
        private const int SpiritGoldCost = 200;
        private const int TranscendenceGoldCost = 175;

        // 공격력
        private const int HumanAttackPower = 15;
        private const int SpiritAttackPower = 15;
        private const int TranscendenceAttackPower = 15;

        // 공격 사거리 (타일 단위)
        private const float HumanAttackRange = 4.0f;
        private const float SpiritAttackRange = 4.0f;
        private const float TranscendenceAttackRange = 4.0f;

        // 공격 쿨다운 (초)
        private const float HumanAttackCooldown = 5.0f;
        private const float SpiritAttackCooldown = 3.5f;
        private const float TranscendenceAttackCooldown = 5.0f;

        // AutoTower 는 비생산 건물이므로 업그레이드 비용 없음
        private const int UpgradeCost = 0;

        /// <summary>
        /// AutoTower 스탯을 BuildingStatsConfig.asset 에 입력한다.
        /// 메뉴: Hexiege/Setup/AutoTower 스탯 입력
        /// </summary>
        [MenuItem("Hexiege/Setup/AutoTower 스탯 입력")]
        public static void Run()
        {
            // 1) 대상 에셋 로드
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ConfigAssetPath);
            if (config == null)
            {
                Debug.LogError($"[SetupAutoTowerStats] 에셋을 찾을 수 없습니다: {ConfigAssetPath}");
                return;
            }

            // 2) SerializedObject 로 private _stats 리스트에 접근
            //    (private [SerializeField] 필드는 SerializedObject 를 통해서만 안전하게 수정 가능)
            var so = new SerializedObject(config);
            SerializedProperty statsProp = so.FindProperty("_stats");
            if (statsProp == null || !statsProp.isArray)
            {
                Debug.LogError("[SetupAutoTowerStats] '_stats' 직렬화 프로퍼티를 찾지 못했습니다. " +
                               "BuildingStatsConfig 의 필드명이 변경되었는지 확인하세요.");
                return;
            }

            // 3) 기존 AutoTower 항목 탐색 (buildingType == AutoTower)
            SerializedProperty targetEntry = null;
            for (int i = 0; i < statsProp.arraySize; i++)
            {
                SerializedProperty entry = statsProp.GetArrayElementAtIndex(i);
                SerializedProperty typeProp = entry.FindPropertyRelative("buildingType");
                if (typeProp != null && typeProp.intValue == AutoTowerTypeValue)
                {
                    targetEntry = entry;
                    break;
                }
            }

            // 4) 없으면 리스트 끝에 새 항목 추가
            bool isNew = false;
            if (targetEntry == null)
            {
                statsProp.arraySize++;
                targetEntry = statsProp.GetArrayElementAtIndex(statsProp.arraySize - 1);
                isNew = true;
            }

            // 5) 항목에 값 기록
            WriteEntry(targetEntry);

            // 6) 변경 사항 저장
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 7) 결과 로그 출력
            string action = isNew ? "새로 추가" : "덮어쓰기";
            Debug.Log(
                $"[SetupAutoTowerStats] AutoTower 스탯 {action} 완료 ({ConfigAssetPath})\n" +
                $"  HP            : Human {HumanMaxHp} / Spirit {SpiritMaxHp} / Transcendence {TranscendenceMaxHp}\n" +
                $"  건설비용      : Human {HumanGoldCost} / Spirit {SpiritGoldCost} / Transcendence {TranscendenceGoldCost}\n" +
                $"  공격력        : Human {HumanAttackPower} / Spirit {SpiritAttackPower} / Transcendence {TranscendenceAttackPower}\n" +
                $"  공격 사거리   : Human {HumanAttackRange} / Spirit {SpiritAttackRange} / Transcendence {TranscendenceAttackRange}\n" +
                $"  공격 쿨다운   : Human {HumanAttackCooldown} / Spirit {SpiritAttackCooldown} / Transcendence {TranscendenceAttackCooldown}");
        }

        /// <summary>
        /// 하나의 BuildingTypeEntry 직렬화 항목에 AutoTower 의 모든 스탯 값을 기록한다.
        /// </summary>
        /// <param name="entry">기록 대상 BuildingTypeEntry 직렬화 프로퍼티</param>
        private static void WriteEntry(SerializedProperty entry)
        {
            // buildingType (enum → intValue)
            entry.FindPropertyRelative("buildingType").intValue = AutoTowerTypeValue;

            // HP
            entry.FindPropertyRelative("humanMaxHp").intValue = HumanMaxHp;
            entry.FindPropertyRelative("spiritMaxHp").intValue = SpiritMaxHp;
            entry.FindPropertyRelative("transcendenceMaxHp").intValue = TranscendenceMaxHp;

            // 건설 비용
            entry.FindPropertyRelative("humanGoldCost").intValue = HumanGoldCost;
            entry.FindPropertyRelative("spiritGoldCost").intValue = SpiritGoldCost;
            entry.FindPropertyRelative("transcendenceGoldCost").intValue = TranscendenceGoldCost;

            // 공격력
            entry.FindPropertyRelative("humanAttackPower").intValue = HumanAttackPower;
            entry.FindPropertyRelative("spiritAttackPower").intValue = SpiritAttackPower;
            entry.FindPropertyRelative("transcendenceAttackPower").intValue = TranscendenceAttackPower;

            // 공격 쿨다운
            entry.FindPropertyRelative("humanAttackCooldown").floatValue = HumanAttackCooldown;
            entry.FindPropertyRelative("spiritAttackCooldown").floatValue = SpiritAttackCooldown;
            entry.FindPropertyRelative("transcendenceAttackCooldown").floatValue = TranscendenceAttackCooldown;

            // 공격 사거리 (방금 추가된 필드)
            entry.FindPropertyRelative("humanAttackRange").floatValue = HumanAttackRange;
            entry.FindPropertyRelative("spiritAttackRange").floatValue = SpiritAttackRange;
            entry.FindPropertyRelative("transcendenceAttackRange").floatValue = TranscendenceAttackRange;

            // 업그레이드 비용 (AutoTower 는 0)
            entry.FindPropertyRelative("upgradeCost").intValue = UpgradeCost;
        }
    }
}
