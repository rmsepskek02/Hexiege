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

        /// <summary>
        /// 우리가 계측하지 않은 곳에서 **엔진 계층이 낸 오류**(Debug.LogError).
        /// UnhandledException 과 같은 훅(UnityEngine.Application.logMessageReceived)이 수집하지만
        /// **키를 나눈다.** 접두사를 맞춰 둔 것은 "같은 훅이 수집한 짝" 임을 이름만으로 알게 하기 위해서다.
        ///
        /// ── 왜 UnhandledException 과 한 키로 묶지 않는가 (LogRules.md 1.5 신설 기준 2개 충족) ──
        ///   ① 집계가 섞이면 안 된다:
        ///      UnhandledException 은 "try-catch 로 감싸지 않은 곳에서 터진 예외" 다 — 스택이 풀리고 흐름이 끊긴다.
        ///      반면 엔진·플러그인의 Debug.LogError 는 **예외가 아니다.** 게임은 그대로 계속 진행된다.
        ///      한 키로 묶으면 "크래시 건수" 지표가 크래시가 아닌 사건으로 부풀어,
        ///      출시 판단에 쓰는 가장 중요한 숫자를 못 쓰게 된다.
        ///   ② 조치가 다르다:
        ///      미처리 예외 → 그 자리에 try-catch 를 두거나 원인 코드를 고친다.
        ///      엔진 계층 오류 → 대개 우리가 SDK 를 잘못된 상태·순서로 호출한 것이라
        ///                      (예: NetworkManager 정지 후 RPC 발신) **호출 시점 가드 추가**가 조치다.
        ///
        /// 발생 지점: Infrastructure/Debug/LogSessionOwner.cs — OnUnityLogMessageReceived
        /// </summary>
        UnhandledEngineError,

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
        MatchmakingTicketDeleteFailed,

        // ====================================================================
        // [H] UGS Cloud Save · Leaderboards (배치 2 — 초기화·계정 계층 이관)
        //
        // 부여 근거: _Tasks/2026-08-17/17_19_remaining-layers-log-migration/Plan.md
        //            §5-2 기준 2 — "집계가 섞이면 안 되고 + 조치가 다를 때"만 신설.
        //
        // ⚠️ 초급 개발자 주의 — 왜 이 네 개는 새로 만들었는가:
        //    기존 32개 키는 전부 "네트워크 세션 · Firebase 인증" 계층에서 나온 것이라
        //    클라우드 저장(Cloud Save) · 랭킹(Leaderboards) 사건을 담을 키가 하나도 없었다.
        //    기존 키에 억지로 흡수시키면 "매칭이 안 된 횟수"와 "프로필이 안 불러와진 횟수"가
        //    한 지표에 섞여, 어느 쪽이 늘었는지 영원히 알 수 없게 된다.
        //
        // ⚠️ 반대로 "서비스 호출 실패"는 Load / Save 로 쪼개지 않았다.
        //    둘 다 조치가 같기 때문이다(UGS 연결·권한·대시보드 스키마 점검).
        //    어느 작업이었는지는 key=value 의 Operation= 이 담는다
        //    (RelaySetupFailed 가 Stage= 로 단계를 담는 것과 같은 방식).
        // ====================================================================

        /// <summary>
        /// UGS Cloud Save 호출 자체가 실패했다(프로필 로드 / 닉네임 저장).
        /// 어느 작업이었는지는 Operation 값(LoadProfile / SaveNickname)으로 구분한다.
        ///
        /// 왜 운영인가: 로드 실패는 빈 프로필로 조용히 폴백되어 아무도 눈치채지 못하고,
        /// 저장 실패는 화면에 "다시 시도하세요"만 뜰 뿐 원인(예외 타입)이 이 로그에만 남는다.
        /// 발생 지점: Infrastructure/Cloud/PlayerProfileService.cs
        /// </summary>
        CloudSaveOperationFailed,

        /// <summary>
        /// Cloud Save 에서 읽어 온 값을 원하는 타입으로 변환하지 못해 폴백 경로로 넘어갔다.
        /// 어느 키·타입이었는지는 Key / Type 값으로 구분한다.
        ///
        /// CloudSaveOperationFailed 와 키를 나눈 이유:
        ///   호출 실패는 "네트워크·권한" 문제이고, 변환 실패는 "저장된 데이터의 타입·스키마"
        ///   문제다. 조치가 완전히 다르므로 한 지표에 섞으면 원인 판단이 불가능해진다.
        ///
        /// 왜 운영인가(LogRules.md 1.3 분류 원칙 4 — 삼킨 예외):
        ///   변환에 실패해도 프로필이 "멀쩡한 기본값"으로 보여 실패했다는 사실 자체가 사라진다.
        /// 발생 지점: Infrastructure/Cloud/PlayerProfileService.cs — GetString / GetInt / GetBool
        /// </summary>
        CloudSaveValueParseFailed,

        /// <summary>
        /// UGS Leaderboards 조회가 실패해 빈 랭킹 목록을 반환했다.
        /// 화면에는 "랭킹이 비어 있음"으로만 보여 실패와 구분되지 않는다.
        /// 발생 지점: Infrastructure/Cloud/LeaderboardService.cs — GetTopRankingsAsync
        /// </summary>
        LeaderboardQueryFailed,

        /// <summary>
        /// 랭킹 엔트리의 Metadata(JSON) 파싱에 실패해 닉네임·전적 칸이 비었다.
        /// LeaderboardQueryFailed 와 키를 나눈 이유: 조회는 성공했고 데이터 포맷만 깨진
        /// 상태라, 조치 대상이 SDK/회선이 아니라 Cloud Code 가 기록하는 JSON 스키마다.
        /// 발생 지점: Infrastructure/Cloud/LeaderboardService.cs — ApplyMetadata
        /// </summary>
        LeaderboardMetadataParseFailed,

        // ====================================================================
        // [I] 무작위 맵 준비 · 투영 (무작위 맵 2단계 K)
        //
        // 부여 근거: GameSystemRules/GameSystemRules_RandomMap.md 규칙 12 「로그 필수 항목」
        //            _Tasks/2026-09-03/03_14_random-map-phase2-generator/Plan.md §4-K
        //            LogRules.md 1.5 (신설 기준 2개) · 1.2 (두 축)
        //
        // ⚠️ 초급 개발자 주의 — 이 네 키가 왜 「운영(Ops)」인가:
        //    LogRules 1.2 축 B 는 두 질문에 **둘 다 "예"** 여야 운영이라고 정한다.
        //      ① 플레이어 기기에서만 벌어지는가?
        //         → 예. 맵은 매 판 새로 뽑은 root seed 로 만들어지고, 그 seed 는 기기마다 다르다.
        //           "이 seed 에서만 100회가 전부 거부됐다" 같은 사건은 개발자 에디터에서
        //           같은 seed 를 손에 넣기 전에는 재현할 방법이 없다.
        //      ② 이 로그가 없으면 원인을 추적할 수단이 없는가?
        //         → 예. 로딩 화면은 규칙 12 에 따라 맵 유형·광산 수·골드·seed 를 **일부러 숨긴다.**
        //           화면에는 아무 단서도 뜨지 않고, 실패해도 플레이어에게 가는 통지가 없다.
        //
        // ⚠️ 초급 개발자 주의 — 왜 네 개로 나눴는가(그리고 왜 더 나누지 않았는가):
        //    LogRules 1.5 는 키를 새로 만들 조건을 두 개로 못 박는다 —
        //      ① 집계가 섞이면 안 된다   ② 조치가 다르다.
        //    아래 네 결말은 그 둘을 각각 충족한다(키마다 주석에 적어 두었다).
        //    반대로 **실패 사유 7종(MapPreparationErrorCode)마다 키를 만들지는 않았다.**
        //    그 일곱은 전부 "맵을 못 만들어 경기가 성립하지 않았다"는 하나의 사건이고,
        //    쪼개면 「맵 준비 실패로 경기가 안 열린 횟수」라는 지표 자체가 만들어지지 않는다.
        //    어느 사유였는지는 key=value 의 ErrorCode= 가 담는다
        //    (RelaySetupFailed 가 Stage= 로 단계를 담는 것과 같은 방식).
        //
        // ⚠️ 규칙 12 의 14개 항목 중 「전송/재전송 횟수」·「Host/Client 해시 비교 결과」는
        //    여기에 없다. 그 값은 무작위 맵 3단계(맵 전송)에서 처음 생기므로,
        //    지금 필드를 만들면 **항상 비어 있는 필드**가 되어 집계를 오염시킨다.
        // ====================================================================

        /// <summary>
        /// 맵 준비가 정상 경로로 끝났다(생성 → 검증 통과). 경기당 정확히 한 줄이다.
        ///
        /// 실패가 아닌데도 운영 로그인 이유:
        ///   규칙 12 가 「로그 필수 항목」으로 seed·해시·소요 시간 등을 요구하기 때문이다.
        ///   그 판의 맵을 나중에 똑같이 다시 만들어 보려면 root seed 가 있어야 하는데,
        ///   seed 는 매 판 새로 뽑히므로 이 줄을 안 남기면 영영 재현할 수 없다.
        ///
        /// ── 왜 아래 세 키와 나누는가 (LogRules.md 1.5 신설 기준 2개) ──
        ///   ① 집계가 섞이면 안 된다:
        ///      이 키는 나머지 셋의 **분모**다. "폴백을 쓴 비율"·"준비 실패율"은
        ///      정상 성공 건수와 나뉘어 있어야만 계산된다. 한 키로 묶으면 비율 자체가 사라진다.
        ///   ② 조치가 다르다:
        ///      이 키는 조치가 **없다**(정상 동작의 기록). 나머지 셋은 각각 다른 조치를 부른다.
        ///
        /// 축 A = Info (LogRules 1.2 — 의도된 흐름이므로 애초에 문제가 아니다)
        /// 발생 지점: Bootstrap/GameBootstrapper.Map.cs — PrepareAndProjectMap
        /// </summary>
        MapPreparationSucceeded,

        /// <summary>
        /// 생성 시도가 최대 횟수(100회)까지 전부 거부되어 **폴백 템플릿**으로 경기를 연다.
        /// 맵 자체는 검증을 통과한 정상 맵이므로 경기는 그대로 진행된다.
        ///
        /// ── 왜 MapPreparationSucceeded 와 한 키로 묶지 않는가 (신설 기준 2개 충족) ──
        ///   ① 집계가 섞이면 안 된다:
        ///      묶으면 「정상 생성 성공 건수」가 **비상구로 겨우 살아난 판**까지 포함해 부풀어 오른다.
        ///      그리고 서버 집계의 기본 단위는 이벤트 키다(LogRules 1.4 표). 매 판 올라오는
        ///      성공 줄에 이 드문 사건이 같은 키로 묻히면 **키 단위로는 보이지 않게 된다.**
        ///      폴백 사용률은 생성기·검증기의 **품질 지표**라 그 자체로 추적 대상이다.
        ///   ② 조치가 다르다:
        ///      정상 성공은 조치가 없고, 이쪽은 **그 맵 유형의 생성기·검증기 제약을 손보는**
        ///      일이 조치다(100회 연속 거부는 제약이 과하거나 생성기가 좁게 뽑는다는 신호다).
        ///
        /// 축 A = Warn (LogRules 1.2 — "복구되었나?" 에 예. 대체 경로로 계속 진행된다)
        /// 발생 지점: Bootstrap/GameBootstrapper.Map.cs — PrepareAndProjectMap
        /// </summary>
        MapPreparationUsedFallbackTemplate,

        /// <summary>
        /// 맵을 끝내 만들지 못했다. 폴백 템플릿까지 실패한 상태라 **경기를 진행할 수 없다**
        /// (성·시작 광산을 배치하지 않는다).
        ///
        /// ── 왜 위 두 키와 나누는가 (신설 기준 2개 충족) ──
        ///   ① 집계가 섞이면 안 된다:
        ///      위 둘은 "게임이 돌아갔다", 이쪽은 "경기가 성립하지 않았다" 다.
        ///      묶으면 **장애 지표**(맵이 없어 경기가 안 열린 횟수)가 만들어지지 않는다.
        ///   ② 조치가 다르다:
        ///      폴백 사용은 생성기 제약 조정이지만, 이쪽은 대개 **폴백 템플릿 에셋 자체**의
        ///      문제다(누락 · 포맷 불일치 · 낡은 템플릿). 조치는 템플릿 재생성·재배포다.
        ///
        /// 사유 7종은 키를 나누지 않고 key=value 의 ErrorCode= 로 가른다(위 그룹 주석 참조).
        /// 축 A = Error (LogRules 1.2 — 복구 경로가 없다) · LogRules 1.3 원칙 3
        /// 발생 지점: Bootstrap/GameBootstrapper.Map.cs — PrepareAndProjectMap
        /// </summary>
        MapPreparationFailed,

        /// <summary>
        /// 맵은 정상적으로 만들어졌는데 그것을 **격자(HexGrid)에 새기지 못했다.**
        /// 지형·광산이 하나도 반영되지 않으므로 성·시작 광산을 배치하지 않는다.
        ///
        /// ── 왜 MapPreparationFailed 와 한 키로 묶지 않는가 (신설 기준 2개 충족) ──
        ///   ① 집계가 섞이면 안 된다:
        ///      한쪽은 **생성 계통**(생성기·검증기·폴백 템플릿), 이쪽은 **격자·설정 계통**
        ///      (GameConfig 의 격자 크기 11x21, 헥스 방향)이다. 두 사건은 쓰는 필드부터 다르다 —
        ///      이쪽에는 ErrorCode(MapPreparationErrorCode) 라는 값 자체가 존재하지 않는다.
        ///      한 지표에 섞으면 "생성이 문제인가 설정이 문제인가"를 영원히 가를 수 없다.
        ///   ② 조치가 다르다:
        ///      이쪽의 조치는 **GameConfig 의 격자 크기·헥스 방향을 맵 정의와 맞추는** 일이다.
        ///      템플릿 재생성과는 손대는 파일부터 다르다.
        ///
        /// ⚠️ 축 B ① 에 대한 판단을 적어 둔다(뒷사람이 "설정 오류인데 왜 운영이냐"고 묻게 되므로).
        ///    실패 원인은 두 갈래다.
        ///      (a) 격자 크기·헥스 방향 불일치 — 모든 기기에서 똑같이 실패하므로 에디터에서
        ///          첫 실행에 잡힌다. 이 갈래만 보면 「설정 오류 = 개발」(1.3 원칙 3 단서)이 맞다.
        ///          그러나 이 갈래는 **애초에 플레이어 빌드까지 갈 수 없다**(100% 실패라 즉시 드러난다).
        ///      (b) 맵 정의의 타일 인덱스·팀 값이 격자에 안 맞는 경우 — 이쪽은 seed 로 파생된
        ///          데이터에 달려 있어 **그 seed 를 손에 넣기 전에는 재현할 수 없다.**
        ///    한 키가 두 갈래를 함께 담아야 하므로, **플레이어 빌드에 실제로 도달할 수 있는
        ///    갈래(b)를 기준**으로 운영으로 둔다. 개발로 내리면 (b)의 유일한 기록이 사라진다
        ///    (LogRules 1.14 금지 9 의 단서 — 하위 계층에 대응 로그가 없으면 중복이 아니다.
        ///     MapProjectionUseCase 는 순수 C# 이라 로그를 하나도 내지 않는다).
        ///
        /// 축 A = Error (LogRules 1.2 — 복구 경로가 없다)
        /// 발생 지점: Bootstrap/GameBootstrapper.Map.cs — PrepareAndProjectMap
        /// </summary>
        MapProjectionFailed,

        // ====================================================================
        // [J] 무작위 맵 멀티 전송 · 해시 대조 · Client 검증 (무작위 맵 3단계 E)
        //
        // 부여 근거: GameSystemRules/GameSystemRules_RandomMap.md 규칙 16
        //              (멀티플레이 맵 전송과 실패 복구 — 조각 전송 · timeout 10초 · 1회 재전송 ·
        //               해시 일치 시에만 씬 전환 · 부분 데이터 사용 금지)
        //            같은 문서 규칙 12 「로그 필수 항목」 중 **3단계에서 비로소 값이 생기는 2가지**
        //              (전송/재전송 횟수 · Host/Client 해시 비교 결과)
        //            _Tasks/2026-09-08/17_29_random-map-phase3-multiplayer/Plan.md §7-E
        //            LogRules.md 1.5 (신설 기준 2개) · 1.2 (두 축) · 1.3 (분류 원칙)
        //
        // ⚠️ 초급 개발자 주의 — 이 다섯 키가 왜 전부 「운영(Ops)」인가:
        //    LogRules 1.2 축 B 는 두 질문에 **둘 다 "예"** 여야 운영이라고 정한다.
        //      ① 플레이어 기기에서만 벌어지는가?
        //         → 예. 여기 적힌 사건은 전부 **Relay 를 사이에 둔 실기 2대**에서만 일어난다.
        //           timeout 과 조각 불완전 수신은 실제 회선의 지연·유실에서 나오고,
        //           맵 내용은 매 판 새로 뽑은 root seed 에서 나온다. 개발자가 에디터에서
        //           그 회선과 그 seed 를 동시에 손에 넣기 전에는 재현할 방법이 없다.
        //      ② 이 로그가 없으면 원인을 추적할 수단이 없는가?
        //         → 예. 규칙 16 이 **"seed·유형·내부 error code는 UI에 노출하지 않고 로그에만 남긴다"**
        //           고 못 박았다. 그래서 실패 팝업이 나중에 생기더라도(그 UI 는 이번 범위 밖이다)
        //           화면에는 "맵 준비에 실패했습니다" 한 문장만 뜨고 **원인은 로그에만 남는다.**
        //           LogRules 1.3 원칙 2 의 단서 — "원인이 그 로그에만 있으면 개발로 내리지 않는다" —
        //           가 그대로 적용되는 자리다. (UI 가 생긴 뒤에도 이 판정은 바뀌지 않는다.)
        //
        // 🔴 초급 개발자 주의 — 「한 판에 몇 줄이 남는가」 (LogRules 1.14 금지 9 확인):
        //    바로 위 [I] 그룹의 네 키는 전부 **결말**이라 한 판에 딱 한 줄만 남는다.
        //    이 그룹은 그 성질이 **똑같지 않으므로** 헷갈리지 않게 여기 적어 둔다.
        //      · 결말 키 4개 — MapTransferSucceeded / MapTransferFailed /
        //        MapHashMismatch / MapClientVerificationFailed 는 **서로 배타적**이다.
        //        한 번의 전송은 이 넷 중 정확히 하나로 끝난다.
        //      · MapTransferRetried **하나만 결말이 아니라 중간 상태 전이**다.
        //        그래서 한 판에 「재전송 1줄 + 결말 1줄」 = 최대 2줄이 남을 수 있다.
        //    이것은 금지 9 위반이 **아니다.** 금지 9 가 막는 것은 **같은 사건을 두 계층에서**
        //    두 번 적는 것인데, 여기 둘은 시점도 사건도 다르다
        //    (= "재전송이 일어났다" / "끝내 실패했다" 또는 "결국 성공했다").
        //    오히려 LogRules 1.14 금지 8 이 "상태 **전이** 시점에만 남긴다"고 요구하는 바로 그 자리다.
        //
        // ⚠️ 이 다섯 키는 **3단계 E 에서 선언만 한다.** 실제 호출은 F~H 에서 붙는다.
        //    E 단계(= NetworkMapTransfer 골격)의 스폰/디스폰 로그는 이 키를 쓰지 않는다 —
        //    그 사건은 에디터 2인 구성에서 그대로 재현되므로 축 B ① 이 "아니오"이고,
        //    따라서 **개발 축**(키를 받지 않는 쪽)이다 — LogRules 1.5 는 키를 운영 로그만 받는다고 정한다.
        // ====================================================================

        /// <summary>
        /// Host 가 만든 맵이 Client 에 **온전히 전달되고 해시까지 일치**해 전송이 끝났다.
        /// 전송 1회당 정확히 한 줄이다.
        ///
        /// 실패가 아닌데도 운영 로그인 이유는 둘이다.
        ///   ① 규칙 12 가 「전송/재전송 횟수」와 「Host/Client 해시 비교 결과」를 남기라고 요구한다.
        ///   ② 아래 실패 키들의 **분모**가 된다. 이 줄이 없으면 "몇 판 중 몇 판이 실패했나"를
        ///      낼 수 없고, 실패 건수의 절대값만으로는 회선이 나빠진 것인지 판단할 수 없다.
        ///
        /// ── 왜 [I] 그룹의 MapPreparationSucceeded 로 갈음하지 않는가 (신설 기준 2개 충족) ──
        ///   ① 집계가 섞이면 안 된다:
        ///      MapPreparationSucceeded 는 **Host 혼자 맵을 만들어 낸** 사건이고 싱글에서도 남는다.
        ///      이 키는 **그 맵이 상대에게 건너간** 사건이다. 한 키로 묶으면
        ///      「맵은 만들었는데 못 보낸 판」이 성공으로 집계돼 장애가 통째로 가려진다.
        ///   ② 조치가 다르다:
        ///      이쪽은 정상 종료라 코드 조치가 없고, 남기는 값 자체가 다르다 —
        ///      준비 쪽은 seed·맵 유형·소요 시간, 이쪽은 조각 수·재전송 횟수·해시 대조 결과다.
        ///
        /// 축 A = Info (LogRules 1.2 — 의도된 흐름)
        /// 발생 지점: Infrastructure/Network/NetworkMapTransfer.cs (3단계 F~G 에서 호출이 붙는다)
        /// </summary>
        MapTransferSucceeded,

        /// <summary>
        /// timeout(10초) 또는 조각 불완전 수신 때문에 **같은 준비 작업의 전체 데이터를 1회 재전송**했다.
        /// 규칙 16 이 재전송을 이 두 경우로만 허용하고 횟수도 1회로 못 박았다.
        ///
        /// 🔴 이 키만 「결말」이 아니다(그룹 머리말 참조). 이 줄 뒤에는
        ///    MapTransferSucceeded 또는 MapTransferFailed 가 한 줄 더 남는다.
        ///
        /// ── 왜 성공·실패 키에 RetryCount= 필드로 얹지 않고 키를 나눴는가 (신설 기준 2개 충족) ──
        ///   ① 집계가 섞이면 안 된다:
        ///      이 줄은 **재전송이 일어난 그 순간** 남는다. 결말 줄의 필드로만 두는 방식은
        ///      "결말 줄이 반드시 남는다"는 전제에 기대는데, 규칙 16 이 「연결 끊김은 즉시 실패」라고
        ///      정한 대로 **상대가 사라지면 결말 줄이 남지 않을 수 있다.** 그러면 회선 품질의
        ///      유일한 단서인 재전송 사실까지 함께 사라진다.
        ///      게다가 재전송은 **성공한 판에도 실패한 판에도** 생기므로, 필드로만 두면
        ///      「재전송 발생률」이라는 한 지표를 내려고 늘 두 키를 합쳐야 한다.
        ///   ② 조치가 다르다:
        ///      이쪽의 조치는 **조각 크기와 timeout 값 재검토**다. 둘 다 실측으로 정하는 값이고,
        ///      MapTransferFailed 의 조치인 「전송 경로·용량 한도 점검」과는 손대는 값부터 다르다.
        ///
        /// 축 A = Warn (LogRules 1.2 — 예상 밖이지만 **대체 경로(재전송)로 계속 진행**된다.
        ///        1.2 가 "재시도 성공"을 Warn 의 예로 직접 들고 있다. 그 재전송마저 실패하면
        ///        그때 MapTransferFailed 가 Error 로 따로 남으므로 심각도가 묻히지 않는다.)
        /// 발생 지점: Infrastructure/Network/NetworkMapTransfer.cs (3단계 F 에서 호출이 붙는다)
        /// </summary>
        MapTransferRetried,

        /// <summary>
        /// 두 번째 10초에도 조각이 다 모이지 않아 **전송이 끝내 실패**했다.
        /// 규칙 16 에 따라 전투 씬으로 전환하지 않고 로비를 유지한다 — 그 판은 성립하지 않는다.
        ///
        /// ── 왜 MapHashMismatch·MapClientVerificationFailed 와 한 키로 묶지 않는가 ──
        ///   ① 집계가 섞이면 안 된다:
        ///      이쪽은 **바이트가 다 도착하지 못한** 사건이고, 나머지 둘은 **다 도착한 뒤**
        ///      내용을 따지다 걸린 사건이다. 원인 계통이 「회선」과 「데이터」로 정반대라
        ///      한 지표에 섞으면 "망이 문제인가 코드가 문제인가"를 영영 가를 수 없다.
        ///   ② 조치가 다르다:
        ///      이쪽의 조치는 **전송 경로·용량 한도 점검**이다
        ///      (조각 크기, NGO RPC 실효 페이로드 상한, Relay 실효 MTU).
        ///
        /// 축 A = Error (LogRules 1.2 — 규칙 16 이 재전송을 1회로 제한해 **더 이상 복구 경로가 없다**)
        ///        · LogRules 1.3 원칙 3
        /// 발생 지점: Infrastructure/Network/NetworkMapTransfer.cs (3단계 F~G 에서 호출이 붙는다)
        /// </summary>
        MapTransferFailed,

        /// <summary>
        /// 조각은 모두 도착했는데 **Host 와 Client 의 최종 맵 해시가 다르다.**
        /// 규칙 16 은 이 경우를 「즉시 실패」로 정하고 **같은 데이터의 재전송을 금지**한다 —
        /// 같은 바이트를 다시 보내도 결과가 같기 때문이다.
        ///
        /// ── 왜 MapTransferFailed 와 나누는가 (신설 기준 2개 충족) ──
        ///   ① 집계가 섞이면 안 된다:
        ///      전송 자체는 **성공**했다. 실패한 것은 내용의 동일성이다. 전송 실패에 섞으면
        ///      「보냈는데 내용이 달랐던 횟수」 — 직렬화 계약이 깨졌다는 유일한 신호 — 가 사라진다.
        ///   ② 조치가 다르다:
        ///      조치는 **canonical 직렬화·해시 계약 점검**이다(MapVersion 불일치, 인코딩 순서,
        ///      바이트 정렬). 재전송이나 한도 조정은 규칙 16 이 아예 금지한 조치다.
        ///
        /// ⚠️ 해시는 **원본 32바이트로 대조**하고 로그 필드에는 **앞 16자만** 싣는다.
        ///    2단계 K 가 정한 규약을 그대로 잇는다(Bootstrap/GameBootstrapper.Map.cs 의 ToMapHashField).
        ///
        /// 축 A = Error (LogRules 1.2 — 재전송이 금지돼 복구 경로가 없다) · LogRules 1.3 원칙 3
        /// 발생 지점: Infrastructure/Network/NetworkMapTransfer.cs (3단계 G 에서 호출이 붙는다)
        /// </summary>
        MapHashMismatch,

        /// <summary>
        /// 해시까지 일치한 맵인데 **Client 쪽 검증에서 떨어졌다.**
        /// 세 갈래를 한 키가 담는다 — 역직렬화 실패 / 공정성(Validate) 실패 /
        /// Client 가 같은 seed 로 스스로 다시 만들어 대조하는 「D 방식」 재생성 불일치.
        ///
        /// ── 왜 앞의 두 실패 키와 나누는가 (신설 기준 2개 충족) ──
        ///   ① 집계가 섞이면 안 된다:
        ///      여기까지 왔다는 것은 **회선도 정상이고 바이트도 동일**하다는 뜻이다.
        ///      그런데도 떨어졌다면 남은 용의자는 생성기·검증기 자신뿐이다.
        ///      전송 계통 지표에 섞으면 그 신호가 회선 잡음에 묻힌다.
        ///   ② 조치가 다르다:
        ///      조치는 **생성기·검증기 수정**이고 손대는 파일이 Domain/Map 이다
        ///      (전송 쪽은 Infrastructure, 해시 쪽은 코덱이다).
        ///
        /// ⚠️ 세 갈래를 키로 더 쪼개지 않은 이유는 [I] 그룹이 실패 사유 7종을 쪼개지 않은 것과 같다.
        ///    전부 「받은 맵을 쓸 수 없어 경기가 성립하지 않았다」는 하나의 사건이고,
        ///    쪼개면 그 장애 지표 자체가 만들어지지 않는다. 어느 갈래였는지는 key=value 가 담는다.
        ///
        /// 축 A = Error (LogRules 1.2 — 규칙 16 이 즉시 실패로 정해 복구 경로가 없다) · 1.3 원칙 3
        /// 발생 지점: Infrastructure/Network/NetworkMapTransfer.cs (3단계 G~H 에서 호출이 붙는다)
        /// </summary>
        MapClientVerificationFailed
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
