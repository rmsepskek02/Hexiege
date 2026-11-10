// ============================================================================
// StatusEffectSystem.cs
// 유닛별 상태효과(버프/디버프/제어)를 서버 권위로 부여·해제·틱 관리하는 서비스.
//
// 무엇을 하나(초급자용 설명):
//   "어떤 유닛에게 어떤 상태가 몇 초 걸려 있는가"를 유닛 Id별로 보관하고, 매 서버 틱마다 남은 시간을
//   줄여 만료된 상태를 지운다. 유효 스탯 접근자(UnitCombatUseCase.EffectiveAttack /
//   GetUnitMoveSpeedMultiplier / CanAttack)가 이 서비스에 "이 유닛의 공격 배율/이속 배율/공격 가능?"을
//   물어 배율·게이트를 산출한다.
//
//   ⚠️ 회복(HealOverTime)은 여기서 다루지 않는다 — 회복 HP는 기존 HoT(UnitCombatUseCase.ApplyTimedEffect)가
//      처리하고 네트워크로 이미 동기화되므로, 이 시스템에는 "스탯 배율/게이트에 영향을 주는" 상태만 담는다.
//
// 소유·틱 패턴(기존과 동일):
//   UnitUpgradeUseCase(_active)·UnitCombatUseCase(_activeTimedEffects)처럼 딕셔너리로 상태를 보관하고
//   서버 틱 단일 지점에서 Tick(dt)을 호출한다(싱글=GameBootstrapper.Update, 멀티 서버=NetworkCombatController).
//
// 회귀 안전(핵심):
//   유닛에 걸린 상태가 하나도 없으면 GetAttackMultiplier/GetMoveSpeedMultiplier=1, CanAttack=true를 반환한다.
//   → 상태 시스템을 배선해도 스킬을 쓰기 전에는 전투가 기존과 완전히 동일하다.
//
// Application 레이어 — Domain 의존. Unity/Netcode 참조 없음(순수 C#).
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 유닛 Id별 상태효과를 부여·해제·틱 관리하고 유효 스탯 배율/공격 게이트를 제공하는 서버 권위 서비스.
    /// </summary>
    public sealed class StatusEffectSystem
    {
        // 유닛 Id → 그 유닛의 활성 상태 목록. 상태가 있는 유닛만 항목을 갖는다(없으면 조회 시 기본값).
        private readonly Dictionary<int, UnitStatusState> _states = new Dictionary<int, UnitStatusState>();

        // Tick 중 컬렉션 변경(빈 상태 제거)을 피하기 위한 재사용 키 버퍼(서버/싱글 단일 스레드라 재사용 안전).
        private readonly List<int> _emptyKeyBuffer = new List<int>(8);
        private readonly List<int> _tickKeyBuffer = new List<int>(16);

        /// <summary>
        /// 지정 유닛에 상태효과를 부여한다(같은 종류가 있으면 갱신). 서버(싱글=로컬 서버/멀티=서버)에서만 호출된다.
        /// </summary>
        /// <param name="unitId">대상 유닛 Id.</param>
        /// <param name="effect">부여할 상태효과.</param>
        public void Apply(int unitId, StatusEffect effect)
        {
            if (effect == null || effect.RemainingDuration <= 0f) return;

            if (!_states.TryGetValue(unitId, out UnitStatusState state))
            {
                state = new UnitStatusState();
                _states[unitId] = state;
            }
            state.ApplyOrRefresh(effect);
        }

        /// <summary>
        /// 매 서버 틱에서 모든 유닛의 상태 남은 시간을 감소시키고 만료된 상태·빈 유닛 항목을 제거한다.
        /// </summary>
        /// <param name="dt">경과 시간(초).</param>
        public void Tick(float dt)
        {
            // [2026-11-10 강화 스킬 진단] 상태가 하나도 없어 일찍 끝나는 틱에서도 진단 감시자에게 경과 시간을 알린다
            //   (에디터 · 개발 빌드 전용 — 릴리스에서는 이 호출 자체가 사라진다). 옛 줄(기록용):
            // if (dt <= 0f || _states.Count == 0) return;
            if (dt <= 0f || _states.Count == 0)
            {
                NotifyDiagnosticTick(dt);
                return;
            }

            // 딕셔너리 순회 중 제거를 피하기 위해 키를 복사한 뒤 값만 갱신한다.
            _tickKeyBuffer.Clear();
            foreach (var key in _states.Keys) _tickKeyBuffer.Add(key);

            _emptyKeyBuffer.Clear();
            for (int i = 0; i < _tickKeyBuffer.Count; i++)
            {
                int id = _tickKeyBuffer[i];
                UnitStatusState state = _states[id];
                state.TickAndPrune(dt);
                if (state.IsEmpty)
                {
                    _emptyKeyBuffer.Add(id);
                }
            }

            for (int i = 0; i < _emptyKeyBuffer.Count; i++)
                _states.Remove(_emptyKeyBuffer[i]);

            _emptyKeyBuffer.Clear();
            _tickKeyBuffer.Clear();

            // [2026-11-10 강화 스킬 진단] 이번 틱의 만료 처리가 끝난 "뒤"에 알린다 → 감시자가 「이번 틱에 만료됐다」를 바로 본다.
            NotifyDiagnosticTick(dt);
        }

        /// <summary>
        /// 지정 유닛에 지정 종류의 상태가 지금 걸려 있는지(읽기 전용 — 2026-11-10 추가).
        /// 강화 스킬 진단(재적용 · 만료 판정)과 Editor 자가 검증이 쓴다. 상태를 바꾸지 않는다.
        /// </summary>
        /// <param name="unitId">대상 유닛 Id.</param>
        /// <param name="kind">확인할 상태 종류.</param>
        /// <returns>걸려 있으면 true.</returns>
        public bool HasEffect(int unitId, StatusEffectKind kind)
        {
            return _states.TryGetValue(unitId, out UnitStatusState s) && s.Has(kind);
        }

        // ====================================================================
        // [2026-11-10 강화 스킬 진단 — 지원 스킬 1차 강화 Task 구현 단위 ④] 틱 감시자
        //   무엇인가(초급자용 설명):
        //     강화 효과가 "몇 초 뒤에 실제로 끝났는지"(진단 BuffExpired)를 알려면 상태 시간이 줄어드는 순간을 봐야 한다.
        //     틱은 조합 루트(싱글 · 순수 Client = GameBootstrapper.Update, Host = NetworkCombatController)가 이 Tick을 직접 부르므로,
        //     전투 UseCase가 여기에 "틱이 끝나면 불러 달라"는 감시자(함수)를 1개 걸어 둔다.
        //   진단 전용이다 — 감시자는 상태를 바꾸지 않고, 감시자 호출은 [Conditional]로 릴리스 빌드에서 사라진다.
        //   감시자에서 예외가 나도 서버 틱이 깨지지 않게 여기서 삼킨다(진단 실패는 게임 결과와 무관).
        // ====================================================================
        private System.Action<float> _diagnosticTickListener;

        /// <summary>
        /// 진단 틱 감시자를 건다(null이면 해제). UnitCombatUseCase.SetStatusEffectSystem이 에디터 · 개발 빌드에서만 부른다.
        /// </summary>
        /// <param name="listener">틱마다 경과 시간(초)을 받는 함수.</param>
        public void SetDiagnosticTickListener(System.Action<float> listener)
        {
            _diagnosticTickListener = listener;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void NotifyDiagnosticTick(float dt)
        {
            if (!(dt > 0f) || _diagnosticTickListener == null) return;
            try { _diagnosticTickListener(dt); }
            catch { /* 진단 전용 — 서버 틱을 막지 않는다 */ }
        }

        /// <summary> 지정 유닛의 공격력 배율(상태 없으면 1 — 무변경). </summary>
        /// <param name="unitId">대상 유닛 Id.</param>
        public float GetAttackMultiplier(int unitId)
        {
            return _states.TryGetValue(unitId, out UnitStatusState s) ? s.AttackMultiplier() : 1f;
        }

        /// <summary> 지정 유닛의 이동속도 배율(상태 없으면 1, 빙결 시 0 — 무변경 보장). </summary>
        /// <param name="unitId">대상 유닛 Id.</param>
        public float GetMoveSpeedMultiplier(int unitId)
        {
            return _states.TryGetValue(unitId, out UnitStatusState s) ? s.MoveSpeedMultiplier() : 1f;
        }

        /// <summary> 지정 유닛이 공격 가능한지(상태 없으면 true, 빙결/기절 시 false). </summary>
        /// <param name="unitId">대상 유닛 Id.</param>
        public bool CanAttack(int unitId)
        {
            return !_states.TryGetValue(unitId, out UnitStatusState s) || s.CanAttack();
        }

        /// <summary> 지정 유닛의 모든 상태를 제거한다(유닛 사망/제거 정리용, 선택 호출). </summary>
        /// <param name="unitId">대상 유닛 Id.</param>
        public void RemoveUnit(int unitId)
        {
            _states.Remove(unitId);
        }

        /// <summary> 모든 상태를 초기화한다(재경기/맵 전환 시). </summary>
        public void ClearAll()
        {
            _states.Clear();
        }
    }
}
