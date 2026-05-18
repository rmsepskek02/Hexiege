// ============================================================================
// BuildingFactory.cs
// BuildingData(Domain 데이터)를 받아 Unity 프리팹 인스턴스(GameObject)를 생성하는 팩토리.
//
// UnitFactory와 동일한 패턴:
//   1. GameEvents.OnBuildingPlaced 이벤트 수신
//   2. BuildingData.Type에 해당하는 프리팹 선택
//   3. HexMetrics.HexToWorld()로 월드 위치 계산 → ViewConverter.ToView() → Y 오프셋
//   4. Instantiate → _buildingObjects 딕셔너리(Id → GameObject)에 저장
//   5. 생성된 GameObject를 Buildings 부모 오브젝트 하위에 배치
//
// 건물 파괴(사망/철거):
//   - GameEvents.OnEntityDied 이벤트를 구독해 BuildingData인 경우 딕셔너리에서 GO를 찾아 Destroy.
//   - 별도의 BuildingView 컴포넌트 없이 BuildingFactory가 단일 책임으로 GO 생명주기를 관리한다.
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

            // 업그레이드 이벤트 구독 — 새 프리팹을 먼저 만든 후 기존 GO를 제거한다.
            GameEvents.OnBuildingUpgraded
                .Subscribe(e => UpgradeBuildingObject(e.OldBuildingId, e.NewBuilding))
                .AddTo(this);

            // ── 건물 사망/철거 이벤트 구독 ──────────────────────
            // OnEntityDied 이벤트는 유닛/건물 공용이다.
            // (UnitCombatUseCase 등에서 유닛이 죽을 때도 같은 이벤트가 발행됨)
            // 따라서 이벤트로 전달된 Entity가 BuildingData인지 먼저 확인해
            // "건물 사망"인 경우에만 GO를 파괴한다.
            //
            // 동작 흐름:
            //   1) BuildingPlacementUseCase.DemolishBuilding() 또는 전투 시스템에서 OnEntityDied 발행
            //   2) 여기서 _buildingObjects 딕셔너리(Id → GameObject)를 O(1)로 조회
            //   3) 해당 GO를 Destroy 후 딕셔너리에서 제거
            //
            // 과거에는 BuildingView 컴포넌트가 자기 자신의 사망을 구독하고
            // Destroy(gameObject)를 호출했지만, 프리팹에 BuildingView가 누락되어
            // 철거 시 GO가 사라지지 않는 버그가 있었다. → BuildingFactory가 단일 책임으로 처리.
            GameEvents.OnEntityDied
                .Subscribe(e =>
                {
                    // 유닛 사망 이벤트는 무시 (BuildingData가 아니면 즉시 리턴)
                    if (e.Entity is not BuildingData building) return;

                    // 딕셔너리에서 해당 건물 GO를 꺼내 파괴한다.
                    // Unity 객체 비교 시 == null 패턴 사용 (Destroyed 객체 대응).
                    if (_buildingObjects.TryGetValue(building.Id, out var go) && go != null)
                    {
                        _buildingObjects.Remove(building.Id);
                        Destroy(go);
                    }
                })
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

            // 생성된 GO를 딕셔너리에 등록 (Id → GameObject).
            // 이후 사망/철거 시 OnEntityDied 구독자(Awake 참조)가 이 딕셔너리에서 GO를 찾아 파괴한다.
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
        /// 건물 업그레이드 처리.
        /// 새 프리팹을 동일 위치/회전에 먼저 생성 → 기존 GO를 Destroy → 딕셔너리 갱신.
        /// 빈 타일이 보이지 않도록 반드시 "새 GO 생성 후 기존 GO 제거" 순서를 지킨다.
        /// </summary>
        /// <param name="oldBuildingId">업그레이드 전 BuildingData Id.</param>
        /// <param name="newBuilding">업그레이드 후 새로 생성된 BuildingData.</param>
        private void UpgradeBuildingObject(int oldBuildingId, BuildingData newBuilding)
        {
            // 기존 GO 위치/회전 백업. 신규 GO 생성 시 그대로 적용.
            // (BuildingData.Position에서 다시 계산해도 되지만, ViewConverter/Y 오프셋 등을 한 번 더
            //  수행하면 부동소수 오차가 누적될 수 있어 기존 GO의 transform을 그대로 재사용한다.)
            if (!_buildingObjects.TryGetValue(oldBuildingId, out var oldObj) || oldObj == null)
            {
                // 기존 GO가 없으면 일반 배치 경로로 폴백 (혹시 모를 누락 대비).
                CreateBuildingObject(newBuilding);
                return;
            }

            Vector3 spawnPos = oldObj.transform.position;
            Quaternion spawnRot = oldObj.transform.rotation;

            // 종족 + 새 BuildingType + 팀으로 프리팹 조회
            RaceId race = newBuilding.Team == TeamId.Blue
                ? GameRaceContext.BlueRace
                : GameRaceContext.RedRace;

            GameObject prefab = GetPrefab(race, newBuilding.Type, newBuilding.Team);
            if (prefab == null)
            {
                Debug.LogError($"[BuildingFactory] 업그레이드 프리팹 누락: {race}/{newBuilding.Team}/{newBuilding.Type}");
                return;
            }

            // 1) 새 GO 먼저 생성 — 빈 타일 방지를 위해 반드시 기존 GO 제거 전에 수행
            GameObject newObj = Instantiate(prefab, spawnPos, spawnRot, _buildingParent);
            newObj.name = $"{prefab.name}_{newBuilding.Id}";

            // 2) 기존 GO 제거
            Destroy(oldObj);
            _buildingObjects.Remove(oldBuildingId);

            // 3) 새 GO 등록 (서버/클라이언트 모두 newBuilding.Id 키로 저장)
            //    이후 사망/철거 시 OnEntityDied 구독자(Awake 참조)가 newBuilding.Id로 GO를 찾아 파괴한다.
            _buildingObjects[newBuilding.Id] = newObj;
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
