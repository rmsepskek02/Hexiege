#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Presentation;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// [1회성 셋업] ProductionPopup의 잠금 유닛 버튼 슬롯에 자물쇠 인디케이터(LockIndicator)를 추가하는 에디터 스크립트.
    ///
    /// 배경:
    ///  - ProductionPopup의 유닛 버튼은 슬롯이 3개이며 각각 1/2/3단계 유닛에 대응한다.
    ///    · 슬롯0(index 0): 1단계 유닛 — 항상 해금 → 자물쇠 불필요
    ///    · 슬롯1(index 1): 2단계 유닛 — 잠길 수 있음 → 자물쇠 필요
    ///    · 슬롯2(index 2): 3단계 유닛 — 잠길 수 있음 → 자물쇠 필요
    ///  - 기존 씬에는 ProductionPanelUI의 `_unitLockIndicators` 리스트가 비어 있어,
    ///    런타임 코드가 켜고 끌 잠금 오버레이 자체가 존재하지 않는 상태였다.
    ///
    /// 동작 개요:
    ///  1) 현재 열려 있는 씬에서 ProductionPanelUI 컴포넌트를 찾는다(비활성 포함).
    ///  2) SerializedObject로 private 직렬화 필드 `_unitButtons`(List&lt;Button&gt;)를 읽는다.
    ///  3) 인덱스 1, 2 버튼의 GameObject(= Slot GO)를 가져온다.
    ///     (인덱스 0은 1단계 유닛이므로 항상 해금 → 자물쇠 인디케이터를 만들지 않는다.)
    ///  4) 각 Slot GO 아래에 "LockIndicator" 자식 GO를 만들고, 그 위에 자물쇠 Image를 구성한다.
    ///     (이미 있으면 중복 생성하지 않고 기존 것을 재사용한다.)
    ///  5) 만들거나 찾은 2개의 LockIndicator GO를 `_unitLockIndicators` 리스트에 연결한다.
    ///
    /// 이 스크립트는 코드 로직을 바꾸지 않고, 씬의 GameObject 구성(Inspector 값)만 변경한다.
    /// 사용법: Unity 상단 메뉴 [Hexiege/Setup/잠금 아이콘 추가] 실행.
    /// </summary>
    public static class AddLockIcons
    {
        // 자물쇠 스프라이트 에셋 경로. AssetDatabase로 로드한다.
        private const string LockSpritePath = "Assets/_Project/Sprites/UI/Icons/ui_icon_lock.png";

        // Slot GO 아래에 생성할 자식 오브젝트 이름. 이 이름이 이미 있으면 재사용한다.
        private const string LockIndicatorObjectName = "LockIndicator";

        // ProductionPanelUI에서 읽어올 private 직렬화 필드 이름.
        // (필드명이 바뀌면 이 상수들도 함께 바꿔야 한다.)
        private const string UnitButtonsFieldName = "_unitButtons";
        private const string LockIndicatorsFieldName = "_unitLockIndicators";

        // 자물쇠 인디케이터를 추가할 슬롯 인덱스.
        // 슬롯1(2단계 유닛), 슬롯2(3단계 유닛)만 잠길 수 있으므로 1, 2번만 대상으로 한다.
        // 슬롯0(1단계 유닛)은 항상 해금이라 제외한다.
        private static readonly int[] TargetSlotIndices = { 1, 2 };

        [MenuItem("Hexiege/Setup/잠금 아이콘 추가")]
        public static void AddIcons()
        {
            // ---------- 1) 자물쇠 스프라이트 로드 ----------
            // 스프라이트를 먼저 확보해 둔다. 없으면 작업할 의미가 없으므로 즉시 중단한다.
            var lockSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LockSpritePath);
            if (lockSprite == null)
            {
                Debug.LogError($"[AddLockIcons] 자물쇠 스프라이트를 찾지 못했습니다: '{LockSpritePath}'. 작업을 중단합니다.");
                return;
            }

            // ---------- 2) 씬에서 ProductionPanelUI 찾기 ----------
            // 비활성(숨김) 오브젝트에 붙어 있을 수도 있으므로 includeInactive=true로 검색한다.
            var panel = Object.FindObjectOfType<ProductionPanelUI>(true);
            if (panel == null)
            {
                Debug.LogError("[AddLockIcons] 현재 씬에서 ProductionPanelUI를 찾지 못했습니다. " +
                               "Game.unity 씬을 연 상태에서 다시 실행하세요.");
                return;
            }

            // ---------- 3) private 직렬화 필드 _unitButtons 읽기 ----------
            // 직접 필드 접근은 private이라 불가능하므로, SerializedObject로 직렬화된 값을 읽는다.
            var so = new SerializedObject(panel);

            var buttonsProp = so.FindProperty(UnitButtonsFieldName);
            if (buttonsProp == null || !buttonsProp.isArray)
            {
                Debug.LogError($"[AddLockIcons] ProductionPanelUI에서 '{UnitButtonsFieldName}' 리스트를 찾지 못했습니다. " +
                               "필드명이 바뀌었는지 확인하세요.");
                return;
            }

            var indicatorsProp = so.FindProperty(LockIndicatorsFieldName);
            if (indicatorsProp == null || !indicatorsProp.isArray)
            {
                Debug.LogError($"[AddLockIcons] ProductionPanelUI에서 '{LockIndicatorsFieldName}' 리스트를 찾지 못했습니다. " +
                               "필드명이 바뀌었는지 확인하세요.");
                return;
            }

            // 컴포넌트(직렬화 필드 변경) 자체를 Undo에 등록해 Ctrl+Z로 되돌릴 수 있게 한다.
            Undo.RegisterCompleteObjectUndo(panel, "Add Lock Indicators");

            // 슬롯 인덱스 → 만들어진(또는 찾은) LockIndicator GO를 보관.
            // _unitLockIndicators 리스트는 슬롯 순서대로(슬롯1, 슬롯2) 채워야 하므로 순서를 보장한다.
            var indicatorGos = new List<GameObject>();

            int createdCount = 0;  // 새로 만든 인디케이터 개수
            int reusedCount = 0;   // 기존 것을 재사용한 개수

            // ---------- 4) 대상 슬롯(1, 2)에 LockIndicator 자식 생성/재사용 ----------
            foreach (int slotIndex in TargetSlotIndices)
            {
                // 슬롯 인덱스가 _unitButtons 범위를 벗어나면 건너뛴다(안전 가드).
                if (slotIndex >= buttonsProp.arraySize)
                {
                    Debug.LogWarning($"[AddLockIcons] _unitButtons에 슬롯 {slotIndex}가 없습니다(개수 {buttonsProp.arraySize}). 건너뜁니다.");
                    continue;
                }

                // 버튼 컴포넌트 → 그 버튼이 붙어 있는 GameObject(= Slot GO)를 구한다.
                var buttonElement = buttonsProp.GetArrayElementAtIndex(slotIndex);
                var button = buttonElement.objectReferenceValue as Button;
                if (button == null)
                {
                    Debug.LogWarning($"[AddLockIcons] _unitButtons[{slotIndex}]가 비어 있습니다(None). 건너뜁니다.");
                    continue;
                }

                var slotGo = button.gameObject;

                // 이미 "LockIndicator" 자식이 있으면 재사용하고, 없으면 새로 만든다.
                var existing = slotGo.transform.Find(LockIndicatorObjectName);
                GameObject indicatorGo;
                if (existing != null)
                {
                    indicatorGo = existing.gameObject;
                    reusedCount++;
                    Debug.Log($"[AddLockIcons] '{slotGo.name}'에 이미 '{LockIndicatorObjectName}'이 있어 재사용합니다.");
                }
                else
                {
                    indicatorGo = CreateLockIndicator(slotGo, lockSprite);
                    createdCount++;
                }

                indicatorGos.Add(indicatorGo);
            }

            // ---------- 5) 생성/재사용한 인디케이터를 _unitLockIndicators 리스트에 연결 ----------
            // 슬롯 순서(슬롯1 → 슬롯2)대로 리스트를 채운다. 코드의 UpdateLockIndicators()는
            // _unitLockIndicators[0]을 슬롯1, [1]을 슬롯2로 해석하므로 이 순서가 중요하다.
            indicatorsProp.arraySize = indicatorGos.Count;
            for (int i = 0; i < indicatorGos.Count; i++)
            {
                indicatorsProp.GetArrayElementAtIndex(i).objectReferenceValue = indicatorGos[i];
            }

            // SerializedObject 변경 사항을 실제 컴포넌트에 반영(직렬화 필드 쓰기).
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(panel);

            // ---------- 6) 변경 사항 저장 ----------
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log($"[AddLockIcons] 완료: LockIndicator {createdCount}개 생성, {reusedCount}개 재사용 → " +
                      $"'_unitLockIndicators' {indicatorGos.Count}개 연결, '{activeScene.name}.unity' 저장.");
        }

        /// <summary>
        /// Slot GameObject 아래에 "LockIndicator" 자식을 만들고 자물쇠 Image를 구성한다.
        /// 버튼 우측 하단 코너에 버튼 대비 약 40% 크기로 비율 기반 배치한다.
        /// (GameSystemRules 규칙 2 — 고정 픽셀 금지, 앵커 비율 기반 배치 준수)
        ///
        /// Slot GO에는 HorizontalLayoutGroup이 있으므로 LayoutElement.ignoreLayout=true로
        /// 레이아웃 흐름에서 제외시켜 자유 위치(앵커 기준)로 떠 있게 한다.
        /// (BorderOverlay GO와 동일한 방식 — HLG 위에 자유롭게 배치)
        ///
        /// 초기 상태는 SetActive(false). 런타임에 UpdateLockIndicators()가 잠금 시 켜준다.
        /// </summary>
        /// <returns>생성한 LockIndicator GameObject.</returns>
        private static GameObject CreateLockIndicator(GameObject parent, Sprite lockSprite)
        {
            // GameObject 생성 자체를 Undo에 등록해 Ctrl+Z로 통째로 되돌릴 수 있게 한다.
            var indicatorGo = new GameObject(LockIndicatorObjectName,
                typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            Undo.RegisterCreatedObjectUndo(indicatorGo, "Create LockIndicator");

            // 부모(Slot GO) 아래로 이동. worldPositionStays=false로 로컬 기준 배치.
            Undo.SetTransformParent(indicatorGo.transform, parent.transform, "Parent LockIndicator");
            indicatorGo.transform.localScale = Vector3.one;

            // ---------- RectTransform: 우측 하단 코너, 버튼 대비 약 40% 크기 ----------
            // anchorMin/Max를 (0.6,0)~(1,0.4)로 잡으면 부모 영역의 우측 하단 40% 사각형을 차지한다.
            // sizeDelta=0, anchoredPosition=0이면 앵커 비율이 그대로 크기/위치가 된다(고정 픽셀 없음).
            var rt = indicatorGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.6f, 0f);
            rt.anchorMax = new Vector2(1f, 0.4f);
            rt.pivot = new Vector2(1f, 0f); // 우측 하단 기준
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            // ---------- LayoutElement: HorizontalLayoutGroup에서 제외 ----------
            // Slot GO에 HLG가 있으므로 ignoreLayout=true로 두지 않으면 자물쇠가
            // 다른 자식들과 가로로 나란히 배치되어 버린다. true로 두면 앵커 기준 자유 배치된다.
            var layoutElement = indicatorGo.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            // ---------- Image: 자물쇠 스프라이트 ----------
            var image = indicatorGo.GetComponent<Image>();
            image.sprite = lockSprite;
            image.color = Color.white;     // 아이콘 원본 색 유지
            image.raycastTarget = false;   // 클릭 이벤트를 가리지 않도록 OFF (버튼으로 그대로 전달)
            image.preserveAspect = true;   // 스프라이트 가로세로 비율 유지(찌그러짐 방지)

            // ---------- 초기 상태: 숨김 ----------
            // 런타임에 UpdateLockIndicators()가 잠금 유닛에 대해서만 SetActive(true)로 켜준다.
            indicatorGo.SetActive(false);

            EditorUtility.SetDirty(indicatorGo);
            return indicatorGo;
        }
    }
}
#endif
