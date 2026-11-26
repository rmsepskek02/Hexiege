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

        /// <summary>씬을 열고(필요하면) 덮개를 만든 뒤 저장한다.</summary>
        private static bool Apply(StringBuilder report)
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
                return BuildOverlay(scene, report);
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
