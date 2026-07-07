// ============================================================================
// SetupInGameVolumePanel.cs
// Game.unity 씬의 InGameSettingsUI 팝업 안에 볼륨 조절 패널을 구성하는
// 1회성 에디터 셋업 스크립트.
//
// 실행 방법: Unity 상단 메뉴 → Hexiege/Setup/사운드 - 인게임 볼륨 패널 생성
//
// 동작 개요:
//   1) Game.unity 열기
//   2) InGameSettingsUI 컴포넌트 탐색
//   3) 기존 _soundButton / _forfeitButton을 MainButtonContainer(CanvasGroup)로 묶기
//   4) VolumePanel(CanvasGroup) 생성 — 슬라이더 3종 + 뒤로가기 버튼
//   5) InGameSettingsUI SerializedObject로 필드 연결
//   6) 씬 저장
//
// 결과 계층:
//   (팝업 박스)
//     ├── MainButtonContainer (CanvasGroup)  ← 사운드/포기 버튼 그룹
//     │     ├── [기존 SoundButton]
//     │     └── [기존 ForfeitButton]
//     └── VolumePanel (CanvasGroup, 초기 숨김)
//           ├── SliderContainer (VerticalLayoutGroup)
//           │     ├── MasterRow (Label + Slider + ValueText)
//           │     ├── BGMRow    (Label + Slider + ValueText)
//           │     └── SFXRow    (Label + Slider + ValueText)
//           └── BackButton
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
    /// Game.unity의 InGameSettingsUI 팝업에 볼륨 패널 UI를 자동 구성하는 에디터 도구.
    /// </summary>
    public static class SetupInGameVolumePanel
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        // 규칙 6 — 모든 TextMeshProUGUI 폰트는 반드시 Maplestory Light SDF를 사용한다.
        private const string FontPath = "Assets/_Project/Fonts/Maplestory Light SDF.asset";

        /// <summary>Maplestory Light SDF 폰트 에셋을 로드한다. 없으면 null(기본 폰트 유지).</summary>
        private static TMP_FontAsset LoadProjectFont()
        {
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        }

        [MenuItem("Hexiege/Setup/사운드 - 인게임 볼륨 패널 생성")]
        public static void Run()
        {
            // ----------------------------------------------------------------
            // 1) Game.unity 열기.
            // ----------------------------------------------------------------
            if (!System.IO.File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("씬 없음",
                    $"{ScenePath} 를 찾을 수 없습니다.", "확인");
                return;
            }
            var scene = EditorSceneManager.OpenScene(ScenePath);

            // ----------------------------------------------------------------
            // 2) InGameSettingsUI 컴포넌트 탐색.
            // ----------------------------------------------------------------
            var settingsUI = Object.FindFirstObjectByType<InGameSettingsUI>();
            if (settingsUI == null)
            {
                EditorUtility.DisplayDialog("컴포넌트 없음",
                    "InGameSettingsUI 컴포넌트를 Game.unity에서 찾을 수 없습니다.\n" +
                    "Game 씬 셋업이 완료된 후 실행해주세요.",
                    "확인");
                return;
            }

            var so = new SerializedObject(settingsUI);

            // ----------------------------------------------------------------
            // 3) 팝업 박스(_panel)의 Transform 가져오기.
            //    _panel(AnimatedPanel)의 gameObject가 팝업 박스 본체.
            // ----------------------------------------------------------------
            var panelProp = so.FindProperty("_panel");
            if (panelProp == null || panelProp.objectReferenceValue == null)
            {
                EditorUtility.DisplayDialog("_panel 없음",
                    "InGameSettingsUI._panel 필드가 연결되어 있지 않습니다.\n" +
                    "먼저 _panel을 Inspector에서 연결해주세요.",
                    "확인");
                return;
            }
            var panelComp   = panelProp.objectReferenceValue as Component;
            var panelParent = panelComp.transform; // 팝업 박스 본체 Transform

            // ----------------------------------------------------------------
            // 4) 기존 버튼(_soundButton, _forfeitButton) 참조 가져오기.
            // ----------------------------------------------------------------
            var soundBtnProp   = so.FindProperty("_soundButton");
            var forfeitBtnProp = so.FindProperty("_forfeitButton");
            var soundBtn       = soundBtnProp?.objectReferenceValue   as Button;
            var forfeitBtn     = forfeitBtnProp?.objectReferenceValue as Button;

            // ----------------------------------------------------------------
            // 5) MainButtonContainer 생성 — 팝업 박스 직속 자식.
            //    기존 버튼을 이 컨테이너 아래로 이동한다.
            // ----------------------------------------------------------------
            var mainContainerGo = GetOrCreateUIChild("MainButtonContainer", panelParent);
            StretchFull(mainContainerGo.GetComponent<RectTransform>());
            var mainContainerCg = GetOrAdd<CanvasGroup>(mainContainerGo);

            // VLG가 없으면 추가 — 버튼들을 세로로 정렬한다.
            if (mainContainerGo.GetComponent<VerticalLayoutGroup>() == null)
            {
                var vlg = mainContainerGo.AddComponent<VerticalLayoutGroup>();
                vlg.padding               = new RectOffset(40, 40, 40, 40);
                vlg.spacing               = 24f;
                vlg.childAlignment        = TextAnchor.MiddleCenter;
                vlg.childControlWidth     = true;
                vlg.childForceExpandWidth = true;
                vlg.childControlHeight    = true;
                vlg.childForceExpandHeight = false;
            }

            // 기존 버튼을 MainButtonContainer 아래로 재배치.
            if (soundBtn  != null) soundBtn.transform.SetParent(mainContainerGo.transform,  false);
            if (forfeitBtn != null) forfeitBtn.transform.SetParent(mainContainerGo.transform, false);

            // ----------------------------------------------------------------
            // 6) VolumePanel 생성 — MainButtonContainer의 형제, 초기 숨김.
            // ----------------------------------------------------------------
            var volumePanelGo = GetOrCreateUIChild("VolumePanel", panelParent);
            StretchFull(volumePanelGo.GetComponent<RectTransform>());
            var volumePanelCg = GetOrAdd<CanvasGroup>(volumePanelGo);
            // 초기 숨김 상태 (ShowVolumePanel 호출 전까지 보이지 않음).
            volumePanelCg.alpha          = 0f;
            volumePanelCg.interactable   = false;
            volumePanelCg.blocksRaycasts = false;

            // SliderContainer (VerticalLayoutGroup) — 슬라이더 3행을 세로로 배치.
            var sliderContainerGo = GetOrCreateUIChild("SliderContainer", volumePanelGo.transform);
            var scRt              = sliderContainerGo.GetComponent<RectTransform>();
            scRt.anchorMin        = new Vector2(0f, 0.3f);
            scRt.anchorMax        = new Vector2(1f, 0.85f);
            scRt.pivot            = new Vector2(0.5f, 0.5f);
            scRt.anchoredPosition = Vector2.zero;
            scRt.sizeDelta        = Vector2.zero;
            var scVlg = GetOrAdd<VerticalLayoutGroup>(sliderContainerGo);
            scVlg.padding               = new RectOffset(20, 20, 20, 20);
            scVlg.spacing               = 16f;
            scVlg.childAlignment        = TextAnchor.MiddleCenter;
            scVlg.childControlWidth     = true;
            scVlg.childForceExpandWidth = true;
            scVlg.childControlHeight    = true;
            scVlg.childForceExpandHeight = false;

            // 슬라이더 3행 생성.
            CreateSliderRow(sliderContainerGo.transform, "MasterRow", "마스터",
                out Slider masterSlider, out TMP_Text masterText);
            CreateSliderRow(sliderContainerGo.transform, "BGMRow",    "배경음",
                out Slider bgmSlider,    out TMP_Text bgmText);
            CreateSliderRow(sliderContainerGo.transform, "SFXRow",    "효과음",
                out Slider sfxSlider,    out TMP_Text sfxText);

            // BackButton — VolumePanel 하단 중앙.
            var backBtnGo = GetOrCreateUIChild("BackButton", volumePanelGo.transform);
            var backBtnRt = backBtnGo.GetComponent<RectTransform>();
            backBtnRt.anchorMin        = new Vector2(0.25f, 0.05f);
            backBtnRt.anchorMax        = new Vector2(0.75f, 0.2f);
            backBtnRt.pivot            = new Vector2(0.5f, 0.5f);
            backBtnRt.anchoredPosition = Vector2.zero;
            backBtnRt.sizeDelta        = Vector2.zero;
            var backBtnImg = GetOrAdd<Image>(backBtnGo);
            backBtnImg.color = new Color(0.7f, 0.7f, 0.9f, 1f);
            var backBtn = GetOrAdd<Button>(backBtnGo);
            backBtn.targetGraphic = backBtnImg;

            // 뒤로가기 버튼 텍스트 라벨
            var backLabelGo  = GetOrCreateUIChild("Label", backBtnGo.transform);
            StretchFull(backLabelGo.GetComponent<RectTransform>());
            var backLabelTmp = GetOrAdd<TextMeshProUGUI>(backLabelGo);
            backLabelTmp.text      = "← 뒤로";
            backLabelTmp.fontSize  = 36f;
            backLabelTmp.alignment = TextAlignmentOptions.Center;
            backLabelTmp.color     = Color.black;
            // 규칙 6 — Maplestory Light SDF 폰트 적용.
            var backFont = LoadProjectFont();
            if (backFont != null)
            {
                backLabelTmp.font = backFont;
                EditorUtility.SetDirty(backLabelTmp); // 씬 저장 시 반영되도록 dirty 마크
            }

            // ----------------------------------------------------------------
            // 7) InGameSettingsUI SerializedObject 필드 연결.
            // ----------------------------------------------------------------
            so.FindProperty("_mainButtonContainer").objectReferenceValue = mainContainerCg;
            so.FindProperty("_volumePanelGroup").objectReferenceValue    = volumePanelCg;
            so.FindProperty("_masterSlider").objectReferenceValue        = masterSlider;
            so.FindProperty("_bgmSlider").objectReferenceValue           = bgmSlider;
            so.FindProperty("_sfxSlider").objectReferenceValue           = sfxSlider;
            so.FindProperty("_backButton").objectReferenceValue          = backBtn;
            so.FindProperty("_masterValueText").objectReferenceValue     = masterText;
            so.FindProperty("_bgmValueText").objectReferenceValue        = bgmText;
            so.FindProperty("_sfxValueText").objectReferenceValue        = sfxText;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ----------------------------------------------------------------
            // 8) 씬 저장.
            // ----------------------------------------------------------------
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SetupInGameVolumePanel] 완료.\n" +
                      "  MainButtonContainer + VolumePanel(슬라이더 3종 + 뒤로버튼) 생성 완료.");
            EditorUtility.DisplayDialog("완료", "인게임 볼륨 패널 설정이 완료되었습니다.", "확인");
        }

        // --------------------------------------------------------------------
        // 슬라이더 행 생성 헬퍼
        // --------------------------------------------------------------------

        /// <summary>
        /// "Label + Slider + ValueText" 한 행(HorizontalLayoutGroup)을 생성한다.
        /// </summary>
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
            // 규칙 6 — Maplestory Light SDF 폰트 적용.
            var font = LoadProjectFont();
            if (font != null)
            {
                labelTmp.font = font;
                EditorUtility.SetDirty(labelTmp); // 씬 저장 시 반영되도록 dirty 마크
            }

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
            // 규칙 6 — Maplestory Light SDF 폰트 적용.
            if (font != null)
            {
                valueTmp.font = font;
                EditorUtility.SetDirty(valueTmp); // 씬 저장 시 반영되도록 dirty 마크
            }
            valueText = valueTmp;
        }

        /// <summary>Unity UI 표준 Slider를 코드로 생성한다.</summary>
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

            // Fill Area — 현재 값을 채운 부분. 앵커 비율 기반 배치(규칙 2), 고정 픽셀 오프셋 제거.
            var fillAreaGo = GetOrCreateUIChild("Fill Area", sliderGo.transform);
            var fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRt.offsetMin = Vector2.zero;
            fillAreaRt.offsetMax = Vector2.zero;
            var fillGo = GetOrCreateUIChild("Fill", fillAreaGo.transform);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;
            var fillImg = GetOrAdd<Image>(fillGo);
            fillImg.color   = new Color(0.6f, 0.4f, 0.9f, 1f);
            slider.fillRect = fillRt;

            // Handle Slide Area — 손잡이 드래그 영역. 앵커 비율 기반 배치(규칙 2).
            var handleAreaGo = GetOrCreateUIChild("Handle Slide Area", sliderGo.transform);
            var handleAreaRt = handleAreaGo.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = Vector2.zero;
            handleAreaRt.offsetMax = Vector2.zero;
            var handleGo = GetOrCreateUIChild("Handle", handleAreaGo.transform);
            var handleRt = handleGo.GetComponent<RectTransform>();
            // 핸들은 슬라이더 값에 따라 x가 이동하므로 anchorMax.x=0을 유지하고,
            // 세로는 앵커 비율로 배치한다. 너비만 터치 영역 확보를 위해 고정한다.
            handleRt.anchorMin = new Vector2(0f, 0.1f);
            handleRt.anchorMax = new Vector2(0f, 0.9f);
            handleRt.sizeDelta = new Vector2(40f, 0f); // 핸들 너비만 고정(터치 영역)
            var handleImg = GetOrAdd<Image>(handleGo);
            handleImg.color      = Color.white;
            slider.handleRect    = handleRt;
            slider.targetGraphic = handleImg;

            return slider;
        }

        // --------------------------------------------------------------------
        // 공통 유틸
        // --------------------------------------------------------------------

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
