// ============================================================================
// RunUnitActionSelfValidation.SupportNewStatuses.cs
// [2026-11-16 지원 스킬 새 상태 — 단계 A] 대지의 갑주(받는 피해 배율) · 전술 지령 생산 속도 회귀 검증(fixture).
// Task: Assets/_Project/Docs/_Tasks/2026-11-16/01_00_support-skill-new-statuses/ (Plan 4절 A-4 · Research 11-4 중 단계 A 몫).
// Run()(메뉴 「Hexiege/Combat/Run Full Regression Suite」)이 함께 돌린다.
//
// [배경 — 초급 개발자용 설명]
//   피해가 HP에 들어가는 서버 자리 6곳을 단일 헬퍼(IncomingDamage.Apply)로 모았다. 한 곳이라도 빠지거나, 효과가 없는데 값이
//   달라지면(회귀) 눈으로는 알아차리기 어렵다(HP 바가 없다). 그래서 실제 전투 · 탑 UseCase의 6곳을 직접 불러 숫자로 확인한다.
//   싱글 · 멀티 실기에서 함께 보기 어려운 지점(③ 포격 · ⑤ 즉발 탑 · ⑥ 대포탑 — Plan 8절 표)도 여기서 덮는다.
//
// 무엇을 확인하는가:
//   S-T1 반올림 · 최소 1 — 10→7, 5→4(3.5 AwayFromZero), 2→1(1.4), 1→1(최소 1), 0→0, 음수 그대로, 배율 1 · NaN이면 그대로.
//   S-T2 무효과 회귀(A-L12) — 서비스가 없을 때와 있지만 효과가 없을 때, 6곳 피해가 헬퍼 도입 전 계산과 정확히 같다.
//   S-T3 6곳 감소 — 대지의 갑주가 걸린 유닛은 6곳 모두 「반올림(기존 값 × 0.7) · 최소 1」, 건물 · 상대 팀 · 발동 뒤 새 유닛은 그대로.
//   S-T4 수명 — 재사용 = 남은 시간 교체 · 8초 뒤 정확히 1 · 사망 통보 · 틱 감지 정리 · Close 뒤 거부 · 지원 안 하는 조합 · 잘못된 값 거부.
//   S-T5 생산 속도(서버) — dt × 배율(진행 중 생산에도 즉시) · 5초 × 1.5배 → 3.4초째 100%(0.1초 틱) · 효과 끝 뒤 원래 속도 ·
//        효과 중 새로 등록한 생산 건물에도 적용(팀 단위 — S-9) · 배율 창구 없으면 기존과 같음.
//   S-T6 생산 막대 거울(순수 Client) — 전술 지령 발동 알림 → 같은 배율로 막대 · 8초 뒤 원래 · 대지의 갑주는 무시 · Close.
//   S-T7 실행기 — 대지의 갑주는 지원 효과 다리(유닛 · 0.7 · 8)만, 전술 지령은 상태 다리(공격력 1.5) + 지원 효과 다리(팀 · 생산 1.5).
//        옛 9인자 컨텍스트(지원 다리 없음)도 전술 지령 공격력은 그대로 건다(B-5 보호).
//   S-T8 자산 — 대지의 갑주(사용 가능 · 즉시 · 타입 C · 종류 6 · 0.7 · 8초 · 쿨 15 · 아군 · 정령 슬롯 3) · 전술 지령 추가 효과 1줄 ·
//        재생의 뿌리 · 신목의 각성은 여전히 사용 불가 · 채워진 유닛 상태 VFX 칸은 상태 이펙트 계약.
//        (2026-11-17 — 「재생의 뿌리 사용 불가」 검사는 단계 B 테스트 통과 뒤 삭제. 재생의 뿌리 자산 계약은 SupportRegen.cs R-T8.)
//   S-T9 소스 게이트 — 6곳 헬퍼 호출 · 남은 직접 TakeDamage 0 · 틱 자리(구역 틱 뒤 · 상태 틱 앞, 싱글 !IsNetworkMode) ·
//        경기 종료 닫기 · 주입 · 셋업 기대 수 9 · 스키마 9 유지.
//
// 실제 씬 · 멀티 전송 · 화면 막대 모양은 여기서 확인하지 않는다(사용자 실기 항목 — Plan 8절).
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
using UnityEditor;
using UnityEngine;

namespace Hexiege.Editor.Combat
{
    public static partial class RunUnitActionSelfValidation
    {
        private const string EarthArmorAssetPath = "Assets/_Project/Resources/Config/Skills/Skill_Spirit_EarthArmor.asset";
        private const string TacticalOrderAssetPath = "Assets/_Project/Resources/Config/Skills/Skill_Human_TacticalOrder.asset";
        private const string RootsAssetPath = "Assets/_Project/Resources/Config/Skills/Skill_Trans_RootsOfRegeneration.asset";
        private const string SacredTreeAssetPath = "Assets/_Project/Resources/Config/Skills/Skill_Trans_SacredTreeAwakening.asset";
        private const BindingFlags SupportNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>
        /// 지원 스킬 새 상태 단계 A 회귀 검증 진입점(S-T1 ~ S-T9).
        /// </summary>
        private static void ValidateSupportNewStatusesStageA()
        {
            Require(!UnityEngine.Application.isPlaying, "Support new-status fixture must run outside a live match.");
            // [2026-11-17 스위트 실패 수정 — 「S-T2 ② splash fixture target HP 10 must exceed the expected damage 55」]
            //   이 검사의 대상 유닛 HP · 공격자 공격력은 전역 정적 표 UnitStats에서 온다(SpawnUnitWithId → UnitStats.GetMaxHp).
            //   메뉴로 스위트를 돌리면 그 표가 비어 있어(null — 게임 부트스트랩이 안 돌았고, 앞선 검사들도 끝나면 원래 null로 되돌린다)
            //   Tank HP가 폴백 10, Pistoleer 공격력이 폴백 1이 되어 ② 이후 지점이 「HP > 기대 피해」 전제를 못 지켰다.
            //   이 줄이 이 메서드가 끝날 때까지만 필요한 행을 고정값으로 넣고, 끝나면(예외로 끝나도) 원래 표를 그대로 되돌린다.
            using var fixtureStats = new SupportFixtureStatsScope();
            ValidateSupportDamageScaling();        // S-T1
            ValidateSupportDamagePoints(false);    // S-T2 (무효과 회귀)
            ValidateSupportDamagePoints(true);     // S-T3 (6곳 감소)
            ValidateSupportEffectLifecycle();      // S-T4
            ValidateSupportProductionSpeed();      // S-T5
            ValidateSupportProductionMirror();     // S-T6
            ValidateSupportExecutorRouting();      // S-T7
            ValidateSupportNewStatusAssets();      // S-T8
            ValidateSupportNewStatusSourceGates(); // S-T9
            Debug.Log("[UAS-DIAG][SKILL-ARMOR] self-validation PASS: rounding AwayFromZero + minimum 1 (S-T1), no-effect regression exact at all six damage points with and without the service (S-T2), Earth Armor ×0.7 at all six points while buildings / enemy team / units born after activation stay unchanged (S-T3), replace / expiry / death / Close / rejection lifecycle (S-T4), production dt × team multiplier incl. in-progress and newly registered barracks then exact restore (S-T5), pure-client progress mirror (S-T6), executor routing to the support bridge with Tactical Order attack path unchanged (S-T7), Earth Armor / Tactical Order asset contract (S-T8), six helper call sites + tick / close / injection / setup 12 / schema 11 source gates (S-T9).");
        }

        // ── 피해 지점 fixture ─────────────────────────────────────────────

        private sealed class SupportDamageFixture
        {
            public readonly UnitSpawnUseCase Spawn;
            public readonly BuildingPlacementUseCase Buildings;
            public readonly UnitCombatUseCase Combat;
            public readonly TowerCombatUseCase Tower;
            public readonly SupportSkillEffectSystem Effects;

            public SupportDamageFixture(bool withService)
            {
                var grid = new HexGrid(3, 3, HexOrientation.FlatTop);
                Spawn = new UnitSpawnUseCase(grid);
                Buildings = new BuildingPlacementUseCase(grid);
                var mapper = new ProjectileFixtureMapper();
                Combat = new UnitCombatUseCase(grid, Spawn, Buildings, new ProjectileFixturePositions(), mapper);
                Tower = new TowerCombatUseCase(null, Spawn, mapper, null, _ => RaceId.Spirit);
                if (withService)
                {
                    Effects = new SupportSkillEffectSystem(Spawn);
                    Combat.SetSupportSkillEffectSystem(Effects);
                    Tower.SetSupportSkillEffectSystem(Effects);
                }
            }

            // 그리드 안 타일(0, 0)에 만든 뒤 판정용 좌표로 옮긴다(지속 상태 구역 fixture와 같은 방식 — 스킬 범위 판정은 Position만 읽는다).
            public UnitData Unit(int id, TeamId team, int q, int r, UnitType type = UnitType.Pistoleer)
            {
                UnitData unit = Spawn.SpawnUnitWithId(id, type, team, new HexCoord(0, 0));
                Require(unit != null, "S-T0 support fixture unit must spawn: " + id);
                unit.Position = new HexCoord(q, r);
                return unit;
            }
        }

        // ── fixture 전용 스탯 고정(2026-11-17) ─────────────────────────────
        //
        // [왜 필요한가 — 초급 개발자용 설명]
        //   UnitStats · BuildingStats는 「게임 전체가 함께 쓰는 정적(static) 표」다. 실제 게임에서는 GameBootstrapper가
        //   설정 에셋 값을 넣지만, 에디터 메뉴로 스위트를 돌리면 아무도 넣지 않아 비어 있다(null). 비어 있으면 조회 함수가
        //   폴백 값을 준다 — 유닛 HP 10 · 공격력 1 · 방어 0, 초월 Castle HP 200(UnitStats.GetMaxHp / BuildingStats.GetDefaultMaxHp).
        //   또 앞선 검사들(SoloTiming · Fox · 서버 투사체)은 자기 값으로 바꿨다가 끝나면 「원래 값(null)」으로 되돌린다.
        //   그래서 이 검사들이 그 순간의 전역 값에 기대면, 스위트 안 순서나 그 전에 게임을 실행했는지에 따라 결과가 달라진다.
        //
        // [무엇을 하는가]
        //   만들 때: 「빈 표」에서 시작해(2026-11-17 두 번째 수정 — 아래 설명) 이 검사들이 쓰는 행만 고정값으로 넣는다.
        //     - Pistoleer(공격자) 공격력 60: 0이 아니고, 쉴드 Sh-T3의 「공격력 > 20」 전제를 만족한다.
        //     - Tank(피격 대상) HP 10000 · 방어 0: 가장 큰 기대 피해(③ 포격 150 · ⑤ Spirit AutoTower 100)보다 훨씬 크다.
        //     - 초월 Castle HP 2000: Sh-T3에서 공격력 60을 두 번 맞아도 살아 있다.
        //     - 초월 TrainingCamp · WarAcademy HP 1000: Sh-T5가 훈련소를 놓고(살아 있어야 한다) 업그레이드한다(다음 단계 = WarAcademy).
        //     - 정령 AutoTower(공격력 100 · 쿨 1 · 사거리 3 · HP 1000): S-T2 / S-T3 ⑤ 탑 즉발 피해가 이 공격력을 읽는다.
        //   표에 없는 행은 조회 함수의 코드 폴백(상수)을 쓴다 — 전역 상태와 무관하게 언제나 같은 값이다.
        //   끝날 때(Dispose): 두 표의 「원래 참조」를 그대로 되돌린다(null이었으면 null로) — 뒤따르는 검사가 보는 전역 상태는 이전과 같다.
        //   (Initialize는 사본을 만들므로 되돌릴 때는 Initialize가 아니라 필드에 원래 참조를 직접 넣는다 — 서버 투사체 fixture와 같은 방식.)
        //
        // [2026-11-17 두 번째 스위트 실패 — 「Sh-T5 fixture TrainingCamp must be placed alive」 — 왜 복사를 그만두었나]
        //   처음 버전은 「지금 표를 복사」한 뒤 몇 행만 덮었다. 그런데 에디터에서 게임을 한 번 실행(Play)하고 멈추면,
        //   GameBootstrapper가 넣은 「실제 설정 에셋 표」가 정적 필드에 그대로 남는다(다음 스크립트 재컴파일 전까지).
        //   실제 BuildingStatsConfig에서 TrainingCamp는 인간 전용 건물이라 초월(Transcendence) HP가 0이다(BuildingStatsConfig.asset
        //   buildingType: 7 → transcendenceMaxHp: 0). 그 표를 복사하면 Sh-T5의 초월 훈련소가 HP 0(= IsAlive false)으로 놓인다.
        //   표가 비어 있을 때(재컴파일 직후)는 폴백 48이라 통과했다 — 같은 코드가 「그 전에 Play를 했는지」에 따라 갈렸다.
        //   그래서 복사하지 않고 빈 표 + 고정 행으로 바꿨다. 검사가 새 행을 쓰면 여기에 고정 행을 더한다.
        private sealed class SupportFixtureStatsScope : IDisposable
        {
            public const int AttackerAttackPower = 60;
            public const int VictimTankMaxHp = 10000;
            public const int CastleTranscendenceMaxHp = 2000;
            public const int ProductionTranscendenceMaxHp = 1000;
            public const int SpiritTowerAttackPower = 100;

            private static readonly FieldInfo UnitDataField =
                typeof(UnitStats).GetField("_data", BindingFlags.Static | BindingFlags.NonPublic);
            private static readonly FieldInfo BuildingDataField =
                typeof(BuildingStats).GetField("_data", BindingFlags.Static | BindingFlags.NonPublic);

            private readonly object _oldUnitData;
            private readonly object _oldBuildingData;
            private bool _disposed;

            public SupportFixtureStatsScope()
            {
                Require(UnitDataField != null && BuildingDataField != null,
                    "S-T0 support fixture stat seams (UnitStats._data / BuildingStats._data) must exist.");
                _oldUnitData = UnitDataField.GetValue(null);
                _oldBuildingData = BuildingDataField.GetValue(null);

                // 유닛 표 — 빈 표에 두 행만 넣는다(원래 표는 건드리지 않는다 — 새 Dictionary).
                var units = new Dictionary<UnitType, UnitStats.StatValues>();
                units[UnitType.Pistoleer] = new UnitStats.StatValues { MaxHp = 300, AttackPower = AttackerAttackPower, AttackRange = 3f,
                    DetectRange = 3f, MoveSpeed = 1f, AttackCooldown = 1f, HitFrameTimes = new[] { .2f }, Defense = 0 };
                units[UnitType.Tank] = new UnitStats.StatValues { MaxHp = VictimTankMaxHp, AttackPower = 50, AttackRange = 3f,
                    DetectRange = 3f, MoveSpeed = .5f, AttackCooldown = 2f, HitFrameTimes = new[] { .4f }, Defense = 0 };
                UnitStats.Initialize(units);

                // 건물 표 — 마찬가지로 빈 표에 이 검사들이 쓰는 행만 넣는다.
                var buildings = new Dictionary<(BuildingType, RaceId), BuildingStats.StatValues>();
                buildings[(BuildingType.Castle, RaceId.Transcendence)] = new BuildingStats.StatValues { MaxHp = CastleTranscendenceMaxHp };
                // Sh-T5 — 초월 훈련소 배치 · 업그레이드(TrainingCamp → WarAcademy). 실제 표에서는 둘 다 초월 HP 0이다(인간 전용 건물).
                buildings[(BuildingType.TrainingCamp, RaceId.Transcendence)] = new BuildingStats.StatValues { MaxHp = ProductionTranscendenceMaxHp };
                buildings[(BuildingType.WarAcademy, RaceId.Transcendence)] = new BuildingStats.StatValues { MaxHp = ProductionTranscendenceMaxHp };
                // S-T2 / S-T3 ⑤ — 정령 탑 즉발 공격력. 값은 ⑤의 「행이 없으면 넣는」 예비 값과 같다(그래서 그 예비 분기는 이제 타지 않는다).
                buildings[(BuildingType.AutoTower, RaceId.Spirit)] = new BuildingStats.StatValues
                    { MaxHp = 1000, AttackPower = SpiritTowerAttackPower, AttackCooldown = 1f, AttackRange = 3f };
                BuildingStats.Initialize(buildings);

                Require(UnitStats.GetMaxHp(UnitType.Tank) == VictimTankMaxHp && UnitStats.GetAttackPower(UnitType.Pistoleer) == AttackerAttackPower
                        && UnitStats.GetDefense(UnitType.Tank) == 0
                        && BuildingStats.GetMaxHp(BuildingType.Castle, RaceId.Transcendence) == CastleTranscendenceMaxHp
                        && BuildingStats.GetMaxHp(BuildingType.TrainingCamp, RaceId.Transcendence) == ProductionTranscendenceMaxHp
                        && BuildingStats.GetMaxHp(BuildingType.WarAcademy, RaceId.Transcendence) == ProductionTranscendenceMaxHp
                        && BuildingStats.GetAttackPower(BuildingType.AutoTower, RaceId.Spirit) == SpiritTowerAttackPower,
                    "S-T0 support fixture stats must be pinned (Tank HP " + VictimTankMaxHp + ", Pistoleer attack " + AttackerAttackPower
                    + ", Transcendence Castle HP " + CastleTranscendenceMaxHp + ", Transcendence TrainingCamp / WarAcademy HP "
                    + ProductionTranscendenceMaxHp + ", Spirit AutoTower attack " + SpiritTowerAttackPower + ").");
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                UnitDataField.SetValue(null, _oldUnitData);
                BuildingDataField.SetValue(null, _oldBuildingData);
            }
        }

        // 대상 HP가 실제로 몇 줄었는지 잰다(피해 뒤 살아 있어야 다음 확인이 뜻이 있다).
        private static int SupportHpDrop(IDamageable target, Action hit)
        {
            int before = target.Hp;
            hit();
            return before - target.Hp;
        }

        /// <summary> S-T1 — 배율 곱의 반올림 · 최소 1. </summary>
        private static void ValidateSupportDamageScaling()
        {
            Require(IncomingDamage.ScaleDamage(10, 0.7f) == 7, "S-T1 10 × 0.7 must be 7.");
            Require(IncomingDamage.ScaleDamage(5, 0.7f) == 4, "S-T1 5 × 0.7 = 3.5 must round AwayFromZero to 4 (no float 3.4999 error).");
            Require(IncomingDamage.ScaleDamage(2, 0.7f) == 1, "S-T1 2 × 0.7 = 1.4 must round to 1.");
            Require(IncomingDamage.ScaleDamage(1, 0.7f) == 1, "S-T1 1 × 0.7 = 0.7 must clamp to the minimum 1.");
            Require(IncomingDamage.ScaleDamage(1000, 0.7f) == 700, "S-T1 1000 × 0.7 must be 700.");
            Require(IncomingDamage.ScaleDamage(0, 0.7f) == 0 && IncomingDamage.ScaleDamage(-3, 0.7f) == -3,
                "S-T1 damage ≤ 0 must pass through unchanged (no minimum 1 — same as DamageCalculator.ApplyDefense).");
            Require(IncomingDamage.ScaleDamage(123, 1f) == 123 && IncomingDamage.ScaleDamage(123, float.NaN) == 123
                    && IncomingDamage.ScaleDamage(123, -0.5f) == 123,
                "S-T1 multiplier 1 / NaN / negative must leave damage unchanged.");
            Require(IncomingDamage.ScaleDamage(7, 0f) == 1, "S-T1 multiplier 0 still deals the minimum 1.");
        }

        /// <summary>
        /// S-T2 (armor=false) — 효과가 없으면 6곳 피해가 기존 계산과 정확히 같다(서비스 없음 · 서비스 있지만 효과 없음 둘 다).
        /// S-T3 (armor=true)  — 대지의 갑주가 걸린 유닛은 6곳 모두 반올림(기존 × 0.7)·최소 1, 건물 · 상대 팀 · 새 유닛은 그대로.
        /// </summary>
        private static void ValidateSupportDamagePoints(bool armor)
        {
            string tag = armor ? "S-T3" : "S-T2";
            bool[] serviceModes = armor ? new[] { true } : new[] { false, true };
            foreach (bool withService in serviceModes)
            {
                var f = new SupportDamageFixture(withService);
                UnitData attacker = f.Unit(9700, TeamId.Red, 0, 0);
                Require(attacker.AttackPower > 0, tag + " fixture attacker must have attack power (UnitStats initialised).");
                var victims = new List<UnitData>();
                // 서로 다른 좌표(③ 포격 원 반경 0.1이 한 유닛만 잡도록 — 서로 1 이상 떨어뜨린다).
                for (int i = 0; i < 6; i++) victims.Add(f.Unit(9710 + i, TeamId.Blue, 10 + 3 * i, 10, UnitType.Tank));
                UnitData enemyTeamUnit = f.Unit(9720, TeamId.Red, 40, 10, UnitType.Tank);

                if (armor)
                {
                    int applied = f.Effects.Apply(901, TeamId.Blue, 3, 77, TeamId.Blue, SkillEffectTarget.Unit,
                        StatusEffectKind.IncomingDamageMul, 0.7f, 8f);
                    Require(applied == 6, "S-T3 Earth Armor must be applied to the six living Blue units at activation (got " + applied + ").");
                }
                float mul = armor ? 0.7f : 1f;
                int Expect(int oldDamage) => armor ? IncomingDamage.ScaleDamage(oldDamage, mul) : oldDamage;

                // ① 직접 · 휩쓸기 경로(2-인자 피해 다리 → 5-인자 본문 → 헬퍼).
                MethodInfo direct = typeof(UnitCombatUseCase).GetMethod("ApplyDamageToVictim", SupportNonPublic, null,
                    new[] { typeof(UnitData), typeof(IDamageable) }, null);
                Require(direct != null, tag + " ApplyDamageToVictim(UnitData, IDamageable) must exist (damage point ①).");
                int old1 = DamageCalculator.ApplyDefense(attacker.AttackPower, victims[0].Defense);
                RequireSupportHit(tag, "① direct/sweep", victims[0], Expect(old1),
                    () => direct.Invoke(f.Combat, new object[] { attacker, victims[0] }));

                // ② QuakeSpirit 스플래시(고정량).
                MethodInfo splash = typeof(UnitCombatUseCase).GetMethod("ApplyFixedDamageToVictim", SupportNonPublic);
                Require(splash != null, tag + " ApplyFixedDamageToVictim must exist (damage point ②).");
                int old2 = DamageCalculator.ApplyDefense(55, victims[1].Defense);
                RequireSupportHit(tag, "② splash", victims[1], Expect(old2),
                    () => splash.Invoke(f.Combat, new object[] { attacker, victims[1], 55, false }));

                // ③ 스킬 타입 A(포격) — 공개 경로. 시전 팀 Red, 대상 victims[2]의 타일 중심(fixture 매퍼 = (q, 0, r)), 반경 0.1.
                int old3 = DamageCalculator.ApplyDefense(150, victims[2].Defense);
                RequireSupportHit(tag, "③ skill", victims[2], Expect(old3),
                    () => f.Combat.ApplySkillInstantAreaDamage(TeamId.Red, new Vector3(victims[2].Position.Q, 0f, victims[2].Position.R), 0.1f, 150, 55));

                // ④ 지속 피해 1틱(방어 감쇄 없음).
                MethodInfo dot = typeof(UnitCombatUseCase).GetMethod("ApplyTimedDamageToUnit", SupportNonPublic);
                Require(dot != null, tag + " ApplyTimedDamageToUnit must exist (damage point ④).");
                RequireSupportHit(tag, "④ DoT tick 2", victims[3], Expect(2),
                    () => dot.Invoke(f.Combat, new object[] { attacker, victims[3], 2 }));
                RequireSupportHit(tag, "④ DoT tick 20", victims[3], Expect(20),
                    () => dot.Invoke(f.Combat, new object[] { attacker, victims[3], 20 }));

                // ⑤ RuneSpire · VineTower 즉발(실제 탑 공격력 — 앞선 VfxConnections 검사가 Spirit AutoTower 행을 넣는다. 없으면 같은 방식으로 넣는다).
                //   [2026-11-17 메모] 이제 SupportFixtureStatsScope가 이 행(공격력 100)을 고정해 넣으므로 아래 「없으면 넣기」 분기는 타지 않는다.
                //   (VfxConnections는 끝날 때 원래 표를 되돌리므로 그 행이 여기까지 남아 있지도 않다.)
                int power = BuildingStats.GetAttackPower(BuildingType.AutoTower, RaceId.Spirit);
                if (power <= 0)
                {
                    BuildingStats.Initialize(new Dictionary<(BuildingType, RaceId), BuildingStats.StatValues>
                    {
                        [(BuildingType.AutoTower, RaceId.Spirit)] = new BuildingStats.StatValues { MaxHp = 1000, AttackPower = 100, AttackCooldown = 1f, AttackRange = 3f }
                    });
                    power = BuildingStats.GetAttackPower(BuildingType.AutoTower, RaceId.Spirit);
                }
                var towerData = new BuildingData(97900, BuildingType.AutoTower, TeamId.Red, new HexCoord(0, 2), 1000);
                MethodInfo towerAttack = typeof(TowerCombatUseCase).GetMethod("ExecuteTowerAttack", SupportNonPublic);
                Require(towerAttack != null, tag + " ExecuteTowerAttack must exist (damage point ⑤).");
                int old5 = DamageCalculator.ApplyDefense(power, victims[4].Defense);
                RequireSupportHit(tag, "⑤ tower", victims[4], Expect(old5),
                    () => towerAttack.Invoke(f.Tower, new object[] { towerData, victims[4] }));

                // ⑥ 대포탑 포탄 착탄(공개 경로 — 발사 때 공격력 120).
                int old6 = DamageCalculator.ApplyDefense(120, victims[5].Defense);
                RequireSupportHit(tag, "⑥ tower shell", victims[5], Expect(old6),
                    () => f.Tower.ApplyTowerProjectileImpact(towerData, victims[5], 120, default));

                // 상대 팀 유닛 · 건물은 언제나 그대로(대지의 갑주는 시전 팀 유닛만).
                //   이 지점은 ④ 지속 피해(dot)로 때리는데, 지속 피해는 방어 감쇄를 거치지 않는다(ApplyTimedDamageToUnit → 틱 값 그대로)
                //   — 그래서 ④ · newcomer처럼 20 그대로를 기대한다(방어 감쇄 식을 쓰면 안 된다).
                RequireSupportHit(tag, "enemy-team unit", enemyTeamUnit, 20,
                    () => dot.Invoke(f.Combat, new object[] { attacker, enemyTeamUnit, 20 }));
                var blueBuilding = new BuildingData(97901, BuildingType.MiningPost, TeamId.Blue, new HexCoord(2, 0), 5000);
                RequireSupportHit(tag, "Blue building", blueBuilding, attacker.AttackPower,
                    () => direct.Invoke(f.Combat, new object[] { attacker, blueBuilding }));

                if (armor)
                {
                    // 발동 뒤 새로 나온 유닛 — 대상이 아니다(Plan S-11).
                    UnitData newcomer = f.Unit(9730, TeamId.Blue, 50, 10, UnitType.Tank);
                    Require(f.Effects.GetIncomingDamageMultiplier(newcomer.Id) == 1f && !f.Effects.UnitHasEffect(newcomer.Id, StatusEffectKind.IncomingDamageMul),
                        "S-T3 a unit spawned after activation must not get Earth Armor.");
                    RequireSupportHit(tag, "newcomer DoT", newcomer, 20, () => dot.Invoke(f.Combat, new object[] { attacker, newcomer, 20 }));
                }
            }
        }

        private static void RequireSupportHit(string tag, string point, IDamageable target, int expected, Action hit)
        {
            int hpBefore = target.Hp;
            Require(hpBefore > expected, tag + " " + point + " fixture target HP " + hpBefore + " must exceed the expected damage " + expected + ".");
            int drop = SupportHpDrop(target, hit);
            Require(drop == expected, tag + " " + point + " must deal exactly " + expected + " (got " + drop + ").");
        }

        /// <summary> S-T4 — 교체 · 만료 · 사망 · 닫기 · 거부. </summary>
        private static void ValidateSupportEffectLifecycle()
        {
            var grid = new HexGrid(3, 3, HexOrientation.FlatTop);
            var spawn = new UnitSpawnUseCase(grid);
            var fx = new SupportSkillEffectSystem(spawn);
            UnitData a = spawn.SpawnUnitWithId(9800, UnitType.Pistoleer, TeamId.Blue, new HexCoord(0, 0));
            UnitData b = spawn.SpawnUnitWithId(9801, UnitType.Pistoleer, TeamId.Blue, new HexCoord(1, 0));
            UnitData c = spawn.SpawnUnitWithId(9802, UnitType.Pistoleer, TeamId.Blue, new HexCoord(2, 0));
            Require(a != null && b != null && c != null, "S-T4 lifecycle fixture units must spawn.");

            Require(fx.GetIncomingDamageMultiplier(a.Id) == 1f && fx.GetProductionSpeedMultiplier(TeamId.Blue) == 1f && fx.ActiveBatchCount == 0,
                "S-T4 an empty service answers exactly 1.");
            Require(fx.Apply(1, TeamId.Blue, 3, 7, TeamId.Blue, SkillEffectTarget.Building, StatusEffectKind.IncomingDamageMul, 0.7f, 8f) == 0
                    && fx.Apply(2, TeamId.Blue, 3, 7, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.ProductionSpeedMul, 1.5f, 8f) == 0
                    && fx.Apply(3, TeamId.Blue, 3, 7, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.IncomingDamageMul, 0.7f, 0f) == 0
                    && fx.Apply(4, TeamId.Blue, 3, 7, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.IncomingDamageMul, float.NaN, 8f) == 0
                    && fx.ActiveBatchCount == 0,
                "S-T4 unsupported target/kind (stage A) and invalid duration/magnitude must be rejected without effect.");

            Require(fx.Apply(10, TeamId.Blue, 3, 7, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.IncomingDamageMul, 0.7f, 8f) == 3,
                "S-T4 the effect is applied to all three living Blue units.");
            fx.Tick(5f);
            Require(Mathf.Approximately(fx.GetIncomingDamageMultiplier(a.Id), 0.7f), "S-T4 still active after 5 s.");
            // 재사용(다른 스킬 건물에서) — 남은 시간이 새 8초로 바뀐다(더하지 않음). 앞 묶음은 대상이 모두 넘어가 끝난다.
            Require(fx.Apply(11, TeamId.Blue, 3, 8, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.IncomingDamageMul, 0.7f, 8f) == 3
                    && fx.ActiveBatchCount == 1,
                "S-T4 re-apply replaces the records and ends the previous batch (Replaced).");
            fx.Tick(5f); // 재사용 뒤 5초 — 처음 걸었다면 10초라 끝났겠지만, 교체됐으므로 아직 남아 있다.
            Require(Mathf.Approximately(fx.GetIncomingDamageMultiplier(b.Id), 0.7f), "S-T4 re-apply must reset the remaining time to 8 s.");

            fx.NotifyUnitDied(a.Id);
            Require(fx.GetIncomingDamageMultiplier(a.Id) == 1f, "S-T4 a dead unit's effect is removed by the death notification.");
            spawn.RemoveUnit(b.Id); // 사망 통보를 놓친 경우 — 틱이 목록에 없는 유닛을 정리한다.
            fx.Tick(0.1f);
            Require(!fx.UnitHasEffect(b.Id, StatusEffectKind.IncomingDamageMul), "S-T4 the tick removes effects of units no longer alive.");

            fx.Tick(3.0f); // 재사용 뒤 8.1초 — 끝.
            Require(fx.GetIncomingDamageMultiplier(c.Id) == 1f && fx.ActiveBatchCount == 0 && fx.UnitRecordCount == 0,
                "S-T4 after the duration the multiplier returns exactly to 1 and nothing is left.");

            Require(fx.Apply(12, TeamId.Blue, 4, 9, TeamId.Blue, SkillEffectTarget.Team, StatusEffectKind.ProductionSpeedMul, 1.5f, 8f) == 1
                    && Mathf.Approximately(fx.GetProductionSpeedMultiplier(TeamId.Blue), 1.5f) && fx.GetProductionSpeedMultiplier(TeamId.Red) == 1f
                    && fx.GetProductionSpeedSequence(TeamId.Blue) == 12,
                "S-T4 the production effect is a team record (Blue 1.5, Red 1, sequence kept for diagnostics).");
            fx.Close();
            Require(fx.IsClosed && fx.ActiveBatchCount == 0 && fx.GetProductionSpeedMultiplier(TeamId.Blue) == 1f,
                "S-T4 Close clears every effect.");
            Require(fx.Apply(13, TeamId.Blue, 3, 7, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.IncomingDamageMul, 0.7f, 8f) == 0,
                "S-T4 after Close new effects are rejected.");
        }

        // 테스트용 생산 배율 창구(값을 바꿔 가며 쓴다).
        private sealed class SupportSpeedStub : IProductionSpeedSource
        {
            public float Multiplier = 1f;
            public float GetProductionSpeedMultiplier(TeamId team) => team == TeamId.Blue ? Multiplier : 1f;
            public int GetProductionSpeedSequence(TeamId team) => Multiplier != 1f ? 1 : 0;
        }

        private static ProductionState SupportStartProduction(UnitProductionUseCase production, int barracksId, TeamId team, HexCoord coord, float required)
        {
            production.RegisterBarracks(new BuildingData(barracksId, BuildingType.TrainingCamp, team, coord, 1000));
            ProductionState state = production.GetState(barracksId);
            Require(state != null, "S-T5 TrainingCamp must register as a production building.");
            state.CurrentProducing = UnitType.Pistoleer;
            state.RequiredTime = required;
            state.ElapsedTime = 0f;
            return state;
        }

        /// <summary> S-T5 — 서버 생산 진행에 팀 배율. 완료 직전까지만 진행한다(완료 · 스폰 경로는 이 검사 대상이 아니다). </summary>
        private static void ValidateSupportProductionSpeed()
        {
            var grid = new HexGrid(3, 3, HexOrientation.FlatTop);
            var spawn = new UnitSpawnUseCase(grid);
            var buildings = new BuildingPlacementUseCase(grid);

            // 창구 없음 = 기존과 같다.
            var plain = new UnitProductionUseCase(grid, spawn, null, null, buildings);
            ProductionState p0 = SupportStartProduction(plain, 9600, TeamId.Blue, new HexCoord(0, 0), 100f);
            plain.Tick(1f);
            Require(Mathf.Approximately(p0.ElapsedTime, 1f), "S-T5 without a speed source production advances exactly dt.");

            // 진행 중에 배율이 생기면 그 순간부터 빨라지고, 끝나면 바로 원래 속도(안 A).
            var stub = new SupportSpeedStub();
            var production = new UnitProductionUseCase(grid, spawn, null, null, buildings);
            production.SetProductionSpeedSource(stub);
            ProductionState s = SupportStartProduction(production, 9601, TeamId.Blue, new HexCoord(1, 0), 100f);
            ProductionState red = SupportStartProduction(production, 9602, TeamId.Red, new HexCoord(2, 0), 100f);
            production.Tick(1f);
            stub.Multiplier = 1.5f;
            production.Tick(1f);
            production.Tick(1f);
            stub.Multiplier = 1f;
            production.Tick(1f);
            Require(Mathf.Approximately(s.ElapsedTime, 1f + 1.5f + 1.5f + 1f), "S-T5 dt × multiplier applies to in-progress production and stops at once (got " + s.ElapsedTime + ").");
            Require(Mathf.Approximately(red.ElapsedTime, 4f), "S-T5 the other team's production is not affected.");
            Require(Mathf.Approximately(s.RequiredTime, 100f), "S-T5 the required time is never changed (plan A — not plan B).");

            // 실제 서비스 · 5초 생산 × 1.5배 — 0.1초 틱 33번(3.3초)에 4.95, 34번째 틱에 100%(필요 시간 5초를 넘지 않게 캡).
            var fx = new SupportSkillEffectSystem(spawn);
            var real = new UnitProductionUseCase(grid, spawn, null, null, buildings);
            real.SetProductionSpeedSource(fx);
            fx.Apply(21, TeamId.Blue, 4, 9, TeamId.Blue, SkillEffectTarget.Team, StatusEffectKind.ProductionSpeedMul, 1.5f, 8f);
            ProductionState q = SupportStartProduction(real, 9603, TeamId.Blue, new HexCoord(0, 1), 5f);
            for (int i = 0; i < 33; i++) { fx.Tick(0.1f); real.Tick(0.1f); }
            Require(Mathf.Abs(q.ElapsedTime - 4.95f) < 1e-3f, "S-T5 5 s × 1.5 must be at 4.95 after 3.3 s (got " + q.ElapsedTime + ").");
            q.CurrentProducing = null; // 완료 · 스폰 경로는 이 검사 대상이 아니므로 여기서 멈춘다(다음 틱이 완료시키지 않게).
            // 효과 중 새로 등록한 생산 건물도 팀 단위라 빨라진다(Plan S-9 — S-11은 유닛 · 건물에 붙는 효과에만).
            ProductionState late = SupportStartProduction(real, 9604, TeamId.Blue, new HexCoord(1, 1), 100f);
            real.Tick(1f);
            Require(Mathf.Approximately(late.ElapsedTime, 1.5f), "S-T5 a barracks registered during the effect also gets the team multiplier.");
            fx.Tick(5f); // 3.3 + 5 = 8.3초 — 끝.
            Require(fx.GetProductionSpeedMultiplier(TeamId.Blue) == 1f, "S-T5 the team multiplier returns exactly to 1 after 8 s.");
            float before = late.ElapsedTime;
            real.Tick(1f);
            Require(Mathf.Approximately(late.ElapsedTime - before, 1f), "S-T5 after the effect production runs at the original speed.");
        }

        /// <summary> S-T6 — 순수 클라이언트 진행 막대 거울(발동 알림 → 같은 배율 · 같은 지속). </summary>
        private static void ValidateSupportProductionMirror()
        {
            SkillDefinition tactical = AssetDatabase.LoadAssetAtPath<SkillDefinition>(TacticalOrderAssetPath);
            SkillDefinition armor = AssetDatabase.LoadAssetAtPath<SkillDefinition>(EarthArmorAssetPath);
            Require(tactical != null && armor != null, "S-T6 Tactical Order / Earth Armor assets must exist.");

            var grid = new HexGrid(3, 3, HexOrientation.FlatTop);
            var spawn = new UnitSpawnUseCase(grid);
            var production = new UnitProductionUseCase(grid, spawn, null, null, new BuildingPlacementUseCase(grid));
            ProductionState blue = SupportStartProduction(production, 9650, TeamId.Blue, new HexCoord(0, 0), 100f);
            ProductionState red = SupportStartProduction(production, 9651, TeamId.Red, new HexCoord(1, 0), 100f);

            production.MirrorProductionSpeedFromSkill(armor.ToSkillData(), TeamId.Blue, 30, 3, 5);
            production.TickProgressOnly(1f);
            Require(Mathf.Approximately(blue.ElapsedTime, 1f), "S-T6 Earth Armor has no production effect — the mirror must ignore it.");

            production.MirrorProductionSpeedFromSkill(tactical.ToSkillData(), TeamId.Blue, 31, 4, 6);
            production.TickProgressOnly(1f);
            Require(Mathf.Approximately(blue.ElapsedTime, 2.5f) && Mathf.Approximately(red.ElapsedTime, 2f),
                "S-T6 after the Tactical Order notice the caster team bar fills ×1.5 and the other team stays ×1.");
            for (int i = 0; i < 7; i++) production.TickProgressOnly(1f); // 거울 8초 끝.
            float before = blue.ElapsedTime;
            production.TickProgressOnly(1f);
            Require(Mathf.Approximately(blue.ElapsedTime - before, 1f), "S-T6 after 8 s the mirror returns to the original speed.");

            production.MirrorProductionSpeedFromSkill(tactical.ToSkillData(), TeamId.Blue, 32, 4, 6);
            production.CloseProductionSpeedMirror();
            before = blue.ElapsedTime;
            production.TickProgressOnly(1f);
            Require(Mathf.Approximately(blue.ElapsedTime - before, 1f), "S-T6 Close ends the mirror (game end).");
            production.MirrorProductionSpeedFromSkill(tactical.ToSkillData(), TeamId.Blue, 33, 4, 6);
            Require(production.ClientSpeedMirror.GetMultiplier(TeamId.Blue) == 1f, "S-T6 a closed mirror rejects later notices.");
        }

        /// <summary> S-T7 — 실행기가 새 종류 · 추가 효과를 지원 효과 다리로 보내고 기존 종류는 상태 다리로 보낸다. </summary>
        private static void ValidateSupportExecutorRouting()
        {
            SkillData armor = AssetDatabase.LoadAssetAtPath<SkillDefinition>(EarthArmorAssetPath).ToSkillData();
            SkillData tactical = AssetDatabase.LoadAssetAtPath<SkillDefinition>(TacticalOrderAssetPath).ToSkillData();

            var statusCalls = new List<StatusEffectKind>();
            var supportCalls = new List<(TeamId team, SkillEffectTarget target, StatusEffectKind kind, float mag, float dur)>();
            Action<TeamId, SkillEffectTarget, StatusEffectKind, float, float> supportBridge =
                (t, tg, k, m, d) => { supportCalls.Add((t, tg, k, m, d)); };
            SkillActivationContext Ctx(SkillData data, bool withSupport) => new SkillActivationContext(data, TeamId.Red, 1, Vector3.zero, false,
                (t, c, r, d, b) => { }, (t, c, r, dps, dur) => { },
                (t, k, m, d) => { statusCalls.Add(k); },
                (t, c, r, d) => { },
                null,
                withSupport ? supportBridge : null);

            var executor = new GlobalStatusChangeExecutor();
            executor.Execute(Ctx(armor, true));
            Require(statusCalls.Count == 0 && supportCalls.Count == 1 && supportCalls[0].team == TeamId.Red
                    && supportCalls[0].target == SkillEffectTarget.Unit && supportCalls[0].kind == StatusEffectKind.IncomingDamageMul
                    && Mathf.Approximately(supportCalls[0].mag, 0.7f) && Mathf.Approximately(supportCalls[0].dur, 8f),
                "S-T7 Earth Armor must go only to the support bridge (caster team units, 0.7, 8 s) — never the status system (R-7).");

            statusCalls.Clear(); supportCalls.Clear();
            executor.Execute(Ctx(tactical, true));
            Require(statusCalls.Count == 1 && statusCalls[0] == StatusEffectKind.AttackPowerMul
                    && supportCalls.Count == 1 && supportCalls[0].target == SkillEffectTarget.Team
                    && supportCalls[0].kind == StatusEffectKind.ProductionSpeedMul && Mathf.Approximately(supportCalls[0].mag, 1.5f)
                    && Mathf.Approximately(supportCalls[0].dur, 8f),
                "S-T7 Tactical Order keeps its attack path and adds one team production-speed effect (1.5, 8 s).");

            statusCalls.Clear(); supportCalls.Clear();
            executor.Execute(Ctx(tactical, false)); // 옛 컨텍스트(지원 다리 없음) — 공격력 경로는 그대로.
            Require(statusCalls.Count == 1 && supportCalls.Count == 0, "S-T7 a context without the support bridge still applies the attack buff.");
        }

        /// <summary> S-T8 — 자산 계약. </summary>
        private static void ValidateSupportNewStatusAssets()
        {
            const string loadoutPath = "Assets/_Project/Resources/Config/Skills/SkillLoadoutConfig.asset";
            SkillLoadoutConfig loadout = AssetDatabase.LoadAssetAtPath<SkillLoadoutConfig>(loadoutPath);
            Require(loadout != null, "S-T8 SkillLoadoutConfig asset must exist.");
            loadout.ClearCache();

            SkillData armor = AssetDatabase.LoadAssetAtPath<SkillDefinition>(EarthArmorAssetPath).ToSkillData();
            Require(armor.IsAvailable && armor.AimType == SkillAimType.Instant && armor.Mechanic == SkillMechanicType.GlobalStatusChange
                    && (StatusEffectKind)armor.StatusKind == StatusEffectKind.IncomingDamageMul && Mathf.Approximately(armor.Magnitude, 0.7f)
                    && Mathf.Approximately(armor.Duration, 8f) && Mathf.Approximately(armor.Cooldown, 15f) && armor.TargetsAllies
                    && armor.ExtraEffects.Count == 0,
                "S-T8 Earth Armor must be available / instant / type C / kind 6 / 0.7 / 8 s / cooldown 15 / allies / no extras (Plan S-1 temporary values).");
            var spirit = loadout.GetLoadout(RaceId.Spirit);
            Require(spirit != null && spirit.Count > 3 && spirit[3] != null && spirit[3].DisplayName == armor.DisplayName && spirit[3].IsAvailable,
                "S-T8 Earth Armor must be the available skill in Spirit slot 3 (button 4).");
            if (armor.StatusVfxPrefab != null)
            {
                Require(EffectManager.CheckUnitBuffLoopContract(armor.StatusVfxPrefab),
                    "S-T8 a filled Earth Armor unit status VFX must satisfy the loop contract (Looping ON, Stop Action None).");
                foreach (ParticleSystem ps in armor.StatusVfxPrefab.GetComponentsInChildren<ParticleSystem>(true))
                    Require(ps.main.scalingMode == ParticleSystemScalingMode.Hierarchy,
                        "S-T8 Earth Armor status VFX particle '" + ps.name + "' must use Scaling Mode Hierarchy.");
            }

            SkillData tactical = AssetDatabase.LoadAssetAtPath<SkillDefinition>(TacticalOrderAssetPath).ToSkillData();
            Require(tactical.ExtraEffects.Count == 1 && tactical.ExtraEffects[0].Target == SkillEffectTarget.Team
                    && tactical.ExtraEffects[0].Kind == StatusEffectKind.ProductionSpeedMul && Mathf.Approximately(tactical.ExtraEffects[0].Magnitude, 1.5f)
                    && (StatusEffectKind)tactical.StatusKind == StatusEffectKind.AttackPowerMul && Mathf.Approximately(tactical.Magnitude, 1.5f),
                "S-T8 Tactical Order keeps attack ×1.5 and has exactly one extra effect: team production speed ×1.5.");

            // 신목의 각성 자산 계약은 SupportShield.cs Sh-T8이 본다.
            // 재생의 뿌리 자산 계약은 SupportRegen.cs R-T8이 본다.
        }

        /// <summary> S-T9 — 소스 게이트(6곳 · 틱 · 닫기 · 주입 · 셋업 · 스키마). </summary>
        private static void ValidateSupportNewStatusSourceGates()
        {
            string combat = File.ReadAllText("Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs");
            string tower = File.ReadAllText("Assets/_Project/Scripts/Application/UseCases/TowerCombatUseCase.cs");
            Require(SupportActiveLineCount(combat, "IncomingDamage.Apply(") == 4 && SupportActiveLineCount(tower, "IncomingDamage.Apply(") == 2,
                "S-T9 the six damage points (four in UnitCombatUseCase, two in TowerCombatUseCase) must call IncomingDamage.Apply.");
            Require(SupportActiveLineCount(combat, ".TakeDamage(") == 0 && SupportActiveLineCount(tower, ".TakeDamage(") == 0,
                "S-T9 no direct TakeDamage may remain in the two combat use cases (R-1).");

            string net = File.ReadAllText("Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs");
            int zoneTick = net.IndexOf("GetSkillActivationUseCase()?.TickZones(elapsed);", StringComparison.Ordinal);
            int supportTick = net.IndexOf("GetSkillActivationUseCase()?.TickSupportEffects(elapsed);", StringComparison.Ordinal);
            int statusTick = net.IndexOf("GetStatusEffectSystem()?.Tick(elapsed);", StringComparison.Ordinal);
            Require(zoneTick >= 0 && supportTick > zoneTick && statusTick > supportTick,
                "S-T9 the multiplayer server tick must run TickSupportEffects after TickZones and before the status tick.");

            string boot = File.ReadAllText("Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs").Replace("\r\n", "\n");
            Require(boot.Contains("if (!IsNetworkMode() && _skillActivation != null)\n            {\n                _skillActivation.TickSupportEffects(Time.deltaTime);")
                    && boot.Contains("_skillActivation?.CloseSupportEffects()") && boot.Contains("_unitProduction?.CloseProductionSpeedMirror()"),
                "S-T9 single-player TickSupportEffects must be guarded by !IsNetworkMode(), and game end must close effects and the mirror.");
            string setup = File.ReadAllText("Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs");
            Require(setup.Contains("_skillActivation.SetSupportSkillEffectSystem(_supportSkillEffects);")
                    && setup.Contains("_unitCombat.SetSupportSkillEffectSystem(_supportSkillEffects);")
                    && setup.Contains("_towerCombat.SetSupportSkillEffectSystem(_supportSkillEffects);")
                    && setup.Contains("_unitProduction.SetProductionSpeedSource(_supportSkillEffects);")
                    && setup.Contains("GameEvents.OnSkillActivationReceived.Subscribe(")
                    && setup.Contains("_supportSkillEffects?.NotifyUnitDied(e.Unit.Id);"),
                "S-T9 the composition root must inject the same service into skills / combat / tower / production and wire the mirror and death cleanup.");

            FieldInfo expected = typeof(Hexiege.EditorTools.SupportSkillLoadoutSetup).GetField("ExpectedAvailableCount",
                BindingFlags.NonPublic | BindingFlags.Static);
            // [2026-11-17 공습 Task L-6] 11 → 12 — 공습(직선 연속 폭격)을 「사용 가능」으로 켰다(지원 스킬 새 상태와 무관한 변경).
            Require(expected != null && (int)expected.GetRawConstantValue() == 12,
                "S-T9 SupportSkillLoadoutSetup.ExpectedAvailableCount must be 12 (Earth Armor + Sacred Tree Awakening + Roots of Regeneration + Air Strike enabled).");
            // [2026-11-17 공습 Task L-2] 10 → 11 — 공습 조준 방향이 요청 · 발동 알림 RPC에 붙었다(지원 스킬 새 상태와 무관한 변경).
            Require(UnitAttackShadowProfileResolver.PresentationContractSchemaRevision == 11,
                "S-T9 stage A added no RPC (schema stayed 9); stage C's shield-broken RPC raised it to 10; the air-strike direction arguments raised it to 11.");
        }

        // 주석(//로 시작하는 줄)을 빼고 그 문자열이 들어 있는 줄 수.
        private static int SupportActiveLineCount(string source, string needle)
        {
            int count = 0;
            foreach (string raw in source.Split('\n'))
            {
                string line = raw.TrimStart();
                if (line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith("///", StringComparison.Ordinal)) continue;
                if (line.Contains(needle)) count++;
            }
            return count;
        }
    }
}
