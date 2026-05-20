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
//     → [서버] GameEndUseCase.OnBuildingDied → GameEvents.OnGameEnd
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

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 네트워크 승패 판정 동기화 + 커스텀게임 재경기 컨트롤러.
    /// 서버에서 게임 종료를 감지하고 모든 클라이언트에 결과를 전파.
    ///
    /// [2026-05-20] 리팩토링:
    ///   - using Hexiege.Presentation 제거 (Infrastructure → Presentation 역방향 의존 제거)
    ///   - GameEndUI / RematchRequestPopup / GameUIManager SerializeField 제거
    ///   - UI 직접 호출 → GameEvents 발행으로 교체
    ///   - IForfeitService 인터페이스 구현 — InGameSettingsUI가 NetworkGameEndController를 직접 알지 않게
    /// </summary>
    public class NetworkGameEndController : NetworkBehaviour, IForfeitService
    {
        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>게임 종료 이벤트 구독 해제용 Disposable.</summary>
        private System.IDisposable _gameEndSubscription;

        /// <summary>로컬 재경기 요청 이벤트 구독 해제용 Disposable.</summary>
        private System.IDisposable _localRematchRequestedSub;

        /// <summary>로컬 재경기 수락 이벤트 구독 해제용 Disposable.</summary>
        private System.IDisposable _localRematchAcceptedSub;

        /// <summary>로컬 재경기 거절 이벤트 구독 해제용 Disposable.</summary>
        private System.IDisposable _localRematchDeclinedSub;

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
        /// 네트워크 스폰 시 서버라면 OnGameEnd를 구독하고,
        /// 양측 모두 로컬 재경기 응답 이벤트(GameEvents.OnLocalRematch*)를 구독한다.
        /// UI 컴포넌트(GameEndUI/RematchRequestPopup/GameUIManager) 탐색 제거.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            Debug.Log($"[Network] NetworkGameEndController 스폰. IsServer={IsServer}");

            if (IsServer)
            {
                // 서버: 게임 종료 이벤트 구독 → 모든 클라이언트에 결과 전파
                _gameEndSubscription = GameEvents.OnGameEnd
                    .Subscribe(OnGameEndServer);

                Debug.Log("[Network] NetworkGameEndController: 서버 측 OnGameEnd 구독 완료.");
            }

            // 로컬 측 재경기 응답 이벤트 구독 — UI가 이벤트를 발행하면 본 컨트롤러가
            // 적절한 ServerRpc로 변환하여 서버에 전달한다.
            //
            // 호스트(IsServer)도 자신의 로컬 UI 발행 신호를 ServerRpc로 그대로 보낸다.
            // (ServerRpc는 호스트에서 즉시 실행되므로 함수 호출과 동일하게 동작)
            _localRematchRequestedSub = GameEvents.OnLocalRematchRequested
                .Subscribe(_ => RequestRematchServerRpc());

            _localRematchAcceptedSub = GameEvents.OnLocalRematchAccepted
                .Subscribe(_ => AcceptRematchServerRpc());

            _localRematchDeclinedSub = GameEvents.OnLocalRematchDeclined
                .Subscribe(_ => DeclineRematchServerRpc());
        }

        /// <summary>
        /// 네트워크 디스폰 시 구독 해제 및 재경기 상태 초기화.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _gameEndSubscription?.Dispose();
            _gameEndSubscription = null;

            _localRematchRequestedSub?.Dispose();
            _localRematchRequestedSub = null;
            _localRematchAcceptedSub?.Dispose();
            _localRematchAcceptedSub = null;
            _localRematchDeclinedSub?.Dispose();
            _localRematchDeclinedSub = null;

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
        ///
        /// [2026-05-20] 리팩토링:
        ///   기존: GameEndUI/GameUIManager를 직접 참조해 메서드 호출.
        ///   변경: GameEvents 이벤트 발행으로 교체. UI 컴포넌트는 각자 이벤트를 구독해 반응한다.
        ///     - OnGameEnd: GameEndUI(자체 구독)가 ShowResult/일시정지 처리,
        ///                  GameUIManager(자체 구독)가 열린 팝업을 닫음.
        ///     - OnNetworkRematchAvailable: GameEndUI(자체 구독)가 재경기 버튼 활성화.
        ///
        /// 호스트(서버)에서는 OnGameEnd가 이미 OnGameEndServer 호출 직전에 한 번 발행되었으므로,
        /// 본 핸들러에서는 클라이언트 측에서만 OnGameEnd를 재발행하여 동일한 UI 흐름이 작동하게 한다.
        /// </summary>
        /// <param name="winnerTeamIndex">승리한 팀의 TeamId 정수값 (Blue=1, Red=2)</param>
        /// <param name="isRandomMatch">랜덤 매칭 여부. true이면 재경기 버튼 숨김.</param>
        [ClientRpc]
        private void AnnounceWinnerClientRpc(int winnerTeamIndex, bool isRandomMatch)
        {
            TeamId winnerTeam = (TeamId)winnerTeamIndex;

            Debug.Log($"[Network] AnnounceWinnerClientRpc 수신. 승리 팀={winnerTeam}, 로컬 팀={LocalPlayerTeam.Current}, 랜덤매칭={isRandomMatch}");

            // 클라이언트(비서버)에서는 OnGameEnd가 발행되지 않았으므로 여기서 발행한다.
            // (서버에서는 OnGameEndServer 호출 직전 이미 OnGameEnd가 발행된 상태)
            // GameEndUI가 OnGameEnd를 구독해 ShowResult 등 표시/일시정지 처리.
            // GameUIManager는 OnGameEnd를 구독해 NotifyGameEnded()로 열린 팝업을 닫음.
            if (!IsServer)
            {
                GameEvents.OnGameEnd.OnNext(new GameEndEvent(winnerTeam));
            }

            // 재경기 버튼 설정 신호 — GameEndUI가 구독해 SetupRematchButton 호출.
            GameEvents.OnNetworkRematchAvailable.OnNext(new NetworkRematchAvailableEvent(isRandomMatch));
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
        // 포기 (Forfeit) — 인게임 설정 메뉴에서 호출
        // ====================================================================

        /// <summary>
        /// 포기 요청. 로컬에서 InGameSettingsUI가 사용자에게 확인 후 호출.
        /// 내부적으로 ServerRpc를 전송하여 서버에서 자기 팀을 패배 처리한다.
        /// </summary>
        public void RequestForfeit()
        {
            Debug.Log("[Network] 포기 요청 전송.");
            ForfeitServerRpc();
        }

        /// <summary>
        /// 클라이언트의 포기 요청을 서버에서 처리.
        /// 포기자의 ClientId로 팀을 식별하고(Host=0=Blue, Client=Red), 반대 팀을 승리자로 전체 클라이언트에 발표.
        /// 이미 결과가 발표된 상태(_announced==true)라면 무시 — Castle 파괴 직후 포기 클릭 등의 경쟁 상황 방어.
        ///
        /// RequireOwnership=false: 이 NetworkObject는 서버 소유이므로
        /// 클라이언트가 호출할 수 있도록 명시적으로 허용해야 한다.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void ForfeitServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (_announced) return;

            ulong forfeiterId = rpcParams.Receive.SenderClientId;

            // ClientId → TeamId 매핑.
            //   Host(서버 호스트) = ClientId 0 = Blue 팀
            //   Client(접속자)     = Red 팀
            // 이 매핑은 NetworkGameManager에서도 동일하게 사용 중인 규약.
            TeamId forfeitTeam = (forfeiterId == 0) ? TeamId.Blue : TeamId.Red;
            TeamId winnerTeam = (forfeitTeam == TeamId.Blue) ? TeamId.Red : TeamId.Blue;

            _announced = true;
            Debug.Log($"[Network] 포기 처리. 포기자ClientId={forfeiterId}, " +
                      $"포기팀={forfeitTeam}, 승리팀={winnerTeam}");

            // 포기에 의한 정상 종료 — 재경기 신청은 가능하도록 isRandomMatch=false 전달.
            // (랜덤 매칭이라도 isRandomMatch는 NetworkGameManager에서 별도로 판단되지만,
            //  여기서는 보수적으로 false를 넣어 클라이언트가 재경기 버튼을 띄울 수 있게 함.
            //  실제 랜덤매칭 종료 처리는 기존 OnGameEndServer 경로와 동일하게 동작.)
            AnnounceWinnerClientRpc((int)winnerTeam, false);
        }

        // ====================================================================
        // 재경기 (Rematch) — 커스텀게임 전용
        // ====================================================================

        // [2026-05-20] 리팩토링: 재경기 콜백(RequestRematch / OnAcceptRematch / OnDeclineRematch)
        // 메서드는 제거되었다. 이전에는 NetworkGameEndController가 GameEndUI/RematchRequestPopup에
        // 콜백을 직접 등록했으나, 이제는 UI가 GameEvents.OnLocalRematchRequested 등을 발행하고
        // 본 컨트롤러는 OnNetworkSpawn에서 해당 이벤트를 구독한다. 구독 핸들러에서 ServerRpc를 호출.

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
        ///
        /// [2026-05-20] 리팩토링:
        ///   기존: RematchRequestPopup을 직접 ShowRequest(콜백) 호출.
        ///   변경: GameEvents.OnNetworkRematchRequested 발행. 팝업은 자체 구독으로 ShowRequest 호출.
        ///         수락/거절은 OnLocalRematchAccepted / OnLocalRematchDeclined 이벤트로 다시 본 컨트롤러에 전달됨.
        /// </summary>
        [ClientRpc]
        private void NotifyRematchRequestedClientRpc(ClientRpcParams clientRpcParams = default)
        {
            Debug.Log("[Network] 재경기 요청 수신. OnNetworkRematchRequested 발행.");
            GameEvents.OnNetworkRematchRequested.OnNext(new NetworkRematchRequestedEvent());
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
        ///
        /// [2026-05-20] 리팩토링:
        ///   기존: RematchRequestPopup.ShowDeclined() + GameEndUI.RestoreRematchButton() 직접 호출.
        ///   변경: GameEvents.OnNetworkRematchDeclined 발행. 두 UI 모두 자체 구독으로 처리.
        /// </summary>
        [ClientRpc]
        private void NotifyRematchDeclinedClientRpc(ClientRpcParams clientRpcParams = default)
        {
            Debug.Log("[Network] 재경기 거절 알림 수신. OnNetworkRematchDeclined 발행.");
            GameEvents.OnNetworkRematchDeclined.OnNext(Unit.Default);
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
