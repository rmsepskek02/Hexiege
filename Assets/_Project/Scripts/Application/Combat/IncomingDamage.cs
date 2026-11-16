// ============================================================================
// IncomingDamage.cs
// 서버에서 피해가 HP에 들어가는 6곳이 모두 거치는 「단일 헬퍼」 — 방어 감쇄가 끝난 피해에 스킬 효과를 얹고 HP를 깎는다.
// 2026-11-16 지원 스킬 새 상태 Task(_Tasks/2026-11-16/01_00_support-skill-new-statuses) 단계 A — 구현 단위 A-2(Plan S-14).
//
// 왜 필요한가(초급자용 설명):
//   피해가 HP에 들어가는 서버 자리가 6곳 흩어져 있다(Research 6-1):
//     ① UnitCombatUseCase.ApplyDamageToVictim       — 직접 공격 · 유닛 투사체 착탄 · 도끼병 휩쓸기 · 물정령 파도
//     ② UnitCombatUseCase.ApplyFixedDamageToVictim  — QuakeSpirit 스플래시
//     ③ UnitCombatUseCase.ApplySkillInstantDamageToVictim — 스킬 타입 A(포격)
//     ④ UnitCombatUseCase.ApplyTimedDamageToUnit    — 지속 피해(독 · 화상 · 타입 B 장판)
//     ⑤ TowerCombatUseCase.ExecuteTowerAttack       — RuneSpire · VineTower 즉발
//     ⑥ TowerCombatUseCase.ApplyTowerProjectileImpact — 인간 대포탑 포탄
//   대지의 갑주(받는 피해 0.7배)는 "모든 피해"에 걸려야 하므로, 6곳이 각자 계산하면 하나라도 빠뜨리기 쉽다(위험 R-1).
//   그래서 6곳의 `target.TakeDamage(값)` 줄을 모두 이 헬퍼 호출로 바꿨다. 순서(Plan S-14):
//     방어 감쇄(각 자리의 기존 계산 그대로) → 받는 피해 배율(유닛만) → (단계 C: 쉴드 흡수) → HP.
//
// 회귀 안전(핵심):
//   - 서비스가 없거나(null) · 대상이 건물이거나 · 배율이 정확히 1이면 곱하지 않고 받은 값을 그대로 TakeDamage에 넘긴다.
//     → 대지의 갑주가 안 걸린 유닛 · 건물은 이 헬퍼가 생기기 전과 비트 단위로 같은 피해를 받는다.
//
// 반올림 · 최소 1(Plan S-14 — DamageCalculator.ApplyDefense와 같은 규칙):
//   결과 = 반올림(피해 × 배율, 0.5는 0에서 먼 쪽으로 = AwayFromZero), 그리고 감쇄 전 피해가 0보다 크면 결과는 최소 1.
//   배율은 0.001 단위(밀리)로 맞춘 뒤 정수로만 계산한다 — float 0.7은 실제로 0.699999988이라 5 × 0.7이 3.4999… → 3이 되는
//   부동소수 오차를 막고, 3.5 → 4처럼 정확히 반인 값을 확실하게 올리기 위해서다.
//     예) 10 × 0.7 = 7 · 5 × 0.7 = 3.5 → 4 · 2 × 0.7 = 1.4 → 1 · 1 × 0.7 = 0.7 → 1(최소 1) · 0 → 0(그대로).
//
// Application 레이어 — Domain(IDamageable · UnitData) 의존. Unity 의존 없음.
// ============================================================================

using System;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 피해를 HP에 넣은 자리(진단 로그 Writer= 값). 피해 지점 6곳 중 어디서 왔는지를 가른다.
    /// 정수 값은 진단 계수 배열의 칸 번호로 쓰인다(새 값은 뒤에 붙인다).
    /// </summary>
    public enum IncomingDamageWriter
    {
        /// <summary> ① 직접 공격(주 타깃 단일 피해). </summary>
        Direct = 0,
        /// <summary> ① 유닛 투사체 착탄(서버 권위 투사체). </summary>
        Projectile = 1,
        /// <summary> ① 도끼병 휩쓸기(주 타깃 외 부채꼴 대상). </summary>
        Sweep = 2,
        /// <summary> ① 물정령 파도. </summary>
        Wave = 3,
        /// <summary> ② QuakeSpirit 스플래시. </summary>
        Splash = 4,
        /// <summary> ③ 스킬 타입 A(포격). </summary>
        Skill = 5,
        /// <summary> ④ 지속 피해(독 · 화상 · 장판). </summary>
        DoT = 6,
        /// <summary> ⑤ RuneSpire · VineTower 즉발. </summary>
        Tower = 7,
        /// <summary> ⑥ 인간 대포탑 포탄. </summary>
        TowerShell = 8
    }

    /// <summary>
    /// 피해 지점 6곳의 단일 헬퍼. 방어 감쇄 뒤 피해에 받는 피해 배율을 곱하고 대상 HP를 깎는다.
    /// </summary>
    public static class IncomingDamage
    {
        /// <summary>
        /// 대상에게 피해를 적용한다(서버 권위 경로 전용). 효과가 없으면 받은 값 그대로 <c>target.TakeDamage</c>.
        /// </summary>
        /// <param name="effects">같은 경기의 지원 스킬 효과 서비스(null 허용 — 무효과).</param>
        /// <param name="target">피해를 받을 대상(유닛 또는 건물).</param>
        /// <param name="damage">방어 감쇄까지 끝난 피해(각 자리의 기존 계산 결과).</param>
        /// <param name="rawBeforeDefense">감쇄 전 원본 피해(진단 표본용 — 판정에는 쓰지 않는다).</param>
        /// <param name="writer">피해 지점(진단 표본 · 계수용).</param>
        /// <returns>실제로 TakeDamage에 넘긴 피해량.</returns>
        public static int Apply(SupportSkillEffectSystem effects, IDamageable target, int damage, int rawBeforeDefense,
                                IncomingDamageWriter writer)
        {
            if (target == null) return 0;

            if (effects != null && target is UnitData unit)
            {
                // 받는 피해 배율(대지의 갑주). 효과가 없으면 정확히 1 → 곱하지 않는다(회귀 안전).
                float mul = effects.GetIncomingDamageMultiplier(unit.Id);
                int finalDamage = mul != 1f ? ScaleDamage(damage, mul) : damage;

                // (단계 C 자리) 쉴드 흡수 — 신목의 각성 회차에서 여기에 「흡수 → 남은 피해만 HP」를 넣는다.

                target.TakeDamage(finalDamage);
                // [진단 [SKILL-ARMOR]] 감소 표본 · 지점별 계수(에디터 · 개발 빌드 전용 — 릴리스에서는 이 호출이 사라진다).
                effects.DiagRecordIncomingDamage(unit, writer, rawBeforeDefense, damage, finalDamage, mul);
                return finalDamage;
            }

            // 건물 · 서비스 없음 — 기존과 같은 값 그대로(대지의 갑주는 유닛만 — Plan S-4 표 아래).
            target.TakeDamage(damage);
            return damage;
        }

        /// <summary>
        /// 피해에 배율을 곱한다 — 반올림 AwayFromZero · 감쇄 전 피해가 0보다 크면 최소 1(Plan S-14).
        /// 피해가 0 이하이거나 배율이 음수 · NaN · 무한대면 받은 피해를 그대로 돌려준다(방어적).
        /// 배율은 0.001 단위로 반올림해 쓴다(MagMilli와 같은 값 — 진단 판정과 일치).
        /// </summary>
        /// <param name="damage">방어 감쇄 뒤 피해.</param>
        /// <param name="multiplier">받는 피해 배율(예: 0.7).</param>
        /// <returns>배율이 반영된 피해.</returns>
        public static int ScaleDamage(int damage, float multiplier)
        {
            if (damage <= 0) return damage;
            if (!(multiplier >= 0f) || float.IsInfinity(multiplier)) return damage;

            long mulMilli = (long)Math.Round(multiplier * 1000.0, MidpointRounding.AwayFromZero);
            if (mulMilli == 1000) return damage;

            // 정수 반올림: (피해 × 배율밀리 + 500) ÷ 1000 — 양수에서 AwayFromZero와 같다(3500 → 4, 1400 → 1).
            long scaled = ((long)damage * mulMilli + 500L) / 1000L;
            if (scaled < 1) return 1;                    // 최소 1(감쇄 전 피해 > 0 — 위에서 확인)
            if (scaled > int.MaxValue) return int.MaxValue;
            return (int)scaled;
        }
    }
}
