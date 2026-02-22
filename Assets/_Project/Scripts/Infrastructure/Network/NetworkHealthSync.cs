// ============================================================================
// NetworkHealthSync.cs
// 유닛/건물 HP 변화를 모든 클라이언트에 동기화.
//
// 역할:
//   - 서버에서 UnitCombatUseCase가 발행하는 OnEntityDamaged 이벤트를 구독
//   - 피격된 엔티티의 현재 HP를 SyncHealthClientRpc로 전파
//   - 각 클라이언트에서 Domain 데이터(UnitData.Hp 필드)를 직접 갱신
//     (UnitData.Hp는 private set이므로 TakeDamage 호출 방식 사용)
//   - 사망 처리는 NetworkCombatController가 담당 (여기서 중복 처리 않음)
//
// 흐름:
//   서버: UnitCombatUseCase.TryAttack() → OnEntityDamaged 발행
//     → NetworkHealthSync.OnEntityDamaged() → SyncHealthClientRpc 전송
//   클라이언트: SyncHealthClientRpc 수신
//     → UnitSpawnUseCase.GetUnit() 또는 BuildingPlacementUseCase.GetBuilding()
//     → TakeDamage로 HP 맞춤 (차이만큼 데미지 적용)
//
// 배치:
//   씬에 빈 GameObject "NetworkHealthSync" 생성.
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

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 유닛/건물 HP 변화를 모든 클라이언트에 동기화하는 NetworkBehaviour.
    /// 서버에서 OnEntityDamaged를 구독하고 ClientRpc로 전파.
    /// </summary>
    public class NetworkHealthSync : NetworkBehaviour
    {
        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>GameBootstrapper 참조. UseCase 접근에 사용.</summary>
        private Hexiege.Bootstrap.GameBootstrapper _bootstrapper;

        /// <summary>OnEntityDamaged 구독 해제용 Disposable.</summary>
        private System.IDisposable _damagedSubscription;

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 GameBootstrapper를 탐색.
        /// 서버라면 OnEntityDamaged 이벤트를 구독하여 클라이언트 전파 준비.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();
            if (_bootstrapper == null)
            {
                Debug.LogWarning("[Network] NetworkHealthSync: GameBootstrapper를 찾을 수 없습니다.");
            }

            Debug.Log($"[Network] NetworkHealthSync 스폰. IsServer={IsServer}");

            // 서버만 HP 변화 이벤트를 구독하여 클라이언트에 동기화
            if (IsServer)
            {
                _damagedSubscription = GameEvents.OnEntityDamaged
                    .Subscribe(OnEntityDamaged);

                Debug.Log("[Network] NetworkHealthSync: 서버 측 OnEntityDamaged 구독 완료.");
            }
        }

        /// <summary>
        /// 네트워크 디스폰 시 이벤트 구독 해제.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _damagedSubscription?.Dispose();
            _damagedSubscription = null;
        }

        // ====================================================================
        // 이벤트 핸들러 (서버 전용)
        // ====================================================================

        /// <summary>
        /// 서버에서 엔티티 피격 이벤트를 수신하여 모든 클라이언트에 HP 전파.
        /// </summary>
        private void OnEntityDamaged(EntityDamagedEvent e)
        {
            if (!IsServer) return;
            if (e.Entity == null) return;

            // 엔티티 Id 추출 (UnitData 또는 BuildingData)
            int entityId;
            if (e.IsUnit && e.Entity is UnitData unit)
            {
                entityId = unit.Id;
            }
            else if (!e.IsUnit && e.Entity is BuildingData building)
            {
                entityId = building.Id;
            }
            else
            {
                Debug.LogWarning("[Network] NetworkHealthSync: 알 수 없는 엔티티 타입.");
                return;
            }

            // 모든 클라이언트에 HP 동기화 전송
            SyncHealthClientRpc(entityId, e.IsUnit, e.CurrentHp);
        }

        // ====================================================================
        // ClientRpc — 서버 → 모든 클라이언트
        // ====================================================================

        /// <summary>
        /// 서버에서 피격 후 현재 HP를 모든 클라이언트에 전파.
        /// 클라이언트는 도메인 데이터를 서버 권위 HP에 맞춰 갱신.
        /// 사망 처리는 NetworkCombatController의 EntityDiedClientRpc에서 별도 수행.
        /// </summary>
        /// <param name="entityId">피격된 엔티티 Id</param>
        /// <param name="isUnit">true=유닛, false=건물</param>
        /// <param name="serverHp">서버 기준 현재 HP</param>
        [ClientRpc]
        private void SyncHealthClientRpc(int entityId, bool isUnit, int serverHp)
        {
            // 서버는 이미 UseCase에서 처리 완료 → 중복 방지
            if (IsServer) return;

            // UseCase 접근
            if (_bootstrapper == null)
                _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();

            if (_bootstrapper == null)
            {
                Debug.LogError("[Network] SyncHealthClientRpc: GameBootstrapper를 찾을 수 없습니다.");
                return;
            }

            if (isUnit)
            {
                SyncUnitHealth(entityId, serverHp);
            }
            else
            {
                SyncBuildingHealth(entityId, serverHp);
            }
        }

        // ====================================================================
        // HP 동기화 헬퍼
        // ====================================================================

        /// <summary>
        /// 클라이언트 측 유닛 HP를 서버 값에 맞춤.
        /// UnitData.Hp는 TakeDamage를 통해서만 변경 가능하므로
        /// 현재 HP와 서버 HP의 차이를 데미지로 적용.
        /// </summary>
        private void SyncUnitHealth(int unitId, int serverHp)
        {
            UnitSpawnUseCase unitSpawn = _bootstrapper.GetUnitSpawn();
            if (unitSpawn == null)
            {
                Debug.LogWarning("[Network] SyncUnitHealth: UnitSpawnUseCase가 null. 맵 로드 전일 수 있음.");
                return;
            }

            UnitData unit = unitSpawn.GetUnit(unitId);
            if (unit == null)
            {
                // 이미 사망 처리되었거나 아직 스폰 전일 수 있음 (조용히 무시)
                return;
            }

            // 현재 HP가 서버 HP보다 높으면 차이만큼 데미지 적용
            int diff = unit.Hp - serverHp;
            if (diff > 0)
            {
                unit.TakeDamage(diff);
                Debug.Log($"[Network] 유닛 HP 동기화. UnitId={unitId}, 적용 데미지={diff}, 현재HP={unit.Hp}");
            }
        }

        /// <summary>
        /// 클라이언트 측 건물 HP를 서버 값에 맞춤.
        /// BuildingData.Hp도 TakeDamage를 통해서만 변경 가능.
        /// </summary>
        private void SyncBuildingHealth(int buildingId, int serverHp)
        {
            BuildingPlacementUseCase buildingPlacement = _bootstrapper.GetBuildingPlacement();
            if (buildingPlacement == null)
            {
                Debug.LogWarning("[Network] SyncBuildingHealth: BuildingPlacementUseCase가 null.");
                return;
            }

            BuildingData building = buildingPlacement.GetBuilding(buildingId);
            if (building == null)
            {
                // 이미 사망 처리되었거나 아직 배치 전일 수 있음 (조용히 무시)
                return;
            }

            int diff = building.Hp - serverHp;
            if (diff > 0)
            {
                building.TakeDamage(diff);
                Debug.Log($"[Network] 건물 HP 동기화. BuildingId={buildingId}, 적용 데미지={diff}, 현재HP={building.Hp}");
            }
        }
    }
}
