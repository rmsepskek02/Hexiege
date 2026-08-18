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
// Z축 방향 (XZ 평면):
//   row가 증가할수록 z가 감소 (맵 아래쪽).
//   3D 탑다운 뷰에서 +Z 방향이 row=0.
//   Y축은 높이(수직)로 사용, 타일은 Y=0 평면에 배치.
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
        /// XZ 평면에서 Y축은 높이이므로, 유닛이 타일 "위에 떠 있는" 효과.
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
        /// 2. offset 좌표 → 월드 위치 계산 (XZ 평면)
        ///    x = col * TileWidth + (홀수행이면 TileWidth * 0.5)
        ///    z = -row * TileHeight * 0.75 (아래로 갈수록 z 감소)
        ///
        /// 0.75를 곱하는 이유:
        ///   pointy-top 육각형은 위아래가 겹치므로 세로 간격이 타일 높이의 3/4.
        /// </summary>
        private static Vector3 HexToWorldPointyTop(HexCoord coord)
        {
            // 큐브 좌표 → even-r offset 좌표로 역변환
            int col = coord.Q + (coord.R - (coord.R & 1)) / 2;
            int row = coord.R;

            // offset 좌표 → 월드 위치 (XZ 평면, Y=0)
            float x = col * TileWidth;
            if (row % 2 != 0)
                x += TileWidth * 0.5f;  // 홀수 행: 반 칸 오른쪽 시프트

            float z = -row * TileHeight * 0.75f; // row↑ → z↓ (탑다운 3D)

            return new Vector3(x, 0f, z);
        }

        /// <summary>
        /// FlatTop (even-q offset) 변환.
        ///
        /// 변환 과정:
        /// 1. 큐브 좌표 → even-q offset 좌표(col, row)로 역변환
        ///    col = Q
        ///    row = R + (Q - (Q & 1)) / 2
        ///
        /// 2. offset 좌표 → 월드 위치 계산 (XZ 평면)
        ///    x = col * TileWidth * 0.75 (flat-top은 가로에 겹침)
        ///    z = -row * TileHeight + (홀수열이면 -TileHeight * 0.5)
        ///
        /// 0.75를 곱하는 이유:
        ///   flat-top 육각형은 좌우가 겹치므로 가로 간격이 타일 폭의 3/4.
        /// </summary>
        private static Vector3 HexToWorldFlatTop(HexCoord coord)
        {
            // 큐브 좌표 → even-q offset 좌표로 역변환
            int col = coord.Q;
            int row = coord.R + (coord.Q - (coord.Q & 1)) / 2;

            // offset 좌표 → 월드 위치 (XZ 평면, Y=0)
            float x = col * TileWidth * 0.75f;    // 가로 3/4 겹침
            float z = -row * TileHeight;           // 세로 겹침 없음
            if (col % 2 != 0)
                z -= TileHeight * 0.5f;            // 홀수 열: 반 칸 아래 시프트

            return new Vector3(x, 0f, z);
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
        /// XZ 평면 기반: Z좌표로 row 역산.
        /// </summary>
        private static HexCoord WorldToHexPointyTop(Vector3 worldPos)
        {
            // 대략적인 row 추정 (z로부터 역산)
            int approxRow = Mathf.RoundToInt(-worldPos.z / (TileHeight * 0.75f));

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
        /// XZ 평면 기반: Z좌표로 row 역산.
        /// PointyTop과 동일한 9-neighbor 탐색 알고리즘, 다른 역산 공식.
        /// </summary>
        private static HexCoord WorldToHexFlatTop(Vector3 worldPos)
        {
            // 대략적인 col 추정 (x로부터 역산)
            int approxCol = Mathf.RoundToInt(worldPos.x / (TileWidth * 0.75f));

            // 해당 col의 z 오프셋 보정 후 row 추정
            float zOffset = (approxCol % 2 != 0) ? TileHeight * 0.5f : 0f;
            int approxRow = Mathf.RoundToInt((-worldPos.z - zOffset) / TileHeight);

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
        /// XZ 평면에서 Y축이 높이이므로, Y에 오프셋 추가.
        /// UnitFactory, UnitView 등 유닛 위치 설정 시 사용.
        /// </summary>
        public static Vector3 HexToWorldUnit(HexCoord coord)
        {
            Vector3 pos = HexToWorld(coord);
            pos.y += UnitYOffset; // Y축 = 높이 (XZ 평면에서 위로 올림)
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

        // ====================================================================
        // 맵 월드 경계(도메인 좌표) — 스킬 조준 연속 좌표 clamp/판정용
        // ====================================================================
        //
        // 왜 필요한가(초급자용 설명):
        //   스킬 지점 조준이 "타일 스냅"에서 "연속 좌표"로 바뀌면서, 조준 중심이 더 이상
        //   정수 타일(HexCoord)이 아니라 임의의 연속 월드 좌표가 된다. 그래서 "이 연속 점이
        //   맵 안인가?"를 타일 존재(HasTile)로는 판정할 수 없다. 대신 "맵 전체를 감싸는
        //   월드 사각 경계(최외곽 타일 바깥선 기준)" 안인지로 판정·clamp한다.
        //
        // 경계 정의(엄밀 — 결정 3):
        //   최외곽 타일들의 중심 월드 좌표 극값(min/max)을 구한 뒤, 타일 반칸(가로 0.5×TileWidth,
        //   세로 0.5×TileHeight)만큼 바깥으로 확장한 축 정렬 사각형(AABB)을 맵 경계로 삼는다.
        //   → "최근접 타일 근사(반칸 여유)"가 아니라 실제 맵 가장자리선에 해당하는 연속 경계다.
        //
        // 좌표계 주의:
        //   여기서 다루는 좌표는 모두 "도메인 월드"(HexToWorld 결과, Blue 기준)다. Red 팀의 뷰
        //   반전(ViewConverter)은 표시 단계에서만 적용되므로, 이 경계는 뷰 반전과 무관하게 동일하다.

        /// <summary>
        /// 맵 전체를 감싸는 도메인 월드 경계(AABB)를 계산한다. Y는 0으로 둔다(XZ 평면).
        /// 최외곽 타일 중심의 극값을 구한 뒤 타일 반칸만큼 바깥으로 확장한다.
        /// </summary>
        /// <param name="gridWidth">그리드 가로 타일 수.</param>
        /// <param name="gridHeight">그리드 세로 타일 수.</param>
        /// <param name="min">경계 사각형의 최소 모서리(작은 x·z).</param>
        /// <param name="max">경계 사각형의 최대 모서리(큰 x·z).</param>
        public static void ComputeMapWorldBounds(int gridWidth, int gridHeight, out Vector3 min, out Vector3 max)
        {
            // 방어: 비정상 그리드면 원점 한 점으로 축약(clamp가 항상 원점을 돌려주도록).
            if (gridWidth <= 0 || gridHeight <= 0)
            {
                min = Vector3.zero;
                max = Vector3.zero;
                return;
            }

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            // 테두리 셀만 순회해도 극값을 모두 얻는다(내부 셀은 극값에 영향 없음 — 순회 절감).
            //   홀수 행/열 반칸 시프트 때문에 극값이 코너가 아닌 곳에 생길 수 있어, 테두리 전체를 훑는다.
            for (int r = 0; r < gridHeight; r++)
            {
                for (int c = 0; c < gridWidth; c++)
                {
                    bool border = (r == 0 || r == gridHeight - 1 || c == 0 || c == gridWidth - 1);
                    if (!border) continue;

                    Vector3 w = HexToWorld(HexGrid.OffsetToCube(c, r, Orientation));
                    if (w.x < minX) minX = w.x;
                    if (w.x > maxX) maxX = w.x;
                    if (w.z < minZ) minZ = w.z;
                    if (w.z > maxZ) maxZ = w.z;
                }
            }

            // 타일 중심 극값에서 반칸만큼 바깥으로 확장 = 최외곽 타일 바깥선.
            float padX = 0.5f * TileWidth;
            float padZ = 0.5f * TileHeight;
            min = new Vector3(minX - padX, 0f, minZ - padZ);
            max = new Vector3(maxX + padX, 0f, maxZ + padZ);
        }

        /// <summary>
        /// 도메인 월드 점이 맵 경계(사각 AABB) 안에 있는지 판정한다. 서버 재검증(규칙 26)용.
        /// </summary>
        /// <param name="domainPoint">판정할 도메인 월드 좌표(XZ, Y 무시).</param>
        /// <param name="gridWidth">그리드 가로 타일 수.</param>
        /// <param name="gridHeight">그리드 세로 타일 수.</param>
        /// <returns>맵 경계 안이면 true.</returns>
        public static bool IsWithinMapBounds(Vector3 domainPoint, int gridWidth, int gridHeight)
        {
            ComputeMapWorldBounds(gridWidth, gridHeight, out Vector3 min, out Vector3 max);
            return domainPoint.x >= min.x && domainPoint.x <= max.x
                && domainPoint.z >= min.z && domainPoint.z <= max.z;
        }

        /// <summary>
        /// 도메인 월드 점을 맵 경계(사각 AABB) 안으로 clamp한다. 조준 원이 맵 밖으로 못 나가게 한다(규칙 22).
        /// Y는 0으로 정규화한다(XZ 평면).
        /// </summary>
        /// <param name="domainPoint">clamp할 도메인 월드 좌표.</param>
        /// <param name="gridWidth">그리드 가로 타일 수.</param>
        /// <param name="gridHeight">그리드 세로 타일 수.</param>
        /// <returns>맵 경계 안으로 clamp된 도메인 월드 좌표(Y=0).</returns>
        public static Vector3 ClampToMapBounds(Vector3 domainPoint, int gridWidth, int gridHeight)
        {
            ComputeMapWorldBounds(gridWidth, gridHeight, out Vector3 min, out Vector3 max);
            float x = Mathf.Clamp(domainPoint.x, min.x, max.x);
            float z = Mathf.Clamp(domainPoint.z, min.z, max.z);
            return new Vector3(x, 0f, z);
        }
    }
}
