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
    /// UI는 직접 호출하지 않고 GameEvents 발행으로 처리한다(Infrastructure → Presentation 역방향 의존 회피).
    /// IForfeitService를 구현하여 InGameSettingsUI가 이 컨트롤러를 직접 알지 않아도 포기 요청을 전달할 수 있다.
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

        /// <summary>
        /// NetworkGameManager 참조.
        /// 서버 측 OnNetworkSpawn에서 1회만 탐색하여 캐시하고 이후에는 재탐색 없이 사용한다.
        /// OnGameEndServer에서 씬 전체 탐색을 반복하지 않기 위한 캐시.
        /// </summary>
        private Hexiege.Infrastructure.NetworkGameManager _networkGameManager;

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

            // [로그 이관] LogAudit.md §3-1 — 축 A=Info / 축 B=개발.
            // "[Network]" 접두사와 클래스 이름을 메시지에서 뺀 이유:
            // GameLog 가 [System/Class] 카테고리를 앞에 붙여 주므로 중복이다(LogRules.md 1.4 형식).
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController), "네트워크 스폰", $"IsServer={IsServer}");

            if (IsServer)
            {
                // 서버 측 OnNetworkSpawn에서 1회만 탐색하여 캐시.
                // OnGameEndServer 핸들러에서 매번 탐색하지 않도록 미리 보관.
                _networkGameManager = FindFirstObjectByType<Hexiege.Infrastructure.NetworkGameManager>();

                // 서버: 게임 종료 이벤트 구독 → 모든 클라이언트에 결과 전파
                _gameEndSubscription = GameEvents.OnGameEnd
                    .Subscribe(OnGameEndServer);

                GameLog.Dev.Info("Network", nameof(NetworkGameEndController), "서버 측 OnGameEnd 구독 완료");
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
            // 이 오브젝트가 아직 네트워크에 살아 있고, 내가 서버일 때만 진행한다.
            //   이 핸들러는 아래에서 AnnounceWinnerClientRpc 를 전송한다.
            //
            //   이유는 NetworkCombatController.Update() 진입부 가드와 같다 — 그쪽의 상세 주석 참조.
            //   (요약: IsServer 는 "내가 서버 역할인가" 이지 "이 오브젝트가 아직 살아 있는가" 가 아니다.
            //    NetworkManager.Shutdown() 이 불린 뒤에도 디스폰 전까지 IsServer 는 여전히 참이므로,
            //    위험 구간은 NetworkManager.Shutdown() 과 디스폰 사이다.)
            //
            //   ※ IsSpawned 를 앞에 두는 이유 — || 는 앞 조건이 참이면 뒤를 평가하지 않는다(단락 평가).
            //     스폰되지 않은 상태에서 IsServer 를 건드리지 않고 곧바로 반환하기 위해서다
            //     (NetworkUnit.cs:291 이 같은 이유로 같은 순서를 쓴다).
            //
            //   ※ 아래 _announced 가드와 목적이 다르다 — _announced 는 "중복 발표 방지",
            //     이 줄은 "네트워크 생존 확인" 이다. 그래서 합치지 않고 순서도 그대로 둔다.
            //     정상 경로에서는 승패 확정이 Shutdown 보다 수 초 앞서므로 승자 발표가 막히지 않는다.
            if (!IsSpawned || !IsServer) return;
            if (_announced) return;

            _announced = true;
            int winnerTeamIndex = (int)e.Winner;

            // 랜덤 매칭 여부 확인 — 재경기 버튼 분기에 사용.
            // OnNetworkSpawn에서 미리 캐시해 둔 참조를 사용 (매번 씬 탐색하지 않음).
            bool isRandomMatch = false;
            var ngm = _networkGameManager;
            if (ngm != null)
                isRandomMatch = ngm.IsRandomMatchmaking;

            // 승리 팀 · 인덱스 · 랜덤매칭 여부는 나중에 집계·필터링에 쓰일 값이므로
            // 문장에 섞지 않고 key=value 자리로 뺀다(LogRules.md 1.4 형식 — "key=value가 곧 전송 데이터다").
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "서버: 게임 종료 감지 — 결과 전파 시작",
                $"WinnerTeam={e.Winner}, WinnerTeamIndex={winnerTeamIndex}, IsRandomMatch={isRandomMatch}");

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
        /// UI 컴포넌트는 GameEvents 이벤트를 각자 구독해 반응한다.
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

            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "AnnounceWinnerClientRpc 수신",
                $"WinnerTeam={winnerTeam}, LocalTeam={LocalPlayerTeam.Current}, IsRandomMatch={isRandomMatch}");

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
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "ForceWin 호출 — 상대 연결 끊김으로 강제 승리 처리",
                $"WinnerTeamIndex={winnerTeamIndex}");

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
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController), "포기 요청 전송");
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
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "포기 처리",
                $"ForfeiterClientId={forfeiterId}, ForfeitTeam={forfeitTeam}, WinnerTeam={winnerTeam}");

            // ----------------------------------------------------------------
            // [버그 수정 2026-05-27] 호스트 측 GameEndUI 미표시 문제 해결
            // ----------------------------------------------------------------
            // 정상 종료 흐름(Castle 파괴)은 다음 경로를 거친다:
            //   GameEndUseCase → GameEvents.OnGameEnd 발행 → OnGameEndServer 수신
            //   → AnnounceWinnerClientRpc 호출
            // 이 경로에서는 서버(호스트)에서 OnGameEnd가 이미 발행된 상태이므로
            // AnnounceWinnerClientRpc 안의 "!IsServer" 가드(중복 발행 방지)가 의미를 가진다.
            //
            // 그러나 포기 흐름(ForfeitServerRpc)은 GameEndUseCase를 건너뛰고
            // 곧바로 AnnounceWinnerClientRpc를 호출한다. 그 결과:
            //   - 클라이언트(비서버): !IsServer == true → OnGameEnd 발행 → GameEndUI 표시 정상
            //   - 호스트(서버):       !IsServer == false → OnGameEnd 미발행 → GameEndUI 미표시 (버그)
            //
            // 따라서 포기 흐름에서는 서버 측에서도 OnGameEnd를 명시적으로 발행해야 한다.
            // OnGameEndServer는 위에서 _announced=true로 설정되었기 때문에
            // 이 발행을 다시 받아도 153행 가드에 의해 즉시 return → 중복 처리 없음.
            // ----------------------------------------------------------------
            GameEvents.OnGameEnd.OnNext(new GameEndEvent(winnerTeam));

            // 포기에 의한 정상 종료 — 재경기 신청은 가능하도록 isRandomMatch=false 전달.
            // (랜덤 매칭이라도 isRandomMatch는 NetworkGameManager에서 별도로 판단되지만,
            //  여기서는 보수적으로 false를 넣어 클라이언트가 재경기 버튼을 띄울 수 있게 함.
            //  실제 랜덤매칭 종료 처리는 기존 OnGameEndServer 경로와 동일하게 동작.)
            AnnounceWinnerClientRpc((int)winnerTeam, false);
        }

        // ====================================================================
        // 재경기 (Rematch) — 커스텀게임 전용
        // ====================================================================

        /// <summary>
        /// 클라이언트의 재경기 요청을 서버에서 처리.
        /// 첫 요청 시 상대에게 알림, 양측 모두 요청 시 즉시 재경기 시작.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void RequestRematchServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong requesterId = rpcParams.Receive.SenderClientId;
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "RequestRematchServerRpc 수신",
                $"RequesterClientId={requesterId}, PreviousRequesterClientId={_rematchRequesterId}");

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
                GameLog.Dev.Info("Network", nameof(NetworkGameEndController), "양측 재경기 동의 — 즉시 재경기 시작");
                StartRematch();
            }
        }

        /// <summary>
        /// 상대 클라이언트에게 재경기 요청이 들어왔음을 알림.
        /// 팝업으로 수락/거절 선택지 표시.
        ///
        /// GameEvents.OnNetworkRematchRequested를 발행하면 팝업이 자체 구독으로 ShowRequest를 호출한다.
        /// 수락/거절은 OnLocalRematchAccepted / OnLocalRematchDeclined 이벤트로 다시 본 컨트롤러에 전달된다.
        /// </summary>
        [ClientRpc]
        private void NotifyRematchRequestedClientRpc(ClientRpcParams clientRpcParams = default)
        {
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController), "재경기 요청 수신 — OnNetworkRematchRequested 발행");
            GameEvents.OnNetworkRematchRequested.OnNext(new NetworkRematchRequestedEvent());
        }

        /// <summary>
        /// 재경기 수락을 서버에서 처리. 즉시 재경기 시작.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void AcceptRematchServerRpc()
        {
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController), "AcceptRematchServerRpc 수신 — 재경기 시작");
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
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                "DeclineRematchServerRpc 수신",
                $"DeclinerClientId={declinerId}");

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
        /// GameEvents.OnNetworkRematchDeclined를 발행하면 두 UI(RematchRequestPopup / GameEndUI)가 자체 구독으로 처리한다.
        /// </summary>
        [ClientRpc]
        private void NotifyRematchDeclinedClientRpc(ClientRpcParams clientRpcParams = default)
        {
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController), "재경기 거절 알림 수신 — OnNetworkRematchDeclined 발행");
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
                    // 이 로그는 **루프 안**에서 오브젝트 개수만큼 반복 출력된다.
                    // Dev 로 두는 이유: Dev 메서드에는 [Conditional] 두 개가 붙어 있어
                    // 릴리스 빌드에서는 호출과 인자 평가(netObj.name 접근·문자열 보간)까지
                    // 통째로 사라진다(LogRules.md 1.7 릴리스 스트리핑).
                    // 운영 로그로 두면 릴리스에서 매 오브젝트마다 로그가 쏟아져
                    // LogRules.md 1.14 금지 사항 8(매 틱·매 프레임 로깅 금지)에 걸린다.
                    GameLog.Dev.Info("Network", nameof(NetworkGameEndController),
                        "StartRematch: 동적 NetworkObject Despawn",
                        $"ObjectName={netObj.name}");
                    netObj.Despawn();
                }
            }

            // ----------------------------------------------------------------
            // [로딩 인디케이터] Game 씬 재로드 직전에 모든 클라이언트(서버 포함)에게
            // "재경기 준비 중..." 로딩 인디케이터를 띄우도록 신호를 보낸다.
            // 씬이 재로드되어 새 GameBootstrapper.LoadMap()이 완료되면 자동으로 꺼진다(UI 규칙 L-3).
            // ----------------------------------------------------------------
            NotifyRematchStartingClientRpc();

            GameLog.Dev.Info("Network", nameof(NetworkGameEndController), "StartRematch: Game 씬 재로드");
            NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
        }

        /// <summary>
        /// 재경기 시작을 모든 클라이언트(서버 포함)에 알려 로딩 인디케이터를 표시하게 한다.
        /// ClientRpc는 NetworkBehaviour(Infrastructure)에서만 호출 가능하다.
        /// UIManager(Presentation)를 직접 참조하지 않고 GameEvents(Application)를 경유 발행하여
        /// 레이어 방향(Infrastructure → Presentation 역행)을 어기지 않는다.
        /// GameEndUI가 OnNetworkRematchStarting을 구독해 ShowLoading(true)를 호출한다(UI 규칙 L-3).
        ///
        /// 호스트(서버)도 ClientRpc 본문이 로컬에서 실행되므로 별도 호출 없이 함께 로딩이 표시된다.
        /// </summary>
        [ClientRpc]
        private void NotifyRematchStartingClientRpc()
        {
            GameLog.Dev.Info("Network", nameof(NetworkGameEndController), "NotifyRematchStartingClientRpc 수신 — 재경기 로딩 표시 신호 발행");
            GameEvents.OnNetworkRematchStarting.OnNext(Unit.Default);
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
