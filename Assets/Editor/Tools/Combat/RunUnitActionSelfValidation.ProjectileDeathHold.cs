// ============================================================================
// RunUnitActionSelfValidation.ProjectileDeathHold.cs
// [2026-10-26 서버 권위 투사체 — 단계 D 후속, 수정안 (a)] 회귀 검증(fixture).
// ValidateServerAuthoritativeProjectiles(메뉴 「Hexiege/Combat/Run Full Regression Suite」)가 함께 돌린다.
//
// 무엇을 확인하는가 (분석 문서 7절 후보 + 이번 작업 요구):
//   E-1  순수 일정표: 사망타가 보관 중이면 사망 표현을 미루고, 그 번호표가 화면에 나오는 순간 1회 진행(시각 차 0).
//        번호표 2개면 둘 다 나온 뒤, 상한 0.3초 초과면 즉시 진행(나중 착탄의 음수 시각 차 기록), 보관 사망타 없음 → 즉시,
//        착탄이 사망 통보보다 먼저(Host·싱글) → 즉시 + 양수 시각 차, 항목 상한 초과 → 즉시, 게임 종료 → 모두 진행, 버림.
//   E-2  화상 VFX 시작 판정: 화면 시각이 시작 시각 전이면 대기, 같은 공격자 피해 보관 중이면 대기, 0.3초 상한, 독은 즉시.
//   E-3  실제 HitPresentationQueue + DeferredUnitDeathPresenter: 사망타 보관 → 미룸 → ReleaseProjectileImpact 때 진행 1회,
//        비투사체 사망·살아 있는 대상·ID 재사용 다른 객체·대역 불가 → 즉시, 상한 초과 진행, 번호표 2개, 착탄 먼저(Host 순서),
//        재경기 재초기화 → 표현 없이 버림, 게임 종료 → 지금 진행 후 이후 사망은 즉시, 진단 줄 형식.
//   E-4  소스 게이트: 서버 판정·도메인 제거·사망 재발행·서버 Despawn 시각 무변경, UnitView 보류 위치(정리보다 앞),
//        Client 디스폰 사망 VFX 중복 방지, 조합 루트 연결, 화상 시작 보류 연결, 차지 VFX 대역 이관.
//   E-5  파티클 정적 검사: 프로젝트 전체 프리팹·씬·에셋에서 켜진 VelocityModule의 x/y/z(및 orbital 3축·offset 3축)
//        커브 모드가 섞인 것이 없다(기기 오류 "Particle Velocity curves must all be in the same mode" 재발 방지).
// 실제 Animator·NGO 전송·화면 픽셀·대역의 시각적 연속성은 여기서 확인하지 않는다(사용자 실기 항목).
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Hexiege.Application;
using Hexiege.Domain;
using UnityEngine;
using UniRx;

namespace Hexiege.Editor.Combat
{
    public static partial class RunUnitActionSelfValidation
    {
        /// <summary>fixture 전용 "미룬 사망 표현 주인" — 진행·버림 호출을 기록만 한다.</summary>
        private sealed class DeathHoldFixtureOwner : Hexiege.Presentation.IDeferredUnitDeathOwner
        {
            public readonly List<string> Released = new List<string>();
            public int Discarded;
            public void ReleaseDeferredDeathPresentation(string reason) => Released.Add(reason);
            public void DiscardDeferredDeathPresentation() => Discarded++;
        }

        /// <summary>[수정안 (a)] 사망 표현·화상 VFX 화면 착탄 정렬 회귀 검증 묶음.</summary>
        private static void ValidateServerProjectileDeathPresentationHold()
        {
            ValidateProjectileKillPresentationSchedule();
            ValidateProjectileAlignedBurnStart();
            ValidateDeferredUnitDeathPresenterWithQueue();
            ValidateDeathPresentationHoldSourceGates();
            ValidateParticleVelocityCurveModes();
        }

        // ── E-1 순수 일정표 ─────────────────────────────────────────────
        private static void ValidateProjectileKillPresentationSchedule()
        {
            var s = new ProjectileKillPresentationSchedule<string>();
            var output = new List<string>();
            var k1 = new ProjectileDeliveryKey(5, 1);
            var k2 = new ProjectileDeliveryKey(5, 2);
            var k3 = new ProjectileDeliveryKey(5, 3);
            object a = new object(), b = new object(), c = new object(), d = new object();

            Require(s.TryDefer(a, "A", new[] { k1 }, 10d) && s.HeldCount == 1, "[E-1] A held kill shot defers the death presentation.");
            s.NotifyKillShotShown(k1, a, 10.12d, output);
            Require(output.Count == 1 && output[0] == "A" && s.HeldCount == 0 && s.ReleasedAtImpact == 1,
                "[E-1] The death presentation runs once at the kill shot's screen impact.");
            output.Clear();
            s.NotifyKillShotShown(k1, a, 10.2d, output);
            Require(output.Count == 0, "[E-1] A duplicate impact notice does not run the death presentation twice.");

            Require(s.TryDefer(b, "B", new[] { k2, k3 }, 11d), "[E-1] Two held kill shots defer once.");
            s.NotifyKillShotShown(k2, b, 11.05d, output);
            Require(output.Count == 0, "[E-1] With two kill-shot keys the first impact alone does not release.");
            s.NotifyKillShotShown(k3, b, 11.08d, output);
            Require(output.Count == 1 && output[0] == "B", "[E-1] The last kill-shot impact releases.");
            output.Clear();

            Require(!s.TryDefer(c, "C", Array.Empty<ProjectileDeliveryKey>(), 12d) && s.ImmediateNoHeldKillShot == 1,
                "[E-1] A death without a held projectile kill shot (melee/instant/DoT) proceeds immediately.");

            // 착탄이 먼저(Host·싱글 순서) → 사망 통보는 즉시 진행, 시각 차는 양수.
            var k4 = new ProjectileDeliveryKey(5, 4);
            s.NotifyKillShotShown(k4, d, 13d, output);
            Require(!s.TryDefer(d, "D", Array.Empty<ProjectileDeliveryKey>(), 13.02d) && s.ImmediateAfterImpact == 1,
                "[E-1] A death notice after its impact was already shown proceeds immediately (afterImpact).");

            // 상한 초과 → 즉시 진행, 나중 착탄은 음수 시각 차.
            var k5 = new ProjectileDeliveryKey(5, 5);
            object e = new object();
            Require(s.TryDefer(e, "E", new[] { k5 }, 20d), "[E-1] Cap scenario defers.");
            s.CollectExpired(20d + ProjectileKillPresentationSchedule<string>.MaxHoldSeconds - .01d, output);
            Require(output.Count == 0, "[E-1] Nothing is released before the 0.3 s cap.");
            s.CollectExpired(20d + ProjectileKillPresentationSchedule<string>.MaxHoldSeconds, output);
            Require(output.Count == 1 && output[0] == "E" && s.ReleasedCap == 1,
                "[E-1] Past the cap the death presentation proceeds without waiting for the impact.");
            output.Clear();
            s.NotifyKillShotShown(k5, e, 20.4d, output);
            Require(output.Count == 0, "[E-1] The late impact after a cap release does not run the death again.");
            string text = s.DescribeForDiagnostics();
            Require(text.StartsWith("death=deferred:3|atImpact:2|cap:1|forced:0|discarded:0|heldNow:0 "
                        + "immediate=noHeldKillShot:2|afterImpact:1|capacity:0 holdDuration=n:3|", StringComparison.Ordinal)
                    && text.Contains("deathMinusImpact=n:4|min:-0.100|max:0.020|"),
                "[E-1] Diagnostics count deferred/at-impact/cap/immediate and the death-minus-impact distribution: " + text);

            // 항목 상한 초과 → 즉시.
            var full = new ProjectileKillPresentationSchedule<string>();
            for (int i = 0; i < ProjectileKillPresentationSchedule<string>.Capacity; i++)
                full.TryDefer(new object(), "x", new[] { new ProjectileDeliveryKey(6, (ulong)i + 1) }, 0d);
            Require(!full.TryDefer(new object(), "y", new[] { new ProjectileDeliveryKey(6, 9999) }, 0d) && full.ImmediateCapacity == 1,
                "[E-1] Over the entry capacity the death proceeds immediately (counted, never silently lost).");
            // 게임 종료 → 모두 진행 / 버림.
            full.ReleaseAll(1d, output);
            Require(output.Count == ProjectileKillPresentationSchedule<string>.Capacity && full.ReleasedForced == output.Count && full.HeldCount == 0,
                "[E-1] Game end releases every deferred death now.");
            output.Clear();
            full.TryDefer(new object(), "z", new[] { new ProjectileDeliveryKey(6, 10000) }, 2d);
            full.Discard(output);
            Require(output.Count == 1 && full.Discarded == 1 && full.HeldCount == 0, "[E-1] Reinitialize discards without presenting.");
        }

        // ── E-2 화상 VFX 시작 판정 ──────────────────────────────────────
        private static void ValidateProjectileAlignedBurnStart()
        {
            Require(ProjectileAlignedTimedDamageStart.ShouldWait(TimedDamageVisual.Burn, 10d, 9.95d, false, .05d),
                "[E-2] Burn VFX waits while the screen time is before the server start (Client 0.10 s delay).");
            Require(!ProjectileAlignedTimedDamageStart.ShouldWait(TimedDamageVisual.Burn, 10d, 10d, false, .1d),
                "[E-2] Burn VFX starts when the screen time reaches the server start.");
            Require(ProjectileAlignedTimedDamageStart.ShouldWait(TimedDamageVisual.Burn, 10d, 11d, true, .1d),
                "[E-2] Burn VFX waits while the same attacker's projectile hit is still held for the screen impact.");
            Require(!ProjectileAlignedTimedDamageStart.ShouldWait(TimedDamageVisual.Burn, 10d, 9d, true,
                    ProjectileAlignedTimedDamageStart.MaxHoldSeconds),
                "[E-2] Burn VFX starts at the 0.3 s cap even if still held.");
            // [2026-10-27 단계 E — 결정 E-B (a)] MushroomBomber가 서버 투사체가 되어 독도 화상과 같은 규칙으로 미룬다.
            //   옛 단언(기록용 — 주석 보존):
            //   Require(!ProjectileAlignedTimedDamageStart.ShouldWait(TimedDamageVisual.Poison, 10d, 9d, true, 0d),
            //       "[E-2] Poison is not delayed (Mushroom is not on the server projectile path yet).");
            Require(ProjectileAlignedTimedDamageStart.ShouldWait(TimedDamageVisual.Poison, 10d, 9.95d, false, .05d),
                "[E-2 / E2-4] Poison VFX waits while the screen time is before the server start (other enemies in the blast radius).");
            Require(ProjectileAlignedTimedDamageStart.ShouldWait(TimedDamageVisual.Poison, 10d, 11d, true, .1d),
                "[E-2 / E2-4] Poison VFX waits while the Mushroom's projectile hit on the primary target is still held.");
            Require(!ProjectileAlignedTimedDamageStart.ShouldWait(TimedDamageVisual.Poison, 10d, 10d, false, .1d)
                    && !ProjectileAlignedTimedDamageStart.ShouldWait(TimedDamageVisual.Poison, 10d, 9d, true,
                        ProjectileAlignedTimedDamageStart.MaxHoldSeconds),
                "[E-2 / E2-4] Poison VFX starts at the screen impact, or at the same 0.3 s cap as the burn.");
            Require(!ProjectileAlignedTimedDamageStart.ShouldWait(TimedDamageVisual.None, 10d, 9d, true, 0d),
                "[E-2] A timed effect without a visual is never delayed.");
            Require(ProjectileAlignedTimedDamageStart.IsProjectileAligned(TimedDamageVisual.Burn)
                    && ProjectileAlignedTimedDamageStart.IsProjectileAligned(TimedDamageVisual.Poison)
                    && !ProjectileAlignedTimedDamageStart.IsProjectileAligned(TimedDamageVisual.None),
                "[E-2 / E2-4] Burn (InfernoSpirit) and poison (MushroomBomber) are both server-projectile aligned while their units are switched on.");
            // [단계 E] 진단 계수는 화상·독을 따로 센다.
            ProjectileAlignedTimedDamageDiagnostics.Reset();
            ProjectileAlignedTimedDamageDiagnostics.RecordHeld(TimedDamageVisual.Poison);
            ProjectileAlignedTimedDamageDiagnostics.RecordReleased(TimedDamageVisual.Poison, false);
            ProjectileAlignedTimedDamageDiagnostics.RecordImmediate(TimedDamageVisual.Burn);
            string counters = ProjectileAlignedTimedDamageDiagnostics.Describe();
            ProjectileAlignedTimedDamageDiagnostics.Reset();
            Require(counters == "burn=held:0|released:0|cap:0|immediate:1 poison=held:1|released:1|cap:0|immediate:0",
                "[E-2 / E2-4] Burn and poison start-hold counters are separate: " + counters);
            Require(!ProjectileAlignedTimedDamageStart.ShouldWait(TimedDamageVisual.Burn, 10d, double.NaN, false, 0d),
                "[E-2] An unreadable presentation clock never blocks the burn VFX.");
        }

        // ── E-3 실제 큐 + 조율자 ────────────────────────────────────────
        private static void ValidateDeferredUnitDeathPresenterWithQueue()
        {
            var root = new GameObject("Death hold fixture") { hideFlags = HideFlags.HideAndDontSave };
            Hexiege.Presentation.HitPresentationQueue queue = null;
            Hexiege.Presentation.DeferredUnitDeathPresenter deaths = null;
            bool oldActive = NetworkContext.IsNetworkActive, oldServer = NetworkContext.IsNetworkServer;
            try
            {
                NetworkContext.Set(false, false);
                var texts = new List<int>();
                queue = root.AddComponent<Hexiege.Presentation.HitPresentationQueue>();
                queue.Initialize(null, null, null, null, null, new KillShotFixturePoses());
                queue.SetProjectileOwnerReady(() => true);
                queue.SetPresentationChannelsForValidation((ev, _) => { texts.Add(ev.CurrentHp); return true; }, (_, __) => { }, _ => { });
                double clock = 100d;
                deaths = root.AddComponent<Hexiege.Presentation.DeferredUnitDeathPresenter>();
                deaths.SetClockForValidation(() => clock);
                deaths.Initialize(queue);
                Require(Hexiege.Presentation.DeferredUnitDeathPresenter.Current == deaths, "[E-3] The presenter registers itself for UnitView.");

                UnitData Unit(int id) => new UnitData(id, UnitType.FoxMagician, TeamId.Blue, new HexCoord(0, 0), 100, 10, .5f, 1f);
                ProjectileDeliveryKey Key(ulong id) => new ProjectileDeliveryKey(88UL, id);
                void Damage(UnitData unit, int hp, ProjectileDeliveryKey key, int attacker = 9700)
                    => GameEvents.OnEntityDamaged.OnNext(new EntityDamagedEvent(unit, hp, true, attacker, true, false,
                        default, projectilePresentationKey: key));
                var point = new Vector3(1f, 0f, 1f);

                // ① 사망타 보관 → 미룸 → 화면 착탄 때 숫자 "0"과 함께 1회 진행.
                UnitData a = Unit(9850); var ownerA = new DeathHoldFixtureOwner();
                Damage(a, 0, Key(1));
                Require(queue.HasHeldProjectileHit(a, 9700) && !queue.HasHeldProjectileHit(a, 9701),
                    "[E-3] The queue reports a held projectile hit only for the same attacker (burn alignment input).");
                Require(deaths.TryDefer(a, ownerA, true) && deaths.HeldCount == 1 && ownerA.Released.Count == 0,
                    "[E-3] A held projectile kill shot defers the death presentation.");
                queue.ReleaseProjectileImpact(Key(1), point);
                Require(texts.Count == 1 && texts[0] == 0 && ownerA.Released.Count == 1 && ownerA.Released[0] == "at-impact"
                        && deaths.HeldCount == 0 && !queue.HasHeldProjectileHit(a, 9700),
                    "[E-3] The screen impact shows \"0\" and runs the deferred death once in the same call.");

                // ② 비투사체 사망(번호표 없음) → 즉시.
                UnitData b = Unit(9851);
                GameEvents.OnEntityDamaged.OnNext(new EntityDamagedEvent(b, 0, true, 9700, true));
                Require(!deaths.TryDefer(b, new DeathHoldFixtureOwner(), true), "[E-3] A non-projectile death stays immediate.");

                // ③ 살아 있는 대상의 투사체 피해는 사망타가 아니다 → 즉시.
                UnitData c = Unit(9852);
                Damage(c, 40, Key(2));
                Require(!deaths.TryDefer(c, new DeathHoldFixtureOwner(), true), "[E-3] A non-lethal held hit does not defer a later death.");
                queue.ReleaseProjectileImpact(Key(2), point);

                // ④ ID 재사용: 같은 Id의 다른 UnitData 객체는 보관 사망타와 섞이지 않는다.
                UnitData d = Unit(9853), dReused = Unit(9853); var ownerD = new DeathHoldFixtureOwner();
                Damage(d, 0, Key(3));
                Require(!deaths.TryDefer(dReused, new DeathHoldFixtureOwner(), true), "[E-3] A different object with a reused Id is not deferred.");
                Require(deaths.TryDefer(d, ownerD, true), "[E-3] The original object is deferred.");
                queue.ReleaseProjectileImpact(Key(3), point);
                Require(ownerD.Released.Count == 1, "[E-3] The original object's death runs at its impact.");

                // ⑤ 대역을 만들 수 없는 유닛(Visual Root 없음) → 즉시.
                UnitData e = Unit(9854);
                Damage(e, 0, Key(4));
                Require(!deaths.TryDefer(e, new DeathHoldFixtureOwner(), false), "[E-3] Without a detachable Visual Root the death stays immediate.");
                queue.ReleaseProjectileImpact(Key(4), point);

                // ⑥ 상한 초과 → 착탄 전 진행, 이후 착탄에서 다시 진행하지 않는다.
                clock = 200d;
                UnitData f = Unit(9855); var ownerF = new DeathHoldFixtureOwner();
                Damage(f, 0, Key(5));
                Require(deaths.TryDefer(f, ownerF, true), "[E-3] Cap scenario defers.");
                deaths.TickForValidation(200.29d);
                Require(ownerF.Released.Count == 0, "[E-3] Not released before the cap.");
                deaths.TickForValidation(200d + ProjectileKillPresentationSchedule<string>.MaxHoldSeconds);
                Require(ownerF.Released.Count == 1 && ownerF.Released[0] == "cap", "[E-3] Past 0.3 s the deferred death proceeds (cap).");
                clock = 200.4d;
                queue.ReleaseProjectileImpact(Key(5), point);
                Require(ownerF.Released.Count == 1, "[E-3] The late impact does not run the death twice.");

                // ⑦ 같은 대상에 사망타 2발(같은 틱) → 둘 다 화면에 나온 뒤 1회.
                UnitData g = Unit(9856); var ownerG = new DeathHoldFixtureOwner();
                Damage(g, 0, Key(6)); Damage(g, 0, Key(7));
                Require(deaths.TryDefer(g, ownerG, true), "[E-3] Two kill shots defer once.");
                queue.ReleaseProjectileImpact(Key(6), point);
                Require(ownerG.Released.Count == 0, "[E-3] The first of two kill shots does not release.");
                queue.ReleaseProjectileImpact(Key(7), point);
                Require(ownerG.Released.Count == 1, "[E-3] The second kill shot releases once.");

                // ⑧ Host·싱글 순서: 화면 착탄이 사망 통보보다 먼저 → 즉시(afterImpact).
                UnitData h = Unit(9857);
                Damage(h, 0, Key(8));
                queue.ReleaseProjectileImpact(Key(8), point);
                clock = 200.42d;
                Require(!deaths.TryDefer(h, new DeathHoldFixtureOwner(), true), "[E-3] Impact already shown → immediate (Host order).");

                string line = deaths.DescribeForDiagnostics("fixture");
                // [2026-10-27 단계 E] poison 칸 추가로 v=1 → v=2. 옛 단언(기록용 — 주석 보존):
                //   Require(line.StartsWith("side=view v=1 role=Single reason=fixture death=deferred:4|atImpact:3|cap:1|forced:0|discarded:0|heldNow:0 "
                //               + "immediate=noHeldKillShot:5|afterImpact:1|capacity:0 holdDuration=n:4|", StringComparison.Ordinal)
                //           && line.Contains(" gate=notReady:0|noVisualRoot:1 faults=0 burn=held:0|released:0|cap:0|immediate:0 verdict=ATTENTION attention=cap,no-visual-root"),
                //       "[E-3] [PROJECTILE-DEATH-HOLD] line format: " + line);
                Require(line.StartsWith("side=view v=2 role=Single reason=fixture death=deferred:4|atImpact:3|cap:1|forced:0|discarded:0|heldNow:0 "
                            + "immediate=noHeldKillShot:5|afterImpact:1|capacity:0 holdDuration=n:4|", StringComparison.Ordinal)
                        && line.Contains(" gate=notReady:0|noVisualRoot:1 faults=0 burn=held:0|released:0|cap:0|immediate:0 "
                            + "poison=held:0|released:0|cap:0|immediate:0 verdict=ATTENTION attention=cap,no-visual-root"),
                    "[E-3] [PROJECTILE-DEATH-HOLD] line format (v=2 with the poison start-hold counters): " + line);

                // ⑨ 재경기(맵 재초기화): 미뤄 둔 사망은 표현 없이 버린다.
                UnitData j = Unit(9858); var ownerJ = new DeathHoldFixtureOwner();
                Damage(j, 0, Key(9));
                Require(deaths.TryDefer(j, ownerJ, true), "[E-3] Rematch scenario defers.");
                deaths.Initialize(queue);
                Require(ownerJ.Discarded == 1 && ownerJ.Released.Count == 0 && deaths.HeldCount == 0,
                    "[E-3] Reinitialize (rematch) discards the deferred death without presenting it.");
                queue.ReleaseProjectileImpact(Key(9), point);
                Require(ownerJ.Released.Count == 0, "[E-3] An old-match impact after reinitialize does not present the discarded death.");

                // ⑩ 게임 종료: 남은 것을 지금 진행하고, 이후 사망은 즉시.
                UnitData i2 = Unit(9859); var ownerI = new DeathHoldFixtureOwner();
                Damage(i2, 0, Key(10));
                Require(deaths.TryDefer(i2, ownerI, true), "[E-3] Game-end scenario defers.");
                deaths.SimulateGameEndForValidation();
                Require(ownerI.Released.Count == 1 && ownerI.Released[0] == "game-end", "[E-3] Game end runs the deferred death now.");
                UnitData k = Unit(9860);
                Damage(k, 0, Key(11));
                Require(!deaths.TryDefer(k, new DeathHoldFixtureOwner(), true), "[E-3] After game end deaths are immediate.");
                string end = deaths.DescribeForDiagnostics("fixture");
                Require(end.Contains("death=deferred:1|atImpact:0|cap:0|forced:1|discarded:0|heldNow:0")
                        && end.Contains("gate=notReady:1|noVisualRoot:0"),
                    "[E-3] Game-end counters: " + end);
            }
            finally
            {
                deaths?.DetachForValidation();
                queue?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                NetworkContext.Set(oldServer, oldActive);
            }
            Require(Hexiege.Presentation.DeferredUnitDeathPresenter.Current == null, "[E-3] Detach clears the presenter registration.");
        }

        // ── E-4 소스 게이트 ─────────────────────────────────────────────
        private static void ValidateDeathPresentationHoldSourceGates()
        {
            string Read(string relative)
            {
                string path = Path.Combine(UnityEngine.Application.dataPath, relative);
                Require(File.Exists(path), "Death-hold source gate file exists: " + relative);
                return File.ReadAllText(path);
            }
            string Active(string source)
            {
                var kept = new System.Text.StringBuilder(source.Length);
                foreach (string line in source.Split('\n'))
                    if (!line.TrimStart().StartsWith("//", StringComparison.Ordinal)) kept.Append(line).Append('\n');
                return kept.ToString();
            }
            string ncc = Active(Read("_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs"));
            string netUnit = Active(Read("_Project/Scripts/Infrastructure/Network/NetworkUnit.cs"));
            string unitView = Active(Read("_Project/Scripts/Presentation/Unit/UnitView.cs"));
            string ghost = Active(Read("_Project/Scripts/Presentation/Unit/UnitView.DeathDeferral.cs"));
            string bootstrap = Active(Read("_Project/Scripts/Bootstrap/GameBootstrapper.Map.cs"));
            string burn = Active(Read("_Project/Scripts/Presentation/Effects/TimedDamageEffectPresenter.cs"));
            string casterOwned = Active(Read("_Project/Scripts/Presentation/Effects/VfxPoolItem.CasterOwned.cs"));

            // 서버 판정·도메인 제거·사망 재발행·Despawn 시각은 무변경(표현만 미룬다).
            int remove = ncc.IndexOf("unitSpawn.RemoveUnit(unitId);", StringComparison.Ordinal);
            int republish = ncc.IndexOf("GameEvents.OnUnitDied.OnNext(new UnitDiedEvent(unit));", StringComparison.Ordinal);
            Require(remove > 0 && republish > remove && ncc.Contains("EntityDiedClientRpc(unitId, true);")
                    && ncc.Contains("netObj.Despawn(destroy: true);") && !ncc.Contains("DeferredUnitDeathPresenter"),
                "[E-4] Client domain removal + OnUnitDied republish and the server Despawn stay immediate; the network controller is untouched.");
            int defer = unitView.IndexOf("bool deferDeathPresentation = TryBeginDeferredDeathPresentation();", StringComparison.Ordinal);
            int endHeal = unitView.IndexOf("EndHealPresentation(\"death\", terminal: true);", StringComparison.Ordinal);
            int closeAttack = unitView.IndexOf("CloseAttackPhasePresentation(\"attacker-death\");", StringComparison.Ordinal);
            int detach = unitView.IndexOf("DetachDeferredDeathGhost();", StringComparison.Ordinal);
            int casterDeath = unitView.IndexOf("StopCasterOwnedAttackEffects(this, \"CasterDeath\");", StringComparison.Ordinal);
            Require(defer > 0 && endHeal > defer && closeAttack > endHeal && detach > closeAttack && casterDeath > detach,
                "[E-4] UnitView decides the deferral before death cleanup (charge hand-over first) and detaches the ghost before the old immediate path.");
            Require(unitView.Contains("public partial class UnitView") && ghost.Contains("HandOverCasterOwnedAttackEffects(this, ghost);")
                    && ghost.Contains("StopGhostOwnedAttackEffects(ghost, \"CasterDeath\");")
                    && ghost.Contains("_deathGhostAnimator.SetBool(AnimIsDead, true);")
                    && ghost.Contains("PlayUnitDeath(_deathGhostType, position);") && ghost.Contains("PlayUnitDeathSfx(_deathGhostType);"),
                "[E-4] The ghost release replays the old death presentation (charge stop, IsDead, death VFX+SFX, destroy).");
            Require(netUnit.Contains("if (unitView != null && unitView.UnitData != null && !unitView.OwnsDeferredDeathPresentation)")
                    && netUnit.Contains("unitView.MarkDeathPresentedAtDespawn();"),
                "[E-4] Client despawn does not replay the death VFX of a deferred unit and marks despawn-first deaths.");
            Require(bootstrap.Contains("deathPresenter.Initialize(_hitPresentationQueue);")
                    && bootstrap.Contains("damagePresenter.SetProjectileImpactAlignment("),
                "[E-4] The composition root wires the death presenter and the burn start alignment.");
            Require(burn.Contains("if (ShouldHoldStart(owner)) continue;")
                    && burn.Contains("ProjectileAlignedTimedDamageStart.ShouldWait("),
                "[E-4] The burn VFX start goes through the alignment gate.");
            Require(casterOwned.Contains("&& _casterView.IsDeathPresentationDeferred) return false;")
                    && casterOwned.Contains("return StopCasterOwned(\"CasterGhostDestroyed\");"),
                "[E-4] The caster-owned safety net keeps a deferred caster's charge until the ghost is released/destroyed.");
        }

        // ── E-5 파티클 커브 모드 정적 검사 ──────────────────────────────
        private static void ValidateParticleVelocityCurveModes()
        {
            var module = new Regex(@"\n  VelocityModule:\n    enabled: 1\n(.*?)(?=\n  [A-Za-z]+Module:|\n--- |\z)",
                RegexOptions.Singleline);
            string[][] groups =
            {
                new[] { "x", "y", "z" },
                new[] { "orbitalX", "orbitalY", "orbitalZ" },
                new[] { "orbitalOffsetX", "orbitalOffsetY", "orbitalOffsetZ" }
            };
            var offenders = new List<string>();
            int scanned = 0;
            foreach (string pattern in new[] { "*.prefab", "*.unity", "*.asset" })
            {
                foreach (string path in Directory.GetFiles(UnityEngine.Application.dataPath, pattern, SearchOption.AllDirectories))
                {
                    string text = File.ReadAllText(path);
                    if (text.IndexOf("VelocityModule:", StringComparison.Ordinal) < 0) continue;
                    text = text.Replace("\r\n", "\n");
                    foreach (Match m in module.Matches(text))
                    {
                        scanned++;
                        string block = m.Groups[1].Value;
                        foreach (string[] group in groups)
                        {
                            string first = null;
                            bool mixed = false;
                            foreach (string axis in group)
                            {
                                Match mode = Regex.Match(block, @"\n    " + axis + @":\n      serializedVersion: \d+\n      minMaxState: (\d+)");
                                if (!mode.Success) continue;
                                if (first == null) first = mode.Groups[1].Value;
                                else if (first != mode.Groups[1].Value) mixed = true;
                            }
                            if (mixed) offenders.Add(path.Substring(UnityEngine.Application.dataPath.Length) + ":" + group[0]);
                        }
                    }
                }
            }
            Require(scanned > 0, "[E-5] The particle scan found enabled velocity modules to check.");
            Require(offenders.Count == 0,
                "[E-5] Every enabled particle VelocityModule uses one curve mode per axis group (device error "
                + "\"Particle Velocity curves must all be in the same mode\"): " + string.Join(", ", offenders));
        }
    }
}
