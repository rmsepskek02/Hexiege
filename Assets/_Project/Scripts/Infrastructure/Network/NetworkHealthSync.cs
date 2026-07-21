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

        /// <summary>게임 서비스 참조. UseCase 접근에 사용. Bootstrap에 직접 의존하지 않도록 IGameServices로 추상화.</summary>
        private IGameServices _services;

        /// <summary>OnEntityDamaged 구독 해제용 Disposable.</summary>
        private System.IDisposable _damagedSubscription;

        /// <summary>OnEntityHealed 구독 해제용 Disposable(서버 전용 — 힐 HP를 클라이언트에 전파).</summary>
        private System.IDisposable _healedSubscription;

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

            _services = GameServicesLocator.Current;
            if (_services == null)
            {
                Debug.LogWarning("[Network] NetworkHealthSync: GameServicesLocator에 IGameServices가 등록되지 않았습니다.");
            }

            Debug.Log($"[Network] NetworkHealthSync 스폰. IsServer={IsServer}");

            // 서버만 HP 변화 이벤트를 구독하여 클라이언트에 동기화
            if (IsServer)
            {
                _damagedSubscription = GameEvents.OnEntityDamaged
                    .Subscribe(OnEntityDamaged);

                // 힐(회복)도 동일하게 서버에서 구독하여 절대 HP를 클라이언트에 전파.
                _healedSubscription = GameEvents.OnEntityHealed
                    .Subscribe(OnEntityHealed);

                Debug.Log("[Network] NetworkHealthSync: 서버 측 OnEntityDamaged/OnEntityHealed 구독 완료.");
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

            _healedSubscription?.Dispose();
            _healedSubscription = null;
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

            // 모든 클라이언트에 HP 동기화 전송.
            // 공격자 정보(AttackerId / AttackerIsUnit)도 함께 실어 보내야
            // 클라이언트의 피격 표현 큐가 "누가 때렸는지"를 알고 공격자별 큐를 구성할 수 있다.
            // (Phase 2 — 축 3)
            // ImmediatePresentation(파도 등 이동형 AoE)도 전파하여 클라이언트도 즉시 방출하게 한다(규칙 26).
            SyncHealthClientRpc(entityId, e.IsUnit, e.CurrentHp, e.AttackerId, e.AttackerIsUnit,
                e.ImmediatePresentation);
        }

        /// <summary>
        /// 서버에서 엔티티 회복(힐) 이벤트를 수신하여 모든 클라이언트에 절대 HP 전파.
        /// 유닛 힐만 다룬다(현재 파도/힐러 대상은 유닛). 건물 힐은 아직 사용처가 없어 무시.
        /// </summary>
        private void OnEntityHealed(EntityHealedEvent e)
        {
            if (!IsServer) return;
            if (e.Entity == null) return;
            if (!e.IsUnit || !(e.Entity is UnitData unit)) return;

            // 모든 클라이언트에 회복 후 절대 HP를 전송(힐러 정보도 함께 실어 연출이 힐러를 알 수 있게).
            //   ShowText도 그대로 전파한다:
            //     - HoT 틱(ShowText=false): 클라도 HP만 동기화하고 텍스트는 안 뜬다.
            //     - HoT 완료(ShowText=true): 클라도 "현재 HP" 텍스트를 1회 뜨게 한다(HP 차이가 0이어도).
            SyncHealClientRpc(unit.Id, e.CurrentHp, e.HealerId, e.HealerIsUnit,
                e.ShowText);
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
        /// <param name="attackerId">공격자 Id (유닛이면 UnitData.Id, 타워면 BuildingData.Id)</param>
        /// <param name="attackerIsUnit">공격자가 유닛인지 여부. false면 타워(건물).</param>
        /// <param name="immediatePresentation">파도 등 이동형 AoE 피해면 true — 클라이언트도 즉시 방출(규칙 26).</param>
        [ClientRpc]
        private void SyncHealthClientRpc(int entityId, bool isUnit, int serverHp,
            int attackerId, bool attackerIsUnit, bool immediatePresentation)
        {
            // 서버는 이미 UseCase에서 처리 완료 → 중복 방지
            if (IsServer) return;

            // _services는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            if (_services == null)
            {
                Debug.LogError("[Network] SyncHealthClientRpc: GameBootstrapper를 찾을 수 없습니다.");
                return;
            }

            if (isUnit)
            {
                SyncUnitHealth(entityId, serverHp, attackerId, attackerIsUnit, immediatePresentation);
            }
            else
            {
                SyncBuildingHealth(entityId, serverHp, attackerId, attackerIsUnit);
            }
        }

        /// <summary>
        /// 서버에서 회복 후 현재 HP를 모든 클라이언트에 전파.
        /// 클라이언트는 도메인 데이터를 서버 권위 HP까지 Heal로 끌어올리고 OnEntityHealed를 재발행한다.
        /// 피격과 별도 RPC로 분리해, 부호 추정 없이 명확히 힐로만 처리한다(오분류 방지).
        /// </summary>
        /// <param name="unitId">회복된 유닛 Id</param>
        /// <param name="serverHp">서버 기준 회복 후 현재 HP</param>
        /// <param name="healerId">힐러 Id(없으면 -1)</param>
        /// <param name="healerIsUnit">힐러가 유닛인지 여부</param>
        /// <param name="showText">부유 힐 텍스트 표시 여부(HoT 틱=false, 즉발/파도/HoT 완료=true). 표시 형식은 모두 현재 HP.</param>
        [ClientRpc]
        private void SyncHealClientRpc(int unitId, int serverHp, int healerId, bool healerIsUnit,
            bool showText)
        {
            // 서버는 이미 UseCase에서 처리 완료 → 중복 방지
            if (IsServer) return;

            if (_services == null)
            {
                Debug.LogError("[Network] SyncHealClientRpc: GameBootstrapper를 찾을 수 없습니다.");
                return;
            }

            SyncUnitHeal(unitId, serverHp, healerId, healerIsUnit, showText);
        }

        // ====================================================================
        // HP 동기화 헬퍼
        // ====================================================================

        /// <summary>
        /// 클라이언트 측 유닛 HP를 서버 값에 맞춤.
        /// UnitData.Hp는 TakeDamage를 통해서만 변경 가능하므로
        /// 현재 HP와 서버 HP의 차이를 데미지로 적용.
        /// </summary>
        private void SyncUnitHealth(int unitId, int serverHp, int attackerId, bool attackerIsUnit,
            bool immediatePresentation)
        {
            UnitSpawnUseCase unitSpawn = _services.GetUnitSpawn();
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

                // [임시 로그 — QuakeSpirit 검증, 제거 예정]
                // 클라이언트가 서버로부터 받은 HP 변화를 파일에 기록해 host(서버 권위) 분포와 대조한다.
                // 공격자가 QuakeSpirit인 피격만 남겨 무관한 전투 로그로 파일이 오염되지 않게 필터링한다
                // (주 타깃 100%·스플래시 50% 모두 공격자가 QuakeSpirit이므로 둘 다 잡힌다).
                if (IsQuakeSpiritAttacker(attackerIsUnit, attackerId, unitSpawn))
                {
                    RuntimeLogger.Log(LogLevel.Info, "Combat", "NetworkHealthSync",
                        "[client 수신] 유닛 HP 동기화",
                        $"victimId={unitId}, 종류=Unit, 적용데미지={diff}, 현재HP={unit.Hp}, 공격자Id={attackerId}");
                }

                // 클라이언트에서도 피격 표현 큐/HP 텍스트가 반응할 수 있도록 이벤트 재발행.
                // 서버는 UnitCombatUseCase에서 이미 발행했으므로 클라이언트 전용.
                // RPC로 받은 공격자 정보를 그대로 전달하여, 클라이언트 큐가 공격자의 로컬
                // OnAttackHit 신호에 맞춰 연출을 방출할 수 있게 한다. (Phase 2 — 축 3)
                // 파도 피해는 immediatePresentation=true를 전달해 큐 보류 없이 즉시 방출된다(규칙 26).
                GameEvents.OnEntityDamaged.OnNext(new EntityDamagedEvent(unit, serverHp, isUnit: true,
                    attackerId: attackerId, attackerIsUnit: attackerIsUnit,
                    immediatePresentation: immediatePresentation));
            }
        }

        /// <summary>
        /// 클라이언트 측 유닛 HP를 서버 회복 값까지 끌어올린다(힐 전용).
        /// UnitData.Hp는 Heal을 통해서만 증가하므로 서버 HP와의 양(+) 차이만큼 Heal을 적용한다.
        /// 부유 힐 텍스트는 showText=true일 때만 OnEntityHealed 재발행으로 띄운다(HoT 틱은 텍스트 억제).
        /// HoT 완료 텍스트는 HP 차이가 0이어도 표시해야 하므로, HP 동기화와 텍스트 재발행을 분리해 처리한다.
        /// </summary>
        private void SyncUnitHeal(int unitId, int serverHp, int healerId, bool healerIsUnit,
            bool showText)
        {
            UnitSpawnUseCase unitSpawn = _services.GetUnitSpawn();
            if (unitSpawn == null)
            {
                Debug.LogWarning("[Network] SyncUnitHeal: UnitSpawnUseCase가 null. 맵 로드 전일 수 있음.");
                return;
            }

            UnitData unit = unitSpawn.GetUnit(unitId);
            if (unit == null)
            {
                // 이미 사망 처리되었거나 아직 스폰 전일 수 있음 (조용히 무시)
                return;
            }

            // (1) HP 동기화 — 서버 HP가 현재보다 높으면 그 차이만큼 회복(절대값 동기화 — 방향만 반대).
            //     HoT 틱(showText=false)이라도 여기서 HP는 매번 올라간다 → 클라 HP바가 서서히 차오른다.
            int diff = serverHp - unit.Hp;
            if (diff > 0)
            {
                unit.Heal(diff);
                Debug.Log($"[Network] 유닛 힐 동기화. UnitId={unitId}, 적용 회복={diff}, 현재HP={unit.Hp}");
            }

            // (2) 부유 힐 텍스트 — ShowText가 true일 때만 이벤트를 재발행하여 텍스트를 띄운다(클라이언트 전용).
            //     ⚠️ HoT 완료 텍스트는 HP 차이가 0(이미 매 틱 동기화 완료)이어도 떠야 하므로,
            //        위 diff>0 블록 밖에서 showText만 보고 재발행한다.
            //     ⚠️ HoT 틱(showText=false)은 재발행하지 않아 틱마다 텍스트가 뜨지 않는다(이번 변경의 핵심).
            if (showText)
            {
                GameEvents.OnEntityHealed.OnNext(new EntityHealedEvent(unit, unit.Hp, isUnit: true,
                    healerId: healerId, healerIsUnit: healerIsUnit,
                    showText: true));
            }
        }

        /// <summary>
        /// 클라이언트 측 건물 HP를 서버 값에 맞춤.
        /// BuildingData.Hp도 TakeDamage를 통해서만 변경 가능.
        /// </summary>
        private void SyncBuildingHealth(int buildingId, int serverHp, int attackerId, bool attackerIsUnit)
        {
            BuildingPlacementUseCase buildingPlacement = _services.GetBuildingPlacement();
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

                // [임시 로그 — QuakeSpirit 검증, 제거 예정]
                // 클라이언트가 받은 건물 HP 변화를 기록. QuakeSpirit 스플래시(건물 대상)만 필터링해 남긴다.
                if (IsQuakeSpiritAttacker(attackerIsUnit, attackerId, _services.GetUnitSpawn()))
                {
                    RuntimeLogger.Log(LogLevel.Info, "Combat", "NetworkHealthSync",
                        "[client 수신] 건물 HP 동기화",
                        $"victimId={buildingId}, 종류=Building, 적용데미지={diff}, 현재HP={building.Hp}, 공격자Id={attackerId}");
                }

                // 클라이언트에서도 피격 표현 큐/HP 텍스트가 반응할 수 있도록 이벤트 재발행.
                // 서버는 UnitCombatUseCase에서 이미 발행했으므로 클라이언트 전용.
                // RPC로 받은 공격자 정보를 그대로 전달한다. (Phase 2 — 축 3)
                GameEvents.OnEntityDamaged.OnNext(new EntityDamagedEvent(building, serverHp, isUnit: false,
                    attackerId: attackerId, attackerIsUnit: attackerIsUnit));
            }
        }

        // [임시 로그 — QuakeSpirit 검증, 제거 예정]
        /// <summary>
        /// 이번 피격의 공격자가 QuakeSpirit인지 판정한다(클라 로그 필터용).
        /// 공격자가 유닛이고, 현재 클라이언트에 그 유닛이 존재하며, 타입이 QuakeSpirit일 때만 true.
        /// (공격자가 이미 사망/미스폰이면 확정할 수 없으므로 false → 무관 로그를 남기지 않는다.)
        /// </summary>
        private static bool IsQuakeSpiritAttacker(bool attackerIsUnit, int attackerId, UnitSpawnUseCase unitSpawn)
        {
            if (!attackerIsUnit || unitSpawn == null) return false;
            UnitData attacker = unitSpawn.GetUnit(attackerId);
            return attacker != null && attacker.Type == UnitType.QuakeSpirit;
        }
    }
}
