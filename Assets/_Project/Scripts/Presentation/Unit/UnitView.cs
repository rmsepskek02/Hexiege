// ============================================================================
// UnitView.cs
// 유닛의 비주얼(이동, 방향, 애니메이션 상태)을 담당하는 컴포넌트.
//
// [Phase 2] 3D 전환:
//   - FrameAnimator + SpriteRenderer 기반 → Animator(Mecanim) 기반으로 전면 교체
//   - flipX 방향 처리 → Y축 회전 (transform.rotation)으로 교체
//   - sortingOrder 관련 코드 제거
//   - UnitAnimationData(Sprite[] ScriptableObject)는 더 이상 사용하지 않음
//     → AnimatorController에서 상태/트랜지션을 관리
//
// [Phase 3] NetworkTransform Rotation 동기화:
//   - DORotate 보간 → 즉시 스냅(Quaternion.Euler)으로 전면 교체
//   - 서버에서 즉시 스냅한 rotation을 NetworkTransform이 클라이언트에 보간 전달
//   - 이중 보간 문제 해결: 서버 DORotate(0.3초) + NetworkTransform 보간(0.1초) = ~1초 딜레이
//     → 서버 즉시 스냅 + NetworkTransform 보간(0.1초)만 적용되어 자연스러운 회전
//   - 클라이언트 자체 회전 로직(LateUpdate 델타 기반) 전면 제거
//   - Red 클라이언트 rotation 보정은 NetworkUnit.LateUpdate()에서 일괄 처리
//
// 프리팹 구조 (3D 전환 후):
//   Unit_{Type} (GameObject)
//     ├─ MeshFilter + MeshRenderer (or SkinnedMeshRenderer)
//     ├─ Animator (AnimatorController 설정)
//     └─ UnitView (이 스크립트)
//
// Animator 제어 방식:
//   - Walk: Animator.Play(StateWalk) + speed 제어 (1=재생, 0=정지)
//   - Attack: Animator.Play(StateAttack) — 클립 length 만큼 대기
//   - IsDead (bool): 사망 여부 (Animator 트랜지션 조건)
//   - 방향: transform.rotation Y축으로 표현 (Animator 파라미터 불필요)
//
// HexDirection → Y축 회전 각도 (FlatTop 기준):
//   E=90, NE=30, NW=330, W=270, SW=210, SE=150
// PointyTop 기준:
//   E=90, NE=30, NW=330, W=270, SW=210, SE=150
//   (동일 — HexDirection 0~5가 동일한 시각적 회전을 가짐)
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, Coroutine, Animator).
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Hexiege.Domain;
using Hexiege.Core;
using Hexiege.Application;
using Hexiege.Infrastructure;

namespace Hexiege.Presentation
{
    public class UnitView : MonoBehaviour
    {
        // ====================================================================
        // Animator 파라미터 해시 (문자열 비교 방지)
        // ====================================================================

        // Animator.Play()에 사용할 스테이트 이름 해시
        private static readonly int StateWalk = Animator.StringToHash("Walk");
        private static readonly int StateAttack = Animator.StringToHash("Attack");
        // StateIdle 제거 — 이 게임에서 유닛은 항상 이동 또는 공격 중이므로 Idle 상태가 존재하지 않음.

        // 사망은 bool 파라미터로 유지 (Animator 트랜지션 조건)
        private static readonly int AnimIsDead = Animator.StringToHash("IsDead");

        // ====================================================================
        // HexDirection → Y축 회전 각도 매핑
        // NE(0)=60, E(1)=120, SE(2)=180, SW(3)=240, W(4)=300, NW(5)=0
        // FlatTop 헥스에서 각 방향의 실제 Unity 월드 각도 (atan2 기반).
        // 메시 자식 Y=0, _meshYOffset 제거 이후 기준값.
        // ====================================================================

        private static readonly float[] DirectionAngles = { 60f, 120f, 180f, 240f, 300f, 0f };
        // NE(0)=60, E(1)=120, SE(2)=180, SW(3)=240, W(4)=300, NW(5)=0
        // ====================================================================
        // Inspector 설정
        // ====================================================================

        /// <summary> Walk로 전환할 때 블렌딩 시간 (초). </summary>
        [SerializeField] private float _idleToWalkBlend = 0.10f;

        /// <summary> 어떤 상태에서 Attack으로 전환할 때 블렌딩 시간 (초). </summary>
        [SerializeField] private float _toAttackBlend = 0.08f;

        [Tooltip("Attack → Walk 전환 시 CrossFade 블렌드 시간 (초). 규칙 3: 0.10초")]
        [SerializeField] private float _attackToWalkBlend = 0.10f;

        // ====================================================================
        // 내부 참조
        // ====================================================================

        /// <summary> 이 유닛에 연결된 Domain 데이터. </summary>
        private UnitData _unitData;

        /// <summary> Animator — 상태 전환 및 애니메이션 재생. </summary>
        private Animator _animator;

        /// <summary> 전역 설정 (이동 속도 등). </summary>
        private GameConfig _config;

        /// <summary>
        /// 이동 UseCase 참조. 타일 도착 시 ProcessStep 호출용.
        /// GameBootstrapper에서 주입.
        /// </summary>
        private UnitMovementUseCase _movementUseCase;

        /// <summary> 전투 UseCase 참조. 이동 완료 후 자동 공격용. </summary>
        private UnitCombatUseCase _combatUseCase;

        /// <summary> 유닛 팩토리. 타겟 유닛의 실제 transform 조회용. </summary>
        private UnitFactory _unitFactory;

        /// <summary> 건물 팩토리. 타겟 건물의 실제 transform 조회용. </summary>
        private BuildingFactory _buildingFactory;

        /// <summary>
        /// 엔티티 월드 좌표 제공자.
        /// Phase 1(월드 좌표 직선 추적)에서 매 프레임 적의 최신 월드 위치를 조회하는 데 사용.
        /// 주입되지 않은 경우 Phase 1은 스킵되고 Phase 2(A* 재개)로 바로 진입한다.
        /// </summary>
        private IEntityPositionProvider _positionProvider;

        /// <summary>
        /// 근접 유닛 추격 시 타겟 주변 12방향 위치를 슬롯으로 배정하는 매니저.
        /// 같은 적을 노리는 여러 유닛이 한 지점으로 몰리는 뭉침 현상을 방지한다.
        /// 주입되지 않으면 근접 추격 시 타겟 위치 그대로 추적(폴백).
        /// </summary>
        private AttackPositionManager _attackPositionManager;

        /// <summary>
        /// [V3 — 새 이동/전투 흐름] 같은 타일에 여러 유닛이 들어오면 슬롯(앞/뒤좌/뒤우)에 배정하여
        /// 시각적으로 분산시키는 매니저. 새 코루틴(MoveAlongPathV3)이 타일 도착 직전에
        /// GetUnitCount로 인원수를 확인하고, 1명 이상이면 ClaimSlot으로 슬롯 위치를 받아 이동 목표로 사용.
        /// 주입되지 않으면 항상 타일 중심을 이동 목표로 삼는다(폴백).
        /// </summary>
        private TileMoveSlotManager _moveSlotManager;

        // ====================================================================
        // [V3 전용 상태 필드]
        //   현재 어떤 이동 슬롯/공격 슬롯을 점유 중인지 추적하여 cleanup 시점에
        //   누수 없이 정확하게 매니저에 ReleaseSlot을 호출하기 위한 변수들.
        // ====================================================================

        /// <summary>
        /// 현재 점유 중인 이동 슬롯의 타일 좌표.
        /// TileMoveSlotManager.ClaimSlot 호출 시 등록, ReleaseSlot 호출 직후 null로 초기화.
        /// HexCoord는 struct이므로 nullable로 보관 — "이동 슬롯 미점유"를 명확히 표현.
        /// </summary>
        private HexCoord? _v2MoveSlotTile;

        /// <summary>
        /// 현재 점유 중인 공격 슬롯의 타겟 타일 좌표.
        /// ClaimByApproach 호출 시 등록, ReleaseAttackSlot 호출 직후 null로 초기화.
        /// </summary>
        private HexCoord? _v2AttackSlotTargetCoord;

        /// <summary>
        /// Lerp 시작 직전 RegisterOccupancyMove로 예약한 타일 좌표.
        /// Lerp가 정상 완료되면 default로 초기화.
        /// StopMovement/사망 등 Lerp 중간 종료 시 ReleaseOccupancy 호출용.
        ///
        /// HexCoord는 struct이므로 default = (Q=0, R=0).
        /// 비교용 약속으로 (0,0)이 일반 타일이더라도 _pendingOccupancyTile에는 저장하지 않는다 —
        /// 호출 측에서 보장한다(TileOccupancyManager.IsInvalid 약속과 동일).
        /// </summary>
        private HexCoord _pendingOccupancyTile;

        /// <summary>
        /// [BUG-007] 원거리 정지 전투 진행 중인지 여부.
        ///
        /// 배경:
        ///   근접 전투는 공격 슬롯 보유(_v2AttackSlotTargetCoord.HasValue)로 전투 상태를 식별할 수 있지만,
        ///   원거리 정지 전투는 공격 슬롯을 별도로 잡지 않으므로 별도 플래그가 필요하다.
        ///
        ///   건물 건설/파괴 등으로 GameBootstrapper.RepathAllAliveUnits()가 호출되면
        ///   OnPathInvalidated()가 모든 살아있는 유닛에게 전달된다. 전투 중인 원거리 유닛까지
        ///   새 경로 계산 + 이동 코루틴 재시작 대상이 되어 두 코루틴이 동시에 돌고
        ///   이동 슬롯이 중복 점유되는 현상이 발생했다.
        ///
        /// 처리:
        ///   - 원거리 정지 전투 진입 직전에 true, 종료 직후에 false.
        ///   - OnPathInvalidated()는 이 플래그가 true이면 즉시 return하여 전투를 보존한다.
        /// </summary>
        private bool _v2InStationaryCombat;

        /// <summary> 현재 이동 코루틴. null이면 정지 상태. </summary>
        private Coroutine _moveCoroutine;

        /// <summary>
        /// [2026-04-30] 현재 이동 명령의 최종 목적지(예: 적 성, 랠리포인트).
        /// MoveTo(path)가 호출될 때 path[path.Count - 1]을 저장하며,
        /// 새 규칙 4(건물 변경 시 즉시 모든 유닛 경로 재계산) 트리거에서
        /// "이 유닛은 어디로 가고 있었나"를 외부에서 알 수 있도록 노출용으로 보관.
        ///
        /// HexCoord는 struct여서 default = (Q=0, R=0). (0,0)이 일반 타일일 수도 있으나
        /// _hasDestination 플래그를 함께 두어 "목적지 없음" 상태를 명확히 표현한다.
        /// </summary>
        private HexCoord _currentDestination = default;
        private bool _hasDestination = false;

        /// <summary> 외부 조회용: 현재 진행 중인 이동의 최종 목적지. </summary>
        public HexCoord CurrentDestination => _currentDestination;
        /// <summary> 외부 조회용: 진행 중인 이동이 있는지 여부 (목적지 유효성과 무관). </summary>
        public bool HasDestination => _hasDestination;

        /// <summary>
        /// 공격 중 타겟 추적용 Transform 참조.
        /// 공격 시작 시 타겟 GameObject의 Transform을 저장하여,
        /// Update()에서 매 프레임 이 참조의 position을 읽어 회전 갱신.
        /// 팩토리 딕셔너리를 매 프레임 조회하는 대신 Transform 참조를 직접 사용하여 효율적.
        /// 타겟 오브젝트 파괴 시 Unity가 자동으로 null 처리.
        /// </summary>
        private Transform _combatTargetTransform = null;

        // 공격 중 타겟 방향으로 회전하는 속도 (초당 각도).
        // 270도/s 기준: 반대 방향(180도)에서도 약 0.67초 내 전환 완료.
        // 수치 조정 시 이 상수만 변경하면 됨.
        private const float CombatRotationSpeed = 270f;

        // [방어적 백업] Transform 참조가 null이 될 경우 팩토리에서 재조회하기 위한 ID 백업.
        // 멀티플레이 타이밍 문제(StopCombatAnimation / ChangeTarget 호출 순서 역전 등)로
        // _combatTargetTransform이 null로 덮어써지는 경우에도 추적을 복구할 수 있도록 한다.
        private int _combatTargetId = -1;
        private bool _combatTargetIsUnit = true;

        /// <summary> 현재 이동 중인지 여부. InputHandler에서 이동 명령 중복 방지에 사용. </summary>
        public bool IsMoving => _moveCoroutine != null;

        /// <summary> 이 유닛의 Domain 데이터. 외부에서 읽기 전용. </summary>
        public UnitData Data => _unitData;

        /// <summary>
        /// 이동 완료 시 호출되는 콜백. 1회성 — 호출 후 자동 null 리셋.
        /// ProductionTicker에서 랠리→Castle 자동 이동 체인에 사용.
        /// </summary>
        public Action OnMoveComplete { get; set; }

        // ====================================================================
        // 매 프레임 업데이트
        // ====================================================================

        /// <summary>
        /// 공격 중 타겟 방향으로 매 프레임 회전 갱신.
        /// _combatTargetTransform이 null이면(공격 중 아님 또는 타겟 파괴) 즉시 리턴.
        ///
        /// 멀티플레이:
        ///   서버에서만 transform.rotation을 갱신하고,
        ///   NetworkTransform이 이 값을 클라이언트에 보간 전달.
        ///   클라이언트에서 직접 rotation을 쓰면 NetworkTransform과 충돌하여
        ///   이전에 제거한 "클라이언트 자체 회전 로직" 문제가 재발하므로 건너뜀.
        ///
        /// 싱글플레이:
        ///   NetworkContext.IsNetworkActive == false이므로 가드 없이 매 프레임 실행.
        /// </summary>
        private void Update()
        {
            // Transform 참조가 null인 경우, 백업 ID로 재조회를 시도한다.
            // 멀티플레이 타이밍 문제 등으로 _combatTargetTransform이 null로 덮어써진 경우
            // 다음 프레임에 자동으로 복구하기 위한 방어적 처리.
            if (_combatTargetTransform == null)
            {
                // 백업 ID가 유효하지 않으면 추적 대상 없음 — 즉시 리턴
                if (_combatTargetId == -1) return;

                // 백업 ID로 Transform 재조회 시도
                _combatTargetTransform = GetTargetTransform(_combatTargetId, _combatTargetIsUnit);

                // 재조회 실패 → 진짜 없는 타겟이므로 ID도 초기화하고 추적 완전 중단
                if (_combatTargetTransform == null)
                {
                    _combatTargetId = -1;
                    return;
                }
            }

            // 멀티플레이 클라이언트에서는 rotation을 직접 쓰지 않는다.
            // NetworkTransform이 서버 rotation을 자동으로 클라이언트에 동기화하므로,
            // 클라이언트가 직접 rotation을 쓰면 NetworkTransform과 충돌한다.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;

            // 매 프레임 타겟 방향으로 유닛을 부드럽게 회전시킨다.
            // Quaternion.RotateTowards: 현재 회전에서 목표 회전으로 초당 CombatRotationSpeed 각도만큼 이동.
            // 타겟이 멀리 있을수록 회전이 느리게 보이지만, 일반적인 유닛 이동 속도에서는 즉각 추적처럼 보임.
            // 타겟 교체 시(ChangeTarget) 방향이 크게 바뀌어도 자연스럽게 전환.
            float angle = CalculateAttackAngle(_combatTargetTransform.position);
            Quaternion targetRotation = Quaternion.Euler(0f, angle, 0f);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                CombatRotationSpeed * Time.deltaTime
            );
        }

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// UnitFactory에서 프리팹 Instantiate 직후 호출.
        /// Domain 데이터를 전달받아 초기 상태 설정.
        ///
        /// 스폰 시 즉시 rotation 스냅:
        ///   서버: 즉시 스냅 → NetworkTransform이 클라이언트에 보간 전달.
        ///   클라이언트: NetworkTransform이 서버 rotation을 자동 동기화하므로
        ///             여기서 설정한 값은 곧 서버 값으로 덮어씌워짐.
        ///             Red 클라이언트는 NetworkUnit.LateUpdate()에서 +180° 보정.
        /// </summary>
        /// <param name="unitData">이 유닛의 Domain 데이터</param>
        public void Initialize(UnitData unitData)
        {
            _unitData = unitData;

            // [Phase 2] Animator 캐시 — 자식 오브젝트에 있을 수 있으므로 GetComponentInChildren 사용
            _animator = GetComponentInChildren<Animator>();

            // [실기 로그] 유닛 스폰 — 처음에는 finalTarget을 모르므로 위치 정보만 기록.
            // finalTarget은 MoveAlongPathV3 진입 시 별도 로그로 남는다.
            MovementLogger.Log(_unitData.Id, "SPAWN",
                $"position={_unitData.Position} type={_unitData.Type} team={_unitData.Team}");

            // 스폰 시 Facing 방향으로 즉시 rotation 설정.
            // 서버에서 즉시 스냅하면 NetworkTransform이 이 값을 클라이언트에 자동 보간 전달.
            // ViewConverter.IsFlipped 보정은 NetworkUnit.LateUpdate()에서 일괄 처리하므로 불필요.
            int index = (int)_unitData.Facing;
            if (index >= 0 && index < DirectionAngles.Length)
            {
                float spawnAngle = DirectionAngles[index];
                transform.rotation = Quaternion.Euler(0f, spawnAngle, 0f);
            }
        }

        /// <summary>
        /// 외부 의존성 주입. GameBootstrapper에서 모든 컴포넌트 생성 후 호출.
        /// [Phase 2] UnitAnimationData 파라미터 제거 — Animator(Mecanim)가 대체.
        /// </summary>
        public void SetDependencies(GameConfig config,
            UnitMovementUseCase movementUseCase, UnitCombatUseCase combatUseCase,
            UnitFactory unitFactory = null, BuildingFactory buildingFactory = null,
            IEntityPositionProvider positionProvider = null,
            AttackPositionManager attackPositionManager = null,
            TileMoveSlotManager moveSlotManager = null)
        {
            _config = config;
            _movementUseCase = movementUseCase;
            _combatUseCase = combatUseCase;
            _unitFactory = unitFactory;
            _buildingFactory = buildingFactory;
            _positionProvider = positionProvider;
            _attackPositionManager = attackPositionManager;
            // [V3 — 2026-05-06] 새 이동/전투 흐름(MoveAlongPathV3) 전용 매니저.
            //   같은 타일에 도착한 유닛 수가 많으면 GetUnitCount/ClaimSlot으로 슬롯에 분산 배치.
            //   null이면 항상 타일 중심을 이동 목표로 삼는 폴백 동작.
            _moveSlotManager = moveSlotManager;

            // 전투 상태 변화 이벤트 구독 — 싱글플레이 전용.
            // 멀티플레이에서는 NetworkCombatController가 ClientRpc로 직접 메서드 호출.
            if (!NetworkContext.IsNetworkActive)
            {
                // 전투 시작 → Walk 정지 후 Attack 루프 시작 + 타겟 방향 회전
                GameEvents.OnCombatStarted
                    .Subscribe(e =>
                    {
                        if (_unitData != null && e.UnitId == _unitData.Id)
                        {
                            // Walk → Attack 직접 전환 (Idle 없음)
                            StartCombatAnimation(e.TargetId, e.TargetIsUnit);
                        }
                    })
                    .AddTo(this);

                // 타겟 변경 → 회전만 업데이트 (애니메이션 재시작 없음)
                GameEvents.OnCombatTargetChanged
                    .Subscribe(e =>
                    {
                        if (_unitData != null && e.UnitId == _unitData.Id)
                        {
                            ChangeTarget(e.NewTargetId, e.NewTargetIsUnit);
                        }
                    })
                    .AddTo(this);

                // 전투 종료 → Attack 정지 후 Idle 전환
                GameEvents.OnCombatStopped
                    .Subscribe(unitId =>
                    {
                        if (_unitData != null && unitId == _unitData.Id)
                        {
                            StopCombatAnimation();
                        }
                    })
                    .AddTo(this);
            }

            // 사망 이벤트 구독 — 이 유닛이 사망하면 GameObject 파괴
            GameEvents.OnEntityDied
                .Subscribe(e =>
                {
                    if (_unitData != null && e.Entity == (IDamageable)_unitData)
                    {
                        // [실기 로그] 유닛 사망 — 위치/타입/팀/현재 점유 슬롯 상태를 함께 기록.
                        // 사망 시 슬롯 누수 여부 추적이 핵심이므로 어떤 슬롯이 살아있던 상태인지 미리 캡처한다.
                        bool hadV2MoveSlot = _v2MoveSlotTile.HasValue;
                        bool hadV2AttackSlot = _v2AttackSlotTargetCoord.HasValue;
                        MovementLogger.Log(_unitData.Id, "UNIT_DIED",
                            $"position={_unitData.Position} type={_unitData.Type} team={_unitData.Team} hadMoveSlot={hadV2MoveSlot} hadAttackSlot={hadV2AttackSlot}");

                        // [V3] 사망 시 슬롯/점유 해제 — 다른 유닛이 이 자리에 들어올 수 있도록.
                        // V3 흐름은 헬퍼 ReleaseV2MoveSlotIfClaimed / ReleaseV2AttackSlotIfClaimed 만 사용.
                        // (필드/메서드 명만 V2 접두어가 남아있을 뿐, V3에서도 동일하게 사용한다.)
                        ReleaseV2MoveSlotIfClaimed();
                        ReleaseV2AttackSlotIfClaimed();

                        // [V3] Lerp 전에 예약한 TO 타일 점유도 해제 (점유 누수 방지).
                        //   _pendingOccupancyTile != default 이면, 이 유닛이 도착하지 못한 채로 죽었다는 뜻.
                        //   도메인 OnEntityDied 구독으로 UnitMovementUseCase가 unit.Position(=FROM)에 대해서는
                        //   자동 해제하지만, _pendingOccupancyTile(=TO 예약)은 별도로 명시적으로 풀어야 한다.
                        if (_pendingOccupancyTile != default(HexCoord)
                            && _movementUseCase != null && _unitData != null)
                        {
                            _movementUseCase.ReleaseOccupancy(_pendingOccupancyTile, _unitData.Type);
                            _pendingOccupancyTile = default;
                        }

                        // [실기 로그] 사망 시 슬롯 해제 완료 — 호출 직후 상태로 기록(누수 점검용).
                        // ReleaseV2MoveSlot/AttackSlot 헬퍼가 내부에서 SLOT_RELEASE/ATTACK_SLOT_RELEASE를
                        // 이미 기록하지만, "사망 경로에서 해제됐다"는 별도 마커를 한 줄 더 남긴다.
                        MovementLogger.Log(_unitData.Id, "SLOT_RELEASE_ON_DEATH",
                            $"moveSlotReleased={hadV2MoveSlot} attackSlotReleased={hadV2AttackSlot}");

                        // 사망 시 speed 복원 후 IsDead bool 설정 (Animator 트랜지션)
                        if (_animator != null)
                        {
                            _animator.speed = 1f;
                            _animator.SetBool(AnimIsDead, true);
                        }
                        Destroy(gameObject);
                    }
                })
                .AddTo(this);
        }

        // ====================================================================
        // 방향 처리 — Y축 회전 기반
        // ====================================================================

        /// <summary>
        /// HexDirection에 따라 유닛의 Y축 회전을 즉시 설정.
        /// 서버 전용 — 클라이언트는 NetworkTransform이 이 값을 자동 보간 동기화.
        /// DORotate 대신 즉시 스냅하여 NetworkTransform의 이중 보간 문제를 방지.
        ///
        /// 이중 보간 문제란:
        ///   서버에서 DORotate(0.3초 보간) 사용 시, DORotate가 매 프레임 중간값을 설정.
        ///   NetworkTransform이 이 중간값을 다시 0.1초 보간으로 클라이언트에 전달.
        ///   결과: 0.3초 + 0.1초 = 최대 ~1초 딜레이로 회전이 느려지는 현상.
        ///   즉시 스냅하면 NetworkTransform 보간(0.1초)만 적용되어 자연스럽게 동작.
        /// </summary>
        /// <param name="dir">뷰 기준 HexDirection (ViewConverter 반전 적용 후).</param>
        private void ApplyDirection(HexDirection dir)
        {
            int index = (int)dir;
            if (index < 0 || index >= DirectionAngles.Length) return;
            transform.rotation = Quaternion.Euler(0f, DirectionAngles[index], 0f);
        }

        /// <summary>
        /// 타겟의 실제 월드 위치를 기반으로 Atan2 정밀 공격 각도를 계산.
        /// HexCoord 기반이 아닌 transform.position 기반이므로 Lerp 이동 중에도 정확한 방향.
        /// 메시 자식 Y=0으로 통일 후 보정값 불필요 — Atan2 결과를 그대로 사용.
        /// </summary>
        /// <param name="targetWorldPos">타겟의 실제 월드 좌표 (transform.position).</param>
        /// <returns>Y축 회전 각도 (도 단위).</returns>
        private float CalculateAttackAngle(Vector3 targetWorldPos)
        {
            Vector3 dir = targetWorldPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f)
                return transform.eulerAngles.y;
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// targetId와 타입으로 실제 GameObject의 world position 조회.
        /// 이미 파괴된 경우 현재 forward 방향 fallback.
        /// </summary>
        private Vector3 GetTargetWorldPos(int targetId, bool targetIsUnit)
        {
            GameObject obj = targetIsUnit
                ? _unitFactory?.GetUnitObject(targetId)
                : _buildingFactory?.GetBuildingObject(targetId);

            if (obj != null) return obj.transform.position;

            // fallback: 팩토리 미등록 또는 이미 파괴된 경우 현재 forward 방향
            return transform.position + transform.forward;
        }

        /// <summary>
        /// targetId와 타입으로 실제 GameObject의 Transform 조회.
        /// 팩토리에 등록되지 않았거나 이미 파괴된 경우 null 반환.
        /// </summary>
        private Transform GetTargetTransform(int targetId, bool targetIsUnit)
        {
            GameObject obj = targetIsUnit
                ? _unitFactory?.GetUnitObject(targetId)
                : _buildingFactory?.GetBuildingObject(targetId);
            return obj != null ? obj.transform : null;
        }

        // ====================================================================
        // Animator 안전 래퍼
        // ====================================================================

        /// <summary> Animator null 체크 후 bool 파라미터 설정. IsDead 전용. </summary>
        private void SetAnimatorBool(int paramHash, bool value)
        {
            if (_animator != null)
                _animator.SetBool(paramHash, value);
        }

        // ====================================================================
        // 이동
        // ====================================================================

        /// <summary>
        /// 현재 이동을 즉시 중단. 현재 위치에서 정지.
        /// 멀티플레이 서버에서 호출 시 OnUnitWalkStopped 이벤트를 발행하여
        /// NetworkCombatController가 클라이언트에 Walk 정지를 전파.
        /// </summary>
        public void StopMovement()
        {
            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
            }
            _unitData.ClaimedTile = null;

            // [V3] 정지 시 슬롯/점유 해제 — 정지한 유닛이 빈 슬롯을 계속 차지해
            // 다른 유닛이 그 자리로 들어오지 못하는 부작용 방지.
            ReleaseV2MoveSlotIfClaimed();
            ReleaseV2AttackSlotIfClaimed();

            // [V3] Lerp 전에 예약한 TO 타일 점유도 해제 (점유 누수 방지).
            //   _pendingOccupancyTile != default 이면, 도착하지 못한 채 멈춘 것 → 다른 유닛이 들어올 수 있도록 풀어준다.
            if (_pendingOccupancyTile != default(HexCoord)
                && _movementUseCase != null && _unitData != null)
            {
                _movementUseCase.ReleaseOccupancy(_pendingOccupancyTile, _unitData.Type);
                _pendingOccupancyTile = default;
            }

            // Walk 정지 — speed=0으로 프레임 고정
            if (_animator != null) _animator.speed = 0f;

        }

        // ====================================================================
        // [V3] 슬롯 cleanup 헬퍼 (V2에서 만든 헬퍼를 V3가 그대로 재사용)
        // ====================================================================

        /// <summary>
        /// [V2] 현재 점유 중인 TileMoveSlotManager 슬롯을 해제한다.
        /// 사망/이동 정지/공격 슬롯 진입/타일 도착(다음 타일로 이전) 등
        /// 슬롯이 살아있을 수 있는 모든 종료 경로에서 호출.
        /// 매니저 미주입이거나 점유 중이 아니면 안전하게 무시한다.
        /// </summary>
        private void ReleaseV2MoveSlotIfClaimed()
        {
            if (_moveSlotManager != null && _v2MoveSlotTile.HasValue && _unitData != null)
            {
                // [실기 로그] 이동 슬롯 해제 — 호출 직전 좌표를 캡처해야 정확한 위치가 남는다.
                MovementLogger.Log(_unitData.Id, "SLOT_RELEASE",
                    $"tile={_v2MoveSlotTile.Value}");
                _moveSlotManager.ReleaseSlot(_v2MoveSlotTile.Value, _unitData.Id);
            }
            // 매니저가 없더라도 상태는 일관되게 정리한다.
            _v2MoveSlotTile = null;
        }

        /// <summary>
        /// [V2] 현재 점유 중인 AttackPositionManager 공격 슬롯을 해제한다.
        /// 근접 추적 종료(타겟 처치/이탈), 사망, 이동 정지, 헬퍼 종료 모든 경로에서 호출.
        /// 매니저 미주입이거나 점유 중이 아니면 안전하게 무시한다.
        /// </summary>
        private void ReleaseV2AttackSlotIfClaimed()
        {
            if (_attackPositionManager != null && _v2AttackSlotTargetCoord.HasValue && _unitData != null)
            {
                // [실기 로그] 공격 슬롯 해제 — 호출 직전 좌표를 캡처해야 정확한 위치가 남는다.
                // 추격 종료/타겟 전환/사망/StopMovement 등 다양한 경로에서 호출되므로 통합 지점으로 사용.
                MovementLogger.Log(_unitData.Id, "ATTACK_SLOT_RELEASE",
                    $"targetCoord={_v2AttackSlotTargetCoord.Value}");
                _attackPositionManager.ReleaseAttackSlot(_v2AttackSlotTargetCoord.Value, _unitData.Id);
            }
            _v2AttackSlotTargetCoord = null;
        }

        /// <summary>
        /// 경로를 따라 유닛을 시각적으로 이동시킴.
        /// 멀티플레이에서는 서버만 실행 — 클라이언트는 NetworkTransform이
        /// 서버 위치를 자동으로 보간·동기화하므로 이동 로직을 실행하지 않음.
        /// </summary>
        /// <param name="path">A* 경로 (시작점 포함)</param>
        public void MoveTo(List<HexCoord> path)
        {
            // 멀티플레이에서는 서버만 이동 로직을 실행.
            // 클라이언트는 NetworkTransform이 서버 위치를 자동으로 보간·동기화.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;

            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
            }

            // [2026-04-30] 새 규칙 4용 — 외부에서 이 유닛이 어디로 가는지 알 수 있도록 저장.
            // 빈 경로/null 안전 처리: path가 비정상이면 기존 코루틴 동작이 알아서 종료된다.
            if (path != null && path.Count > 0)
            {
                _currentDestination = path[path.Count - 1];
                _hasDestination = true;

                // [실기 로그] A* 경로 계산 완료 — 경로 길이와 시작/종점을 함께 기록.
                if (_unitData != null)
                {
                    MovementLogger.Log(_unitData.Id, "PATH_CALC",
                        $"length={path.Count} from={path[0]} to={_currentDestination}");
                }
            }

            // [V3 — 2026-05-06] 새 이동/전투 흐름 코루틴 시작.
            //   - GameSystemRules.md 규칙 1~18을 그대로 따른다.
            //   - V2(콜백 + V2Result enum)와 달리 외부 while 안에 타일 순회를 직접 인라인 → 가독성 향상.
            //   - 기능 로직(슬롯 처리/우회/원거리 정지/근접 추격/전투 루프/전방 재개)은 V2와 동일.
            _moveCoroutine = StartCoroutine(MoveAlongPathV3(path));
        }

        /// <summary>
        /// [2026-04-30] 외부(예: GameBootstrapper / FlowField 갱신 트리거)에서
        /// "현재 진행 중인 경로가 무효화됐으니 동일 destination으로 다시 RequestMove해서
        ///  새 경로로 이동을 재시작하라"고 알려주는 메서드.
        ///
        /// 호출 조건:
        ///   - HasDestination == true (이미 이동 중인 유닛만)
        ///   - 서버(또는 싱글플레이)에서만. 클라이언트는 NetworkTransform 보간 동기화 결과만 본다.
        ///
        /// 동작:
        ///   1) 현재 destination을 그대로 두고 RequestMove 재호출.
        ///   2) 결과 path가 유효하면 MoveTo(path)로 새 경로 시작 — 기존 코루틴 자동 정지.
        ///   3) 경로 못 찾으면 그대로 두고 다음 트리거를 기다림(=대기).
        ///
        /// 주의:
        ///   - 새 코루틴(MoveAlongPathV3)이 도입된 후에도 동일하게 동작.
        ///   - 전투 중 유닛에 대해 호출되어도 안전: MoveTo가 코루틴을 교체하므로 전투 루프가
        ///     자연스럽게 새 path 기반 외부 while로 대체됨.
        /// </summary>
        public void OnPathInvalidated()
        {
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;
            if (!_hasDestination) return;
            if (_unitData == null || !_unitData.IsAlive) return;
            if (_movementUseCase == null) return;

            // ────────────────────────────────────────────────────────────
            // [BUG-007 — 2026-05-06] 전투 중인 유닛에 대한 repath 차단.
            //
            // 배경:
            //   건물 건설/파괴 시 GameBootstrapper.RepathAllAliveUnits()가
            //   살아있는 모든 유닛의 OnPathInvalidated()를 호출한다.
            //   여기서 그대로 MoveTo(newPath)를 부르면 기존 전투 코루틴이 종료되지 않은 채
            //   새 이동 코루틴이 시작되어 두 코루틴이 동시에 실행된다.
            //   결과: 전투 타일과 별도로 다음 타일에 이동 슬롯이 추가로 잡혀 유령 점유가 생긴다.
            //
            // 전투 상태 판정:
            //   - 근접: _v2AttackSlotTargetCoord.HasValue (EnterMeleePursuit 동안 set).
            //   - 원거리: _v2InStationaryCombat (EnterStationaryCombat 진입 직전에 true).
            //
            // 동작:
            //   전투 중이면 즉시 return. 전투 종료 후 ResumeFromForwardTile / 자연 진행으로 path가
            //   다시 정상 흐름을 타기 때문에, 이 시점에 굳이 새 path를 강제로 끼워 넣지 않는다.
            // ────────────────────────────────────────────────────────────
            if (IsInCombat())
            {
                // [실기 로그] 전투 중이라 repath를 무시했음을 명시 — 검증용 마커.
                MovementLogger.Log(_unitData.Id, "REPATH_SKIP_COMBAT", $"reason=InCombat");
                return;
            }

            HexCoord dest = _currentDestination;
            List<HexCoord> newPath = _movementUseCase.RequestMove(_unitData, dest);
            if (newPath != null && newPath.Count >= 2)
            {
                MoveTo(newPath);
            }
            // newPath가 null/짧으면 현재 위치에서 그대로 대기. 다음 트리거에서 다시 시도.
        }

        /// <summary>
        /// [BUG-007 — 2026-05-06] 현재 유닛이 전투 중인지 판정.
        ///
        /// 판정 기준:
        ///   - 근접 공격 슬롯을 보유 중이면 EnterMeleePursuit/EnterCombatLoop 진행 중.
        ///   - 원거리 정지 전투 플래그가 true이면 EnterStationaryCombat 진행 중.
        ///
        /// 호출 측은 이 결과로 "이동 코루틴을 새로 시작하면 안 되는 상태"를 빠르게 식별한다.
        /// </summary>
        private bool IsInCombat()
        {
            return _v2AttackSlotTargetCoord.HasValue || _v2InStationaryCombat;
        }

        // ====================================================================
        // [V3 — 2026-05-06] MoveAlongPathV3 — 새 이동/전투 흐름 (인라인 구조)
        // ====================================================================
        // GameSystemRules.md 규칙 1~18을 따른 V3 코루틴.
        //
        // V2 대비 구조적 변경:
        //   - V2Result enum + Action<>/콜백 위임 패턴 제거
        //   - RunTileTraversal 분리 헬퍼 제거 → 외부 while 안에 타일 순회 for 루프 직접 인라인
        //   - 흐름 제어가 명확해져 디버깅과 코드 리뷰가 쉬워짐
        //
        // 기능 로직(슬롯/우회/원거리 정지/근접 추격/전투 루프/전방 재개)은 V2와 동일.
        //
        // 헬퍼 구조:
        //   MoveAlongPathV3            — 메인 코루틴 (서버 가드 + 외부 while + 타일 순회 인라인 + cleanup)
        //   ├─ EnterMeleePursuitV3     — 근접: 이동슬롯 해제 → 공격슬롯으로 직진 (규칙 11/13)
        //   │     └─ EnterCombatLoopV3 — 공격 사거리 도달 후 공격/쿨다운 루프
        //   ├─ EnterStationaryCombatV3 — 원거리: 이동슬롯 유지 + 즉시 공격 (규칙 13)
        //   │     └─ EnterCombatLoopV3
        //   ├─ ResumeFromForwardTileV3 — 근접 전투 종료 후 앞쪽 타일 A* 재개 (규칙 11/15)
        //   └─ MoveCleanupAndCompleteV3 — 슬롯/점유 정리 + OnMoveComplete 콜백
        // ====================================================================

        /// <summary>
        /// [V3] 새 이동/전투 메인 코루틴.
        ///
        /// 동작 흐름:
        ///   1) 멀티플레이 가드 — 서버가 아니면 즉시 yield break (NetworkTransform이 보간 동기화).
        ///   2) path 유효성 체크 — 비정상이면 cleanup 후 종료.
        ///   3) Walk 애니메이션 시작 — 한 번만 CrossFade (반복 호출 시 모션이 끊김).
        ///   4) 외부 while: 유닛이 살아있는 동안 path 순회용 for 루프를 인라인으로 돌린다.
        ///        - 우회 발생 또는 근접 전투 종료로 새 path가 필요해지면 break → 외부 while로 빠져 RequestMove 후 재진입.
        ///        - 정상 완주 또는 사망이면 break → cleanup 단계로.
        ///   5) MoveCleanupAndCompleteV3로 마무리 (슬롯/점유 정리 + OnMoveComplete 콜백).
        ///
        /// 멀티플레이 정책:
        ///   서버에서만 코루틴이 실행되며, NetworkTransform이 클라이언트에 위치를 자동 보간 전달한다.
        ///   클라이언트가 직접 이동하면 이중 보간 + 권위 위반이 발생하므로 진입부에서 차단한다.
        /// </summary>
        /// <param name="path">A* 경로 (시작점 포함, 길이 ≥ 2).</param>
        private IEnumerator MoveAlongPathV3(List<HexCoord> path)
        {
            // ── 멀티플레이 가드 ────────────────────────────────────────────
            // 서버가 아닌 클라이언트는 NetworkTransform이 위치를 보간 동기화하므로 코루틴을 돌리지 않는다.
            if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
            {
                _moveCoroutine = null;
                yield break;
            }

            // path가 비정상(null / 길이 1 이하 / 유닛 사망 등)이면 즉시 cleanup 후 종료.
            if (path == null || path.Count < 2 || _unitData == null || !_unitData.IsAlive)
            {
                MoveCleanupAndCompleteV3();
                yield break;
            }

            // 멀티플레이 서버: 이동 시작 이벤트 발행 → NetworkCombatController가
            // StartWalkAnimationClientRpc로 모든 클라이언트에 Walk 시작 전파.
            if (NetworkContext.IsNetworkActive)
                GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);

            // Walk 애니메이션 시작 — 이동 시작 시 1회만 CrossFade.
            // 매 타일마다 CrossFade하면 블렌딩이 매번 리셋되어 Walk 모션이 끊긴다.
            if (_animator != null)
            {
                _animator.speed = 1f;
                _animator.CrossFadeInFixedTime(StateWalk, _idleToWalkBlend, 0);
            }

            // 최종 목적지(외부 while 동안 변하지 않음). 우회/재경로 모두 이 좌표를 기준.
            HexCoord finalTarget = path[path.Count - 1];

            // [실기 로그] V3 진입 — finalTarget을 포함한 이동 시작 기록.
            MovementLogger.Log(_unitData.Id, "SPAWN",
                $"finalTarget={finalTarget} from={_unitData.Position} pathLength={path.Count}");

            // 유닛별 개별 이동 속도 (MoveSpeed 칸/초 → 1칸 이동 시간(초) 변환).
            float moveSeconds = _unitData.MoveSpeed > 0f ? 1f / _unitData.MoveSpeed : 1.0f;

            // ────────────────────────────────────────────────────────────────
            // 외부 while — 다음의 두 가지 이유로 path가 갱신되면 다시 위로 돌아온다:
            //   (A) 우회(detour) 발생: 원래 path가 무효 → RequestMove로 새 path 받음.
            //   (B) 근접 전투 후 ResumeFromForwardTileV3: 전투 종료 시점에서 새 path 받음.
            // 정상 완주 또는 사망이면 외부 while 자체가 break.
            // ────────────────────────────────────────────────────────────────
            while (_unitData != null && _unitData.IsAlive)
            {
                // 새 path를 받아 다시 시작할지 결정하는 플래그.
                // for 안에서 true로 바꾼 뒤 break하면 외부 while로 빠져 path를 갱신하고 continue로 돌아온다.
                bool needRepath = false;

                // 마지막에 실제로 도착한 타일 — from 인자에 사용. 우회 시에도 정확히 추적.
                HexCoord prevActualTile = _unitData.Position;

                // ────────────────────────────────────────────────────────────
                // 타일 순회 for — path[0]은 시작점이므로 1부터.
                // ────────────────────────────────────────────────────────────
                for (int i = 1; i < path.Count; i++)
                {
                    // 사망/소실 체크 — 매 타일 진입 시 안전 장치.
                    if (_unitData == null || !_unitData.IsAlive) break;

                    HexCoord from = prevActualTile;
                    HexCoord to = path[i];

                    // 마지막 스텝이 non-walkable(성/건물)인지 — 점유/우회 검사를 건너뛰는 기준.
                    bool isLastStep = (i == path.Count - 1);
                    bool isLastStepToNonWalkable = isLastStep
                        && (_movementUseCase != null && !_movementUseCase.IsWalkable(to));

                    // ──────────────────────────────────────────────────────
                    // 규칙 5 — 점유가 가득 찬 타일 처리: 앞쪽 빈 타일 우회 또는 대기.
                    // ──────────────────────────────────────────────────────
                    bool didDetour = false;
                    if (!isLastStepToNonWalkable && _movementUseCase != null)
                    {
                        float mySize = _movementUseCase.GetOccupancySize(_unitData.Type);

                        HexCoord? actualTo = null;
                        // 첫 시도가 실패해 yield return null로 대기에 들어간 경우만 WAIT_START/END 기록.
                        bool waitLogged = false;

                        while (actualTo == null && _unitData.IsAlive)
                        {
                            // [BUG-008] 현재 유닛 위치 타일을 명시 전달 → BFS visited에 미리 등록되어
                            // "현재 위치"가 우회 후보로 반환되는 사고(detourTo == currentTile)를 차단.
                            actualTo = _movementUseCase.FindForwardAvailable(to, mySize, finalTarget, prevActualTile);
                            if (actualTo == null)
                            {
                                if (!waitLogged)
                                {
                                    MovementLogger.Log(_unitData.Id, "WAIT_START",
                                        $"current={prevActualTile} blockedTile={to}");
                                    waitLogged = true;
                                }
                                yield return null;          // 앞쪽 빈 타일 없음 — 다음 프레임에 다시 시도.
                            }
                        }
                        if (!_unitData.IsAlive) break;

                        if (waitLogged)
                        {
                            MovementLogger.Log(_unitData.Id, "WAIT_END",
                                $"resumeTo={actualTo.Value}");
                        }

                        if (actualTo.Value != to)
                        {
                            didDetour = true;
                            MovementLogger.Log(_unitData.Id, "DETOUR",
                                $"originalTo={to} detourTo={actualTo.Value} currentTile={prevActualTile}");
                        }

                        to = actualTo.Value;
                        // 우회 결과 마지막 스텝 판정이 바뀔 수 있으므로 재평가.
                        isLastStepToNonWalkable = isLastStep && !_movementUseCase.IsWalkable(to);
                    }

                    // [실기 로그] 다음 타일로의 이동이 확정된 시점.
                    MovementLogger.Log(_unitData.Id, "TILE_MOVE",
                        $"from={from} to={to} lastStep={isLastStep}");

                    // ──────────────────────────────────────────────────────
                    // Lerp 시작 직전 점유 등록 (Race Condition 방지).
                    //   같은 프레임 내 다른 유닛이 즉시 "이 타일 차 있음"을 인식하도록 TO+1 예약.
                    //   FROM 해제는 ProcessStep에서 수행 (4차 개선 분리 방식).
                    // ──────────────────────────────────────────────────────
                    if (_movementUseCase != null && _unitData != null)
                    {
                        _movementUseCase.RegisterOccupancyMove(from, to, _unitData.Type);
                        _pendingOccupancyTile = to;
                    }

                    // 다른 시스템(전투/생산)에서 참조 가능하도록 ClaimedTile 갱신 (마지막 non-walkable 제외).
                    if (!isLastStepToNonWalkable)
                        _unitData.ClaimedTile = to;

                    // ──────────────────────────────────────────────────────
                    // 방향 전환 — 도메인 from→to 방향을 뷰 관점으로 반전 후 Y축 회전 즉시 스냅.
                    //   서버에서 즉시 스냅하면 NetworkTransform이 클라이언트에 보간 전달.
                    // ──────────────────────────────────────────────────────
                    HexDirection dir = FacingDirection.FromCoords(from, to);
                    dir = ViewConverter.FlipDirection(dir);
                    _unitData.Facing = dir;
                    ApplyDirection(dir);

                    // ──────────────────────────────────────────────────────
                    // 규칙 10/17 — 이동 슬롯 결정 + 슬롯 위치를 이동 목표로.
                    //   forward 벡터(도메인 좌표 기준 from→to)는 매니저에 전달.
                    //   슬롯 위치는 도메인 좌표 → ViewConverter.ToView로 뷰 좌표 변환 + UnitYOffset.
                    // ──────────────────────────────────────────────────────
                    Vector3 fromDomain = HexMetrics.HexToWorld(from);
                    Vector3 toDomain = HexMetrics.HexToWorld(to);

                    // forward 벡터(도메인 좌표). 영벡터 폴백은 매니저 내부에서 처리.
                    Vector3 forwardDirDomain = toDomain - fromDomain;

                    // Lerp 출발점은 현재 위치(직전 슬롯/위치를 그대로 이어받는다).
                    Vector3 fromPos = transform.position;

                    // ──────────────────────────────────────────────────────
                    // 이동 목표 = 타일 중심 또는 이동 슬롯의 뷰 월드 좌표.
                    //
                    // [규칙 10] A* 이동 중 다음 타일이 결정되는 순간, 해당 타일 내 유닛 수를 확인한다.
                    //   - 타일 안에 유닛이 혼자(=목표 타일 점유 0명, 내가 들어가면 1명)인 경우:
                    //       → 타일 중심을 이동 목표로 삼는다. 슬롯을 새로 잡지 않는다.
                    //   - 타일 안에 유닛이 2명 이상(=목표 타일 점유 1명 이상, 내가 들어가면 2명 이상)인 경우:
                    //       → 이동 슬롯을 즉시 할당하고 슬롯 위치를 이동 목표로 삼는다.
                    //
                    // 주의: GetUnitCount(to)는 "현재 타일에 이미 클레임된 유닛 수"이며,
                    //       나 자신은 ClaimSlot 호출 직전이므로 아직 카운트에 포함되지 않는다.
                    //       따라서 0이면 "혼자 들어감", 1 이상이면 "2명 이상".
                    // ──────────────────────────────────────────────────────
                    Vector3 toPos;
                    if (!isLastStepToNonWalkable && _moveSlotManager != null && _unitData != null)
                    {
                        int existingCount = _moveSlotManager.GetUnitCount(to);

                        if (existingCount == 0)
                        {
                            // [규칙 10] 혼자 들어가는 타일 — 타일 중심으로 이동.
                            //   직전 스텝에서 다른 타일에 잡아둔 이동 슬롯이 남아있다면 해제(slot leak 방지).
                            //   _v2MoveSlotTile은 null로 비워, 다음 스텝에서 정상적으로 새 판단이 이뤄지도록 한다.
                            if (_v2MoveSlotTile.HasValue && _v2MoveSlotTile.Value != to)
                                ReleaseV2MoveSlotIfClaimed();

                            // 타일 중심 좌표 = 도메인 좌표 → 뷰 좌표 변환 + 유닛 높이 보정.
                            toPos = ViewConverter.ToView(toDomain);
                            toPos.y += HexMetrics.UnitYOffset;

                            _v2MoveSlotTile = null;

                            // [실기 로그] 혼자 들어가는 타일이므로 타일 중심으로 이동한다.
                            MovementLogger.Log(_unitData.Id, "TILE_CENTER_MOVE",
                                $"tile={to} existingCount={existingCount} worldPos={toPos}");
                        }
                        else
                        {
                            // [규칙 10] 이미 다른 유닛이 있는 타일 — 이동 슬롯을 즉시 할당.
                            //   직전 이동 슬롯이 다른 타일이면 먼저 해제 (slot leak 방지).
                            if (_v2MoveSlotTile.HasValue && _v2MoveSlotTile.Value != to)
                                ReleaseV2MoveSlotIfClaimed();

                            // 새 타일에서 슬롯을 클레임 — 도메인 좌표 → 뷰 좌표 변환 + 유닛 높이 보정.
                            Vector3 slotDomain = _moveSlotManager.ClaimSlot(to, _unitData.Id, fromDomain, forwardDirDomain);
                            Vector3 slotView = ViewConverter.ToView(slotDomain);
                            slotView.y += HexMetrics.UnitYOffset;

                            toPos = slotView;
                            _v2MoveSlotTile = to;

                            // [실기 로그] 이동 슬롯 클레임 — 슬롯 번호도 함께 조회해 기록.
                            int slotIndex = -1;
                            _moveSlotManager.TryGetSlot(to, _unitData.Id, out slotIndex);
                            MovementLogger.Log(_unitData.Id, "SLOT_CLAIM",
                                $"tile={to} slot={slotIndex} existingCount={existingCount} worldPos={toPos}");
                        }
                    }
                    else
                    {
                        // 매니저 미주입 또는 마지막 스텝 non-walkable — 타일 중심을 그대로 사용.
                        // non-walkable 마지막 스텝(성/건물)은 슬롯을 새로 잡지 않으나 직전 슬롯은 유지(공격 위치 분산 효과).
                        toPos = ViewConverter.ToView(toDomain);
                        toPos.y += HexMetrics.UnitYOffset;
                    }

                    // ──────────────────────────────────────────────────────
                    // Lerp 이동 + 매 프레임 적 감지 / 사거리 체크.
                    //   이동 시간 = moveSeconds (한 칸 시간). 슬롯 오프셋 미세 차이는 무시.
                    // ──────────────────────────────────────────────────────
                    float elapsed = 0f;
                    float targetDuration = moveSeconds;

                    // [규칙 15] 원거리 전투로 Lerp가 중단되었는지 표시하는 플래그.
                    //   true면 Lerp while 탈출 후 toPos 스냅과 ProcessStep을 건너뛰고
                    //   현재 물리 위치 기준으로 A*를 새로 요청한다.
                    //   for 루프의 매 타일 반복마다 자동으로 새로 선언되므로 별도 초기화 불필요.
                    bool interruptedByCombat = false;

                    while (elapsed < targetDuration && _unitData != null && _unitData.IsAlive)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / targetDuration);
                        transform.position = Vector3.Lerp(fromPos, toPos, t);

                        // ── 적 감지 / 전투 진입 분기 ──────────────────────
                        if (_combatUseCase != null && _unitData.IsAlive)
                        {
                            AttackKind kind = UnitStats.GetAttackKind(_unitData.Type);

                            // 원거리: 공격 사거리 진입 즉시 정지 + 공격 (이동 슬롯 유지).
                            if (kind == AttackKind.Ranged && _combatUseCase.HasEnemyInRange(_unitData))
                            {
                                // [실기 로그] 원거리 적 감지 — 공격 사거리 안.
                                var detectedRanged = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
                                if (detectedRanged.HasValue)
                                {
                                    MovementLogger.Log(_unitData.Id, "ENEMY_DETECTED",
                                        $"kind=Ranged enemyId={detectedRanged.Value.id} isUnit={detectedRanged.Value.isUnit}");
                                }

                                // [규칙 13] 원거리는 현재 위치에서 즉시 멈춤 — 스냅 없음.
                                //   - 과거에는 toPos(이동 슬롯 위치)로 순간이동시켰지만,
                                //     이렇게 하면 후속 유닛이 도착해 같은 toPos에서 또 멈추면서 겹친다.
                                //   - 현재 물리 위치에서 그대로 정지 → 공격 슬롯(아래 EnterStationaryCombatV3)이
                                //     접근 방향 기반 12방향 분산을 담당해 겹침을 방지한다.
                                //   - 목적지 이동 슬롯은 더 이상 도착하지 않으므로 즉시 해제(점유 누수 방지).
                                ReleaseV2MoveSlotIfClaimed();

                                // elapsed를 최대값으로 설정 → 전투 종료 후 Lerp while 루프 즉시 탈출,
                                // 이어지는 TILE_ARRIVED 처리 흐름이 자연스럽게 실행된다.
                                elapsed = targetDuration;

                                MovementLogger.Log(_unitData.Id, "STATIONARY_COMBAT_START",
                                    $"currentTile={to}");
                                MovementLogger.Log(_unitData.Id, "COMBAT_START",
                                    $"mode=Ranged currentTile={prevActualTile}");

                                // [BUG-007] 원거리 정지 전투 진입 플래그 ON — OnPathInvalidated의 repath를 차단.
                                _v2InStationaryCombat = true;
                                yield return EnterStationaryCombatV3();
                                // [방향 버그 수정 — 2026-05-11]
                                // A* 이동 재개 시 ApplyDirection(dir)이 이동 방향으로 회전을 설정하지만,
                                // _combatTargetTransform이 null이 아닌 동안 Update()가 매 프레임 회전을 덮어써서
                                // 이동 방향이 아닌 잘못된 방향을 바라보는 버그가 발생한다.
                                // StopCombatClientRpc는 TickCombat(50ms 주기)으로 지연 전송되어 즉시 해소되지 않는다.
                                // 서버에서 StopCombatAnimation()을 직접 호출하면 _combatTargetTransform = null,
                                // _combatTargetId = -1이 즉시 적용되어 Update()의 회전 간섭이 사라진다.
                                // 이후 ApplyDirection(dir)이 설정한 이동 방향이 그대로 유지된다.
                                // 클라이언트 동기화는 기존 StopCombatClientRpc가 그대로 처리. 중복 호출은 내부 null 체크로 안전.
                                StopCombatAnimation();
                                _v2InStationaryCombat = false;

                                MovementLogger.Log(_unitData.Id, "COMBAT_END",
                                    $"reason=StationaryEnded mode=Ranged");

                                if (_unitData == null || !_unitData.IsAlive) break;

                                // [규칙 15] 원거리 전투 종료 — 현재 물리 위치에서 A*를 재개해야 한다.
                                //   과거에는 yield/continue로 같은 Lerp를 이어갔는데, elapsed가 이미
                                //   targetDuration이므로 다음 while 조건이 false가 되어 루프를 자연 탈출하고
                                //   곧이어 transform.position = toPos가 실행 → 슬롯 위치로 순간이동하는
                                //   버그가 있었다.
                                //   이제는 플래그만 ON으로 켜두고 while을 자연 탈출시킨 뒤,
                                //   while 종료 직후 분기에서 toPos 스냅과 ProcessStep을 모두 건너뛰고
                                //   needRepath = true로 for를 빠져나가 외부 while에서 RequestMove를
                                //   다시 호출하도록 한다.
                                interruptedByCombat = true;
                            }

                            // 근접: 감지 사거리 진입(공격 사거리 밖) → 이동 슬롯/점유 풀고 추격 시작.
                            if (kind == AttackKind.Melee
                                && _combatUseCase.HasEnemyInDetectRange(_unitData)
                                && !_combatUseCase.HasEnemyInRange(_unitData))
                            {
                                // [실기 로그] 근접 적 감지 — 추격 모드 전환 직전.
                                var detectedMelee = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
                                var detectedMeleeCoord = _combatUseCase.FindNearestEnemyPositionInDetectRange(_unitData);
                                if (detectedMelee.HasValue)
                                {
                                    int hexDist = detectedMeleeCoord.HasValue
                                        ? HexCoord.Distance(_unitData.Position, detectedMeleeCoord.Value)
                                        : -1;
                                    MovementLogger.Log(_unitData.Id, "ENEMY_DETECTED",
                                        $"kind=Melee enemyId={detectedMelee.Value.id} isUnit={detectedMelee.Value.isUnit} hexDist={hexDist}");
                                }

                                // 이동 슬롯 해제 — 다른 유닛이 이 타일에 들어올 수 있게 함.
                                ReleaseV2MoveSlotIfClaimed();
                                // Lerp 도중 도착 못 한 to 타일의 점유 예약을 해제(누수 방지) — 인라인 처리.
                                if (_pendingOccupancyTile != default(HexCoord)
                                    && _movementUseCase != null && _unitData != null)
                                {
                                    _movementUseCase.ReleaseOccupancy(_pendingOccupancyTile, _unitData.Type);
                                    _pendingOccupancyTile = default;
                                }
                                _unitData.ClaimedTile = null;

                                yield return EnterMeleePursuitV3(finalTarget);

                                if (_unitData == null || !_unitData.IsAlive) break;

                                // 근접 전투 종료 → ResumeFromForwardTileV3로 새 path를 받고 외부 while로 위임.
                                List<HexCoord> resumePath = ResumeFromForwardTileV3(finalTarget);
                                if (resumePath != null && resumePath.Count >= 2)
                                {
                                    MovementLogger.Log(_unitData.Id, "RESUME_FORWARD",
                                        $"resumeFrom={_unitData.Position} newPathLength={resumePath.Count}");
                                    path = resumePath;
                                    needRepath = true;
                                    break;  // for 탈출 → 외부 while continue
                                }

                                // 새 path를 만들지 못함 → 이동 종료(외부 while도 자연 break).
                                MovementLogger.Log(_unitData.Id, "RESUME_FAILED",
                                    $"fromPos={_unitData.Position} finalTarget={finalTarget}");
                                goto cleanup;
                            }

                            // 근접인데 이미 공격 사거리 안(드문 케이스) → 즉시 EnterCombatLoopV3.
                            if (kind == AttackKind.Melee && _combatUseCase.HasEnemyInRange(_unitData))
                            {
                                var detectedMeleeAdj = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
                                if (detectedMeleeAdj.HasValue)
                                {
                                    MovementLogger.Log(_unitData.Id, "ENEMY_DETECTED",
                                        $"kind=Melee-InRange enemyId={detectedMeleeAdj.Value.id} isUnit={detectedMeleeAdj.Value.isUnit}");
                                }

                                MovementLogger.Log(_unitData.Id, "COMBAT_START",
                                    $"mode=Melee-InRange currentTile={prevActualTile}");

                                yield return EnterCombatLoopV3();

                                MovementLogger.Log(_unitData.Id, "COMBAT_END",
                                    $"reason=InRangeLoopEnded mode=Melee-InRange");

                                if (_unitData == null || !_unitData.IsAlive) break;

                                // 같은 슬롯 위치 그대로 이번 Lerp 계속.
                                yield return null;
                                continue;
                            }
                        }

                        yield return null;
                    }

                    if (_unitData == null || !_unitData.IsAlive) break;

                    // [규칙 15] 원거리 전투로 Lerp가 중단된 경우:
                    //   유닛은 toPos(이동 슬롯 위치)에 물리적으로 도달하지 않았으므로
                    //   ① toPos 강제 스냅 금지 — 순간이동으로 시각적 텔레포트가 발생함.
                    //   ② ProcessStep 호출 금지 — 도메인 위치(_unitData.Position)를 to로 갱신하면
                    //      실제로는 마지막 정상 도착 타일(prevActualTile)에 있는 유닛의 좌표가
                    //      어긋나 점유/패스파인딩 정합성이 깨진다.
                    //
                    //   [버그 수정 — 2026-05-11]
                    //   기존 코드는 needRepath = true 만 세팅하고 path 자체를 교체하지 않았다.
                    //   그 결과 외부 while이 continue로 돌아온 뒤에도 같은 path를 재사용하여
                    //   for i=1 부터 다시 순회 → 이미 통과한 타일(path[1], 즉 출발지 근방)로
                    //   역행하는 현상이 발생했다.
                    //
                    //   예시:
                    //     원래 path = [(5,0), (5,1), (5,2), ..., (5,5), ..., (5,16)]
                    //     (5,5)까지 정상 도달 후 (5,5)→(5,6) Lerp 중 원거리 적 감지 → 정지 전투.
                    //     전투 종료 시점에 _unitData.Position = (5,5).
                    //     이때 path를 그대로 두고 for i=1 부터 시작하면 to = path[1] = (5,1) → 역행.
                    //
                    //   해결:
                    //     RequestMove로 현재 위치(_unitData.Position) → finalTarget 새 경로를 받아
                    //     path를 교체한 뒤 break. 외부 while continue 시 새 path[0] = 현재 위치,
                    //     path[1] = 정방향 다음 타일이 되어 정상 진행한다.
                    //
                    //   _pendingOccupancyTile 해제는 원거리 전투 진입부에서 이미 처리되지 않으므로
                    //   여기서 누수 방지 차원에서 함께 풀어준다.
                    if (interruptedByCombat)
                    {
                        // ── 1) 점유/슬롯 클리어 ──
                        // Lerp 중단으로 to 타일에 도달하지 않았으므로 to에 대한 점유 예약을 풀어야 한다.
                        // 풀지 않으면 다음 유닛이 이 타일을 사용 불가로 인식해 무한 대기/우회가 발생한다.
                        if (_pendingOccupancyTile != default(HexCoord)
                            && _movementUseCase != null && _unitData != null)
                        {
                            _movementUseCase.ReleaseOccupancy(_pendingOccupancyTile, _unitData.Type);
                            _pendingOccupancyTile = default;
                        }
                        _unitData.ClaimedTile = null;

                        MovementLogger.Log(_unitData.Id, "RANGED_COMBAT_REPATH",
                            $"resumeFrom={_unitData.Position} finalTarget={finalTarget}");

                        // ── 2) 새 path 계산 ──
                        // 기존 path를 그대로 쓰면 외부 while → for i=1 → path[1](=출발지 근방)로 역행한다.
                        // 따라서 현재 도메인 위치(_unitData.Position = 마지막 정상 도착 타일)에서
                        // finalTarget까지의 새 경로를 받아 path를 교체한 뒤 break 한다.
                        // 패턴은 같은 코루틴 내 didDetour 분기의 rerouted 처리(라인 1179~)와 동일하다.
                        List<HexCoord> repathedPath = _movementUseCase != null
                            ? _movementUseCase.RequestMove(_unitData, finalTarget)
                            : null;

                        if (repathedPath != null && repathedPath.Count >= 2)
                        {
                            // 새 경로 확보 — path를 교체하고 외부 while로 위임.
                            // 외부 while은 continue로 다시 진입하여 prevActualTile = _unitData.Position
                            // 부터 새 path[1]로 정방향 이동을 시작한다.
                            path = repathedPath;
                            needRepath = true;
                            break;  // for 탈출 → 외부 while continue → 새 path로 재순회.
                        }

                        // 경로를 만들지 못함 — 적이 목적지를 완전히 막았거나 finalTarget이 도달 불가.
                        // 더 이상 이동할 수 없으므로 이동 자체를 종료하고 cleanup으로 점프한다.
                        MovementLogger.Log(_unitData.Id, "RANGED_COMBAT_REPATH_FAIL",
                            $"resumeFrom={_unitData.Position} finalTarget={finalTarget}");
                        goto cleanup;
                    }

                    // 정확한 최종 위치 보정 — 슬롯 위치(toPos)에 도착 (정상 Lerp 완료 시에만).
                    transform.position = toPos;

                    // 도메인 처리:
                    //   ProcessStep으로 _unitData.Position을 to로 갱신 + FROM 점유 해제.
                    //   마지막 스텝이 non-walkable이면 ProcessStep 생략 — 건물/성 위에 유닛이 위치한 것으로 처리되지 않도록.
                    if (_movementUseCase != null && !(isLastStep && isLastStepToNonWalkable))
                    {
                        _movementUseCase.ProcessStep(_unitData, from, to);
                    }

                    _unitData.ClaimedTile = null;
                    prevActualTile = to;
                    _pendingOccupancyTile = default;     // 정상 도착 — 미해제 점유 없음.

                    MovementLogger.Log(_unitData.Id, "TILE_ARRIVED",
                        $"tile={to} pos={transform.position}");

                    // 우회가 발생했다면 원래 path는 더 이상 유효하지 않다 — 새 RequestMove로 재계산.
                    if (didDetour)
                    {
                        List<HexCoord> rerouted = _movementUseCase != null
                            ? _movementUseCase.RequestMove(_unitData, finalTarget)
                            : null;

                        if (rerouted != null && rerouted.Count >= 2)
                        {
                            path = rerouted;
                            needRepath = true;
                            break;  // for 탈출 → 외부 while continue
                        }

                        // 새 경로 없음 — 이동 종료.
                        goto cleanup;
                    }
                }  // end of for (path 순회)

                // for를 정상 종료(완주) 또는 사망/탈출 — 외부 while 분기.
                if (needRepath) continue;   // 새 path로 외부 while 재진입.
                break;                       // 정상 완주 또는 사망 — 외부 while 종료.
            }

            cleanup:
            // 정상/사망/완주/RESUME 실패 등 모든 종료 경로에서 공통 cleanup.
            MoveCleanupAndCompleteV3();
        }

        /// <summary>
        /// [V3] 근접 추격 코루틴 (규칙 11/13).
        ///
        /// 동작:
        ///   1) FindNearestEnemyInDetectRange로 첫 타겟 선택.
        ///   2) ClaimByApproach로 공격 슬롯 배정 (contactRadius: 유닛=0.3f, 건물=0.5f).
        ///   3) 매 프레임 슬롯 위치로 직선 이동 + 적 방향 회전 추적.
        ///   4) HasEnemyInRange → EnterCombatLoopV3 호출.
        ///   5) 타겟 처치 시 다음 적 재선택 or 종료.
        ///   6) 감지 사거리 이탈 시 종료.
        ///   7) 종료 시 ReleaseV2AttackSlotIfClaimed 보장 (누수 방지).
        ///
        /// finalTarget은 현재 헬퍼 안에서 직접 쓰지 않지만, 추후 forward 필터 도입에 대비해 호출 측이 가지고 있다.
        /// </summary>
        private IEnumerator EnterMeleePursuitV3(HexCoord finalTarget)
        {
            if (_combatUseCase == null || _positionProvider == null
                || _attackPositionManager == null || _unitData == null)
            {
                // [실기 로그 — 2026-05-11] 추적 진입 자체가 불가한 케이스.
                //   주입이 빠진 상태 — 일반적인 플레이에서는 발생하면 안 된다.
                //   분산이 안 되는 원인 추적 중 "로그조차 안 남는 케이스"를 잡기 위해 기록.
                int safeId = _unitData != null ? _unitData.Id : -1;
                MovementLogger.Log(safeId, "MELEE_PURSUIT_SKIP",
                    $"reason=NullDependency combatUseCase={_combatUseCase != null}"
                    + $" positionProvider={_positionProvider != null}"
                    + $" attackPositionManager={_attackPositionManager != null}"
                    + $" unitData={_unitData != null}");
                yield break;
            }

            MovementLogger.Log(_unitData.Id, "MELEE_PURSUIT_START",
                $"currentPos={_unitData.Position}");

            // 첫 타겟 선택 — 감지 사거리 내 가장 가까운 적.
            var firstTarget = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
            if (!firstTarget.HasValue)
            {
                // [실기 로그 — 2026-05-11] 호출 측에서 감지 사거리 안 적이 있다고 판단했는데
                //   FindNearestEnemyInDetectRange가 null을 반환하는 경우.
                //   (감지 이벤트 발생부터 EnterMeleePursuitV3 진입까지 사이에 적이 사라졌거나,
                //    내부 조회 로직이 동기화되지 않은 케이스를 잡기 위함)
                MovementLogger.Log(_unitData.Id, "MELEE_PURSUIT_SKIP",
                    $"reason=FindNearestEnemyInDetectRangeNull currentPos={_unitData.Position}");
                yield break;
            }

            int targetId = firstTarget.Value.id;
            bool targetIsUnit = firstTarget.Value.isUnit;

            // 공격 슬롯 배정 — 접근 방향 기반(규칙 18).
            HexCoord? targetCoord = _combatUseCase.FindNearestEnemyPositionInDetectRange(_unitData);
            if (!targetCoord.HasValue)
            {
                // [실기 로그 — 2026-05-11] FindNearestEnemyInDetectRange는 적을 찾았지만
                //   FindNearestEnemyPositionInDetectRange가 좌표를 반환 못 하는 비정상 케이스.
                //   타겟 id는 잡혔지만 도메인 좌표 변환에 실패한 상황을 추적.
                MovementLogger.Log(_unitData.Id, "MELEE_PURSUIT_SKIP",
                    $"reason=NoTargetCoord targetId={targetId} targetIsUnit={targetIsUnit}");
                yield break;
            }

            // approach 벡터: 유닛(도메인) → 적(도메인). 도메인 좌표 변환 후 차로 계산.
            Vector3 unitDomainPos = ViewConverter.FromView(transform.position);
            Vector3 targetDomainPos = HexMetrics.HexToWorld(targetCoord.Value);
            Vector3 approachVecDomain = targetDomainPos - unitDomainPos;

            // contactRadius: 유닛 0.3f, 건물 0.5f. (전투 사거리와 일치하여 도달 즉시 전투 진입.)
            float contactRadius = targetIsUnit ? 0.3f : 0.5f;

            // [실기 로그 — 2026-05-11] ClaimByApproach 호출 컨텍스트(첫 진입).
            //   분산 분석 시점에 "어느 경로(초기/재선택/전투후)로 슬롯 클레임이 일어났는지"
            //   추적할 수 있도록 단계명(stage)을 함께 남긴다.
            //   AttackPositionManager 내부에서 호출 직후 MELEE_SLOT_DETAIL(상세)이 추가로 기록된다.
            float approachAnglePre = Mathf.Atan2(approachVecDomain.x, approachVecDomain.z) * Mathf.Rad2Deg;
            MovementLogger.Log(_unitData.Id, "MELEE_SLOT_DETAIL",
                $"stage=InitialClaim targetCoord={targetCoord.Value} targetId={targetId}"
                + $" approachAngle={approachAnglePre:F1} contactRadius={contactRadius:F2}");

            Vector3 attackSlotViewPos = _attackPositionManager.ClaimByApproach(
                targetCoord.Value, _unitData.Id, unitDomainPos, approachVecDomain, contactRadius);

            // [동적 슬롯 — 2026-05-11] 배정 실패(정원 초과 또는 각도 충돌)
            // → 이 타겟은 이미 포화 상태이므로 공격 슬롯을 잡지 않고 A* 이동으로 우회한다.
            // EnterMeleePursuitV3 진입 전에 이동 슬롯은 이미 해제됐으므로 별도 해제 불필요.
            if (attackSlotViewPos == Vector3.zero)
            {
                MovementLogger.Log(_unitData.Id, "MELEE_SLOT_BYPASS",
                    $"reason=ClaimFailed targetCoord={targetCoord.Value} targetId={targetId}");
                yield break;
            }

            _v2AttackSlotTargetCoord = targetCoord;

            // [실기 로그] 첫 공격 슬롯 클레임 — 적 좌표/슬롯 월드 위치/접근 각도.
            float approachAngle = approachAnglePre;
            MovementLogger.Log(_unitData.Id, "ATTACK_SLOT_CLAIM",
                $"targetCoord={targetCoord.Value} targetId={targetId} slotPos={attackSlotViewPos} approachAngle={approachAngle:F1}");

            // 이동 속도 (규칙 9 — A* 이동과 동일 스탯).
            float moveSeconds = _unitData.MoveSpeed > 0f ? 1f / _unitData.MoveSpeed : 1.0f;
            float worldSpeed = HexMetrics.TileHeight / moveSeconds;

            while (_unitData != null && _unitData.IsAlive)
            {
                // 적의 최신 월드 좌표 (적이 이동 중일 수 있음).
                Vector3 enemyViewPos = targetIsUnit
                    ? _positionProvider.GetUnitWorldPosition(targetId)
                    : _positionProvider.GetBuildingWorldPosition(targetId);

                // 타겟 파괴 — 사거리 내 다음 적 재선택(규칙 14). 없으면 종료.
                if (enemyViewPos == Vector3.zero)
                {
                    var next = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
                    if (!next.HasValue)
                    {
                        ReleaseV2AttackSlotIfClaimed();
                        yield break;
                    }

                    int prevTargetId = targetId;
                    ReleaseV2AttackSlotIfClaimed();
                    targetId = next.Value.id;
                    targetIsUnit = next.Value.isUnit;

                    MovementLogger.Log(_unitData.Id, "TARGET_SWITCH",
                        $"reason=prevDestroyed prevTargetId={prevTargetId} newTargetId={targetId}");

                    HexCoord? newCoord = _combatUseCase.FindNearestEnemyPositionInDetectRange(_unitData);
                    if (!newCoord.HasValue) yield break;

                    Vector3 newUnitDomain = ViewConverter.FromView(transform.position);
                    Vector3 newTargetDomain = HexMetrics.HexToWorld(newCoord.Value);
                    Vector3 newApproach = newTargetDomain - newUnitDomain;
                    contactRadius = targetIsUnit ? 0.3f : 0.5f;

                    // [실기 로그 — 2026-05-11] ClaimByApproach 호출 컨텍스트(타겟 파괴 후 재선택).
                    //   직전 타겟이 파괴되어 새로운 적을 향해 재클레임 하는 경로.
                    //   AttackPositionManager 내부에서 호출 직후 MELEE_SLOT_DETAIL(상세)이 추가로 기록된다.
                    float approachAngle2Pre = Mathf.Atan2(newApproach.x, newApproach.z) * Mathf.Rad2Deg;
                    MovementLogger.Log(_unitData.Id, "MELEE_SLOT_DETAIL",
                        $"stage=ReclaimAfterTargetDestroyed targetCoord={newCoord.Value} targetId={targetId}"
                        + $" approachAngle={approachAngle2Pre:F1} contactRadius={contactRadius:F2}");

                    attackSlotViewPos = _attackPositionManager.ClaimByApproach(
                        newCoord.Value, _unitData.Id, newUnitDomain, newApproach, contactRadius);

                    // [동적 슬롯 — 2026-05-11] 재선택 후 배정 실패 → 우회
                    if (attackSlotViewPos == Vector3.zero)
                    {
                        MovementLogger.Log(_unitData.Id, "MELEE_SLOT_BYPASS",
                            $"reason=ClaimFailedAfterTargetSwitch targetCoord={newCoord.Value} targetId={targetId}");
                        yield break;
                    }

                    _v2AttackSlotTargetCoord = newCoord;

                    float approachAngle2 = approachAngle2Pre;
                    MovementLogger.Log(_unitData.Id, "ATTACK_SLOT_CLAIM",
                        $"targetCoord={newCoord.Value} targetId={targetId} slotPos={attackSlotViewPos} approachAngle={approachAngle2:F1}");

                    yield return null;
                    continue;
                }

                // 공격 사거리 진입 → EnterCombatLoopV3. 종료 후 다음 적 또는 추적 종료.
                if (_combatUseCase.HasEnemyInRange(_unitData))
                {
                    MovementLogger.Log(_unitData.Id, "COMBAT_START",
                        $"mode=Melee targetId={targetId} targetCoord={_v2AttackSlotTargetCoord}");

                    yield return EnterCombatLoopV3();

                    if (_unitData == null || !_unitData.IsAlive)
                        yield break;

                    // 전투 종료 후 사거리 내에 다른 적이 남았으면 새 타겟 재선택.
                    if (!_combatUseCase.HasEnemyInDetectRange(_unitData))
                    {
                        MovementLogger.Log(_unitData.Id, "COMBAT_END",
                            $"reason=NoEnemyInDetectRange targetId={targetId}");
                        ReleaseV2AttackSlotIfClaimed();
                        yield break;
                    }

                    var nextEnemy = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
                    if (!nextEnemy.HasValue)
                    {
                        MovementLogger.Log(_unitData.Id, "COMBAT_END",
                            $"reason=FindNearestNull targetId={targetId}");
                        ReleaseV2AttackSlotIfClaimed();
                        yield break;
                    }

                    MovementLogger.Log(_unitData.Id, "COMBAT_END",
                        $"reason=SwitchTarget prevTargetId={targetId}");

                    int prevTargetId2 = targetId;
                    ReleaseV2AttackSlotIfClaimed();
                    targetId = nextEnemy.Value.id;
                    targetIsUnit = nextEnemy.Value.isUnit;

                    MovementLogger.Log(_unitData.Id, "TARGET_SWITCH",
                        $"reason=AfterCombat prevTargetId={prevTargetId2} newTargetId={targetId}");

                    HexCoord? nextCoord = _combatUseCase.FindNearestEnemyPositionInDetectRange(_unitData);
                    if (!nextCoord.HasValue) yield break;

                    Vector3 udPos = ViewConverter.FromView(transform.position);
                    Vector3 tdPos = HexMetrics.HexToWorld(nextCoord.Value);
                    Vector3 appr = tdPos - udPos;
                    contactRadius = targetIsUnit ? 0.3f : 0.5f;

                    // [실기 로그 — 2026-05-11] ClaimByApproach 호출 컨텍스트(전투 종료 후 재선택).
                    //   직전 적과의 전투 루프가 끝나고 사거리 내 다른 적을 향해 재클레임 하는 경로.
                    //   AttackPositionManager 내부에서 호출 직후 MELEE_SLOT_DETAIL(상세)이 추가로 기록된다.
                    float approachAngle3Pre = Mathf.Atan2(appr.x, appr.z) * Mathf.Rad2Deg;
                    MovementLogger.Log(_unitData.Id, "MELEE_SLOT_DETAIL",
                        $"stage=ReclaimAfterCombat targetCoord={nextCoord.Value} targetId={targetId}"
                        + $" approachAngle={approachAngle3Pre:F1} contactRadius={contactRadius:F2}");

                    attackSlotViewPos = _attackPositionManager.ClaimByApproach(
                        nextCoord.Value, _unitData.Id, udPos, appr, contactRadius);
                    _v2AttackSlotTargetCoord = nextCoord;

                    float approachAngle3 = approachAngle3Pre;
                    MovementLogger.Log(_unitData.Id, "ATTACK_SLOT_CLAIM",
                        $"targetCoord={nextCoord.Value} targetId={targetId} slotPos={attackSlotViewPos} approachAngle={approachAngle3:F1}");

                    yield return null;
                    continue;
                }

                // 감지 사거리 이탈 — 추적 종료.
                if (!_combatUseCase.HasEnemyInDetectRange(_unitData))
                {
                    MovementLogger.Log(_unitData.Id, "COMBAT_END",
                        $"reason=EnemyEscapedDetect targetId={targetId}");
                    ReleaseV2AttackSlotIfClaimed();
                    yield break;
                }

                // ── 슬롯으로 직선 이동 + 매 프레임 적 방향 회전 ──
                Vector3 moveDir = attackSlotViewPos - transform.position;
                moveDir.y = 0f;
                float dist = moveDir.magnitude;
                if (dist > 0.01f)
                {
                    // 적 방향(실제 적 위치)으로 회전 — 시각적으로 자연스러운 추적.
                    float pursuitAngle = CalculateAttackAngle(enemyViewPos);
                    Quaternion targetRot = Quaternion.Euler(0f, pursuitAngle, 0f);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation, targetRot, CombatRotationSpeed * Time.deltaTime);

                    // 슬롯 위치로 직선 이동.
                    transform.position += moveDir.normalized * worldSpeed * Time.deltaTime;
                }

                yield return null;
            }

            // 사망/외부 종료 — 슬롯 cleanup은 호출 측에서 한 번 더 보장하지만, 안전망으로 한 번 더.
            ReleaseV2AttackSlotIfClaimed();
        }

        /// <summary>
        /// [V3] 원거리 정지 전투(규칙 13/15).
        ///
        /// 동작:
        ///   - 이동 슬롯을 유지한 채 그 자리에서 EnterCombatLoopV3 호출.
        ///   - 전투 종료 후 별도 처리 없이 호출 측(메인 코루틴)으로 복귀하면
        ///     같은 슬롯 위치에서 이번 Lerp가 자연스럽게 재개된다(규칙 15).
        /// </summary>
        private IEnumerator EnterStationaryCombatV3()
        {
            if (_combatUseCase == null || _unitData == null)
                yield break;

            // 진입 시점에 사거리 안에 적이 없으면 즉시 yield break.
            if (!_combatUseCase.HasEnemyInRange(_unitData))
                yield break;

            // ──────────────────────────────────────────────────────
            // [규칙 18] 공격 슬롯 클레임 — 같은 적을 노리는 원거리 유닛 분산.
            //   - 같은 자리에서 멈춰 겹치는 문제를 해결하기 위해, 전투 진입 직전에
            //     적 타겟 타일을 기준으로 접근 방향 기반 12방향 슬롯 중 하나를 점유한다.
            //   - 슬롯은 EnterCombatLoopV3 동안 유지되며, 종료 시 명시적으로 해제한다.
            //   - 타겟 정보를 조회할 수 없으면 슬롯 없이 그대로 전투 루프에 진입(fallback).
            //   - 시각적으로 유닛 위치를 이동시키지는 않는다(규칙 13 — 원거리는 그 자리에서 공격).
            //     슬롯 클레임의 목적은 "이 슬롯은 내가 차지했다"는 점유 표시여서, 후속 원거리
            //     유닛의 ClaimByApproach가 이 슬롯을 피해 다른 슬롯을 선택하도록 만드는 것.
            // ──────────────────────────────────────────────────────
            bool attackSlotClaimed = false;
            if (_attackPositionManager != null)
            {
                // 타겟 좌표와 타겟 유닛/건물 여부 조회.
                HexCoord? targetCoord = _combatUseCase.FindNearestEnemyPositionInDetectRange(_unitData);
                var targetInfo = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);

                if (targetCoord.HasValue && targetInfo.HasValue)
                {
                    // approach 벡터: 유닛(도메인) → 적(도메인). 도메인 좌표 변환 후 차로 계산.
                    //   - 뷰 좌표(transform.position)를 ViewConverter.FromView로 도메인 좌표로 환원,
                    //     적 도메인 좌표는 HexMetrics.HexToWorld로 계산.
                    //   - 두 도메인 좌표의 차이 벡터 = 적을 향한 접근 방향.
                    Vector3 unitDomainPos = ViewConverter.FromView(transform.position);
                    Vector3 targetDomainPos = HexMetrics.HexToWorld(targetCoord.Value);
                    Vector3 approachVecDomain = targetDomainPos - unitDomainPos;

                    // contactRadius: 유닛 타겟=0.3f, 건물 타겟=0.5f. (전투 사거리와 일치 — 규칙 18.)
                    float contactRadius = targetInfo.Value.isUnit ? 0.3f : 0.5f;

                    // 12방향 슬롯 중 접근 방향에 가장 가까운 빈 슬롯을 받는다.
                    Vector3 attackSlotViewPos = _attackPositionManager.ClaimByApproach(
                        targetCoord.Value, _unitData.Id, unitDomainPos, approachVecDomain, contactRadius);

                    _v2AttackSlotTargetCoord = targetCoord;
                    attackSlotClaimed = true;

                    MovementLogger.Log(_unitData.Id, "RANGED_ATTACK_SLOT_CLAIM",
                        $"targetCoord={targetCoord.Value} slotPos={attackSlotViewPos}");
                }
            }

            // 실제 공격/쿨다운 루프 — 사거리 내에 적이 있는 동안 유지.
            yield return EnterCombatLoopV3();

            // 전투 종료 후 공격 슬롯 해제 — 다른 유닛이 이 슬롯을 사용할 수 있도록.
            //   (StopMovement/OnEntityDied 경로에서도 해제되지만, 정상 종료 경로의 안전망.)
            if (attackSlotClaimed)
            {
                ReleaseV2AttackSlotIfClaimed();
            }
        }

        /// <summary>
        /// [V3] 공격 사거리 도달 후 공격/쿨다운 루프 (규칙 13 — 공격 중 이동 없음).
        ///
        /// 멀티플레이/싱글플레이 분기:
        ///   - 멀티플레이: OnUnitEnteredCombat 1회 발행 → NetworkCombatController가 RPC 전파.
        ///                 사거리 내인 동안 yield, 쿨다운 만료 후 사거리 재확인 시 재진입,
        ///                 사거리 이탈 시 OnUnitWalkStarted 발행 후 종료.
        ///   - 싱글플레이: 매 프레임 TryAttack(쿨다운 내부 체크), 쿨다운 대기, ClearCombatState로 종료.
        ///
        /// 재진입은 재귀 호출 대신 yield return으로 꼬리 재진입(스택 안전).
        /// </summary>
        private IEnumerator EnterCombatLoopV3()
        {
            if (_combatUseCase == null || _unitData == null) yield break;
            if (!_combatUseCase.HasEnemyInRange(_unitData)) yield break;

            if (NetworkContext.IsNetworkActive)
            {
                // 멀티플레이 서버: 전투 진입 이벤트 발행 (RPC 전파는 NetworkCombatController 책임).
                GameEvents.OnUnitEnteredCombat.OnNext(_unitData.Id);

                // 사거리 내인 동안 대기 (이동 없음 — 규칙 13).
                while (_unitData.IsAlive && _combatUseCase.HasEnemyInRange(_unitData))
                    yield return null;

                if (!_unitData.IsAlive) yield break;

                // 현재 공격 사이클이 끝날 때까지 쿨다운 대기.
                while (_unitData.IsAlive && _unitData.AttackCooldownRemaining > 0f)
                    yield return null;

                if (!_unitData.IsAlive) yield break;

                // 사이클 완료 후 사거리에 다시 들어왔으면 같은 루프 재진입 (꼬리 재진입).
                if (_combatUseCase.HasEnemyInRange(_unitData))
                {
                    yield return EnterCombatLoopV3();
                    yield break;
                }

                // 이동 재개 애니메이션 — Walk CrossFade.
                if (_animator != null)
                {
                    var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                    if (stateInfo.shortNameHash == StateWalk)
                        _animator.speed = 1f;
                    else
                        _animator.CrossFadeInFixedTime(StateWalk, _attackToWalkBlend, 0);
                }
                GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);
            }
            else
            {
                // 싱글플레이: 매 프레임 TryAttack(내부 쿨다운 체크).
                while (_unitData.IsAlive && _combatUseCase.HasEnemyInRange(_unitData))
                {
                    _combatUseCase.TryAttack(_unitData);
                    yield return null;
                }

                if (!_unitData.IsAlive) yield break;

                while (_unitData.IsAlive && _unitData.AttackCooldownRemaining > 0f)
                    yield return null;

                if (!_unitData.IsAlive) yield break;

                if (_combatUseCase.HasEnemyInRange(_unitData))
                {
                    yield return EnterCombatLoopV3();
                    yield break;
                }

                _combatUseCase.ClearCombatState(_unitData.Id);

                if (_animator != null)
                {
                    var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                    if (stateInfo.shortNameHash == StateWalk)
                        _animator.speed = 1f;
                    else
                        _animator.CrossFadeInFixedTime(StateWalk, _attackToWalkBlend, 0);
                }
            }
        }

        /// <summary>
        /// [V3] 근접 전투 종료 후 A* 재개 시 출발 타일을 결정한다 (규칙 11/15).
        ///
        /// 동작:
        ///   1) FindForwardClosestTile로 현재 월드 위치 기준 앞쪽 가장 가까운 walkable 타일 탐색.
        ///   2) ProcessStep으로 도메인 위치를 그 타일로 갱신 + FROM(이전 _unitData.Position) 해제.
        ///   3) 같은 타일이면 ProcessStep 생략 — 점유 누수 방지.
        ///   4) [BUG-002] transform.position을 forwardTile에 강제 스냅 — 도메인↔뷰 불일치로 인한 순간이동 방지.
        ///   5) Walk 애니메이션 재개.
        ///   6) finalTarget으로 RequestMove → 새 path 반환. 호출 측이 외부 while로 재진입.
        ///
        /// 뒤쪽 타일로 복귀하지 않는다(규칙 11/15).
        /// </summary>
        private List<HexCoord> ResumeFromForwardTileV3(HexCoord finalTarget)
        {
            if (_movementUseCase == null || _unitData == null || !_unitData.IsAlive)
                return null;

            // 현재 뷰 좌표 → 도메인 좌표 역변환.
            Vector3 unitDomainPos = ViewConverter.FromView(transform.position);

            // 앞쪽 가장 가까운 walkable 타일 — 뒤쪽 절대 금지 규칙은 UseCase 내부에서 보장.
            HexCoord forwardTile = _movementUseCase.FindForwardClosestTile(unitDomainPos, finalTarget);

            // 같은 타일이면 점유 변동이 없으므로 ProcessStep 호출 자체를 생략(점유 누수 방지).
            if (forwardTile != _unitData.Position)
            {
                _movementUseCase.RegisterOccupancyMove(_unitData.Position, forwardTile, _unitData.Type);
                _movementUseCase.ProcessStep(_unitData, _unitData.Position, forwardTile);
            }
            _unitData.ClaimedTile = null;

            // ────────────────────────────────────────────────────────────
            // [BUG-002] 뷰 좌표(transform.position)를 forwardTile에 강제 스냅.
            //
            // 왜 필요한가:
            //   근접 전투 종료 시점에 transform.position은 공격 슬롯(=적 근처)이고,
            //   _unitData.Position만 forwardTile로 갱신되면 도메인-뷰 좌표가 불일치한다.
            //   이후 메인 코루틴이 재진입하여 다음 타일을 향해 Lerp를 시작하면,
            //   출발점(공격 슬롯) → 도착점(forwardTile+1 슬롯)으로 1칸 이동 시간 안에
            //   먼 거리를 점프해 시각적으로 순간이동이 발생한다.
            //
            // 어떻게 해결:
            //   forwardTile의 도메인 월드 좌표를 ViewConverter로 뷰 좌표로 바꾼 뒤
            //   UnitYOffset을 더해 transform.position에 즉시 스냅한다.
            //   ViewConverter.ToView는 Red 팀의 Y 반전을 자동 적용하므로 팀 무관하게 안전.
            // ────────────────────────────────────────────────────────────
            Vector3 forwardWorld = HexMetrics.HexToWorld(forwardTile);
            Vector3 forwardView = ViewConverter.ToView(forwardWorld);
            forwardView.y += HexMetrics.UnitYOffset;
            transform.position = forwardView;

            // Walk 애니메이션 재개 — 전투 중 Attack 상태였을 수 있음.
            if (_animator != null)
            {
                var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.shortNameHash == StateWalk)
                    _animator.speed = 1f;
                else
                    _animator.CrossFadeInFixedTime(StateWalk, _attackToWalkBlend, 0);
            }
            if (NetworkContext.IsNetworkActive)
                GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);

            // 새 path 계산 — finalTarget으로 RequestMove. 실패 시 null 반환(호출 측이 종료 처리).
            List<HexCoord> newPath = _movementUseCase.RequestMove(_unitData, finalTarget);
            return newPath;
        }

        /// <summary>
        /// [V3] 이동 종료 시 슬롯/점유/콜백을 일관되게 정리한다.
        /// 외부 while 종료(완주/사망/RESUME 실패) 직후 + path 비정상 종료 직후 등에서 호출.
        /// </summary>
        private void MoveCleanupAndCompleteV3()
        {
            // [실기 로그] 이동 종료 cleanup — 사망/완주/StopMovement 등 모든 종료 경로에서 호출.
            // 사망 경로(_unitData == null 또는 IsAlive == false)이면 UNIT_DIED 마커 추가.
            if (_unitData == null || !_unitData.IsAlive)
            {
                int idForLog = _unitData != null ? _unitData.Id : -1;
                MovementLogger.Log(idForLog, "UNIT_DIED",
                    $"context=MoveCleanupAndCompleteV3 moveSlot={_v2MoveSlotTile} attackSlot={_v2AttackSlotTargetCoord}");
            }

            if (_unitData != null)
                _unitData.ClaimedTile = null;

            // 슬롯/공격슬롯/점유 모두 해제 — 이미 풀려 있어도 헬퍼 안에서 안전하게 무시.
            ReleaseV2MoveSlotIfClaimed();
            ReleaseV2AttackSlotIfClaimed();

            // [V3] pending occupancy 해제 — 인라인 처리 (V1 헬퍼가 제거됐으므로 직접 처리).
            if (_pendingOccupancyTile != default(HexCoord)
                && _movementUseCase != null && _unitData != null)
            {
                _movementUseCase.ReleaseOccupancy(_pendingOccupancyTile, _unitData.Type);
                _pendingOccupancyTile = default;
            }

            _moveCoroutine = null;

            // 1회성 이동 완료 콜백 (랠리→Castle 자동 이동 체인 등에서 사용).
            var callback = OnMoveComplete;
            OnMoveComplete = null;
            callback?.Invoke();
        }

        // ====================================================================
        // 타격 반응 (Animation Event → HitReaction)
        // ====================================================================

        /// <summary>
        /// Animation Event에서 AnimationEventRelay를 통해 호출.
        /// 공격 애니메이션의 타격 프레임에 맞춰 피격 대상에게 시각적 반응(스케일 펀치)을 적용.
        /// 실제 데미지 로직과는 무관 — 순수 비주얼 피드백.
        /// </summary>
        public void OnAttackHit()
        {
            if (_unitData == null || !_unitData.IsAlive) return;

            StartCoroutine(HitReactionCoroutine());
        }

        /// <summary>
        /// 스케일 펀치로 타격감을 표현하는 코루틴.
        /// 0.85배 축소 → 0.05초 후 원래 크기 복원.
        /// </summary>
        private IEnumerator HitReactionCoroutine()
        {
            Vector3 originalScale = transform.localScale;
            transform.localScale = originalScale * 0.85f;
            yield return new WaitForSeconds(0.05f);
            transform.localScale = originalScale;
        }

        // ====================================================================
        // Walk 애니메이션 — 클라이언트 전용 (NetworkCombatController의 ClientRpc에서 호출)
        // ====================================================================

        /// <summary>
        /// Walk 애니메이션 시작. NetworkCombatController의 StartWalkAnimationClientRpc에서 호출.
        /// 클라이언트 전용 — 서버는 MoveAlongPath에서 직접 Animator를 제어.
        /// 이미 Walk 상태면 리셋하지 않고 speed만 복원.
        /// </summary>
        public void StartWalkAnimation()
        {
            // _animator가 null이면 lazy-init 시도 (Initialize() 전에 RPC가 도착한 경우 대응)
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
            if (_animator == null) return;

            _animator.speed = 1f;
            _animator.CrossFadeInFixedTime(StateWalk, _idleToWalkBlend, 0);
        }

        // StopWalkAnimation() 제거 — Idle 상태가 없으므로 Walk 정지 전용 메서드 불필요.
        // Walk→Attack 전환: StartCombatAnimation()에서 Attack CrossFade 직접 호출.
        // Walk→Walk: 이동 재개 시 StartWalkAnimation()에서 Walk CrossFade 호출.

        // WaitForAttackCycleEnd() 제거됨 (2026-04-03).
        // Animator normalizedTime 기반 대기 → 도메인 AttackCooldownRemaining 기반 대기로 교체.
        // AttackCooldown = 공격 클립 길이이므로 쿨다운 만료 = 한 사이클 완료.
        // Animator 상태 의존성을 제거하여 CrossFade 블렌딩 중 상태 판별 오류를 근본적으로 방지.

        // ====================================================================
        // 전투 애니메이션 — 상태 기반 (RPC/이벤트에서 호출)
        // ====================================================================

        /// <summary>
        /// 전투 시작. 타겟 방향으로 즉시 스냅 회전 후 Attack CrossFade.
        /// Attack 클립은 Loop Time=ON이므로 별도 루프 로직 불필요 — CrossFade 1회 호출로 무한 루프.
        ///
        /// 호출 경로:
        ///   멀티플레이 → NetworkCombatController.StartCombatClientRpc → UnitView.StartCombatAnimation
        ///   싱글플레이 → GameEvents.OnCombatStarted → UnitView.StartCombatAnimation
        /// </summary>
        /// <param name="targetId">타겟 엔티티의 Id.</param>
        /// <param name="targetIsUnit">true=유닛, false=건물.</param>
        public void StartCombatAnimation(int targetId, bool targetIsUnit)
        {
            if (_unitData == null || !_unitData.IsAlive) return;

            // 타겟 방향으로 즉시 스냅 회전
            // 서버에서 즉시 스냅 → NetworkTransform이 클라이언트에 보간 전달
            float angle = CalculateAttackAngle(GetTargetWorldPos(targetId, targetIsUnit));
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            // Attack CrossFade 시작 — Loop Time=ON이므로 자동 루프
            if (_animator != null)
            {
                _animator.speed = 1f;
                _animator.CrossFadeInFixedTime(StateAttack, _toAttackBlend, 0);
            }

            // 공격 중 타겟 Transform 참조 저장 → Update()에서 매 프레임 추적
            _combatTargetTransform = GetTargetTransform(targetId, targetIsUnit);

            // 멀티플레이 타이밍 문제 대비: Transform 참조가 나중에 null이 되더라도
            // 백업 ID로 재조회할 수 있도록 ID를 저장한다.
            _combatTargetId = targetId;
            _combatTargetIsUnit = targetIsUnit;
        }

        /// <summary>
        /// 전투 중 타겟 변경. 회전만 업데이트, 애니메이션 상태 변경 없음.
        /// Attack 루프는 이미 재생 중이므로 끊김 없이 방향만 전환.
        ///
        /// 호출 경로:
        ///   멀티플레이 → NetworkCombatController.ChangeTargetClientRpc → UnitView.ChangeTarget
        ///   싱글플레이 → GameEvents.OnCombatTargetChanged → UnitView.ChangeTarget
        /// </summary>
        /// <param name="targetId">새 타겟 엔티티의 Id.</param>
        /// <param name="targetIsUnit">true=유닛, false=건물.</param>
        public void ChangeTarget(int targetId, bool targetIsUnit)
        {
            if (_unitData == null || !_unitData.IsAlive) return;

            // 추적 대상을 새 타겟으로 교체 → Update()가 새 타겟 방향 추적 시작
            _combatTargetTransform = GetTargetTransform(targetId, targetIsUnit);

            // 새 타겟의 ID로 백업을 갱신한다.
            _combatTargetId = targetId;
            _combatTargetIsUnit = targetIsUnit;
        }

        /// <summary>
        /// 전투 종료 알림. 멀티플레이에서는 의도적으로 아무것도 하지 않음.
        ///
        /// 호출 경로:
        ///   멀티플레이 → NetworkCombatController.StopCombatClientRpc → UnitView.StopCombatAnimation
        ///   싱글플레이 → GameEvents.OnCombatStopped → UnitView.StopCombatAnimation
        ///
        /// 비어있는 이유 (멀티플레이):
        ///   Walk 전환은 서버가 이동을 재개할 때 StartWalkAnimationClientRpc로 명시적으로 신호를 보냄.
        ///   StopCombatClientRpc 도착 시점과 서버의 실제 이동 재개 시점이 다를 수 있으므로,
        ///   여기서 Walk CrossFade를 즉시 호출하면 서버가 아직 쿨다운(최대 3초) 대기 중인 동안
        ///   클라이언트만 Walk 애니메이션이 재생되고 유닛이 실제로 이동하지 않는 불일치가 발생.
        ///   → Walk 전환 타이밍은 StartWalkAnimationClientRpc에 완전히 위임.
        ///
        /// 싱글플레이:
        ///   MoveAlongPath 내부에서 Walk CrossFade를 직접 호출하므로 이 메서드 불필요.
        /// </summary>
        public void StopCombatAnimation()
        {
            // 회전 추적 중단 — null로 초기화하면 Update()가 자동으로 건너뜀
            _combatTargetTransform = null;

            // Transform 참조와 함께 백업 ID도 초기화하여 Update()의 재조회 시도를 차단한다.
            _combatTargetId = -1;

            // 의도적으로 비워둠 — 위 summary 참조.
        }
    }
}
