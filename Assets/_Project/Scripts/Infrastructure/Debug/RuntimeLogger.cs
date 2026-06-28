using System;
using System.IO;
using UnityEngine;

// 사용 예:
// RuntimeLogger.BeginSession("Assets/_Project/Docs/_Logs/2026-06-25/07_25_matchmaking-debug", "host");
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
        /// <param name="role">"host" 또는 "client". 파일명(RuntimeLog_host.txt / RuntimeLog_client.txt)을 결정한다.</param>
        public static void BeginSession(string folderPath, string role)
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

                // role 값에 따라 파일명을 결정한다.
                // "host"가 아니면 모두 client 파일로 취급한다.
                string fileName = role == "host" ? "RuntimeLog_host.txt" : "RuntimeLog_client.txt";
                string fullPath = Path.Combine(folderPath, fileName);

                // append: true  → 기존 파일이 있으면 이어쓰기(기록 보존)
                // autoFlush: true → 매 줄 작성 직후 디스크에 즉시 기록.
                //                   에디터가 갑자기 멈추거나 크래시 나도 로그가 유실되지 않는다.
                _writer = new StreamWriter(fullPath, append: true) { AutoFlush = true };

                // 세션을 구분하기 쉽도록 헤더 두 줄을 기록한다.
                _writer.WriteLine("=== RuntimeLogger 디버그 세션 ===");
                _writer.WriteLine($"=== 세션 시작: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
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
