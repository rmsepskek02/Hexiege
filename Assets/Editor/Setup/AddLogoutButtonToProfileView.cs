using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Hexiege.Presentation;

namespace Hexiege.EditorTools.Setup
{
    /// <summary>
    /// Lobby.unity 씬의 ProfilePanel에 ProfileView 컴포넌트와 로그아웃 버튼을
    /// 자동으로 추가하는 일회성 에디터 셋업 스크립트입니다.
    /// (시각적 배치는 임시이며, 추후 ProfileView 전체 구축 시 재설계 예정)
    /// </summary>
    public static class AddLogoutButtonToProfileView
    {
        // Lobby 씬 경로 (프로젝트 내 실제 위치)
        private const string LobbyScenePath = "Assets/_Project/Scenes/Lobby.unity";

        // 적용할 폰트 에셋 경로
        private const string FontAssetPath = "Assets/_Project/Fonts/Maplestory Bold SDF.asset";

        // 탐색할 패널 이름
        private const string ProfilePanelName = "ProfilePanel";

        [MenuItem("Hexiege/Setup/ProfileView 로그아웃 버튼 추가")]
        public static void Run()
        {
            // 1) Lobby 씬을 연다. (이미 열려있으면 그대로 사용)
            Scene scene = GetOrOpenLobbyScene();
            if (!scene.IsValid())
            {
                Debug.LogError($"[Setup] Lobby 씬을 열 수 없습니다: {LobbyScenePath}");
                return;
            }

            // 2) 씬 루트를 순회하여 "ProfilePanel" GameObject를 탐색한다.
            GameObject profilePanel = FindInScene(scene, ProfilePanelName);
            if (profilePanel == null)
            {
                Debug.LogError($"[Setup] 씬에서 '{ProfilePanelName}' GameObject를 찾지 못했습니다.");
                return;
            }

            // 3) ProfilePanel에 ProfileView 컴포넌트를 부착한다. (이미 있으면 재사용)
            ProfileView profileView = profilePanel.GetComponent<ProfileView>();
            if (profileView == null)
            {
                profileView = profilePanel.AddComponent<ProfileView>();
                Debug.Log("[Setup] ProfileView 컴포넌트를 새로 추가했습니다.");
            }

            // 4) ProfilePanel 직하위에 LogoutButton을 생성한다.
            Button logoutButton = CreateLogoutButton(profilePanel.transform);

            // 5) SerializedObject로 ProfileView._logoutButton 필드에 버튼을 연결한다.
            AssignLogoutButtonField(profileView, logoutButton);

            // 6) 씬을 dirty 처리하고 저장한다.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 7) 완료 로그 출력
            Debug.Log("[Setup] ProfileView 로그아웃 버튼 추가 완료 및 씬 저장 완료.");
        }

        /// <summary>
        /// 이미 열려있는 Lobby 씬이 있으면 반환하고, 없으면 Additive로 연다.
        /// </summary>
        private static Scene GetOrOpenLobbyScene()
        {
            // 현재 열려있는 씬들 중 Lobby 씬이 있는지 확인
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.path == LobbyScenePath)
                    return s;
            }

            // 열려있지 않으면 Additive 모드로 연다.
            return EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Additive);
        }

        /// <summary>
        /// 지정한 씬의 모든 루트 오브젝트를 순회하며 이름으로 GameObject를 찾는다.
        /// 비활성 오브젝트도 포함하여 탐색한다.
        /// </summary>
        private static GameObject FindInScene(Scene scene, string targetName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                // 자식까지 포함(includeInactive=true)하여 모든 Transform을 검사
                Transform[] all = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in all)
                {
                    if (t.name == targetName)
                        return t.gameObject;
                }
            }
            return null;
        }

        /// <summary>
        /// ProfilePanel 직하위에 Button + Image + 자식 Text(로그아웃)를 가진
        /// LogoutButton GameObject를 생성하여 Button 컴포넌트를 반환한다.
        /// </summary>
        private static Button CreateLogoutButton(Transform parent)
        {
            // 버튼 본체 생성
            GameObject buttonGo = new GameObject("LogoutButton", typeof(RectTransform));
            buttonGo.transform.SetParent(parent, false);

            // 배경 이미지 (흰색)
            Image image = buttonGo.AddComponent<Image>();
            image.color = Color.white;

            // 버튼 컴포넌트
            Button button = buttonGo.AddComponent<Button>();
            button.targetGraphic = image;

            // RectTransform 임시 배치: 패널 하단에 가로로 꽉 채운 위치
            RectTransform rt = buttonGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 60f);
            rt.sizeDelta = new Vector2(0f, 80f);

            // 자식 텍스트 생성
            CreateButtonLabel(buttonGo.transform);

            return button;
        }

        /// <summary>
        /// 버튼 자식으로 "Text" GameObject를 만들고 TextMeshProUGUI로 "로그아웃"을 표시한다.
        /// </summary>
        private static void CreateButtonLabel(Transform buttonTransform)
        {
            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(buttonTransform, false);

            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "로그아웃";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;
            tmp.enableAutoSizing = true;

            // 폰트 적용 (없으면 경고만 출력하고 기본 폰트 유지)
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font != null)
                tmp.font = font;
            else
                Debug.LogWarning($"[Setup] 폰트 에셋을 찾지 못했습니다: {FontAssetPath}");

            // 텍스트는 버튼 영역 전체를 채우도록 stretch
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// SerializedObject를 사용해 ProfileView의 private 필드 _logoutButton에
        /// 생성한 Button을 연결한다.
        /// </summary>
        private static void AssignLogoutButtonField(ProfileView profileView, Button logoutButton)
        {
            SerializedObject so = new SerializedObject(profileView);
            SerializedProperty prop = so.FindProperty("_logoutButton");
            if (prop == null)
            {
                Debug.LogError("[Setup] ProfileView에서 '_logoutButton' 필드를 찾지 못했습니다.");
                return;
            }

            prop.objectReferenceValue = logoutButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
