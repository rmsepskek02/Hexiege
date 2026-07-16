// ============================================================================
// RankingUseCase.cs
// 랭킹 리스트 조회 + 내 순위 조회를 담당하는 Application 레이어 UseCase.
//
// 역할:
//   - 상위 랭킹 목록 조회(UGS Leaderboard)
//   - 내 순위 조회(20판 미만이면 미등재로 간주 → -1)
//
// 의존성:
//   - ILeaderboardService(Application 인터페이스). 구현은 Infrastructure 의
//     LeaderboardService(UGS Leaderboards). 의존성 역전.
//
// Application 레이어 — 순수 C# + Task.
// ============================================================================

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Hexiege.Application
{
    /// <summary>
    /// 랭킹 조회 UseCase.
    /// </summary>
    public class RankingUseCase
    {
        // ====================================================================
        // 상수 (GameSystemRules_UI.md Ranking 탭 규칙 2, 5)
        // ====================================================================

        /// <summary>랭킹에 등재되기 위한 최소 게임 수.</summary>
        public const int MinGamesForRank = 20;

        /// <summary>조회할 최대 인원(상위 100명 = 10페이지 × 10행).</summary>
        public const int MaxRankingCount = 100;

        // ====================================================================
        // 의존성
        // ====================================================================

        private readonly ILeaderboardService _leaderboardService;

        /// <summary>
        /// 생성자. ILeaderboardService 를 주입받는다(Bootstrap/상위가 구현체를 넣어준다).
        /// </summary>
        public RankingUseCase(ILeaderboardService leaderboardService)
        {
            _leaderboardService = leaderboardService;
        }

        // ====================================================================
        // 조회
        // ====================================================================

        /// <summary>
        /// 상위 랭킹 목록(최대 100명, 승률 내림차순)을 조회한다.
        /// 실패 시 빈 목록을 반환한다.
        /// </summary>
        public Task<List<LeaderboardEntry>> GetRankingsAsync()
        {
            return _leaderboardService.GetTopRankingsAsync(MaxRankingCount);
        }

        /// <summary>
        /// 내 순위를 조회한다.
        /// Leaderboard 에는 20판 이상 플레이어만 등재되므로, 미등재(전적 부족 등)이면 -1 을 반환한다.
        /// </summary>
        public Task<int> GetMyRankAsync()
        {
            return _leaderboardService.GetPlayerRankAsync();
        }
    }
}
