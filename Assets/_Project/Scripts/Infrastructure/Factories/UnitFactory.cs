// ============================================================================
// UnitFactory.cs
// UnitData(Domain 데이터)를 받아 Unity 프리팹 인스턴스(GameObject)를 생성하는 팩토리.
//
// 팩토리 패턴이란?
//   객체 생성 로직을 한 곳에 캡슐화하는 패턴.
//   "유닛을 만들어줘" → 팩토리가 프리팹 선택, Instantiate, 컴포넌트 초기화를 모두 처리.
//   호출자는 생성 과정의 세부사항을 알 필요 없음.
//
// 흐름:
//   1. GameEvents.OnUnitSpawned 이벤트 수신 (Application 레이어에서 발행)
//   2. UnitData.Type에 해당하는 프리팹 선택
//   3. HexMetrics.HexToWorld()로 월드 위치 계산
//   4. Instantiate → UnitView 컴포넌트 초기화
//   5. 생성된 GameObject를 Units 부모 오브젝트 하위에 배치
//
// [Phase 2] 3D 전환:
//   - UnitAnimationData(Sprite[] 기반) 의존성 완전 제거
//   - Animator(Mecanim) 기반 애니메이션으로 교체 (프리팹에 Animator 설정)
//   - sortingOrder 불필요 (3D Z-buffer로 렌더 순서 자동 처리)
//
// Inspector 설정:
//   - UnitPrefab: 유닛 프리팹 참조 (Renderer + UnitView + Animator)
//   - UnitParent: 생성된 유닛들의 부모 Transform ([World]/Units)
//
// 프로토타입에서는 프리팹이 1종(권총병)뿐이라 단순 구조.
// MVP에서 여러 유닛 타입 지원 시 Dictionary<UnitType, GameObject>로 확장.
//
// Infrastructure 레이어 — Unity 의존 (GameObject, Instantiate).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Unity.Netcode;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;
using Hexiege.Presentation;

namespace Hexiege.Infrastructure
{
    public class UnitFactory : MonoBehaviour
    {
        // ====================================================================
        // 런타임 의존성 주입 — 생산된 유닛에 자동 적용
        // ====================================================================

        private GameConfig _depConfig;
        private UnitMovementUseCase _depMovement;
        private UnitCombatUseCase _depCombat;
        private UnitFactory _depUnitFactory;
        private BuildingFactory _depBuildingFactory;
        private bool _hasDependencies;

        /// <summary>
        /// GameBootstrapper에서 UseCase 생성 후 한 번 호출.
        /// 이후 생성되는 모든 유닛에 자동으로 SetDependencies() 적용.
        /// UnitFactory/BuildingFactory는 공격 방향 계산 시 타겟 transform 조회에 사용.
        /// </summary>
        public void SetDependencyReferences(
            GameConfig config,
            UnitMovementUseCase movement, UnitCombatUseCase combat,
            UnitFactory unitFactory = null, BuildingFactory buildingFactory = null)
        {
            _depConfig = config;
            _depMovement = movement;
            _depCombat = combat;
            _depUnitFactory = unitFactory;
            _depBuildingFactory = buildingFactory;
            _hasDependencies = true;
        }

        // ====================================================================
        // Inspector에서 설정할 필드
        // ====================================================================

        /// <summary>
        /// 팀별 유닛 프리팹 세트. Inspector에서 Blue/Red 각각 설정.
        /// </summary>
        [System.Serializable]
        public struct UnitTeamPrefabSet
        {
            public GameObject pistoleer;
            public GameObject assault;
            public GameObject sniper;
        }

        [Header("Prefabs")]
        [SerializeField] private UnitTeamPrefabSet _bluePrefabs;
        [SerializeField] private UnitTeamPrefabSet _redPrefabs;

        [Header("Hierarchy")]
        /// <summary>
        /// 생성된 유닛 GameObject들의 부모 Transform.
        /// 씬 계층 정리용. [World]/Units 오브젝트를 연결.
        /// </summary>
        [Tooltip("유닛 부모 Transform ([World]/Units)")]
        [SerializeField] private Transform _unitParent;

        // 생성된 유닛 GameObject를 UnitData.Id로 관리.
        // 유닛 삭제(MVP) 시 GameObject.Destroy에 사용.
        private readonly Dictionary<int, GameObject> _unitObjects = new Dictionary<int, GameObject>();

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// GameEvents.OnUnitSpawned 이벤트를 구독.
        /// UnitSpawnUseCase가 유닛을 생성할 때마다 CreateUnitObject 호출.
        /// .AddTo(this): 이 MonoBehaviour가 파괴되면 자동으로 구독 해제.
        /// </summary>
        /// <summary>
        /// Awake에서 이벤트 구독.
        /// Start()가 아닌 Awake()를 사용하는 이유:
        ///   GameBootstrapper.Start()에서 SpawnTestUnits()가 호출되므로,
        ///   UnitFactory가 반드시 먼저 구독을 완료해야 이벤트를 수신할 수 있음.
        ///   Awake()는 모든 Start()보다 먼저 실행됨.
        /// </summary>
        private void Awake()
        {
            GameEvents.OnUnitSpawned
                .Subscribe(e => CreateUnitObject(e.Unit))
                .AddTo(this);
        }

        // ====================================================================
        // 유닛 GameObject 생성
        // ====================================================================

        /// <summary>
        /// UnitData를 기반으로 유닛 프리팹 인스턴스를 생성하고 초기화.
        ///
        /// 분기:
        ///   - 싱글플레이: 기존 Instantiate 방식 유지
        ///   - 멀티플레이 서버: Instantiate → NetworkObject.Spawn() → NetworkUnit.SetUnitId()
        ///     → NGO가 클라이언트에 자동으로 프리팹 전달
        ///   - 멀티플레이 클라이언트: 아무것도 하지 않음 (NGO가 자동 생성)
        ///     → NetworkUnit.OnNetworkSpawn()에서 RegisterUnitObject로 딕셔너리에 등록
        ///     → SpawnUnitClientRpc에서 InitializeUnitView()로 UnitView 초기화
        ///
        /// 처리 (서버/싱글):
        ///   1. HexCoord → 월드 위치 변환
        ///   2. 프리팹 Instantiate
        ///   3. (멀티) NetworkObject.Spawn() + SetParent + SetUnitId
        ///   4. UnitView 컴포넌트에 UnitData 전달하여 초기화
        ///   5. 내부 딕셔너리에 등록
        /// </summary>
        private void CreateUnitObject(UnitData unitData)
        {
            // ================================================================
            // 멀티플레이 클라이언트: NGO가 자동 생성하므로 아무것도 하지 않음
            // NetworkUnit.OnNetworkSpawn() → RegisterUnitObject()로 딕셔너리에 등록됨
            // SpawnUnitClientRpc → InitializeUnitView()로 UnitView 초기화됨
            // ================================================================
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
                return;

            // 팀·유닛타입에 맞는 프리팹 선택
            var set = unitData.Team == TeamId.Blue ? _bluePrefabs : _redPrefabs;
            GameObject prefab = unitData.Type switch
            {
                UnitType.Pistoleer => set.pistoleer,
                UnitType.Assault   => set.assault,
                UnitType.Sniper    => set.sniper,
                _ => null
            };

            if (prefab == null)
            {
                Debug.LogError($"[UnitFactory] {unitData.Team}/{unitData.Type} 프리팹이 설정되지 않았습니다.");
                return;
            }

            // 유닛의 헥스 좌표 → 도메인 월드 좌표 변환 (Y 오프셋 미포함)
            Vector3 worldPos = HexMetrics.HexToWorld(unitData.Position);

            // 도메인 좌표 → 뷰 좌표 변환 (Red팀이면 맵 중심 기준 Y 반전)
            Vector3 viewPos = ViewConverter.ToView(worldPos);

            // ToView 이후에 Y 오프셋 적용 — ToView 이전에 적용하면 Red팀에서 오프셋 방향이 반전됨
            viewPos.y += HexMetrics.UnitYOffset;

            // 프리팹 인스턴스 생성.
            // 멀티플레이 서버: 부모 없이 월드 루트에 생성 (NGO Spawn 후 SetParent 필요)
            // 싱글플레이: 직접 _unitParent 아래에 생성
            GameObject unitObj;
            if (NetworkContext.IsNetworkActive)
            {
                // 서버: 월드 루트에 Instantiate (NGO는 루트 오브젝트만 Spawn 가능)
                unitObj = Instantiate(prefab, viewPos, Quaternion.identity);
            }
            else
            {
                // 싱글플레이: 기존 방식 — 부모 아래에 직접 생성
                unitObj = Instantiate(prefab, viewPos, Quaternion.identity, _unitParent);
            }

            // 오브젝트 이름을 유닛 정보로 설정 (에디터 디버깅용)
            unitObj.name = $"Unit_{unitData.Type}_{unitData.Team}_{unitData.Id}";

            // ================================================================
            // 멀티플레이 서버: NetworkObject.Spawn() → NGO가 클라이언트에 자동 전달
            // ================================================================
            if (NetworkContext.IsNetworkActive)
            {
                NetworkObject networkObject = unitObj.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    Debug.LogError($"[UnitFactory] NetworkObject 컴포넌트가 없습니다. 프리팹에 추가 필요: {unitData.Type}_{unitData.Team}");
                    Destroy(unitObj);
                    return;
                }

                // NGO에 이 오브젝트를 네트워크 스폰으로 등록
                // → 모든 클라이언트에 자동으로 프리팹 Instantiate됨
                networkObject.Spawn();

                // 씬 계층 정리 생략: NGO에서 NetworkObject의 부모는 반드시 NetworkObject이거나
                // 씬 루트여야 함. [World]/Units는 일반 GameObject이므로 SetParent 불가.
                // 멀티플레이에서는 씬 루트에 그대로 두고, 에디터 계층 정리는 싱글에서만 적용.

                // NetworkUnit 컴포넌트에 unitId 설정 → 클라이언트가 읽어서 딕셔너리 등록
                NetworkUnit networkUnit = unitObj.GetComponent<NetworkUnit>();
                if (networkUnit != null)
                {
                    networkUnit.SetUnitId(unitData.Id);
                }
                else
                {
                    Debug.LogWarning($"[UnitFactory] NetworkUnit 컴포넌트가 없습니다. 프리팹에 추가 필요: {unitData.Type}_{unitData.Team}");
                }
            }

            // UnitView 컴포넌트 가져와서 UnitData 전달
            var unitView = unitObj.GetComponent<UnitView>();
            if (unitView != null)
            {
                unitView.Initialize(unitData);

                // Attack 클립 길이를 읽어 쿨다운 설정
                var animator = unitObj.GetComponentInChildren<Animator>();
                float attackClipLength = GetAttackClipLength(animator);
                if (attackClipLength > 0f)
                    unitData.AttackCooldown = attackClipLength;

                // 런타임 의존성 자동 주입 (생산된 유닛도 즉시 동작 가능)
                if (_hasDependencies)
                    unitView.SetDependencies(_depConfig, _depMovement, _depCombat,
                        _depUnitFactory, _depBuildingFactory);
            }

            // 내부 딕셔너리에 등록 (나중에 삭제 시 사용)
            _unitObjects[unitData.Id] = unitObj;
        }

        /// <summary>
        /// Id로 유닛 GameObject 조회. 외부에서 접근 필요 시 사용.
        /// </summary>
        public GameObject GetUnitObject(int unitId)
        {
            _unitObjects.TryGetValue(unitId, out GameObject obj);
            return obj;
        }

        /// <summary>
        /// 클라이언트 전용: NGO가 자동 생성한 유닛 GameObject를 딕셔너리에 등록.
        /// NetworkUnit.OnNetworkSpawn()에서 호출.
        /// 이후 SpawnUnitClientRpc에서 GetUnitObject(unitId)로 조회 가능.
        /// </summary>
        /// <param name="unitId">서버에서 발급된 유닛 Id</param>
        /// <param name="unitObj">NGO가 자동 생성한 유닛 GameObject</param>
        public void RegisterUnitObject(int unitId, GameObject unitObj)
        {
            _unitObjects[unitId] = unitObj;
        }

        /// <summary>
        /// 클라이언트 전용: NGO가 자동 생성한 유닛 GameObject에 UnitView를 초기화.
        /// SpawnUnitClientRpc에서 UnitData 생성 후 호출.
        ///
        /// 처리:
        ///   1. 딕셔너리에서 해당 unitId의 GameObject 조회
        ///   2. UnitView.Initialize(unitData) 호출
        ///   3. Attack 클립 길이 → 쿨다운 설정
        ///   4. 런타임 의존성 주입
        ///   5. 씬 계층 정리 (_unitParent 아래로 배치)
        ///   6. 오브젝트 이름 설정 (디버깅용)
        ///
        /// 반환값: 초기화 성공 여부 (GameObject를 찾지 못하면 false)
        /// </summary>
        /// <param name="unitData">서버와 동일한 Id로 재생성된 UnitData</param>
        /// <returns>초기화 성공 여부</returns>
        public bool InitializeUnitView(UnitData unitData)
        {
            if (!_unitObjects.TryGetValue(unitData.Id, out GameObject unitObj) || unitObj == null)
            {
                Debug.LogWarning($"[UnitFactory] InitializeUnitView: UnitId={unitData.Id}에 해당하는 GameObject 없음. " +
                                 "NetworkUnit.OnNetworkSpawn()이 아직 호출되지 않았을 수 있습니다.");
                return false;
            }

            // 오브젝트 이름 설정 (에디터 디버깅용)
            unitObj.name = $"Unit_{unitData.Type}_{unitData.Team}_{unitData.Id}";

            // 씬 계층 정리: [World]/Units 하위로 배치
            // NetworkObject는 서버만 부모를 변경할 수 있으므로 클라이언트에서는 건너뜀.
            // 서버에서 Spawn 후 SetParent를 수행하므로 클라이언트에서는 불필요.
            if (_unitParent != null && unitObj.transform.parent != _unitParent
                && (!NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer))
                unitObj.transform.SetParent(_unitParent);

            // 위치는 NetworkTransform이 서버에서 자동 동기화하므로 수동 설정하지 않음.
            // NGO Spawn 시점에 서버 위치가 이미 동기화되어 있어야 함.
            // 만약 초기 1~2프레임 동안 원점에 나타나는 문제가 있다면
            // 여기에 viewPos 설정을 추가할 수 있음.

            // UnitView 컴포넌트 초기화
            var unitView = unitObj.GetComponent<UnitView>();
            if (unitView != null)
            {
                unitView.Initialize(unitData);

                // Attack 클립 길이를 읽어 쿨다운 설정
                var animator = unitObj.GetComponentInChildren<Animator>();
                float attackClipLength = GetAttackClipLength(animator);
                if (attackClipLength > 0f)
                    unitData.AttackCooldown = attackClipLength;

                // 런타임 의존성 자동 주입
                if (_hasDependencies)
                    unitView.SetDependencies(_depConfig, _depMovement, _depCombat,
                        _depUnitFactory, _depBuildingFactory);
            }

            // NetworkUnit에 초기화 완료 플래그 설정
            var networkUnit = unitObj.GetComponent<NetworkUnit>();
            if (networkUnit != null)
                networkUnit.MarkInitialized();

            Debug.Log($"[UnitFactory] 클라이언트: UnitView 초기화 완료. UnitId={unitData.Id}, Type={unitData.Type}, Team={unitData.Team}");
            return true;
        }

        /// <summary>
        /// Animator에서 "Attack"이 포함된 첫 번째 클립의 길이를 반환.
        /// 클립이 없거나 Animator가 null이면 0 반환.
        /// </summary>
        private float GetAttackClipLength(Animator animator)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return 0f;
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name.Contains("Attack"))
                    return clip.length;
            return 0f;
        }

        /// <summary>
        /// 모든 유닛 GameObject를 파괴. 맵 전환 시 호출.
        /// 멀티플레이 서버: NetworkObject.Despawn()으로 클라이언트에서도 자동 제거.
        /// 싱글플레이: 기존 Destroy 방식.
        /// 멀티플레이 클라이언트: 딕셔너리만 초기화 (NGO Despawn은 서버에서 처리).
        /// </summary>
        public void DestroyAllUnits()
        {
            if (NetworkContext.IsNetworkActive && NetworkContext.IsNetworkServer)
            {
                // 서버: NetworkObject.Despawn()으로 네트워크 제거 (클라이언트 자동 동기화)
                foreach (var kvp in _unitObjects)
                {
                    if (kvp.Value != null)
                    {
                        var networkObject = kvp.Value.GetComponent<NetworkObject>();
                        if (networkObject != null && networkObject.IsSpawned)
                            networkObject.Despawn(); // Despawn이 Destroy도 같이 처리
                        else
                            Destroy(kvp.Value);
                    }
                }
            }
            else if (!NetworkContext.IsNetworkActive)
            {
                // 싱글플레이: 기존 Destroy 방식
                foreach (var kvp in _unitObjects)
                {
                    if (kvp.Value != null)
                        Destroy(kvp.Value);
                }
            }
            // 클라이언트: NGO가 서버 Despawn 시 자동으로 GameObject를 제거하므로 수동 파괴 불필요

            _unitObjects.Clear();
        }
    }
}
