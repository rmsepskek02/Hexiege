// ============================================================================
// FixProductionPopup.cs — 1회성(2차) 에디터 스크립트
// ProductionPopup UI의 3개 영역(UnitsButtons / QueueSlots / InfoBar)을
// 1차 스크립트(RebuildProductionPopup) 실행 후 발견된 시각 문제에 맞춰 수정합니다.
// 실행: Unity 상단 메뉴 Hexiege > Setup > ProductionPopup 2차 수정
// 실행 후 이 파일은 삭제해도 무방합니다.
// ============================================================================
//
// [이 스크립트가 하는 일 — 유니티 초급자용 요약]
// 1차 스크립트로 HeaderText/CancelButton 앵커는 이미 잘 정리됐지만,
// 나머지 3개 영역에서 아래 시각 문제가 발견됐습니다.
//
//   1) UnitsButtons: 버튼이 너무 크게 늘어나 패널을 가림
//      → 1차에서 넣은 LayoutElement(flexibleHeight 3/2 비율)를 제거해
//        두 행이 1:1 균등 그리드가 되도록 되돌립니다.
//
//   2) QueueSlots: 슬롯이 높이를 잃어 너무 작게 표시됨
//      → HLG가 폭을 강제로 늘리지 못하게 끄고(childControlWidth=false,
//        childForceExpandWidth=false), 가운데 정렬한 뒤, 각 슬롯을
//        160x160 정사각형으로 직접 지정합니다.
//
//   3) InfoBar: 크기/위치가 어긋남
//      → 패널 프레임 하단에 작게 배치되도록 앵커를 다시 잡고,
//        아이콘 너비(preferredWidth 44)와 텍스트 비율(flexibleWidth 1)을
//        다시 설정합니다.
//
// 핵심 원리:
//  - 앵커(anchorMin/anchorMax)를 0~1 비율로 잡고 anchoredPosition·sizeDelta를
//    0으로 초기화하면 → 부모 영역의 "비율 구간"에 딱 맞게 늘어납니다.
//    (단, QueueSlots 슬롯처럼 고정 크기를 원할 때는 sizeDelta에 직접 픽셀값을 줍니다.)
//  - Layout Group의 childControl/childForceExpand 스위치로 자식 크기 제어 방식을 바꿉니다.
//
// 모든 변경은 Undo(Ctrl+Z)로 되돌릴 수 있도록 Undo에 등록합니다.
// 씬 저장은 하지 않습니다. 실행 후 직접 Ctrl+S로 저장하세요.
// ============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Hexiege.Presentation;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// ProductionPanel 하위 3개 영역(UnitsButtons / QueueSlots / InfoBar)을
    /// 2차 설계 방향에 맞춰 수정하는 1회성 에디터 도구. 메뉴에서 실행한다.
    /// </summary>
    public static class FixProductionPopup
    {
        // 메뉴 경로 — Unity 상단 바에 'Hexiege > Setup > ProductionPopup 2차 수정'으로 노출됨.
        private const string MenuPath = "Hexiege/Setup/ProductionPopup 2차 수정";

        // QueueSlots 각 슬롯의 정사각형 한 변 길이(px).
        // QueueSlots 영역 높이(약 253px)의 약 63% 수준으로, 실기 확인 후 조정 가능.
        private const float SlotSize = 160f;

        // Unity의 Horizontal/VerticalLayoutGroup.childAlignment에서 가운데(MiddleCenter)를
        // 가리키는 enum 값. TextAnchor.MiddleCenter == 4.
        private const TextAnchor MiddleCenter = TextAnchor.MiddleCenter;

        /// <summary>
        /// 메뉴 실행 진입점. 씬에서 ProductionPanel을 찾아 3개 영역을 차례로 수정한다.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void Fix()
        {
            // ── 1) ProductionPanelUI 컴포넌트를 통해 ProductionPanel을 안전하게 찾는다. ──
            //    이름으로 직접 찾으면(GameObject.Find) 다른 패널의 동명 오브젝트와 충돌할 수
            //    있으므로, 씬에 유일하게 존재하는 ProductionPanelUI 컴포넌트를 기준점으로 삼는다.
            //    (1차 스크립트와 동일한 탐색 방식)
            var panelUI = Object.FindFirstObjectByType<ProductionPanelUI>(FindObjectsInactive.Include);
            if (panelUI == null)
            {
                Debug.LogWarning("[FixProductionPopup] 씬에서 ProductionPanelUI를 찾을 수 없습니다. " +
                                 "Game.unity 씬이 열려 있는지 확인하세요.");
                return;
            }

            // ProductionPanelUI는 ProductionPopup(래퍼)에 붙어 있고,
            // 실제 수정 대상은 그 직계 자식인 ProductionPanel이다.
            Transform productionPanel = panelUI.transform.Find("ProductionPanel");
            if (productionPanel == null)
            {
                Debug.LogWarning("[FixProductionPopup] ProductionPopup 하위에서 'ProductionPanel'을 찾을 수 없습니다. " +
                                 "씬 계층 구조가 변경됐는지 확인하세요.");
                return;
            }
            Debug.Log($"[FixProductionPopup] 대상 패널: '{productionPanel.name}' — 2차 수정을 시작합니다.");

            // 변경 작업 카운터 — 마지막에 몇 건이 처리됐는지 로그로 남긴다.
            int changeCount = 0;

            // ── 2) 수정 1. UnitsButtons — 2행 3열 균등 그리드 복원 ──
            changeCount += FixUnitsButtons(productionPanel);

            // ── 3) 수정 2. QueueSlots — 정사각형 슬롯 + 가운데 정렬 ──
            changeCount += FixQueueSlots(productionPanel);

            // ── 4) 수정 3. InfoBar — 패널 하단 소형 중앙 배치 ──
            changeCount += FixInfoBar(productionPanel);

            // ── 5) 씬 Dirty 마킹 — 변경사항이 있음을 에디터에 알려 저장 표시(*)를 띄운다. ──
            //    (스크립트가 직접 씬을 저장하지는 않음. 사용자가 Ctrl+S로 저장해야 함.)
            EditorUtility.SetDirty(productionPanel.gameObject);
            EditorSceneManager.MarkSceneDirty(productionPanel.gameObject.scene);

            Debug.Log($"[FixProductionPopup] 완료 — 총 {changeCount}건 처리. " +
                      "Ctrl+S로 씬을 저장하세요. 잘못됐다면 Ctrl+Z로 되돌릴 수 있습니다.");
        }

        // ====================================================================
        // 수정 1. UnitsButtons — 2행 3열 균등 그리드
        // ====================================================================

        /// <summary>
        /// UnitsButtons(VLG) 영역을 BuildingPopup의 균등 그리드와 동일한 방식으로 되돌린다.
        ///
        /// 변경 내용:
        ///  - UnitsButtons VLG: childControl/ForceExpand 4개 모두 true 유지(이미 1차에서 설정됨).
        ///  - UnitButtons / Buttons 두 행(HLG): childControl/ForceExpand 4개 모두 true 유지.
        ///  - 두 행에 1차 스크립트가 추가한 LayoutElement(flexibleHeight 3/2 비율)를 제거.
        ///    → 비율 없이 부모 VLG가 두 행을 1:1로 균등 분배하게 되어 버튼이 과하게 커지지 않는다.
        /// </summary>
        private static int FixUnitsButtons(Transform productionPanel)
        {
            int changes = 0;

            var unitsButtons = FindChild(productionPanel, "UnitsButtons");
            if (unitsButtons == null)
            {
                WarnMissing("UnitsButtons");
                return 0;
            }

            // 1) UnitsButtons VLG — 자식(두 행)을 가로/세로로 완전 제어 + 강제 확장.
            //    이미 1차에서 켜져 있지만, 안전을 위해 다시 명시적으로 설정한다.
            var vlg = unitsButtons.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                Undo.RecordObject(vlg, "Fix UnitsButtons VLG");
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = true;
                EditorUtility.SetDirty(vlg);
                changes++;
            }
            else Debug.LogWarning("[FixProductionPopup] UnitsButtons에 VerticalLayoutGroup이 없습니다.");

            // 2) UnitButtons 행 — HLG 균등 분배 유지 + LayoutElement 제거.
            var unitButtonsRow = FindChild(unitsButtons, "UnitButtons");
            if (unitButtonsRow != null)
            {
                ResetRect(unitButtonsRow as RectTransform);
                SetupEqualRow(unitButtonsRow);
                RemoveLayoutElement(unitButtonsRow);
                changes++;
            }
            else WarnMissing("UnitsButtons/UnitButtons");

            // 3) Buttons 행(랠리/업그레이드/철거) — 동일하게 HLG 유지 + LayoutElement 제거.
            var actionButtonsRow = FindChild(unitsButtons, "Buttons");
            if (actionButtonsRow != null)
            {
                ResetRect(actionButtonsRow as RectTransform);
                SetupEqualRow(actionButtonsRow);
                RemoveLayoutElement(actionButtonsRow);
                changes++;
            }
            else WarnMissing("UnitsButtons/Buttons");

            return changes;
        }

        // ====================================================================
        // 수정 2. QueueSlots — 정사각형 슬롯, 가운데 배치
        // ====================================================================

        /// <summary>
        /// QueueSlots(HLG)의 슬롯들을 정사각형으로 만들고 영역 가운데에 균등 배치한다.
        ///
        /// 변경 내용:
        ///  - HLG: childControlWidth=false, childForceExpandWidth=false
        ///         → HLG가 슬롯 폭을 강제로 늘리지 않고, 슬롯이 지정한 sizeDelta(160)를 그대로 사용.
        ///  - childAlignment=MiddleCenter, spacing=20 → 슬롯들이 가운데로 모여 균등 간격으로 배치.
        ///  - 각 Slot: anchoredPosition=(0,0), sizeDelta=(160,160) 정사각형.
        ///
        /// 주의: childControlWidth를 끄면 HLG는 더 이상 자식의 RectTransform 크기를 덮어쓰지
        ///       않으므로, sizeDelta로 지정한 160x160이 그대로 화면에 반영된다.
        /// </summary>
        private static int FixQueueSlots(Transform productionPanel)
        {
            int changes = 0;

            var queueSlots = FindChild(productionPanel, "QueueSlots");
            if (queueSlots == null)
            {
                WarnMissing("QueueSlots");
                return 0;
            }

            // 1) HLG 설정 변경 — 폭 강제 제어/확장을 끄고 가운데 정렬.
            var hlg = queueSlots.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                Undo.RecordObject(hlg, "Fix QueueSlots HLG");
                hlg.childControlWidth = false;      // 슬롯 폭을 HLG가 제어하지 않음 → 슬롯 자체 크기 유지
                hlg.childForceExpandWidth = false;   // 남는 공간을 채우려 폭을 늘리지 않음
                hlg.childAlignment = MiddleCenter;    // 슬롯들을 영역 가운데로 모음
                hlg.spacing = 20f;                    // 슬롯 사이 간격
                EditorUtility.SetDirty(hlg);
                changes++;
            }
            else Debug.LogWarning("[FixProductionPopup] QueueSlots에 HorizontalLayoutGroup이 없습니다.");

            // 2) 각 Slot을 160x160 정사각형으로. (HLG가 폭을 제어하지 않으므로 이 크기가 그대로 적용됨)
            for (int i = 0; i < queueSlots.childCount; i++)
            {
                var slot = queueSlots.GetChild(i) as RectTransform;
                if (slot == null) continue;

                Undo.RecordObject(slot, "Fix QueueSlot Size");
                slot.anchoredPosition = Vector2.zero;
                slot.sizeDelta = new Vector2(SlotSize, SlotSize);
                EditorUtility.SetDirty(slot);
                changes++;
            }

            return changes;
        }

        // ====================================================================
        // 수정 3. InfoBar — 패널 하단 소형 중앙 배치
        // ====================================================================

        /// <summary>
        /// InfoBar를 패널 프레임 하단 경계 부근에 가로로 작게 배치한다.
        ///
        /// 변경 내용:
        ///  - RectTransform: anchorMin=(0.1, 0.0), anchorMax=(0.9, 0.09), pos/delta=0
        ///    → 가로 10~90% 구간, 세로 하단 0~9% 구간에 얇게 배치.
        ///  - HLG: childControlWidth=true, childForceExpandWidth=true 유지 + childAlignment=MiddleCenter, spacing=8.
        ///  - GoldIcon/PopIcon: preferredWidth=44, minWidth=44, flexibleWidth=0 (정사각형 아이콘 너비 고정).
        ///  - GoldText/PopText: flexibleWidth=1 유지 (남은 공간 균등 분배).
        ///
        /// 자식 순서는 [GoldIcon, GoldText, PopIcon, PopText] 이다.
        /// </summary>
        private static int FixInfoBar(Transform productionPanel)
        {
            int changes = 0;

            var infoBar = FindChild(productionPanel, "InfoBar");
            if (infoBar == null)
            {
                WarnMissing("InfoBar");
                return 0;
            }

            // 1) InfoBar RectTransform — 앵커를 패널 하단 중앙 소형으로 재설정.
            //    앵커 변경 후 pos/delta를 0으로 명시 초기화해야 기존 픽셀 오프셋이 남지 않는다.
            SetAnchors(infoBar as RectTransform,
                new Vector2(0.1f, 0.0f), new Vector2(0.9f, 0.09f));
            changes++;

            // 2) InfoBar HLG — 자식 폭 제어/확장 유지 + 가운데 정렬.
            var hlg = infoBar.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                Undo.RecordObject(hlg, "Fix InfoBar HLG");
                hlg.childControlWidth = true;
                hlg.childForceExpandWidth = true;
                hlg.childAlignment = MiddleCenter;
                hlg.spacing = 8f;
                EditorUtility.SetDirty(hlg);
                changes++;
            }
            else Debug.LogWarning("[FixProductionPopup] InfoBar에 HorizontalLayoutGroup이 없습니다.");

            // 3) 아이콘 2개: 너비 44px로 고정(preferredWidth=minWidth=44), 남는 공간 차지 안 함(flexibleWidth=0).
            changes += SetupInfoBarElement(infoBar, "GoldIcon", isIcon: true);
            changes += SetupInfoBarElement(infoBar, "PopIcon", isIcon: true);

            // 4) 텍스트 2개: 남은 가로 공간을 균등 분배(flexibleWidth=1).
            changes += SetupInfoBarElement(infoBar, "GoldText", isIcon: false);
            changes += SetupInfoBarElement(infoBar, "PopText", isIcon: false);

            return changes;
        }

        // ====================================================================
        // 헬퍼 메서드
        // ====================================================================

        /// <summary>
        /// 부모 Transform의 직계 자식 중 지정한 이름의 Transform을 반환한다.
        /// 못 찾으면 null을 반환한다 (호출부에서 null 체크 + 경고 로그 처리).
        /// "부모의 직계 자식"만 보므로 다른 패널의 동명 오브젝트와 충돌하지 않는다.
        /// </summary>
        private static Transform FindChild(Transform parent, string childName)
        {
            if (parent == null) return null;
            return parent.Find(childName);
        }

        /// <summary>
        /// RectTransform의 앵커를 지정 비율로 설정하고, anchoredPosition·sizeDelta를 0으로 초기화한다.
        /// 앵커만 바꾸고 pos/delta를 0으로 만들지 않으면 기존 픽셀 오프셋이 그대로 남아
        /// 위치가 어긋나므로, 두 값을 반드시 명시적으로 0으로 만든다.
        /// </summary>
        private static void SetAnchors(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (rt == null) return;
            Undo.RecordObject(rt, "Set Anchors");
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = Vector2.zero; // 픽셀 오프셋 제거 (필수)
            rt.sizeDelta = Vector2.zero;          // 추가 크기 제거 (필수)
            EditorUtility.SetDirty(rt);
        }

        /// <summary>
        /// RectTransform의 anchoredPosition·sizeDelta만 0으로 초기화한다.
        /// (앵커 비율은 건드리지 않음 — 부모 Layout Group이 크기를 제어할 자식에 사용.)
        /// </summary>
        private static void ResetRect(RectTransform rt)
        {
            if (rt == null) return;
            Undo.RecordObject(rt, "Reset Rect");
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            EditorUtility.SetDirty(rt);
        }

        /// <summary>
        /// 가로 버튼 행(HLG)을 균등 분배 모드로 설정한다.
        ///  - childControlWidth/Height + childForceExpandWidth/Height = true
        ///    → 부모(VLG)가 행 높이를 나눠주면, 행은 자식 버튼들을 가로로 균등 분배한다.
        /// (1차의 SetupHorizontalRow와 달리, 여기서는 LayoutElement를 추가하지 않는다.
        ///  비율 없는 1:1 균등 그리드를 만들기 위해 LayoutElement는 RemoveLayoutElement로 제거한다.)
        /// </summary>
        private static void SetupEqualRow(Transform row)
        {
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                Undo.RecordObject(hlg, "Set Equal Row HLG");
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;
                EditorUtility.SetDirty(hlg);
            }
            else
            {
                Debug.LogWarning($"[FixProductionPopup] '{row.name}'에 HorizontalLayoutGroup이 없습니다.");
            }
        }

        /// <summary>
        /// 행에 부착된 LayoutElement를 제거한다(1차 스크립트가 추가한 flexibleHeight 비율 컴포넌트).
        /// LayoutElement가 없으면 아무 것도 하지 않는다.
        /// Undo.DestroyObjectImmediate로 삭제해 Ctrl+Z로 되돌릴 수 있게 한다.
        /// </summary>
        private static void RemoveLayoutElement(Transform row)
        {
            if (row == null) return;
            var le = row.GetComponent<LayoutElement>();
            if (le != null)
            {
                Undo.DestroyObjectImmediate(le);
            }
        }

        /// <summary>
        /// InfoBar 자식(아이콘/텍스트)을 반응형으로 설정한다.
        ///  - RectTransform: anchoredPosition·sizeDelta = 0
        ///  - 아이콘이면 LayoutElement.preferredWidth=44, minWidth=44, flexibleWidth=0 (너비 고정)
        ///  - 텍스트면 LayoutElement.flexibleWidth=1 (남은 공간 균등 분배)
        /// 처리 성공 시 1, 자식을 못 찾으면 0을 반환한다.
        /// </summary>
        private static int SetupInfoBarElement(Transform infoBar, string childName, bool isIcon)
        {
            var child = FindChild(infoBar, childName);
            if (child == null)
            {
                WarnMissing($"InfoBar/{childName}");
                return 0;
            }

            // RectTransform 오프셋 초기화 (HLG가 위치/크기를 제어).
            ResetRect(child as RectTransform);

            // LayoutElement 설정 — 없으면 추가.
            var le = child.GetComponent<LayoutElement>();
            if (le == null) le = Undo.AddComponent<LayoutElement>(child.gameObject);
            Undo.RecordObject(le, "Set InfoBar LayoutElement");

            if (isIcon)
            {
                // 아이콘: 너비를 44로 고정하고, 남는 공간은 차지하지 않음.
                le.minWidth = 44f;
                le.preferredWidth = 44f;
                le.flexibleWidth = 0f;
            }
            else
            {
                // 텍스트: 남은 가로 공간을 균등하게 채움.
                le.minWidth = -1f;       // 최소 너비 강제 없음
                le.preferredWidth = -1f; // 선호 너비 강제 없음
                le.flexibleWidth = 1f;
            }
            EditorUtility.SetDirty(le);
            return 1;
        }

        /// <summary>
        /// 대상 오브젝트를 찾지 못했을 때 공통 경고 로그를 출력한다.
        /// 작업은 중단하지 않고 나머지 요소 처리를 계속 진행한다 (부분 적용 허용).
        /// </summary>
        private static void WarnMissing(string path)
        {
            Debug.LogWarning($"[FixProductionPopup] '{path}' 오브젝트를 찾을 수 없어 건너뜁니다. " +
                             "씬 계층 구조가 변경됐는지 확인하세요.");
        }
    }
}
#endif
