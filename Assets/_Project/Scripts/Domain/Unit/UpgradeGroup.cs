// ============================================================================
// UpgradeGroup.cs
// 연구소(Research) 유닛 강화 시스템의 "강화 그룹"과 "강화 스탯" 열거형,
// 그리고 UnitType → UpgradeGroup 매핑을 담는 순수 Domain 정적 헬퍼.
//
// 왜 그룹으로 묶는가:
//   연구는 유닛 하나가 아니라 "근접 / 원거리 / 탈것" 같은 그룹 단위로 팀 전체에 적용된다.
//   예) 인간 근접 공격력 Lv3 → LittleKnight/SpearMan/BattleAxe 3종이 모두 강화된다.
//
// 종족별 트랙 구조(GameSystemRules_Upgrade.md 규칙 2·3):
//   - 인간계: 근접 / 원거리 / 탈것        (3 그룹 × 공·방·속)
//   - 정령계: 불 / 물 / 땅                (3 그룹 × 공·방·속)
//   - 초월계: 동물 / 식물 (2 그룹 × 공·방·속) + 자연회복(초월 공용 1트랙)
//
// 기존 선례: BuildingTypeHelper(순수 Domain 정적 매핑 헬퍼)와 동일한 패턴.
//
// Domain 레이어 — 순수 C#, Unity / Hexiege.Core 의존 없음.
// ============================================================================

namespace Hexiege.Domain
{
    /// <summary>
    /// 연구 강화 그룹. 유닛은 각자 정확히 하나의 그룹에 속한다(UpgradeGroupHelper.GetGroup).
    /// 그룹 단위로 팀 전체가 함께 강화된다.
    /// </summary>
    public enum UpgradeGroup
    {
        // ── 인간계 ──
        HumanMelee,    // 근접: LittleKnight, SpearMan, BattleAxe
        HumanRanged,   // 원거리: Pistoleer, Assault, Sniper
        HumanVehicle,  // 탈것: Tank, CannonCart

        // ── 정령계 ──
        SpiritFire,    // 불: FlameSpirit, EmberSpirit, InfernoSpirit
        SpiritWater,   // 물: TideSpirit, StreamSpirit, TorrentSpirit
        SpiritEarth,   // 땅: DustSpirit, BoulderSpirit, QuakeSpirit

        // ── 초월계 ──
        TransAnimal,   // 동물: BearGuard, FoxMagician, LionKnight, RhinoBreaker, EagleArcher, RabbitTrickster
        TransPlant     // 식물: MushroomBomber, BloomFairy
    }

    /// <summary>
    /// 강화 스탯 종류. 공격력 / 방어력 / 이동속도는 "그룹 단위" 트랙이고,
    /// 자연회복(Regen)은 초월 종족 공용 "팀 단위" 트랙이다(그룹 무관).
    /// </summary>
    public enum UnitUpgradeStat
    {
        /// <summary>공격력 — 유닛별 고정 정수 증가치(Round(기본공격력×8%) × 레벨).</summary>
        Attack,
        /// <summary>방어력 — 전 유닛 공통, 레벨당 +8(0/8/16/24/32/40).</summary>
        Defense,
        /// <summary>이동속도 — 전 유닛 공통 배율(1 + 0.064 × 레벨).</summary>
        MoveSpeed,
        /// <summary>자연회복 — 초월 공용 팀 단위 트랙(3 × 레벨 HP/s). 그룹 무시.</summary>
        Regen
    }

    /// <summary>
    /// UnitType ↔ UpgradeGroup 매핑 및 종족/초월 판정을 담는 순수 Domain 정적 헬퍼.
    /// 규칙 3의 유닛→그룹 표를 그대로 코드화했다.
    /// </summary>
    public static class UpgradeGroupHelper
    {
        /// <summary> 강화 트랙 최대 레벨(Lv0=연구 전 ~ Lv5). </summary>
        public const int MaxLevel = 5;

        /// <summary>
        /// 자연회복(Regen) 트랙의 그룹 키 정규화용 상수.
        /// Regen은 그룹과 무관한 초월 공용 트랙이라, 딕셔너리 키를 한 그룹으로 고정해 둔다.
        /// (동물/식물 어느 쪽으로도 조회되지 않도록 별도 상수 하나로 통일.)
        /// </summary>
        public const UpgradeGroup RegenCanonicalGroup = UpgradeGroup.TransPlant;

        /// <summary>
        /// 유닛 타입이 속한 강화 그룹을 반환한다(규칙 3의 표 그대로).
        /// 매핑에 없는(정의되지 않은) 타입은 안전하게 HumanMelee로 폴백한다.
        /// </summary>
        public static UpgradeGroup GetGroup(UnitType type)
        {
            switch (type)
            {
                // 인간 근접
                case UnitType.LittleKnight:
                case UnitType.SpearMan:
                case UnitType.BattleAxe:
                    return UpgradeGroup.HumanMelee;

                // 인간 원거리
                case UnitType.Pistoleer:
                case UnitType.Assault:
                case UnitType.Sniper:
                    return UpgradeGroup.HumanRanged;

                // 인간 탈것
                case UnitType.Tank:
                case UnitType.CannonCart:
                    return UpgradeGroup.HumanVehicle;

                // 정령 불
                case UnitType.FlameSpirit:
                case UnitType.EmberSpirit:
                case UnitType.InfernoSpirit:
                    return UpgradeGroup.SpiritFire;

                // 정령 물
                case UnitType.TideSpirit:
                case UnitType.StreamSpirit:
                case UnitType.TorrentSpirit:
                    return UpgradeGroup.SpiritWater;

                // 정령 땅
                case UnitType.DustSpirit:
                case UnitType.BoulderSpirit:
                case UnitType.QuakeSpirit:
                    return UpgradeGroup.SpiritEarth;

                // 초월 동물
                case UnitType.BearGuard:
                case UnitType.FoxMagician:
                case UnitType.LionKnight:
                case UnitType.RhinoBreaker:
                case UnitType.EagleArcher:
                case UnitType.RabbitTrickster:
                    return UpgradeGroup.TransAnimal;

                // 초월 식물
                case UnitType.MushroomBomber:
                case UnitType.BloomFairy:
                    return UpgradeGroup.TransPlant;

                default:
                    return UpgradeGroup.HumanMelee;
            }
        }

        /// <summary>
        /// 유닛이 초월계(자연회복 대상)인지 여부. 동물/식물 그룹이면 true.
        /// UnitType enum 상 초월계는 20 이상이지만, 그룹 판정으로 안전하게 구한다.
        /// </summary>
        public static bool IsTranscendence(UnitType type)
        {
            UpgradeGroup g = GetGroup(type);
            return g == UpgradeGroup.TransAnimal || g == UpgradeGroup.TransPlant;
        }

        /// <summary>
        /// 강화 그룹이 속한 종족을 반환한다(연구 패널 종족 필터·비용 배율 참고용).
        /// </summary>
        public static RaceId GetRace(UpgradeGroup group)
        {
            switch (group)
            {
                case UpgradeGroup.HumanMelee:
                case UpgradeGroup.HumanRanged:
                case UpgradeGroup.HumanVehicle:
                    return RaceId.Human;
                case UpgradeGroup.SpiritFire:
                case UpgradeGroup.SpiritWater:
                case UpgradeGroup.SpiritEarth:
                    return RaceId.Spirit;
                default:
                    return RaceId.Transcendence;
            }
        }

        /// <summary>
        /// 해당 종족이 보유한 강화 그룹 목록을 반환한다(연구 패널 트랙 나열용).
        /// 초월 종족은 동물/식물 2개만 반환하며, 자연회복은 그룹이 아닌 별도 팀 트랙이므로 미포함.
        /// </summary>
        public static UpgradeGroup[] GetGroupsForRace(RaceId race)
        {
            switch (race)
            {
                case RaceId.Human:
                    return new[] { UpgradeGroup.HumanMelee, UpgradeGroup.HumanRanged, UpgradeGroup.HumanVehicle };
                case RaceId.Spirit:
                    return new[] { UpgradeGroup.SpiritFire, UpgradeGroup.SpiritWater, UpgradeGroup.SpiritEarth };
                default:
                    return new[] { UpgradeGroup.TransAnimal, UpgradeGroup.TransPlant };
            }
        }
    }
}
