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

        // [테스트 진단 로그 — 제거 예정] 파일 기록 로그 싱크(콘솔+에디터 파일).
        //   Application은 Infrastructure(RuntimeLogger)를 직접 참조할 수 없으므로 인터페이스로 역전 주입한다.
        //   GameBootstrapper가 SetLogSink로 주입하며, 미주입(null)이면 로깅은 조용히 생략된다(기존 동작과 동일).
        private IRuntimeLogSink _log;

        /// <summary>
        /// [테스트 진단 로그 — 제거 예정] 파일 로그 싱크를 주입한다(GameBootstrapper 조합 루트에서 호출).
        /// </summary>
        /// <param name="log">RuntimeLogger로 위임하는 어댑터.</param>
        public void SetLogSink(IRuntimeLogSink log) => _log = log;

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

            // [테스트 진단 로그 — 제거 예정] 상태 "부여" 전이. RuntimeLogger(파일+콘솔)로 남겨 Claude가 읽게 한다.
            //   LogRules 준수: raw Debug.Log 금지 → 싱크 경유. _log 미주입 시 무동작(?. 널전파).
            _log?.Log(LogLevel.Info, "Skill", nameof(StatusEffectSystem), "Apply",
                $"role={DbgRole()}, unit={unitId}, kind={effect.Kind}, mag={effect.Magnitude}, " +
                $"dur={effect.RemainingDuration:F2}, team={effect.SourceTeam}");
        }

        // [테스트 진단 로그 — 제거 예정] 서버/클라/싱글 역할 태그. NetworkContext(Application 홀더)만 참조.
        private static string DbgRole()
        {
            if (!NetworkContext.IsNetworkActive) return "Single";
            return NetworkContext.IsNetworkServer ? "Server" : "Client";
        }

        /// <summary>
        /// 매 서버 틱에서 모든 유닛의 상태 남은 시간을 감소시키고 만료된 상태·빈 유닛 항목을 제거한다.
        /// </summary>
        /// <param name="dt">경과 시간(초).</param>
        public void Tick(float dt)
        {
            if (dt <= 0f || _states.Count == 0) return;

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
                    // [테스트 진단 로그 — 제거 예정] 이 유닛의 마지막 상태가 만료됨(Expire). 틱마다 찍지 않고
                    //   "빈 상태가 되는 전이"에서만 1회 — 스팸 없이 상태가 실제로 풀렸는지 확인 가능.
                    _log?.Log(LogLevel.Info, "Skill", nameof(StatusEffectSystem), "Expire(모든 상태 만료)",
                        $"role={DbgRole()}, unit={id}");
                }
            }

            for (int i = 0; i < _emptyKeyBuffer.Count; i++)
                _states.Remove(_emptyKeyBuffer[i]);

            _emptyKeyBuffer.Clear();
            _tickKeyBuffer.Clear();
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
