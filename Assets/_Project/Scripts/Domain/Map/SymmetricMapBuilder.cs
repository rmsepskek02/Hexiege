// ============================================================================
// SymmetricMapBuilder.cs
// 무작위 맵 후보를 만들 때 "타일 배열을 바꿀 수 있는 유일한 통로".
//
// ─────────────────────────────────────────────────────────────────────────────
// 이 클래스가 왜 필요한가 (초급자용 설명)
// ─────────────────────────────────────────────────────────────────────────────
//   이 게임의 맵은 두 플레이어에게 정확히 공평해야 한다. 그래서 맵의 모든 요소는
//   맵 중앙을 기준으로 180도 돌렸을 때 자기 자신과 겹쳐야 한다(= 회전 대칭).
//
//   맵을 만드는 생성기 5종이 타일 배열을 직접 만지게 두면, 어느 한 곳에서
//   "반대편에도 똑같이 놓기"를 깜빡하는 순간 대칭이 깨진다. 사람이 안 깜빡하기를
//   바라는 대신, 아예 그런 코드를 쓸 수 없게 만드는 것이 이 클래스다.
//
//   생성기는 "여기에 이걸 놓아라" 한 번만 말하고, 반대편 자리에 짝을 놓는 일은
//   이 클래스가 같은 호출 안에서 함께 처리한다. 한쪽만 기록되는 상태 자체가
//   만들어질 수 없다.
//
//   🔴 그렇다고 이 클래스를 "믿고 검증을 건너뛰지" 않는다. 완성된 맵은
//      MapDefinitionValidator 가 builder 내부 사정을 전혀 모른 채 결과만 보고
//      다시 검사한다(그것은 이번 단계 범위 밖의 후속 작업이다).
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔴 180도 회전이란 무엇인가 — 규칙 1이 단일 소스다
// ─────────────────────────────────────────────────────────────────────────────
//   GameSystemRules/GameSystemRules_RandomMap.md 규칙 1 이 정의를 갖는다.
//
//       큐브 좌표:  p -> 2C - p            (C = offset (5,10) 의 큐브 좌표)
//
//       offset 표현:
//           col -> 10 - col
//           row -> 21 - row      (col 이 짝수일 때)
//           row -> 20 - row      (col 이 홀수일 때)
//
//   🔴 짝수 열과 홀수 열의 식이 다르다는 점이 이 파일에서 가장 중요한 부분이다.
//      왜 그런지는 아래 RotateCoord 의 주석에서 그림과 함께 설명한다.
//
//   두 축 번호를 각각 뒤집는 옛 해석(열도 뒤집고 행도 뒤집는 방식)은 2026-08-26 에
//   폐기됐다. 그것은 회전이 아니라 뒤집기여서 헥스 타일의 이웃 관계가 보존되지
//   않고, 그래서 "타일 상태가 대칭이면 양측 이동 그래프도 대칭"이라는 공정성
//   논증이 무너진다. 이 파일은 폐기된 식을 계산에도 설명에도 쓰지 않는다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 이 클래스가 지키는 계약 (TechnicalDesignDocument.md 「SymmetricMapBuilder 생성 경계」)
// ─────────────────────────────────────────────────────────────────────────────
//   - SetPair 계열은 중심 (5,10) 을 입력으로 받지 않는다. 중심은 SetCenter 계열 전용이다.
//   - SetPair 계열은 "짝 없는 6칸"을 입력으로 받지 않는다(아래 설명).
//   - SetPair 계열은 원본 자리와 회전 자리 두 곳에 한 번에 기록한다.
//   - TileKind, 광산, 장식 등 모든 정적 생성 상태가 이 API 를 통과한다.
//   - 생성기에게 타일 배열이나 "회전 상대만 따로 바꾸는" API 를 노출하지 않는다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 짝 없는 6칸 (규칙 10)
// ─────────────────────────────────────────────────────────────────────────────
//   짝수 열 0행 6칸 (0,0) (2,0) (4,0) (6,0) (8,0) (10,0) 은 회전 결과가 21행이라
//   격자 밖으로 나간다. 즉 짝이 될 상대가 아예 없다. 그래서 이 6칸은 "생성해서
//   채우는 칸"이 아니라 항상 막힌 타일(TileKind.Blocked)로 고정한다.
//   231칸 중 실제로 플레이에 쓰는 칸은 225칸이 된다.
//
//   ⚠️ 이 6칸을 코드에 좌표 배열로 박아 두지 않는다. "회전 결과가 격자 밖인 칸"
//      이라는 정의 그대로 생성자에서 계산한다. 격자 크기가 바뀌어도 따라온다.
//
// 근거 문서:
//   GameSystemRules/GameSystemRules_RandomMap.md 규칙 1 · 규칙 10 · 7장 용어 정의
//   TechnicalDesignDocument.md 「SymmetricMapBuilder 생성 경계」
//   (두 문서가 어긋나면 규칙 문서가 옳다.)
//
// Domain 레이어 — 순수 C#, Unity/Core 의존 없음(양쪽 다 참조하지 않는다).
// ============================================================================

using System;
using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 회전 대칭을 구조적으로 강제하는 맵 후보 생성기. 타일·광산·장식 등
    /// 모든 정적 생성 상태는 이 클래스의 API 를 통과해야만 기록된다.
    /// </summary>
    public sealed class SymmetricMapBuilder
    {
        // ====================================================================
        // 상수
        // ====================================================================

        /// <summary>
        /// 장식물의 회전 단계 총 개수. 헥스는 60도씩 6방향이므로 6단계로 둔다
        /// (유닛 방향 표현 E/NE/NW/W/SW/SE 와 같은 분해능).
        /// 180도는 그 절반인 3단계이므로 대응 규칙은 (단계 + 3) % 6 이 된다.
        ///
        /// ⚠️ 이 값은 규칙 문서·TDD 어디에도 정해져 있지 않아 이 구현에서 정한 것이다.
        ///    장식 에셋과 맵 테마가 확정되면(규칙 15) 그때 실제 단계 수로 다시 정한다.
        ///    최초 구현의 생성기는 장식을 하나도 만들지 않으므로 지금은 실제로 쓰이지 않는다.
        /// </summary>
        public const int DecorationRotationStepCount = 6;

        // ====================================================================
        // 상태
        // ====================================================================

        // 만들어지는 중인 맵. 🔴 절대 프로퍼티로 외부에 내보내지 않는다.
        // 내보내는 순간 생성기가 타일 배열을 직접 만질 수 있게 되어 이 클래스의
        // 존재 이유가 사라진다. 완성본은 Build() 로만 나간다.
        private readonly MapDefinition _definition;

        // 회전 상대가 없는 칸들의 row-major 인덱스(생성자에서 계산).
        private readonly int[] _unpairedTileIndices;

        // Build() 를 부른 뒤에는 더 이상 기록할 수 없다.
        private bool _closed;

        /// <summary> 격자 가로 타일 수. </summary>
        public int Width { get; }

        /// <summary> 격자 세로 타일 수. </summary>
        public int Height { get; }

        /// <summary> 회전 중심의 열. 11칸이면 5. </summary>
        public int CenterCol { get; }

        /// <summary> 회전 중심의 행. 21칸이면 10. </summary>
        public int CenterRow { get; }

        /// <summary>
        /// 회전 상대가 없어 항상 막힌 타일로 고정된 칸들의 row-major 인덱스.
        /// 읽기 전용이며, 검증기가 "이 칸들이 정말 막혀 있는가"를 볼 때 쓴다.
        /// </summary>
        public IReadOnlyList<int> UnpairedTileIndices => _unpairedTileIndices;

        // ====================================================================
        // 생성
        // ====================================================================

        /// <summary>
        /// 규격(11x21)대로 빈 후보를 만든다. 모든 타일은 TileKind.Normal 로 시작하고,
        /// 회전 상대가 없는 칸만 즉시 TileKind.Blocked 로 고정 기록된다.
        /// </summary>
        public SymmetricMapBuilder()
        {
            _definition = new MapDefinition(MapDefinition.DefaultWidth, MapDefinition.DefaultHeight);

            Width = _definition.Width;
            Height = _definition.Height;

            // 회전 중심은 격자의 한가운데 칸이다. 가로·세로가 홀수여야 "가운데 칸"이
            // 정확히 하나 존재한다. 11x21 은 두 값 모두 홀수다.
            if ((Width & 1) == 0 || (Height & 1) == 0)
            {
                throw new InvalidOperationException(
                    "회전 대칭 맵의 가로·세로는 홀수여야 한다(현재 " + Width + "x" + Height + ").");
            }

            CenterCol = (Width - 1) / 2;
            CenterRow = (Height - 1) / 2;

            // ⚠️ 아래 RotateCoord 의 "짧은 식"(행 -> Height - row / Height-1 - row)은
            //    중심 열이 홀수일 때만 성립한다. 11칸이면 중심 열이 5라 성립한다.
            //    가로 크기를 바꿔 이 전제가 깨지면 자기 검증 2번(큐브 정의 대조)이
            //    231칸 전수에서 즉시 어긋나므로 조용히 넘어가지 않는다.
            if ((CenterCol & 1) == 0)
            {
                throw new InvalidOperationException(
                    "중심 열이 짝수라 규칙 1의 offset 회전식을 그대로 쓸 수 없다(중심 열 " + CenterCol + ").");
            }

            _unpairedTileIndices = FixUnpairedTiles();
        }

        // ====================================================================
        // 1. 좌표 회전 — RotateCoord
        // ====================================================================

        /// <summary>
        /// offset 좌표 (col, row) 를 180도 회전한 좌표로 바꾼다. 규칙 1이 단일 소스다.
        /// </summary>
        /// <param name="col">열(0 ~ width-1)</param>
        /// <param name="row">행(0 ~ height-1)</param>
        /// <param name="width">격자 가로 타일 수</param>
        /// <param name="height">격자 세로 타일 수</param>
        /// <param name="rotatedCol">회전한 열</param>
        /// <param name="rotatedRow">회전한 행(격자 밖 값이 나올 수 있다 — 짝 없는 칸)</param>
        // ────────────────────────────────────────────────────────────────────
        // 🔴 왜 짝수 열과 홀수 열의 식이 다른가
        // ────────────────────────────────────────────────────────────────────
        //   이 맵은 FlatTop(육각형의 뾰족한 부분이 좌우로 향한) 격자라서
        //   홀수 열이 반 칸 아래로 내려가 있다. 그림으로 보면 이렇다.
        //
        //        열:   0     1     2     3     4
        //             ┌─┐         ┌─┐         ┌─┐
        //     0행 ->  │ │   ┌─┐   │ │   ┌─┐   │ │   <- 짝수 열은 여기서 시작
        //             └─┘   │ │   └─┘   │ │   └─┘
        //             ┌─┐   └─┘   ┌─┐   └─┘   ┌─┐   <- 홀수 열은 반 칸 아래에서 시작
        //     1행 ->  │ │   ┌─┐   │ │   ┌─┐   │ │
        //             └─┘   │ │   └─┘   │ │   └─┘
        //
        //   즉 "같은 행"이라도 짝수 열과 홀수 열은 실제 높이가 반 칸 다르다.
        //   그래서 행 번호 하나만 뒤집는 식으로는 회전을 표현할 수 없다.
        //
        //   규칙 문서는 이 문제를 「높이 단계」라는 용어로 푼다. 높이 단계는
        //   실제 높이가 같은 타일끼리 묶은 진짜 가로선 번호이며
        //
        //       높이 단계 = 행 x 2 + (홀수 열이면 1)
        //
        //   로 구한다. 180도 회전은 이 높이 단계를 중앙선 기준으로 뒤집는 것이고,
        //   식은 단 하나다.
        //
        //       높이 단계 L  ->  (세로 크기 x 2) - L        (21x2 = 42 이므로 L -> 42 - L)
        //
        //   이 하나의 식을 다시 "행 번호"로 옮겨 적으면 열의 홀짝에 따라 두 줄이 된다.
        //
        //       짝수 열: L = 행 x 2       -> 42 - L = 42 - 행 x 2       -> 행 = 21 - 행
        //       홀수 열: L = 행 x 2 + 1   -> 42 - L = 41 - 행 x 2       -> 행 = 20 - 행
        //
        //   두 줄로 갈라진 이유는 규칙이 두 개여서가 아니라, "행"이라는 표현이
        //   높이를 정확히 나타내지 못하기 때문이다.
        //
        //   열은 반대로 홀짝을 따지지 않는다. 열 방향으로는 시프트가 없기 때문이다.
        //   또한 (가로-1) 에서 빼기 때문에 회전해도 열의 홀짝은 그대로 유지된다
        //   (10 - 짝수 = 짝수, 10 - 홀수 = 홀수). 그래서 위 두 줄 중 어느 쪽을
        //   적용할지는 회전 전후가 항상 일치한다.
        // ────────────────────────────────────────────────────────────────────
        public static void RotateCoord(int col, int row, int width, int height,
            out int rotatedCol, out int rotatedRow)
        {
            rotatedCol = (width - 1) - col;
            rotatedRow = ((col & 1) == 0) ? (height - row) : ((height - 1) - row);
        }

        /// <summary> 이 builder 의 격자 크기로 좌표를 180도 회전한다. </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <param name="rotatedCol">회전한 열</param>
        /// <param name="rotatedRow">회전한 행</param>
        public void RotateCoord(int col, int row, out int rotatedCol, out int rotatedRow)
        {
            RotateCoord(col, row, Width, Height, out rotatedCol, out rotatedRow);
        }

        /// <summary> 좌표가 격자 안인지 확인한다. </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <param name="width">격자 가로</param>
        /// <param name="height">격자 세로</param>
        /// <returns>격자 안이면 true</returns>
        public static bool IsInsideGrid(int col, int row, int width, int height)
        {
            return col >= 0 && col < width && row >= 0 && row < height;
        }

        /// <summary> 좌표가 이 격자 안인지 확인한다. </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <returns>격자 안이면 true</returns>
        public bool IsInsideGrid(int col, int row) => IsInsideGrid(col, row, Width, Height);

        /// <summary> 이 좌표가 회전 중심인지 확인한다. </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <returns>중심이면 true</returns>
        public bool IsCenter(int col, int row) => col == CenterCol && row == CenterRow;

        /// <summary>
        /// 이 좌표에 회전 상대가 존재하는지 확인한다.
        /// 회전 결과가 격자 밖으로 나가면 상대가 없는 칸(짝 없는 6칸)이다.
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <returns>상대가 있으면 true</returns>
        public bool HasRotationPartner(int col, int row)
        {
            RotateCoord(col, row, out int rc, out int rr);
            return IsInsideGrid(rc, rr);
        }

        // ====================================================================
        // 2. 상태 회전 — RotateState
        // ====================================================================
        //
        // 🔴 좌표를 옮기는 연산(RotateCoord)과 상태를 바꾸는 연산(RotateState)은
        //    이름을 나눠 쓴다. 예전에는 둘 다 같은 이름이어서, 컴파일러는 입력
        //    타입으로 구분해도 코드를 읽는 사람은 구분할 수 없었다.
        //    같은 이름으로 되돌리지 않는다.

        /// <summary>
        /// 팀 전용 상태의 180도 대응. 회전하면 Blue 진영 자리는 Red 진영 자리가 된다.
        /// 중립은 진영이 없으므로 그대로 둔다.
        /// </summary>
        /// <param name="team">원본 팀</param>
        /// <returns>회전 자리에 놓일 팀</returns>
        public static TeamId RotateState(TeamId team)
        {
            switch (team)
            {
                case TeamId.Blue: return TeamId.Red;
                case TeamId.Red: return TeamId.Blue;
                default: return TeamId.Neutral;
            }
        }

        /// <summary>
        /// 지형 종류의 180도 대응. 지형에는 팀 개념이 없으므로 그대로 돌려준다.
        /// (팀 전용 지형이 생기면 여기에서 대응시킨다. 지금 이 메서드가 있는 이유는
        ///  "모든 상태는 RotateState 를 거쳐 기록된다"는 규칙을 예외 없이 지키기 위해서다.)
        /// </summary>
        /// <param name="kind">원본 지형 종류</param>
        /// <returns>회전 자리에 놓일 지형 종류</returns>
        public static TileKind RotateState(TileKind kind) => kind;

        /// <summary>
        /// 장식물의 회전 단계 ID 를 180도 대응 값으로 바꾼다.
        /// 6단계(60도 간격) 기준이므로 절반인 3단계를 더한다.
        /// 두 번 적용하면 원래 값으로 돌아온다.
        /// </summary>
        /// <param name="rotationStepId">원본 회전 단계 ID</param>
        /// <returns>180도 대응 회전 단계 ID(0 ~ 5)</returns>
        public static int RotateRotationStepId(int rotationStepId)
        {
            // 음수 ID 가 들어와도 0~5 범위로 정규화한다.
            int normalized = rotationStepId % DecorationRotationStepCount;
            if (normalized < 0) normalized += DecorationRotationStepCount;

            return (normalized + DecorationRotationStepCount / 2) % DecorationRotationStepCount;
        }

        /// <summary>
        /// 장식물 하나를 회전 자리에 놓을 형태로 바꾼다.
        /// 위치는 호출자가 계산한 회전 자리 인덱스를 쓰고, 회전 단계 ID 만 대응 값으로 바꾼다.
        /// 종류·재질 변형·크기 단계는 회전과 무관하므로 그대로 유지한다.
        /// </summary>
        /// <param name="decoration">원본 장식물</param>
        /// <param name="rotatedTileIndex">회전 자리의 row-major 인덱스</param>
        /// <returns>회전 자리에 놓을 장식물</returns>
        public static DecorationDefinition RotateState(DecorationDefinition decoration, int rotatedTileIndex)
        {
            return new DecorationDefinition(
                rotatedTileIndex,
                decoration.TypeId,
                decoration.MaterialVariantId,
                decoration.ScaleStepId,
                RotateRotationStepId(decoration.RotationStepId));
        }

        // ====================================================================
        // 3. 기록 API — 원본과 회전 자리에 함께 쓴다
        // ====================================================================

        /// <summary>
        /// 타일 종류를 대응쌍 두 자리에 함께 기록한다.
        /// 중심 좌표와 짝 없는 칸은 받지 않는다(각각 SetCenter / 고정값 담당).
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <param name="kind">기록할 지형 종류</param>
        public void SetPair(int col, int row, TileKind kind)
        {
            ResolvePair(col, row, out int index, out int rotatedIndex);

            // 두 줄이 언제나 함께 실행된다. 사이에 조건문이나 return 이 없다.
            _definition.Tiles[index] = kind;
            _definition.Tiles[rotatedIndex] = RotateState(kind);
        }

        /// <summary>
        /// 회전 중심 타일의 종류를 기록한다. 중심은 회전해도 자기 자신이므로 한 자리만 쓴다.
        /// </summary>
        /// <param name="kind">기록할 지형 종류</param>
        public void SetCenter(TileKind kind)
        {
            EnsureOpen();
            _definition.Tiles[_definition.ToIndex(CenterCol, CenterRow)] = kind;
        }

        /// <summary>
        /// 성을 대응쌍 두 자리에 기록한다. 지정한 자리에 team 이, 회전 자리에는
        /// 반대 팀이 자동으로 놓인다.
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <param name="team">이 자리에 놓을 팀(Blue 또는 Red)</param>
        public void SetCastlePair(int col, int row, TeamId team)
        {
            EnsureTeamSide(team, nameof(team));
            ResolvePair(col, row, out int index, out int rotatedIndex);

            _definition.Castles.Add(new MapObjectPlacement(index, team));
            _definition.Castles.Add(new MapObjectPlacement(rotatedIndex, RotateState(team)));
        }

        /// <summary>
        /// 팀 시작 광산을 대응쌍 두 자리에 기록한다. 회전 자리에는 반대 팀 것이 놓인다.
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <param name="team">이 자리에 놓을 팀(Blue 또는 Red)</param>
        public void SetStartingMinePair(int col, int row, TeamId team)
        {
            EnsureTeamSide(team, nameof(team));
            ResolvePair(col, row, out int index, out int rotatedIndex);

            _definition.StartingMines.Add(new MapObjectPlacement(index, team));
            _definition.StartingMines.Add(new MapObjectPlacement(rotatedIndex, RotateState(team)));
        }

        /// <summary>
        /// 중립 광산을 대응쌍 두 자리에 기록한다. 중립 광산에는 팀이 없으므로
        /// 상태 변환 없이 위치만 회전한다.
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        public void SetNeutralMinePair(int col, int row)
        {
            ResolvePair(col, row, out int index, out int rotatedIndex);

            AddNeutralMine(index);
            AddNeutralMine(rotatedIndex);
        }

        /// <summary>
        /// 회전 중심에 단독 중립 광산을 기록한다. 중립 광산 개수가 홀수일 때 쓰는
        /// 유일한 자리이며, 중심은 회전해도 자기 자신이라 짝이 필요 없다.
        /// </summary>
        public void SetCenterNeutralMine()
        {
            EnsureOpen();
            AddNeutralMine(_definition.ToIndex(CenterCol, CenterRow));
        }

        /// <summary>
        /// 장식물을 대응쌍 두 자리에 기록한다. 회전 자리에는 위치뿐 아니라
        /// 회전 단계 ID 도 180도 대응 값으로 바뀐 장식물이 놓인다.
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <param name="typeId">장식물 종류 ID</param>
        /// <param name="materialVariantId">재질 변형 ID</param>
        /// <param name="scaleStepId">크기 단계 ID</param>
        /// <param name="rotationStepId">회전 단계 ID</param>
        public void SetDecorationPair(int col, int row, int typeId, int materialVariantId,
            int scaleStepId, int rotationStepId)
        {
            ResolvePair(col, row, out int index, out int rotatedIndex);

            var original = new DecorationDefinition(index, typeId, materialVariantId, scaleStepId, rotationStepId);
            _definition.Decorations.Add(original);
            _definition.Decorations.Add(RotateState(original, rotatedIndex));
        }

        // ====================================================================
        // 4. 읽기 / 완성
        // ====================================================================

        /// <summary>
        /// 현재 기록된 타일 종류를 읽는다. 생성기가 "여기는 이미 막혔는가"를 확인할 때 쓴다.
        /// 읽기 전용이므로 이 경로로는 대칭을 깨뜨릴 수 없다.
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <returns>해당 칸의 지형 종류</returns>
        public TileKind GetTile(int col, int row)
        {
            EnsureInsideGrid(col, row);
            return _definition.Tiles[_definition.ToIndex(col, row)];
        }

        /// <summary>
        /// 완성된 맵 정의를 넘겨주고 이 builder 를 닫는다. 닫힌 뒤에는 어떤 기록 API 도
        /// 받지 않으므로 "완성본을 넘긴 뒤 몰래 더 고치는" 경로가 생기지 않는다.
        /// 목록은 canonical 순서로 정렬해서 넘긴다(같은 맵이 순서 차이로 다른 해시를 갖지 않게).
        /// </summary>
        /// <returns>완성된 맵 정의</returns>
        public MapDefinition Build()
        {
            EnsureOpen();
            _closed = true;

            _definition.SortCanonical();
            return _definition;
        }

        // ====================================================================
        // 내부 도우미
        // ====================================================================

        /// <summary>
        /// 회전 상대가 없는 칸을 찾아 항상 막힌 타일로 고정하고, 그 인덱스 목록을 돌려준다.
        /// 좌표를 배열로 박아 두지 않고 "회전 결과가 격자 밖인가"라는 정의로 직접 찾는다.
        /// </summary>
        /// <returns>고정된 칸들의 row-major 인덱스</returns>
        private int[] FixUnpairedTiles()
        {
            var found = new List<int>();

            for (int row = 0; row < Height; row++)
            {
                for (int col = 0; col < Width; col++)
                {
                    if (HasRotationPartner(col, row)) continue;

                    int index = _definition.ToIndex(col, row);
                    _definition.Tiles[index] = TileKind.Blocked;
                    found.Add(index);
                }
            }

            return found.ToArray();
        }

        /// <summary> 대응쌍 기록의 사전 조건을 모두 확인하고 두 자리의 인덱스를 계산한다. </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <param name="index">원본 자리의 row-major 인덱스</param>
        /// <param name="rotatedIndex">회전 자리의 row-major 인덱스</param>
        private void ResolvePair(int col, int row, out int index, out int rotatedIndex)
        {
            EnsureOpen();
            EnsureInsideGrid(col, row);

            if (IsCenter(col, row))
            {
                throw new ArgumentException(
                    "회전 중심 (" + CenterCol + "," + CenterRow + ") 은 대응쌍이 아니다. 중심 전용 API 를 쓸 것.",
                    nameof(col));
            }

            RotateCoord(col, row, out int rotatedCol, out int rotatedRow);

            if (!IsInsideGrid(rotatedCol, rotatedRow))
            {
                throw new ArgumentException(
                    "(" + col + "," + row + ") 은 회전 상대가 격자 밖이라 대응쌍을 만들 수 없다. " +
                    "이 칸은 항상 막힌 타일로 고정된다.",
                    nameof(col));
            }

            index = _definition.ToIndex(col, row);
            rotatedIndex = _definition.ToIndex(rotatedCol, rotatedRow);
        }

        /// <summary> 중립 광산 인덱스를 중복 없이 추가한다. </summary>
        /// <param name="tileIndex">광산이 놓일 타일 인덱스</param>
        private void AddNeutralMine(int tileIndex)
        {
            if (_definition.NeutralMines.Contains(tileIndex))
            {
                throw new InvalidOperationException(
                    "같은 타일에 중립 광산을 두 번 기록했다(인덱스 " + tileIndex + ").");
            }

            _definition.NeutralMines.Add(tileIndex);
        }

        /// <summary> Build() 이후의 기록 시도를 막는다. </summary>
        private void EnsureOpen()
        {
            if (_closed)
            {
                throw new InvalidOperationException("Build() 로 완성된 뒤에는 더 이상 기록할 수 없다.");
            }
        }

        /// <summary> 격자 밖 좌표를 막는다. </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        private void EnsureInsideGrid(int col, int row)
        {
            if (!IsInsideGrid(col, row))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(col),
                    "격자 밖 좌표 (" + col + "," + row + "). 허용 범위는 0~" + (Width - 1) + " / 0~" + (Height - 1) + ".");
            }
        }

        /// <summary> 팀 전용 오브젝트에 중립이 들어오는 것을 막는다. </summary>
        /// <param name="team">확인할 팀</param>
        /// <param name="parameterName">예외 메시지에 쓸 인자 이름</param>
        private static void EnsureTeamSide(TeamId team, string parameterName)
        {
            if (team != TeamId.Blue && team != TeamId.Red)
            {
                throw new ArgumentException("팀 전용 오브젝트에는 Blue 또는 Red 만 쓸 수 있다.", parameterName);
            }
        }

        // ====================================================================
        // 자기 검증 (MapRandomStreams 와 같은 형태)
        // ====================================================================
        //
        // 이 프로젝트에는 유닛 테스트 어셈블리가 없다. 그래서 "이 계산이 규칙대로인가"를
        // 코드 안에서 직접 확인한다. 실패하면 구현이 규칙을 벗어났다는 뜻이다.

        /// <summary>
        /// 회전식·계약·초기화가 규칙대로인지 전수 확인한다. 실패하면 사유를 채우고 false.
        /// (테스트 어셈블리가 생기면 이 메서드를 그대로 호출하면 되도록 Conditional 을 붙이지 않았다.)
        /// </summary>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>모두 통과하면 true</returns>
        public static bool TryRunSelfCheck(out string failureReason)
        {
            var builder = new SymmetricMapBuilder();
            int width = builder.Width;
            int height = builder.Height;

            // ── 1. 격자 규격 ────────────────────────────────────────────────
            if (width != 11 || height != 21)
            {
                failureReason = "격자 규격이 11x21 이 아니다(" + width + "x" + height + ").";
                return false;
            }
            if (builder.CenterCol != 5 || builder.CenterRow != 10)
            {
                failureReason = "회전 중심이 (5,10) 이 아니다(" +
                                builder.CenterCol + "," + builder.CenterRow + ").";
                return false;
            }

            // ── 2. 규칙 1의 큐브 정의와 같은가 (231칸 전수) ─────────────────
            // 큐브 좌표에서 회전은 p -> 2C - p 다. offset 짧은 식이 그것과 같은지
            // 모든 칸에서 대조한다. 이 검사가 통과하면 "짝수/홀수 열 두 줄짜리 식"이
            // 실제로 참 180도 회전임이 확인된 것이다.
            HexCoord center = HexGrid.OffsetToCube(builder.CenterCol, builder.CenterRow, HexOrientation.FlatTop);
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    RotateCoord(col, row, width, height, out int rc, out int rr);

                    HexCoord source = HexGrid.OffsetToCube(col, row, HexOrientation.FlatTop);
                    HexCoord rotated = HexGrid.OffsetToCube(rc, rr, HexOrientation.FlatTop);
                    var expected = new HexCoord(2 * center.Q - source.Q, 2 * center.R - source.R);

                    if (rotated != expected)
                    {
                        failureReason = "(" + col + "," + row + ") 회전이 큐브 정의와 다르다: 실제 " +
                                        rotated + " / 기대 " + expected;
                        return false;
                    }
                }
            }

            // ── 3. 자기 역함수인가 ──────────────────────────────────────────
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    RotateCoord(col, row, width, height, out int rc, out int rr);
                    RotateCoord(rc, rr, width, height, out int bc, out int br);

                    if (bc != col || br != row)
                    {
                        failureReason = "(" + col + "," + row + ") 을 두 번 회전했더니 (" +
                                        bc + "," + br + ") 이 됐다.";
                        return false;
                    }
                }
            }

            // ── 4. 중심은 고정점인가 ────────────────────────────────────────
            {
                RotateCoord(builder.CenterCol, builder.CenterRow, width, height, out int cc, out int cr);
                if (cc != builder.CenterCol || cr != builder.CenterRow)
                {
                    failureReason = "중심이 고정점이 아니다: (" + cc + "," + cr + ").";
                    return false;
                }
            }

            // ── 5. 높이 단계 대응 (L + 회전한 L == 세로 x 2) ────────────────
            // 규칙 문서 7장이 정의한 「높이 단계」로도 같은 회전이 성립하는지 본다.
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    if (!builder.HasRotationPartner(col, row)) continue;

                    RotateCoord(col, row, width, height, out int rc, out int rr);
                    int step = row * 2 + (col & 1);
                    int rotatedStep = rr * 2 + (rc & 1);

                    if (step + rotatedStep != height * 2)
                    {
                        failureReason = "높이 단계 대응이 어긋났다(" + col + "," + row + "): " +
                                        step + " + " + rotatedStep + " != " + (height * 2);
                        return false;
                    }
                }
            }

            // ── 6. 🔴 인접성 보존 (공정성 논증이 걸린 지점) ─────────────────
            // 서로 붙어 있는 두 칸을 회전시켰을 때도 여전히 붙어 있어야 한다.
            // 이것이 보장돼야 "타일 상태가 대칭이면 양측 이동 그래프도 대칭"이 성립한다.
            int checkedPairs = 0;
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    if (!builder.HasRotationPartner(col, row)) continue;

                    HexCoord source = HexGrid.OffsetToCube(col, row, HexOrientation.FlatTop);

                    for (int d = 0; d < HexDirectionExtensions.Count; d++)
                    {
                        HexCoord neighbor = ((HexDirection)d).Neighbor(source);

                        // 큐브 좌표를 다시 offset 으로 되돌린다(even-q 역변환).
                        // 열이 격자 밖이면 되돌릴 것도 없이 건너뛴다.
                        if (neighbor.Q < 0 || neighbor.Q >= width) continue;

                        int nCol = neighbor.Q;
                        int nRow = neighbor.R + (nCol - (nCol & 1)) / 2;
                        if (!builder.IsInsideGrid(nCol, nRow)) continue;
                        if (!builder.HasRotationPartner(nCol, nRow)) continue;

                        RotateCoord(col, row, width, height, out int rc, out int rr);
                        RotateCoord(nCol, nRow, width, height, out int rnc, out int rnr);

                        HexCoord rotatedSource = HexGrid.OffsetToCube(rc, rr, HexOrientation.FlatTop);
                        HexCoord rotatedNeighbor = HexGrid.OffsetToCube(rnc, rnr, HexOrientation.FlatTop);

                        if (HexCoord.Distance(rotatedSource, rotatedNeighbor) != 1)
                        {
                            failureReason = "회전이 인접성을 깨뜨렸다: (" + col + "," + row + ")-(" +
                                            nCol + "," + nRow + ") -> (" + rc + "," + rr + ")-(" +
                                            rnc + "," + rnr + ")";
                            return false;
                        }

                        checkedPairs++;
                    }
                }
            }
            if (checkedPairs == 0)
            {
                failureReason = "인접성 검사가 한 쌍도 확인하지 못했다(검사 코드 자체가 잘못됐다).";
                return false;
            }

            // ── 7. 짝 없는 칸이 정확히 6개이고 전부 막힌 타일인가 ───────────
            int[] expectedUnpaired = { 0, 2, 4, 6, 8, 10 }; // 짝수 열 0행 -> 인덱스 = 열
            if (builder.UnpairedTileIndices.Count != expectedUnpaired.Length)
            {
                failureReason = "짝 없는 칸이 " + builder.UnpairedTileIndices.Count + "개다(기대 6개).";
                return false;
            }
            for (int i = 0; i < expectedUnpaired.Length; i++)
            {
                if (builder.UnpairedTileIndices[i] != expectedUnpaired[i])
                {
                    failureReason = "짝 없는 칸 #" + (i + 1) + " 인덱스 기대 " + expectedUnpaired[i] +
                                    " / 실제 " + builder.UnpairedTileIndices[i];
                    return false;
                }
                if (builder.GetTile(expectedUnpaired[i], 0) != TileKind.Blocked)
                {
                    failureReason = "짝 없는 칸 (" + expectedUnpaired[i] + ",0) 이 막힌 타일로 고정되지 않았다.";
                    return false;
                }
            }

            // ── 8. 거부 계약이 실제로 예외를 던지는가 ───────────────────────
            if (!ExpectRejected(() => builder.SetPair(builder.CenterCol, builder.CenterRow, TileKind.Blocked),
                    "중심 좌표 SetPair", out failureReason)) return false;
            if (!ExpectRejected(() => builder.SetPair(0, 0, TileKind.Normal),
                    "짝 없는 칸 SetPair", out failureReason)) return false;
            if (!ExpectRejected(() => builder.SetPair(10, 0, TileKind.Normal),
                    "짝 없는 칸 SetPair(마지막 열)", out failureReason)) return false;
            if (!ExpectRejected(() => builder.SetPair(-1, 5, TileKind.Normal),
                    "격자 밖 SetPair", out failureReason)) return false;
            if (!ExpectRejected(() => builder.SetNeutralMinePair(builder.CenterCol, builder.CenterRow),
                    "중심 좌표 SetNeutralMinePair", out failureReason)) return false;
            if (!ExpectRejected(() => builder.SetCastlePair(5, 1, TeamId.Neutral),
                    "중립 팀 SetCastlePair", out failureReason)) return false;

            // ── 9. SetPair 한 번에 두 자리가 함께 기록되는가 ────────────────
            // (1,0) 은 홀수 열이라 회전 상대가 (9,20) 이다.
            builder.SetPair(1, 0, TileKind.NoBuild);
            if (builder.GetTile(1, 0) != TileKind.NoBuild)
            {
                failureReason = "SetPair 가 원본 자리에 기록하지 않았다.";
                return false;
            }
            if (builder.GetTile(9, 20) != TileKind.NoBuild)
            {
                failureReason = "SetPair 가 회전 자리 (9,20) 에 기록하지 않았다.";
                return false;
            }

            // ── 10. 상태 회전이 규칙대로인가 ────────────────────────────────
            if (RotateState(TeamId.Blue) != TeamId.Red || RotateState(TeamId.Red) != TeamId.Blue)
            {
                failureReason = "팀 상태 회전이 Blue<->Red 대응이 아니다.";
                return false;
            }
            if (RotateState(TeamId.Neutral) != TeamId.Neutral)
            {
                failureReason = "중립 팀이 회전으로 바뀌었다.";
                return false;
            }
            for (int step = 0; step < DecorationRotationStepCount; step++)
            {
                int once = RotateRotationStepId(step);
                if (once == step)
                {
                    failureReason = "장식 회전 단계 " + step + " 이 180도 회전으로 바뀌지 않았다.";
                    return false;
                }
                if (RotateRotationStepId(once) != step)
                {
                    failureReason = "장식 회전 단계 " + step + " 이 두 번 회전해도 제자리로 오지 않았다.";
                    return false;
                }
            }

            // ── 11. 팀 대응 · 장식 대응이 함께 기록되는가 ───────────────────
            var teamBuilder = new SymmetricMapBuilder();
            teamBuilder.SetCastlePair(5, 19, TeamId.Blue);             // 규칙 1의 Blue 성 자리
            teamBuilder.SetStartingMinePair(3, 19, TeamId.Blue);       // 규칙 2 경우 A
            teamBuilder.SetNeutralMinePair(2, 4);
            teamBuilder.SetCenterNeutralMine();
            teamBuilder.SetDecorationPair(4, 6, 1, 2, 3, 0);
            MapDefinition built = teamBuilder.Build();

            if (built.Castles.Count != 2 || built.StartingMines.Count != 2 ||
                built.NeutralMines.Count != 3 || built.Decorations.Count != 2)
            {
                failureReason = "대응쌍 기록 개수가 맞지 않는다(성 " + built.Castles.Count +
                                " / 시작광산 " + built.StartingMines.Count +
                                " / 중립광산 " + built.NeutralMines.Count +
                                " / 장식 " + built.Decorations.Count + ").";
                return false;
            }

            // Blue 성 (5,19) 의 회전 상대는 (5,1) 이고 그 자리는 Red 여야 한다(규칙 1).
            int redCastleIndex = built.ToIndex(5, 1);
            bool redCastleFound = false;
            for (int i = 0; i < built.Castles.Count; i++)
            {
                if (built.Castles[i].TileIndex == redCastleIndex && built.Castles[i].Team == TeamId.Red)
                {
                    redCastleFound = true;
                }
            }
            if (!redCastleFound)
            {
                failureReason = "Blue 성을 (5,19) 에 놓았는데 (5,1) 에 Red 성이 생기지 않았다.";
                return false;
            }

            // 장식은 위치뿐 아니라 회전 단계 ID 도 대응돼야 한다.
            int rotatedDecoIndex = built.ToIndex(6, 14); // (4,6) 짝수 열 -> (6, 21-6=15)? 아래에서 검산한다
            RotateCoord(4, 6, built.Width, built.Height, out int decoCol, out int decoRow);
            rotatedDecoIndex = built.ToIndex(decoCol, decoRow);
            bool rotatedDecoFound = false;
            for (int i = 0; i < built.Decorations.Count; i++)
            {
                if (built.Decorations[i].TileIndex == rotatedDecoIndex &&
                    built.Decorations[i].RotationStepId == RotateRotationStepId(0))
                {
                    rotatedDecoFound = true;
                }
            }
            if (!rotatedDecoFound)
            {
                failureReason = "장식 회전 자리(" + decoCol + "," + decoRow + ") 기록이 없거나 회전 단계가 대응하지 않는다.";
                return false;
            }

            // ── 12. Build() 이후에는 닫히는가 ───────────────────────────────
            bool closedRejected = false;
            try
            {
                teamBuilder.SetPair(1, 1, TileKind.Normal);
            }
            catch (InvalidOperationException)
            {
                closedRejected = true;
            }
            if (!closedRejected)
            {
                failureReason = "Build() 이후에도 기록이 받아들여졌다.";
                return false;
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 에디터에서만 실행되는 자기 검증. 어긋나면 즉시 예외로 알린다.
        /// 빌드에는 이 호출 자체가 컴파일되지 않는다(Conditional 특성).
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void AssertSelfCheck()
        {
            if (!TryRunSelfCheck(out string reason))
            {
                throw new InvalidOperationException("SymmetricMapBuilder 자기 검증 실패: " + reason);
            }
        }

        /// <summary> 반드시 거부돼야 하는 호출이 실제로 예외를 던지는지 확인한다. </summary>
        /// <param name="call">거부돼야 하는 호출</param>
        /// <param name="label">실패 메시지에 쓸 이름</param>
        /// <param name="failureReason">실패 사유(통과 시 null)</param>
        /// <returns>기대대로 거부되면 true</returns>
        private static bool ExpectRejected(Action call, string label, out string failureReason)
        {
            try
            {
                call();
            }
            catch (ArgumentException)
            {
                failureReason = null;
                return true;
            }

            failureReason = label + " 가(이) 거부되지 않았다.";
            return false;
        }
    }
}
