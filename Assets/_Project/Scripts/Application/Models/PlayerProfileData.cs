// ============================================================================
// PlayerProfileData.cs
// 플레이어 프로필 데이터 전달용 순수 C# 클래스(DTO).
//
// 역할:
//   - UGS Cloud Save 에서 읽어온 닉네임/전적 데이터를 담아 상위 레이어로 전달한다.
//   - Infrastructure(PlayerProfileService) → Application(UseCase) → Presentation(View)
//     로 값이 흐르는 동안 사용되는 단순 데이터 컨테이너다.
//
// 위치:
//   Application 레이어. 인터페이스(IPlayerProfileService)의 반환 타입이므로
//   Application 계층에 선언한다(Infrastructure 역참조 방지).
//
// 순수 C# — UnityEngine 의존 없음.
// ============================================================================

namespace Hexiege.Application
{
    /// <summary>
    /// 플레이어 프로필 1건을 나타내는 데이터 클래스.
    /// Cloud Save 에 저장된 닉네임/코드/전적 필드를 그대로 담는다.
    /// </summary>
    public class PlayerProfileData
    {
        /// <summary>닉네임(표시 이름). Cloud Save 키: nickname.</summary>
        public string Nickname;

        /// <summary>닉네임 뒤에 붙는 4자리 숫자 코드. Cloud Save 키: nicknameCode.</summary>
        public string NicknameCode;

        /// <summary>총 게임 수(멀티플레이 매치 총 횟수). Cloud Save 키: totalGames.</summary>
        public int TotalGames;

        /// <summary>승리 횟수. Cloud Save 키: wins.</summary>
        public int Wins;

        /// <summary>패배 횟수. Cloud Save 키: losses.</summary>
        public int Losses;

        /// <summary>
        /// 마지막 접속 종료 시각(ISO 8601 문자열). Cloud Save 키: lastSessionEndAt.
        /// 표시 포맷 변환은 상위 View 가 담당한다(예: "2026-06-29 15:30").
        /// </summary>
        public string LastSessionEndAt;

        /// <summary>무료 닉네임 변경을 이미 사용했는지 여부. Cloud Save 키: hasUsedFreeNicknameChange.</summary>
        public bool HasUsedFreeNicknameChange;

        /// <summary>
        /// 닉네임이 설정되어 있는지 여부.
        /// Cloud Save 에 nickname 값이 비어 있으면 "최초 로그인"으로 판정하는 데 사용한다.
        /// </summary>
        public bool HasNickname => !string.IsNullOrWhiteSpace(Nickname);

        /// <summary>
        /// 승률(%). 총 게임 수가 0이면 0을 반환한다.
        /// "-" 표시 여부는 상위 View 가 TotalGames 로 직접 판단한다(값 0과 실제 0% 구분).
        /// </summary>
        public float WinRate => TotalGames > 0 ? (float)Wins / TotalGames * 100f : 0f;

        /// <summary>
        /// "닉네임#코드" 형식의 표시 문자열을 반환한다(예: 전사#4729).
        /// 코드가 비어 있으면 닉네임만 반환한다.
        /// </summary>
        public string DisplayName =>
            string.IsNullOrEmpty(NicknameCode) ? Nickname : $"{Nickname}#{NicknameCode}";
    }
}
