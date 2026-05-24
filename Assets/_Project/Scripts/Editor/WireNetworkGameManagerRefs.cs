// ===================================================================================================
// WireNetworkGameManagerRefs.cs
// ---------------------------------------------------------------------------------------------------
// 목적:
//   리팩토링 후 GameEndUI / NetworkStatusUI 에 새로 추가된 SerializeField 필드인
//   `_networkGameManager` 를 씬에 존재하는 NetworkGameManager 인스턴스로 자동 연결한다.
//
// 사용법:
//   Unity 에디터 상단 메뉴에서 [Hexiege] -> [Setup] -> [Wire NetworkGameManager References] 클릭.
//   현재 열려 있는(=활성) 씬에서만 동작한다. 씬 저장은 사용자가 직접 수행한다.
//
// 동작 순서:
//   1) 씬에서 NetworkGameManager 인스턴스를 찾는다. 없으면 에러 로그 + 조기 종료.
//   2) 씬에서 GameEndUI 인스턴스를 찾아 `_networkGameManager` 필드를 SerializedProperty 로 주입.
//   3) 씬에서 NetworkStatusUI 인스턴스를 찾아 동일하게 주입.
//   4) 각 단계마다 Debug.Log / Debug.LogWarning 으로 결과를 출력.
//   5) 변경이 1건이라도 있으면 EditorSceneManager.MarkSceneDirty() 호출 (저장 표시).
//   6) 마지막에 EditorUtility.DisplayDialog 로 요약 팝업을 띄운다.
//
// 주의:
//   - 이 파일은 Editor 폴더 하위에 위치하므로 빌드에는 포함되지 않는다.
//     (그래도 명시성을 위해 #if UNITY_EDITOR 가드를 함께 적용)
//   - SerializedObject 경로로 값을 쓰면 Undo / Prefab Override / Dirty 처리가 올바르게 동작한다.
//     직접 reflection 으로 값을 넣는 방식보다 안전하므로 SerializedProperty 경로를 사용.
// ===================================================================================================

#if UNITY_EDITOR
using Hexiege.Infrastructure;
using Hexiege.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hexiege.Editor
{
    /// <summary>
    /// GameEndUI / NetworkStatusUI 의 `_networkGameManager` SerializeField 를
    /// 씬에 존재하는 NetworkGameManager 인스턴스로 자동 연결하는 에디터 유틸.
    /// </summary>
    public static class WireNetworkGameManagerRefs
    {
        // SerializedProperty 경로로 사용할 필드 이름.
        // GameEndUI / NetworkStatusUI 양쪽 모두 동일하게 `_networkGameManager` 로 선언되어 있다.
        private const string TargetFieldName = "_networkGameManager";

        // Unity 에디터 상단 메뉴 경로.
        private const string MenuPath = "Hexiege/Setup/Wire NetworkGameManager References";

        /// <summary>
        /// 메뉴 클릭 시 진입점. 현재 활성 씬에서 자동 연결을 수행한다.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void WireReferences()
        {
            // 1) 씬에서 NetworkGameManager 를 찾는다.
            //    Unity 6 부터는 FindObjectOfType 대신 FindFirstObjectByType 사용이 권장된다.
            //    NetworkGameManager 가 비활성 상태일 수도 있으므로 Inactive 포함 검색 옵션을 지정.
            NetworkGameManager ngm = Object.FindFirstObjectByType<NetworkGameManager>(FindObjectsInactive.Include);
            if (ngm == null)
            {
                // 씬에 NetworkGameManager 가 아예 없으면 더 이상 진행할 수 없다.
                // 에러 로그를 남기고 사용자에게 다이얼로그로 안내한 뒤 종료.
                Debug.LogError("[WireNetworkGameManagerRefs] 씬에서 NetworkGameManager 를 찾지 못했습니다. " +
                               "Game.unity 씬을 열고 NetworkGameManager 오브젝트가 배치되어 있는지 확인하세요.");
                EditorUtility.DisplayDialog(
                    "Wire NetworkGameManager References",
                    "씬에서 NetworkGameManager 를 찾지 못했습니다.\n\n" +
                    "Game.unity 씬을 열고 NetworkGameManager 오브젝트가 존재하는지 확인하세요.",
                    "OK");
                return;
            }

            Debug.Log($"[WireNetworkGameManagerRefs] NetworkGameManager 발견: {ngm.gameObject.name} (InstanceID={ngm.GetInstanceID()})");

            // 결과 집계용 변수. 다이얼로그 요약과 MarkSceneDirty 판단에 사용된다.
            int wiredCount = 0;
            int skippedCount = 0;

            // 2) GameEndUI 연결
            GameEndUI gameEndUI = Object.FindFirstObjectByType<GameEndUI>(FindObjectsInactive.Include);
            if (gameEndUI == null)
            {
                // 씬에 컴포넌트가 없으면 경고만 남기고 다음 단계로 진행.
                Debug.LogWarning("[WireNetworkGameManagerRefs] 씬에서 GameEndUI 를 찾지 못했습니다. 건너뜁니다.");
                skippedCount++;
            }
            else
            {
                // TryAssign 헬퍼로 SerializedProperty 를 통해 필드 값을 주입.
                // 이미 동일한 값이 들어있으면 false 반환 → 변경 없음으로 집계.
                bool changed = TryAssignNetworkGameManager(gameEndUI, ngm);
                if (changed)
                {
                    Debug.Log($"[WireNetworkGameManagerRefs] GameEndUI._networkGameManager 연결 완료 (대상 GO: {gameEndUI.gameObject.name})");
                    wiredCount++;
                }
                else
                {
                    Debug.Log($"[WireNetworkGameManagerRefs] GameEndUI._networkGameManager 는 이미 올바르게 연결되어 있습니다. 변경 없음.");
                    skippedCount++;
                }
            }

            // 3) NetworkStatusUI 연결 (GameEndUI 와 동일한 로직)
            NetworkStatusUI networkStatusUI = Object.FindFirstObjectByType<NetworkStatusUI>(FindObjectsInactive.Include);
            if (networkStatusUI == null)
            {
                Debug.LogWarning("[WireNetworkGameManagerRefs] 씬에서 NetworkStatusUI 를 찾지 못했습니다. 건너뜁니다.");
                skippedCount++;
            }
            else
            {
                bool changed = TryAssignNetworkGameManager(networkStatusUI, ngm);
                if (changed)
                {
                    Debug.Log($"[WireNetworkGameManagerRefs] NetworkStatusUI._networkGameManager 연결 완료 (대상 GO: {networkStatusUI.gameObject.name})");
                    wiredCount++;
                }
                else
                {
                    Debug.Log($"[WireNetworkGameManagerRefs] NetworkStatusUI._networkGameManager 는 이미 올바르게 연결되어 있습니다. 변경 없음.");
                    skippedCount++;
                }
            }

            // 5) 변경이 1건이라도 있으면 씬을 더티 표시.
            //    저장(Ctrl+S)은 사용자가 직접 수행한다 (요구사항).
            if (wiredCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("[WireNetworkGameManagerRefs] 씬에 변경 사항이 발생하여 Dirty 로 표시했습니다. Ctrl+S 로 저장하세요.");
            }

            // 6) 결과 요약 다이얼로그.
            string summary =
                $"NetworkGameManager: {ngm.gameObject.name}\n" +
                $"신규 연결: {wiredCount} 건\n" +
                $"변경 없음/스킵: {skippedCount} 건\n\n" +
                (wiredCount > 0
                    ? "씬이 Dirty 상태로 변경되었습니다. Ctrl+S 로 저장하세요."
                    : "추가로 변경된 항목이 없습니다.");

            EditorUtility.DisplayDialog("Wire NetworkGameManager References", summary, "OK");
        }

        /// <summary>
        /// 대상 컴포넌트의 `_networkGameManager` SerializeField 에 NetworkGameManager 인스턴스를 주입한다.
        /// SerializedObject / SerializedProperty 를 통해 변경하므로 Undo / Dirty 처리가 정상 동작한다.
        /// </summary>
        /// <param name="target">필드를 주입할 MonoBehaviour (GameEndUI 또는 NetworkStatusUI).</param>
        /// <param name="ngm">씬에서 찾은 NetworkGameManager 인스턴스.</param>
        /// <returns>실제로 값이 바뀌었으면 true, 이미 같은 값이면 false.</returns>
        private static bool TryAssignNetworkGameManager(MonoBehaviour target, NetworkGameManager ngm)
        {
            // SerializedObject 는 컴포넌트의 직렬화된 표현. 인스펙터가 사용하는 것과 동일한 통로다.
            SerializedObject so = new SerializedObject(target);

            // 우리가 주입할 필드를 찾는다. 필드명 변경 등으로 못 찾으면 에러 로그 후 false 반환.
            SerializedProperty prop = so.FindProperty(TargetFieldName);
            if (prop == null)
            {
                Debug.LogError($"[WireNetworkGameManagerRefs] {target.GetType().Name} 에서 '{TargetFieldName}' 필드를 찾지 못했습니다. " +
                               "필드명이 변경되었거나 SerializeField 가 누락되었는지 확인하세요.");
                return false;
            }

            // 이미 동일한 인스턴스가 연결되어 있으면 더 이상 손대지 않는다.
            // 동일성 비교는 ObjectReferenceValue 기준 (UnityEngine.Object 참조).
            if (prop.objectReferenceValue == ngm)
            {
                return false;
            }

            // 값 주입 → ApplyModifiedProperties 로 직렬화 데이터 반영.
            // ApplyModifiedProperties 는 내부적으로 Undo 기록도 같이 처리한다.
            prop.objectReferenceValue = ngm;
            so.ApplyModifiedProperties();

            return true;
        }
    }
}
#endif
