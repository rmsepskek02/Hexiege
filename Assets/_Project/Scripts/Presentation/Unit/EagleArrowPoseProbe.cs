// ============================================================================
// EagleArrowPoseProbe.cs
// [2026-10-27 EagleArcher 손 화살 튐 — 진단 전용(개발)] 손에 든 화살(EagleArrow)이
// "켜져 있는 동안 제자리(공격 클립 자세)에서 벗어나거나 한 프레임에 크게 움직이는지"를 로그로 확인한다.
//
// [왜 필요한가 — 유니티 초급자용]
//   손 화살은 공격 클립(EagleArcher_Attack.anim)이 켜고(m_IsActive=1) 손 안의 위치·회전을 고정값으로 쓴다.
//   Walk → Attack 으로 CrossFade(섞어서 전환)하는 짧은 시간에는 Animator가
//   "프리팹 기본값(Walk 쪽, 화살 커브 없음)"과 "공격 클립 값"을 섞어 쓴다.
//   두 값이 다르면 화살이 켜지는 순간 엉뚱한 곳에서 나타나 제자리로 튀어 보인다.
//   이 튐은 수 프레임이라 육안으로 판정하기 어렵다 → 매 프레임 재되, 로그는 "사건"이 생길 때만 남긴다.
//
// [무엇을 재나]
//   이 컴포넌트는 화살 오브젝트 자체에 붙는다. 꺼진 오브젝트의 LateUpdate는 불리지 않으므로
//   "화살이 켜져 있는 프레임"만 자동으로 잰다(꺼져 있으면 비용 0).
//   · offset : 지금 손 안 위치와 공격 클립 위치의 차이(월드 미터로 환산 — 손 배율 반영)
//   · angle  : 지금 손 안 회전과 공격 클립 회전의 차이(도)
//   · step   : 직전 프레임 대비 손 안 위치 변화(월드 미터) — 손 자체가 움직이는 것은 빼고 "손 안에서 움직인 양"만 본다
//   손 안 좌표(localPosition)로 비교하므로 유닛 이동·손 흔들림·Red Client 화면 반전은 값에 섞이지 않는다.
//
// [로그 — 아래 두 태그로 grep]
//   [EAGLE-ARROW-EVENT] : 사건 1건(경기당 처음 EventLimit건만). kind=offPose(제자리 이탈 구간 시작) | step(한 프레임 급이동)
//   [EAGLE-ARROW]       : 경기 종료 때 요약 1줄(verdict=PASS | JUMP | NO-SAMPLES)
//   두 줄 모두 GameLog.Dev(개발 로그, LogRules 1.2 축A Info · 축B 개발) — 릴리스 빌드에서는 이 파일 전체가 컴파일되지 않는다.
//
// [이 컴포넌트가 하지 않는 것]
//   화살의 위치·회전·켜짐을 읽기만 하고 절대 쓰지 않는다. 전투·타이밍·애니메이션 재생에 영향 없음.
//
// UnitView.Initialize(개발 빌드·에디터 전용 블록)가 EagleArcher일 때만 AttachTo로 붙인다. Presentation 레이어(MonoBehaviour).
// ============================================================================

#if DEVELOPMENT_BUILD || UNITY_EDITOR

using System;
using UniRx;
using UnityEngine;
using Hexiege.Application;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    /// <summary>
    /// [개발 전용 진단] EagleArcher 손 화살의 "켜진 동안 제자리 이탈 / 한 프레임 급이동"을 재서 로그로 남긴다.
    /// 읽기 전용 관측이며 릴리스 빌드에는 존재하지 않는다.
    /// </summary>
    public sealed class EagleArrowPoseProbe : MonoBehaviour
    {
        /// <summary>프리팹 안 손 화살 오브젝트 이름(공격 클립 경로 …/RightHand/EagleArrow 의 마지막 이름).</summary>
        public const string ArrowChildName = "EagleArrow";

        // ── 공격 클립 0초 키 값(= 클립 전체 구간 고정값) ──────────────────────
        //   출처: Assets/_Project/Animations/Units/EagleArcher/EagleArcher_Attack.anim
        //     m_PositionCurves …/EagleArrow  time 0 value {x: -0.063, y: -0.222, z: -0.051}
        //     m_EulerCurves    …/EagleArrow  time 0 value {x: 12.076, y: 93.234, z: 84.23}, m_RotationOrder 4(ZXY)
        //   Unity의 Quaternion.Euler 도 Z → X → Y 순서(ZXY)라 클립과 같은 회전이 된다.
        //   프리팹 기본값을 이 값에 맞췄더라도(수정안 A) 진단은 프리팹이 아니라 "클립 값"을 기준으로 잰다 —
        //   그래야 수정 전/후 어느 쪽에서 돌려도 같은 잣대로 비교된다.
        private static readonly Vector3 ClipLocalPosition = new Vector3(-0.063f, -0.222f, -0.051f);
        private static readonly Quaternion ClipLocalRotation = Quaternion.Euler(12.076f, 93.234f, 84.23f);

        // ── 판정 문턱값 ─────────────────────────────────────────────────────
        // 제자리 이탈: 월드 5cm 또는 10도 초과. (손 크기 대비 눈에 띄는 수준을 잡되 부동소수 오차는 넘지 않게)
        private const float OffPoseWorldMeters = 0.05f;
        private const float OffPoseDegrees = 10f;
        // 한 프레임 급이동: 손 안에서 월드 5cm 또는 10도 초과(클립 값이 상수라 정상이면 0에 가깝다).
        private const float StepWorldMeters = 0.05f;
        private const float StepDegrees = 10f;
        // 경기당 [EAGLE-ARROW-EVENT] 상세 줄 상한(넘는 사건은 개수만 센다 — LogRules 1.14 금지 8 스팸 방지).
        private const int EventLimit = 24;

        // 컨트롤러(EagleArcher.controller)의 상태 이름 — 로그에 해시 대신 이름으로 찍기 위해서만 쓴다.
        private static readonly int StateAttack = Animator.StringToHash("Attack");
        private static readonly int StateWalk = Animator.StringToHash("Walk");

        // ── 경기 단위 집계(모든 EagleArcher 화살이 함께 쓴다) ────────────────
        private static IDisposable _endSubscription;
        private static bool _ended;
        private static int _probes, _attachMissing;
        private static long _samples, _samplesInTransition, _offPoseFrames, _offPoseInTransition;
        private static int _activations, _offPoseEpisodes, _stepEvents, _eventsTotal, _eventsLogged;
        private static float _maxOffset, _maxAngle, _maxStep, _maxStepAngle, _maxActivationOffset, _maxActivationAngle;

        // ── 화살 1개 상태 ───────────────────────────────────────────────────
        private UnitData _unit;
        private Animator _animator;
        private Transform _hand;          // 화살의 부모(RightHand) — 손 안 좌표를 월드 미터로 바꿀 때 쓴다
        private bool _hasPrevious;        // 이번에 켜진 뒤 한 번이라도 쟀는가(false면 다음 표본이 "켜진 첫 프레임")
        private Vector3 _previousLocalPosition;
        private Quaternion _previousLocalRotation;
        private bool _offEpisode;         // 지금 제자리 이탈 구간 안인가(구간 시작 때만 사건 1건)

        /// <summary>
        /// 유닛 루트 아래에서 손 화살을 찾아 이 진단을 붙인다(이미 있으면 재사용). EagleArcher가 아니면 아무것도 안 한다.
        /// </summary>
        /// <param name="unitRoot">유닛 루트 Transform(UnitView가 붙은 오브젝트).</param>
        /// <param name="unit">유닛 데이터(Id·팀·종류 기록용).</param>
        /// <param name="animator">유닛 Animator(전환 상태 기록용 — null이어도 동작).</param>
        public static void AttachTo(Transform unitRoot, UnitData unit, Animator animator)
        {
            if (unitRoot == null || unit == null || unit.Type != UnitType.EagleArcher) return;
            EnsureMatchOpen();
            Transform arrow = FindArrow(unitRoot);
            if (arrow == null || arrow.parent == null) { _attachMissing++; return; }
            var probe = arrow.GetComponent<EagleArrowPoseProbe>();
            if (probe == null) probe = arrow.gameObject.AddComponent<EagleArrowPoseProbe>();
            probe.Bind(unit, animator, arrow.parent);
            _probes++;
        }

        // 꺼진 자식까지 포함해 이름이 정확히 같은 Transform을 찾는다(Initialize 때 1회 — 할당은 개발 빌드에서만).
        private static Transform FindArrow(Transform root)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == ArrowChildName) return all[i];
            return null;
        }

        private void Bind(UnitData unit, Animator animator, Transform hand)
        {
            _unit = unit;
            _animator = animator;
            _hand = hand;
            _hasPrevious = false;
            _offEpisode = false;
        }

        // 화살이 켜질 때마다(Animator가 m_IsActive=1을 쓸 때) "켜진 첫 프레임" 상태로 되돌린다.
        private void OnEnable()
        {
            _hasPrevious = false;
            _offEpisode = false;
        }

        // Animator는 Update 뒤·LateUpdate 앞에 자세를 쓴다 → LateUpdate에서 읽으면 이번 프레임에 화면에 그려질 값이다.
        private void LateUpdate()
        {
            if (_ended || _unit == null || _hand == null) return;
            Sample();
        }

        private void Sample()
        {
            Vector3 local = transform.localPosition;
            Quaternion rotation = transform.localRotation;
            float offset = _hand.TransformVector(local - ClipLocalPosition).magnitude;
            float angle = Quaternion.Angle(rotation, ClipLocalRotation);
            bool activation = !_hasPrevious;
            float step = activation ? 0f : _hand.TransformVector(local - _previousLocalPosition).magnitude;
            float stepAngle = activation ? 0f : Quaternion.Angle(rotation, _previousLocalRotation);
            _previousLocalPosition = local;
            _previousLocalRotation = rotation;
            _hasPrevious = true;

            bool inTransition = AnimatorReady() && _animator.IsInTransition(0);
            Accumulate(offset, angle, step, stepAngle, activation, inTransition);

            bool off = offset > OffPoseWorldMeters || angle > OffPoseDegrees;
            if (off && !_offEpisode) { _offPoseEpisodes++; Event("offPose", offset, angle, step, stepAngle, activation, inTransition, local); }
            _offEpisode = off;
            if (step > StepWorldMeters || stepAngle > StepDegrees)
            {
                _stepEvents++;
                Event("step", offset, angle, step, stepAngle, activation, inTransition, local);
            }
        }

        private static void Accumulate(float offset, float angle, float step, float stepAngle, bool activation, bool inTransition)
        {
            _samples++;
            if (inTransition) _samplesInTransition++;
            if (offset > OffPoseWorldMeters || angle > OffPoseDegrees)
            {
                _offPoseFrames++;
                if (inTransition) _offPoseInTransition++;
            }
            if (activation)
            {
                _activations++;
                _maxActivationOffset = Mathf.Max(_maxActivationOffset, offset);
                _maxActivationAngle = Mathf.Max(_maxActivationAngle, angle);
            }
            _maxOffset = Mathf.Max(_maxOffset, offset);
            _maxAngle = Mathf.Max(_maxAngle, angle);
            _maxStep = Mathf.Max(_maxStep, step);
            _maxStepAngle = Mathf.Max(_maxStepAngle, stepAngle);
        }

        private bool AnimatorReady() => _animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null;

        // 사건 1건 — 경기당 처음 EventLimit건만 줄로 남기고 나머지는 개수만 센다.
        private void Event(string kind, float offset, float angle, float step, float stepAngle,
                           bool activation, bool inTransition, Vector3 local)
        {
            _eventsTotal++;
            if (_eventsLogged >= EventLimit) return;
            _eventsLogged++;
            GameLog.Dev.Info("Combat", nameof(EagleArrowPoseProbe), "[EAGLE-ARROW-EVENT]",
                FormattableString.Invariant(
                    $"side=view v=1 role={ServerProjectileDiagnostics.RoleName} n={_eventsLogged} kind={kind} unit={_unit.Id} team={_unit.Team} alive={(_unit.IsAlive ? 1 : 0)} frame={Time.frameCount} t={Time.timeAsDouble:F3} offset={offset:F3} angle={angle:F1} step={step:F3} stepAngle={stepAngle:F1} activation={(activation ? 1 : 0)} inTransition={(inTransition ? 1 : 0)} ")
                + DescribeAnimator()
                + FormattableString.Invariant($" local={local.x:F3}|{local.y:F3}|{local.z:F3}"));
        }

        // 현재/다음 상태 이름 · 정규화 시간 · 전환 진행도. 사건 줄에서만 부른다(상한 안에서만 문자열 생성).
        private string DescribeAnimator()
        {
            if (!AnimatorReady()) return "cur=none";
            AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(0);
            string text = FormattableString.Invariant(
                $"cur={StateName(current.shortNameHash)} curT={current.normalizedTime:F3} speed={_animator.speed:F2}");
            if (!_animator.IsInTransition(0)) return text + " next=none";
            AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(0);
            AnimatorTransitionInfo transition = _animator.GetAnimatorTransitionInfo(0);
            return text + FormattableString.Invariant(
                $" next={StateName(next.shortNameHash)} nextT={next.normalizedTime:F3} transT={transition.normalizedTime:F3}");
        }

        private static string StateName(int hash)
            => hash == StateAttack ? "Attack" : hash == StateWalk ? "Walk" : "0x" + hash.ToString("x8");

        // ── 경기 경계 ────────────────────────────────────────────────────────
        // 첫 화살이 붙을 때 경기 종료 구독을 연다. 종료 때 요약 1줄을 남기고 집계를 비운 뒤 구독을 닫는다
        // (재경기에서 새 화살이 붙으면 다시 열린다 — 이전 경기 값이 섞이지 않는다).
        private static void EnsureMatchOpen()
        {
            if (_endSubscription != null) return;
            ResetCounters();
            _endSubscription = GameEvents.OnGameEnd.Subscribe(_ => { OnGameEnd(); });
        }

        private static void OnGameEnd()
        {
            try { WriteSummary("game-end"); }
            finally
            {
                _ended = true;   // 종료 화면 뒤에 남은 화살 표본이 다음 경기 집계에 섞이지 않게 막는다
                _endSubscription?.Dispose();
                _endSubscription = null;
            }
        }

        private static void ResetCounters()
        {
            _ended = false;
            _probes = _attachMissing = 0;
            _samples = _samplesInTransition = _offPoseFrames = _offPoseInTransition = 0;
            _activations = _offPoseEpisodes = _stepEvents = _eventsTotal = _eventsLogged = 0;
            _maxOffset = _maxAngle = _maxStep = _maxStepAngle = _maxActivationOffset = _maxActivationAngle = 0f;
        }

        private static void WriteSummary(string reason)
        {
            string verdict = _samples == 0 ? "NO-SAMPLES"
                : (_offPoseEpisodes > 0 || _stepEvents > 0) ? "JUMP" : "PASS";
            GameLog.Dev.Info("Combat", nameof(EagleArrowPoseProbe), "[EAGLE-ARROW]",
                FormattableString.Invariant(
                    $"side=view v=1 role={ServerProjectileDiagnostics.RoleName} reason={reason} probes={_probes} attachMissing={_attachMissing} samplesActive={_samples} samplesInTransition={_samplesInTransition} activations={_activations} offPoseFrames={_offPoseFrames} offPoseInTransition={_offPoseInTransition} offPoseEpisodes={_offPoseEpisodes} stepEvents={_stepEvents} maxOffset={_maxOffset:F3} maxAngle={_maxAngle:F1} maxStep={_maxStep:F3} maxStepAngle={_maxStepAngle:F1} maxActivationOffset={_maxActivationOffset:F3} maxActivationAngle={_maxActivationAngle:F1} events={_eventsLogged}/{_eventsTotal} threshold={OffPoseWorldMeters:F2}m|{OffPoseDegrees:F0}deg verdict={verdict}"));
        }
    }
}

#endif
