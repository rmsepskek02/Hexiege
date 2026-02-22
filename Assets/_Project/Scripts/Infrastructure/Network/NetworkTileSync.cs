// ============================================================================
// NetworkTileSync.cs
// 타일 소유권 변경을 서버→모든 클라이언트에 전파하는 NetworkBehaviour.
//
// 역할:
//   - 서버: GameEvents.OnTileOwnerChanged 구독 → BroadcastTileChangeClientRpc 호출
//   - 클라이언트: RPC 수신 → HexGrid 도메인 상태 갱신 + GameEvents 재발행 (HexTileView 색상 갱신)
//
// 흐름 (서버):
//   UnitMovementUseCase.ProcessStep()
//     → _grid.SetOwner() (서버 도메인 상태 갱신)
//     → GameEvents.OnTileOwnerChanged.OnNext() (서버 HexTileView 갱신)
//     → NetworkTileSync가 구독 → BroadcastTileChangeClientRpc() 호출
//
// 흐름 (클라이언트):
//   BroadcastTileChangeClientRpc 수신
//     → GameBootstrapper._grid.SetOwner() (클라이언트 도메인 상태 갱신)
//     → GameEvents.OnTileOwnerChanged.OnNext() (클라이언트 HexTileView 색상 갱신)
//
// 주의:
//   - 서버 자신은 BroadcastTileChangeClientRpc에서 IsServer 체크로 스킵
//     (서버는 이미 이벤트를 직접 처리했으므로 중복 처리 방지)
//   - 싱글플레이 시 IsSpawned = false → 이벤트 구독 자체가 되지 않으므로 영향 없음
//   - LoadMap() 호출마다 이벤트 구독이 갱신될 수 있으므로 OnNetworkDespawn에서 해제
//
// 배치:
//   씬에 빈 GameObject "NetworkTileSync" 생성 후 이 컴포넌트 + NetworkObject 부착.
//   NetworkManager의 NetworkPrefabs에 등록하고 씬에 배치하면 Host 시작 시 자동 스폰.
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using System;
using Unity.Netcode;
using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 타일 점령 상태를 네트워크로 동기화하는 NetworkBehaviour.
    /// 서버에서 발행된 OnTileOwnerChanged 이벤트를 모든 클라이언트에 전파.
    /// </summary>
    public class NetworkTileSync : NetworkBehaviour
    {
        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary> 서버의 이벤트 구독 해제 토큰. </summary>
        private IDisposable _tileOwnerChangedSubscription;

        /// <summary> GameBootstrapper 참조. HexGrid 접근에 사용. </summary>
        private Hexiege.Bootstrap.GameBootstrapper _bootstrapper;

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 호출.
        /// 서버에서만 OnTileOwnerChanged 이벤트 구독.
        /// 클라이언트는 ClientRpc 수신만 처리.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // GameBootstrapper 탐색 (HexGrid 접근용)
            _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();
            if (_bootstrapper == null)
            {
                Debug.LogWarning("[Network] NetworkTileSync: GameBootstrapper를 찾을 수 없습니다. 맵 로드 후 재시도.");
            }

            // 서버에서만 타일 변경 이벤트를 구독하여 클라이언트에 전파
            if (IsServer)
            {
                SubscribeTileOwnerChanged();
                Debug.Log("[Network] NetworkTileSync: 서버 모드로 타일 소유권 동기화 시작.");
            }
            else
            {
                Debug.Log("[Network] NetworkTileSync: 클라이언트 모드. ClientRpc 대기 중.");
            }
        }

        /// <summary>
        /// 네트워크 디스폰 시 호출.
        /// 이벤트 구독 해제로 메모리 누수 방지.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            UnsubscribeTileOwnerChanged();
            Debug.Log("[Network] NetworkTileSync: 디스폰. 이벤트 구독 해제.");
        }

        // ====================================================================
        // 서버 전용: 이벤트 구독
        // ====================================================================

        /// <summary>
        /// 서버에서 OnTileOwnerChanged 이벤트 구독.
        /// 타일 소유권 변경 시 모든 클라이언트에 RPC 전송.
        /// </summary>
        private void SubscribeTileOwnerChanged()
        {
            // 기존 구독이 있으면 먼저 해제 (LoadMap 재호출 대비)
            UnsubscribeTileOwnerChanged();

            _tileOwnerChangedSubscription = GameEvents.OnTileOwnerChanged
                .Subscribe(OnTileOwnerChangedOnServer);

            Debug.Log("[Network] NetworkTileSync: OnTileOwnerChanged 구독 완료.");
        }

        /// <summary>
        /// 이벤트 구독 해제.
        /// </summary>
        private void UnsubscribeTileOwnerChanged()
        {
            _tileOwnerChangedSubscription?.Dispose();
            _tileOwnerChangedSubscription = null;
        }

        /// <summary>
        /// 서버에서 타일 소유권 변경 이벤트 수신.
        /// 해당 정보를 모든 클라이언트에 ClientRpc로 전파.
        /// </summary>
        private void OnTileOwnerChangedOnServer(TileOwnerChangedEvent evt)
        {
            // ClientRpc로 모든 클라이언트에 전파
            BroadcastTileChangeClientRpc(evt.Coord.Q, evt.Coord.R, (int)evt.NewOwner);
        }

        // ====================================================================
        // ClientRpc — 서버 → 모든 클라이언트
        // ====================================================================

        /// <summary>
        /// 서버가 타일 소유권 변경을 모든 클라이언트에 전파.
        ///
        /// 서버 자신은 이미 UnitMovementUseCase에서 처리했으므로 스킵.
        /// 클라이언트는:
        ///   1. HexGrid 도메인 상태 갱신 (SetOwner)
        ///   2. GameEvents 재발행 → HexTileView 색상 자동 갱신
        /// </summary>
        /// <param name="q">타일 큐브 좌표 Q</param>
        /// <param name="r">타일 큐브 좌표 R</param>
        /// <param name="teamIndex">TeamId를 int로 직렬화한 값 (Neutral=0, Blue=1, Red=2)</param>
        [ClientRpc]
        private void BroadcastTileChangeClientRpc(int q, int r, int teamIndex)
        {
            // 서버(Host 포함)는 이미 로컬에서 처리됨 → 중복 처리 방지
            if (IsServer) return;

            HexCoord coord = new HexCoord(q, r);
            TeamId newOwner = (TeamId)teamIndex;

            // bootstrapper 참조가 없으면 재탐색 (맵 로드 타이밍 차이 대비)
            if (_bootstrapper == null)
            {
                _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();
                if (_bootstrapper == null)
                {
                    Debug.LogWarning($"[Network] BroadcastTileChangeClientRpc: GameBootstrapper 없음. 좌표=({q},{r})");
                    return;
                }
            }

            // 1. 클라이언트의 HexGrid 도메인 상태 갱신
            HexGrid grid = _bootstrapper.GetGrid();
            if (grid != null)
            {
                grid.SetOwner(coord, newOwner);
            }
            else
            {
                Debug.LogWarning($"[Network] BroadcastTileChangeClientRpc: HexGrid가 null. 맵 로드 전일 수 있음. 좌표=({q},{r})");
            }

            // 2. GameEvents 재발행 → HexTileView가 자동으로 색상 갱신
            GameEvents.OnTileOwnerChanged.OnNext(new TileOwnerChangedEvent(coord, newOwner));

            Debug.Log($"[Network] 타일 동기화 수신. 좌표=({q},{r}), 팀={newOwner}");
        }
    }
}
