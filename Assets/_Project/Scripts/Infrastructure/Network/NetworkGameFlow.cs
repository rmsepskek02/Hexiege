// ============================================================================
// NetworkGameFlow.cs
// 네트워크 게임 시작 흐름을 총괄하는 NetworkBehaviour.
//
// 역할:
//   - 서버: 모든 플레이어 준비 신호 수집 → 동시 게임 시작 명령 전송
//   - 클라이언트: 준비 완료 신호 서버에 전송 → 게임 시작 명령 대기
//   - 게임 시작 시 GameBootstrapper.StartNetworkGame() 호출 (맵 로드)
//
// 흐름:
//   1. OnNetworkSpawn() 호출 (Host/Client 모두)
//   2. 각 클라이언트: IsHost 기반으로 팀 직접 결정 후 RequestReadyServerRpc() 호출
//   3. 서버: 2명 준비 완료 시 StartGameClientRpc() 호출
//   4. 모든 클라이언트: GameBootstrapper에서 맵 로드 + 팀별 초기화
//
// 배치:
//   씬에 빈 GameObject "NetworkGameFlow" 배치 후 이 컴포넌트 부착.
//   NetworkObject 컴포넌트도 필요.
//
// 주의:
//   이 오브젝트는 NetworkObject이므로 Host가 StartHost() 후 직접 Spawn해야 함.
//   또는 NetworkManager의 씬 오브젝트로 등록하여 자동 Spawn.
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Hexiege.Core;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 네트워크 게임 시작 흐름 총괄 NetworkBehaviour.
    /// GameBootstrapper의 네트워크 버전으로, 양 플레이어 준비 완료 후 게임을 동시 시작.
    /// </summary>
    public class NetworkGameFlow : NetworkBehaviour
    {
        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>준비 완료 신호를 보낸 클라이언트 수.</summary>
        private int _readyCount = 0;

        /// <summary>게임 시작 여부 (중복 시작 방지).</summary>
        private bool _gameStarted = false;

        /// <summary>게임 서비스 참조. Bootstrap에 직접 의존하지 않도록 IGameServices로 추상화.</summary>
        private IGameServices _services;

        /// <summary>Blue 팀(Host)이 선택한 종족. 서버에서만 사용.</summary>
        private int _blueRace = 0;

        /// <summary>Red 팀(Client)이 선택한 종족. 서버에서만 사용.</summary>
        private int _redRace = 0;

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 호출.
        /// 부트스트래퍼를 찾고 팀 할당 완료 후 준비 신호 전송.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // GameServicesLocator에서 IGameServices 획득.
            // Bootstrap에 직접 의존하지 않으므로 FindFirstObjectByType<GameBootstrapper>() 불필요.
            _services = GameServicesLocator.Current;
            if (_services == null)
            {
                // [운영] 여기서 함수를 빠져나가면 준비 신호(RequestReadyServerRpc)가 아예 전송되지 않아
                //   게임이 시작되지 못한다 — 복구 경로가 없다 → 축 A Error.
                //   (같은 키를 쓰는 NetworkBuildingController:58 등은 스폰이 계속되므로 Warn 이었다.
                //    여기는 즉시 빠져나가므로 결과가 다르고, 그래서 축 A 만 다르다.)
                //   플레이어에게 알리는 경로가 없어 이 로그가 유일한 기록이다 → 축 B 운영.
                GameLog.Ops.Error(LogEvent.NetworkControllerSpawnedWithoutGameServices,
                                  "Network", nameof(NetworkGameFlow),
                                  "GameServicesLocator 에 IGameServices 가 등록되지 않았다 — 게임을 시작할 수 없다");
                return;
            }

            // [개발] 진입 흔적.
            GameLog.Dev.Info("Network", nameof(NetworkGameFlow), "네트워크 스폰",
                             $"IsServer={IsServer}, IsHost={IsHost}");

            // 게임이 이미 진행 중이면 재스폰으로 인한 중복 시작 차단
            // (NetworkObject가 Despawn → Respawn될 때 _gameStarted/_readyCount가 리셋되는 것 방지)
            if (_services.IsNetworkGameStarted)
            {
                // ⚠️ [잠정 판정] Warn + 개발.
                //   축 A: 중복 시작을 막고 게임은 그대로 계속된다 = 가드가 곧 복구다 → Warn.
                //   축 B: ①("플레이어 기기에서만 벌어지는가")를 "아니오"로 봤다 —
                //         이 재스폰은 재경기(씬 재로드) 경로에서 에디터 2인 구성으로 재현할 수 있다.
                //   ⚠️ 다만 "회선 불안정으로 인한 Despawn→Respawn" 이라면 ①이 "예"가 되어 운영이
                //      되고 새 키가 필요하다. 어느 쪽인지 코드만으로 단정할 수 없어 잠정으로 둔다.
                //      (LogRules 1.5 — 키 이름은 한 번 정하면 바꿀 수 없으므로 애매하면 만들지 않는다)
                GameLog.Dev.Warn("Network", nameof(NetworkGameFlow),
                                 "게임 이미 진행 중 감지 — 재스폰으로 인한 준비 신호 재전송을 차단했다");
                return;
            }

            // 팀 할당 대기 후 준비 신호 전송 (코루틴으로 폴링)
            StartCoroutine(WaitForTeamAndSendReady());
        }

        // ====================================================================
        // 준비 흐름
        // ====================================================================

        /// <summary>
        /// 팀을 직접 할당하고 서버에 준비 신호 전송.
        /// Player Prefab이 None이므로 TeamAssigner가 스폰되지 않아 IsHost로 직접 결정.
        /// Host → Blue, Client → Red.
        /// </summary>
        private IEnumerator WaitForTeamAndSendReady()
        {
            TeamId myTeam = IsHost ? TeamId.Blue : TeamId.Red;
            LocalPlayerTeam.Set(myTeam);

            // [개발] 의도된 흐름. 팀이 틀리면 화면(진영·카메라)에 즉시 드러난다.
            GameLog.Dev.Info("Network", nameof(NetworkGameFlow), "팀 직접 할당",
                             $"IsHost={IsHost}, Team={myTeam}");

            // 로비에서 선택한 종족을 int로 캐스팅하여 서버에 전송
            int myRace = (int)LocalPlayerRace.Current;
            RequestReadyServerRpc(myRace);
            yield break;
        }

        // ====================================================================
        // ServerRpc — 클라이언트 → 서버
        // ====================================================================

        /// <summary>
        /// 클라이언트가 게임 준비 완료를 서버에 알림.
        /// 로비에서 선택한 종족 정보(int)를 함께 전송.
        /// 2명 모두 준비되면 StartGameClientRpc(blueRace, redRace) 호출.
        /// </summary>
        /// <param name="race">로비에서 선택한 종족. (int)RaceId로 캐스팅하여 전송.</param>
        [ServerRpc(RequireOwnership = false)]
        public void RequestReadyServerRpc(int race, ServerRpcParams rpcParams = default)
        {
            _readyCount++;
            ulong senderId = rpcParams.Receive.SenderClientId;
            // [개발] RPC 수신 덤프. 의도된 흐름이고 에디터 2인 구성으로 그대로 재현된다.
            GameLog.Dev.Info("Network", nameof(NetworkGameFlow), "준비 신호 수신",
                             $"ClientId={senderId}, Race={race}, ReadyCount={_readyCount}, ExpectedPlayers=2");

            // Host(ServerClientId)가 보낸 종족 → Blue 팀, 그 외 → Red 팀
            if (senderId == NetworkManager.ServerClientId)
            {
                _blueRace = race;
            }
            else
            {
                _redRace = race;
            }

            // 접속 중인 클라이언트 수 = 2명 (Host + Client)
            int expectedPlayers = 2;
            if (_readyCount >= expectedPlayers && !_gameStarted)
            {
                _gameStarted = true;
                // [개발] 의도된 흐름. 시작 실패는 씬 전환이 일어나지 않는 것으로 드러난다.
                GameLog.Dev.Info("Network", nameof(NetworkGameFlow), "모든 플레이어 준비 완료 — 게임 시작 명령 전송",
                                 $"BlueRace={_blueRace}, RedRace={_redRace}");
                StartGameClientRpc(_blueRace, _redRace);
            }
        }

        // ====================================================================
        // ClientRpc — 서버 → 모든 클라이언트
        // ====================================================================

        /// <summary>
        /// 서버가 모든 클라이언트에 게임 시작을 명령.
        /// 양 팀 종족 정보를 함께 전달하여 GameRaceContext에 저장.
        /// 각 클라이언트에서 팀에 맞게 맵 로드 및 카메라 초기 위치를 설정.
        /// 맵 로드 후 서버에서 초기 골드 동기화를 수행.
        /// </summary>
        /// <param name="blueRace">Blue 팀(Host)이 선택한 종족. (int)RaceId.</param>
        /// <param name="redRace">Red 팀(Client)이 선택한 종족. (int)RaceId.</param>
        [ClientRpc]
        private void StartGameClientRpc(int blueRace, int redRace)
        {
            // [개발] RPC 수신 덤프. 결과(맵 로드)가 화면에 즉시 나타난다.
            GameLog.Dev.Info("Network", nameof(NetworkGameFlow), "게임 시작 ClientRpc 수신",
                             $"Team={LocalPlayerTeam.Current}, BlueRace={blueRace}, RedRace={redRace}");

            // 양 팀 종족 정보를 전역 홀더에 저장 — 인게임에서 종족별 초기화에 사용
            GameRaceContext.Set((RaceId)blueRace, (RaceId)redRace);

            // _services는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            if (_services == null)
            {
                // [운영] 이 클라이언트만 맵을 로드하지 못한다 — 재시도 경로가 없어 게임에 들어가지 못한다.
                //   화면에는 아무 안내도 뜨지 않으므로 이 로그가 유일한 기록이다 → Error + 운영.
                GameLog.Ops.Error(LogEvent.ClientRpcGameServicesMissing, "Network", nameof(NetworkGameFlow),
                                  "StartGameClientRpc: IGameServices 를 찾을 수 없다 — 맵을 로드할 수 없다",
                                  "Request=StartGame");
                return;
            }

            // GameBootstrapper를 통해 네트워크 게임 시작 (맵 로드 + UseCase 생성)
            _services.StartNetworkGame(LocalPlayerTeam.Current);

            // 서버: 맵 로드 후 초기 골드를 NetworkResourceSync를 통해 강제 동기화.
            // ResourceUseCase 생성자에서는 OnResourceChanged 이벤트를 발행하지 않으므로
            // 초기 골드를 클라이언트에 수동으로 전파해야 함.
            if (IsServer)
            {
                SyncInitialGold();
            }
        }

        /// <summary>
        /// 맵 로드 직후 양 팀 초기 골드를 GameEvents로 발행하여 NetworkResourceSync가
        /// NetworkVariable을 갱신하도록 트리거.
        /// ResourceUseCase가 null이면 조용히 무시.
        /// </summary>
        private void SyncInitialGold()
        {
            ResourceUseCase resource = _services.GetResource();
            if (resource == null)
            {
                // [운영] _services 는 바로 위에서 non-null 로 확인됐으므로, 여기서 null 이라는 것은
                //   StartNetworkGame(맵 로드) 이 ResourceUseCase 를 만들지 못했다는 뜻이다.
                //   초기 골드가 클라이언트에 전파되지 않지만 이후 골드 변동이 다시 동기화하므로
                //   복구된다 → 축 A Warn. 화면에는 "골드가 잠깐 이상함" 으로만 보이고 다른 기록이
                //   없으며, 맵 로드 실패는 기기 성능·회선에 좌우된다 → 축 B 운영.
                GameLog.Ops.Warn(LogEvent.ClientRpcGameServicesMissing, "Network", nameof(NetworkGameFlow),
                                 "SyncInitialGold: ResourceUseCase 가 null 이다 — 초기 골드 동기화를 생략했다",
                                 "Request=SyncInitialGold");
                return;
            }

            // OnResourceChanged를 강제 발행하여 NetworkResourceSync._blueGold / _redGold 갱신
            int blueGold = resource.GetGold(TeamId.Blue);
            int redGold = resource.GetGold(TeamId.Red);

            GameEvents.OnResourceChanged.OnNext(new ResourceChangedEvent(TeamId.Blue, blueGold));
            GameEvents.OnResourceChanged.OnNext(new ResourceChangedEvent(TeamId.Red, redGold));

            // [개발] 의도된 흐름. 값이 틀리면 화면의 골드 표시로 즉시 드러난다.
            GameLog.Dev.Info("Network", nameof(NetworkGameFlow), "초기 골드 동기화 완료",
                             $"BlueGold={blueGold}, RedGold={redGold}");
        }
    }
}
