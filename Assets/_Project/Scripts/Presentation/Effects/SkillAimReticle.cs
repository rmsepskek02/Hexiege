// ============================================================================
// SkillAimReticle.cs
// 스킬 지점 조준 시 착탄 범위(원형 반경)를 월드에 표시하는 조준점(플레이스홀더 아트).
//
// 무엇을 하나(초급자용 설명):
//   지점 지정 스킬(타입 A·B)을 누른 채 드래그하면, 손가락(조준점)이 가리키는 지점에 "이 정도
//   범위가 맞는다"는 원을 보여준다. 아트는 플레이스홀더(단순 도형/스프라이트)이며, 이 스크립트는
//   "어디에·얼마 크기로 보여줄지"의 로직만 담당한다(규칙 17).
//
// 사용:
//   조준 컨트롤러(SkillAimController)가 매 프레임 Show(worldPos, radius)를 호출해 위치·크기를 갱신하고,
//   조준이 끝나면 Hide()를 호출한다.
//
// 배치(사용자 Unity 작업):
//   빈 GameObject에 이 스크립트를 부착하고, 자식으로 링(원) 메시/스프라이트를 둔 뒤 _ring에 연결한다.
//   _ring은 지름이 1(반경 0.5)인 기준 크기로 만들면 SetRadius가 정확히 스케일된다.
//   (플레이스홀더: 반경 기준으로 XZ 평면에 놓이는 단색 원판/링이면 충분.)
//
// Presentation 레이어 — Unity MonoBehaviour 의존.
// ============================================================================

using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 스킬 조준 범위 원을 월드에 표시하는 조준점(플레이스홀더). 위치·반경만 제어한다.
    /// </summary>
    public sealed class SkillAimReticle : MonoBehaviour
    {
        [Header("링(원) 트랜스폼")]
        [Tooltip("범위를 나타내는 원 메시/스프라이트의 트랜스폼. 지름 1(반경 0.5) 기준 크기로 제작하면 스케일이 정확하다. " +
                 "비워두면 이 오브젝트 자신을 스케일한다.")]
        [SerializeField] private Transform _ring;

        [Tooltip("링이 XZ 평면 위에 떠 보이도록 하는 Y 높이 오프셋(지면 z-fighting 회피).")]
        [SerializeField] private float _yOffset = 0.05f;

        [Tooltip("_ring 기준 지름(월드). 제작한 원의 실제 지름을 넣는다. 반경 r일 때 스케일 = (2r)/BaseDiameter.")]
        [SerializeField] private float _baseDiameter = 1.0f;

        private void Awake()
        {
            // 시작 시 숨긴 상태로 둔다(조준 시작 때만 보인다).
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 조준점을 지정 월드 위치에 지정 반경으로 표시한다.
        /// </summary>
        /// <param name="worldPos">조준점 중심 월드 좌표(뷰 좌표계).</param>
        /// <param name="radius">범위 반경(월드 단위).</param>
        public void Show(Vector3 worldPos, float radius)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            transform.position = new Vector3(worldPos.x, _yOffset, worldPos.z);
            SetRadius(radius);
        }

        /// <summary>
        /// 조준점을 숨긴다.
        /// </summary>
        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>
        /// 링 스케일을 반경에 맞춘다. XZ 평면이므로 X·Z를 지름 비율로 스케일한다(Y는 유지).
        /// </summary>
        /// <param name="radius">범위 반경(월드 단위).</param>
        private void SetRadius(float radius)
        {
            Transform target = _ring != null ? _ring : transform;

            // 지름 = 2 × 반경. 제작 기준 지름(_baseDiameter) 대비 스케일 배율.
            float diameter = Mathf.Max(0f, radius) * 2f;
            float scale = _baseDiameter > 0.0001f ? diameter / _baseDiameter : diameter;

            Vector3 s = target.localScale;
            target.localScale = new Vector3(scale, s.y, scale);
        }
    }
}
