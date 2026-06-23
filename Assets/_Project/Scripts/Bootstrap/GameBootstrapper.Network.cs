// ============================================================================
// GameBootstrapper.Network.cs (partial 파일)
//
// 본 파일은 GameBootstrapper의 "네트워크 게임 진입 + eager 재경로" 코드를
// 분리해 둔 partial이다. Inspector 필드 / 생명주기 메서드는 메인 파일에 있다.
//
// 담당 영역:
//   1. StartNetworkGame                       — NetworkGameFlow.StartGameClientRpc()가 호출
//   2. SetupEagerRepathOnBuildingChanges      — 건물 변경 시 모든 유닛 경로 즉시 재계산 트리거 구독
//   3. RepathAllAliveUnits                    — 살아있는 모든 유닛에 OnPathInvalidated() 호출
//
// 규칙:
//   * [SerializeField] 필드를 본 파일에 추가하지 않는다 — Inspector 추적성 보장.
//   * Unity 생명주기 메서드를 본 파일에 두지 않는다 — 중복 정의 방지.
// ============================================================================

using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Bootstrap
{
    public partial class GameBootstrapper
    {
        // ====================================================================
        // 네트워크 게임 진입점
        // ====================================================================

        /// <summary>
        /// 네트워크 모드에서 게임을 시작. NetworkGameFlow.StartGameClientRpc()가 호출.
        /// FlatTop 맵을 로드하고, 로컬 플레이어 팀에 맞춰 ViewConverter를 설정.
        /// Blue 팀(Host) → 기본 뷰, Red 팀(Client) → 맵 중심 기준 반전 뷰.
        /// </summary>
        /// <param name="localTeam">이 클라이언트에 할당된 팀.</param>
        public void StartNetworkGame(TeamId localTeam)
        {
            // NetworkGameFlow가 재스폰되어 중복 호출되는 경우 방지
            if (_networkGameStarted)
            {
                Debug.LogWarning("[Network] StartNetworkGame: 이미 시작됨. 중복 호출 무시.");
                return;
            }
            _networkGameStarted = true;

            Debug.Log($"[Network] StartNetworkGame 호출. 로컬 팀={localTeam}");

            // ViewConverter를 LoadMap() 이전에 설정.
            // LoadMap() 내부에서 PlaceCastles(), RenderGrid() 등이 ViewConverter.ToView()를 호출하므로
            // 반드시 먼저 초기화해야 Red팀 건물/타일이 올바른 반전 위치에 렌더링됨.
            //
            // 주의: HexMetrics.GridCenter()는 HexMetrics.Orientation / TileWidth / TileHeight를
            // 참조하므로, GridCenter() 호출 전에 반드시 FlatTop 설정을 HexMetrics에 적용해야 함.
            // ApplyConfig()는 LoadMap() 내부에서 실행되므로, 여기서 먼저 적용.
            if (_config != null)
            {
                OrientationConfig oc = _config.FlatTop;

                // GridCenter() 호출 전에 HexMetrics를 FlatTop 기준으로 사전 설정.
                // 이렇게 하지 않으면 기본값(PointyTop)으로 중심 좌표가 계산되어
                // Red팀 반전 위치가 한 칸 이상 틀어지는 버그가 발생.
                HexMetrics.Orientation = HexOrientation.FlatTop;
                HexOrientationContext.Current = HexOrientation.FlatTop;
                HexMetrics.TileWidth = oc.TileWidth;
                HexMetrics.TileHeight = oc.TileHeight;

                Vector3 mapCenter = HexMetrics.GridCenter(oc.GridWidth, oc.GridHeight);
                bool isRed = (localTeam == TeamId.Red);
                ViewConverter.Setup(isRed, mapCenter);
                Debug.Log($"[Network] ViewConverter 사전 설정 완료. isRed={isRed}, mapCenter={mapCenter}");
            }

            // 맵 로드 (FlatTop 고정) — ViewConverter 설정 완료 후 실행
            // LoadMap() 내부의 SetupCamera()가 카메라를 맵 중심에 배치함.
            // 싱글플레이와 동일하게 맵 중심에서 시작하도록 팀별 카메라 이동은 수행하지 않음.
            LoadMap(HexOrientation.FlatTop);
        }

        // ====================================================================
        // eager 경로 재계산 트리거 설정
        // ====================================================================

        /// <summary>
        /// 건물 배치 / 건물 사망 이벤트를 구독하여
        /// 살아있는 모든 유닛에게 OnPathInvalidated() 호출 → 동일 destination으로
        /// 새 경로를 받아 이동을 즉시 재시작시킨다.
        ///
        /// 구독 해제는 ClearAll() 또는 OnDestroy()에서 처리.
        /// 멀티플레이에서는 서버에서만 트리거되어야 하므로 가드 적용.
        /// </summary>
        private void SetupEagerRepathOnBuildingChanges()
        {
            // 기존 구독 정리 (재경기/맵 전환 시 중복 구독 방지).
            _eagerRepathSubscriptions?.Dispose();
            _eagerRepathSubscriptions = new CompositeDisposable();

            // 건물 배치: walkable이 false로 바뀌어 기존 경로가 부정확해질 수 있다.
            GameEvents.OnBuildingPlaced
                .Subscribe(_ => RepathAllAliveUnits())
                .AddTo(_eagerRepathSubscriptions);

            // 건물 사망만 walkable에 영향 → 그때만 재계산.
            // 유닛 사망(OnUnitDied)은 walkable과 무관하므로 구독 대상이 아니다.
            // OnBuildingDied는 건물 전용 강타입 이벤트라 별도 캐스트 분기가 필요 없다.
            GameEvents.OnBuildingDied
                .Subscribe(_ => RepathAllAliveUnits())
                .AddTo(_eagerRepathSubscriptions);
        }

        /// <summary>
        /// 모든 살아있는 유닛에 대해 OnPathInvalidated()를 호출.
        /// 서버(또는 싱글플레이)에서만 동작 — 클라이언트는 NetworkTransform이 결과를 동기화한다.
        /// </summary>
        private void RepathAllAliveUnits()
        {
            // 서버 가드 — 싱글(IsNetworkActive==false) 또는 Host(IsNetworkServer==true) 통과.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;
            if (_unitSpawn == null || _unitFactory == null) return;

            // _unitSpawn.Units는 IReadOnlyDictionary<int, UnitData>.
            // 살아있는 유닛만 처리 (사망한 유닛은 OnUnitDied 경로에서 별도 정리됨).
            foreach (var kv in _unitSpawn.Units)
            {
                UnitData unit = kv.Value;
                if (unit == null || !unit.IsAlive) continue;

                GameObject obj = _unitFactory.GetUnitObject(unit.Id);
                if (obj == null) continue;

                Hexiege.Presentation.UnitView view = obj.GetComponent<Hexiege.Presentation.UnitView>();
                if (view == null) continue;

                // UnitView 내부에서 destination 보유 여부와 NetworkContext 가드를 다시 한 번 체크.
                view.OnPathInvalidated();
            }
        }
    }
}
