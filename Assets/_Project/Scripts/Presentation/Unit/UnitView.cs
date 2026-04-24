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
        // NE(0)=30, E(1)=90, SE(2)=150, SW(3)=210, W(4)=270, NW(5)=330
        // 3D 탑다운 뷰에서 유닛이 바라보는 방향을 Y축 회전으로 표현.
        // ====================================================================

        private static readonly float[] DirectionAngles = { 30f, 90f, 150f, 210f, 270f, 330f };

        // ====================================================================
        // Inspector 설정
        // ====================================================================

        /// <summary>
        /// 프리팹 메시 자식 오브젝트의 localEulerAngles.y 보정값.
        /// Unit_Pistoleer_Mesh가 30도 회전되어 있으므로 Atan2 결과에서 빼줌.
        /// 서버에서 공격 방향 계산(CalculateAttackAngle)에 사용.
        /// </summary>
        [SerializeField] private float _meshYOffset = 30f;

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

        /// <summary> 현재 이동 코루틴. null이면 정지 상태. </summary>
        private Coroutine _moveCoroutine;

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
            IEntityPositionProvider positionProvider = null)
        {
            _config = config;
            _movementUseCase = movementUseCase;
            _combatUseCase = combatUseCase;
            _unitFactory = unitFactory;
            _buildingFactory = buildingFactory;
            _positionProvider = positionProvider;

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
        /// _meshYOffset을 빼서 프리팹 메시의 기본 회전을 보정.
        /// </summary>
        /// <param name="targetWorldPos">타겟의 실제 월드 좌표 (transform.position).</param>
        /// <returns>Y축 회전 각도 (도 단위).</returns>
        private float CalculateAttackAngle(Vector3 targetWorldPos)
        {
            Vector3 dir = targetWorldPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f)
                return transform.eulerAngles.y;
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg - _meshYOffset;
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

            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
            }

            _moveCoroutine = StartCoroutine(MoveAlongPath(path));
        }

        /// <summary>
        /// 경로를 따라 타일→타일 Lerp 이동하는 코루틴.
        /// [Phase 2] 방향 전환: flipX → ApplyDirection(Y축 회전).
        ///           애니메이션: FrameAnimator → Animator.SetBool("IsWalking").
        /// [Phase 3] DORotate 제거 → 즉시 스냅. TurnToFace RPC 제거.
        ///           회전은 서버에서 즉시 스냅 → NetworkTransform이 클라이언트에 보간 전달.
        /// </summary>
        private IEnumerator MoveAlongPath(List<HexCoord> path)
        {
            // 멀티플레이 서버: 이동 시작 이벤트 발행
            // → NetworkCombatController가 수신하여 StartWalkAnimationClientRpc로 클라이언트에 Walk 시작 전파
            if (NetworkContext.IsNetworkActive)
                GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);

            // 유닛별 개별 이동속도 사용 (MoveSpeed 칸/초 → lerp duration 초 변환)
            float moveSeconds = _unitData.MoveSpeed > 0f ? 1f / _unitData.MoveSpeed : 1.0f;
            HexCoord finalTarget = path[path.Count - 1];

            // Walk 애니메이션 시작 — 이동 시작 시 1회만 CrossFade.
            // for 루프 안에서 매 타일마다 호출하면 같은 상태로의 CrossFade가 반복되어
            // 블렌딩이 매번 리셋되고 Walk 모션이 끊기는 현상이 발생.
            // 이동 시작 전 1회만 호출하면 Walk 루프가 끊김 없이 자연스럽게 재생됨.
            if (_animator != null)
            {
                _animator.speed = 1f;
                _animator.CrossFadeInFixedTime(StateWalk, _idleToWalkBlend, 0);
            }

            // ====================================================================
            // 하이브리드 이동 시스템
            // ====================================================================
            // 유닛의 이동은 두 가지 Phase가 번갈아 동작한다.
            //
            // [ Phase 0 ] 타일 기반 A* 이동 (기본 상태)
            //   - path 리스트를 순회하며 타일에서 타일로 Lerp 이동.
            //   - 각 타일 도착 후 적 감지 사거리(DetectRange) 체크:
            //       적 감지 && 공격 사거리 밖이면 → shouldPursue = true 로 Phase 1 진입.
            //
            // [ Phase 1 ] 월드 좌표 직선 추적
            //   - 타일 단위가 아닌 실시간 월드 좌표로 적을 직선 추적한다.
            //   - A* 재경로보다 자연스럽고 반응이 빠르다.
            //   - 종료 조건:
            //       (a) 적이 공격 사거리에 들어옴 → 전투 루프 진입 후, 감지 사거리 이탈 시 Phase 2.
            //       (b) 적이 감지 사거리 밖으로 이탈 → Phase 2.
            //       (c) 추적 대상이 사라짐(파괴 등) → Phase 2.
            //
            // [ Phase 2 ] 가장 가까운 타일로 스냅 후 A* 재개
            //   - Phase 1 종료 시 유닛은 타일 격자 위가 아닌 임의의 월드 좌표에 있다.
            //   - 가장 가까운 타일 중심으로 이동(타일 이동속도와 동일) 후 도메인 위치 동기화.
            //   - finalTarget(원래 목적지)로 A* 재계산 → 외부 while 루프 재진입으로 Phase 0 재개.
            //
            // 외부 while 루프는 Phase 0 ↔ Phase 1 ↔ Phase 2 전환을 반복시키는 역할.

            // Phase 1(추적 모드) 진입 여부 플래그. for 루프 내부 detect 체크에서 설정.
            bool shouldPursue = false;

            // 경로 재계산 후 재실행을 위한 외부 루프 (Phase 2 완료 후 새 경로로 Phase 0 재개).
            while (_unitData.IsAlive)
            {
                shouldPursue = false;

                // ====================================================================
                // [ Phase 0 ] 타일 기반 A* 이동
                // ====================================================================
                // 경로의 각 구간을 순회 (0=시작은 건너뜀)
                for (int i = 1; i < path.Count; i++)
                {
                HexCoord from = path[i - 1];
                HexCoord to = path[i];

                // Per-step 체크: 같은 팀에 의해 차단되었는지 확인 → 재탐색
                if (_movementUseCase != null && _movementUseCase.IsTileBlockedBySameTeam(_unitData, to))
                {
                    List<HexCoord> newPath = _movementUseCase.RequestMove(_unitData, finalTarget);
                    if (newPath != null)
                    {
                        path = newPath;
                        i = 0;
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }

                // ClaimedTile 선점 (같은 팀 유닛 겹침 방지)
                // 단, 마지막 스텝의 목적지가 non-walkable 타일(성/건물 타일)인 경우에는
                // ClaimedTile을 설정하지 않는다.
                // 이유: 성 타일은 유닛이 실제로 도착하여 머무를 수 없는 타일이다.
                //   ClaimedTile로 설정하면 공격 루프 동안 이 값이 유지되어,
                //   후속 유닛이 IsTileBlockedBySameTeam()에서 성 타일을 blocked로 인식하게 되고
                //   성 방향으로의 경로 탐색이 실패하여 두 번째 이후 유닛이 접근하지 못하는 버그가 발생한다.
                bool isLastStepToNonWalkable = (i == path.Count - 1)
                    && (_movementUseCase != null && !_movementUseCase.IsWalkable(to));

                if (!isLastStepToNonWalkable)
                    _unitData.ClaimedTile = to;

                // 이동 방향 계산 → 뷰 관점에 맞게 반전 → Y축 회전 즉시 스냅
                HexDirection dir = FacingDirection.FromCoords(from, to);
                dir = ViewConverter.FlipDirection(dir);

                _unitData.Facing = dir;

                // [Phase 2] Y축 회전으로 방향 표현 (flipX 대신)
                // [Phase 3] 즉시 스냅 → NetworkTransform이 클라이언트에 보간 전달
                ApplyDirection(dir);

                // 출발/도착의 도메인 좌표 계산 → 뷰 좌표로 변환
                Vector3 fromPos = ViewConverter.ToView(HexMetrics.HexToWorld(from));
                fromPos.y += HexMetrics.UnitYOffset;
                Vector3 toPos = ViewConverter.ToView(HexMetrics.HexToWorld(to));
                toPos.y += HexMetrics.UnitYOffset;

                // --------------------------------------------------------
                // Lerp 이동 + 이동 중 전투 체크
                // --------------------------------------------------------
                float elapsed = 0f;

                while (elapsed < moveSeconds)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / moveSeconds);
                    transform.position = Vector3.Lerp(fromPos, toPos, t);

                    // 이동 중 전투 체크 (매 프레임)
                    if (_combatUseCase != null && _unitData.IsAlive)
                    {
                        // 공격 사거리 내 적 존재 — 전투 루프 진입.
                        if (_combatUseCase.HasEnemyInRange(_unitData))
                        {
                            if (NetworkContext.IsNetworkActive)
                            {
                                // 적 감지 즉시 이벤트 발행 → NetworkCombatController가 즉시 StartCombatClientRpc 전송
                                // TickCombat 인터벌(최대 50ms) 동안 공격 애니메이션이 시작되지 않는 현상 방지.
                                // 루프 진입 전 1회만 발행 (루프 안에서 반복 발행 금지).
                                GameEvents.OnUnitEnteredCombat.OnNext(_unitData.Id);

                                // Walk 정지 이벤트를 발행하지 않는 이유:
                                //   StartCombatClientRpc 내부에서 StopWalkAnimation() + StartCombatAnimation()을
                                //   같은 프레임에 호출하므로, 별도의 StopWalkRpc는 불필요.
                                //   여기서 OnUnitWalkStopped를 발행하면:
                                //     StopWalkRpc(Idle CrossFade) 도착 → TickCombat 인터벌 후 StartCombatRpc 도착
                                //     → 그 사이에 Idle이 재생되어 유닛이 멈추는 현상(멈춤현상) 발생.

                                // 이동 정지: 적이 사거리 내에 있는 동안 이 루프에서 대기.
                                // yield return null만 반복하므로 elapsed가 증가하지 않아 유닛 위치가 고정됨.
                                // (이 루프 없이 외부 while(elapsed < moveSeconds)로 돌아가면 elapsed가 증가하여 유닛이 이동해버림)
                                while (_unitData.IsAlive && _combatUseCase.HasEnemyInRange(_unitData))
                                {
                                    yield return null;
                                }

                                if (!_unitData.IsAlive) break;

                                // 타겟 이탈 또는 사망 무관하게 현재 공격 사이클이 끝날 때까지 쿨다운 대기.
                                // 규칙: 타겟 상태에 관계없이 현재 공격 애니메이션 완료 후 이동 재개.
                                // AttackCooldown = 공격 클립 길이이므로, 쿨다운 만료 = 한 사이클 완료.
                                while (_unitData.IsAlive && _unitData.AttackCooldownRemaining > 0f)
                                {
                                    yield return null;
                                }

                                if (!_unitData.IsAlive) break;

                                // 사이클 완료 후 적이 다시 사거리에 들어왔으면 전투 루프 재진입
                                if (_combatUseCase.HasEnemyInRange(_unitData))
                                    continue;

                                // 이동 재개: 현재 Animator 상태 확인 후 Walk CrossFade.
                                // 이미 Walk 상태면 CrossFade 재호출 시 Walk 루프가 처음부터 리셋되므로 speed만 복원.
                                // Attack 상태이면 Attack → Walk CrossFade (0.10초 블렌드, 규칙 3).
                                if (_animator != null)
                                {
                                    var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                                    if (stateInfo.shortNameHash == StateWalk)
                                    {
                                        _animator.speed = 1f;
                                    }
                                    else
                                    {
                                        _animator.CrossFadeInFixedTime(StateWalk, _attackToWalkBlend, 0);
                                    }
                                }
                                GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);
                            }
                            else
                            {
                                // 싱글플레이: 적이 사거리 내에 있는 동안 매 프레임 TryAttack 호출.
                                // TryAttack이 내부에서 쿨다운을 체크하고, 쿨다운 만료 시 공격 + 이벤트 발행.
                                while (_unitData.IsAlive && _combatUseCase.HasEnemyInRange(_unitData))
                                {
                                    _combatUseCase.TryAttack(_unitData);
                                    yield return null;
                                }

                                if (!_unitData.IsAlive) break;

                                // 현재 공격 사이클이 끝날 때까지 쿨다운 대기 (순수 타이머 기반, 규칙 4)
                                while (_unitData.IsAlive && _unitData.AttackCooldownRemaining > 0f)
                                {
                                    yield return null;
                                }

                                if (!_unitData.IsAlive) break;

                                if (_combatUseCase.HasEnemyInRange(_unitData))
                                    continue;

                                // 전투 종료 → 이동 재개
                                _combatUseCase.ClearCombatState(_unitData.Id);

                                if (_animator != null)
                                {
                                    var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                                    if (stateInfo.shortNameHash == StateWalk)
                                    {
                                        _animator.speed = 1f;
                                    }
                                    else
                                    {
                                        _animator.CrossFadeInFixedTime(StateWalk, _attackToWalkBlend, 0);
                                    }
                                }
                            }
                        }
                    }

                    yield return null;
                }

                if (!_unitData.IsAlive) break;

                // 정확한 최종 위치 보정
                transform.position = toPos;

                // 논리적 이동 처리.
                // 경로 마지막 타일이 walkable하지 않은 경우(건물 타일) ProcessStep을 생략.
                // 이유: 건물 타일에 대해 Position/ClaimedTile 업데이트, 타일 점령을 하면
                //   건물 타일의 소유권이 변경되고 유닛이 건물 위에 위치한 것으로 처리됨.
                // Lerp 이동 자체는 위에서 수행 완료 — 건물 방향으로의 시각 효과는 유지됨.
                bool isLastStep = (i == path.Count - 1);
                bool isNonWalkableTile = (_movementUseCase != null && !_movementUseCase.IsWalkable(to));

                if (_movementUseCase != null && !(isLastStep && isNonWalkableTile))
                {
                    _movementUseCase.ProcessStep(_unitData, from, to);
                }

                _unitData.ClaimedTile = null;

                // --------------------------------------------------------
                // 스텝 완료 후 적 감지 사거리(DetectRange) 체크 → Phase 1 진입
                // --------------------------------------------------------
                // ProcessStep 실행 후에만 도메인 위치(_unitData.Position)가 현재 타일로 갱신된다.
                // Lerp 도중 체크하면 _unitData.Position이 이전 타일을 가리켜 판정이 어긋난다.
                //
                // 이전 방식: A* 재경로로 적 타일까지 새 경로 계산 → 무한루프 가드(isRerouting) 필요.
                // 신규 방식: 타일 이동을 중단하고 Phase 1(월드 좌표 직선 추적)으로 즉시 전환.
                //   - shouldPursue=true, for 루프 break → 외부 while이 Phase 1을 실행.
                //   - Phase 1 종료 후 Phase 2에서 가장 가까운 타일로 스냅 + A* 재계산 → 다음 루프에서 다시 Phase 0.
                //
                // 근접유닛만 해당 (원거리유닛은 DetectRange == AttackRange → HasEnemyInRange에서 처리).
                if (_combatUseCase != null && _unitData.IsAlive
                    && _combatUseCase.HasEnemyInDetectRange(_unitData)
                    && !_combatUseCase.HasEnemyInRange(_unitData))
                {
                    // ClaimedTile 해제 — Phase 1에서 타일 점유가 필요 없고,
                    // 방치 시 다른 유닛의 경로 탐색이 이 타일을 blocked로 인식하는 부작용을 방지한다.
                    _unitData.ClaimedTile = null;
                    shouldPursue = true;
                    break;  // for 루프 탈출 → 외부 while에서 Phase 1 진입
                }
            }  // end of Phase 0 (for)

            // 유닛 사망 시 정리로 진행
            if (!_unitData.IsAlive) break;

            // 적 감지 없이 경로를 끝까지 완주한 경우 → 이동 완료 정리로 진행
            if (!shouldPursue) break;

            // ====================================================================
            // [ Phase 1 ] 월드 좌표 직선 추적
            // ====================================================================
            // _positionProvider가 주입되지 않은 경우(구버전/테스트 환경) Phase 1을 스킵하고
            // 바로 Phase 2로 넘어간다. 이 경우 유닛은 현재 타일에서 A* 재계산으로 이어진다.
            if (_combatUseCase != null && _positionProvider != null)
            {
                // 추적 대상 선정 — 감지 사거리 내 가장 가까운 적.
                var pursuitTarget = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
                if (pursuitTarget.HasValue)
                {
                    int targetId = pursuitTarget.Value.id;
                    bool targetIsUnit = pursuitTarget.Value.isUnit;

                    // 타일 이동속도와 동일한 월드 단위 속도.
                    // 1타일을 moveSeconds초에 이동 = TileHeight 월드거리를 moveSeconds초에 이동.
                    // → 초당 (TileHeight / moveSeconds) 월드 단위.
                    float worldSpeed = HexMetrics.TileHeight / moveSeconds;

                    while (_unitData.IsAlive)
                    {
                        // 매 프레임 추적 대상의 최신 월드 좌표를 조회 → 움직이는 적도 따라간다.
                        Vector3 enemyViewPos = targetIsUnit
                            ? _positionProvider.GetUnitWorldPosition(targetId)
                            : _positionProvider.GetBuildingWorldPosition(targetId);

                        // provider가 Vector3.zero를 반환 = 대상 파괴 또는 미등록 → Phase 2로.
                        if (enemyViewPos == Vector3.zero)
                            break;

                        // --------------------------------------------------------
                        // 공격 사거리 진입 → 전투 루프 (기존 Phase 0 내부 로직과 동일 구조)
                        // --------------------------------------------------------
                        if (_combatUseCase.HasEnemyInRange(_unitData))
                        {
                            if (NetworkContext.IsNetworkActive)
                            {
                                // 멀티플레이: 서버는 이벤트만 발행 — NetworkCombatController가 RPC로 전파.
                                GameEvents.OnUnitEnteredCombat.OnNext(_unitData.Id);

                                // 적이 사거리 내에 있는 동안 대기 (이동 없음)
                                while (_unitData.IsAlive && _combatUseCase.HasEnemyInRange(_unitData))
                                    yield return null;

                                if (!_unitData.IsAlive) break;

                                // 현재 공격 사이클이 끝날 때까지 쿨다운 대기.
                                while (_unitData.IsAlive && _unitData.AttackCooldownRemaining > 0f)
                                    yield return null;

                                if (!_unitData.IsAlive) break;

                                // 사이클 완료 후 다시 사거리에 들어왔으면 전투 루프 재진입
                                if (_combatUseCase.HasEnemyInRange(_unitData))
                                    continue;

                                // 이동 재개 애니메이션 — Attack → Walk CrossFade (또는 이미 Walk면 speed만)
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
                                // 싱글플레이: 매 프레임 TryAttack (내부 쿨다운 체크).
                                while (_unitData.IsAlive && _combatUseCase.HasEnemyInRange(_unitData))
                                {
                                    _combatUseCase.TryAttack(_unitData);
                                    yield return null;
                                }

                                if (!_unitData.IsAlive) break;

                                while (_unitData.IsAlive && _unitData.AttackCooldownRemaining > 0f)
                                    yield return null;

                                if (!_unitData.IsAlive) break;

                                if (_combatUseCase.HasEnemyInRange(_unitData))
                                    continue;

                                // 싱글플레이: 전투 종료 시 상태 직접 정리.
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

                            // 전투 후: 감지 사거리 밖 = 적 이탈 → Phase 2로.
                            if (!_combatUseCase.HasEnemyInDetectRange(_unitData))
                                break;

                            // 감지 사거리 안에 다른 적이 있는 경우 계속 추적 루프로.
                            continue;
                        }

                        // --------------------------------------------------------
                        // 감지 사거리 이탈 (전투 없이) → Phase 2로
                        // --------------------------------------------------------
                        if (!_combatUseCase.HasEnemyInDetectRange(_unitData))
                            break;

                        // --------------------------------------------------------
                        // 직선 이동 (Y축 무시 — 수평 이동만)
                        // --------------------------------------------------------
                        Vector3 moveDir = enemyViewPos - transform.position;
                        moveDir.y = 0f;
                        float dist = moveDir.magnitude;
                        if (dist > 0.01f)
                            transform.position += moveDir.normalized * worldSpeed * Time.deltaTime;

                        yield return null;
                    }
                }
            }

            if (!_unitData.IsAlive) break;

            // ====================================================================
            // [ Phase 2 ] 가장 가까운 타일 중심으로 스냅 후 A* 재개
            // ====================================================================
            // Phase 1 종료 시점에 유닛은 타일 격자 위가 아닌 임의의 월드 좌표에 있다.
            // 도메인 좌표계(HexCoord)와 뷰 좌표계를 다시 동기화하려면:
            //   1) 현재 뷰 좌표 → 도메인 좌표 역변환 → 가장 가까운 HexCoord 탐색.
            //   2) 해당 타일 중심까지 부드럽게 이동 (타일 이동속도 동일).
            //   3) 도메인 위치 갱신(ProcessStep).
            //   4) finalTarget 방향으로 A* 재계산 → 외부 while 상단으로 돌아가 Phase 0 재개.
            {
                // 현재 뷰 좌표 → 도메인 월드 좌표 역변환
                Vector3 domainPos = ViewConverter.FromView(transform.position);
                HexCoord nearestTile = HexMetrics.WorldToHex(domainPos);

                // 가장 가까운 타일 중심의 뷰 좌표 (유닛 높이 오프셋 포함)
                Vector3 tileCenter = ViewConverter.ToView(HexMetrics.HexToWorld(nearestTile));
                tileCenter.y += HexMetrics.UnitYOffset;

                // 타일 중심까지 Lerp 이동 (타일 이동속도와 동일한 월드 속도)
                float worldSpeed2 = HexMetrics.TileHeight / moveSeconds;
                Vector3 snapStart = transform.position;
                float snapDist = Vector3.Distance(snapStart, tileCenter);

                if (snapDist > 0.01f)
                {
                    float snapDuration = snapDist / worldSpeed2;
                    float snapElapsed = 0f;
                    while (snapElapsed < snapDuration && _unitData.IsAlive)
                    {
                        snapElapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(snapElapsed / snapDuration);
                        transform.position = Vector3.Lerp(snapStart, tileCenter, t);
                        yield return null;
                    }
                }
                transform.position = tileCenter;

                if (!_unitData.IsAlive) break;

                // 도메인 좌표 동기화 — ProcessStep으로 _unitData.Position을 nearestTile로 갱신.
                // from/to를 동일하게 넘겨도 ProcessStep 내부에서 Position/타일 점유가 정상 처리된다.
                if (_movementUseCase != null)
                    _movementUseCase.ProcessStep(_unitData, _unitData.Position, nearestTile);
                _unitData.ClaimedTile = null;

                // Walk 애니메이션 재개 (Phase 1 전투 중 Attack 상태였을 수 있음)
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

                // 원래 목적지(finalTarget)로 A* 재계산.
                // 성공 시 새 path로 외부 while 상단(Phase 0)으로 돌아가 이동 재개.
                if (_movementUseCase != null)
                {
                    List<HexCoord> resumePath = _movementUseCase.RequestMove(_unitData, finalTarget);
                    if (resumePath != null && resumePath.Count >= 2)
                    {
                        path = resumePath;
                        continue;  // 외부 while → 새 path로 Phase 0 재실행
                    }
                }

                // A* 재계산 실패 → 더 이상 진행 불가, 이동 종료
                break;
            }
        }  // end of outer while (Phase 0 ↔ Phase 1 ↔ Phase 2 반복)

            // 이동 완료 정리
            _unitData.ClaimedTile = null;

            // 이동 완료 — 게임 내 모든 이동의 목적지는 적 성이므로
            // 성 파괴 시 게임 종료. 도착 시 Idle 전환 불필요.
            _moveCoroutine = null;

            // 이동 완료 콜백 실행 (1회성)
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
