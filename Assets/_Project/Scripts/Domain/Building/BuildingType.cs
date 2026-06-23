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
    // 각 멤버에 정수 값을 명시 부여.
    //   - 기존 순서와 동일한 값을 그대로 부여하므로 기존 ScriptableObject/Scene 직렬화 데이터에 영향 없음.
    //   - 신규 건물 추가 시 반드시 마지막 빈 번호를 명시 부여하여 기존 인덱스 보존.
    //   - RPC 직렬화(NetworkBuildingController)에서 (int)BuildingType 캐스트가 명시값으로 안정화됨.
    public enum BuildingType
    {
        // ---------------- 비생산 건물 ----------------
        Castle = 0,           // 본기지 — 게임 시작 시 자동 배치
        MiningPost = 1,       // 채굴소 — 자원 수집
        AutoTower = 2,        // 자동 방어 포탑
        FlightFacility = 3,   // 지원 건물
        Research = 4,         // 업그레이드 연구 건물
        MagicBuilding = 5,    // 마법 특수 건물
        HealShrine = 6,       // 회복 건물

        // ---------------- Human 생산 건물 ----------------
        TrainingCamp = 7,     // 근거리A 1단계
        WarAcademy = 8,       // 근거리A 2단계
        HumanBarracks = 9,    // 근거리A 3단계

        Gunsmith = 10,        // 총기류 1단계
        Armory = 11,          // 총기류 2단계
        WeaponForge = 12,     // 총기류 3단계

        Garage = 13,          // 탈것류 1단계
        VehicleBay = 14,      // 탈것류 2단계

        // ---------------- Spirit 생산 건물 ----------------
        FireSpire = 15,       // 불 1단계
        BlazeConduit = 16,    // 불 2단계
        InfernoCore = 17,     // 불 3단계

        AquaSpring = 18,      // 물 1단계
        TidalNexus = 19,      // 물 2단계
        OceanicHeart = 20,    // 물 3단계

        StoneMound = 21,      // 땅 1단계
        TerraForge = 22,      // 땅 2단계
        GaeaSanctum = 23,     // 땅 3단계

        // ---------------- Transcendence 생산 건물 ----------------
        PrimalAltar = 24,     // 동물A 1단계
        PrimalDen = 25,       // 동물A 2단계
        PrimalSanctuary = 26, // 동물A 3단계 (제작 예정)

        FeralAltar = 27,      // 동물B 1단계
        FeralDen = 28,        // 동물B 2단계
        FeralSanctuary = 29,  // 동물B 3단계

        SporePatch = 30,      // 식물 1단계
        FloralNursery = 31    // 식물 2단계
    }
}
