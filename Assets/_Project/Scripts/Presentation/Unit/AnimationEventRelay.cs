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
        public void OnAttackHit()
        {
            if (_unitView != null)
                _unitView.OnAttackHit();
        }
    }
}
