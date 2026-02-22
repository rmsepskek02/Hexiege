// ============================================================================
// TeamAssigner.cs
// 네트워크 게임에서 플레이어 팀을 할당하는 NetworkBehaviour.
//
// 역할:
//   - 서버가 연결된 플레이어들에게 팀을 자동 할당
//     Host(OwnerClientId=0) → Blue 팀
//     Client(OwnerClientId!=0) → Red 팀
//   - NetworkVariable로 팀 인덱스를 동기화 (서버 Write, 클라이언트 Read)
//   - 팀이 확정되면 OnTeamAssigned 이벤트 발행 + LocalPlayerTeam 갱신
//
// 배치:
//   NetworkManager의 Player Prefab에 부착.
//   OnNetworkSpawn이 각 플레이어 오브젝트 생성 시 호출됨.
//
// 팀 매핑:
//   _assignedTeamIndex 0 → TeamId.Blue (Host)
//   _assignedTeamIndex 1 → TeamId.Red  (Client)
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using System;
using Unity.Netcode;
using UnityEngine;
using UniRx;
using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 플레이어 팀 할당 NetworkBehaviour.
    /// Player Prefab에 부착하여 스폰 시 자동으로 팀을 배정받음.
    /// </summary>
    public class TeamAssigner : NetworkBehaviour
    {
        // ====================================================================
        // NetworkVariable — 서버만 쓰고, 모든 클라이언트가 읽음
        // 0 = Blue, 1 = Red (TeamId - 1 에 대응)
        // ====================================================================

        /// <summary>
        /// 서버에서 할당한 팀 인덱스. 0=Blue, 1=Red.
        /// 클라이언트는 이 값이 변경되면 OnTeamIndexChanged 콜백 수신.
        /// </summary>
        private readonly NetworkVariable<int> _assignedTeamIndex = new NetworkVariable<int>(
            value: -1, // -1 = 미할당 초기 상태
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        // ====================================================================
        // 이벤트 (UniRx Subject)
        // ====================================================================

        /// <summary>
        /// 팀이 확정되었을 때 발행. 구독자에게 TeamId 전달.
        /// 로컬 플레이어의 TeamAssigner에서만 발행됨.
        /// </summary>
        public IObservable<TeamId> OnTeamAssigned => _onTeamAssigned;
        private readonly Subject<TeamId> _onTeamAssigned = new Subject<TeamId>();

        // ====================================================================
        // 프로퍼티
        // ====================================================================

        /// <summary>현재 할당된 팀. 미할당 상태면 Blue 반환(안전 기본값).</summary>
        public TeamId LocalTeam
        {
            get
            {
                return _assignedTeamIndex.Value switch
                {
                    0 => TeamId.Blue,
                    1 => TeamId.Red,
                    _ => TeamId.Blue // 미할당 시 기본값
                };
            }
        }

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 호출.
        /// 서버: OwnerClientId 기반으로 팀 할당.
        /// 클라이언트: NetworkVariable 변경 감지 콜백 등록.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // 서버: 팀 할당 결정
            if (IsServer)
            {
                AssignTeamOnServer();
            }

            // 모든 인스턴스: NetworkVariable 변경 감지 등록
            _assignedTeamIndex.OnValueChanged += OnTeamIndexChanged;

            // 이미 값이 설정된 경우(늦게 참가) 즉시 처리
            if (_assignedTeamIndex.Value >= 0)
            {
                HandleTeamAssigned(_assignedTeamIndex.Value);
            }

            Debug.Log($"[Network] TeamAssigner 스폰. IsOwner={IsOwner}, IsServer={IsServer}");
        }

        /// <summary>
        /// 네트워크 디스폰 시 호출.
        /// 이벤트 구독 해제 및 Subject 완료 처리.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            _assignedTeamIndex.OnValueChanged -= OnTeamIndexChanged;
            _onTeamAssigned.OnCompleted();

            Debug.Log("[Network] TeamAssigner 디스폰.");
        }

        // ====================================================================
        // 서버 전용 로직
        // ====================================================================

        /// <summary>
        /// 서버에서 이 플레이어 오브젝트의 팀을 결정.
        /// Host(OwnerClientId=0) → 0(Blue), 나머지 → 1(Red).
        /// </summary>
        private void AssignTeamOnServer()
        {
            int teamIndex = (OwnerClientId == 0) ? 0 : 1;
            _assignedTeamIndex.Value = teamIndex;

            TeamId assignedTeam = teamIndex == 0 ? TeamId.Blue : TeamId.Red;
            Debug.Log($"[Network] 팀 할당 완료. ClientId={OwnerClientId}, 팀={assignedTeam}");
        }

        // ====================================================================
        // 팀 변경 콜백
        // ====================================================================

        /// <summary>
        /// NetworkVariable 변경 감지 콜백.
        /// 로컬 플레이어 오브젝트에서만 LocalPlayerTeam 갱신 및 이벤트 발행.
        /// </summary>
        private void OnTeamIndexChanged(int previousValue, int newValue)
        {
            if (newValue < 0) return; // 미할당 상태 무시
            HandleTeamAssigned(newValue);
        }

        /// <summary>
        /// 팀 인덱스 확정 처리.
        /// 로컬 플레이어라면 LocalPlayerTeam 갱신 후 이벤트 발행.
        /// </summary>
        private void HandleTeamAssigned(int teamIndex)
        {
            TeamId team = teamIndex == 0 ? TeamId.Blue : TeamId.Red;

            // 로컬 플레이어 오브젝트에서만 갱신
            if (IsOwner)
            {
                LocalPlayerTeam.Set(team);
                _onTeamAssigned.OnNext(team);
                Debug.Log($"[Network] 로컬 플레이어 팀 확정: {team}");
            }
        }
    }
}
