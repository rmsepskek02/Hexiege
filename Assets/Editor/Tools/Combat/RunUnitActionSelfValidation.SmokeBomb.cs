// ============================================================================
// RunUnitActionSelfValidation.SmokeBomb.cs
// [2026-11-23 지원 스킬 연막탄 — 단계 S-1(구역 · 상태 · 자산 · 공용 난수) + S-2(Hitscan 총잡이 회피)] 회귀 검증(fixture).
// Task: Assets/_Project/Docs/_Tasks/2026-11-23/15_28_support-skill-smoke-bomb/ (Plan 7절 E-T 중 S-1 · S-2 해당 항목).
// Run()(메뉴 「Hexiege/Combat/Run Full Regression Suite」)이 함께 돌린다.
//
// [배경 — 초급 개발자용 설명]
//   연막탄은 "연막 안의 우리 편 유닛이 적의 원거리 단일 공격을 50% 확률로 피한다"는 스킬이다.
//   확률은 눈으로 맞는지 알기 어렵고(10번 중 5번? 6번?), 피했을 때 "피해가 정말 0인지 · 숫자가 정말 안 뜨는지"도
//   한 프레임 사이에 지나간다. 그래서 실제 구역 서비스 · 회피 판정 서비스 · 전투 writer로 자동 확인한다.
//   확률은 1.0 · 0.0으로 결정적으로 검사하고, 중간값(0.5)은 고정 seed + 열쇠로 기대값을 다시 계산해 맞춘다(Plan 7절 원칙).
//
// 무엇을 확인하는가(Plan 7절 번호):
//   E-T10 난수 해시 결정성 · 경계 — 같은 seed · 열쇠 → 같은 결과, 다른 계산이 끼어도 불변, 확률 0 → 항상 명중 · 1 → 항상 회피,
//         경계 「눈금 == 확률 − 1 → 회피 · 눈금 == 확률 → 명중」, 0.5의 표본 비율, 경로별 열쇠 분리, 빗나간 지점 식, 싱글 대체 열쇠 순번.
//   E-T1  아군 구역 구성원 — 시전 팀 유닛만 확률 0.5, 적 유닛 · 구역 밖 0, 구역 없음 0, 연막은 이동 · 공격에 영향 없음, 실행기 검사.
//   E-T2  같은 스킬 겹침 1번 · 이탈 · 만료 · 사망 · 경기 종료 → 0.
//   E-T3  Hitscan 회피(확률 1.0) — 싱글 writer · 멀티 writer(결과 키) · 묶음(번들) 경로: HP 그대로 · 피해 이벤트 0(= 피해 숫자 원천 없음) ·
//         writer 상태 Evaded · 관측 유효. 확률 0 · 연막 밖 · 적 연막 · 총잡이가 아닌 공격자 → 판정 없이 기존 피해(회귀).
//         확률 0.5 — 40발의 회피 여부가 seed + (공격자 Id, 순번) 재계산과 정확히 같고, HP 감소 = 공격력 × 명중 수.
//   E-T6  이중 판정 없음 — 투사체 착탄 처리 중이면 Hitscan 관문이 굴리지 않는다(순번이 늘지 않고 피해는 그대로 들어간다).
//   E-T4  C2 결과 — 「승인 명중 + writer Evaded」가 AttackImpactOutcome.Evaded로 닫힌다(거절 아님) · 관찰기 불일치 0 ·
//         Client 수신 검사 통과 · 화면 시각 결과 아님(숫자 없음) · 「승인 Miss + Evaded」는 위조하지 않고 거절.
//   E-T12 연막탄 자산 계약(Z-T9 「사용 불가」 검사 대체) · 셋업 표 줄 · 소스 게이트(관문 자리 · 조합 루트 · 경기 종료 닫기).
//
// 실제 씬 · 멀티 전송 · 화면은 여기서 확인하지 않는다(사용자 실기 항목 — Plan 8절).
// 전역 스탯 표는 비어 있거나 실제 값일 수 있으므로 HP를 보는 검사는 SupportFixtureStatsScope(빈 표 + 필요한 행만 고정 + 끝나면 복원) 안에서 돈다.
// 이벤트 구독은 finally / Dispose에서 해제한다(다른 검사에 영향 없음).
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Domain;
using Hexiege.Infrastructure;
using UniRx;
using UnityEditor;
using UnityEngine;

namespace Hexiege.Editor.Combat
{
    public static partial class RunUnitActionSelfValidation
    {
        // 검증 fixture 고정 seed(경기 seed 자리). 값 자체에는 의미가 없다 — 고정이라 결과가 매번 같다.
        private const ulong SmokeFixtureSeed = 0x5EED5EED12345678UL;

        /// <summary>
        /// 연막탄 회귀 검증 진입점(E-T10 · E-T1 · E-T2 · E-T3 · E-T6 · E-T4 · E-T12 소스 게이트). 자산 계약(E-T12)은 Z-T9가 부른다.
        /// </summary>
        private static void ValidateSmokeBombSkill()
        {
            Require(!UnityEngine.Application.isPlaying, "Smoke Bomb fixture must run outside a live match.");
            ValidateSmokeEvadeRollDeterminism();       // E-T10
            ValidateSmokeZoneAllyMembership();         // E-T1
            ValidateSmokeZoneOverlapAndRelease();      // E-T2
            using (new SupportFixtureStatsScope())
            {
                ValidateSmokeHitscanEvasion();         // E-T3
                ValidateSmokeNoDoubleRollAtProjectileImpact(); // E-T6
            }
            ValidateSmokeC2EvadedOutcome();            // E-T4
            ValidateSmokeBombSourceGates();            // E-T12(소스 · 셋업 표)
            Debug.Log("[UAS-DIAG][SKILL-EVADE] self-validation PASS: deterministic seed+key hash with boundary 0% / 100% / bucket == chance-1 evades and == chance hits, order-independent, 0.5 sample rate, per-path keys, miss point formula, single fallback key ordinal (E-T10), ally-only smoke membership with no move / attack side effect and executor guards (E-T1), same-skill overlap once and exit / expiry / death / Close release (E-T2), Hitscan evasion on the single writer, the keyed writer and the bundle writer with HP unchanged and zero damage events plus no-roll regressions and 0.5 recomputation (E-T3), no second roll during a projectile impact (E-T6), C2 AuthorizedHit + Evaded closes as Evaded with no observer mismatch while AuthorizedMiss + Evaded is rejected (E-T4), setup table line + source gates (E-T12; asset contract via Z-T9).");
        }

        // 연막 fixture — 새 유닛 목록 · 전투 UseCase · 구역 서비스 · 회피 서비스 + 피해 · 공격받음 이벤트 수집.
        private sealed class SmokeFixture : IDisposable
        {
            public readonly UnitSpawnUseCase Spawn;
            public readonly UnitCombatUseCase Combat;
            public readonly SkillZoneSystem Zones;
            public readonly SkillEvasionSystem Evasion;
            public int DamagedEvents;
            public int AttackedEvents;
            private readonly IDisposable _damagedSub;
            private readonly IDisposable _attackedSub;

            public SmokeFixture(ulong seed = SmokeFixtureSeed)
            {
                var grid = new HexGrid(3, 3, HexOrientation.FlatTop);
                Spawn = new UnitSpawnUseCase(grid);
                var buildings = new BuildingPlacementUseCase(grid);
                var mapper = new ProjectileFixtureMapper(); // 타일 (q, r) → 월드 (q, 0, r)
                Combat = new UnitCombatUseCase(grid, Spawn, buildings, new ProjectileFixturePositions(), mapper);
                Zones = new SkillZoneSystem(Spawn, mapper, buildings);
                Combat.SetSkillZoneSystem(Zones);
                Evasion = new SkillEvasionSystem(Zones, seed);
                Combat.SetSkillEvasionSystem(Evasion);
                _damagedSub = GameEvents.OnEntityDamaged.Subscribe(_ => { DamagedEvents++; });
                _attackedSub = GameEvents.OnEntityAttacked.Subscribe(_ => { AttackedEvents++; });
            }

            // 3×3 그리드 안 타일에 만든 뒤 판정용 좌표로 옮긴다(구역 판정은 Position만 읽는다 — 매퍼가 그대로 월드로 바꾼다).
            public UnitData Unit(int id, TeamId team, int q, int r, UnitType type)
            {
                UnitData unit = Spawn.SpawnUnitWithId(id, type, team, new HexCoord(0, 0));
                Require(unit != null, "E-T0 smoke fixture unit must spawn: " + id);
                unit.Position = new HexCoord(q, r);
                return unit;
            }

            // 시전 팀 = Blue, 슬롯 3(인간 4번 칸), 중심 (0, 0, 0), 반경 2, 6초, 아군 대상.
            public int Smoke(int sequence, float chance, TeamId caster = TeamId.Blue, int slot = 3, Vector3 center = default,
                             float duration = 6f)
                => Zones.CreateZone(sequence, caster, slot, 77, center, 2f, duration, StatusEffectKind.EvadeChance, chance, true);

            public void Dispose()
            {
                _damagedSub?.Dispose();
                _attackedSub?.Dispose();
            }
        }

        /// <summary> E-T10 — 결정적 해시 · 경계 · 순서 독립 · 표본 비율 · 열쇠 분리 · 빗나간 지점 · 싱글 대체 열쇠. </summary>
        private static void ValidateSmokeEvadeRollDeterminism()
        {
            // ① 같은 seed · 같은 열쇠 → 같은 roll(그리고 정의 식 Mix64(Combine(seed, key))와 같다).
            ulong key = SkillEvasionRoll.KeyFromAttackerOrdinal(9801, 1UL);
            ulong a = SkillEvasionRoll.ComputeRoll(SmokeFixtureSeed, key);
            ulong b = SkillEvasionRoll.ComputeRoll(SmokeFixtureSeed, key);
            Require(a == b && a == MapRandom.Mix64(MapRandom.Combine(SmokeFixtureSeed, key)),
                "E-T10 the same seed and key must give the same roll = Mix64(Combine(seed, key)).");
            Require(SkillEvasionRoll.ComputeRoll(SmokeFixtureSeed + 1UL, key) != a,
                "E-T10 a different match seed must give a different roll for the same key.");

            // ② 순서 독립 — 그 사이에 다른 판정 · 다른 난수 스트림을 아무리 돌려도 같은 열쇠의 결과는 그대로.
            var stream = new MapRandom(42UL);
            for (int i = 0; i < 1000; i++) { stream.NextUInt64(); SkillEvasionRoll.ComputeRoll(SmokeFixtureSeed, (ulong)i); }
            Require(SkillEvasionRoll.ComputeRoll(SmokeFixtureSeed, key) == a,
                "E-T10 other random draws between two rolls must not change the roll of the same key (order independence).");

            // ③ 확률 → 만분율.
            Require(SkillEvasionRoll.ToChanceBasisPoints(0f) == 0 && SkillEvasionRoll.ToChanceBasisPoints(-1f) == 0
                    && SkillEvasionRoll.ToChanceBasisPoints(float.NaN) == 0 && SkillEvasionRoll.ToChanceBasisPoints(0.5f) == 5000
                    && SkillEvasionRoll.ToChanceBasisPoints(1f) == 10000 && SkillEvasionRoll.ToChanceBasisPoints(2f) == 10000
                    && SkillEvasionRoll.ToChanceBasisPoints(float.PositiveInfinity) == 10000,
                "E-T10 chance → basis points must map ≤0/NaN → 0, 0.5 → 5000, ≥1/∞ → 10000.");

            // ④ 경계 — 확률 0 → 항상 명중, 1.0 → 항상 회피, 눈금 == 확률 − 1 → 회피, 눈금 == 확률 → 명중.
            int evadedHalf = 0;
            const int samples = 10000;
            for (int i = 0; i < samples; i++)
            {
                ulong roll = SkillEvasionRoll.ComputeRoll(SmokeFixtureSeed, SkillEvasionRoll.KeyFromAttackerOrdinal(7, (ulong)(i + 1)));
                int bucket = SkillEvasionRoll.RollBucket(roll);
                Require(bucket >= 0 && bucket < SkillEvasionRoll.ChanceScale, "E-T10 roll bucket must be within 0..9999.");
                Require(!SkillEvasionRoll.IsEvaded(roll, 0) && SkillEvasionRoll.IsEvaded(roll, SkillEvasionRoll.ChanceScale),
                    "E-T10 chance 0 must always hit and chance 10000 must always evade.");
                Require(SkillEvasionRoll.IsEvaded(roll, bucket + 1) && !SkillEvasionRoll.IsEvaded(roll, bucket),
                    "E-T10 boundary: bucket == chance − 1 must evade and bucket == chance must hit.");
                if (SkillEvasionRoll.IsEvaded(roll, 5000)) evadedHalf++;
            }
            // 이항분포 표준편차 50 — ±300(6σ) 밖이면 해시가 한쪽으로 치우친 것이다.
            Require(evadedHalf > 4700 && evadedHalf < 5300,
                "E-T10 a 0.5 chance over 10000 keys must evade about half the time (got " + evadedHalf + ").");

            // ⑤ 경로별 열쇠 분리 — 같은 숫자라도 경로가 다르면 다른 열쇠.
            Require(SkillEvasionRoll.KeyFromAttackerOrdinal(5, 1UL) != SkillEvasionRoll.KeyFromProjectile(5UL, 1UL)
                    && SkillEvasionRoll.KeyFromAttackerOrdinal(5, 1UL) != SkillEvasionRoll.KeyFromAttackerOrdinal(5, 2UL)
                    && SkillEvasionRoll.KeyFromAttackerOrdinal(5, 1UL) != SkillEvasionRoll.KeyFromAttackerOrdinal(6, 1UL),
                "E-T10 keys must differ by path tag, ordinal and attacker.");
            var resultKey = new AttackResultKey(new AttackerInstanceId(11UL), new AttackSequenceId(3UL), 0, 1, 9802, 0, 0);
            var resultKeyOtherHit = new AttackResultKey(new AttackerInstanceId(11UL), new AttackSequenceId(3UL), 1, 1, 9802, 0, 0);
            Require(SkillEvasionRoll.KeyFromResultKey(resultKey) == SkillEvasionRoll.KeyFromResultKey(resultKey)
                    && SkillEvasionRoll.KeyFromResultKey(resultKey) != SkillEvasionRoll.KeyFromResultKey(resultKeyOtherHit),
                "E-T10 the C2 result key must hash deterministically and a different hit index must give a different key.");

            // ⑥ 빗나간 지점 — M = C + n × (r + 0.3) + d × 0.5, 좌우 = roll 가장 위 비트, 방향을 못 정하면 +Z.
            Vector3 launch = new Vector3(0f, 0f, 0f), center = new Vector3(4f, 0.6f, 0f);
            ulong leftRoll = 1UL << 63, rightRoll = 0UL;
            Vector3 left = SkillEvasionRoll.ComputeMissPoint(launch, center, 0.4f, leftRoll);
            Vector3 right = SkillEvasionRoll.ComputeMissPoint(launch, center, 0.4f, rightRoll);
            Require(Mathf.Abs(left.x - 4.5f) < 1e-4f && Mathf.Abs(left.z - 0.7f) < 1e-4f && Mathf.Abs(left.y - 0.6f) < 1e-4f
                    && Mathf.Abs(right.x - 4.5f) < 1e-4f && Mathf.Abs(right.z + 0.7f) < 1e-4f,
                "E-T10 miss point must be target centre + side (radius + 0.3) + forward 0.5, left/right by the top roll bit, Y = centre Y.");
            Vector3 degenerate = SkillEvasionRoll.ComputeMissPoint(center, center, float.NaN, rightRoll);
            Require(Mathf.Abs(degenerate.x - (4f + 0.3f)) < 1e-4f && Mathf.Abs(degenerate.z - 0.5f) < 1e-4f,
                "E-T10 a launch at the target centre must fall back to +Z with radius NaN treated as 0.");

            // ⑦ 싱글 대체 열쇠 — 공격자마다 따로 1, 2, 3 … (다른 공격자의 판정이 끼어도 자기 순번은 그대로).
            var service = new SkillEvasionSystem(null, SmokeFixtureSeed);
            ulong k1 = service.NextAttackerOrdinalKey(100, out ulong o1);
            service.NextAttackerOrdinalKey(200, out ulong other);
            ulong k2 = service.NextAttackerOrdinalKey(100, out ulong o2);
            Require(o1 == 1UL && o2 == 2UL && other == 1UL
                    && k1 == SkillEvasionRoll.KeyFromAttackerOrdinal(100, 1UL) && k2 == SkillEvasionRoll.KeyFromAttackerOrdinal(100, 2UL),
                "E-T10 the single-player fallback key must count per attacker from 1, independent of other attackers.");
            Require(service.GetEvadeChance(100) == 0f && service.MatchSeed == SmokeFixtureSeed,
                "E-T10 without a zone service the evade chance must be 0 and the seed kept.");
            bool evaded = service.Roll(k1, 10000, out ulong r1);
            Require(evaded && r1 == SkillEvasionRoll.ComputeRoll(SmokeFixtureSeed, k1) && !service.Roll(k1, 0, out _),
                "E-T10 the service roll must equal the pure function and respect chance 0 / 10000.");
            service.Close(); service.Close();
            Require(service.IsClosed, "E-T10 Close must be idempotent.");
        }

        /// <summary> E-T1 — 아군 구역 구성원 · 이동 · 공격 무영향 · 실행기 검사. </summary>
        private static void ValidateSmokeZoneAllyMembership()
        {
            using (var f = new SmokeFixture())
            {
                UnitData blueIn = f.Unit(9810, TeamId.Blue, 0, 0, UnitType.Pistoleer);
                UnitData redIn = f.Unit(9811, TeamId.Red, 1, 0, UnitType.Pistoleer);
                UnitData blueOut = f.Unit(9812, TeamId.Blue, 10, 10, UnitType.Pistoleer);
                Require(f.Zones.GetEvadeChance(blueIn.Id) == 0f && f.Evasion.GetEvadeChance(blueIn.Id) == 0f,
                    "E-T1 with no zone the evade chance must be exactly 0 (normal combat path).");

                int members = f.Smoke(1, 0.5f);
                Require(members == 1, "E-T1 the ally smoke must take only the caster-team unit inside (got " + members + ").");
                Require(Mathf.Approximately(f.Zones.GetEvadeChance(blueIn.Id), 0.5f) && Mathf.Approximately(f.Evasion.GetEvadeChance(blueIn.Id), 0.5f),
                    "E-T1 a caster-team unit inside the smoke must get evade chance 0.5.");
                Require(f.Zones.GetEvadeChance(redIn.Id) == 0f && !f.Zones.IsInAnyZone(redIn.Id),
                    "E-T1 an enemy unit standing in the smoke must get no benefit (not a member).");
                Require(f.Zones.GetEvadeChance(blueOut.Id) == 0f, "E-T1 a caster-team unit outside the smoke must get 0.");
                Require(f.Combat.GetUnitMoveSpeedMultiplier(blueIn) == 1f && f.Combat.CanAttack(blueIn)
                        && f.Combat.GetUnitMoveSpeedMultiplier(redIn) == 1f && f.Combat.CanAttack(redIn),
                    "E-T1 the smoke must not change movement or attacking (only the evade chance).");
            }

            // 실행기 — 회피 확률 0~1만 받고, 자산의 아군 여부를 그대로 넘긴다.
            // Z-T8과 같은 방식으로 스킬 데이터 · 컨텍스트를 만든다(구역 다리만 기록하고 나머지 다리는 빈 람다).
            int calls = 0; StatusEffectKind gotKind = StatusEffectKind.None; float gotMag = -1f; bool gotAllies = false;
            Action<TeamId, Vector3, float, float, StatusEffectKind, float, bool> bridge = (t, c, r, d, k, m, al) =>
            { calls++; gotKind = k; gotMag = m; gotAllies = al; };
            SkillData Skill(float mag) => new SkillData(
                null, SkillAimType.PointTarget, 15f, SkillMechanicType.PersistentStatusZone, 2f, 6f, 0, 0f,
                (int)StatusEffectKind.EvadeChance, mag, true, "smoke", "smoke", true, 0f, null, 2f);
            SkillActivationContext Ctx(SkillData sd) => new SkillActivationContext(sd, TeamId.Blue, 3,
                new Vector3(1f, 0f, 1f), true, (t, c, r, d, b) => { }, (t, c, r, dps, dur) => { }, (t, k, m, d) => { },
                (t, c, r, d) => { }, bridge);
            var executor = new PersistentStatusZoneExecutor();
            executor.Execute(Ctx(Skill(0.5f)));
            Require(calls == 1 && gotKind == StatusEffectKind.EvadeChance && Mathf.Approximately(gotMag, 0.5f) && gotAllies,
                "E-T1 the zone executor must accept EvadeChance 0.5 and pass targetsAllies = true.");
            executor.Execute(Ctx(Skill(1.5f)));
            executor.Execute(Ctx(Skill(-0.1f)));
            executor.Execute(Ctx(Skill(float.NaN)));
            Require(calls == 1, "E-T1 the zone executor must reject an evade chance above 1, below 0 or NaN.");
            Require((int)StatusEffectKind.EvadeChance == 11 && (int)StatusEffectKind.HealAmpMul == 10,
                "E-T1 EvadeChance must be appended as 11 (serialized integer — existing values unchanged).");
        }

        /// <summary> E-T2 — 같은 스킬 겹침 1번 · 다른 출처 독립 합성 · 이탈 · 만료 · 사망 · 경기 종료. </summary>
        private static void ValidateSmokeZoneOverlapAndRelease()
        {
            using (var f = new SmokeFixture())
            {
                UnitData u = f.Unit(9820, TeamId.Blue, 0, 0, UnitType.Pistoleer);
                UnitData dier = f.Unit(9821, TeamId.Blue, 1, 0, UnitType.Pistoleer);
                f.Smoke(1, 0.5f);
                f.Smoke(2, 0.5f, center: new Vector3(1f, 0f, 0f)); // 같은 팀 · 같은 슬롯 = 같은 스킬
                Require(Mathf.Approximately(f.Zones.GetEvadeChance(u.Id), 0.5f),
                    "E-T2 two overlapping smokes of the same skill must apply the chance once (0.5, not 0.75).");
                // 다른 출처(지금은 존재하지 않는 회피 스킬 — 다른 슬롯으로 흉내)는 서로 독립 → 1 − 0.5 × 0.5 = 0.75.
                f.Smoke(3, 0.5f, slot: 2);
                Require(Mathf.Abs(f.Zones.GetEvadeChance(u.Id) - 0.75f) < 1e-5f,
                    "E-T2 evade zones from different skills must combine as independent rolls: 1 − (1 − a)(1 − b).");

                // 사망 → 구성원에서 바로 빠진다.
                f.Zones.NotifyUnitDied(dier.Id);
                Require(f.Zones.GetEvadeChance(dier.Id) == 0f, "E-T2 a dead unit must drop out of the smoke at once.");

                // 이탈 → 다음 틱에 0.
                u.Position = new HexCoord(10, 10);
                f.Zones.Tick(0.1f);
                Require(f.Zones.GetEvadeChance(u.Id) == 0f, "E-T2 a unit that walks out must get 0 on the next tick.");
                u.Position = new HexCoord(0, 0);
                f.Zones.Tick(0.1f);
                Require(f.Zones.GetEvadeChance(u.Id) > 0f, "E-T2 a unit that walks back in must get the chance again (late entry).");

                // 만료 → 0.
                f.Zones.Tick(10f);
                Require(f.Zones.ZoneCount == 0 && f.Zones.GetEvadeChance(u.Id) == 0f, "E-T2 after the 6 s expiry the chance must be 0.");

                // 경기 종료 → 0 · 새 연막 거부.
                f.Smoke(4, 0.5f);
                Require(f.Zones.GetEvadeChance(u.Id) > 0f, "E-T2 fixture smoke after expiry must apply again.");
                f.Zones.Close();
                Require(f.Zones.GetEvadeChance(u.Id) == 0f && f.Smoke(5, 0.5f) == -1,
                    "E-T2 game end Close must release the smoke and reject a new one.");
            }
        }

        /// <summary> E-T3 — Hitscan 회피(싱글 writer · 결과 키 writer · 묶음 writer) + 판정 없는 회귀 + 0.5 재계산. </summary>
        private static void ValidateSmokeHitscanEvasion()
        {
            int attack = SupportFixtureStatsScope.AttackerAttackPower;
            using (var f = new SmokeFixture())
            {
                UnitData shooter = f.Unit(9830, TeamId.Red, 2, 0, UnitType.Pistoleer);
                UnitData victim = f.Unit(9831, TeamId.Blue, 0, 0, UnitType.Tank);
                Require(shooter.AttackPower == attack && victim.Hp == SupportFixtureStatsScope.VictimTankMaxHp,
                    "E-T3 fixture stats must be pinned (Pistoleer attack " + attack + ", Tank HP " + SupportFixtureStatsScope.VictimTankMaxHp + ").");

                // 회귀(연막 없음) — 판정 없이 기존 피해 그대로.
                int hp0 = victim.Hp;
                f.Combat.ApplyAttackDamage(shooter, victim.Id, true);
                Require(victim.Hp == hp0 - attack && f.DamagedEvents == 1,
                    "E-T3 without a smoke the Pistoleer hit must deal its attack power and raise one damage event (unchanged path).");

                // 확률 1.0 연막 — 싱글 writer(결과 키 없음 → 대체 열쇠).
                f.Smoke(1, 1f);
                int hp1 = victim.Hp; int damaged1 = f.DamagedEvents; int attacked1 = f.AttackedEvents;
                f.Combat.ApplyAttackDamage(shooter, victim.Id, true);
                AttackDamageObservation single = f.Combat.ApplyAttackDamageObserved(shooter, victim.Id, true);
                Require(victim.Hp == hp1 && f.DamagedEvents == damaged1 && f.AttackedEvents == attacked1,
                    "E-T3 an evaded hit must leave HP unchanged and raise no damage / attacked event (so no damage number).");
                Require(single.Status == AttackDamageApplyStatus.Evaded && single.IsValid && single.AppliedAmount == 0
                        && single.ResultingHp == hp1 && single.HasPresentationSnapshot,
                    "E-T3 the writer must return a valid Evaded observation (applied 0, resulting HP = current HP).");

                // 결과 키가 있는 writer(멀티 C2 경로) — 같은 회피.
                var key = new AttackResultKey(new AttackerInstanceId(31UL), new AttackSequenceId(1UL), 0, 1, victim.Id, 0, 0);
                AttackDamageObservation keyed = f.Combat.ApplyAttackDamageObserved(
                    shooter, victim.Id, true, AttackDamageApplyStatus.Applied, key);
                Require(keyed.Status == AttackDamageApplyStatus.Evaded && keyed.IsValid && victim.Hp == hp1,
                    "E-T3 the keyed multiplayer writer must also evade with HP unchanged.");
                AttackDamagePresentationBundleObservation bundle = f.Combat.ApplyAttackDamageObservedWithPresentationBundle(
                    shooter, victim.Id, true, AttackDamageApplyStatus.Applied,
                    new AttackResultKey(new AttackerInstanceId(31UL), new AttackSequenceId(2UL), 0, 1, victim.Id, 0, 0));
                Require(bundle.IsComplete && bundle.PrimaryObservation.Status == AttackDamageApplyStatus.Evaded
                        && bundle.Facts.Length == 0 && victim.Hp == hp1 && f.DamagedEvents == damaged1,
                    "E-T3 the bundle writer must complete with the Evaded primary result and zero applied facts.");

                // 승인 거절(Miss)은 writer가 판정 전에 끝낸다 — 회피 판정을 하지 않는다(대체 열쇠 순번이 늘지 않는다).
                f.Evasion.NextAttackerOrdinalKey(shooter.Id, out ulong before);
                f.Combat.ApplyAttackDamageObserved(shooter, victim.Id, true, AttackDamageApplyStatus.CombatConditionFailed);
                f.Evasion.NextAttackerOrdinalKey(shooter.Id, out ulong after);
                Require(after == before + 1UL, "E-T3 an authorization rejection must not roll (no fallback ordinal consumed).");

                // 총잡이가 아닌 공격자(투사체 유닛 Tank)는 이 관문 대상이 아니다 — 연막 안이어도 기존 피해.
                UnitData tankShooter = f.Unit(9832, TeamId.Red, 2, 1, UnitType.Tank);
                int hp2 = victim.Hp;
                f.Combat.ApplyAttackDamage(tankShooter, victim.Id, true);
                Require(victim.Hp < hp2, "E-T3 a non-Hitscan attacker must not be evaded by the Hitscan gate.");

                // 적 연막(시전 팀 Red) 안의 Blue 유닛 · 연막 밖 유닛 → 혜택 없음.
                UnitData blueOutside = f.Unit(9833, TeamId.Blue, 10, 10, UnitType.Tank);
                int hp3 = blueOutside.Hp;
                f.Combat.ApplyAttackDamage(shooter, blueOutside.Id, true);
                Require(blueOutside.Hp == hp3 - attack, "E-T3 a unit outside the smoke must be hit normally.");
            }

            // 확률 0.5 — 40발의 결과가 seed + (공격자 Id, 순번) 재계산과 정확히 같다.
            using (var f = new SmokeFixture())
            {
                UnitData shooter = f.Unit(9840, TeamId.Red, 2, 0, UnitType.Pistoleer);
                UnitData victim = f.Unit(9841, TeamId.Blue, 0, 0, UnitType.Tank);
                f.Smoke(1, 0.5f);
                int hits = 0, evades = 0, start = victim.Hp;
                for (int i = 1; i <= 40; i++)
                {
                    int hp = victim.Hp;
                    AttackDamageObservation o = f.Combat.ApplyAttackDamageObserved(shooter, victim.Id, true);
                    bool expected = SkillEvasionRoll.IsEvaded(SkillEvasionRoll.ComputeRoll(SmokeFixtureSeed,
                        SkillEvasionRoll.KeyFromAttackerOrdinal(shooter.Id, (ulong)i)), 5000);
                    bool evaded = o.Status == AttackDamageApplyStatus.Evaded;
                    Require(evaded == expected, "E-T3 shot " + i + " evasion must match the offline recomputation (seed + attacker ordinal).");
                    Require(evaded ? victim.Hp == hp : victim.Hp == hp - attack, "E-T3 shot " + i + " HP change must follow the evasion result.");
                    if (evaded) evades++; else hits++;
                }
                Require(evades > 0 && hits > 0 && start - victim.Hp == hits * attack && f.DamagedEvents == hits,
                    "E-T3 with chance 0.5 some shots must evade and some hit; HP loss = attack × hits, damage events = hits (got evades "
                    + evades + ", hits " + hits + ").");
            }
        }

        /// <summary> E-T6 — 투사체 착탄 처리 중에는 Hitscan 관문이 굴리지 않는다(이중 판정 없음). </summary>
        private static void ValidateSmokeNoDoubleRollAtProjectileImpact()
        {
            // 투사체 착탄 처리 중 표시 두 칸 — 실제 착탄(ApplyProjectileImpactDamage)은 공격자와 함께 "발사 때 저장한 공격력"도 세운다.
            //   그래서 fixture도 두 칸을 함께 세워야 착탄 피해가 실제와 같은 값(저장한 공격력)이 된다.
            FieldInfo marker = typeof(UnitCombatUseCase).GetField("_projectileImpactAttacker", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo power = typeof(UnitCombatUseCase).GetField("_projectileImpactAttackPower", BindingFlags.NonPublic | BindingFlags.Instance);
            Require(marker != null && marker.FieldType == typeof(UnitData) && power != null && power.FieldType == typeof(int),
                "E-T6 UnitCombatUseCase._projectileImpactAttacker / _projectileImpactAttackPower (projectile impact markers) must exist.");
            using (var f = new SmokeFixture())
            {
                UnitData shooter = f.Unit(9850, TeamId.Red, 2, 0, UnitType.Pistoleer);
                UnitData victim = f.Unit(9851, TeamId.Blue, 0, 0, UnitType.Tank);
                f.Smoke(1, 1f);
                int hp = victim.Hp;
                marker.SetValue(f.Combat, shooter);
                power.SetValue(f.Combat, SupportFixtureStatsScope.AttackerAttackPower);
                try
                {
                    f.Combat.ApplyAttackDamage(shooter, victim.Id, true);
                }
                finally
                {
                    marker.SetValue(f.Combat, null);
                    power.SetValue(f.Combat, 0);
                }
                f.Evasion.NextAttackerOrdinalKey(shooter.Id, out ulong ordinal);
                Require(victim.Hp == hp - SupportFixtureStatsScope.AttackerAttackPower && ordinal == 1UL,
                    "E-T6 during a projectile impact the Hitscan gate must not roll again (damage applied, no ordinal consumed).");
                AttackDamageObservation evaded = f.Combat.ApplyAttackDamageObserved(shooter, victim.Id, true);
                Require(evaded.Status == AttackDamageApplyStatus.Evaded,
                    "E-T6 after the impact marker is cleared the same Hitscan hit must be evaded again (chance 1.0).");
            }
        }

        /// <summary> E-T12 — 연막탄 셋업 표 줄 · 소스 게이트(관문 자리 · 조합 루트 · 경기 종료 닫기 · 코디네이터 갈래). </summary>
        private static void ValidateSmokeBombSourceGates()
        {
            string setup = File.ReadAllText("Assets/Editor/Setup/SupportSkillLoadoutSetup.cs");
            Require(SupportActiveLineCount(setup, "PointPlaceholder(\"Skill_Human_SmokeBomb\"") == 0
                    && SupportActiveLineCount(setup, "PersistentZone(\"Skill_Human_SmokeBomb\", h, 3, \"연막탄\",") == 1
                    && SupportActiveLineCount(setup, "StatusEffectKind.EvadeChance, 0.5f, duration: 6f, radius: 2f, cooldown: 15f, targetsAllies: true),") == 1
                    && SupportActiveLineCount(setup, "StatusKind = (int)kind, Magnitude = magnitude, TargetsAllies = targetsAllies,") == 1,
                "E-T12 the setup table must build Smoke Bomb as an ally PersistentZone (EvadeChance 0.5 · 6 s · radius 2 · cd 15), not a placeholder.");

            const string root = "Assets/_Project/Scripts/";
            string combat = File.ReadAllText(root + "Application/UseCases/UnitCombatUseCase.cs");
            int core = combat.IndexOf("private AttackDamageObservation ApplyAttackDamageCore(", StringComparison.Ordinal);
            int gate = combat.IndexOf("TryRollHitscanEvasion(", core, StringComparison.Ordinal);
            int targetCheck = combat.IndexOf("AttackDamageApplyStatus.TargetUnavailable);", core, StringComparison.Ordinal);
            // ExecuteAttack 호출의 인자 줄로 찾는다(관문 위 주석에도 「ExecuteAttack(」 글자가 있어 이름만으로는 주석에 걸린다).
            int execute = combat.IndexOf("attacker, target, updateLegacyDomainFacing, presentationResultKey);", core, StringComparison.Ordinal);
            Require(core > 0 && targetCheck > core && gate > targetCheck && execute > gate,
                "E-T12 the Hitscan evasion gate must sit inside ApplyAttackDamageCore after the target-alive check and before ExecuteAttack (Plan P-4).");
            string evasion = File.ReadAllText(root + "Application/UseCases/UnitCombatUseCase.Evasion.cs");
            Require(evasion.Contains("if (ProjectileImpactInProgress) return false;")
                    && evasion.Contains("if (!SkillEvasionRoll.IsHitscanEvadableAttacker(attacker.Type)) return false;")
                    && evasion.Contains("if (!(target is UnitData victim)) return false;"),
                "E-T12 the Hitscan gate must skip projectile impacts, non-Hitscan attackers and building targets.");

            string setupRoot = File.ReadAllText(root + "Bootstrap/GameBootstrapper.Setup.cs");
            string boot = File.ReadAllText(root + "Bootstrap/GameBootstrapper.cs");
            Require(setupRoot.Contains("_skillEvasionSystem = new SkillEvasionSystem(_skillZoneSystem, MapRootSeed.Create());")
                    && setupRoot.Contains("_unitCombat.SetSkillEvasionSystem(_skillEvasionSystem);")
                    && boot.Contains("GameEvents.OnGameEnd.Subscribe(_ => _skillEvasionSystem?.Close()).AddTo(this);"),
                "E-T12 the composition root must build the evasion service per map with a fresh seed, inject it into combat and close it at game end.");

            string coordinator = File.ReadAllText(root + "Infrastructure/Network/UnitAttackShadowCoordinator.cs");
            Require(coordinator.Contains("&& legacy.Status == AttackDamageApplyStatus.Evaded)")
                    && coordinator.Contains("outcome = AttackImpactOutcome.Evaded;"),
                "E-T12 the C2 coordinator must close AuthorizedHit + writer Evaded as AttackImpactOutcome.Evaded.");
        }

        /// <summary> E-T12 — 연막탄 자산 계약(Z-T9가 부른다 — 옛 「사용 불가 유지」 검사 대체). </summary>
        /// <param name="loadout">이미 캐시를 비운 로드아웃 설정.</param>
        private static void ValidateSmokeBombAssetContract(SkillLoadoutConfig loadout)
        {
            SkillDefinition def = AssetDatabase.LoadAssetAtPath<SkillDefinition>(SmokeBombAssetPath);
            Require(def != null, "E-T12 Smoke Bomb asset must exist: " + SmokeBombAssetPath);
            SkillData data = def.ToSkillData();
            Require(data.IsAvailable && data.AimType == SkillAimType.PointTarget && data.Mechanic == SkillMechanicType.PersistentStatusZone
                    && (StatusEffectKind)data.StatusKind == StatusEffectKind.EvadeChance && Mathf.Approximately(data.Magnitude, 0.5f)
                    && Mathf.Approximately(data.Duration, 6f) && Mathf.Approximately(data.Radius, 2f)
                    && Mathf.Approximately(data.Cooldown, 15f) && data.TargetsAllies && data.TotalDamage == 0,
                "E-T12 Smoke Bomb must be available / point aim / type 4 / EvadeChance 0.5 / 6 s / radius 2 / cooldown 15 / allies / damage 0 (U-2 temporary values).");
            var human = loadout.GetLoadout(RaceId.Human);
            Require(human != null && human.Count > 3 && human[3] != null && human[3].DisplayName == data.DisplayName && human[3].IsAvailable,
                "E-T12 Smoke Bomb must be the available skill in the Human loadout slot index 3 (4th button).");
            // 채워진 구역 VFX 칸(테스트용 임시 프리팹 포함)은 상태 이펙트 계약을 지킨다. 비어 있으면 회피만 동작(검사할 것 없음).
            RequireSkillZoneLoopPrefab(SmokeBombAssetPath, "zone VFX", data.ZoneVfxPrefab);
            RequireSkillZoneLoopPrefab(SmokeBombAssetPath, "unit status VFX", data.StatusVfxPrefab);
        }

        // ── [E-T4] C2 결과 — Infrastructure 코디네이터 · 관찰기 · 전송 상태(아래 메서드는 Infrastructure 타입을 쓴다) ─────────

        /// <summary> E-T4 — 「승인 명중 + writer Evaded」가 C2 결과 「회피」로 닫히고 관찰기 · 수신 검사 · 화면 필터와 맞는다. </summary>
        private static void ValidateSmokeC2EvadedOutcome()
        {
            var unit = new UnitData(9860, UnitType.Pistoleer, TeamId.Red, new HexCoord(0, 0),
                maxHp: 300, attackPower: 60, attackRange: 0.5f, detectRange: 1f);
            unit.AttackCooldown = 1f;
            unit.HitFrameTimes = new[] { 0.2f }; // production resolver의 Pistoleer = Hitscan · 1타
            var target = new EntityRef(EntityKind.Unit, 9861);
            var binding = new AttackTargetBinding(AttackTargetMode.TargetLocked, target);
            Require(UnitActionPoseSample.TryCreate(target, 0d, 0d, 1d, 0d, 0.5d, 0d, false, 0d, 0d, out UnitActionPoseSample aligned),
                "E-T4 aligned pose fixture must be valid.");

            var coordinator = new UnitAttackShadowCoordinator();
            var instance = new AttackerInstanceId(9860UL);
            UnitAttackShadowCoordinator.IntentObservation commit = coordinator.PrepareLegacyAttack(
                instance, unit, binding, aligned, true, true, 40d, 0d);
            Require(commit.CommittedNow, "E-T4 the Pistoleer Hitscan cycle must commit; status=" + commit.Status + ", reason=" + commit.Reason + ".");
            LegacyAttackToken token = coordinator.ScheduleLegacyAttack(instance, binding, unit.HitFrameTimes.Length, 40d, out _);
            UnitAttackShadowCoordinator.DispatchObservation dispatch = coordinator.DispatchLegacyImpact(
                token, 0, aligned, true, true, commit.Snapshot.CommitServerTime + unit.HitFrameTimes[0]);
            Require(dispatch.EvaluateStatus == UnitActionReducerStatus.Accepted
                    && dispatch.Authorization.Outcome == ImpactAuthorizationOutcome.AuthorizedHit
                    && UnitAttackShadowCoordinator.ResolveLegacyWriterAuthorization(dispatch) == AttackDamageApplyStatus.Applied,
                "E-T4 the fixture must reach an AuthorizedHit dispatch (writer authorized to apply).");

            Require(WorldPointXZ.TryCreate(0.5d, 0d, out WorldPointXZ impactPoint), "E-T4 impact point fixture must be valid.");
            var evadedLegacy = new AttackDamageObservation(AttackDamageApplyStatus.Evaded, 0, 300,
                (int)UnitType.Tank, (int)TeamId.Blue, true, impactPoint);
            Require(evadedLegacy.IsValid, "E-T4 the Evaded writer observation must be valid.");
            UnitAttackShadowCoordinator.ResultObservation result = coordinator.CompleteLegacyImpact(
                token, 0, evadedLegacy, commit.Snapshot.CommitServerTime + unit.HitFrameTimes[0] + 0.01d);
            Require(result.CompletionStatus == UnitActionReducerStatus.Accepted && result.HasResult
                    && result.Result.Outcome == AttackImpactOutcome.Evaded && result.Result.AppliedAmount == 0
                    && result.Result.ResultingHp == 300,
                "E-T4 AuthorizedHit + writer Evaded must close as AttackImpactOutcome.Evaded (applied 0, resulting HP kept); reason="
                + result.Reason + ".");
            Require(!UnitAttackShadowObserver.ClassifyImpactResultMismatchForValidation(evadedLegacy, result.CompletionStatus, result.Result)
                    && !UnitAttackShadowObserver.ClassifyDispatchMismatchForValidation(
                        -1, 9861, 9861, 1UL, 1UL, UnitActionReducerStatus.Accepted,
                        ImpactAuthorizationOutcome.AuthorizedHit, AttackDamageApplyStatus.Evaded)
                    && UnitAttackShadowObserver.ClassifyDispatchMismatchForValidation(
                        -1, 9861, 9861, 1UL, 1UL, UnitActionReducerStatus.Accepted,
                        ImpactAuthorizationOutcome.AuthorizedMiss, AttackDamageApplyStatus.Evaded),
                "E-T4 the observer must accept AuthorizedHit + Evaded (no causal failure) and still flag AuthorizedMiss + Evaded.");
            Require(NetworkAttackImpactShadowResult.TryCreate(result.Result, AttackDeliveryKind.Hitscan, out NetworkAttackImpactShadowResult wire)
                    && wire.IsValid,
                "E-T4 the Evaded C2 result must pass the Client receive check.");
            var presentation = new AttackResultPresentationInput(
                9860, result.Result.ActionRevision, result.Result.Key, AttackDeliveryKind.Hitscan,
                result.Result.ImpactServerTime, result.Result.AuthoritativeAimDirection, result.Result.HasImpactPosition,
                result.Result.ImpactPosition, result.Result.Outcome, result.Result.AppliedAmount, result.Result.ResultingHp);
            Require(presentation.IsValid && !presentation.IsVisualResult,
                "E-T4 an Evaded result must be a valid presentation input that is not visual (no HP number / hit reaction).");

            // 승인 Miss인데 writer가 회피라고 하면 서로 다른 사실 — 위조하지 않고 거절로 닫는다.
            var missCoordinator = new UnitAttackShadowCoordinator();
            var missInstance = new AttackerInstanceId(9862UL);
            UnitAttackShadowCoordinator.IntentObservation missCommit = missCoordinator.PrepareLegacyAttack(
                missInstance, unit, binding, aligned, true, true, 50d, 0d);
            LegacyAttackToken missToken = missCoordinator.ScheduleLegacyAttack(missInstance, binding, 1, 50d, out _);
            UnitAttackShadowCoordinator.DispatchObservation missDispatch = missCoordinator.DispatchLegacyImpact(
                missToken, 0, aligned, false, false, missCommit.Snapshot.CommitServerTime + unit.HitFrameTimes[0]);
            Require(missDispatch.Authorization.Outcome == ImpactAuthorizationOutcome.AuthorizedMiss,
                "E-T4 the miss fixture must reach an AuthorizedMiss dispatch.");
            UnitAttackShadowCoordinator.ResultObservation mismatch = missCoordinator.CompleteLegacyImpact(
                missToken, 0, evadedLegacy, missCommit.Snapshot.CommitServerTime + unit.HitFrameTimes[0] + 0.01d);
            Require(mismatch.CompletionStatus == UnitActionReducerStatus.InvalidInput && !mismatch.HasResult
                    && mismatch.Reason == "authorization-result-outcome-mismatch",
                "E-T4 AuthorizedMiss + writer Evaded must be rejected as an outcome mismatch, not forged into Evaded.");
        }
    }
}
