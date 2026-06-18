// ============================================================================
// AddUIManagerTestButton.cs
// UIManager.ShowConfirm / ShowLoading 동작을 빠르게 검증하기 위한 임시 테스트 버튼.
//
// 실행 방법:
//   Unity 상단 메뉴 → Hexiege/Test/UIManager 테스트 버튼 추가
//
// 생성 위치:
//   Login.unity의 LoginRoot 하위에 [UIManager Test] 버튼을 생성한다.
//
// 테스트 동작:
//   버튼 클릭 → ShowConfirm 팝업 표시
//             → 확인 클릭 시 ShowLoading(true) → 2초 후 ShowLoading(false)
//
// 주의:
//   이 스크립트는 임시 테스트용이다. 테스트 완료 후 씬에서 [UIManager Test] 오브젝트를
//   삭제하고 이 에디터 스크립트 파일도 제거해도 무방하다.
//
// Editor 전용 — 빌드에 포함되지 않는다 (Assets/Editor 폴더).
// ============================================================================

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HexiegeEditor
{
    public static class AddUIManagerTestButton
    {
        private const string LoginScenePath = "Assets/_Project/Scenes/Login.unity";
        private const string FontLightPath  = "Assets/_Project/Fonts/Maplestory Light SDF.asset";

        [MenuItem("Hexiege/Test/UIManager 테스트 버튼 추가")]
        public static void AddTestButton()
        {
            // Login.unity를 Single 모드로 연다.
            var scene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);

            // 이미 테스트 버튼이 있으면 중복 생성을 막는다.
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<UIManagerTestButtonHandler>(true) != null)
                {
                    Debug.LogWarning("[AddUIManagerTestButton] 이미 [UIManager Test] 버튼이 씬에 있습니다. 중복 생성을 막습니다.");
                    return;
                }
            }

            // LoginRoot GameObject 탐색 — 없으면 씬 루트에 직접 생성한다.
            GameObject loginRoot = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                // 이름이 "LoginRoot"인 오브젝트 또는 그 하위에서 탐색한다.
                if (root.name == "LoginRoot")
                {
                    loginRoot = root;
                    break;
                }
                var found = root.transform.Find("LoginRoot");
                if (found != null)
                {
                    loginRoot = found.gameObject;
                    break;
                }
            }

            if (loginRoot == null)
                Debug.LogWarning("[AddUIManagerTestButton] LoginRoot를 찾지 못했습니다. 씬 루트에 [UIManager Test]를 생성합니다.");

            // --- [UIManager Test] 버튼 생성 ---
            // 화면 좌상단에 작게 배치해 기존 UI와 겹치지 않도록 한다.
            GameObject testGo = new GameObject("[UIManager Test]", typeof(RectTransform));
            testGo.transform.SetParent(loginRoot != null ? loginRoot.transform : scene.GetRootGameObjects()[0].transform, false);

            RectTransform rt = testGo.GetComponent<RectTransform>();
            // 좌상단 앵커 고정, 크기 240×80.
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(20f, -20f);
            rt.sizeDelta = new Vector2(240f, 80f);

            // Image — 버튼 배경색 (반투명 빨강: 테스트용임을 시각적으로 구분)
            Image img = testGo.AddComponent<Image>();
            img.color = new Color(0.8f, 0.1f, 0.1f, 0.85f);

            // Button 컴포넌트
            testGo.AddComponent<Button>();

            // 텍스트 레이블
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(testGo.transform, false);
            RectTransform labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text      = "UIManager TEST";
            tmp.fontSize  = 28;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;

            // 규칙 6: Maplestory Light SDF 폰트 적용
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontLightPath);
            if (font != null)
                tmp.font = font;
            else
                Debug.LogWarning($"[AddUIManagerTestButton] 폰트를 찾지 못했습니다({FontLightPath}). 기본 폰트로 생성됩니다.");

            // 런타임 테스트 핸들러 부착
            testGo.AddComponent<UIManagerTestButtonHandler>();

            // 씬 저장
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, LoginScenePath);
            Debug.Log("[AddUIManagerTestButton] [UIManager Test] 버튼 생성 완료. Play 모드에서 버튼을 클릭해 테스트하세요.");
        }
    }
}
