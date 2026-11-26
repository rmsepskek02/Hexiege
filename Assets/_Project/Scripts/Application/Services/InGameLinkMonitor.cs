// ============================================================================
// InGameLinkMonitor.cs
// 인게임 연결 상태기계 — 「상대 신호가 언제 마지막으로 왔나」만 보고 상태를 정한다 (2026-11-26 신설).
//
// [무엇을 하나 — 초급 개발자용 설명]
//   경기 중 두 기기는 1초마다 서로에게 「나 여기 있다」 신호를 보낸다(감시기 InGameLinkWatch 담당).
//   이 클래스는 그 신호가 도착한 시각과 왕복 시간(RTT)을 받아서 다음 넷 중 지금 어느 상태인지 정한다.
//
//     대기(Waiting) ─첫 신호→ 정상(Normal) ─침묵 2초 / 느림 3초→ 지연(Delayed) ─회복→ 정상
//                                    └──────── 침묵 15초 또는 끊김 통보 ────────→ 판정 중(Judging)
//     어디서든 경기 결과가 정해지면 → 종료(Ended)
//
//   🔴 이 클래스는 시간을 스스로 읽지 않는다. 「지금 몇 초인가」를 매번 밖에서 넣어 준다(now 인자).
//      그래서 Unity 없이도 숫자만 넣어 동작을 시험할 수 있다(Plan 3절 1단계 「시간을 밖에서 넣어 시험 가능하게」).
//   🔴 역할(Host/Client)을 모른다. 두 기기가 똑같은 코드로 똑같이 판단한다(최상위 원칙 · 규칙 2 확정 D).
//   🔴 판정(인터넷 도달 확인 → 승리/이탈)은 하지 않는다. 「판정을 시작해야 한다」까지만 알려 주고,
//      실제 확인과 결론은 감시기가 한다(확인은 네트워크 요청이라 Infrastructure 의 일이다).
//
// [값 — 모두 임시값(U-4 · U-3 · U-6). const 로 둔 이유: 씬 직렬화 값이 코드 값을 이기는 함정 회피(Plan 2-1)]
//
// [⚠️ Plan 과 다른 점 하나 — 지연 「해제」의 무응답 기준 (보고 대상)]
//   Plan 2-1 은 해제 조건을 「무응답 1.0초 미만 그리고 RTT 500ms 미만이 1.0초 유지」로 적었다.
//   그런데 신호 간격 자체가 1.0초라서, 정상일 때도 다음 신호 직전에는 「마지막 신호 후 1.0초」를
//   (프레임 단위 지연만큼) 항상 조금 넘는다. 글자 그대로 구현하면 「1.0초 미만이 1.0초 동안 유지」가
//   사실상 절대 성립하지 않아 지연 덮개가 영영 내려가지 않는다.
//   그래서 해제 쪽 「무응답」은 **「다음 신호가 와야 할 때보다 얼마나 늦었나」**로 해석했다:
//     마지막 신호 후 경과 < 신호 간격(1.0) + 1.0  (= 2.0초 미만)
//   값 1.0(DelayExitSilenceSec)은 그대로이고, 신호 간격만큼의 여유(DelayExitSilenceAllowanceSec)를 더했다.
//   진입 기준(마지막 신호 후 2.0초 이상)은 Plan 그대로다. 사용자 확인 필요 항목으로 보고한다.
//
// [2026-11-26 2차 수정 — 사양 ③ 표시 시점 (사용자 확정)]
//   1차 실기에서 「게임은 바로 멈추는데 덮개는 1.4~2초 뒤에 뜨고, 게임은 바로 움직이는데 덮개는 1.2~1.8초 뒤에 사라진다」가
//   관찰됐다(1차 로그 2026-11-26_23_52 (나)). 원인은 값 자체다 — 신호 간격 1.0초 + 무응답 기준 2.0초라 뜨는 데 2초,
//   해제는 「1초 안정 확인 + 최소 표시 1초」라 사라지는 데 1초 이상이 걸렸다. 그래서:
//     · 신호 간격           1.0 → 0.5초
//     · 무응답 표시 기준     2.0 → 1.0초
//     · 무응답(Silence)으로 들어간 지연은 **신호가 다시 오는 그 순간 즉시 해제** — 1초 안정 확인 · 최소 표시 시간 없음
//     · RTT 기준(800ms 이상이 3초 연속)은 그대로. RTT 로 들어간 지연은 RTT 가 500ms 미만이 되는 그 신호에서 즉시 해제.
//       (RTT 로만 들어간 지연은 「조작을 막지 않는 띠」로 보인다 — 그 결정은 InGameLinkBlockPolicy 가 한다.)
//     · RTT 지연 중에 신호가 1.0초 끊기면 계기를 Silence 로 바꾼다(DelayTriggerChanged) — 「끊김 구간」이 시작된 것이므로
//       감시기가 인터넷 확인을 시작해야 한다.
//     · 「연결 복구 중」(Resume)의 해제 규칙은 **1차 그대로**다(사양 ② 「돌아온 기기 연결 복구 중은 기존 그대로」).
//   위의 「Plan 과 다른 점」(해제 무응답 해석)은 이제 Resume 해제에만 쓰인다.
//
// task: Assets/_Project/Docs/_Tasks/2026-11-26/10_31_ingame-leave-judgment/Plan.md 2-1 · 2-2 · 2-5 (+ 「2차 수정」 절)
// Application 레이어 — 순수 C#. UnityEngine · Unity.Netcode 참조 없음.
// ============================================================================

using System;

namespace Hexiege.Application
{
    /// <summary>지연에 들어간 계기(진단 로그 Trigger 값).</summary>
    public enum InGameLinkDelayTrigger
    {
        /// <summary>지연 상태가 아님.</summary>
        None = 0,

        /// <summary>상대 신호가 2초 넘게 오지 않았다.</summary>
        Silence = 1,

        /// <summary>신호는 오지만 왕복 시간(RTT)이 800ms 이상으로 3초 연속 느렸다.</summary>
        Rtt = 2,

        /// <summary>내 앱이 잠깐(13초 미만) 멈췄다가 돌아왔다 — 「연결 복구 중」 표시.</summary>
        Resume = 3
    }

    /// <summary>판정 중에 들어간 계기(진단 로그 Trigger 값).</summary>
    public enum InGameLinkJudgeTrigger
    {
        /// <summary>판정 중이 아님.</summary>
        None = 0,

        /// <summary>마지막 상대 신호부터 15초가 지났다.</summary>
        Silence = 1,

        /// <summary>NGO 가 「연결이 끊겼다」 콜백(OnClientDisconnectCallback)을 보냈다.</summary>
        DisconnectCallback = 2,

        /// <summary>전송 계층이 실패를 알렸다(OnTransportFailure).</summary>
        TransportFailure = 3,

        /// <summary>NGO 가 이미 내려가 있다(IsListening=false)는 것을 감시 틱이 발견했다.</summary>
        NetworkStopped = 4
    }

    /// <summary>
    /// 한 번의 입력(신호 수신 · 틱 · 통보)으로 일어난 변화. 여러 개가 함께 켜질 수 있다.
    /// 감시기는 켜진 항목마다 진단 로그 한 줄을 남긴다(상태 변화 시점에만 — LogRules §1.14 금지 8).
    /// </summary>
    [Flags]
    public enum InGameLinkChange
    {
        /// <summary>변화 없음.</summary>
        None = 0,

        /// <summary>상대 첫 신호를 받았다(대기 → 정상).</summary>
        FirstSignal = 1,

        /// <summary>지연에 들어갔다.</summary>
        DelayEntered = 2,

        /// <summary>지연에서 나왔다(평소 화면으로).</summary>
        DelayExited = 4,

        /// <summary>판정 중에 들어갔다 — 감시기가 인터넷 도달 확인을 시작해야 한다.</summary>
        JudgeStarted = 8,

        /// <summary>첫 신호 없이 판정 시간(15초)이 지났다 — 진단용 알림(판정하지 않는다).</summary>
        WaitingLong = 16,

        /// <summary>
        /// [2026-11-26 2차] 지연 계기가 RTT 에서 Silence 로 바뀌었다(느린 신호마저 1.0초 끊김) — 끊김 구간 시작.
        /// </summary>
        DelayTriggerChanged = 32
    }

    /// <summary>
    /// 인게임 연결 상태기계. 시간은 항상 밖에서 넣어 준다(초 단위, 같은 시계를 계속 써야 한다).
    /// Host·Client 가 같은 코드로 같은 판단을 한다.
    /// </summary>
    public sealed class InGameLinkMonitor
    {
        // ====================================================================
        // 값 (Plan 2-1 — 모두 임시값)
        // ====================================================================

        /// <summary>신호 간격(초). 두 기기가 이 간격으로 서로에게 신호를 보낸다.</summary>
        // [2026-11-26 2차 — 사양 ③] 1.0 → 0.5. 옛 줄: public const double HeartbeatIntervalSec = 1.0;
        public const double HeartbeatIntervalSec = 0.5;

        /// <summary>지연 진입 — 마지막 상대 신호 후 이만큼(초) 넘게 아무것도 없으면 지연.</summary>
        // [2026-11-26 2차 — 사양 ③] 2.0 → 1.0. 옛 줄: public const double DelayEnterSilenceSec = 2.0;
        public const double DelayEnterSilenceSec = 1.0;

        /// <summary>지연 진입 — 이 RTT(ms) 이상이 계속되면 「느림」으로 본다.</summary>
        public const double DelayEnterRttMs = 800.0;

        /// <summary>지연 진입 — 느린 RTT 가 이만큼(초) 연속이면 지연.</summary>
        public const double DelayEnterRttHoldSec = 3.0;

        /// <summary>지연 해제 — 무응답 기준(초). 해석은 파일 머리말의 「Plan 과 다른 점」 참조.</summary>
        public const double DelayExitSilenceSec = 1.0;

        /// <summary>
        /// 지연 해제 — 무응답 기준에 더하는 여유(초) = 신호 간격.
        /// 「마지막 신호 후 경과」에서 신호 간격만큼은 정상적인 기다림이므로 빼고 본다(머리말 참조).
        /// </summary>
        public const double DelayExitSilenceAllowanceSec = HeartbeatIntervalSec;

        /// <summary>지연 해제 — 이 RTT(ms) 미만이어야 한다.</summary>
        public const double DelayExitRttMs = 500.0;

        /// <summary>지연 해제 — 두 조건을 함께 이만큼(초) 유지해야 해제.</summary>
        /// <remarks>[2026-11-26 2차] 이제 「연결 복구 중」(Resume) 해제에만 쓴다. Silence · RTT 지연은 즉시 해제(사양 ③).</remarks>
        public const double DelayExitHoldSec = 1.0;

        /// <summary>지연 표시를 켰으면 최소 이만큼(초)은 보인다(깜빡임 방지).</summary>
        /// <remarks>
        /// [2026-11-26 2차] 더 이상 쓰지 않는다(사양 ③ 「최소 표시 시간 제거」). 1차 기록 · 로그 비교용으로 값만 남긴다.
        /// 「연결 복구 중」의 최소 표시는 RecoveringMinShownSec(0.5초)가 따로 맡는다(1차 그대로).
        /// </remarks>
        public const double DelayMinShownSec = 1.0;

        /// <summary>
        /// [2026-11-26 2차] 깜빡임 관찰 기준(초) — 지연 표시가 이보다 짧게 떴다 사라지면 「짧은 지연」으로 센다(진단 로그 전용).
        /// </summary>
        public const double ShortDelaySec = 1.0;

        /// <summary>판정 시작 N(초) — 마지막 상대 신호부터.</summary>
        public const double JudgeSilenceSec = 15.0;

        /// <summary>스스로 패배 기준 — 내 앱이 이만큼(초) 이상 멈췄다가 돌아오면 패배 처리(U-6).</summary>
        public const double SelfPauseDefeatSec = 13.0;

        /// <summary>「연결 복구 중」 최소 표시(초).</summary>
        public const double RecoveringMinShownSec = 0.5;

        /// <summary>
        /// 「내 앱이 멈췄었다」로 보는 최소 정지 시간(초). 감시기가 프레임 사이 기기 시계 차가
        /// 이보다 크면 앱 정지로 본다. 지연 진입 기준(2.0초)과 같은 값 — 그보다 짧은 멈춤은
        /// 상대에게도 지연으로 보이지 않을 만큼 짧아서 따로 처리할 이유가 없다.
        /// </summary>
        /// <remarks>
        /// [2026-11-26 2차] 지연 진입 기준이 2.0 → 1.0 으로 바뀌었지만 이 값은 <b>2.0 그대로</b> 둔다.
        /// 「돌아온 기기 『연결 복구 중』은 기존 그대로」(사양 ②) — 1~2초짜리 프레임 멈춤까지 「연결 복구 중」 덮개(최소 0.5초 +
        /// 1초 안정 확인)로 보이면 오히려 오래 막힌다. 그런 짧은 멈춤은 무응답 지연(즉시 해제)으로 처리된다.
        /// 옛 줄: public const double FreezeDetectSec = DelayEnterSilenceSec;
        /// </remarks>
        public const double FreezeDetectSec = 2.0;

        // ====================================================================
        // 상태
        // ====================================================================

        private readonly double _startedAt;          // 감시 시작 시각
        private double _slowRttSince = double.NaN;  // 느린 RTT 가 처음 시작된 시각(NaN = 느리지 않음)
        private double _exitGoodSince = double.NaN; // 해제 조건을 처음 만족한 시각(NaN = 만족 안 함)
        private bool _waitingLongReported;          // WaitingLong 을 이미 알렸는가(경기당 1회)

        /// <summary>현재 상태.</summary>
        public InGameLinkPhase Phase { get; private set; } = InGameLinkPhase.Waiting;

        /// <summary>「연결 복구 중」 문구를 보여야 하는가.</summary>
        public bool IsRecovering { get; private set; }

        /// <summary>상대 첫 신호를 받았는가.</summary>
        public bool HasFirstSignal { get; private set; }

        /// <summary>상대 첫 신호를 받은 시각(받기 전에는 NaN).</summary>
        public double FirstSignalAt { get; private set; } = double.NaN;

        /// <summary>상대 신호를 마지막으로 받은 시각(받기 전에는 NaN). 침묵 시간의 기준점.</summary>
        public double LastSignalAt { get; private set; } = double.NaN;

        /// <summary>가장 최근 RTT(ms). 모르면 NaN.</summary>
        public double LatestRttMs { get; private set; } = double.NaN;

        /// <summary>마지막으로 지연에 들어간 계기.</summary>
        public InGameLinkDelayTrigger DelayTrigger { get; private set; } = InGameLinkDelayTrigger.None;

        /// <summary>판정 중에 들어간 계기.</summary>
        public InGameLinkJudgeTrigger JudgeTrigger { get; private set; } = InGameLinkJudgeTrigger.None;

        /// <summary>덮개를 켠 시각(지연 또는 판정 중 진입). 켜지 않았으면 NaN.</summary>
        public double DelayEnteredAt { get; private set; } = double.NaN;

        /// <summary>판정 중에 들어간 시각. 안 들어갔으면 NaN.</summary>
        public double JudgeStartedAt { get; private set; } = double.NaN;

        /// <summary>직전 지연 해제 때 덮개가 떠 있던 시간(초). 해제 로그 ShownSec 용.</summary>
        public double LastShownSeconds { get; private set; } = double.NaN;

        /// <summary>[2026-11-26 2차] 이번 경기에서 지연에 들어간 횟수(깜빡임 관찰용 — DelayNo).</summary>
        public int DelayCount { get; private set; }

        /// <summary>[2026-11-26 2차] 직전 지연 해제 시각(초). 해제한 적이 없으면 NaN. 「해제 후 얼마 만에 다시 떴나」 로그용.</summary>
        public double LastDelayExitedAt { get; private set; } = double.NaN;

        /// <summary>[2026-11-26 2차] 이번 지연 진입 시점의 「직전 해제 후 경과(초)」. 첫 지연이면 NaN. 짧을수록 깜빡임.</summary>
        public double SinceLastExitAtEnterSeconds { get; private set; } = double.NaN;

        /// <summary>
        /// [2026-11-26 2차] 가장 최근 신호가 도착했을 때 「그 직전 신호와의 간격(초)」. 즉시 해제 확인용(LinkDelayExit SignalGapMs).
        /// 첫 신호 전이면 NaN.
        /// </summary>
        public double LastSignalGapSeconds { get; private set; } = double.NaN;

        /// <summary>[2026-11-26 2차] 직전 지연 해제를 일으킨 것(Signal / RttRecovered / ResumeHold). 해제 로그 ExitBy 용.</summary>
        public string LastExitBy { get; private set; } = "-";

        /// <summary>
        /// 지금 플레이어 조작을 막아야 하는가(지연·판정 중).
        /// ⚠️ [2026-11-26 2차] 이 값은 「지연 · 판정 중인가」라는 상태 사실일 뿐이다. 실제 조작 차단은 이제
        ///    NetworkLinkState.BlocksPlayerCommands(차단 방식 Overlay 일 때만)가 정한다. 이 속성을 읽는 곳은 없다(기록용으로 둔다).
        /// </summary>
        public bool BlocksPlayerCommands =>
            Phase == InGameLinkPhase.Delayed || Phase == InGameLinkPhase.Judging;

        /// <summary>감시를 시작한다. 이 시각부터 대기 상태다.</summary>
        /// <param name="now">현재 시각(초).</param>
        public InGameLinkMonitor(double now)
        {
            _startedAt = now;
        }

        /// <summary>감시 시작 후 경과(초).</summary>
        /// <param name="now">현재 시각(초).</param>
        /// <returns>경과 초.</returns>
        public double SinceStartSeconds(double now) => now - _startedAt;

        /// <summary>마지막 상대 신호 후 경과(초). 첫 신호 전이면 NaN.</summary>
        /// <param name="now">현재 시각(초).</param>
        /// <returns>침묵 초 또는 NaN.</returns>
        public double SilenceSeconds(double now) => HasFirstSignal ? now - LastSignalAt : double.NaN;

        // ====================================================================
        // 입력
        // ====================================================================

        /// <summary>
        /// 상대 신호를 받았다. RTT 를 모르면 double.NaN 을 넣는다.
        /// 🔴 판정 중에는 신호가 돌아와도 판정을 취소하지 않는다(Plan 2-3 5번) — 시각만 기록한다.
        /// </summary>
        /// <param name="now">현재 시각(초).</param>
        /// <param name="rttMs">이번 신호로 잰 왕복 시간(ms). 모르면 NaN.</param>
        /// <returns>이번 입력으로 일어난 변화.</returns>
        public InGameLinkChange OnSignal(double now, double rttMs)
        {
            if (Phase == InGameLinkPhase.Ended) return InGameLinkChange.None;
            if (Phase == InGameLinkPhase.Judging)
            {
                // 판정은 뒤집지 않는다. 다만 「판정 시점의 마지막 신호 후 경과」를 로그에서 정확히
                // 볼 수 있도록 시각과 RTT 는 그대로 적어 둔다.
                LastSignalAt = now;
                if (!double.IsNaN(rttMs)) LatestRttMs = rttMs;
                return InGameLinkChange.None;
            }

            InGameLinkChange change = InGameLinkChange.None;
            if (!HasFirstSignal)
            {
                HasFirstSignal = true;
                FirstSignalAt = now;
                change |= InGameLinkChange.FirstSignal;
                if (Phase == InGameLinkPhase.Waiting) Phase = InGameLinkPhase.Normal;
            }

            // [2026-11-26 2차] 이 신호와 직전 신호의 간격 — 「신호가 다시 오는 순간 즉시 해제」를 로그로 확인하기 위함(SignalGapMs).
            //   첫 신호면 직전 신호가 없으므로 NaN.
            LastSignalGapSeconds = double.IsNaN(LastSignalAt) ? double.NaN : now - LastSignalAt;

            LastSignalAt = now;
            if (!double.IsNaN(rttMs))
            {
                LatestRttMs = rttMs;
                if (rttMs >= DelayEnterRttMs)
                {
                    // 느린 상태가 「처음」 시작된 시각만 적는다 — 연속 여부는 아래 빠른 신호가 끊는다.
                    if (double.IsNaN(_slowRttSince)) _slowRttSince = now;
                }
                else
                {
                    _slowRttSince = double.NaN; // 빠른 신호가 하나라도 오면 「연속 느림」이 끊긴다
                }
            }

            // [2026-11-26 2차 — 사양 ③] 무응답(Silence)으로 들어간 지연은 신호가 다시 오는 바로 이 순간 해제한다.
            //   1차의 「1초 안정 확인 + 최소 표시 1초」를 거치지 않는다. 바로 return 하는 이유: 같은 호출 안에서
            //   Evaluate 가 RTT 지연으로 다시 들어가 「해제 + 진입」이 한꺼번에 나오면 로그 순서가 헷갈린다 —
            //   RTT 재진입 여부는 다음 프레임 Tick 이 본다(해제 때 느림 집계를 지우므로 곧바로 다시 들어가지도 않는다).
            if (Phase == InGameLinkPhase.Delayed && DelayTrigger == InGameLinkDelayTrigger.Silence)
            {
                ExitDelay(now, "Signal");
                return change | InGameLinkChange.DelayExited;
            }

            return change | Evaluate(now);
        }

        /// <summary>
        /// 시간이 흘렀다(감시기가 매 프레임 부른다). 침묵·느림·해제 조건을 검사한다.
        /// </summary>
        /// <param name="now">현재 시각(초).</param>
        /// <returns>이번 틱에서 일어난 변화.</returns>
        public InGameLinkChange Tick(double now)
        {
            if (Phase == InGameLinkPhase.Ended || Phase == InGameLinkPhase.Judging) return InGameLinkChange.None;
            return Evaluate(now);
        }

        /// <summary>
        /// 연결이 끊겼다는 통보(콜백 · 전송 실패 · NGO 정지)를 받았다 → 곧바로 판정 중.
        /// 대기 상태(첫 신호 전)에서도 판정 중으로 간다(Plan 2-2).
        /// </summary>
        /// <param name="now">현재 시각(초).</param>
        /// <param name="trigger">통보 종류.</param>
        /// <returns>JudgeStarted 또는 None(이미 판정 중·종료).</returns>
        public InGameLinkChange OnDisconnectNotice(double now, InGameLinkJudgeTrigger trigger)
        {
            if (Phase == InGameLinkPhase.Ended || Phase == InGameLinkPhase.Judging) return InGameLinkChange.None;
            StartJudging(now, trigger);
            return InGameLinkChange.JudgeStarted;
        }

        /// <summary>
        /// 내 앱이 잠깐(13초 미만) 멈췄다가 돌아왔다(Plan 2-5).
        /// 🔴 멈춰 있던 시간은 「상대가 조용했던 시간」이 아니다 — 내가 듣지 못했을 뿐이다.
        ///    그래서 침묵 기준점을 지금으로 되돌려, 돌아온 쪽이 상대를 침묵으로 오판하지 않게 한다.
        ///    「연결 복구 중」 덮개를 띄우고, 새 신호와 새 RTT 가 와야 해제된다(옛 RTT 는 버린다).
        /// </summary>
        /// <param name="now">현재 시각(초).</param>
        /// <returns>DelayEntered(덮개를 새로 켰을 때) 또는 None.</returns>
        public InGameLinkChange OnResumedShort(double now)
        {
            // 판정이 이미 시작됐으면 뒤집지 않는다. 첫 신호 전이면 기준점이 없어 할 일이 없다.
            if (Phase == InGameLinkPhase.Ended || Phase == InGameLinkPhase.Judging) return InGameLinkChange.None;
            if (!HasFirstSignal) return InGameLinkChange.None;

            bool wasShown = Phase == InGameLinkPhase.Delayed;
            LastSignalAt = now;               // 침묵 기준점 재설정
            LatestRttMs = double.NaN;         // 멈추기 전 RTT 로 해제되지 않게
            _slowRttSince = double.NaN;
            _exitGoodSince = double.NaN;
            Phase = InGameLinkPhase.Delayed;
            DelayTrigger = InGameLinkDelayTrigger.Resume;
            IsRecovering = true;
            DelayEnteredAt = now;             // 최소 표시(0.5초)를 지금부터 센다
            if (!wasShown) NoteDelayEntered(now); // [2026-11-26 2차] 깜빡임 관찰용 집계(새로 켰을 때만)
            return wasShown ? InGameLinkChange.None : InGameLinkChange.DelayEntered;
        }

        /// <summary>경기 결과가 정해졌다 — 더 이상 감시하지 않는다.</summary>
        public void MarkEnded()
        {
            Phase = InGameLinkPhase.Ended;
            IsRecovering = false;
        }

        // ====================================================================
        // 내부
        // ====================================================================

        /// <summary>침묵·느림·해제 조건을 검사해 상태를 옮긴다(정상 ↔ 지연, → 판정 중).</summary>
        private InGameLinkChange Evaluate(double now)
        {
            if (Phase == InGameLinkPhase.Waiting)
            {
                // 첫 신호 전에는 침묵을 재지 않는다(씬 로딩 차이로 첫 신호가 늦을 수 있다 — Research 7절).
                // 15초가 지나도 판정하지 않고 「오래 기다린다」는 진단만 한 번 알린다. 끊김 통보가 오면
                // 그때 판정 중으로 간다(OnDisconnectNotice) — Plan 2-2, 11절 3번 보고 항목.
                if (!_waitingLongReported && now - _startedAt >= JudgeSilenceSec)
                {
                    _waitingLongReported = true;
                    return InGameLinkChange.WaitingLong;
                }
                return InGameLinkChange.None;
            }

            double silence = now - LastSignalAt;

            // ① 15초 — 어느 상태에서든(정상·지연) 판정 중으로 간다.
            if (silence >= JudgeSilenceSec)
            {
                StartJudging(now, InGameLinkJudgeTrigger.Silence);
                return InGameLinkChange.JudgeStarted;
            }

            if (Phase == InGameLinkPhase.Normal)
            {
                // ② 지연 진입 — 침묵이 먼저, 그다음 느림.
                if (silence >= DelayEnterSilenceSec)
                {
                    EnterDelay(now, InGameLinkDelayTrigger.Silence);
                    return InGameLinkChange.DelayEntered;
                }
                if (!double.IsNaN(_slowRttSince) && now - _slowRttSince >= DelayEnterRttHoldSec)
                {
                    EnterDelay(now, InGameLinkDelayTrigger.Rtt);
                    return InGameLinkChange.DelayEntered;
                }
                return InGameLinkChange.None;
            }

            // ----------------------------------------------------------------
            // [2026-11-26 2차 — 사양 ③] 지연 해제 규칙을 계기별로 나눴다.
            //   Silence : 여기서는 해제하지 않는다 — 신호가 오는 순간 OnSignal 이 즉시 해제한다.
            //   Rtt     : ⓐ 신호가 1.0초 끊기면 계기를 Silence 로 바꾼다(끊김 구간 시작 — 인터넷 확인 시작 신호)
            //             ⓑ RTT 가 500ms 미만이 되면 즉시 해제(1초 유지 · 최소 표시 없음)
            //   Resume  : 1차 규칙 그대로(아래 ③ — 「연결 복구 중」은 기존 그대로, 사양 ②)
            // ----------------------------------------------------------------
            if (Phase == InGameLinkPhase.Delayed && DelayTrigger == InGameLinkDelayTrigger.Silence)
            {
                return InGameLinkChange.None;
            }

            if (Phase == InGameLinkPhase.Delayed && DelayTrigger == InGameLinkDelayTrigger.Rtt)
            {
                if (silence >= DelayEnterSilenceSec)
                {
                    // 느린 신호마저 끊겼다 — 화면에 뜬 시간(DelayEnteredAt)은 처음 RTT 로 들어간 때부터 그대로 센다.
                    DelayTrigger = InGameLinkDelayTrigger.Silence;
                    return InGameLinkChange.DelayTriggerChanged;
                }
                if (!double.IsNaN(LatestRttMs) && LatestRttMs < DelayExitRttMs)
                {
                    ExitDelay(now, "RttRecovered");
                    return InGameLinkChange.DelayExited;
                }
                return InGameLinkChange.None;
            }

            if (Phase == InGameLinkPhase.Delayed)
            {
                // ③ 지연 해제 — 「신호가 제때 온다」 AND 「RTT 가 빠르다」를 함께 1초 유지 + 최소 표시.
                //    [2026-11-26 2차] 이 블록은 이제 Resume(「연결 복구 중」)에만 온다 — 위 두 분기가 Silence · Rtt 를 먼저 가져간다.
                //    1차에는 세 계기 모두 이 블록으로 해제됐다(그것이 「복구 때 덮개가 1.2~1.8초 늦게 사라짐」의 원인).
                bool silenceGood = silence < DelayExitSilenceSec + DelayExitSilenceAllowanceSec;
                bool rttGood = !double.IsNaN(LatestRttMs) && LatestRttMs < DelayExitRttMs;
                if (!(silenceGood && rttGood))
                {
                    _exitGoodSince = double.NaN;
                    return InGameLinkChange.None;
                }
                if (double.IsNaN(_exitGoodSince)) _exitGoodSince = now;

                double minShown = IsRecovering ? RecoveringMinShownSec : DelayMinShownSec;
                if (now - _exitGoodSince >= DelayExitHoldSec && now - DelayEnteredAt >= minShown)
                {
                    // [2026-11-26 2차] 해제 처리를 ExitDelay 하나로 모았다(해제 시각 · 계기를 함께 적기 위함). 옛 본문(기록용):
                    // LastShownSeconds = now - DelayEnteredAt;
                    // Phase = InGameLinkPhase.Normal;
                    // IsRecovering = false;
                    // _exitGoodSince = double.NaN;
                    // _slowRttSince = double.NaN;
                    ExitDelay(now, "ResumeHold");
                    return InGameLinkChange.DelayExited;
                }
            }

            return InGameLinkChange.None;
        }

        /// <summary>지연에 들어간다(덮개 켜기).</summary>
        private void EnterDelay(double now, InGameLinkDelayTrigger trigger)
        {
            Phase = InGameLinkPhase.Delayed;
            DelayTrigger = trigger;
            IsRecovering = false;
            DelayEnteredAt = now;
            _exitGoodSince = double.NaN;
            NoteDelayEntered(now); // [2026-11-26 2차] 깜빡임 관찰용 집계
        }

        /// <summary>[2026-11-26 2차] 지연 진입 횟수 · 「직전 해제 후 경과」를 적는다(깜빡임 관찰 — 로그 전용, 판단에 쓰지 않는다).</summary>
        private void NoteDelayEntered(double now)
        {
            DelayCount++;
            SinceLastExitAtEnterSeconds = double.IsNaN(LastDelayExitedAt) ? double.NaN : now - LastDelayExitedAt;
        }

        /// <summary>
        /// [2026-11-26 2차] 지연에서 나온다(평소 화면으로). 세 해제 경로(Signal · RttRecovered · ResumeHold)가 함께 쓴다.
        /// 느림 집계를 지워, 해제 직후 같은 느림으로 곧바로 다시 들어가지 않게 한다(1차 해제와 같은 처리).
        /// </summary>
        private void ExitDelay(double now, string exitBy)
        {
            LastShownSeconds = now - DelayEnteredAt;
            Phase = InGameLinkPhase.Normal;
            IsRecovering = false;
            _exitGoodSince = double.NaN;
            _slowRttSince = double.NaN;
            LastDelayExitedAt = now;
            LastExitBy = exitBy;
        }

        /// <summary>판정 중에 들어간다. 덮개가 아직 꺼져 있었으면 지금부터 켠 것으로 본다.</summary>
        private void StartJudging(double now, InGameLinkJudgeTrigger trigger)
        {
            if (Phase != InGameLinkPhase.Delayed) DelayEnteredAt = now;
            Phase = InGameLinkPhase.Judging;
            JudgeTrigger = trigger;
            JudgeStartedAt = now;
            IsRecovering = false;
        }
    }
}
