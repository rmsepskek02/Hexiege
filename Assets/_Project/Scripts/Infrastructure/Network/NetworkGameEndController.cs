// ============================================================================
// NetworkGameEndController.cs
// 네트워크 모드에서 승패 결과를 서버에서 결정하여 모든 클라이언트에 동기화.
// 커스텀게임 재경기(Rematch) 요청/수락/거절 RPC 처리.
//
// 역할:
//   - 서버: OnGameEnd 이벤트 구독 → AnnounceWinnerClientRpc로 전체 전파
//   - 클라이언트: AnnounceWinnerClientRpc 수신 → GameEndUI 표시 (팀 보정 포함)
//   - 재경기: 양측 동의 시 서버가 Game 씬 재로드 (NGO SceneManager)
//
// 흐름:
//   [서버] Castle 파괴
//     → NetworkCombatController.EntityDiedClientRpc
//     → [서버] GameEndUseCase.OnEntityDied → GameEvents.OnGameEnd
//     → [서버] NetworkGameEndController.OnGameEnd 수신
//     → AnnounceWinnerClientRpc(winnerTeamIndex, isRandomMatch)
//     → [모든 클라이언트] SetupRematchButton + ShowResult
//
// 재경기 흐름 (커스텀게임):
//   [요청자] RequestRematch() → RequestRematchServerRpc
//     → [서버] 첫 요청 기록 → NotifyRematchRequestedClientRpc (상대에게만)
//   [상대] 수락 → AcceptRematchServerRpc → StartRematch()
//   [상대] 거절 → DeclineRematchServerRpc → NotifyRematchDeclinedClientRpc (요청자에게만)
//   [양측 동시 요청] 두 번째 ServerRpc에서 바로 StartRematch()
//
// 싱글플레이와의 관계:
//   싱글플레이 시 이 컴포넌트는 씬에 없거나 NetworkObject가 스폰되지 않으므로
//   기존 GameEndUseCase → GameEndUI 직접 구독 흐름이 그대로 작동.
//
// 배치:
//   씬에 빈 GameObject "NetworkGameEndController" 배치.
//   NetworkObject 컴포넌트 + 이 스크립트 부착.
//   NetworkManager의 씬 오브젝트로 자동 스폰.
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;
using Hexiege.Presentation;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 네트워크 승패 판정 동기화 + 커스텀게임 재경기 컨트롤러.
    /// 서버에서 게임 종료를 감지하고 모든 클라이언트에 결과를 전파.
    /// </summary>
    public class NetworkGameEndController : NetworkBehaviour
    {
        // ====================================================================
        // Inspector 설정
        // ====================================================================

        [Header("씬 연결")]
        [Tooltip("게임 종료 UI 컴포넌트. GameBootstrapper에서 자동 주입 가능.")]
        [SerializeField] private GameEndUI _gameEndUI;

        [Tooltip("재경기 요청 팝업. Inspector 미연결 시 자동 탐색.")]
        [SerializeField] private RematchRequestPopup _rematchRequestPopup;

        [Tooltip("게임 UI 매니저. 게임 종료 시 클라이언트 측 열린 팝업(생산 패널, 건물 배치 등)을 일괄 닫기 위해 사용. Inspector 미연결 시 자동 탐색.")]
        [SerializeField] private GameUIManager _uiManager;

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>게임 종료 이벤트 구독 해제용 Disposable.</summary>
        private System.IDisposable _gameEndSubscription;

        /// <summary>결과 발표 여부. 서버에서 중복 전파 방지용.</summary>
        private bool _announced = false;

        /// <summary>
        /// 재경기를 먼저 요청한 클라이언트 ID.
        /// ulong.MaxValue이면 아직 요청 없음.
        /// </summary>
        private ulong _rematchRequesterId = ulong.MaxValue;

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 GameEndUI, RematchRequestPopup을 탐색하고,
        /// 서버라면 OnGameEnd를 구독하여 승패 발표 준비.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // GameEndUI가 Inspector에 연결되지 않은 경우 씬에서 탐색
            if (_gameEndUI == null)
                _gameEndUI = FindFirstObjectByType<GameEndUI>();

            if (_gameEndUI == null)
                Debug.LogWarning("[Network] NetworkGameEndController: GameEndUI를 찾을 수 없습니다.");

            // RematchRequestPopup 자동 탐색 (비활성 오브젝트 포함)
            if (_rematchRequestPopup == null)
                _rematchRequestPopup = FindFirstObjectByType<RematchRequestPopup>(FindObjectsInactive.Include);

            // GameUIManager 탐색 — 게임 종료 시 클라이언트 측 열린 팝업을 닫기 위해 필요
            // (Inspector 미연결 시 자동 탐색)
            if (_uiManager == null)
                _uiManager = FindFirstObjectByType<GameUIManager>();

            Debug.Log($"[Network] NetworkGameEndController 스폰. IsServer={IsServer}");

            if (IsServer)
            {
                // 서버: 게임 종료 이벤트 구독 → 모든 클라이언트에 결과 전파
                _gameEndSubscription = GameEvents.OnGameEnd
                    .Subscribe(OnGameEndServer);

                Debug.Log("[Network] NetworkGameEndController: 서버 측 OnGameEnd 구독 완료.");
            }
        }

        /// <summary>
        /// 네트워크 디스폰 시 구독 해제 및 재경기 상태 초기화.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _gameEndSubscription?.Dispose();
            _gameEndSubscription = null;
            _rematchRequesterId = ulong.MaxValue;
        }

        // ====================================================================
        // 이벤트 핸들러 (서버 전용)
        // ====================================================================

        /// <summary>
        /// 서버에서 GameEndEvent를 수신하여 모든 클라이언트에 승자를 전파.
        /// NetworkGameManager.IsRandomMatchmaking을 읽어 게임 모드 정보도 함께 전달.
        /// </summary>
        private void OnGameEndServer(GameEndEvent e)
        {
            if (!IsServer) return;
            if (_announced) return;

            _announced = true;
            int winnerTeamIndex = (int)e.Winner;

            // 랜덤 매칭 여부 확인 — 재경기 버튼 분기에 사용
            bool isRandomMatch = false;
            var ngm = FindFirstObjectByType<NetworkGameManager>();
            if (ngm != null)
                isRandomMatch = ngm.IsRandomMatchmaking;

            Debug.Log($"[Network] 서버: 게임 종료 감지. 승리 팀={e.Winner} (index={winnerTeamIndex}), 랜덤매칭={isRandomMatch}. 결과 전파 시작.");

            // 모든 클라이언트에 승자 발표
            AnnounceWinnerClientRpc(winnerTeamIndex, isRandomMatch);
        }

        // ====================================================================
        // ClientRpc — 서버 → 모든 클라이언트
        // ====================================================================

        /// <summary>
        /// 서버에서 확정된 승리 팀 인덱스를 모든 클라이언트에 전송.
        /// 게임 모드에 따라 재경기 버튼 동작을 분기 설정.
        /// </summary>
        /// <param name="winnerTeamIndex">승리한 팀의 TeamId 정수값 (Blue=1, Red=2)</param>
        /// <param name="isRandomMatch">랜덤 매칭 여부. true이면 재경기 버튼 숨김.</param>
        [ClientRpc]
        private void AnnounceWinnerClientRpc(int winnerTeamIndex, bool isRandomMatch)
        {
            TeamId winnerTeam = (TeamId)winnerTeamIndex;

            Debug.Log($"[Network] AnnounceWinnerClientRpc 수신. 승리 팀={winnerTeam}, 로컬 팀={LocalPlayerTeam.Current}, 랜덤매칭={isRandomMatch}");

            if (_gameEndUI == null)
            {
                _gameEndUI = FindFirstObjectByType<GameEndUI>();
                if (_gameEndUI == null)
                {
                    Debug.LogError("[Network] AnnounceWinnerClientRpc: GameEndUI를 찾을 수 없습니다.");
                    return;
                }
            }

            // 클라이언트에서는 GameEvents.OnGameEnd가 발행되지 않으므로
            // GameUIManager에 직접 게임 종료를 알려 열린 팝업들(생산 패널, 건물 배치 등)을 닫는다.
            // Host에서는 이미 OnGameEnd 구독으로 1회 호출됐으나, 중복 호출해도 안전
            // (이미 닫힌 UI에 OnGameEnded()를 다시 호출해도 부작용 없음).
            _uiManager?.NotifyGameEnded();

            // 게임 모드에 따라 재경기 버튼 설정
            _gameEndUI.SetupRematchButton(isRandomMatch, RequestRematch);

            // 로컬 팀 기준으로 승리/패배 결정하여 UI 표시
            _gameEndUI.ShowResult(winnerTeam, LocalPlayerTeam.Current);
        }

        // ====================================================================
        // 강제 승리 — 상대방 연결 끊김 시 서버에서 호출
        // ====================================================================

        /// <summary>
        /// 서버에서 상대방 연결 끊김으로 인해 남은 팀을 강제 승리 처리.
        /// ReconnectionHandler.OnClientDisconnected() 대기 타임아웃 후 호출.
        /// 연결 끊김 상황이므로 재경기 불가 — isRandomMatch=false로 전달하되
        /// 상대 부재로 재경기 요청 자체가 성립하지 않음.
        /// </summary>
        /// <param name="winnerTeamIndex">강제 승리할 팀의 TeamId 정수값 (Blue=1, Red=2)</param>
        public void ForceWin(int winnerTeamIndex)
        {
            if (!IsServer) return;
            if (_announced) return;

            _announced = true;
            Debug.Log($"[Network] ForceWin 호출. 강제 승리 팀 index={winnerTeamIndex}");

            // 연결 끊김 시 재경기 불가 — isRandomMatch=false
            AnnounceWinnerClientRpc(winnerTeamIndex, false);
        }

        // ====================================================================
        // 재경기 (Rematch) — 커스텀게임 전용
        // ====================================================================

        /// <summary>
        /// 재경기 요청. GameEndUI의 다시하기 버튼 콜백으로 연결됨.
        /// </summary>
        private void RequestRematch()
        {
            Debug.Log("[Network] 재경기 요청 전송.");
            RequestRematchServerRpc();
        }

        /// <summary>
        /// 클라이언트의 재경기 요청을 서버에서 처리.
        /// 첫 요청 시 상대에게 알림, 양측 모두 요청 시 즉시 재경기 시작.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void RequestRematchServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong requesterId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[Network] RequestRematchServerRpc 수신. 요청자={requesterId}, 기존 요청자={_rematchRequesterId}");

            if (_rematchRequesterId == ulong.MaxValue)
            {
                // 첫 번째 요청 — 기록 후 상대에게 알림
                _rematchRequesterId = requesterId;
                ulong otherClientId = GetOtherClientId(requesterId);

                if (otherClientId != ulong.MaxValue)
                {
                    var clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new[] { otherClientId }
                        }
                    };
                    NotifyRematchRequestedClientRpc(clientRpcParams);
                }
            }
            else
            {
                // 상대도 이미 요청 → 상호 동의 — 즉시 재경기
                Debug.Log("[Network] 양측 재경기 동의. 즉시 재경기 시작.");
                StartRematch();
            }
        }

        /// <summary>
        /// 상대 클라이언트에게 재경기 요청이 들어왔음을 알림.
        /// 팝업으로 수락/거절 선택지 표시.
        /// </summary>
        [ClientRpc]
        private void NotifyRematchRequestedClientRpc(ClientRpcParams clientRpcParams = default)
        {
            Debug.Log("[Network] 재경기 요청 수신. 팝업 표시.");
            // 비활성 상태일 수 있으므로 null이면 재탐색
            if (_rematchRequestPopup == null)
                _rematchRequestPopup = FindFirstObjectByType<RematchRequestPopup>(FindObjectsInactive.Include);
            if (_rematchRequestPopup != null)
                _rematchRequestPopup.ShowRequest(OnAcceptRematch, OnDeclineRematch);
        }

        /// <summary>재경기 수락 콜백.</summary>
        private void OnAcceptRematch()
        {
            Debug.Log("[Network] 재경기 수락.");
            AcceptRematchServerRpc();
        }

        /// <summary>
        /// 재경기 수락을 서버에서 처리. 즉시 재경기 시작.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void AcceptRematchServerRpc()
        {
            Debug.Log("[Network] AcceptRematchServerRpc 수신. 재경기 시작.");
            StartRematch();
        }

        /// <summary>재경기 거절 콜백.</summary>
        private void OnDeclineRematch()
        {
            Debug.Log("[Network] 재경기 거절.");
            DeclineRematchServerRpc();
        }

        /// <summary>
        /// 재경기 거절을 서버에서 처리.
        /// 요청자에게 거절 알림 전송 + 요청 상태 초기화.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void DeclineRematchServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong declinerId = rpcParams.Receive.SenderClientId;
            Debug.Log($"[Network] DeclineRematchServerRpc 수신. 거절자={declinerId}");

            // 요청 상태 초기화
            _rematchRequesterId = ulong.MaxValue;

            // 요청자에게 거절 알림
            ulong requesterId = GetOtherClientId(declinerId);
            if (requesterId != ulong.MaxValue)
            {
                var clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { requesterId }
                    }
                };
                NotifyRematchDeclinedClientRpc(clientRpcParams);
            }
        }

        /// <summary>
        /// 요청자에게 재경기 거절을 알림. 버튼 상태 복원.
        /// </summary>
        [ClientRpc]
        private void NotifyRematchDeclinedClientRpc(ClientRpcParams clientRpcParams = default)
        {
            Debug.Log("[Network] 재경기 거절 알림 수신.");

            if (_rematchRequestPopup != null)
                _rematchRequestPopup.ShowDeclined();

            if (_gameEndUI != null)
                _gameEndUI.RestoreRematchButton();
        }

        /// <summary>
        /// 서버에서 Game 씬을 재로드하여 재경기 시작.
        /// NGO SceneManager가 모든 클라이언트에 씬 전환을 동기화.
        /// </summary>
        private void StartRematch()
        {
            _rematchRequesterId = ulong.MaxValue;

            // ----------------------------------------------------------------
            // LoadScene 이전에 모든 동적 스폰 NetworkObject를 명시적으로 Despawn.
            // NGO SceneManager.LoadScene(Single)으로 같은 씬("Game")을 재로드할 때
            // 동적 스폰 NetworkObject(유닛, 건물 등)가 자동 정리되지 않는 문제 수정.
            // ----------------------------------------------------------------
            // SpawnedObjects Dictionary를 직접 순회하면서 Despawn하면
            // 컬렉션이 변경되어 InvalidOperationException이 발생하므로,
            // 먼저 List에 복사한 뒤 복사본을 순회한다.
            // ----------------------------------------------------------------
            var spawnedCopy = new System.Collections.Generic.List<NetworkObject>(
                NetworkManager.SpawnManager.SpawnedObjects.Values);

            foreach (var netObj in spawnedCopy)
            {
                // IsSceneObject == true인 오브젝트는 씬에 미리 배치된 NetworkObject
                // (예: NetworkGameFlow, NetworkCombatController 등).
                // 이들은 씬 재로드 시 NGO가 자동으로 처리하므로 건드리지 않는다.
                //
                // null 체크: Despawn 과정에서 다른 오브젝트가 연쇄 파괴될 수 있으므로
                // Unity 오브젝트 유효성을 먼저 확인한다.
                // IsSpawned 체크: 이미 Despawn된 오브젝트를 중복 처리하지 않도록 한다.
                if (netObj != null && netObj.IsSpawned == true && netObj.IsSceneObject == false)
                {
                    Debug.Log($"[Network] StartRematch: 동적 NetworkObject Despawn. name={netObj.name}");
                    netObj.Despawn();
                }
            }

            Debug.Log("[Network] StartRematch: Game 씬 재로드.");
            NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
        }

        // ====================================================================
        // 유틸리티
        // ====================================================================

        /// <summary>
        /// 현재 연결된 클라이언트 중 자신이 아닌 상대의 ClientId를 반환.
        /// 2인 게임 전용. 상대를 찾을 수 없으면 ulong.MaxValue 반환.
        /// </summary>
        private ulong GetOtherClientId(ulong myClientId)
        {
            foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (id != myClientId)
                    return id;
            }
            return ulong.MaxValue;
        }
    }
}
