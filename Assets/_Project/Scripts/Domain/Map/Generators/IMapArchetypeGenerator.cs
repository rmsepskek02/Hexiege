// ============================================================================
// IMapArchetypeGenerator.cs
// 「무작위 맵 유형별 생성기」가 공통으로 지켜야 하는 계약과, 그 계약에 오가는
// 자료구조들을 한 곳에 모은 파일.
//
// ─────────────────────────────────────────────────────────────────────────────
// 이 파일이 왜 필요한가 (초급자용 설명)
// ─────────────────────────────────────────────────────────────────────────────
//   무작위 맵은 다섯 가지 유형(완전개방형 · 장애물 개방형 · 협곡형 · 외곽형 ·
//   3갈래형) 중 하나로 만들어진다. 유형마다 지형을 그리는 방법은 완전히 다르지만,
//   그 뒤에 따라오는 일들은 전부 똑같다.
//
//     · 성과 시작 광산을 규격 자리에 놓는다        (규칙 1)
//     · 중립 광산을 대응쌍으로 흩뿌린다            (규칙 3)
//     · 완성된 맵이 규칙을 지켰는지 검사한다        (규칙 13, 아직 미구현)
//
//   그런데 중립 광산을 놓는 코드나 검사하는 코드가 "지금 만드는 게 협곡형인가?"를
//   일일이 따지기 시작하면, 유형이 하나 늘 때마다 그 코드들을 전부 고쳐야 한다.
//   그래서 유형마다 다른 값들을 이 파일의 계약을 통해 "물어보는" 형태로 바꿨다.
//   광산 배치기와 검증기는 유형 이름을 전혀 몰라도 일할 수 있다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 유형마다 다른 값 세 가지를 어디로 노출했는가
// ─────────────────────────────────────────────────────────────────────────────
//   ③ 허용 중립 광산 수  -> IMapArchetypeGenerator 의 Min/Max 와 허용 판정
//        생성 전에 정해지는 값이라 생성기 자체에 붙어 있다.
//
//   ④ 광산 금지 구역 / ⑤ 건설 불가 구역 / ⑥ 필수 통로
//        -> IMapArchetypeConstraints (생성 "시도" 하나마다 새로 만들어지는 물건)
//
//        🔴 왜 생성기가 아니라 시도 쪽에 붙였는가:
//           협곡형의 통로는 폭과 위치를 난수로 뽑는다. 즉 "어디가 금지 구역인가"는
//           맵을 다 그려 봐야 정해진다. 이 값을 생성기(유형)에 붙여 두면 같은
//           생성기 인스턴스로 두 번 만들 때 앞 시도의 값이 남아 섞인다.
//           그래서 시도 결과와 함께 다니는 별도 물건으로 분리했다.
//
//   이 모양이면 아직 만들지 않은 세 유형도 그대로 들어맞는다.
//     · 협곡형   : 통로 타일을 RequiredCorridors 에, 통로 벽을 광산 금지 구역에
//     · 외곽형   : 막힌 중앙을 광산 금지 구역에, 바깥 두 우회로를 RequiredCorridors 에
//     · 3갈래형  : 세 갈래를 RequiredCorridors 세 개로, 갈래 사이 벽을 금지 구역에
//
// 근거: GameSystemRules/GameSystemRules_RandomMap.md 규칙 1 · 3 · 4 · 5 · 6 · 15
// Domain 레이어 — 순수 C#, Unity/Core 의존 없음.
// ============================================================================

using System;
using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 시작 광산이 놓이는 두 가지 경우(규칙 1). 경기 선택 단계에서 50:50 으로 뽑히며,
    /// 생성기는 뽑힌 값을 받기만 하고 스스로 뽑지 않는다.
    /// 숫자 값은 로그·재현에 쓰이므로 바꾸지 말 것.
    /// </summary>
    public enum MapStartingMineSide
    {
        /// <summary> 경우 A — Blue 시작 광산이 (3,19), Red 가 (7,1). </summary>
        CaseA = 0,

        /// <summary> 경우 B — Blue 시작 광산이 (7,19), Red 가 (3,1). </summary>
        CaseB = 1
    }

    /// <summary>
    /// 맵 생성 시도 한 번에 필요한 입력값 묶음.
    /// 유형·시작 광산 경우·중립 광산 개수는 모두 「경기 선택 단계」에서 이미 뽑힌 값이며,
    /// 생성기는 여기에 담겨 온 값을 그대로 쓴다(규칙 3).
    /// </summary>
    public sealed class MapGenerationRequest
    {
        /// <summary> canonical 형식 버전. 보통 MapDefinition.CurrentMapVersion. </summary>
        public int MapVersion { get; set; } = MapDefinition.CurrentMapVersion;

        /// <summary> 경기의 64비트 최상위 시드. 같은 값이면 같은 맵이 나와야 한다. </summary>
        public ulong RootSeed { get; set; }

        /// <summary> 시도 번호(0 이상). 앞 시도가 거부되면 1 늘려 다시 부른다. </summary>
        public int AttemptIndex { get; set; }

        /// <summary> 시작 광산 배치 경우(A 또는 B). </summary>
        public MapStartingMineSide StartingMineSide { get; set; } = MapStartingMineSide.CaseA;

        /// <summary> 놓을 중립 광산 개수. 유형이 허용하는 범위 안이어야 한다. </summary>
        public int NeutralMineCount { get; set; }

        /// <summary> 테스트 모드 표시값. 맵 정의에 그대로 기록된다. </summary>
        public int TestModeFlag { get; set; }

        /// <summary> 시작 골드. 맵 정의에 그대로 기록된다. </summary>
        public int InitialGold { get; set; }
    }

    /// <summary>
    /// 「필수 통로」 하나(규칙 6 ⑥). 이 타일들이 막히면 그 시도는 유형의 뜻을 잃는다.
    /// 검증기가 "이 목록의 타일이 전부 지나갈 수 있는가"를 확인하는 데 쓴다.
    /// 완전개방형·장애물 개방형에는 필수 통로가 없어 목록이 비어 있다.
    /// </summary>
    public readonly struct MapCorridorRequirement
    {
        /// <summary> 통로 이름(로그·실패 사유에 쓰는 사람이 읽는 이름). </summary>
        public string Name { get; }

        /// <summary> 통로를 이루는 타일들의 row-major 인덱스. </summary>
        public IReadOnlyList<int> TileIndices { get; }

        /// <summary>
        /// 필수 통로 하나를 만든다.
        /// </summary>
        /// <param name="name">통로 이름</param>
        /// <param name="tileIndices">통로 타일의 row-major 인덱스 목록</param>
        public MapCorridorRequirement(string name, IReadOnlyList<int> tileIndices)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            TileIndices = tileIndices ?? throw new ArgumentNullException(nameof(tileIndices));
        }
    }

    /// <summary>
    /// 생성 시도 하나에서 확정된 유형별 제약. 광산 배치기와 검증기가 유형 이름을
    /// 모른 채로 "여기에 광산을 놓아도 되는가" 같은 질문만 던질 수 있게 해 준다.
    /// </summary>
    public interface IMapArchetypeConstraints
    {
        /// <summary> 이 제약이 나온 맵 유형. </summary>
        MapType MapType { get; }

        /// <summary> 이 시도에서 반드시 열려 있어야 하는 통로들(없으면 빈 목록). </summary>
        IReadOnlyList<MapCorridorRequirement> RequiredCorridors { get; }

        /// <summary>
        /// 그 칸이 중립 광산 금지 구역인지 확인한다(규칙 6 ④).
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <returns>광산을 놓으면 안 되는 칸이면 true</returns>
        bool IsNeutralMineForbidden(int col, int row);

        /// <summary>
        /// 그 칸이 건설 불가 구역인지 확인한다(규칙 6 ⑤).
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <returns>건설하면 안 되는 칸이면 true</returns>
        bool IsBuildForbidden(int col, int row);
    }

    /// <summary>
    /// IMapArchetypeConstraints 의 기본 구현. 금지 구역을 타일 인덱스 집합으로 들고 있으며,
    /// 아무 제약도 없는 유형은 CreateUnrestricted 로 빈 것을 만들어 쓴다.
    /// 만들어진 뒤에는 내용이 바뀌지 않는다(시도 결과와 함께 검증기까지 그대로 전달된다).
    /// </summary>
    public sealed class MapArchetypeConstraints : IMapArchetypeConstraints
    {
        private static readonly MapCorridorRequirement[] NoCorridors = new MapCorridorRequirement[0];

        private readonly int _width;
        private readonly HashSet<int> _mineForbiddenTiles;
        private readonly HashSet<int> _buildForbiddenTiles;
        private readonly MapCorridorRequirement[] _corridors;

        /// <summary> 이 제약이 나온 맵 유형. </summary>
        public MapType MapType { get; }

        /// <summary> 이 시도에서 반드시 열려 있어야 하는 통로들. </summary>
        public IReadOnlyList<MapCorridorRequirement> RequiredCorridors => _corridors;

        /// <summary>
        /// 유형별 제약을 만든다. 넘긴 집합·목록은 안에서 복사하므로 밖에서 나중에 바꿔도
        /// 이 제약은 흔들리지 않는다.
        /// </summary>
        /// <param name="mapType">맵 유형</param>
        /// <param name="width">격자 가로 타일 수(열·행을 인덱스로 바꿀 때 쓴다)</param>
        /// <param name="mineForbiddenTiles">중립 광산 금지 타일 인덱스(없으면 null)</param>
        /// <param name="buildForbiddenTiles">건설 불가 타일 인덱스(없으면 null)</param>
        /// <param name="corridors">필수 통로 목록(없으면 null)</param>
        public MapArchetypeConstraints(MapType mapType, int width,
            IReadOnlyCollection<int> mineForbiddenTiles,
            IReadOnlyCollection<int> buildForbiddenTiles,
            IReadOnlyList<MapCorridorRequirement> corridors)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "격자 가로 크기는 1 이상이어야 한다.");
            }

            MapType = mapType;
            _width = width;
            _mineForbiddenTiles = Copy(mineForbiddenTiles);
            _buildForbiddenTiles = Copy(buildForbiddenTiles);
            _corridors = CopyCorridors(corridors);
        }

        /// <summary>
        /// 금지 구역도 필수 통로도 없는 제약을 만든다.
        /// 완전개방형(규칙 4)과 장애물 개방형(규칙 5)이 쓰는 형태다.
        /// </summary>
        /// <param name="mapType">맵 유형</param>
        /// <param name="width">격자 가로 타일 수</param>
        /// <returns>아무 제약도 없는 제약 객체</returns>
        public static MapArchetypeConstraints CreateUnrestricted(MapType mapType, int width)
        {
            return new MapArchetypeConstraints(mapType, width, null, null, null);
        }

        /// <summary>
        /// 그 칸이 중립 광산 금지 구역인지 확인한다.
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <returns>광산을 놓으면 안 되는 칸이면 true</returns>
        public bool IsNeutralMineForbidden(int col, int row)
        {
            return _mineForbiddenTiles.Count != 0 && _mineForbiddenTiles.Contains(ToIndex(col, row));
        }

        /// <summary>
        /// 그 칸이 건설 불가 구역인지 확인한다.
        /// </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <returns>건설하면 안 되는 칸이면 true</returns>
        public bool IsBuildForbidden(int col, int row)
        {
            return _buildForbiddenTiles.Count != 0 && _buildForbiddenTiles.Contains(ToIndex(col, row));
        }

        /// <summary> 열·행을 row-major 인덱스로 바꾼다. </summary>
        /// <param name="col">열</param>
        /// <param name="row">행</param>
        /// <returns>row-major 인덱스</returns>
        private int ToIndex(int col, int row) => row * _width + col;

        /// <summary> 넘어온 집합을 새 HashSet 으로 복사한다(null 이면 빈 집합). </summary>
        /// <param name="source">복사할 원본</param>
        /// <returns>복사된 집합</returns>
        private static HashSet<int> Copy(IReadOnlyCollection<int> source)
        {
            var result = new HashSet<int>();
            if (source == null) return result;

            foreach (int value in source)
            {
                result.Add(value);
            }
            return result;
        }

        /// <summary> 넘어온 통로 목록을 새 배열로 복사한다(null 이면 빈 배열). </summary>
        /// <param name="source">복사할 원본</param>
        /// <returns>복사된 배열</returns>
        private static MapCorridorRequirement[] CopyCorridors(IReadOnlyList<MapCorridorRequirement> source)
        {
            if (source == null || source.Count == 0) return NoCorridors;

            var result = new MapCorridorRequirement[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                result[i] = source[i];
            }
            return result;
        }
    }

    /// <summary>
    /// 생성 시도 한 번의 결과.
    ///
    /// 🔴 「거부」는 오류가 아니다. 규칙 6은 도달성 검증이나 국소 제약에 걸린 시도를
    ///    고쳐 쓰지 말고 통째로 버리라고 정한다. 그래서 실패를 예외로 던지지 않고
    ///    이 구조체의 IsAccepted 가 false 인 형태로 돌려준다. 부르는 쪽은 시도 번호를
    ///    하나 올려 다시 부르면 된다(최대 MapRandomStreams.MaxAttemptCount 회).
    ///    전역 검증(규칙 13)은 아직 만들지 않았지만, 그 실패도 같은 모양으로 돌아온다.
    /// </summary>
    public readonly struct MapGenerationResult
    {
        /// <summary> 이 시도가 받아들여졌으면 true. false 면 시도 전체를 버린다. </summary>
        public bool IsAccepted { get; }

        /// <summary> 완성된 맵 정의(거부된 시도면 null). </summary>
        public MapDefinition Definition { get; }

        /// <summary> 이 시도에서 확정된 유형별 제약(거부된 시도면 null 일 수 있다). </summary>
        public IMapArchetypeConstraints Constraints { get; }

        /// <summary> 거부 사유(받아들여졌으면 null). </summary>
        public string RejectionReason { get; }

        /// <summary> Terrain 스트림에서 실제로 뽑은 횟수. </summary>
        public long TerrainDrawCount { get; }

        /// <summary> MinePlacement 스트림에서 실제로 뽑은 횟수. </summary>
        public long MinePlacementDrawCount { get; }

        /// <summary>
        /// Decoration 스트림에서 실제로 뽑은 횟수.
        /// 규칙 15에 따라 최초 구현은 장식을 하나도 만들지 않으므로 항상 0 이어야 한다.
        /// </summary>
        public long DecorationDrawCount { get; }

        /// <summary>
        /// 결과를 직접 만든다. 보통은 Accept / Reject 정적 메서드를 쓴다.
        /// </summary>
        /// <param name="isAccepted">받아들여졌는지</param>
        /// <param name="definition">완성된 맵 정의</param>
        /// <param name="constraints">유형별 제약</param>
        /// <param name="rejectionReason">거부 사유</param>
        /// <param name="terrainDrawCount">Terrain 스트림 뽑기 횟수</param>
        /// <param name="minePlacementDrawCount">MinePlacement 스트림 뽑기 횟수</param>
        /// <param name="decorationDrawCount">Decoration 스트림 뽑기 횟수</param>
        public MapGenerationResult(bool isAccepted, MapDefinition definition,
            IMapArchetypeConstraints constraints, string rejectionReason,
            long terrainDrawCount, long minePlacementDrawCount, long decorationDrawCount)
        {
            IsAccepted = isAccepted;
            Definition = definition;
            Constraints = constraints;
            RejectionReason = rejectionReason;
            TerrainDrawCount = terrainDrawCount;
            MinePlacementDrawCount = minePlacementDrawCount;
            DecorationDrawCount = decorationDrawCount;
        }

        /// <summary>
        /// 받아들여진 결과를 만든다.
        /// </summary>
        /// <param name="definition">완성된 맵 정의</param>
        /// <param name="constraints">유형별 제약</param>
        /// <param name="terrainDrawCount">Terrain 스트림 뽑기 횟수</param>
        /// <param name="minePlacementDrawCount">MinePlacement 스트림 뽑기 횟수</param>
        /// <param name="decorationDrawCount">Decoration 스트림 뽑기 횟수</param>
        /// <returns>받아들여진 결과</returns>
        public static MapGenerationResult Accept(MapDefinition definition,
            IMapArchetypeConstraints constraints,
            long terrainDrawCount, long minePlacementDrawCount, long decorationDrawCount)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            return new MapGenerationResult(true, definition, constraints, null,
                terrainDrawCount, minePlacementDrawCount, decorationDrawCount);
        }

        /// <summary>
        /// 거부된 결과를 만든다. 맵은 넘기지 않는다 — 반쯤 만든 맵을 손보는 경로 자체를
        /// 만들지 않기 위해서다(규칙 6 「이동·수선하지 않고 시도 전체를 거부」).
        /// </summary>
        /// <param name="rejectionReason">거부 사유</param>
        /// <param name="constraints">유형별 제약(없으면 null)</param>
        /// <param name="terrainDrawCount">Terrain 스트림 뽑기 횟수</param>
        /// <param name="minePlacementDrawCount">MinePlacement 스트림 뽑기 횟수</param>
        /// <param name="decorationDrawCount">Decoration 스트림 뽑기 횟수</param>
        /// <returns>거부된 결과</returns>
        public static MapGenerationResult Reject(string rejectionReason,
            IMapArchetypeConstraints constraints,
            long terrainDrawCount, long minePlacementDrawCount, long decorationDrawCount)
        {
            if (string.IsNullOrEmpty(rejectionReason))
            {
                throw new ArgumentException("거부 사유는 비워 둘 수 없다.", nameof(rejectionReason));
            }

            return new MapGenerationResult(false, null, constraints, rejectionReason,
                terrainDrawCount, minePlacementDrawCount, decorationDrawCount);
        }
    }

    /// <summary>
    /// 맵 유형 하나를 그리는 생성기의 공통 계약.
    /// 구현체는 MapArchetypeGeneratorBase 를 상속해 지형 그리기만 채우면 된다.
    /// </summary>
    public interface IMapArchetypeGenerator
    {
        /// <summary> 이 생성기가 만드는 맵 유형. </summary>
        MapType MapType { get; }

        /// <summary> 이 유형이 허용하는 중립 광산 최소 개수(규칙 6 ③). </summary>
        int MinNeutralMineCount { get; }

        /// <summary> 이 유형이 허용하는 중립 광산 최대 개수(규칙 6 ③). </summary>
        int MaxNeutralMineCount { get; }

        /// <summary>
        /// 그 중립 광산 개수를 이 유형이 허용하는지 확인한다.
        /// 최소~최대 사이가 아닌 개수만 허용하는 유형이 나와도 이 메서드만 바꾸면 된다.
        /// </summary>
        /// <param name="neutralMineCount">확인할 중립 광산 개수</param>
        /// <returns>허용하면 true</returns>
        bool IsNeutralMineCountAllowed(int neutralMineCount);

        /// <summary>
        /// 맵 생성을 한 번 시도한다. 실패는 예외가 아니라 거부된 결과로 돌아온다.
        /// </summary>
        /// <param name="request">이미 뽑혀 있는 입력값 묶음</param>
        /// <returns>시도 결과</returns>
        MapGenerationResult Generate(MapGenerationRequest request);
    }
}
