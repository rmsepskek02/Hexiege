// ============================================================================
// CreateNicknameSetupPanel.cs  (에디터 전용 · 1회성 셋업)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  1) Unity 에디터에서 반드시 "Login.unity" 씬을 연다.                        │
// │  2) 상단 메뉴  Hexiege > Setup > Create Nickname Setup Panel (Login)  실행. │
// │  3) 실행 후 씬을 저장(Ctrl+S)한다.                                          │
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 하는가:
//   - 열린 Login 씬에서 LoginRootView / LoginBootstrapper 를 찾는다.
//   - 기존 회원가입 패널(_signUpPanel)과 같은 부모 아래에 "NicknameSetupPanel"
//     GameObject 를 만들고(전체 스트레치 + CanvasGroup + NicknameSetupView 부착),
//     제목/닉네임 입력 필드/확인·스킵 버튼/상태 텍스트 자식을 생성한다.
//   - NicknameSetupView / LoginRootView / LoginBootstrapper 의 [SerializeField]
//     슬롯을 SerializedObject 로 연결한다(런타임 private 필드이므로 리플렉션 대입 금지).
//
// 주의(에디터 전용):
//   이 파일은 Assets/Editor/ 하위에 있으므로 자동으로 에디터 전용 컴파일된다.
//   빌드에는 포함되지 않는다.
// ============================================================================

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Bootstrap;
using Hexiege.Presentation;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// Login 씬에 닉네임 설정 패널을 생성하고 슬롯을 연결하는 1회성 에디터 스크립트.
    /// </summary>
    public static class CreateNicknameSetupPanel
    {
        // 폰트 경로(공통 UI 규칙 6). Light = 기본 본문, Bold = 헤더/버튼.
        private const string LightFontPath = "Assets/_Project/Fonts/Maplestory Light SDF.asset";
        private const string BoldFontPath = "Assets/_Project/Fonts/Maplestory Bold SDF.asset";

        // 스프라이트 경로(확정 결정 5 — 스프라이트 적용). 미세 조정은 사용자가 실기에서 진행.
        private const string PanelDarkSpritePath = "Assets/_Project/Sprites/UI/Panels/ui_panel_dark.png";
        private const string InputLightSpritePath = "Assets/_Project/Sprites/UI/ui_input_light.png";
        private const string BtnGoldSpritePath = "Assets/_Project/Sprites/UI/Buttons/ui_btn_gold.png";
        private const string BtnSilverSpritePath = "Assets/_Project/Sprites/UI/Buttons/ui_btn_silver.png";

        [MenuItem("Hexiege/Setup/Create Nickname Setup Panel (Login)")]
        public static void Run()
        {
            // ── 1) 대상 컴포넌트 탐색 ─────────────────────────────────────
            // 비활성 오브젝트도 포함해서 찾는다(패널이 CanvasGroup 로만 숨겨져 있을 수 있음).
            LoginRootView rootView =
                Object.FindFirstObjectByType<LoginRootView>(FindObjectsInactive.Include);
            LoginBootstrapper bootstrapper =
                Object.FindFirstObjectByType<LoginBootstrapper>(FindObjectsInactive.Include);

            if (rootView == null)
            {
                Debug.LogError("[Setup] LoginRootView 를 씬에서 찾지 못했습니다. " +
                               "Login.unity 씬을 연 상태에서 실행하세요.");
                return;
            }
            if (bootstrapper == null)
            {
                Debug.LogError("[Setup] LoginBootstrapper 를 씬에서 찾지 못했습니다. " +
                               "Login.unity 씬을 연 상태에서 실행하세요.");
                return;
            }

            // ── 2) 폰트 로드 ──────────────────────────────────────────────
            TMP_FontAsset lightFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LightFontPath);
            TMP_FontAsset boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            if (lightFont == null)
                Debug.LogWarning($"[Setup] 기본 폰트를 찾지 못했습니다: {LightFontPath}");
            if (boldFont == null)
                Debug.LogWarning($"[Setup] Bold 폰트를 찾지 못했습니다: {BoldFontPath}");

            // 스프라이트 로드(없으면 경고만 하고 단색 폴백으로 진행).
            Sprite panelSprite = LoadSprite(PanelDarkSpritePath);
            Sprite inputSprite = LoadSprite(InputLightSpritePath);
            Sprite goldSprite = LoadSprite(BtnGoldSpritePath);
            Sprite silverSprite = LoadSprite(BtnSilverSpritePath);

            // ── 3) 새 패널의 부모 결정 ────────────────────────────────────
            // 기존 회원가입 패널(_signUpPanel)의 부모와 같은 곳에 형제로 만든다.
            Transform panelParent = rootView.transform;
            var rootSO = new SerializedObject(rootView);
            SerializedProperty signUpProp = rootSO.FindProperty("_signUpPanel");
            if (signUpProp != null && signUpProp.objectReferenceValue is CanvasGroup signUpCg &&
                signUpCg != null && signUpCg.transform.parent != null)
            {
                panelParent = signUpCg.transform.parent;
            }
            else
            {
                Debug.LogWarning("[Setup] _signUpPanel 참조로 부모를 찾지 못해 LoginRootView 하위에 생성합니다.");
            }

            // ── 4) NicknameSetupPanel 생성(멱등) ─────────────────────────
            GameObject panel = FindOrCreateChild(panelParent, "NicknameSetupPanel");
            SetStretch(panel.GetComponent<RectTransform>());

            if (!panel.TryGetComponent(out CanvasGroup panelGroup))
                panelGroup = panel.AddComponent<CanvasGroup>();

            // 배경: ui_panel_dark 스프라이트(Sliced) 적용. 스프라이트가 없으면 단색 폴백.
            if (!panel.TryGetComponent(out Image panelBg))
                panelBg = panel.AddComponent<Image>();
            if (panelSprite != null)
            {
                panelBg.sprite = panelSprite;
                panelBg.type = Image.Type.Sliced;
                panelBg.color = new Color(1f, 1f, 1f, 0f);
            }
            else
            {
                panelBg.color = new Color(0f, 0f, 0f, 0f);
            }
            panelBg.raycastTarget = true;

            if (!panel.TryGetComponent(out NicknameSetupView view))
                view = panel.AddComponent<NicknameSetupView>();

            // ── 5) 중앙 콘텐츠 박스(비율 앵커) + 세로 배치 ─────────────────
            GameObject content = FindOrCreateChild(panel.transform, "Content");
            RectTransform contentRt = content.GetComponent<RectTransform>();
            // 화면의 가로 15~85%, 세로 20~80% 영역을 차지(고정 픽셀 대신 비율 앵커, UI 규칙 2).
            SetAnchors(contentRt, new Vector2(0.04f, 0.34f), new Vector2(0.96f, 0.73f));

            VerticalLayoutGroup vlg = EnsureVLG(content);
            vlg.spacing = 24f;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            // childForceExpandHeight=false 로 두고, 자식별 LayoutElement 비율/선호높이로 높이를 배분한다.
            //   (규칙 2 + 메모리 교훈: ChildForceExpandHeight 만으론 형제 높이 불균등을 못 잡는다.)
            vlg.childForceExpandHeight = false;

            // 제목(선호높이 80)
            GameObject titleGo = FindOrCreateChild(content.transform, "Title");
            EnsureText(titleGo, boldFont, "닉네임 설정", 40, TextAlignmentOptions.Center);
            SetLayoutElement(titleGo, 78f, 0f);

            // 닉네임 입력 필드(선호높이 90, ui_input_light)
            GameObject inputGo = FindOrCreateChild(content.transform, "NicknameInput");
            TMP_InputField nicknameInput = EnsureInputField(inputGo, lightFont, "닉네임을 입력하세요", inputSprite);
            SetLayoutElement(inputGo, 94f, 0f);

            // 확인 버튼(선호높이 90, ui_btn_gold)
            GameObject confirmGo = FindOrCreateChild(content.transform, "ConfirmButton");
            Button confirmButton = EnsureButton(confirmGo, boldFont, "확인",
                new Color(0.20f, 0.45f, 0.85f, 1f), goldSprite);
            SetLayoutElement(confirmGo, 96f, 0f);

            // 스킵 버튼(선호높이 70, ui_btn_silver)
            GameObject skipGo = FindOrCreateChild(content.transform, "SkipButton");
            Button skipButton = EnsureButton(skipGo, boldFont, "건너뛰기",
                new Color(0.35f, 0.35f, 0.40f, 1f), silverSprite);
            SetLayoutElement(skipGo, 90f, 0f);

            // 상태 텍스트(남는 공간 흡수 — flexibleHeight=1)
            GameObject statusGo = FindOrCreateChild(content.transform, "StatusText");
            TextMeshProUGUI statusText =
                EnsureText(statusGo, lightFont, string.Empty, 28, TextAlignmentOptions.Center);
            statusText.color = new Color(1f, 0.55f, 0.55f, 1f);
            SetLayoutElement(statusGo, 0f, 1f);

            // ── 6) 슬롯 연결 ─────────────────────────────────────────────
            // NicknameSetupView
            Connect(view, "_nicknameInput", nicknameInput);
            Connect(view, "_confirmButton", confirmButton);
            Connect(view, "_skipButton", skipButton);
            Connect(view, "_statusText", statusText);

            // LoginRootView
            Connect(rootView, "_nicknameSetupPanel", panelGroup);
            Connect(rootView, "_nicknameSetupView", view);

            // LoginBootstrapper
            Connect(bootstrapper, "_nicknameSetupView", view);

            // ── 7) Dirty 처리 + 씬 저장 표시 ─────────────────────────────
            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(rootView);
            EditorUtility.SetDirty(bootstrapper);
            EditorSceneManager.MarkSceneDirty(panel.scene);

            Debug.Log("[Setup] NicknameSetupPanel 생성 및 슬롯 연결 완료. 씬을 저장하세요(Ctrl+S).");
            Selection.activeGameObject = panel;
        }

        // ====================================================================
        // UI 생성 헬퍼
        // ====================================================================

        /// <summary>지정 이름의 자식을 찾고 없으면 RectTransform 을 가진 새 자식을 만든다(멱등).</summary>
        private static GameObject FindOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>RectTransform 을 부모 전체로 스트레치.</summary>
        private static void SetStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>비율 앵커 지정(offset 0). 고정 픽셀 대신 비율 배치(UI 규칙 2).</summary>
        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>VerticalLayoutGroup 을 확보(없으면 추가).</summary>
        private static VerticalLayoutGroup EnsureVLG(GameObject go)
        {
            if (!go.TryGetComponent(out VerticalLayoutGroup vlg))
                vlg = go.AddComponent<VerticalLayoutGroup>();
            return vlg;
        }

        /// <summary>
        /// LayoutGroup 자식으로서의 선호높이/유연높이를 지정한다(고정 픽셀 대신 비율 가중치, UI 규칙 2).
        /// preferredHeight 는 기본 크기, flexibleHeight 는 남는 공간 배분 비율이다.
        /// </summary>
        private static void SetLayoutElement(GameObject go, float preferredHeight, float flexibleHeight)
        {
            if (!go.TryGetComponent(out LayoutElement le))
                le = go.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.flexibleHeight = flexibleHeight;
        }

        /// <summary>스프라이트를 로드한다. 없으면 경고 후 null(단색 폴백).</summary>
        private static Sprite LoadSprite(string path)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null)
                Debug.LogWarning($"[Setup] 스프라이트를 찾지 못해 단색으로 대체합니다: {path}");
            return s;
        }

        /// <summary>TextMeshProUGUI 를 확보하고 폰트/문구/정렬을 설정한다.</summary>
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
            t.raycastTarget = false; // 라벨은 터치를 가로채지 않도록.
            return t;
        }

        /// <summary>Image + Button + 자식 라벨을 갖춘 버튼을 확보한다. sprite 가 있으면 Sliced 로 적용.</summary>
        private static Button EnsureButton(GameObject go, TMP_FontAsset font, string label, Color bg, Sprite sprite)
        {
            if (!go.TryGetComponent(out Image img))
                img = go.AddComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = bg;
            }

            if (!go.TryGetComponent(out Button btn))
                btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            GameObject labelGo = FindOrCreateChild(go.transform, "Label");
            SetStretch(labelGo.GetComponent<RectTransform>());
            EnsureText(labelGo, font, label, 34, TextAlignmentOptions.Center);
            return btn;
        }

        /// <summary>
        /// 기능적으로 동작하는 최소 구성의 TMP_InputField 를 확보한다.
        /// 계층: InputField(Image) → Text Area(RectMask2D) → Placeholder / Text.
        /// sprite 가 있으면 배경에 Sliced 로 적용한다.
        /// </summary>
        private static TMP_InputField EnsureInputField(
            GameObject go, TMP_FontAsset font, string placeholderText, Sprite sprite)
        {
            if (!go.TryGetComponent(out Image bg))
                bg = go.AddComponent<Image>();
            if (sprite != null)
            {
                bg.sprite = sprite;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            else
            {
                bg.color = Color.white;
            }

            if (!go.TryGetComponent(out TMP_InputField input))
                input = go.AddComponent<TMP_InputField>();

            // Text Area(클리핑 영역)
            GameObject textArea = FindOrCreateChild(go.transform, "Text Area");
            RectTransform taRt = textArea.GetComponent<RectTransform>();
            SetStretch(taRt);
            taRt.offsetMin = new Vector2(12f, 6f);
            taRt.offsetMax = new Vector2(-12f, -6f);
            if (!textArea.TryGetComponent(out RectMask2D _))
                textArea.AddComponent<RectMask2D>();

            // Placeholder
            GameObject phGo = FindOrCreateChild(textArea.transform, "Placeholder");
            SetStretch(phGo.GetComponent<RectTransform>());
            TextMeshProUGUI placeholder =
                EnsureText(phGo, font, placeholderText, 30, TextAlignmentOptions.Left);
            placeholder.color = new Color(0.4f, 0.4f, 0.4f, 0.75f);
            placeholder.fontStyle = FontStyles.Italic;

            // Text(실제 입력 텍스트)
            GameObject textGo = FindOrCreateChild(textArea.transform, "Text");
            SetStretch(textGo.GetComponent<RectTransform>());
            TextMeshProUGUI text =
                EnsureText(textGo, font, string.Empty, 30, TextAlignmentOptions.Left);
            text.color = Color.black;

            // 배선
            input.textViewport = taRt;
            input.textComponent = text;
            input.placeholder = placeholder;
            if (font != null) input.fontAsset = font;
            input.targetGraphic = bg;
            return input;
        }

        // ====================================================================
        // 참조 연결 헬퍼
        // ====================================================================

        /// <summary>
        /// private [SerializeField] 슬롯을 SerializedObject 로 연결한다.
        /// 런타임 필드가 private 이므로 반드시 이 경로로 대입해야 씬에 직렬화된다.
        /// </summary>
        private static void Connect(Component target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[Setup] {target.GetType().Name} 에 '{fieldName}' 필드가 없습니다. " +
                               "필드명이 변경되었는지 확인하세요.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
