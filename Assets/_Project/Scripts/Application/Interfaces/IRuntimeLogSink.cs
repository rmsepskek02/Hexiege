// ============================================================================
// IRuntimeLogSink.cs
// Application 레이어가 "파일 기록형 런타임 로그"를 남기기 위한 로깅 싱크 인터페이스.
//
// 왜 인터페이스인가(레이어 규칙 — 핵심):
//   실제 파일 기록은 Infrastructure의 RuntimeLogger(정적 유틸)가 담당한다.
//   그런데 Application(StatusEffectSystem·UnitCombatUseCase)은 Infrastructure를
//   "직접 참조하면 안 된다"(레이어 방향: Application → Infrastructure 금지).
//   그래서 IUnitFactory / IGameServices와 동일하게 "인터페이스는 Application에 선언,
//   구현(어댑터)은 Infrastructure에 두고 GameBootstrapper가 주입"하는 의존성 역전을 쓴다.
//   → Application은 이 인터페이스만 알면 되고, RuntimeLogger의 존재를 전혀 모른다.
//
// LogLevel을 왜 Application에 또 두나(레이어 중립):
//   RuntimeLogger의 LogLevel은 Hexiege.Infrastructure에 있다. 그것을 Application이 쓰면
//   Application이 Infrastructure enum을 참조하게 되어 레이어 위반이다.
//   따라서 Application 전용 LogLevel을 여기 선언하고, Infrastructure 어댑터가
//   Application.LogLevel → Infrastructure.LogLevel로 매핑한다(원 enum은 건드리지 않음 — 리스크 0).
//
// Application 레이어 — 순수 C#. Unity/Netcode/Infrastructure 참조 없음.
// ============================================================================

namespace Hexiege.Application
{
    /// <summary>
    /// 로그 심각도 단계(Application 중립). Infrastructure.LogLevel과 값 구성은 같지만,
    /// 레이어 분리를 위해 별도로 선언한다(어댑터가 1:1 매핑).
    /// </summary>
    public enum LogLevel
    {
        Info,
        Warn,
        Error
    }

    /// <summary>
    /// 파일 기록형 런타임 로그를 남기는 싱크(구현은 Infrastructure의 어댑터).
    /// LogRules 형식([HH:MM:SS.ms] [LEVEL] [System/Class] 메시지 | key=value)은 구현체가 자동 생성한다.
    /// </summary>
    public interface IRuntimeLogSink
    {
        /// <summary>
        /// 로그 한 줄을 기록한다(콘솔 + 에디터 파일). 세션 제어(BeginSession/EndSession)는
        /// 역할(host/client) 판별이 가능한 상위 레이어(Bootstrap/Network)가 직접 담당하므로
        /// 이 싱크에는 포함하지 않는다(로그 지점은 오직 "쓰기"만 필요).
        /// </summary>
        /// <param name="level">로그 심각도(Info/Warn/Error).</param>
        /// <param name="system">상위 시스템 영역. 예: "Skill".</param>
        /// <param name="className">로그를 남기는 클래스 이름. 예: "StatusEffectSystem".</param>
        /// <param name="message">사람이 읽을 메시지 본문.</param>
        /// <param name="data">선택. "key=value, key=value" 형태의 추가 데이터. 민감정보 금지.</param>
        void Log(LogLevel level, string system, string className, string message, string data = null);
    }
}
