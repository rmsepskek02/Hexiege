// ============================================================================
// NetworkGameEndController.cs
// 네트워크 모드에서 승패 결과를 서버에서 결정하여 모든 클라이언트에 동기화.
//
// 역할:
//   - 서버: OnGameEnd 이벤트 구독 → AnnounceWinnerClientRpc로 전체 전파
//   - 클라이언트: AnnounceWinnerClientRpc 수신 → GameEndUI 표시 (팀 보정 포함)
//   - 멀티플레이 재시작: NetworkManager.Shutdown() → 씬 재로드
//
// 흐름:
//   [서버] Castle 파괴
//     → NetworkCombatController.EntityDiedClientRpc
//     → [서버] GameEndUseCase.OnEntityDied → GameEvents.OnGameEnd
//     → [서버] NetworkGameEndController.OnGameEnd 수신
//     → AnnounceWinnerClientRpc(winnerTeamIndex)
//     → [모든 클라이언트] ShowResult(winner, localTeam) → GameEndUI 표시
//
// 싱글플레이와의 관계:
//   싱글플레이 시 이 컴포넌트는 씬에 없거나 NetworkObject가 스폰되지 않으므로
//   기존 GameEndUseCase → GameEndUI 직접 구독 흐름이 그대로 작동.
//
// 멀티플레이에서 GameEndUI 중복 표시 방지:
//   클라이언트 GameEndUseCase도 OnEntityDied(Castle) → OnGameEnd를 발행하지만,
//   GameEndUI는 첫 수신 후 패널을 띄우고 구독이 남아 있어도 IsGameOver 체크로 중복 발행 없음.
//   AnnounceWinnerClientRpc는 서버 권위의 최종 결과이므로 이것이 표시 기준.
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
    /// 네트워크 승패 판정 동기화 컨트롤러.
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

        [Header("멀티플레이 재시작 설정")]
        [Tooltip("재시작 시 로드할 씬 이름. 로비/메인 메뉴 씬 이름을 입력.")]
        [SerializeField] private string _lobbySceneName = "SampleScene";

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>게임 종료 이벤트 구독 해제용 Disposable.</summary>
        private System.IDisposable _gameEndSubscription;

        /// <summary>결과 발표 여부. 서버에서 중복 전파 방지용.</summary>
        private bool _announced = false;

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 GameEndUI를 탐색하고,
        /// 서버라면 OnGameEnd를 구독하여 승패 발표 준비.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // GameEndUI가 Inspector에 연결되지 않은 경우 씬에서 탐색
            if (_gameEndUI == null)
            {
                _gameEndUI = FindFirstObjectByType<GameEndUI>();
            }

            if (_gameEndUI == null)
            {
                Debug.LogWarning("[Network] NetworkGameEndController: GameEndUI를 찾을 수 없습니다.");
            }

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
        /// 네트워크 디스폰 시 구독 해제.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _gameEndSubscription?.Dispose();
            _gameEndSubscription = null;
        }

        // ====================================================================
        // 이벤트 핸들러 (서버 전용)
        // ====================================================================

        /// <summary>
        /// 서버에서 GameEndEvent를 수신하여 모든 클라이언트에 승자를 전파.
        /// GameEndUseCase.IsGameOver로 중복 발행이 차단되지만,
        /// 이 컨트롤러 측에서도 _announced 플래그로 이중 방어.
        /// </summary>
        private void OnGameEndServer(GameEndEvent e)
        {
            if (!IsServer) return;
            if (_announced) return;

            _announced = true;
            int winnerTeamIndex = (int)e.Winner;

            Debug.Log($"[Network] 서버: 게임 종료 감지. 승리 팀={e.Winner} (index={winnerTeamIndex}). 결과 전파 시작.");

            // 모든 클라이언트에 승자 발표
            AnnounceWinnerClientRpc(winnerTeamIndex);
        }

        // ====================================================================
        // ClientRpc — 서버 → 모든 클라이언트
        // ====================================================================

        /// <summary>
        /// 서버에서 확정된 승리 팀 인덱스를 모든 클라이언트에 전송.
        /// 각 클라이언트는 자신의 로컬 팀과 비교하여 승리/패배를 판단.
        ///
        /// 멀티플레이에서 GameEndUI는 로컬 팀 기준 승/패를 표시해야 하므로
        /// 서버 권위의 winnerTeam과 LocalPlayerTeam.Current를 비교.
        /// </summary>
        /// <param name="winnerTeamIndex">승리한 팀의 TeamId 정수값 (Blue=1, Red=2)</param>
        [ClientRpc]
        private void AnnounceWinnerClientRpc(int winnerTeamIndex)
        {
            TeamId winnerTeam = (TeamId)winnerTeamIndex;

            Debug.Log($"[Network] AnnounceWinnerClientRpc 수신. 승리 팀={winnerTeam}, 로컬 팀={LocalPlayerTeam.Current}");

            if (_gameEndUI == null)
            {
                _gameEndUI = FindFirstObjectByType<GameEndUI>();
                if (_gameEndUI == null)
                {
                    Debug.LogError("[Network] AnnounceWinnerClientRpc: GameEndUI를 찾을 수 없습니다.");
                    return;
                }
            }

            // 멀티플레이 재시작 동작으로 교체 후 결과 표시
            _gameEndUI.OverrideRestartForMultiplayer(OnMultiplayerRestart);

            // 로컬 팀 기준으로 승리/패배 결정하여 UI 표시
            _gameEndUI.ShowResult(winnerTeam, LocalPlayerTeam.Current);
        }

        // ====================================================================
        // 강제 승리 — 상대방 연결 끊김 시 서버에서 호출
        // ====================================================================

        /// <summary>
        /// 서버에서 상대방 연결 끊김으로 인해 남은 팀을 강제 승리 처리.
        /// ReconnectionHandler.OnClientDisconnected() 대기 타임아웃 후 호출.
        /// AnnounceWinnerClientRpc와 동일한 경로로 결과 전파.
        /// </summary>
        /// <param name="winnerTeamIndex">강제 승리할 팀의 TeamId 정수값 (Blue=1, Red=2)</param>
        public void ForceWin(int winnerTeamIndex)
        {
            if (!IsServer) return;
            if (_announced) return;

            _announced = true;
            Debug.Log($"[Network] ForceWin 호출. 강제 승리 팀 index={winnerTeamIndex}");

            // 기존 AnnounceWinnerClientRpc로 동일하게 전파
            AnnounceWinnerClientRpc(winnerTeamIndex);
        }

        // ====================================================================
        // 멀티플레이 재시작 처리
        // ====================================================================

        /// <summary>
        /// 멀티플레이 재시작 버튼 클릭 시 연결 해제 후 씬 재로드.
        /// NetworkManager.Shutdown()으로 네트워크를 완전히 종료하고
        /// 로비 씬(또는 초기 씬)으로 복귀.
        /// </summary>
        private void OnMultiplayerRestart()
        {
            Debug.Log("[Network] 멀티플레이 재시작: 연결 해제 후 씬 재로드.");

            // 시간 복원 (게임 종료 시 timeScale=0으로 멈춤)
            Time.timeScale = 1f;

            // 네트워크 연결 해제
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }

            // 씬 재로드 (로비 복귀)
            SceneManager.LoadScene(_lobbySceneName);
        }
    }
}
