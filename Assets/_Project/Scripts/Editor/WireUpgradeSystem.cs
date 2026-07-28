// ============================================================================
// WireUpgradeSystem.cs  (에디터 전용 · 1회성 씬/UI 배선 스크립트)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  1) Unity 에디터에서 "Game.unity" 씬을 연다(안 열려 있으면 자동으로 연다). │
// │  2) 상단 메뉴  Hexiege > Setup > Wire Upgrade System  실행.                 │
// │  3) 실행 후 씬을 저장(Ctrl+S)한다.                                          │
// │  (선택) 메뉴  Hexiege > Setup > Add Upgrade Debug Hotkeys (Game) 로          │
// │        F1~F4 디버그 핫키 컴포넌트를 부착할 수 있다.                          │
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 하는가 (연구소 유닛 강화 시스템 씬 배선 — 멱등):
//   [1] NetworkUpgradeController 씬 오브젝트 확보 + GameBootstrapper 배선.
//   [2] ResearchPanelUI 오브젝트 확보 + 참조 배선 + "확정 설계"로 재구성:
//        · ResearchPanelUI 는 BuildingPanelBase 를 상속한다. 그래서 공통 프레임을 갖춘다:
//            헤더 제목 + 닫기[X] + 하단 철거 버튼 + 환불액(생산/액션 패널과 동일 위치·스타일).
//        · 본문은 2개 레이어로 구성한다(공통 프레임은 고정):
//            (a) 매트릭스 레이어: 열 헤더(공/방/속) + 행(그룹) 격자. 각 칸=연구 셀(ResearchCellView).
//            (b) 진행 레이어: 이름 + Lv X→X+1 + 게이지 + 남은 시간 + [취소] (ResearchProgressView).
//        · 배경/폰트/닫기아이콘/버튼 스프라이트/색상/Canvas SO/하단 절반 프레임/철거 버튼/
//          AnimatedPanel(슬라이드) 설정을 "지금 씬에 있는" 생산 패널에서 라이브로 하베스트한다.
//          → GUID 하드코딩 없이 룩이 일치하고(블라인드 안전), 여러 번 실행해도 결과가 같다(멱등).
//
// ── 재구성 배경(2026-07-28) ────────────────────────────────────────────────
//   확정 설계에 따라 연구 패널을 BuildingPanelBase 상속 구조 + 매트릭스/진행 2-레이어로 재구성한다.
//   구 버전(트랙 세로 리스트: HeaderRow/TrackContainer/RowTemplate)은 자동 정리/재구성한다.
//
// 멱등성: 여러 번 실행해도 안전. 이미 있으면 재생성하지 않고 구조를 목표 형태로 강제한다.
// 안전 배선: 이름 문자열 매칭이 아니라 "타입(FindFirstObjectByType) + 생성 참조 직접 Connect".
// 에셋 미발견 시: 추정 배선 금지 — 폴백(단색/기본폰트) 사용 후 콘솔 경고로 사용자에게 안내.
//
// Editor 전용 — Assets/_Project/Scripts/Editor/ 하위(Assembly-CSharp-Editor). 빌드 미포함.
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;                 // NetworkObject
using Hexiege.Bootstrap;             // GameBootstrapper
using Hexiege.Presentation;          // ResearchPanelUI, ProductionPanelUI, AnimatedPanel, 뷰들
using Hexiege.Infrastructure;        // NetworkUpgradeController

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 연구소 강화 시스템의 씬 오브젝트/참조/UI 를 멱등하게 배선하는 1회성 에디터 스크립트.
    /// 생산 패널의 실제 에셋을 라이브로 하베스트해 연구 패널을 "확정 설계(베이스 프레임 + 2-레이어)"로 재구성한다.
    /// </summary>
    public static class WireUpgradeSystem
    {
        // NetworkUpgradeController / ResearchPanelUI 가 배치될 씬. 열려 있지 않으면 이 씬을 연다.
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        // 공용 UI 색상 설정 에셋(BuildingPanelBase._colorConfig 에 배선).
        private const string UIColorConfigPath = "Assets/_Project/Resources/Config/UIColorConfig.asset";

        // 폰트(생산 패널에서 하베스트 실패 시의 폴백 경로). GameSystemRules_UI 규칙 6.
        private const string LightFontPath = "Assets/_Project/Fonts/Maplestory Light SDF.asset";
        private const string BoldFontPath = "Assets/_Project/Fonts/Maplestory Bold SDF.asset";

        // ── 배선 대상 필드명(정확히 일치해야 한다) ──────────────────────────
        private const string BootstrapperNetCtrlField = "_networkUpgradeController"; // GameBootstrapper
        private const string BootstrapperPanelField = "_researchPanelUI";            // GameBootstrapper

        private const string NetworkControllerObjectName = "NetworkUpgradeController";
        private const string ResearchPanelObjectName = "ResearchPanel";

        // ── 매트릭스 열 순서(공/방/속) — ResearchMatrixView 와 동일 순서 ──────
        private static readonly UnitUpgradeStatShim[] Columns =
        {
            UnitUpgradeStatShim.Attack,
            UnitUpgradeStatShim.Defense,
            UnitUpgradeStatShim.MoveSpeed
        };

        // 매트릭스 폭 비중(열 헤더와 행이 정렬되도록 ResearchMatrixView 기본값과 동일하게 맞춘다).
        private const float LabelWeight = 1.0f;
        private const float CellWeight = 1.4f;

        // ====================================================================
        // 메인 메뉴 — 씬/UI 배선
        // ====================================================================

        [MenuItem("Hexiege/Setup/Wire Upgrade System")]
        public static void Run()
        {
            bool sceneOpenedByThis;
            if (!EnsureGameSceneOpen(out sceneOpenedByThis))
                return;

            GameBootstrapper bootstrapper =
                Object.FindFirstObjectByType<GameBootstrapper>(FindObjectsInactive.Include);
            if (bootstrapper == null)
            {
                Debug.LogError("[UpgradeSetup] GameBootstrapper 를 씬에서 찾지 못했습니다. " +
                               "Game.unity 씬을 연 상태에서 실행하세요. 배선을 중단합니다.");
                return;
            }

            Scene scene = bootstrapper.gameObject.scene;

            // 생산 패널 룩 하베스트(리플렉션으로 라이브 에셋 수집).
            ProductionStyle style = HarvestProductionStyle();

            // [1] NetworkUpgradeController 씬 오브젝트 확보 + 배선.
            NetworkUpgradeController netCtrl = EnsureNetworkUpgradeController(scene);
            bool netWired = Connect(bootstrapper, BootstrapperNetCtrlField, netCtrl);
            Debug.Log(netWired
                ? "[UpgradeSetup] ✔ GameBootstrapper._networkUpgradeController 배선 완료."
                : "[UpgradeSetup] ✘ GameBootstrapper._networkUpgradeController 배선 실패(위 로그 참조).");

            // [2] ResearchPanelUI 오브젝트 확보 + 배선 + 재구성.
            ResearchPanelUI panel = EnsureResearchPanel(scene, style);
            if (panel != null)
            {
                bool panelWired = Connect(bootstrapper, BootstrapperPanelField, panel);
                Debug.Log(panelWired
                    ? "[UpgradeSetup] ✔ GameBootstrapper._researchPanelUI 배선 완료."
                    : "[UpgradeSetup] ✘ GameBootstrapper._researchPanelUI 배선 실패(위 로그 참조).");
            }
            else
            {
                Debug.LogWarning("[UpgradeSetup] ResearchPanelUI 생성/확보에 실패하여 " +
                                 "GameBootstrapper._researchPanelUI 배선을 건너뜁니다.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (sceneOpenedByThis)
                EditorSceneManager.SaveScene(scene);

            Debug.Log(
                "[UpgradeSetup] 연구 패널 재구성 완료(BuildingPanelBase 프레임 + 매트릭스/진행 2-레이어).\n" +
                "  · 배경/폰트/닫기아이콘/버튼/색상/철거버튼/AnimatedPanel = 생산 패널에서 라이브 재사용.\n" +
                "  · 매트릭스: 열 헤더(공/방/속) + 행(그룹) 격자, 셀=ResearchCellView(레벨/핍/비용).\n" +
                "  · 진행: 이름 + Lv X→X+1 + 게이지 + 남은 시간 + [취소](ResearchProgressView).\n" +
                "  · 하단 철거 버튼 + 환불액(BuildingPanelBase) — 액션 패널과 동일 위치.\n" +
                "  ※ 하베스트 실패 항목이 있으면 위 경고 로그를 확인하세요(폴백 사용됨).\n" +
                "  ※ 씬을 저장(Ctrl+S)하세요." + (sceneOpenedByThis ? " (이 스크립트가 자동 저장했습니다.)" : ""));

            Selection.activeObject = panel != null ? (Object)panel.gameObject : bootstrapper.gameObject;
        }

        // ====================================================================
        // 생산 패널 룩 하베스트 (리플렉션)
        // ====================================================================

        /// <summary>
        /// 생산 패널(ProductionPanelUI)의 실제 컴포넌트에서 시각 스타일을 수집한 결과.
        /// 하나라도 못 찾으면 해당 항목만 폴백 값으로 채워지고 경고 로그가 남는다.
        /// </summary>
        private struct ProductionStyle
        {
            public bool valid;

            // 프레임(배경).
            public Sprite bgSprite;
            public Color bgColor;
            public Material bgMaterial;
            public Image.Type bgType;
            public Vector2 frameAnchorMin;
            public Vector2 frameAnchorMax;
            public Vector2 framePivot;

            // Canvas 정렬(오버레이 위로 올리기).
            public int canvasSortingOrder;

            // 헤더/본문 폰트.
            public TMP_FontAsset headerFont;
            public Color headerColor;
            public TMP_FontAsset bodyFont;
            public Color bodyColor;

            // 닫기 아이콘 버튼.
            public Sprite closeSprite;
            public Color closeColor;
            public Vector2 closeAnchorMin;
            public Vector2 closeAnchorMax;
            public Vector2 closePivot;

            // 게임 버튼/슬롯 프레임 스프라이트(연구 셀·버튼 배경 재사용).
            public Sprite buttonSprite;
            public Image.Type buttonType;

            // 철거 버튼(하단).
            public Sprite demolishSprite;
            public Color demolishColor;
            public Vector2 demolishAnchorMin;
            public Vector2 demolishAnchorMax;
            public Vector2 demolishPivot;

            // 환불 텍스트.
            public TMP_FontAsset refundFont;
            public Color refundColor;
            public Vector2 refundAnchorMin;
            public Vector2 refundAnchorMax;
            public Vector2 refundPivot;

            // AnimatedPanel(슬라이드 등장) 설정.
            public int animTypeIndex;   // AnimatedPanel.AnimationType (0=PopupFade,1=SlideFromBottom,2=SlideFromTop)
            public float animShow;
            public float animHide;
            public float animSlide;
        }

        private static ProductionStyle HarvestProductionStyle()
        {
            var s = new ProductionStyle
            {
                bgColor = Color.white,
                bgType = Image.Type.Simple,
                frameAnchorMin = new Vector2(0f, 0f),
                frameAnchorMax = new Vector2(1f, 0.5f),
                framePivot = new Vector2(0.5f, 0f),
                canvasSortingOrder = 200,
                headerColor = Color.white,
                bodyColor = Color.white,
                closeColor = Color.white,
                closeAnchorMin = new Vector2(0.86f, 0.83f),
                closeAnchorMax = new Vector2(0.98f, 0.97f),
                closePivot = new Vector2(1f, 1f),
                buttonType = Image.Type.Simple,
                demolishColor = Color.white,
                demolishAnchorMin = new Vector2(0.70f, 0.03f),
                demolishAnchorMax = new Vector2(0.965f, 0.12f),
                demolishPivot = new Vector2(0.5f, 0.5f),
                refundColor = Color.green,
                refundAnchorMin = new Vector2(0.50f, 0.03f),
                refundAnchorMax = new Vector2(0.69f, 0.12f),
                refundPivot = new Vector2(0.5f, 0.5f),
                animTypeIndex = 1,   // SlideFromBottom
                animShow = 0.2f,
                animHide = 0.15f,
                animSlide = 300f,
            };

            ProductionPanelUI prod =
                Object.FindFirstObjectByType<ProductionPanelUI>(FindObjectsInactive.Include);
            if (prod == null)
            {
                Debug.LogWarning("[UpgradeSetup] 씬에서 ProductionPanelUI 를 찾지 못해 룩 하베스트를 생략합니다. " +
                                 "폴백(단색 배경·기본 폰트)으로 연구 패널을 구성합니다. 스크린샷 확인 필요.");
                s.headerFont = LoadFont(BoldFontPath);
                s.bodyFont = LoadFont(LightFontPath);
                s.refundFont = s.bodyFont;
                s.valid = false;
                return s;
            }
            s.valid = true;
            GameObject prodRoot = prod.gameObject;

            // ── Canvas(SO) ──
            if (prodRoot.TryGetComponent(out Canvas prodCanvas))
                s.canvasSortingOrder = prodCanvas.overrideSorting ? prodCanvas.sortingOrder : 200;
            else
                Debug.LogWarning("[UpgradeSetup] 생산 패널 루트에 Canvas 오버라이드가 없습니다. 연구 패널 SO=200 폴백 사용.");

            // ── AnimatedPanel ──
            if (prodRoot.TryGetComponent(out AnimatedPanel prodAnim))
            {
                var pso = new SerializedObject(prodAnim);
                s.animTypeIndex = FindIntEnum(pso, "_animationType", s.animTypeIndex);
                s.animShow = FindFloat(pso, "_showDuration", s.animShow);
                s.animHide = FindFloat(pso, "_hideDuration", s.animHide);
                s.animSlide = FindFloat(pso, "_slideOffset", s.animSlide);
            }
            else
            {
                Debug.LogWarning("[UpgradeSetup] 생산 패널 루트에 AnimatedPanel 이 없습니다. SlideFromBottom 폴백 설정 사용.");
            }

            // ── 프레임 배경 이미지 ──
            Image frameImg = FindFrameImage(prodRoot);
            if (frameImg != null)
            {
                s.bgSprite = frameImg.sprite;
                s.bgColor = frameImg.color;
                s.bgMaterial = frameImg.material == frameImg.defaultMaterial ? null : frameImg.material;
                s.bgType = frameImg.type;
                var frt = frameImg.rectTransform;
                s.frameAnchorMin = frt.anchorMin;
                s.frameAnchorMax = frt.anchorMax;
                s.framePivot = frt.pivot;
            }
            else
            {
                Debug.LogWarning("[UpgradeSetup] 생산 패널 배경 프레임 이미지를 찾지 못했습니다. 단색 배경 폴백 사용. 스크린샷 확인 필요.");
            }

            // ── 헤더 폰트/색상 ──
            var headerText = GetPrivateField(prod, "_headerText") as TextMeshProUGUI;
            if (headerText != null) { s.headerFont = headerText.font; s.headerColor = headerText.color; }
            if (s.headerFont == null)
            {
                s.headerFont = LoadFont(BoldFontPath);
                Debug.LogWarning("[UpgradeSetup] 생산 패널 헤더 폰트를 못 읽어 Bold SDF 폴백 사용.");
            }

            // ── 본문(수치) 폰트/색상 — 유닛 비용 텍스트 기준 ──
            var costText = FirstUnityObject<TextMeshProUGUI>(GetPrivateField(prod, "_unitCostTexts"));
            if (costText != null) { s.bodyFont = costText.font; s.bodyColor = costText.color; }
            if (s.bodyFont == null)
            {
                s.bodyFont = LoadFont(LightFontPath);
                Debug.LogWarning("[UpgradeSetup] 생산 패널 본문 폰트를 못 읽어 Light SDF 폴백 사용.");
            }

            // ── 닫기 아이콘 버튼 ──
            var cancelBtn = GetPrivateField(prod, "_cancelButton") as Button;
            if (cancelBtn != null)
            {
                Image ci = cancelBtn.targetGraphic as Image;
                if (ci == null) cancelBtn.TryGetComponent(out ci);
                if (ci != null) { s.closeSprite = ci.sprite; s.closeColor = ci.color; }
                var crt = cancelBtn.GetComponent<RectTransform>();
                if (crt != null) { s.closeAnchorMin = crt.anchorMin; s.closeAnchorMax = crt.anchorMax; s.closePivot = crt.pivot; }
            }
            if (s.closeSprite == null)
                Debug.LogWarning("[UpgradeSetup] 생산 패널 닫기 버튼 아이콘 스프라이트를 못 읽었습니다. 단색 닫기 버튼 폴백 사용. 스크린샷 확인 필요.");

            // ── 게임 버튼/슬롯 프레임 스프라이트 — 유닛 버튼 이미지 기준 ──
            var unitBtn = FirstUnityObject<Button>(GetPrivateField(prod, "_unitButtons"));
            if (unitBtn != null)
            {
                Image bi = unitBtn.targetGraphic as Image;
                if (bi == null) unitBtn.TryGetComponent(out bi);
                if (bi != null) { s.buttonSprite = bi.sprite; s.buttonType = bi.type; }
            }
            if (s.buttonSprite == null)
                Debug.LogWarning("[UpgradeSetup] 생산 패널 유닛 버튼 스프라이트를 못 읽었습니다. 셀/버튼 배경은 단색 폴백 사용. 스크린샷 확인 필요.");

            // ── 철거 버튼(하단, BuildingPanelBase) ──
            var demolishBtn = GetPrivateField(prod, "_demolishButton") as Button;
            if (demolishBtn != null)
            {
                Image di = demolishBtn.targetGraphic as Image;
                if (di == null) demolishBtn.TryGetComponent(out di);
                if (di != null) { s.demolishSprite = di.sprite; s.demolishColor = di.color; }
                var drt = demolishBtn.GetComponent<RectTransform>();
                if (drt != null) { s.demolishAnchorMin = drt.anchorMin; s.demolishAnchorMax = drt.anchorMax; s.demolishPivot = drt.pivot; }
            }
            else
            {
                Debug.LogWarning("[UpgradeSetup] 생산 패널 철거 버튼(_demolishButton)을 못 읽었습니다. 폴백 위치/단색 사용. 스크린샷 확인 필요.");
            }

            // ── 환불 텍스트(BuildingPanelBase) ──
            var refundText = GetPrivateField(prod, "_demolishRefundText") as TextMeshProUGUI;
            if (refundText != null)
            {
                s.refundFont = refundText.font;
                s.refundColor = refundText.color;
                var rrt = refundText.GetComponent<RectTransform>();
                if (rrt != null) { s.refundAnchorMin = rrt.anchorMin; s.refundAnchorMax = rrt.anchorMax; s.refundPivot = rrt.pivot; }
            }
            if (s.refundFont == null) s.refundFont = s.bodyFont;

            return s;
        }

        private static Image FindFrameImage(GameObject root)
        {
            if (root.transform.childCount > 0 &&
                root.transform.GetChild(0).TryGetComponent(out Image first) && first.sprite != null)
                return first;

            foreach (var img in root.GetComponentsInChildren<Image>(true))
                if (img != null && img.sprite != null)
                    return img;
            return null;
        }

        // ====================================================================
        // [1] NetworkUpgradeController 씬 오브젝트
        // ====================================================================

        private static NetworkUpgradeController EnsureNetworkUpgradeController(Scene scene)
        {
            NetworkUpgradeController existing =
                Object.FindFirstObjectByType<NetworkUpgradeController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log($"[UpgradeSetup] 기존 NetworkUpgradeController('{existing.name}') 재사용 — 새로 만들지 않음.");
                EnsureNetworkObject(existing.gameObject);
                return existing;
            }

            var go = new GameObject(NetworkControllerObjectName);
            SceneManager.MoveGameObjectToScene(go, scene);
            EnsureNetworkObject(go);
            NetworkUpgradeController ctrl = go.AddComponent<NetworkUpgradeController>();

            EditorUtility.SetDirty(go);
            Debug.Log($"[UpgradeSetup] NetworkUpgradeController 씬 오브젝트를 새로 생성했습니다('{go.name}').");
            return ctrl;
        }

        private static void EnsureNetworkObject(GameObject go)
        {
            if (!go.TryGetComponent(out NetworkObject _))
            {
                go.AddComponent<NetworkObject>();
                Debug.Log($"[UpgradeSetup] '{go.name}' 에 NetworkObject 컴포넌트를 부착했습니다.");
            }
        }

        // ====================================================================
        // [2] ResearchPanelUI 오브젝트 + 재구성
        // ====================================================================

        private static ResearchPanelUI EnsureResearchPanel(Scene scene, ProductionStyle style)
        {
            ResearchPanelUI existing =
                Object.FindFirstObjectByType<ResearchPanelUI>(FindObjectsInactive.Include);

            GameObject panelGo;
            ResearchPanelUI panel;

            if (existing != null)
            {
                Debug.Log($"[UpgradeSetup] 기존 ResearchPanelUI('{existing.name}') 재사용 — 구조만 목표 형태로 재구성.");
                panel = existing;
                panelGo = existing.gameObject;
            }
            else
            {
                Transform uiParent = FindUiParent();
                if (uiParent == null)
                {
                    Debug.LogError("[UpgradeSetup] 연구 패널을 배치할 Canvas 를 찾지 못했습니다. " +
                                   "씬에 Canvas(또는 ProductionPanelUI)가 있는지 확인하세요. 패널 생성을 건너뜁니다.");
                    return null;
                }
                panelGo = new GameObject(ResearchPanelObjectName, typeof(RectTransform));
                panelGo.transform.SetParent(uiParent, false);
                panel = panelGo.AddComponent<ResearchPanelUI>();
                Debug.Log($"[UpgradeSetup] ResearchPanel 오브젝트를 '{uiParent.name}' 하위에 새로 생성했습니다.");
            }

            BuildPanel(panelGo, panel, style);
            return panel;
        }

        /// <summary>
        /// 연구 패널을 확정 설계로 강제 구성한다(멱등·구버전 이관).
        ///   루트 = 하단 절반 프레임(배경) + Canvas 오버라이드(SO) + GraphicRaycaster + AnimatedPanel + CanvasGroup.
        ///   자식 = Title / CloseButton / DemolishButton / RefundText / MatrixLayer / ProgressLayer.
        /// 구버전 구조(루트 VLG, HeaderRow, TrackContainer, RowTemplate 등)는 자동 제거/재구성한다.
        /// </summary>
        private static void BuildPanel(GameObject panelGo, ResearchPanelUI panel, ProductionStyle s)
        {
            // ── 구버전 구조 정리(멱등 이관) ──
            RemoveComponent<VerticalLayoutGroup>(panelGo);
            RemoveComponent<HorizontalLayoutGroup>(panelGo);
            RemoveComponent<ContentSizeFitter>(panelGo);
            DeleteChild(panelGo.transform, "HeaderRow");      // 구 헤더 래퍼
            DeleteChild(panelGo.transform, "PlaceholderNote"); // 구 안내 텍스트
            DeleteChild(panelGo.transform, "TrackContainer");  // 구 트랙 세로 리스트

            var rt = panelGo.GetComponent<RectTransform>();
            SetRect(rt, s.frameAnchorMin, s.frameAnchorMax, s.framePivot);

            // ── 배경 이미지(스프라이트 재사용) ──
            Image bg = Ensure<Image>(panelGo);
            if (s.bgSprite != null)
            {
                bg.sprite = s.bgSprite; bg.type = s.bgType; bg.color = s.bgColor; bg.material = s.bgMaterial;
            }
            else
            {
                bg.sprite = null; bg.color = new Color(0.10f, 0.12f, 0.16f, 0.97f);
            }
            bg.raycastTarget = true;

            // ── Canvas 오버라이드 + GraphicRaycaster (BlockingOverlay SO=100 위로) ──
            var canvas = Ensure<Canvas>(panelGo);
            canvas.overrideSorting = true;
            canvas.sortingOrder = s.canvasSortingOrder;
            Ensure<GraphicRaycaster>(panelGo);

            // ── AnimatedPanel(슬라이드 등장) + CanvasGroup ──
            AnimatedPanel anim = Ensure<AnimatedPanel>(panelGo);
            ConfigureAnimatedPanel(anim, s);
            CanvasGroup rootCg = Ensure<CanvasGroup>(panelGo);
            rootCg.alpha = 0f; rootCg.interactable = false; rootCg.blocksRaycasts = false; // 초기 숨김

            // ── 헤더 제목 ──
            GameObject titleGo = FindOrCreateChild(panelGo.transform, "Title");
            SetRect(titleGo.GetComponent<RectTransform>(),
                new Vector2(0.05f, 0.90f), new Vector2(s.closeAnchorMin.x - 0.02f, 0.99f), new Vector2(0f, 1f));
            TextMeshProUGUI title = EnsureText(titleGo, s.headerFont, "연구소 강화", 34, TextAlignmentOptions.Left);
            title.color = s.headerColor;

            // ── 닫기 버튼(아이콘, 우상단) — onClick 은 런타임 InitializeBase 가 연결 ──
            GameObject closeGo = FindOrCreateChild(panelGo.transform, "CloseButton");
            SetRect(closeGo.GetComponent<RectTransform>(), s.closeAnchorMin, s.closeAnchorMax, s.closePivot);
            Button closeBtn = EnsureIconButton(closeGo, s.closeSprite, s.closeColor);

            // ── 철거 버튼(하단, 아이콘) — onClick 은 런타임 InitializeBase 가 연결 ──
            GameObject demolishGo = FindOrCreateChild(panelGo.transform, "DemolishButton");
            SetRect(demolishGo.GetComponent<RectTransform>(), s.demolishAnchorMin, s.demolishAnchorMax, s.demolishPivot);
            Button demolishBtn = EnsureIconButton(demolishGo, s.demolishSprite,
                s.demolishSprite != null ? s.demolishColor : new Color(0.55f, 0.20f, 0.20f, 1f));

            // ── 환불 텍스트(철거 옆) — 값/색은 런타임 UpdateDemolishRefund 가 채운다 ──
            GameObject refundGo = FindOrCreateChild(panelGo.transform, "RefundText");
            SetRect(refundGo.GetComponent<RectTransform>(), s.refundAnchorMin, s.refundAnchorMax, s.refundPivot);
            TextMeshProUGUI refundText = EnsureText(refundGo, s.refundFont, "0", 24, TextAlignmentOptions.Right);
            refundText.color = s.refundColor;

            // ── 본문 영역(공통) : 헤더 아래 ~ 철거 버튼 위 ──
            //   매트릭스/진행 두 레이어가 같은 영역을 겹쳐 차지하고, 런타임에 하나만 보인다.
            Vector2 bodyMin = new Vector2(0.035f, 0.16f);
            Vector2 bodyMax = new Vector2(0.965f, 0.86f);

            // ── (a) 매트릭스 레이어 ──
            CanvasGroup matrixCg = BuildMatrixLayer(panelGo, panel, s, bodyMin, bodyMax);

            // ── (b) 진행 레이어 ──
            CanvasGroup progressCg = BuildProgressLayer(panelGo, panel, s, bodyMin, bodyMax);

            // ── 배선(생성 참조 직접 Connect — 이름 매칭 아님) ──
            // 베이스(BuildingPanelBase) 필드.
            Connect(panel, "_popup", anim);
            Connect(panel, "_headerText", title);
            Connect(panel, "_cancelButton", closeBtn);
            Connect(panel, "_demolishButton", demolishBtn);
            Connect(panel, "_demolishRefundText", refundText);
            var colorConfig = AssetDatabase.LoadAssetAtPath<UIColorConfig>(UIColorConfigPath);
            if (colorConfig != null) Connect(panel, "_colorConfig", colorConfig);
            else Debug.LogWarning($"[UpgradeSetup] UIColorConfig 를 찾지 못했습니다: {UIColorConfigPath} (_colorConfig 비움 — 코드 폴백 색상 사용).");

            // ResearchPanelUI 본문 레이어 필드.
            Connect(panel, "_matrixLayerGroup", matrixCg);
            Connect(panel, "_progressLayerGroup", progressCg);

            EditorUtility.SetDirty(panel);
            EditorUtility.SetDirty(panelGo);
        }

        // ====================================================================
        // (a) 매트릭스 레이어
        // ====================================================================

        /// <summary>
        /// 매트릭스 레이어를 구성한다: 열 헤더(공/방/속) + 행 컨테이너(VLG) + 셀/라벨 템플릿 + 드라이버.
        /// 반환값은 이 레이어의 CanvasGroup(패널의 _matrixLayerGroup 에 배선).
        /// </summary>
        private static CanvasGroup BuildMatrixLayer(GameObject panelGo, ResearchPanelUI panel,
            ProductionStyle s, Vector2 bodyMin, Vector2 bodyMax)
        {
            GameObject layer = FindOrCreateChild(panelGo.transform, "MatrixLayer");
            SetRect(layer.GetComponent<RectTransform>(), bodyMin, bodyMax, new Vector2(0.5f, 0.5f));
            CanvasGroup cg = Ensure<CanvasGroup>(layer);
            cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true; // 기본 표시(런타임이 재평가)

            // 레이어 내부 세로 배치: [열 헤더][행 컨테이너].
            VerticalLayoutGroup layerVlg = Ensure<VerticalLayoutGroup>(layer);
            layerVlg.spacing = 6f;
            layerVlg.padding = new RectOffset(2, 2, 2, 2);
            layerVlg.childControlWidth = true;
            layerVlg.childControlHeight = true;
            layerVlg.childForceExpandWidth = true;
            layerVlg.childForceExpandHeight = false;
            layerVlg.childAlignment = TextAnchor.UpperCenter;

            // ── 열 헤더 행: [코너 공백][공격력][방어력][이동속도] ──
            GameObject header = FindOrCreateChild(layer.transform, "StatHeader");
            header.transform.SetSiblingIndex(0);
            SetPreferredHeight(header, 40f);
            HorizontalLayoutGroup hHlg = EnsureRowHlg(header);
            // 코너(그룹 라벨 열 자리).
            GameObject corner = FindOrCreateChild(header.transform, "Corner");
            corner.transform.SetSiblingIndex(0);
            SetFlexibleWidth(corner, LabelWeight);
            EnsureText(corner, s.headerFont, "", 20, TextAlignmentOptions.Center);
            // 스탯 라벨 3개.
            for (int c = 0; c < Columns.Length; c++)
            {
                GameObject colGo = FindOrCreateChild(header.transform, $"Col_{Columns[c]}");
                colGo.transform.SetSiblingIndex(c + 1);
                SetFlexibleWidth(colGo, CellWeight);
                TextMeshProUGUI colText = EnsureText(colGo, s.headerFont,
                    StatDisplayNameShim(Columns[c]), 22, TextAlignmentOptions.Center);
                colText.color = s.headerColor;
            }

            // ── 행 컨테이너(VLG) — 런타임 행 생성 대상 ──
            GameObject rowContainer = FindOrCreateChild(layer.transform, "RowContainer");
            rowContainer.transform.SetSiblingIndex(1);
            // 나머지 세로 공간을 채우도록 flexibleHeight.
            LayoutElement rcLe = Ensure<LayoutElement>(rowContainer);
            rcLe.flexibleHeight = 1f;
            rcLe.flexibleWidth = 1f;
            VerticalLayoutGroup rcVlg = Ensure<VerticalLayoutGroup>(rowContainer);
            rcVlg.spacing = 6f;
            rcVlg.childControlWidth = true;
            rcVlg.childControlHeight = true;
            rcVlg.childForceExpandWidth = true;
            rcVlg.childForceExpandHeight = false;
            rcVlg.childAlignment = TextAnchor.UpperCenter;

            // ── 셀 템플릿 + 그룹 라벨 템플릿(레이어 직속, 비활성) ──
            ResearchCellView cellTemplate = BuildCellTemplate(layer, s);
            TextMeshProUGUI rowLabelTemplate = BuildRowLabelTemplate(layer, s);

            // ── 드라이버(ResearchMatrixView) 확보 + 배선 ──
            if (!layer.TryGetComponent(out ResearchMatrixView driver))
                driver = layer.AddComponent<ResearchMatrixView>();
            Connect(driver, "_panel", panel);
            Connect(driver, "_cellTemplate", cellTemplate);
            Connect(driver, "_rowLabelTemplate", rowLabelTemplate);
            Connect(driver, "_rowContainer", rowContainer.transform);

            EditorUtility.SetDirty(driver);
            Debug.Log("[UpgradeSetup] ✔ 매트릭스 레이어(ResearchMatrixView) + 셀/라벨 템플릿 배선 완료.");
            return cg;
        }

        /// <summary>
        /// 셀 템플릿(ResearchCellView)을 만든다: 버튼 배경 + [레벨텍스트][핍 5칸][상태텍스트] 세로 구성.
        /// 항상 비활성(드라이버가 복제). 반환값은 배선된 ResearchCellView.
        /// </summary>
        private static ResearchCellView BuildCellTemplate(GameObject layer, ProductionStyle s)
        {
            GameObject cellGo = FindOrCreateChild(layer.transform, "CellTemplate");

            // 셀 배경 + 버튼.
            Image cellBg = Ensure<Image>(cellGo);
            if (s.buttonSprite != null) { cellBg.sprite = s.buttonSprite; cellBg.type = s.buttonType; cellBg.color = new Color(1f, 1f, 1f, 0.92f); }
            else { cellBg.sprite = null; cellBg.color = new Color(1f, 1f, 1f, 0.08f); }
            cellBg.raycastTarget = true;
            Button cellBtn = Ensure<Button>(cellGo);
            cellBtn.targetGraphic = cellBg;

            // 셀 내부 세로 배치.
            VerticalLayoutGroup cellVlg = Ensure<VerticalLayoutGroup>(cellGo);
            cellVlg.spacing = 2f;
            cellVlg.padding = new RectOffset(6, 6, 6, 6);
            cellVlg.childControlWidth = true;
            cellVlg.childControlHeight = true;
            cellVlg.childForceExpandWidth = true;
            cellVlg.childForceExpandHeight = true;
            cellVlg.childAlignment = TextAnchor.MiddleCenter;

            // 레벨 텍스트.
            GameObject lvGo = FindOrCreateChild(cellGo.transform, "Level");
            lvGo.transform.SetSiblingIndex(0);
            TextMeshProUGUI levelText = EnsureText(lvGo, s.bodyFont, "Lv 0/5", 20, TextAlignmentOptions.Center);
            levelText.color = s.bodyColor;

            // 핍 5칸(HLG).
            GameObject pipsGo = FindOrCreateChild(cellGo.transform, "Pips");
            pipsGo.transform.SetSiblingIndex(1);
            HorizontalLayoutGroup pipsHlg = Ensure<HorizontalLayoutGroup>(pipsGo);
            pipsHlg.spacing = 3f;
            pipsHlg.childControlWidth = true;
            pipsHlg.childControlHeight = true;
            pipsHlg.childForceExpandWidth = true;
            pipsHlg.childForceExpandHeight = true;
            pipsHlg.childAlignment = TextAnchor.MiddleCenter;
            var pips = new Image[5];
            for (int i = 0; i < pips.Length; i++)
            {
                GameObject pipGo = FindOrCreateChild(pipsGo.transform, $"Pip{i}");
                pipGo.transform.SetSiblingIndex(i);
                Image pip = Ensure<Image>(pipGo);
                pip.sprite = null;
                pip.color = new Color(1f, 1f, 1f, 0.18f);
                pip.raycastTarget = false;
                pips[i] = pip;
            }

            // 상태 텍스트(비용 / 연구중 / MAX).
            GameObject statusGo = FindOrCreateChild(cellGo.transform, "Status");
            statusGo.transform.SetSiblingIndex(2);
            TextMeshProUGUI statusText = EnsureText(statusGo, s.bodyFont, "0", 20, TextAlignmentOptions.Center);
            statusText.color = s.bodyColor;

            if (!cellGo.TryGetComponent(out ResearchCellView cell))
                cell = cellGo.AddComponent<ResearchCellView>();

            Connect(cell, "_levelText", levelText);
            Connect(cell, "_statusText", statusText);
            Connect(cell, "_button", cellBtn);
            ConnectArray(cell, "_pips", pips);

            cellGo.SetActive(false); // 템플릿은 항상 비활성.
            EditorUtility.SetDirty(cell);
            return cell;
        }

        private static TextMeshProUGUI BuildRowLabelTemplate(GameObject layer, ProductionStyle s)
        {
            GameObject labelGo = FindOrCreateChild(layer.transform, "RowLabelTemplate");
            TextMeshProUGUI label = EnsureText(labelGo, s.headerFont, "그룹", 24, TextAlignmentOptions.MidlineLeft);
            label.color = s.headerColor;
            SetFlexibleWidth(labelGo, LabelWeight);
            labelGo.SetActive(false); // 템플릿은 항상 비활성.
            return label;
        }

        // ====================================================================
        // (b) 진행 레이어
        // ====================================================================

        /// <summary>
        /// 진행 레이어를 구성한다: [이름][Lv X→X+1][게이지 바][남은 시간][취소 버튼].
        /// 반환값은 이 레이어의 CanvasGroup(패널의 _progressLayerGroup 에 배선).
        /// </summary>
        private static CanvasGroup BuildProgressLayer(GameObject panelGo, ResearchPanelUI panel,
            ProductionStyle s, Vector2 bodyMin, Vector2 bodyMax)
        {
            GameObject layer = FindOrCreateChild(panelGo.transform, "ProgressLayer");
            SetRect(layer.GetComponent<RectTransform>(), bodyMin, bodyMax, new Vector2(0.5f, 0.5f));
            CanvasGroup cg = Ensure<CanvasGroup>(layer);
            cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false; // 기본 숨김(런타임이 재평가)

            VerticalLayoutGroup vlg = Ensure<VerticalLayoutGroup>(layer);
            vlg.spacing = 12f;
            vlg.padding = new RectOffset(16, 16, 20, 20);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;

            // 이름.
            GameObject nameGo = FindOrCreateChild(layer.transform, "Name");
            nameGo.transform.SetSiblingIndex(0);
            SetPreferredHeight(nameGo, 44f);
            TextMeshProUGUI nameText = EnsureText(nameGo, s.headerFont, "연구 중", 30, TextAlignmentOptions.Center);
            nameText.color = s.headerColor;

            // 레벨 전이.
            GameObject lvGo = FindOrCreateChild(layer.transform, "LevelTransition");
            lvGo.transform.SetSiblingIndex(1);
            SetPreferredHeight(lvGo, 34f);
            TextMeshProUGUI levelText = EnsureText(lvGo, s.bodyFont, "Lv 0 → 1", 24, TextAlignmentOptions.Center);
            levelText.color = s.bodyColor;

            // 게이지 바(배경 + Fill).
            GameObject barGo = FindOrCreateChild(layer.transform, "Bar");
            barGo.transform.SetSiblingIndex(2);
            SetPreferredHeight(barGo, 40f);
            EnsureImage(barGo, new Color(0.08f, 0.09f, 0.12f, 1f));
            GameObject fillGo = FindOrCreateChild(barGo.transform, "Fill");
            SetStretch(fillGo.GetComponent<RectTransform>());
            Image fill = EnsureFilledImage(fillGo, new Color(0.25f, 0.55f, 0.85f, 1f));

            // 남은 시간.
            GameObject timeGo = FindOrCreateChild(layer.transform, "Time");
            timeGo.transform.SetSiblingIndex(3);
            SetPreferredHeight(timeGo, 34f);
            TextMeshProUGUI timeText = EnsureText(timeGo, s.bodyFont, "0s", 24, TextAlignmentOptions.Center);
            timeText.color = s.bodyColor;

            // 취소 버튼(라벨) — onClick 은 런타임 ResearchProgressView.Awake 가 연결.
            GameObject cancelGo = FindOrCreateChild(layer.transform, "CancelButton");
            cancelGo.transform.SetSiblingIndex(4);
            SetPreferredHeight(cancelGo, 60f);
            Button cancelBtn = EnsureSpriteButton(cancelGo, s.headerFont, "연구 취소",
                s.buttonSprite, s.buttonType, new Color(0.55f, 0.22f, 0.22f, 1f), 24);

            if (!layer.TryGetComponent(out ResearchProgressView view))
                view = layer.AddComponent<ResearchProgressView>();
            Connect(view, "_panel", panel);
            Connect(view, "_nameText", nameText);
            Connect(view, "_levelText", levelText);
            Connect(view, "_progressFill", fill);
            Connect(view, "_timeText", timeText);
            Connect(view, "_cancelButton", cancelBtn);

            EditorUtility.SetDirty(view);
            Debug.Log("[UpgradeSetup] ✔ 진행 레이어(ResearchProgressView) 배선 완료.");
            return cg;
        }

        // ====================================================================
        // 선택 메뉴 — 디버그 핫키 부착
        // ====================================================================

        [MenuItem("Hexiege/Setup/Add Upgrade Debug Hotkeys (Game)")]
        public static void AddDebugHotkeys()
        {
            bool sceneOpenedByThis;
            if (!EnsureGameSceneOpen(out sceneOpenedByThis))
                return;

            GameBootstrapper bootstrapper =
                Object.FindFirstObjectByType<GameBootstrapper>(FindObjectsInactive.Include);
            if (bootstrapper == null)
            {
                Debug.LogError("[UpgradeSetup] GameBootstrapper 를 찾지 못해 디버그 핫키를 부착할 수 없습니다.");
                return;
            }

            GameObject host = bootstrapper.gameObject;
            if (!host.TryGetComponent(out UpgradeDebugHotkeys _))
            {
                host.AddComponent<UpgradeDebugHotkeys>();
                Debug.Log("[UpgradeSetup] ✔ UpgradeDebugHotkeys 를 GameBootstrapper 오브젝트에 부착했습니다.");
            }
            else
            {
                Debug.Log("[UpgradeSetup] UpgradeDebugHotkeys 가 이미 부착돼 있습니다 — 건너뜁니다.");
            }

            EditorSceneManager.MarkSceneDirty(host.scene);
            if (sceneOpenedByThis)
                EditorSceneManager.SaveScene(host.scene);
        }

        // ====================================================================
        // 씬 오픈 헬퍼
        // ====================================================================

        private static bool EnsureGameSceneOpen(out bool openedByThis)
        {
            openedByThis = false;

            if (Object.FindFirstObjectByType<GameBootstrapper>(FindObjectsInactive.Include) != null)
                return true;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                Debug.LogError($"[UpgradeSetup] Game 씬을 찾지 못했습니다: {GameScenePath}");
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[UpgradeSetup] 씬 열기가 취소되었습니다(저장 대화 취소). 배선을 건너뜁니다.");
                return false;
            }

            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            openedByThis = true;
            return true;
        }

        // ====================================================================
        // 리플렉션 / 배선 헬퍼
        // ====================================================================

        private static object GetPrivateField(object obj, string fieldName)
        {
            if (obj == null) return null;
            for (var t = obj.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) return f.GetValue(obj);
            }
            return null;
        }

        private static T FirstUnityObject<T>(object listObj) where T : Object
        {
            if (listObj is IList list)
            {
                foreach (var e in list)
                    if (e is T t && t != null) return t;
            }
            return null;
        }

        private static TMP_FontAsset LoadFont(string path)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
                Debug.LogWarning($"[UpgradeSetup] 폰트 에셋을 찾지 못했습니다: {path} (TMP 기본 폰트로 대체됨).");
            return font;
        }

        /// <summary> private [SerializeField] 슬롯(상속 포함)을 SerializedObject 로 안전하게 연결. </summary>
        private static bool Connect(Component target, string fieldName, Object value)
        {
            if (target == null) return false;

            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[UpgradeSetup] {target.GetType().Name} 에 '{fieldName}' 필드가 없습니다(배선 실패).");
                return false;
            }
            if (value == null)
            {
                Debug.LogWarning($"[UpgradeSetup] '{fieldName}' 에 넣을 값이 null 입니다(배선 건너뜀).");
                return false;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            return true;
        }

        /// <summary> 배열/리스트 [SerializeField] 슬롯을 SerializedObject 로 연결(요소 순서 유지). </summary>
        private static bool ConnectArray(Component target, string fieldName, Object[] values)
        {
            if (target == null) return false;

            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null || !prop.isArray)
            {
                Debug.LogError($"[UpgradeSetup] {target.GetType().Name} 에 배열 '{fieldName}' 필드가 없습니다(배선 실패).");
                return false;
            }

            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedProperties();
            return true;
        }

        /// <summary> AnimatedPanel 의 애니메이션 설정을 SerializedObject 로 강제한다(하베스트 값 반영). </summary>
        private static void ConfigureAnimatedPanel(AnimatedPanel target, ProductionStyle s)
        {
            var so = new SerializedObject(target);
            SetEnumIndex(so, "_animationType", s.animTypeIndex);
            SetFloat(so, "_showDuration", s.animShow);
            SetFloat(so, "_hideDuration", s.animHide);
            SetFloat(so, "_slideOffset", s.animSlide);
            so.ApplyModifiedProperties();
        }

        private static void SetEnumIndex(SerializedObject so, string field, int value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.enumValueIndex = value;
        }

        private static void SetFloat(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
        }

        private static int FindIntEnum(SerializedObject so, string field, int fallback)
        {
            var p = so.FindProperty(field);
            return p != null ? p.enumValueIndex : fallback;
        }

        private static float FindFloat(SerializedObject so, string field, float fallback)
        {
            var p = so.FindProperty(field);
            return p != null ? p.floatValue : fallback;
        }

        // ====================================================================
        // UI 생성 헬퍼
        // ====================================================================

        private static T Ensure<T>(GameObject go) where T : Component
        {
            if (!go.TryGetComponent(out T c)) c = go.AddComponent<T>();
            return c;
        }

        private static void RemoveComponent<T>(GameObject go) where T : Component
        {
            if (go.TryGetComponent(out T c)) Object.DestroyImmediate(c);
        }

        private static void DeleteChild(Transform parent, string childName)
        {
            Transform t = parent.Find(childName);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        private static GameObject FindOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        // 행/헤더용 HLG(자식은 자신의 flexibleWidth 비중으로 폭 분배).
        private static HorizontalLayoutGroup EnsureRowHlg(GameObject go)
        {
            HorizontalLayoutGroup hlg = Ensure<HorizontalLayoutGroup>(go);
            hlg.spacing = 6f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            return hlg;
        }

        private static TextMeshProUGUI EnsureText(
            GameObject go, TMP_FontAsset font, string text, int fontSize, TextAlignmentOptions align)
        {
            TextMeshProUGUI t = Ensure<TextMeshProUGUI>(go);
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        /// <summary> 아이콘 스프라이트 버튼(라벨 없음). 구버전 "X" 텍스트 라벨은 제거한다. </summary>
        private static Button EnsureIconButton(GameObject go, Sprite sprite, Color color)
        {
            Image img = Ensure<Image>(go);
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Simple; img.color = color; }
            else { img.sprite = null; img.color = color; }
            Button btn = Ensure<Button>(go);
            btn.targetGraphic = img;
            DeleteChild(go.transform, "Label");
            return btn;
        }

        /// <summary> 스프라이트 배경 + 텍스트 라벨 버튼. 스프라이트 없으면 폴백 단색. </summary>
        private static Button EnsureSpriteButton(
            GameObject go, TMP_FontAsset font, string label,
            Sprite sprite, Image.Type spriteType, Color fallbackColor, int fontSize)
        {
            Image img = Ensure<Image>(go);
            if (sprite != null) { img.sprite = sprite; img.type = spriteType; img.color = Color.white; }
            else { img.sprite = null; img.color = fallbackColor; }

            Button btn = Ensure<Button>(go);
            btn.targetGraphic = img;

            GameObject labelGo = FindOrCreateChild(go.transform, "Label");
            SetStretch(labelGo.GetComponent<RectTransform>());
            TextMeshProUGUI t = EnsureText(labelGo, font, label, fontSize, TextAlignmentOptions.Center);
            t.color = Color.white;
            return btn;
        }

        private static void SetStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetPreferredHeight(GameObject go, float h)
        {
            LayoutElement le = Ensure<LayoutElement>(go);
            le.preferredHeight = h;
            le.flexibleHeight = 0f;
            le.flexibleWidth = 1f;
        }

        private static void SetFlexibleWidth(GameObject go, float weight)
        {
            LayoutElement le = Ensure<LayoutElement>(go);
            le.flexibleWidth = weight;
        }

        private static Image EnsureImage(GameObject go, Color color)
        {
            Image img = Ensure<Image>(go);
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Image EnsureFilledImage(GameObject go, Color color)
        {
            Image img = EnsureImage(go, color);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 0f;
            return img;
        }

        private static Transform FindUiParent()
        {
            ProductionPanelUI production =
                Object.FindFirstObjectByType<ProductionPanelUI>(FindObjectsInactive.Include);
            if (production != null && production.transform.parent != null)
                return production.transform.parent;

            Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            return canvas != null ? canvas.transform : null;
        }

        // ====================================================================
        // 매트릭스 열 헤더 이름 — Domain enum 을 Editor 어셈블리에서 안전 참조하기 위한 shim
        // ====================================================================
        //   Editor 스크립트는 Domain(Hexiege.Domain)을 직접 using 하지 않아도 되도록,
        //   열 순서/이름만 별도 shim 으로 관리한다(런타임 ResearchMatrixView 와 순서 일치).
        private enum UnitUpgradeStatShim { Attack, Defense, MoveSpeed }

        private static string StatDisplayNameShim(UnitUpgradeStatShim stat)
        {
            switch (stat)
            {
                case UnitUpgradeStatShim.Attack: return "공격력";
                case UnitUpgradeStatShim.Defense: return "방어력";
                case UnitUpgradeStatShim.MoveSpeed: return "이동속도";
                default: return stat.ToString();
            }
        }
    }
}
