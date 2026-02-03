// ============================================================================
// HexDirection.cs
// 육각형 타일의 6가지 이동 방향을 정의하고,
// 각 방향으로 이동했을 때의 큐브 좌표 변화량(오프셋)을 제공.
//
// 육각형 방향 배치 (Flat-top 기준):
//
//       NW    NE
//        \   /
//    W — (중심) — E
//        /   \
//       SW    SE
//
// 주요 용도:
//   - 인접 타일 탐색: grid.GetNeighbors()에서 6방향 순회
//   - 경로탐색: HexPathfinder에서 이웃 노드 탐색
//   - 방향 전환: FacingDirection에서 이동방향 → 스프라이트 매핑
//
// 사용 예시:
//   HexCoord neighbor = HexDirection.E.Neighbor(origin);  // origin 동쪽 타일
//   HexDirection opposite = HexDirection.NE.Opposite();   // SW 반환
// ============================================================================

namespace Hexiege.Domain
{
    /// <summary>
    /// 육각형 6방향. 시계 방향 순서 (NE → E → SE → SW → W → NW).
    /// int 값 0~5로 배열 인덱스로 직접 사용 가능.
    /// </summary>
    public enum HexDirection
    {
        NE = 0,   // 북동 (오른쪽 위)
        E = 1,    // 동 (오른쪽)
        SE = 2,   // 남동 (오른쪽 아래)
        SW = 3,   // 남서 (왼쪽 아래)
        W = 4,    // 서 (왼쪽)
        NW = 5    // 북서 (왼쪽 위)
    }

    /// <summary>
    /// HexDirection에 대한 확장 메서드 모음.
    /// 방향별 좌표 오프셋, 이웃 좌표 계산, 반대 방향 계산 등 제공.
    /// </summary>
    public static class HexDirectionExtensions
    {
        // 각 방향으로 이동 시 큐브 좌표(Q, R)의 변화량.
        // 인덱스가 HexDirection enum 값과 일치.
        //
        //  방향  |  Q변화  |  R변화
        // ------+---------+--------
        //  NE   |   +1    |   -1
        //  E    |   +1    |    0
        //  SE   |    0    |   +1
        //  SW   |   -1    |   +1
        //  W    |   -1    |    0
        //  NW   |    0    |   -1
        private static readonly HexCoord[] Offsets =
        {
            new HexCoord(+1, -1), // NE: Q+1, R-1
            new HexCoord(+1,  0), // E:  Q+1, R 그대로
            new HexCoord( 0, +1), // SE: Q 그대로, R+1
            new HexCoord(-1, +1), // SW: Q-1, R+1
            new HexCoord(-1,  0), // W:  Q-1, R 그대로
            new HexCoord( 0, -1), // NW: Q 그대로, R-1
        };

        /// <summary> 전체 방향 수 (6). for문 상한으로 사용. </summary>
        public const int Count = 6;

        /// <summary>
        /// 이 방향의 큐브 좌표 오프셋을 반환.
        /// 예: HexDirection.E.Offset() → HexCoord(+1, 0)
        /// </summary>
        public static HexCoord Offset(this HexDirection dir)
        {
            return Offsets[(int)dir];
        }

        /// <summary>
        /// origin 타일에서 이 방향으로 한 칸 이동한 이웃 좌표를 반환.
        /// 예: HexDirection.NE.Neighbor(origin) → origin + (1, -1)
        /// </summary>
        public static HexCoord Neighbor(this HexDirection dir, HexCoord origin)
        {
            return origin + Offsets[(int)dir];
        }

        /// <summary>
        /// 반대 방향을 반환. (NE↔SW, E↔W, SE↔NW)
        /// 내부적으로 인덱스를 +3 하면 정반대 방향.
        /// </summary>
        public static HexDirection Opposite(this HexDirection dir)
        {
            return (HexDirection)(((int)dir + 3) % Count);
        }
    }
}
