// ============================================================================
// NetworkLagOverlaySetup.cs
// [1회성 Editor 스크립트 — 2026-11-26 인게임 이탈 판정 task, WORKFLOW [5-2]]
//   계획 원본: Assets/_Project/Docs/_Tasks/2026-11-26/10_31_ingame-leave-judgment/Plan.md 8절 · 3절 6단계 (U-17)
//
// 무엇을 하나 (유니티 초급자용):
//   Game.unity 에 「연결 지연 덮개」 오브젝트(NetworkLagOverlay)를 만들고 필요한 참조를 연결한 뒤 씬을 저장한다.
//   손으로 하면 Canvas 정렬 값 · 레이캐스터 · 참조 4개를 빠뜨리기 쉬워 메뉴 한 번으로 한다.
//
//   만드는 것:
//     [UI] (인게임 루트 Canvas)
//       └─ NetworkLagOverlay   ← 화면 전체(안전 영역 바깥까지) · 맨 마지막 형제(같은 Canvas 안에서 가장 앞)
//            Canvas(Override Sorting = 210) + GraphicRaycaster + CanvasGroup(숨김 상태) + Image(반투명 검정, 터치 받음)
//            + NetworkLagOverlayUI(참조 6개 연결)
//            └─ MessageText    ← 가운데 문구(TextMeshProUGUI). 글꼴은 결과 화면 상태 줄 글꼴을 그대로 복사한다.
//
//   연결하는 참조(NetworkLagOverlayUI):
//     _canvas / _group / _messageText        : 위에서 만든 자기 부품
//     _settingsButton                        : GameHudUI 의 _settingsButton (HUD 설정 여는 버튼)
//     _settingsUI                            : GameHudUI 의 _settingsUI (없으면 씬의 InGameSettingsUI)
//     _settingsPanelCanvas                   : InGameSettingsUI 의 _panel 이 붙은 오브젝트의 Canvas(평소 200)
//
// 왜 기존 패널을 「복제」하지 않고 새로 만드나:
//   씬의 기존 패널(BuildingActionPanel 등)은 버튼 · 레이아웃 · 패널 스크립트가 잔뜩 붙어 있어 복제하면 지울 것이 더 많다.
//   덮개는 「반투명 배경 + 글자 하나」라 새로 만들되, **보이는 스타일(글꼴 · 머티리얼)은 씬의 기존 텍스트에서 그대로 가져와**
//   기존 UI 와 같은 모습이 되게 했다(경로 · GUID 하드코딩 없음 — 메모리 「연구 패널 하베스트 패턴」 관례).
//
// 다시 실행해도 된다(멱등): 이미 있는 NetworkLagOverlay 는 지우고 새로 만든다(「통째로 만드는 대상은 기존 제거 후 재생성」 관례).
// 🔴 HUD 설정 버튼 · 설정 창 오브젝트는 **이 스크립트가 고치지 않는다.** 덮개가 떠 있는 동안만 런타임 코드가 정렬 값을
//    잠시 올렸다 되돌린다(NetworkLagOverlayUI 머리말).
// 실행 조건: Play 모드가 아닐 것. Game 씬이 열려 있고 저장 안 된 변경이 있으면 건너뛴다(사용자 변경까지 같이 저장되지 않게).
//
// ⚠️ 1회성: 실행 완료 확인 뒤 WORKFLOW [5-2] · [4] 의 비활성화 · 최종 삭제 절차를 따른다(다른 코드의 직접 의존 없음).
//
// ----------------------------------------------------------------------------
// [2026-11-26 2차 수정 — 사양 ⑤] 두 번째 메뉴 추가: 「알림 띠」(NetworkLagBanner) 배치
//   메뉴: Hexiege/Setup/Network Lag Banner - Place In Game Scene
//   만드는 것:
//     [UI]/SafeAreaContainer (GameHUD 의 부모 — 노치 · 상태 표시줄 아래 안전 영역)
//       └─ NetworkLagBanner   ← 화면 위쪽 가로 띠(높이 96), GameHUD 의 보이는 내용 바로 아래 · 맨 마지막 형제
//            Canvas(Override Sorting = 205, 🔴 GraphicRaycaster 없음) + CanvasGroup(숨김 · 터치 차단 끔)
//            + Image(반투명 어두운 띠, raycastTarget 끔) + NetworkLagBannerUI(참조 3개)
//            └─ MessageText    ← 띠 문구(TextMeshProUGUI, raycastTarget 끔). 글꼴은 덮개와 같은 원본에서 복사.
//   🔴 이 메뉴는 기존 NetworkLagOverlay(덮개)를 **건드리지 않는다** — NetworkLagBanner 라는 이름의 오브젝트만 찾아 지우고 새로 만든다.
//      덮개 메뉴도 마찬가지로 띠를 건드리지 않는다. 두 메뉴 모두 다시 실행해도 된다(멱등).
//   🔴 띠는 조작을 막지 않는다 — 터치가 띠를 통과한다(raycastTarget 끔 · blocksRaycasts 끔 · 레이캐스터 없음).
//   ⚠️ 띠 위치(GameHUD 아래)는 이 메뉴가 실행 시점의 HUD 배치로 계산한다. 못 재면 위에서 220 아래로 둔다 — 실행 뒤 Inspector 에서 옮겨도 된다
//      (런타임 코드는 위치를 읽지 않는다).
// ============================================================================

using System.Text;
using Hexiege.Application;   // GameLog
using Hexiege.Presentation;  // GameHudUI · InGameSettingsUI · GameEndUI · NetworkLagOverlayUI
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// Game.unity 에 인게임 연결 지연 덮개(NetworkLagOverlay)를 만들고 참조를 연결하는 1회성 메뉴 도구.
    /// </summary>
    public static class NetworkLagOverlaySetup
    {
        private const string MenuPath = "Hexiege/Setup/Network Lag Overlay - Place In Game Scene";

        /// <summary>[2026-11-26 2차] 알림 띠 배치 메뉴.</summary>
        private const string BannerMenuPath = "Hexiege/Setup/Network Lag Banner - Place In Game Scene";

        /// <summary>[2026-11-26 2차] 만들 띠 오브젝트 이름(재실행 때 이 이름으로 찾아 지운다).</summary>
        private const string BannerName = "NetworkLagBanner";

        /// <summary>[2026-11-26 2차] 띠 높이(1080×1920 기준 캔버스 단위).</summary>
        private const float BannerHeight = 96f;

        /// <summary>[2026-11-26 2차] 띠 문구 글자 크기.</summary>
        private const float BannerFontSize = 44f;

        /// <summary>[2026-11-26 2차] HUD 와 띠 사이 여백.</summary>
        private const float BannerGapBelowHud = 8f;

        /// <summary>[2026-11-26 2차] HUD 아래 위치를 재지 못했을 때 「안전 영역 위에서부터」 띠를 둘 거리.</summary>
        private const float BannerFallbackTopOffset = 220f;

        /// <summary>[2026-11-26 2차] 띠 배경 색(덮개보다 진하게 — 작은 띠라 글자가 잘 보이게). Inspector 에서 바꿔도 된다.</summary>
        private static readonly Color BannerBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        private const string DialogTitle = "Network Lag Overlay Setup";
        private const string LogSystem = "UI";
        private const string LogClass = nameof(NetworkLagOverlaySetup);

        /// <summary>대상 씬.</summary>
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        /// <summary>만들 오브젝트 이름(재실행 때 이 이름으로 찾아 지운다).</summary>
        private const string OverlayName = "NetworkLagOverlay";
        private const string MessageName = "MessageText";

        /// <summary>
        /// 반투명 배경 색. 공용 막의 실제 색은 Login 씬 · UIManager 쪽에 있어 이 씬에서 읽을 수 없으므로 값을 정했다.
        /// 실행 뒤 Inspector 에서 Image 색을 바꿔도 된다(런타임 코드는 이 색을 읽지 않는다).
        /// </summary>
        private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.6f);

        /// <summary>가운데 문구 글자 크기(1080×1920 기준 캔버스).</summary>
        private const float MessageFontSize = 72f;

        // private 필드 이름(nameof 를 쓸 수 없어 글자로 적는다 — 각 클래스의 SerializeField 이름).
        private const string HudSettingsButtonField = "_settingsButton";
        private const string HudSettingsUIField = "_settingsUI";
        private const string SettingsPanelField = "_panel";
        private const string GameEndCountdownTextField = "_countdownText";

        /// <summary>메뉴: 확인 창 → 실행 → 결과 창 · 로그.</summary>
        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(DialogTitle, "Play 모드에서는 실행할 수 없습니다. Play를 멈춘 뒤 다시 실행하세요.", "확인");
                return;
            }
            if (!EditorUtility.DisplayDialog(DialogTitle,
                    "Game.unity 의 [UI] 아래에 연결 지연 덮개(NetworkLagOverlay)를 만들고 참조를 연결한 뒤 씬을 저장합니다.\n" +
                    "이미 있으면 지우고 새로 만듭니다. HUD 설정 버튼 · 설정 창은 고치지 않습니다.\n\n진행할까요?",
                    "실행", "취소"))
                return;

            var report = new StringBuilder();
            bool ok = Apply(report);
            GameLog.Dev.Info(LogSystem, LogClass, "연결 지연 덮개 1회성 배치 메뉴 실행 완료", $"Ok={ok}");
            EditorUtility.DisplayDialog(DialogTitle,
                report + (ok ? "\n완료. 씬이 저장됐습니다. Console 로그도 확인하세요."
                             : "\n⚠️ 끝나지 않았습니다. Console 경고 로그를 확인하세요."),
                "확인");
        }

        /// <summary>
        /// [2026-11-26 2차] 메뉴: 알림 띠 배치. 확인 창 → 실행 → 결과 창 · 로그. 덮개는 건드리지 않는다.
        /// </summary>
        [MenuItem(BannerMenuPath)]
        public static void RunBanner()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(DialogTitle, "Play 모드에서는 실행할 수 없습니다. Play를 멈춘 뒤 다시 실행하세요.", "확인");
                return;
            }
            if (!EditorUtility.DisplayDialog(DialogTitle,
                    "Game.unity 의 SafeAreaContainer 아래(HUD 바로 아래)에 「상대 연결 지연」 알림 띠(NetworkLagBanner)를 만들고 씬을 저장합니다.\n" +
                    "이미 있으면 지우고 새로 만듭니다. 기존 지연 덮개(NetworkLagOverlay) · HUD · 설정 창은 고치지 않습니다.\n\n진행할까요?",
                    "실행", "취소"))
                return;

            var report = new StringBuilder();
            bool ok = ApplyWith(report, BuildBanner);
            GameLog.Dev.Info(LogSystem, LogClass, "알림 띠 1회성 배치 메뉴 실행 완료", $"Ok={ok}");
            EditorUtility.DisplayDialog(DialogTitle,
                report + (ok ? "\n완료. 씬이 저장됐습니다. Console 로그도 확인하세요."
                             : "\n⚠️ 끝나지 않았습니다. Console 경고 로그를 확인하세요."),
                "확인");
        }

        /// <summary>씬을 열고(필요하면) 덮개를 만든 뒤 저장한다.</summary>
        /// <remarks>[2026-11-26 2차] 씬 열기 · 닫기 절차를 띠 메뉴와 함께 쓰려고 ApplyWith 로 옮겼다. 동작은 1차와 같다.</remarks>
        private static bool Apply(StringBuilder report)
        {
            return ApplyWith(report, BuildOverlay);
        }

        /// <summary>
        /// [2026-11-26 2차] 씬을 열고(필요하면) 주어진 만들기 함수를 실행한 뒤, 이 메뉴가 연 씬이면 닫는다.
        /// 본문은 1차 Apply 의 본문 그대로이며, 마지막 BuildOverlay 호출만 build 인자로 바꿨다.
        /// </summary>
        private static bool ApplyWith(StringBuilder report, System.Func<Scene, StringBuilder, bool> build)
        {
            Scene scene = SceneManager.GetSceneByPath(GameScenePath);
            bool openedHere = false;
            if (scene.IsValid() && scene.isLoaded)
            {
                if (scene.isDirty)
                {
                    Warn("Game 씬에 저장 안 된 변경이 있어 건너뛴다", $"Scene={GameScenePath}");
                    report.AppendLine("· Game 씬에 저장 안 된 변경이 있어 건너뜀 — 씬을 저장(또는 되돌림)한 뒤 메뉴를 다시 실행하세요");
                    return false;
                }
            }
            else
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
                {
                    Warn("Game 씬 파일을 찾지 못했다", $"Scene={GameScenePath}");
                    report.AppendLine("· Game 씬 파일 없음 — 건너뜀");
                    return false;
                }
                scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
                openedHere = true;
            }

            try
            {
                // [2026-11-26 2차] 옛 줄(1차): return BuildOverlay(scene, report);
                return build(scene, report);
            }
            finally
            {
                // 이 메뉴가 연 씬이면 다시 닫아, 사용자가 보던 씬 구성을 그대로 둔다.
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// 필요한 대상을 **모두 찾은 뒤에야** 만들기 시작한다(반쯤 만들어진 씬이 저장되지 않게 — 메모리 관례).
        /// </summary>
        private static bool BuildOverlay(Scene scene, StringBuilder report)
        {
            // ── 1. 찾기 ─────────────────────────────────────────────────────
            GameHudUI hud = FindSingle<GameHudUI>(scene, report);
            GameEndUI gameEnd = FindSingle<GameEndUI>(scene, report);
            if (hud == null || gameEnd == null) return false;

            Canvas hudCanvas = hud.GetComponentInParent<Canvas>(true);
            Canvas root = hudCanvas != null ? hudCanvas.rootCanvas : null;
            if (root == null)
            {
                Warn("GameHudUI 위의 루트 Canvas 를 찾지 못했다", "");
                report.AppendLine("· 루트 Canvas([UI]) 를 찾지 못함 — 중단");
                return false;
            }

            var hudSo = new SerializedObject(hud);
            Button settingsButton = hudSo.FindProperty(HudSettingsButtonField)?.objectReferenceValue as Button;
            InGameSettingsUI settingsUI = hudSo.FindProperty(HudSettingsUIField)?.objectReferenceValue as InGameSettingsUI;
            if (settingsUI == null) settingsUI = FindSingle<InGameSettingsUI>(scene, null);

            Canvas settingsPanelCanvas = null;
            if (settingsUI != null)
            {
                var settingsSo = new SerializedObject(settingsUI);
                var panel = settingsSo.FindProperty(SettingsPanelField)?.objectReferenceValue as Component;
                if (panel != null) settingsPanelCanvas = panel.GetComponent<Canvas>();
            }

            var endSo = new SerializedObject(gameEnd);
            TextMeshProUGUI styleSource = endSo.FindProperty(GameEndCountdownTextField)?.objectReferenceValue as TextMeshProUGUI;

            report.AppendLine($"· 루트 Canvas: {root.name}");
            report.AppendLine($"· 설정 여는 버튼: {(settingsButton != null ? settingsButton.name : "없음(런타임이 이름으로 찾는다)")}");
            report.AppendLine($"· 설정 창: {(settingsUI != null ? settingsUI.name : "없음")}" +
                              $" / 본체 Canvas: {(settingsPanelCanvas != null ? settingsPanelCanvas.name + " (SO " + settingsPanelCanvas.sortingOrder + ")" : "없음")}");
            report.AppendLine($"· 글꼴 원본: {(styleSource != null ? styleSource.name : "없음(TMP 기본 글꼴)")}");

            // ── 2. 기존 것 제거(멱등) ─────────────────────────────────────────
            Transform existing = root.transform.Find(OverlayName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                report.AppendLine("· 기존 NetworkLagOverlay 를 지우고 새로 만든다");
            }

            // ── 3. 만들기 ──────────────────────────────────────────────────
            var go = new GameObject(OverlayName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create NetworkLagOverlay");
            go.layer = root.gameObject.layer;
            go.transform.SetParent(root.transform, false);
            go.transform.SetAsLastSibling();
            StretchFull((RectTransform)go.transform);

            var canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = NetworkLagOverlayUI.OverlaySortingOrder;
            go.AddComponent<GraphicRaycaster>(); // 독립 Canvas 는 자체 레이캐스터가 있어야 터치를 받는다(CanvasSortingOrder 규칙 1)

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;            // 숨긴 상태로 저장(런타임 Awake 도 숨김으로 시작한다)
            group.blocksRaycasts = false;
            group.interactable = false;

            var image = go.AddComponent<Image>();
            image.color = BackgroundColor;
            image.raycastTarget = true;  // 🔴 이 배경이 게임 화면 터치를 받아 먹는다(U-5)

            var msgGo = new GameObject(MessageName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(msgGo, "Create NetworkLagOverlay Message");
            msgGo.layer = go.layer;
            msgGo.transform.SetParent(go.transform, false);
            StretchFull((RectTransform)msgGo.transform);
            var tmp = msgGo.AddComponent<TextMeshProUGUI>();
            if (styleSource != null)
            {
                // ⚠️ font 를 넣으면 머티리얼이 그 글꼴 기본값으로 되돌아가므로 머티리얼은 font 다음에 넣는다(메모리 관례).
                tmp.font = styleSource.font;
                tmp.fontSharedMaterial = styleSource.fontSharedMaterial;
            }
            tmp.text = string.Empty;    // 🔴 문구는 런타임 상수가 단일 소스다(씬에 적지 않는다)
            tmp.fontSize = MessageFontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;  // 글자는 터치를 받지 않는다(배경이 받는다)

            var overlay = go.AddComponent<NetworkLagOverlayUI>();
            var so = new SerializedObject(overlay);
            SetRef(so, "_canvas", canvas);
            SetRef(so, "_group", group);
            SetRef(so, "_messageText", tmp);
            SetRef(so, "_settingsButton", settingsButton);
            SetRef(so, "_settingsUI", settingsUI);
            SetRef(so, "_settingsPanelCanvas", settingsPanelCanvas);
            so.ApplyModifiedPropertiesWithoutUndo();

            // ── 4. 저장 — SetDirty + MarkSceneDirty 둘 다(하나라도 빠지면 저장되지 않는다 — 메모리 관례) ──
            EditorUtility.SetDirty(overlay);
            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Warn("Game 씬 저장 실패", $"Scene={GameScenePath}");
                report.AppendLine("· 씬 저장 실패");
                return false;
            }

            GameLog.Dev.Info(LogSystem, LogClass, "연결 지연 덮개 배치 완료",
                $"Root={root.name}, SortingOrder={NetworkLagOverlayUI.OverlaySortingOrder}, " +
                $"SettingsButton={(settingsButton != null)}, SettingsUI={(settingsUI != null)}, " +
                $"SettingsPanelCanvas={(settingsPanelCanvas != null)}, FontSource={(styleSource != null)}");
            report.AppendLine($"· NetworkLagOverlay 생성(SO {NetworkLagOverlayUI.OverlaySortingOrder}) · 참조 연결 · 씬 저장 완료");
            return true;
        }

        /// <summary>
        /// [2026-11-26 2차] 알림 띠를 만든다. 필요한 대상을 모두 찾은 뒤에야 만들기 시작한다(덮개와 같은 관례).
        /// 🔴 NetworkLagOverlay(덮개)는 찾지도 고치지도 않는다.
        /// </summary>
        private static bool BuildBanner(Scene scene, StringBuilder report)
        {
            // ── 1. 찾기 ─────────────────────────────────────────────────────
            GameHudUI hud = FindSingle<GameHudUI>(scene, report);
            GameEndUI gameEnd = FindSingle<GameEndUI>(scene, report);
            if (hud == null || gameEnd == null) return false;

            // 띠는 GameHUD 와 같은 부모(SafeAreaContainer — GameHudUI 머리말의 씬 구조) 아래에 둔다 — 노치 아래 안전 영역.
            RectTransform parent = hud.transform.parent as RectTransform;
            if (parent == null)
            {
                Warn("GameHUD 의 부모 RectTransform 을 찾지 못했다", "");
                report.AppendLine("· GameHUD 의 부모(SafeAreaContainer)를 찾지 못함 — 중단");
                return false;
            }

            var endSo = new SerializedObject(gameEnd);
            TextMeshProUGUI styleSource = endSo.FindProperty(GameEndCountdownTextField)?.objectReferenceValue as TextMeshProUGUI;

            float topOffset = MeasureHudBottomOffset(hud, parent, out bool measured);
            report.AppendLine($"· 부모: {parent.name}");
            report.AppendLine($"· 띠 위치: 위에서 {topOffset:F0} 아래 ({(measured ? "HUD 배치로 계산" : "계산 실패 — 기본값")})");
            report.AppendLine($"· 글꼴 원본: {(styleSource != null ? styleSource.name : "없음(TMP 기본 글꼴)")}");

            // ── 2. 기존 띠 제거(멱등) — 이름이 NetworkLagBanner 인 것만 ─────────────────
            Transform existing = parent.Find(BannerName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                report.AppendLine("· 기존 NetworkLagBanner 를 지우고 새로 만든다");
            }

            // ── 3. 만들기 ──────────────────────────────────────────────────
            var go = new GameObject(BannerName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create NetworkLagBanner");
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, BannerHeight);
            rt.anchoredPosition = new Vector2(0f, -topOffset);

            var canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = NetworkLagBannerUI.BannerSortingOrder;
            // 🔴 GraphicRaycaster 를 붙이지 않는다 — 이 띠는 터치를 받지 않는다(사양 ⑤ 「입력 통과」).

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;             // 숨긴 상태로 저장(런타임 Awake 도 숨김으로 시작한다)
            group.blocksRaycasts = false; // 🔴 보일 때도 막지 않는다
            group.interactable = false;

            var image = go.AddComponent<Image>();
            image.color = BannerBackgroundColor;
            image.raycastTarget = false;  // 🔴 터치를 받지 않는다

            var msgGo = new GameObject(MessageName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(msgGo, "Create NetworkLagBanner Message");
            msgGo.layer = go.layer;
            msgGo.transform.SetParent(go.transform, false);
            StretchFull((RectTransform)msgGo.transform);
            var tmp = msgGo.AddComponent<TextMeshProUGUI>();
            if (styleSource != null)
            {
                // ⚠️ font 다음에 머티리얼(덮개와 같은 이유 — 메모리 관례).
                tmp.font = styleSource.font;
                tmp.fontSharedMaterial = styleSource.fontSharedMaterial;
            }
            tmp.text = string.Empty;      // 🔴 문구는 런타임 상수가 단일 소스다
            tmp.fontSize = BannerFontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            var banner = go.AddComponent<NetworkLagBannerUI>();
            var so = new SerializedObject(banner);
            SetRef(so, "_canvas", canvas);
            SetRef(so, "_group", group);
            SetRef(so, "_messageText", tmp);
            so.ApplyModifiedPropertiesWithoutUndo();

            // ── 4. 저장 — SetDirty + MarkSceneDirty 둘 다 ─────────────────────
            EditorUtility.SetDirty(banner);
            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Warn("Game 씬 저장 실패", $"Scene={GameScenePath}");
                report.AppendLine("· 씬 저장 실패");
                return false;
            }

            GameLog.Dev.Info(LogSystem, LogClass, "알림 띠 배치 완료",
                $"Parent={parent.name}, SortingOrder={NetworkLagBannerUI.BannerSortingOrder}, TopOffset={topOffset:F0}, " +
                $"Measured={measured}, FontSource={(styleSource != null)}");
            report.AppendLine($"· NetworkLagBanner 생성(SO {NetworkLagBannerUI.BannerSortingOrder}, 터치 통과) · 씬 저장 완료");
            return true;
        }

        /// <summary>
        /// [2026-11-26 2차] 부모(SafeAreaContainer) 위쪽 끝에서 GameHUD 의 「보이는 내용(그래픽)」 맨 아래까지의 거리를 잰다.
        /// HUD 자체가 화면 전체로 늘어나 있어도 실제 글자 · 아이콘 · 버튼의 아래 끝을 쓰므로 띠가 HUD 를 가리지 않는다.
        /// 재지 못하면(그래픽 없음 · 값이 이상함) 기본값을 쓴다.
        /// </summary>
        private static float MeasureHudBottomOffset(GameHudUI hud, RectTransform parent, out bool measured)
        {
            measured = false;
            try
            {
                Canvas.ForceUpdateCanvases();
                float parentTop = parent.rect.yMax;
                float minY = float.PositiveInfinity;
                var corners = new Vector3[4];
                foreach (Graphic g in hud.GetComponentsInChildren<Graphic>(true))
                {
                    g.rectTransform.GetWorldCorners(corners);
                    foreach (Vector3 c in corners)
                    {
                        float y = parent.InverseTransformPoint(c).y;
                        if (y < minY) minY = y;
                    }
                }
                if (float.IsInfinity(minY)) return BannerFallbackTopOffset;
                float offset = parentTop - minY + BannerGapBelowHud;
                // HUD 는 화면 위쪽 고정이다. 계산값이 0 이하이거나 부모 높이의 절반을 넘으면(전체 화면 그래픽 등) 믿지 않는다.
                if (offset <= 0f || offset > parent.rect.height * 0.5f) return BannerFallbackTopOffset;
                measured = true;
                return offset;
            }
            catch (System.Exception e)
            {
                Warn("HUD 아래 위치 계산 실패 — 기본값을 쓴다", $"Exception={e.GetType().Name}");
                return BannerFallbackTopOffset;
            }
        }

        /// <summary>씬에서 T 를 정확히 1개 찾는다. 0개 · 2개 이상이면 null + 경고.</summary>
        private static T FindSingle<T>(Scene scene, StringBuilder report) where T : Component
        {
            T found = null;
            int count = 0;
            foreach (GameObject rootGo in scene.GetRootGameObjects())
            {
                foreach (T c in rootGo.GetComponentsInChildren<T>(true))
                {
                    count++;
                    if (found == null) found = c;
                }
            }
            if (count == 1) return found;
            if (report != null)
            {
                Warn($"{typeof(T).Name} 가 정확히 1개가 아니다", $"Count={count}");
                report.AppendLine($"· {typeof(T).Name} {count}개 발견(1개여야 함) — 중단");
            }
            return null;
        }

        /// <summary>SerializedObject 의 참조 칸 하나를 채운다(칸이 없으면 경고).</summary>
        private static void SetRef(SerializedObject so, string field, Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Warn("NetworkLagOverlayUI 참조 칸을 찾지 못했다", $"Field={field}");
                return;
            }
            p.objectReferenceValue = value;
        }

        /// <summary>RectTransform 을 부모 전체로 늘린다.</summary>
        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>경고 로그(에디터 도구 — 개발 로그).</summary>
        private static void Warn(string message, string data)
        {
            GameLog.Dev.Warn(LogSystem, LogClass, message, data);
        }
    }
}
