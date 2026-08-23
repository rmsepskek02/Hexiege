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
                // [개발] ⚠️ 잠정 판정 — Warn + 개발.
                //   축 A: 중복 호출을 무시하고 게임은 그대로 진행된다 → 복구됨 → Warn.
                //   축 B: NetworkGameFlow 가 다시 스폰됐다는 신호이고, 그 수신 자체는
                //         NetworkGameFlow.StartGameClientRpc 의 개발 로그로도 확인된다.
                //         가드가 정상 동작해 피해가 없으므로 서버 집계 가치가 낮다고 보아 개발로 둔다.
                GameLog.Dev.Warn("Bootstrap", nameof(GameBootstrapper),
                                 "StartNetworkGame 중복 호출 — 이미 시작되어 무시한다",
                                 $"Team={localTeam}");
                return;
            }
            _networkGameStarted = true;

            // [개발] 네트워크 게임 진입 흐름 추적용. 결과(맵 로드)가 곧바로 화면에 나타난다.
            GameLog.Dev.Info("Bootstrap", nameof(GameBootstrapper), "StartNetworkGame 호출",
                             $"Team={localTeam}");

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
                //
                // [Phase 2 리팩토링] 기존 수동 설정 4줄을 ApplyConfig() 호출로 대체.
                // LoadMap() 내부에서도 동일한 ApplyConfig()가 실행되므로, 여기서 먼저
                // 동일 로직을 재사용해 중복을 제거한다. (HexMetrics 적용 → GridCenter
                // 계산 → ViewConverter.Setup → LoadMap 순서는 그대로 유지됨.)
                ApplyConfig(HexOrientation.FlatTop, oc);

                Vector3 mapCenter = HexMetrics.GridCenter(oc.GridWidth, oc.GridHeight);
                bool isRed = (localTeam == TeamId.Red);
                ViewConverter.Setup(isRed, mapCenter);
                // [개발] Red 팀 뷰 반전이 제대로 걸렸는지 확인하는 흐름 추적 로그.
                //   축 A: 정상 흐름 → Info / 축 B: 에디터 2인 테스트로 그대로 재현된다 → 개발.
                GameLog.Dev.Info("Bootstrap", nameof(GameBootstrapper), "ViewConverter 사전 설정 완료",
                                 $"IsRed={isRed}, MapCenter={mapCenter}");
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
