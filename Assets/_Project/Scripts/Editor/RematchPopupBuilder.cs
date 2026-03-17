// ============================================================================
// RematchPopupBuilder.cs
// Game.unity 씬에 RematchRequestPopup UI 계층 구조를 자동 생성하는 1회성 에디터 도구.
//
// 사용법:
//   1. Game.unity 씬을 열기
//   2. Hierarchy에서 GameEndPanel 하위에 배치될 위치 확인
//   3. Unity 메뉴 Hexiege/UI/Build Rematch Popup 실행
//   4. 생성된 RematchRequestPopup 오브젝트를 GameEndPanel 하위로 이동 (필요 시)
//   5. NetworkGameEndController._rematchRequestPopup Inspector 연결
//
// Editor 전용 스크립트.
// ============================================================================

#if UNITY_EDITOR

using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace Hexiege.Editor
{
    public static class RematchPopupBuilder
    {
        // ====================================================================
        // 색상
        // ====================================================================

        private const string ColorOverlay    = "#000000";   // 반투명 오버레이
        private const string ColorPanel      = "#1A1A2E";   // 팝업 배경
        private const string ColorAccept     = "#00B894";   // 수락 (초록)
        private const string ColorDecline    = "#D63031";   // 거절 (빨강)
        private const string ColorConfirm    = "#636E72";   // 확인 (회색)
        private const string ColorText       = "#FFFFFF";   // 일반 텍스트
        private const string ColorSubText    = "#B2BEC3";   // 보조 텍스트

        private static TMP_FontAsset _font;

        // ====================================================================
        // 메뉴 진입점
        // ====================================================================

        [MenuItem("Hexiege/UI/Build Rematch Popup")]
        public static void BuildRematchPopup()
        {
            // 폰트 로드
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/_Project/Fonts/Maplestory Bold SDF.asset");

            if (_font == null)
                Debug.LogWarning("[RematchPopupBuilder] Maplestory Bold SDF 폰트를 찾을 수 없습니다. 기본 폰트를 사용합니다.");

            // 씬에서 GameEndPanel 또는 Canvas 탐색
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[RematchPopupBuilder] 씬에 Canvas가 없습니다. Game.unity 씬을 열고 실행하세요.");
                return;
            }

            // RematchRequestPopup 루트 생성 (Canvas 직속 자식 — GameEndPanel과 동레벨)
            var popupRoot = CreateChild(canvas.gameObject, "RematchRequestPopup");
            SetFullStretch(popupRoot);
            // 루트는 활성 상태 유지 — 자식 패널은 RematchRequestPopup.Awake() → Hide()에서 비활성화됨

            // RematchRequestPopup 컴포넌트 부착
            // ※ 루트는 활성 상태 유지 — FindFirstObjectByType이 비활성 오브젝트를 탐색 못함
            //    자식 패널(_requestPanel, _declinedPanel)은 Awake()의 Hide()에서 자동 비활성화됨
            var popupComp = popupRoot.AddComponent<Hexiege.Presentation.RematchRequestPopup>();

            // ── 반투명 배경 오버레이 ──────────────────────────────
            var overlay = CreateChild(popupRoot, "Overlay");
            SetFullStretch(overlay);
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.6f);

            // ── RequestPanel (수락/거절 팝업) ─────────────────────
            var requestPanel = CreateCenteredPanel(popupRoot, "RequestPanel", 480f, 280f);

            // 제목 텍스트
            var requestTitle = CreateChild(requestPanel, "TitleText");
            var requestTitleRect = GetRect(requestTitle);
            requestTitleRect.anchorMin = new Vector2(0f, 0.65f);
            requestTitleRect.anchorMax = new Vector2(1f, 1f);
            requestTitleRect.offsetMin = new Vector2(20f, 0f);
            requestTitleRect.offsetMax = new Vector2(-20f, 0f);
            var requestTitleTmp = requestTitle.AddComponent<TextMeshProUGUI>();
            requestTitleTmp.text = "재경기 요청";
            requestTitleTmp.fontSize = 32;
            requestTitleTmp.fontStyle = FontStyles.Bold;
            requestTitleTmp.color = HexColor(ColorText);
            requestTitleTmp.alignment = TextAlignmentOptions.Center;
            ApplyFont(requestTitleTmp);

            // 메시지 텍스트
            var requestMsg = CreateChild(requestPanel, "MessageText");
            var requestMsgRect = GetRect(requestMsg);
            requestMsgRect.anchorMin = new Vector2(0f, 0.35f);
            requestMsgRect.anchorMax = new Vector2(1f, 0.65f);
            requestMsgRect.offsetMin = new Vector2(20f, 0f);
            requestMsgRect.offsetMax = new Vector2(-20f, 0f);
            var requestMsgTmp = requestMsg.AddComponent<TextMeshProUGUI>();
            requestMsgTmp.text = "상대방이 재경기를 요청하였습니다.";
            requestMsgTmp.fontSize = 24;
            requestMsgTmp.color = HexColor(ColorSubText);
            requestMsgTmp.alignment = TextAlignmentOptions.Center;
            ApplyFont(requestMsgTmp);

            // 버튼 영역
            var requestBtnArea = CreateChild(requestPanel, "ButtonArea");
            var requestBtnRect = GetRect(requestBtnArea);
            requestBtnRect.anchorMin = new Vector2(0f, 0f);
            requestBtnRect.anchorMax = new Vector2(1f, 0.35f);
            requestBtnRect.offsetMin = new Vector2(20f, 10f);
            requestBtnRect.offsetMax = new Vector2(-20f, 0f);
            var btnHlg = requestBtnArea.AddComponent<HorizontalLayoutGroup>();
            btnHlg.childControlWidth = true;
            btnHlg.childControlHeight = true;
            btnHlg.childForceExpandWidth = true;
            btnHlg.childForceExpandHeight = true;
            btnHlg.spacing = 16f;

            // 수락 버튼
            var acceptBtn = CreatePopupButton(requestBtnArea, "AcceptButton", "수락", ColorAccept);

            // 거절 버튼
            var declineBtn = CreatePopupButton(requestBtnArea, "DeclineButton", "거절", ColorDecline);

            // ── DeclinedPanel (거절 알림 팝업) ────────────────────
            var declinedPanel = CreateCenteredPanel(popupRoot, "DeclinedPanel", 400f, 220f);
            declinedPanel.SetActive(false);

            // 제목 텍스트
            var declinedTitle = CreateChild(declinedPanel, "TitleText");
            var declinedTitleRect = GetRect(declinedTitle);
            declinedTitleRect.anchorMin = new Vector2(0f, 0.6f);
            declinedTitleRect.anchorMax = new Vector2(1f, 1f);
            declinedTitleRect.offsetMin = new Vector2(20f, 0f);
            declinedTitleRect.offsetMax = new Vector2(-20f, 0f);
            var declinedTitleTmp = declinedTitle.AddComponent<TextMeshProUGUI>();
            declinedTitleTmp.text = "재경기 거절";
            declinedTitleTmp.fontSize = 32;
            declinedTitleTmp.fontStyle = FontStyles.Bold;
            declinedTitleTmp.color = HexColor(ColorText);
            declinedTitleTmp.alignment = TextAlignmentOptions.Center;
            ApplyFont(declinedTitleTmp);

            // 메시지 텍스트
            var declinedMsg = CreateChild(declinedPanel, "MessageText");
            var declinedMsgRect = GetRect(declinedMsg);
            declinedMsgRect.anchorMin = new Vector2(0f, 0.3f);
            declinedMsgRect.anchorMax = new Vector2(1f, 0.6f);
            declinedMsgRect.offsetMin = new Vector2(20f, 0f);
            declinedMsgRect.offsetMax = new Vector2(-20f, 0f);
            var declinedMsgTmp = declinedMsg.AddComponent<TextMeshProUGUI>();
            declinedMsgTmp.text = "상대방이 재경기를 거절하였습니다.";
            declinedMsgTmp.fontSize = 24;
            declinedMsgTmp.color = HexColor(ColorSubText);
            declinedMsgTmp.alignment = TextAlignmentOptions.Center;
            ApplyFont(declinedMsgTmp);

            // 확인 버튼 영역
            var declinedBtnArea = CreateChild(declinedPanel, "ButtonArea");
            var declinedBtnRect = GetRect(declinedBtnArea);
            declinedBtnRect.anchorMin = new Vector2(0.2f, 0f);
            declinedBtnRect.anchorMax = new Vector2(0.8f, 0.3f);
            declinedBtnRect.offsetMin = new Vector2(0f, 10f);
            declinedBtnRect.offsetMax = new Vector2(0f, 0f);

            var confirmBtnImg = declinedBtnArea.AddComponent<Image>();
            confirmBtnImg.color = HexColor(ColorConfirm);
            var confirmBtn = declinedBtnArea.AddComponent<Button>();
            confirmBtn.targetGraphic = confirmBtnImg;

            var confirmTextGo = CreateChild(declinedBtnArea, "Text");
            SetFullStretch(confirmTextGo);
            var confirmTmp = confirmTextGo.AddComponent<TextMeshProUGUI>();
            confirmTmp.text = "확인";
            confirmTmp.fontSize = 26;
            confirmTmp.color = Color.white;
            confirmTmp.alignment = TextAlignmentOptions.Center;
            ApplyFont(confirmTmp);

            // ── RematchRequestPopup SerializeField 연결 ───────────
            SetPrivateField(popupComp, "_overlay", overlay);
            SetPrivateField(popupComp, "_requestPanel", requestPanel);
            SetPrivateField(popupComp, "_acceptButton", acceptBtn.GetComponent<Button>());
            SetPrivateField(popupComp, "_declineButton", declineBtn.GetComponent<Button>());
            SetPrivateField(popupComp, "_declinedPanel", declinedPanel);
            SetPrivateField(popupComp, "_declinedConfirmButton", confirmBtn);

            // 씬 더티 마킹
            EditorUtility.SetDirty(popupRoot);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            // Hierarchy에서 선택
            Selection.activeGameObject = popupRoot;

            Debug.Log("[RematchPopupBuilder] RematchRequestPopup 생성 완료. " +
                      "NetworkGameEndController._rematchRequestPopup에 연결하세요.");
        }

        // ====================================================================
        // 헬퍼 메서드
        // ====================================================================

        /// <summary>
        /// 화면 중앙 고정 크기 패널 생성.
        /// </summary>
        private static GameObject CreateCenteredPanel(GameObject parent, string name, float width, float height)
        {
            var panel = CreateChild(parent, name);
            var rect = GetRect(panel);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;

            var img = panel.AddComponent<Image>();
            img.color = HexColor(ColorPanel);

            return panel;
        }

        /// <summary>
        /// LayoutElement 기반 버튼 생성.
        /// </summary>
        private static GameObject CreatePopupButton(GameObject parent, string name, string label, string colorHex)
        {
            var btnGo = CreateChild(parent, name);

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = HexColor(colorHex);

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            var textGo = CreateChild(btnGo, "Text");
            SetFullStretch(textGo);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 26;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            ApplyFont(tmp);

            return btnGo;
        }

        private static GameObject CreateChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static void SetFullStretch(GameObject go)
        {
            var rect = GetRect(go);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RectTransform GetRect(GameObject go) =>
            go.GetComponent<RectTransform>();

        private static void ApplyFont(TextMeshProUGUI tmp)
        {
            if (_font != null) tmp.font = _font;
        }

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }

        private static void SetPrivateField<T>(Component component, string fieldName, T value)
        {
            var type = component.GetType();
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(component, value);
                EditorUtility.SetDirty(component);
            }
            else
            {
                Debug.LogWarning($"[RematchPopupBuilder] {type.Name}.{fieldName} 필드를 찾을 수 없습니다.");
            }
        }
    }
}

#endif
