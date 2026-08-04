// ============================================================================
// SkillAimReticle.cs
// 스킬 지점 조준 시 착탄 범위(AoE)를 월드 지도 위에 표시하는 조준원.
//
// 도식 룩(초급자용 설명):
//   착탄 범위를 "반투명하게 채워진 원 + 뚜렷한 테두리(링) + 중앙 점"으로 보여준다.
//   세 겹의 스프라이트로 구성한다:
//     · Ring : 가장 큰 원(진한 붉은) — 아래에 깔려 바깥 테두리(림)로 보인다.
//     · Fill : 링보다 살짝 작은 원(반투명 붉은) — 채움 영역. 그래서 링의 바깥 림이 테두리처럼 남는다.
//     · Dot  : 아주 작은 중앙 점(진한 붉은).
//   원뿐 아니라 가로·세로 스케일(_sizeScale)을 다르게 주면 타원으로도 표시된다.
//
// 크기 기준:
//   착탄 반경은 스킬 데이터의 radius(gameplay)가 기준이다. Show(pos, radius)의 radius로 기본 크기를 잡고,
//   Inspector의 _visualMultiplier(전체 배수)·_sizeScale(가로/세로 = 타원)로 시각 튜닝만 얹는다.
//
// 사용:
//   SkillAimController가 Show(worldPos, radius)로 위치·크기를 갱신하고, 조준 종료 시 Hide()를 부른다.
//   (호출부 시그니처는 기존과 동일 — 표시 로직만 재구성.)
//
// 배치(사용자 Unity 작업 / 셋업 스크립트 자동):
//   루트 GameObject를 X축 90도로 눕혀(지도 XZ 평면에 평행) 자식 SpriteRenderer 3개(Ring/Fill/Dot)를 둔다.
//   각 SpriteRenderer를 _ringRenderer/_fillRenderer/_dotRenderer에 연결한다(셋업 스크립트가 자동 배선).
//
// Presentation 레이어 — Unity MonoBehaviour 의존.
// ============================================================================

using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 스킬 착탄 범위(반투명 채움 + 링 테두리 + 중앙 점)를 월드에 표시하는 조준원. 원/타원·색·크기 조절.
    /// </summary>
    public sealed class SkillAimReticle : MonoBehaviour
    {
        // ====================================================================
        // 렌더러(3겹) — 셋업 스크립트가 자동 배선
        // ====================================================================

        [Header("렌더러(3겹: Ring 아래 / Fill 중간 / Dot 위)")]
        [Tooltip("바깥 테두리(림)로 보이는 가장 큰 원 스프라이트.")]
        [SerializeField] private SpriteRenderer _ringRenderer;

        [Tooltip("반투명 채움 원 스프라이트(링보다 살짝 작다).")]
        [SerializeField] private SpriteRenderer _fillRenderer;

        [Tooltip("중앙 점 스프라이트.")]
        [SerializeField] private SpriteRenderer _dotRenderer;

        [Header("오버레이 머티리얼(지형 관통 표시)")]
        [Tooltip("조준원이 3D 지형·타일에 파묻히지 않도록 하는 전용 머티리얼(셋업 스크립트가 자동 배선). " +
                 "ZTest LEqual + Offset -1,-1 로 지면 z-fighting을 이겨 항상 위에 그려지되, 유닛·건물(불투명·더 앞)에는 가려진다. " +
                 "비어 있으면 SpriteRenderer 기본 머티리얼(Sprites/Default)로 폴백(지형에 파묻힐 수 있음).")]
        [SerializeField] private Material _overlayMaterial;

        // ====================================================================
        // 색상 — Inspector 조절(기본 = 도식 붉은)
        // ====================================================================

        [Header("색상")]
        [Tooltip("채움 색(반투명 붉은). 기본 (0.8, 0.2, 0.2, 0.25).")]
        [SerializeField] private Color _fillColor = new Color(0.8f, 0.2f, 0.2f, 0.25f);

        [Tooltip("링 테두리·중앙 점 색(더 진한 붉은). 기본 (0.85, 0.15, 0.15, 0.9).")]
        [SerializeField] private Color _edgeColor = new Color(0.85f, 0.15f, 0.15f, 0.9f);

        // ====================================================================
        // 크기 / 타원 — Inspector 조절
        // ====================================================================

        [Header("크기(시각 튜닝)")]
        [Tooltip("전체 크기 배수. Show의 radius(gameplay) 위에 곱해지는 시각 튜닝값.")]
        [SerializeField] private float _visualMultiplier = 1f;

        [Tooltip("가로(x)·세로(z) 스케일. 값이 같으면 원, 다르면 타원. 기본 (1,1)=원.")]
        [SerializeField] private Vector2 _sizeScale = Vector2.one;

        [Tooltip("링 테두리 두께 비율(0~1). Fill이 이 비율만큼 작아져 링의 바깥 림이 테두리로 보인다.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float _ringThickness = 0.14f;

        [Tooltip("중앙 점 크기 비율(전체 지름 대비, 0~1).")]
        [Range(0f, 1f)]
        [SerializeField] private float _dotScale = 0.14f;

        [Header("배치")]
        [Tooltip("지면 z-fighting 회피용 Y 높이 오프셋.")]
        [SerializeField] private float _yOffset = 0.05f;

        [Tooltip("링 스프라이트의 기준 지름(월드, localScale 1일 때). 반경 r일 때 스케일=(2r)/BaseDiameter. " +
                 "내장 스프라이트 크기에 맞춰 Inspector에서 튜닝한다.")]
        [SerializeField] private float _baseDiameter = 1.0f;

        private void Awake()
        {
            // 지도(XZ 평면)에 눕도록 X축 90도 회전(스프라이트는 기본 XY 평면 → 눕혀야 바닥에 깔린다).
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            // 시작 시 숨김(조준 시작 때만 보인다).
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 조준원을 지정 월드 위치에 지정 반경(gameplay)으로 표시한다. 시각 튜닝(_visualMultiplier/_sizeScale)이 얹힌다.
        /// </summary>
        /// <param name="worldPos">조준원 중심 월드 좌표(뷰 좌표계).</param>
        /// <param name="radius">착탄 반경(월드 단위, 스킬 데이터 기준).</param>
        public void Show(Vector3 worldPos, float radius)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            // 지도에 눕는 회전 보장(Awake를 못 탄 경우 대비) + 위치 갱신.
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            transform.position = new Vector3(worldPos.x, _yOffset, worldPos.z);

            ApplyVisual(radius);
        }

        /// <summary>
        /// 조준원을 숨긴다.
        /// </summary>
        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>
        /// 반경·타원·색을 세 겹 렌더러에 적용한다.
        /// Ring(전체) → Fill(테두리 두께만큼 작게) → Dot(중앙 점) 순으로 위에 그린다.
        /// </summary>
        /// <param name="radius">착탄 반경(월드 단위).</param>
        private void ApplyVisual(float radius)
        {
            // 지름 = 2 × 반경 × 전체 배수. 링 기준 스프라이트 지름 대비 스케일.
            float diameter = Mathf.Max(0f, radius) * 2f * Mathf.Max(0f, _visualMultiplier);
            float baseScale = _baseDiameter > 0.0001f ? diameter / _baseDiameter : diameter;

            // 가로/세로 스케일(타원). 값이 같으면 원.
            Vector2 full = new Vector2(baseScale * _sizeScale.x, baseScale * _sizeScale.y);

            // Ring: 전체 크기, 진한 색, 맨 아래. 바깥 림이 테두리로 남는다.
            ApplyRenderer(_ringRenderer, full, _edgeColor, sortingOrder: 0);
            // Fill: 테두리 두께만큼 작게, 반투명 채움, 중간.
            ApplyRenderer(_fillRenderer, full * Mathf.Clamp01(1f - _ringThickness), _fillColor, sortingOrder: 1);
            // Dot: 아주 작게, 진한 색, 맨 위(중앙 점).
            ApplyRenderer(_dotRenderer, full * Mathf.Clamp01(_dotScale), _edgeColor, sortingOrder: 2);
        }

        /// <summary>
        /// 한 겹 렌더러의 스케일(XY = 월드 XZ, 루트 90도 회전 상태)·색·정렬순서를 적용하고,
        /// 오버레이 머티리얼이 배선돼 있으면 그것으로 렌더되게 보장한다(지형 관통 표시).
        /// </summary>
        private void ApplyRenderer(SpriteRenderer r, Vector2 scaleXY, Color color, int sortingOrder)
        {
            if (r == null) return;
            // 루트가 X축 90도로 눕어 있으므로 자식의 로컬 X→월드 X, 로컬 Y→월드 Z가 된다(타원 가로/세로).
            r.transform.localScale = new Vector3(scaleXY.x, scaleXY.y, 1f);
            r.color = color;
            r.sortingOrder = sortingOrder;

            // 오버레이 머티리얼 자가 배선(셋업 스크립트가 못 물린 경우 대비 — 런타임 안전장치).
            //   지형·타일에 파묻히지 않고 항상 선명하게 보이도록 하는 전용 셰이더(ZTest LEqual + Offset -1,-1).
            if (_overlayMaterial != null && r.sharedMaterial != _overlayMaterial)
                r.sharedMaterial = _overlayMaterial;
        }
    }
}
