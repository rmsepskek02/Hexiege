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
//   4) 게임 종료·재초기화·파괴 때 남은 투사체를 즉시 거둔다(착탄 이펙트·숫자 없음).
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
        /// <summary>Client 화면 지연(초). 기존 서버 확정 표현(C3·버섯·파도)과 같은 값.</summary>
        public const double ClientPresentationDelaySeconds =
            Hexiege.Application.Combat.Sequencing.AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds;

        /// <summary>서버 결과가 도착한 뒤 끝점을 서버 착탄 지점으로 옮기는 데 걸리는 시간(초) — 끝점이 튀지 않게.</summary>
        private const double ResultBlendSeconds = 0.10d;

        /// <summary>쏜 유닛 화면을 찾지 못했을 때 서버 발사 위치에서 띄우는 높이(미터, 화면 표현 전용 대체값).</summary>
        private const float FallbackLaunchHeight = 0.3f;

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
            _coordinator.Reset(_epoch, this);
            combat.ProjectileLaunched += ReceiveLaunch;
            combat.ProjectileResolved += ReceiveResult;
            _endSubscription = GameEvents.OnGameEnd.Subscribe(_ =>
            {
                // 게임 종료 — 서버도 남은 투사체를 피해 없이 닫았다. 화면도 즉시 거둔다(착탄 이펙트·숫자 없음).
                _ended = true;
                _coordinator.Close(this);
            });
        }

        private void EnsureEpoch()
        {
            // Client는 서버 epoch를 받은 뒤 UseCase의 epoch가 바뀐다. 바뀌면 일정표를 새 epoch로 시작한다.
            if (_combat == null || _epoch == _combat.ProjectileEpoch) return;
            _epoch = _combat.ProjectileEpoch;
            _coordinator.Reset(_epoch, this);
        }

        private void ReceiveLaunch(ProjectileLaunchPresentation launch)
        {
            if (_ended) return;
            EnsureEpoch();
            _coordinator.AcceptLaunch(launch, Time.realtimeSinceStartupAsDouble);
        }

        private void ReceiveResult(ProjectileImpactPresentation result)
        {
            if (_ended) return;
            EnsureEpoch();
            _coordinator.AcceptResult(result, Time.realtimeSinceStartupAsDouble);
        }

        private void Update()
        {
            if (_combat == null || _ended) return;
            EnsureEpoch();
            double serverNow = _combat.ProjectileServerTime;
            if (double.IsNaN(serverNow) || double.IsInfinity(serverNow)) return;
            bool pureClient = NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer;
            double presentationTime = serverNow - (pureClient ? ClientPresentationDelaySeconds : 0d);
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
                    LastCenter = ViewConverter.ToView(launch.TargetCenter)
                };
                visual.Start = ResolveLaunchPoint(launch);
                ResolveTarget(launch, visual);
                visual.LastEndpoint = SurfacePoint(visual.Start, visual.LastCenter, launch.TargetRadius);
                EffectManager effects = EffectManager.Instance;
                if (effects != null && launch.AttackerIsUnit)
                    visual.Tracer = effects.BeginServerProjectile(visual.AttackerType, visual.Start);
                slot.Visual = visual;
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
                if (withResult && visual != null)
                {
                    Vector3 point = ServerPoint(slot.Result, visual.LastEndpoint.y);
                    // 착탄 이펙트는 명중·대상 소실 공통으로 같은 표면 지점에서 낸다(Q4 — 대상 소실이면 이펙트만).
                    if (visual.AttackerIsUnit)
                        EffectManager.Instance?.PlayServerProjectileImpact(visual.AttackerType, point, point - visual.Start);
                }
                // 명중이면 이 투사체로 생긴 피격 표현(숫자·피격 VFX·펀치)을 지금 한 번 방출한다(Q7 — 숫자는 착탄 시각 1회).
                if (withResult && slot.Result.Outcome == ProjectileImpactOutcome.Hit)
                    _hits?.ReleaseProjectileImpact(slot.Key);
            }
            catch (Exception exception) { Fault("finish", slot, exception); }
            finally { EndVisual(slot); }
        }

        /// <summary>즉시 거둔다(게임 종료·epoch 교체) — 착탄 이펙트·숫자 없음.</summary>
        public void Abort(ProjectilePresentationCoordinator.Slot slot) => EndVisual(slot);

        // ====================================================================
        // 내부 — 위치 해석
        // ====================================================================

        private Vector3 ResolveLaunchPoint(ProjectileLaunchPresentation launch)
        {
            if (launch.AttackerIsUnit && _unitFactory != null)
            {
                GameObject go = _unitFactory.GetUnitObject(launch.AttackerId);
                UnitView view = go != null ? go.GetComponent<UnitView>() : null;
                // 같은 객체 확인: View가 들고 있는 Data의 Id가 발사 기록과 같을 때만 그 총구를 쓴다(ID 재사용 안전).
                if (view != null && view.Data != null && view.Data.Id == launch.AttackerId && go.activeInHierarchy)
                    return view.ProjectileLaunchPoint;
            }
            return ViewConverter.ToView(launch.Source) + Vector3.up * FallbackLaunchHeight;
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
            }
            _endSubscription?.Dispose();
            _endSubscription = null;
            _coordinator.Close(this);
            _combat = null;
            _hits = null;
        }

        private void OnDestroy() => Detach();
    }
}
