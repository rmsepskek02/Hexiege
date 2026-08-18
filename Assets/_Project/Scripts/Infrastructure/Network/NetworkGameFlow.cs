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
using Hexiege.Application.Combat.Sequencing;

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

        [Header("B3 이동 권위 전환")]
        [Tooltip("서버가 다음 경기 전체에 고정할 이동 writer. 안전 기본값은 Legacy입니다.")]
        [SerializeField]
        private UnitMovementPipelineMode _serverMovementPipelineMode =
            UnitMovementPipelineMode.Legacy;
        private UnitMovementPipelineMode _selectedMovementPipelineMode =
            UnitMovementPipelineMode.Legacy;

        private const int UnitMovementSchemaRevision =
            UnitMovementReducer.ContractSchemaRevision;
        private const int SupportedMovementModeMask =
            (1 << (int)UnitMovementPipelineMode.Legacy)
            | (1 << (int)UnitMovementPipelineMode.ReducerAuthoritative);

        private readonly System.Collections.Generic.HashSet<ulong> _readyClients =
            new System.Collections.Generic.HashSet<ulong>();

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
                Debug.LogError("[Network] NetworkGameFlow: GameServicesLocator에 IGameServices가 등록되지 않았습니다.");
                return;
            }

            Debug.Log($"[Network] NetworkGameFlow 스폰. IsServer={IsServer}, IsHost={IsHost}");

            if (IsServer)
            {
                if (!UnitMovementPipelineModeLatch.IsSupported(
                    _serverMovementPipelineMode))
                {
                    Debug.LogError(
                        "[Network] 지원하지 않는 서버 이동 mode입니다. 게임 시작을 거부합니다.");
                    return;
                }

                _selectedMovementPipelineMode = _serverMovementPipelineMode;
            }

            // 게임이 이미 진행 중이면 재스폰으로 인한 중복 시작 차단
            // (NetworkObject가 Despawn → Respawn될 때 _gameStarted/_readyCount가 리셋되는 것 방지)
            if (_services.IsNetworkGameStarted)
            {
                Debug.LogWarning("[Network] NetworkGameFlow: 게임 이미 진행 중 감지. " +
                                 "재스폰으로 인한 준비 신호 재전송 차단.");
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

            Debug.Log($"[Network] 팀 직접 할당. IsHost={IsHost}, 팀={myTeam}");

            // 로비에서 선택한 종족을 int로 캐스팅하여 서버에 전송
            int myRace = (int)LocalPlayerRace.Current;
            RequestReadyServerRpc(
                myRace,
                UnitMovementSchemaRevision,
                SupportedMovementModeMask);
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
        public void RequestReadyServerRpc(
            int race,
            int movementSchemaRevision,
            int supportedMovementModeMask,
            ServerRpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            int selectedModeBit = 1 << (int)_selectedMovementPipelineMode;
            bool validSelection =
                UnitMovementPipelineModeLatch.IsSupported(
                    _selectedMovementPipelineMode);
            bool compatible = validSelection
                && movementSchemaRevision == UnitMovementSchemaRevision
                && (supportedMovementModeMask & selectedModeBit) != 0;
            if (!compatible)
            {
                Debug.LogError(
                    $"[Network] 이동 파이프라인 호환 실패. ClientId={senderId}, " +
                    $"serverMode={_selectedMovementPipelineMode}, " +
                    $"serverSchema={UnitMovementSchemaRevision}, " +
                    $"clientSchema={movementSchemaRevision}, " +
                    $"clientModeMask={supportedMovementModeMask}. 게임 시작을 거부합니다.");
                if (senderId != NetworkManager.ServerClientId)
                    NetworkManager.DisconnectClient(senderId);
                return;
            }

            if (!_readyClients.Add(senderId))
                return;

            _readyCount = _readyClients.Count;
            Debug.Log($"[Network] 준비 신호 수신. ClientId={senderId}, Race={race}, 준비 완료={_readyCount}/2");

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
                Debug.Log(
                    $"[Network] 모든 플레이어 준비 완료. 게임 시작 명령 전송. " +
                    $"BlueRace={_blueRace}, RedRace={_redRace}, " +
                    $"UnitMovementPipelineMode={_selectedMovementPipelineMode}, " +
                    $"MovementSchemaRevision={UnitMovementSchemaRevision}");
                StartGameClientRpc(
                    _blueRace,
                    _redRace,
                    (int)_selectedMovementPipelineMode,
                    UnitMovementSchemaRevision);
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
        private void StartGameClientRpc(
            int blueRace,
            int redRace,
            int movementPipelineMode,
            int movementSchemaRevision)
        {
            UnitMovementPipelineMode selectedMovementMode =
                (UnitMovementPipelineMode)movementPipelineMode;
            if (movementSchemaRevision != UnitMovementSchemaRevision
                || !UnitMovementPipelineModeLatch.IsSupported(selectedMovementMode)
                || !NetworkContext.TryBeginUnitMovementPipelineMatch(
                    selectedMovementMode))
            {
                Debug.LogError(
                    $"[Network] 게임 시작 이동 계약 불일치. " +
                    $"mode={movementPipelineMode}, schema={movementSchemaRevision}. " +
                    "로컬 게임 시작을 거부합니다.");
                return;
            }

            Debug.Log(
                $"[Network] 게임 시작 ClientRpc 수신. 로컬 팀={LocalPlayerTeam.Current}, " +
                $"BlueRace={blueRace}, RedRace={redRace}, " +
                $"UnitMovementPipelineMode={selectedMovementMode}");

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (selectedMovementMode == UnitMovementPipelineMode.ReducerAuthoritative)
                UnitMovementAuthorityObserver.BeginSession(NetworkManager, IsServer);
            else
                UnitMovementShadowObserver.BeginSession(NetworkManager, IsServer);
#endif

            // 양 팀 종족 정보를 전역 홀더에 저장 — 인게임에서 종족별 초기화에 사용
            GameRaceContext.Set((RaceId)blueRace, (RaceId)redRace);

            // _services는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            if (_services == null)
            {
                Debug.LogError("[Network] StartGameClientRpc: GameBootstrapper를 찾을 수 없습니다.");
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
                Debug.LogWarning("[Network] SyncInitialGold: ResourceUseCase가 null. 초기 골드 동기화 생략.");
                return;
            }

            // OnResourceChanged를 강제 발행하여 NetworkResourceSync._blueGold / _redGold 갱신
            int blueGold = resource.GetGold(TeamId.Blue);
            int redGold = resource.GetGold(TeamId.Red);

            GameEvents.OnResourceChanged.OnNext(new ResourceChangedEvent(TeamId.Blue, blueGold));
            GameEvents.OnResourceChanged.OnNext(new ResourceChangedEvent(TeamId.Red, redGold));

            Debug.Log($"[Network] 초기 골드 동기화 완료. Blue={blueGold}, Red={redGold}");
        }
    }
}
