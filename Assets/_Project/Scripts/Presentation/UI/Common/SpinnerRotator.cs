// ============================================================================
// SpinnerRotator.cs
// 오브젝트를 매 프레임 Z축으로 회전시키는 단순 컴포넌트.
//
// 용도:
//   LoadingIndicator 프리팹의 Spinner 오브젝트에 부착하여
//   로딩 중 스피너 아이콘이 회전하도록 한다.
//   (같은 회전 일을 하는 코드가 LoadingScreen.cs 의 Update() 에도 따로 남아 있다 —
//    그쪽은 일시정지 중에는 멈추는 시간 기준을 쓰고, 이 컴포넌트는 아래 주의대로
//    일시정지와 무관한 시간 기준을 쓴다. 먼저 있던 쪽은 LoadingScreen.cs 의 회전이다 —
//    이 컴포넌트가 처음 생긴 2026-06-18 커밋(691248f8) 직전 판에 이미 있었다. 그 커밋 메시지는
//    이 컴포넌트를 앞의 회전 로직을 떼어 낸 것이라고 적었으나, LoadingScreen.cs 쪽 회전은
//    그때 지워지지 않고 남았다.)
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
