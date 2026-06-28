// ============================================================================
// AIScenarioConfig.cs
// 한 종족의 AI 빌드오더 시나리오(Phase 1~4 항목 목록)를 담는 ScriptableObject.
//
// 빌드오더 시나리오란?
//   "Phase 시작 후 N초가 지나면 어떤 건물을 짓고/업그레이드하고/유닛 생산을 시작한다"는
//   사전 정의된 시간표. AI는 이 시간표를 순차 실행하며 게임을 진행한다.
//   (GameSystemRules_AI.md 규칙 6~11)
//
// 데이터 구조:
//   여러 Phase의 항목(BuildOrderStep)을 "하나의 평탄(flat) 리스트"로 보관한다.
//   각 항목이 phaseIndex(0~3)를 직접 가지고 있어, AIOpponentController가
//   phaseIndex로 그룹핑하여 Phase 단위로 순차 실행한다.
//
// 종족별 단일 에셋 구조 (신규):
//   각 종족 1개의 .asset에 3개 시나리오를 ScenarioBundle 배열로 담는다.
//     - AIScenarioConfig_Human.asset       (scenarios[0/1/2] = A/B/C)
//     - AIScenarioConfig_Spirit.asset      (scenarios[0/1/2] = Inferno/Torrent/Quake)
//     - AIScenarioConfig_Transcendence.asset (scenarios[0/1/2] = Rush/Flora/Beast)
//   GameBootstrapper가 Random.Range(0,3)으로 하나를 선택해 AI에 주입한다.
//
// 레거시 에셋 (AIScenarioConfig_Human_A/B/C.asset):
//   기존 단일 시나리오 에셋. scenarioName + _steps 필드 사용.
//   신규 Human.asset 테스트 통과 후 삭제 예정.
//
// 타입 이동 안내:
//   AIActionType(enum), BuildOrderStep(struct)은 Domain 레이어
//   (Domain/AI/BuildOrderStep.cs)로 이동되었다.
//   - 이 두 타입은 "AI가 무엇을 할지"라는 게임 규칙 자체이므로 Unity에 의존하지 않는
//     Domain에 두는 것이 의존 방향(Domain ← Application ← Infrastructure)에 맞다.
//   - 본 파일은 상단의 `using Hexiege.Domain;`을 통해 두 타입을 그대로 참조한다.
//     (ScenarioBundle.steps가 Domain의 BuildOrderStep을 리스트로 담는다.)
//
// Infrastructure 레이어 — Unity 의존 허용 (ScriptableObject).
//   (GameSystemRules_AI.md 규칙 11, 29)
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 종족 단위 에셋(Human / Spirit / Transcendence)에 담기는 단일 시나리오 묶음.
    /// AIScenarioConfig.scenarios 리스트의 원소로 사용된다.
    /// </summary>
    [System.Serializable]
    public class ScenarioBundle
    {
        /// <summary>이 시나리오의 식별 이름(예: Human-Rush). 디버깅·로깅용.</summary>
        [Tooltip("이 시나리오의 식별 이름(예: Human-Rush). 디버깅·로깅용.")]
        public string scenarioName = "Unnamed";

        /// <summary>빌드오더 항목 목록. 여러 Phase의 항목을 하나의 평탄 리스트로 보관.</summary>
        [Tooltip("빌드오더 항목 목록. 여러 Phase의 항목을 하나의 평탄 리스트로 보관.")]
        public List<BuildOrderStep> steps = new List<BuildOrderStep>();
    }

    /// <summary>
    /// 한 종족의 AI 빌드오더 시나리오 묶음을 담는 ScriptableObject.
    /// 에셋 경로: Assets/_Project/Resources/Config/AIScenarioConfig_{종족}.asset
    /// </summary>
    [CreateAssetMenu(fileName = "AIScenarioConfig", menuName = "Hexiege/AIScenarioConfig")]
    public class AIScenarioConfig : ScriptableObject
    {
        /// <summary>이 에셋(종족)의 이름. 디버깅/로깅용.</summary>
        [Tooltip("이 에셋(종족)의 이름. 디버깅/로깅용.")]
        public string scenarioName = "Unnamed";

        /// <summary>
        /// 이 종족이 보유한 시나리오 배열(3개). 게임 시작 시 무작위로 하나를 선택한다.
        /// Human: Rush/Tech/Balance / Spirit: Inferno/Torrent/Quake / Transcendence: Rush/Flora/Beast
        /// </summary>
        [Tooltip("종족 에셋이 담는 3개 시나리오 배열. 게임 시작 시 무작위로 하나를 선택한다.")]
        public List<ScenarioBundle> scenarios = new List<ScenarioBundle>();
    }
}
