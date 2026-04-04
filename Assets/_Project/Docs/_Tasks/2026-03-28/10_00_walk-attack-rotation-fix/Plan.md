# Plan: Walk→Attack 전환 멈춤 & 회전 불일치 수정

**작성일:** 2026-03-28

---

## 목표

멀티플레이 클라이언트에서 발생하는 두 가지 버그 수정:
1. 이동 중 적 감지 시 Walk → Idle(멈춤) → Attack 전환 현상 제거
2. 유닛이 이동/공격 방향과 다른 방향을 바라보는 현상 수정

---

## 수정 1: 멈춤현상 제거

### 원인

`MoveAlongPath` 전투 감지 블록에서 `OnUnitWalkStopped` 이벤트를 발행하면
→ `StopWalkAnimationClientRpc` 전송
→ 클라이언트: `CrossFade(Idle)` 시작
→ 50ms 후 `TriggerAttackAnimationClientRpc` 도착
→ 클라이언트: `CrossFade(Attack)` — 이 사이 50ms 동안 Idle(멈춤) 재생

`TriggerAttackAnimationClientRpc` 내부에서 이미 `StopWalkAnimation()` + `TriggerAttackAnimation()`을 같은 프레임에 호출하므로 `StopWalkRpc`는 전투 감지 시 불필요.

### 수정 위치

**`UnitView.cs` — `MoveAlongPath()` 멀티플레이 전투 감지 블록**

**제거 대상:**
```csharp
// 아래 1줄 제거
GameEvents.OnUnitWalkStopped.OnNext(_unitData.Id);
```

**유지 대상 (제거하면 안 됨):**
```csharp
// 이동 완료 시 (경로 끝) — 유지
if (NetworkContext.IsNetworkActive)
    GameEvents.OnUnitWalkStopped.OnNext(_unitData.Id);
```
```csharp
// StopMovement() 강제 정지 시 — 유지
if (NetworkContext.IsNetworkActive && NetworkContext.IsNetworkServer)
    GameEvents.OnUnitWalkStopped.OnNext(_unitData.Id);
```

### 기대 결과

```
[수정 전] Walk → CrossFade(Idle) 50ms → CrossFade(Attack)
[수정 후] Walk → (TriggerAttackRpc 도착 즉시) CrossFade(Attack)  ← 직접 전환
```

---

## 수정 2: 회전 불일치 수정

### 원인 A — 공격 DORotate 중 LateUpdate 간섭

`PlayAttackAnimation`에서 `DORotate(공격 방향)` 시작 시
`_isPreRotating`을 설정하지 않음
→ `NetworkUnit.LateUpdate`가 위치 델타로 계산한 이동 방향으로 회전을 즉시 덮어씀

`TurnToFaceClientRpc`는 `SetPreRotating(true)`로 LateUpdate를 차단하지만,
`PlayAttackAnimation`의 DORotate는 동일한 보호 장치가 없음.

**수정 위치: `NetworkUnit.cs` + `UnitView.cs`**

`NetworkUnit`에 공격 회전 보호용 API 추가:
```csharp
// NetworkUnit.cs에 추가
public void SetAttackRotating(bool value) { _isPreRotating = value; }
```

`UnitView.TriggerAttackAnimation()`에서 공격 전 LateUpdate 차단:
```csharp
// UnitView.cs — TriggerAttackAnimation() 수정
public void TriggerAttackAnimation(int targetId, bool targetIsUnit)
{
    if (_unitData == null || !_unitData.IsAlive) return;

    if (_attackCoroutine != null)
        StopCoroutine(_attackCoroutine);

    // 공격 DORotate 중 LateUpdate 간섭 차단
    _networkUnit?.SetAttackRotating(true);

    float angle = CalculateAttackAngle(GetTargetWorldPos(targetId, targetIsUnit));
    _attackCoroutine = StartCoroutine(PlayAttackAnimation(angle));
}
```

`PlayAttackAnimation`에서 DORotate 완료 시 차단 해제:
```csharp
// PlayAttackAnimation 회전 부분 수정
transform.DOKill();
transform.DORotate(new Vector3(0f, yAngle, 0f), _rotationDuration)
         .SetEase(Ease.OutQuad)
         .OnComplete(() => _networkUnit?.SetAttackRotating(false));  // 완료 시 LateUpdate 재개
```

`_networkUnit` 참조 추가 필요:
```csharp
// UnitView.cs — 필드 추가
private NetworkUnit _networkUnit;

// Initialize() 내부에 추가
_networkUnit = GetComponent<NetworkUnit>();
```

### 원인 B — 전투 후 이동 재개 시 방향 RPC 누락

전투 후 이동 재개 시 서버는 `ApplyDirection(dir)`으로 즉시 회전하지만
클라이언트에는 방향 RPC를 보내지 않음 (i > 1이므로 `TurnToFaceClientRpc` 조건 미충족).
클라이언트는 LateUpdate 델타 회전으로만 따라잡아 ~0.25초 방향 불일치 발생.

**수정 위치: `UnitView.cs` — `MoveAlongPath()` 전투 후 Walk 재개 블록**

현재:
```csharp
if (_animator != null)
{
    _animator.speed = 1f;
    _animator.CrossFadeInFixedTime(StateWalk, _idleToWalkBlend, 0);
}
GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);
```

수정 후:
```csharp
if (_animator != null)
{
    _animator.speed = 1f;
    _animator.CrossFadeInFixedTime(StateWalk, _idleToWalkBlend, 0);
}
GameEvents.OnUnitWalkStarted.OnNext(_unitData.Id);

// 전투 후 이동 재개 시 클라이언트에 현재 방향 전달
// (i > 1 구간은 TurnToFaceClientRpc가 전송되지 않으므로 여기서 명시적으로 전달)
if (NetworkContext.IsNetworkActive)
{
    int dirIndex = (int)_unitData.Facing;
    float yAngle = dirIndex >= 0 && dirIndex < DirectionAngles.Length
        ? DirectionAngles[dirIndex]
        : transform.eulerAngles.y;
    GameEvents.OnUnitFacingChanged.OnNext(
        new UnitFacingChangedEvent(_unitData.Id, yAngle, _rotationDuration));
}
```

---

## 수정 순서

```
[1] UnitView.cs — MoveAlongPath() 전투 감지 StopWalkRpc 제거   (멈춤현상)
[2] NetworkUnit.cs — SetAttackRotating() API 추가               (회전 A)
[3] UnitView.cs — _networkUnit 필드 + Initialize() 참조 추가   (회전 A)
[4] UnitView.cs — TriggerAttackAnimation() SetAttackRotating 추가 (회전 A)
[5] UnitView.cs — PlayAttackAnimation() DORotate OnComplete 추가  (회전 A)
[6] UnitView.cs — MoveAlongPath() 전투 후 방향 이벤트 추가      (회전 B)
[7] 컴파일 확인
```

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| 전투 감지 StopWalkRpc 제거 후 Walk가 너무 오래 재생될 수 있음 | TriggerAttackRpc 내 StopWalkAnimation()이 동일 프레임 처리 — 문제 없음 |
| `_isPreRotating = true` 고착 (DORotate가 DOKill로 중단되면 OnComplete 미호출) | `ResetMovementTracking()`이 `_isPreRotating = false`로 리셋 — 이미 안전망 있음 |
| 전투 후 방향 이벤트 → TurnToFaceClientRpc → 회전 대기(_rotationDuration) 발생 | TurnToFaceClientRpc는 대기 없이 DORotate만 실행. 이동은 대기 없이 계속됨 |
| 싱글플레이에서 NetworkUnit이 null | `_networkUnit?.SetAttackRotating()` null 체크로 안전하게 처리됨 |

---

## 체크리스트

- [ ] `UnitView.MoveAlongPath()` 전투 감지 StopWalkRpc 제거
- [ ] `NetworkUnit` SetAttackRotating() 추가
- [ ] `UnitView` _networkUnit 필드 + Initialize 참조
- [ ] `UnitView.TriggerAttackAnimation()` SetAttackRotating(true) 추가
- [ ] `UnitView.PlayAttackAnimation()` DORotate OnComplete SetAttackRotating(false) 추가
- [ ] `UnitView.MoveAlongPath()` 전투 후 FacingChanged 이벤트 추가
- [ ] 컴파일 확인
- [ ] 멀티플레이 테스트: 멈춤현상 없음
- [ ] 멀티플레이 테스트: 공격 방향 정상
- [ ] 멀티플레이 테스트: 전투 후 이동 재개 방향 정상
