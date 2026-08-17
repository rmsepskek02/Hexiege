using System;
using System.IO;
using UnityEngine;

// 사용 예:
// (두 번째 인자는 "이 로그가 무엇을 위한 것인가" = 목적 문자열이며, 파일 헤더 1줄째에 그대로 들어간다)
// RuntimeLogger.BeginSession("Assets/_Project/Docs/_Logs/2026-06-25/07_25_matchmaking-debug", "매치메이킹 디버그");
// RuntimeLogger.Log(LogLevel.Info, "Network", "NetworkGameManager", "StartMatchmakingAsync 진입", $"IsListening={val}");
// RuntimeLogger.EndSession();

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 로그 심각도(중요도) 단계를 나타내는 열거형.
    /// - Info: 일반적인 흐름 추적용 정보
    /// - Warn: 비정상은 아니지만 주의가 필요한 상황
    /// - Error: 실제 오류 상황
    /// </summary>
    public enum LogLevel
    {
        Info,
        Warn,
        Error
    }

    /// <summary>
    /// 런타임 디버그 로그를 "콘솔 + 파일"에 동시에 기록하는 정적(static) 유틸리티 클래스.
    ///
    /// 동작 요약:
    /// - 에디터(UNITY_EDITOR)에서는 지정한 폴더에 텍스트 파일로도 로그를 남긴다.
    /// - 실기기(빌드)에서는 파일 쓰기를 전혀 하지 않고, Debug.Log 계열로만 출력한다.
    ///   (Android Logcat 등에서 동일한 형식으로 확인 가능)
    ///
    /// 보안 주의:
    /// - PlayerId, 인증 토큰, 세션 키 등 "민감한 데이터"는 로그(message/data)에 절대 출력하지 말 것.
    ///   로그 파일은 개발자 간 공유되거나 외부로 유출될 수 있다.
    ///
    /// 스레드 주의:
    /// - 이 클래스는 메인 스레드(유니티 게임 루프)에서의 호출을 전제로 한다.
    ///   여러 스레드에서 동시에 호출하는 용도가 아니다.
    /// </summary>
    public static class RuntimeLogger
    {
#if UNITY_EDITOR
        // 현재 열려 있는 로그 파일에 대한 쓰기 스트림.
        // null이면 아직 BeginSession이 호출되지 않았거나 EndSession으로 닫힌 상태다.
        // (이 필드는 에디터에서만 사용하므로 #if UNITY_EDITOR 안에 둔다)
        private static StreamWriter _writer;
#endif

        /// <summary>
        /// 디버그 로그 세션을 시작한다.
        /// 에디터에서는 지정한 폴더에 로그 파일을 만들고(또는 이어쓰기) 헤더를 기록한다.
        /// 실기기에서는 아무 동작도 하지 않는다(파일을 만들지 않음).
        /// </summary>
        /// <param name="folderPath">로그 파일을 저장할 폴더 경로. 없으면 자동으로 생성한다.</param>
        /// <param name="purpose">
        /// 이 로그 세션의 목적(또는 작업명). 파일 헤더 1줄째 "=== {purpose} ===" 에 그대로 들어간다.
        /// 예: "에디터 상시 런타임 로그", "유닛 사망 NGO 버그 픽스 검증 로그"
        /// (LogRules.md 1.4 「파일 헤더」 — 1줄째는 고정 문자열이 아니라 작업명/목적이어야 한다)
        /// </param>
        public static void BeginSession(string folderPath, string purpose)
        {
#if UNITY_EDITOR
            try
            {
                // 중복 호출 안전장치:
                // 이미 열려 있는 스트림이 있으면 먼저 깔끔하게 닫고 새로 연다.
                EndSession();

                // 폴더가 아직 없다면 만들어 준다. (중간 경로까지 모두 생성됨)
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // 파일명은 항상 "RuntimeLog.txt" 하나다. 역할(host/client)을 파일명에 넣지 않는다.
                //
                // 왜 역할별로 파일을 나누지 않는가 (LogRules.md 1.10):
                //   이 클래스의 파일 쓰기 코드는 전부 #if UNITY_EDITOR 안에 있어
                //   빌드(실기기)는 로그 파일을 아예 만들지 않는다.
                //   즉 파일을 쓰는 프로세스는 언제나 에디터 1개뿐이라 파일이 서로 충돌할 수 없고,
                //   파일명을 나눌 이유가 없다.
                //   역할은 파일명이 아니라 로그 라인의 "Role=host" 같은 key=value 필드로 남긴다.
                const string FileName = "RuntimeLog.txt";
                string fullPath = Path.Combine(folderPath, FileName);

                // append: true  → 기존 파일이 있으면 이어쓰기(기록 보존)
                // autoFlush: true → 매 줄 작성 직후 디스크에 즉시 기록.
                //                   에디터가 갑자기 멈추거나 크래시 나도 로그가 유실되지 않는다.
                _writer = new StreamWriter(fullPath, append: true) { AutoFlush = true };

                // ── 헤더 기록 (LogRules.md 1.4 「파일 헤더」) ──
                //   1줄째: === [작업명 또는 로그 목적] ===   ← 호출자가 넘긴 purpose
                //   2줄째: === [시각의 종류]: YYYY-MM-DD HH:MM:SS ===
                //   그다음: 빈 줄 1줄 (헤더가 여기서 끝난다는 표시)
                //
                //   purpose 가 비어 있으면 헤더 1줄째가 "===  ===" 처럼 빈 칸이 되어
                //   규정 형식이 깨지므로, 그런 경우에만 최소한의 기본 문구로 대체한다.
                //   (ScriptableObject/Inspector 값처럼 잘못 들어올 수 있는 값은 항상 방어한다)
                string headerTitle = string.IsNullOrWhiteSpace(purpose) ? "런타임 로그" : purpose;

                _writer.WriteLine($"=== {headerTitle} ===");
                _writer.WriteLine($"=== 세션 시작: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");

                // 헤더와 본문(로그 라인)을 시각적으로도, 파싱상으로도 갈라 주는 빈 줄.
                // 본문 줄은 "[" 로 시작하므로, 빈 줄까지가 헤더라는 규칙만으로 경계가 명확해진다.
                _writer.WriteLine();
            }
            catch (Exception e)
            {
                // 파일 관련 작업은 권한/경로 문제로 실패할 수 있다.
                // 로깅 유틸리티 때문에 게임이 멈추면 안 되므로 예외는 콘솔 경고로만 알리고 넘어간다.
                _writer = null;
                Debug.LogWarning($"[RuntimeLogger] 세션 시작 실패: {e.Message}");
            }
#endif
        }

        /// <summary>
        /// 로그 한 줄을 기록한다.
        /// - 콘솔(Debug.Log 계열)에는 항상 출력된다(에디터 + 실기기 공통).
        /// - 에디터에서 BeginSession으로 파일이 열려 있으면 파일에도 같은 형식으로 이어쓴다.
        /// - BeginSession 없이 호출해도 예외 없이 콘솔 출력만 수행된다.
        /// </summary>
        /// <param name="level">로그 심각도(Info/Warn/Error).</param>
        /// <param name="system">상위 시스템/도메인 이름. 예: "Network"</param>
        /// <param name="className">로그를 남기는 클래스 이름. 예: "NetworkGameManager"</param>
        /// <param name="message">사람이 읽을 메시지 본문.</param>
        /// <param name="data">선택. "key=value, key=value" 형태의 추가 데이터. 민감 정보 금지.</param>
        public static void Log(LogLevel level, string system, string className, string message, string data = null)
        {
            // 1) 공통 로그 문자열을 조립한다.
            // 형식: [HH:MM:SS.ms] [LEVEL] [System/Class] 메시지 | data
            // 시간 표기에서 "fff"는 밀리초 3자리를 의미한다.
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string levelText = LevelToText(level);

            string line = $"[{timestamp}] [{levelText}] [{system}/{className}] {message}";

            // data가 실제로 들어온 경우에만 " | data"를 덧붙인다.
            if (!string.IsNullOrEmpty(data))
            {
                line += $" | {data}";
            }

#if UNITY_EDITOR
            // 2) 에디터에서 파일 스트림이 열려 있다면 파일에도 같은 줄을 기록한다.
            if (_writer != null)
            {
                try
                {
                    _writer.WriteLine(line);
                }
                catch (Exception e)
                {
                    // 파일 쓰기 실패가 게임 진행을 막지 않도록 경고만 남긴다.
                    Debug.LogWarning($"[RuntimeLogger] 파일 기록 실패: {e.Message}");
                }
            }
#endif

            // 3) 콘솔에는 심각도에 맞는 메서드로 항상 출력한다.
            //    이렇게 하면 에디터 콘솔과 실기기 Logcat에서 모두 동일한 형식으로 보인다.
            switch (level)
            {
                case LogLevel.Warn:
                    Debug.LogWarning(line);
                    break;
                case LogLevel.Error:
                    Debug.LogError(line);
                    break;
                default: // LogLevel.Info
                    Debug.Log(line);
                    break;
            }
        }

        /// <summary>
        /// 디버그 로그 세션을 종료한다.
        /// 에디터에서는 열려 있던 파일 스트림을 닫고 정리한다.
        /// 실기기에서는 아무 동작도 하지 않는다.
        /// 이미 닫혀 있어도(스트림이 null이어도) 안전하게 호출할 수 있다.
        /// </summary>
        public static void EndSession()
        {
#if UNITY_EDITOR
            if (_writer != null)
            {
                try
                {
                    _writer.Flush();  // 혹시 남아 있을 수 있는 버퍼를 마지막으로 비운다.
                    _writer.Dispose(); // 파일 핸들을 닫아 다른 프로그램이 접근할 수 있게 한다.
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[RuntimeLogger] 세션 종료 중 오류: {e.Message}");
                }
                finally
                {
                    // 어떤 경우든 참조를 비워 "닫힌 상태"로 만든다.
                    _writer = null;
                }
            }
#endif
        }

        /// <summary>
        /// LogLevel 열거형을 로그 문자열에 쓸 대문자 텍스트로 변환한다.
        /// (예: LogLevel.Info → "INFO")
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
    }
}
