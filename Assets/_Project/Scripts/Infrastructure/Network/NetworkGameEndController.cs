// ============================================================================
// NetworkGameEndController.cs
// 네트워크 모드에서 승패 결과를 서버에서 결정하여 모든 클라이언트에 동기화.
// 커스텀게임 재경기(Rematch) 요청/수락/거절 RPC 처리.
//
// 역할:
//   - 서버: OnGameEnd 이벤트 구독 → AnnounceWinnerClientRpc로 전체 전파
//   - 클라이언트: AnnounceWinnerClientRpc 수신 → GameEndUI 표시 (팀 보정 포함)
//   - 재경기: 양측 동의 시 서버가 Game 씬 재로드 (NGO SceneManager)
//
// 흐름:
//   [서버] Castle 파괴
//     → NetworkCombatController.EntityDiedClientRpc
//     → [서버] GameEndUseCase.OnBuildingDied → GameEvents.OnGameEnd
//     → [서버] NetworkGameEndController.OnGameEnd 수신
//     → AnnounceWinnerClientRpc(winnerTeamIndex, isRandomMatch)
//     → [모든 클라이언트] SetupRematchButton + ShowResult
//
// 재경기 흐름 (커스텀게임):
//   [요청자] RequestRematch() → RequestRematchServerRpc
//     → [서버] 첫 요청 기록 → NotifyRematchRequestedClientRpc (상대에게만)
//   [상대] 수락 → AcceptRematchServerRpc → BeginRematchMapPreparation() → (성공) StartRematch()
//   [상대] 거절 → DeclineRematchServerRpc → NotifyRematchDeclinedClientRpc (요청자에게만)
//   [양측 동시 요청] 두 번째 ServerRpc에서 BeginRematchMapPreparation() → (성공) StartRematch()
//
//   🔴 2026-09-15 (재경기 맵 D): 수락/상호 동의와 씬 재로드 **사이에 새 맵 준비·전송·검증이 끼었다.**
//      종전에는 수락 즉시 StartRematch() 를 불러 씬을 재로드했는데, 맵 인계 홀더(MapHandoff)를
//      다시 채워 주는 사람이 없어 **재경기 전장이 텅 비었다.**
//      실패하면 씬을 재로드하지 않고 결과 화면을 유지한다 → HandleRematchMapFailed()
//      (GameSystemRules_RandomMap.md 규칙 14 「재경기 맵」 절).
//
// 싱글플레이와의 관계:
//   싱글플레이 시 이 컴포넌트는 씬에 없거나 NetworkObject가 스폰되지 않으므로
//   기존 GameEndUseCase → GameEndUI 직접 구독 흐름이 그대로 작동.
//
// 배치:
//   씬에 빈 GameObject "NetworkGameEndController" 배치.
//   NetworkObject 컴포넌트 + 이 스크립트 부착.
//   NetworkManager의 씬 오브젝트로 자동 스폰.
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 네트워크 승패 판정 동기화 + 커스텀게임 재경기 컨트롤러.
    /// 서버에서 게임 종료를 감지하고 모든 클라이언트에 결과를 전파.
    ///
    /// UI는 직접 호출하지 않고 GameEvents 발행으로 처리한다(Infrastructure → Presentation 역방향 의존 회피).
    /// IForfeitService를 구현하여 InGameSettingsUI가 이 컨트롤러를 직접 알지 않아도 포기 요청을 전달할 수 있다.
    /// </summary>
    public class NetworkGameEndController : NetworkBehaviour, IForfeitService
    {
        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>게임 종료 이벤트 구독 해제용 Disposable.</summary>
        private System.IDisposable _gameEndSubscription;

        /// <summary>로컬 재경기 요청 이벤트 구독 해제용 Disposable.</summary>
        private System.IDisposable _localRematchRequestedSub;

        /// <summary>로컬 재경기 수락 이벤트 구독 해제용 Disposable.</summary>
        private System.IDisposable _localRematchAcceptedSub;

        /// <summary>로컬 재경기 거절 이벤트 구독 해제용 Disposable.</summary>
        private System.IDisposable _localRematchDeclinedSub;

        /// <summary>결과 발표 여부. 서버에서 중복 전파 방지용.</summary>
        private bool _announced = false;

        /// <summary>
        /// 재경기를 먼저 요청한 클라이언트 ID.
        /// ulong.MaxValue이면 아직 요청 없음.
        /// </summary>
        private ulong _rematchRequesterId = ulong.MaxValue;

        /// <summary>
        /// 🔴 <b>「상대가 떠났다」가 이미 확정됐는가.</b> 확정된 뒤에 도착한 재경기 요청·수락을
        /// <b>서버가 처리하지 않게</b> 막는 깃발이다.
        ///
        /// <para>
        /// <b>[초급자용 설명] 이 깃발이 없으면 무슨 일이 생기는가</b><br/>
        /// 결과 화면에서 상대가 이미 떠났는데 이쪽은 아직 모르는 구간(최대 30초)이 존재한다.
        /// 그 구간에 재경기 수락을 누르면 <b>내가 Host 냐 Client 냐에 따라 동작이 갈렸다.</b>
        /// <list type="bullet">
        ///   <item>수락자가 <b>Host</b>: 수락 ServerRpc 가 <b>자기 자신(서버)</b>에게 가므로 그대로 실행된다
        ///         → 이미 없는 상대를 위해 새 맵을 만들고 전송을 시작하고, 10초쯤 뒤에 응답 없음으로 실패한다.</item>
        ///   <item>수락자가 <b>Client</b>: 서버(Host)가 이미 없어 ServerRpc 가 <b>증발</b>한다 → 아무 일도 일어나지 않는다.</item>
        /// </list>
        /// 같은 상황인데 화면과 결과가 역할에 따라 달라지는 것이므로
        /// (TechnicalDesignDocument.md 「🔴 최상위 원칙」) 그 비대칭을 없애기 위해
        /// <b>서버 쪽에서 아예 처리하지 않는다.</b>
        /// </para>
        ///
        /// ⚠️ <b>진행 중이던 맵 준비를 취소하는 코드는 일부러 두지 않았다 — 이미 저절로 된다.</b>
        ///    이탈 판정은 연결을 함께 종료하고(규칙 17), 그때 맵 전송 객체도 함께 디스폰된다
        ///    (2026-09-22 로그 실측: 「무반응 이탈 확정」 → 「NetworkManager Shutdown 완료」 →
        ///     「맵 전송 객체 디스폰」이 0.01초 안에 연달아 일어난다).
        /// </summary>
        private bool _opponentLeftJudged;

        /// <summary>
        /// NetworkGameManager 참조.
        /// 서버 측 OnNetworkSpawn에서 1회만 탐색하여 캐시하고 이후에는 재탐색 없이 사용한다.
        /// OnGameEndServer에서 씬 전체 탐색을 반복하지 않기 위한 캐시.
        /// </summary>
        private Hexiege.Infrastructure.NetworkGameManager _networkGameManager;

        // ====================================================================
        // 결과 화면 무반응 이탈 자체 감시 — 설정값 (규칙 17 · 단계 6)
        // ====================================================================
        //
        // 🔴 왜 [SerializeField] 가 아니라 const 인가 — 이 프로젝트에서 두 번 걸린 함정이다.
        //    [SerializeField] 로 만들면 그 값은 씬(Game.unity)에 직렬화되고, 한 번 저장된
        //    뒤에는 **씬 값이 코드 기본값을 이긴다.** 그러면 코드의 30 을 고쳐도 동작은 그대로다
        //    (실제로 _autoReturnSeconds 30→60 이 코드와 씬 **양쪽**을 고쳐야 반영됐다).
        //    아래 30초는 규칙 17 이 정한 값이라 화면에서 조정할 이유가 없으므로,
        //    애초에 씬에 저장되지 않는 const 로 둬서 함정 자체를 없앴다.
        //    ✅ 그 결과 **이번 작업은 씬 수정이 필요하지 않다.**
        //
        // 🔴 이 값들은 **결과 화면 전용**이다. ReconnectionHandler._reconnectWaitSeconds(인게임
        //    재접속 대기 30초)와 숫자가 같지만 **다른 시계**이고, 필드도 클래스도 공유하지 않는다.
        //    로그에서 구분하기 위해 아래 ResultScreenWatchClockId 를 모든 관련 로그에 실어 보낸다.

        /// <summary>
        /// [결과 화면 전용] 상대 신호가 이만큼 없으면 「무반응」으로 본다(초).
        /// 규칙 17 「30초 무반응으로 이탈을 판정한다」가 정한 값이다.
        /// ⚠️ 이 값만으로 이탈이 확정되지 않는다 — 뒤에 인터넷 도달 확인이 한 번 더 붙는다.
        /// </summary>
        private const float ResultScreenSilenceTimeoutSeconds = 30f;

        /// <summary>
        /// [결과 화면 전용] 내 쪽에서 「나 아직 여기 있다」 신호를 보내는 간격(초).
        /// 30초 안에 열 번쯤 기회가 생기도록 잡았다 — 한두 개가 유실돼도 오판하지 않는다.
        /// </summary>
        private const float ResultScreenHeartbeatIntervalSeconds = 3f;

        /// <summary>[결과 화면 전용] 감시 루프가 한 바퀴 도는 간격(초).</summary>
        private const float ResultScreenWatchTickSeconds = 1f;

        /// <summary>
        /// [결과 화면 전용] 인터넷 도달 확인이 실패했을 때 다시 시도하기까지의 간격(초).
        /// 🔴 왜 간격을 두는가: 도달 확인은 실제 네트워크 요청이라 매 초 보내면 요청 제한에
        ///    걸리고 배터리도 먹는다. 실패했다는 것은 「기다린다」는 뜻이므로 급할 이유도 없다.
        /// </summary>
        private const float ResultScreenReachabilityRetryIntervalSeconds = 10f;

        /// <summary>
        /// 로그에 싣는 시계 식별자. 이 프로젝트에는 30초짜리 시계가 여럿이라
        /// (인게임 재접속 대기 30초 · 자동 로비 복귀 카운트다운) 로그만 보고는 구분이 안 된다.
        /// 그래서 이 감시가 남기는 모든 줄에 <c>Clock=ResultScreenLeaveWatch</c> 를 붙인다.
        /// </summary>
        private const string ResultScreenWatchClockId = "ResultScreenLeaveWatch";

        // ── 감시 상태 ────────────────────────────────────────────────────

        /// <summary>돌고 있는 감시 코루틴. null 이면 감시하지 않는 상태다.</summary>
        private Coroutine _resultScreenWatchCoroutine;

        /// <summary>
        /// 상대 신호를 마지막으로 받은 시각(초, <see cref="Time.realtimeSinceStartup"/> 기준).
        ///
        /// ⚠️ <b>realtime 을 쓰는 이유</b>: 결과 화면에서는 <c>Time.timeScale</c> 이 0 일 수 있고,
        ///    그러면 <c>Time.time</c> 이 아예 흐르지 않아 30초가 영영 지나지 않는다
        ///    (같은 이유로 GameEndUI 의 카운트다운과 NetworkMapTransfer 의 마감 시각도 realtime 을 쓴다).
        /// </summary>
        private float _lastOpponentSignalRealtime;

        /// <summary>내 신호를 마지막으로 보낸 시각(초, realtime 기준).</summary>
        private float _lastHeartbeatSentRealtime;

        /// <summary>다음 인터넷 도달 확인을 보낼 수 있는 가장 이른 시각(초, realtime 기준).</summary>
        private float _nextReachabilityProbeRealtime;

        /// <summary>
        /// 이번 결과 화면에서 이미 이탈을 확정했는가. 같은 판에서 두 번 확정하지 않기 위한 깃발.
        /// </summary>
        private bool _resultScreenLeaveJudged;

        /// <summary>
        /// 신호 전송 실패를 이미 로그로 남겼는가. 3초마다 같은 줄이 쌓이는 것을 막는 깃발이다
        /// (매 틱 로깅 금지 — LogRules 1.14 금지 8).
        /// </summary>
        private bool _heartbeatSendFailureLogged;

        /// <summary>
        /// 상대 이탈 알림(<c>GameEvents.OnNetworkOpponentLeft</c>) 구독 해제용 Disposable.
        /// 정상 퇴장 통보로 이미 알림이 온 뒤에는 감시를 계속할 이유가 없어 멈추는 데 쓴다.
        /// </summary>
        private System.IDisposable _opponentLeftSub;

        /// <summary>
        /// 인터넷 도달 확인기. 판정 직전에만 쓰므로 처음 필요할 때 만든다(지연 생성).
        /// </summary>
        private InternetReachabilityProbe _reachabilityProbe;

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 서버라면 OnGameEnd를 구독하고,
        /// 양측 모두 로컬 재경기 응답 이벤트(GameEvents.OnLocalRematch*)를 구독한다.
        /// UI 컴포넌트(GameEndUI/RematchRequestPopup/GameUIManager) 탐색 제거.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // [로그 이관] LogAudit.md §3-1 — 축 A=Info / 축 B=개발.
            // "[Network]" 접두사와 클래스 이름을 메시지에서 뺀 이유:
            // GameLog 가 [System/Class] 카테고리를 앞에 붙여 주므로 중복이다(LogRules.md 1.4 형식).
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController), "네트워크 스폰", $"IsServer={IsServer}");

            if (IsServer)
            {
                // 서버 측 OnNetworkSpawn에서 1회만 탐색하여 캐시.
                // OnGameEndServer 핸들러에서 매번 탐색하지 않도록 미리 보관.
                _networkGameManager = FindFirstObjectByType<Hexiege.Infrastructure.NetworkGameManager>();

                // 서버: 게임 종료 이벤트 구독 → 모든 클라이언트에 결과 전파
                _gameEndSubscription = GameEvents.OnGameEnd
                    .Subscribe(OnGameEndServer);

                GameLog.Dev.Info("Network", nameof(NetworkGameEndController), "서버 측 OnGameEnd 구독 완료");
            }

            // 로컬 측 재경기 응답 이벤트 구독 — UI가 이벤트를 발행하면 본 컨트롤러가
            // 적절한 ServerRpc로 변환하여 서버에 전달한다.
            //
            // 호스트(IsServer)도 자신의 로컬 UI 발행 신호를 ServerRpc로 그대로 보낸다.
            // (ServerRpc는 호스트에서 즉시 실행되므로 함수 호출과 동일하게 동작)
            _localRematchRequestedSub = GameEvents.OnLocalRematchRequested
                .Subscribe(_ => RequestRematchServerRpc());

            // 🔴 수락만 예외 처리로 감싼다 — 이 채널에만 **구독자가 둘**이기 때문이다
            //    (이 컨트롤러 + 결과 화면 GameEndUI). 아래 SendAcceptRematchSafely 의 주석 참조.
            _localRematchAcceptedSub = GameEvents.OnLocalRematchAccepted
                .Subscribe(_ => SendAcceptRematchSafely());

            _localRematchDeclinedSub = GameEvents.OnLocalRematchDeclined
                .Subscribe(_ => DeclineRematchServerRpc());

            // [규칙 17 · 단계 6] 상대 이탈 알림이 **어떤 경로로든** 도착하면 자체 감시를 멈춘다.
            //
            //   왜 필요한가: 상대가 로비 복귀 버튼으로 정상 퇴장하면 그 사실이 통보 RPC 로
            //   먼저 도착한다(단계 5). 그때 감시를 그대로 두면 30초 뒤에 같은 사건을 한 번 더
            //   확정해 OnNetworkOpponentLeft 가 두 번 발행된다.
            //
            //   🔴 이 구독을 쓴 이유는 **단계 5 의 통보 RPC 를 한 글자도 건드리지 않기 위해서**다.
            //      통보를 받는 쪽에 코드를 끼워 넣는 대신, 이미 발행되는 이벤트를 듣기만 한다.
            //      자체 감시가 스스로 발행한 이벤트도 여기로 돌아오므로 판정 후 정리까지 겸한다.
            //
            //   ⚠️ IsServer 가드 밖에 둔다 — Host 와 Client 가 모두 이 알림을 받을 수 있다.
            _opponentLeftSub = GameEvents.OnNetworkOpponentLeft
                .Subscribe(_ => OnOpponentLeftSignal());
        }

        /// <summary>
        /// 네트워크 디스폰 시 구독 해제 및 재경기 상태 초기화.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _gameEndSubscription?.Dispose();
            _gameEndSubscription = null;

            _localRematchRequestedSub?.Dispose();
            _localRematchRequestedSub = null;
            _localRematchAcceptedSub?.Dispose();
            _localRematchAcceptedSub = null;
            _localRematchDeclinedSub?.Dispose();
            _localRematchDeclinedSub = null;

            // [규칙 17 · 단계 6] 결과 화면 자체 감시 뒷정리.
            //   디스폰은 재경기로 씬이 재로드되거나 연결이 내려갈 때 온다. 어느 쪽이든
            //   더 이상 감시할 결과 화면이 없으므로 코루틴과 구독을 함께 정리한다.
            //   (정리하지 않으면 다음 판에서 지난 판의 감시가 되살아난다.)
            _opponentLeftSub?.Dispose();
            _opponentLeftSub = null;
            StopResultScreenLeaveWatch("네트워크 디스폰");

            _rematchRequesterId = ulong.MaxValue;

            // 이탈 확정 깃발도 함께 내린다. 디스폰은 재경기로 씬이 재로드될 때도 오므로,
            // 내리지 않으면 지난 판의 판정이 다음 판까지 따라와 재경기가 통째로 막힌다.
            _opponentLeftJudged = false;
        }

        // ====================================================================
        // 이벤트 핸들러 (서버 전용)
        // ====================================================================

        /// <summary>
        /// 서버에서 GameEndEvent를 수신하여 모든 클라이언트에 승자를 전파.
        /// NetworkGameManager.IsRandomMatchmaking을 읽어 게임 모드 정보도 함께 전달.
        /// </summary>
        private void OnGameEndServer(GameEndEvent e)
        {
            // 이 오브젝트가 아직 네트워크에 살아 있고, 내가 서버일 때만 진행한다.
            //   이 핸들러는 아래에서 AnnounceWinnerClientRpc 를 전송한다.
            //
            //   이유는 NetworkCombatController.Update() 진입부 가드와 같다 — 그쪽의 상세 주석 참조.
            //   (요약: IsServer 는 "내가 서버 역할인가" 이지 "이 오브젝트가 아직 살아 있는가" 가 아니다.
            //    NetworkManager.Shutdown() 이 불린 뒤에도 디스폰 전까지 IsServer 는 여전히 참이므로,
            //    위험 구간은 NetworkManager.Shutdown() 과 디스폰 사이다.)
            //
            //   ※ IsSpawned 를 앞에 두는 이유 — || 는 앞 조건이 참이면 뒤를 평가하지 않는다(단락 평가).
            //     스폰되지 않은 상태에서 IsServer 를 건드리지 않고 곧바로 반환하기 위해서다
            //     (NetworkUnit.cs:291 이 같은 이유로 같은 순서를 쓴다).
            //
            //   ※ 아래 _announced 가드와 목적이 다르다 — _announced 는 "중복 발표 방지",
            //     이 줄은 "네트워크 생존 확인" 이다. 그래서 합치지 않고 순서도 그대로 둔다.
            //     정상 경로에서는 승패 확정이 Shutdown 보다 수 초 앞서므로 승자 발표가 막히지 않는다.
            if (!IsSpawned || !IsServer) return;
            if (_announced) return;

            _announced = true;
            int winnerTeamIndex = (int)e.Winner;

            // 랜덤 매칭 여부 확인 — 재경기 버튼 분기에 사용.
            // OnNetworkSpawn에서 미리 캐시해 둔 참조를 사용 (매번 씬 탐색하지 않음).
            bool isRandomMatch = false;
            var ngm = _networkGameManager;
            if (ngm != null)
                isRandomMatch = ngm.IsRandomMatchmaking;

            // 승리 팀 · 인덱스 · 랜덤매칭 여부는 나중에 집계·필터링에 쓰일 값이므로
            // 문장에 섞지 않고 key=value 자리로 뺀다(LogRules.md 1.4 형식 — "key=value가 곧 전송 데이터다").
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "서버: 게임 종료 감지 — 결과 전파 시작",
                $"WinnerTeam={e.Winner}, WinnerTeamIndex={winnerTeamIndex}, IsRandomMatch={isRandomMatch}");

            // 모든 클라이언트에 승자 발표
            AnnounceWinnerClientRpc(winnerTeamIndex, isRandomMatch);

            // ----------------------------------------------------------------
            // [규칙 17 · 단계 6] 결과 화면 무반응 이탈 자체 감시를 켠다.
            //
            // 🔴 켜는 기준은 「결과 화면에 상대가 아직 있을 수 있는가」다.
            //    이 경로는 성(Castle)이 파괴돼 승패가 갈린 **정상 종료**다. 두 사람이 멀쩡히
            //    붙어 있는 상태로 결과 화면에 들어가므로, 그 뒤에 한쪽이 조용해지는 것은
            //    **새로운 사건**이고 감시할 값이 있다.
            //
            //    같은 기준으로 **포기 경로도 켠다**(ForfeitServerRpc 끝 참조) — 포기는
            //    「경기를 지겠다」는 뜻이지 「나간다」는 뜻이 아니어서 상대가 남아 있을 수 있다.
            //    **켜지 않는 자리는 ForceWin 하나뿐**이며, 그쪽은 상대의 연결 끊김이 곧 승리의
            //    원인이라 상대가 확실히 없다(그 메서드 끝의 주석 참조).
            //
            // ⚠️ 왜 AnnounceWinnerClientRpc 안이 아니라 별도 ClientRpc 인가:
            //    그 안에 넣으면 **세 호출처 전부**에서 감시가 켜진다. 인자로 가르는 방법도
            //    있었지만, 이미 bool 하나(isRandomMatch)를 받는 메서드에 bool 을 하나 더
            //    붙이면 호출부가 `(winnerTeamIndex, false, true)` 처럼 읽을 수 없는 모양이 된다.
            //    「감시를 켜라」를 **독립된 메시지**로 두면 켜는 자리와 켜지 않는 자리가
            //    호출부에서 그대로 보인다.
            // ----------------------------------------------------------------
            BeginResultScreenLeaveWatchClientRpc();
        }

        // ====================================================================
        // ClientRpc — 서버 → 모든 클라이언트
        // ====================================================================

        /// <summary>
        /// 서버에서 확정된 승리 팀 인덱스를 모든 클라이언트에 전송.
        /// 게임 모드에 따라 재경기 버튼 동작을 분기 설정.
        ///
        /// UI 컴포넌트는 GameEvents 이벤트를 각자 구독해 반응한다.
        ///     - OnGameEnd: GameEndUI(자체 구독)가 ShowResult/일시정지 처리,
        ///                  GameUIManager(자체 구독)가 열린 팝업을 닫음.
        ///     - OnNetworkRematchAvailable: GameEndUI(자체 구독)가 재경기 버튼 활성화.
        ///
        /// 호스트(서버)에서는 OnGameEnd가 이미 OnGameEndServer 호출 직전에 한 번 발행되었으므로,
        /// 본 핸들러에서는 클라이언트 측에서만 OnGameEnd를 재발행하여 동일한 UI 흐름이 작동하게 한다.
        /// </summary>
        /// <param name="winnerTeamIndex">승리한 팀의 TeamId 정수값 (Blue=1, Red=2)</param>
        /// <param name="isRandomMatch">랜덤 매칭 여부. true이면 재경기 버튼 숨김.</param>
        [ClientRpc]
        private void AnnounceWinnerClientRpc(int winnerTeamIndex, bool isRandomMatch)
        {
            TeamId winnerTeam = (TeamId)winnerTeamIndex;

            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "AnnounceWinnerClientRpc 수신",
                $"WinnerTeam={winnerTeam}, LocalTeam={LocalPlayerTeam.Current}, IsRandomMatch={isRandomMatch}");

            // 클라이언트(비서버)에서는 OnGameEnd가 발행되지 않았으므로 여기서 발행한다.
            // (서버에서는 OnGameEndServer 호출 직전 이미 OnGameEnd가 발행된 상태)
            // GameEndUI가 OnGameEnd를 구독해 ShowResult 등 표시/일시정지 처리.
            // GameUIManager는 OnGameEnd를 구독해 NotifyGameEnded()로 열린 팝업을 닫음.
            if (!IsServer)
            {
                GameEvents.OnGameEnd.OnNext(new GameEndEvent(winnerTeam));
            }

            // 재경기 버튼 설정 신호 — GameEndUI가 구독해 SetupRematchButton 호출.
            GameEvents.OnNetworkRematchAvailable.OnNext(new NetworkRematchAvailableEvent(isRandomMatch));

            // ⚠️ [규칙 17 · 단계 6] 결과 화면 이탈 감시는 **여기서 켜지 않는다.**
            //    이 ClientRpc 는 정상 종료 · ForceWin(상대 연결 끊김) · 포기 **세 경로 모두**가
            //    부르기 때문에, 여기에 넣으면 감시가 필요 없는 ForceWin 에서도 켜진다.
            //    감시를 켜는 신호는 별도 ClientRpc(BeginResultScreenLeaveWatchClientRpc)이며
            //    **정상 종료(OnGameEndServer)와 포기(ForfeitServerRpc) 두 자리에서만** 보낸다.
            //
            // 🔴 위 isRandomMatch 를 그 판별에 쓰지 않는다 — 정상 종료(커스텀)도 false,
            //    포기도 false 라 **애초에 구분되지 않는 값**이다. 「랜덤 매칭인가」는
            //    재경기 버튼을 감추기 위한 값이고 종료 사유와 아무 관계가 없다.
        }

        /// <summary>
        /// [양쪽에서 실행] 결과 화면 무반응 이탈 자체 감시를 시작하라는 신호.
        ///
        /// 🔴 <b>왜 별도 RPC 로 두는가</b>: 감시를 켜야 하는 경기는 <b>정상 종료</b>뿐인데
        ///    <c>AnnounceWinnerClientRpc</c> 는 정상 종료 · 강제 승리 · 포기 <b>세 경로</b>가
        ///    공유한다. 켜는 신호를 따로 두면 <b>호출부만 보고도</b> 어느 경로가 감시를 켜고
        ///    어느 경로가 켜지 않는지 알 수 있다.
        ///
        /// 🔴 <b>왜 Host 쪽에 따로 호출을 두지 않는가</b>: ClientRpc 본문은 <b>Host(서버 자신)에서도
        ///    로컬로 실행</b>된다. 그래서 서버가 이 RPC 를 한 번 보내면 Host 와 Client 양쪽에서
        ///    같은 감시가 시작된다 — 역할에 따라 갈리지 않는다(규칙 17 ①).
        ///
        /// 🔴 NGO 명명 규약상 ClientRpc 메서드 이름은 반드시 <c>ClientRpc</c> 로 끝나야 한다.
        /// </summary>
        [ClientRpc]
        private void BeginResultScreenLeaveWatchClientRpc()
        {
            // 여기에 if (IsServer) 같은 가드를 두지 않는다 — 두면 Host 가 스스로를 감시하지 않게 되고,
            // Host 가 사라졌을 때 남은 Client 만 판정하는 비대칭이 생긴다(자세한 이유는 아래 감시 절 머리말).
            StartResultScreenLeaveWatch();
        }

        // ====================================================================
        // 강제 승리 — 상대방 연결 끊김 시 서버에서 호출
        // ====================================================================

        /// <summary>
        /// 서버에서 상대방 연결 끊김으로 인해 남은 팀을 강제 승리 처리.
        /// ReconnectionHandler.OnClientDisconnected() 대기 타임아웃 후 호출.
        /// 연결 끊김 상황이므로 재경기 불가 — isRandomMatch=false로 전달하되
        /// 상대 부재로 재경기 요청 자체가 성립하지 않음.
        /// </summary>
        /// <param name="winnerTeamIndex">강제 승리할 팀의 TeamId 정수값 (Blue=1, Red=2)</param>
        public void ForceWin(int winnerTeamIndex)
        {
            if (!IsServer) return;
            if (_announced) return;

            _announced = true;
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "ForceWin 호출 — 상대 연결 끊김으로 강제 승리 처리",
                $"WinnerTeamIndex={winnerTeamIndex}");

            // ----------------------------------------------------------------
            // [버그 수정 2026-09-21] 호스트(서버) 측 결과 화면 미표시 + 전투 계속 진행 문제 해결
            // ----------------------------------------------------------------
            // [초급자용 설명] 바로 아래에서 부르는 AnnounceWinnerClientRpc 는 본문에서
            //   "서버가 아닐 때(!IsServer)"에만 OnGameEnd 를 발행하도록 되어 있다. 그래서:
            //     - 클라이언트(비서버): 조건 성립  → OnGameEnd 발행 → 결과 화면 정상 표시
            //     - 호스트(서버):       조건 불성립 → OnGameEnd 미발행 → 화면에 아무것도 뜨지 않고
            //                            전투도 계속 돈다. (NetworkCombatController 가 OnGameEnd 를
            //                            구독해 전투 틱을 정지시키므로, 발행이 없으면 정지도 없다.)
            //   정상 종료(성 파괴) 경로는 GameEndUseCase 가 OnGameEnd 를 먼저 발행하기 때문에
            //   이 문제가 드러나지 않는다. 그러나 ForceWin 은 GameEndUseCase 를 건너뛰고
            //   ClientRpc 만 곧바로 부르므로, 서버 측에서 OnGameEnd 를 직접 발행해 줘야 한다.
            //
            // 🔴 아래 포기 경로(ForfeitServerRpc)와 **원인도 해법도 똑같은 문제**다.
            //   그 자리의 "[버그 수정 2026-05-27] 호스트 측 GameEndUI 미표시 문제 해결" 주석 블록을
            //   함께 보라. 그때 포기 경로만 고쳐지고 ForceWin 에는 같은 수정이 들어가지 않아
            //   이 경로에만 남아 있던 버그다.
            //
            // [중복 처리가 없는 이유] 위에서 이미 _announced 를 true 로 만들었다. 따라서 이 발행을
            //   서버 자신의 구독자인 OnGameEndServer 가 다시 받아도 그 메서드 앞머리의 _announced
            //   가드에 걸려 즉시 반환된다 — 발표가 두 번 일어나지 않는다.
            //
            // int(winnerTeamIndex) → TeamId 변환은 이 파일 AnnounceWinnerClientRpc 가 쓰는 방식과
            // 동일한 캐스팅이다(새 변환 수단을 만들지 않는다).
            // ----------------------------------------------------------------
            TeamId winnerTeam = (TeamId)winnerTeamIndex;
            GameEvents.OnGameEnd.OnNext(new GameEndEvent(winnerTeam));

            // 연결 끊김 시 재경기 불가 — isRandomMatch=false
            AnnounceWinnerClientRpc(winnerTeamIndex, false);

            // 🔴 [규칙 17 · 단계 6] 여기서는 결과 화면 이탈 감시를 **일부러 켜지 않는다.**
            //    (BeginResultScreenLeaveWatchClientRpc 를 보내지 않는다는 뜻이다.)
            //
            //    [초급자용 설명] 이 메서드가 불린 이유 자체가 **상대의 연결이 끊겼다**는 것이다.
            //    즉 「상대가 없다」는 사실이 이미 확인됐고, 그것이 이 승리의 **원인**이다.
            //    그런데 결과 화면에서 감시를 켜면 30초 뒤에 **똑같은 사실을 한 번 더 판정**해
            //    「상대가 나갔습니다」를 다시 알리게 된다. 이미 끝난 이야기를 되풀이하는 것이고,
            //    같은 사건이 화면과 로그에 두 번 남아 원인을 읽는 사람을 헷갈리게 만든다.
            //    감시는 **정상 종료 뒤에 새로 생긴 침묵**을 잡기 위한 그물이지,
            //    이미 알고 있는 단절을 다시 확인하는 장치가 아니다.
        }

        // ====================================================================
        // 포기 (Forfeit) — 인게임 설정 메뉴에서 호출
        // ====================================================================

        /// <summary>
        /// 포기 요청. 로컬에서 InGameSettingsUI가 사용자에게 확인 후 호출.
        /// 내부적으로 ServerRpc를 전송하여 서버에서 자기 팀을 패배 처리한다.
        /// </summary>
        public void RequestForfeit()
        {
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController), "포기 요청 전송");
            ForfeitServerRpc();
        }

        /// <summary>
        /// 클라이언트의 포기 요청을 서버에서 처리.
        /// 포기자의 ClientId로 팀을 식별하고(Host=0=Blue, Client=Red), 반대 팀을 승리자로 전체 클라이언트에 발표.
        /// 이미 결과가 발표된 상태(_announced==true)라면 무시 — Castle 파괴 직후 포기 클릭 등의 경쟁 상황 방어.
        ///
        /// RequireOwnership=false: 이 NetworkObject는 서버 소유이므로
        /// 클라이언트가 호출할 수 있도록 명시적으로 허용해야 한다.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void ForfeitServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (_announced) return;

            ulong forfeiterId = rpcParams.Receive.SenderClientId;

            // ClientId → TeamId 매핑.
            //   Host(서버 호스트) = ClientId 0 = Blue 팀
            //   Client(접속자)     = Red 팀
            // 이 매핑은 NetworkGameManager에서도 동일하게 사용 중인 규약.
            TeamId forfeitTeam = (forfeiterId == 0) ? TeamId.Blue : TeamId.Red;
            TeamId winnerTeam = (forfeitTeam == TeamId.Blue) ? TeamId.Red : TeamId.Blue;

            _announced = true;
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "포기 처리",
                $"ForfeiterClientId={forfeiterId}, ForfeitTeam={forfeitTeam}, WinnerTeam={winnerTeam}");

            // ----------------------------------------------------------------
            // [버그 수정 2026-05-27] 호스트 측 GameEndUI 미표시 문제 해결
            // ----------------------------------------------------------------
            // 정상 종료 흐름(Castle 파괴)은 다음 경로를 거친다:
            //   GameEndUseCase → GameEvents.OnGameEnd 발행 → OnGameEndServer 수신
            //   → AnnounceWinnerClientRpc 호출
            // 이 경로에서는 서버(호스트)에서 OnGameEnd가 이미 발행된 상태이므로
            // AnnounceWinnerClientRpc 안의 "!IsServer" 가드(중복 발행 방지)가 의미를 가진다.
            //
            // 그러나 포기 흐름(ForfeitServerRpc)은 GameEndUseCase를 건너뛰고
            // 곧바로 AnnounceWinnerClientRpc를 호출한다. 그 결과:
            //   - 클라이언트(비서버): !IsServer == true → OnGameEnd 발행 → GameEndUI 표시 정상
            //   - 호스트(서버):       !IsServer == false → OnGameEnd 미발행 → GameEndUI 미표시 (버그)
            //
            // 따라서 포기 흐름에서는 서버 측에서도 OnGameEnd를 명시적으로 발행해야 한다.
            // OnGameEndServer는 위에서 _announced=true로 설정되었기 때문에
            // 이 발행을 다시 받아도 153행 가드에 의해 즉시 return → 중복 처리 없음.
            // ----------------------------------------------------------------
            GameEvents.OnGameEnd.OnNext(new GameEndEvent(winnerTeam));

            // 포기에 의한 정상 종료 — 재경기 신청은 가능하도록 isRandomMatch=false 전달.
            // (랜덤 매칭이라도 isRandomMatch는 NetworkGameManager에서 별도로 판단되지만,
            //  여기서는 보수적으로 false를 넣어 클라이언트가 재경기 버튼을 띄울 수 있게 함.
            //  실제 랜덤매칭 종료 처리는 기존 OnGameEndServer 경로와 동일하게 동작.)
            AnnounceWinnerClientRpc((int)winnerTeam, false);

            // ----------------------------------------------------------------
            // [규칙 17 · 단계 6] 포기 경로에서도 결과 화면 이탈 감시를 **켠다.**
            //
            // [초급자용 설명] 포기는 **「이 경기를 지겠다」는 뜻이고 「화면을 떠난다」는 뜻이 아니다.**
            //   바로 위에서 보듯 포기 뒤에도 재경기 버튼이 뜨므로(isRandomMatch=false),
            //   두 사람이 결과 화면에 그대로 머물며 재경기를 주고받을 수 있다.
            //   그러니 **그 뒤에 한쪽이 조용해지는 일은 여전히 일어날 수 있고 감지해야 한다.**
            //   켜지 않으면 규칙 17 이 없애려던 상황이 이 경로에만 되살아난다 —
            //   「재경기를 신청했는데 상대가 앱을 강제 종료 → 아무 통보도 없이 버튼이 잠긴 채
            //     자동 로비 복귀 카운트다운 만료까지 갇힌다」.
            //
            // 🔴 위 ForceWin 과 다른 점(한 문장으로): **ForceWin 은 상대의 연결 끊김이 곧 승리의
            //    원인이라 상대가 확실히 없고, 포기는 상대가 여전히 있을 수 있다.**
            //    그래서 저쪽은 켜지 않고 이쪽은 켠다.
            // ----------------------------------------------------------------
            BeginResultScreenLeaveWatchClientRpc();
        }

        // ====================================================================
        // 재경기 (Rematch) — 커스텀게임 전용
        // ====================================================================

        /// <summary>
        /// 클라이언트의 재경기 요청을 서버에서 처리.
        /// 첫 요청 시 상대에게 알림, 양측 모두 요청 시 즉시 재경기 시작.
        ///
        /// 🔴 <b>상대 이탈이 이미 확정된 뒤에는 아무것도 하지 않는다</b>
        ///    (<see cref="_opponentLeftJudged"/> 의 주석에 이유가 적혀 있다).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void RequestRematchServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong requesterId = rpcParams.Receive.SenderClientId;
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "RequestRematchServerRpc 수신",
                $"RequesterClientId={requesterId}, PreviousRequesterClientId={_rematchRequesterId}");

            // 🔴 「상대가 떠났다」가 확정된 뒤에 도착한 요청은 처리하지 않는다.
            //    받을 사람이 없는 요청이고, 여기를 통과시키면 양측 동의 분기로 빠져
            //    없는 상대를 위해 맵 준비가 시작될 수 있다.
            //    이 줄은 판정을 하지 않는다 — 이미 확정된 판정을 읽기만 한다.
            if (_opponentLeftJudged)
            {
                GameLog.Dev.Warn("Network", nameof(NetworkGameEndController),
                    "재경기 요청을 무시한다 — 상대 이탈이 이미 확정됐다",
                    $"RequesterClientId={requesterId}");
                return;
            }

            if (_rematchRequesterId == ulong.MaxValue)
            {
                // 첫 번째 요청 — 기록 후 상대에게 알림
                _rematchRequesterId = requesterId;
                ulong otherClientId = GetOtherClientId(requesterId);

                if (otherClientId != ulong.MaxValue)
                {
                    var clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new[] { otherClientId }
                        }
                    };
                    NotifyRematchRequestedClientRpc(clientRpcParams);
                }
            }
            else
            {
                // 상대도 이미 요청 → 상호 동의 — 재경기 성립
                GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                    "양측 재경기 동의 — 새 맵 준비를 시작한다");

                // 🔴 이 경로도 「수락이 접수됐다」와 **완전히 같은 사건**이므로 같은 통보를 쓴다.
                //
                //   [초급자용 설명] 재경기가 성립하는 길은 둘이다.
                //     ① 한 쪽이 요청 → 다른 쪽이 팝업에서 「수락」 (AcceptRematchServerRpc)
                //     ② 두 사람이 각자 「다시하기」를 눌러 요청이 겹침 (지금 이 분기)
                //   어느 길이든 **서버가 양측 신호를 다 받은 시점이 곧 성사 확정**이고,
                //   그 뒤에 일어나는 일(새 맵을 만들어 보내고 검증한다)도 완전히 같다.
                //   그러니 화면도 같아야 한다.
                //
                //   🔴 통보를 여기에 넣지 않으면: **같은 「맵 준비 중」인데 경로에 따라**
                //      상태 줄이 뜨기도 하고 안 뜨기도 한다. 게다가 이 경로에서는 자동 로비
                //      복귀 타이머가 멈추지 않아, 맵을 만드는 도중에 한쪽이 먼저 로비로 나가 버린다.
                //
                //   ⚠️ 발행을 BeginRematchMapPreparation() **앞**에 두는 이유는
                //      AcceptRematchServerRpc 와 같다 — 맵 준비가 즉시 실패하면 실패 통보가
                //      먼저 도착해 화면이 「실패 → 준비 중」 순서로 거꾸로 바뀐다.
                NotifyRematchAcceptedClientRpc();

                BeginRematchMapPreparation();
            }
        }

        /// <summary>
        /// 상대 클라이언트에게 재경기 요청이 들어왔음을 알림.
        /// 팝업으로 수락/거절 선택지 표시.
        ///
        /// GameEvents.OnNetworkRematchRequested를 발행하면 팝업이 자체 구독으로 ShowRequest를 호출한다.
        /// 수락/거절은 OnLocalRematchAccepted / OnLocalRematchDeclined 이벤트로 다시 본 컨트롤러에 전달된다.
        /// </summary>
        [ClientRpc]
        private void NotifyRematchRequestedClientRpc(ClientRpcParams clientRpcParams = default)
        {
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController), "재경기 요청 수신 — OnNetworkRematchRequested 발행");
            GameEvents.OnNetworkRematchRequested.OnNext(new NetworkRematchRequestedEvent());
        }

        /// <summary>
        /// 재경기 수락을 서버에서 처리.
        /// 🔴 [재경기 맵 D] 「즉시 재경기 시작」이 아니라 <b>새 맵 준비부터 시작</b>한다.
        /// 씬 재로드는 양쪽 검증이 끝난 뒤 <see cref="StartRematch"/> 에서 일어난다.
        ///
        /// 🔴 <b>상대 이탈이 이미 확정된 뒤에는 아무것도 하지 않는다</b> — 이것이
        ///    「Host 가 없는 상대를 위해 새 맵을 만드는」 비대칭을 없애는 자리다
        ///    (<see cref="_opponentLeftJudged"/> 의 주석에 두 역할의 동작 차이가 적혀 있다).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void AcceptRematchServerRpc()
        {
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "AcceptRematchServerRpc 수신 — 새 맵 준비를 시작한다");

            // 🔴 이탈 확정 뒤에 도착한 수락은 처리하지 않는다.
            //    통과시키면 서버가 이미 없는 상대를 위해 맵을 만들고 전송을 시작한 뒤
            //    응답 없음으로 실패한다(수락자가 Host 일 때만 그렇게 되므로 역할 비대칭이 된다).
            if (_opponentLeftJudged)
            {
                GameLog.Dev.Warn("Network", nameof(NetworkGameEndController),
                    "재경기 수락을 무시한다 — 상대 이탈이 이미 확정됐다");
                return;
            }

            // 🔴 「수락이 접수됐다」를 **양쪽 모두**에게 알린다 — 맵 준비를 시작하기 **전**이다.
            //
            //   [초급자용 설명] 이 통보를 정말 받아야 하는 사람은 **요청한 쪽**이다.
            //     요청자는 지금까지 상대가 수락했다는 사실을 알 길이 없어서, 수락이 됐는데도
            //     자기 자동 복귀 카운트다운(60초)이 만료되면 혼자 로비로 나가 버렸다.
            //     이 통보를 받으면 요청자도 상태 줄을 「재경기 준비 중...」으로 바꾸고
            //     타이머를 멈춘다(맵이 만들어지는 동안 기다려야 하므로).
            //
            //   🔴 대상을 요청자 한 쪽으로 좁히지 않는 이유는 NotifyRematchMapFailedClientRpc 와 같다.
            //      Host 도 ClientRpc 본문이 로컬에서 실행되므로 별도 호출 없이 함께 받는다.
            //      수락한 쪽은 버튼을 누른 즉시 이미 같은 상태에 들어가 있으므로 이 신호는 무시된다.
            //
            //   ⚠️ 맵 준비 **전**에 보내는 이유: BeginRematchMapPreparation() 은 실패하면
            //      그 자리에서 곧바로 실패 통보를 보낼 수 있다. 수락 통보를 뒤에 두면
            //      실패 통보가 먼저 도착해 화면이 「실패 → 준비 중」 순서로 거꾸로 바뀐다.
            NotifyRematchAcceptedClientRpc();

            BeginRematchMapPreparation();
        }

        /// <summary>
        /// 재경기 수락이 접수됐음을 <b>양쪽 모두</b>에게 알린다.
        ///
        /// <para>
        /// 🔴 <b>「사람을 기다리면 타이머가 돈다. 시스템을 기다리면 타이머가 멈춘다.」</b><br/>
        /// 이 통보가 화면에서 하는 일은 자동 로비 복귀 타이머를 <b>멈추는 것</b>이다.
        /// 요청만 해 둔 동안에는 <b>상대가 수락할지</b>를 기다리는 것이라 타이머가 돌아야 한다 —
        /// 상대가 팝업을 띄운 채 아무것도 누르지 않으면 답이 영영 오지 않기 때문이다.
        /// 수락이 접수된 뒤에는 <b>서버가 맵을 만드는 것</b>을 기다리는 것이라 멈춰야 한다 —
        /// 시스템 작업이고 끝나는 시점이 정해져 있다.
        /// </para>
        ///
        /// 🔴 NGO 명명 규약상 ClientRpc 메서드 이름은 반드시 <c>ClientRpc</c> 로 끝나야 한다.
        /// </summary>
        [ClientRpc]
        private void NotifyRematchAcceptedClientRpc()
        {
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "재경기 수락 접수 알림 수신 — OnNetworkRematchAccepted 발행");
            GameEvents.OnNetworkRematchAccepted.OnNext(Unit.Default);
        }

        /// <summary>
        /// 로컬 「수락」 입력을 <see cref="AcceptRematchServerRpc"/> 로 바꿔 보낸다.
        /// <b>예외가 나도 삼킨다.</b>
        ///
        /// <para>
        /// 🔴 <b>[초급자용 설명] 왜 이 하나만 감싸는가</b><br/>
        /// <c>GameEvents.OnLocalRematchAccepted</c> 는 이 프로젝트에서 <b>구독자가 둘인 유일한
        /// 로컬 재경기 채널</b>이다 — 이 컨트롤러(서버로 보내기)와 결과 화면 <c>GameEndUI</c>
        /// (화면을 「재경기 준비 중」으로 바꾸기). UniRx 의 <c>Subject</c> 는 구독자를 차례로
        /// 부르는데, <b>앞 구독자가 예외를 던지면 뒤 구독자는 아예 불리지 않는다.</b>
        /// </para>
        ///
        /// <para>
        /// 하필 예외가 나는 상황이 <b>정확히 이번에 고치려는 그 상황</b>이다 —
        /// Host 가 이미 떠나 연결이 내려가는 중에 RPC 를 보내면 NGO 가
        /// <i>"Rpc methods can only be invoked after starting the NetworkManager!"</i> 를 던진다.
        /// 그 예외가 화면 갱신을 막아 버리면 <b>수락을 눌러도 아무 반응이 없는</b>
        /// 원래 증상이 그대로 남는다. 그래서 보내기 실패는 여기서 끝내고 화면은 반드시 바뀌게 한다.
        /// </para>
        ///
        /// ⚠️ 같은 이유의 try/catch 가 이 파일 <see cref="SendResultScreenHeartbeat"/> 에도 있다
        ///    (그쪽은 감시 코루틴이 죽는 것을 막는다). 나머지 두 로컬 재경기 채널
        ///    (요청·거절)은 구독자가 이 컨트롤러 하나뿐이라 막을 뒤 구독자가 없어 감싸지 않는다.
        /// </summary>
        private void SendAcceptRematchSafely()
        {
            try
            {
                AcceptRematchServerRpc();
            }
            catch (System.Exception e)
            {
                // 삼킨 예외를 기록 없이 두지 않는다(LogRules 원칙 4).
                // 한 판에 최대 한 번 도달하는 경로라 스로틀이 필요 없다.
                GameLog.Dev.Warn("Network", nameof(NetworkGameEndController),
                    "재경기 수락을 서버로 보내지 못했다 — 재경기 실패로 화면에 알린다",
                    $"Exception={e.GetType().Name}");

                // 🔴 사용자에게 「재경기가 안 됐다」를 알린다 — 조용히 넘어가지 않는다.
                //
                //   [초급자용 설명] 재경기가 안 되는 길은 셋이다.
                //     ① 서버로 수락을 아예 못 보냈다 (여기)
                //     ② 서버가 새 맵 준비에 실패했다고 통보해 왔다
                //     ③ 준비 한도가 지났는데 아무 통보도 오지 않았다
                //   사용자는 이 셋을 구분할 수 없고 구분할 필요도 없다 — 전부 「재경기가 안 됐다」다.
                //   그래서 **셋이 같은 화면으로 끝나야** 한다. 그 화면을 만드는 통로가 이미 있으므로
                //   (아래) 새 채널을 만들지 않고 그것을 그대로 쓴다.
                //
                //   ⚠️ **왜 여기서는 RPC 가 아니라 로컬 발행인가** — 이 채널은 원래
                //      NotifyRematchMapFailedClientRpc 가 서버에서 쏘는 것이다. 그런데 여기까지
                //      온 이유 자체가 **RPC 를 보낼 수 없었다**는 것이다. 보낼 수 없는 수단으로
                //      실패를 알릴 수는 없으므로, 이 한 경로만 로컬에서 직접 발행한다.
                //      알릴 대상도 내 화면 하나뿐이다 — 상대는 애초에 닿지 않는다.
                GameEvents.OnNetworkRematchMapFailed.OnNext(Unit.Default);
            }
        }

        /// <summary>
        /// 재경기 거절을 서버에서 처리.
        /// 요청자에게 거절 알림 전송 + 요청 상태 초기화.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void DeclineRematchServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong declinerId = rpcParams.Receive.SenderClientId;
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "DeclineRematchServerRpc 수신",
                $"DeclinerClientId={declinerId}");

            // 요청 상태 초기화
            _rematchRequesterId = ulong.MaxValue;

            // 요청자에게 거절 알림
            ulong requesterId = GetOtherClientId(declinerId);
            if (requesterId != ulong.MaxValue)
            {
                var clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { requesterId }
                    }
                };
                NotifyRematchDeclinedClientRpc(clientRpcParams);
            }
        }

        /// <summary>
        /// 요청자에게 재경기 거절을 알림. 버튼 상태 복원.
        ///
        /// GameEvents.OnNetworkRematchDeclined를 발행하면 두 UI(RematchRequestPopup / GameEndUI)가 자체 구독으로 처리한다.
        /// </summary>
        [ClientRpc]
        private void NotifyRematchDeclinedClientRpc(ClientRpcParams clientRpcParams = default)
        {
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController), "재경기 거절 알림 수신 — OnNetworkRematchDeclined 발행");
            GameEvents.OnNetworkRematchDeclined.OnNext(Unit.Default);
        }

        // ====================================================================
        // 재경기 맵 준비 실패 — 상태 복원 (재경기 맵 C 단계)
        //
        // 규칙 근거:
        //   GameSystemRules_RandomMap.md 규칙 14 — "생성·전송·검증이 실패하면 교체하거나
        //     씬을 재로드하지 않고 기존 결과 화면과 기존 맵 정의를 유지한다."
        //   GameSystemRules_UI.md 「공통 UI 규칙」 규칙 M-3 — "이전 MapDefinition과 결과 화면을
        //     유지하고 rematch pending 상태를 초기화한다." · "결과 화면의 기존 선택지를 모두 복원한다."
        //
        // 🔴 이 경로가 하는 일은 **상태 복원**뿐이다. 실패를 알리는 팝업·문구·로딩 표시는
        //    범위 밖이다(규칙 M-3 의 "팝업 여부 미정" 표시). 카운트다운도 건드리지 않는다.
        //
        // ⚠️ **기존 맵 정의를 유지하기 위해 여기서 하는 일은 「아무것도 안 하는 것」이다.**
        //    실패한 회차는 MapHandoff.Set() 을 부르지 않으므로 홀더에는 새 값이 심기지 않는다.
        //    🔴 그러니 여기서 MapHandoff 를 건드리지 말 것 — 지우면 이미 확정된 값까지 날아간다.
        // ====================================================================

        /// <summary>
        /// [서버 전용] 재경기용 새 맵 준비·전송·검증이 실패했다.
        /// 씬을 재로드하지 <b>않고</b> 재경기 대기 상태를 초기화한 뒤, 양쪽 결과 화면을 되살린다.
        ///
        /// 🔴 <b>여기서 절대 <see cref="StartRematch"/> 로 넘어가면 안 된다.</b>
        ///    맵 없이 씬이 재로드되면 텅 빈 전장이 열린다(규칙 14 정면 위반).
        /// </summary>
        /// <param name="reason">진단용 사유 문자열(플레이어에게 보이는 문구가 아니다)</param>
        private void HandleRematchMapFailed(string reason)
        {
            // 이 콜백은 맵 전송 쪽에서 한참 뒤에 돌아온다. 그 사이에 로비로 돌아가거나
            // 연결이 끊겨 이 오브젝트가 디스폰됐을 수 있다 — 그 상태에서 아래 ClientRpc 를
            // 보내면 "Rpc methods can only be invoked after starting the NetworkManager!" 로 터진다.
            // IsSpawned 를 앞에 두는 이유는 단락 평가다(이 파일 OnGameEndServer 의 같은 가드 참조).
            if (!IsSpawned || !IsServer) return;

            // 🔴 규칙 M-3 "rematch pending 상태를 초기화한다".
            //    되돌리지 않으면 다음 요청이 「상대도 이미 요청했다」 분기로 빠져
            //    맵 준비 없이 곧바로 시작돼 버린다(= 다시 빈 맵).
            _rematchRequesterId = ulong.MaxValue;

            // [개발] 운영 축 결말 로그(MapTransferFailed 등)는 NetworkMapTransfer 가 이미 정확히
            //   한 줄 남겼다. 여기서 같은 사건을 운영으로 또 남기면 한 판이 두 번 세어진다
            //   (LogRules 1.14 금지 9). 다만 "재경기가 막혔다"는 사실 자체는 반드시 남긴다.
            GameLog.Dev.Warn("Network", nameof(NetworkGameEndController),
                "재경기 맵 준비 실패 — 씬을 재로드하지 않고 결과 화면을 유지한다",
                $"Reason={reason}");

            NotifyRematchMapFailedClientRpc();
        }

        /// <summary>
        /// 재경기 맵 준비 실패를 <b>양쪽 모두</b>에게 알려 결과 화면의 선택지를 되살리게 한다.
        ///
        /// ⚠️ <b>왜 대상을 한 쪽으로 좁히지 않는가</b>: 거절 알림(NotifyRematchDeclinedClientRpc)은
        ///    요청자에게만 가면 됐다. 맵 준비 실패는 다르다 — 요청한 쪽은 「요청 중...」으로 버튼이
        ///    잠겨 있고, 수락한 쪽은 「수락했는데 아무 일도 안 일어난」 상태다. 둘 다 되돌려야 한다.
        ///
        /// 호스트(서버)도 ClientRpc 본문이 로컬에서 실행되므로 별도 호출 없이 함께 복원된다
        /// (NotifyRematchStartingClientRpc 와 같은 방식).
        /// </summary>
        [ClientRpc]
        private void NotifyRematchMapFailedClientRpc()
        {
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "재경기 맵 준비 실패 알림 수신 — OnNetworkRematchMapFailed 발행");
            GameEvents.OnNetworkRematchMapFailed.OnNext(Unit.Default);
        }

        // ====================================================================
        // 재경기 맵 준비 — 🔴 여기서 「수락 즉시 씬 재로드」가 뒤집힌다 (재경기 맵 D 단계)
        // ====================================================================

        /// <summary>
        /// [서버 전용] 재경기가 성립했다 → <b>씬을 바로 재로드하지 않고</b> 새 맵부터 만든다.
        ///
        /// 순서(전부 이미 있는 코드다. 이번에 만든 것은 이 배선 하나뿐이다):
        ///   ① <see cref="NetworkGameManager.BeginRematchMapTransfer"/> 가 결과 화면 위에서
        ///      맵 전송 객체를 <b>새로 스폰</b>한다(재경기마다 다시 만든다 — 계획서 §4).
        ///   ② Host 가 새 root seed 로 맵을 만들어 Client 에게 조각으로 보낸다.
        ///   ③ 양쪽이 해시를 대조하고 Client 가 재생성·공정성 검증까지 통과한다.
        ///   ④ 양쪽이 각각 확정 맵을 <c>MapHandoff</c> 에 심는다.
        ///   ⑤ <b>그제서야</b> <see cref="StartRematch"/> 가 불려 씬이 재로드되고,
        ///      새 Game 씬의 GameBootstrapper 가 인계된 맵을 꺼내 격자에 새긴다.
        ///
        /// ✅ ⑤ 의 despawn 루프가 ① 에서 스폰한 전송 객체까지 <b>저절로</b> 정리한다 —
        ///    동적 스폰 NetworkObject 이기 때문이다. 그래서 그 루프에 예외를 팔 필요가 없다.
        ///
        /// 🔴 <b>실패하면 절대 <see cref="StartRematch"/> 로 넘어가지 않는다.</b>
        ///    맵 없이 씬을 재로드하면 지금까지 그랬던 것처럼 전장이 텅 빈다
        ///    (규칙 14 — *"실패하면 … 씬을 재로드하지 않는다"*).
        /// </summary>
        private void BeginRematchMapPreparation()
        {
            // OnNetworkSpawn(서버)에서 캐시해 둔 참조를 쓴다. 혹시 그때 못 찾았다면 여기서 한 번 더
            // 찾아본다 — NetworkGameManager 는 DontDestroyOnLoad 객체라 Game 씬 인스펙터로는
            // 연결할 수 없고, 런타임 탐색이 이 프로젝트의 관습이다(LobbyUI · GameEndUI 와 같은 방식).
            var manager = _networkGameManager;
            if (manager == null)
            {
                manager = FindFirstObjectByType<Hexiege.Infrastructure.NetworkGameManager>();
                _networkGameManager = manager;
            }

            if (manager == null)
            {
                // [개발] 배치 누락 = 설정 오류다(LogRules 1.3 원칙 3 단서 — Warn + 개발).
                //   🔴 여기서 StartRematch() 로 넘어가면 안 된다. 맵을 만들 주체가 없는데 씬만
                //      재로드하면 정확히 지금 고치려는 그 버그(빈 전장)가 재현된다.
                GameLog.Dev.Warn("Network", nameof(NetworkGameEndController),
                    "NetworkGameManager 를 찾을 수 없어 재경기 맵을 준비할 수 없다 — 결과 화면을 유지한다");
                HandleRematchMapFailed("NetworkGameManagerMissing");
                return;
            }

            // 결말 두 갈래를 넘긴다.
            //   성공 → StartRematch (🔴 그 메서드는 한 줄도 고치지 않았다)
            //   실패 → HandleRematchMapFailed (C 단계의 상태 복원 경로)
            manager.BeginRematchMapTransfer(StartRematch, HandleRematchMapFailed);
        }

        /// <summary>
        /// 서버에서 Game 씬을 재로드하여 재경기 시작.
        /// NGO SceneManager가 모든 클라이언트에 씬 전환을 동기화.
        ///
        /// ⚠️ <b>[재경기 맵 D] 이 메서드는 한 줄도 바뀌지 않았다.</b> 달라진 것은 <b>언제 불리는가</b>
        ///    뿐이다 — 종전에는 수락 즉시, 이제는 새 맵의 전송·검증이 모두 성공한 뒤에 불린다.
        ///    특히 아래 despawn 루프는 <b>절대 손대지 않는다</b>(계획서 §4-1).
        /// </summary>
        private void StartRematch()
        {
            _rematchRequesterId = ulong.MaxValue;

            // ----------------------------------------------------------------
            // LoadScene 이전에 모든 동적 스폰 NetworkObject를 명시적으로 Despawn.
            // NGO SceneManager.LoadScene(Single)으로 같은 씬("Game")을 재로드할 때
            // 동적 스폰 NetworkObject(유닛, 건물 등)가 자동 정리되지 않는 문제 수정.
            // ----------------------------------------------------------------
            // SpawnedObjects Dictionary를 직접 순회하면서 Despawn하면
            // 컬렉션이 변경되어 InvalidOperationException이 발생하므로,
            // 먼저 List에 복사한 뒤 복사본을 순회한다.
            // ----------------------------------------------------------------
            var spawnedCopy = new System.Collections.Generic.List<NetworkObject>(
                NetworkManager.SpawnManager.SpawnedObjects.Values);

            foreach (var netObj in spawnedCopy)
            {
                // IsSceneObject == true인 오브젝트는 씬에 미리 배치된 NetworkObject
                // (예: NetworkGameFlow, NetworkCombatController 등).
                // 이들은 씬 재로드 시 NGO가 자동으로 처리하므로 건드리지 않는다.
                //
                // null 체크: Despawn 과정에서 다른 오브젝트가 연쇄 파괴될 수 있으므로
                // Unity 오브젝트 유효성을 먼저 확인한다.
                // IsSpawned 체크: 이미 Despawn된 오브젝트를 중복 처리하지 않도록 한다.
                if (netObj != null && netObj.IsSpawned == true && netObj.IsSceneObject == false)
                {
                    // 이 로그는 **루프 안**에서 오브젝트 개수만큼 반복 출력된다.
                    // Dev 로 두는 이유: Dev 메서드에는 [Conditional] 두 개가 붙어 있어
                    // 릴리스 빌드에서는 호출과 인자 평가(netObj.name 접근·문자열 보간)까지
                    // 통째로 사라진다(LogRules.md 1.7 릴리스 스트리핑).
                    // 운영 로그로 두면 릴리스에서 매 오브젝트마다 로그가 쏟아져
                    // LogRules.md 1.14 금지 사항 8(매 틱·매 프레임 로깅 금지)에 걸린다.
                    GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                        "StartRematch: 동적 NetworkObject Despawn",
                        $"ObjectName={netObj.name}");
                    netObj.Despawn();
                }
            }

            // ----------------------------------------------------------------
            // [로딩 인디케이터] Game 씬 재로드 직전에 모든 클라이언트(서버 포함)에게
            // "재경기 준비 중..." 로딩 인디케이터를 띄우도록 신호를 보낸다.
            // 씬이 재로드되어 새 GameBootstrapper.LoadMap()이 완료되면 자동으로 꺼진다(UI 규칙 L-3).
            // ----------------------------------------------------------------
            NotifyRematchStartingClientRpc();

            GameLog.Dev.Info("Network", nameof(NetworkGameEndController), "StartRematch: Game 씬 재로드");
            NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
        }

        /// <summary>
        /// 재경기 시작을 모든 클라이언트(서버 포함)에 알려 로딩 인디케이터를 표시하게 한다.
        /// ClientRpc는 NetworkBehaviour(Infrastructure)에서만 호출 가능하다.
        /// UIManager(Presentation)를 직접 참조하지 않고 GameEvents(Application)를 경유 발행하여
        /// 레이어 방향(Infrastructure → Presentation 역행)을 어기지 않는다.
        /// GameEndUI가 OnNetworkRematchStarting을 구독해 ShowLoading(true)를 호출한다(UI 규칙 L-3).
        ///
        /// 호스트(서버)도 ClientRpc 본문이 로컬에서 실행되므로 별도 호출 없이 함께 로딩이 표시된다.
        /// </summary>
        [ClientRpc]
        private void NotifyRematchStartingClientRpc()
        {
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController), "NotifyRematchStartingClientRpc 수신 — 재경기 로딩 표시 신호 발행");
            GameEvents.OnNetworkRematchStarting.OnNext(Unit.Default);
        }

        // ====================================================================
        // 결과 화면에서의 상대 이탈 — 정상 퇴장 통보 (규칙 17)
        //
        // [초급자용 설명]
        //   경기가 끝나 결과 화면이 떠 있는 동안 상대가 「로비로 돌아가기」를 누르면,
        //   그 사실을 남아 있는 쪽 화면에 알려 줘야 한다. 알리지 않으면 남은 사람은
        //   응답이 오지 않을 재경기 요청을 붙들고 카운트다운 만료까지 기다리게 된다.
        //
        // 메시지 흐름 (GameSystemRules_RandomMap.md 규칙 17 의 「정상 퇴장」 갈래 ·
        //              TechnicalDesignDocument.md 「결과 화면 이탈 판정·통보 구조」):
        //   [나가는 쪽] NetworkGameManager.BackToLobby()
        //     → NotifyLeavingResultScreen()            … 이 파일의 공개 진입점
        //     → NotifyLeavingResultScreenServerRpc()    … 클라이언트 → 서버
        //   [서버] 남아 있는 쪽 한 명에게만
        //     → NotifyOpponentLeftClientRpc()           … 서버 → 클라이언트
        //   [받은 쪽] GameEvents.OnNetworkOpponentLeft 발행 → 결과 화면(Presentation)이 구독
        //
        // 🔴 Host 가 나가도 같은 길을 그대로 탄다 — Host 전용 경로를 만들지 않는다(규칙 17).
        //    ServerRpc 는 호스트에서 곧바로 로컬 실행되므로(이 파일 OnNetworkSpawn 의 주석 참조)
        //    호스트가 보낸 퇴장 통보도 위 흐름 그대로 상대에게 전달된다.
        //
        // ⚠️ 이번에 구현한 것은 「정상 퇴장」 갈래뿐이다. 무반응(앱 강제 종료·네트워크 단절)을
        //    서버가 스스로 지켜보다 이탈로 판정하고 연결까지 끊는 부분은 규칙 17 의 다른 갈래이며
        //    <b>아직 구현하지 않았다</b>. 그 갈래도 아래 NotifyOpponentLeftClientRpc 하나를
        //    그대로 재사용하게 된다(같은 통보 하나로 흡수 — 규칙 17).
        // ====================================================================

        /// <summary>
        /// [나가는 쪽에서 호출] 결과 화면에서 스스로 나가기 <b>직전</b>에 「나 지금 나간다」를 서버에 알린다.
        /// 호출처는 <c>NetworkGameManager.BackToLobby()</c> 한 곳이다.
        ///
        /// 🔴 <b>왜 IsSpawned 를 먼저 보는가</b>: NGO 는 NetworkManager 가 살아 있고 이 오브젝트가
        ///    스폰돼 있을 때만 RPC 송신을 허용한다. 아니면
        ///    <i>"Rpc methods can only be invoked after starting the NetworkManager!"</i> 로 예외가 난다
        ///    (같은 이유의 가드가 이 파일 HandleRematchMapFailed 에도 있다).
        /// </summary>
        public void NotifyLeavingResultScreen()
        {
            if (!IsSpawned)
            {
                // 이미 연결이 끊겼거나 디스폰된 뒤라면 보낼 수단 자체가 없다.
                // 이 경우 상대는 「무반응 이탈」 갈래로 판정하게 된다(규칙 17).
                GameLog.Dev.Warn("Network", nameof(NetworkGameEndController),
                    "정상 퇴장 통보를 보내지 못했다 — 이미 디스폰된 상태다");
                return;
            }

            GameLog.Dev.Info("Network", nameof(NetworkGameEndController), "정상 퇴장 통보 전송");
            NotifyLeavingResultScreenServerRpc();
        }

        /// <summary>
        /// [서버에서 실행] 결과 화면에서 나가는 클라이언트의 퇴장 통보를 받아 <b>상대 한 명에게만</b> 전달한다.
        ///
        /// RequireOwnership=false 인 이유는 이 NetworkObject 가 서버 소유라 그대로 두면
        /// 클라이언트가 호출할 수 없기 때문이다(이 파일의 다른 ServerRpc 들과 같은 이유).
        ///
        /// 🔴 NGO 명명 규약상 ServerRpc 메서드 이름은 반드시 <c>ServerRpc</c> 로 끝나야 한다.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void NotifyLeavingResultScreenServerRpc(ServerRpcParams rpcParams = default)
        {
            // 보낸 사람의 ClientId. 호스트가 보냈을 때도 NGO 가 로컬 ClientId 를 채워 준다
            // (이 파일 ForfeitServerRpc 가 같은 방식으로 포기자를 식별한다).
            ulong leaverId = rpcParams.Receive.SenderClientId;
            ulong otherClientId = GetOtherClientId(leaverId);

            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "NotifyLeavingResultScreenServerRpc 수신 — 상대에게 이탈을 알린다",
                $"LeaverClientId={leaverId}, TargetClientId={otherClientId}");

            // 🔴 서버는 이 순간 「한 명이 결과 화면을 떠났다」를 직접 알게 된다 — 여기서 곧바로
            //    이탈 확정 깃발을 세운다. 아래 ClientRpc 가 돌아오기를 기다리지 않는 이유는 둘이다.
            //      ① 바로 아래 줄에서 상대가 없으면(MaxValue) ClientRpc 를 보내지 않고 끝내므로,
            //         그 경로에서는 깃발을 세울 다른 기회가 없다.
            //      ② 서버가 이미 아는 사실을 RPC 왕복만큼 늦게 반영할 이유가 없다.
            //    (같은 깃발을 OnOpponentLeftSignal 도 세운다 — 무반응 판정 갈래를 받기 위해서다.
            //     bool 을 true 로 두 번 넣는 것은 아무 부작용이 없다.)
            _opponentLeftJudged = true;

            // 상대가 이미 없다면(먼저 나갔거나 끊긴 경우) 알릴 대상이 없다 — 조용히 끝낸다.
            if (otherClientId == ulong.MaxValue) return;

            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { otherClientId }
                }
            };
            NotifyOpponentLeftClientRpc(clientRpcParams);
        }

        /// <summary>
        /// [남아 있는 쪽에서 실행] 상대가 사라졌음을 화면에 알린다.
        ///
        /// UI 를 직접 건드리지 않고 <c>GameEvents.OnNetworkOpponentLeft</c> 를 발행하는 이유는
        /// Infrastructure 가 Presentation 을 직접 참조하면 레이어 방향이 역행하기 때문이다
        /// (이 파일의 다른 ClientRpc 들과 같은 방식).
        ///
        /// ⚠️ <b>이 신호를 받아 화면을 바꾸는 쪽(결과 화면)은 아직 없다</b> — 타이머 문구 교체와
        ///    재경기 버튼 비활성화는 다음 단계의 범위다. 지금은 통보가 여기까지 도달한다.
        ///
        /// 🔴 NGO 명명 규약상 ClientRpc 메서드 이름은 반드시 <c>ClientRpc</c> 로 끝나야 한다.
        /// </summary>
        [ClientRpc]
        private void NotifyOpponentLeftClientRpc(ClientRpcParams clientRpcParams = default)
        {
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "상대 이탈 알림 수신 — OnNetworkOpponentLeft 발행");
            GameEvents.OnNetworkOpponentLeft.OnNext(Unit.Default);
        }

        // ====================================================================
        // 결과 화면에서의 상대 이탈 — 무반응 이탈 자체 감시 (규칙 17 · 단계 6)
        //
        // [초급자용 설명 — 무엇을 하는 코드인가]
        //   결과 화면이 떠 있는 동안, 두 기기가 각자 서로에게 「나 아직 여기 있다」 신호를
        //   3초마다 보낸다. 그 신호가 30초 동안 한 번도 오지 않으면 뭔가 잘못된 것이다.
        //   그때 곧바로 「상대가 나갔다」고 단정하지 않고 **내 인터넷이 되는지 먼저 확인**한 뒤,
        //   내 인터넷이 멀쩡할 때만 상대 이탈로 확정하고 연결을 정리한다.
        //
        //   전체 그림 (GameSystemRules_RandomMap.md 규칙 17 의 ② 블록과 같은 그림):
        //     상대 신호 30초간 없음
        //       → 내 인터넷 도달 확인
        //           ├─ 된다    → 상대 이탈 확정 → 연결 종료 → OnNetworkOpponentLeft 발행
        //           └─ 안 된다 → 내 문제 → 연결을 끊지 않고 복구를 기다린다
        //
        // 🔴 [왜 if (IsServer) 가드를 두지 않는가 — 이 구조의 핵심]
        //   「서버가 감시하고 결과를 상대에게 RPC 로 알린다」가 자연스러워 보이지만 그러면
        //   **Host 가 사라진 경우에 아무도 알 수 없다.** 감시하던 서버도, 알려 줄 RPC 도
        //   Host 와 함께 사라지기 때문이다. 남은 Client 는 이유도 모르고 갇힌다.
        //   이 게임은 P2P 라 **플레이어는 자기가 Host 인지 Client 인지 알 수 없으므로**
        //   같은 상황에서 화면이 달라지면 그것만으로 결함이다
        //   (TechnicalDesignDocument.md 「🔴 최상위 원칙」 · 규칙 17 ①).
        //   → 그래서 **판정은 양쪽이 각자** 한다. 아래 감시 루프에는 역할 가드가 없다.
        //
        //   ⚠️ 단 하나 역할에 따라 갈리는 것은 **신호를 실어 보내는 RPC 의 방향**이다.
        //      NGO 는 「서버 → 클라이언트(ClientRpc)」와 「클라이언트 → 서버(ServerRpc)」
        //      두 방향만 제공하므로, 내가 서버면 ClientRpc 로, 클라이언트면 ServerRpc 로 보낸다.
        //      **판정하는 코드는 양쪽이 똑같고, 다른 것은 배달 수단뿐이다.**
        //
        // 🔴 [왜 도달 확인이 판정보다 먼저인가]
        //   규칙 17 은 「이탈로 판정하면 연결도 함께 종료한다」고 정하고 있다. 그래서 오판은
        //   문구가 잘못 뜨는 데서 끝나지 않고 **돌아올 수 있었던 연결을 내가 스스로 끊는 것**이
        //   된다. 내 와이파이가 30초 깜빡였을 뿐인데 상대는 멀쩡히 기다리고 있고 나는 재경기도
        //   못 하게 되는 상황이다. 판정을 한 단계 늦추는 비용보다 이쪽 손해가 훨씬 크다.
        //
        // ✅ [단계 5 의 정상 퇴장 통보와의 관계 — 대체가 아니라 보완]
        //   위쪽 NotifyLeavingResultScreen / …ServerRpc / NotifyOpponentLeftClientRpc 는
        //   그대로 살아 있다. 그쪽은 상대가 **정상적으로 나갈 때 즉시** 알리는 길이고,
        //   이 감시는 **그 통보가 오지 못한 경우를 받는 그물**이다(규칙 17 ①).
        //
        // [브로드캐스트를 두지 않은 이유]
        //   무반응 갈래에서는 판정 결과를 상대에게 알리는 RPC 를 **일부러 만들지 않았다.**
        //   ① 양쪽이 같은 감시를 각자 돌리므로 상대도 스스로 같은 결론에 도달한다.
        //   ② 알릴 상대는 「응답이 30초간 없는 상대」다 — 그 통보가 닿는다는 보장이 없고,
        //      닿을 상태라면 애초에 무반응이 아니다.
        //   ③ 판정과 동시에 연결을 끊으므로(규칙 17) 보낼 통로 자체가 곧 사라진다.
        //   → 즉 있어도 도착하지 않고, 도착한다면 필요 없는 메시지다.
        // ====================================================================

        /// <summary>
        /// [양쪽 공통] 결과 화면 무반응 이탈 감시를 시작한다.
        /// 호출처는 <c>BeginResultScreenLeaveWatchClientRpc</c> 한 곳이며, 그 ClientRpc 는
        /// Host 에서도 로컬 실행되므로 이 메서드는 Host·Client 양쪽에서 각각 한 번씩 불린다.
        ///
        /// ⚠️ 그 RPC 를 보내는 자리는 <b>두 곳</b>이다 — 정상 종료(<c>OnGameEndServer</c>)와
        ///    포기(<c>ForfeitServerRpc</c>). 둘 다 <b>결과 화면에 상대가 아직 있을 수 있는</b> 경기다.
        ///    보내지 않는 자리는 <c>ForceWin</c> <b>하나뿐</b>이며, 그쪽은 상대의 연결 끊김이
        ///    곧 승리의 원인이라 <b>같은 사실을 다시 판정하는 것</b>이 되기 때문이다
        ///    (각 메서드 끝의 주석 참조).
        ///
        /// 🔴 <b>역할 가드를 두지 않는다</b> — 이유는 이 절 머리말의 「왜 if (IsServer) 가드를
        ///    두지 않는가」 참조.
        /// </summary>
        private void StartResultScreenLeaveWatch()
        {
            // 싱글플레이(미스폰)나 이미 연결이 내려간 상태에서는 감시할 대상이 없다.
            // IsSpawned 를 앞에 두는 이유는 이 파일의 다른 가드와 같다(단락 평가).
            if (!IsSpawned) return;

            // 중복 시작 방지 — 같은 결과 화면에서 두 번 불릴 일은 없지만,
            // 코루틴이 두 개 돌면 신호를 두 배로 보내고 로그도 두 줄씩 남는다.
            if (_resultScreenWatchCoroutine != null) return;

            float now = Time.realtimeSinceStartup;
            _lastOpponentSignalRealtime = now;   // 시작 시점을 기준으로 30초를 센다
            _lastHeartbeatSentRealtime = 0f;     // 첫 바퀴에서 곧바로 한 번 보내게 한다
            _nextReachabilityProbeRealtime = 0f; // 도달 확인도 첫 판정 때 바로 할 수 있게 한다
            _resultScreenLeaveJudged = false;
            _heartbeatSendFailureLogged = false;

            _resultScreenWatchCoroutine = StartCoroutine(ResultScreenLeaveWatchLoop());

            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "결과 화면 이탈 감시 시작",
                $"Clock={ResultScreenWatchClockId}, TimeoutSeconds={ResultScreenSilenceTimeoutSeconds}, " +
                $"HeartbeatIntervalSeconds={ResultScreenHeartbeatIntervalSeconds}, IsServer={IsServer}");
        }

        /// <summary>
        /// [양쪽 공통] 감시를 멈춘다. 이미 멈춰 있으면 아무 일도 하지 않는다(멱등).
        /// </summary>
        /// <param name="reason">로그에 남길 중단 사유(사람이 읽는 문장).</param>
        private void StopResultScreenLeaveWatch(string reason)
        {
            if (_resultScreenWatchCoroutine == null) return;

            StopCoroutine(_resultScreenWatchCoroutine);
            _resultScreenWatchCoroutine = null;

            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "결과 화면 이탈 감시 중단",
                $"Clock={ResultScreenWatchClockId}, Reason={reason}");
        }

        /// <summary>
        /// [양쪽 공통] 「상대가 떠났다」 신호를 받았을 때의 <b>내부(Infrastructure) 처리</b>.
        /// 화면 처리는 <c>GameEndUI</c> 가 같은 이벤트를 따로 구독해서 한다.
        ///
        /// <para>하는 일은 둘이다.</para>
        /// <list type="number">
        ///   <item><b>이탈 확정 깃발을 세운다</b>(<see cref="_opponentLeftJudged"/>) — 이 뒤에 도착하는
        ///         재경기 요청·수락은 서버가 처리하지 않는다. 그 필드의 주석에 이유가 적혀 있다.</item>
        ///   <item><b>결과 화면 이탈 감시를 멈춘다</b> — 이미 결론이 난 사건을 다시 판정할 이유가 없다.</item>
        /// </list>
        ///
        /// <para>
        /// [초급자용 설명] 왜 이 한 자리에 모으는가 — 상대가 사라지는 길은 두 갈래다.
        /// ① 상대가 로비 복귀 버튼으로 <b>정상 퇴장</b>해서 통보 RPC 가 온 경우,
        /// ② 30초 무반응을 <b>내 쪽에서 직접 판정</b>한 경우. 두 갈래 모두 마지막에는
        /// <c>GameEvents.OnNetworkOpponentLeft</c> 를 발행하므로, 그 이벤트 하나만 들으면
        /// 두 갈래를 모두 받을 수 있다. 갈래마다 코드를 심으면 한쪽을 빠뜨리게 된다.
        /// </para>
        /// </summary>
        private void OnOpponentLeftSignal()
        {
            _opponentLeftJudged = true;
            StopResultScreenLeaveWatch("상대 이탈 알림을 받았다");
        }

        /// <summary>
        /// [양쪽 공통] 감시 본체. 1초마다 ① 내 신호 보내기 ② 상대 침묵 시간 검사를 한다.
        ///
        /// ⚠️ <b>WaitForSecondsRealtime 을 쓰는 이유</b>: 결과 화면에서는 <c>Time.timeScale</c> 이
        ///    0 일 수 있고, 그러면 일반 <c>WaitForSeconds</c> 는 영원히 끝나지 않는다.
        /// </summary>
        private System.Collections.IEnumerator ResultScreenLeaveWatchLoop()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(ResultScreenWatchTickSeconds);

                // 연결이 이미 내려갔거나 디스폰됐다면 감시할 것이 없다.
                // 🔴 가드 자체에는 로그를 남기지 않는다 — 매 틱 로깅 금지(LogRules 1.14 금지 8).
                if (!IsSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                {
                    _resultScreenWatchCoroutine = null;
                    yield break;
                }

                float now = Time.realtimeSinceStartup;

                // ── ① 내 신호 보내기 ─────────────────────────────────────
                if (now - _lastHeartbeatSentRealtime >= ResultScreenHeartbeatIntervalSeconds)
                {
                    _lastHeartbeatSentRealtime = now;
                    SendResultScreenHeartbeat();
                }

                // ── ② 상대 침묵 시간 검사 ────────────────────────────────
                float silenceSeconds = now - _lastOpponentSignalRealtime;
                if (silenceSeconds < ResultScreenSilenceTimeoutSeconds) continue;

                // 여기부터가 「30초 무반응」이다. 아직 이탈이 아니라 **의심**일 뿐이다.
                // 도달 확인은 실제 네트워크 요청이므로 재시도 간격을 지킨다.
                if (now < _nextReachabilityProbeRealtime) continue;
                _nextReachabilityProbeRealtime = now + ResultScreenReachabilityRetryIntervalSeconds;

                GameLog.Dev.Warn("Network", nameof(NetworkGameEndController),
                    "상대 신호가 끊겼다 — 판정 전에 내 인터넷 도달을 먼저 확인한다",
                    $"Clock={ResultScreenWatchClockId}, SilenceSeconds={silenceSeconds:F1}, " +
                    $"TimeoutSeconds={ResultScreenSilenceTimeoutSeconds}");

                // 도달 확인 결과를 기다린다.
                //   ⚠️ 코루틴에서 async 메서드를 기다리는 방법으로 「Task 가 끝날 때까지 프레임을
                //      넘긴다」를 쓴다. async void 로 빼지 않는 이유는 그렇게 하면 예외가 조용히
                //      사라지고, 판정 순서(확인 → 결론)도 코드에서 보이지 않게 되기 때문이다.
                if (_reachabilityProbe == null) _reachabilityProbe = new InternetReachabilityProbe();
                var probeTask = _reachabilityProbe.CheckAsync();
                while (!probeTask.IsCompleted) yield return null;

                // 확인기는 예외를 던지지 않도록 만들어 뒀지만, 만약 그래도 터졌다면
                // 「확인하지 못했다」와 같게 다룬다 — 확실하지 않으면 연결을 끊지 않는다.
                if (probeTask.IsFaulted || probeTask.IsCanceled)
                {
                    GameLog.Dev.Warn("Network", nameof(NetworkGameEndController),
                        "도달 확인이 예외로 끝났다 — 연결을 끊지 않고 기다린다",
                        $"Clock={ResultScreenWatchClockId}");
                    continue;
                }

                if (probeTask.Result != InternetReachabilityResult.Reachable)
                {
                    // 🔴 여기서 절대 연결을 끊지 않는다. 상대가 아니라 내가 문제일 수 있고,
                    //    끊어 버리면 돌아올 수 있었던 연결이 되돌릴 수 없게 된다(규칙 17 ②).
                    //    침묵 타이머도 일부러 되돌리지 않는다 — 상대는 여전히 조용하므로,
                    //    인터넷이 돌아오는 순간 곧바로 판정할 수 있어야 한다.
                    GameLog.Dev.Warn("Network", nameof(NetworkGameEndController),
                        "내 인터넷 도달을 확인하지 못했다 — 내 문제로 보고 연결을 유지한 채 기다린다",
                        $"Clock={ResultScreenWatchClockId}, ProbeResult={probeTask.Result}, " +
                        $"RetryAfterSeconds={ResultScreenReachabilityRetryIntervalSeconds}");
                    continue;
                }

                // 내 인터넷은 되는데 상대만 조용하다 → 상대 이탈로 확정한다.
                ConfirmOpponentLeftAfterSilence(silenceSeconds);
                yield break;
            }
        }

        /// <summary>
        /// [양쪽 공통] 상대 이탈을 확정하고 화면에 알린 뒤 연결을 종료한다.
        ///
        /// 순서가 「알림 → 연결 종료」인 이유: 알림은 로컬 이벤트(<c>GameEvents</c>)라
        /// 네트워크와 무관하지만, 연결 종료가 먼저 일어나면 이 컴포넌트가 디스폰되면서
        /// 뒤 코드가 실행되지 않을 수 있다. 상대에게 보내는 메시지가 아니므로 순서를
        /// 바꿔도 상대 쪽 동작에는 영향이 없다.
        /// </summary>
        /// <param name="silenceSeconds">판정 시점의 침묵 시간(초). 로그용.</param>
        private void ConfirmOpponentLeftAfterSilence(float silenceSeconds)
        {
            if (_resultScreenLeaveJudged) return;
            _resultScreenLeaveJudged = true;

            GameLog.Dev.Warn("Network", nameof(NetworkGameEndController),
                "무반응 이탈 확정 — 내 인터넷은 되고 상대만 조용하다",
                $"Clock={ResultScreenWatchClockId}, SilenceSeconds={silenceSeconds:F1}, IsServer={IsServer}");

            // 1. 화면에 알린다(Presentation 은 이 이벤트를 구독한다 — 단계 7 의 몫).
            //    Infrastructure 가 UI 를 직접 부르면 레이어 방향이 역행하므로 GameEvents 를 쓴다.
            //    ⚠️ 이 발행은 위 OnNetworkSpawn 의 구독으로 되돌아와 감시를 멈추게 한다(의도된 동작).
            GameEvents.OnNetworkOpponentLeft.OnNext(Unit.Default);

            // 2. 연결을 종료한다 — 규칙 17 「이탈로 판정하면 연결도 함께 종료한다」.
            //    살려 두면 「나갔다고 표시했는데 응답이 뒤늦게 도착하는」 상태가 생긴다.
            //
            //    🔴 NetworkBehaviour 안에서 NetworkManager.Shutdown() 을 직접 부르면 디스폰
            //       타이밍 문제가 있어, DontDestroyOnLoad 인 NetworkGameManager 에 위임한다
            //       (그 클래스 BackToLobby 의 주석이 같은 이유를 적고 있다).
            var ngm = ResolveNetworkGameManager();
            if (ngm == null)
            {
                GameLog.Dev.Warn("Network", nameof(NetworkGameEndController),
                    "연결 종료를 건너뛴다 — NetworkGameManager 를 찾지 못했다",
                    $"Clock={ResultScreenWatchClockId}");
                return;
            }

            ngm.ShutdownNetwork();
        }

        /// <summary>
        /// [양쪽 공통] 내 쪽 「아직 여기 있다」 신호를 보낸다.
        ///
        /// 🔴 여기서만 역할에 따라 갈린다 — NGO 의 RPC 는 방향이 정해져 있어서
        ///    서버는 ClientRpc 로만, 클라이언트는 ServerRpc 로만 보낼 수 있다.
        ///    <b>판정 로직이 갈리는 것이 아니라 배달 수단이 갈리는 것</b>이다.
        /// </summary>
        private void SendResultScreenHeartbeat()
        {
            // 🔴 왜 try/catch 로 감싸는가: 연결이 내려가는 도중에 RPC 를 보내면 NGO 가
            //    "Rpc methods can only be invoked after starting the NetworkManager!" 로 예외를 던진다.
            //    그 예외가 코루틴 안에서 터지면 **감시 루프가 조용히 죽어** 아무도 이탈을 판정하지
            //    못하게 된다. 보내기 실패는 감시를 멈출 이유가 아니므로 삼키고 계속 지켜본다.
            //    (삼킨 예외를 기록 없이 두지 않도록 첫 실패 한 번은 반드시 로그로 남긴다 —
            //     LogRules 원칙 4. 매 3초마다 같은 줄이 쌓이면 1.14 금지 8 에 걸리므로 한 번만 남긴다.)
            try
            {
                if (IsServer)
                {
                    // 서버 → 클라이언트. 2인 게임이라 대상 지정 없이 보내도 받는 사람은 한 명이다.
                    ResultScreenHeartbeatClientRpc();
                }
                else
                {
                    // 클라이언트 → 서버.
                    ResultScreenHeartbeatServerRpc();
                }
            }
            catch (System.Exception e)
            {
                if (_heartbeatSendFailureLogged) return;
                _heartbeatSendFailureLogged = true;

                GameLog.Dev.Warn("Network", nameof(NetworkGameEndController),
                    "결과 화면 신호 전송에 실패했다 — 감시는 계속한다(이 줄은 판당 한 번만 남는다)",
                    $"Clock={ResultScreenWatchClockId}, Exception={e.GetType().Name}");
            }
        }

        /// <summary>
        /// [서버에서 실행] 클라이언트가 보낸 「아직 여기 있다」 신호.
        ///
        /// 🔴 NGO 명명 규약상 ServerRpc 메서드 이름은 반드시 <c>ServerRpc</c> 로 끝나야 한다.
        /// RequireOwnership=false 인 이유는 이 NetworkObject 가 서버 소유라 그대로 두면
        /// 클라이언트가 호출할 수 없기 때문이다(이 파일의 다른 ServerRpc 들과 같은 이유).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void ResultScreenHeartbeatServerRpc(ServerRpcParams rpcParams = default)
        {
            // 내가 보낸 것이 나에게 돌아온 경우는 상대 신호가 아니다.
            // (지금 구조에서는 Host 가 이 ServerRpc 를 보내지 않으므로 발생하지 않지만,
            //  자기 신호를 상대 신호로 세면 침묵을 영원히 감지하지 못하는 치명적 버그가 되므로
            //  값이 싼 방어를 남겨 둔다.)
            if (NetworkManager.Singleton != null &&
                rpcParams.Receive.SenderClientId == NetworkManager.Singleton.LocalClientId) return;

            MarkOpponentSignalReceived();
        }

        /// <summary>
        /// [클라이언트에서 실행] 서버가 보낸 「아직 여기 있다」 신호.
        ///
        /// 🔴 NGO 명명 규약상 ClientRpc 메서드 이름은 반드시 <c>ClientRpc</c> 로 끝나야 한다.
        /// </summary>
        [ClientRpc]
        private void ResultScreenHeartbeatClientRpc()
        {
            // Host 는 서버이면서 클라이언트이기도 해서 자기가 보낸 ClientRpc 본문을 자기도 실행한다.
            // 그 실행은 상대 신호가 아니므로 반드시 걸러야 한다 — 걸러 내지 않으면 Host 는
            // **영원히 침묵을 감지하지 못한다.**
            if (IsServer) return;

            MarkOpponentSignalReceived();
        }

        /// <summary>
        /// [양쪽 공통] 상대 신호를 받은 시각을 갱신한다(= 침묵 타이머 리셋).
        ///
        /// 🔴 여기에는 로그를 남기지 않는다 — 3초마다 들어오는 정상 신호라
        ///    로그를 남기면 매 틱 로깅 금지(LogRules 1.14 금지 8)에 걸리고 파일이 신호로 찬다.
        /// </summary>
        private void MarkOpponentSignalReceived()
        {
            _lastOpponentSignalRealtime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// NetworkGameManager 참조를 얻는다. 서버는 OnNetworkSpawn 에서 이미 캐시해 두었고,
        /// 클라이언트는 캐시가 없으므로 이때 한 번 찾아 채운다.
        ///
        /// ⚠️ 매 프레임 도는 탐색이 아니다 — 판정은 한 판에 한 번뿐인 경로다.
        /// 씬이 달라 Inspector 로 미리 연결해 둘 수 없다는 사정은 반대 방향
        /// (NetworkGameManager → 이 컨트롤러)도 같다(그쪽도 FindFirstObjectByType 을 쓴다).
        /// </summary>
        private Hexiege.Infrastructure.NetworkGameManager ResolveNetworkGameManager()
        {
            if (_networkGameManager == null)
            {
                _networkGameManager = FindFirstObjectByType<Hexiege.Infrastructure.NetworkGameManager>();
            }
            return _networkGameManager;
        }

        // ====================================================================
        // 유틸리티
        // ====================================================================

        /// <summary>
        /// 현재 연결된 클라이언트 중 자신이 아닌 상대의 ClientId를 반환.
        /// 2인 게임 전용. 상대를 찾을 수 없으면 ulong.MaxValue 반환.
        /// </summary>
        private ulong GetOtherClientId(ulong myClientId)
        {
            foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (id != myClientId)
                    return id;
            }
            return ulong.MaxValue;
        }
    }
}
