// ============================================================================
// ReconnectionHandler.cs
// 서버 전용: 상대방 연결 끊김 감지 → 재접속 대기 → 강제 승리 판정.
//
// 역할:
//   - 서버에서 OnClientDisconnectCallback 구독
//   - 상대방(Host가 아닌 클라이언트)이 나갔을 때 _reconnectWaitSeconds 대기
//   - 대기 중 재접속이 없으면 NetworkGameEndController.ForceWin() 호출
//   - 재접속(OnClientConnectedCallback) 발생 시 코루틴 취소
//
// 설계:
//   - 서버만 실행 (OnNetworkSpawn에서 IsServer 체크)
//   - NetworkBehaviour → Infrastructure 레이어에 배치
//   - NetworkGameEndController는 FindFirstObjectByType으로 탐색
//     (씬에 반드시 존재해야 ForceWin 작동)
//
// 배치:
//   씬에 빈 GameObject "ReconnectionHandler" 배치.
//   NetworkObject 컴포넌트 + 이 스크립트 부착.
//   Host 시작 시 자동 스폰.
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 서버 전용 재접속 대기 + 강제 승리 판정 핸들러.
    /// 상대방이 연결을 끊으면 일정 시간 대기 후 남은 팀을 승리 처리.
    /// </summary>
    public class ReconnectionHandler : NetworkBehaviour
    {
        // ====================================================================
        // Inspector 설정
        // ====================================================================

        [Header("재접속 설정")]
        [Tooltip("재접속을 허용할 최대 대기 시간 (초). 이 시간이 지나면 강제 승리 처리.")]
        [SerializeField] private float _reconnectWaitSeconds = 30f;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>재접속 대기 코루틴 참조. 재접속 시 취소에 사용.</summary>
        private Coroutine _reconnectCoroutine;

        /// <summary>연결이 끊긴 클라이언트의 ID. 재접속 확인 시 동일 ID 여부 검사.</summary>
        private ulong _disconnectedClientId;

        /// <summary>이미 ForceWin을 호출했는지 여부. 중복 실행 방지.</summary>
        private bool _forceWinTriggered;

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 서버에서만 콜백 등록.
        /// 클라이언트에서는 아무 동작도 하지 않음.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer)
            {
                // 클라이언트: 이 컴포넌트는 서버 전용 — 비활성화
                enabled = false;
                return;
            }

            // 서버: 연결/연결 끊김 콜백 등록
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.OnClientConnectedCallback += OnClientReconnected;

            Debug.Log("[Network] ReconnectionHandler: 서버 모드로 재접속 감시 시작.");
        }

        /// <summary>
        /// 디스폰 시 콜백 해제 및 코루틴 정리.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (!IsServer) return;

            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.OnClientConnectedCallback -= OnClientReconnected;

            if (_reconnectCoroutine != null)
            {
                StopCoroutine(_reconnectCoroutine);
                _reconnectCoroutine = null;
            }

            Debug.Log("[Network] ReconnectionHandler: 디스폰. 콜백 해제 완료.");
        }

        // ====================================================================
        // 연결 끊김 처리
        // ====================================================================

        /// <summary>
        /// 클라이언트 연결 끊김 수신.
        /// Host(자신)의 ClientId가 아닌 경우만 처리 (상대방이 나간 경우).
        /// _reconnectWaitSeconds 동안 재접속 대기 코루틴 시작.
        /// </summary>
        private void OnClientDisconnected(ulong clientId)
        {
            // Host 자신이 나간 경우는 무시 (Host는 LocalClientId == 0 이지만
            // 실제로 disconnect 이벤트는 상대방에게만 발생)
            if (clientId == NetworkManager.LocalClientId)
                return;

            // 이미 ForceWin 처리됐으면 무시
            if (_forceWinTriggered)
                return;

            // 이미 대기 중이면 이전 코루틴 취소 후 새로 시작
            if (_reconnectCoroutine != null)
            {
                StopCoroutine(_reconnectCoroutine);
                _reconnectCoroutine = null;
            }

            _disconnectedClientId = clientId;

            Debug.Log($"[Network] ReconnectionHandler: 클라이언트(ID={clientId}) 연결 끊김. " +
                      $"{_reconnectWaitSeconds}초 재접속 대기 시작.");

            _reconnectCoroutine = StartCoroutine(WaitAndForceWin());
        }

        /// <summary>
        /// 클라이언트 재접속 수신.
        /// 대기 중인 ForceWin 코루틴을 취소.
        /// </summary>
        private void OnClientReconnected(ulong clientId)
        {
            // 연결 끊긴 클라이언트가 재접속했는지 확인
            if (clientId != _disconnectedClientId)
                return;

            if (_reconnectCoroutine != null)
            {
                StopCoroutine(_reconnectCoroutine);
                _reconnectCoroutine = null;
                Debug.Log($"[Network] ReconnectionHandler: 클라이언트(ID={clientId}) 재접속 확인. " +
                          "ForceWin 코루틴 취소.");
            }
        }

        // ====================================================================
        // 강제 승리 대기 코루틴
        // ====================================================================

        /// <summary>
        /// _reconnectWaitSeconds 동안 대기 후 남은 팀(Host 팀 = Blue)을 강제 승리 처리.
        /// 대기 중 OnClientReconnected가 호출되면 이 코루틴은 외부에서 StopCoroutine으로 중단.
        /// </summary>
        private IEnumerator WaitAndForceWin()
        {
            Debug.Log($"[Network] ReconnectionHandler: {_reconnectWaitSeconds}초 후 강제 승리 처리.");
            yield return new WaitForSeconds(_reconnectWaitSeconds);

            if (_forceWinTriggered)
                yield break;

            _forceWinTriggered = true;
            _reconnectCoroutine = null;

            // 서버(Host)는 항상 Blue 팀 → 상대방이 나갔으므로 Blue 팀 승리
            // 단, LocalPlayerTeam.Current로 서버 팀을 재확인
            int winnerTeamIndex = (int)LocalPlayerTeam.Current;

            Debug.Log($"[Network] ReconnectionHandler: 재접속 타임아웃. " +
                      $"강제 승리 처리. 승리 팀 index={winnerTeamIndex}");

            // NetworkGameEndController를 통해 모든 클라이언트에 결과 전파
            NetworkGameEndController endController =
                FindFirstObjectByType<NetworkGameEndController>();

            if (endController == null)
            {
                Debug.LogError("[Network] ReconnectionHandler: NetworkGameEndController를 찾을 수 없습니다. " +
                               "ForceWin 처리 실패.");
                yield break;
            }

            endController.ForceWin(winnerTeamIndex);
        }
    }
}
