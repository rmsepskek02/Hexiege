// ============================================================================
// LocalPlayerDifficulty.cs
// 로비에서 선택한 싱글플레이 AI 난이도를 게임 씬으로 전달하기 위한 정적 홀더.
//
// 역할:
//   - 로비 난이도 선택 UI(DifficultySelectView)에서 난이도가 결정되면 여기에 저장
//   - 게임 씬 진입 시 GameBootstrapper.InitializeAI()가 Current를 읽어
//     해당 난이도의 AIConfig 파라미터를 AIOpponentController에 주입
//
// 왜 정적 홀더인가?
//   로비 씬(Lobby.unity)과 게임 씬(Game.unity)은 분리되어 있어, 씬 전환 시
//   일반 인스턴스 참조는 사라진다. LocalPlayerRace와 동일하게 static 클래스로 두어
//   씬 전환 후에도 값이 유지되도록 한다.
//
// 사용 예시:
//   // 난이도 설정 (DifficultySelectView 버튼 클릭 → BattleViewModel.CmdSelectDifficulty)
//   LocalPlayerDifficulty.Set(DifficultyLevel.Hard);
//
//   // 난이도 조회 (GameBootstrapper.InitializeAI에서)
//   var diff = LocalPlayerDifficulty.Current;
//
// 타입 이동 안내:
//   DifficultyLevel(enum)은 Domain 레이어(Domain/AI/DifficultyLevel.cs)로 이동되었다.
//   - 난이도는 게임 규칙의 핵심 개념이라 Unity에 의존하지 않는 Domain에 두는 것이
//     의존 방향(Domain ← Infrastructure)에 맞다.
//   - 본 파일은 상단의 `using Hexiege.Domain;`을 통해 그 타입을 그대로 참조한다.
//
// Infrastructure 레이어 — 로비 난이도 선택 전역 접근점.
//   (GameSystemRules_AI.md 규칙 32)
// ============================================================================

using Hexiege.Domain; // DifficultyLevel(enum)을 Domain 레이어에서 가져오기 위함.

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 현재 로컬 플레이어가 선택한 AI 난이도를 전역에서 접근 가능하게 하는 정적 홀더.
    /// DifficultySelectView에서 난이도를 선택하면 Set()을 호출해 갱신.
    /// 기본값: Normal(보통).
    /// </summary>
    public static class LocalPlayerDifficulty
    {
        // ====================================================================
        // 상태
        // ====================================================================

        /// <summary>현재 선택된 AI 난이도. 기본값은 Normal(보통).</summary>
        public static DifficultyLevel Current { get; private set; } = DifficultyLevel.Normal;

        // ====================================================================
        // API
        // ====================================================================

        /// <summary>
        /// AI 난이도를 설정.
        /// 로비 DifficultySelectView에서 난이도 버튼을 누르면 호출.
        /// </summary>
        /// <param name="level">선택된 난이도.</param>
        public static void Set(DifficultyLevel level)
        {
            Current = level;
        }

        /// <summary>
        /// 난이도를 기본값(Normal)으로 초기화.
        /// 로비 복귀 등에서 필요 시 호출.
        /// </summary>
        public static void Reset()
        {
            Current = DifficultyLevel.Normal;
        }
    }
}
