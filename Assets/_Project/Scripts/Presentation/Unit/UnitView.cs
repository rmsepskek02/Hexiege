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

            // 공격 이벤트 구독 — 싱글플레이 전용.
            // 멀티플레이에서는 NetworkCombatController가 ClientRpc로 직접 TriggerAttackAnimation() 호출.
            // 싱글플레이: UnitCombatUseCase.ExecuteAttack() → OnEntityAttacked 발행 → 이 구독이 처리.
            // 멀티플레이: OnEntityAttacked는 서버에서만 발행되므로 이 구독은 서버 UnitView만 수신.
            //            대신 NetworkCombatController의 ClientRpc가 모든 클라이언트(서버 포함)에서 처리.
            if (!NetworkContext.IsNetworkActive)
            {
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
            }

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
        /// 현재 이동을 즉시 중단. 현재 위치에서 정지.
        /// 외부(네트워크 전투 등)에서 이동 중단이 필요할 때 호출.
        /// </summary>
        public void StopMovement()
        {
            if (_moveCoroutine != null)
            {
                StopCoroutine(_moveCoroutine);
                _moveCoroutine = null;
            }
            _unitData.ClaimedTile = null;
            UpdateSprite(UnitAnimState.Idle);
        }

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
            // 유닛별 개별 이동속도 사용 (UnitData.MoveSeconds). 0 이하면 안전 기본값 적용.
            float moveSeconds = _unitData.MoveSeconds > 0f ? _unitData.MoveSeconds : 0.3f;
            HexCoord finalTarget = path[path.Count - 1]; // 최종 목적지 저장

            // 경로의 각 구간을 순회 (0=시작은 건너뜀)
            for (int i = 1; i < path.Count; i++)
            {
                HexCoord from = path[i - 1];
                HexCoord to = path[i];

                // --------------------------------------------------------
                // Per-step 체크: 다음 타일이 같은 팀에 의해 차단되었는지 확인.
                // 경로 계산 이후 다른 유닛이 해당 타일로 이동/선점했을 수 있으므로
                // 각 스텝마다 실시간 검증 → 차단 시 재탐색.
                // --------------------------------------------------------
                if (_movementUseCase != null && _movementUseCase.IsTileBlockedBySameTeam(_unitData, to))
                {
                    // 현재 위치에서 최종 목적지까지 재탐색
                    List<HexCoord> newPath = _movementUseCase.RequestMove(_unitData, finalTarget);
                    if (newPath != null)
                    {
                        path = newPath;
                        i = 0; // for 루프 시작 시 i++로 1이 됨 → 새 경로의 첫 스텝부터
                        continue;
                    }
                    else
                    {
                        break; // 경로 없음 → 이동 중단
                    }
                }

                // --------------------------------------------------------
                // ClaimedTile 선점 (같은 팀 유닛 겹침 방지)
                // 적 팀에게는 영향 없음 — 전투로 해결.
                // --------------------------------------------------------
                _unitData.ClaimedTile = to;

                // 이동 방향 계산 → 뷰 관점에 맞게 반전 → 스프라이트 방향 전환
                HexDirection dir = FacingDirection.FromCoords(from, to);
                dir = ViewConverter.FlipDirection(dir);
                FacingInfo facing = FacingDirection.FromHexDirection(dir);

                // UnitData.Facing 업데이트 (Attack 등 다른 애니메이션에서 방향 참조용)
                _unitData.Facing = dir;

                // SpriteRenderer.flipX 설정 (왼쪽 방향이면 true)
                _spriteRenderer.flipX = facing.FlipX;

                // Walk 애니메이션으로 전환
                UpdateSprite(UnitAnimState.Walk, facing.Art);

                // 출발/도착의 도메인 좌표 계산 → 뷰 좌표로 변환 (Red팀이면 반전)
                // ToView 이후에 UnitYOffset 적용 — 이전에 적용하면 Red팀에서 오프셋 방향 반전
                Vector3 fromPos = ViewConverter.ToView(HexMetrics.HexToWorld(from));
                fromPos.y += HexMetrics.UnitYOffset;
                Vector3 toPos = ViewConverter.ToView(HexMetrics.HexToWorld(to));
                toPos.y += HexMetrics.UnitYOffset;

                // --------------------------------------------------------
                // Lerp 이동 + 이동 중 거리 기반 전투 체크
                // 싱글플레이/서버: TryAttack()으로 직접 전투 실행.
                // 클라이언트: HasEnemyInRange()로 적 감지 시 Lerp 일시정지.
                //   서버가 전투를 처리하고 EntityDiedClientRpc로 결과 전파.
                //   적이 제거되면 HasEnemyInRange가 false → Lerp 재개.
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
                            // ========================================================
                            // 멀티플레이 모드: 전투 판정은 NetworkCombatController에 위임.
                            // 여기서는 HasEnemyInRange()로 적 존재 여부만 확인하여
                            // Lerp 이동을 일시정지. 공격 애니메이션은 ClientRpc로 트리거.
                            // ========================================================
                            if (_combatUseCase.HasEnemyInRange(_unitData))
                            {
                                // 적이 사거리에서 사라지거나 자신이 사망할 때까지 대기.
                                // Idle 전환 없이 현재 상태 유지 — Attack 애니메이션은 ClientRpc가 담당.
                                while (_unitData.IsAlive && _combatUseCase.HasEnemyInRange(_unitData))
                                {
                                    // 공격 애니메이션 재생 중이면 완료 대기
                                    while (_attackCoroutine != null)
                                        yield return null;
                                    yield return null;
                                }

                                if (!_unitData.IsAlive) break;

                                // 적이 제거되면 Walk 애니메이션 복귀 후 Lerp 재개
                                FacingInfo resumeFacing = FacingDirection.FromHexDirection(_unitData.Facing);
                                UpdateSprite(UnitAnimState.Walk, resumeFacing.Art);
                            }
                        }
                        else
                        {
                            // ========================================================
                            // 싱글플레이 모드: 기존 로직 유지.
                            // TryAttack()으로 직접 전투 실행 + 이벤트 기반 애니메이션.
                            // ========================================================
                            if (_combatUseCase.TryAttack(_unitData))
                            {
                                // 공격 애니메이션 완료 대기
                                while (_attackCoroutine != null)
                                    yield return null;

                                // 사거리 내 적이 남아있으면 반복 공격
                                while (_unitData.IsAlive && _combatUseCase.TryAttack(_unitData))
                                {
                                    while (_attackCoroutine != null)
                                        yield return null;
                                }

                                // 전투 중 사망했으면 이동 중단
                                if (!_unitData.IsAlive) break;

                                // 전투 승리 후 남은 Lerp 계속 진행
                            }
                        }
                    }

                    yield return null; // 다음 프레임까지 대기
                }

                // 전투 중 사망했으면 루프 탈출
                if (!_unitData.IsAlive) break;

                // 정확한 최종 위치 보정 (Lerp 오차 방지)
                transform.position = toPos;

                // 논리적 이동 처리 (UnitData 위치 업데이트 + 타일 점령 + 이벤트 발행)
                if (_movementUseCase != null)
                {
                    _movementUseCase.ProcessStep(_unitData, from, to);
                }

                // ClaimedTile 해제 (Position이 갱신되었으므로 더 이상 불필요)
                _unitData.ClaimedTile = null;
            }

            // 이동 완료 또는 사망 시 ClaimedTile 정리
            _unitData.ClaimedTile = null;

            // Idle 상태 복귀
            UpdateSprite(UnitAnimState.Idle);
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
        /// 서버가 공격 판정 후 모든 클라이언트에 애니메이션을 동기화할 때 사용.
        /// </summary>
        /// <param name="direction">공격 방향 (스프라이트 방향 결정에 사용)</param>
        public void TriggerAttackAnimation(HexDirection direction)
        {
            if (_unitData == null || !_unitData.IsAlive) return;

            // 진행 중인 공격 애니메이션이 있으면 중단 후 새로 시작
            if (_attackCoroutine != null)
                StopCoroutine(_attackCoroutine);

            // Red팀(ViewConverter.IsFlipped)이면 방향 반전 적용.
            // 서버에서 전달된 도메인 방향을 뷰 방향으로 변환 — MoveAlongPath의 패턴과 동일.
            HexDirection viewDir = ViewConverter.FlipDirection(direction);
            _attackCoroutine = StartCoroutine(PlayAttackAnimation(viewDir));
        }

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
