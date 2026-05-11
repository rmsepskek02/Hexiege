// ============================================================================
// BuildingType.cs
// 건물 종류를 정의하는 열거형.
//
// 프로토타입/MVP 범위:
//   - Castle: 본기지. 게임 시작 시 양 팀에 자동 배치.
//   - Barracks: 배럭. 플레이어가 자기 타일에 배치. MVP에서 유닛 생산 기능 추가.
//   - MiningPost: 채굴소. 플레이어가 자기 타일에 배치. MVP에서 자원 수집 기능 추가.
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

namespace Hexiege.Domain
{
    public enum BuildingType
    {
        Castle,          // 본기지 — 게임 시작 시 자동 배치
        Barracks,        // 배럭 — 유닛 생산
        MiningPost,      // 채굴소 — 자원 수집
        AutoTower,       // 자동 방어 포탑
        FlightFacility,  // 지원 건물
        Research,        // 업그레이드 연구 건물
        MagicBuilding,   // 마법 특수 건물
        AncientGrove,    // 초월계 업그레이드 건물
        HealShrine       // 회복 건물
    }
}
