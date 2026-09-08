// ============================================================================
// HexGridRenderer.cs
// HexGrid(Domain 데이터)를 받아 화면에 타일 프리팹들을 배치하는 렌더러.
//
// 이 스크립트가 부착되는 오브젝트:
//   [World]/HexGrid (빈 GameObject) — 모든 타일의 부모
//
// 역할:
//   1. HexGrid의 타일을 순회
//   2. 각 HexCoord → HexMetrics.HexToWorld()로 월드 좌표 계산
//   3. 타일 프리팹을 Instantiate하여 XZ 평면에 배치
//   4. HexTileView 컴포넌트를 Initialize()로 초기화
//
// ─────────────────────────────────────────────────────────────────────────────
// 무작위 맵 2단계 J — TileKind(지형 종류)별 렌더 규칙
//   (단일 소스: GameSystemRules_RandomMap.md 5장 규칙 9·10 / GameSystemRules_UI.md
//    「무작위 맵 타일 선택과 건설 패널」)
//
//   · TileKind.Normal   : 지금까지와 완전히 같다. 타일 프리팹 1개를 그대로 만든다.
//   · TileKind.NoBuild  : 겉모습(메시·높이·소유권 색)은 일반 타일과 똑같이 만들고,
//                         그 "위에" 반투명 짙은 회색 대각선 빗금 3개를 덧그린다.
//                         빗금은 타일과 별개의 자식 오브젝트라서, 선택 하이라이트
//                         (HexTileView가 타일 자신의 머티리얼 색을 바꾸는 방식)와
//                         서로를 지우지 않고 동시에 보인다.
//   · TileKind.Blocked  : 타일 오브젝트를 "아예 만들지 않는다".
//                         → 메시도 collider도 없으므로 화면에서는 구멍(빈 공간)이 되고,
//                           클릭 레이캐스트에도 잡히지 않는다.
//                         ⚠️ "투명하게 그린다"가 아니다. collider가 남아 있으면 클릭이
//                            잡혀서 GridInteractionUseCase의 판정 순서가 무의미해진다.
//
// Presentation 레이어 — Unity 의존.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Core;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    public class HexGridRenderer : MonoBehaviour
    {
        // ====================================================================
        // Inspector에서 설정할 필드
        // ====================================================================

        [Header("Prefabs")]
        /// <summary> PointyTop 타일 프리팹. </summary>
        [Tooltip("PointyTop 타일 프리팹 (Renderer + Collider + HexTileView)")]
        [SerializeField] private GameObject _pointyTopTilePrefab;

        /// <summary> FlatTop 타일 프리팹. </summary>
        [Tooltip("FlatTop 타일 프리팹 (Renderer + Collider + HexTileView)")]
        [SerializeField] private GameObject _flatTopTilePrefab;

        [Header("Gold Mine")]
        /// <summary> 금광 프리팹. 3D 전환 후 메시 기반 오브젝트로 교체 예정. </summary>
        [Tooltip("금광 프리팹 (3D 메시 또는 임시 스프라이트)")]
        [SerializeField] private GameObject _goldMinePrefab;

        [Header("Config")]
        /// <summary> 전역 설정. 각 타일의 HexTileView에 전달. </summary>
        [Tooltip("GameConfig ScriptableObject 참조")]
        [SerializeField] private GameConfig _config;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        // 생성된 모든 타일 View를 좌표로 인덱싱.
        private readonly Dictionary<HexCoord, HexTileView> _tileViews = new Dictionary<HexCoord, HexTileView>();

        // 생성된 금광 오버레이 오브젝트들. 좌표를 키로 사용하여 특정 광산을 빠르게 조회.
        // 건물 배치/파괴 시 해당 좌표의 광산 오브젝트를 숨기거나 다시 표시하기 위해
        // List에서 Dictionary로 변경함.
        private readonly Dictionary<HexCoord, GameObject> _goldMineObjects = new Dictionary<HexCoord, GameObject>();

        /// <summary>
        /// 생성된 타일 View 딕셔너리 (읽기 전용).
        /// ⚠️ TileKind.Blocked 좌표는 타일 오브젝트를 만들지 않으므로 여기에 들어오지 않는다.
        ///    "논리 격자(HexGrid.Tiles)에는 있지만 화면에는 없는" 좌표가 존재한다는 뜻이다.
        /// </summary>
        public IReadOnlyDictionary<HexCoord, HexTileView> TileViews => _tileViews;

        // ====================================================================
        // 건설 불가(NoBuild) 빗금 오버레이 — 수치 상수
        //
        // 왜 코드 상수인가:
        //   [SerializeField]로 빼면 씬에 이미 배치된 HexGridRenderer 컴포넌트의
        //   Inspector 값이 우선하게 되어, 새 필드가 "0"으로 저장된 씬에서는 빗금이
        //   보이지 않는 사고가 난다(프로젝트 공통 교훈: Inspector 값이 코드 기본값보다 우선).
        //   빗금 "모양"(길이·폭·간격·높이)은 타일 메시에 맞춰 계산된 값이라 조정 대상이 아니므로
        //   상수로 고정한다.
        //
        // ⚠️ 단, 빗금 "색·투명도"는 여기 상수가 아니라 머티리얼 에셋
        //   (Assets/_Project/Resources/Materials/NoBuildHatch.mat)에 들어 있다.
        //   실기에서 눈으로 보고 조정해야 하는 값이라 에셋으로 뺐다. 아래 HatchColor 는
        //   그 에셋을 못 찾았을 때만 쓰이는 최후 수단용 값이다.
        //
        // 타일 메시 실측값(HexTile.prefab):
        //   정육각형(FlatTop) · 외접원 반지름 0.5 · 윗면 y = +0.05 · 아랫면 y = -0.05
        // ====================================================================

        /// <summary>
        /// 빗금 한 줄의 길이 절반. 빗금은 45도 대각선이라 육각형 밖으로 삐져나가기 쉽다.
        /// 0.34는 아래 간격(0.19)·폭 절반(0.0375)과 조합했을 때 빗금 3개의 네 꼭짓점이
        /// 전부 육각형 내부에 들어오는 값이다(기하 계산으로 확인).
        /// </summary>
        private const float HatchHalfLength = 0.34f;

        /// <summary> 빗금 한 줄의 폭 절반. 값이 커질수록 굵은 빗금이 된다. </summary>
        private const float HatchHalfWidth = 0.0375f;

        /// <summary> 빗금 사이 간격(빗금에 수직인 방향). 3개를 -1 / 0 / +1 배수 위치에 놓는다. </summary>
        private const float HatchSpacing = 0.19f;

        /// <summary>
        /// 빗금을 띄울 높이(타일 로컬 기준).
        /// 타일 윗면이 y=+0.05이므로 그보다 0.01만큼 위에 둔다.
        /// 같은 높이에 두면 깊이 싸움(z-fighting)이 나서 빗금이 타일에 파묻혀 깜빡인다.
        /// </summary>
        private const float HatchYOffset = 0.06f;

        /// <summary>
        /// 빗금 전용 머티리얼 에셋의 Resources 경로.
        ///
        /// 🔴 Resources.Load 에 넘기는 경로는 "Resources 폴더 다음"부터 쓰고 확장자를 붙이지 않는다.
        ///    실제 파일: Assets/_Project/Resources/Materials/NoBuildHatch.mat
        ///    (같은 형태의 선례: MapFallbackTemplateFactory.GetTemplateResourcePath)
        ///
        /// 🔴 왜 Resources 인가:
        ///    Resources 폴더 아래에 있는 에셋은 씬이나 프리팹이 참조하지 않아도 빌드에 무조건 포함된다.
        ///    종전처럼 Shader.Find 로 찾으면, 그 셰이더가 빌드에 들어가는 근거가
        ///    "스킬 조준 기능이 쓰는 머티리얼을 씬이 참조한다"는 남의 사정에 얹혀 있게 된다.
        ///    스킬 조준 UI를 빼거나 그 머티리얼을 바꾸는 순간 빗금이 조용히 사라진다
        ///    (에디터에서는 계속 보이므로 빌드에서만 드러난다). 전용 에셋으로 그 의존을 끊는다.
        /// </summary>
        private const string HatchMaterialResourcePath = "Materials/NoBuildHatch";

        /// <summary>
        /// 빗금 색 — 반투명 짙은 회색. 팀 색이 비쳐 보이도록 알파를 1보다 낮게 둔다.
        ///
        /// ⚠️ 이 값은 "머티리얼 에셋을 못 찾았을 때만" 쓰이는 최후 수단용 색이다.
        ///    평소 화면에 보이는 색은 위 NoBuildHatch.mat 에 저장된 _Color 값이고,
        ///    조정도 그 에셋의 Inspector 에서 한다(코드가 에셋 색을 덮어쓰지 않는다).
        /// </summary>
        private static readonly Color HatchColor = new Color(0.12f, 0.12f, 0.12f, 0.62f);

        /// <summary>
        /// 빗금 메시. 모든 NoBuild 타일이 똑같은 모양이므로 한 번만 만들어 전부가 공유한다.
        /// (타일마다 새로 만들면 231칸 규모에서 불필요한 메모리·GC가 생긴다)
        /// </summary>
        private static Mesh _hatchMesh;

        /// <summary> 빗금 머티리얼. 메시와 같은 이유로 공유한다. </summary>
        private static Material _hatchMaterial;

        // ====================================================================
        // 그리드 렌더링
        // ====================================================================

        /// <summary>
        /// HexGrid 데이터를 받아 화면에 타일을 배치.
        /// GameBootstrapper에서 그리드 생성 직후 호출.
        /// </summary>
        /// <param name="grid">렌더링할 헥스 그리드 데이터</param>
        public void RenderGrid(HexGrid grid)
        {
            // 현재 orientation에 맞는 프리팹 선택
            GameObject prefab = (HexMetrics.Orientation == HexOrientation.FlatTop)
                ? _flatTopTilePrefab : _pointyTopTilePrefab;

            if (prefab == null)
            {
                // [개발] Warn + 개발.
                //   원본은 LogError 였지만 이건 Inspector 프리팹 슬롯 미배선이다 — 설정 오류라
                //   LogRules 1.3 원칙 3 단서에 따라 Warn + 개발로 낮춘다.
                //   현재 orientation 을 key=value 로 남겨, 두 슬롯 중 어느 쪽이 비었는지 바로 알 수 있게 한다.
                GameLog.Dev.Warn("HexGrid", nameof(HexGridRenderer),
                                 "타일 프리팹 미배선 — 그리드를 렌더링할 수 없다",
                                 $"Orientation={HexMetrics.Orientation}");
                return;
            }

            // 기존 타일 제거 (재렌더링 시 안전)
            ClearGrid();

            // 모든 타일 순회하여 프리팹 생성
            foreach (var kvp in grid.Tiles)
            {
                HexCoord coord = kvp.Key;
                HexTile tile = kvp.Value;

                // ------------------------------------------------------------
                // [J] TileKind.Blocked — 타일을 만들지 않고 건너뛴다.
                //
                //   여기서 continue 하면 이 좌표에는 메시도, collider도, HexTileView도
                //   생기지 않는다. 그래서 화면에서는 그 자리가 그냥 "빈 공간"이 되고
                //   클릭 레이캐스트에도 걸리지 않는다.
                //
                //   ⚠️ 투명 머티리얼로 "안 보이게" 처리하면 collider는 그대로 남아
                //      클릭이 잡히고, 결국 아래 GridInteractionUseCase의 판정 순서가
                //      의미를 잃는다. 그래서 "안 그린다"가 아니라 "안 만든다"이다.
                // ------------------------------------------------------------
                if (tile != null && tile.TileKind == TileKind.Blocked) continue;

                // 헥스 좌표 → 도메인 월드 좌표 변환 (XZ 평면)
                Vector3 worldPos = HexMetrics.HexToWorld(coord);

                // 도메인 좌표 → 뷰 좌표 변환 (Red팀이면 맵 중심 기준 반전)
                Vector3 viewPos = ViewConverter.ToView(worldPos);

                // 프리팹 인스턴스 생성. 뷰 좌표에 배치.
                GameObject tileObj = Instantiate(prefab, viewPos, Quaternion.identity, transform);

                // 오브젝트 이름을 좌표로 설정 (에디터 Hierarchy에서 식별 용이)
                tileObj.name = $"Tile_{coord}";

                // HexTileView 초기화 (좌표, 설정 전달)
                var tileView = tileObj.GetComponent<HexTileView>();
                if (tileView != null)
                {
                    tileView.Initialize(coord, _config);
                    _tileViews[coord] = tileView;
                }

                // ------------------------------------------------------------
                // [J] TileKind.NoBuild — 일반 타일과 똑같이 만든 뒤 빗금만 덧붙인다.
                //
                //   빗금은 타일의 "자식" 오브젝트다. HexTileView가 선택 하이라이트를
                //   적용할 때 건드리는 것은 타일 자신의 머티리얼(_BaseColor)뿐이라,
                //   자식인 빗금은 영향을 받지 않는다.
                //   → 선택 하이라이트와 빗금이 서로를 대체하지 않고 함께 보인다.
                // ------------------------------------------------------------
                if (tile != null && tile.TileKind == TileKind.NoBuild)
                {
                    CreateNoBuildHatch(tileObj);
                }
            }
        }

        /// <summary>
        /// 모든 타일 오브젝트를 제거. 재렌더링 또는 씬 정리 시 사용.
        /// </summary>
        private void ClearGrid()
        {
            // 이 오브젝트의 모든 자식(타일들) 파괴
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            _tileViews.Clear();
            _goldMineObjects.Clear();
        }

        // ====================================================================
        // 건설 불가(NoBuild) 빗금 오버레이 생성
        // ====================================================================

        /// <summary>
        /// NoBuild 타일 위에 반투명 짙은 회색 대각선 빗금 3개를 덧그린다.
        ///
        /// 구조:
        ///   Tile_(q,r)            ← 일반 타일과 완전히 동일한 프리팹 인스턴스
        ///     └ NoBuildHatch      ← 이 메서드가 만드는 자식 (MeshFilter + MeshRenderer만)
        ///
        /// collider를 붙이지 않는 이유:
        ///   NoBuild 타일은 "선택은 되는" 타일이다. 클릭 판정은 부모 타일의 collider가
        ///   그대로 담당해야 하므로, 빗금은 순수하게 보여주기만 하는 오브젝트여야 한다.
        /// </summary>
        /// <param name="tileObj">빗금을 붙일 타일 오브젝트(부모가 된다)</param>
        private void CreateNoBuildHatch(GameObject tileObj)
        {
            Mesh mesh = GetHatchMesh();
            Material material = GetHatchMaterial();

            // 셰이더를 못 찾은 경우 등 — 빗금만 생략하고 타일 자체는 정상 표시한다.
            if (mesh == null || material == null) return;

            var hatchObj = new GameObject("NoBuildHatch");

            // worldPositionStays: false → 부모의 로컬 좌표계를 그대로 쓰겠다는 뜻.
            hatchObj.transform.SetParent(tileObj.transform, false);
            hatchObj.transform.localPosition = new Vector3(0f, HatchYOffset, 0f);
            hatchObj.transform.localRotation = Quaternion.identity;
            hatchObj.transform.localScale = Vector3.one;

            hatchObj.AddComponent<MeshFilter>().sharedMesh = mesh;

            var meshRenderer = hatchObj.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;

            // 얇은 장식이라 그림자를 주고받을 이유가 없다. 모바일 렌더 비용도 아낀다.
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        /// <summary>
        /// 빗금 3개짜리 메시를 만든다(최초 1회). 이후에는 캐시를 그대로 돌려준다.
        ///
        /// 만드는 방법:
        ///   XZ 평면에서 45도 대각선 방향(dir)으로 뻗는 얇은 사각형 3개를 만들고,
        ///   그 대각선에 수직인 방향(perp)으로 -1 / 0 / +1 칸씩 밀어 나란히 배치한다.
        ///   사각형 하나 = 정점 4개 + 삼각형 2개 → 3개면 정점 12개 + 삼각형 6개.
        /// </summary>
        private static Mesh GetHatchMesh()
        {
            // Unity의 == 는 "파괴된 오브젝트"도 null로 판정한다.
            // 씬을 다시 로드해 메시가 정리된 경우에도 여기서 다시 만들어진다.
            if (_hatchMesh != null) return _hatchMesh;

            // 45도 방향 단위벡터 성분 (= 1/√2)
            const float Diagonal = 0.70710678f;

            Vector3 dir = new Vector3(Diagonal, 0f, Diagonal);   // 빗금이 뻗는 방향
            Vector3 perp = new Vector3(Diagonal, 0f, -Diagonal); // 빗금을 나란히 벌리는 방향

            var vertices = new Vector3[12];
            var normals = new Vector3[12];
            var uvs = new Vector2[12];
            var colors = new Color[12];
            var triangles = new int[18];

            for (int i = 0; i < 3; i++)
            {
                int slot = i - 1;                                  // -1, 0, +1
                Vector3 center = perp * (slot * HatchSpacing);     // 이 빗금의 중심
                Vector3 along = dir * HatchHalfLength;             // 길이 방향 절반
                Vector3 side = perp * HatchHalfWidth;              // 폭 방향 절반

                int v = i * 4;
                vertices[v + 0] = center - along - side;
                vertices[v + 1] = center - along + side;
                vertices[v + 2] = center + along + side;
                vertices[v + 3] = center + along - side;

                for (int k = 0; k < 4; k++)
                {
                    normals[v + k] = Vector3.up;   // 위(하늘)를 향하는 면
                    colors[v + k] = Color.white;   // 셰이더가 정점색 × 머티리얼 색으로 계산한다
                }

                uvs[v + 0] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(1f, 1f);
                uvs[v + 3] = new Vector2(0f, 1f);

                // 삼각형 감는 순서(winding): 위(+Y)에서 봤을 때 앞면이 되도록 0-3-2 / 0-2-1.
                int t = i * 6;
                triangles[t + 0] = v + 0;
                triangles[t + 1] = v + 3;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 0;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 1;
            }

            var mesh = new Mesh { name = "NoBuildHatchMesh" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            _hatchMesh = mesh;
            return _hatchMesh;
        }

        /// <summary>
        /// 빗금 머티리얼을 구한다(최초 1회). 이후에는 캐시를 그대로 돌려준다.
        ///
        /// 구하는 순서:
        ///   1순위 — Resources 의 전용 머티리얼 에셋(NoBuildHatch.mat).
        ///     Resources 아래에 있으므로 아무도 참조하지 않아도 빌드에 반드시 포함되고,
        ///     그 에셋이 셰이더(Hexiege/SkillAimOverlay)를 참조하므로 셰이더도 함께 포함된다.
        ///     색·투명도는 이 에셋에 들어 있다 → 실기에서 보고 Inspector 로 바로 조정할 수 있다.
        ///     🔴 그래서 여기서 색을 다시 칠하지 않는다. 코드가 덮어쓰면 조정이 무의미해진다.
        ///
        ///   2순위(최후 수단) — Shader.Find 로 셰이더만 찾아 머티리얼을 즉석에서 만든다.
        ///     에셋이 없거나 이름이 바뀐 비정상 상태이므로, 이 경로로 내려왔다는 사실 자체를
        ///     아래 경고 로그로 남긴다. 이때만 코드 상수 HatchColor 를 색으로 쓴다.
        ///
        /// 셰이더 Hexiege/SkillAimOverlay 를 쓰는 이유:
        ///   스킬 조준원이 쓰던 "지면 데칼" 셰이더다. 반투명 + ZWrite Off + 깊이 오프셋이 들어 있어
        ///   타일 윗면과 겹쳐도 깜빡이지 않고(z-fighting), 유닛·건물 같은 불투명 물체에는
        ///   정상적으로 가려진다.
        /// </summary>
        private static Material GetHatchMaterial()
        {
            if (_hatchMaterial != null) return _hatchMaterial;

            // 1순위 — 전용 머티리얼 에셋.
            // Resources.Load 는 못 찾으면 예외 없이 null 을 돌려준다.
            Material asset = Resources.Load<Material>(HatchMaterialResourcePath);
            if (asset != null)
            {
                // 에셋을 그대로 공유해서 쓴다.
                // 색을 건드리지 않으므로 인스턴스 복제도 필요 없다(머티리얼 인스턴스 증가 방지).
                _hatchMaterial = asset;
                return _hatchMaterial;
            }

            // ----------------------------------------------------------------
            // 2순위 — 최후 수단. 여기 내려온 것 자체가 "전용 에셋을 못 찾았다"는 뜻이다.
            // ----------------------------------------------------------------

            // [개발] Warn + 개발 — 에셋/셰이더 구성 문제이고, 있으면 모든 기기에 있고
            //   없으면 모든 기기에 없다(플레이어 기기 고유 사건이 아님).
            //   아래에서 셰이더를 찾으면 빗금은 계속 보이므로 대체 경로가 있다 → Warn.
            GameLog.Dev.Warn("HexGrid", nameof(HexGridRenderer),
                             "빗금 전용 머티리얼 에셋을 찾지 못했다 — Shader.Find 최후 수단으로 대체한다",
                             $"Path=Resources/{HatchMaterialResourcePath}");

            Shader shader = Shader.Find("Hexiege/SkillAimOverlay");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                // 셰이더까지 못 찾으면 빗금만 생략하고 타일은 정상 표시한다(호출부 null 처리).
                GameLog.Dev.Warn("HexGrid", nameof(HexGridRenderer),
                                 "빗금 오버레이용 셰이더도 찾지 못했다 — NoBuild 타일에 빗금이 표시되지 않는다",
                                 "Shader=Hexiege/SkillAimOverlay");
                return null;
            }

            var material = new Material(shader) { name = "NoBuildHatchMaterial" };

            // 에셋이 없어 색을 가져올 곳이 없으므로, 이 경로에서만 코드 상수를 쓴다.
            // 셰이더마다 색 프로퍼티 이름이 달라 둘 다 시도한다(있는 쪽만 적용됨).
            if (material.HasProperty("_Color")) material.SetColor("_Color", HatchColor);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", HatchColor);

            _hatchMaterial = material;
            return _hatchMaterial;
        }

        // ====================================================================
        // 금광 오버레이 렌더링
        // ====================================================================

        /// <summary>
        /// 금광이 있는 타일 위에 금광 오브젝트를 생성.
        /// GameBootstrapper에서 PlaceGoldMines() 후 호출.
        ///
        /// _goldMinePrefab이 null이면 금광 비주얼 생략 (프리팹 미설정 상태).
        ///
        /// 초기 숨김 처리:
        ///   PlaceGoldMines()에서 시작 채굴소를 PlaceMiningPostDirect()로 배치하면
        ///   해당 타일의 Owner가 Blue/Red로 변경됨.
        ///   따라서 Owner가 Neutral이 아닌 금광 타일 = 이미 건물이 배치된 타일이므로
        ///   생성 직후 SetActive(false)로 숨김.
        ///   (이 시점에선 OnBuildingPlaced 이벤트가 이미 발행된 후이므로
        ///    이벤트로는 초기 숨김 처리 불가.)
        /// </summary>
        public void RenderGoldMines(HexGrid grid)
        {
            if (_goldMinePrefab == null || grid == null) return;

            foreach (var kvp in grid.Tiles)
            {
                if (kvp.Value.MineKind == MineKind.None) continue;

                HexCoord coord = kvp.Key;
                Vector3 worldPos = HexMetrics.HexToWorld(coord);

                // 도메인 좌표 → 뷰 좌표 변환 (Red팀이면 맵 중심 기준 반전)
                Vector3 viewPos = ViewConverter.ToView(worldPos);

                // 금광 프리팹 인스턴스 생성 (약간 위에 배치하여 타일과 겹침 방지)
                GameObject mineObj = Instantiate(
                    _goldMinePrefab,
                    viewPos + new Vector3(0f, 0.05f, 0f),
                    Quaternion.identity,
                    transform
                );
                mineObj.name = $"GoldMine_{coord}";

                // 좌표를 키로 저장하여 나중에 HideGoldMine/ShowGoldMine에서 조회 가능
                _goldMineObjects[coord] = mineObj;

                // 초기 숨김: 이미 건물(시작 채굴소)이 배치된 금광 타일은 숨김.
                // PlaceMiningPostDirect()가 타일 Owner를 해당 팀으로 설정하므로,
                // Owner가 Neutral이 아닌 금광 타일 = 이미 건물이 존재하는 타일.
                bool alreadyOccupied = kvp.Value.Owner != TeamId.Neutral;
                if (alreadyOccupied)
                {
                    mineObj.SetActive(false);
                }
            }

            // 금광 오브젝트 생성 완료 후 이벤트 구독 시작.
            // 이후 건물 배치/파괴 시 광산 오브젝트를 실시간으로 숨기거나 표시.
            SubscribeGoldMineEvents();
        }

        // ====================================================================
        // 금광 오브젝트 표시/숨김
        // ====================================================================

        /// <summary>
        /// 건물 배치/파괴 이벤트를 구독하여 금광 오브젝트를 실시간으로 숨기거나 표시.
        ///
        /// (a) OnBuildingPlaced: 건물이 배치되면 해당 좌표의 금광 오브젝트를 숨김.
        ///     - 금광 타일 위에 채굴소가 건설되면 금광 비주얼이 가려져야 함.
        ///     - MiningPost뿐 아니라 모든 건물 타입에 대해 숨김 처리 (안전장치).
        ///
        /// (b) OnBuildingDied: 채굴소(MiningPost)가 파괴되면 금광 오브젝트를 다시 표시.
        ///     - 채굴소만 금광 타일 위에 건설되므로, MiningPost 타입만 필터링.
        ///     - 다른 건물 타입(Castle, Barracks)은 금광 타일과 무관하므로 무시.
        ///     - 사망 이벤트 분리(OnUnitDied/OnBuildingDied) 이후 캐스트가 불필요해졌다.
        ///
        /// .AddTo(this): 이 MonoBehaviour가 Destroy되면 자동으로 구독 해제.
        /// HexTileView.SubscribeEvents()와 동일한 패턴.
        /// </summary>
        private void SubscribeGoldMineEvents()
        {
            // (a) 건물 배치 시 → 해당 좌표의 금광 오브젝트 숨김
            GameEvents.OnBuildingPlaced
                .Subscribe(e => HideGoldMine(e.Building.Position))
                .AddTo(this);

            // (b) 건물 사망 시 → 채굴소(MiningPost)인 경우만 금광 오브젝트 재표시.
            // 사망 이벤트가 유닛/건물로 분리되어 건물 전용 OnBuildingDied만 구독하면 충분하다.
            GameEvents.OnBuildingDied
                .Subscribe(e =>
                {
                    // MiningPost만 금광 타일 위에 건설되므로 그 외 타입은 무시.
                    if (e.Building.Type == BuildingType.MiningPost)
                    {
                        ShowGoldMine(e.Building.Position);
                    }
                })
                .AddTo(this);
        }

        /// <summary>
        /// 지정 좌표의 금광 오브젝트를 숨김 (비활성화).
        /// 건물이 배치되어 금광 비주얼이 가려져야 할 때 호출.
        /// 해당 좌표에 금광 오브젝트가 없으면 아무 동작 안 함 (null 안전).
        /// </summary>
        /// <param name="coord">숨길 금광의 헥스 좌표</param>
        public void HideGoldMine(HexCoord coord)
        {
            if (_goldMineObjects.TryGetValue(coord, out GameObject mineObj) && mineObj != null)
            {
                mineObj.SetActive(false);
            }
        }

        /// <summary>
        /// 지정 좌표의 금광 오브젝트를 다시 표시 (활성화).
        /// 채굴소가 파괴되어 금광 비주얼이 다시 보여야 할 때 호출.
        /// 해당 좌표에 금광 오브젝트가 없으면 아무 동작 안 함 (null 안전).
        /// </summary>
        /// <param name="coord">표시할 금광의 헥스 좌표</param>
        public void ShowGoldMine(HexCoord coord)
        {
            if (_goldMineObjects.TryGetValue(coord, out GameObject mineObj) && mineObj != null)
            {
                mineObj.SetActive(true);
            }
        }

        // ====================================================================
        // 타일 View 조회
        // ====================================================================

        /// <summary>
        /// 좌표로 특정 타일의 View를 조회. 없으면 null.
        /// </summary>
        public HexTileView GetTileView(HexCoord coord)
        {
            _tileViews.TryGetValue(coord, out HexTileView view);
            return view;
        }
    }
}
