// ============================================================================
// MapDefinition.cs
// "맵 한 판 전체"를 하나의 데이터로 표현한 완성본.
//
// 이 클래스가 무엇인가:
//   맵을 만들라는 "명령"이나 "후보"가 아니라, 검증까지 끝나서 그대로 전장을
//   구성할 수 있는 최종 결과물이다. 멀티플레이에서는 Host가 이것을 만들고
//   Client는 똑같은 데이터를 받아 소비한다. 양쪽이 같은 맵을 받았는지는
//   이 데이터의 SHA-256 해시를 비교해 확인한다(MapDefinitionCodec 참조).
//
// ⚠️ 이 파일은 1단계에서 "만들어만 두는" 자료구조다.
//    지금 게임(GameBootstrapper)은 여전히 코드에 하드코딩된 좌표로 고정 맵을
//    만들며, 이 클래스를 호출하는 코드는 프로젝트 어디에도 없다.
//    실제 맵 생성기는 2단계, 네트워크 전송은 3단계 작업이다.
//
// 근거: TechnicalDesignDocument.md 「MapDefinition 정규 데이터 계약」
//
// Domain 레이어 — 순수 C#, Unity/Core 의존 없음.
// ============================================================================

using System;
using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 성(Castle)이나 시작 광산처럼 "위치 + 소속 팀"을 함께 갖는 오브젝트의 배치 정보.
    /// 위치는 타일의 row-major 인덱스 하나로만 표현한다(index = row * Width + col).
    /// </summary>
    public readonly struct MapObjectPlacement : IEquatable<MapObjectPlacement>
    {
        /// <summary> 오브젝트가 놓인 타일의 row-major 인덱스. </summary>
        public int TileIndex { get; }

        /// <summary> 이 오브젝트의 소속 팀(Blue/Red). </summary>
        public TeamId Team { get; }

        /// <summary> 배치 정보를 만든다. </summary>
        /// <param name="tileIndex">타일의 row-major 인덱스</param>
        /// <param name="team">소속 팀</param>
        public MapObjectPlacement(int tileIndex, TeamId team)
        {
            TileIndex = tileIndex;
            Team = team;
        }

        /// <summary>
        /// canonical 정렬 기준 비교. 정렬 순서는 TileIndex → Team이며,
        /// 이 순서는 직렬화 규약(TDD)에 고정되어 있다.
        /// </summary>
        /// <param name="a">비교 대상 1</param>
        /// <param name="b">비교 대상 2</param>
        /// <returns>a가 앞이면 음수, 뒤면 양수, 같으면 0</returns>
        public static int CompareCanonical(MapObjectPlacement a, MapObjectPlacement b)
        {
            int c = a.TileIndex.CompareTo(b.TileIndex);
            if (c != 0) return c;
            return ((int)a.Team).CompareTo((int)b.Team);
        }

        /// <summary> 위치와 팀이 모두 같은지 비교한다. </summary>
        /// <param name="other">비교 대상</param>
        /// <returns>같으면 true</returns>
        public bool Equals(MapObjectPlacement other)
        {
            return TileIndex == other.TileIndex && Team == other.Team;
        }

        /// <summary> object 기반 동등 비교(박싱 경로). </summary>
        /// <param name="obj">비교 대상</param>
        /// <returns>같은 타입이며 값이 같으면 true</returns>
        public override bool Equals(object obj) => obj is MapObjectPlacement o && Equals(o);

        /// <summary> 위치와 팀을 섞은 해시 코드. </summary>
        /// <returns>해시 코드</returns>
        public override int GetHashCode()
        {
            unchecked { return (TileIndex * 397) ^ (int)Team; }
        }
    }

    /// <summary>
    /// 맵 한 판의 최종 완성본. 상위 필드 + 타일 배열 + 오브젝트 배치 목록으로 구성된다.
    /// </summary>
    public sealed class MapDefinition
    {
        // ====================================================================
        // 상수 — 무작위 맵의 규격(기획 확정값)
        // ====================================================================

        /// <summary>
        /// canonical binary 형식의 버전. 형식이 바뀌면 이 값을 올린다.
        /// 지원하지 않는 버전의 데이터는 아예 해석하지 않고 실패 처리한다.
        /// ⚠️ 앱 버전이나 접속 호환성 판정에는 사용하지 않는다(별도 작업 영역).
        /// </summary>
        public const int CurrentMapVersion = 1;

        /// <summary> 무작위 맵의 가로 타일 수(기획 확정값). </summary>
        public const int DefaultWidth = 11;

        /// <summary> 무작위 맵의 세로 타일 수(기획 확정값). </summary>
        public const int DefaultHeight = 21;

        /// <summary> SHA-256 다이제스트의 바이트 수. </summary>
        public const int HashByteLength = 32;

        // ====================================================================
        // 상위 필드
        // ====================================================================

        /// <summary> canonical binary 형식 버전. 초기값 1. </summary>
        public int MapVersion { get; set; } = CurrentMapVersion;

        /// <summary> 이 맵을 만들 때 사용한 64비트 최상위 시드. </summary>
        public ulong RootSeed { get; set; }

        /// <summary> 이 맵의 유형(다섯 가지 중 하나). </summary>
        public MapType MapType { get; set; }

        /// <summary> 가로 타일 수. </summary>
        public int Width { get; }

        /// <summary> 세로 타일 수. </summary>
        public int Height { get; }

        /// <summary> 헥스 방향. 무작위 맵은 FlatTop을 사용한다. </summary>
        public HexOrientation Orientation { get; set; } = HexOrientation.FlatTop;

        /// <summary> 이 맵에 배치된 중립 광산 개수. </summary>
        public int NeutralMineCount { get; set; }

        /// <summary>
        /// 테스트 모드 표식. 소수점이나 bool이 아니라 고정폭 정수 0/1로 기록한다
        /// (직렬화 결과가 플랫폼마다 달라지지 않게 하기 위함).
        /// </summary>
        public int TestModeFlag { get; set; }

        /// <summary> 이 경기에서 실제로 지급할 초기 골드. </summary>
        public int InitialGold { get; set; }

        /// <summary>
        /// 최종 SHA-256 해시(32바이트). MapDefinitionCodec.ComputeHash로 계산해 넣는다.
        /// ⚠️ 이 필드 자신은 canonical bytes에 포함되지 않는다(자기 자신을 해시할 수 없으므로).
        /// </summary>
        public byte[] Hash { get; set; }

        // ====================================================================
        // 타일 배열
        // ====================================================================

        /// <summary>
        /// 타일 종류 배열. 길이는 Width * Height이며 row-major 순서로 저장한다.
        /// index = row * Width + col / col = index % Width / row = index / Width.
        /// 배열 인덱스가 곧 offset 좌표의 유일한 표현이다.
        /// </summary>
        public TileKind[] Tiles { get; }

        // ====================================================================
        // 오브젝트 배치 목록
        // ====================================================================

        /// <summary> 성 배치 목록(위치 + 팀). 보통 Blue/Red 각 1개씩 2개. </summary>
        public List<MapObjectPlacement> Castles { get; } = new List<MapObjectPlacement>();

        /// <summary> 팀 시작 광산 배치 목록(위치 + 팀). 보통 Blue/Red 각 1개씩 2개. </summary>
        public List<MapObjectPlacement> StartingMines { get; } = new List<MapObjectPlacement>();

        /// <summary> 중립 광산이 놓인 타일 인덱스 목록(팀 정보가 없으므로 인덱스만 저장). </summary>
        public List<int> NeutralMines { get; } = new List<int>();

        /// <summary> 장식물 목록. 최초 구현에서는 항상 비어 있다. </summary>
        public List<DecorationDefinition> Decorations { get; } = new List<DecorationDefinition>();

        // ====================================================================
        // 생성자 / 좌표 헬퍼
        // ====================================================================

        /// <summary>
        /// 지정한 크기의 빈 맵 정의를 만든다. 모든 타일은 TileKind.Normal로 초기화된다.
        /// </summary>
        /// <param name="width">가로 타일 수(기본 11)</param>
        /// <param name="height">세로 타일 수(기본 21)</param>
        public MapDefinition(int width = DefaultWidth, int height = DefaultHeight)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            Width = width;
            Height = height;
            Tiles = new TileKind[width * height];  // enum 기본값 0 = TileKind.Normal
        }

        /// <summary> 이 맵의 전체 타일 수(Width * Height). </summary>
        public int TileCount => Tiles.Length;

        /// <summary> offset 좌표(col, row)를 row-major 인덱스로 바꾼다. </summary>
        /// <param name="col">열(0 ~ Width-1)</param>
        /// <param name="row">행(0 ~ Height-1)</param>
        /// <returns>row * Width + col</returns>
        public int ToIndex(int col, int row) => row * Width + col;

        /// <summary> row-major 인덱스에서 열(col)을 얻는다. </summary>
        /// <param name="index">row-major 인덱스</param>
        /// <returns>열 번호</returns>
        public int ToCol(int index) => index % Width;

        /// <summary> row-major 인덱스에서 행(row)을 얻는다. </summary>
        /// <param name="index">row-major 인덱스</param>
        /// <returns>행 번호</returns>
        public int ToRow(int index) => index / Width;

        /// <summary> 해당 인덱스가 이 맵의 타일 배열 범위 안인지 확인한다. </summary>
        /// <param name="index">확인할 row-major 인덱스</param>
        /// <returns>범위 안이면 true</returns>
        public bool IsValidIndex(int index) => index >= 0 && index < Tiles.Length;

        /// <summary>
        /// 목록들을 canonical 정렬 순서로 맞춘다.
        /// 직렬화 전에 호출해 두면 "같은 맵인데 목록 순서만 달라서 해시가 다른" 문제를 막는다.
        /// (MapDefinitionCodec.Encode도 내부적으로 정렬된 복사본을 사용하므로 필수는 아니다.)
        /// </summary>
        public void SortCanonical()
        {
            Castles.Sort(MapObjectPlacement.CompareCanonical);
            StartingMines.Sort(MapObjectPlacement.CompareCanonical);
            NeutralMines.Sort();
            Decorations.Sort(DecorationDefinition.CompareCanonical);
        }
    }
}
