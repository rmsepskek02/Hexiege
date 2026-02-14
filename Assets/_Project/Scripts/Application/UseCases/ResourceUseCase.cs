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
        /// 채굴소 수입 처리. 매 프레임 ProductionTicker에서 호출.
        /// 팀별 채굴소 수 × goldPerSecond × deltaTime 만큼 골드 누적.
        /// float 누적 후 정수 단위가 되면 골드에 반영.
        /// </summary>
        /// <param name="deltaTime">프레임 경과 시간</param>
        /// <param name="buildingPlacement">채굴소 수 조회용</param>
        /// <param name="goldPerSecond">채굴소 1개당 초당 골드 수입</param>
        public void TickIncome(float deltaTime, BuildingPlacementUseCase buildingPlacement, float goldPerSecond)
        {
            if (buildingPlacement == null) return;

            // 각 팀별 채굴소 수입 처리
            TickTeamIncome(TeamId.Blue, deltaTime, buildingPlacement, goldPerSecond);
            TickTeamIncome(TeamId.Red, deltaTime, buildingPlacement, goldPerSecond);
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

            // 소수점 누적 후 정수 골드 반영
            _incomeAccumulator[team] += miningCount * goldPerSecond * deltaTime;

            int wholeGold = (int)_incomeAccumulator[team];
            if (wholeGold > 0)
            {
                _incomeAccumulator[team] -= wholeGold;
                AddGold(team, wholeGold);
            }
        }
    }
}
