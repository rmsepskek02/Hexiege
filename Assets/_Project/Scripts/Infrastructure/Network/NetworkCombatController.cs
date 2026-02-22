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

using Unity.Netcode;
using UnityEngine;
using UniRx;
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
        [Tooltip("전투 처리 간격 (초). 0.2 = 초당 5회 전투 판정.")]
        [SerializeField] private float _attackInterval = 0.2f;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>GameBootstrapper 참조. UseCase 접근에 사용.</summary>
        private Hexiege.Bootstrap.GameBootstrapper _bootstrapper;

        /// <summary>OnEntityDied 구독 해제용 Disposable.</summary>
        private System.IDisposable _diedSubscription;

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

            // 서버만 사망 이벤트를 구독하여 클라이언트에 동기화
            if (IsServer)
            {
                _diedSubscription = GameEvents.OnEntityDied
                    .Subscribe(OnEntityDied);

                Debug.Log("[Network] NetworkCombatController: 서버 측 OnEntityDied 구독 완료.");
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

                if (combat.TryAttack(unit))
                {
                    // 공격 성공 → 모든 클라이언트에 공격 애니메이션 전파.
                    // TryAttack 내부에서 attacker.Facing이 공격 방향으로 갱신됨.
                    TriggerAttackAnimationClientRpc(unit.Id, (int)unit.Facing);
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
        /// UnitView.TriggerAttackAnimation()을 호출하여 공격 스프라이트 재생.
        /// 서버(Host)도 이 ClientRpc를 수신하여 동일한 애니메이션 처리.
        /// (싱글플레이의 OnEntityAttacked 이벤트 구독을 멀티플레이에서는 비활성화하고
        ///  이 ClientRpc로 대체하여 타이밍 일관성 보장.)
        /// </summary>
        /// <param name="unitId">공격한 유닛의 Id</param>
        /// <param name="facingInt">공격 방향 (HexDirection enum의 정수값)</param>
        [ClientRpc]
        private void TriggerAttackAnimationClientRpc(int unitId, int facingInt)
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

            HexDirection facing = (HexDirection)facingInt;
            unitView.TriggerAttackAnimation(facing);
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
