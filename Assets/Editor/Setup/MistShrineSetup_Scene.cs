// ============================================================================
// MistShrineSetup_Scene.cs  (에디터 전용 · 1회성 셋업)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  1) 먼저  Hexiege > MistShrine > 1. Apply Config Values (SpecialAttackConfig)  실행. │
// │  2) 상단 메뉴  Hexiege > MistShrine > 2. Setup Scene (Panel, Range, Network)  실행.  │
// │     (Game 씬이 안 열려 있으면 스크립트가 자동으로 열고, 끝나면 저장한다.)              │
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 하는가(유니티 초급자 기준):
//   MistShrine(물안개 신전) 힐 시스템을 실기로 돌리는 데 필요한 "씬 골격"을 자동으로
//   만들고, 스크립트의 [SerializeField] 슬롯들을 손으로 끌어다 놓는 대신 코드로 연결한다.
//
//     A) MistShrinePanel  — 기존 BuildingActionPanel 을 "복제"해 룩·구조(3×3 그리드 9슬롯,
//        헤더/닫기[X]/철거 버튼, 오버라이드 Canvas SortingOrder 200)를 그대로 물려받는다.
//        슬롯 1(index 0)에 사용(시전) 버튼 구성요소를 얹는다:
//          · 쿨다운 오버레이(SkillCooldownOverlay — 시계방향 radial fill + 남은 초)
//          · 자동 모드 테두리 회전 오버레이(생산 패널이 쓰는 머티리얼 자산 재사용)
//          · 임시 텍스트 라벨(물안개 아이콘 미제작 — UI 규칙 15)
//     B) MistShrineRangeIndicator — 지도 바닥에 깔리는 회복 범위 원(채움 + 외곽선 2겹).
//        지면 데칼 셰이더(Hexiege/SkillAimOverlay) 기반 머티리얼을 멱등 생성해 물린다.
//     C) NetworkMistShrineController — 멀티플레이 시전/자동토글 중계용 씬 오브젝트
//        (NetworkObject 포함 → NetworkManager 가 씬 오브젝트로 자동 스폰).
//     D) GameBootstrapper 슬롯 배선: _mistShrinePanelUI / _networkMistShrineController.
//
// 멱등성(여러 번 실행해도 안전):
//   · 패널은 기존 MistShrinePanel 을 먼저 제거하고 원본에서 다시 복제한다(항상 정확히 1개).
//   · 그 외 오브젝트는 "이름/컴포넌트로 찾아 재사용"한다(FindOrCreate 패턴). 중복 생성 없음.
//   · 참조 배선(Connect)은 이미 같은 값이면 아무것도 쓰지 않는다.
//
// 이름 기반 자동 매칭 주의(과거 사고 교훈):
//   예전에 "이름이 비슷한 다른 버튼"이 잘못 연결된 사고가 있었다(_backButton ↔ OffButton).
//   그래서 이 스크립트는 (1) 컴포넌트 타입으로 먼저 찾고, (2) 이름 탐색은 폴백으로만 쓰며,
//   (3) 못 찾은 경우 **조용히 넘어가지 않고 반드시 경고/에러를 남긴다.**
//
// 이 스크립트가 하지 않는 것(사용자 후속):
//   · 정식 아트(물안개 아이콘 / 범위 원 스프라이트 / 쿨다운 fill 스프라이트) 지정 — 임시 내장 스프라이트 사용.
//   · 밸런싱 수치 확정(→ SpecialAttackConfig.asset 한 파일만 수정).
//   · 롱프레스 EventTrigger 부착 — 이것은 **런타임에 MistShrinePanelUI 코드가 자동으로** 붙인다.
//   · 미세 레이아웃/앵커 튜닝(모바일 9:16), 범위 원 색·두께 튜닝(UI 규칙 12 — 미확정).
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
    /// MistShrine 패널·범위 표시·네트워크 오브젝트를 씬에 생성하고 GameBootstrapper 에 배선하는 1회성 에디터 스크립트.
    /// </summary>
    public static class MistShrineSetup_Scene
    {
        // ── 경로 상수 ──────────────────────────────────────────────────────
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string ColorConfigPath = "Assets/_Project/Resources/Config/UIColorConfig.asset";
        private const string SpecialAttackConfigPath = "Assets/_Project/Resources/Config/SpecialAttackConfig.asset";
        private const string BoldFontPath = "Assets/_Project/Fonts/Maplestory Bold SDF.asset";

        /// <summary>자동 모드 테두리 회전 머티리얼 — 생산 패널(ProductionPanelUI)이 쓰는 것과 **같은 자산을 재사용**한다(UI 규칙 6).</summary>
        private const string RotatingBorderMaterialPath = "Assets/_Project/Materials/UI/mat_ui_rotatingborder.mat";

        // 범위 원 오버레이 셰이더/머티리얼(지면 데칼). SkillAimReticle 과 같은 셰이더를 쓰되 머티리얼은 따로 둔다.
        private const string OverlayShaderName = "Hexiege/SkillAimOverlay";
        private const string RangeOverlayMaterialPath = "Assets/_Project/Materials/MistShrineRangeOverlay.mat";

        // ── 씬 오브젝트 이름 ───────────────────────────────────────────────
        private const string PanelObjectName = "MistShrinePanel";
        private const string RangeIndicatorObjectName = "MistShrineRangeIndicator";
        private const string NetworkControllerObjectName = "NetworkMistShrineController";

        /// <summary>사용(시전) 버튼이 놓이는 슬롯 인덱스. 슬롯 1 = index 0 (UI 규칙 10).</summary>
        private const int UseSlotIndex = 0;

        /// <summary>철거 버튼이 놓이는 슬롯 인덱스. 슬롯 6 = index 5 (베이스가 제공 — UI 규칙 10).</summary>
        private const int DemolishSlotIndex = 5;

        /// <summary>3×3 그리드 슬롯 개수(공통 규격).</summary>
        private const int SlotCount = 9;

        // ====================================================================
        // 엔트리 포인트
        // ====================================================================

        [MenuItem("Hexiege/MistShrine/2. Setup Scene (Panel, Range, Network)")]
        public static void Run()
        {
            // ── 0) Game 씬 + GameBootstrapper 확보 ─────────────────────────
            bool sceneOpenedByThis = false;
            GameBootstrapper bootstrapper = Object.FindFirstObjectByType<GameBootstrapper>(FindObjectsInactive.Include);
            if (bootstrapper == null)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
                {
                    Debug.LogError($"[MistShrine Setup] Game 씬을 찾지 못했습니다: {GameScenePath}");
                    return;
                }
                // 저장 안 된 변경이 있으면 먼저 물어본다(사용자 작업 유실 방지).
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.LogWarning("[MistShrine Setup] 씬 열기가 취소되었습니다.");
                    return;
                }
                EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
                sceneOpenedByThis = true;
                bootstrapper = Object.FindFirstObjectByType<GameBootstrapper>(FindObjectsInactive.Include);
            }
            if (bootstrapper == null)
            {
                Debug.LogError("[MistShrine Setup] GameBootstrapper 를 씬에서 찾지 못했습니다.");
                return;
            }
            Scene scene = bootstrapper.gameObject.scene;

            // ── 공용 에셋 로드(없어도 진행 — 폴백/플레이스홀더) ────────────
            TMP_FontAsset boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            UIColorConfig colorConfig = AssetDatabase.LoadAssetAtPath<UIColorConfig>(ColorConfigPath);

            // 자동 모드 테두리 머티리얼 — 생산 패널과 같은 자산(UI 규칙 6 "재사용").
            Material borderMaterial = AssetDatabase.LoadAssetAtPath<Material>(RotatingBorderMaterialPath);
            if (borderMaterial == null)
            {
                Debug.LogWarning(
                    $"[MistShrine Setup] 테두리 회전 머티리얼을 찾지 못했습니다: {RotatingBorderMaterialPath}\n" +
                    "  → 자동 모드 표시가 '보이지 않는 상태'로 셋업됩니다(배선 자체는 진행). " +
                    "생산 패널(ProductionPanelUI)의 _autoProductionMaterial 이 가리키는 자산 경로를 확인하세요.");
            }

            // 범위 반경(_rangeRadius)은 회복 판정 반경과 반드시 같아야 하므로 설정 에셋에서 읽어 온다.
            var specialAttackConfig = AssetDatabase.LoadAssetAtPath<SpecialAttackConfig>(SpecialAttackConfigPath);
            if (specialAttackConfig == null)
            {
                Debug.LogWarning(
                    $"[MistShrine Setup] SpecialAttackConfig 를 찾지 못했습니다: {SpecialAttackConfigPath}\n" +
                    "  → 패널의 _rangeRadius 는 현재 값(코드 기본값 3)을 그대로 둡니다. " +
                    "먼저 메뉴 'Hexiege/MistShrine/1. Apply Config Values' 를 실행하는 것을 권장합니다.");
            }

            // ── UI 부모 결정 ───────────────────────────────────────────────
            Transform panelParent = ResolvePanelParent();
            if (panelParent == null)
            {
                Debug.LogError("[MistShrine Setup] 패널을 붙일 부모(UI 루트)를 찾지 못했습니다. 씬에 Canvas/패널이 있는지 확인하세요.");
                return;
            }

            // ── A) 패널 생성/배선 ──────────────────────────────────────────
            MistShrinePanelUI panel = BuildPanel(panelParent, boldFont, colorConfig, borderMaterial);
            if (panel == null) return; // 실패 사유는 BuildPanel 내부에서 이미 로그로 남겼다.

            // ── B) 회복 범위 표시 오브젝트 ─────────────────────────────────
            MistShrineRangeIndicator indicator = BuildRangeIndicator();
            Connect(panel, "_rangeIndicator", indicator);
            if (specialAttackConfig != null)
                ConnectFloat(panel, "_rangeRadius", specialAttackConfig.MistRadius);

            // ── C) NetworkMistShrineController 씬 오브젝트 ─────────────────
            NetworkMistShrineController netMist = BuildNetworkController();

            // ── D) GameBootstrapper 배선 ───────────────────────────────────
            Connect(bootstrapper, "_mistShrinePanelUI", panel);
            Connect(bootstrapper, "_networkMistShrineController", netMist);

            // ── 저장 처리 ──────────────────────────────────────────────────
            //   ⚠️ SetDirty / MarkSceneDirty 를 빠뜨리면 값이 씬 파일에 저장되지 않는다(과거 교훈).
            EditorUtility.SetDirty(bootstrapper);
            EditorUtility.SetDirty(panel);
            if (indicator != null) EditorUtility.SetDirty(indicator);
            if (netMist != null) EditorUtility.SetDirty(netMist);
            EditorSceneManager.MarkSceneDirty(scene);
            if (sceneOpenedByThis) EditorSceneManager.SaveScene(scene);

            Debug.Log(
                "[MistShrine Setup] 씬 골격 생성·배선 완료(플레이스홀더 아트).\n" +
                "  · MistShrinePanel(= BuildingActionPanel 복제, [UI] 루트 형제·오버라이드 Canvas SO 200) 생성 — 슬롯1=사용 / 슬롯6=철거 / 나머지는 alpha 0 숨김.\n" +
                "  · 슬롯1에 CooldownOverlay(Radial360·Origin Top) + BorderOverlay(테두리 회전 머티리얼 재사용, 오브젝트 1개) + 임시 라벨 생성·배선.\n" +
                "  · MistShrineRangeIndicator(Ring/Fill 2겹 + 지면 데칼 머티리얼) 생성·배선.\n" +
                "  · NetworkMistShrineController(NetworkObject 포함, 씬 오브젝트 자동 스폰) 생성.\n" +
                "  · GameBootstrapper 슬롯 2종 연결(_mistShrinePanelUI / _networkMistShrineController).\n" +
                (sceneOpenedByThis ? "  · 씬 자동 저장 완료.\n" : "  · 열린 씬 dirty 표시 — Ctrl+S 로 저장하세요.\n") +
                "  · 후속: 물안개 아이콘/범위 원 정식 아트 교체, 범위 원 색·두께 튜닝(UI 규칙 12 미확정), 밸런싱 수치 확정(SpecialAttackConfig.asset).");

            Selection.activeObject = panel.gameObject;
        }

        // ====================================================================
        // A) MistShrine 패널
        // ====================================================================

        /// <summary>
        /// MistShrinePanel 을 "기존 BuildingActionPanel 복제 기반"으로 만든다(룩·구조 그대로).
        ///
        /// 왜 복제인가:
        ///   처음부터 하드코딩으로 그리면 기존 패널과 룩이 전혀 맞지 않는다. 씬의 실제
        ///   BuildingActionPanel GameObject 를 통째로 Instantiate 로 복제하면 배경/크기/앵커/헤더/
        ///   닫기[X]/철거/AnimatedPanel/오버라이드 Canvas(SortingOrder 200)/GraphicRaycaster/CanvasGroup
        ///   을 전부 그대로 물려받는다. Canvas SortingOrder 규칙(GameSystemRules_CanvasSortingOrder.md
        ///   "패널 = 200")도 복제만으로 자동 충족된다.
        ///   Unity 의 Instantiate 는 계층 내부 참조(_popup/_headerText/_allSlotButtons 등)를 복제본
        ///   자식으로 자동 remap 하므로, 그 값을 읽어 MistShrinePanelUI 에 그대로 옮겨 배선한다.
        ///
        /// 슬롯 매핑(UI 규칙 10 — 3×3 그리드 9슬롯 규격 그대로):
        ///   index 0 = 사용(시전) / index 1~4 = 예약(숨김) / index 5 = 철거(기존 버튼 그대로) / 6~8 = 예약(숨김).
        ///
        /// 멱등: 재실행 시 기존 MistShrinePanel 을 먼저 제거하고 새로 복제한다(정확히 1개).
        /// </summary>
        /// <param name="panelParent">패널을 붙일 부모([UI] 루트 — 다른 패널들과 형제가 되도록).</param>
        /// <param name="font">임시 라벨/남은초 텍스트에 쓸 TMP 폰트(없으면 TMP 기본).</param>
        /// <param name="colorConfig">공용 UI 색상 설정 에셋(없으면 복제본 참조 유지).</param>
        /// <param name="borderMaterial">자동 모드 테두리 회전 머티리얼(없으면 미지정 — 경고는 호출부에서).</param>
        /// <returns>배선 완료된 패널 컴포넌트. 실패 시 null.</returns>
        private static MistShrinePanelUI BuildPanel(
            Transform panelParent, TMP_FontAsset font, UIColorConfig colorConfig, Material borderMaterial)
        {
            // ── 0) 복제 원본(BuildingActionPanel) 확보 ─────────────────────
            //   ⚠️ 이전 실행이 중간에 실패해 "MistShrinePanel"이라는 이름의 복제본이
            //      BuildingActionPanelUI 를 그대로 달고 남아 있을 수 있다. 그것을 원본으로 삼으면
            //      "복제본의 복제본"이 만들어져 구조가 어긋나므로, 이름으로 걸러 진짜 원본만 고른다.
            BuildingActionPanelUI srcAction = null;
            var actionPanels = Object.FindObjectsByType<BuildingActionPanelUI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < actionPanels.Length; i++)
            {
                if (actionPanels[i] == null) continue;
                if (actionPanels[i].gameObject.name == PanelObjectName) continue; // 실패 잔재는 원본이 아니다.
                srcAction = actionPanels[i];
                break;
            }
            if (srcAction == null)
            {
                Debug.LogError("[MistShrine Setup] 복제 원본 BuildingActionPanel(BuildingActionPanelUI)을 씬에서 찾지 못했습니다.");
                return null;
            }

            // ── 1) 기존 MistShrinePanel 제거 → 정확히 1개 보장(멱등) ────────
            GameObject existing = FindExistingPanel();
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
                Debug.Log("[MistShrine Setup] 기존 MistShrinePanel 을 제거하고 BuildingActionPanel 복제로 재생성합니다.");
            }

            // ── 2) 복제(로컬 rect 보존 — instantiateInWorldSpace=false) ─────
            GameObject clone = Object.Instantiate(srcAction.gameObject, panelParent, false);
            clone.name = PanelObjectName;

            // ── 3) 복제본의 액션 패널 컴포넌트에서 remap 된 참조 읽기 ────────
            var cloneAction = clone.GetComponent<BuildingActionPanelUI>();
            if (cloneAction == null)
            {
                Debug.LogError("[MistShrine Setup] 복제본에 BuildingActionPanelUI 가 없습니다(원본 구조 확인 필요).");
                Object.DestroyImmediate(clone);
                return null;
            }

            var srcSo = new SerializedObject(cloneAction);
            Object popupRef = GetRef(srcSo, "_popup");
            Object headerRef = GetRef(srcSo, "_headerText");
            Object cancelRef = GetRef(srcSo, "_cancelButton");
            Object demolishRef = GetRef(srcSo, "_demolishButton");
            Object refundRef = GetRef(srcSo, "_demolishRefundText");
            Object colorRef = GetRef(srcSo, "_colorConfig");

            // _allSlotButtons(9) 읽기 — MistShrine 패널도 같은 9슬롯 규격을 그대로 쓴다.
            var allSlots = new List<Button>(SlotCount);
            SerializedProperty slotsProp = srcSo.FindProperty("_allSlotButtons");
            if (slotsProp != null)
            {
                for (int i = 0; i < slotsProp.arraySize; i++)
                    allSlots.Add(slotsProp.GetArrayElementAtIndex(i).objectReferenceValue as Button);
            }
            if (allSlots.Count != SlotCount)
            {
                // 조용히 넘어가지 않는다 — 슬롯 개수가 다르면 사용/철거 위치가 어긋난다.
                Debug.LogWarning(
                    $"[MistShrine Setup] 원본 패널의 슬롯 개수가 {allSlots.Count}개입니다(기대 {SlotCount}개). " +
                    "슬롯 배치(1=사용 / 6=철거)가 어긋날 수 있으니 Inspector 에서 확인하세요.");
            }

            // ── 4) 액션 컴포넌트 제거 → MistShrine 컴포넌트 추가 ────────────
            Object.DestroyImmediate(cloneAction);
            var panel = clone.AddComponent<MistShrinePanelUI>();

            // ── 5) 베이스(BuildingPanelBase) 필드 배선 — E2 ─────────────────
            //   전부 protected 라 자식(MistShrinePanelUI) Inspector 에도 같은 슬롯이 노출된다.
            Connect(panel, "_popup", popupRef);
            Connect(panel, "_headerText", headerRef);
            Connect(panel, "_cancelButton", cancelRef);
            Connect(panel, "_demolishButton", demolishRef);
            Connect(panel, "_demolishRefundText", refundRef);
            Connect(panel, "_colorConfig", colorConfig != null ? colorConfig : colorRef);

            // ── 6) 슬롯 리스트 배선 ────────────────────────────────────────
            var slotObjects = new List<Object>(allSlots.Count);
            for (int i = 0; i < allSlots.Count; i++) slotObjects.Add(allSlots[i]);
            ConnectList(panel, "_allSlotButtons", slotObjects);

            // ── 7) 슬롯 가시성 초기화(UI 규칙 11 — SetActive(false) 금지) ───
            //   런타임에도 MistShrinePanelUI.ApplySlotVisibility 가 같은 규칙으로 다시 맞추지만,
            //   에디터에서 씬을 열었을 때의 모습도 실제와 같도록 여기서 미리 적용해 둔다.
            ApplyInitialSlotVisibility(allSlots);

            // ── 8) 슬롯 1(사용 버튼) 구성 — E3·E4 ──────────────────────────
            Button useButton = (allSlots.Count > UseSlotIndex) ? allSlots[UseSlotIndex] : null;
            if (useButton == null)
            {
                Debug.LogError(
                    $"[MistShrine Setup] 슬롯 {UseSlotIndex + 1}(사용 버튼)을 찾지 못했습니다. " +
                    "원본 BuildingActionPanel 의 _allSlotButtons 배선을 확인하세요.");
                return panel; // 패널 자체는 살려 두고(철거 등은 동작) 사용 버튼만 미배선 상태로 남긴다.
            }

            Transform useSlot = useButton.transform;

            // 비용/골드 표시는 MistShrine 에 없다 → "레이아웃은 남기고 화면에서만" 숨긴다.
            //   ⚠️ SetActive(false) 로 빼면 그 슬롯만 preferred 높이가 줄어 버튼 크기가 불균일해진다
            //      (스킬 패널에서 실제로 겪은 버그 — SkillSetup_Scene 참조).
            HideChildKeepLayout(useSlot, "CostContainer");

            // 아이콘 자리는 유지하되 스프라이트가 없으므로 꺼 둔다(정식 아이콘 미제작 — UI 규칙 15).
            Image icon = FindImageChild(useSlot, "IconImage");
            if (icon == null)
            {
                Debug.LogWarning(
                    "[MistShrine Setup] 슬롯1에서 'IconImage' 자식을 찾지 못했습니다. " +
                    "아이콘 자리 없이 진행합니다(임시 텍스트 라벨만 표시).");
            }
            else
            {
                icon.raycastTarget = false; // 버튼 클릭을 가리지 않도록.
                icon.sprite = null;
                icon.enabled = false;       // 정식 아이콘이 생기면 켠다.
            }

            // (E4) 자동 모드 테두리 오버레이 — 오브젝트는 **하나만**(UI 규칙 14).
            //      생산 패널처럼 GameObject 리스트 + Image 리스트로 이중 배선하지 않는다.
            Image autoBorder = BuildAutoBorderOverlay(useSlot, borderMaterial);

            // (E3) 쿨다운 오버레이 — 테두리보다 위에 그려지도록 나중에 만든다.
            SkillCooldownOverlay cooldown = BuildCooldownOverlay(useSlot, font);

            // 임시 라벨(물안개) — 가장 위에 읽히도록 마지막 형제로 둔다. 정식 아이콘 도입 시 제거.
            TextMeshProUGUI useLabel = BuildUseLabel(useSlot, font);

            Connect(panel, "_useButton", useButton);
            Connect(panel, "_useCooldownOverlay", cooldown);
            Connect(panel, "_autoBorderOverlay", autoBorder);
            Connect(panel, "_useLabel", useLabel);

            return panel;
        }

        /// <summary>
        /// 슬롯 9개의 초기 가시성을 UI 규칙 10·11 대로 맞춘다.
        /// 슬롯 1(index 0)·슬롯 6(index 5)만 보이고, 나머지는 CanvasGroup.alpha=0 으로 숨긴다.
        /// ⚠️ SetActive(false) 는 GridLayout 셀 정렬을 무너뜨리므로 절대 쓰지 않는다.
        /// </summary>
        /// <param name="slots">패널의 슬롯 버튼 9개.</param>
        private static void ApplyInitialSlotVisibility(List<Button> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                Button slot = slots[i];
                if (slot == null) continue;

                GameObject go = slot.gameObject;
                if (!go.activeSelf) go.SetActive(true); // 이전 셋업이 꺼 놨을 수 있으므로 되살린다.

                if (!go.TryGetComponent(out CanvasGroup cg)) cg = go.AddComponent<CanvasGroup>();

                bool visible = (i == UseSlotIndex) || (i == DemolishSlotIndex);
                cg.alpha = visible ? 1f : 0f;
                cg.interactable = visible;
                cg.blocksRaycasts = visible;
            }
        }

        /// <summary>
        /// 사용 버튼 위 쿨다운 오버레이(SkillCooldownOverlay)를 멱등 생성·배선한다(E3).
        /// 시계방향 radial fill 이미지 + 남은초 TMP + CanvasGroup 3종을 구성한다.
        /// </summary>
        /// <param name="slot">오버레이를 붙일 사용 버튼(슬롯 1)의 Transform.</param>
        /// <param name="font">남은 초 텍스트에 쓸 TMP 폰트(없으면 TMP 기본).</param>
        /// <returns>배선 완료된 오버레이 컴포넌트.</returns>
        private static SkillCooldownOverlay BuildCooldownOverlay(Transform slot, TMP_FontAsset font)
        {
            GameObject overlayGo = FindOrCreateChild(slot, "CooldownOverlay");
            SetStretch(overlayGo.GetComponent<RectTransform>());
            overlayGo.transform.SetAsLastSibling();

            // 슬롯 버튼에 LayoutGroup(아이콘+비용 배치용)이 있으면 자식인 오버레이도 눌려 한 줄로 찌그러진다.
            //   ignoreLayout=true 로 레이아웃에서 빼야 위 SetStretch(앵커 0~1)가 그대로 적용되어
            //   오버레이가 "버튼 전체"를 덮는다.
            if (!overlayGo.TryGetComponent(out LayoutElement le)) le = overlayGo.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            // 오버레이 전체를 켜고 끄는 CanvasGroup.
            if (!overlayGo.TryGetComponent(out CanvasGroup cg)) cg = overlayGo.AddComponent<CanvasGroup>();
            cg.alpha = 0f;              // 유휴 시 완전히 숨김(쿨다운 중에만 표시 — UI 규칙 13).
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // 시계방향 radial fill 이미지(반투명 검정).
            GameObject fillGo = FindOrCreateChild(overlayGo.transform, "Fill");
            SetStretch(fillGo.GetComponent<RectTransform>());
            Image fill = EnsureImage(fillGo, new Color(0f, 0f, 0f, 0.6f), null);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;   // 12시 방향에서 시작.
            // ⚠️ "어두운 오버레이가 12시부터 시계방향으로 걷혀 사라지는" 표준 쿨다운 스윕 방향 설정.
            //   Unity Filled 이미지는 [fillOrigin 에서 fillClockwise 방향으로 fillAmount 만큼]을 "보이게(어둡게)" 그린다.
            //   SkillCooldownOverlay.SetCooldown 이 fillAmount = remaining/total(남은 비율 = 어두운 비율)로 줄이므로,
            //   fillClockwise=false(반시계로 그림)여야 밝아지는 경계가 12시부터 "시계방향"으로 전진한다.
            //   (fillClockwise=true 로 두면 반대로 걷힌다 — 스킬 패널에서 실제로 났던 버그. SkillSetup_Scene 과 동일 처리.)
            fill.fillClockwise = false;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;
            // Filled 타입 Image 는 스프라이트가 없으면 아무것도 그리지 않으므로 Unity 내장 스프라이트를 임시로 넣는다.
            Sprite cooldownSprite = LoadBuiltinSprite("UI/Skin/UISprite.psd", "UI/Skin/Background.psd");
            if (cooldownSprite != null) fill.sprite = cooldownSprite;

            // 남은 초 숫자.
            GameObject textGo = FindOrCreateChild(overlayGo.transform, "Remaining");
            SetStretch(textGo.GetComponent<RectTransform>());
            TextMeshProUGUI remaining = EnsureText(textGo, font, string.Empty, 28, TextAlignmentOptions.Center);

            if (!overlayGo.TryGetComponent(out SkillCooldownOverlay overlay))
                overlay = overlayGo.AddComponent<SkillCooldownOverlay>();

            Connect(overlay, "_fillImage", fill);
            Connect(overlay, "_remainingText", remaining);
            Connect(overlay, "_canvasGroup", cg);
            EditorUtility.SetDirty(overlay);

            return overlay;
        }

        /// <summary>
        /// 자동 모드 테두리 회전 오버레이를 멱등 생성한다(E4 / UI 규칙 6·14).
        ///
        /// ⚠️ 오브젝트는 **하나만** 만든다.
        ///   생산 패널(ProductionPanelUI)은 같은 BorderOverlay 오브젝트를
        ///   _unitAutoIndicators(List&lt;GameObject&gt;)와 _unitBorderOverlays(List&lt;Image&gt;) 양쪽에
        ///   중복 배선해 SetActive 와 CanvasGroup.alpha 두 경로로 제어한다(과거 설계 잔재).
        ///   MistShrine 패널은 이 구조를 복제하지 않고, Image 하나만 _autoBorderOverlay 에 배선한다.
        ///   표시 제어는 런타임에 MistShrinePanelUI.ApplyAutoBorder(CanvasGroup.alpha) 단일 경로로만 한다.
        /// </summary>
        /// <param name="slot">테두리를 붙일 사용 버튼(슬롯 1)의 Transform.</param>
        /// <param name="borderMaterial">생산 패널과 공유하는 테두리 회전 머티리얼(null 가능 — 그 경우 미지정).</param>
        /// <returns>배선할 Image 컴포넌트.</returns>
        private static Image BuildAutoBorderOverlay(Transform slot, Material borderMaterial)
        {
            GameObject borderGo = FindOrCreateChild(slot, "BorderOverlay");
            SetStretch(borderGo.GetComponent<RectTransform>());
            borderGo.transform.SetAsLastSibling();

            // 쿨다운 오버레이와 같은 이유로 레이아웃에서 제외(버튼 전체를 덮게 하려고).
            if (!borderGo.TryGetComponent(out LayoutElement le)) le = borderGo.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            // 테두리는 셰이더가 직접 그리므로 스프라이트가 필요 없다(생산 패널 BorderOverlay 와 동일 구성).
            Image img = EnsureImage(borderGo, Color.white, null);
            img.type = Image.Type.Simple;
            img.raycastTarget = false;      // 장식이므로 버튼 입력을 절대 가로채지 않는다.
            if (borderMaterial != null) img.material = borderMaterial;

            // 표시 제어용 CanvasGroup(런타임 단일 경로). 기본은 꺼진 상태 — 자동 모드 기본값 OFF(UI 규칙 6).
            if (!borderGo.TryGetComponent(out CanvasGroup cg)) cg = borderGo.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            return img;
        }

        /// <summary>
        /// ⚠️ 임시 라벨 — 물안개 아이콘이 아직 없어서 버튼에 글자로 표시한다(UI 규칙 15).
        /// 정식 아이콘 도입 시 이 메서드와 호출부, 생성된 "UseLabel" 오브젝트, 패널의 _useLabel 필드를 함께 제거한다.
        /// 실제 표시 문자열("물안개")은 런타임에 MistShrinePanelUI.OnShow 가 채운다.
        /// </summary>
        /// <param name="slot">라벨을 붙일 사용 버튼(슬롯 1)의 Transform.</param>
        /// <param name="font">사용할 TMP 폰트(없으면 TMP 기본 폰트).</param>
        /// <returns>배선용 TMP 라벨 컴포넌트.</returns>
        private static TextMeshProUGUI BuildUseLabel(Transform slot, TMP_FontAsset font)
        {
            GameObject labelGo = FindOrCreateChild(slot, "UseLabel");
            SetStretch(labelGo.GetComponent<RectTransform>());

            if (!labelGo.TryGetComponent(out LayoutElement le)) le = labelGo.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            TextMeshProUGUI label = EnsureText(labelGo, font, string.Empty, 24, TextAlignmentOptions.Center);

            // 아이콘·테두리·쿨다운 오버레이보다 뒤(마지막 형제)에 두어 항상 위에 읽히게 한다.
            labelGo.transform.SetAsLastSibling();
            return label;
        }

        // ====================================================================
        // B) 회복 범위 표시 오브젝트
        // ====================================================================

        /// <summary>
        /// MistShrineRangeIndicator(회복 범위 원)를 월드 오브젝트로 멱등 생성한다(E5).
        /// 루트를 X축 90도로 눕혀 지도(XZ 평면)에 깔고, 자식 SpriteRenderer 2겹(Ring/Fill)을 배선한다.
        /// 색·두께·크기는 컴포넌트의 Inspector 값을 따른다(UI 규칙 12 — 아직 미확정 임시값).
        ///
        /// ⚠️ 이 GameObject 를 에디터에서 비활성(SetActive(false))으로 두면 안 된다.
        ///   MistShrineRangeIndicator.Awake 가 "시작 시 숨김"을 수행하는데, 비활성 오브젝트는
        ///   Awake 가 실행되지 않는다. 그러면 런타임에 Show() 가 SetActive(true) 를 호출하는 그 순간
        ///   Awake 가 뒤늦게 돌면서 스스로를 다시 꺼 버려 원이 영영 보이지 않는다.
        ///   → 씬에는 **활성 상태로** 저장하고, 숨김은 Awake 가 처리하게 둔다.
        /// </summary>
        /// <returns>배선 완료된 범위 표시 컴포넌트.</returns>
        private static MistShrineRangeIndicator BuildRangeIndicator()
        {
            GameObject root = FindSceneObject<MistShrineRangeIndicator>(RangeIndicatorObjectName);
            if (root == null) root = new GameObject(RangeIndicatorObjectName);
            root.name = RangeIndicatorObjectName;

            // 지도(XZ 평면)에 눕도록 X축 90도 회전(컴포넌트도 Awake/Show 에서 보장하지만 에디터에서도 눕혀 둔다).
            root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            if (!root.activeSelf) root.SetActive(true); // 위 ⚠️ 주석 참조 — 반드시 활성 상태로 남긴다.

            // 임시 내장 스프라이트(채워진 원) — 정식 아트 전에도 화면에 실제로 보이게 한다.
            Sprite circle = LoadBuiltinSprite("UI/Skin/Knob.psd", "UI/Skin/UISprite.psd");

            // 지형에 파묻히지 않는 지면 데칼 머티리얼(전용 셰이더). 없으면 경고 후 null 폴백.
            Material overlayMat = EnsureRangeOverlayMaterial();

            SpriteRenderer ring = EnsureCirclePart(root.transform, "Ring", circle, overlayMat);
            SpriteRenderer fill = EnsureCirclePart(root.transform, "Fill", circle, overlayMat);

            if (!root.TryGetComponent(out MistShrineRangeIndicator indicator))
                indicator = root.AddComponent<MistShrineRangeIndicator>();

            Connect(indicator, "_ringRenderer", ring);
            Connect(indicator, "_fillRenderer", fill);
            Connect(indicator, "_overlayMaterial", overlayMat);
            return indicator;
        }

        /// <summary>
        /// 범위 원용 지면 데칼 머티리얼을 멱등 확보한다(SkillSetup_Scene.EnsureOverlayMaterial 과 동일 패턴).
        /// 이미 에셋이 있으면 그대로 쓰고, 없으면 셰이더로 생성해 에셋으로 저장한다.
        /// 셰이더를 못 찾으면(임포트 전 등) 경고 후 null 을 반환한다(폴백: SpriteRenderer 기본 머티리얼).
        ///
        /// 셰이더는 스킬 조준원과 같은 Hexiege/SkillAimOverlay 를 쓴다.
        ///   ZTest LEqual + Offset -1,-1 + ZWrite Off 구성이라 지면 z-fighting 은 이기되
        ///   유닛·건물에는 가려진다. **ZTest Always 는 금지**다(Buildings 규칙 27).
        /// 머티리얼 파일만 따로 두는 이유: 나중에 범위 원의 색/투명도를 조준원과 별개로 튜닝할 수 있게 하기 위함이다.
        /// </summary>
        /// <returns>확보한 머티리얼(셰이더 부재 시 null).</returns>
        private static Material EnsureRangeOverlayMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(RangeOverlayMaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find(OverlayShaderName);
            if (shader == null)
            {
                Debug.LogWarning(
                    $"[MistShrine Setup] 지면 데칼 셰이더('{OverlayShaderName}')를 찾지 못했습니다. " +
                    "범위 원이 지형에 파묻힐 수 있습니다(기본 머티리얼 폴백). " +
                    "SkillAimOverlay.shader 가 임포트됐는지 확인 후 이 메뉴를 다시 실행하세요.");
                return null;
            }

            // 머티리얼 폴더 보장.
            string dir = System.IO.Path.GetDirectoryName(RangeOverlayMaterialPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");

            var mat = new Material(shader) { name = "MistShrineRangeOverlay" };
            AssetDatabase.CreateAsset(mat, RangeOverlayMaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MistShrine Setup] 범위 원 오버레이 머티리얼 생성: {RangeOverlayMaterialPath}");
            return mat;
        }

        /// <summary>
        /// 범위 원 한 겹(SpriteRenderer 자식)을 이름으로 멱등 확보하고 스프라이트·오버레이 머티리얼을 물린다.
        /// </summary>
        /// <param name="parent">범위 원 루트 Transform.</param>
        /// <param name="name">자식 이름("Ring" 또는 "Fill").</param>
        /// <param name="sprite">쓸 원 스프라이트(null 이면 기존 유지).</param>
        /// <param name="overlayMat">지면 데칼 머티리얼(null 이면 기본 머티리얼 유지 — 폴백).</param>
        /// <returns>확보한 SpriteRenderer.</returns>
        private static SpriteRenderer EnsureCirclePart(Transform parent, string name, Sprite sprite, Material overlayMat)
        {
            Transform t = parent.Find(name);
            GameObject go = t != null ? t.gameObject : new GameObject(name);
            if (t == null) go.transform.SetParent(parent, false);
            if (!go.activeSelf) go.SetActive(true);

            if (!go.TryGetComponent(out SpriteRenderer sr)) sr = go.AddComponent<SpriteRenderer>();
            if (sprite != null) sr.sprite = sprite;
            if (overlayMat != null) sr.sharedMaterial = overlayMat;
            return sr;
        }

        // ====================================================================
        // C) NetworkMistShrineController 씬 오브젝트
        // ====================================================================

        /// <summary>
        /// NetworkMistShrineController(NetworkObject 포함)를 씬에 멱등 생성한다(E6).
        /// NetworkSkillController / NetworkUpgradeController 와 동일하게 **씬 오브젝트**로 두면
        /// NetworkManager 가 게임 시작 시 자동으로 스폰한다(프리팹 등록 불필요).
        /// GlobalObjectIdHash 는 Unity 가 자동 부여한다.
        /// </summary>
        /// <returns>확보한 컨트롤러 컴포넌트.</returns>
        private static NetworkMistShrineController BuildNetworkController()
        {
            GameObject go = FindSceneObject<NetworkMistShrineController>(NetworkControllerObjectName);
            if (go == null) go = new GameObject(NetworkControllerObjectName);
            go.name = NetworkControllerObjectName;
            if (!go.activeSelf) go.SetActive(true);

            // NetworkObject 가 있어야 씬 오브젝트 자동 스폰 대상이 된다.
            if (!go.TryGetComponent(out NetworkObject _)) go.AddComponent<NetworkObject>();

            if (!go.TryGetComponent(out NetworkMistShrineController net))
                net = go.AddComponent<NetworkMistShrineController>();
            return net;
        }

        // ====================================================================
        // 탐색 헬퍼
        // ====================================================================

        /// <summary>
        /// MistShrine 패널을 붙일 부모를 결정한다.
        ///
        /// BuildingActionPanel 의 "부모"([UI] 루트 = 다른 패널들이 형제로 놓인 컨테이너)를 쓴다.
        ///   BuildingActionPanel 자신을 부모로 삼으면 그 안에 중첩되는데, BuildingActionPanel 은
        ///   자체 오버라이드 Canvas 를 가진 독립 패널이라 부모가 닫히면(alpha 0) 자식도 함께 사라진다.
        ///   폴백: 액션 패널이 없으면 씬의 첫 Canvas.
        /// </summary>
        /// <returns>부모 Transform(찾지 못하면 null).</returns>
        private static Transform ResolvePanelParent()
        {
            var action = Object.FindFirstObjectByType<BuildingActionPanelUI>(FindObjectsInactive.Include);
            if (action != null && action.transform.parent != null)
                return action.transform.parent;

            Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null) return null;

            Debug.LogWarning("[MistShrine Setup] BuildingActionPanel 의 부모를 찾지 못해 씬의 첫 Canvas 를 부모로 사용합니다(레이아웃 확인 필요).");
            return canvas.transform;
        }

        /// <summary>
        /// 씬에 이미 존재하는 MistShrinePanel 을 찾는다(컴포넌트 우선, 없으면 이름 기반 폴백).
        /// 잘못 만들어진 잔재까지 정리하기 위해 이름 탐색도 함께 한다. 없으면 null.
        /// </summary>
        /// <returns>기존 패널 GameObject(없으면 null).</returns>
        private static GameObject FindExistingPanel()
        {
            return FindSceneObject<MistShrinePanelUI>(PanelObjectName);
        }

        /// <summary>
        /// 씬에서 오브젝트를 찾는다. **먼저 컴포넌트 타입으로** 찾고, 없을 때만 이름으로 폴백한다.
        ///
        /// 왜 이 순서인가(과거 사고 교훈):
        ///   이름만으로 찾으면 비슷한 이름의 엉뚱한 오브젝트에 배선되는 사고가 난다
        ///   (실제 사례: _backButton 이 OffButton 에 잘못 연결). 타입이 훨씬 안전한 기준이므로
        ///   타입 탐색을 우선하고, 이름 탐색은 "컴포넌트가 빠진 잔재"를 회수할 때만 쓴다.
        ///   또한 GameObject.Find 는 비활성 오브젝트를 못 찾으므로 직접 전수 탐색한다.
        /// </summary>
        /// <typeparam name="T">찾을 컴포넌트 타입.</typeparam>
        /// <param name="fallbackName">컴포넌트가 없을 때 이름으로 찾을 대상 이름.</param>
        /// <returns>찾은 GameObject(없으면 null).</returns>
        private static GameObject FindSceneObject<T>(string fallbackName) where T : Component
        {
            var comp = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (comp != null) return comp.gameObject;

            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                // 프리팹 에셋이 아닌 "씬에 실재하는" 오브젝트만 대상으로 한다.
                if (all[i] != null && all[i].name == fallbackName && all[i].gameObject.scene.IsValid())
                    return all[i].gameObject;
            }
            return null;
        }

        // ====================================================================
        // 공통 UI 헬퍼
        // ====================================================================

        /// <summary> SerializedObject 에서 objectReference 값을 읽는다(없으면 null). </summary>
        /// <param name="so">읽을 대상 SerializedObject.</param>
        /// <param name="field">필드 이름.</param>
        /// <returns>참조 값(없으면 null).</returns>
        private static Object GetRef(SerializedObject so, string field)
        {
            SerializedProperty p = so.FindProperty(field);
            return p != null ? p.objectReferenceValue : null;
        }

        /// <summary>
        /// 직속 자식을 "레이아웃에는 남기고 화면에서만" 숨긴다(CanvasGroup.alpha=0). 없으면 no-op.
        ///
        /// SetActive(false) 와의 결정적 차이:
        ///   SetActive(false) 는 자식을 레이아웃 계산에서 완전히 제거해 부모 슬롯의 preferred 크기를 바꾼다.
        ///   그러면 이 자식을 끈 슬롯과 켜둔 슬롯의 크기가 달라져 버튼 크기가 불균일해진다(과거 실제 버그).
        ///   이 메서드는 GameObject 를 켜둔 채 그래픽만 감추므로 레이아웃 기여도가 그대로 유지된다.
        /// </summary>
        /// <param name="parent">자식을 가진 부모(슬롯) Transform.</param>
        /// <param name="childName">숨길 직속 자식 이름(예: "CostContainer").</param>
        private static void HideChildKeepLayout(Transform parent, string childName)
        {
            Transform t = parent.Find(childName);
            if (t == null) return;

            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);

            GameObject go = t.gameObject;
            if (!go.TryGetComponent(out CanvasGroup cg)) cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }

        /// <summary> 직속 자식(이름)의 Image 컴포넌트를 반환한다. 없으면 null. </summary>
        /// <param name="parent">부모 Transform.</param>
        /// <param name="childName">자식 이름.</param>
        /// <returns>Image 컴포넌트(없으면 null).</returns>
        private static Image FindImageChild(Transform parent, string childName)
        {
            Transform t = parent.Find(childName);
            return t != null ? t.GetComponent<Image>() : null;
        }

        /// <summary> 이름이 같은 직속 자식이 있으면 재사용하고, 없으면 RectTransform 을 가진 자식을 새로 만든다(멱등). </summary>
        /// <param name="parent">부모 Transform.</param>
        /// <param name="childName">자식 이름.</param>
        /// <returns>확보한 자식 GameObject.</returns>
        private static GameObject FindOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                if (!existing.gameObject.activeSelf) existing.gameObject.SetActive(true);
                return existing.gameObject;
            }
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary> RectTransform 을 부모 전체로 늘린다(앵커 0~1, 여백 0). </summary>
        /// <param name="rt">대상 RectTransform.</param>
        private static void SetStretch(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Unity 내장(빌트인) 스프라이트를 방어적으로 로드한다(임시 테스트 아트용).
        /// primaryPath 가 없으면 fallbackPath 를 시도하고, 둘 다 실패하면 경고 후 null 을 반환한다.
        /// </summary>
        /// <param name="primaryPath">우선 시도할 내장 리소스 경로(예: "UI/Skin/UISprite.psd").</param>
        /// <param name="fallbackPath">실패 시 대체 경로(없으면 null 전달).</param>
        /// <returns>로드한 스프라이트(실패 시 null).</returns>
        private static Sprite LoadBuiltinSprite(string primaryPath, string fallbackPath)
        {
            Sprite s = AssetDatabase.GetBuiltinExtraResource<Sprite>(primaryPath);
            if (s == null && !string.IsNullOrEmpty(fallbackPath))
                s = AssetDatabase.GetBuiltinExtraResource<Sprite>(fallbackPath);
            if (s == null)
                Debug.LogWarning(
                    $"[MistShrine Setup] 내장 스프라이트를 찾지 못했습니다('{primaryPath}'). " +
                    "임시 아트 없이 진행합니다(정식 아트로 교체 필요).");
            return s;
        }

        /// <summary> Image 컴포넌트를 멱등 확보하고 스프라이트·색을 지정한다. </summary>
        /// <param name="go">대상 GameObject.</param>
        /// <param name="color">지정할 색.</param>
        /// <param name="sprite">지정할 스프라이트(null 가능).</param>
        /// <returns>확보한 Image.</returns>
        private static Image EnsureImage(GameObject go, Color color, Sprite sprite)
        {
            if (!go.TryGetComponent(out Image img)) img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            if (sprite != null) img.type = Image.Type.Sliced;
            return img;
        }

        /// <summary> TextMeshProUGUI 를 멱등 확보하고 폰트·크기·정렬을 지정한다. </summary>
        /// <param name="go">대상 GameObject.</param>
        /// <param name="font">TMP 폰트(null 이면 TMP 기본값 유지).</param>
        /// <param name="text">초기 문자열.</param>
        /// <param name="fontSize">폰트 크기.</param>
        /// <param name="align">정렬.</param>
        /// <returns>확보한 TMP 컴포넌트.</returns>
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

        /// <summary>
        /// private [SerializeField] 오브젝트 참조 슬롯을 연결한다(멱등 — 이미 같은 값이면 아무것도 하지 않는다).
        /// 필드가 없으면 조용히 넘어가지 않고 에러를 남긴다(오배선·오타 즉시 발견용).
        /// </summary>
        /// <param name="target">배선할 컴포넌트.</param>
        /// <param name="fieldName">필드 이름.</param>
        /// <param name="value">연결할 값(null 허용 — 미배선을 명시할 때).</param>
        private static void Connect(Component target, string fieldName, Object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[MistShrine Setup] {target.GetType().Name} 에 '{fieldName}' 필드가 없습니다.");
                return;
            }
            if (prop.objectReferenceValue == value) return; // 멱등.
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        /// <summary>
        /// private [SerializeField] float 슬롯을 지정 값으로 설정한다(멱등).
        /// </summary>
        /// <param name="target">배선할 컴포넌트.</param>
        /// <param name="fieldName">필드 이름.</param>
        /// <param name="value">설정할 값.</param>
        private static void ConnectFloat(Component target, string fieldName, float value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[MistShrine Setup] {target.GetType().Name} 에 '{fieldName}' 필드가 없습니다.");
                return;
            }
            if (Mathf.Approximately(prop.floatValue, value)) return; // 멱등.
            prop.floatValue = value;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        /// <summary> private [SerializeField] 리스트/배열 슬롯을 값 목록으로 채운다. </summary>
        /// <param name="target">배선할 컴포넌트.</param>
        /// <param name="fieldName">필드 이름.</param>
        /// <param name="values">채울 값 목록.</param>
        private static void ConnectList(Component target, string fieldName, List<Object> values)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[MistShrine Setup] {target.GetType().Name} 에 '{fieldName}' 필드가 없습니다.");
                return;
            }
            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }
}
