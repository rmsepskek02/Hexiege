using Hexiege.Application;
using Hexiege.Core;
using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// Reads the authoritative Simulation Root pose and projects it onto the visual hierarchy.
    /// This component never writes to its own transform.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisualRootProjector : MonoBehaviour
    {
        [SerializeField] private Transform _visualRoot;

        /// <summary>
        /// Read-only diagnostic seam for the development observer.
        /// Unlike the public presentation getters, this accessor deliberately does not call
        /// ProjectNow: sampling it can therefore never repair or otherwise hide a bad pose.
        /// The returned Transform must be treated as immutable by every caller.
        /// </summary>
        internal Transform ObservedVisualRoot => _visualRoot;

        /// <summary>
        /// Update/Animation Event 중 표현 pose를 읽는 소비자는 LateUpdate보다 먼저 실행될 수 있다.
        /// getter에서 현재 canonical root를 즉시 투영하여 같은 프레임의 절대 pose를 반환한다.
        /// </summary>
        public Transform PresentationTransform
        {
            get
            {
                ProjectNow();
                return _visualRoot != null ? _visualRoot : transform;
            }
        }

        public Vector3 PresentationPosition
        {
            get
            {
                ProjectNow();
                return _visualRoot != null ? _visualRoot.position : transform.position;
            }
        }

        public Quaternion PresentationRotation
        {
            get
            {
                ProjectNow();
                return _visualRoot != null ? _visualRoot.rotation : transform.rotation;
            }
        }

        /// <summary>
        /// [2026-10-26 수정안 (a)] 사망 표현 보류용 — Visual Root(메시·Animator가 든 자식)를 이 투영기에서 떼어 돌려준다.
        /// 뗀 뒤에는 이 투영기가 더 이상 그 Transform을 쓰지 않는다(_visualRoot = null). 그래서 떼어 낸 "사망 대역(ghost)"이
        /// 서버 Root(NetworkTransform이 계속 움직이는 쪽)를 따라 움직이지 않고, 죽은 그 자리·그 자세에 그대로 남는다.
        /// 이후 PresentationTransform/Position은 Root 자체를 돌려준다(기존 "Visual Root 없음" 폴백과 같다).
        /// Visual Root가 없거나 Root 자신이면 null(떼어 낼 대역이 없다 — 호출자는 사망 표현을 미루지 않는다).
        /// </summary>
        /// <returns>떼어 낸 Visual Root. 실제 부모 분리(SetParent)는 호출자가 한다.</returns>
        internal Transform DetachVisualRootForDeathGhost()
        {
            if (_visualRoot == null || _visualRoot == transform) return null;
            ProjectNow();   // 마지막 화면 pose(Red 반전 포함)를 한 번 더 써 둔 뒤 뗀다.
            Transform detached = _visualRoot;
            _visualRoot = null;
            return detached;
        }

        /// <summary>[2026-10-26 수정안 (a)] 떼어 낼 Visual Root가 있는가(읽기 전용 — 상태 불변).</summary>
        internal bool HasDetachableVisualRoot => _visualRoot != null && _visualRoot != transform;

        private void LateUpdate()
        {
            // NetworkTransform/UnitView가 Update 중 root를 쓴 뒤 렌더 직전 최종 투영한다.
            ProjectNow();
        }

        /// <summary>
        /// canonical Simulation Root에서 로컬 화면의 absolute visual pose를 즉시 계산한다.
        /// 이 메서드는 자신의 transform(Simulation Root)을 절대 쓰지 않으며 Visual Root만 쓴다.
        /// 여러 presentation 소비자가 같은 프레임 호출해도 같은 입력에 같은 pose를 쓰므로 멱등이다.
        /// </summary>
        public void ProjectNow()
        {
            if (_visualRoot == null)
                return;

            Vector3 projectedPosition = transform.position;
            Quaternion projectedRotation = transform.rotation;

            // Multiplayer clients receive the canonical Simulation Root. Team-relative
            // presentation is applied only to VisualRoot; host/server and single-player
            // retain the existing canonical/root writer behaviour.
            if (NetworkContext.IsNetworkActive
                && !NetworkContext.IsNetworkServer
                && ViewConverter.IsFlipped)
            {
                projectedPosition = ViewConverter.ToView(projectedPosition);
                projectedRotation =
                    Quaternion.Euler(0f, 180f, 0f) * projectedRotation;
            }

            _visualRoot.SetPositionAndRotation(projectedPosition, projectedRotation);
        }
    }
}
