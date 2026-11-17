// ============================================================================
// RunUnitActionSelfValidation.SupportShield.cs
// [2026-11-16 지원 스킬 새 상태 — 단계 C] 신목의 각성(쉴드 — 유닛 · 건물) 회귀 검증(fixture).
// Task: Assets/_Project/Docs/_Tasks/2026-11-16/01_00_support-skill-new-statuses/ (Plan 6-1 · 구현 단위 C-1 ~ C-4).
// Run()(메뉴 「Hexiege/Combat/Run Full Regression Suite」)이 함께 돌린다.
//
// [배경 — 초급 개발자용 설명]
//   쉴드는 「HP와 별개인 흡수량」이다. 피해 지점 헬퍼(IncomingDamage.Apply)가 방어 감쇄 → 받는 피해 배율 → 쉴드 흡수 → HP 순서로
//   처리한다. 화면에 쉴드 양을 보여 줄 수단이 없어(HP 바 없음) 눈으로 확인하기 어렵기 때문에, 실제 전투 UseCase를 불러 숫자로 확인한다.
//
// 무엇을 확인하는가:
//   Sh-T1 순서 — 받는 피해 배율(0.7) "뒤" 값을 쉴드가 흡수한다(감소 전 값이 아니다).
//   Sh-T2 유닛 쉴드 — 부분 흡수(HP 그대로) · 깨짐(남은 양만 흡수, 나머지 HP) · 깨짐 알림(다음 Tick 머리, 순번 · Id) · 만료(남은 양 버림) ·
//         재적용 = 교체(새 양 · 새 시간, 앞 묶음 끝).
//   Sh-T3 건물 쉴드 — 직격 · 포격(③)이 건물 쉴드를 먼저 깎고, 깨지면 나머지가 HP로 간다 · 건물 깨짐도 알림에 실린다(유닛 목록과 따로).
//   Sh-T4 무효과 회귀 — 쉴드가 없는 유닛 · 건물(서비스는 있고 다른 대상에는 쉴드가 있음)은 기존 값 그대로.
//   Sh-T5 제외(S-11) — 발동 뒤 새 유닛 · 새 건물 · 업그레이드로 Id가 바뀐 건물에는 쉴드가 없다(업그레이드 순간 옛 기록 버림).
//   Sh-T6 정리 — 유닛 사망 통보 · 건물 파괴 통보 · 틱의 생존 확인(통보를 놓친 경우)으로 기록이 지워진다.
//   Sh-T7 경기 종료 — Close 뒤 새 쉴드 거부 · 대기 중인 깨짐 알림 버림. 양이 1 미만이면 거부.
//   Sh-T8 자산 · 실행기 — 신목의 각성(사용 가능 · 즉시 · 타입 C · 종류 8 · 150 · 8초 · 쿨 15 · 아군 · 초월 슬롯 2) ·
//         추가 효과 정확히 1줄(건물 · 쉴드 · 500) · 채워진 상태 VFX 칸은 상태 이펙트 계약 · 실행기가 상태 다리 없이 지원 다리 2번 ·
//         재생의 뿌리는 여전히 사용 불가. (2026-11-17 — 이 검사는 단계 B에서 주석 비활성화. 재생의 뿌리 계약은 SupportRegen.cs R-T8.)
//   Sh-T9 소스 게이트 — 스키마 10 · 셋업 기대 수 10 · 헬퍼의 흡수 호출 2곳 · 깨짐 RPC · S-6 차이 0 재발행 2곳 · 배선 · 건물 VFX 장치.
//
// 실제 씬 · 멀티 전송 · 이펙트 모양은 여기서 확인하지 않는다(사용자 실기 항목 — Plan 6-1-7).
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
using UnityEditor;
using UnityEngine;

namespace Hexiege.Editor.Combat
{
    public static partial class RunUnitActionSelfValidation
    {
        /// <summary>
        /// 지원 스킬 새 상태 단계 C(신목의 각성 쉴드) 회귀 검증 진입점(Sh-T1 ~ Sh-T9).
        /// </summary>
        private static void ValidateSupportShieldStageC()
        {
            Require(!UnityEngine.Application.isPlaying, "Support shield fixture must run outside a live match.");
            // [2026-11-17 스위트 실패 수정 — 전역 스탯 의존 제거] Sh-T1 · Sh-T2 · Sh-T4 · Sh-T7의 Tank HP(폴백 10이면 60 · 50+30 · 20 피해를
            //   못 견딘다)와 Sh-T3의 공격력(폴백 1이면 「둘째 타에 쉴드가 깨진다」 전제 = 공격력 > 20이 거짓) · 초월 Castle HP를
            //   이 메서드 동안만 고정값으로 넣고 끝나면 원래 표를 되돌린다(SupportFixtureStatsScope — SupportNewStatuses.cs 설명 참고).
            using var fixtureStats = new SupportFixtureStatsScope();
            ValidateShieldOrderAfterReduction(); // Sh-T1
            ValidateUnitShieldLifecycle();       // Sh-T2
            ValidateBuildingShield();            // Sh-T3 · Sh-T4(건물)
            ValidateShieldNoEffectRegression();  // Sh-T4(유닛)
            ValidateShieldExclusions();          // Sh-T5
            ValidateShieldCleanup();             // Sh-T6
            ValidateShieldClose();               // Sh-T7
            ValidateSacredTreeAssetAndExecutor();// Sh-T8
            ValidateShieldSourceGates();         // Sh-T9
            ValidateBuildingVfxTargetScope();    // Sh-T10 [2026-11-17 S-10 — 전술 지령 건물 이펙트 = 아군 생산 건물 전부]
            Debug.Log("[UAS-DIAG][SKILL-BVFX] self-validation PASS: building status VFX target scope (Sh-T10) — Tactical Order → ProductionBuildings (late built / upgraded production buildings attach), Sacred Tree → ActivationBuildings (activation-time buildings only), Earth Armor / null → None; every production stage 1-3 of all three races is a target, non-production buildings / enemy team / dead buildings are not.");
            Debug.Log("[UAS-DIAG][SKILL-SHIELD] self-validation PASS: shield absorbs the value after the incoming-damage multiplier (Sh-T1), unit partial absorb / break with remainder to HP / next-tick break notice with sequence and ids / expiry discard / replace on re-apply (Sh-T2), building shield on direct and skill damage with separate building id list (Sh-T3), no-shield targets unchanged (Sh-T4), newcomers / new buildings / upgraded buildings excluded (Sh-T5), death / destruction / tick cleanup (Sh-T6), Close rejects and drops pending notices (Sh-T7), Sacred Tree asset + executor routing (Sh-T8), schema 10 / setup 11 / helper absorb / RPC / zero-diff republish / wiring source gates (Sh-T9).");
        }

        // 쉴드 깨짐 「확정」 이벤트를 모으는 도우미(검사 동안만 구독).
        private sealed class ShieldBrokenCapture : IDisposable
        {
            public readonly List<ShieldBrokenPresentation> Events = new List<ShieldBrokenPresentation>();
            private readonly IDisposable _subscription;
            public ShieldBrokenCapture() { _subscription = GameEvents.OnShieldBrokenConfirmed.Subscribe(p => { Events.Add(p); }); }
            public void Dispose() => _subscription?.Dispose();
        }

        private static MethodInfo ShieldDotMethod()
        {
            MethodInfo dot = typeof(UnitCombatUseCase).GetMethod("ApplyTimedDamageToUnit", SupportNonPublic);
            Require(dot != null, "Sh-T0 ApplyTimedDamageToUnit must exist (damage point ④).");
            return dot;
        }

        private static MethodInfo ShieldDirectMethod()
        {
            MethodInfo direct = typeof(UnitCombatUseCase).GetMethod("ApplyDamageToVictim", SupportNonPublic, null,
                new[] { typeof(UnitData), typeof(IDamageable) }, null);
            Require(direct != null, "Sh-T0 ApplyDamageToVictim(UnitData, IDamageable) must exist (damage point ①).");
            return direct;
        }

        /// <summary> Sh-T1 — 쉴드는 받는 피해 배율 "뒤" 값을 흡수한다. </summary>
        private static void ValidateShieldOrderAfterReduction()
        {
            var f = new SupportDamageFixture(true);
            MethodInfo dot = ShieldDotMethod();
            UnitData attacker = f.Unit(9900, TeamId.Red, 0, 0);
            UnitData victim = f.Unit(9901, TeamId.Blue, 10, 10, UnitType.Tank);
            // 한 유닛에 두 효과(실제 게임에서는 종족이 달라 함께 걸리지 않는다 — 순서 정의만 확인).
            f.Effects.Apply(1, TeamId.Blue, 3, 7, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.IncomingDamageMul, 0.7f, 8f);
            f.Effects.Apply(2, TeamId.Blue, 2, 8, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.Shield, 150f, 8f);

            int drop = SupportHpDrop(victim, () => dot.Invoke(f.Combat, new object[] { attacker, victim, 100 }));
            Require(drop == 0 && f.Effects.GetShieldRemaining(victim.Id, true) == 80,
                "Sh-T1 100 × 0.7 = 70 must be absorbed by the shield (150 → 80), not 100 (drop " + drop + ", left " + f.Effects.GetShieldRemaining(victim.Id, true) + ").");
            drop = SupportHpDrop(victim, () => dot.Invoke(f.Combat, new object[] { attacker, victim, 200 }));
            Require(drop == 60 && f.Effects.GetShieldRemaining(victim.Id, true) == 0,
                "Sh-T1 200 × 0.7 = 140 → shield absorbs the last 80 and 60 reaches HP (drop " + drop + ").");
        }

        /// <summary> Sh-T2 — 유닛 쉴드: 부분 흡수 · 깨짐 · 알림 · 만료 · 교체. </summary>
        private static void ValidateUnitShieldLifecycle()
        {
            var f = new SupportDamageFixture(true);
            MethodInfo dot = ShieldDotMethod();
            UnitData attacker = f.Unit(9910, TeamId.Red, 0, 0);
            UnitData a = f.Unit(9911, TeamId.Blue, 10, 10, UnitType.Tank);
            UnitData b = f.Unit(9912, TeamId.Blue, 13, 10, UnitType.Tank);
            Require(f.Effects.Apply(21, TeamId.Blue, 2, 8, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.Shield, 150f, 8f) == 2,
                "Sh-T2 the shield is applied to both living Blue units at activation.");

            using (var capture = new ShieldBrokenCapture())
            {
                int drop = SupportHpDrop(a, () => dot.Invoke(f.Combat, new object[] { attacker, a, 100 }));
                Require(drop == 0 && f.Effects.GetShieldRemaining(a.Id, true) == 50, "Sh-T2 partial absorb: 100 into 150 → HP unchanged, 50 left.");
                drop = SupportHpDrop(a, () => dot.Invoke(f.Combat, new object[] { attacker, a, 100 }));
                Require(drop == 50 && f.Effects.GetShieldRemaining(a.Id, true) == 0 && !f.Effects.UnitHasEffect(a.Id, StatusEffectKind.Shield),
                    "Sh-T2 break: the last 50 is absorbed, 50 reaches HP and the shield record is gone (drop " + drop + ").");
                drop = SupportHpDrop(a, () => dot.Invoke(f.Combat, new object[] { attacker, a, 30 }));
                Require(drop == 30, "Sh-T2 after the break the full damage reaches HP.");
                Require(f.Effects.PendingBrokenCount == 1 && capture.Events.Count == 0,
                    "Sh-T2 a break is queued, not published inside the damage call.");
                f.Effects.Tick(0.1f);
                Require(capture.Events.Count == 1 && capture.Events[0].Sequence == 21 && capture.Events[0].UnitCount == 1
                        && capture.Events[0].UnitIds[0] == a.Id && capture.Events[0].BuildingCount == 0 && f.Effects.PendingBrokenCount == 0,
                    "Sh-T2 the next Tick head publishes one notice with the activation sequence and the broken unit id.");
                f.Effects.Tick(0.1f);
                Require(capture.Events.Count == 1, "Sh-T2 a tick without breaks publishes nothing.");
            }

            // 만료 — b는 맞지 않았다. 8초가 지나면 남은 150을 버리고 기록이 사라진다.
            f.Effects.Tick(7.9f);
            Require(f.Effects.GetShieldRemaining(b.Id, true) == 0 && f.Effects.UnitRecordCount == 0 && f.Effects.ActiveBatchCount == 0,
                "Sh-T2 after 8 s the remaining shield is discarded and nothing is left.");
            int expiredDrop = SupportHpDrop(b, () => dot.Invoke(f.Combat, new object[] { attacker, b, 40 }));
            Require(expiredDrop == 40, "Sh-T2 after expiry damage reaches HP unchanged.");

            // 교체(S-5) — 깎인 뒤 다시 걸면 새 양 · 새 시간(더하지 않음), 앞 묶음은 끝난다.
            f.Effects.Apply(22, TeamId.Blue, 2, 8, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.Shield, 150f, 8f);
            dot.Invoke(f.Combat, new object[] { attacker, b, 100 });
            Require(f.Effects.GetShieldRemaining(b.Id, true) == 50, "Sh-T2 setup: 50 left before re-apply.");
            f.Effects.Tick(5f);
            f.Effects.Apply(23, TeamId.Blue, 2, 9, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.Shield, 150f, 8f);
            Require(f.Effects.GetShieldRemaining(b.Id, true) == 150 && f.Effects.ActiveBatchCount == 1,
                "Sh-T2 re-apply replaces the amount with 150 (not 200) and ends the previous batch.");
            f.Effects.Tick(5f);
            Require(f.Effects.GetShieldRemaining(b.Id, true) == 150, "Sh-T2 re-apply resets the remaining time to 8 s.");
        }

        /// <summary> Sh-T3 — 건물 쉴드(직격 ① · 포격 ③) · 쉴드 없는 건물 회귀(Sh-T4 건물 몫). </summary>
        private static void ValidateBuildingShield()
        {
            var f = new SupportDamageFixture(true);
            f.Effects.SetBuildingPlacement(f.Buildings);
            MethodInfo direct = ShieldDirectMethod();
            UnitData attacker = f.Unit(9920, TeamId.Red, 0, 0);
            int power = attacker.AttackPower;
            Require(power > 0, "Sh-T3 fixture attacker must have attack power.");

            BuildingData shielded = f.Buildings.PlaceBuildingWithId(97950, BuildingType.Castle, TeamId.Blue, new HexCoord(0, 0), RaceId.Transcendence);
            Require(shielded != null && shielded.IsAlive && shielded.Hp > power, "Sh-T3 fixture Castle must be placed alive with HP above one hit.");
            // 직격 피해가 공격력 그대로(건물 2배는 Tank/CannonCart만) → 쉴드 양 = 공격력 + 20: 첫 타 전부 흡수 · 둘째 타에 20만 흡수.
            Require(f.Effects.Apply(31, TeamId.Blue, 2, 8, TeamId.Blue, SkillEffectTarget.Building, StatusEffectKind.Shield, power + 20, 8f) == 1,
                "Sh-T3 the building shield is applied to the living Blue building.");

            using (var capture = new ShieldBrokenCapture())
            {
                int drop = SupportHpDrop(shielded, () => direct.Invoke(f.Combat, new object[] { attacker, shielded }));
                Require(drop == 0 && f.Effects.GetShieldRemaining(shielded.Id, false) == 20,
                    "Sh-T3 the first direct hit is fully absorbed by the building shield (drop " + drop + ").");
                drop = SupportHpDrop(shielded, () => direct.Invoke(f.Combat, new object[] { attacker, shielded }));
                Require(drop == power - 20 && !f.Effects.BuildingHasEffect(shielded.Id, StatusEffectKind.Shield),
                    "Sh-T3 the second hit breaks the shield: 20 absorbed and the rest reaches HP (drop " + drop + ").");
                f.Effects.Tick(0.1f);
                Require(capture.Events.Count == 1 && capture.Events[0].BuildingCount == 1 && capture.Events[0].BuildingIds[0] == shielded.Id
                        && capture.Events[0].UnitCount == 0,
                    "Sh-T3 the break notice carries the building id in the building list (unit list empty).");
            }

            // ③ 포격 — 건물 쉴드도 흡수한다(공개 경로 — fixture 매퍼 = (q, 0, r), 반경 0.1).
            BuildingData skillTarget = f.Buildings.PlaceBuildingWithId(97951, BuildingType.Castle, TeamId.Blue, new HexCoord(1, 0), RaceId.Transcendence);
            Require(skillTarget != null && skillTarget.IsAlive, "Sh-T3 the second Castle must be placed.");
            f.Effects.Apply(32, TeamId.Blue, 2, 8, TeamId.Blue, SkillEffectTarget.Building, StatusEffectKind.Shield, 50f, 8f);
            int skillDrop = SupportHpDrop(skillTarget,
                () => f.Combat.ApplySkillInstantAreaDamage(TeamId.Red, new Vector3(skillTarget.Position.Q, 0f, skillTarget.Position.R), 0.1f, 30, 55));
            Require(skillDrop == 0 && f.Effects.GetShieldRemaining(skillTarget.Id, false) == 20,
                "Sh-T3 skill damage 30 on a building is absorbed by its shield (50 → 20, drop " + skillDrop + ").");

            // Sh-T4(건물) — 쉴드가 없는 다른 팀 건물은 기존 값 그대로.
            var redBuilding = new BuildingData(97952, BuildingType.MiningPost, TeamId.Red, new HexCoord(2, 0), 5000);
            RequireSupportHit("Sh-T4", "Red building without shield", redBuilding, power,
                () => direct.Invoke(f.Combat, new object[] { attacker, redBuilding }));
        }

        /// <summary> Sh-T4(유닛) — 쉴드가 없는 유닛은 기존 값 그대로(서비스에 다른 유닛 쉴드가 있어도). </summary>
        private static void ValidateShieldNoEffectRegression()
        {
            var f = new SupportDamageFixture(true);
            MethodInfo dot = ShieldDotMethod();
            UnitData attacker = f.Unit(9930, TeamId.Red, 0, 0);
            UnitData shieldedBlue = f.Unit(9931, TeamId.Blue, 10, 10, UnitType.Tank);
            f.Effects.Apply(41, TeamId.Blue, 2, 8, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.Shield, 150f, 8f);
            UnitData redUnit = f.Unit(9932, TeamId.Red, 13, 10, UnitType.Tank);
            RequireSupportHit("Sh-T4", "enemy-team unit DoT 20", redUnit, 20, () => dot.Invoke(f.Combat, new object[] { attacker, redUnit, 20 }));
            Require(f.Effects.GetShieldRemaining(shieldedBlue.Id, true) == 150, "Sh-T4 hitting another unit never touches this shield.");
            Require(IncomingDamage.Apply(null, redUnit, 17, 17, IncomingDamageWriter.DoT) == 17,
                "Sh-T4 without the service the helper passes the value through.");
        }

        /// <summary> Sh-T5 — 발동 뒤 새 유닛 · 새 건물 · 업그레이드 건물은 제외(S-11). </summary>
        private static void ValidateShieldExclusions()
        {
            var f = new SupportDamageFixture(true);
            f.Effects.SetBuildingPlacement(f.Buildings);
            UnitData old = f.Unit(9940, TeamId.Blue, 10, 10, UnitType.Tank);
            // 초월 훈련소의 HP는 SupportFixtureStatsScope가 1000으로 고정한다(2026-11-17 — 실제 설정 표에서는 초월 HP가 0이라
            //   Play 뒤 남은 표를 그대로 쓰면 HP 0 = 죽은 건물로 놓인다. 업그레이드 결과 WarAcademy도 같은 이유로 고정).
            BuildingData camp = f.Buildings.PlaceBuildingWithId(97960, BuildingType.TrainingCamp, TeamId.Blue, new HexCoord(0, 0), RaceId.Transcendence);
            Require(camp != null && camp.IsAlive, "Sh-T5 fixture TrainingCamp must be placed alive.");
            f.Effects.Apply(51, TeamId.Blue, 2, 8, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.Shield, 150f, 8f);
            f.Effects.Apply(51, TeamId.Blue, 2, 8, TeamId.Blue, SkillEffectTarget.Building, StatusEffectKind.Shield, 500f, 8f);
            Require(f.Effects.GetShieldRemaining(old.Id, true) == 150 && f.Effects.GetShieldRemaining(camp.Id, false) == 500,
                "Sh-T5 setup: the existing unit and building are shielded.");

            UnitData newcomer = f.Unit(9941, TeamId.Blue, 13, 10, UnitType.Tank);
            BuildingData newBuilding = f.Buildings.PlaceBuildingWithId(97961, BuildingType.Castle, TeamId.Blue, new HexCoord(1, 0), RaceId.Transcendence);
            Require(f.Effects.GetShieldRemaining(newcomer.Id, true) == 0 && newBuilding != null && f.Effects.GetShieldRemaining(newBuilding.Id, false) == 0,
                "Sh-T5 a unit or building created after activation has no shield.");

            BuildingData upgraded = f.Buildings.UpgradeBuilding(camp.Id, RaceId.Transcendence);
            Require(upgraded != null && upgraded.Id != camp.Id, "Sh-T5 upgrading the TrainingCamp must create a new building id.");
            f.Effects.NotifyBuildingRemoved(camp.Id, upgraded: true);
            Require(f.Effects.GetShieldRemaining(camp.Id, false) == 0 && f.Effects.GetShieldRemaining(upgraded.Id, false) == 0
                    && f.Effects.BuildingRecordCount == 0,
                "Sh-T5 the upgrade discards the old building's shield and does not carry it to the new id.");
        }

        /// <summary> Sh-T6 — 사망 · 파괴 통보와 틱 안전망으로 기록이 지워진다. </summary>
        private static void ValidateShieldCleanup()
        {
            var f = new SupportDamageFixture(true);
            f.Effects.SetBuildingPlacement(f.Buildings);
            UnitData a = f.Unit(9950, TeamId.Blue, 10, 10, UnitType.Tank);
            UnitData b = f.Unit(9951, TeamId.Blue, 13, 10, UnitType.Tank);
            BuildingData x = f.Buildings.PlaceBuildingWithId(97970, BuildingType.Castle, TeamId.Blue, new HexCoord(0, 0), RaceId.Transcendence);
            BuildingData y = f.Buildings.PlaceBuildingWithId(97971, BuildingType.Castle, TeamId.Blue, new HexCoord(1, 0), RaceId.Transcendence);
            Require(x != null && y != null, "Sh-T6 fixture buildings must be placed.");
            f.Effects.Apply(61, TeamId.Blue, 2, 8, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.Shield, 150f, 8f);
            f.Effects.Apply(61, TeamId.Blue, 2, 8, TeamId.Blue, SkillEffectTarget.Building, StatusEffectKind.Shield, 500f, 8f);

            f.Effects.NotifyUnitDied(a.Id);
            f.Effects.NotifyBuildingRemoved(x.Id, upgraded: false);
            Require(f.Effects.GetShieldRemaining(a.Id, true) == 0 && f.Effects.GetShieldRemaining(x.Id, false) == 0,
                "Sh-T6 death / destruction notifications remove the shield records.");
            f.Spawn.RemoveUnit(b.Id);
            f.Buildings.RemoveBuilding(y.Id);
            f.Effects.Tick(0.1f);
            Require(f.Effects.UnitRecordCount == 0 && f.Effects.BuildingRecordCount == 0 && f.Effects.ActiveBatchCount == 0,
                "Sh-T6 the tick removes records of units / buildings no longer present (missed notifications).");
        }

        /// <summary> Sh-T7 — Close 뒤 거부 · 대기 알림 버림 · 양 1 미만 거부. </summary>
        private static void ValidateShieldClose()
        {
            var f = new SupportDamageFixture(true);
            MethodInfo dot = ShieldDotMethod();
            UnitData attacker = f.Unit(9960, TeamId.Red, 0, 0);
            UnitData a = f.Unit(9961, TeamId.Blue, 10, 10, UnitType.Tank);
            Require(f.Effects.Apply(70, TeamId.Blue, 2, 8, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.Shield, 0.4f, 8f) == 0,
                "Sh-T7 a shield amount that rounds below 1 is rejected.");
            f.Effects.Apply(71, TeamId.Blue, 2, 8, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.Shield, 10f, 8f);
            dot.Invoke(f.Combat, new object[] { attacker, a, 30 });
            Require(f.Effects.PendingBrokenCount == 1, "Sh-T7 setup: one break pending.");
            using (var capture = new ShieldBrokenCapture())
            {
                f.Effects.Close();
                f.Effects.Tick(0.1f);
                Require(capture.Events.Count == 0 && f.Effects.PendingBrokenCount == 0 && f.Effects.ActiveBatchCount == 0,
                    "Sh-T7 Close drops pending break notices (the screens tail-stop on game end) and clears every batch.");
            }
            Require(f.Effects.Apply(72, TeamId.Blue, 2, 8, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.Shield, 150f, 8f) == 0
                    && f.Effects.GetShieldRemaining(a.Id, true) == 0,
                "Sh-T7 after Close new shields are rejected.");
        }

        /// <summary> Sh-T8 — 신목의 각성 자산 계약 · 실행기 경로. </summary>
        private static void ValidateSacredTreeAssetAndExecutor()
        {
            const string loadoutPath = "Assets/_Project/Resources/Config/Skills/SkillLoadoutConfig.asset";
            SkillLoadoutConfig loadout = AssetDatabase.LoadAssetAtPath<SkillLoadoutConfig>(loadoutPath);
            Require(loadout != null, "Sh-T8 SkillLoadoutConfig asset must exist.");
            loadout.ClearCache();

            SkillData tree = AssetDatabase.LoadAssetAtPath<SkillDefinition>(SacredTreeAssetPath).ToSkillData();
            Require(tree.IsAvailable && tree.AimType == SkillAimType.Instant && tree.Mechanic == SkillMechanicType.GlobalStatusChange
                    && (StatusEffectKind)tree.StatusKind == StatusEffectKind.Shield && Mathf.Approximately(tree.Magnitude, 150f)
                    && Mathf.Approximately(tree.Duration, 8f) && Mathf.Approximately(tree.Cooldown, 15f) && tree.TargetsAllies,
                "Sh-T8 Sacred Tree Awakening must be available / instant / type C / kind 8 / 150 / 8 s / cooldown 15 / allies (Plan S-1 temporary values).");
            Require(tree.ExtraEffects.Count == 1 && tree.ExtraEffects[0].Target == SkillEffectTarget.Building
                    && tree.ExtraEffects[0].Kind == StatusEffectKind.Shield && Mathf.Approximately(tree.ExtraEffects[0].Magnitude, 500f)
                    && tree.HasBuildingEffect() && tree.HasBuildingShield(),
                "Sh-T8 Sacred Tree Awakening has exactly one extra effect: building shield 500.");
            var trans = loadout.GetLoadout(RaceId.Transcendence);
            Require(trans != null && trans.Count > 2 && trans[2] != null && trans[2].DisplayName == tree.DisplayName && trans[2].IsAvailable,
                "Sh-T8 Sacred Tree Awakening must be the available skill in Transcendence slot 2 (button 3).");
            foreach (GameObject prefab in new[] { tree.StatusVfxPrefab, tree.BuildingStatusVfxPrefab })
            {
                if (prefab == null) continue; // 빈 칸은 통과(테스트 뒤 비운다 — C-L11).
                Require(EffectManager.CheckUnitBuffLoopContract(prefab),
                    "Sh-T8 a filled Sacred Tree status VFX (" + prefab.name + ") must satisfy the loop contract (Looping ON, Stop Action None).");
                foreach (ParticleSystem ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
                    Require(ps.main.scalingMode == ParticleSystemScalingMode.Hierarchy,
                        "Sh-T8 Sacred Tree status VFX particle '" + ps.name + "' must use Scaling Mode Hierarchy.");
            }
            // [2026-11-17 단계 B 비활성화 — 재생의 뿌리를 켰다. 자산 계약은 SupportRegen.cs R-T8이 본다] 옛 검사(기록용):
            // Require(!AssetDatabase.LoadAssetAtPath<SkillDefinition>(RootsAssetPath).ToSkillData().IsAvailable,
            //     "Sh-T8 Roots of Regeneration must stay unavailable until stage B.");

            // 실행기 — 주 효과(유닛 쉴드 150)와 추가 효과(건물 쉴드 500)가 모두 지원 다리로 가고 상태 시스템 다리는 부르지 않는다(R-7).
            var statusCalls = new List<StatusEffectKind>();
            var supportCalls = new List<(SkillEffectTarget target, StatusEffectKind kind, float mag, float dur)>();
            var ctx = new SkillActivationContext(tree, TeamId.Blue, 2, Vector3.zero, false,
                (t, c, r, d, bId) => { }, (t, c, r, dps, dur) => { },
                (t, k, m, d) => { statusCalls.Add(k); },
                (t, c, r, d) => { },
                null,
                (t, tg, k, m, d) => { supportCalls.Add((tg, k, m, d)); });
            new GlobalStatusChangeExecutor().Execute(ctx);
            Require(statusCalls.Count == 0 && supportCalls.Count == 2
                    && supportCalls[0].target == SkillEffectTarget.Unit && supportCalls[0].kind == StatusEffectKind.Shield && Mathf.Approximately(supportCalls[0].mag, 150f)
                    && supportCalls[1].target == SkillEffectTarget.Building && supportCalls[1].kind == StatusEffectKind.Shield && Mathf.Approximately(supportCalls[1].mag, 500f)
                    && Mathf.Approximately(supportCalls[0].dur, 8f) && Mathf.Approximately(supportCalls[1].dur, 8f),
                "Sh-T8 the executor sends unit shield 150 and building shield 500 (8 s) to the support bridge only.");
            Require(SupportSkillEffectSystem.IsSupported(SkillEffectTarget.Building, StatusEffectKind.Shield)
                    && SupportSkillEffectSystem.IsSupported(SkillEffectTarget.Unit, StatusEffectKind.Shield)
                    && !SupportSkillEffectSystem.IsSupported(SkillEffectTarget.Building, StatusEffectKind.IncomingDamageMul)
                    && !SupportSkillEffectSystem.IsSupported(SkillEffectTarget.Team, StatusEffectKind.Shield),
                "Sh-T8 supported combinations: unit / building shield only (no building armor, no team shield).");
        }

        /// <summary> Sh-T9 — 소스 게이트. </summary>
        private static void ValidateShieldSourceGates()
        {
            Require(UnitAttackShadowProfileResolver.PresentationContractSchemaRevision == 10,
                "Sh-T9 the shield-broken RPC must raise the presentation schema to 10.");
            FieldInfo expected = typeof(Hexiege.EditorTools.SupportSkillLoadoutSetup).GetField("ExpectedAvailableCount",
                BindingFlags.NonPublic | BindingFlags.Static);
            // [2026-11-17 단계 B 비활성화 — 셋업 기대 수 10 → 11(재생의 뿌리)] 옛 검사(기록용):
            // Require(expected != null && (int)expected.GetRawConstantValue() == 10,
            //     "Sh-T9 SupportSkillLoadoutSetup.ExpectedAvailableCount must be 10 (Sacred Tree Awakening enabled).");
            Require(expected != null && (int)expected.GetRawConstantValue() == 11,
                "Sh-T9 SupportSkillLoadoutSetup.ExpectedAvailableCount must be 11 (Sacred Tree Awakening + Roots of Regeneration enabled).");

            const string root = "Assets/_Project/Scripts/";
            string helper = File.ReadAllText(root + "Application/Combat/IncomingDamage.cs");
            Require(SupportActiveLineCount(helper, ".AbsorbShield(") == 2,
                "Sh-T9 IncomingDamage.Apply must absorb shields on both the unit and the building branch.");
            string service = File.ReadAllText(root + "Application/Services/SupportSkillEffectSystem.cs").Replace("\r\n", "\n");
            int flush = service.IndexOf("FlushBrokenShields();\n            if (_activeBatches.Count == 0) return;", StringComparison.Ordinal);
            Require(flush > 0, "Sh-T9 Tick must flush break notices before the 'no active batch' early return.");
            string net = File.ReadAllText(root + "Infrastructure/Network/NetworkSkillController.cs");
            Require(net.Contains("private void ShieldBrokenClientRpc(int sequence, int[] unitIds, int[] buildingIds)")
                    && net.Contains("GameEvents.OnShieldBrokenConfirmed") && net.Contains("GameEvents.OnShieldBrokenReceived.OnNext(")
                    && net.Contains("Event=ShieldBrokenReceived"),
                "Sh-T9 NetworkSkillController must relay shield breaks (server subscription, ClientRpc, Received event, client diagnostic).");
            string health = File.ReadAllText(root + "Infrastructure/Network/NetworkHealthSync.cs");
            Require(SupportActiveLineCount(health, "else if (diff == 0 && serverHp > 0)") == 2,
                "Sh-T9 S-6 (b): the client must republish fully-absorbed hits for units and buildings (exactly zero diff, living target).");
            string setup = File.ReadAllText(root + "Bootstrap/GameBootstrapper.Setup.cs");
            Require(setup.Contains("_supportSkillEffects.SetBuildingPlacement(_buildingPlacement);")
                    && setup.Contains("NotifyBuildingRemoved(e.Building.Id, upgraded: false)")
                    && setup.Contains("NotifyBuildingRemoved(e.OldBuildingId, upgraded: true)"),
                "Sh-T9 the composition root must inject buildings and wire building death / upgrade cleanup.");
            string map = File.ReadAllText(root + "Bootstrap/GameBootstrapper.Map.cs");
            Require(map.Contains("buildingStatusVfxPresenter.Initialize("), "Sh-T9 the building status VFX presenter must be initialized per map.");
            string unitPresenter = File.ReadAllText(root + "Presentation/Effects/UnitBuffVfxPresenter.cs");
            string shieldDriver = File.ReadAllText(root + "Presentation/Effects/UnitBuffVfxPresenter.ShieldDriver.cs");
            Require(unitPresenter.Contains("SubscribeShieldDriver();") && unitPresenter.Contains("DisposeShieldDriverSubscriptions();")
                    && shieldDriver.Contains("GameEvents.OnShieldBrokenConfirmed.Subscribe") && shieldDriver.Contains("GameEvents.OnShieldBrokenReceived.Subscribe")
                    && shieldDriver.Contains("path: \"ShieldBroken\""),
                "Sh-T9 UnitBuffVfxPresenter must stop a unit's shield VFX immediately on a break notice (both channels).");
            string buildingPresenter = File.ReadAllText(root + "Presentation/Effects/BuildingStatusVfxPresenter.cs");
            Require(buildingPresenter.Contains("\"NoPrefab\"") && buildingPresenter.Contains("GameEvents.OnShieldBrokenReceived.Subscribe")
                    && buildingPresenter.Contains("GameEvents.OnBuildingUpgraded.Subscribe") && buildingPresenter.Contains("path: \"Upgraded\"")
                    && buildingPresenter.Contains("path: \"Broken\"") && buildingPresenter.Contains("path: \"Died\""),
                "Sh-T9 BuildingStatusVfxPresenter must log NoPrefab and stop on break / death / upgrade.");
        }

        /// <summary>
        /// Sh-T10 [2026-11-17 S-10] — 건물 상태 VFX 대상 기준(순수 규칙 BuildingStatusVfxTargetPolicy).
        /// ① 스킬별 기준: 전술 지령 = ProductionBuildings(효과 중 새 건물도) · 신목의 각성 = ActivationBuildings(발동 순간만) · 대지의 갑주 = None.
        /// ② 생산 기준 대상: 세 종족 생산 라인의 1 · 2 · 3단계(업그레이드 형태) 전부 = 대상, 비생산 건물 · 적 팀 · 파괴된 건물 = 대상 아님.
        /// ③ 쉴드 기준(ActivationBuildings)은 종류와 무관하게 대상 팀 건물 전부(단계 C 동작 그대로).
        /// 전역 스탯 표(UnitStats · BuildingStats)를 읽지 않는다 — 건물은 HP를 직접 넣은 BuildingData로 만든다(2026-11-17 회귀 실패의 교훈).
        /// </summary>
        private static void ValidateBuildingVfxTargetScope()
        {
            SkillData tactical = AssetDatabase.LoadAssetAtPath<SkillDefinition>(TacticalOrderAssetPath).ToSkillData();
            SkillData tree = AssetDatabase.LoadAssetAtPath<SkillDefinition>(SacredTreeAssetPath).ToSkillData();
            SkillData armor = AssetDatabase.LoadAssetAtPath<SkillDefinition>(EarthArmorAssetPath).ToSkillData();
            Require(BuildingStatusVfxTargetPolicy.Resolve(tactical) == BuildingStatusVfxScope.ProductionBuildings
                    && BuildingStatusVfxTargetPolicy.Resolve(tree) == BuildingStatusVfxScope.ActivationBuildings
                    && BuildingStatusVfxTargetPolicy.Resolve(armor) == BuildingStatusVfxScope.None
                    && BuildingStatusVfxTargetPolicy.Resolve(null) == BuildingStatusVfxScope.None,
                "Sh-T10 scope: Tactical Order → ProductionBuildings, Sacred Tree → ActivationBuildings, Earth Armor / null → None.");
            Require(BuildingStatusVfxTargetPolicy.AttachesLateBuildings(BuildingStatusVfxScope.ProductionBuildings)
                    && !BuildingStatusVfxTargetPolicy.AttachesLateBuildings(BuildingStatusVfxScope.ActivationBuildings)
                    && !BuildingStatusVfxTargetPolicy.AttachesLateBuildings(BuildingStatusVfxScope.None),
                "Sh-T10 only the production scope attaches to buildings created during the effect (shield keeps S-11).");

            // 생산 건물 = BuildingTypeHelper 표의 1 · 2 · 3단계(업그레이드 형태 포함). 목록을 여기 직접 적어 표가 바뀌면 이 검사가 알린다.
            BuildingType[] production =
            {
                BuildingType.TrainingCamp, BuildingType.WarAcademy, BuildingType.HumanBarracks,
                BuildingType.Gunsmith, BuildingType.Armory, BuildingType.WeaponForge,
                BuildingType.Garage, BuildingType.VehicleBay,
                BuildingType.FireSpire, BuildingType.BlazeConduit, BuildingType.InfernoCore,
                BuildingType.AquaSpring, BuildingType.TidalNexus, BuildingType.OceanicHeart,
                BuildingType.StoneMound, BuildingType.TerraForge, BuildingType.GaeaSanctum,
                BuildingType.PrimalAltar, BuildingType.PrimalDen, BuildingType.PrimalSanctuary,
                BuildingType.FeralAltar, BuildingType.FeralDen, BuildingType.FeralSanctuary,
                BuildingType.SporePatch, BuildingType.FloralNursery,
            };
            BuildingType[] nonProduction =
            {
                BuildingType.Castle, BuildingType.MiningPost, BuildingType.AutoTower, BuildingType.FlightFacility,
                BuildingType.Research, BuildingType.MagicBuilding, BuildingType.HealShrine,
            };
            Require(production.Length + nonProduction.Length == Enum.GetValues(typeof(BuildingType)).Length,
                "Sh-T10 every BuildingType must be classified here (a new building type needs a decision).");

            int nextId = 97980;
            BuildingData Make(BuildingType type, TeamId team) => new BuildingData(nextId++, type, team, new HexCoord(0, 0), 1000);
            const BuildingStatusVfxScope prod = BuildingStatusVfxScope.ProductionBuildings;
            const BuildingStatusVfxScope act = BuildingStatusVfxScope.ActivationBuildings;
            foreach (BuildingType type in production)
            {
                Require(BuildingStatusVfxTargetPolicy.IsTarget(prod, Make(type, TeamId.Blue), TeamId.Blue),
                    "Sh-T10 own production building " + type + " (incl. upgraded stages) must be a Tactical Order VFX target.");
                Require(!BuildingStatusVfxTargetPolicy.IsTarget(prod, Make(type, TeamId.Red), TeamId.Blue),
                    "Sh-T10 enemy production building " + type + " must not be a target.");
            }
            foreach (BuildingType type in nonProduction)
            {
                Require(!BuildingStatusVfxTargetPolicy.IsTarget(prod, Make(type, TeamId.Blue), TeamId.Blue),
                    "Sh-T10 non-production building " + type + " must not be a Tactical Order VFX target.");
                Require(BuildingStatusVfxTargetPolicy.IsTarget(act, Make(type, TeamId.Blue), TeamId.Blue),
                    "Sh-T10 the shield scope keeps every own building (" + type + ") — stage C behaviour unchanged.");
            }
            BuildingData dead = Make(BuildingType.TrainingCamp, TeamId.Blue);
            dead.TakeDamage(dead.MaxHp * 10);
            Require(!dead.IsAlive && !BuildingStatusVfxTargetPolicy.IsTarget(prod, dead, TeamId.Blue)
                    && !BuildingStatusVfxTargetPolicy.IsTarget(prod, null, TeamId.Blue)
                    && !BuildingStatusVfxTargetPolicy.IsTarget(BuildingStatusVfxScope.None, Make(BuildingType.TrainingCamp, TeamId.Blue), TeamId.Blue),
                "Sh-T10 dead / null buildings and the None scope are never targets.");

            // 소스 게이트 — 장치가 이 규칙을 쓰고, 효과 중 건설 · 업그레이드를 받아 늦게 붙인다.
            string presenter = File.ReadAllText("Assets/_Project/Scripts/Presentation/Effects/BuildingStatusVfxPresenter.cs");
            Require(SupportActiveLineCount(presenter, "BuildingStatusVfxTargetPolicy.Resolve(skill)") == 1
                    && SupportActiveLineCount(presenter, "GameEvents.OnBuildingPlaced.Subscribe(") == 1
                    && SupportActiveLineCount(presenter, "EnqueueLateBuilding(e.NewBuilding.Id, LateReasonUpgraded)") == 1
                    && presenter.Contains("Event=BuildingVfxAttachedLate") && presenter.Contains("Scope={BuildingStatusVfxTargetPolicy.DiagName(scope)}"),
                "Sh-T10 BuildingStatusVfxPresenter must resolve the scope, subscribe to placement, re-attach after upgrade and log Scope / AttachedLate.");
        }
    }
}
