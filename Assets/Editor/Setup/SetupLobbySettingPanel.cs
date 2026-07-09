// ============================================================================
// SetupLobbySettingPanel.cs
// Lobby.unity 씬의 LobbySettingsView(SettingPanel) 사운드 서브 화면에
// VolumeButtonContainer(음소거/초기화 버튼)를 추가하는 에디터 셋업 스크립트.
//
// 실행 방법: Unity 상단 메뉴 → Hexiege/Setup/사운드 - 로비 설정 패널(볼륨 버튼) 구성
//
// 씬 구조 (실행 전 확인된 상태):
//   SettingPanel
//     └── LobbySettingsView [GO]  ← LobbySettingsView 컴포넌트 (soundButton/soundSubView 연결됨)
//           └── SubViewContainer
//                 └── SoundSubView  ← _soundSubView CanvasGroup [fid:1316763635]
//                       ├── SliderContainer  ← 슬라이더 3종 이미 존재
//                       └── VolumeButtonContainer (신규 생성 — 기존 잔해 있으면 삭제 후 재생성)
//                             ├── MuteToggleSlot
//                             │     ├── OnButton  (소리 켜기, 초기 숨김)
//                             │     └── OffButton (음소거,  초기 표출)
//                             └── ResetButton
//
// 주의:
//   - 씬에 LobbySettingsView가 두 개 있을 수 있음(ProfilePanel/SettingPanel).
//     _soundButton이 연결된 인스턴스(SettingPanel 쪽)를 선택한다.
//   - 기존 스크립트(SetupLobbySettingsTab.cs)는 수정하지 않는다.
//   - 이미 생성된 VolumeButtonContainer는 삭제 후 재생성한다(불완전 상태 방지).
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
    public static class SetupLobbySettingPanel
    {
        private const string ScenePath = "Assets/_Project/Scenes/Lobby.unity";
        private const string FontPath  = "Assets/_Project/Fonts/Maplestory Bold SDF.asset";

        // 소리 켜짐 = 초록, 음소거 = 빨강 (InGameSettingsUI.ColorSoundOn과 동일)
        private static readonly Color ColorSoundOn = new Color(0.2f, 0.8f, 0.3f, 1f);

        [MenuItem("Hexiege/Setup/사운드 - 로비 설정 패널(볼륨 버튼) 구성")]
        public static void Run()
        {
            // ------------------------------------------------------------------
            // 1) Lobby.unity 열기
            // ------------------------------------------------------------------
            if (!System.IO.File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("씬 없음", ScenePath + " 를 찾을 수 없습니다.", "확인");
                return;
            }
            var scene = EditorSceneManager.OpenScene(ScenePath);

            // ------------------------------------------------------------------
            // 2) LobbySettingsView 중 _soundButton이 연결된 인스턴스 선택
            //    (SettingPanel 쪽, ProfilePanel 쪽은 _soundButton이 null)
            // ------------------------------------------------------------------
            var allViews = Object.FindObjectsByType<LobbySettingsView>(FindObjectsSortMode.None);
            if (allViews == null || allViews.Length == 0)
            {
                EditorUtility.DisplayDialog("컴포넌트 없음",
                    "LobbySettingsView가 없습니다. 먼저 '사운드 - 로비 설정 탭 구성'을 실행하세요.", "확인");
                return;
            }

            LobbySettingsView targetView = null;
            foreach (var v in allViews)
            {
                var so_ = new SerializedObject(v);
                var soundBtn = so_.FindProperty("_soundButton")?.objectReferenceValue;
                if (soundBtn != null) { targetView = v; break; }
            }
            if (targetView == null)
            {
                EditorUtility.DisplayDialog("대상 없음",
                    "_soundButton이 연결된 LobbySettingsView를 찾지 못했습니다.\n" +
                    "먼저 '사운드 - 로비 설정 탭 구성'을 실행하세요.", "확인");
                return;
            }

            var so = new SerializedObject(targetView);

            // ------------------------------------------------------------------
            // 3) SoundSubView CanvasGroup 가져오기
            // ------------------------------------------------------------------
            var soundSubViewCg = so.FindProperty("_soundSubView")?.objectReferenceValue as CanvasGroup;
            if (soundSubViewCg == null)
            {
                EditorUtility.DisplayDialog("_soundSubView 없음",
                    "_soundSubView가 연결되어 있지 않습니다.\n" +
                    "먼저 '사운드 - 로비 설정 탭 구성'을 실행하세요.", "확인");
                return;
            }

            // ------------------------------------------------------------------
            // 4) 슬라이더 참조 가져오기
            // ------------------------------------------------------------------
            var masterSlider = so.FindProperty("_masterSlider")?.objectReferenceValue as Slider;
            var bgmSlider    = so.FindProperty("_bgmSlider")?.objectReferenceValue    as Slider;
            var sfxSlider    = so.FindProperty("_sfxSlider")?.objectReferenceValue    as Slider;

            // ------------------------------------------------------------------
            // 5) 기존 VolumeButtonContainer 삭제 (이전 실행 잔해 제거)
            // ------------------------------------------------------------------
            var existingContainer = soundSubViewCg.transform.Find("VolumeButtonContainer");
            if (existingContainer != null)
            {
                Object.DestroyImmediate(existingContainer.gameObject);
                Debug.Log("[SetupLobbySettingPanel] 기존 VolumeButtonContainer 삭제 완료.");
            }

            // ------------------------------------------------------------------
            // 6) VolumeButtonContainer 신규 생성 (HorizontalLayoutGroup, 가로 배치)
            // ------------------------------------------------------------------
            var containerGo = new GameObject("VolumeButtonContainer", typeof(RectTransform));
            containerGo.transform.SetParent(soundSubViewCg.transform, false);

            var containerRt = containerGo.GetComponent<RectTransform>();
            containerRt.anchorMin        = new Vector2(0.05f, 0.02f);
            containerRt.anchorMax        = new Vector2(0.95f, 0.18f);
            containerRt.pivot            = new Vector2(0.5f, 0.5f);
            containerRt.anchoredPosition = Vector2.zero;
            containerRt.sizeDelta        = Vector2.zero;

            var hlg = containerGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing                = 20f;
            hlg.childAlignment         = TextAnchor.MiddleCenter;
            hlg.childControlWidth      = true;
            hlg.childForceExpandWidth  = true;
            hlg.childControlHeight     = true;
            hlg.childForceExpandHeight = true;
            EditorUtility.SetDirty(containerGo);

            // ------------------------------------------------------------------
            // 7) MuteToggleSlot — OnButton / OffButton 겹침 배치
            // ------------------------------------------------------------------
            var slotGo = new GameObject("MuteToggleSlot", typeof(RectTransform));
            slotGo.transform.SetParent(containerGo.transform, false);
            slotGo.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

            // OnButton (소리 켜기 — 뮤트 상태일 때만 표출, 초기 숨김)
            var onBtn = CreateButton(slotGo.transform, "OnButton", "소리 켜기");
            StretchFull(onBtn.GetComponent<RectTransform>());
            var onCg = onBtn.gameObject.AddComponent<CanvasGroup>();
            SetCgVisible(onCg, false);

            // OffButton (음소거 — 언뮤트 상태일 때 표출, 초기 표출)
            var offBtn = CreateButton(slotGo.transform, "OffButton", "음소거");
            StretchFull(offBtn.GetComponent<RectTransform>());
            var offCg = offBtn.gameObject.AddComponent<CanvasGroup>();
            SetCgVisible(offCg, true);

            // ------------------------------------------------------------------
            // 8) ResetButton
            // ------------------------------------------------------------------
            var resetBtn = CreateButton(containerGo.transform, "ResetButton", "초기화");

            // ------------------------------------------------------------------
            // 9) 슬라이더 Fill / Background 초기색 → 초록(소리 켜짐 상태)
            // ------------------------------------------------------------------
            SetSliderColor(masterSlider, ColorSoundOn);
            SetSliderColor(bgmSlider,    ColorSoundOn);
            SetSliderColor(sfxSlider,    ColorSoundOn);

            // ------------------------------------------------------------------
            // 10) LobbySettingsView 필드 연결
            // ------------------------------------------------------------------
            SetRef(so, "_onButton",    onBtn);
            SetRef(so, "_offButton",   offBtn);
            SetRef(so, "_resetButton", resetBtn);

            SetRef(so, "_masterFill",       FindFill(masterSlider));
            SetRef(so, "_bgmFill",          FindFill(bgmSlider));
            SetRef(so, "_sfxFill",          FindFill(sfxSlider));
            SetRef(so, "_masterBackground", FindBackground(masterSlider));
            SetRef(so, "_bgmBackground",    FindBackground(bgmSlider));
            SetRef(so, "_sfxBackground",    FindBackground(sfxSlider));

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(targetView);

            // ------------------------------------------------------------------
            // 11) 씬 저장
            // ------------------------------------------------------------------
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SetupLobbySettingPanel] 완료. VolumeButtonContainer(On/Off/Reset) 생성, Fill/Background 초록 초기화 완료.");
            EditorUtility.DisplayDialog("완료", "로비 설정 패널(볼륨 버튼) 구성이 완료되었습니다.", "확인");
        }

        // -----------------------------------------------------------------------
        // 헬퍼
        // -----------------------------------------------------------------------

        private static Button CreateButton(Transform parent, string name, string label)
        {
            var go  = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

            var img = go.AddComponent<Image>();
            ApplySprite(img, "ui_btn_lavender");

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            StretchFull(labelGo.GetComponent<RectTransform>());

            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 40f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.black;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) { tmp.font = font; EditorUtility.SetDirty(tmp); }

            return btn;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin        = Vector2.zero;
            rt.anchorMax        = Vector2.one;
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.zero;
        }

        private static void SetCgVisible(CanvasGroup cg, bool visible)
        {
            cg.alpha          = visible ? 1f : 0f;
            cg.blocksRaycasts = visible;
            cg.interactable   = visible;
        }

        private static void ApplySprite(Image img, string keyword)
        {
            var guids = AssetDatabase.FindAssets(keyword + " t:Sprite");
            if (guids.Length == 0) return;
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (sp == null) return;
            img.sprite = sp;
            img.type   = Image.Type.Simple;
            img.color  = Color.white;
        }

        private static Image FindFill(Slider slider)
        {
            if (slider == null) return null;
            var t = slider.transform.Find("Fill Area/Fill");
            if (t != null) return t.GetComponent<Image>();
            return slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        }

        private static Image FindBackground(Slider slider)
        {
            if (slider == null) return null;
            var t = slider.transform.Find("Background");
            return t != null ? t.GetComponent<Image>() : null;
        }

        private static void SetSliderColor(Slider slider, Color color)
        {
            var fill = FindFill(slider);
            var bg   = FindBackground(slider);
            if (fill != null) { fill.color = color; EditorUtility.SetDirty(fill); }
            if (bg   != null) { bg.color   = color; EditorUtility.SetDirty(bg);   }
        }

        private static void SetRef(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning("[SetupLobbySettingPanel] 필드 없음: " + prop); return; }
            p.objectReferenceValue = value;
        }
    }
}
