// ============================================================================
// UnitView.cs
// 유닛의 비주얼(스프라이트 이동, 방향 전환, 애니메이션 상태)을 담당하는 컴포넌트.
//
// 이 스크립트가 부착되는 프리팹 구조 (Phase 10에서 생성):
//   Unit_Pistoleer (GameObject)
//     ├─ SpriteRenderer  (유닛 스프라이트)
//     ├─ FrameAnimator   (프레임 순환)
//     └─ UnitView        (이 스크립트)
//
// 역할:
//   1. 이동 코루틴 — 경로(List<HexCoord>)를 받아 타일→타일 Lerp 이동
//   2. 방향 전환 — 이동 방향에 따라 스프라이트 + flipX 변경
//   3. 상태 전환 — 이동 중 Walk, 정지 시 Idle 애니메이션 설정
//   4. 타일 점령 — 각 타일 도착 시 UnitMovementUseCase.ProcessStep() 호출
//
// 이동 흐름:
//   InputHandler가 이동 명령
//     → UnitMovementUseCase.RequestMove()로 경로 계산
//     → UnitView.MoveTo(path)로 시각적 이동 시작
//     → 코루틴이 타일마다 Lerp + ProcessStep (논리 이동)
//     → 이동 완료 시 Idle 상태로 복귀
//
// Lerp(선형 보간)란?
//   두 점 사이를 t(0~1)로 부드럽게 이동하는 계산.
//   position = Vector3.Lerp(from, to, t)
//   t=0이면 from, t=1이면 to, t=0.5면 중간점.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, Coroutine).
// ============================================================================

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
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(FrameAnimator))]
    public class UnitView : MonoBehaviour
    {
        // ====================================================================
        // 내부 참조
        // ====================================================================

        /// <summary> 이 유닛에 연결된 Domain 데이터. </summary>
        private UnitData _unitData;

        /// <summary> SpriteRenderer — flipX 설정에 사용. </summary>
        private SpriteRenderer _spriteRenderer;

        /// <summary> FrameAnimator — 스프라이트 프레임 순환. </summary>
        private FrameAnimator _frameAnimator;

        /// <summary> 이 유닛의 애니메이션 데이터 (방향별 스프라이트). </summary>
        private UnitAnimationData _animData;

        /// <summary> 전역 설정 (이동 속도, 애니메이션 FPS 등). </summary>
        private GameConfig _config;

        /// <summary>
        /// 이동 UseCase 참조. 타일 도착 시 ProcessStep 호출용.
        /// GameBootstrapper에서 주입.
        /// </summary>
        private UnitMovementUseCase _movementUseCase;

        /// <summary> 전투 UseCase 참조. 이동 완료 후 자동 공격용. </summary>
        private UnitCombatUseCase _combatUseCase;

        /// <summary> 현재 이동 코루틴. null이면 정지 상태. </summary>
        private Coroutine _moveCoroutine;

        /// <summary> 현재 공격 코루틴. </summary>
        private Coroutine _attackCoroutine;

        /// <summary> 현재 이동 중인지 여부. InputHandler에서 이동 명령 중복 방지에 사용. </summary>
        public bool IsMoving => _moveCoroutine != null;

        /// <summary> 이 유닛의 Domain 데이터. 외부에서 읽기 전용. </summary>
        public UnitData Data => _unitData;

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
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _frameAnimator = GetComponent<FrameAnimator>();

            // 초기 방향/상태로 스프라이트 설정
            UpdateSprite(UnitAnimState.Idle);
        }

        /// <summary>
        /// 외부 의존성 주입. GameBootstrapper에서 모든 컴포넌트 생성 후 호출.
        /// </summary>
        public void SetDependencies(UnitAnimationData animData, GameConfig config,
            UnitMovementUseCase movementUseCase, UnitCombatUseCase combatUseCase)
        {
            _animData = animData;
            _config = config;
            _movementUseCase = movementUseCase;
            _combatUseCase = combatUseCase;

            // 공격 이벤트 구독 — 이 유닛이 공격자일 때 공격 애니메이션 재생
            GameEvents.OnEntityAttacked
                .Subscribe(e =>
                {
                    // 이벤트의 공격자가 이 유닛인지 확인 (참조 비교)
                    if (_unitData != null && e.Attacker == (IDamageable)_unitData)
                    {
                        // 공격자의 Facing 방향을 사용해 애니메이션 재생
                        _attackCoroutine = StartCoroutine(PlayAttackAnimation(_unitData.Facing));
                    }
                })
                .AddTo(this);

            // 사망 이벤트 구독 — 이 유닛 또는 다른 엔티티가 사망하면 처리
            GameEvents.OnEntityDied
                .Subscribe(e =>
                {
                    // 사망한 엔티티가 이 유닛일 경우 GameObject 파괴
                    if (_unitData != null && e.Entity == (IDamageable)_unitData)
                    {
                        Destroy(gameObject);
                    }
                })
                .AddTo(this);

            // 의존성 설정 후 스프라이트 재설정 (animData가 필요하므로)
            UpdateSprite(UnitAnimState.Idle);
        }

        // ====================================================================
        // 이동
        // ====================================================================

        /// <summary>
        /// 경로를 따라 유닛을 시각적으로 이동시킴.
        /// 이미 이동 중이면 기존 이동을 중단하고 새 이동 시작.
        ///
        /// 경로 리스트: [시작, 중간1, 중간2, ..., 목표]
        /// 인덱스 0(시작)은 건너뛰고 1부터 순회.
        /// </summary>
        /// <param name="path">A* 경로 (시작점 포함)</param>
        public void MoveTo(List<HexCoord> path)
        {
            // 기존 이동 중이면 중단
            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
            }

            _moveCoroutine = StartCoroutine(MoveAlongPath(path));
        }

        /// <summary>
        /// 경로를 따라 타일→타일 Lerp 이동하는 코루틴.
        ///
        /// 각 타일 이동 단계:
        ///   1. 이동 방향 계산 → 스프라이트 방향/flipX 변경
        ///   2. Walk 애니메이션 설정
        ///   3. UnitMoveSeconds 동안 Lerp로 부드럽게 이동
        ///   4. 도착 시 ProcessStep 호출 (논리적 이동 + 타일 점령)
        ///
        /// 모든 타일 이동 완료 후 Idle 상태로 복귀.
        /// </summary>
        private IEnumerator MoveAlongPath(List<HexCoord> path)
        {
            float moveSeconds = _config != null ? _config.UnitMoveSeconds : 0.3f;

            // 경로의 각 구간을 순회 (0=시작은 건너뜀)
            for (int i = 1; i < path.Count; i++)
            {
                HexCoord from = path[i - 1];
                HexCoord to = path[i];

                // 이동 방향 계산 → 스프라이트 방향 전환
                HexDirection dir = FacingDirection.FromCoords(from, to);
                FacingInfo facing = FacingDirection.FromHexDirection(dir);

                // UnitData.Facing 업데이트 (Attack 등 다른 애니메이션에서 방향 참조용)
                _unitData.Facing = dir;

                // SpriteRenderer.flipX 설정 (왼쪽 방향이면 true)
                _spriteRenderer.flipX = facing.FlipX;

                // Walk 애니메이션으로 전환
                UpdateSprite(UnitAnimState.Walk, facing.Art);

                // 출발/도착의 월드 좌표 계산 (유닛 Y 오프셋 포함)
                // HexToWorldUnit: 타일 위에 서있는 위치로 변환
                Vector3 fromPos = HexMetrics.HexToWorldUnit(from);
                Vector3 toPos = HexMetrics.HexToWorldUnit(to);

                // Lerp로 부드럽게 이동
                float elapsed = 0f;
                while (elapsed < moveSeconds)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / moveSeconds);
                    transform.position = Vector3.Lerp(fromPos, toPos, t);
                    yield return null; // 다음 프레임까지 대기
                }

                // 정확한 최종 위치 보정 (Lerp 오차 방지)
                transform.position = toPos;

                // 논리적 이동 처리 (UnitData 위치 업데이트 + 타일 점령 + 이벤트 발행)
                if (_movementUseCase != null)
                {
                    _movementUseCase.ProcessStep(_unitData, from, to);
                }

                // 매 타일 도착 후 사거리 내 적 체크 → 발견 시 공격 후 남은 경로 계속 이동
                if (_combatUseCase != null && _unitData.IsAlive && _combatUseCase.TryAttack(_unitData))
                {
                    while (_attackCoroutine != null)
                        yield return null;

                    // 적이 남아있는 동안 반복 공격
                    while (_unitData.IsAlive && _combatUseCase.TryAttack(_unitData))
                    {
                        while (_attackCoroutine != null)
                            yield return null;
                    }

                    // 전투 중 사망했으면 이동 중단
                    if (!_unitData.IsAlive) break;
                }
            }

            // Idle 상태 복귀
            UpdateSprite(UnitAnimState.Idle);
            _moveCoroutine = null;
        }

        // ====================================================================
        // 공격 애니메이션
        // ====================================================================

        /// <summary>
        /// 공격 애니메이션을 재생하고 일정 시간 후 Idle로 복귀하는 코루틴.
        /// </summary>
        private IEnumerator PlayAttackAnimation(HexDirection direction)
        {
            FacingInfo info = FacingDirection.FromHexDirection(direction);
            _spriteRenderer.flipX = info.FlipX;
            UpdateSprite(UnitAnimState.Attack, info.Art);

            // 공격 애니메이션 재생 시간 (2프레임 × FPS 기반)
            float attackDuration = _config != null ? 2f / _config.AnimationFps : 0.33f;
            yield return new WaitForSeconds(attackDuration);

            _attackCoroutine = null;
        }

        // ====================================================================
        // 스프라이트 갱신
        // ====================================================================

        /// <summary>
        /// 현재 상태와 방향에 맞는 스프라이트 배열을 FrameAnimator에 설정.
        /// 방향을 지정하지 않으면 UnitData의 현재 Facing 사용.
        /// </summary>
        /// <param name="state">애니메이션 상태 (Idle/Walk/Attack)</param>
        /// <param name="artDir">아트 방향. null이면 현재 Facing에서 계산.</param>
        private void UpdateSprite(UnitAnimState state, ArtDirection? artDir = null)
        {
            if (_animData == null || _frameAnimator == null || _unitData == null) return;

            // 방향 미지정 시 현재 UnitData.Facing에서 계산
            if (!artDir.HasValue)
            {
                FacingInfo info = FacingDirection.FromHexDirection(_unitData.Facing);
                artDir = info.Art;
                _spriteRenderer.flipX = info.FlipX;
            }

            // 해당 상태/방향의 스프라이트 배열 가져오기
            Sprite[] frames = _animData.GetSprites(state, artDir.Value);

            // FrameAnimator에 설정 (같은 배열이면 리셋 안 됨)
            float fps = _config != null ? _config.AnimationFps : 6f;
            _frameAnimator.SetAnimation(frames, fps);
        }
    }
}
