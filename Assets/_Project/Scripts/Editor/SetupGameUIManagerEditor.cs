// ============================================================================
// SetupGameUIManagerEditor.cs
// [1회성 에디터 스크립트] GameUIManager Inspector 연결 자동화.
//
// 실행 방법:
//   Unity 메뉴 → Hexiege → Setup GameUIManager
//
// 수행 작업:
//   1. [Managers] 하위에 "GameUIManager" GameObject 생성 + GameUIManager 컴포넌트 부착
//   2. GameUIManager._gameEndUI 에 씬의 GameEndUI 연결
//   3. GameBootstrapper._uiManager 에 GameUIManager 연결
//
// 실행 후 이 파일은 삭제해도 무방합니다.
//
// 주의: Game.unity 씬이 열려 있는 상태에서 실행하세요.
// ============================================================================

#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using Hexiege.Presentation;
using Hexiege.Bootstrap;

namespace Hexiege.Editor
{
    public static class SetupGameUIManagerEditor
    {
        private const string MenuPath = "Hexiege/Setup GameUIManager";

        [MenuItem(MenuPath)]
        public static void SetupGameUIManager()
        {
            // ----------------------------------------------------------------
            // 1단계: [Managers] GameObject 탐색
            // ----------------------------------------------------------------

            // 씬에서 [Managers] GameObject를 이름으로 찾는다.
            // GameBootstrapper의 부착 위치가 [Managers]/GameBootstrapper이므로 반드시 존재해야 함.
            GameObject managers = GameObject.Find("[Managers]");
            if (managers == null)
            {
                Debug.LogError("[SetupGameUIManager] '[Managers]' GameObject를 찾을 수 없습니다. Game.unity 씬이 열려 있는지 확인하세요.");
                return;
            }

            // ----------------------------------------------------------------
            // 2단계: GameUIManager GameObject 생성 또는 기존 것 재사용
            // ----------------------------------------------------------------

            // 이미 [Managers] 하위에 GameUIManager가 있는지 확인 (중복 생성 방지)
            Transform existingTransform = managers.transform.Find("GameUIManager");
            GameObject uiManagerGO;

            if (existingTransform != null)
            {
                // 이미 존재하면 재사용
                uiManagerGO = existingTransform.gameObject;
                Debug.Log("[SetupGameUIManager] 기존 'GameUIManager' GameObject를 재사용합니다.");
            }
            else
            {
                // 새로 생성: 빈 GameObject를 [Managers] 하위에 배치
                uiManagerGO = new GameObject("GameUIManager");
                uiManagerGO.transform.SetParent(managers.transform, worldPositionStays: false);
                Debug.Log("[SetupGameUIManager] 'GameUIManager' GameObject를 [Managers] 하위에 생성했습니다.");
            }

            // Undo 등록 — 에디터 작업 실행 취소 가능하도록
            Undo.RegisterCreatedObjectUndo(uiManagerGO, "Setup GameUIManager");

            // ----------------------------------------------------------------
            // 3단계: GameUIManager 컴포넌트 부착
            // ----------------------------------------------------------------

            // 이미 컴포넌트가 있으면 기존 것 사용, 없으면 추가
            GameUIManager uiManager = uiManagerGO.GetComponent<GameUIManager>();
            if (uiManager == null)
            {
                uiManager = Undo.AddComponent<GameUIManager>(uiManagerGO);
                Debug.Log("[SetupGameUIManager] GameUIManager 컴포넌트를 부착했습니다.");
            }

            // ----------------------------------------------------------------
            // 4단계: 씬에서 GameEndUI 탐색 → GameUIManager._gameEndUI 연결
            // ----------------------------------------------------------------

            // FindFirstObjectByType: 씬 전체에서 GameEndUI 컴포넌트를 탐색.
            // FindObjectsInactive.Include: 비활성 오브젝트도 포함 (패널이 숨겨져 있을 수 있으므로)
            GameEndUI gameEndUI = Object.FindFirstObjectByType<GameEndUI>(FindObjectsInactive.Include);
            if (gameEndUI == null)
            {
                Debug.LogError("[SetupGameUIManager] 씬에서 GameEndUI를 찾을 수 없습니다. Inspector에서 수동 연결이 필요합니다.");
            }
            else
            {
                // SerializedObject를 통해 private SerializeField에 접근
                SerializedObject soManager = new SerializedObject(uiManager);
                SerializedProperty propGameEndUI = soManager.FindProperty("_gameEndUI");

                if (propGameEndUI != null)
                {
                    propGameEndUI.objectReferenceValue = gameEndUI;
                    soManager.ApplyModifiedProperties();
                    Debug.Log($"[SetupGameUIManager] GameUIManager._gameEndUI → '{gameEndUI.gameObject.name}' 연결 완료.");
                }
                else
                {
                    Debug.LogError("[SetupGameUIManager] GameUIManager에서 '_gameEndUI' 필드를 찾을 수 없습니다.");
                }
            }

            // ----------------------------------------------------------------
            // 5단계: GameBootstrapper._uiManager 연결
            // ----------------------------------------------------------------

            GameBootstrapper bootstrapper = Object.FindFirstObjectByType<GameBootstrapper>(FindObjectsInactive.Include);
            if (bootstrapper == null)
            {
                Debug.LogError("[SetupGameUIManager] 씬에서 GameBootstrapper를 찾을 수 없습니다. Inspector에서 수동 연결이 필요합니다.");
            }
            else
            {
                SerializedObject soBootstrapper = new SerializedObject(bootstrapper);
                SerializedProperty propUIManager = soBootstrapper.FindProperty("_uiManager");

                if (propUIManager != null)
                {
                    propUIManager.objectReferenceValue = uiManager;
                    soBootstrapper.ApplyModifiedProperties();
                    Debug.Log($"[SetupGameUIManager] GameBootstrapper._uiManager → '{uiManagerGO.name}' 연결 완료.");
                }
                else
                {
                    Debug.LogError("[SetupGameUIManager] GameBootstrapper에서 '_uiManager' 필드를 찾을 수 없습니다.");
                }
            }

            // ----------------------------------------------------------------
            // 완료: 씬 저장 안내
            // ----------------------------------------------------------------

            // 씬을 dirty 상태로 표시 (저장 안내)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("[SetupGameUIManager] ✅ 설정 완료! Ctrl+S 로 씬을 저장하세요.");

            // 에디터에서 GameUIManager GameObject를 선택 상태로 전환 (결과 확인 용이)
            Selection.activeGameObject = uiManagerGO;
        }

        /// <summary>
        /// 메뉴 항목 활성화 조건: 에디터 플레이 모드가 아닐 때만 실행 가능.
        /// </summary>
        [MenuItem(MenuPath, validate = true)]
        public static bool ValidateSetupGameUIManager()
        {
            return !UnityEngine.Application.isPlaying;
        }
    }
}

#endif
