// ============================================================================
// InGameLinkProbeSchedule.cs
// 인게임 「내 인터넷 확인」을 언제 몇 번 할지 정하는 순수 C# 일정표 (2026-11-26 2차 수정 신설 — 사양 ①).
//
// [왜 생겼나 — 초급 개발자용 설명]
//   1차는 상대 신호가 15초 끊긴 「판정 시점」에만 내 인터넷을 한 번 확인했다.
//   1차 실기(로그 2026-11-26_23_52, (가))에서 이런 일이 있었다:
//     실기기 와이파이를 껐다 켬 → 게임 연결은 끝내 돌아오지 않음 → 그런데 15초 판정 시점에는 실기기 인터넷이 이미 돌아와 있음
//     → 실기기도 「내 인터넷 됨 = 상대가 나갔다 = 내 승리」, 에디터도 「내 승리」 → **양쪽 모두 승리**.
//   그래서 2차부터는 끊긴 동안 여러 번 확인하고, **한 번이라도 실패했으면 「내가 끊긴 것(SelfLeft)」** 으로 본다.
//   판정 시점의 확인이 성공해도 마찬가지다(사용자 확정 2026-11-26).
//
// [확인 일정 — 한 「끊김 구간(episode)」 안에서]
//   ① 지연 진입 직후 1회(Entry)
//   ② 이후 「직전 확인이 끝난 뒤」 ProbeIntervalSec 마다 1회씩(Periodic) — 🔴 동시에 두 개 돌지 않는다(순차)
//   ③ 판정 시점(15초)에 1회(Judge) — 그 결과를 받은 뒤에 결론을 낸다(판정 시점 결과를 기다리는 구조 유지)
//      · 판정 시점에 다른 확인이 아직 돌고 있으면 그것이 끝나기를 기다린 뒤 Judge 를 시작한다(순차 규칙).
//   끊김 구간이 끝나면(상대 신호가 돌아옴) 실패 기록도 함께 지운다 — 다음 끊김은 새로 센다.
//   구간이 끝났는데 아직 돌고 있던 확인의 결과는 「지난 구간 것(Stale)」으로 로그만 남기고 세지 않는다.
//
// [끊김 구간이 짧게 여러 번 반복될 때 — 요청 제한 보호]
//   신호 간격이 0.5초로 줄어 짧은 끊김이 자주 생길 수 있다. 그때마다 곧바로 확인 요청을 보내면 Cloud Save 요청 제한에
//   걸릴 수 있고, 요청 제한 오류는 확인기에서 「Unreachable(실패)」로 나온다 → 멀쩡한 기기가 「내 이탈」로 판정될 위험.
//   그래서 구간이 바뀌어도 「직전 확인이 끝난 뒤 최소 ProbeMinGapSec」은 쉬고 다음 Entry 를 시작한다(Judge 는 예외 — 반드시 한다).
//   ⚠️ Cloud Save 의 실제 요청 제한 수치는 이 프로젝트에서 확인한 적이 없다(미확인 — 보고 항목).
//
// [이 클래스가 하지 않는 것]
//   실제 요청(InternetReachabilityProbe — Infrastructure)은 보내지 않는다. 「지금 시작해야 하나」 · 「결과를 어떻게 셀까」만
//   정한다. 시간은 밖에서 넣는다(InGameLinkMonitor 와 같은 방식) — Unity 없이 숫자만으로 시험할 수 있다.
//
// task: Assets/_Project/Docs/_Tasks/2026-11-26/10_31_ingame-leave-judgment/Plan.md 「2차 수정」 절 ①
// Application 레이어 — 순수 C#. UnityEngine · Unity.Netcode 참조 없음.
// ============================================================================

namespace Hexiege.Application
{
    /// <summary>[2026-11-26 2차] 인터넷 확인의 종류(진단 로그 Kind 값).</summary>
    public enum InGameLinkProbeKind
    {
        /// <summary>확인 없음.</summary>
        None = 0,

        /// <summary>끊김 구간에 들어간 직후의 첫 확인.</summary>
        Entry = 1,

        /// <summary>끊김 구간 중 일정 간격으로 하는 확인.</summary>
        Periodic = 2,

        /// <summary>판정 시점(15초 · 끊김 통보)의 확인. 이 결과를 받은 뒤 결론을 낸다.</summary>
        Judge = 3
    }

    /// <summary>[2026-11-26 2차] 확인 하나가 끝났을 때의 기록(로그용).</summary>
    public readonly struct InGameLinkProbeOutcome
    {
        /// <summary>이 구간 안에서 몇 번째 확인인가(1부터). 지난 구간 확인이면 그 구간에서의 번호.</summary>
        public readonly int Seq;

        /// <summary>확인 종류.</summary>
        public readonly InGameLinkProbeKind Kind;

        /// <summary>이미 끝난 지난 끊김 구간의 확인이었는가(true 면 판정에 세지 않는다).</summary>
        public readonly bool Stale;

        /// <summary>확인 시작부터 끝까지 걸린 시간(초).</summary>
        public readonly double DurationSec;

        /// <summary>확인을 시작했던 끊김 구간 번호(경기 안에서 1부터).</summary>
        public readonly int EpisodeNo;

        /// <summary>기록을 만든다.</summary>
        /// <param name="seq">구간 안 확인 번호.</param>
        /// <param name="kind">확인 종류.</param>
        /// <param name="stale">지난 구간 확인인가.</param>
        /// <param name="durationSec">걸린 시간(초).</param>
        /// <param name="episodeNo">확인을 시작한 구간 번호.</param>
        public InGameLinkProbeOutcome(int seq, InGameLinkProbeKind kind, bool stale, double durationSec, int episodeNo)
        {
            Seq = seq;
            Kind = kind;
            Stale = stale;
            DurationSec = durationSec;
            EpisodeNo = episodeNo;
        }
    }

    /// <summary>
    /// [2026-11-26 2차] 끊김 구간 동안의 인터넷 확인 일정 · 결과 집계. 시간은 밖에서 넣는다(초, 같은 시계).
    /// Host · Client 가 같은 코드로 같은 일정을 쓴다(최상위 원칙).
    /// </summary>
    public sealed class InGameLinkProbeSchedule
    {
        // ====================================================================
        // 값 (모두 임시값 — 테스트 후 조정)
        // ====================================================================

        /// <summary>같은 끊김 구간 안에서 「직전 확인이 끝난 뒤」 다음 확인까지의 간격(초). 사양 ① 「예: 4~5초」.</summary>
        public const double ProbeIntervalSec = 4.0;

        /// <summary>
        /// 구간이 바뀌어도 「직전 확인이 끝난 뒤」 다음 Entry 확인까지 최소 이만큼(초) 쉰다(요청 제한 보호 — 머리말).
        /// Judge 확인에는 적용하지 않는다(판정 시점 확인은 반드시 한다).
        /// </summary>
        public const double ProbeMinGapSec = 1.0;

        // ====================================================================
        // 상태
        // ====================================================================

        private double _lastCompletedAt = double.NegativeInfinity; // 직전 확인이 끝난 시각(구간 무관)
        private double _nextProbeAt;                                // 이 구간에서 다음 Entry/Periodic 을 시작해도 되는 시각
        private int _inFlightSeq;                                   // 돌고 있는 확인의 구간 안 번호
        private int _inFlightEpisodeNo;                             // 돌고 있는 확인을 시작한 구간 번호
        private double _inFlightStartedAt;                          // 돌고 있는 확인을 시작한 시각

        /// <summary>지금 끊김 구간 안인가.</summary>
        public bool EpisodeActive { get; private set; }

        /// <summary>경기 안 끊김 구간 번호(1부터). 구간이 없었으면 0.</summary>
        public int EpisodeNo { get; private set; }

        /// <summary>
        /// [2026-11-26 2차 보완] 지금(또는 마지막) 끊김 구간이 시작된 시각(초).
        /// 「구간 시작 뒤 몇 초째 확인 결과가 없나」를 재는 데 쓴다(InGameLinkBlockPolicy 의 ProbeTimeout 규칙).
        /// </summary>
        public double EpisodeStartedAt { get; private set; }

        /// <summary>지금 확인이 돌고 있는가(동시에 하나만).</summary>
        public bool InFlight { get; private set; }

        /// <summary>돌고 있는 확인의 종류.</summary>
        public InGameLinkProbeKind InFlightKind { get; private set; } = InGameLinkProbeKind.None;

        /// <summary>이 구간에서 시작한 확인 수.</summary>
        public int StartedCount { get; private set; }

        /// <summary>이 구간에서 끝난(세는) 확인 수.</summary>
        public int CompletedCount { get; private set; }

        /// <summary>이 구간에서 실패한 확인 수.</summary>
        public int FailedCount { get; private set; }

        /// <summary>🔴 이 구간에서 확인이 한 번이라도 실패했는가 — true 면 판정은 「내 이탈(SelfLeft)」.</summary>
        public bool AnyFailed => FailedCount > 0;

        /// <summary>이 구간에서 판정 시점 확인을 시작했는가.</summary>
        public bool JudgeProbeStarted { get; private set; }

        /// <summary>이 구간에서 판정 시점 확인이 끝났는가 — true 면 결론을 낼 수 있다.</summary>
        public bool JudgeProbeCompleted { get; private set; }

        /// <summary>경기 전체에서 시작한 확인 수(요약 로그용).</summary>
        public int TotalStarted { get; private set; }

        /// <summary>경기 전체에서 실패한 확인 수(요약 로그용 — 지난 구간 확인 포함).</summary>
        public int TotalFailed { get; private set; }

        /// <summary>판정 결론을 낼 수 있는가(판정 시점 확인 결과가 나왔다).</summary>
        public bool VerdictReady => EpisodeActive && JudgeProbeCompleted;

        // ====================================================================
        // 구간
        // ====================================================================

        /// <summary>
        /// 끊김 구간을 시작한다(지연 진입 · 판정 진입). 이미 구간 안이면 아무것도 하지 않는다.
        /// </summary>
        /// <param name="now">현재 시각(초).</param>
        /// <returns>새 구간을 시작했으면 true.</returns>
        public bool BeginEpisode(double now)
        {
            if (EpisodeActive) return false;
            EpisodeActive = true;
            EpisodeNo++;
            EpisodeStartedAt = now; // [2026-11-26 2차 보완] ProbeTimeout 규칙용 구간 시작 시각
            StartedCount = 0;
            CompletedCount = 0;
            FailedCount = 0;
            JudgeProbeStarted = false;
            JudgeProbeCompleted = false;
            // 「직후 1회」 — 단, 직전 확인이 막 끝났다면 최소 간격만큼은 쉰다(요청 제한 보호).
            double earliest = _lastCompletedAt + ProbeMinGapSec;
            _nextProbeAt = now > earliest ? now : earliest;
            return true;
        }

        /// <summary>
        /// 끊김 구간을 끝낸다(상대 신호가 돌아와 지연이 풀림). 실패 기록도 함께 지운다.
        /// 🔴 판정 중에는 부르지 않는다(판정은 뒤집지 않는다 — 감시기가 지킨다).
        /// </summary>
        public void EndEpisode()
        {
            EpisodeActive = false;
        }

        // ====================================================================
        // 확인 시작 / 끝
        // ====================================================================

        /// <summary>
        /// 지금 확인을 시작해야 하는가. 구간 안 · 돌고 있는 확인 없음 · (판정 중이면 Judge 미시작, 아니면 시각 도달).
        /// </summary>
        /// <param name="now">현재 시각(초).</param>
        /// <param name="judging">판정 중인가.</param>
        /// <returns>시작해야 하면 true.</returns>
        public bool ShouldStart(double now, bool judging)
        {
            if (!EpisodeActive || InFlight) return false;
            if (judging) return !JudgeProbeStarted;
            return now >= _nextProbeAt;
        }

        /// <summary>확인을 시작했다고 적는다.</summary>
        /// <param name="now">현재 시각(초).</param>
        /// <param name="judging">판정 중인가(true 면 Judge 확인).</param>
        /// <returns>구간 안 확인 번호(1부터).</returns>
        public int Start(double now, bool judging)
        {
            InGameLinkProbeKind kind = judging ? InGameLinkProbeKind.Judge
                : (StartedCount == 0 ? InGameLinkProbeKind.Entry : InGameLinkProbeKind.Periodic);
            StartedCount++;
            TotalStarted++;
            if (judging) JudgeProbeStarted = true;
            InFlight = true;
            InFlightKind = kind;
            _inFlightSeq = StartedCount;
            _inFlightEpisodeNo = EpisodeNo;
            _inFlightStartedAt = now;
            return StartedCount;
        }

        /// <summary>
        /// 확인이 끝났다. 지금 구간의 확인이면 성공 · 실패를 세고, 지난 구간 확인이면 세지 않는다(Stale).
        /// </summary>
        /// <param name="now">현재 시각(초).</param>
        /// <param name="success">Reachable 이었는가(Unreachable · Skipped · 예외는 모두 false).</param>
        /// <returns>로그용 기록.</returns>
        public InGameLinkProbeOutcome Complete(double now, bool success)
        {
            InGameLinkProbeKind kind = InFlightKind;
            int seq = _inFlightSeq;
            int episodeNo = _inFlightEpisodeNo;
            bool stale = !EpisodeActive || episodeNo != EpisodeNo;

            InFlight = false;
            InFlightKind = InGameLinkProbeKind.None;
            _lastCompletedAt = now;
            if (!success) TotalFailed++;

            if (!stale)
            {
                CompletedCount++;
                if (!success) FailedCount++;
                if (kind == InGameLinkProbeKind.Judge) JudgeProbeCompleted = true;
                _nextProbeAt = now + ProbeIntervalSec;          // 순차 — 끝난 뒤부터 간격을 센다
            }
            else if (EpisodeActive)
            {
                // 지난 구간 확인이 끝나기를 기다리던 새 구간 — 최소 간격 뒤에 Entry 를 시작한다.
                double earliest = now + ProbeMinGapSec;
                if (_nextProbeAt < earliest) _nextProbeAt = earliest;
            }

            return new InGameLinkProbeOutcome(seq, kind, stale, now - _inFlightStartedAt, episodeNo);
        }
    }

    /// <summary>
    /// [2026-11-26 2차 — 사양 ②] 「문제가 생긴 쪽만 막고 피해 쪽은 막지 않는다」 — 화면을 막는 방식을 고르는 순수 규칙.
    ///
    /// <para>
    /// [초급자용 설명 — 규칙 표 (위에서부터 먼저 맞는 것)]
    /// <list type="number">
    ///   <item>지연 · 판정 중이 아니다 → None(평소 화면)</item>
    ///   <item>「연결 복구 중」(내 앱이 잠깐 멈췄다 돌아옴, 판정 전) → Overlay / Recovering (1차 그대로)</item>
    ///   <item>RTT 만 느린 지연(신호는 옴, 판정 전) → Banner / Rtt (지연이 크다는 이유로 조작을 막지 않는다 — 사양 ③)</item>
    ///   <item>이번 끊김 동안 내 인터넷 확인이 한 번이라도 실패 → Overlay / SelfProbeFailed (내 쪽 문제)</item>
    ///   <item>내가 Client 다(Host 신호가 끊김 — 양쪽 게임이 멈춤, 악용 대상 아님) → Overlay / HostLost</item>
    ///   <item>[2026-11-26 2차 보완] 내가 Host 이고 구간 시작 뒤 <see cref="PendingProbeBannerMaxSec"/>(2.0초)가 지났는데
    ///         아직 확인 결과가 하나도 없다 → Overlay / ProbeTimeout (「상대 쪽 문제」라는 확인 전에는 내 쪽 문제로 본다)</item>
    ///   <item>내가 Host 이고 아직 확인 결과가 없다(구간 시작 뒤 2.0초 미만) → Banner / PendingProbe (실패하면 4번으로 바뀐다)</item>
    ///   <item>내가 Host 이고 확인이 모두 성공 → Banner / PeerSide (상대 쪽 문제 — 내 게임은 계속 진행)</item>
    /// </list>
    /// 🔴 역할(Host/Client)로 갈리는 것은 「게임이 멈추는가」의 차이 때문이다(P2P — 게임을 돌리는 쪽이 Host).
    ///    Client 가 끊기면 Host 의 게임은 그대로 흐르므로 Host 를 막으면 일부러 끊는 쪽이 상대를 묶어 둘 수 있다(악용 — 사용자 지적 (다)).
    ///    Host 가 끊기면 Client 의 게임도 멈추므로 Client 를 막아도 잃는 것이 없다.
    /// </para>
    /// </summary>
    public static class InGameLinkBlockPolicy
    {
        /// <summary>
        /// [2026-11-26 2차 보완 — 사용자 승인 2026-11-26] Host 가 「확인 결과 없음」 상태로 띠(Banner / PendingProbe)를
        /// 보여도 되는 최대 시간(초). 이 시간이 지나도 이번 끊김 구간의 확인 결과가 하나도 없으면 덮개(Overlay / ProbeTimeout)로 바꾼다.
        /// <para>
        /// [왜 필요한가 — 초급자용] 와이파이는 붙어 있는데 실제 인터넷이 안 되는 경우, 인터넷 확인 요청은 실패 응답이 오기까지
        /// 최대 10초까지 늦을 수 있다. 2차 규칙은 그 10초 동안 Host 를 「상대 쪽 문제(띠 — 조작 가능)」로 두었기 때문에,
        /// Host 가 일부러 인터넷을 끊어 놓고 그동안 혼자 조작하는 악용이 가능했다(Plan 12-10 한계 1).
        /// 그래서 「상대 쪽 문제라는 확인(성공 결과)이 오기 전에는 내 쪽 문제로 본다」 — 확인이 성공으로 오면 다시 띠로 내려간다.
        /// 정상 인터넷이면 확인은 보통 1초 안에 끝나므로(1차 실측 수백 ms) 피해 쪽 Host 는 2초 안에 띠(PeerSide)로 넘어간다.
        /// </para>
        /// RTT 만 느린 지연(Banner / Rtt)은 이 규칙의 대상이 아니다(신호는 오므로 끊김 구간이 아니다).
        /// </summary>
        public const double PendingProbeBannerMaxSec = 2.0;

        // [2026-11-26 2차 보완] 옛 메서드(구간 경과 시간 인자 없음 — 기록용). 새 메서드는 바로 아래.
        //   ProbeTimeout 규칙(PendingProbeBannerMaxSec)을 넣기 위해 인자 episodeElapsedSec 를 추가했다.
        // public static InGameLinkBlockMode Decide(bool isHost, InGameLinkPhase phase, InGameLinkDelayTrigger trigger,
        //     bool isRecovering, bool anyProbeFailed, int completedProbes, out InGameLinkBlockReason reason)

        /// <summary>지금 화면을 막는 방식을 고른다.</summary>
        /// <param name="isHost">이 기기가 Host 인가.</param>
        /// <param name="phase">감시 상태.</param>
        /// <param name="trigger">지연 계기.</param>
        /// <param name="isRecovering">「연결 복구 중」 표시 중인가.</param>
        /// <param name="anyProbeFailed">이번 끊김 구간에서 인터넷 확인이 한 번이라도 실패했는가.</param>
        /// <param name="completedProbes">이번 끊김 구간에서 끝난 확인 수.</param>
        /// <param name="episodeElapsedSec">
        /// [2026-11-26 2차 보완] 이번 끊김 구간이 시작된 뒤 지난 시간(초). 끊김 구간이 아니면 0 을 넣는다.
        /// </param>
        /// <param name="reason">고른 이유.</param>
        /// <returns>화면을 막는 방식.</returns>
        public static InGameLinkBlockMode Decide(bool isHost, InGameLinkPhase phase, InGameLinkDelayTrigger trigger,
            bool isRecovering, bool anyProbeFailed, int completedProbes, double episodeElapsedSec,
            out InGameLinkBlockReason reason)
        {
            bool delayed = phase == InGameLinkPhase.Delayed;
            if (!delayed && phase != InGameLinkPhase.Judging)
            {
                reason = InGameLinkBlockReason.None;
                return InGameLinkBlockMode.None;
            }
            if (delayed && (isRecovering || trigger == InGameLinkDelayTrigger.Resume))
            {
                reason = InGameLinkBlockReason.Recovering;
                return InGameLinkBlockMode.Overlay;
            }
            if (delayed && trigger == InGameLinkDelayTrigger.Rtt)
            {
                reason = InGameLinkBlockReason.Rtt;
                return InGameLinkBlockMode.Banner;
            }
            if (anyProbeFailed)
            {
                reason = InGameLinkBlockReason.SelfProbeFailed;
                return InGameLinkBlockMode.Overlay;
            }
            if (!isHost)
            {
                reason = InGameLinkBlockReason.HostLost;
                return InGameLinkBlockMode.Overlay;
            }
            // [2026-11-26 2차 보완] 옛 줄(기록용 — 확인 결과가 없는 동안 시간 제한 없이 띠):
            // reason = completedProbes <= 0 ? InGameLinkBlockReason.PendingProbe : InGameLinkBlockReason.PeerSide;
            if (completedProbes <= 0)
            {
                // 아직 확인 결과가 하나도 없다. 2.0초까지는 띠로 기다리고(정상 인터넷이면 이 안에 성공 결과가 온다),
                // 그 뒤에도 없으면 「내 쪽 문제」로 보고 덮개로 막는다(악용 방지 — PendingProbeBannerMaxSec 설명).
                if (episodeElapsedSec >= PendingProbeBannerMaxSec)
                {
                    reason = InGameLinkBlockReason.ProbeTimeout;
                    return InGameLinkBlockMode.Overlay;
                }
                reason = InGameLinkBlockReason.PendingProbe;
                return InGameLinkBlockMode.Banner;
            }
            reason = InGameLinkBlockReason.PeerSide;
            return InGameLinkBlockMode.Banner;
        }
    }
}
