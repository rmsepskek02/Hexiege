// ============================================================================
// ILeaderboardService.cs
// 랭킹(리더보드) 조회 추상화 인터페이스.
//
// 이 인터페이스가 필요한 이유:
//   실제 구현(LeaderboardService)은 UGS Leaderboards 를 사용하는 Infrastructure
//   레이어 클래스다. Application 레이어의 RankingUseCase 가 그 구체 클래스를 직접
//   참조하면 Application → Infrastructure 역방향 의존이 생긴다(MEMORY.md 제약).
//   따라서 UseCase 는 이 인터페이스에만 의존하고, Infrastructure 가 구현한다.
//
// Application 레이어 — 순수 C# + Task.
// ============================================================================

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hexiege.Application
{
    /// <summary>
    /// 랭킹 데이터를 조회하는 저장소 추상화.
    /// Infrastructure 의 LeaderboardService(UGS Leaderboards)가 이 인터페이스를 구현한다.
    /// </summary>
    public interface ILeaderboardService
    {
        /// <summary>
        /// 상위 랭킹 목록을 조회한다(승률 내림차순, 상위 limit 명).
        /// 조회 실패 시 빈 목록을 반환한다(null 아님).
        /// </summary>
        /// <param name="limit">가져올 최대 인원 수(예: 100).</param>
        Task<List<LeaderboardEntry>> GetTopRankingsAsync(int limit);

        /// <summary>
        /// 현재 로그인된 플레이어의 순위를 조회한다.
        /// 랭킹에 등재되지 않은 경우(전적 부족 등) -1 을 반환한다.
        /// </summary>
        Task<int> GetPlayerRankAsync();
    }
}
