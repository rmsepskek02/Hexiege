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
//     A) BuildingSkillPanel(전용 스킬 패널) — 헤더/닫기/철거 + 스킬 슬롯 5개(아이콘+쿨다운 오버레이).
//     B) SkillAimReticle(조준 범위 원) + SkillAimController(조준 입력) + 하단 X(취소) 버튼.
//     C) NetworkSkillController(멀티 발동 중계) 씬 오브젝트(NetworkObject 포함, 자동 스폰).
//     D) GameBootstrapper의 신규 슬롯 배선:
//          _skillLoadoutConfig / _buildingSkillPanelUI / _skillAimController / _networkSkillController.
//   아트는 전부 "플레이스홀더"(단색/기본 도형)다. 참조 배선까지 끝내 실기 동작이 가능한 상태로 만든다.
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

            // ── UI 부모 Canvas 결정(기존 액션 패널이 붙은 캔버스 재사용) ──────
            Canvas canvas = ResolveUiCanvas();
            if (canvas == null)
            {
                Debug.LogError("[Skill Setup] UI Canvas를 찾지 못했습니다. 씬에 Canvas가 있는지 확인하세요.");
                return;
            }

            // ── A) 스킬 패널 생성/배선 ─────────────────────────────────────
            BuildingSkillPanelUI skillPanel = BuildSkillPanel(canvas.transform, boldFont, colorConfig);

            // ── B) 조준 원 + 조준 컨트롤러 + 취소 X 버튼 ──────────────────
            SkillAimReticle reticle = BuildReticle();
            RectTransform cancelZone = BuildCancelButton(canvas.transform, boldFont);
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
                "  · BuildingSkillPanel / SkillAimReticle / SkillAimController / SkillCancelButton / NetworkSkillController 생성.\n" +
                "  · GameBootstrapper 슬롯 4종 연결(_skillLoadoutConfig/_buildingSkillPanelUI/_skillAimController/_networkSkillController).\n" +
                (sceneOpenedByThis ? "  · 씬 자동 저장 완료.\n" : "  · 열린 씬 dirty 표시 — Ctrl+S로 저장하세요.\n") +
                "  · 쿨다운 fill·조준 원에 Unity 내장 스프라이트를 임시 지정(테스트용) — 이미 셋업된 씬이면 이 메뉴를 다시 실행하면 반영됩니다.\n" +
                "  · 후속: 아이콘/조준원/오버레이/취소X 정식 아트로 교체, 레이아웃·엣지스크롤 여백 튜닝, 멀티 NetworkObject 확인.");

            Selection.activeObject = skillPanel != null ? (Object)skillPanel.gameObject : bootstrapper;
        }

        // ====================================================================
        // A) 스킬 패널
        // ====================================================================

        /// <summary>
        /// BuildingSkillPanel(전용 스킬 패널)을 캔버스 하위에 멱등 생성하고 필드를 배선한다.
        /// AnimatedPanel(자체 CanvasGroup)·헤더/닫기/철거 + 스킬 슬롯 5개(아이콘+쿨다운 오버레이)를 구성한다.
        /// </summary>
        private static BuildingSkillPanelUI BuildSkillPanel(Transform canvas, TMP_FontAsset font, UIColorConfig colorConfig)
        {
            // 루트(패널). 화면 하단 중앙 밴드에 배치(모바일 9:16 가정, 실기 튜닝 대상).
            GameObject root = FindOrCreateChild(canvas, "BuildingSkillPanel");
            RectTransform rootRt = root.GetComponent<RectTransform>();
            SetAnchors(rootRt, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.42f));

            // ── Canvas 정렬 오버라이드(필수) ──────────────────────────────
            // GameSystemRules_CanvasSortingOrder: 패널 본체는 OverrideSorting=true, SortingOrder=200.
            // (BuildingActionPanel/ProductionPopup 등과 동일 대역.)
            // 이게 없으면 패널이 SO=0으로 그려져 BlockingOverlay(SO=100, 반투명 배경)에 덮여
            // "배경만 보이고 패널은 안 보이는" 버그가 난다.
            if (!root.TryGetComponent(out Canvas panelCanvas))
                panelCanvas = root.AddComponent<Canvas>();
            panelCanvas.overrideSorting = true;
            panelCanvas.sortingOrder = 200;
            // Canvas Override에는 GraphicRaycaster가 반드시 함께 있어야 버튼 입력이 동작한다
            // (CanvasSortingOrder 규칙 1). 없으면 스킬 버튼이 눌리지 않는다.
            if (!root.TryGetComponent(out GraphicRaycaster _))
                root.AddComponent<GraphicRaycaster>();

            // 배경 이미지(플레이스홀더 반투명 어두운 색).
            Image bg = EnsureImage(root, new Color(0.08f, 0.10f, 0.16f, 0.92f), null);
            bg.raycastTarget = true;

            // AnimatedPanel(팝업 등장/사라짐 — CanvasGroup 자동 추가).
            if (!root.TryGetComponent(out AnimatedPanel popup))
                popup = root.AddComponent<AnimatedPanel>();

            // 헤더(건물 이름).
            GameObject headerGo = FindOrCreateChild(root.transform, "Header");
            SetAnchors(headerGo.GetComponent<RectTransform>(), new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.98f));
            TextMeshProUGUI headerText = EnsureText(headerGo, font, "Skill Building", 30, TextAlignmentOptions.Center);

            // 닫기(X) 버튼 — 우상단.
            GameObject cancelGo = FindOrCreateChild(root.transform, "CloseButton");
            SetAnchors(cancelGo.GetComponent<RectTransform>(), new Vector2(0.88f, 0.84f), new Vector2(0.99f, 0.99f));
            Button cancelButton = EnsureButton(cancelGo, font, "X", new Color(0.5f, 0.2f, 0.2f, 1f));

            // 슬롯 그리드(3열) — 스킬 5 + 철거 1 = 6칸.
            GameObject grid = FindOrCreateChild(root.transform, "SlotGrid");
            SetAnchors(grid.GetComponent<RectTransform>(), new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.78f));
            EnsureGrid(grid);

            // 스킬 슬롯 5개(아이콘 + 쿨다운 오버레이).
            var slotButtons = new List<Button>(SkillSlotCount);
            var slotIcons = new List<Image>(SkillSlotCount);
            var slotOverlays = new List<SkillCooldownOverlay>(SkillSlotCount);

            for (int i = 0; i < SkillSlotCount; i++)
            {
                GameObject slot = FindOrCreateChild(grid.transform, $"SkillSlot{i + 1}");
                Image slotBg = EnsureImage(slot, new Color(0.20f, 0.24f, 0.34f, 1f), null);
                if (!slot.TryGetComponent(out Button slotBtn)) slotBtn = slot.AddComponent<Button>();
                slotBtn.targetGraphic = slotBg;

                // 아이콘(스킬 스프라이트가 들어갈 자리 — 플레이스홀더는 비활성).
                GameObject iconGo = FindOrCreateChild(slot.transform, "Icon");
                SetStretch(iconGo.GetComponent<RectTransform>());
                Image icon = EnsureImage(iconGo, Color.white, null);
                icon.raycastTarget = false;
                icon.enabled = false; // 아이콘 스프라이트 미지정 → 숨김(런타임에 SkillData.Icon로 켜짐).

                // 쿨다운 오버레이(시계방향 radial fill + 남은초 숫자).
                SkillCooldownOverlay overlay = BuildCooldownOverlay(slot.transform, font);

                slotButtons.Add(slotBtn);
                slotIcons.Add(icon);
                slotOverlays.Add(overlay);
            }

            // 철거(6번째 칸) — BuildingPanelBase._demolishButton.
            GameObject demolishGo = FindOrCreateChild(grid.transform, "DemolishSlot");
            Image demolishBg = EnsureImage(demolishGo, new Color(0.45f, 0.20f, 0.20f, 1f), null);
            if (!demolishGo.TryGetComponent(out Button demolishButton)) demolishButton = demolishGo.AddComponent<Button>();
            demolishButton.targetGraphic = demolishBg;
            GameObject demolishLabelGo = FindOrCreateChild(demolishGo.transform, "Label");
            SetStretch(demolishLabelGo.GetComponent<RectTransform>());
            EnsureText(demolishLabelGo, font, "철거", 24, TextAlignmentOptions.Center);
            // 환불 금액 텍스트(철거 칸 하단).
            GameObject refundGo = FindOrCreateChild(demolishGo.transform, "RefundText");
            SetAnchors(refundGo.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0.35f));
            TextMeshProUGUI refundText = EnsureText(refundGo, font, "0", 20, TextAlignmentOptions.Center);

            // 컴포넌트 확보(BuildingSkillPanelUI).
            if (!root.TryGetComponent(out BuildingSkillPanelUI panel))
                panel = root.AddComponent<BuildingSkillPanelUI>();

            // ── 필드 배선(base + skill) ──
            // BuildingPanelBase(protected serialized) 필드.
            Connect(panel, "_popup", popup);
            Connect(panel, "_headerText", headerText);
            Connect(panel, "_cancelButton", cancelButton);
            Connect(panel, "_demolishButton", demolishButton);
            Connect(panel, "_demolishRefundText", refundText);
            Connect(panel, "_colorConfig", colorConfig);
            // BuildingSkillPanelUI 스킬 슬롯 리스트.
            ConnectList(panel, "_skillSlotButtons", slotButtons.ConvertAll(b => (Object)b));
            ConnectList(panel, "_skillSlotIcons", slotIcons.ConvertAll(im => (Object)im));
            ConnectList(panel, "_skillSlotOverlays", slotOverlays.ConvertAll(o => (Object)o));

            return panel;
        }

        /// <summary>
        /// 슬롯 위 쿨다운 오버레이(SkillCooldownOverlay)를 멱등 생성한다.
        /// 시계방향 radial fill 이미지 + 남은초 TMP + CanvasGroup으로 구성한다.
        /// </summary>
        private static SkillCooldownOverlay BuildCooldownOverlay(Transform slot, TMP_FontAsset font)
        {
            GameObject overlayGo = FindOrCreateChild(slot, "CooldownOverlay");
            SetStretch(overlayGo.GetComponent<RectTransform>());

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
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = 1f;
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

        // ====================================================================
        // B) 조준 원 / 컨트롤러 / 취소 버튼
        // ====================================================================

        /// <summary>
        /// SkillAimReticle(조준 범위 원)을 월드 오브젝트로 멱등 생성한다(아트 플레이스홀더 — Ring 자식).
        /// </summary>
        private static SkillAimReticle BuildReticle()
        {
            GameObject root = GameObject.Find("SkillAimReticle");
            if (root == null) root = new GameObject("SkillAimReticle");

            // 링(원) 자식 — 지름 1(반경 0.5) 기준. 아트는 사용자가 SpriteRenderer/메시로 채운다.
            Transform ringT = root.transform.Find("Ring");
            GameObject ring;
            if (ringT == null)
            {
                ring = new GameObject("Ring");
                ring.transform.SetParent(root.transform, false);
                ring.AddComponent<SpriteRenderer>();
            }
            else ring = ringT.gameObject;

            // 임시 내장 스프라이트 — 정식 조준 원 아트로 교체 전, 화면에 실제로 보이게 한다.
            //   Unity 내장 Knob(채워진 원)을 임시로 넣는다. 테스트용이라 링이 아니어도 무방하며,
            //   반투명 색으로 두면 조준 범위를 가늠할 수 있다. (내장 스프라이트 로드 실패 시 조용히 스킵.)
            if (!ring.TryGetComponent(out SpriteRenderer ringRenderer))
                ringRenderer = ring.AddComponent<SpriteRenderer>();
            Sprite reticleSprite = LoadBuiltinSprite("UI/Skin/Knob.psd", "UI/Skin/UISprite.psd");
            if (reticleSprite != null) ringRenderer.sprite = reticleSprite;
            // 눈에 띄되 아래 지형이 비치도록 반투명 붉은 톤(임시).
            ringRenderer.color = new Color(1f, 0.35f, 0.35f, 0.4f);

            if (!root.TryGetComponent(out SkillAimReticle reticle))
                reticle = root.AddComponent<SkillAimReticle>();

            Connect(reticle, "_ring", ring.transform);
            return reticle;
        }

        /// <summary>
        /// 하단 중앙 X(취소) 버튼(플레이스홀더)을 멱등 생성하고 RectTransform을 반환한다(규칙 20·21).
        /// 화면 맨 끝보다 살짝 안쪽(하단 중앙)에 둔다.
        /// </summary>
        private static RectTransform BuildCancelButton(Transform canvas, TMP_FontAsset font)
        {
            GameObject go = FindOrCreateChild(canvas, "SkillCancelButton");
            RectTransform rt = go.GetComponent<RectTransform>();
            // 하단 중앙(맨 끝보다 안쪽 — 엣지 스크롤 영역과 겹치지 않도록, 규칙 21).
            SetAnchors(rt, new Vector2(0.40f, 0.06f), new Vector2(0.60f, 0.14f));
            EnsureButton(go, font, "✕ 취소", new Color(0.5f, 0.2f, 0.2f, 0.9f));
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

        /// <summary> 기존 액션 패널이 붙은 Canvas를 우선 재사용, 없으면 씬의 첫 Canvas. </summary>
        private static Canvas ResolveUiCanvas()
        {
            var action = Object.FindFirstObjectByType<BuildingActionPanelUI>(FindObjectsInactive.Include);
            if (action != null)
            {
                Canvas c = action.GetComponentInParent<Canvas>();
                if (c != null) return c;
            }
            return Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
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

        private static GridLayoutGroup EnsureGrid(GameObject go)
        {
            if (!go.TryGetComponent(out GridLayoutGroup grid)) grid = go.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(120f, 120f);
            grid.spacing = new Vector2(12f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;
            return grid;
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

        private static Button EnsureButton(GameObject go, TMP_FontAsset font, string label, Color bg)
        {
            Image img = EnsureImage(go, bg, null);
            if (!go.TryGetComponent(out Button btn)) btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            GameObject labelGo = FindOrCreateChild(go.transform, "Label");
            SetStretch(labelGo.GetComponent<RectTransform>());
            EnsureText(labelGo, font, label, 24, TextAlignmentOptions.Center);
            return btn;
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
