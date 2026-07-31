// ============================================================================
// DamageCalculator.cs
// 방어력(Defense) 기반 데미지 감쇄 공식을 담는 순수 함수 헬퍼.
//
// 왜 별도 클래스인가:
//   직격(ApplyDamageToVictim) · 스플래시(ApplyFixedDamageToVictim/ApplyQuakeSplash) ·
//   타워→유닛(TowerCombatUseCase) 등 "최종 데미지 적용 지점"이 여러 곳에 흩어져 있다.
//   이 모든 지점이 정확히 같은 감쇄 공식을 쓰도록, 계산을 한 곳(순수 함수)에 모은다.
//
// 감쇄 공식 (GameSystemRules_Upgrade.md 규칙 5 / GameSystemRules_Units.md 규칙 44):
//   데미지 = Max(1, Round(공격력 × (1 − 방어력 / (방어력 + K))))
//   - K = 120 (감쇄 상수)
//   - 최소 데미지 1 보장(floor)
//   - 감쇄율 하드캡 65% (미래 확장 안전장치. 현 Lv5 방어 40은 25%로 미도달)
//
// 하위호환 (매우 중요):
//   방어력 0이면 감쇄율 0 → 결과가 기존과 정확히 동일(= rawDamage 그대로).
//   즉 방어력 연구(Phase 2) 이전의 모든 전투는 회귀 없이 그대로 동작한다.
//
// Domain 레이어 — 순수 C#. UnityEngine / Hexiege.Core 의존 없음
//   (UnitData.Heal과 동일하게 System.Math만 사용).
// ============================================================================

using System;

namespace Hexiege.Domain
{
    /// <summary>
    /// 방어력 기반 데미지 감쇄를 계산하는 순수 정적 헬퍼.
    /// 모든 최종 데미지 지점이 이 클래스를 통해 동일한 공식을 사용한다.
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary> 감쇄 상수 K. 방어력과 같은 단위. 값이 클수록 같은 방어력의 감쇄 효과가 작아진다. </summary>
        public const int DefenseConstantK = 120;

        /// <summary>
        /// 감쇄율 하드캡(0.0~1.0). 방어력이 아무리 높아도 이 비율 이상은 깎이지 않는다.
        /// 미래 확장(높은 방어력 트랙 추가) 시 무한 감쇄를 막는 안전장치.
        /// 현재 확정 최고 방어력(Lv5=40)의 감쇄율은 40/(40+120)=25%로 이 캡에 도달하지 않는다.
        /// </summary>
        public const float MaxMitigationRate = 0.65f;

        /// <summary>
        /// raw 피해에 대상의 방어력 감쇄를 적용한 최종 데미지를 반환한다.
        ///
        /// 규칙:
        ///   - rawDamage가 0 이하이면 그대로 반환한다(0 데미지 공격은 0 유지 — 하위호환).
        ///   - 방어력이 0 이하이면 감쇄 없이 rawDamage를 그대로 반환한다(하위호환: 반올림/floor 개입 없음).
        ///   - 그 외에는 Max(1, Round(raw × (1 − 방어율)))를 반환한다. 방어율은 하드캡으로 제한된다.
        /// </summary>
        /// <param name="rawDamage">감쇄 전 피해량(공격력 또는 스플래시 산출값 등).</param>
        /// <param name="defense">피격 대상의 방어력. 0이면 무감쇄.</param>
        /// <returns>방어 감쇄가 반영된 최종 데미지(최소 1, 단 raw가 0 이하면 그대로).</returns>
        public static int ApplyDefense(int rawDamage, int defense)
        {
            // 0 이하 피해는 감쇄 로직을 태우지 않고 그대로 둔다.
            // (예: 공격력 0 유닛의 공격이 floor 1로 인해 1이 되어버리는 회귀 방지.)
            if (rawDamage <= 0) return rawDamage;

            // 방어력 0(연구 이전/건물)이면 어떤 계산도 거치지 않고 원본 그대로 반환한다.
            // → 방어 연구 이전 상태의 전투가 기존과 100% 동일하게 동작함을 보장(하위호환).
            if (defense <= 0) return rawDamage;

            // 감쇄율 = 방어력 / (방어력 + K). 0~1 범위.
            float mitigation = (float)defense / (defense + DefenseConstantK);

            // 하드캡으로 감쇄율 상한을 제한(미래 안전장치).
            if (mitigation > MaxMitigationRate) mitigation = MaxMitigationRate;

            // Round: 소수점 반올림(0.5는 반올림 올림 방향 = AwayFromZero)으로 처리.
            // double로 계산해 float 누적 오차를 줄인다.
            double reduced = rawDamage * (1.0 - mitigation);
            int result = (int)Math.Round(reduced, MidpointRounding.AwayFromZero);

            // 최소 데미지 1 보장(floor). raw가 1 이상인데 감쇄로 0이 되는 것을 방지.
            return result < 1 ? 1 : result;
        }
    }
}
