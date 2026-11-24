// ============================================================================
// IProductionSpeedSource.cs
// 「이 팀의 생산 속도 배율이 지금 얼마인가」를 알려 주는 창구(인터페이스).
// 2026-11-16 지원 스킬 새 상태 Task(_Tasks/2026-11-16/01_00_support-skill-new-statuses) 단계 A — 구현 단위 A-1(Plan S-9).
//
// 왜 인터페이스인가(초급자용 설명):
//   생산(UnitProductionUseCase)은 「배율을 어디서 계산하는가」를 몰라도 된다. 지금 구현은 하나다:
//     - 서버 · 싱글: SupportSkillEffectSystem(전술 지령이 건 팀 생산 배율을 서버 권위로 보관 · 만료).
//   순수 클라이언트의 생산 막대 거울은 UnitProductionUseCase 안의 ProductionSpeedMirror가 따로 맡는다(RPC 없음).
//   생산 쪽은 이 인터페이스만 보므로, 서비스가 없거나(null) 효과가 없으면 배율 1 → 지금과 똑같이 동작한다.
//
// Application 레이어 — Domain(TeamId) 의존.
// ============================================================================

using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 팀 생산 속도 배율 조회 창구. 효과가 없으면 1을 돌려준다.
    /// </summary>
    public interface IProductionSpeedSource
    {
        /// <summary>
        /// 지정 팀의 지금 생산 속도 배율(1 = 보통, 1.5 = 1.5배 빠름). 효과가 없으면 정확히 1.
        /// </summary>
        /// <param name="team">생산 건물의 팀.</param>
        float GetProductionSpeedMultiplier(TeamId team);

        /// <summary>
        /// (진단용) 지금 그 팀에 생산 속도를 건 발동 순번. 효과가 없으면 0. 판정에는 쓰지 않는다.
        /// </summary>
        /// <param name="team">생산 건물의 팀.</param>
        int GetProductionSpeedSequence(TeamId team);
    }
}
