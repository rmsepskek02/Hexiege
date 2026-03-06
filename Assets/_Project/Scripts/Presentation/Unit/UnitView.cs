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
// 프리팹 구조 (3D 전환 후):
//   Unit_{Type} (GameObject)
//     ├─ MeshFilter + MeshRenderer (or SkinnedMeshRenderer)
//     ├─ Animator (AnimatorController 설정)
//     └─ UnitView (이 스크립트)
//
// Animator 파라미터:
//   - IsWalking (bool): 이동 중 여부
//   - IsDead (bool): 사망 여부
//   - Attack (trigger): 공격 트리거
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
using Hexiege.Infrastructure;
using Hexiege.Application;

namespace Hexiege.Presentation
{
    public class UnitView : MonoBehaviour
    {
        // ====================================================================
        // Animator 파라미터 해시 (문자열 비교 방지)
        // ====================================================================

        private static readonly int AnimIsWalking = Animator.StringToHash("IsWalking");
        private static readonly int AnimIsDead = Animator.StringToHash("IsDead");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");

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
        /// </summary>
        [SerializeField] private float _meshYOffset = 30f;

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

        /// <summary> 현재 이동 코루틴. null이면 정지 상태. </summary>
        private Coroutine _moveCoroutine;

        /// <summary> 현재 공격 코루틴. </summary>
        private Coroutine _attackCoroutine;

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
        // 초기화
        // ====================================================================

        /// <summary>
        /// UnitFactory에서 프리팹 Instantiate 직후 호출.
        /// Domain 데이터를 전달받아 초기 상태 설정.
        /// </summary>
        /// <param name="unitData">이 유닛의 Domain 데이터</param>
        public void Initialize(UnitData unitData)
        {
            _unitData = unitData;

            // [Phase 2] Animator 캐시 — 자식 오브젝트에 있을 수 있으므로 GetComponentInChildren 사용
            _animator = GetComponentInChildren<Animator>();

            // 초기 방향으로 Y축 회전 설정
            ApplyDirection(_unitData.Facing);
        }

        /// <summary>
        /// 외부 의존성 주입. GameBootstrapper에서 모든 컴포넌트 생성 후 호출.
        /// [Phase 2] UnitAnimationData 파라미터 제거 — Animator(Mecanim)가 대체.
        /// </summary>
        public void SetDependencies(GameConfig config,
            UnitMovementUseCase movementUseCase, UnitCombatUseCase combatUseCase,
            UnitFactory unitFactory = null, BuildingFactory buildingFactory = null)
        {
            _config = config;
            _movementUseCase = movementUseCase;
            _combatUseCase = combatUseCase;
            _unitFactory = unitFactory;
            _buildingFactory = buildingFactory;

            // 공격 이벤트 구독 — 싱글플레이 전용.
            // 멀티플레이에서는 NetworkCombatController가 ClientRpc로 직접 TriggerAttackAnimation() 호출.
            if (!NetworkContext.IsNetworkActive)
            {
                GameEvents.OnEntityAttacked
                    .Subscribe(e =>
                    {
                        if (_unitData != null && e.Attacker == (IDamageable)_unitData)
                        {
                            TriggerAttackAnimation(e.Target.Id, e.Target is UnitData);
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
                        // [Phase 2] 사망 애니메이션 트리거 (Animator가 있으면)
                        SetAnimatorBool(AnimIsDead, true);
                        Destroy(gameObject);
                    }
                })
                .AddTo(this);
        }

        // ====================================================================
        // Update — 싱글플레이 쿨다운 감소
        // ====================================================================

        private void Update()
        {
            // 싱글플레이에서만 매 프레임 쿨다운 감소
            // 멀티플레이에서는 NetworkCombatController가 서버 Tick에서 감소 처리
            if (!NetworkContext.IsNetworkActive && _unitData != null && _unitData.AttackCooldownRemaining > 0f)
            {
                _unitData.AttackCooldownRemaining = Mathf.Max(0f, _unitData.AttackCooldownRemaining - Time.deltaTime);
            }
        }

        // ====================================================================
        // 방향 처리 — Y축 회전 기반
        // ====================================================================

        /// <summary>
        /// HexDirection에 따라 유닛의 Y축 회전을 설정.
        /// [Phase 2] flipX 대신 transform.rotation Y축으로 방향 표현.
        /// </summary>
        /// <param name="dir">뷰 기준 HexDirection (ViewConverter 반전 적용 후).</param>
        private void ApplyDirection(HexDirection dir)
        {
            int index = (int)dir;
            if (index >= 0 && index < DirectionAngles.Length)
            {
                transform.rotation = Quaternion.Euler(0f, DirectionAngles[index], 0f);
            }
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

        // ====================================================================
        // Animator 안전 래퍼
        // ====================================================================

        /// <summary> Animator null 체크 후 bool 파라미터 설정. </summary>
        private void SetAnimatorBool(int paramHash, bool value)
        {
            if (_animator != null)
                _animator.SetBool(paramHash, value);
        }

        /// <summary> Animator null 체크 후 trigger 파라미터 설정. </summary>
        private void SetAnimatorTrigger(int paramHash)
        {
            if (_animator != null)
                _animator.SetTrigger(paramHash);
        }

        // ====================================================================
        // 이동
        // ====================================================================

        /// <summary>
        /// 현재 이동을 즉시 중단. 현재 위치에서 정지.
        /// </summary>
        public void StopMovement()
        {
            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
            }
            _unitData.ClaimedTile = null;

            // [Phase 2] Idle 상태 전환
            SetAnimatorBool(AnimIsWalking, false);
        }

        /// <summary>
        /// 경로를 따라 유닛을 시각적으로 이동시킴.
        /// </summary>
        /// <param name="path">A* 경로 (시작점 포함)</param>
        public void MoveTo(List<HexCoord> path)
        {
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
        /// </summary>
        private IEnumerator MoveAlongPath(List<HexCoord> path)
        {
            // 유닛별 개별 이동속도 사용 (UnitData.MoveSeconds)
            float moveSeconds = _unitData.MoveSeconds > 0f ? _unitData.MoveSeconds : 0.3f;
            HexCoord finalTarget = path[path.Count - 1];

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
                _unitData.ClaimedTile = to;

                // 이동 방향 계산 → 뷰 관점에 맞게 반전 → Y축 회전 적용
                HexDirection dir = FacingDirection.FromCoords(from, to);
                dir = ViewConverter.FlipDirection(dir);

                _unitData.Facing = dir;

                // [Phase 2] Y축 회전으로 방향 표현 (flipX 대신)
                ApplyDirection(dir);

                // Walk 애니메이션 시작
                SetAnimatorBool(AnimIsWalking, true);

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
                        if (NetworkContext.IsNetworkActive)
                        {
                            // 멀티플레이: HasEnemyInRange로 Lerp 일시정지
                            if (_combatUseCase.HasEnemyInRange(_unitData))
                            {
                                while (_unitData.IsAlive && _combatUseCase.HasEnemyInRange(_unitData))
                                {
                                    while (_attackCoroutine != null)
                                        yield return null;
                                    yield return null;
                                }

                                if (!_unitData.IsAlive) break;

                                // 적 제거 후 Walk 복귀
                                SetAnimatorBool(AnimIsWalking, true);
                            }
                        }
                        else
                        {
                            // 싱글플레이: HasEnemyInRange로 이동 차단, TryAttack은 쿨다운 기반으로 자동 실행
                            if (_combatUseCase.HasEnemyInRange(_unitData))
                            {
                                // 이동 중단 → Idle 전환
                                SetAnimatorBool(AnimIsWalking, false);

                                while (_unitData.IsAlive && _combatUseCase.HasEnemyInRange(_unitData))
                                {
                                    // 쿨다운이 끝나면 공격 시도
                                    _combatUseCase.TryAttack(_unitData);

                                    while (_attackCoroutine != null)
                                        yield return null;
                                    yield return null;
                                }

                                if (!_unitData.IsAlive) break;

                                // 적 제거 후 Walk 복귀
                                SetAnimatorBool(AnimIsWalking, true);
                            }
                        }
                    }

                    yield return null;
                }

                if (!_unitData.IsAlive) break;

                // 정확한 최종 위치 보정
                transform.position = toPos;

                // 논리적 이동 처리
                if (_movementUseCase != null)
                {
                    _movementUseCase.ProcessStep(_unitData, from, to);
                }

                _unitData.ClaimedTile = null;
            }

            // 이동 완료 정리
            _unitData.ClaimedTile = null;

            // [Phase 2] Idle 상태 전환
            SetAnimatorBool(AnimIsWalking, false);
            _moveCoroutine = null;

            // 이동 완료 콜백 실행 (1회성)
            var callback = OnMoveComplete;
            OnMoveComplete = null;
            callback?.Invoke();
        }

        // ====================================================================
        // 공격 애니메이션
        // ====================================================================

        /// <summary>
        /// 외부에서 공격 애니메이션을 트리거. NetworkCombatController의 ClientRpc에서 호출.
        /// 타겟의 실제 transform.position 기반 Atan2 정밀 각도로 회전.
        /// </summary>
        /// <param name="targetId">타겟 엔티티의 Id.</param>
        /// <param name="targetIsUnit">true=유닛, false=건물.</param>
        public void TriggerAttackAnimation(int targetId, bool targetIsUnit)
        {
            if (_unitData == null || !_unitData.IsAlive) return;

            if (_attackCoroutine != null)
                StopCoroutine(_attackCoroutine);

            float angle = CalculateAttackAngle(GetTargetWorldPos(targetId, targetIsUnit));
            _attackCoroutine = StartCoroutine(PlayAttackAnimation(angle));
        }

        /// <summary>
        /// 공격 애니메이션을 재생하고 일정 시간 후 복귀하는 코루틴.
        /// Atan2 기반 정밀 Y축 회전 각도를 사용.
        /// [Phase 2] Animator.SetTrigger("Attack") 사용.
        /// </summary>
        private IEnumerator PlayAttackAnimation(float yAngle)
        {
            // Atan2 기반 정밀 공격 각도로 Y축 회전
            transform.rotation = Quaternion.Euler(0f, yAngle, 0f);

            // [Phase 2] Animator 트리거로 공격 애니메이션 재생
            SetAnimatorTrigger(AnimAttack);

            // 공격 애니메이션 재생 시간 (기본값 0.33초, config에서 조정 가능)
            float attackDuration = _config != null ? 2f / _config.AnimationFps : 0.33f;
            yield return new WaitForSeconds(attackDuration);

            _attackCoroutine = null;
        }
    }
}
