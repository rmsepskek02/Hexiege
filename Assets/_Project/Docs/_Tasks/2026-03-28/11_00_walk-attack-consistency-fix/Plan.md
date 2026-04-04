# Plan: Walk→Attack 전환 일관성 수정 및 스폰 회전 오류 수정

**작성일:** 2026-03-28

---

## 목표

1. 공격 이력 유무와 무관하게 Walk→Attack 전환이 동일하게 동작하도록 수정
2. 이동 중 방향 불일치 수정 — Red 클라이언트에서 첫 타일부터 경로 전체에 걸쳐 반대 방향을 바라보는 문제

---

## 수정 1: `_isWalkPending` 제거 (Walk→Attack 일관성)

### 수정 위치: `UnitView.cs`

#### `StartWalkAnimation()` — `_attackCoroutine` 가드 제거

**현재:**
```csharp
public void StartWalkAnimation()
{
    if (_animator == null)
        _animator = GetComponentInChildren<Animator>();
    if (_animator == null) return;

    if (_attackCoroutine != null)   // ← 이 블록 제거
    {
        _isWalkPending = true;
        return;
    }

    _isWalkPending = false;
    _animator.speed = 1f;
    _animator.CrossFadeInFixedTime(StateWalk, _idleToWalkBlend, 0);
}
```

**수정 후:**
```csharp
public void StartWalkAnimation()
{
    if (_animator == null)
        _animator = GetComponentInChildren<Animator>();
    if (_animator == null) return;

    _isWalkPending = false;
    _animator.speed = 1f;
    _animator.CrossFadeInFixedTime(StateWalk, _idleToWalkBlend, 0);
}
```

→ `StartWalkRpc` 도착 시 현재 상태(Attack 포함)에서 즉시 Walk CrossFade 시작.

---

#### `PlayAttackAnimation()` 종료부 — `_isWalkPending` 블록 제거

**현재:**
```csharp
_attackCoroutine = null;

if (_isWalkPending)
{
    _isWalkPending = false;
    yield return null;  // 1프레임 대기
    if (_attackCoroutine == null)
        StartWalkAnimation();
}
```

**수정 후:**
```csharp
_attackCoroutine = null;
// _isWalkPending 블록 제거 — Walk 시작은 StartWalkRpc가 전담
```

---

#### `_isWalkPending` 필드 제거 (선택)

```csharp
// 제거
private bool _isWalkPending;
```

`StopWalkAnimation()`에서도 `_isWalkPending = false;`를 사용하므로 함께 제거.
`StopWalkAnimation()` 내부의 `_isWalkPending = false;`도 제거.

---

### 기대 결과

```
[수정 전 - 미공격 유닛]  Walk(안정) → TriggerAttackRpc → Attack         (자연스러움)
[수정 전 - 공격경험 유닛] Walk(방금 시작) → TriggerAttackRpc → Attack    (제자리 걷기 발생)

[수정 후 - 모든 유닛]     Walk CrossFade 시작 → TriggerAttackRpc → Attack (일관적)
```

---

## 수정 2: 이동 중 회전 방향 불일치 수정

### 원인 요약

`MoveAlongPath`에서 `OnUnitFacingChanged`는 `i == 1` (첫 번째 스텝)과 전투 재개 시점에만 발행됨.
나머지 스텝의 방향 전환은 `NetworkUnit.LateUpdate`의 델타 회전에 의존.

Red 클라이언트에서 `LateUpdate`는 `ViewConverter.ToView()`로 반전된 좌표를 기준으로 델타를 계산하므로,
서버(Blue)가 East(90°)로 이동 중인 Red 유닛을 클라이언트에서 West(270°)로 계산하는 방향 역전이 발생.

`TurnToFaceClientRpc`는 도메인 각도(90°)를 전송 → 카메라 방향은 양 팀 동일하므로 이 값이 정상.
→ **클라이언트 방향의 단일 권위는 `TurnToFaceClientRpc`여야 한다.**

### 수정 위치: `UnitView.cs` — `MoveAlongPath()`

**현재:**
```csharp
// i == 1 블록에서만 OnUnitFacingChanged 발행
if (i == 1)
{
    if (NetworkContext.IsNetworkActive)
    {
        int dirIndex = (int)dir;
        float yAngle = dirIndex >= 0 && dirIndex < DirectionAngles.Length
            ? DirectionAngles[dirIndex]
            : transform.eulerAngles.y;
        GameEvents.OnUnitFacingChanged.OnNext(new UnitFacingChangedEvent(_unitData.Id, yAngle, _rotationDuration));
    }
    yield return new WaitForSeconds(_rotationDuration);
}
```

**수정 후:**
```csharp
if (NetworkContext.IsNetworkActive)
{
    // 방향이 바뀔 때마다 OnUnitFacingChanged 발행 — LateUpdate 델타 추정에 의존하지 않음
    // Red 클라이언트에서 LateUpdate 델타는 반전 좌표 기준이라 방향이 역전되므로,
    // 모든 방향 전환을 서버 RPC로 클라이언트에 직접 전달.
    int dirIndex = (int)dir;
    float yAngle = dirIndex >= 0 && dirIndex < DirectionAngles.Length
        ? DirectionAngles[dirIndex]
        : transform.eulerAngles.y;
    GameEvents.OnUnitFacingChanged.OnNext(new UnitFacingChangedEvent(_unitData.Id, yAngle, _rotationDuration));
}

// 첫 스텝에서만 회전 완료까지 대기 — 매 스텝 대기하면 이동이 느려짐
if (i == 1)
    yield return new WaitForSeconds(_rotationDuration);
```

→ 방향이 변하지 않는 스텝에서도 이벤트를 발행해도 되지만,
  `TurnToFaceClientRpc`의 DORotate는 같은 각도면 시각적으로 무해함.

---

### 수정 위치: `NetworkUnit.cs` — `LateUpdate()` 델타 회전 비활성화

LateUpdate 델타 회전은 이제 `TurnToFaceClientRpc`가 전담하므로 불필요.
Red 클라이언트 위치 보정(①)은 유지, 델타 기반 회전(②)만 제거.

**현재:**
```csharp
// ② 이동 방향 기반 회전 계산
if (!_isPreRotating && _hasInitialPosition)
{
    Vector3 delta = transform.position - _prevPosition;
    delta.y = 0f;
    if (delta.sqrMagnitude > 0.0001f)
    {
        float angle = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg - MeshYOffset;
        Quaternion targetRot = Quaternion.Euler(0f, angle, 0f);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRot, 720f * Time.deltaTime);
    }
}
_prevPosition = transform.position;
_hasInitialPosition = true;
```

**수정 후:**
```csharp
// 델타 기반 회전 블록 제거.
// 모든 방향 전환은 MoveAlongPath → OnUnitFacingChanged → TurnToFaceClientRpc가 처리.
// Red 클라이언트에서 반전 좌표 델타는 방향 역전을 유발하므로 사용 불가.
```

`_prevPosition`, `_hasInitialPosition`, `MeshYOffset` 필드 및 관련 참조도 함께 제거.
`ResetMovementTracking()`에서 `_hasInitialPosition = false` 참조도 제거 (또는 메서드 단순화).

---

## 수정 순서

```
[1] UnitView.cs — StartWalkAnimation() _attackCoroutine 가드 제거
[2] UnitView.cs — PlayAttackAnimation() _isWalkPending 블록 제거
[3] UnitView.cs — _isWalkPending 필드 제거 (StopWalkAnimation 참조도 함께 제거)
[4] UnitView.cs — MoveAlongPath() OnUnitFacingChanged를 i == 1 조건 외부로 이동
[5] NetworkUnit.cs — LateUpdate() 델타 회전 블록 제거 + 관련 필드 정리
[6] 컴파일 확인
```

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| Attack 코루틴 실행 중 Walk CrossFade → 시각적 블렌딩 | Attack→Walk CrossFade 후 TriggerAttackRpc가 즉시 덮어씌우므로 사실상 보이지 않음 |
| `_isWalkPending = false`가 `StopWalkAnimation`에서 사용됨 | 필드와 함께 해당 줄도 제거 |
| 싱글플레이에서도 동일하게 동작하는지 | 싱글플레이도 같은 CrossFade 경로 사용 — 영향 없음 |
| MoveAlongPath에서 방향 불변 스텝에 OnUnitFacingChanged 발행 | TurnToFaceClientRpc DORotate는 같은 각도 입력 시 무해 |
| ResetMovementTracking에서 _hasInitialPosition 참조 제거 시 | _isPreRotating = false만 남기거나 메서드 자체 단순화 |
| 싱글플레이에서 LateUpdate 델타 회전 제거 후 방향 추적 손실 | LateUpdate는 `NetworkContext.IsNetworkActive` 조건부 실행이므로 싱글플레이 영향 없음 |

---

## 체크리스트

- [ ] `StartWalkAnimation()` — `_attackCoroutine != null` 가드 제거
- [ ] `PlayAttackAnimation()` — `_isWalkPending` 블록 제거
- [ ] `_isWalkPending` 필드 제거 + 관련 참조 모두 제거
- [ ] `MoveAlongPath()` — `OnUnitFacingChanged` 발행을 `i == 1` 조건 외부로 이동 (모든 스텝)
- [ ] `NetworkUnit.LateUpdate()` — 델타 회전 블록 제거
- [ ] `NetworkUnit` — `_prevPosition`, `_hasInitialPosition`, `MeshYOffset` 정리
- [ ] 컴파일 확인
- [ ] 멀티플레이 테스트: 미공격 유닛 Walk→Attack 전환
- [ ] 멀티플레이 테스트: 공격경험 유닛 Walk→Attack 전환 (일관성 확인)
- [ ] 멀티플레이 테스트: 이동 중 방향 정상 (Red 클라이언트, 첫 타일부터 경로 전체)
