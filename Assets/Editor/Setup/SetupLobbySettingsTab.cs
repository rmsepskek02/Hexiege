// ============================================================================
// SetupLobbySettingsTab.cs
// Lobby.unity 씬의 프로필 탭을 설정 탭으로 전환하고,
// ProfilePanel 안에 LobbySettingsView 구조를 구성하는 1회성 에디터 셋업 스크립트.
//
// 실행 방법: Unity 상단 메뉴 → Hexiege/Setup/사운드 - 로비 설정 탭 구성
//
// 동작 개요:
//   1) Lobby.unity 열기
//   2) LobbyRootView에서 _profilePanel(ProfilePanel GO) 탐색
//   3) 프로필 탭 버튼 아이콘/텍스트를 "설정"으로 교체
//   4) ProfilePanel 안의 기존 ProfileView GO 비활성화
//   5) LobbySettingsView GO 구조 생성 + SerializedObject로 필드 연결
//   6) 씬 저장
//
// LobbySettingsView 계층 (생성 결과):
//   LobbySettingsView
//     ├── BackButton       (좌상단 고정, 초기 숨김)
//     ├── MainView         (CanvasGroup, 설정 버튼 목록)
//     │     └── ButtonList (VerticalLayoutGroup, 화면 중앙)
//     │           ├── ProfileButton
//     │           └── SoundButton
//     └── SubViewContainer (CanvasGroup, 초기 숨김)
//           ├── SoundSubView (사운드 설정 화면 — 슬라이더 3종)
//           └── ProfileSubView (프로필 서브 화면 — 빈 GO, 향후 확장)
//
// Editor 전용 — 빌드에 포함되지 않는다 (Assets/Editor 폴더).
// ============================================================================

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Hexiege.Presentation;

namespace HexiegeEditor
{
    /// <summary>
    /// Lobby.unity 프로필 탭을 설정 탭으로 전환하고 LobbySettingsView를 구성하는 에디터 도구.
    /// </summary>
    public static class SetupLobbySettingsTab
    {
        private const string ScenePath = "Assets/_Project/Scenes/Lobby.unity";

        [MenuItem("Hexiege/Setup/사운드 - 로비 설정 탭 구성")]
        public static void Run()
        {
            // ----------------------------------------------------------------
            // 1) Lobby.unity 열기.
            // ----------------------------------------------------------------
            if (!System.IO.File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("씬 없음",
                    $"{ScenePath} 를 찾을 수 없습니다.", "확인");
                return;
            }
            var scene = EditorSceneManager.OpenScene(ScenePath);

            // ----------------------------------------------------------------
            // 2) LobbyRootView → _profilePanel 탐색.
            // ----------------------------------------------------------------
            var lobbyRoot = Object.FindFirstObjectByType<LobbyRootView>();
            if (lobbyRoot == null)
            {
                EditorUtility.DisplayDialog("LobbyRootView 없음",
                    "LobbyRootView 컴포넌트를 Lobby.unity에서 찾을 수 없습니다.", "확인");
                return;
            }
            var lrSo           = new SerializedObject(lobbyRoot);
            var profilePanelGo = lrSo.FindProperty("_profilePanel")?.objectReferenceValue as GameObject;
            if (profilePanelGo == null)
            {
                EditorUtility.DisplayDialog("ProfilePanel 없음",
                    "LobbyRootView._profilePanel 필드가 연결되어 있지 않습니다.", "확인");
                return;
            }

            // ----------------------------------------------------------------
            // 3) 프로필 탭 버튼 아이콘/텍스트 교체.
            // ----------------------------------------------------------------
            var tabBar = Object.FindFirstObjectByType<TabBarView>();
            if (tabBar != null)
            {
                var tabSo      = new SerializedObject(tabBar);
                var profileBtn = tabSo.FindProperty("_profileTabButton")?.objectReferenceValue as Button;
                if (profileBtn != null)
                {
                    // 버튼 Image → ui_icon_settings 스프라이트로 교체.
                    var btnImg = profileBtn.GetComponent<Image>();
                    if (btnImg != null)
                        ApplySprite(btnImg, "ui_icon_settings");

                    // 버튼 자식 TMP 텍스트 → "설정"으로 변경.
                    var labelTmp = profileBtn.GetComponentInChildren<TextMeshProUGUI>();
                    if (labelTmp != null)
                        labelTmp.text = "설정";
                }
            }

            // ----------------------------------------------------------------
            // 4) 기존 ProfileView GO 비활성화.
            // ----------------------------------------------------------------
            var profileView = profilePanelGo.GetComponentInChildren<ProfileView>(true);
            if (profileView != null)
                profileView.gameObject.SetActive(false);

            // ----------------------------------------------------------------
            // 5) LobbySettingsView GO 생성 (ProfilePanel 직속 자식).
            // ----------------------------------------------------------------
            // 이미 있으면 재사용.
            var settingsViewGo = profilePanelGo.transform.Find("LobbySettingsView")?.gameObject;
            if (settingsViewGo == null)
            {
                settingsViewGo = new GameObject("LobbySettingsView", typeof(RectTransform));
                settingsViewGo.transform.SetParent(profilePanelGo.transform, false);
            }
            StretchFull(settingsViewGo.GetComponent<RectTransform>());
            var settingsView = GetOrAdd<LobbySettingsView>(settingsViewGo);

            // ---- BackButton (좌상단 고정, 초기 숨김) ----
            // 로그인 씬 BackButton과 동일한 앵커 비율 사용 (1080×1920 기준 좌측 40px, 상단 40px).
            var backBtnGo = GetOrCreateUIChild("BackButton", settingsViewGo.transform);
            var backBtnRt = backBtnGo.GetComponent<RectTransform>();
            backBtnRt.anchorMin        = new Vector2(0.037f, 0.917f);
            backBtnRt.anchorMax        = new Vector2(0.148f, 0.979f);
            backBtnRt.pivot            = new Vector2(0.5f, 0.5f);
            backBtnRt.anchoredPosition = Vector2.zero;
            backBtnRt.sizeDelta        = Vector2.zero;
            var backBtnImg = GetOrAdd<Image>(backBtnGo);
            ApplySprite(backBtnImg, "ui_icon_back");
            var backBtn = GetOrAdd<Button>(backBtnGo);
            backBtn.targetGraphic = backBtnImg;
            backBtnGo.SetActive(false); // 초기 숨김 — 서브 화면 진입 시에만 표시됨.

            // ---- MainView (설정 버튼 목록, 초기 표시) ----
            var mainViewGo = GetOrCreateUIChild("MainView", settingsViewGo.transform);
            StretchFull(mainViewGo.GetComponent<RectTransform>());
            var mainViewCg = GetOrAdd<CanvasGroup>(mainViewGo);
            mainViewCg.alpha          = 1f;
            mainViewCg.interactable   = true;
            mainViewCg.blocksRaycasts = true;

            // ButtonList — 설정 항목 버튼들을 화면 중앙 세로로 배치.
            var btnListGo = GetOrCreateUIChild("ButtonList", mainViewGo.transform);
            var blRt      = btnListGo.GetComponent<RectTransform>();
            blRt.anchorMin        = new Vector2(0.222f, 0.35f);
            blRt.anchorMax        = new Vector2(0.778f, 0.65f);
            blRt.pivot            = new Vector2(0.5f, 0.5f);
            blRt.anchoredPosition = Vector2.zero;
            blRt.sizeDelta        = Vector2.zero;
            var blVlg = GetOrAdd<VerticalLayoutGroup>(btnListGo);
            blVlg.spacing                = 24f;
            blVlg.childAlignment         = TextAnchor.MiddleCenter;
            blVlg.childControlWidth      = true;
            blVlg.childForceExpandWidth  = true;
            blVlg.childControlHeight     = true;
            blVlg.childForceExpandHeight = false;

            var profileBtn2 = CreateMenuButton(btnListGo.transform, "ProfileButton", "프로필");
            var soundBtn2   = CreateMenuButton(btnListGo.transform, "SoundButton",   "사운드");

            // ---- SubViewContainer (서브 화면 컨테이너, 초기 숨김) ----
            var subContainerGo = GetOrCreateUIChild("SubViewContainer", settingsViewGo.transform);
            StretchFull(subContainerGo.GetComponent<RectTransform>());
            var subContainerCg = GetOrAdd<CanvasGroup>(subContainerGo);
            subContainerCg.alpha          = 0f;
            subContainerCg.interactable   = false;
            subContainerCg.blocksRaycasts = false;

            // SoundSubView — 사운드 설정 서브 화면 (슬라이더 3종).
            var soundSubViewGo = GetOrCreateUIChild("SoundSubView", subContainerGo.transform);
            StretchFull(soundSubViewGo.GetComponent<RectTransform>());
            soundSubViewGo.SetActive(false);

            var sliderContainerGo = GetOrCreateUIChild("SliderContainer", soundSubViewGo.transform);
            var scRt              = sliderContainerGo.GetComponent<RectTransform>();
            scRt.anchorMin        = new Vector2(0f, 0.3f);
            scRt.anchorMax        = new Vector2(1f, 0.85f);
            scRt.pivot            = new Vector2(0.5f, 0.5f);
            scRt.anchoredPosition = Vector2.zero;
            scRt.sizeDelta        = Vector2.zero;
            var scVlg = GetOrAdd<VerticalLayoutGroup>(sliderContainerGo);
            scVlg.padding                = new RectOffset(20, 20, 20, 20);
            scVlg.spacing                = 16f;
            scVlg.childAlignment         = TextAnchor.MiddleCenter;
            scVlg.childControlWidth      = true;
            scVlg.childForceExpandWidth  = true;
            scVlg.childControlHeight     = true;
            scVlg.childForceExpandHeight = false;

            CreateSliderRow(sliderContainerGo.transform, "MasterRow", "마스터",
                out Slider masterSlider, out TMP_Text masterText);
            CreateSliderRow(sliderContainerGo.transform, "BGMRow",    "배경음",
                out Slider bgmSlider,    out TMP_Text bgmText);
            CreateSliderRow(sliderContainerGo.transform, "SFXRow",    "효과음",
                out Slider sfxSlider,    out TMP_Text sfxText);

            // ProfileSubView — 향후 프로필 기능 통합을 위한 빈 GO.
            var profileSubViewGo = GetOrCreateUIChild("ProfileSubView", subContainerGo.transform);
            StretchFull(profileSubViewGo.GetComponent<RectTransform>());
            profileSubViewGo.SetActive(false);

            // ----------------------------------------------------------------
            // 6) LobbySettingsView SerializedObject 필드 연결.
            // ----------------------------------------------------------------
            var svSo = new SerializedObject(settingsView);
            svSo.FindProperty("_backButton").objectReferenceValue       = backBtn;
            svSo.FindProperty("_mainView").objectReferenceValue         = mainViewCg;
            svSo.FindProperty("_subViewContainer").objectReferenceValue = subContainerCg;
            svSo.FindProperty("_profileButton").objectReferenceValue    = profileBtn2;
            svSo.FindProperty("_soundButton").objectReferenceValue      = soundBtn2;
            svSo.FindProperty("_soundSubView").objectReferenceValue     = soundSubViewGo;
            svSo.FindProperty("_profileSubView").objectReferenceValue   = profileSubViewGo;
            svSo.FindProperty("_masterSlider").objectReferenceValue     = masterSlider;
            svSo.FindProperty("_bgmSlider").objectReferenceValue        = bgmSlider;
            svSo.FindProperty("_sfxSlider").objectReferenceValue        = sfxSlider;
            svSo.FindProperty("_masterValueText").objectReferenceValue  = masterText;
            svSo.FindProperty("_bgmValueText").objectReferenceValue     = bgmText;
            svSo.FindProperty("_sfxValueText").objectReferenceValue     = sfxText;
            svSo.ApplyModifiedPropertiesWithoutUndo();

            // ----------------------------------------------------------------
            // 7) 씬 저장.
            // ----------------------------------------------------------------
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SetupLobbySettingsTab] 완료.\n" +
                      "  ProfilePanel → LobbySettingsView 구성 완료.");
            EditorUtility.DisplayDialog("완료", "로비 설정 탭 구성이 완료되었습니다.", "확인");
        }

        // --------------------------------------------------------------------
        // 헬퍼
        // --------------------------------------------------------------------

        /// <summary>설정 메뉴 버튼 한 개를 생성한다 (VLG 자식용).</summary>
        private static Button CreateMenuButton(Transform parent, string name, string label)
        {
            var go = GetOrCreateUIChild(name, parent);
            go.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            var le = GetOrAdd<LayoutElement>(go);
            le.preferredHeight = 120f;
            le.flexibleHeight  = 0f;
            var img = GetOrAdd<Image>(go);
            ApplySprite(img, "ui_btn_lavender");
            var btn = GetOrAdd<Button>(go);
            btn.targetGraphic = img;

            var labelGo  = GetOrCreateUIChild("Label", go.transform);
            StretchFull(labelGo.GetComponent<RectTransform>());
            var labelTmp = GetOrAdd<TextMeshProUGUI>(labelGo);
            labelTmp.text      = label;
            labelTmp.fontSize  = 42f;
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.color     = Color.black;
            return btn;
        }

        /// <summary>슬라이더 행(Label + Slider + ValueText)을 생성한다.</summary>
        private static void CreateSliderRow(Transform parent, string rowName, string labelText,
            out Slider slider, out TMP_Text valueText)
        {
            var rowGo = GetOrCreateUIChild(rowName, parent);
            rowGo.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            var rowLe = GetOrAdd<LayoutElement>(rowGo);
            rowLe.preferredHeight  = 80f;
            rowLe.flexibleHeight   = 0f;
            var hlg = GetOrAdd<HorizontalLayoutGroup>(rowGo);
            hlg.spacing                = 12f;
            hlg.childAlignment         = TextAnchor.MiddleCenter;
            hlg.childControlWidth      = true;
            hlg.childForceExpandWidth  = false;
            hlg.childControlHeight     = true;
            hlg.childForceExpandHeight = true;

            // Label
            var labelGo  = GetOrCreateUIChild("Label", rowGo.transform);
            labelGo.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            var labelLe  = GetOrAdd<LayoutElement>(labelGo);
            labelLe.preferredWidth = 70f;
            labelLe.flexibleWidth  = 0f;
            var labelTmp = GetOrAdd<TextMeshProUGUI>(labelGo);
            labelTmp.text      = labelText;
            labelTmp.fontSize  = 32f;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            labelTmp.color     = Color.black;

            // Slider
            slider = CreateSlider(rowGo.transform, rowName + "Slider");

            // ValueText
            var valueGo  = GetOrCreateUIChild("ValueText", rowGo.transform);
            valueGo.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            var valueLe  = GetOrAdd<LayoutElement>(valueGo);
            valueLe.preferredWidth = 60f;
            valueLe.flexibleWidth  = 0f;
            var valueTmp = GetOrAdd<TextMeshProUGUI>(valueGo);
            valueTmp.text      = "100%";
            valueTmp.fontSize  = 28f;
            valueTmp.alignment = TextAlignmentOptions.MidlineRight;
            valueTmp.color     = Color.black;
            valueText = valueTmp;
        }

        /// <summary>Unity UI 표준 Slider를 생성한다.</summary>
        private static Slider CreateSlider(Transform parent, string name)
        {
            var sliderGo = GetOrCreateUIChild(name, parent);
            sliderGo.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            var sliderLe = GetOrAdd<LayoutElement>(sliderGo);
            sliderLe.flexibleWidth   = 1f;
            sliderLe.preferredHeight = 40f;
            sliderLe.flexibleHeight  = 0f;
            var slider = GetOrAdd<Slider>(sliderGo);
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value    = 1f;

            // Background — 슬라이더 트랙 배경.
            var bgGo = GetOrCreateUIChild("Background", sliderGo.transform);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.25f);
            bgRt.anchorMax = new Vector2(1f, 0.75f);
            bgRt.sizeDelta = Vector2.zero;
            var bgImg = GetOrAdd<Image>(bgGo);
            bgImg.color = new Color(0.5f, 0.5f, 0.5f, 1f);

            // Fill Area — 현재 값을 채운 부분.
            var fillAreaGo = GetOrCreateUIChild("Fill Area", sliderGo.transform);
            var fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRt.offsetMin = new Vector2(5f, 0f);
            fillAreaRt.offsetMax = new Vector2(-15f, 0f);
            var fillGo = GetOrCreateUIChild("Fill", fillAreaGo.transform);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.sizeDelta = new Vector2(10f, 0f);
            var fillImg = GetOrAdd<Image>(fillGo);
            fillImg.color   = new Color(0.6f, 0.4f, 0.9f, 1f);
            slider.fillRect = fillRt;

            // Handle Slide Area — 손잡이 드래그 영역.
            var handleAreaGo = GetOrCreateUIChild("Handle Slide Area", sliderGo.transform);
            var handleAreaRt = handleAreaGo.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = new Vector2(10f, 0f);
            handleAreaRt.offsetMax = new Vector2(-10f, 0f);
            var handleGo = GetOrCreateUIChild("Handle", handleAreaGo.transform);
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0f, 0f);
            handleRt.anchorMax = new Vector2(0f, 1f);
            handleRt.sizeDelta = new Vector2(20f, 0f);
            var handleImg = GetOrAdd<Image>(handleGo);
            handleImg.color      = Color.white;
            slider.handleRect    = handleRt;
            slider.targetGraphic = handleImg;

            return slider;
        }

        /// <summary>Image에 키워드로 스프라이트를 적용한다 (없으면 기본 색상 유지).</summary>
        private static void ApplySprite(Image img, string keyword)
        {
            var guids = AssetDatabase.FindAssets($"{keyword} t:Sprite");
            if (guids.Length == 0) return;
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (sp == null) return;
            img.sprite = sp;
            img.type   = Image.Type.Simple;
            img.color  = Color.white;
        }

        /// <summary>
        /// GetComponent를 시도하고, Unity null이면 AddComponent한다.
        /// C# ?? 연산자는 Unity fake-null을 감지하지 못하므로 이 헬퍼를 사용한다.
        /// </summary>
        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        /// <summary>부모 Transform에서 이름으로 자식을 찾거나 없으면 새로 생성한다.</summary>
        private static GameObject GetOrCreateUIChild(string name, Transform parent)
        {
            var found = parent.Find(name);
            if (found != null) return found.gameObject;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>RectTransform을 부모 전체로 스트레치시킨다.</summary>
        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin        = Vector2.zero;
            rt.anchorMax        = Vector2.one;
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.zero;
        }
    }
}
