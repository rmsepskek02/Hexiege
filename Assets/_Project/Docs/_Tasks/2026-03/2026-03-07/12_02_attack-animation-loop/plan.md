# Plan: 공격 애니메이션 루프 개선

**날짜:** 2026-03-07

---

## 코드 변경 (UnitView.cs)

### 1. 상수 변경

```csharp
// 제거
private static readonly int AnimIsWalking = Animator.StringToHash("IsWalking");
private static readonly int AnimAttack = Animator.StringToHash("Attack");

// 추가
private static readonly int StateWalk = Animator.StringToHash("Walk");
private static readonly int StateAttack = Animator.StringToHash("Attack");
private static readonly int AnimIsDead = Animator.StringToHash("IsDead"); // 유지
```

### 2. PlayAttackAnimation 수정

```csharp
private IEnumerator PlayAttackAnimation(float yAngle)
{
    transform.rotation = Quaternion.Euler(0f, yAngle, 0f);

    if (_animator != null)
    {
        _animator.speed = 1f;
        _animator.Play(StateAttack, 0, 0f);
        yield return null; // 1프레임 대기 — Animator 상태 갱신 후 length 읽기

        float clipLen = _animator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(clipLen);
    }

    _attackCoroutine = null;
    // Walk 복귀 없음 — 전투 루프 탈출 시에만 처리
}
```

### 3. 이동/정지 처리 변경

```csharp
// 기존: SetAnimatorBool(AnimIsWalking, true/false)
// 변경: _animator.speed 제어

// Walk 시작 (MoveAlongPath 진입 시):
if (_animator != null) { _animator.speed = 1f; _animator.Play(StateWalk, 0, 0f); }

// Walk 일시정지 (전투 진입 시):
if (_animator != null) _animator.speed = 0f;

// Walk 재개 (전투 탈출 시):
if (_animator != null) _animator.speed = 1f;

// Walk 종료 (이동 완료 시):
if (_animator != null) _animator.speed = 0f;
```

### 4. 사망 처리 유지

```csharp
// IsDead bool은 그대로 유지 (Any State → Dead 트랜지션용)
if (_animator != null) { _animator.speed = 1f; _animator.SetBool(AnimIsDead, true); }
```

### 5. SetAnimatorBool / SetAnimatorTrigger 래퍼 제거 or 정리

`SetAnimatorBool(AnimIsWalking, ...)` 호출부 → speed 제어로 교체
`SetAnimatorTrigger(AnimAttack)` 호출부 → Play(StateAttack) 로 교체

---

## 에디터 작업 (Animator Controller)

1. **Walk 스테이트 이름 확인**: "Walk" 이어야 함 (대소문자 포함)
2. **Attack 스테이트 이름 확인**: "Attack" 이어야 함
3. **불필요한 트랜지션 제거**: Attack → Walk (Exit Time) 트랜지션 있으면 삭제
4. **남길 트랜지션**: `Any State → Dead (IsDead=true)` 하나만
5. **IsWalking 파라미터 제거**: 더 이상 사용하지 않음 (Attack 트리거도 제거)
6. **IsDead 파라미터만 유지**

---

## 애니메이션 클립 수정 (루프 끊김 개선)

**문제**: 공격 루프 반복 시 마지막 프레임과 첫 프레임 포즈가 달라 끊기는 느낌 발생

**해결**: Attack 클립의 마지막 키프레임을 첫 키프레임과 동일한 포즈로 설정 → 루프 경계에서 자연스럽게 연결

- 대상 클립: `Assets/_Project/Animations/Units/Pistoleer/Pistoleer_Attack.anim` (또는 해당 Attack 클립)
- 수정 내용: 마지막 프레임 = 첫 프레임 포즈 (완료)
