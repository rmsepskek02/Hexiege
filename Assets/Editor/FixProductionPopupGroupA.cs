// ====================================================================================
// FixProductionPopupGroupA.cs
// ------------------------------------------------------------------------------------
// [목적]
//   Game.unity 씬의 ProductionPopup > ProductionPanel 오브젝트가 가진 고정 픽셀값
//   (sizeDelta=(0,150))을 GameSystemRules.md 규칙 2(앵커 비율 기반 배치)에 맞게
//   순수 앵커 비율로 환산한다.
//
// [GameSystemRules 근거]
//   규칙 2 — 모든 UI 요소는 고정 픽셀 크기 대신 앵커 비율 기반으로 배치한다.
//            sizeDelta, offsetMin, offsetMax 등 고정 픽셀값 사용 금지.
//
// [핵심 원칙]
//   현재 시각적 위치/크기를 완전히 유지하면서 픽셀 오프셋만 제거한다.
//
// [역산 근거]
//   기존: anchorMax.y=0.4 + sizeDelta.y=150 → 0.4×1920 + 150 = 918px
//         918 / 1920 = 0.478  → 새 anchorMax.y
//
// [사용 방법]
//   1) Unity Editor에서 Game.unity 씬을 연다. (반드시 열려 있어야 함)
//   2) 상단 메뉴 Hexiege > Fix > ProductionPopup > GroupA 클릭
//   3) 실행 후 Ctrl+Z로 되돌리기 가능 (Undo.RecordObject 사용)
//
// [주의] 이 스크립트는 1회성이므로 실행 후 직접 삭제해도 무방하다.
// ====================================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// ProductionPanel의 고정 픽셀 sizeDelta를 앵커 비율로 환산하는 1회성 에디터 스크립트.
    /// </summary>
    public static class FixProductionPopupGroupA
    {
        // 씬 계층 경로 — GameObject.Find 대신 정확한 경로로 탐색한다.
        private const string ProductionPanelPath = "ProductionPopup/ProductionPanel";

        [MenuItem("Hexiege/Fix/ProductionPopup/GroupA")]
        public static void Fix()
        {
            // 1) 대상 오브젝트 탐색 — 실패 시 오류 출력 후 중단
            RectTransform panel = FindRectTransform(ProductionPanelPath);
            if (panel == null)
            {
                Debug.LogError($"[GroupA] 오브젝트를 찾을 수 없습니다: {ProductionPanelPath}\n" +
                               "Game.unity 씬이 열려 있는지 확인하세요.");
                return;
            }

            // 2) Ctrl+Z 지원을 위해 변경 전 상태를 기록한다.
            Undo.RecordObject(panel, "Fix ProductionPanel Anchor (GroupA)");

            // 3) RectTransform 수정은 반드시 [앵커 → anchoredPosition → sizeDelta] 순서로 한다.
            //    앵커를 먼저 설정해야 anchoredPosition/sizeDelta가 올바른 기준으로 계산된다.
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(1f, 0.478f); // 0.4 + 150/1920 = 0.478
            panel.pivot = new Vector2(0.5f, 0f);       // pivot 유지
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = Vector2.zero;            // 고정 픽셀값 제거

            // 4) 변경 사항을 씬에 반영(dirty 표시) — 저장 대상으로 등록
            EditorUtility.SetDirty(panel);

            Debug.Log("[GroupA] 완료: ProductionPanel을 앵커 비율(0,0)~(1,0.478)로 환산했습니다. " +
                      "Ctrl+S로 씬을 저장하세요.");
        }

        /// <summary>
        /// 씬 루트부터 계층 경로를 따라 내려가며 RectTransform을 탐색한다.
        /// </summary>
        /// <param name="hierarchyPath">슬래시로 구분된 계층 경로 (예: "ProductionPopup/ProductionPanel")</param>
        /// <returns>찾은 RectTransform, 없으면 null</returns>
        private static RectTransform FindRectTransform(string hierarchyPath)
        {
            GameObject go = FindByHierarchyPath(hierarchyPath);
            return go != null ? go.GetComponent<RectTransform>() : null;
        }

        /// <summary>
        /// 씬 전체(계층 깊이 무관, 비활성 포함)에서 이름이 일치하는 오브젝트를 찾은 뒤
        /// 나머지 경로를 자식으로 따라 내려간다.
        /// ProductionPopup은 [UI]/SafeAreaContainer 아래에 있어 루트 탐색으로는 찾을 수 없으므로
        /// Resources.FindObjectsOfTypeAll로 씬 전체를 검색한다.
        /// </summary>
        private static GameObject FindByHierarchyPath(string hierarchyPath)
        {
            string[] segments = hierarchyPath.Split('/');
            if (segments.Length == 0) return null;

            // 1) 씬 전체에서 첫 번째 세그먼트 이름과 일치하는 Transform을 탐색한다 (비활성 포함).
            //    GetRootGameObjects()는 최상위만 보므로 사용하지 않는다.
            Transform current = null;
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                // 로드된 씬의 오브젝트만 대상으로 한다 (에셋/프리팹 제외).
                if (t.gameObject.scene.isLoaded && t.name == segments[0])
                {
                    current = t;
                    break;
                }
            }
            if (current == null) return null;

            // 2) 나머지 경로를 자식으로 따라 내려간다.
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
