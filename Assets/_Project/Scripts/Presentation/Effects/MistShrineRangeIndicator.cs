// ============================================================================
// MistShrineRangeIndicator.cs
// MistShrine 패널이 열려 있는 동안 지도 위에 회복 범위를 보여 주는 반투명 원.
//
// 무엇을 하나(유니티 초급자용 설명):
//   내 MistShrine을 눌러 패널이 열리면, 그 건물 주위 땅바닥에 "여기까지 회복돼요"를 알려 주는
//   반투명한 원이 그려진다. 패널을 닫으면 즉시 사라진다.
//   적 MistShrine은 눌러도 패널 자체가 열리지 않으므로 이 원도 보이지 않는다
//   (상대에게 정보 우위를 주지 않기 위함 — GameSystemRules_UI.md MistShrine 규칙 8).
//
// 구성(2겹 — UI 규칙 12 "반투명 채움 + 외곽선"):
//   · Ring : 가장 큰 원(진한 색) — 아래에 깔려 바깥 테두리(림)로 보인다.
//   · Fill : 링보다 살짝 작은 원(반투명) — 채움 영역. 그래서 링의 바깥 림이 테두리처럼 남는다.
//   스킬 조준원(SkillAimReticle)과 같은 구조지만, MistShrine은 "조준"이 아니라 "고정 범위 안내"라
//   중앙 점(Dot)을 두지 않고 색상도 따로 갖는다.
//
// 렌더링 주의(GameSystemRules_Buildings.md MistShrine 규칙 27):
//   지면 데칼 셰이더 Hexiege/SkillAimOverlay(ZTest LEqual + Offset -1,-1 + ZWrite Off + Cull Off)를 재사용한다.
//   ⚠️ ZTest Always는 금지다 — 유닛·건물까지 뚫고 그려져 화면이 어지러워진다.
//
// 배치(에디터 셋업 스크립트가 자동 생성·배선):
//   루트 GameObject를 X축 90도로 눕혀(지도 XZ 평면에 평행) 자식 SpriteRenderer 2개(Ring/Fill)를 둔다.
//
// Presentation 레이어 — Unity MonoBehaviour 의존.
// ============================================================================

using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// MistShrine 회복 범위(반투명 채움 + 외곽선)를 지도 위에 표시하는 컴포넌트.
    /// </summary>
    public sealed class MistShrineRangeIndicator : MonoBehaviour
    {
        // ====================================================================
        // 렌더러(2겹) — 셋업 스크립트가 자동 배선
        // ====================================================================

        [Header("렌더러(2겹: Ring 아래 / Fill 위)")]
        [Tooltip("바깥 테두리(림)로 보이는 가장 큰 원 스프라이트.")]
        [SerializeField] private SpriteRenderer _ringRenderer;

        [Tooltip("반투명 채움 원 스프라이트(링보다 살짝 작다).")]
        [SerializeField] private SpriteRenderer _fillRenderer;

        [Header("오버레이 머티리얼(지면 데칼)")]
        [Tooltip("범위 원이 3D 지형·타일에 파묻히지 않도록 하는 전용 머티리얼(셋업 스크립트가 자동 배선). " +
                 "ZTest LEqual + Offset -1,-1 로 지면 z-fighting을 이기되, 유닛·건물(불투명·더 앞)에는 가려진다. " +
                 "비어 있으면 SpriteRenderer 기본 머티리얼로 폴백(지형에 파묻힐 수 있음).")]
        [SerializeField] private Material _overlayMaterial;

        // ====================================================================
        // 색상 / 크기 — Inspector 조절
        //   ⚠️ 색상·투명도·테두리 두께는 UI 규칙 12에서 "미확정"이며 아트 단계에서 확정한다.
        //      아래 값은 물안개를 연상시키는 청록 계열 임시값이다.
        // ====================================================================

        [Header("색상(임시값 — 아트 단계에서 확정)")]
        [Tooltip("채움 색(반투명). 기본 (0.35, 0.75, 0.9, 0.20).")]
        [SerializeField] private Color _fillColor = new Color(0.35f, 0.75f, 0.9f, 0.20f);

        [Tooltip("외곽선(링) 색(더 진한 색). 기본 (0.4, 0.85, 1.0, 0.85).")]
        [SerializeField] private Color _edgeColor = new Color(0.4f, 0.85f, 1.0f, 0.85f);

        [Header("크기(시각 튜닝)")]
        [Tooltip("전체 크기 배수. Show의 radius(gameplay) 위에 곱해지는 시각 튜닝값.")]
        [SerializeField] private float _visualMultiplier = 1f;

        [Tooltip("외곽선 두께 비율(0~1). Fill이 이 비율만큼 작아져 링의 바깥 림이 테두리로 보인다.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float _ringThickness = 0.08f;

        [Header("배치")]
        [Tooltip("지면 z-fighting 회피용 Y 높이 오프셋.")]
        [SerializeField] private float _yOffset = 0.05f;

        [Tooltip("링 스프라이트의 기준 지름(월드, localScale 1일 때). 반경 r일 때 스케일=(2r)/BaseDiameter. " +
                 "내장 스프라이트 크기에 맞춰 Inspector에서 튜닝한다.")]
        [SerializeField] private float _baseDiameter = 1.0f;

        // ====================================================================
        // 표시 요청 플래그 — "Awake 자기 비활성화" 함정 방지 (유니티 초급자용 상세 설명)
        //
        // 유니티는 비활성(SetActive(false)) 상태로 씬에 저장된 GameObject의 Awake를 실행하지 않는다.
        // 그 상태에서 런타임에 Show()가 SetActive(true)를 호출하면, 바로 그 순간 Awake가 뒤늦게
        // 처음 실행된다. 예전 코드의 Awake는 무조건 gameObject.SetActive(false)를 했기 때문에
        // "켜자마자 스스로 다시 꺼지는" 현상이 일어나 원이 영영 보이지 않았다.
        //   (Show()의 나머지 줄 — 회전·위치·ApplyVisual — 은 그대로 돌지만 오브젝트가 꺼져 있어 무의미하다.)
        //
        // 해결: Show()가 SetActive(true)보다 "먼저" 이 플래그를 세운다.
        //       늦게 도는 Awake는 플래그를 보고 "시작 시 숨김"을 건너뛴다.
        //       → 씬에 활성으로 저장돼 있든 비활성으로 저장돼 있든 Show()는 항상 보이게 만든다.
        //
        // ⚠️ [SerializeField]를 붙이지 않는다 — 씬 파일에 저장되면 안 되는 순수 런타임 상태다.
        // ====================================================================
        private bool _showRequested;

        private void Awake()
        {
            // 지도(XZ 평면)에 눕도록 X축 90도 회전(스프라이트는 기본 XY 평면 → 눕혀야 바닥에 깔린다).
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // 시작 시 숨김(패널이 열릴 때만 보인다 — UI 규칙 8).
            // 단, Show()가 이미 표시를 요청한 뒤에 늦게 도는 Awake라면 끄지 않는다(위 설명 참조).
            if (!_showRequested) gameObject.SetActive(false);
        }

        /// <summary>
        /// 회복 범위 원을 지정 위치에 지정 반경으로 표시한다.
        /// </summary>
        /// <param name="worldPos">원 중심의 월드 좌표(화면에 보이는 그대로의 "뷰" 좌표).</param>
        /// <param name="radius">회복 범위 반경(월드 단위, MistShrineUseCase와 같은 기준).</param>
        public void Show(Vector3 worldPos, float radius)
        {
            // ⚠️ 순서 중요: SetActive(true)가 Awake를 그 자리에서 실행시키므로,
            //    플래그를 반드시 "먼저" 세워야 Awake가 자기 자신을 끄지 않는다.
            _showRequested = true;
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            // 지도에 눕는 회전 보장(Awake를 못 탄 경우 대비) + 위치 갱신.
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            transform.position = new Vector3(worldPos.x, _yOffset, worldPos.z);

            ApplyVisual(radius);
        }

        /// <summary> 범위 원을 즉시 숨긴다(패널 닫힘 — UI 규칙 8 "패널을 닫으면 즉시 사라진다"). </summary>
        public void Hide()
        {
            // 아직 Awake가 돌지 않은 상태에서 Hide()가 먼저 불릴 수도 있으므로 플래그도 함께 내린다
            // (그래야 나중에 도는 Awake가 "시작 시 숨김"을 정상 수행한다).
            _showRequested = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>
        /// 반경·색을 두 겹 렌더러에 적용한다. Ring(전체) → Fill(테두리 두께만큼 작게) 순으로 위에 그린다.
        /// </summary>
        /// <param name="radius">회복 범위 반경(월드 단위).</param>
        private void ApplyVisual(float radius)
        {
            // 지름 = 2 × 반경 × 전체 배수. 링 기준 스프라이트 지름 대비 스케일.
            float diameter = Mathf.Max(0f, radius) * 2f * Mathf.Max(0f, _visualMultiplier);
            float baseScale = _baseDiameter > 0.0001f ? diameter / _baseDiameter : diameter;

            ApplyRenderer(_ringRenderer, baseScale, _edgeColor, sortingOrder: 0);
            ApplyRenderer(_fillRenderer, baseScale * Mathf.Clamp01(1f - _ringThickness), _fillColor, sortingOrder: 1);
        }

        /// <summary>
        /// 한 겹 렌더러의 스케일·색·정렬순서를 적용하고, 오버레이 머티리얼이 배선돼 있으면 그것으로 렌더되게 보장한다.
        /// </summary>
        /// <param name="r">적용할 스프라이트 렌더러(null이면 무시).</param>
        /// <param name="scale">원 지름 스케일(루트가 X축 90도로 눕어 있어 로컬 XY = 월드 XZ).</param>
        /// <param name="color">적용할 색.</param>
        /// <param name="sortingOrder">겹침 순서(작을수록 아래).</param>
        private void ApplyRenderer(SpriteRenderer r, float scale, Color color, int sortingOrder)
        {
            if (r == null) return;
            r.transform.localScale = new Vector3(scale, scale, 1f);
            r.color = color;
            r.sortingOrder = sortingOrder;

            // 오버레이 머티리얼 자가 배선(셋업 스크립트가 못 물린 경우 대비 — 런타임 안전장치).
            if (_overlayMaterial != null && r.sharedMaterial != _overlayMaterial)
                r.sharedMaterial = _overlayMaterial;
        }
    }
}
