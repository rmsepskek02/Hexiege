// ============================================================================
// HexTile.cs
// 육각형 타일 한 칸의 상태 데이터.
//
// 각 타일은 다음 정보를 가짐:
//   - Coord:       그리드 내 위치 (HexCoord, 불변)
//   - Owner:       현재 점령한 팀 (Neutral/Blue/Red, 변경 가능)
//   - TileKind:    정적 지형 종류 (일반/건설불가/막힘) — 맵 정의로 결정, 경기 중 불변
//   - MineKind:    광산 종류 (없음/중립/Blue시작/Red시작) — 맵 로드 시 투영, 경기 중 불변
//   - HasBuilding: 이 타일 위에 건물이 서 있는지 — 배치/철거/파괴로 바뀌는 동적 상태
//   - IsWalkable:  위 세 가지로부터 자동 계산되는 "이동 가능한가" (직접 대입 불가)
//   - Accepts***:  위 세 가지로부터 자동 계산되는 "이 타일이 무엇을 받아들이는가"
//                  (AcceptsGeneralBuilding / AcceptsMiningPost / AcceptsCapture)
//                  ⚠️ 이동과 건설은 서로 다른 판정이다 — 아래 판정표 주석 참조.
//
// 🔴 2026-09-01 상태 계약 전환 — 왜 구조가 바뀌었는가:
//   예전에는 IsWalkable이 직접 켜고 끌 수 있는 필드였고, 코드 5곳에서 각각
//   true/false를 대입했다. 그런데 그 5곳이 뜻하는 바는 실제로는 서로 달랐다.
//     · 건물이 생겼다  (2곳)          → 이제 HasBuilding 을 켠다
//     · 건물이 없어졌다 (1곳)          → 이제 HasBuilding 을 끈다
//     · 광산이라 원래 막힌 칸이다 (1곳) → 이제 MineKind 설정
//     · 새 타일의 기본값이다 (1곳)      → 이제 기본값(Normal/None/false)이 대신함
//   서로 다른 이유를 같은 스위치 하나로 표현하다 보니 "건물을 부쉈는데 광산이라
//   계속 막아야 한다" 같은 조건을 한 줄에 뒤섞어 적어야 했다.
//   이제는 이유를 각각 따로 적고, "이동 가능한가"는 그 이유들로부터 계산한다.
//   그래서 IsWalkable에는 setter가 없다 — 계산 결과를 손으로 덮어쓰면 다시
//   이유와 결과가 어긋나기 때문이다.
//
//   판정식의 단일 소스: TechnicalDesignDocument.md 「HexTile 런타임 상태 계약」
//
// 점령 규칙 (GDD 기준):
//   - 유닛이 타일 위로 이동하면 즉시 해당 팀 색상으로 변경
//   - 적 유닛이 진입하면 빼앗김
//   - 주변에 아군 타일/유닛이 없으면 10초 후 중립화 (MVP에서 구현)
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// Presentation 레이어의 HexTileView가 이 데이터를 읽어서 화면에 표시.
// ============================================================================

namespace Hexiege.Domain
{
    public class HexTile
    {
        /// <summary> 이 타일의 그리드 내 위치 (큐브 좌표). 생성 후 변경 불가. </summary>
        public HexCoord Coord { get; }

        /// <summary> 현재 이 타일을 점령한 팀. 유닛 이동 시 변경됨. </summary>
        public TeamId Owner { get; set; }

        /// <summary>
        /// 이 타일의 정적 지형 종류(일반/건설 불가/막힘).
        /// 맵 정의(MapDefinition)로 결정되며 경기 중에는 바뀌지 않는다.
        /// setter가 열려 있는 이유는 "맵을 로드하는 시점"에는 값을 넣어야 하기 때문이며,
        /// 로드가 끝난 뒤에는 다시 설정하지 않는다는 것이 규약이다(Owner와 같은 방식).
        /// </summary>
        public TileKind TileKind { get; set; } = TileKind.Normal;

        /// <summary>
        /// 이 타일에 놓인 광산의 종류(없음/중립/Blue 시작/Red 시작).
        /// 맵 정의의 광산 배치 목록에서 로드 시 투영되며 경기 중에는 바뀌지 않는다.
        /// None이 아니면 그 타일에는 일반 건물 대신 채굴소(MiningPost)만 지을 수 있고,
        /// 유닛은 그 위로 지나갈 수 없다(아래 IsWalkable 계산식 참조).
        /// </summary>
        public MineKind MineKind { get; set; } = MineKind.None;

        /// <summary>
        /// 이 타일 위에 건물이 서 있는지 여부.
        /// 건물 배치 시 true, 철거·파괴 시 false로 바뀌는 동적 상태다.
        /// ⚠️ 건물을 없앨 때 IsWalkable을 직접 되돌리지 말 것 —
        ///    이 값만 false로 내리면 광산·막힌 지형 조건은 계산식이 알아서 유지한다.
        /// </summary>
        public bool HasBuilding { get; set; }

        /// <summary>
        /// 유닛이 이 타일 위를 지나갈 수 있는지 여부.
        /// 저장되는 값이 아니라 위 세 가지 상태로부터 매번 계산되는 프로퍼티다(대입 불가).
        /// HexPathfinder/CongestionAwarePathfinder/HexFlowField가 경로 계산 시 이 값을 읽는다.
        ///
        /// 계산식(TDD 「HexTile 런타임 상태 계약」의 판정식 그대로):
        ///   막힌 지형이 아니고(TileKind != Blocked)
        ///   광산이 없고(MineKind == None)
        ///   건물이 서 있지 않으면(!HasBuilding) → 지나갈 수 있다.
        /// </summary>
        public bool IsWalkable => TileKind != TileKind.Blocked
                               && MineKind == MineKind.None
                               && !HasBuilding;

        // ====================================================================
        // 판정 프로퍼티 — "이동"과 "건설"과 "점령"은 서로 다른 판정이다
        // ====================================================================
        // 🔴 왜 IsWalkable 하나로는 안 되는가 (2026-09-08 무작위 맵 2단계 I):
        //
        //   규칙 문서 GameSystemRules_RandomMap.md 5장 판정표가 단일 소스다.
        //
        //     TileKind | 이동 | 점령 | 일반 건설 | MiningPost(채굴소)
        //     ---------+------+------+-----------+--------------------
        //     Normal   |  O   |  O   |     O     | 광산 타일일 때만
        //     NoBuild  |  O   |  O   |   🔴 X    | 예외로 허용
        //     Blocked  |  X   |  X   |     X     | X
        //
        //   표를 세로로 읽으면 "이동" 열과 "일반 건설" 열이 NoBuild 행에서 갈린다.
        //   NoBuild(건설 불가 타일 = 화면에 빗금이 그려지는 칸)는
        //   "지나갈 수는 있는데 건물은 못 짓는" 칸이기 때문이다.
        //
        //   그런데 IsWalkable 은 TileKind != Blocked 만 보므로 NoBuild 를 통과시킨다.
        //   그래서 건설 판정에 IsWalkable 을 쓰면 빗금 타일에 건물이 그대로 지어진다.
        //   (2026-09-08 이전 실제 증상이었다. 사람이 클릭하는 경로만 먼저 막아 두었고
        //    AI 와 내부 판정은 여전히 IsWalkable 을 읽어 빗금 타일에 건물을 지었다.)
        //
        //   → 그래서 "이동 가능"과 "무엇을 받아들이는가"를 이름부터 분리한다.
        //     같은 판정식을 호출부마다 복사해 적으면 예전 IsWalkable 필드 시절처럼
        //     여러 곳이 조용히 서로 어긋난다(위 2026-09-01 전환 사유와 같은 모양).
        //
        // 🔴 소유권(누구 땅인가)은 여기에 들어 있지 않다 — 호출부에 그대로 남는다.
        //
        //   소유권은 "타일 자체의 성질"이 아니라 "누가 물어보느냐(team)"에 따라
        //   답이 달라지는 조건이다. 같은 타일 하나가 Blue 에게는 건설 가능이고
        //   Red 에게는 불가능하다. HexTile 은 팀을 인자로 받지 않으므로
        //   여기서는 애초에 답을 낼 수 없다.
        //
        //   ⚠️ 따라서 아래 프로퍼티가 true 라고 해서 "건설해도 된다"는 뜻이 아니다.
        //      실제 배치 판정 = (아래 프로퍼티) && (호출부의 소유권/인접 조건)
        //      완전한 판정은 BuildingPlacementUseCase 쪽에 있다.
        //      이름을 CanPlaceBuilding 이 아니라 Accepts~ 로 지은 이유가 이것이다
        //      (CanPlaceBuilding 은 BuildingPlacementUseCase 에 이미 있고, 그쪽은
        //       소유권까지 포함한 "최종 판정"이다. 이름이 겹치면 반드시 혼동된다).
        //
        // 세 프로퍼티 모두 IsWalkable 과 같은 이유로 setter 가 없다 —
        // 계산 결과를 손으로 덮어쓰면 다시 "이유"와 "결과"가 어긋나기 때문이다.

        /// <summary>
        /// 이 타일이 <b>일반 건물</b>(성채·배럭 등 채굴소가 아닌 모든 건물)을 받아들일 수 있는지.
        /// 저장되는 값이 아니라 매번 계산되는 프로퍼티다(대입 불가).
        ///
        /// 계산식(TDD 「HexTile 런타임 상태 계약」의 판정식 그대로):
        ///   일반 지형이고(TileKind == Normal — 🔴 NoBuild 는 여기서 걸러진다)
        ///   광산이 없고(MineKind == None — 광산 위에는 채굴소만 지을 수 있다)
        ///   건물이 서 있지 않으면(!HasBuilding) → 일반 건물을 얹을 수 있다.
        ///
        /// ⚠️ 소유권 조건(자기 팀 타일인가)은 여기 없다. 호출부에서 함께 확인해야 한다.
        /// </summary>
        public bool AcceptsGeneralBuilding => TileKind == TileKind.Normal
                                           && MineKind == MineKind.None
                                           && !HasBuilding;

        /// <summary>
        /// 이 타일이 <b>채굴소(MiningPost)</b>를 받아들일 수 있는지.
        /// 저장되는 값이 아니라 매번 계산되는 프로퍼티다(대입 불가).
        ///
        /// 계산식(TDD 「HexTile 런타임 상태 계약」의 판정식 그대로):
        ///   막힌 지형이 아니고(TileKind != Blocked — 🔴 NoBuild 는 "예외로 허용"이라 통과한다)
        ///   광산이 있고(MineKind != None — 채굴소는 광산 위에만 지을 수 있다)
        ///   건물이 서 있지 않으면(!HasBuilding) → 채굴소를 얹을 수 있다.
        ///
        /// 🔴 NoBuild 를 통과시키는 것이 오타가 아니라 규칙이다(규칙 9).
        ///    건설 불가 타일 위에 광산이 놓일 수 있고, 그 광산은 캘 수 있어야 한다.
        ///
        /// ⚠️ 인접 팀 타일 조건은 여기 없다. 호출부에서 함께 확인해야 한다.
        /// </summary>
        public bool AcceptsMiningPost => TileKind != TileKind.Blocked
                                      && MineKind != MineKind.None
                                      && !HasBuilding;

        /// <summary>
        /// 이 타일을 <b>점령</b>할 수 있는지(유닛이 밟고 지나가면 그 팀 색으로 물드는가).
        /// 저장되는 값이 아니라 매번 계산되는 프로퍼티다(대입 불가).
        ///
        /// 계산식: 막힌 지형이 아니면(TileKind != Blocked) 점령할 수 있다.
        ///   광산·건물 여부는 점령을 막지 않는다 — 그래서 IsWalkable 과 조건이 다르다.
        ///   (건물이 선 타일도 소유권은 계속 바뀐다.)
        ///
        /// 판정식이 비교 한 번뿐인데도 프로퍼티로 둔 이유는 두 가지다.
        ///   ① 규칙 문서 판정표의 "점령" 열이 코드에서도 한 자리에만 있게 하려고.
        ///      호출부에 TileKind != Blocked 를 직접 적으면, 나중에 점령 규칙이 바뀔 때
        ///      "이동/건설은 고쳤는데 점령만 안 고침" 이 또 생긴다.
        ///   ② 판정표 3열(이동/점령/건설)이 코드에서도 나란히 보여야
        ///      다음 사람이 "점령은 어디서 판정하지?" 를 찾아 헤매지 않는다.
        /// </summary>
        public bool AcceptsCapture => TileKind != TileKind.Blocked;

        /// <summary>
        /// 타일을 만든다. 상태는 전부 기본값(일반 지형 · 광산 없음 · 건물 없음)이며,
        /// 그 결과 IsWalkable은 true로 계산된다 — 전환 이전의 기본값과 같은 결과다.
        /// (전환 이전에 있던 isWalkable 매개변수는 제거했다. IsWalkable이 계산 결과가 된
        ///  이상 생성자로 강제할 수 없고, 유일한 호출처인 HexGrid.Generate()도
        ///  기본값만 사용하고 있었기 때문이다.)
        /// </summary>
        /// <param name="coord">이 타일의 큐브 좌표</param>
        /// <param name="owner">초기 점령 팀 (기본값 Neutral)</param>
        public HexTile(HexCoord coord, TeamId owner = TeamId.Neutral)
        {
            Coord = coord;
            Owner = owner;
        }
    }
}
