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
// Presentation 레이어 — Unity MonoBehaviour 의존. 화면 전용(판정에 아무 영향 없음).
// ============================================================================

using UnityEngine;
using Hexiege.Application;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 공습 직선 조준 표시(채움 + 테두리 + 방향 화살표). 위치 · 방향은 화면(뷰) 좌표로 받는다.
    /// </summary>
    public sealed class SkillLineAimReticle : MonoBehaviour
    {
        // ====================================================================
        // 모양 · 색 상수(화면 전용 — 판정과 무관. 아트 단계에서 바꿀 수 있는 임시값)
        // ====================================================================

        /// <summary> 지면 위 높이(월드 Y). 타일 윗면 0.05 보다 0.01 위(범위 표시 · 스킬 VFX 선례). </summary>
        private const float GroundY = 0.06f;

        /// <summary> 테두리 띠 두께(월드 m). 직사각형 안쪽으로 그린다 — 바깥 끝 = 정확히 판정 범위. </summary>
        private const float BorderThickness = 0.05f;

        /// <summary> 겹친 층(채움 → 테두리 → 화살표)이 깜빡이지 않도록 층마다 아주 조금씩 높이를 다르게 둔다(월드 m). </summary>
        private const float LayerStep = 0.0005f;

        /// <summary> 이보다 짧은 길이는 "그릴 것이 없다"로 보고 렌더러를 끈다(맵 경계 위에서 바깥쪽을 향한 경우 — 유효 길이 0). </summary>
        private const float MinDrawLength = 0.01f;

        /// <summary> 화살표 막대 폭 = 직선 폭 × 이 비율. </summary>
        private const float ShaftWidthRatio = 0.08f;

        /// <summary> 화살촉 반폭 = 직선 폭 × 이 비율. </summary>
        private const float HeadHalfWidthRatio = 0.28f;

        /// <summary> 화살촉 길이 = min(직선 폭 × 이 비율, 길이 × HeadMaxLengthRatio). </summary>
        private const float HeadLengthWidthRatio = 0.55f;

        /// <summary> 화살촉 길이의 상한(직선 길이 대비). 짧은 직선에서 화살촉이 직사각형 밖으로 나가지 않게. </summary>
        private const float HeadMaxLengthRatio = 0.4f;

        /// <summary> 화살표 막대가 시작하는 자리(길이 대비 — 시작점에서 조금 떨어져 시작). </summary>
        private const float ShaftStartRatio = 0.06f;

        /// <summary> 채움 색(반투명 붉은 — 원형 조준원 채움색과 같은 계열). </summary>
        private static readonly Color32 FillColor = new Color32(204, 51, 51, 64);

        /// <summary> 테두리 색(진한 붉은 — 원형 조준원 링 색과 같은 계열). </summary>
        private static readonly Color32 EdgeColor = new Color32(217, 38, 38, 230);

        /// <summary> 화살표 색(밝은 살구빛 — 붉은 채움 위에서 방향이 잘 보이도록). </summary>
        private static readonly Color32 ArrowColor = new Color32(255, 225, 205, 235);

        /// <summary> 정점 색이 그대로 나오게 머티리얼 틴트는 흰색으로 둔다. </summary>
        private static readonly Color MaterialTint = Color.white;

        /// <summary> Resources 의 지면 데칼 머티리얼 경로(TowerRangeIndicator 와 같은 에셋 — 복사해서 쓴다). </summary>
        private const string OverlayMaterialResourcePath = "Materials/NoBuildHatch";

        // 정점 · 삼각형 수(고정 — 배열 재사용).
        //   채움 4 + 테두리 띠 4개 × 4 + 화살표 막대 4 + 화살촉 3 = 27 정점,
        //   채움 2 + 테두리 8 + 막대 2 + 화살촉 1 = 13 삼각형.
        private const int VertexCount = 27;
        private const int TriangleIndexCount = 13 * 3;

        // ====================================================================
        // 런타임 구성물
        // ====================================================================

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private Material _material;
        private readonly Vector3[] _vertices = new Vector3[VertexCount];
        private float _builtLength = -1f;   // 마지막으로 메시를 만든 길이(같으면 다시 만들지 않는다)
        private float _builtWidth = -1f;    // 마지막으로 메시를 만든 폭

        /// <summary> 지금 보이는 중인지. </summary>
        public bool IsShown => gameObject.activeSelf;

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
            go.SetActive(false); // 처음에는 숨김 — 공습 조준의 첫 터치 때 Show 가 켠다(M-1)
            return reticle;
        }

        /// <summary>
        /// 직사각형 + 화살표를 표시한다. 위치 · 방향은 화면(뷰) 좌표다(빨강 화면은 호출자가 변환해 넘긴다 — P-10).
        /// </summary>
        /// <param name="viewStart">시작점의 화면 좌표(Y 는 무시하고 GroundY 에 깐다).</param>
        /// <param name="viewDirection">화면 방향(XZ, 길이 무관). 0 이면 화면 위쪽(+Z).</param>
        /// <param name="length">그릴 길이(월드 m) — 맵 안 유효 길이(U-14).</param>
        /// <param name="width">직선 폭(월드 m) — 스킬 데이터 LineWidth.</param>
        public void Show(Vector3 viewStart, Vector3 viewDirection, float length, float width)
        {
            if (_mesh == null) Build(); // 혹시 CreateRuntime 을 거치지 않고 붙은 경우 대비

            float l = float.IsNaN(length) || float.IsInfinity(length) ? 0f : Mathf.Max(0f, length);
            float w = float.IsNaN(width) || float.IsInfinity(width) ? 0f : Mathf.Max(0f, width);

            transform.position = new Vector3(viewStart.x, GroundY, viewStart.z);
            Vector3 flat = new Vector3(viewDirection.x, 0f, viewDirection.z);
            // 메시는 로컬 +Z 를 "앞(끝점 쪽)"으로 만들어 두었다 → 루트를 화면 방향으로 Y축 회전하면 된다.
            transform.rotation = flat.sqrMagnitude > 1e-10f
                ? Quaternion.LookRotation(flat.normalized, Vector3.up)
                : Quaternion.identity;

            // 길이 · 폭이 바뀐 경우에만 정점을 다시 계산한다(방향만 바뀌면 회전만 바꾼다).
            if (Mathf.Abs(l - _builtLength) > 0.0005f || Mathf.Abs(w - _builtWidth) > 0.0005f)
            {
                RebuildVertices(l, w);
                _builtLength = l;
                _builtWidth = w;
            }

            // 그릴 길이가 거의 0 이면(맵 경계 위에서 바깥을 향함) 렌더러만 끈다 — 오브젝트는 켠 채로 두어 다음 Show 가 곧바로 그린다.
            if (_renderer != null) _renderer.enabled = _material != null && l >= MinDrawLength && w > 0f;

            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        /// <summary> 표시를 즉시 숨긴다(조준 종료 · 패널 닫힘 · 건물 파괴 · 경기 종료). 이미 숨겨져 있으면 아무 일도 없다. </summary>
        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            // 코드로 만든 메시 · 머티리얼은 씬이 사라져도 자동으로 풀리지 않는다 → 직접 정리(메모리 누수 방지).
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }

        // ====================================================================
        // 구성
        // ====================================================================

        /// <summary> 렌더러 · 메시 · 머티리얼을 만든다(1회). 정점 색과 삼각형 순서는 여기서 한 번만 정한다. </summary>
        private void Build()
        {
            if (_mesh != null) return;
            _material = CreateOverlayMaterial();

            _filter = gameObject.AddComponent<MeshFilter>();
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.enabled = _material != null; // 머티리얼이 없으면 분홍색으로 보이지 않게 꺼 둔다
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            _mesh = new Mesh { name = "SkillLineAimReticle (runtime)" };
            _mesh.MarkDynamic(); // 조준 중 길이가 자주 바뀐다 — 정점 갱신이 잦은 메시라고 알려 준다

            // 정점 색 — 순서: 채움(0~3) · 테두리(4~19) · 막대(20~23) · 화살촉(24~26).
            var colors = new Color32[VertexCount];
            for (int i = 0; i < 4; i++) colors[i] = FillColor;
            for (int i = 4; i < 20; i++) colors[i] = EdgeColor;
            for (int i = 20; i < VertexCount; i++) colors[i] = ArrowColor;

            // 삼각형 — 반투명(ZWrite Off) 셰이더는 한 메시 안에서 "삼각형 순서대로" 그린다.
            //   그래서 채움을 먼저, 테두리 · 화살표를 나중에 둬야 위에 덮여 보인다.
            var triangles = new int[TriangleIndexCount];
            int t = 0;
            t = AddQuad(triangles, t, 0);   // 채움
            t = AddQuad(triangles, t, 4);   // 테두리 왼쪽
            t = AddQuad(triangles, t, 8);   // 테두리 오른쪽
            t = AddQuad(triangles, t, 12);  // 테두리 앞(시작점 쪽)
            t = AddQuad(triangles, t, 16);  // 테두리 끝(끝점 쪽)
            t = AddQuad(triangles, t, 20);  // 화살표 막대
            triangles[t] = 24; triangles[t + 1] = 26; triangles[t + 2] = 25; // 화살촉(셰이더가 Cull Off 라 감는 방향과 무관하게 보인다)

            RebuildVertices(0f, 0f);
            _mesh.vertices = _vertices;
            _mesh.colors32 = colors;
            _mesh.triangles = triangles;
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
        /// 길이 · 폭에 맞춰 정점 위치를 다시 계산한다(로컬 XZ — +Z = 앞, X = 옆). 정점 수 · 삼각형 순서는 바뀌지 않는다.
        /// </summary>
        /// <param name="length">길이(월드 m).</param>
        /// <param name="width">폭(월드 m).</param>
        private void RebuildVertices(float length, float width)
        {
            float h = width * 0.5f;
            // 테두리 두께는 직사각형의 반폭 · 반길이를 넘지 않게 줄인다(아주 짧은 직선에서 띠가 겹쳐 뒤집히지 않게).
            float b = Mathf.Min(BorderThickness, h, length * 0.5f);
            float y0 = 0f, y1 = LayerStep, y2 = LayerStep * 2f;

            // 채움(직사각형 전체).
            SetQuad(0, -h, h, 0f, length, y0);
            // 테두리 띠 4개(안쪽으로 두께 b).
            SetQuad(4, -h, -h + b, 0f, length, y1);           // 왼쪽
            SetQuad(8, h - b, h, 0f, length, y1);             // 오른쪽
            SetQuad(12, -h, h, 0f, b, y1);                    // 앞(시작점)
            SetQuad(16, -h, h, length - b, length, y1);       // 끝

            // 화살표 — 가운데 막대 + 끝 쪽 삼각형 화살촉. 짧은 직선에서는 비율로 줄여 직사각형 안에 머물게 한다.
            float headLen = Mathf.Min(width * HeadLengthWidthRatio, length * HeadMaxLengthRatio);
            float tip = Mathf.Max(0f, length - b * 1.5f);
            float headBase = Mathf.Max(0f, tip - headLen);
            float shaftStart = Mathf.Min(length * ShaftStartRatio + b, headBase);
            float sh = width * ShaftWidthRatio * 0.5f;
            float hh = Mathf.Min(width * HeadHalfWidthRatio, h - b);
            if (hh < 0f) hh = 0f;
            SetQuad(20, -sh, sh, shaftStart, headBase, y2);
            _vertices[24] = new Vector3(-hh, y2, headBase);
            _vertices[25] = new Vector3(hh, y2, headBase);
            _vertices[26] = new Vector3(0f, y2, tip);

            if (_mesh != null)
            {
                _mesh.vertices = _vertices; // 같은 배열을 다시 넣는다(새 배열 할당 없음)
                _mesh.RecalculateBounds();
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

        /// <summary>
        /// 지면 데칼 머티리얼 복사본을 만든다(TowerRangeIndicator.CreateOverlayMaterial 과 같은 순서).
        /// 1순위 Resources 의 NoBuildHatch.mat 복사, 2순위 셰이더 직접 찾기. 둘 다 실패하면 null(표시만 안 될 뿐 게임은 정상 — 경고 1줄).
        /// </summary>
        private static Material CreateOverlayMaterial()
        {
            Material source = Resources.Load<Material>(OverlayMaterialResourcePath);
            Material material = null;
            if (source != null)
            {
                material = new Material(source);
            }
            else
            {
                Shader shader = Shader.Find("Hexiege/SkillAimOverlay");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null) material = new Material(shader);
                // [개발] Warn + 개발 — 에셋 구성 문제(모든 기기 공통)이고, 셰이더를 찾았으면 계속 보이므로 대체 경로가 있다.
                GameLog.Dev.Warn("Skill", nameof(SkillLineAimReticle),
                                 "[SKILL-AIM] 직선 조준 표시용 지면 데칼 머티리얼 에셋을 찾지 못했다 — 셰이더를 직접 찾아 대체한다",
                                 $"Diag=SkillAim, DiagRev=1, Event=LineReticleMaterialFallback, Path=Resources/{OverlayMaterialResourcePath}, ShaderFound={(material != null)}");
            }
            if (material == null) return null;
            material.name = "SkillLineAimReticleMaterial (runtime)";
            if (material.HasProperty("_Color")) material.SetColor("_Color", MaterialTint);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", MaterialTint);
            return material;
        }
    }
}
