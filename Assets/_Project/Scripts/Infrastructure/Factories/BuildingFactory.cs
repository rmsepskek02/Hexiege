// ============================================================================
// BuildingFactory.cs
// BuildingData(Domain 데이터)를 받아 Unity 프리팹 인스턴스(GameObject)를 생성하는 팩토리.
//
// UnitFactory와 동일한 패턴:
//   1. GameEvents.OnBuildingPlaced 이벤트 수신
//   2. BuildingData.Type에 해당하는 프리팹 선택
//   3. HexMetrics.HexToWorld()로 월드 위치 계산 → ViewConverter.ToView() → Y 오프셋
//   4. Instantiate → BuildingView 컴포넌트 초기화
//   5. 생성된 GameObject를 Buildings 부모 오브젝트 하위에 배치
//
// [Phase 2] 3D 전환:
//   - SpriteRenderer + sortingOrder 코드 완전 제거
//   - 3D MeshRenderer 기반 프리팹 사용 (프리팹 교체는 에디터 작업)
//   - 렌더 순서는 3D 깊이(Z-buffer)로 자동 처리
//
// Inspector 설정:
//   - 종족별 프리팹 세트 6개: Human/Spirit/Transcendence × Blue/Red
//   - 각 세트에 Castle, Barracks, MiningPost 3개 프리팹
//   - MiningPost: 종족별 프리팹 (Human은 Blue/Red 동일, Spirit/Transcendence는 팀별 구분)
//   - BuildingParent: [World]/Buildings Transform
//
// Infrastructure 레이어 — Unity 의존 (GameObject, Instantiate).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    public class BuildingFactory : MonoBehaviour
    {
        // ====================================================================
        // Inspector에서 설정할 필드
        // ====================================================================

        /// <summary>
        /// 건물 타입별 Blue/Red 프리팹 쌍.
        /// Inspector에서 type 필드로 건물 종류를 식별하고, 각 팀 프리팹을 연결합니다.
        /// </summary>
        [System.Serializable]
        public struct BuildingPrefabEntry
        {
            public BuildingType type;
            public GameObject blue;
            public GameObject red;
        }

        // 종족별 건물 프리팹 리스트.
        // 각 리스트에 해당 종족의 건물(본진, 배럭, 채굴소, 방어탑 등) × Blue/Red 프리팹을 연결합니다.

        [Header("인간계 (Human)")]
        [SerializeField] private List<BuildingPrefabEntry> _humanPrefabs;

        [Header("정령계 (Spirit)")]
        [SerializeField] private List<BuildingPrefabEntry> _spiritPrefabs;

        [Header("초월계 (Transcendence)")]
        [SerializeField] private List<BuildingPrefabEntry> _transcendencePrefabs;

        [Header("Hierarchy")]
        [Tooltip("건물 부모 Transform ([World]/Buildings)")]
        [SerializeField] private Transform _buildingParent;

        // 생성된 건물 GameObject를 BuildingData.Id로 관리.
        private readonly Dictionary<int, GameObject> _buildingObjects = new Dictionary<int, GameObject>();

        /// <summary> 건물 Y 오프셋. GameBootstrapper에서 설정. </summary>
        private float _buildingYOffset;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// Awake에서 이벤트 구독.
        /// GameBootstrapper.Start()에서 PlaceCastles()가 호출되므로,
        /// BuildingFactory가 반드시 먼저 구독을 완료해야 이벤트를 수신할 수 있음.
        /// </summary>
        private void Awake()
        {
            GameEvents.OnBuildingPlaced
                .Subscribe(e => CreateBuildingObject(e.Building))
                .AddTo(this);
        }

        /// <summary>
        /// 건물 Y 오프셋 설정. GameBootstrapper에서 호출.
        /// </summary>
        public void SetBuildingYOffset(float offset)
        {
            _buildingYOffset = offset;
        }

        // ====================================================================
        // 건물 GameObject 생성
        // ====================================================================

        /// <summary>
        /// BuildingData를 기반으로 건물 프리팹 인스턴스를 생성하고 초기화.
        /// </summary>
        private void CreateBuildingObject(BuildingData data)
        {
            // ── 종족 + 팀 조합으로 프리팹 조회 ──────────────────────
            // GameRaceContext는 게임 시작 시(GameBootstrapper) Blue/Red 각 팀의 종족을 저장한다.
            RaceId race = data.Team == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;

            // 종족 + 건물 타입 + 팀 조합으로 프리팹 조회.
            GameObject prefab = GetPrefab(race, data.Type, data.Team);

            if (prefab == null)
            {
                Debug.LogError($"[BuildingFactory] {race}/{data.Team}/{data.Type}에 해당하는 프리팹이 설정되지 않았습니다.");
                return;
            }

            // 건물의 헥스 좌표 → 도메인 월드 좌표 변환
            Vector3 worldPos = HexMetrics.HexToWorld(data.Position);

            // 도메인 좌표 → 뷰 좌표 변환 (Red팀이면 맵 중심 기준 반전)
            // Y 오프셋은 반전 이후에 적용해야 Blue/Red 양쪽에서 방향이 동일함
            Vector3 viewPos = ViewConverter.ToView(worldPos);
            viewPos.y += _buildingYOffset;

            // 프리팹 인스턴스 생성. 뷰 좌표에 배치.
            // 건물은 NetworkBuildingController의 SpawnBuildingClientRpc를 통해 모든 클라이언트에서
            // 각각 로컬 인스턴스로 생성되어 동기화됩니다 (NetworkObject.Spawn 방식 아님).
            GameObject obj = Instantiate(prefab, viewPos, Quaternion.identity, _buildingParent);
            
            // 오브젝트 이름을 실제 프리팹 이름 + Id로 설정 (에디터 디버깅용)
            obj.name = $"{prefab.name}_{data.Id}";

            // BuildingView 컴포넌트 초기화
            var view = obj.GetComponent<Presentation.BuildingView>();
            if (view != null)
            {
                view.Initialize(data);
            }

            _buildingObjects[data.Id] = obj;
        }

        /// <summary>
        /// 종족 + 건물 타입 + 팀 조합으로 해당 프리팹을 조회.
        /// </summary>
        private GameObject GetPrefab(RaceId race, BuildingType type, TeamId team)
        {
            List<BuildingPrefabEntry> list = race switch
            {
                RaceId.Human         => _humanPrefabs,
                RaceId.Spirit        => _spiritPrefabs,
                RaceId.Transcendence => _transcendencePrefabs,
                _                    => null
            };

            if (list == null) return null;

            foreach (var entry in list)
            {
                if (entry.type == type)
                    return team == TeamId.Blue ? entry.blue : entry.red;
            }

            return null;
        }

        /// <summary>
        /// Id로 건물 GameObject 조회. 외부에서 접근 필요 시 사용.
        /// </summary>
        public GameObject GetBuildingObject(int buildingId)
        {
            _buildingObjects.TryGetValue(buildingId, out GameObject obj);
            return obj;
        }

        /// <summary>
        /// 모든 건물 GameObject를 파괴. 맵 전환 시 호출.
        /// </summary>
        public void DestroyAllBuildings()
        {
            foreach (var kvp in _buildingObjects)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            _buildingObjects.Clear();
        }
    }
}
