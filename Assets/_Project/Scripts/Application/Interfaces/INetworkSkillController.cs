// ============================================================================
// INetworkSkillController.cs
// 멀티플레이에서 "스킬 발동 요청"을 서버로 보내는 래퍼 인터페이스.
//
// 왜 인터페이스인가(레이어 규칙):
//   실제 RPC 송수신(NetworkBehaviour)은 Infrastructure의 NetworkSkillController가 담당한다.
//   Presentation(스킬 패널/조준 컨트롤러)이 Infrastructure 구체 타입에 직접 의존하지 않도록,
//   또 Application이 Netcode를 모르도록, 이 인터페이스를 Application에 선언하고 Infrastructure가
//   구현한다(NetworkBuildingController/NetworkUpgradeController와 동일한 의존성 역전).
//
// Presentation은 이 인터페이스만 호출한다:
//   멀티 → INetworkSkillController.RequestActivateSkill(...) → 서버 재검증 → 실행
//   싱글 → SkillActivationUseCase.Activate(...) 직접 호출(네트워크 컨트롤러 불필요)
//
// Application 레이어 — Domain 의존. Unity.Netcode 직접 참조 없음(UnityEngine.Vector3만 사용 — 허용).
// ============================================================================

using UnityEngine;

namespace Hexiege.Application
{
    /// <summary>
    /// 스킬 발동 요청을 서버로 중계하는 래퍼 인터페이스(구현은 Infrastructure).
    /// </summary>
    public interface INetworkSkillController
    {
        /// <summary>
        /// 스킬 발동을 서버에 요청한다. 서버가 건물 생존·쿨다운·좌표(맵 경계) 유효성을 재검증한 뒤 실행한다(규칙 26).
        /// 좌표는 지점 지정(타입 A·B) 스킬에서만 의미가 있으며, 즉시 발동(타입 C)은 무시된다.
        /// </summary>
        /// <param name="buildingId">발동한 스킬 건물의 Id.</param>
        /// <param name="skillSlot">발동한 슬롯 번호(0-based, 0~4).</param>
        /// <param name="aimWorld">조준 좌표(도메인 월드 Vector3, 연속). 지점 지정 스킬에서만 사용.</param>
        /// <param name="hasAim">조준 좌표가 유효한지 여부(즉시 발동이면 false).</param>
        void RequestActivateSkill(int buildingId, int skillSlot, Vector3 aimWorld, bool hasAim);
    }
}
