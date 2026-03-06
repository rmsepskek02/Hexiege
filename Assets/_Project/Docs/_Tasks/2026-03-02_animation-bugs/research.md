# research.md — 애니메이션 버그 3종 (2026-03-02)

## 대상 파일
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`
- `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`

---

## 버그 1: 공격 애니메이션이 끝나지 않았는데 이동하는 현상

### 코드 흐름
```
UnitView.MoveAlongPath() 코루틴
  └─ Lerp 이동 루프 내부
       ├─ _combatUseCase.StartAttack() 호출 (line ~385)
       │   └─ GameEvents.OnEntityAttacked 발행
       │       └─ UnitView.PlayAttackAnimation() 코루틴 시작 (_attackCoroutine)
       └─ while (_attackCoroutine != null) yield return null; 대기 (lines ~387-393)
```

### 원인
- `MoveAlongPath`에서 `_attackCoroutine != null` 조건으로 대기하는 구조이지만,
  **연쇄 공격(인접 적 여러 명)** 상황에서 첫 공격 코루틴 종료 직후 두 번째 공격이 즉시 시작되면,
  `_attackCoroutine`이 null이 되는 순간(1프레임 이내)에 이동 루프가 재개될 수 있음.
- 또한 `PlayAttackAnimation()` 종료 시 `Animator.Play(StateWalk)`를 호출하는데,
  다음 공격이 없는 상황에서도 Walk 상태로 전환 후 이동 루프가 다음 타일로 진행함.

### 영향 범위
- `_attackCoroutine` (UnitView.cs:108)
- `MoveAlongPath()` 코루틴 (이동 대기 로직)
- 싱글플레이: 직접 이벤트로 발행 → 즉시 반응
- 멀티플레이: 서버 쿨다운(attackTimers)으로 빈도가 낮아 덜 발생하나 동일 구조적 문제 존재

---

## 버그 2: 공격과 공격 애니메이션 사이에 뚝 끊어지는 현상

### 코드 흐름
```
PlayAttackAnimation() / PlayAttackAnimationByHex() 코루틴
  ├─ Animator.Play(StateAttack, 0, 0f)  ← Attack 즉시 점프
  ├─ yield return null
  ├─ attackDuration = GetCurrentAnimatorStateInfo(0).length
  ├─ yield return new WaitForSeconds(attackDuration)
  └─ Animator.Play(StateWalk, 0, 0f)   ← Walk 즉시 점프 (normalizedTime=0)
```

### 원인
- `Animator.Play(StateWalk, 0, 0f)` 호출 시 `normalizedTime=0` — Walk 애니메이션이 **첫 프레임부터 재시작**
- 연속 공격 시 매번 Walk→Attack→Walk 전환이 "뚝" 끊기는 느낌을 유발
- Animator 트랜지션을 거치지 않고 `Play()`로 직접 상태를 강제 전환하기 때문
- `Animator.speed` 복구 시점(`SetAnimatorSpeed(1f)`)도 급격한 전환에 기여

### 영향 범위
- `StateWalk`, `StateAttack` 해시 (UnitView.cs:58-59)
- `PlayAttackAnimation()`, `PlayAttackAnimationByHex()` 코루틴 (UnitView.cs)
- 싱글플레이 / 멀티플레이 동일한 문제

---

## 버그 3: 죽음 애니메이션이 끝나기 전 오브젝트 삭제되는 현상

### 코드 흐름
```
GameEvents.OnEntityDied 발행
  └─ UnitView 구독 핸들러 (line ~188-199)
       ├─ SetAnimatorBool(AnimIsDead, true)   ← IsDead 파라미터 설정
       └─ Destroy(gameObject)                 ← 즉시 삭제 (같은 프레임!)
```

### 원인
- `SetAnimatorBool(AnimIsDead, true)` 호출 시 Animator 상태 전환은 **다음 프레임**에 일어남
- `Destroy(gameObject)`는 같은 프레임에 호출됨 → 오브젝트가 제거되면서 IsDead 애니메이션은 재생되지 않음
- `Destroy()`는 현재 프레임 말에 실행되지만, Animator 업데이트도 현재 프레임에 이루어지므로
  실질적으로 애니메이션 재생 기회가 전혀 없음

### 멀티플레이 추가 문제
- 서버: `OnEntityDied` → `EntityDiedClientRpc()` 발행
- 클라이언트: `HandleUnitDied()` → `OnEntityDied` 재발행 → UnitView의 즉시 삭제
- 네트워크 패킷 도착 순서에 따라 HP 동기화 전에 삭제될 수도 있음

### 영향 범위
- `AnimIsDead` 파라미터 (UnitView.cs:55)
- `Destroy(gameObject)` (UnitView.cs:196)
- `UnitSpawnUseCase.RemoveUnit()` — 삭제 타이밍에 영향
- 싱글플레이 동일, 멀티플레이에서 더 심함

---

## 공통 관찰 사항
- 세 버그 모두 **UnitView.cs 단일 파일** 내에서 수정 가능
- `UnitMovementUseCase`, `UnitCombatUseCase`는 수정 불필요
- `NetworkCombatController`는 버그 3 멀티플레이 케이스에서 고려 필요할 수 있으나,
  UnitView 측 수정으로 해결 가능
