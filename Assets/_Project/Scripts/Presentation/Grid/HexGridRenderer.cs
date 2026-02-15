// ============================================================================
// HexGridRenderer.cs
// HexGrid(Domain 데이터)를 받아 화면에 타일 프리팹들을 배치하는 렌더러.
//
// 이 스크립트가 부착되는 오브젝트:
//   [World]/HexGrid (빈 GameObject) — 모든 타일의 부모
//
// 역할:
//   1. HexGrid의 187개 타일을 순회
//   2. 각 HexCoord → HexMetrics.HexToWorld()로 월드 좌표 계산
//   3. 타일 프리팹을 Instantiate하여 해당 위치에 배치
//   4. HexTileView 컴포넌트를 Initialize()로 초기화
//
// 렌더링 순서 (Sorting):
//   타일은 모두 같은 Sorting Layer("Background")에 배치.
//   row가 큰(화면 아래쪽) 타일이 위에 그려지도록 sortingOrder = row로 설정.
//   (탑다운 2D에서 아래쪽 타일이 위에 겹쳐 보여야 자연스러움)
//
// 타일 프리팹 구조 (Phase 10에서 생성):
//   HexTile
//     ├─ SpriteRenderer (tile_hex.png, Sorting Layer: Background)
//     ├─ PolygonCollider2D (클릭 판정)
//     └─ HexTileView (색상/선택 관리)
//
// Presentation 레이어 — Unity 의존.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;
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
        [Tooltip("PointyTop 타일 프리팹 (SpriteRenderer + Collider + HexTileView)")]
        [SerializeField] private GameObject _pointyTopTilePrefab;

        /// <summary> FlatTop 타일 프리팹. </summary>
        [Tooltip("FlatTop 타일 프리팹 (SpriteRenderer + Collider + HexTileView)")]
        [SerializeField] private GameObject _flatTopTilePrefab;

        [Header("Gold Mine")]
        /// <summary> 금광 오버레이 스프라이트. </summary>
        [Tooltip("금광 스프라이트 (obj_goldmine.png)")]
        [SerializeField] private Sprite _goldMineSprite;

        [Header("Config")]
        /// <summary> 전역 설정. 각 타일의 HexTileView에 전달. </summary>
        [Tooltip("GameConfig ScriptableObject 참조")]
        [SerializeField] private GameConfig _config;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        // 생성된 모든 타일 View를 좌표로 인덱싱.
        // 외부에서 특정 타일의 View에 접근할 때 사용.
        private readonly Dictionary<HexCoord, HexTileView> _tileViews = new Dictionary<HexCoord, HexTileView>();

        // 생성된 금광 오버레이 오브젝트들. ClearGrid 시 함께 정리.
        private readonly List<GameObject> _goldMineObjects = new List<GameObject>();

        /// <summary> 생성된 타일 View 딕셔너리 (읽기 전용). </summary>
        public IReadOnlyDictionary<HexCoord, HexTileView> TileViews => _tileViews;

        // ====================================================================
        // 그리드 렌더링
        // ====================================================================

        /// <summary>
        /// HexGrid 데이터를 받아 화면에 타일을 배치.
        /// GameBootstrapper에서 그리드 생성 직후 호출.
        ///
        /// 처리 순서:
        ///   1. 기존 타일 제거 (중복 호출 방지)
        ///   2. HexGrid의 모든 타일 순회
        ///   3. 각 타일마다: 월드 좌표 계산 → 프리팹 생성 → HexTileView 초기화
        ///   4. Sorting 설정 (row 기반)
        /// </summary>
        /// <param name="grid">렌더링할 헥스 그리드 데이터</param>
        public void RenderGrid(HexGrid grid)
        {
            // 현재 orientation에 맞는 프리팹 선택
            GameObject prefab = (HexMetrics.Orientation == HexOrientation.FlatTop)
                ? _flatTopTilePrefab : _pointyTopTilePrefab;

            if (prefab == null)
            {
                Debug.LogError("[HexGridRenderer] TilePrefab이 설정되지 않았습니다.");
                return;
            }

            // 기존 타일 제거 (재렌더링 시 안전)
            ClearGrid();

            // 모든 타일 순회하여 프리팹 생성
            foreach (var kvp in grid.Tiles)
            {
                HexCoord coord = kvp.Key;
                HexTile tileData = kvp.Value;

                // 헥스 좌표 → 월드 좌표 변환
                Vector3 worldPos = HexMetrics.HexToWorld(coord);

                // 프리팹 인스턴스 생성. 이 오브젝트(HexGrid)의 자식으로 배치.
                GameObject tileObj = Instantiate(prefab, worldPos, Quaternion.identity, transform);

                // 오브젝트 이름을 좌표로 설정 (에디터 Hierarchy에서 식별 용이)
                tileObj.name = $"Tile_{coord}";

                // --------------------------------------------------------
                // Sorting 설정
                // 화면 아래쪽 타일이 나중에 그려져 위에 표시.
                // 탑다운 2D에서 아래쪽 오브젝트가 앞에 보여야 자연스러움.
                //
                // PointyTop: coord.R (행 인덱스)로 정렬 — R이 화면 Y와 직접 대응.
                // FlatTop: 월드 Y 좌표 기반 정렬 — 홀수 열 시프트로 R만으로는 부정확.
                // --------------------------------------------------------
                var sr = tileObj.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    if (HexMetrics.Orientation == Domain.HexOrientation.FlatTop)
                        sr.sortingOrder = Mathf.RoundToInt(-worldPos.y * 3);
                    else
                        sr.sortingOrder = coord.R;
                }

                // HexTileView 초기화 (좌표, 설정 전달)
                var tileView = tileObj.GetComponent<HexTileView>();
                if (tileView != null)
                {
                    tileView.Initialize(coord, _config);
                    _tileViews[coord] = tileView;
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
        // 금광 오버레이 렌더링
        // ====================================================================

        /// <summary>
        /// 금광이 있는 타일 위에 스프라이트 오버레이를 생성.
        /// GameBootstrapper에서 PlaceGoldMines() 후 호출.
        /// </summary>
        public void RenderGoldMines(HexGrid grid)
        {
            if (_goldMineSprite == null || grid == null) return;

            foreach (var kvp in grid.Tiles)
            {
                if (!kvp.Value.HasGoldMine) continue;

                Vector3 worldPos = HexMetrics.HexToWorld(kvp.Key);

                // 금광 오버레이 오브젝트 생성 (타일 자식)
                var mineObj = new GameObject($"GoldMine_{kvp.Key}");
                mineObj.transform.position = worldPos + new Vector3(0f, 0.05f, 0f);
                mineObj.transform.SetParent(transform);

                var sr = mineObj.AddComponent<SpriteRenderer>();
                sr.sprite = _goldMineSprite;
                sr.sortingLayerName = "Background";

                // sortingOrder: 타일보다 높게, 유닛(100)보다 낮게
                if (HexMetrics.Orientation == HexOrientation.FlatTop)
                    sr.sortingOrder = Mathf.RoundToInt(-worldPos.y * 3) + 1;
                else
                    sr.sortingOrder = kvp.Key.R + 1;

                _goldMineObjects.Add(mineObj);
            }
        }

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
