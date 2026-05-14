# Research: 공격 애니메이션 루프 개선

**날짜:** 2026-03-07

---

## 현재 문제

`UnitView.PlayAttackAnimation()` 이 `2f / AnimationFps = 0.33s` 대기 후 종료.
실제 Attack 클립 길이(~1.0s)보다 짧아 Animator가 Exit Time으로 Walk 상태로 복귀 → Walk 플래시.

`SetAnimatorTrigger(AnimAttack)` + `SetAnimatorBool(AnimIsWalking)` 방식:
- Trigger는 한 번 발화 후 소멸 → Exit Time이 있으면 Walk로 복귀
- 연속 공격 시 Attack → Walk → Attack 전환 반복 → 어색함

## 현재 코드 구조 (UnitView.cs)

```csharp
private static readonly int AnimIsWalking = Animator.StringToHash("IsWalking");
private static readonly int AnimIsDead = Animator.StringToHash("IsDead");
private static readonly int AnimAttack = Animator.StringToHash("Attack");

// PlayAttackAnimation:
SetAnimatorTrigger(AnimAttack);
float attackDuration = _config != null ? 2f / _config.AnimationFps : 0.33f;
yield return new WaitForSeconds(attackDuration);
_attackCoroutine = null;

// 이동/정지:
SetAnimatorBool(AnimIsWalking, true/false);
```

## 해결 방향 (MEMORY 기록 기반, 개선 포함)

Animator.Play() 직접 호출 방식:
- `_animator.Play(StateAttack, 0, 0f)` → Attack 상태로 직접 점프 (트랜지션 없음)
- 1프레임 대기 후 `GetCurrentAnimatorStateInfo(0).length`로 실제 클립 길이 읽기
- 클립 길이만큼 대기 후 `_attackCoroutine = null` (Walk 복귀 없음)
- Walk 복귀는 전투 루프 탈출 시에만 처리

이동/정지는 `_animator.speed`로 처리:
- 이동: `_animator.speed = 1f` + `Play(StateWalk, 0, 0f)` (처음 시작 시)
- 정지(전투 중): `_animator.speed = 0f`
- 재개: `_animator.speed = 1f`

## Animator Controller 확인 필요 사항

- Walk, Attack, Dead 스테이트 이름 확인
- `Any State → Dead (IsDead=true)` 트랜지션만 남기고 나머지 제거 필요
- Attack Exit Time 트랜지션 제거 필요 (있다면)

## 영향 범위

- `UnitView.cs` — PlayAttackAnimation, MoveAlongPath(이동/정지 처리), TriggerAttackAnimation
- Animator Controller (에디터 작업)
