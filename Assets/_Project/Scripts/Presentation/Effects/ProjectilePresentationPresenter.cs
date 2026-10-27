// ============================================================================
// ProjectilePresentationPresenter.cs
// 서버 권위 투사체(옵션 C)의 "화면 단일 소비자" — 서버 발사 기록으로 투사체를 그리고,
// 서버 착탄 결과로 착탄 이펙트와 피해 숫자를 화면상 착탄 순간에 1회 낸다.
//
// [이 컴포넌트가 하는 일 — 유니티 초급자용]
//   1) UnitCombatUseCase.ProjectileLaunched / ProjectileResolved를 구독한다.
//      · 싱글·Host: 서버 UseCase가 직접 발행한다.
//      · Client: 네트워크로 받은 기록을 UseCase.ReceiveProjectile*가 같은 이벤트로 다시 발행한다.
//   2) 순수 일정표(ProjectilePresentationCoordinator)가 "언제 꺼내고, 진행도가 얼마고, 언제 끝내는가"를 정한다.
//   3) 이 컴포넌트는 그 결정대로 실제 오브젝트를 다룬다:
//      · 출발점 = 쏜 유닛의 총구(UnitView.ProjectileLaunchPoint). 없으면 서버 발사 위치.
//      · 끝점   = 원래 대상의 "화면 위치"를 따라가며(Homing) 대상 반경만큼 당긴 표면 지점(Q12).
//                 대상이 사라지면 마지막 위치에 고정한다(Q4). 서버 결과가 오면 서버 착탄 지점으로 부드럽게 맞춘다.
//      · 위치   = 출발점 → 끝점을 진행도(서버 발사/착탄 시각 기준)로 보간 — 그래서 서버 착탄 시각에 정확히 닿는다.
//      · 착탄   = 착탄 이펙트(명중·대상 소실 공통) + 명중이면 피격 표현 큐에 "지금 숫자를 내라"(ReleaseProjectileImpact).
//                 [2026-10-21] 화면 착탄 지점도 함께 넘긴다 — 사망타로 대상이 이미 디스폰됐으면 큐가 그 지점에 "0"을 낸다.
//   4) 게임 종료·재초기화·파괴 때 남은 투사체를 즉시 거둔다(착탄 이펙트·숫자 없음).
//   5) [2026-10-20 단계 B — 의심 1 수정] 투사체를 꺼내는 순간(Begin) 쏜 유닛의 "발사 신호"(총구 VFX + 발사음)를 낸다
//      (UnitView.PlayServerProjectileLaunchCue). 예전에는 애니메이션 marker가 냈는데, 순수 Client는 서버가 발사를
//      취소했는지 몰라 취소된 공격에도 총구 VFX가 났다. 발사 기록이 있을 때만 내므로 세 실행 형태(싱글·Host·Client)가
//      같은 서버 권위 정보로 판정한다. 늦은 접속으로 비행 중간부터 그리는 투사체는 발사 신호를 생략한다(이미 쏜 뒤다).
//   5-1) [2026-10-21 단계 C — 인간 방어타워(CannonTower)] 건물 공격자의 발사 기록도 같은 일정표로 그린다.
//      · 출발점 = 서버가 보낸 포구 위치(발사 기록 Source — 높이 포함). 타워에는 UnitView 총구가 없다.
//      · 투사체·착탄 이펙트 = BuildingEffectConfig의 타워 종족별 프리셋(인간 = CannonCart 것 재사용).
//      · 발사 신호 = 포구에서 타워 발사 이펙트(EffectPreset_HumanAutoTower_Attack) 1회 — 사용자 결정 「발사 이펙트도 같은 위치」.
//        늦은 접속 중간 복원이면 생략한다(유닛과 같은 규칙). 숫자·피격 반응은 유닛 투사체와 같은 착탄 순간 방출이다.
//   5-2) [2026-10-21 단계 D — 차지 유닛 InfernoSpirit · FoxMagician] 서버 발사 취소 통보(ProjectileLaunchCancelled)를 받아
//      쏘려던 유닛 화면의 차지 VFX를 즉시 끈다(UnitView.ApplyServerProjectileLaunchCancel). 발사 신호는 위 5)와 같다 —
//      차지 유닛은 발사 기록이 화면에 나올 때 총구 VFX 없이 발사음만 낸다(차지 VFX는 프리팹 길이로 발사 순간 스스로 끝난다).
//   6) [단계 B 진단] 받은 수·그린 수·착탄 VFX 수·거부 사유·결과/화면 도착 순서·화면 착탄 지연 분포를 세어
//      경기 종료 때 [PROJECTILE-END] side=view 2줄(2026-10-21 전에는 1줄)로 남기고, 처음 24발은 [PROJECTILE-VIEW] 표본 줄을 남긴다.
//
// [화면 시각 = 서버 시각 − 지연]
//   · 싱글·Host: 지연 0 — 서버 피해·사망과 화면 착탄이 같은 순간이다.
//   · Client: 지연 0.10초 — 기존 C3/버섯/파도 표현과 같은 값. 네트워크 도착 흔들림을 흡수해 비행 중간 점프를 줄인다.
//     (그 대가로 Client는 HP 막대·사망 연출이 화면 착탄보다 최대 그만큼 먼저 보일 수 있다 — 실기 확인 항목.)
//
// 이 컴포넌트는 HP·피해·타겟·서버 판정을 읽거나 쓰지 않는다(표현 전용). 씬에 배치하지 않고 GameBootstrapper가 붙인다.
// Presentation 레이어 — Unity 의존(MonoBehaviour).
// ============================================================================

using System;
using UniRx;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Profiling;
#endif
using Hexiege.Application;
using Hexiege.Core;
using Hexiege.Domain;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 서버 투사체의 화면 단일 소비자. 서버 기록의 시각·위치만으로 그린다(Client 속도 계산 없음 — Plan 위험 15).
    /// </summary>
    public sealed class ProjectilePresentationPresenter : MonoBehaviour, IProjectilePresentationSink
    {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 고정 이름으로 동기 호출 구간만 측정한다. 릴리스에는 계측 참조·필드·호출이 남지 않는다.
        private static readonly ProfilerMarker _projectileViewProfilerMarker = new ProfilerMarker("Hexiege.ProjectileView.Update");
#endif
        /// <summary>Client 화면 지연(초). 기존 서버 확정 표현(C3·버섯·파도)과 같은 값.</summary>
        public const double ClientPresentationDelaySeconds =
            Hexiege.Application.Combat.Sequencing.AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds;

        /// <summary>서버 결과가 도착한 뒤 끝점을 서버 착탄 지점으로 옮기는 데 걸리는 시간(초) — 끝점이 튀지 않게.</summary>
        private const double ResultBlendSeconds = 0.10d;

        /// <summary>쏜 유닛 화면을 찾지 못했을 때 서버 발사 위치에서 띄우는 높이(미터, 화면 표현 전용 대체값).</summary>
        private const float FallbackLaunchHeight = 0.3f;

        /// <summary>
        /// 발사 시각보다 이만큼(초) 넘게 늦게 꺼낸 투사체는 "비행 중간부터 복원"(늦은 접속 스냅샷 등)으로 보고
        /// 발사 신호(총구 VFX·발사음)를 생략한다. 정상 꺼냄은 한 프레임(0.016~0.05초) 안쪽이다.
        /// </summary>
        public const double MidFlightBeginSeconds = 0.2d;

        /// <summary>[진단] [PROJECTILE-VIEW] 표본 줄을 남기는 투사체 수(맵당 처음 N발의 begin/finish).</summary>
        private const int ViewSampleLimit = 24;

        // 투사체 1발의 화면 오브젝트·추적 상태(일정표 Slot.Visual에 붙인다).
        private sealed class Visual
        {
            public TracerProjectile Tracer;
            public Vector3 Start;
            public Transform TargetPose;
            public UnitView TargetView;
            public UnitData TargetData;
            public GameObject TargetObject;
            public Vector3 LastCenter;
            public Vector3 LastEndpoint;
            public UnitType AttackerType;
            public bool AttackerIsUnit;
            // [단계 C] 건물 공격자(타워)일 때만 의미가 있다 — 프리셋 선택용.
            public BuildingType TowerType;
            public RaceId TowerRace;
        }

        private readonly ProjectilePresentationCoordinator _coordinator = new ProjectilePresentationCoordinator();
        private UnitCombatUseCase _combat;
        private HitPresentationQueue _hits;
        private UnitFactory _unitFactory;
        private BuildingFactory _buildingFactory;
        private IPresentationPoseProvider _pose;
        private IDisposable _endSubscription;
        private ulong _epoch;
        private bool _ended;
        private int _faults;
        // 이번 프레임 일정표 Tick에 넘긴 화면 시각(Begin이 "얼마나 늦게 꺼냈는가"를 계산할 때 쓴다).
        private double _tickPresentationTime = double.NaN;

        // ── [2026-10-20 단계 B 진단] 화면 쪽 계수(읽기 전용 관측 — 그리기 판단에는 쓰지 않는다) ──
        private sealed class ViewCounters
        {
            public int LaunchReceived, LaunchAccepted, ResultReceived, ResultAccepted, EpochResets;
            public int Begins, BeginsMidFlight, TracerMissing;
            public int CuePlayed, CueVfxStarted, CueSkippedMidFlight, CueNoAttackerView;
            // [단계 C] 타워 발사 신호(포구 발사 이펙트) 계수 — played = 재생, skippedMidFlight = 중간 복원이라 생략, noPreset = 프리셋 없음.
            public int TowerCuePlayed, TowerCueSkippedMidFlight, TowerCueNoPreset;
            public int FinishHit, FinishLost, ImpactVfxPlayed, ImpactVfxMissing, Aborts, Samples;
            public readonly System.Collections.Generic.HashSet<ulong> Sampled = new System.Collections.Generic.HashSet<ulong>();
            // 화면 착탄 지연 = 화면이 착탄을 보여 준 순간의 서버 시각 − 서버 착탄 시각(Client 표시 지연, 초).
            public readonly ProjectileTimeDistribution DisplayLag =
                new ProjectileTimeDistribution(0.05d, 0.1d, 0.15d, 0.2d, 0.3d, 0.5d);
            // 결과 도착 지연 = 결과를 받은 순간의 서버 시각 − 서버 착탄 시각(네트워크 한쪽 지연 추정, 초).
            public readonly ProjectileTimeDistribution ResultLag =
                new ProjectileTimeDistribution(0d, 0.05d, 0.1d, 0.2d, 0.5d);
            // 꺼냄 지연 = 꺼낸 순간의 화면 시각 − 서버 발사 시각(초). 늦은 접속 복원이면 큰 값이 된다.
            public readonly ProjectileTimeDistribution BeginLag =
                new ProjectileTimeDistribution(0.02d, 0.05d, 0.1d, 0.2d);
            public bool EndWritten;
        }

        private ViewCounters _view = new ViewCounters();

        /// <summary>표현을 맡을 준비가 됐는가. 피격 표현 큐는 이 값이 false면 투사체 피해 숫자를 즉시 방출한다.</summary>
        public bool IsReady => _combat != null && !_ended;

        /// <summary>
        /// 조합 루트(GameBootstrapper)가 맵을 로드할 때마다 호출한다. 이전 구독·표현을 먼저 정리한다.
        /// </summary>
        /// <param name="combat">현재 경기 전투 UseCase(이벤트·시계 제공).</param>
        /// <param name="hits">피격 표현 큐(착탄 순간 숫자 방출 창구).</param>
        /// <param name="unitFactory">유닛 화면 오브젝트 조회.</param>
        /// <param name="buildingFactory">건물 화면 오브젝트 조회.</param>
        /// <param name="pose">화면 pose 조회(Red 화면 반전이 적용된 위치).</param>
        public void Initialize(UnitCombatUseCase combat, HitPresentationQueue hits, UnitFactory unitFactory,
            BuildingFactory buildingFactory, IPresentationPoseProvider pose)
        {
            Detach();
            if (combat == null || hits == null) return;
            _combat = combat; _hits = hits; _unitFactory = unitFactory; _buildingFactory = buildingFactory; _pose = pose;
            _epoch = combat.ProjectileEpoch;
            _ended = false;
            _faults = 0;
            _view = new ViewCounters();
            ServerProjectileDiagnostics.ResetView();   // [진단] UnitView marker 계수도 이 맵부터 새로 센다.
            _coordinator.Reset(_epoch, this);
            combat.ProjectileLaunched += ReceiveLaunch;
            combat.ProjectileResolved += ReceiveResult;
            combat.ProjectileLaunchCancelled += ReceiveLaunchCancel;   // [단계 D]
            _endSubscription = GameEvents.OnGameEnd.Subscribe(_ =>
            {
                // 게임 종료 — 서버도 남은 투사체를 피해 없이 닫았다. 화면도 즉시 거둔다(착탄 이펙트·숫자 없음).
                _ended = true;
                _coordinator.Close(this);
                WriteViewEnd("game-end");   // [진단] 거둔 수(aborts)까지 센 뒤 END 1줄.
            });
        }

        private void EnsureEpoch()
        {
            // Client는 서버 epoch를 받은 뒤 UseCase의 epoch가 바뀐다. 바뀌면 일정표를 새 epoch로 시작한다.
            if (_combat == null || _epoch == _combat.ProjectileEpoch) return;
            _epoch = _combat.ProjectileEpoch;
            _coordinator.Reset(_epoch, this);
            _view.EpochResets++;   // [진단] Client가 서버 epoch를 받으면 1회(정상). 2 이상이면 경기 중 epoch가 바뀌었다.
        }

        private void ReceiveLaunch(ProjectileLaunchPresentation launch)
        {
            if (_ended) return;
            EnsureEpoch();
            _view.LaunchReceived++;
            if (_coordinator.AcceptLaunch(launch, Time.realtimeSinceStartupAsDouble))
            {
                _view.LaunchAccepted++;
                // [진단] 공격자별 "받은 발사 수" — UnitView marker 수와 비교해 "서버 발사 없이 나온 marker"를 센다.
                if (launch.AttackerIsUnit) ServerProjectileDiagnostics.RecordServerLaunchAccepted(launch.AttackerId);
            }
        }

        private void ReceiveResult(ProjectileImpactPresentation result)
        {
            if (_ended) return;
            EnsureEpoch();
            _view.ResultReceived++;
            if (_coordinator.AcceptResult(result, Time.realtimeSinceStartupAsDouble))
            {
                _view.ResultAccepted++;
                _view.ResultLag.Add(ReadServerNow() - result.ImpactTime);
            }
        }

        /// <summary>
        /// [2026-10-21 단계 D] 서버 발사 취소 통보 — 쏘려던 유닛 화면의 차지 VFX를 즉시 끈다(차지 유닛만, 같은 회차만).
        /// 이 컴포넌트가 투사체 일정표에 넣는 것은 없다(취소된 예약은 발사 기록이 없어 처음부터 그릴 것이 없다).
        /// 게임 종료 뒤에는 처리하지 않는다 — 종료 정리가 모든 화면을 거둔다.
        /// </summary>
        private void ReceiveLaunchCancel(ProjectileLaunchCancelPresentation cancel)
        {
            if (_ended) return;
            try
            {
                UnitView view = ResolveUnitView(cancel.AttackerId);
                ServerProjectileChargeCancelResult result = view != null
                    ? view.ApplyServerProjectileLaunchCancel(cancel.PresentationRevision)
                    : ServerProjectileChargeCancelResult.Unavailable;
                ServerProjectileDiagnostics.RecordChargeCancelApplied((int)result);   // [진단] 읽기 전용
            }
            catch (Exception exception)
            {
                _faults++;
                if (_faults == 1 || _faults % 64 == 0)
                    GameLog.Dev.Warn("Combat", nameof(ProjectilePresentationPresenter),
                        "Projectile launch-cancel presentation degraded; HP authority unchanged",
                        $"projectile={cancel.Key}, attacker={cancel.AttackerId}, faults={_faults}, error={exception.GetType().Name}:{exception.Message}");
            }
        }

        // 공격자 Id → 그 Id를 가진 살아 있는 화면(같은 객체 확인: View의 Data Id가 같을 때만 — ID 재사용 안전).
        private UnitView ResolveUnitView(int unitId)
        {
            if (_unitFactory == null) return null;
            GameObject go = _unitFactory.GetUnitObject(unitId);
            UnitView view = go != null ? go.GetComponent<UnitView>() : null;
            return view != null && view.Data != null && view.Data.Id == unitId && go.activeInHierarchy ? view : null;
        }

        // [진단 전용] 지금 서버 시계 값(없으면 NaN — 분포 상자는 NaN을 넣지 않는다).
        private double ReadServerNow() => _combat != null ? _combat.ProjectileServerTime : double.NaN;

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            using var projectileViewSample = _projectileViewProfilerMarker.Auto();
#endif
            if (_combat == null || _ended) return;
            EnsureEpoch();
            double serverNow = _combat.ProjectileServerTime;
            if (double.IsNaN(serverNow) || double.IsInfinity(serverNow)) return;
            bool pureClient = NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer;
            double presentationTime = serverNow - (pureClient ? ClientPresentationDelaySeconds : 0d);
            _tickPresentationTime = presentationTime;
            _coordinator.Tick(presentationTime, Time.realtimeSinceStartupAsDouble, this);
        }

        // ====================================================================
        // IProjectilePresentationSink — 일정표가 정한 시점에 실제 오브젝트를 다룬다
        // ====================================================================

        /// <summary>투사체를 화면에 꺼낸다(출발점·추적 대상 1회 해석 — 이후 ID로 다시 찾지 않는다).</summary>
        public void Begin(ProjectilePresentationCoordinator.Slot slot)
        {
            try
            {
                ProjectileLaunchPresentation launch = slot.Launch;
                var visual = new Visual
                {
                    AttackerIsUnit = launch.AttackerIsUnit,
                    AttackerType = (UnitType)launch.AttackerKind,
                    // [단계 C] 건물 공격자면 AttackerKind = BuildingType, AttackerRace = RaceId(서버가 채운다).
                    TowerType = (BuildingType)launch.AttackerKind,
                    TowerRace = (RaceId)launch.AttackerRace,
                    LastCenter = ViewConverter.ToView(launch.TargetCenter)
                };
                UnitView attackerView = ResolveAttackerView(launch);
                // 옛 줄(기록용): visual.Start = ResolveLaunchPoint(launch);  — 같은 규칙을 아래 두 줄로 나눴다(공격자 화면을 한 번만 찾는다).
                // [단계 C] 건물 공격자(타워)는 서버가 보낸 포구 위치(높이 포함)를 그대로 출발점으로 쓴다.
                //   Red 화면은 ToView가 XZ를 반전한다 — 타워도 그 화면에서 180° 돌아 놓여 있어 같은 포구에 맞는다.
                //   유닛 공격자 줄은 예전과 같다(공격자 화면이 있으면 총구, 없으면 서버 위치 + 대체 높이).
                if (!launch.AttackerIsUnit)
                    visual.Start = ViewConverter.ToView(launch.Source);
                else
                    visual.Start = attackerView != null
                        ? attackerView.ProjectileLaunchPoint
                        : ViewConverter.ToView(launch.Source) + Vector3.up * FallbackLaunchHeight;
                ResolveTarget(launch, visual);
                visual.LastEndpoint = SurfacePoint(visual.Start, visual.LastCenter, launch.TargetRadius);
                EffectManager effects = EffectManager.Instance;
                // [2026-10-27 단계 E] 팀별 트레이서(EagleArcher 화살 Blue/Red 머티리얼)를 고르도록 서버 발사 기록의 Team을 넘기고,
                //   메시 화살이 첫 프레임부터 대상 쪽을 보도록 처음 바라볼 지점(바로 위에서 정한 표면점)도 넘긴다.
                //   Team은 서버가 채운 실제 팀이다(Red 관점 Client의 화면 뒤집기와 무관) → 양쪽 화면에서 같은 팀 색이 나온다.
                //   옛 줄(기록용): visual.Tracer = effects.BeginServerProjectile(visual.AttackerType, visual.Start);
                if (effects != null && launch.AttackerIsUnit)
                    visual.Tracer = effects.BeginServerProjectile(visual.AttackerType, launch.Team, visual.Start, visual.LastEndpoint);
                // [단계 C] 타워 투사체 — CannonCart 트레이서 프리셋(BuildingEffectConfig 종족별 줄).
                if (effects != null && !launch.AttackerIsUnit)
                    visual.Tracer = effects.BeginServerTowerProjectile(visual.TowerType, visual.TowerRace, visual.Start);
                slot.Visual = visual;

                // [단계 B — 의심 1 수정] 발사 신호(총구 VFX + 발사음)는 "서버 발사 기록이 화면에 나오는 이 순간"에만 낸다.
                //   늦은 접속 복원처럼 비행 중간부터 꺼낸 투사체는 이미 쏜 뒤라 발사 신호를 생략한다.
                double beginLag = double.IsNaN(_tickPresentationTime) ? 0d : _tickPresentationTime - launch.LaunchTime;
                bool midFlight = beginLag > MidFlightBeginSeconds;
                UnitAttackVfxPlaybackResult cueResult = default;
                if (launch.AttackerIsUnit && !midFlight && attackerView != null)
                    cueResult = attackerView.PlayServerProjectileLaunchCue();
                // [단계 C] 타워 발사 신호 — 포구(= 투사체 출발점)에서 타워 발사 이펙트를 1회. 방향 = 포구 → 대상 화면 위치.
                //   예전에는 서버 Tower 스냅샷(HitPresentationQueue → EffectManager.PlayAttackEffect)이 타워 루트에서 냈다.
                //   인간 타워는 이제 그 스냅샷을 내지 않으므로(TowerCombatUseCase) 여기 한 곳에서만 난다(이중 재생 없음).
                bool towerCue = false;
                if (!launch.AttackerIsUnit && !midFlight && effects != null)
                    towerCue = effects.PlayServerTowerLaunchCue(visual.TowerType, visual.TowerRace, visual.Start,
                        visual.LastCenter - visual.Start);
                DiagBegin(slot, beginLag, midFlight, visual.Tracer != null, cueResult.Status,
                    attackerView != null, attackerView != null && attackerView.HasAttackChargeMarker, towerCue);
            }
            catch (Exception exception) { Fault("begin", slot, exception); }
        }

        /// <summary>진행도에 맞춰 투사체를 옮긴다(Homing — 원래 대상의 화면 위치를 따라간다).</summary>
        public void Advance(ProjectilePresentationCoordinator.Slot slot, float progress, double local)
        {
            if (!(slot.Visual is Visual visual)) return;
            try
            {
                if (TryReadTargetCenter(visual, out Vector3 center)) visual.LastCenter = center;
                Vector3 endpoint = SurfacePoint(visual.Start, visual.LastCenter, slot.Launch.TargetRadius);
                if (slot.HasResult)
                {
                    // 서버 결과(표면 착탄 지점)가 오면 그 지점으로 짧게 맞춘다 — 양쪽 화면이 같은 지점에서 터지게(Q12·Q14).
                    Vector3 server = ServerPoint(slot.Result, endpoint.y);
                    float weight = (float)Math.Max(0d, Math.Min(1d, (local - slot.ResultReceivedLocal) / ResultBlendSeconds));
                    endpoint = Vector3.Lerp(endpoint, server, weight);
                }
                visual.LastEndpoint = endpoint;
                if (visual.Tracer != null) visual.Tracer.DriveTo(Vector3.Lerp(visual.Start, endpoint, progress));
            }
            catch (Exception exception) { Fault("advance", slot, exception); }
        }

        /// <summary>화면상 착탄 — 착탄 이펙트(+명중이면 숫자·피격 반응 1회). 결과가 없으면 투사체만 거둔다.</summary>
        public void Finish(ProjectilePresentationCoordinator.Slot slot, bool withResult)
        {
            Visual visual = slot.Visual as Visual;
            try
            {
                bool impactVfx = false;
                if (withResult && visual != null)
                {
                    Vector3 point = ServerPoint(slot.Result, visual.LastEndpoint.y);
                    // 착탄 이펙트는 명중·대상 소실 공통으로 같은 표면 지점에서 낸다(Q4 — 대상 소실이면 이펙트만).
                    // [단계 B 진단] 재생 여부(프리셋 유무)를 받아 센다. 옛 줄은 반환값을 버렸다(동작 같음):
                    //   EffectManager.Instance?.PlayServerProjectileImpact(visual.AttackerType, point, point - visual.Start);
                    EffectManager effects = EffectManager.Instance;
                    if (visual.AttackerIsUnit && effects != null)
                        impactVfx = effects.PlayServerProjectileImpact(visual.AttackerType, point, point - visual.Start);
                    // [단계 C] 타워 투사체 착탄 — CannonCart 착탄 프리셋(BuildingEffectConfig 종족별 줄), 같은 표면 지점.
                    if (!visual.AttackerIsUnit && effects != null)
                        impactVfx = effects.PlayServerTowerProjectileImpact(visual.TowerType, visual.TowerRace, point, point - visual.Start);
                }
                DiagFinish(slot, withResult, impactVfx);
                // 명중이면 이 투사체로 생긴 피격 표현(숫자·피격 VFX·펀치)을 지금 한 번 방출한다(Q7 — 숫자는 착탄 시각 1회).
                //   [2026-10-21 제안 A] 화면 착탄 지점도 함께 넘긴다. 사망타는 이 순간 대상 오브젝트가 이미 디스폰돼 있을 수 있어
                //   (Client는 0.10초 늦게 그린다) 큐가 이 지점에 숫자 "0"을 낸다. 대상이 살아 있으면 큐는 이 지점을 쓰지 않는다.
                //   옛 줄(기록용): _hits?.ReleaseProjectileImpact(slot.Key);
                if (withResult && slot.Result.Outcome == ProjectileImpactOutcome.Hit)
                    _hits?.ReleaseProjectileImpact(slot.Key, NumberFallbackPoint(slot, visual));
            }
            catch (Exception exception) { Fault("finish", slot, exception); }
            finally { EndVisual(slot); }
        }

        /// <summary>즉시 거둔다(게임 종료·epoch 교체) — 착탄 이펙트·숫자 없음.</summary>
        public void Abort(ProjectilePresentationCoordinator.Slot slot)
        {
            _view.Aborts++;   // [진단] 거둔 수(읽기 전용)
            EndVisual(slot);
        }

        // ====================================================================
        // 내부 — 위치 해석
        // ====================================================================

        // [단계 B] 옛 ResolveLaunchPoint를 "공격자 화면 찾기"와 "출발점 계산"으로 나눴다(발사 신호도 같은 화면에 내기 위해).
        //   옛 메서드(기록용 — 같은 규칙):
        //     if (launch.AttackerIsUnit && _unitFactory != null) { go = GetUnitObject(id); view = go.GetComponent<UnitView>();
        //         if (view != null && view.Data != null && view.Data.Id == launch.AttackerId && go.activeInHierarchy) return view.ProjectileLaunchPoint; }
        //     return ViewConverter.ToView(launch.Source) + Vector3.up * FallbackLaunchHeight;
        private UnitView ResolveAttackerView(ProjectileLaunchPresentation launch)
        {
            if (!launch.AttackerIsUnit || _unitFactory == null) return null;
            GameObject go = _unitFactory.GetUnitObject(launch.AttackerId);
            UnitView view = go != null ? go.GetComponent<UnitView>() : null;
            // 같은 객체 확인: View가 들고 있는 Data의 Id가 발사 기록과 같을 때만 그 총구를 쓴다(ID 재사용 안전).
            return view != null && view.Data != null && view.Data.Id == launch.AttackerId && go.activeInHierarchy
                ? view : null;
        }

        private void ResolveTarget(ProjectileLaunchPresentation launch, Visual visual)
        {
            if (launch.TargetIsUnit)
            {
                GameObject go = _unitFactory != null ? _unitFactory.GetUnitObject(launch.TargetId) : null;
                UnitView view = go != null ? go.GetComponent<UnitView>() : null;
                UnitData data = view != null ? view.Data : null;
                if (data == null || data.Id != launch.TargetId || !data.IsAlive || !go.activeInHierarchy) return;
                Transform pose = _pose?.GetUnitTransform(launch.TargetId);
                if (pose == null) return;
                visual.TargetObject = go; visual.TargetView = view; visual.TargetData = data; visual.TargetPose = pose;
                visual.LastCenter = pose.position;
                return;
            }
            GameObject building = _buildingFactory != null ? _buildingFactory.GetBuildingObject(launch.TargetId) : null;
            Transform buildingPose = _pose?.GetBuildingTransform(launch.TargetId);
            if (building == null || buildingPose == null) return;
            visual.TargetObject = building; visual.TargetPose = buildingPose;
            visual.LastCenter = buildingPose.position;
        }

        private static bool TryReadTargetCenter(Visual visual, out Vector3 center)
        {
            center = default;
            // 처음 붙잡은 그 객체만 따라간다. 파괴·비활성·다른 Data·사망이면 더 이상 따라가지 않는다(마지막 위치 고정).
            if (visual.TargetPose == null || visual.TargetObject == null || !visual.TargetObject.activeInHierarchy)
                return false;
            if (visual.TargetView != null
                && (!ReferenceEquals(visual.TargetView.Data, visual.TargetData) || !visual.TargetData.IsAlive))
                return false;
            center = visual.TargetPose.position;
            return true;
        }

        // 대상 "표면 지점" = 대상 화면 중심에서 출발점 쪽으로 반경만큼 당긴 점. 서버 계산(UnitCombatUseCase)과 같은 식이다.
        private static Vector3 SurfacePoint(Vector3 start, Vector3 center, float radius)
            => UnitCombatUseCase.ProjectileSurfacePoint(start, center, radius);

        // 서버 착탄 지점(도메인 좌표)을 이 화면 좌표로 바꾼다. 높이는 화면 끝점 높이를 쓴다(서버는 바닥 높이만 안다).
        private static Vector3 ServerPoint(ProjectileImpactPresentation result, float height)
        {
            Vector3 view = ViewConverter.ToView(result.ImpactPoint);
            view.y = height;
            return view;
        }

        /// <summary>
        /// [2026-10-21 제안 A] 피해 숫자·피격 VFX의 대체 위치 = 화면 착탄 지점(착탄 이펙트와 같은 XZ).
        /// 높이는 착탄 높이(가슴께) 대신 "대상 화면 중심의 마지막 높이"를 쓴다. 평소 숫자·피격 VFX는 대상 화면 위치
        /// (발밑 기준)에서 나오므로, 같은 높이를 써야 사망타 숫자가 다른 숫자들과 같은 높이에 뜬다.
        /// 화면 오브젝트를 못 만든 투사체(Begin 실패)는 지점이 없다 → NaN을 넘겨 큐가 "보관 때 기억한 위치"를 쓰게 한다.
        /// </summary>
        private static Vector3 NumberFallbackPoint(ProjectilePresentationCoordinator.Slot slot, Visual visual)
        {
            if (visual == null) return new Vector3(float.NaN, float.NaN, float.NaN);
            return ServerPoint(slot.Result, visual.LastCenter.y);
        }

        private void EndVisual(ProjectilePresentationCoordinator.Slot slot)
        {
            if (!(slot.Visual is Visual visual)) return;
            slot.Visual = null;
            try { EffectManager.Instance?.EndServerProjectile(visual.Tracer); }
            catch (Exception exception) { Fault("end", slot, exception); }
            visual.Tracer = null;
        }

        private void Fault(string stage, ProjectilePresentationCoordinator.Slot slot, Exception exception)
        {
            _faults++;
            if (_faults != 1 && _faults % 64 != 0) return;
            GameLog.Dev.Warn("Combat", nameof(ProjectilePresentationPresenter),
                "Projectile presentation degraded; HP authority unchanged",
                $"stage={stage}, projectile={slot.Key}, faults={_faults}, error={exception.GetType().Name}:{exception.Message}");
        }

        private void Detach()
        {
            if (_combat != null)
            {
                _combat.ProjectileLaunched -= ReceiveLaunch;
                _combat.ProjectileResolved -= ReceiveResult;
                _combat.ProjectileLaunchCancelled -= ReceiveLaunchCancel;   // [단계 D]
            }
            _endSubscription?.Dispose();
            _endSubscription = null;
            _coordinator.Close(this);
            // [진단] 경기 종료 END를 아직 안 남겼으면(종료 없이 맵이 바뀌거나 파괴된 경우) 거둔 수까지 센 뒤 지금 남긴다.
            if (_combat != null) WriteViewEnd("detach");
            _combat = null;
            _hits = null;
        }

        private void OnDestroy() => Detach();

        // ====================================================================
        // [2026-10-20 단계 B 진단 보강] 화면 쪽 계수·표본·END (읽기 전용 관측 — 릴리스 빌드에서는 호출이 사라진다)
        //
        // [다음 실기 분석에서 grep할 태그]
        //   [PROJECTILE-VIEW] stage=begin|finish  — 맵당 처음 24발의 꺼냄/착탄 시각 표본(서버 시각·화면 시각·지연).
        //   [PROJECTILE-END] side=view            — 경기 종료 때 2줄(v=2 part=1/2 · part=2/2, 싱글·Host·Client 각각).
        //     ※ 2026-10-21부터 logcat 줄 잘림(약 1024자) 때문에 2줄로 나눴다. 아래 목록 중 displayLag~verdict가 2줄째다.
        //     text=played:N|fallback:N|missing:N|missingDead:N — 투사체 피해 숫자 표시 결과(1줄째, hold= 바로 뒤).
        //     launch=recv:N|accepted:N  result=recv:N|accepted:N  begins=all:N|midFlight:N|tracerMissing:N
        //     cue=played:N|vfxStarted:N|skippedMidFlight:N|noAttackerView:N  finish=hit:N|lost:N  impactVfx=played:N|missing:N
        //     towerCue=played:N|skippedMidFlight:N|noPreset:N  ([단계 C] 타워 포구 발사 이펙트 — 건물 공격자만)
        //     aborts=N  rejected=...|order=...|timeouts=N  (ProjectilePresentationCoordinator)
        //     displayLag=...(화면 착탄 − 서버 착탄)  resultLag=...(결과 수신 − 서버 착탄)  beginLag=...(꺼냄 − 서버 발사)
        //     hold=held:N|atImpact:N|immediateAfterRelease:N|immediateCapacity:N|timeout:N|emptyRelease:N|heldNow:N|ownerNotReady:N
        //     markers=...|path=...|legacyTracer=...  (ServerProjectileDiagnostics — UnitView marker 계수)
        //     chargeCancel=recv:N|stopped:N|nothingActive:N|stale:N|notCharge:N|noView:N|suppressedAfterCancel:N
        //       ([2026-10-21 단계 D] 2줄째 markers 뒤 — 서버 발사 취소 통보로 차지 VFX를 끈 수 · 취소 뒤 차지 신호를 막은 수)
        // ====================================================================

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagBegin(ProjectilePresentationCoordinator.Slot slot, double beginLag, bool midFlight,
            bool tracer, UnitAttackVfxPlaybackStatus cueStatus, bool attackerView, bool chargeUnit, bool towerCue)
        {
            _view.Begins++;
            _view.BeginLag.Add(beginLag);
            if (!tracer) _view.TracerMissing++;
            string cue;
            // [단계 C] 옛 줄(기록용): if (!slot.Launch.AttackerIsUnit) cue = "not-unit";
            //   건물 공격자(타워)의 발사 신호를 따로 센다. 유닛 공격자 칸(played/vfxStarted/...)에는 섞지 않는다.
            if (!slot.Launch.AttackerIsUnit)
            {
                if (midFlight) { cue = "tower-skipped-mid-flight"; _view.TowerCueSkippedMidFlight++; }
                else if (towerCue) { cue = "tower-played"; _view.TowerCuePlayed++; }
                else { cue = "tower-no-preset"; _view.TowerCueNoPreset++; }
            }
            else if (midFlight) { cue = "skipped-mid-flight"; _view.CueSkippedMidFlight++; }
            else if (!attackerView) { cue = "no-attacker-view"; _view.CueNoAttackerView++; }
            else
            {
                _view.CuePlayed++;
                // 차지 유닛은 설계상 발사 신호에서 총구 VFX를 켜지 않는다(발사음만) — VFX 시작 수에 넣지 않는다.
                bool started = !chargeUnit && cueStatus == UnitAttackVfxPlaybackStatus.Started;
                if (started) _view.CueVfxStarted++;
                cue = chargeUnit ? "played-sfx-only-charge" : started ? "played-vfx-started" : "played-vfx-" + cueStatus;
            }
            if (midFlight) _view.BeginsMidFlight++;
            if (_view.Samples >= ViewSampleLimit) return;
            _view.Samples++;
            _view.Sampled.Add(slot.Key.Id);
            ProjectileLaunchPresentation launch = slot.Launch;
            GameLog.Dev.Info("Combat", nameof(ProjectilePresentationPresenter), "[PROJECTILE-VIEW]",
                FormattableString.Invariant($"role={ServerProjectileDiagnostics.RoleName}, stage=begin, projectile={slot.Key}, attacker={launch.AttackerId}, type={launch.AttackerKind}, target={launch.TargetId}, targetIsUnit={launch.TargetIsUnit}, launch={launch.LaunchTime:G17}, impact={launch.ImpactTime:G17}, presentation={_tickPresentationTime:G17}, beginLag={beginLag:F3}, midFlight={(midFlight ? 1 : 0)}, tracer={(tracer ? 1 : 0)}, cue={cue}, local={Time.realtimeSinceStartupAsDouble:F3}, sample={_view.Samples}/{ViewSampleLimit}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DiagFinish(ProjectilePresentationCoordinator.Slot slot, bool withResult, bool impactVfx)
        {
            double serverNow = ReadServerNow();
            double displayLag = double.NaN;
            if (withResult)
            {
                if (slot.Result.Outcome == ProjectileImpactOutcome.Hit) _view.FinishHit++;
                else _view.FinishLost++;
                if (impactVfx) _view.ImpactVfxPlayed++; else _view.ImpactVfxMissing++;
                displayLag = serverNow - slot.Result.ImpactTime;
                _view.DisplayLag.Add(displayLag);
            }
            if (!_view.Sampled.Contains(slot.Key.Id)) return;
            GameLog.Dev.Info("Combat", nameof(ProjectilePresentationPresenter), "[PROJECTILE-VIEW]",
                FormattableString.Invariant($"role={ServerProjectileDiagnostics.RoleName}, stage=finish, projectile={slot.Key}, withResult={(withResult ? 1 : 0)}, outcome={(withResult ? slot.Result.Outcome.ToString() : "timeout")}, serverImpact={(withResult ? slot.Result.ImpactTime : slot.Launch.ImpactTime):G17}, serverNow={serverNow:G17}, presentation={_tickPresentationTime:G17}, displayLag={displayLag:F3}, impactVfx={(impactVfx ? 1 : 0)}, waitedAtEndpoint={(double.IsNaN(slot.ArrivedLocal) ? 0 : 1)}, local={Time.realtimeSinceStartupAsDouble:F3}"));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void WriteViewEnd(string reason)
        {
            ViewCounters v = _view;
            if (v == null || v.EndWritten) return;
            v.EndWritten = true;
            int holdTimeouts = _hits != null ? _hits.ProjectileHoldTimeoutItems : 0;
            int coordinatorAttention = _coordinator.RejectedInvalid + _coordinator.RejectedEpoch
                + _coordinator.RejectedCapacity + _coordinator.RejectedDuplicateResult + _coordinator.RejectedFinishedResult;
            var attention = new System.Collections.Generic.List<string>(6);
            if (_coordinator.ResultTimeouts > 0) attention.Add("result-timeouts");
            if (coordinatorAttention > 0) attention.Add("rejected");
            if (holdTimeouts > 0) attention.Add("hold-timeouts");
            if (ServerProjectileDiagnostics.ServerMarkerMuzzleCount > 0) attention.Add("muzzle-at-marker");
            if (_faults > 0) attention.Add("faults");
            if (v.LaunchAccepted > 0 && v.Begins == 0) attention.Add("never-drawn");
            // [2026-10-21 제안 A 진단] 투사체 피해 숫자를 끝내 못 낸 수가 있으면 주의 표시(관측만 — 동작 판단에 쓰지 않는다).
            if (_hits != null && _hits.ProjectileTextMissingItems > 0) attention.Add("text-missing");

            // [2026-10-21] 기기 logcat은 한 줄을 약 1024자에서 자른다(2026-10-21 실기에서 Host END 뒷부분 유실 확인).
            //   그래서 END를 2줄(part=1/2 · part=2/2)로 나눈다. 두 줄 모두 같은 머리(role·epoch·reason)를 달아 짝을 맞출 수 있다.
            //   형식이 바뀌었으므로 v=1 → v=2. 1줄째 = 개수(받음·그림·착탄·일정표·보관함·숫자 결과), 2줄째 = 시간 분포·marker·verdict.
            //   옛 1줄 형식(기록용): "side=view v=1 role=.. epoch=.. reason=.. switch=.. launch=.. ... hold=.. markers=.. verdict=.."
            string head = FormattableString.Invariant(
                $"side=view v=2 role={ServerProjectileDiagnostics.RoleName} epoch={_epoch:x16} reason={reason} ");
            string part1 = head + "part=1/2 "
                + FormattableString.Invariant($"switch={ServerProjectileDeliveryPolicy.ManifestToken} ")
                + FormattableString.Invariant(
                $"launch=recv:{v.LaunchReceived}|accepted:{v.LaunchAccepted} result=recv:{v.ResultReceived}|accepted:{v.ResultAccepted} epochResets={v.EpochResets} ")
                + FormattableString.Invariant(
                $"begins=all:{v.Begins}|midFlight:{v.BeginsMidFlight}|tracerMissing:{v.TracerMissing} cue=played:{v.CuePlayed}|vfxStarted:{v.CueVfxStarted}|skippedMidFlight:{v.CueSkippedMidFlight}|noAttackerView:{v.CueNoAttackerView} ")
                // [단계 C] 타워 발사 신호 칸(건물 공격자만). 옛 1줄째에는 이 칸이 없었다.
                + FormattableString.Invariant(
                $"towerCue=played:{v.TowerCuePlayed}|skippedMidFlight:{v.TowerCueSkippedMidFlight}|noPreset:{v.TowerCueNoPreset} ")
                + FormattableString.Invariant(
                $"finish=hit:{v.FinishHit}|lost:{v.FinishLost} impactVfx=played:{v.ImpactVfxPlayed}|missing:{v.ImpactVfxMissing} aborts={v.Aborts} faults={_faults} ")
                + _coordinator.DescribeForDiagnostics()
                + " hold=" + (_hits != null ? _hits.DescribeProjectileHoldForDiagnostics() : "unavailable")
                + " text=" + (_hits != null ? _hits.DescribeProjectileTextForDiagnostics() : "unavailable");
            string part2 = head + "part=2/2"
                + " displayLag=" + v.DisplayLag.Format() + " resultLag=" + v.ResultLag.Format() + " beginLag=" + v.BeginLag.Format()
                + " " + ServerProjectileDiagnostics.DescribeView()
                + " " + ServerProjectileDiagnostics.DescribeCharge()   // [단계 D] 차지 VFX 취소 표현 계수
                + " verdict=" + (attention.Count == 0 ? "PASS" : "ATTENTION attention=" + string.Join(",", attention));
            GameLog.Dev.Info("Combat", nameof(ProjectilePresentationPresenter), "[PROJECTILE-END]", part1);
            GameLog.Dev.Info("Combat", nameof(ProjectilePresentationPresenter), "[PROJECTILE-END]", part2);
        }
    }
}
