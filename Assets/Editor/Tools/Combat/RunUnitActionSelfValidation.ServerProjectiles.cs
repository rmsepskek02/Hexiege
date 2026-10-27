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
// [2026-10-21 단계 C — 인간 방어타워(CannonTower)]
//   C-1   스위치 토큰 "6,7,17;tower:human" · 투사체 타워 분류(Human AutoTower만, RuneSpire·VineTower 제외).
//   C-2   인간 타워: 공격 순간 발사(피해 0) → 착탄 때 타워 writer로 1회(투사체 번호표, 공격자=건물), Tower 스냅샷 없음,
//         발사 원점 = 포구(Blue +Z · Red −Z), 착탄 시각 = 발사 + (포구→대상 중심 XZ 거리 − 반경) ÷ 타워 속도, 쿨다운 시작.
//   C-3   비행 중 타워 파괴 → 그래도 피해(byTower destroyedThenHit), 비행 중 대상 사망 → TargetLost·피해 0,
//         공격 순간 대상이 죽어 있으면 발사 0·쿨다운 미시작, 서비스가 닫혔으면 발사 거절·쿨다운 미시작.
//   C-4   타워는 건물을 공격하지 않는다(적 건물만 사거리 안 → 발사 0).
//   C-5   RuneSpire(Spirit) 회귀: 즉발 피해·번호표 없음·Tower 스냅샷 1건, 서버 END path tower-Spirit:s0/l1.
//   C-6   화면 소비자 소스 게이트: 건물 공격자 출발점 = 서버 포구 위치, 타워 트레이서·발사 신호·착탄 프리셋 경로.
// [2026-10-21 단계 D — 차지 유닛 InfernoSpirit · FoxMagician]
//   D-1   스위치 토큰 "6,7,12,17,21;tower:human" · 스키마 6(발사 전 취소 통보 RPC).
//   D-2   Inferno: 차지 구간 피해·화상 0 → 1.5초 발사(피해 0) → 착탄 때 직격 1회 + 화상 시작. 차지 END 줄.
//   D-3   Fox: 85/30초 발사 → 착탄 때 1회(번호표, 즉시 표시 없음 — RM-5), foxPrimary 계수.
//   D-4   차지 중 대상 사망 / 공격자 사망 / 이동 명령 → 발사·피해 0 + 사유별 취소 통보 1건 + 차지 표현 취소 질문 true,
//         새 커밋이면 다시 false, 차지 중 사거리 이탈 → 원래 대상 발사, 발사 순간 대상 사망 → 발사 0.
//   D-5   발사 뒤 대상 사망 → TargetLost(피해·화상 0), 발사 뒤 공격자 사망 → 피해 + 화상.
//   D-6   실제 EffectManager: 서버 취소 통보로 차지 VFX 즉시 정지(싱글·Client 같은 회차), 다른 회차 무시, 취소 뒤 차지 신호 차단
//         (lease 미소비), Host 서버 게이트, 비차지 유닛 무동작, 서버 투사체 차지 유닛 타격 marker는 로컬 트레이서 없음.
//   D-7   소스 게이트: 취소 RPC(회차 포함)·화면 연결·RM-5 주석 처리·Fox 시간축 발사 기록.
// [2026-10-27 단계 E — EagleArcher · MushroomBomber (두 유닛 한 번에 — 사용자 「두 유닛 모두 한 번에 테스트」)]
//   E-0   스위치 토큰 "6,7,12,17,21,24,26;tower:human" · 투사체 유닛 7종 모두 ON · 스키마 6 유지 · 두 유닛 resolver 사유 갱신(E-E).
//   E1-1  EagleArcher: 0.2초 발사(피해 0) → 착탄 때 단일 writer 1회(번호표), 착탄 시각 = 발사 + (중심 거리 − 반경) ÷ 속도 6.
//   E1-2  EagleArcher 화살 트레이서 에셋 계약: 팀별 프리셋 2개(Blue = tracerPreset, Red = redTeamTracerPreset), 프리팹 메시 머티리얼이
//         그 팀 유닛의 손 화살 머티리얼과 같음, 착탄·공격·피격 프리셋 비어 있음(U-10), 실제 EffectManager가 팀으로 프리팹을 고름.
//   E2-1  MushroomBomber: 1.0초 발사(피해·독 0) → 착탄 때 주 타깃 직접 피해 1회 + 반경 안 적에게 독(주 타깃 위치 중심), 반경 밖 0.
//   E2-2  스위치 켜진 Mushroom 착탄에서 옛 스냅샷(OnAttackEffectConfirmed) 발행 0건(RM-8 — 이중 착탄 VFX 금지).
//   E2-3  비행 중 공격자 사망 → 피해·독 진행(Persist), 비행 중 대상 소실 → 피해·독 0, TargetLost.
//   E2-4  독 VFX 시작 보류(ProjectileDeathHold E-2) — 독도 화상처럼 화면 착탄까지(상한 0.3초).
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
                Combat = new UnitCombatUseCase(grid, Spawn, BuildingsUseCase, Positions,
                    new ProjectileFixtureMapper());
                var settings = new ProjectileSettings();
                settings.SetUnitSpeed(UnitType.StreamSpirit, streamSpeed);
                // [단계 B] Tank · CannonCart 속도 4(2026-10-20 결정 초기값)와 Castle 반경 0.5(건물 대상 fixture).
                settings.SetUnitSpeed(UnitType.Tank, 4f);
                settings.SetUnitSpeed(UnitType.CannonCart, 4f);
                // [단계 D] 차지 유닛 InfernoSpirit · FoxMagician 속도 5(2026-10-20 결정 초기값).
                settings.SetUnitSpeed(UnitType.InfernoSpirit, 5f);
                settings.SetUnitSpeed(UnitType.FoxMagician, 5f);
                // [단계 E] EagleArcher 6 · MushroomBomber 5(2026-10-20 결정 1-1-2 값 = ProjectileConfig.asset 값).
                settings.SetUnitSpeed(UnitType.EagleArcher, 6f);
                settings.SetUnitSpeed(UnitType.MushroomBomber, 5f);
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
            ValidateServerProjectileDeathPresentationHold();    // [2026-10-26 수정안 (a)] 사망 표현·화상 VFX 화면 착탄 정렬 + 파티클 커브 모드
            ValidateServerProjectileConfigAsset();
            ValidateServerProjectileDiagnosticFormats();       // [단계 B] 분포 상자 · marker 계수 형식
            ValidateServerProjectileStageBSourceGates();       // [단계 B] 의심 1 수정 · 진단 오표기 C
            ValidateServerProjectileStageCSourceGates();       // [단계 C] 타워 화면 소비자 경로
            ValidateServerProjectileStageDSourceGates();       // [단계 D] 취소 통보 전송·화면 연결 · RM-5 · Fox 시간축
            ValidateEagleArcherArrowTracerAssets();            // [단계 E] E1-2 팀별 화살 트레이서 에셋 계약 + 팀 선택(실제 EffectManager)
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
                    // [단계 B] CannonCart(건물 2배 확인 — 공격력 10 → Castle 피해 20) · EagleArcher(단계 E부터 E1-1 투사체 fixture).
                    { UnitType.CannonCart, new UnitStats.StatValues { MaxHp = 400, AttackPower = 10, AttackRange = 3f,
                        DetectRange = 3f, MoveSpeed = .5f, AttackCooldown = 2f, HitFrameTimes = new[] { .4f } } },
                    { UnitType.EagleArcher, new UnitStats.StatValues { MaxHp = 300, AttackPower = 20, AttackRange = 3f,
                        DetectRange = 3f, MoveSpeed = .5f, AttackCooldown = 1.5f, HitFrameTimes = new[] { .2f } } },
                    // [단계 D] 차지 유닛 fixture 값. 타격(=발사) 시각은 운영 값과 같은 1.5 · 85/30 float을 넣고,
                    //   검증은 이 숫자를 다시 쓰지 않고 유닛 데이터의 실제 HitFrameTimes[0]로 tick 한다(ac05e189 교훈).
                    { UnitType.InfernoSpirit, new UnitStats.StatValues { MaxHp = 600, AttackPower = 25, AttackRange = 4f,
                        DetectRange = 4f, MoveSpeed = 1f, AttackCooldown = 3f, HitFrameTimes = new[] { 1.5f } } },
                    { UnitType.FoxMagician, new UnitStats.StatValues { MaxHp = 200, AttackPower = 15, AttackRange = 3f,
                        DetectRange = 3f, MoveSpeed = .5f, AttackCooldown = 4f, HitFrameTimes = new[] { 85f / 30f } } },
                    // [단계 E] MushroomBomber fixture 값. 발사 시각은 운영 값과 같은 1.0초(MushroomBomber_Attack.anim OnAttackHit).
                    //   사거리는 fixture 거리(중심 2.0)에 맞춰 3으로 둔다(운영 2 — 이 검증은 사거리 판정이 아니라 발사·착탄·독을 본다).
                    { UnitType.MushroomBomber, new UnitStats.StatValues { MaxHp = 300, AttackPower = 30, AttackRange = 3f,
                        DetectRange = 3f, MoveSpeed = .5f, AttackCooldown = 3f, HitFrameTimes = new[] { 1f } } },
                });
                ValidateServerProjectileNormalHit();
                ValidateServerProjectileHomingAndSpeed();
                ValidateServerProjectileFlightOutcomes();
                ValidateServerProjectilePrelaunchCancellation();
                ValidateServerProjectileTankAndCannonCart();   // [단계 B]
                ValidateServerProjectileHumanTower();          // [단계 C] 인간 방어타워 투사체 + RuneSpire 즉발 회귀
                ValidateServerProjectileChargeUnits();         // [단계 D] Inferno 화상 착탄 시작 · Fox 숫자 착탄 · 차지 중 취소/사거리 이탈
                ValidateChargeUnitServerCancelPresentation();  // [단계 D] 차지 VFX 즉시 정지(서버 취소 통보) · 취소 뒤 차지 신호 차단
                ValidateServerProjectileEagleArcher();         // [단계 E] E1-1 EagleArcher 단일 타격 발사 → 착탄
                ValidateServerProjectileMushroomBomber();      // [단계 E] E2-1~E2-3 MushroomBomber 착탄 피해 · 범위 독 · 옛 스냅샷 0
            }
            finally
            {
                UnitStats.Initialize(oldStats);
                idField.SetValue(null, oldId);
                NetworkContext.Set(oldServer, oldActive);
            }
            Debug.Log("[UAS-DIAG][SERVER-PROJECTILE] self-validation PASS: stage-E EagleArcher and MushroomBomber (switch token 6,7,12,17,21,24,26;tower:human, all seven projectile units on, schema 6 kept, C3 reasons updated, EagleArcher 0.2 s launch then one single-writer impact at speed 6, team arrow tracer assets (Blue tracerPreset / Red redTeamTracerPreset, unit arrow mesh and team material, no impact preset) and team selection with first-frame facing in the actual EffectManager, MushroomBomber 1.0 s launch then one direct hit plus blast-radius poison centred on the primary target, no legacy writer snapshot at impact (RM-8) with poison provenance kept, attacker-dead Persist hit and poison, in-flight target loss with no poison, poison VFX start held like the burn with separate counters, switch-off legacy fixture retired RM-13), projectile kill-shot death presentation hold (deferred until the kill shot's screen impact, two keys, 0.3 s cap, immediate for melee/non-lethal/ID-reuse/no-visual-root/impact-first, rematch discard, game-end release, diagnostic line), burn VFX start alignment, death-hold source gates (server/domain timing unchanged), particle velocity curve modes, stage-D charge units (switch token 6,7,12,17,21;tower:human, schema 6, InfernoSpirit launch at 1.5 s and FoxMagician at 85/30 s with no damage at launch, one impact hit through the single writer, Inferno burn DoT starting at impact even after the attacker died, Fox number at impact with no rule-19 immediate presentation and foxPrimary counters, charge-time target death / attacker death / move command cancel with one cause-tagged notice and no launch or damage, out-of-range charge still launches at the original target, dead-at-launch never shot, in-flight target loss TargetLost, charge END line), charge VFX stops on the server cancel notice (single same frame, Client same revision only, late charge after cancel blocked without consuming the lease, Host authority gate, non-charge no-op), stage-D source gates (cancel RPC with revision, presenter routing, RM-5 commented out, Fox timeline launch record), stage-C Human CannonTower (switch token 6,7,17;tower:human, muzzle launch Blue +Z / Red -Z, no damage at launch, one tower-writer hit at impact with projectile key and building attacker, no Tower snapshot, cooldown at launch, destroyed-in-flight still hits, target lost in flight, dead target / closed service no launch and no cooldown, never targets buildings), RuneSpire stays instant with its Tower snapshot, tower presenter source gates, kill-shot numbers (unit/building kill shot shows 0 once at the screen impact point after despawn, living target unchanged, timeout uses remembered position, reversed order once, duplicate release once, later projectile on a dead target shows none, text result counters), stage-B switch (Tank, CannonCart, StreamSpirit; EagleArcher etc. off), schema 5 + Mushroom TargetLocked + switch token in handshake hash, Tank unit-target and CannonCart building-target (x2) launch-then-impact through the single writer, diagnostic END formats (server totals/cancel causes/balance, view schedule rejects/order, impact-hold release paths, marker-minus-launch counters, time distributions), suspicion-1 launch cue only on server launch records and NotInvoked timer label (source gates), single launch-without-damage then one surface-timed impact through the single writer with projectile-keyed damage events, fixed homing flight time and speed dependency, target-lost/attacker-dead Persist/close/ID-reuse outcomes, pre-launch attacker/target/move-command cancellation versus out-of-range launch, switch-off legacy timing, presentation schedule (begin/progress/duplicate/late-join/orphan/timeout/abort) and impact-hold release ordering, config asset contract.");
        }

        // ── 분류·스위치·handshake ───────────────────────────────────────────
        private static void ValidateServerProjectilePolicyAndManifest()
        {
            // [단계 E — E-0] 투사체 유닛 7종이 모두 켜졌다. 비투사체(Pistoleer)·파도(TorrentSpirit)는 계속 꺼져 있다(분류 밖).
            UnitType[] switchedOn = { UnitType.Tank, UnitType.CannonCart, UnitType.InfernoSpirit, UnitType.StreamSpirit,
                UnitType.FoxMagician, UnitType.EagleArcher, UnitType.MushroomBomber };
            Require(Array.TrueForAll(switchedOn, ServerProjectileDeliveryPolicy.IsServerProjectile)
                    && !ServerProjectileDeliveryPolicy.IsServerProjectile(UnitType.Pistoleer)
                    && !ServerProjectileDeliveryPolicy.IsServerProjectile(UnitType.TorrentSpirit),
                "[E-0] Stage E switches on EagleArcher and MushroomBomber; all seven projectile units are now on the server projectile path.");
            Require(ServerProjectileDeliveryPolicy.ManifestToken == "6,7,12,17,21,24,26;tower:human",
                "[E-0] Stage E switch token adds EagleArcher (24) and MushroomBomber (26): " + ServerProjectileDeliveryPolicy.ManifestToken);
            Require(ServerProjectileDeliveryPolicy.IsProjectileTower(BuildingType.AutoTower, RaceId.Human)
                    && ServerProjectileDeliveryPolicy.IsServerProjectileTower(BuildingType.AutoTower, RaceId.Human)
                    && !ServerProjectileDeliveryPolicy.IsProjectileTower(BuildingType.AutoTower, RaceId.Spirit)
                    && !ServerProjectileDeliveryPolicy.IsServerProjectileTower(BuildingType.AutoTower, RaceId.Spirit)
                    && !ServerProjectileDeliveryPolicy.IsServerProjectileTower(BuildingType.AutoTower, RaceId.Transcendence)
                    && !ServerProjectileDeliveryPolicy.IsServerProjectileTower(BuildingType.Castle, RaceId.Human),
                "[C-1] Only the Human AutoTower (CannonTower) is a projectile tower; RuneSpire and VineTower stay instant.");
            UnitType[] projectileUnits = { UnitType.Tank, UnitType.CannonCart, UnitType.InfernoSpirit,
                UnitType.StreamSpirit, UnitType.FoxMagician, UnitType.EagleArcher, UnitType.MushroomBomber };
            int projectileCount = 0;
            foreach (UnitType type in UnitAttackShadowProfileResolver.AllManifestTypes)
                if (ServerProjectileDeliveryPolicy.IsProjectileUnit(type)) projectileCount++;
            Require(projectileCount == projectileUnits.Length
                    && Array.TrueForAll(projectileUnits, ServerProjectileDeliveryPolicy.IsProjectileUnit)
                    && !ServerProjectileDeliveryPolicy.IsProjectileUnit(UnitType.TorrentSpirit),
                "Q11 projectile-unit classification is exactly the seven projectile units (Torrent excluded).");
            // [단계 E] 스키마는 6 그대로다 — 스위치 목록·사유 문자열만 바뀌고 RPC 모양은 그대로다(결정 E-E).
            Require(UnitAttackShadowProfileResolver.PresentationContractSchemaRevision == 6,
                "[D-1] The stage D launch-cancel RPC raises the presentation schema to 6 (stage E keeps 6 — no RPC shape change).");
            Require(UnitAttackShadowProfileResolver.TryResolve(UnitType.MushroomBomber, out var mushroom)
                    && mushroom.TargetMode == Hexiege.Application.Combat.Sequencing.AttackTargetMode.TargetLocked
                    && mushroom.Support == Hexiege.Application.Combat.Sequencing.UnitAttackShadowSupport.Unresolved
                    && !mushroom.CanRunSequencer,
                "U-16: MushroomBomber is TargetLocked (homing) while staying C3 Unresolved.");
            // [단계 E — E-E] 두 유닛의 C3 사유 문자열이 지금 사실과 맞는다(근거가 사라진 옛 문자열을 쓰지 않는다).
            //   Eagle = Tank·CannonCart와 같은 단일 타격 투사체, Mushroom = Inferno와 같은 투사체 + 지속 피해. 분류값(C3 Unresolved)은 그대로.
            Require(UnitAttackShadowProfileResolver.TryResolve(UnitType.EagleArcher, out var eagle)
                    && eagle.Reason == "projectile-system-unresolved" && !eagle.HasSecondaryResults
                    && eagle.Support == Hexiege.Application.Combat.Sequencing.UnitAttackShadowSupport.Unresolved
                    && mushroom.Reason == "projectile-and-periodic-result-unresolved" && mushroom.HasSecondaryResults,
                "[E-0] EagleArcher / MushroomBomber C3 reasons are updated (E-E) while their C3 classification is unchanged.");
            Require(UnitAttackShadowProfileResolver.TryComputePresentationProfileHash(out string hash, out _)
                    && hash.Length == 64,
                "Handshake hash still computes with the schema 6 manifest and the switch token.");
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
                        && end.Contains(" switch=6,7,12,17,21,24,26;tower:human ") && end.Contains(" reserved=1 ") && end.Contains(" launched=1 ")
                        && end.Contains(" hit=1 ") && end.Contains(" targetLost=0 ") && end.Contains(" cancelled=0 ")
                        && end.Contains(" cancel=attacker-dead:0|attacker-replaced:0|target-dead:0|target-missing:0|target-replaced:0|move-command:0|invalid-launch-geometry:0|other:0 ")
                        && end.Contains(" flight=n:1|") && end.Contains(" path=17:s1/l0 ")
                        && end.Contains(" balance=ok ") && end.EndsWith(" verdict=PASS"),
                    "Server END line carries whole-match totals, every cancel cause, path counts, balance and verdict: " + end);
                // [단계 C] 건물 공격자 합계 줄 — 유닛만 쏜 경기에서는 모두 0이고 PASS다.
                string towerEnd = f.Combat.BuildProjectileTowerEndLine("fixture");
                Require(towerEnd == "side=server-tower v=1 role=Single epoch=" + f.Combat.ProjectileEpoch.ToString("x16")
                        + " reason=fixture byTower=launched:0|hit:0|targetLost:0|destroyedThenHit:0|destroyedThenLost:0"
                        + " towerPath=Human:s0/l0|Spirit:s0/l0|Transcendence:s0/l0 balance=ok verdict=PASS",
                    "[C-2] Server tower END line format (no tower attacks): " + towerEnd);
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

        // ── [단계 C — C-2~C-5] 인간 방어타워(CannonTower) 투사체 · RuneSpire 즉발 회귀 ─────────────────
        /// <summary>
        /// 실제 TowerCombatUseCase.Tick(→ ExecuteTowerAttack → TryLaunchTowerProjectile)과 실제 투사체 수명 관리자로 돌린다.
        /// 타워 수치(공격력·쿨다운·사거리)는 실제 BuildingStatsConfig.asset 표를 임시 주입해 읽고(하드코딩 금지),
        /// 끝나면 원래 BuildingStats 표 "참조 그대로"를 되돌린다(VfxConnections fixture와 같은 복원 규칙).
        /// </summary>
        private static void ValidateServerProjectileHumanTower()
        {
            var statsField = typeof(BuildingStats).GetField("_data", BindingFlags.Static | BindingFlags.NonPublic);
            Require(statsField != null, "Building stats seam exists for the tower fixture.");
            object oldBuildingStats = statsField.GetValue(null);
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<BuildingStatsConfig>("Assets/_Project/Resources/Config/BuildingStatsConfig.asset");
                Require(asset != null, "Tower fixture resolves the actual BuildingStatsConfig.");
                var table = new Dictionary<(BuildingType, RaceId), BuildingStats.StatValues>();
                foreach (var row in asset.Stats)
                {
                    table[(row.buildingType, RaceId.Human)] = new BuildingStats.StatValues { MaxHp = row.humanMaxHp, GoldCost = row.humanGoldCost,
                        AttackPower = row.humanAttackPower, AttackCooldown = row.humanAttackCooldown, AttackRange = row.humanAttackRange, UpgradeCost = row.upgradeCost };
                    table[(row.buildingType, RaceId.Spirit)] = new BuildingStats.StatValues { MaxHp = row.spiritMaxHp, GoldCost = row.spiritGoldCost,
                        AttackPower = row.spiritAttackPower, AttackCooldown = row.spiritAttackCooldown, AttackRange = row.spiritAttackRange, UpgradeCost = row.upgradeCost };
                    table[(row.buildingType, RaceId.Transcendence)] = new BuildingStats.StatValues { MaxHp = row.transcendenceMaxHp, GoldCost = row.transcendenceGoldCost,
                        AttackPower = row.transcendenceAttackPower, AttackCooldown = row.transcendenceAttackCooldown, AttackRange = row.transcendenceAttackRange, UpgradeCost = row.upgradeCost };
                }
                BuildingStats.Initialize(table);
                int humanPower = BuildingStats.GetAttackPower(BuildingType.AutoTower, RaceId.Human);
                int spiritPower = BuildingStats.GetAttackPower(BuildingType.AutoTower, RaceId.Spirit);
                Require(humanPower > 0 && spiritPower > 0 && BuildingStats.GetAttackRange(BuildingType.AutoTower, RaceId.Human) > 0f,
                    "Actual AutoTower rows give positive tower attack power and range.");

                ValidateHumanTowerLaunchAndImpact(humanPower);
                ValidateHumanTowerRedMuzzle();
                ValidateHumanTowerFlightOutcomes(humanPower);
                ValidateHumanTowerNoLaunchCases();
                ValidateSpiritTowerStaysInstant(spiritPower);
            }
            finally
            {
                // 사본을 다시 만들지 않고 원래 표 참조(null 포함)를 그대로 되돌린다.
                statsField.SetValue(null, oldBuildingStats);
            }
        }

        /// <summary>타워 fixture 한 벌 — ProjectileFixture(대상 = Red LittleKnight, Blue 유닛 1기) 위에 실제 TowerCombatUseCase를 얹는다.</summary>
        private sealed class TowerFixture : IDisposable
        {
            public readonly ProjectileFixture Base;
            public readonly TowerCombatUseCase Tower;
            public readonly BuildingData TowerData;
            public readonly ProjectileSettings Settings;
            public readonly List<AttackEffectPresentation> Snapshots = new List<AttackEffectPresentation>();
            public double Now;
            private readonly IDisposable _snapshots;

            // 타워 위치 (0.6, 0.1, −0.9): Y 0.1 = 건물 Y 오프셋(GameConfig.BuildingYOffset 기본값과 같은 높이).
            //   대상(2.1, 0, 0.1)까지 3D 거리 ≈ 1.81 → 실제 사거리(4타일 × 0.866 + 0.05) 안이다.
            public static readonly Vector3 TowerPosition = new Vector3(.6f, .1f, -.9f);

            public TowerFixture(TeamId towerTeam, RaceId race)
            {
                Base = new ProjectileFixture(UnitType.Tank);
                Settings = new ProjectileSettings(humanTowerSpeed: 4f, humanTowerMuzzleOffset: ProjectileSettings.DefaultHumanTowerMuzzleOffset);
                Settings.SetUnitRadius(UnitType.LittleKnight, .25f);
                Settings.SetUnitRadius(UnitType.Tank, .3f);
                Base.Combat.SetProjectileSettings(Settings);
                Base.Combat.SetWaveClock(() => Now);
                TowerData = Base.BuildingsUseCase.PlaceBuildingWithId(9850, BuildingType.AutoTower, towerTeam, new HexCoord(2, 0), race);
                Require(TowerData != null && TowerData.IsAlive, "Fixture tower is placed.");
                Base.Positions.Buildings[TowerData.Id] = TowerPosition;
                Tower = new TowerCombatUseCase(Base.BuildingsUseCase, Base.Spawn, new ProjectileFixtureMapper(), Base.Positions, _ => race);
                Tower.SetEffectClock(() => Now);
                Tower.SetProjectileLauncher(Base.Combat);
                _snapshots = GameEvents.OnAttackEffectConfirmed.Subscribe(Snapshots.Add);
            }

            /// <summary>타워 공격 판단(서버 Tick) — dt 0이면 쿨다운만 그대로 두고 공격 가능 여부만 본다.</summary>
            public void TickTower(double now, float dt = 0f) { Now = now; Tower.Tick(dt); }

            /// <summary>투사체 진행(발사 뒤 비행·착탄).</summary>
            public void TickProjectiles(double now) { Now = now; Base.Tick(now); }

            public void Dispose() { _snapshots?.Dispose(); Base.Dispose(); }
        }

        private static void ValidateHumanTowerLaunchAndImpact(int humanPower)
        {
            using (var t = new TowerFixture(TeamId.Blue, RaceId.Human))
            {
                UnitData victim = t.Base.Victim;
                int hp = victim.Hp;
                t.TickTower(1d);
                Require(t.Base.Launches.Count == 1 && victim.Hp == hp && t.Base.VictimDamage.Count == 0 && t.Snapshots.Count == 0,
                    "[C-2] Human tower attack launches a server projectile with no damage at launch and no Tower snapshot.");
                Require(Mathf.Approximately(t.TowerData.AttackCooldownRemaining,
                        BuildingStats.GetAttackCooldown(BuildingType.AutoTower, RaceId.Human)),
                    "[C-2] The tower cooldown starts at the launch (same cadence as the old instant attack).");
                ProjectileLaunchPresentation launch = t.Base.Launches[0];
                Vector3 muzzle = TowerFixture.TowerPosition + ProjectileSettings.DefaultHumanTowerMuzzleOffset;   // Blue: 그대로(+Z)
                Require(!launch.AttackerIsUnit && launch.AttackerId == t.TowerData.Id
                        && launch.AttackerKind == (int)BuildingType.AutoTower && launch.AttackerRace == (int)RaceId.Human
                        && launch.TargetId == victim.Id && launch.TargetIsUnit && launch.Homing
                        && (launch.Source - muzzle).sqrMagnitude < 1e-8f && Math.Abs(launch.LaunchTime - 1d) < 1e-9,
                    "[C-2] Launch record: building attacker, Human race, Blue muzzle = tower + (0, 0.405, 0.17), launch at the attack moment: " + launch.Source);
                float centre = new Vector2(2.1f - muzzle.x, .1f - muzzle.z).magnitude;
                double expectedImpact = 1d + (centre - .25f) / 4d;
                Require(Math.Abs(launch.ImpactTime - expectedImpact) < 1e-5 && Mathf.Approximately(launch.TargetRadius, .25f),
                    "[C-2] Impact time = launch + (muzzle-to-centre XZ distance - target radius) / tower speed 4.");
                // ⚠️ 진행 시각은 발사 기록의 실제 착탄 시각(서버가 float 거리로 계산한 double 값)을 그대로 쓴다.
                //   위 expectedImpact(이 검증이 따로 계산한 값)와는 1e-7 안쪽으로 다를 수 있어, 그것으로 Tick 하면
                //   운영 코드의 "now < ImpactTime" 판정에 걸려 착탄이 안 날 수 있다(ac05e189 교훈 — 데이터 값으로 tick).
                double impactAt = launch.ImpactTime;
                t.TickProjectiles(impactAt - .001d);
                Require(t.Base.Impacts.Count == 0 && victim.Hp == hp, "[C-2] No tower damage before the authoritative impact.");
                // 쿨다운 동안 타워가 다시 쏘지 않는다(Tick이 와도 같은 쿨다운 규칙).
                t.TickTower(impactAt - .0005d, .01f);
                Require(t.Base.Launches.Count == 1, "[C-2] The tower does not fire again while its cooldown runs.");
                t.TickProjectiles(impactAt);
                int expected = DamageCalculator.ApplyDefense(humanPower, victim.Defense);
                Require(t.Base.Impacts.Count == 1 && t.Base.Impacts[0].Outcome == ProjectileImpactOutcome.Hit
                        && victim.Hp == hp - expected && t.Base.Impacts[0].AppliedAmount == expected
                        && t.Base.VictimDamage.Count == 1,
                    "[C-2] At impact the tower writer applies the launch-time tower power once (defence applied).");
                EntityDamagedEvent damage = t.Base.VictimDamage[0];
                Require(damage.ProjectilePresentationKey.Equals(launch.Key) && !damage.AttackerIsUnit
                        && damage.AttackerId == t.TowerData.Id && !damage.ImmediatePresentation,
                    "[C-2] The impact damage event carries the projectile key and the building attacker (number waits for the visual impact).");
                Require(t.Snapshots.Count == 0, "[C-2] No Tower attack snapshot is published for the projectile tower (launch cue comes from the presenter).");
                string end = t.Base.Combat.BuildProjectileEndLine("fixture");
                Require(end.Contains(" path=tower-Human:s1/l0 ") && end.Contains(" reserved=1 ") && end.Contains(" launched=1 ")
                        && end.Contains(" hit=1 ") && end.Contains(" balance=ok ") && end.EndsWith(" verdict=PASS"),
                    "[C-2] Server END path separates the building attacker (tower-Human): " + end);
                string towerEnd = t.Base.Combat.BuildProjectileTowerEndLine("fixture");
                Require(towerEnd.Contains(" byTower=launched:1|hit:1|targetLost:0|destroyedThenHit:0|destroyedThenLost:0 ")
                        && towerEnd.Contains(" towerPath=Human:s1/l0|Spirit:s0/l0|Transcendence:s0/l0 ")
                        && towerEnd.EndsWith(" balance=ok verdict=PASS"),
                    "[C-2] Server tower END line counts the building-attacker launch and hit: " + towerEnd);
            }
        }

        private static void ValidateHumanTowerRedMuzzle()
        {
            using (var t = new TowerFixture(TeamId.Red, RaceId.Human))
            {
                // Red 타워는 Blue 유닛(fixture 공격자 자리, (0.1, 0, 0.1))을 노린다. 포구는 180° 돌아 −Z 쪽이다.
                t.TickTower(2d);
                Require(t.Base.Launches.Count == 1 && t.Base.Launches[0].TargetId == t.Base.Attacker.Id,
                    "[C-2] A Red Human tower targets the enemy (Blue) unit.");
                Vector3 offset = ProjectileSettings.DefaultHumanTowerMuzzleOffset;
                Vector3 muzzle = new Vector3(TowerFixture.TowerPosition.x - offset.x, TowerFixture.TowerPosition.y + offset.y,
                    TowerFixture.TowerPosition.z - offset.z);
                Require((t.Base.Launches[0].Source - muzzle).sqrMagnitude < 1e-8f && t.Base.Launches[0].Team == TeamId.Red,
                    "[C-2] Red tower muzzle = tower + (−x, y, −z) offset (the Red tower stands rotated 180°): " + t.Base.Launches[0].Source);
            }
        }

        private static void ValidateHumanTowerFlightOutcomes(int humanPower)
        {
            using (var t = new TowerFixture(TeamId.Blue, RaceId.Human))
            {
                // 비행 중 타워 파괴 → 이미 쏜 포탄은 맞으면 피해(공격자 사망과 같음 — U-6).
                UnitData victim = t.Base.Victim;
                int hp = victim.Hp;
                t.TickTower(1d);
                double impact = t.Base.Launches[0].ImpactTime;
                t.TowerData.TakeDamage(t.TowerData.Hp);
                t.Base.BuildingsUseCase.RemoveBuilding(t.TowerData.Id);
                t.TickProjectiles((1d + impact) * .5d);
                t.TickProjectiles(impact);
                Require(t.Base.Impacts.Count == 1 && t.Base.Impacts[0].Outcome == ProjectileImpactOutcome.Hit
                        && victim.Hp == hp - DamageCalculator.ApplyDefense(humanPower, victim.Defense),
                    "[C-3] A tower destroyed in flight still damages on impact with the launch-time power.");
                string end = t.Base.Combat.BuildProjectileEndLine("fixture");
                string towerEnd = t.Base.Combat.BuildProjectileTowerEndLine("fixture");
                Require(towerEnd.Contains("|destroyedThenHit:1|destroyedThenLost:0 ") && end.Contains(" attackerDead=hit:1|lost:0 ")
                        && end.Contains(" balance=ok ") && towerEnd.EndsWith(" balance=ok verdict=PASS"),
                    "[C-3] Server END lines count the hit after the tower was destroyed: " + end + " / " + towerEnd);
            }
            using (var t = new TowerFixture(TeamId.Blue, RaceId.Human))
            {
                // 비행 중 대상 사망(다른 원인) → 착탄 이펙트만, 피해·숫자 없음(Q4).
                t.TickTower(1d);
                t.Base.Victim.TakeDamage(t.Base.Victim.Hp);
                t.TickProjectiles(t.Base.Launches[0].ImpactTime);
                Require(t.Base.Impacts.Count == 1 && t.Base.Impacts[0].Outcome == ProjectileImpactOutcome.TargetLost
                        && t.Base.VictimDamage.Count == 0,
                    "[C-3] A target lost while the cannonball flies takes no damage; the result is TargetLost.");
                Require(t.Base.Combat.BuildProjectileTowerEndLine("fixture").Contains(" byTower=launched:1|hit:0|targetLost:1|"),
                    "[C-3] Server tower END line counts the tower target-lost result.");
            }
            using (var t = new TowerFixture(TeamId.Blue, RaceId.Human))
            {
                // 게임 종료 닫기 → 비행 중 포탄 피해 없음(위험 7).
                int hp = t.Base.Victim.Hp;
                t.TickTower(1d);
                t.Base.Combat.CloseProjectiles("game-end");
                t.TickProjectiles(5d);
                Require(t.Base.Impacts.Count == 0 && t.Base.Victim.Hp == hp,
                    "[C-3] Closing the service cancels the in-flight tower projectile without damage.");
            }
        }

        private static void ValidateHumanTowerNoLaunchCases()
        {
            using (var t = new TowerFixture(TeamId.Blue, RaceId.Human))
            {
                // 공격 순간 대상이 이미 죽어 있으면 쏘지 않는다(Q15) — 쿨다운도 시작하지 않는다(공격 자체가 없었다).
                t.Base.Victim.TakeDamage(t.Base.Victim.Hp);
                t.TickTower(1d);
                Require(t.Base.Launches.Count == 0 && t.TowerData.AttackCooldownRemaining <= 0f,
                    "[C-3] A dead target is never shot and the tower cooldown does not start.");
            }
            using (var t = new TowerFixture(TeamId.Blue, RaceId.Human))
            {
                // 서비스가 닫혔으면(게임 종료 뒤) 발사를 거절하고 쿨다운도 시작하지 않는다(유닛 커밋 거절과 같은 원칙).
                t.Base.Combat.CloseProjectiles("game-end");
                int hp = t.Base.Victim.Hp;
                t.TickTower(1d);
                Require(t.Base.Launches.Count == 0 && t.Base.Victim.Hp == hp && t.TowerData.AttackCooldownRemaining <= 0f,
                    "[C-3] A closed projectile service refuses the tower launch without instant fallback damage or cooldown.");
            }
            using (var t = new TowerFixture(TeamId.Blue, RaceId.Human))
            {
                // 타워는 건물을 공격하지 않는다(방어 타워 규칙 1) — 사거리 안에 적 건물만 있으면 발사 0.
                t.Base.Spawn.RemoveUnit(t.Base.Victim.Id);
                BuildingData enemy = t.Base.BuildingsUseCase.PlaceBuildingWithId(9851, BuildingType.Castle, TeamId.Red, new HexCoord(1, 0));
                Require(enemy != null, "Fixture enemy building is placed.");
                t.Base.Positions.Buildings[enemy.Id] = new Vector3(1.2f, 0f, -.4f);
                t.TickTower(1d);
                Require(t.Base.Launches.Count == 0, "[C-4] The tower never targets buildings (units only), so nothing is launched.");
            }
        }

        private static void ValidateSpiritTowerStaysInstant(int spiritPower)
        {
            using (var t = new TowerFixture(TeamId.Blue, RaceId.Spirit))
            {
                // RuneSpire(Spirit AutoTower)는 투사체가 아니다 — 공격 순간 즉발 피해 + Tower 스냅샷(번개) 그대로.
                UnitData victim = t.Base.Victim;
                int hp = victim.Hp;
                t.TickTower(1d);
                int expected = DamageCalculator.ApplyDefense(spiritPower, victim.Defense);
                Require(t.Base.Launches.Count == 0 && victim.Hp == hp - expected && t.Base.VictimDamage.Count == 1
                        && !t.Base.VictimDamage[0].ProjectilePresentationKey.IsValid && !t.Base.VictimDamage[0].AttackerIsUnit,
                    "[C-5] RuneSpire keeps instant damage with no projectile record or key.");
                Require(t.Snapshots.Count == 1 && t.Snapshots[0].Visual == AttackEffectVisual.Tower && t.Snapshots[0].Race == RaceId.Spirit,
                    "[C-5] RuneSpire still publishes its Tower attack snapshot (bolt presentation unchanged).");
                Require(Mathf.Approximately(t.TowerData.AttackCooldownRemaining,
                        BuildingStats.GetAttackCooldown(BuildingType.AutoTower, RaceId.Spirit)),
                    "[C-5] RuneSpire cooldown cadence is unchanged.");
                string end = t.Base.Combat.BuildProjectileEndLine("fixture");
                Require(end.Contains(" path=tower-Spirit:s0/l1 ") && end.Contains(" reserved=0 ") && end.Contains(" launched=0 ")
                        && t.Base.Combat.BuildProjectileTowerEndLine("fixture").Contains(" towerPath=Human:s0/l0|Spirit:s0/l1|Transcendence:s0/l0 "),
                    "[C-5] Server END shows the RuneSpire attack on the instant path: " + end);
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
            // [단계 C] 인간 타워 포구(발사 원점) — 에셋 값이 측정값(CannonTower 메시 포신 끝)과 같고 설정 묶음으로 그대로 넘어간다.
            Require((config.HumanTowerMuzzleOffset - ProjectileSettings.DefaultHumanTowerMuzzleOffset).sqrMagnitude < 1e-8f
                    && (settings.HumanTowerMuzzleOffset - config.HumanTowerMuzzleOffset).sqrMagnitude < 1e-8f,
                "[C-2] ProjectileConfig carries the measured CannonTower muzzle offset (0, 0.405, 0.17) into the server settings "
                + "(update this check when the Inspector value is tuned): " + config.HumanTowerMuzzleOffset);
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
            //   [2026-10-27 단계 E] EagleArcher는 이제 스위치 ON이라 실제 경기에서는 아래 "옛 경로" 입력이 나오지 않는다.
            //     이 검사는 END 줄 칸(path=..|..:s0/l1 · legacyTracer=)의 "형식"을 보는 것이고, 계수 함수는 스위치를 보지 않으므로
            //     (ServerProjectileDiagnostics — 투사체 분류만 확인) 가상 입력으로 그대로 둔다. 옛 경로 칸은 RM-9 스위치 제거 때 함께 정리한다.
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

        // ── [단계 C — C-6] 타워 화면 소비자 경로 소스 게이트 ─────────────────────────
        private static void ValidateServerProjectileStageCSourceGates()
        {
            string Read(string relative)
            {
                string path = Path.Combine(UnityEngine.Application.dataPath, relative);
                Require(File.Exists(path), "Stage C source gate file exists: " + relative);
                return File.ReadAllText(path);
            }
            string Active(string source)
            {
                var kept = new System.Text.StringBuilder(source.Length);
                foreach (string line in source.Split('\n'))
                    if (!line.TrimStart().StartsWith("//", StringComparison.Ordinal)) kept.Append(line).Append('\n');
                return kept.ToString();
            }
            string presenter = Active(Read("_Project/Scripts/Presentation/Effects/ProjectilePresentationPresenter.cs"));
            string tower = Active(Read("_Project/Scripts/Application/UseCases/TowerCombatUseCase.cs"));
            Require(presenter.Contains("visual.Start = ViewConverter.ToView(launch.Source);")
                    && presenter.Contains("effects.BeginServerTowerProjectile(visual.TowerType, visual.TowerRace, visual.Start)")
                    && presenter.Contains("effects.PlayServerTowerLaunchCue(visual.TowerType, visual.TowerRace, visual.Start,")
                    && presenter.Contains("!launch.AttackerIsUnit && !midFlight && effects != null")
                    && presenter.Contains("effects.PlayServerTowerProjectileImpact(visual.TowerType, visual.TowerRace, point, point - visual.Start)"),
                "[C-6] Tower projectiles start at the server muzzle, use the tower tracer/impact presets and emit the muzzle launch cue only for non-mid-flight begins.");
            Require(tower.Contains("ServerProjectileDeliveryPolicy.IsServerProjectileTower(tower.Type, projectileRace)")
                    && tower.Contains("_projectiles.TryLaunchTowerProjectile(tower, projectileRace, target, launchPower, this)")
                    && tower.Contains("projectilePresentationKey: key"),
                "[C-6] The tower writer branches only projectile towers to the server launch and tags impact damage with the projectile key.");
        }

        // ── [2026-10-21 단계 D — D-2~D-5] 차지 유닛 InfernoSpirit · FoxMagician 서버 투사체 ─────────────────────
        /// <summary>
        /// 실제 UnitCombatUseCase(TryAttack → 발사 예약 → TickProjectiles)로 차지 유닛의 서버 판정을 확인한다.
        ///   D-2 Inferno: 차지 구간(커밋~1.5초)에는 발사·피해 없음 → 1.5초 발사(피해 0, 화상 없음) → 착탄 때 직격 1회 + 화상 DoT 시작.
        ///   D-3 Fox: 85/30초 발사 → 착탄 때 1회, 피해 이벤트에 투사체 번호표·즉시 표시 없음(규칙 19 예외 제거 RM-5), foxPrimary 계수.
        ///   D-4 차지 중 끊김: 대상 사망 / 공격자 사망 / 이동 명령 → 발사 0 · 피해 0 · 취소 통보 1건(사유별) · 차지 표현 취소 질문 true.
        ///       차지 중 사거리 이탈 → 원래 대상에게 발사·피해, 통보 0. 발사 순간 대상 사망 → 발사 0.
        ///   D-5 발사 뒤 대상 사망 → TargetLost(피해·화상 없음), 발사 뒤 공격자 사망 → 피해 + 화상.
        /// 타격 시각·착탄 시각은 유닛 데이터(HitFrameTimes)와 발사 기록(ImpactTime)의 실제 값으로 tick 한다(하드코딩 금지).
        /// </summary>
        private static void ValidateServerProjectileChargeUnits()
        {
            // D-2 Inferno 정상: 발사 때 피해·화상 0 → 착탄 때 직격 1회 + 화상 시작.
            using (var f = new ProjectileFixture(UnitType.InfernoSpirit))
            {
                var cancels = new List<ProjectileLaunchCancelPresentation>();
                f.Combat.ProjectileLaunchCancelled += cancels.Add;
                int hp = f.Victim.Hp;
                Require(f.Combat.TryAttack(f.Attacker, 0d).HasValue && f.Combat.HasPendingProjectileLaunch(f.Attacker),
                    "[D-2] InfernoSpirit (switch ON) commit creates a launch reservation instead of a hit-time damage timer.");
                double hitAt = f.Attacker.HitFrameTimes[0];
                f.Tick(hitAt * .5d);   // 차지 구간 한가운데
                f.Combat.TickTimedEffects(1f);
                Require(f.Launches.Count == 0 && f.Victim.Hp == hp && f.VictimDamage.Count == 0,
                    "[D-2] During the charge window nothing is launched and no damage or burn is applied.");
                f.Tick(hitAt);
                Require(f.Launches.Count == 1 && f.Victim.Hp == hp && f.VictimDamage.Count == 0
                        && Math.Abs(f.Launches[0].LaunchTime - hitAt) < 1e-9 && f.Launches[0].AttackerKind == (int)UnitType.InfernoSpirit,
                    "[D-2] InfernoSpirit launches at its OnAttackHit offset (1.5 s) with no damage at launch.");
                ProjectileLaunchPresentation launch = f.Launches[0];
                Require(Math.Abs(launch.ImpactTime - (hitAt + (2.0d - .25d) / 5d)) < 1e-5,
                    "[D-2] Inferno impact time = launch + (centre distance - target radius) / speed 5.");
                f.Combat.TickTimedEffects(1f);
                Require(f.Victim.Hp == hp && f.VictimDamage.Count == 0,
                    "[D-2] The burn DoT does not exist while the fireball is still flying.");
                f.Tick(launch.ImpactTime);
                Require(f.Impacts.Count == 1 && f.Impacts[0].Outcome == ProjectileImpactOutcome.Hit
                        && f.VictimDamage.Count == 1 && f.Victim.Hp == hp - 25
                        && f.VictimDamage[0].ProjectilePresentationKey.Equals(launch.Key)
                        && !f.VictimDamage[0].ImmediatePresentation,
                    "[D-2] At impact exactly one direct hit goes through the single writer with the projectile key.");
                int afterImpact = f.Victim.Hp;
                f.Combat.TickTimedEffects(1f);
                Require(f.Victim.Hp < afterImpact,
                    "[D-2] The Inferno burn DoT starts at the impact (first one-second tick lowers HP after the impact, never before).");
                Require(cancels.Count == 0 && !f.Combat.IsChargePresentationCancelled(f.Attacker),
                    "[D-2] A launched Inferno attack sends no cancel notice and is not reported as a cancelled charge.");
                string charge = f.Combat.BuildProjectileChargeEndLine("fixture");
                Require(charge.StartsWith("side=server-charge v=1 role=Single epoch=") && charge.Contains(" reason=fixture ")
                        && charge.Contains(" inferno=reserved:1|launched:1|hit:1|targetLost:0 cancel12=attacker-dead:0|")
                        && charge.Contains(" fox=reserved:0|launched:0|hit:0|targetLost:0 cancel21=")
                        && charge.EndsWith(" foxPrimary=all:0|projectileKeyed:0|immediate:0|oldRuleImmediate:0 verdict=PASS"),
                    "[D-2] Server charge END line counts the Inferno reservation, launch and hit: " + charge);
            }

            // D-3 Fox: 착탄 때 숫자(번호표) — 즉시 표시 예외 없음.
            using (var f = new ProjectileFixture(UnitType.FoxMagician))
            {
                int hp = f.Victim.Hp;
                Require(f.Combat.TryAttack(f.Attacker, 0d).HasValue, "[D-3] FoxMagician commit succeeds.");
                double hitAt = f.Attacker.HitFrameTimes[0];
                f.Tick(1d);   // Fox 차지 신호(1초) 무렵 — 차지는 발사가 아니다
                Require(f.Launches.Count == 0 && f.Combat.HasPendingProjectileLaunch(f.Attacker),
                    "[D-3] The Fox charge moment (1 s) is not a launch; the reservation stays pending.");
                f.Tick(hitAt);
                Require(f.Launches.Count == 1 && f.Victim.Hp == hp && Math.Abs(f.Launches[0].LaunchTime - hitAt) < 1e-9,
                    "[D-3] FoxMagician launches at its 85/30 s OnAttackHit offset with no damage at launch.");
                f.Tick(f.Launches[0].ImpactTime);
                Require(f.Impacts.Count == 1 && f.VictimDamage.Count == 1 && f.Victim.Hp == hp - 15
                        && f.VictimDamage[0].ProjectilePresentationKey.Equals(f.Launches[0].Key)
                        && !f.VictimDamage[0].ImmediatePresentation,
                    "[D-3] RM-5: Fox damage is applied at impact with the projectile key and without the rule-19 immediate presentation.");
                string charge = f.Combat.BuildProjectileChargeEndLine("fixture");
                Require(charge.Contains(" fox=reserved:1|launched:1|hit:1|targetLost:0 ")
                        && charge.EndsWith(" foxPrimary=all:1|projectileKeyed:1|immediate:0|oldRuleImmediate:1 verdict=PASS"),
                    "[D-3] Server charge END confirms the Fox immediate presentation is off (immediate 0, old rule would have been 1): " + charge);
            }

            // D-4 차지 중 끊김 3종 — 대상 사망 / 공격자 사망 / 이동 명령.
            foreach (string cut in new[] { "target-dead", "attacker-dead", "move-command" })
            foreach (UnitType type in new[] { UnitType.InfernoSpirit, UnitType.FoxMagician })
            using (var f = new ProjectileFixture(type))
            {
                var cancels = new List<ProjectileLaunchCancelPresentation>();
                f.Combat.ProjectileLaunchCancelled += cancels.Add;
                int hp = f.Victim.Hp;
                f.Combat.TryAttack(f.Attacker, 0d);
                double hitAt = f.Attacker.HitFrameTimes[0];
                f.Tick(hitAt * .5d);   // 차지 중
                if (cut == "target-dead") f.Victim.TakeDamage(f.Victim.Hp);
                else if (cut == "attacker-dead") f.Attacker.TakeDamage(f.Attacker.Hp);
                else f.Combat.CancelProjectileLaunches(f.Attacker, "move-command");
                f.Tick(hitAt * .6d);
                ProjectileLaunchCancelCause expected = cut == "target-dead" ? ProjectileLaunchCancelCause.TargetDead
                    : cut == "attacker-dead" ? ProjectileLaunchCancelCause.AttackerDead : ProjectileLaunchCancelCause.MoveCommand;
                Require(cancels.Count == 1 && cancels[0].IsValid && cancels[0].Cause == expected
                        && cancels[0].AttackerId == f.Attacker.Id && cancels[0].AttackerKind == (int)type
                        && cancels[0].Key.Epoch == f.Combat.ProjectileEpoch && cancels[0].PresentationRevision == 0UL,
                    "[D-4] " + type + " " + cut + " during the charge publishes exactly one server cancel notice with its cause.");
                Require(f.Combat.IsChargePresentationCancelled(f.Attacker),
                    "[D-4] " + type + " " + cut + ": the authority reports the latest charge as cancelled (no later charge VFX).");
                f.Tick(hitAt);
                f.Tick(hitAt + 2d);
                int expectedHp = cut == "target-dead" ? 0 : hp;
                Require(f.Launches.Count == 0 && f.Impacts.Count == 0 && f.VictimDamage.Count == 0 && f.Victim.Hp == expectedHp,
                    "[D-4] " + type + " " + cut + " before launch: no launch, no result, no damage.");
                string charge = f.Combat.BuildProjectileChargeEndLine("fixture");
                string slot = type == UnitType.InfernoSpirit ? "cancel12=" : "cancel21=";
                Require(charge.Contains(slot) && charge.Substring(charge.IndexOf(slot, StringComparison.Ordinal)).Contains(cut + ":1"),
                    "[D-4] Server charge END counts the charge-time cancel under its own cause (" + cut + "): " + charge);
                Require(f.Combat.BuildProjectileEndLine("fixture").Contains(" balance=ok "), "[D-4] Server END balance stays ok after the cancel.");
            }

            // D-4 새 커밋이 오면 차지 표현 취소 질문은 다시 false(다음 공격의 차지는 정상).
            using (var f = new ProjectileFixture(UnitType.InfernoSpirit))
            {
                f.Combat.TryAttack(f.Attacker, 0d);
                f.Victim.TakeDamage(f.Victim.Hp);
                f.Tick(.3d);
                Require(f.Combat.IsChargePresentationCancelled(f.Attacker), "[D-4] Cancelled charge is reported.");
                UnitData next = f.Spawn.SpawnUnitWithId(9702, UnitType.LittleKnight, TeamId.Red, new HexCoord(1, 1));
                f.Positions.Units[next.Id] = new Vector3(1.6f, 0f, .9f);
                Require(f.Combat.TryReserveProjectileLaunch(f.Attacker, next.Id, true, 4.5d, 0)
                        && !f.Combat.IsChargePresentationCancelled(f.Attacker),
                    "[D-4] A new commit (new reservation) clears the cancelled-charge answer so the next charge plays.");
            }

            // D-4 차지 중 사거리 이탈 → 끊김이 아니다: 원래 대상에게 발사, 통보 0.
            using (var f = new ProjectileFixture(UnitType.InfernoSpirit))
            {
                var cancels = new List<ProjectileLaunchCancelPresentation>();
                f.Combat.ProjectileLaunchCancelled += cancels.Add;
                int hp = f.Victim.Hp;
                f.Combat.TryAttack(f.Attacker, 0d);
                double hitAt = f.Attacker.HitFrameTimes[0];
                f.Positions.Units[f.Victim.Id] = new Vector3(9.1f, 0f, .1f);   // 차지 중 사거리(4) 밖으로
                f.Tick(hitAt * .5d);
                Require(f.Combat.HasPendingProjectileLaunch(f.Attacker) && cancels.Count == 0,
                    "[D-4] Leaving range during the charge keeps the reservation pending and sends no cancel notice.");
                f.Tick(hitAt);
                Require(f.Launches.Count == 1 && f.Launches[0].TargetId == f.Victim.Id,
                    "[D-4] The out-of-range original target is still shot at the hit offset.");
                f.Tick(f.Launches[0].ImpactTime);
                Require(f.Impacts.Count == 1 && f.Victim.Hp == hp - 25 && cancels.Count == 0
                        && !f.Combat.IsChargePresentationCancelled(f.Attacker),
                    "[D-4] It hits once; the charge was never cancelled.");
            }

            // D-4 발사 순간 대상 사망 → 발사 안 함(Q15).
            using (var f = new ProjectileFixture(UnitType.FoxMagician))
            {
                var cancels = new List<ProjectileLaunchCancelPresentation>();
                f.Combat.ProjectileLaunchCancelled += cancels.Add;
                f.Combat.TryAttack(f.Attacker, 0d);
                double hitAt = f.Attacker.HitFrameTimes[0];
                f.Tick(hitAt - .01d);
                f.Victim.TakeDamage(f.Victim.Hp);
                f.Tick(hitAt);
                Require(f.Launches.Count == 0 && cancels.Count == 1 && cancels[0].Cause == ProjectileLaunchCancelCause.TargetDead,
                    "[D-4] A target dead at the launch moment is never shot; the cancel notice says target-dead.");
            }

            // D-5 발사 뒤 대상 사망 → TargetLost(피해·화상 없음) / 발사 뒤 공격자 사망 → 피해 + 화상.
            using (var f = new ProjectileFixture(UnitType.InfernoSpirit))
            {
                f.Combat.TryAttack(f.Attacker, 0d);
                f.Tick(f.Attacker.HitFrameTimes[0]);
                f.Victim.TakeDamage(f.Victim.Hp);
                f.Tick(f.Launches[0].ImpactTime);
                Require(f.Impacts.Count == 1 && f.Impacts[0].Outcome == ProjectileImpactOutcome.TargetLost && f.VictimDamage.Count == 0,
                    "[D-5] A target lost while the fireball flies takes no damage and no burn; the result is TargetLost.");
                Require(f.Combat.BuildProjectileChargeEndLine("fixture").Contains(" inferno=reserved:1|launched:1|hit:0|targetLost:1 "),
                    "[D-5] Server charge END counts the in-flight target loss.");
            }
            using (var f = new ProjectileFixture(UnitType.InfernoSpirit))
            {
                int hp = f.Victim.Hp;
                f.Combat.TryAttack(f.Attacker, 0d);
                f.Tick(f.Attacker.HitFrameTimes[0]);
                f.Attacker.TakeDamage(f.Attacker.Hp);
                f.Spawn.RemoveUnit(f.Attacker.Id);
                f.Tick(f.Launches[0].ImpactTime);
                Require(f.Impacts.Count == 1 && f.Impacts[0].Outcome == ProjectileImpactOutcome.Hit && f.Victim.Hp == hp - 25,
                    "[D-5] Q3 Persist: a launched fireball still damages after the Inferno died.");
                int afterImpact = f.Victim.Hp;
                f.Combat.TickTimedEffects(1f);
                Require(f.Victim.Hp < afterImpact, "[D-5] The burn still starts at impact after the attacker died (InfernoAttackBehavior Persist).");
            }
        }

        // ── [단계 D — D-6] 차지 VFX 즉시 정지(서버 취소 통보)와 취소 뒤 차지 신호 차단 — 실제 EffectManager · UnitView ─────────
        /// <summary>
        /// 실제 UnitEffectConfig 프리셋과 EffectManager로, 서버 취소 통보가 차지 VFX를 끄는지 세 실행 형태에서 확인한다.
        ///   싱글: 실제 UseCase 예약 → 차지 재생 → 차지 중 대상 사망 → 취소 통보로 즉시 정지 → 같은 공격의 다음 차지 신호도 안 켜짐 →
        ///         타격 marker는 트레이서를 쏘지 않는다(서버 발사 기록이 없으므로 화면 소비자도 그리지 않는다).
        ///   Client: 회차가 다른 통보는 무시(더 새 공격 보호), 같은 회차면 정지, 차지 신호 전에 온 통보는 그 회차 차지를 막는다(lease 미소비).
        ///   Host: lease가 장전돼 있어도 서버 UseCase가 취소한 공격의 차지는 켜지 않고, 새 예약이 오면 다시 켠다.
        ///   비차지 유닛: 통보를 받아도 할 일 없음(NotChargeUnit).
        /// 화면 소비자의 실제 연결(Presenter → ApplyServerProjectileLaunchCancel)은 단계 D 소스 게이트가 확인한다.
        /// </summary>
        private static void ValidateChargeUnitServerCancelPresentation()
        {
            var units = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.UnitEffectConfig>("Assets/_Project/Resources/Config/UnitEffectConfig.asset");
            var buildings = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.BuildingEffectConfig>("Assets/_Project/Resources/Config/BuildingEffectConfig.asset");
            Require(units != null && buildings != null, "[D-6] VFX production configs exist.");
            units.Initialize(); buildings.Initialize();
            var effectSingleton = typeof(Hexiege.Presentation.EffectManager).GetField(
                "<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var audioSingleton = typeof(Hexiege.Core.SingletonMonoBehaviour<Hexiege.Presentation.AudioManager>).GetField(
                "<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            Require(effectSingleton != null && audioSingleton != null, "[D-6] Fixture singleton isolation seams exist.");
            object oldEffect = effectSingleton.GetValue(null), oldAudio = audioSingleton.GetValue(null);
            bool oldActive = NetworkContext.IsNetworkActive, oldServer = NetworkContext.IsNetworkServer;
            var root = new GameObject("ChargeCancelFixture") { hideFlags = HideFlags.HideAndDontSave };
            var casters = new List<GameObject>();
            var fixtures = new List<ProjectileFixture>();
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var viewType = typeof(Hexiege.Presentation.UnitView);
            try
            {
                var manager = root.AddComponent<Hexiege.Presentation.EffectManager>();
                typeof(Hexiege.Presentation.EffectManager).GetField("_vfxContainer", fields).SetValue(manager, root.transform);
                manager.Initialize(units, buildings, null);
                effectSingleton.SetValue(null, manager); audioSingleton.SetValue(null, null);

                int Active(GameObject prefab)
                {
                    int count = 0;
                    foreach (var item in root.GetComponentsInChildren<Hexiege.Presentation.VfxPoolItem>(true))
                        if (item.SourcePrefab == prefab && item.gameObject.activeSelf) count++;
                    return count;
                }
                int ActiveTracers()
                {
                    int count = 0;
                    foreach (var t in root.GetComponentsInChildren<Hexiege.Presentation.TracerProjectile>(true))
                        if (t.gameObject.activeSelf) count++;
                    return count;
                }
                Hexiege.Presentation.UnitView View(UnitData data, UnitCombatUseCase combat, bool chargeUnit)
                {
                    var go = new GameObject("ChargeCancel" + data.Type) { hideFlags = HideFlags.HideAndDontSave };
                    casters.Add(go);
                    var view = go.AddComponent<Hexiege.Presentation.UnitView>();
                    viewType.GetField("_unitData", fields).SetValue(view, data);
                    viewType.GetField("_combatUseCase", fields).SetValue(view, combat);
                    // Animator가 없는 fixture라 클립 이벤트 판별(P2-1)을 계산할 수 없다 → 판별값만 직접 연다(클립 집합은 T-4가 고정).
                    viewType.GetField("_hasAttackChargeMarker", fields).SetValue(view, chargeUnit);
                    viewType.GetField("_attackChargeMarkerCacheResolved", fields).SetValue(view, true);
                    return view;
                }
                void Commit(Hexiege.Presentation.UnitView view, ulong revision)
                {
                    // 서버 커밋 1회 흉내: 수락 회차 + source lease 1회분 + LegacyFallback(두 차지 유닛의 현재 모드).
                    var lease = new Hexiege.Application.Combat.Sequencing.AttackPresentationSourceMarkerLease();
                    Require(lease.TryArm(revision, 1), "[D-6] Fixture source lease must arm.");
                    viewType.GetField("_attackPresentationSourceMarkerLease", fields).SetValue(view, lease);
                    viewType.GetField("_attackPresentationImpactMode", fields).SetValue(view, Hexiege.Application.Combat.Sequencing.AttackPresentationImpactMode.LegacyFallback);
                    viewType.GetField("_lastAttackPresentationRevision", fields).SetValue(view, revision);
                }
                var charge = units.GetAttack(UnitType.InfernoSpirit);
                Require(charge?.VfxPrefab != null && charge.StopWhenCasterDies, "[D-6] Inferno charge preset is wired and caster-owned.");
                var chargeEvent = new AnimationEvent { time = 5f / 30f };
                var hitEvent = new AnimationEvent { time = 1.5f };

                // ── 싱글 ──
                NetworkContext.Set(false, false);
                var single = new ProjectileFixture(UnitType.InfernoSpirit);
                fixtures.Add(single);
                var singleView = View(single.Attacker, single.Combat, true);
                var results = new List<Hexiege.Presentation.ServerProjectileChargeCancelResult>();
                // 화면 소비자(ProjectilePresentationPresenter.ReceiveLaunchCancel)와 같은 호출을 그대로 연결한다.
                single.Combat.ProjectileLaunchCancelled += c => results.Add(singleView.ApplyServerProjectileLaunchCancel(c.PresentationRevision));
                Require(single.Combat.TryAttack(single.Attacker, 0d).HasValue, "[D-6] Single Inferno commit succeeds.");
                singleView.OnAttackCharge(chargeEvent);
                Require(Active(charge.VfxPrefab) == 1 && ActiveTracers() == 0,
                    "[D-6] Single: the charge marker plays one charge VFX while the server reservation is pending.");
                single.Victim.TakeDamage(single.Victim.Hp);
                single.Tick(.5d);
                Require(results.Count == 1 && results[0] == Hexiege.Presentation.ServerProjectileChargeCancelResult.Stopped
                        && Active(charge.VfxPrefab) == 0,
                    "[D-6] Single: target death during the charge stops the charge VFX at once through the server cancel notice.");
                singleView.OnAttackCharge(chargeEvent);
                Require(Active(charge.VfxPrefab) == 0,
                    "[D-6] Single: a later charge marker of the cancelled attack does not restart the charge.");
                singleView.OnAttackHit(hitEvent);
                Require(ActiveTracers() == 0 && Active(charge.VfxPrefab) == 0 && single.Launches.Count == 0,
                    "[D-6] Single: the hit marker of a server-projectile charge unit fires no local tracer and replays no charge.");

                // ── Client(회차 규칙) ──
                NetworkContext.Set(false, true);
                var clientData = new UnitData(98310, UnitType.InfernoSpirit, TeamId.Red, new HexCoord(0, 0), 600, 25, 4f, 4f);
                var clientView = View(clientData, null, true);
                Commit(clientView, 7UL);
                clientView.OnAttackCharge(chargeEvent);
                Require(Active(charge.VfxPrefab) == 1, "[D-6] Client: the charge plays with the commit's lease (check only).");
                Require(clientView.ApplyServerProjectileLaunchCancel(6UL) == Hexiege.Presentation.ServerProjectileChargeCancelResult.StaleRevision
                        && Active(charge.VfxPrefab) == 1,
                    "[D-6] Client: a cancel notice for another revision never stops the current attack's charge.");
                Require(clientView.ApplyServerProjectileLaunchCancel(7UL) == Hexiege.Presentation.ServerProjectileChargeCancelResult.Stopped
                        && Active(charge.VfxPrefab) == 0,
                    "[D-6] Client: the same-revision server cancel notice stops the charge VFX immediately.");
                Commit(clientView, 8UL);
                Require(clientView.ApplyServerProjectileLaunchCancel(8UL) == Hexiege.Presentation.ServerProjectileChargeCancelResult.NothingActive,
                    "[D-6] Client: a cancel that arrives before the charge marker has nothing to stop yet.");
                clientView.OnAttackCharge(chargeEvent);
                // lease는 struct라 OnAttackCharge 뒤에 다시 읽는다(앞에서 복사해 두면 바뀐 값을 못 본다).
                var lease8 = (Hexiege.Application.Combat.Sequencing.AttackPresentationSourceMarkerLease)
                    viewType.GetField("_attackPresentationSourceMarkerLease", fields).GetValue(clientView);
                Require(Active(charge.VfxPrefab) == 0 && lease8.IsArmed && lease8.RemainingMarkerCount == 1,
                    "[D-6] Client: that revision's late charge marker does not start the charge, and the lease is not consumed.");

                // ── Host(서버 UseCase 게이트) ──
                NetworkContext.Set(true, true);
                var host = new ProjectileFixture(UnitType.InfernoSpirit);
                fixtures.Add(host);
                var hostView = View(host.Attacker, host.Combat, true);
                Commit(hostView, 10UL);
                Require(host.Combat.TryReserveProjectileLaunch(host.Attacker, host.Victim.Id, true, 1.5d, 0),
                    "[D-6] Host: the server reservation exists.");
                host.Combat.CancelProjectileLaunches(host.Attacker, "move-command");
                hostView.OnAttackCharge(chargeEvent);
                Require(Active(charge.VfxPrefab) == 0,
                    "[D-6] Host: even with an armed lease, the charge of an attack the server already cancelled does not start.");
                Require(host.Combat.TryReserveProjectileLaunch(host.Attacker, host.Victim.Id, true, 4.5d, 0), "[D-6] Host: next reservation.");
                hostView.OnAttackCharge(chargeEvent);
                Require(Active(charge.VfxPrefab) == 1, "[D-6] Host: the next committed attack's charge plays normally.");

                // ── 비차지 유닛 ──
                var streamView = View(new UnitData(98311, UnitType.StreamSpirit, TeamId.Blue, new HexCoord(0, 0), 300, 60, 3f, 3f), null, false);
                Require(streamView.ApplyServerProjectileLaunchCancel(0UL) == Hexiege.Presentation.ServerProjectileChargeCancelResult.NotChargeUnit,
                    "[D-6] A non-charge projectile unit has nothing to stop.");
            }
            finally
            {
                foreach (var f in fixtures) f.Dispose();
                effectSingleton.SetValue(null, oldEffect); audioSingleton.SetValue(null, oldAudio);
                NetworkContext.Set(oldServer, oldActive);
                foreach (var go in casters) if (go != null) UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // ── [단계 D — D-7] 취소 통보 전송·화면 연결 · RM-5 · Fox 시간축 소스 게이트 ─────────────────────────
        private static void ValidateServerProjectileStageDSourceGates()
        {
            string Read(string relative)
            {
                string path = Path.Combine(UnityEngine.Application.dataPath, relative);
                Require(File.Exists(path), "Stage D source gate file exists: " + relative);
                return File.ReadAllText(path);
            }
            string Active(string source)
            {
                var kept = new System.Text.StringBuilder(source.Length);
                foreach (string line in source.Split('\n'))
                    if (!line.TrimStart().StartsWith("//", StringComparison.Ordinal)) kept.Append(line).Append('\n');
                return kept.ToString();
            }
            string transport = Active(Read("_Project/Scripts/Infrastructure/Network/NetworkCombatController.Projectiles.cs"));
            string presenter = Active(Read("_Project/Scripts/Presentation/Effects/ProjectilePresentationPresenter.cs"));
            string unitView = Active(Read("_Project/Scripts/Presentation/Unit/UnitView.cs"));
            string useCase = Active(Read("_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs"));
            Require(transport.Contains("combat.ProjectileLaunchCancelled += SendProjectileLaunchCancel;")
                    && transport.Contains("_attackPresentationRevisions.TryGetValue(cancel.AttackerId, out ulong revision);")
                    && transport.Contains("private void ProjectileLaunchCancelClientRpc(")
                    && transport.Contains("combat.ReceiveProjectileLaunchCancel(_pendingProjectileCancels.Dequeue());"),
                "[D-7] The server cancel notice is sent with the attacker's latest presentation revision and delivered to the client use case.");
            Require(presenter.Contains("combat.ProjectileLaunchCancelled += ReceiveLaunchCancel;")
                    && presenter.Contains("view.ApplyServerProjectileLaunchCancel(cancel.PresentationRevision)"),
                "[D-7] The projectile presenter routes every cancel notice to the attacker's UnitView.");
            Require(unitView.Contains("if (chargeOnly && IsServerProjectileChargeCancelled())")
                    && unitView.Contains("_combatUseCase.IsChargePresentationCancelled(_unitData)"),
                "[D-7] The charge marker asks the server authority (single/Host) whether the attack was already cancelled.");
            // [P5 정리] RM-5 옛 분기(Fox 즉시 표시)는 주석까지 삭제했다 — 주 타깃 피해 호출이 즉시 표시 false를 직접 넘기는지 본다.
            Require(useCase.Contains("attacker, target, immediatePresentation: false,")
                    && !useCase.Contains("immediatePrimaryPresentation"),
                "[D-7] RM-5: the primary hit never uses an immediate presentation (the Fox rule-19 branch is deleted).");
            Require(transport.Contains("UnitAttackShadowObserver.RecordFoxMagicianTimerImpact(")
                    && transport.Contains("RememberFoxProjectileTimelineTicket(UnitData unit, ulong ticket)"),
                "[D-7] The Fox timeline diagnostic records its timer-impact at the server launch on the projectile path.");
        }

        // ── [2026-10-27 단계 E — E1-1] EagleArcher: 특수 공격 없는 단일 타격 투사체 ─────────────────────
        /// <summary>
        /// 실제 UnitCombatUseCase로 EagleArcher의 서버 판정을 확인한다(단계 B Tank와 같은 모양).
        ///   발사 = OnAttackHit(fixture 값 = 운영 값 0.2초)에 피해 0 → 착탄 시각(발사 + (중심 거리 − 반경) ÷ 속도 6)에 단일 writer 1회.
        /// 타격 시각은 유닛 데이터의 실제 HitFrameTimes[0](float)로 tick 한다(하드코딩 금지 — ac05e189 교훈).
        /// </summary>
        private static void ValidateServerProjectileEagleArcher()
        {
            using (var f = new ProjectileFixture(UnitType.EagleArcher))
            {
                int hp = f.Victim.Hp;
                Require(f.Combat.TryAttack(f.Attacker, 0d).HasValue && f.Combat.HasPendingProjectileLaunch(f.Attacker),
                    "[E1-1] EagleArcher (switch ON) commit creates a launch reservation instead of a hit-time damage timer.");
                double hitAt = f.Attacker.HitFrameTimes[0];
                f.Tick(hitAt * .5d);
                Require(f.Launches.Count == 0 && f.Victim.Hp == hp && f.VictimDamage.Count == 0,
                    "[E1-1] Before the OnAttackHit offset nothing is launched and no damage is applied.");
                f.Tick(hitAt);
                Require(f.Launches.Count == 1 && f.Victim.Hp == hp && f.VictimDamage.Count == 0
                        && Math.Abs(f.Launches[0].LaunchTime - hitAt) < 1e-9
                        && f.Launches[0].AttackerKind == (int)UnitType.EagleArcher && f.Launches[0].Team == TeamId.Blue,
                    "[E1-1] EagleArcher launches at its OnAttackHit offset (0.2 s) with no damage; the launch record carries the team (arrow colour).");
                ProjectileLaunchPresentation launch = f.Launches[0];
                double expectedImpact = hitAt + (2.0d - .25d) / 6d;
                Require(Math.Abs(launch.ImpactTime - expectedImpact) < 1e-5 && launch.Homing,
                    "[E1-1] EagleArcher impact time = launch + (centre distance - target radius) / speed 6 (homing).");
                f.Tick(expectedImpact - .001d);
                Require(f.Impacts.Count == 0 && f.Victim.Hp == hp, "[E1-1] No damage before the authoritative impact.");
                f.Tick(expectedImpact);
                Require(f.Impacts.Count == 1 && f.Impacts[0].Outcome == ProjectileImpactOutcome.Hit
                        && f.VictimDamage.Count == 1 && f.Victim.Hp == hp - 20
                        && f.VictimDamage[0].ProjectilePresentationKey.Equals(launch.Key)
                        && !f.VictimDamage[0].ImmediatePresentation,
                    "[E1-1] At impact exactly one hit goes through the single writer with the projectile key (number waits for the arrow).");
                f.Tick(expectedImpact + 2d);
                Require(f.Impacts.Count == 1 && f.VictimDamage.Count == 1, "[E1-1] Repeated ticks never duplicate the arrow impact.");
                string end = f.Combat.BuildProjectileEndLine("fixture");
                Require(end.Contains(" switch=6,7,12,17,21,24,26;tower:human ") && end.Contains(" path=24:s1/l0 ")
                        && end.Contains(" launched=1 ") && end.Contains(" hit=1 ") && end.Contains(" balance=ok ") && end.EndsWith(" verdict=PASS"),
                    "[E1-1] Server END counts the EagleArcher attack on the server path (24:s1/l0): " + end);
            }
        }

        // ── [2026-10-27 단계 E — E2-1~E2-3] MushroomBomber: 착탄 피해 + 범위 독 + 옛 스냅샷 0 ─────────────────────
        /// <summary>
        /// 실제 UnitCombatUseCase로 MushroomBomber의 서버 판정을 확인한다.
        ///   E2-1 발사 = OnAttackHit 1.0초(피해·독 0) → 착탄 때 주 타깃 직접 피해 1회(번호표) + BlastAttackBehavior가
        ///        "주 타깃 위치" 중심 반경(기본 1.0) 안의 적 전원(주 타깃 포함)에게 독. 반경 밖 적 · 아군은 독 없음.
        ///   E2-2 착탄 순간 옛 "피해 순간 스냅샷"(OnAttackEffectConfirmed) 발행 0건(RM-8 — 이중 착탄 VFX 방지).
        ///        독의 공격 출처 키(damageOrigin)는 그대로 채워진다(생성은 유지 — 결정 E-D (가)).
        ///   E2-3 비행 중 공격자 사망 → 피해·독 진행(Persist — BlastAttackBehavior AttackerMayBeInactive),
        ///        비행 중 대상 소실 → 피해·독 0, 결과 TargetLost(반경 안 다른 적도 독 없음).
        /// </summary>
        private static void ValidateServerProjectileMushroomBomber()
        {
            // 주 타깃(9701, x 2.1) 주변에 적 2 · 아군 1을 둔다. 반경 1.0(UnitCombatUseCase 기본값)은 주 타깃 "위치"가 중심이다.
            //   near(x 2.6) = 주 타깃에서 0.5 → 안, far(x 3.6) = 1.5 → 밖(공격자에게서 3.5라 사거리 3 밖 — 주 타깃으로 뽑히지 않는다),
            //   ally(x 2.3, Blue) = 반경 안이지만 아군 → 제외(규칙 16).
            UnitData Add(ProjectileFixture f, int id, TeamId team, HexCoord hex, float x)
            {
                UnitData unit = f.Spawn.SpawnUnitWithId(id, UnitType.LittleKnight, team, hex);
                Require(unit != null, "[E2] Mushroom fixture neighbour spawns on the fixture grid: " + id);
                f.Positions.Units[id] = new Vector3(x, 0f, .1f);
                return unit;
            }
            bool Poisoned(ProjectileFixture f, UnitData unit)
                => f.Combat.TimedDamageEffects.TryGet(unit, out TimedDamageEffectPresentation state)
                   && state.Active && state.Visual == TimedDamageVisual.Poison;

            // E2-1 · E2-2 정상 착탄.
            using (var f = new ProjectileFixture(UnitType.MushroomBomber))
            {
                var snapshots = new List<AttackEffectPresentation>();
                using (GameEvents.OnAttackEffectConfirmed.Subscribe(snapshots.Add))
                {
                    UnitData near = Add(f, 9702, TeamId.Red, new HexCoord(2, 0), 2.6f);
                    UnitData far = Add(f, 9703, TeamId.Red, new HexCoord(0, 2), 3.6f);
                    UnitData ally = Add(f, 9704, TeamId.Blue, new HexCoord(0, 1), 2.3f);
                    int hp = f.Victim.Hp;
                    Require(f.Combat.TryAttack(f.Attacker, 0d).HasValue && f.Combat.HasPendingProjectileLaunch(f.Attacker),
                        "[E2-1] MushroomBomber (switch ON) commit creates a launch reservation instead of a hit-time damage timer.");
                    double hitAt = f.Attacker.HitFrameTimes[0];
                    f.Tick(hitAt * .5d);
                    Require(f.Launches.Count == 0 && f.Victim.Hp == hp && !Poisoned(f, f.Victim) && !Poisoned(f, near),
                        "[E2-1] Before the 1.0 s OnAttackHit offset nothing is launched, no damage and no poison.");
                    f.Tick(hitAt);
                    Require(f.Launches.Count == 1 && f.Victim.Hp == hp && f.VictimDamage.Count == 0
                            && !Poisoned(f, f.Victim) && !Poisoned(f, near)
                            && Math.Abs(f.Launches[0].LaunchTime - hitAt) < 1e-9 && f.Launches[0].AttackerKind == (int)UnitType.MushroomBomber
                            && f.Launches[0].TargetId == f.Victim.Id && f.Launches[0].Homing,
                        "[E2-1] MushroomBomber launches a homing spore at 1.0 s with no damage and no poison at launch.");
                    ProjectileLaunchPresentation launch = f.Launches[0];
                    Require(Math.Abs(launch.ImpactTime - (hitAt + (2.0d - .25d) / 5d)) < 1e-5,
                        "[E2-1] Mushroom impact time = launch + (centre distance - target radius) / speed 5.");
                    f.Positions.Units[f.Victim.Id] = new Vector3(2.2f, 0f, .1f);   // 비행 중 주 타깃이 조금 움직인다(Homing) — 반경 중심도 따라간다.
                    f.Tick(launch.ImpactTime - .001d);
                    Require(f.Impacts.Count == 0 && f.Victim.Hp == hp && !Poisoned(f, near),
                        "[E2-1] No damage and no poison before the authoritative impact.");
                    f.Tick(launch.ImpactTime);
                    Require(f.Impacts.Count == 1 && f.Impacts[0].Outcome == ProjectileImpactOutcome.Hit
                            && f.VictimDamage.Count == 1 && f.Victim.Hp == hp - 30
                            && f.VictimDamage[0].ProjectilePresentationKey.Equals(launch.Key)
                            && !f.VictimDamage[0].ImmediatePresentation,
                        "[E2-1] At impact exactly one direct hit goes through the single writer with the projectile key.");
                    Require(Poisoned(f, f.Victim) && Poisoned(f, near) && !Poisoned(f, far) && !Poisoned(f, ally),
                        "[E2-1] At impact the blast poisons every enemy within the radius of the primary target's position (primary included); "
                        + "enemies outside the radius and allies get none (rules 38~40 unchanged — only the timing moved to the impact).");
                    Require(f.Combat.TimedDamageEffects.TryGet(near, out TimedDamageEffectPresentation poison)
                            && poison.SourceId == f.Attacker.Id && poison.AttackSequence != 0UL && poison.AttackMatch != 0UL,
                        "[E2-2] The poison keeps its attack provenance (damageOrigin is still created at the impact).");
                    Require(snapshots.Count == 0,
                        "[E2-2] RM-8: a switched-on Mushroom impact publishes no legacy writer snapshot (no legacy 0.1 s tracer, no second impact VFX).");
                    int afterImpact = f.Victim.Hp;
                    f.Combat.TickTimedEffects(1f);
                    Require(f.Victim.Hp < afterImpact, "[E2-1] The poison DoT ticks after the impact.");
                    string end = f.Combat.BuildProjectileEndLine("fixture");
                    Require(end.Contains(" path=26:s1/l0 ") && end.Contains(" hit=1 ") && end.Contains(" balance=ok ") && end.EndsWith(" verdict=PASS"),
                        "[E2-1] Server END counts the MushroomBomber attack on the server path (26:s1/l0): " + end);
                }
            }

            // E2-3a 비행 중 공격자 사망 → 그래도 피해 + 독(Q3 Persist).
            using (var f = new ProjectileFixture(UnitType.MushroomBomber))
            {
                UnitData near = Add(f, 9702, TeamId.Red, new HexCoord(2, 0), 2.6f);
                int hp = f.Victim.Hp;
                f.Combat.TryAttack(f.Attacker, 0d);
                f.Tick(f.Attacker.HitFrameTimes[0]);
                Require(f.Launches.Count == 1, "[E2-3] Mushroom launched before the attacker dies.");
                f.Attacker.TakeDamage(f.Attacker.Hp);
                f.Spawn.RemoveUnit(f.Attacker.Id);
                f.Tick(f.Launches[0].ImpactTime);
                Require(f.Impacts.Count == 1 && f.Impacts[0].Outcome == ProjectileImpactOutcome.Hit
                        && f.Victim.Hp == hp - 30 && Poisoned(f, f.Victim) && Poisoned(f, near),
                    "[E2-3] Q3 Persist: a spore launched before its attacker died still hits and still poisons the blast radius.");
            }

            // E2-3b 비행 중 대상 소실 → 피해·독 0, TargetLost(반경 안 다른 적도 독 없음).
            using (var f = new ProjectileFixture(UnitType.MushroomBomber))
            {
                var snapshots = new List<AttackEffectPresentation>();
                using (GameEvents.OnAttackEffectConfirmed.Subscribe(snapshots.Add))
                {
                    UnitData near = Add(f, 9702, TeamId.Red, new HexCoord(2, 0), 2.6f);
                    f.Combat.TryAttack(f.Attacker, 0d);
                    f.Tick(f.Attacker.HitFrameTimes[0]);
                    f.Victim.TakeDamage(f.Victim.Hp);                               // 다른 원인으로 비행 중 사망.
                    f.Tick(f.Launches[0].ImpactTime);
                    Require(f.Impacts.Count == 1 && f.Impacts[0].Outcome == ProjectileImpactOutcome.TargetLost
                            && f.Impacts[0].AppliedAmount == 0 && f.VictimDamage.Count == 0 && !Poisoned(f, near) && snapshots.Count == 0,
                        "[E2-3] Q4: a target lost in flight gets no damage, the blast does not run (no poison) and no snapshot is published.");
                }
            }

            // E2-2 소스 게이트: 독 origin(damageOrigin) 생성은 남고, 옛 Mushroom 스냅샷 발행 코드는 없다 — 실제 코드 줄로 확인한다.
            //   [P5 정리] 옛 발행 블록(RM-8)을 삭제해 "발행 조건" 대신 "ExecuteAttack에 발행 호출이 없음"을 본다.
            string useCase = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
                "_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs"));
            var active = new System.Text.StringBuilder(useCase.Length);
            foreach (string line in useCase.Split('\n'))
                if (!line.TrimStart().StartsWith("//", StringComparison.Ordinal)) active.Append(line).Append('\n');
            string code = active.ToString();
            Require(code.Contains("damageOrigin = _attackEffects.Create(attacker, target, AttackEffectVisual.MushroomImpact,")
                    && !code.Contains("mushroomVisual")
                    && !code.Contains("GameEvents.OnAttackEffectConfirmed.OnNext("),
                "[E2-2] RM-8: ExecuteAttack keeps creating damageOrigin (poison/burn origin) and the unit combat use case publishes no attack effect snapshot.");
        }

        // ── [2026-10-27 단계 E — E1-2] EagleArcher 팀별 화살 트레이서 에셋 계약 + 팀 선택 ─────────────────────
        /// <summary>
        /// 저장소에 커밋된 EagleArcher 화살 트레이서 에셋(사용자 커밋 be9b6e1a)이 그대로 연결돼 있는지 확인한다.
        ///   이 에셋들은 2026-10-27 1회성 메뉴 「Hexiege/Effects/Setup/Create EagleArcher Arrow Tracers」로 한 번 만들었고,
        ///   그 메뉴 스크립트(Assets/Editor/Setup/EagleArcherArrowTracerSetup.cs)는 사용자 지시로 같은 날 삭제했다.
        ///   → 이 검사가 실패하면 "메뉴를 다시 실행"이 아니라 "커밋된 에셋이 지워졌거나 연결이 바뀌었는지" 확인해야 한다.
        ///   · UnitEffectConfig type 24 행: tracerPreset = EffectPreset_EagleArcher_Tracer_Blue, redTeamTracerPreset = _Red,
        ///     attackPreset · impactPreset · hitPreset = 비어 있음(착탄 이펙트 없음 — U-10).
        ///   · 두 프리팹: 파티클 없음, 자식 Arrow의 메시 = 그 팀 유닛 프리팹 손 화살(EagleArrow)과 같은 메시, 머티리얼 = 그 팀 화살 머티리얼.
        ///   · 실제 EffectManager.BeginServerProjectile이 팀으로 프리팹을 고르고(Blue → blue, Red → red), 처음부터 대상 쪽을 본다.
        ///   · 팀 칸이 비어 있는 다른 투사체 유닛(Tank)은 두 팀 모두 같은 트레이서(예전 동작 그대로).
        /// 경로 문자열은 이 검사 안에 직접 적는다(처음부터 1회성 Setup 스크립트의 상수를 참조하지 않았다 — 그래서 스크립트 삭제 뒤에도 그대로 컴파일된다).
        /// </summary>
        private static void ValidateEagleArcherArrowTracerAssets()
        {
            const string configPath = "Assets/_Project/Resources/Config/UnitEffectConfig.asset";
            const string bluePresetPath = "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_EagleArcher_Tracer_Blue.asset";
            const string redPresetPath = "Assets/_Project/Resources/Config/EffectPresets/EffectPreset_EagleArcher_Tracer_Red.asset";
            const string blueUnitPath = "Assets/_Project/Prefabs/Units/Transcendence/Unit_EagleArcher_Blue.prefab";
            const string redUnitPath = "Assets/_Project/Prefabs/Units/Transcendence/Unit_EagleArcher_Red.prefab";
            const string blueMaterialPath = "Assets/_Project/Materials/Units/EagleArcher/mat_eaglearrow_blue.mat";
            const string redMaterialPath = "Assets/_Project/Materials/Units/EagleArcher/mat_eaglearrow_red.mat";
            // [2026-10-27] 1회성 메뉴 스크립트를 삭제해 "메뉴를 먼저 실행하라"는 안내가 더 이상 사실이 아니다 →
            //   실패 안내를 "커밋된 에셋(be9b6e1a)이 있어야 한다"로 바꿨다.
            const string committedAssetsHint = "assets committed in be9b6e1a must be present; the one-off setup menu was removed on 2026-10-27";

            var units = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.UnitEffectConfig>(configPath);
            var bluePreset = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.EffectPreset>(bluePresetPath);
            var redPreset = AssetDatabase.LoadAssetAtPath<Hexiege.Presentation.EffectPreset>(redPresetPath);
            Require(units != null && bluePreset != null && redPreset != null
                    && bluePreset.VfxPrefab != null && redPreset.VfxPrefab != null && bluePreset.VfxPrefab != redPreset.VfxPrefab,
                "[E1-2] EagleArcher team arrow tracer presets exist with two different prefabs (" + committedAssetsHint + ").");

            // UnitEffectConfig 행(직렬화 값 그대로 — intValue로 UnitType 비교).
            var serialized = new SerializedObject(units);
            SerializedProperty entries = serialized.FindProperty("_entries");
            int rows = 0;
            for (int i = 0; entries != null && i < entries.arraySize; i++)
            {
                SerializedProperty row = entries.GetArrayElementAtIndex(i);
                if (row.FindPropertyRelative("unitType").intValue != (int)UnitType.EagleArcher) continue;
                rows++;
                Require(row.FindPropertyRelative("tracerPreset").objectReferenceValue == bluePreset
                        && row.FindPropertyRelative("redTeamTracerPreset").objectReferenceValue == redPreset
                        && row.FindPropertyRelative("attackPreset").objectReferenceValue == null
                        && row.FindPropertyRelative("impactPreset").objectReferenceValue == null
                        && row.FindPropertyRelative("hitPreset").objectReferenceValue == null,
                    "[E1-2] UnitEffectConfig EagleArcher row: tracer Blue / red-team tracer Red, and no attack/impact/hit preset (U-10 no impact VFX).");
            }
            Require(rows == 1, "[E1-2] UnitEffectConfig has exactly one EagleArcher row.");

            // 프리팹: 파티클 없는 메시 화살, 메시·머티리얼 = 그 팀 유닛 손 화살.
            void RequireArrowPrefab(GameObject prefab, string unitPath, string materialPath, string team)
            {
                var unit = AssetDatabase.LoadAssetAtPath<GameObject>(unitPath);
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                MeshFilter source = null;
                if (unit != null)
                    foreach (var filter in unit.GetComponentsInChildren<MeshFilter>(true))
                        if (filter.gameObject.name == "EagleArrow") { source = filter; break; }
                Transform arrow = prefab.transform.Find("Arrow");
                var arrowFilter = arrow != null ? arrow.GetComponent<MeshFilter>() : null;
                var arrowRenderer = arrow != null ? arrow.GetComponent<MeshRenderer>() : null;
                Require(source != null && material != null && arrowFilter != null && arrowRenderer != null
                        && arrowFilter.sharedMesh == source.sharedMesh && arrowRenderer.sharedMaterial == material
                        && prefab.GetComponentsInChildren<ParticleSystem>(true).Length == 0
                        && prefab.transform.localRotation == Quaternion.identity,
                    "[E1-2] " + team + " arrow tracer prefab copies the unit's EagleArrow mesh with the " + team + " arrow material (no particles).");
            }
            RequireArrowPrefab(bluePreset.VfxPrefab, blueUnitPath, blueMaterialPath, "Blue");
            RequireArrowPrefab(redPreset.VfxPrefab, redUnitPath, redMaterialPath, "Red");

            // 실제 EffectManager: 팀으로 프리팹 선택 + 처음 바라보는 방향.
            var root = new GameObject("EagleArrowTracerFixture") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var manager = root.AddComponent<Hexiege.Presentation.EffectManager>();
                typeof(Hexiege.Presentation.EffectManager).GetField("_vfxContainer", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(manager, root.transform);
                manager.Initialize(units, null, null);
                Require(units.GetTracer(UnitType.EagleArcher, TeamId.Blue) == bluePreset
                        && units.GetTracer(UnitType.EagleArcher, TeamId.Red) == redPreset
                        && units.GetTracer(UnitType.EagleArcher, TeamId.Neutral) == bluePreset
                        && units.GetTracer(UnitType.EagleArcher) == bluePreset,
                    "[E1-2] Team tracer lookup: Blue/Neutral use tracerPreset, Red uses redTeamTracerPreset.");
                Require(units.GetTracer(UnitType.Tank, TeamId.Red) == units.GetTracer(UnitType.Tank, TeamId.Blue)
                        && units.GetTracer(UnitType.Tank, TeamId.Red) == units.GetTracer(UnitType.Tank),
                    "[E1-2] A unit without a red-team tracer uses the same tracer for both teams (unchanged behaviour).");
                Vector3 start = new Vector3(1f, .4f, 1f), facing = new Vector3(3f, .2f, 1f);
                var blue = manager.BeginServerProjectile(UnitType.EagleArcher, TeamId.Blue, start, facing);
                var red = manager.BeginServerProjectile(UnitType.EagleArcher, TeamId.Red, start, facing);
                Require(blue != null && red != null && blue.SourcePrefab == bluePreset.VfxPrefab && red.SourcePrefab == redPreset.VfxPrefab
                        && blue.IsDriven && red.IsDriven,
                    "[E1-2] The actual EffectManager picks the Blue/Red arrow prefab from the launch team.");
                Vector3 expected = (facing - start).normalized;
                Require(Vector3.Angle(blue.transform.forward, expected) < .01f && (blue.transform.position - start).sqrMagnitude < 1e-8f,
                    "[E1-2] The arrow faces its target from the first frame (no stale pooled rotation) and starts at the launch point.");
                manager.EndServerProjectile(blue);
                manager.EndServerProjectile(red);
                Require(!blue.gameObject.activeSelf && !red.gameObject.activeSelf, "[E1-2] Ending the driven arrows returns them to the pool.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
