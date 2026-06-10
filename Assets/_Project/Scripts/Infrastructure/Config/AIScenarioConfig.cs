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
// Infrastructure 레이어 — Unity 의존 허용 (ScriptableObject).
//   (GameSystemRules_AI.md 규칙 11, 29)
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 빌드오더 한 항목의 행동 종류.
    /// </summary>
    public enum AIActionType
    {
        /// <summary>새 건물 배치 (buildingType 사용).</summary>
        PlaceBuilding,

        /// <summary>기존 건물 업그레이드 (buildingType = 업그레이드 후 단계 타입).</summary>
        UpgradeBuilding,

        /// <summary>유닛 생산 시작 (unitType 사용). 콜백 기반 연속 생산 시드.</summary>
        StartProduction
    }

    /// <summary>
    /// 종족 단위 에셋(Human / Spirit / Transcendence)에 담기는 단일 시나리오 묶음.
    /// AIScenarioConfig.scenarios 리스트의 원소로 사용된다.
    /// </summary>
    [System.Serializable]
    public class ScenarioBundle
    {
        /// <summary>이 시나리오의 식별 이름(예: Human_A). 디버깅·로깅용.</summary>
        [Tooltip("이 시나리오의 식별 이름(예: Human_A). 디버깅·로깅용.")]
        public string scenarioName = "Unnamed";

        /// <summary>빌드오더 항목 목록. 여러 Phase의 항목을 하나의 평탄 리스트로 보관.</summary>
        [Tooltip("빌드오더 항목 목록. 여러 Phase의 항목을 하나의 평탄 리스트로 보관.")]
        public List<BuildOrderStep> steps = new List<BuildOrderStep>();
    }

    /// <summary>
    /// 빌드오더 한 단계(Step)의 직렬화 데이터. (GameSystemRules_AI.md 규칙 9)
    ///
    /// [System.Serializable] 속성이 있어야 Inspector 리스트에 펼쳐져 노출된다.
    /// </summary>
    [System.Serializable]
    public struct BuildOrderStep
    {
        [Tooltip("행동 종류: 건물 배치 / 건물 업그레이드 / 유닛 생산 시작")]
        public AIActionType actionType;

        [Tooltip("대상 건물 타입 (PlaceBuilding / UpgradeBuilding 시 사용). " +
                 "UpgradeBuilding이면 업그레이드 후의 다음 단계 타입을 지정.")]
        public BuildingType buildingType;

        [Tooltip("생산 유닛 타입 (StartProduction 시 사용)")]
        public UnitType unitType;

        [Tooltip("대상 건물 라인 인덱스. 0=근거리A, 1=총기류, 2=탈것류. " +
                 "같은 라인의 PlaceBuilding이 만든 배럭에 이후 항목이 연결된다.")]
        public int targetBuildingLine;

        [Tooltip("속하는 Phase 번호. 0=Phase1(초반), 1=Phase2(중반), 2=Phase3(중후반), 3=Phase4(후반)")]
        public int phaseIndex;

        [Header("난이도별 대기 시간 (Phase 시작 후 이 항목까지의 초)")]

        [Tooltip("쉬움 난이도에서 이 항목 실행까지의 대기 시간(초)")]
        public float delaySecondsEasy;

        [Tooltip("보통 난이도에서 이 항목 실행까지의 대기 시간(초)")]
        public float delaySecondsNormal;

        [Tooltip("어려움 난이도에서 이 항목 실행까지의 대기 시간(초)")]
        public float delaySecondsHard;

        /// <summary>
        /// 난이도에 해당하는 대기 시간(초)을 반환한다.
        /// AIOpponentController가 현재 난이도에 맞는 타이머 길이를 구할 때 사용.
        /// </summary>
        /// <param name="level">현재 AI 난이도.</param>
        /// <returns>해당 난이도의 delaySeconds 값.</returns>
        public float GetDelaySeconds(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.Easy: return delaySecondsEasy;
                case DifficultyLevel.Hard: return delaySecondsHard;
                default: return delaySecondsNormal;
            }
        }
    }

    /// <summary>
    /// 한 종족(시나리오)의 빌드오더 전체를 담는 ScriptableObject.
    /// 에셋 경로(권장): Assets/_Project/Resources/Config/AIScenarioConfig_Human_A.asset 등
    /// </summary>
    [CreateAssetMenu(fileName = "AIScenarioConfig", menuName = "Hexiege/AIScenarioConfig")]
    public class AIScenarioConfig : ScriptableObject
    {
        // ---- 레거시 필드 (Human_A/B/C.asset 호환용 — 해당 에셋 삭제 후 제거 가능) ----
        [Tooltip("이 시나리오를 식별하기 위한 이름(예: Human_A). 디버깅/로깅용.")]
        public string scenarioName = "Unnamed";

        [Tooltip("빌드오더 항목 목록. 여러 Phase의 항목을 하나의 평탄 리스트로 보관. " +
                 "리스트 순서 = 같은 Phase 내 실행 순서.")]
        [SerializeField] private List<BuildOrderStep> _steps = new List<BuildOrderStep>();

        /// <summary> 외부에서 읽기 전용으로 빌드오더 항목 목록에 접근할 때 사용. </summary>
        public IReadOnlyList<BuildOrderStep> Steps => _steps;

        // ---- 신규: 종족 단위 에셋(Human / Spirit / Transcendence)에서 사용 ----

        [Tooltip("종족 에셋이 담는 3개 시나리오 배열. 게임 시작 시 무작위로 하나를 선택한다. " +
                 "Human_A/B/C 같은 레거시 에셋은 이 필드를 비워두고 _steps를 사용한다.")]
        public List<ScenarioBundle> scenarios = new List<ScenarioBundle>();
    }
}
