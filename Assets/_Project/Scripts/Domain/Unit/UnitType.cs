// ============================================================================
// UnitType.cs
// 유닛 종류를 구분하는 열거형.
//
// 종족별 독립 유닛 식별자.
// 각 유닛은 고유한 enum 값을 가지며, 종족(RaceId)과는 별개로 관리됨.
//
// 용도:
//   - UnitData에서 유닛의 종류 식별
//   - UnitFactory에서 프리팹 선택 시 키로 사용
//   - UnitStats/UnitProductionStats에서 스탯 조회 키
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

namespace Hexiege.Domain
{
    public enum UnitType
    {
        // ── 인간계 (Human) ──
        Pistoleer     = 0,   // 권총병: 사거리 1.0, 빠른 생산
        Assault       = 1,   // 돌격소총병: 사거리 2.0, 높은 연사력
        Sniper        = 2,   // 저격총병: 사거리 5.0, 높은 공격력
        LittleKnight  = 3,   // 근접 보병
        SpearMan      = 4,   // 창병
        BattleAxe     = 5,   // 도끼병
        Tank          = 6,   // 중장갑 포격 전차
        CannonCart    = 7,   // 원거리 포격 수레

        // ── 정령계 (Spirit) ──
        FlameSpirit   = 10,  // 화염 정령: 근접(0.5), 빠른 이동, 6히트 공격
        EmberSpirit   = 11,  // 잔불 정령: 근접(0.5), 느린 이동
        InfernoSpirit = 12,  // 지옥불 정령: 원거리(4.0), 중거리 포격
        DustSpirit    = 13,  // 흙정령1
        BoulderSpirit = 14,  // 흙정령2
        QuakeSpirit   = 15,  // 흙정령3
        TideSpirit    = 16,  // 물정령1
        StreamSpirit  = 17,  // 물정령2
        TorrentSpirit = 18,  // 물정령3

        // ── 초월계 (Transcendence) ──
        BearGuard       = 20,  // 곰 근위병: 근접(0.5), 탱커
        FoxMagician     = 21,  // 여우 마법사: 원거리(3.0), 느린 이동
        LionKnight      = 22,  // 사자 기사: 근접(0.5), 빠른 이동, 4히트 공격
        RhinoBreaker    = 23,  // 돌진 탱커
        EagleArcher     = 24,  // 원거리 궁수
        RabbitTrickster = 25,  // 민첩 근접
        MushroomBomber  = 26,  // 범위 폭발 딜러
        BloomFairy      = 27   // 힐러
    }
}
