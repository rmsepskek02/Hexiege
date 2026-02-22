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
// Inspector 설정:
//   - 프리팹 3종: Castle, Barracks, MiningPost
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

        [Header("Prefabs")]
        [Tooltip("본기지 프리팹 (SpriteRenderer + BuildingView)")]
        [SerializeField] private GameObject _castlePrefab;

        [Tooltip("배럭 프리팹 (SpriteRenderer + BuildingView)")]
        [SerializeField] private GameObject _barracksPrefab;

        [Tooltip("채굴소 프리팹 (SpriteRenderer + BuildingView)")]
        [SerializeField] private GameObject _miningPostPrefab;

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
            GameObject prefab = data.Type switch
            {
                BuildingType.Castle => _castlePrefab,
                BuildingType.Barracks => _barracksPrefab,
                BuildingType.MiningPost => _miningPostPrefab,
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
            obj.name = $"Building_{data.Type}_{data.Team}_{data.Id}";

            // SortingOrder 동적 설정.
            // 건물이 아래쪽 타일(더 높은 sortingOrder)에 가려지지 않도록
            // 뷰 좌표 기반으로 타일과 동일한 방식으로 계산 후 +50 오프셋 적용.
            // HexGridRenderer의 타일 sortingOrder 계산 방식과 동일.
            //   FlatTop: ViewConverter.FlatTopSortingOrder(viewPos) + 50
            //   PointyTop: coord.R + 50
            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (HexMetrics.Orientation == Domain.HexOrientation.FlatTop)
                    sr.sortingOrder = ViewConverter.FlatTopSortingOrder(viewPos) + 50;
                else
                    sr.sortingOrder = data.Position.R + 50;
            }

            // BuildingView 컴포넌트 초기화
            var view = obj.GetComponent<Presentation.BuildingView>();
            if (view != null)
            {
                view.Initialize(data);
            }

            _buildingObjects[data.Id] = obj;
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
