// ============================================================================
// RunUnitActionSelfValidation.SkillZones.cs
// [2026-11-11 지원 스킬 2-Z 지속 상태 구역(서리 감옥 · 수액 늪)] 회귀 검증(fixture).
// Task: Assets/_Project/Docs/_Tasks/2026-11-10/16_02_support-skill-persistent-zones/ (Plan 구현 단위 ⑤ · Research 12-3 후보).
// Run()(메뉴 「Hexiege/Combat/Run Full Regression Suite」)이 함께 돌린다.
//
// [배경 — 초급 개발자용 설명]
//   서리 감옥 · 수액 늪은 "구역이 남아 있는 동안, 구역 안 유닛에게만" 효과가 걸린다. 이 규칙(들어오면 걸림 · 나가면 풀림 ·
//   끝나면 풀림 · 같은 스킬 겹침 1번 · 다른 출처는 곱)이 깨지면 눈으로는 알아차리기 어렵다. 그래서 실제 구역 서비스와
//   실제 전투 UseCase 접근자로 자동 확인한다.
//
// 무엇을 확인하는가(Research 12-3 후보 → 검사 번호):
//   Z-T1 구역 없음 회귀 — 구역 서비스가 있어도 구역이 없으면 이동 배율 · 공격 가능이 기존과 정확히 같다.
//   Z-T2 타일 진입 · 이탈 — 발동 순간 안 유닛은 바로 걸리고, 나중에 들어온 유닛도 걸리고, 나가면 정확히 1.0으로 돌아온다.
//        구성원 알림: 첫 구성원은 다음 틱에 Entered로 · 들어옴 · 나감이 바뀐 틱에만 · 아군은 대상이 아니다.
//   Z-T3 빙결 = 이동 0 · 공격 불가 · 만료 뒤 정확히 1.0 · 공격 가능.
//   Z-T4 같은 스킬(같은 팀 · 같은 슬롯) 겹침 = 효과 1번(0.4, 0.16 아님) · 다른 스킬 같은 종류는 곱(Z-5 · Z-6).
//   Z-T5 순풍(상태 1.3) × 수액(구역 0.4) = 0.52, 늪을 나가면 1.3 그대로(순풍 기록이 지워지지 않는다 — Z1 버그 없음).
//   Z-T6 사망 정리 — 사망 통보 · 틱 감지 모두 구성원에서 빠지고 다음 틱에 Exited로 알린다.
//   Z-T7 경기 종료 Close — 전부 해제 · 이후 생성 거부. 시전 건물 Id는 구역 판정에 쓰지 않는다(Z-7 — 시간까지 유지).
//   Z-T8 실행기 — 조준 · 지속 · 반경 · 종류 조건을 지키고, 맞으면 컨텍스트 구역 다리를 자산 값으로 부른다.
//   Z-T9 두 자산 계약(Z-10 임시값 · 사용 가능 · 타입 4 · 지점 조준 · 적 대상 · 피해 0 · 로드아웃 슬롯) · 연막탄은 여전히 사용 불가 ·
//        채워진 VFX 칸은 상태 이펙트 계약(Looping · Stop Action None · Hierarchy).
//   Z-T10 스키마 == 9 · 셋업 도구 기대 수 == 8 · 소스 게이트(틱 자리 · 구독 · RPC).
//
// 실제 씬 · 멀티 전송 · 화면 이펙트 모양은 여기서 확인하지 않는다(사용자 실기 항목 — Plan 6절).
// 모든 fixture는 새 인스턴스를 만들고, 이벤트 구독은 finally에서 해제한다(다른 검사에 영향 없음).
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
        private const string FrostPrisonAssetPath = "Assets/_Project/Resources/Config/Skills/Skill_Spirit_FrostPrison.asset";
        private const string SapSwampAssetPath = "Assets/_Project/Resources/Config/Skills/Skill_Trans_SapSwamp.asset";
        private const string SmokeBombAssetPath = "Assets/_Project/Resources/Config/Skills/Skill_Human_SmokeBomb.asset";

        /// <summary>
        /// 지속 상태 구역 회귀 검증 진입점(Z-T1 ~ Z-T10).
        /// </summary>
        private static void ValidateSkillZones()
        {
            Require(!UnityEngine.Application.isPlaying, "Skill zone fixture must run outside a live match.");
            ValidateSkillZoneNoZoneRegression();   // Z-T1
            ValidateSkillZoneEntryExit();          // Z-T2
            ValidateSkillZoneFreeze();             // Z-T3
            ValidateSkillZoneOverlap();            // Z-T4
            ValidateSkillZoneTailwindProduct();    // Z-T5
            ValidateSkillZoneDeath();              // Z-T6
            ValidateSkillZoneClose();              // Z-T7
            ValidateSkillZoneExecutor();           // Z-T8
            ValidateSkillZoneAssets();             // Z-T9
            ValidateSkillZoneSourceGates();        // Z-T10
            Debug.Log("[UAS-DIAG][SKILL-ZONE] self-validation PASS: no-zone regression exact (Z-T1), tile-centre entry/late entry/exit restore 1.0 with next-tick membership announcements and allies untouched (Z-T2), freeze = move 0 + no attack then exact restore (Z-T3), same-skill overlap applied once and different skills multiply (Z-T4), tailwind 1.3 × sap 0.4 = 0.52 and tailwind kept after exit (Z-T5), death removes membership and announces exit (Z-T6), Close releases all and rejects new zones while the caster building is never consulted (Z-T7), executor guards and bridge values (Z-T8), Frost Prison / Sap Swamp asset contract and Smoke Bomb still unavailable (Z-T9), schema 9 + setup expected count 8 + tick/subscription/RPC source gates (Z-T10).");
        }

        // 구역 fixture — 새 유닛 목록 · 전투 UseCase · 상태 시스템 · 구역 서비스 + 구성원 알림 수집.
        private sealed class SkillZoneFixture : IDisposable
        {
            public readonly UnitSpawnUseCase Spawn;
            public readonly UnitCombatUseCase Combat;
            public readonly StatusEffectSystem Status;
            public readonly SkillZoneSystem Zones;
            public readonly List<SkillZoneMembershipPresentation> Events = new List<SkillZoneMembershipPresentation>();
            private readonly IDisposable _subscription;

            public SkillZoneFixture()
            {
                var grid = new HexGrid(3, 3, HexOrientation.FlatTop);
                Spawn = new UnitSpawnUseCase(grid);
                var buildings = new BuildingPlacementUseCase(grid);
                var mapper = new ProjectileFixtureMapper(); // 타일 (q, r) → 월드 (q, 0, r)
                Combat = new UnitCombatUseCase(grid, Spawn, buildings, new ProjectileFixturePositions(), mapper);
                Status = new StatusEffectSystem();
                Combat.SetStatusEffectSystem(Status);
                Zones = new SkillZoneSystem(Spawn, mapper, buildings);
                Combat.SetSkillZoneSystem(Zones);
                _subscription = GameEvents.OnSkillZoneMembershipConfirmed.Subscribe(p => { Events.Add(p); });
            }

            // 3×3 그리드 안 타일에 만든 뒤, 구역 판정용 좌표로 옮긴다(판정은 Position만 읽는다 — 매퍼가 그대로 월드로 바꾼다).
            public UnitData Unit(int id, TeamId team, HexCoord spawnTile, HexCoord position)
            {
                UnitData unit = Spawn.SpawnUnitWithId(id, UnitType.Pistoleer, team, spawnTile);
                Require(unit != null, "Z-T0 skill zone fixture unit must spawn: " + id);
                unit.Position = position;
                return unit;
            }

            public void Dispose() => _subscription?.Dispose();
        }

        private static bool ZoneEventHas(SkillZoneMembershipPresentation p, int unitId, bool entered)
        {
            IReadOnlyList<int> ids = entered ? p.EnteredIds : p.ExitedIds;
            for (int i = 0; i < ids.Count; i++) if (ids[i] == unitId) return true;
            return false;
        }

        /// <summary> Z-T1 — 구역이 없으면 접근자 결과가 기존과 정확히 같다. </summary>
        private static void ValidateSkillZoneNoZoneRegression()
        {
            using (var f = new SkillZoneFixture())
            {
                UnitData red = f.Unit(9900, TeamId.Red, new HexCoord(0, 0), new HexCoord(0, 0));
                Require(f.Combat.GetUnitMoveSpeedMultiplier(red) == 1f && f.Combat.CanAttack(red),
                    "Z-T1 with the zone service but no zone the move multiplier must be exactly 1 and attack allowed.");
                Require(f.Zones.GetMoveSpeedMultiplier(red.Id) == 1f && f.Zones.CanAttack(red.Id) && f.Zones.ZoneCount == 0,
                    "Z-T1 an empty zone service must answer 1 / true without any zone.");
                f.Zones.Tick(0.1f);
                Require(f.Events.Count == 0, "Z-T1 ticking without zones must not announce anything.");
            }
        }

        /// <summary> Z-T2 — 타일 중심 기준 진입 · 늦은 진입 · 이탈과 구성원 알림. </summary>
        private static void ValidateSkillZoneEntryExit()
        {
            using (var f = new SkillZoneFixture())
            {
                var center = new Vector3(10f, 0f, 10f);
                UnitData inside = f.Unit(9910, TeamId.Red, new HexCoord(0, 0), new HexCoord(11, 10));   // 거리 1
                UnitData outside = f.Unit(9911, TeamId.Red, new HexCoord(1, 0), new HexCoord(13, 10));  // 거리 3
                UnitData ally = f.Unit(9912, TeamId.Blue, new HexCoord(2, 0), new HexCoord(10, 10));    // 시전 팀(아군)

                int initial = f.Zones.CreateZone(101, TeamId.Blue, 0, 77, center, 1.5f, 6f, StatusEffectKind.MoveSpeedMul, 0.4f, false);
                Require(initial == 1, "Z-T2 only the enemy unit whose tile centre is inside the radius is an initial member.");
                Require(Mathf.Abs(f.Combat.GetUnitMoveSpeedMultiplier(inside) - 0.4f) < 1e-5f,
                    "Z-T2 a unit inside at activation is slowed immediately (×0.4).");
                Require(f.Combat.GetUnitMoveSpeedMultiplier(outside) == 1f && f.Combat.GetUnitMoveSpeedMultiplier(ally) == 1f,
                    "Z-T2 a unit outside the radius and the caster's own unit stay exactly 1.");
                Require(f.Events.Count == 0, "Z-T2 the first members are announced on the next tick, not at creation (R-10 order).");

                f.Zones.Tick(0.1f);
                Require(f.Events.Count == 1 && f.Events[0].Sequence == 101 && ZoneEventHas(f.Events[0], inside.Id, true)
                        && f.Events[0].EnteredCount == 1 && f.Events[0].ExitedCount == 0,
                    "Z-T2 the first tick announces the initial member as Entered.");

                f.Zones.Tick(0.1f);
                Require(f.Events.Count == 1, "Z-T2 a tick without membership change must not announce.");

                outside.Position = new HexCoord(11, 11); // 거리 √2 ≈ 1.41 < 1.5 — 늦게 들어옴(상위 결정 4)
                f.Zones.Tick(0.1f);
                Require(f.Events.Count == 2 && ZoneEventHas(f.Events[1], outside.Id, true)
                        && Mathf.Abs(f.Combat.GetUnitMoveSpeedMultiplier(outside) - 0.4f) < 1e-5f,
                    "Z-T2 a unit walking in later is affected and announced as Entered.");

                inside.Position = new HexCoord(13, 10); // 나감
                f.Zones.Tick(0.1f);
                Require(f.Events.Count == 3 && ZoneEventHas(f.Events[2], inside.Id, false) && f.Events[2].EnteredCount == 0,
                    "Z-T2 a unit walking out is announced as Exited.");
                Require(f.Combat.GetUnitMoveSpeedMultiplier(inside) == 1f && f.Combat.CanAttack(inside),
                    "Z-T2 after leaving the zone the move multiplier is exactly 1 again.");
            }
        }

        /// <summary> Z-T3 — 빙결 구역: 이동 0 · 공격 불가 · 만료 뒤 정확히 원래대로. </summary>
        private static void ValidateSkillZoneFreeze()
        {
            using (var f = new SkillZoneFixture())
            {
                UnitData red = f.Unit(9920, TeamId.Red, new HexCoord(0, 0), new HexCoord(5, 5));
                f.Zones.CreateZone(201, TeamId.Blue, 1, 0, new Vector3(5f, 0f, 5f), 2f, 4f, StatusEffectKind.Freeze, 0f, false);
                Require(f.Combat.GetUnitMoveSpeedMultiplier(red) == 0f && !f.Combat.CanAttack(red),
                    "Z-T3 a frozen member must have move multiplier 0 and must not attack.");
                f.Zones.Tick(3.9f);
                Require(f.Zones.ZoneCount == 1 && !f.Combat.CanAttack(red), "Z-T3 the zone must last its full duration (4 s).");
                f.Zones.Tick(0.2f);
                Require(f.Zones.ZoneCount == 0 && f.Combat.GetUnitMoveSpeedMultiplier(red) == 1f && f.Combat.CanAttack(red),
                    "Z-T3 after expiry the unit must move at exactly 1 and attack again.");
            }
        }

        /// <summary> Z-T4 — 같은 스킬 겹침은 1번, 다른 스킬 같은 종류는 곱. </summary>
        private static void ValidateSkillZoneOverlap()
        {
            using (var f = new SkillZoneFixture())
            {
                UnitData red = f.Unit(9930, TeamId.Red, new HexCoord(0, 0), new HexCoord(5, 5));
                var c = new Vector3(5f, 0f, 5f);
                f.Zones.CreateZone(301, TeamId.Blue, 0, 1, c, 2f, 6f, StatusEffectKind.MoveSpeedMul, 0.4f, false);
                f.Zones.CreateZone(302, TeamId.Blue, 0, 2, c, 2f, 6f, StatusEffectKind.MoveSpeedMul, 0.4f, false);
                Require(Mathf.Abs(f.Combat.GetUnitMoveSpeedMultiplier(red) - 0.4f) < 1e-5f,
                    "Z-T4 two overlapping zones of the same skill (team + slot) must apply once (×0.4, not ×0.16).");
                f.Zones.CreateZone(303, TeamId.Blue, 3, 3, c, 2f, 6f, StatusEffectKind.MoveSpeedMul, 0.5f, false);
                Require(Mathf.Abs(f.Combat.GetUnitMoveSpeedMultiplier(red) - 0.2f) < 1e-5f,
                    "Z-T4 a different skill of the same kind multiplies (0.4 × 0.5 = 0.2 — Z-6).");
            }
        }

        /// <summary> Z-T5 — 순풍(상태) × 수액(구역) 곱, 나간 뒤 순풍 유지. </summary>
        private static void ValidateSkillZoneTailwindProduct()
        {
            using (var f = new SkillZoneFixture())
            {
                UnitData red = f.Unit(9940, TeamId.Red, new HexCoord(0, 0), new HexCoord(5, 5));
                f.Status.Apply(red.Id, new StatusEffect(StatusEffectKind.MoveSpeedMul, 1.3f, 8f, TeamId.Red));
                f.Zones.CreateZone(401, TeamId.Blue, 0, 0, new Vector3(5f, 0f, 5f), 2f, 6f, StatusEffectKind.MoveSpeedMul, 0.4f, false);
                Require(Mathf.Abs(f.Combat.GetUnitMoveSpeedMultiplier(red) - 0.52f) < 1e-5f,
                    "Z-T5 tailwind 1.3 × sap 0.4 must be 0.52 (research 1).");
                red.Position = new HexCoord(9, 5);
                f.Zones.Tick(0.1f);
                Require(f.Status.HasEffect(red.Id, StatusEffectKind.MoveSpeedMul)
                        && Mathf.Abs(f.Combat.GetUnitMoveSpeedMultiplier(red) - 1.3f) < 1e-5f,
                    "Z-T5 after leaving the swamp the tailwind (1.3) must remain — the zone never overwrites the status record (Z1 bug absent).");
            }
        }

        /// <summary> Z-T6 — 사망 정리(통보 · 틱 감지). </summary>
        private static void ValidateSkillZoneDeath()
        {
            using (var f = new SkillZoneFixture())
            {
                UnitData a = f.Unit(9950, TeamId.Red, new HexCoord(0, 0), new HexCoord(5, 5));
                UnitData b = f.Unit(9951, TeamId.Red, new HexCoord(1, 0), new HexCoord(5, 6));
                f.Zones.CreateZone(501, TeamId.Blue, 1, 0, new Vector3(5f, 0f, 5f), 2f, 4f, StatusEffectKind.Freeze, 0f, false);
                f.Zones.Tick(0.1f); // 첫 알림
                int before = f.Events.Count;

                a.TakeDamage(a.MaxHp * 10);
                f.Zones.NotifyUnitDied(a.Id);
                Require(!f.Zones.IsInAnyZone(a.Id), "Z-T6 a dead unit must be removed from the zone at the death notification.");
                b.TakeDamage(b.MaxHp * 10); // 통보 없이 죽음 — 틱이 감지해야 한다
                f.Zones.Tick(0.1f);
                Require(f.Events.Count == before + 1 && ZoneEventHas(f.Events[before], a.Id, false) && ZoneEventHas(f.Events[before], b.Id, false),
                    "Z-T6 both deaths (notified and tick-detected) must be announced as Exited on the next tick.");
                Require(!f.Zones.IsInAnyZone(b.Id), "Z-T6 the tick must drop a dead member.");
            }
        }

        /// <summary> Z-T7 — 경기 종료 Close · 시전 건물 무관. </summary>
        private static void ValidateSkillZoneClose()
        {
            using (var f = new SkillZoneFixture())
            {
                UnitData red = f.Unit(9960, TeamId.Red, new HexCoord(0, 0), new HexCoord(5, 5));
                // 시전 건물 Id 12345는 건물 목록에 없다(= 이미 파괴된 건물과 같다) — 구역은 그래도 시간까지 남아야 한다(Z-7).
                f.Zones.CreateZone(601, TeamId.Blue, 1, 12345, new Vector3(5f, 0f, 5f), 2f, 4f, StatusEffectKind.Freeze, 0f, false);
                f.Zones.Tick(2f);
                Require(f.Zones.ZoneCount == 1 && !f.Combat.CanAttack(red),
                    "Z-T7 a zone whose caster building is gone must remain until its duration ends (Z-7).");
                f.Zones.Close();
                Require(f.Zones.IsClosed && f.Zones.ZoneCount == 0 && f.Combat.CanAttack(red) && f.Combat.GetUnitMoveSpeedMultiplier(red) == 1f,
                    "Z-T7 Close (game end) must release every member immediately (Z-8).");
                Require(f.Zones.CreateZone(602, TeamId.Blue, 1, 0, new Vector3(5f, 0f, 5f), 2f, 4f, StatusEffectKind.Freeze, 0f, false) == -1
                        && f.Zones.ZoneCount == 0,
                    "Z-T7 after Close no new zone may be created.");
            }
        }

        /// <summary> Z-T8 — 실행기 조건과 다리 값. </summary>
        private static void ValidateSkillZoneExecutor()
        {
            var executor = new PersistentStatusZoneExecutor();
            int calls = 0;
            Vector3 gotCenter = default; float gotRadius = 0f, gotDuration = 0f, gotMag = -1f;
            StatusEffectKind gotKind = StatusEffectKind.None; bool gotAllies = true; TeamId gotTeam = TeamId.Neutral;
            Action<TeamId, Vector3, float, float, StatusEffectKind, float, bool> bridge = (t, c, r, d, k, m, a) =>
            { calls++; gotTeam = t; gotCenter = c; gotRadius = r; gotDuration = d; gotKind = k; gotMag = m; gotAllies = a; };

            SkillData Skill(int kind, float mag, float radius, float duration) => new SkillData(
                null, SkillAimType.PointTarget, 15f, SkillMechanicType.PersistentStatusZone, radius, duration, 0, 0f,
                kind, mag, false, "z", "z", true, 0f, null, 2f);
            SkillActivationContext Ctx(SkillData s, bool hasAim) => new SkillActivationContext(s, TeamId.Blue, 1,
                new Vector3(3f, 0f, 4f), hasAim, (t, c, r, d, b) => { }, (t, c, r, dps, dur) => { }, (t, k, m, d) => { },
                (t, c, r, d) => { }, bridge);

            executor.Execute(Ctx(Skill((int)StatusEffectKind.MoveSpeedMul, 0.4f, 2f, 6f), true));
            Require(calls == 1 && gotTeam == TeamId.Blue && gotCenter == new Vector3(3f, 0f, 4f) && Mathf.Approximately(gotRadius, 2f)
                    && Mathf.Approximately(gotDuration, 6f) && gotKind == StatusEffectKind.MoveSpeedMul && Mathf.Approximately(gotMag, 0.4f) && !gotAllies,
                "Z-T8 the zone executor must call the zone bridge with the aim and the asset values.");
            executor.Execute(Ctx(Skill((int)StatusEffectKind.Freeze, 0f, 2f, 4f), false));
            executor.Execute(Ctx(Skill((int)StatusEffectKind.Freeze, 0f, 2f, 0f), true));
            executor.Execute(Ctx(Skill((int)StatusEffectKind.Freeze, 0f, 0f, 4f), true));
            executor.Execute(Ctx(Skill((int)StatusEffectKind.AttackPowerMul, 1.5f, 2f, 4f), true));
            Require(calls == 1, "Z-T8 no aim / zero duration / zero radius / an unsupported kind must not create a zone.");
            Require(new SkillExecutorRegistry().TryGet(SkillMechanicType.PersistentStatusZone) is PersistentStatusZoneExecutor
                    && (int)SkillMechanicType.PersistentStatusZone == 4,
                "Z-T8 the registry must map PersistentStatusZone (= 4, appended last) to the zone executor.");
        }

        /// <summary> Z-T9 — 두 자산 계약 · 로드아웃 · 연막탄 사용 불가 · 채워진 VFX 칸 계약. </summary>
        private static void ValidateSkillZoneAssets()
        {
            const string loadoutPath = "Assets/_Project/Resources/Config/Skills/SkillLoadoutConfig.asset";
            SkillLoadoutConfig loadout = AssetDatabase.LoadAssetAtPath<SkillLoadoutConfig>(loadoutPath);
            Require(loadout != null, "Z-T9 SkillLoadoutConfig asset must exist at " + loadoutPath + ".");
            loadout.ClearCache();

            ValidateSkillZoneAsset(loadout, FrostPrisonAssetPath, RaceId.Spirit, 1, StatusEffectKind.Freeze, 0f, 4f);
            ValidateSkillZoneAsset(loadout, SapSwampAssetPath, RaceId.Transcendence, 0, StatusEffectKind.MoveSpeedMul, 0.4f, 6f);

            SkillDefinition smoke = AssetDatabase.LoadAssetAtPath<SkillDefinition>(SmokeBombAssetPath);
            Require(smoke != null && !smoke.ToSkillData().IsAvailable,
                "Z-T9 Smoke Bomb must stay unavailable (its evasion is out of scope).");
        }

        private static void ValidateSkillZoneAsset(SkillLoadoutConfig loadout, string path, RaceId race, int slot,
                                                   StatusEffectKind kind, float magnitude, float duration)
        {
            SkillDefinition def = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            Require(def != null, "Z-T9 zone skill asset must exist: " + path);
            SkillData data = def.ToSkillData();
            Require(data.IsAvailable && data.AimType == SkillAimType.PointTarget && data.Mechanic == SkillMechanicType.PersistentStatusZone
                    && (StatusEffectKind)data.StatusKind == kind && Mathf.Approximately(data.Magnitude, magnitude)
                    && Mathf.Approximately(data.Duration, duration) && Mathf.Approximately(data.Radius, 2f)
                    && Mathf.Approximately(data.Cooldown, 15f) && !data.TargetsAllies && data.TotalDamage == 0,
                "Z-T9 " + path + " must be available / point aim / type 4 / kind " + kind + " / magnitude " + magnitude +
                " / duration " + duration + " / radius 2 / cooldown 15 / enemies / damage 0 (Z-10 temporary values).");
            var raceLoadout = loadout.GetLoadout(race);
            Require(raceLoadout != null && slot < raceLoadout.Count && raceLoadout[slot] != null
                    && raceLoadout[slot].DisplayName == data.DisplayName && raceLoadout[slot].IsAvailable,
                "Z-T9 " + path + " must be the available skill in loadout " + race + " slot " + slot + ".");

            // 채워진 VFX 칸(구역 · 유닛 상태)은 상태 이펙트 계약을 지킨다(비어 있으면 효과만 동작 — 검사할 것 없음).
            RequireSkillZoneLoopPrefab(path, "zone VFX", data.ZoneVfxPrefab);
            RequireSkillZoneLoopPrefab(path, "unit status VFX", data.StatusVfxPrefab);
        }

        private static void RequireSkillZoneLoopPrefab(string path, string slotName, GameObject prefab)
        {
            if (prefab == null) return;
            Require(EffectManager.CheckUnitBuffLoopContract(prefab),
                "Z-T9 " + path + " " + slotName + " prefab must have ≥1 particle system, all Looping ON and Stop Action None.");
            foreach (ParticleSystem ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
                Require(ps.main.scalingMode == ParticleSystemScalingMode.Hierarchy,
                    "Z-T9 " + path + " " + slotName + " particle '" + ps.name + "' must use Scaling Mode Hierarchy.");
        }

        /// <summary> Z-T10 — 스키마 · 셋업 기대 수 · 소스 게이트. </summary>
        private static void ValidateSkillZoneSourceGates()
        {
            Require(UnitAttackShadowProfileResolver.PresentationContractSchemaRevision == 9,
                "Z-T10 the persistent-zone membership RPC must raise the presentation schema to 9.");

            FieldInfo expected = typeof(Hexiege.EditorTools.SupportSkillLoadoutSetup).GetField("ExpectedAvailableCount",
                BindingFlags.NonPublic | BindingFlags.Static);
            Require(expected != null && (int)expected.GetRawConstantValue() == 8,
                "Z-T10 SupportSkillLoadoutSetup.ExpectedAvailableCount must be 8 (six earlier skills + Frost Prison + Sap Swamp).");

            string net = File.ReadAllText("Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs");
            int zoneTick = net.IndexOf("GetSkillActivationUseCase()?.TickZones(elapsed);", StringComparison.Ordinal);
            int statusTick = net.IndexOf("GetStatusEffectSystem()?.Tick(elapsed);", StringComparison.Ordinal);
            Require(zoneTick >= 0 && statusTick > zoneTick,
                "Z-T10 the multiplayer server tick must run TickZones right before the unit status tick (Z-4).");

            string boot = File.ReadAllText("Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs");
            int singleZone = boot.IndexOf("if (!IsNetworkMode() && _skillActivation != null)\n            {\n                _skillActivation.TickZones(Time.deltaTime);", StringComparison.Ordinal);
            if (singleZone < 0) singleZone = boot.IndexOf("if (!IsNetworkMode() && _skillActivation != null)\r\n            {\r\n                _skillActivation.TickZones(Time.deltaTime);", StringComparison.Ordinal);
            int singleStatus = boot.IndexOf("_statusEffectSystem.Tick(Time.deltaTime);", StringComparison.Ordinal);
            Require(singleZone >= 0 && singleStatus > singleZone && boot.Contains("_skillActivation?.CloseZones()"),
                "Z-T10 single-player TickZones must be guarded by !IsNetworkMode() before the status tick, and game end must close zones.");

            string skillNet = File.ReadAllText("Assets/_Project/Scripts/Infrastructure/Network/NetworkSkillController.cs");
            Require(skillNet.Contains("private void ZoneMembershipChangedClientRpc(int sequence, int[] enteredIds, int[] exitedIds)")
                    && skillNet.Contains("GameEvents.OnSkillZoneMembershipConfirmed"),
                "Z-T10 NetworkSkillController must relay zone membership through ZoneMembershipChangedClientRpc (Z-2 N-B).");

            string driver = File.ReadAllText("Assets/_Project/Scripts/Presentation/Effects/UnitBuffVfxPresenter.ZoneDriver.cs");
            Require(driver.Contains("GameEvents.OnSkillZoneMembershipConfirmed.Subscribe") && driver.Contains("GameEvents.OnSkillZoneMembershipReceived.Subscribe")
                    && driver.Contains("\"NoPrefab\"") && driver.Contains("Diag=SkillZone, DiagRev=1"),
                "Z-T10 the zone-membership driver must subscribe both channels and log [SKILL-ZONE] NoPrefab.");
            string zoneVfx = File.ReadAllText("Assets/_Project/Scripts/Presentation/Effects/SkillZoneVfxPresenter.cs");
            Require(zoneVfx.Contains("GameEvents.OnSkillActivationConfirmed.Subscribe") && zoneVfx.Contains("GameEvents.OnSkillActivationReceived.Subscribe")
                    && zoneVfx.Contains("viewPosition.y += _groundYOffset;"),
                "Z-T10 the zone VFX presenter must use both activation channels and the ground height offset.");
        }
    }
}
