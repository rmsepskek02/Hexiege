// ============================================================================
// RunUnitActionSelfValidation.AirStrike.cs
// [2026-11-17 지원 스킬 2-L 공습 — L-1 서버 판정 · 연속 폭격 + L-2 네트워크 · 스키마 11] 회귀 검증(fixture).
//   (2026-11-17 L-5 · L-6 — LS-T9 자산 켜기 · VFX 칸 검사를 덧붙였다.)
// Task: Assets/_Project/Docs/_Tasks/2026-11-17/09_12_support-skill-air-strike/ (Plan 7절 LS-T* 중 L-1 · L-2 해당 항목).
// Run()(메뉴 「Hexiege/Combat/Run Full Regression Suite」)이 함께 돌린다.
//
// [배경 — 초급 개발자용 설명]
//   공습은 아직 자산이 「사용 불가」라(자산 켜기 = Plan L-6) 게임 안에서 눌러 볼 수 없다. 그래서 서버 판정 코드
//   (LineStrikeGeometry 순수 함수 · SkillActivationUseCase 예약 · UnitCombatUseCase 폭격 1회)를 fixture로 직접 불러 숫자로 확인한다.
//   [2026-11-17 L-6 정정 — 위 두 줄은 L-1 · L-2 때의 설명이다] 이제 공습 자산은 「사용 가능」으로 켜져 게임에서 쓸 수 있다(LS-T9).
//   fixture 검사는 그대로 둔다 — 자산 값과 무관하게 서버 판정 코드를 숫자로 확인하는 역할이다.
//   🔴 전역 스탯 표(UnitStats · BuildingStats)에 기대지 않는다 — 메뉴로 돌리면 비어 있고, Play 뒤에는 실제 값이 남아 있다
//   (공용 메모리 2026-11-17 「Regression suite fixtures」). 그래서 LineStrikeFixtureStatsScope가 「빈 표」에서 시작해
//   이 검사가 쓰는 행만 고정하고, 끝나면(예외로 끝나도) 원래 참조를 되돌린다(SupportFixtureStatsScope 선례와 같은 방식).
//
// 무엇을 확인하는가(Plan 7절 번호):
//   LS-T1  직선 판정 순수 함수 — 시작점 · 끝점 · 중간 · 반폭 안/밖 · 시작점 뒤 · 끝점 너머 · 대각선 · 경계(t = 0 · L, d = 반폭)는 안.
//   LS-T2  구간 분할 — 0 ≤ t ≤ L의 모든 점이 정확히 한 구간, 반열린 경계(1.2 · 2.4 …는 뒤 구간), 끝점은 마지막 구간.
//   LS-T11 맵 안 유효 길이 순수 함수 — 맵 안 · 끝이 맵 밖 · 경계 위에서 밖으로 · 대각선 모서리 · 음의 방향 · 시작점 밖 · NaN.
//          (맵 경계는 사각형이라 「나갔다 다시 들어오는」 모양은 생기지 않는다 — 그 픽스처는 만들 수 없어 넣지 않았다.)
//   LS-T3  연속 폭격 횟수 · 순서 · 시각 — 0.1초 틱으로 0.4 · 0.6 · 0.8 · 1.0 · 1.2초에 구간 1 → 5 차례로, 쿨다운은 발동 순간.
//   LS-T4  시전당 중복 없음 — ① 한 구간 안 유닛 · 건물 ② 폭격 사이 다음 구간으로 옮긴 유닛 ③ 유닛 Id = 건물 Id(따로 센다).
//   LS-T5  아군 · 직선 밖(옆 · 시작점 뒤 · 끝점 너머) 대상 피해 0.
//   LS-T6  경기 종료(ClosePendingImpacts) 뒤 남은 폭격 피해 0 · 새 예약 거부 · 폭격 처리 중 닫힘(같은 틱 남은 폭격 버림).
//   LS-T7  잘못된 방향(0 · NaN · 무한대 · 너무 짧음 · 수직만) 거부 · 쿨다운 없음, 정규화(3, 4 → 0.6, 0.8) · 발동 알림 방향 정리.
//   LS-T8  스키마 11 + RPC 방향 인자 · 서버 재검증 · 배선 소스 게이트 + 메커니즘 5 · 실행기 등록 · 자산 칸 초기값(P-7).
//   LS-T10 원형 스킬 무변경 — 포격(타입 A · 착탄 지연 0.4) 예약 · 피해 1회 · 0.3초 전엔 피해 없음 · 방향 0으로도 발동.
//   LS-T12 유효 길이 밖 폭격 — 유효 길이 2.5(L의 절반 미만): 구간 3 일부만 판정 · 구간 4 · 5 건너뜀 · 맵 밖 대상 피해 0.
//
//   LS-T9  [2026-11-17 L-5 · L-6 추가] 공습 자산 켜기 — 사용 가능 · 지점 지정 · 메커니즘 5 · P-7 임시 수치(길이 6 · 폭 2 · 5회 · 간격 0.2 ·
//          첫 지연 0.4 · 150 · 쿨 10) · 인간 로드아웃 슬롯 1 · 셋업 기대 수 13 · 셋업 표의 공습 줄(자리표시 아님).
//          *(2026-11-18 메테오 — 셋업 기대 수 12 → 13. 직전 「셋업 기대 수 12」)*
//          덧붙여 L-5: VFX 칸 초기값(기준 길이 6 · 폭격 프리팹 없음) · 기준 길이 정리 · 칸에 든 프리팹의 1회 재생 계약(반복 없음 · Hierarchy) ·
//          재생 경로 소스 게이트(공습 분기가 원형 「프리팹 없음」 검사보다 앞 · 공용 유효 길이 함수 · 맵 밖 구간 건너뜀 · 회전 재생 · 경계 배선).
//
// 하지 않는 것: LS-T13(떼는 순서 · 터치 Id)과 LS-T1의 「빨강 뷰 변환 뒤 도메인 방향」(P-10) — 이 둘은 AirStrikeAim.cs(L-3 · L-4)가 본다.
//   실제 멀티 전송 · 화면 이펙트의 실제 재생 모양 · 타이밍(L-5 경고 · 폭격 연출)은 사용자 실기 항목(Plan L-L16 · L-L17)이다.
// 모든 fixture는 새 인스턴스를 만든다. 실행 중(Play)에는 돌지 않는다.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Hexiege.Application;
using Hexiege.Domain;
using Hexiege.Infrastructure;
using UniRx;
using UnityEngine;

namespace Hexiege.Editor.Combat
{
    public static partial class RunUnitActionSelfValidation
    {
        /// <summary>
        /// 공습(직선 연속 폭격) L-1 · L-2 회귀 검증 진입점(LS-T1 · T2 · T3 · T4 · T5 · T6 · T7 · T8 · T10 · T11 · T12).
        /// </summary>
        private static void ValidateAirStrikeLineStrike()
        {
            Require(!UnityEngine.Application.isPlaying, "Air strike fixture must run outside a live match.");
            // 전역 스탯 표 고정(Tank HP 10000 · 방어 0, 인간 FlightFacility · Castle HP) — 끝나면 원래 참조를 되돌린다.
            using var fixtureStats = new LineStrikeFixtureStatsScope();
            ValidateLineStrikeInsideGeometry();      // LS-T1
            ValidateLineStrikeSegments();            // LS-T2
            ValidateLineStrikeEffectiveLength();     // LS-T11
            ValidateLineStrikeSequenceAndTiming();   // LS-T3 · LS-T4 ① ③ · LS-T5
            ValidateLineStrikeOncePerCastMoving();   // LS-T4 ②
            ValidateLineStrikeClose();               // LS-T6
            ValidateLineStrikeDirectionValidation(); // LS-T7
            ValidateLineStrikeOutOfMapStrikes();     // LS-T12
            ValidateLineStrikeBombardmentUnchanged(); // LS-T10
            ValidateLineStrikeSourceGates();         // LS-T8
            ValidateLineStrikeAssetEnabled();        // LS-T9 (+ L-5 VFX 칸 · 재생 경로 게이트) — 2026-11-17 L-5 · L-6 추가
            // [2026-11-17 L-5 · L-6] PASS 문구 끝에 LS-T9를 덧붙였다(옛 문구는 「… out-of-map strikes skipped (LS-T12).」로 끝났다).
            // [2026-11-18 메테오 Task S2-4] PASS 문구의 「setup count 12」를 13으로 바꿨다(메테오를 켜 셋업 기대 수가 13이 됐다 — 문구만).
            Debug.Log("[UAS-DIAG][SKILL-LINE] self-validation PASS: line inside / boundary / behind / beyond / diagonal (LS-T1), every point in exactly one half-open segment with the end point in the last (LS-T2), map-clipped effective length incl. boundary-outward 0 and diagonal corner (LS-T11), five strikes at 0.4 / 0.6 / 0.8 / 1.0 / 1.2 s in segment order with cooldown at activation (LS-T3), one hit per target per cast incl. moved units and equal unit / building ids (LS-T4), allies and off-line targets untouched (LS-T5), game-end drop incl. close during a strike and rejection after close (LS-T6), invalid direction rejected without cooldown and normalization (LS-T7), schema 11 / RPC direction / wiring source gates and asset defaults (LS-T8), Bombardment delay path unchanged (LS-T10), out-of-map strikes skipped (LS-T12), Air Strike asset enabled with P-7 values in Human slot 1 / setup count 13 / VFX slot contract and playback gates (LS-T9).");
        }

        // ── fixture 전용 스탯 고정 ─────────────────────────────────────────
        //   SupportFixtureStatsScope(RunUnitActionSelfValidation.SupportNewStatuses.cs)와 같은 원칙 — 「빈 표」에서 시작해
        //   이 검사가 쓰는 행만 넣고, 끝나면 두 표의 원래 참조(null이었으면 null)를 그대로 되돌린다.
        //     - Tank(피격 대상) HP 10000 · 방어 0: 폭격 1회 150을 맞아도 살아 있고, 방어 감쇄 없이 정확히 150이 깎인다.
        //     - 인간 FlightFacility HP 1000: 시전 건물(스킬 건물 재검증을 통과해야 한다 — 살아 있어야 한다).
        //     - 인간 Castle HP 5000: 적 건물 대상(150을 맞아도 살아 있다).
        private sealed class LineStrikeFixtureStatsScope : IDisposable
        {
            public const int VictimTankMaxHp = 10000;
            public const int CasterMaxHp = 1000;
            public const int TargetCastleMaxHp = 5000;

            private static readonly FieldInfo UnitDataField =
                typeof(UnitStats).GetField("_data", BindingFlags.Static | BindingFlags.NonPublic);
            private static readonly FieldInfo BuildingDataField =
                typeof(BuildingStats).GetField("_data", BindingFlags.Static | BindingFlags.NonPublic);

            private readonly object _oldUnitData;
            private readonly object _oldBuildingData;
            private bool _disposed;

            public LineStrikeFixtureStatsScope()
            {
                Require(UnitDataField != null && BuildingDataField != null,
                    "LS-T0 fixture stat seams (UnitStats._data / BuildingStats._data) must exist.");
                _oldUnitData = UnitDataField.GetValue(null);
                _oldBuildingData = BuildingDataField.GetValue(null);

                var units = new Dictionary<UnitType, UnitStats.StatValues>();
                units[UnitType.Tank] = new UnitStats.StatValues { MaxHp = VictimTankMaxHp, AttackPower = 50, AttackRange = 3f,
                    DetectRange = 3f, MoveSpeed = .5f, AttackCooldown = 2f, HitFrameTimes = new[] { .4f }, Defense = 0 };
                UnitStats.Initialize(units);

                var buildings = new Dictionary<(BuildingType, RaceId), BuildingStats.StatValues>();
                buildings[(BuildingType.FlightFacility, RaceId.Human)] = new BuildingStats.StatValues { MaxHp = CasterMaxHp };
                buildings[(BuildingType.Castle, RaceId.Human)] = new BuildingStats.StatValues { MaxHp = TargetCastleMaxHp };
                BuildingStats.Initialize(buildings);

                Require(UnitStats.GetMaxHp(UnitType.Tank) == VictimTankMaxHp && UnitStats.GetDefense(UnitType.Tank) == 0
                        && BuildingStats.GetMaxHp(BuildingType.FlightFacility, RaceId.Human) == CasterMaxHp
                        && BuildingStats.GetMaxHp(BuildingType.Castle, RaceId.Human) == TargetCastleMaxHp,
                    "LS-T0 fixture stats must be pinned (Tank HP " + VictimTankMaxHp + " defense 0, Human FlightFacility HP "
                    + CasterMaxHp + ", Human Castle HP " + TargetCastleMaxHp + ").");
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

        // 로드아웃 대역 — 인간 로드아웃 = [슬롯 0 포격(타입 A · 지연 0.4), 슬롯 1 공습(직선 연속 폭격)].
        private sealed class LineStrikeFixtureLoadout : ISkillDataProvider
        {
            private readonly IReadOnlyList<SkillData> _loadout;
            public LineStrikeFixtureLoadout(SkillData slot0, SkillData slot1) { _loadout = new[] { slot0, slot1 }; }
            public IReadOnlyList<SkillData> GetLoadout(RaceId race) => _loadout;
        }

        private const int LineStrikeDamage = 150;   // 폭격 1회 피해(P-7 임시값)
        private const float LineStrikeCooldown = 10f; // 쿨다운(P-7 임시값)

        // 공습 스킬 데이터(P-7 임시값 — 길이 6 · 폭 2 · 5회 · 첫 지연 0.4 · 간격 0.2 · 1회 150 · 쿨 10).
        private static SkillData LineStrikeSkill(float length = 6f, float width = 2f, int count = 5, float firstDelay = .4f,
                                                 float interval = .2f, int damage = LineStrikeDamage)
        {
            return new SkillData(null, SkillAimType.PointTarget, LineStrikeCooldown, SkillMechanicType.LineStrike, 2f, 0f, damage, 0f,
                0, 0f, false, "공습", string.Empty, true, firstDelay, null, 2f,
                lineLength: length, lineWidth: width, lineStrikeCount: count, lineStrikeInterval: interval);
        }

        // 포격 스킬 데이터(타입 A · 반경 2 · 150 · 지연 0.4 — 원형 스킬 무변경 확인용).
        private static SkillData LineStrikeBombardmentSkill()
        {
            return new SkillData(null, SkillAimType.PointTarget, LineStrikeCooldown, SkillMechanicType.InstantAreaDamage, 2f, 0f,
                LineStrikeDamage, 0f, 0, 0f, false, "포격 지원", string.Empty, true, .4f, null, 2f);
        }

        private sealed class LineStrikeFixture
        {
            public readonly HexGrid Grid;
            public readonly UnitSpawnUseCase Spawn;
            public readonly BuildingPlacementUseCase Buildings;
            public readonly UnitCombatUseCase Combat;
            public readonly SkillActivationUseCase Activation;
            public readonly BuildingData Caster;
            public Vector3 BoundsMin = new Vector3(-.5f, 0f, -.5f);
            public Vector3 BoundsMax = new Vector3(20.5f, 0f, 20.5f);

            public LineStrikeFixture(SkillData lineSkill = null)
            {
                // FlatTop 12 × 12 — 대상 건물을 (5, 5) 같은 타일에 놓을 수 있게 넉넉히(유닛은 (0, 0)에 만든 뒤 좌표만 옮긴다).
                Grid = new HexGrid(12, 12, HexOrientation.FlatTop);
                Spawn = new UnitSpawnUseCase(Grid);
                Buildings = new BuildingPlacementUseCase(Grid);
                // 좌표 대역: 헥스 (q, r) → 월드 (q, 0, r) — 판정 좌표를 정수로 바로 읽을 수 있다.
                Combat = new UnitCombatUseCase(Grid, Spawn, Buildings, new ProjectileFixturePositions(), new ProjectileFixtureMapper());
                var loadout = new LineStrikeFixtureLoadout(LineStrikeBombardmentSkill(), lineSkill ?? LineStrikeSkill());
                Activation = new SkillActivationUseCase(Buildings, InBounds, Combat, loadout, _ => RaceId.Human);
                Activation.SetMapWorldBoundsProvider(Bounds);
                Caster = Buildings.PlaceBuildingWithId(97000, BuildingType.FlightFacility, TeamId.Blue, new HexCoord(0, 3), RaceId.Human);
                Require(Caster != null && Caster.IsAlive, "LS-T0 fixture caster FlightFacility must be placed alive.");
            }

            private bool InBounds(Vector3 p) =>
                p.x >= BoundsMin.x && p.x <= BoundsMax.x && p.z >= BoundsMin.z && p.z <= BoundsMax.z;

            private bool Bounds(out Vector3 min, out Vector3 max)
            {
                min = BoundsMin;
                max = BoundsMax;
                return true;
            }

            public UnitData Unit(int id, TeamId team, int x, int z)
            {
                UnitData unit = Spawn.SpawnUnitWithId(id, UnitType.Tank, team, new HexCoord(0, 0));
                Require(unit != null && unit.MaxHp == LineStrikeFixtureStatsScope.VictimTankMaxHp,
                    "LS-T0 fixture Tank must spawn with the pinned HP: " + id);
                unit.Position = new HexCoord(x, z);
                return unit;
            }

            public BuildingData Castle(int id, TeamId team, int q, int r)
            {
                BuildingData b = Buildings.PlaceBuildingWithId(id, BuildingType.Castle, team, new HexCoord(q, r), RaceId.Human);
                Require(b != null && b.MaxHp == LineStrikeFixtureStatsScope.TargetCastleMaxHp,
                    "LS-T0 fixture Castle must be placed with the pinned HP at (" + q + ", " + r + ").");
                return b;
            }

            public bool Fire(Vector3 start, Vector3 direction) =>
                Activation.Activate(Caster.Id, 1, start, direction);

            public void Tick(int count, float dt = .1f)
            {
                for (int i = 0; i < count; i++) Activation.TickPendingImpacts(dt);
            }
        }

        private static int LineStrikeLost(IDamageable target, int maxHp) => maxHp - target.Hp;

        // ── LS-T1 ───────────────────────────────────────────────────────

        /// <summary> LS-T1 — 직선 판정 순수 함수(안 / 밖 · 경계 포함 · 대각선). </summary>
        private static void ValidateLineStrikeInsideGeometry()
        {
            // 시작 (0, 0) · 방향 +X · 길이 6 · 반폭 1.
            Require(LineStrikeGeometry.IsInside(0, 0, 1, 0, 6, 1, 0, 0, out float t0) && t0 == 0f, "LS-T1 the start point is inside (t = 0).");
            Require(LineStrikeGeometry.IsInside(0, 0, 1, 0, 6, 1, 6, 0, out float t6) && t6 == 6f, "LS-T1 the end point is inside (t = L).");
            Require(LineStrikeGeometry.IsInside(0, 0, 1, 0, 6, 1, 3, 0, out float t3) && t3 == 3f, "LS-T1 the middle is inside.");
            Require(LineStrikeGeometry.IsInside(0, 0, 1, 0, 6, 1, 3, 1, out _) && LineStrikeGeometry.IsInside(0, 0, 1, 0, 6, 1, 3, -1, out _),
                "LS-T1 a point exactly half a width to the side is inside (d = half width).");
            Require(LineStrikeGeometry.IsInside(0, 0, 1, 0, 6, 1, 3, .5f, out _), "LS-T1 a point within the half width is inside.");
            Require(!LineStrikeGeometry.IsInside(0, 0, 1, 0, 6, 1, 3, 1.01f, out _), "LS-T1 a point past the half width is outside.");
            Require(!LineStrikeGeometry.IsInside(0, 0, 1, 0, 6, 1, -.01f, 0, out _), "LS-T1 a point behind the start (t < 0) is outside.");
            Require(!LineStrikeGeometry.IsInside(0, 0, 1, 0, 6, 1, 6.01f, 0, out _), "LS-T1 a point beyond the end (t > L) is outside.");
            // 대각선 — 방향 (0.6, 0.8), 길이 5: 끝점 (3, 4), 옆 수직 (0.8, −0.6).
            Require(LineStrikeGeometry.IsInside(0, 0, .6f, .8f, 5, 1, 3, 4, out float td) && Math.Abs(td - 5f) < 1e-4f,
                "LS-T1 diagonal end point (3, 4) is inside at t = 5.");
            Require(LineStrikeGeometry.IsInside(0, 0, .6f, .8f, 5, 1, 3.8f, 3.4f, out _),
                "LS-T1 diagonal point exactly one half width to the side is inside.");
            Require(!LineStrikeGeometry.IsInside(0, 0, .6f, .8f, 5, 1, 4.6f, 2.8f, out _),
                "LS-T1 diagonal point two units to the side is outside.");
            Require(!LineStrikeGeometry.IsInside(0, 0, 1, 0, 6, 1, float.NaN, 0, out _), "LS-T1 a NaN point is outside.");
            // 방향 정규화 — 0 · 짧음 · NaN · 무한대 거부.
            Require(LineStrikeGeometry.TryNormalizeDirection(3, 4, out float nx, out float nz) && Math.Abs(nx - .6f) < 1e-6f && Math.Abs(nz - .8f) < 1e-6f,
                "LS-T1 (3, 4) normalizes to (0.6, 0.8).");
            Require(!LineStrikeGeometry.TryNormalizeDirection(0, 0, out _, out _) && !LineStrikeGeometry.TryNormalizeDirection(1e-6f, 0, out _, out _)
                    && !LineStrikeGeometry.TryNormalizeDirection(float.NaN, 1, out _, out _)
                    && !LineStrikeGeometry.TryNormalizeDirection(float.PositiveInfinity, 0, out _, out _),
                "LS-T1 zero / too short / NaN / infinite directions are rejected.");
        }

        // ── LS-T2 ───────────────────────────────────────────────────────

        /// <summary> LS-T2 — 구간 분할(반열린 구간 · 끝점은 마지막 구간 · 모든 점이 정확히 한 구간). </summary>
        private static void ValidateLineStrikeSegments()
        {
            const float L = 6f;
            const int N = 5;
            for (int k = 0; k < N; k++)
            {
                LineStrikeGeometry.GetSegmentBounds(k, N, L, out float from, out _);
                Require(LineStrikeGeometry.SegmentIndexOf(from, L, N) == k,
                    "LS-T2 segment " + k + " boundary " + from + " belongs to the later segment (half-open [from, to)).");
                if (k > 0)
                    Require(LineStrikeGeometry.SegmentIndexOf(from - 1e-3f, L, N) == k - 1,
                        "LS-T2 just before boundary " + from + " belongs to segment " + (k - 1) + ".");
            }
            Require(LineStrikeGeometry.SegmentIndexOf(L, L, N) == N - 1, "LS-T2 the end point t = L belongs to the last segment (closed).");
            Require(LineStrikeGeometry.SegmentIndexOf(0f, L, N) == 0, "LS-T2 the start point t = 0 belongs to the first segment.");

            // 0 ~ L을 0.001 간격으로 훑어 구간 번호가 0 ~ N−1 안 · 줄지 않음 · 구간 경계 안(from ≤ t < to, 끝점만 t = to) 인지.
            int last = 0;
            for (int i = 0; i <= 6000; i++)
            {
                float t = i * .001f;
                if (t > L) t = L;
                int k = LineStrikeGeometry.SegmentIndexOf(t, L, N);
                LineStrikeGeometry.GetSegmentBounds(k, N, L, out float from, out float to);
                bool inBounds = t >= from - 1e-5f && (t < to + 1e-5f);
                Require(k >= 0 && k < N && k >= last && inBounds, "LS-T2 t = " + t + " must map to exactly one segment (got " + k + ").");
                last = k;
            }
            Require(last == N - 1, "LS-T2 the sweep must end in the last segment.");
        }

        // ── LS-T11 ──────────────────────────────────────────────────────

        /// <summary> LS-T11 — 맵 안 유효 길이 순수 함수(맵 경계 = 축 정렬 사각형). </summary>
        private static void ValidateLineStrikeEffectiveLength()
        {
            // 맵 [-0.5, 10] × [-0.5, 10].
            Require(Near(LineStrikeGeometry.ComputeEffectiveLength(0, 0, 1, 0, 6, -.5f, -.5f, 10, 10), 6f),
                "LS-T11 a line fully inside keeps its length 6.");
            Require(Near(LineStrikeGeometry.ComputeEffectiveLength(0, 0, 1, 0, 6, -.5f, -.5f, 3.5f, 10), 3.5f),
                "LS-T11 a line leaving the map at x = 3.5 is clipped to 3.5.");
            Require(Near(LineStrikeGeometry.ComputeEffectiveLength(10, 5, 1, 0, 6, -.5f, -.5f, 10, 10), 0f),
                "LS-T11 a start on the boundary pointing outward has length 0.");
            Require(Near(LineStrikeGeometry.ComputeEffectiveLength(9.9f, 5, 1, 0, 6, -.5f, -.5f, 10, 10), .1f),
                "LS-T11 a start just inside the boundary keeps only the inside part (0.1).");
            // 대각선 — 방향 (0.6, 0.8): X 벽(3)까지 5, Z 벽(2)까지 2.5 → 먼저 닿는 Z 벽에서 끝.
            Require(Near(LineStrikeGeometry.ComputeEffectiveLength(0, 0, .6f, .8f, 6, -.5f, -.5f, 3, 2), 2.5f),
                "LS-T11 a diagonal line ends at the first wall it reaches (2.5).");
            Require(Near(LineStrikeGeometry.ComputeEffectiveLength(5, 5, -1, 0, 6, -.5f, -.5f, 10, 10), 5.5f),
                "LS-T11 a negative direction is clipped at the minimum wall (5.5).");
            Require(Near(LineStrikeGeometry.ComputeEffectiveLength(5, 5, 0, 1, 6, -.5f, -.5f, 10, 10), 5f),
                "LS-T11 a line parallel to the X walls is limited only by the Z wall (5).");
            Require(Near(LineStrikeGeometry.ComputeEffectiveLength(20, 5, -1, 0, 6, -.5f, -.5f, 10, 10), 0f),
                "LS-T11 a start outside the map has no inside part (0).");
            Require(Near(LineStrikeGeometry.ComputeEffectiveLength(float.NaN, 5, 1, 0, 6, -.5f, -.5f, 10, 10), 0f)
                    && Near(LineStrikeGeometry.ComputeEffectiveLength(5, 5, 1, 0, float.NaN, -.5f, -.5f, 10, 10), 0f),
                "LS-T11 NaN inputs give 0.");
            // 래퍼(Vector3) — 경계 함수가 없으면 원래 길이(잘라내지 않음).
            Require(Near(LineStrikeWorld.ComputeEffectiveLength(Vector3.zero, Vector3.right, 6f, null, out bool known), 6f) && !known,
                "LS-T11 without a bounds provider the original length is used (MapBounds=Missing).");
        }

        private static bool Near(float a, float b) => Math.Abs(a - b) < 1e-4f;

        // ── LS-T3 · LS-T4 ① ③ · LS-T5 ──────────────────────────────────

        /// <summary> LS-T3 — 5회 · 구간 순서 · 0.4 / 0.2초 시각, LS-T4 ① ③ — 시전당 1회 · Id 겹침, LS-T5 — 아군 · 밖 0. </summary>
        private static void ValidateLineStrikeSequenceAndTiming()
        {
            var f = new LineStrikeFixture();
            // 시작 (1, 5) · 방향 +X · 길이 6 → 진행 거리 = x − 1, 구간 k = [1.2k, 1.2(k+1)).
            UnitData e0 = f.Unit(9100, TeamId.Red, 1, 5);    // t 0 → 구간 1
            UnitData e1 = f.Unit(9101, TeamId.Red, 3, 5);    // t 2 → 구간 2
            UnitData e1b = f.Unit(9102, TeamId.Red, 3, 6);   // t 2 · 옆 1(반폭 경계) → 구간 2
            UnitData e2 = f.Unit(9103, TeamId.Red, 4, 5);    // t 3 → 구간 3
            BuildingData b3 = f.Castle(9105, TeamId.Red, 5, 5); // t 4 → 구간 4 (건물)
            UnitData e4 = f.Unit(9105, TeamId.Red, 7, 5);    // t 6(끝점) → 구간 5 — 유닛 Id = 건물 Id(LS-T4 ③)
            UnitData side = f.Unit(9110, TeamId.Red, 3, 7);  // 옆 2 > 반폭 1 → 밖
            UnitData behind = f.Unit(9111, TeamId.Red, 0, 5); // t −1 → 밖
            UnitData beyond = f.Unit(9112, TeamId.Red, 8, 5); // t 7 → 밖
            UnitData ally = f.Unit(9113, TeamId.Blue, 3, 5);  // 아군 → 피해 0
            BuildingData allyB = f.Castle(9114, TeamId.Blue, 4, 4); // 아군 건물 (t 3, 옆 1) → 피해 0

            var confirmed = new List<SkillActivationPresentation>();
            using (GameEvents.OnSkillActivationConfirmed.Subscribe(p => confirmed.Add(p)))
            {
                Require(f.Fire(new Vector3(1f, 0f, 5f), new Vector3(2f, 0f, 0f)), "LS-T3 a valid air strike must activate.");
            }
            Require(f.Activation.GetCooldownRemaining(f.Caster.Id) == LineStrikeCooldown,
                "LS-T3 the cooldown starts at the activation moment (rule 3), not after the strikes.");
            Require(f.Activation.PendingImpactCount == 5 && f.Activation.ActiveLineStrikeCount == 1,
                "LS-T3 one activation schedules exactly 5 strike reservations (got " + f.Activation.PendingImpactCount + ").");
            Require(confirmed.Count == 1 && Near(confirmed[0].AimDirection.x, 1f) && Near(confirmed[0].AimDirection.z, 0f)
                    && confirmed[0].AimDirection.y == 0f,
                "LS-T3 the activation notice carries the normalized domain direction (2, 0, 0) → (1, 0, 0).");

            // 틱마다 새로 깎인 대상을 본다 — 4 · 6 · 8 · 10 · 12번째 틱(0.4 · 0.6 · 0.8 · 1.0 · 1.2초)에 구간 1 → 5.
            var expectedAtTick = new Dictionary<int, IDamageable[]>
            {
                { 4, new IDamageable[] { e0 } },
                { 6, new IDamageable[] { e1, e1b } },
                { 8, new IDamageable[] { e2 } },
                { 10, new IDamageable[] { b3 } },
                { 12, new IDamageable[] { e4 } },
            };
            var all = new IDamageable[] { e0, e1, e1b, e2, b3, e4, side, behind, beyond, ally, allyB };
            var hpBefore = new Dictionary<IDamageable, int>();
            foreach (IDamageable d in all) hpBefore[d] = d.Hp;
            for (int tick = 1; tick <= 14; tick++)
            {
                f.Tick(1);
                expectedAtTick.TryGetValue(tick, out IDamageable[] expected);
                foreach (IDamageable d in all)
                {
                    int drop = hpBefore[d] - d.Hp;
                    bool shouldDrop = expected != null && Array.IndexOf(expected, d) >= 0;
                    Require(drop == (shouldDrop ? LineStrikeDamage : 0),
                        "LS-T3 at tick " + tick + " (" + (tick * 100) + " ms) target " + DescribeLineTarget(d) + " lost " + drop
                        + " (expected " + (shouldDrop ? LineStrikeDamage : 0) + ").");
                    hpBefore[d] = d.Hp;
                }
                int expectedPending = tick < 4 ? 5 : tick < 6 ? 4 : tick < 8 ? 3 : tick < 10 ? 2 : tick < 12 ? 1 : 0;
                Require(f.Activation.PendingImpactCount == expectedPending,
                    "LS-T3 after tick " + tick + " pending strikes must be " + expectedPending + " (got " + f.Activation.PendingImpactCount + ").");
            }

            // LS-T4 ① ③ · LS-T5 — 대상마다 5회 전체에서 정확히 1회, 아군 · 밖은 0.
            Require(LineStrikeLost(e0, 10000) == 150 && LineStrikeLost(e1, 10000) == 150 && LineStrikeLost(e1b, 10000) == 150
                    && LineStrikeLost(e2, 10000) == 150 && LineStrikeLost(e4, 10000) == 150 && LineStrikeLost(b3, 5000) == 150,
                "LS-T4 every target inside the line is hit exactly once in the whole cast (unit id 9105 and building id 9105 counted separately).");
            Require(LineStrikeLost(side, 10000) == 0 && LineStrikeLost(behind, 10000) == 0 && LineStrikeLost(beyond, 10000) == 0,
                "LS-T5 targets beside the line, behind the start and beyond the end take no damage.");
            Require(LineStrikeLost(ally, 10000) == 0 && LineStrikeLost(allyB, 5000) == 0, "LS-T5 allies inside the line take no damage.");

            LineStrikeEndedRecord end = f.Activation.LastLineStrikeEnded;
            Require(f.Activation.ActiveLineStrikeCount == 0 && end.Sequence == confirmed[0].Sequence && end.Reason == "Complete"
                    && end.Done == 5 && end.Skipped == 0 && end.Dropped == 0 && end.UniqueUnits == 5 && end.UniqueBuildings == 1,
                "LS-T3 the cast ends Complete with Done 5 / Skipped 0 / Dropped 0 and 5 unique units + 1 unique building (got "
                + end.Reason + " " + end.Done + "/" + end.Skipped + "/" + end.Dropped + " units " + end.UniqueUnits + " buildings " + end.UniqueBuildings + ").");
        }

        private static string DescribeLineTarget(IDamageable d) =>
            d is UnitData u ? "unit " + u.Id + " @" + u.Position : d is BuildingData b ? "building " + b.Id + " @" + b.Position : "?";

        // ── LS-T4 ② ─────────────────────────────────────────────────────

        /// <summary> LS-T4 ② — 폭격 사이에 다음 구간으로 옮겨 간 유닛도 시전당 1회만. </summary>
        private static void ValidateLineStrikeOncePerCastMoving()
        {
            var f = new LineStrikeFixture();
            UnitData m = f.Unit(9200, TeamId.Red, 1, 5); // 구간 1에서 맞은 뒤 구간 2로 이동
            UnitData p = f.Unit(9201, TeamId.Red, 4, 5); // 구간 3 → (맞기 전에) 구간 4로 이동 → 구간 4에서 1회 → 구간 5로 이동
            Require(f.Fire(new Vector3(1f, 0f, 5f), Vector3.right), "LS-T4 the air strike must activate.");
            f.Tick(4);
            Require(LineStrikeLost(m, 10000) == 150 && LineStrikeLost(p, 10000) == 0, "LS-T4 strike 1 hits only the unit in segment 1.");
            m.Position = new HexCoord(3, 5);  // 다음 폭격(구간 2) 자리로
            p.Position = new HexCoord(5, 5);  // 구간 3 폭격 전에 구간 4로
            f.Tick(6);                        // 1.0초까지 — 구간 2 · 3 · 4
            Require(LineStrikeLost(m, 10000) == 150, "LS-T4 a unit that moved into the next segment is not hit again (once per cast — U-12).");
            Require(LineStrikeLost(p, 10000) == 150, "LS-T4 a unit that moved ahead before its segment is hit once by the later strike.");
            p.Position = new HexCoord(7, 5);  // 마지막 구간으로
            f.Tick(3);
            Require(LineStrikeLost(p, 10000) == 150 && LineStrikeLost(m, 10000) == 150, "LS-T4 the last strike does not hit an already-hit unit.");
            LineStrikeEndedRecord end = f.Activation.LastLineStrikeEnded;
            Require(end.Reason == "Complete" && end.UniqueUnits == 2 && end.Done == 5,
                "LS-T4 the cast counts 2 unique units over 5 strikes (got " + end.UniqueUnits + ").");
        }

        // ── LS-T6 ───────────────────────────────────────────────────────

        /// <summary> LS-T6 — 경기 종료로 남은 폭격 버림 · 닫힌 뒤 새 예약 거부 · 폭격 처리 중 닫힘. </summary>
        private static void ValidateLineStrikeClose()
        {
            // ① 첫 폭격 뒤 경기 종료.
            var f = new LineStrikeFixture();
            UnitData e0 = f.Unit(9300, TeamId.Red, 1, 5);
            UnitData e1 = f.Unit(9301, TeamId.Red, 3, 5);
            UnitData e4 = f.Unit(9302, TeamId.Red, 7, 5);
            Require(f.Fire(new Vector3(1f, 0f, 5f), Vector3.right), "LS-T6 the air strike must activate.");
            f.Tick(4);
            Require(LineStrikeLost(e0, 10000) == 150, "LS-T6 strike 1 lands before the game ends.");
            f.Activation.ClosePendingImpacts();
            LineStrikeEndedRecord end = f.Activation.LastLineStrikeEnded;
            Require(f.Activation.PendingImpactCount == 0 && f.Activation.ActiveLineStrikeCount == 0 && end.Reason == "GameEnd"
                    && end.Done == 1 && end.Skipped == 0 && end.Dropped == 4,
                "LS-T6 game end drops the 4 remaining strikes (Done 1 / Dropped 4 — got " + end.Done + "/" + end.Dropped + ").");
            f.Tick(12);
            Require(LineStrikeLost(e1, 10000) == 0 && LineStrikeLost(e4, 10000) == 0, "LS-T6 no strike lands after the game ended.");

            // 닫힌 뒤 다른 건물의 새 공습 — 예약이 생기지 않는다(쿨다운 흐름은 기존과 같다).
            BuildingData caster2 = f.Buildings.PlaceBuildingWithId(97001, BuildingType.FlightFacility, TeamId.Blue, new HexCoord(0, 4), RaceId.Human);
            Require(caster2 != null && f.Activation.Activate(caster2.Id, 1, new Vector3(1f, 0f, 5f), Vector3.right),
                "LS-T6 the second caster activates (validation is unchanged after close).");
            Require(f.Activation.PendingImpactCount == 0 && f.Activation.ActiveLineStrikeCount == 0,
                "LS-T6 after close a new air strike schedules nothing (LineStrikeRejected Reason=Closed).");
            f.Tick(14);
            Require(LineStrikeLost(e1, 10000) == 0, "LS-T6 the rejected cast deals no damage.");

            // ② 폭격 처리 중 닫힘 — 한 틱(1.25초)에 5회가 모두 다 되고, 2번째 폭격 피해 이벤트 안에서 경기가 끝난다.
            var g = new LineStrikeFixture();
            UnitData g0 = g.Unit(9310, TeamId.Red, 1, 5);
            UnitData g1 = g.Unit(9311, TeamId.Red, 3, 5);
            UnitData g2 = g.Unit(9312, TeamId.Red, 4, 5);
            UnitData g4 = g.Unit(9313, TeamId.Red, 7, 5);
            Require(g.Fire(new Vector3(1f, 0f, 5f), Vector3.right), "LS-T6 ② the air strike must activate.");
            using (GameEvents.OnEntityDamaged.Subscribe(ev =>
                   {
                       if (ev.Entity is UnitData u && u.Id == g1.Id) g.Activation.ClosePendingImpacts();
                   }))
            {
                g.Tick(1, 1.25f);
            }
            LineStrikeEndedRecord gEnd = g.Activation.LastLineStrikeEnded;
            Require(LineStrikeLost(g0, 10000) == 150 && LineStrikeLost(g1, 10000) == 150
                    && LineStrikeLost(g2, 10000) == 0 && LineStrikeLost(g4, 10000) == 0,
                "LS-T6 ② strikes after the one that ended the game are dropped in the same tick.");
            Require(gEnd.Reason == "GameEnd" && gEnd.Done == 2 && gEnd.Dropped == 3 && g.Activation.ActiveLineStrikeCount == 0
                    && g.Activation.PendingImpactCount == 0,
                "LS-T6 ② the cast is closed after the in-progress strike (Done 2 / Dropped 3 — got " + gEnd.Done + "/" + gEnd.Dropped + ").");
        }

        // ── LS-T7 ───────────────────────────────────────────────────────

        /// <summary> LS-T7 — 잘못된 방향 거부(쿨다운 없음) · 정규화 · 발동 알림 방향 정리. </summary>
        private static void ValidateLineStrikeDirectionValidation()
        {
            var f = new LineStrikeFixture();
            Vector3 start = new Vector3(1f, 0f, 5f);
            Vector3[] invalid =
            {
                Vector3.zero, new Vector3(float.NaN, 0f, 1f), new Vector3(float.PositiveInfinity, 0f, 0f),
                new Vector3(1e-6f, 0f, 0f), new Vector3(0f, 5f, 0f)
            };
            foreach (Vector3 dir in invalid)
            {
                Require(!f.Fire(start, dir), "LS-T7 direction " + dir + " must be rejected.");
                Require(f.Activation.GetCooldownRemaining(f.Caster.Id) == 0f && f.Activation.PendingImpactCount == 0
                        && f.Activation.ActiveLineStrikeCount == 0,
                    "LS-T7 a rejected direction starts no cooldown and schedules nothing (" + dir + ").");
            }
            Require(!f.Activation.Activate(f.Caster.Id, 1, null, Vector3.right), "LS-T7 a point skill without an aim point is still rejected.");
            Require(!f.Fire(new Vector3(30f, 0f, 5f), Vector3.right), "LS-T7 a start point outside the map is still rejected (rule 26).");

            var confirmed = new List<SkillActivationPresentation>();
            using (GameEvents.OnSkillActivationConfirmed.Subscribe(p => confirmed.Add(p)))
            {
                Require(f.Fire(start, new Vector3(3f, 7f, 4f)), "LS-T7 a non-normalized direction (3, y, 4) is accepted.");
            }
            Require(confirmed.Count == 1 && Near(confirmed[0].AimDirection.x, .6f) && Near(confirmed[0].AimDirection.z, .8f)
                    && confirmed[0].AimDirection.y == 0f,
                "LS-T7 the server normalizes (3, y, 4) to (0.6, 0, 0.8) and ignores Y.");

            // 발동 알림 값 객체의 전송 방어 — 조준 없음 · NaN · Y.
            var noAim = new SkillActivationPresentation(1, TeamId.Blue, 1, false, Vector3.zero, 1, 0f, Vector3.right);
            var nan = new SkillActivationPresentation(1, TeamId.Blue, 1, true, Vector3.zero, 1, 0f, new Vector3(float.NaN, 0f, 1f));
            var withY = new SkillActivationPresentation(1, TeamId.Blue, 1, true, Vector3.zero, 1, 0f, new Vector3(.6f, 9f, .8f));
            var legacy = new SkillActivationPresentation(1, TeamId.Blue, 1, true, Vector3.zero, 1, 0f);
            Require(noAim.AimDirection == Vector3.zero && nan.AimDirection == Vector3.zero && legacy.AimDirection == Vector3.zero
                    && withY.AimDirection.y == 0f && Near(withY.AimDirection.x, .6f),
                "LS-T7 the activation notice keeps a direction only with an aim and finite X / Z (Y dropped, old call = zero).");
        }

        // ── LS-T12 ──────────────────────────────────────────────────────

        /// <summary> LS-T12 — 유효 길이 밖 폭격 건너뜀 · 걸친 구간은 맵 안 부분만 · 맵 밖 대상 0. </summary>
        private static void ValidateLineStrikeOutOfMapStrikes()
        {
            var f = new LineStrikeFixture();
            f.BoundsMax = new Vector3(3.5f, 0f, 20.5f); // 시작 x 1 → 유효 길이 2.5 (L 6의 절반 미만)
            UnitData e0 = f.Unit(9400, TeamId.Red, 1, 5); // t 0 · 구간 1
            UnitData e1 = f.Unit(9401, TeamId.Red, 3, 5); // t 2 · 구간 2 · 맵 안
            UnitData e2 = f.Unit(9402, TeamId.Red, 4, 5); // t 3 · 구간 3(일부만 맵 안 [2.4, 2.5]) · 맵 밖 → 0
            UnitData e3 = f.Unit(9403, TeamId.Red, 5, 5); // t 4 · 구간 4 → 건너뜀
            UnitData e4 = f.Unit(9404, TeamId.Red, 7, 5); // t 6 · 구간 5 → 건너뜀
            Require(f.Fire(new Vector3(1f, 0f, 5f), Vector3.right), "LS-T12 an air strike leaving the map is still fired (U-14).");
            Require(f.Activation.PendingImpactCount == 5, "LS-T12 all 5 reservations are made; out-of-map ones are skipped when due.");
            f.Tick(13);
            Require(LineStrikeLost(e0, 10000) == 150 && LineStrikeLost(e1, 10000) == 150, "LS-T12 strikes inside the map still hit.");
            Require(LineStrikeLost(e2, 10000) == 0 && LineStrikeLost(e3, 10000) == 0 && LineStrikeLost(e4, 10000) == 0,
                "LS-T12 targets past the effective length take no damage.");
            LineStrikeEndedRecord end = f.Activation.LastLineStrikeEnded;
            Require(end.Reason == "Complete" && end.Done == 3 && end.Skipped == 2 && end.Dropped == 0,
                "LS-T12 the partially-inside segment 3 is judged, segments 4 · 5 are skipped (Done 3 / Skipped 2 — got " + end.Done + "/" + end.Skipped + ").");
        }

        // ── LS-T10 ──────────────────────────────────────────────────────

        /// <summary> LS-T10 — 원형 스킬(포격) 착탄 예약 · 피해 무변경, 방향 인자를 생략해도 발동. </summary>
        private static void ValidateLineStrikeBombardmentUnchanged()
        {
            var f = new LineStrikeFixture();
            UnitData near = f.Unit(9500, TeamId.Red, 1, 5);
            UnitData far = f.Unit(9501, TeamId.Red, 6, 5);
            var confirmed = new List<SkillActivationPresentation>();
            using (GameEvents.OnSkillActivationConfirmed.Subscribe(p => confirmed.Add(p)))
            {
                // 옛 호출 모양(방향 인자 생략) 그대로 — 포격은 방향을 쓰지 않는다.
                Require(f.Activation.Activate(f.Caster.Id, 0, new Vector3(1f, 0f, 5f)), "LS-T10 Bombardment activates without a direction.");
            }
            Require(f.Activation.PendingImpactCount == 1 && f.Activation.ActiveLineStrikeCount == 0,
                "LS-T10 Bombardment schedules exactly one circle impact (not a line strike).");
            Require(confirmed.Count == 1 && confirmed[0].AimDirection == Vector3.zero, "LS-T10 Bombardment's activation notice has no direction.");
            f.Tick(3);
            Require(LineStrikeLost(near, 10000) == 0, "LS-T10 no Bombardment damage before 0.4 s.");
            f.Tick(2); // 0.5초 — 기존 동작 그대로(0.4초 지연은 0.1초 틱에서 float 오차로 다섯 번째 틱에 떨어진다 — 이 Task가 바꾸지 않는다)
            Require(LineStrikeLost(near, 10000) == 150 && LineStrikeLost(far, 10000) == 0 && f.Activation.PendingImpactCount == 0,
                "LS-T10 Bombardment hits the target in the circle once by 0.5 s and nothing outside.");
            f.Tick(5);
            Require(LineStrikeLost(near, 10000) == 150, "LS-T10 Bombardment hits only once.");
        }

        // ── LS-T8 ───────────────────────────────────────────────────────

        /// <summary> LS-T8 — 스키마 11 · RPC 방향 인자 · 서버 재검증 · 배선 소스 게이트 · 메커니즘 5 · 자산 칸 초기값. </summary>
        private static void ValidateLineStrikeSourceGates()
        {
            Require(UnitAttackShadowProfileResolver.PresentationContractSchemaRevision == 11,
                "LS-T8 the air-strike direction arguments must raise the presentation schema to 11.");
            Require((int)SkillMechanicType.LineStrike == 5 && new SkillExecutorRegistry().TryGet(SkillMechanicType.LineStrike) is LineStrikeExecutor,
                "LS-T8 SkillMechanicType.LineStrike must be 5 (appended last) and registered to LineStrikeExecutor.");

            const string root = "Assets/_Project/Scripts/";
            string net = File.ReadAllText(root + "Infrastructure/Network/NetworkSkillController.cs");
            Require(SupportActiveLineCount(net, "Vector3 aimDirection, ServerRpcParams rpcParams = default)") == 1
                    && SupportActiveLineCount(net, "int sequence, float impactDelay, float cooldown, Vector3 aimDirection)") == 1
                    && SupportActiveLineCount(net, "skill.Activate(buildingId, skillSlot, aim, aimDirection);") == 1
                    && SupportActiveLineCount(net, "p.Sequence, p.ImpactDelay, cooldown, p.AimDirection);") == 1
                    && SupportActiveLineCount(net, "RequestActivateSkillServerRpc(buildingId, skillSlot, aimWorld, hasAim, aimDirection);") == 1,
                "LS-T8 the request and activation RPCs must carry the aim direction end to end (P-4).");
            string skill = File.ReadAllText(root + "Application/UseCases/SkillActivationUseCase.cs");
            Require(SupportActiveLineCount(skill, "LogActivationRejected(building, skillSlot, skill, \"InvalidDirection\");") == 1
                    && SupportActiveLineCount(skill, "if (skill.Mechanic == SkillMechanicType.LineStrike)") == 1,
                "LS-T8 Activate must re-validate the direction for LineStrike only (P-11).");
            string setup = File.ReadAllText(root + "Bootstrap/GameBootstrapper.Setup.cs");
            Require(setup.Contains("_skillActivation.SetMapWorldBoundsProvider(") && setup.Contains("HexMetrics.ComputeMapWorldBounds(_grid.Width, _grid.Height, out boundsMin, out boundsMax);"),
                "LS-T8 the composition root must inject the map bounds used for clipping (U-14).");

            // 자산 칸 초기값(P-7 임시값) — 칸이 없는 자산은 이 값으로 읽힌다.
            var def = ScriptableObject.CreateInstance<SkillDefinition>();
            try
            {
                SkillData d = def.ToSkillData();
                Require(Near(d.LineLength, 6f) && Near(d.LineWidth, 2f) && d.LineStrikeCount == 5 && Near(d.LineStrikeInterval, .2f),
                    "LS-T8 SkillDefinition line defaults must be the P-7 temporary values (6 / 2 / 5 / 0.2).");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(def);
            }
            var bad = new SkillData(null, SkillAimType.PointTarget, 1f, SkillMechanicType.LineStrike, 0f, 0f, 1, 0f, 0, 0f, false, "", "", true, 0f,
                null, 2f, lineLength: float.NaN, lineWidth: -1f, lineStrikeCount: 999, lineStrikeInterval: float.PositiveInfinity);
            Require(bad.LineLength == 0f && bad.LineWidth == 0f && bad.LineStrikeCount == SkillData.MaxLineStrikeCount && bad.LineStrikeInterval == 0f,
                "LS-T8 SkillData must sanitize bad line values (NaN / negative → 0, count capped at " + SkillData.MaxLineStrikeCount + ").");
        }

        // ── LS-T9 (L-6 자산 켜기 · 셋업 기대 수 13 — 2026-11-18 메테오로 12 → 13) + L-5 VFX 칸 · 재생 경로 게이트 ─────────────

        private const string AirStrikeAssetPath = "Assets/_Project/Resources/Config/Skills/Skill_Human_AirStrike.asset";

        /// <summary>
        /// LS-T9 — 공습 자산이 Plan 값으로 켜졌는지(사용 가능 · 지점 지정 · 메커니즘 5 · P-7 임시 수치) · 인간 로드아웃 슬롯 1 ·
        /// 셋업 도구 기대 수 13(2026-11-18 메테오로 12 → 13) · 셋업 표의 공습 줄. 덧붙여 L-5(VFX 칸 초기값 · 칸에 든 프리팹의 1회 재생 계약 · 재생 경로 소스 게이트)도 본다.
        /// </summary>
        private static void ValidateLineStrikeAssetEnabled()
        {
            // ① 셋업 도구 기대 수 13(다른 스위트 5파일의 같은 검사와 같은 값 — 표와 개수가 어긋나면 도구가 쓰기 전에 멈춘다).
            //   *(2026-11-18 메테오 — 직전 「기대 수 12(다른 스위트 4파일 …)」. 메테오를 켜 13이 됐고 메테오 스위트 M-T8이 같은 검사를 더해 5파일)*
            FieldInfo expected = typeof(Hexiege.EditorTools.SupportSkillLoadoutSetup).GetField("ExpectedAvailableCount",
                BindingFlags.NonPublic | BindingFlags.Static);
            // [2026-11-18 메테오 Task S2-4] 12 → 13 — 메테오(타입 A · 착탄 지연 · 착탄 화상)를 「사용 가능」으로 켰다.
            // [2026-11-23 연막탄 Task S-1] 13 → 14 — 연막탄(지속 상태 구역 · 아군 대상 · 회피 확률)을 「사용 가능」으로 켰다. 옛 검사:
            // Require(expected != null && (int)expected.GetRawConstantValue() == 13,
            //     "LS-T9 SupportSkillLoadoutSetup.ExpectedAvailableCount must be 13 (Air Strike enabled, then Meteor).");
            Require(expected != null && (int)expected.GetRawConstantValue() == 14,
                "LS-T9 SupportSkillLoadoutSetup.ExpectedAvailableCount must be 14 (Air Strike enabled, then Meteor, then Smoke Bomb).");

            // ② 자산 값(Plan P-7 · L-L9 — 시험용 임시값). 칸이 없으면 SkillDefinition 초기값으로 읽히므로 ToSkillData 결과로 본다.
            SkillDefinition def = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDefinition>(AirStrikeAssetPath);
            Require(def != null, "LS-T9 Skill_Human_AirStrike asset must exist: " + AirStrikeAssetPath);
            SkillData air = def.ToSkillData();
            Require(air.IsAvailable && air.AimType == SkillAimType.PointTarget && air.Mechanic == SkillMechanicType.LineStrike,
                "LS-T9 Air Strike must be available / point target / mechanic 5 (LineStrike).");
            Require(Near(air.LineLength, 6f) && Near(air.LineWidth, 2f) && air.LineStrikeCount == 5 && Near(air.LineStrikeInterval, .2f)
                    && Near(air.ImpactDelay, .4f) && air.TotalDamage == 150 && Near(air.Cooldown, 10f),
                "LS-T9 Air Strike values must be the P-7 temporary values (length 6 / width 2 / 5 strikes / 0.2 s / first delay 0.4 / 150 / cooldown 10).");

            // ③ 인간 로드아웃 슬롯 1(버튼 2)이 이 자산이어야 한다(스킬 규칙 9 — 셋업 도구 h, 1).
            SkillLoadoutConfig loadout = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillLoadoutConfig>(
                "Assets/_Project/Resources/Config/Skills/SkillLoadoutConfig.asset");
            Require(loadout != null, "LS-T9 SkillLoadoutConfig asset must exist.");
            loadout.ClearCache();
            var human = loadout.GetLoadout(RaceId.Human);
            Require(human != null && human.Count > 1 && human[1] != null && human[1].DisplayName == air.DisplayName
                    && human[1].IsAvailable && human[1].Mechanic == SkillMechanicType.LineStrike,
                "LS-T9 Air Strike must be the available LineStrike skill in Human slot 1 (button 2).");

            // ④ 셋업 표 — 공습 줄이 자리표시(PointPlaceholder)가 아니라 LineStrike 값이어야 도구를 다시 돌려도 꺼지지 않는다.
            string setup = File.ReadAllText("Assets/Editor/Setup/SupportSkillLoadoutSetup.cs");
            Require(SupportActiveLineCount(setup, "PointPlaceholder(\"Skill_Human_AirStrike\"") == 0
                    && SupportActiveLineCount(setup, "Mechanic = SkillMechanicType.LineStrike,") == 1
                    && SupportActiveLineCount(setup, "airStrike,") == 1,
                "LS-T9 the setup table must list Air Strike as an available LineStrike spec, not the old placeholder.");

            // ⑤ [L-5] VFX 칸 — 초기값(기준 길이 6 · 폭격 프리팹 없음)과 값 정리(잘못된 기준 길이 → 0 = 배율 1).
            var probe = ScriptableObject.CreateInstance<SkillDefinition>();
            try
            {
                SkillData d = probe.ToSkillData();
                Require(Near(d.LineVfxReferenceLength, 6f) && d.StrikeVfxPrefab == null,
                    "LS-T9 L-5 SkillDefinition air-strike VFX defaults must be reference length 6 and no strike prefab.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
            var badRef = new SkillData(null, SkillAimType.PointTarget, 1f, SkillMechanicType.LineStrike, 0f, 0f, 1, 0f, 0, 0f, false, "", "", true,
                0f, null, 2f, lineVfxReferenceLength: float.NaN);
            Require(badRef.LineVfxReferenceLength == 0f && EffectManagerScaleIsOne(3f, badRef.LineVfxReferenceLength),
                "LS-T9 L-5 a bad line VFX reference length must sanitize to 0 and play at length scale 1.");
            Require(Near(Hexiege.Presentation.EffectManager.ComputeSkillVfxScale(3f, 6f), .5f),
                "LS-T9 L-5 the warning length scale is effective length ÷ reference length (3 ÷ 6 = 0.5).");

            // ⑥ [L-5] 칸에 프리팹이 들어 있으면(테스트용 임시 프리팹 — M-2) 1회 재생 계약: 반복 없음 · Hierarchy 배율. 빈 칸은 통과.
            foreach (GameObject prefab in new[] { air.ActivationVfxPrefab, air.StrikeVfxPrefab })
            {
                if (prefab == null) continue; // 빈 칸은 통과(테스트 뒤 비운다 — M-2).
                foreach (ParticleSystem ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
                {
                    Require(!ps.main.loop,
                        "LS-T9 L-5 air-strike VFX " + prefab.name + "/" + ps.name + " must not loop (played once, then returned to the pool).");
                    Require(ps.main.scalingMode == ParticleSystemScalingMode.Hierarchy,
                        "LS-T9 L-5 air-strike VFX " + prefab.name + "/" + ps.name + " must use Hierarchy scaling (root scale reaches the particles).");
                }
            }

            // ⑦ [L-5] 재생 경로 소스 게이트 — 공습 분기가 원형 「프리팹 없음」 검사보다 앞 · 유효 길이 · 맵 밖 구간 · 회전 재생 · 배선.
            const string root = "Assets/_Project/Scripts/";
            string presenter = File.ReadAllText(root + "Presentation/Effects/SkillActivationVfxPresenter.cs");
            int branch = presenter.IndexOf("if (skill != null && skill.Mechanic == SkillMechanicType.LineStrike)", StringComparison.Ordinal);
            int noPrefab = presenter.IndexOf("if (skill == null || skill.ActivationVfxPrefab == null)", StringComparison.Ordinal);
            Require(branch >= 0 && noPrefab > branch && SupportActiveLineCount(presenter, "PlayLineStrike(p, channel, skill);") == 1,
                "LS-T9 L-5 the LineStrike branch must run before the circle NoPrefab check (strikes play even with an empty warning slot).");
            string line = File.ReadAllText(root + "Presentation/Effects/SkillActivationVfxPresenter.LineStrike.cs");
            Require(SupportActiveLineCount(line, "LineStrikeWorld.ComputeEffectiveLength(start, dir, length, _lineMapBounds, out bool boundsKnown);") == 1
                    && SupportActiveLineCount(line, "if (!LineStrikeGeometry.IsStrikeInRange(k, count, length, eff))") == 1
                    && SupportActiveLineCount(line, "_effects.PlaySkillVfxOriented(") == 2
                    && SupportActiveLineCount(line, "GameEvents.OnGameEnd.Subscribe(") == 1,
                "LS-T9 L-5 the presenter must clip with the shared effective-length function, skip out-of-map strikes, play oriented and drop on game end.");
            string map = File.ReadAllText(root + "Bootstrap/GameBootstrapper.Map.cs");
            Require(SupportActiveLineCount(map, "skillVfxPresenter.SetMapWorldBoundsProvider(") == 1,
                "LS-T9 L-5 the composition root must inject the map bounds into the activation VFX presenter.");
            string effects = File.ReadAllText(root + "Presentation/Effects/EffectManager.cs");
            Require(SupportActiveLineCount(effects, "public bool PlaySkillVfxOriented(") == 1
                    && SupportActiveLineCount(effects, "public bool PlaySkillActivationVfx(GameObject prefab, Vector3 viewPosition, float uniformScale)") == 1,
                "LS-T9 L-5 EffectManager must add the oriented player and keep the old circle player.");
        }

        // ComputeSkillVfxScale(길이, 기준 길이)가 1인지(기준 길이 0 = 배율 1 규칙 확인용).
        private static bool EffectManagerScaleIsOne(float length, float referenceLength) =>
            Near(Hexiege.Presentation.EffectManager.ComputeSkillVfxScale(length, referenceLength), 1f);
    }
}
