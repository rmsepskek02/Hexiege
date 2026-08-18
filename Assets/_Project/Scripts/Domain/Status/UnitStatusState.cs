// ============================================================================
// UnitStatusState.cs
// 한 유닛에 걸려 있는 활성 상태효과 목록 + "유효 스탯 계산"(순수 도메인).
//
// 무엇을 하나(초급자용 설명):
//   한 유닛에게 동시에 여러 상태가 걸릴 수 있다(예: 공격 버프 + 둔화). 이 클래스는 그 목록을 들고
//   "지금 이 유닛의 공격력 배율/이동속도 배율/공격 가능 여부"를 하나로 합쳐 계산한다.
//   실제 스탯은 여기서 바꾸지 않는다(UnitData는 불변) — 접근자(UnitCombatUseCase)가 이 배율을
//   기본 스탯·연구 배율과 곱해서 쓴다(확정 9-1·9-4 곱연산).
//
// 중첩(스택) 규칙:
//   같은 종류(Kind)의 상태를 다시 걸면 새 레코드를 추가하지 않고 기존 것을 갱신한다(강도·시간 새 값으로 교체).
//   → 한 유닛의 같은 종류 상태는 항상 최대 1개. (HoT/DoT의 "대상별 동종 1개" 규칙과 동일 철학.)
//
// 합성 규칙:
//   - 공격력 배율(AttackMultiplier)   = 모든 AttackPowerMul 강도의 곱(없으면 1).
//   - 이동속도 배율(MoveSpeedMultiplier) = Freeze가 하나라도 있으면 0, 아니면 모든 MoveSpeedMul 강도의 곱(없으면 1).
//   - 공격 가능(CanAttack)            = Freeze/AttackDisabled가 하나라도 있으면 false.
//   ⚠️ 상태가 하나도 없으면 배율은 1, CanAttack은 true → 기존 전투와 완전히 동일(회귀 안전).
//
// Domain 레이어 — 순수 C#. UnityEngine / Hexiege.Core 참조 금지(System.Collections.Generic만 사용).
// ============================================================================

using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 한 유닛의 활성 상태효과 목록과, 그로부터 산출한 유효 스탯 배율/공격 게이트를 제공한다(순수 계산).
    /// </summary>
    public sealed class UnitStatusState
    {
        // 이 유닛에 걸린 활성 상태효과들(종류별 최대 1개).
        private readonly List<StatusEffect> _effects = new List<StatusEffect>(4);

        /// <summary> 활성 상태효과가 하나도 없는지 여부(비면 상태 시스템이 이 상태 객체를 제거한다). </summary>
        public bool IsEmpty => _effects.Count == 0;

        /// <summary>
        /// 상태효과를 부여하거나, 같은 종류가 이미 있으면 갱신(강도·남은 시간을 새 값으로 교체)한다.
        /// </summary>
        /// <param name="effect">부여/갱신할 상태효과.</param>
        public void ApplyOrRefresh(StatusEffect effect)
        {
            if (effect == null) return;

            // 같은 종류가 이미 있으면 그 레코드를 교체(중첩 없음).
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].Kind == effect.Kind)
                {
                    _effects[i] = effect;
                    return;
                }
            }
            _effects.Add(effect);
        }

        /// <summary>
        /// 모든 활성 상태의 남은 시간을 dt만큼 줄이고, 만료(0 이하)된 것을 제거한다.
        /// </summary>
        /// <param name="dt">경과 시간(초).</param>
        public void TickAndPrune(float dt)
        {
            // 뒤에서 앞으로 순회하여 제거 시 인덱스 밀림을 피한다.
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                StatusEffect e = _effects[i];
                e.RemainingDuration -= dt;
                if (e.RemainingDuration <= 0f)
                    _effects.RemoveAt(i);
            }
        }

        /// <summary>
        /// 공격력 배율 = 모든 AttackPowerMul 강도의 곱. 해당 상태가 없으면 1(무변경).
        /// </summary>
        public float AttackMultiplier()
        {
            float mul = 1f;
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].Kind == StatusEffectKind.AttackPowerMul)
                    mul *= _effects[i].Magnitude;
            }
            return mul;
        }

        /// <summary>
        /// 이동속도 배율 = Freeze가 있으면 0, 아니면 모든 MoveSpeedMul 강도의 곱. 해당 상태가 없으면 1(무변경).
        /// </summary>
        public float MoveSpeedMultiplier()
        {
            float mul = 1f;
            for (int i = 0; i < _effects.Count; i++)
            {
                StatusEffect e = _effects[i];
                if (e.Kind == StatusEffectKind.Freeze) return 0f; // 빙결 = 완전 정지(최우선).
                if (e.Kind == StatusEffectKind.MoveSpeedMul) mul *= e.Magnitude;
            }
            return mul;
        }

        /// <summary>
        /// 공격 가능 여부 = Freeze/AttackDisabled가 하나라도 있으면 false. 해당 상태가 없으면 true(무변경).
        /// </summary>
        public bool CanAttack()
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                StatusEffectKind k = _effects[i].Kind;
                if (k == StatusEffectKind.Freeze || k == StatusEffectKind.AttackDisabled)
                    return false;
            }
            return true;
        }
    }
}
