// ============================================================================
// AddNetworkTransformToPrefabs.cs
// 유닛 프리팹 6개에 NetworkObject + NetworkTransform + NetworkUnit 컴포넌트를
// 일괄 추가하는 1회성 에디터 스크립트.
//
// 메뉴 경로: Hexiege > Add NetworkTransform To Unit Prefabs
//
// 추가되는 컴포넌트:
//   1. NetworkObject — NGO 네트워크 오브젝트 등록 (Spawn/Despawn 관리)
//   2. NetworkTransform — 서버 position → 클라이언트 자동 보간 동기화
//   3. NetworkUnit — unitId를 NetworkVariable로 동기화하여 클라이언트 초기화에 사용
//
// NetworkTransform 설정:
//   - SyncPositionX/Y/Z: true (3축 위치 동기화)
//   - SyncRotAngleX/Y/Z: false (회전 동기화 비활성화)
//     → NGO 보간 버퍼로 인해 ~1초 회전 딜레이 발생 문제 해결
//     → 회전은 클라이언트 로컬에서 UnitView.ApplyDirection()으로 처리
//   - InLocalSpace: false (월드 좌표 기준 동기화)
//
// 주의:
//   - 이미 해당 컴포넌트가 부착된 프리팹은 건너뜀 (중복 방지)
//   - 실행 후 반드시 Unity 에디터에서 프리팹 변경 확인
//   - 실행 후 NetworkManager의 Network Prefabs List에 유닛 프리팹 6개를 수동 등록해야 함
//
// Editor 전용 — 빌드에 포함되지 않음.
// ============================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

namespace Hexiege.Editor
{
    /// <summary>
    /// 유닛 프리팹에 NetworkObject + NetworkTransform + NetworkUnit 컴포넌트를
    /// 일괄 추가하는 에디터 유틸리티.
    /// </summary>
    public static class AddNetworkTransformToPrefabs
    {
        // 유닛 프리팹 경로 목록 (Assets/_Project/Prefabs/Units/ 하위)
        private static readonly string[] PrefabPaths = new[]
        {
            "Assets/_Project/Prefabs/Units/Unit_Pistoleer_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Unit_Pistoleer_Red.prefab",
            "Assets/_Project/Prefabs/Units/Unit_Assault_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Unit_Assault_Red.prefab",
            "Assets/_Project/Prefabs/Units/Unit_Sniper_Blue.prefab",
            "Assets/_Project/Prefabs/Units/Unit_Sniper_Red.prefab",
        };

        /// <summary>
        /// 메뉴에서 실행: 유닛 프리팹 6개에 NetworkObject + NetworkTransform + NetworkUnit 추가.
        /// 이미 부착된 컴포넌트는 건너뛰고, 추가된 컴포넌트만 저장.
        /// </summary>
        [MenuItem("Hexiege/Add NetworkTransform To Unit Prefabs")]
        public static void Execute()
        {
            int modifiedCount = 0;

            foreach (string path in PrefabPaths)
            {
                // 프리팹 에셋을 로드하여 존재 여부 먼저 확인
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabAsset == null)
                {
                    Debug.LogWarning($"[Editor] 프리팹을 찾을 수 없습니다: {path}");
                    continue;
                }

                // 프리팹 편집 모드로 열기
                // PrefabUtility.LoadPrefabContents()로 격리 편집 → SaveAsPrefabAsset()으로 저장
                // ※ 기존 skip 로직 제거: 이미 컴포넌트가 있어도 NetworkTransform 회전 설정 업데이트가 필요하므로
                //    항상 열어서 설정을 확인/갱신
                GameObject prefabInstance = PrefabUtility.LoadPrefabContents(path);

                // 1) NetworkObject — NGO가 관리하는 네트워크 오브젝트로 등록
                //    NetworkTransform, NetworkUnit보다 먼저 추가해야 함 (의존 관계)
                if (prefabInstance.GetComponent<NetworkObject>() == null)
                {
                    prefabInstance.AddComponent<NetworkObject>();
                    Debug.Log($"[Editor] NetworkObject 추가: {path}");
                }

                // 2) NetworkTransform — 서버 position을 클라이언트에 자동 보간 동기화
                //    기본값: SyncPositionX/Y/Z=true, InLocalSpace=false
                //    회전 동기화 비활성화: NGO 보간 버퍼로 인해 ~1초 회전 딜레이 발생하므로
                //    회전은 클라이언트 로컬에서 UnitView가 직접 처리
                NetworkTransform existingNt = prefabInstance.GetComponent<NetworkTransform>();
                if (existingNt == null)
                {
                    existingNt = prefabInstance.AddComponent<NetworkTransform>();
                    Debug.Log($"[Editor] NetworkTransform 추가: {path}");
                }
                // 회전 동기화 비활성화 — 회전은 클라이언트 로컬에서 UnitView.ApplyDirection()으로 처리.
                // NGO NetworkTransform의 보간 버퍼(InterpolationBufferTickOffset)가
                // 회전에도 적용되어 적 감지 후 방향 전환에 ~1초 딜레이가 발생하는 문제 해결.
                existingNt.SyncRotAngleX = false;
                existingNt.SyncRotAngleY = false;
                existingNt.SyncRotAngleZ = false;

                // 3) NetworkUnit — unitId를 NetworkVariable로 동기화
                //    서버 스폰 시 SetUnitId() 호출, 클라이언트 OnNetworkSpawn()에서 UnitView 초기화
                if (prefabInstance.GetComponent<Hexiege.Infrastructure.NetworkUnit>() == null)
                {
                    prefabInstance.AddComponent<Hexiege.Infrastructure.NetworkUnit>();
                    Debug.Log($"[Editor] NetworkUnit 추가: {path}");
                }

                // 프리팹 저장
                PrefabUtility.SaveAsPrefabAsset(prefabInstance, path);
                PrefabUtility.UnloadPrefabContents(prefabInstance);

                modifiedCount++;
            }

            // 에셋 데이터베이스 갱신
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Editor] 네트워크 컴포넌트 추가/갱신 완료. 처리={modifiedCount}");
            EditorUtility.DisplayDialog(
                "네트워크 컴포넌트 추가/갱신 완료",
                $"처리: {modifiedCount}개\n\n" +
                "NetworkTransform 회전 동기화: 전부 OFF (클라이언트 로컬 처리)\n\n" +
                "중요: NetworkManager의 Network Prefabs List에\n유닛 프리팹 6개를 수동 등록해주세요.",
                "확인");
        }
    }
}
#endif
