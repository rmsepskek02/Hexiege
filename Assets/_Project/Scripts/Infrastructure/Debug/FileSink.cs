// ============================================================================
// FileSink.cs
// GameLog 의 로그를 에디터에서 텍스트 파일로 남기는 sink.
//
// ── 왜 파일에 남기는가 ────────────────────────────────────────────────────
//   콘솔에만 남은 로그는 파일이 되지 않아 Claude(QA 에이전트 포함)가 읽을 수 없다.
//   버그 재현 흐름을 추적하려면 파일이 반드시 필요하다(LogRules.md 1.14 금지사항 1).
//
// ── 파일 쓰기를 새로 만들지 않고 RuntimeLogger 를 재사용한다 ────────────────
//   이미 검증된 파일 쓰기 코드(append + AutoFlush, 예외가 나도 게임을 멈추지 않음)가 있는데
//   구현을 하나 더 만들면, 둘이 서서히 다르게 동작하기 시작하고
//   "어느 쪽이 진짜인지" 알 수 없게 된다(LogRules.md 1.10 / 1.11).
//   → 이 클래스는 경로·세션 수명만 책임지고, 실제 기록은 RuntimeLogger 에 위임한다.
//
// ── 에디터 전용이다 ──────────────────────────────────────────────────────
//   RuntimeLogger 의 파일 쓰기 코드는 #if UNITY_EDITOR 안에 있어 실기기에서는 동작하지 않는다.
//   그리고 RuntimeLogger.Log 는 파일과 **콘솔에 동시에** 출력한다.
//   그래서 이 sink 를 빌드에서 ConsoleSink 와 함께 등록하면 콘솔에 같은 줄이 두 번 찍힌다.
//   그 사고를 구조적으로 막기 위해, 이 클래스의 동작 전체를 #if UNITY_EDITOR 로 감쌌다.
//   빌드에서는 등록되더라도 아무 일도 하지 않는다.
//   (조합 루트에서도 에디터: FileSink / 빌드: ConsoleSink 로 갈라 등록한다.)
//
// ⚠️ enum 이름 충돌 주의
//   LogLevel 이 Hexiege.Application 과 Hexiege.Infrastructure 양쪽에 존재한다.
//   이 파일의 네임스페이스는 Hexiege.Infrastructure 이므로 그냥 `LogLevel` 이라고 쓰면
//   Infrastructure 쪽으로 해석된다. 인터페이스 구현 자리에서는 반드시 완전 수식한다.
//
// ⚠️ 이름 충돌 주의 2 — Application
//   이 파일은 namespace Hexiege.Infrastructure 안에 있고, 그 바깥 네임스페이스는 Hexiege 다.
//   따라서 그냥 `Application` 이라고 쓰면 **네임스페이스 Hexiege.Application** 으로 해석되어
//   UnityEngine.Application.dataPath 같은 코드가 컴파일되지 않는다.
//   → 유니티 쪽은 반드시 UnityEngine.Application 으로 완전 수식한다.
//
// 규격 출처: Assets/_Project/Docs/LogRules.md (1.4 형식 / 1.8 sink 구조 / 1.10 파일 기록 / 1.11 RuntimeLogger 의 위치)
//
// Infrastructure 레이어 — Application(안쪽) 참조는 정상 방향이다.
// ============================================================================

using System;
using System.IO;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 에디터에서 런타임 로그를 파일로 남기는 sink.
    /// 세션(파일 열기/닫기)은 이 클래스가 스스로 열지 않고, 조합 루트가 명시적으로 지시한다.
    /// </summary>
    public sealed class FileSink : Hexiege.Application.ILogSink
    {
        // ====================================================================
        // 경로 규칙 (LogRules.md 1.10 "파일 기록 — 에디터 상시 활성")
        // ====================================================================

        /// <summary>
        /// 에디터 상시 로그의 뿌리 폴더. Assets 폴더 기준 상대 경로다.
        ///
        /// 왜 기존 _Logs/YYYY-MM-DD/ 가 아니라 _Logs/_editor/ 인가:
        ///   _Logs/YYYY-MM-DD/HH_MM_[작업명]/ 은 특정 작업에 귀속된 **영구 보존물**이고,
        ///   상시 로그는 **언제 버려도 되는 소모품**이다.
        ///   성격이 정반대인 둘이 같은 폴더에 섞이면
        ///   "지워도 되는 파일"과 "지우면 안 되는 파일"을 구분할 수 없게 되고,
        ///   자동 정리를 도입하는 순간 사고가 난다.
        /// </summary>
        private const string EditorLogsRootRelativeToAssets = "_Project/Docs/_Logs/_editor";

        /// <summary>
        /// 이 sink 가 여는 세션의 목적 문자열. RuntimeLogger 가 파일 헤더 1줄째에 그대로 쓴다.
        /// (LogRules.md 1.4 「파일 헤더」 — 1줄째는 "작업명 또는 로그 목적"이어야 한다)
        ///
        /// 이 sink 는 특정 작업이 아니라 "에디터에서 플레이할 때마다 항상 켜져 있는 로그"이므로,
        /// 작업명 대신 그 성격을 그대로 적는다.
        /// </summary>
        private const string SessionPurpose = "에디터 상시 런타임 로그";

        // ====================================================================
        // 상태
        // ====================================================================

        /// <summary>
        /// 이 인스턴스가 RuntimeLogger 세션을 열어 두었는지 여부(소유권 플래그).
        ///
        /// 왜 소유권을 따지는가:
        ///   RuntimeLogger 의 세션(파일 스트림)은 전역에 하나뿐이다.
        ///   세션을 여닫는 주체가 여럿이면, 한쪽이 다른 쪽 세션을 닫아 버려
        ///   그 뒤의 로그가 파일에 아예 안 남는 사고가 난다(과거 실제 발생).
        ///   그래서 "자기가 연 세션만 자기가 닫는다"는 규칙을 코드로 못 박아 둔다.
        ///
        /// 선언 지점에서 false 로 명시 초기화하는 이유:
        ///   빌드에서는 이 값을 대입하는 코드가 #if UNITY_EDITOR 로 잘려 나가
        ///   "한 번도 대입되지 않는 필드" 경고(CS0649)가 뜨기 때문이다.
        /// </summary>
        private bool _sessionOwned = false;

        /// <summary>이 sink 가 세션을 열어 둔 상태인지(디버깅·검증용).</summary>
        public bool IsSessionOpen => _sessionOwned;

        // ====================================================================
        // 세션 제어 — 조합 루트(GameBootstrapper)만 호출한다
        // ====================================================================

        /// <summary>
        /// 오늘 날짜 폴더에 로그 파일을 열고(없으면 만들고) 기록을 시작한다.
        ///
        /// 세션 제어를 ILogSink 인터페이스에 넣지 않은 이유:
        ///   RuntimeLogger 의 파일 스트림은 전역에 하나뿐이라, 여닫는 주체를 하나로 못 박아야
        ///   세션 소유권 사고(한쪽이 다른 쪽 세션을 닫아 로그가 통째로 사라지는 문제)가 나지 않는다.
        ///   그 하나의 주체가 조합 루트인 GameBootstrapper 다.
        ///
        /// 파일은 **날짜 단위로 이어쓰기** 한다(하루에 파일 1개 — RuntimeLog.txt).
        /// 플레이 모드에 다시 들어가면 RuntimeLogger 가 헤더를 다시 기록하므로
        /// 그 헤더가 실행 구분선 역할을 한다(LogRules.md 1.10).
        ///
        /// 파일명에 역할(host/client)을 넣지 않는 이유:
        ///   이 클래스의 동작 전체가 #if UNITY_EDITOR 안에 있어 빌드(실기기)는 파일을 쓰지 않는다.
        ///   따라서 파일을 쓰는 프로세스는 항상 에디터 1개뿐이라 파일이 충돌할 수 없다.
        ///   역할은 NetworkCombatController.OnNetworkSpawn 에서 "Role=host / Role=client"
        ///   로그 한 줄로 남긴다(LogRules.md 1.10).
        /// </summary>
        public void BeginSession()
        {
#if UNITY_EDITOR
            // 이미 열어 둔 세션이 있으면 아무것도 하지 않는다(중복 호출 방어).
            if (_sessionOwned) return;

            try
            {
                // UnityEngine.Application.dataPath 는 <프로젝트>/Assets 를 가리킨다.
                // (Application 을 완전 수식하지 않으면 Hexiege.Application 네임스페이스로 해석된다)
                string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                string folderPath = Path.Combine(
                    UnityEngine.Application.dataPath,
                    EditorLogsRootRelativeToAssets,
                    dateFolder);

                // 폴더 생성은 RuntimeLogger.BeginSession 안에서도 하지만,
                // 여기서 미리 만들어 두면 실패 원인을 이 자리에서 바로 알 수 있다.
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // 두 번째 인자는 파일 헤더 1줄째에 들어갈 목적 문자열이다(LogRules.md 1.4).
                RuntimeLogger.BeginSession(folderPath, SessionPurpose);
                _sessionOwned = true;
            }
            catch (Exception e)
            {
                // 로그 파일을 못 열었다고 게임이 멈추면 안 된다.
                // 콘솔 경고만 남기고 계속 진행한다(이후 로그는 콘솔에만 남는다).
                _sessionOwned = false;
                UnityEngine.Debug.LogWarning($"[FileSink] 로그 세션 시작 실패: {e.Message}");
            }
#endif
        }

        /// <summary>
        /// 로그 파일을 닫는다. 자기가 연 세션이 아니면 아무것도 하지 않는다(소유권 가드).
        /// </summary>
        public void EndSession()
        {
#if UNITY_EDITOR
            if (!_sessionOwned) return;

            RuntimeLogger.EndSession();
            _sessionOwned = false;
#endif
        }

        // ====================================================================
        // ILogSink 구현
        // ====================================================================

        /// <summary>
        /// 로그 한 건을 파일(+에디터 콘솔)에 기록한다.
        /// 실제 기록은 RuntimeLogger 가 수행하며, 로그 한 줄의 형식도 RuntimeLogger 가 조립한다
        /// (LogRules.md 1.4 와 동일한 형식).
        /// </summary>
        /// <param name="level">심각도(Application 중립 enum).</param>
        /// <param name="system">시스템 영역.</param>
        /// <param name="className">클래스 이름.</param>
        /// <param name="message">메시지 본문(GameLog 가 이미 민감 데이터를 걸러 둔 상태).</param>
        /// <param name="data">"key=value" 구조화 필드(Event=... 이 이미 병합되어 있다).</param>
        /// <param name="exception">함께 남길 예외. 없으면 null.</param>
        public void Write(Hexiege.Application.LogLevel level, string system, string className,
                          string message, string data, Exception exception)
        {
#if UNITY_EDITOR
            if (exception == null)
            {
                RuntimeLogger.Log(MapLevel(level), system, className, message, data);
                return;
            }

            // ── 예외가 있을 때 ──
            //   RuntimeLogger 는 "[시각] [레벨] [System/Class] 메시지 | data" 순서로 조립한다.
            //   스택 트레이스를 data 자리에 넣으면 여러 줄 · 쉼표 때문에 key=value 형식이 깨지고,
            //   message 자리에 넣으면 스택 뒤에 " | data" 가 붙어 순서가 뒤집힌다.
            //   그래서 data 를 message 뒤에 직접 이어 붙여 첫 줄을 규정 형식 그대로 만들고,
            //   스택은 줄바꿈 뒤에 덧붙인다. 결과:
            //     [시각] [ERROR] [Network/LobbyManager] 메시지 | Event=..., ExceptionType=...
            //     <스택 트레이스 여러 줄>
            string head = string.IsNullOrEmpty(data) ? message : $"{message} | {data}";
            RuntimeLogger.Log(MapLevel(level), system, className, $"{head}\n{exception}", null);
#endif
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

#if UNITY_EDITOR
        /// <summary>
        /// Application 중립 LogLevel 을 RuntimeLogger 가 쓰는 Infrastructure LogLevel 로 1:1 변환한다.
        /// (이 파일 안의 수식 없는 LogLevel 은 Hexiege.Infrastructure.LogLevel 로 해석된다)
        /// </summary>
        private static LogLevel MapLevel(Hexiege.Application.LogLevel level)
        {
            switch (level)
            {
                case Hexiege.Application.LogLevel.Warn:
                    return LogLevel.Warn;
                case Hexiege.Application.LogLevel.Error:
                    return LogLevel.Error;
                default:
                    return LogLevel.Info;
            }
        }
#endif
    }
}
