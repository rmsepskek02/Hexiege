// ============================================================================
// InitialMapStateEvaluator.cs
// "성과 시작 광산만 놓인 맵"에서 경기 시작 순간의 상태를 계산하는 순수 규칙.
//
// ─────────────────────────────────────────────────────────────────────────────
// 이 클래스가 왜 필요한가 (초급자용 설명)
// ─────────────────────────────────────────────────────────────────────────────
//   경기가 시작되면 각 팀은 자기 성과 자기 시작 광산 주변 몇 칸을 이미 자기
//   땅으로 갖고 시작한다. 그리고 그 땅 중에서 "지금 당장 건물을 지을 수 있는 칸"이
//   양 팀에게 똑같이 10칸 있어야 공평하다.
//
//   문제는 이 계산이 필요한 곳이 세 군데라는 점이다.
//
//     ① 맵 생성기 — 그 10칸 위에 중립 광산을 놓아 버리면 안 되므로 미리 빼둬야 한다
//     ② 맵 검증기 — 완성된 맵이 정말 양쪽 10칸인지 확인해야 한다(규칙 13 검증 3번)
//     ③ 런타임   — 실제로 성과 채굴소를 놓고 그 주변 타일 주인을 정해야 한다
//
//   같은 계산을 세 벌 적으면 나중에 규칙이 한 줄 바뀔 때 세 곳을 모두 고쳐야 하고,
//   한 곳만 고치면 "생성기는 통과시켰는데 검증기는 떨어뜨리는" 맵이 생긴다.
//   그래서 계산을 여기 한 곳에만 두고 세 곳이 이것을 가져다 쓴다.
//   🔴 그것이 이 클래스가 존재하는 이유다. 계산을 복사해 가지 말 것.
//
// ─────────────────────────────────────────────────────────────────────────────
// 계약 — 어디에 적혀 있는가
// ─────────────────────────────────────────────────────────────────────────────
//   TechnicalDesignDocument.md 「초기 소유권 단일 소스」(「MapDefinition 정규 데이터
//   계약」 안에 있다) 와 GameSystemRules/GameSystemRules_RandomMap.md 규칙 2·규칙 13.
//   두 곳이 어긋나면 규칙 문서가 옳다(TDD 자신이 그렇게 적고 있다).
//
//       각 팀의 초기 소유 타일 =
//           Castle 타일 + Castle 인접 6타일
//         ∪ Starting MiningPost 타일 + 그 인접 6타일
//
//   맵 정의(MapDefinition)는 타일별 초기 소유자를 저장하지도, 전송하지도, 해시에
//   넣지도 않는다. Blue/Red 성 위치와 팀 시작 광산 위치, 그리고 위 확장 규칙이
//   유일한 입력이다. Host 와 Client 가 각자 같은 위치에서 같은 결과를 다시 만들어
//   내므로 소유권을 따로 주고받을 이유가 없다.
//
//   즉시 일반 건설 가능 타일은 TDD 가 정한 아래 순서 그대로 판정한다.
//
//     1. 성과 초기 채굴소가 점유한 타일을 제외한다.
//     2. TileKind 가 일반 타일이고 · 광산이 없고 · 건물이 없고 + 기존 소유권 조건.
//     3. 중복 좌표를 한 번만 세어 Blue/Red 각각 정확히 10개인지 확인한다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔴 「고유(unique)」 타일이란 무엇인가 — 이 파일에서 가장 오해하기 쉬운 낱말
// ─────────────────────────────────────────────────────────────────────────────
//   규칙 2 는 "양 팀은 각각 정확히 10개의 즉시 건설 가능한 고유 타일을 가져야 한다"
//   고 쓴다. 「고유」에는 서로 다른 두 가지 뜻이 겹쳐 있고, 이 클래스는 둘 다 지킨다.
//
//   (가) 한 팀 안에서의 중복 제거 — TDD 판정 3번이 명시한 뜻이다.
//        성 인접 6칸과 시작 광산 인접 6칸은 실제로 겹치는 칸이 있다(규격 좌표에서
//        팀마다 2칸씩 겹친다). 그 칸을 두 번 세면 개수가 부풀려진다.
//        → 이 클래스는 모든 결과를 집합(HashSet)으로 다루므로 같은 좌표는 언제나
//          한 번만 들어간다. "중복 좌표를 한 번만 센다"가 구조적으로 보장된다.
//
//   (나) 두 팀 사이의 겹침 제거 — "고유"라는 낱말 자체의 뜻이다.
//        어떤 좌표가 Blue 의 건설 가능 집합에도 Red 의 건설 가능 집합에도 들어
//        있다면, 그 좌표는 어느 쪽에게도 "그 팀만의 칸"이 아니다.
//        → GetUniqueBuildableTiles 는 양쪽에 함께 들어 있는 좌표를 양쪽 모두에서
//          빼고 돌려준다. 개수 검증(규칙 13 검증 3번)은 이 집합을 센다.
//
//   ⚠️ 그러나 「고유가 아니다」와 「보호하지 않아도 된다」는 다른 이야기다.
//      두 팀이 함께 닿는 칸이라도 그 위에 중립 광산이 놓이면 양쪽 시작 지역이
//      동시에 망가진다. 그래서 생성기에게 넘기는 ProtectedTiles 는 고유 여부를
//      따지지 않고 두 팀의 건설 가능 집합을 통째로 합친다.
//
//   덧붙여, 규격 좌표(규칙 1·규칙 2)에서는 두 팀 사이 겹침이 실제로 0칸이다.
//   즉 (가)만 적용하든 (가)+(나)를 적용하든 결과가 같다. (나)를 넣어 둔 이유는
//   지형이 이상하게 생긴 맵에서 개수가 조용히 부풀려지는 것을 막기 위해서다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔴 인접 6타일을 offset 좌표에서 "위아래좌우"로 구하면 틀린다
// ─────────────────────────────────────────────────────────────────────────────
//   MapDefinition 은 타일을 (col, row) 라는 offset 좌표로 줄 세워 저장한다
//   (행 번호에 가로 크기를 곱하고 열 번호를 더한 값이 그 타일의 인덱스다 — row-major).
//   이건 사각형 표처럼 생겼지만 실제 격자는 육각형이다.
//
//   이 맵은 FlatTop(육각형의 평평한 변이 위쪽) 격자라서 홀수 열이 반 칸 아래로
//   내려가 있다.
//
//        열:   0     1     2     3     4
//             ┌─┐         ┌─┐         ┌─┐
//     0행 ->  │ │   ┌─┐   │ │   ┌─┐   │ │   <- 짝수 열은 여기서 시작
//             └─┘   │ │   └─┘   │ │   └─┘
//             ┌─┐   └─┘   ┌─┐   └─┘   ┌─┐   <- 홀수 열은 반 칸 아래에서 시작
//     1행 ->  │ │   ┌─┐   │ │   ┌─┐   │ │
//             └─┘   │ │   └─┘   │ │   └─┘
//
//   그래서 "옆 열의 같은 행"이 실제로 옆에 붙어 있는지 아닌지가 열의 홀짝에 따라
//   달라진다. offset 표에서 상하좌우로 한 칸씩 집는 방식은 어떤 칸에서는 맞고
//   어떤 칸에서는 틀리며, 틀렸다는 사실이 눈에 띄지 않는다. 보호 10타일이 한두 칸
//   어긋난 채로 맵이 만들어지면 그 위에 광산이 놓여도 아무도 모른다.
//
//   육각형의 인접은 큐브 좌표(Q, R, S = -Q-R)에서만 깔끔하게 정의된다. 큐브에서는
//   6방향이 전부 같은 모양의 덧셈이고, 인접 = 큐브 거리 1 이다. 그래서 순서는
//   언제나 아래 셋이다.
//
//     1. offset (col, row) 를 큐브로 바꾼다        (HexGrid.OffsetToCube, FlatTop = even-q)
//     2. 큐브에서 6방향 이웃을 구한다               (HexDirectionExtensions)
//     3. 큐브를 다시 offset 으로 되돌린다           (CubeToOffset — even-q 역변환)
//        그리고 격자 밖으로 나간 이웃은 버린다.
//
//   격자 가장자리에서는 이웃이 6개보다 적게 나오는 것이 정상이다. 예를 들어
//   11x21 격자의 (0,0) 은 이웃이 2개뿐이다. 그 사실 자체를 아래 자기 검증이 확인한다.
//
// Domain 레이어 — 순수 C#, UnityEngine 참조 없음, Core 레이어 참조 없음.
// ============================================================================

using System;
using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 성·시작 광산 위치에서 경기 시작 순간의 타일 소유권과 즉시 건설 가능 타일을
    /// 계산하는 순수 규칙. 생성기·검증기·런타임이 같은 인스턴스 계산을 공유한다.
    /// </summary>
    public sealed class InitialMapStateEvaluator
    {
        // ====================================================================
        // 상수
        // ====================================================================

        /// <summary>
        /// 각 팀이 가져야 하는 즉시 건설 가능한 고유 타일 수(규칙 2 확정값).
        /// </summary>
        public const int RequiredBuildableTileCount = 10;

        /// <summary>
        /// 육각형 한 칸의 최대 이웃 수(6). 이웃 버퍼의 최소 길이이기도 하다.
        /// 격자 가장자리에서는 이보다 적게 나온다.
        /// </summary>
        public const int MaxNeighborCount = HexDirectionExtensions.Count;

        // ====================================================================
        // 상태 — 생성자에서 한 번 계산한 스냅샷
        // ====================================================================
        //
        // ⚠️ 이 클래스는 생성자에서 맵 정의를 읽어 결과를 굳혀 둔다(스냅샷).
        //    생성 이후에 맵 정의를 고치면 이 인스턴스의 값은 옛것이 된다.
        //    맵을 고쳤다면 새 evaluator 를 만들 것. 이렇게 정한 이유는 생성기가
        //    시도 한 번마다 같은 값을 여러 번 물어보기 때문이며, 매번 다시 계산하면
        //    "언제 계산한 값인지" 가 흐려져 오히려 추적이 어려워진다.

        private readonly MapDefinition _definition;

        // 광산이 놓인 타일 -> 광산 종류. 맵 정의의 배치 목록에서 투영한 것이다.
        private readonly Dictionary<int, MineKind> _mineTiles;

        // 경기 시작 시점에 이미 건물이 서 있는 타일(성 + 시작 채굴소).
        private readonly HashSet<int> _occupiedTiles;

        private readonly HashSet<int> _blueOwnedTiles;
        private readonly HashSet<int> _redOwnedTiles;
        private readonly HashSet<int> _contestedOwnedTiles;

        private readonly HashSet<int> _blueBuildableTiles;
        private readonly HashSet<int> _redBuildableTiles;
        private readonly HashSet<int> _sharedBuildableTiles;
        private readonly HashSet<int> _blueUniqueBuildableTiles;
        private readonly HashSet<int> _redUniqueBuildableTiles;

        private readonly HashSet<int> _protectedTiles;

        // ====================================================================
        // 읽기 전용 노출
        // ====================================================================

        /// <summary> 이 evaluator 가 읽은 맵 정의. </summary>
        public MapDefinition Definition => _definition;

        /// <summary>
        /// 경기 시작 시점에 건물이 이미 점유한 타일(성 + 시작 채굴소)의 row-major 인덱스.
        /// TDD 판정 1번이 말하는 "제외 대상"이 바로 이 집합이다.
        /// </summary>
        public IReadOnlyCollection<int> OccupiedTiles => _occupiedTiles;

        /// <summary>
        /// 두 팀의 초기 소유 타일이 겹친 좌표. 규격 좌표에서는 비어 있어야 한다.
        /// 비어 있지 않다면 성이나 시작 광산이 서로 너무 가깝게 놓였다는 뜻이다.
        /// </summary>
        public IReadOnlyCollection<int> ContestedOwnedTiles => _contestedOwnedTiles;

        /// <summary>
        /// 두 팀의 즉시 건설 가능 집합에 함께 들어간 좌표(= 어느 쪽에도 고유하지 않은 칸).
        /// 규격 좌표에서는 비어 있어야 한다.
        /// </summary>
        public IReadOnlyCollection<int> SharedBuildableTiles => _sharedBuildableTiles;

        /// <summary>
        /// 중립 광산 후보에서 반드시 빼야 하는 타일 전체
        /// (성 + 시작 채굴소 + 양 팀의 즉시 건설 가능 타일).
        /// 🔴 고유 여부를 따지지 않고 두 팀 집합을 통째로 합친다 — 두 팀이 함께 닿는
        /// 칸이라도 광산이 놓이면 양쪽 시작 지역이 동시에 망가지기 때문이다.
        /// </summary>
        public IReadOnlyCollection<int> ProtectedTiles => _protectedTiles;

        // ====================================================================
        // 생성
        // ====================================================================

        /// <summary>
        /// 맵 정의를 읽어 초기 상태를 계산한다. 계산은 이 생성자에서 한 번만 일어난다.
        /// </summary>
        /// <param name="definition">성·시작 광산 배치가 끝난 맵 정의</param>
        public InitialMapStateEvaluator(MapDefinition definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));

            _mineTiles = BuildMineTileMap(definition);
            _occupiedTiles = BuildOccupiedTiles(definition);

            // ── 1. 각 팀의 초기 소유 타일 ────────────────────────────────────
            // 성 타일 + 성 인접 6타일 ∪ 시작 광산 타일 + 그 인접 6타일.
            // 집합에 넣으므로 겹치는 좌표는 저절로 한 번만 들어간다.
            _blueOwnedTiles = BuildOwnedTiles(TeamId.Blue);
            _redOwnedTiles = BuildOwnedTiles(TeamId.Red);

            _contestedOwnedTiles = Intersect(_blueOwnedTiles, _redOwnedTiles);

            // ── 2. 즉시 건설 가능 타일 (TDD 판정 1번 -> 2번) ─────────────────
            _blueBuildableTiles = BuildBuildableTiles(_blueOwnedTiles);
            _redBuildableTiles = BuildBuildableTiles(_redOwnedTiles);

            // ── 3. 「고유」 처리 — 두 팀에 함께 들어간 칸은 양쪽에서 뺀다 ─────
            _sharedBuildableTiles = Intersect(_blueBuildableTiles, _redBuildableTiles);
            _blueUniqueBuildableTiles = Except(_blueBuildableTiles, _sharedBuildableTiles);
            _redUniqueBuildableTiles = Except(_redBuildableTiles, _sharedBuildableTiles);

            // ── 4. 생성기에게 넘길 보호 집합 ─────────────────────────────────
            _protectedTiles = new HashSet<int>(_occupiedTiles);
            _protectedTiles.UnionWith(_blueBuildableTiles);
            _protectedTiles.UnionWith(_redBuildableTiles);
        }

        // ====================================================================
        // 1. 좌표 변환과 인접 6타일 — 이 파일의 기초 도구
        // ====================================================================

        /// <summary>
        /// offset 좌표(col, row)를 FlatTop(even-q) 큐브 좌표로 바꾼다.
        /// 변환식의 단일 소스는 HexGrid.OffsetToCube 이며 여기서는 방향만 고정해 부른다.
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <returns>큐브 좌표</returns>
        public static HexCoord OffsetToCube(int col, int row)
        {
            return HexGrid.OffsetToCube(col, row, HexOrientation.FlatTop);
        }

        /// <summary>
        /// FlatTop(even-q) 큐브 좌표를 offset 좌표로 되돌린다.
        /// HexGrid.OffsetToCube 의 역함수이며, 프로젝트에 역변환 API 가 없어 여기 둔다.
        /// (같은 두 줄이 SymmetricMapBuilder 의 자기 검증 안에도 인라인으로 들어 있다.)
        /// </summary>
        /// <param name="cube">큐브 좌표</param>
        /// <param name="col">되돌린 열</param>
        /// <param name="row">되돌린 행</param>
        public static void CubeToOffset(HexCoord cube, out int col, out int row)
        {
            // 정변환이 r = row - (col - (col & 1)) / 2 이므로 그대로 이항하면 아래가 된다.
            // col 이 음수여도 (col - (col & 1)) 은 항상 짝수라 나눗셈이 정확히 떨어진다.
            col = cube.Q;
            row = cube.R + (col - (col & 1)) / 2;
        }

        /// <summary>
        /// 타일의 인접 6타일을 구해 버퍼에 채우고 실제 개수를 돌려준다.
        /// 격자 밖으로 나가는 이웃은 버리므로 가장자리에서는 6개보다 적게 나온다.
        /// 반드시 큐브 좌표를 거쳐 계산한다(파일 머리말의 이유 참조).
        /// </summary>
        /// <param name="tileIndex">기준 타일의 row-major 인덱스</param>
        /// <param name="width">격자 가로 타일 수</param>
        /// <param name="height">격자 세로 타일 수</param>
        /// <param name="buffer">결과를 담을 배열(길이 6 이상)</param>
        /// <returns>실제로 채운 이웃 수(0~6)</returns>
        public static int GetNeighborIndices(int tileIndex, int width, int height, int[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (buffer.Length < MaxNeighborCount)
            {
                throw new ArgumentException(
                    "이웃 버퍼는 길이가 " + MaxNeighborCount + " 이상이어야 한다.", nameof(buffer));
            }
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            int total = width * height;
            if (tileIndex < 0 || tileIndex >= total)
            {
                throw new ArgumentOutOfRangeException(nameof(tileIndex));
            }

            int col = tileIndex % width;
            int row = tileIndex / width;

            // 1단계: offset -> 큐브
            HexCoord center = OffsetToCube(col, row);

            int count = 0;
            for (int d = 0; d < HexDirectionExtensions.Count; d++)
            {
                // 2단계: 큐브에서 6방향 이웃
                HexCoord neighbor = ((HexDirection)d).Neighbor(center);

                // 3단계: 큐브 -> offset 후 격자 밖이면 버린다
                CubeToOffset(neighbor, out int nCol, out int nRow);
                if (nCol < 0 || nCol >= width) continue;
                if (nRow < 0 || nRow >= height) continue;

                buffer[count] = nRow * width + nCol;
                count++;
            }

            return count;
        }

        /// <summary>
        /// 이 맵 크기로 인접 6타일을 구해 버퍼에 채우고 실제 개수를 돌려준다.
        /// </summary>
        /// <param name="tileIndex">기준 타일의 row-major 인덱스</param>
        /// <param name="buffer">결과를 담을 배열(길이 6 이상)</param>
        /// <returns>실제로 채운 이웃 수(0~6)</returns>
        public int GetNeighborIndices(int tileIndex, int[] buffer)
        {
            return GetNeighborIndices(tileIndex, _definition.Width, _definition.Height, buffer);
        }

        /// <summary>
        /// 인접 6타일을 리스트에 채운다. 리스트는 먼저 비운다.
        /// 버퍼를 직접 들고 다니기 번거로운 호출부(BFS 등)를 위한 편의 메서드다.
        /// </summary>
        /// <param name="tileIndex">기준 타일의 row-major 인덱스</param>
        /// <param name="results">결과를 담을 리스트</param>
        public void CollectNeighborIndices(int tileIndex, List<int> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            results.Clear();

            var buffer = new int[MaxNeighborCount];
            int count = GetNeighborIndices(tileIndex, buffer);
            for (int i = 0; i < count; i++)
            {
                results.Add(buffer[i]);
            }
        }

        // ====================================================================
        // 2. 초기 소유 타일
        // ====================================================================

        /// <summary>
        /// 한 팀의 초기 소유 타일 집합을 돌려준다.
        /// (성 타일 + 성 인접 6타일) ∪ (시작 광산 타일 + 그 인접 6타일).
        /// 중복 좌표는 집합이라 한 번만 들어 있다.
        /// </summary>
        /// <param name="team">Blue 또는 Red</param>
        /// <returns>row-major 인덱스 집합(읽기 전용)</returns>
        public IReadOnlyCollection<int> GetInitialOwnedTiles(TeamId team)
        {
            EnsureTeamSide(team, nameof(team));
            return team == TeamId.Blue ? _blueOwnedTiles : _redOwnedTiles;
        }

        /// <summary>
        /// 그 타일의 경기 시작 시점 주인을 돌려준다. 어느 팀에도 속하지 않거나
        /// 두 팀 집합에 함께 들어 있으면 Neutral 이다.
        /// (양 팀이 동시에 닿는 칸은 어느 한쪽 것으로 정하지 않는다 — 기존 점령 규칙과 같은 처리.)
        /// </summary>
        /// <param name="tileIndex">타일의 row-major 인덱스</param>
        /// <returns>Blue / Red / Neutral</returns>
        public TeamId GetInitialOwner(int tileIndex)
        {
            bool blue = _blueOwnedTiles.Contains(tileIndex);
            bool red = _redOwnedTiles.Contains(tileIndex);

            if (blue && red) return TeamId.Neutral;
            if (blue) return TeamId.Blue;
            if (red) return TeamId.Red;
            return TeamId.Neutral;
        }

        // ====================================================================
        // 3. 즉시 건설 가능 타일
        // ====================================================================

        /// <summary>
        /// 한 팀의 즉시 건설 가능 타일 집합(그 팀 기준, 두 팀 겹침은 아직 빼지 않은 것).
        /// 생성기가 보호 대상을 모을 때처럼 "겹쳐도 보호는 해야 하는" 용도에 쓴다.
        /// </summary>
        /// <param name="team">Blue 또는 Red</param>
        /// <returns>row-major 인덱스 집합(읽기 전용)</returns>
        public IReadOnlyCollection<int> GetBuildableTiles(TeamId team)
        {
            EnsureTeamSide(team, nameof(team));
            return team == TeamId.Blue ? _blueBuildableTiles : _redBuildableTiles;
        }

        /// <summary>
        /// 한 팀의 즉시 건설 가능한 「고유」 타일 집합.
        /// 두 팀 집합에 함께 들어 있는 좌표는 어느 쪽에도 고유하지 않으므로 빠져 있다.
        /// 규칙 2 의 "정확히 10개"는 이 집합의 크기를 뜻한다.
        /// </summary>
        /// <param name="team">Blue 또는 Red</param>
        /// <returns>row-major 인덱스 집합(읽기 전용)</returns>
        public IReadOnlyCollection<int> GetUniqueBuildableTiles(TeamId team)
        {
            EnsureTeamSide(team, nameof(team));
            return team == TeamId.Blue ? _blueUniqueBuildableTiles : _redUniqueBuildableTiles;
        }

        /// <summary>
        /// 한 팀의 즉시 건설 가능한 고유 타일 수.
        /// </summary>
        /// <param name="team">Blue 또는 Red</param>
        /// <returns>타일 수</returns>
        public int GetUniqueBuildableTileCount(TeamId team)
        {
            return GetUniqueBuildableTiles(team).Count;
        }

        /// <summary>
        /// 양 팀이 각각 정확히 10개의 즉시 건설 가능한 고유 타일을 갖는지 확인한다
        /// (규칙 13 검증 3번). 검증기가 그대로 호출해 쓰라고 만든 형태다.
        /// </summary>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <param name="blueCount">실제 Blue 고유 타일 수</param>
        /// <param name="redCount">실제 Red 고유 타일 수</param>
        /// <returns>양 팀 모두 정확히 10개면 true</returns>
        public bool TryValidateBuildableTileCount(out string failureReason, out int blueCount, out int redCount)
        {
            blueCount = _blueUniqueBuildableTiles.Count;
            redCount = _redUniqueBuildableTiles.Count;

            if (blueCount != RequiredBuildableTileCount || redCount != RequiredBuildableTileCount)
            {
                failureReason =
                    "즉시 건설 가능한 고유 타일이 " + RequiredBuildableTileCount + "개가 아니다 (Blue " +
                    blueCount + " / Red " + redCount + ", 두 팀 겹침 " +
                    _sharedBuildableTiles.Count + "칸).";
                return false;
            }

            failureReason = null;
            return true;
        }

        // ====================================================================
        // 4. 광산 종류 조회
        // ====================================================================

        /// <summary>
        /// 그 타일에 놓인 광산의 종류를 돌려준다. 맵 정의의 광산 배치 목록에서 투영한 값이다.
        /// </summary>
        /// <param name="tileIndex">타일의 row-major 인덱스</param>
        /// <returns>광산 종류(없으면 None)</returns>
        public MineKind GetMineKind(int tileIndex)
        {
            return _mineTiles.TryGetValue(tileIndex, out MineKind kind) ? kind : MineKind.None;
        }

        /// <summary>
        /// 경기 시작 시점에 그 타일에 건물이 서 있는지 확인한다(성 또는 시작 채굴소).
        /// </summary>
        /// <param name="tileIndex">타일의 row-major 인덱스</param>
        /// <returns>건물이 있으면 true</returns>
        public bool HasInitialBuilding(int tileIndex)
        {
            return _occupiedTiles.Contains(tileIndex);
        }

        // ====================================================================
        // 내부 도우미
        // ====================================================================

        /// <summary>
        /// 맵 정의의 광산 배치 목록을 "타일 -> 광산 종류" 사전으로 투영한다.
        /// </summary>
        /// <param name="definition">맵 정의</param>
        /// <returns>광산이 놓인 타일만 담긴 사전</returns>
        private static Dictionary<int, MineKind> BuildMineTileMap(MapDefinition definition)
        {
            var map = new Dictionary<int, MineKind>();

            for (int i = 0; i < definition.StartingMines.Count; i++)
            {
                MapObjectPlacement placement = definition.StartingMines[i];
                if (!definition.IsValidIndex(placement.TileIndex)) continue;

                map[placement.TileIndex] =
                    placement.Team == TeamId.Red ? MineKind.RedStart : MineKind.BlueStart;
            }

            for (int i = 0; i < definition.NeutralMines.Count; i++)
            {
                int index = definition.NeutralMines[i];
                if (!definition.IsValidIndex(index)) continue;

                // 시작 광산과 겹쳐 기록된 정의라면 시작 광산 쪽을 남긴다.
                // (그런 정의는 애초에 검증기가 떨어뜨릴 대상이며, 여기서 조용히 덮어써
                //  "겹침이 없던 것처럼" 보이게 만들지 않는다.)
                if (map.ContainsKey(index)) continue;

                map[index] = MineKind.Neutral;
            }

            return map;
        }

        /// <summary>
        /// 경기 시작 시점에 건물이 서 있는 타일을 모은다.
        /// 성 타일과 시작 광산 타일이며, 시작 광산에는 채굴소가 자동 건설된다.
        /// </summary>
        /// <param name="definition">맵 정의</param>
        /// <returns>점유 타일 집합</returns>
        private static HashSet<int> BuildOccupiedTiles(MapDefinition definition)
        {
            var occupied = new HashSet<int>();

            for (int i = 0; i < definition.Castles.Count; i++)
            {
                int index = definition.Castles[i].TileIndex;
                if (definition.IsValidIndex(index)) occupied.Add(index);
            }

            for (int i = 0; i < definition.StartingMines.Count; i++)
            {
                int index = definition.StartingMines[i].TileIndex;
                if (definition.IsValidIndex(index)) occupied.Add(index);
            }

            return occupied;
        }

        /// <summary>
        /// 한 팀의 초기 소유 타일을 계산한다.
        /// 성과 시작 광산 각각에 대해 "자기 타일 + 인접 6타일"을 합집합으로 모은다.
        /// </summary>
        /// <param name="team">Blue 또는 Red</param>
        /// <returns>초기 소유 타일 집합</returns>
        private HashSet<int> BuildOwnedTiles(TeamId team)
        {
            var owned = new HashSet<int>();
            var buffer = new int[MaxNeighborCount];

            for (int i = 0; i < _definition.Castles.Count; i++)
            {
                MapObjectPlacement placement = _definition.Castles[i];
                if (placement.Team != team) continue;
                AddTileAndNeighbors(placement.TileIndex, owned, buffer);
            }

            for (int i = 0; i < _definition.StartingMines.Count; i++)
            {
                MapObjectPlacement placement = _definition.StartingMines[i];
                if (placement.Team != team) continue;
                AddTileAndNeighbors(placement.TileIndex, owned, buffer);
            }

            return owned;
        }

        /// <summary>
        /// 기준 타일과 그 인접 6타일을 집합에 넣는다. 격자 밖 이웃은 애초에 나오지 않는다.
        /// </summary>
        /// <param name="tileIndex">기준 타일의 row-major 인덱스</param>
        /// <param name="target">넣을 집합</param>
        /// <param name="buffer">이웃 계산에 쓸 버퍼(길이 6 이상)</param>
        private void AddTileAndNeighbors(int tileIndex, HashSet<int> target, int[] buffer)
        {
            if (!_definition.IsValidIndex(tileIndex)) return;

            target.Add(tileIndex);

            int count = GetNeighborIndices(tileIndex, buffer);
            for (int i = 0; i < count; i++)
            {
                target.Add(buffer[i]);
            }
        }

        /// <summary>
        /// 초기 소유 타일 중 즉시 일반 건설이 가능한 것만 걸러낸다.
        /// 판정 순서는 TDD 「초기 소유권 단일 소스」가 정한 그대로다.
        /// </summary>
        /// <param name="ownedTiles">그 팀의 초기 소유 타일</param>
        /// <returns>즉시 건설 가능 타일 집합</returns>
        private HashSet<int> BuildBuildableTiles(HashSet<int> ownedTiles)
        {
            var buildable = new HashSet<int>();

            foreach (int index in ownedTiles)
            {
                // 판정 1번 — 성과 초기 채굴소가 점유한 타일을 먼저 제외한다.
                if (_occupiedTiles.Contains(index)) continue;

                // 판정 2번 — 지형이 일반 타일이고, 광산이 없고, 건물이 없어야 한다.
                //   · 건설 불가 타일과 막힌 타일은 일반 건물을 지을 수 없다.
                //   · 광산 타일에는 채굴소만 지을 수 있으므로 "일반 건설 가능"이 아니다.
                //   · 경기 시작 시점의 건물은 성과 시작 채굴소뿐이며 위에서 이미 걸렀다.
                //     (맵 정의에는 런타임 건물이라는 개념 자체가 없다.)
                //   · 소유권 조건은 이 메서드에 들어온 집합이 곧 그 팀 소유 타일이라는 사실로 충족된다.
                if (_definition.Tiles[index] != TileKind.Normal) continue;
                if (GetMineKind(index) != MineKind.None) continue;

                // 판정 3번의 "중복 좌표를 한 번만" 은 집합에 넣는 것으로 자동 충족된다.
                buildable.Add(index);
            }

            return buildable;
        }

        /// <summary> 두 집합의 교집합을 새 집합으로 만든다. </summary>
        /// <param name="a">집합 1</param>
        /// <param name="b">집합 2</param>
        /// <returns>교집합</returns>
        private static HashSet<int> Intersect(HashSet<int> a, HashSet<int> b)
        {
            var result = new HashSet<int>(a);
            result.IntersectWith(b);
            return result;
        }

        /// <summary> a 에서 b 를 뺀 차집합을 새 집합으로 만든다. </summary>
        /// <param name="a">기준 집합</param>
        /// <param name="b">뺄 집합</param>
        /// <returns>차집합</returns>
        private static HashSet<int> Except(HashSet<int> a, HashSet<int> b)
        {
            var result = new HashSet<int>(a);
            result.ExceptWith(b);
            return result;
        }

        /// <summary> Blue 또는 Red 인지 확인하고 아니면 예외를 던진다. </summary>
        /// <param name="team">확인할 팀</param>
        /// <param name="parameterName">예외 메시지에 쓸 인자 이름</param>
        private static void EnsureTeamSide(TeamId team, string parameterName)
        {
            if (team != TeamId.Blue && team != TeamId.Red)
            {
                throw new ArgumentOutOfRangeException(parameterName,
                    "초기 소유·건설 판정은 Blue 또는 Red 에만 정의된다(받은 값: " + team + ").");
            }
        }

        // ====================================================================
        // 자기 검증 — 테스트 어셈블리가 없으므로 코드 안에서 확인한다
        // ====================================================================
        //
        // 이 프로젝트에는 유닛 테스트 어셈블리가 없다(2026-09-03 기준 Assets/ 아래
        // .asmdef 는 외부 패키지 것 하나뿐이다). 그래서 A·B 단계와 같은 방식으로
        // 기대값을 코드 안에 두고 스스로 확인한다.
        //
        // 🔴 기대값은 "그렇게 되기를 바라는 값"이 아니라 규칙 문서에 적힌 값이거나
        //    격자 기하에서 따라 나오는 값이다. 검사가 실패하면 검사를 고치지 말고
        //    구현이나 규칙 중 어느 쪽이 어긋났는지부터 판단할 것.

        /// <summary>
        /// 좌표 변환·인접 계산·규칙 2 개수가 규칙대로인지 확인한다. 실패하면 사유를 채우고 false.
        /// (테스트 어셈블리가 생기면 이 메서드를 그대로 호출하면 되도록 Conditional 을 붙이지 않았다.)
        /// </summary>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>모두 통과하면 true</returns>
        public static bool TryRunSelfCheck(out string failureReason)
        {
            const int width = MapDefinition.DefaultWidth;   // 11
            const int height = MapDefinition.DefaultHeight;  // 21
            int total = width * height;                      // 231

            var buffer = new int[MaxNeighborCount];

            // ── 1. offset <-> 큐브 왕복이 231칸 전수에서 제자리로 돌아오는가 ──
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    HexCoord cube = OffsetToCube(col, row);
                    CubeToOffset(cube, out int backCol, out int backRow);

                    if (backCol != col || backRow != row)
                    {
                        failureReason = "offset<->큐브 왕복 실패: (" + col + "," + row + ") -> " +
                                        cube + " -> (" + backCol + "," + backRow + ").";
                        return false;
                    }
                }
            }

            // ── 2. 🔴 이웃이 정말 헥스 인접인가 (큐브 거리 1) ────────────────
            // 이 검사가 이 파일의 핵심이다. offset 표에서 잘못 집은 이웃은
            // 큐브 거리가 1이 아니므로 여기서 반드시 걸린다.
            int edgeCells = 0;
            for (int index = 0; index < total; index++)
            {
                int count = GetNeighborIndices(index, width, height, buffer);

                if (count < 0 || count > MaxNeighborCount)
                {
                    failureReason = "이웃 수가 범위를 벗어났다(인덱스 " + index + ", " + count + "개).";
                    return false;
                }
                if (count < MaxNeighborCount) edgeCells++;

                HexCoord source = OffsetToCube(index % width, index / width);

                for (int i = 0; i < count; i++)
                {
                    int neighborIndex = buffer[i];

                    if (neighborIndex < 0 || neighborIndex >= total)
                    {
                        failureReason = "이웃이 격자 밖 인덱스다(" + index + " -> " + neighborIndex + ").";
                        return false;
                    }
                    if (neighborIndex == index)
                    {
                        failureReason = "자기 자신이 이웃으로 들어갔다(인덱스 " + index + ").";
                        return false;
                    }

                    HexCoord neighbor = OffsetToCube(neighborIndex % width, neighborIndex / width);
                    if (HexCoord.Distance(source, neighbor) != 1)
                    {
                        failureReason = "이웃이 헥스 인접이 아니다: 인덱스 " + index + " -> " + neighborIndex +
                                        " (큐브 거리 " + HexCoord.Distance(source, neighbor) + ").";
                        return false;
                    }

                    // 같은 이웃이 두 번 들어가지 않았는가
                    for (int j = 0; j < i; j++)
                    {
                        if (buffer[j] != neighborIndex) continue;
                        failureReason = "같은 이웃이 두 번 들어갔다(인덱스 " + index + " -> " + neighborIndex + ").";
                        return false;
                    }
                }
            }

            // ── 3. 가장자리에서 이웃이 6개보다 적게 나오는가 ─────────────────
            // 11x21 격자의 테두리 칸 수 = 11*2 + 21*2 - 4 = 60. 안쪽 칸은 전부 6개다.
            // (이 수가 어긋나면 이웃을 너무 많이 버렸거나 너무 적게 버린 것이다.)
            if (edgeCells != 60)
            {
                failureReason = "이웃이 6개 미만인 칸이 " + edgeCells + "개다(기대 60개).";
                return false;
            }

            // 대표 좌표 두 개를 직접 확인한다.
            // (0,0) 은 짝수 열 맨 위 모서리라 이웃이 2개뿐이고, 중심 (5,10) 은 6개다.
            if (GetNeighborIndices(0, width, height, buffer) != 2)
            {
                failureReason = "(0,0) 의 이웃이 2개가 아니다.";
                return false;
            }
            if (GetNeighborIndices(10 * width + 5, width, height, buffer) != MaxNeighborCount)
            {
                failureReason = "중심 (5,10) 의 이웃이 6개가 아니다.";
                return false;
            }

            // ── 4. 이웃 관계가 서로 대칭인가 (a 의 이웃이면 b 의 이웃이기도 한가) ──
            var neighborBuffer = new int[MaxNeighborCount];
            for (int index = 0; index < total; index++)
            {
                int count = GetNeighborIndices(index, width, height, buffer);
                for (int i = 0; i < count; i++)
                {
                    int neighborIndex = buffer[i];
                    int backCount = GetNeighborIndices(neighborIndex, width, height, neighborBuffer);

                    bool found = false;
                    for (int j = 0; j < backCount; j++)
                    {
                        if (neighborBuffer[j] != index) continue;
                        found = true;
                        break;
                    }

                    if (!found)
                    {
                        failureReason = "이웃 관계가 한쪽 방향으로만 성립한다(" + index + " -> " +
                                        neighborIndex + ").";
                        return false;
                    }
                }
            }

            // ── 5. 규칙 2 좌표로 실제 맵을 조립해 10개가 나오는가 ─────────────
            // 경우 A: Blue 시작 광산 (3,19) / 경우 B: Blue 시작 광산 (7,19).
            // 성은 Blue (5,19) 로 두면 회전 대응으로 Red (5,1) 이 자동으로 놓인다(B 단계).
            if (!CheckRuleTwoLayout(3, "경우 A", out failureReason)) return false;
            if (!CheckRuleTwoLayout(7, "경우 B", out failureReason)) return false;

            // ── 6. 위반을 실제로 잡아내는가 (음성 대조) ──────────────────────
            // 검사 5번이 통과했다는 사실만으로는 "검사가 아무거나 통과시키는" 경우와
            // 구분되지 않는다. 그래서 일부러 어긋난 맵을 하나 만들어 떨어지는지 본다.
            if (!CheckRuleTwoViolationIsDetected(out failureReason)) return false;

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 규칙 2 좌표로 맵을 조립해 초기 소유·건설 가능 타일 수를 확인한다.
        /// </summary>
        /// <param name="blueStartingMineCol">Blue 시작 광산의 열(경우 A는 3, 경우 B는 7)</param>
        /// <param name="label">실패 메시지에 쓸 경우 이름</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>기대대로면 true</returns>
        private static bool CheckRuleTwoLayout(int blueStartingMineCol, string label, out string failureReason)
        {
            InitialMapStateEvaluator evaluator = BuildRuleTwoEvaluator(blueStartingMineCol);

            // 초기 소유 타일: 성 7칸 + 시작 광산 7칸 = 14칸이지만 규격 좌표에서는
            // 두 영역이 2칸 겹치므로 12칸이 된다. 겹침을 두 번 세지 않는지 보는 검사다.
            int blueOwned = evaluator.GetInitialOwnedTiles(TeamId.Blue).Count;
            int redOwned = evaluator.GetInitialOwnedTiles(TeamId.Red).Count;
            if (blueOwned != 12 || redOwned != 12)
            {
                failureReason = label + " 초기 소유 타일이 12칸이 아니다(Blue " + blueOwned +
                                " / Red " + redOwned + ").";
                return false;
            }

            // 두 팀 영역이 겹치면 안 된다(성끼리 18행 떨어져 있다).
            if (evaluator.ContestedOwnedTiles.Count != 0)
            {
                failureReason = label + " 두 팀 초기 소유 영역이 " +
                                evaluator.ContestedOwnedTiles.Count + "칸 겹쳤다.";
                return false;
            }
            if (evaluator.SharedBuildableTiles.Count != 0)
            {
                failureReason = label + " 두 팀 건설 가능 영역이 " +
                                evaluator.SharedBuildableTiles.Count + "칸 겹쳤다.";
                return false;
            }

            // 12칸에서 성 1칸과 시작 광산 1칸을 빼면 10칸이 남는다(규칙 2).
            if (!evaluator.TryValidateBuildableTileCount(out string reason, out int blue, out int red))
            {
                failureReason = label + " " + reason;
                return false;
            }
            if (blue != RequiredBuildableTileCount || red != RequiredBuildableTileCount)
            {
                failureReason = label + " 건설 가능 타일 수가 어긋났다(Blue " + blue + " / Red " + red + ").";
                return false;
            }

            // 성 타일과 시작 광산 타일은 소유 집합에는 있고 건설 가능 집합에는 없어야 한다.
            foreach (int occupied in evaluator.OccupiedTiles)
            {
                if (evaluator.GetBuildableTiles(TeamId.Blue).Contains(occupied) ||
                    evaluator.GetBuildableTiles(TeamId.Red).Contains(occupied))
                {
                    failureReason = label + " 점유 타일 " + occupied + " 이 건설 가능으로 남았다.";
                    return false;
                }
            }

            // 보호 집합 = 점유 4칸(성 2 + 시작 광산 2) + 양 팀 건설 가능 20칸 = 24칸.
            if (evaluator.ProtectedTiles.Count != 24)
            {
                failureReason = label + " 보호 타일이 " + evaluator.ProtectedTiles.Count +
                                "칸이다(기대 24칸).";
                return false;
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 일부러 어긋난 맵에서 개수 검증이 실제로 실패하는지 확인한다.
        /// </summary>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>위반을 제대로 잡아내면 true</returns>
        private static bool CheckRuleTwoViolationIsDetected(out string failureReason)
        {
            // 경우 A 를 만들되 Blue 건설 가능 타일 하나 위에 막힌 타일을 얹는다.
            // (2,19) 는 Blue 시작 광산 (3,19) 의 이웃이며 성 영역과는 겹치지 않는다.
            var builder = new SymmetricMapBuilder();
            builder.SetCastlePair(5, 19, TeamId.Blue);
            builder.SetStartingMinePair(3, 19, TeamId.Blue);
            builder.SetPair(2, 19, TileKind.Blocked);

            var evaluator = new InitialMapStateEvaluator(builder.Build());

            if (evaluator.TryValidateBuildableTileCount(out _, out int blue, out int red))
            {
                failureReason = "막힌 타일을 얹었는데도 개수 검증이 통과했다(Blue " + blue +
                                " / Red " + red + ").";
                return false;
            }
            if (blue != RequiredBuildableTileCount - 1 || red != RequiredBuildableTileCount - 1)
            {
                // SetPair 는 회전 대응 자리에도 함께 기록하므로 양 팀이 한 칸씩 줄어야 한다.
                failureReason = "막힌 타일 한 쌍을 얹었을 때 기대한 감소가 아니다(Blue " + blue +
                                " / Red " + red + ", 기대 9 / 9).";
                return false;
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 규칙 2 좌표(성 Blue (5,19) · 시작 광산 Blue (col,19))로 맵을 조립해
        /// evaluator 를 만든다. Red 쪽은 B 단계 builder 가 180도 회전으로 놓아 준다.
        /// </summary>
        /// <param name="blueStartingMineCol">Blue 시작 광산의 열</param>
        /// <returns>조립된 맵의 evaluator</returns>
        private static InitialMapStateEvaluator BuildRuleTwoEvaluator(int blueStartingMineCol)
        {
            var builder = new SymmetricMapBuilder();
            builder.SetCastlePair(5, 19, TeamId.Blue);
            builder.SetStartingMinePair(blueStartingMineCol, 19, TeamId.Blue);

            return new InitialMapStateEvaluator(builder.Build());
        }

        /// <summary>
        /// 에디터에서 자기 검증을 실행하고 실패하면 즉시 예외로 알린다.
        /// 빌드에서는 호출 자체가 사라지므로 실기 성능에 영향이 없다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void AssertSelfCheck()
        {
            if (TryRunSelfCheck(out string failureReason)) return;

            throw new InvalidOperationException(
                "InitialMapStateEvaluator 자기 검증 실패: " + failureReason);
        }
    }
}
