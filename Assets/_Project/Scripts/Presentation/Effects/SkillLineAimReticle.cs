// ============================================================================
// SkillLineAimReticle.cs
// 공습(직선 연속 폭격) 조준 중 땅바닥에 "직사각형 범위 + 방향 화살표"를 그려 주는 표시물.
// (2026-11-17 지원 스킬 2-L 공습 Task L-4 — _Tasks/2026-11-17/09_12_support-skill-air-strike, Plan P-5 · U-4 · U-14 · 규칙 22-1)
//
// 무엇을 하나(유니티 초급자용 설명):
//   · 시작점(플레이어가 누른 자리)에서 방향(끈 쪽)으로 길이만큼 뻗은 직사각형을 그린다.
//       - 채움  : 반투명 붉은색 — 폭격이 떨어지는 범위.
//       - 테두리: 진한 붉은 띠 4개 — 범위 끝이 어디인지 또렷하게.
//       - 화살표: 시작점 → 끝 쪽을 가리키는 밝은 화살(가운데 막대 + 끝의 삼각형) — 방향(U-4).
//   · 길이는 「맵 안 유효 길이」다(U-14) — 직선이 맵 밖으로 나가면 맵 끝에서 잘린 길이만 그린다.
//     그 길이는 호출자(SkillAimController)가 서버와 "같은 순수 함수"(LineStrikeWorld.ComputeEffectiveLength)로 구해 넘긴다
//     (Plan R-14 — 화면과 서버가 따로 계산해 어긋나지 않게). 이 컴포넌트는 숫자를 직접 계산하지 않고 받은 대로 그린다.
//   · 폭은 스킬 데이터의 「직선 폭」(LineWidth) 그대로다.
//   · 월드 공간에 깔리므로 화면을 줌하면 함께 커지고 작아진다(조준 원과 같다 — U-15).
//
// 새 아트 에셋 없이 만든다(방어탑 범위 표시 TowerRangeIndicator 선례):
//   모양은 코드로 만든 메시 하나(정점 27개 · 삼각형 13개, 정점 수가 늘 같아 배열을 재사용한다 — 매 프레임 할당 없음),
//   색은 메시의 정점 색이다. 머티리얼은 Resources 의 NoBuildHatch.mat(지면 데칼 셰이더 Hexiege/SkillAimOverlay)을 "복사"해 쓴다.
//     · 그 셰이더는 ZTest LEqual + Offset −1,−1 + ZWrite Off 라 타일 윗면에 파묻히지 않고, 유닛 · 건물에는 가려진다
//       (스킬 규칙 22-1 — 원형 조준원과 같은 가림 관계). ZTest Always 는 쓰지 않는다.
//   씬 · 프리팹 수정이 필요 없다 — 조준 컨트롤러가 처음 필요할 때 CreateRuntime()으로 빈 GameObject 를 만들어 붙인다.
//
// 높이: 타일 윗면은 Y 0.05 이고 불투명이라 그보다 낮으면 가려진다. 범위 표시 · 스킬 VFX(0.06) 선례대로 0.06 위에 깐다.
//
// ── 2026-11-18 단계 L-7 조준 UI 개선(Plan 14절 UI-1 · UI-2c · UI-2d) — 위 설명 중 바뀐 것 ──
//   · 채움: 단색 → 가운데 옅고 양옆 가장자리로 갈수록 진한 그라데이션(원형 조준원과 같은 느낌).
//   · 화살표: 가운데 막대를 없애고, 시작점 → 끝 쪽으로 흐르는 쉐브론(>>>) 여러 개 + 끝의 화살촉으로 방향을 보인다(UI-2c).
//   · 폭격 눈금: 폭격 횟수(LineStrikeCount)만큼 직선을 가로지르는 눈금 — 각 폭격이 떨어지는 자리(화면 폭격 VFX 와 같은 식).
//     맵 밖이라 서버가 건너뛰는 폭격의 눈금은 그리지 않는다(UI-2c). 눈금 위치는 SkillAimVisualMath.TryGetStrikeTickAlong 가 정한다.
//   · 상태 색(UI-2d): 고스트(공습 첫 화면 — 흐릿한 빨강, UI-1) · 발사 가능(빨강) · 최소 드래그 미만(회색/흰색) · X 위(회색으로 흐려짐).
//     정점 색에는 흰색 + 알파(모양)만 넣고, 색은 머티리얼 2개(주색 · 강조색)의 _Color 로 바꾼다(SkillAimVisualStyle) — 메시 재생성 없음.
//   · 등장(UI-2d): 나타날 때 시작점을 중심으로 작게 시작해 커진다. 고스트 → 활성(첫 터치)일 때도 한 번 더 튼다(PlayAppear).
//   · 메시 = 정점 고정 개수 · 하위 메시 2개(0 = 채움 · 테두리 · 눈금 — 주색, 1 = 화살촉 · 쉐브론 — 강조색). 쉐브론만 매 프레임
//     정점 위치 · 알파를 고쳐 넣는다(미리 만든 배열 재사용 — 할당 없음).
//   (2026-11-17 L-4 의 옛 구현 — 단색 상수 · 막대 화살표 · 정점 27개 메시 — 을 대체했다. 머티리얼 생성은 SkillAimVisualStyle 로 옮겼다.)
//
// Presentation 레이어 — Unity MonoBehaviour 의존. 화면 전용(판정에 아무 영향 없음).
// ============================================================================

using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 공습 직선 조준 표시(그라데이션 채움 + 테두리 + 폭격 눈금 + 흐르는 쉐브론 + 화살촉). 위치 · 방향은 화면(뷰) 좌표로 받는다.
    /// </summary>
    public sealed class SkillLineAimReticle : MonoBehaviour
    {
        // ====================================================================
        // 모양 상수(화면 전용 — 판정과 무관. 아트 단계에서 바꿀 수 있는 임시값)
        // ====================================================================

        /// <summary> 지면 위 높이(월드 Y). 타일 윗면 0.05 보다 0.01 위(범위 표시 · 스킬 VFX 선례). </summary>
        private const float GroundY = 0.06f;

        /// <summary> 테두리 띠 두께(월드 m). 직사각형 안쪽으로 그린다 — 바깥 끝 = 정확히 판정 범위. </summary>
        private const float BorderThickness = 0.05f;

        /// <summary> 겹친 층(채움 → 테두리 → 화살표)이 깜빡이지 않도록 층마다 아주 조금씩 높이를 다르게 둔다(월드 m). </summary>
        private const float LayerStep = 0.0005f;

        /// <summary> 이보다 짧은 길이는 "그릴 것이 없다"로 보고 렌더러를 끈다(맵 경계 위에서 바깥쪽을 향한 경우 — 유효 길이 0). </summary>
        private const float MinDrawLength = 0.01f;

        /// <summary> 화살촉 반폭 = 직선 폭 × 이 비율. </summary>
        private const float HeadHalfWidthRatio = 0.28f;

        /// <summary> 화살촉 길이 = min(직선 폭 × 이 비율, 길이 × HeadMaxLengthRatio). </summary>
        private const float HeadLengthWidthRatio = 0.55f;

        /// <summary> 화살촉 길이의 상한(직선 길이 대비). 짧은 직선에서 화살촉이 직사각형 밖으로 나가지 않게. </summary>
        private const float HeadMaxLengthRatio = 0.4f;

        // ── [2026-11-18 L-7] 새 모양 상수 ──

        /// <summary> 채움 그라데이션 정점 알파 — 양옆 가장자리(테두리 바로 안) · 중간(반폭의 절반) · 가운데. </summary>
        private const float FillEdgeAlpha = 0.36f;
        private const float FillMidAlpha = 0.12f;
        private const float FillCenterAlpha = 0.03f;

        /// <summary> 테두리 띠 알파. </summary>
        private const float BorderAlpha = 0.95f;

        /// <summary> 폭격 눈금 두께(월드 m, 진행 방향)와 알파. </summary>
        private const float TickThickness = 0.07f;
        private const float TickAlpha = 0.85f;

        /// <summary> 그릴 수 있는 폭격 눈금 최대 수(정점 고정용) = 스킬 데이터가 허용하는 최대 폭격 횟수(SkillData.MaxLineStrikeCount — 20). </summary>
        private const int MaxTicks = Hexiege.Application.SkillData.MaxLineStrikeCount;

        /// <summary> 쉐브론 사이 간격(월드 m)과 흐르는 속도(월드 m/초). </summary>
        private const float ChevronSpacing = 0.55f;
        private const float ChevronSpeed = 1.1f;

        /// <summary> 쉐브론 모양 — 반폭 = (직선 반폭 − 테두리) × 이 비율, 깊이(꺾인 정도) = 직선 폭 × 이 비율, 팔 두께(월드 m). </summary>
        private const float ChevronHalfWidthRatio = 0.5f;
        private const float ChevronDepthRatio = 0.2f;
        private const float ChevronThickness = 0.08f;

        /// <summary> 쉐브론 최대 알파(양끝으로 갈수록 옅어진다). </summary>
        private const float ChevronAlpha = 0.9f;

        /// <summary> 그릴 수 있는 쉐브론 최대 수(정점 고정용). 길이 / 간격 보다 많으면 앞쪽만 그린다. </summary>
        private const int MaxChevrons = 32;

        /// <summary> 화살촉 알파. </summary>
        private const float HeadAlpha = 0.95f;

        // 정점 배치(고정 — 배열 재사용).
        //   하위 메시 0(주색): 채움 10(열 5 × 줄 2) · 테두리 16(띠 4 × 4) · 눈금 MaxTicks × 4
        //   하위 메시 1(강조색): 화살촉 3 · 쉐브론 MaxChevrons × 8(팔 2 × 4)
        private const int FillVertexStart = 0;
        private const int FillColumns = 5;
        private const int BorderVertexStart = FillVertexStart + FillColumns * 2;   // 10
        private const int TickVertexStart = BorderVertexStart + 16;                 // 26
        private const int HeadVertexStart = TickVertexStart + MaxTicks * 4;
        private const int ChevronVertexStart = HeadVertexStart + 3;
        private const int VertexCount = ChevronVertexStart + MaxChevrons * 8;

        // ====================================================================
        // 런타임 구성물
        // ====================================================================

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private Material _material;        // 주색(채움 · 테두리 · 눈금)
        private Material _accentMaterial;  // 강조색(화살촉 · 쉐브론)
        private readonly Vector3[] _vertices = new Vector3[VertexCount];
        private readonly Color32[] _colors = new Color32[VertexCount];

        // 마지막으로 정적 부분(채움 · 테두리 · 눈금 · 화살촉)을 만든 값 — 같으면 다시 만들지 않는다.
        private float _builtLength = -1f;    // 그린 길이(맵 안 유효 길이)
        private float _builtWidth = -1f;     // 폭
        private float _builtFullLength = -1f; // 원래 길이 L(눈금 위치 계산용)
        private int _builtStrikeCount = -1;  // 폭격 횟수

        // 쉐브론이 흐를 구간(로컬 진행 거리) — 정적 부분을 만들 때 정한다.
        private float _chevronFrom;
        private float _chevronTo;
        private float _chevronHalfWidth;
        private float _chevronDepth;
        private float _chevronPhase;         // 흐름 위치(0 ~ ChevronSpacing)

        // 상태 색 · 등장 애니메이션.
        private SkillAimVisualState _visualState = SkillAimVisualState.Armed;
        private Color _currentMain;
        private Color _currentAccent;
        private float _appearElapsed = SkillAimVisualStyle.AppearDuration;

        /// <summary> 지금 보이는 중인지. </summary>
        public bool IsShown => gameObject.activeSelf;

        /// <summary> 지금 그리고 있는 폭격 눈금 수(진단 · 자가 검증용). </summary>
        public int VisibleTickCount { get; private set; }

        /// <summary>
        /// 씬에 빈 GameObject 를 만들고 이 컴포넌트를 붙여 돌려준다(씬 수정 없이 런타임 생성 — TowerRangeIndicator 선례).
        /// 씬이 바뀌면 함께 사라지며, 호출자는 다음에 필요할 때 다시 만든다(Unity null 검사).
        /// </summary>
        /// <returns>숨겨진 상태의 새 표시물.</returns>
        public static SkillLineAimReticle CreateRuntime()
        {
            var go = new GameObject("SkillLineAimReticle (runtime)");
            var reticle = go.AddComponent<SkillLineAimReticle>();
            reticle.Build();
            // 처음에는 숨김 — 공습 조준에 들어오면 Show 가 켠다(2026-11-18 L-7: 첫 터치 전 고스트부터 — UI-1. 직전 「첫 터치 때(M-1)」).
            go.SetActive(false);
            return reticle;
        }

        /// <summary>
        /// 직사각형 + 화살표를 표시한다(폭격 눈금 없음 — 2026-11-17 L-4 의 옛 호출 모양). 눈금까지 그리려면 아래 오버로드를 쓴다.
        /// </summary>
        /// <param name="viewStart">시작점의 화면 좌표(Y 는 무시하고 GroundY 에 깐다).</param>
        /// <param name="viewDirection">화면 방향(XZ, 길이 무관). 0 이면 화면 위쪽(+Z).</param>
        /// <param name="length">그릴 길이(월드 m) — 맵 안 유효 길이(U-14).</param>
        /// <param name="width">직선 폭(월드 m) — 스킬 데이터 LineWidth.</param>
        public void Show(Vector3 viewStart, Vector3 viewDirection, float length, float width)
        {
            Show(viewStart, viewDirection, length, width, length, 0);
        }

        /// <summary>
        /// 직사각형 + 폭격 눈금 + 쉐브론 + 화살촉을 표시한다(2026-11-18 L-7). 위치 · 방향은 화면(뷰) 좌표다(빨강 화면은 호출자가 변환 — P-10).
        /// 숨겨져 있다가 나타나면 등장 애니메이션(작게 → 크게)을 튼다.
        /// </summary>
        /// <param name="viewStart">시작점의 화면 좌표(Y 는 무시하고 GroundY 에 깐다).</param>
        /// <param name="viewDirection">화면 방향(XZ, 길이 무관). 0 이면 화면 위쪽(+Z).</param>
        /// <param name="length">그릴 길이(월드 m) — 맵 안 유효 길이(U-14).</param>
        /// <param name="width">직선 폭(월드 m) — 스킬 데이터 LineWidth.</param>
        /// <param name="fullLength">원래 길이 L(스킬 데이터 LineLength) — 폭격 구간은 이 길이로 나눈다(P-13).</param>
        /// <param name="strikeCount">폭격 횟수(스킬 데이터 LineStrikeCount). 0 이하면 눈금을 그리지 않는다.</param>
        public void Show(Vector3 viewStart, Vector3 viewDirection, float length, float width, float fullLength, int strikeCount)
        {
            if (_mesh == null) Build(); // 혹시 CreateRuntime 을 거치지 않고 붙은 경우 대비

            float l = float.IsNaN(length) || float.IsInfinity(length) ? 0f : Mathf.Max(0f, length);
            float w = float.IsNaN(width) || float.IsInfinity(width) ? 0f : Mathf.Max(0f, width);
            float full = float.IsNaN(fullLength) || float.IsInfinity(fullLength) ? l : Mathf.Max(0f, fullLength);

            transform.position = new Vector3(viewStart.x, GroundY, viewStart.z);
            Vector3 flat = new Vector3(viewDirection.x, 0f, viewDirection.z);
            // 메시는 로컬 +Z 를 "앞(끝점 쪽)"으로 만들어 두었다 → 루트를 화면 방향으로 Y축 회전하면 된다.
            transform.rotation = flat.sqrMagnitude > 1e-10f
                ? Quaternion.LookRotation(flat.normalized, Vector3.up)
                : Quaternion.identity;

            // 길이 · 폭 · 눈금 조건이 바뀐 경우에만 정적 정점을 다시 계산한다(방향만 바뀌면 회전만 바꾼다).
            if (Mathf.Abs(l - _builtLength) > 0.0005f || Mathf.Abs(w - _builtWidth) > 0.0005f
                || Mathf.Abs(full - _builtFullLength) > 0.0005f || strikeCount != _builtStrikeCount)
            {
                RebuildStatic(l, w, full, strikeCount);
                _builtLength = l;
                _builtWidth = w;
                _builtFullLength = full;
                _builtStrikeCount = strikeCount;
            }

            // 그릴 길이가 거의 0 이면(맵 경계 위에서 바깥을 향함) 렌더러만 끈다 — 오브젝트는 켠 채로 두어 다음 Show 가 곧바로 그린다.
            if (_renderer != null) _renderer.enabled = _material != null && _accentMaterial != null && l >= MinDrawLength && w > 0f;

            if (!gameObject.activeSelf)
            {
                PlayAppear();
                gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 조준 표시 상태(색)를 바꾼다(2026-11-18 L-7 — UI-1 · UI-2d). 실제 색은 매 프레임 목표로 부드럽게 따라간다.
        /// </summary>
        /// <param name="state">고스트 · 발사 가능 · 최소 드래그 미만 · X 위.</param>
        public void SetState(SkillAimVisualState state)
        {
            _visualState = state;
        }

        /// <summary>
        /// 등장 애니메이션(작게 → 크게)을 처음부터 다시 튼다. 숨김 → 보임 때 자동으로 불리고,
        /// 공습 고스트 → 활성(첫 터치 — UI-1)처럼 이미 보이는 중에 "새로 놓였다"를 보여 줄 때 조준 컨트롤러가 부른다.
        /// 색도 목표 색에서 바로 시작한다(지난 색이 한 박자 남지 않게).
        /// </summary>
        public void PlayAppear()
        {
            _appearElapsed = 0f;
            transform.localScale = Vector3.one * SkillAimVisualStyle.AppearScale(0f);
            _currentMain = SkillAimVisualStyle.MainColor(_visualState);
            _currentAccent = SkillAimVisualStyle.AccentColor(_visualState);
            SkillAimVisualStyle.SetTint(_material, _currentMain);
            SkillAimVisualStyle.SetTint(_accentMaterial, _currentAccent);
        }

        /// <summary> 표시를 즉시 숨긴다(조준 종료 · 패널 닫힘 · 건물 파괴 · 경기 종료). 이미 숨겨져 있으면 아무 일도 없다. </summary>
        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>
        /// [2026-11-18 L-7] 매 프레임 — 등장 크기 · 상태 색 · 쉐브론 흐름. 할당 없음. 조준 연출이라 실제 시간(unscaledDeltaTime)으로 움직인다.
        /// </summary>
        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_appearElapsed < SkillAimVisualStyle.AppearDuration)
            {
                _appearElapsed += dt;
                transform.localScale = Vector3.one * SkillAimVisualStyle.AppearScale(_appearElapsed);
            }

            Color targetMain = SkillAimVisualStyle.MainColor(_visualState);
            if (_currentMain != targetMain)
            {
                _currentMain = SkillAimVisualStyle.TickColor(_currentMain, targetMain, dt);
                SkillAimVisualStyle.SetTint(_material, _currentMain);
            }
            Color targetAccent = SkillAimVisualStyle.AccentColor(_visualState);
            if (_currentAccent != targetAccent)
            {
                _currentAccent = SkillAimVisualStyle.TickColor(_currentAccent, targetAccent, dt);
                SkillAimVisualStyle.SetTint(_accentMaterial, _currentAccent);
            }

            // 쉐브론 흐름 — 위치(phase)를 앞으로 밀고 정점을 다시 넣는다(렌더러가 꺼져 있으면 건너뛴다).
            if (_renderer != null && _renderer.enabled && _mesh != null)
            {
                _chevronPhase += ChevronSpeed * dt;
                if (_chevronPhase >= ChevronSpacing) _chevronPhase -= ChevronSpacing * Mathf.Floor(_chevronPhase / ChevronSpacing);
                WriteChevrons();
                _mesh.vertices = _vertices;   // 같은 배열 재사용(할당 없음)
                _mesh.colors32 = _colors;
            }
        }

        private void OnDestroy()
        {
            // 코드로 만든 메시 · 머티리얼은 씬이 사라져도 자동으로 풀리지 않는다 → 직접 정리(메모리 누수 방지).
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
            if (_accentMaterial != null) Destroy(_accentMaterial);
        }

        // ====================================================================
        // 구성
        // ====================================================================

        /// <summary>
        /// 렌더러 · 메시 · 머티리얼 2개를 만든다(1회). 정점 수 · 삼각형 순서 · 하위 메시는 여기서 한 번만 정한다.
        /// (2026-11-18 L-7 — 정점 27개 · 단색 정점 색이던 옛 Build 를 대체했다.)
        /// </summary>
        private void Build()
        {
            if (_mesh != null) return;
            _material = SkillAimVisualStyle.CreateOverlayMaterial(null, nameof(SkillLineAimReticle),
                                                                  "SkillLineAimReticleMaterial (runtime)");
            _accentMaterial = SkillAimVisualStyle.CreateOverlayMaterial(null, nameof(SkillLineAimReticle),
                                                                        "SkillLineAimReticleAccentMaterial (runtime)");

            _filter = gameObject.AddComponent<MeshFilter>();
            _renderer = gameObject.AddComponent<MeshRenderer>();
            // 하위 메시 0 → 주색 머티리얼, 1 → 강조색 머티리얼. 한 렌더러 안에서는 하위 메시 순서대로 그려 화살표가 채움 위에 보인다.
            if (_material != null && _accentMaterial != null)
                _renderer.sharedMaterials = new[] { _material, _accentMaterial };
            _renderer.enabled = _material != null && _accentMaterial != null; // 머티리얼이 없으면 분홍색으로 보이지 않게 꺼 둔다
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            _mesh = new Mesh { name = "SkillLineAimReticle (runtime)" };
            _mesh.MarkDynamic(); // 조준 중 길이 · 쉐브론이 자주 바뀐다 — 정점 갱신이 잦은 메시라고 알려 준다

            // 정점 색(흰색 + 알파 = 모양). 쉐브론 알파는 매 프레임 WriteChevrons 가 고친다.
            float[] fillAlphas = { FillEdgeAlpha, FillMidAlpha, FillCenterAlpha, FillMidAlpha, FillEdgeAlpha };
            for (int c = 0; c < FillColumns; c++)
            {
                byte a = ToByte(fillAlphas[c]);
                _colors[FillVertexStart + c * 2] = new Color32(255, 255, 255, a);
                _colors[FillVertexStart + c * 2 + 1] = new Color32(255, 255, 255, a);
            }
            for (int i = BorderVertexStart; i < TickVertexStart; i++) _colors[i] = new Color32(255, 255, 255, ToByte(BorderAlpha));
            for (int i = TickVertexStart; i < HeadVertexStart; i++) _colors[i] = new Color32(255, 255, 255, ToByte(TickAlpha));
            for (int i = HeadVertexStart; i < ChevronVertexStart; i++) _colors[i] = new Color32(255, 255, 255, ToByte(HeadAlpha));
            for (int i = ChevronVertexStart; i < VertexCount; i++) _colors[i] = new Color32(255, 255, 255, 0);

            // 삼각형 — 반투명(ZWrite Off) 셰이더는 "삼각형 순서대로" 그린다 → 채움 먼저, 테두리 · 눈금은 그 위.
            var main = new int[((FillColumns - 1) + 4 + MaxTicks) * 6];
            int t = 0;
            for (int c = 0; c + 1 < FillColumns; c++)
                t = AddQuadIndices(main, t, FillVertexStart + c * 2, FillVertexStart + (c + 1) * 2,
                                   FillVertexStart + c * 2 + 1, FillVertexStart + (c + 1) * 2 + 1);
            for (int q = 0; q < 4; q++) t = AddQuad(main, t, BorderVertexStart + q * 4);
            for (int k = 0; k < MaxTicks; k++) t = AddQuad(main, t, TickVertexStart + k * 4);

            var accent = new int[3 + MaxChevrons * 2 * 6];
            int u = 0;
            accent[u++] = HeadVertexStart; accent[u++] = HeadVertexStart + 2; accent[u++] = HeadVertexStart + 1; // 화살촉(Cull Off — 감는 방향 무관)
            for (int k = 0; k < MaxChevrons; k++)
            {
                u = AddQuad(accent, u, ChevronVertexStart + k * 8);
                u = AddQuad(accent, u, ChevronVertexStart + k * 8 + 4);
            }

            RebuildStatic(0f, 0f, 0f, 0);
            _mesh.vertices = _vertices;
            _mesh.colors32 = _colors;
            _mesh.subMeshCount = 2;
            _mesh.SetTriangles(main, 0);
            _mesh.SetTriangles(accent, 1);
            _mesh.RecalculateBounds();
            _filter.sharedMesh = _mesh;
        }

        /// <summary>
        /// 사각형 하나(정점 4개: 0 = 왼쪽 아래, 1 = 오른쪽 아래, 2 = 왼쪽 위, 3 = 오른쪽 위)의 삼각형 2개를 넣는다.
        /// </summary>
        private static int AddQuad(int[] triangles, int t, int v)
        {
            triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            return t + 6;
        }

        /// <summary>
        /// 정점 번호가 연속이 아닌 사각형(왼쪽 아래 lb · 오른쪽 아래 rb · 왼쪽 위 lt · 오른쪽 위 rt)의 삼각형 2개를 넣는다(채움 열 사이용).
        /// </summary>
        private static int AddQuadIndices(int[] triangles, int t, int lb, int rb, int lt, int rt)
        {
            triangles[t] = lb; triangles[t + 1] = lt; triangles[t + 2] = rb;
            triangles[t + 3] = rb; triangles[t + 4] = lt; triangles[t + 5] = rt;
            return t + 6;
        }

        /// <summary>
        /// 길이 · 폭 · 눈금 조건이 바뀌었을 때 "움직이지 않는 부분"(채움 · 테두리 · 눈금 · 화살촉) 정점과 쉐브론이 흐를 구간을 다시 계산한다.
        /// 로컬 XZ — +Z = 앞(끝점 쪽), X = 옆. 정점 수 · 삼각형 순서는 바뀌지 않는다(쓰지 않는 눈금 · 쉐브론은 넓이 0 으로 접는다).
        /// </summary>
        /// <param name="length">그릴 길이(맵 안 유효 길이, 월드 m).</param>
        /// <param name="width">폭(월드 m).</param>
        /// <param name="fullLength">원래 길이 L(눈금 위치 계산용).</param>
        /// <param name="strikeCount">폭격 횟수(0 이하면 눈금 없음).</param>
        private void RebuildStatic(float length, float width, float fullLength, int strikeCount)
        {
            float h = width * 0.5f;
            // 테두리 두께는 직사각형의 반폭 · 반길이를 넘지 않게 줄인다(아주 짧은 직선에서 띠가 겹쳐 뒤집히지 않게).
            float b = Mathf.Min(BorderThickness, h, length * 0.5f);
            float y0 = 0f, y1 = LayerStep, y2 = LayerStep * 2f;

            // 채움 — 테두리 안쪽 전체를 5열로 나눠 가운데 옅고 양옆 진한 그라데이션(정점 알파는 Build 에서 정했다).
            float inner = h - b;
            float[] xs = { -inner, -inner * 0.5f, 0f, inner * 0.5f, inner };
            for (int c = 0; c < FillColumns; c++)
            {
                _vertices[FillVertexStart + c * 2] = new Vector3(xs[c], y0, b);
                _vertices[FillVertexStart + c * 2 + 1] = new Vector3(xs[c], y0, Mathf.Max(b, length - b));
            }

            // 테두리 띠 4개(안쪽으로 두께 b).
            SetQuad(BorderVertexStart, -h, -h + b, 0f, length, y1);          // 왼쪽
            SetQuad(BorderVertexStart + 4, h - b, h, 0f, length, y1);        // 오른쪽
            SetQuad(BorderVertexStart + 8, -h, h, 0f, b, y1);                // 앞(시작점)
            SetQuad(BorderVertexStart + 12, -h, h, length - b, length, y1);  // 끝

            // 폭격 눈금 — 각 폭격이 떨어지는 진행 거리(화면 폭격 VFX 와 같은 식). 맵 밖이라 건너뛰는 폭격 · 남는 칸은 접는다.
            int visibleTicks = 0;
            float halfTick = TickThickness * 0.5f;
            for (int k = 0; k < MaxTicks; k++)
            {
                int v = TickVertexStart + k * 4;
                if (k < strikeCount && SkillAimVisualMath.TryGetStrikeTickAlong(k, strikeCount, fullLength, length, out float along))
                {
                    float z0 = Mathf.Clamp(along - halfTick, 0f, length);
                    float z1 = Mathf.Clamp(along + halfTick, 0f, length);
                    SetQuad(v, -inner, inner, z0, z1, y1);
                    visibleTicks++;
                }
                else
                {
                    CollapseQuad(v);
                }
            }
            VisibleTickCount = visibleTicks;

            // 화살촉 — 끝 쪽 삼각형. 짧은 직선에서는 비율로 줄여 직사각형 안에 머물게 한다(옛 계산과 같다).
            float headLen = Mathf.Min(width * HeadLengthWidthRatio, length * HeadMaxLengthRatio);
            float tip = Mathf.Max(0f, length - b * 1.5f);
            float headBase = Mathf.Max(0f, tip - headLen);
            float hh = Mathf.Min(width * HeadHalfWidthRatio, h - b);
            if (hh < 0f) hh = 0f;
            _vertices[HeadVertexStart] = new Vector3(-hh, y2, headBase);
            _vertices[HeadVertexStart + 1] = new Vector3(hh, y2, headBase);
            _vertices[HeadVertexStart + 2] = new Vector3(0f, y2, tip);

            // 쉐브론이 흐를 구간 — 시작 테두리 안쪽 ~ 화살촉 바로 앞.
            _chevronFrom = b + 0.02f;
            _chevronTo = Mathf.Max(_chevronFrom, headBase - 0.04f);
            _chevronHalfWidth = Mathf.Max(0f, inner * ChevronHalfWidthRatio);
            _chevronDepth = width * ChevronDepthRatio;
            WriteChevrons();

            if (_mesh != null)
            {
                _mesh.vertices = _vertices; // 같은 배열을 다시 넣는다(새 배열 할당 없음)
                _mesh.colors32 = _colors;
                _mesh.RecalculateBounds();
            }
        }

        /// <summary>
        /// 쉐브론(>) 정점과 알파를 지금 흐름 위치(_chevronPhase)로 다시 쓴다. 구간 밖 · 남는 칸은 넓이 0 으로 접는다.
        /// 구간 양끝에서는 서서히 나타나고 사라지도록 알파를 줄인다. 배열만 고친다(메시에 넣는 것은 호출자).
        /// </summary>
        private void WriteChevrons()
        {
            float y = LayerStep * 2f;
            float span = ChevronThickness + _chevronDepth; // 쉐브론 하나가 차지하는 진행 길이
            for (int k = 0; k < MaxChevrons; k++)
            {
                int v = ChevronVertexStart + k * 8;
                float z = _chevronFrom + _chevronPhase + (k - 1) * ChevronSpacing; // k = 0 은 구간 앞에서 막 들어오는 중
                if (_chevronHalfWidth <= 0f || z < _chevronFrom || z + span > _chevronTo)
                {
                    CollapseQuad(v);
                    CollapseQuad(v + 4);
                    for (int i = 0; i < 8; i++) _colors[v + i].a = 0;
                    continue;
                }

                // 양끝 페이드 — 구간 시작 · 끝에서 쉐브론 한 간격 동안 서서히.
                float fadeIn = (z - _chevronFrom) / ChevronSpacing;
                float fadeOut = (_chevronTo - (z + span)) / ChevronSpacing;
                byte a = ToByte(ChevronAlpha * Mathf.Clamp01(Mathf.Min(fadeIn, fadeOut)));

                float cw = _chevronHalfWidth;
                float apex = z + _chevronDepth;
                // 왼쪽 팔: (-cw, z) → (0, apex), 두께 ChevronThickness 만큼 앞으로. 순서 = 왼아래 · 오른아래 · 왼위 · 오른위(AddQuad 약속).
                _vertices[v] = new Vector3(-cw, y, z);
                _vertices[v + 1] = new Vector3(0f, y, apex);
                _vertices[v + 2] = new Vector3(-cw, y, z + ChevronThickness);
                _vertices[v + 3] = new Vector3(0f, y, apex + ChevronThickness);
                // 오른쪽 팔: (0, apex) → (cw, z).
                _vertices[v + 4] = new Vector3(0f, y, apex);
                _vertices[v + 5] = new Vector3(cw, y, z);
                _vertices[v + 6] = new Vector3(0f, y, apex + ChevronThickness);
                _vertices[v + 7] = new Vector3(cw, y, z + ChevronThickness);
                for (int i = 0; i < 8; i++) _colors[v + i].a = a;
            }
        }

        /// <summary> 사각형 정점 4개(x0~x1 × z0~z1, 높이 y)를 v 번부터 채운다. </summary>
        private void SetQuad(int v, float x0, float x1, float z0, float z1, float y)
        {
            _vertices[v] = new Vector3(x0, y, z0);
            _vertices[v + 1] = new Vector3(x1, y, z0);
            _vertices[v + 2] = new Vector3(x0, y, z1);
            _vertices[v + 3] = new Vector3(x1, y, z1);
        }

        /// <summary> 사각형 정점 4개를 한 점(원점)에 모아 넓이 0 으로 만든다 — 화면에 그려지지 않는다(정점 수는 그대로). </summary>
        private void CollapseQuad(int v)
        {
            _vertices[v] = Vector3.zero;
            _vertices[v + 1] = Vector3.zero;
            _vertices[v + 2] = Vector3.zero;
            _vertices[v + 3] = Vector3.zero;
        }

        /// <summary> 0~1 알파를 0~255 바이트로. </summary>
        private static byte ToByte(float a)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);
        }
    }
}
