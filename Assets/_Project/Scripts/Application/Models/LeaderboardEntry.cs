// ============================================================================
// LeaderboardEntry.cs
// 랭킹 테이블 한 행(플레이어 1명)의 데이터 전달용 순수 C# 클래스(DTO).
//
// 역할:
//   - UGS Leaderboard 에서 조회한 순위/닉네임/전적을 담아 상위 레이어로 전달한다.
//   - Infrastructure(LeaderboardService) → Application(RankingUseCase) → Presentation
//     흐름에서 사용되는 단순 데이터 컨테이너다.
//
// 이름 주의:
//   UGS SDK 에도 동일한 이름의 타입(Unity.Services.Leaderboards.Models.LeaderboardEntry)이
//   존재한다. 본 클래스는 우리 도메인용 DTO 이며 SDK 타입과는 별개다.
//   LeaderboardService 내부에서 SDK 타입을 우리 DTO 로 변환한다.
//
// 위치:
//   Application 레이어. 인터페이스(ILeaderboardService)의 반환 타입이므로
//   Application 계층에 선언한다(Infrastructure 역참조 방지).
//
// 순수 C# — UnityEngine 의존 없음.
// ============================================================================

namespace Hexiege.Application
{
    /// <summary>
    /// 랭킹 1행을 나타내는 데이터 클래스.
    /// </summary>
    public class LeaderboardEntry
    {
        /// <summary>순위(1위부터 시작). 표시용으로 이미 1-base 로 보정된 값이다.</summary>
        public int Rank;

        /// <summary>UGS PlayerId. 내 행 강조 등에 사용할 수 있다.</summary>
        public string PlayerId;

        /// <summary>닉네임(표시 이름).</summary>
        public string Nickname;

        /// <summary>닉네임 뒤에 붙는 4자리 숫자 코드.</summary>
        public string NicknameCode;

        /// <summary>총 게임 수.</summary>
        public int TotalGames;

        /// <summary>승리 횟수.</summary>
        public int Wins;

        /// <summary>패배 횟수.</summary>
        public int Losses;

        /// <summary>승률(%). 정렬 기본 기준이며 Leaderboard 점수(score)로 저장된다.</summary>
        public float WinRate;

        /// <summary>
        /// "닉네임#코드" 형식의 표시 문자열(예: 전사#4729).
        /// 코드가 비어 있으면 닉네임만 반환한다.
        /// </summary>
        public string DisplayName =>
            string.IsNullOrEmpty(NicknameCode) ? Nickname : $"{Nickname}#{NicknameCode}";
    }
}
