// ============================================================================
// SetupStatCostTexts.cs
// ProductionPanelUI와 BuildingPlacementUI의 골드 비용 텍스트(GoldText)를
// 코드 SerializedField에 자동으로 연결하는 1회성 에디터 스크립트.
//
// 메뉴: Hexiege/Setup/스탯 비용 텍스트 연결
//
// 실제 씬 UI 구조:
//   [ProductionPopup]
//     UnitsButtons > UnitButtons1 > Slot1/2/3
//       각 Slot 하위에 GoldText (TextMeshProUGUI) 이미 존재
//       → _slot1CostText, _slot2CostText, _slot3CostText에 연결
//
//   [BuildingPopup]
//     BuildingButtons > BuildingButtons1 > Slot1/Slot2
//       각 Slot 하위에 GoldText (TextMeshProUGUI) 이미 존재
//       → _barracksCostText, _miningPostCostText에 연결
//
// 동작:
//   1. 씬에서 ProductionPanelUI / BuildingPlacementUI 컴포넌트를 찾음
//   2. 각 버튼(_pistoleerButton 등) 하위에서 "GoldText" 이름의 자식을 탐색
//   3. 해당 TextMeshProUGUI를 SerializedField에 연결
//   4. 이미 연결된 필드는 덮어쓰지 않음 (안전)
//
// 새 오브젝트를 생성하지 않음 — 기존 GoldText를 재활용.
//
// Editor 전용 — 빌드에 포함되지 않음.
// ============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Hexiege.Presentation;

namespace Hexiege.Editor
{
    public static class SetupStatCostTexts
    {
        private const string MenuPath = "Hexiege/Setup/스탯 비용 텍스트 연결";

        // 씬에서 찾을 자식 오브젝트 이름 (실제 씬 구조 기준)
        private const string GoldTextName = "GoldText";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            int changeCount = 0;

            changeCount += SetupProductionPanel();
            changeCount += SetupBuildingPlacementPanel();

            if (changeCount == 0)
            {
                Debug.Log("[SetupStatCostTexts] 변경 사항 없음. 이미 모두 연결되어 있거나 컴포넌트/GoldText를 찾지 못했습니다.");
                return;
            }

            // 씬 변경사항을 저장 대기 상태로 표시
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[SetupStatCostTexts] 완료. {changeCount}개 GoldText 연결됨. 씬을 저장하세요 (Ctrl+S).");
        }

        // ====================================================================
        // ProductionPanelUI — 유닛 버튼 3개의 GoldText를 _slot1/2/3CostText에 연결
        // ====================================================================

        /// <summary>
        /// 씬의 ProductionPanelUI를 찾아 _pistoleerButton, _assaultButton, _sniperButton
        /// 각각의 하위 "GoldText" 오브젝트를 _slot1/2/3CostText에 연결합니다.
        /// </summary>
        private static int SetupProductionPanel()
        {
            var ui = Object.FindAnyObjectByType<ProductionPanelUI>();
            if (ui == null)
            {
                Debug.LogWarning("[SetupStatCostTexts] ProductionPanelUI를 씬에서 찾을 수 없습니다.");
                return 0;
            }

            var so = new SerializedObject(ui);

            // 유닛 버튼 SerializedField 참조
            var pistoleerBtnProp = so.FindProperty("_pistoleerButton");
            var assaultBtnProp   = so.FindProperty("_assaultButton");
            var sniperBtnProp    = so.FindProperty("_sniperButton");

            // 비용 텍스트 SerializedField 참조
            var slot1CostProp = so.FindProperty("_slot1CostText");
            var slot2CostProp = so.FindProperty("_slot2CostText");
            var slot3CostProp = so.FindProperty("_slot3CostText");

            // 버튼 컴포넌트 가져오기
            var btn1 = pistoleerBtnProp?.objectReferenceValue as UnityEngine.UI.Button;
            var btn2 = assaultBtnProp?.objectReferenceValue as UnityEngine.UI.Button;
            var btn3 = sniperBtnProp?.objectReferenceValue as UnityEngine.UI.Button;

            if (btn1 == null || btn2 == null || btn3 == null)
            {
                Debug.LogWarning("[SetupStatCostTexts] ProductionPanelUI 유닛 버튼이 Inspector에 연결되지 않았습니다." +
                                 " (_pistoleerButton / _assaultButton / _sniperButton)");
                return 0;
            }

            int count = 0;

            // 각 버튼 하위에서 "GoldText" 자식을 찾아 SerializedField에 연결
            if (TryAssignGoldText(btn1.gameObject, slot1CostProp, "Slot1(PistoleerButton)")) count++;
            if (TryAssignGoldText(btn2.gameObject, slot2CostProp, "Slot2(AssaultButton)"))   count++;
            if (TryAssignGoldText(btn3.gameObject, slot3CostProp, "Slot3(SniperButton)"))     count++;

            so.ApplyModifiedProperties();

            Debug.Log($"[SetupStatCostTexts] ProductionPanelUI: {count}개 GoldText 연결 완료.");
            return count;
        }

        // ====================================================================
        // BuildingPlacementUI — 건물 버튼 2개의 GoldText를 _barracksCostText 등에 연결
        // ====================================================================

        /// <summary>
        /// 씬의 BuildingPlacementUI를 찾아 _barracksButton, _miningPostButton
        /// 각각의 하위 "GoldText" 오브젝트를 _barracksCostText, _miningPostCostText에 연결합니다.
        /// </summary>
        private static int SetupBuildingPlacementPanel()
        {
            var ui = Object.FindAnyObjectByType<BuildingPlacementUI>();
            if (ui == null)
            {
                Debug.LogWarning("[SetupStatCostTexts] BuildingPlacementUI를 씬에서 찾을 수 없습니다.");
                return 0;
            }

            var so = new SerializedObject(ui);

            // 버튼 SerializedField 참조
            var barracksBtnProp   = so.FindProperty("_barracksButton");
            var miningPostBtnProp = so.FindProperty("_miningPostButton");

            // 비용 텍스트 SerializedField 참조
            var barracksCostProp   = so.FindProperty("_barracksCostText");
            var miningPostCostProp = so.FindProperty("_miningPostCostText");

            // 버튼 컴포넌트 가져오기
            var barracksBtn   = barracksBtnProp?.objectReferenceValue as UnityEngine.UI.Button;
            var miningPostBtn = miningPostBtnProp?.objectReferenceValue as UnityEngine.UI.Button;

            if (barracksBtn == null || miningPostBtn == null)
            {
                Debug.LogWarning("[SetupStatCostTexts] BuildingPlacementUI 버튼이 Inspector에 연결되지 않았습니다." +
                                 " (_barracksButton / _miningPostButton)");
                return 0;
            }

            int count = 0;

            // 각 버튼 하위 GoldText 연결
            if (TryAssignGoldText(barracksBtn.gameObject,   barracksCostProp,   "Slot1(BarracksButton)"))   count++;
            if (TryAssignGoldText(miningPostBtn.gameObject, miningPostCostProp, "Slot2(MiningPostButton)")) count++;

            so.ApplyModifiedProperties();

            Debug.Log($"[SetupStatCostTexts] BuildingPlacementUI: {count}개 GoldText 연결 완료.");
            return count;
        }

        // ====================================================================
        // 공통 유틸
        // ====================================================================

        /// <summary>
        /// 지정된 부모 오브젝트의 직속 자식 중 "GoldText" 이름의 TextMeshProUGUI를 찾아
        /// SerializedProperty에 연결합니다.
        ///
        /// - 이미 연결된 경우 스킵 (덮어쓰지 않음)
        /// - GoldText 자식이 없으면 경고 로그 출력
        /// </summary>
        /// <param name="parent">탐색 대상 부모 오브젝트</param>
        /// <param name="prop">연결할 SerializedProperty</param>
        /// <param name="debugLabel">로그용 식별 문자열</param>
        /// <returns>실제로 연결이 이루어졌으면 true</returns>
        private static bool TryAssignGoldText(
            GameObject parent, SerializedProperty prop, string debugLabel)
        {
            if (prop == null)
            {
                Debug.LogWarning($"[SetupStatCostTexts] {debugLabel}: SerializedProperty를 찾을 수 없습니다. 필드명을 확인하세요.");
                return false;
            }

            // 이미 연결되어 있으면 스킵
            if (prop.objectReferenceValue != null)
            {
                Debug.Log($"[SetupStatCostTexts] {debugLabel}: 이미 연결됨 — 스킵.");
                return false;
            }

            // 직속 자식에서 "GoldText" 이름의 오브젝트 탐색
            var goldTextTransform = parent.transform.Find(GoldTextName);
            if (goldTextTransform == null)
            {
                Debug.LogWarning($"[SetupStatCostTexts] {debugLabel}: '{GoldTextName}' 자식 오브젝트를 찾을 수 없습니다.");
                return false;
            }

            var tmp = goldTextTransform.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                Debug.LogWarning($"[SetupStatCostTexts] {debugLabel}: '{GoldTextName}'에 TextMeshProUGUI 컴포넌트가 없습니다.");
                return false;
            }

            prop.objectReferenceValue = tmp;
            Debug.Log($"[SetupStatCostTexts] {debugLabel}: '{GoldTextName}' 연결 완료.");
            return true;
        }
    }
}
#endif
