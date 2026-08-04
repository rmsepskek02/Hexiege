// ============================================================================
// NetworkSkillController.cs
// 멀티플레이에서 스킬 발동 요청을 서버로 중계하고, 발동 성공 시 건물 글로벌 쿨다운을
// 양 클라이언트에 동기화(로컬 미러)한다.
//
// 흐름(규칙 25·26):
//   1. 클라이언트(조준 완료) → RequestActivateSkill(buildingId, slot, aimCoord, hasAim)
//        → RequestActivateSkillServerRpc(...)
//   2. 서버: 서비스/UseCase null → 발신자 팀 소유권 + 건물 소유 → SkillActivationUseCase.Activate
//        (Activate 내부에서 건물 생존·쿨다운·유효 타일 재검증 — 클라 입력 불신뢰).
//        - 실패: 아무것도 하지 않음(조용히 무시. 필요 시 향후 실패 토스트 추가).
//        - 성공: 쿨다운 총량을 읽어 SkillActivatedClientRpc를 **양 클라 브로드캐스트**.
//   3. 클라이언트: 브로드캐스트를 받아 SkillActivationUseCase.StartCooldownLocal로 쿨다운 미러를 세운다
//        (오버레이가 서버와 같은 남은 시간을 표시하도록). 서버(호스트)는 Activate에서 이미 세웠으므로 스킵.
//
// 참고 패턴 = NetworkUpgradeController(main 병합 최신 관례):
//   래퍼 → ServerRpc, 팀 소유권(SenderClientId), 브로드캐스트 ClientRpc,
//   GameServicesLocator.Current 지연 해석(스폰 레이스 방지).
//
// 배치(사용자 Unity 작업):
//   씬에 빈 GameObject "NetworkSkillController" 생성 → NetworkObject + 이 스크립트 부착.
//   NetworkManager의 씬 오브젝트(자동 스폰)로 설정. GameBootstrapper._networkSkillController에 연결.
//
// 아키텍처: Infrastructure 레이어 — NetworkBehaviour 허용. RPC 접미사 ServerRpc/ClientRpc 준수.
//   INetworkSkillController(Application 인터페이스)를 구현해 Presentation이 Netcode를 모르게 한다.
// ============================================================================

using Unity.Netcode;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 스킬 발동 네트워크 컨트롤러. 클라이언트 발동 요청을 서버에서 재검증·실행하고,
    /// 발동 성공 시 건물 글로벌 쿨다운을 양 클라이언트에 동기화한다.
    /// </summary>
    public class NetworkSkillController : NetworkBehaviour, INetworkSkillController
    {
        /// <summary> 게임 서비스 참조(IGameServices로 추상화). 지연 해석. </summary>
        private IGameServices _services;

        // ====================================================================
        // 생명주기
        // ====================================================================

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // 스폰 시점 1차 캐시. 아직 GameServicesLocator 등록 전이면 null일 수 있고,
            // 그 경우 사용 시점 ResolveServices()가 지연 재조회로 복구한다(스폰 레이스 방지).
            if (ResolveServices() == null)
            {
                Debug.LogWarning("[Network] NetworkSkillController: GameServicesLocator에 IGameServices가 없습니다(스폰 레이스 가능 — 사용 시점 재조회로 복구 시도).");
            }

            Debug.Log($"[Network] NetworkSkillController 스폰. IsServer={IsServer}");
        }

        /// <summary>
        /// IGameServices를 지연 해석한다(NetworkUpgradeController.ResolveServices 미러).
        /// 씬 오브젝트 스폰 레이스로 _services가 null로 굳는 것을 방지하기 위해 사용 시점 재조회한다.
        /// </summary>
        private IGameServices ResolveServices()
        {
            if (_services == null) _services = GameServicesLocator.Current;
            return _services;
        }

        // ====================================================================
        // 외부 호출용 래퍼 — Presentation(스킬 패널/조준 컨트롤러)에서 사용
        // ====================================================================

        /// <summary>
        /// 스킬 발동 요청 — UI/조준 래퍼. 실제로는 RequestActivateSkillServerRpc로 위임된다.
        /// 좌표는 지점 지정(타입 A·B)에서만 의미가 있으며, 즉시 발동(타입 C)은 hasAim=false로 보낸다.
        /// </summary>
        /// <param name="buildingId">발동한 스킬 건물 Id.</param>
        /// <param name="skillSlot">발동한 슬롯 번호(0-based).</param>
        /// <param name="aimWorld">조준 좌표(도메인 월드 Vector3, 연속). 지점 지정에서만 사용.</param>
        /// <param name="hasAim">조준 좌표 유효 여부(즉시 발동이면 false).</param>
        public void RequestActivateSkill(int buildingId, int skillSlot, Vector3 aimWorld, bool hasAim)
        {
            // NGO 2.9.2는 Vector3를 기본 직렬화하므로 연속 좌표를 그대로 전송한다(정수 분해 불필요).
            RequestActivateSkillServerRpc(buildingId, skillSlot, aimWorld, hasAim);
        }

        // ====================================================================
        // ServerRpc — 클라이언트 → 서버
        // ====================================================================

        /// <summary>
        /// 스킬 발동 요청. 서버가 팀 소유권을 검증하고 SkillActivationUseCase.Activate를 호출한다.
        /// (건물 생존·쿨다운·좌표 유효성은 Activate 내부에서 재검증한다 — 규칙 26.)
        /// RequireOwnership=false: 어느 클라이언트든 호출 가능(팀 검증은 내부에서 수행).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestActivateSkillServerRpc(int buildingId, int skillSlot, Vector3 aimWorld, bool hasAim,
            ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            IGameServices services = ResolveServices();
            if (services == null) return;

            SkillActivationUseCase skill = services.GetSkillActivationUseCase();
            BuildingPlacementUseCase buildings = services.GetBuildingPlacement();
            if (skill == null || buildings == null) return;

            // 팀 소유권 검증: 요청 건물의 팀이 발신자 팀과 일치해야 한다.
            //   Host(ClientId=0)=Blue, Client(그 외)=Red.
            BuildingData building = buildings.GetBuilding(buildingId);
            if (building == null) return;
            TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;
            if (building.Team != expectedTeam) return;

            // 조준 좌표 구성(지점 지정만). 서버 Activate가 맵 경계 안 점인지 재확인한다(규칙 26).
            Vector3? aim = hasAim ? aimWorld : (Vector3?)null;

            // 서버 권위 발동(재검증 포함). 성공 시 쿨다운 총량을 읽어 양 클라 브로드캐스트.
            bool activated = skill.Activate(buildingId, skillSlot, aim);
            if (!activated) return;

            float cooldown = skill.GetCooldownTotal(buildingId);
            SkillActivatedClientRpc(buildingId, cooldown);
        }

        // ====================================================================
        // ClientRpc — 서버 → 클라이언트(양 클라 브로드캐스트)
        // ====================================================================

        /// <summary>
        /// 스킬 발동 성공을 모든 클라이언트에 알려 건물 글로벌 쿨다운을 로컬 미러로 세운다.
        /// 서버(호스트)는 Activate에서 이미 쿨다운을 세웠으므로 스킵한다(이중 설정 방지).
        /// 순수 클라이언트는 이 값으로 오버레이가 서버와 같은 남은 시간을 표시할 수 있다.
        /// (VFX 등 연출은 플레이스홀더이므로 이번 범위에서는 쿨다운 미러만 동기화한다.)
        /// </summary>
        [ClientRpc]
        private void SkillActivatedClientRpc(int buildingId, float cooldown)
        {
            if (IsServer) return; // 호스트는 서버 Activate에서 이미 쿨다운을 세웠다.

            SkillActivationUseCase skill = ResolveServices()?.GetSkillActivationUseCase();
            skill?.StartCooldownLocal(buildingId, cooldown);
        }
    }
}
