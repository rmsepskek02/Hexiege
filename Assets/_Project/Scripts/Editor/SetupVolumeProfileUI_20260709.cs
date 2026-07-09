// ============================================================================
// SetupVolumeProfileUI_20260709.cs  (1회성 Editor 유틸리티 — 실행 후 삭제 가능)
//
// 목적 (task: 2026-07-09 인게임/로비 볼륨·프로필 UI 로직 연결):
//   이번 작업에서 InGameSettingsUI / LobbySettingsView 스크립트에 새로 추가된
//   Serialized 필드(전체 소리켜기/음소거/초기화 버튼, 프로필 버튼, UIColorConfig 등)를
//   씬 하이러키에서 이름으로 찾아 자동으로 연결한다. 손으로 Inspector를 일일이 드래그하는
//   실수를 줄이고, 과거 교훈(2026-07-08: SetDirty 누락으로 씬 저장 미반영)을 반영하여
//   변경 후 반드시 SetDirty + 씬 저장을 수행한다.
//
// 수행 작업:
//   [Game.unity]
//     - InGameSettingsUI의 신규 필드 자동 연결:
//         _soundOnButton(OnButton), _muteButton(OffButton), _resetButton(ResetButton),
//         _profileButton(ProfileButton), _colorConfig(UIColorConfig 에셋).
//       (프로필 서브 패널 오브젝트는 아직 씬에 없으므로 _profileSubViewGroup/_profileBackButton은
//        발견 시에만 연결하고, 없으면 경고 로그로 안내한다.)
//   [Lobby.unity]
//     - SettingPanel 자식에 붙어 있던 LobbySettingsView 컴포넌트를 SettingPanel 루트로 이동
//       (다른 탭 패널 컨벤션과 동일하게 스크립트+CanvasGroup을 루트에). 기존 참조는 보존.
//     - 이동된 컴포넌트의 신규 필드 자동 연결:
//         _soundOnButton(OnButton), _muteButton(OffButton), _resetButton(ResetButton),
//         _colorConfig(UIColorConfig 에셋).
//     - SettingPanel > ... > VolumeButtonContainer 의 앵커 정리(겹침 해소):
//         anchorMin=(0.15, 0.0), anchorMax=(0.85, 0.15), sizeDelta=(0,0), anchoredPosition=(0,0).
//
// 사용법:
//   Unity 상단 메뉴 → Hexiege/Setup/볼륨·프로필 UI 자동 연결 (2026-07-09)
//   실행하면 Game.unity → Lobby.unity 순서로 자동으로 열고, 연결 후 저장한다.
//   실행 전 열려 있던 씬에 저장하지 않은 변경이 있으면 저장 여부를 물으므로 주의.
//
// 실행 후 확인 사항 (Console 로그 참조):
//   - "[SetupVolumeProfileUI] 완료" 요약에서 각 필드가 OK/경고인지 확인.
//   - Game.unity: 프로필 서브 패널 오브젝트가 아직 없으므로 _profileSubViewGroup 경고는 정상.
//     추후 프로필 서브 패널 오브젝트를 만들면 해당 필드를 수동 연결하거나 이 스크립트를 확장한다.
//   - Lobby.unity: SettingPanel 루트에 LobbySettingsView가 있고, 예전 자식 오브젝트의
//     컴포넌트는 제거되었는지 확인. VolumeButtonContainer가 SliderContainer와 겹치지 않는지 확인.
//
// Editor 전용 — Editor 폴더에 위치하므로 빌드에 포함되지 않는다.
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal; // ComponentUtility (컴포넌트 복사/붙여넣기)
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Hexiege.Presentation;
using Hexiege.Infrastructure;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 인게임/로비 볼륨·프로필 UI의 신규 Serialized 필드를 씬에서 자동 연결하는 1회성 유틸리티.
    /// </summary>
    public static class SetupVolumeProfileUI_20260709
    {
        // 씬 경로 상수.
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string LobbyScenePath = "Assets/_Project/Scenes/Lobby.unity";

        // UIColorConfig 에셋 경로.
        private const string ColorConfigPath = "Assets/_Project/Resources/Config/UIColorConfig.asset";

        // 씬 내 오브젝트 이름 상수 — 하이러키 이름이 바뀌면 이 한 곳만 수정한다.
        private const string NameOnButton = "OnButton";       // 전체 소리켜기
        private const string NameOffButton = "OffButton";     // 전체 음소거
        private const string NameResetButton = "ResetButton"; // 초기화
        private const string NameProfileButton = "ProfileButton";
        private const string NameVolumeButtonContainer = "VolumeButtonContainer";
        private const string NameSettingPanel = "SettingPanel";
        private const string NameProfileSubView = "ProfileSubView"; // 프로필 서브 패널(빈 껍데기)
        private const string NameVolumePanel = "VolumePanel";       // ProfileSubView를 형제로 만들 기준 오브젝트

        [MenuItem("Hexiege/Setup/볼륨·프로필 UI 자동 연결 (2026-07-09)")]
        public static void Run()
        {
            Debug.Log("[SetupVolumeProfileUI] 시작 — Game.unity → Lobby.unity 순서로 자동 연결합니다.");

            // 공통으로 사용할 UIColorConfig 에셋 로드.
            UIColorConfig colorConfig = AssetDatabase.LoadAssetAtPath<UIColorConfig>(ColorConfigPath);
            if (colorConfig == null)
                Debug.LogWarning($"[SetupVolumeProfileUI] UIColorConfig 에셋을 찾지 못했습니다: {ColorConfigPath}");

            SetupGameScene(colorConfig);
            SetupLobbyScene(colorConfig);

            AssetDatabase.SaveAssets();
            Debug.Log("[SetupVolumeProfileUI] 완료 — 위 로그에서 각 필드의 OK/경고 여부를 확인하세요.");
        }

        // ================================================================
        // Game.unity — InGameSettingsUI 신규 필드 연결
        // ================================================================

        private static void SetupGameScene(UIColorConfig colorConfig)
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            InGameSettingsUI settings = FindComponentInScene<InGameSettingsUI>(scene);
            if (settings == null)
            {
                Debug.LogError("[SetupVolumeProfileUI][Game] InGameSettingsUI를 찾지 못했습니다. 연결을 건너뜁니다.");
                return;
            }

            // 볼륨 버튼/프로필 버튼은 InGameSettingsPanel 하위에서 이름으로 탐색한다.
            Transform root = settings.transform;

            Button onButton = FindButton(root, NameOnButton);
            Button offButton = FindButton(root, NameOffButton);
            Button resetButton = FindButton(root, NameResetButton);
            Button profileButton = FindButton(root, NameProfileButton);

            var so = new SerializedObject(settings);
            SetRef(so, "_soundOnButton", onButton, "[Game] _soundOnButton");
            SetRef(so, "_muteButton", offButton, "[Game] _muteButton");
            SetRef(so, "_resetButton", resetButton, "[Game] _resetButton");
            SetRef(so, "_profileButton", profileButton, "[Game] _profileButton");
            SetRef(so, "_colorConfig", colorConfig, "[Game] _colorConfig");

            // 프로필 서브 패널 오브젝트(ProfileSubView)를 확보한다.
            //   이미 씬에 있으면 그대로 재사용하고, 없으면 VolumePanel의 형제로 빈 CanvasGroup 패널을 새로 만든다.
            //   (내부 콘텐츠는 이번 범위 밖 — CanvasGroup 하나만 있는 빈 껍데기로 유지)
            GameObject profileSubView = EnsureProfileSubView(root);
            if (profileSubView != null)
            {
                CanvasGroup group = profileSubView.GetComponent<CanvasGroup>();
                if (group == null) group = profileSubView.AddComponent<CanvasGroup>();
                SetRef(so, "_profileSubViewGroup", group, "[Game] _profileSubViewGroup");

                Button pBack = FindButton(profileSubView.transform, "BackButton");
                if (pBack != null)
                    SetRef(so, "_profileBackButton", pBack, "[Game] _profileBackButton");
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SetupVolumeProfileUI][Game] InGameSettingsUI 연결 및 저장 완료.");
        }

        /// <summary>
        /// 프로필 서브 패널(ProfileSubView) 오브젝트를 확보한다.
        /// 이미 존재하면(이름으로 발견되면) 그대로 반환하고, 없으면 VolumePanel의 형제로 새로 생성한다.
        /// 여러 번 실행해도 중복 생성되지 않도록 idempotent하게 동작한다.
        /// 생성되는 오브젝트는 CanvasGroup 하나만 붙은 빈 껍데기이며, 숨김 상태(alpha=0)로 시작한다.
        /// </summary>
        /// <param name="root">InGameSettingsUI가 붙은 루트 Transform(하위에서 탐색 시작).</param>
        /// <returns>확보한 ProfileSubView GameObject. 기준 VolumePanel을 찾지 못하면 null.</returns>
        private static GameObject EnsureProfileSubView(Transform root)
        {
            // 1) 이미 있으면 재사용(idempotent). 과거 네이밍(ProfilePanel)도 호환 탐색한다.
            GameObject existing = FindDeep(root, NameProfileSubView) ?? FindDeep(root, "ProfilePanel");
            if (existing != null)
            {
                Debug.Log($"[SetupVolumeProfileUI][Game] 기존 ProfileSubView 재사용함 → {existing.name}");
                return existing;
            }

            // 2) 없으면 VolumePanel을 기준으로 그 형제(같은 부모)로 새로 만든다.
            GameObject volumePanel = FindDeep(root, NameVolumePanel);
            if (volumePanel == null)
            {
                // 기준 오브젝트가 없으면 어디에 붙일지 결정할 수 없으므로 생성을 포기하고 경고만 남긴다.
                Debug.LogWarning("[SetupVolumeProfileUI][Game] VolumePanel을 찾지 못해 ProfileSubView를 생성하지 못했습니다. " +
                                 "VolumePanel과 형제로 만들 위치를 특정할 수 없습니다.");
                return null;
            }

            Transform parent = volumePanel.transform.parent; // VolumePanel의 부모(= Panel) 아래에 형제로 생성.

            // RectTransform을 가진 새 UI 오브젝트 생성.
            var newGo = new GameObject(NameProfileSubView, typeof(RectTransform));
            newGo.transform.SetParent(parent, worldPositionStays: false);

            // RectTransform을 VolumePanel과 동일하게 전체 스트레치로 맞춘다(공통 UI 규칙 2 — 앵커 기반, 고정 픽셀 금지).
            var rt = newGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;                 // (0,0)
            rt.anchorMax = Vector2.one;                  // (1,1)
            rt.sizeDelta = Vector2.zero;                 // (0,0)
            rt.anchoredPosition = Vector2.zero;          // (0,0)
            rt.localScale = Vector3.one;

            // CanvasGroup 부착 후 숨김 상태로 시작(공통 UI 규칙 5 — Show/Hide 로직이 제어).
            var group = newGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            EditorUtility.SetDirty(newGo);
            Debug.Log($"[SetupVolumeProfileUI][Game] ProfileSubView 신규 생성함 → 부모 '{parent.name}' 아래, " +
                      "VolumePanel의 형제로 배치(빈 CanvasGroup, alpha=0).");
            return newGo;
        }

        // ================================================================
        // Lobby.unity — LobbySettingsView 이동 + 신규 필드 연결 + 앵커 정리
        // ================================================================

        private static void SetupLobbyScene(UIColorConfig colorConfig)
        {
            Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);

            // 1) SettingPanel 루트 오브젝트 찾기.
            GameObject settingPanel = FindDeepInScene(scene, NameSettingPanel);
            if (settingPanel == null)
            {
                Debug.LogError("[SetupVolumeProfileUI][Lobby] SettingPanel을 찾지 못했습니다. 연결을 건너뜁니다.");
                return;
            }

            // 2) 기존 LobbySettingsView 컴포넌트 위치 확인.
            LobbySettingsView view = FindComponentInScene<LobbySettingsView>(scene);
            if (view == null)
            {
                Debug.LogError("[SetupVolumeProfileUI][Lobby] LobbySettingsView 컴포넌트를 찾지 못했습니다. 연결을 건너뜁니다.");
                return;
            }

            // 3) 컴포넌트가 이미 SettingPanel 루트에 있으면 이동 불필요(재실행 안전).
            LobbySettingsView movedView;
            if (view.gameObject == settingPanel)
            {
                movedView = view;
                Debug.Log("[SetupVolumeProfileUI][Lobby] LobbySettingsView가 이미 SettingPanel 루트에 있습니다. 이동을 건너뜁니다.");
            }
            else
            {
                // 자식에 붙어 있던 컴포넌트를 SettingPanel 루트로 "이동".
                //   Unity는 컴포넌트를 물리적으로 옮길 수 없으므로,
                //   ComponentUtility.CopyComponent/PasteComponentAsNew로 모든 값·참조를 보존한 채
                //   루트에 새로 붙이고, 자식의 원본은 제거한다.
                GameObject oldHost = view.gameObject;
                ComponentUtility.CopyComponent(view);
                bool pasted = ComponentUtility.PasteComponentAsNew(settingPanel);
                if (!pasted)
                {
                    Debug.LogError("[SetupVolumeProfileUI][Lobby] 컴포넌트 붙여넣기에 실패했습니다. 수동 이동이 필요합니다.");
                    return;
                }

                movedView = settingPanel.GetComponent<LobbySettingsView>();
                Object.DestroyImmediate(view); // 자식의 원본 컴포넌트 제거.
                Debug.Log($"[SetupVolumeProfileUI][Lobby] LobbySettingsView를 '{oldHost.name}' → 'SettingPanel' 루트로 이동했습니다.");
            }

            // 4) SettingPanel 루트에 CanvasGroup 보장(다른 탭 패널 컨벤션).
            if (settingPanel.GetComponent<CanvasGroup>() == null)
            {
                settingPanel.AddComponent<CanvasGroup>();
                Debug.Log("[SetupVolumeProfileUI][Lobby] SettingPanel 루트에 CanvasGroup을 추가했습니다.");
            }

            // 5) 신규 필드 연결 — 볼륨 버튼은 SettingPanel 하위(VolumeButtonContainer)에서 탐색.
            Transform root = settingPanel.transform;
            Button onButton = FindButton(root, NameOnButton);
            Button offButton = FindButton(root, NameOffButton);
            Button resetButton = FindButton(root, NameResetButton);

            var so = new SerializedObject(movedView);
            SetRef(so, "_soundOnButton", onButton, "[Lobby] _soundOnButton");
            SetRef(so, "_muteButton", offButton, "[Lobby] _muteButton");
            SetRef(so, "_resetButton", resetButton, "[Lobby] _resetButton");
            SetRef(so, "_colorConfig", colorConfig, "[Lobby] _colorConfig");
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(movedView);

            // 6) VolumeButtonContainer 앵커 정리(겹침 해소).
            FixVolumeButtonContainerAnchors(root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SetupVolumeProfileUI][Lobby] LobbySettingsView 이동/연결 및 저장 완료.");
        }

        /// <summary>
        /// Lobby SettingPanel의 VolumeButtonContainer 앵커를 정리한다.
        /// 고정 픽셀 보정(sizeDelta/anchoredPosition)을 제거하고, SliderContainer(anchorMin.y=0.15)와
        /// 겹치지 않도록 anchorMax.y=0.15로 맞춘다(공통 UI 규칙 2).
        /// </summary>
        private static void FixVolumeButtonContainerAnchors(Transform settingPanelRoot)
        {
            GameObject container = FindDeep(settingPanelRoot, NameVolumeButtonContainer);
            if (container == null)
            {
                Debug.LogWarning("[SetupVolumeProfileUI][Lobby] VolumeButtonContainer를 찾지 못해 앵커 정리를 건너뜁니다.");
                return;
            }

            var rt = container.GetComponent<RectTransform>();
            if (rt == null)
            {
                Debug.LogWarning("[SetupVolumeProfileUI][Lobby] VolumeButtonContainer에 RectTransform이 없습니다.");
                return;
            }

            rt.anchorMin = new Vector2(0.15f, 0.0f);
            rt.anchorMax = new Vector2(0.85f, 0.15f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            EditorUtility.SetDirty(rt);
            Debug.Log("[SetupVolumeProfileUI][Lobby] VolumeButtonContainer 앵커를 정리했습니다 " +
                      "(anchorMin=(0.15,0), anchorMax=(0.85,0.15), sizeDelta=0, anchoredPosition=0).");
        }

        // ================================================================
        // SerializedProperty / 탐색 헬퍼
        // ================================================================

        /// <summary>
        /// SerializedObject의 오브젝트 참조 필드를 설정한다. 필드가 없거나 값이 null이면 경고 로그를 남긴다.
        /// </summary>
        private static void SetRef(SerializedObject so, string propName, Object value, string label)
        {
            SerializedProperty prop = so.FindProperty(propName);
            if (prop == null)
            {
                Debug.LogWarning($"[SetupVolumeProfileUI] 직렬화 필드를 찾지 못했습니다: {propName} ({label})");
                return;
            }

            prop.objectReferenceValue = value;
            if (value == null)
                Debug.LogWarning($"[SetupVolumeProfileUI] 대상 오브젝트를 찾지 못해 null로 연결됨: {label}");
            else
                Debug.Log($"[SetupVolumeProfileUI] 연결 OK: {label} → {value.name}");
        }

        /// <summary>지정한 이름의 자식 GameObject에서 Button 컴포넌트를 찾는다(없으면 null).</summary>
        private static Button FindButton(Transform root, string name)
        {
            GameObject go = FindDeep(root, name);
            return go != null ? go.GetComponent<Button>() : null;
        }

        /// <summary>씬 전체에서 특정 컴포넌트를 가진 첫 오브젝트를 찾는다(비활성 포함).</summary>
        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject rootGo in scene.GetRootGameObjects())
            {
                T found = rootGo.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>씬 전체에서 이름이 일치하는 첫 GameObject를 찾는다(비활성 포함).</summary>
        private static GameObject FindDeepInScene(Scene scene, string name)
        {
            foreach (GameObject rootGo in scene.GetRootGameObjects())
            {
                GameObject found = FindDeep(rootGo.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>root 하위(자기 자신 포함)에서 이름이 일치하는 첫 GameObject를 DFS로 찾는다(비활성 포함).</summary>
        private static GameObject FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root.gameObject;

            for (int i = 0; i < root.childCount; i++)
            {
                GameObject found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
