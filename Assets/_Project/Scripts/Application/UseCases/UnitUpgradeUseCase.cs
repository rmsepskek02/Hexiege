// ============================================================================
// UnitUpgradeUseCase.cs
// 연구소(Research) 기반 유닛 강화 시스템의 팀별 상태를 관리하는 UseCase.
//
// 핵심 개념 ((B) 방식 — GameSystemRules_Upgrade.md 규칙 4):
//   유닛 스탯을 재계산/재동기화하지 않는다. 대신 "팀별 트랙 레벨"만 여기 보관하고,
//   데미지·이동을 실제로 쓰는 순간에 조회 API로 배율/증가치를 곱해 준다.
//   → 이미 전장에 있는 유닛도 연구 완료 즉시 소급 강화된다.
//
// 이 클래스가 담당하는 것:
//   1) 팀별 트랙 레벨 보관: (팀, 그룹, 스탯) → 레벨(0~5)
//   2) 레벨 → 효과값 변환 조회 API(공격 증가치·이동 배율·방어값·자연회복·힐 스케일)
//   3) 연구 비용/시간 테이블 + 그룹 비용 배율
//   4) 연구 착수/진행 타이머/완료/취소(환불) 라이프사이클
//        - 싱글플레이: GameBootstrapper가 TickResearch로 타이머를 돌린다.
//        - 멀티플레이: 서버(Infrastructure의 NetworkUpgradeController)가 이 UseCase를
//          구동하고, 완료 시 OnResearchCompleted 훅으로 양 클라 브로드캐스트를 트리거한다.
//
// 기존 선례: ResourceUseCase._incomeMultipliers(팀별 배율 상태 레이어).
//
// 아키텍처: Application 레이어. Unity.Netcode를 직접 참조하지 않는다.
//   네트워크 동기화는 Infrastructure가 이 UseCase를 구동/조회하는 방식(의존성 역전).
//   OnResearchCompleted(Action)는 "완료 알림 훅"으로, Infrastructure가 구독해 브로드캐스트한다.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine; // Mathf (Hexiege.Application이 UnityEngine.Application을 가리므로 Application 직접 참조는 피한다)
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 팀별 유닛 강화(연구) 레벨과 진행 상태를 관리하고, 레벨을 실제 효과값으로 변환해 주는 UseCase.
    /// </summary>
    public class UnitUpgradeUseCase
    {
        // 트랙 최대 레벨(Lv0~Lv5).
        private const int MaxLevel = UpgradeGroupHelper.MaxLevel;

        // 표준 트랙 비용/시간 테이블. 인덱스 0 = Lv1로 올릴 때, 인덱스 4 = Lv5로 올릴 때.
        //   (GameSystemRules_Upgrade.md 규칙 10 — 골드 80/120/180/260/360, 시간 15/25/35/50/70초)
        private static readonly int[] StandardCost = { 80, 120, 180, 260, 360 };
        private static readonly float[] StandardTime = { 15f, 25f, 35f, 50f, 70f };

        // 공격력: 레벨당 Round(기본공격력 × 8%)의 고정 정수 등차(BalanceReview §C-1).
        private const float AttackPerLevelRate = 0.08f;
        // 방어력: 레벨당 +8 (0/8/16/24/32/40, BalanceReview §C-2).
        private const int DefensePerLevel = 8;
        // 이동속도: 레벨당 +0.064 배율(1.000~1.320, BalanceReview §C-3).
        private const float MoveSpeedPerLevel = 0.064f;
        // 자연회복: 레벨당 +3 HP/s(0/3/6/9/12/15, BalanceReview §D).
        private const int RegenPerLevel = 3;

        // (팀, 그룹, 스탯) → 현재 레벨. 없으면 0(연구 전).
        private readonly Dictionary<(TeamId team, UpgradeGroup group, UnitUpgradeStat stat), int> _levels
            = new Dictionary<(TeamId, UpgradeGroup, UnitUpgradeStat), int>();

        // 진행 중 연구(트랙 단위 잠금). 같은 (팀, 그룹, 스탯) 트랙은 동시에 하나만 진행 가능.
        private readonly Dictionary<(TeamId team, UpgradeGroup group, UnitUpgradeStat stat), ActiveResearch> _active
            = new Dictionary<(TeamId, UpgradeGroup, UnitUpgradeStat), ActiveResearch>();

        // TickResearch/OnLabDestroyed에서 순회 중 컬렉션 변경을 피하기 위한 재사용 버퍼.
        private readonly List<(TeamId, UpgradeGroup, UnitUpgradeStat)> _completedBuffer
            = new List<(TeamId, UpgradeGroup, UnitUpgradeStat)>(4);
        private readonly List<(TeamId, UpgradeGroup, UnitUpgradeStat)> _cancelBuffer
            = new List<(TeamId, UpgradeGroup, UnitUpgradeStat)>(4);

        /// <summary>
        /// 연구 완료 시 호출되는 알림 훅(팀, 그룹, 스탯, 새 레벨).
        /// Infrastructure(NetworkUpgradeController)가 구독하여 완료 레벨을 양 클라에 브로드캐스트한다.
        /// Application이 Netcode를 직접 참조하지 않기 위한 의존성 역전 지점이다.
        /// </summary>
        public event Action<TeamId, UpgradeGroup, UnitUpgradeStat, int> OnResearchCompleted;

        /// <summary>진행 중인 연구 하나의 타이머/비용/소속 연구소 정보.</summary>
        private class ActiveResearch
        {
            /// <summary>전체 연구 시간(초).</summary>
            public float Total;
            /// <summary>남은 연구 시간(초). 0 이하가 되면 완료.</summary>
            public float Remaining;
            /// <summary>투입한 골드(취소 시 100% 환불 대상).</summary>
            public int Cost;
            /// <summary>연구를 착수한 연구소 건물 Id(파괴 시 이 연구를 취소·환불).</summary>
            public int BuildingId;
        }

        // ====================================================================
        // 키 정규화 — 자연회복(Regen)은 그룹 무관 초월 공용 트랙이라 그룹 키를 하나로 고정
        // ====================================================================
        private static (TeamId, UpgradeGroup, UnitUpgradeStat) Key(
            TeamId team, UpgradeGroup group, UnitUpgradeStat stat)
        {
            UpgradeGroup g = stat == UnitUpgradeStat.Regen
                ? UpgradeGroupHelper.RegenCanonicalGroup
                : group;
            return (team, g, stat);
        }

        // ====================================================================
        // 레벨 조회
        // ====================================================================

        /// <summary> (팀, 그룹, 스탯) 트랙의 현재 레벨(0~5). </summary>
        public int GetLevel(TeamId team, UpgradeGroup group, UnitUpgradeStat stat)
            => _levels.TryGetValue(Key(team, group, stat), out int lv) ? lv : 0;

        /// <summary> 특정 유닛이 속한 그룹의 (스탯) 트랙 레벨. </summary>
        public int GetLevelForUnit(TeamId team, UnitType type, UnitUpgradeStat stat)
            => GetLevel(team, UpgradeGroupHelper.GetGroup(type), stat);

        // ====================================================================
        // 효과값 조회 — (B) 실시간 배율/증가치
        // ====================================================================

        /// <summary>
        /// 유닛의 공격력 연구 증가치(고정 정수). = Round(기본공격력 × 8%) × 그룹 공격 레벨.
        /// 레벨 0이면 0. 유닛마다 증가폭이 다르다(유닛별 고정 정수 등차).
        /// </summary>
        public int GetAttackBonus(TeamId team, UnitType type)
        {
            int level = GetLevelForUnit(team, type, UnitUpgradeStat.Attack);
            if (level <= 0) return 0;
            int baseAttack = UnitStats.GetAttackPower(type);
            int perLevel = Mathf.RoundToInt(baseAttack * AttackPerLevelRate);
            return perLevel * level;
        }

        /// <summary> 유닛의 유효 공격력 = 기본 공격력 + 공격 연구 증가치. </summary>
        public int GetEffectiveAttack(TeamId team, UnitType type)
            => UnitStats.GetAttackPower(type) + GetAttackBonus(team, type);

        /// <summary>
        /// 임의의 기본값을 "특정 그룹의 공격 레벨"로 스케일한다(힐량 등 공격력 파생값 전용).
        /// = 기본값 + Round(기본값 × 8%) × 그룹 공격 레벨.
        /// 예) BloomFairy 힐: ScaleByGroupAttack(team, TransPlant, 200) → +16/Lv.
        /// </summary>
        public int ScaleByGroupAttack(TeamId team, UpgradeGroup group, int baseValue)
        {
            if (baseValue <= 0) return baseValue;
            int level = GetLevel(team, group, UnitUpgradeStat.Attack);
            if (level <= 0) return baseValue;
            int perLevel = Mathf.RoundToInt(baseValue * AttackPerLevelRate);
            return baseValue + perLevel * level;
        }

        /// <summary> 유닛의 이동속도 배율(1.000 ~ 1.320). 기본 이동속도에 곱해 실제 속도를 낸다. </summary>
        public float GetMoveSpeedMultiplier(TeamId team, UnitType type)
            => 1f + MoveSpeedPerLevel * GetLevelForUnit(team, type, UnitUpgradeStat.MoveSpeed);

        /// <summary>
        /// 유닛의 방어력(연구 레벨 기반). = 8 × 그룹 방어 레벨(0/8/16/24/32/40).
        /// 데미지 감쇄(DamageCalculator.ApplyDefense)의 defense 인자로 쓰인다.
        /// 규칙 5: 방어력은 "피격 대상 팀"의 연구 레벨로 결정된다.
        /// </summary>
        public int GetDefense(TeamId team, UnitType type)
            => DefensePerLevel * GetLevelForUnit(team, type, UnitUpgradeStat.Defense);

        /// <summary>
        /// 팀의 자연회복량(HP/s). = 3 × 자연회복 레벨(0/3/6/9/12/15). 초월 유닛에만 적용.
        /// 레벨 0이면 0(무동작).
        /// </summary>
        public int GetRegenPerSecond(TeamId team)
            => RegenPerLevel * GetLevel(team, UpgradeGroupHelper.RegenCanonicalGroup, UnitUpgradeStat.Regen);

        // ====================================================================
        // 비용 / 시간 조회
        // ====================================================================

        /// <summary>
        /// 그룹 비용 배율(퍼센트). 초월 동물 200(×2.0), 자연회복 250(×2.5), 그 외(초월 식물 포함) 100(×1.0).
        /// 퍼센트 정수로 다뤄 골드 계산에서 부동소수 오차를 피한다.
        /// </summary>
        public int GetGroupCostMultiplierPercent(UpgradeGroup group, UnitUpgradeStat stat)
        {
            if (stat == UnitUpgradeStat.Regen) return 250;      // 자연회복 ×2.5
            if (group == UpgradeGroup.TransAnimal) return 200;  // 초월 동물 ×2.0
            return 100;                                          // 그 외(초월 식물 포함) ×1.0
        }

        /// <summary>
        /// 다음 레벨로 올리는 데 필요한 골드. 이미 최대 레벨이면 -1.
        /// = 표준 비용(레벨별) × 그룹 배율(%) / 100.
        /// </summary>
        public int GetNextLevelCost(TeamId team, UpgradeGroup group, UnitUpgradeStat stat)
        {
            int lv = GetLevel(team, group, stat);
            if (lv >= MaxLevel) return -1;
            int stdCost = StandardCost[lv]; // 현재 레벨 인덱스 = 다음 레벨 비용
            int mult = GetGroupCostMultiplierPercent(group, stat);
            return stdCost * mult / 100;
        }

        /// <summary>
        /// 다음 레벨로 올리는 데 필요한 연구 시간(초). 이미 최대 레벨이면 -1.
        /// 연구 시간은 그룹 배율과 무관하게 표준(15/25/35/50/70초).
        /// </summary>
        public float GetNextLevelTime(TeamId team, UpgradeGroup group, UnitUpgradeStat stat)
        {
            int lv = GetLevel(team, group, stat);
            if (lv >= MaxLevel) return -1f;
            return StandardTime[lv];
        }

        // ====================================================================
        // 진행 상태 조회
        // ====================================================================

        /// <summary> 해당 트랙이 현재 연구 진행 중인지(팀 잠금). </summary>
        public bool IsResearching(TeamId team, UpgradeGroup group, UnitUpgradeStat stat)
            => _active.ContainsKey(Key(team, group, stat));

        /// <summary>
        /// 진행 중 연구의 남은/전체 시간을 조회한다. 진행 중이 아니면 false.
        /// UI 프로그레스 바 표시용.
        /// </summary>
        public bool TryGetProgress(TeamId team, UpgradeGroup group, UnitUpgradeStat stat,
            out float remaining, out float total)
        {
            if (_active.TryGetValue(Key(team, group, stat), out var r))
            {
                remaining = r.Remaining;
                total = r.Total;
                return true;
            }
            remaining = 0f;
            total = 0f;
            return false;
        }

        /// <summary> 연구 착수 가능 여부(최대 레벨 미만 + 진행 중 아님). 골드 검증은 별도. </summary>
        public bool CanResearch(TeamId team, UpgradeGroup group, UnitUpgradeStat stat)
            => GetLevel(team, group, stat) < MaxLevel && !IsResearching(team, group, stat);

        /// <summary>
        /// 특정 연구소(건물 Id)가 현재 진행 중인 연구의 그룹/스탯을 조회한다.
        /// 연구 패널의 "매트릭스 ↔ 진행 레이어" 전환을 연구소 단위로 하기 위한 매핑 API다.
        ///   - 착수 시 각 진행 연구는 자신을 착수한 연구소 Id(BuildingId)를 기억하므로,
        ///     그 Id와 일치하는 첫 진행 연구를 돌려준다.
        ///   - UI가 "이 연구소는 한 번에 하나의 연구만" 규칙을 강제하므로 보통 최대 1개다.
        /// 싱글플레이/호스트에서만 유효하다(순수 클라이언트는 _active가 비어 있어 항상 false).
        /// </summary>
        /// <param name="buildingId">조회할 연구소 건물 Id.</param>
        /// <param name="group">진행 중이면 그 그룹.</param>
        /// <param name="stat">진행 중이면 그 스탯.</param>
        /// <returns>해당 연구소가 진행 중인 연구를 가지고 있으면 true.</returns>
        public bool TryGetActiveResearchByBuilding(int buildingId,
            out UpgradeGroup group, out UnitUpgradeStat stat)
        {
            foreach (var kv in _active)
            {
                if (kv.Value.BuildingId != buildingId) continue;
                group = kv.Key.group;
                stat = kv.Key.stat;
                return true;
            }
            group = default;
            stat = default;
            return false;
        }

        // ====================================================================
        // 라이프사이클 — 착수 / 진행 / 완료 / 취소(환불)
        // ====================================================================

        /// <summary>
        /// 연구를 착수한다(서버 권위 진입점). 검증 → 골드 차감 → 진행 타이머 시작.
        ///   - 최대 레벨/진행 중이면 실패.
        ///   - 골드 부족이면 실패(resource.SpendGold 실패).
        /// 성공 시 트랙이 잠기고(IsResearching=true) OnUpgradeChanged 발행.
        /// </summary>
        /// <param name="team">연구하는 팀.</param>
        /// <param name="group">강화 그룹(Regen은 그룹 무시).</param>
        /// <param name="stat">강화 스탯.</param>
        /// <param name="buildingId">착수한 연구소 건물 Id(파괴 시 취소·환불 기준).</param>
        /// <param name="resource">골드 검증·차감용. null이면 무료(테스트).</param>
        /// <returns>착수 성공 여부.</returns>
        public bool TryStartResearch(TeamId team, UpgradeGroup group, UnitUpgradeStat stat,
            int buildingId, ResourceUseCase resource)
        {
            if (!CanResearch(team, group, stat)) return false;

            int cost = GetNextLevelCost(team, group, stat);
            if (cost < 0) return false;

            // 서버 권위: 골드 검증·차감. 부족하면 착수 실패(트랙 잠금·타이머 미시작).
            if (resource != null && !resource.SpendGold(team, cost)) return false;

            float time = GetNextLevelTime(team, group, stat);
            _active[Key(team, group, stat)] = new ActiveResearch
            {
                Total = time,
                Remaining = time,
                Cost = cost,
                BuildingId = buildingId
            };

            GameEvents.OnUpgradeChanged.OnNext(team);
            return true;
        }

        /// <summary>
        /// 진행 중인 모든 연구의 타이머를 dt만큼 진행시키고, 완료된 연구는 레벨을 올린다.
        /// 완료 시 OnResearchCompleted 훅 호출(Infrastructure 브로드캐스트) + OnUpgradeChanged 발행.
        ///
        /// 호출 위치(서버 전용, 이중 틱 금지):
        ///   - 싱글플레이: GameBootstrapper.Update()(!IsNetworkMode).
        ///   - 멀티플레이: NetworkUpgradeController/NetworkCombatController 서버 틱(IsServer).
        /// </summary>
        /// <param name="dt">경과 시간(초).</param>
        public void TickResearch(float dt)
        {
            if (_active.Count == 0) return;

            _completedBuffer.Clear();
            foreach (var kv in _active)
            {
                kv.Value.Remaining -= dt;
                if (kv.Value.Remaining <= 0f)
                    _completedBuffer.Add(kv.Key);
            }

            for (int i = 0; i < _completedBuffer.Count; i++)
            {
                var key = _completedBuffer[i];
                _active.Remove(key);

                int newLevel = Mathf.Min(MaxLevel, GetLevelFromKey(key) + 1);
                _levels[key] = newLevel;

                OnResearchCompleted?.Invoke(key.Item1, key.Item2, key.Item3, newLevel);
                GameEvents.OnUpgradeChanged.OnNext(key.Item1);
            }
        }

        /// <summary>
        /// 레벨을 직접 지정한다(네트워크 클라가 서버 브로드캐스트를 적용할 때 — 골드/타이머 무관).
        /// 진행 중 레코드가 있으면 제거하여 완료 상태로 정리한다. OnUpgradeChanged 발행.
        /// </summary>
        public void SetLevel(TeamId team, UpgradeGroup group, UnitUpgradeStat stat, int level)
        {
            var key = Key(team, group, stat);
            _levels[key] = Mathf.Clamp(level, 0, MaxLevel);
            _active.Remove(key);
            GameEvents.OnUpgradeChanged.OnNext(team);
        }

        /// <summary>
        /// 연구소 파괴 시 그 건물이 착수한 진행 중 연구를 취소하고 투입 골드를 100% 환불한다.
        /// (완료된 레벨은 유지 — 환불 대상 아님. GameSystemRules_Upgrade.md 규칙 8.)
        /// </summary>
        /// <param name="buildingId">파괴된 연구소 건물 Id.</param>
        /// <param name="resource">환불 지급용 자원 UseCase.</param>
        public void OnLabDestroyed(int buildingId, ResourceUseCase resource)
        {
            CancelActiveForBuilding(buildingId, resource);
        }

        /// <summary>
        /// 연구 "취소"(건물은 유지) — 진행 본문의 [취소] 버튼이 호출한다.
        /// 해당 연구소가 진행 중인 연구를 중단하고 투입 골드를 100% 환불한다.
        /// 철거(OnLabDestroyed)와 동일한 취소·환불 로직을 쓰되, 건물은 파괴하지 않는다.
        /// (위치가 분리되어 있어 오조작을 막는다 — 철거는 하단, 취소는 진행 본문.)
        /// </summary>
        /// <param name="buildingId">연구를 취소할 연구소 건물 Id.</param>
        /// <param name="resource">환불 지급용 자원 UseCase.</param>
        /// <returns>취소된 연구가 하나라도 있으면 true.</returns>
        public bool CancelResearchByBuilding(int buildingId, ResourceUseCase resource)
        {
            return CancelActiveForBuilding(buildingId, resource) > 0;
        }

        /// <summary>
        /// 특정 연구소가 착수한 진행 중 연구를 모두 취소하고 투입 골드를 100% 환불하는 공용 내부 헬퍼.
        /// OnLabDestroyed(철거)와 CancelResearchByBuilding(연구 취소)이 공유한다.
        /// </summary>
        /// <returns>취소된 연구 개수.</returns>
        private int CancelActiveForBuilding(int buildingId, ResourceUseCase resource)
        {
            if (_active.Count == 0) return 0;

            _cancelBuffer.Clear();
            foreach (var kv in _active)
            {
                if (kv.Value.BuildingId == buildingId)
                    _cancelBuffer.Add(kv.Key);
            }

            for (int i = 0; i < _cancelBuffer.Count; i++)
            {
                var key = _cancelBuffer[i];
                ActiveResearch r = _active[key];
                _active.Remove(key);

                // 투입 골드 100% 환불(진행 중이던 연구만).
                if (resource != null && r.Cost > 0)
                    resource.AddGold(key.Item1, r.Cost);

                GameEvents.OnUpgradeChanged.OnNext(key.Item1);
            }

            return _cancelBuffer.Count;
        }

        /// <summary>
        /// 모든 강화 상태를 초기화한다(재경기/맵 재로드 시 잔여 상태 차단).
        /// </summary>
        public void Reset()
        {
            _levels.Clear();
            _active.Clear();
        }

        // 이미 정규화된 키에서 현재 레벨을 읽는 내부 헬퍼(TickResearch 완료 처리용).
        private int GetLevelFromKey((TeamId team, UpgradeGroup group, UnitUpgradeStat stat) key)
            => _levels.TryGetValue(key, out int lv) ? lv : 0;
    }
}
