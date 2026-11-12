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
    /// - Development Android는 고유 파일 기록 성공 후 개발 Info와 명시된 진단 Warning 두 종류의 중복 콘솔을 생략한다.
    /// - 다른 빌드는 기존 Debug.Log 경로를 유지한다. 파일 실패·등록 전/종료 뒤에도 콘솔로 복귀한다.
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

#if UNITY_EDITOR || (UNITY_ANDROID && DEVELOPMENT_BUILD)
        // 현재 열려 있는 로그 파일에 대한 쓰기 스트림.
        // null이면 아직 BeginSession이 호출되지 않았거나 EndSession으로 닫힌 상태다.
        // Development Android도 같은 writer 구현을 사용하며 별도 백그라운드 큐는 두지 않는다.
        private static StreamWriter _writer;
#endif

        /// <summary>
        /// 지금 이 클래스가 **콘솔로 로그를 내보내는 중**인지 여부.
        ///
        /// ── 이 플래그가 왜 필요한가 (초급 개발자용 설명 — 되먹임 차단) ─────────────
        ///   아래 Log() 는 마지막에 Debug.Log / LogWarning / **LogError** 로 콘솔에 출력한다.
        ///   그런데 이 프로젝트는 UnityEngine.Application.logMessageReceived 훅을 걸어
        ///   "엔진이 낸 오류" 를 로그 파일로 끌어온다(LogSessionOwner).
        ///   훅이 Error 종류까지 수집하게 되면서 다음 고리가 생길 수 있다.
        ///
        ///       GameLog.Ops.Error → FileSink → RuntimeLogger.Log → Debug.LogError
        ///         → logMessageReceived 훅 → GameLog.Ops.Error → ... (끝없이 반복)
        ///
        ///   그래서 "지금 나가는 이 오류 줄은 우리 로그 시스템 자신이 만든 것" 임을
        ///   훅이 확실히 알아볼 수 있어야 한다. 그 표식이 이 플래그다.
        ///
        /// ── 왜 GameLog.IsEmitting 만으로는 부족한가 ────────────────────────────
        ///   GameLog 를 거치는 경로는 GameLog.IsEmitting 으로 이미 걸러진다.
        ///   그런데 LogRules.md 1.11 은 축 B 의 「임시」 로그에 대해
        ///   **RuntimeLogger.Log 직접 호출**을 명시적으로 허용한다. 그 경로는 GameLog 를 거치지 않으므로
        ///   GameLog.IsEmitting 이 거짓이라 구멍이 된다.
        ///   → 출력하는 코드 바로 옆(=이 클래스)에 플래그를 두면 두 경로를 한꺼번에 덮는다.
        ///
        /// ── 왜 "직전 줄과 같으면 무시" 로는 못 막는가 ──────────────────────────
        ///   우리가 만드는 줄에는 [HH:mm:ss.fff] 처럼 **밀리초까지 들어간 시각**이 붙는다.
        ///   그래서 같은 사건이라도 문자열이 매번 달라져 "직전과 동일" 비교가 영원히 성립하지 않는다.
        ///
        /// 선례: Hexiege.Application.GameLog.IsEmitting 이 정확히 같은 패턴이다.
        /// </summary>
        public static bool IsEmittingToConsole => _isEmittingToConsole;

        /// <summary>
        /// IsEmittingToConsole 의 실제 저장소.
        /// 이 클래스는 메인 스레드 호출을 전제로 하므로 잠금(lock)을 두지 않는다.
        /// </summary>
        private static bool _isEmittingToConsole;

        /// <summary>
        /// 디버그 로그 세션을 시작한다.
        /// 에디터에서는 지정한 폴더에 로그 파일을 만들고(또는 이어쓰기) 헤더를 기록한다.
        /// 이 에디터 진입점은 실기기에서 동작하지 않는다. 개발 Android는 Owner의 BeginDeviceArchive를 쓴다.
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
                Directory.CreateDirectory(folderPath);

                // 파일명은 항상 "RuntimeLog.txt" 하나다. 역할(host/client)을 파일명에 넣지 않는다.
                //
                // 왜 역할별로 파일을 나누지 않는가 (LogRules.md 1.10):
                //   이 클래스의 파일 쓰기 코드는 전부 #if UNITY_EDITOR 안에 있어
                //   개발 Android는 이 경로 대신 앱 전용 고유 run 폴더를 사용한다.
                //   이 에디터 경로를 쓰는 프로세스는 에디터 1개뿐이라 파일이 서로 충돌할 수 없고,
                //   파일명을 나눌 이유가 없다.
                //   역할은 파일명이 아니라 로그 라인의 "Role=host" 같은 key=value 필드로 남긴다.
                const string FileName = "RuntimeLog.txt";
                string fullPath = Path.Combine(folderPath, FileName);

                // append: true  → 기존 파일이 있으면 이어쓰기(기록 보존)
                // autoFlush: true → 매 줄 작성 직후 디스크에 즉시 기록.
                //                   관리 버퍼의 미기록 tail 위험을 줄이지만 OS/디스크 고장의 무손실 보장은 아니다.
                _writer = OpenWriter(fullPath, false);

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
        /// - 기본적으로 콘솔(Debug.Log 계열)에 출력한다.
        /// - Development Android 파일 성공 시 일반 Info 및 정확히 지정된 진단 Warning 두 종류만 콘솔 중복을 생략한다(Event=는 제외).
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

#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
            // 임시 RuntimeLogger 직접 호출도 같은 파일 세션에 정확히 한 번 기록한다.
            // 이 경로의 기존 스택 생략은 Wave 한 종류뿐이다(ConsoleSink 특례와 별개).
            bool omitStack = level == LogLevel.Info
                && className == Hexiege.Application.WavePresentationDiagnostics.LogClass
                && message == Hexiege.Application.WavePresentationDiagnostics.Prefix;
            bool archived = TryArchiveDeviceRecord(line,
                level == LogLevel.Warn ? LogType.Warning : level == LogLevel.Error ? LogType.Error : LogType.Log,
                omitStack, null);
            if (archived && IsFileOnlyInfo(level == LogLevel.Info, data, null))
            {
                return;
            }
            if (archived && IsFileOnlyDiagnosticWarning(level == LogLevel.Warn, system, className, message, data, null))
            {
                return;
            }
#endif

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

            // 3) 파일 전용으로 반환한 Info/허용된 진단 Warning을 제외하고 심각도대로 콘솔 출력한다.
            //    이렇게 하면 에디터 콘솔과 실기기 Logcat에서 모두 동일한 형식으로 보인다.
            //
            //    ⚠️ 이 구간만 "콘솔 출력 중" 플래그로 감싼다(IsEmittingToConsole 주석 참조).
            //       파일 쓰기(위 2번)는 감싸지 않는다 — 파일 쓰기는 logMessageReceived 훅을
            //       발화시키지 않으므로 플래그 구간을 넓힐 이유가 없다.
            //
            //    ⚠️ try / finally 가 필수인 이유:
            //       Debug.LogError 가 어떤 이유로든 예외를 던졌을 때 플래그가 올라간 채로 남으면,
            //       그 뒤로 훅이 **모든 엔진 오류를 영구히 무시**하게 된다.
            //       즉 로그 시스템이 조용히 죽는다. 그래서 무슨 일이 있어도 finally 에서 내린다.
            //       (GameLog.Emit 의 재진입 가드도 정확히 같은 이유로 finally 를 쓴다.)
#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
            if (DeviceArchiveFallbackRequired)
            {
                EmitDeviceConsole(level, className, message, line);
            }
            else EmitDeviceConsole(level, className, message, line);
        }

        private static void EmitDeviceConsole(LogLevel level, string className, string message, string line)
        {
#endif
            _isEmittingToConsole = true;
            try
            {
                // FileSink의 파일 쓰기는 위에서 이미 완료했다. 기존 finally 가드는 그대로 유지한다.
                if (level == LogLevel.Info
                    && className == Hexiege.Application.WavePresentationDiagnostics.LogClass
                    && message == Hexiege.Application.WavePresentationDiagnostics.Prefix)
                    Debug.LogFormat(LogType.Log,
                        LogOption.NoStacktrace, null, "{0}", line);
                else switch (level)
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
            finally
            {
                _isEmittingToConsole = false;
            }
        }

        /// <summary>
        /// 디버그 로그 세션을 종료한다.
        /// 에디터에서는 열려 있던 파일 스트림을 닫고 정리한다.
        /// Development Android는 파일 footer/Flush와 종료를 처리한다. 다른 실기기 빌드는 파일 동작이 없다.
        /// 이미 닫혀 있어도(스트림이 null이어도) 안전하게 호출할 수 있다.
        /// </summary>
        public static void EndSession()
        {
#if UNITY_EDITOR || (UNITY_ANDROID && DEVELOPMENT_BUILD)
            if (_deviceAttempted && (_deviceOwnsWriter || _writer == null))
            {
                if (_deviceWriting) { _deviceCloseRequested = true; return; }
                CloseDeviceArchive();
                return;
            }
#endif
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

#if UNITY_EDITOR || (UNITY_ANDROID && DEVELOPMENT_BUILD)
        private static StreamWriter OpenWriter(string path, bool createNew)
        {
            var stream = new FileStream(path, createNew ? FileMode.CreateNew : FileMode.Append, FileAccess.Write, FileShare.Read);
            StreamWriter writer = null;
            try
            {
                writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
                writer.AutoFlush = true;
                return writer;
            }
            catch
            {
                // 생성/초기 Flush 실패도 아직 필드에 대입되지 않은 자원을 반드시 회수한다.
                try { writer?.Dispose(); } catch { }
                try { stream.Dispose(); } catch { }
                throw;
            }
        }

        private static bool _deviceAttempted, _deviceActive, _deviceFaulted, _deviceWarned, _deviceOwnsWriter;
        private static bool _deviceWriting, _deviceClosing, _deviceCloseRequested;
        private static long _deviceAttempts, _deviceRecords;
        private static long _deviceDiagnosticFailures;
        private static long _deviceConsoleFallbacks;
        private static string _devicePath;
        internal static bool DeviceArchiveFailed => _deviceFaulted;
        internal static bool DeviceArchiveFallbackRequired => _deviceAttempted && (_deviceFaulted || _deviceWriting || _deviceClosing);
        internal static bool DeviceArchiveActive => _deviceActive && !_deviceWriting && !_deviceClosing;
        internal static bool IsFileOnlyInfo(bool isInfo, string data, Exception exception) => isInfo && exception == null
            && (data == null || !data.StartsWith("Event=", StringComparison.Ordinal));


        // 성공한 archive만 호출자가 적용한다. 원문/Warning 레벨/호출 스택은 파일에 그대로 남긴다.
        internal static bool IsFileOnlyDiagnosticWarning(bool isWarning, string system, string className,
            string message, string data, Exception exception)
        {
            if (!isWarning || exception != null || system != "Network"
                || (data != null && data.StartsWith("Event=", StringComparison.Ordinal))) return false;
            if (className == "UnitAttackShadowObserver")
                return message == "[UAS-ATTACK-SHADOW] presentation-shadow-failure";
            return className == "TimedDamageEffectChannel" && IsPoisonStaleStopMessage(message);
        }

        private static readonly string[] _poisonStopFields = {
            "Stage=", "Match=", "Lifetime=", "Effect=", "Revision=", "Target=", "Source=", "Visual=",
            "Active=", "Start=", "End=", "Now=", "Role=", "Authority=", "Reason=", "AttackKey=", "Writer=" };

        private static bool IsPoisonStaleStopMessage(string message)
        {
            if (message == null) return false;
            ReadOnlySpan<char> remaining = message.AsSpan();
            for (int field = 0; field < _poisonStopFields.Length; field++)
            {
                int separator = remaining.IndexOf(", ".AsSpan());
                bool last = field == _poisonStopFields.Length - 1;
                if (last ? separator >= 0 : separator < 0) return false;
                ReadOnlySpan<char> token = last ? remaining : remaining.Slice(0, separator);
                ReadOnlySpan<char> key = _poisonStopFields[field].AsSpan();
                if (!token.StartsWith(key, StringComparison.Ordinal)) return false;
                ReadOnlySpan<char> value = token.Slice(key.Length);
                if (value.IsEmpty) return false;
                // Record의 scalar에는 공백/개행이 없다. 불명확한 형태는 native 출력을 유지한다.
                for (int i = 0; i < value.Length; i++)
                    if (char.IsWhiteSpace(value[i]) || char.IsControl(value[i])) return false;
                bool valid;
                switch (field)
                {
                    case 0: valid = value.SequenceEqual("Drop".AsSpan()); break;
                    case 1: case 2: case 3: case 4:
                        valid = ulong.TryParse(value, System.Globalization.NumberStyles.None,
                            System.Globalization.CultureInfo.InvariantCulture, out _); break;
                    case 5: case 6:
                        valid = int.TryParse(value, System.Globalization.NumberStyles.AllowLeadingSign,
                            System.Globalization.CultureInfo.InvariantCulture, out _); break;
                    case 7: valid = value.SequenceEqual("Poison".AsSpan()); break;
                    case 8: case 13: valid = value.SequenceEqual("True".AsSpan()) || value.SequenceEqual("False".AsSpan()); break;
                    case 9: case 10: case 11:
                        valid = IsInvariantDiagnosticDouble(value); break;
                    case 12: valid = value.SequenceEqual("Solo".AsSpan()) || value.SequenceEqual("Server".AsSpan())
                            || value.SequenceEqual("Client".AsSpan()); break;
                    case 14: valid = value.SequenceEqual("StaleStopOrClosed".AsSpan()); break;
                    case 15: valid = IsAttackKey(value); break;
                    default: valid = value.SequenceEqual("Damage".AsSpan()); break;
                }
                if (!valid) return false;
                if (!last) remaining = remaining.Slice(separator + 2);
            }
            return true;
        }

        private static bool IsAttackKey(ReadOnlySpan<char> value)
        {
            for (int part = 0; part < 3; part++)
            {
                int colon = value.IndexOf(':');
                if (part == 2 ? colon >= 0 : colon < 0) return false;
                ReadOnlySpan<char> number = part == 2 ? value : value.Slice(0, colon);
                if (!ulong.TryParse(number, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out _)) return false;
                if (part != 2) value = value.Slice(colon + 1);
            }
            return true;
        }

        private static bool IsInvariantDiagnosticDouble(ReadOnlySpan<char> value)
        {
            // Invariant G17이 생성하는 특수값의 정확한 표기만 허용한다.
            if (value.SequenceEqual("NaN".AsSpan()) || value.SequenceEqual("Infinity".AsSpan())
                || value.SequenceEqual("-Infinity".AsSpan())) return true;
            for (int i = 0; i < value.Length; i++)
                if ((value[i] < '0' || value[i] > '9') && value[i] != '.'
                    && value[i] != '-' && value[i] != '+' && value[i] != 'E') return false;
            return double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double number)
                && !double.IsInfinity(number) && !double.IsNaN(number);
        }


        /// <summary>Owner가 앱당 한 번만 연다. 같은 run 폴더/파일을 다시 열거나 덮어쓰지 않는다.</summary>
        internal static void BeginDeviceArchive(string root)
        {
            if (_deviceAttempted) return;
            _deviceAttempted = true;
            try
            {
                if (_writer != null) throw new InvalidOperationException("Another runtime log writer is already open.");
                string run = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture)
                    + "-" + Guid.NewGuid().ToString("N");
                string directory = Path.Combine(root, run);
                if (Directory.Exists(directory)) throw new IOException("Device archive directory already exists.");
                Directory.CreateDirectory(directory);
                _devicePath = Path.Combine(directory, "RuntimeLog.txt");
                _writer = OpenWriter(_devicePath, true);
                _deviceOwnsWriter = true;
#if ENABLE_IL2CPP
                const string backend = "IL2CPP";
#else
                const string backend = "MonoOrEditor";
#endif
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                _writer.WriteLine("=== Android development runtime archive ===");
                _writer.WriteLine("=== DEVICE-ARCHIVE schema=1 session=" + run + " buildGuid=" + UnityEngine.Application.buildGUID
                    + " backend=" + backend + " process=" + process.Id + " utc="
                    + DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture) + " ===");
                _writer.WriteLine();
                _writer.Flush();
                _deviceActive = true;
                // 수집기가 폴더를 찾는 1회 식별 헤더. UID/토큰/원문 로그는 출력하지 않는다.
                EmitDeviceDiagnostic("[DEVICE-LOG-ARCHIVE] schema=1 path=" + _devicePath + " buildGuid=" + UnityEngine.Application.buildGUID);
            }
            catch (Exception error) { FailDeviceArchive(error, "open"); CloseDeviceArchive(); }
        }

        internal static string CaptureDeviceCallerStack(LogType type, bool omitStack)
        {
            if (omitStack || UnityEngine.Application.GetStackTraceLogType(type) == StackTraceLogType.None) return null;
            return StackTraceUtility.ExtractStackTrace();
        }

        /// <summary>
        /// 완성된 원문을 공유 writer에 동기 기록한다. 성공한 뒤에만 console 중복을 생략할 수 있다.
        /// 길이/seq/END 경계로 불완전 tail을 찾는다. AutoFlush는 OS 강제 종료/디스크 고장의 무손실 보장이 아니다.
        /// </summary>
        internal static bool TryArchiveDeviceRecord(string line, LogType type, bool omitStack, Exception exception)
        {
            if (!DeviceArchiveActive || _writer == null)
            {
                if (DeviceArchiveFallbackRequired) _deviceConsoleFallbacks++;
                return false;
            }
            bool saved = false;
            _deviceWriting = true;
            long sequence = ++_deviceAttempts;
            try
            {
                string stack = CaptureDeviceCallerStack(type, omitStack);
                string payload = line + Environment.NewLine;
                if (!string.IsNullOrEmpty(stack)) payload += stack + Environment.NewLine;
                if (exception != null) payload += exception + Environment.NewLine;
                string record = "=== RECORD seq=" + sequence + " utf8Bytes=" + System.Text.Encoding.UTF8.GetByteCount(payload)
                    + " ===" + Environment.NewLine + payload + "=== RECORD-END seq=" + sequence + " ===" + Environment.NewLine;
                {
                    _writer.Write(record);
                    _writer.Flush();
                }
                _deviceRecords++;
                saved = true;
            }
            catch (Exception error) { FailDeviceArchive(error, "record"); }
            finally
            {
                _deviceWriting = false;
                if (_deviceFaulted || _deviceCloseRequested) CloseDeviceArchive();
            }
            // Close/flush 재진입에서 고장이 확인된 경우 현재 행도 기존 console 경로로 돌린다.
            bool success = saved && !_deviceFaulted;
            if (!success) _deviceConsoleFallbacks++;
            return success;
        }

        private static void FailDeviceArchive(Exception error, string stage)
        {
            _deviceFaulted = true; _deviceActive = false;
            if (_deviceWarned) return;
            _deviceWarned = true;
            bool prior = _isEmittingToConsole;
            _isEmittingToConsole = true;
            try
            {
                // 파일 일부가 쓰인 뒤 실패하면 같은 행이 Logcat에도 있을 수 있다. seq/기록 수로 그 경계를 남긴다.
                Debug.LogWarning("[RuntimeLogger] device archive unavailable; console fallback. stage=" + stage
                    + ", error=" + error.GetType().Name + ", attempts=" + _deviceAttempts + ", committed=" + _deviceRecords);
            }
            catch { _deviceDiagnosticFailures++; } // 실패 경고의 외부 logHandler 오류가 원래 행의 fallback을 막으면 안 된다.
            finally { _isEmittingToConsole = prior; }
        }

        private static void EmitDeviceDiagnostic(string line)
        {
            bool prior = _isEmittingToConsole;
            _isEmittingToConsole = true;
            try { Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", line); }
            catch { _deviceDiagnosticFailures++; } // 진단 자체의 오류를 다시 로그하여 재귀하지 않는다.
            finally { _isEmittingToConsole = prior; }
        }

        private static void CloseDeviceArchive()
        {
            if (_deviceClosing) return;
            _deviceActive = false;
            _deviceCloseRequested = false;
            if (!_deviceOwnsWriter) return;
            var writer = _writer;
            _writer = null;
            _deviceOwnsWriter = false;
            if (writer == null) return;
            _deviceClosing = true;
            bool flushed = false, disposed = false;
            try
            {
                if (!_deviceFaulted)
                {
                    writer.WriteLine("=== DEVICE-ARCHIVE-END attempts=" + _deviceAttempts + " committed=" + _deviceRecords
                        + " diagnosticFailures=" + _deviceDiagnosticFailures + " consoleFallbacks=" + _deviceConsoleFallbacks + " recordsFlushed=True ===");
                    writer.Flush();
                    flushed = true;
                }
            }
            catch (Exception error) { FailDeviceArchive(error, "close-flush"); }
            finally
            {
                try { writer.Dispose(); disposed = true; }
                catch (Exception error) { FailDeviceArchive(error, "close-dispose"); }
                _deviceClosing = false;
            }
            // 파일 footer는 Flush까지만 증명한다. Dispose까지 성공한 별도 receipt를 Logcat에 남긴다.
            if (flushed && disposed && !_deviceFaulted)
                EmitDeviceDiagnostic("[DEVICE-LOG-ARCHIVE-END] closed=True committed=" + _deviceRecords
                    + " consoleFallbacks=" + _deviceConsoleFallbacks + " path=" + _devicePath);
        }
#endif

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
