// ============================================================================
// HexGrid.cs
// 11×17 육각형 그리드 전체를 관리하는 데이터 구조.
//
// 내부적으로 Dictionary<HexCoord, HexTile>로 187개 타일을 저장.
// 그리드 생성 시 even-r offset 좌표를 cube 좌표로 변환하여 저장.
//
// even-r offset 좌표란?
//   일반적인 2D 배열처럼 (col, row)로 표현하되,
//   홀수 행(row)의 타일들이 반 칸 오른쪽으로 밀리는 방식.
//
//   row 0: [0] [1] [2] [3] [4]  ← 짝수 행: 정렬
//   row 1:  [0] [1] [2] [3] [4] ← 홀수 행: 반 칸 오른쪽
//   row 2: [0] [1] [2] [3] [4]  ← 짝수 행: 정렬
//
//   이 offset 좌표를 OffsetToCube()로 큐브 좌표(Q, R)로 변환하여 저장.
//   큐브 좌표를 사용하면 거리 계산, 이웃 탐색 등이 단순해짐.
//
// 주요 메서드:
//   GetTile(coord)               → 특정 좌표의 타일 반환 (없으면 null)
//   HasTile(coord)               → 해당 좌표에 타일이 있는지 확인
//   SetOwner(coord, team)        → 타일 점령 (소유자 변경)
//   GetNeighbors(coord)          → 인접한 6방향 타일 목록
//   GetWalkableNeighborCoords()  → 이동 가능한 인접 타일 좌표 (A* 경로탐색용)
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

using System.Collections.Generic;

namespace Hexiege.Domain
{
    public class HexGrid
    {
        /// <summary> 그리드 가로 타일 수 (프로토타입: 11) </summary>
        public int Width { get; }

        /// <summary> 그리드 세로 타일 수 (프로토타입: 17) </summary>
        public int Height { get; }

        /// <summary> 이 그리드의 헥스 방향 (PointyTop 또는 FlatTop). </summary>
        private readonly HexOrientation _orientation;

        // 모든 타일을 큐브 좌표로 인덱싱하여 저장.
        // Dictionary를 쓰는 이유: 큐브 좌표는 음수 값이 있어 2D 배열로 매핑이 불편.
        private readonly Dictionary<HexCoord, HexTile> _tiles = new Dictionary<HexCoord, HexTile>();

        // ====================================================================
        // 팀별 소유 타일 카운터 (성능 최적화)
        // ====================================================================
        // CountTilesOwnedBy(team) 호출 시 187타일 전체를 순회하지 않도록 캐시.
        // SetOwner 호출 시 이전 팀 -1, 새 팀 +1 갱신.
        // HexTile.Owner를 외부에서 직접 set하는 경로가 있다면 무결성이 깨질 수 있어,
        // 본 카운터는 "반드시 SetOwner를 통해 변경"한다는 규약을 전제로 한다.
        private readonly Dictionary<TeamId, int> _ownedTileCounts = new Dictionary<TeamId, int>();

        /// <summary> 읽기 전용 타일 딕셔너리. 외부에서 순회용으로 사용. </summary>
        public IReadOnlyDictionary<HexCoord, HexTile> Tiles => _tiles;

        /// <summary>
        /// 생성자. width×height 크기의 그리드를 즉시 생성.
        /// 기존 호환용: PointyTop (even-r offset) 기본값.
        /// </summary>
        public HexGrid(int width, int height) : this(width, height, HexOrientation.PointyTop) { }

        /// <summary>
        /// orientation 지정 생성자.
        /// PointyTop이면 even-r offset, FlatTop이면 even-q offset으로 생성.
        /// </summary>
        public HexGrid(int width, int height, HexOrientation orientation)
        {
            Width = width;
            Height = height;
            _orientation = orientation;
            Generate();
        }

        /// <summary>
        /// 그리드 생성. offset 좌표 (col, row)를 순회하며
        /// 각각을 큐브 좌표로 변환하여 HexTile 생성.
        /// PointyTop이면 even-r, FlatTop이면 even-q offset 사용.
        ///
        /// 생성 직후 팀별 카운터를 Neutral=총타일수로 초기화한다.
        /// 모든 타일이 기본값으로 Neutral 상태이므로.
        /// </summary>
        private void Generate()
        {
            for (int r = 0; r < Height; r++)
            {
                for (int col = 0; col < Width; col++)
                {
                    HexCoord coord = OffsetToCube(col, r, _orientation);
                    _tiles[coord] = new HexTile(coord);
                }
            }

            // 카운터 초기화 — Neutral만 _tiles.Count, 다른 팀은 0
            _ownedTileCounts[TeamId.Neutral] = _tiles.Count;
        }

        /// <summary>
        /// even-r offset (col, row) → cube 좌표 (q, r) 변환.
        ///
        /// 변환 공식:
        ///   q = col - (row - (row & 1)) / 2
        ///   r = row
        ///
        /// (row & 1)은 row가 홀수이면 1, 짝수이면 0.
        /// 이를 통해 홀수 행의 반 칸 시프트를 큐브 좌표로 보정.
        ///
        /// 예시 (row=3, col=2):
        ///   q = 2 - (3 - 1) / 2 = 2 - 1 = 1
        ///   r = 3
        ///   → HexCoord(1, 3)
        /// </summary>
        public static HexCoord OffsetToCube(int col, int row)
        {
            int q = col - (row - (row & 1)) / 2;
            int r = row;
            return new HexCoord(q, r);
        }

        /// <summary>
        /// orientation 지정 offset → cube 변환.
        /// PointyTop: even-r offset (기존과 동일).
        /// FlatTop: even-q offset.
        ///
        /// even-q 변환 공식:
        ///   q = col
        ///   r = row - (col - (col & 1)) / 2
        ///
        /// (col & 1)은 col이 홀수이면 1, 짝수이면 0.
        /// 홀수 열의 반 칸 시프트를 큐브 좌표로 보정.
        /// </summary>
        public static HexCoord OffsetToCube(int col, int row, HexOrientation orientation)
        {
            if (orientation == HexOrientation.FlatTop)
            {
                int q = col;
                int r = row - (col - (col & 1)) / 2;
                return new HexCoord(q, r);
            }
            return OffsetToCube(col, row);
        }

        /// <summary>
        /// 특정 좌표의 타일을 반환. 그리드 밖이면 null.
        /// </summary>
        public HexTile GetTile(HexCoord coord)
        {
            _tiles.TryGetValue(coord, out HexTile tile);
            return tile;
        }

        /// <summary>
        /// 해당 좌표에 타일이 존재하는지 확인. 그리드 경계 판단에 사용.
        /// </summary>
        public bool HasTile(HexCoord coord)
        {
            return _tiles.ContainsKey(coord);
        }

        /// <summary>
        /// 타일의 소유자를 변경 (점령). 존재하지 않는 좌표는 무시.
        /// UnitMovementUseCase에서 유닛 이동 시 호출.
        ///
        /// 팀별 소유 타일 카운터(_ownedTileCounts)를 함께 갱신.
        /// 같은 소유자로 재설정하면 카운터는 변하지 않는다.
        /// </summary>
        public void SetOwner(HexCoord coord, TeamId owner)
        {
            if (_tiles.TryGetValue(coord, out HexTile tile))
            {
                TeamId prev = tile.Owner;
                if (prev == owner) return; // 변화 없음 — 카운터 갱신 불필요

                // 이전 팀 -1
                if (_ownedTileCounts.TryGetValue(prev, out int prevCount))
                    _ownedTileCounts[prev] = prevCount - 1;
                // 새 팀 +1
                if (_ownedTileCounts.TryGetValue(owner, out int newCount))
                    _ownedTileCounts[owner] = newCount + 1;
                else
                    _ownedTileCounts[owner] = 1;

                tile.Owner = owner;
            }
        }

        /// <summary>
        /// 타일의 현재 소유 팀을 반환한다.
        /// 그리드 밖이거나 등록되지 않은 좌표라면 TeamId.Neutral을 돌려준다.
        ///
        /// TileOwnershipService에서 매 프레임 "현재 소유자가 새 점령 팀과 다른가?"를
        /// 판단할 때 사용한다. 다를 때만 SetOwner와 OnTileOwnerChanged 이벤트를 호출하여
        /// 같은 팀이 계속 차지하고 있는 경우의 불필요한 이벤트 발행을 막는다.
        /// </summary>
        /// <param name="coord">조회할 타일의 큐브 좌표.</param>
        /// <returns>해당 타일의 소유 팀. 존재하지 않으면 TeamId.Neutral.</returns>
        public TeamId GetOwner(HexCoord coord)
        {
            if (_tiles.TryGetValue(coord, out HexTile tile))
            {
                return tile.Owner;
            }
            return TeamId.Neutral;
        }

        /// <summary>
        /// 인접한 6방향의 타일 목록을 반환 (그리드 밖은 제외).
        /// 타일 정보가 필요한 경우 사용 (소유자 확인 등).
        /// </summary>
        public List<HexTile> GetNeighbors(HexCoord coord)
        {
            var neighbors = new List<HexTile>(HexDirectionExtensions.Count);
            for (int i = 0; i < HexDirectionExtensions.Count; i++)
            {
                HexDirection dir = (HexDirection)i;
                HexCoord neighborCoord = dir.Neighbor(coord);
                if (_tiles.TryGetValue(neighborCoord, out HexTile tile))
                {
                    neighbors.Add(tile);
                }
            }
            return neighbors;
        }

        /// <summary>
        /// 특정 팀이 소유한 타일 수를 반환.
        /// 인구수 계산에 사용: 총 인구 = 보유 타일 수.
        ///
        /// _ownedTileCounts Dictionary 캐시로 O(187) → O(1) 단축.
        /// SetOwner 호출 시점에 증감 갱신되므로 매번 전체 순회할 필요가 없다.
        /// </summary>
        public int CountTilesOwnedBy(TeamId team)
        {
            return _ownedTileCounts.TryGetValue(team, out int count) ? count : 0;
        }

        /// <summary>
        /// 인접한 6방향 중 이동 가능(IsWalkable)한 타일의 좌표만 반환.
        /// HexPathfinder의 A* 알고리즘에서 이웃 노드 탐색에 사용.
        /// </summary>
        public List<HexCoord> GetWalkableNeighborCoords(HexCoord coord)
        {
            var result = new List<HexCoord>(HexDirectionExtensions.Count);
            for (int i = 0; i < HexDirectionExtensions.Count; i++)
            {
                HexDirection dir = (HexDirection)i;
                HexCoord neighborCoord = dir.Neighbor(coord);
                if (_tiles.TryGetValue(neighborCoord, out HexTile tile) && tile.IsWalkable)
                {
                    result.Add(neighborCoord);
                }
            }
            return result;
        }
    }
}
