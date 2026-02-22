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

        /// <summary> GameBootstrapper 참조. ResourceUseCase 접근에 사용. </summary>
        private Hexiege.Bootstrap.GameBootstrapper _bootstrapper;

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

            // GameBootstrapper 탐색 (ResourceUseCase 접근용)
            _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();
            if (_bootstrapper == null)
            {
                Debug.LogWarning("[Network] NetworkResourceSync: GameBootstrapper를 찾을 수 없습니다.");
            }

            // 서버: 게임 이벤트 구독 → NetworkVariable 갱신
            if (IsServer)
            {
                SubscribeResourceChanged();
                Debug.Log("[Network] NetworkResourceSync: 서버 모드로 골드 동기화 시작.");
            }

            // 모든 인스턴스: NetworkVariable 변경 콜백 등록
            // 서버 자신도 등록하지만, 콜백 내 IsServer 체크로 처리 스킵
            _blueGold.OnValueChanged += OnBlueGoldChanged;
            _redGold.OnValueChanged += OnRedGoldChanged;

            Debug.Log("[Network] NetworkResourceSync: NetworkVariable 콜백 등록 완료.");
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

            Debug.Log("[Network] NetworkResourceSync: 디스폰. 구독 해제 완료.");
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

            Debug.Log("[Network] NetworkResourceSync: OnResourceChanged 구독 완료.");
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
            if (_bootstrapper == null)
            {
                _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();
                if (_bootstrapper == null) return;
            }

            ResourceUseCase resource = _bootstrapper.GetResource();
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

            Debug.Log($"[Network] 골드 동기화. 팀={team}, 이전={currentGold}, 서버={serverGold}, 차이={diff}");
        }
    }
}
