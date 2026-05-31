// ====================================================================================
// FixProductionPopupGroupC.cs
// ------------------------------------------------------------------------------------
// [목적]
//   ProductionPopup > ProductionPanel > QueueSlots 영역의 고정 픽셀 배치를
//   앵커 비율로 환산한다. 대상:
//     - QueueSlots (컨테이너)
//     - QueueSlots의 HorizontalLayoutGroup (spacing=100 고정 픽셀 → 제거)
//     - QueueSlots > Slot1 / Slot2 / Slot3 (각 슬롯 위치)
//     - 각 Slot > SlotImage (슬롯 내부 이미지)
//
// [GameSystemRules 근거]
//   규칙 2 — 고정 픽셀 크기/간격 대신 앵커 비율 기반 배치.
//            (HorizontalLayoutGroup의 spacing=100은 고정 픽셀이므로 제거하고
//             각 슬롯을 앵커로 직접 배치한다.)
//
// [핵심 원칙] 현재 시각적 위치/크기를 완전히 유지하면서 픽셀 오프셋을 제거한다.
//
// [역산 근거]
//   QueueSlots (부모 ProductionPanel=918px):
//     bottom=158.5px→0.173, top=378.82px→0.413
//   슬롯 배치 (QueueSlots=1080×220.32px):
//     3슬롯(160px)+2간격(100px)=680px, 좌우여백 200px씩
//     세로: (220.32-160)/2=30.16px → min.y=0.137, max.y=0.863
//     Slot1: left=200/1080=0.185, right=360/1080=0.333
//     Slot2: left=460/1080=0.426, right=620/1080=0.574
//     Slot3: left=720/1080=0.667, right=880/1080=0.815
//   SlotImage (Slot=160×160px, SlotImage=150×150px, 여백 5px):
//     5/160=0.031, 155/160=0.969
//
// [사용 방법]
//   1) Unity Editor에서 Game.unity 씬을 연다. (반드시 열려 있어야 함)
//   2) 상단 메뉴 Hexiege > Fix > ProductionPopup > GroupC 클릭
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
    /// QueueSlots 영역의 고정 픽셀 배치를 앵커 비율로 환산하는 1회성 에디터 스크립트.
    /// </summary>
    public static class FixProductionPopupGroupC
    {
        private const string QueueSlotsPath = "ProductionPopup/ProductionPanel/QueueSlots";

        // 슬롯별 앵커 X 범위 (좌, 우). Y는 세 슬롯 공통으로 0.137 ~ 0.863.
        private static readonly Vector2[] SlotAnchorX =
        {
            new Vector2(0.185f, 0.333f), // Slot1
            new Vector2(0.426f, 0.574f), // Slot2
            new Vector2(0.667f, 0.815f), // Slot3
        };
        private const float SlotMinY = 0.137f;
        private const float SlotMaxY = 0.863f;

        // SlotImage 내부 여백 비율
        private const float SlotImageMin = 0.031f;
        private const float SlotImageMax = 0.969f;

        [MenuItem("Hexiege/Fix/ProductionPopup/GroupC")]
        public static void Fix()
        {
            RectTransform queueSlots = FindRectTransform(QueueSlotsPath);
            if (queueSlots == null)
            {
                Debug.LogError($"[GroupC] 오브젝트를 찾을 수 없습니다: {QueueSlotsPath}\n" +
                               "Game.unity 씬이 열려 있는지 확인하세요.");
                return;
            }

            // 1) QueueSlots 컨테이너 앵커 환산 ------------------------------------------
            Undo.RecordObject(queueSlots, "Fix QueueSlots Anchor (GroupC)");
            queueSlots.anchorMin = new Vector2(0f, 0.173f);
            queueSlots.anchorMax = new Vector2(1f, 0.413f);
            queueSlots.anchoredPosition = Vector2.zero;
            queueSlots.sizeDelta = Vector2.zero;
            EditorUtility.SetDirty(queueSlots);

            // 2) HorizontalLayoutGroup 제거 (spacing=100 고정 픽셀 제거 목적) -----------
            //    슬롯을 앵커로 직접 배치할 것이므로 Layout Group이 더 이상 필요 없다.
            var hlg = queueSlots.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                Undo.DestroyObjectImmediate(hlg);
                Debug.Log("[GroupC] QueueSlots의 HorizontalLayoutGroup을 제거했습니다.");
            }
            else
            {
                Debug.Log("[GroupC] QueueSlots에 HorizontalLayoutGroup이 없어 제거를 건너뜁니다.");
            }

            // 3) Slot1 ~ Slot3 + 각 SlotImage 앵커 환산 ---------------------------------
            for (int i = 0; i < 3; i++)
            {
                string slotName = $"Slot{i + 1}";
                Transform slotTr = queueSlots.Find(slotName);
                if (slotTr == null)
                {
                    Debug.LogError($"[GroupC] 슬롯을 찾을 수 없습니다: {QueueSlotsPath}/{slotName}");
                    return;
                }

                // 슬롯 본체 앵커
                var slotRt = slotTr.GetComponent<RectTransform>();
                Undo.RecordObject(slotRt, $"Fix {slotName} Anchor (GroupC)");
                slotRt.anchorMin = new Vector2(SlotAnchorX[i].x, SlotMinY);
                slotRt.anchorMax = new Vector2(SlotAnchorX[i].y, SlotMaxY);
                slotRt.anchoredPosition = Vector2.zero;
                slotRt.sizeDelta = Vector2.zero;
                EditorUtility.SetDirty(slotRt);

                // SlotImage 앵커 (부모 슬롯 기준 5px 여백 비율)
                Transform imageTr = slotTr.Find("SlotImage");
                if (imageTr == null)
                {
                    Debug.LogError($"[GroupC] SlotImage를 찾을 수 없습니다: {QueueSlotsPath}/{slotName}/SlotImage");
                    return;
                }
                var imageRt = imageTr.GetComponent<RectTransform>();
                Undo.RecordObject(imageRt, $"Fix {slotName}/SlotImage Anchor (GroupC)");
                imageRt.anchorMin = new Vector2(SlotImageMin, SlotImageMin);
                imageRt.anchorMax = new Vector2(SlotImageMax, SlotImageMax);
                imageRt.anchoredPosition = Vector2.zero;
                imageRt.sizeDelta = Vector2.zero;
                EditorUtility.SetDirty(imageRt);
            }

            Debug.Log("[GroupC] 완료: QueueSlots, Slot1~3, SlotImage를 앵커 비율로 환산했습니다. Ctrl+S로 씬을 저장하세요.");
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
