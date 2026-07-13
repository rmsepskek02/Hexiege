// ============================================================================
// UnitView.cs
// 유닛의 비주얼(이동, 방향, 애니메이션 상태)을 담당하는 컴포넌트.
//
// 회전 동기화 방식:
//   - 회전은 즉시 스냅(Quaternion.Euler)으로 적용한다.
//   - 멀티플레이에서는 서버가 즉시 스냅한 rotation을 NetworkTransform이 클라이언트에 보간 전달한다.
//   - Red 클라이언트 rotation 보정은 NetworkUnit.LateUpdate()에서 일괄 처리한다.
//
// 프리팹 구조:
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
    // IUnitView 인터페이스 구현.
    //   UnitFactory(Infrastructure)가 Presentation 구체 타입 대신 인터페이스로 Initialize를 호출하도록.
    //   SetDependencies는 Infrastructure 구체 타입을 받으므로 인터페이스에 포함하지 않음.
    public class UnitView : MonoBehaviour, Hexiege.Application.IUnitView
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
        // 원거리/근접 판정 경계
        // ====================================================================

        /// <summary>
        /// 원거리 유닛 판정 경계. AttackRange가 이 값 이상이면 원거리 유닛으로 간주하여
        /// 공격 시 트레이서(발사체) 연출을 재생한다. 미만이면 근접 유닛으로 즉시 피격 연출한다.
        /// UnitCombatUseCase의 근접 판정(AttackRange &lt; 1.0f)과 동일한 경계값이다.
        /// </summary>
        private const float RangedAttackThreshold = 1.0f;

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

        /// <summary>
        /// 공격 VFX를 재생할 기준 위치/회전을 제공하는 빈 자식 Transform(예: 총구 끝).
        /// 프리팹에서 무기 끝 같은 위치에 빈 GameObject("VfxSpawnPoint")를 두고 이 슬롯에 연결한다.
        /// 유닛은 공격 시 적 방향으로 이미 회전하므로, 이 Transform의 월드 회전이 곧 발사 방향이 된다.
        /// 연결하지 않은 유닛(피스톨러 외)은 기존처럼 유닛 발 위치(transform.position)로 폴백한다.
        /// </summary>
        [Tooltip("공격 VFX 스폰 기준 Transform(총구 등). 비어 있으면 유닛 발 위치로 폴백.")]
        [SerializeField] private Transform _vfxSpawnPoint;

        // ====================================================================
        // 내부 참조
        // ====================================================================

        /// <summary> 이 유닛에 연결된 Domain 데이터. </summary>
        private UnitData _unitData;

        /// <summary> NetworkUnit.OnNetworkDespawn에서 클라이언트 이펙트 재생 시 타입 참조용. </summary>
        public UnitData UnitData => _unitData;

        /// <summary> Animator — 상태 전환 및 애니메이션 재생. </summary>
        private Animator _animator;

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

        /// <summary> 현재 이동 코루틴. null이면 정지 상태. </summary>
        private Coroutine _moveCoroutine;

        /// <summary>
        /// 현재 이동 명령의 최종 목적지(예: 적 성, 랠리포인트).
        /// MoveTo(path)가 호출될 때 path[path.Count - 1]을 저장하며,
        /// 건물 변경 시 모든 유닛 경로를 재계산하는 트리거에서
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

        // 회전 속도 (초당 각도) — 인스펙터에서 조정 가능.
        // 전투 중 타겟 추적, A* 이동 중 방향 전환, 전투 종료 후 정렬 모두 동일한 속도로 회전한다.
        // 270도/s 기준: 반대 방향(180도)에서도 약 0.67초 내 전환 완료.
        // SerializeField로 노출되어 있어 프리팹별/유닛별 회전감 차이를 데이터로 조정할 수 있다.
        [SerializeField] private float _rotationSpeed = 270f;

        // [방어적 백업] Transform 참조가 null이 될 경우 팩토리에서 재조회하기 위한 ID 백업.
        // 멀티플레이 타이밍 문제(StopCombatAnimation / ChangeTarget 호출 순서 역전 등)로
        // _combatTargetTransform이 null로 덮어써지는 경우에도 추적을 복구할 수 있도록 한다.
        private int _combatTargetId = -1;
        private bool _combatTargetIsUnit = true;

        // ────────────────────────────────────────────────────────────────────
        // 전투 추격(EnterCombatPursuitV3 실행 중) 여부 플래그.
        //
        // 왜 필요한가:
        //   IsInCombat()는 _combatTargetTransform != null만 판정합니다.
        //   _combatTargetTransform은 "실제 공격 시작"(StartCombatAnimation 호출) 시점에만 set되므로,
        //   적을 감지하고 추격 중인 단계(아직 공격 사거리에 미도달)에서는 false가 됩니다.
        //   이 상태에서 건물 생성/파괴 → RepathAllAliveUnits → OnPathInvalidated가 호출되면
        //   추격 중인 유닛도 repath 대상이 되어 추격 코루틴이 끊기고 다시 이동 코루틴이 시작되어
        //   유닛이 한 프레임 이상 시각적으로 멈춥니다.
        //
        // 사용법:
        //   - EnterCombatPursuitV3 진입 직후 true로 set.
        //   - 모든 yield break 직전 + 함수 끝에서 false로 reset.
        //     (Unity 코루틴(IEnumerator)에서는 try/finally가 동작하지 않으므로 명시적 처리 필요.)
        //   - IsInCombat()이 (_combatTargetTransform != null || _isInCombatPursuit)로 평가하여
        //     추격 단계도 "전투 중"으로 판정하게 합니다.
        // ────────────────────────────────────────────────────────────────────
        private bool _isInCombatPursuit = false;

        // ────────────────────────────────────────────────────────────────────
        // A* 이동 단계 여부 플래그 (혼잡도 시스템 연동용).
        //
        // 왜 필요한가:
        //   혼잡도 누적은 "정상 A* 이동" 중에만 의미가 있다. 전투 추격(EnterCombatPursuitV3)
        //   단계에서는 유닛이 적을 향해 직선 이동하므로 타일 진입 이벤트를 발행하면
        //   엉뚱한 타일에 혼잡도가 쌓일 수 있다.
        //
        // 사용법:
        //   - MoveAlongPathV3 시작 시 true로 set.
        //   - EnterCombatPursuitV3 진입 직전(detect 사거리 적 발견)에 false로 set.
        //   - 전투 종료 후 ResumeFromForwardTileV3로 새 path를 받으면 다시 true.
        //   - MoveCleanupAndCompleteV3(이동 완료)에서 false로 reset.
        // ────────────────────────────────────────────────────────────────────
        private bool _isAStarMoving = false;

        // ────────────────────────────────────────────────────────────────────
        // 건물 생성/파괴 시 유닛 멈춤 현상 제거용 — 부드러운 경로 교체 패턴.
        //
        // 왜 필요한가:
        //   OnPathInvalidated가 호출될 때 MoveTo(newPath)로 현재 이동 코루틴을 강제로
        //   StopCoroutine + StartCoroutine하면 1~2 프레임의 공백이 생겨 유닛이 잠깐 멈춘다.
        //   이를 피하기 위해 코루틴을 멈추지 않고 경로만 교체하는 예약 방식을 사용한다.
        //
        // 동작:
        //   _pendingPath: OnPathInvalidated가 새 경로를 여기에 "예약"만 한다.
        //                 코루틴은 그대로 돌면서 매 타일 도착 시점에 _pendingPath를 검사해
        //                 비어있지 않으면 path 변수를 교체하고 외부 while로 재진입한다.
        //                 코루틴 자체는 멈추지 않으므로 시각적 끊김 없음.
        //
        //   _currentNextTileCoord: 지금 Lerp 중인 "다음 도착 타일"의 좌표를 보관.
        //                          Lerp 시작 직전 set, Lerp 완료 직후 null.
        //                          OnPathInvalidated에서 "현재 향하는 타일에 건물이 새로 생긴
        //                          (=walkable이 아닌)" 케이스를 감지해야 할 때 사용한다.
        //                          이 경우는 예약 방식으로는 늦으므로 기존처럼 즉시 MoveTo한다.
        // ────────────────────────────────────────────────────────────────────
        private List<HexCoord> _pendingPath = null;
        private HexCoord? _currentNextTileCoord = null;

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
            // Quaternion.RotateTowards: 현재 회전에서 목표 회전으로 초당 _rotationSpeed 각도만큼 이동.
            // 타겟이 멀리 있을수록 회전이 느리게 보이지만, 일반적인 유닛 이동 속도에서는 즉각 추적처럼 보임.
            // 타겟 교체 시(ChangeTarget) 방향이 크게 바뀌어도 자연스럽게 전환.
            float angle = CalculateAttackAngle(_combatTargetTransform.position);
            Quaternion targetRotation = Quaternion.Euler(0f, angle, 0f);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _rotationSpeed * Time.deltaTime
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

            // Animator 캐시 — 자식 오브젝트에 있을 수 있으므로 GetComponentInChildren 사용
            _animator = GetComponentInChildren<Animator>();

            // 스폰 시 Facing 방향으로 즉시 rotation 설정.
            // 서버에서 즉시 스냅하면 NetworkTransform이 이 값을 클라이언트에 자동 보간 전달.
            // ViewConverter.IsFlipped 보정은 NetworkUnit.LateUpdate()에서 일괄 처리하므로 불필요.
            int index = (int)_unitData.Facing;
            if (index >= 0 && index < DirectionAngles.Length)
            {
                float spawnAngle = DirectionAngles[index];
                transform.rotation = Quaternion.Euler(0f, spawnAngle, 0f);
            }

            // ────────────────────────────────────────────────────────────────
            // [Phase 2 후속 — 적용 순서 틈 봉합] Initialize 완료 직후 애니메이션 상태 재적용.
            //
            //   왜 필요한가 (초급자용 설명):
            //     클라이언트에서 같은 GameObject의 NetworkUnit이 스폰되는 순간(OnNetworkSpawn) 서버의
            //     현재 애니메이션 상태를 즉시 적용하는데, 그 시점이 이 Initialize(=위에서 _animator를
            //     캐시한 지점)보다 "먼저" 실행될 수 있다. 그때 메시/Animator가 아직 준비되지 않았으면
            //     StartWalkAnimation이 조용히 반환하고, 레벨 값(NetworkVariable)은 그대로라 다시 통지가
            //     오지 않아 재시도가 없다 → 간헐적으로 걷기 애니메이션이 무음 실패한다.
            //
            //   해결:
            //     _animator를 캐시한 "지금" 같은 GameObject의 NetworkUnit에서 현재 상태 값을 한 번 더
            //     읽어 재적용한다. 조기 적용이 실패했더라도 준비된 Animator로 반드시 재적용되어 틈이 닫힌다.
            //     레벨(값 기반) 동기화라 "현재 값 재적용"은 멱등하여 무해하고, 서버(호스트)/싱글플레이는
            //     ReapplyAnimStateToView 내부 가드(IsSpawned/IsServer)로 스킵된다.
            //     (UnitView→NetworkUnit은 같은 프리팹 컴포넌트 접근 — 기존 관례이며 레이어 위반이 아니다.)
            // ────────────────────────────────────────────────────────────────
            var networkUnit = GetComponent<NetworkUnit>();
            if (networkUnit != null) networkUnit.ReapplyAnimStateToView();
        }

        /// <summary>
        /// 외부 의존성 주입. GameBootstrapper에서 모든 컴포넌트 생성 후 호출.
        /// </summary>
        public void SetDependencies(
            UnitMovementUseCase movementUseCase, UnitCombatUseCase combatUseCase,
            UnitFactory unitFactory = null, BuildingFactory buildingFactory = null,
            IEntityPositionProvider positionProvider = null)
        {
            _movementUseCase = movementUseCase;
            _combatUseCase = combatUseCase;
            _unitFactory = unitFactory;
            _buildingFactory = buildingFactory;
            _positionProvider = positionProvider;

            // 전투 상태 변화 이벤트 구독 — 싱글플레이 전용.
            // 멀티플레이에서는 NetworkCombatController가 GameEvents.OnNetworkCombatStarted 등을 발행한다.
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
            else
            {
                // 멀티플레이 전용 전투/Walk 이벤트 구독.
                // NetworkCombatController가 ClientRpc 핸들러에서 UnitView를 직접 GetComponent로 잡지 않도록,
                // GameEvents.OnNetworkCombatStarted/등을 발행하면 본인 Id에 해당하는 이벤트만 처리한다.
                //
                // AddTo(this)로 GameObject 파괴 시 자동 구독 해제 — 재경기/씬 전환 시 누수 방지.

                // 멀티플레이 전투 시작
                GameEvents.OnNetworkCombatStarted
                    .Subscribe(e =>
                    {
                        if (_unitData != null && e.UnitId == _unitData.Id)
                        {
                            StartCombatAnimation(e.TargetId, e.TargetIsUnit);
                        }
                    })
                    .AddTo(this);

                // 멀티플레이 타겟 변경
                GameEvents.OnNetworkCombatTargetChanged
                    .Subscribe(e =>
                    {
                        if (_unitData != null && e.UnitId == _unitData.Id)
                        {
                            ChangeTarget(e.TargetId, e.TargetIsUnit);
                        }
                    })
                    .AddTo(this);

                // 멀티플레이 전투 종료
                GameEvents.OnNetworkCombatStopped
                    .Subscribe(e =>
                    {
                        if (_unitData != null && e.UnitId == _unitData.Id)
                        {
                            StopCombatAnimation();
                        }
                    })
                    .AddTo(this);

                // 멀티플레이 Walk 시작은 애니메이션 상태 레벨 동기화로 처리한다.
                //   서버가 NetworkUnit._animState = Walk로 쓰면, 클라이언트 NetworkUnit이
                //   값 변경(OnValueChanged) 및 스폰 시 현재 값을 읽어 UnitView.StartWalkAnimation()을
                //   직접 호출한다(스폰 레이스로 신호가 유실될 수 없음).
            }

            // 사망 이벤트 구독 — 이 유닛이 사망하면 GameObject 파괴.
            // 유닛 전용 이벤트(OnUnitDied)를 구독한다.
            // UnitDiedEvent.Unit이 UnitData 강타입이라 별도 캐스트 없이 직접 비교만 하면 된다.
            GameEvents.OnUnitDied
                .Subscribe(e =>
                {
                    // 이 UnitView 인스턴스가 들고 있는 _unitData와 일치할 때만 자기 자신을 파괴.
                    // 다른 유닛의 사망 이벤트는 무시.
                    if (_unitData != null && e.Unit == _unitData)
                    {
                        // 사망 시 speed 복원 후 IsDead bool 설정 (Animator 트랜지션)
                        if (_animator != null)
                        {
                            _animator.speed = 1f;
                            _animator.SetBool(AnimIsDead, true);
                        }

                        // ────────────────────────────────────────────────────
                        // 멀티플레이 클라이언트:
                        //   사망 이펙트 재생과 GameObject 파괴를 모두
                        //   NetworkUnit.OnNetworkDespawn()에서 처리한다.
                        //   (서버가 NetworkObject.Despawn(destroy:true)를 호출하면
                        //    NGO가 이 클라이언트의 OnNetworkDespawn을 부르고 GO를 파괴한다.)
                        //   따라서 클라이언트는 여기서 아무것도 하지 않고 즉시 반환한다.
                        //
                        //   레이어 규칙 준수:
                        //     Presentation 레이어에서는 Unity.Netcode를 직접 참조하지 않고
                        //     Application 레이어의 NetworkContext 정적 홀더만 사용한다.
                        // ────────────────────────────────────────────────────
                        if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
                        {
                            // 멀티플레이 클라이언트 경로 — 여기서는 아무것도 하지 않고 반환.
                            //   (이펙트 재생/파괴는 NetworkUnit.OnNetworkDespawn이 담당)
                            return;
                        }

                        // 서버 또는 싱글플레이: 사망 이펙트를 여기서 즉시 재생.
                        // EffectManager/AudioManager가 아직 없는 경우를 대비해 ?. 연산자로 안전 처리.
                        EffectManager.Instance?.PlayUnitDeath(_unitData.Type, transform.position);   // VFX
                        AudioManager.Instance?.PlayUnitDeathSfx(_unitData.Type);                     // SFX (규칙 15 — VFX와 짝)

                        // GameObject 파괴 경로 분기:
                        //   - 싱글플레이: NGO가 없으므로 여기서 직접 Destroy한다.
                        //   - 멀티플레이 서버: 파괴하지 않는다.
                        //       NetworkCombatController가 NetworkObject.Despawn(destroy:true)로
                        //       서버/클라이언트 GameObject를 함께 파괴하기 때문이다.
                        //       (여기서 Destroy를 호출하면 Despawn 대상이 사라져 NGO 경고가 발생한다.)
                        if (!NetworkContext.IsNetworkActive)
                        {
                            Destroy(gameObject);
                        }
                    }
                })
                .AddTo(this);
        }

        // ====================================================================
        // 방향 처리 — Y축 회전 기반
        // ====================================================================

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

            // Walk 정지 — speed=0으로 프레임 고정
            if (_animator != null) _animator.speed = 0f;
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

            // 새 코루틴을 시작하므로, OnPathInvalidated가 예약해 둔
            // _pendingPath는 더 이상 유효하지 않다(이미 새 path가 들어왔기 때문).
            // 이전 예약을 끌고 가면 다음 타일 도착 직후 즉시 path가 또 교체되어 의도와 다른 동작이 된다.
            _pendingPath = null;
            // 현재 Lerp 중인 타일 정보도 초기화 — 새 코루틴이 자기 첫 타일에서 다시 set한다.
            _currentNextTileCoord = null;

            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
            }

            // ────────────────────────────────────────────────────────────
            // [Phase 3 이동 보정] 경로 출발점을 유닛의 실제 transform 위치와 정합.
            //
            //   왜 필요한가:
            //     RequestMove는 경로를 "도메인 타일"(_unitData.Position) 기준으로 계산한다.
            //     그런데 유닛이 두 타일 사이를 이동하는 도중(transform이 도메인 타일보다 목적지 쪽으로
            //     앞서 있음)에 새 경로가 발급되면, 경로 첫 타일(path[1])이 실제 위치보다 "뒤"에 놓여
            //     첫 걸음이 목적지 역방향으로 향한다("뒤로 밀림", 규칙 11 위반).
            //
            //   동작:
            //     AlignPathStartToTransform이 이 "역방향 첫 스텝" 케이스만 감지해 앞쪽 타일 기준으로
            //     경로를 재발급한다. 정방향/정합 상태는 원본 path를 그대로 반환하므로 일반 이동은
            //     전혀 건드리지 않는다.
            // ────────────────────────────────────────────────────────────
            path = AlignPathStartToTransform(path);

            // 외부에서 이 유닛이 어디로 가는지 알 수 있도록 최종 목적지를 저장.
            // 빈 경로/null 안전 처리: path가 비정상이면 코루틴 동작이 알아서 종료된다.
            if (path != null && path.Count > 0)
            {
                _currentDestination = path[path.Count - 1];
                _hasDestination = true;
            }

            // 이동/전투 흐름 코루틴 시작.
            //   - GameSystemRules.md 규칙 1~18을 그대로 따른다.
            //   - 외부 while 안에 타일 순회를 직접 인라인하여 가독성을 높였다.
            _moveCoroutine = StartCoroutine(MoveAlongPathV3(path));
        }

        /// <summary>
        /// [Phase 3 이동 보정] 새 경로의 출발점을 유닛의 실제 transform 위치와 정합시킨다.
        ///
        /// 배경(왜 필요한가 — 초급자용 설명):
        ///   경로 계산(RequestMove)은 항상 "도메인 타일"(_unitData.Position)을 출발점으로 삼는다.
        ///   그런데 유닛의 도메인 위치는 "타일 중심에 도착했을 때"만 갱신되므로, 유닛이 두 타일
        ///   사이를 이동하는 도중(=transform이 도메인 타일보다 목적지 쪽으로 앞서 있음)에 새 경로가
        ///   발급되면, 경로의 첫 타일(path[1])이 실제 위치보다 "뒤"에 놓일 수 있다. 이 경우 유닛이
        ///   첫 걸음을 목적지 반대 방향(뒤)으로 내딛어 "뒤로 밀리는" 것처럼 보인다.
        ///   (규칙 11: A* 재개 시 뒤쪽 타일로 복귀 금지 — 이 위반을 강제로 바로잡는다.)
        ///
        /// 동작:
        ///   1) 첫 스텝(transform → path[1])이 최종 목적지 기준 "역방향"(XZ 내적&lt;0)인지 판정한다.
        ///      역방향이 아니면(정상) 원본 path를 그대로 반환 — 일반 이동은 전혀 건드리지 않는다.
        ///   2) 역방향이면: 실제 transform 위치 기준 "앞쪽 가장 가까운 타일"을 FindForwardClosestTile로
        ///      구하고, 도메인 위치도 그 타일로 정합(ProcessStep)시킨 뒤, 그 출발점에서 목적지까지
        ///      경로를 재발급(RequestMove)한다.
        ///      (전투 종료 정렬의 ResumeFromForwardTileV3에서 이미 검증된 패턴을 그대로 재사용한다.)
        ///
        /// 반환: 보정된 경로(역방향 케이스) 또는 원본 경로(정상/보정 불가 케이스).
        /// </summary>
        /// <param name="path">MoveTo가 받은 원본 경로(시작점 포함).</param>
        /// <returns>보정 경로 또는 원본 경로.</returns>
        private List<HexCoord> AlignPathStartToTransform(List<HexCoord> path)
        {
            // 방어: 경로 비정상 / 의존성 없음 / 유닛 사망 → 원본 그대로.
            if (path == null || path.Count < 2) return path;
            if (_movementUseCase == null || _unitData == null || !_unitData.IsAlive) return path;

            // 최종 목적지 — "앞쪽/뒤쪽" 판정의 기준.
            HexCoord finalTarget = path[path.Count - 1];

            // path[1] 중심과 최종 목적지 중심을 뷰 좌표로 변환.
            Vector3 step1View = TileCenterView(path[1]);
            Vector3 finalView = TileCenterView(finalTarget);

            // 첫 스텝 방향(transform → path[1])과 목적지 방향(transform → finalTarget)의 XZ 내적.
            //   내적 ≥ 0 : 정방향(또는 옆) — 정상 이동이므로 보정하지 않는다.
            //   내적 < 0 : 첫 스텝이 목적지 반대쪽 — "뒤로 밀림" 버그이므로 보정 대상.
            Vector3 toStep1 = step1View - transform.position; toStep1.y = 0f;
            Vector3 toFinal = finalView - transform.position; toFinal.y = 0f;
            float dot = toStep1.x * toFinal.x + toStep1.z * toFinal.z;
            if (dot >= 0f) return path;   // 정방향 — 원본 그대로.

            // ── 여기부터 역방향(보정) 케이스 ──

            // 실제 transform 위치 → 도메인 좌표 → 앞쪽 가장 가까운 타일.
            //   (ResumeFromForwardTileV3의 forwardTile 계산과 동일한 방식.)
            Vector3 domainPos = ViewConverter.FromView(transform.position);
            HexCoord forwardTile = _movementUseCase.FindForwardClosestTile(domainPos, finalTarget);

            // 도메인 위치 정합 — forwardTile이 현재 도메인과 다를 때만 ProcessStep.
            //   (같은 타일이면 생략 — ResumeFromForwardTileV3와 동일 규약.)
            //   ProcessStep은 위치 역인덱스(NotifyUnitMoved) 갱신과 OnUnitMoved 이벤트 발행만 하며,
            //   혼잡도 누적 이벤트(OnUnitEnteredTile)는 발행하지 않는다. 따라서 혼잡도/점유 부작용이 없다.
            if (forwardTile != _unitData.Position)
                _movementUseCase.ProcessStep(_unitData, _unitData.Position, forwardTile);

            // 앞쪽 타일에서 목적지까지 경로 재발급.
            List<HexCoord> corrected = _movementUseCase.RequestMove(_unitData, finalTarget);

            // 재발급 실패(도착/경로 없음) 시 원본 유지 — 최소한 기존 동작을 보존한다.
            if (corrected == null || corrected.Count < 2) return path;
            return corrected;
        }

        /// <summary>
        /// 타일 좌표를 유닛이 서는 뷰 좌표(타일 중심 + 유닛 높이 보정)로 변환하는 소형 헬퍼.
        /// (도메인 → 월드(HexToWorld) → 뷰(ToView) + UnitYOffset. 반복되던 3줄 변환을 모은 것.)
        /// </summary>
        private static Vector3 TileCenterView(HexCoord coord)
        {
            Vector3 world = HexMetrics.HexToWorld(coord);
            Vector3 view = ViewConverter.ToView(world);
            view.y += HexMetrics.UnitYOffset;
            return view;
        }

        /// <summary>
        /// 외부(예: GameBootstrapper / FlowField 갱신 트리거)에서
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
            // 전투 중인 유닛에 대한 repath 차단.
            //
            // 왜 필요한가:
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
                return;
            }

            HexCoord dest = _currentDestination;
            List<HexCoord> newPath = _movementUseCase.RequestMove(_unitData, dest);
            if (newPath == null || newPath.Count < 2)
            {
                // newPath가 null/짧으면 현재 위치에서 그대로 대기. 다음 트리거에서 다시 시도.
                return;
            }

            // ────────────────────────────────────────────────────────────
            // 부드러운 경로 교체 — 건물 생성/파괴 시 멈춤 현상 제거.
            //
            // 기본 동작:
            //   _pendingPath에 새 경로를 "예약"만 한다.
            //   MoveAlongPathV3 코루틴은 그대로 돌면서, 매 타일 도착 직후
            //   _pendingPath를 검사해 비어있지 않으면 path 변수를 교체하고
            //   외부 while로 재진입한다. 코루틴 자체는 중단되지 않으므로
            //   StopCoroutine + StartCoroutine에 의한 1~2 프레임 공백이 사라진다.
            //
            // 예외(즉시 MoveTo 필요):
            //   지금 Lerp 중인 다음 도착 타일(_currentNextTileCoord)에
            //   바로 건물이 새로 생긴 경우. 이때는 다음 타일 도착까지 기다리면
            //   유닛이 건물을 뚫고 그 타일 중심까지 이동해 버린다.
            //   따라서 이 케이스에만 기존처럼 코루틴을 즉시 교체한다.
            // ────────────────────────────────────────────────────────────
            if (_currentNextTileCoord.HasValue
                && !_movementUseCase.IsWalkable(_currentNextTileCoord.Value))
            {
                // 다음 도착 타일이 막혔으므로 부드러운 교체 불가 — 즉시 코루틴 재시작.
                MoveTo(newPath);
                return;
            }

            // 그 외 일반 케이스: 코루틴을 그대로 두고 새 path만 예약.
            // 이후 매 타일 도착 시점에 MoveAlongPathV3가 이 값을 소비한다.
            _pendingPath = newPath;
        }

        /// <summary>
        /// 현재 유닛이 전투 중(공격 또는 전투 이동 중)인지 판정.
        /// 공격 타겟 추적 상태로 판정합니다.
        ///
        /// 판정 기준:
        ///   - `_combatTargetTransform != null`: 전투/추격 코루틴이 진행 중일 때
        ///     `StartCombatAnimation`이 set하고, `StopCombatAnimation`이 null로 리셋합니다.
        ///   - `_isInCombatPursuit == true`: 추격 단계(아직 공격 사거리 미도달)
        ///     인 동안 EnterCombatPursuitV3가 set합니다.
        ///     이 플래그가 없으면 추격 중에 건물 변화 → RepathAllAliveUnits이 트리거되어
        ///     OnPathInvalidated가 새 path로 코루틴을 교체하고, 결과적으로 추격 코루틴이
        ///     중단되어 유닛이 한 프레임 시각적으로 멈추는 버그가 발생합니다.
        ///
        /// 호출 측(OnPathInvalidated)은 이 결과로 "이동 코루틴을 새로 시작하면 안 되는 상태"를
        /// 빠르게 식별하여, 전투 중 RepathAllAliveUnits에 의한 강제 종료를 방지합니다.
        /// </summary>
        private bool IsInCombat()
        {
            // 공격 애니메이션 중(_combatTargetTransform != null) 또는
            // 추격 코루틴 실행 중(_isInCombatPursuit) — 둘 중 하나만 참이어도 전투 상태.
            return _combatTargetTransform != null || _isInCombatPursuit;
        }

        // ====================================================================
        // MoveAlongPathV3 — 단일 상태 머신
        // ====================================================================
        // GameSystemRules.md 새 규칙 7~10에 따른 통합 상태 머신:
        //
        //   [A* 이동] : path 따라 타일 중심 → 타일 중심 Lerp 이동.
        //              매 프레임 HasEnemyInDetectRange 체크.
        //              감지되면 → [전투 이동]으로 전환.
        //
        //   [전투 이동] : 적의 월드 위치를 향해 직선 이동 (근접/원거리 동일).
        //                매 프레임 HasEnemyInRange 체크.
        //                사거리 진입 → [공격]으로 전환.
        //                적이 detect 사거리 이탈 → [A* 이동 재개].
        //                타겟 사망 → detect 내 다른 적 재선택 또는 [A* 이동 재개].
        //
        //   [공격] : 멈춘 채로 공격/쿨다운 루프.
        //            적이 사거리 이탈 → [전투 이동] 또는 [A* 이동 재개].
        //
        // 특징:
        //   - 근접/원거리(AttackKind) 차이는 공격 사거리 수치뿐.
        //   - 타일 점유 없이 단순 타일 중심 이동 — 다른 유닛과 같은 타일에 들어가도 문제 없음.
        //   - 원거리도 detect 진입 후 전투 이동으로 접근한다.
        //
        // 헬퍼 구조:
        //   MoveAlongPathV3            — 메인 코루틴 (서버 가드 + path 순회 + cleanup)
        //   ├─ EnterCombatPursuitV3    — [전투 이동] 적 월드 위치로 직선 이동 + 사거리 도달 시 공격
        //   │     └─ EnterCombatLoopV3 — [공격] 사거리 안에 있는 동안 공격/쿨다운 루프
        //   ├─ ResumeFromForwardTileV3 — [A* 이동 재개] 현재 위치 기준 앞쪽 타일에서 새 경로 시작
        //   └─ MoveCleanupAndCompleteV3 — 코루틴 정리 + OnMoveComplete 콜백
        // ====================================================================

        /// <summary>
        /// [2026-05-11 재설계] 단일 상태 머신 메인 코루틴.
        ///
        /// 동작 흐름:
        ///   1) 멀티플레이 가드 — 서버가 아니면 즉시 yield break.
        ///   2) path 유효성 체크 — 비정상이면 cleanup 후 종료.
        ///   3) Walk 애니메이션 시작 (1회만 CrossFade).
        ///   4) 외부 while: path를 순회하며 타일 중심으로 Lerp.
        ///      - 매 프레임 detect 사거리 내 적 확인.
        ///      - 감지되면 EnterCombatPursuitV3로 전투 이동 → 공격 흐름 위임.
        ///      - 전투 종료 후 ResumeFromForwardTileV3로 앞쪽 타일에서 새 경로 받아 재개.
        ///   5) 정상 완주 또는 사망이면 cleanup.
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
            // _animState(=Walk) 레벨 동기화로 모든 클라이언트에 Walk 시작 전파.
            if (NetworkContext.IsNetworkActive)
                GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);

            // Walk 애니메이션 시작 — 이동 시작 시 1회만 CrossFade.
            // 매 타일마다 CrossFade하면 블렌딩이 매번 리셋되어 Walk 모션이 끊긴다.
            if (_animator != null)
            {
                _animator.speed = 1f;
                _animator.CrossFadeInFixedTime(StateWalk, _idleToWalkBlend, 0);
            }

            // 혼잡도 기여 활성화 — 본 코루틴이 도는 동안 새 타일 진입 시
            // GameEvents.OnUnitEnteredTile을 발행한다. 전투 추격 진입 시 false로 바뀌고,
            // resumePath로 A* 재개 시 다시 true로 돌아온다.
            _isAStarMoving = true;

            // 최종 목적지(외부 while 동안 변하지 않음). 우회/재경로 모두 이 좌표를 기준.
            HexCoord finalTarget = path[path.Count - 1];

            // 유닛별 개별 이동 속도 (MoveSpeed 칸/초 → 1칸 이동 시간(초) 변환).
            float moveSeconds = _unitData.MoveSpeed > 0f ? 1f / _unitData.MoveSpeed : 1.0f;

            // ────────────────────────────────────────────────────────────────
            // 외부 while — path가 새로 발급될 때마다 위로 돌아와 새 path로 순회.
            //   재경로 사유:
            //     (A) 전투 종료 후 ResumeFromForwardTileV3가 새 path를 반환.
            //   정상 완주 또는 사망이면 외부 while 자체가 break.
            //
            //   타일에 다른 유닛이 있어도 그대로 진입하며, 같은 타일에서 시각적으로 겹치는 것은
            //   허용된다(분산은 별도 시스템 책임이 아님).
            // ────────────────────────────────────────────────────────────────
            while (_unitData != null && _unitData.IsAlive)
            {
                // 새 path를 받아 다시 시작할지 결정하는 플래그.
                // for 안에서 true로 바꾼 뒤 break하면 외부 while로 빠져 새 path로 continue.
                bool needRepath = false;

                // 마지막에 실제로 도착한 타일 — ProcessStep의 from 인자에 사용.
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

                    // 마지막 스텝이 non-walkable(성/건물)인지 — ProcessStep을 건너뛰는 기준.
                    bool isLastStep = (i == path.Count - 1);
                    bool isLastStepToNonWalkable = isLastStep
                        && (_movementUseCase != null && !_movementUseCase.IsWalkable(to));

                    // ──────────────────────────────────────────────────────
                    // [A* 이동] 도메인 방향(_unitData.Facing) 갱신 — HexDirection 단위의 도메인 상태 추적용.
                    //   시각적 회전은 ApplyDirection으로 즉시 스냅하지 않고,
                    //   아래 Lerp 루프에서 매 프레임 RotateTowards로 부드럽게 회전한다.
                    //   (즉시 스냅하면 타일 전환 순간 Y축 회전이 점프하여 시각적으로 부자연스럽다.)
                    // ──────────────────────────────────────────────────────
                    HexDirection dir = FacingDirection.FromCoords(from, to);
                    dir = ViewConverter.FlipDirection(dir);
                    _unitData.Facing = dir;

                    // ──────────────────────────────────────────────────────
                    // [A* 이동] 이동 목표 = 항상 타일 중심.
                    //   같은 타일에 여러 유닛이 들어가도 동일한 중심으로 이동한다.
                    //   시각적 분산은 별도 시스템 책임이 아니다.
                    // ──────────────────────────────────────────────────────
                    Vector3 toDomain = HexMetrics.HexToWorld(to);

                    // Lerp 출발점은 현재 위치(직전 도착 위치를 그대로 이어받음).
                    Vector3 fromPos = transform.position;

                    // 타일 중심 = 도메인 좌표 → 뷰 좌표 변환 + 유닛 높이 보정.
                    Vector3 toPos = ViewConverter.ToView(toDomain);
                    toPos.y += HexMetrics.UnitYOffset;

                    // ──────────────────────────────────────────────────────
                    // [회전 시스템 변경 — 2026-05-14] 목표 각도 계산 — toPos 정의 직후 1회만.
                    //   CalculateAttackAngle은 (toPos - 현재 위치) 벡터의 XZ 평면 Atan2 결과를 도 단위로 반환한다.
                    //   이 각도는 Lerp 루프 시작 시점의 출발 위치 기준이며, 루프 도중에는 갱신하지 않는다.
                    //   (1 타일 거리이므로 출발 시점 방향과 도착 시점 방향이 거의 동일.)
                    // ──────────────────────────────────────────────────────
                    float targetAngle = CalculateAttackAngle(toPos);
                    Quaternion targetRot = Quaternion.Euler(0f, targetAngle, 0f);

                    // ──────────────────────────────────────────────────────
                    // [A* 이동] Lerp 이동 + 매 프레임 detect 사거리 적 감지.
                    //   - detect 진입 → EnterCombatPursuitV3로 [전투 이동] 위임.
                    //   - 근접/원거리 구분 없음. 차이는 EnterCombatPursuitV3 내부의 사거리 수치뿐.
                    // ──────────────────────────────────────────────────────
                    float elapsed = 0f;
                    float targetDuration = moveSeconds;

                    // [전투 이동]으로 전환되었음을 표시. true면 Lerp while 탈출 후
                    // toPos 강제 스냅과 ProcessStep을 모두 건너뛰고 새 path로 외부 while 재진입.
                    bool interruptedByCombat = false;

                    // 지금 Lerp 중인 "다음 도착 타일" 좌표를 기록.
                    // OnPathInvalidated가 호출됐을 때 이 타일에 건물이 새로 생긴 경우만
                    // 즉시 코루틴을 재시작해야 하므로 외부에서 검사할 수 있도록 노출한다.
                    _currentNextTileCoord = to;

                    while (elapsed < targetDuration && _unitData != null && _unitData.IsAlive)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / targetDuration);
                        transform.position = Vector3.Lerp(fromPos, toPos, t);

                        // [회전 시스템 변경 — 2026-05-14] 매 프레임 RotateTowards로 점진 회전.
                        //   _rotationSpeed(초당 각도) × Time.deltaTime 만큼 targetRot로 다가간다.
                        //   1 타일 이동 시간(moveSeconds)이 회전 완료에 충분할 만큼 _rotationSpeed가 크면 거의 즉시 정렬됨.
                        //   서버에서만 실행되며, NetworkTransform이 클라이언트로 회전 보간 전달.
                        transform.rotation = Quaternion.RotateTowards(
                            transform.rotation, targetRot, _rotationSpeed * Time.deltaTime);

                        // ── [A* 이동] → [전투 이동] 전환 ──
                        // 감지 사거리 안에 적이 있으면 즉시 전투 이동으로 위임.
                        // 근접/원거리 분기 없음. 두 종류 모두 동일하게 적을 향해 직선 이동 시작.
                        if (_combatUseCase != null && _unitData.IsAlive
                            && _combatUseCase.HasEnemyInDetectRange(_unitData))
                        {
                            // 혼잡도 기여 일시 중단 — 추격 단계는 타일을 거치지 않는
                            // 직선 이동이므로 OnUnitEnteredTile 발행을 막는다. resumePath에서 다시 true.
                            _isAStarMoving = false;

                            // 전투 추격 진입 — 더 이상 "다음 도착 타일"을 향해 가지 않으므로
                            // OnPathInvalidated가 잘못된 타일을 검사하지 않도록 즉시 null로 비운다.
                            // (IsInCombat()가 true가 되어 OnPathInvalidated가 사실상 early return하지만,
                            //  방어적으로 정합 상태 유지.)
                            _currentNextTileCoord = null;

                            // [전투 이동] → [공격] 흐름을 EnterCombatPursuitV3에 전부 위임.
                            yield return EnterCombatPursuitV3();

                            // 전투 종료 후 회전 정합성 유지.
                            // _combatTargetTransform이 null이 아닌 동안 Update()가 매 프레임 회전을 덮어쓰므로
                            // StopCombatAnimation()을 직접 호출하여 즉시 추적을 끊습니다.
                            StopCombatAnimation();

                            if (_unitData == null || !_unitData.IsAlive) break;

                            // ──────────────────────────────────────────────────────────
                            // [BUG-002 수정 — 2026-05-13] 전투 종료 → 앞쪽 타일까지 "걸어서" 정렬.
                            //
                            //   왜 필요한가:
                            //     전투 추격 중 transform.position은 공격 슬롯(=적 근처)으로 이동했고,
                            //     _unitData.Position(도메인)은 여전히 추격 전 타일을 가리킨다.
                            //     이 상태에서 즉시 새 path Lerp를 시작하면, 출발점이 공격 슬롯이 되어
                            //     다음 타일까지 비정상적으로 멀리 점프하는 시각적 순간이동이 발생한다.
                            //
                            //   해결:
                            //     1) FindForwardClosestTile로 "앞쪽 타일" forwardTile을 1회만 결정.
                            //     2) 그 타일 중심 뷰 좌표(alignView)까지 동일 이동 속도로 Lerp.
                            //        (규칙 5: A* 이동과 동일 속도 — moveSeconds는 1칸 이동 시간이므로
                            //         실제 거리 / TileHeight 비율을 곱해 시간 환산.)
                            //     3) Lerp 도중에도 detect 사거리 적 감지 시 즉시 중단 → 새 추격 진입.
                            //     4) Lerp 완료 후 transform.position을 alignView로 최종 정렬 스냅.
                            //     5) 같은 forwardTile을 ResumeFromForwardTileV3에 전달해 도메인 갱신 +
                            //        새 path 발급. (이중 FindForwardClosestTile 호출 방지)
                            // ──────────────────────────────────────────────────────────

                            // 1) forwardTile 계산 — 호출은 단 1회.
                            Vector3 unitDomainPos = ViewConverter.FromView(transform.position);
                            HexCoord forwardTile = _movementUseCase.FindForwardClosestTile(unitDomainPos, finalTarget);

                            // 2) forwardTile 중심 뷰 좌표.
                            Vector3 forwardWorld = HexMetrics.HexToWorld(forwardTile);
                            Vector3 alignView = ViewConverter.ToView(forwardWorld);
                            alignView.y += HexMetrics.UnitYOffset;

                            Vector3 alignFromPos = transform.position;
                            float alignDist = Vector3.Distance(alignFromPos, alignView);

                            // 동일 이동 속도 환산: moveSeconds는 1 타일 거리 이동에 필요한 시간이므로,
                            // 실제 정렬 거리에 비례해 시간을 줄이거나 늘린다.
                            // 거리 0(또는 매우 작음)이면 Lerp 자체를 생략하고 즉시 스냅.
                            float alignDuration = (HexMetrics.TileHeight > 0f)
                                ? (alignDist / HexMetrics.TileHeight) * moveSeconds
                                : 0f;

                            // 정렬 Lerp 중에 새 적이 감지되면 다시 추격에 진입해야 하므로 별도 플래그.
                            bool alignInterruptedByCombat = false;

                            // 정렬 방향 계산 — unitDomainPos가 속한 타일에서 forwardTile로 가는 HexDirection.
                            HexCoord nearestTile = HexMetrics.WorldToHex(unitDomainPos);
                            HexDirection alignDir = FacingDirection.FromCoords(nearestTile, forwardTile);

                            // ──────────────────────────────────────────────────────
                            // [회전 시스템 변경 — 2026-05-14] 정렬 회전을 RotateTowards로 점진 회전.
                            //   기존: ApplyDirection으로 Lerp 시작 직전에 Y축 회전을 즉시 스냅.
                            //     문제 - 전투 종료 후 적 방향(스냅된 회전)에서 정렬 방향으로 점프하여 시각적으로 부자연스러움.
                            //   변경: 목표 각도(alignTargetRot)를 정해두고 Lerp 루프에서 매 프레임 누적 회전.
                            //     CalculateAttackAngle(alignView): 현재 위치에서 정렬 목적지 뷰 좌표까지의 XZ 방향 각도.
                            //   _unitData.Facing은 도메인 상태 추적 목적으로 기존처럼 HexDirection으로 갱신 유지.
                            // ──────────────────────────────────────────────────────
                            HexDirection alignViewDir = ViewConverter.FlipDirection(alignDir);
                            _unitData.Facing = alignViewDir;

                            float alignTargetAngle = CalculateAttackAngle(alignView);
                            Quaternion alignTargetRot = Quaternion.Euler(0f, alignTargetAngle, 0f);

                            if (alignDuration > 0.0001f)
                            {
                                float alignElapsed = 0f;
                                while (alignElapsed < alignDuration && _unitData != null && _unitData.IsAlive)
                                {
                                    alignElapsed += Time.deltaTime;
                                    float at = Mathf.Clamp01(alignElapsed / alignDuration);
                                    transform.position = Vector3.Lerp(alignFromPos, alignView, at);

                                    // [회전 시스템 변경 — 2026-05-14] 매 프레임 RotateTowards로 점진 회전.
                                    //   _rotationSpeed(초당 각도) × Time.deltaTime 만큼 alignTargetRot로 다가간다.
                                    //   전투 종료 후 적 방향에서 정렬 방향까지 부드러운 회전이 보이도록 한다.
                                    //   서버에서만 실행되며, NetworkTransform이 클라이언트로 회전 보간 전달.
                                    transform.rotation = Quaternion.RotateTowards(
                                        transform.rotation, alignTargetRot, _rotationSpeed * Time.deltaTime);

                                    // 정렬 도중에도 새 적 감지 시 즉시 다음 추격 사이클로 넘긴다.
                                    if (_combatUseCase != null && _unitData.IsAlive
                                        && _combatUseCase.HasEnemyInDetectRange(_unitData))
                                    {
                                        alignInterruptedByCombat = true;
                                        break;
                                    }

                                    yield return null;
                                }
                            }

                            if (_unitData == null || !_unitData.IsAlive) break;

                            // 정렬 Lerp 도중 새 적이 감지된 경우: 정렬을 마무리하지 않고 곧장 외부 while로
                            // 빠져 새 path를 받지 않은 채 다시 진입 → 다음 사이클에서 추격이 재개된다.
                            // 같은 path를 그대로 재사용하면 다시 path[i]를 향해 Lerp가 시작되고, 그 즉시
                            // detect 체크가 추격으로 전환한다.
                            if (alignInterruptedByCombat)
                            {
                                interruptedByCombat = true;
                                break;  // Lerp while 탈출 → for 루프도 interruptedByCombat 처리로 break
                            }

                            // 4) 최종 정렬 스냅 — 부동소수점 누적 오차 보정.
                            transform.position = alignView;

                            // 5) 같은 forwardTile로 도메인 갱신 + 새 path 발급.
                            List<HexCoord> resumePath = ResumeFromForwardTileV3(finalTarget, forwardTile);
                            if (resumePath != null && resumePath.Count >= 2)
                            {
                                path = resumePath;
                                needRepath = true;
                                interruptedByCombat = true;
                                // 혼잡도 기여 재활성화 — 전투 종료 후 정상 A* 재개.
                                _isAStarMoving = true;
                                break;  // Lerp while 탈출
                            }

                            // 새 path를 만들지 못함 → 이동 종료.
                            goto cleanup;
                        }

                        yield return null;
                    }

                    if (_unitData == null || !_unitData.IsAlive) break;

                    // [전투 이동]으로 중단된 경우: toPos 강제 스냅과 ProcessStep을 건너뛰고
                    // for 루프를 빠져나가 외부 while에서 새 path로 재진입.
                    if (interruptedByCombat)
                    {
                        break;
                    }

                    // ──────────────────────────────────────────────────────
                    // [A* 이동] 정상 Lerp 완료 — 타일 중심에 스냅 + 도메인 위치 갱신.
                    // ──────────────────────────────────────────────────────
                    // 정확한 최종 위치 보정.
                    transform.position = toPos;

                    // 도메인 처리: _unitData.Position을 to로 갱신.
                    // 마지막 스텝이 non-walkable(성/건물)이면 ProcessStep 생략 — 유닛이 건물 위에 존재하는 것으로 처리되지 않도록.
                    if (_movementUseCase != null && !(isLastStep && isLastStepToNonWalkable))
                    {
                        _movementUseCase.ProcessStep(_unitData, from, to);
                    }

                    // 혼잡도 시스템 — A* 이동 중 새 타일에 진입했을 때만 발행.
                    // 전투 추격 단계에서는 _isAStarMoving == false라 발행되지 않는다.
                    // GameBootstrapper가 이 이벤트를 받아 CongestionMap.Increment(tile)을 수행한다.
                    if (_isAStarMoving)
                    {
                        GameEvents.OnUnitEnteredTile.OnNext(new UnitEnteredTileEvent(_unitData.Id, to));
                    }

                    prevActualTile = to;

                    // 타일 도착 완료 — 다음 Lerp 시작 전까지는 "다음 도착 타일" 정보 없음.
                    // null로 비워 두지 않으면 OnPathInvalidated가 이미 지나간 타일을 검사할 수 있다.
                    _currentNextTileCoord = null;

                    // ──────────────────────────────────────────────────────
                    // 부드러운 경로 교체 — OnPathInvalidated가 예약한 새 path 소비.
                    //
                    //   OnPathInvalidated가 _pendingPath에 새 경로를 담아 두면,
                    //   여기서(타일 중심 도착 직후) 그것을 꺼내 path 변수를 교체한다.
                    //   현재 위치(_unitData.Position == to)가 새 path 어디에 해당하는지 찾아
                    //   거기서부터 이어가도록 외부 while로 재진입한다.
                    //
                    //   현재 위치를 새 path에서 찾지 못하는 경우(드물지만 가능):
                    //     - 새 경로 자체가 다른 시작점부터 만들어진 경우 등.
                    //     - 이때는 안전하게 MoveTo(_pendingPath)로 코루틴을 재시작한다.
                    //       (이 경로에서는 시각적 끊김이 있지만, 데이터 무결성이 우선.)
                    // ──────────────────────────────────────────────────────
                    if (_pendingPath != null)
                    {
                        List<HexCoord> pending = _pendingPath;
                        _pendingPath = null;

                        // 현재 도메인 위치가 새 path의 어느 인덱스에 있는지 검색.
                        // 찾으면 그 지점부터 이어 돌도록 path를 잘라낸다.
                        int startIdx = -1;
                        for (int k = 0; k < pending.Count; k++)
                        {
                            if (pending[k].Q == _unitData.Position.Q
                                && pending[k].R == _unitData.Position.R)
                            {
                                startIdx = k;
                                break;
                            }
                        }

                        if (startIdx >= 0 && startIdx < pending.Count - 1)
                        {
                            // 정상 케이스: 현재 위치를 새 path의 startIdx로 두고,
                            // 그 다음 타일(startIdx+1)부터 진행하도록 path 교체.
                            // path[0]은 항상 시작점(현재 위치) — 슬라이스 후에도 이 규약 유지.
                            List<HexCoord> sliced = new List<HexCoord>(pending.Count - startIdx);
                            for (int k = startIdx; k < pending.Count; k++)
                            {
                                sliced.Add(pending[k]);
                            }
                            path = sliced;

                            // 최종 목적지가 바뀌었을 수도 있으므로 갱신.
                            finalTarget = path[path.Count - 1];

                            // 외부 while 재진입 → 새 path 기준으로 i=1부터 다시 순회.
                            needRepath = true;
                            break;  // for 탈출 → 아래 `if (needRepath) continue;`로 외부 while 재진입.
                        }
                        else
                        {
                            // 안전망: 현재 위치를 새 path에서 찾지 못함 → 코루틴 재시작.
                            // MoveTo 안에서 _pendingPath / _currentNextTileCoord가 초기화되고
                            // 기존 코루틴이 정지된 뒤 새 코루틴이 시작된다.
                            MoveTo(pending);
                            yield break;
                        }
                    }

                    // [2026-05-11 비활성화] 우회(didDetour)/재경로 분기는 제거됐습니다.
                    // 새 규칙에서는 같은 타일에 여러 유닛이 들어가도 우회하지 않으므로 재계산 불필요.
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
        /// [2026-05-11 신규] 통합 전투 추격 코루틴 (근접/원거리 공통).
        ///
        /// 새 규칙(GameSystemRules.md 규칙 7)에 따른 [전투 이동] → [공격] 단일 상태 머신.
        /// 근접/원거리 구분 없음 — 차이는 UnitData.AttackRange 수치뿐.
        ///
        /// 동작:
        ///   1) FindNearestEnemyInDetectRange로 첫 타겟 선택.
        ///   2) 매 프레임:
        ///      - 타겟이 detect 사거리 밖이면 종료(→ A* 재개).
        ///      - 타겟이 사망/파괴되면 detect 내 다른 적 재선택. 없으면 종료.
        ///      - 타겟이 공격 사거리(HasEnemyInRange) 안이면 EnterCombatLoopV3 호출.
        ///         · 전투 종료 후 detect 내 다른 적이 있으면 타겟 교체 후 continue.
        ///         · 없으면 종료.
        ///      - 그 외(detect 안, 사거리 밖) → 적 방향으로 직선 이동.
        ///
        /// 슬롯/점유 없음 — 같은 적을 향해 여러 유닛이 같은 위치로 모여도 그대로 진행합니다.
        /// </summary>
        private IEnumerator EnterCombatPursuitV3()
        {
            // ────────────────────────────────────────────────────────────
            // [BUG-001 — 2026-05-12] 추격 단계 진입 마킹.
            //   IsInCombat()가 true를 반환하도록 하여, 건물 변화로 인한
            //   RepathAllAliveUnits → OnPathInvalidated에서 추격 코루틴이 끊기지 않게 한다.
            //   Unity 코루틴(IEnumerator)은 try/finally가 동작하지 않으므로,
            //   각 yield break 직전 + 함수 끝에서 false로 명시적 리셋해야 한다.
            // ────────────────────────────────────────────────────────────
            _isInCombatPursuit = true;

            // 필수 의존성 가드 — _positionProvider는 적 월드 좌표 조회에 사용.
            if (_combatUseCase == null || _positionProvider == null || _unitData == null)
            {
                _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                yield break;
            }

            // 첫 타겟 선택 — 감지 사거리 내 가장 가까운 적.
            var firstTarget = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
            if (!firstTarget.HasValue)
            {
                _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                yield break;
            }

            int targetId = firstTarget.Value.id;
            bool targetIsUnit = firstTarget.Value.isUnit;

            // 이동 속도 (A* 이동과 동일 스탯 사용).
            // worldSpeed = TileHeight / moveSeconds → 1초당 약 한 칸 거리 이동.
            float moveSeconds = _unitData.MoveSpeed > 0f ? 1f / _unitData.MoveSpeed : 1.0f;
            float worldSpeed = HexMetrics.TileHeight / moveSeconds;

            // 매 프레임 루프 — 적 상태 확인 + 전투 이동 또는 공격으로 분기.
            while (_unitData != null && _unitData.IsAlive)
            {
                // ── 1) 적의 최신 월드 좌표 조회 ──
                // 적이 이동 중일 수 있으므로 매 프레임 갱신.
                Vector3 enemyViewPos = targetIsUnit
                    ? _positionProvider.GetUnitWorldPosition(targetId)
                    : _positionProvider.GetBuildingWorldPosition(targetId);

                // ── 2) 타겟 파괴 → 다음 타겟 재선택 or 종료 ──
                if (enemyViewPos == Vector3.zero)
                {
                    var next = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
                    if (!next.HasValue)
                    {
                        // detect 내 다른 적 없음 → A* 재개로 위임.
                        _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                        yield break;
                    }

                    targetId = next.Value.id;
                    targetIsUnit = next.Value.isUnit;

                    yield return null;
                    continue;
                }

                // ── 3) 적이 detect 사거리 밖으로 이탈 → 종료 ──
                if (!_combatUseCase.HasEnemyInDetectRange(_unitData))
                {
                    _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                    yield break;
                }

                // ── 4) 공격 사거리 도달 → [공격] 루프 ──
                if (_combatUseCase.HasEnemyInRange(_unitData))
                {
                    yield return EnterCombatLoopV3();

                    if (_unitData == null || !_unitData.IsAlive)
                    {
                        _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                        yield break;
                    }

                    // 전투 종료 후 detect 내에 다른 적이 남았으면 새 타겟 선택 후 continue.
                    if (!_combatUseCase.HasEnemyInDetectRange(_unitData))
                    {
                        _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                        yield break;
                    }

                    var nextEnemy = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
                    if (!nextEnemy.HasValue)
                    {
                        _isInCombatPursuit = false; // [BUG-001] 추격 종료 마킹
                        yield break;
                    }

                    targetId = nextEnemy.Value.id;
                    targetIsUnit = nextEnemy.Value.isUnit;

                    yield return null;
                    continue;
                }

                // ── 5) [전투 이동] 적의 월드 위치로 직선 이동 + 매 프레임 적 방향 회전 ──
                // 슬롯 없음 — 같은 적을 노리는 유닛이 같은 위치로 모여도 진행.
                // 회전: 시각적으로 자연스럽게 적을 바라보며 추적.
                Vector3 moveDir = enemyViewPos - transform.position;
                moveDir.y = 0f;
                float dist = moveDir.magnitude;
                if (dist > 0.01f)
                {
                    float pursuitAngle = CalculateAttackAngle(enemyViewPos);
                    Quaternion targetRot = Quaternion.Euler(0f, pursuitAngle, 0f);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation, targetRot, _rotationSpeed * Time.deltaTime);

                    // 적 방향으로 직선 이동 (한 프레임 이동량 = worldSpeed × dt).
                    transform.position += moveDir.normalized * worldSpeed * Time.deltaTime;
                }

                yield return null;
            }

            // ────────────────────────────────────────────────────────────
            // [BUG-001 — 2026-05-12] while 루프가 자연 종료(_unitData null/사망)된 경우의
            // 안전망 리셋. 위쪽의 모든 명시적 yield break 경로에서도 false로 set하지만,
            // 함수 마지막에서도 반드시 false임을 보장하여 다음 추격 진입 시 잔존하지 않도록 한다.
            // ────────────────────────────────────────────────────────────
            _isInCombatPursuit = false;
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
        ///   1) (호출 측이 미리 계산한) forwardTile을 파라미터로 받음 — 이중 계산 방지.
        ///   2) ProcessStep으로 도메인 위치를 그 타일로 갱신 + FROM(이전 _unitData.Position) 해제.
        ///   3) 같은 타일이면 ProcessStep 생략 — 점유 누수 방지.
        ///   4) Walk 애니메이션 재개.
        ///   5) finalTarget으로 RequestMove → 새 path 반환. 호출 측이 외부 while로 재진입.
        ///
        /// 뒤쪽 타일로 복귀하지 않는다(규칙 11/15).
        ///
        /// [BUG-002 — 2026-05-13]
        ///   이전에는 transform.position을 forwardTile에 즉시 스냅했으나, 이는 규칙 9(걸어서 재개)
        ///   위반이며 1타일 순간이동의 직접 원인이었다. 이제 transform.position 정렬은 호출 측
        ///   (MoveAlongPathV3)에서 Lerp로 처리하므로, 이 함수는 도메인 갱신과 path 재발급만 담당한다.
        ///
        /// [중요] forwardTile 이중 계산 방지:
        ///   호출 측은 Lerp 시작 전에 FindForwardClosestTile을 1회 호출하여 forwardTile을 결정한 뒤,
        ///   그 값으로 transform을 정렬하고 동일한 forwardTile을 이 함수에 전달해야 한다.
        ///   만약 이 함수가 FindForwardClosestTile을 다시 호출하면, 정렬 후의 transform.position
        ///   기준으로 한 타일 더 앞쪽이 선택될 수 있어 도메인-뷰 좌표가 다시 어긋난다.
        /// </summary>
        private List<HexCoord> ResumeFromForwardTileV3(HexCoord finalTarget, HexCoord forwardTile)
        {
            if (_movementUseCase == null || _unitData == null || !_unitData.IsAlive)
                return null;

            // 같은 타일이면 도메인 갱신 불필요. 다른 타일이면 ProcessStep만 호출.
            // [2026-05-11 비활성화] RegisterOccupancyMove는 점유 시스템 폐기로 인해 호출 제거됨.
            if (forwardTile != _unitData.Position)
            {
                _movementUseCase.ProcessStep(_unitData, _unitData.Position, forwardTile);
            }

            // ────────────────────────────────────────────────────────────
            // [BUG-002 수정 — 2026-05-13] 즉시 스냅 제거.
            //
            //   이전 구현:
            //     Vector3 forwardWorld = HexMetrics.HexToWorld(forwardTile);
            //     Vector3 forwardView = ViewConverter.ToView(forwardWorld);
            //     forwardView.y += HexMetrics.UnitYOffset;
            //     transform.position = forwardView;   ← 1타일 순간이동의 원인.
            //
            //   현재:
            //     호출 측(MoveAlongPathV3)이 이 함수 호출 전에 Lerp로 forwardTile 중심까지
            //     "걸어서" 이동시킨 뒤 정렬 스냅까지 마쳤다고 가정한다.
            //     따라서 여기서는 도메인 갱신과 path 재발급만 처리한다.
            // ────────────────────────────────────────────────────────────

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
        /// 이동 종료 시 정리 + 1회성 콜백 호출.
        /// 외부 while 종료(완주/사망/RESUME 실패) 직후 + path 비정상 종료 직후 등에서 호출.
        /// </summary>
        private void MoveCleanupAndCompleteV3()
        {
            _moveCoroutine = null;

            // 혼잡도 기여 종료 — 이동이 끝났으니 새 코루틴 전까지는 발행하지 않는다.
            _isAStarMoving = false;

            // 이동이 완전히 종료되었으므로 부드러운 교체 관련 상태도 모두 초기화.
            // 이렇게 해두지 않으면 다음에 MoveTo가 호출되기 전에 OnPathInvalidated가 끼어들었을 때
            // 이미 종료된 코루틴의 잔재 정보를 보고 잘못된 분기를 탈 수 있다.
            _pendingPath = null;
            _currentNextTileCoord = null;

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

            // 공격 이펙트 재생 (VFX + SFX 동시).
            // OnAttackHit은 모든 클라이언트에서 로컬로 실행되는 Animation Event이므로
            // 멀티플레이에서도 양쪽 화면에 공격 이펙트가 정상 재생된다.
            // EffectManager가 아직 없는 경우(초기화 전/테스트 등)를 대비해 ?. 연산자로 안전 처리.

            // VfxSpawnPoint가 연결된 유닛(피스톨러 등)은 총구 위치를 스폰 기준점으로 사용한다.
            // ※ 회전은 VfxSpawnPoint.rotation을 쓰지 않는다.
            //   VfxSpawnPoint가 스켈레톤 본(손 부위) 하위에 배치되어 있어,
            //   월드 회전에 본 고유 회전(약 0, -90, -90도)이 섞여 VFX가 엉뚱한 방향으로 발사되기 때문이다.
            //   위치는 본 덕분에 정확하므로 유지하고, 회전만 유닛 루트의 정면 방향으로 교체한다.
            Vector3 spawnPos = _vfxSpawnPoint != null ? _vfxSpawnPoint.position : transform.position;
            Quaternion spawnRot = Quaternion.LookRotation(transform.forward);
            EffectManager.Instance?.PlayUnitAttack(_unitData.Type, spawnPos, spawnRot);  // VFX
            AudioManager.Instance?.PlayUnitAttackSfx(_unitData.Type);                    // SFX (규칙 15 — VFX와 짝)

            // ────────────────────────────────────────────────────────────────
            // 피격 연출 방출 타이밍 결정 (전투 타격 타이밍 동기화 Phase 3 — 축 4 / 3-2)
            //
            //   근접 유닛 : 이 자리(타격 프레임)에서 OnLocalAttackHit을 즉시 발행 → 즉시 피격 연출. (기존 동작)
            //   원거리 유닛: 트레이서(발사체)를 발사하고, 그 발사체가 "착탄"하는 순간에 OnLocalAttackHit을
            //               발행한다. 즉 트레이서 비행 시간만큼 피격 연출이 지연되어, 화살/탄환이
            //               실제로 맞는 순간과 피격 연출(HP 텍스트·피격 VFX·타격 반응)이 일치한다.
            //
            //   ※ 데미지 타이밍(서버 권위)은 여기서 전혀 건드리지 않는다. 트레이서는 순수 시각 표현이다.
            // ────────────────────────────────────────────────────────────────
            int attackerId = _unitData.Id;
            EffectManager em = EffectManager.Instance;

            if (_unitData.AttackRange >= RangedAttackThreshold && em != null)
            {
                // 도착 지점 = 발사 시점의 타겟 월드 위치(값으로 복사).
                //   비행 중 타겟이 파괴되어도 트레이서는 이 좌표까지 그대로 날아가 소멸한다(댕글링 참조 없음).
                //   _combatTargetTransform이 있으면 그 위치를, 없으면 백업 ID로 재조회(둘 다 실패 시 전방 폴백).
                Vector3 targetPos = _combatTargetTransform != null
                    ? _combatTargetTransform.position
                    : GetTargetWorldPos(_combatTargetId, _combatTargetIsUnit);

                // 트레이서 발사. 착탄 콜백에서 OnLocalAttackHit을 발행하여 피격 연출을 착탄 시점에 방출한다.
                //   트레이서 프리셋이 없으면 PlayTracer가 콜백을 "즉시" 실행하므로 기존 즉시 방출로 폴백된다.
                em.PlayTracer(_unitData.Type, spawnPos, targetPos,
                    () => GameEvents.OnLocalAttackHit.OnNext(attackerId));
            }
            else
            {
                // 근접 유닛(또는 EffectManager 부재 시): 발사 순간 = 타격 순간이므로 즉시 신호를 발행한다.
                // HitPresentationQueue가 이 신호를 받아 해당 공격자의 보류 큐에서 피격 연출 1건을 방출한다.
                // 모든 클라이언트에서 로컬로 실행되므로 각 화면이 자기 애니메이션 타이밍에 맞춰 연출된다.
                // (전투 타격 타이밍 동기화 Phase 2 — 축 3)
                GameEvents.OnLocalAttackHit.OnNext(attackerId);
            }
        }

            // ====================================================================
        // Walk 애니메이션 — 클라이언트 전용 (NetworkCombatController의 ClientRpc에서 호출)
        // ====================================================================

        /// <summary>
        /// Walk 애니메이션 시작(CrossFade). 클라이언트 전용 — 서버는 MoveAlongPath에서 직접 제어.
        ///
        /// [Phase 2] 호출 경로:
        ///   NetworkUnit._animState(=Walk) 값 변경/스폰 시 → NetworkUnit.ApplyAnimState → 여기.
        ///   (과거의 엣지 트리거 Walk RPC를 레벨 동기화 값으로 대체 — 스폰 레이스 유실 방지.)
        /// 이미 Walk 상태면 리셋하지 않고 speed만 복원.
        /// </summary>
        public void StartWalkAnimation()
        {
            // _animator가 null이면 lazy-init 시도 (Initialize() 전에 상태 값이 적용되는 경우 대응 — 스폰 레이스)
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
            if (_animator == null) return;

            _animator.speed = 1f;
            _animator.CrossFadeInFixedTime(StateWalk, _idleToWalkBlend, 0);
        }

        /// <summary>
        /// [Phase 2 신규] 공격(Attack) 애니메이션 루프를 재생한다 — CrossFade만 수행.
        ///
        /// 클라이언트에서 NetworkUnit의 애니메이션 상태 NetworkVariable(=Attack)이 적용될 때 호출된다
        /// (값 변경 및 스폰 시 현재 값 적용). Attack 클립은 Loop Time=ON이므로 1회 CrossFade로 무한 루프.
        ///
        /// 회전/타겟 추적은 StartCombatAnimation(StartCombatClientRpc 경로)이 담당하므로, 이 메서드는
        /// 타겟 정보를 받지 않고 순수하게 "Attack 상태 전환"만 담당한다(애니메이션과 조준의 책임 분리).
        /// _unitData/Initialize보다 먼저 호출될 수 있어 animator를 지연 초기화한다(스폰 레이스 대응).
        /// </summary>
        public void PlayAttackAnimation()
        {
            // _animator가 null이면 lazy-init 시도 (Initialize() 전에 상태 값이 적용되는 경우 대응 — 스폰 레이스)
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
            if (_animator == null) return;

            _animator.speed = 1f;
            _animator.CrossFadeInFixedTime(StateAttack, _toAttackBlend, 0);
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

            // ────────────────────────────────────────────────────────────────
            // [Phase 2 대체] Attack CrossFade 책임 분리.
            //   멀티플레이 "클라이언트"의 Attack 전환은 이제 애니메이션 상태 레벨 동기화가 담당한다
            //   (NetworkUnit._animState = Attack → PlayAttackAnimation). 스폰 레이스로 유실될 수 없다.
            //   여기서는 "싱글플레이" 또는 "호스트(서버)"만 직접 CrossFade한다:
            //     - 싱글플레이: NGO가 없어 레벨 동기화 경로가 없으므로 직접 재생해야 한다.
            //     - 호스트(서버): 서버가 Animator를 직접 제어하는 기존 관례를 유지한다
            //       (서버가 애니메이션을 직접 제어하는 다른 경로들과 동일한 취지).
            //   클라이언트가 여기서도 CrossFade하면 레벨 동기화와 이중 적용되므로 스킵한다.
            // ────────────────────────────────────────────────────────────────
            bool applyCrossFadeHere = !NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer;
            if (applyCrossFadeHere && _animator != null)
            {
                _animator.speed = 1f;
                _animator.CrossFadeInFixedTime(StateAttack, _toAttackBlend, 0);
            }

            // 공격 중 타겟 Transform 참조 저장 → Update()에서 매 프레임 추적.
            // [Phase 2] 이 타겟 참조/회전 추적은 애니메이션과 분리해 "유지"한다(클라이언트도 필요):
            //   원거리 유닛의 트레이서 조준(OnAttackHit)이 _combatTargetTransform/_combatTargetId를 사용한다.
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
        ///   Walk 전환은 서버가 이동을 재개할 때 _animState(=Walk) 레벨 동기화로 명시적으로 신호를 보냄.
        ///   StopCombatClientRpc 도착 시점과 서버의 실제 이동 재개 시점이 다를 수 있으므로,
        ///   여기서 Walk CrossFade를 즉시 호출하면 서버가 아직 쿨다운(최대 3초) 대기 중인 동안
        ///   클라이언트만 Walk 애니메이션이 재생되고 유닛이 실제로 이동하지 않는 불일치가 발생.
        ///   → Walk 전환 타이밍은 _animState 레벨 동기화에 완전히 위임.
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
