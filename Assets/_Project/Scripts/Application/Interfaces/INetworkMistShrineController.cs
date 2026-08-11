// ============================================================================
// INetworkMistShrineController.cs
// 멀티플레이에서 "MistShrine 시전 / 자동 모드 토글" 요청을 서버로 보내는 래퍼 인터페이스.
//
// 왜 인터페이스인가(레이어 규칙):
//   실제 RPC 송수신(NetworkBehaviour)은 Infrastructure의 NetworkMistShrineController가 담당한다.
//   Presentation(MistShrine 패널)이 Infrastructure 구체 타입에 직접 의존하지 않도록,
//   또 Application이 Unity.Netcode를 모르도록, 이 인터페이스를 Application에 선언하고
//   Infrastructure가 구현한다(INetworkSkillController와 완전히 같은 의존성 역전 구조).
//
// Presentation은 이 인터페이스만 호출한다:
//   멀티 → INetworkMistShrineController.RequestActivate(...) → 서버 재검증 → 시전
//   싱글 → MistShrineUseCase.Activate(...) 직접 호출(네트워크 컨트롤러 불필요)
//
// Application 레이어 — Domain 의존. Unity.Netcode 직접 참조 없음.
// ============================================================================

using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// MistShrine 시전·자동 모드 토글 요청을 서버로 중계하는 래퍼 인터페이스(구현은 Infrastructure).
    /// </summary>
    public interface INetworkMistShrineController
    {
        /// <summary>
        /// MistShrine 시전을 서버에 요청한다. 서버가 발신자 팀·건물 소유·생존·타입·쿨다운을 재검증한 뒤 실행한다
        /// (GameSystemRules_Buildings.md MistShrine 규칙 20·22).
        /// </summary>
        /// <param name="buildingId">시전할 MistShrine 건물 Id.</param>
        /// <param name="team">요청자가 주장하는 팀(서버가 발신자 ClientId로 재검증한다).</param>
        void RequestActivate(int buildingId, TeamId team);

        /// <summary>
        /// MistShrine 자동 모드 토글을 서버에 요청한다. 서버가 상태를 바꾸고 양 클라이언트에 브로드캐스트한다
        /// (MistShrine 규칙 18·19·22).
        /// </summary>
        /// <param name="buildingId">대상 MistShrine 건물 Id.</param>
        /// <param name="team">요청자가 주장하는 팀(서버가 발신자 ClientId로 재검증한다).</param>
        void RequestToggleAuto(int buildingId, TeamId team);
    }
}
