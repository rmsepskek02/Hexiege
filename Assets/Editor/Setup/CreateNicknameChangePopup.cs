// ============================================================================
// CreateNicknameChangePopup.cs  (에디터 전용 · 1회성 셋업)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  1) Unity 에디터에서 반드시 "Lobby.unity" 씬을 연다.                        │
// │  2) 상단 메뉴  Hexiege > Setup > Create Nickname Change Popup (Lobby) 실행. │
// │  3) 실행 후 씬을 저장(Ctrl+S)한다.                                          │
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 하는가:
//   - 열린 Lobby 씬에서 ProfileView / ProfilePanel 을 찾는다.
//   - ProfilePanel 과 같은 부모(ContentArea, SafeAreaContainer 하위) 아래에
//     "NicknameChangePopup" 모달을 생성한다:
//       · Root: Canvas(overrideSorting=250) + GraphicRaycaster + NicknameChangePopup
//               → BlockingOverlay(SortingOrder 100) 위에 그려지도록 별도 정렬.
//       · Panel: Image(ui_panel_dark) + AnimatedPanel(PopupFade)
//               ├─ Title / FreeSection(입력) / PaidSection(안내+구매) / Status / ButtonRow
//   - NicknameChangePopup 의 [SerializeField] 슬롯을 SerializedObject 로 연결하고,
//     ProfileView._nicknameChangePopup 슬롯에 이 팝업을 연결한다.
//
// 주의(에디터 전용):
//   Assets/Editor/ 하위이므로 자동 에디터 전용 컴파일 — 빌드에 포함되지 않는다.
// ============================================================================

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Presentation;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// Lobby 씬에 닉네임 변경 모달을 생성하고 슬롯을 연결하는 1회성 에디터 스크립트.
    /// </summary>
    public static class CreateNicknameChangePopup
    {
        private const string LightFontPath = "Assets/_Project/Fonts/Maplestory Light SDF.asset";
        private const string BoldFontPath = "Assets/_Project/Fonts/Maplestory Bold SDF.asset";

        // 스프라이트 경로(확정 결정 5).
        private const string PanelDarkSpritePath = "Assets/_Project/Sprites/UI/Panels/ui_panel_dark.png";
        private const string InputLightSpritePath = "Assets/_Project/Sprites/UI/ui_input_light.png";
        private const string BtnGoldSpritePath = "Assets/_Project/Sprites/UI/Buttons/ui_btn_gold.png";
        private const string BtnCancelSpritePath = "Assets/_Project/Sprites/UI/Buttons/ui_btn_cancel.png";
        private const string BtnSkySpritePath = "Assets/_Project/Sprites/UI/Buttons/ui_btn_sky.png";

        // 모달이 BlockingOverlay(SortingOrder 100) 위에 그려지도록 하는 정렬값(ConfirmPopup 250 관례).
        private const int PopupSortingOrder = 250;

        [MenuItem("Hexiege/Setup/Create Nickname Change Popup (Lobby)")]
        public static void Run()
        {
            // ── 1) ProfileView / 부모 탐색 ───────────────────────────────
            ProfileView profileView =
                Object.FindFirstObjectByType<ProfileView>(FindObjectsInactive.Include);
            if (profileView == null)
            {
                Debug.LogError("[Setup] ProfileView 를 씬에서 찾지 못했습니다. " +
                               "Lobby.unity 씬을 연 상태에서 실행하세요.");
                return;
            }

            // ProfilePanel 과 같은 부모(ContentArea) 아래에 모달을 둔다(Plan 5, SafeAreaContainer 하위).
            Transform popupParent = ResolvePopupParent(profileView);
            if (popupParent == null)
            {
                Debug.LogError("[Setup] 모달을 배치할 부모(ProfilePanel/ContentArea)를 찾지 못했습니다.");
                return;
            }

            // ── 2) 폰트/스프라이트 로드 ──────────────────────────────────
            TMP_FontAsset lightFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LightFontPath);
            TMP_FontAsset boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            if (lightFont == null)
                Debug.LogWarning($"[Setup] 기본 폰트를 찾지 못했습니다: {LightFontPath}");

            Sprite panelSprite = LoadSprite(PanelDarkSpritePath);
            Sprite inputSprite = LoadSprite(InputLightSpritePath);
            Sprite goldSprite = LoadSprite(BtnGoldSpritePath);
            Sprite cancelSprite = LoadSprite(BtnCancelSpritePath);
            Sprite skySprite = LoadSprite(BtnSkySpritePath);

            // ── 3) 모달 Root(전체 스트레치 + Canvas 정렬 오버라이드) ──────
            GameObject root = FindOrCreateChild(popupParent, "NicknameChangePopup");
            SetStretch(root.GetComponent<RectTransform>());

            // BlockingOverlay(SO 100) 위에 그려지도록 자체 Canvas 로 정렬을 올린다.
            if (!root.TryGetComponent(out Canvas rootCanvas))
                rootCanvas = root.AddComponent<Canvas>();
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = PopupSortingOrder;
            if (!root.TryGetComponent(out GraphicRaycaster _))
                root.AddComponent<GraphicRaycaster>();

            if (!root.TryGetComponent(out NicknameChangePopup popup))
                popup = root.AddComponent<NicknameChangePopup>();

            // ── 4) Panel(중앙 정렬 + 배경 + 애니메이션 + 세로 배치) ───────
            GameObject panel = FindOrCreateChild(root.transform, "Panel");
            RectTransform panelRt = panel.GetComponent<RectTransform>();
            SetAnchors(panelRt, new Vector2(0.12f, 0.28f), new Vector2(0.88f, 0.72f));

            if (!panel.TryGetComponent(out Image panelBg))
                panelBg = panel.AddComponent<Image>();
            if (panelSprite != null)
            {
                panelBg.sprite = panelSprite;
                panelBg.type = Image.Type.Sliced;
                panelBg.color = Color.white;
            }
            else
            {
                panelBg.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);
            }
            panelBg.raycastTarget = true;

            // AnimatedPanel(PopupFade 기본) — Show/Hide 애니메이션 담당. CanvasGroup 은 자동 추가된다.
            if (!panel.TryGetComponent(out AnimatedPanel animPanel))
                animPanel = panel.AddComponent<AnimatedPanel>();
            // 에디터에서 초기엔 숨김 상태로 보이도록 CanvasGroup alpha=0(런타임 Awake 에서도 재설정됨).
            if (!panel.TryGetComponent(out CanvasGroup panelCg))
                panelCg = panel.AddComponent<CanvasGroup>();
            panelCg.alpha = 0f;
            panelCg.blocksRaycasts = false;
            panelCg.interactable = false;

            VerticalLayoutGroup vlg = EnsureVLG(panel);
            vlg.spacing = 16f;
            vlg.padding = new RectOffset(24, 24, 24, 24);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Title
            GameObject titleGo = FindOrCreateChild(panel.transform, "Title");
            TextMeshProUGUI titleText =
                EnsureText(titleGo, boldFont, "닉네임 변경", 40, TextAlignmentOptions.Center);
            SetLayoutElement(titleGo, 70f, 0f);

            // FreeSection(입력) — CanvasGroup 으로 무료 모드에서만 표시.
            GameObject freeGo = FindOrCreateChild(panel.transform, "FreeSection");
            SetLayoutElement(freeGo, 100f, 0f);
            if (!freeGo.TryGetComponent(out CanvasGroup freeGroup))
                freeGroup = freeGo.AddComponent<CanvasGroup>();
            EnsureVLG(freeGo).childForceExpandHeight = false;
            GameObject inputGo = FindOrCreateChild(freeGo.transform, "NicknameInput");
            TMP_InputField nicknameInput = EnsureInputField(inputGo, lightFont, "새 닉네임", inputSprite);
            SetLayoutElement(inputGo, 90f, 0f);

            // PaidSection(안내 + 구매) — CanvasGroup 으로 유료 소진 모드에서만 표시.
            GameObject paidGo = FindOrCreateChild(panel.transform, "PaidSection");
            SetLayoutElement(paidGo, 140f, 0f);
            if (!paidGo.TryGetComponent(out CanvasGroup paidGroup))
                paidGroup = paidGo.AddComponent<CanvasGroup>();
            VerticalLayoutGroup paidVlg = EnsureVLG(paidGo);
            paidVlg.spacing = 12f;
            paidVlg.childForceExpandHeight = false;
            GameObject paidNoticeGo = FindOrCreateChild(paidGo.transform, "PaidNoticeText");
            TextMeshProUGUI paidNoticeText = EnsureText(paidNoticeGo, lightFont,
                "무료 변경을 이미 사용했습니다.\n추가 변경은 다이아로 가능합니다.", 26, TextAlignmentOptions.Center);
            SetLayoutElement(paidNoticeGo, 70f, 0f);
            GameObject purchaseGo = FindOrCreateChild(paidGo.transform, "PurchaseButton");
            Button purchaseButton = EnsureButton(purchaseGo, boldFont, "구매하기 (준비 중)",
                new Color(0.30f, 0.60f, 0.80f, 1f), skySprite);
            SetLayoutElement(purchaseGo, 70f, 0f);

            // StatusText(검증/오류/준비 중 안내 공용)
            GameObject statusGo = FindOrCreateChild(panel.transform, "StatusText");
            TextMeshProUGUI statusText =
                EnsureText(statusGo, lightFont, string.Empty, 24, TextAlignmentOptions.Center);
            statusText.color = new Color(1f, 0.6f, 0.6f, 1f);
            SetLayoutElement(statusGo, 40f, 1f);

            // ButtonRow(확인 + 취소)
            GameObject buttonRow = FindOrCreateChild(panel.transform, "ButtonRow");
            EnsureHLG(buttonRow, 16f);
            SetLayoutElement(buttonRow, 90f, 0f);
            GameObject confirmGo = FindOrCreateChild(buttonRow.transform, "ConfirmButton");
            Button confirmButton = EnsureButton(confirmGo, boldFont, "확인",
                new Color(0.20f, 0.45f, 0.85f, 1f), goldSprite);
            GameObject cancelGo = FindOrCreateChild(buttonRow.transform, "CancelButton");
            Button cancelButton = EnsureButton(cancelGo, boldFont, "취소",
                new Color(0.55f, 0.30f, 0.30f, 1f), cancelSprite);

            // ── 5) 슬롯 연결 ─────────────────────────────────────────────
            Connect(popup, "_panel", animPanel);
            Connect(popup, "_titleText", titleText);
            Connect(popup, "_freeSectionGroup", freeGroup);
            Connect(popup, "_nicknameInput", nicknameInput);
            Connect(popup, "_paidSectionGroup", paidGroup);
            Connect(popup, "_paidNoticeText", paidNoticeText);
            Connect(popup, "_purchaseButton", purchaseButton);
            Connect(popup, "_statusText", statusText);
            Connect(popup, "_confirmButton", confirmButton);
            Connect(popup, "_cancelButton", cancelButton);

            // ProfileView 가 이 팝업을 참조하도록 연결.
            Connect(profileView, "_nicknameChangePopup", popup);

            // ── 6) Dirty 처리 + 씬 저장 표시 ─────────────────────────────
            EditorUtility.SetDirty(popup);
            EditorUtility.SetDirty(profileView);
            EditorSceneManager.MarkSceneDirty(root.scene);

            Debug.Log("[Setup] NicknameChangePopup 생성 및 슬롯 연결 완료. 씬을 저장하세요(Ctrl+S).");
            Selection.activeGameObject = root;
        }

        // ====================================================================
        // 부모 탐색
        // ====================================================================

        /// <summary>
        /// 모달을 배치할 부모를 결정한다.
        ///   1순위: ProfilePanel(ProfileView 의 상위 조상) 의 부모(=ContentArea).
        ///   2순위: 씬에서 이름 "ProfilePanel" 을 찾아 그 부모.
        ///   3순위: ProfileView 의 부모.
        /// </summary>
        private static Transform ResolvePopupParent(ProfileView profileView)
        {
            // 1순위 — 조상 중 ProfilePanel 을 찾아 그 부모 사용.
            Transform t = profileView.transform;
            while (t != null)
            {
                if (t.name == "ProfilePanel" && t.parent != null)
                    return t.parent;
                t = t.parent;
            }

            // 2순위 — 씬 전체에서 이름 탐색.
            foreach (Transform tr in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (tr != null && tr.name == "ProfilePanel" &&
                    tr.gameObject.scene.IsValid() && tr.parent != null)
                    return tr.parent;
            }

            // 3순위 — 폴백.
            Debug.LogWarning("[Setup] ProfilePanel 을 찾지 못해 ProfileView 부모에 배치합니다.");
            return profileView.transform.parent != null ? profileView.transform.parent : profileView.transform;
        }

        // ====================================================================
        // UI 생성 헬퍼 (다른 CreateXxx 스크립트와 동일 패턴)
        // ====================================================================

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

        private static void SetLayoutElement(GameObject go, float preferredHeight, float flexibleHeight)
        {
            if (!go.TryGetComponent(out LayoutElement le))
                le = go.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.flexibleHeight = flexibleHeight;
        }

        private static VerticalLayoutGroup EnsureVLG(GameObject go)
        {
            if (!go.TryGetComponent(out VerticalLayoutGroup vlg))
                vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
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
            EnsureText(labelGo, font, label, 30, TextAlignmentOptions.Center);
            return btn;
        }

        /// <summary>기능적으로 동작하는 최소 구성의 TMP_InputField 를 확보한다(ui_input_light 배경).</summary>
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

            GameObject textArea = FindOrCreateChild(go.transform, "Text Area");
            RectTransform taRt = textArea.GetComponent<RectTransform>();
            SetStretch(taRt);
            taRt.offsetMin = new Vector2(12f, 6f);
            taRt.offsetMax = new Vector2(-12f, -6f);
            if (!textArea.TryGetComponent(out RectMask2D _))
                textArea.AddComponent<RectMask2D>();

            GameObject phGo = FindOrCreateChild(textArea.transform, "Placeholder");
            SetStretch(phGo.GetComponent<RectTransform>());
            TextMeshProUGUI placeholder =
                EnsureText(phGo, font, placeholderText, 28, TextAlignmentOptions.Left);
            placeholder.color = new Color(0.4f, 0.4f, 0.4f, 0.75f);
            placeholder.fontStyle = FontStyles.Italic;

            GameObject textGo = FindOrCreateChild(textArea.transform, "Text");
            SetStretch(textGo.GetComponent<RectTransform>());
            TextMeshProUGUI text = EnsureText(textGo, font, string.Empty, 28, TextAlignmentOptions.Left);
            text.color = Color.black;

            input.textViewport = taRt;
            input.textComponent = text;
            input.placeholder = placeholder;
            if (font != null) input.fontAsset = font;
            input.targetGraphic = bg;
            return input;
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null)
                Debug.LogWarning($"[Setup] 스프라이트를 찾지 못해 단색으로 대체합니다: {path}");
            return s;
        }

        /// <summary>private [SerializeField] 슬롯을 SerializedObject 로 연결한다(런타임 private 필드 대응).</summary>
        private static void Connect(Component target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[Setup] {target.GetType().Name} 에 '{fieldName}' 필드가 없습니다.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
