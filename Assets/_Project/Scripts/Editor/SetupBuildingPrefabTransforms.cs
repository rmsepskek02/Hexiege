// ============================================================================
// SetupBuildingPrefabTransforms.cs
// Human / Spirit / Transcendence 건물 프리팹의 메시 자식 오브젝트 Transform을
// 지정된 수치로 일괄 변경하는 1회성 에디터 스크립트.
//
// 메뉴: Hexiege/Setup/건물 프리팹 Transform 일괄 설정
//
// 구조 전제:
//   프리팹 루트 (Building_Castle_Blue 등)
//   └── 첫 번째 자식 = 메시 오브젝트  ← 이 Transform을 변경
//
// 수치 변경 방법:
//   아래 "수치 조정 구역"의 값을 원하는 숫자로 바꾼 뒤 메뉴를 실행하세요.
//
// 1회성 — 실행 후 삭제해도 무방.
// ============================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class SetupBuildingPrefabTransforms
{
    // ════════════════════════════════════════════════════════════════════════
    //  수치 조정 구역 — 여기서만 수정하세요
    // ════════════════════════════════════════════════════════════════════════

    // 메시 오브젝트의 로컬 Position (프리팹 루트 기준)
    static readonly Vector3 MeshPosition = new Vector3(0f, 0.23f, 0f);

    // 메시 오브젝트의 로컬 Rotation (오일러각)
    static readonly Vector3 MeshRotation = new Vector3(-90f, 135f, 0f);

    // 메시 오브젝트의 로컬 Scale
    static readonly Vector3 MeshScale = new Vector3(30f, 30f, 30f);

    // ════════════════════════════════════════════════════════════════════════

    // 적용 대상 폴더 목록
    private static readonly string[] TARGET_FOLDERS =
    {
        "Assets/_Project/Prefabs/Buildings/Human",
        "Assets/_Project/Prefabs/Buildings/Spirit",
        "Assets/_Project/Prefabs/Buildings/Transcendence",
    };

    [MenuItem("Hexiege/Setup/건물 프리팹 Transform 일괄 설정")]
    private static void Setup()
    {
        int successCount = 0;
        int skipCount    = 0;

        // 대상 폴더에서 모든 프리팹 GUID 수집
        string[] guids = AssetDatabase.FindAssets("t:Prefab", TARGET_FOLDERS);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // EditPrefabContentsScope: 프리팹을 메모리에 로드 → 수정 → 자동 저장
            // using 블록을 벗어나는 순간 변경 사항이 디스크에 저장됨
            using var scope = new PrefabUtility.EditPrefabContentsScope(path);
            Transform root = scope.prefabContentsRoot.transform;

            // 자식이 없는 프리팹은 건너뜀
            if (root.childCount == 0)
            {
                Debug.LogWarning($"[Setup] 자식 없음, 건너뜀: {path}");
                skipCount++;
                continue;
            }

            // 첫 번째 자식 = 메시 오브젝트
            Transform mesh = root.GetChild(0);
            mesh.localPosition    = MeshPosition;
            mesh.localEulerAngles = MeshRotation;
            mesh.localScale       = MeshScale;

            successCount++;
            Debug.Log($"[Setup] {root.name}  /  {mesh.name}  →  " +
                      $"Pos{MeshPosition}  Rot{MeshRotation}  Scale{MeshScale}");
        }

        Debug.Log($"[Setup] 완료 — 적용: {successCount}개 / 건너뜀: {skipCount}개 " +
                  $"(총 검색: {guids.Length}개)");
    }
}
#endif
