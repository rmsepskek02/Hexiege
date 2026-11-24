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
//   [2026-11-23 단계 S-4 — 즉발 탑] E-T9 정령 RuneSpire · 초월 VineTower 회피(확률 1.0) — 피해 writer · 공격받음 / 피해 이벤트 · 사망 처리 없음,
//         빗나감 스냅샷 1건(Evaded · 적용 0 · Impact = 빗나간 지점) · 탑 열쇠(탑 Id + 탑별 순번) 0.5 재계산 · 판정 제외(연막 밖 · 적 연막 ·
//         인간 탑 즉발 대역) · 회피해도 쿨다운 시작 · 스냅샷 계약, E-T12 관문 자리 · 조합 루트 · RPC evaded 칸 · 화면 비추적 · 분류 토큰.
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
            // [2026-11-23 단계 S-3 — 서버 권위 투사체 회피] 계약 · 발사 순간 판정 · 착탄 writer 미호출 · 화상 차단 · 제외 · 이중 판정 없음 ·
            //   빗나간 지점 결정성 · 스키마 12 · 분류 토큰 · RPC 소스 게이트. HP를 보는 검사는 빈 표 + 고정 행 안에서 돈다.
            ValidateSmokeProjectileContracts();        // E-T11
            ValidateSmokeMissPointDeterminism();       // E-T10(빗나간 지점 · 좌우 · 다른 seed)
            using (new SupportFixtureStatsScope())
            {
                PinSmokeProjectileStats();
                ValidateSmokeProjectileEvasion();      // E-T5
                ValidateSmokeProjectileRollOncePerLaunch(); // E-T6(투사체 — 판정 수 = 발사 수, 0.5 재계산)
                ValidateSmokeInfernoBurnBlocked();     // E-T7
                ValidateSmokeProjectileExclusions();   // E-T8
                ValidateSmokeTowerShellEvasion();      // E-T5(대포탑 포탄 — TowerShell)
            }
            ValidateSmokeProjectileSourceGates();      // E-T12(스키마 12 · 분류 토큰 · RPC · 화면 소스 게이트)
            // [2026-11-23 단계 S-4 — 즉발 탑(RuneSpire · VineTower) 회피] 판정 · writer 미호출 · 사망 · 이벤트 차단 · 빗나감 스냅샷 ·
            //   탑 열쇠 결정성 · 제외 · 쿨다운 · 스냅샷 계약 · RPC 소스 게이트 · 토큰. HP를 보는 검사는 빈 표 + 고정 행 안에서 돈다.
            ValidateSmokeTowerInstantContracts();      // E-T9(스냅샷 계약) · E-T10(탑 열쇠 분리)
            using (new SupportFixtureStatsScope())
            {
                PinSmokeTowerInstantStats();
                ValidateSmokeTowerInstantEvasion();    // E-T9(정령 번개 · 초월 덩굴 — 피해 · 이벤트 · 사망 없음, 빗나감 스냅샷)
                ValidateSmokeTowerInstantKeys();       // E-T9 · E-T10(열쇠 = 탑 Id + 순번, 0.5 재계산, 탑마다 따로)
                ValidateSmokeTowerInstantExclusions(); // E-T9(연막 밖 · 적 연막 · 인간 탑 즉발 대역 → 판정 없음) + 쿨다운
            }
            ValidateSmokeTowerInstantSourceGates();    // E-T12(S-4 — 관문 자리 · 조합 루트 · RPC evaded · 화면 비추적 · 토큰)
            Debug.Log("[UAS-DIAG][SKILL-EVADE] S-4 self-validation PASS: evaded tower snapshot must be a Tower visual with applied 0 and tower / unit ordinal keys never collide (E-T9 · E-T10), Spirit RuneSpire and Transcendence VineTower attacks at a chance-1.0 smoke unit skip the damage writer, attacked / damaged events and the death workflow while publishing one Evaded snapshot whose Impact is ComputeMissPoint(tower, target, radius, roll) (E-T9), tower keys = (tower id, per-tower roll ordinal) recompute exactly at 0.5 with HP loss = power × hits and independent per tower (E-T9 · E-T10), outside-smoke / enemy-smoke / human tower on the instant fallback never roll and an evaded attack still starts the cooldown (E-T9), source gates for the gate position, composition root, AttackEffectClientRpc evaded argument, the untracked Evaded bolt and the tower-instant handshake token (E-T12).");
            Debug.Log("[UAS-DIAG][SKILL-EVADE] S-3 self-validation PASS: launch / impact contracts accept Evaded (applied 0) and reject NaN miss points and unknown outcomes (E-T11), miss point = M = C + n(r+0.3) + d·0.5 from the same seed+projectile key on both sides (E-T10), projectile evasion at launch with the writer never invoked, HP and damage events unchanged, outcome Evaded at the fixed miss point and flight = miss distance / speed (E-T5), one roll per launch with 0.5 recomputation (E-T6), InfernoSpirit burn blocked only when evaded (E-T7), MushroomBomber and building targets never rolled even when a smoke unit shares the building id (E-T8), human cannon tower shell evaded with the tower writer not invoked (TowerShell), schema 13 + smoke-evasion handshake token + launch RPC evaded / missPoint source gates (E-T12).");
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

        // ====================================================================
        // [2026-11-23 단계 S-3 — 서버 권위 투사체 회피] E-T5 · E-T6 · E-T7 · E-T8 · E-T10 · E-T11 · E-T12
        //   [초급자용] 날아가는 투사체는 "쏘는 순간" 서버가 피할지 정한다. 피했으면 발사 기록에 「회피 + 빗나간 지점」이 실리고,
        //   착탄 때 피해 writer를 부르지 않는다(HP · 피해 이벤트 · 화상 없음). 아래 검사는 실제 전투 UseCase · 구역 · 회피 서비스로
        //   예약 → 발사 → 비행 → 착탄을 돌려 그 약속을 확인한다. 확률은 1.0(항상 회피) · 0(연막 밖)으로 결정적으로 보고,
        //   0.5는 고정 seed + 발사 번호표(epoch · id)로 기대값을 다시 계산해 맞춘다.
        // ====================================================================

        // 투사체 검사용 고정 행(빈 표에서 시작 — SupportFixtureStatsScope가 끝나면 원래 표 참조로 되돌린다).
        private const int SmokeTankAttack = 50;
        private const int SmokeInfernoAttack = 25;
        private const int SmokeMushroomAttack = 20;
        private const int SmokeVictimHp = 10000;
        private const int SmokeCastleHp = 2000;
        private const int SmokeTowerShellPower = 30;

        /// <summary>
        /// 투사체 검사가 쓰는 (종류, 종족) 행만 빈 표에 고정한다. 바깥 SupportFixtureStatsScope의 Dispose가 원래 표 참조를 되돌린다
        /// (이 메서드는 그 범위 안에서만 부른다 — 「빈 표 시작 + 필요한 행만 + 끝나면 복원」 관례).
        /// </summary>
        private static void PinSmokeProjectileStats()
        {
            var units = new Dictionary<UnitType, UnitStats.StatValues>();
            units[UnitType.Tank] = new UnitStats.StatValues { MaxHp = SmokeVictimHp, AttackPower = SmokeTankAttack, AttackRange = 3f,
                DetectRange = 3f, MoveSpeed = .5f, AttackCooldown = 2f, HitFrameTimes = new[] { .4f }, Defense = 0 };
            units[UnitType.InfernoSpirit] = new UnitStats.StatValues { MaxHp = 300, AttackPower = SmokeInfernoAttack, AttackRange = 3f,
                DetectRange = 3f, MoveSpeed = .5f, AttackCooldown = 3f, HitFrameTimes = new[] { 1.5f }, Defense = 0 };
            units[UnitType.MushroomBomber] = new UnitStats.StatValues { MaxHp = 300, AttackPower = SmokeMushroomAttack, AttackRange = 3f,
                DetectRange = 3f, MoveSpeed = .5f, AttackCooldown = 2f, HitFrameTimes = new[] { 1f }, Defense = 0 };
            UnitStats.Initialize(units);
            var buildings = new Dictionary<(BuildingType, RaceId), BuildingStats.StatValues>();
            buildings[(BuildingType.Castle, RaceId.Transcendence)] = new BuildingStats.StatValues { MaxHp = SmokeCastleHp };
            buildings[(BuildingType.AutoTower, RaceId.Human)] = new BuildingStats.StatValues
                { MaxHp = 1000, AttackPower = SmokeTowerShellPower, AttackCooldown = 1f, AttackRange = 3f };
            BuildingStats.Initialize(buildings);
            Require(UnitStats.GetMaxHp(UnitType.Tank) == SmokeVictimHp && UnitStats.GetAttackPower(UnitType.Tank) == SmokeTankAttack
                    && UnitStats.GetAttackPower(UnitType.InfernoSpirit) == SmokeInfernoAttack
                    && UnitStats.GetAttackPower(UnitType.MushroomBomber) == SmokeMushroomAttack
                    && UnitStats.GetDefense(UnitType.Tank) == 0
                    && BuildingStats.GetMaxHp(BuildingType.Castle, RaceId.Transcendence) == SmokeCastleHp
                    && BuildingStats.GetMaxHp(BuildingType.AutoTower, RaceId.Human) == 1000,
                "E-T0 smoke projectile fixture stats must be pinned (Tank HP " + SmokeVictimHp + " / attack " + SmokeTankAttack
                + ", Inferno attack " + SmokeInfernoAttack + ", Mushroom attack " + SmokeMushroomAttack + ", Castle/Trans HP " + SmokeCastleHp + ").");
        }

        // 대포탑 착탄 writer 대역 — 몇 번 불렸는지만 센다(회피면 0번이어야 한다).
        private sealed class SmokeTowerWriterProbe : IBuildingProjectileImpactWriter
        {
            public int Calls;
            public AttackDamageObservation ApplyTowerProjectileImpact(BuildingData tower, UnitData target,
                int attackPowerAtLaunch, ProjectileDeliveryKey key)
            {
                Calls++;
                return AttackDamageObservation.Unavailable(AttackDamageApplyStatus.AttackerUnavailable);
            }
        }

        // 투사체 연막 fixture — 전투 UseCase(투사체 수명 포함) · 구역 · 회피 서비스 · 발사 / 착탄 / 피해 이벤트 수집 · 서버 시계.
        private sealed class SmokeProjectileFixture : IDisposable
        {
            public readonly UnitSpawnUseCase Spawn;
            public readonly BuildingPlacementUseCase Buildings;
            public readonly UnitCombatUseCase Combat;
            public readonly SkillZoneSystem Zones;
            public readonly SkillEvasionSystem Evasion;
            public readonly ProjectileSettings Settings;
            public readonly List<ProjectileLaunchPresentation> Launches = new List<ProjectileLaunchPresentation>();
            public readonly List<ProjectileImpactPresentation> Impacts = new List<ProjectileImpactPresentation>();
            public int DamagedEvents;
            public double Now;
            private readonly IDisposable _damagedSub;

            public SmokeProjectileFixture(ulong seed = SmokeFixtureSeed)
            {
                var grid = new HexGrid(3, 3, HexOrientation.FlatTop);
                Spawn = new UnitSpawnUseCase(grid);
                Buildings = new BuildingPlacementUseCase(grid);
                var mapper = new ProjectileFixtureMapper(); // 타일 (q, r) → 월드 (q, 0, r)
                Combat = new UnitCombatUseCase(grid, Spawn, Buildings, new ProjectileFixturePositions(), mapper);
                Settings = new ProjectileSettings(humanTowerSpeed: 4f);
                Settings.SetUnitSpeed(UnitType.Tank, 4f);
                Settings.SetUnitSpeed(UnitType.InfernoSpirit, 5f);
                Settings.SetUnitSpeed(UnitType.MushroomBomber, 5f);
                Settings.SetUnitRadius(UnitType.Tank, .3f);
                Combat.SetProjectileSettings(Settings);
                Combat.SetWaveClock(() => Now);
                Zones = new SkillZoneSystem(Spawn, mapper, Buildings);
                Combat.SetSkillZoneSystem(Zones);
                Evasion = new SkillEvasionSystem(Zones, seed);
                Combat.SetSkillEvasionSystem(Evasion);
                Combat.ProjectileLaunched += Launches.Add;
                Combat.ProjectileResolved += Impacts.Add;
                _damagedSub = GameEvents.OnEntityDamaged.Subscribe(_ => { DamagedEvents++; });
            }

            public UnitData Unit(int id, TeamId team, int q, int r, UnitType type)
            {
                UnitData unit = Spawn.SpawnUnitWithId(id, type, team, new HexCoord(0, 0));
                Require(unit != null, "E-T0 smoke projectile fixture unit must spawn: " + id);
                unit.Position = new HexCoord(q, r);
                return unit;
            }

            // 시전 팀 = Blue(아군 구역), 슬롯 3, 중심 (0, 0, 0), 반경 2, 6초.
            public int Smoke(int sequence, float chance)
                => Zones.CreateZone(sequence, TeamId.Blue, 3, 77, Vector3.zero, 2f, 6f, StatusEffectKind.EvadeChance, chance, true);

            public void Tick(double now) { Now = now; Combat.TickProjectiles(now); }

            /// <summary> 예약 1발 → due에 발사 → 기록의 착탄 시각에 착탄. 발사 기록을 돌려준다. </summary>
            public ProjectileLaunchPresentation FireAndLand(UnitData attacker, int targetId, bool targetIsUnit, double due)
            {
                int launchesBefore = Launches.Count, impactsBefore = Impacts.Count;
                Require(Combat.TryReserveProjectileLaunch(attacker, targetId, targetIsUnit, due, 0),
                    "E-T0 the projectile reservation must be accepted: attacker " + attacker.Id + " → " + targetId);
                Tick(due);
                Require(Launches.Count == launchesBefore + 1, "E-T0 the reservation must launch at its due time.");
                ProjectileLaunchPresentation launch = Launches[Launches.Count - 1];
                Tick(launch.ImpactTime);
                Require(Impacts.Count == impactsBefore + 1, "E-T0 the projectile must resolve at its authoritative impact time.");
                return launch;
            }

            public void Dispose()
            {
                _damagedSub?.Dispose();
                Combat.CloseProjectiles("fixture-dispose");
            }
        }

        // 발사 기록 하나의 회피 여부 · 빗나간 지점을 서버와 같은 식으로 다시 계산해 맞춰 본다(Client가 받는 값이 서버 계산과 같은가).
        private static void RequireSmokeLaunchMatchesRecomputation(ProjectileLaunchPresentation launch, ulong seed, int chanceBp, string label)
        {
            ulong roll = SkillEvasionRoll.ComputeRoll(seed, SkillEvasionRoll.KeyFromProjectile(launch.Key.Epoch, launch.Key.Id));
            bool expected = SkillEvasionRoll.IsEvaded(roll, chanceBp);
            Require(launch.Evaded == expected, label + " launch evasion must equal the offline recomputation from seed + projectile key (epoch:id).");
            if (!expected) return;
            Vector3 miss = SkillEvasionRoll.ComputeMissPoint(launch.Source, launch.TargetCenter, launch.TargetRadius, roll);
            Require((launch.MissPoint - miss).sqrMagnitude < 1e-10f,
                label + " the miss point carried by the launch record must equal ComputeMissPoint(source, centre, radius, roll): " + launch.MissPoint + " vs " + miss);
        }

        /// <summary> E-T11 — 발사 기록 · 착탄 결과 계약(회피 통과 · 피해 0 요구 · 미정의 값 · NaN 빗나간 지점 거부). </summary>
        private static void ValidateSmokeProjectileContracts()
        {
            var key = new ProjectileDeliveryKey(5, 1);
            Require((int)ProjectileImpactOutcome.Evaded == 3 && (int)ProjectileImpactOutcome.TargetLost == 2 && (int)ProjectileImpactOutcome.Hit == 1,
                "E-T11 ProjectileImpactOutcome.Evaded must be appended as 3 (Hit 1 · TargetLost 2 unchanged — network byte values).");
            Require(new ProjectileImpactPresentation(key, ProjectileImpactOutcome.Evaded, Vector3.one, 2, true, -1, 0, 1.5d).IsValid
                    && !new ProjectileImpactPresentation(key, ProjectileImpactOutcome.Evaded, Vector3.one, 2, true, -1, 3, 1.5d).IsValid
                    && !new ProjectileImpactPresentation(key, (ProjectileImpactOutcome)4, Vector3.one, 2, true, -1, 0, 1.5d).IsValid
                    && !new ProjectileImpactPresentation(key, ProjectileImpactOutcome.Evaded, new Vector3(float.NaN, 0f, 0f), 2, true, -1, 0, 1.5d).IsValid,
                "E-T11 an Evaded impact must pass with applied 0, and must be rejected with applied > 0, outcome 4 or a NaN point.");
            Require(new ProjectileImpactPresentation(key, ProjectileImpactOutcome.Hit, Vector3.zero, 2, true, 5, 3, 1.5d).IsValid
                    && new ProjectileImpactPresentation(key, ProjectileImpactOutcome.TargetLost, Vector3.zero, 2, true, -1, 0, 1.5d).IsValid,
                "E-T11 Hit / TargetLost results must stay valid (regression).");
            var hit = new ProjectileLaunchPresentation(key, 1, true, (int)UnitType.Tank, 0, TeamId.Red, 2, true,
                Vector3.zero, Vector3.right, .3f, 1d, 1.5d, true);
            var evaded = new ProjectileLaunchPresentation(key, 1, true, (int)UnitType.Tank, 0, TeamId.Red, 2, true,
                Vector3.zero, Vector3.right, .3f, 1d, 1.5d, true, evaded: true, missPoint: new Vector3(1.5f, 0f, .6f));
            var evadedNaN = new ProjectileLaunchPresentation(key, 1, true, (int)UnitType.Tank, 0, TeamId.Red, 2, true,
                Vector3.zero, Vector3.right, .3f, 1d, 1.5d, true, evaded: true, missPoint: new Vector3(float.NaN, 0f, 0f));
            var hitWithJunk = new ProjectileLaunchPresentation(key, 1, true, (int)UnitType.Tank, 0, TeamId.Red, 2, true,
                Vector3.zero, Vector3.right, .3f, 1d, 1.5d, true, evaded: false, missPoint: new Vector3(float.NaN, 0f, 0f));
            Require(hit.IsValid && !hit.Evaded && hit.MissPoint == Vector3.zero,
                "E-T11 a launch built without the new arguments must be a non-evaded record with a zero miss point (old call sites unchanged).");
            Require(evaded.IsValid && evaded.Evaded && evaded.MissPoint == new Vector3(1.5f, 0f, .6f),
                "E-T11 an evaded launch with a finite miss point must be valid and keep the point.");
            Require(!evadedNaN.IsValid, "E-T11 an evaded launch with a NaN miss point must be rejected (receiver drops it as invalid).");
            Require(hitWithJunk.IsValid && hitWithJunk.MissPoint == Vector3.zero,
                "E-T11 a non-evaded launch must ignore (zero) the miss point argument so junk never reaches the screen.");
        }

        /// <summary> E-T10 — 빗나간 지점 결정성(같은 seed · 열쇠 → 같은 점) · 식 · 좌우가 둘 다 나온다 · seed가 다르면 결과가 달라진다. </summary>
        private static void ValidateSmokeMissPointDeterminism()
        {
            Vector3 source = new Vector3(3f, 0f, 0f), center = Vector3.zero;
            const float radius = .3f;
            int left = 0, right = 0, differentAcrossSeed = 0;
            for (ulong id = 1; id <= 64; id++)
            {
                ulong key = SkillEvasionRoll.KeyFromProjectile(0xABCDEF01UL, id);
                ulong roll = SkillEvasionRoll.ComputeRoll(SmokeFixtureSeed, key);
                Vector3 a = SkillEvasionRoll.ComputeMissPoint(source, center, radius, roll);
                Vector3 b = SkillEvasionRoll.ComputeMissPoint(source, center, radius, SkillEvasionRoll.ComputeRoll(SmokeFixtureSeed, key));
                Require(a == b, "E-T10 the same seed and projectile key must give the same miss point (both screens and the server agree).");
                // 쏜 쪽(+X) → 대상(원점): 진행 방향 d = (−1, 0). 앞 거리 = 0.5, 옆 거리 = 반경 + 0.3, 높이 = 대상 중심 높이.
                Require(Mathf.Abs(a.x - (-SkillEvasionRoll.MissForwardExtra)) < 1e-5f
                        && Mathf.Abs(Mathf.Abs(a.z) - (radius + SkillEvasionRoll.MissSideExtra)) < 1e-5f && a.y == 0f,
                    "E-T10 miss point must sit 0.5 beyond the target along the shot and radius + 0.3 to the side: " + a);
                if (SkillEvasionRoll.MissSideIsLeft(roll)) left++; else right++;
                if (SkillEvasionRoll.ComputeRoll(SmokeFixtureSeed ^ 0x1UL, key) != roll) differentAcrossSeed++;
            }
            Require(left > 0 && right > 0, "E-T10 over 64 projectile keys both miss sides must occur (left " + left + ", right " + right + ").");
            Require(differentAcrossSeed == 64, "E-T10 a different match seed must change the roll of every projectile key.");
            Require(Mathf.Abs(SkillEvasionRoll.MissTravelDistance(new Vector3(0f, 5f, 0f), new Vector3(3f, 0f, 4f)) - 5f) < 1e-5f,
                "E-T10 the miss travel distance must be the XZ distance (height ignored).");
        }

        /// <summary> E-T5 — 투사체 회피(확률 1.0): 발사 순간 판정 · 빗나간 지점 · 비행 시간 · 착탄 writer 미호출 · 결과 Evaded. </summary>
        private static void ValidateSmokeProjectileEvasion()
        {
            using (var f = new SmokeProjectileFixture())
            {
                UnitData shooter = f.Unit(9870, TeamId.Red, 3, 0, UnitType.Tank);
                UnitData victim = f.Unit(9871, TeamId.Blue, 0, 0, UnitType.Tank);

                // 회귀(연막 없음) — 판정 없이 대상 표면까지 날아가 착탄 때 피해 1회.
                int hp0 = victim.Hp;
                ProjectileLaunchPresentation plain = f.FireAndLand(shooter, victim.Id, true, 1d);
                Require(!plain.Evaded && plain.MissPoint == Vector3.zero
                        && Math.Abs(plain.ImpactTime - (1d + (3d - .3d) / 4d)) < 1e-5,
                    "E-T5 without a smoke the launch must be a normal homing record (impact = launch + (distance - radius) / speed).");
                Require(f.Impacts[0].Outcome == ProjectileImpactOutcome.Hit && victim.Hp == hp0 - SmokeTankAttack && f.DamagedEvents == 1,
                    "E-T5 without a smoke the impact must hit once through the writer (unchanged path).");

                // 확률 1.0 연막 — 발사 순간 회피, 빗나간 지점으로 비행, 착탄 때 writer 미호출.
                f.Smoke(1, 1f);
                int hp1 = victim.Hp, damaged1 = f.DamagedEvents;
                Require(f.Combat.TryReserveProjectileLaunch(shooter, victim.Id, true, 3d, 0), "E-T5 smoke reservation must be accepted.");
                f.Tick(3d);
                ProjectileLaunchPresentation launch = f.Launches[f.Launches.Count - 1];
                Require(launch.IsValid && launch.Evaded, "E-T5 a Tank shell at a unit inside a chance-1.0 smoke must be evaded at launch.");
                RequireSmokeLaunchMatchesRecomputation(launch, SmokeFixtureSeed, 10000, "E-T5");
                double expectedFlight = SkillEvasionRoll.MissTravelDistance(launch.Source, launch.MissPoint) / 4d;
                Require(Math.Abs(launch.ImpactTime - (3d + expectedFlight)) < 1e-5,
                    "E-T5 the evaded flight time must be (launch → miss point XZ distance) / speed: " + (launch.ImpactTime - 3d) + " vs " + expectedFlight);
                Vector3 surface = UnitCombatUseCase.ProjectileSurfacePoint(launch.Source, launch.TargetCenter, launch.TargetRadius);
                Require((launch.MissPoint - surface).sqrMagnitude > .01f, "E-T5 the miss point must differ from the target surface point.");
                // 비행 중 대상이 움직여도 착탄 지점은 빗나간 지점 그대로다(대상 추적 안 함).
                victim.Position = new HexCoord(0, 1);
                f.Tick(launch.ImpactTime - .001d);
                Require(f.Impacts.Count == 1 && victim.Hp == hp1, "E-T5 nothing resolves before the authoritative evaded impact time.");
                f.Tick(launch.ImpactTime);
                ProjectileImpactPresentation impact = f.Impacts[f.Impacts.Count - 1];
                Require(f.Impacts.Count == 2 && impact.Outcome == ProjectileImpactOutcome.Evaded && impact.IsValid
                        && impact.Key.Equals(launch.Key) && impact.AppliedAmount == 0 && impact.ResultingHp == -1
                        && (impact.ImpactPoint - launch.MissPoint).sqrMagnitude < 1e-12f,
                    "E-T5 the evaded impact must resolve as Evaded at the launch miss point with applied 0.");
                Require(victim.Hp == hp1 && f.DamagedEvents == damaged1,
                    "E-T5 an evaded shell must not invoke the writer — HP unchanged and no damage event (so no damage number).");
                f.Tick(launch.ImpactTime + 1d);
                Require(f.Impacts.Count == 2, "E-T5 repeated ticks must never resolve the evaded shell twice.");

                // O-6 — 비행 중 대상이 죽어도 결과는 Evaded(대상 소실로 바꾸지 않는다).
                victim.Position = new HexCoord(0, 0);
                Require(f.Combat.TryReserveProjectileLaunch(shooter, victim.Id, true, 10d, 0), "E-T5 O-6 reservation must be accepted.");
                f.Tick(10d);
                ProjectileLaunchPresentation doomed = f.Launches[f.Launches.Count - 1];
                Require(doomed.Evaded, "E-T5 O-6 the shell must be evaded at launch.");
                victim.TakeDamage(victim.Hp);   // 다른 원인으로 사망(fixture)
                f.Tick(doomed.ImpactTime);
                Require(f.Impacts[f.Impacts.Count - 1].Outcome == ProjectileImpactOutcome.Evaded,
                    "E-T5 O-6 an evaded shell whose target died in flight must still resolve as Evaded (decision O-6).");

                string end = f.Combat.BuildProjectileEndLine("fixture");
                Require(end.StartsWith("side=server v=3 ") && end.Contains(" hit=1 ") && end.Contains(" targetLost=0 evaded=2 ")
                        && end.Contains(" balance=ok ") && end.EndsWith(" verdict=PASS"),
                    "E-T5 the server END line (v=3) must count the evaded shells and stay balanced: " + end);
            }
        }

        /// <summary> E-T6 — 투사체 판정은 발사마다 정확히 1번(착탄 때 다시 굴리지 않는다) · 확률 0.5 재계산 · HP = 공격력 × 명중 수. </summary>
        private static void ValidateSmokeProjectileRollOncePerLaunch()
        {
            using (var f = new SmokeProjectileFixture())
            {
                UnitData shooter = f.Unit(9880, TeamId.Red, 3, 0, UnitType.Tank);
                UnitData victim = f.Unit(9881, TeamId.Blue, 0, 0, UnitType.Tank);
                f.Smoke(1, .5f);
                int start = victim.Hp, hits = 0, evades = 0;
                const int shots = 32;
                for (int i = 0; i < shots; i++)
                {
                    int hp = victim.Hp, damaged = f.DamagedEvents;
                    ProjectileLaunchPresentation launch = f.FireAndLand(shooter, victim.Id, true, 1d + i * 2d);
                    RequireSmokeLaunchMatchesRecomputation(launch, SmokeFixtureSeed, 5000, "E-T6 shot " + (i + 1));
                    ProjectileImpactOutcome outcome = f.Impacts[f.Impacts.Count - 1].Outcome;
                    if (launch.Evaded)
                    {
                        evades++;
                        Require(outcome == ProjectileImpactOutcome.Evaded && victim.Hp == hp && f.DamagedEvents == damaged,
                            "E-T6 shot " + (i + 1) + " evaded at launch must resolve Evaded with no damage.");
                    }
                    else
                    {
                        hits++;
                        // 발사 때 「명중」으로 정해진 투사체는 착탄 때 writer를 한 번 지나 그대로 맞는다 — 착탄 중 다시 굴려 피하는 일이 없다.
                        Require(outcome == ProjectileImpactOutcome.Hit && victim.Hp == hp - SmokeTankAttack && f.DamagedEvents == damaged + 1,
                            "E-T6 shot " + (i + 1) + " not evaded at launch must hit at impact (no second roll).");
                    }
                }
                Require(evades > 0 && hits > 0 && start - victim.Hp == hits * SmokeTankAttack,
                    "E-T6 with chance 0.5 some shells must evade and some hit; HP loss = attack × hits (evades " + evades + ", hits " + hits + ").");
                // 판정 수 = 발사 수(개발 빌드 진단 계수 — Editor 스위트는 UNITY_EDITOR라 항상 센다).
                FieldInfo rolls = typeof(SkillEvasionSystem).GetField("_diagRolls", BindingFlags.NonPublic | BindingFlags.Instance);
                Require(rolls != null && rolls.GetValue(f.Evasion) is int[], "E-T6 SkillEvasionSystem._diagRolls (per-path roll counter) must exist.");
                int[] counts = (int[])rolls.GetValue(f.Evasion);
                Require(counts[(int)SkillEvasionPath.Projectile] == shots && counts[(int)SkillEvasionPath.Hitscan] == 0,
                    "E-T6 projectile rolls must equal launches (" + shots + ") with no Hitscan roll at impact (got projectile "
                    + counts[(int)SkillEvasionPath.Projectile] + ", hitscan " + counts[(int)SkillEvasionPath.Hitscan] + ").");
            }
        }

        /// <summary> E-T7 — InfernoSpirit 회피면 직격 · 화상 둘 다 없다. 연막 밖이면 화상이 그대로(유닛 규칙 41 회귀). </summary>
        private static void ValidateSmokeInfernoBurnBlocked()
        {
            using (var f = new SmokeProjectileFixture())
            {
                UnitData inferno = f.Unit(9890, TeamId.Red, 3, 0, UnitType.InfernoSpirit);
                UnitData inside = f.Unit(9891, TeamId.Blue, 0, 0, UnitType.Tank);
                UnitData outside = f.Unit(9892, TeamId.Blue, 10, 10, UnitType.Tank);
                f.Smoke(1, 1f);

                // 연막 밖 — 직격 + 화상(회귀).
                int hpOut = outside.Hp;
                ProjectileLaunchPresentation hit = f.FireAndLand(inferno, outside.Id, true, 1d);
                Require(!hit.Evaded && f.Impacts[f.Impacts.Count - 1].Outcome == ProjectileImpactOutcome.Hit
                        && outside.Hp == hpOut - SmokeInfernoAttack,
                    "E-T7 an Inferno fireball at a unit outside the smoke must hit directly (regression).");
                int afterImpact = outside.Hp;
                f.Combat.TickTimedEffects(1f);
                Require(outside.Hp < afterImpact, "E-T7 the Inferno burn must start on a normal hit (unit rule 41 regression).");

                // 연막 안(확률 1.0) — 발사 순간 회피 → 착탄 writer 미호출 → 직격도 화상도 없다.
                int hpIn = inside.Hp, damaged = f.DamagedEvents;
                ProjectileLaunchPresentation evaded = f.FireAndLand(inferno, inside.Id, true, 5d);
                Require(evaded.Evaded && f.Impacts[f.Impacts.Count - 1].Outcome == ProjectileImpactOutcome.Evaded,
                    "E-T7 an Inferno fireball at a unit inside a chance-1.0 smoke must be evaded at launch.");
                int damagedAfterImpact = f.DamagedEvents;
                f.Combat.TickTimedEffects(1f);
                f.Combat.TickTimedEffects(1f);
                Require(inside.Hp == hpIn, "E-T7 an evaded Inferno fireball must leave no burn (HP unchanged after two DoT ticks).");
                // 피해 이벤트는 연막 밖 유닛의 화상 틱만 늘 수 있다 — 연막 안 유닛 몫은 0이어야 한다(HP 그대로가 그 증거).
                Require(damagedAfterImpact == damaged, "E-T7 the evaded fireball impact itself must raise no damage event.");
            }
        }

        /// <summary> E-T8 — MushroomBomber는 판정하지 않는다 · 건물 대상은 판정하지 않는다(같은 Id의 연막 속 유닛이 있어도). </summary>
        private static void ValidateSmokeProjectileExclusions()
        {
            using (var f = new SmokeProjectileFixture())
            {
                UnitData mushroom = f.Unit(9900, TeamId.Red, 3, 0, UnitType.MushroomBomber);
                UnitData tank = f.Unit(9901, TeamId.Red, 3, 1, UnitType.Tank);
                UnitData victim = f.Unit(9902, TeamId.Blue, 0, 0, UnitType.Tank);
                // 건물 Id와 같은 Id의 아군 유닛(아래 건물 대상 검사용). 구역 구성원은 생성 순간 · 권위 틱에 세므로 연막보다 먼저 만든다.
                UnitData twin = f.Unit(9910, TeamId.Blue, 0, 1, UnitType.Tank);
                f.Smoke(1, 1f);
                Require(f.Zones.GetEvadeChance(victim.Id) >= 1f, "E-T8 fixture victim must be inside the chance-1.0 smoke.");

                int hp = victim.Hp;
                ProjectileLaunchPresentation spore = f.FireAndLand(mushroom, victim.Id, true, 1d);
                Require(!spore.Evaded && f.Impacts[f.Impacts.Count - 1].Outcome == ProjectileImpactOutcome.Hit && victim.Hp < hp,
                    "E-T8 a MushroomBomber spore (area burst, not single-target) must never be evaded — it hits the smoke unit.");

                // 건물 대상 — 건물 Id를 연막 속 유닛 Id와 같게 만든다. Id만 보고 확률을 물으면 잘못 회피한다(그러면 안 된다).
                Require(f.Zones.GetEvadeChance(twin.Id) >= 1f, "E-T8 fixture twin unit (same id as the building) must be inside the smoke.");
                BuildingData castle = f.Buildings.PlaceBuildingWithId(9910, BuildingType.Castle, TeamId.Blue, new HexCoord(1, 1), RaceId.Transcendence);
                Require(castle != null && castle.IsAlive && castle.Hp == SmokeCastleHp, "E-T8 fixture castle must be placed with pinned HP.");
                int castleHp = castle.Hp, twinHp = twin.Hp;
                ProjectileLaunchPresentation atBuilding = f.FireAndLand(tank, castle.Id, false, 5d);
                Require(!atBuilding.Evaded && f.Impacts[f.Impacts.Count - 1].Outcome == ProjectileImpactOutcome.Hit
                        && castle.Hp < castleHp && twin.Hp == twinHp,
                    "E-T8 a shell at a building must never be rolled even when a smoke unit shares the building id (smoke protects units only).");

                FieldInfo rolls = typeof(SkillEvasionSystem).GetField("_diagRolls", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo skipped = typeof(SkillEvasionSystem).GetField("_diagMushroomInSmokeSkipped", BindingFlags.NonPublic | BindingFlags.Instance);
                Require(rolls != null && skipped != null, "E-T8 evasion diagnostic counters must exist.");
                int[] counts = (int[])rolls.GetValue(f.Evasion);
                Require(counts[(int)SkillEvasionPath.Projectile] == 0 && (int)skipped.GetValue(f.Evasion) == 1,
                    "E-T8 neither the Mushroom spore nor the building shell may roll; the Mushroom skip must be counted once (E-L11).");
            }
        }

        /// <summary> E-T5(대포탑) — 인간 대포탑 포탄 회피(TowerShell): 탑 writer 미호출 · 결과 Evaded · 포구 기준 빗나간 지점. </summary>
        private static void ValidateSmokeTowerShellEvasion()
        {
            using (var f = new SmokeProjectileFixture())
            {
                BuildingData tower = f.Buildings.PlaceBuildingWithId(9920, BuildingType.AutoTower, TeamId.Red, new HexCoord(2, 0), RaceId.Human);
                Require(tower != null && tower.IsAlive, "E-T5 fixture human cannon tower must be placed.");
                UnitData inside = f.Unit(9921, TeamId.Blue, 0, 0, UnitType.Tank);
                UnitData outside = f.Unit(9922, TeamId.Blue, 10, 10, UnitType.Tank);
                f.Smoke(1, 1f);
                var writer = new SmokeTowerWriterProbe();

                // 연막 밖 — 판정 없음 · 착탄 때 탑 writer 1회(회귀).
                f.Now = 1d;
                Require(f.Combat.TryLaunchTowerProjectile(tower, RaceId.Human, outside, SmokeTowerShellPower, writer),
                    "E-T5 the tower shell at an outside unit must launch.");
                ProjectileLaunchPresentation plain = f.Launches[f.Launches.Count - 1];
                Require(!plain.Evaded && !plain.AttackerIsUnit, "E-T5 the tower shell outside the smoke must be a normal record.");
                f.Tick(plain.ImpactTime);
                Require(writer.Calls == 1, "E-T5 the tower writer must be invoked once for a non-evaded shell (regression).");

                // 연막 안(확률 1.0) — 발사 순간 회피, 착탄 때 탑 writer를 부르지 않는다.
                f.Now = 5d;
                int hp = inside.Hp, damaged = f.DamagedEvents;
                Require(f.Combat.TryLaunchTowerProjectile(tower, RaceId.Human, inside, SmokeTowerShellPower, writer),
                    "E-T5 the tower shell at a smoke unit must launch.");
                ProjectileLaunchPresentation shell = f.Launches[f.Launches.Count - 1];
                Require(shell.IsValid && shell.Evaded, "E-T5 a human cannon tower shell at a unit inside a chance-1.0 smoke must be evaded (TowerShell).");
                RequireSmokeLaunchMatchesRecomputation(shell, SmokeFixtureSeed, 10000, "E-T5 TowerShell");
                Require(Math.Abs(shell.ImpactTime - (5d + SkillEvasionRoll.MissTravelDistance(shell.Source, shell.MissPoint) / 4d)) < 1e-5,
                    "E-T5 the evaded tower shell flight must be (muzzle → miss point XZ distance) / tower speed.");
                f.Tick(shell.ImpactTime);
                Require(writer.Calls == 1 && f.Impacts[f.Impacts.Count - 1].Outcome == ProjectileImpactOutcome.Evaded
                        && inside.Hp == hp && f.DamagedEvents == damaged,
                    "E-T5 the evaded tower shell must resolve Evaded without invoking the tower writer.");
                FieldInfo rolls = typeof(SkillEvasionSystem).GetField("_diagRolls", BindingFlags.NonPublic | BindingFlags.Instance);
                int[] counts = (int[])rolls.GetValue(f.Evasion);
                Require(counts[(int)SkillEvasionPath.TowerShell] == 1 && counts[(int)SkillEvasionPath.Projectile] == 0,
                    "E-T5 the tower shell roll must be counted on the TowerShell path.");
            }
        }

        /// <summary> E-T12(S-3) — 스키마 12 · 회피 분류 토큰 · handshake 해시 · 발사 RPC · 늦은 접속 · 화면 · 착탄 갈래 소스 게이트. </summary>
        private static void ValidateSmokeProjectileSourceGates()
        {
            Require(UnitAttackShadowProfileResolver.PresentationContractSchemaRevision == 13,
                "E-T12 the smoke-bomb projectile launch RPC arguments must raise the presentation schema to 12.; current merged DoT provenance + smoke evasion require schema 13.");
            // [2026-11-23 S-4] S-4가 즉발 탑을 회피 대상에 넣어 토큰 끝에 ";tower-instant:spirit,transcendence"가 붙었다
            //   (S-3 때 기대값은 「…;tower-shell:human」까지) — 새 기대값은 ValidateSmokeTowerInstantSourceGates(E-T9 · E-T12)도 함께 본다.
            //   (2026-11-24 연막탄 테스트 PASS 뒤 주석으로 남겨 둔 S-3 옛 검사를 지웠다 — 이 검사가 회귀 스위트에서 통과했고, 같은 토큰이 든 handshake로 실기 멀티 경기가 시작됐다.)
            Require(SkillEvasionRoll.ManifestToken == "hitscan:0,1,2;projectile:6,7,12,17,21,24;tower-shell:human;tower-instant:spirit,transcendence",
                "E-T12 the evasion classification token must be hitscan 0,1,2 · projectile 6,7,12,17,21,24 (no Mushroom 26) · tower-shell human · tower-instant spirit,transcendence: "
                + SkillEvasionRoll.ManifestToken);
            Require(UnitAttackShadowProfileResolver.TryComputePresentationProfileHash(out string hash, out _) && hash.Length == 64,
                "E-T12 the handshake hash must still compute with schema 13 and the evasion token.");

            const string root = "Assets/_Project/Scripts/";
            string resolver = File.ReadAllText(root + "Infrastructure/Network/UnitAttackShadowProfileResolver.cs");
            Require(SupportActiveLineCount(resolver, "manifest.Append(\"|smoke-evasion=\")") == 1
                    && SupportActiveLineCount(resolver, ".Append(Hexiege.Application.SkillEvasionRoll.ManifestToken);") == 1,
                "E-T12 the handshake manifest must include the smoke-evasion classification token (M-11).");

            string net = File.ReadAllText(root + "Infrastructure/Network/NetworkCombatController.Projectiles.cs");
            Require(SupportActiveLineCount(net, "launch.Homing, launch.Evaded, launch.MissPoint, rpc);") == 1
                    && SupportActiveLineCount(net, "bool homing, bool evaded, Vector3 missPoint, ClientRpcParams rpc = default)") == 1
                    && SupportActiveLineCount(net, "evaded: evaded, missPoint: missPoint);") == 1
                    && SupportActiveLineCount(net, "SendProjectileLaunchRpc(launch, rpc);") == 1,
                "E-T12 the single launch RPC sender must carry evaded + missPoint, the receiver must rebuild them, and the late-join snapshot must reuse the same sender.");

            string combat = File.ReadAllText(root + "Application/UseCases/UnitCombatUseCase.Projectiles.cs");
            Require(SupportActiveLineCount(combat, "evaded: record.Evaded, missPoint: record.MissPoint);") == 2
                    && SupportActiveLineCount(combat, "bool evadeRolled = TryRollProjectileEvasion(record, target, source, center, radius, out ProjectileEvadeGate evade);") == 2,
                "E-T12 both the unit launch and the tower launch must roll at launch and record evaded + missPoint.");
            int flight = combat.IndexOf("private void ProcessProjectileFlight(", StringComparison.Ordinal);
            int evadedBranch = combat.IndexOf("ResolveEvadedProjectileImpact(record, key, diag, now);", flight, StringComparison.Ordinal);
            int writer = combat.IndexOf("AttackDamageObservation observation = ApplyProjectileImpactDamage(record, key);", flight, StringComparison.Ordinal);
            Require(flight > 0 && evadedBranch > flight && writer > evadedBranch,
                "E-T12 the evaded impact branch must return before the projectile impact writer is called.");
            string contracts = File.ReadAllText(root + "Application/Combat/ProjectileDeliveryContracts.cs");
            Require(SupportActiveLineCount(contracts, "&& (!Evaded || Finite(MissPoint));") == 1
                    && SupportActiveLineCount(contracts, "|| Outcome == ProjectileImpactOutcome.Evaded)") == 1,
                "E-T12 the launch / impact receiver checks must accept Evaded and require a finite miss point.");

            string presenter = File.ReadAllText(root + "Presentation/Effects/ProjectilePresentationPresenter.cs");
            Require(SupportActiveLineCount(presenter, "visual.LastEndpoint = MissEndpoint(launch);") == 1
                    && SupportActiveLineCount(presenter, "if (visual.Evaded)") == 1
                    && SupportActiveLineCount(presenter, "if (withResult && slot.Result.Outcome == ProjectileImpactOutcome.Hit)") == 1,
                "E-T12 the presenter must fly an evaded projectile to the fixed miss point and release damage numbers only for Hit.");
        }

        // ====================================================================
        // [2026-11-23 단계 S-4 — 즉발 탑 회피(정령 RuneSpire 번개 · 초월 VineTower)] E-T9 · E-T10 · E-T12
        //   [초급자용] 정령 · 초월 방어탑은 투사체 없이 "공격 순간 바로" 피해를 준다. 그래서 회피도 공격 순간 한 번 정한다.
        //   피했으면 피해 writer(IncomingDamage)를 부르지 않고 공격받음 · 피해 이벤트와 사망 처리도 하지 않는다 — 「빗나감」 표현 스냅샷만 낸다
        //   (Impact = 빗나간 지점, 적용 피해 0). 아래 검사는 실제 탑 UseCase · 구역 · 회피 서비스로 그 약속을 확인한다.
        //   탑 열쇠 = (탑 Id, 그 탑의 회피 판정 순번)이라 0.5 확률의 결과도 오프라인으로 정확히 다시 계산된다.
        // ====================================================================

        private const int SmokeSpiritTowerPower = 40;
        private const int SmokeTransTowerPower = 35;
        private const int SmokeHumanTowerInstantPower = 30;

        /// <summary>
        /// 즉발 탑 검사가 쓰는 행만 빈 표에 고정한다(Tank HP 10000 · 방어 0, 정령 · 초월 · 인간 AutoTower 공격력 · 쿨 1 · 사거리 3).
        /// 바깥 SupportFixtureStatsScope의 Dispose가 원래 표 참조를 되돌린다(이 메서드는 그 범위 안에서만 부른다).
        /// </summary>
        private static void PinSmokeTowerInstantStats()
        {
            var units = new Dictionary<UnitType, UnitStats.StatValues>();
            units[UnitType.Tank] = new UnitStats.StatValues { MaxHp = SmokeVictimHp, AttackPower = SmokeTankAttack, AttackRange = 3f,
                DetectRange = 3f, MoveSpeed = .5f, AttackCooldown = 2f, HitFrameTimes = new[] { .4f }, Defense = 0 };
            UnitStats.Initialize(units);
            var buildings = new Dictionary<(BuildingType, RaceId), BuildingStats.StatValues>();
            buildings[(BuildingType.AutoTower, RaceId.Spirit)] = new BuildingStats.StatValues
                { MaxHp = 1000, AttackPower = SmokeSpiritTowerPower, AttackCooldown = 1f, AttackRange = 3f };
            buildings[(BuildingType.AutoTower, RaceId.Transcendence)] = new BuildingStats.StatValues
                { MaxHp = 1000, AttackPower = SmokeTransTowerPower, AttackCooldown = 1f, AttackRange = 3f };
            buildings[(BuildingType.AutoTower, RaceId.Human)] = new BuildingStats.StatValues
                { MaxHp = 1000, AttackPower = SmokeHumanTowerInstantPower, AttackCooldown = 1f, AttackRange = 3f };
            BuildingStats.Initialize(buildings);
            Require(UnitStats.GetMaxHp(UnitType.Tank) == SmokeVictimHp && UnitStats.GetDefense(UnitType.Tank) == 0
                    && BuildingStats.GetAttackPower(BuildingType.AutoTower, RaceId.Spirit) == SmokeSpiritTowerPower
                    && BuildingStats.GetAttackPower(BuildingType.AutoTower, RaceId.Transcendence) == SmokeTransTowerPower
                    && BuildingStats.GetAttackPower(BuildingType.AutoTower, RaceId.Human) == SmokeHumanTowerInstantPower
                    && Mathf.Approximately(BuildingStats.GetAttackCooldown(BuildingType.AutoTower, RaceId.Spirit), 1f),
                "E-T0 smoke tower-instant fixture stats must be pinned (Tank HP " + SmokeVictimHp + ", Spirit tower " + SmokeSpiritTowerPower
                + ", Trans tower " + SmokeTransTowerPower + ", Human tower " + SmokeHumanTowerInstantPower + ").");
        }

        // 즉발 탑 연막 fixture — 실제 탑 UseCase(5인자 생성자) + 전투 UseCase(대상 반경 표) + 구역 · 회피 서비스 + 이벤트 수집.
        private sealed class SmokeTowerFixture : IDisposable
        {
            public readonly UnitSpawnUseCase Spawn;
            public readonly BuildingPlacementUseCase Buildings;
            public readonly UnitCombatUseCase Combat;
            public readonly TowerCombatUseCase Tower;
            public readonly SkillZoneSystem Zones;
            public readonly SkillEvasionSystem Evasion;
            public readonly List<AttackEffectPresentation> Snapshots = new List<AttackEffectPresentation>();
            public int DamagedEvents, AttackedEvents, DiedEvents;
            public double Now = 1d;
            // 팀 → 종족(탑 UseCase에 주입하는 함수가 이 값을 읽는다 — 검사마다 바꾼다).
            public RaceId RedRace = RaceId.Spirit;
            private readonly IDisposable _damagedSub, _attackedSub, _diedSub, _snapshotSub;
            private static readonly MethodInfo ExecuteTowerAttackMethod =
                typeof(TowerCombatUseCase).GetMethod("ExecuteTowerAttack", BindingFlags.Instance | BindingFlags.NonPublic);

            public SmokeTowerFixture(ulong seed = SmokeFixtureSeed, bool withLauncher = true)
            {
                var grid = new HexGrid(3, 3, HexOrientation.FlatTop);
                Spawn = new UnitSpawnUseCase(grid);
                Buildings = new BuildingPlacementUseCase(grid);
                var mapper = new ProjectileFixtureMapper(); // 타일 (q, r) → 월드 (q, 0, r)
                var positions = new ProjectileFixturePositions(); // 비어 있음 → 탑 UseCase가 헥스 좌표 변환으로 폴백(실제와 같은 규칙)
                Combat = new UnitCombatUseCase(grid, Spawn, Buildings, positions, mapper);
                var settings = new ProjectileSettings(humanTowerSpeed: 4f);
                settings.SetUnitRadius(UnitType.Tank, .3f);
                Combat.SetProjectileSettings(settings);
                Tower = new TowerCombatUseCase(Buildings, Spawn, mapper, positions, team => team == TeamId.Red ? RedRace : RaceId.Human);
                Tower.SetEffectClock(() => Now);
                // withLauncher=false = 투사체 발사기가 없는 상태(인간 탑도 즉발 경로로 오는 Editor 전용 상황) 재현.
                if (withLauncher) Tower.SetProjectileLauncher(Combat);
                Zones = new SkillZoneSystem(Spawn, mapper, Buildings);
                Combat.SetSkillZoneSystem(Zones);
                Evasion = new SkillEvasionSystem(Zones, seed);
                Combat.SetSkillEvasionSystem(Evasion);
                Tower.SetSkillEvasionSystem(Evasion);
                _damagedSub = GameEvents.OnEntityDamaged.Subscribe(_ => { DamagedEvents++; });
                _attackedSub = GameEvents.OnEntityAttacked.Subscribe(_ => { AttackedEvents++; });
                _diedSub = GameEvents.OnUnitDied.Subscribe(_ => { DiedEvents++; });
                _snapshotSub = GameEvents.OnAttackEffectConfirmed.Subscribe(Snapshots.Add);
                Require(ExecuteTowerAttackMethod != null, "E-T0 TowerCombatUseCase.ExecuteTowerAttack(BuildingData, UnitData) must exist.");
            }

            public UnitData Unit(int id, TeamId team, int q, int r)
            {
                UnitData unit = Spawn.SpawnUnitWithId(id, UnitType.Tank, team, new HexCoord(0, 0));
                Require(unit != null, "E-T0 smoke tower fixture unit must spawn: " + id);
                unit.Position = new HexCoord(q, r);
                return unit;
            }

            public BuildingData PlaceTower(int id, int q, int r, RaceId race)
            {
                BuildingData tower = Buildings.PlaceBuildingWithId(id, BuildingType.AutoTower, TeamId.Red, new HexCoord(q, r), race);
                Require(tower != null && tower.IsAlive, "E-T0 smoke tower fixture tower must be placed alive: " + id);
                return tower;
            }

            // 연막 — 기본은 Blue(피격 팀 = 아군 구역). caster를 Red로 주면 「적 연막」(탑 팀 구역)이 된다.
            public int Smoke(int sequence, float chance, TeamId caster = TeamId.Blue)
                => Zones.CreateZone(sequence, caster, 3, 77, Vector3.zero, 2f, 6f, StatusEffectKind.EvadeChance, chance, true);

            /// <summary> 실제 즉발 탑 공격 1회(ExecuteTowerAttack — Tick이 부르는 그 메서드). 공격이 일어났으면 true. </summary>
            public bool Attack(BuildingData tower, UnitData target)
                => (bool)ExecuteTowerAttackMethod.Invoke(Tower, new object[] { tower, target });

            public int TowerInstantRolls()
            {
                FieldInfo rolls = typeof(SkillEvasionSystem).GetField("_diagRolls", BindingFlags.NonPublic | BindingFlags.Instance);
                Require(rolls != null && rolls.GetValue(Evasion) is int[], "E-T0 SkillEvasionSystem._diagRolls must exist.");
                return ((int[])rolls.GetValue(Evasion))[(int)SkillEvasionPath.TowerInstant];
            }

            public void Dispose()
            {
                _damagedSub?.Dispose(); _attackedSub?.Dispose(); _diedSub?.Dispose(); _snapshotSub?.Dispose();
                Combat.CloseProjectiles("fixture-dispose");
            }
        }

        // 즉발 탑 공격 1회의 기대 회피 여부 · 빗나간 지점을 seed + (탑 Id, 순번)으로 다시 계산한다(서버와 같은 함수).
        private static bool SmokeTowerExpectedEvaded(ulong seed, int towerId, ulong ordinal, int chanceBp, out ulong roll)
        {
            roll = SkillEvasionRoll.ComputeRoll(seed, SkillEvasionRoll.KeyFromTowerOrdinal(towerId, ordinal));
            return SkillEvasionRoll.IsEvaded(roll, chanceBp);
        }

        /// <summary> E-T9(스냅샷 계약) · E-T10(탑 열쇠 분리) — 회피 스냅샷은 Tower · 적용 0만 유효, 탑 열쇠와 유닛 열쇠는 겹치지 않는다. </summary>
        private static void ValidateSmokeTowerInstantContracts()
        {
            var ok = new AttackEffectPresentation { Match = 1, Lifetime = 1, Sequence = 1, AttackerId = 1, TargetId = 2,
                Visual = AttackEffectVisual.Tower, Race = RaceId.Spirit, Source = Vector3.zero, Impact = Vector3.one, ImpactTime = 1d,
                AppliedDamage = 0, ResultHp = 50, Evaded = true };
            Require(ok.IsValid, "E-T9 an evaded Tower snapshot with applied 0 must be valid.");
            var applied = ok; applied.AppliedDamage = 5;
            Require(!applied.IsValid, "E-T9 an evaded snapshot claiming applied damage must be rejected (an evaded attack never hits).");
            var mushroom = ok; mushroom.Visual = AttackEffectVisual.MushroomImpact;
            Require(!mushroom.IsValid, "E-T9 only a Tower snapshot may be marked evaded (MushroomImpact is never evaded).");
            var hit = ok; hit.Evaded = false; hit.AppliedDamage = 5;
            Require(hit.IsValid && !default(AttackEffectPresentation).Evaded,
                "E-T9 a normal (non-evaded) tower snapshot must stay valid and the default Evaded must be false (old snapshots unchanged).");
            for (ulong n = 1; n <= 16; n++)
                Require(SkillEvasionRoll.KeyFromTowerOrdinal(12, n) != SkillEvasionRoll.KeyFromAttackerOrdinal(12, n)
                        && SkillEvasionRoll.KeyFromTowerOrdinal(12, n) == SkillEvasionRoll.KeyFromTowerOrdinal(12, n)
                        && SkillEvasionRoll.KeyFromTowerOrdinal(12, n) != SkillEvasionRoll.KeyFromTowerOrdinal(13, n),
                    "E-T10 tower keys must be deterministic, differ per tower and never equal the single-Hitscan key of the same id and ordinal.");
            Require(SkillEvasionRoll.IsTowerInstantEvadableAttacker(BuildingType.AutoTower, RaceId.Spirit)
                    && SkillEvasionRoll.IsTowerInstantEvadableAttacker(BuildingType.AutoTower, RaceId.Transcendence)
                    && !SkillEvasionRoll.IsTowerInstantEvadableAttacker(BuildingType.AutoTower, RaceId.Human)
                    && !SkillEvasionRoll.IsTowerInstantEvadableAttacker(BuildingType.Castle, RaceId.Spirit),
                "E-T9 tower-instant evasion targets must be the Spirit and Transcendence AutoTowers only (Human = projectile shell, S-3).");
        }

        /// <summary>
        /// E-T9 — 정령 RuneSpire · 초월 VineTower(확률 1.0): 피해 writer · 공격받음 / 피해 이벤트 · 사망 처리 없음, 빗나감 스냅샷 1건
        /// (Evaded · 적용 0 · HP 그대로 · Impact = ComputeMissPoint). 연막 없음은 기존 명중(회귀).
        /// </summary>
        private static void ValidateSmokeTowerInstantEvasion()
        {
            foreach (RaceId race in new[] { RaceId.Spirit, RaceId.Transcendence })
            {
                int power = race == RaceId.Spirit ? SmokeSpiritTowerPower : SmokeTransTowerPower;
                string tag = "E-T9 " + race;
                using (var f = new SmokeTowerFixture())
                {
                    f.RedRace = race;
                    BuildingData tower = f.PlaceTower(9930, 2, 0, race);
                    UnitData victim = f.Unit(9931, TeamId.Blue, 0, 0);
                    UnitData lowHp = f.Unit(9932, TeamId.Blue, 0, 1);

                    // 회귀(연막 없음) — 판정 없이 즉발 피해 1회 · 이벤트 2개 · 명중 스냅샷(Impact = 대상 발밑).
                    int hp0 = victim.Hp;
                    Require(f.Attack(tower, victim), tag + " the instant tower attack must happen without a smoke.");
                    AttackEffectPresentation plain = f.Snapshots[f.Snapshots.Count - 1];
                    Require(victim.Hp == hp0 - power && f.DamagedEvents == 1 && f.AttackedEvents == 1 && f.TowerInstantRolls() == 0
                            && !plain.Evaded && plain.AppliedDamage == power && plain.Impact == new Vector3(0f, 0f, 0f) && plain.Race == race,
                        tag + " without a smoke the tower must hit once through the writer with a normal snapshot (regression).");

                    // 확률 1.0 연막 — 공격 순간 회피: HP · 이벤트 · 사망 없음, 빗나감 스냅샷 1건.
                    f.Smoke(1, 1f);
                    Require(f.Zones.GetEvadeChance(victim.Id) >= 1f && f.Zones.GetEvadeChance(lowHp.Id) >= 1f,
                        tag + " fixture victims must be inside the chance-1.0 smoke.");
                    int hp1 = victim.Hp, damaged = f.DamagedEvents, attacked = f.AttackedEvents, snaps = f.Snapshots.Count;
                    Require(f.Attack(tower, victim), tag + " an evaded instant attack must still count as an attack (cooldown starts).");
                    Require(victim.Hp == hp1 && f.DamagedEvents == damaged && f.AttackedEvents == attacked,
                        tag + " an evaded instant tower attack must not invoke the damage writer nor raise attacked / damaged events (no number).");
                    Require(f.Snapshots.Count == snaps + 1, tag + " an evaded attack must publish exactly one presentation snapshot.");
                    AttackEffectPresentation miss = f.Snapshots[f.Snapshots.Count - 1];
                    Require(miss.IsValid && miss.Evaded && miss.AppliedDamage == 0 && miss.ResultHp == hp1 && miss.Race == race
                            && miss.Visual == AttackEffectVisual.Tower && miss.TargetId == victim.Id && miss.AttackerId == tower.Id,
                        tag + " the evaded snapshot must be a valid Tower snapshot with Evaded, applied 0 and the unchanged HP.");
                    Require(SmokeTowerExpectedEvaded(SmokeFixtureSeed, tower.Id, 1UL, 10000, out ulong roll),
                        tag + " the offline recomputation of (tower id, ordinal 1) at chance 1.0 must evade.");
                    Vector3 expected = SkillEvasionRoll.ComputeMissPoint(new Vector3(2f, 0f, 0f), Vector3.zero, .3f, roll);
                    Require((miss.Impact - expected).sqrMagnitude < 1e-10f && (miss.Impact - Vector3.zero).sqrMagnitude > .25f
                            && miss.Source == new Vector3(2f, 0f, 0f),
                        tag + " the snapshot Impact must be the miss point ComputeMissPoint(tower, target, radius 0.3, roll), not the target: "
                        + miss.Impact + " vs " + expected);

                    // 사망 처리 차단 — 맞으면 죽을 HP여도 회피면 살아 있고 목록에서 빠지지 않는다(사망 이벤트 0).
                    lowHp.TakeDamage(lowHp.Hp - 1);
                    int died = f.DiedEvents;
                    Require(f.Attack(tower, lowHp), tag + " the evaded attack at a 1-HP unit must still be an attack.");
                    Require(lowHp.IsAlive && lowHp.Hp == 1 && f.Spawn.GetUnit(lowHp.Id) == lowHp && f.DiedEvents == died,
                        tag + " an evaded lethal tower attack must leave the unit alive and in the unit list (no death workflow).");
                    Require(f.TowerInstantRolls() == 2, tag + " two smoke attacks must be two TowerInstant rolls (got " + f.TowerInstantRolls() + ").");
                }
            }
        }

        /// <summary>
        /// E-T9 · E-T10 — 탑 열쇠 = (탑 Id, 그 탑의 회피 판정 순번): 확률 0.5에서 매 공격의 결과가 오프라인 재계산과 같고,
        /// HP 감소 = 공격력 × 명중 수, 연막 밖 공격은 순번을 늘리지 않으며, 다른 탑의 순번은 따로 센다. 다른 seed면 결과가 달라진다.
        /// </summary>
        private static void ValidateSmokeTowerInstantKeys()
        {
            using (var f = new SmokeTowerFixture())
            {
                BuildingData tower = f.PlaceTower(9940, 2, 0, RaceId.Spirit);
                BuildingData other = f.PlaceTower(9941, 2, 1, RaceId.Spirit);
                UnitData victim = f.Unit(9942, TeamId.Blue, 0, 0);
                UnitData outside = f.Unit(9943, TeamId.Blue, 10, 10);
                f.Smoke(1, .5f);
                int start = victim.Hp, hits = 0, evades = 0;
                const int shots = 32;
                for (int i = 1; i <= shots; i++)
                {
                    int hp = victim.Hp;
                    Require(f.Attack(tower, victim), "E-T9 key shot " + i + " must be an attack.");
                    bool expected = SmokeTowerExpectedEvaded(SmokeFixtureSeed, tower.Id, (ulong)i, 5000, out _);
                    AttackEffectPresentation s = f.Snapshots[f.Snapshots.Count - 1];
                    Require(s.Evaded == expected && (expected ? victim.Hp == hp : victim.Hp == hp - SmokeSpiritTowerPower),
                        "E-T9 key shot " + i + " must match the offline recomputation from seed + (tower id, ordinal " + i + ").");
                    if (expected) evades++; else hits++;
                    // 연막 밖 대상 공격을 사이에 끼워도 순번이 늘지 않는다(판정 자체가 없다).
                    if (i % 4 == 0) Require(f.Attack(tower, outside), "E-T9 the interleaved outside attack must happen.");
                }
                Require(evades > 0 && hits > 0 && start - victim.Hp == hits * SmokeSpiritTowerPower && f.TowerInstantRolls() == shots,
                    "E-T9 at chance 0.5 some attacks must evade and some hit; HP loss = power × hits; rolls = smoke attacks only (evades "
                    + evades + ", hits " + hits + ", rolls " + f.TowerInstantRolls() + ").");
                // 다른 탑은 자기 순번 1부터 시작한다(첫 탑 순번과 무관).
                Require(f.Attack(other, victim), "E-T9 the second tower attack must happen.");
                bool otherExpected = SmokeTowerExpectedEvaded(SmokeFixtureSeed, other.Id, 1UL, 5000, out _);
                Require(f.Snapshots[f.Snapshots.Count - 1].Evaded == otherExpected,
                    "E-T9 a second tower must count its own ordinal from 1 (per-tower key, independent of the first tower).");
            }
            // 다른 seed — 32개 탑 열쇠 중 적어도 하나는 결과가 달라진다(seed가 실제로 섞인다).
            int differs = 0;
            for (ulong n = 1; n <= 32; n++)
                if (SmokeTowerExpectedEvaded(SmokeFixtureSeed, 9940, n, 5000, out _) != SmokeTowerExpectedEvaded(SmokeFixtureSeed ^ 0x77UL, 9940, n, 5000, out _))
                    differs++;
            Require(differs > 0, "E-T10 a different match seed must change at least one tower roll outcome.");
        }

        /// <summary>
        /// E-T9 — 판정하지 않는 경우: 연막 밖 대상 · 적 연막(탑 팀이 깐 연막 — 피격 유닛은 구성원 아님) · 인간 탑이 즉발 대역 경로로 온 경우.
        /// 그리고 회피한 공격도 Tick 흐름에서 쿨다운을 시작한다(빗나가도 다음 공격까지 기다린다).
        /// </summary>
        private static void ValidateSmokeTowerInstantExclusions()
        {
            using (var f = new SmokeTowerFixture())
            {
                BuildingData tower = f.PlaceTower(9950, 2, 0, RaceId.Spirit);
                UnitData outside = f.Unit(9951, TeamId.Blue, 10, 10);
                UnitData inEnemySmoke = f.Unit(9952, TeamId.Blue, 0, 0);
                f.Smoke(1, 1f, TeamId.Red);   // 적 연막(Red 구역) — Blue 유닛은 구성원이 아니다
                Require(f.Zones.GetEvadeChance(inEnemySmoke.Id) == 0f, "E-T9 a Blue unit must get no evasion from a Red smoke.");
                int hpA = outside.Hp, hpB = inEnemySmoke.Hp;
                Require(f.Attack(tower, outside) && f.Attack(tower, inEnemySmoke), "E-T9 the exclusion attacks must happen.");
                Require(outside.Hp == hpA - SmokeSpiritTowerPower && inEnemySmoke.Hp == hpB - SmokeSpiritTowerPower && f.TowerInstantRolls() == 0,
                    "E-T9 outside-smoke and enemy-smoke targets must be hit without a roll.");
            }
            using (var f = new SmokeTowerFixture(withLauncher: false))
            {
                // 인간 탑 + 발사기 없음 = 인간 탑이 즉발 경로로 오는 Editor 전용 상황. 포탄 탑이라 즉발 회피 대상이 아니다(판정 없음).
                f.RedRace = RaceId.Human;
                BuildingData human = f.PlaceTower(9955, 2, 0, RaceId.Human);
                UnitData victim = f.Unit(9956, TeamId.Blue, 0, 0);
                f.Smoke(1, 1f);
                int hp = victim.Hp;
                Require(f.Attack(human, victim) && victim.Hp == hp - SmokeHumanTowerInstantPower && f.TowerInstantRolls() == 0,
                    "E-T9 a human tower on the instant fallback must not roll (its evasion belongs to the projectile shell path, S-3).");
            }
            using (var f = new SmokeTowerFixture())
            {
                // Tick 흐름 — 회피한 공격도 쿨다운을 시작한다(공격 1회 = 쿨 1초).
                BuildingData tower = f.PlaceTower(9960, 2, 0, RaceId.Spirit);
                UnitData victim = f.Unit(9961, TeamId.Blue, 0, 0);
                f.Smoke(1, 1f);
                int hp = victim.Hp, snaps = f.Snapshots.Count;
                tower.AttackCooldownRemaining = 0f;
                f.Tower.Tick(.1f);
                Require(f.Snapshots.Count == snaps + 1 && f.Snapshots[f.Snapshots.Count - 1].Evaded && victim.Hp == hp
                        && Mathf.Approximately(tower.AttackCooldownRemaining, 1f),
                    "E-T9 through Tick an evaded instant attack must publish the evaded snapshot and still start the 1 s cooldown (cooldown "
                    + tower.AttackCooldownRemaining + ").");
                f.Tower.Tick(.1f);
                Require(f.Snapshots.Count == snaps + 1, "E-T9 the tower must wait for its cooldown after an evaded attack.");
            }
        }

        /// <summary> E-T12(S-4) — 관문 자리(writer 앞) · 조합 루트 주입 · 탑 공격 표현 RPC evaded 칸 · 화면 비추적 · 스냅샷 검사 · 토큰 · 스키마 12. </summary>
        private static void ValidateSmokeTowerInstantSourceGates()
        {
            Require(UnitAttackShadowProfileResolver.PresentationContractSchemaRevision == 13,
                "E-T12 S-4 tower RPC change stays in the schema 13 bundle (no further bump before commit).; current merged DoT provenance + smoke evasion require schema 13.");
            Require(SkillEvasionRoll.ManifestToken.EndsWith(";tower-instant:spirit,transcendence", StringComparison.Ordinal),
                "E-T12 the handshake evasion token must end with the tower-instant classification: " + SkillEvasionRoll.ManifestToken);

            const string root = "Assets/_Project/Scripts/";
            string tower = File.ReadAllText(root + "Application/UseCases/TowerCombatUseCase.cs");
            int method = tower.IndexOf("private bool ExecuteTowerAttack(", StringComparison.Ordinal);
            int gate = tower.IndexOf("bool evadeRolled = TryRollTowerInstantEvasion(tower, race, target, effectSource, effectImpact, out TowerInstantEvadeGate evade);", method, StringComparison.Ordinal);
            int early = tower.IndexOf("return PublishEvadedTowerInstantAttack(tower, race, target, effectSource, effectTime, evade);", method, StringComparison.Ordinal);
            int writer = tower.IndexOf("IncomingDamage.Apply(_supportEffects, target, damage, rawDamage, IncomingDamageWriter.Tower);", method, StringComparison.Ordinal);
            Require(method > 0 && gate > method && early > gate && writer > early,
                "E-T12 the tower-instant evasion gate and its early return must sit before the instant damage writer.");
            Require(SupportActiveLineCount(tower, "IncomingDamage.Apply(") == 2 && SupportActiveLineCount(tower, ".TakeDamage(") == 0,
                "E-T12 the tower use case must keep exactly its two damage points (the evaded path adds no writer call).");

            string boot = File.ReadAllText(root + "Bootstrap/GameBootstrapper.Setup.cs");
            Require(SupportActiveLineCount(boot, "_towerCombat.SetSkillEvasionSystem(_skillEvasionSystem);") == 1,
                "E-T12 the composition root must inject the same evasion service into the tower use case.");

            string net = File.ReadAllText(root + "Infrastructure/Network/NetworkCombatController.cs");
            Require(SupportActiveLineCount(net, "s.ResultKey.VictimKind, s.ResultKey.VictimId, s.ResultKey.EffectKind, s.ResultKey.ResultOrdinal,") == 1
                    && SupportActiveLineCount(net, "s.Evaded);") == 1
                    && SupportActiveLineCount(net, "Evaded = evaded };") == 1
                    && SupportActiveLineCount(net, "bool evaded)") == 1,
                "E-T12 AttackEffectClientRpc must send and rebuild the evaded flag (Impact carries the miss point).");

            string queue = File.ReadAllText(root + "Presentation/Effects/HitPresentationQueue.cs");
            Require(SupportActiveLineCount(queue, "if (s.Evaded) boltUntracked = \"Evaded\";") == 1,
                "E-T12 the presentation queue must not resolve / track the target for an evaded tower snapshot (fixed endpoint at the miss point).");

            string snapshot = File.ReadAllText(root + "Application/Combat/AttackEffectPresentation.cs");
            Require(SupportActiveLineCount(snapshot, "&& (!Evaded || (Visual == AttackEffectVisual.Tower && AppliedDamage == 0));") == 1,
                "E-T12 the snapshot validity check must require an evaded snapshot to be a Tower snapshot with applied 0.");
        }
    }
}
