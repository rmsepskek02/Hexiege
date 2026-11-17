// ============================================================================
// SkillAimTileHighlight.cs
// 스킬 조준 중 "맞는 타일"의 육각 테두리를 땅바닥에 짚어 보여 주는 표시물(원형 · 공습 공통).
// (2026-11-18 지원 스킬 2-L 공습 Task 단계 L-7 조준 UI 개선 — Plan 14절 UI-2a)
//
// 무엇을 하나(유니티 초급자용 설명):
//   · 조준 컨트롤러(SkillAimController)가 "맞는 타일 목록"을 넘겨주면 그 타일마다 육각형 테두리(띠)를 그린다.
//     어떤 타일이 맞는지는 이 컴포넌트가 정하지 않는다 — 서버 판정과 같은 기준의 순수 함수(SkillAimVisualMath)가 정한다.
//     (「보이는 곳 = 맞는 곳」 — 이 컴포넌트는 받은 대로 그리기만 한다.)
//   · 육각형 모양은 타일 배치 간격(HexMetrics.TileWidth · TileHeight)과 방향(PointyTop / FlatTop)으로 만든다 →
//     타일과 같은 간격으로 빈틈없이 맞물리는 육각형이다. 띠는 그 육각형의 안쪽에 그려 이웃 타일 테두리와 겹치지 않는다.
//   · 빨강(뒤집힌) 화면에서도 육각형은 180° 돌려도 같은 모양이라 꼭짓점 위치를 따로 바꿀 필요가 없다(중심만 ToView 로 바꾼다).
//
// 성능(모바일 — 매 프레임 할당 금지):
//   · 메시 하나에 모든 육각형을 담는다(그리기 호출 1번). 육각형 1개 = 정점 12개 · 삼각형 12개.
//   · 정점 · 색 · 삼각형 배열은 "용량"만큼 미리 만들어 두고 재사용한다(풀). 쓰지 않는 칸은 정점을 한 점에 모아(넓이 0)
//     화면에 아무것도 그리지 않게 한다. 맞는 타일이 용량보다 많아질 때만 배열을 2배로 키운다(드물다 — 그때 1번 할당).
//   · 타일 목록이 바뀌었을 때만 정점을 다시 넣는다(호출자가 바뀐 경우에만 Show 를 부른다).
//   · 색(상태)은 머티리얼 _Color 하나만 바꾼다(메시 재생성 없음 — SkillAimVisualStyle).
//
// 새 아트 에셋 · 새 셰이더 없이 만든다 — 머티리얼은 기존 지면 데칼 머티리얼 복사(규칙 22-1 — 타일에는 안 묻히고 유닛 · 건물에는 가려진다).
// Presentation 레이어. 화면 전용(판정에 아무 영향 없음).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Core;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 조준 범위 안 타일의 육각 테두리 하이라이트(메시 1개 · 배열 재사용).
    /// </summary>
    public sealed class SkillAimTileHighlight : MonoBehaviour
    {
        // ====================================================================
        // 모양 상수(화면 전용 임시값)
        // ====================================================================

        /// <summary> 지면 위 높이(월드 Y). 조준 채움(0.06)보다 아주 조금 위 — 테두리가 채움 위에 보이게. 타일 윗면은 0.05. </summary>
        private const float GroundY = 0.062f;

        /// <summary> 띠 바깥선 = 타일 육각형 × 이 비율(이웃 타일 테두리와 붙지 않게 살짝 안쪽). </summary>
        private const float OuterInset = 0.93f;

        /// <summary> 띠 안쪽선 = 타일 육각형 × 이 비율. 바깥선과의 차이가 띠 두께다. </summary>
        private const float InnerInset = 0.8f;

        /// <summary> 바깥선 정점 알파(진함) · 안쪽선 정점 알파(옅음) — 안쪽으로 부드럽게 사라지는 띠. </summary>
        private const byte OuterAlpha = 235;
        private const byte InnerAlpha = 60;

        /// <summary> 처음 만들 때 담을 수 있는 육각형 수(모자라면 2배로 늘린다). </summary>
        private const int InitialCapacity = 48;

        private const int VerticesPerHex = 12;
        private const int IndicesPerHex = 12 * 3;

        /// <summary> 한 번에 그리는 육각형 최대 수(16비트 정점 번호 65,535 ÷ 12 아래). </summary>
        private const int MaxHexes = 5000;

        /// <summary> 조준 표시물 사이 겹침 순서 — 원형 채움(1) · 바깥 링(3)보다 위. </summary>
        private const int SortingOrder = 6;

        // ====================================================================
        // 런타임 구성물
        // ====================================================================

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private Material _material;
        private Vector3[] _vertices;
        private Color32[] _colors;
        private int[] _triangles;
        private int _capacity;
        private int _count;

        // 육각형 꼭짓점 6개(중심 기준 오프셋, 월드 XZ). 타일 크기 · 방향이 바뀌면 다시 계산한다.
        private readonly Vector3[] _corners = new Vector3[6];
        private float _cornersTileWidth = -1f;
        private float _cornersTileHeight = -1f;
        private HexOrientation _cornersOrientation;

        // 상태 색(목표 · 현재) — 매 프레임 목표로 부드럽게 따라간다.
        private Color _targetColor = Color.white;
        private Color _currentColor = Color.white;

        /// <summary> 지금 그리고 있는 타일 수(진단 로그 HighlightTiles). 숨겨져 있으면 0. </summary>
        public int Count => gameObject.activeSelf ? _count : 0;

        /// <summary>
        /// 씬에 빈 GameObject 를 만들고 이 컴포넌트를 붙여 돌려준다(씬 수정 없이 런타임 생성 — SkillLineAimReticle 선례).
        /// 씬이 바뀌면 함께 사라지며, 호출자는 다음에 필요할 때 다시 만든다(Unity null 검사).
        /// </summary>
        /// <returns>숨겨진 상태의 새 표시물.</returns>
        public static SkillAimTileHighlight CreateRuntime()
        {
            var go = new GameObject("SkillAimTileHighlight (runtime)");
            var highlight = go.AddComponent<SkillAimTileHighlight>();
            highlight.Build();
            go.SetActive(false);
            return highlight;
        }

        /// <summary>
        /// 넘겨받은 타일들의 테두리를 그린다. 호출자는 타일 목록이 "바뀌었을 때만" 부른다(정점 재계산 비용 절약).
        /// </summary>
        /// <param name="tileCentersDomain">모든 타일 중심(도메인 월드).</param>
        /// <param name="indices">그릴 타일의 번호(tileCentersDomain 안 위치).</param>
        public void Show(IReadOnlyList<Vector3> tileCentersDomain, List<int> indices)
        {
            if (_mesh == null) Build();
            int count = (tileCentersDomain == null || indices == null) ? 0 : indices.Count;
            if (count > MaxHexes) count = MaxHexes; // 16비트 정점 번호 한도(실제 맵에서는 닿지 않는다 — 안전장치)
            EnsureCapacity(count);
            RefreshCornersIfNeeded();

            // 이번에 쓸 칸(count)과 직전에 쓰던 칸(_count) 중 큰 쪽까지만 돈다 — 그 뒤 칸은 이미 비어 있다.
            int end = count > _count ? count : _count;
            for (int h = 0; h < end; h++)
            {
                int v = h * VerticesPerHex;
                if (h < count)
                {
                    // 타일 중심(도메인) → 이 화면(뷰) 좌표. 뒤집힌 화면에서도 육각형 모양은 같다.
                    Vector3 c = ViewConverter.ToView(tileCentersDomain[indices[h]]);
                    for (int k = 0; k < 6; k++)
                    {
                        Vector3 o = _corners[k];
                        _vertices[v + k] = new Vector3(c.x + o.x * OuterInset, GroundY, c.z + o.z * OuterInset);
                        _vertices[v + 6 + k] = new Vector3(c.x + o.x * InnerInset, GroundY, c.z + o.z * InnerInset);
                    }
                }
                else
                {
                    // 직전에 쓰던 칸만 비운다(정점을 한 점에 모아 넓이 0 → 그려지지 않는다). 처음부터 빈 칸은 이미 0 이다.
                    for (int k = 0; k < VerticesPerHex; k++) _vertices[v + k] = Vector3.zero;
                }
            }
            _count = count;
            _mesh.vertices = _vertices; // 같은 배열을 다시 넣는다(새 배열 할당 없음)
            _mesh.RecalculateBounds();

            if (_renderer != null) _renderer.enabled = _material != null && count > 0;
            if (!gameObject.activeSelf)
            {
                // 다시 나타날 때는 색을 목표 색에서 바로 시작한다(지난 조준의 색이 한 박자 남지 않게).
                _currentColor = _targetColor;
                SkillAimVisualStyle.SetTint(_material, _currentColor);
                gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 상태 색을 바꾼다(UI-2d). 실제 색은 매 프레임 목표로 부드럽게 따라간다.
        /// </summary>
        /// <param name="state">조준 표시 상태.</param>
        public void SetState(SkillAimVisualState state)
        {
            _targetColor = SkillAimVisualStyle.TileColor(state);
        }

        /// <summary> 숨긴다(조준 종료). 이미 숨겨져 있으면 아무 일도 없다. </summary>
        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void Update()
        {
            // 상태 색을 목표로 부드럽게(색이 같아지면 사실상 아무 변화 없음 — 할당 없음).
            if (_currentColor != _targetColor)
            {
                _currentColor = SkillAimVisualStyle.TickColor(_currentColor, _targetColor, Time.unscaledDeltaTime);
                SkillAimVisualStyle.SetTint(_material, _currentColor);
            }
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

        /// <summary> 렌더러 · 메시 · 머티리얼을 만든다(1회). </summary>
        private void Build()
        {
            if (_mesh != null) return;
            _material = SkillAimVisualStyle.CreateOverlayMaterial(null, nameof(SkillAimTileHighlight),
                                                                  "SkillAimTileHighlightMaterial (runtime)");
            _filter = gameObject.AddComponent<MeshFilter>();
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.enabled = false;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.sortingOrder = SortingOrder;

            _mesh = new Mesh { name = "SkillAimTileHighlight (runtime)" };
            _mesh.MarkDynamic(); // 조준 중 자주 바뀐다
            // 정점 번호는 기본 16비트(최대 65,535) 그대로 쓴다 — 오래된 모바일 GPU 호환. 그래서 육각형 수를 MaxHexes 로 묶는다.
            _filter.sharedMesh = _mesh;
            EnsureCapacity(InitialCapacity);
        }

        /// <summary>
        /// 육각형 n개를 담을 수 있게 배열을 키운다. 이미 충분하면 아무 일도 없다(할당 없음).
        /// 키울 때는 2배로 키워 다음 몇 번은 할당이 없게 한다. 정점 색 · 삼각형 순서는 여기서만 정한다.
        /// </summary>
        private void EnsureCapacity(int n)
        {
            if (_vertices != null && n <= _capacity) return;
            int newCap = _capacity > 0 ? _capacity : InitialCapacity;
            while (newCap < n) newCap *= 2;
            if (newCap > MaxHexes) newCap = MaxHexes; // n 은 이미 MaxHexes 이하로 묶여 들어온다(Show)

            _vertices = new Vector3[newCap * VerticesPerHex];
            _colors = new Color32[newCap * VerticesPerHex];
            _triangles = new int[newCap * IndicesPerHex];
            for (int h = 0; h < newCap; h++)
            {
                int v = h * VerticesPerHex;
                for (int k = 0; k < 6; k++)
                {
                    _colors[v + k] = new Color32(255, 255, 255, OuterAlpha);
                    _colors[v + 6 + k] = new Color32(255, 255, 255, InnerAlpha);
                }
                // 변 k: 바깥 k · 바깥 k+1 · 안쪽 k · 안쪽 k+1 로 사각형(삼각형 2개). 셰이더가 Cull Off 라 감는 방향과 무관하게 보인다.
                int t = h * IndicesPerHex;
                for (int k = 0; k < 6; k++)
                {
                    int k1 = (k + 1) % 6;
                    _triangles[t++] = v + k;     _triangles[t++] = v + k1;    _triangles[t++] = v + 6 + k;
                    _triangles[t++] = v + 6 + k; _triangles[t++] = v + k1;    _triangles[t++] = v + 6 + k1;
                }
            }
            _capacity = newCap;
            _count = 0; // 새 배열은 모든 칸이 비어 있다(정점 0)

            if (_mesh != null)
            {
                _mesh.Clear();
                _mesh.vertices = _vertices;
                _mesh.colors32 = _colors;
                _mesh.triangles = _triangles;
                _mesh.RecalculateBounds();
            }
        }

        /// <summary>
        /// 타일 간격 · 방향에 맞는 육각형 꼭짓점 6개를 계산한다(타일 배치 공식 HexMetrics.HexToWorld 와 맞물리는 모양).
        ///   PointyTop: 위아래가 뾰족 — (0, ±H/2), (±W/2, ±H/4). 행 간격 0.75H · 홀수 행 W/2 밀림과 빈틈없이 맞물린다.
        ///   FlatTop  : 좌우가 뾰족 — (±W/2, 0), (±W/4, ±H/2). 열 간격 0.75W · 홀수 열 H/2 밀림과 맞물린다.
        /// </summary>
        private void RefreshCornersIfNeeded()
        {
            float w = HexMetrics.TileWidth;
            float hgt = HexMetrics.TileHeight;
            HexOrientation o = HexMetrics.Orientation;
            if (w == _cornersTileWidth && hgt == _cornersTileHeight && o == _cornersOrientation) return;

            if (o == HexOrientation.FlatTop)
            {
                _corners[0] = new Vector3(w * 0.5f, 0f, 0f);
                _corners[1] = new Vector3(w * 0.25f, 0f, -hgt * 0.5f);
                _corners[2] = new Vector3(-w * 0.25f, 0f, -hgt * 0.5f);
                _corners[3] = new Vector3(-w * 0.5f, 0f, 0f);
                _corners[4] = new Vector3(-w * 0.25f, 0f, hgt * 0.5f);
                _corners[5] = new Vector3(w * 0.25f, 0f, hgt * 0.5f);
            }
            else
            {
                _corners[0] = new Vector3(0f, 0f, hgt * 0.5f);
                _corners[1] = new Vector3(w * 0.5f, 0f, hgt * 0.25f);
                _corners[2] = new Vector3(w * 0.5f, 0f, -hgt * 0.25f);
                _corners[3] = new Vector3(0f, 0f, -hgt * 0.5f);
                _corners[4] = new Vector3(-w * 0.5f, 0f, -hgt * 0.25f);
                _corners[5] = new Vector3(-w * 0.5f, 0f, hgt * 0.25f);
            }
            _cornersTileWidth = w;
            _cornersTileHeight = hgt;
            _cornersOrientation = o;
        }
    }
}
