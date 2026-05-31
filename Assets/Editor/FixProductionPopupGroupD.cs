// ====================================================================================
// FixProductionPopupGroupD.cs
// ------------------------------------------------------------------------------------
// [목적]
//   ProductionPopup > ProductionPanel > InfoBar 영역(골드/인구 아이콘·텍스트)의
//   고정 픽셀값을 앵커 비율 직접 배치로 환산한다. 대상:
//     - InfoBar (컨테이너)
//     - InfoBar의 HorizontalLayoutGroup (제거 — 앵커 배치로 불필요)
//     - InfoBar > GoldIcon (아이콘, 1:1 비율 자동)
//     - InfoBar > GoldText (텍스트, 왼쪽 절반)
//     - InfoBar > PopIcon  (아이콘, 1:1 비율 자동)
//     - InfoBar > PopText  (텍스트, 오른쪽 절반)
//
// [GameSystemRules 근거]
//   규칙 2 — 고정 픽셀 크기 대신 앵커 비율 기반으로 배치한다.
//
// [핵심 원칙] 현재 시각적 위치/크기를 완전히 유지하면서 픽셀 오프셋을 제거한다.
//
// [역산 근거]
//   InfoBar (부모 ProductionPanel=918px, 시각중심=58.31px, 목표높이=100px):
//     bottom=8.31px → 8.31/918=0.009
//     top=108.31px  → 108.31/918=0.118
//
//   자식 배치 (InfoBar: 864×100px 기준, 아이콘=100px 폭):
//     아이콘 anchorMax.x = 100/864 = 0.116
//     좌우 절반 기준 0.5
//     GoldIcon : (0,     0)~(0.116, 1) → 100×100px (1:1 자동)
//     GoldText : (0.116, 0)~(0.5,   1) → 왼쪽 텍스트 영역
//     PopIcon  : (0.5,   0)~(0.616, 1) → 100×100px (1:1 자동)
//     PopText  : (0.616, 0)~(1.0,   1) → 오른쪽 텍스트 영역
//
// [사용 방법]
//   1) Unity Editor에서 Game.unity 씬을 연다. (반드시 열려 있어야 함)
//   2) 상단 메뉴 Hexiege > Fix > ProductionPopup > GroupD 클릭
//   3) 실행 후 Ctrl+Z로 되돌리기 가능
//
// [주의] 이 스크립트는 1회성이므로 실행 후 직접 삭제해도 무방하다.
// ====================================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// InfoBar 영역의 고정 픽셀값을 앵커 비율 직접 배치로 환산하는 1회성 에디터 스크립트.
    /// HLG를 제거하고 각 자식을 InfoBar 기준 앵커로 직접 배치한다.
    /// </summary>
    public static class FixProductionPopupGroupD
    {
        private const string InfoBarPath = "ProductionPopup/ProductionPanel/InfoBar";

        // 아이콘 anchorMax.x = 100px / 864px(InfoBar 가로) = 0.1157 ≈ 0.116
        private const float IconWidthRatio = 0.116f;

        [MenuItem("Hexiege/Fix/ProductionPopup/GroupD")]
        public static void Fix()
        {
            // ── 1) InfoBar 탐색 ────────────────────────────────────────────────────────
            RectTransform infoBarRt = FindRectTransform(InfoBarPath);
            if (infoBarRt == null)
            {
                Debug.LogError($"[GroupD] 오브젝트를 찾을 수 없습니다: {InfoBarPath}\n" +
                               "Game.unity 씬이 열려 있는지 확인하세요.");
                return;
            }
            Transform infoBar = infoBarRt.transform;

            // ── 2) InfoBar 컨테이너 앵커 환산 ─────────────────────────────────────────
            Undo.RecordObject(infoBarRt, "Fix InfoBar Anchor (GroupD)");
            infoBarRt.anchorMin        = new Vector2(0.1f, 0.009f);
            infoBarRt.anchorMax        = new Vector2(0.9f, 0.118f);
            infoBarRt.anchoredPosition = Vector2.zero;
            infoBarRt.sizeDelta        = Vector2.zero;
            EditorUtility.SetDirty(infoBarRt);

            // ── 3) InfoBar의 HorizontalLayoutGroup 제거 ───────────────────────────────
            //    자식을 앵커로 직접 배치하므로 레이아웃 그룹이 불필요하다.
            var hlg = infoBar.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                Undo.DestroyObjectImmediate(hlg);
            }

            // ── 4) 자식 오브젝트 탐색 ─────────────────────────────────────────────────
            Transform goldIcon = infoBar.Find("GoldIcon");
            Transform goldText = infoBar.Find("GoldText");
            Transform popIcon  = infoBar.Find("PopIcon");
            Transform popText  = infoBar.Find("PopText");

            if (goldIcon == null || goldText == null || popIcon == null || popText == null)
            {
                Debug.LogError("[GroupD] InfoBar 자식 오브젝트(GoldIcon/GoldText/PopIcon/PopText) 중 " +
                               "하나 이상을 찾을 수 없습니다. 오브젝트 이름을 확인하세요.");
                return;
            }

            // ── 5) 각 자식 앵커 배치 ──────────────────────────────────────────────────
            //    앵커 비율은 반드시 [anchorMin/Max → anchoredPosition → sizeDelta] 순서로 설정한다.
            //    LayoutElement가 있으면 제거한다 (앵커 배치로 불필요).

            // GoldIcon: 왼쪽 아이콘 (100×100px → 1:1 자동)
            SetChildAnchor(goldIcon, 0f,            0f, IconWidthRatio, 1f, "GoldIcon");
            RemoveLayoutElement(goldIcon.gameObject);

            // GoldText: 왼쪽 텍스트 영역
            SetChildAnchor(goldText, IconWidthRatio, 0f, 0.5f,          1f, "GoldText");
            RemoveLayoutElement(goldText.gameObject);

            // PopIcon: 오른쪽 아이콘 (100×100px → 1:1 자동)
            SetChildAnchor(popIcon,  0.5f,           0f, 0.5f + IconWidthRatio, 1f, "PopIcon");
            RemoveLayoutElement(popIcon.gameObject);

            // PopText: 오른쪽 텍스트 영역
            SetChildAnchor(popText,  0.5f + IconWidthRatio, 0f, 1f, 1f, "PopText");
            RemoveLayoutElement(popText.gameObject);

            Debug.Log("[GroupD] 완료: InfoBar 자식을 앵커 비율로 직접 배치했습니다. " +
                      "Ctrl+S로 씬을 저장하세요.");
        }

        /// <summary>
        /// RectTransform을 지정한 앵커 비율로 설정한다.
        /// 반드시 [앵커 → anchoredPosition → sizeDelta] 순서로 설정해야 올바르게 적용된다.
        /// </summary>
        private static void SetChildAnchor(Transform tr,
                                           float minX, float minY,
                                           float maxX, float maxY,
                                           string label)
        {
            var rt = tr.GetComponent<RectTransform>();
            Undo.RecordObject(rt, $"Fix {label} Anchor (GroupD)");
            rt.anchorMin        = new Vector2(minX, minY);
            rt.anchorMax        = new Vector2(maxX, maxY);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.zero;
            EditorUtility.SetDirty(rt);
        }

        /// <summary>
        /// LayoutElement 컴포넌트가 있으면 제거한다.
        /// 앵커 직접 배치로 전환했으므로 Layout 제약이 불필요하다.
        /// </summary>
        private static void RemoveLayoutElement(GameObject go)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le != null)
                Undo.DestroyObjectImmediate(le);
        }

        /// <summary>
        /// 씬 전체(계층 깊이 무관, 비활성 포함)에서 경로에 해당하는 RectTransform을 탐색한다.
        /// </summary>
        private static RectTransform FindRectTransform(string hierarchyPath)
        {
            GameObject go = FindByHierarchyPath(hierarchyPath);
            return go != null ? go.GetComponent<RectTransform>() : null;
        }

        /// <summary>
        /// ProductionPopup은 [UI]/SafeAreaContainer 아래에 있어 루트 탐색으로는 찾을 수 없으므로
        /// Resources.FindObjectsOfTypeAll로 씬 전체를 검색한다 (비활성 포함).
        /// </summary>
        private static GameObject FindByHierarchyPath(string hierarchyPath)
        {
            string[] segments = hierarchyPath.Split('/');
            if (segments.Length == 0) return null;

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
