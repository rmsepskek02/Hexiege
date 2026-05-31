// ====================================================================================
// ConnectProductionPanelUI.cs
// ------------------------------------------------------------------------------------
// [목적]
//   ProductionPanelUI 컴포넌트의 미연결(null) Inspector 필드를 자동 연결한다.
//
// [사용 방법]
//   1) Game.unity 씬을 열고
//   2) Hexiege/Setup/ProductionPanelUI 필드 자동 연결 클릭
//   3) Console 로그로 연결 결과 확인 후 Ctrl+S 저장
// ====================================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

namespace Hexiege.EditorTools
{
    public static class ConnectProductionPanelUI
    {
        [MenuItem("Hexiege/Setup/ProductionPanelUI 필드 자동 연결")]
        public static void Connect()
        {
            // ── 1) ProductionPopup 탐색 ────────────────────────────────────────────
            Transform popup = FindInScene("ProductionPopup");
            if (popup == null)
            {
                Debug.LogError("[Connect] ProductionPopup을 씬에서 찾을 수 없습니다.");
                return;
            }
            Debug.Log($"[Connect] ProductionPopup 발견: {GetPath(popup)}");

            // ── 2) ProductionPanelUI 컴포넌트 탐색 ────────────────────────────────
            // GetComponent(string) 대신 GetComponents<MonoBehaviour>() + 타입명 비교로 더 안정적으로 탐색
            MonoBehaviour ui = popup.GetComponents<MonoBehaviour>()
                .FirstOrDefault(c => c.GetType().Name == "ProductionPanelUI");
            if (ui == null)
            {
                Debug.LogError("[Connect] ProductionPopup에서 ProductionPanelUI 컴포넌트를 찾을 수 없습니다.");
                return;
            }
            Debug.Log($"[Connect] ProductionPanelUI 발견: {ui.GetType().FullName}");

            // ── 3) 계층 탐색 ──────────────────────────────────────────────────────
            Transform panel         = popup.Find("ProductionPanel");
            Transform gridContainer = panel != null ? panel.Find("GridContainer") : null;
            Transform row0          = gridContainer != null ? gridContainer.Find("Row0") : null;
            Transform row1          = gridContainer != null ? gridContainer.Find("Row1") : null;

            Debug.Log($"[Connect] ProductionPanel: {(panel != null ? "OK" : "NULL")}");
            Debug.Log($"[Connect] GridContainer  : {(gridContainer != null ? "OK" : "NULL")}");
            Debug.Log($"[Connect] Row0           : {(row0 != null ? "OK" : "NULL")}");
            Debug.Log($"[Connect] Row1           : {(row1 != null ? "OK" : "NULL")}");

            if (row0 == null || row1 == null)
            {
                Debug.LogError("[Connect] Row0 또는 Row1을 찾을 수 없습니다. GridContainer 하위 구조를 확인하세요.");
                return;
            }

            // Row0 슬롯
            Transform slot1 = row0.Find("Slot1");
            Transform slot2 = row0.Find("Slot2");
            Transform slot3 = row0.Find("Slot3");
            Debug.Log($"[Connect] Slot1={slot1 != null}, Slot2={slot2 != null}, Slot3={slot3 != null}");

            // Row1 액션 버튼
            Transform slot5   = row1.Find("Slot5");
            Transform destroy = row1.Find("Destroy");
            Debug.Log($"[Connect] Slot5(업그레이드)={slot5 != null}, Destroy={destroy != null}");

            // Row0 BorderOverlay 3개 수집
            Transform[] bos = CollectChildren(row0, "BorderOverlay");
            Debug.Log($"[Connect] BorderOverlay 개수: {bos.Length}");

            // ── 4) SerializedObject 필드 연결 ─────────────────────────────────────
            var so = new SerializedObject(ui);
            so.Update();

            // _unitButtons [0/1/2]: Slot의 Button 컴포넌트
            Connect(so, "_unitButtons",       0, GetComp<Button>(slot1, "Slot1"));
            Connect(so, "_unitButtons",       1, GetComp<Button>(slot2, "Slot2"));
            Connect(so, "_unitButtons",       2, GetComp<Button>(slot3, "Slot3"));

            // _unitButtonPortraits [0/1/2]: IconImage의 Image 컴포넌트
            // GetComponentsInChildren으로 "IconImage" 이름의 Image를 직접 탐색한다.
            Connect(so, "_unitButtonPortraits", 0, FindImageByName(slot1, "IconImage"));
            Connect(so, "_unitButtonPortraits", 1, FindImageByName(slot2, "IconImage"));
            Connect(so, "_unitButtonPortraits", 2, FindImageByName(slot3, "IconImage"));

            // _unitCostTexts [0/1/2]: CostContainer/CostText의 TMP
            Connect(so, "_unitCostTexts", 0, FindTMPByName(slot1, "CostText"));
            Connect(so, "_unitCostTexts", 1, FindTMPByName(slot2, "CostText"));
            Connect(so, "_unitCostTexts", 2, FindTMPByName(slot3, "CostText"));

            // _unitButtonGroups [0/1/2]: Slot의 CanvasGroup
            Connect(so, "_unitButtonGroups", 0, GetComp<CanvasGroup>(slot1, "Slot1"));
            Connect(so, "_unitButtonGroups", 1, GetComp<CanvasGroup>(slot2, "Slot2"));
            Connect(so, "_unitButtonGroups", 2, GetComp<CanvasGroup>(slot3, "Slot3"));

            // _unitBorderOverlays [0/1/2]: BorderOverlay의 Image
            if (bos.Length >= 3)
            {
                Connect(so, "_unitBorderOverlays", 0, GetComp<Image>(bos[0], "BorderOverlay[0]"));
                Connect(so, "_unitBorderOverlays", 1, GetComp<Image>(bos[1], "BorderOverlay[1]"));
                Connect(so, "_unitBorderOverlays", 2, GetComp<Image>(bos[2], "BorderOverlay[2]"));
            }
            else
            {
                Debug.LogWarning($"[Connect] BorderOverlay가 {bos.Length}개. Row0에 3개 있어야 합니다.");
            }

            // _upgradeButtonGroup: Slot5의 CanvasGroup
            ConnectSingle(so, "_upgradeButtonGroup", GetComp<CanvasGroup>(slot5, "Slot5"));

            // _demolishButton: Destroy의 Button
            ConnectSingle(so, "_demolishButton", GetComp<Button>(destroy, "Destroy"));

            // ── 5) 적용 및 저장 표시 ──────────────────────────────────────────────
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ui);

            // ── 6) 연결 결과 검증 ─────────────────────────────────────────────────
            so.Update();
            Debug.Log("[Connect] ─── 연결 결과 검증 ───");
            VerifyList(so, "_unitButtons",        3);
            VerifyList(so, "_unitButtonPortraits", 3);
            VerifyList(so, "_unitCostTexts",       3);
            VerifyList(so, "_unitButtonGroups",    3);
            VerifyList(so, "_unitBorderOverlays",  3);
            VerifySingle(so, "_upgradeButtonGroup");
            VerifySingle(so, "_demolishButton");

            Debug.Log("[Connect] 완료. Ctrl+S로 씬을 저장하세요.");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 연결 헬퍼
        // ─────────────────────────────────────────────────────────────────────────

        private static void Connect(SerializedObject so, string field, int index, Object obj)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[Connect] 프로퍼티 없음: {field}");
                return;
            }
            while (prop.arraySize <= index)
                prop.InsertArrayElementAtIndex(prop.arraySize);

            prop.GetArrayElementAtIndex(index).objectReferenceValue = obj;

            string result = obj != null ? $"{obj.GetType().Name} ({obj.name})" : "NULL";
            Debug.Log($"[Connect] {field}[{index}] = {result}");
        }

        private static void ConnectSingle(SerializedObject so, string field, Object obj)
        {
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"[Connect] 프로퍼티 없음: {field}"); return; }
            prop.objectReferenceValue = obj;
            string result = obj != null ? $"{obj.GetType().Name} ({obj.name})" : "NULL";
            Debug.Log($"[Connect] {field} = {result}");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 검증 헬퍼
        // ─────────────────────────────────────────────────────────────────────────

        private static void VerifyList(SerializedObject so, string field, int expectedCount)
        {
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"[Verify] 프로퍼티 없음: {field}"); return; }
            int nullCount = 0;
            for (int i = 0; i < prop.arraySize; i++)
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue == null) nullCount++;
            string status = nullCount == 0 ? "✓" : $"⚠ null {nullCount}개";
            Debug.Log($"[Verify] {field}[{prop.arraySize}] {status}");
        }

        private static void VerifySingle(SerializedObject so, string field)
        {
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"[Verify] 프로퍼티 없음: {field}"); return; }
            string status = prop.objectReferenceValue != null ? "✓" : "⚠ null";
            Debug.Log($"[Verify] {field} {status}");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 컴포넌트 탐색 헬퍼
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>GetComponentsInChildren으로 이름이 일치하는 첫 번째 Image를 반환한다.</summary>
        private static Image FindImageByName(Transform root, string goName)
        {
            if (root == null) { Debug.LogWarning($"[Connect] FindImageByName: root null ({goName})"); return null; }
            foreach (var img in root.GetComponentsInChildren<Image>(true))
            {
                if (img.gameObject.name == goName)
                {
                    Debug.Log($"[Connect] FindImageByName '{goName}' → {GetPath(img.transform)} ✓");
                    return img;
                }
            }
            Debug.LogWarning($"[Connect] '{goName}' Image를 {root.name} 하위에서 찾을 수 없습니다.");
            return null;
        }

        /// <summary>GetComponentsInChildren으로 이름이 일치하는 첫 번째 TMP를 반환한다.</summary>
        private static TextMeshProUGUI FindTMPByName(Transform root, string goName)
        {
            if (root == null) { Debug.LogWarning($"[Connect] FindTMPByName: root null ({goName})"); return null; }
            foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp.gameObject.name == goName)
                    return tmp;
            }
            Debug.LogWarning($"[Connect] '{goName}' TMP를 {root.name} 하위에서 찾을 수 없습니다.");
            return null;
        }

        /// <summary>Transform에서 컴포넌트를 가져온다.</summary>
        private static T GetComp<T>(Transform tr, string label) where T : Component
        {
            if (tr == null) { Debug.LogWarning($"[Connect] GetComp: {label}이 null"); return null; }
            var comp = tr.GetComponent<T>();
            if (comp == null) Debug.LogWarning($"[Connect] {label}에서 {typeof(T).Name} 없음");
            return comp;
        }

        /// <summary>특정 이름의 직접 자식을 모두 수집한다.</summary>
        private static Transform[] CollectChildren(Transform parent, string name)
        {
            var list = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == name) list.Add(c);
            }
            return list.ToArray();
        }

        /// <summary>씬 전체에서 해당 이름의 Transform을 탐색한다 (비활성 포함).</summary>
        private static Transform FindInScene(string name)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.gameObject.scene.isLoaded && t.name == name)
                    return t;
            return null;
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }
    }
}
#endif
