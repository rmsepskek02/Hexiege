// ============================================================================
// LobbyUI.cs
// 게임 시작 전 로비 화면 UI. 방 만들기(Host)와 코드로 참가(Client)를 제공.
//
// 역할:
//   1. Host: NetworkGameManager.HostGameAsync() 호출 → Join Code 표시
//   2. Join: NetworkGameManager.JoinGameAsync(code) 호출
//   3. 2명 연결 완료 시 로비 패널 숨기기 (게임은 NetworkGameFlow가 시작)
//   4. 오류 발생 시 상태 텍스트에 메시지 표시
//   5. UGS 초기화를 Start()에서 자동 실행
//
// 씬 구조 (Inspector에서 수동 배치):
//   [UI] Canvas
//     └─ LobbyPanel (_lobbyPanel, 게임 시작 전 표시)
//         ├─ StatusText (_statusText, 상태 메시지 / Join Code 표시)
//         ├─ HostButton (_hostButton, "방 만들기")
//         ├─ CodeInputField (_codeInput, TMP_InputField, 로비 코드 입력)
//         ├─ JoinByCodeButton (_joinByCodeButton, "코드로 참가")
//         └─ JoinButton (_joinButton, 미래: 빠른 참가 확장용, 현재는 비활성 가능)
//
// 흐름:
//   게임 시작 → LobbyPanel 표시
//   → Host: HostGame() → 코드 표시 → 상대방 접속 대기
//   → Client: 코드 입력 → JoinByCode() → 게임 시작 대기
//   → 2명 연결 시 → LobbyPanel 숨김 → NetworkGameFlow가 게임 시작
//
// 주의:
//   - NetworkGameManager는 DontDestroyOnLoad로 씬 전환 후에도 유지됨.
//     따라서 씬에 직접 배치 또는 FindFirstObjectByType으로 탐색.
//   - async/await 사용으로 UI 스레드 블로킹 없음.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour).
// ============================================================================

using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 로비 UI. Host/Join 버튼으로 멀티플레이 세션을 시작.
    /// NetworkGameManager에 세션 관리를 위임.
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("패널")]
        // [Rule 5] UI 숨김/표시는 GameObject.SetActive 대신 CanvasGroup으로 제어한다.
        //   - SetActive(false)는 오브젝트 자체를 끄기 때문에, 그 위의 코루틴/이벤트 구독 등이
        //     함께 멈출 수 있고, 다시 켤 때 레이아웃 재계산 비용이 든다.
        //   - CanvasGroup은 오브젝트를 켜둔 채로 alpha(투명도) / blocksRaycasts(클릭 차단) /
        //     interactable(상호작용)만 끄므로 더 안전하고 일관적이다.
        [Tooltip("로비 패널 루트 CanvasGroup. alpha/blocksRaycasts/interactable로 표시·숨김 제어.")]
        [SerializeField] private CanvasGroup _lobbyPanel;

        [Header("버튼")]
        [Tooltip("방 만들기(Host) 버튼")]
        [SerializeField] private Button _hostButton;

        [Tooltip("빠른 참가 버튼 (미래 확장용, 현재는 코드 참가 유도)")]
        [SerializeField] private Button _joinButton;

        [Tooltip("코드로 참가 버튼")]
        [SerializeField] private Button _joinByCodeButton;

        [Header("입력 / 텍스트")]
        [Tooltip("로비 코드 입력 필드")]
        [SerializeField] private TMP_InputField _codeInput;

        [Tooltip("상태 메시지 텍스트. 코드 표시, 오류 메시지에도 사용.")]
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("NetworkGameManager 참조 (선택)")]
        [Tooltip("씬에 직접 배치한 NetworkGameManager. 비워두면 자동 탐색.")]
        [SerializeField] private NetworkGameManager _networkGameManager;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>버튼 조작 잠금 여부. 비동기 작업 중 중복 입력 방지.</summary>
        private bool _isWorking;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Start()
        {
            // NetworkGameManager 자동 탐색 (Inspector에 연결 안 된 경우)
            // [2026-05-20] 리팩토링: FindFirstObjectByType은 유지하되, Unity.Netcode 직접 의존은 제거.
            //   NetworkManager.OnClientConnectedCallback 구독 → NGM.OnAllPlayersReady 이벤트로 위임.
            if (_networkGameManager == null)
                _networkGameManager = FindFirstObjectByType<NetworkGameManager>();

            if (_networkGameManager == null)
            {
                Debug.LogError("[Network] LobbyUI: NetworkGameManager를 찾을 수 없습니다. " +
                               "씬에 NetworkGameManager GameObject를 배치하세요.");
                return;
            }

            // 이벤트 구독
            _networkGameManager.OnHostStarted += OnHostStarted;
            _networkGameManager.OnClientConnected += OnClientJoined;
            _networkGameManager.OnError += OnNetworkError;

            // 2명 접속 완료 시 로비 숨김 — NGM이 NetworkManager 콜백을 가로채 발행.
            // 이전: NetworkManager.Singleton.OnClientConnectedCallback 직접 구독
            // 변경: NGM.OnAllPlayersReady (int connectedCount) 구독으로 위임
            _networkGameManager.OnAllPlayersReady += OnAllPlayersReady;

            // 버튼 이벤트 등록
            if (_hostButton != null)
                _hostButton.onClick.AddListener(() => _ = HostGameAsync());

            if (_joinByCodeButton != null)
                _joinByCodeButton.onClick.AddListener(() => _ = JoinByCodeAsync());

            // 빠른 참가는 현재 미구현 → 코드 입력 유도 메시지 표시
            if (_joinButton != null)
                _joinButton.onClick.AddListener(OnJoinButtonClicked);

            // 로비 패널 표시
            // [Rule 5] SetActive 대신 CanvasGroup으로 표시한다.
            //   alpha=1(완전 불투명) / blocksRaycasts=true(뒤 클릭 차단 및 자기 클릭 허용) /
            //   interactable=true(버튼 등 상호작용 허용).
            if (_lobbyPanel != null)
            {
                _lobbyPanel.alpha = 1f;
                _lobbyPanel.blocksRaycasts = true;
                _lobbyPanel.interactable = true;
            }

            SetStatus("방을 만들거나 코드를 입력하여 참가하세요.");
            Debug.Log("[Network] LobbyUI: 초기화 완료. UGS 초기화 시작.");

            // UGS 자동 초기화 (익명 로그인)
            _ = InitializeAsync();
        }

        private void OnDestroy()
        {
            // 이벤트 해제
            if (_networkGameManager != null)
            {
                _networkGameManager.OnHostStarted -= OnHostStarted;
                _networkGameManager.OnClientConnected -= OnClientJoined;
                _networkGameManager.OnError -= OnNetworkError;
                _networkGameManager.OnAllPlayersReady -= OnAllPlayersReady;
            }
        }

        // ====================================================================
        // UGS 초기화
        // ====================================================================

        /// <summary>
        /// Unity Gaming Services 초기화 (익명 로그인 포함).
        /// 완료 후 버튼 활성화.
        /// </summary>
        private async Task InitializeAsync()
        {
            SetStatus("서비스 초기화 중...");
            SetButtonsInteractable(false);

            try
            {
                await _networkGameManager.InitializeAsync();
                SetStatus("준비 완료. 방을 만들거나 코드로 참가하세요.");
                SetButtonsInteractable(true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Network] LobbyUI: UGS 초기화 예외: {e.Message}");
                SetStatus($"초기화 실패: {e.Message}");
                // 재시도 버튼을 활성화하여 사용자가 다시 시도할 수 있게
                SetButtonsInteractable(true);
            }
        }

        // ====================================================================
        // Host 흐름
        // ====================================================================

        /// <summary>
        /// "방 만들기" 버튼 클릭 처리.
        /// Relay + Lobby 생성 후 상대방 접속 대기.
        /// </summary>
        private async Task HostGameAsync()
        {
            if (_isWorking) return;
            _isWorking = true;
            SetButtonsInteractable(false);
            SetStatus("방 생성 중...");

            try
            {
                await _networkGameManager.HostGameAsync("Hexiege Room");
                // 성공 콜백은 OnHostStarted에서 처리
            }
            catch (Exception e)
            {
                Debug.LogError($"[Network] LobbyUI: HostGame 예외: {e.Message}");
                SetStatus($"방 생성 실패: {e.Message}");
                SetButtonsInteractable(true);
                _isWorking = false;
            }
        }

        /// <summary>
        /// Host 시작 완료. 로비 코드를 상태 텍스트에 표시.
        /// </summary>
        private void OnHostStarted(string lobbyCode)
        {
            _isWorking = false;
            SetStatus($"방 코드: {lobbyCode}\n상대방 접속 대기 중...");
            Debug.Log($"[Network] LobbyUI: Host 시작 완료. 로비 코드: {lobbyCode}");
        }

        // ====================================================================
        // Join 흐름
        // ====================================================================

        /// <summary>
        /// "코드로 참가" 버튼 클릭 처리.
        /// 입력된 코드로 Lobby에 참가.
        /// </summary>
        private async Task JoinByCodeAsync()
        {
            if (_isWorking) return;

            string code = _codeInput != null ? _codeInput.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(code))
            {
                SetStatus("방 코드를 입력하세요.");
                return;
            }

            _isWorking = true;
            SetButtonsInteractable(false);
            SetStatus($"방 참가 중... (코드: {code})");

            try
            {
                await _networkGameManager.JoinGameAsync(code);
                // 성공 콜백은 OnClientJoined에서 처리
            }
            catch (Exception e)
            {
                Debug.LogError($"[Network] LobbyUI: JoinGame 예외: {e.Message}");
                SetStatus($"참가 실패: {e.Message}");
                SetButtonsInteractable(true);
                _isWorking = false;
            }
        }

        /// <summary>
        /// Client 접속 완료. 게임 시작 대기 메시지 표시.
        /// </summary>
        private void OnClientJoined()
        {
            _isWorking = false;
            SetStatus("접속 완료. 게임 시작 대기 중...");
            Debug.Log("[Network] LobbyUI: Client 접속 완료.");
        }

        /// <summary>
        /// 빠른 참가 버튼 클릭. 현재는 코드 입력 유도.
        /// </summary>
        private void OnJoinButtonClicked()
        {
            SetStatus("코드를 입력하여 참가하세요.");
            // 코드 입력 필드에 포커스
            if (_codeInput != null)
                _codeInput.Select();
        }

        // ====================================================================
        // 연결 완료 콜백
        // ====================================================================

        /// <summary>
        /// NGM.OnAllPlayersReady 이벤트 핸들러.
        /// 2명 이상 접속이 완료되면 NGM이 발행하므로, 여기서는 로비 패널 숨김 처리만 수행.
        /// (NetworkGameFlow가 게임 시작을 총괄하므로 LobbyUI는 UI 숨김만 담당)
        ///
        /// 이전에는 NetworkManager.Singleton.OnClientConnectedCallback을 직접 구독해
        /// ConnectedClientsList.Count로 판정했으나, Presentation 레이어가 Unity.Netcode에 직접 의존하지 않도록
        /// 책임을 NGM(Infrastructure)에 이전.
        /// </summary>
        /// <param name="connectedCount">현재까지 접속 완료한 클라이언트 수.</param>
        private void OnAllPlayersReady(int connectedCount)
        {
            Debug.Log($"[Network] LobbyUI: OnAllPlayersReady 수신. 총 접속 수={connectedCount}");
            HideLobby();
        }

        // ====================================================================
        // 오류 처리
        // ====================================================================

        /// <summary>
        /// NetworkGameManager 오류 이벤트 수신.
        /// </summary>
        private void OnNetworkError(string errorMessage)
        {
            _isWorking = false;
            SetStatus($"오류: {errorMessage}");
            SetButtonsInteractable(true);
            Debug.LogWarning($"[Network] LobbyUI: 네트워크 오류 수신: {errorMessage}");
        }

        // ====================================================================
        // UI 헬퍼
        // ====================================================================

        /// <summary>
        /// 상태 텍스트 갱신.
        /// </summary>
        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
        }

        /// <summary>
        /// 모든 버튼의 인터랙션 가능 여부 설정.
        /// </summary>
        private void SetButtonsInteractable(bool interactable)
        {
            if (_hostButton != null) _hostButton.interactable = interactable;
            if (_joinButton != null) _joinButton.interactable = interactable;
            if (_joinByCodeButton != null) _joinByCodeButton.interactable = interactable;
        }

        /// <summary>
        /// 로비 패널 숨김. 게임 시작 시 호출.
        /// </summary>
        private void HideLobby()
        {
            // [Rule 5] SetActive 대신 CanvasGroup으로 숨긴다.
            //   alpha=0(완전 투명) / blocksRaycasts=false(클릭 통과) /
            //   interactable=false(상호작용 차단). 오브젝트 자체는 켜진 상태를 유지한다.
            if (_lobbyPanel != null)
            {
                _lobbyPanel.alpha = 0f;
                _lobbyPanel.blocksRaycasts = false;
                _lobbyPanel.interactable = false;
            }

            Debug.Log("[Network] LobbyUI: 로비 패널 숨김. 게임 시작 대기.");
        }
    }
}
