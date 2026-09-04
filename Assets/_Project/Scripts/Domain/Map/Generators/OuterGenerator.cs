// ============================================================================
// OuterGenerator.cs
// 외곽형(규칙 7) 맵 생성기.
//
// ─────────────────────────────────────────────────────────────────────────────
// ① 이 유형이 노리는 것
// ─────────────────────────────────────────────────────────────────────────────
//   맵 한가운데를 통째로 막아 두 진영이 반드시 바깥으로 돌아가게 만든다.
//   길은 왼쪽과 오른쪽 두 갈래이며, 둘 다 맵의 위에서 아래까지 끊기지 않는다.
//
// ─────────────────────────────────────────────────────────────────────────────
// ② 지형 (초급자용 설명)
// ─────────────────────────────────────────────────────────────────────────────
//   중앙 차단 덩어리는 두 값으로 정해진다.
//     · 길이   : 5 · 7 · 9 · 11 중 하나(같은 확률)
//     · 최대 폭 : 3 · 5 중 하나(같은 확률)
//
//   덩어리 대역은 「높이 단계 21 에서 그 길이만큼 위아래」다.
//   길이별 실제 행 범위는 MapBandTable 의 표가 단일 소스이며, 3갈래형도 같은 표를
//   읽는다 — 두 유형이 각자 표를 베껴 적으면 한쪽만 고쳐졌을 때 조용히 어긋난다.
//
//   덩어리에 포함된 각 높이 단계는 가운데 열을 중심으로 한 「하나의 연속 홀수 폭」
//   구간이 막힌 형태다. 중앙선(높이 단계 21)은 반드시 뽑은 최대 폭이다.
//   윗절반의 폭만 정하고 아랫절반은 회전 복제이므로 덩어리는 저절로 대칭이 된다.
//
//   최대 폭이 5 를 넘지 않으므로 0~2열과 8~10열은 어느 높이에서도 막히지 않는다.
//   그래서 좌·우 통로는 위아래로 끝까지 이어지고, 규정 폭도 언제나 3 이상이다.
//     · 최대 폭 3 이면 통로는 각각 4열(0~3열 / 7~10열)
//     · 최대 폭 5 이면 통로는 각각 3열(0~2열 / 8~10열)
//
// ─────────────────────────────────────────────────────────────────────────────
// 덩어리 모양 3종 — 셋 중 하나를 같은 확률로 고른다
// ─────────────────────────────────────────────────────────────────────────────
//   폭은 1 · 3 · 5 세 값밖에 쓸 수 없으므로, 세 모양의 차이는 「끝에서 중앙으로
//   갈 때 얼마나 빨리 넓어지는가」로 나타난다.
//
//   · 마름모형 : 끝에서 한 단계 갈 때마다 2씩 넓어진다(1 → 3 → 5). 최대 폭에
//                도달하면 거기서 멈추고 그대로 이어진다. 뾰족한 끝 + 긴 몸통.
//   · 타원형   : 끝에서 두 단계 갈 때마다 2씩 넓어진다. 어깨가 완만해서 좁은
//                구간이 더 길고, 중앙에는 최대 폭이 그대로 이어지는 구간이 남는다.
//   · 울퉁불퉁형 : 단계마다 폭을 무작위로 정한다. 아래 다섯 제약을 동시에 지킨다.
//
//   ⚠️ 마름모형과 타원형을 「넓어지는 속도」로 구분한 것은 이 프로젝트의 운용상
//      정의다. 실제 폭 값이 1 · 3 · 5 뿐이라 곡선 모양으로는 두 형태를 구분할 수
//      없기 때문이다(같은 값이 나온다).
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔴 울퉁불퉁형이 다섯 제약을 동시에 지키는 방법
// ─────────────────────────────────────────────────────────────────────────────
//   지켜야 할 것은 ① 중앙선은 최대 폭 ② 폭은 홀수 ③ 위아래로 끊기지 않고 이어짐
//   ④ 인접 단계 폭 차이 최대 2 ⑤ 회전 대칭 이다.
//
//   폭을 다 뽑아 놓고 나서 검사해 버리는 방식(뽑고 나서 고치기)은 쓰지 않는다.
//   대신 「애초에 규칙을 어길 수 없는 방법으로만 뽑는다」.
//
//     · 중앙선부터 바깥으로 한 단계씩 나아가며 폭을 정한다. 시작값이 최대 폭이므로
//       ① 은 뽑기 전에 이미 지켜져 있다.
//     · 각 단계의 후보는 「직전 폭보다 2 작은 값 · 같은 값 · 2 큰 값」 셋뿐이고,
//       그중 1 이상 최대 폭 이하인 것만 남긴다. 이 후보 목록에서 균등하게 뽑는다.
//       → 직전 값이 홀수면 후보도 전부 홀수이므로 ② 가 지켜진다.
//       → 후보의 차이가 항상 2 이므로 ④ 가 지켜진다.
//       → 후보가 1 이상이므로 폭이 0 이 되는 일이 없다. 모든 단계에 막힌 칸이
//         하나 이상 남고, 이웃한 단계의 막힌 구간은 가운데 열 근처에서 반드시
//         맞닿으므로 ③ 이 지켜진다(덩어리에 공동이 생기지 않는다).
//     · 정한 것은 「중앙선에서 바깥으로 얼마나 떨어졌는가」에 대한 폭 하나뿐이고,
//       위쪽과 아래쪽에 같은 값을 쓴다. → ⑤ 가 지켜진다.
//
//   즉 다섯 제약이 전부 「뽑기 후보를 좁히는 방식」으로 흡수돼 있어서, 사후 검사나
//   재시도가 필요 없다. 자기 검증은 이 사실을 확인하는 역할만 한다.
//
// ─────────────────────────────────────────────────────────────────────────────
// ③④⑤⑥
// ─────────────────────────────────────────────────────────────────────────────
//   ③ 허용 중립 광산 수 : 2 · 4 · 6 (연속 범위가 아니라 이 셋뿐이다)
//   ④ 광산 금지        : 높이 단계 18~24 안에는 광산 대응쌍을 최대 1쌍만 둔다.
//                        나머지는 더 넓은 위·아래 구역으로 간다.
//                        🔴 이 범위는 덩어리 길이와 무관하게 고정이며 협곡형과 같다.
//   ⑤ 건설 불가        : 높이 단계 18~24 에서 덩어리 바깥의 열린 외곽 통로 타일 전부
//   ⑥ 필수 통로       : 좌·우 통로 2개(각각 규정 폭 3 이상)
//
//   ⚠️ 「대역 안 대응쌍은 최대 1쌍」을 제약 인터페이스로 표현하는 방법
//      광산 금지 판정은 칸 하나를 받아 참/거짓만 돌려주는 함수라 "몇 쌍까지"라는
//      개수 조건을 담을 수 없다. 그래서 이 생성기가 대역 안의 열린 대응쌍 중
//      「허용할 한 쌍」을 시도마다 하나 뽑아 두고 나머지 대역 칸을 전부 금지로
//      표시한다. 결과적으로 대역 안에 놓일 수 있는 대응쌍은 많아야 한 쌍이다.
//
//   장식은 만들지 않는다(규칙 15). Decoration 스트림에서 한 번도 뽑지 않는다.
//
// 근거: GameSystemRules/GameSystemRules_RandomMap.md 규칙 7 · 규칙 8(대역 표) · 규칙 15
// Domain 레이어 — 순수 C#, Unity/Core 의존 없음.
// ============================================================================

using System;
using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 외곽형 중앙 덩어리의 모양. 숫자 값은 뽑기 순서에 그대로 쓰이므로 바꾸지 말 것.
    /// </summary>
    public enum OuterMassShape
    {
        /// <summary> 마름모형 — 끝에서 한 단계마다 2씩 넓어진다. </summary>
        Diamond = 0,

        /// <summary> 타원형 — 끝에서 두 단계마다 2씩 넓어진다. </summary>
        Ellipse = 1,

        /// <summary> 울퉁불퉁형 — 단계마다 무작위로 정하되 다섯 제약을 지킨다. </summary>
        Rugged = 2
    }

    /// <summary>
    /// 외곽형(규칙 7) 생성기. 중앙을 덩어리로 막고 좌·우 두 통로만 남긴다.
    /// </summary>
    public sealed class OuterGenerator : MapArchetypeGeneratorBase
    {
        // ====================================================================
        // 규칙 7 의 숫자들
        // ====================================================================

        private static readonly int[] MassMaxWidthValues = { 3, 5 };

        /// <summary> 덩어리 최대 폭 후보. 두 값이 같은 확률로 뽑힌다. </summary>
        public static IReadOnlyList<int> MassMaxWidthCandidates => MassMaxWidthValues;

        /// <summary> 덩어리 모양의 가짓수. 셋을 각각 3분의 1로 뽑는다. </summary>
        public const int MassShapeCount = 3;

        /// <summary> 이 유형이 허용하는 중립 광산 최소 개수(규칙 7 ③). </summary>
        public const int OuterMinNeutralMineCount = 2;

        /// <summary> 이 유형이 허용하는 중립 광산 최대 개수(규칙 7 ③). </summary>
        public const int OuterMaxNeutralMineCount = 6;

        /// <summary> 필수 통로 이름 — 왼쪽. </summary>
        public const string LeftCorridorName = "좌측 외곽 통로";

        /// <summary> 필수 통로 이름 — 오른쪽. </summary>
        public const string RightCorridorName = "우측 외곽 통로";

        /// <summary> 필수 통로가 지켜야 하는 최소 규정 폭. </summary>
        public const int MinCorridorWidth = 3;

        /// <summary> 이 생성기가 만드는 맵 유형. </summary>
        public override MapType MapType => MapType.Outer;

        /// <summary> 이 유형이 허용하는 중립 광산 최소 개수. </summary>
        public override int MinNeutralMineCount => OuterMinNeutralMineCount;

        /// <summary> 이 유형이 허용하는 중립 광산 최대 개수. </summary>
        public override int MaxNeutralMineCount => OuterMaxNeutralMineCount;

        /// <summary>
        /// 이 유형은 2 · 4 · 6 만 허용한다. 연속 범위가 아니므로 기본 판정을 덮어쓴다.
        /// (덩어리가 회전 중심을 반드시 막기 때문에 홀수 개일 때 쓰는 중심 단독 광산
        ///  자리를 쓸 수 없다. 그래서 짝수만 성립한다.)
        /// </summary>
        /// <param name="neutralMineCount">확인할 중립 광산 개수</param>
        /// <returns>허용하면 true</returns>
        public override bool IsNeutralMineCountAllowed(int neutralMineCount)
        {
            if ((neutralMineCount & 1) != 0) return false;
            return neutralMineCount >= OuterMinNeutralMineCount && neutralMineCount <= OuterMaxNeutralMineCount;
        }

        // ====================================================================
        // 지형 그리기
        // ====================================================================

        /// <summary>
        /// 규칙 7대로 중앙 덩어리를 그린다. 윗절반의 폭만 정하고 아랫절반은
        /// 대응쌍 기록 API 가 자동으로 채운다.
        /// </summary>
        /// <param name="builder">기록 대상 builder(성·시작 광산은 이미 놓여 있다)</param>
        /// <param name="terrainStream">Terrain 스트림</param>
        /// <param name="constraints">이 시도에서 확정된 유형별 제약</param>
        /// <returns>성공이면 null, 거부면 사유</returns>
        protected override string TryApplyTerrain(SymmetricMapBuilder builder, MapRandom terrainStream,
            out IMapArchetypeConstraints constraints)
        {
            constraints = null;

            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (terrainStream == null) throw new ArgumentNullException(nameof(terrainStream));

            // ── 1. 길이 · 최대 폭 · 모양 ────────────────────────────────────
            int massLength = terrainStream.Choose(MapBandTable.SeparationLengths);
            int maxWidth = terrainStream.Choose(MassMaxWidthValues);
            var shape = (OuterMassShape)terrainStream.NextInt(MassShapeCount);

            // ── 2. 중앙선에서 바깥으로 나가며 폭을 정한다 ───────────────────
            int[] halfProfile = BuildHalfProfile(terrainStream, massLength, maxWidth, shape);

            // ── 3. 지형 기록 ────────────────────────────────────────────────
            MapBandTable.ApplySymmetricTerrain(builder,
                (col, row) => GetTileKind(col, row, massLength, halfProfile));

            // ── 4. ④⑤⑥ 제약 ───────────────────────────────────────────────
            constraints = BuildConstraints(builder, terrainStream, out string constraintReason);
            if (constraints == null) return constraintReason;

            return null;
        }

        /// <summary>
        /// 중앙선에서 바깥으로 나가며 폭을 정한다. 결과의 i 번째 값은
        /// 「중앙선에서 i 단계 떨어진 높이 단계」의 폭이다.
        /// 위쪽과 아래쪽이 같은 값을 쓰므로 회전 대칭은 이 형태 자체로 보장된다.
        /// </summary>
        /// <param name="terrainStream">Terrain 스트림</param>
        /// <param name="massLength">덩어리 길이</param>
        /// <param name="maxWidth">덩어리 최대 폭</param>
        /// <param name="shape">덩어리 모양</param>
        /// <returns>중앙선으로부터의 거리로 찾아 쓰는 폭 배열</returns>
        private static int[] BuildHalfProfile(MapRandom terrainStream, int massLength, int maxWidth,
            OuterMassShape shape)
        {
            var halfProfile = new int[massLength + 1];

            switch (shape)
            {
                case OuterMassShape.Diamond:
                    // 끝(거리 massLength)에서 폭 1 로 시작해 한 단계마다 2씩 넓어지고,
                    // 최대 폭에 닿으면 그대로 이어진다.
                    for (int distance = 0; distance <= massLength; distance++)
                    {
                        int grown = 1 + 2 * (massLength - distance);
                        halfProfile[distance] = grown < maxWidth ? grown : maxWidth;
                    }
                    break;

                case OuterMassShape.Ellipse:
                    // 끝에서 두 단계마다 2씩 넓어진다. 어깨가 완만해지는 대신
                    // 중앙에는 최대 폭이 그대로 이어지는 구간이 남는다.
                    for (int distance = 0; distance <= massLength; distance++)
                    {
                        int grown = 1 + 2 * ((massLength - distance) / 2);
                        halfProfile[distance] = grown < maxWidth ? grown : maxWidth;
                    }
                    break;

                default:
                    // 울퉁불퉁형 — 뽑기 후보 자체를 규칙 안으로 좁혀 둔다(파일 머리말 참조).
                    halfProfile[0] = maxWidth;

                    var options = new List<int>(3);
                    for (int distance = 1; distance <= massLength; distance++)
                    {
                        int previous = halfProfile[distance - 1];

                        options.Clear();
                        AddOptionIfValid(options, previous - 2, maxWidth);
                        AddOptionIfValid(options, previous, maxWidth);
                        AddOptionIfValid(options, previous + 2, maxWidth);

                        halfProfile[distance] = options[terrainStream.NextInt(options.Count)];
                    }
                    break;
            }

            return halfProfile;
        }

        /// <summary> 폭 후보가 1 이상 최대 폭 이하일 때만 후보 목록에 넣는다. </summary>
        /// <param name="options">후보 목록</param>
        /// <param name="width">넣으려는 폭</param>
        /// <param name="maxWidth">덩어리 최대 폭</param>
        private static void AddOptionIfValid(List<int> options, int width, int maxWidth)
        {
            if (width >= 1 && width <= maxWidth) options.Add(width);
        }

        /// <summary>
        /// 좌표 하나의 지형 종류를 정한다.
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <param name="massLength">덩어리 길이</param>
        /// <param name="halfProfile">중앙선으로부터의 거리로 찾아 쓰는 폭 배열</param>
        /// <returns>그 칸에 놓을 지형 종류</returns>
        private static TileKind GetTileKind(int col, int row, int massLength, int[] halfProfile)
        {
            int step = MapBandTable.GetHeightStep(col, row);

            int distance = step - MapBandTable.CenterHeightStep;
            if (distance < 0) distance = -distance;

            if (distance <= massLength && MapBandTable.IsWithinWidth(col, halfProfile[distance]))
            {
                return TileKind.Blocked;
            }

            // 덩어리 바깥이라도 높이 단계 18~24 안이면 건설 불가 외곽 통로다.
            return MapBandTable.IsWithinCentralBand(step) ? TileKind.NoBuild : TileKind.Normal;
        }

        /// <summary>
        /// 이 시도의 ④⑤⑥ 제약을 만든다.
        /// </summary>
        /// <param name="builder">지형이 이미 기록된 builder</param>
        /// <param name="terrainStream">Terrain 스트림(허용할 대역 대응쌍을 하나 뽑는 데 쓴다)</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>유형별 제약(실패하면 null)</returns>
        private static IMapArchetypeConstraints BuildConstraints(SymmetricMapBuilder builder,
            MapRandom terrainStream, out string failureReason)
        {
            // ── 대역 안에서 허용할 대응쌍 하나를 미리 뽑는다 ────────────────
            var pairCandidates = new List<int>();

            for (int row = 0; row < builder.Height; row++)
            {
                for (int col = 0; col < builder.Width; col++)
                {
                    if (builder.IsCenter(col, row)) continue;
                    if (!builder.HasRotationPartner(col, row)) continue;
                    if (!MapBandTable.IsWithinCentralBand(col, row)) continue;

                    builder.RotateCoord(col, row, out int rotatedCol, out int rotatedRow);

                    int index = row * builder.Width + col;
                    int rotatedIndex = rotatedRow * builder.Width + rotatedCol;
                    if (index > rotatedIndex) continue;

                    // 막힌 칸에는 광산이 놓이지 않으므로 애초에 후보가 아니다.
                    if (builder.GetTile(col, row) == TileKind.Blocked) continue;
                    if (builder.GetTile(rotatedCol, rotatedRow) == TileKind.Blocked) continue;

                    pairCandidates.Add(index);
                }
            }

            if (pairCandidates.Count == 0)
            {
                failureReason = "높이 단계 18~24 안에 열린 대응쌍 후보가 하나도 없다.";
                return null;
            }

            int allowedPairIndex = pairCandidates[terrainStream.NextInt(pairCandidates.Count)];

            SymmetricMapBuilder.RotateCoord(allowedPairIndex % builder.Width, allowedPairIndex / builder.Width,
                builder.Width, builder.Height, out int allowedRotatedCol, out int allowedRotatedRow);
            int allowedPartnerIndex = allowedRotatedRow * builder.Width + allowedRotatedCol;

            // ── 금지 구역과 좌·우 통로를 모은다 ─────────────────────────────
            var mineForbidden = new List<int>();
            var buildForbidden = new List<int>();
            var leftCorridorTiles = new List<int>();
            var rightCorridorTiles = new List<int>();

            for (int row = 0; row < builder.Height; row++)
            {
                for (int col = 0; col < builder.Width; col++)
                {
                    if (!MapBandTable.IsWithinCentralBand(col, row)) continue;

                    int index = row * builder.Width + col;

                    if (index != allowedPairIndex && index != allowedPartnerIndex)
                    {
                        mineForbidden.Add(index);
                    }

                    if (builder.GetTile(col, row) == TileKind.Blocked) continue;

                    buildForbidden.Add(index);

                    if (col < MapBandTable.CenterCol) leftCorridorTiles.Add(index);
                    else rightCorridorTiles.Add(index);
                }
            }

            var corridors = new List<MapCorridorRequirement>(2)
            {
                new MapCorridorRequirement(LeftCorridorName, leftCorridorTiles),
                new MapCorridorRequirement(RightCorridorName, rightCorridorTiles)
            };

            failureReason = null;
            return new MapArchetypeConstraints(MapType.Outer, builder.Width,
                mineForbidden, buildForbidden, corridors);
        }

        // ====================================================================
        // 폭 프로파일 검사 — 자기 검증과 음성 대조가 함께 쓴다
        // ====================================================================

        /// <summary>
        /// 덩어리의 폭 프로파일이 규칙 7의 다섯 제약을 지키는지 확인한다.
        /// (회전 대칭은 프로파일이 「중앙선으로부터의 거리」 하나로만 정의된다는 사실에서
        ///  구조적으로 따라 나오므로 여기서는 나머지 넷을 본다.)
        /// 자기 검증과 음성 대조가 같은 함수를 쓴다 — 검사가 진짜로 무엇을 걸러내는지
        /// 확인하려면 「걸러져야 하는 입력」도 같은 문으로 넣어 봐야 하기 때문이다.
        /// </summary>
        /// <param name="halfProfile">중앙선으로부터의 거리로 찾아 쓰는 폭 배열</param>
        /// <param name="maxWidth">덩어리 최대 폭</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>규칙대로면 true</returns>
        public static bool TryVerifyMassWidthProfile(int[] halfProfile, int maxWidth, out string failureReason)
        {
            if (halfProfile == null) throw new ArgumentNullException(nameof(halfProfile));

            if (halfProfile.Length == 0)
            {
                failureReason = "폭 배열이 비어 있다.";
                return false;
            }

            if (halfProfile[0] != maxWidth)
            {
                failureReason = "중앙선의 폭이 최대 폭 " + maxWidth + " 이 아니다(실제 " + halfProfile[0] + ").";
                return false;
            }

            for (int distance = 0; distance < halfProfile.Length; distance++)
            {
                int width = halfProfile[distance];

                if (width < 1 || width > maxWidth)
                {
                    failureReason = "거리 " + distance + " 의 폭이 1 이상 최대 폭 이하가 아니다(실제 " +
                        width + ").";
                    return false;
                }

                if ((width & 1) == 0)
                {
                    failureReason = "거리 " + distance + " 의 폭이 홀수가 아니다(실제 " + width + ").";
                    return false;
                }

                if (distance == 0) continue;

                int difference = width - halfProfile[distance - 1];
                if (difference < 0) difference = -difference;

                if (difference > 2)
                {
                    failureReason = "거리 " + distance + " 의 인접 폭 차이가 " + difference + " 이다(최대 2).";
                    return false;
                }
            }

            failureReason = null;
            return true;
        }

        // ====================================================================
        // 자기 검증 — 이 프로젝트에는 유닛 테스트 어셈블리가 없다
        // ====================================================================
        //
        // 🔴 기대값은 "그렇게 나오면 좋겠다"가 아니라 규칙 문서에 적힌 값이거나
        //    격자 기하에서 그대로 따라 나오는 값이다. 실패하면 검사를 고치지 말고
        //    구현과 규칙 중 어느 쪽이 어긋났는지부터 판단할 것.

        /// <summary> 통계 확인에 쓰는 서로 다른 시드의 개수. </summary>
        public const int SelfCheckSeedCount = 200;

        private static readonly int[] SelfCheckExpectedLengthCountValues = { 53, 58, 50, 39 };
        private static readonly int[] SelfCheckExpectedMaxWidthCountValues = { 99, 101 };
        private static readonly int[] SelfCheckExpectedShapeCountValues = { 72, 65, 63 };

        /// <summary>
        /// 시드 0~199 에서 덩어리 길이 5 · 7 · 9 · 11 이 각각 몇 번 뽑히는지의 기대값.
        /// 파이썬 독립 구현으로 얻은 실측값이며 합은 200 이다.
        /// </summary>
        public static IReadOnlyList<int> SelfCheckExpectedLengthCounts => SelfCheckExpectedLengthCountValues;

        /// <summary>
        /// 시드 0~199 에서 최대 폭 3 · 5 가 각각 몇 번 뽑히는지의 기대값.
        /// </summary>
        public static IReadOnlyList<int> SelfCheckExpectedMaxWidthCounts => SelfCheckExpectedMaxWidthCountValues;

        /// <summary>
        /// 시드 0~199 에서 마름모형 · 타원형 · 울퉁불퉁형이 각각 몇 번 뽑히는지의 기대값.
        ///
        /// 🔴 이 수들이 어긋나면 검사를 고치지 말 것. 뽑기 순서나 확률이 바뀌었다는 뜻이고,
        ///    그러면 과거 시드가 다른 맵을 만들게 되므로 MapVersion 을 올려야 하는지부터 판단한다.
        /// </summary>
        public static IReadOnlyList<int> SelfCheckExpectedShapeCounts => SelfCheckExpectedShapeCountValues;

        /// <summary>
        /// 시드 0~199 의 차단 타일 총합 기대값(짝 없는 6칸은 제외).
        /// 파이썬 독립 구현으로 얻은 실측값이다.
        /// </summary>
        public const int SelfCheckExpectedBlockedTotal = 4832;

        /// <summary>
        /// 시드 0~199 의 건설 불가 타일 총합 기대값.
        /// 높이 단계 18~24 의 39칸에서 덩어리가 차지한 칸을 뺀 수의 합이다.
        /// 파이썬 독립 구현으로 얻은 실측값이다.
        /// </summary>
        public const int SelfCheckExpectedNoBuildTotal = 5302;

        /// <summary>
        /// 외곽형 결과가 규칙 7대로인지 확인한다. 실패하면 사유를 채우고 false.
        /// (테스트 어셈블리가 생기면 이 메서드를 그대로 호출하면 되도록 Conditional 을 붙이지 않았다.)
        /// </summary>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>모두 통과하면 true</returns>
        public static bool TryRunSelfCheck(out string failureReason)
        {
            if (!MapBandTable.TryRunSelfCheck(out string bandReason))
            {
                failureReason = "[0] 대역 표 자기 검증 실패: " + bandReason;
                return false;
            }

            var generator = new OuterGenerator();
            var unpairedTiles = new HashSet<int>(new SymmetricMapBuilder().UnpairedTileIndices);

            // ③ 허용 개수가 2 · 4 · 6 뿐이라는 사실부터 확인한다.
            for (int count = -1; count <= 8; count++)
            {
                bool expected = count == 2 || count == 4 || count == 6;
                if (generator.IsNeutralMineCountAllowed(count) != expected)
                {
                    failureReason = "[1] 중립 광산 개수 " + count + " 의 허용 판정이 규칙 7 ③ 과 다르다.";
                    return false;
                }
            }

            var lengthCounts = new int[MapBandTable.SeparationLengths.Count];
            var maxWidthCounts = new int[MassMaxWidthValues.Length];
            var shapeCounts = new int[MassShapeCount];
            int blockedTotal = 0;
            int noBuildTotal = 0;

            for (int seed = 0; seed < SelfCheckSeedCount; seed++)
            {
                int mineCount = 2 + 2 * (seed % 3);

                var request = new MapGenerationRequest
                {
                    MapVersion = MapDefinition.CurrentMapVersion,
                    RootSeed = (ulong)seed,
                    AttemptIndex = 0,
                    StartingMineSide = (seed % 2 == 0) ? MapStartingMineSide.CaseA : MapStartingMineSide.CaseB,
                    NeutralMineCount = mineCount
                };

                MapGenerationResult result = generator.Generate(request);

                if (!result.IsAccepted)
                {
                    failureReason = "[2] 시드 " + seed + " 가 거부됐다: " + result.RejectionReason;
                    return false;
                }

                if (result.DecorationDrawCount != 0)
                {
                    failureReason = "[3] Decoration 스트림을 " + result.DecorationDrawCount +
                        "번 썼다(규칙 15에 따라 0번이어야 한다).";
                    return false;
                }

                MapDefinition definition = result.Definition;
                int width = definition.Width;

                // ── 4. 회전 대칭 전수 확인 ──────────────────────────────────
                if (!TryVerifyRotationalSymmetry(definition, out string symmetryReason))
                {
                    failureReason = "[4] 시드 " + seed + ": " + symmetryReason;
                    return false;
                }

                // ── 5. 이 시도가 무엇을 뽑았는지 같은 시드로 다시 재생한다 ──
                //
                // 🔴 완성된 맵에서 폭을 되읽는 방법은 쓸 수 없다. 한 높이 단계에는
                //    짝수 열 아니면 홀수 열만 있어서, 홀수 단계에서는 폭 1 과 폭 3 이
                //    (둘 다 가운데 열 한 칸) 똑같아 보이고 짝수 단계에서는 폭 3 과
                //    폭 5 가 (둘 다 4열과 6열) 똑같아 보인다. 즉 되읽기는 정보를 잃는다.
                //    그래서 같은 시드로 Terrain 스트림을 다시 만들어 같은 순서로 뽑아
                //    「무엇이 뽑혔는지」를 그대로 재생한다.
                MapRandom replayStream = MapRandomStreams.CreateAttemptStream(
                    request.MapVersion, request.RootSeed, MapRandomStreams.Terrain, request.AttemptIndex);

                int massLength = replayStream.Choose(MapBandTable.SeparationLengths);
                int maxWidth = replayStream.Choose(MassMaxWidthValues);
                var shape = (OuterMassShape)replayStream.NextInt(MassShapeCount);
                int[] halfProfile = BuildHalfProfile(replayStream, massLength, maxWidth, shape);

                int lengthSlot = IndexOf(MapBandTable.SeparationLengths, massLength);
                int maxWidthSlot = IndexOf(MassMaxWidthValues, maxWidth);

                if (lengthSlot < 0 || maxWidthSlot < 0)
                {
                    failureReason = "[5] 시드 " + seed + ": 덩어리 길이 " + massLength + " · 최대 폭 " +
                        maxWidth + " 가 후보에 없다.";
                    return false;
                }

                lengthCounts[lengthSlot]++;
                maxWidthCounts[maxWidthSlot]++;
                shapeCounts[(int)shape]++;

                // ── 6. 다섯 제약 확인 ───────────────────────────────────────
                if (!TryVerifyMassWidthProfile(halfProfile, maxWidth, out string profileReason))
                {
                    failureReason = "[6] 시드 " + seed + ": " + profileReason;
                    return false;
                }

                // ── 7. 지형이 폭 프로파일과 정확히 같은가(231칸 전수) ───────
                for (int index = 0; index < definition.TileCount; index++)
                {
                    TileKind actual = definition.Tiles[index];

                    if (unpairedTiles.Contains(index))
                    {
                        if (actual != TileKind.Blocked)
                        {
                            failureReason = "[7] 시드 " + seed + ": 짝 없는 칸이 막혀 있지 않다(인덱스 " +
                                index + ").";
                            return false;
                        }
                        continue;
                    }

                    int col = index % width;
                    int row = index / width;
                    TileKind expected = GetTileKind(col, row, massLength, halfProfile);

                    if (actual != expected)
                    {
                        failureReason = "[7] 시드 " + seed + ": (" + col + "," + row + ") 이 " +
                            expected + " 이어야 하는데 " + actual + " 이다.";
                        return false;
                    }

                    if (actual == TileKind.Blocked) blockedTotal++;
                    else if (actual == TileKind.NoBuild) noBuildTotal++;
                }

                // ── 8. 덩어리에 공동이 없는가(연결성 전수 탐색) ─────────────
                if (!TryVerifyMassConnectivity(definition, unpairedTiles, out string connectivityReason))
                {
                    failureReason = "[8] 시드 " + seed + ": " + connectivityReason;
                    return false;
                }

                // ── 9. 좌·우 통로가 위아래로 끝까지 이어지는가 ──────────────
                if (!TryVerifyOuterCorridorsContinuous(definition, unpairedTiles, out string continuityReason))
                {
                    failureReason = "[9] 시드 " + seed + ": " + continuityReason;
                    return false;
                }

                // ── 10. ⑤⑥ 제약 ───────────────────────────────────────────
                IMapArchetypeConstraints constraints = result.Constraints;

                if (constraints.RequiredCorridors.Count != 2)
                {
                    failureReason = "[10] 시드 " + seed + ": 필수 통로가 2개가 아니다(실제 " +
                        constraints.RequiredCorridors.Count + "개).";
                    return false;
                }

                for (int side = 0; side < 2; side++)
                {
                    IReadOnlyList<int> tiles = constraints.RequiredCorridors[side].TileIndices;
                    var corridorCols = new HashSet<int>();

                    for (int i = 0; i < tiles.Count; i++)
                    {
                        int col = tiles[i] % width;
                        int row = tiles[i] / width;

                        if (definition.Tiles[tiles[i]] != TileKind.NoBuild)
                        {
                            failureReason = "[10] 시드 " + seed + ": 통로 타일이 건설 불가 타일이 아니다(" +
                                col + "," + row + ").";
                            return false;
                        }

                        if (!constraints.IsBuildForbidden(col, row))
                        {
                            failureReason = "[10] 시드 " + seed + ": 통로 타일이 건설 불가 구역에 없다(" +
                                col + "," + row + ").";
                            return false;
                        }

                        bool isLeft = col < MapBandTable.CenterCol;
                        if (isLeft != (side == 0))
                        {
                            failureReason = "[10] 시드 " + seed + ": 통로가 좌우로 섞였다(" +
                                col + "," + row + ").";
                            return false;
                        }

                        corridorCols.Add(col);
                    }

                    if (corridorCols.Count < MinCorridorWidth)
                    {
                        failureReason = "[10] 시드 " + seed + ": 통로의 규정 폭이 " + MinCorridorWidth +
                            " 미만이다(실제 " + corridorCols.Count + "열).";
                        return false;
                    }
                }

                // ── 11. ④ 대역 안 광산이 최대 1쌍인가 ──────────────────────
                int bandMineCount = 0;

                for (int i = 0; i < definition.NeutralMines.Count; i++)
                {
                    int index = definition.NeutralMines[i];
                    int col = index % width;
                    int row = index / width;

                    if (constraints.IsNeutralMineForbidden(col, row))
                    {
                        failureReason = "[11] 시드 " + seed + ": 광산 금지 구역에 중립 광산이 놓였다(" +
                            col + "," + row + ").";
                        return false;
                    }

                    if (MapBandTable.IsWithinCentralBand(col, row)) bandMineCount++;
                }

                if (bandMineCount > 2)
                {
                    failureReason = "[11] 시드 " + seed + ": 높이 단계 18~24 안의 중립 광산이 " +
                        bandMineCount + "개다(대응쌍 1쌍 = 2개까지).";
                    return false;
                }

                if (definition.NeutralMines.Count != mineCount)
                {
                    failureReason = "[11] 시드 " + seed + ": 중립 광산이 " + mineCount + "개가 아니다(실제 " +
                        definition.NeutralMines.Count + "개).";
                    return false;
                }

                // ── 12. 같은 시드 -> 같은 맵 ────────────────────────────────
                MapGenerationResult again = generator.Generate(request);
                if (!TryCompareDefinitions(definition, again.Definition, out string diffReason))
                {
                    failureReason = "[12] 시드 " + seed + " 를 다시 만들었더니 다른 맵이 나왔다: " + diffReason;
                    return false;
                }
            }

            // ── 13. 통계 ────────────────────────────────────────────────────
            if (!TryCompareCounts(lengthCounts, SelfCheckExpectedLengthCountValues, "덩어리 길이",
                    out failureReason))
            {
                return false;
            }

            if (!TryCompareCounts(maxWidthCounts, SelfCheckExpectedMaxWidthCountValues, "최대 폭",
                    out failureReason))
            {
                return false;
            }

            if (!TryCompareCounts(shapeCounts, SelfCheckExpectedShapeCountValues, "덩어리 모양",
                    out failureReason))
            {
                return false;
            }

            if (blockedTotal != SelfCheckExpectedBlockedTotal)
            {
                failureReason = "[13] 차단 타일 총합이 기대값 " + SelfCheckExpectedBlockedTotal +
                    " 과 다르다(실제 " + blockedTotal + ").";
                return false;
            }

            if (noBuildTotal != SelfCheckExpectedNoBuildTotal)
            {
                failureReason = "[13] 건설 불가 타일 총합이 기대값 " + SelfCheckExpectedNoBuildTotal +
                    " 과 다르다(실제 " + noBuildTotal + ").";
                return false;
            }

            // ── 14. 음성 대조 ───────────────────────────────────────────────
            //
            // 「통과해야 할 것」만 넣어 보는 검사는 무엇이든 통과시키는 검사와 구별되지
            // 않는다. 인접 단계 폭 차이를 일부러 4 로 만든 프로파일을 같은 문으로 넣는다.
            var brokenProfile = new[] { 5, 1, 1, 1, 1, 1 };
            if (TryVerifyMassWidthProfile(brokenProfile, 5, out _))
            {
                failureReason = "[14] 음성 대조 실패 — 인접 폭 차이가 4 인 프로파일이 통과했다.";
                return false;
            }

            // 중앙선이 최대 폭이 아닌 프로파일도 걸러져야 한다.
            var notMaxAtCenter = new[] { 3, 3, 3, 1, 1, 1 };
            if (TryVerifyMassWidthProfile(notMaxAtCenter, 5, out _))
            {
                failureReason = "[14] 음성 대조 실패 — 중앙선이 최대 폭이 아닌 프로파일이 통과했다.";
                return false;
            }

            // 폭이 짝수인 프로파일도 걸러져야 한다.
            var evenWidth = new[] { 5, 4, 3, 1, 1, 1 };
            if (TryVerifyMassWidthProfile(evenWidth, 5, out _))
            {
                failureReason = "[14] 음성 대조 실패 — 짝수 폭이 섞인 프로파일이 통과했다.";
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
                throw new InvalidOperationException("OuterGenerator 자기 검증 실패: " + failureReason);
            }
        }

        // ====================================================================
        // 자기 검증용 도우미
        // ====================================================================

        /// <summary>
        /// 덩어리가 공동 없이 하나로 이어져 있는지 확인한다.
        /// 육각 인접은 「열이 1 다르고 높이 단계가 1 다른 칸」 또는 「같은 열에서 높이
        /// 단계가 2 다른 칸」이다.
        /// </summary>
        /// <param name="definition">완성된 맵 정의</param>
        /// <param name="unpairedTiles">유형과 무관하게 고정된 짝 없는 칸들</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>하나로 이어져 있으면 true</returns>
        private static bool TryVerifyMassConnectivity(MapDefinition definition, HashSet<int> unpairedTiles,
            out string failureReason)
        {
            var mass = new HashSet<int>();

            for (int index = 0; index < definition.TileCount; index++)
            {
                if (unpairedTiles.Contains(index)) continue;
                if (definition.Tiles[index] == TileKind.Blocked) mass.Add(index);
            }

            if (mass.Count == 0)
            {
                failureReason = "중앙 덩어리가 하나도 만들어지지 않았다.";
                return false;
            }

            int start = -1;
            foreach (int index in mass)
            {
                start = index;
                break;
            }

            var visited = new HashSet<int> { start };
            var frontier = new Stack<int>();
            frontier.Push(start);

            var neighborBuffer = new List<int>(6);

            while (frontier.Count > 0)
            {
                int current = frontier.Pop();
                CollectHexNeighbors(definition, current % definition.Width, current / definition.Width,
                    neighborBuffer);

                for (int i = 0; i < neighborBuffer.Count; i++)
                {
                    int next = neighborBuffer[i];
                    if (!mass.Contains(next) || visited.Contains(next)) continue;

                    visited.Add(next);
                    frontier.Push(next);
                }
            }

            if (visited.Count != mass.Count)
            {
                failureReason = "중앙 덩어리가 " + mass.Count + "칸인데 이어진 것은 " + visited.Count +
                    "칸뿐이다(공동이 있다).";
                return false;
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 좌·우 통로가 위아래로 끝까지 이어지는지 확인한다.
        /// 최대 폭이 5 를 넘지 않으므로 0~2열과 8~10열은 어느 높이에서도 막히면 안 된다.
        /// </summary>
        /// <param name="definition">완성된 맵 정의</param>
        /// <param name="unpairedTiles">유형과 무관하게 고정된 짝 없는 칸들</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>끝까지 이어지면 true</returns>
        private static bool TryVerifyOuterCorridorsContinuous(MapDefinition definition,
            HashSet<int> unpairedTiles, out string failureReason)
        {
            for (int row = 0; row < definition.Height; row++)
            {
                for (int col = 0; col < definition.Width; col++)
                {
                    int colDistance = col - MapBandTable.CenterCol;
                    if (colDistance < 0) colDistance = -colDistance;

                    // 가운데 열에서 3열 이상 떨어진 열은 통로에 해당한다.
                    if (colDistance < 3) continue;

                    int index = row * definition.Width + col;
                    if (unpairedTiles.Contains(index)) continue;

                    if (definition.Tiles[index] == TileKind.Blocked)
                    {
                        failureReason = "외곽 통로가 막혔다(" + col + "," + row + ").";
                        return false;
                    }
                }
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 육각 격자에서 그 칸에 인접한 칸들의 row-major 인덱스를 모은다.
        /// </summary>
        /// <param name="definition">맵 정의(격자 크기를 안다)</param>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <param name="results">결과를 담을 목록(안에서 먼저 비운다)</param>
        private static void CollectHexNeighbors(MapDefinition definition, int col, int row, List<int> results)
        {
            results.Clear();

            int step = MapBandTable.GetHeightStep(col, row);

            // 같은 열에서 위아래(높이 단계 2 차이), 그리고 좌우 열의 위아래(1 차이).
            AddNeighbor(definition, col, step - 2, results);
            AddNeighbor(definition, col, step + 2, results);
            AddNeighbor(definition, col - 1, step - 1, results);
            AddNeighbor(definition, col - 1, step + 1, results);
            AddNeighbor(definition, col + 1, step - 1, results);
            AddNeighbor(definition, col + 1, step + 1, results);
        }

        /// <summary> 열과 높이 단계로 지정한 칸이 격자 안이면 결과에 넣는다. </summary>
        /// <param name="definition">맵 정의</param>
        /// <param name="col">열</param>
        /// <param name="heightStep">높이 단계</param>
        /// <param name="results">결과 목록</param>
        private static void AddNeighbor(MapDefinition definition, int col, int heightStep, List<int> results)
        {
            if (col < 0 || col >= definition.Width) return;

            // 높이 단계의 홀짝은 열의 홀짝과 반드시 같아야 한다. 다르면 그 자리에는 칸이 없다.
            if ((heightStep & 1) != (col & 1)) return;

            int row = (heightStep - (col & 1)) / 2;
            if (row < 0 || row >= definition.Height) return;

            results.Add(row * definition.Width + col);
        }

        /// <summary> 정수 목록에서 값의 위치를 찾는다(없으면 -1). </summary>
        /// <param name="values">찾을 대상 목록</param>
        /// <param name="value">찾을 값</param>
        /// <returns>위치, 없으면 -1</returns>
        private static int IndexOf(IReadOnlyList<int> values, int value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value) return i;
            }
            return -1;
        }

        /// <summary> 실측 횟수와 기대 횟수를 견준다. </summary>
        /// <param name="actual">실측 횟수</param>
        /// <param name="expected">기대 횟수</param>
        /// <param name="label">실패 사유에 쓸 이름</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>모두 같으면 true</returns>
        private static bool TryCompareCounts(int[] actual, int[] expected, string label, out string failureReason)
        {
            for (int i = 0; i < actual.Length; i++)
            {
                if (actual[i] != expected[i])
                {
                    failureReason = "[13] " + label + " " + i + "번 후보의 등장 횟수가 기대값 " +
                        expected[i] + " 과 다르다(실제 " + actual[i] + ").";
                    return false;
                }
            }

            failureReason = null;
            return true;
        }
    }
}
