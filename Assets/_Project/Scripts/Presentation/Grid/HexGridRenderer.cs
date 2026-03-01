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
// [Phase 2] 3D 전환 완료:
//   - SpriteRenderer 참조 및 sortingOrder 로직 완전 제거됨
//   - 3D MeshRenderer 기반 타일 프리팹 사용 (프리팹 교체는 에디터 작업)
//   - XZ 평면 배치 (Phase 1에서 HexMetrics가 XZ 반환)
//   - 금광: 3D 프리팹으로 교체 (_goldMinePrefab에 3D 메시 설정)
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
        /// [Phase 2] 3D 전환 완료: sortingOrder 제거됨, XZ 평면 배치.
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
        /// 금광이 있는 타일 위에 금광 오브젝트를 생성.
        /// GameBootstrapper에서 PlaceGoldMines() 후 호출.
        ///
        /// [Phase 2] 3D 전환 완료: 3D 금광 프리팹 사용.
        /// _goldMinePrefab이 null이면 금광 비주얼 생략 (프리팹 미설정 상태).
        /// </summary>
        public void RenderGoldMines(HexGrid grid)
        {
            if (_goldMinePrefab == null || grid == null) return;

            foreach (var kvp in grid.Tiles)
            {
                if (!kvp.Value.HasGoldMine) continue;

                Vector3 worldPos = HexMetrics.HexToWorld(kvp.Key);

                // 도메인 좌표 → 뷰 좌표 변환 (Red팀이면 맵 중심 기준 반전)
                Vector3 viewPos = ViewConverter.ToView(worldPos);

                // 금광 프리팹 인스턴스 생성 (약간 위에 배치하여 타일과 겹침 방지)
                GameObject mineObj = Instantiate(
                    _goldMinePrefab,
                    viewPos + new Vector3(0f, 0.05f, 0f),
                    Quaternion.identity,
                    transform
                );
                mineObj.name = $"GoldMine_{kvp.Key}";

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
