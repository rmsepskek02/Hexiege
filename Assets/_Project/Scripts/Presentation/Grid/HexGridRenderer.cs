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

        /// <summary> 생성된 타일 View 딕셔너리 (읽기 전용). </summary>
        public IReadOnlyDictionary<HexCoord, HexTileView> TileViews => _tileViews;

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
