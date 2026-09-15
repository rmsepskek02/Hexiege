// ============================================================================
// NetworkGameManager.cs
// 네트워크 게임 세션 전체 흐름을 관리하는 MonoBehaviour.
//
// 역할:
//   - UGS 초기화 (UnityServicesInitializer)
//   - Host 게임 시작: Relay 생성 → Lobby 생성 → NetworkManager.StartHost()
//   - Client 게임 참가: Lobby 참가 → Relay Join Code 추출 → JoinRelay → NetworkManager.StartClient()
//   - 연결 해제: NetworkManager 종료 + Lobby 나가기
//   - Host Heartbeat 코루틴 관리
//
// 배치:
//   씬에 빈 GameObject "NetworkGameManager" 를 만들고 이 컴포넌트 부착.
//   DontDestroyOnLoad 로 씬 전환에도 유지됨.
//
// 아키텍처:
//   Infrastructure 레이어 — 외부 서비스와 Unity 게임 오브젝트의 교차점.
//   NetworkBehaviour 는 아니므로 Presentation 레이어 규칙과 분리됨.
//   다만 MonoBehaviour 로서 코루틴 실행 및 씬 생명주기 관리 담당.
// ============================================================================

using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hexiege.Application; // GameEvents (씬 전환을 Presentation에 위임하는 이벤트 채널)
                           // GameLog / LogEvent (두 축 로그 facade — LogRules.md 1.2)

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 네트워크 게임 세션 전체 흐름 관리 MonoBehaviour.
    /// 씬에 하나만 배치. DontDestroyOnLoad 로 씬 전환 유지.
    /// </summary>
    public class NetworkGameManager : MonoBehaviour
    {
        // ====================================================================
        // Inspector 배선 — 무작위 맵 3단계 I
        // ====================================================================

        [Header("Random Map")]
        [Tooltip("맵 전송 전용 NetworkObject 프리팹. NetworkObject + NetworkMapTransfer 컴포넌트가 " +
                 "부착된 프리팹을 연결한다. Host 가 로비에서 이것을 동적 스폰해 맵을 보낸다. " +
                 "🔴 이 프리팹은 Resources/Config/DefaultNetworkPrefabs.asset 의 네트워크 프리팹 " +
                 "목록에도 반드시 등록되어 있어야 한다(등록되지 않으면 Spawn 이 실패한다).")]
        [SerializeField] private GameObject _mapTransferPrefab;

        // ====================================================================
        // 이벤트 — 외부 UI 가 구독하여 상태 갱신에 활용
        // ====================================================================

        /// <summary>UGS 초기화 완료. playerId 전달.</summary>
        public event Action<string> OnInitialized;

        /// <summary>Host 게임 시작 완료. 로비 코드 전달.</summary>
        public event Action<string> OnHostStarted;

        /// <summary>Client 접속 완료.</summary>
        public event Action OnClientConnected;

        /// <summary>초기화 또는 세션 중 오류 발생. 에러 메시지 전달.</summary>
        public event Action<string> OnError;

        /// <summary>연결 해제 완료.</summary>
        public event Action OnDisconnected;

        /// <summary>
        /// 2명(또는 그 이상) 클라이언트 접속이 완료되어 게임 시작 가능 상태가 되었을 때 발행.
        /// LobbyUI가 로비 패널 숨김 처리를 위해 구독.
        /// 이벤트 매개변수: 현재까지 접속 완료한 클라이언트 수.
        ///
        /// 이전에는 LobbyUI가 NetworkManager.OnClientConnectedCallback을 직접 구독해
        /// ConnectedClientsList.Count로 판정했으나, Presentation 레이어가 Unity.Netcode에 직접 의존하지 않도록
        /// 책임을 Infrastructure 레이어인 본 매니저로 이전.
        /// </summary>
        public event Action<int> OnAllPlayersReady;

        /// <summary>
        /// 클라이언트 측에서 서버 연결이 끊겼을 때 발행.
        /// NetworkStatusUI가 연결 끊김 팝업 표시를 위해 구독.
        ///
        /// 서버 측은 ReconnectionHandler가 별도 처리하므로 이 이벤트는 발행되지 않는다.
        /// (서버는 자신의 LocalClientId 끊김만 인지하므로 이 이벤트와 무관)
        /// </summary>
        public event Action OnServerDisconnected;

        /// <summary>
        /// [무작위 맵 3단계 I] 맵 준비·전송이 실패해 <b>전투 씬으로 넘어가지 않았다.</b>
        /// 인자는 진단용 사유 문자열이다.
        ///
        /// 🔴 구독자(BattleViewModel)가 반드시 해야 하는 일은 <b>로딩 UI 를 내리는 것</b>이다.
        ///    안 내리면 화면이 "게임에 접속하는 중..." 에서 영영 멈춘다.
        /// ⚠️ 실패 팝업·재시도 버튼은 이번 범위가 아니다(계획서 §2-3 · §10).
        ///    그래서 지금은 "로비로 돌아가 있다"는 것 외의 안내가 플레이어에게 가지 않는다.
        ///    이것은 결함이 아니라 확정된 범위 결정이다.
        /// </summary>
        public event Action<string> OnMapTransferFailed;

        // ====================================================================
        // 내부 매니저
        // ====================================================================

        private UnityServicesInitializer _servicesInitializer;
        private LobbyManager _lobbyManager;
        private RelayManager _relayManager;
        private MatchmakerManager _matchmakerManager;

        // Heartbeat 코루틴 추적용
        private Coroutine _heartbeatCoroutine;

        // 매칭 상태 관리
        private string _currentTicketId;
        private CancellationTokenSource _matchmakingCts;

        // 랜덤 매칭 여부 (커스텀게임 재경기 분기용)
        private bool _isRandomMatchmaking;

        // ── 무작위 맵 3단계 I: 씬 전환 게이트 상태 ───────────────────────────

        /// <summary>
        /// 이번에 스폰한 맵 전송 객체. 결말 구독을 풀 때 필요하다(없으면 null).
        /// </summary>
        private NetworkMapTransfer _activeMapTransfer;

        /// <summary>
        /// 맵 준비·전송이 진행 중인가. 중복 요청으로 전송 객체가 둘이 되는 것을 막는다.
        /// (접속 콜백이 두 번 울리는 상황이 실제로 있을 수 있다.)
        /// </summary>
        private bool _mapTransferInProgress;

        /// <summary>
        /// 이번 회차의 결말(성공 또는 실패)을 바깥에 이미 알렸는가.
        ///
        /// 🔴 <b>"한 번만" 과 "반드시 한 번은" 을 동시에 지키기 위한 깃발이다.</b>
        ///    실패를 알린 뒤 성공이 뒤따라 오면 <b>실패한 판인데 전투 씬으로 넘어간다</b>
        ///    (규칙 16 정면 위반). 반대로 아무것도 알리지 않으면 로비가 로딩 화면에서 멈춘다.
        /// </summary>
        private bool _mapTransferGateSettled;

        // ── 재경기 맵 A 단계: 회차마다 달라지는 「결말에 무엇을 할지」 ─────────

        /// <summary>
        /// 이번 회차가 <b>성공</b>했을 때 할 일.
        ///
        /// 왜 필드로 두는가(초급자용): 맵 준비·전송은 여러 프레임에 걸쳐 일어나고, 결말은
        /// 한참 뒤에 콜백으로 돌아온다. 그래서 "이 회차가 끝나면 무엇을 해야 하는지"를
        /// 회차를 시작한 쪽이 넘겨 주고, 이 클래스가 결말이 올 때까지 들고 있어야 한다.
        ///
        /// 들어가는 값은 두 가지뿐이다.
        ///   · 최초 경기 → <see cref="LoadGameScene"/> (로비에서 전투 씬으로 넘어간다)
        ///   · 재경기   → 부른 쪽(<c>NetworkGameEndController</c>)이 준 콜백
        ///
        /// 🔴 <b>이 클래스가 스스로 "재경기니까 이렇게 하자"를 판단하지 않는다.</b>
        ///    판단을 여기에 넣으면 최초 경기 경로에 if 가 생기고, 그것이 곧 회귀 면적이다.
        /// </summary>
        private Action _mapTransferSuccessAction;

        /// <summary>
        /// 이번 회차가 <b>실패</b>했을 때 알릴 곳. null 이면 기존 <see cref="OnMapTransferFailed"/>
        /// 이벤트로 알린다(= 최초 경기의 기존 동작 그대로).
        ///
        /// 🔴 <b>왜 기존 이벤트를 재경기에 그대로 쓰지 않는가</b>:
        ///    <see cref="OnMapTransferFailed"/> 의 구독자는 로비 화면의 ViewModel
        ///    (<c>BattleViewModel</c>)이다. 재경기 실패는 <b>전투 씬의 결과 화면</b>이 받아야
        ///    하므로 받는 사람이 아예 다르다. 한 통로로 합치면 "로비 로딩을 내리는 코드"가
        ///    결과 화면에서 불리게 되고, 반대로 결과 화면 복원이 로비에서 불리게 된다.
        /// </summary>
        private Action<string> _mapTransferFailureAction;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Awake()
        {
            // 씬 전환 시에도 NetworkGameManager 유지
            DontDestroyOnLoad(gameObject);

            // 의존 객체 생성 (DI 컨테이너 대신 직접 생성 — 단순 구조 유지)
            _servicesInitializer = new UnityServicesInitializer();
            _lobbyManager = new LobbyManager();
            _relayManager = new RelayManager();
            _matchmakerManager = new MatchmakerManager();
        }

        /// <summary>
        /// NetworkManager.OnClientDisconnectCallback 통합 핸들러.
        /// 클라이언트 측에서 서버와의 연결이 끊겼을 때 OnServerDisconnected 이벤트를 발행한다.
        /// 서버(Host) 측에서는 다른 컨트롤러(ReconnectionHandler)가 처리하므로 발행하지 않는다.
        ///
        /// 이전에는 NetworkStatusUI(Presentation)가 OnClientDisconnectCallback을 직접 구독했으나,
        /// Presentation 레이어의 Unity.Netcode 직접 의존 제거를 위해 NGM이 가로채는 구조로 변경.
        /// </summary>
        private void HandleClientDisconnected(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return;

            // 서버(Host)는 OnServerDisconnected 발행 대상이 아님 (상대 클라 끊김은 ReconnectionHandler 담당)
            if (NetworkManager.Singleton.IsServer) return;

            // 클라이언트 측: 서버(=자기 자신 또는 ServerClientId) 끊김 처리
            //
            // ⚠️ 잠정 판정: Warn / 개발 (LogAudit.md §4-3 질의 Q-1).
            //    축 B 는 개발로 확정이다 — OnServerDisconnected 로 UI 통지 경로가 있고,
            //    이 줄에는 "왜 끊겼는지"가 담겨 있지 않다.
            //    축 A 가 확정되지 않은 이유: DisconnectAsync/BackToLobby 가
            //    OnClientDisconnectCallback 을 구독 해제하지 않아 **의도적으로 나갈 때도**
            //    이 핸들러가 호출된다. 즉 정상 종료와 장애가 한 줄을 공유한다.
            //    Error 로 두면 정상 종료마다 거짓 경보가 쌓이고, Info 로 두면 진짜 장애가 묻힌다.
            //    → 중간값인 Warn 으로 두었다. 축 B 가 개발이라 릴리스에는 어차피 남지 않는다.
            GameLog.Dev.Warn("Network", nameof(NetworkGameManager),
                             "클라이언트 측 서버 연결 끊김 감지 — OnServerDisconnected 발행",
                             $"ClientId={clientId}");
            OnServerDisconnected?.Invoke();
        }

        /// <summary>
        /// UnityTransport의 RTT(왕복 시간, ms)를 조회. Presentation 레이어 표시용.
        /// NetworkManager가 없거나 Transport 가 UnityTransport가 아니면 0 반환.
        /// 호출자(NetworkStatusUI)는 0 또는 음수일 때 "--ms"로 표시한다.
        /// </summary>
        public ulong GetCurrentRttMs()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return 0UL;

            var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport
                as Unity.Netcode.Transports.UTP.UnityTransport;
            if (transport == null) return 0UL;

            // Host(서버 자신) / Client(서버를 향한 RTT) 모두 ServerClientId 사용
            return transport.GetCurrentRtt(NetworkManager.ServerClientId);
        }

        /// <summary>
        /// 현재 NGO가 활성 상태이고 Host 또는 Client로 실행 중인지 여부.
        /// NetworkContext.IsNetworkActive와 같지만 NGM 단일 진입점으로 호출하기 위해 추가.
        /// </summary>
        public bool IsNetworkRunning =>
            NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient);

        /// <summary>
        /// NetworkManager 종료. NetworkStatusUI 등 외부에서 안전 종료를 위해 호출.
        /// ShutdownNetworkManager는 private이므로 공개 래퍼 메서드를 제공한다.
        /// </summary>
        public void ShutdownNetwork()
        {
            ShutdownNetworkManager();
        }

        private void OnDestroy()
        {
            // Client 접속/끊김 콜백 구독 해제 (누수 방지)
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }

            StopHeartbeat();
            _matchmakingCts?.Dispose();

            // 무작위 맵 3단계 I: 맵 전송 객체 구독 해제(누수 방지).
            CleanupMapTransferSubscription();
            // [재경기 맵 A] 회차 결말 콜백도 함께 비운다(위와 같은 이유).
            ClearMapTransferRoundActions();
        }

        // ====================================================================
        // 공개 API
        // ====================================================================

        /// <summary>
        /// Unity Gaming Services 를 초기화하고 익명 로그인.
        /// 게임 시작 시 가장 먼저 호출해야 함.
        /// 완료 시 OnInitialized 이벤트 발행.
        /// </summary>
        public async Task InitializeAsync()
        {
            GameLog.Dev.Info("Network", nameof(NetworkGameManager), "초기화 시작");

            await _servicesInitializer.InitializeAsync(
                onSuccess: playerId =>
                {
                    // PlayerId 는 개인 식별자라 원본 대신 해시를 남긴다 (LogRules.md 1.6).
                    // 바로 아래 OnInitialized 이벤트에는 원본 playerId 를 그대로 넘긴다 —
                    // 해시는 로그에 찍히는 문자열에만 적용하고 실제 로직 값은 건드리지 않는다.
                    GameLog.Dev.Info("Network", nameof(NetworkGameManager), "초기화 성공",
                                     $"PlayerId={GameLog.HashId(playerId)}");
                    OnInitialized?.Invoke(playerId);
                },
                onFailure: e =>
                {
                    // ⚠️ 잠정 판정: Error / 개발 (LogAudit.md §4-3 질의 Q-3).
                    //    원칙 3("Error 는 항상 운영")만 보면 운영이지만, 같은 예외를 이미
                    //    UnityServicesInitializer 가 e.Message 와 함께 운영으로 남긴다(원칙 1).
                    //    LogRules.md 1.3 「원칙 간 우선순위」①에 따라 원칙 1이 우선하므로,
                    //    원인을 쥔 하위 계층을 운영으로 두고 중복되는 이 호출부를 개발로 내렸다.
                    //    화면 통지(OnError)는 그대로 유지되므로 잃는 정보는 없다.
                    GameLog.Dev.Error("Network", nameof(NetworkGameManager),
                                      "UGS 초기화 실패 — 원인은 UnityServicesInitializer 가 운영 로그로 남긴다", e);
                    OnError?.Invoke($"초기화 실패: {e.Message}");
                });
        }

        /// <summary>
        /// Host 로 게임을 시작.
        /// 순서: Relay 할당 생성 → Lobby 생성 (Join Code 포함) → NetworkManager.StartHost().
        /// 완료 시 OnHostStarted 이벤트에 Lobby Code 전달.
        /// </summary>
        /// <param name="lobbyName">만들 방 이름.</param>
        public async Task HostGameAsync(string lobbyName = "Hexiege Room", string matchId = null)
        {
            try
            {
                GameLog.Dev.Info("Network", nameof(NetworkGameManager), "HostGame 시작",
                                 $"LobbyName={lobbyName}");

                // 1. Relay 서버 할당 생성 + Join Code 발급
                string relayJoinCode = await _relayManager.CreateRelayAsync();
                if (string.IsNullOrEmpty(relayJoinCode))
                {
                    const string errorMsg = "Relay 할당 실패. 네트워크 상태를 확인하세요.";
                    // ✅ 확정 판정: Error / 개발 (LogAudit.md §4-3 질의 Q-2 의 전제가 소멸했다).
                    //
                    //    [무엇이 바뀌었나]
                    //    Q-2 의 잠정 판정("운영 유지")은 오직 하나의 전제 위에 서 있었다 —
                    //    *원인(예외 객체)을 쥔 RelayManager 가 그 task 의 범위 밖이라 GameLog 로 이관되지 않는다.*
                    //    그래서 여기를 개발로 내리면 Relay 실패가 운영 스트림에 한 줄도 남지 않게 되므로,
                    //    "정보를 잃지 않는 쪽"을 기본값으로 두고 잠정적으로 운영을 유지했던 것이다.
                    //    로그 이관 배치 1-A 에서 RelayManager 의 catch 블록이
                    //    GameLog.Ops.Error(LogEvent.RelaySetupFailed, ..., e, "Stage=Allocate") 로 이관되면서
                    //    그 전제가 사라졌다. 이제 원인은 운영 스트림에 예외 객체째로 남는다.
                    //
                    //    [그래서 무엇을 적용하나]
                    //    LogRules 1.3 「원칙 간 우선순위」 ② — 최종 처리 지점은 "예외 객체를 직접 쥔 catch 블록"이고,
                    //    이 자리는 CreateRelayAsync() 에서 null 만 받으므로 원인을 담지 못한다.
                    //    같은 절 ① 과 1.14 금지 9 — 원인을 가진 계층(RelayManager)을 운영으로 두고
                    //    중복되는 상위 호출부(여기)를 개발로 내린다.
                    //    그대로 두면 Relay 실패 1회가 서버 집계에서 2건으로 세어진다.
                    //
                    //    축 A 는 Error 그대로다 — 세션을 열지 못해 기능이 죽고 복구 경로가 없다.
                    //    화면 통지(OnError)도 그대로 유지된다. 축 B 값을 내리는 것이지 통지를 없애는 것이 아니다.
                    GameLog.Dev.Error("Network", nameof(NetworkGameManager),
                                      "Relay 할당 실패 — 원인은 RelayManager 가 운영 로그로 남긴다",
                                      "Stage=Allocate, Flow=Host");
                    OnError?.Invoke(errorMsg);
                    return;
                }

                // 2. Lobby 생성 (Relay Join Code 를 Data 에 포함)
                var lobby = await _lobbyManager.CreateLobbyAsync(lobbyName, maxPlayers: 2, relayJoinCode, matchId);
                if (lobby == null)
                {
                    const string errorMsg = "Lobby 생성 실패. Unity Lobby 서비스를 확인하세요.";
                    // ⚠️ 잠정 판정: Error / 개발 (LogAudit.md §4-3 질의 Q-3).
                    //    LobbyManager 가 catch 안에서 e.Message 와 함께 이미 운영으로 남긴다.
                    //    이 자리는 null 만 받아 고정 문구를 찍으므로 원인을 담지 못한다
                    //    → LogRules.md 1.3 ②의 "최종 처리 지점"이 아니다.
                    GameLog.Dev.Error("Network", nameof(NetworkGameManager),
                                      "Lobby 생성 실패 — 원인은 LobbyManager 가 운영 로그로 남긴다",
                                      $"LobbyName={lobbyName}");
                    OnError?.Invoke(errorMsg);
                    return;
                }

                // 3-1. Client 접속/끊김 감지 콜백 구독 — StartHost() 이전에 등록 (레이스 컨디션 방지)
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

                // 3. NetworkManager Host 시작
                if (!StartNetworkHost())
                {
                    // 실패 시 등록한 콜백 해제 후 에러 반환
                    NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                    NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
                    OnError?.Invoke("NetworkManager.StartHost() 실패.");
                    return;
                }

                // 4. Host Heartbeat 시작 (Lobby 활성 유지)
                StartHeartbeat();

                GameLog.Dev.Info("Network", nameof(NetworkGameManager), "Host 게임 시작 완료",
                                 $"LobbyCode={lobby.LobbyCode}");
                OnHostStarted?.Invoke(lobby.LobbyCode);
            }
            catch (Exception e)
            {
                // 이 흐름의 최종 catch 이고, 하위 계층이 분류하지 못한 고유 예외만 여기로 온다
                // → 원칙 1의 중복이 아니다. 예외 객체를 그대로 넘겨 타입을 집계 축으로 남긴다.
                GameLog.Ops.Error(LogEvent.GameSessionStartUnhandledException, "Network", nameof(NetworkGameManager),
                                  "HostGame 처리 중 예외", e, "Flow=Host");
                OnError?.Invoke($"Host 시작 오류: {e.Message}");
            }
        }

        /// <summary>
        /// Lobby Code 로 기존 게임에 참가.
        /// 순서: Lobby 참가 → Relay Join Code 추출 → JoinRelay → NetworkManager.StartClient().
        /// 완료 시 OnClientConnected 이벤트 발행.
        /// </summary>
        /// <param name="lobbyCode">Host 가 공유한 Lobby 참가 코드.</param>
        public async Task JoinGameAsync(string lobbyCode)
        {
            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                OnError?.Invoke("Lobby Code 가 비어 있습니다.");
                return;
            }

            try
            {
                GameLog.Dev.Info("Network", nameof(NetworkGameManager), "JoinGame 시작",
                                 $"LobbyCode={lobbyCode}");

                // 1. Lobby 참가
                var lobby = await _lobbyManager.JoinLobbyByCodeAsync(lobbyCode);
                if (lobby == null)
                {
                    const string errorMsg = "Lobby 참가 실패. 코드를 확인하거나 방이 꽉 찼을 수 있습니다.";
                    // ⚠️ 잠정 판정: Error / 개발 (LogAudit.md §4-3 질의 Q-3).
                    //    LobbyManager 가 catch 안에서 e.Message 와 함께 이미 운영으로 남긴다.
                    //    이 자리는 null 만 받아 원인을 담지 못하므로 최종 처리 지점이 아니다.
                    GameLog.Dev.Error("Network", nameof(NetworkGameManager),
                                      "Lobby 참가 실패 — 원인은 LobbyManager 가 운영 로그로 남긴다",
                                      $"LobbyCode={lobbyCode}");
                    OnError?.Invoke(errorMsg);
                    return;
                }

                // 2. Lobby Data 에서 Relay Join Code 추출
                string relayJoinCode = _lobbyManager.GetRelayJoinCode();
                if (string.IsNullOrEmpty(relayJoinCode))
                {
                    const string errorMsg = "Relay Join Code 를 Lobby 에서 찾을 수 없습니다. " +
                                            "Host 가 아직 준비되지 않았을 수 있습니다.";
                    // 하위 계층에 대응 로그가 없다(원칙 1 통과) → 이 줄이 유일한 기록이다.
                    // Host 의 코드 기록이 늦거나 실패한 상태라 플레이어 기기에서만 벌어진다.
                    //
                    // ⚠️ 이 줄은 위/아래 두 곳(Stage=Allocate · Stage=Join)과 달리 개발로 내리지 않는다.
                    //    같은 RelaySetupFailed 키를 쓰지만 사건이 다르기 때문이다 —
                    //    여기서 감지하는 것은 "Lobby Data 에 Relay Join Code 가 아직 없다"이고,
                    //    이 분기는 return 으로 끝나 RelayManager.JoinRelayAsync 를 아예 호출하지 않는다.
                    //    즉 RelayManager 쪽에는 이 사건에 대응하는 로그가 존재할 수 없어 중복이 아니다.
                    //    (RelayManager 의 Stage=CodeMissing 로그는 "JoinRelay 를 빈 코드로 호출한 계약 위반"이라
                    //     이 자리와는 별개의 사건이다.)
                    //    검증 시 "RelaySetupFailed 를 쓰는 Ops 호출은 RelayManager 5곳뿐"이 아니라
                    //    "RelayManager 5곳 + 이 자리 1곳 = 6곳"이 정상 상태다.
                    GameLog.Ops.Error(LogEvent.RelaySetupFailed, "Network", nameof(NetworkGameManager),
                                      "Lobby 에 Relay Join Code 가 없다 — Host 가 아직 기록하지 못했다",
                                      "Stage=CodeMissing, Flow=Join");
                    OnError?.Invoke(errorMsg);
                    return;
                }

                // 3. Relay 서버 참가 + UnityTransport 설정
                bool relayJoined = await _relayManager.JoinRelayAsync(relayJoinCode);
                if (!relayJoined)
                {
                    const string errorMsg = "Relay 참가 실패.";
                    // ✅ 확정 판정: Error / 개발 — 위 HostGameAsync 의 "Relay 할당 실패" 와 같은 사안이다.
                    //    Q-2 의 잠정 판정이 기대고 있던 전제(원인 로그를 가진 RelayManager 가 범위 밖이라
                    //    이관되지 않는다)가 배치 1-A 의 RelayManager 이관으로 소멸했다.
                    //    이제 JoinRelayAsync 의 catch 가 예외 객체와 함께
                    //    Ops.Error(RelaySetupFailed, ..., e, "Stage=Join") 을 남긴다.
                    //    이 자리는 bool false 만 받아 원인을 담지 못하므로 최종 처리 지점이 아니다
                    //    (LogRules 1.3 ②) → 중복 집계를 막기 위해 개발로 내린다(1.14 금지 9).
                    //    축 A 는 Error 유지, OnError 화면 통지도 그대로다.
                    GameLog.Dev.Error("Network", nameof(NetworkGameManager),
                                      "Relay 참가 실패 — 원인은 RelayManager 가 운영 로그로 남긴다",
                                      "Stage=Join, Flow=Join");
                    OnError?.Invoke(errorMsg);
                    return;
                }

                // 4. NetworkManager Client 시작 — 끊김 콜백을 StartClient 이전에 등록 (레이스 컨디션 방지)
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

                if (!StartNetworkClient())
                {
                    NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
                    OnError?.Invoke("NetworkManager.StartClient() 실패.");
                    return;
                }

                GameLog.Dev.Info("Network", nameof(NetworkGameManager), "Client 게임 참가 완료");
                OnClientConnected?.Invoke();
            }
            catch (Exception e)
            {
                // 최종 catch · 고유 예외. 예외 객체를 그대로 넘긴다(LogRules.md 1.9).
                GameLog.Ops.Error(LogEvent.GameSessionStartUnhandledException, "Network", nameof(NetworkGameManager),
                                  "JoinGame 처리 중 예외", e, "Flow=Join");
                OnError?.Invoke($"참가 오류: {e.Message}");
            }
        }

        /// <summary>
        /// 현재 네트워크 세션에서 연결 해제.
        /// NetworkManager 를 종료하고 Lobby 에서도 나감.
        /// 완료 시 OnDisconnected 이벤트 발행.
        /// </summary>
        public async Task DisconnectAsync()
        {
            GameLog.Dev.Info("Network", nameof(NetworkGameManager), "Disconnect 시작");

            // Heartbeat 중단
            StopHeartbeat();

            // Client 접속 콜백 구독 해제 (Host 였을 경우 대비)
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;

            // NetworkManager 종료
            ShutdownNetworkManager();

            // 랜덤 매칭 상태 초기화
            _isRandomMatchmaking = false;

            // 무작위 맵 3단계 I: 진행 중이던 맵 준비 상태도 함께 비운다.
            //   🔴 안 비우면 _mapTransferInProgress 가 true 로 굳어, 다음에 다시 방을 만들었을 때
            //      "이미 진행 중"으로 판정되어 맵 준비가 아예 시작되지 않는다(로딩에서 멈춘다).
            //   확정 맵도 폐기한다 — 규칙 14 *"로비 복귀 또는 연결 종료 시 폐기"*.
            //   남겨 두면 다음 판에서 **지난 판 맵이 조용히 재사용**될 여지가 생긴다.
            CleanupMapTransferSubscription();
            _mapTransferInProgress = false;
            // [재경기 맵 A] 회차 결말 콜백도 같은 자리에서 비운다 — 세션이 끝났는데 콜백이
            //   남아 있으면 이미 사라진 화면을 되살리려 드는 코드가 다음 판에서 불린다.
            ClearMapTransferRoundActions();
            MapHandoff.Clear();

            // Lobby 나가기
            await _lobbyManager.LeaveLobbyAsync();

            GameLog.Dev.Info("Network", nameof(NetworkGameManager), "Disconnect 완료");
            OnDisconnected?.Invoke();
        }

        // ====================================================================
        // 랜덤 매칭 API
        // ====================================================================

        /// <summary>
        /// 랜덤 매칭 시작.
        /// 순서: 티켓 생성 → 폴링 → 매칭 완료 → Host/Client 역할 결정 → 게임 시작.
        /// </summary>
        /// <param name="onWaitSecond">대기 시간(초) 콜백. UI 타이머용.</param>
        /// <param name="onMatchFound">매칭 성사 직후 콜백. 로딩 스크린 표시 등에 활용.</param>
        public async Task StartMatchmakingAsync(Action<int> onWaitSecond = null, Action onMatchFound = null)
        {
            _isRandomMatchmaking = true;
            _matchmakingCts = new CancellationTokenSource();

            try
            {
                _currentTicketId = await _matchmakerManager.CreateTicketAsync();
                GameLog.Dev.Info("Network", nameof(NetworkGameManager), "매칭 티켓 생성",
                                 $"TicketId={_currentTicketId}");

                var matchId = await _matchmakerManager.PollUntilMatchedAsync(
                    _currentTicketId, _matchmakingCts.Token, onWaitSecond);

                GameLog.Dev.Info("Network", nameof(NetworkGameManager), "매칭 완료",
                                 $"MatchId={matchId}");

                // 매칭 성사 콜백 — UI에서 로딩 스크린 표시 등에 활용
                onMatchFound?.Invoke();

                // [A방식 — 2026-07-17] 호스트를 "계산"하지 않고 Lobby CreateOrJoin 으로 "선점"한다.
                // 기존 DetermineIsHostAsync(매치 결과 조회) 호출은 P2P 환경에서 404 를 유발하여 제거.
                // 모든 플레이어가 동일 matchId 로 CreateOrJoin → 먼저 만든 쪽이 호스트가 된다.
                await StartMatchmadeGameAsync(matchId);
            }
            catch (OperationCanceledException)
            {
                // 매칭 취소 — 정상 흐름이므로 별도 처리 없음
            }
            catch (Exception e)
            {
                // catch 본문이 로그 한 줄뿐이고 OnError 도 없다 — 매칭이 조용히 죽는다.
                // 삼킨 예외는 반드시 운영으로 남긴다(LogRules.md 1.3 분류 원칙 4).
                GameLog.Ops.Error(LogEvent.MatchmakingUnhandledException, "Network", nameof(NetworkGameManager),
                                  "랜덤 매칭 처리 중 예외 — 매칭이 통지 없이 종료된다", e);
            }
        }

        // ====================================================================
        // A방식 — Lobby CreateOrJoin 기반 매칭 게임 시작 (2026-07-17 추가)
        // ====================================================================

        /// <summary>
        /// 매칭 성사 후 실제 게임을 시작한다 (A방식: Lobby CreateOrJoin 선점).
        ///
        /// 흐름:
        ///   1) 모든 플레이어가 동일 matchId 를 키로 Lobby 에 CreateOrJoin 요청.
        ///      - 먼저 만든 쪽 → Lobby "생성" → 호스트(IsHost == true)
        ///      - 나중에 온 쪽 → 기존 Lobby "참가" → 클라이언트(IsHost == false)
        ///   2) 호스트/클라이언트 각자 후속 처리로 분기.
        /// </summary>
        /// <param name="matchId">매칭된 Match ID (Lobby 고유 ID 로도 사용).</param>
        private async Task StartMatchmadeGameAsync(string matchId)
        {
            // Lobby CreateOrJoin — 서버가 원자적으로 처리하여 정확히 한 명만 호스트가 된다.
            var lobby = await _lobbyManager.CreateOrJoinLobbyByMatchIdAsync(
                matchId, $"match_{matchId}", maxPlayers: 2);

            if (lobby == null)
            {
                OnError?.Invoke("매칭 방 생성/참가에 실패했습니다. 다시 시도해주세요.");
                return;
            }

            bool isHost = _lobbyManager.IsHost;
            GameLog.Dev.Info("Network", nameof(NetworkGameManager), "CreateOrJoin 으로 역할 확정",
                             $"Role={(isHost ? "Host" : "Client")}, MatchId={matchId}");

            if (isHost)
                await HostMatchmadeGameAsync(lobby.LobbyCode);
            else
                await JoinMatchmadeGameAsync();
        }

        /// <summary>
        /// 매칭 호스트 후속 처리.
        /// Lobby 는 이미 CreateOrJoin 으로 생성됐으므로, Relay 할당 → JoinCode 를 Lobby 에 기록 →
        /// StartHost → Heartbeat 순으로 진행한다.
        /// (기존 HostGameAsync 의 Relay·Host 로직을 매칭 경로에 맞게 재구성 — Lobby 생성 단계는 제외)
        /// </summary>
        /// <param name="lobbyCode">CreateOrJoin 으로 만든 Lobby 의 참가 코드 (OnHostStarted 전달용).</param>
        private async Task HostMatchmadeGameAsync(string lobbyCode)
        {
            try
            {
                // 1. Relay 서버 할당 + Join Code 발급
                string relayJoinCode = await _relayManager.CreateRelayAsync();
                if (string.IsNullOrEmpty(relayJoinCode))
                {
                    OnError?.Invoke("Relay 할당 실패. 네트워크 상태를 확인하세요.");
                    return;
                }

                // 2. 발급받은 Relay Join Code 를 Lobby Data 에 기록 → 클라이언트가 읽어감
                await _lobbyManager.UpdateRelayJoinCodeAsync(relayJoinCode);

                // 3. Client 접속/끊김 감지 콜백 — StartHost() 이전에 등록 (레이스 컨디션 방지)
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

                // 4. NetworkManager Host 시작
                if (!StartNetworkHost())
                {
                    NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                    NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
                    OnError?.Invoke("NetworkManager.StartHost() 실패.");
                    return;
                }

                // 5. Host Heartbeat 시작 (Lobby 활성 유지)
                StartHeartbeat();

                GameLog.Dev.Info("Network", nameof(NetworkGameManager), "매칭 Host 게임 시작 완료",
                                 $"LobbyCode={lobbyCode}");
                OnHostStarted?.Invoke(lobbyCode);
            }
            catch (Exception e)
            {
                // 최종 catch · 고유 예외. Flow 로 어느 경로였는지 분해한다.
                GameLog.Ops.Error(LogEvent.GameSessionStartUnhandledException, "Network", nameof(NetworkGameManager),
                                  "매칭 Host 시작 처리 중 예외", e, "Flow=MatchHost");
                OnError?.Invoke($"Host 시작 오류: {e.Message}");
            }
        }

        /// <summary>
        /// 매칭 클라이언트 후속 처리.
        /// Lobby 는 이미 CreateOrJoin 으로 참가된 상태다. 다만 호스트가 Relay 를 할당하고
        /// JoinCode 를 Lobby 에 기록하기까지 시간차가 있으므로, RelayJoinCode 가 채워질 때까지
        /// Lobby 를 폴링하며 대기한 뒤 Relay 참가 → StartClient 를 진행한다.
        /// (기존 JoinGameByIdAsync 의 참가 로직 + JoinCode 대기 폴링을 반영)
        /// </summary>
        private async Task JoinMatchmadeGameAsync()
        {
            try
            {
                // 1. 호스트가 RelayJoinCode 를 Lobby 에 기록할 때까지 폴링 대기
                const int maxRetries = 15;   // 최대 약 15초 대기
                const int retryDelayMs = 1000;
                string relayJoinCode = null;

                for (int i = 0; i < maxRetries; i++)
                {
                    // 최신 Lobby Data 를 서버에서 다시 받아온 뒤 JoinCode 확인
                    await _lobbyManager.RefreshCurrentLobbyAsync();
                    relayJoinCode = _lobbyManager.GetRelayJoinCode();

                    if (!string.IsNullOrEmpty(relayJoinCode))
                        break;

                    // 루프 안 진행 로그는 상태 "전이"가 아니다(LogRules.md 1.14 금지 사항 8).
                    // 개발 판정이라 릴리스에서는 호출과 인자 평가까지 통째로 사라진다(1.7).
                    GameLog.Dev.Info("Network", nameof(NetworkGameManager), "RelayJoinCode 대기 중",
                                     $"Attempt={i + 1}, MaxRetries={maxRetries}");
                    await Task.Delay(retryDelayMs);
                }

                if (string.IsNullOrEmpty(relayJoinCode))
                {
                    OnError?.Invoke("호스트의 Relay 준비를 기다리지 못했습니다. 다시 시도해주세요.");
                    return;
                }

                // 2. Relay 서버 참가 + UnityTransport 설정
                bool relayJoined = await _relayManager.JoinRelayAsync(relayJoinCode);
                if (!relayJoined) { OnError?.Invoke("Relay 참가 실패."); return; }

                // 3. NetworkManager Client 시작 — 끊김 콜백을 StartClient 이전에 등록
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

                if (!StartNetworkClient())
                {
                    NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
                    OnError?.Invoke("StartClient() 실패.");
                    return;
                }

                GameLog.Dev.Info("Network", nameof(NetworkGameManager),
                                 "Client 게임 참가 완료 (매칭 — CreateOrJoin)");
                OnClientConnected?.Invoke();
            }
            catch (Exception e)
            {
                // 최종 catch · 고유 예외.
                GameLog.Ops.Error(LogEvent.GameSessionStartUnhandledException, "Network", nameof(NetworkGameManager),
                                  "매칭 Client 참가 처리 중 예외", e, "Flow=MatchJoin");
                OnError?.Invoke($"참가 오류: {e.Message}");
            }
        }

        // ====================================================================
        // [비활성화됨] 2026-07-17 — 구 매칭 클라이언트 참가 경로 (A방식으로 대체)
        // ====================================================================
        //
        // 아래 JoinByMatchIdAsync / JoinGameByIdAsync 는 "호스트가 만든 Lobby 를 matchId 로
        // 검색(FindLobbyByMatchIdAsync)해서 참가"하던 구방식이다. A방식(CreateOrJoin)에서는
        // 클라이언트도 CreateOrJoin 한 번으로 곧바로 Lobby 에 참가되므로 별도 검색 폴링이
        // 필요 없어졌다. 클라이언트 참가 경로는 위 JoinMatchmadeGameAsync 로 일원화되었고,
        // 남은 대기는 "RelayJoinCode 채워짐 대기"뿐이다.
        //
        // ⚠️ 즉시 삭제가 아니라 "비활성화(주석)"다. 사용자 실기 테스트 통과 후 별도 단계에서
        //    최종 삭제한다 (WORKFLOW [4] 규칙).
        //    (LobbyManager.FindLobbyByMatchIdAsync 도 이 경로 전용이라 함께 미사용 상태가 됨)
        // ====================================================================

        /*
        /// <summary>
        /// 매칭된 MatchId 로 Host 가 만든 Lobby 를 검색하여 참가.
        /// Host 의 Lobby 생성에 시간이 걸릴 수 있으므로 재시도 폴링.
        /// </summary>
        /// <param name="matchId">매칭된 Match ID.</param>
        private async Task JoinByMatchIdAsync(string matchId)
        {
            const int maxRetries = 10;
            for (int i = 0; i < maxRetries; i++)
            {
                await Task.Delay(1000);

                string lobbyId = await _lobbyManager.FindLobbyByMatchIdAsync(matchId);
                if (!string.IsNullOrEmpty(lobbyId))
                {
                    await JoinGameByIdAsync(lobbyId);
                    return;
                }

                Debug.Log($"[Matchmaker] Lobby 대기 중... ({i + 1}/{maxRetries})");
            }

            OnError?.Invoke("매칭된 방을 찾을 수 없습니다. 다시 시도해주세요.");
        }

        private async Task JoinGameByIdAsync(string lobbyId)
        {
            try
            {
                Debug.Log($"[Network] JoinGameById 시작. Lobby Id: {lobbyId}");

                var lobby = await _lobbyManager.JoinLobbyByIdAsync(lobbyId);
                if (lobby == null) { OnError?.Invoke("Lobby 참가 실패."); return; }

                string relayJoinCode = _lobbyManager.GetRelayJoinCode();
                if (string.IsNullOrEmpty(relayJoinCode)) { OnError?.Invoke("Relay Join Code 없음."); return; }

                bool relayJoined = await _relayManager.JoinRelayAsync(relayJoinCode);
                if (!relayJoined) { OnError?.Invoke("Relay 참가 실패."); return; }

                if (!StartNetworkClient()) { OnError?.Invoke("StartClient() 실패."); return; }

                Debug.Log("[Network] Client 게임 참가 완료 (매칭).");
                OnClientConnected?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Network] JoinGameById 예외: {e.Message}");
                OnError?.Invoke($"참가 오류: {e.Message}");
            }
        }
        */

        /// <summary>
        /// 진행 중인 매칭을 취소.
        /// 폴링 루프를 중단하고 티켓을 삭제.
        /// </summary>
        public async Task CancelMatchmakingAsync()
        {
            _matchmakingCts?.Cancel();

            try
            {
                await _matchmakerManager.CancelTicketAsync(_currentTicketId);
            }
            catch (Exception e)
            {
                // catch 본문이 로그 한 줄뿐인 삼킨 예외 → 반드시 운영(LogRules.md 1.3 분류 원칙 4).
                // 흐름은 계속되므로 Warn 이지만, 서버에 티켓이 남아 이후 매칭을 방해할 수 있다.
                GameLog.Ops.Warn(LogEvent.MatchmakingTicketDeleteFailed, "Network", nameof(NetworkGameManager),
                                 "매칭 티켓 삭제 실패 (무시하고 계속) — 서버에 티켓이 남을 수 있다", e,
                                 $"TicketId={_currentTicketId}");
            }

            _currentTicketId = null;
            _isRandomMatchmaking = false;
            GameLog.Dev.Info("Network", nameof(NetworkGameManager), "매칭 취소 완료");
        }

        // ====================================================================
        // 프로퍼티 노출
        // ====================================================================

        /// <summary>현재 참가 중인 Lobby 정보 (null 이면 미참가).</summary>
        public Unity.Services.Lobbies.Models.Lobby CurrentLobby => _lobbyManager?.CurrentLobby;

        /// <summary>UGS 초기화 완료 여부.</summary>
        public bool IsInitialized => _servicesInitializer?.IsInitialized ?? false;

        /// <summary>현재 자신이 Host 인지 여부.</summary>
        public bool IsHost => _lobbyManager?.IsHost ?? false;

        /// <summary>현재 세션이 랜덤 매칭으로 시작되었는지 여부.</summary>
        public bool IsRandomMatchmaking => _isRandomMatchmaking;

        // ====================================================================
        // 씬 전환 — 🔴 게이트(무작위 맵 3단계 I)
        //
        // 여기가 "맵이 준비되기 전에는 전투 씬으로 못 넘어간다"를 실제로 강제하는 자리다.
        // 종전에는 BattleViewModel 이 두 명이 접속한 순간 LoadGameScene() 을 **직접** 불렀다.
        // 이제는 이 메서드를 부르고, 씬 로드는 맵 전송이 성공했을 때만 일어난다.
        //
        // ⚠️ 게이트의 **주인**은 이 클래스가 아니라 NetworkMapTransfer(Infrastructure)다.
        //    이 클래스는 NetworkBehaviour 가 아니라(그래서 RPC 를 직접 쓸 수 없다)
        //    959행짜리 세션 매니저이고 로비 UI 가 직접 참조하므로, NetworkBehaviour 로
        //    승격하면 초기화 순서 전체가 영향권에 든다(계획서 §4-1 후보 C 탈락).
        //    그래서 여기는 **위임 진입점**만 갖는다.
        //
        // 🔴 [재경기 맵 A, 2026-09-15] 진입점이 **하나에서 둘**이 됐다.
        //      · BeginMapTransferAndLoadGameScene() — 최초 경기(로비에서 BattleViewModel 이 부른다)
        //      · BeginRematchMapTransfer(...)       — 재경기(전투 씬에서 NetworkGameEndController 가 부른다)
        //    둘은 **같은 본체**(BeginMapTransferRound)를 쓰고, 다른 것은 「결말에 무엇을 하는가」뿐이다.
        //    규칙 16 이 *"최초 경기와 재경기는 모두 씬에 종속되지 않는 공용 전송 경로를 사용한다"* 고
        //    정하므로, 절차를 복사해 두 벌로 만들지 않고 결말만 밖에서 정하게 했다.
        // ====================================================================

        /// <summary>
        /// [Host 전용] 이번 판의 맵을 준비·전송하고, <b>성공했을 때만</b> 전투 씬으로 넘어간다.
        /// 두 명이 접속 완료한 시점에 BattleViewModel 이 부른다.
        ///
        /// 하는 일 순서(실제 절차는 공용 본체 <see cref="BeginMapTransferRound"/> 에 있다):
        ///   ① 맵 전송용 NetworkObject 를 로비에서 동적 스폰한다(Host 권한).
        ///   ② 그 객체의 결말 이벤트 두 개를 구독한다(성공 → 씬 로드 / 실패 → 로비 유지).
        ///   ③ 이번 판의 root seed 를 뽑아(<see cref="Hexiege.Domain.MapRootSeed.Create"/>) 전송을 시작한다.
        ///
        /// 🔴 <b>실패하면 씬을 절대 넘기지 않는다</b>
        ///    (GameSystemRules_RandomMap.md 규칙 16 — *"어떤 실패에서도 전투 씬으로 전환하지 않는다"*).
        ///    대신 <see cref="OnMapTransferFailed"/> 를 발행해 로딩 UI 를 내리게 한다.
        ///
        /// ⚠️ 왜 로비에서 스폰하는가: 규칙 16 이 *"해시가 같을 때만 전투 씬 전환을 시작한다"* 고
        ///    정하므로 해시 대조가 씬 전환보다 **먼저** 끝나야 한다. 즉 전송은 로비에서 일어난다.
        ///    확정된 맵은 씬 재로드를 넘어야 하므로 이 객체가 아니라
        ///    MapHandoff(Application 정적 홀더)가 들고 넘어간다.
        /// </summary>
        public void BeginMapTransferAndLoadGameScene()
        {
            // 🔴 이름과 시그니처를 그대로 둔다 — 호출부(BattleViewModel)가 한 줄도 바뀌지 않아야
            //    최초 경기 경로의 회귀 면적이 0이 된다(계획서 §1 분할-1).
            //    달라진 것은 "본문이 공용 메서드로 옮겨졌다"는 사실뿐이고, 넘기는 값
            //    (성공 → LoadGameScene / 실패 → 기존 이벤트)은 종전과 완전히 같다.
            BeginMapTransferRound(MapTransferRoundKind.First, LoadGameScene, null);
        }

        /// <summary>
        /// [Host 전용] <b>재경기</b>용 진입점. 결과 화면을 띄워 둔 채로 새 맵을 준비·전송하고,
        /// 그 결말을 <b>부른 쪽이 정한 대로</b> 처리한다.
        ///
        /// 최초 경기용 <see cref="BeginMapTransferAndLoadGameScene"/> 와 <b>같은 절차</b>를 쓴다
        /// (GameSystemRules_RandomMap.md 규칙 16 — *"최초 경기와 재경기는 모두 씬에 종속되지 않는
        /// 공용 전송 경로를 사용한다"*). 다른 것은 결말에 무엇을 하는가뿐이다.
        ///
        /// 🔴 <b>씬 재로드는 이 클래스가 하지 않는다.</b> 재경기의 씬 재로드는
        ///    <c>NetworkGameEndController.StartRematch()</c> 가 이미 하고 있고, 그 메서드는
        ///    동적 스폰 NetworkObject 정리까지 함께 한다. 여기서 <see cref="LoadGameScene"/> 을
        ///    부르면 그 정리가 건너뛰어져 지난 판의 유닛·건물이 남는다.
        ///    그래서 성공 시 할 일을 <paramref name="onSucceeded"/> 로 <b>받기만</b> 한다.
        /// </summary>
        /// <param name="onSucceeded">양쪽 검증까지 끝나 새 맵이 확정됐을 때 부를 콜백(필수)</param>
        /// <param name="onFailed">준비·전송·검증이 실패했을 때 부를 콜백(필수). 인자는 진단용 사유</param>
        public void BeginRematchMapTransfer(Action onSucceeded, Action<string> onFailed)
        {
            if (onSucceeded == null || onFailed == null)
            {
                // [개발] 호출부 실수다. 재경기는 결말 두 갈래가 **둘 다** 배선돼야 성립한다 —
                //   성공 콜백이 없으면 맵만 만들고 아무 일도 일어나지 않고,
                //   실패 콜백이 없으면 결과 화면의 버튼이 잠긴 채 영영 돌아오지 않는다.
                GameLog.Dev.Warn("Network", nameof(NetworkGameManager),
                                 "재경기 맵 준비 요청에 결말 콜백이 빠져 있어 시작하지 않았다",
                                 $"HasSucceeded={onSucceeded != null}, HasFailed={onFailed != null}");
                if (onFailed != null) onFailed("RematchCallbackMissing");
                return;
            }

            BeginMapTransferRound(MapTransferRoundKind.Rematch, onSucceeded, onFailed);
        }

        /// <summary>
        /// 🔴 <b>최초 경기와 재경기가 공유하는 본체.</b> 스폰 → 결말 구독 → root seed → 전송 시작까지
        /// 한 회차를 여는 절차 전부가 여기에 있다.
        ///
        /// 종전에는 이 본문이 <see cref="BeginMapTransferAndLoadGameScene"/> 안에 그대로 있었다.
        /// 재경기도 같은 절차가 필요해졌으므로 <b>한 벌만 남기고 갈랐다</b>(계획서 §1 분할-1).
        /// 복사해서 두 벌로 두지 않은 이유는 늘 같다 — 두 벌이 되면 언젠가 한쪽만 고쳐진다.
        /// </summary>
        /// <param name="roundKind">
        /// 이번 회차가 최초 경기인가 재경기인가. 🔴 <b>절차를 가르는 값이 아니라 로그 표식이다</b> —
        /// 전송 쪽이 결말 로그에 <c>Round=</c> 로 실어, 문제가 났을 때 어느 경기의 전송이었는지
        /// 로그만 보고 가려낼 수 있게 한다(계획서 §6-B).
        /// </param>
        /// <param name="onSucceeded">성공 시 할 일</param>
        /// <param name="onFailed">실패 시 알릴 곳. null 이면 기존 <see cref="OnMapTransferFailed"/> 이벤트로 알린다</param>
        private void BeginMapTransferRound(MapTransferRoundKind roundKind,
                                           Action onSucceeded, Action<string> onFailed)
        {
            // 🔴 중복 요청 가드가 **가장 앞**이다. 접속 콜백이 두 번 울리는 등으로 두 번 불릴 수
            //    있는데, 두 번 스폰하면 같은 판에 전송 객체가 둘이 되어 결말도 둘이 된다.
            //    가드 자체에는 결말 로그를 남기지 않는다 — 여기서 되돌아가는 것은 정상 흐름이고,
            //    "무엇이 또 오려 했는가"는 개발 축으로만 남긴다(LogRules 1.14 금지 8).
            if (_mapTransferInProgress)
            {
                GameLog.Dev.Warn("Network", nameof(NetworkGameManager),
                                 "맵 준비가 이미 진행 중이라 중복 요청을 무시했다",
                                 $"RejectedRound={roundKind}");

                // 🔴 거절당한 **이번 요청**의 실패 콜백만 부른다(진행 중인 회차의 것이 아니다).
                //    안 부르면 재경기에서 "수락했는데 아무 일도 일어나지 않고 버튼도 잠긴 채"
                //    남는다. 최초 경기는 onFailed 가 null 이라 종전과 완전히 같은 동작이다.
                if (onFailed != null) onFailed("MapTransferAlreadyInProgress");
                return;
            }

            // 여기서부터가 새 회차다. 지난 회차의 "결말을 이미 알렸다" 표시를 지운다.
            // (중복 요청 가드보다 **뒤**에 두는 것이 중요하다 — 앞에 두면 진행 중인 회차의
            //  표시를 중복 요청이 지워 버려 같은 판의 결말이 두 번 나갈 수 있다.)
            _mapTransferGateSettled = false;

            // 이번 회차의 결말에 무엇을 할지 보관한다. 결말 처리 자리(성공/실패)에서 꺼내 쓰고
            // 그 자리에서 곧바로 비운다 — 지난 회차의 콜백이 다음 회차에 남아 있으면
            // 「최초 경기가 성공했는데 결과 화면 복원 코드가 불린다」 같은 사고가 난다.
            _mapTransferSuccessAction = onSucceeded;
            _mapTransferFailureAction = onFailed;

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                // 정상 흐름에서는 올 수 없다 — 이 메서드를 부르는 BattleViewModel.OnClientConnected
                // 는 Host 에서만 발행되기 때문이다. 그래도 막아 둔다(불변식 위반).
                GameLog.Ops.Warn(LogEvent.SceneLoadRequestedByNonServer, "Network", nameof(NetworkGameManager),
                                 "서버가 아닌 쪽에서 맵 준비·씬 전환을 요청했다 — 무시한다",
                                 $"HasSingleton={NetworkManager.Singleton != null}");
                FailMapTransferGate("NotServer");
                return;
            }

            if (_mapTransferPrefab == null)
            {
                // [개발] Inspector 배선 누락 = 설정 오류다. LogRules 1.3 분류 원칙 3 의 단서에 따라
                //   Error 가 아니라 Warn + 개발로 낮춘다 — 모든 기기에서 똑같이 실패하므로
                //   플레이어 빌드에 도달하기 전 에디터 첫 실행에 반드시 드러난다.
                //   (선례: GameBootstrapper.Map.cs 의 "GameConfig 가 Inspector 에 연결되지 않아…")
                GameLog.Dev.Warn("Network", nameof(NetworkGameManager),
                                 "맵 전송 프리팹이 Inspector 에 연결되지 않아 맵을 준비할 수 없다 — " +
                                 "NetworkGameManager 의 Map Transfer Prefab 항목을 확인할 것");
                FailMapTransferGate("MapTransferPrefabMissing");
                return;
            }

            _mapTransferInProgress = true;

            // ── ① 동적 스폰 ────────────────────────────────────────────────
            GameObject instance = Instantiate(_mapTransferPrefab);

            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            NetworkMapTransfer transfer = instance.GetComponent<NetworkMapTransfer>();

            if (networkObject == null || transfer == null)
            {
                // 프리팹은 연결됐는데 컴포넌트가 빠진 경우다. 위와 같은 이유로 개발 축.
                GameLog.Dev.Warn("Network", nameof(NetworkGameManager),
                                 "맵 전송 프리팹에 필요한 컴포넌트가 없다",
                                 $"HasNetworkObject={networkObject != null}, " +
                                 $"HasMapTransfer={transfer != null}");
                Destroy(instance);
                FailMapTransferGate("MapTransferPrefabMalformed");
                return;
            }

            // 🔴 Spawn() 을 먼저 해야 한다. BeginHostMapTransfer 가 !IsSpawned 면 바로 false 를
            //    돌려주고, RPC 도 스폰된 뒤에만 나갈 수 있기 때문이다.
            networkObject.Spawn();

            _activeMapTransfer = transfer;

            // ── ② 결말 구독 (= 게이트) ─────────────────────────────────────
            transfer.OnHostTransferSucceeded += HandleMapTransferSucceeded;
            transfer.OnHostTransferFailed += HandleMapTransferFailed;

            // ── ③ root seed 를 뽑아 전송 시작 ──────────────────────────────
            // 🔴 seed 를 뽑는 계산은 여기에 적지 않는다. 싱글플레이(GameBootstrapper)와
            //    같은 함수를 쓴다 — 두 벌로 복사하면 언젠가 한쪽만 고쳐져 싱글과 멀티가
            //    서로 다른 방식으로 seed 를 뽑는 상태가 조용히 생긴다.
            // (완전 수식으로 쓴다 — 이 파일은 Unity.Netcode / UGS / UnityEngine 타입을 함께 쓰는 자리라
            //  네임스페이스를 더 열지 않는 편이 이름 충돌 위험이 없다. 같은 이유로
            //  Unity.Services.Lobbies.Models.Lobby 도 이 파일에서 완전 수식으로 쓰고 있다.)
            ulong rootSeed = Hexiege.Domain.MapRootSeed.Create();

            // 🔴 2026-09-14: 종전에는 두 번째 인자로 ReadMapTestModeEnabled() 를 함께 넘겼다.
            //    「맵 테스트 모드」가 규칙에서 삭제돼 인자가 root seed 하나로 줄었다.
            bool started = transfer.BeginHostMapTransfer(rootSeed, roundKind);

            if (!started)
            {
                // 맵 준비 자체가 실패했다(실패 로그는 BeginHostMapTransfer 안에서 이미 운영 축으로
                // 남겼다 — MapPreparationFailed). 여기서 또 운영 로그를 내지 않는다(금지 9).
                GameLog.Dev.Warn("Network", nameof(NetworkGameManager),
                                 "맵 전송을 시작하지 못했다 — 전투 씬으로 넘어가지 않는다",
                                 $"Round={roundKind}, RootSeed={rootSeed}");
                FailMapTransferGate("BeginHostMapTransferRejected");
            }
        }

        /// <summary>
        /// [Host] 맵 전송이 성공했다 → <b>이제서야</b> 전투 씬을 로드한다.
        /// 규칙 16 *"해시가 같을 때만 전투 씬 전환을 시작한다"* 가 지켜지는 지점이다.
        /// </summary>
        private void HandleMapTransferSucceeded()
        {
            // 🔴 이번 회차의 결말을 이미 처리했다면 아무것도 하지 않는다.
            //    특히 "실패로 판정해 로비에 남기로 한 뒤" 성공 통보가 늦게 따라오는 경우를 막는다 —
            //    그대로 두면 실패한 판인데 전투 씬으로 넘어간다(규칙 16 정면 위반).
            if (_mapTransferGateSettled) return;
            _mapTransferGateSettled = true;

            GameLog.Dev.Info("Network", nameof(NetworkGameManager),
                             "맵 전송 성공 — 씬 전환 게이트를 통과했다");

            CleanupMapTransferSubscription();
            _mapTransferInProgress = false;

            // 🔴 콜백을 **먼저 꺼내 두고 필드를 비운 뒤** 부른다. 성공 콜백 안에서 씬이 재로드되는 등
            //    무슨 일이 일어날지 모르는데, 그 안에서 이 필드를 다시 읽으면 지난 회차 값이 보인다.
            Action successAction = _mapTransferSuccessAction;
            ClearMapTransferRoundActions();

            if (successAction == null)
            {
                // [개발] 정상 흐름에서는 올 수 없다 — 두 진입점 모두 성공 콜백을 반드시 넘긴다.
                //   🔴 여기서 LoadGameScene() 으로 넘어가지 **않는다.** 누가 시작한 회차인지 모르는
                //      채로 씬을 넘기면, 재경기였을 경우 StartRematch() 의 정리 절차를 건너뛴
                //      전투 씬이 열린다(지난 판의 유닛·건물이 남는다).
                GameLog.Dev.Warn("Network", nameof(NetworkGameManager),
                                 "맵 전송은 성공했는데 이번 회차의 성공 처리 콜백이 없다 — 아무것도 하지 않는다");
                return;
            }

            successAction();
        }

        /// <summary>
        /// [Host] 맵 전송이 실패했다 → <b>씬을 넘기지 않고</b> 로비를 유지한다.
        /// </summary>
        /// <param name="code">전송 쪽이 판정한 내부 error code</param>
        private void HandleMapTransferFailed(MapTransferErrorCode code)
        {
            // 🔴 운영 축 결말 로그(MapTransferFailed / MapHashMismatch / MapClientVerificationFailed)는
            //    NetworkMapTransfer 가 이미 정확히 한 줄 남겼다. 여기서 같은 사건을 운영으로 또
            //    남기면 한 판이 두 번 세어진다(LogRules 1.14 금지 9). 그래서 개발 축으로만 남긴다.
            //    다만 **게이트가 막았다는 사실 자체**는 반드시 기록한다 — 실기에서 "전송이 잘못됐나 /
            //    게이트가 잘못됐나 / 투영이 잘못됐나"를 로그만 보고 가려내야 하기 때문이다.
            GameLog.Dev.Warn("Network", nameof(NetworkGameManager),
                             "맵 전송 실패 — 씬 전환 게이트가 전투 씬 전환을 막았다(로비 유지)",
                             $"ErrorCode={code}");

            CleanupMapTransferSubscription();
            FailMapTransferGate("MapTransferFailed:" + code);
        }

        /// <summary>
        /// 게이트가 막았음을 바깥에 알린다. <b>이번 회차를 시작한 쪽</b>이 받는다.
        ///   · 최초 경기 → <see cref="OnMapTransferFailed"/> 이벤트(로비 UI 가 구독) = 종전 그대로
        ///   · 재경기   → <see cref="BeginRematchMapTransfer"/> 에 넘어온 실패 콜백(결과 화면)
        ///
        /// 🔴 <b>이 함수를 거치지 않는 실패 경로를 만들면 안 된다.</b> 아무에게도 알리지 않고
        ///    돌아가면 로비가 "게임에 접속하는 중..." 로딩 화면인 채 영영 멈추고,
        ///    재경기라면 결과 화면의 두 버튼이 잠긴 채 영영 돌아오지 않는다.
        /// </summary>
        /// <param name="reason">진단용 사유 문자열(플레이어에게 보이는 문구가 아니다)</param>
        private void FailMapTransferGate(string reason)
        {
            // 🔴 한 회차에 한 번만 알린다. 같은 회차의 실패 경로가 두 번 겹칠 수 있기 때문이다.
            //    (예: BeginHostMapTransfer 안에서 이미 실패 통보가 나간 뒤 그 함수가 false 를
            //     돌려주어 호출부가 한 번 더 알리려 하는 경우.)
            if (_mapTransferGateSettled) return;
            _mapTransferGateSettled = true;

            _mapTransferInProgress = false;

            // 위 성공 자리와 같은 이유로 **먼저 꺼내 두고 비운 뒤** 부른다.
            Action<string> failureAction = _mapTransferFailureAction;
            ClearMapTransferRoundActions();

            if (failureAction != null)
            {
                // 재경기 회차 — 결과 화면 쪽으로만 알린다. 🔴 로비 UI 이벤트는 발행하지 않는다.
                failureAction(reason);
                return;
            }

            // 최초 경기 회차 — 종전과 같이 로비 UI 가 구독하는 이벤트로 알린다.
            OnMapTransferFailed?.Invoke(reason);
        }

        /// <summary>
        /// 이번 회차의 결말 콜백 두 개를 비운다. 회차가 결말을 맺었거나(성공·실패)
        /// 세션이 끝났을 때(로비 복귀·연결 종료) 부른다.
        ///
        /// 🔴 <b>비우지 않으면 지난 회차의 콜백이 다음 회차에 그대로 살아 있다.</b>
        ///    예를 들어 재경기 실패 콜백이 남아 있는 채로 다음 판이 시작되면,
        ///    로비에서 실패했을 때 이미 사라진 결과 화면을 복원하려 든다.
        /// </summary>
        private void ClearMapTransferRoundActions()
        {
            _mapTransferSuccessAction = null;
            _mapTransferFailureAction = null;
        }

        /// <summary>
        /// 전송 객체 구독을 푼다. 씬 전환·객체 파괴와 무관하게 반드시 짝을 맞춘다.
        /// </summary>
        private void CleanupMapTransferSubscription()
        {
            if (_activeMapTransfer == null) return;

            _activeMapTransfer.OnHostTransferSucceeded -= HandleMapTransferSucceeded;
            _activeMapTransfer.OnHostTransferFailed -= HandleMapTransferFailed;
            _activeMapTransfer = null;
        }

        // 🔴 2026-09-14 제거: 여기에 private bool ReadMapTestModeEnabled() 가 있었다.
        //    로비 씬에는 GameBootstrapper 가 없어 Inspector 주입 GameConfig 참조가 없으므로
        //    Resources.Load<GameConfig>("Config/GameConfig") 로 같은 에셋을 직접 읽어
        //    맵 테스트 모드 표식을 얻던 함수이고, 유일한 호출부는 BeginHostMapTransfer 였다.
        //    「맵 테스트 모드」가 규칙에서 삭제돼(GameSystemRules_RandomMap.md 규칙 3 아래
        //    2026-09-14 개정 블록) 읽을 설정 자체가 없어졌다.
        //
        //    ✅ 덤: 이 함수가 사라지면서 이 파일의 Resources.Load 직접 호출도 함께 사라졌다.
        //       (「의존성 조합은 GameBootstrapper 한 곳」 제약과의 마찰 2건 중 1건이 해소된 것이며,
        //        나머지 1건 — NetworkMapTransfer.BeginHostMapTransfer 가 MapPreparationUseCase 를
        //        스스로 조립하는 것 — 은 이번 작업으로 달라지지 않았다.)

        /// <summary>
        /// 서버에서 Game 씬을 로드. NGO SceneManager가 모든 클라이언트에 자동 동기화.
        ///
        /// ⚠️ <b>이 메서드를 바깥에서 직접 부르지 말 것</b>(무작위 맵 3단계 I).
        ///    맵이 준비되지 않은 채 씬이 넘어가면 규칙 16 이 깨진다.
        ///    바깥의 진입점은 <see cref="BeginMapTransferAndLoadGameScene"/> 하나다.
        /// </summary>
        public void LoadGameScene()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                // 요청만 무시하고 게임은 계속되므로 Warn.
                // 다만 정상 흐름에서는 발생할 수 없는 불변식 위반이고 통지 경로가 없다 → 운영.
                GameLog.Ops.Warn(LogEvent.SceneLoadRequestedByNonServer, "Network", nameof(NetworkGameManager),
                                 "서버가 아닌 쪽에서 Game 씬 로드를 요청했다 — 무시한다",
                                 $"HasSingleton={NetworkManager.Singleton != null}");
                return;
            }

            GameLog.Dev.Info("Network", nameof(NetworkGameManager), "Game 씬 로드 시작");
            NetworkManager.Singleton.SceneManager
                .LoadScene("Game", LoadSceneMode.Single);
        }

        // ====================================================================
        // NetworkManager 헬퍼
        // ====================================================================

        /// <summary>
        /// NGO Client 접속 콜백. HOST 전용.
        /// Host 자신(LocalClientId)을 제외한 실제 Client 접속 시 OnClientConnected 발행.
        ///
        /// 또한 2명(또는 그 이상) 접속이 완료되었으면 OnAllPlayersReady 이벤트도 발행하여
        /// LobbyUI가 NetworkManager에 직접 의존하지 않고 로비 숨김 처리를 할 수 있도록 한다.
        /// </summary>
        private void HandleClientConnected(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return;
            if (clientId == NetworkManager.Singleton.LocalClientId) return;

            GameLog.Dev.Info("Network", nameof(NetworkGameManager),
                             "Client 접속 감지 — OnClientConnected 발행", $"ClientId={clientId}");
            OnClientConnected?.Invoke();

            // 전체 접속 수가 2명 이상이면 OnAllPlayersReady 발행
            // (LobbyUI가 NetworkManager.OnClientConnectedCallback을 직접 구독하지 않도록 책임을 분리)
            int connectedCount = NetworkManager.Singleton.ConnectedClientsList.Count;
            if (connectedCount >= 2)
            {
                GameLog.Dev.Info("Network", nameof(NetworkGameManager), "OnAllPlayersReady 발행",
                                 $"ConnectedCount={connectedCount}");
                OnAllPlayersReady?.Invoke(connectedCount);
            }
        }

        /// <summary>
        /// NetworkManager.StartHost() 호출.
        /// NetworkManager.Singleton 이 없으면 에러 반환.
        /// </summary>
        private bool StartNetworkHost()
        {
            if (NetworkManager.Singleton == null)
            {
                // 불변식 위반 — "GameBootstrapper 가 유일한 조합 루트"라는 전제가 깨진 상태다.
                GameLog.Ops.Error(LogEvent.NetworkManagerSingletonMissing, "Network", nameof(NetworkGameManager),
                                  "NetworkManager.Singleton 이 null 이라 Host 를 시작할 수 없다",
                                  "Role=Host");
                return false;
            }

            bool result = NetworkManager.Singleton.StartHost();

            // 중괄호를 명시한다 — 원래 없었는데, 본문이 한 줄일 때 이후 편집에서
            // 문장을 덧붙이면 조건 밖으로 새기 쉽다(동작은 동일하다).
            // 성공 쪽은 개발, 실패 쪽은 운영으로 판정이 갈리므로 조건을 뒤집을 이유가 없다.
            // 릴리스에서는 Dev 호출이 [Conditional] 로 제거돼 if (result) { } 가 되지만
            // 문법·동작 모두 문제없다.
            if (result)
            {
                GameLog.Dev.Info("Network", nameof(NetworkGameManager), "NetworkManager.StartHost() 성공");
            }
            else
            {
                // 호스트 시작이 거부되면 게임을 만들 수 없다 — 복구 경로가 없다.
                GameLog.Ops.Error(LogEvent.NetworkSessionStartFailed, "Network", nameof(NetworkGameManager),
                                  "NetworkManager.StartHost() 가 false 를 반환했다", "Role=Host");
            }

            return result;
        }

        /// <summary>
        /// NetworkManager.StartClient() 호출.
        /// NetworkManager.Singleton 이 없으면 에러 반환.
        /// </summary>
        private bool StartNetworkClient()
        {
            if (NetworkManager.Singleton == null)
            {
                // StartNetworkHost 와 같은 사건 유형이므로 같은 키를 쓴다.
                // 키가 갈리면 "조합 루트가 깨진 횟수"라는 지표가 둘로 쪼개진다.
                GameLog.Ops.Error(LogEvent.NetworkManagerSingletonMissing, "Network", nameof(NetworkGameManager),
                                  "NetworkManager.Singleton 이 null 이라 Client 를 시작할 수 없다",
                                  "Role=Client");
                return false;
            }

            bool result = NetworkManager.Singleton.StartClient();

            // 중괄호를 명시한다 — 위 StartNetworkHost 와 같은 이유다.
            if (result)
            {
                GameLog.Dev.Info("Network", nameof(NetworkGameManager), "NetworkManager.StartClient() 성공");
            }
            else
            {
                GameLog.Ops.Error(LogEvent.NetworkSessionStartFailed, "Network", nameof(NetworkGameManager),
                                  "NetworkManager.StartClient() 가 false 를 반환했다", "Role=Client");
            }

            return result;
        }

        /// <summary>
        /// NetworkManager 를 안전하게 종료.
        /// </summary>
        private void ShutdownNetworkManager()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
                GameLog.Dev.Info("Network", nameof(NetworkGameManager), "NetworkManager Shutdown 완료");
            }
        }

        // ====================================================================
        // 로비 복귀
        // ====================================================================

        /// <summary>
        /// 게임 종료 후 로비로 안전하게 복귀.
        /// NetworkGameEndController에서 호출. NGO 구독 해제 → Shutdown → 씬 전환.
        /// NetworkBehaviour 내부에서 Shutdown() 직접 호출 시 Despawn 타이밍 문제가 있으므로
        /// DontDestroyOnLoad인 이 매니저에 위임하여 안전하게 처리.
        /// </summary>
        /// <param name="lobbySceneName">전환할 로비 씬 이름.</param>
        public void BackToLobby(string lobbySceneName = "Lobby")
        {
            // 1. OnClientConnectedCallback 구독 해제
            //    (HandleClientConnected가 BeginMapTransferAndLoadGameScene 을 재트리거하는 것을 막는다)
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            }

            // 1-A. 무작위 맵 3단계 I: 맵 준비 상태와 확정 맵을 함께 비운다.
            //      규칙 14 *"로비 복귀 또는 연결 종료 시 폐기"* — 남겨 두면 다음 판에서
            //      지난 판 맵이 조용히 재사용될 여지가 생긴다.
            CleanupMapTransferSubscription();
            _mapTransferInProgress = false;
            // [재경기 맵 A] 회차 결말 콜백도 같은 자리에서 비운다 — 세션이 끝났는데 콜백이
            //   남아 있으면 이미 사라진 화면을 되살리려 드는 코드가 다음 판에서 불린다.
            ClearMapTransferRoundActions();
            MapHandoff.Clear();

            // 2. Heartbeat 중지
            StopHeartbeat();

            // 3. Lobby 퇴장 (fire-and-forget)
            _ = _lobbyManager?.LeaveLobbyAsync();

            // 4. NGO 연결 해제
            ShutdownNetworkManager();

            // 5. 씬 전환 — SceneLoader(Presentation)를 직접 참조하지 않고
            //    GameEvents(Application)를 경유해 GameEndUI(Presentation)가 처리하도록 한다(UI 규칙 L-4).
            GameEvents.OnNetworkBackToLobby.OnNext(lobbySceneName);
        }

        // ====================================================================
        // Heartbeat 관리
        // ====================================================================

        /// <summary>
        /// Host 전용 Heartbeat 코루틴 시작.
        /// </summary>
        private void StartHeartbeat()
        {
            StopHeartbeat(); // 중복 실행 방지
            _heartbeatCoroutine = StartCoroutine(_lobbyManager.HeartbeatCoroutine());
            // 실제 Heartbeat 전송 실패는 LobbyManager 가 운영 로그로 잡는다(원칙 1).
            GameLog.Dev.Info("Network", nameof(NetworkGameManager), "Heartbeat 코루틴 시작");
        }

        /// <summary>
        /// 실행 중인 Heartbeat 코루틴 정지.
        /// </summary>
        private void StopHeartbeat()
        {
            if (_heartbeatCoroutine != null)
            {
                StopCoroutine(_heartbeatCoroutine);
                _heartbeatCoroutine = null;
                GameLog.Dev.Info("Network", nameof(NetworkGameManager), "Heartbeat 코루틴 정지");
            }
        }
    }
}
