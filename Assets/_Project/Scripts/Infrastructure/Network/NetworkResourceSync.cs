// ============================================================================
// NetworkResourceSync.cs
// 팀별 골드(자원) 현황을 서버→모든 클라이언트에 동기화하는 NetworkBehaviour.
//
// 역할:
//   - 서버: GameEvents.OnResourceChanged 구독 → NetworkVariable 갱신
//   - 클라이언트: NetworkVariable.OnValueChanged 콜백 → 로컬 ResourceUseCase 보정
//              → GameEvents.OnResourceChanged 재발행 (HUD 즉시 갱신)
//
// 동기화 방식:
//   NetworkVariable<int> _blueGold / _redGold
//   서버가 값을 쓰면 NGO가 자동으로 모든 클라이언트에 전파.
//   클라이언트는 OnValueChanged 콜백에서 로컬 ResourceUseCase 상태를 보정.
//
// 로컬 UseCase 보정 방법:
//   ResourceUseCase.GetGold(team)으로 현재값 읽기
//   → 서버 값과의 차이를 AddGold()로 더하거나, SpendGold()로 빼기.
//   → 이 과정에서 OnResourceChanged 이벤트가 발행되어 HUD가 갱신됨.
//
// 주의:
//   - 싱글플레이 시 IsSpawned=false → 이벤트 구독 안 됨 → 영향 없음
//   - 클라이언트에서 NetworkVariable 콜백으로 이미 UseCase를 갱신하므로
//     서버의 이벤트가 클라이언트에 이중 발행될 수 있음.
//     단, 서버는 IsServer 체크로 콜백에서 스킵하므로 중복 없음.
//
// 배치:
//   씬에 빈 GameObject "NetworkResourceSync" 생성 후 이 컴포넌트 + NetworkObject 부착.
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
    /// 팀별 골드를 NetworkVariable로 동기화하는 NetworkBehaviour.
    /// 서버의 ResourceUseCase 상태를 모든 클라이언트에 전파.
    /// </summary>
    public class NetworkResourceSync : NetworkBehaviour
    {
        // ====================================================================
        // NetworkVariable — 서버만 쓰고, 모든 클라이언트가 읽음
        // ====================================================================

        /// <summary> Blue 팀 골드. 서버가 갱신, 클라이언트가 동기화됨. </summary>
        private readonly NetworkVariable<int> _blueGold = new NetworkVariable<int>(
            value: 0,
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        /// <summary> Red 팀 골드. 서버가 갱신, 클라이언트가 동기화됨. </summary>
        private readonly NetworkVariable<int> _redGold = new NetworkVariable<int>(
            value: 0,
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary> 서버의 OnResourceChanged 구독 해제 토큰. </summary>
        private IDisposable _resourceChangedSubscription;

        /// <summary>게임 서비스 참조. ResourceUseCase 접근에 사용. Bootstrap에 직접 의존하지 않도록 IGameServices로 추상화.</summary>
        private IGameServices _services;

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 호출.
        /// 서버: OnResourceChanged 구독 + NetworkVariable OnValueChanged 등록.
        /// 클라이언트: NetworkVariable OnValueChanged 등록만.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // GameServicesLocator에서 IGameServices 획득 (Bootstrap 직접 참조 불필요)
            _services = GameServicesLocator.Current;
            if (_services == null)
            {
                // [운영] 축 A=Warn / 축 B=운영 — 배치 1-A 와 같은 판정, 같은 키를 쓴다
                //   (선례: NetworkHealthSync:67 · NetworkBuildingController:58 · NetworkProductionController:72).
                //   축 A: 여기서 return 하지 않고 스폰이 계속되므로 아직 기능이 죽지 않았다 → Warn.
                //   축 B: NGO 스폰 타이밍은 회선 상태에 좌우돼 플레이어 기기에서만 어긋날 수 있고(①),
                //         플레이어에게 알리는 경로가 없다(②) → 운영.
                GameLog.Ops.Warn(LogEvent.NetworkControllerSpawnedWithoutGameServices,
                                 "Network", nameof(NetworkResourceSync),
                                 "스폰 시점에 GameServicesLocator 에서 IGameServices 를 얻지 못했다",
                                 $"IsServer={IsServer}");
            }

            // 서버: 게임 이벤트 구독 → NetworkVariable 갱신
            if (IsServer)
            {
                SubscribeResourceChanged();
                // [개발] 의도된 흐름의 진입 흔적 → Info / 개발.
                GameLog.Dev.Info("Network", nameof(NetworkResourceSync), "서버 모드로 골드 동기화 시작");
            }

            // 모든 인스턴스: NetworkVariable 변경 콜백 등록
            // 서버 자신도 등록하지만, 콜백 내 IsServer 체크로 처리 스킵
            _blueGold.OnValueChanged += OnBlueGoldChanged;
            _redGold.OnValueChanged += OnRedGoldChanged;

            // [개발] 구독 완료 통보 → Info / 개발 (배치 1-A 선례: "스폰/디스폰/구독완료"는 전부 개발).
            GameLog.Dev.Info("Network", nameof(NetworkResourceSync), "NetworkVariable 콜백 등록 완료",
                             $"IsServer={IsServer}");
        }

        /// <summary>
        /// 네트워크 디스폰 시 호출.
        /// 이벤트 구독 및 NetworkVariable 콜백 해제.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            UnsubscribeResourceChanged();

            _blueGold.OnValueChanged -= OnBlueGoldChanged;
            _redGold.OnValueChanged -= OnRedGoldChanged;

            // [개발] 스폰 로그와 짝을 이루는 의도된 흐름 → Info / 개발.
            GameLog.Dev.Info("Network", nameof(NetworkResourceSync), "디스폰 — 구독 해제 완료");
        }

        // ====================================================================
        // 서버 전용: 이벤트 구독
        // ====================================================================

        /// <summary>
        /// 서버에서 OnResourceChanged 이벤트 구독.
        /// 골드 변경 시 해당 팀의 NetworkVariable을 갱신.
        /// </summary>
        private void SubscribeResourceChanged()
        {
            UnsubscribeResourceChanged();

            _resourceChangedSubscription = GameEvents.OnResourceChanged
                .Subscribe(OnResourceChangedOnServer);

            // [개발] 구독 완료 통보 → Info / 개발.
            GameLog.Dev.Info("Network", nameof(NetworkResourceSync), "OnResourceChanged 구독 완료");
        }

        /// <summary>
        /// 이벤트 구독 해제.
        /// </summary>
        private void UnsubscribeResourceChanged()
        {
            _resourceChangedSubscription?.Dispose();
            _resourceChangedSubscription = null;
        }

        /// <summary>
        /// 서버에서 골드 변경 이벤트 수신.
        /// 해당 팀의 NetworkVariable을 서버 값으로 업데이트.
        /// NGO가 자동으로 모든 클라이언트에 전파.
        /// </summary>
        private void OnResourceChangedOnServer(ResourceChangedEvent evt)
        {
            // 이 오브젝트가 아직 네트워크에 살아 있고, 내가 서버일 때만 진행한다.
            //   이 핸들러가 하는 일은 아래의 NetworkVariable 쓰기(_blueGold.Value / _redGold.Value)이고,
            //   NetworkVariable 쓰기도 RPC 와 마찬가지로 "네트워크가 살아 있을 때"를 전제한다.
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
            //      OnNetworkSpawn 의 IsServer 블록(103행) 안에서만 이루어지므로 원래도 서버에서만
            //      호출되던 자리다. 프로젝트 전체에서 가드 모양이 갈리지 않도록 형태를 통일한다.
            if (!IsSpawned || !IsServer) return;

            switch (evt.Team)
            {
                case TeamId.Blue:
                    _blueGold.Value = evt.Gold;
                    break;
                case TeamId.Red:
                    _redGold.Value = evt.Gold;
                    break;
                default:
                    // Neutral 팀은 골드 없음 → 무시
                    break;
            }
        }

        // ====================================================================
        // NetworkVariable 변경 콜백 — 클라이언트에서 UI 갱신
        // ====================================================================

        /// <summary>
        /// Blue 팀 골드 NetworkVariable 변경 감지.
        /// 클라이언트에서만 로컬 ResourceUseCase 보정 + 이벤트 발행.
        /// 서버는 이미 로컬에서 처리됐으므로 스킵.
        /// </summary>
        private void OnBlueGoldChanged(int previousValue, int newValue)
        {
            // 서버는 이미 UseCase에서 이벤트 처리됨 → 중복 방지
            if (IsServer) return;

            ApplyGoldToLocalUseCase(TeamId.Blue, newValue);
        }

        /// <summary>
        /// Red 팀 골드 NetworkVariable 변경 감지.
        /// 클라이언트에서만 로컬 ResourceUseCase 보정 + 이벤트 발행.
        /// 서버는 스킵.
        /// </summary>
        private void OnRedGoldChanged(int previousValue, int newValue)
        {
            if (IsServer) return;

            ApplyGoldToLocalUseCase(TeamId.Red, newValue);
        }

        /// <summary>
        /// 클라이언트의 로컬 ResourceUseCase 골드를 서버 값으로 보정.
        ///
        /// ResourceUseCase에는 SetGold() 메서드가 없으므로
        /// 현재값과의 차이를 AddGold() / SpendGold()로 보정.
        /// AddGold/SpendGold 내부에서 OnResourceChanged 이벤트 발행 → HUD 갱신.
        ///
        /// 맵 로드 전(ResourceUseCase가 null)이면 무시.
        /// </summary>
        /// <param name="team">보정할 팀</param>
        /// <param name="serverGold">서버의 현재 골드 값</param>
        private void ApplyGoldToLocalUseCase(TeamId team, int serverGold)
        {
            // _services는 OnNetworkSpawn에서 1회 캐시 — 보호적 재탐색은 하지 않음.
            if (_services == null) return;

            ResourceUseCase resource = _services.GetResource();
            if (resource == null)
            {
                // 맵이 아직 로드되지 않은 상태 — 로그 없이 무시
                return;
            }

            int currentGold = resource.GetGold(team);
            int diff = serverGold - currentGold;

            if (diff == 0) return; // 이미 동기화된 상태

            // AddGold(team, diff)는 내부적으로 _gold[team] += diff를 수행.
            // diff가 양수이면 골드 증가, 음수이면 골드 감소.
            // 어느 방향이든 OnResourceChanged 이벤트가 발행되어 HUD가 갱신됨.
            resource.AddGold(team, diff);

            // [개발] 축 A=Info / 축 B=개발.
            //   축 A: 클라이언트 골드를 서버 값으로 맞추는 것은 이 클래스가 하기로 되어 있는 정상 흐름이다.
            //   축 B: 값이 바뀔 때마다 늘 흐르는 동기화 로그라 라이브 지표로서의 가치가 없다(축 B ②).
            //   ⚠️ diff == 0 이면 위에서 이미 return 하므로 이 줄은 "값이 실제로 바뀐 순간"에만 찍힌다
            //      — 매 틱 로깅 금지(LogRules 1.14 금지 8)에 걸리지 않는다.
            GameLog.Dev.Info("Network", nameof(NetworkResourceSync), "클라이언트 골드를 서버 값으로 보정",
                             $"Team={team}, PreviousGold={currentGold}, ServerGold={serverGold}, Diff={diff}");
        }
    }
}
