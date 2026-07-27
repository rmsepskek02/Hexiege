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
//   [2] ResearchPanelUI 오브젝트 확보 + 참조 배선 + "생산 패널 룩" 재구성:
//        · 생산 패널(ProductionPanelUI)의 실제 컴포넌트를 리플렉션으로 읽어
//          배경 스프라이트 · 폰트(TMP) · 닫기 아이콘 · 버튼 스프라이트 · 색상 ·
//          Canvas 정렬순서(SO=200) · 하단 절반 프레임 배치를 그대로 재사용한다.
//        · 이렇게 하면 GUID 하드코딩 없이 "지금 씬에 있는" 생산 패널과 자동으로
//          룩이 일치하고(블라인드 환경 안전), 여러 번 실행해도 결과가 같다(멱등).
//   [3] 트랙 행 드라이버(ResearchTrackListView) + 행 템플릿(ResearchTrackRowView)
//        을 동일한 에셋으로 생성/배선.
//
// ── 재구성 배경(2026-07-27) ────────────────────────────────────────────────
//   기존 스크립트는 단색 배경 + "X" 텍스트 닫기버튼 + 자체 스타일로 "기능만 되는
//   골격"을 만들어 생산 패널과 룩이 달랐다. 또한 Canvas 오버라이드(SO=200)가 없어
//   BlockingOverlay(SO=100) 아래에 그려지는 레이어 문제가 있었다.
//   이번 재구성으로:
//     · 배경/폰트/닫기아이콘/버튼/색상을 생산 패널에서 라이브로 하베스트해 일치.
//     · Canvas 오버라이드(SO=200) + GraphicRaycaster 추가로 레이어 정상화.
//     · 하단 절반·전체 너비 프레임(생산 패널과 동일한 크기감).
//     · 트랙 행을 [트랙명][레벨][비용·시간 / 진행바 / MAX][연구 버튼] 가독성 레이아웃으로.
//
// 멱등성: 여러 번 실행해도 안전. 이미 있으면 재생성하지 않고 구조를 목표 형태로 강제한다.
//   (구 버전 패널 구조 — 루트 VLG, HeaderRow, X 텍스트 닫기버튼 — 은 자동 정리/이관.)
// 안전 배선: 이름 문자열 매칭이 아니라 "타입(FindFirstObjectByType) + 생성 참조 직접 Connect"
//            으로 컴포넌트를 특정한다(.claude 교훈: 이름 기반 매칭 오연결 위험).
// 에셋 미발견 시: 추정 배선 금지 — 폴백(단색/기본폰트) 사용 후 콘솔 경고로 사용자에게 안내.
//
// Editor 전용 — Assets/_Project/Scripts/Editor/ 하위(Assembly-CSharp-Editor). 빌드 미포함.
// ============================================================================

using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;                 // NetworkObject
using Hexiege.Bootstrap;             // GameBootstrapper
using Hexiege.Presentation;          // ResearchPanelUI, ProductionPanelUI
using Hexiege.Infrastructure;        // NetworkUpgradeController

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 연구소 강화 시스템의 씬 오브젝트/참조/UI 를 멱등하게 배선하는 1회성 에디터 스크립트.
    /// 생산 패널의 실제 에셋을 라이브로 하베스트해 연구 패널을 "생산 패널 룩"으로 재구성한다.
    /// </summary>
    public static class WireUpgradeSystem
    {
        // NetworkUpgradeController / ResearchPanelUI 가 배치될 씬. 열려 있지 않으면 이 씬을 연다.
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        // 공용 UI 색상 설정 에셋(ResearchPanelUI._colorConfig 에 배선).
        private const string UIColorConfigPath = "Assets/_Project/Resources/Config/UIColorConfig.asset";

        // 폰트(생산 패널에서 하베스트 실패 시의 폴백 경로). GameSystemRules_UI 규칙 6.
        private const string LightFontPath = "Assets/_Project/Fonts/Maplestory Light SDF.asset";
        private const string BoldFontPath = "Assets/_Project/Fonts/Maplestory Bold SDF.asset";

        // ── 배선 대상 필드명(정확히 일치해야 한다) ──────────────────────────
        private const string BootstrapperNetCtrlField = "_networkUpgradeController"; // GameBootstrapper
        private const string BootstrapperPanelField = "_researchPanelUI";            // GameBootstrapper
        private const string PanelGroupField = "_panelGroup";                        // ResearchPanelUI
        private const string PanelColorField = "_colorConfig";                       // ResearchPanelUI

        private const string NetworkControllerObjectName = "NetworkUpgradeController";
        private const string ResearchPanelObjectName = "ResearchPanel";

        // ── 트랙 행 드라이버/오브젝트/필드명 ───────────────────────────────────
        private const string TrackContainerName = "TrackContainer";   // 행이 배치될 컨테이너
        private const string RowTemplateName = "RowTemplate";         // 행 프로토타입(비활성)
        private const string PlaceholderNoteName = "PlaceholderNote"; // 구 안내 텍스트(있으면 제거)
        private const string LegacyHeaderRowName = "HeaderRow";       // 구 헤더 래퍼(있으면 제거·이관)

        private const string DriverPanelField = "_panel";
        private const string DriverTemplateField = "_rowTemplate";
        private const string DriverContainerField = "_rowContainer";

        // ====================================================================
        // 메인 메뉴 — 씬/UI 배선
        // ====================================================================

        [MenuItem("Hexiege/Setup/Wire Upgrade System")]
        public static void Run()
        {
            // ── 0) Game 씬 확보 ─────────────────────────────────────────────
            bool sceneOpenedByThis;
            if (!EnsureGameSceneOpen(out sceneOpenedByThis))
                return;

            // ── 0-1) GameBootstrapper(조합 루트) 특정 ───────────────────────
            GameBootstrapper bootstrapper =
                Object.FindFirstObjectByType<GameBootstrapper>(FindObjectsInactive.Include);
            if (bootstrapper == null)
            {
                Debug.LogError("[UpgradeSetup] GameBootstrapper 를 씬에서 찾지 못했습니다. " +
                               "Game.unity 씬을 연 상태에서 실행하세요. 배선을 중단합니다.");
                return;
            }

            Scene scene = bootstrapper.gameObject.scene;

            // ── 0-2) 생산 패널 룩 하베스트(리플렉션으로 라이브 에셋 수집) ─────
            //   여기서 배경 스프라이트·폰트·닫기 아이콘·버튼 스프라이트·색상·Canvas SO 등을
            //   "지금 씬에 있는" ProductionPanelUI 에서 그대로 읽어 온다. GUID 하드코딩 없음.
            ProductionStyle style = HarvestProductionStyle();

            // ── 1) NetworkUpgradeController 씬 오브젝트 확보 + 배선 ──────────
            NetworkUpgradeController netCtrl = EnsureNetworkUpgradeController(scene);
            bool netWired = Connect(bootstrapper, BootstrapperNetCtrlField, netCtrl);
            Debug.Log(netWired
                ? "[UpgradeSetup] ✔ GameBootstrapper._networkUpgradeController 배선 완료."
                : "[UpgradeSetup] ✘ GameBootstrapper._networkUpgradeController 배선 실패(위 로그 참조).");

            // ── 2) ResearchPanelUI 오브젝트 확보 + 배선 + 룩 재구성 ─────────
            ResearchPanelUI panel = EnsureResearchPanel(scene, style);
            if (panel != null)
            {
                bool panelWired = Connect(bootstrapper, BootstrapperPanelField, panel);
                Debug.Log(panelWired
                    ? "[UpgradeSetup] ✔ GameBootstrapper._researchPanelUI 배선 완료."
                    : "[UpgradeSetup] ✘ GameBootstrapper._researchPanelUI 배선 실패(위 로그 참조).");

                // ── 2-1) 트랙 행 드라이버 + 행 템플릿 생성/배선 ───────────────
                EnsureTrackDriver(panel, style);
            }
            else
            {
                Debug.LogWarning("[UpgradeSetup] ResearchPanelUI 생성/확보에 실패하여 " +
                                 "GameBootstrapper._researchPanelUI 배선을 건너뜁니다.");
            }

            // ── 3) 저장/더티 처리 ───────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(scene);
            if (sceneOpenedByThis)
                EditorSceneManager.SaveScene(scene);

            // ── 4) 완료 안내 ────────────────────────────────────────────────
            Debug.Log(
                "[UpgradeSetup] 연구 패널 재구성 완료(생산 패널 룩 하베스트 적용).\n" +
                "  · 배경/폰트/닫기아이콘/버튼/색상 = 생산 패널에서 라이브로 재사용.\n" +
                "  · Canvas 오버라이드(SO) + GraphicRaycaster 추가로 BlockingOverlay 위에 정상 표시.\n" +
                "  · 트랙 행 = [트랙명][레벨][비용·시간 / 진행바 / MAX][연구 버튼] 가독성 레이아웃.\n" +
                "  ※ 하베스트 실패 항목이 있으면 위 경고 로그를 확인하세요(폴백 사용됨).\n" +
                "  ※ 씬을 저장(Ctrl+S)하세요." + (sceneOpenedByThis ? " (이 스크립트가 자동 저장했습니다.)" : ""));

            Selection.activeObject = panel != null ? (Object)panel.gameObject : bootstrapper.gameObject;
        }

        // ====================================================================
        // 생산 패널 룩 하베스트 (리플렉션)
        // ====================================================================

        /// <summary>
        /// 생산 패널(ProductionPanelUI)의 실제 컴포넌트에서 시각 스타일을 수집한 결과.
        /// 하나라도 못 찾으면 해당 항목만 폴백 값으로 채워지고 valid=false 항목은 경고 로그가 남는다.
        /// </summary>
        private struct ProductionStyle
        {
            public bool valid;                 // 생산 패널 자체를 찾았는지

            // 프레임(배경).
            public Sprite bgSprite;
            public Color bgColor;
            public Material bgMaterial;
            public Image.Type bgType;
            public Vector2 frameAnchorMin;     // 하단 절반 전체너비 등 생산 프레임 배치
            public Vector2 frameAnchorMax;
            public Vector2 framePivot;

            // Canvas 정렬(오버레이 위로 올리기).
            public bool hasCanvas;
            public int canvasSortingOrder;

            // 헤더 텍스트.
            public TMP_FontAsset headerFont;
            public Color headerColor;

            // 본문(수치) 텍스트.
            public TMP_FontAsset bodyFont;
            public Color bodyColor;

            // 닫기 아이콘 버튼.
            public Sprite closeSprite;
            public Color closeColor;
            public Vector2 closeAnchorMin;
            public Vector2 closeAnchorMax;
            public Vector2 closePivot;

            // 게임 버튼/슬롯 프레임 스프라이트(연구 버튼·행 배경 재사용).
            public Sprite buttonSprite;
            public Image.Type buttonType;
        }

        /// <summary>
        /// 씬의 ProductionPanelUI 에서 룩 에셋을 리플렉션으로 수집한다.
        /// 생산 패널이 없거나 특정 항목을 못 찾으면 폴백 값 + 경고 로그로 안전하게 처리한다.
        /// </summary>
        private static ProductionStyle HarvestProductionStyle()
        {
            var s = new ProductionStyle
            {
                // 폴백 기본값(생산 패널을 못 찾았을 때 사용).
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
            };

            ProductionPanelUI prod =
                Object.FindFirstObjectByType<ProductionPanelUI>(FindObjectsInactive.Include);
            if (prod == null)
            {
                Debug.LogWarning("[UpgradeSetup] 씬에서 ProductionPanelUI 를 찾지 못해 룩 하베스트를 생략합니다. " +
                                 "폴백(단색 배경·기본 폰트)으로 연구 패널을 구성합니다. 스크린샷 확인 필요.");
                s.headerFont = LoadFont(BoldFontPath);
                s.bodyFont = LoadFont(LightFontPath);
                s.valid = false;
                return s;
            }
            s.valid = true;
            GameObject prodRoot = prod.gameObject; // ProductionPopup = 생산 패널 루트

            // ── Canvas(SO) ──────────────────────────────────────────────────
            if (prodRoot.TryGetComponent(out Canvas prodCanvas))
            {
                s.hasCanvas = true;
                s.canvasSortingOrder = prodCanvas.overrideSorting ? prodCanvas.sortingOrder : 200;
            }
            else
            {
                Debug.LogWarning("[UpgradeSetup] 생산 패널 루트에 Canvas 오버라이드가 없습니다. 연구 패널 SO=200 폴백 사용.");
            }

            // ── 프레임 배경 이미지 ──────────────────────────────────────────
            //   생산 패널 루트의 첫 자식(프레임)에서 스프라이트 있는 Image 를 우선 사용.
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

            // ── 헤더 폰트/색상 ──────────────────────────────────────────────
            var headerText = GetPrivateField(prod, "_headerText") as TextMeshProUGUI;
            if (headerText != null)
            {
                s.headerFont = headerText.font;
                s.headerColor = headerText.color;
            }
            if (s.headerFont == null)
            {
                s.headerFont = LoadFont(BoldFontPath);
                Debug.LogWarning("[UpgradeSetup] 생산 패널 헤더 폰트를 못 읽어 Bold SDF 폴백 사용.");
            }

            // ── 본문(수치) 폰트/색상 — 유닛 비용 텍스트 기준 ───────────────────
            var costText = FirstUnityObject<TextMeshProUGUI>(GetPrivateField(prod, "_unitCostTexts"));
            if (costText != null)
            {
                s.bodyFont = costText.font;
                s.bodyColor = costText.color;
            }
            if (s.bodyFont == null)
            {
                s.bodyFont = LoadFont(LightFontPath);
                Debug.LogWarning("[UpgradeSetup] 생산 패널 본문 폰트를 못 읽어 Light SDF 폴백 사용.");
            }

            // ── 닫기 아이콘 버튼 ────────────────────────────────────────────
            var cancelBtn = GetPrivateField(prod, "_cancelButton") as Button;
            if (cancelBtn != null)
            {
                Image ci = cancelBtn.targetGraphic as Image;
                if (ci == null) cancelBtn.TryGetComponent(out ci);
                if (ci != null) { s.closeSprite = ci.sprite; s.closeColor = ci.color; }
                var crt = cancelBtn.GetComponent<RectTransform>();
                if (crt != null)
                {
                    s.closeAnchorMin = crt.anchorMin;
                    s.closeAnchorMax = crt.anchorMax;
                    s.closePivot = crt.pivot;
                }
            }
            if (s.closeSprite == null)
                Debug.LogWarning("[UpgradeSetup] 생산 패널 닫기 버튼 아이콘 스프라이트를 못 읽었습니다. 단색 닫기 버튼 폴백 사용. 스크린샷 확인 필요.");

            // ── 게임 버튼/슬롯 프레임 스프라이트 — 유닛 버튼 이미지 기준 ────────
            var unitBtn = FirstUnityObject<Button>(GetPrivateField(prod, "_unitButtons"));
            if (unitBtn != null)
            {
                Image bi = unitBtn.targetGraphic as Image;
                if (bi == null) unitBtn.TryGetComponent(out bi);
                if (bi != null) { s.buttonSprite = bi.sprite; s.buttonType = bi.type; }
            }
            if (s.buttonSprite == null)
                Debug.LogWarning("[UpgradeSetup] 생산 패널 유닛 버튼 스프라이트를 못 읽었습니다. 연구/행 배경은 단색 폴백 사용. 스크린샷 확인 필요.");

            return s;
        }

        /// <summary>
        /// 생산 패널 루트에서 배경 프레임 Image 를 찾는다.
        ///   1순위: 첫 자식(생산 패널은 루트 아래 프레임 1개 구조)의 Image(스프라이트 보유).
        ///   2순위: 자손 중 스프라이트를 가진 첫 Image.
        /// </summary>
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

        /// <summary>
        /// 씬에서 NetworkUpgradeController 를 타입으로 찾고, 없으면 새로 만들어
        /// NetworkObject + NetworkUpgradeController 컴포넌트를 부착한다(멱등).
        /// </summary>
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

        /// <summary> GameObject 에 NetworkObject 가 없으면 부착한다(멱등). </summary>
        private static void EnsureNetworkObject(GameObject go)
        {
            if (!go.TryGetComponent(out NetworkObject _))
            {
                go.AddComponent<NetworkObject>();
                Debug.Log($"[UpgradeSetup] '{go.name}' 에 NetworkObject 컴포넌트를 부착했습니다.");
            }
        }

        // ====================================================================
        // [2] ResearchPanelUI 오브젝트 + 생산 패널 룩 컨테이너
        // ====================================================================

        /// <summary>
        /// 씬에서 ResearchPanelUI 를 타입으로 찾고, 없으면 게임 Canvas 하위에 만든 뒤
        /// 생산 패널 룩으로 컨테이너를 재구성한다(멱등). _panelGroup · _colorConfig 배선.
        /// </summary>
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
                // 게임 UI 부모(Canvas) 특정 — 생산 패널의 부모를 재사용(타입 기반, SafeArea 정합).
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

            // ── 생산 패널 룩으로 컨테이너 재구성(멱등·구버전 이관) ──────────
            BuildPanelContainer(panelGo, panel, style);

            // ── CanvasGroup(_panelGroup) 확보 + 배선 ────────────────────────
            CanvasGroup cg = Ensure<CanvasGroup>(panelGo);
            bool groupWired = Connect(panel, PanelGroupField, cg);
            Debug.Log(groupWired
                ? "[UpgradeSetup] ✔ ResearchPanelUI._panelGroup(CanvasGroup) 배선 완료."
                : "[UpgradeSetup] ✘ ResearchPanelUI._panelGroup 배선 실패.");

            // ── UIColorConfig(_colorConfig) 배선 ────────────────────────────
            var colorConfig = AssetDatabase.LoadAssetAtPath<UIColorConfig>(UIColorConfigPath);
            if (colorConfig == null)
            {
                Debug.LogWarning($"[UpgradeSetup] UIColorConfig 에셋을 찾지 못했습니다: {UIColorConfigPath} " +
                                 "(_colorConfig 는 비워둡니다 — 골드 부족 색상은 코드 폴백 Color.red 사용).");
            }
            else
            {
                bool colorWired = Connect(panel, PanelColorField, colorConfig);
                Debug.Log(colorWired
                    ? "[UpgradeSetup] ✔ ResearchPanelUI._colorConfig 배선 완료."
                    : "[UpgradeSetup] ✘ ResearchPanelUI._colorConfig 배선 실패.");
            }

            EditorUtility.SetDirty(panel);
            return panel;
        }

        /// <summary>
        /// 연구 패널 컨테이너를 "생산 패널 룩"으로 강제 구성한다(멱등).
        ///   루트 = 하단 절반·전체 너비 프레임(배경 스프라이트) + Canvas 오버라이드(SO) + GraphicRaycaster.
        ///   자식 = Title(헤더 폰트) + CloseButton(닫기 아이콘, 우상단) + TrackContainer(행 목록 VLG).
        /// 구버전 구조(루트 VLG, HeaderRow, X 텍스트 버튼)는 자동 제거/이관한다.
        /// </summary>
        private static void BuildPanelContainer(GameObject panelGo, ResearchPanelUI panel, ProductionStyle s)
        {
            // ── 구버전 구조 정리(멱등 이관) ─────────────────────────────────
            //   과거 스켈레톤은 루트에 VLG/Image + HeaderRow(제목+X버튼)를 두었다.
            //   새 구조는 자식을 앵커로 절대 배치하므로 루트의 레이아웃 그룹을 제거한다.
            RemoveComponent<VerticalLayoutGroup>(panelGo);
            RemoveComponent<HorizontalLayoutGroup>(panelGo);
            RemoveComponent<ContentSizeFitter>(panelGo);
            DeleteChild(panelGo.transform, LegacyHeaderRowName);
            DeleteChild(panelGo.transform, PlaceholderNoteName);

            // ── 루트 RectTransform = 생산 프레임 배치(하단 절반 전체 너비) ─────
            var rt = panelGo.GetComponent<RectTransform>();
            SetRect(rt, s.frameAnchorMin, s.frameAnchorMax, s.framePivot);

            // ── 배경 이미지(스프라이트 재사용) ──────────────────────────────
            Image bg = Ensure<Image>(panelGo);
            if (s.bgSprite != null)
            {
                bg.sprite = s.bgSprite;
                bg.type = s.bgType;
                bg.color = s.bgColor;
                bg.material = s.bgMaterial;
            }
            else
            {
                bg.sprite = null;
                bg.color = new Color(0.10f, 0.12f, 0.16f, 0.97f); // 폴백 단색
            }
            bg.raycastTarget = true; // 패널 뒤 입력 차단(팝업 본체)

            // ── Canvas 오버라이드 — BlockingOverlay(SO=100) 위로 올림 ─────────
            var canvas = Ensure<Canvas>(panelGo);
            canvas.overrideSorting = true;
            canvas.sortingOrder = s.canvasSortingOrder;
            Ensure<GraphicRaycaster>(panelGo);

            // ── 헤더 제목 ───────────────────────────────────────────────────
            GameObject titleGo = FindOrCreateChild(panelGo.transform, "Title");
            SetRect(titleGo.GetComponent<RectTransform>(),
                new Vector2(0.05f, 0.84f), new Vector2(s.closeAnchorMin.x - 0.02f, 0.98f), new Vector2(0f, 1f));
            TextMeshProUGUI title = EnsureText(titleGo, s.headerFont, "연구소 강화", 34, TextAlignmentOptions.Left);
            title.color = s.headerColor;

            // ── 닫기 버튼(아이콘, 우상단 — 생산 패널 위치 재사용) ────────────
            GameObject closeGo = FindOrCreateChild(panelGo.transform, "CloseButton");
            SetRect(closeGo.GetComponent<RectTransform>(), s.closeAnchorMin, s.closeAnchorMax, s.closePivot);
            Button closeBtn = EnsureIconButton(closeGo, s.closeSprite, s.closeColor);
            // 닫기 → ResearchPanelUI.Close() persistent 리스너(멱등: 기존 제거 후 추가).
            WirePersistentClick(closeBtn.onClick, panel.Close);

            // ── 트랙 컨테이너(행 목록 VLG) — 헤더 아래 영역 채움 ───────────────
            GameObject tracks = FindOrCreateChild(panelGo.transform, TrackContainerName);
            SetRect(tracks.GetComponent<RectTransform>(),
                new Vector2(0.035f, 0.04f), new Vector2(0.965f, 0.80f), new Vector2(0.5f, 0.5f));
            VerticalLayoutGroup vlg = EnsureVLG(tracks);
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;    // 행은 고정 preferredHeight (MEMORY 교훈)
            vlg.childAlignment = TextAnchor.UpperCenter;

            // ── 초기 숨김(Initialize 의 HidePanel 과 동일 상태로 명시) ────────
            CanvasGroup cg = Ensure<CanvasGroup>(panelGo);
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            EditorUtility.SetDirty(panelGo);
        }

        /// <summary>
        /// 게임 UI 부모(Canvas Transform)를 특정한다.
        ///   1순위: 씬의 ProductionPanelUI 의 부모(같은 Canvas 하위, SafeArea 정합).
        ///   2순위: 씬의 아무 Canvas.
        /// </summary>
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
        // [A] 트랙 행 드라이버 + 행 템플릿
        // ====================================================================

        /// <summary>
        /// 트랙 행 드라이버(ResearchTrackListView)와 행 템플릿(RowTemplate)을 멱등 생성/배선한다.
        /// </summary>
        private static void EnsureTrackDriver(ResearchPanelUI panel, ProductionStyle style)
        {
            GameObject panelGo = panel.gameObject;

            GameObject tracks = FindOrCreateChild(panelGo.transform, TrackContainerName);
            EnsureVLG(tracks);

            // 구 안내 텍스트(PlaceholderNote) 잔존 시 제거.
            DeleteChild(tracks.transform, PlaceholderNoteName);

            // 행 템플릿 생성/확보 + 필드 배선.
            ResearchTrackRowView rowTemplate = BuildRowTemplate(tracks, style);

            // 드라이버 컴포넌트 확보 + 배선.
            if (!tracks.TryGetComponent(out ResearchTrackListView driver))
                driver = tracks.AddComponent<ResearchTrackListView>();

            Connect(driver, DriverPanelField, panel);
            Connect(driver, DriverTemplateField, rowTemplate);
            Connect(driver, DriverContainerField, tracks.transform);

            EditorUtility.SetDirty(driver);
            EditorUtility.SetDirty(rowTemplate);
            Debug.Log("[UpgradeSetup] ✔ ResearchTrackListView(행 드라이버) + RowTemplate 배선 완료.");
        }

        /// <summary>
        /// 트랙 한 행의 프로토타입(RowTemplate)을 만들고 ResearchTrackRowView 필드를 배선한다.
        /// 생산 패널의 폰트/버튼 스프라이트를 재사용해 룩을 맞춘다. 항상 비활성(드라이버가 복제).
        /// 레이아웃: [트랙명][레벨][StateArea: NormalGroup/ProgressGroup/MaxGroup 겹침][—].
        /// </summary>
        private static ResearchTrackRowView BuildRowTemplate(GameObject container, ProductionStyle s)
        {
            const float RowHeight = 76f; // 누르기 쉬운 크기(모바일). 스크린샷 후 튜닝 대상.

            GameObject rowGo = FindOrCreateChild(container.transform, RowTemplateName);
            HorizontalLayoutGroup rowHlg = EnsureHLG(rowGo, 8f);
            rowHlg.padding = new RectOffset(12, 12, 6, 6);
            rowHlg.childForceExpandHeight = true;
            SetPreferredHeight(rowGo, RowHeight);

            // 행 배경(슬롯/버튼 프레임 스프라이트 재사용 — 리스트 항목처럼 보이게).
            Image rowBg = Ensure<Image>(rowGo);
            if (s.buttonSprite != null)
            {
                rowBg.sprite = s.buttonSprite;
                rowBg.type = s.buttonType;
                rowBg.color = new Color(1f, 1f, 1f, 0.9f);
            }
            else
            {
                rowBg.sprite = null;
                rowBg.color = new Color(1f, 1f, 1f, 0.06f); // 폴백: 아주 옅은 패널
            }
            rowBg.raycastTarget = false; // 행 배경이 버튼 입력을 가로채지 않도록

            if (!rowGo.TryGetComponent(out ResearchTrackRowView row))
                row = rowGo.AddComponent<ResearchTrackRowView>();

            // ── 트랙명 ──
            GameObject nameGo = FindOrCreateChild(rowGo.transform, "Name");
            nameGo.transform.SetSiblingIndex(0);
            SetFlexibleWidth(nameGo, 2.6f);
            TextMeshProUGUI nameText = EnsureText(nameGo, s.headerFont, "트랙", 26, TextAlignmentOptions.MidlineLeft);
            nameText.color = s.headerColor;

            // ── 레벨 ──
            GameObject lvGo = FindOrCreateChild(rowGo.transform, "Level");
            lvGo.transform.SetSiblingIndex(1);
            SetFlexibleWidth(lvGo, 1.1f);
            TextMeshProUGUI levelText = EnsureText(lvGo, s.bodyFont, "Lv 0/5", 22, TextAlignmentOptions.Center);
            levelText.color = s.bodyColor;

            // ── 상태 영역(3그룹 겹침 stretch) ──
            GameObject stateGo = FindOrCreateChild(rowGo.transform, "StateArea");
            stateGo.transform.SetSiblingIndex(2);
            SetFlexibleWidth(stateGo, 3.0f);

            // ── 일반 그룹(비용/시간/연구 버튼) ──
            GameObject normalGo = FindOrCreateChild(stateGo.transform, "NormalGroup");
            SetStretch(normalGo.GetComponent<RectTransform>());
            CanvasGroup normalCg = Ensure<CanvasGroup>(normalGo);
            EnsureHLG(normalGo, 6f);

            GameObject costGo = FindOrCreateChild(normalGo.transform, "Cost");
            costGo.transform.SetSiblingIndex(0);
            SetFlexibleWidth(costGo, 1f);
            TextMeshProUGUI costText = EnsureText(costGo, s.bodyFont, "0", 22, TextAlignmentOptions.Center);
            costText.color = s.bodyColor;

            GameObject timeGo = FindOrCreateChild(normalGo.transform, "Time");
            timeGo.transform.SetSiblingIndex(1);
            SetFlexibleWidth(timeGo, 0.9f);
            TextMeshProUGUI timeText = EnsureText(timeGo, s.bodyFont, "0s", 20, TextAlignmentOptions.Center);
            timeText.color = new Color(s.bodyColor.r, s.bodyColor.g, s.bodyColor.b, 0.75f);

            GameObject btnGo = FindOrCreateChild(normalGo.transform, "ResearchButton");
            btnGo.transform.SetSiblingIndex(2);
            SetFlexibleWidth(btnGo, 1.3f);
            Button researchBtn = EnsureSpriteButton(btnGo, s.headerFont, "연구",
                s.buttonSprite, s.buttonType, new Color(0.20f, 0.45f, 0.30f, 1f), 22);

            // ── 진행 그룹(진행 바 + 남은 시간) ──
            GameObject progGo = FindOrCreateChild(stateGo.transform, "ProgressGroup");
            SetStretch(progGo.GetComponent<RectTransform>());
            CanvasGroup progCg = Ensure<CanvasGroup>(progGo);
            EnsureImage(progGo, new Color(0.08f, 0.09f, 0.12f, 1f)); // 바 배경

            GameObject fillGo = FindOrCreateChild(progGo.transform, "Fill");
            SetStretch(fillGo.GetComponent<RectTransform>());
            Image progFill = EnsureFilledImage(fillGo, new Color(0.25f, 0.55f, 0.85f, 1f));

            GameObject timerGo = FindOrCreateChild(progGo.transform, "Timer");
            SetStretch(timerGo.GetComponent<RectTransform>());
            TextMeshProUGUI progText = EnsureText(timerGo, s.headerFont, "0s", 22, TextAlignmentOptions.Center);

            // ── 최대 그룹(MAX) ──
            GameObject maxGo = FindOrCreateChild(stateGo.transform, "MaxGroup");
            SetStretch(maxGo.GetComponent<RectTransform>());
            CanvasGroup maxCg = Ensure<CanvasGroup>(maxGo);
            TextMeshProUGUI maxText = EnsureText(maxGo, s.headerFont, "MAX", 24, TextAlignmentOptions.Center);
            maxText.color = new Color(1f, 0.85f, 0.3f, 1f);

            // 초기 표시(일반만 — 런타임 Refresh 가 상태에 맞게 다시 정한다).
            SetGroupInitial(normalCg, true);
            SetGroupInitial(progCg, false);
            SetGroupInitial(maxCg, false);

            // ── ResearchTrackRowView 필드 배선(이름 매칭 아님 — 생성 참조 직접 Connect) ──
            Connect(row, "_nameText", nameText);
            Connect(row, "_levelText", levelText);
            Connect(row, "_normalGroup", normalCg);
            Connect(row, "_costText", costText);
            Connect(row, "_timeText", timeText);
            Connect(row, "_researchButton", researchBtn);
            Connect(row, "_progressGroup", progCg);
            Connect(row, "_progressFill", progFill);
            Connect(row, "_progressText", progText);
            Connect(row, "_maxGroup", maxCg);

            rowGo.SetActive(false); // 템플릿은 항상 비활성(드라이버가 복제하여 활성화).
            return row;
        }

        private static void SetGroupInitial(CanvasGroup cg, bool visible)
        {
            cg.alpha = visible ? 1f : 0f;
            cg.interactable = visible;
            cg.blocksRaycasts = visible;
        }

        // ====================================================================
        // 선택 메뉴 — 디버그 핫키 부착
        // ====================================================================

        /// <summary>
        /// GameBootstrapper 오브젝트에 UpgradeDebugHotkeys 를 부착한다(멱등).
        /// F1~F4 로 지정 팀/그룹의 Attack/Defense/MoveSpeed/Regen 레벨을 +1 한다.
        /// </summary>
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

        /// <summary>
        /// Game 씬이 열려 있지 않으면 연다. 성공 시 true, 저장 대화상자 취소 시 false.
        /// </summary>
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

        /// <summary>
        /// 대상 오브젝트의 private/protected 인스턴스 필드 값을 리플렉션으로 읽는다.
        /// 상속 계층(BaseType)을 따라 올라가며 탐색한다. 없으면 null.
        /// </summary>
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

        /// <summary>
        /// IList(List/배열)에서 파괴되지 않은 첫 UnityEngine.Object 요소를 T 로 반환한다.
        /// (Unity 의 "가짜 null" 도 안전하게 걸러낸다.)
        /// </summary>
        private static T FirstUnityObject<T>(object listObj) where T : Object
        {
            if (listObj is IList list)
            {
                foreach (var e in list)
                {
                    if (e is T t && t != null) return t;
                }
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

        /// <summary>
        /// private [SerializeField] 슬롯을 SerializedObject 로 안전하게 연결한다.
        /// </summary>
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

        /// <summary> UnityEvent(onClick)에 persistent 리스너를 멱등하게 연결한다(기존 제거 후 추가). </summary>
        private static void WirePersistentClick(Button.ButtonClickedEvent onClick, UnityAction call)
        {
            for (int i = onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(onClick, i);
            UnityEventTools.AddPersistentListener(onClick, call);
        }

        // ====================================================================
        // UI 생성 헬퍼
        // ====================================================================

        /// <summary> 컴포넌트가 없으면 부착하고, 있으면 그대로 반환(멱등). </summary>
        private static T Ensure<T>(GameObject go) where T : Component
        {
            if (!go.TryGetComponent(out T c)) c = go.AddComponent<T>();
            return c;
        }

        /// <summary> 해당 타입 컴포넌트가 있으면 즉시 제거(구버전 구조 정리용). </summary>
        private static void RemoveComponent<T>(GameObject go) where T : Component
        {
            if (go.TryGetComponent(out T c)) Object.DestroyImmediate(c);
        }

        /// <summary> 이름이 일치하는 직속 자식이 있으면 제거(구버전 구조 정리용). </summary>
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

        private static VerticalLayoutGroup EnsureVLG(GameObject go) => Ensure<VerticalLayoutGroup>(go);

        private static HorizontalLayoutGroup EnsureHLG(GameObject go, float spacing)
        {
            HorizontalLayoutGroup hlg = Ensure<HorizontalLayoutGroup>(go);
            hlg.spacing = spacing;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
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
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Simple;
                img.color = color;
            }
            else
            {
                img.sprite = null;
                img.color = new Color(0.55f, 0.20f, 0.20f, 1f); // 폴백 단색
            }
            Button btn = Ensure<Button>(go);
            btn.targetGraphic = img;
            DeleteChild(go.transform, "Label"); // 구버전 X 텍스트 제거
            return btn;
        }

        /// <summary> 스프라이트 배경 + 텍스트 라벨 버튼(연구 버튼). 스프라이트 없으면 폴백 단색. </summary>
        private static Button EnsureSpriteButton(
            GameObject go, TMP_FontAsset font, string label,
            Sprite sprite, Image.Type spriteType, Color fallbackColor, int fontSize)
        {
            Image img = Ensure<Image>(go);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = spriteType;
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = fallbackColor;
            }

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

        /// <summary> 앵커 min/max/pivot 을 지정하고 offset 을 0으로 맞춘다(비율 기반 배치). </summary>
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

        /// <summary> Filled 타입 Image(진행 바). 스프라이트 없이도 흰 텍스처로 채워진다. </summary>
        private static Image EnsureFilledImage(GameObject go, Color color)
        {
            Image img = EnsureImage(go, color);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 0f;
            return img;
        }
    }
}
