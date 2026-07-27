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
