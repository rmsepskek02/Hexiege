// ============================================================================
// VfxPoolItem.CasterOwned.cs
// "시전자에게 귀속된" 공격 이펙트(기 모으기 charge 등)를 시전자 사망·제거 시 즉시 끄는 부분.
//
// 왜 필요한가 (2026-10-13 vfx-code-followups 재작업 R2-1):
//   FoxMagician(vfx_foxmagician_charge, 약 2초)·InfernoSpirit(vfx_infernospirit_charge, 약 1초)은
//   공격 동작 앞부분의 OnAttackHit 표식에서 "기 모으기" 이펙트를 시작한다(UnitView → EffectManager.PlayUnitAttack).
//   이 이펙트는 일반 풀 이펙트라 파티클이 다 사라질 때까지 혼자 재생됐다.
//   그래서 기를 모으는 도중 유닛이 죽어도 빈자리에서 이펙트가 끝까지 남아 보였다.
//
// 동작 방식 (두 겹 — 하나가 놓쳐도 다른 하나가 잡는다):
//   ① 즉시 정지: UnitView가 사망 이벤트(OnUnitDied)와 OnDisable(파괴·디스폰)에서
//      EffectManager.StopCasterOwnedAttackEffects(자기 자신)를 부른다 → 그 유닛이 소유한 이펙트만 즉시 정지.
//   ② 안전망: 이 아이템이 매 프레임(Update) 시전자 상태를 직접 확인한다.
//      시전자 View가 파괴됐거나, 비활성화됐거나, 다른 유닛 데이터로 바뀌었거나, 죽었으면 스스로 정지한다.
//      (이벤트 순서가 어긋나거나 구독이 없는 경로로 유닛이 사라져도 최대 1프레임 안에 정리된다.)
//
// 정지 = "새 파티클 생성 중지 + 이미 나온 파티클까지 즉시 삭제(StopEmittingAndClear)" → 바로 풀 반환.
//   기 모으기는 시전자 몸 앞에 붙어 있는 연출이라, 시전자가 사라졌으면 잔상도 남기지 않는 것이 자연스럽다.
//
// 적용 대상: EffectPreset.StopWhenCasterDies를 켠 "공격" 프리셋만(현재 FoxMagician·InfernoSpirit).
//   사망 이펙트, 트레이서, 착탄(버섯), 타워 번개, 화상·독·힐 지속 이펙트는 이 경로를 타지 않는다.
//
// Host·Client: 각 화면이 자기 UnitView로 자기 이펙트를 재생·정지한다(서버·RPC·피해 경로 무변경).
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, ParticleSystem).
// ============================================================================

using Hexiege.Domain;
using UnityEngine;

namespace Hexiege.Presentation
{
    public partial class VfxPoolItem
    {
        // ── 재생별 시전자 귀속 상태 (ResetCasterOwnership에서 전부 비운다) ──
        private bool _casterOwned;        // 지금 이 재생이 시전자에게 귀속돼 있는가
        private UnitView _casterView;      // 시전자의 화면 컴포넌트(재생 시점에 1번 고정)
        private UnitData _casterData;      // 재생 시점의 시전자 Domain 객체(ID가 아니라 객체 참조로 동일성 확인)

        /// <summary>읽기 전용 진단/검증용: 지금 재생 중이며 시전자에게 귀속돼 있는지.</summary>
        public bool CasterOwned => _active && _casterOwned;

        /// <summary>
        /// 방금 Play()로 시작한 공격 이펙트를 시전자에게 귀속시킨다.
        /// 반드시 Play() "뒤"에 호출해야 한다(Play가 ResetTravelState로 이전 재생의 귀속 상태를 먼저 지우기 때문).
        /// </summary>
        /// <param name="caster">공격한 유닛의 UnitView.</param>
        /// <param name="casterData">공격한 유닛의 Domain 데이터(caster.Data).</param>
        /// <returns>귀속에 성공했으면 true. 재생 중이 아니거나 참조가 없으면 false(일반 이펙트처럼 끝까지 재생).</returns>
        public bool BeginCasterOwnership(UnitView caster, UnitData casterData)
        {
            if (!_active || caster == null || casterData == null) return false;
            _casterOwned = true;
            _casterView = caster;
            _casterData = casterData;
            return true;
        }

        /// <summary>
        /// 이 이펙트가 지금 주어진 시전자에게 귀속돼 재생 중인지.
        /// ReferenceEquals를 쓰므로 시전자 GameObject가 이미 파괴 중(OnDisable/OnDestroy)이어도 같은 객체로 판별된다.
        /// </summary>
        /// <param name="caster">확인할 시전자 UnitView.</param>
        public bool IsOwnedByCaster(UnitView caster)
            => _active && _casterOwned && caster is object && ReferenceEquals(_casterView, caster);

        /// <summary>
        /// 시전자 귀속 이펙트를 즉시 멈추고(남은 파티클까지 삭제) 풀에 반환한다.
        /// 귀속되지 않은 재생(트레이서·사망 이펙트·일반 공격 이펙트)에는 아무것도 하지 않는다.
        /// </summary>
        /// <param name="reason">정지 사유(진단용 짧은 토큰, 예: "CasterDeath").</param>
        /// <returns>실제로 정지·반환했으면 true.</returns>
        public bool StopCasterOwned(string reason)
        {
            if (!_active || !_casterOwned) return false;
            // 귀속 표시를 먼저 내려 Return() 도중 재진입(Update 안전망 등)이 같은 일을 두 번 하지 않게 한다.
            ResetCasterOwnership();
            // 루트 PS가 있으면 하위까지 한 번에, 없으면 형제 시스템을 각각(재생 때와 같은 구조) 정지·삭제한다.
            if (_ps != null) _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            else if (_childSystems != null)
                for (int i = 0; i < _childSystems.Length; i++)
                    if (_childSystems[i] != null) _childSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Return(); // 위치·스케일·이동 상태 원복 + 비활성화 + EffectManager 풀 반환(기존 반환 경로 그대로)
            return true;
        }

        /// <summary>
        /// 안전망: 매 프레임 시전자가 아직 "원래 그 유닛이고 살아 있는지" 확인하고, 아니면 스스로 정지한다.
        /// Update에서 호출된다. 귀속되지 않은 재생이면 즉시 false를 돌려주므로 비용이 거의 없다.
        /// </summary>
        /// <returns>이번 호출에서 정지·반환했으면 true(호출한 Update는 그 프레임 처리를 끝내야 한다).</returns>
        private bool PollCasterOwnership()
        {
            if (!_active || !_casterOwned) return false;
            string reason;
            // Unity 오브젝트는 파괴되면 == null 이 참이 된다(C# 참조가 남아 있어도).
            if (_casterView == null) reason = "CasterDestroyed";
            // 같은 View에 다른 UnitData가 들어왔다 = 다른 유닛으로 재사용됐다 → 원래 시전자는 이미 없다.
            else if (!ReferenceEquals(_casterView.Data, _casterData)) reason = "CasterReplaced";
            else if (_casterData == null || !_casterData.IsAlive) reason = "CasterDead";
            else if (!_casterView.gameObject.activeInHierarchy) reason = "CasterRemoved";
            else return false; // 시전자 정상 → 계속 재생
            return StopCasterOwned(reason);
        }

        /// <summary>
        /// 시전자 귀속 상태를 비운다. 새 재생 시작·풀 반환이 모두 거치는 ResetTravelState에서 호출되므로
        /// 풀 재사용 시 옛 시전자 참조가 남지 않는다.
        /// </summary>
        private void ResetCasterOwnership()
        {
            _casterOwned = false;
            _casterView = null;
            _casterData = null;
        }
    }
}
