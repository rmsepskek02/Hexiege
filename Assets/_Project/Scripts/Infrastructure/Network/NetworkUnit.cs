// ============================================================================
// NetworkUnit.cs
// 유닛 프리팹 루트에 부착하는 NetworkBehaviour.
// 서버에서 unitId를 NetworkVariable<int>로 설정하면,
// 클라이언트가 OnNetworkSpawn()에서 이를 읽어 UnitView를 초기화한다.
//
// 타이밍 문제 해결:
//   NGO가 클라이언트에 프리팹을 자동 생성하는 시점과
//   SpawnUnitClientRpc로 UnitData가 도착하는 시점이 다를 수 있음.
//   → NetworkVariable<int>로 unitId를 즉시 동기화하여,
//     OnNetworkSpawn() 시점에 unitId를 읽을 수 있도록 보장.
//
// 초기화 흐름:
//   [서버]
//   1. UnitFactory.CreateUnitObject() → Instantiate → NetworkObject.Spawn()
//   2. NetworkUnit.SetUnitId(unitId) → NetworkVariable에 값 설정
//   3. SpawnUnitClientRpc로 UnitData 생성에 필요한 추가 정보 전송
//
//   [클라이언트]
//   1. NGO가 자동으로 프리팹 Instantiate + OnNetworkSpawn() 호출
//   2. OnNetworkSpawn()에서 unitId 확인:
//      - 이미 값이 있으면 즉시 등록
//      - 없으면 _unitId.OnValueChanged 콜백을 등록하여 값 도착 시 등록
//   3. SpawnUnitClientRpc 수신 시 UnitData 생성 + UnitView 초기화
//      → RegisterClientUnit()으로 UnitView를 UnitFactory에 등록
//
// LateUpdate 역할:
//   Red 클라이언트 전용 좌표/회전 보정.
//   NetworkTransform이 동기화한 서버(Blue) 도메인 좌표와 Y축 회전을
//   ViewConverter로 반전하여 Red팀 뷰에 맞게 보정.
//   - Position: X, Z를 맵 중심 기준으로 반전.
//   - Rotation: Y축에 +180° 적용 (시각적 이동 방향과 일치하도록).
//
// 배치:
//   유닛 프리팹 루트 오브젝트에 부착.
//   에디터 스크립트(AddNetworkTransformToPrefabs)가 자동 추가.
//
// Infrastructure 레이어 — NetworkBehaviour 사용 허용.
// ============================================================================

using Unity.Netcode;
using UnityEngine;
using Hexiege.Core;
using Hexiege.Application;
using Hexiege.Presentation;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// [Phase 2] 유닛 애니메이션 상태 — NetworkVariable로 서버→클라이언트 동기화되는 "값".
    ///
    /// 왜 이 enum이 필요한가 (초급자용 설명):
    ///   기존에는 걷기/공격 전환을 "상태가 바뀌는 순간 딱 1번 쏘는 신호(엣지 트리거 RPC)"로
    ///   전달했다. 그 신호가 도착하는 순간 클라이언트 유닛이 아직 준비(구독)되지 않았으면
    ///   신호를 놓치고, 이후 다시 알려주는 신호가 없어 틀린 애니메이션에 갇혔다(스폰 레이스).
    ///   → 애니메이션 상태를 "현재 값 자체"로 공유하면, 유닛이 늦게 스폰되어도 스폰되는 순간
    ///     서버의 현재 값을 즉시 읽어 올바른 애니메이션을 재생하므로 신호를 놓칠 수 없다.
    ///
    /// byte로 인코딩하여 NetworkVariable&lt;byte&gt;에 저장한다(직렬화가 가장 안전한 형태).
    /// 사망(IsDead)은 기존 IsDead bool + Despawn 경로가 담당하므로 여기에 포함하지 않는다.
    /// </summary>
    public enum UnitAnimState : byte
    {
        /// <summary> 아직 상태가 설정되지 않음(스폰 직후 기본값). </summary>
        None = 0,
        /// <summary> 이동(걷기) 애니메이션. </summary>
        Walk = 1,
        /// <summary> 공격 애니메이션. </summary>
        Attack = 2
    }

    /// <summary>
    /// 유닛 프리팹의 네트워크 동기화 컴포넌트.
    /// unitId를 NetworkVariable로 서버→클라이언트 동기화.
    /// 클라이언트 OnNetworkSpawn에서 UnitView 초기화를 위한 연결 지점 역할.
    /// Red 클라이언트에서는 LateUpdate에서 위치/회전을 반전 보정.
    /// </summary>
    public class NetworkUnit : NetworkBehaviour
    {
        // ====================================================================
        // NetworkVariable — unitId 동기화
        // ====================================================================

        /// <summary>
        /// 서버가 설정하고 모든 클라이언트가 읽는 유닛 Id.
        /// -1은 아직 설정되지 않은 초기 상태를 의미.
        /// ReadPermission: Everyone — 모든 클라이언트가 읽을 수 있음.
        /// WritePermission: Server — 서버만 값을 설정할 수 있음.
        /// </summary>
        private NetworkVariable<int> _unitId = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        /// <summary> 현재 동기화된 유닛 Id. -1이면 아직 미설정. </summary>
        public int UnitId => _unitId.Value;

        /// <summary>
        /// [Phase 2] 서버가 쓰고 모든 클라이언트가 읽는 유닛 애니메이션 상태(byte 인코딩).
        /// 저장값은 UnitAnimState를 byte로 캐스팅한 것. 초기값 None(0).
        /// ReadPermission: Everyone / WritePermission: Server — _unitId와 완전히 동일한 관례.
        ///
        /// [엣지 트리거 RPC 대비 핵심 장점 — 스폰 레이스 구조적 해소]
        ///   StartWalkAnimationClientRpc / StartCombatClientRpc는 "상태가 바뀌는 순간 1회"만 쏘므로
        ///   그 순간 클라이언트가 아직 이벤트 구독 전이면 신호가 유실되어 틀린 애니메이션에 갇혔다.
        ///   NetworkVariable은 "현재 값"을 항상 보관하므로 클라이언트가 스폰(OnNetworkSpawn)하는
        ///   순간 현재 값을 즉시 읽어(ApplySpawnAnimState) 적용할 수 있어 유실이 불가능하다.
        ///
        /// [중복 전송 걱정 없음 — 애니메이션 목적의 중복 방지 가드가 불필요해짐]
        ///   NGO는 값이 실제로 바뀔 때만 델타를 전송한다(같은 값을 다시 써도 전송하지 않음).
        ///   따라서 서버가 매 틱 같은 상태를 다시 써도 네트워크 비용이 없고 중복 적용도 없다.
        /// </summary>
        private NetworkVariable<byte> _animState = new NetworkVariable<byte>(
            (byte)UnitAnimState.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        /// <summary>
        /// [Phase 2] 같은 GameObject의 UnitView 캐시 — 애니메이션 상태 적용 시 메서드 호출용.
        /// NetworkUnit → 같은 프리팹 내 UnitView 참조는 기존 관례다(OnNetworkDespawn에서도 GetComponent 사용).
        /// 클라이언트에서만 사용한다(호스트/서버는 Animator를 직접 제어하므로 적용하지 않음).
        /// </summary>
        private UnitView _unitView;

        /// <summary>
        /// 클라이언트에서 UnitView 초기화가 완료되었는지 여부.
        /// SpawnUnitClientRpc에서 중복 초기화를 방지하기 위해 사용.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 게임 서비스 참조. Bootstrap에 직접 의존하지 않도록 IGameServices로 추상화.
        /// OnNetworkSpawn에서 1회만 획득하여 캐시하고 이후에는 재탐색 없이 사용한다.
        /// RegisterToFactory에서 반복 접근을 방지하기 위한 캐시.
        /// </summary>
        private IGameServices _services;

        // ====================================================================
        // 서버 전용 API
        // ====================================================================

        /// <summary>
        /// 서버에서 유닛 스폰 후 호출하여 unitId를 NetworkVariable에 설정.
        /// 이 값은 NGO가 자동으로 모든 클라이언트에 동기화.
        /// UnitFactory.CreateUnitObject()에서 NetworkObject.Spawn() 이후 호출.
        /// </summary>
        /// <param name="unitId">서버에서 발급된 유닛 Id (UnitData.Id)</param>
        public void SetUnitId(int unitId)
        {
            _unitId.Value = unitId;
        }

        /// <summary>
        /// [Phase 2] 서버에서 유닛 애니메이션 상태를 설정한다(서버 권위).
        /// NetworkCombatController의 서버 전용 핸들러(Walk/Attack 진입 지점)에서 호출한다.
        ///
        /// 같은 값을 다시 써도 NGO가 전송하지 않으므로(값 동일) 매 틱 호출해도 안전하며,
        /// 이 특성 덕분에 애니메이션 목적의 중복 방지 가드(예: _combatAnimationSent)가 불필요하다.
        /// (단, _combatAnimationSent는 데미지/타겟 RPC를 게이팅하는 별개의 기능 가드로 그대로 유지된다.)
        /// </summary>
        /// <param name="state">설정할 애니메이션 상태(Walk / Attack 등).</param>
        public void SetAnimState(UnitAnimState state)
        {
            // 방어적 가드 — WritePermission=Server이므로 클라이언트에서 호출되면 무시.
            if (!IsServer) return;

            // [MOVESYNC-LOG] 애니메이션 상태 쓰기(서버) — 클라이언트 수신/적용 로그와 대조해
            // "유실 0"을 판정하는 기준선. 검증 후 이 마커는 제거한다.
            if (MoveAnimSyncLog.Enabled)
                MoveAnimSyncLog.Info("Move/NetworkUnit",
                    $"AnimState 쓰기(서버) | unit={_unitId.Value}, state={state}");

            _animState.Value = (byte)state;
        }

        // ====================================================================
        // NetworkBehaviour 생명주기
        // ====================================================================

        /// <summary>
        /// 네트워크 스폰 시 호출.
        /// 서버: 아무 작업 없음 (UnitFactory에서 직접 초기화 완료).
        /// 클라이언트: unitId가 이미 동기화되었으면 즉시 등록,
        ///            아니면 OnValueChanged 콜백을 등록하여 값이 도착할 때 등록.
        ///
        /// OnValueChanged 콜백 방식:
        ///   NetworkVariable의 값이 변경되면 NGO가 자동으로 콜백을 호출.
        ///   폴링(매 프레임 확인)보다 효율적이고, 값 변경 즉시 반응하므로
        ///   Walk RPC가 도착하기 전에 딕셔너리 등록을 완료할 수 있음.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // 서버는 UnitFactory.CreateUnitObject()에서 이미 모든 초기화 완료
            if (IsServer) return;

            // 클라이언트 측에서만 RegisterToFactory가 호출되므로, 클라이언트 경로에서만 캐시한다.
            // GameServicesLocator에서 꺼내므로 FindFirstObjectByType<GameBootstrapper>() 불필요.
            _services = GameServicesLocator.Current;

            // ────────────────────────────────────────────────────────────────
            // [Phase 2] 애니메이션 상태 레벨 동기화 — 클라이언트 전용.
            //   1) 값 변경 콜백 구독: 서버가 상태를 바꿀 때마다 OnAnimStateChanged가 호출된다.
            //   2) 스폰 시 현재 값 즉시 적용: 이 유닛이 스폰되기 "전에" 서버가 이미 상태를 설정했다면
            //      OnValueChanged로는 다시 통지되지 않으므로, 지금 현재 값을 직접 읽어 적용한다.
            //      → 이것이 스폰 레이스(구독 전 신호 도착)를 구조적으로 없애는 핵심이다.
            //   (이 코드는 클라이언트 경로에서만 실행된다 — 위에서 IsServer일 때 이미 return했다.)
            // ────────────────────────────────────────────────────────────────
            _animState.OnValueChanged += OnAnimStateChanged;
            ApplySpawnAnimState();

            // 클라이언트: unitId가 이미 설정되었으면 즉시 딕셔너리에 등록
            if (_unitId.Value != -1)
            {
                RegisterToFactory(_unitId.Value);
                return;
            }

            // unitId가 아직 도착하지 않았으면 OnValueChanged 콜백으로 대기.
            // NGO가 서버에서 NetworkVariable 값이 변경되면 이 콜백을 자동 호출.
            // 폴링 코루틴 대신 콜백을 사용하여 불필요한 매 프레임 체크를 제거.
            _unitId.OnValueChanged += OnUnitIdReceived;
        }

        /// <summary>
        /// NetworkVariable(_unitId)의 값이 변경되었을 때 NGO가 호출하는 콜백.
        /// 새 값이 -1이 아니면(유효한 unitId) UnitFactory에 등록하고 콜백을 해제.
        /// 콜백 해제를 하지 않으면 이후 값 변경 시 중복 등록이 발생할 수 있으므로 반드시 해제.
        /// </summary>
        /// <param name="previousValue">변경 전 값 (보통 -1, 초기 상태).</param>
        /// <param name="newValue">변경 후 값 (서버가 설정한 unitId).</param>
        private void OnUnitIdReceived(int previousValue, int newValue)
        {
            if (newValue == -1) return;

            // 유효한 unitId 도착 → UnitFactory 딕셔너리에 등록
            RegisterToFactory(newValue);

            // 등록 완료 후 콜백 해제 — 1회만 실행하면 되므로 더 이상 구독할 필요 없음
            _unitId.OnValueChanged -= OnUnitIdReceived;
        }

        // ====================================================================
        // [Phase 2] 애니메이션 상태 적용 (클라이언트 전용)
        // ====================================================================

        /// <summary>
        /// [Phase 2] 스폰 시 현재 애니메이션 상태 값을 즉시 적용한다.
        /// 값이 None(미설정)이면 아무것도 하지 않고, 이후 값이 설정되면 OnAnimStateChanged가 적용한다.
        /// "구독 전에 도착한 신호가 유실되던" 엣지 트리거 구조의 스폰 레이스를 여기서 구조적으로 없앤다.
        /// </summary>
        private void ApplySpawnAnimState()
        {
            byte current = _animState.Value;
            if (current == (byte)UnitAnimState.None) return;

            // [MOVESYNC-LOG] 스폰 시 초기값 적용(클라) — 재검증 시 "유실 0" 판정용.
            if (MoveAnimSyncLog.Enabled)
                MoveAnimSyncLog.Info("Move/NetworkUnit",
                    $"AnimState 초기적용(스폰) | unit={_unitId.Value}, state={(UnitAnimState)current}");

            ApplyAnimState(current);
        }

        /// <summary>
        /// [Phase 2] 서버가 애니메이션 상태를 바꿀 때 NGO가 호출하는 콜백(클라이언트 전용).
        /// 새 값으로 애니메이션을 즉시 적용한다.
        /// </summary>
        /// <param name="previousValue">변경 전 상태(byte).</param>
        /// <param name="newValue">변경 후 상태(byte).</param>
        private void OnAnimStateChanged(byte previousValue, byte newValue)
        {
            // [MOVESYNC-LOG] 상태 변경 수신(클라) — 서버 쓰기 로그와 대조해 유실 판별.
            if (MoveAnimSyncLog.Enabled)
                MoveAnimSyncLog.Info("Move/NetworkUnit",
                    $"AnimState 수신(변경) | unit={_unitId.Value}, prev={(UnitAnimState)previousValue}, new={(UnitAnimState)newValue}");

            ApplyAnimState(newValue);
        }

        /// <summary>
        /// [Phase 2] 애니메이션 상태 값을 같은 GameObject의 UnitView에 적용한다(클라이언트 전용).
        ///
        /// 호스트/서버는 이 경로를 타지 않는다 — OnNetworkSpawn에서 IsServer일 때 이미 return하므로
        /// _animState.OnValueChanged를 애초에 구독하지 않고, 스폰 시 적용도 하지 않는다.
        /// 호스트의 애니메이션은 서버 로직(MoveAlongPathV3의 Walk CrossFade, StartCombatAnimation의
        /// Attack CrossFade)이 Animator를 직접 제어하므로 여기서 중복 적용하면 안 된다.
        ///
        /// UnitView는 같은 프리팹 루트의 컴포넌트다(OnNetworkDespawn에서도 GetComponent로 접근).
        /// _animator/Initialize보다 먼저 호출될 수 있으므로 UnitView 쪽 메서드가 animator를 지연 초기화한다.
        /// </summary>
        /// <param name="state">적용할 애니메이션 상태(byte).</param>
        private void ApplyAnimState(byte state)
        {
            if (_unitView == null) _unitView = GetComponent<UnitView>();
            if (_unitView == null) return;

            switch ((UnitAnimState)state)
            {
                case UnitAnimState.Walk:
                    _unitView.StartWalkAnimation();
                    break;
                case UnitAnimState.Attack:
                    _unitView.PlayAttackAnimation();
                    break;
                // None: 애니메이션 변경 없음(초기 상태).
            }
        }

        /// <summary>
        /// 네트워크 디스폰 시 호출.
        /// OnValueChanged 콜백이 아직 등록되어 있을 수 있으므로 안전하게 해제.
        /// 예: unitId가 도착하기 전에 오브젝트가 Despawn된 경우
        ///     콜백이 남아있으면 파괴된 오브젝트에 접근하는 버그 발생 가능.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            // 안전망: Despawn 시 혹시 남아있는 콜백 해제.
            // 이미 해제된 상태여도 -= 연산은 에러 없이 무시됨.
            _unitId.OnValueChanged -= OnUnitIdReceived;

            // [Phase 2] 애니메이션 상태 콜백도 해제(클라이언트에서만 구독했으므로 서버에선 no-op).
            // 남겨두면 파괴된 오브젝트에 접근하는 버그로 이어질 수 있다.
            _animState.OnValueChanged -= OnAnimStateChanged;

            // 클라이언트 전용: 사망 이펙트를 여기서 재생한다.
            // OnNetworkDespawn은 GO가 파괴되기 직전에 반드시 호출되므로
            // EntityDiedClientRpc보다 NGO Despawn이 먼저 도착하는 경쟁 조건에서도 이펙트가 보장된다.
            // 서버는 UnitView의 OnUnitDied 구독자에서 이미 재생하므로 클라이언트만 처리한다.
            if (!IsServer)
            {
                var unitView = GetComponent<UnitView>();

                if (unitView != null && unitView.UnitData != null)
                {
                    EffectManager.Instance?.PlayUnitDeath(
                        unitView.UnitData.Type,
                        transform.position);                                // VFX
                    AudioManager.Instance?.PlayUnitDeathSfx(unitView.UnitData.Type); // SFX (규칙 15 — VFX와 짝)
                }
            }

            base.OnNetworkDespawn();
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

        /// <summary>
        /// 클라이언트에서 NGO가 자동 생성한 이 GameObject를
        /// UnitFactory._unitObjects 딕셔너리에 등록.
        /// 이후 SpawnUnitClientRpc에서 UnitData를 생성하고 UnitView를 초기화할 때,
        /// UnitFactory.GetUnitObject(unitId)로 이 GameObject를 찾을 수 있게 됨.
        /// </summary>
        /// <param name="unitId">서버에서 발급된 유닛 Id</param>
        private void RegisterToFactory(int unitId)
        {
            // IGameServices를 통해 IUnitFactory 접근.
            // OnNetworkSpawn에서 미리 캐시해 둔 참조를 사용 (매번 씬 탐색하지 않음).
            var services = _services;
            if (services == null)
            {
                Debug.LogWarning($"[NetworkUnit] GameServicesLocator에 IGameServices가 등록되지 않았습니다. UnitId={unitId}");
                return;
            }

            IUnitFactory unitFactory = services.GetUnitFactory();
            if (unitFactory == null)
            {
                Debug.LogWarning($"[NetworkUnit] UnitFactory가 null입니다. UnitId={unitId}");
                return;
            }

            // 이 GameObject를 UnitFactory 딕셔너리에 등록
            // → SpawnUnitClientRpc에서 GetUnitObject(unitId)로 조회 가능
            unitFactory.RegisterUnitObject(unitId, gameObject);

            Debug.Log($"[NetworkUnit] 클라이언트: UnitFactory에 등록 완료. UnitId={unitId}");
        }

        /// <summary>
        /// SpawnUnitClientRpc에서 UnitView 초기화 완료 후 호출.
        /// 중복 초기화 방지 플래그 설정.
        /// </summary>
        public void MarkInitialized()
        {
            IsInitialized = true;
        }

        // ====================================================================
        // Red 클라이언트 좌표/회전 보정
        // ====================================================================

        /// <summary>
        /// 클라이언트 전용 LateUpdate 처리:
        ///
        /// Red 클라이언트 좌표/회전 보정:
        ///   NetworkTransform이 동기화한 서버(Blue) 도메인 좌표와 Y축 회전을
        ///   ViewConverter로 반전하여 Red팀 뷰에 맞게 보정.
        ///   Position: X, Z를 맵 중심 기준으로 반전.
        ///   Rotation: Y축에 +180° 적용 (시각적 이동 방향과 일치하도록).
        ///
        ///   예시: Blue팀에서 "남동쪽(SE=150°)"을 바라보는 유닛은
        ///         Red팀에서는 "북서쪽(NW=330°)"을 바라봐야 시각적으로 올바름.
        ///         150° + 180° = 330° → 정확히 반대 방향.
        ///
        /// Blue 클라이언트(Host)에서는 서버가 직접 제어하므로 보정 불필요.
        /// 싱글플레이에서도 서버로 동작하므로 처리 불필요.
        ///
        /// 타이밍:
        ///   NetworkTransform이 Update()에서 서버 값을 적용한 뒤,
        ///   LateUpdate()에서 Red 보정을 덮어씌움.
        ///   이 순서가 보장되므로 매 프레임 올바른 Red 뷰가 렌더링됨.
        /// </summary>
        private void LateUpdate()
        {
            // 서버(Blue Host)와 싱글플레이에서는 처리 불필요
            if (IsServer || !NetworkContext.IsNetworkActive) return;

            // Red 클라이언트: 서버(Blue) 좌표/회전을 Red 뷰로 반전
            if (ViewConverter.IsFlipped)
            {
                // Position 반전: 맵 중심 기준으로 X, Z를 뒤집어 Red팀 관점의 위치로 변환
                transform.position = ViewConverter.ToView(transform.position);

                // Rotation 반전: 서버(Blue)의 Y축 회전에 +180° 적용
                // Blue팀에서 "남쪽(SE=150°)"을 바라보는 유닛은
                // Red팀에서는 "북쪽(NW=330°)"을 바라봐야 시각적으로 올바름
                Vector3 euler = transform.eulerAngles;
                euler.y = (euler.y + 180f) % 360f;
                transform.rotation = Quaternion.Euler(euler);
            }
        }
    }
}
