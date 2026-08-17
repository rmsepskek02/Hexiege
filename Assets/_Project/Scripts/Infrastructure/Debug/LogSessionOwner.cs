// ============================================================================
// LogSessionOwner.cs
// GameLog 의 sink 등록 · 파일 세션 · 전역 예외 훅을 "앱 전체에 단 하나"로 소유하는 정적 클래스.
//
// ── 왜 이 클래스가 필요한가 (씬과 무관한 로그 수집) ──────────────────────────
//   예전에는 이 배선이 GameBootstrapper 안에 인스턴스 멤버로 들어 있었다.
//   그런데 GameBootstrapper 는 Game.unity 씬에만 붙어 있어서,
//   Login 씬 / Lobby 씬에서 남긴 로그는 sink 가 하나도 없는 상태로 흘러가
//   **파일에 한 글자도 남지 않았다**(LogRules.md 1.8 의 "콘솔 폴백"만 탔다).
//   로그인 실패·매칭 실패는 라이브에서 가장 자주 들어오는 신고인데,
//   정작 그 구간의 기록이 비어 있어 원인을 추적할 수단이 없었다.
//
//   그래서 배선을 이 클래스로 옮기고 규칙을 이렇게 뒤집었다.
//     · 여는 쪽은 여럿 : 가장 먼저 뜨는 부트스트래퍼가 EnsureInitialized() 를 부른다.
//                        두 번째부터는 아무 일도 하지 않는다(멱등).
//     · 닫는 쪽은 하나 : 씬이 언로드될 때가 아니라 **앱/플레이모드가 끝날 때** 1회 닫는다.
//   결과적으로 로그인 → 로비 → 전투가 파일 하나에 한 흐름으로 이어진다.
//
// ── 왜 모든 상태가 static 인가 (이 파일에서 가장 중요한 부분) ────────────────
//   RuntimeLogger 가 여는 파일 스트림은 **전역에 하나뿐**이다.
//   그런데 예전 구조에서는 sink 를 담는 필드가 MonoBehaviour 의 인스턴스 필드였다.
//   씬이 바뀌면 인스턴스가 새로 생기므로, "이미 열었는가?" 라는 판단이
//   **씬 경계를 건너가지 못한다.** 그러면 이런 일이 벌어진다.
//
//     Login 씬 : sink A 를 만들어 세션을 연다        → A 는 "내가 주인" 이라고 기억
//     Game 씬  : 새 인스턴스라 아무것도 모른 채 sink B 를 만들어 또 연다
//                → RuntimeLogger 가 A 의 스트림을 닫고 새로 연다
//     그 뒤     : A 를 쥔 쪽이 EndSession() 을 부르면 **B 의 스트림이 닫힌다**
//                → 파일에 헤더만 남고 본문이 텅 빈다
//
//   이 프로젝트는 2026-08-10 에 실제로 이 사고를 겪었다(LogRules.md 1.10 근거 문단).
//   그때 도입된 FileSink._sessionOwned 가드는 **같은 인스턴스의 이중 열기**만 막을 뿐,
//   서로 다른 인스턴스끼리는 상대의 플래그를 볼 수 없다.
//   → 그래서 판단에 쓰이는 상태를 전부 static 으로 끌어올려,
//     "이미 열려 있는가?" 를 앱 전체에서 단 하나의 값으로 답하게 만든 것이 이 클래스다.
//   (기존 _sessionOwned 가드는 그대로 둔다. 이 클래스는 그 위에 한 겹 더 얹는 것이지 대체가 아니다.)
//
// ── 조합 루트 원칙과 충돌하지 않는 이유 (LogRules.md 1.8 "등록 주체") ─────────
//   이 클래스는 **의존성을 남에게 주입하지 않는다.** 자기가 만든 sink 를 자기 안에 들고 있을 뿐이다.
//   그리고 EnsureInitialized() 를 호출하는 주체는 여전히 Bootstrap 레이어의
//   LoginBootstrapper / GameBootstrapper 뿐이다.
//   즉 "누가 로그를 켰는가" 는 코드에서 그대로 추적된다.
//   (아무도 부르지 않았는데 저절로 켜지는 [RuntimeInitializeOnLoadMethod] 방식을 쓰지 않은 이유다.)
//
// ⚠️ 이름 충돌 주의 — Application
//   이 파일은 namespace Hexiege.Infrastructure 안에 있고, 그 바깥 네임스페이스는 Hexiege 다.
//   Hexiege.Application 네임스페이스가 존재하므로, 그냥 `Application` 이라고 쓰면
//   **네임스페이스 Hexiege.Application** 으로 해석되어 컴파일되지 않는다(이 프로젝트에서 CS0234 3건 발생 이력).
//   → UnityEngine 쪽은 이 파일 전체에서 예외 없이 `UnityEngine.` 을 붙여 완전 수식한다.
//     같은 이유로 `using UnityEngine;` / `using Hexiege.Application;` 을 일부러 넣지 않았다.
//
// ⚠️ 이름 충돌 주의 2 — LogLevel
//   LogLevel 이 Hexiege.Application 과 Hexiege.Infrastructure 양쪽에 존재한다.
//   이 클래스는 LogLevel 을 직접 다루지 않지만, 다루게 된다면 반드시 완전 수식해야 한다.
//
// 규격 출처: Assets/_Project/Docs/LogRules.md
//            (1.8 sink 구조 / 1.9 예외 처리 / 1.10 파일 기록 — 에디터 상시 활성)
//
// Infrastructure 레이어 — Application(안쪽 = GameLog) 참조는 정상 방향이다.
// ============================================================================

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// GameLog 의 sink · 파일 세션 · 전역 예외 훅을 앱 전체에서 단 하나로 소유하는 정적 클래스.
    /// 여는 것은 여러 부트스트래퍼가 호출해도 되고(멱등), 닫는 것은 앱 종료 시 1회 자동으로 일어난다.
    /// </summary>
    public static class LogSessionOwner
    {
        // ====================================================================
        // 상태 — 전부 static 이다 (파일 상단 "왜 모든 상태가 static 인가" 참조)
        // ====================================================================

        /// <summary>
        /// 이미 초기화했는지 여부. EnsureInitialized() 의 멱등성을 보장하는 유일한 판단 근거다.
        ///
        /// 에디터 도메인 리로드와의 관계:
        ///   static 필드는 플레이 모드에 들어갈 때 도메인 리로드로 false 로 되돌아간다.
        ///   그러면 다음 플레이에서 정상적으로 다시 열리므로 이것이 **정상 경로**다.
        ///   (도메인 리로드를 꺼 둔 프로젝트라면 이 값과 RuntimeLogger 의 스트림이 **함께** 살아남으므로
        ///    "열려 있다고 판단했는데 실제로는 닫혀 있다" 는 어긋남은 생기지 않는다.)
        /// </summary>
        private static bool _initialized;

        /// <summary>이미 로그 배선이 켜져 있는지(디버깅·검증용).</summary>
        public static bool IsInitialized => _initialized;

        // 두 필드를 #if 로 갈라 선언하는 이유:
        //   해당 환경에서 쓰이지 않는 필드를 남겨 두면
        //   "대입은 하는데 아무도 읽지 않는 필드" 라는 컴파일러 경고가 뜬다.
        //   등록하는 sink 가 환경마다 하나뿐이므로 필드도 하나만 존재하게 만든다.

#if UNITY_EDITOR
        /// <summary>에디터 전용 파일 로그 sink. 빌드에서는 아예 선언되지 않는다.</summary>
        private static FileSink _logFileSink;
#else
        /// <summary>빌드 전용 콘솔 로그 sink. 에디터에서는 아예 선언되지 않는다.</summary>
        private static ConsoleSink _logConsoleSink;
#endif

        /// <summary>
        /// 전역 예외 훅이 직전에 전달한 예외 메시지(중복 수집 방지용).
        /// 우리가 남긴 로그가 훅으로 되돌아오는 경우를 걸러 낸다.
        /// 자세한 이유는 OnUnityLogMessageReceived() 주석 참조.
        /// </summary>
        private static string _lastHookCondition;

        /// <summary>
        /// 전역 예외 훅이 직전에 전달한 스택 트레이스(중복 수집 방지용).
        /// _lastHookCondition 과 짝으로 비교한다.
        /// </summary>
        private static string _lastHookStackTrace;

        // ====================================================================
        // 공개 API — 부트스트래퍼가 호출하는 유일한 진입점
        // ====================================================================

        /// <summary>
        /// GameLog 에 sink 를 등록하고, 전역 미처리 예외 훅과 종료 훅을 건다.
        /// **이미 켜져 있으면 아무 일도 하지 않는다(멱등).**
        ///
        /// 호출 위치: 각 씬 부트스트래퍼의 Awake() **가장 앞줄**.
        ///   (LoginBootstrapper.Awake / GameBootstrapper.Awake)
        ///   가장 앞에 두는 이유는 그 뒤에 일어나는 모든 초기화를 로그가 관측할 수 있어야 하기 때문이다.
        ///
        /// 어느 씬에서 게임을 시작하든 "가장 먼저 뜬 부트스트래퍼" 하나만 실제로 열고,
        /// 그 뒤 씬 전환으로 다시 호출되는 것들은 전부 첫 줄에서 되돌아간다.
        /// 그래서 씬을 오가도 파일 세션이 끊기지 않는다.
        /// </summary>
        public static void EnsureInitialized()
        {
            // ── 멱등 가드 ──
            //   씬 전환 때마다 다시 여는 것을 막는 핵심 한 줄이다.
            //   이것이 없으면 아래 GameLog.ClearSinks() 가 다시 돌아
            //   앞 씬이 등록해 둔 sink 가 목록에서 통째로 빠지고,
            //   RuntimeLogger 가 앞 세션의 스트림을 닫아 버린다(파일 상단 사고 시나리오).
            if (_initialized) return;

            // 플래그를 "실제 작업 전에" 세운다.
            //   아래 초기화 도중에 로그나 예외가 발생해 이 메서드로 되돌아 들어오더라도
            //   두 번 열리지 않게 하기 위해서다(재진입 방어).
            _initialized = true;

            // 이전 세션의 sink 를 먼저 비운다.
            // 에디터의 도메인 리로드 직후에는 "이미 닫힌 파일" 을 붙잡고 있는
            // 옛 sink 가 목록에 남아 있을 수 있고, 그대로 두면 로그가 조용히 사라진다.
            //
            // ⚠️ 이 호출은 반드시 멱등 가드 **뒤**에 있어야 한다.
            //    가드 앞에 두면 씬이 바뀔 때마다 sink 목록이 비워져 로그가 유실된다.
            Hexiege.Application.GameLog.ClearSinks();

#if UNITY_EDITOR
            // ── 에디터: 파일 sink 하나만 등록한다 ──
            //   FileSink 는 RuntimeLogger 를 재사용하는데, RuntimeLogger 는 파일과 콘솔에
            //   동시에 출력한다. 따라서 ConsoleSink 를 함께 등록하면 콘솔에 같은 줄이 두 번 찍힌다.
            _logFileSink = new FileSink();

            // 세션을 여닫는 주체는 오직 이 클래스다(LogRules.md 1.10 — 세션 소유권).
            //
            // 파일명에 역할(host/client)이 들어가지 않는 이유:
            //   ① 이 시점(Awake)에는 NGO 가 아직 연결되지 않아 host/client 를 알 수 없다.
            //      이 프로젝트의 멀티 테스트 구성은 "에디터 1개 + 실기기 빌드 1개"이고,
            //      어느 쪽이 host 가 될지는 정해져 있지 않다.
            //   ② 애초에 파일명을 나눌 필요가 없다. FileSink 의 동작 전체가 #if UNITY_EDITOR 안에 있어
            //      빌드(실기기)는 로그 파일을 아예 쓰지 않는다.
            //      즉 파일을 쓰는 프로세스는 언제나 에디터 1개뿐이라 파일이 충돌할 수 없다.
            //   → 그래서 파일은 RuntimeLog.txt 하나이고,
            //     역할은 NetworkCombatController.OnNetworkSpawn 이 역할 확정 시점에
            //     "Role=host / Role=client" 로그 한 줄로 남긴다(LogRules.md 1.4 / 1.10).
            _logFileSink.BeginSession();

            Hexiege.Application.GameLog.AddSink(_logFileSink);
#else
            // ── 빌드(실기기): 콘솔 sink 하나만 등록한다 ──
            //   빌드에서는 파일 쓰기가 아예 없으므로(RuntimeLogger 의 파일 코드는 에디터 전용),
            //   로그는 Logcat 으로 나가고 필요하면 LogcatCapture 도구로 파일에 뽑는다.
            _logConsoleSink = new ConsoleSink();
            Hexiege.Application.GameLog.AddSink(_logConsoleSink);
#endif

            // ── 전역 미처리 예외 수집 (LogRules.md 1.9) ──
            //   아무리 규율을 지켜도 try-catch 를 두지 않은 자리는 남고,
            //   실제 크래시는 대개 그런 자리에서 난다.
            //   등록(+=)과 해제(-=)를 반드시 짝으로 맞춘다 —
            //   해제하지 않으면 에디터의 도메인 리로드/씬 재진입 때마다 핸들러가 중복 등록되어
            //   같은 예외가 2번, 3번... 계속 늘어난 횟수만큼 기록된다.
            //
            //   ⚠️ Application 은 완전 수식한다(파일 상단 "이름 충돌 주의" 참조).
            UnityEngine.Application.logMessageReceived += OnUnityLogMessageReceived;

            // ── 종료 시 1회 정리 (이 작업의 핵심 — "닫는 쪽은 하나") ──
            //   예전에는 GameBootstrapper.OnDestroy 에서 닫았다. 그런데 OnDestroy 는
            //   **씬이 언로드될 때마다** 불리므로, 씬을 옮기는 것만으로 파일이 닫혀 버린다.
            //   그러면 로그인 → 로비 → 전투를 한 파일로 잇겠다는 목적 자체가 깨진다.
            //   그래서 정리 시점을 "앱이 끝날 때" 로 옮겼다.
            UnityEngine.Application.quitting += Shutdown;

#if UNITY_EDITOR
            // ── 에디터 보조 배선 ──
            //   UnityEngine.Application.quitting 이 에디터에서 플레이 모드를 멈출 때도
            //   반드시 발생하는지를 이 코드베이스만으로는 단정할 수 없다(선례 0건).
            //   발생하지 않으면 파일 스트림이 닫히지 않은 채 남는다.
            //   그래서 에디터에서는 "플레이 모드가 완전히 끝나고 편집 모드로 돌아온 시점" 에도
            //   한 번 더 정리를 시도한다. Shutdown() 은 멱등이라 둘 다 불려도 안전하다.
            //
            //   ExitingPlayMode 가 아니라 EnteredEditMode 를 고른 이유:
            //     ExitingPlayMode 는 오브젝트들의 OnDestroy 보다 앞설 수 있어,
            //     종료 과정에서 남는 로그를 놓칠 위험이 있다.
            //     EnteredEditMode 는 정리가 모두 끝난 뒤라 그럴 일이 없다.
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

        // ====================================================================
        // 정리 — 외부에서 호출하지 않는다 (닫는 주체를 하나로 못 박기 위해 private)
        // ====================================================================

        /// <summary>
        /// 전역 예외 훅을 해제하고, 파일 세션을 닫고, 등록한 sink 를 뺀다.
        /// 앱/플레이 모드 종료 시 1회만 실행된다. 여러 경로에서 불려도 안전하다(멱등).
        ///
        /// private 인 이유:
        ///   "닫는 주체는 하나" 라는 규칙을 코드로 못 박기 위해서다.
        ///   외부에서 임의로 닫을 수 있게 열어 두면, 예전처럼 씬 전환 시점에 닫는 코드가
        ///   다시 생겨나 같은 사고가 반복된다.
        /// </summary>
        private static void Shutdown()
        {
            // 이미 정리됐거나 애초에 켠 적이 없으면 아무 일도 하지 않는다.
            if (!_initialized) return;

            // 훅부터 뗀다. 이 뒤로는 되먹임 걱정 없이 정리 작업을 진행할 수 있다.
            UnityEngine.Application.logMessageReceived -= OnUnityLogMessageReceived;

            // 등록(+=)과 해제(-=)는 언제나 짝으로 맞춘다.
            UnityEngine.Application.quitting -= Shutdown;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif

#if UNITY_EDITOR
            if (_logFileSink != null)
            {
                Hexiege.Application.GameLog.RemoveSink(_logFileSink);

                // 자기가 연 세션만 자기가 닫는다(FileSink 내부 소유권 가드).
                _logFileSink.EndSession();
                _logFileSink = null;
            }
#else
            if (_logConsoleSink != null)
            {
                Hexiege.Application.GameLog.RemoveSink(_logConsoleSink);
                _logConsoleSink = null;
            }
#endif

            // 훅 중복 방지용 캐시도 비운다.
            // 다음 플레이에서 "직전 예외" 로 남은 옛 문자열과 비교되는 일이 없도록 한다.
            _lastHookCondition = null;
            _lastHookStackTrace = null;

            // 마지막에 내린다. 다시 EnsureInitialized() 가 불리면 정상적으로 새로 열 수 있다.
            _initialized = false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 플레이 모드 상태 변화 콜백(보조 배선).
        /// 플레이 모드가 끝나 편집 모드로 돌아온 시점에 정리를 한 번 더 시도한다.
        /// </summary>
        /// <param name="state">플레이 모드 상태 변화 종류.</param>
        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state != UnityEditor.PlayModeStateChange.EnteredEditMode) return;

            // quitting 이 이미 정리를 마쳤다면 Shutdown() 첫 줄에서 그대로 되돌아간다.
            Shutdown();
        }
#endif

        // ====================================================================
        // 전역 미처리 예외 훅
        // ====================================================================

        /// <summary>
        /// UnityEngine.Application.logMessageReceived 콜백.
        /// 계측하지 않은 곳에서 터진 **미처리 예외**를 GameLog 로 끌어온다.
        ///
        /// ── 여기서 무한 루프가 나는 구조 (이 구현에서 가장 위험한 지점) ──────────
        ///   이 훅은 Debug.Log 계열이 호출될 때마다 되불린다.
        ///   그런데 GameLog 의 sink 도 결국 Debug.Log 계열을 호출한다.
        ///   따라서 아무 방어 없이 이 훅에서 GameLog 를 호출하면
        ///
        ///       훅 → GameLog → sink → Debug.LogError → 훅 → GameLog → sink → ...
        ///
        ///   이 고리가 끝없이 돌아 스택이 터지고 앱이 죽는다.
        ///   로그 시스템이 크래시를 기록하는 대신 크래시의 **원인**이 되는 것이다.
        ///   그래서 방어를 세 겹 둔다(아래 1·2·3).
        /// </summary>
        /// <param name="condition">예외 메시지.</param>
        /// <param name="stackTrace">스택 트레이스(여러 줄).</param>
        /// <param name="type">로그 종류. 예외만 수집한다.</param>
        private static void OnUnityLogMessageReceived(string condition, string stackTrace, UnityEngine.LogType type)
        {
            // ── 방어 1: 예외(LogType.Exception)만 수집한다 ──
            //   sink 가 콘솔에 내보내는 일반 로그는 Log / Warning / Error 종류다.
            //   그 셋을 여기서 걸러 내면, sink 의 평범한 출력은 애초에 이 훅을 통과하지 못한다.
            //   → 되먹임 고리가 만들어질 수 있는 경로 자체가 크게 줄어든다.
            //   (Debug.LogError 로 남는 기존 로그를 여기서 다시 수집하지 않는 이유이기도 하다.
            //    같은 사건을 두 번 기록하면 서버 집계에서 발생 건수가 부풀려진다 —
            //    LogRules.md 1.3 분류 원칙 1.)
            if (type != UnityEngine.LogType.Exception) return;

            // ── 방어 2: 재진입 가드 ──
            //   GameLog 가 sink 로 내보내는 도중이라면, 지금 들어온 이 예외는
            //   우리 자신이 방금 만든 출력(예: ConsoleSink 의 Debug.LogException)이 되돌아온 것이다.
            //   즉시 반환해 고리를 끊는다.
            //   (GameLog.Emit 안에도 같은 취지의 가드가 있다 — 방어를 한 겹만 두지 않는다.)
            if (Hexiege.Application.GameLog.IsEmitting) return;

            // ── 방어 3: 직전과 동일한 예외면 무시한다 ──
            //   훅 전달이 혹시라도 즉시(동기)가 아니라 나중에 이루어지는 경우,
            //   방어 2 의 플래그가 이미 내려가 있어 같은 예외를 다시 받을 수 있다.
            //   메시지 + 스택이 직전과 똑같으면 우리가 되쏜 것으로 보고 버린다.
            if (condition == _lastHookCondition && stackTrace == _lastHookStackTrace) return;

            _lastHookCondition = condition;
            _lastHookStackTrace = stackTrace;

            // 운영 로그로 남긴다 — 미처리 예외는 정확히 "플레이어 기기에서만 벌어지고,
            // 이 로그가 없으면 추적할 수단이 없는" 사건이다(LogRules.md 1.2 축 B).
            //
            // 스택을 message 뒤에 줄바꿈으로 붙이는 이유:
            //   여기서는 Exception 객체가 아니라 문자열만 전달받으므로 예외 오버로드를 쓸 수 없다.
            //   스택을 key=value 자리에 넣으면 여러 줄·쉼표 때문에 구조화 필드가 깨지므로
            //   메시지 쪽에 붙인다(LogRules.md 1.4 — 자유 문장은 메시지에).
            //
            // className 이 GameBootstrapper 가 아니라 이 클래스인 이유:
            //   훅의 소유자가 여기로 옮겨 왔기 때문이다. Infrastructure 인 이 파일이
            //   Bootstrap 레이어의 GameBootstrapper 를 참조하면 의존 방향이 거꾸로 뒤집힌다.
            Hexiege.Application.GameLog.Ops.Error(
                Hexiege.Application.LogEvent.UnhandledException,
                "Runtime",
                nameof(LogSessionOwner),
                $"미처리 예외: {condition}\n{stackTrace}",
                "Source=UnityLogHook");
        }
    }
}
