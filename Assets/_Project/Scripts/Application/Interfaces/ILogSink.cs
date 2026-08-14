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
        UgsBridgeFailed,

        // ── FirebaseAuthService (4단계 4/8) ───────────────────────────
        /// <summary>Firebase 의존성 해결 실패. Google Play 서비스 상태는 기기마다 달라 재현되지 않는다.</summary>
        FirebaseDependencyUnavailable,

        /// <summary>Firebase 초기화 실패. catch 후 false 를 반환해 호출부는 사유를 받지 못한다.</summary>
        FirebaseInitializeFailed,

        /// <summary>Google Play Games 인증 실패. 단계는 Stage 값으로 구분한다.</summary>
        GooglePlayGamesAuthFailed,

        /// <summary>Firebase 인증 작업 실패. 원본 code·reason 이 이 자리에만 남는다.</summary>
        FirebaseAuthOperationFailed,

        // ── LobbyManager (4단계 5/8) ──────────────────────────────────
        /// <summary>Lobby 서비스 호출 실패. 이 파일은 OnError 통지가 없어 로그가 유일한 실패 기록이다.</summary>
        LobbyServiceCallFailed,

        /// <summary>Lobby 불변식 위반. matchId 누락이나 현재 Lobby 부재 등 있을 수 없는 상태다.</summary>
        LobbyInvariantViolated,

        /// <summary>매칭 Lobby 생성·참가·검색 실패. 호출부는 null 만 받아 재시도 안내만 띄운다.</summary>
        MatchmakingLobbyJoinFailed,

        /// <summary>Lobby Heartbeat 전송 실패. 끊기면 Lobby 가 만료돼 매칭이 조용히 깨진다.</summary>
        LobbyHeartbeatFailed,

        // ── NetworkProductionController · NetworkBuildingController (4단계 6·7/8) ──
        //
        // 두 파일을 한 번에 넣는 이유: 아래 아홉 키 중 여덟 개를 두 파일이 함께 쓴다.
        // 같은 사건은 같은 키로 묶어야 집계가 쪼개지지 않기 때문이다(LogAudit.md §5-0).
        //
        // ⚠️ 초급 개발자 주의 — "요청 종류"를 키 이름에 넣지 않았다.
        //    배치/업그레이드/생산 골드 부족을 세 키로 나누면
        //    "자원 부족으로 서버가 거부한 횟수"라는 지표 자체가 만들어지지 않는다.
        //    어느 요청이었는지는 [System/Class] 와 key=value(Request=, Reason=)가 담는다.

        /// <summary>
        /// 서버 RPC 처리 중 조합 루트(IGameServices) 또는 UseCase 를 얻지 못했다.
        /// 발생하면 같은 종류의 요청이 이후 전부 같은 자리에서 죽는다 — 복구 경로가 없다.
        /// </summary>
        ServerRpcGameServicesMissing,

        /// <summary>
        /// 클라이언트 RPC 적용 중 조합 루트 또는 UseCase 를 얻지 못했다.
        /// 서버 상태가 이 클라이언트 화면에 반영되지 않는다.
        /// 서버 쪽(ServerRpcGameServicesMissing)과 원인은 같지만 결과가 다르므로 키를 나눈다 —
        /// 한 키로 묶으면 "게임이 죽었나 화면만 틀어졌나"를 집계에서 구분할 수 없다.
        /// </summary>
        ClientRpcGameServicesMissing,

        /// <summary>
        /// 네트워크 컨트롤러가 스폰 시점에 IGameServices 를 찾지 못했다.
        /// 위 두 키의 선행 신호다. 별도 키인 이유는 한 번의 사고가
        /// "스폰 1회 + 요청 N회"로 부풀려 집계되는 것을 막기 위해서다.
        /// </summary>
        NetworkControllerSpawnedWithoutGameServices,

        /// <summary>
        /// 골드·인구·큐 용량 부족으로 서버가 요청을 거부했다.
        /// 클라이언트가 CanAfford/HasPopulation 으로 이미 막았어야 하므로,
        /// 이 키가 올라오면 클라·서버 상태 불일치를 뜻한다.
        /// </summary>
        ServerRejectedInsufficientResource,

        /// <summary>
        /// 팀 불일치·소유권 불일치·규칙 위반(최고 단계 / Castle 철거) 요청을 서버가 거부했다.
        /// 정상 클라이언트는 보낼 수 없는 요청이라 변조 탐지 신호로 읽는다.
        /// </summary>
        ServerRejectedUnauthorizedRequest,

        /// <summary>
        /// 요청 대상(건물·배럭 생산 상태)이 서버에 없다.
        /// 클라이언트는 존재하는 대상의 패널만 열 수 있으므로 상태 불일치다.
        /// </summary>
        ServerRejectedTargetNotFound,

        /// <summary>
        /// 검증을 모두 통과한 뒤 실행이 실패했다. 서버 내부 상태 이상이며
        /// ServerRejectedTargetNotFound(대상 없음)와는 원인이 다르다.
        /// </summary>
        ServerActionExecutionFailed,

        /// <summary>
        /// 서버가 보낸 스폰·업그레이드를 클라이언트가 적용하지 못했다.
        /// 재시도 경로가 없어 그 오브젝트는 이 클라이언트에서 영구히 누락된다.
        /// </summary>
        ClientStateSyncApplyFailed,

        /// <summary>
        /// 유닛 뷰 초기화 재시도가 제한 시간을 넘겨 실패했다.
        /// ClientStateSyncApplyFailed 와 달리 재시도를 거친 뒤의 최종 실패다.
        /// </summary>
        UnitViewInitializeTimeout,

        // ── NetworkGameManager (4단계 8/8) ────────────────────────────
        //
        // 부여 근거: LogAudit.md §5-1 E그룹(Relay·네트워크 세션) · F그룹(매칭·세션 시작 예외).
        //
        // ⚠️ 초급 개발자 주의 — 왜 "Relay 할당 실패"와 "Relay 참가 실패"를 한 키로 묶는가:
        //    셋 다 "Relay 문제로 세션을 열지 못했다"는 하나의 사건이다. 키를 셋으로 나누면
        //    "Relay 때문에 게임에 못 들어간 횟수"라는 지표 자체가 만들어지지 않는다.
        //    어느 단계였는지는 key=value(Stage=Allocate|Join|CodeMissing)가 담는다.

        /// <summary>
        /// Relay 할당 · 참가 · Join Code 확보 중 하나가 실패해 네트워크 세션을 열 수 없다.
        /// 단계는 Stage 값(Allocate / Join / CodeMissing)으로 구분한다.
        /// </summary>
        RelaySetupFailed,

        /// <summary>
        /// NetworkManager.Singleton 이 null 이다.
        /// "GameBootstrapper 가 유일한 의존성 조합 루트"라는 프로젝트 전제가 깨진 상태이며,
        /// 이 자리에서 Host/Client 시작이 통째로 불가능해진다 — 복구 경로가 없다.
        /// </summary>
        NetworkManagerSingletonMissing,

        /// <summary>
        /// StartHost() / StartClient() 가 false 를 반환했다.
        /// NetworkManagerSingletonMissing 과 달리 객체는 존재하는데 시작 자체가 거부된 것이다.
        /// </summary>
        NetworkSessionStartFailed,

        /// <summary>
        /// 서버가 아닌 쪽에서 게임 씬 로드를 요청했다.
        /// 정상 흐름에서는 발생할 수 없는 불변식 위반이고 플레이어에게 통지되는 경로도 없다.
        /// </summary>
        SceneLoadRequestedByNonServer,

        /// <summary>
        /// 게임 생성 · 참가 흐름의 최종 catch 에서 잡힌 예외.
        /// 하위 계층이 분류하지 못한 것이 여기로 온다. 어느 경로였는지는 Flow 값
        /// (Host / Join / MatchHost / MatchJoin)으로 분해한다.
        /// </summary>
        GameSessionStartUnhandledException,

        /// <summary>
        /// 랜덤 매칭 흐름에서 예외가 났다.
        /// catch 본문이 로그 한 줄뿐이고 OnError 도 없어 매칭이 조용히 죽는다.
        /// </summary>
        MatchmakingUnhandledException,

        /// <summary>
        /// 매칭 취소 시 티켓 삭제가 실패했다.
        /// 흐름은 계속되지만 서버에 티켓이 남아 이후 매칭을 방해할 수 있다.
        /// </summary>
        MatchmakingTicketDeleteFailed
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
