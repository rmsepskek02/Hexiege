// ============================================================================
// BuildingType.cs
// 건물 종류를 정의하는 열거형.
//
// 본 열거형은 단일 카테고리(Barracks)를 종족별 생산 라인 × 단계(1/2/3)
// 구조로 확장한 결과물이다.
//   - 비생산 건물: Castle, MiningPost, AutoTower, FlightFacility, Research,
//                  MagicBuilding, HealShrine
//   - 생산 건물(Human / Spirit / Transcendence): 각 종족별 라인의 1·2·3단계
//
// 생산 건물 여부, 단계 번호, 다음 단계 조회는 BuildingTypeHelper에서 처리한다.
// (이 파일은 순수 열거형 정의만 담는다.)
//
// 주의:
//   - 열거형 멤버 순서가 변경되면 직렬화된 데이터(ScriptableObject, Scene 등)가
//     깨질 수 있다. 개발 단계이므로 Inspector 재설정으로 복구한다.
//   - NetworkBuildingController는 BuildingType을 int로 직렬화하여 RPC를 보낸다.
//     순서 변경 시 클라이언트/서버가 같은 버전을 사용하는지 확인 필요.
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

namespace Hexiege.Domain
{
    public enum BuildingType
    {
        // ---------------- 비생산 건물 ----------------
        Castle,          // 본기지 — 게임 시작 시 자동 배치
        MiningPost,      // 채굴소 — 자원 수집
        AutoTower,       // 자동 방어 포탑
        FlightFacility,  // 지원 건물
        Research,        // 업그레이드 연구 건물 (Human: TechnicalLaboratory / Spirit: AstronomicalSpirit / Transcendence: AncientGrove)
        MagicBuilding,   // 마법 특수 건물
        HealShrine,      // 회복 건물

        // ---------------- Human 생산 건물 ----------------
        // 근거리A 라인 (1 → 2 → 3)
        TrainingCamp,    // 근거리A 1단계
        WarAcademy,      // 근거리A 2단계
        HumanBarracks,   // 근거리A 3단계 (구 Barracks 명칭 충돌 방지)

        // 총기류 라인 (1 → 2 → 3)
        Gunsmith,        // 총기류 1단계
        Armory,          // 총기류 2단계
        WeaponForge,     // 총기류 3단계

        // 탈것류 라인 (1 → 2)
        Garage,          // 탈것류 1단계
        VehicleBay,      // 탈것류 2단계

        // ---------------- Spirit 생산 건물 ----------------
        // 불 속성 라인 (1 → 2 → 3)
        FireSpire,       // 불 1단계
        BlazeConduit,    // 불 2단계
        InfernoCore,     // 불 3단계

        // 물 속성 라인 (1 → 2 → 3)
        AquaSpring,      // 물 1단계
        TidalNexus,      // 물 2단계
        OceanicHeart,    // 물 3단계

        // 땅 속성 라인 (1 → 2 → 3)
        StoneMound,      // 땅 1단계
        TerraForge,      // 땅 2단계
        GaeaSanctum,     // 땅 3단계

        // ---------------- Transcendence 생산 건물 ----------------
        // 동물A 라인 (1 → 2 → 3) — PrimalSanctuary는 제작 예정
        PrimalAltar,     // 동물A 1단계
        PrimalDen,       // 동물A 2단계
        PrimalSanctuary, // 동물A 3단계 (제작 예정)

        // 동물B 라인 (1 → 2 → 3)
        FeralAltar,      // 동물B 1단계
        FeralDen,        // 동물B 2단계
        FeralSanctuary,  // 동물B 3단계

        // 식물 라인 (1 → 2)
        SporePatch,      // 식물 1단계
        FloralNursery    // 식물 2단계
    }
}
