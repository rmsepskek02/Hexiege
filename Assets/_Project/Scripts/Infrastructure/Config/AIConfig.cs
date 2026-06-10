// ============================================================================
// AIConfig.cs
// 싱글플레이 AI(Red 팀)의 난이도별 파라미터를 Inspector에서 편집하는 ScriptableObject.
//
// ScriptableObject란?
//   Unity의 데이터 컨테이너 에셋. 씬에 붙이지 않고 .asset 파일로 저장되며
//   Inspector에서 값을 편집할 수 있다. 코드 재컴파일 없이 수치를 조정할 수 있어
//   밸런싱에 유용하다.
//
// 이 에셋의 역할:
//   쉬움(easy)/보통(normal)/어려움(hard) 3개 난이도의 AI 파라미터를
//   하나의 .asset 파일에 담는다. UnitStatsConfig가 9개 유닛 스탯을 하나에 담는 것과 동일 패턴.
//   인스펙터에서 세 난이도의 수치를 한 곳에서 비교하며 수정할 수 있다.
//
// 생성 방법:
//   메뉴 `Hexiege/Setup/AIConfig 생성` 실행 시
//   `Assets/_Project/Resources/Config/AIConfig.asset`으로 기본값과 함께 생성된다.
//
// 사용 흐름:
//   GameBootstrapper.InitializeAI()에서 Resources.Load로 이 에셋을 읽고,
//   LocalPlayerDifficulty.Current에 따라 GetParams()로 해당 DifficultyParams를 골라
//   AIOpponentController에 주입한다.
//
// Infrastructure 레이어 — Unity 의존 허용 (ScriptableObject).
//   (GameSystemRules_AI.md 규칙 4~5, 29)
// ============================================================================

using UnityEngine;
using Hexiege.Domain; // DifficultyLevel(enum)을 Domain 레이어에서 가져오기 위함.

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// AI 한 난이도의 동작 파라미터 묶음. (GameSystemRules_AI.md 규칙 5)
    /// AIConfig가 easy/normal/hard 세 개를 필드로 가진다.
    ///
    /// [System.Serializable] 속성이 있어야 Inspector에 펼쳐져 노출된다.
    /// </summary>
    [System.Serializable]
    public struct DifficultyParams
    {
        [Header("자원/생산 배율")]

        [Tooltip("AI 채굴소 수입에 곱하는 배율. 플레이어는 항상 1.0. (쉬움 0.7 / 보통 1.0 / 어려움 1.3)")]
        public float goldIncomeMultiplier;

        [Tooltip("AI 유닛 생산 시간에 곱하는 배율. 클수록 생산이 느려짐. (쉬움 1.3 / 보통 1.0 / 어려움 0.7)")]
        public float productionTimeMultiplier;

        [Header("반응 시스템")]

        [Tooltip("반응 시스템이 게임 상태를 점검하는 주기(초). (쉬움 20 / 보통 10 / 어려움 5)")]
        public float decisionInterval;

        [Tooltip("규칙 R1 — AI 유닛 수가 (플레이어 유닛 수 × 이 값) 이하일 때 긴급 보충. (쉬움 0.7 / 보통 1.0 / 어려움 1.3)")]
        public float unitCountThreshold;

        [Tooltip("규칙 R2 — AI 골드가 이 값 이상일 때 빌드오더 가속. (쉬움 300 / 보통 200 / 어려움 150)")]
        public float goldExcessThreshold;

        [Header("쿨다운/간격 (난이도 무관 동일값)")]

        [Tooltip("규칙 R2 빌드오더 가속 쿨다운(초). 기본 20.")]
        public float buildOrderAccelCooldown;

        [Tooltip("규칙 R1 생산 활성화 쿨다운(초). 기본 15.")]
        public float productionActivationCooldown;

        [Tooltip("빌드오더 단계 실패 시 재시도 간격(초). 기본 10.")]
        public float retryInterval;

        [Tooltip("MiningPost 존재 여부 탐지 및 재건 시도 주기(초). 기본 20.")]
        public float mineCheckInterval;
    }

    /// <summary>
    /// 3개 난이도의 AI 파라미터를 담는 ScriptableObject.
    /// 에셋 경로(권장): Assets/_Project/Resources/Config/AIConfig.asset
    /// </summary>
    [CreateAssetMenu(fileName = "AIConfig", menuName = "Hexiege/AIConfig")]
    public class AIConfig : ScriptableObject
    {
        [Header("AI On/Off")]

        // [AIConfig 이전] 기존 GameBootstrapper._enableAI(SerializeField)를 이 필드로 대체한다.
        //   장점: Game.unity 씬을 열지 않아도 Project 창에서 AIConfig.asset만 선택해
        //         즉시 AI를 켜고 끌 수 있다. 테스트 편의성 향상.
        //   사용처: GameBootstrapper.InitializeAI()가 AIConfig를 로드한 직후 이 값을 점검하여,
        //          false이면 AI 컨트롤러를 만들지 않고 조기 반환한다.
        [Tooltip("AI 활성화 여부. false로 끄면 싱글플레이에서도 AI가 동작하지 않는다. (테스트용 토글)")]
        public bool enableAI = true;

        [Header("난이도별 파라미터")]

        [Tooltip("쉬움 난이도 파라미터")]
        public DifficultyParams easy;

        [Tooltip("보통 난이도 파라미터")]
        public DifficultyParams normal;

        [Tooltip("어려움 난이도 파라미터")]
        public DifficultyParams hard;

        /// <summary>
        /// 난이도 단계에 해당하는 DifficultyParams를 반환한다.
        /// GameBootstrapper.InitializeAI()에서 LocalPlayerDifficulty.Current를 인자로 호출.
        /// </summary>
        /// <param name="level">난이도 단계.</param>
        /// <returns>해당 난이도의 파라미터. 알 수 없는 값이면 normal을 반환.</returns>
        public DifficultyParams GetParams(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.Easy: return easy;
                case DifficultyLevel.Hard: return hard;
                default: return normal; // Normal 및 예외적 미정의 값
            }
        }
    }
}
