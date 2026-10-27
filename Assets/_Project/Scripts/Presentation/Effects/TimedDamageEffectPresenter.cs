using System;
using System.Collections.Generic;
using Hexiege.Application;
using Hexiege.Domain;
using UnityEngine;
using UniRx;
namespace Hexiege.Presentation
{
    public sealed class TimedDamageEffectPresenter : MonoBehaviour
    {
        // [2026-10-26 수정안 (a)] ReceivedLocal·AlignHeld 추가 — 화상 VFX 시작을 투사체 화면 착탄까지 미루는 판정용(아래 ShouldHoldStart).
        // [2026-10-26 Plan 10 J1·J4] Projector·진단 계수 3개 추가(아래 「따라가기 직전 바로잡기」 절).
        //   옛 줄(기록용): private sealed class Owner { public UnitData Target; public TimedDamageEffectPresentation State; public Transform Pose; public VfxPoolItem Item; public ulong Lease; public bool Pending; public double ReceivedLocal = double.NaN; public bool AlignHeld; public bool AlignCounted; }
        private sealed class Owner
        {
            public UnitData Target; public TimedDamageEffectPresentation State; public Transform Pose; public VfxPoolItem Item; public ulong Lease; public bool Pending;
            public double ReceivedLocal = double.NaN; public bool AlignHeld; public bool AlignCounted;
            // Pose(보이는 몸 = VisualRoot)를 화면 위치로 바로잡아 주는 그 유닛의 투영기. Pose를 얻을 때 1번만 찾아 둔다(매 프레임 검색 없음).
            //   없으면 null — Visual Root가 없는 옛 프리팹 등. 그때는 예전처럼 저장된 Pose를 그대로 읽는다.
            public VisualRootProjector Projector;
            // [진단 전용 — 에디터·개발 빌드] 이 대상에서 바로잡기 전후 차이가 문턱을 넘은 프레임 수 · 지금까지 기록한 최대 차이 · 기록한 줄 수.
            public int PoseOverCount; public float PosePeak; public int PoseLogCount;
        }
        private readonly Dictionary<int, Owner> _owners = new();
        private readonly List<Owner> _iteration = new();
        private readonly List<TimedDamageEffectChannel.Entry> _baseline = new();
        private TimedDamageEffectChannel _channel;
        private EffectManager _manager;
        private Func<UnitData, Transform> _pose;
        private IDisposable _end, _death;
        private bool _closed = true;
        // ────────────────────────────────────────────────────────────────
        // [2026-10-26 수정안 (a)] 화상(InfernoSpirit Burn) VFX 시작을 투사체 화면 착탄에 맞춘다.
        //   서버는 투사체가 닿는 순간 화상을 건다. Client 화면은 투사체를 0.10초 늦게 그리므로, 받자마자 켜면 불이 먼저 붙어 보였다.
        //   그래서 "화면 시각(서버 시각 − 표시 지연) ≥ 화상 시작 시각" 그리고 "같은 공격자의 투사체 피해가 화면 착탄을 기다리는 중이 아님"이
        //   될 때까지 시작만 미룬다(상한 0.3초 — ProjectileAlignedTimedDamageStart). 화상 피해·틱·지속 시간은 그대로다.
        //   조합 루트가 SetProjectileImpactAlignment로 두 함수를 넣지 않으면 예전처럼 바로 시작한다.
        // ────────────────────────────────────────────────────────────────
        private Func<double> _presentationClock;
        private Func<UnitData, int, bool> _heldProjectileHit;

        // ────────────────────────────────────────────────────────────────
        // [2026-10-26 Plan 10 J1·J2·J4] 따라가기 직전 바로잡기 — 뒤집힌 Client에서 화상·독이 "잠깐씩 옆으로 튀는" 현상 대응.
        //   (근거: Assets/_Project/Docs/_Tasks/2026-10-26/18_34_status-effect-width-fit/Research.md 12-4 — 코드 읽기로 좁힌 추정)
        //
        //   ■ 배경 — 유닛 하나는 두 Transform으로 이루어진다.
        //     · Simulation Root(유닛 GameObject 자신): 서버가 정한 "실제 위치". NetworkTransform이 Update 중에 옮기고 돌린다.
        //     · Visual Root(그 직계 자식, 메시·Animator가 있는 쪽): 화면에 "보이는 몸". VisualRootProjector가 위치를 정해 준다.
        //     Red 관점(화면이 뒤집힌) Client는 보이는 몸을 맵 중심 반대편(점대칭)·180° 돌린 방향에 그린다(ProjectNow).
        //
        //   ■ 문제 — 보이는 몸은 실제 위치의 "자식"이라, 실제 위치가 이번 프레임에 돌면 보이는 몸도 함께 휘둘려
        //     VisualRootProjector.LateUpdate가 다시 바로잡기 전까지 틀린 자리에 있다(맵 중심에서 먼 유닛일수록 크게 — 1~2 m).
        //     이 presenter와 VisualRootProjector는 둘 다 LateUpdate에서 돌지만 순서가 정해져 있지 않다(DefaultExecutionOrder 없음).
        //     이 presenter가 먼저 돌면, 처음 1번 저장해 둔 Pose(= Visual Root Transform)를 그대로 읽어 그 틀린 자리로 이펙트를 옮겼다.
        //     (Pose를 처음 얻을 때는 PresentationTransform getter가 바로잡아 주지만, 그 뒤 매 프레임 읽기는 그 바로잡기를 거치지 않았다.)
        //
        //   ■ 해결 — 읽기 직전에 그 유닛의 ProjectNow()를 직접 1번 부른다. 그러면 실행 순서와 상관없이 "이번 프레임의 바로잡힌 위치"를 읽는다.
        //     · ProjectNow()는 같은 입력이면 같은 결과를 쓰는 멱등 함수다 → 이번 프레임에 projector LateUpdate가 또 불러도 같은 값이다.
        //     · ProjectNow()는 Visual Root만 쓰고 Simulation Root(서버 위치)는 절대 쓰지 않는다 → 판정·피해·이동에 영향 없음.
        //     · Host·싱글·뒤집히지 않은 Client는 원래 Visual Root = Root pose라 결과가 바뀌지 않는다.
        //     · 사망 대역(ghost)으로 떼어진 뒤에는 projector가 Visual Root를 놓아 버려 ProjectNow()가 아무것도 하지 않는다.
        //   화상·독은 이 presenter 하나를 같이 쓰므로 한 곳 수정으로 둘 다 적용된다. 힐(BloomHealEffectPresenter)은 이번 범위 밖(J3).
        //
        //   ■ J4 결정 — "Owner에 projector를 저장"(b). 매 프레임 getter를 다시 타는 (a)를 쓰지 않은 이유:
        //     (a)의 pose 함수(조합 루트 람다)는 부를 때마다 유닛 GameObject 조회 + GetComponent 2회를 하고,
        //     "View가 그 대상을 들고 있고 활성인가"를 다시 판정해 null을 돌려줄 수 있다 → 매 프레임 비용이 늘고, 정지 조건까지 바뀐다(범위 초과).
        //     (b)는 지금처럼 Pose를 처음 1번만 얻고, 그때 짝이 되는 projector도 1번만 찾아 둔다 → 프레임당 추가 비용은 ProjectNow() 1회, 할당 0.
        //     같은 방식의 선례: 타워 볼트의 TowerBoltTarget.Projector(VfxPoolItem.TowerBolt.cs).
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// [2026-10-26 Plan 10 J4] Pose(보이는 몸 Transform)를 바로잡아 주는 그 유닛의 VisualRootProjector를 찾는다. Pose를 얻은 직후 1번만 부른다.
        /// 찾은 projector가 정말 이 Pose의 주인인지(projector가 돌려주는 화면 Transform이 이 Pose와 같은지) 확인한 뒤에만 돌려준다.
        /// </summary>
        /// <param name="pose">조합 루트의 pose 함수가 돌려준 Transform(보통 Visual Root).</param>
        /// <returns>이 Pose를 쓰는 projector. 없거나 Pose가 Visual Root가 아니면 null(그때는 바로잡기 없이 예전처럼 읽는다).</returns>
        private static VisualRootProjector ResolveProjector(Transform pose)
        {
            if (pose == null) return null;
            // Visual Root는 projector가 붙은 Simulation Root의 자식이므로 부모 쪽으로 찾는다(자기 자신 포함, 비활성 포함).
            VisualRootProjector projector = pose.GetComponentInParent<VisualRootProjector>(true);
            if (projector == null || projector.transform == pose) return null; // Root 자체를 받았다면 바로잡을 Visual Root가 없다.
            // PresentationTransform getter는 ProjectNow()(멱등) 뒤 Visual Root를 돌려준다 — 그 Transform이 이 Pose와 같아야 짝이다.
            return projector.PresentationTransform == pose ? projector : null;
        }

        // ── [2026-10-26 Plan 10 J5] 진단 로그(에디터·개발 빌드 전용 — 릴리스에서는 호출과 인자 계산이 통째로 사라진다, LogRules 1.7) ──
        //   뒤집힌 Client에서 "바로잡기 전 위치"와 "바로잡은 위치"가 문턱보다 크게 다를 때, 그 대상의 최대값을 새로 넘을 때만 1줄 남긴다.
        //   · 문턱 0.05 m — Plan 10-4 3번 제안값. 회전 프레임의 1~2 m보다 훨씬 작다.
        //   · 단, 뒤집힌 화면에서는 이동만 해도 차이가 "프레임당 이동 거리의 2배"(자식은 +Δ, 바로잡은 위치는 −Δ)라
        //     빠른 유닛은 0.05 m를 매 프레임 넘을 수 있다 `[계산]`. 그래서 "넘을 때마다"가 아니라 "그 대상 최대값 갱신 때만" 남겨
        //     이동 프레임이 상한을 다 써 버려 회전 프레임(큰 값)이 묻히지 않게 했다. 넘은 프레임 수는 Over= 로 함께 적는다.
        //   · 상한: 대상 1건당 4줄, presenter 전체 64줄(Initialize마다 다시 센다). 넘으면 "DiagnosticLimit" 1줄만 남기고 멈춘다.
        //   · Host·싱글·뒤집히지 않은 Client는 기록하지 않는다(애초에 차이가 0이다).
        private const float PoseDiagThreshold = 0.05f;
        private const int PoseDiagPerOwnerLimit = 4;
        private const int PoseDiagTotalLimit = 64;
        private int _poseDiagTotal;

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void RecordPoseCorrection(Owner owner, Vector3 before, bool following)
        {
            if (!NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer || !Hexiege.Core.ViewConverter.IsFlipped) return;
            float delta = Vector3.Distance(before, owner.Pose.position);
            if (delta <= PoseDiagThreshold) return;
            owner.PoseOverCount++;
            if (delta <= owner.PosePeak || owner.PoseLogCount >= PoseDiagPerOwnerLimit || _poseDiagTotal > PoseDiagTotalLimit) return;
            owner.PosePeak = delta;
            if (++_poseDiagTotal > PoseDiagTotalLimit)
            { _channel.Record("PoseCorrected", owner.State, "DiagnosticLimit=" + PoseDiagTotalLimit + "; Completeness=Unknown"); return; }
            owner.PoseLogCount++;
            _channel.Record("PoseCorrected", owner.State, System.FormattableString.Invariant(
                $"Delta={delta:0.###}; Over={owner.PoseOverCount}; Phase={(following ? "Follow" : "Start")}; Flipped=True; Line={owner.PoseLogCount}/{PoseDiagPerOwnerLimit}"));
        }

        /// <summary>
        /// [2026-10-26 수정안 (a)] 화상 VFX 시작 보류에 쓸 두 함수를 넣는다(조합 루트 전용).
        /// </summary>
        /// <param name="presentationClock">이 화면의 표시 시각(서버 시계 − Client 표시 지연, 싱글·Host는 지연 0).</param>
        /// <param name="heldProjectileHit">(대상, 공격자 Id) → 그 공격자의 투사체 피해가 대상에게 아직 화면 착탄 대기 중인가.</param>
        public void SetProjectileImpactAlignment(Func<double> presentationClock, Func<UnitData, int, bool> heldProjectileHit)
        {
            _presentationClock = presentationClock;
            _heldProjectileHit = heldProjectileHit;
        }

        // 화상·독 VFX 시작을 이번 프레임 미뤄야 하는가. 판단은 순수 함수(ProjectileAlignedTimedDamageStart)가 한다.
        // [2026-10-27 서버 권위 투사체 단계 E — 사용자 결정 E-B (a)] 독(MushroomBomber)도 화상과 같은 장치로 미룬다.
        //   "이 지속 피해가 서버 투사체 착탄으로 시작하는가"(화상 = Inferno 스위치, 독 = Mushroom 스위치)는
        //   ProjectileAlignedTimedDamageStart.IsProjectileAligned 한 곳이 정한다(종류 → 유닛 대응을 여기 두지 않는다).
        private bool ShouldHoldStart(Owner owner)
        {
            if (_presentationClock == null
                || !ProjectileAlignedTimedDamageStart.IsProjectileAligned(owner.State.Visual)) return false;
            double now = Time.realtimeSinceStartupAsDouble;
            if (double.IsNaN(owner.ReceivedLocal)) owner.ReceivedLocal = now;
            double presentationNow;
            try { presentationNow = _presentationClock(); } catch { presentationNow = double.NaN; }
            bool held = false;
            try { held = _heldProjectileHit != null && _heldProjectileHit(owner.Target, owner.State.SourceId); } catch { held = false; }
            double waited = now - owner.ReceivedLocal;
            bool wait = ProjectileAlignedTimedDamageStart.ShouldWait(owner.State.Visual, owner.State.StartTime, presentationNow, held, waited);
            if (wait)
            {
                // [단계 E] 화상·독을 따로 센다 — 종류를 넘긴다.
                if (!owner.AlignHeld) { owner.AlignHeld = true; ProjectileAlignedTimedDamageDiagnostics.RecordHeld(owner.State.Visual); }
                return true;
            }
            // [진단] 한 화상·독에 1번만 센다(프리셋이 없어 시작이 매 프레임 재시도되는 경우에도 중복으로 세지 않게).
            if (!owner.AlignCounted)
            {
                owner.AlignCounted = true;
                if (owner.AlignHeld) ProjectileAlignedTimedDamageDiagnostics.RecordReleased(owner.State.Visual,
                    waited >= ProjectileAlignedTimedDamageStart.MaxHoldSeconds);
                else ProjectileAlignedTimedDamageDiagnostics.RecordImmediate(owner.State.Visual);
            }
            return false;
        }
        public void Initialize(TimedDamageEffectChannel channel, EffectManager manager, Func<UnitData, Transform> pose)
        {
            Close("Reinitialize"); _channel = channel; _manager = manager; _pose = pose;
            _poseDiagTotal = 0; // [Plan 10 J5] 진단 상한은 초기화(경기)마다 다시 센다.
            if (channel == null || channel.Closed) return;
            _closed = false; channel.Changed += Current; channel.AttachmentClosed += Attachment; channel.VisualsClosed += Close;
            _end = GameEvents.OnGameEnd.Subscribe(_ => channel.CloseVisuals("GameEnd"));
            _death = GameEvents.OnUnitDied.Subscribe(e => channel.CloseAttachment(e.Unit, "LocalDeath"));
            channel.CopyCurrent(_baseline);
            foreach (var entry in _baseline) Current(entry.State, entry.Target);
            _baseline.Clear();
        }
        private void Current(TimedDamageEffectPresentation state, UnitData target)
        {
            if (_closed || !state.IsValid || target == null) return;
            if (_owners.TryGetValue(state.TargetId, out var owner))
            {
                if (owner.State.SameOwner(state) && state.Revision <= owner.State.Revision) return;
                if (owner.State.SameOwner(state) && ReferenceEquals(owner.Target, target) && owner.State.Visual == state.Visual && state.Active)
                { owner.State = state; owner.Item?.RefreshTimedDamage(owner.Lease, state); return; }
                if (!state.Active && owner.State.SameOwner(state)) owner.State = state;
                Stop(owner, state.Active ? "Overwrite" : "ServerStop"); _owners.Remove(state.TargetId);
            }
            if (!state.Active || state.Visual == TimedDamageVisual.None)
            { _channel.Record("NotStarted", state, state.Active ? "UnspecifiedProvenance" : "StopBeforeReady"); return; }
            // [수정안 (a)] 받은 실제 시각을 기록한다(화상 VFX 시작 보류의 상한 0.3초 기준).
            _owners[state.TargetId] = new Owner { Target = target, State = state, ReceivedLocal = Time.realtimeSinceStartupAsDouble };
        }
        private void LateUpdate()
        {
            if (_closed) return;
            _iteration.Clear(); _iteration.AddRange(_owners.Values);
            foreach (var owner in _iteration)
            {
                try
                {
                    if (!owner.Target.IsAlive) { _channel.CloseAttachment(owner.Target, "LocalDeath"); continue; }
                    // [2026-10-26 Plan 10 J4] Pose를 처음 얻을 때 짝이 되는 projector도 1번만 찾아 둔다.
                    //   옛 줄(기록용): if (owner.Pose == null) owner.Pose = _pose?.Invoke(owner.Target);
                    if (owner.Pose == null) { owner.Pose = _pose?.Invoke(owner.Target); owner.Projector = ResolveProjector(owner.Pose); }
                    if (owner.Pose == null || !owner.Pose.gameObject.activeInHierarchy)
                    {
                        if (owner.Item != null) { owner.Item.StopTimedDamage(owner.Lease, "PoseUnavailable"); owner.Item = null; }
                        owner.Pose = null; owner.Projector = null; // [Plan 10] 다음에 Pose를 다시 얻을 때 projector도 다시 찾는다.
                        if (!owner.Pending) { _channel.Record("Pending", owner.State, "PoseUnavailable"); owner.Pending = true; }
                        continue;
                    }
                    // [2026-10-26 Plan 10 J1·J2] 읽기 직전에 보이는 몸을 이번 프레임의 화면 위치로 바로잡는다(위 「따라가기 직전 바로잡기」 절).
                    //   아래 두 읽기 — 따라가기(FollowTimedDamage)와 재생 시작(StartTimedDamageEffect, 첫 위치를 Pose에서 읽는다) — 모두 이 뒤에 온다.
                    //   시작도 함께 덮는 이유: Pose는 시작보다 앞선 프레임에 얻었을 수 있다(화상 시작 보류 최대 0.3초 · PoseUnavailable 대기 뒤 재시도).
                    //   그 사이 실제 위치가 돌았다면 첫 위치부터 틀린 자리에 나올 수 있으므로, 시작 직전에도 같은 바로잡기가 필요하다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Vector3 poseBeforeProjection = owner.Pose.position; // [진단 전용] 바로잡기 전 위치(릴리스 빌드에는 이 줄이 없다).
#endif
                    if (owner.Projector != null) owner.Projector.ProjectNow();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    RecordPoseCorrection(owner, poseBeforeProjection, owner.Item != null);
#endif
                    if (owner.Item != null) { owner.Item.FollowTimedDamage(owner.Lease, owner.Pose); continue; }
                    // [2026-10-26 수정안 (a)] 화상은 투사체가 화면에서 닿을 때까지(상한 0.3초) 시작을 미룬다. 독·이미 켠 이펙트는 해당 없음.
                    if (ShouldHoldStart(owner)) continue;
                    // [2026-10-13 C] 맞은 유닛 타입을 넘겨 화상·독 이펙트를 그 유닛 크기에 맞춘다(표에 없으면 배율 1).
                    owner.Item = _manager?.StartTimedDamageEffect(owner.State, owner.Pose, _channel, owner.Target.Type);
                    if (owner.Item != null) { owner.Lease = owner.Item.DamageLease; owner.Pending = false; }
                    else if (!owner.Pending) { _channel.Record("Skip", owner.State, "MissingPreset; Pending=True"); owner.Pending = true; }
                }
                catch (Exception ex) { _channel.Record("Fault", owner.State, ex.GetType().Name + ": " + ex.Message); Stop(owner, "PlaybackFault"); _owners.Remove(owner.State.TargetId); }
            }
            _iteration.Clear();
        }
        private void Stop(Owner owner, string reason)
        { if (owner.Item != null) { owner.Item.RefreshTimedDamage(owner.Lease, owner.State); owner.Item.StopTimedDamage(owner.Lease, reason); } else _channel.Record("NotStarted", owner.State, reason); owner.Item = null; owner.Target = null; owner.Pose = null; owner.Projector = null; }
        private void Attachment(UnitData target, string reason)
        { if (target != null && _owners.TryGetValue(target.Id, out var owner) && ReferenceEquals(owner.Target, target)) { Stop(owner, reason); _owners.Remove(target.Id); } }
        private void Close(string reason)
        {
            if (_closed) return; _closed = true;
            foreach (var owner in _owners.Values) Stop(owner, reason);
            _owners.Clear(); _iteration.Clear(); _baseline.Clear();
            _channel.Changed -= Current; _channel.AttachmentClosed -= Attachment; _channel.VisualsClosed -= Close;
            _end?.Dispose(); _death?.Dispose(); _end = _death = null; _pose = null;
        }
        private void OnDestroy() { _channel?.CloseVisuals("SceneTeardown"); Close("SceneTeardown"); }
    }
}
