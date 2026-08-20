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

        /// <summary>게임 서비스 참조. HexGrid 접근에 사용. Bootstrap에 직접 의존하지 않도록 IGameServices로 추상화.</summary>
        private IGameServices _services;

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

            // GameServicesLocator에서 IGameServices 획득 (Bootstrap 직접 참조 불필요)
            _services = GameServicesLocator.Current;
            if (_services == null)
            {
                // [운영] 스폰은 계속되고 아직 RPC 수신 전이라 기능이 죽지는 않았다 → Warn.
                //   NGO 스폰 타이밍은 회선에 좌우되고 통지 경로가 없다 → 운영.
                //   선행 판정(NetworkBuildingController:58)과 같은 사건 → 같은 키.
                GameLog.Ops.Warn(LogEvent.NetworkControllerSpawnedWithoutGameServices,
                                 "Network", nameof(NetworkTileSync),
                                 "스폰 시점에 GameServicesLocator 에서 IGameServices 를 얻지 못했다");
            }

            // 서버에서만 타일 변경 이벤트를 구독하여 클라이언트에 전파
            if (IsServer)
            {
                SubscribeTileOwnerChanged();
                // [개발] 의도된 분기. 역할은 이후 흐름으로 드러난다.
                GameLog.Dev.Info("Network", nameof(NetworkTileSync), "서버 모드로 타일 소유권 동기화 시작",
                                 $"IsServer={IsServer}");
            }
            else
            {
                // [개발] 의도된 분기.
                GameLog.Dev.Info("Network", nameof(NetworkTileSync), "클라이언트 모드 — ClientRpc 대기 중",
                                 $"IsServer={IsServer}");
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
            // [개발] 의도된 흐름(스폰 로그와 대칭 쌍).
            GameLog.Dev.Info("Network", nameof(NetworkTileSync), "디스폰 — 이벤트 구독 해제");
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

            // [개발] 진입 흔적. 구독이 실패하면 이후 타일 동기화가 통째로 멈춰 별도로 드러난다.
            GameLog.Dev.Info("Network", nameof(NetworkTileSync), "OnTileOwnerChanged 구독 완료");
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
            // 이 오브젝트가 아직 네트워크에 살아 있고, 내가 서버일 때만 진행한다.
            //   이 핸들러는 바로 아래에서 BroadcastTileChangeClientRpc 를 전송한다.
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
            //   ⚠️ IsServer 조건이 새로 붙지만 동작은 달라지지 않는다 — 이 이벤트 구독은
            //      OnNetworkSpawn 의 IsServer 블록(84행) 안에서만 이루어지므로 원래도 서버에서만
            //      호출되던 자리다.
            //
            //   🔴 아래 BroadcastTileChangeClientRpc 본문에 있는 `if (IsServer) return;` 과 혼동하지 말 것.
            //      그것은 "클라이언트 수신부에서 서버의 중복 처리를 막는" 정반대 목적의 가드이고 부호도 반대다.
            if (!IsSpawned || !IsServer) return;

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

            // _services는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            if (_services == null)
            {
                // [운영] ⚠️ 축 A 승격(원래 Warning → Error).
                //   타일 소유권 변경은 "그때 한 번만" 오는 이벤트라 재전송 경로가 없다.
                //   여기서 빠져나가면 이 클라이언트의 타일 색·점령 상태가 영구히 어긋난다
                //   — "복구되었나?" 가 아니오 → Error(LogRules 1.3 원칙 3).
                //   선행 판정의 같은 사유 승격 사례: LogAudit.md 3-2 의 272·490 행(↑승격).
                //   화면에는 "타일 색이 이상함" 으로만 보이고 통지가 없다 → 운영.
                GameLog.Ops.Error(LogEvent.ClientRpcGameServicesMissing, "Network", nameof(NetworkTileSync),
                                  "BroadcastTileChangeClientRpc: IGameServices 를 찾을 수 없다 — 타일 상태가 영구히 어긋난다",
                                  $"Request=TileSync, Q={q}, R={r}");
                return;
            }

            // 1. 클라이언트의 HexGrid 도메인 상태 갱신
            HexGrid grid = _services.GetGrid();
            if (grid != null)
            {
                grid.SetOwner(coord, newOwner);
            }
            else
            {
                // [운영] 맵 로드 전이면 이 시점의 도메인 갱신은 버려지지만,
                //   맵 로드가 초기 소유권을 다시 세팅하므로 복구된다 → 축 A Warn(레벨 유지).
                //   맵 로드 완료 시점은 기기 성능·회선에 좌우되고 화면에 통지가 없다 → 축 B 운영.
                //   위와 같은 "조합 루트에서 얻어야 할 것을 못 얻었다" 사건이므로 같은 키를 쓴다.
                GameLog.Ops.Warn(LogEvent.ClientRpcGameServicesMissing, "Network", nameof(NetworkTileSync),
                                 "BroadcastTileChangeClientRpc: HexGrid 가 null 이다 — 맵 로드 전일 수 있다",
                                 $"Request=TileSync, Q={q}, R={r}");
            }

            // 2. GameEvents 재발행 → HexTileView가 자동으로 색상 갱신
            GameEvents.OnTileOwnerChanged.OnNext(new TileOwnerChangedEvent(coord, newOwner));

            // [개발] 타일이 점령될 때마다 찍히는 고빈도 로그(LogRules 1.14 금지 8).
            //   Dev 라서 릴리스에서는 호출과 문자열 보간까지 통째로 사라진다(1.7).
            GameLog.Dev.Info("Network", nameof(NetworkTileSync), "타일 동기화 수신",
                             $"Q={q}, R={r}, Team={newOwner}");
        }
    }
}
