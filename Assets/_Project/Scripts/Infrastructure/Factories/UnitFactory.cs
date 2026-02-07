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
// Inspector 설정:
//   - UnitPrefab: 유닛 프리팹 참조 (SpriteRenderer + UnitView + FrameAnimator)
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
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    public class UnitFactory : MonoBehaviour
    {
        // ====================================================================
        // Inspector에서 설정할 필드
        // ====================================================================

        [Header("Prefab")]
        /// <summary>
        /// 유닛 프리팹 참조.
        /// 프리팹 구성: SpriteRenderer + UnitView + FrameAnimator.
        /// Phase 10에서 생성 예정.
        /// </summary>
        [Tooltip("유닛 프리팹 (SpriteRenderer + UnitView + FrameAnimator)")]
        [SerializeField] private GameObject _unitPrefab;

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
        /// 처리:
        ///   1. HexCoord → 월드 위치 변환
        ///   2. 프리팹 Instantiate
        ///   3. UnitView 컴포넌트에 UnitData 전달하여 초기화
        ///   4. 내부 딕셔너리에 등록
        /// </summary>
        private void CreateUnitObject(UnitData unitData)
        {
            if (_unitPrefab == null)
            {
                Debug.LogError("[UnitFactory] UnitPrefab이 설정되지 않았습니다.");
                return;
            }

            // 유닛의 헥스 좌표 → Unity 월드 좌표 변환 (Y 오프셋 포함)
            // HexToWorldUnit: 타일 중심보다 위에 배치하여 "타일 위에 서있는" 느낌
            Vector3 worldPos = HexMetrics.HexToWorldUnit(unitData.Position);

            // 프리팹 인스턴스 생성. _unitParent 하위에 배치.
            GameObject unitObj = Instantiate(_unitPrefab, worldPos, Quaternion.identity, _unitParent);

            // 오브젝트 이름을 유닛 정보로 설정 (에디터 디버깅용)
            unitObj.name = $"Unit_{unitData.Type}_{unitData.Team}_{unitData.Id}";

            // UnitView 컴포넌트 가져와서 UnitData 전달
            // UnitView는 Presentation 레이어 — Phase 7에서 구현
            var unitView = unitObj.GetComponent<Presentation.UnitView>();
            if (unitView != null)
            {
                unitView.Initialize(unitData);
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
        /// 모든 유닛 GameObject를 파괴. 맵 전환 시 호출.
        /// </summary>
        public void DestroyAllUnits()
        {
            foreach (var kvp in _unitObjects)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            _unitObjects.Clear();
        }
    }
}
