// ============================================================================
// LocalPlayerTeam.cs
// 현재 로컬 플레이어의 팀 정보를 전역으로 접근하기 위한 정적 홀더.
//
// 역할:
//   - TeamAssigner가 네트워크에서 팀을 받아오면 여기에 저장
//   - 게임 내 어느 시스템이든 LocalPlayerTeam.Current로 로컬 팀 조회 가능
//   - 싱글플레이 기본값은 Blue 팀
//
// 사용 예시:
//   // 팀 설정 (TeamAssigner에서 호출)
//   LocalPlayerTeam.Set(TeamId.Red);
//
//   // 팀 조회 (UI, 입력 처리 등에서)
//   if (LocalPlayerTeam.Current == TeamId.Blue) { ... }
//
// Infrastructure 레이어 — 네트워크 팀 정보 전역 접근점.
// ============================================================================

using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 현재 로컬 플레이어의 팀을 전역에서 접근 가능하게 하는 정적 홀더.
    /// TeamAssigner가 서버로부터 팀을 받으면 Set()을 호출해 갱신.
    /// 싱글플레이 기본값: Blue 팀.
    /// </summary>
    public static class LocalPlayerTeam
    {
        // ====================================================================
        // 상태
        // ====================================================================

        /// <summary>현재 로컬 플레이어의 팀. 기본값은 Blue(싱글플레이 호환).</summary>
        public static TeamId Current { get; private set; } = TeamId.Blue;

        /// <summary>TeamAssigner가 실제로 팀을 할당했는지 여부. 타임아웃 감지용.</summary>
        public static bool IsAssigned { get; private set; } = false;

        // ====================================================================
        // API
        // ====================================================================

        /// <summary>
        /// 로컬 플레이어 팀을 설정.
        /// TeamAssigner.OnNetworkSpawn() 또는 OnTeamAssigned 이벤트에서 호출.
        /// </summary>
        /// <param name="team">할당된 팀.</param>
        public static void Set(TeamId team)
        {
            Current = team;
            IsAssigned = true;
        }

        /// <summary>
        /// 팀을 기본값(Blue)으로 초기화.
        /// 씬 전환이나 연결 해제 시 호출.
        /// </summary>
        public static void Reset()
        {
            Current = TeamId.Blue;
            IsAssigned = false;
        }
    }
}
