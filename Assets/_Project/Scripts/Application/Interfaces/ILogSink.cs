// ============================================================================
// ILogSink.cs
// 로그를 "실제로 어딘가에 내보내는 곳(sink)"의 계약 + Application 중립 열거형 정의.
//
// ── 이 파일이 왜 Application 레이어에 있는가 (레이어 규칙 — 핵심) ──────────
//   운영 로그는 Application 을 포함한 거의 모든 레이어에서 호출된다.
//   그런데 이 프로젝트는 "Application → Infrastructure 직접 참조 금지" 규칙이 있다.
//   (레이어 방향: Domain → Application → Core → Infrastructure → Presentation → Bootstrap)
//
//   따라서 계약(인터페이스)과 열거형은 안쪽 레이어인 Application 에 두고,
//   실제 구현(콘솔 출력 / 파일 기록)은 바깥 레이어인 Infrastructure 에 둔다.
//   그리고 조합 루트(GameBootstrapper)가 구현체를 만들어 GameLog 에 등록한다.
//   → Application 은 "ILogSink 라는 계약"만 알고, ConsoleSink/FileSink 의 존재를 전혀 모른다.
//   기존 IUnitFactory / IGameServices 와 완전히 동일한 의존성 역전 패턴이다.
//
// ── 왜 LogLevel 을 여기에 또 선언하는가 ────────────────────────────────────
//   Infrastructure 의 RuntimeLogger 에도 LogLevel 이 있다(Hexiege.Infrastructure.LogLevel).
//   그것을 Application 이 쓰면 Application → Infrastructure 참조가 되어 레이어 위반이다.
//   그래서 값 구성이 같은 "Application 중립 LogLevel"을 여기에 따로 선언하고,
//   Infrastructure 쪽 구현체(FileSink)가 1:1 로 매핑한다.
//
//   ⚠️ 초급 개발자 주의: 같은 이름의 enum 이 두 네임스페이스에 존재한다.
//      Infrastructure 파일 안에서 그냥 `LogLevel` 이라고 쓰면
//      그 파일이 속한(enclosing) 네임스페이스인 Hexiege.Infrastructure.LogLevel 로 해석된다.
//      따라서 ILogSink 를 구현할 때는 반드시 `Hexiege.Application.LogLevel` 로
//      **완전 수식(fully-qualify)** 해야 인터페이스 구현이 성립한다.
//
// 규격 출처: Assets/_Project/Docs/LogRules.md
//   - 1.5 이벤트 키 — LogEvent
//   - 1.8 sink 구조
//   - 1.9 예외 처리
//
// Application 레이어 — 순수 C#. Unity/Netcode/Infrastructure 참조 없음.
// ============================================================================

using System;

namespace Hexiege.Application
{
    /// <summary>
    /// 로그 심각도(축 A — LogRules.md 1.2 "두 축 — 심각도와 존속").
    /// 판정 질문은 딱 하나 — "복구되었나?"
    /// - Info : 의도된 흐름. 애초에 문제가 아님
    /// - Warn : 예상 밖이지만 대체 경로로 계속 진행됨(폴백/재시도 성공/요청만 거부)
    /// - Error: 복구 경로가 없음. 기능이 죽고 사용자가 그 기능을 더 못 씀
    /// </summary>
    public enum LogLevel
    {
        Info,
        Warn,
        Error
    }

    /// <summary>
    /// 운영 로그의 "이벤트 키"(LogRules.md 1.5 "이벤트 키 — LogEvent").
    ///
    /// 왜 문자열이 아니라 enum 인가:
    ///   문자열을 쓰면 "MatchmakingFailed" / "matchmaking_failed" / "MatchmakingFail" 처럼
    ///   조금씩 다른 키가 생겨 서버 집계가 조용히 여러 갈래로 쪼개진다.
    ///   그리고 이 오류는 컴파일러가 잡아 주지 못한다.
    ///   enum 이면 오타는 곧바로 컴파일 에러이고, 이름을 바꾸면 IDE 가 전부 따라 바꾼다.
    ///
    /// 이름 규칙:
    ///   - "무엇이 일어났는지"를 적는다(어디서 일어났는지는 [System/Class]가 이미 담고 있다).
    ///   - 한 번 정한 이름은 바꾸지 않는다 — 이름이 바뀌면 서버에 쌓인 과거 지표와 연결이 끊긴다.
    ///   - 서버 전송 시 enum 멤버 이름을 그대로(PascalCase) 사용한다. 변환 규칙을 두지 않는다.
    ///
    /// ⚠️ 지금은 "전역 미처리 예외 수집"에 필요한 최소 항목만 정의되어 있다.
    ///    기존 Debug.Log 209건을 GameLog 로 이관하는 후속 작업에서 항목이 늘어난다.
    ///    (예: MatchmakingLobbyJoinFailed, CloudSaveValueParseFailed,
    ///         ServerRejectedUpgradeInsufficientGold ...)
    /// </summary>
    public enum LogEvent
    {
        /// <summary>
        /// 분류되지 않은 이벤트. **의도적으로 사용하지 않는다.**
        /// 0번 자리를 일부러 "쓸모없는 값"으로 채워 둔 이유:
        /// enum 의 기본값(default(LogEvent))은 항상 0이므로, 여기에 의미 있는 키를 두면
        /// 값을 깜빡 잊고 넘긴 호출이 그 지표로 조용히 섞여 들어가 집계를 오염시킨다.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// try-catch 로 감싸지 않은 곳에서 터진 미처리 예외.
        /// GameBootstrapper 가 건 UnityEngine.Application.logMessageReceived 훅이 수집한다.
        /// (LogRules.md 1.9 "전역 미처리 예외 수집")
        /// </summary>
        UnhandledException,

        // ====================================================================
        // [G] UGS 초기화 · 브릿지
        //
        // 부여 근거: _Tasks/2026-08-13/07_13_network-auth-log-cleanup/LogAudit.md §5-1 G그룹.
        //
        // ⚠️ 초급 개발자 주의 — 왜 감사표의 키 30개를 한 번에 넣지 않는가:
        //    이관은 파일 단위로 진행되며, 각 파일 차례에 그 파일이 실제로 쓰는 키만 추가한다.
        //    미리 다 넣어 두면 "선언은 되어 있는데 아무도 쓰지 않는 키"가 생기고,
        //    나중에 그것이 이관 누락 때문인지 원래 안 쓰는 키인지 구분할 수 없게 된다.
        //
        // ⚠️ 멤버를 추가하는 위치(순서)는 집계에 영향을 주지 않는다.
        //    서버로는 enum 의 숫자 값이 아니라 **멤버 이름 그대로** 나가기 때문이다
        //    (LogRules.md 1.5 — "변환 규칙을 두지 않는다").
        //    반대로 **이름을 바꾸면** 서버에 쌓인 과거 지표와 연결이 끊긴다. 이름은 고정이다.
        // ====================================================================

        /// <summary>
        /// UGS(Unity Gaming Services) 초기화 자체가 실패했다.
        /// 지역·회선·프로젝트 설정에 좌우돼 개발 기기에서는 재현되지 않는 종류의 실패이고,
        /// 예외 메시지가 원인을 알 수 있는 유일한 단서다.
        /// 발생 지점: Infrastructure/Network/UnityServicesInitializer.cs
        /// </summary>
        UnityServicesInitializeFailed,

        /// <summary>
        /// UGS 세션이 하나도 없어 익명 로그인으로 폴백했다.
        /// 릴리스 빌드에서 이 키가 올라오면 Login 씬이 만든 OIDC 세션이 유실됐다는 뜻이고,
        /// 그 결과 PlayerId 가 통째로 바뀌어 멀티플레이 정체성이 달라진다.
        /// 게임은 계속 진행되며 플레이어에게는 아무 통지도 가지 않는다.
        /// 발생 지점: Infrastructure/Network/UnityServicesInitializer.cs
        /// </summary>
        UgsSessionMissingAnonymousFallback,

        // ── LoginUseCase (4단계 3/8) ──────────────────────────────────
        /// <summary>UGS 브릿지에서 BridgeToUGSAsync 가 삼키지 못한 예외가 올라온 경우.</summary>
        UgsBridgeUnhandledException,

        /// <summary>UGS SignOut 실패. 이전 계정 세션이 남아 다음 로그인에서 계정이 뒤바뀔 수 있다.</summary>
        UgsSignOutFailed,

        /// <summary>BridgeToUGSAsync 호출 계약 위반 — firebaseUID 가 비어 있다.</summary>
        UgsBridgeMissingFirebaseUid,

        /// <summary>UGS 브릿지 실패. 로그인은 성공 처리되어 플레이어는 통지받지 못한다.</summary>
        UgsBridgeFailed
    }

    /// <summary>
    /// 로그 한 건을 실제 출력 매체로 내보내는 곳(sink)의 계약.
    /// 구현체는 Infrastructure 에 있다 — ConsoleSink(콘솔), FileSink(파일).
    ///
    /// ⚠️ 세션 제어(BeginSession/EndSession)를 이 인터페이스에 넣지 않는다.
    ///    파일을 어떤 이름(host/client)으로 열지는 "역할 판별이 가능한 상위 지점"만 알 수 있고,
    ///    그 상위 지점은 조합 루트인 GameBootstrapper 다.
    ///    세션을 여닫는 주체를 하나로 못 박아 두지 않으면,
    ///    한쪽이 다른 쪽 세션을 닫아 버려 로그 파일이 아예 안 남는 사고가 난다(과거 실제 발생).
    /// </summary>
    public interface ILogSink
    {
        /// <summary>
        /// 로그 한 건을 출력한다.
        ///
        /// 넘어오는 값은 GameLog 가 이미 처리를 끝낸 상태다:
        ///   - 이벤트 키(Event=...)와 예외 타입(ExceptionType=...)은 data 에 병합되어 있다.
        ///   - 민감 데이터(이메일 패턴) 차단이 이미 적용되어 있다(LogRules.md 1.6).
        /// 따라서 구현체는 "받은 값을 내보내는 일"만 하면 된다.
        ///
        /// 최종 로그 한 줄 형식이 필요하면 <see cref="GameLog.Compose"/> 를 사용한다
        /// (형식 규정을 여러 곳에 복사하지 않기 위해).
        /// </summary>
        /// <param name="level">심각도(Info/Warn/Error).</param>
        /// <param name="system">시스템 영역. 예: "Network", "Combat", "UI".</param>
        /// <param name="className">로그를 남기는 클래스 이름. 예: nameof(LobbyManager).</param>
        /// <param name="message">사람이 읽을 메시지 본문(집계 키로 쓰지 않는다).</param>
        /// <param name="data">"key=value, key=value" 형태의 구조화 필드. 없으면 null.</param>
        /// <param name="exception">함께 남길 예외. 없으면 null.
        /// 예외를 e.Message 문자열로 눌러 담지 않고 객체 그대로 넘기는 이유는
        /// 예외 "타입"이 텔레메트리 집계의 핵심 축이고,
        /// 콘솔 sink 가 Debug.LogException 으로 내보내야 스택 심볼화가 되기 때문이다
        /// (LogRules.md 1.9 "예외 처리").</param>
        void Write(LogLevel level, string system, string className, string message, string data, Exception exception);
    }
}
