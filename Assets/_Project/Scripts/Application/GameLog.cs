// ============================================================================
// GameLog.cs
// 프로젝트 전역 로그 진입점(facade). 모든 진단/운영 로그는 이 클래스를 통과한다.
//
// ── 왜 Application 레이어에 두는가 ────────────────────────────────────────
//   운영 로그는 Application 을 포함한 거의 모든 레이어에서 호출된다.
//   그런데 "Application → Infrastructure 직접 참조 금지" 규칙이 있으므로,
//   facade 를 Infrastructure 에 두면 레이어 역전이 발생한다.
//   같은 이유로 이미 Application 에 존재하는 정적 홀더 NetworkContext 와 동일한 위치다.
//
// ── 두 축이 메서드 구조로 드러난다 (LogRules.md 1.2 "두 축 — 심각도와 존속") ──
//   축 A(심각도)  → 메서드 이름   : Info / Warn / Error
//   축 B(존속)    → 중첩 클래스   : Ops(운영) / Dev(개발)
//
//     GameLog.Ops.Error(LogEvent.UnhandledException, "Network", nameof(LobbyManager), "메시지", "key=value");
//     GameLog.Dev.Warn("UI", nameof(ProductionPanelUI), "메시지");
//
//   축을 하나만 정하고 로그를 쓰는 것은 규칙 위반이다(LogRules.md 1.14 금지사항 2).
//   이 구조에서는 애초에 Ops 인지 Dev 인지를 고르지 않으면 코드를 쓸 수 없으므로,
//   "축 B 를 깜빡한다"는 실수 자체가 성립하지 않는다.
//
// ── 두 축의 차이가 코드로 강제되는 지점 ───────────────────────────────────
//   1) Ops 는 LogEvent 를 **필수 인자**로 받고, Dev 는 아예 받지 않는다.
//      → "운영 로그에 이벤트 키 생략"이 컴파일 단계에서 불가능해진다(LogRules.md 1.5).
//   2) Dev 메서드에만 [Conditional] 두 개가 붙어 릴리스 빌드에서 통째로 사라진다.
//      Ops 에는 붙이지 않는다 — 릴리스에서 살아 있어야 하기 때문이다(LogRules.md 1.7).
//
// 규격 출처: Assets/_Project/Docs/LogRules.md (1.4 형식 / 1.5 이벤트 키 / 1.6 민감 데이터 /
//                                             1.7 릴리스 스트리핑 / 1.8 sink 구조 / 1.9 예외 처리)
//
// 스레드 주의:
//   이 클래스는 메인 스레드(유니티 게임 루프)에서의 호출을 전제로 한다.
//   RuntimeLogger 와 동일한 전제이며, sink 목록과 재진입 플래그를 락 없이 다룬다.
//
// Application 레이어 — Infrastructure 참조 없음(UnityEngine.Debug 는 폴백 출력에만 사용).
// ============================================================================

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Hexiege.Application
{
    /// <summary>
    /// 전역 로그 facade. 등록된 모든 sink 로 로그를 전달한다.
    /// sink 가 하나도 없으면 콘솔로 폴백한다(로그가 조용히 사라지지 않도록).
    /// </summary>
    public static class GameLog
    {
        // ====================================================================
        // sink 목록
        // ====================================================================

        /// <summary>
        /// 등록된 sink 목록. GameBootstrapper 가 초기화 시 채운다(LogRules.md 1.8 sink 구조).
        /// 정적(static)인 이유: 로그는 인스턴스를 주입받기 어려운 자리
        /// (정적 메서드, 예외 훅, 초기화 이전 코드)에서도 남길 수 있어야 하기 때문이다.
        /// </summary>
        private static readonly List<ILogSink> _sinks = new List<ILogSink>();

        /// <summary>
        /// 현재 sink 로 로그를 내보내는 중인지 여부(재진입 가드).
        /// 자세한 이유는 <see cref="Emit"/> 주석 참조.
        /// </summary>
        private static bool _isEmitting;

        /// <summary>
        /// 지금 GameLog 가 sink 로 로그를 내보내는 중인지.
        /// 전역 예외 훅(UnityEngine.Application.logMessageReceived)이
        /// "이 로그는 우리 자신이 방금 만든 것"임을 판별하는 데 쓴다.
        /// </summary>
        public static bool IsEmitting => _isEmitting;

        /// <summary>현재 등록된 sink 개수(0이면 콘솔 폴백으로 동작).</summary>
        public static int SinkCount => _sinks.Count;

        /// <summary>
        /// sink 를 등록한다. 조합 루트(GameBootstrapper)만 호출한다.
        /// 같은 인스턴스를 두 번 등록하면 로그가 두 줄로 남으므로 중복 등록은 무시한다.
        /// </summary>
        /// <param name="sink">등록할 sink. null 은 무시된다.</param>
        public static void AddSink(ILogSink sink)
        {
            if (sink == null) return;
            if (_sinks.Contains(sink)) return;
            _sinks.Add(sink);
        }

        /// <summary>
        /// 등록된 sink 를 해제한다. 씬 종료 시 자기가 등록한 sink 만 빼도록 사용한다.
        /// </summary>
        /// <param name="sink">해제할 sink. 목록에 없으면 아무 일도 하지 않는다.</param>
        public static void RemoveSink(ILogSink sink)
        {
            if (sink == null) return;
            _sinks.Remove(sink);
        }

        /// <summary>
        /// 모든 sink 등록을 해제한다.
        /// 에디터의 도메인 리로드/씬 재진입으로 이전 세션의 sink 가 남아 있을 때
        /// (이미 닫힌 파일을 붙잡고 있는 sink 등) 깨끗한 상태에서 다시 등록하기 위해 쓴다.
        /// </summary>
        public static void ClearSinks()
        {
            _sinks.Clear();
        }

        // ====================================================================
        // 축 B — 운영(Ops) : 릴리스 빌드에도 남는다
        // ====================================================================

        /// <summary>
        /// 운영 로그(축 B = 운영). 릴리스 빌드에 **남는다.** 나중에 서버 수집 대상이 된다.
        ///
        /// 언제 운영인가(LogRules.md 1.2 축 B) — 아래 두 질문이 모두 "예"일 때만:
        ///   1. 플레이어 기기에서만 벌어지는가(개발자가 에디터에서 재현할 수 없는가)?
        ///   2. 이 로그가 없으면 원인을 추적할 수단이 없는가(UI 통지·다른 로그로 대체 불가능한가)?
        ///
        /// ⚠️ 이 클래스의 메서드에는 [Conditional] 을 붙이지 않는다.
        ///    붙이면 릴리스 빌드에서 호출이 통째로 사라져, 정작 필요한 플레이어 기기에서
        ///    아무 기록도 남지 않는다(LogRules.md 1.7 / 1.14 금지사항 7).
        /// </summary>
        public static class Ops
        {
            /// <summary>운영 로그 — Info(의도된 흐름 중 지표로 남길 가치가 있는 지점).</summary>
            /// <param name="logEvent">이벤트 키. 집계의 기본 단위(LogRules.md 1.5).</param>
            /// <param name="system">시스템 영역. 예: "Network".</param>
            /// <param name="className">클래스 이름. 예: nameof(LobbyManager).</param>
            /// <param name="message">사람이 읽을 메시지.</param>
            /// <param name="data">"key=value, key=value" 구조화 필드. 없으면 생략.</param>
            public static void Info(LogEvent logEvent, string system, string className, string message, string data = null)
            {
                Emit(LogLevel.Info, logEvent, system, className, message, data, null);
            }

            /// <summary>운영 로그 — Warn(대체 경로로 복구되었지만 기록이 필요한 지점).</summary>
            /// <param name="logEvent">이벤트 키(LogRules.md 1.5).</param>
            /// <param name="system">시스템 영역.</param>
            /// <param name="className">클래스 이름.</param>
            /// <param name="message">사람이 읽을 메시지.</param>
            /// <param name="data">"key=value" 구조화 필드. 없으면 생략.</param>
            public static void Warn(LogEvent logEvent, string system, string className, string message, string data = null)
            {
                Emit(LogLevel.Warn, logEvent, system, className, message, data, null);
            }

            /// <summary>
            /// 운영 로그 — Warn + 예외.
            /// 삼킨 예외(catch 후 폴백)에 쓴다 — 로그가 없으면 실패했다는 사실 자체가 사라진다
            /// (LogRules.md 1.3 분류 원칙 4).
            /// </summary>
            /// <param name="logEvent">이벤트 키.</param>
            /// <param name="system">시스템 영역.</param>
            /// <param name="className">클래스 이름.</param>
            /// <param name="message">사람이 읽을 메시지.</param>
            /// <param name="exception">함께 남길 예외 객체(e.Message 문자열로 눌러 담지 말 것).</param>
            /// <param name="data">"key=value" 구조화 필드. 없으면 생략.</param>
            public static void Warn(LogEvent logEvent, string system, string className, string message, Exception exception, string data = null)
            {
                Emit(LogLevel.Warn, logEvent, system, className, message, data, exception);
            }

            /// <summary>운영 로그 — Error(복구 경로가 없는 실패).</summary>
            /// <param name="logEvent">이벤트 키.</param>
            /// <param name="system">시스템 영역.</param>
            /// <param name="className">클래스 이름.</param>
            /// <param name="message">사람이 읽을 메시지.</param>
            /// <param name="data">"key=value" 구조화 필드. 없으면 생략.</param>
            public static void Error(LogEvent logEvent, string system, string className, string message, string data = null)
            {
                Emit(LogLevel.Error, logEvent, system, className, message, data, null);
            }

            /// <summary>운영 로그 — Error + 예외.</summary>
            /// <param name="logEvent">이벤트 키.</param>
            /// <param name="system">시스템 영역.</param>
            /// <param name="className">클래스 이름.</param>
            /// <param name="message">사람이 읽을 메시지.</param>
            /// <param name="exception">함께 남길 예외 객체.</param>
            /// <param name="data">"key=value" 구조화 필드. 없으면 생략.</param>
            public static void Error(LogEvent logEvent, string system, string className, string message, Exception exception, string data = null)
            {
                Emit(LogLevel.Error, logEvent, system, className, message, data, exception);
            }
        }

        // ====================================================================
        // 축 B — 개발(Dev) : 릴리스 빌드에서 컴파일 단계에 사라진다
        // ====================================================================

        /// <summary>
        /// 개발 로그(축 B = 개발). 에디터 · 개발 빌드 전용.
        ///
        /// ── [Conditional] 을 왜 두 개 붙이는가 (LogRules.md 1.7) ──────────────
        ///   여러 개의 [Conditional] 은 **OR** 로 동작한다.
        ///   즉 "UNITY_EDITOR 이거나 DEVELOPMENT_BUILD 이면 살아 있고, 그 외에는 사라진다".
        ///   한쪽만 붙이면 반드시 구멍이 생긴다:
        ///     · UNITY_EDITOR 만      → 실기기 개발 빌드에서 로그가 통째로 사라진다.
        ///                              (기기에 올려도 Logcat 에 아무것도 안 나온다)
        ///     · DEVELOPMENT_BUILD 만 → 에디터에서 로그가 안 보인다.
        ///                              (이 심볼은 에디터에 정의되어 있지 않다)
        ///
        /// ── if (Debug.isDebugBuild) 로 감싸는 것과 무엇이 다른가 ───────────────
        ///   런타임 분기는 호출이 그대로 남고 **인자가 먼저 평가된 뒤** 조건을 검사한다.
        ///   즉 $"unit={id}, hp={hp}" 같은 문자열 보간이 릴리스에서도 매 호출마다 실행된다.
        ///   [Conditional] 은 컴파일러가 **호출과 인자 평가까지 통째로 제거**하므로 비용이 0이다.
        ///   프레임마다 도는 코드에 개발 로그를 넣을 수 있는 것은 이 차이 때문이다.
        ///
        /// ⚠️ [Conditional] 이 붙는 메서드는 반환형이 반드시 void 여야 한다(C# 언어 제약).
        ///    호출이 통째로 지워지므로 돌려줄 값이 존재할 수 없기 때문이다.
        ///
        /// ⚠️ Dev 는 LogEvent 를 받지 않는다. 개발 로그는 서버 전송 대상이 아니므로
        ///    집계 키가 필요 없다(LogRules.md 1.5 "적용 범위: 운영 로그만").
        /// </summary>
        public static class Dev
        {
            /// <summary>개발 로그 — Info(흐름 추적).</summary>
            /// <param name="system">시스템 영역. 예: "Combat".</param>
            /// <param name="className">클래스 이름. 예: nameof(UnitCombatUseCase).</param>
            /// <param name="message">사람이 읽을 메시지.</param>
            /// <param name="data">"key=value" 구조화 필드. 없으면 생략.</param>
            [System.Diagnostics.Conditional("UNITY_EDITOR")]
            [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
            public static void Info(string system, string className, string message, string data = null)
            {
                Emit(LogLevel.Info, null, system, className, message, data, null);
            }

            /// <summary>개발 로그 — Warn(예상 밖이지만 복구된 상황. 예: Inspector 배선 누락).</summary>
            /// <param name="system">시스템 영역.</param>
            /// <param name="className">클래스 이름.</param>
            /// <param name="message">사람이 읽을 메시지.</param>
            /// <param name="data">"key=value" 구조화 필드. 없으면 생략.</param>
            [System.Diagnostics.Conditional("UNITY_EDITOR")]
            [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
            public static void Warn(string system, string className, string message, string data = null)
            {
                Emit(LogLevel.Warn, null, system, className, message, data, null);
            }

            /// <summary>개발 로그 — Error(개발 환경에서만 의미가 있는 치명적 상황).</summary>
            /// <param name="system">시스템 영역.</param>
            /// <param name="className">클래스 이름.</param>
            /// <param name="message">사람이 읽을 메시지.</param>
            /// <param name="data">"key=value" 구조화 필드. 없으면 생략.</param>
            [System.Diagnostics.Conditional("UNITY_EDITOR")]
            [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
            public static void Error(string system, string className, string message, string data = null)
            {
                Emit(LogLevel.Error, null, system, className, message, data, null);
            }

            /// <summary>개발 로그 — Error + 예외(개발 중 잡아 둔 예외 확인용).</summary>
            /// <param name="system">시스템 영역.</param>
            /// <param name="className">클래스 이름.</param>
            /// <param name="message">사람이 읽을 메시지.</param>
            /// <param name="exception">함께 남길 예외 객체.</param>
            /// <param name="data">"key=value" 구조화 필드. 없으면 생략.</param>
            [System.Diagnostics.Conditional("UNITY_EDITOR")]
            [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
            public static void Error(string system, string className, string message, Exception exception, string data = null)
            {
                Emit(LogLevel.Error, null, system, className, message, data, exception);
            }
        }

        // ====================================================================
        // 민감 데이터 (LogRules.md 1.6)
        // ====================================================================

        /// <summary>
        /// 이메일 주소 패턴. 최종 문자열에서 이 패턴이 발견되면 통째로 치환한다.
        /// RegexOptions.Compiled 를 쓰지 않는 이유: IL2CPP(AOT) 환경에서는 런타임 코드 생성이
        /// 불가능해 어차피 인터프리터로 동작하며, Compiled 지정은 초기화 비용만 늘린다.
        /// </summary>
        private static readonly Regex EmailPattern =
            new Regex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant);

        /// <summary>이메일이 감지됐을 때 자리를 대신할 문자열.</summary>
        private const string EmailRedacted = "[email-redacted]";

        /// <summary>
        /// UID · PlayerId 를 로그에 넣기 전에 익명 해시로 바꾼다(LogRules.md 1.6 — 1단 방어).
        ///
        /// 왜 통째로 지우지 않고 해시로 남기는가:
        ///   라이브 운영에서 답해야 하는 질문은 "누가 겪었나"가 아니라
        ///   "몇 명이 겪었나", "같은 사용자가 반복해서 겪나" 이다.
        ///   두 질문 모두 **안정적인 익명 식별자**가 있어야 답할 수 있다.
        ///   해시는 "같은 사람인지"는 알려 주되 "누구인지"는 알려 주지 않는다 —
        ///   정확히 필요한 만큼만 남기는 방식이다.
        ///
        /// 같은 입력은 언제나 같은 결과를 낸다(솔트를 쓰지 않는 이유).
        /// 솔트를 실행마다 새로 만들면 세션이 바뀔 때마다 같은 사람이 다른 사람으로 보여
        /// "반복해서 겪나"를 판별할 수 없게 된다.
        /// </summary>
        /// <param name="rawId">원본 UID / PlayerId. null 이나 빈 문자열이면 "(none)" 을 돌려준다.</param>
        /// <returns>16자리 16진수 해시 문자열(SHA-256 앞 8바이트).</returns>
        public static string HashId(string rawId)
        {
            if (string.IsNullOrEmpty(rawId)) return "(none)";

            try
            {
                // using 으로 감싸 매번 해제한다. 호출 빈도가 낮은(로그인/오류 경로) 자리이므로
                // 정적 인스턴스를 공유해 스레드 문제를 떠안는 것보다 이 쪽이 안전하다.
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(rawId));

                    // 앞 8바이트(=16진수 16자리)만 쓴다. 충돌 확률이 사실상 0이면서 로그가 짧아진다.
                    StringBuilder sb = new StringBuilder(16);
                    for (int i = 0; i < 8; i++)
                    {
                        sb.Append(hash[i].ToString("x2"));
                    }
                    return sb.ToString();
                }
            }
            catch (Exception)
            {
                // 해시 실패가 게임을 멈추게 해서는 안 된다.
                // 그렇다고 원본을 그대로 흘리면 규칙 위반이므로 고정 문자열로 대체한다.
                return "(hash-failed)";
            }
        }

        /// <summary>
        /// 최종 문자열에서 이메일 패턴을 찾아 차단한다(LogRules.md 1.6 — 2단 방어).
        ///
        /// 왜 2단인가:
        ///   1단(호출부에서 마스킹 헬퍼를 거치기)만 두면 언젠가 누군가 헬퍼를 빠뜨린다.
        ///   사람이 매번 기억해야 하는 규칙은 결국 깨진다. 2단은 그 실수를 마지막에 잡는 그물이다.
        ///
        /// 에디터에서도 똑같이 적용한다. 경로를 갈라 에디터에서만 원본이 보이게 하면
        /// "에디터에선 보였는데 빌드에선 안 보인다"는 혼란이 생기고,
        /// 그 혼란을 없애려고 누군가 마스킹을 임시로 끄는 순간 방어가 무너진다.
        /// </summary>
        /// <param name="text">검사할 문자열(메시지 또는 data).</param>
        /// <returns>이메일이 제거된 문자열.</returns>
        private static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 빠른 경로: '@' 가 아예 없으면 이메일일 수 없으므로 정규식을 돌리지 않는다.
            // 로그 대부분이 여기서 끝나므로 정규식 비용이 실질적으로 발생하지 않는다.
            if (text.IndexOf('@') < 0) return text;

            return EmailPattern.Replace(text, EmailRedacted);
        }

        // ====================================================================
        // 형식 조립 (LogRules.md 1.4)
        // ====================================================================

        /// <summary>
        /// 로그 한 줄을 규정 형식으로 조립한다(LogRules.md 1.4 "형식").
        ///
        ///   [HH:MM:SS.ms] [LEVEL] [System/Class] 메시지 | key=value, key=value
        ///
        /// 형식 규정을 여러 곳에 복사하지 않기 위해 public 으로 열어 둔다.
        /// 콘솔 sink 처럼 "완성된 한 줄"이 필요한 구현체가 이 메서드를 사용한다.
        /// </summary>
        /// <param name="level">심각도.</param>
        /// <param name="system">시스템 영역.</param>
        /// <param name="className">클래스 이름.</param>
        /// <param name="message">메시지 본문.</param>
        /// <param name="data">"key=value" 구조화 필드. 비어 있으면 " | ..." 부분이 붙지 않는다.</param>
        /// <returns>규정 형식의 로그 한 줄.</returns>
        public static string Compose(LogLevel level, string system, string className, string message, string data)
        {
            // "fff" 는 밀리초 3자리를 뜻한다.
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string levelText = LevelToText(level);

            string line = $"[{timestamp}] [{levelText}] [{system}/{className}] {message}";

            if (!string.IsNullOrEmpty(data))
            {
                line += $" | {data}";
            }

            return line;
        }

        /// <summary>
        /// LogLevel 을 로그 문자열에 쓸 대문자 텍스트로 바꾼다(Info → "INFO").
        /// </summary>
        private static string LevelToText(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Warn:
                    return "WARN";
                case LogLevel.Error:
                    return "ERROR";
                default:
                    return "INFO";
            }
        }

        /// <summary>
        /// 이벤트 키 · 예외 타입을 data(구조화 필드) 앞쪽에 병합한다.
        ///
        /// 왜 이벤트 키를 key=value 자리에 넣는가:
        ///   LogRules.md 1.4 는 "key=value 가 곧 서버 전송 데이터"이고
        ///   "이벤트 키가 집계의 기본 단위"라고 규정한다.
        ///   따라서 이벤트 키도 구조화 필드의 하나(Event=...)로 두는 것이
        ///   나중에 서버로 보낼 때 그대로 필드가 된다.
        ///
        /// 예외 타입을 별도 필드로 남기는 이유:
        ///   TimeoutException 300건과 NullReferenceException 300건은 완전히 다른 사건이다.
        ///   메시지 문자열만 남기면 그 구분이 집계에서 사라진다(LogRules.md 1.9).
        /// </summary>
        private static string MergeData(LogEvent? logEvent, string data, Exception exception)
        {
            // 붙일 것이 아무것도 없으면 원본을 그대로 돌려준다(불필요한 할당 방지).
            if (!logEvent.HasValue && exception == null) return data;

            StringBuilder sb = new StringBuilder(64);

            if (logEvent.HasValue)
            {
                // enum 멤버 이름을 변환 없이 그대로 쓴다(LogRules.md 1.5).
                sb.Append("Event=").Append(logEvent.Value.ToString());
            }

            if (exception != null)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append("ExceptionType=").Append(exception.GetType().Name);
            }

            if (!string.IsNullOrEmpty(data))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(data);
            }

            return sb.ToString();
        }

        // ====================================================================
        // 실제 전달 (LogRules.md 1.8)
        // ====================================================================

        /// <summary>
        /// 로그 한 건을 등록된 모든 sink 로 전달한다. Ops/Dev 의 모든 메서드가 여기로 모인다.
        ///
        /// ── 재진입 가드가 없으면 무한 루프가 되는 이유 (가장 위험한 지점) ─────────
        ///   sink 는 최종적으로 UnityEngine.Debug.Log 계열을 호출한다.
        ///   그런데 GameBootstrapper 는 미처리 예외를 잡기 위해
        ///   UnityEngine.Application.logMessageReceived 훅을 걸어 두었고,
        ///   이 훅은 Debug.Log 계열이 호출될 때마다 되불린다.
        ///
        ///       GameLog.Emit → sink → Debug.LogError → logMessageReceived 훅
        ///                    → 훅이 다시 GameLog.Emit 호출 → sink → Debug.LogError → ...
        ///
        ///   즉 로그 한 줄이 무한히 자기 자신을 되먹여 스택이 터지고 앱이 죽는다.
        ///   로그 시스템이 크래시를 "기록하는" 대신 크래시의 "원인"이 되는 것이다.
        ///   그래서 내보내는 중임을 표시하는 플래그를 두고, 그 사이에 들어온 호출은 즉시 버린다.
        ///   (훅 쪽에도 같은 취지의 가드가 있다 — 방어를 한 겹만 두지 않는다.)
        ///
        /// ── sink 마다 개별 try-catch 를 두는 이유 ────────────────────────────
        ///   파일 sink 가 디스크 권한/경로 문제로 예외를 던져도
        ///   콘솔 sink 는 정상 출력되어야 한다.
        ///   무엇보다 **로그 때문에 게임이 멈추면 본말전도다.**
        ///   로그는 게임을 돕는 도구지 게임의 일부가 아니다(LogRules.md 1.8).
        /// </summary>
        private static void Emit(LogLevel level, LogEvent? logEvent, string system, string className,
                                 string message, string data, Exception exception)
        {
            // ── 재진입 가드 ──
            // 이미 sink 로 내보내는 중이라면, 지금 들어온 이 호출은 우리 자신이 만든 로그가
            // 되돌아온 것이다. 즉시 반환해 되먹임 고리를 끊는다.
            if (_isEmitting) return;

            _isEmitting = true;
            try
            {
                // 민감 데이터 차단(LogRules.md 1.6 2단 방어).
                // 최종 한 줄은 "고정 부분(시각/레벨/System/Class) + message + data" 로만 이루어지고
                // 고정 부분에는 이메일이 들어갈 수 없으므로, 이 둘을 검사하면 최종 문자열 전체를
                // 검사한 것과 같다. Compose 이전에 걸러야 모든 sink 가 동일하게 보호된다.
                string safeMessage = Sanitize(message);
                string safeData = Sanitize(MergeData(logEvent, data, exception));

                // sink 가 하나도 없으면 콘솔로 폴백한다(LogRules.md 1.8).
                // 등록 이전에 발생한 로그를 버리면, "로그가 안 나온다"의 원인이
                // "로그 시스템이 아직 안 켜졌다"인 경우 그 사실을 추적할 방법이 사라진다.
                if (_sinks.Count == 0)
                {
                    FallbackToConsole(level, system, className, safeMessage, safeData, exception);
                    return;
                }

                // 인덱스 for 를 쓰는 이유: foreach 의 열거자 할당을 피하고,
                // 혹시 목록이 도중에 바뀌더라도 예외로 죽지 않게 하기 위해서다.
                for (int i = 0; i < _sinks.Count; i++)
                {
                    ILogSink sink = _sinks[i];
                    if (sink == null) continue;

                    try
                    {
                        sink.Write(level, system, className, safeMessage, safeData, exception);
                    }
                    catch (Exception sinkException)
                    {
                        // ⚠️ 여기서 GameLog 를 다시 호출하면 안 된다(재진입).
                        //    콘솔에 직접 경고만 남기고 다음 sink 로 계속 진행한다.
                        UnityEngine.Debug.LogWarning(
                            $"[GameLog] sink 출력 실패({sink.GetType().Name}): {sinkException.Message}");
                    }
                }
            }
            finally
            {
                // 중간에 무슨 일이 있어도 플래그는 반드시 내려야 한다.
                // 내려가지 않으면 그 뒤의 모든 로그가 영구히 차단된다.
                _isEmitting = false;
            }
        }

        /// <summary>
        /// sink 가 하나도 등록되지 않았을 때 콘솔로 직접 출력한다(LogRules.md 1.8).
        /// </summary>
        private static void FallbackToConsole(LogLevel level, string system, string className,
                                              string message, string data, Exception exception)
        {
            string line = Compose(level, system, className, message, data);

            switch (level)
            {
                case LogLevel.Warn:
                    UnityEngine.Debug.LogWarning(line);
                    break;
                case LogLevel.Error:
                    UnityEngine.Debug.LogError(line);
                    break;
                default:
                    UnityEngine.Debug.Log(line);
                    break;
            }

            // 예외는 LogError(e.ToString()) 가 아니라 LogException 으로 내보낸다.
            // 크래시 리포터가 문자열은 "예외"로 인식하지 못해 스택 심볼화가 되지 않기 때문이다
            // (LogRules.md 1.9).
            if (exception != null)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }
    }
}
