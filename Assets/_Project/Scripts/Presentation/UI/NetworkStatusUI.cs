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
