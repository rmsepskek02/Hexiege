// ============================================================================
// NetworkCombatController.cs
// 네트워크 모드에서 유닛 전투 처리를 서버 권위로 수행하고 사망을 모든 클라이언트에 동기화.
//
// 역할:
//   - 서버 Update에서 매 프레임 UnitCombatUseCase를 통해 전투 Tick 수행
//     (싱글플레이에서는 UnitView.MoveAlongPath의 코루틴이 TryAttack을 직접 호출)
//     (멀티플레이에서는 UnitCombatUseCase.TryAttack이 클라이언트에서 return false이므로
//      이 컨트롤러가 서버 Update에서 모든 살아있는 유닛에 대해 TryAttack을 호출)
//   - OnEntityDied 이벤트 구독 → EntityDiedClientRpc로 사망 전파
//   - 클라이언트에서 도메인 데이터 정리 + GameEvents.OnEntityDied 재발행
//     → GameEndUseCase가 Castle 파괴 감지 → GameEndUI 반응
//
// 전투 Tick 주기:
//   UnitView.MoveAlongPath에서는 Lerp 이동 중 매 프레임 TryAttack을 호출.
//   멀티플레이에서는 클라이언트가 TryAttack을 호출해도 즉시 return false됨.
//   대신 이 컨트롤러가 서버에서 _attackInterval마다 모든 유닛을 일괄 처리.
//
// 배치:
//   씬에 빈 GameObject "NetworkCombatController" 생성.
//   NetworkObject 컴포넌트 + 이 스크립트를 부착.
//   NetworkManager의 씬 오브젝트로 자동 스폰.
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UniRx;
using DG.Tweening;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Presentation;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 전투 네트워크 컨트롤러.
    /// 서버에서 전투를 처리하고 사망 이벤트를 모든 클라이언트에 전파.
    /// </summary>
    public class NetworkCombatController : NetworkBehaviour
    {
        // ====================================================================
        // Inspector 설정
        // ====================================================================

        [Header("전투 설정")]
        [Tooltip("전투 처리 간격 (초). 0.05 = 초당 20회 전투 판정. 첫 공격까지의 최대 지연을 줄이기 위해 낮게 설정.")]
        [SerializeField] private float _attackInterval = 0.05f;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>GameBootstrapper 참조. UseCase 접근에 사용.</summary>
        private Hexiege.Bootstrap.GameBootstrapper _bootstrapper;

        /// <summary>OnEntityDied 구독 해제용 Disposable.</summary>
        private System.IDisposable _diedSubscription;

        /// <summary>OnUnitWalkStarted 구독 해제용 Disposable. 서버 전용.</summary>
        private System.IDisposable _walkStartedSubscription;

        /// <summary>OnUnitWalkStopped 구독 해제용 Disposable. 서버 전용.</summary>
        private System.IDisposable _walkStoppedSubscription;

        /// <summary>OnUnitFacingChanged 구독 해제용. 서버 전용. 이동 시작 직전 방향을 클라이언트에 전파.</summary>
        private System.IDisposable _facingChangedSubscription;

        /// <summary>다음 전투 판정까지 남은 시간.</summary>
        private float _attackTimer = 0f;

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 GameBootstrapper를 탐색하고
        /// 서버라면 OnEntityDied 이벤트를 구독하여 사망 전파 준비.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();
            if (_bootstrapper == null)
            {
                Debug.LogWarning("[Network] NetworkCombatController: GameBootstrapper를 찾을 수 없습니다.");
            }

            // NetworkContext에 네트워크 상태 주입.
            // UnitCombatUseCase가 NetworkManager에 직접 의존하지 않도록
            // Application 레이어용 정적 홀더를 업데이트.
            NetworkContext.Set(isServer: IsServer, isActive: true);
            Debug.Log($"[Network] NetworkCombatController 스폰. IsServer={IsServer}. NetworkContext 설정 완료.");

            // 서버만 사망/Walk 이벤트를 구독하여 클라이언트에 동기화
            if (IsServer)
            {
                _diedSubscription = GameEvents.OnEntityDied
                    .Subscribe(OnEntityDied);

                // 서버 UnitView의 Walk 시작/정지 이벤트를 수신하여 클라이언트에 전파.
                // UnitView.MoveAlongPath()는 서버에서만 실행되므로,
                // Walk 애니메이션 상태를 ClientRpc로 클라이언트에 동기화.
                _walkStartedSubscription = GameEvents.OnUnitWalkStarted
                    .Subscribe(unitId => StartWalkAnimationClientRpc(unitId));

                _walkStoppedSubscription = GameEvents.OnUnitWalkStopped
                    .Subscribe(unitId => StopWalkAnimationClientRpc(unitId));

                // 유닛 방향 전환 이벤트: 이동 시작 직전 방향을 클라이언트에 전달하여
                // 이동보다 회전이 먼저 시작되도록 함
                _facingChangedSubscription = GameEvents.OnUnitFacingChanged
                    .Subscribe(e => TurnToFaceClientRpc(e.UnitId, e.YAngle, e.RotationDuration));

                Debug.Log("[Network] NetworkCombatController: 서버 측 OnEntityDied, Walk, Facing 이벤트 구독 완료.");
            }
        }

        /// <summary>
        /// 네트워크 디스폰 시 이벤트 구독 해제.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _diedSubscription?.Dispose();
            _diedSubscription = null;

            // Walk 이벤트 구독 해제 (서버에서만 구독했으므로 null일 수 있음)
            _walkStartedSubscription?.Dispose();
            _walkStartedSubscription = null;
            _walkStoppedSubscription?.Dispose();
            _walkStoppedSubscription = null;
            _facingChangedSubscription?.Dispose();
            _facingChangedSubscription = null;

            // 연결 해제 시 NetworkContext를 싱글플레이 기본값으로 초기화
            NetworkContext.Reset();
        }

        // ====================================================================
        // 서버 전투 Update
        // ====================================================================

        /// <summary>
        /// 서버 전용 Update: 주기적으로 모든 살아있는 유닛의 전투를 처리.
        ///
        /// 싱글플레이: UnitView.MoveAlongPath의 코루틴이 TryAttack을 직접 호출.
        /// 멀티플레이: TryAttack이 클라이언트에서 false 반환되므로
        ///             이 서버 Update에서 일괄 처리.
        ///
        /// _attackInterval마다 Tick을 발행하여 매 프레임 처리 비용을 줄임.
        /// </summary>
        private void Update()
        {
            // 멀티플레이 서버만 실행
            if (!IsServer) return;

            _attackTimer += Time.deltaTime;
            if (_attackTimer < _attackInterval) return;
            _attackTimer = 0f;

            TickCombat();
        }

        /// <summary>
        /// 살아있는 모든 유닛에 대해 TryAttack 호출.
        /// 공격 성공 시 모든 클라이언트에 공격 애니메이션 ClientRpc 전송.
        /// UseCase가 null이면 맵 로드 전이므로 건너뜀.
        /// </summary>
        private void TickCombat()
        {
            if (_bootstrapper == null)
                _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();

            if (_bootstrapper == null) return;

            UnitSpawnUseCase unitSpawn = _bootstrapper.GetUnitSpawn();
            UnitCombatUseCase combat = _bootstrapper.GetCombatUseCase();

            if (unitSpawn == null || combat == null) return;

            // Dictionary를 순회하면서 TryAttack
            // 전투 중 RemoveUnit이 호출될 수 있으므로 키를 미리 복사
            var unitIds = new System.Collections.Generic.List<int>(unitSpawn.Units.Keys);
            foreach (int id in unitIds)
            {
                // 순회 도중 사망 처리로 제거되었을 수 있으므로 재확인
                if (!unitSpawn.Units.TryGetValue(id, out UnitData unit)) continue;
                if (!unit.IsAlive) continue;

                // 쿨다운 감소 (서버 Tick 간격만큼)
                if (unit.AttackCooldownRemaining > 0f)
                    unit.AttackCooldownRemaining -= _attackInterval;

                var attackResult = combat.TryAttack(unit);
                if (attackResult.HasValue)
                {
                    // 공격 성공 → 모든 클라이언트에 타겟 Id + 타입 전파.
                    // 클라이언트에서 타겟 GameObject의 실제 transform.position으로 정밀 방향 계산.
                    TriggerAttackAnimationClientRpc(unit.Id, attackResult.Value.id, attackResult.Value.isUnit);
                }
            }
        }

        // ====================================================================
        // 이벤트 핸들러 (서버 전용)
        // ====================================================================

        /// <summary>
        /// 서버에서 엔티티 사망 이벤트를 수신하여 모든 클라이언트에 사망 전파.
        /// GameEndUseCase도 OnEntityDied를 구독하므로 서버에서 이미 게임 종료 판정됨.
        /// 클라이언트에도 동일한 이벤트 체인을 재현하기 위해 ClientRpc로 전파.
        /// </summary>
        private void OnEntityDied(EntityDiedEvent e)
        {
            if (!IsServer) return;
            if (e.Entity == null) return;

            // 엔티티 Id와 타입 추출
            int entityId;
            bool isUnit;

            if (e.Entity is UnitData unit)
            {
                entityId = unit.Id;
                isUnit = true;
            }
            else if (e.Entity is BuildingData building)
            {
                entityId = building.Id;
                isUnit = false;
            }
            else
            {
                Debug.LogWarning("[Network] NetworkCombatController.OnEntityDied: 알 수 없는 엔티티 타입.");
                return;
            }

            Debug.Log($"[Network] 서버: 엔티티 사망. Id={entityId}, IsUnit={isUnit}");

            // 모든 클라이언트에 사망 전파
            EntityDiedClientRpc(entityId, isUnit);
        }

        // ====================================================================
        // ClientRpc — 서버 → 모든 클라이언트
        // ====================================================================

        /// <summary>
        /// 서버에서 엔티티 사망 후 모든 클라이언트에 사망 처리 명령 전송.
        /// 클라이언트:
        ///   1. 도메인 데이터(Unit/Building) Dictionary에서 제거
        ///   2. GameEvents.OnEntityDied 발행
        ///      → UnitView / BuildingView가 GameObject 파괴
        ///      → GameEndUseCase(클라이언트)가 Castle 사망 감지 → GameEndUI 반응
        /// </summary>
        /// <param name="entityId">사망한 엔티티 Id</param>
        /// <param name="isUnit">true=유닛, false=건물</param>
        [ClientRpc]
        private void EntityDiedClientRpc(int entityId, bool isUnit)
        {
            // 서버는 이미 UseCase에서 처리 완료 → 중복 방지
            if (IsServer) return;

            Debug.Log($"[Network] EntityDiedClientRpc 수신. Id={entityId}, IsUnit={isUnit}");

            // UseCase 접근
            if (_bootstrapper == null)
                _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();

            if (_bootstrapper == null)
            {
                Debug.LogError("[Network] EntityDiedClientRpc: GameBootstrapper를 찾을 수 없습니다.");
                return;
            }

            if (isUnit)
            {
                HandleUnitDied(entityId);
            }
            else
            {
                HandleBuildingDied(entityId);
            }
        }

        /// <summary>
        /// 서버에서 공격 발생 후 모든 클라이언트에 공격 애니메이션 트리거 전송.
        /// 타겟 Id와 타입을 전달하여 클라이언트에서 실제 transform.position 기반 정밀 방향 계산.
        /// 서버(Host)도 이 ClientRpc를 수신하여 동일한 애니메이션 처리.
        /// </summary>
        /// <param name="unitId">공격한 유닛의 Id</param>
        /// <param name="targetId">타겟 엔티티의 Id</param>
        /// <param name="targetIsUnit">true=유닛, false=건물</param>
        [ClientRpc]
        private void TriggerAttackAnimationClientRpc(int unitId, int targetId, bool targetIsUnit)
        {
            if (_bootstrapper == null)
                _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();

            if (_bootstrapper == null) return;

            // UnitFactory에서 해당 유닛의 GameObject 조회
            var unitFactory = _bootstrapper.GetUnitFactory();
            if (unitFactory == null) return;

            GameObject unitObj = unitFactory.GetUnitObject(unitId);
            if (unitObj == null) return;

            UnitView unitView = unitObj.GetComponent<UnitView>();
            if (unitView == null) return;

            // 공격 애니메이션 시작 전에 Walk 애니메이션을 먼저 정지.
            // StopWalkAnimationClientRpc가 별도 RPC로 전송되지만,
            // TriggerAttackAnimationClientRpc보다 늦게 도착할 수 있으므로
            // 여기서 명시적으로 Walk를 정지하여 Walk/Attack 동시 재생을 방지.
            unitView.StopWalkAnimation();
            unitView.TriggerAttackAnimation(targetId, targetIsUnit);
        }

        /// <summary>
        /// 서버가 유닛 Walk 시작을 감지하여 모든 클라이언트에 Walk 애니메이션 재생 명령 전송.
        /// 서버(HOST)는 MoveAlongPath에서 이미 Animator를 직접 제어하므로 스킵.
        /// 클라이언트는 NetworkTransform으로 위치만 동기화받고,
        /// Walk 애니메이션은 이 RPC를 통해 별도로 동기화.
        ///
        /// 유닛 생성 직후 Walk RPC가 도착하면 아직 UnitFactory에 미등록일 수 있으므로,
        /// 코루틴으로 최대 1초간 재시도하여 등록 완료를 대기.
        /// </summary>
        /// <param name="unitId">Walk를 시작한 유닛의 Id</param>
        [ClientRpc]
        private void StartWalkAnimationClientRpc(int unitId)
        {
            // 서버(HOST)는 MoveAlongPath에서 이미 Walk 애니메이션을 직접 제어함 → 중복 방지
            if (IsServer) return;

            StartCoroutine(ApplyStartWalkWithRetry(unitId));
        }

        /// <summary>
        /// Walk 애니메이션 적용 코루틴.
        /// 유닛이 UnitFactory에 등록될 때까지 최대 1초 대기 후 Walk 시작.
        /// 생성 직후 RPC가 도착하면 NetworkUnit의 RegisterToFactory()가 아직 완료되지 않아
        /// GetUnitObject()가 null을 반환할 수 있으므로 재시도가 필요.
        /// </summary>
        private IEnumerator ApplyStartWalkWithRetry(int unitId)
        {
            var unitFactory = _bootstrapper?.GetUnitFactory();
            if (unitFactory == null) yield break;

            // 유닛이 등록될 때까지 최대 1초 대기 (생성 직후 RPC 도착 타이밍 대응)
            float timeout = 1f;
            GameObject unitObj = null;
            while (unitObj == null && timeout > 0f)
            {
                unitObj = unitFactory.GetUnitObject(unitId);
                if (unitObj == null)
                {
                    yield return null;
                    timeout -= Time.deltaTime;
                }
            }

            if (unitObj == null) yield break;

            // Walk 재시작 전 이동 방향 추적 리셋.
            // 공격 중 _prevPosition이 정지 위치로 고정되어 있다가 Walk 재개 시
            // 첫 프레임 델타가 실제 이동 방향과 다를 수 있으므로 리셋해서 올바른 방향 계산 보장.
            NetworkUnit networkUnit = unitObj.GetComponent<NetworkUnit>();
            networkUnit?.ResetMovementTracking();

            UnitView unitView = unitObj.GetComponent<UnitView>();
            unitView?.StartWalkAnimation();
        }

        /// <summary>
        /// 서버가 유닛 Walk 정지를 감지하여 모든 클라이언트에 Walk 애니메이션 정지 명령 전송.
        /// 서버(HOST)는 MoveAlongPath에서 이미 Animator를 직접 제어하므로 스킵.
        /// Walk 정지 = speed=0 → 현재 Walk 프레임 고정 (Idle 역할).
        /// </summary>
        /// <param name="unitId">Walk를 정지한 유닛의 Id</param>
        [ClientRpc]
        private void StopWalkAnimationClientRpc(int unitId)
        {
            // 서버(HOST)는 MoveAlongPath에서 이미 Walk 정지를 직접 제어함 → 중복 방지
            if (IsServer) return;

            var unitFactory = _bootstrapper?.GetUnitFactory();
            if (unitFactory == null) return;

            GameObject unitObj = unitFactory.GetUnitObject(unitId);
            if (unitObj == null) return;

            UnitView unitView = unitObj.GetComponent<UnitView>();
            unitView?.StopWalkAnimation();
        }

        /// <summary>
        /// 이동 시작 직전 유닛의 목표 방향을 클라이언트에 전달.
        /// 클라이언트에서 이동보다 회전이 먼저 시작되도록 DORotate 적용.
        /// 서버는 MoveAlongPath에서 ApplyDirection으로 직접 처리하므로 스킵.
        /// </summary>
        /// <param name="unitId">방향이 바뀐 유닛의 Id</param>
        /// <param name="yAngle">목표 Y축 회전 각도 (도 단위)</param>
        /// <param name="rotationDuration">회전 지속 시간 (초) — UnitView._rotationDuration과 동일값</param>
        [ClientRpc]
        private void TurnToFaceClientRpc(int unitId, float yAngle, float rotationDuration)
        {
            // 서버(HOST)는 MoveAlongPath에서 ApplyDirection으로 이미 회전 처리 완료 → 중복 방지
            if (IsServer) return;

            var unitFactory = _bootstrapper?.GetUnitFactory();
            if (unitFactory == null) return;

            GameObject unitObj = unitFactory.GetUnitObject(unitId);
            if (unitObj == null) return;

            // 이동 방향 추적 리셋 — 회전 대기 중 delta=0이 _prevPosition에 영향 없도록.
            // 회전만 진행되는 동안 LateUpdate의 delta 기반 회전이 간섭하지 않도록 함.
            NetworkUnit networkUnit = unitObj.GetComponent<NetworkUnit>();
            networkUnit?.ResetMovementTracking();

            // 목표 방향으로 부드럽게 회전.
            // rotationDuration은 서버 WaitForSeconds와 동일값 → 서버 이동 시작 시점과 클라이언트 회전 완료 시점 일치.
            unitObj.transform.DOKill();
            unitObj.transform.DORotate(new Vector3(0f, yAngle, 0f), rotationDuration)
                             .SetEase(Ease.OutQuad);
        }

        // ====================================================================
        // 사망 처리 헬퍼 (클라이언트 전용)
        // ====================================================================

        /// <summary>
        /// 클라이언트 측 유닛 사망 처리.
        /// UnitData를 Dictionary에서 제거하고 GameEvents.OnEntityDied 발행.
        /// UnitView가 이를 구독하여 GameObject를 파괴.
        /// </summary>
        private void HandleUnitDied(int unitId)
        {
            UnitSpawnUseCase unitSpawn = _bootstrapper.GetUnitSpawn();
            if (unitSpawn == null)
            {
                Debug.LogWarning("[Network] HandleUnitDied: UnitSpawnUseCase가 null.");
                return;
            }

            UnitData unit = unitSpawn.GetUnit(unitId);
            if (unit == null)
            {
                // 이미 처리되었거나 없는 유닛 (조용히 무시)
                return;
            }

            // HP가 0이 아니라면 강제로 0으로 맞춤 (HP 동기화 패킷보다 사망 패킷이 먼저 도착한 경우)
            if (unit.IsAlive)
            {
                unit.TakeDamage(unit.Hp);
            }

            // 도메인 Dictionary에서 제거
            unitSpawn.RemoveUnit(unitId);

            // GameEvents 재발행 → UnitView.OnEntityDied 구독자 실행
            GameEvents.OnEntityDied.OnNext(new EntityDiedEvent(unit));

            Debug.Log($"[Network] 클라이언트: 유닛 사망 처리 완료. UnitId={unitId}");
        }

        /// <summary>
        /// 클라이언트 측 건물 사망 처리.
        /// BuildingData를 Dictionary에서 제거하고 GameEvents.OnEntityDied 발행.
        /// BuildingView가 이를 구독하여 GameObject를 파괴.
        /// GameEndUseCase도 OnEntityDied를 구독 → Castle 파괴 시 게임 종료 판정.
        /// </summary>
        private void HandleBuildingDied(int buildingId)
        {
            BuildingPlacementUseCase buildingPlacement = _bootstrapper.GetBuildingPlacement();
            if (buildingPlacement == null)
            {
                Debug.LogWarning("[Network] HandleBuildingDied: BuildingPlacementUseCase가 null.");
                return;
            }

            BuildingData building = buildingPlacement.GetBuilding(buildingId);
            if (building == null)
            {
                // 이미 처리되었거나 없는 건물 (조용히 무시)
                return;
            }

            // HP 강제 소진 (클라이언트 HP 동기화보다 사망 패킷이 먼저 도착한 경우 대비)
            if (building.IsAlive)
            {
                building.TakeDamage(building.Hp);
            }

            // 도메인 Dictionary에서 제거
            buildingPlacement.RemoveBuilding(buildingId);

            // GameEvents 재발행 → BuildingView, GameEndUseCase 구독자 실행
            GameEvents.OnEntityDied.OnNext(new EntityDiedEvent(building));

            Debug.Log($"[Network] 클라이언트: 건물 사망 처리 완료. BuildingId={buildingId}");
        }
    }
}
