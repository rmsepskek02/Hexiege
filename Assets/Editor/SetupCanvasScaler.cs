// ============================================================================
// SetupCanvasScaler.cs
// ----------------------------------------------------------------------------
// 목적 (1회성 셋업 스크립트):
//   프로젝트의 모든 씬 Canvas에 대해 CanvasScaler 설정을 통일한다.
//   - uiScaleMode      : ScaleWithScreenSize
//   - referenceResolution : 1080×1920
//   - matchWidthOrHeight  : 0  (Width 기준 매칭)
//
//   현재 씬 상태 (2026-05-25 확인):
//     Lobby.unity Canvas 1 : 1080×1920 / match=0     → 변경 없음
//     Lobby.unity Canvas 2 : 1080×1920 / match=0.5   → match만 0으로 변경
//     Game.unity  Canvas   : 540×960  / match=0.5    → 둘 다 변경
//
// 사용 방법:
//   Unity 메뉴 [Hexiege] -> [Setup] -> [Canvas Scaler 통일 적용] 클릭.
//   - 두 씬(Game.unity, Lobby.unity)을 차례로 열고 변경 후 저장한다.
//   - 작업 전 다른 변경 사항이 있다면 먼저 저장하길 권장한다.
//   - 1회성 스크립트이므로 실행 후 사용자가 직접 삭제하면 된다.
//
// 동작 순서:
//   1) 현재 열린 씬을 기억해 둔다 (작업 후 복구용).
//   2) Game.unity 열기 → 모든 Canvas의 CanvasScaler 값 통일 → 저장.
//   3) Lobby.unity 열기 → 모든 Canvas의 CanvasScaler 값 통일 → 저장.
//   4) 처음 씬으로 복귀.
//   5) DisplayDialog로 결과 안내.
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hexiege.Editor
{
    /// <summary>
    /// 프로젝트 내 씬들의 CanvasScaler 값을 통일하는 1회성 에디터 유틸.
    /// </summary>
    public static class SetupCanvasScaler
    {
        // 메뉴 경로
        private const string MenuPath = "Hexiege/Setup/Canvas Scaler 통일 적용";

        // 통일할 목표 값들
        private static readonly Vector2 TargetReferenceResolution = new Vector2(1080f, 1920f);
        private const float TargetMatch = 0f; // 0 = Width 기준 매칭
        private const CanvasScaler.ScaleMode TargetMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // 변경 대상 씬 경로들
        private static readonly string[] TargetScenes = new[]
        {
            "Assets/_Project/Scenes/Game.unity",
            "Assets/_Project/Scenes/Lobby.unity",
        };

        /// <summary>
        /// 메뉴 클릭 시 진입점. 변경 대상 씬들을 순회하며 통일 작업을 수행한다.
        /// </summary>
        [MenuItem(MenuPath)]
        public static void RunSetup()
        {
            // 현재 열려 있는 씬에 저장 안 한 변경이 있으면 사용자에게 저장 여부 확인.
            // 사용자가 취소하면 작업 자체를 중단한다.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[SetupCanvasScaler] 사용자가 저장을 취소하여 작업을 중단합니다.");
                return;
            }

            // 작업 후 원래 씬으로 복구하기 위해 현재 씬 경로를 기억해 둔다.
            string previousScenePath = EditorSceneManager.GetActiveScene().path;

            // 결과 집계용. 다이얼로그로 사용자에게 요약 보여줄 때 사용.
            var resultLog = new List<string>();

            foreach (string scenePath in TargetScenes)
            {
                // 씬 파일이 실제로 존재하는지 검사. 없으면 스킵.
                if (!System.IO.File.Exists(scenePath))
                {
                    string msg = $"[SetupCanvasScaler] 씬 파일을 찾지 못했습니다: {scenePath}";
                    Debug.LogWarning(msg);
                    resultLog.Add($"[스킵] {scenePath} (파일 없음)");
                    continue;
                }

                // Single 모드로 씬을 연다 = 다른 모든 씬을 닫고 이 씬만 활성화.
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                // 씬 내 모든 CanvasScaler를 찾는다. (비활성 GameObject 포함)
                // Unity 6 권장 API : FindObjectsByType. SortMode는 None이 가장 빠름.
                CanvasScaler[] scalers = Object.FindObjectsByType<CanvasScaler>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

                int changedCount = 0;
                int alreadyOkCount = 0;

                foreach (CanvasScaler scaler in scalers)
                {
                    // SerializedObject를 사용하면 Undo / Dirty / Prefab Override 등이 올바르게 처리됨.
                    // 직접 프로퍼티에 값을 대입하는 것보다 안전하므로 SerializedObject 경로 사용.
                    SerializedObject so = new SerializedObject(scaler);

                    SerializedProperty modeProp = so.FindProperty("m_UiScaleMode");
                    SerializedProperty refResProp = so.FindProperty("m_ReferenceResolution");
                    SerializedProperty matchProp = so.FindProperty("m_MatchWidthOrHeight");

                    if (modeProp == null || refResProp == null || matchProp == null)
                    {
                        Debug.LogWarning($"[SetupCanvasScaler] CanvasScaler의 직렬화 프로퍼티를 찾지 못했습니다: {scaler.gameObject.name}");
                        continue;
                    }

                    bool needChange = false;

                    // ScaleMode가 다르면 변경.
                    if (modeProp.enumValueIndex != (int)TargetMode)
                    {
                        modeProp.enumValueIndex = (int)TargetMode;
                        needChange = true;
                    }

                    // referenceResolution 비교는 Vector2 단위 (x, y).
                    Vector2 currentRefRes = refResProp.vector2Value;
                    if (!Mathf.Approximately(currentRefRes.x, TargetReferenceResolution.x)
                        || !Mathf.Approximately(currentRefRes.y, TargetReferenceResolution.y))
                    {
                        refResProp.vector2Value = TargetReferenceResolution;
                        needChange = true;
                    }

                    // matchWidthOrHeight는 0~1 사이 float.
                    if (!Mathf.Approximately(matchProp.floatValue, TargetMatch))
                    {
                        matchProp.floatValue = TargetMatch;
                        needChange = true;
                    }

                    if (needChange)
                    {
                        so.ApplyModifiedProperties();
                        changedCount++;
                        Debug.Log($"[SetupCanvasScaler] {scenePath} → Canvas '{scaler.gameObject.name}' 변경 완료.");
                    }
                    else
                    {
                        alreadyOkCount++;
                    }
                }

                // 씬에 변경이 발생했으면 Dirty 표시 후 저장.
                if (changedCount > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    bool saved = EditorSceneManager.SaveScene(scene);
                    Debug.Log($"[SetupCanvasScaler] {scenePath} 저장 결과: {(saved ? "성공" : "실패")}");
                    resultLog.Add($"[{System.IO.Path.GetFileName(scenePath)}] 변경 {changedCount}건, 변경 없음 {alreadyOkCount}건 → 저장 {(saved ? "OK" : "실패")}");
                }
                else
                {
                    resultLog.Add($"[{System.IO.Path.GetFileName(scenePath)}] 변경 없음 ({alreadyOkCount}건 모두 정상)");
                }
            }

            // 마지막으로 처음 씬으로 복구.
            if (!string.IsNullOrEmpty(previousScenePath) && System.IO.File.Exists(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }

            // 결과 다이얼로그.
            string summary = string.Join("\n", resultLog);
            EditorUtility.DisplayDialog(
                "Canvas Scaler 통일 적용",
                "작업 완료.\n\n" + summary + "\n\n" +
                "1회성 스크립트이므로 직접 삭제하셔도 됩니다.",
                "OK");
        }
    }
}
#endif
