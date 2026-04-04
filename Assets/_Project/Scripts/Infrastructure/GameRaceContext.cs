// ============================================================================
// GameRaceContext.cs
// 양 팀(Blue/Red)의 종족 정보를 전역으로 접근하기 위한 정적 홀더.
//
// 역할:
//   - 멀티플레이: StartGameClientRpc에서 서버가 전달한 양 팀 종족 정보를 수신하여 저장
//   - 싱글플레이: GameBootstrapper에서 LocalPlayerRace.Current를 Blue로 설정
//   - 인게임에서 종족별 유닛/건물 풀 초기화 시 이 값을 참조
//
// 사용 예시:
//   // 멀티플레이 수신 시
//   GameRaceContext.Set(RaceId.Spirit, RaceId.Nature);
//
//   // 인게임에서 종족별 유닛 풀 초기화 시
//   RaceId myRace = GameRaceContext.BlueRace;
//
// Infrastructure 레이어 — 게임 시작 시 종족 정보 전역 접근점.
// ============================================================================

using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 양 팀의 종족 정보를 전역에서 접근 가능하게 하는 정적 홀더.
    /// 멀티플레이 시 StartGameClientRpc에서 수신, 싱글플레이 시 GameBootstrapper에서 설정.
    /// </summary>
    public static class GameRaceContext
    {
        // ====================================================================
        // 상태
        // ====================================================================

        /// <summary>Blue 팀의 종족. 기본값 Human(인간).</summary>
        public static RaceId BlueRace { get; private set; } = RaceId.Human;

        /// <summary>Red 팀의 종족. 기본값 Human(인간).</summary>
        public static RaceId RedRace { get; private set; } = RaceId.Human;

        // ====================================================================
        // API
        // ====================================================================

        /// <summary>
        /// 양 팀 종족을 설정.
        /// 멀티플레이 시 StartGameClientRpc에서, 싱글플레이 시 GameBootstrapper에서 호출.
        /// </summary>
        /// <param name="blueRace">Blue 팀 종족.</param>
        /// <param name="redRace">Red 팀 종족.</param>
        public static void Set(RaceId blueRace, RaceId redRace)
        {
            BlueRace = blueRace;
            RedRace = redRace;
        }

        /// <summary>
        /// 양 팀 종족을 기본값(Human)으로 초기화.
        /// 로비 복귀 또는 연결 해제 시 호출.
        /// </summary>
        public static void Reset()
        {
            BlueRace = RaceId.Human;
            RedRace = RaceId.Human;
        }
    }
}
