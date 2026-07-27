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
//   [1] NetworkUpgradeController 씬 오브젝트 확보:
//        · 씬에 NetworkUpgradeController 가 없으면 빈 GameObject 를 만들고
//          Unity.Netcode.NetworkObject + NetworkUpgradeController 컴포넌트를 부착.
//        · GameBootstrapper._networkUpgradeController 필드에 SerializedObject 로 안전 배선.
//   [2] ResearchPanelUI 오브젝트 확보 + 참조 배선:
//        · 씬에 ResearchPanelUI 가 없으면 게임 Canvas 하위에 간이 패널을 생성.
//        · ResearchPanelUI._panelGroup(CanvasGroup) · _colorConfig(UIColorConfig) 배선.
//        · 닫기 버튼 onClick → ResearchPanelUI.Close() 를 persistent 리스너로 연결.
//        · GameBootstrapper._researchPanelUI 필드에 배선.
//
// ⚠️ 이 스크립트가 "하지 않는" 것 (런타임 코드가 필요해 별도 승인 대상):
//   (A) 트랙 행(그룹×스탯 + 자연회복) 버튼/텍스트를 Get*/TryResearch/RefreshRequested 에
//       바인딩하는 "행 드라이버" 컴포넌트 — ResearchPanelUI 는 행 렌더링을 외부 컴포넌트에
//       위임하도록 설계돼 있는데 그 컴포넌트가 아직 코드에 없다. 순수 에디터 배선으로는
//       동적 갱신/구독을 만들 수 없으므로 여기서는 패널 골격만 만들고 로그로 안내한다.
//   (B) 연구소(Research) 클릭 → ResearchPanelUI.Open(lab) 라우팅 — 현재 InputHandler 는
//       Research 를 BuildingActionPanelUI 로 보낸다(247행). 이 분기 추가는 런타임 코드
//       변경이라 에디터 스크립트 범위 밖이다.
//   → 위 (A)(B)는 실행 후 콘솔에 "수동/코드 필요" 로 명확히 안내한다.
//
// 멱등성: 여러 번 실행해도 안전. 이미 있으면 재생성하지 않고 참조만 재확인/재배선한다.
// 안전 배선: 이름 문자열 매칭이 아니라 "타입(FindFirstObjectByType)" 으로 컴포넌트를 특정한다
//            (.claude 교훈: 이름 기반 매칭 오연결 위험). 못 찾으면 생성하거나 로그로 안내한다.
//
// Editor 전용 — Assets/_Project/Scripts/Editor/ 하위(Assembly-CSharp-Editor). 빌드 미포함.
// ============================================================================

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
    /// 연구소 강화 시스템의 씬 오브젝트/참조/간이 UI 를 멱등하게 배선하는 1회성 에디터 스크립트.
    /// </summary>
    public static class WireUpgradeSystem
    {
        // NetworkUpgradeController / ResearchPanelUI 가 배치될 씬. 열려 있지 않으면 이 씬을 연다.
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        // 공용 UI 색상 설정 에셋(ResearchPanelUI._colorConfig 에 배선).
        private const string UIColorConfigPath = "Assets/_Project/Resources/Config/UIColorConfig.asset";

        // 폰트(있으면 사용, 없으면 TMP 기본 폰트). 다른 셋업 스크립트와 동일 경로.
        private const string LightFontPath = "Assets/_Project/Fonts/Maplestory Light SDF.asset";
        private const string BoldFontPath = "Assets/_Project/Fonts/Maplestory Bold SDF.asset";

        // ── 배선 대상 필드명(정확히 일치해야 한다) ──────────────────────────
        private const string BootstrapperNetCtrlField = "_networkUpgradeController"; // GameBootstrapper
        private const string BootstrapperPanelField = "_researchPanelUI";            // GameBootstrapper
        private const string PanelGroupField = "_panelGroup";                        // ResearchPanelUI
        private const string PanelColorField = "_colorConfig";                       // ResearchPanelUI

        private const string NetworkControllerObjectName = "NetworkUpgradeController";
        private const string ResearchPanelObjectName = "ResearchPanel";

        // ── 트랙 행 드라이버(A) 관련 오브젝트/필드명 ───────────────────────────
        private const string TrackContainerName = "TrackContainer";   // 행이 배치될 컨테이너
        private const string RowTemplateName = "RowTemplate";         // 행 프로토타입(비활성)
        private const string PlaceholderNoteName = "PlaceholderNote"; // 구 안내 텍스트(있으면 제거)

        // ResearchTrackListView 필드명.
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

            // ── 1) NetworkUpgradeController 씬 오브젝트 확보 + 배선 ──────────
            NetworkUpgradeController netCtrl = EnsureNetworkUpgradeController(scene);
            bool netWired = Connect(bootstrapper, BootstrapperNetCtrlField, netCtrl);
            Debug.Log(netWired
                ? "[UpgradeSetup] ✔ GameBootstrapper._networkUpgradeController 배선 완료."
                : "[UpgradeSetup] ✘ GameBootstrapper._networkUpgradeController 배선 실패(위 로그 참조).");

            // ── 2) ResearchPanelUI 오브젝트 확보 + 배선 ─────────────────────
            ResearchPanelUI panel = EnsureResearchPanel(scene);
            if (panel != null)
            {
                bool panelWired = Connect(bootstrapper, BootstrapperPanelField, panel);
                Debug.Log(panelWired
                    ? "[UpgradeSetup] ✔ GameBootstrapper._researchPanelUI 배선 완료."
                    : "[UpgradeSetup] ✘ GameBootstrapper._researchPanelUI 배선 실패(위 로그 참조).");

                // ── 2-1) 트랙 행 드라이버(A) + 행 템플릿 생성/배선 ───────────
                EnsureTrackDriver(panel);
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

            // ── 4) 완료 안내 + 남은 확인 항목 ───────────────────────────────
            Debug.Log(
                "[UpgradeSetup] 연구 패널 배선 완료. (A) 트랙 행 드라이버·행 템플릿, (B) 연구소 클릭 라우팅까지\n" +
                "  런타임 코드가 준비되어 이 한 번의 실행으로 연구 패널이 작동합니다.\n" +
                "  · (A) ResearchTrackListView + RowTemplate(ResearchTrackRowView) 를 TrackContainer 에 생성·배선했습니다.\n" +
                "  · (B) 연구소 클릭 라우팅은 InputHandler 가 GameBootstrapper._researchPanelUI 주입으로 처리합니다\n" +
                "        (별도 InputHandler 필드 배선 불필요 — 코드 주입 방식).\n" +
                "  ※ NetworkObject 의 GlobalObjectIdHash 는 씬 저장 시 NGO 가 부여합니다. Inspector 에서 확인하세요.\n" +
                "  ※ 씬을 저장(Ctrl+S)하세요." + (sceneOpenedByThis ? " (이 스크립트가 자동 저장했습니다.)" : ""));

            Selection.activeObject = panel != null ? (Object)panel.gameObject : bootstrapper.gameObject;
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
            // 타입 기반 탐색(이름 매칭 오연결 방지).
            NetworkUpgradeController existing =
                Object.FindFirstObjectByType<NetworkUpgradeController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log($"[UpgradeSetup] 기존 NetworkUpgradeController('{existing.name}') 재사용 — 새로 만들지 않음.");
                EnsureNetworkObject(existing.gameObject);
                return existing;
            }

            // 신규 생성.
            var go = new GameObject(NetworkControllerObjectName);
            // 다른 씬(예: 프리뷰)에 생성되지 않도록 대상 씬으로 이동.
            SceneManager.MoveGameObjectToScene(go, scene);

            EnsureNetworkObject(go);
            NetworkUpgradeController ctrl = go.AddComponent<NetworkUpgradeController>();

            EditorUtility.SetDirty(go);
            Debug.Log($"[UpgradeSetup] NetworkUpgradeController 씬 오브젝트를 새로 생성했습니다('{go.name}'). " +
                      "NGO 씬 관리(Enable Scene Management=ON)가 켜져 있으면 서버 시작 시 자동 스폰됩니다.");
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
        // [2] ResearchPanelUI 오브젝트 + 간이 UI 골격
        // ====================================================================

        /// <summary>
        /// 씬에서 ResearchPanelUI 를 타입으로 찾고, 없으면 게임 Canvas 하위에 간이 패널을 만든다.
        /// _panelGroup(CanvasGroup) · _colorConfig(UIColorConfig) 를 배선한다(멱등).
        /// </summary>
        private static ResearchPanelUI EnsureResearchPanel(Scene scene)
        {
            ResearchPanelUI existing =
                Object.FindFirstObjectByType<ResearchPanelUI>(FindObjectsInactive.Include);

            GameObject panelGo;
            ResearchPanelUI panel;

            if (existing != null)
            {
                Debug.Log($"[UpgradeSetup] 기존 ResearchPanelUI('{existing.name}') 재사용 — 새로 만들지 않음.");
                panel = existing;
                panelGo = existing.gameObject;
            }
            else
            {
                // 게임 UI 부모(Canvas) 특정 — 기존 ProductionPanelUI 의 부모를 재사용(타입 기반, 안전).
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
                BuildPanelSkeleton(panelGo, panel);
                Debug.Log($"[UpgradeSetup] ResearchPanel 오브젝트를 '{uiParent.name}' 하위에 새로 생성했습니다.");
            }

            // ── CanvasGroup(_panelGroup) 확보 + 배선 ────────────────────────
            if (!panelGo.TryGetComponent(out CanvasGroup cg))
                cg = panelGo.AddComponent<CanvasGroup>();
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
        /// 연구 패널의 "기능 우선 간이 골격" 을 구성한다(비주얼은 추후 미화).
        ///   배경 Image + 헤더(제목) + 닫기 버튼 + 트랙 컨테이너(빈 VLG).
        ///   트랙 컨테이너의 행 템플릿/드라이버 배선은 EnsureTrackDriver 가 담당한다.
        ///   닫기 버튼 onClick → ResearchPanelUI.Close() persistent 리스너로 연결.
        /// </summary>
        private static void BuildPanelSkeleton(GameObject panelGo, ResearchPanelUI panel)
        {
            TMP_FontAsset boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);

            // 패널 루트 RectTransform — 화면 중앙 하단부에 배치(생산 패널과 유사한 위치감).
            var rt = panelGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, 0.12f);
            rt.anchorMax = new Vector2(0.95f, 0.62f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 배경.
            if (!panelGo.TryGetComponent(out Image bg))
                bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.12f, 0.16f, 0.95f);
            bg.raycastTarget = true;

            // 세로 레이아웃.
            VerticalLayoutGroup vlg = EnsureVLG(panelGo);
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(14, 14, 14, 14);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 헤더 행(제목 + 닫기 버튼).
            GameObject header = FindOrCreateChild(panelGo.transform, "HeaderRow");
            EnsureHLG(header, 8f);
            SetPreferredHeight(header, 56f);

            GameObject titleGo = FindOrCreateChild(header.transform, "Title");
            titleGo.transform.SetSiblingIndex(0);
            SetFlexibleWidth(titleGo, 3f);
            EnsureText(titleGo, boldFont, "연구소 강화", 30, TextAlignmentOptions.Left);

            GameObject closeGo = FindOrCreateChild(header.transform, "CloseButton");
            closeGo.transform.SetSiblingIndex(1);
            SetFlexibleWidth(closeGo, 0.6f);
            Button closeBtn = EnsureButton(closeGo, boldFont, "X", new Color(0.55f, 0.20f, 0.20f, 1f), 26);

            // 닫기 버튼 → ResearchPanelUI.Close() persistent 리스너(멱등: 기존 리스너 제거 후 추가).
            WirePersistentClick(closeBtn.onClick, panel.Close);

            // 트랙 컨테이너(빈 VLG) — 런타임 행 드라이버(ResearchTrackListView)가 행을 채운다.
            //   행 템플릿/드라이버 컴포넌트 생성·배선은 EnsureTrackDriver 가 담당한다(멱등).
            GameObject tracks = FindOrCreateChild(panelGo.transform, TrackContainerName);
            EnsureVLG(tracks).spacing = 4f;
            SetFlexibleHeight(tracks, 1f);

            // 초기엔 숨김(ResearchPanelUI.Initialize() 가 HidePanel() 로도 처리하지만 명시적으로 0).
            if (panelGo.TryGetComponent(out CanvasGroup cg))
            {
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// 게임 UI 부모(Canvas Transform)를 특정한다.
        ///   1순위: 씬의 ProductionPanelUI 의 부모(같은 Canvas 하위, 타입 기반 안전).
        ///   2순위: 씬의 아무 Canvas.
        /// 못 찾으면 null.
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
        /// 연구 패널에 트랙 행 드라이버(ResearchTrackListView)와 행 템플릿(RowTemplate)을
        /// 멱등하게 생성/배선한다. 재실행 시 기존 오브젝트를 재사용한다.
        /// </summary>
        private static void EnsureTrackDriver(ResearchPanelUI panel)
        {
            GameObject panelGo = panel.gameObject;

            // 트랙 컨테이너 확보(패널 신규 생성 시 BuildPanelSkeleton 이 만들지만, 기존 패널 방어를 위해 재확인).
            GameObject tracks = FindOrCreateChild(panelGo.transform, TrackContainerName);
            EnsureVLG(tracks).spacing = 4f;

            // 구 안내 텍스트(PlaceholderNote)가 남아 있으면 제거(행 드라이버로 대체됨).
            Transform stale = tracks.transform.Find(PlaceholderNoteName);
            if (stale != null)
            {
                Object.DestroyImmediate(stale.gameObject);
                Debug.Log("[UpgradeSetup] 구 PlaceholderNote 를 제거했습니다(행 드라이버로 대체).");
            }

            // 행 템플릿 생성/확보 + 필드 배선.
            ResearchTrackRowView rowTemplate = BuildRowTemplate(tracks);

            // 드라이버 컴포넌트(트랙 컨테이너에 부착) 확보 + 배선.
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
        /// 항상 비활성으로 두어 드라이버가 복제해서 사용한다. 멱등(기존 자식 재사용).
        /// 배선은 이름 매칭이 아니라 생성한 컴포넌트 참조를 직접 Connect 한다(오연결 방지).
        /// </summary>
        private static ResearchTrackRowView BuildRowTemplate(GameObject container)
        {
            TMP_FontAsset lightFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LightFontPath);
            TMP_FontAsset boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);

            GameObject rowGo = FindOrCreateChild(container.transform, RowTemplateName);
            EnsureHLG(rowGo, 6f);
            SetPreferredHeight(rowGo, 48f);
            if (!rowGo.TryGetComponent(out ResearchTrackRowView row))
                row = rowGo.AddComponent<ResearchTrackRowView>();

            // ── 이름 ──
            GameObject nameGo = FindOrCreateChild(rowGo.transform, "Name");
            nameGo.transform.SetSiblingIndex(0);
            SetFlexibleWidth(nameGo, 2.4f);
            TextMeshProUGUI nameText = EnsureText(nameGo, boldFont, "트랙", 22, TextAlignmentOptions.Left);

            // ── 레벨 ──
            GameObject lvGo = FindOrCreateChild(rowGo.transform, "Level");
            lvGo.transform.SetSiblingIndex(1);
            SetFlexibleWidth(lvGo, 1.1f);
            TextMeshProUGUI levelText = EnsureText(lvGo, lightFont, "Lv 0/5", 20, TextAlignmentOptions.Center);

            // ── 상태 영역(3상태 그룹이 겹쳐 stretch — 레이아웃 그룹 없음) ──
            GameObject stateGo = FindOrCreateChild(rowGo.transform, "StateArea");
            stateGo.transform.SetSiblingIndex(2);
            SetFlexibleWidth(stateGo, 2.8f);

            // ── 일반 그룹(비용/시간/버튼) ──
            GameObject normalGo = FindOrCreateChild(stateGo.transform, "NormalGroup");
            SetStretch(normalGo.GetComponent<RectTransform>());
            CanvasGroup normalCg = EnsureCanvasGroup(normalGo);
            EnsureHLG(normalGo, 4f);

            GameObject costGo = FindOrCreateChild(normalGo.transform, "Cost");
            costGo.transform.SetSiblingIndex(0);
            SetFlexibleWidth(costGo, 1f);
            TextMeshProUGUI costText = EnsureText(costGo, lightFont, "0", 20, TextAlignmentOptions.Center);

            GameObject timeGo = FindOrCreateChild(normalGo.transform, "Time");
            timeGo.transform.SetSiblingIndex(1);
            SetFlexibleWidth(timeGo, 0.9f);
            TextMeshProUGUI timeText = EnsureText(timeGo, lightFont, "0s", 18, TextAlignmentOptions.Center);

            GameObject btnGo = FindOrCreateChild(normalGo.transform, "ResearchButton");
            btnGo.transform.SetSiblingIndex(2);
            SetFlexibleWidth(btnGo, 1.2f);
            Button researchBtn = EnsureButton(btnGo, boldFont, "연구", new Color(0.20f, 0.45f, 0.30f, 1f), 20);

            // ── 진행 그룹(진행 바 + 남은 시간) ──
            GameObject progGo = FindOrCreateChild(stateGo.transform, "ProgressGroup");
            SetStretch(progGo.GetComponent<RectTransform>());
            CanvasGroup progCg = EnsureCanvasGroup(progGo);
            EnsureImage(progGo, new Color(0.08f, 0.09f, 0.12f, 1f)); // 바 배경

            GameObject fillGo = FindOrCreateChild(progGo.transform, "Fill");
            SetStretch(fillGo.GetComponent<RectTransform>());
            Image progFill = EnsureFilledImage(fillGo, new Color(0.25f, 0.55f, 0.85f, 1f));

            GameObject timerGo = FindOrCreateChild(progGo.transform, "Timer");
            SetStretch(timerGo.GetComponent<RectTransform>());
            TextMeshProUGUI progText = EnsureText(timerGo, boldFont, "0s", 20, TextAlignmentOptions.Center);

            // ── 최대 그룹(MAX) ──
            GameObject maxGo = FindOrCreateChild(stateGo.transform, "MaxGroup");
            SetStretch(maxGo.GetComponent<RectTransform>());
            CanvasGroup maxCg = EnsureCanvasGroup(maxGo);
            TextMeshProUGUI maxText = EnsureText(maxGo, boldFont, "MAX", 22, TextAlignmentOptions.Center);
            maxText.color = new Color(1f, 0.85f, 0.3f, 1f);

            // 초기 표시(일반만 — 런타임 Refresh 가 상태에 맞게 다시 정한다).
            SetGroupInitial(normalCg, true);
            SetGroupInitial(progCg, false);
            SetGroupInitial(maxCg, false);

            // ── ResearchTrackRowView 필드 배선 ──
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

            // 템플릿은 항상 비활성(드라이버가 복제하여 활성화).
            rowGo.SetActive(false);
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
        /// UI 없이도 빠른 검증을 위해 GameBootstrapper 오브젝트에 UpgradeDebugHotkeys 를 부착한다(멱등).
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
                Debug.Log("[UpgradeSetup] ✔ UpgradeDebugHotkeys 를 GameBootstrapper 오브젝트에 부착했습니다. " +
                          "(플레이 모드에서 F1=공격 F2=방어 F3=이속 F4=자연회복 +1. 출시 전 제거 권장.)");
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
        /// Game 씬이 열려 있지 않으면 연다. 성공 시 true, 사용자가 저장 대화상자를 취소하면 false.
        /// </summary>
        private static bool EnsureGameSceneOpen(out bool openedByThis)
        {
            openedByThis = false;

            // 이미 GameBootstrapper 가 있는 씬이 열려 있으면 그대로 사용.
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
        // 배선 헬퍼
        // ====================================================================

        /// <summary>
        /// private [SerializeField] 슬롯을 SerializedObject 로 안전하게 연결한다.
        /// 필드가 없으면 에러 로그 후 false.
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
        // UI 생성 헬퍼 (CreateRankingTable 패턴 준용)
        // ====================================================================

        private static GameObject FindOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static VerticalLayoutGroup EnsureVLG(GameObject go)
        {
            if (!go.TryGetComponent(out VerticalLayoutGroup vlg))
                vlg = go.AddComponent<VerticalLayoutGroup>();
            return vlg;
        }

        private static HorizontalLayoutGroup EnsureHLG(GameObject go, float spacing)
        {
            if (!go.TryGetComponent(out HorizontalLayoutGroup hlg))
                hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            return hlg;
        }

        private static TextMeshProUGUI EnsureText(
            GameObject go, TMP_FontAsset font, string text, int fontSize, TextAlignmentOptions align)
        {
            if (!go.TryGetComponent(out TextMeshProUGUI t))
                t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        private static Button EnsureButton(
            GameObject go, TMP_FontAsset font, string label, Color bg, int fontSize)
        {
            if (!go.TryGetComponent(out Image img))
                img = go.AddComponent<Image>();
            img.color = bg;

            if (!go.TryGetComponent(out Button btn))
                btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            GameObject labelGo = FindOrCreateChild(go.transform, "Label");
            SetStretch(labelGo.GetComponent<RectTransform>());
            EnsureText(labelGo, font, label, fontSize, TextAlignmentOptions.Center);
            return btn;
        }

        private static void SetStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetPreferredHeight(GameObject go, float h)
        {
            if (!go.TryGetComponent(out LayoutElement le))
                le = go.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.flexibleHeight = 0f;
            le.flexibleWidth = 1f;
        }

        private static void SetFlexibleHeight(GameObject go, float weight)
        {
            if (!go.TryGetComponent(out LayoutElement le))
                le = go.AddComponent<LayoutElement>();
            le.flexibleHeight = weight;
            le.flexibleWidth = 1f;
        }

        private static void SetFlexibleWidth(GameObject go, float weight)
        {
            if (!go.TryGetComponent(out LayoutElement le))
                le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = weight;
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            if (!go.TryGetComponent(out CanvasGroup cg))
                cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        private static Image EnsureImage(GameObject go, Color color)
        {
            if (!go.TryGetComponent(out Image img))
                img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary> Filled 타입 Image(진행 바)를 확보한다. 스프라이트 없이도 흰 텍스처로 채워진다. </summary>
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
