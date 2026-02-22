// ============================================================================
// NetworkStatusUI.cs
// 네트워크 상태(Ping/RTT) 표시 및 연결 끊김 처리 UI.
//
// 역할:
//   1. 매 0.5초마다 UnityTransport.GetCurrentRtt()로 RTT(ms) 읽어 텍스트 갱신
//   2. 싱글플레이/미연결 시 패널 전체 비활성화
//   3. 연결 끊김 감지:
//      - 서버: 상대방(클라이언트)이 나갔을 때 ReconnectionHandler에 위임
//      - 클라이언트: 서버 연결이 끊겼을 때 연결 끊김 팝업 표시
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
//   - NetworkManager.Singleton null 방어 필수
//   - 씬 전환(SceneManager) 참조: UnityEngine.SceneManagement
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour).
// ============================================================================

using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

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

        [Tooltip("연결 끊김 후 복귀할 씬 이름")]
        [SerializeField] private string _returnSceneName = "SampleScene";

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

            // 네트워크 모드 확인 후 패널 활성/비활성 결정
            bool isNetworkMode = NetworkManager.Singleton != null &&
                                 (NetworkManager.Singleton.IsHost ||
                                  NetworkManager.Singleton.IsClient);

            if (_networkStatusPanel != null)
                _networkStatusPanel.SetActive(isNetworkMode);

            if (!isNetworkMode)
            {
                // 싱글플레이: 아무 동작도 하지 않음
                return;
            }

            // 멀티플레이: Ping 갱신 코루틴 시작 + 연결 끊김 콜백 등록
            _pingCoroutine = StartCoroutine(PingRefreshCoroutine());
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            Debug.Log("[Network] NetworkStatusUI: 네트워크 상태 모니터링 시작.");
        }

        private void OnDestroy()
        {
            // 코루틴 정리
            if (_pingCoroutine != null)
            {
                StopCoroutine(_pingCoroutine);
                _pingCoroutine = null;
            }

            // 콜백 해제 (NetworkManager가 살아있는 경우에만)
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
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
        /// UnityTransport.GetCurrentRtt()를 사용하여 RTT를 읽고 텍스트 갱신.
        /// Host에서는 서버 측 클라이언트 ID 0의 RTT를, Client에서는 서버 RTT를 조회.
        /// RTT 조회 실패 시 "--ms" 표시.
        /// </summary>
        private void UpdatePingDisplay()
        {
            if (_pingText == null) return;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                _pingText.text = "--ms";
                return;
            }

            // UnityTransport 컴포넌트 획득 (NetworkTransport 베이스 클래스 경유)
            UnityTransport transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport
                as UnityTransport;

            if (transport == null)
            {
                _pingText.text = "--ms";
                return;
            }

            // 클라이언트: 서버(NGO 서버 ClientId = 서버 측 고유 ID) RTT 조회
            // Host: 자신이 서버이므로 로컬 연결 → RTT는 0에 가까움
            // 클라이언트가 서버에 대한 RTT를 조회하려면 ServerClientId를 사용
            ulong targetClientId = NetworkManager.Singleton.IsHost
                ? NetworkManager.ServerClientId  // Host에서 서버 자신의 ID
                : NetworkManager.ServerClientId; // Client에서 서버를 향한 RTT

            ulong rttMs = transport.GetCurrentRtt(targetClientId);

            _pingText.text = $"{rttMs}ms";
        }

        // ====================================================================
        // 연결 끊김 처리
        // ====================================================================

        /// <summary>
        /// NetworkManager.OnClientDisconnectCallback 수신.
        /// - 서버: 상대방 클라이언트(자신이 아닌 ID)가 나갔을 때 → ReconnectionHandler에 위임
        /// - 클라이언트: 서버(clientId == NetworkManager.ServerClientId)가 끊겼을 때 팝업 표시
        /// </summary>
        private void OnClientDisconnected(ulong clientId)
        {
            if (_disconnectHandled) return;

            // 서버(Host)인 경우: 상대방 클라이언트 연결 끊김
            if (NetworkManager.Singleton.IsServer)
            {
                // 자기 자신의 ClientId는 무시 (NetworkManager.LocalClientId)
                if (clientId == NetworkManager.Singleton.LocalClientId)
                    return;

                // ReconnectionHandler가 처리 — 서버측 로직은 위임
                // NetworkStatusUI는 서버에서는 팝업을 띄우지 않음
                Debug.Log($"[Network] 서버: 클라이언트(ID={clientId}) 연결 끊김. ReconnectionHandler에 위임.");
                return;
            }

            // 클라이언트인 경우: 서버 연결 끊김
            // clientId가 서버 ID(0)이거나 0인 경우 서버와의 연결 해제로 판단
            Debug.Log($"[Network] 클라이언트: 서버 연결 끊김 (clientId={clientId}). 팝업 표시.");
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

            Debug.Log("[Network] NetworkStatusUI: 연결 끊김 팝업 표시.");
        }

        // ====================================================================
        // 버튼 핸들러
        // ====================================================================

        /// <summary>
        /// 연결 끊김 팝업의 "확인" 버튼 클릭 시.
        /// NetworkManager를 종료하고 지정된 씬으로 복귀.
        /// </summary>
        private void OnReturnButtonClicked()
        {
            Debug.Log("[Network] NetworkStatusUI: 로비 복귀 버튼 클릭.");

            // 시간 복원 (게임 종료 팝업과 함께 표시된 경우)
            Time.timeScale = 1f;

            // 네트워크 종료
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            // 복귀 씬 로드
            SceneManager.LoadScene(_returnSceneName);
        }
    }
}
