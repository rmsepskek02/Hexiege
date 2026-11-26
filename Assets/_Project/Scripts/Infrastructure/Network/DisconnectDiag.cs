// ============================================================================
// DisconnectDiag.cs
// 인게임 연결 끊김 「실측」용 진단 로그의 공통 키·공통 상태를 모아 둔 정적 도우미.
//
// [이 파일이 왜 있나 — 초급 개발자용 설명]
//   경기 도중 한쪽 연결이 끊겼을 때 「몇 초 뒤에 무엇이 일어나는가」를 기기 두 대로 재기 위한
//   측정용 기록이다(task: _Tasks/2026-11-26/05_28_ingame-disconnect-measure/Plan.md).
//   🔴 게임 동작은 한 줄도 바꾸지 않는다. 이 클래스는 「기록할 글자」를 만들고
//      「몇 번 일어났는지」를 세기만 한다. 게임 상태(승패·대기 시간·연결)는 읽지도 쓰지도 않는다.
//
// [왜 이 파일만 #if 로 감싸지 않았나]
//   기존 로그 줄(ReconnectionHandler · NetworkGameManager · NetworkGameEndController)에
//   `DisconnectDiag.Keys(...)` 를 인자로 덧붙였다. 그 인자를 받는 `GameLog.Dev.*` 는
//   [Conditional] 이라 릴리스 빌드에서는 호출과 인자 계산이 **통째로 사라진다**(LogRules §1.7).
//   그런데 C# 컴파일러는 사라질 호출의 인자도 **문법 검사는 한다.** 그래서 이 클래스가
//   릴리스에 아예 없으면 릴리스 빌드가 컴파일 오류가 난다 → 이 클래스는 항상 컴파일되게 두고,
//   실제로 부르는 쪽(GameLog.Dev 인자 · [Conditional] 메서드 · #if 블록)이 릴리스에서 전부 사라지게 했다.
//   결과: 릴리스 빌드에서는 이 클래스의 어떤 코드도 실행되지 않는다.
//
// [공통 키 — 모든 진단 줄의 맨 앞에 붙는다 (Plan 2절)]
//   Diag=Disconnect  검색어. 이 한 단어로 이번 측정 줄이 전부 잡힌다.
//   DiagRev=2        진단 내용 버전. 키·의미를 바꾸면 올린다.
//                    (2026-11-26 1→2: 인게임 이탈 판정 task 가 새 이벤트(Link*, OfflineForfeit, CommandBlocked …)를
//                     더하고 ResultShown 에 Cause·Countdown 을 붙였다. 측정 task 의 기존 이벤트 이름·키는 그대로다 —
//                     _Tasks/2026-11-26/10_31_ingame-leave-judgment/Plan.md 5절.)
//   Event=…          무슨 일이 일어났는지(MatchStart, PeerDisconnected …). 한 번 정하면 바꾸지 않는다.
//   Role=Host|Client 이 기기의 역할. 경기 시작(MatchStart) 때 정해 둔 값. 경기 밖이면 `-`.
//   UtcMs            기기 시계(1970-01-01 UTC 기준 ms). 🔴 두 기기를 비교할 때는 이 값의 차를 쓴다.
//   RtMs             앱 시작 후 실제 경과 ms(Time.realtimeSinceStartupAsDouble).
//                    ⚠️ Android 에서 앱이 내려가 있는 동안 이 값이 흐르는지는 미확인이다.
//   MatchSec         경기 시작(MatchStart) 기준 경과 초(소수 1자리, RtMs 와 같은 시계). 경기 밖이면 `-`.
//
// [민감 정보 — LogRules §1.6 · §1.14 금지 4]
//   PlayerId·UID·이메일·토큰·Relay Join Code·Lobby 코드·Allocation ID·IP 주소는 넣지 않는다.
//   NGO ClientId(경기마다 0·1·2 로 매기는 세션 번호)는 계정 식별자가 아니라 넣는다.
//   DisconnectReason 처럼 엔진이 주는 자유 문장은 SanitizeReason 으로 IP 모양을 가리고 자른다.
//
// Infrastructure 레이어 — Unity 시간 API 와 GameLog(Application)만 쓴다. Unity.Netcode 는 쓰지 않는다.
// ============================================================================

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using Hexiege.Application; // GameLog. ⚠️ 이 파일에서 수식 없는 Application 은 Hexiege.Application 이다.

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 인게임 연결 끊김 실측용 진단 로그의 공통 키 조립과 경기 단위 집계를 맡는 정적 도우미.
    /// 게임 상태를 바꾸지 않으며, 릴리스 빌드에서는 호출부가 모두 사라져 실행되지 않는다.
    /// </summary>
    public static class DisconnectDiag
    {
        // ====================================================================
        // 상수
        // ====================================================================

        /// <summary>새 진단 줄의 메시지 앞에 붙이는 회차 구분 접두어. 메시지 = "[DISC-DIAG] &lt;Event&gt;".</summary>
        public const string MessagePrefix = "[DISC-DIAG]";

        /// <summary>진단 내용 버전(DiagRev). 키 이름이나 의미를 바꾸면 이 값을 올린다.</summary>
        public const int DiagRevision = 2; // [2026-11-26] 1 → 2 (인게임 이탈 판정 task — 머리말 참조)

        /// <summary>자유 문장 값(DisconnectReason)을 자르는 최대 글자 수(Plan 2절 「값 표기」).</summary>
        private const int ReasonMaxLength = 120;

        /// <summary>값이 없을 때 쓰는 표기(Plan 2절). 빈 문자열 대신 이 글자를 남겨 「값 없음」을 눈으로 구분한다.</summary>
        public const string None = "-";

        /// <summary>
        /// IPv4 주소처럼 보이는 글자(숫자.숫자.숫자.숫자)를 찾는 정규식.
        /// 엔진이 주는 끊김 사유 문장에 주소가 섞일 수 있는지 코드로 확인할 수 없어서(패키지 소스가 로컬에 없다),
        /// 섞이더라도 로그에 남지 않게 미리 가린다(LogRules §1.6 — IP 금지).
        /// </summary>
        private static readonly Regex IpLikePattern =
            new Regex(@"\d{1,3}(\.\d{1,3}){3}(:\d+)?", RegexOptions.CultureInvariant);

        // ====================================================================
        // 경기 단위 상태 — 관찰자(InGameDisconnectObserver)만 바꾼다
        // ====================================================================
        //   ⚠️ 모두 「기록용 숫자」다. 게임 로직은 이 값을 읽지 않는다.

        private static bool s_matchActive;              // 지금 경기 중인가(MatchStart ~ MatchSummary 사이)
        private static double s_matchStartRealtime;     // MatchStart 시각(Time.realtimeSinceStartupAsDouble, 초)
        private static string s_role = None;            // "Host" / "Client" / "-"
        private static int s_matchNo;                   // 앱 실행 후 몇 번째 경기인가(1부터)

        private static int s_disconnectNo;              // 이번 경기 PeerDisconnected 횟수
        private static int s_connectNo;                 // 이번 경기 PeerConnected 횟수
        private static int s_pauseNo;                   // 이번 경기 AppPause(Paused=True) 횟수
        private static int s_transportFailureNo;        // 이번 경기 TransportFailure 횟수
        private static bool s_resultShown;              // 이번 경기에 결과 화면이 떴는가

        private static bool s_hasPeerDisconnect;        // 이번 경기에 「상대」 끊김을 받은 적이 있는가
        private static double s_lastPeerDisconnectRealtime; // 마지막 「상대」 끊김 시각(초)
        private static ulong s_lastDisconnectedPeerId; // 마지막으로 끊긴 상대의 NGO ClientId

        private static bool s_hasResume;                // 이번 경기에 앱 복귀(Paused=False)가 있었는가
        private static double s_lastResumeRealtime;     // 마지막 복귀 시각(초)

        /// <summary>지금 경기 중(MatchStart 이후, MatchSummary 이전)인지 여부.</summary>
        public static bool IsMatchActive => s_matchActive;

        // ====================================================================
        // 공통 키 조립
        // ====================================================================

        /// <summary>
        /// 모든 진단 줄 맨 앞에 붙는 공통 키(Diag·DiagRev·Event·Role·UtcMs·RtMs·MatchSec)를 만든다.
        /// extra 가 있으면 그 뒤에 ", " 로 이어 붙인다(기존 줄의 원래 key=value 를 그대로 넘기는 자리).
        /// 이 메서드는 예외를 밖으로 던지지 않는다 — 로그 인자 계산이 실패해도 기존 처리 흐름이 끊기지 않게 하기 위함.
        /// </summary>
        /// <param name="eventName">Event 값. 「무엇이 일어났는지」 이름(예: GraceStart).</param>
        /// <param name="extra">덧붙일 key=value 목록(", " 로 이어진 문자열). 없으면 null.</param>
        /// <returns>로그 data 문자열.</returns>
        public static string Keys(string eventName, string extra = null)
        {
            try
            {
                string head = "Diag=Disconnect, DiagRev=" + DiagRevision.ToString(CultureInfo.InvariantCulture)
                    + ", Event=" + (string.IsNullOrEmpty(eventName) ? None : eventName)
                    + ", Role=" + s_role
                    + ", UtcMs=" + UtcMsNow().ToString(CultureInfo.InvariantCulture)
                    + ", RtMs=" + RtMsNow().ToString(CultureInfo.InvariantCulture)
                    + ", MatchSec=" + MatchSecText();
                return string.IsNullOrEmpty(extra) ? head : head + ", " + extra;
            }
            catch (Exception e)
            {
                // 여기까지 올 일은 사실상 없지만, 로그 글자 만들기 때문에 게임 흐름이 끊기면 안 된다.
                string fallback = "Diag=Disconnect, DiagRev=" + DiagRevision + ", Event=" + eventName
                    + ", DiagError=" + e.GetType().Name;
                return string.IsNullOrEmpty(extra) ? fallback : fallback + ", " + extra;
            }
        }

        /// <summary>기기 시계 기준 현재 시각(1970-01-01 UTC 부터 ms). 두 기기 비교용.</summary>
        public static long UtcMsNow() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>앱 시작 후 실제 경과 ms. Time.timeScale(결과 화면에서 0)의 영향을 받지 않는다.</summary>
        public static long RtMsNow() => (long)(Time.realtimeSinceStartupAsDouble * 1000d);

        /// <summary>경기 시작 기준 경과 초(소수 1자리). 경기 밖이면 "-".</summary>
        public static string MatchSecText()
        {
            if (!s_matchActive) return None;
            return SecondsText(Time.realtimeSinceStartupAsDouble - s_matchStartRealtime);
        }

        /// <summary>
        /// 마지막 「상대」 끊김 콜백(PeerDisconnected, IsSelf=False)부터 지금까지 걸린 초. 없으면 "-".
        /// ReconnectionHandler 의 30초 대기는 바로 그 콜백 안에서 시작되므로, 강제 승리 줄에서 이 값은
        /// 「대기 시작부터 실제로 걸린 초」와 같다(GraceElapsedSec). 30 보다 크게 나오면 그 사이 앱이
        /// 멈춰 있었거나(백그라운드) 프레임이 밀렸다는 뜻이다.
        /// </summary>
        public static string SinceLastPeerDisconnectText()
        {
            if (!s_matchActive || !s_hasPeerDisconnect) return None;
            return SecondsText(Time.realtimeSinceStartupAsDouble - s_lastPeerDisconnectRealtime);
        }

        /// <summary>마지막 앱 복귀(AppPause Paused=False)부터 지금까지 걸린 초. 없으면 "-".</summary>
        public static string SinceResumeText()
        {
            if (!s_matchActive || !s_hasResume) return None;
            return SecondsText(Time.realtimeSinceStartupAsDouble - s_lastResumeRealtime);
        }

        /// <summary>
        /// 엔진이 준 자유 문장(예: NGO DisconnectReason)을 key=value 값으로 쓸 수 있게 다듬는다.
        /// ① 비어 있으면 "-" ② 줄바꿈 → 공백 ③ ", " 와 "," → ";" (LogRules §1.4 — 값 안에 구분자 금지)
        /// ④ IP 주소 모양 → "&lt;ip&gt;" (§1.6) ⑤ 120자로 자름.
        /// </summary>
        /// <param name="raw">원본 문장. null 허용.</param>
        /// <returns>로그에 넣을 수 있는 값.</returns>
        public static string SanitizeReason(string raw)
        {
            try
            {
                if (string.IsNullOrEmpty(raw)) return None;
                string s = raw.Replace("\r", " ").Replace("\n", " ").Replace(", ", ";").Replace(",", ";");
                s = IpLikePattern.Replace(s, "<ip>").Trim();
                if (s.Length == 0) return None;
                return s.Length > ReasonMaxLength ? s.Substring(0, ReasonMaxLength) : s;
            }
            catch (Exception e)
            {
                return "SanitizeFailed:" + e.GetType().Name;
            }
        }

        /// <summary>초 값을 소수 1자리 문자열로(문화권 무관 — 소수점이 항상 '.').</summary>
        private static string SecondsText(double seconds)
            => seconds.ToString("F1", CultureInfo.InvariantCulture);

        // ====================================================================
        // ⑧ 결과 화면 — Presentation(GameEndUI)이 부르는 진입점
        // ====================================================================

        /// <summary>
        /// 결과 화면이 뜬 순간을 기록한다(Event=ResultShown) — 경기 중일 때만 남긴다(싱글플레이는 기록 안 함).
        /// [Conditional] 두 개가 붙어 있어 릴리스 빌드에서는 호출 자체가 사라진다(LogRules §1.7).
        /// 예외를 밖으로 던지지 않는다 — 결과 화면 표시 흐름을 끊지 않기 위함.
        /// </summary>
        /// <param name="className">로그 카테고리의 Class 이름(부른 쪽 — GameEndUI).</param>
        /// <param name="isWin">이 기기 기준 승리 여부.</param>
        /// <param name="cause">
        /// [2026-11-26 DiagRev=2 추가] 결과 사유(Normal / OpponentLeft / SelfDisconnected / SelfPauseDefeat /
        /// OfflineForfeit). 기존 호출(인자 2개)과 호환되도록 기본값 "Normal" 을 둔다.
        /// </param>
        /// <param name="countdownSeconds">
        /// [2026-11-26 DiagRev=2 추가] 결과 화면 자동 복귀 카운트다운 길이(초). 카운트다운이 없으면(싱글) -1 → "-" 로 남긴다.
        /// </param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void ReportResultShown(string className, bool isWin, string cause = "Normal", int countdownSeconds = -1)
        {
            try
            {
                if (!s_matchActive) return;
                s_resultShown = true;
                // [2026-11-26 DiagRev=2] IsWin 뒤에 Cause · Countdown 을 붙였다(Plan 5절 「ResultShown(기존 확장)」).
                //   IsWin 키는 같은 글자·같은 자리 그대로다(DiagRev=1 로그와 같은 검색어로 잡힌다).
                GameLog.Dev.Info("Network", className, MessagePrefix + " ResultShown",
                                 Keys("ResultShown", "IsWin=" + (isWin ? "True" : "False")
                                     + ", Cause=" + (string.IsNullOrEmpty(cause) ? None : cause)
                                     + ", Countdown=" + (countdownSeconds >= 0
                                         ? countdownSeconds.ToString(CultureInfo.InvariantCulture) : None)));
            }
            catch (Exception)
            {
                // 측정 실패는 게임에 영향을 주면 안 되므로 조용히 버린다.
                // (여기서 다시 로그를 쓰다 같은 예외가 나면 결과 화면 흐름까지 번질 수 있다.)
            }
        }

        // ====================================================================
        // 관찰자(InGameDisconnectObserver) 전용 상태 갱신 — 개발 빌드에서만 불린다
        // ====================================================================

        /// <summary>경기 시작 — 집계를 0 으로 돌리고 역할·기준 시각을 정한다. 반환값은 MatchNo.</summary>
        internal static int BeginMatch(bool isServer)
        {
            s_matchNo++;
            s_matchActive = true;
            s_matchStartRealtime = Time.realtimeSinceStartupAsDouble;
            s_role = isServer ? "Host" : "Client"; // 기존 Role= 표기(NetworkCombatController)와 같은 대소문자
            s_disconnectNo = 0;
            s_connectNo = 0;
            s_pauseNo = 0;
            s_transportFailureNo = 0;
            s_resultShown = false;
            s_hasPeerDisconnect = false;
            s_lastPeerDisconnectRealtime = 0d;
            s_lastDisconnectedPeerId = 0UL;
            s_hasResume = false;
            s_lastResumeRealtime = 0d;
            return s_matchNo;
        }

        /// <summary>경기 끝(요약을 남긴 뒤) — 이후 줄은 MatchSec=- / Role=- 가 된다.</summary>
        internal static void EndMatch()
        {
            s_matchActive = false;
            s_role = None;
        }

        /// <summary>끊김 콜백 1회 집계. 상대(IsSelf=False)면 시각·ClientId 를 기억한다. 반환값은 DisconnectNo.</summary>
        internal static int NotePeerDisconnected(ulong clientId, bool isSelf)
        {
            s_disconnectNo++;
            if (!isSelf)
            {
                s_hasPeerDisconnect = true;
                s_lastPeerDisconnectRealtime = Time.realtimeSinceStartupAsDouble;
                s_lastDisconnectedPeerId = clientId;
            }
            return s_disconnectNo;
        }

        /// <summary>연결 콜백 1회 집계. 반환값은 ConnectNo.</summary>
        internal static int NotePeerConnected() => ++s_connectNo;

        /// <summary>
        /// 이 ClientId 가 마지막으로 끊긴 상대와 같은가 — "True"/"False", 끊긴 적이 없으면 "-".
        /// NGO 는 재접속한 클라이언트에 새 ClientId 를 줄 수 있어(미확인), 그 여부를 로그로 보기 위함.
        /// </summary>
        internal static string SameAsLastDisconnectedText(ulong clientId)
        {
            if (!s_hasPeerDisconnect) return None;
            return clientId == s_lastDisconnectedPeerId ? "True" : "False";
        }

        /// <summary>전송 계층 실패 1회 집계. 반환값은 TransportFailureNo.</summary>
        internal static int NoteTransportFailure() => ++s_transportFailureNo;

        /// <summary>앱 일시정지 1회 집계. 반환값은 PauseNo.</summary>
        internal static int NotePaused() => ++s_pauseNo;

        /// <summary>앱 복귀 시각 기억(PeerDisconnected 의 SinceResumeSec 계산용).</summary>
        internal static void NoteResumed()
        {
            s_hasResume = true;
            s_lastResumeRealtime = Time.realtimeSinceStartupAsDouble;
        }

        /// <summary>경기 요약(MatchSummary) 줄의 집계 키 문자열.</summary>
        internal static string SummaryText()
        {
            return "DisconnectNo=" + s_disconnectNo.ToString(CultureInfo.InvariantCulture)
                + ", ConnectNo=" + s_connectNo.ToString(CultureInfo.InvariantCulture)
                + ", PauseNo=" + s_pauseNo.ToString(CultureInfo.InvariantCulture)
                + ", TransportFailureNo=" + s_transportFailureNo.ToString(CultureInfo.InvariantCulture)
                + ", ResultShown=" + (s_resultShown ? "True" : "False");
        }

        /// <summary>
        /// 정적 값 초기화. 에디터에서 「도메인 리로드 없이 플레이」를 켜면 정적 값이 이전 플레이에서 남으므로
        /// 관찰자 파일의 [RuntimeInitializeOnLoadMethod] 가 플레이 시작 때 부른다(개발 빌드 전용).
        /// </summary>
        internal static void ResetForNewRuntime()
        {
            s_matchNo = 0;
            EndMatch();
        }
    }
}
