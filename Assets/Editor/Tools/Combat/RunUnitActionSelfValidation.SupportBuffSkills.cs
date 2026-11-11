// ============================================================================
// RunUnitActionSelfValidation.SupportBuffSkills.cs
// [2026-11-10 지원 스킬 1차 강화 4종 + 강화 상태 VFX 경로] 회귀 검증(fixture).
// Task: Assets/_Project/Docs/_Tasks/2026-11-10/04_20_support-buff-skills-status-vfx/ (Plan 구현 단위 ④ · Research 10-3 후보 1~9).
// Run()(메뉴 「Hexiege/Combat/Run Full Regression Suite」)이 함께 돌린다.
//
// [배경 — 초급 개발자용 설명]
//   전술 지령 · 화염의 축복 · 숲의 포효(공격력 1.5배 · 8초)와 숲의 순풍(이동 1.3배 · 8초)은 스킬 자산의 숫자만으로 동작한다
//   (타입 C 실행기 · 상태 시스템은 이미 있다). 그래서 "숫자 하나가 빠지면 효과 없이 쿨다운만 도는" 실수(위험 R-1)와
//   "상태 규칙(아군만 · 살아 있는 유닛만 · 다시 쓰면 시간 교체 · 끝나면 원래대로)"이 깨지는 것을 자동으로 막는다.
//
// 무엇을 확인하는가(Research 10-3 후보 번호):
//   B-1 (후보 1) 공격력 = 기본 × 1.5를 반올림 · 이동 배율 1.3(1보다 큰 배율 허용) · 시간이 지나면 정확히 원래 값으로 돌아온다.
//   B-2 (후보 2) 같은 종류를 다시 걸면 남은 시간이 새 지속 시간으로 교체된다(누적 · 중첩 아님).
//   B-3 (후보 3 · 7) 대상 팀(아군)의 "살아 있는" 유닛에만 걸린다 — 죽은 아군 · 적 유닛에는 걸리지 않는다.
//   B-4 (후보 8) 걸린 뒤 새로 생긴 유닛에는 걸리지 않는다(사용자 결정 D1 — 발동 순간의 아군만).
//   B-5 (후보 4) 네 자산 계약: 사용 가능 · 즉시 발동 · 타입 C · 지속 > 0 · 상태 종류 · 강도 > 0 · 아군 · 쿨다운 15 + 로드아웃의 그 슬롯에
//        들어 있다 + 실제 실행기(GlobalStatusChangeExecutor)가 그 값으로 적용 다리를 부른다.
//   B-6 (후보 5) 네 자산 중 강화 상태 VFX 칸이 채워진 것은 「상태 이펙트 계약」(모든 파티클 Looping ON · Stop Action None ·
//        Scaling Mode Hierarchy)을 지킨다 — 런타임 재생 검사(EffectManager.CheckUnitBuffLoopContract)와 같은 함수 + Hierarchy.
//   B-7 (후보 6) 확인 번호(lease): 따라가기는 지금 번호일 때만 움직이고, 옛 번호 · 정지 뒤에는 움직이지 않는다.
//   B-8 (후보 9) 스키마 고정값 8 그대로(안 A — RPC 변경 없음) + 소스 게이트(즉시 발동 VFX 갈래 · ProjectNow 순서 · 진단 머리).
//
// 실제 씬 · 멀티 전송 · 화면 이펙트 모양은 여기서 확인하지 않는다(사용자 실기 항목 — Plan 6절).
// 모든 fixture는 새 인스턴스를 만들고, 만든 GameObject는 finally에서 지운다(다른 검사에 영향 없음).
// ============================================================================

using System;
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
        // 네 자산의 경로 · 기대값(D4 시험용 임시값). 값이 바뀌면 이 표도 함께 바꾼다(밸런싱 단계).
        private struct SupportBuffAssetExpectation
        {
            public string Path;
            public RaceId Race;
            public int Slot;
            public StatusEffectKind Kind;
            public float Magnitude;
        }

        private static readonly SupportBuffAssetExpectation[] SupportBuffAssets =
        {
            new SupportBuffAssetExpectation { Path = "Assets/_Project/Resources/Config/Skills/Skill_Human_TacticalOrder.asset",
                Race = RaceId.Human, Slot = 4, Kind = StatusEffectKind.AttackPowerMul, Magnitude = 1.5f },
            new SupportBuffAssetExpectation { Path = "Assets/_Project/Resources/Config/Skills/Skill_Spirit_FlameBlessing.asset",
                Race = RaceId.Spirit, Slot = 2, Kind = StatusEffectKind.AttackPowerMul, Magnitude = 1.5f },
            new SupportBuffAssetExpectation { Path = "Assets/_Project/Resources/Config/Skills/Skill_Trans_ForestRoar.asset",
                Race = RaceId.Transcendence, Slot = 3, Kind = StatusEffectKind.AttackPowerMul, Magnitude = 1.5f },
            new SupportBuffAssetExpectation { Path = "Assets/_Project/Resources/Config/Skills/Skill_Trans_ForestTailwind.asset",
                Race = RaceId.Transcendence, Slot = 4, Kind = StatusEffectKind.MoveSpeedMul, Magnitude = 1.3f },
        };

        private const float SupportBuffDuration = 8f;
        private const float SupportBuffCooldown = 15f;

        /// <summary>
        /// 지원 스킬 1차 강화 4종 + 강화 상태 VFX 경로 회귀 검증 진입점(B-1 ~ B-8).
        /// </summary>
        private static void ValidateSupportBuffSkills()
        {
            Require(!UnityEngine.Application.isPlaying, "Support buff fixture must run outside a live match.");
            ValidateSupportBuffStatusRules();       // B-1 · B-2 · B-3 · B-4
            ValidateSupportBuffSkillAssets();       // B-5 · B-6
            ValidateSupportBuffStatusLoopLease();   // B-7
            ValidateSupportBuffSourceGates();       // B-8
            Debug.Log("[UAS-DIAG][SKILL-BUFF] self-validation PASS: attack ×1.5 rounding / move ×1.3 (>1 allowed) / exact restore after expiry (B-1), same-kind re-apply replaces the remaining time (B-2), only alive allies of the target team are buffed (B-3), units spawned after the activation are not buffed (B-4, D1), the four buff skill assets are available instant type-C with duration/kind/magnitude/allies/cooldown and reach the executor bridge from their loadout slots (B-5), filled status VFX slots satisfy the looping/StopAction None/Hierarchy contract (B-6), status-loop follow obeys the lease (B-7), schema stays 8 and the instant-skill VFX / ProjectNow / [SKILL-BUFF] source gates hold (B-8).");
        }

        // EffectiveAttack는 private이라 리플렉션으로 부른다(유효 공격력 = 기본 × 연구 × 상태 — 실제 피해 계산이 쓰는 그 함수).
        private static int SupportBuffEffectiveAttack(UnitCombatUseCase combat, UnitData attacker)
        {
            MethodInfo method = typeof(UnitCombatUseCase).GetMethod("EffectiveAttack",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(UnitData) }, null);
            Require(method != null, "B-0 UnitCombatUseCase.EffectiveAttack(UnitData) must exist (the damage path's effective attack).");
            return (int)method.Invoke(combat, new object[] { attacker });
        }

        /// <summary> B-1 · B-2 · B-3 · B-4 — 상태 규칙을 실제 전투 UseCase · 상태 시스템으로 확인한다. </summary>
        private static void ValidateSupportBuffStatusRules()
        {
            var grid = new HexGrid(3, 3, HexOrientation.FlatTop);
            var spawn = new UnitSpawnUseCase(grid);
            var buildings = new BuildingPlacementUseCase(grid);
            var combat = new UnitCombatUseCase(grid, spawn, buildings, new ProjectileFixturePositions(), new ProjectileFixtureMapper());
            var status = new StatusEffectSystem();
            combat.SetStatusEffectSystem(status);

            UnitData blue = spawn.SpawnUnitWithId(9800, UnitType.Pistoleer, TeamId.Blue, new HexCoord(0, 0));
            UnitData blueDead = spawn.SpawnUnitWithId(9801, UnitType.Pistoleer, TeamId.Blue, new HexCoord(1, 0));
            UnitData red = spawn.SpawnUnitWithId(9802, UnitType.Pistoleer, TeamId.Red, new HexCoord(2, 0));
            Require(blue != null && blueDead != null && red != null, "B-0 fixture units must spawn.");
            blueDead.TakeDamage(blueDead.MaxHp * 10);
            Require(!blueDead.IsAlive, "B-0 fixture dead ally must be dead before the activation.");

            int blueBase = blue.AttackPower;
            int redBase = red.AttackPower;
            Require(SupportBuffEffectiveAttack(combat, blue) == blueBase && Mathf.Approximately(combat.GetUnitMoveSpeedMultiplier(blue), 1f),
                "B-1 without any status the effective attack and move multiplier must be unchanged.");

            // ── B-3: 아군(Blue)에게 공격력 1.5배 8초 — 살아 있는 아군만 ──
            combat.ApplySkillGlobalStatus(TeamId.Blue, StatusEffectKind.AttackPowerMul, 1.5f, SupportBuffDuration, raiseNetworkEvent: false);
            Require(status.HasEffect(blue.Id, StatusEffectKind.AttackPowerMul), "B-3 the alive ally must receive the buff.");
            Require(!status.HasEffect(blueDead.Id, StatusEffectKind.AttackPowerMul), "B-3 a dead ally must not receive the buff.");
            Require(!status.HasEffect(red.Id, StatusEffectKind.AttackPowerMul) && SupportBuffEffectiveAttack(combat, red) == redBase,
                "B-3 an enemy unit must not receive the ally buff (team filter).");

            // ── B-1: 반올림 공격력 · 이동 1.3배 ──
            Require(SupportBuffEffectiveAttack(combat, blue) == Mathf.Max(0, Mathf.RoundToInt(blueBase * 1.5f)),
                "B-1 buffed effective attack must be round(base × 1.5).");
            // 홀수 기본 공격력(25 → 37.5)으로 반올림 경로를 따로 본다(유닛 목록 밖 데이터 — EffectiveAttack은 데이터 + 상태만 읽는다).
            var odd = new UnitData(9810, UnitType.Pistoleer, TeamId.Blue, new HexCoord(0, 0), 100, 25, 1f, 1f);
            status.Apply(odd.Id, new StatusEffect(StatusEffectKind.AttackPowerMul, 1.5f, SupportBuffDuration, TeamId.Blue));
            Require(SupportBuffEffectiveAttack(combat, odd) == Mathf.RoundToInt(25 * 1.5f) && SupportBuffEffectiveAttack(combat, odd) > 25,
                "B-1 an odd base attack must use the same rounding as the damage path (Mathf.RoundToInt).");
            combat.ApplySkillGlobalStatus(TeamId.Blue, StatusEffectKind.MoveSpeedMul, 1.3f, SupportBuffDuration, raiseNetworkEvent: false);
            Require(Mathf.Abs(combat.GetUnitMoveSpeedMultiplier(blue) - 1.3f) < 1e-4f && Mathf.Approximately(combat.GetUnitMoveSpeedMultiplier(red), 1f),
                "B-1 the ally move multiplier must be 1.3 (a multiplier above 1 is allowed) while the enemy stays 1.");

            // ── B-4: 걸린 뒤 새로 생긴 아군에는 걸리지 않는다(D1) ──
            UnitData late = spawn.SpawnUnitWithId(9803, UnitType.Pistoleer, TeamId.Blue, new HexCoord(0, 1));
            Require(late != null && !status.HasEffect(late.Id, StatusEffectKind.AttackPowerMul)
                    && SupportBuffEffectiveAttack(combat, late) == late.AttackPower,
                "B-4 a unit spawned after the activation must not be buffed (D1: alive allies at the activation moment only).");

            // ── B-2: 5초 뒤 다시 걸면 남은 시간이 8초로 교체된다 ──
            status.Tick(5f);
            Require(status.HasEffect(blue.Id, StatusEffectKind.AttackPowerMul), "B-2 the buff must still be active after 5 s of 8 s.");
            combat.ApplySkillGlobalStatus(TeamId.Blue, StatusEffectKind.AttackPowerMul, 1.5f, SupportBuffDuration, raiseNetworkEvent: false);
            status.Tick(3.1f); // 교체가 없었다면 5 + 3.1 = 8.1초라 여기서 끝났어야 한다.
            Require(status.HasEffect(blue.Id, StatusEffectKind.AttackPowerMul)
                    && SupportBuffEffectiveAttack(combat, blue) == Mathf.Max(0, Mathf.RoundToInt(blueBase * 1.5f)),
                "B-2 re-applying the same kind must replace the remaining time with the new duration (not keep the old end).");
            Require(Mathf.Abs(UnitStatusMultiplierOf(status, blue.Id) - 1.5f) < 1e-4f,
                "B-2 re-applying must not stack the multiplier (still ×1.5, not ×2.25).");

            // ── B-1: 끝나면 정확히 원래 값 ──
            status.Tick(4.8f); // 재적용 뒤 3.1 + 4.8 = 7.9초 — 아직 남아 있다.
            Require(status.HasEffect(blue.Id, StatusEffectKind.AttackPowerMul), "B-2 the replaced buff must last the full new duration.");
            status.Tick(0.2f); // 8.1초 — 끝.
            Require(!status.HasEffect(blue.Id, StatusEffectKind.AttackPowerMul) && SupportBuffEffectiveAttack(combat, blue) == blueBase,
                "B-1 after expiry the effective attack must return exactly to the base value.");
            // 이동 버프는 처음 건 뒤 5 + 3.1 + 4.8 + 0.2 = 13.1초가 지났다(재적용 없음) → 끝나 있어야 한다.
            Require(!status.HasEffect(blue.Id, StatusEffectKind.MoveSpeedMul) && Mathf.Approximately(combat.GetUnitMoveSpeedMultiplier(blue), 1f),
                "B-1 after expiry the move multiplier must return exactly to 1.");
        }

        // 상태 시스템의 공격 배율(곱) — 중첩 여부 확인용.
        private static float UnitStatusMultiplierOf(StatusEffectSystem status, int unitId) => status.GetAttackMultiplier(unitId);

        /// <summary> B-5 · B-6 — 네 자산 계약 · 로드아웃 슬롯 · 실행기 연결 · 상태 VFX 계약. </summary>
        private static void ValidateSupportBuffSkillAssets()
        {
            const string loadoutPath = "Assets/_Project/Resources/Config/Skills/SkillLoadoutConfig.asset";
            SkillLoadoutConfig loadout = AssetDatabase.LoadAssetAtPath<SkillLoadoutConfig>(loadoutPath);
            Require(loadout != null, "B-5 SkillLoadoutConfig asset must exist at " + loadoutPath + ".");
            loadout.ClearCache(); // 이전 검사가 남긴 캐시가 아니라 지금 자산 값을 읽는다.

            foreach (SupportBuffAssetExpectation e in SupportBuffAssets)
            {
                SkillDefinition def = AssetDatabase.LoadAssetAtPath<SkillDefinition>(e.Path);
                Require(def != null, "B-5 support buff skill asset must exist: " + e.Path);
                SkillData data = def.ToSkillData();
                Require(data.IsAvailable && data.AimType == SkillAimType.Instant && data.Mechanic == SkillMechanicType.GlobalStatusChange,
                    "B-5 " + e.Path + " must be available, instant and type C (GlobalStatusChange).");
                Require(data.Duration > 0f && Mathf.Approximately(data.Duration, SupportBuffDuration)
                        && (StatusEffectKind)data.StatusKind == e.Kind && data.Magnitude > 0f && Mathf.Approximately(data.Magnitude, e.Magnitude)
                        && data.TargetsAllies && Mathf.Approximately(data.Cooldown, SupportBuffCooldown),
                    "B-5 " + e.Path + " must keep duration 8 / kind " + e.Kind + " / magnitude " + e.Magnitude + " / allies / cooldown 15 (D4 temporary values).");

                // 로드아웃의 그 종족 · 슬롯에 이 자산이 들어 있다(이름으로 대조 — GetLoadout은 SkillData를 새로 만든다).
                var raceLoadout = loadout.GetLoadout(e.Race);
                Require(raceLoadout != null && e.Slot < raceLoadout.Count && raceLoadout[e.Slot] != null
                        && raceLoadout[e.Slot].DisplayName == data.DisplayName && raceLoadout[e.Slot].IsAvailable,
                    "B-5 " + e.Path + " must be the available skill in loadout " + e.Race + " slot " + e.Slot + ".");

                // 실제 실행기가 이 값으로 상태 적용 다리를 부른다(효과 없이 쿨다운만 도는 일이 없다 — 위험 R-1).
                bool called = false;
                TeamId calledTeam = TeamId.Neutral; StatusEffectKind calledKind = StatusEffectKind.None; float calledMag = 0f, calledDur = 0f;
                var ctx = new SkillActivationContext(data, TeamId.Blue, 1, Vector3.zero, false,
                    (t, c, r, d, b) => { }, (t, c, r, dps, dur) => { },
                    (t, k, m, d) => { called = true; calledTeam = t; calledKind = k; calledMag = m; calledDur = d; },
                    (t, c, r, d) => { });
                new GlobalStatusChangeExecutor().Execute(ctx);
                Require(called && calledTeam == TeamId.Blue && calledKind == e.Kind && Mathf.Approximately(calledMag, e.Magnitude)
                        && Mathf.Approximately(calledDur, SupportBuffDuration),
                    "B-5 GlobalStatusChangeExecutor must call the status bridge for the caster team with the asset values: " + e.Path);

                // B-6: 강화 상태 VFX 칸이 채워져 있으면 상태 이펙트 계약을 지킨다(비어 있으면 검사할 것이 없다 — 칸이 비면 효과만 동작).
                if (data.StatusVfxPrefab != null)
                {
                    Require(EffectManager.CheckUnitBuffLoopContract(data.StatusVfxPrefab),
                        "B-6 " + e.Path + " status VFX prefab must have ≥1 particle system, all Looping ON and Stop Action None.");
                    foreach (ParticleSystem ps in data.StatusVfxPrefab.GetComponentsInChildren<ParticleSystem>(true))
                        Require(ps.main.scalingMode == ParticleSystemScalingMode.Hierarchy,
                            "B-6 " + e.Path + " status VFX particle '" + ps.name + "' must use Scaling Mode Hierarchy (unit-size fit).");
                    Require(data.StatusVfxScale > 0f, "B-6 " + e.Path + " status VFX scale must be positive.");
                }
            }
        }

        /// <summary> B-7 — 상태 루프 따라가기의 확인 번호(lease). EditMode라 Awake 대신 Setup으로 초기화한다. </summary>
        private static void ValidateSupportBuffStatusLoopLease()
        {
            GameObject vfx = null, pose = null;
            try
            {
                vfx = new GameObject("SupportBuffLeaseFixture");
                ParticleSystem ps = vfx.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = ps.main;
                main.loop = true;
                main.stopAction = ParticleSystemStopAction.None;
                VfxPoolItem item = vfx.AddComponent<VfxPoolItem>();
                item.Setup(vfx, null);

                pose = new GameObject("SupportBuffLeasePose");
                pose.transform.SetPositionAndRotation(new Vector3(1f, 0f, 2f), Quaternion.Euler(0f, 90f, 0f));

                item.Play(Vector3.zero, Quaternion.identity, Vector3.zero, Vector3.zero, Vector3.one, 0f, 0f);
                ulong lease = item.BeginStatusLoop();
                Require(lease != 0 && item.IsStatusLoopActive(lease), "B-7 a started status loop must issue a non-zero active lease.");
                var offset = new Vector3(0f, 0.1f, 0.2f);
                item.SetStatusLoopFollowOffset(lease, offset);

                Require(item.FollowStatusLoop(lease, pose.transform), "B-7 follow with the current lease must move the item.");
                Vector3 expected = pose.transform.position + pose.transform.rotation * offset;
                Require((item.transform.position - expected).sqrMagnitude < 1e-8f
                        && Quaternion.Angle(item.transform.rotation, pose.transform.rotation) < 0.01f,
                    "B-7 follow must place the item at pose + rotation × offset with the pose rotation.");

                pose.transform.position = new Vector3(5f, 0f, 5f);
                Require(!item.FollowStatusLoop(lease + 1, pose.transform) && (item.transform.position - expected).sqrMagnitude < 1e-8f,
                    "B-7 follow with a stale/foreign lease must not move the item.");

                Require(item.StopStatusLoop(lease, clear: false), "B-7 tail stop with the current lease must succeed.");
                Require(!item.FollowStatusLoop(lease, pose.transform) && (item.transform.position - expected).sqrMagnitude < 1e-8f,
                    "B-7 after the stop the item must no longer follow (tail particles fade where the unit was).");
            }
            finally
            {
                if (vfx != null) UnityEngine.Object.DestroyImmediate(vfx);
                if (pose != null) UnityEngine.Object.DestroyImmediate(pose);
            }
        }

        /// <summary> B-8 — 스키마 고정값 · 소스 게이트. </summary>
        private static void ValidateSupportBuffSourceGates()
        {
            // 안 A(D2): 강화 상태 VFX는 기존 발동 알림만 재사용 — 강화 스킬 자체는 RPC를 늘리지 않는다.
            //   [2026-11-11 지속 상태 구역 Task] 스키마는 구역 구성원 변경 RPC 때문에 8 → 9가 됐다(강화 스킬과 무관한 변경).
            //   옛 줄(기록용):
            // Require(UnitAttackShadowProfileResolver.PresentationContractSchemaRevision == 8,
            //     "B-8 support buff skills (D2 plan A) add no RPC — the presentation schema must stay 8.");
            Require(UnitAttackShadowProfileResolver.PresentationContractSchemaRevision == 9,
                "B-8 support buff skills (D2 plan A) add no RPC; the schema is 9 only because of the persistent-zone membership RPC.");

            string presenter = File.ReadAllText("Assets/_Project/Scripts/Presentation/Effects/UnitBuffVfxPresenter.cs");
            int project = presenter.IndexOf("entry.Projector.ProjectNow();\n            entry.Item.FollowStatusLoop(", StringComparison.Ordinal);
            if (project < 0) project = presenter.IndexOf("entry.Projector.ProjectNow();\r\n            entry.Item.FollowStatusLoop(", StringComparison.Ordinal);
            Require(project >= 0,
                "B-8 UnitBuffVfxPresenter must call VisualRootProjector.ProjectNow() right before FollowStatusLoop (flipped Client contract).");
            Require(presenter.Contains("GameEvents.OnSkillActivationConfirmed.Subscribe") && presenter.Contains("GameEvents.OnSkillActivationReceived.Subscribe")
                    && presenter.Contains("\"NoPrefab\"") && presenter.Contains("Diag=SkillBuff, DiagRev=1"),
                "B-8 UnitBuffVfxPresenter must subscribe both activation channels and log [SKILL-BUFF] NoPrefab.");

            string activation = File.ReadAllText("Assets/_Project/Scripts/Presentation/Effects/SkillActivationVfxPresenter.cs");
            Require(activation.Contains("// if (!p.HasAim)") && activation.Contains("anchor = AnchorCasterBuilding;")
                    && activation.Contains("HexMetrics.HexToWorld(caster.Position)"),
                "B-8 SkillActivationVfxPresenter must keep the old NoAim return commented and play instant skills at the caster building (D3).");

            string combat = File.ReadAllText("Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.SkillBuffDiagnostics.cs");
            Require(combat.Contains("Event=BuffApplied") && combat.Contains("Event=BuffExpired") && combat.Contains("Event=BuffAttackSample")
                    && combat.Contains("Event=BuffMoveSample") && combat.Contains("Event=BuffStatusReceived") && combat.Contains("Event=BuffNewUnit"),
                "B-8 the [SKILL-BUFF] server/client diagnostics (L1 · L3 · L4 · L5 · L8 · L9) must exist.");
            string skill = File.ReadAllText("Assets/_Project/Scripts/Application/UseCases/SkillActivationUseCase.cs");
            Require(skill.Contains("Event=BuffNoEffect"), "B-8 the BuffNoEffect warning (L2) must exist.");
        }
    }
}
