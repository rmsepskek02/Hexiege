using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GoldIcon / GoldText 의 AnchoredPosition 오프셋을
/// 순수 AnchorMin/Max 값으로 변환한다.
/// AnchoredPosition=(0,0), SizeDelta=(0,0) 으로 정리.
/// </summary>
public static class ConvertGoldAnchors
{
    [MenuItem("Hexiege/UI/Convert GoldIcon GoldText Anchors")]
    public static void Convert()
    {
        // 캔버스 강제 업데이트 → 실제 Rect 크기 확보
        Canvas.ForceUpdateCanvases();

        int convertedCount = 0;

        // Production 슬롯: VerticalButtons → UnitButtons1/2 → Slot1/2/3
        var verticalButtons = FindInactive("VerticalButtons");
        if (verticalButtons != null)
            convertedCount += ProcessRows(verticalButtons.transform);

        // Building 슬롯: BuildingButtons → BuildingButtons1/2/3 → 버튼들
        var buildingButtons = FindInactive("BuildingButtons");
        if (buildingButtons != null)
            convertedCount += ProcessRows(buildingButtons.transform);

        if (convertedCount == 0)
            Debug.LogWarning("[ConvertGoldAnchors] 변환된 오브젝트 없음. VerticalButtons / BuildingButtons 를 확인하세요.");
        else
            Debug.Log($"[ConvertGoldAnchors] {convertedCount}개 변환 완료. 씬 저장 (Ctrl+S) 하세요.");
    }

    private static int ProcessRows(Transform parent)
    {
        int count = 0;
        foreach (Transform row in parent)
        {
            foreach (Transform slot in row)
                count += ProcessSlot(slot);
        }
        return count;
    }

    private static int ProcessSlot(Transform slot)
    {
        int count = 0;
        var slotRect = slot.GetComponent<RectTransform>();
        if (slotRect == null) return 0;

        // 슬롯의 실제 크기 (Canvas.ForceUpdateCanvases 이후 유효)
        float parentW = slotRect.rect.width;
        float parentH = slotRect.rect.height;

        if (parentW <= 0 || parentH <= 0)
        {
            Debug.LogWarning($"[ConvertGoldAnchors] 슬롯 '{slot.name}' 크기 미확인 (w={parentW}, h={parentH}). 건너뜀.");
            return 0;
        }

        foreach (Transform child in slot)
        {
            string lower = child.name.ToLower();
            bool isTarget = lower.Contains("goldicon") || lower.Contains("coinicon")
                         || lower.Contains("goldtext") || lower.Contains("cointext")
                         || lower.Contains("unitimage") || lower.Contains("buildingimage");
            if (!isTarget) continue;

            var rect = child.GetComponent<RectTransform>();
            if (rect == null) continue;

            // 현재 값 읽기
            Vector2 anchorMin = rect.anchorMin;
            Vector2 anchorMax = rect.anchorMax;
            Vector2 anchoredPos = rect.anchoredPosition;
            Vector2 sizeDelta = rect.sizeDelta;
            Vector2 pivot = rect.pivot;

            // 현재 렌더링 크기 계산
            float rw = (anchorMax.x - anchorMin.x) * parentW + sizeDelta.x;
            float rh = (anchorMax.y - anchorMin.y) * parentH + sizeDelta.y;

            // 슬롯 로컬 좌표에서 rect 의 bottom-left 절대 위치
            // AnchoredPosition 기준점 = anchorMin + (anchorMax-anchorMin)*pivot (정규화 후 * parentSize)
            float refX = (anchorMin.x + (anchorMax.x - anchorMin.x) * pivot.x) * parentW;
            float refY = (anchorMin.y + (anchorMax.y - anchorMin.y) * pivot.y) * parentH;

            float left   = refX + anchoredPos.x - pivot.x * rw;
            float bottom = refY + anchoredPos.y - pivot.y * rh;
            float right  = left + rw;
            float top    = bottom + rh;

            // 새 앵커 (SizeDelta=0 기준이므로 rect 크기 = anchorSpan * parentSize)
            float newMinX = Round2(left   / parentW);
            float newMinY = Round2(bottom / parentH);
            float newMaxX = Round2(right  / parentW);
            float newMaxY = Round2(top    / parentH);

            Undo.RecordObject(rect, $"Convert Anchor {child.name}");
            rect.anchorMin       = new Vector2(newMinX, newMinY);
            rect.anchorMax       = new Vector2(newMaxX, newMaxY);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta        = Vector2.zero;
            EditorUtility.SetDirty(rect);

            Debug.Log($"[ConvertGoldAnchors] {slot.name}/{child.name}: " +
                      $"AnchorMin=({newMinX:F3},{newMinY:F3}) " +
                      $"AnchorMax=({newMaxX:F3},{newMaxY:F3}) " +
                      $"(parentSize={parentW:F1}x{parentH:F1}, rectSize={rw:F1}x{rh:F1})");
            count++;
        }
        return count;
    }

    private static float Round2(float v) => Mathf.Round(v * 100f) / 100f;

    private static GameObject FindInactive(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) return go;

        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.name == name && t.hideFlags == HideFlags.None)
                return t.gameObject;
        }
        Debug.LogWarning($"[ConvertGoldAnchors] '{name}' 오브젝트를 찾을 수 없음.");
        return null;
    }
}
