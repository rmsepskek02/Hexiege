// ============================================================================
// TowerRangeIndicator.cs
// 내 방어탑을 눌러 패널이 열려 있는 동안, 땅바닥에 그 탑의 공격 범위를 그려 주는 표시물.
// 2026-11-09 Task `_Tasks/2026-11-09/22_19_cannon-tower-front-arc/` 커밋 3(추가 범위 (가) — 사용자 승인)에서 새로 만들었다.
//
// 무엇을 하나(유니티 초급자용 설명):
//   · 정령 · 초월 탑(360°) : 사거리 원(붉은 반투명 채움 + 진한 붉은 테두리)만 그린다.
//   · 인간 대포탑(120°)    : "정면 좌우 60°(합 120°)" 부채꼴(붉은 채움 + 양옆 경계선 + 호 테두리)만 그린다.
//                            부채꼴 밖의 원 부분(원판 · 원 테두리)은 그리지 않는다 — 쏘지 못하는 곳을 범위처럼 보이지 않게.
//                            인간 대포탑은 이 부채꼴 안의 적만 쏜다(서버 판정 — TowerFrontArc).
//   2026-11-10 사용자 피드백으로 바뀌었다: 「공격 범위 색상은 붉은색 계열, 포탑의 경우 120도만 나오도록,
//   그 외에 다른 회색으로 보이는 부분은 안 나오면 좋겠어」. 그 전(1e505f3)은 모든 탑에 흰빛(화면에서 회색으로 보임)
//   사거리 원을 깔고, 대포탑은 그 위에 주황 부채꼴을 겹쳐 그렸다.
//   패널이 닫히면(닫기 버튼 · 바깥 탭 · 철거 · 탑 파괴 · 경기 시작/종료) 즉시 사라진다.
//   화면 전용이다 — 공격 판정에는 아무 영향이 없다.
//
// 값은 판정과 같은 원본에서 읽는다(호출자 BuildingActionPanelUI가 넘겨준다):
//   반경 = TowerCombatUseCase.GetTowerRangeDisplay(판정 사거리 그대로), 부채꼴 각도 = TowerFrontArc.HalfAngleDeg.
//   이 컴포넌트는 숫자를 자체로 들고 있지 않다 — 설정 에셋만 바꿨을 때 "보이는 범위"와 "실제 범위"가 어긋나지 않게.
//
// 새 아트 에셋 없이 만든다:
//   모양은 코드로 만든 메시(원판 · 고리 · 부채꼴 · 경계선 띠)이고, 색은 메시의 정점 색이다.
//   머티리얼은 Resources 의 NoBuildHatch.mat(지면 데칼 셰이더 Hexiege/SkillAimOverlay 사용)을 "복사"해 쓴다.
//     · 그 셰이더는 ZTest LEqual + Offset −1,−1 + ZWrite Off 라 타일 윗면에 파묻히지 않고, 유닛 · 건물에는 가려진다
//       (MistShrine 범위 원 · 스킬 조준원과 같은 렌더링 규칙 — Buildings 규칙 27). ZTest Always 는 쓰지 않는다.
//     · Resources 아래 에셋이라 빌드에 반드시 포함되고, 그 에셋이 셰이더를 참조하므로 셰이더도 함께 들어간다
//       (HexGridRenderer.GetHatchMaterial 선례). 복사본을 쓰는 이유: 원본의 회색 틴트를 바꾸면 빗금 색이 같이 바뀐다.
//   씬 · 프리팹 수정이 필요 없다 — 처음 필요할 때 CreateRuntime()이 빈 GameObject를 만들어 붙인다.
//
// 높이: 타일 윗면은 Y 0.05 이고 불투명이라, 그보다 낮으면 가려진다. 스킬 VFX(0.06) · 빗금(0.06) 선례대로 0.06 위에 깐다.
//
// Presentation 레이어 — Unity MonoBehaviour 의존. 판정 값은 읽기만 한다.
// ============================================================================

using UnityEngine;
using Hexiege.Application;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 방어탑 공격 범위를 지면에 붉은색으로 그리는 화면 전용 표시물.
    /// 360° 탑은 사거리 원만, 인간 대포탑은 전방 120° 부채꼴만 그린다(둘을 겹쳐 그리지 않는다 — 2026-11-10).
    /// 런타임에 코드로 메시를 만들어 그린다(씬 · 프리팹 배선 불필요).
    /// </summary>
    public sealed class TowerRangeIndicator : MonoBehaviour
    {
        // ====================================================================
        // 모양 · 색 상수 (화면 전용 — 판정과 무관. 아트 단계에서 바꿀 수 있는 임시값)
        // ====================================================================

        /// <summary> 지면 위 높이(월드 Y). 타일 윗면 0.05 보다 0.01 위(스킬 VFX · 빗금 선례). </summary>
        private const float GroundY = 0.06f;

        /// <summary> 원을 근사하는 조각 수(360° 기준). 많을수록 매끈하다 — 96이면 화면에서 각이 보이지 않는다. </summary>
        private const int CircleSegments = 96;

        /// <summary> 사거리 원 테두리 두께(월드 m). 원 안쪽으로 그린다 — 바깥 끝 = 정확히 판정 사거리. </summary>
        private const float RingThickness = 0.045f;

        /// <summary> 부채꼴 경계선(좌우 60° 선) 두께(월드 m). </summary>
        private const float EdgeThickness = 0.035f;

        /// <summary> 겹친 반투명 층끼리 깜빡이지 않도록 층마다 아주 조금씩 높이를 다르게 둔다(월드 m). </summary>
        private const float LayerStep = 0.0005f;

        /// <summary> 정점 색이 그대로 나오게 머티리얼 틴트는 흰색으로 둔다. </summary>
        private static readonly Color MaterialTint = Color.white;

        /// <summary>
        /// 공격 범위 "채움" 색(붉은 계열, 2026-11-10). 360° 탑의 원판과 대포탑의 120° 부채꼴이 같은 색을 쓴다.
        /// 알파 64(약 25%)의 근거: 사용자가 「육안상 잘 작업됐다」고 본 1e505f3 의 부채꼴 채움 알파가 72(약 28%)였다.
        ///   붉은색은 주황보다 조금 진하게 보이므로 한 단계 낮춘 64로 둔다 — 아래 타일 색 · 유닛이 그대로 비쳐 보이는 수준.
        ///   (더 올리면 붉은 팀 타일과 구분이 흐려지고 유닛이 덮여 보인다. 아트 단계에서 바꿀 수 있는 임시값.)
        /// </summary>
        private static readonly Color32 RangeFillRed = new Color32(235, 45, 35, 64);

        /// <summary>
        /// 공격 범위 "테두리" 색(붉은 계열, 2026-11-10). 360° 탑의 원 테두리, 대포탑의 좌우 경계선 · 호 테두리에 쓴다.
        /// 채움보다 밝은 빨강 + 알파 220(약 86%)으로, 채움이 옅어도 "어디까지가 범위인지" 경계는 또렷하게 읽히게 한다.
        ///   (1e505f3 의 테두리 알파 200~230 범위 안의 값.)
        /// </summary>
        private static readonly Color32 RangeEdgeRed = new Color32(255, 70, 55, 220);

        /// <summary> Resources 의 지면 데칼 머티리얼 경로(HexGridRenderer 와 같은 에셋 — 복사해서 쓴다). </summary>
        private const string OverlayMaterialResourcePath = "Materials/NoBuildHatch";

        // ====================================================================
        // 런타임 구성물
        // ====================================================================

        private MeshFilter _fillFilter, _ringFilter, _arcFillFilter, _arcEdgeFilter;
        private Transform _arcRoot;              // 부채꼴 층의 부모 — 정면 방향으로 Y축 회전한다
        private Material _material;              // 이 표시물 전용 머티리얼 복사본
        private float _builtRadius = -1f;        // 마지막으로 메시를 만든 반경(같으면 다시 만들지 않는다)
        private float _builtHalfAngle = -1f;     // 마지막으로 부채꼴을 만든 반각

        /// <summary> 지금 보이는 중인지. </summary>
        public bool IsShown => gameObject.activeSelf;

        /// <summary>
        /// 씬에 빈 GameObject 를 만들고 이 컴포넌트를 붙여 돌려준다(씬 수정 없이 런타임 생성).
        /// 씬이 바뀌면 함께 사라지며, 호출자는 다음에 필요할 때 다시 만든다(Unity null 검사).
        /// </summary>
        /// <returns>숨겨진 상태의 새 표시물.</returns>
        public static TowerRangeIndicator CreateRuntime()
        {
            var go = new GameObject("TowerRangeIndicator (runtime)");
            var indicator = go.AddComponent<TowerRangeIndicator>();
            indicator.BuildLayers();
            go.SetActive(false); // 처음에는 숨김 — 패널이 열릴 때 Show 가 켠다
            return indicator;
        }

        /// <summary>
        /// 범위를 표시한다. 위치 · 방향은 모두 "화면(뷰) 좌표"로 받는다(Red 화면은 맵이 뒤집혀 보이므로 호출자가 변환해 넘긴다).
        /// </summary>
        /// <param name="viewCenter">탑 중심의 화면 좌표(Y는 무시하고 GroundY 에 깐다).</param>
        /// <param name="radius">원 반경(월드 m) — 판정 사거리 그대로.</param>
        /// <param name="showFrontArc">전방 부채꼴을 함께 그릴지(인간 대포탑).</param>
        /// <param name="halfAngleDeg">부채꼴 반각(도) — TowerFrontArc.HalfAngleDeg.</param>
        /// <param name="viewForward">탑 정면의 화면 방향(XZ, 길이 무관). showFrontArc 가 false 면 쓰지 않는다.</param>
        public void Show(Vector3 viewCenter, float radius, bool showFrontArc, float halfAngleDeg, Vector3 viewForward)
        {
            if (_fillFilter == null) BuildLayers(); // 혹시 CreateRuntime 을 거치지 않고 붙은 경우 대비
            float r = Mathf.Max(0f, radius);

            transform.position = new Vector3(viewCenter.x, GroundY, viewCenter.z);
            transform.rotation = Quaternion.identity;

            if (!Mathf.Approximately(r, _builtRadius))
            {
                SetMesh(_fillFilter, BuildDisc(r, RangeFillRed));
                SetMesh(_ringFilter, BuildAnnulus(r, Mathf.Max(0f, r - RingThickness), 0f, 360f, RangeEdgeRed));
                _builtRadius = r;
                _builtHalfAngle = -1f; // 부채꼴도 반경이 바뀌었으니 다시 만든다
            }

            // 모양 고르기(2026-11-10): 원 층(원판 · 원 테두리)과 부채꼴 층 중 "한쪽만" 켠다.
            //   · 대포탑(showFrontArc = true)  → 부채꼴만. 원 층을 끄므로 부채꼴 밖 원 부분은 화면에 전혀 그려지지 않는다.
            //   · 360° 탑(showFrontArc = false) → 원만.
            //   층을 지우지 않고 GameObject 를 껐다 켜는 이유: 같은 표시물을 다른 종류의 탑에 다시 쓸 때(패널이 다른 탑으로
            //   바로 열림) 메시를 새로 만들 필요 없이 켜고 끄기만 하면 된다.
            _fillFilter.gameObject.SetActive(!showFrontArc);
            _ringFilter.gameObject.SetActive(!showFrontArc);

            _arcRoot.gameObject.SetActive(showFrontArc);
            if (showFrontArc)
            {
                if (!Mathf.Approximately(halfAngleDeg, _builtHalfAngle))
                {
                    SetMesh(_arcFillFilter, BuildSector(r, halfAngleDeg, RangeFillRed));
                    SetMesh(_arcEdgeFilter, BuildArcEdges(r, halfAngleDeg, RangeEdgeRed));
                    _builtHalfAngle = halfAngleDeg;
                }
                // 부채꼴 메시는 로컬 +Z 를 정면으로 만들어 두었다 → 부모를 화면 정면 방향으로 Y축 회전하면 된다.
                Vector3 flat = new Vector3(viewForward.x, 0f, viewForward.z);
                _arcRoot.localRotation = flat.sqrMagnitude > 1e-8f
                    ? Quaternion.LookRotation(flat.normalized, Vector3.up)
                    : Quaternion.identity;
            }

            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        /// <summary> 범위 표시를 즉시 숨긴다(패널 닫힘 · 탑 파괴 · 경기 종료). 이미 숨겨져 있으면 아무 일도 없다. </summary>
        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            // 코드로 만든 메시 · 머티리얼은 씬이 사라져도 자동으로 풀리지 않는다 → 직접 정리(메모리 누수 방지).
            DestroyMesh(_fillFilter);
            DestroyMesh(_ringFilter);
            DestroyMesh(_arcFillFilter);
            DestroyMesh(_arcEdgeFilter);
            if (_material != null) Destroy(_material);
        }

        // ====================================================================
        // 구성 — 층 4개(아래 → 위): 사거리 채움 · 사거리 테두리 · 부채꼴 채움 · 부채꼴 경계선
        //   2026-11-10 부터 한 번에 켜지는 것은 "원 2층" 또는 "부채꼴 2층" 중 한쪽뿐이다(Show 참조).
        // ====================================================================

        /// <summary> 자식 층 GameObject 4개와 공용 머티리얼을 만든다(1회). </summary>
        private void BuildLayers()
        {
            if (_fillFilter != null) return;
            _material = CreateOverlayMaterial();

            _arcRoot = new GameObject("FrontArc").transform;
            _arcRoot.SetParent(transform, false);

            _fillFilter = CreateLayer("RangeFill", transform, 0);
            _ringFilter = CreateLayer("RangeRing", transform, 1);
            _arcFillFilter = CreateLayer("ArcFill", _arcRoot, 2);
            _arcEdgeFilter = CreateLayer("ArcEdge", _arcRoot, 3);
        }

        /// <summary>
        /// 층 하나(MeshFilter + MeshRenderer)를 만든다. 그림자 · 빛 영향 없음, 겹침 순서는 sortingOrder + 미세 높이로 고정한다.
        /// 머티리얼을 끝내 못 만들었으면(에셋 · 셰이더 모두 없음) 렌더러를 꺼 둔다 — 머티리얼 없는 렌더러가 분홍색으로 보이지 않게.
        /// </summary>
        /// <returns>메시를 넣을 MeshFilter.</returns>
        private MeshFilter CreateLayer(string name, Transform parent, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, order * LayerStep, 0f);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.enabled = _material != null;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = order;
            return filter;
        }

        /// <summary>
        /// 지면 데칼 머티리얼 복사본을 만든다. 1순위 Resources 의 NoBuildHatch.mat 복사, 2순위 셰이더 직접 찾기.
        /// 둘 다 실패하면 null(그래도 메시는 그려지지 않을 뿐 게임은 정상 — 경고 1줄).
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
                GameLog.Dev.Warn("UI", nameof(TowerRangeIndicator),
                                 "범위 표시용 지면 데칼 머티리얼 에셋을 찾지 못했다 — 셰이더를 직접 찾아 대체한다",
                                 $"Path=Resources/{OverlayMaterialResourcePath}, ShaderFound={(material != null)}");
            }
            if (material == null) return null;
            material.name = "TowerRangeIndicatorMaterial (runtime)";
            if (material.HasProperty("_Color")) material.SetColor("_Color", MaterialTint);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", MaterialTint);
            return material;
        }

        private static void SetMesh(MeshFilter filter, Mesh mesh)
        {
            if (filter == null) { Destroy(mesh); return; }
            DestroyMesh(filter);
            filter.sharedMesh = mesh;
        }

        private static void DestroyMesh(MeshFilter filter)
        {
            if (filter != null && filter.sharedMesh != null) Destroy(filter.sharedMesh);
        }

        // ====================================================================
        // 메시 만들기 — 모두 로컬 XZ 평면(Y 0), 로컬 +Z 가 각도 0°(정면), 시계 방향(+X 쪽)이 + 각도
        // ====================================================================

        /// <summary> 로컬 +Z 기준 각도(도)의 단위 방향(XZ). 0° = +Z, 90° = +X. </summary>
        private static Vector3 Dir(float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }

        /// <summary> 꽉 찬 원판(중심 1점 + 둘레 점들의 부채 삼각형). </summary>
        private static Mesh BuildDisc(float radius, Color32 color)
        {
            return BuildSector(radius, 180f, color);
        }

        /// <summary> 정면(+Z) 기준 좌우 halfAngleDeg 의 부채꼴 채움. halfAngleDeg 180 이면 원판. </summary>
        private static Mesh BuildSector(float radius, float halfAngleDeg, Color32 color)
        {
            float half = Mathf.Clamp(halfAngleDeg, 0f, 180f);
            int segments = Mathf.Max(2, Mathf.CeilToInt(CircleSegments * (2f * half) / 360f));
            var vertices = new Vector3[segments + 2];
            var colors = new Color32[segments + 2];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            colors[0] = color;
            for (int i = 0; i <= segments; i++)
            {
                float angle = -half + 2f * half * i / segments;
                vertices[i + 1] = Dir(angle) * radius;
                colors[i + 1] = color;
            }
            for (int i = 0; i < segments; i++)
            {
                // 위(+Y)에서 볼 때 앞면이 되도록 (중심, i, i+1) 순서. 셰이더가 Cull Off 라 순서가 틀려도 보이긴 한다.
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            return Finish("TowerRangeSector", vertices, colors, triangles);
        }

        /// <summary> 고리(바깥 반경 ~ 안쪽 반경) — fromDeg ~ toDeg 구간. 360° 전체면 사거리 원 테두리, 일부면 부채꼴 호 테두리. </summary>
        private static Mesh BuildAnnulus(float outer, float inner, float fromDeg, float toDeg, Color32 color)
        {
            float span = Mathf.Clamp(toDeg - fromDeg, 0f, 360f);
            int segments = Mathf.Max(2, Mathf.CeilToInt(CircleSegments * span / 360f));
            var vertices = new Vector3[(segments + 1) * 2];
            var colors = new Color32[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                Vector3 d = Dir(fromDeg + span * i / segments);
                vertices[i * 2] = d * outer;
                vertices[i * 2 + 1] = d * inner;
                colors[i * 2] = color;
                colors[i * 2 + 1] = color;
            }
            for (int i = 0; i < segments; i++)
            {
                int o0 = i * 2, i0 = o0 + 1, o1 = o0 + 2, i1 = o0 + 3;
                triangles[i * 6] = o0; triangles[i * 6 + 1] = o1; triangles[i * 6 + 2] = i0;
                triangles[i * 6 + 3] = i0; triangles[i * 6 + 4] = o1; triangles[i * 6 + 5] = i1;
            }
            return Finish("TowerRangeAnnulus", vertices, colors, triangles);
        }

        /// <summary>
        /// 부채꼴 경계: 좌우 반각 방향의 직선 띠 2개(중심 → 원 둘레) + 그 사이 호 테두리. 한 메시에 합쳐 그린다.
        /// </summary>
        private static Mesh BuildArcEdges(float radius, float halfAngleDeg, Color32 color)
        {
            float half = Mathf.Clamp(halfAngleDeg, 0f, 180f);
            Mesh arc = BuildAnnulus(radius, Mathf.Max(0f, radius - RingThickness), -half, half, color);
            Vector3[] arcVertices = arc.vertices;
            int[] arcTriangles = arc.triangles;
            Destroy(arc);

            var vertices = new Vector3[arcVertices.Length + 8];
            var colors = new Color32[vertices.Length];
            var triangles = new int[arcTriangles.Length + 12];
            arcVertices.CopyTo(vertices, 0);
            arcTriangles.CopyTo(triangles, 0);
            int v = arcVertices.Length, t = arcTriangles.Length;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 d = Dir(side * half);
                Vector3 n = new Vector3(d.z, 0f, -d.x) * (EdgeThickness * 0.5f); // 띠 두께 방향(선에 수직)
                vertices[v] = -n; vertices[v + 1] = n; vertices[v + 2] = d * radius - n; vertices[v + 3] = d * radius + n;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
                v += 4; t += 6;
            }
            for (int i = 0; i < colors.Length; i++) colors[i] = color;
            return Finish("TowerRangeArcEdges", vertices, colors, triangles);
        }

        private static Mesh Finish(string name, Vector3[] vertices, Color32[] colors, int[] triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.colors32 = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
