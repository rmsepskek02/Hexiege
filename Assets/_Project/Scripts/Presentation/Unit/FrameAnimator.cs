// ============================================================================
// FrameAnimator.cs
// 스프라이트 프레임을 순환시키는 경량 애니메이션 엔진.
//
// Unity Animator를 사용하지 않는 이유 (PrototypePlan.md 참고):
//   - 1~2프레임 사이클에 Animator 상태머신은 과잉 (.anim, Controller, Transition 등)
//   - ScriptableObject에 스프라이트 배열 저장 → 드래그 앤 드롭으로 교체 가능
//   - ~50줄 코드로 전체 애니메이션 처리
//
// 동작 원리:
//   1. SetAnimation()으로 Sprite[] 배열과 FPS를 받음
//   2. Update()에서 경과 시간을 누적
//   3. 1/fps 초마다 다음 프레임으로 전환
//   4. 마지막 프레임 도달 시 처음으로 돌아감 (루프)
//
// 예시 (Walk E방향, 2프레임, 6fps):
//   프레임: [walk_e_01] [walk_e_02] [walk_e_01] [walk_e_02] ...
//   간격:      0.167s      0.167s      0.167s      0.167s
//
// 1프레임짜리 배열(Idle 등)을 넣으면 정지 이미지처럼 동작.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, SpriteRenderer).
// ============================================================================

using UnityEngine;

namespace Hexiege.Presentation
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class FrameAnimator : MonoBehaviour
    {
        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary> 이 오브젝트의 SpriteRenderer. sprite 속성을 교체하여 애니메이션. </summary>
        private SpriteRenderer _spriteRenderer;

        /// <summary> 현재 재생 중인 스프라이트 배열. null이면 애니메이션 없음. </summary>
        private Sprite[] _currentFrames;

        /// <summary> 현재 프레임 인덱스 (0부터 시작). </summary>
        private int _currentIndex;

        /// <summary> 프레임 전환 간격 (초). 1/fps로 계산. </summary>
        private float _frameDuration;

        /// <summary> 마지막 프레임 전환 이후 경과 시간 누적. </summary>
        private float _timer;

        // ====================================================================
        // 초기화
        // ====================================================================

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // ====================================================================
        // 애니메이션 설정
        // ====================================================================

        /// <summary>
        /// 새 스프라이트 배열로 애니메이션을 교체.
        /// 상태 전환(idle→walk) 또는 방향 전환 시 호출.
        ///
        /// 같은 배열을 다시 설정하면 무시 (불필요한 리셋 방지).
        /// 새 배열이면 프레임 인덱스를 0으로 리셋하고 즉시 첫 프레임 표시.
        /// </summary>
        /// <param name="frames">재생할 스프라이트 배열</param>
        /// <param name="fps">초당 프레임 수. 6이면 0.167초마다 전환.</param>
        public void SetAnimation(Sprite[] frames, float fps)
        {
            // 같은 배열이면 리셋하지 않음 (이동 중 매 프레임 호출되어도 안전)
            if (_currentFrames == frames) return;

            _currentFrames = frames;
            _currentIndex = 0;
            _timer = 0f;

            // fps → 프레임 간격 변환. 0 이하면 안전 기본값 사용.
            _frameDuration = fps > 0f ? 1f / fps : 0.167f;

            // 즉시 첫 프레임 표시
            if (_currentFrames != null && _currentFrames.Length > 0)
            {
                _spriteRenderer.sprite = _currentFrames[0];
            }
        }

        // ====================================================================
        // 프레임 순환
        // ====================================================================

        /// <summary>
        /// 매 프레임 경과 시간을 누적하여 다음 프레임으로 전환.
        /// 1프레임짜리 배열이면 전환 없이 정지 상태 유지.
        /// </summary>
        private void Update()
        {
            // 재생할 배열이 없거나 1프레임이면 전환 불필요
            if (_currentFrames == null || _currentFrames.Length <= 1) return;

            _timer += Time.deltaTime;

            // 경과 시간이 프레임 간격을 초과하면 다음 프레임으로
            if (_timer >= _frameDuration)
            {
                _timer -= _frameDuration;

                // 다음 프레임 인덱스. 마지막이면 0으로 돌아감 (루프).
                _currentIndex = (_currentIndex + 1) % _currentFrames.Length;
                _spriteRenderer.sprite = _currentFrames[_currentIndex];
            }
        }

        // ====================================================================
        // 유틸리티
        // ====================================================================

        /// <summary>
        /// 특정 프레임을 즉시 표시. 애니메이션 루프는 유지.
        /// 외부에서 강제로 특정 프레임을 보여줘야 할 때 사용.
        /// </summary>
        public void SetFrame(int index)
        {
            if (_currentFrames == null || _currentFrames.Length == 0) return;
            _currentIndex = Mathf.Clamp(index, 0, _currentFrames.Length - 1);
            _spriteRenderer.sprite = _currentFrames[_currentIndex];
            _timer = 0f;
        }
    }
}
