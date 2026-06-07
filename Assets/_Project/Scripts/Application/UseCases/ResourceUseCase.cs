// ============================================================================
// ResourceUseCase.cs
// 팀별 골드(자원)를 관리하는 UseCase.
//
// 역할:
//   - 팀별 골드 보유량 관리
//   - 골드 소비/획득 처리 + 이벤트 발행
//   - 채굴소(MiningPost) 수입 처리 (매 프레임 TickIncome)
//
// 흐름:
//   건물 건설 시: SpendGold(team, cost) → 골드 차감 → OnResourceChanged
//   유닛 생산 시: SpendGold(team, cost) → 골드 차감 → OnResourceChanged
//   채굴소 수입: TickIncome(dt) → 팀별 채굴소 수 × 초당 수입 누적 → AddGold
//
// Application 레이어 — Domain에 의존.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    public class ResourceUseCase
    {
        // 팀별 골드 보유량
        private readonly Dictionary<TeamId, int> _gold;

        // 채굴소 수입 누적용 (float → int 변환 시 소수점 누적)
        private readonly Dictionary<TeamId, float> _incomeAccumulator;

        // 기본 수입 누적용 (채굴소와 별도 관리)
        private readonly Dictionary<TeamId, float> _baseIncomeAccumulator;

        // ====================================================================
        // [AI 시스템] 팀별 채굴소 수입 배율 (GameSystemRules_AI.md 규칙 34)
        // ====================================================================
        // AI(Red 팀)의 난이도에 따라 채굴소 수입을 가감하기 위한 배율.
        //   쉬움 0.7 / 보통 1.0 / 어려움 1.3
        // 플레이어(Blue 팀)는 항상 1.0이며, AI 초기화 시 SetIncomeMultiplier로 Red만 조정한다.
        // 기본값은 1.0(배율 없음)이므로 싱글/멀티 일반 플레이에는 영향이 없다.
        private readonly Dictionary<TeamId, float> _incomeMultipliers;

        public ResourceUseCase(int startingGold)
        {
            _gold = new Dictionary<TeamId, int>
            {
                { TeamId.Blue, startingGold },
                { TeamId.Red, startingGold }
            };
            _incomeAccumulator = new Dictionary<TeamId, float>
            {
                { TeamId.Blue, 0f },
                { TeamId.Red, 0f }
            };
            _baseIncomeAccumulator = new Dictionary<TeamId, float>
            {
                { TeamId.Blue, 0f },
                { TeamId.Red, 0f }
            };
            // 수입 배율 기본값 1.0 (배율 효과 없음). AI 초기화 시 Red만 변경.
            _incomeMultipliers = new Dictionary<TeamId, float>
            {
                { TeamId.Blue, 1.0f },
                { TeamId.Red, 1.0f }
            };
        }

        /// <summary>
        /// 해당 팀의 채굴소 수입 배율을 설정한다. (GameSystemRules_AI.md 규칙 34)
        /// GameBootstrapper.InitializeAI()에서 게임 시작 시 1회 호출하여
        /// AI(Red 팀)의 난이도별 골드 수입 배율을 적용한다.
        /// 플레이어(Blue 팀)는 항상 1.0을 유지한다.
        /// </summary>
        /// <param name="team">배율을 적용할 팀.</param>
        /// <param name="multiplier">채굴소 수입에 곱할 배율(예: 쉬움 0.7, 어려움 1.3).</param>
        public void SetIncomeMultiplier(TeamId team, float multiplier)
        {
            _incomeMultipliers[team] = multiplier;
        }

        /// <summary> 해당 팀의 현재 골드. </summary>
        public int GetGold(TeamId team)
        {
            return _gold.TryGetValue(team, out int gold) ? gold : 0;
        }

        /// <summary> 해당 팀이 비용을 지불할 수 있는지 확인. </summary>
        public bool CanAfford(TeamId team, int cost)
        {
            return GetGold(team) >= cost;
        }

        /// <summary>
        /// 골드 차감. 성공 시 이벤트 발행.
        /// 잔고 부족 시 false 반환.
        /// </summary>
        public bool SpendGold(TeamId team, int cost)
        {
            if (!CanAfford(team, cost)) return false;

            _gold[team] -= cost;
            GameEvents.OnResourceChanged.OnNext(new ResourceChangedEvent(team, _gold[team]));
            return true;
        }

        /// <summary>
        /// 골드 추가. 수입, 보상 등에서 호출.
        /// </summary>
        public void AddGold(TeamId team, int amount)
        {
            if (!_gold.ContainsKey(team)) return;

            _gold[team] += amount;
            GameEvents.OnResourceChanged.OnNext(new ResourceChangedEvent(team, _gold[team]));
        }

        /// <summary>
        /// 수입 처리. 매 프레임 ProductionTicker에서 호출.
        /// 1) 기본 수입: 매 팀 baseGoldPerSecond × deltaTime 누적.
        /// 2) 채굴소 수입: 팀별 채굴소 수 × miningGoldPerSecond × deltaTime 누적.
        /// float 누적 후 정수 단위가 되면 골드에 반영.
        /// </summary>
        public void TickIncome(float deltaTime, BuildingPlacementUseCase buildingPlacement,
            float miningGoldPerSecond, float baseGoldPerSecond)
        {
            // 기본 수입 처리 (채굴소 없이도 매 초 수급)
            TickBaseIncome(TeamId.Blue, deltaTime, baseGoldPerSecond);
            TickBaseIncome(TeamId.Red, deltaTime, baseGoldPerSecond);

            if (buildingPlacement == null) return;

            // 각 팀별 채굴소 수입 처리
            TickTeamIncome(TeamId.Blue, deltaTime, buildingPlacement, miningGoldPerSecond);
            TickTeamIncome(TeamId.Red, deltaTime, buildingPlacement, miningGoldPerSecond);
        }

        private void TickBaseIncome(TeamId team, float deltaTime, float baseGoldPerSecond)
        {
            if (baseGoldPerSecond <= 0f) return;

            _baseIncomeAccumulator[team] += baseGoldPerSecond * deltaTime;

            int wholeGold = (int)_baseIncomeAccumulator[team];
            if (wholeGold > 0)
            {
                _baseIncomeAccumulator[team] -= wholeGold;
                AddGold(team, wholeGold);
            }
        }

        private void TickTeamIncome(TeamId team, float deltaTime,
            BuildingPlacementUseCase buildingPlacement, float goldPerSecond)
        {
            // 해당 팀의 살아있는 채굴소 수 세기
            int miningCount = 0;
            foreach (var kvp in buildingPlacement.Buildings)
            {
                if (kvp.Value.Team == team
                    && kvp.Value.Type == BuildingType.MiningPost
                    && kvp.Value.IsAlive)
                {
                    miningCount++;
                }
            }

            if (miningCount == 0) return;

            // [AI 시스템] 팀별 수입 배율 적용 (규칙 34).
            // Blue(플레이어)는 항상 1.0이므로 영향이 없고, Red(AI)만 난이도 배율이 곱해진다.
            float multiplier = _incomeMultipliers.TryGetValue(team, out float m) ? m : 1.0f;

            // 소수점 누적 후 정수 골드 반영
            _incomeAccumulator[team] += miningCount * goldPerSecond * deltaTime * multiplier;

            int wholeGold = (int)_incomeAccumulator[team];
            if (wholeGold > 0)
            {
                _incomeAccumulator[team] -= wholeGold;
                AddGold(team, wholeGold);
            }
        }
    }
}
