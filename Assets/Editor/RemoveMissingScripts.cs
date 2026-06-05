using UnityEditor;
using UnityEngine;

namespace Hexiege.Editor
{
    /// <summary>
    /// 프리팹에 남아 있는 Missing Script(삭제된 BuildingView 등) 참조를 제거하는 1회성 Editor 도구.
    /// 과거에 삭제된 Hexiege.Presentation.BuildingView 스크립트의 참조(GUID: c178b6f3e086351409b946635cbfae71)가
    /// 일부 건물 프리팹에 Missing 상태로 남아 있어 이를 일괄 정리한다.
    /// BuildingView는 의도적으로 제거된 스크립트이므로 복원하지 않고 참조만 삭제한다.
    /// </summary>
    public static class RemoveMissingScripts
    {
        // 처리 대상 프리팹 경로 목록(8개).
        // 주의: _Old 폴더 프리팹은 절대 포함하지 않는다. 목록에 명시된 프리팹만 처리한다.
        private static readonly string[] TargetPrefabPaths =
        {
            "Assets/_Project/Prefabs/Buildings/Spirit/Building_ManaRift_Blue.prefab",
            "Assets/_Project/Prefabs/Buildings/Spirit/Building_ManaRift_Red.prefab",
            "Assets/_Project/Prefabs/Buildings/Spirit/Building_SpiritNexus_Blue.prefab",
            "Assets/_Project/Prefabs/Buildings/Spirit/Building_SpiritNexus_Red.prefab",
            "Assets/_Project/Prefabs/Buildings/Transcendence/Building_ElderTree_Blue.prefab",
            "Assets/_Project/Prefabs/Buildings/Transcendence/Building_ElderTree_Red.prefab",
            "Assets/_Project/Prefabs/Buildings/Transcendence/Building_FungalNode_Blue.prefab",
            "Assets/_Project/Prefabs/Buildings/Transcendence/Building_FungalNode_Red.prefab",
        };

        /// <summary>
        /// 메뉴에서 호출되는 진입점. 대상 프리팹들을 순회하며 Missing Script 참조를 제거한다.
        /// </summary>
        [MenuItem("Hexiege/Setup/Missing Script 제거")]
        public static void RemoveMissingScriptsFromBuildings()
        {
            // 전체 프리팹에서 제거된 컴포넌트의 총합을 누적한다.
            int totalRemoved = 0;

            // 정상 처리된 프리팹 개수(로그/요약용).
            int processedPrefabCount = 0;

            foreach (string path in TargetPrefabPaths)
            {
                // 경로에 실제 프리팹 에셋이 존재하는지 먼저 확인한다.
                // (오타나 파일 이동으로 경로가 깨졌을 경우 안전하게 건너뛰기 위함)
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabAsset == null)
                {
                    Debug.LogWarning($"[RemoveMissingScripts] 프리팹을 찾을 수 없어 건너뜀: {path}");
                    continue;
                }

                // LoadPrefabContents: 프리팹을 편집 가능한 임시 GameObject 인스턴스로 연다.
                // 이 인스턴스를 수정한 뒤 SaveAsPrefabAsset으로 원본 에셋에 다시 저장한다.
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);

                try
                {
                    // 이 프리팹 한 개에서 제거된 컴포넌트 수.
                    int removedInThisPrefab = 0;

                    // 루트 + 모든 자식(비활성 포함, true)을 재귀적으로 순회하며 Missing Script를 제거한다.
                    // GetComponentsInChildren는 자기 자신(루트)도 포함하므로 루트를 따로 처리하지 않아도 된다.
                    foreach (Transform child in prefabRoot.GetComponentsInChildren<Transform>(true))
                    {
                        // RemoveMonoBehavioursWithMissingScript: 해당 GameObject에서 스크립트가 Missing 상태인
                        // MonoBehaviour 컴포넌트들을 제거하고 제거된 개수를 반환한다.
                        removedInThisPrefab +=
                            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                    }

                    // 변경 사항이 있을 때만 저장한다(불필요한 에셋 더티 방지).
                    if (removedInThisPrefab > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    }

                    totalRemoved += removedInThisPrefab;
                    processedPrefabCount++;

                    Debug.Log(
                        $"[RemoveMissingScripts] {System.IO.Path.GetFileName(path)} → 제거된 컴포넌트: {removedInThisPrefab}개");
                }
                finally
                {
                    // UnloadPrefabContents: LoadPrefabContents로 연 임시 인스턴스를 반드시 메모리에서 해제한다.
                    // 예외가 발생하더라도 해제되도록 finally에서 호출한다.
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            // 에셋 데이터베이스를 갱신해 변경 사항을 에디터에 반영한다.
            AssetDatabase.Refresh();

            // 최종 결과를 콘솔에 요약 출력한다.
            string summary =
                $"[RemoveMissingScripts] 완료 — 처리 프리팹 {processedPrefabCount}/{TargetPrefabPaths.Length}개, " +
                $"총 제거된 Missing Script: {totalRemoved}개";
            Debug.Log(summary);

            // 작업 완료를 사용자에게 다이얼로그로 알린다.
            EditorUtility.DisplayDialog(
                "Missing Script 제거 완료",
                $"처리 프리팹: {processedPrefabCount}/{TargetPrefabPaths.Length}개\n" +
                $"총 제거된 Missing Script: {totalRemoved}개\n\n" +
                "자세한 내역은 Console 로그를 확인하세요.",
                "확인");
        }
    }
}
