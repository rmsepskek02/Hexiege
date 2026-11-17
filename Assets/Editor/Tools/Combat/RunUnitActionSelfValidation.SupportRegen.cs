// ============================================================================
// RunUnitActionSelfValidation.SupportRegen.cs
// [2026-11-17 지원 스킬 새 상태 — 단계 B] 재생의 뿌리(재생 · 받는 회복 배율 — 유닛 · 건물) 회귀 검증(fixture).
// Task: Assets/_Project/Docs/_Tasks/2026-11-16/01_00_support-skill-new-statuses/ (Plan 6-2 · 구현 단위 B-1 ~ B-4).
// Run()(메뉴 「Hexiege/Combat/Run Full Regression Suite」)이 함께 돌린다.
//
// [배경 — 초급 개발자용 설명]
//   화면에 HP 바가 없어(현재 HP 숫자만 뜬다) 「회복이 1.5배인지」 · 「효과가 없을 때 회복이 전과 같은지」를 눈으로 가늠하기 어렵다.
//   그래서 실제 회복 자리(UnitCombatUseCase.ApplyHealToUnit · MistShrineUseCase 유닛 · 건물)와 지원 스킬 효과 서비스의 재생 채널을
//   직접 불러 숫자로 확인한다. 🔴 전역 스탯 표(UnitStats · BuildingStats)에 기대지 않는다 — 메뉴로 스위트를 돌리면 그 표가 비어 있어
//   유닛이 폴백 값(HP 10)으로 생긴다(단계 C 회귀 실패의 원인, Plan 6-2-5 머리 · RB-9). 그래서 SupportFixtureStatsScope로 필요한 행만
//   이 검사 동안 고정하고 끝나면 원래 표를 되돌린다(Tank HP 10000 · 초월 Castle HP 2000).
//
// 무엇을 확인하는가:
//   R-T1 증폭 계산 — 1.5배 · 소수 이월(1 → 1, 2, 1, 2 = 4 × 1.5) · 10 → 15 · 배율 1/없음/0 이하/죽은 대상은 그대로 · 건물도 같은 규칙.
//   R-T2 회복 지점 3곳 — ApplyHealToUnit(자연회복 · 지속 회복 · 파도 공용) · 물안개 유닛 · 물안개 건물이
//        ① 서비스 없음 ② 서비스 있지만 효과 없음 → 기존 값 그대로(무효과 회귀 — B-L16) ③ 효과 있음 → 정확히 한 번 증폭.
//   R-T3 재생 채널 — 1초마다 초당 회복량(유닛 20 · 건물 40) · 받는 회복 배율은 재생에도 한 번만(30 · 60 — RB-5) ·
//        8초 동안 정확히 8주기(0.1초 틱 · 1/60초 틱 모두) · 최대 HP에서 멈춤 · 체력 가득이면 건너뛰고 효과 유지 → 맞으면 다시 회복(B-L7).
//   R-T4 수명 — 만료 뒤 배율 1 · 나머지 버림 · 재적용 = 교체(나머지 0 · 새 8초) · 사망 · 건물 파괴 · 업그레이드 · 틱 안전망 정리 ·
//        발동 뒤 새 유닛 · 새 건물 제외(S-11) · Close 뒤 거부 · 지원하지 않는 조합(팀 대상 재생) 거부.
//   R-T5 회복 이벤트 — 실제로 오른 주기만 OnEntityHealed(유닛 · 건물 구분 · 힐러 = 시전 건물) · 텍스트는 3주기마다 · 가득이면 이벤트 없음.
//   R-T6 실행기 — 재생의 뿌리 자산이 상태 다리 없이 지원 다리로 4번(유닛 재생 20 · 건물 재생 40 · 유닛 배율 1.5 · 건물 배율 1.5, 8초).
//   R-T7 VFX 경로 — 주 종류가 HealOverTime이 아니라 유닛 상태 VFX 장치가 건너뛰지 않음(RB-8) · 건물 이펙트 기준 ActivationBuildings ·
//        쉴드 없음(깨짐 정지 대상 아님).
//   R-T8 자산 — 재생의 뿌리(사용 가능 · 즉시 · 타입 C · 종류 9 · 20 · 8초 · 쿨 15 · 아군 · 초월 슬롯 1) · 추가 효과 정확히 3줄 ·
//        채워진 상태 VFX 칸은 상태 이펙트 계약.
//   R-T9 소스 게이트 — 회복 지점 3곳 증폭 호출 · 남은 직접 Heal 없음 · 출처 전달 · 물안개 주입 · Tick 순서(재생 → 만료) ·
//        셋업 기대 수 11 · 스키마 10 유지(새 RPC 없음).
//
// 실제 씬 · 멀티 전송 · 이펙트 모양 · 자연회복 연구 · BloomFairy 시전 경로는 여기서 확인하지 않는다(사용자 실기 항목 — Plan 6-2-7).
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
        /// 지원 스킬 새 상태 단계 B(재생의 뿌리) 회귀 검증 진입점(R-T1 ~ R-T9).
        /// </summary>
        private static void ValidateSupportRegenStageB()
        {
            Require(!UnityEngine.Application.isPlaying, "Support regen fixture must run outside a live match.");
            // 전역 스탯 표 고정(Tank HP 10000 · 초월 Castle HP 2000 등) — 끝나면(예외로 끝나도) 원래 표를 되돌린다(RB-9).
            using var fixtureStats = new SupportFixtureStatsScope();
            ValidateRegenAmplifyArithmetic();   // R-T1
            ValidateRegenHealPoints();          // R-T2
            ValidateRegenChannel();             // R-T3
            ValidateRegenLifecycle();           // R-T4
            ValidateRegenHealEvents();          // R-T5
            ValidateRegenExecutorRouting();     // R-T6
            ValidateRegenVfxRouting();          // R-T7
            ValidateRegenAsset();               // R-T8
            ValidateRegenSourceGates();         // R-T9
            Debug.Log("[UAS-DIAG][SKILL-REGEN] self-validation PASS: heal amplification ×1.5 with per-target carry (1,1,1,1 → 6) and pass-through without an effect (R-T1), the three heal points amplify exactly once and stay unchanged without the service or the effect (R-T2), regen pulses every 1 s (unit 20 / building 40, amplified once to 30 / 60), exactly 8 pulses in 8 s at 0.1 s and 1/60 s ticks, max-HP clamp, full-HP skip keeps the effect (R-T3), expiry / replace / death / destruction / upgrade / newcomer exclusion / Close / rejection (R-T4), healed events only on real gains with text every 3 pulses (R-T5), executor routing 4 support calls (R-T6), unit VFX not skipped + building scope ActivationBuildings (R-T7), Roots asset contract (R-T8), heal-point / tick-order / injection / setup 11 / schema 10 source gates (R-T9).");
        }

        // ── fixture 도우미 ─────────────────────────────────────────────

        // 재생의 뿌리 한 번(유닛 재생 · 건물 재생 · 유닛 배율 · 건물 배율 — 실행기가 지원 다리로 보내는 네 줄과 같은 값).
        private static void RegenApplyRoots(SupportSkillEffectSystem fx, int sequence, TeamId team, float unitPerSec = 20f,
                                            float buildingPerSec = 40f, float amp = 1.5f, float duration = 8f)
        {
            fx.Apply(sequence, team, 1, 77, team, SkillEffectTarget.Unit, StatusEffectKind.Regen, unitPerSec, duration);
            fx.Apply(sequence, team, 1, 77, team, SkillEffectTarget.Building, StatusEffectKind.Regen, buildingPerSec, duration);
            fx.Apply(sequence, team, 1, 77, team, SkillEffectTarget.Unit, StatusEffectKind.HealAmpMul, amp, duration);
            fx.Apply(sequence, team, 1, 77, team, SkillEffectTarget.Building, StatusEffectKind.HealAmpMul, amp, duration);
        }

        // 체력을 지정 값까지 깎은 Tank(고정 HP 10000 — SupportFixtureStatsScope).
        private static UnitData RegenWoundedTank(SupportDamageFixture f, int id, TeamId team, int hp)
        {
            UnitData unit = f.Unit(id, team, 10 + id % 50, 10, UnitType.Tank);
            Require(unit.MaxHp == SupportFixtureStatsScope.VictimTankMaxHp, "R-T0 fixture Tank must have the pinned max HP (got " + unit.MaxHp + ").");
            unit.TakeDamage(unit.MaxHp - hp);
            Require(unit.Hp == hp, "R-T0 fixture Tank must be wounded to " + hp + " (got " + unit.Hp + ").");
            return unit;
        }

        // 체력을 지정 값까지 깎은 초월 Castle(고정 HP 2000 — SupportFixtureStatsScope).
        private static BuildingData RegenWoundedCastle(SupportDamageFixture f, int id, TeamId team, int q, int r, int hp)
        {
            BuildingData building = f.Buildings.PlaceBuildingWithId(id, BuildingType.Castle, team, new HexCoord(q, r), RaceId.Transcendence);
            Require(building != null && building.MaxHp == SupportFixtureStatsScope.CastleTranscendenceMaxHp,
                "R-T0 fixture Castle must be placed with the pinned max HP.");
            building.TakeDamage(building.MaxHp - hp);
            Require(building.Hp == hp, "R-T0 fixture Castle must be wounded to " + hp + " (got " + building.Hp + ").");
            return building;
        }

        private static MethodInfo RegenHealToUnitMethod()
        {
            MethodInfo m = typeof(UnitCombatUseCase).GetMethod("ApplyHealToUnit", SupportNonPublic);
            Require(m != null && m.GetParameters().Length == 6,
                "R-T0 UnitCombatUseCase.ApplyHealToUnit(healer, target, amount, showText, wave, ampSource) must exist (heal point ①).");
            return m;
        }

        // 회복 지점 ① 호출(자연회복과 같은 인자 — healer 없음 · 텍스트 없음 · 파도 없음).
        private static int RegenHealVia(MethodInfo healToUnit, UnitCombatUseCase combat, UnitData target, int amount, HealAmpSource source)
        {
            int before = target.Hp;
            healToUnit.Invoke(combat, new object[] { null, target, amount, false, null, source });
            return target.Hp - before;
        }

        private static MistShrineUseCase RegenMist(SupportDamageFixture f, SupportSkillEffectSystem inject)
        {
            var mist = new MistShrineUseCase(f.Buildings, f.Spawn, new ProjectileFixtureMapper(), 10f, 10f, 20f, 3f, 3f);
            if (inject != null) mist.SetSupportSkillEffectSystem(inject);
            return mist;
        }

        private static int RegenMistHealUnit(MistShrineUseCase mist, UnitData target)
        {
            MethodInfo m = typeof(MistShrineUseCase).GetMethod("ApplyHealToUnitEntity", SupportNonPublic);
            Require(m != null, "R-T0 MistShrineUseCase.ApplyHealToUnitEntity must exist (heal point ②).");
            int before = target.Hp;
            m.Invoke(mist, new object[] { target, 555, false });
            return target.Hp - before;
        }

        private static int RegenMistHealBuilding(MistShrineUseCase mist, BuildingData target)
        {
            MethodInfo m = typeof(MistShrineUseCase).GetMethod("ApplyHealToBuildingEntity", SupportNonPublic);
            Require(m != null, "R-T0 MistShrineUseCase.ApplyHealToBuildingEntity must exist (heal point ③).");
            int before = target.Hp;
            m.Invoke(mist, new object[] { target, 555, false });
            return target.Hp - before;
        }

        // ── R-T1 ────────────────────────────────────────────────────────

        /// <summary> R-T1 — 증폭 계산(1.5배 · 소수 이월) · 효과 없으면 그대로. </summary>
        private static void ValidateRegenAmplifyArithmetic()
        {
            var f = new SupportDamageFixture(true);
            f.Effects.SetBuildingPlacement(f.Buildings);
            UnitData a = RegenWoundedTank(f, 9800, TeamId.Blue, 1000);
            BuildingData castle = RegenWoundedCastle(f, 97800, TeamId.Blue, 0, 0, 500);
            UnitData red = RegenWoundedTank(f, 9801, TeamId.Red, 1000);

            Require(f.Effects.AmplifyHeal(a, 7, HealAmpSource.Natural) == 7 && f.Effects.GetHealAmpMultiplier(a.Id, true) == 1f,
                "R-T1 an empty service returns the heal unchanged.");
            RegenApplyRoots(f.Effects, 11, TeamId.Blue);
            Require(f.Effects.GetHealAmpMultiplier(a.Id, true) == 1.5f && f.Effects.GetHealAmpMultiplier(castle.Id, false) == 1.5f,
                "R-T1 Roots puts ×1.5 on the living Blue unit and building.");

            // 소수 이월 — 1 HP 회복 4번 = 1 · 2 · 1 · 2(합 6 = 4 × 1.5). 매번 반올림했다면 2 · 2 · 2 · 2 = 8(2배)이다(RB-4).
            int[] got = new int[4];
            for (int i = 0; i < 4; i++) got[i] = f.Effects.AmplifyHeal(a, 1, HealAmpSource.Natural);
            Require(got[0] == 1 && got[1] == 2 && got[2] == 1 && got[3] == 2 && f.Effects.GetHealAmpCarry(a.Id, true) == 0d,
                "R-T1 per-target carry: 1,1,1,1 → 1,2,1,2 (sum 6 = 4 × 1.5), carry back to 0 (got " + string.Join(",", got) + ").");
            Require(f.Effects.AmplifyHeal(a, 1, HealAmpSource.Natural) == 1 && Math.Abs(f.Effects.GetHealAmpCarry(a.Id, true) - 0.5d) < 1e-9,
                "R-T1 a remainder below 1 is kept on the target (0.5).");
            Require(f.Effects.AmplifyHeal(a, 10, HealAmpSource.Mist) == 15, "R-T1 10 × 1.5 + carry 0.5 = 15.5 → 15 (carry 0.5 kept).");
            Require(f.Effects.AmplifyHeal(castle, 10, HealAmpSource.Mist) == 15 && f.Effects.AmplifyHeal(castle, 40, HealAmpSource.SkillRegen) == 60,
                "R-T1 buildings use the same rule (10 → 15, 40 → 60).");
            Require(f.Effects.AmplifyHeal(red, 10, HealAmpSource.Natural) == 10, "R-T1 the other team's unit is unchanged.");
            Require(f.Effects.AmplifyHeal(a, 0, HealAmpSource.Natural) == 0 && f.Effects.AmplifyHeal(a, -5, HealAmpSource.Natural) == -5
                    && f.Effects.AmplifyHeal(null, 9, HealAmpSource.Natural) == 9,
                "R-T1 non-positive amounts and null targets pass through unchanged.");

            // 배율이 정확히 1이면 곱하지 않는다(다른 유닛으로 확인).
            var f1 = new SupportDamageFixture(true);
            UnitData one = RegenWoundedTank(f1, 9802, TeamId.Blue, 1000);
            f1.Effects.Apply(12, TeamId.Blue, 1, 77, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.HealAmpMul, 1f, 8f);
            Require(f1.Effects.AmplifyHeal(one, 3, HealAmpSource.Natural) == 3 && f1.Effects.GetHealAmpCarry(one.Id, true) == 0d,
                "R-T1 multiplier exactly 1 returns the heal unchanged and keeps no carry.");

            // 죽은 대상 — 그대로(나머지도 쌓지 않는다).
            a.TakeDamage(a.MaxHp * 10);
            Require(!a.IsAlive && f.Effects.AmplifyHeal(a, 10, HealAmpSource.Natural) == 10, "R-T1 a dead target is passed through.");
        }

        // ── R-T2 ────────────────────────────────────────────────────────

        /// <summary> R-T2 — 회복 지점 3곳: 서비스 없음 · 효과 없음 = 기존 값, 효과 있음 = 한 번만 증폭. </summary>
        private static void ValidateRegenHealPoints()
        {
            MethodInfo healToUnit = RegenHealToUnitMethod();

            // ① 서비스 없음(조합 루트가 주입하기 전 — 하위호환).
            var none = new SupportDamageFixture(false);
            UnitData u0 = RegenWoundedTank(none, 9810, TeamId.Blue, 1000);
            BuildingData b0 = RegenWoundedCastle(none, 97810, TeamId.Blue, 0, 0, 500);
            MistShrineUseCase mist0 = RegenMist(none, null);
            Require(RegenHealVia(healToUnit, none.Combat, u0, 10, HealAmpSource.Natural) == 10
                    && RegenMistHealUnit(mist0, u0) == 10 && RegenMistHealBuilding(mist0, b0) == 10,
                "R-T2 without the service the three heal points heal exactly the old amount (10).");

            // ② 서비스 있지만 효과 없음(재생의 뿌리를 쓰기 전 — B-L16 무효과 회귀).
            var idle = new SupportDamageFixture(true);
            idle.Effects.SetBuildingPlacement(idle.Buildings);
            UnitData u1 = RegenWoundedTank(idle, 9811, TeamId.Blue, 1000);
            BuildingData b1 = RegenWoundedCastle(idle, 97811, TeamId.Blue, 0, 0, 500);
            MistShrineUseCase mist1 = RegenMist(idle, idle.Effects);
            // 다른 효과(대지의 갑주)가 있어도 회복은 그대로다.
            idle.Effects.Apply(13, TeamId.Blue, 3, 77, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.IncomingDamageMul, 0.7f, 8f);
            Require(RegenHealVia(healToUnit, idle.Combat, u1, 10, HealAmpSource.Bloom) == 10
                    && RegenHealVia(healToUnit, idle.Combat, u1, 1, HealAmpSource.Natural) == 1
                    && RegenMistHealUnit(mist1, u1) == 10 && RegenMistHealBuilding(mist1, b1) == 10,
                "R-T2 with the service but no heal amplification the three heal points heal exactly the old amount.");

            // ③ 효과 있음 — 세 자리 모두 정확히 한 번 1.5배(10 → 15, 자연회복 1씩 → 1 · 2).
            var amp = new SupportDamageFixture(true);
            amp.Effects.SetBuildingPlacement(amp.Buildings);
            UnitData u2 = RegenWoundedTank(amp, 9812, TeamId.Blue, 1000);
            UnitData u3 = RegenWoundedTank(amp, 9813, TeamId.Blue, 1000);
            BuildingData b2 = RegenWoundedCastle(amp, 97812, TeamId.Blue, 0, 0, 500);
            MistShrineUseCase mist2 = RegenMist(amp, amp.Effects);
            RegenApplyRoots(amp.Effects, 14, TeamId.Blue);
            Require(RegenHealVia(healToUnit, amp.Combat, u2, 10, HealAmpSource.Bloom) == 15,
                "R-T2 heal point ① (BloomFairy / HoT / natural / wave) amplifies 10 → 15 exactly once.");
            Require(RegenHealVia(healToUnit, amp.Combat, u3, 1, HealAmpSource.Natural) == 1
                    && RegenHealVia(healToUnit, amp.Combat, u3, 1, HealAmpSource.Natural) == 2,
                "R-T2 natural regen 1 HP ticks become 1 then 2 through the carry.");
            Require(RegenMistHealUnit(mist2, u2) == 15 && RegenMistHealBuilding(mist2, b2) == 15,
                "R-T2 heal points ② ③ (mist unit / mist building) amplify 10 → 15 exactly once.");

            // 최대 HP에서 멈춘다 — 남은 자리 7인 유닛에 물안개 15가 와도 7만 오른다(UnitData.Heal 클램프 그대로).
            UnitData nearFull = RegenWoundedTank(amp, 9814, TeamId.Blue, SupportFixtureStatsScope.VictimTankMaxHp - 7);
            amp.Effects.Apply(15, TeamId.Blue, 1, 78, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.HealAmpMul, 1.5f, 8f);
            Require(RegenMistHealUnit(mist2, nearFull) == 7 && nearFull.Hp == nearFull.MaxHp, "R-T2 an amplified heal still stops at max HP.");
        }

        // ── R-T3 ────────────────────────────────────────────────────────

        /// <summary> R-T3 — 재생 채널: 1초 주기 · 8주기 · 한 번만 증폭 · 최대 HP · 가득이면 건너뛰고 효과 유지. </summary>
        private static void ValidateRegenChannel()
        {
            // (1) 배율 없이 재생만 — 1초가 차야 첫 회복(20), 8초에 정확히 8주기(160).
            var f = new SupportDamageFixture(true);
            f.Effects.SetBuildingPlacement(f.Buildings);
            UnitData u = RegenWoundedTank(f, 9820, TeamId.Blue, 1000);
            f.Effects.Apply(21, TeamId.Blue, 1, 77, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.Regen, 20f, 8f);
            f.Effects.Tick(0.5f);
            Require(u.Hp == 1000, "R-T3 no heal before the first 1 s pulse.");
            f.Effects.Tick(0.5f);
            Require(u.Hp == 1020, "R-T3 the first pulse at 1 s heals the per-second amount 20 (got " + (u.Hp - 1000) + ").");
            for (int i = 0; i < 80; i++) f.Effects.Tick(0.1f);
            Require(u.Hp == 1160 && !f.Effects.UnitHasEffect(u.Id, StatusEffectKind.Regen) && f.Effects.ActiveBatchCount == 0,
                "R-T3 8 s of regen at 20/s heals exactly 160 in 8 pulses and then ends (got " + (u.Hp - 1000) + ").");

            // (2) 재생의 뿌리 전체(재생 + 배율) — 유닛 30 · 건물 60 per 1 s(재생에도 배율은 한 번만 — 45 · 90이 아니다, RB-5).
            //     1/60초 프레임 틱으로 8초 → float 누적 오차가 있어도 정확히 8주기(240 · 480).
            var g = new SupportDamageFixture(true);
            g.Effects.SetBuildingPlacement(g.Buildings);
            UnitData gu = RegenWoundedTank(g, 9821, TeamId.Blue, 1000);
            BuildingData gb = RegenWoundedCastle(g, 97820, TeamId.Blue, 0, 0, 500);
            RegenApplyRoots(g.Effects, 22, TeamId.Blue);
            // 61프레임(≈1.017초) — 1/60을 60번 더한 float 합이 1보다 아주 조금 작을 수 있어 한 프레임 더 진행한 뒤 첫 주기를 본다.
            for (int i = 0; i < 61; i++) g.Effects.Tick(1f / 60f);
            Require(gu.Hp == 1030 && gb.Hp == 560,
                "R-T3 after ~1 s the unit regained 20 × 1.5 = 30 and the building 40 × 1.5 = 60 — amplified exactly once (unit +" + (gu.Hp - 1000) + ", building +" + (gb.Hp - 500) + ").");
            for (int i = 0; i < 60 * 7 + 4; i++) g.Effects.Tick(1f / 60f); // 합 485프레임 ≈ 8.08초 — 효과 끝까지
            Require(gu.Hp == 1240 && gb.Hp == 980 && g.Effects.ActiveBatchCount == 0,
                "R-T3 8 s at 1/60 s frames gives exactly 8 pulses (unit +240, building +480) (unit +" + (gu.Hp - 1000) + ", building +" + (gb.Hp - 500) + ").");

            // (3) 건물이 최대 HP에서 멈춘다 · 체력 가득이면 건너뛰고 효과는 남는다 → 맞으면 다음 주기부터 다시 회복(B-L7).
            var h = new SupportDamageFixture(true);
            h.Effects.SetBuildingPlacement(h.Buildings);
            UnitData full = RegenWoundedTank(h, 9822, TeamId.Blue, SupportFixtureStatsScope.VictimTankMaxHp);
            BuildingData nearFull = RegenWoundedCastle(h, 97821, TeamId.Blue, 0, 0, SupportFixtureStatsScope.CastleTranscendenceMaxHp - 25);
            RegenApplyRoots(h.Effects, 23, TeamId.Blue);
            for (int i = 0; i < 30; i++) h.Effects.Tick(0.1f); // 3초 — 3주기
            Require(full.Hp == full.MaxHp && h.Effects.UnitHasEffect(full.Id, StatusEffectKind.Regen)
                    && h.Effects.UnitHasEffect(full.Id, StatusEffectKind.HealAmpMul) && h.Effects.GetHealAmpCarry(full.Id, true) == 0d,
                "R-T3 a full-HP unit is skipped but keeps both effects (no carry builds up while full).");
            Require(nearFull.Hp == nearFull.MaxHp && h.Effects.BuildingHasEffect(nearFull.Id, StatusEffectKind.Regen),
                "R-T3 the building regen stops at max HP and the effect stays.");
            full.TakeDamage(100);
            for (int i = 0; i < 11; i++) h.Effects.Tick(0.1f); // ≈4.1초 — 4번째 주기(0.1 float 합 오차를 피해 한 틱 더)
            Require(full.Hp == full.MaxHp - 70, "R-T3 after being hit the full-HP unit heals again on the next pulse (+30) (hp " + full.Hp + ").");
        }

        // ── R-T4 ────────────────────────────────────────────────────────

        /// <summary> R-T4 — 만료 · 교체 · 사망 · 파괴 · 업그레이드 · 새 대상 제외 · Close · 거부. </summary>
        private static void ValidateRegenLifecycle()
        {
            MethodInfo healToUnit = RegenHealToUnitMethod();
            var f = new SupportDamageFixture(true);
            f.Effects.SetBuildingPlacement(f.Buildings);
            UnitData a = RegenWoundedTank(f, 9830, TeamId.Blue, 1000);
            UnitData b = RegenWoundedTank(f, 9831, TeamId.Blue, 1000);
            BuildingData x = RegenWoundedCastle(f, 97830, TeamId.Blue, 0, 0, 500);
            BuildingData y = RegenWoundedCastle(f, 97831, TeamId.Blue, 1, 0, 500);

            Require(f.Effects.Apply(30, TeamId.Blue, 1, 77, TeamId.Blue, SkillEffectTarget.Team, StatusEffectKind.Regen, 20f, 8f) == 0
                    && f.Effects.Apply(30, TeamId.Blue, 1, 77, TeamId.Blue, SkillEffectTarget.Team, StatusEffectKind.HealAmpMul, 1.5f, 8f) == 0
                    && f.Effects.Apply(30, TeamId.Blue, 1, 77, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.Regen, float.NaN, 8f) == 0
                    && f.Effects.Apply(30, TeamId.Blue, 1, 77, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.HealAmpMul, 1.5f, 0f) == 0
                    && f.Effects.ActiveBatchCount == 0,
                "R-T4 team-target regen / amplification and invalid values are rejected without effect.");

            RegenApplyRoots(f.Effects, 31, TeamId.Blue);
            Require(f.Effects.ActiveBatchCount == 4, "R-T4 one Roots activation makes four batches (unit / building × regen / amplification).");
            // 발동 뒤 새 유닛 · 새 건물 — 대상이 아니다(S-11).
            UnitData newcomer = RegenWoundedTank(f, 9832, TeamId.Blue, 1000);
            BuildingData newBuilding = RegenWoundedCastle(f, 97832, TeamId.Blue, 2, 0, 500);
            Require(f.Effects.AmplifyHeal(newcomer, 10, HealAmpSource.Mist) == 10 && f.Effects.AmplifyHeal(newBuilding, 10, HealAmpSource.Mist) == 10
                    && !f.Effects.UnitHasEffect(newcomer.Id, StatusEffectKind.Regen) && !f.Effects.BuildingHasEffect(newBuilding.Id, StatusEffectKind.Regen),
                "R-T4 a unit or building created after activation gets neither regen nor amplification.");

            // 나머지를 남겨 둔 뒤(1 → 1, 남 0.5) 재적용 = 교체 — 나머지 0 · 새 8초.
            f.Effects.AmplifyHeal(a, 1, HealAmpSource.Natural);
            Require(Math.Abs(f.Effects.GetHealAmpCarry(a.Id, true) - 0.5d) < 1e-9, "R-T4 setup: carry 0.5 on unit a.");
            f.Effects.Tick(5f);
            RegenApplyRoots(f.Effects, 32, TeamId.Blue);
            Require(f.Effects.GetHealAmpCarry(a.Id, true) == 0d && f.Effects.ActiveBatchCount == 4,
                "R-T4 re-apply replaces the records (carry reset) and ends the previous four batches.");
            f.Effects.Tick(5f);
            Require(f.Effects.GetHealAmpMultiplier(a.Id, true) == 1.5f, "R-T4 re-apply resets the remaining time to 8 s.");

            // 사망 · 파괴 · 업그레이드 통보 — 기록과 나머지를 버린다.
            f.Effects.NotifyUnitDied(a.Id);
            f.Effects.NotifyBuildingRemoved(x.Id, upgraded: false);
            f.Effects.NotifyBuildingRemoved(y.Id, upgraded: true);
            Require(f.Effects.GetHealAmpMultiplier(a.Id, true) == 1f && f.Effects.GetHealAmpMultiplier(x.Id, false) == 1f
                    && f.Effects.GetHealAmpMultiplier(y.Id, false) == 1f && !f.Effects.BuildingHasEffect(y.Id, StatusEffectKind.Regen),
                "R-T4 death / destruction / upgrade notifications remove regen and amplification.");
            // 통보를 놓친 경우 — 틱이 목록에 없는 유닛을 정리한다.
            f.Spawn.RemoveUnit(b.Id);
            f.Effects.Tick(0.1f);
            Require(!f.Effects.UnitHasEffect(b.Id, StatusEffectKind.HealAmpMul) && !f.Effects.UnitHasEffect(b.Id, StatusEffectKind.Regen)
                    && f.Effects.UnitHasEffect(newcomer.Id, StatusEffectKind.Regen),
                "R-T4 the tick removes effects of units no longer present (the re-applied newcomer keeps its own).");

            // 만료 — 다른 유닛으로 새로 걸고 8초 뒤 배율 1 · 회복 지점 값 그대로.
            var e = new SupportDamageFixture(true);
            UnitData c = RegenWoundedTank(e, 9833, TeamId.Blue, 1000);
            RegenApplyRoots(e.Effects, 33, TeamId.Blue);
            for (int i = 0; i < 81; i++) e.Effects.Tick(0.1f);
            int hpBefore = c.Hp;
            Require(e.Effects.GetHealAmpMultiplier(c.Id, true) == 1f && e.Effects.ActiveBatchCount == 0
                    && RegenHealVia(healToUnit, e.Combat, c, 10, HealAmpSource.Natural) == 10,
                "R-T4 after 8 s heals are exactly the original amount again (hp " + hpBefore + " → " + c.Hp + ").");

            // Close — 남은 효과를 지우고 새 효과를 받지 않는다.
            var z = new SupportDamageFixture(true);
            UnitData d = RegenWoundedTank(z, 9834, TeamId.Blue, 1000);
            RegenApplyRoots(z.Effects, 34, TeamId.Blue);
            z.Effects.Close();
            Require(z.Effects.IsClosed && z.Effects.ActiveBatchCount == 0 && z.Effects.AmplifyHeal(d, 10, HealAmpSource.Mist) == 10,
                "R-T4 Close clears regen and amplification.");
            Require(z.Effects.Apply(35, TeamId.Blue, 1, 77, TeamId.Blue, SkillEffectTarget.Unit, StatusEffectKind.Regen, 20f, 8f) == 0,
                "R-T4 after Close new regen is rejected.");
            z.Effects.Tick(2f);
            Require(d.Hp == 1000, "R-T4 a closed service never heals.");
        }

        // ── R-T5 ────────────────────────────────────────────────────────

        // 회복 이벤트를 모으는 도우미(검사 동안만 구독).
        private sealed class RegenHealedCapture : IDisposable
        {
            public readonly List<EntityHealedEvent> Events = new List<EntityHealedEvent>();
            private readonly IDisposable _subscription;
            public RegenHealedCapture() { _subscription = GameEvents.OnEntityHealed.Subscribe(e => { Events.Add(e); }); }
            public void Dispose() => _subscription?.Dispose();
        }

        /// <summary> R-T5 — 실제로 오른 주기만 회복 이벤트 · 텍스트 3주기마다 · 힐러 = 시전 건물. </summary>
        private static void ValidateRegenHealEvents()
        {
            var f = new SupportDamageFixture(true);
            f.Effects.SetBuildingPlacement(f.Buildings);
            UnitData u = RegenWoundedTank(f, 9840, TeamId.Blue, 1000);
            UnitData full = RegenWoundedTank(f, 9841, TeamId.Blue, SupportFixtureStatsScope.VictimTankMaxHp);
            BuildingData castle = RegenWoundedCastle(f, 97840, TeamId.Blue, 0, 0, 500);
            RegenApplyRoots(f.Effects, 41, TeamId.Blue);
            using (var capture = new RegenHealedCapture())
            {
                for (int i = 0; i < 61; i++) f.Effects.Tick(0.1f); // ≈6.1초 — 6주기(7번째는 7초)
                var unitEvents = capture.Events.FindAll(e => e.IsUnit && e.Entity != null && e.Entity.Id == u.Id);
                var fullEvents = capture.Events.FindAll(e => e.IsUnit && e.Entity != null && e.Entity.Id == full.Id);
                var buildingEvents = capture.Events.FindAll(e => !e.IsUnit && e.Entity != null && e.Entity.Id == castle.Id);
                Require(unitEvents.Count == 6 && buildingEvents.Count == 6 && fullEvents.Count == 0,
                    "R-T5 one healed event per pulse that really healed (unit 6, building 6) and none for the full-HP unit (unit " + unitEvents.Count + ", building " + buildingEvents.Count + ", full " + fullEvents.Count + ").");
                Require(unitEvents.TrueForAll(e => e.HealerId == 77 && !e.HealerIsUnit) && buildingEvents.TrueForAll(e => e.HealerId == 77 && !e.HealerIsUnit),
                    "R-T5 the healer is the caster building (id 77, not a unit).");
                Require(!unitEvents[0].ShowText && !unitEvents[1].ShowText && unitEvents[2].ShowText && !unitEvents[3].ShowText && unitEvents[5].ShowText
                        && unitEvents[5].CurrentHp == u.Hp,
                    "R-T5 the floating text shows every 3rd pulse (3 and 6) — the other pulses only sync HP.");
            }
        }

        // ── R-T6 · R-T7 · R-T8 ─────────────────────────────────────────

        /// <summary> R-T6 — 실행기: 재생의 뿌리는 상태 다리 없이 지원 다리로 4번. </summary>
        private static void ValidateRegenExecutorRouting()
        {
            SkillData roots = AssetDatabase.LoadAssetAtPath<SkillDefinition>(RootsAssetPath).ToSkillData();
            var statusCalls = new List<StatusEffectKind>();
            var supportCalls = new List<(SkillEffectTarget target, StatusEffectKind kind, float mag, float dur)>();
            var ctx = new SkillActivationContext(roots, TeamId.Blue, 1, Vector3.zero, false,
                (t, c, r, d, bId) => { }, (t, c, r, dps, dur) => { },
                (t, k, m, d) => { statusCalls.Add(k); },
                (t, c, r, d) => { },
                null,
                (t, tg, k, m, d) => { supportCalls.Add((tg, k, m, d)); });
            new GlobalStatusChangeExecutor().Execute(ctx);
            Require(statusCalls.Count == 0 && supportCalls.Count == 4
                    && supportCalls[0].target == SkillEffectTarget.Unit && supportCalls[0].kind == StatusEffectKind.Regen && Mathf.Approximately(supportCalls[0].mag, 20f)
                    && supportCalls[1].target == SkillEffectTarget.Building && supportCalls[1].kind == StatusEffectKind.Regen && Mathf.Approximately(supportCalls[1].mag, 40f)
                    && supportCalls[2].target == SkillEffectTarget.Unit && supportCalls[2].kind == StatusEffectKind.HealAmpMul && Mathf.Approximately(supportCalls[2].mag, 1.5f)
                    && supportCalls[3].target == SkillEffectTarget.Building && supportCalls[3].kind == StatusEffectKind.HealAmpMul && Mathf.Approximately(supportCalls[3].mag, 1.5f)
                    && supportCalls.TrueForAll(s => Mathf.Approximately(s.dur, 8f)),
                "R-T6 the executor sends unit regen 20, building regen 40, unit ×1.5 and building ×1.5 (8 s) to the support bridge only — never the status system / HoT.");
            Require(SupportSkillEffectSystem.IsServiceKind(StatusEffectKind.Regen) && SupportSkillEffectSystem.IsServiceKind(StatusEffectKind.HealAmpMul)
                    && !SupportSkillEffectSystem.IsServiceKind(StatusEffectKind.HealOverTime)
                    && SupportSkillEffectSystem.IsSupported(SkillEffectTarget.Unit, StatusEffectKind.Regen)
                    && SupportSkillEffectSystem.IsSupported(SkillEffectTarget.Building, StatusEffectKind.Regen)
                    && SupportSkillEffectSystem.IsSupported(SkillEffectTarget.Unit, StatusEffectKind.HealAmpMul)
                    && SupportSkillEffectSystem.IsSupported(SkillEffectTarget.Building, StatusEffectKind.HealAmpMul)
                    && !SupportSkillEffectSystem.IsSupported(SkillEffectTarget.Team, StatusEffectKind.Regen)
                    && !SupportSkillEffectSystem.IsSupported(SkillEffectTarget.Building, StatusEffectKind.IncomingDamageMul),
                "R-T6 supported combinations: unit / building regen and amplification only (no team regen, no building armor).");
        }

        /// <summary> R-T7 — VFX 경로: 유닛 상태 VFX 장치가 건너뛰지 않는 종류 · 건물 이펙트 기준 = 발동 순간 건물. </summary>
        private static void ValidateRegenVfxRouting()
        {
            SkillData roots = AssetDatabase.LoadAssetAtPath<SkillDefinition>(RootsAssetPath).ToSkillData();
            var kind = (StatusEffectKind)roots.StatusKind;
            Require(roots.Mechanic == SkillMechanicType.GlobalStatusChange && kind != StatusEffectKind.None && kind != StatusEffectKind.HealOverTime
                    && roots.Duration > 0f,
                "R-T7 Roots' main kind is not HealOverTime, so UnitBuffVfxPresenter attaches the unit status VFX (RB-8).");
            Require(BuildingStatusVfxTargetPolicy.Resolve(roots) == BuildingStatusVfxScope.ActivationBuildings
                    && roots.HasBuildingEffect() && !roots.HasBuildingShield(),
                "R-T7 building VFX scope = ActivationBuildings (buildings alive at activation — S-11), and no shield (no break stop).");
            string presenter = File.ReadAllText("Assets/_Project/Scripts/Presentation/Effects/UnitBuffVfxPresenter.cs");
            Require(SupportActiveLineCount(presenter, "if (kind == StatusEffectKind.HealOverTime)") == 1
                    && SupportActiveLineCount(presenter, "StatusEffectKind.Regen") == 0,
                "R-T7 the unit VFX presenter skips only HealOverTime — Regen needs no special case.");
        }

        /// <summary> R-T8 — 재생의 뿌리 자산 계약. </summary>
        private static void ValidateRegenAsset()
        {
            const string loadoutPath = "Assets/_Project/Resources/Config/Skills/SkillLoadoutConfig.asset";
            SkillLoadoutConfig loadout = AssetDatabase.LoadAssetAtPath<SkillLoadoutConfig>(loadoutPath);
            Require(loadout != null, "R-T8 SkillLoadoutConfig asset must exist.");
            loadout.ClearCache();

            SkillData roots = AssetDatabase.LoadAssetAtPath<SkillDefinition>(RootsAssetPath).ToSkillData();
            Require(roots.IsAvailable && roots.AimType == SkillAimType.Instant && roots.Mechanic == SkillMechanicType.GlobalStatusChange
                    && (StatusEffectKind)roots.StatusKind == StatusEffectKind.Regen && Mathf.Approximately(roots.Magnitude, 20f)
                    && Mathf.Approximately(roots.Duration, 8f) && Mathf.Approximately(roots.Cooldown, 15f) && roots.TargetsAllies,
                "R-T8 Roots of Regeneration must be available / instant / type C / kind 9 / 20 per s / 8 s / cooldown 15 / allies (Plan S-1 temporary values).");
            Require(roots.ExtraEffects.Count == 3
                    && roots.ExtraEffects[0].Target == SkillEffectTarget.Building && roots.ExtraEffects[0].Kind == StatusEffectKind.Regen && Mathf.Approximately(roots.ExtraEffects[0].Magnitude, 40f)
                    && roots.ExtraEffects[1].Target == SkillEffectTarget.Unit && roots.ExtraEffects[1].Kind == StatusEffectKind.HealAmpMul && Mathf.Approximately(roots.ExtraEffects[1].Magnitude, 1.5f)
                    && roots.ExtraEffects[2].Target == SkillEffectTarget.Building && roots.ExtraEffects[2].Kind == StatusEffectKind.HealAmpMul && Mathf.Approximately(roots.ExtraEffects[2].Magnitude, 1.5f),
                "R-T8 Roots has exactly three extras: building regen 40, unit ×1.5, building ×1.5.");
            var trans = loadout.GetLoadout(RaceId.Transcendence);
            Require(trans != null && trans.Count > 1 && trans[1] != null && trans[1].DisplayName == roots.DisplayName && trans[1].IsAvailable,
                "R-T8 Roots of Regeneration must be the available skill in Transcendence slot 1 (button 2).");
            foreach (GameObject prefab in new[] { roots.StatusVfxPrefab, roots.BuildingStatusVfxPrefab })
            {
                if (prefab == null) continue; // 빈 칸은 통과(테스트 뒤 비운다 — B-L12).
                Require(EffectManager.CheckUnitBuffLoopContract(prefab),
                    "R-T8 a filled Roots status VFX (" + prefab.name + ") must satisfy the loop contract (Looping ON, Stop Action None).");
                foreach (ParticleSystem ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
                    Require(ps.main.scalingMode == ParticleSystemScalingMode.Hierarchy,
                        "R-T8 Roots status VFX particle '" + ps.name + "' must use Scaling Mode Hierarchy.");
            }
        }

        // ── R-T9 ────────────────────────────────────────────────────────

        /// <summary> R-T9 — 소스 게이트(회복 지점 · 출처 · 주입 · Tick 순서 · 셋업 · 스키마). </summary>
        private static void ValidateRegenSourceGates()
        {
            const string root = "Assets/_Project/Scripts/";
            string combat = File.ReadAllText(root + "Application/UseCases/UnitCombatUseCase.cs");
            Require(SupportActiveLineCount(combat, "_supportEffects.AmplifyHeal(target, amount, ampSource)") == 1
                    && SupportActiveLineCount(combat, "target.Heal(healAmount);") == 1
                    && SupportActiveLineCount(combat, "target.Heal(amount);") == 0,
                "R-T9 heal point ① must amplify once right before the single Heal in ApplyHealToUnit (old direct Heal only as a comment).");
            Require(SupportActiveLineCount(combat, "ampSource: HealAmpSource.Natural") == 1
                    && SupportActiveLineCount(combat, "HealAmpSource.Bloom : HealAmpSource.HoT") == 1
                    && SupportActiveLineCount(combat, "ampSource: HealAmpSource.Wave") == 1,
                "R-T9 natural regen / HoT (BloomFairy) / wave must pass their heal source.");

            string mist = File.ReadAllText(root + "Application/UseCases/MistShrineUseCase.cs");
            Require(SupportActiveLineCount(mist, "target.Heal(AmplifiedMistHeal(target));") == 2
                    && SupportActiveLineCount(mist, "target.Heal(_healPerTick);") == 0
                    && SupportActiveLineCount(mist, "_supportEffects.AmplifyHeal(target, _healPerTick, HealAmpSource.Mist)") == 1,
                "R-T9 heal points ② ③ (mist unit / building) must heal the amplified amount (old direct Heal only as comments).");

            string setup = File.ReadAllText(root + "Bootstrap/GameBootstrapper.Setup.cs");
            Require(setup.Contains("_mistShrine.SetSupportSkillEffectSystem(_supportSkillEffects);")
                    && setup.Contains("_unitCombat.SetSupportSkillEffectSystem(_supportSkillEffects);"),
                "R-T9 the composition root must inject the same service into the mist and combat use cases.");

            string service = File.ReadAllText(root + "Application/Services/SupportSkillEffectSystem.cs").Replace("\r\n", "\n");
            int regenTick = service.IndexOf("TickRegen(dt);", StringComparison.Ordinal);
            int unitStore = service.IndexOf("TickStore(_unitEffects, dt, checkUnitAlive: true);", StringComparison.Ordinal);
            Require(regenTick > 0 && unitStore > regenTick,
                "R-T9 Tick must heal regen before expiring records (the last pulse and its amplification happen before expiry).");

            FieldInfo expected = typeof(Hexiege.EditorTools.SupportSkillLoadoutSetup).GetField("ExpectedAvailableCount",
                BindingFlags.NonPublic | BindingFlags.Static);
            Require(expected != null && (int)expected.GetRawConstantValue() == 11,
                "R-T9 SupportSkillLoadoutSetup.ExpectedAvailableCount must be 11 (Roots of Regeneration enabled).");
            Require(UnitAttackShadowProfileResolver.PresentationContractSchemaRevision == 10,
                "R-T9 stage B adds no RPC — the presentation schema stays 10 (heal HP uses the existing heal sync).");
        }
    }
}
