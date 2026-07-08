// ============================================================================
// SetupInGameSettingsPanel.cs
// Game.unity 씬의 InGameSettingsUI 팝업에 "VolumeButtonContainer(음소거/초기화/뒤로)"와
// 메인 버튼 그룹의 "프로필 버튼"을 추가하는 1회성 에디터 셋업 스크립트.
//
// 실행 방법: Unity 상단 메뉴 → Hexiege/Setup/사운드 - 인게임 설정 패널(볼륨 버튼) 구성
//
// 중요:
//   - 기존 SetupInGameVolumePanel.cs가 만든 결과물(MainButtonContainer + VolumePanel +
//     슬라이더 3종 + BackButton) 위에 "증분(덧붙이기)"으로 동작한다.
//   - 이미 셋업된 씬에서 다시 실행해도 안전하도록 GetOrCreate 방식으로 작성했다.
//   - 기존 스크립트는 수정하지 않는다(별도 신규 파일).
//
// 추가되는 계층:
//   (팝업 박스 _panel)
//     ├── MainButtonContainer
//     │     ├── SoundButton                  ← 기존
//     │     ├── ProfileButton (신규, 미구현)  ← 추가
//     │     └── ForfeitButton                ← 기존
//     └── VolumePanel
//           ├── SliderContainer              ← 기존(Fill/Background 색을 초록으로 초기화)
//           └── VolumeButtonContainer (신규)  ← 추가
//                 ├── MuteToggleSlot
//                 │     ├── OnButton  (전체 소리켜기 — 초기 숨김)
//                 │     └── OffButton (전체 음소거 — 초기 표출)
//                 ├── ResetButton
//                 └── BackButton              ← 기존 BackButton을 이 컨테이너로 이동
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
    /// Game.unity의 InGameSettingsUI에 VolumeButtonContainer와 프로필 버튼을 증분 구성하는 에디터 도구.
    /// </summary>
    public static class SetupInGameSettingsPanel
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        // 규칙 6 — 모든 TextMeshProUGUI 폰트는 반드시 Maplestory Bold SDF를 사용한다.
        private const string FontPath = "Assets/_Project/Fonts/Maplestory Bold SDF.asset";

        // 규칙 3 — 기본(소리 켜짐) 색상은 초록. 슬라이더 Fill/Background 초기색으로 사용한다.
        //   UI 스크립트의 ColorSoundOn과 동일한 값이어야 한다.
        private static readonly Color ColorSoundOn = new Color(0.2f, 0.8f, 0.3f, 1f); // 초록

        [MenuItem("Hexiege/Setup/사운드 - 인게임 설정 패널(볼륨 버튼) 구성")]
        public static void Run()
        {
            // ----------------------------------------------------------------
            // 1) Game.unity 열기.
            // ----------------------------------------------------------------
            if (!System.IO.File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("씬 없음", $"{ScenePath} 를 찾을 수 없습니다.", "확인");
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
                    "먼저 'Hexiege/Setup/사운드 - 인게임 볼륨 패널 생성'을 실행해주세요.",
                    "확인");
                return;
            }

            var so = new SerializedObject(settingsUI);

            // ----------------------------------------------------------------
            // 3) 기존 참조(메인 버튼 그룹, 볼륨 패널, 슬라이더, 버튼) 가져오기.
            // ----------------------------------------------------------------
            var mainContainerCg = so.FindProperty("_mainButtonContainer")?.objectReferenceValue as CanvasGroup;
            var volumePanelCg   = so.FindProperty("_volumePanelGroup")?.objectReferenceValue   as CanvasGroup;
            var soundBtn        = so.FindProperty("_soundButton")?.objectReferenceValue        as Button;
            var forfeitBtn      = so.FindProperty("_forfeitButton")?.objectReferenceValue      as Button;
            var backBtn         = so.FindProperty("_backButton")?.objectReferenceValue         as Button;
            var masterSlider    = so.FindProperty("_masterSlider")?.objectReferenceValue       as Slider;
            var bgmSlider       = so.FindProperty("_bgmSlider")?.objectReferenceValue          as Slider;
            var sfxSlider       = so.FindProperty("_sfxSlider")?.objectReferenceValue          as Slider;

            if (volumePanelCg == null)
            {
                EditorUtility.DisplayDialog("_volumePanelGroup 없음",
                    "InGameSettingsUI._volumePanelGroup이 연결되어 있지 않습니다.\n" +
                    "먼저 'Hexiege/Setup/사운드 - 인게임 볼륨 패널 생성'을 실행해주세요.",
                    "확인");
                return;
            }

            // ----------------------------------------------------------------
            // 4) 메인 버튼 그룹에 프로필 버튼 추가 (사운드 ↔ 포기 사이).
            //    프로필 버튼은 클릭 이벤트가 없으며, 버튼 필드만 연결한다(UI 규칙 1-1).
            // ----------------------------------------------------------------
            Button profileBtn = null;
            if (mainContainerCg != null)
            {
                profileBtn = CreateButton(mainContainerCg.transform, "ProfileButton", "프로필");

                // 버튼 순서: 사운드 → 프로필 → 포기 로 정렬한다.
                if (soundBtn != null)   soundBtn.transform.SetSiblingIndex(0);
                if (profileBtn != null) profileBtn.transform.SetSiblingIndex(1);
                if (forfeitBtn != null) forfeitBtn.transform.SetSiblingIndex(2);
            }
            else
            {
                Debug.LogWarning("[SetupInGameSettingsPanel] _mainButtonContainer가 없어 프로필 버튼을 추가하지 못했습니다.");
            }

            // ----------------------------------------------------------------
            // 5) VolumeButtonContainer 생성 — VolumePanel 하단, 가로 배치.
            // ----------------------------------------------------------------
            var containerGo = GetOrCreateUIChild("VolumeButtonContainer", volumePanelCg.transform);
            var containerRt = containerGo.GetComponent<RectTransform>();
            containerRt.anchorMin        = new Vector2(0.05f, 0.03f);
            containerRt.anchorMax        = new Vector2(0.95f, 0.18f);
            containerRt.pivot            = new Vector2(0.5f, 0.5f);
            containerRt.anchoredPosition = Vector2.zero;
            containerRt.sizeDelta        = Vector2.zero;
            var hlg = GetOrAdd<HorizontalLayoutGroup>(containerGo);
            hlg.spacing                = 20f;
            hlg.childAlignment         = TextAnchor.MiddleCenter;
            hlg.childControlWidth      = true;
            hlg.childForceExpandWidth  = true;
            hlg.childControlHeight     = true;
            hlg.childForceExpandHeight = true;

            // MuteToggleSlot — On/Off 버튼이 같은 자리에 겹치는 슬롯.
            //   토글은 둘 중 하나만 표출되므로(규칙 4), 두 버튼을 같은 위치에 겹쳐 두고
            //   CanvasGroup.alpha로 하나만 보이게 한다. HLG에서는 이 슬롯이 한 칸을 차지한다.
            var slotGo = GetOrCreateUIChild("MuteToggleSlot", containerGo.transform);
            slotGo.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

            // OnButton — 전체 소리켜기(언뮤트). 슬롯을 가득 채우도록 스트레치.
            var onBtn = CreateButton(slotGo.transform, "OnButton", "소리 켜기");
            StretchFull(onBtn.GetComponent<RectTransform>());
            var onCg = GetOrAdd<CanvasGroup>(onBtn.gameObject);

            // OffButton — 전체 음소거(뮤트). 슬롯을 가득 채우도록 스트레치.
            var offBtn = CreateButton(slotGo.transform, "OffButton", "음소거");
            StretchFull(offBtn.GetComponent<RectTransform>());
            var offCg = GetOrAdd<CanvasGroup>(offBtn.gameObject);

            // 초기 표출 상태: 기본은 언뮤트(소리 켜짐)이므로 '음소거' 버튼을 표출하고 '소리 켜기'는 숨긴다.
            //   (런타임 RefreshMuteVisuals()가 실제 상태로 다시 갱신하지만, 씬 저장 시점의 기본값을 맞춰둔다)
            SetCanvasGroupVisible(onCg,  false);
            SetCanvasGroupVisible(offCg, true);

            // ResetButton — 초기화.
            var resetBtn = CreateButton(containerGo.transform, "ResetButton", "초기화");

            // BackButton — 기존 뒤로 버튼을 이 컨테이너로 이동(규칙 2: 인게임에는 뒤로 버튼 포함).
            //   _backButton 참조는 그대로 유지되므로 필드 재연결은 불필요하다.
            if (backBtn != null)
            {
                backBtn.transform.SetParent(containerGo.transform, false);
                backBtn.GetComponent<RectTransform>().sizeDelta = Vector2.zero; // HLG가 크기를 제어하도록.
            }

            // ----------------------------------------------------------------
            // 6) 슬라이더 Fill/Background 이미지 참조 확보 + 기본색(초록) 적용.
            // ----------------------------------------------------------------
            Image masterFill = FindSliderFill(masterSlider);
            Image bgmFill    = FindSliderFill(bgmSlider);
            Image sfxFill    = FindSliderFill(sfxSlider);
            Image masterBg   = FindSliderBackground(masterSlider);
            Image bgmBg      = FindSliderBackground(bgmSlider);
            Image sfxBg      = FindSliderBackground(sfxSlider);

            // 기본(소리 켜짐) 색상은 초록 (규칙 3).
            SetImageColor(masterFill, ColorSoundOn);
            SetImageColor(bgmFill,    ColorSoundOn);
            SetImageColor(sfxFill,    ColorSoundOn);
            SetImageColor(masterBg,   ColorSoundOn);
            SetImageColor(bgmBg,      ColorSoundOn);
            SetImageColor(sfxBg,      ColorSoundOn);

            // ----------------------------------------------------------------
            // 7) InGameSettingsUI SerializedObject 필드 연결.
            // ----------------------------------------------------------------
            SetRef(so, "_profileButton",    profileBtn);
            SetRef(so, "_onButton",         onBtn);
            SetRef(so, "_offButton",        offBtn);
            SetRef(so, "_resetButton",      resetBtn);
            SetRef(so, "_masterFill",       masterFill);
            SetRef(so, "_bgmFill",          bgmFill);
            SetRef(so, "_sfxFill",          sfxFill);
            SetRef(so, "_masterBackground", masterBg);
            SetRef(so, "_bgmBackground",    bgmBg);
            SetRef(so, "_sfxBackground",    sfxBg);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settingsUI);

            // ----------------------------------------------------------------
            // 8) 씬 저장.
            // ----------------------------------------------------------------
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SetupInGameSettingsPanel] 완료.\n" +
                      "  VolumeButtonContainer(On/Off/Reset/Back) + 프로필 버튼 + Fill/Background 초록 초기화 완료.");
            EditorUtility.DisplayDialog("완료", "인게임 설정 패널(볼륨 버튼) 구성이 완료되었습니다.", "확인");
        }

        // --------------------------------------------------------------------
        // 슬라이더 하위 이미지 탐색 헬퍼
        // --------------------------------------------------------------------

        /// <summary>슬라이더의 Fill 이미지를 찾는다. "Fill Area/Fill" 우선, 없으면 fillRect로 폴백.</summary>
        private static Image FindSliderFill(Slider slider)
        {
            if (slider == null) return null;
            var t = slider.transform.Find("Fill Area/Fill");
            if (t != null) return t.GetComponent<Image>();
            return slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        }

        /// <summary>슬라이더의 트랙 배경(Background) 이미지를 찾는다.</summary>
        private static Image FindSliderBackground(Slider slider)
        {
            if (slider == null) return null;
            var t = slider.transform.Find("Background");
            return t != null ? t.GetComponent<Image>() : null;
        }

        // --------------------------------------------------------------------
        // 공통 유틸
        // --------------------------------------------------------------------

        /// <summary>SerializedObject 프로퍼티에 참조를 설정한다(프로퍼티가 없으면 경고 후 무시).</summary>
        private static void SetRef(SerializedObject so, string propertyName, Object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[SetupInGameSettingsPanel] 필드 '{propertyName}'를 찾을 수 없습니다(연결 생략).");
                return;
            }
            prop.objectReferenceValue = value;
        }

        /// <summary>Image가 있으면 지정 색을 적용하고 dirty 마크한다(없으면 무시).</summary>
        private static void SetImageColor(Image img, Color color)
        {
            if (img == null) return;
            img.color = color;
            EditorUtility.SetDirty(img);
        }

        /// <summary>CanvasGroup 표시/숨김을 설정한다(alpha + blocksRaycasts + interactable 세트).</summary>
        private static void SetCanvasGroupVisible(CanvasGroup cg, bool visible)
        {
            if (cg == null) return;
            cg.alpha          = visible ? 1f : 0f;
            cg.blocksRaycasts = visible;
            cg.interactable   = visible;
        }

        /// <summary>ui_btn_lavender 스프라이트를 적용한 라벨 버튼을 생성한다.</summary>
        private static Button CreateButton(Transform parent, string name, string label)
        {
            var go = GetOrCreateUIChild(name, parent);
            go.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            var img = GetOrAdd<Image>(go);
            ApplySprite(img, "ui_btn_lavender");
            var btn = GetOrAdd<Button>(go);
            btn.targetGraphic = img;

            var labelGo = GetOrCreateUIChild("Label", go.transform);
            StretchFull(labelGo.GetComponent<RectTransform>());
            var tmp = GetOrAdd<TextMeshProUGUI>(labelGo);
            tmp.text      = label;
            tmp.fontSize  = 40f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.black;
            // 규칙 6 — Maplestory Bold SDF 폰트 적용.
            var font = LoadProjectFont();
            if (font != null)
            {
                tmp.font = font;
                EditorUtility.SetDirty(tmp);
            }
            return btn;
        }

        /// <summary>Maplestory Bold SDF 폰트 에셋을 로드한다. 없으면 null(기본 폰트 유지).</summary>
        private static TMP_FontAsset LoadProjectFont()
        {
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
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
