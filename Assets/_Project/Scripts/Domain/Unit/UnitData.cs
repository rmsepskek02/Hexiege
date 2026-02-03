// ============================================================================
// UnitData.cs
// 유닛 한 마리의 상태 데이터.
//
// Domain 레이어의 데이터 클래스로, Unity 컴포넌트가 아님.
// Presentation 레이어의 UnitView가 이 데이터를 참조하여 화면에 표시.
//
// 프로토타입 범위:
//   - 위치, 타입, 팀, 방향만 관리
//   - HP, 공격력 등 전투 관련 스탯은 MVP에서 추가
//
// Id는 정적 카운터(_nextId)로 자동 발급.
// 각 유닛이 고유한 Id를 가지므로 Dictionary 키, 이벤트 식별 등에 사용 가능.
//
// 사용 예시:
//   var unit = new UnitData(UnitType.Pistoleer, TeamId.Blue, startCoord);
//   unit.Position = newCoord;  // 이동
//   unit.Facing = HexDirection.SE;  // 방향 전환
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

namespace Hexiege.Domain
{
    public class UnitData
    {
        /// <summary> 유닛 고유 식별자. 생성 시 자동 발급, 변경 불가. </summary>
        public int Id { get; }

        /// <summary> 유닛 종류 (Pistoleer 등). 생성 시 결정, 변경 불가. </summary>
        public UnitType Type { get; }

        /// <summary> 소속 팀 (Blue/Red). 생성 시 결정, 변경 불가. </summary>
        public TeamId Team { get; }

        /// <summary> 현재 위치 (헥스 좌표). 이동 시 업데이트. </summary>
        public HexCoord Position { get; set; }

        /// <summary>
        /// 현재 바라보는 방향 (HexDirection).
        /// 이동 시 이전→현재 타일 방향으로 업데이트.
        /// FacingDirection.FromHexDirection()으로 아트 방향+flipX 변환.
        /// </summary>
        public HexDirection Facing { get; set; }

        // 유닛 Id 자동 발급용 정적 카운터.
        // 첫 유닛은 Id=0, 다음은 Id=1, ...
        private static int _nextId;

        /// <summary>
        /// 유닛 생성.
        /// </summary>
        /// <param name="type">유닛 종류</param>
        /// <param name="team">소속 팀</param>
        /// <param name="position">초기 위치 (헥스 좌표)</param>
        /// <param name="facing">초기 바라보는 방향 (기본: 동쪽)</param>
        public UnitData(UnitType type, TeamId team, HexCoord position, HexDirection facing = HexDirection.E)
        {
            Id = _nextId++;
            Type = type;
            Team = team;
            Position = position;
            Facing = facing;
        }
    }
}
