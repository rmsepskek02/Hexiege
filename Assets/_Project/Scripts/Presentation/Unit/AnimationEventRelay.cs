// ============================================================================
// AnimationEventRelay.cs
// Animator가 있는 자식 GameObject에 부착하여 Animation Event를 부모 UnitView에 전달.
//
// Unity Animation Event 시스템은 Animator 컴포넌트가 있는 동일 GameObject의
// MonoBehaviour에서만 이벤트 함수를 호출할 수 있으므로, 이 중계 컴포넌트가 필요.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour).
// ============================================================================

using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// Animation Event를 부모 UnitView로 중계하는 컴포넌트.
    /// Animator가 있는 Mesh 자식 오브젝트에 부착.
    /// </summary>
    public class AnimationEventRelay : MonoBehaviour
    {
        /// <summary> 부모 UnitView 캐시. </summary>
        private UnitView _unitView;

        private void Awake()
        {
            _unitView = GetComponentInParent<UnitView>();
        }

        /// <summary>
        /// Attack 애니메이션의 타격 프레임에서 Animation Event로 호출.
        /// </summary>
        public void OnAttackHit(AnimationEvent marker)
        {
            if (_unitView == null) _unitView = GetComponentInParent<UnitView>();
            if (_unitView != null)
                _unitView.OnAttackHit(marker);
        }

        // [P4 — 2026-10-19 차지 유닛 발사 시점] 옛 요약: "Fox의 charge 표식을 피해 표식과 구별하여 부모의 source 연출 경계로 전달한다."
        /// <summary>
        /// 「차지 시작」 Animation Event(OnAttackCharge)를 부모 UnitView로 전달한다 — 차지 유닛 공통
        /// (공격 클립에 이 이벤트가 찍힌 유닛: 현재 FoxMagician 1.0초, InfernoSpirit 0.1667초).
        /// 피해 표식(OnAttackHit)과 구별되며, UnitView는 이 신호에서 기 모으기(charge) VFX만 켠다.
        /// 투사체 발사·공격 SFX는 OnAttackHit에서 한다. 서버 피해 시점과는 무관하다(서버는 OnAttackHit 시간만 쓴다).
        /// </summary>
        public void OnAttackCharge(AnimationEvent marker)
        {
            if (_unitView == null) _unitView = GetComponentInParent<UnitView>();
            if (_unitView != null)
                _unitView.OnAttackCharge(marker);
        }
    }
}
