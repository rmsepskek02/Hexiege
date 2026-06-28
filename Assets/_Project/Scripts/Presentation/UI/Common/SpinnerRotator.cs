// ============================================================================
// SpinnerRotator.cs
// 오브젝트를 매 프레임 Z축으로 회전시키는 단순 컴포넌트.
//
// 용도:
//   LoadingIndicator 프리팹의 Spinner 오브젝트에 부착하여
//   로딩 중 스피너 아이콘이 회전하도록 한다.
//   (기존 LoadingScreen.cs의 Update()가 담당하던 회전 로직을 분리한 컴포넌트)
//
// 주의:
//   - 일시정지(Time.timeScale = 0) 시에도 회전이 유지되도록 unscaledDeltaTime을 사용한다.
//     (인게임 포즈 중 로딩 스피너가 멈추지 않아야 하는 경우를 고려)
//
// Presentation 레이어 — MonoBehaviour 의존.
// ============================================================================

using UnityEngine;

namespace Hexiege.Presentation.UI
{
    /// <summary>
    /// Z축 회전 스피너. LoadingIndicator 프리팹의 Spinner 자식에 부착한다.
    /// </summary>
    public class SpinnerRotator : MonoBehaviour
    {
        [Tooltip("초당 회전 각도(degree). 양수 = 반시계, 음수 = 시계 방향.")]
        [SerializeField] private float _spinSpeed = -180f;

        private void Update()
        {
            // unscaledDeltaTime: Time.timeScale=0 일시정지 중에도 회전 유지.
            transform.Rotate(0f, 0f, _spinSpeed * Time.unscaledDeltaTime);
        }
    }
}
