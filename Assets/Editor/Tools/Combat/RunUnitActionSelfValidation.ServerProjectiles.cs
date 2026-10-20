// ============================================================================
// RunUnitActionSelfValidation.ServerProjectiles.cs
// [2026-10-20 서버 권위 투사체 — 단계 A] 회귀 검증(fixture). 메뉴 「Hexiege/Combat/Run Full Regression Suite」에서 함께 돈다.
//
// 무엇을 확인하는가 (Plan 6절 fixture 중 단계 A 해당분 — 번호는 Plan 6절 목록 번호):
//   1·15  싱글: 스위치가 켜진 StreamSpirit은 발사 때 피해 0, 착탄 때 1회. 꺼진 Tank는 예전처럼 타격 순간 피해.
//   3     비행 중 대상 사망 / 발사 후 공격자 사망(Persist) / 닫기(게임 종료) / ID 재사용 다른 객체 미적중.
//   4·17  Homing 착탄: 비행 시간은 발사 때 (중심 거리 − 대상 반경) ÷ 속도로 고정, 대상이 움직여도 따라가 맞는다.
//   6     중복 거부·늦은 접속 중간 시작(화면 일정표).
//   7     화면: 결과 전 숫자 없음 → 착탄 때 1회, 순서 역전·시간 초과 방출(피격 표현 보관함).
//   8     건물 반경 표·누락 시 대체값.
//   9     스키마 4 · Mushroom TargetLocked · 스위치 토큰이 handshake 해시에 들어감.
//   10    발사 전 공격자 사망 / 대상 사망 / 이동 명령(취소 API) → 발사 0건, 사거리 이탈 → 원래 대상에게 발사 1건.
//   11    비행 중 대상 소실 → 피해 0, 결과 TargetLost 1건.
//   14    속도 값이 서버 착탄 시각을 바꾼다.
//   16    발사 전 예약이 남아 있는 동안 "대기 중"으로 보고(멀티 서버의 표현 닫기 유예 판단 입력).
// [2026-10-20 단계 B 추가]
//   B-1   스위치: Tank · CannonCart · StreamSpirit ON(토큰 "6,7,17"), 스키마 5. 스위치 OFF 예시는 Tank → EagleArcher로 교체.
//   B-2   Tank(유닛 대상)·CannonCart(건물 대상, 건물 2배) — 발사 때 피해 0, 착탄 때 같은 writer로 1회.
//   B-3   진단 END 형식: 서버 [PROJECTILE-END] side=server 합계·취소 사유·balance·verdict, 분포 상자, 화면 일정표 거부 사유·
//         도착 순서, 피해 숫자 보관함 방출 경로, UnitView marker 계수(공격자별 marker − 발사).
//   A-1~6 [2026-10-21 제안 A] 사망타 숫자: 대상 디스폰 뒤 착탄 지점에 "0" 1회, 건물, 살아 있는 대상 회귀 없음, 타임아웃 기억 위치,
//         순서 역전 1회, 중복 Release 1회, 이미 죽은 대상에 닿는 후속 투사체 숫자 없음, text= 계수.
//   B-4   의심 1: 새 경로 marker는 총구 VFX·발사음을 내지 않고, 발사 신호는 서버 발사 기록을 화면에 꺼낼 때만 낸다(소스 게이트).
//         진단 오표기 C: 발사 시점 StreamSpirit timer-impact가 writerResult=NotInvoked로 찍힌다(소스 게이트).
// 실제 Animator·VFX·NGO 전송·화면 픽셀은 여기서 확인하지 않는다(사용자 실기 항목 — Plan 7절).
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Hexiege.Application;
using Hexiege.Domain;
using Hexiege.Infrastructure;
using UnityEditor;
using UnityEngine;
using UniRx;

namespace Hexiege.Editor.Combat
{
    public static partial class RunUnitActionSelfValidation
    {
        /// <summary>fixture 전용 위치 공급자 — 테스트가 유닛 위치를 직접 옮길 수 있다.</summary>
        private sealed class ProjectileFixturePositions : IEntityPositionProvider
        {
            public readonly Dictionary<int, Vector3> Units = new Dictionary<int, Vector3>();
            // [단계 B] 건물 대상 fixture용. 표에 없으면 zero → UseCase가 헥스 좌표 변환으로 폴백한다(실제와 같은 규칙).
            public readonly Dictionary<int, Vector3> Buildings = new Dictionary<int, Vector3>();
            public Vector3 GetUnitWorldPosition(int unitId) => Units.TryGetValue(unitId, out Vector3 p) ? p : Vector3.zero;
            public Vector3 GetBuildingWorldPosition(int buildingId) => Buildings.TryGetValue(buildingId, out Vector3 p) ? p : Vector3.zero;
        }

        /// <summary>fixture 전용 좌표 변환 — 헥스(Q,R)를 그대로 (x,z)로 쓴다.</summary>
        private sealed class ProjectileFixtureMapper : IHexCoordinateMapper
        {
            public float TileHeight => .866f;
            public Vector3 HexToWorld(HexCoord h) => new Vector3(h.Q, 0, h.R);
            public HexCoord WorldToHex(Vector3 p) => new HexCoord((int)p.x, (int)p.z);
            public Vector3 NormalizeToDomainPosition(Vector3 p) => p;
        }

        /// <summary>한 시나리오의 준비물(새 전투 UseCase · 공격자 · 대상 · 관측 카운터).</summary>
        private sealed class ProjectileFixture : IDisposable
        {
            public UnitSpawnUseCase Spawn;
            public BuildingPlacementUseCase BuildingsUseCase;
            public ProjectileFixturePositions Positions;
            public UnitCombatUseCase Combat;
            public UnitData Attacker;
            public UnitData Victim;
            public readonly List<ProjectileLaunchPresentation> Launches = new List<ProjectileLaunchPresentation>();
            public readonly List<ProjectileImpactPresentation> Impacts = new List<ProjectileImpactPresentation>();
            public readonly List<EntityDamagedEvent> VictimDamage = new List<EntityDamagedEvent>();
            private IDisposable _damage;

            public ProjectileFixture(UnitType attackerType, float streamSpeed = 4f)
            {
                var grid = new HexGrid(3, 3, HexOrientation.FlatTop);
                Spawn = new UnitSpawnUseCase(grid);
                Positions = new ProjectileFixturePositions();
                BuildingsUseCase = new BuildingPlacementUseCase(grid);
                // 옛 줄(기록용): Combat = new UnitCombatUseCase(grid, Spawn, new BuildingPlacementUseCase(grid), Positions, ...)
                Combat = new UnitCombatUseCase(grid, Spawn, BuildingsUseCase, Positions,
                    new ProjectileFixtureMapper());
                var settings = new ProjectileSettings();
                settings.SetUnitSpeed(UnitType.StreamSpirit, streamSpeed);
                // [단계 B] Tank · CannonCart 속도 4(2026-10-20 결정 초기값)와 Castle 반경 0.5(건물 대상 fixture).
                settings.SetUnitSpeed(UnitType.Tank, 4f);
                settings.SetUnitSpeed(UnitType.CannonCart, 4f);
                settings.SetBuildingRadius(BuildingType.Castle, 0.5f);
                settings.SetUnitRadius(UnitType.LittleKnight, 0.25f);
                Combat.SetProjectileSettings(settings);
                Attacker = Spawn.SpawnUnitWithId(9700, attackerType, TeamId.Blue, new HexCoord(0, 0));
                Victim = Spawn.SpawnUnitWithId(9701, UnitType.LittleKnight, TeamId.Red, new HexCoord(1, 0));
                // 위치 (0.1, 0, 0.1) → (2.1, 0, 0.1): 중심 거리 2.0, 대상 반경 0.25 → 비행 거리 1.75.
                Positions.Units[Attacker.Id] = new Vector3(0.1f, 0f, 0.1f);
                Positions.Units[Victim.Id] = new Vector3(2.1f, 0f, 0.1f);
                Combat.ProjectileLaunched += Launches.Add;
                Combat.ProjectileResolved += Impacts.Add;
                UnitData victim = Victim;
                _damage = GameEvents.OnEntityDamaged.Subscribe(e => { if (ReferenceEquals(e.Entity, victim)) VictimDamage.Add(e); });
            }

            /// <summary>예약·발사·착탄 진행(싱글 PendingHit도 함께 돌려 이중 피해가 없는지 본다).</summary>
            public void Tick(double now)
            {
                Combat.TickPendingHits(0f, now);
                Combat.TickProjectiles(now);
            }

            public void Dispose()
            {
                _damage?.Dispose();
                Combat.CloseProjectiles("fixture-dispose");
            }
        }

        /// <summary>
        /// [2026-10-20 단계 A] 서버 권위 투사체 회귀 검증 진입점. 전역 상태(유닛 스탯·네트워크 문맥)는 finally에서 원래대로 되돌린다.
        /// </summary>
        public static void ValidateServerAuthoritativeProjectiles()
        {
            Require(!UnityEngine.Application.isPlaying, "Server projectile fixture must run outside a live match.");
            ValidateServerProjectilePolicyAndManifest();
            ValidateServerProjectileContracts();
            ValidateServerProjectilePresentationCoordinator();
            ValidateServerProjectileImpactHold();
            ValidateServerProjectileKillShotNumbers();          // [2026-10-21 제안 A] 사망타 숫자 대체 위치 ①~⑥ + 죽은 대상 후속 투사체 숫자 없음
            ValidateServerProjectileConfigAsset();
            ValidateServerProjectileDiagnosticFormats();       // [단계 B] 분포 상자 · marker 계수 형식
            ValidateServerProjectileStageBSourceGates();       // [단계 B] 의심 1 수정 · 진단 오표기 C
            var dataField = typeof(UnitStats).GetField("_data", BindingFlags.Static | BindingFlags.NonPublic);
            var idField = typeof(UnitData).GetField("_nextId", BindingFlags.Static | BindingFlags.NonPublic);
            Require(dataField != null && idField != null, "Server projectile fixture global-state seams must exist.");
            var oldStats = (IReadOnlyDictionary<UnitType, UnitStats.StatValues>)dataField.GetValue(null);
            object oldId = idField.GetValue(null);
            bool oldActive = NetworkContext.IsNetworkActive, oldServer = NetworkContext.IsNetworkServer;
            try
            {
                NetworkContext.Set(false, false);
                UnitStats.Initialize(new Dictionary<UnitType, UnitStats.StatValues>
                {
                    { UnitType.StreamSpirit, new UnitStats.StatValues { MaxHp = 300, AttackPower = 60, AttackRange = 3f,
                        DetectRange = 3f, MoveSpeed = .5f, AttackCooldown = 1.5f, HitFrameTimes = new[] { .5f } } },
                    { UnitType.Tank, new UnitStats.StatValues { MaxHp = 500, AttackPower = 50, AttackRange = 3f,
                        DetectRange = 3f, MoveSpeed = .5f, AttackCooldown = 2f, HitFrameTimes = new[] { .4f } } },
                    { UnitType.LittleKnight, new UnitStats.StatValues { MaxHp = 1000, AttackPower = 10, AttackRange = .5f,
                        DetectRange = 1f, MoveSpeed = 1f, AttackCooldown = 1f, HitFrameTimes = new[] { .3f } } },
                    // [단계 B] CannonCart(건물 2배 확인 — 공격력 10 → Castle 피해 20) · EagleArcher(스위치 OFF 예시).
                    { UnitType.CannonCart, new UnitStats.StatValues { MaxHp = 400, AttackPower = 10, AttackRange = 3f,
                        DetectRange = 3f, MoveSpeed = .5f, AttackCooldown = 2f, HitFrameTimes = new[] { .4f } } },
                    { UnitType.EagleArcher, new UnitStats.StatValues { MaxHp = 300, AttackPower = 20, AttackRange = 3f,
                        DetectRange = 3f, MoveSpeed = .5f, AttackCooldown = 1.5f, HitFrameTimes = new[] { .2f } } },
                });
                ValidateServerProjectileNormalHit();
                ValidateServerProjectileHomingAndSpeed();
                ValidateServerProjectileFlightOutcomes();
                ValidateServerProjectilePrelaunchCancellation();
                ValidateServerProjectileSwitchOffKeepsLegacy();
                ValidateServerProjectileTankAndCannonCart();   // [단계 B]
            }
            finally
            {
                UnitStats.Initialize(oldStats);
                idField.SetValue(null, oldId);
                NetworkContext.Set(oldServer, oldActive);
            }
            // [단계 B] PASS 문구 앞부분 갱신. 옛 앞부분(기록용): "self-validation PASS: stage-A switch (StreamSpirit only), schema 4 + ..."
            Debug.Log("[UAS-DIAG][SERVER-PROJECTILE] self-validation PASS: kill-shot numbers (unit/building kill shot shows 0 once at the screen impact point after despawn, living target unchanged, timeout uses remembered position, reversed order once, duplicate release once, later projectile on a dead target shows none, text result counters), stage-B switch (Tank, CannonCart, StreamSpirit; EagleArcher etc. off), schema 5 + Mushroom TargetLocked + switch token in handshake hash, Tank unit-target and CannonCart building-target (x2) launch-then-impact through the single writer, diagnostic END formats (server totals/cancel causes/balance, view schedule rejects/order, impact-hold release paths, marker-minus-launch counters, time distributions), suspicion-1 launch cue only on server launch records and NotInvoked timer label (source gates), single launch-without-damage then one surface-timed impact through the single writer with projectile-keyed damage events, fixed homing flight time and speed dependency, target-lost/attacker-dead Persist/close/ID-reuse outcomes, pre-launch attacker/target/move-command cancellation versus out-of-range launch, switch-off legacy timing, presentation schedule (begin/progress/duplicate/late-join/orphan/timeout/abort) and impact-hold release ordering, config asset contract.");
        }

        // ── 분류·스위치·handshake ───────────────────────────────────────────
        private static void ValidateServerProjectilePolicyAndManifest()
        {
            // [단계 B] 옛 단언(단계 A, 기록용 — 주석 보존):
            //   Require(IsServerProjectile(StreamSpirit) && !IsServerProjectile(Tank) && !IsServerProjectile(CannonCart) && ...
            //           && ManifestToken == "17", "Stage A switches on StreamSpirit only; ...");
            Require(ServerProjectileDeliveryPolicy.IsServerProjectile(UnitType.StreamSpirit)
                    && ServerProjectileDeliveryPolicy.IsServerProjectile(UnitType.Tank)
                    && ServerProjectileDeliveryPolicy.IsServerProjectile(UnitType.CannonCart)
                    && !ServerProjectileDeliveryPolicy.IsServerProjectile(UnitType.InfernoSpirit)
                    && !ServerProjectileDeliveryPolicy.IsServerProjectile(UnitType.FoxMagician)
                    && !ServerProjectileDeliveryPolicy.IsServerProjectile(UnitType.EagleArcher)
                    && !ServerProjectileDeliveryPolicy.IsServerProjectile(UnitType.MushroomBomber)
                    && !ServerProjectileDeliveryPolicy.IsServerProjectile(UnitType.Pistoleer)
                    && ServerProjectileDeliveryPolicy.ManifestToken == "6,7,17",
                "Stage B switches on Tank, CannonCart and StreamSpirit; every other projectile unit keeps the legacy path.");
            UnitType[] projectileUnits = { UnitType.Tank, UnitType.CannonCart, UnitType.InfernoSpirit,
                UnitType.StreamSpirit, UnitType.FoxMagician, UnitType.EagleArcher, UnitType.MushroomBomber };
            int projectileCount = 0;
            foreach (UnitType type in UnitAttackShadowProfileResolver.AllManifestTypes)
                if (ServerProjectileDeliveryPolicy.IsProjectileUnit(type)) projectileCount++;
            Require(projectileCount == projectileUnits.Length
                    && Array.TrueForAll(projectileUnits, ServerProjectileDeliveryPolicy.IsProjectileUnit)
                    && !ServerProjectileDeliveryPolicy.IsProjectileUnit(UnitType.TorrentSpirit),
                "Q11 projectile-unit classification is exactly the seven projectile units (Torrent excluded).");
            // [단계 B] 옛 단언(기록용): PresentationContractSchemaRevision == 4 — 단계 A 전송 계약.
            Require(UnitAttackShadowProfileResolver.PresentationContractSchemaRevision == 5,
                "Stage B projectile epoch RPC (snapshot launch count) raises the presentation schema to 5.");
            Require(UnitAttackShadowProfileResolver.TryResolve(UnitType.MushroomBomber, out var mushroom)
                    && mushroom.TargetMode == Hexiege.Application.Combat.Sequencing.AttackTargetMode.TargetLocked
                    && mushroom.Support == Hexiege.Application.Combat.Sequencing.UnitAttackShadowSupport.Unresolved
                    && !mushroom.CanRunSequencer,
                "U-16: MushroomBomber is TargetLocked (homing) while staying C3 Unresolved.");
            Require(UnitAttackShadowProfileResolver.TryComputePresentationProfileHash(out string hash, out _)
                    && hash.Length == 64,
                "Handshake hash still computes with the schema 5 manifest and the switch token.");
        }

        // ── 계약 값 검사 ────────────────────────────────────────────────────
        private static void ValidateServerProjectileContracts()
        {
            var key = new ProjectileDeliveryKey(5, 1);
            var good = new ProjectileLaunchPresentation(key, 1, true, (int)UnitType.StreamSpirit, 0, TeamId.Blue,
                2, true, Vector3.zero, Vector3.right, .25f, 1d, 1.5d, true);
            var reversed = new ProjectileLaunchPresentation(key, 1, true, 17, 0, TeamId.Blue,
                2, true, Vector3.zero, Vector3.right, .25f, 2d, 1.5d, true);
            var nan = new ProjectileLaunchPresentation(key, 1, true, 17, 0, TeamId.Blue,
                2, true, new Vector3(float.NaN, 0f, 0f), Vector3.right, .25f, 1d, 1.5d, true);
            Require(good.IsValid && !reversed.IsValid && !nan.IsValid && !new ProjectileDeliveryKey(0, 1).IsValid,
                "Launch records reject reversed times, non-finite positions and zero epochs.");
            Require(new ProjectileImpactPresentation(key, ProjectileImpactOutcome.Hit, Vector3.zero, 2, true, 5, 3, 1.5d).IsValid
                    && new ProjectileImpactPresentation(key, ProjectileImpactOutcome.TargetLost, Vector3.zero, 2, true, -1, 0, 1.5d).IsValid
                    && !new ProjectileImpactPresentation(key, ProjectileImpactOutcome.Hit, Vector3.zero, 2, true, -1, 3, 1.5d).IsValid
                    && !new ProjectileImpactPresentation(key, ProjectileImpactOutcome.None, Vector3.zero, 2, true, 5, 3, 1.5d).IsValid,
                "Impact results require a known outcome and a non-negative hit HP.");
            Vector3 surface = UnitCombatUseCase.ProjectileSurfacePoint(new Vector3(0f, 0f, 0f), new Vector3(2f, 0f, 0f), .5f);
            Require(Mathf.Abs(surface.x - 1.5f) < 1e-5f && Mathf.Abs(surface.z) < 1e-5f
                    && UnitCombatUseCase.ProjectileSurfacePoint(Vector3.zero, new Vector3(.2f, 0f, 0f), .5f) == new Vector3(.2f, 0f, 0f),
                "Q12 surface point moves the centre toward the shooter by the radius, never past the shooter.");
            var settings = new ProjectileSettings(.1f, .4f, 4f);
            settings.SetBuildingRadius(BuildingType.Castle, .9f);
            Require(Mathf.Approximately(settings.GetTargetRadius(new BuildingData(7001, BuildingType.Castle, TeamId.Red, new HexCoord(0, 0), 100)), .9f)
                    && Mathf.Approximately(settings.GetTargetRadius(new BuildingData(7002, BuildingType.MiningPost, TeamId.Red, new HexCoord(0, 0), 100)), .4f)
                    && Mathf.Approximately(settings.GetUnitSpeed(UnitType.Tank), ProjectileSettings.FallbackSpeed),
                "Building radius table falls back to the default radius; missing speed uses the safe fallback.");
        }

        // ── 1 · 15 · 17: 발사 때 피해 0, 착탄 때 1회(단일 writer · 투사체 번호표) ─────────
        private static void ValidateServerProjectileNormalHit()
        {
            using (var f = new ProjectileFixture(UnitType.StreamSpirit))
            {
                int hp = f.Victim.Hp;
                Require(f.Combat.TryAttack(f.Attacker, 0d).HasValue, "StreamSpirit TryAttack commits one attack.");
                Require(f.Combat.HasPendingProjectileLaunch(f.Attacker) && f.Launches.Count == 0,
                    "Commit creates a pending launch reservation, not a launch.");
                f.Tick(.49d);
                Require(f.Launches.Count == 0 && f.Victim.Hp == hp && f.VictimDamage.Count == 0,
                    "Before the OnAttackHit offset nothing is launched and no damage is applied.");
                f.Tick(.5d);
                Require(f.Launches.Count == 1 && f.Victim.Hp == hp && f.VictimDamage.Count == 0
                        && !f.Combat.HasPendingProjectileLaunch(f.Attacker),
                    "At the hit offset the server launches; damage is not applied at launch.");
                ProjectileLaunchPresentation launch = f.Launches[0];
                double expectedImpact = .5d + (2.0d - .25d) / 4d;
                Require(launch.IsValid && Math.Abs(launch.LaunchTime - .5d) < 1e-6 && Math.Abs(launch.ImpactTime - expectedImpact) < 1e-5
                        && launch.TargetId == f.Victim.Id && launch.AttackerId == f.Attacker.Id && launch.Homing
                        && Mathf.Approximately(launch.TargetRadius, .25f),
                    "Impact time is fixed at launch as launch + (centre distance - target radius) / speed.");
                f.Tick(expectedImpact - .001d);
                Require(f.Impacts.Count == 0 && f.Victim.Hp == hp, "No damage before the authoritative impact time.");
                f.Tick(expectedImpact);
                Require(f.Impacts.Count == 1 && f.VictimDamage.Count == 1 && f.Victim.Hp == hp - 60,
                    "At impact exactly one damage application goes through the existing writer.");
                ProjectileImpactPresentation impact = f.Impacts[0];
                Require(impact.Outcome == ProjectileImpactOutcome.Hit && impact.Key.Equals(launch.Key)
                        && impact.ResultingHp == f.Victim.Hp && impact.AppliedAmount == 60
                        && Mathf.Abs(impact.ImpactPoint.x - 1.85f) < 1e-4f && Mathf.Abs(impact.ImpactPoint.z - .1f) < 1e-4f,
                    "Hit result carries the server HP/amount and the surface impact point.");
                Require(f.VictimDamage[0].ProjectilePresentationKey.Equals(launch.Key)
                        && !f.VictimDamage[0].ImmediatePresentation && !f.VictimDamage[0].PresentationResultKey.IsValid,
                    "Impact damage events carry the projectile key so presentation waits for the visual impact.");
                f.Tick(expectedImpact + 1d);
                Require(f.Impacts.Count == 1 && f.VictimDamage.Count == 1, "Repeated ticks never duplicate the impact.");
                Require(!f.Combat.WasLatestProjectileLaunchCancelled(f.Attacker),
                    "A launched attack is not reported as a cancelled cue.");
                // [단계 B — B-3] 서버 END 1줄 형식: 전 경기 합계 · 수 맞춤(balance) · 판정.
                string end = f.Combat.BuildProjectileEndLine("fixture");
                Require(end.StartsWith("side=server v=1 role=Single authority=local ") && end.Contains(" reason=fixture ")
                        && end.Contains(" switch=6,7,17 ") && end.Contains(" reserved=1 ") && end.Contains(" launched=1 ")
                        && end.Contains(" hit=1 ") && end.Contains(" targetLost=0 ") && end.Contains(" cancelled=0 ")
                        && end.Contains(" cancel=attacker-dead:0|attacker-replaced:0|target-dead:0|target-missing:0|target-replaced:0|move-command:0|invalid-launch-geometry:0|other:0 ")
                        && end.Contains(" flight=n:1|") && end.Contains(" path=17:s1/l0 ")
                        && end.Contains(" balance=ok ") && end.EndsWith(" verdict=PASS"),
                    "Server END line carries whole-match totals, every cancel cause, path counts, balance and verdict: " + end);
            }
        }

        // ── 4 · 14: Homing(비행 시간 고정) · 속도 의존 ───────────────────────────
        private static void ValidateServerProjectileHomingAndSpeed()
        {
            using (var f = new ProjectileFixture(UnitType.StreamSpirit))
            {
                f.Combat.TryAttack(f.Attacker, 0d);
                f.Tick(.5d);
                double impactTime = f.Launches[0].ImpactTime;
                f.Positions.Units[f.Victim.Id] = new Vector3(2.6f, 0f, .1f);   // 비행 중 대상이 물러난다.
                f.Tick(impactTime - .01d);
                Require(f.Impacts.Count == 0, "Moving the target does not change the fixed flight time.");
                f.Tick(impactTime);
                Require(f.Impacts.Count == 1 && f.Impacts[0].Outcome == ProjectileImpactOutcome.Hit
                        && Mathf.Abs(f.Impacts[0].ImpactPoint.x - 2.35f) < 1e-4f,
                    "Homing hits the moved target at the fixed impact time, on its current surface point (no range recheck).");
            }
            using (var f = new ProjectileFixture(UnitType.StreamSpirit, streamSpeed: 2f))
            {
                f.Combat.TryAttack(f.Attacker, 0d);
                f.Tick(.5d);
                Require(f.Launches.Count == 1 && Math.Abs(f.Launches[0].ImpactTime - (.5d + 1.75d / 2d)) < 1e-5,
                    "Changing the server speed value changes the authoritative impact time.");
            }
        }

        // ── 3 · 11: 비행 중 대상 사망 · ID 재사용 · 공격자 사망(Persist) · 닫기 ────────
        private static void ValidateServerProjectileFlightOutcomes()
        {
            using (var f = new ProjectileFixture(UnitType.StreamSpirit))
            {
                f.Combat.TryAttack(f.Attacker, 0d);
                f.Tick(.5d);
                f.Victim.TakeDamage(f.Victim.Hp);                               // 다른 원인으로 비행 중 사망.
                f.Tick(f.Launches[0].ImpactTime);
                Require(f.Impacts.Count == 1 && f.Impacts[0].Outcome == ProjectileImpactOutcome.TargetLost
                        && f.Impacts[0].AppliedAmount == 0 && f.VictimDamage.Count == 0,
                    "Q4: a target lost in flight gets no damage; the result is TargetLost at the last point.");
            }
            using (var f = new ProjectileFixture(UnitType.StreamSpirit))
            {
                f.Combat.TryAttack(f.Attacker, 0d);
                f.Tick(.5d);
                f.Spawn.RemoveUnit(f.Victim.Id);
                UnitData reused = f.Spawn.SpawnUnitWithId(f.Victim.Id, UnitType.LittleKnight, TeamId.Red, new HexCoord(1, 0));
                int reusedHp = reused.Hp;
                f.Tick(f.Launches[0].ImpactTime);
                Require(reused != null && reused.Hp == reusedHp && f.Impacts.Count == 1
                        && f.Impacts[0].Outcome == ProjectileImpactOutcome.TargetLost,
                    "Risk 6: a new object reusing the target ID is never hit by the old projectile.");
            }
            using (var f = new ProjectileFixture(UnitType.StreamSpirit))
            {
                int hp = f.Victim.Hp;
                f.Combat.TryAttack(f.Attacker, 0d);
                f.Tick(.5d);
                f.Attacker.TakeDamage(f.Attacker.Hp);                           // 발사 뒤 공격자 사망.
                f.Spawn.RemoveUnit(f.Attacker.Id);
                f.Tick(f.Launches[0].ImpactTime);
                Require(f.Impacts.Count == 1 && f.Impacts[0].Outcome == ProjectileImpactOutcome.Hit
                        && f.Victim.Hp == hp - 60 && f.VictimDamage.Count == 1,
                    "Q3 Persist: a launched projectile still damages after its attacker died, with the launch-time attack power.");
                string end = f.Combat.BuildProjectileEndLine("fixture");
                Require(end.Contains(" attackerDead=hit:1|lost:0 ") && end.Contains(" attackerDeadGap=n:1|") && end.Contains(" balance=ok "),
                    "[B-3] Server END records the hit applied after the attacker died in flight and its death-to-impact gap: " + end);
            }
            using (var f = new ProjectileFixture(UnitType.StreamSpirit))
            {
                int hp = f.Victim.Hp;
                f.Combat.TryAttack(f.Attacker, 0d);
                f.Tick(.5d);
                f.Combat.CloseProjectiles("game-end");
                f.Tick(5d);
                Require(f.Impacts.Count == 0 && f.Victim.Hp == hp && f.VictimDamage.Count == 0,
                    "Risk 7: closing (game end/map clear) cancels in-flight projectiles without damage.");
                Require(!f.Combat.TryReserveProjectileLaunch(f.Attacker, f.Victim.Id, true, 6d, 0),
                    "A closed projectile service never accepts new reservations.");
                string end = f.Combat.BuildProjectileEndLine("game-end");
                Require(end.Contains(" launched=1 ") && end.Contains(" closedInFlight=1 ") && end.Contains(" hit=0 ")
                        && end.Contains(" reserveRefused=1 ") && end.Contains(" balance=ok ") && end.EndsWith(" verdict=PASS"),
                    "[B-3] Closing counts the in-flight projectile as closed (not hit) and keeps the balance: " + end);
            }
        }

        // ── 10 · 16: 발사 전 끊김 → 발사 0건 / 사거리 이탈 → 원래 대상에게 발사 ─────────
        private static void ValidateServerProjectilePrelaunchCancellation()
        {
            using (var f = new ProjectileFixture(UnitType.StreamSpirit))
            {
                f.Combat.TryAttack(f.Attacker, 0d);
                f.Attacker.TakeDamage(f.Attacker.Hp);
                f.Tick(.5d);
                f.Tick(2d);
                Require(f.Launches.Count == 0 && f.Impacts.Count == 0 && f.VictimDamage.Count == 0,
                    "Attacker death before launch: no launch, no result, no damage.");
            }
            using (var f = new ProjectileFixture(UnitType.StreamSpirit))
            {
                f.Combat.TryAttack(f.Attacker, 0d);
                f.Victim.TakeDamage(f.Victim.Hp);
                f.Tick(.3d);
                Require(!f.Combat.HasPendingProjectileLaunch(f.Attacker)
                        && f.Combat.WasLatestProjectileLaunchCancelled(f.Attacker),
                    "Target death before launch cancels at once and reports a cancelled cue (no muzzle VFX/SFX).");
                f.Tick(2d);
                Require(f.Launches.Count == 0 && f.Impacts.Count == 0,
                    "Target death before launch: no launch (Q15 — a dead target is never shot).");
                string end = f.Combat.BuildProjectileEndLine("fixture");
                Require(end.Contains(" cancelled=1 ") && end.Contains("|target-dead:1|") && end.Contains(" launched=0 ")
                        && end.Contains(" balance=ok "),
                    "[B-3] Pre-launch target death is counted under its own cancel cause (target-dead): " + end);
                // [B-4] 발사 기록이 없으므로 화면 소비자는 아무것도 꺼내지 않는다 → 발사 신호(총구 VFX·발사음)도 없다(의심 1).
                Require(f.Launches.Count == 0, "No server launch record exists for a cancelled attack, so no launch cue can be emitted.");
            }
            using (var f = new ProjectileFixture(UnitType.StreamSpirit))
            {
                f.Combat.TryAttack(f.Attacker, 0d);
                f.Combat.CancelProjectileLaunches(f.Attacker, "move-command");
                Require(!f.Combat.HasPendingProjectileLaunch(f.Attacker)
                        && f.Combat.WasLatestProjectileLaunchCancelled(f.Attacker),
                    "Explicit move/stop command cancels the unlaunched reservation.");
                f.Tick(2d);
                Require(f.Launches.Count == 0 && f.VictimDamage.Count == 0, "Cancelled reservation never launches.");
                Require(f.Combat.BuildProjectileEndLine("fixture").Contains("|move-command:1|"),
                    "[B-3] Explicit move-command cancellation is counted under its own cause.");
            }
            using (var f = new ProjectileFixture(UnitType.StreamSpirit))
            {
                // [B-3] 발사 전 공격자 사망은 attacker-dead로 따로 센다.
                f.Combat.TryAttack(f.Attacker, 0d);
                f.Attacker.TakeDamage(f.Attacker.Hp);
                f.Tick(.5d);
                Require(f.Combat.BuildProjectileEndLine("fixture").Contains("cancel=attacker-dead:1|"),
                    "[B-3] Pre-launch attacker death is counted under attacker-dead.");
            }
            using (var f = new ProjectileFixture(UnitType.StreamSpirit))
            {
                int hp = f.Victim.Hp;
                f.Combat.TryAttack(f.Attacker, 0d);
                f.Positions.Units[f.Victim.Id] = new Vector3(9.1f, 0f, .1f);   // 발사 전 사거리 밖으로 이동.
                f.Tick(.25d);
                Require(f.Combat.HasPendingProjectileLaunch(f.Attacker),
                    "Risk 16: leaving range before launch keeps the committed reservation pending (presentation must not close).");
                f.Tick(.5d);
                Require(f.Launches.Count == 1 && f.Launches[0].TargetId == f.Victim.Id,
                    "Q5: a target that walked out of range is still shot as the original target.");
                f.Tick(f.Launches[0].ImpactTime);
                Require(f.Impacts.Count == 1 && f.Victim.Hp == hp - 60, "The out-of-range original target is hit once.");
            }
        }

        // ── 15: 스위치가 꺼진 투사체 유닛은 옛 경로(타격 순간 피해) 그대로 ─────────────
        private static void ValidateServerProjectileSwitchOffKeepsLegacy()
        {
            // [단계 B] Tank가 스위치 ON이 됐으므로 OFF 예시를 EagleArcher(아직 OFF, 특수 공격 없음 단일 타격)로 바꾼다.
            //   옛 블록(단계 A, 기록용 — 주석 보존):
            //   using (var f = new ProjectileFixture(UnitType.Tank))
            //   {
            //       int hp = f.Victim.Hp;
            //       Require(f.Combat.TryAttack(f.Attacker, 0d).HasValue, "Tank TryAttack commits one attack.");
            //       Require(!f.Combat.HasPendingProjectileLaunch(f.Attacker), "A switched-off unit creates no projectile reservation.");
            //       f.Tick(.4d);
            //       Require(f.Launches.Count == 0 && f.Impacts.Count == 0 && f.Victim.Hp == hp - 50 && f.VictimDamage.Count == 1
            //               && !f.VictimDamage[0].ProjectilePresentationKey.IsValid,
            //           "Switch OFF keeps the legacy hit-time damage with no projectile key (no double damage).");
            //   }
            using (var f = new ProjectileFixture(UnitType.EagleArcher))
            {
                int hp = f.Victim.Hp;
                Require(f.Combat.TryAttack(f.Attacker, 0d).HasValue, "EagleArcher TryAttack commits one attack.");
                Require(!f.Combat.HasPendingProjectileLaunch(f.Attacker), "A switched-off unit creates no projectile reservation.");
                // [2026-10-20 검증 수정] 타격 예정 시각은 float 타격 시각(.2f)을 double로 넓힌 값(0 + 0.20000000298…)이다.
                //   옛 줄 f.Tick(.2d)는 그보다 아주 조금 이른 0.2라서 운영 코드의 "now < DueAt" 판정에 걸려 피해가 아직 안 들어갔다.
                //   그래서 유닛 데이터의 실제 타격 시각을 그대로 읽어 Tick 한다(하드코딩 시각 금지).
                //   옛 줄(기록용): f.Tick(.2d);
                f.Tick(f.Attacker.HitFrameTimes[0]);
                Require(f.Launches.Count == 0 && f.Impacts.Count == 0 && f.Victim.Hp == hp - 20 && f.VictimDamage.Count == 1
                        && !f.VictimDamage[0].ProjectilePresentationKey.IsValid,
                    "Switch OFF keeps the legacy hit-time damage with no projectile key (no double damage).");
                string end = f.Combat.BuildProjectileEndLine("fixture");
                Require(end.Contains(" path=24:s0/l1 ") && end.Contains(" reserved=0 ") && end.Contains(" launched=0 "),
                    "[B-3] The switched-off unit is counted on the legacy path in the server END path field: " + end);
            }
        }

        // ── [단계 B — B-2] Tank(유닛 대상) · CannonCart(건물 대상, 건물 2배) ───────────────────
        private static void ValidateServerProjectileTankAndCannonCart()
        {
            using (var f = new ProjectileFixture(UnitType.Tank))
            {
                int hp = f.Victim.Hp;
                Require(f.Combat.TryAttack(f.Attacker, 0d).HasValue && f.Combat.HasPendingProjectileLaunch(f.Attacker),
                    "Tank (switch ON) commit creates a launch reservation instead of a hit-time damage timer.");
                // [2026-10-20 검증 수정] 발사 예약 시각 = float 타격 시각(.4f)을 double로 넓힌 값(0.40000000596…)이다.
                //   옛 줄 f.Tick(.4d)·.4d 기준 착탄 시각은 그보다 조금 이르므로 운영 코드의 "now < Due"/"now < ImpactTime"
                //   판정에 걸려 발사·착탄이 일어나지 않는다. 유닛 데이터의 실제 타격 시각을 읽어 기준으로 삼는다.
                //   옛 줄(기록용): f.Tick(.4d);
                //   옛 줄(기록용): double expectedImpact = .4d + (2.0d - .25d) / 4d;
                double hitAt = f.Attacker.HitFrameTimes[0];
                f.Tick(hitAt);
                Require(f.Launches.Count == 1 && f.Victim.Hp == hp && f.VictimDamage.Count == 0,
                    "Tank launches at its OnAttackHit offset (0.4) with no damage at launch.");
                double expectedImpact = hitAt + (2.0d - .25d) / 4d;
                Require(Math.Abs(f.Launches[0].ImpactTime - expectedImpact) < 1e-5 && f.Launches[0].AttackerKind == (int)UnitType.Tank,
                    "Tank impact time = launch + (centre distance - target radius) / speed 4.");
                f.Tick(expectedImpact - .001d);
                Require(f.Impacts.Count == 0 && f.Victim.Hp == hp, "Tank deals no damage before the authoritative impact.");
                f.Tick(expectedImpact);
                Require(f.Impacts.Count == 1 && f.Impacts[0].Outcome == ProjectileImpactOutcome.Hit
                        && f.Victim.Hp == hp - 50 && f.VictimDamage.Count == 1
                        && f.VictimDamage[0].ProjectilePresentationKey.Equals(f.Launches[0].Key),
                    "Tank hits once at impact through the single writer with the projectile key.");
            }
            using (var f = new ProjectileFixture(UnitType.CannonCart))
            {
                // 건물 대상: Castle(Red, (2,0)) — 화면 위치 표에 없으므로 헥스 좌표 (2,0,0)으로 폴백한다(실제 규칙과 같다).
                BuildingData castle = f.BuildingsUseCase.PlaceBuildingWithId(9800, BuildingType.Castle, TeamId.Red, new HexCoord(2, 0));
                Require(castle != null && castle.IsAlive, "Fixture castle target is placed.");
                int hp = castle.Hp;
                Require(f.Combat.TryReserveProjectileLaunch(f.Attacker, castle.Id, false, .4d, 0),
                    "CannonCart (switch ON) can reserve a launch against a building target.");
                f.Tick(.4d);
                Require(f.Launches.Count == 1 && !f.Launches[0].TargetIsUnit && Mathf.Approximately(f.Launches[0].TargetRadius, .5f)
                        && castle.Hp == hp,
                    "Building target: launch uses the building radius table (Castle 0.5) and applies no damage at launch.");
                double distance = new Vector2(2f - .1f, 0f - .1f).magnitude;
                double expectedImpact = .4d + (distance - .5d) / 4d;
                Require(Math.Abs(f.Launches[0].ImpactTime - expectedImpact) < 1e-4,
                    "Building impact time = launch + (centre distance - building radius) / speed.");
                f.Tick(expectedImpact);
                Require(f.Impacts.Count == 1 && f.Impacts[0].Outcome == ProjectileImpactOutcome.Hit && castle.Hp == hp - 20
                        && f.Impacts[0].AppliedAmount == 20,
                    "CannonCart building hit applies the existing building x2 rule once at impact (attack 10 -> 20).");
                string end = f.Combat.BuildProjectileEndLine("fixture");
                Require(end.Contains(" launchedAtBuilding=1 ") && end.Contains(" hitBuilding=1 ") && end.Contains(" balance=ok "),
                    "[B-3] Server END separates building-target launches/hits: " + end);
            }
        }

        // ── 6 · 7: 화면 일정표 ─────────────────────────────────────────────
        private sealed class ProjectileFixtureSink : IProjectilePresentationSink
        {
            public int Begins, Finishes, FinishesWithoutResult, Aborts;
            public float LastProgress = -1f;
            public void Begin(ProjectilePresentationCoordinator.Slot slot) => Begins++;
            public void Advance(ProjectilePresentationCoordinator.Slot slot, float progress, double local) => LastProgress = progress;
            public void Finish(ProjectilePresentationCoordinator.Slot slot, bool withResult)
            { if (withResult) Finishes++; else FinishesWithoutResult++; }
            public void Abort(ProjectilePresentationCoordinator.Slot slot) => Aborts++;
        }

        private static void ValidateServerProjectilePresentationCoordinator()
        {
            const ulong epoch = 77;
            ProjectileLaunchPresentation Launch(ulong id, double launch, double impact) =>
                new ProjectileLaunchPresentation(new ProjectileDeliveryKey(epoch, id), 1, true, 17, 0, TeamId.Blue,
                    2, true, Vector3.zero, Vector3.right, .25f, launch, impact, true);
            ProjectileImpactPresentation Hit(ulong id) =>
                new ProjectileImpactPresentation(new ProjectileDeliveryKey(epoch, id), ProjectileImpactOutcome.Hit,
                    Vector3.right, 2, true, 10, 5, 1.5d);
            var c = new ProjectilePresentationCoordinator();
            var sink = new ProjectileFixtureSink();
            c.Reset(epoch, sink);
            Require(c.AcceptLaunch(Launch(1, 1d, 1.5d), 0d) && !c.AcceptLaunch(Launch(1, 1d, 1.5d), 0d)
                    && !c.AcceptLaunch(new ProjectileLaunchPresentation(new ProjectileDeliveryKey(epoch + 1, 2), 1, true, 17, 0,
                        TeamId.Blue, 2, true, Vector3.zero, Vector3.right, .25f, 1d, 1.5d, true), 0d),
                "Presentation schedule rejects duplicate keys (normal + snapshot) and other epochs.");
            c.Tick(.9d, 0d, sink);
            Require(sink.Begins == 0, "Nothing is shown before the server launch time.");
            c.Tick(1d, 0d, sink);
            Require(sink.Begins == 1 && Mathf.Approximately(sink.LastProgress, 0f), "The projectile appears at the launch time.");
            c.Tick(1.25d, 0d, sink);
            Require(Mathf.Approximately(sink.LastProgress, .5f) && sink.Finishes == 0, "Progress follows server launch/impact times.");
            c.Tick(1.5d, 0d, sink);
            c.Tick(1.6d, 1d, sink);
            Require(sink.Finishes == 0 && sink.FinishesWithoutResult == 0 && c.Count == 1,
                "Arriving before the server result holds at the endpoint: no number/impact yet.");
            Require(c.AcceptResult(Hit(1), 1d), "The server result is accepted.");
            c.Tick(1.7d, 1.1d, sink);
            Require(sink.Finishes == 1 && c.Count == 0 && !c.AcceptLaunch(Launch(1, 1d, 1.5d), 1.2d),
                "With the result the impact is shown exactly once and a late duplicate is rejected.");
            // 결과가 끝내 오지 않으면 상한 뒤 투사체만 거둔다.
            c.AcceptLaunch(Launch(2, 2d, 2.2d), 2d);
            c.Tick(2.3d, 2d, sink);
            c.Tick(2.4d, 2d + ProjectilePresentationCoordinator.ResultWaitSeconds + .01d, sink);
            Require(sink.FinishesWithoutResult == 1 && c.ResultTimeouts == 1, "Missing results time out without an impact.");
            // 늦은 접속: 이미 비행 중인 투사체는 중간 진행도로 바로 나타난다.
            c.AcceptLaunch(Launch(3, 3d, 4d), 5d);
            c.Tick(3.8d, 5d, sink);
            Require(sink.Begins == 3 && Mathf.Abs(sink.LastProgress - .8f) < 1e-4f,
                "Late join restores an in-flight projectile at its current progress (Q14).");
            // 결과가 발사 기록보다 먼저 온 경우(순서 역전)도 착탄 때 한 번 처리한다.
            Require(c.AcceptResult(Hit(4), 6d) && c.AcceptLaunch(Launch(4, 6d, 6.2d), 6d), "Result-before-launch is buffered.");
            c.Tick(6.3d, 6d, sink);
            Require(sink.Finishes == 2, "A buffered result completes on visual arrival.");
            // [단계 B — B-3] 거부 사유·도착 순서 계수: 끝난 키의 늦은 결과(중복 착탄) / 같은 키 결과 2회.
            Require(!c.AcceptResult(Hit(4), 6.4d), "A late duplicate result for a finished projectile is rejected.");
            c.AcceptLaunch(Launch(5, 7d, 7.2d), 6.5d);
            Require(c.AcceptResult(Hit(5), 6.5d) && !c.AcceptResult(Hit(5), 6.6d), "A second result for the same key is rejected.");
            string order = c.DescribeForDiagnostics();
            Require(order.Contains("rejected=total:5|invalid:0|epoch:1|dupLaunch:1|dupResult:1|finishedLaunch:1|finishedResult:1|capacity:0")
                    && order.Contains("order=resultBeforeLaunch:1|resultBeforeArrival:1|arrivalBeforeResult:3|orphanExpired:0|maxEndpointWait:1.100")
                    && order.Contains("timeouts=1"),
                "[B-3] Coordinator diagnostics separate reject causes (duplicate impact included) and arrival order: " + order);
            c.Close(sink);
            Require(sink.Aborts == 1 && c.Count == 0, "Game end aborts the started in-flight projectile without an impact.");
        }

        private static void ValidateServerProjectileImpactHold()
        {
            var hold = new ProjectileImpactPresentationHold<int>();
            var key = new ProjectileDeliveryKey(9, 1);
            var output = new List<int>();
            Require(!hold.HoldOrRelease(key, 11, 0d) && hold.HeldKeys == 1, "Damage presentation is held until the visual impact.");
            hold.Release(key, .1d, output);
            Require(output.Count == 1 && output[0] == 11 && hold.HeldKeys == 0, "The visual impact releases the held number once.");
            Require(hold.HoldOrRelease(key, 12, .2d), "A secondary event of the same impact arriving later is released at once.");
            var early = new ProjectileDeliveryKey(9, 2);
            output.Clear();
            hold.Release(early, 1d, output);
            Require(output.Count == 0 && hold.HoldOrRelease(early, 13, 1.05d),
                "If the visual impact comes first, the late damage event is released immediately.");
            var lost = new ProjectileDeliveryKey(9, 3);
            hold.HoldOrRelease(lost, 14, 2d);
            output.Clear();
            hold.CollectExpired(2d + ProjectileImpactPresentationHold<int>.TimeoutSeconds + .01d, output);
            Require(output.Count == 1 && output[0] == 14, "A never-released number is force-emitted after the timeout (no loss).");
            // [단계 B — B-3] 방출 경로별 계수: 착탄 방출 1 · 즉시 방출 2(착탄 신호 먼저 온 뒤 도착) · 타임아웃 1 · 빈 착탄 신호 1.
            Require(hold.DescribeForDiagnostics()
                    == "held:2|atImpact:1|immediateAfterRelease:2|immediateCapacity:0|timeout:1|emptyRelease:1|heldNow:0",
                "[B-3] Impact hold counts each release path: " + hold.DescribeForDiagnostics());
            hold.Clear();
            Require(hold.DescribeForDiagnostics()
                    == "held:0|atImpact:0|immediateAfterRelease:0|immediateCapacity:0|timeout:0|emptyRelease:0|heldNow:0",
                "Clearing the hold (map reload) resets its counters.");
        }

        // ── [2026-10-21 제안 A] 사망타 피해 숫자 — 대상이 이미 사라진 뒤의 방출 ──────────────────────
        /// <summary>fixture 전용 화면 pose 공급자 — 테스트가 유닛/건물 화면 오브젝트를 넣고 파괴할 수 있다(파괴되면 Unity null).</summary>
        private sealed class KillShotFixturePoses : Hexiege.Presentation.IPresentationPoseProvider
        {
            public readonly Dictionary<int, Transform> Units = new Dictionary<int, Transform>();
            public readonly Dictionary<int, Transform> Buildings = new Dictionary<int, Transform>();
            public Transform GetUnitTransform(int unitId) => Units.TryGetValue(unitId, out Transform t) && t != null ? t : null;
            public Transform GetBuildingTransform(int buildingId) => Buildings.TryGetValue(buildingId, out Transform t) && t != null ? t : null;
            public Vector3 GetUnitPosition(int unitId) { Transform t = GetUnitTransform(unitId); return t != null ? t.position : Vector3.zero; }
            public Vector3 GetBuildingPosition(int buildingId) { Transform t = GetBuildingTransform(buildingId); return t != null ? t.position : Vector3.zero; }
        }

        /// <summary>
        /// 분석 문서 7절 fixture ①~⑥ + 사용자 확인(2026-10-21): 사망타는 착탄 지점에 "0", 그 뒤 죽은 대상에 닿는 투사체는 숫자 없음.
        /// 실제 HitPresentationQueue를 Edit Mode에서 만들고, 그리기 직전 호출(숫자·피격 VFX·펀치)만 가로채 위치·값을 확인한다.
        /// </summary>
        private static void ValidateServerProjectileKillShotNumbers()
        {
            var root = new GameObject("Kill-shot number fixture") { hideFlags = HideFlags.HideAndDontSave };
            var spawned = new List<GameObject>();
            Hexiege.Presentation.HitPresentationQueue queue = null;
            bool oldActive = NetworkContext.IsNetworkActive, oldServer = NetworkContext.IsNetworkServer;
            try
            {
                NetworkContext.Set(false, false);
                var poses = new KillShotFixturePoses();
                var texts = new List<(int Hp, Vector3 Position)>();
                var vfx = new List<Vector3>();
                int punches = 0;
                queue = root.AddComponent<Hexiege.Presentation.HitPresentationQueue>();
                queue.Initialize(null, null, null, null, null, poses);
                queue.SetProjectileOwnerReady(() => true);
                queue.SetPresentationChannelsForValidation(
                    (e, p) => { texts.Add((e.CurrentHp, p)); return true; },
                    (_, p) => vfx.Add(p),
                    _ => punches++);

                Transform View(Dictionary<int, Transform> table, int id, Vector3 position)
                {
                    var go = new GameObject("Kill-shot view " + id) { hideFlags = HideFlags.HideAndDontSave };
                    go.transform.position = position;
                    spawned.Add(go);
                    table[id] = go.transform;
                    return go.transform;
                }
                UnitData Unit(int id) => new UnitData(id, UnitType.LittleKnight, TeamId.Red, new HexCoord(0, 0), 100, 10, .5f, 1f);
                void Damage(IDamageable entity, int hp, bool isUnit, ProjectileDeliveryKey key)
                    => GameEvents.OnEntityDamaged.OnNext(new EntityDamagedEvent(entity, hp, isUnit, 9700, true, false,
                        default, projectilePresentationKey: key));
                ProjectileDeliveryKey Key(ulong id) => new ProjectileDeliveryKey(77UL, id);

                // ① 유닛 사망타: HP 0 보관 → 사망 이벤트(보관함은 비우지 않는다 — Q7) → 디스폰 → 화면 착탄 → 착탄 지점에 "0" 1회.
                UnitData dead = Unit(9801);
                Transform deadView = View(poses.Units, dead.Id, new Vector3(3f, 0f, 4f));
                Damage(dead, 0, true, Key(1));
                GameEvents.OnUnitDied.OnNext(new UnitDiedEvent(dead));
                Require(texts.Count == 0, "Q7: the target-death flush does not release a projectile-held number before the visual impact.");
                UnityEngine.Object.DestroyImmediate(deadView.gameObject);
                var point1 = new Vector3(5f, .1f, 6f);
                queue.ReleaseProjectileImpact(Key(1), point1);
                Require(texts.Count == 1 && texts[0].Hp == 0 && texts[0].Position == point1,
                    "[A-1] Unit kill shot after despawn shows \"0\" once at the screen impact point: " + Describe(texts));
                Require(vfx.Count == 1 && vfx[0] == point1 && punches == 0,
                    "[A-1] Hit VFX uses the same fallback point; the punch is skipped without a target.");
                // ⑥ 같은 키 중복 Release → 추가 표시 없음.
                queue.ReleaseProjectileImpact(Key(1), point1);
                Require(texts.Count == 1, "[A-6] A duplicate release of the same key does not show the number twice.");
                // 사용자 확인: 이미 죽은 대상에 나중에 닿는 투사체는 서버가 TargetLost로 끝내 HP 이벤트가 없다 → 숫자 없음.
                //   (Presenter는 TargetLost에서 Release를 부르지 않지만, 불려도 보관 항목이 없으므로 아무것도 내지 않는다.)
                queue.ReleaseProjectileImpact(Key(2), new Vector3(5.2f, .1f, 6.1f));
                Require(texts.Count == 1 && vfx.Count == 1,
                    "[A-user] A later projectile reaching an already-dead target (no HP event) shows no number.");

                // ② 건물 사망타: 사전 제거(오브젝트 파괴) 후 Release → 1회, 건물이라 피격 VFX 없음.
                var castle = new BuildingData(9901, BuildingType.Castle, TeamId.Red, new HexCoord(0, 0), 1000);
                Transform castleView = View(poses.Buildings, castle.Id, new Vector3(8f, 0f, 2f));
                Damage(castle, 0, false, Key(3));
                UnityEngine.Object.DestroyImmediate(castleView.gameObject);
                var point3 = new Vector3(7.5f, 0f, 2f);
                queue.ReleaseProjectileImpact(Key(3), point3);
                Require(texts.Count == 2 && texts[1].Hp == 0 && texts[1].Position == point3 && vfx.Count == 1,
                    "[A-2] Building kill shot after removal shows \"0\" once at the impact point: " + Describe(texts));

                // ③ 살아 있는 대상: 착탄 지점이 아니라 지금 대상 위치에 표시(회귀 없음), VFX·펀치 정상.
                UnitData alive = Unit(9802);
                var alivePosition = new Vector3(7f, 0f, 8f);
                View(poses.Units, alive.Id, alivePosition);
                Damage(alive, 40, true, Key(4));
                queue.ReleaseProjectileImpact(Key(4), new Vector3(6.6f, .3f, 7.9f));
                Require(texts.Count == 3 && texts[2].Hp == 40 && texts[2].Position == alivePosition
                        && vfx.Count == 2 && vfx[1] == alivePosition && punches == 1,
                    "[A-3] A living target keeps the existing target-position number, hit VFX and punch: " + Describe(texts));

                // ⑤ 순서 역전: 화면 착탄(Release)이 먼저 → 디스폰 → HP 이벤트 → 기억한 착탄 지점에 바로 1회.
                UnitData reversed = Unit(9803);
                Transform reversedView = View(poses.Units, reversed.Id, new Vector3(1f, 0f, 9f));
                var point5 = new Vector3(1.2f, 0f, 8.8f);
                queue.ReleaseProjectileImpact(Key(5), point5);
                Require(texts.Count == 3, "[A-5] A release without a held item shows nothing yet.");
                UnityEngine.Object.DestroyImmediate(reversedView.gameObject);
                Damage(reversed, 0, true, Key(5));
                Require(texts.Count == 4 && texts[3].Hp == 0 && texts[3].Position == point5,
                    "[A-5] Reversed order (release first, HP later) shows the number once at the remembered impact point: " + Describe(texts));

                // ④ 타임아웃 방출: 보관할 때(HP 이벤트 순간) 기억한 대상 위치로 1회.
                UnitData timedOut = Unit(9804);
                var rememberedPosition = new Vector3(9f, 0f, 1f);
                Transform timedOutView = View(poses.Units, timedOut.Id, rememberedPosition);
                Damage(timedOut, 0, true, Key(6));
                UnityEngine.Object.DestroyImmediate(timedOutView.gameObject);
                // 보관 때부터 대상 화면이 없던 항목(대체 위치 없음) → 타임아웃 때 missing·missingDead로 센다(조용한 실패 금지).
                Damage(Unit(9805), 0, true, Key(7));
                queue.TickProjectileHoldForValidation(Time.realtimeSinceStartupAsDouble
                    + ProjectileImpactPresentationHold<int>.TimeoutSeconds + .5d);
                Require(texts.Count == 5 && texts[4].Hp == 0 && texts[4].Position == rememberedPosition,
                    "[A-4] A timed-out held number uses the target position remembered at the HP event: " + Describe(texts));

                string text = queue.DescribeProjectileTextForDiagnostics();
                Require(text == "played:1|fallback:4|missing:1|missingDead:1" && queue.ProjectileTextMissingItems == 1,
                    "[A-diag] Projectile number results are counted: " + text);
                string hold = queue.DescribeProjectileHoldForDiagnostics();
                Require(hold == "held:5|atImpact:3|immediateAfterRelease:1|immediateCapacity:0|timeout:2|emptyRelease:3|heldNow:0|ownerNotReady:0",
                    "[A-diag] played+fallback+missing equals atImpact+immediate+timeout: " + hold);
            }
            finally
            {
                queue?.Dispose();
                foreach (GameObject go in spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(root);
                NetworkContext.Set(oldServer, oldActive);
            }
        }

        private static string Describe(List<(int Hp, Vector3 Position)> texts)
        {
            var parts = new List<string>(texts.Count);
            foreach (var t in texts)
                parts.Add(FormattableString.Invariant($"{t.Hp}@({t.Position.x:F2},{t.Position.y:F2},{t.Position.z:F2})"));
            return "[" + string.Join(", ", parts) + "]";
        }

        // ── 설정 에셋 계약 ──────────────────────────────────────────────────
        private static void ValidateServerProjectileConfigAsset()
        {
            var config = AssetDatabase.LoadAssetAtPath<ProjectileConfig>("Assets/_Project/Resources/Config/ProjectileConfig.asset");
            Require(config != null, "ProjectileConfig.asset exists under Resources/Config (Resources.Load fallback path).");
            ProjectileSettings settings = config.ToSettings();
            Require(Mathf.Approximately(settings.GetUnitSpeed(UnitType.StreamSpirit), 4f)
                    && Mathf.Approximately(settings.GetUnitSpeed(UnitType.Tank), 4f)
                    && Mathf.Approximately(settings.GetUnitSpeed(UnitType.CannonCart), 4f)
                    && Mathf.Approximately(settings.GetUnitSpeed(UnitType.InfernoSpirit), 5f)
                    && Mathf.Approximately(settings.GetUnitSpeed(UnitType.FoxMagician), 5f)
                    && Mathf.Approximately(settings.GetUnitSpeed(UnitType.EagleArcher), 6f)
                    && Mathf.Approximately(settings.GetUnitSpeed(UnitType.MushroomBomber), 5f)
                    && Mathf.Approximately(settings.HumanTowerSpeed, 4f),
                "Initial server speeds match the 2026-10-20 decision (Inspector-tunable; update this check when tuned).");
            var unitTypes = new HashSet<UnitType>();
            foreach (var row in config.UnitRadii)
                if (row.radius > 0f && row.radius < 1f) unitTypes.Add(row.unitType);
            int buildings = 0;
            foreach (var row in config.BuildingRadii) if (row.radius > 0f) buildings++;
            Require(unitTypes.Count == UnitAttackShadowProfileResolver.AllManifestTypes.Count
                    && buildings == Enum.GetValues(typeof(BuildingType)).Length,
                "Every unit type has a server target radius and every building type has a radius row.");
        }

        // ── [단계 B — B-3] 분포 상자 · UnitView marker 계수 형식 ──────────────────────────
        private static void ValidateServerProjectileDiagnosticFormats()
        {
            var distribution = new ProjectileTimeDistribution(0.1d);
            distribution.Add(0.05d);
            distribution.Add(0.15d);
            distribution.Add(double.NaN);
            Require(distribution.Format() == "n:2|min:0.050|max:0.150|mean:0.100|lt0.1:1|ge0.1:1",
                "Time distribution ignores NaN and formats min/max/mean/buckets: " + distribution.Format());
            distribution.Clear();
            Require(distribution.Format() == "n:0|min:na|max:na|mean:na|lt0.1:0|ge0.1:0", "An empty distribution prints na.");

            // marker 계수: StreamSpirit 공격자 5가 marker 2 · 받은 발사 1 → "서버 발사 없이 나온 marker" 1.
            //   EagleArcher(스위치 OFF)는 기존 경로 marker·로컬 트레이서로, Pistoleer(비투사체)는 세지 않는다.
            ServerProjectileDiagnostics.ResetView();
            try
            {
                ServerProjectileDiagnostics.RecordProjectileUnitMarker(UnitType.StreamSpirit, 5, true, false, false);
                ServerProjectileDiagnostics.RecordProjectileUnitMarker(UnitType.StreamSpirit, 5, true, true, false);
                ServerProjectileDiagnostics.RecordServerLaunchAccepted(5);
                ServerProjectileDiagnostics.RecordServerLaunchAccepted(8);
                ServerProjectileDiagnostics.RecordProjectileUnitMarker(UnitType.EagleArcher, 6, false, false, false);
                ServerProjectileDiagnostics.RecordLegacyTracer(UnitType.EagleArcher);
                ServerProjectileDiagnostics.RecordProjectileUnitMarker(UnitType.Pistoleer, 7, false, false, false);
                ServerProjectileDiagnostics.RecordLegacyTracer(UnitType.Pistoleer);
                string view = ServerProjectileDiagnostics.DescribeView();
                Require(view == "markers=server:2|sourceOnly:1|muzzleAtMarker:0|withoutLaunch:1|launchWithoutMarker:1|attackers:2|overflow:0 path=17:s2/l0|24:s0/l1 legacyTracer=24:1",
                    "[B-3] View marker counters: per-attacker marker-minus-launch and per-type server/legacy path: " + view);
                Require(ServerProjectileDiagnostics.ServerMarkerMuzzleCount == 0, "No muzzle VFX is counted at a server-path marker.");
            }
            finally { ServerProjectileDiagnostics.ResetView(); }
        }

        // ── [단계 B — B-4] 의심 1 수정 · 진단 오표기 C 소스 게이트 ─────────────────────────
        private static void ValidateServerProjectileStageBSourceGates()
        {
            string Read(string relative)
            {
                string path = Path.Combine(UnityEngine.Application.dataPath, relative);
                Require(File.Exists(path), "Stage B source gate file exists: " + relative);
                return File.ReadAllText(path);
            }
            // 주석 줄(//로 시작)을 뺀 "실행되는 줄"만 모은다 — 주석 보존한 옛 코드가 게이트를 속이지 않게.
            string Active(string source)
            {
                var lines = source.Split('\n');
                var kept = new System.Text.StringBuilder(source.Length);
                foreach (string line in lines)
                    if (!line.TrimStart().StartsWith("//", StringComparison.Ordinal)) kept.Append(line).Append('\n');
                return kept.ToString();
            }
            string unitView = Active(Read("_Project/Scripts/Presentation/Unit/UnitView.cs"));
            string presenter = Active(Read("_Project/Scripts/Presentation/Effects/ProjectilePresentationPresenter.cs"));
            string transport = Active(Read("_Project/Scripts/Infrastructure/Network/NetworkCombatController.Projectiles.cs"));
            Require(unitView.Contains("if (serverProjectileMarker) return;")
                    && !unitView.Contains("WasLatestProjectileLaunchCancelled(")
                    && unitView.Contains("public UnitAttackVfxPlaybackResult PlayServerProjectileLaunchCue()"),
                "Suspicion 1: a server-path OnAttackHit marker emits nothing; the client-unknowable cancel flag is no longer read by the view.");
            Require(presenter.Contains("attackerView.PlayServerProjectileLaunchCue()")
                    && presenter.Contains("!midFlight"),
                "Suspicion 1: the launch cue (muzzle VFX + SFX) is emitted only when a server launch record is drawn, never for mid-flight restores.");
            Require(transport.Contains("writerResultOverride: \"NotInvoked(server-projectile-launch; damage-at-impact)\""),
                "Diagnostic label C: the StreamSpirit launch-time timer record says the writer was not invoked.");
            Require(transport.Contains("BeginProjectileEpochClientRpc(combat.ProjectileEpoch, _projectileSnapshotBuffer.Count, rpc)")
                    && transport.Contains("BeginProjectileEpochClientRpc(combat.ProjectileEpoch, -1)"),
                "Late-join snapshot announces its launch count; the normal epoch broadcast uses -1.");
            // [2026-10-21 제안 A] 화면 착탄 지점을 숫자 대체 위치로 넘기고, view END는 logcat 잘림 대응으로 2줄이다.
            Require(presenter.Contains("_hits?.ReleaseProjectileImpact(slot.Key, NumberFallbackPoint(slot, visual));")
                    && presenter.Contains("\"part=1/2 \"") && presenter.Contains("\"part=2/2\"")
                    && presenter.Contains("\" text=\" + (_hits != null ? _hits.DescribeProjectileTextForDiagnostics()"),
                "Option A: the presenter passes the screen impact point; the view END is split into two lines with text= results.");
        }
    }
}
