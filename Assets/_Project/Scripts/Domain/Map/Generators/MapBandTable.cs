// ============================================================================
// MapBandTable.cs
// 협곡형 · 외곽형 · 3갈래형 세 생성기가 함께 쓰는 「높이 단계 · 대역」 표와,
// 그 표를 그대로 지형에 옮겨 적는 공통 루프.
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔴 왜 「행」이 아니라 「높이 단계」로 대역을 잡는가 (초급자용 설명)
// ─────────────────────────────────────────────────────────────────────────────
//   이 맵은 FlatTop 육각 격자라서 홀수 열이 반 칸 아래로 내려가 있다.
//   그래서 "같은 행"이라도 짝수 열과 홀수 열의 실제 높이가 반 칸 다르다.
//
//        열:   0     1     2     3     4
//             ┌─┐         ┌─┐         ┌─┐
//     0행 ->  │ │   ┌─┐   │ │   ┌─┐   │ │   <- 짝수 열은 여기서 시작
//             └─┘   │ │   └─┘   │ │   └─┘
//             ┌─┐   └─┘   ┌─┐   └─┘   ┌─┐   <- 홀수 열은 반 칸 아래에서 시작
//     1행 ->  │ │   ┌─┐   │ │   ┌─┐   │ │
//             └─┘   │ │   └─┘   │ │   └─┘
//
//   「높이 단계」는 실제 높이가 같은 타일끼리 묶은 진짜 가로선 번호다.
//   행에 2를 곱하고, 홀수 열이면 1을 더해 구한다. 값의 범위는 0~41 이고
//   그중 21이 중앙선이다.
//
//   높이 단계를 쓰면 좋은 점이 두 가지다.
//
//   ① 180도 회전이 식 하나로 끝난다.
//      높이 단계 L 의 회전 상대는 언제나 42에서 L 을 뺀 값이다.
//      행 번호로 쓰면 짝수 열과 홀수 열의 식이 갈라져 두 줄이 된다.
//
//   ② 대역이 회전에 대해 「닫혀」 있는지를 눈으로 확인할 수 있다.
//      예를 들어 "중앙 3개 행"이라는 뜻으로 9~11행을 잡으면 회전 대칭이 아니다.
//      (그 대역의 한가운데가 맵 한가운데보다 반 칸 위로 어긋난다.)
//      높이 단계 18~24 로 잡으면 42에서 빼는 식에 대해 닫혀 있다.
//      18과 24, 19와 23, 20과 22 가 서로 짝이고 21은 자기 자신이다.
//
//   ⚠️ 한 높이 단계에는 짝수 열 아니면 홀수 열만 존재한다.
//      높이 단계가 짝수면 짝수 열만, 홀수면 홀수 열만 그 단계에 속한다.
//      그래서 "폭 5" 같은 열 구간을 잡아도 그 단계가 실제로 갖는 타일은
//      구간 안의 「같은 홀짝 열」뿐이다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 「폭」은 열 구간으로 읽는다
// ─────────────────────────────────────────────────────────────────────────────
//   폭은 항상 홀수이며 가운데 열(5열)을 중심으로 좌우 대칭인 열 구간을 뜻한다.
//   폭 3 이면 4·5·6열, 폭 11 이면 0~10열 전부다.
//   열 구간은 회전(열 번호를 10에서 뺀 값)에 대해 그대로 보존된다.
//
// 근거: GameSystemRules/GameSystemRules_RandomMap.md 규칙 6 · 규칙 7 · 규칙 8
// Domain 레이어 — 순수 C#, Unity/Core 의존 없음.
// ============================================================================

using System;
using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 좌표 하나에 놓일 지형 종류를 돌려주는 함수. 생성기가 "이 칸은 무엇인가"를
    /// 한 곳에 적어 두고, 공통 루프가 그것을 대칭으로 옮겨 적는 데 쓴다.
    /// </summary>
    /// <param name="col">열</param>
    /// <param name="row">행</param>
    /// <returns>그 칸에 놓을 지형 종류</returns>
    public delegate TileKind MapTileKindSelector(int col, int row);

    /// <summary>
    /// 높이 단계 계산과 대역 표를 모아 둔 공용 도우미.
    /// 외곽형(규칙 7)과 3갈래형(규칙 8)이 「분리 길이별 행 범위」를 여기서 함께 읽는다.
    /// </summary>
    public static class MapBandTable
    {
        // ====================================================================
        // 1. 높이 단계
        // ====================================================================

        /// <summary> 격자 가로 타일 수(규격 11). </summary>
        public const int Width = MapDefinition.DefaultWidth;

        /// <summary> 격자 세로 타일 수(규격 21). </summary>
        public const int Height = MapDefinition.DefaultHeight;

        /// <summary> 가운데 열. 모든 「폭」은 이 열을 중심으로 좌우 대칭이다. </summary>
        public const int CenterCol = (Width - 1) / 2;

        /// <summary> 가운데 행. </summary>
        public const int CenterRow = (Height - 1) / 2;

        /// <summary>
        /// 회전 상대를 구할 때 쓰는 높이 단계의 합. 세로 크기의 2배이며,
        /// 높이 단계 L 의 회전 상대는 이 값에서 L 을 뺀 값이다.
        /// </summary>
        public const int HeightStepSum = Height * 2;

        /// <summary> 중앙선의 높이 단계. 세로 크기와 같은 값이 된다(21). </summary>
        public const int CenterHeightStep = Height;

        /// <summary> 존재할 수 있는 가장 작은 높이 단계(0행 짝수 열, 짝 없는 6칸). </summary>
        public const int MinHeightStep = 0;

        /// <summary> 존재할 수 있는 가장 큰 높이 단계(20행 홀수 열). </summary>
        public const int MaxHeightStep = (Height - 1) * 2 + 1;

        /// <summary>
        /// 그 칸의 높이 단계를 구한다. 행에 2를 곱하고 홀수 열이면 1을 더한 값이다.
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <returns>0 이상 41 이하의 높이 단계</returns>
        public static int GetHeightStep(int col, int row)
        {
            return row * 2 + (col & 1);
        }

        /// <summary>
        /// 높이 단계의 180도 회전 상대를 구한다. 42에서 빼는 것이 전부다.
        /// </summary>
        /// <param name="heightStep">원래 높이 단계</param>
        /// <returns>회전 상대의 높이 단계</returns>
        public static int RotateHeightStep(int heightStep)
        {
            return HeightStepSum - heightStep;
        }

        /// <summary>
        /// 높이 단계 구간을 「행 범위」로 바꾼다. 열의 홀짝에 따라 답이 달라지므로
        /// 어느 쪽을 묻는지 반드시 지정해야 한다.
        /// </summary>
        /// <param name="minHeightStep">구간의 시작 높이 단계(포함)</param>
        /// <param name="maxHeightStep">구간의 끝 높이 단계(포함)</param>
        /// <param name="isOddColumn">홀수 열의 행 범위를 묻는 것이면 true</param>
        /// <param name="minRow">구간에 포함되는 가장 작은 행</param>
        /// <param name="maxRow">구간에 포함되는 가장 큰 행</param>
        public static void GetRowRange(int minHeightStep, int maxHeightStep, bool isOddColumn,
            out int minRow, out int maxRow)
        {
            if (minHeightStep > maxHeightStep)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHeightStep),
                    "높이 단계 구간의 끝이 시작보다 작다(" + minHeightStep + "~" + maxHeightStep + ").");
            }

            // 짝수 열은 높이 단계가 행의 2배, 홀수 열은 거기에 1을 더한 값이다.
            // 그래서 홀수 열은 양쪽 경계에서 1씩 밀린 자리에 걸린다.
            if (isOddColumn)
            {
                minRow = minHeightStep / 2;
                maxRow = (maxHeightStep - 1) / 2;
            }
            else
            {
                minRow = (minHeightStep + 1) / 2;
                maxRow = maxHeightStep / 2;
            }
        }

        // ====================================================================
        // 2. 중앙 대역(높이 단계 18~24) — 협곡형 ④⑤⑥ 과 외곽형 ④⑤⑥ 이 함께 쓴다
        // ====================================================================

        /// <summary> 중앙 대역의 시작 높이 단계. </summary>
        public const int CentralBandMinHeightStep = 18;

        /// <summary> 중앙 대역의 끝 높이 단계. </summary>
        public const int CentralBandMaxHeightStep = 24;

        /// <summary>
        /// 중앙 대역에 들어가는 타일 수.
        /// 짝수 열은 9~12행 4개 행에 6칸씩 24칸, 홀수 열은 9~11행 3개 행에 5칸씩 15칸이라
        /// 합이 39칸이다. 규칙이 명시한 수이므로 자기 검증에서 실제 집합의 크기와 대조한다.
        /// </summary>
        public const int CentralBandTileCount = 39;

        /// <summary>
        /// 그 높이 단계가 중앙 대역 안인지 확인한다.
        /// </summary>
        /// <param name="heightStep">확인할 높이 단계</param>
        /// <returns>중앙 대역 안이면 true</returns>
        public static bool IsWithinCentralBand(int heightStep)
        {
            return heightStep >= CentralBandMinHeightStep && heightStep <= CentralBandMaxHeightStep;
        }

        /// <summary>
        /// 그 칸이 중앙 대역 안인지 확인한다.
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <returns>중앙 대역 안이면 true</returns>
        public static bool IsWithinCentralBand(int col, int row)
        {
            return IsWithinCentralBand(GetHeightStep(col, row));
        }

        // ====================================================================
        // 3. 분리 길이별 대역 표 — 외곽형과 3갈래형의 단일 소스
        // ====================================================================
        //
        // 🔴 이 표를 두 생성기가 각자 베껴 적지 않는다. 한쪽만 고치면 두 유형의 대역이
        //    조용히 어긋나고, 그 어긋남은 맵을 눈으로 봐도 잘 드러나지 않는다.
        //
        //    분리 길이 L 의 대역은 「높이 단계 21 에서 L 만큼 위아래」다.
        //    아래 행 범위는 그 정의에서 그대로 계산되며, 규칙 문서의 표와 같다.
        //
        //      L        높이 단계    짝수 열     홀수 열
        //       5        16~26       8~13행      8~12행
        //       7        14~28       7~14행      7~13행
        //       9        12~30       6~15행      6~14행
        //      11        10~32       5~16행      5~15행

        private static readonly int[] SeparationLengthValues = { 5, 7, 9, 11 };

        /// <summary>
        /// 분리 길이 후보. 외곽형의 덩어리 길이와 3갈래형의 분리 길이가 같은 후보를 쓴다.
        /// 네 값은 모두 같은 확률로 뽑힌다.
        /// </summary>
        public static IReadOnlyList<int> SeparationLengths => SeparationLengthValues;

        /// <summary>
        /// 그 값이 분리 길이 후보에 있는지 확인한다.
        /// </summary>
        /// <param name="separationLength">확인할 분리 길이</param>
        /// <returns>후보에 있으면 true</returns>
        public static bool IsSeparationLengthValid(int separationLength)
        {
            for (int i = 0; i < SeparationLengthValues.Length; i++)
            {
                if (SeparationLengthValues[i] == separationLength) return true;
            }
            return false;
        }

        /// <summary>
        /// 분리 길이에 해당하는 대역의 시작 높이 단계를 구한다.
        /// </summary>
        /// <param name="separationLength">분리 길이</param>
        /// <returns>대역의 시작 높이 단계</returns>
        public static int GetBandMinHeightStep(int separationLength)
        {
            EnsureSeparationLength(separationLength);
            return CenterHeightStep - separationLength;
        }

        /// <summary>
        /// 분리 길이에 해당하는 대역의 끝 높이 단계를 구한다.
        /// </summary>
        /// <param name="separationLength">분리 길이</param>
        /// <returns>대역의 끝 높이 단계</returns>
        public static int GetBandMaxHeightStep(int separationLength)
        {
            EnsureSeparationLength(separationLength);
            return CenterHeightStep + separationLength;
        }

        /// <summary>
        /// 분리 길이에 해당하는 대역의 행 범위를 구한다(규칙 8의 표와 같은 값).
        /// </summary>
        /// <param name="separationLength">분리 길이</param>
        /// <param name="isOddColumn">홀수 열의 행 범위를 묻는 것이면 true</param>
        /// <param name="minRow">대역에 포함되는 가장 작은 행</param>
        /// <param name="maxRow">대역에 포함되는 가장 큰 행</param>
        public static void GetBandRowRange(int separationLength, bool isOddColumn,
            out int minRow, out int maxRow)
        {
            GetRowRange(GetBandMinHeightStep(separationLength), GetBandMaxHeightStep(separationLength),
                isOddColumn, out minRow, out maxRow);
        }

        /// <summary>
        /// 그 칸이 분리 길이에 해당하는 대역 안인지 확인한다.
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <param name="separationLength">분리 길이</param>
        /// <returns>대역 안이면 true</returns>
        public static bool IsWithinBand(int col, int row, int separationLength)
        {
            EnsureSeparationLength(separationLength);

            int distance = GetHeightStep(col, row) - CenterHeightStep;
            if (distance < 0) distance = -distance;

            return distance <= separationLength;
        }

        // ====================================================================
        // 4. 폭 — 열 구간으로 읽는다
        // ====================================================================

        /// <summary>
        /// 폭이 차지하는 가장 왼쪽 열을 구한다.
        /// </summary>
        /// <param name="width">홀수 폭(1 이상)</param>
        /// <returns>가장 왼쪽 열</returns>
        public static int GetWidthMinCol(int width)
        {
            EnsureOddWidth(width);
            return CenterCol - (width - 1) / 2;
        }

        /// <summary>
        /// 폭이 차지하는 가장 오른쪽 열을 구한다.
        /// </summary>
        /// <param name="width">홀수 폭(1 이상)</param>
        /// <returns>가장 오른쪽 열</returns>
        public static int GetWidthMaxCol(int width)
        {
            EnsureOddWidth(width);
            return CenterCol + (width - 1) / 2;
        }

        /// <summary>
        /// 그 열이 폭 안에 들어가는지 확인한다.
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="width">홀수 폭(1 이상)</param>
        /// <returns>폭 안이면 true</returns>
        public static bool IsWithinWidth(int col, int width)
        {
            EnsureOddWidth(width);

            int distance = col - CenterCol;
            if (distance < 0) distance = -distance;

            return distance <= (width - 1) / 2;
        }

        // ====================================================================
        // 5. 지형 기록 공통 루프
        // ====================================================================

        /// <summary>
        /// 윗절반(0행부터 가운데 행까지)만 훑으면서 지형을 기록한다.
        /// 아랫절반은 대응쌍 기록 API 가 자동으로 채우므로 여기서 손대지 않는다.
        ///
        /// 🔴 아랫절반의 행 번호를 직접 계산해 쓰지 않는 이유: 짝수 열과 홀수 열의
        ///    회전 행 식이 다르기 때문에 손으로 적으면 한쪽이 반드시 어긋난다.
        /// </summary>
        /// <param name="builder">기록 대상 builder</param>
        /// <param name="kindSelector">좌표 하나의 지형 종류를 돌려주는 함수</param>
        public static void ApplySymmetricTerrain(SymmetricMapBuilder builder, MapTileKindSelector kindSelector)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (kindSelector == null) throw new ArgumentNullException(nameof(kindSelector));

            for (int row = 0; row <= builder.CenterRow; row++)
            {
                for (int col = 0; col < builder.Width; col++)
                {
                    TileKind kind = kindSelector(col, row);

                    // 회전 중심은 자기 자신이 회전 상대라 전용 API 로 한 자리만 쓴다.
                    if (builder.IsCenter(col, row))
                    {
                        builder.SetCenter(kind);
                        continue;
                    }

                    // 짝 없는 6칸(0행 짝수 열)은 builder 생성자가 이미 막힌 타일로 고정했다.
                    // 대응쌍 API 로는 기록할 수 없는 칸이므로 건너뛴다.
                    if (!builder.HasRotationPartner(col, row)) continue;

                    builder.SetPair(col, row, kind);
                }
            }
        }

        // ====================================================================
        // 내부 도우미
        // ====================================================================

        /// <summary> 분리 길이가 후보 안의 값인지 확인한다. </summary>
        /// <param name="separationLength">확인할 분리 길이</param>
        private static void EnsureSeparationLength(int separationLength)
        {
            if (!IsSeparationLengthValid(separationLength))
            {
                throw new ArgumentOutOfRangeException(nameof(separationLength),
                    "분리 길이는 5 · 7 · 9 · 11 중 하나여야 한다(받은 값 " + separationLength + ").");
            }
        }

        /// <summary> 폭이 1 이상 홀수인지 확인한다. </summary>
        /// <param name="width">확인할 폭</param>
        private static void EnsureOddWidth(int width)
        {
            if (width < 1 || (width & 1) == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width),
                    "폭은 1 이상 홀수여야 한다(받은 값 " + width + ").");
            }
        }

        // ====================================================================
        // 자기 검증 — 이 프로젝트에는 유닛 테스트 어셈블리가 없다
        // ====================================================================
        //
        // 🔴 기대값은 "그렇게 나오면 좋겠다"가 아니라 규칙 문서에 적힌 값이거나
        //    격자 기하에서 그대로 따라 나오는 값이다. 실패하면 검사를 고치지 말고
        //    구현과 규칙 중 어느 쪽이 어긋났는지부터 판단할 것.

        /// <summary>
        /// 높이 단계 계산과 대역 표가 규칙대로인지 확인한다. 실패하면 사유를 채우고 false.
        /// (테스트 어셈블리가 생기면 이 메서드를 그대로 호출하면 되도록 Conditional 을 붙이지 않았다.)
        /// </summary>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>모두 통과하면 true</returns>
        public static bool TryRunSelfCheck(out string failureReason)
        {
            // ── 1. 높이 단계와 회전이 서로 맞는가(231칸 전수) ────────────────
            for (int row = 0; row < Height; row++)
            {
                for (int col = 0; col < Width; col++)
                {
                    int step = GetHeightStep(col, row);

                    if (step < MinHeightStep || step > MaxHeightStep)
                    {
                        failureReason = "[1] 높이 단계가 범위를 벗어났다(" + col + "," + row + " 단계 " + step + ").";
                        return false;
                    }

                    SymmetricMapBuilder.RotateCoord(col, row, Width, Height, out int rc, out int rr);
                    if (!SymmetricMapBuilder.IsInsideGrid(rc, rr, Width, Height)) continue;

                    if (GetHeightStep(rc, rr) != RotateHeightStep(step))
                    {
                        failureReason = "[1] 회전 상대의 높이 단계가 42에서 뺀 값과 다르다(" +
                            col + "," + row + ").";
                        return false;
                    }
                }
            }

            // ── 2. 🔴 중앙 대역이 정확히 39칸인가 ───────────────────────────
            var centralBand = new HashSet<int>();
            int centralEvenCount = 0;
            int centralOddCount = 0;

            for (int row = 0; row < Height; row++)
            {
                for (int col = 0; col < Width; col++)
                {
                    if (!IsWithinCentralBand(col, row)) continue;

                    centralBand.Add(row * Width + col);
                    if ((col & 1) == 0) centralEvenCount++;
                    else centralOddCount++;
                }
            }

            if (centralBand.Count != CentralBandTileCount)
            {
                failureReason = "[2] 중앙 대역이 " + CentralBandTileCount + "칸이 아니다(실제 " +
                    centralBand.Count + "칸).";
                return false;
            }

            if (centralEvenCount != 24 || centralOddCount != 15)
            {
                failureReason = "[2] 중앙 대역의 짝수 열 24칸 / 홀수 열 15칸 구성이 아니다(실제 " +
                    centralEvenCount + " / " + centralOddCount + ").";
                return false;
            }

            // 행 범위도 규칙이 명시한 값과 같아야 한다.
            GetRowRange(CentralBandMinHeightStep, CentralBandMaxHeightStep, false,
                out int centralEvenMinRow, out int centralEvenMaxRow);
            GetRowRange(CentralBandMinHeightStep, CentralBandMaxHeightStep, true,
                out int centralOddMinRow, out int centralOddMaxRow);

            if (centralEvenMinRow != 9 || centralEvenMaxRow != 12 ||
                centralOddMinRow != 9 || centralOddMaxRow != 11)
            {
                failureReason = "[2] 중앙 대역의 행 범위가 짝수 9~12 · 홀수 9~11 이 아니다(실제 짝수 " +
                    centralEvenMinRow + "~" + centralEvenMaxRow + " · 홀수 " +
                    centralOddMinRow + "~" + centralOddMaxRow + ").";
                return false;
            }

            // ── 3. 중앙 대역이 회전에 대해 닫혀 있는가 ──────────────────────
            foreach (int index in centralBand)
            {
                SymmetricMapBuilder.RotateCoord(index % Width, index / Width, Width, Height,
                    out int rc, out int rr);

                if (!SymmetricMapBuilder.IsInsideGrid(rc, rr, Width, Height) ||
                    !centralBand.Contains(rr * Width + rc))
                {
                    failureReason = "[3] 중앙 대역의 회전 상대가 대역 밖이다(인덱스 " + index + ").";
                    return false;
                }
            }

            // ── 4. 분리 길이별 행 범위가 규칙 8의 표와 같은가 ───────────────
            int[] expectedEvenMinRow = { 8, 7, 6, 5 };
            int[] expectedEvenMaxRow = { 13, 14, 15, 16 };
            int[] expectedOddMinRow = { 8, 7, 6, 5 };
            int[] expectedOddMaxRow = { 12, 13, 14, 15 };

            for (int i = 0; i < SeparationLengthValues.Length; i++)
            {
                int length = SeparationLengthValues[i];

                GetBandRowRange(length, false, out int evenMinRow, out int evenMaxRow);
                GetBandRowRange(length, true, out int oddMinRow, out int oddMaxRow);

                if (evenMinRow != expectedEvenMinRow[i] || evenMaxRow != expectedEvenMaxRow[i] ||
                    oddMinRow != expectedOddMinRow[i] || oddMaxRow != expectedOddMaxRow[i])
                {
                    failureReason = "[4] 분리 길이 " + length + " 의 행 범위가 규칙 8의 표와 다르다(실제 짝수 " +
                        evenMinRow + "~" + evenMaxRow + " · 홀수 " + oddMinRow + "~" + oddMaxRow + ").";
                    return false;
                }

                // 대역이 회전에 대해 닫혀 있는지도 확인한다.
                for (int row = 0; row < Height; row++)
                {
                    for (int col = 0; col < Width; col++)
                    {
                        if (!IsWithinBand(col, row, length)) continue;

                        SymmetricMapBuilder.RotateCoord(col, row, Width, Height, out int rc, out int rr);

                        if (!SymmetricMapBuilder.IsInsideGrid(rc, rr, Width, Height) ||
                            !IsWithinBand(rc, rr, length))
                        {
                            failureReason = "[4] 분리 길이 " + length + " 의 대역이 회전에 닫혀 있지 않다(" +
                                col + "," + row + ").";
                            return false;
                        }
                    }
                }

                // 중앙 대역은 어떤 분리 길이의 대역에도 반드시 포함된다(가장 짧은 5도 16~26).
                foreach (int index in centralBand)
                {
                    if (!IsWithinBand(index % Width, index / Width, length))
                    {
                        failureReason = "[4] 분리 길이 " + length + " 의 대역이 중앙 대역을 품지 못한다.";
                        return false;
                    }
                }
            }

            // ── 5. 폭이 회전에 보존되는가 ───────────────────────────────────
            int[] widths = { 1, 3, 5, 7, 9, 11 };
            for (int i = 0; i < widths.Length; i++)
            {
                for (int col = 0; col < Width; col++)
                {
                    if (IsWithinWidth(col, widths[i]) != IsWithinWidth(Width - 1 - col, widths[i]))
                    {
                        failureReason = "[5] 폭 " + widths[i] + " 이 회전에 보존되지 않는다(열 " + col + ").";
                        return false;
                    }
                }

                int minCol = GetWidthMinCol(widths[i]);
                int maxCol = GetWidthMaxCol(widths[i]);

                if (maxCol - minCol + 1 != widths[i])
                {
                    failureReason = "[5] 폭 " + widths[i] + " 의 열 구간 길이가 폭과 다르다(" +
                        minCol + "~" + maxCol + ").";
                    return false;
                }
            }

            // ── 6. 음성 대조 — 회전에 닫히지 않은 대역은 걸러져야 한다 ──────
            //
            // "중앙 3개 행"을 뜻으로 9~11행을 잡으면 회전 대칭이 아니다. 이 사실을
            // 검사가 실제로 잡아내는지 확인한다. 통과해야 할 것만 보는 검사는
            // 무엇이든 통과시키는 검사와 구별되지 않는다.
            bool rowBandIsClosed = true;
            for (int row = 9; row <= 11 && rowBandIsClosed; row++)
            {
                for (int col = 0; col < Width; col++)
                {
                    SymmetricMapBuilder.RotateCoord(col, row, Width, Height, out int rc, out int rr);
                    if (rr < 9 || rr > 11)
                    {
                        rowBandIsClosed = false;
                        break;
                    }
                }
            }

            if (rowBandIsClosed)
            {
                failureReason = "[6] 음성 대조 실패 — 9~11행 대역이 회전에 닫혀 있다고 나왔다. " +
                    "닫혀 있지 않아야 정상이며, 그래서 대역을 높이 단계로 잡는다.";
                return false;
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 자기 검증 실패를 에디터에서 즉시 드러낸다. 빌드에는 들어가지 않는다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void AssertSelfCheck()
        {
            if (!TryRunSelfCheck(out string failureReason))
            {
                throw new InvalidOperationException("MapBandTable 자기 검증 실패: " + failureReason);
            }
        }
    }
}
