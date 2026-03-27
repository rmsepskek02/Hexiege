// ============================================================================
// NetworkUnit.cs
// 유닛 프리팹 루트에 부착하는 NetworkBehaviour.
// 서버에서 unitId를 NetworkVariable<int>로 설정하면,
// 클라이언트가 OnNetworkSpawn()에서 이를 읽어 UnitView를 초기화한다.
//
// 타이밍 문제 해결:
//   NGO가 클라이언트에 프리팹을 자동 생성하는 시점과
//   SpawnUnitClientRpc로 UnitData가 도착하는 시점이 다를 수 있음.
//   → NetworkVariable<int>로 unitId를 즉시 동기화하여,
//     OnNetworkSpawn() 시점에 unitId를 읽을 수 있도록 보장.
//
// 초기화 흐름:
//   [서버]
//   1. UnitFactory.CreateUnitObject() → Instantiate → NetworkObject.Spawn()
//   2. NetworkUnit.SetUnitId(unitId) → NetworkVariable에 값 설정
//   3. SpawnUnitClientRpc로 UnitData 생성에 필요한 추가 정보 전송
//
//   [클라이언트]
//   1. NGO가 자동으로 프리팹 Instantiate + OnNetworkSpawn() 호출
//   2. OnNetworkSpawn()에서 unitId 확인:
//      - 이미 값이 있으면 즉시 등록
//      - 없으면 OnValueChanged 콜백으로 대기
//   3. SpawnUnitClientRpc 수신 시 UnitData 생성 + UnitView 초기화
//      → RegisterClientUnit()으로 UnitView를 UnitFactory에 등록
//
// 배치:
//   유닛 프리팹 루트 오브젝트에 부착.
//   에디터 스크립트(AddNetworkTransformToPrefabs)가 자동 추가.
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Hexiege.Core;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 유닛 프리팹의 네트워크 동기화 컴포넌트.
    /// unitId를 NetworkVariable로 서버→클라이언트 동기화.
    /// 클라이언트 OnNetworkSpawn에서 UnitView 초기화를 위한 연결 지점 역할.
    /// </summary>
    public class NetworkUnit : NetworkBehaviour
    {
        // ====================================================================
        // NetworkVariable — unitId 동기화
        // ====================================================================

        /// <summary>
        /// 서버가 설정하고 모든 클라이언트가 읽는 유닛 Id.
        /// -1은 아직 설정되지 않은 초기 상태를 의미.
        /// ReadPermission: Everyone — 모든 클라이언트가 읽을 수 있음.
        /// WritePermission: Server — 서버만 값을 설정할 수 있음.
        /// </summary>
        private NetworkVariable<int> _unitId = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        /// <summary> 현재 동기화된 유닛 Id. -1이면 아직 미설정. </summary>
        public int UnitId => _unitId.Value;

        /// <summary>
        /// 클라이언트에서 UnitView 초기화가 완료되었는지 여부.
        /// SpawnUnitClientRpc에서 중복 초기화를 방지하기 위해 사용.
        /// </summary>
        public bool IsInitialized { get; private set; }

        // ====================================================================
        // 서버 전용 API
        // ====================================================================

        /// <summary>
        /// 서버에서 유닛 스폰 후 호출하여 unitId를 NetworkVariable에 설정.
        /// 이 값은 NGO가 자동으로 모든 클라이언트에 동기화.
        /// UnitFactory.CreateUnitObject()에서 NetworkObject.Spawn() 이후 호출.
        /// </summary>
        /// <param name="unitId">서버에서 발급된 유닛 Id (UnitData.Id)</param>
        public void SetUnitId(int unitId)
        {
            _unitId.Value = unitId;
        }

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 호출.
        /// 서버: 아무 작업 없음 (UnitFactory에서 직접 초기화 완료).
        /// 클라이언트: unitId가 이미 동기화되었으면 즉시 등록,
        ///            아니면 코루틴으로 매 프레임 폴링하며 대기.
        ///
        /// OnValueChanged 대신 폴링을 사용하는 이유:
        ///   NetworkVariable 업데이트가 Spawn() 메시지와 별도 패킷으로 오기 때문에
        ///   OnNetworkSpawn() 시점에 _unitId.Value == -1인 경우가 있음.
        ///   OnValueChanged 콜백은 동일 프레임 내 변경을 놓칠 수 있어,
        ///   Walk RPC가 그 사이에 도착하면 유닛이 딕셔너리에 미등록 → 무음 실패.
        ///   코루틴 폴링은 매 프레임 확인하므로 등록 지연을 최소화.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // 서버는 UnitFactory.CreateUnitObject()에서 이미 모든 초기화 완료
            if (IsServer) return;

            // 클라이언트: unitId가 이미 설정되었으면 즉시 딕셔너리에 등록
            if (_unitId.Value != -1)
            {
                RegisterToFactory(_unitId.Value);
                return;
            }

            // unitId가 아직 도착하지 않았으면 매 프레임 확인하는 코루틴으로 대기
            StartCoroutine(WaitForUnitId());
        }

        /// <summary>
        /// unitId가 설정될 때까지 매 프레임 폴링하는 코루틴.
        /// 최대 3초 대기 후 타임아웃 경고 출력.
        /// </summary>
        private IEnumerator WaitForUnitId()
        {
            float timeout = 3f;
            while (_unitId.Value == -1 && timeout > 0f)
            {
                yield return null;
                timeout -= Time.deltaTime;
            }

            if (_unitId.Value != -1)
                RegisterToFactory(_unitId.Value);
            else
                Debug.LogWarning($"[NetworkUnit] unitId 동기화 타임아웃 (3초 초과)");
        }

        /// <summary>
        /// 네트워크 디스폰 시 호출.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

        /// <summary>
        /// 클라이언트에서 NGO가 자동 생성한 이 GameObject를
        /// UnitFactory._unitObjects 딕셔너리에 등록.
        /// 이후 SpawnUnitClientRpc에서 UnitData를 생성하고 UnitView를 초기화할 때,
        /// UnitFactory.GetUnitObject(unitId)로 이 GameObject를 찾을 수 있게 됨.
        /// </summary>
        /// <param name="unitId">서버에서 발급된 유닛 Id</param>
        private void RegisterToFactory(int unitId)
        {
            // GameBootstrapper를 통해 UnitFactory 접근
            var bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();
            if (bootstrapper == null)
            {
                Debug.LogWarning($"[NetworkUnit] GameBootstrapper를 찾을 수 없습니다. UnitId={unitId}");
                return;
            }

            UnitFactory unitFactory = bootstrapper.GetUnitFactory();
            if (unitFactory == null)
            {
                Debug.LogWarning($"[NetworkUnit] UnitFactory가 null입니다. UnitId={unitId}");
                return;
            }

            // 이 GameObject를 UnitFactory 딕셔너리에 등록
            // → SpawnUnitClientRpc에서 GetUnitObject(unitId)로 조회 가능
            unitFactory.RegisterUnitObject(unitId, gameObject);

            Debug.Log($"[NetworkUnit] 클라이언트: UnitFactory에 등록 완료. UnitId={unitId}");
        }

        /// <summary>
        /// SpawnUnitClientRpc에서 UnitView 초기화 완료 후 호출.
        /// 중복 초기화 방지 플래그 설정.
        /// </summary>
        public void MarkInitialized()
        {
            IsInitialized = true;
        }

        /// <summary>
        /// 이동 방향 추적 상태를 리셋.
        /// Walk 재시작 시 이전 공격 위치 기준의 잘못된 델타 계산 방지.
        /// NetworkCombatController.StartWalkAnimationClientRpc에서 Walk 시작 직전 호출.
        /// 안전망: DOKill 등으로 DORotate가 중단되어 OnComplete가 미호출된 경우에도
        /// 다음 Walk 시작 시 _isPreRotating을 해제하여 LateUpdate 회전이 재개되도록 보장.
        /// </summary>
        public void ResetMovementTracking()
        {
            _hasInitialPosition = false;
            _isPreRotating = false;
        }

        /// <summary>
        /// TurnToFaceClientRpc의 DORotate 실행 중 플래그를 설정/해제.
        /// true: LateUpdate의 델타 기반 회전을 차단 → DOTween 프리-회전 보호.
        /// false: LateUpdate 델타 회전 재개 → 이동 중 자연스러운 방향 추적.
        /// NetworkCombatController에서 DORotate 시작 시 true, OnComplete 시 false로 호출.
        /// </summary>
        public void SetPreRotating(bool value) { _isPreRotating = value; }

        // ====================================================================
        // 클라이언트 이동 방향 회전 계산용
        // ====================================================================

        /// <summary>
        /// 이전 LateUpdate의 뷰 공간 위치.
        /// 이동 방향 벡터 계산에 사용.
        /// </summary>
        private Vector3 _prevPosition;

        /// <summary>
        /// 첫 LateUpdate 전 _prevPosition 초기화 여부.
        /// 첫 프레임의 잘못된 델타 계산 방지.
        /// </summary>
        private bool _hasInitialPosition;

        /// <summary>
        /// TurnToFaceClientRpc의 DORotate 실행 중 여부.
        /// true이면 LateUpdate 델타 기반 회전을 차단하여 DOTween 프리-회전을 보호.
        /// DORotate OnComplete 또는 ResetMovementTracking에서 false로 해제.
        /// </summary>
        private bool _isPreRotating;

        /// <summary>
        /// 유닛 메시의 기본 Y축 회전 오프셋.
        /// UnitView._meshYOffset과 동일값 (프리팹별로 동일하게 설정됨).
        /// Atan2 계산 결과에서 빼서 모델의 기본 회전을 보정.
        /// </summary>
        private const float MeshYOffset = 30f;

        // ====================================================================
        // Red 클라이언트 좌표 보정 + 이동 방향 회전
        // ====================================================================

        /// <summary>
        /// 클라이언트 전용 LateUpdate 처리:
        /// ① Red 클라이언트: NetworkTransform이 동기화한 서버(Blue) 도메인 좌표를
        ///    ViewConverter로 반전하여 올바른 렌더링 위치로 보정.
        /// ② 이동 방향 기반 회전: NetworkTransform rotation 동기화가 비활성이므로,
        ///    위치 델타에서 이동 방향을 계산하여 Y축 회전에 적용.
        ///    정지 중(delta 임계값 미만)에는 회전을 유지하여 공격 애니메이션 회전 보존.
        ///
        /// 실행 조건:
        ///   - 서버(IsServer=true): 실행 안 함. MoveAlongPath에서 ApplyDirection이 직접 처리.
        ///   - 싱글플레이(NetworkContext.IsNetworkActive=false): 실행 안 함. 기존 동작 유지.
        /// </summary>
        private void LateUpdate()
        {
            // 서버는 MoveAlongPath에서 직접 위치·회전을 제어하므로 처리 불필요
            if (IsServer || !NetworkContext.IsNetworkActive) return;

            // ① Red 클라이언트 위치 보정: NetworkTransform이 전달한 서버(Blue) 도메인 좌표를
            //    ViewConverter로 반전하여 Red팀 뷰 좌표로 교정.
            if (ViewConverter.IsFlipped)
                transform.position = ViewConverter.ToView(transform.position);

            // ② 이동 방향 기반 회전 계산 (NetworkTransform rotation 동기화 비활성 상태 대응).
            //    NetworkTransform은 위치만 동기화하고 회전은 전달하지 않으므로,
            //    이전 프레임과 현재 프레임의 위치 델타에서 이동 방향을 계산하여 회전에 적용.
            //    - 이동 중(delta 임계값 이상): 이동 방향으로 회전 업데이트
            //    - 정지 중(delta 임계값 미만): 회전 유지 (공격 애니메이션 회전 보존)
            // _isPreRotating=true이면 TurnToFaceClientRpc의 DORotate 실행 중.
            // DOTween(Update)이 rotation을 설정한 뒤 LateUpdate가 덮어씌우는 충돌을 방지.
            // DORotate OnComplete 또는 ResetMovementTracking에서 false로 해제되면 재개.
            if (!_isPreRotating && _hasInitialPosition)
            {
                Vector3 delta = transform.position - _prevPosition;
                delta.y = 0f; // Y축(높이) 성분 제거 — XZ 평면 이동 방향만 사용

                // sqrMagnitude 임계값 0.0001f = |delta| > 약 0.01f/프레임
                // 걷기 속도(약 0.02~0.05f/프레임)는 통과, 보간 노이즈는 필터링
                if (delta.sqrMagnitude > 0.0001f)
                {
                    float angle = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg - MeshYOffset;
                    Quaternion targetRot = Quaternion.Euler(0f, angle, 0f);
                    // 즉시 스냅 대신 부드러운 회전 보간.
                    // 720 deg/s = 180° 방향 전환이 약 0.25초에 완료.
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation, targetRot, 720f * Time.deltaTime);
                }
            }

            // 위치 보정 후의 뷰 공간 좌표를 저장 (다음 프레임 델타 계산 기준)
            _prevPosition = transform.position;
            _hasInitialPosition = true;
        }
    }
}
