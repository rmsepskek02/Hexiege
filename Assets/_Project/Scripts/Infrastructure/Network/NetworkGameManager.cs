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
