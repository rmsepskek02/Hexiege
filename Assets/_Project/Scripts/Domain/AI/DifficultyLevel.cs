// ============================================================================
// DifficultyLevel.cs
// AI 난이도를 나타내는 열거형. Easy / Normal / Hard 3단계.
//
// Domain 레이어에 위치하는 이유:
//   난이도는 게임 규칙의 핵심 개념으로 특정 Unity 기술에 의존하지 않는다.
//   Application 레이어(AIOpponentController)와 Infrastructure 레이어(AIConfig,
//   LocalPlayerDifficulty) 모두 이 타입을 참조하므로, 의존 방향(Domain ← 나머지)을
//   지키려면 Domain에 위치해야 한다.
// ============================================================================

namespace Hexiege.Domain
{
    /// <summary>
    /// AI 난이도 3단계. Easy / Normal / Hard.
    /// AIConfig에서 각 난이도별 파라미터를 꺼낼 때 키로 사용한다.
    /// </summary>
    public enum DifficultyLevel
    {
        Easy,
        Normal,
        Hard
    }
}
