// ============================================================================
// HexMetrics.cs
// 헥스 좌표(HexCoord) ↔ Unity 월드 좌표(Vector3) 변환 유틸리티.
//
// 역할:
//   Domain 레이어의 논리 좌표(HexCoord)와
//   Presentation 레이어의 화면 좌표(Vector3)를 연결하는 다리.
//
// Orientation별 좌표 배치 방식:
//
// PointyTop (even-r offset):
//   짝수 행(row 0, 2, 4...): 타일이 왼쪽 정렬
//   홀수 행(row 1, 3, 5...): 타일이 반 칸 오른쪽으로 밀림
//   세로 간격에 0.75 겹침 적용
//
//   row 0: [0]  [1]  [2]  [3]    ← 짝수 행
//   row 1:   [0]  [1]  [2]  [3]  ← 홀수 행 (반 칸 오프셋)
//   row 2: [0]  [1]  [2]  [3]    ← 짝수 행
//
// FlatTop (even-q offset):
//   짝수 열(col 0, 2, 4...): 타일이 위쪽 정렬
//   홀수 열(col 1, 3, 5...): 타일이 반 칸 아래로 밀림
//   가로 간격에 0.75 겹침 적용
//
//   col: 0   1   2   3
//       [0] [0] [0] [0]  ← row 0
//       [1]  [1] [1]  [1]
//       [2] [2] [2] [2]
//
// Y축 방향:
//   row가 증가할수록 y가 감소 (화면 아래쪽).
//   2D 탑다운 뷰에서 위쪽이 row=0.
//
// Core 레이어 — Unity 의존 (Vector3, Mathf 사용).
// ============================================================================

using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Core
{
    public static class HexMetrics
    {
        /// <summary>
        /// 타일 간 가로 간격 (Unity 월드 단위).
        /// 스프라이트 PPU와 해상도에 맞춰 조정.
        /// </summary>
        public static float TileWidth = 1.0f;

        /// <summary>
        /// 타일 간 세로 간격 (Unity 월드 단위).
        /// 육각형이므로 한 축의 간격은 타일 크기의 약 0.75배로 배치.
        /// </summary>
        public static float TileHeight = 1.0f;

        /// <summary>
        /// 유닛을 타일 중심보다 위로 올리는 Y 오프셋.
        /// 유닛이 타일 "위에 서 있는" 시각적 효과를 위한 값.
        /// GameBootstrapper에서 GameConfig.UnitYOffset 값을 복사.
        /// </summary>
        public static float UnitYOffset = 0.15f;

        /// <summary>
        /// 현재 활성화된 헥스 방향.
        /// GameBootstrapper에서 GameConfig.Orientation 값을 복사.
        /// PointyTop이면 even-r offset, FlatTop이면 even-q offset 사용.
        /// </summary>
        public static HexOrientation Orientation = HexOrientation.PointyTop;

        // ====================================================================
        // HexToWorld: 헥스 좌표 → 월드 좌표
        // ====================================================================

        /// <summary>
        /// 헥스 좌표(HexCoord) → Unity 월드 좌표(Vector3) 변환.
        /// Orientation에 따라 PointyTop 또는 FlatTop 공식 사용.
        /// </summary>
        public static Vector3 HexToWorld(HexCoord coord)
        {
            if (Orientation == HexOrientation.FlatTop)
                return HexToWorldFlatTop(coord);
            return HexToWorldPointyTop(coord);
        }

        /// <summary>
        /// PointyTop (even-r offset) 변환.
        ///
        /// 변환 과정:
        /// 1. 큐브 좌표 → even-r offset 좌표(col, row)로 역변환
        ///    col = Q + (R - (R & 1)) / 2
        ///    row = R
        ///
        /// 2. offset 좌표 → 월드 위치 계산
        ///    x = col * TileWidth + (홀수행이면 TileWidth * 0.5)
        ///    y = -row * TileHeight * 0.75 (아래로 갈수록 y 감소)
        ///
        /// 0.75를 곱하는 이유:
        ///   pointy-top 육각형은 위아래가 겹치므로 세로 간격이 타일 높이의 3/4.
        /// </summary>
        private static Vector3 HexToWorldPointyTop(HexCoord coord)
        {
            // 큐브 좌표 → even-r offset 좌표로 역변환
            int col = coord.Q + (coord.R - (coord.R & 1)) / 2;
            int row = coord.R;

            // offset 좌표 → 월드 위치
            float x = col * TileWidth;
            if (row % 2 != 0)
                x += TileWidth * 0.5f;  // 홀수 행: 반 칸 오른쪽 시프트

            float y = -row * TileHeight * 0.75f; // row↑ → y↓ (탑다운 2D)

            return new Vector3(x, y, 0f);
        }

        /// <summary>
        /// FlatTop (even-q offset) 변환.
        ///
        /// 변환 과정:
        /// 1. 큐브 좌표 → even-q offset 좌표(col, row)로 역변환
        ///    col = Q
        ///    row = R + (Q - (Q & 1)) / 2
        ///
        /// 2. offset 좌표 → 월드 위치 계산
        ///    x = col * TileWidth * 0.75 (flat-top은 가로에 겹침)
        ///    y = -row * TileHeight + (홀수열이면 -TileHeight * 0.5)
        ///
        /// 0.75를 곱하는 이유:
        ///   flat-top 육각형은 좌우가 겹치므로 가로 간격이 타일 폭의 3/4.
        /// </summary>
        private static Vector3 HexToWorldFlatTop(HexCoord coord)
        {
            // 큐브 좌표 → even-q offset 좌표로 역변환
            int col = coord.Q;
            int row = coord.R + (coord.Q - (coord.Q & 1)) / 2;

            // offset 좌표 → 월드 위치
            float x = col * TileWidth * 0.75f;    // 가로 3/4 겹침
            float y = -row * TileHeight;           // 세로 겹침 없음
            if (col % 2 != 0)
                y -= TileHeight * 0.5f;            // 홀수 열: 반 칸 아래 시프트

            return new Vector3(x, y, 0f);
        }

        // ====================================================================
        // WorldToHex: 월드 좌표 → 헥스 좌표
        // ====================================================================

        /// <summary>
        /// Unity 월드 좌표(Vector3) → 가장 가까운 헥스 좌표(HexCoord) 변환.
        /// 마우스 클릭 → 어떤 타일을 클릭했는지 판정할 때 사용.
        /// Orientation에 따라 PointyTop 또는 FlatTop 공식 사용.
        ///
        /// 알고리즘:
        /// 1. 월드 좌표에서 대략적인 (col, row) 역산
        /// 2. 주변 3×3 = 9개 후보 타일 중 월드 거리가 가장 가까운 타일 선택
        ///
        /// 정확한 수학적 역변환 대신 브루트포스 후보 탐색을 하는 이유:
        ///   - 육각형 경계의 정확한 역변환 공식이 복잡
        ///   - 9개 후보 비교는 매우 가벼움 (프레임당 1회 호출)
        ///   - 코드가 단순하고 버그 위험 적음
        /// </summary>
        public static HexCoord WorldToHex(Vector3 worldPos)
        {
            if (Orientation == HexOrientation.FlatTop)
                return WorldToHexFlatTop(worldPos);
            return WorldToHexPointyTop(worldPos);
        }

        /// <summary>
        /// PointyTop (even-r offset) 역변환.
        /// </summary>
        private static HexCoord WorldToHexPointyTop(Vector3 worldPos)
        {
            // 대략적인 row 추정 (y로부터 역산)
            int approxRow = Mathf.RoundToInt(-worldPos.y / (TileHeight * 0.75f));

            // 해당 row의 x 오프셋 보정 후 col 추정
            float xOffset = (approxRow % 2 != 0) ? TileWidth * 0.5f : 0f;
            int approxCol = Mathf.RoundToInt((worldPos.x - xOffset) / TileWidth);

            // 추정 좌표를 기준으로 주변 9개 후보 중 가장 가까운 타일 선택
            HexCoord best = HexGrid.OffsetToCube(approxCol, approxRow);
            float bestDist = Vector3.Distance(worldPos, HexToWorld(best));

            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    int r = approxRow + dr;
                    int c = approxCol + dc;
                    HexCoord candidate = HexGrid.OffsetToCube(c, r);
                    float dist = Vector3.Distance(worldPos, HexToWorld(candidate));
                    if (dist < bestDist)
                    {
                        best = candidate;
                        bestDist = dist;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// FlatTop (even-q offset) 역변환.
        /// PointyTop과 동일한 9-neighbor 탐색 알고리즘, 다른 역산 공식.
        /// </summary>
        private static HexCoord WorldToHexFlatTop(Vector3 worldPos)
        {
            // 대략적인 col 추정 (x로부터 역산)
            int approxCol = Mathf.RoundToInt(worldPos.x / (TileWidth * 0.75f));

            // 해당 col의 y 오프셋 보정 후 row 추정
            float yOffset = (approxCol % 2 != 0) ? TileHeight * 0.5f : 0f;
            int approxRow = Mathf.RoundToInt((-worldPos.y - yOffset) / TileHeight);

            // 추정 좌표를 기준으로 주변 9개 후보 중 가장 가까운 타일 선택
            HexCoord best = HexGrid.OffsetToCube(approxCol, approxRow, HexOrientation.FlatTop);
            float bestDist = Vector3.Distance(worldPos, HexToWorld(best));

            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    int r = approxRow + dr;
                    int c = approxCol + dc;
                    HexCoord candidate = HexGrid.OffsetToCube(c, r, HexOrientation.FlatTop);
                    float dist = Vector3.Distance(worldPos, HexToWorld(candidate));
                    if (dist < bestDist)
                    {
                        best = candidate;
                        bestDist = dist;
                    }
                }
            }

            return best;
        }

        // ====================================================================
        // 유틸리티
        // ====================================================================

        /// <summary>
        /// 헥스 좌표 → 유닛이 서 있을 월드 좌표 변환.
        /// HexToWorld()에 UnitYOffset을 더해 타일 위에 서 있는 위치를 반환.
        /// UnitFactory, UnitView 등 유닛 위치 설정 시 사용.
        /// </summary>
        public static Vector3 HexToWorldUnit(HexCoord coord)
        {
            Vector3 pos = HexToWorld(coord);
            pos.y += UnitYOffset;
            return pos;
        }

        /// <summary>
        /// 그리드 전체의 월드 공간 중심점 계산.
        /// 카메라 초기 위치 설정에 사용.
        /// 좌상단(0,0)과 우하단(width-1, height-1) 타일의 중간점.
        /// </summary>
        public static Vector3 GridCenter(int gridWidth, int gridHeight)
        {
            HexCoord topLeft = HexGrid.OffsetToCube(0, 0, Orientation);
            HexCoord bottomRight = HexGrid.OffsetToCube(gridWidth - 1, gridHeight - 1, Orientation);
            Vector3 min = HexToWorld(topLeft);
            Vector3 max = HexToWorld(bottomRight);
            return (min + max) * 0.5f;
        }
    }
}
