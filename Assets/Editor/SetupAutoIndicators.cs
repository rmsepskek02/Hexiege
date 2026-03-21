// ============================================================================
// SetupAutoIndicators.cs
// 씬에서 ProductionPanelUI를 찾아 각 유닛 버튼 하위에 AutoIndicator GameObject를
// 생성하고 SerializedField에 자동 연결하는 에디터 유틸리티.
//
// 메뉴: Hexiege > Setup Auto Indicators
//
// 동작 순서:
//   1. 씬에서 ProductionPanelUI 컴포넌트를 가진 오브젝트 탐색
//   2. _pistoleerButton, _assaultButton, _sniperButton 하위에 "AutoIndicator" 생성 (없는 경우에만)
//   3. ProductionPanelUI의 _pistoleerAutoIndicator, _assaultAutoIndicator, _sniperAutoIndicator에 연결
//   4. 완료 로그 출력
//
// 1회성 유틸리티 — 실행 후 Inspector에서 위치/크기/비주얼 직접 조정 필요.
// Editor 레이어 — 런타임에 포함되지 않음.
// ============================================================================

using UnityEngine;
using UnityEditor;
using Hexiege.Presentation;

namespace Hexiege.Editor
{
    public static class SetupAutoIndicators
    {
        /// <summary>
        /// 씬의 ProductionPanelUI에 자동 생산 인디케이터 오브젝트를 생성하고 연결.
        /// 이미 "AutoIndicator" 이름의 자식이 있으면 새로 생성하지 않고 기존 것을 연결.
        /// </summary>
        [MenuItem("Hexiege/Setup Auto Indicators")]
        public static void Execute()
        {
            // 1. 씬에서 ProductionPanelUI 찾기
            ProductionPanelUI panelUI = Object.FindFirstObjectByType<ProductionPanelUI>();
            if (panelUI == null)
            {
                Debug.LogError("[SetupAutoIndicators] 씬에서 ProductionPanelUI를 찾을 수 없습니다.");
                return;
            }

            // 2. SerializedObject로 private SerializeField에 접근
            SerializedObject so = new SerializedObject(panelUI);

            // 버튼 필드에서 Transform 가져오기
            SerializedProperty pistoleerBtnProp = so.FindProperty("_pistoleerButton");
            SerializedProperty assaultBtnProp   = so.FindProperty("_assaultButton");
            SerializedProperty sniperBtnProp    = so.FindProperty("_sniperButton");

            if (pistoleerBtnProp.objectReferenceValue == null ||
                assaultBtnProp.objectReferenceValue == null ||
                sniperBtnProp.objectReferenceValue == null)
            {
                Debug.LogError("[SetupAutoIndicators] _pistoleerButton, _assaultButton, _sniperButton 중 하나 이상이 Inspector에 연결되지 않았습니다.");
                return;
            }

            // 각 버튼의 Transform 가져오기
            Transform pistoleerBtnTransform = ((UnityEngine.UI.Button)pistoleerBtnProp.objectReferenceValue).transform;
            Transform assaultBtnTransform   = ((UnityEngine.UI.Button)assaultBtnProp.objectReferenceValue).transform;
            Transform sniperBtnTransform    = ((UnityEngine.UI.Button)sniperBtnProp.objectReferenceValue).transform;

            // 3. 각 버튼 하위에 AutoIndicator 생성 (없는 경우에만)
            GameObject pistoleerIndicator = FindOrCreateChild(pistoleerBtnTransform, "AutoIndicator");
            GameObject assaultIndicator   = FindOrCreateChild(assaultBtnTransform, "AutoIndicator");
            GameObject sniperIndicator    = FindOrCreateChild(sniperBtnTransform, "AutoIndicator");

            // 4. SerializedProperty에 연결
            SerializedProperty pistoleerIndicatorProp = so.FindProperty("_pistoleerAutoIndicator");
            SerializedProperty assaultIndicatorProp   = so.FindProperty("_assaultAutoIndicator");
            SerializedProperty sniperIndicatorProp    = so.FindProperty("_sniperAutoIndicator");

            if (pistoleerIndicatorProp == null || assaultIndicatorProp == null || sniperIndicatorProp == null)
            {
                Debug.LogError("[SetupAutoIndicators] _pistoleerAutoIndicator, _assaultAutoIndicator, _sniperAutoIndicator 프로퍼티를 찾을 수 없습니다. 코드를 먼저 컴파일하세요.");
                return;
            }

            pistoleerIndicatorProp.objectReferenceValue = pistoleerIndicator;
            assaultIndicatorProp.objectReferenceValue   = assaultIndicator;
            sniperIndicatorProp.objectReferenceValue    = sniperIndicator;

            // 변경사항 적용
            so.ApplyModifiedProperties();

            // Undo 등록 (Ctrl+Z로 되돌리기 가능)
            Undo.RegisterCompleteObjectUndo(panelUI, "Setup Auto Indicators");

            Debug.Log($"[SetupAutoIndicators] 완료! 3개 AutoIndicator 생성 및 연결됨.\n" +
                      $"  - {pistoleerBtnTransform.name}/AutoIndicator\n" +
                      $"  - {assaultBtnTransform.name}/AutoIndicator\n" +
                      $"  - {sniperBtnTransform.name}/AutoIndicator\n" +
                      $"Inspector에서 위치/크기/비주얼을 조정하세요.");
        }

        /// <summary>
        /// 부모 Transform 하위에서 지정된 이름의 자식을 찾거나, 없으면 새로 생성.
        /// 이미 존재하면 기존 오브젝트를 반환하여 중복 생성 방지.
        /// </summary>
        /// <param name="parent">부모 Transform</param>
        /// <param name="childName">찾거나 생성할 자식 이름</param>
        /// <returns>찾거나 생성된 GameObject</returns>
        private static GameObject FindOrCreateChild(Transform parent, string childName)
        {
            // 기존 자식에서 이름으로 탐색
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                Debug.Log($"[SetupAutoIndicators] 기존 오브젝트 재사용: {parent.name}/{childName}");
                return existing.gameObject;
            }

            // 없으면 새로 생성
            GameObject newObj = new GameObject(childName);
            newObj.transform.SetParent(parent, false);

            // RectTransform 추가 (UI 계층이므로)
            // SetParent 시 Canvas 하위이면 자동으로 RectTransform이 추가됨
            // 기본 위치는 부모 중앙, 크기 조정은 Inspector에서 수동으로
            RectTransform rt = newObj.GetComponent<RectTransform>();
            if (rt == null)
                rt = newObj.AddComponent<RectTransform>();

            // 기본 앵커: 부모 중앙, 크기 30x30 (필요 시 Inspector에서 조정)
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(30f, 30f);
            rt.anchoredPosition = Vector2.zero;

            // 시작 시 비활성 (자동 생산 OFF 상태가 기본)
            newObj.SetActive(false);

            // Undo 등록
            Undo.RegisterCreatedObjectUndo(newObj, $"Create {childName}");

            Debug.Log($"[SetupAutoIndicators] 새 오브젝트 생성: {parent.name}/{childName}");
            return newObj;
        }
    }
}
