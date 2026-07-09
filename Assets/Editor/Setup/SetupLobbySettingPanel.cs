// ============================================================================
// SetupLobbySettingPanel.cs
// Lobby.unity 씬의 LobbySettingsView 사운드 서브 화면에 "VolumeButtonContainer
// (음소거/초기화)"를 추가하는 1회성 에디터 셋업 스크립트.
//
// 실행 방법: Unity 상단 메뉴 → Hexiege/Setup/사운드 - 로비 설정 패널(볼륨 버튼) 구성
//
// 중요:
//   - 기존 SetupLobbySettingsTab.cs가 만든 결과물(LobbySettingsView + SoundSubView +
//     슬라이더 3종) 위에 "증분(덧붙이기)"으로 동작한다.
//   - 이미 셋업된 씬에서 다시 실행해도 안전하도록 GetOrCreate 방식으로 작성했다.
//   - 기존 스크립트는 수정하지 않는다(별도 신규 파일).
//   - 로비 설정 탭은 탭 구조상 뒤로 버튼이 별도(_backButton)이므로,
//     VolumeButtonContainer에는 뒤로 버튼을 포함하지 않는다(규칙 2).
//
// 추가되는 계층:
//   LobbySettingsView
//     └── SubViewContainer
//           └── SoundSubView
//                 ├── SliderContainer              ← 기존(Fill/Background 색을 초록으로 초기화)
//                 └── VolumeButtonContainer (신규)  ← 추가 (뒤로 버튼 없음)
//                       ├── MuteToggleSlot
//                       │     ├── OnButton  (전체 소리켜기 — 초기 숨김)
//                       │     └── OffButton (전체 음소거 — 초기 표출)
//                       └── ResetButton
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
    /// Lobby.unity의 LobbySettingsView 사운드 화면에 VolumeButtonContainer를 증분 구성하는 에디터 도구.
    /// </summary>
    public static class SetupLobbySettingPanel
    {
        private const string ScenePath = "Assets/_Project/Scenes/Lobby.unity";

        // 규칙 6 — 모든 TextMeshProUGUI 폰트는 반드시 Maplestory Bold SDF를 사용한다.
        private const string FontPath = "Assets/_Project/Fonts/Maplestory Bold SDF.asset";

        // 규칙 3 — 기본(소리 켜짐) 색상은 초록. 슬라이더 Fill/Background 초기색으로 사용한다.
        private static readonly Color ColorSoundOn = new Color(0.2f, 0.8f, 0.3f, 1f); // 초록

        [MenuItem("Hexiege/Setup/사운드 - 로비 설정 패널(볼륨 버튼) 구성")]
        public static void Run()
        {
            // ----------------------------------------------------------------
            // 1) Lobby.unity 열기.
            // ----------------------------------------------------------------
            if (!System.IO.File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("씬 없음", $"{ScenePath} 를 찾을 수 없습니다.", "확인");
                return;
            }
            var scene = EditorSceneManager.OpenScene(ScenePath);

            // ----------------------------------------------------------------
            // 2) LobbySettingsView 컴포넌트 탐색.
            // ----------------------------------------------------------------
            var settingsView = Object.FindFirstObjectByType<LobbySettingsView>();
            if (settingsView == null)
            {
                EditorUtility.DisplayDialog("컴포넌트 없음",
                    "LobbySettingsView 컴포넌트를 Lobby.unity에서 찾을 수 없습니다.\n" +
                    "먼저 'Hexiege/Setup/사운드 - 로비 설정 탭 구성'을 실행해주세요.",
                    "확인");
                return;
            }

            var so = new SerializedObject(settingsView);

            // ----------------------------------------------------------------
            // 3) 기존 참조(사운드 서브 화면, 슬라이더) 가져오기.
            //    _soundSubView가 Inspector에 연결되어 있지 않은 경우,
            //    계층구조에서 "SoundSubView" 이름으로 직접 탐색한다.
            //    기존 에디터 스크립트를 재실행하지 않아도 동작하도록 안전 처리.
            // ----------------------------------------------------------------
            var soundSubViewCg = so.FindProperty("_soundSubView")?.objectReferenceValue as CanvasGroup;
            if (soundSubViewCg == null)
            {
                // 계층구조에서 "SoundSubView" GO를 이름으로 탐색한다.
                var found = FindChildRecursive(settingsView.transform, "SoundSubView");
                if (found != null)
                    soundSubViewCg = found.GetComponent<CanvasGroup>();

                if (soundSubViewCg == null)
                {
                    EditorUtility.DisplayDialog("SoundSubView 없음",
                        "SoundSubView GO를 찾을 수 없습니다.\n" +
                        "먼저 'Hexiege/Setup/사운드 - 로비 설정 탭 구성'을 실행해주세요.",
                        "확인");
                    return;
                }

                // 찾은 CanvasGroup을 _soundSubView 필드에 연결해둔다.
                SetRef(so, "_soundSubView", soundSubViewCg);
            }

            var masterSlider = so.FindProperty("_masterSlider")?.objectReferenceValue as Slider;
            var bgmSlider    = so.FindProperty("_bgmSlider")?.objectReferenceValue    as Slider;
            var sfxSlider    = so.FindProperty("_sfxSlider")?.objectReferenceValue    as Slider;

            // 슬라이더도 연결 안 된 경우 계층구조에서 탐색한다.
            if (masterSlider == null) masterSlider = FindSliderInHierarchy(soundSubViewCg.transform, "MasterSlider");
            if (bgmSlider    == null) bgmSlider    = FindSliderInHierarchy(soundSubViewCg.transform, "BGMSlider");
            if (sfxSlider    == null) sfxSlider    = FindSliderInHierarchy(soundSubViewCg.transform, "SFXSlider");

            // ----------------------------------------------------------------
            // 4) VolumeButtonContainer 생성 — SoundSubView 하단, 가로 배치 (뒤로 버튼 없음).
            // ----------------------------------------------------------------
            var containerGo = GetOrCreateUIChild("VolumeButtonContainer", soundSubViewCg.transform);
            var containerRt = containerGo.GetComponent<RectTransform>();
            containerRt.anchorMin        = new Vector2(0.05f, 0.02f);
            containerRt.anchorMax        = new Vector2(0.95f, 0.13f);
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

            // MuteToggleSlot — On/Off 버튼이 같은 자리에 겹치는 슬롯(규칙 4, 둘 중 하나만 표출).
            var slotGo = GetOrCreateUIChild("MuteToggleSlot", containerGo.transform);
            slotGo.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

            // OnButton — 전체 소리켜기(언뮤트).
            var onBtn = CreateButton(slotGo.transform, "OnButton", "소리 켜기");
            StretchFull(onBtn.GetComponent<RectTransform>());
            var onCg = GetOrAdd<CanvasGroup>(onBtn.gameObject);

            // OffButton — 전체 음소거(뮤트).
            var offBtn = CreateButton(slotGo.transform, "OffButton", "음소거");
            StretchFull(offBtn.GetComponent<RectTransform>());
            var offCg = GetOrAdd<CanvasGroup>(offBtn.gameObject);

            // 초기 표출: 기본은 언뮤트(소리 켜짐) → '음소거' 표출, '소리 켜기' 숨김.
            SetCanvasGroupVisible(onCg,  false);
            SetCanvasGroupVisible(offCg, true);

            // ResetButton — 초기화.
            var resetBtn = CreateButton(containerGo.transform, "ResetButton", "초기화");

            // ----------------------------------------------------------------
            // 5) 슬라이더 Fill/Background 이미지 참조 확보 + 기본색(초록) 적용.
            // ----------------------------------------------------------------
            Image masterFill = FindSliderFill(masterSlider);
            Image bgmFill    = FindSliderFill(bgmSlider);
            Image sfxFill    = FindSliderFill(sfxSlider);
            Image masterBg   = FindSliderBackground(masterSlider);
            Image bgmBg      = FindSliderBackground(bgmSlider);
            Image sfxBg      = FindSliderBackground(sfxSlider);

            SetImageColor(masterFill, ColorSoundOn);
            SetImageColor(bgmFill,    ColorSoundOn);
            SetImageColor(sfxFill,    ColorSoundOn);
            SetImageColor(masterBg,   ColorSoundOn);
            SetImageColor(bgmBg,      ColorSoundOn);
            SetImageColor(sfxBg,      ColorSoundOn);

            // ----------------------------------------------------------------
            // 6) LobbySettingsView SerializedObject 필드 연결.
            // ----------------------------------------------------------------
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
            EditorUtility.SetDirty(settingsView);

            // ----------------------------------------------------------------
            // 7) 씬 저장.
            // ----------------------------------------------------------------
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SetupLobbySettingPanel] 완료.\n" +
                      "  VolumeButtonContainer(On/Off/Reset) + Fill/Background 초록 초기화 완료(뒤로 버튼 없음).");
            EditorUtility.DisplayDialog("완료", "로비 설정 패널(볼륨 버튼) 구성이 완료되었습니다.", "확인");
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
                Debug.LogWarning($"[SetupLobbySettingPanel] 필드 '{propertyName}'를 찾을 수 없습니다(연결 생략).");
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

        /// <summary>
        /// 자식 계층을 재귀 탐색하여 이름이 일치하는 Transform을 반환한다.
        /// 없으면 null.
        /// </summary>
        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// 주어진 Transform 계층에서 이름이 포함된 Slider를 탐색한다.
        /// 없으면 null.
        /// </summary>
        private static Slider FindSliderInHierarchy(Transform root, string nameKeyword)
        {
            foreach (var slider in root.GetComponentsInChildren<Slider>(true))
            {
                if (slider.name.IndexOf(nameKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return slider;
            }
            return null;
        }
    }
}
