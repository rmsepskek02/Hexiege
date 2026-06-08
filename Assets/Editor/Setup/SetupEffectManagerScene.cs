using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hexiege.Presentation;
using Hexiege.Bootstrap;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// [1회성 셋업 스크립트]
    /// Game.unity 씬에 EffectManager GameObject를 배치하고
    /// GameBootstrapper와 Config 에셋들을 자동으로 연결해 주는 에디터 전용 스크립트입니다.
    ///
    /// 동작 흐름:
    ///   Step 1) Config 에셋 3종(Unit/Building/Ui)이 존재하는지 확인
    ///   Step 2) Game.unity 씬을 (필요하면) 열기
    ///   Step 3) EffectManager GameObject 생성 + VFX/SFX 컨테이너 자식 생성 + 필드 연결
    ///   Step 4) GameBootstrapper에 EffectManager / Config 3종 연결
    ///   Step 5) 씬 저장
    ///
    /// 이미 EffectManager가 배치되어 있으면 중복 생성하지 않고 연결 갱신만 수행합니다(멱등성).
    /// 셋업이 끝난 뒤에는 이 파일을 삭제해도 무방합니다.
    /// </summary>
    public static class SetupEffectManagerScene
    {
        // ── Config 에셋 경로 (이미 확정된 고정 경로) ──────────────────────────
        private const string UnitConfigPath = "Assets/_Project/Resources/Config/UnitEffectConfig.asset";
        private const string BuildingConfigPath = "Assets/_Project/Resources/Config/BuildingEffectConfig.asset";
        private const string UiConfigPath = "Assets/_Project/Resources/Config/UiEffectConfig.asset";

        /// <summary>
        /// 메뉴에서 실행되는 진입점입니다.
        /// Hexiege > Setup > [1회성] Game씬 EffectManager 배치 클릭 시 호출됩니다.
        /// </summary>
        [MenuItem("Hexiege/Setup/[1회성] Game씬 EffectManager 배치")]
        public static void Run()
        {
            // ── Step 1: Config 에셋 존재 확인 ─────────────────────────────────
            // 세 에셋 중 하나라도 없으면 셋업을 진행할 수 없으므로 에러 로그 후 중단합니다.
            var unitConfig = AssetDatabase.LoadAssetAtPath<UnitEffectConfig>(UnitConfigPath);
            var buildingConfig = AssetDatabase.LoadAssetAtPath<BuildingEffectConfig>(BuildingConfigPath);
            var uiConfig = AssetDatabase.LoadAssetAtPath<UiEffectConfig>(UiConfigPath);

            if (unitConfig == null || buildingConfig == null || uiConfig == null)
            {
                Debug.LogError("[SetupEffectManagerScene] Config 에셋을 찾을 수 없습니다. " +
                               "'Hexiege/Setup/UnitEffectConfig 생성' 메뉴를 먼저 실행하세요.");
                return;
            }

            // ── Step 2: Game.unity 씬 열기 ───────────────────────────────────
            // 경로를 하드코딩하지 않고 AssetDatabase로 동적 검색합니다.
            // ("t:Scene Game" = Scene 타입이면서 이름에 "Game"이 포함된 에셋)
            string gameScenePath = FindGameScenePath();
            if (string.IsNullOrEmpty(gameScenePath))
            {
                Debug.LogError("[SetupEffectManagerScene] Game.unity 씬을 프로젝트에서 찾을 수 없습니다.");
                return;
            }

            // 현재 열려있는 씬이 이미 Game.unity라면 다시 열 필요가 없습니다.
            Scene activeScene = SceneManager.GetActiveScene();
            bool isGameSceneAlreadyOpen = activeScene.path == gameScenePath;

            if (!isGameSceneAlreadyOpen)
            {
                // 현재 씬에 저장하지 않은 변경사항이 있으면 사용자에게 저장 여부를 물어봅니다.
                // 사용자가 저장/취소를 선택할 수 있으며, 취소하면 false가 반환되어 셋업을 중단합니다.
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.LogWarning("[SetupEffectManagerScene] 씬 저장이 취소되어 셋업을 중단합니다.");
                    return;
                }

                EditorSceneManager.OpenScene(gameScenePath);
            }

            // ── Step 3: EffectManager 존재 확인 및 생성 ───────────────────────
            // 이미 씬에 EffectManager가 있으면 중복 생성하지 않습니다(멱등성).
            EffectManager effectManager = Object.FindFirstObjectByType<EffectManager>();
            GameObject effectManagerGo;

            if (effectManager != null)
            {
                Debug.Log("[SetupEffectManagerScene] 이미 EffectManager가 씬에 배치되어 있습니다.");
                effectManagerGo = effectManager.gameObject;
            }
            else
            {
                // 1) 빈 GameObject 생성 → 이름: EffectManager
                effectManagerGo = new GameObject("EffectManager");

                // 2) EffectManager 컴포넌트 추가
                effectManager = effectManagerGo.AddComponent<EffectManager>();

                // 3) VFX 컨테이너 자식 생성
                var vfxContainer = new GameObject("VFX_Container");
                vfxContainer.transform.SetParent(effectManagerGo.transform, false);

                // 4) SFX 컨테이너 자식 생성
                var sfxContainer = new GameObject("SFX_Container");
                sfxContainer.transform.SetParent(effectManagerGo.transform, false);

                // 5) SerializedObject로 private SerializeField 값 연결
                //    (private 필드는 코드로 직접 대입할 수 없으므로 SerializedObject 경유)
                var soEffect = new SerializedObject(effectManager);
                soEffect.FindProperty("_vfxContainer").objectReferenceValue = vfxContainer.transform;
                soEffect.FindProperty("_sfxContainer").objectReferenceValue = sfxContainer.transform;
                soEffect.ApplyModifiedPropertiesWithoutUndo();

                Debug.Log("[SetupEffectManagerScene] EffectManager GameObject 및 VFX/SFX 컨테이너를 생성했습니다.");
            }

            // ── Step 4: GameBootstrapper 연결 ─────────────────────────────────
            GameBootstrapper bootstrapper = Object.FindFirstObjectByType<GameBootstrapper>();
            if (bootstrapper == null)
            {
                Debug.LogError("[SetupEffectManagerScene] 씬에서 GameBootstrapper를 찾을 수 없습니다.");
                return;
            }

            // GameBootstrapper의 4개 SerializeField를 모두 연결합니다.
            // 이미 연결되어 있어도 덮어쓰기(갱신)합니다.
            var soBootstrapper = new SerializedObject(bootstrapper);
            soBootstrapper.FindProperty("_effectManager").objectReferenceValue = effectManager;
            soBootstrapper.FindProperty("_unitEffectConfig").objectReferenceValue = unitConfig;
            soBootstrapper.FindProperty("_buildingEffectConfig").objectReferenceValue = buildingConfig;
            soBootstrapper.FindProperty("_uiEffectConfig").objectReferenceValue = uiConfig;
            soBootstrapper.ApplyModifiedPropertiesWithoutUndo();

            // ── Step 5: 씬 저장 및 완료 ───────────────────────────────────────
            // 변경사항을 디스크에 영구 반영하기 위해 Dirty 표시 후 저장합니다.
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            // 완료 후 EffectManager 오브젝트를 Hierarchy에서 선택해 사용자가 바로 확인할 수 있게 합니다.
            Selection.activeGameObject = effectManagerGo;

            Debug.Log("[SetupEffectManagerScene] 완료 — EffectManager 배치 및 GameBootstrapper 연결 완료.");
        }

        /// <summary>
        /// 프로젝트 내에서 Game.unity 씬의 에셋 경로를 동적으로 찾아 반환합니다.
        /// 찾지 못하면 빈 문자열을 반환합니다.
        /// </summary>
        private static string FindGameScenePath()
        {
            // "t:Scene Game" → Scene 타입이면서 이름에 "Game"이 포함된 에셋 GUID 검색
            string[] guids = AssetDatabase.FindAssets("t:Scene Game");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // 검색어 "Game"은 부분 일치이므로 "GameOver" 같은 다른 씬이 섞여 들어올 수 있습니다.
                // 파일명이 정확히 "Game.unity"인 것만 채택합니다.
                if (System.IO.Path.GetFileName(path) == "Game.unity")
                {
                    return path;
                }
            }

            return string.Empty;
        }
    }
}
