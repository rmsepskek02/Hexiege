// ============================================================================
// RuntimeLoggerSink.cs
// Application의 IRuntimeLogSink(파일 로그 싱크)를 구현하는 Infrastructure 어댑터.
//
// 역할(의존성 역전의 "구현 쪽"):
//   Application 레이어는 IRuntimeLogSink 인터페이스만 알고, 실제 파일 기록은 모른다.
//   이 어댑터가 그 인터페이스를 구현하고, 내부에서 Infrastructure의 RuntimeLogger(정적 유틸)로
//   위임한다. GameBootstrapper(조합 루트)가 이 어댑터를 만들어 Application 객체에 주입한다.
//   (IUnitFactory→UnitFactory, IGameServices→GameBootstrapper와 동일한 구조.)
//
// LogLevel 매핑:
//   인터페이스 시그니처는 Application.LogLevel을 쓰므로, 여기서 Infrastructure.LogLevel로 1:1 변환한다.
//   (두 enum을 헷갈리지 않도록, Application 쪽은 전부 정규화된 이름 Hexiege.Application.*로 표기)
//
// 세션(BeginSession/EndSession)은 이 어댑터가 다루지 않는다:
//   파일 생성/종료는 역할(host/client) 판별이 가능한 상위 지점
//   (NetworkCombatController / GameBootstrapper)이 RuntimeLogger를 직접 호출해 관리한다.
//
// Infrastructure 레이어 — RuntimeLogger(같은 네임스페이스) 직접 사용 허용.
// ============================================================================

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// IRuntimeLogSink를 구현해 RuntimeLogger(파일+콘솔)로 위임하는 어댑터.
    /// </summary>
    public sealed class RuntimeLoggerSink : Hexiege.Application.IRuntimeLogSink
    {
        /// <summary>
        /// Application이 남긴 로그를 RuntimeLogger로 전달한다(콘솔 + 에디터 파일 동시 기록).
        /// </summary>
        /// <param name="level">Application 로그 레벨(Infrastructure 레벨로 매핑).</param>
        /// <param name="system">시스템 영역(예: "Skill").</param>
        /// <param name="className">클래스 이름(예: "StatusEffectSystem").</param>
        /// <param name="message">메시지 본문.</param>
        /// <param name="data">추가 데이터("key=value, ..."). 없으면 null.</param>
        public void Log(Hexiege.Application.LogLevel level, string system, string className, string message, string data = null)
        {
            // Application.LogLevel → Infrastructure.LogLevel(이 파일의 unqualified LogLevel은
            //   en클로징 네임스페이스 규칙에 따라 Hexiege.Infrastructure.LogLevel로 해석된다).
            RuntimeLogger.Log(MapLevel(level), system, className, message, data);
        }

        /// <summary>
        /// Application.LogLevel을 Infrastructure.LogLevel로 1:1 변환한다.
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
    }
}
