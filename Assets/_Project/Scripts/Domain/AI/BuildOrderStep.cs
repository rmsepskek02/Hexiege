// ============================================================================
// BuildOrderStep.cs
// AI 빌드오더(사전 정의된 건설/생산 시간표)의 "한 단계(Step)"를 표현하는 데이터.
//
// 빌드오더 한 단계란?
//   "Phase 시작 후 N초가 지나면 어떤 건물을 짓는다 / 업그레이드한다 / 유닛 생산을
//   시작한다"는 하나의 행동 단위. 여러 BuildOrderStep이 모여 하나의 시나리오가 된다.
//
// 이 파일이 Domain 레이어에 위치하는 이유:
//   빌드오더 단계는 "AI가 무엇을 할지"라는 게임 규칙 자체이며, 특정 Unity 기술
//   (ScriptableObject 등)에 의존하지 않는 순수한 개념이다.
//   - Application 레이어(AIOpponentController)가 이 타입을 직접 다루며,
//   - Infrastructure 레이어(AIScenarioConfig)가 이 타입을 리스트로 담는다.
//   따라서 의존 방향(Domain ← Application ← Infrastructure)을 지키려면
//   Domain에 두어야 양쪽이 안전하게 참조할 수 있다.
//
// Unity 의존 제거:
//   Domain 레이어는 UnityEngine을 참조하지 않는다. 따라서 원래 Infrastructure
//   버전에 있던 [Tooltip], [Header] 같은 UnityEngine 속성은 모두 제거했다.
//   단, Inspector 리스트에 직렬화되어 노출되어야 하므로 [Serializable]만 유지한다.
//   ([Serializable]은 System 네임스페이스 소속이라 Unity 의존이 아니다.)
// ============================================================================

using System; // [Serializable] 속성을 사용하기 위함 (System.Serializable).

namespace Hexiege.Domain
{
    /// <summary>
    /// 빌드오더 한 단계의 행동 종류.
    /// 건물 배치 / 건물 업그레이드 / 유닛 생산 시작 중 하나를 가리킨다.
    /// </summary>
    public enum AIActionType
    {
        /// <summary>새 건물 배치 (buildingType 사용).</summary>
        PlaceBuilding,

        /// <summary>기존 건물 업그레이드 (buildingType = 업그레이드 후 단계 타입).</summary>
        UpgradeBuilding,

        /// <summary>유닛 생산 시작 (unitType 사용). 콜백 기반 연속 생산 시드.</summary>
        StartProduction,

        /// <summary>
        /// 연구소 강화 착수 (upgradeGroup / upgradeStat 사용).
        /// AI가 보유한 연구소(BuildingType.Research)에서 지정 트랙을 1레벨 연구한다.
        /// (연구는 특정 연구소 종속이 아니라 팀 트랙 단위 — 아무 연구소나 있으면 착수 가능.)
        /// </summary>
        StartResearch
    }

    /// <summary>
    /// 빌드오더 한 단계(Step)의 직렬화 데이터.
    ///
    /// [Serializable] 속성이 있어야 Inspector 리스트(AIScenarioConfig의 steps)에
    /// 펼쳐져 노출되고, .asset 파일로 저장될 수 있다.
    /// </summary>
    [Serializable]
    public struct BuildOrderStep
    {
        // 행동 종류: 건물 배치 / 건물 업그레이드 / 유닛 생산 시작 중 하나.
        public AIActionType actionType;

        // 대상 건물 타입 (PlaceBuilding / UpgradeBuilding 시 사용).
        // UpgradeBuilding이면 업그레이드 후의 "다음 단계" 타입을 지정한다.
        public BuildingType buildingType;

        // 생산할 유닛 타입 (StartProduction 시 사용).
        public UnitType unitType;

        // 연구할 강화 그룹 (StartResearch 시 사용). Regen 스탯이면 그룹은 무시된다.
        public UpgradeGroup upgradeGroup;

        // 연구할 강화 스탯 (StartResearch 시 사용). Attack/Defense/MoveSpeed/Regen.
        public UnitUpgradeStat upgradeStat;

        // 대상 건물 라인 인덱스. 0=근거리A, 1=총기류, 2=탈것류.
        // 같은 라인의 PlaceBuilding이 만든 배럭(생산 건물)에 이후 항목이 연결된다.
        public int targetBuildingLine;

        // 이 단계가 속하는 Phase 번호.
        // 0=Phase1(초반), 1=Phase2(중반), 2=Phase3(중후반), 3=Phase4(후반).
        public int phaseIndex;

        // 쉬움 난이도에서 이 단계 실행까지의 대기 시간(초). Phase 시작 기준 누적 시간.
        public float delaySecondsEasy;

        // 보통 난이도에서 이 단계 실행까지의 대기 시간(초).
        public float delaySecondsNormal;

        // 어려움 난이도에서 이 단계 실행까지의 대기 시간(초).
        public float delaySecondsHard;

        /// <summary>
        /// 현재 난이도에 해당하는 대기 시간(초)을 반환한다.
        /// AIOpponentController가 현재 난이도에 맞는 타이머 길이를 구할 때 사용한다.
        /// (예: 어려움 난이도면 delaySecondsHard를 돌려줘 더 빨리 행동하게 한다.)
        /// </summary>
        /// <param name="level">현재 AI 난이도(Easy / Normal / Hard).</param>
        /// <returns>해당 난이도의 대기 시간 값(초). 미정의 값이면 보통(Normal) 값을 반환.</returns>
        public float GetDelaySeconds(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.Easy: return delaySecondsEasy;
                case DifficultyLevel.Hard: return delaySecondsHard;
                default: return delaySecondsNormal; // Normal 및 예외적 미정의 값
            }
        }
    }
}
