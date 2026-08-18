// ============================================================================
// SkillSetup_Scene.cs  (에디터 전용 · 1회성 셋업)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  1) 먼저  Hexiege > Skill > 1. Create Skill Data Assets  를 실행해 둔다.     │
// │  2) 상단 메뉴  Hexiege > Skill > 2. Setup Scene (Panel, Aim, Network)  실행. │
// │     (Game 씬이 안 열려 있으면 스크립트가 자동으로 열고, 끝나면 저장한다.)      │
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 하는가(유니티 초급자 기준):
//   스킬 건물 Phase 1을 실기로 돌리는 데 필요한 "씬 골격"을 자동으로 만들고 배선한다.
//   손으로 프리팹을 만들고 SerializeField를 연결하는 번거로운 작업을 대신한다.
//     A) BuildingSkillPanel — 기존 BuildingActionPanel을 "복제"해 룩·구조를 그대로 물려받고,
//        스킬 슬롯 1~5에만 아이콘 Image + 쿨다운 오버레이를 추가한다(하드코딩으로 새로 그리지 않음).
//     B) SkillAimReticle(조준 범위 원) + SkillAimController(조준 입력) + 하단 X(취소) 버튼.
//     C) NetworkSkillController(멀티 발동 중계) 씬 오브젝트(NetworkObject 포함, 자동 스폰).
//     D) GameBootstrapper의 신규 슬롯 배선:
//          _skillLoadoutConfig / _buildingSkillPanelUI / _skillAimController / _networkSkillController.
//   패널 룩은 기존 BuildingActionPanel 복제라 프로젝트 UI와 일치한다. 스킬 아이콘/조준 원/쿨다운 fill
//   스프라이트만 임시(내장 스프라이트 또는 미지정)이며, 참조 배선까지 끝내 실기 동작이 가능한 상태로 만든다.
//
// 멱등성(여러 번 실행해도 안전):
//   이름으로 기존 오브젝트를 찾아 재사용한다(FindOrCreateChild/TryGetComponent). 중복 생성하지 않는다.
//
// 이 스크립트가 하지 않는 것(사용자 후속):
//   · 정식 아트(스킬 아이콘/조준 원/쿨다운 오버레이/취소 X 스프라이트) 지정.
//   · 미세 레이아웃/앵커 튜닝(모바일 9:16 배치), 엣지 스크롤 여백/속도 실기 튜닝(규칙 21).
//   · NetworkObject의 GlobalObjectIdHash는 Unity가 자동 부여하지만, 멀티 실기 전 Inspector에서
//     NetworkSkillController가 NetworkManager에 씬 오브젝트로 잡히는지 육안 확인 권장.
// ============================================================================

using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hexiege.Bootstrap;
using Hexiege.Presentation;
using Hexiege.Infrastructure;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 스킬 패널·조준·네트워크 오브젝트를 씬에 생성하고 GameBootstrapper에 배선하는 1회성 에디터 스크립트.
    /// </summary>
    public static class SkillSetup_Scene
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string LoadoutAssetPath = "Assets/_Project/Resources/Config/Skills/SkillLoadoutConfig.asset";
        private const string ColorConfigPath = "Assets/_Project/Resources/Config/UIColorConfig.asset";
        private const string BoldFontPath = "Assets/_Project/Fonts/Maplestory Bold SDF.asset";

        private const int SkillSlotCount = 5;

        [MenuItem("Hexiege/Skill/2. Setup Scene (Panel, Aim, Network)")]
        public static void Run()
        {
            // ── 0) Game 씬 + GameBootstrapper 확보 ─────────────────────────
            bool sceneOpenedByThis = false;
            GameBootstrapper bootstrapper = Object.FindFirstObjectByType<GameBootstrapper>(FindObjectsInactive.Include);
            if (bootstrapper == null)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
                {
                    Debug.LogError($"[Skill Setup] Game 씬을 찾지 못했습니다: {GameScenePath}");
                    return;
                }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.LogWarning("[Skill Setup] 씬 열기가 취소되었습니다.");
                    return;
                }
                EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
                sceneOpenedByThis = true;
                bootstrapper = Object.FindFirstObjectByType<GameBootstrapper>(FindObjectsInactive.Include);
            }
            if (bootstrapper == null)
            {
                Debug.LogError("[Skill Setup] GameBootstrapper를 씬에서 찾지 못했습니다.");
                return;
            }
            Scene scene = bootstrapper.gameObject.scene;

            // 공용 에셋 로드(없어도 진행 — 플레이스홀더/폴백).
            TMP_FontAsset boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            UIColorConfig colorConfig = AssetDatabase.LoadAssetAtPath<UIColorConfig>(ColorConfigPath);
            SkillLoadoutConfig loadout = AssetDatabase.LoadAssetAtPath<SkillLoadoutConfig>(LoadoutAssetPath);
            if (loadout == null)
                Debug.LogWarning("[Skill Setup] SkillLoadoutConfig 에셋이 없습니다. 먼저 '1. Create Skill Data Assets'를 실행하세요.");

            // ── UI 부모 결정(기존 패널들이 형제로 있는 [UI] 루트) ─────────────
            // ⚠️ 스킬 패널은 반드시 BuildingActionPanel "안"이 아니라 그 "형제"로 둔다.
            //   BuildingActionPanel은 자체 오버라이드 Canvas(SO 200)를 가진 독립 패널이라,
            //   그 GetComponentInParent<Canvas>()는 자기 자신의 Canvas를 돌려준다 → 스킬 패널이
            //   BuildingActionPanel 자식으로 중첩되면 부모가 닫힘(alpha 0)일 때 함께 안 보인다.
            //   그래서 BuildingActionPanel의 "부모"(다른 패널들과 같은 컨테이너)를 부모로 잡는다.
            Transform panelParent = ResolveSkillPanelParent();
            if (panelParent == null)
            {
                Debug.LogError("[Skill Setup] 스킬 패널을 붙일 부모(UI 루트)를 찾지 못했습니다. 씬에 Canvas/패널이 있는지 확인하세요.");
                return;
            }

            // ── A) 스킬 패널 생성/배선 ─────────────────────────────────────
            BuildingSkillPanelUI skillPanel = BuildSkillPanel(panelParent, boldFont, colorConfig);

            // ── B) 조준 원 + 조준 컨트롤러 + 취소 X 버튼 ──────────────────
            SkillAimReticle reticle = BuildReticle();
            RectTransform cancelZone = BuildCancelButton(panelParent, boldFont);
            SkillAimController aimController = BuildAimController(reticle, cancelZone);

            // ── C) NetworkSkillController 씬 오브젝트 ──────────────────────
            NetworkSkillController netSkill = BuildNetworkSkillController();

            // ── D) GameBootstrapper 배선 ───────────────────────────────────
            Connect(bootstrapper, "_skillLoadoutConfig", loadout);
            Connect(bootstrapper, "_buildingSkillPanelUI", skillPanel);
            Connect(bootstrapper, "_skillAimController", aimController);
            Connect(bootstrapper, "_networkSkillController", netSkill);

            // ── 저장 처리 ──────────────────────────────────────────────────
            EditorUtility.SetDirty(bootstrapper);
            if (skillPanel != null) EditorUtility.SetDirty(skillPanel);
            EditorSceneManager.MarkSceneDirty(scene);
            if (sceneOpenedByThis) EditorSceneManager.SaveScene(scene);

            Debug.Log(
                "[Skill Setup] 씬 골격 생성·배선 완료(플레이스홀더 아트).\n" +
                "  · BuildingSkillPanel(= BuildingActionPanel 복제, [UI] 루트 형제·오버라이드 Canvas SO 200) / SkillAimReticle / SkillAimController / SkillCancelButton / NetworkSkillController 생성.\n" +
                "  · (교정) 기존 BuildingSkillPanel(하드코딩/이전 클론)은 제거 후 BuildingActionPanel 복제로 재생성됩니다(항상 1개).\n" +
                "  · GameBootstrapper 슬롯 4종 연결(_skillLoadoutConfig/_buildingSkillPanelUI/_skillAimController/_networkSkillController).\n" +
                "  · (스펙) 스킬 버튼=아이콘만(슬롯 CostContainer/라벨 비활성) · 쿨다운 오버레이=유휴 시 숨김(시전 중만 표시) · 취소 X=ui_btn_cancel 이미지 버튼·평소 숨김(조준 중만).\n" +
                "  · (테스트용) 각 스킬 슬롯에 임시 라벨(SkillTestLabel) 생성·배선 — 스킬 종류(폭탄/빙결/버프/둔화/회복)를 텍스트로 표시. 정식 아이콘 도입 시 제거.\n" +
                (sceneOpenedByThis ? "  · 씬 자동 저장 완료.\n" : "  · 열린 씬 dirty 표시 — Ctrl+S로 저장하세요.\n") +
                "  · 쿨다운 fill·조준 원에 Unity 내장 스프라이트를 임시 지정(테스트용) — 이미 셋업된 씬이면 이 메뉴를 다시 실행하면 반영됩니다.\n" +
                "  · 후속: 아이콘/조준원/오버레이/취소X 정식 아트로 교체, 레이아웃·엣지스크롤 여백 튜닝, 멀티 NetworkObject 확인.");

            Selection.activeObject = skillPanel != null ? (Object)skillPanel.gameObject : bootstrapper;
        }

        // ====================================================================
        // A) 스킬 패널
        // ====================================================================

        /// <summary>
        /// BuildingSkillPanel을 "기존 BuildingActionPanel 복제 기반"으로 만든다(룩·구조 그대로).
        ///
        /// 왜 복제인가(핵심):
        ///   처음부터 하드코딩으로 그리면 기존 패널과 룩이 전혀 안 맞는다(조잡한 박스). 그래서 씬의 실제
        ///   BuildingActionPanel GameObject를 통째로 Object.Instantiate로 복제해 배경/크기/앵커/헤더/닫기/철거/
        ///   AnimatedPanel/오버라이드 Canvas(SO 200)/GraphicRaycaster/CanvasGroup을 전부 그대로 물려받는다.
        ///   Unity의 Instantiate는 계층 내부 참조(_popup/_headerText/_allSlotButtons 등)를 복제본 자식으로
        ///   자동 remap하므로, 그 값을 읽어 BuildingSkillPanelUI에 그대로 옮겨 배선한다.
        ///
        /// 슬롯 매핑(BuildingActionPanel의 _allSlotButtons 9개 재사용):
        ///   index 0~4 = 스킬 버튼(각 슬롯에 Icon Image + SkillCooldownOverlay만 신규 추가),
        ///   index 5   = 철거(기존 _demolishButton 그대로),
        ///   index 6~8 = 예약(기존처럼 CanvasGroup alpha 0으로 숨김).
        ///
        /// 멱등: 재실행 시 기존 BuildingSkillPanel(하드코딩/이전 클론)을 먼저 제거하고 새로 복제한다(정확히 1개).
        /// </summary>
        private static BuildingSkillPanelUI BuildSkillPanel(Transform panelParent, TMP_FontAsset font, UIColorConfig colorConfig)
        {
            // ── 0) 복제 원본(BuildingActionPanel) 확보 ─────────────────────────
            var srcAction = Object.FindFirstObjectByType<BuildingActionPanelUI>(FindObjectsInactive.Include);
            if (srcAction == null)
            {
                Debug.LogError("[Skill Setup] 복제 원본 BuildingActionPanel(BuildingActionPanelUI)을 씬에서 찾지 못했습니다.");
                return null;
            }
            GameObject srcGo = srcAction.gameObject;

            // ── 1) 기존 스킬 패널(하드코딩/이전 클론) 제거 → 정확히 1개 보장 ──
            GameObject existing = FindExistingSkillPanel();
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
                Debug.Log("[Skill Setup] 기존 BuildingSkillPanel을 제거하고 BuildingActionPanel 복제로 재생성합니다.");
            }

            // ── 2) 복제(로컬 rect 보존 — instantiateInWorldSpace=false) ───────
            GameObject clone = Object.Instantiate(srcGo, panelParent, false);
            clone.name = "BuildingSkillPanel";

            // ── 3) 복제본의 액션 패널 컴포넌트에서 remap된 참조 읽기 ──────────
            //   (Instantiate가 계층 내부 참조를 복제본 자식으로 자동 remap한 상태이다.)
            var cloneAction = clone.GetComponent<BuildingActionPanelUI>();
            if (cloneAction == null)
            {
                Debug.LogError("[Skill Setup] 복제본에 BuildingActionPanelUI가 없습니다(원본 구조 확인 필요).");
                return null;
            }
            var srcSo = new SerializedObject(cloneAction);
            Object popupRef = GetRef(srcSo, "_popup");
            Object headerRef = GetRef(srcSo, "_headerText");
            Object cancelRef = GetRef(srcSo, "_cancelButton");
            Object demolishRef = GetRef(srcSo, "_demolishButton");
            Object refundRef = GetRef(srcSo, "_demolishRefundText");
            Object colorRef = GetRef(srcSo, "_colorConfig");

            // _allSlotButtons(9) 읽기.
            var allSlots = new List<Button>(9);
            SerializedProperty slotsProp = srcSo.FindProperty("_allSlotButtons");
            if (slotsProp != null)
            {
                for (int i = 0; i < slotsProp.arraySize; i++)
                    allSlots.Add(slotsProp.GetArrayElementAtIndex(i).objectReferenceValue as Button);
            }

            // ── 4) 액션 컴포넌트 제거 → 스킬 컴포넌트 추가 ─────────────────────
            Object.DestroyImmediate(cloneAction);
            var panel = clone.AddComponent<BuildingSkillPanelUI>();

            // ── 5) base 필드 배선(복제본 자식을 가리키는 remap된 참조 그대로) ──
            //   _colorConfig는 외부 에셋 참조라 복제본에서도 동일(공유). 인자로 받은 colorConfig가 있으면 우선 사용.
            Connect(panel, "_popup", popupRef);
            Connect(panel, "_headerText", headerRef);
            Connect(panel, "_cancelButton", cancelRef);
            Connect(panel, "_demolishButton", demolishRef);
            Connect(panel, "_demolishRefundText", refundRef);
            Connect(panel, "_colorConfig", colorConfig != null ? colorConfig : colorRef);

            // ── 6) 스킬 슬롯(0~4)에 Icon + CooldownOverlay 추가 후 수집 ───────
            var skillButtons = new List<Object>(SkillSlotCount);
            var skillIcons = new List<Object>(SkillSlotCount);
            var skillOverlays = new List<Object>(SkillSlotCount);
            // ⚠️ 테스트용 임시 라벨 — 정식 아이콘 도입 시 제거.
            var skillLabels = new List<Object>(SkillSlotCount);

            for (int i = 0; i < SkillSlotCount && i < allSlots.Count; i++)
            {
                Button slotBtn = allSlots[i];
                if (slotBtn == null) continue;
                Transform slot = slotBtn.transform;

                // ── 부가요소 숨김(규칙 2 — 마나·비용 없음) ─────────────────────
                //   BuildingActionPanel 슬롯 템플릿 = IconImage + CostContainer(GoldIcon/CostText).
                //   스킬 버튼은 "아이콘만" 보여야 하므로 비용/골드 표시를 감춘다.
                //
                //   ⚠️ [버튼 크기 불균일 버그 수정 — 원인·해결]
                //     원인: 예전 코드는 CostContainer를 SetActive(false)로 "레이아웃에서 제거"했다.
                //       슬롯이 속한 Row/Grid는 HorizontalLayoutGroup(childForceExpand)로 배치되는데,
                //       force-expand는 "여유 공간만 균등 분배"할 뿐 각 행의 preferred 높이(=자식 슬롯의
                //       최대 높이)가 다르면 행 높이가 달라진다. CostContainer를 끄면 스킬 슬롯의 preferred
                //       높이가 확 낮아지는데, 같은 행에 있는 철거 버튼(슬롯 6)·예약 슬롯은 CostContainer를
                //       유지하므로 그 행만 높아진다 → 순수 스킬 행(0~2번 슬롯)보다 4~5번 슬롯이 더 커져
                //       "버튼 크기가 서로 불균일"해졌다. (원본 액션 패널은 모든 슬롯이 CostContainer를
                //       유지해 행 높이가 같아 균일했다.)
                //     해결: SetActive(false)로 "빼지" 말고, CanvasGroup alpha=0으로 "보이지 않게만" 한다.
                //       레이아웃 footprint(preferred 높이)가 원본 슬롯과 동일하게 유지되어 모든 행 높이가
                //       같아지고, 버튼이 원본처럼 균일해진다. (하드코딩 크기 대신 원본 레이아웃 규칙 준수.)
                HideChildKeepLayout(slot, "CostContainer");
                // "Label"은 이 슬롯 템플릿에 존재하지 않는 자식이라 별도 처리가 필요 없다(원본 구조 확인 완료).

                // ── 스킬 아이콘 = 슬롯의 기존 IconImage 재사용(없으면 Icon 생성) ──
                //   기존 아이콘 자리를 그대로 써 룩을 맞춘다. 런타임에 BuildingSkillPanelUI.OnShow가
                //   SkillData.Icon을 넣고 enabled를 켠다(스킬 있는 슬롯만).
                Image icon = FindImageChild(slot, "IconImage");
                if (icon == null)
                {
                    GameObject iconGo = FindOrCreateChild(slot, "Icon");
                    SetStretch(iconGo.GetComponent<RectTransform>());
                    icon = EnsureImage(iconGo, Color.white, null);
                }
                icon.raycastTarget = false;   // 버튼 클릭을 막지 않도록.
                icon.sprite = null;
                icon.enabled = false;         // 스프라이트 미지정 → 숨김(런타임에 켜짐).

                // ── 쿨다운 오버레이(시전 중에만 표시) ─────────────────────────
                //   슬롯의 마지막 자식이라 아이콘 위에 그려진다. 유휴 시엔 alpha 0로 완전히 숨김(규칙 10).
                SkillCooldownOverlay overlay = BuildCooldownOverlay(slot, font);

                // ── 테스트용 임시 라벨(스킬 종류 텍스트) — 정식 아이콘 도입 시 제거 ──
                //   빈 아이콘만으로는 어떤 스킬인지 구분이 안 되므로, 슬롯 위에 종류 이름을 얹는다.
                TextMeshProUGUI testLabel = BuildTestSkillLabel(slot, font);

                skillButtons.Add(slotBtn);
                skillIcons.Add(icon);
                skillOverlays.Add(overlay);
                skillLabels.Add(testLabel);
            }

            // ── 7) 스킬 슬롯 리스트 배선 ─────────────────────────────────────
            ConnectList(panel, "_skillSlotButtons", skillButtons);
            ConnectList(panel, "_skillSlotIcons", skillIcons);
            ConnectList(panel, "_skillSlotOverlays", skillOverlays);
            // ⚠️ 테스트용 임시 라벨 리스트 배선 — 정식 아이콘 도입 시 이 줄과 라벨 오브젝트 제거.
            ConnectList(panel, "_skillSlotLabels", skillLabels);

            // ── 8) 예약 슬롯(6~8) 숨김 — 기존 액션 패널이 비활성 슬롯을 감추던 방식(CanvasGroup alpha 0) ──
            for (int i = SkillSlotCount + 1; i < allSlots.Count; i++) // index 6,7,8 (5=철거는 유지)
            {
                Button reserved = allSlots[i];
                if (reserved == null) continue;
                GameObject go = reserved.gameObject;
                if (!go.TryGetComponent(out CanvasGroup cg)) cg = go.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }

            return panel;
        }

        /// <summary> SerializedObject에서 objectReference 값을 읽는다(없으면 null). </summary>
        private static Object GetRef(SerializedObject so, string field)
        {
            SerializedProperty p = so.FindProperty(field);
            return p != null ? p.objectReferenceValue : null;
        }

        /// <summary> 직속 자식을 이름으로 찾아 비활성화한다(없으면 no-op). 슬롯의 비용/라벨 제거에 사용. </summary>
        private static void DisableChild(Transform parent, string childName)
        {
            Transform t = parent.Find(childName);
            if (t != null && t.gameObject.activeSelf) t.gameObject.SetActive(false);
        }

        /// <summary>
        /// 직속 자식을 "레이아웃에는 남기고 화면에서만" 숨긴다(CanvasGroup alpha=0). 없으면 no-op.
        ///
        /// SetActive(false)와의 결정적 차이:
        ///   SetActive(false)는 자식을 레이아웃 계산에서 완전히 제거해 부모 슬롯의 preferred 크기를 바꾼다.
        ///   그러면 이 자식을 끈 슬롯과 켜둔 슬롯의 크기가 달라져 버튼 크기가 불균일해진다(버그).
        ///   이 메서드는 GameObject를 켜둔 채 CanvasGroup.alpha=0으로 그래픽만 감추므로
        ///   RectTransform/LayoutElement가 그대로 레이아웃에 기여 → 원본 슬롯과 크기가 동일하게 유지된다.
        ///
        /// 멱등: 재실행 시 복제본이 원본에서 새로 만들어져 자식이 다시 켜진 상태로 시작하므로,
        ///   여기서 alpha=0으로 다시 감추면 항상 같은 결과가 된다.
        /// </summary>
        /// <param name="parent">자식을 가진 부모(슬롯) Transform.</param>
        /// <param name="childName">숨길 직속 자식 이름(예: "CostContainer").</param>
        private static void HideChildKeepLayout(Transform parent, string childName)
        {
            Transform t = parent.Find(childName);
            if (t == null) return;

            // 레이아웃에는 남겨야 하므로 GameObject는 반드시 활성 상태로 둔다(이전 셋업이 꺼놨을 수 있음).
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);

            GameObject go = t.gameObject;
            if (!go.TryGetComponent(out CanvasGroup cg)) cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;            // 그래픽만 완전히 숨김(비용/골드 텍스트 안 보임).
            cg.interactable = false;
            cg.blocksRaycasts = false; // 버튼 클릭을 가리지 않도록.
        }

        /// <summary> 직속 자식(이름)의 Image 컴포넌트를 반환한다. 없으면 null. </summary>
        private static Image FindImageChild(Transform parent, string childName)
        {
            Transform t = parent.Find(childName);
            return t != null ? t.GetComponent<Image>() : null;
        }

        /// <summary>
        /// 슬롯 위 쿨다운 오버레이(SkillCooldownOverlay)를 멱등 생성한다.
        /// 시계방향 radial fill 이미지 + 남은초 TMP + CanvasGroup으로 구성한다.
        /// </summary>
        private static SkillCooldownOverlay BuildCooldownOverlay(Transform slot, TMP_FontAsset font)
        {
            GameObject overlayGo = FindOrCreateChild(slot, "CooldownOverlay");
            SetStretch(overlayGo.GetComponent<RectTransform>());

            // ⚠️ 슬롯 버튼에 LayoutGroup(아이콘+비용 배치용)이 있으면 자식인 오버레이도 레이아웃에 눌려
            //   얇은 한 줄로 찌그러진다. LayoutElement.ignoreLayout=true로 레이아웃에서 제외해야
            //   위 SetStretch(앵커 0~1)가 그대로 적용되어 오버레이가 "버튼 전체"를 덮는다.
            if (!overlayGo.TryGetComponent(out LayoutElement le)) le = overlayGo.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            // CanvasGroup(오버레이 전체 토글).
            if (!overlayGo.TryGetComponent(out CanvasGroup cg)) cg = overlayGo.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // 시계방향 radial fill 이미지(반투명 검정).
            GameObject fillGo = FindOrCreateChild(overlayGo.transform, "Fill");
            SetStretch(fillGo.GetComponent<RectTransform>());
            Image fill = EnsureImage(fillGo, new Color(0f, 0f, 0f, 0.6f), null);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;   // 12시에서 시작.
            // ⚠️ "어두운 오버레이가 12시부터 시계방향으로 걷혀 사라지는" 표준 쿨다운 스윕 방향 설정(근거):
            //   Unity Filled 이미지는 [fillOrigin에서 fillClockwise 방향으로 fillAmount만큼]을 "보이게(어둡게)" 그린다.
            //   SetCooldown이 fillAmount = remaining/total(남은 비율=어두운 비율)로 감소시키므로,
            //   fillClockwise=false(반시계로 그림)로 두면 → 드러나는(밝아지는) 경계가 12시부터 "시계방향"으로
            //   전진한다. 즉 어둠이 12시→3시→6시→9시 순서(시계방향)로 걷힌다.
            //   (fillClockwise=true면 반대로 반시계로 걷혀 요구와 어긋난다 — 이전 버그.)
            fill.fillClockwise = false;
            fill.fillAmount = 1f;                         // 초기 = 전부 어두움(쿨다운 시작 시 버튼 전체 덮음).
            fill.raycastTarget = false;
            // 임시 내장 스프라이트 — 정식 아트로 교체 전, radial fill이 화면에 실제로 렌더되게 한다.
            //   Filled 타입 Image는 스프라이트가 없으면 아무것도 그리지 않으므로, Unity 내장 UISprite를 임시로 넣는다.
            //   (버전에 따라 경로가 다를 수 있어 방어적으로 로드 — 실패 시 조용히 스킵.)
            Sprite cooldownSprite = LoadBuiltinSprite("UI/Skin/UISprite.psd", "UI/Skin/Background.psd");
            if (cooldownSprite != null) fill.sprite = cooldownSprite;

            // 남은 초 숫자.
            GameObject textGo = FindOrCreateChild(overlayGo.transform, "Remaining");
            SetStretch(textGo.GetComponent<RectTransform>());
            TextMeshProUGUI remaining = EnsureText(textGo, font, "", 28, TextAlignmentOptions.Center);

            if (!overlayGo.TryGetComponent(out SkillCooldownOverlay overlay))
                overlay = overlayGo.AddComponent<SkillCooldownOverlay>();

            Connect(overlay, "_fillImage", fill);
            Connect(overlay, "_remainingText", remaining);
            Connect(overlay, "_canvasGroup", cg);

            return overlay;
        }

        /// <summary>
        /// ⚠️ 테스트용 임시 라벨 — 정식 아이콘 도입 시 이 메서드와 호출부, 생성된 "SkillTestLabel" 오브젝트를 제거.
        /// 슬롯 버튼 위에 스킬 종류 텍스트(폭탄/빙결/버프/둔화/회복 등)를 얹어 실기 구분을 돕는다.
        /// 실제 표시 문자열은 런타임에 BuildingSkillPanelUI.OnShow가 스킬 메커니즘·상태종류로 파생해 넣는다.
        /// </summary>
        /// <param name="slot">라벨을 붙일 스킬 슬롯 버튼의 Transform.</param>
        /// <param name="font">사용할 TMP 폰트(없으면 TMP 기본 폰트).</param>
        /// <returns>배선용 TMP 라벨 컴포넌트.</returns>
        private static TextMeshProUGUI BuildTestSkillLabel(Transform slot, TMP_FontAsset font)
        {
            GameObject labelGo = FindOrCreateChild(slot, "SkillTestLabel");
            SetStretch(labelGo.GetComponent<RectTransform>());

            // 슬롯에 LayoutGroup(아이콘 배치용)이 있으면 자식 라벨도 눌려 찌그러진다 → 레이아웃 제외.
            if (!labelGo.TryGetComponent(out LayoutElement le)) le = labelGo.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            // 텍스트는 OnShow가 채운다. 여기선 빈 문자열로 생성만.
            TextMeshProUGUI label = EnsureText(labelGo, font, "", 24, TextAlignmentOptions.Center);

            // 아이콘·쿨다운 오버레이보다 뒤(마지막 형제)에 두어 항상 위에 읽히게 한다.
            labelGo.transform.SetAsLastSibling();
            return label;
        }

        // ====================================================================
        // B) 조준 원 / 컨트롤러 / 취소 버튼
        // ====================================================================

        /// <summary>
        /// SkillAimReticle(착탄 범위 조준원)을 월드 오브젝트로 멱등 생성한다(도식 룩: 반투명 채움 + 링 + 중앙 점).
        /// 루트를 X축 90도로 눕혀 지도(XZ 평면)에 깔고, 자식 SpriteRenderer 3겹(Ring/Fill/Dot)을 배선한다.
        /// 색·크기·타원은 SkillAimReticle의 Inspector 필드로 조절되며, 여기서는 구조·스프라이트만 만든다.
        /// </summary>
        private static SkillAimReticle BuildReticle()
        {
            GameObject root = GameObject.Find("SkillAimReticle");
            if (root == null) root = new GameObject("SkillAimReticle");
            // 지도(XZ 평면)에 눕도록 X축 90도 회전(컴포넌트도 Show/Awake에서 보장하지만 에디터에서도 눕혀 둔다).
            root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // 임시 내장 스프라이트(채워진 원) — 정식 아트 전 화면에 실제로 보이게 한다. 실패 시 방어적 스킵.
            //   3겹(Ring/Fill/Dot) 모두 같은 원 스프라이트를 쓰고, 크기·색은 컴포넌트가 구분해 적용한다.
            Sprite circle = LoadBuiltinSprite("UI/Skin/Knob.psd", "UI/Skin/UISprite.psd");

            // 지형에 파묻히지 않는 오버레이 머티리얼(전용 셰이더). 없으면 생성해 에셋으로 저장한다.
            Material overlayMat = EnsureOverlayMaterial();

            SpriteRenderer ring = EnsureReticlePart(root.transform, "Ring", circle, overlayMat);
            SpriteRenderer fill = EnsureReticlePart(root.transform, "Fill", circle, overlayMat);
            SpriteRenderer dot = EnsureReticlePart(root.transform, "Dot", circle, overlayMat);

            if (!root.TryGetComponent(out SkillAimReticle reticle))
                reticle = root.AddComponent<SkillAimReticle>();

            // 3겹 렌더러 배선(색·크기·타원 값은 컴포넌트 Inspector 기본값을 따른다).
            Connect(reticle, "_ringRenderer", ring);
            Connect(reticle, "_fillRenderer", fill);
            Connect(reticle, "_dotRenderer", dot);
            // 오버레이 머티리얼 컴포넌트 배선(런타임 자가 배선용 — ApplyRenderer가 재적용).
            Connect(reticle, "_overlayMaterial", overlayMat);
            return reticle;
        }

        // 조준원 오버레이 셰이더/머티리얼 경로.
        private const string OverlayShaderName = "Hexiege/SkillAimOverlay";
        private const string OverlayMaterialPath = "Assets/_Project/Materials/SkillAimOverlay.mat";

        /// <summary>
        /// 조준원 오버레이 머티리얼(전용 셰이더 기반)을 멱등 확보한다.
        /// 이미 에셋이 있으면 그대로 쓰고, 없으면 셰이더로 생성해 에셋으로 저장한다.
        /// 셰이더를 못 찾으면(임포트 전 등) 경고 후 null(폴백: SpriteRenderer 기본 머티리얼).
        /// </summary>
        private static Material EnsureOverlayMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(OverlayMaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find(OverlayShaderName);
            if (shader == null)
            {
                Debug.LogWarning(
                    $"[Skill Setup] 오버레이 셰이더('{OverlayShaderName}')를 찾지 못했습니다. " +
                    "조준원이 지형에 파묻힐 수 있습니다(기본 머티리얼 폴백). " +
                    "SkillAimOverlay.shader가 임포트됐는지 확인 후 이 메뉴를 다시 실행하세요.");
                return null;
            }

            // 머티리얼 폴더 보장.
            string dir = System.IO.Path.GetDirectoryName(OverlayMaterialPath);
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");

            Material mat = new Material(shader) { name = "SkillAimOverlay" };
            AssetDatabase.CreateAsset(mat, OverlayMaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Skill Setup] 조준원 오버레이 머티리얼 생성: {OverlayMaterialPath}");
            return mat;
        }

        /// <summary>
        /// 조준원 한 겹(SpriteRenderer 자식)을 이름으로 멱등 확보하고 스프라이트·오버레이 머티리얼을 물린다.
        /// </summary>
        private static SpriteRenderer EnsureReticlePart(Transform parent, string name, Sprite sprite, Material overlayMat)
        {
            Transform t = parent.Find(name);
            GameObject go = t != null ? t.gameObject : new GameObject(name);
            if (t == null) go.transform.SetParent(parent, false);
            if (!go.TryGetComponent(out SpriteRenderer sr)) sr = go.AddComponent<SpriteRenderer>();
            if (sprite != null) sr.sprite = sprite;
            // 지형 관통 표시용 오버레이 머티리얼(없으면 기본 머티리얼 유지 — 폴백).
            if (overlayMat != null) sr.sharedMaterial = overlayMat;
            return sr;
        }

        // 취소(X) 버튼 스프라이트 경로(정식 아트).
        private const string CancelSpritePath = "Assets/_Project/Sprites/UI/Buttons/ui_btn_cancel.png";

        /// <summary>
        /// 하단 중앙 X(취소) 이미지 버튼을 멱등 생성하고 RectTransform을 반환한다(규칙 20·21).
        /// ui_btn_cancel 스프라이트를 쓰는 "이미지 버튼"이며(텍스트 라벨 없음),
        /// 평소에는 숨겨두고(SetActive false) 조준 중에만 SkillAimController가 켠다.
        /// </summary>
        private static RectTransform BuildCancelButton(Transform canvas, TMP_FontAsset font)
        {
            // 잔여/중복 정리: 다른 부모(이전 버전이 잘못 붙인 위치)에 있는 SkillCancelButton을 모두 제거한다.
            //   재실행 시 정확히 1개만(이 부모 밑) 남도록 하여 "취소 버튼이 상시 떠 있는" 중복 잔재를 없앤다.
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "SkillCancelButton" && all[i].parent != canvas)
                    Object.DestroyImmediate(all[i].gameObject);
            }

            GameObject go = FindOrCreateChild(canvas, "SkillCancelButton");
            RectTransform rt = go.GetComponent<RectTransform>();
            // 하단 중앙(맨 끝보다 안쪽 — 엣지 스크롤 영역과 겹치지 않도록, 규칙 21).
            SetAnchors(rt, new Vector2(0.40f, 0.06f), new Vector2(0.60f, 0.14f));

            // 정식 취소 스프라이트 지정(이미지 버튼). 없으면 경고 후 단색 폴백.
            Sprite cancelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CancelSpritePath);
            if (cancelSprite == null)
                Debug.LogWarning($"[Skill Setup] 취소 버튼 스프라이트를 찾지 못했습니다({CancelSpritePath}). 단색으로 대체합니다.");

            Image img = EnsureImage(go, cancelSprite != null ? Color.white : new Color(0.5f, 0.2f, 0.2f, 0.9f), cancelSprite);
            img.type = Image.Type.Simple;   // 아이콘이므로 9-슬라이스 대신 단순 표시.
            img.preserveAspect = true;
            img.raycastTarget = true;

            if (!go.TryGetComponent(out Button btn)) btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            // 이전 버전이 만든 텍스트 라벨(자식)이 있으면 제거 — 이미지 버튼이므로 라벨 없음.
            Transform label = go.transform.Find("Label");
            if (label != null) Object.DestroyImmediate(label.gameObject);

            // 평소 숨김 — 조준 시작 시 SkillAimController가 켠다(규칙 20).
            go.SetActive(false);
            return rt;
        }

        /// <summary>
        /// SkillAimController를 멱등 생성하고 카메라/컨트롤러/조준원/취소영역을 배선한다.
        /// </summary>
        private static SkillAimController BuildAimController(SkillAimReticle reticle, RectTransform cancelZone)
        {
            GameObject go = GameObject.Find("SkillAimController");
            if (go == null) go = new GameObject("SkillAimController");

            if (!go.TryGetComponent(out SkillAimController aim))
                aim = go.AddComponent<SkillAimController>();

            CameraController camController = Object.FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
            Camera cam = camController != null ? camController.GetComponent<Camera>() : Camera.main;

            Connect(aim, "_camera", cam);
            Connect(aim, "_cameraController", camController);
            Connect(aim, "_reticle", reticle);
            Connect(aim, "_cancelZone", cancelZone);
            return aim;
        }

        // ====================================================================
        // C) NetworkSkillController 씬 오브젝트
        // ====================================================================

        /// <summary>
        /// NetworkSkillController(NetworkObject 포함)를 씬에 멱등 생성한다.
        /// NetworkUpgradeController와 동일하게 씬 오브젝트(자동 스폰)로 배치한다.
        /// </summary>
        private static NetworkSkillController BuildNetworkSkillController()
        {
            GameObject go = GameObject.Find("NetworkSkillController");
            if (go == null) go = new GameObject("NetworkSkillController");

            // NetworkObject(자동 스폰용). Unity가 GlobalObjectIdHash를 자동 부여한다.
            if (!go.TryGetComponent(out NetworkObject _)) go.AddComponent<NetworkObject>();

            if (!go.TryGetComponent(out NetworkSkillController net)) net = go.AddComponent<NetworkSkillController>();
            return net;
        }

        // ====================================================================
        // 공통 헬퍼(UI/씬)
        // ====================================================================

        /// <summary>
        /// 스킬 패널을 붙일 부모(다른 패널들이 형제로 있는 [UI] 루트)를 결정한다.
        ///
        /// 견고한 방식(BuildingActionPanel의 "부모"를 사용):
        ///   BuildingActionPanel은 자체 오버라이드 Canvas를 가진 독립 패널이므로 그 자신을 부모로 삼으면
        ///   중첩된다. 대신 BuildingActionPanel.transform.parent(= 다른 패널들과 같은 컨테이너, [UI] 루트)를
        ///   반환해 스킬 패널을 "형제"로 만든다. 다른 패널(ProductionPopup/GameEndPanel)이 놓인 위치와 동일.
        ///   폴백: 액션 패널이 없으면 씬의 첫 Canvas 트랜스폼.
        /// </summary>
        private static Transform ResolveSkillPanelParent()
        {
            var action = Object.FindFirstObjectByType<BuildingActionPanelUI>(FindObjectsInactive.Include);
            if (action != null && action.transform.parent != null)
                return action.transform.parent; // 다른 패널들과 같은 컨테이너(형제로 배치).

            // 폴백: 씬의 첫 Canvas([UI] 루트)를 부모로.
            Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            return canvas != null ? canvas.transform : null;
        }

        /// <summary>
        /// 씬에 이미 존재하는 BuildingSkillPanel을 찾는다(컴포넌트 우선, 없으면 이름 기반 폴백).
        /// 잘못된 부모에 만들어진 잔재를 정리·재부모화하기 위해 사용한다. 없으면 null.
        /// </summary>
        private static GameObject FindExistingSkillPanel()
        {
            var comp = Object.FindFirstObjectByType<BuildingSkillPanelUI>(FindObjectsInactive.Include);
            if (comp != null) return comp.gameObject;

            // 컴포넌트가 없는(잘못된) 잔재 대비 — 이름으로도 탐색.
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "BuildingSkillPanel")
                    return all[i].gameObject;
            }
            return null;
        }

        private static GameObject FindOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Unity 내장(빌트인) 스프라이트를 방어적으로 로드한다(임시 테스트 아트용).
        /// primaryPath가 없으면 fallbackPath를 시도하고, 둘 다 실패하면 경고 후 null을 반환한다.
        /// (에디터/Unity 버전에 따라 내장 리소스 경로가 다를 수 있으므로 실패해도 셋업은 계속 진행된다.)
        /// </summary>
        /// <param name="primaryPath">우선 시도할 내장 리소스 경로(예: "UI/Skin/UISprite.psd").</param>
        /// <param name="fallbackPath">실패 시 대체 경로(없으면 null 전달).</param>
        private static Sprite LoadBuiltinSprite(string primaryPath, string fallbackPath)
        {
            Sprite s = AssetDatabase.GetBuiltinExtraResource<Sprite>(primaryPath);
            if (s == null && !string.IsNullOrEmpty(fallbackPath))
                s = AssetDatabase.GetBuiltinExtraResource<Sprite>(fallbackPath);
            if (s == null)
                Debug.LogWarning(
                    $"[Skill Setup] 내장 스프라이트를 찾지 못했습니다('{primaryPath}'). " +
                    "임시 아트 없이 진행합니다(정식 아트로 교체 필요).");
            return s;
        }

        private static Image EnsureImage(GameObject go, Color color, Sprite sprite)
        {
            if (!go.TryGetComponent(out Image img)) img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            if (sprite != null) img.type = Image.Type.Sliced;
            return img;
        }

        private static TextMeshProUGUI EnsureText(
            GameObject go, TMP_FontAsset font, string text, int fontSize, TextAlignmentOptions align)
        {
            if (!go.TryGetComponent(out TextMeshProUGUI t)) t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        // ====================================================================
        // SerializedObject 배선
        // ====================================================================

        /// <summary> private [SerializeField] 오브젝트 참조 슬롯을 연결(멱등 — 이미 동일이면 no-op). </summary>
        private static void Connect(Component target, string fieldName, Object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[Skill Setup] {target.GetType().Name}에 '{fieldName}' 필드가 없습니다.");
                return;
            }
            if (prop.objectReferenceValue == value) return; // 멱등.
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        /// <summary> private [SerializeField] 리스트/배열 슬롯을 값 목록으로 채운다. </summary>
        private static void ConnectList(Component target, string fieldName, List<Object> values)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[Skill Setup] {target.GetType().Name}에 '{fieldName}' 필드가 없습니다.");
                return;
            }
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedProperties();
        }
    }
}
