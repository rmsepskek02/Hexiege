// ============================================================================
// RunUnitActionSelfValidation.Meteor.cs
// [2026-11-18 지원 스킬 2-D 메테오 — S2-1 서버 판정 · 착탄 화상 + S2-2 VFX 칸 · 예고 재생 속도 + S2-4 자산 켜기] 회귀 검증(fixture).
// Task: Assets/_Project/Docs/_Tasks/2026-11-18/03_34_support-skill-meteor/ (Plan 7절 M-T1 ~ M-T10).
// Run()(메뉴 「Hexiege/Combat/Run Full Regression Suite」)이 함께 돌린다.
//
// [배경 — 초급 개발자용 설명]
//   메테오는 포격과 같은 「타입 A 즉발 범위 피해 + 착탄 지연(1.5초)」에 「착탄 순간 살아 남은 적 유닛에게 화상」을 더한 스킬이다.
//   서버 판정 코드(SkillActivationUseCase 착탄 예약 · UnitCombatUseCase 착탄 화상)를 fixture로 직접 불러 숫자로 확인한다.
//   🔴 전역 스탯 표(UnitStats · BuildingStats)에 기대지 않는다 — 메뉴로 돌리면 비어 있고, Play 뒤에는 실제 값이 남아 있다
//   (공용 메모리 2026-11-17 「Regression suite fixtures」). 그래서 MeteorFixtureStatsScope가 「빈 표」에서 시작해
//   이 검사가 쓰는 행만 고정하고, 끝나면(예외로 끝나도) 원래 참조를 되돌린다(SupportFixtureStatsScope · LineStrikeFixtureStatsScope 선례).
//   화상 기대값에는 방어 감쇄를 넣지 않는다(지속 피해는 방어 감쇄 없음 — 유닛 규칙 44).
//
// 무엇을 확인하는가(Plan 7절 번호):
//   M-T1  메테오 자산 계약 — 사용 가능 · 타입 A · 지점 지정 · 지연 1.5 · 화상 50 × 3 · 피해 150 · 반경 2 · 쿨 10 · 정령 슬롯 0 ·
//         채워진 VFX 칸만 계약 검사(예고 · 착탄 = 반복 없음, 몸체 = 반복 계약) · 예고 기준 시간 1.5.
//   M-T2  예약 → 0.1초 틱으로 1.5초(15번째 틱)에 착탄 1회, 14번째 틱까지는 피해 없음, 쿨다운은 발동 순간.
//   M-T3  착탄 순간 원 안 · 밖 — 예약 뒤 원 밖으로 옮긴 유닛은 피해 · 화상 0, 예약 뒤 원 안으로 들어온 유닛은 맞는다.
//   M-T4  착탄 화상 — 살아 남은 적 유닛만 Burn · 출처 −1 · 50 × 3(1초마다 50, 총 150), 건물 화상 없음, 착탄으로 죽은 유닛 0,
//         아군 · 원 밖 0, 결과 값(원 안 수 · 생존자 · 화상 수), 착탄 지연 0 + 화상도 화상이 남는다(위험 R-9).
//   M-T5  이미 화상(유닛 출처)인 유닛에 착탄 — 화상 기록 1개(같은 주인 · 개정 증가) · 지속 리셋 · 값 = 새 쪽(출처 −1) · 총 피해 확인.
//         (독이 있는 유닛은 검사하지 않는다 — 지금 동작은 덮어쓰기이고 U-2와 다르며 경기에서 생기지 않는다, Plan 12절)
//   M-T6  시전 건물 파괴 뒤 착탄 = 피해 + 화상(U-5) / 경기 종료(ClosePendingImpacts) 뒤 = 피해 · 화상 0.
//   M-T7  포격 회귀(화상 칸 0) — 0.4초 예약 · 피해 1회 · 화상 없음 · 화면 메테오 경로 아님(UsesImpactPresentation false).
//   M-T8  사용 가능 수 14 — SupportSkillLoadoutSetup.ExpectedAvailableCount == 14(2026-11-23 연막탄으로 13 → 14 — 직전 「13」), 셋업 표의 메테오 줄(자리표시 아님 · 화상 50 · 3).
//   M-T9  예고 재생 속도 — 기준 1.5 · 지연 1.5 → 1, 지연 0.75 → 2, 잘못된 값 → 1, 상한 · 하한, 풀 항목에서 바꾼 속도가 Return 뒤 원래 값.
//   M-T10 메테오는 RPC · 동기화 형식 변경 없음 — 스키마 값은 12(2026-11-23 연막탄이 11 → 12로 올림 — 직전 「스키마 11 그대로」).
//   덧붙여 소스 게이트 — 화면 메테오 분기가 원형 「프리팹 없음」 검사보다 앞 · Update 연결 · 재생 속도 복원 · 안전 마감 연결.
//
// 하지 않는 것: 실제 멀티 전송 · 화면 이펙트의 실제 모양 · 비스듬한 궤적 · 타이밍(Plan T2-L9 — 사용자 실기 · 육안 항목),
//   몸체 안전 마감의 실제 발화(Edit 모드에서는 Time.time이 흐르지 않는다 — 마감을 거는 경로 · 확인 번호 거부만 본다).
// 모든 fixture는 새 인스턴스를 만든다. 실행 중(Play)에는 돌지 않는다.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Hexiege.Application;
using Hexiege.Domain;
using Hexiege.Infrastructure;
using Hexiege.Presentation;
using UniRx;
using UnityEngine;

namespace Hexiege.Editor.Combat
{
    public static partial class RunUnitActionSelfValidation
    {
        /// <summary>
        /// 메테오(타입 A + 착탄 지연 + 착탄 화상 + 착탄 예약 연출) 회귀 검증 진입점(M-T1 ~ M-T10).
        /// </summary>
        private static void ValidateMeteorSkill()
        {
            Require(!UnityEngine.Application.isPlaying, "Meteor fixture must run outside a live match.");
            // 전역 스탯 표 고정(Tank HP 10000 · Pistoleer HP 100 · 방어 0, 정령 MagicBuilding · 인간 Castle HP) — 끝나면 원래 참조를 되돌린다.
            using (new MeteorFixtureStatsScope())
            {
                ValidateMeteorFixtureLogic();          // M-T2 ~ M-T7 · M-T9(순수 계산) — 헤드리스로도 돈다
            }
            ValidateMeteorSourceAndSetupGates();       // M-T8 · M-T10 · 소스 게이트
            ValidateMeteorAssetContract();             // M-T1
            ValidateMeteorWarningSpeedPoolRestore();   // M-T9(풀 항목 — ParticleSystem)
            Debug.Log("[UAS-DIAG][SKILL-METEOR] self-validation PASS: Meteor asset available as type A / point / delay 1.5 / burn 50 × 3 / Spirit slot 0 with filled-slot VFX contracts (M-T1), impact exactly at the 15th 0.1 s tick with cooldown at activation (M-T2), circle judged at impact incl. moved-out / moved-in units (M-T3), burn only on surviving enemy units with source −1 and 50 per second for 3 s, none on buildings / killed / allies / outside, delay-0 burn kept (M-T4), existing burn refreshed as one record with the new values (M-T5), impact + burn after caster loss and nothing after game end (M-T6), Bombardment unchanged without burn (M-T7), setup count 14 with the Meteor spec (M-T8), warning playback speed math and pool restore (M-T9), schema 12 (M-T10).");
        }

        /// <summary> 서버 판정 fixture 묶음(M-T2 ~ M-T7)과 순수 계산(M-T9 앞부분). 헤드리스 하네스가 이 메서드만 부른다. </summary>
        private static void ValidateMeteorFixtureLogic()
        {
            ValidateMeteorScheduleAndTiming();     // M-T2
            ValidateMeteorCircleAtImpact();        // M-T3
            ValidateMeteorImpactBurn();            // M-T4
            ValidateMeteorBurnRefresh();           // M-T5
            ValidateMeteorCasterLostAndGameEnd();  // M-T6
            ValidateMeteorBombardmentUnchanged();  // M-T7
            ValidateMeteorWarningSpeedMath();      // M-T9 ①
        }

        // ── fixture 전용 스탯 고정 ─────────────────────────────────────────
        //   「빈 표」에서 시작해 이 검사가 쓰는 행만 넣고, 끝나면 두 표의 원래 참조(null이었으면 null)를 그대로 되돌린다.
        //     - Tank(살아 남는 대상) HP 10000 · 방어 0: 착탄 150을 맞아도 살아 있고, 방어 감쇄 없이 정확히 150이 깎인다.
        //     - Pistoleer(착탄으로 죽는 대상) HP 100 · 방어 0: 착탄 150에 죽는다 → 화상이 걸리지 않아야 한다.
        //     - 정령 MagicBuilding HP 1000: 시전 건물(스킬 건물 재검증을 통과해야 한다 — 살아 있어야 한다).
        //     - 인간 Castle HP 5000: 적 건물 대상(150을 맞아도 살아 있다 — 화상은 걸리지 않는다).
        private sealed class MeteorFixtureStatsScope : IDisposable
        {
            public const int SurvivorMaxHp = 10000;
            public const int FragileMaxHp = 100;
            public const int CasterMaxHp = 1000;
            public const int CastleMaxHp = 5000;

            private static readonly FieldInfo UnitDataField =
                typeof(UnitStats).GetField("_data", BindingFlags.Static | BindingFlags.NonPublic);
            private static readonly FieldInfo BuildingDataField =
                typeof(BuildingStats).GetField("_data", BindingFlags.Static | BindingFlags.NonPublic);

            private readonly object _oldUnitData;
            private readonly object _oldBuildingData;
            private bool _disposed;

            public MeteorFixtureStatsScope()
            {
                Require(UnitDataField != null && BuildingDataField != null,
                    "M-T0 fixture stat seams (UnitStats._data / BuildingStats._data) must exist.");
                _oldUnitData = UnitDataField.GetValue(null);
                _oldBuildingData = BuildingDataField.GetValue(null);

                var units = new Dictionary<UnitType, UnitStats.StatValues>();
                units[UnitType.Tank] = new UnitStats.StatValues { MaxHp = SurvivorMaxHp, AttackPower = 50, AttackRange = 3f,
                    DetectRange = 3f, MoveSpeed = .5f, AttackCooldown = 2f, HitFrameTimes = new[] { .4f }, Defense = 0 };
                units[UnitType.Pistoleer] = new UnitStats.StatValues { MaxHp = FragileMaxHp, AttackPower = 50, AttackRange = 3f,
                    DetectRange = 3f, MoveSpeed = .5f, AttackCooldown = 2f, HitFrameTimes = new[] { .4f }, Defense = 0 };
                UnitStats.Initialize(units);

                var buildings = new Dictionary<(BuildingType, RaceId), BuildingStats.StatValues>();
                buildings[(BuildingType.MagicBuilding, RaceId.Spirit)] = new BuildingStats.StatValues { MaxHp = CasterMaxHp };
                buildings[(BuildingType.Castle, RaceId.Human)] = new BuildingStats.StatValues { MaxHp = CastleMaxHp };
                BuildingStats.Initialize(buildings);

                Require(UnitStats.GetMaxHp(UnitType.Tank) == SurvivorMaxHp && UnitStats.GetDefense(UnitType.Tank) == 0
                        && UnitStats.GetMaxHp(UnitType.Pistoleer) == FragileMaxHp && UnitStats.GetDefense(UnitType.Pistoleer) == 0
                        && BuildingStats.GetMaxHp(BuildingType.MagicBuilding, RaceId.Spirit) == CasterMaxHp
                        && BuildingStats.GetMaxHp(BuildingType.Castle, RaceId.Human) == CastleMaxHp,
                    "M-T0 fixture stats must be pinned (Tank HP " + SurvivorMaxHp + " / Pistoleer HP " + FragileMaxHp
                    + " defense 0, Spirit MagicBuilding HP " + CasterMaxHp + ", Human Castle HP " + CastleMaxHp + ").");
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                UnitDataField.SetValue(null, _oldUnitData);
                BuildingDataField.SetValue(null, _oldBuildingData);
            }
        }

        // ── fixture ─────────────────────────────────────────────────────

        private const int MeteorDamage = 150;        // 착탄 피해(U-8 임시값)
        private const float MeteorCooldown = 10f;    // 쿨다운(U-8 임시값)
        private const float MeteorDelay = 1.5f;      // 착탄 지연(U-8 임시값)
        private const float MeteorBurnPerSecond = 50f; // 화상 초당(U-7 임시값)
        private const float MeteorBurnDuration = 3f;   // 화상 지속(U-7 임시값)

        // 메테오 스킬 데이터(타입 A · 반경 2 · 150 · 지연 1.5 · 화상 50 × 3 · 쿨 10).
        private static SkillData MeteorSkill(float delay = MeteorDelay, float burnPerSecond = MeteorBurnPerSecond, float burnDuration = MeteorBurnDuration)
        {
            return new SkillData(null, SkillAimType.PointTarget, MeteorCooldown, SkillMechanicType.InstantAreaDamage, 2f, 0f,
                MeteorDamage, 0f, 0, 0f, false, "메테오", string.Empty, true, delay, null, 2f,
                impactBurnDamagePerSecond: burnPerSecond, impactBurnDuration: burnDuration, warningVfxReferenceTime: 1.5f);
        }

        // 포격 스킬 데이터(타입 A · 반경 2 · 150 · 지연 0.4 · 화상 없음 — 회귀 확인용).
        private static SkillData MeteorBombardmentSkill()
        {
            return new SkillData(null, SkillAimType.PointTarget, MeteorCooldown, SkillMechanicType.InstantAreaDamage, 2f, 0f,
                MeteorDamage, 0f, 0, 0f, false, "포격 지원", string.Empty, true, .4f, null, 2f);
        }

        // 로드아웃 대역 — 정령 로드아웃 = [슬롯 0 메테오, 슬롯 1 포격(회귀 확인용)].
        private sealed class MeteorFixtureLoadout : ISkillDataProvider
        {
            private readonly IReadOnlyList<SkillData> _loadout;
            public MeteorFixtureLoadout(SkillData slot0, SkillData slot1) { _loadout = new[] { slot0, slot1 }; }
            public IReadOnlyList<SkillData> GetLoadout(RaceId race) => _loadout;
        }

        private sealed class MeteorFixture
        {
            public readonly HexGrid Grid;
            public readonly UnitSpawnUseCase Spawn;
            public readonly BuildingPlacementUseCase Buildings;
            public readonly UnitCombatUseCase Combat;
            public readonly SkillActivationUseCase Activation;
            public readonly BuildingData Caster;
            public double Now = 100d; // 지속 피해 표현 채널의 시계(시작 · 끝 시각을 숫자로 확인하려고 고정한다)

            public MeteorFixture(SkillData meteor = null)
            {
                // FlatTop 12 × 12 — 대상 건물을 (5, 5) 근처에 놓을 수 있게 넉넉히(유닛은 (0, 0)에 만든 뒤 좌표만 옮긴다).
                Grid = new HexGrid(12, 12, HexOrientation.FlatTop);
                Spawn = new UnitSpawnUseCase(Grid);
                Buildings = new BuildingPlacementUseCase(Grid);
                // 좌표 대역: 헥스 (q, r) → 월드 (q, 0, r) — 판정 좌표를 정수로 바로 읽을 수 있다.
                Combat = new UnitCombatUseCase(Grid, Spawn, Buildings, new ProjectileFixturePositions(), new ProjectileFixtureMapper());
                Combat.TimedDamageEffects.Clock = () => Now;
                var loadout = new MeteorFixtureLoadout(meteor ?? MeteorSkill(), MeteorBombardmentSkill());
                Activation = new SkillActivationUseCase(Buildings, _ => true, Combat, loadout, _ => RaceId.Spirit);
                Caster = Buildings.PlaceBuildingWithId(96000, BuildingType.MagicBuilding, TeamId.Blue, new HexCoord(0, 3), RaceId.Spirit);
                Require(Caster != null && Caster.IsAlive, "M-T0 fixture caster MagicBuilding must be placed alive.");
            }

            public UnitData Unit(int id, UnitType type, TeamId team, int x, int z)
            {
                UnitData unit = Spawn.SpawnUnitWithId(id, type, team, new HexCoord(0, 0));
                int expected = type == UnitType.Tank ? MeteorFixtureStatsScope.SurvivorMaxHp : MeteorFixtureStatsScope.FragileMaxHp;
                Require(unit != null && unit.MaxHp == expected, "M-T0 fixture " + type + " must spawn with the pinned HP: " + id);
                unit.Position = new HexCoord(x, z);
                return unit;
            }

            public BuildingData Castle(int id, TeamId team, int q, int r)
            {
                BuildingData b = Buildings.PlaceBuildingWithId(id, BuildingType.Castle, team, new HexCoord(q, r), RaceId.Human);
                Require(b != null && b.MaxHp == MeteorFixtureStatsScope.CastleMaxHp,
                    "M-T0 fixture Castle must be placed with the pinned HP at (" + q + ", " + r + ").");
                return b;
            }

            public bool Fire(int slot, float x, float z) => Activation.Activate(Caster.Id, slot, new Vector3(x, 0f, z));

            public void TickImpacts(int count, float dt = .1f)
            {
                for (int i = 0; i < count; i++) Activation.TickPendingImpacts(dt);
            }

            public void TickBurn(int seconds)
            {
                for (int i = 0; i < seconds; i++) { Now += 1d; Combat.TickTimedEffects(1f); }
            }

            // 지금 이 유닛에 붙은 지속 피해 표현 상태(없거나 끝났으면 false).
            public bool ActiveDamage(UnitData u, out TimedDamageEffectPresentation s) =>
                Combat.TimedDamageEffects.TryGet(u, out s) && s.Active;
        }

        private static int MeteorLost(IDamageable target, int maxHp) => maxHp - target.Hp;

        // ── M-T2 ────────────────────────────────────────────────────────

        /// <summary> M-T2 — 0.1초 틱으로 15번째 틱(1.5초)에 착탄 1회, 그전에는 피해 없음, 쿨다운은 발동 순간. </summary>
        private static void ValidateMeteorScheduleAndTiming()
        {
            var f = new MeteorFixture();
            UnitData e = f.Unit(9600, UnitType.Tank, TeamId.Red, 5, 5);
            var confirmed = new List<SkillActivationPresentation>();
            using (GameEvents.OnSkillActivationConfirmed.Subscribe(p => confirmed.Add(p)))
            {
                Require(f.Fire(0, 5f, 5f), "M-T2 a Meteor aimed at (5, 5) must activate.");
            }
            Require(f.Activation.GetCooldownRemaining(f.Caster.Id) == MeteorCooldown,
                "M-T2 the cooldown starts at the activation moment (rule 3), not at the impact.");
            Require(f.Activation.PendingImpactCount == 1, "M-T2 one activation schedules exactly one impact reservation.");
            Require(confirmed.Count == 1 && Math.Abs(confirmed[0].ImpactDelay - MeteorDelay) < 1e-6f && confirmed[0].HasAim,
                "M-T2 the activation notice carries the impact delay 1.5 (screens time the body and impact VFX from it).");

            f.TickImpacts(14);
            Require(MeteorLost(e, 10000) == 0 && f.Activation.PendingImpactCount == 1,
                "M-T2 no Meteor damage before 1.5 s (14 ticks of 0.1 s).");
            f.TickImpacts(1);
            Require(MeteorLost(e, 10000) == MeteorDamage && f.Activation.PendingImpactCount == 0,
                "M-T2 the Meteor lands on the 15th 0.1 s tick (1.5 s) and deals 150 once (lost " + MeteorLost(e, 10000) + ").");
            f.TickImpacts(10);
            Require(MeteorLost(e, 10000) == MeteorDamage, "M-T2 the Meteor hits only once.");
        }

        // ── M-T3 ────────────────────────────────────────────────────────

        /// <summary> M-T3 — 착탄 순간 원 안만: 예약 뒤 원 밖으로 피한 유닛 0, 예약 뒤 원 안으로 들어온 유닛은 맞는다. </summary>
        private static void ValidateMeteorCircleAtImpact()
        {
            var f = new MeteorFixture();
            UnitData dodger = f.Unit(9610, UnitType.Tank, TeamId.Red, 5, 5);   // 처음엔 원 안 → 착탄 전에 밖으로
            UnitData arriver = f.Unit(9611, UnitType.Tank, TeamId.Red, 9, 9);  // 처음엔 원 밖 → 착탄 전에 안으로
            UnitData stayer = f.Unit(9612, UnitType.Tank, TeamId.Red, 6, 5);   // 계속 원 안(거리 1)
            Require(f.Fire(0, 5f, 5f), "M-T3 the Meteor must activate.");
            f.TickImpacts(10);
            dodger.Position = new HexCoord(9, 5);   // 거리 4 > 반경 2
            arriver.Position = new HexCoord(5, 6);  // 거리 1
            f.TickImpacts(5);
            Require(MeteorLost(dodger, 10000) == 0 && !f.ActiveDamage(dodger, out _),
                "M-T3 a unit that left the circle before the impact takes no damage and no burn (dodge — Skills rule 11).");
            Require(MeteorLost(arriver, 10000) == MeteorDamage && f.ActiveDamage(arriver, out _),
                "M-T3 a unit that entered the circle before the impact is hit and burned (judged at the impact moment).");
            Require(MeteorLost(stayer, 10000) == MeteorDamage, "M-T3 a unit that stayed in the circle is hit.");
        }

        // ── M-T4 ────────────────────────────────────────────────────────

        /// <summary> M-T4 — 살아 남은 적 유닛만 화상(출처 −1 · 50 × 3), 건물 · 죽은 유닛 · 아군 · 밖 없음, 지연 0도 화상. </summary>
        private static void ValidateMeteorImpactBurn()
        {
            var f = new MeteorFixture();
            UnitData survivor = f.Unit(9620, UnitType.Tank, TeamId.Red, 5, 5);
            UnitData fragile = f.Unit(9621, UnitType.Pistoleer, TeamId.Red, 6, 5);  // 150에 죽는다
            UnitData ally = f.Unit(9622, UnitType.Tank, TeamId.Blue, 5, 6);
            UnitData outside = f.Unit(9623, UnitType.Tank, TeamId.Red, 9, 9);
            BuildingData castle = f.Castle(9624, TeamId.Red, 4, 5);
            Require(f.Fire(0, 5f, 5f), "M-T4 the Meteor must activate.");
            f.TickImpacts(15);

            Require(MeteorLost(survivor, 10000) == MeteorDamage && MeteorLost(castle, 5000) == MeteorDamage && !fragile.IsAlive,
                "M-T4 the impact deals 150 to the enemy unit and building in the circle and kills the 100-HP unit.");
            Require(f.ActiveDamage(survivor, out TimedDamageEffectPresentation burn)
                    && burn.Visual == TimedDamageVisual.Burn && burn.SourceId == -1
                    && Math.Abs((burn.EndTime - burn.StartTime) - MeteorBurnDuration) < 1e-6,
                "M-T4 the surviving enemy unit gets one Burn with source −1 lasting 3 s (InfernoSpirit burn presentation).");
            Require(!f.ActiveDamage(fragile, out _), "M-T4 a unit killed by the impact gets no burn.");
            Require(MeteorLost(ally, 10000) == 0 && !f.ActiveDamage(ally, out _), "M-T4 allies in the circle take no damage and no burn.");
            Require(MeteorLost(outside, 10000) == 0 && !f.ActiveDamage(outside, out _), "M-T4 enemies outside the circle take nothing.");

            // 화상 틱 — 1초마다 50(방어 감쇄 없음 — 유닛 규칙 44), 3번, 그 뒤 끝. 건물은 그대로(화상 없음 — 상위 결정 18).
            for (int s = 1; s <= 3; s++)
            {
                f.TickBurn(1);
                Require(MeteorLost(survivor, 10000) == MeteorDamage + 50 * s,
                    "M-T4 burn tick " + s + " deals 50 (lost " + MeteorLost(survivor, 10000) + ", expected " + (MeteorDamage + 50 * s) + ").");
            }
            f.TickBurn(2);
            Require(MeteorLost(survivor, 10000) == MeteorDamage + 150 && !f.ActiveDamage(survivor, out _),
                "M-T4 the burn ends after 3 ticks (total 150) and is no longer active.");
            Require(MeteorLost(castle, 5000) == MeteorDamage, "M-T4 the enemy building takes no burn damage.");

            // 결과 값(진단 MeteorBurnApplied의 근거) — 직접 호출로 확인.
            var g = new MeteorFixture();
            g.Unit(9630, UnitType.Tank, TeamId.Red, 5, 5);
            g.Unit(9631, UnitType.Pistoleer, TeamId.Red, 6, 5);
            g.Unit(9632, UnitType.Tank, TeamId.Blue, 5, 6);
            g.Castle(9633, TeamId.Red, 4, 5);
            SkillImpactBurnResult r = g.Combat.ApplySkillInstantAreaDamageWithBurn(TeamId.Blue, new Vector3(5f, 0f, 5f), 2f, MeteorDamage,
                g.Caster.Id, MeteorBurnPerSecond, MeteorBurnDuration);
            Require(r.UnitsInCircle == 2 && r.BuildingsInCircle == 1 && r.Survivors == 1 && r.BurnGranted == 1
                    && r.RefreshedBurn == 0 && r.ReplacedOther == 0,
                "M-T4 the burn result counts 2 enemy units / 1 building in the circle, 1 survivor, 1 burn (got " + r.UnitsInCircle + "/"
                + r.BuildingsInCircle + "/" + r.Survivors + "/" + r.BurnGranted + ").");

            // 화상 값 0 → 화상 단계 없음(같은 메서드가 포격 결과와 같다).
            var z = new MeteorFixture();
            UnitData zu = z.Unit(9640, UnitType.Tank, TeamId.Red, 5, 5);
            SkillImpactBurnResult zr = z.Combat.ApplySkillInstantAreaDamageWithBurn(TeamId.Blue, new Vector3(5f, 0f, 5f), 2f, MeteorDamage,
                z.Caster.Id, 0f, 0f);
            Require(MeteorLost(zu, 10000) == MeteorDamage && !z.ActiveDamage(zu, out _) && zr.BurnGranted == 0 && zr.Survivors == 1,
                "M-T4 with zero burn values only the impact damage is applied.");

            // 위험 R-9 — 착탄 지연 0 + 화상: 발동 순간 피해 + 화상(예약 없음).
            var d = new MeteorFixture(MeteorSkill(delay: 0f));
            UnitData du = d.Unit(9650, UnitType.Tank, TeamId.Red, 5, 5);
            Require(d.Fire(0, 5f, 5f) && d.Activation.PendingImpactCount == 0, "M-T4 a delay-0 Meteor applies at once (no reservation).");
            Require(MeteorLost(du, 10000) == MeteorDamage && d.ActiveDamage(du, out TimedDamageEffectPresentation db)
                    && db.Visual == TimedDamageVisual.Burn && db.SourceId == -1,
                "M-T4 the burn is kept when the impact delay is 0 (both bridges carry the burn values — risk R-9).");
        }

        // ── M-T5 ────────────────────────────────────────────────────────

        /// <summary> M-T5 — 이미 화상(유닛 출처)인 유닛에 착탄: 기록 1개 · 지속 리셋 · 값 = 새 쪽(출처 −1). </summary>
        private static void ValidateMeteorBurnRefresh()
        {
            var f = new MeteorFixture();
            UnitData source = f.Unit(9660, UnitType.Tank, TeamId.Blue, 0, 0);   // InfernoSpirit 역할(출처 유닛 — 종류는 결과와 무관)
            UnitData victim = f.Unit(9661, UnitType.Tank, TeamId.Red, 5, 5);
            f.Now = 100d;
            f.Combat.ApplyDamageOverTime(source, victim, MeteorBurnPerSecond, MeteorBurnDuration, 1f, TimedDamageVisual.Burn);
            Require(f.ActiveDamage(victim, out TimedDamageEffectPresentation first) && first.SourceId == source.Id,
                "M-T5 fixture — the unit-sourced burn must be active.");
            f.TickBurn(1); // 기존 화상 1틱(−50), Now = 101
            Require(MeteorLost(victim, 10000) == 50, "M-T5 fixture — the existing burn ticked once (50).");

            Require(f.Fire(0, 5f, 5f), "M-T5 the Meteor must activate.");
            f.TickImpacts(15); // 착탄 — Now는 그대로 101(지속 피해 틱은 돌리지 않았다)
            Require(f.ActiveDamage(victim, out TimedDamageEffectPresentation refreshed)
                    && refreshed.SameOwner(first) && refreshed.Revision > first.Revision
                    && refreshed.Visual == TimedDamageVisual.Burn && refreshed.SourceId == -1
                    && Math.Abs(refreshed.StartTime - 101d) < 1e-9 && Math.Abs(refreshed.EndTime - 104d) < 1e-9,
                "M-T5 the Meteor refreshes the same burn record (same owner, higher revision, source −1, duration reset to 101 → 104).");

            // 기록이 하나뿐인지 피해로 확인 — 둘이었으면 옛 기록의 남은 100이 더 들어와 총 450이 된다.
            f.TickBurn(5);
            Require(MeteorLost(victim, 10000) == 50 + MeteorDamage + 150 && !f.ActiveDamage(victim, out _),
                "M-T5 after the refresh only one burn record ticks (50 earlier + 150 impact + 150 new burn = 350, got "
                + MeteorLost(victim, 10000) + ").");
        }

        // ── M-T6 ────────────────────────────────────────────────────────

        /// <summary> M-T6 — 시전 건물 파괴 뒤에도 착탄 + 화상(U-5), 경기 종료 뒤에는 피해 · 화상 0. </summary>
        private static void ValidateMeteorCasterLostAndGameEnd()
        {
            var f = new MeteorFixture();
            UnitData e = f.Unit(9670, UnitType.Tank, TeamId.Red, 5, 5);
            Require(f.Fire(0, 5f, 5f), "M-T6 the Meteor must activate.");
            f.TickImpacts(5);
            f.Buildings.RemoveBuilding(f.Caster.Id);
            Require(f.Buildings.GetBuilding(f.Caster.Id) == null || !f.Buildings.GetBuilding(f.Caster.Id).IsAlive,
                "M-T6 fixture — the caster building must be gone before the impact.");
            f.TickImpacts(10);
            Require(MeteorLost(e, 10000) == MeteorDamage && f.ActiveDamage(e, out TimedDamageEffectPresentation s) && s.SourceId == -1,
                "M-T6 the Meteor still lands and burns after its caster building was destroyed (U-5).");

            var g = new MeteorFixture();
            UnitData ge = g.Unit(9671, UnitType.Tank, TeamId.Red, 5, 5);
            Require(g.Fire(0, 5f, 5f), "M-T6 ② the Meteor must activate.");
            g.TickImpacts(5);
            g.Activation.ClosePendingImpacts();
            Require(g.Activation.PendingImpactCount == 0, "M-T6 ② game end drops the pending Meteor.");
            g.TickImpacts(20);
            Require(MeteorLost(ge, 10000) == 0 && !g.ActiveDamage(ge, out _), "M-T6 ② no damage and no burn after the game ended.");
        }

        // ── M-T7 ────────────────────────────────────────────────────────

        /// <summary> M-T7 — 포격(화상 칸 0) 착탄 예약 · 피해 무변경, 화상 없음, 화면 메테오 경로 아님. </summary>
        private static void ValidateMeteorBombardmentUnchanged()
        {
            var f = new MeteorFixture();
            UnitData e = f.Unit(9680, UnitType.Tank, TeamId.Red, 5, 5);
            Require(f.Fire(1, 5f, 5f) && f.Activation.PendingImpactCount == 1, "M-T7 Bombardment schedules one impact.");
            f.TickImpacts(3);
            Require(MeteorLost(e, 10000) == 0, "M-T7 no Bombardment damage before 0.4 s.");
            f.TickImpacts(2); // 0.5초 — 기존 동작 그대로(0.4초는 float 오차로 다섯 번째 틱 — 공습 Task LS-T10과 같은 관찰)
            Require(MeteorLost(e, 10000) == MeteorDamage && !f.ActiveDamage(e, out _),
                "M-T7 Bombardment deals 150 once by 0.5 s and applies no burn (burn values 0).");
            Require(!MeteorBombardmentSkill().UsesImpactPresentation && !MeteorBombardmentSkill().HasImpactBurn
                    && MeteorSkill().UsesImpactPresentation && MeteorSkill().HasImpactBurn,
                "M-T7 only the Meteor data uses the impact presentation / burn path (Bombardment keeps the circle playback).");
        }

        // ── M-T9 ① ──────────────────────────────────────────────────────

        /// <summary> M-T9 ① — 예고 재생 속도 = 기준 시간 ÷ 착탄 지연(잘못된 값 → 1, 상한 · 하한) · 데이터 정리. </summary>
        private static void ValidateMeteorWarningSpeedMath()
        {
            Require(Math.Abs(SkillData.ComputeWarningPlaybackSpeed(1.5f, 1.5f) - 1f) < 1e-6f, "M-T9 reference 1.5 / delay 1.5 → speed 1.");
            Require(Math.Abs(SkillData.ComputeWarningPlaybackSpeed(1.5f, .75f) - 2f) < 1e-6f, "M-T9 reference 1.5 / delay 0.75 → speed 2.");
            Require(Math.Abs(SkillData.ComputeWarningPlaybackSpeed(1.5f, 3f) - .5f) < 1e-6f, "M-T9 reference 1.5 / delay 3 → speed 0.5.");
            Require(SkillData.ComputeWarningPlaybackSpeed(0f, 1.5f) == 1f && SkillData.ComputeWarningPlaybackSpeed(1.5f, 0f) == 1f
                    && SkillData.ComputeWarningPlaybackSpeed(float.NaN, 1.5f) == 1f && SkillData.ComputeWarningPlaybackSpeed(1.5f, float.NaN) == 1f
                    && SkillData.ComputeWarningPlaybackSpeed(float.PositiveInfinity, 1.5f) == 1f,
                "M-T9 a missing / bad reference time or delay keeps speed 1 (no adjustment).");
            Require(SkillData.ComputeWarningPlaybackSpeed(100f, .1f) == SkillData.MaxWarningPlaybackSpeed
                    && SkillData.ComputeWarningPlaybackSpeed(.01f, 10f) == SkillData.MinWarningPlaybackSpeed,
                "M-T9 the speed is clamped to [" + SkillData.MinWarningPlaybackSpeed + ", " + SkillData.MaxWarningPlaybackSpeed + "].");
            var bad = new SkillData(null, SkillAimType.PointTarget, 1f, SkillMechanicType.InstantAreaDamage, 2f, 0f, 1, 0f, 0, 0f, false, "", "",
                true, 1.5f, null, 2f, impactBurnDamagePerSecond: float.NaN, impactBurnDuration: -1f, warningVfxReferenceTime: float.NaN,
                fallingBodyStartOffset: new Vector3(float.NaN, 1f, 1f), fallingBodyEulerOffset: new Vector3(0f, float.PositiveInfinity, 0f));
            Require(bad.ImpactBurnDamagePerSecond == 0f && bad.ImpactBurnDuration == 0f && !bad.HasImpactBurn
                    && bad.WarningVfxReferenceTime == 0f && bad.FallingBodyStartOffset == Vector3.zero && bad.FallingBodyEulerOffset == Vector3.zero
                    && !bad.UsesImpactPresentation,
                "M-T9 SkillData sanitizes bad Meteor values (NaN / negative → 0, non-finite offsets → zero) and then uses neither path.");
        }

        // ── M-T8 · M-T10 · 소스 게이트 ───────────────────────────────────

        /// <summary> M-T8 — 셋업 기대 수 14 · 메테오 표 줄, M-T10 — 스키마 12(2026-11-23 연막탄으로 13 → 14 · 11 → 12), 소스 게이트(서버 · 화면 연결). </summary>
        private static void ValidateMeteorSourceAndSetupGates()
        {
            // M-T10 — 메테오는 RPC · 동기화 형식을 바꾸지 않는다(Plan O-2 · Research 12절 끝).
            // [2026-11-23 연막탄 Task 단계 S-3] 11 → 12 — 서버 권위 투사체 발사 RPC에 회피 여부 · 빗나간 지점이 붙었다(이 검사 대상과 무관한 변경).
            Require(UnitAttackShadowProfileResolver.PresentationContractSchemaRevision == 12,
                "M-T10 the Meteor adds no RPC or sync field — the presentation schema is 12 only because of the smoke-bomb projectile launch RPC arguments.");

            // M-T8 — 셋업 기대 수 14(다른 스위트 5파일의 같은 검사와 같은 값. 2026-11-23 연막탄 — 직전 「13」).
            FieldInfo expected = typeof(Hexiege.EditorTools.SupportSkillLoadoutSetup).GetField("ExpectedAvailableCount",
                BindingFlags.NonPublic | BindingFlags.Static);
            // [2026-11-23 연막탄 Task S-1] 13 → 14 — 연막탄(지속 상태 구역 · 아군 대상 · 회피 확률)을 「사용 가능」으로 켰다.
            Require(expected != null && (int)expected.GetRawConstantValue() == 14,
                "M-T8 SupportSkillLoadoutSetup.ExpectedAvailableCount must be 14 (Meteor enabled, then Smoke Bomb).");
            string setup = File.ReadAllText("Assets/Editor/Setup/SupportSkillLoadoutSetup.cs");
            Require(SupportActiveLineCount(setup, "PointPlaceholder(\"Skill_Spirit_Meteor\"") == 0
                    && SupportActiveLineCount(setup, "FileName = \"Skill_Spirit_Meteor\", Race = s, Slot = 0,") == 1
                    && SupportActiveLineCount(setup, "ImpactBurnPerSecond = 50f, ImpactBurnDuration = 3f") == 1
                    && SupportActiveLineCount(setup, "ImpactDelay = 1.5f, UseBombardmentVfx = false,") == 1
                    && SupportActiveLineCount(setup, "meteor,") == 1,
                "M-T8 the setup table must list the Meteor as an available type-A spec with delay 1.5 and burn 50 × 3 (not the old placeholder).");

            const string root = "Assets/_Project/Scripts/";
            string skill = File.ReadAllText(root + "Application/UseCases/SkillActivationUseCase.cs");
            Require(SupportActiveLineCount(skill, "SelectInstantAreaDamageBridge(skill.ImpactDelay, sequence, skill.ImpactBurnDamagePerSecond, skill.ImpactBurnDuration),") == 1
                    && SupportActiveLineCount(skill, "ApplyPendingImpactDamage(p);") == 1,
                "M-T0 the type-A bridge must carry the burn values and impacts must go through ApplyPendingImpactDamage (P-2 · P-3).");

            string presenter = File.ReadAllText(root + "Presentation/Effects/SkillActivationVfxPresenter.cs");
            int meteorBranch = presenter.IndexOf("if (skill != null && skill.UsesImpactPresentation)", StringComparison.Ordinal);
            int noPrefab = presenter.IndexOf("if (skill == null || skill.ActivationVfxPrefab == null)", StringComparison.Ordinal);
            Require(meteorBranch >= 0 && noPrefab > meteorBranch && SupportActiveLineCount(presenter, "PlayMeteor(p, channel, skill);") == 1
                    && SupportActiveLineCount(presenter, "InitializeMeteor();") == 1 && SupportActiveLineCount(presenter, "DisposeMeteorSubscription();") == 1,
                "M-T9 the Meteor branch must run before the circle NoPrefab check (body / impact play even with an empty warning slot) and be initialized / disposed.");
            string line = File.ReadAllText(root + "Presentation/Effects/SkillActivationVfxPresenter.LineStrike.cs");
            Require(SupportActiveLineCount(line, "UpdateMeteorVfx();") == 1, "M-T9 the presenter Update must drive the Meteor body stop / impact playback.");
            string pool = File.ReadAllText(root + "Presentation/Effects/VfxPoolItem.cs");
            Require(SupportActiveLineCount(pool, "RestorePlaybackSpeed();") == 1 && SupportActiveLineCount(pool, "PollStatusLoopDeadline();") == 1,
                "M-T9 the pool item must restore a changed playback speed on reuse / return and poll the body safety deadline (R-6 · R-7).");
            string loop = File.ReadAllText(root + "Presentation/Effects/VfxPoolItem.StatusLoop.cs");
            Require(SupportActiveLineCount(loop, "ResetStatusLoopDeadline();") == 1, "M-T9 a reused pool item must not keep an old body safety deadline.");
        }

        // ── M-T1 ────────────────────────────────────────────────────────

        private const string MeteorAssetPath = "Assets/_Project/Resources/Config/Skills/Skill_Spirit_Meteor.asset";

        /// <summary> M-T1 — 메테오 자산 계약(값 · 정령 슬롯 0 · 채워진 VFX 칸의 재생 계약). </summary>
        private static void ValidateMeteorAssetContract()
        {
            SkillDefinition def = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDefinition>(MeteorAssetPath);
            Require(def != null, "M-T1 Skill_Spirit_Meteor asset must exist: " + MeteorAssetPath);
            SkillData m = def.ToSkillData();
            Require(m.IsAvailable && m.AimType == SkillAimType.PointTarget && m.Mechanic == SkillMechanicType.InstantAreaDamage,
                "M-T1 Meteor must be available / point target / type A (InstantAreaDamage).");
            Require(Math.Abs(m.ImpactDelay - MeteorDelay) < 1e-4f && m.TotalDamage == MeteorDamage && Math.Abs(m.Radius - 2f) < 1e-4f
                    && Math.Abs(m.Cooldown - MeteorCooldown) < 1e-4f
                    && Math.Abs(m.ImpactBurnDamagePerSecond - MeteorBurnPerSecond) < 1e-4f && Math.Abs(m.ImpactBurnDuration - MeteorBurnDuration) < 1e-4f,
                "M-T1 Meteor values must be delay 1.5 / damage 150 / radius 2 / cooldown 10 / burn 50 × 3 (temporary — U-7 · U-8).");
            Require(Math.Abs(m.WarningVfxReferenceTime - 1.5f) < 1e-4f && m.UsesImpactPresentation,
                "M-T1 Meteor warning reference time must be 1.5 (speed 1 at delay 1.5) and use the impact presentation path.");

            SkillLoadoutConfig loadout = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillLoadoutConfig>(
                "Assets/_Project/Resources/Config/Skills/SkillLoadoutConfig.asset");
            Require(loadout != null, "M-T1 SkillLoadoutConfig asset must exist.");
            loadout.ClearCache();
            var spirit = loadout.GetLoadout(RaceId.Spirit);
            Require(spirit != null && spirit.Count > 0 && spirit[0] != null && spirit[0].DisplayName == m.DisplayName && spirit[0].IsAvailable
                    && spirit[0].HasImpactBurn,
                "M-T1 Meteor must be the available burning type-A skill in Spirit slot 0 (button 1).");

            // 채워진 칸만 계약 검사(빈 칸은 통과 — 테스트 뒤 비운다, U-1). 예고 · 착탄 = 1회 재생(반복 없음), 몸체 = 반복 계약.
            foreach (GameObject once in new[] { m.ActivationVfxPrefab, m.ImpactVfxPrefab })
            {
                if (once == null) continue;
                foreach (ParticleSystem ps in once.GetComponentsInChildren<ParticleSystem>(true))
                    Require(!ps.main.loop, "M-T1 Meteor one-shot VFX " + once.name + "/" + ps.name + " must not loop (played once, then returned to the pool).");
            }
            if (m.FallingBodyVfxPrefab != null)
                Require(EffectManager.CheckUnitBuffLoopContract(m.FallingBodyVfxPrefab),
                    "M-T1 a filled Meteor body VFX must satisfy the loop contract (Looping ON, Stop Action None) — it is stopped by lease.");
        }

        // ── M-T9 ② ──────────────────────────────────────────────────────

        /// <summary> M-T9 ② — 풀 항목에서 바꾼 재생 속도가 Return 뒤 원래 값 · 새 Play에 남지 않음 · 안전 마감은 맞는 확인 번호에만. </summary>
        private static void ValidateMeteorWarningSpeedPoolRestore()
        {
            GameObject vfx = null;
            try
            {
                vfx = new GameObject("MeteorWarningSpeedFixture");
                ParticleSystem ps = vfx.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = ps.main;
                main.loop = false;
                main.simulationSpeed = 1.25f; // 프리팹 원래 속도가 1이 아니어도 「원래 × 배율」로 맞추는지 본다
                VfxPoolItem item = vfx.AddComponent<VfxPoolItem>();
                int returned = 0;
                item.Setup(vfx, _ => returned++);

                item.Play(Vector3.zero, Quaternion.identity, Vector3.zero, Vector3.zero, Vector3.one, 0f, 0f);
                Require(item.SetPlaybackSpeed(2f) && Mathf.Approximately(ps.main.simulationSpeed, 2.5f)
                        && item.PlaybackSpeedEquals(2f) && Mathf.Approximately(item.PlaybackSpeedMultiplier, 2f),
                    "M-T9 the warning speed 2 multiplies the original simulation speed (1.25 → 2.5).");
                item.Return();
                Require(returned == 1 && Mathf.Approximately(ps.main.simulationSpeed, 1.25f) && item.PlaybackSpeedEquals(1f)
                        && Mathf.Approximately(item.PlaybackSpeedMultiplier, 1f),
                    "M-T9 returning to the pool restores the original simulation speed (R-7).");

                item.Play(Vector3.zero, Quaternion.identity, Vector3.zero, Vector3.zero, Vector3.one, 0f, 0f);
                Require(!item.SetPlaybackSpeed(1f) && Mathf.Approximately(ps.main.simulationSpeed, 1.25f),
                    "M-T9 speed 1 changes nothing (other skills keep the authored speed).");
                Require(item.SetPlaybackSpeed(.5f) && Mathf.Approximately(ps.main.simulationSpeed, .625f), "M-T9 speed 0.5 slows the warning.");
                item.Play(Vector3.zero, Quaternion.identity, Vector3.zero, Vector3.zero, Vector3.one, 0f, 0f);
                Require(Mathf.Approximately(ps.main.simulationSpeed, 1.25f), "M-T9 a new Play restores the speed before reuse.");

                // 안전 마감 — 상태 루프 확인 번호가 맞을 때만 걸린다(Edit 모드라 실제 발화는 보지 않는다 — 머리말).
                ulong lease = item.BeginStatusLoop();
                Require(lease != 0 && item.SetStatusLoopDeadline(lease, 2f, "Seq=0") && !item.SetStatusLoopDeadline(lease + 1, 2f, "Seq=0")
                        && !item.SetStatusLoopDeadline(lease, 0f, "Seq=0"),
                    "M-T9 the body safety deadline accepts only the current lease and a positive time.");
                Require(item.StopStatusLoop(lease, clear: true) && !item.SetStatusLoopDeadline(lease, 2f, "Seq=0"),
                    "M-T9 a stopped body cannot get a new deadline.");
            }
            finally
            {
                if (vfx != null) UnityEngine.Object.DestroyImmediate(vfx);
            }
        }
    }
}
