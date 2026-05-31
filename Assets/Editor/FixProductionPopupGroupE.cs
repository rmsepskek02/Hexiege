// ====================================================================================
// FixProductionPopupGroupE.cs
// ------------------------------------------------------------------------------------
// [목적]
//   ProductionPopup > ProductionPanel > GridContainer > Row0 안의 BorderOverlay
//   오브젝트 3개가 가진 고정 픽셀 위치/크기(anchoredPosition, sizeDelta)를 0으로
//   정리한다. 이 오브젝트들은 Slot1/Slot2/Slot3의 바로 다음 형제(sibling)로 위치한다.
//
// [GameSystemRules 근거]
//   규칙 2 — 고정 픽셀 크기/위치 대신 앵커 비율 기반 배치.
//            BorderOverlay는 이미 anchor가 (0,0)~(1,1) stretch 상태이므로,
//            남아있는 고정 픽셀 오프셋(anchoredPosition, sizeDelta)만 0으로 정리하면
//            부모를 정확히 채우는 순수 앵커 stretch가 된다.
//
// [핵심 원칙]
//   sprite가 NONE(미할당)이므로 시각 변화는 없다. 픽셀 오프셋만 정리한다.
//   anchor (0,0)~(1,1)은 그대로 유지한다.
//
// [현재 → 수정]
//   현재: pos=(-284.66,0)/(-1,0)/(290,0), sizeDelta=(-569.32,0), anchor=(0,0)~(1,1)
//   수정: pos=(0,0), sizeDelta=(0,0), anchor=(0,0)~(1,1) 유지
//
// [사용 방법]
//   1) Unity Editor에서 Game.unity 씬을 연다. (반드시 열려 있어야 함)
//   2) 상단 메뉴 Hexiege > Fix > ProductionPopup > GroupE 클릭
//   3) 실행 후 Ctrl+Z로 되돌리기 가능
//
// [주의] 이 스크립트는 1회성이므로 실행 후 직접 삭제해도 무방하다.
// ====================================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// Row0 안 BorderOverlay 3개의 고정 픽셀 오프셋을 0으로 정리하는 1회성 에디터 스크립트.
    /// </summary>
    public static class FixProductionPopupGroupE
    {
        // Row0까지의 계층 경로. Slot/BorderOverlay는 Row0의 직접 자식이다.
        private const string Row0Path = "ProductionPopup/ProductionPanel/GridContainer/Row0";
        private const string BorderOverlayName = "BorderOverlay";

        [MenuItem("Hexiege/Fix/ProductionPopup/GroupE")]
        public static void Fix()
        {
            RectTransform row0 = FindRectTransform(Row0Path);
            if (row0 == null)
            {
                Debug.LogError($"[GroupE] 오브젝트를 찾을 수 없습니다: {Row0Path}\n" +
                               "Game.unity 씬이 열려 있는지 확인하세요.");
                return;
            }

            // Row0의 모든 직접 자식 중 이름이 BorderOverlay인 오브젝트를 전부 수집한다.
            // Transform.Find는 첫 번째 일치만 반환하므로, 3개를 모두 다루기 위해
            // 자식을 순회하며 직접 수집한다.
            List<RectTransform> borders = new List<RectTransform>();
            for (int i = 0; i < row0.childCount; i++)
            {
                Transform child = row0.GetChild(i);
                if (child.name == BorderOverlayName)
                {
                    var rt = child.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        borders.Add(rt);
                    }
                }
            }

            if (borders.Count == 0)
            {
                Debug.LogError($"[GroupE] {Row0Path} 안에서 '{BorderOverlayName}' 오브젝트를 찾지 못했습니다.");
                return;
            }

            // 수집한 BorderOverlay들의 고정 픽셀 오프셋을 0으로 정리한다.
            // anchor는 (0,0)~(1,1)로 유지(보장)하고, anchoredPosition/sizeDelta만 0으로 만든다.
            foreach (var border in borders)
            {
                Undo.RecordObject(border, "Fix BorderOverlay Offset (GroupE)");
                border.anchorMin = new Vector2(0f, 0f); // stretch 유지(보장)
                border.anchorMax = new Vector2(1f, 1f); // stretch 유지(보장)
                border.anchoredPosition = Vector2.zero;
                border.sizeDelta = Vector2.zero;
                EditorUtility.SetDirty(border);
            }

            Debug.Log($"[GroupE] 완료: BorderOverlay {borders.Count}개의 픽셀 오프셋을 0으로 정리했습니다. " +
                      "Ctrl+S로 씬을 저장하세요.");
        }

        /// <summary>
        /// 씬 루트부터 계층 경로를 따라 내려가며 RectTransform을 탐색한다.
        /// </summary>
        private static RectTransform FindRectTransform(string hierarchyPath)
        {
            GameObject go = FindByHierarchyPath(hierarchyPath);
            return go != null ? go.GetComponent<RectTransform>() : null;
        }

        /// <summary>
        /// 활성/비활성을 모두 포함해 계층 경로에 해당하는 오브젝트를 탐색한다.
        /// </summary>
        private static GameObject FindByHierarchyPath(string hierarchyPath)
        {
            string[] segments = hierarchyPath.Split('/');
            if (segments.Length == 0) return null;

            // ProductionPopup은 [UI]/SafeAreaContainer 아래에 있어 루트 탐색으로는 찾을 수 없으므로
            // Resources.FindObjectsOfTypeAll로 씬 전체를 검색한다 (비활성 포함).
            Transform current = null;
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t.gameObject.scene.isLoaded && t.name == segments[0])
                {
                    current = t;
                    break;
                }
            }
            if (current == null) return null;

            for (int i = 1; i < segments.Length; i++)
            {
                Transform child = current.Find(segments[i]);
                if (child == null) return null;
                current = child;
            }
            return current.gameObject;
        }
    }
}
#endif
