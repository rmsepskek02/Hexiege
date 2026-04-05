// ============================================================================
// RaceId.cs
// 게임 내 종족을 구분하는 열거형.
// 유닛/건물의 외형, 스탯 등 종족별 차이를 나타낼 때 사용.
//
// 사용 예시:
//   LocalPlayerRace.Set(RaceId.Spirit);      // 정령 종족 선택
//   if (race == RaceId.Transcendence) { ... } // 초월 종족인지 확인
//
// 종족 목록:
//   Human  (0) → 인간: 화약 무기와 기계 문명
//   Spirit (1) → 정령: 원소의 힘을 다루는 영적 존재
//   Transcendence (2) → 초월: 시공을 초월한 신비로운 존재
//
// Domain 레이어 — 외부 의존성 없음.
// ============================================================================

namespace Hexiege.Domain
{
    /// <summary>
    /// 종족 열거형. 로비에서 선택하여 게임에 반영.
    /// </summary>
    public enum RaceId
    {
        Human  = 0,  // 인간
        Spirit = 1,  // 정령
        Transcendence = 2   // 초월
    }
}
