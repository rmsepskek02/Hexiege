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

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 네트워크 게임 세션 전체 흐름 관리 MonoBehaviour.
    /// 씬에 하나만 배치. DontDestroyOnLoad 로 씬 전환 유지.
    /// </summary>
    public class NetworkGameManager : MonoBehaviour
    {
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
            Debug.Log($"[Network] NGM: 클라이언트 측 서버 연결 끊김 감지 (clientId={clientId}). OnServerDisconnected 발행.");
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
            Debug.Log("[Network] NetworkGameManager: 초기화 시작.");

            await _servicesInitializer.InitializeAsync(
                onSuccess: playerId =>
                {
                    Debug.Log($"[Network] 초기화 성공. PlayerId: {playerId}");
                    OnInitialized?.Invoke(playerId);
                },
                onFailure: e =>
                {
                    Debug.LogError($"[Network] 초기화 실패: {e.Message}");
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
                Debug.Log($"[Network] HostGame 시작. 방 이름: {lobbyName}");

                // 1. Relay 서버 할당 생성 + Join Code 발급
                string relayJoinCode = await _relayManager.CreateRelayAsync();
                if (string.IsNullOrEmpty(relayJoinCode))
                {
                    const string errorMsg = "Relay 할당 실패. 네트워크 상태를 확인하세요.";
                    Debug.LogError($"[Network] {errorMsg}");
                    OnError?.Invoke(errorMsg);
                    return;
                }

                // 2. Lobby 생성 (Relay Join Code 를 Data 에 포함)
                var lobby = await _lobbyManager.CreateLobbyAsync(lobbyName, maxPlayers: 2, relayJoinCode, matchId);
                if (lobby == null)
                {
                    const string errorMsg = "Lobby 생성 실패. Unity Lobby 서비스를 확인하세요.";
                    Debug.LogError($"[Network] {errorMsg}");
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

                Debug.Log($"[Network] Host 게임 시작 완료. Lobby Code: {lobby.LobbyCode}");
                OnHostStarted?.Invoke(lobby.LobbyCode);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Network] HostGame 예외: {e.Message}");
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
                Debug.Log($"[Network] JoinGame 시작. Lobby Code: {lobbyCode}");

                // 1. Lobby 참가
                var lobby = await _lobbyManager.JoinLobbyByCodeAsync(lobbyCode);
                if (lobby == null)
                {
                    const string errorMsg = "Lobby 참가 실패. 코드를 확인하거나 방이 꽉 찼을 수 있습니다.";
                    Debug.LogError($"[Network] {errorMsg}");
                    OnError?.Invoke(errorMsg);
                    return;
                }

                // 2. Lobby Data 에서 Relay Join Code 추출
                string relayJoinCode = _lobbyManager.GetRelayJoinCode();
                if (string.IsNullOrEmpty(relayJoinCode))
                {
                    const string errorMsg = "Relay Join Code 를 Lobby 에서 찾을 수 없습니다. " +
                                            "Host 가 아직 준비되지 않았을 수 있습니다.";
                    Debug.LogError($"[Network] {errorMsg}");
                    OnError?.Invoke(errorMsg);
                    return;
                }

                // 3. Relay 서버 참가 + UnityTransport 설정
                bool relayJoined = await _relayManager.JoinRelayAsync(relayJoinCode);
                if (!relayJoined)
                {
                    const string errorMsg = "Relay 참가 실패.";
                    Debug.LogError($"[Network] {errorMsg}");
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

                Debug.Log("[Network] Client 게임 참가 완료.");
                OnClientConnected?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Network] JoinGame 예외: {e.Message}");
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
            Debug.Log("[Network] Disconnect 시작.");

            // Heartbeat 중단
            StopHeartbeat();

            // Client 접속 콜백 구독 해제 (Host 였을 경우 대비)
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;

            // NetworkManager 종료
            ShutdownNetworkManager();

            // 랜덤 매칭 상태 초기화
            _isRandomMatchmaking = false;

            // Lobby 나가기
            await _lobbyManager.LeaveLobbyAsync();

            Debug.Log("[Network] Disconnect 완료.");
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
            _currentTicketId = await _matchmakerManager.CreateTicketAsync();
            Debug.Log($"[Matchmaker] 티켓 생성: {_currentTicketId}");

            var matchId = await _matchmakerManager.PollUntilMatchedAsync(
                _currentTicketId, _matchmakingCts.Token, onWaitSecond);

            Debug.Log($"[Matchmaker] 매칭 완료. MatchId: {matchId}");

            // 매칭 성사 콜백 — UI에서 로딩 스크린 표시 등에 활용
            onMatchFound?.Invoke();

            bool isHost = await _matchmakerManager.DetermineIsHostAsync(matchId);
            Debug.Log($"[Matchmaker] 역할 결정: {(isHost ? "Host" : "Client")}");

            if (isHost)
            {
                // Host: Relay + Lobby 생성 (Lobby 이름에 matchId 포함하여 Client 가 검색 가능)
                await HostGameAsync($"match_{matchId}", matchId);
            }
            else
            {
                // Client: Host 가 생성한 Lobby 를 matchId 로 검색하여 참가
                await JoinByMatchIdAsync(matchId);
            }
        }

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
                Debug.LogWarning($"[Matchmaker] 티켓 삭제 중 오류 (무시): {e.Message}");
            }

            _currentTicketId = null;
            _isRandomMatchmaking = false;
            Debug.Log("[Matchmaker] 매칭 취소 완료.");
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
        // 씬 전환
        // ====================================================================

        /// <summary>
        /// 서버에서 Game 씬을 로드. NGO SceneManager가 모든 클라이언트에 자동 동기화.
        /// 2명 연결 완료 시 BattleViewModel에서 호출.
        /// </summary>
        public void LoadGameScene()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[Network] LoadGameScene: 서버가 아니므로 무시.");
                return;
            }

            Debug.Log("[Network] LoadGameScene: Game 씬 로드 시작.");
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

            Debug.Log($"[Network] Client 접속 감지 (clientId={clientId}). OnClientConnected 발행.");
            OnClientConnected?.Invoke();

            // 전체 접속 수가 2명 이상이면 OnAllPlayersReady 발행
            // (LobbyUI가 NetworkManager.OnClientConnectedCallback을 직접 구독하지 않도록 책임을 분리)
            int connectedCount = NetworkManager.Singleton.ConnectedClientsList.Count;
            if (connectedCount >= 2)
            {
                Debug.Log($"[Network] OnAllPlayersReady 발행. 접속 수={connectedCount}");
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
                Debug.LogError("[Network] StartNetworkHost: NetworkManager.Singleton 이 null 입니다.");
                return false;
            }

            bool result = NetworkManager.Singleton.StartHost();
            if (result)
                Debug.Log("[Network] NetworkManager.StartHost() 성공.");
            else
                Debug.LogError("[Network] NetworkManager.StartHost() 실패.");

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
                Debug.LogError("[Network] StartNetworkClient: NetworkManager.Singleton 이 null 입니다.");
                return false;
            }

            bool result = NetworkManager.Singleton.StartClient();
            if (result)
                Debug.Log("[Network] NetworkManager.StartClient() 성공.");
            else
                Debug.LogError("[Network] NetworkManager.StartClient() 실패.");

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
                Debug.Log("[Network] NetworkManager Shutdown 완료.");
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
            // 1. OnClientConnectedCallback 구독 해제 (HandleClientConnected가 LoadGameScene 재트리거 방지)
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            }

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
            Debug.Log("[Network] Heartbeat 코루틴 시작.");
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
                Debug.Log("[Network] Heartbeat 코루틴 정지.");
            }
        }
    }
}
