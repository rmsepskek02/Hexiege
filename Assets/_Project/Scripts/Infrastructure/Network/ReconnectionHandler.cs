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
//   - NetworkGameEndController는 OnNetworkSpawn에서 1회 캐시하여 재사용
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
using Hexiege.Application; // GameLog (로그 facade). ⚠️ Hexiege.Application 네임스페이스가 있으므로
                           //    수식 없는 Application 은 UnityEngine.Application 이 아니다.
                           //    이 파일에는 UnityEngine.Application 사용처가 없어 충돌하지 않는다.

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

        /// <summary>
        /// NetworkGameEndController 참조.
        /// 서버 측 OnNetworkSpawn에서 1회만 탐색하여 캐시하고 이후에는 재탐색 없이 사용한다.
        /// WaitAndForceWin 코루틴에서 씬 전체 탐색을 반복하지 않기 위한 캐시.
        /// </summary>
        private NetworkGameEndController _networkGameEndController;

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

            // 서버 측 OnNetworkSpawn에서 1회만 탐색하여 캐시.
            // WaitAndForceWin 코루틴에서 매번 탐색하지 않도록 미리 보관.
            _networkGameEndController = FindFirstObjectByType<NetworkGameEndController>();

            // 서버: 연결/연결 끊김 콜백 등록
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.OnClientConnectedCallback += OnClientReconnected;

            // [개발] 축 A=Info(의도된 흐름) / 축 B=개발.
            //   축 B ① "플레이어 기기에서만 벌어지는가"가 "아니오"다 — 에디터 2인 구성으로 그대로 재현된다.
            //   (선례: LogAudit.md §3-8 NetworkGameManager:692 "Client 접속 감지" → Info / 개발)
            GameLog.Dev.Info("Network", nameof(ReconnectionHandler), "서버 모드로 재접속 감시 시작",
                             $"WaitSeconds={_reconnectWaitSeconds}");
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

            // [개발] 스폰 로그와 짝을 이루는 의도된 흐름 → Info / 개발.
            GameLog.Dev.Info("Network", nameof(ReconnectionHandler), "디스폰 — 콜백 해제 완료");
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

            // [개발] 축 A=Warn / 축 B=개발 — 선행 확정 판정을 그대로 잇는다
            //   (LogAudit.md §4-3 Q-1, NetworkGameManager:128 "서버 연결 끊김 감지" → Warn / 개발 확정).
            //   축 A 가 Warn 인 이유: 이 콜백은 "상대가 스스로 나간 경우"와 "회선이 끊긴 장애"에
            //   똑같이 호출되어 코드가 둘을 구분하지 못한다. Error 로 두면 정상 종료마다 거짓 경보가 쌓이고
            //   Info 로 두면 진짜 장애가 묻히므로 그 중간값인 Warn 을 쓴다.
            //   축 B 가 개발인 이유: 에디터 2인 구성에서 클라이언트를 종료시키면 그대로 재현된다(축 B ① 아니오).
            GameLog.Dev.Warn("Network", nameof(ReconnectionHandler), "클라이언트 연결 끊김 — 재접속 대기 시작",
                             $"ClientId={clientId}, WaitSeconds={_reconnectWaitSeconds}");

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
                // [개발] 재접속으로 복구된 의도된 흐름 → Info / 개발.
                GameLog.Dev.Info("Network", nameof(ReconnectionHandler), "클라이언트 재접속 확인 — 강제 승리 대기 취소",
                                 $"ClientId={clientId}");
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
            // [개발] 코루틴 진입 흔적 → Info / 개발.
            // ⚠️ 이 줄은 바로 위 OnClientDisconnected 의 "재접속 대기 시작" 로그와 같은 사건을 한 번 더 남긴다
            //    (LogRules 1.14 금지 9). 다만 양쪽 모두 개발이라 서버 집계에 올라가지 않아
            //    "발생 건수가 부풀려진다"는 실제 피해는 생기지 않는다.
            //    한쪽 제거는 기존 로직 제거라 승인 대상이므로 이번 배치에서는 손대지 않고 보고만 한다.
            GameLog.Dev.Info("Network", nameof(ReconnectionHandler), "강제 승리 대기 코루틴 시작",
                             $"WaitSeconds={_reconnectWaitSeconds}");
            yield return new WaitForSeconds(_reconnectWaitSeconds);

            if (_forceWinTriggered)
                yield break;

            _forceWinTriggered = true;
            _reconnectCoroutine = null;

            // 서버(Host)는 항상 Blue 팀 → 상대방이 나갔으므로 Blue 팀 승리
            // 단, LocalPlayerTeam.Current로 서버 팀을 재확인
            int winnerTeamIndex = (int)LocalPlayerTeam.Current;

            // [개발] 축 A=Info / 축 B=개발.
            //   축 A: 재접속 타임아웃 후 남은 팀을 승리 처리하는 것은 이 기능이 설계대로 동작한 결과다
            //         — "복구되었나?"라는 축 A 질문 자체가 성립하지 않는 정상 흐름이다.
            //   축 B: 에디터 2인 구성에서 클라이언트를 종료한 뒤 대기하면 그대로 재현된다(축 B ① 아니오).
            //         결과는 게임 종료 화면으로 플레이어에게 그대로 표시된다(축 B ② 아니오).
            GameLog.Dev.Info("Network", nameof(ReconnectionHandler), "재접속 타임아웃 — 강제 승리 처리",
                             $"WinnerTeam={(TeamId)winnerTeamIndex}");

            // NetworkGameEndController를 통해 모든 클라이언트에 결과 전파.
            // OnNetworkSpawn에서 미리 캐시해 둔 참조를 사용 (매번 씬 탐색하지 않음).
            NetworkGameEndController endController = _networkGameEndController;

            if (endController == null)
            {
                // [개발] 축 A=Warn / 축 B=개발 — LogRules 1.3 원칙 3 「단서」 적용(설정 오류).
                //   원칙 3 은 "Error 는 항상 운영"이지만, Inspector·씬 배선 누락 같은 설정 오류는
                //   Warn + 개발로 낮춘다고 단서를 두고 있다.
                //   이 자리가 그 단서에 해당하는 근거:
                //     NetworkGameEndController 는 Game 씬에 직접 배치되는 오브젝트이고(그 파일 헤더 「배치」 절),
                //     참조는 FindFirstObjectByType 으로 얻는다. FindFirstObjectByType 은 네트워크 스폰 여부와
                //     무관하게 씬에 있는 활성 오브젝트를 찾으므로, 여기서 null 이라는 것은
                //     "스폰 순서가 어긋났다"가 아니라 "씬에 그 오브젝트가 아예 없다"는 뜻이다.
                //     → 플레이어 기기에서만 벌어지는 일이 아니라 빌드 전에 잡히는 개발 환경 문제다(축 B ① 아니오).
                GameLog.Dev.Warn("Network", nameof(ReconnectionHandler),
                                 "NetworkGameEndController 를 찾지 못해 강제 승리 처리를 실행할 수 없다 — 씬 배치 확인 필요",
                                 $"WinnerTeam={(TeamId)winnerTeamIndex}");
                yield break;
            }

            endController.ForceWin(winnerTeamIndex);
        }
    }
}
