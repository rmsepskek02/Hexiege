// ============================================================================
// SetupToastUI.cs
// 토스트 알림 시스템을 씬에 자동으로 설정하는 1회성 에디터 스크립트.
//
// 메뉴: Hexiege/Setup/ToastUI 설정
//
// 수행 작업:
//   [1] ToastMessageConfig.asset 생성 (없으면 신규, 있으면 항목만 재등록)
//         - Assets/_Project/Resources/Config/ToastMessageConfig.asset
//         - GoldInsufficient / PopulationFull / ProductionQueueFull 3개 항목 등록
//
//   [2] 씬 루트에 Toast GameObject 계층 생성
//         - Toast  (Canvas + GraphicRaycaster + CanvasGroup + ToastUI)  ← 씬 루트
//           └─ Background  (RectTransform 화면 하단 중앙 고정 + Image, RaycastTarget=ON)
//                └─ Message  (TextMeshProUGUI)
//         - 이미 존재하면 재사용 (중복 생성 방지)
//
// ※ Lobby.unity가 열린 상태에서 실행할 것.
//   Toast는 [UI] Canvas 자식이 아닌 씬 루트 오브젝트로 생성됩니다.
//   DontDestroyOnLoad는 부모가 없는 루트 오브젝트에서만 동작하기 때문입니다.
//   ToastUI는 DontDestroyOnLoad로 모든 씬에서 유지되므로 첫 씬에만 배치하면 됨.
//   GameBootstrapper 연결 불필요 — ToastUI가 Awake()에서 Resources.Load로 자동 초기화.
//
// Editor 전용 — 빌드에 포함되지 않음.
// ============================================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Hexiege.Presentation;
using Hexiege.Infrastructure;

namespace Hexiege.Editor
{
    public static class SetupToastUI
    {
        private const string MenuPath = "Hexiege/Setup/ToastUI 설정";

        // ToastMessageConfig.asset 저장 경로 (Resources 폴더 — Resources.Load 접근용)
        private const string ConfigFolder = "Assets/_Project/Resources/Config";
        private const string ConfigPath   = "Assets/_Project/Resources/Config/ToastMessageConfig.asset";

        // 씬 오브젝트 이름
        private const string CanvasName     = "[UI] Canvas";
        private const string ToastRootName  = "Toast";
        private const string BgChildName    = "Background";
        private const string TextChildName  = "Message";

        // Toast RectTransform 크기/위치 (화면 하단 중앙 기준)
        private const float ToastWidth      = 700f;
        private const float ToastHeight     = 120f;
        private const float ToastBottomPad  = 60f;   // 화면 하단에서 띄울 여백(px)

        // ====================================================================
        // 진입점
        // ====================================================================

        [MenuItem(MenuPath)]
        public static void Run()
        {
            // [1] ToastMessageConfig.asset 생성 또는 갱신
            ToastMessageConfig config = CreateOrUpdateConfig();
            if (config == null)
            {
                Debug.LogError("[SetupToastUI] ToastMessageConfig 생성 실패. 작업을 중단합니다.");
                return;
            }

            // [2] 씬에 Toast GameObject 계층 생성 (또는 기존 재사용)
            ToastUI toastUI = GetOrCreateToastUI();
            if (toastUI == null)
            {
                Debug.LogError("[SetupToastUI] ToastUI 오브젝트 생성 실패. 작업을 중단합니다.");
                return;
            }

            // 씬 변경 표시 — 저장하지 않으면 다음 실행 시 다시 해야 함
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("[SetupToastUI] 완료. Lobby.unity를 저장하세요 (Ctrl+S). Toast는 DontDestroyOnLoad로 모든 씬에서 동작합니다.");
        }

        // ====================================================================
        // [1] ToastMessageConfig.asset 생성 또는 갱신
        // ====================================================================

        /// <summary>
        /// ToastMessageConfig ScriptableObject를 생성하고 초기 3개 항목을 등록합니다.
        /// 이미 에셋이 존재하면 기존 에셋을 로드하여 항목을 덮어씁니다.
        /// </summary>
        private static ToastMessageConfig CreateOrUpdateConfig()
        {
            // Resources/Config 폴더가 없으면 단계적으로 생성
            EnsureFolder("Assets/_Project/Resources", "Config");

            // 기존 에셋이 있으면 로드, 없으면 신규 생성
            ToastMessageConfig config = AssetDatabase.LoadAssetAtPath<ToastMessageConfig>(ConfigPath);
            bool isNew = (config == null);
            if (isNew)
            {
                config = ScriptableObject.CreateInstance<ToastMessageConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                Debug.Log($"[SetupToastUI] ToastMessageConfig 신규 생성: {ConfigPath}");
            }
            else
            {
                Debug.Log($"[SetupToastUI] 기존 ToastMessageConfig 재사용: {ConfigPath}");
            }

            // SerializedObject를 통해 _entries 리스트를 덮어씌워 초기 항목 등록
            // (기존 항목이 있어도 3개로 재설정 — 기획서 기준값 보장)
            var so = new SerializedObject(config);
            SerializedProperty entriesProp = so.FindProperty("_entries");
            if (entriesProp == null)
            {
                Debug.LogError("[SetupToastUI] ToastMessageConfig에 '_entries' 프로퍼티를 찾을 수 없습니다.");
                return null;
            }

            // 기존 내용을 비우고 3개 항목으로 재등록
            // ToastKey enum 값은 int로 설정 (GoldInsufficient=0, PopulationFull=1, ProductionQueueFull=2)
            entriesProp.ClearArray();

            AddToastEntry(entriesProp, 0, (int)ToastKey.GoldInsufficient,    "골드가 부족합니다",        2.0f);
            AddToastEntry(entriesProp, 1, (int)ToastKey.PopulationFull,      "인구수가 가득 찼습니다",   2.0f);
            AddToastEntry(entriesProp, 2, (int)ToastKey.ProductionQueueFull, "생산 대기열이 가득 찼습니다", 1.5f);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log("[SetupToastUI] ToastMessageConfig 항목 등록 완료 (GoldInsufficient / PopulationFull / ProductionQueueFull).");
            return config;
        }

        /// <summary>
        /// SerializedProperty 배열에 ToastEntry 항목 하나를 추가합니다.
        /// </summary>
        private static void AddToastEntry(SerializedProperty arrayProp, int index, int keyValue, string message, float duration)
        {
            arrayProp.InsertArrayElementAtIndex(index);
            SerializedProperty element = arrayProp.GetArrayElementAtIndex(index);

            // key(enum), message(string), duration(float) 필드 이름은 ToastMessageConfig.ToastEntry 구조체와 일치해야 함
            element.FindPropertyRelative("key").enumValueIndex     = keyValue;
            element.FindPropertyRelative("message").stringValue    = message;
            element.FindPropertyRelative("duration").floatValue    = duration;
        }

        // ====================================================================
        // [2] 씬에 Toast GameObject 계층 생성
        // ====================================================================

        /// <summary>
        /// 씬 루트에 Toast 오브젝트 계층을 생성하고 ToastUI 컴포넌트를 구성합니다.
        ///
        /// ※ DontDestroyOnLoad는 씬 루트 오브젝트(부모 없음)에서만 동작하므로,
        ///   Toast는 [UI] Canvas의 자식이 아닌 독립 루트 오브젝트로 생성합니다.
        ///   클릭(터치) 수신을 위해 Canvas + GraphicRaycaster를 Toast 자체에 붙입니다.
        ///
        /// 생성 구조:
        ///   Toast  (Canvas + GraphicRaycaster + CanvasGroup + ToastUI)  ← 씬 루트
        ///     └─ Background  (RectTransform 하단 중앙 고정 + Image, RaycastTarget=true)
        ///          └─ Message  (TextMeshProUGUI)
        ///
        /// 이미 씬에 ToastUI 컴포넌트가 있으면 해당 인스턴스를 재사용합니다.
        /// </summary>
        private static ToastUI GetOrCreateToastUI()
        {
            // 이미 씬에 ToastUI가 있으면 재사용 (중복 생성 방지)
            var existing = Object.FindAnyObjectByType<ToastUI>();
            if (existing != null)
            {
                Debug.Log($"[SetupToastUI] 기존 ToastUI 재사용: {existing.gameObject.name}");
                return existing;
            }

            // ── Toast 루트 오브젝트 (씬 루트 — 부모 없음) ──────────────────────
            // DontDestroyOnLoad는 부모가 없는 루트 오브젝트에서만 작동한다.
            // Toast를 [UI] Canvas 하위에 두면 씬 전환 시 Canvas와 함께 파괴되어
            // 다음 씬에서 토스트가 표시되지 않는다.
            var toastGO = new GameObject(ToastRootName);
            // 부모를 지정하지 않아 씬 루트에 생성됨
            Undo.RegisterCreatedObjectUndo(toastGO, "Create Toast Root");

            // Canvas — 독립 ScreenSpaceOverlay.
            // sortingOrder=100으로 게임 UI(보통 0~10)보다 항상 위에 표시.
            var canvas = toastGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            // GraphicRaycaster — 이 Canvas 위의 UI 터치(클릭) 이벤트를 수신하는 데 필수.
            // GraphicRaycaster가 없으면 IPointerClickHandler(터치로 토스트 제거)가 동작하지 않음.
            toastGO.AddComponent<GraphicRaycaster>();

            // CanvasGroup — DOTween 페이드아웃과 초기 숨김(alpha=0)에 사용.
            var cg = toastGO.AddComponent<CanvasGroup>();
            cg.alpha          = 0f;
            cg.interactable   = true;
            cg.blocksRaycasts = true;

            // ToastUI 컴포넌트 — 실제 토스트 표시 로직
            var toastUI = toastGO.AddComponent<ToastUI>();

            // ── Background 자식 (실제 토스트 패널 — 화면 하단 중앙 고정) ─────────
            var bgGO = new GameObject(BgChildName);
            bgGO.transform.SetParent(toastGO.transform, false);
            Undo.RegisterCreatedObjectUndo(bgGO, "Create Toast Background");

            // Background RectTransform — 화면 하단 중앙에 고정.
            // Canvas가 전체 화면이므로 anchorMin/Max=(0.5,0)으로 하단 중앙 기준점 설정.
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin        = new Vector2(0.5f, 0f);
            bgRT.anchorMax        = new Vector2(0.5f, 0f);
            bgRT.pivot            = new Vector2(0.5f, 0f);
            bgRT.sizeDelta        = new Vector2(ToastWidth, ToastHeight);
            bgRT.anchoredPosition = new Vector2(0f, ToastBottomPad); // 하단에서 ToastBottomPad px 위

            var bgImage = bgGO.AddComponent<Image>();
            bgImage.color         = new Color(0f, 0f, 0f, 0.7f); // 반투명 검정 배경
            bgImage.raycastTarget = true;                          // 터치 감지를 위해 ON 필수

            // ── Message 손자 오브젝트 ────────────────────────────────────────
            var msgGO = new GameObject(TextChildName);
            msgGO.transform.SetParent(bgGO.transform, false);
            Undo.RegisterCreatedObjectUndo(msgGO, "Create Toast Message Text");

            // Message: Background 내부를 채우되 사방 12px 패딩
            var msgRT = msgGO.AddComponent<RectTransform>();
            msgRT.anchorMin = Vector2.zero;
            msgRT.anchorMax = Vector2.one;
            msgRT.offsetMin = new Vector2(12f,  12f);  // 좌·하 패딩
            msgRT.offsetMax = new Vector2(-12f, -12f); // 우·상 패딩 (음수로 지정)

            var tmp = msgGO.AddComponent<TextMeshProUGUI>();
            tmp.fontSize      = 36f;
            tmp.color         = Color.white;
            tmp.alignment     = TextAlignmentOptions.Center;
            tmp.raycastTarget = false; // 텍스트는 클릭 불필요 — Background가 담당
            tmp.text          = "";    // 런타임에 ToastUI가 채움

            // ── ToastUI SerializedField 연결 (_canvasGroup, _messageText) ────
            var toastSO = new SerializedObject(toastUI);
            toastSO.FindProperty("_canvasGroup").objectReferenceValue = cg;
            toastSO.FindProperty("_messageText").objectReferenceValue = tmp;
            toastSO.ApplyModifiedPropertiesWithoutUndo();

            // ※ SetActive(false) 사용 금지 —
            //   비활성 상태에서는 Awake()가 호출되지 않아 DontDestroyOnLoad 등록이 안 됨.
            //   시각적 숨김은 위에서 설정한 CanvasGroup.alpha=0으로 처리.
            //   런타임 시 Awake() → ClearAll()이 alpha=0을 재확인하므로 시작 시 보이지 않음.

            Debug.Log("[SetupToastUI] Toast 루트 오브젝트 생성 완료 (씬 루트 — DontDestroyOnLoad 지원).");
            return toastUI;
        }

        // ====================================================================
        // 유틸리티
        // ====================================================================

        /// <summary>
        /// 지정한 경로의 폴더를 단계적으로 생성합니다.
        /// 예) EnsureFolder("Assets/_Project/Resources", "Config")
        ///     → "Assets/_Project/Resources/Config" 폴더가 없으면 생성.
        /// </summary>
        private static void EnsureFolder(string parent, string child)
        {
            string fullPath = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, child);
                Debug.Log($"[SetupToastUI] 폴더 생성: {fullPath}");
            }
        }
    }
}
#endif
