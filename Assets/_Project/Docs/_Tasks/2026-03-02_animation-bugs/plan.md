# plan.md — 애니메이션 버그 수정 (AnimatorController + 코드 최소 수정)
# 작성: 2026-03-02 | 이전 plan은 롤백됨

## 최종 채택 방식
**AnimatorController 파라미터/트랜지션 추가 + UnitView.cs 최소 수정**

---

## 버그 진단

| # | 증상 | 원인 |
|---|------|------|
| Bug 1 | 공격 중 이동이 즉시 재개됨 | `PlayAttackAnimation` 대기 시간이 실제 클립 길이와 불일치 (`2f / AnimationFps` → AnimationFps 필드 없음 → NaN/0) |
| Bug 2 | 공격 애니메이션이 재생 안 됨 | AnimatorController에 `Attack` Trigger 파라미터 없음 → `SetTrigger("Attack")` 완전 무시 |
-> 이런 버그는 어디서 찾아왔는지 확인해줘
| Bug 3 | 사망 애니메이션 재생 전 오브젝트 삭제 | `SetAnimatorBool(IsDead, true)` 직후 `Destroy(gameObject)` 동 프레임 실행 |

---

## 수정 범위

### Phase 1 — AnimatorController 에디터 수정
파일: `Assets/_Project/Animations/Units/Pistoleer/Pistoleer.controller`

**현재 상태:**
- 파라미터: `IsDead` (Bool) 1개만 존재
- 트랜지션: AnyState → Dead 1개만 존재
- Walk 상태가 default → 항상 Walk 루프

**추가할 것:**

1. **파라미터 추가**
   - `Attack` (Trigger) — 공격 트리거

2. **트랜지션 추가 (2개)**

   | 트랜지션 | Condition | Has Exit Time | Exit Time | Duration |
   |----------|-----------|---------------|-----------|----------|
   | Walk → Attack | Attack (Trigger) | OFF | - | 0 |
   | Attack → Walk | (없음) | ON | 1.0 | 0 |

   - Walk → Attack: 트리거 수신 즉시 전환 (끊김 없음)
   - Attack → Walk: 공격 클립 끝까지 재생 후 자동 복귀 (연속 공격 시 끊김 없음)

3. **IsWalking 파라미터는 추가하지 않음**
   - Walk는 항상 default 상태 → 이동/정지 구분은 `Animator.speed = 0/1`로 코드에서 처리

---

### Phase 2 — UnitView.cs 코드 수정 (3곳)

#### 수정 1: SetAnimatorBool(AnimIsWalking) → Animator.speed

**이유:** AnimatorController에 IsWalking 파라미터가 없어 현재 SetBool 호출이 무시됨.
Walk 상태를 항상 유지하면서 speed=0으로 정지, speed=1로 이동 표현.

```csharp
// 기존 (Walk 시작):
SetAnimatorBool(AnimIsWalking, true);

// 변경:
if (_animator != null) _animator.speed = 1f;

// 기존 (Walk 정지):
SetAnimatorBool(AnimIsWalking, false);

// 변경:
if (_animator != null) _animator.speed = 0f;
```

변경 위치 (2곳):
- `MoveAlongPath` — Walk 시작 (line 280)
- `StopMovement` 및 `MoveAlongPath` 완료 — Walk 정지 (line 216, 359)

---

#### 수정 2: PlayAttackAnimation — 실제 클립 길이 사용

**이유:** `2f / _config.AnimationFps`는 GameConfig에 AnimationFps 필드가 없어 오동작.
`GetCurrentAnimatorStateInfo(0).length`로 실제 Attack 클립 길이를 읽어 대기.

```csharp
// 기존:
private IEnumerator PlayAttackAnimation(HexDirection direction)
{
    ApplyDirection(direction);
    SetAnimatorTrigger(AnimAttack);
    float attackDuration = _config != null ? 2f / _config.AnimationFps : 0.33f;
    yield return new WaitForSeconds(attackDuration);
    _attackCoroutine = null;
}

// 변경:
private IEnumerator PlayAttackAnimation(HexDirection direction)
{
    ApplyDirection(direction);
    if (_animator != null) _animator.speed = 1f;  // speed=0 상태일 수 있으므로 복원
    SetAnimatorTrigger(AnimAttack);
    yield return null;                             // 1프레임 대기 — Animator가 Attack 상태로 전환 완료
    float attackDuration = _animator != null
        ? _animator.GetCurrentAnimatorStateInfo(0).length
        : 0.33f;
    yield return new WaitForSeconds(attackDuration);
    _attackCoroutine = null;
}
```

---

#### 수정 3: OnEntityDied — 사망 애니메이션 후 삭제

**이유:** 현재 `Destroy(gameObject)` 즉시 실행으로 Dead 애니메이션 재생 불가.

```csharp
// 기존 (SetDependencies 내 OnEntityDied 핸들러):
SetAnimatorBool(AnimIsDead, true);
Destroy(gameObject);

// 변경:
SetAnimatorBool(AnimIsDead, true);
StartCoroutine(DestroyAfterDeadAnimation());
```

**추가 코루틴:**
```csharp
private IEnumerator DestroyAfterDeadAnimation()
{
    yield return null;  // 1프레임 대기 — Animator가 Dead 상태로 전환 완료
    float len = _animator != null
        ? _animator.GetCurrentAnimatorStateInfo(0).length
        : 1.5f;
    yield return new WaitForSeconds(len);
    Destroy(gameObject);
}
```

---

## 각 버그 해결 확인

| Bug | 해결 방식 |
|-----|----------|
| 1. 공격 중 이동 재개 | `yield return null` + `GetCurrentAnimatorStateInfo(0).length` → 실제 Attack 클립 길이만큼 대기 → `_attackCoroutine = null` → MoveAlongPath 이동 재개 |
| 2. 공격 애니메이션 미재생 | Attack Trigger 파라미터 추가 + Walk→Attack 트랜지션 → SetTrigger 이제 동작함 |
| 3. 사망 즉시 삭제 | `DestroyAfterDeadAnimation` 코루틴 → Dead 클립 길이만큼 대기 후 삭제 |

---

## 작업 순서

1. **[에디터] Pistoleer.controller** — Attack Trigger 추가, Walk→Attack / Attack→Walk 트랜지션 추가
2. **[코드] UnitView.cs** — 수정 1, 2, 3 적용
3. **[컴파일 확인]** Unity 에디터에서 오류 없음 확인
4. **[테스트]** 싱글플레이 + 멀티플레이 공격/이동/사망 동작 확인

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| Attack→Walk Exit Time=1.0 기준이 샘플링 오차로 복귀 누락 | AnimatorController 기본 동작 — 클립 마지막 프레임 도달 시 자동 전환, 실용상 문제 없음 |
| `GetCurrentAnimatorStateInfo(0)` 가 Attack이 아닌 Walk를 반환 | `yield return null` 1프레임 대기 후 읽으면 전환 완료 보장 (Unity 보장 동작) |
| Animator.speed=0 상태에서 Attack 트리거 수신 | `PlayAttackAnimation` 첫 줄에서 speed=1 복원 후 SetTrigger → 정상 동작 |
| 사망 후 기존 공격 코루틴 남아있는 경우 | `OnEntityDied`에서 `Destroy` 대신 코루틴만 시작 → 기존 coroutine은 gameObject 파괴 시 자동 중단 |
