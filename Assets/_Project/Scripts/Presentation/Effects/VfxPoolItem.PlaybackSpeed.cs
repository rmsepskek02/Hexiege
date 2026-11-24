// ============================================================================
// VfxPoolItem.PlaybackSpeed.cs
// 풀 항목 하나의 "재생 속도" 바꾸기 · 되돌리기, 그리고 반복 재생(상태 루프)의 안전 마감 시각.
// 2026-11-18 지원 스킬 2-D 메테오 Task(_Tasks/2026-11-18/03_34_support-skill-meteor, Plan P-4 · P-15 · U-10 · R-6 · R-7 · O-7).
//
// ① 재생 속도(초급자용 설명):
//   메테오 예고 이펙트(바닥에 그려지는 경고 원)는 「1.5초 뒤 운석이 떨어진다」에 맞춰 만들어진다. 그런데 스킬 데이터의
//   착탄 지연을 0.75초로 줄이면 예고가 절반쯤 진행됐을 때 운석이 떨어져 어색하다. 그래서 예고만 「기준 시간 ÷ 착탄 지연」배
//   빠르게(또는 느리게) 재생한다 — 파티클 시스템의 Simulation Speed(main.simulationSpeed)에 배율을 곱한다.
//   🔴 풀 항목은 재사용된다. 바꾼 속도를 되돌리지 않으면 다음에 같은 프리팹을 꺼낸 재생(다른 스킬일 수도 있다)이 빠르게 돈다(R-7).
//      그래서 원래 속도를 처음 한 번 기억해 두고, 새 재생 시작(Play) · 풀 반환(Return)이 모두 거치는 ResetTravelState에서
//      되돌린다(RestorePlaybackSpeed). 속도를 바꾼 적이 없으면 아무것도 하지 않는다(다른 이펙트는 결과가 같다).
//   쓰는 순서: Play(...) "뒤에" SetPlaybackSpeed를 부른다(Play가 시작할 때 이전 재생의 속도를 먼저 되돌리기 때문이다).
//
// ② 상태 루프 안전 마감(초급자용 설명):
//   메테오의 떨어지는 몸체는 반복(Looping) 프리팹이라, 누가 멈추지 않으면 영영 풀에 돌아오지 않는다(R-6 · VfxPoolItem.cs Update의
//   AnyAlive 검사가 끝나지 않는다). 정상 경로에서는 화면 구독자(SkillActivationVfxPresenter)가 착탄 시각에 꼬리 정지한다.
//   그런데 구독자가 그 일을 못 하는 경우(파괴 · 비활성 등)를 대비해 항목 스스로 「마감 시각」(착탄 지연 + 0.5초 — O-7)을 갖고,
//   그 시각까지 아직 돌고 있으면 스스로 꼬리 정지하고 진단 1줄(Reason=Timeout)을 남긴다. 정상 경로에서는 0건이어야 한다(T2-L10).
//   확인 번호(lease)가 맞을 때만 마감을 건다 — 풀로 돌아가 다른 재생에 쓰이는 항목을 잘못 멈추지 않게(StatusLoop.cs와 같은 원칙).
//
// 화면 표현 전용이다. 피해 · 화상 판정과 무관하다.
// Presentation 레이어 — Unity 의존 (MonoBehaviour, ParticleSystem).
// ============================================================================

using UnityEngine;

namespace Hexiege.Presentation
{
    public partial class VfxPoolItem
    {
        // ── ① 재생 속도 ──
        // 이 항목의 모든 파티클 시스템(중첩 포함)과 그 원래 재생 속도(프리팹에서 처음 꺼낸 값). 처음 SetPlaybackSpeed 때 한 번만 모은다.
        private ParticleSystem[] _speedSystems;
        private float[] _speedOriginal;
        // 지금 재생 속도를 바꿔 둔 상태인가(되돌릴 것이 있는가).
        private bool _speedChanged;
        // 지금 적용된 배율(진단 · 검증용 — 바꾸지 않았으면 1).
        private float _speedMultiplier = 1f;

        /// <summary> 지금 적용된 재생 속도 배율(바꾸지 않았으면 1). 진단 로그 · 자가 검증이 읽는다. </summary>
        public float PlaybackSpeedMultiplier => _speedMultiplier;

        /// <summary>
        /// 방금 Play()로 시작한 재생의 모든 파티클 재생 속도를 「원래 속도 × multiplier」로 바꾼다.
        /// 반드시 Play() 뒤에 부른다. 배율이 1이면 아무것도 바꾸지 않는다(원래 속도 그대로).
        /// 바꾼 속도는 다음 Play · 풀 반환 때 원래 값으로 되돌아간다(RestorePlaybackSpeed).
        /// </summary>
        /// <param name="multiplier">재생 속도 배율. 0 이하 · NaN · 무한대면 1로 본다(바꾸지 않는다).</param>
        /// <returns>실제로 속도를 바꿨으면 true(배율 1 · 재생 중 아님 · 파티클 없음이면 false).</returns>
        public bool SetPlaybackSpeed(float multiplier)
        {
            if (!_active) return false;
            if (!(multiplier > 0f) || float.IsInfinity(multiplier) || Mathf.Approximately(multiplier, 1f)) return false;

            // 처음 한 번만 모은다 — 이때는 아직 속도를 바꾼 적이 없으므로 읽은 값이 곧 프리팹 원래 값이다.
            //   (그 뒤로는 바꿀 때마다 반드시 되돌리므로 원래 값이 오염되지 않는다.)
            if (_speedSystems == null)
            {
                _speedSystems = GetComponentsInChildren<ParticleSystem>(true);
                _speedOriginal = new float[_speedSystems.Length];
                for (int i = 0; i < _speedSystems.Length; i++)
                    _speedOriginal[i] = _speedSystems[i] != null ? _speedSystems[i].main.simulationSpeed : 1f;
            }
            if (_speedSystems.Length == 0) return false;

            for (int i = 0; i < _speedSystems.Length; i++)
            {
                if (_speedSystems[i] == null) continue;
                ParticleSystem.MainModule main = _speedSystems[i].main; // MainModule은 값 복사본이지만 대입하면 실제 시스템에 반영된다(Unity 관용).
                main.simulationSpeed = _speedOriginal[i] * multiplier;
            }
            _speedChanged = true;
            _speedMultiplier = multiplier;
            return true;
        }

        /// <summary>
        /// 바꿔 둔 재생 속도를 원래 값으로 되돌린다. ResetTravelState(새 재생 시작 · 풀 반환이 모두 거친다)에서 부른다.
        /// 바꾼 적이 없으면 아무것도 하지 않는다 — 속도를 바꾸지 않는 다른 이펙트는 결과가 같다.
        /// </summary>
        private void RestorePlaybackSpeed()
        {
            if (!_speedChanged) return;
            _speedChanged = false;
            _speedMultiplier = 1f;
            if (_speedSystems == null) return;
            for (int i = 0; i < _speedSystems.Length; i++)
            {
                if (_speedSystems[i] == null) continue;
                ParticleSystem.MainModule main = _speedSystems[i].main;
                main.simulationSpeed = _speedOriginal[i];
            }
        }

        /// <summary>
        /// (자가 검증용 읽기) 모든 파티클 시스템의 재생 속도가 원래 값 × expectedMultiplier인지 본다.
        /// 아직 속도를 바꾼 적이 없어 원래 값을 모르면, 지금 값을 원래 값으로 보고 배율 1일 때만 true.
        /// </summary>
        /// <param name="expectedMultiplier">기대 배율(원래 값이면 1).</param>
        /// <returns>모두 기대값과 같으면 true.</returns>
        public bool PlaybackSpeedEquals(float expectedMultiplier)
        {
            if (_speedSystems == null) return Mathf.Approximately(expectedMultiplier, 1f);
            for (int i = 0; i < _speedSystems.Length; i++)
            {
                if (_speedSystems[i] == null) continue;
                if (!Mathf.Approximately(_speedSystems[i].main.simulationSpeed, _speedOriginal[i] * expectedMultiplier)) return false;
            }
            return true;
        }

        // ── ② 상태 루프 안전 마감 ──
        // 마감 시각(Time.time 기준). 음수면 마감 없음. 재생별 상태라 ResetStatusLoop에서 비운다.
        private float _statusLoopDeadline = -1f;
        // 마감을 건 확인 번호(이 번호의 상태 루프가 아직 켜져 있을 때만 마감이 동작한다).
        private ulong _statusLoopDeadlineLease;
        // 진단 줄에 쓸 꼬리표(무엇의 마감인가 — 예: 메테오 몸체의 발동 순번). 재생별 상태.
        private string _statusLoopDeadlineTag;

        /// <summary>
        /// 방금 시작한 상태 루프에 안전 마감 시각을 건다(지금부터 seconds초 뒤). 그 시각에 아직 돌고 있으면 스스로 꼬리 정지한다.
        /// 확인 번호가 맞지 않거나 seconds가 0 이하 · NaN이면 아무것도 하지 않는다.
        /// </summary>
        /// <param name="lease">BeginStatusLoop가 돌려준 확인 번호.</param>
        /// <param name="seconds">지금부터 마감까지의 시간(초).</param>
        /// <param name="tag">진단 줄 꼬리표(예: "Seq=3"). null 허용.</param>
        /// <returns>마감을 걸었으면 true.</returns>
        public bool SetStatusLoopDeadline(ulong lease, float seconds, string tag)
        {
            if (!IsStatusLoopActive(lease) || !(seconds > 0f) || float.IsInfinity(seconds)) return false;
            _statusLoopDeadline = Time.time + seconds;
            _statusLoopDeadlineLease = lease;
            _statusLoopDeadlineTag = tag;
            return true;
        }

        /// <summary>
        /// (Update에서 매 프레임) 마감 시각이 지났는데 그 상태 루프가 아직 켜져 있으면 꼬리 정지하고 진단 1줄을 남긴다.
        /// 마감이 없거나 이미 정지했으면 아무것도 하지 않는다. 이번 프레임의 나머지 Update 처리는 그대로 이어진다.
        /// </summary>
        private void PollStatusLoopDeadline()
        {
            if (_statusLoopDeadline < 0f) return;
            if (!IsStatusLoopActive(_statusLoopDeadlineLease)) { _statusLoopDeadline = -1f; return; }
            if (Time.time < _statusLoopDeadline) return;

            ulong lease = _statusLoopDeadlineLease;
            string tag = _statusLoopDeadlineTag;
            float overdue = Time.time - _statusLoopDeadline;
            _statusLoopDeadline = -1f;
            bool stopped = StopStatusLoop(lease, false); // 꼬리 정지 — 남은 입자가 사라지면 Update가 스스로 풀에 반환한다.
            LogStatusLoopTimeout(tag, lease, stopped, overdue);
        }

        /// <summary> 안전 마감 표시를 비운다(ResetStatusLoop에서 — 새 재생 · 풀 반환 때). </summary>
        private void ResetStatusLoopDeadline()
        {
            _statusLoopDeadline = -1f;
            _statusLoopDeadlineLease = 0;
            _statusLoopDeadlineTag = null;
        }

        // [진단 T2-L10] 반복 몸체가 안전 마감으로 멈췄다 — 정상 경로에서는 0건(화면 구독자가 착탄 시각에 먼저 멈춘다).
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogStatusLoopTimeout(string tag, ulong lease, bool stopped, float overdue)
        {
            string role = !Hexiege.Application.NetworkContext.IsNetworkActive ? "Single"
                : Hexiege.Application.NetworkContext.IsNetworkServer ? "Host" : "Client";
            Hexiege.Application.GameLog.Dev.Warn("Skill", nameof(VfxPoolItem), "[SKILL-METEOR] MeteorBodyStopped",
                System.FormattableString.Invariant(
                    $"Diag=SkillMeteor, DiagRev=1, Event=MeteorBodyStopped, Role={role}, Reason=Timeout, Source=PoolItem, {tag ?? "Tag=None"}, Lease={lease}, Stopped={stopped}, OverdueMs={Mathf.RoundToInt(overdue * 1000f)}, Prefab={(_sourcePrefab != null ? _sourcePrefab.name : "None")}"));
        }
    }
}
