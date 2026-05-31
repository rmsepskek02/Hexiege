// ====================================================================================
// FixProductionPopupGroupB.cs
// ------------------------------------------------------------------------------------
// [목적]
//   ProductionPopup > ProductionPanel > ProgressBar 와 그 자식 Fill 이미지의
//   고정 픽셀값(sizeDelta)을 앵커 비율로 환산한다.
//
// [GameSystemRules 근거]
//   규칙 2 — 고정 픽셀 크기 대신 앵커 비율 기반 배치.
//   규칙 3 — Filled/Simple 이미지(ProgressBar) 안의 자식 이미지(Fill)는
//            부모와 같은 비율로 앵커를 설정해야 한다.
//
// [핵심 원칙] 현재 시각적 위치/크기를 완전히 유지하면서 픽셀 오프셋만 제거한다.
//
// [역산 근거]
//   ProgressBar (부모 ProductionPanel 높이 = 918px 기준):
//     시각 bottom = 70.06px → 70.06/918 = 0.076 → anchorMin.y
//     시각 top    = 243.5px → 243.5/918  = 0.265 → anchorMax.y
//   Fill (부모 ProgressBar 크기 = 1080 × 173.44px 기준):
//     X: left=150px→150/1080=0.139, right=930px→930/1080=0.861
//     Y: bottom=70px→70/173.44=0.404, top=103.44px→103.44/173.44=0.597
//
// [사용 방법]
//   1) Unity Editor에서 Game.unity 씬을 연다. (반드시 열려 있어야 함)
//   2) 상단 메뉴 Hexiege > Fix > ProductionPopup > GroupB 클릭
//   3) 실행 후 Ctrl+Z로 되돌리기 가능
//
// [주의] 이 스크립트는 1회성이므로 실행 후 직접 삭제해도 무방하다.
// ====================================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// ProgressBar와 Fill의 고정 픽셀값을 앵커 비율로 환산하는 1회성 에디터 스크립트.
    /// </summary>
    public static class FixProductionPopupGroupB
    {
        private const string ProgressBarPath = "ProductionPopup/ProductionPanel/ProgressBar";
        private const string FillPath = "ProductionPopup/ProductionPanel/ProgressBar/Fill";

        [MenuItem("Hexiege/Fix/ProductionPopup/GroupB")]
        public static void Fix()
        {
            RectTransform progressBar = FindRectTransform(ProgressBarPath);
            RectTransform fill = FindRectTransform(FillPath);

            if (progressBar == null)
            {
                Debug.LogError($"[GroupB] 오브젝트를 찾을 수 없습니다: {ProgressBarPath}\n" +
                               "Game.unity 씬이 열려 있는지 확인하세요.");
                return;
            }
            if (fill == null)
            {
                Debug.LogError($"[GroupB] 오브젝트를 찾을 수 없습니다: {FillPath}\n" +
                               "Game.unity 씬이 열려 있는지 확인하세요.");
                return;
            }

            // ProgressBar 변경 ----------------------------------------------------------
            Undo.RecordObject(progressBar, "Fix ProgressBar Anchor (GroupB)");

            progressBar.anchorMin = new Vector2(0f, 0.076f);
            progressBar.anchorMax = new Vector2(1f, 0.265f);
            progressBar.pivot = new Vector2(0.5f, 0.5f); // pivot 유지
            progressBar.anchoredPosition = Vector2.zero;
            progressBar.sizeDelta = Vector2.zero;

            EditorUtility.SetDirty(progressBar);

            // Fill 변경 (규칙 3: 부모와 같은 비율로 앵커 설정) ---------------------------
            Undo.RecordObject(fill, "Fix ProgressBar Fill Anchor (GroupB)");

            fill.anchorMin = new Vector2(0.139f, 0.404f);
            fill.anchorMax = new Vector2(0.861f, 0.597f);
            fill.pivot = new Vector2(0.5f, 0.5f); // pivot 유지
            fill.anchoredPosition = Vector2.zero;
            fill.sizeDelta = Vector2.zero;

            EditorUtility.SetDirty(fill);

            Debug.Log("[GroupB] 완료: ProgressBar와 Fill을 앵커 비율로 환산했습니다. Ctrl+S로 씬을 저장하세요.");
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
