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
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

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

        // ====================================================================
        // 내부 매니저
        // ====================================================================

        private UnityServicesInitializer _servicesInitializer;
        private LobbyManager _lobbyManager;
        private RelayManager _relayManager;

        // Heartbeat 코루틴 추적용
        private Coroutine _heartbeatCoroutine;

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
        }

        private void OnDestroy()
        {
            StopHeartbeat();
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
        public async Task HostGameAsync(string lobbyName = "Hexiege Room")
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
                var lobby = await _lobbyManager.CreateLobbyAsync(lobbyName, maxPlayers: 2, relayJoinCode);
                if (lobby == null)
                {
                    const string errorMsg = "Lobby 생성 실패. Unity Lobby 서비스를 확인하세요.";
                    Debug.LogError($"[Network] {errorMsg}");
                    OnError?.Invoke(errorMsg);
                    return;
                }

                // 3. NetworkManager Host 시작
                if (!StartNetworkHost())
                {
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

                // 4. NetworkManager Client 시작
                if (!StartNetworkClient())
                {
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

            // NetworkManager 종료
            ShutdownNetworkManager();

            // Lobby 나가기
            await _lobbyManager.LeaveLobbyAsync();

            Debug.Log("[Network] Disconnect 완료.");
            OnDisconnected?.Invoke();
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

        // ====================================================================
        // NetworkManager 헬퍼
        // ====================================================================

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
