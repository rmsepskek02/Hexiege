// ============================================================================
// UnitView.DeathDeferral.cs
// [2026-10-26 서버 권위 투사체 — 단계 D 후속, 수정안 (a)] 투사체 사망타의 "사망 표현"을 화면 착탄까지 미루는 UnitView 쪽 부분.
//
// [흐름 — 유니티 초급자용]
//   ① 사망 통보(OnUnitDied) → TryBeginDeferredDeathPresentation:
//      DeferredUnitDeathPresenter에게 "이 유닛에게 아직 화면에 안 닿은 투사체 사망타가 있나?"를 묻는다.
//      있으면 미루기로 하고, 이 유닛의 차지 VFX 귀속을 Visual Root(곧 사망 대역이 될 자식)로 넘긴다.
//   ② 같은 핸들러 끝에서 DetachDeferredDeathGhost: Visual Root(메시·Animator)를 부모에서 떼어 "사망 대역"으로 남긴다.
//      원래 유닛 오브젝트는 예전처럼 즉시 사라진다(싱글 Destroy · 멀티 NGO Despawn) — 타겟팅·도메인·서버는 이미 "죽음"이다.
//   ③ 투사체가 화면에 닿아 그 사망타 숫자("0")가 나오는 순간(또는 상한 0.3초, 또는 게임 종료)
//      ReleaseDeferredDeathPresentation이 예전 사망 표현을 대역에서 한 번 한다:
//      차지 VFX 정지 → IsDead → 사망 VFX(대역 위치) + 사망음 → 대역 파괴.
//   ④ 맵 재초기화·파괴면 DiscardDeferredDeathPresentation이 표현 없이 대역만 치운다.
//
// [왜 "대역"인가] NGO Despawn(destroy:true)은 Client 오브젝트를 바로 파괴한다. 서버 Despawn을 늦추면
//   서버·Host·늦은 접속·관찰자 Retire·ID 재사용까지 영향이 번지므로, 화면 오브젝트만 떼어 화면에서만 잠깐 더 보여 준다.
//
// 표현 전용 — HP·피해·사망 판정·도메인 제거·서버 Despawn 시각은 바꾸지 않는다.
// ============================================================================

using UnityEngine;
using Hexiege.Application;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    public partial class UnitView : IDeferredUnitDeathOwner
    {
        // 이 유닛의 사망 표현(VFX·SFX 포함)을 보류 장치가 맡았는가. 한 번 true가 되면 사망 VFX는 보류 장치가 정확히 1회 낸다
        // (Client NetworkUnit 디스폰 경로는 이 값을 보고 사망 VFX를 다시 내지 않는다).
        private bool _deathDeferralOwned;
        // 맡은 사망 표현을 이미 진행(또는 버림)했는가.
        private bool _deathDeferralFinished;
        // [Client 경쟁 조건] NGO 디스폰이 사망 통보보다 먼저 와서 디스폰 쪽이 이미 사망 VFX를 냈는가 — 그러면 미루지 않는다.
        private bool _deathPresentedAtDespawn;
        // 사망 대역(떼어 낸 Visual Root)과 그 안의 Animator, 진행 때 쓸 유닛 종류·마지막 위치.
        private GameObject _deathGhost;
        private Animator _deathGhostAnimator;
        private UnitType _deathGhostType;
        private Vector3 _deathGhostPosition;

        /// <summary>
        /// 사망 표현이 투사체 화면 착탄까지 미뤄진 상태인가(차지 VFX 안전망이 "아직 화면에 살아 있다"로 보는 기준).
        /// </summary>
        public bool IsDeathPresentationDeferred => _deathDeferralOwned && !_deathDeferralFinished;

        /// <summary>
        /// 이 유닛의 사망 VFX·SFX를 보류 장치가 맡았는가(진행 전·후 모두 true).
        /// Client NetworkUnit.OnNetworkDespawn이 true면 사망 VFX를 내지 않는다(이중 재생 방지).
        /// </summary>
        public bool OwnsDeferredDeathPresentation => _deathDeferralOwned;

        /// <summary>
        /// [Client 전용 경쟁 조건] NGO 디스폰 쪽이 사망 VFX를 이미 냈다고 표시한다.
        /// 그 뒤 늦게 온 사망 통보에서는 미루지 않는다(사망 VFX가 두 번 나오지 않게).
        /// </summary>
        public void MarkDeathPresentedAtDespawn() => _deathPresentedAtDespawn = true;

        // ① 미룰지 정하고, 미루면 차지 VFX 귀속을 곧 대역이 될 Visual Root로 넘긴다.
        private bool TryBeginDeferredDeathPresentation()
        {
            if (_deathDeferralOwned || _deathPresentedAtDespawn || _unitData == null) return false;
            DeferredUnitDeathPresenter presenter = DeferredUnitDeathPresenter.Current;
            if (presenter == null) return false;
            if (_visualRootProjector == null) _visualRootProjector = GetComponent<VisualRootProjector>();
            bool ghostAvailable = _visualRootProjector != null && _visualRootProjector.HasDetachableVisualRoot;
            if (!presenter.TryDefer(_unitData, this, ghostAvailable)) return false;
            _deathDeferralOwned = true;
            _deathDeferralFinished = false;
            _deathGhostType = _unitData.Type;
            GameObject ghost = _visualRootProjector.ObservedVisualRoot.gameObject;
            _deathGhost = ghost;
            _deathGhostPosition = ghost.transform.position;
            // 차지 VFX(StopWhenCasterDies)를 대역 소유로 넘긴다 — 아래 사망 정리(공격 회차 닫기·OnDisable)가 지금 끄지 않고,
            // 사망 표현을 진행할 때 대역과 함께 멈춘다.
            EffectManager.Instance?.HandOverCasterOwnedAttackEffects(this, ghost);
            return true;
        }

        // ② Visual Root를 떼어 사망 대역으로 남긴다(사망 정리 코드가 끝난 뒤 호출).
        private void DetachDeferredDeathGhost()
        {
            if (!IsDeathPresentationDeferred || _deathGhost == null) return;
            Transform root = _visualRootProjector != null ? _visualRootProjector.DetachVisualRootForDeathGhost() : null;
            if (root == null)
            {
                // 이론상 오지 않는다(TryDefer 전에 확인했다). 그래도 왔다면 지금 진행한다 — 사망 표현이 사라지지 않게.
                ReleaseDeferredDeathPresentation("no-visual-root");
                return;
            }
            // 대역이 마지막 화면 pose 그대로 남도록 월드 위치·회전·크기를 유지한 채 떼어 낸다.
            root.SetParent(null, true);
            _deathGhostPosition = root.position;
            // 이 View의 Animator가 대역 안에 있으면 대역 쪽으로 옮겨 잡는다. View는 곧 파괴되며,
            // 그 사이 다른 신호(전투 정지 등)가 대역의 자세를 바꾸지 않도록 View 쪽 참조를 비운다.
            if (_animator != null && _animator.transform.IsChildOf(root))
            {
                _deathGhostAnimator = _animator;
                _animator = null;
            }
        }

        /// <summary>
        /// 미뤄 둔 사망 표현을 지금 1회 진행한다(DeferredUnitDeathPresenter가 부른다).
        /// 이 View가 이미 파괴된 뒤에도 불릴 수 있다 — 아래는 대역·정적 매니저만 다루고 View의 transform은 건드리지 않는다.
        /// </summary>
        /// <param name="reason">진행 사유 토큰(at-impact · cap · game-end).</param>
        public void ReleaseDeferredDeathPresentation(string reason)
        {
            if (!IsDeathPresentationDeferred) return;
            _deathDeferralFinished = true;
            GameObject ghost = _deathGhost;
            _deathGhost = null;
            Vector3 position = ghost != null ? ghost.transform.position : _deathGhostPosition;
            // 예전 사망 표현과 같은 순서: 차지 VFX 정지 → IsDead → 사망 VFX·SFX(규칙 15 — VFX와 짝) → 오브젝트 소멸.
            if (ghost is object) EffectManager.Instance?.StopGhostOwnedAttackEffects(ghost, "CasterDeath");
            if (_deathGhostAnimator != null)
            {
                _deathGhostAnimator.speed = 1f;
                _deathGhostAnimator.SetBool(AnimIsDead, true);
            }
            _deathGhostAnimator = null;
            EffectManager.Instance?.PlayUnitDeath(_deathGhostType, position);
            AudioManager.Instance?.PlayUnitDeathSfx(_deathGhostType);
            if (ghost != null) Destroy(ghost);
        }

        /// <summary>표현 없이 대역만 치운다(맵 재초기화·파괴 — DeferredUnitDeathPresenter가 부른다).</summary>
        public void DiscardDeferredDeathPresentation()
        {
            if (!IsDeathPresentationDeferred) return;
            _deathDeferralFinished = true;
            GameObject ghost = _deathGhost;
            _deathGhost = null;
            _deathGhostAnimator = null;
            if (ghost is object) EffectManager.Instance?.StopGhostOwnedAttackEffects(ghost, "DeathDiscarded");
            if (ghost != null) Destroy(ghost);
        }
    }
}
