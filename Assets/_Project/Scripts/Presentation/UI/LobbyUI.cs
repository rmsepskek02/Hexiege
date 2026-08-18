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
using Hexiege.Application;      // GameLog — 런타임 로그 파사드 (LogRules.md 1.4)
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
            // 플레이어 준비 알림은 NGM.OnAllPlayersReady 이벤트로 받는다(NetworkManager 콜백 직접 구독 회피).
            if (_networkGameManager == null)
                _networkGameManager = FindFirstObjectByType<NetworkGameManager>();

            if (_networkGameManager == null)
            {
                // [개발] Warn + 개발.
                //   축 A: 원본은 LogError 였지만 축 A 의 판정 질문은 "복구되었나?" 하나뿐이다(1.2).
                //         이건 실행 중 고장이 아니라 "씬에 오브젝트를 안 넣었다"는 설정 오류이고,
                //         LogRules 1.3 원칙 3 단서가 설정 오류를 Warn 으로 낮추라고 규정한다.
                //   축 B: FindFirstObjectByType 은 네트워크 스폰 여부와 무관하게 씬의 활성 오브젝트를
                //         찾으므로, null = "씬에 없다" 다. 있으면 모든 기기에 있고 없으면 모든 기기에 없다
                //         → 축 B ①("플레이어 기기에서만 벌어지는가")이 항상 "아니오" → 개발.
                GameLog.Dev.Warn("UI", nameof(LobbyUI),
                                 "NetworkGameManager 를 찾을 수 없다 — 씬에 NetworkGameManager GameObject 를 배치해야 한다");
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
            // [개발] Info + 개발. 의도된 정상 흐름 통보이고, 화면에도 같은 안내가 이미 떠 있다.
            GameLog.Dev.Info("UI", nameof(LobbyUI), "로비 UI 초기화 완료 — UGS 초기화 시작");

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
                // [개발] Error + 개발 (원본 레벨 유지).
                //   축 B: 같은 사건의 원인은 하위 계층이 이미 운영 로그로 남긴다 —
                //         UnityServicesInitializer 가 LogEvent.UnityServicesInitializeFailed 를 남기고,
                //         NetworkGameManager.InitializeAsync 의 onFailure 도 이미 개발로 내려져 있다.
                //         상위 호출부인 이 자리가 또 운영으로 남으면 한 사건이 두 번 집계된다
                //         (LogRules 1.3 「원칙 간 우선순위」① · 1.14 금지 9).
                //   ⚠️ 개발 축의 Warn 메서드에는 Exception 오버로드가 없다. 예외 타입은 집계의 핵심 축이라
                //      e.Message 로 눌러 담지 않고(1.9), 예외 오버로드가 있는 Dev.Error 를 쓴다.
                GameLog.Dev.Error("UI", nameof(LobbyUI),
                                  "UGS 초기화 예외 — 원인은 UnityServicesInitializer 가 운영 로그로 남긴다", e);
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
                // [개발] Error + 개발 (원본 레벨 유지).
                //   NetworkGameManager.HostGameAsync 는 자체 최종 catch 에서
                //   Ops.Error(GameSessionStartUnhandledException, ..., e, "Flow=Host") 를 남기고
                //   예외를 다시 던지지 않는다. 즉 원인을 쥔 최종 처리 지점은 그쪽이고(1.3 ②),
                //   이 자리는 중복 집계를 막기 위해 개발로 내린다(1.14 금지 9).
                GameLog.Dev.Error("UI", nameof(LobbyUI),
                                  "방 생성(Host) 예외 — 원인은 NetworkGameManager 가 운영 로그로 남긴다", e,
                                  "Flow=Host");
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
            // [개발] Info + 개발. 정상 흐름 통보이고 같은 코드가 화면에도 표시된다.
            //   Lobby Code 는 LogRules 1.6 이 규정한 민감 데이터 3항목(이메일/UID·PlayerId/토큰)이 아니라
            //   평문으로 남긴다 — 이는 선행 이관에서 확정된 표기다.
            GameLog.Dev.Info("UI", nameof(LobbyUI), "Host 시작 완료", $"LobbyCode={lobbyCode}");
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
                // [개발] Error + 개발 (원본 레벨 유지). 위 HostGame 과 완전히 같은 구조다 —
                //   NetworkGameManager.JoinGameAsync 가 최종 catch 에서 운영 로그를 남기고
                //   예외를 다시 던지지 않으므로, 이 자리는 개발로 내려 중복 집계를 막는다.
                GameLog.Dev.Error("UI", nameof(LobbyUI),
                                  "방 참가(Join) 예외 — 원인은 NetworkGameManager 가 운영 로그로 남긴다", e,
                                  "Flow=Join");
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
            // [개발] Info + 개발. 정상 흐름 통보.
            GameLog.Dev.Info("UI", nameof(LobbyUI), "Client 접속 완료 — 게임 시작 대기");
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
            // [개발] Info + 개발. 정상 흐름의 이벤트 수신 통보.
            //   ConnectedCount 는 key=value 로 남긴다 — 자유 문장이 아니라 집계 가능한 값이다(1.4).
            GameLog.Dev.Info("UI", nameof(LobbyUI), "OnAllPlayersReady 수신", $"ConnectedCount={connectedCount}");
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
            // [개발] Warn + 개발.
            //   이 자리는 NetworkGameManager 의 OnError 콜백이다. errorMessage 는 이미 가공된
            //   사용자 안내 문구일 뿐 예외 타입도 원인도 담고 있지 않고, 그 원인은 NGM 과 그 하위
            //   계층이 이미 운영 로그로 남겼다 → 최종 처리 지점이 아니다(1.3 ②).
            //   화면 통지(SetStatus)도 그대로 유지되므로 잃는 정보가 없다.
            GameLog.Dev.Warn("UI", nameof(LobbyUI),
                             $"네트워크 오류 통지 수신 — 원인은 NetworkGameManager 계층이 운영 로그로 남긴다: {errorMessage}");
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

            // [개발] Info + 개발. 정상 흐름의 UI 상태 전이 통보.
            GameLog.Dev.Info("UI", nameof(LobbyUI), "로비 패널 숨김 — 게임 시작 대기");
        }
    }
}
