// ============================================================================
// NetworkStatusUI.cs
// 네트워크 상태(Ping/RTT) 표시 및 연결 끊김 처리 UI.
//
// 역할:
//   1. 매 0.5초마다 NetworkGameManager.GetCurrentRttMs()로 RTT(ms) 읽어 텍스트 갱신
//   2. 싱글플레이/미연결 시 패널 전체 비활성화
//   3. 연결 끊김 감지:
//      - 서버: 상대방(클라이언트)이 나갔을 때 ReconnectionHandler에 위임 (이 UI는 발행 안 함)
//      - 클라이언트: 서버 연결이 끊겼을 때 NetworkGameManager.OnServerDisconnected 수신 → 팝업 표시
//      - 🔴 단, **경기가 끝나 결과 화면이 떠 있는 동안에는 이 팝업을 띄우지 않는다**
//        (공통 UI 규칙 D-1 — 아래 OnServerDisconnected 주석이 이유를 설명한다)
//
// 씬 구조 (Inspector에서 수동 배치):
//   [UI] Canvas
//     └─ NetworkStatusPanel (_networkStatusPanel, 멀티플레이 전용)
//         └─ PingText (_pingText, TMP)
//     └─ DisconnectPanel (_disconnectPanel, 연결 끊김 팝업)
//         ├─ DisconnectText ("상대방이 연결을 끊었습니다")
//         └─ ReturnButton (_disconnectReturnButton)
//
// 주의:
//   - NetworkBehaviour 불필요: 로컬 표시 전용 (Presentation 레이어)
//   - Unity.Netcode / UTP 직접 의존 없음 — NetworkGameManager API로 통일
//   - 씬 전환: SceneLoader.Load 사용 (로딩 인디케이터 자동 표시)
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour).
// ============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UniRx;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 네트워크 상태 표시 및 연결 끊김 처리 UI 컴포넌트.
    /// 멀티플레이 미연결 시 자동으로 비활성화됨.
    /// </summary>
    public class NetworkStatusUI : MonoBehaviour
    {
        // ====================================================================
        // Inspector 참조
        // ====================================================================

        [Header("Ping 표시")]
        [Tooltip("네트워크 상태 패널 루트 (멀티플레이에서만 표시)")]
        [SerializeField] private GameObject _networkStatusPanel;

        [Tooltip("Ping(RTT) 수치 텍스트 (예: '42ms')")]
        [SerializeField] private TextMeshProUGUI _pingText;

        [Header("연결 끊김 팝업")]
        [Tooltip("연결 끊김 알림 패널")]
        [SerializeField] private GameObject _disconnectPanel;

        [Tooltip("로비/메뉴로 돌아가기 버튼")]
        [SerializeField] private Button _disconnectReturnButton;

        [Header("설정")]
        [Tooltip("Ping 갱신 간격 (초)")]
        [SerializeField] private float _pingRefreshInterval = 0.5f;

        // TODO: 재접속(Reconnection) 기능 구현 시, 복구 가능 여부에 따라
        //       게임 씬(SceneLoader.Game) 또는 로비 씬(SceneLoader.Lobby)으로 분기 처리 필요.
        //       현재는 항상 로비로 복귀. ROADMAP B-2 참조.
        [Tooltip("연결 끊김 후 복귀할 씬 이름")]
        [SerializeField] private string _returnSceneName = SceneLoader.Lobby;

        // NetworkGameManager 주입 — NGM의 GetCurrentRttMs / OnServerDisconnected /
        //   IsNetworkRunning / ShutdownNetwork를 사용한다(NetworkManager.Singleton 직접 참조 회피).
        [Header("의존성")]
        [Tooltip("네트워크 게임 매니저. RTT 조회/연결 끊김 알림/Shutdown 위임용. Inspector 미연결 시 자동 탐색.")]
        [SerializeField] private NetworkGameManager _networkGameManager;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary> Ping 갱신 코루틴 참조. 중단 시 사용. </summary>
        private Coroutine _pingCoroutine;

        /// <summary> 이미 연결 끊김 팝업을 표시했는지 여부 (중복 표시 방지). </summary>
        private bool _disconnectHandled;

        /// <summary>
        /// 경기가 끝나 결과 화면(GameEndUI)이 떠 있는지 여부.
        /// true 이면 연결 끊김 팝업을 띄우지 않는다 — 이유는 OnServerDisconnected 주석 참조.
        /// </summary>
        private bool _resultScreenShown;

        /// <summary> 게임 종료 이벤트 구독 해제용. </summary>
        private System.IDisposable _gameEndSubscription;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        private void Start()
        {
            // 초기화: 팝업은 항상 숨김 상태로 시작
            if (_disconnectPanel != null)
                _disconnectPanel.SetActive(false);

            // 버튼 이벤트 등록
            if (_disconnectReturnButton != null)
                _disconnectReturnButton.onClick.AddListener(OnReturnButtonClicked);

            // NetworkGameManager 자동 탐색 (Inspector에 연결 안 된 경우)
            if (_networkGameManager == null)
                _networkGameManager = FindFirstObjectByType<NetworkGameManager>();

            // 네트워크 모드 확인 후 패널 활성/비활성 결정 — NetworkContext / NGM 사용
            // (이전: NetworkManager.Singleton.IsHost || IsClient 직접 호출)
            bool isNetworkMode = NetworkContext.IsNetworkActive ||
                                 (_networkGameManager != null && _networkGameManager.IsNetworkRunning);

            if (_networkStatusPanel != null)
                _networkStatusPanel.SetActive(isNetworkMode);

            if (!isNetworkMode)
            {
                // 싱글플레이: 아무 동작도 하지 않음
                return;
            }

            // 멀티플레이: Ping 갱신 코루틴 시작 + 서버 끊김 이벤트 구독
            _pingCoroutine = StartCoroutine(PingRefreshCoroutine());
            if (_networkGameManager != null)
                _networkGameManager.OnServerDisconnected += OnServerDisconnected;

            // [결과 화면 여부 추적] 경기가 끝났는지만 알면 되므로 GameEvents.OnGameEnd 를 구독한다.
            //   이 이벤트는 싱글/멀티, Host/Client 어느 쪽에서도 결과 화면이 뜨는 순간 발행되므로
            //   (서버는 OnGameEndServer 직전, 클라이언트는 AnnounceWinnerClientRpc 안에서 발행)
            //   역할에 따라 판정이 갈리지 않는다.
            //   🔴 GameEndUI 를 직접 참조하지 않는 이유: UI 컴포넌트끼리 서로를 붙잡으면
            //      배치 순서·파괴 순서에 따라 null 이 되고, 이 프로젝트의 UI 는 서로를 직접
            //      참조하지 않고 GameEvents 로만 소통하는 관례를 쓴다.
            _gameEndSubscription = GameEvents.OnGameEnd
                .Subscribe(_ => _resultScreenShown = true);

            // [개발] Info + 개발. 멀티플레이 진입 시의 의도된 정상 흐름 통보다.
            GameLog.Dev.Info("Network", nameof(NetworkStatusUI), "네트워크 상태 모니터링 시작");
        }

        private void OnDestroy()
        {
            // 코루틴 정리
            if (_pingCoroutine != null)
            {
                StopCoroutine(_pingCoroutine);
                _pingCoroutine = null;
            }

            // 이벤트 구독 해제 (NGM이 살아있는 경우에만)
            if (_networkGameManager != null)
                _networkGameManager.OnServerDisconnected -= OnServerDisconnected;

            // 게임 종료 이벤트 구독 해제 (누수 방지)
            _gameEndSubscription?.Dispose();
        }

        // ====================================================================
        // Ping 갱신 코루틴
        // ====================================================================

        /// <summary>
        /// 매 _pingRefreshInterval초마다 UnityTransport에서 RTT를 읽어 텍스트 갱신.
        /// NetworkManager 또는 Transport가 사라지면 자동 종료.
        /// </summary>
        private IEnumerator PingRefreshCoroutine()
        {
            var wait = new WaitForSeconds(_pingRefreshInterval);

            while (true)
            {
                yield return wait;
                UpdatePingDisplay();
            }
        }

        /// <summary>
        /// NetworkGameManager.GetCurrentRttMs()를 사용하여 RTT를 읽고 텍스트 갱신.
        /// Host에서는 자신(서버)에 대한 RTT(보통 0에 가까움), Client에서는 서버를 향한 RTT.
        /// NGM이 없거나 네트워크 미연결 시 "--ms" 표시.
        ///
        /// 이전에는 UnityTransport를 직접 참조해 GetCurrentRtt를 호출했으나,
        /// Presentation 레이어의 Unity.Netcode.Transports.UTP 직접 의존을 제거하고자
        /// 책임을 Infrastructure 레이어인 NGM으로 이전.
        /// </summary>
        private void UpdatePingDisplay()
        {
            if (_pingText == null) return;
            if (_networkGameManager == null || !_networkGameManager.IsNetworkRunning)
            {
                _pingText.text = "--ms";
                return;
            }

            ulong rttMs = _networkGameManager.GetCurrentRttMs();
            _pingText.text = $"{rttMs}ms";
        }

        // ====================================================================
        // 연결 끊김 처리
        // ====================================================================

        /// <summary>
        /// NetworkGameManager.OnServerDisconnected 이벤트 수신.
        /// NGM은 "클라이언트 측에서 서버 연결이 끊긴 경우"에만 이 이벤트를 발행한다.
        /// 서버(Host) 측 상대방 끊김은 ReconnectionHandler가 별도 처리하므로
        /// 본 핸들러에서는 추가 분기 없이 곧바로 팝업을 표시한다.
        ///
        /// 이전 OnClientDisconnected(ulong clientId)는 NetworkManager.IsServer 분기로
        /// 서버/클라이언트를 구분했으나, NGM이 책임을 가져가면서 본 메서드는 클라이언트 전용으로 단순화되었다.
        /// </summary>
        private void OnServerDisconnected()
        {
            if (_disconnectHandled) return;

            // ================================================================
            // 🔴 경기가 끝나 결과 화면이 떠 있으면 이 팝업을 띄우지 않는다 (공통 UI 규칙 D-1).
            //
            // [초급자용 설명] 왜 억제하는가
            //   결과 화면에서 상대가 나가면, 그 사실은 **결과 화면의 타이머 텍스트**가
            //   상대가 떠났다는 문구와 남은 시간을 함께 적어 알려 준다(규칙 D-1 —
            //   그 문구의 원본은 GameEndUI 의 OpponentLeftCountdownFormat 상수 한 곳뿐이다).
            //   규칙 D-1 은 그 알림을 위해 **별도 팝업을 새로 띄우지 말라**고 정한다 —
            //   「상대가 떠났다」와 「남은 시간」은 사용자에게 한 덩어리의 정보이고,
            //   두 자리로 나누면 서로를 가리기 때문이다.
            //   그런데 이탈 판정은 연결도 함께 종료하므로(규칙 17) 그 종료가
            //   NetworkGameManager.HandleClientDisconnected 를 타고 이 핸들러까지 올라온다.
            //   (의도적으로 나갈 때도 OnClientDisconnectCallback 구독이 해제되지 않아
            //    같은 핸들러가 불린다 — NetworkGameManager 쪽 주석에 그 사실이 적혀 있다.)
            //   그대로 두면 결과 화면 위에 "상대방이 연결을 끊었습니다" 팝업이 겹쳐 떠서
            //   규칙 D-1 의 문구를 가린다. 그래서 **결과 화면일 때만** 팝업을 건너뛴다.
            //
            // 🔴 [무엇을 죽이지 않았는가] **경기 진행 중(결과 화면이 뜨기 전)의 진짜 연결 끊김은
            //    종전과 똑같이 이 팝업을 띄운다.** 그 경로에서는 결과 화면이 없어 사용자에게
            //    사유를 알릴 다른 자리가 아예 없으므로 팝업이 유일한 통보 수단이다.
            //    아래 플래그는 결과 화면이 떠 있을 때만 true 가 되므로 인게임 경로는 무영향이다.
            //
            // 사용자가 화면에 갇히지 않는 근거: 결과 화면의 로비 복귀 버튼은 어떤 상태에서도
            //    꺼지지 않으며(규칙 D-3), 카운트다운이 만료되면 자동으로 로비로 이동한다(규칙 D-4).
            // ================================================================
            if (_resultScreenShown)
            {
                // [개발] Info + 개발. 「팝업을 일부러 띄우지 않았다」는 판단을 남긴다 —
                //   화면에 아무 변화가 없는 분기라, 로그가 없으면 나중에 "팝업이 안 뜬다"는
                //   버그 제보와 이 의도된 억제를 구분할 수 없다.
                GameLog.Dev.Info("Network", nameof(NetworkStatusUI),
                    "서버 연결 끊김 알림 수신 — 결과 화면이 떠 있어 팝업을 띄우지 않는다(규칙 D-1)");
                _disconnectHandled = true;
                return;
            }

            // [개발] Info + 개발.
            //   축 A: 이 자리는 "끊김을 알리는 이벤트를 받았다"는 사실만 남기는 통보다.
            //         끊김 자체의 심각도 판정과 운영 기록은 NetworkGameManager /
            //         ReconnectionHandler 가 이미 갖고 있다.
            //   축 B: 원인(끊김 사유)은 이 자리에 도달하지 않으므로 최종 처리 지점이 아니다(1.3 ②).
            GameLog.Dev.Info("Network", nameof(NetworkStatusUI), "서버 연결 끊김 알림 수신 — 팝업 표시");
            _disconnectHandled = true;
            ShowDisconnectPopup();
        }

        /// <summary>
        /// 연결 끊김 팝업 표시. Time.timeScale은 건드리지 않음 (게임 중단 없이 알림).
        /// </summary>
        private void ShowDisconnectPopup()
        {
            if (_disconnectPanel != null)
                _disconnectPanel.SetActive(true);

            // [개발] Info + 개발. UI 상태 전이 통보.
            GameLog.Dev.Info("Network", nameof(NetworkStatusUI), "연결 끊김 팝업 표시");
        }

        // ====================================================================
        // 버튼 핸들러
        // ====================================================================

        /// <summary>
        /// 연결 끊김 팝업의 "확인" 버튼 클릭 시.
        /// NetworkGameManager.ShutdownNetwork()를 호출하고 지정된 씬으로 복귀.
        ///
        /// 이전: NetworkManager.Singleton.Shutdown() 직접 호출
        /// 변경: NGM의 공개 래퍼 ShutdownNetwork() 호출 — Unity.Netcode 직접 의존 제거
        /// </summary>
        private void OnReturnButtonClicked()
        {
            // [개발] Info + 개발. 사용자가 직접 누른 버튼의 정상 흐름 통보.
            GameLog.Dev.Info("Network", nameof(NetworkStatusUI), "로비 복귀 버튼 클릭");

            // 시간 복원 (게임 종료 팝업과 함께 표시된 경우)
            Time.timeScale = 1f;

            // 네트워크 종료 — NGM에 위임
            if (_networkGameManager != null)
                _networkGameManager.ShutdownNetwork();

            // 복귀 씬 로드.
            // SceneLoader.Load 가 내부에서 로딩 인디케이터를 띄우므로 별도 ShowLoading 호출은 제거했다.
            // 연결 끊김 복귀도 네트워크 종료 + 씬 전환이 일어나므로 그 사이 사용자가 멈춘 화면을 보지 않게 된다.
            // 로딩을 끄는 책임은 목적지 씬의 초기화 완료 시점이 담당한다(UI 규칙 L-3).
            SceneLoader.Load(_returnSceneName, "로비로 이동 중...");
        }
    }
}
