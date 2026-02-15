// ============================================================================
// HexTile.cs
// 육각형 타일 한 칸의 상태 데이터.
//
// 각 타일은 다음 정보를 가짐:
//   - Coord: 그리드 내 위치 (HexCoord, 불변)
//   - Owner: 현재 점령한 팀 (Neutral/Blue/Red, 변경 가능)
//   - IsWalkable: 유닛이 이 타일 위로 이동할 수 있는지 (건물 등이 있으면 false)
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
        /// 유닛이 이 타일 위를 지나갈 수 있는지 여부.
        /// false인 경우: 건물이 있거나 장애물이 있는 타일.
        /// HexPathfinder가 경로 계산 시 이 값을 확인함.
        /// </summary>
        public bool IsWalkable { get; set; }

        /// <summary>
        /// 이 타일에 금광이 있는지 여부.
        /// 금광 타일에만 채굴소(MiningPost) 건설 가능.
        /// GameBootstrapper에서 맵 초기화 시 설정.
        /// </summary>
        public bool HasGoldMine { get; set; }

        public HexTile(HexCoord coord, TeamId owner = TeamId.Neutral, bool isWalkable = true)
        {
            Coord = coord;
            Owner = owner;
            IsWalkable = isWalkable;
        }
    }
}
