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
        /// 팀별 건물 프리팹 세트.
        /// 종족마다 Blue/Red 각각 1세트씩, 총 6세트를 Inspector에서 설정.
        /// castle = 본진(Castle/SpiritNexus/ElderTree), barracks = 병영(Barracks/SummoningAltar/HunterPlant).
        /// </summary>
        [System.Serializable]
        public struct BuildingTeamPrefabSet
        {
            public GameObject castle;
            public GameObject barracks;
            public GameObject miningPost;   // 종족별 채굴소 프리팹 (Human은 Blue/Red 동일 프리팹 사용)
        }

        // ── 종족별 프리팹 세트 (각 종족 × Blue/Red = 2세트) ──────────────
        // GameRaceContext에서 팀의 종족을 조회한 뒤, 아래 6세트 중 하나를 선택한다.

        [Header("Prefabs - Human (인간)")]
        [SerializeField] private BuildingTeamPrefabSet _humanBluePrefabs;
        [SerializeField] private BuildingTeamPrefabSet _humanRedPrefabs;

        [Header("Prefabs - Spirit (정령)")]
        [SerializeField] private BuildingTeamPrefabSet _spiritBluePrefabs;
        [SerializeField] private BuildingTeamPrefabSet _spiritRedPrefabs;

        [Header("Prefabs - Transcendence (초월)")]
        [SerializeField] private BuildingTeamPrefabSet _transcendenceBluePrefabs;
        [SerializeField] private BuildingTeamPrefabSet _transcendenceRedPrefabs;

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
            // ── 종족 + 팀 조합으로 프리팹 세트 선택 ──────────────────────
            // GameRaceContext는 게임 시작 시(GameBootstrapper) Blue/Red 각 팀의 종족을 저장한다.
            // MiningPost도 종족별 프리팹을 사용하므로 동일한 set에서 선택.
            RaceId race = data.Team == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;

            // 종족(3) × 팀(2) = 6가지 조합 → 해당하는 프리팹 세트 반환
            BuildingTeamPrefabSet set = (race, data.Team) switch
            {
                (RaceId.Human,         TeamId.Blue) => _humanBluePrefabs,
                (RaceId.Human,         TeamId.Red)  => _humanRedPrefabs,
                (RaceId.Spirit,        TeamId.Blue) => _spiritBluePrefabs,
                (RaceId.Spirit,        TeamId.Red)  => _spiritRedPrefabs,
                (RaceId.Transcendence, TeamId.Blue) => _transcendenceBluePrefabs,
                (RaceId.Transcendence, TeamId.Red)  => _transcendenceRedPrefabs,
                // 안전망: 매핑되지 않는 조합이 들어오면 Human Blue로 폴백
                _                                   => _humanBluePrefabs
            };

            // 건물타입에 맞는 프리팹 선택 (MiningPost도 종족별 프리팹 세트에서 선택)
            GameObject prefab = data.Type switch
            {
                BuildingType.Castle     => set.castle,
                BuildingType.Barracks   => set.barracks,
                BuildingType.MiningPost => set.miningPost,
                _ => null
            };

            if (prefab == null)
            {
                Debug.LogError($"[BuildingFactory] {data.Type}에 해당하는 프리팹이 설정되지 않았습니다.");
                return;
            }

            // 건물의 헥스 좌표 → 도메인 월드 좌표 변환
            Vector3 worldPos = HexMetrics.HexToWorld(data.Position);

            // 도메인 좌표 → 뷰 좌표 변환 (Red팀이면 맵 중심 기준 반전)
            // Y 오프셋은 반전 이후에 적용해야 Blue/Red 양쪽에서 방향이 동일함
            Vector3 viewPos = ViewConverter.ToView(worldPos);
            viewPos.y += _buildingYOffset;

            // 프리팹 인스턴스 생성. 뷰 좌표에 배치.
            GameObject obj = Instantiate(prefab, viewPos, Quaternion.identity, _buildingParent);
            // 오브젝트 이름을 실제 프리팹 이름 + Id로 설정 (에디터 디버깅용)
            // 예: "Building_SpiritNexus_Blue_1" — 프리팹 이름이 그대로 반영되어 종족 구분이 명확함
            obj.name = $"{prefab.name}_{data.Id}";

            // [Phase 2] 3D 전환: sortingOrder 제거 — 렌더 순서는 Z-buffer로 자동 처리

            // BuildingView 컴포넌트 초기화
            var view = obj.GetComponent<Presentation.BuildingView>();
            if (view != null)
            {
                view.Initialize(data);
            }

            _buildingObjects[data.Id] = obj;
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
