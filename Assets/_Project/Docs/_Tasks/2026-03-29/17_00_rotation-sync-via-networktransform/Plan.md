# Plan: NetworkTransform Rotation 동기화로 전환

**작성일:** 2026-03-29
**Research 참조:** `Research.md`

---

## 전략

1. NetworkTransform의 SyncRotAngleY를 ON으로 변경
2. 서버의 DORotate를 즉시 스냅으로 교체 (이중 보간 제거)
3. 클라이언트 자체 회전 로직 전면 제거
4. Red 클라이언트 rotation 반전을 LateUpdate에 1줄 추가

---

## 수정 1: NetworkTransform SyncRotAngleY ON

**대상:** `Scripts/Editor/AddNetworkTransformToPrefabs.cs`

**변경:**
```
existingNt.SyncRotAngleY = false;  →  existingNt.SyncRotAngleY = true;
```

SyncRotAngleX, Z는 false 유지 (Y축 회전만 사용).

**Editor 스크립트 실행 필요:** `Hexiege > Add NetworkTransform To Unit Prefabs` 메뉴 실행하여 프리팹에 반영.

---

## 수정 2: 서버 DORotate → 즉시 스냅

### UnitView.cs — ApplyDirection

**변경 전:**
```csharp
private void ApplyDirection(HexDirection dir)
{
    int index = (int)dir;
    if (index < 0 || index >= DirectionAngles.Length) return;
    transform.DOKill();
    transform.DORotate(new Vector3(0f, DirectionAngles[index], 0f), _rotationDuration)
             .SetEase(Ease.OutQuad)
             .SetUpdate(UpdateType.Late);
}
```

**변경 후:**
```csharp
private void ApplyDirection(HexDirection dir)
{
    int index = (int)dir;
    if (index < 0 || index >= DirectionAngles.Length) return;
    transform.rotation = Quaternion.Euler(0f, DirectionAngles[index], 0f);
}
```

### UnitView.cs — PlayAttackAnimation 내 DORotate

**변경 전:**
```csharp
transform.DOKill();
transform.DORotate(new Vector3(0f, yAngle, 0f), _rotationDuration)
         .SetEase(Ease.OutQuad)
         .SetUpdate(UpdateType.Late)
         .OnComplete(() => _networkUnit?.SetAttackRotating(false));
```

**변경 후:**
```csharp
transform.rotation = Quaternion.Euler(0f, yAngle, 0f);
```

### UnitView.cs — Initialize 내 DOKill + rotation 설정

**변경 전:**
```csharp
if (_networkUnit != null && _networkUnit.HasReceivedTurnToFace)
{
    Debug.Log(...);
}
else
{
    float spawnAngle = DirectionAngles[index];
    if (ViewConverter.IsFlipped)
        spawnAngle = (spawnAngle + 180f) % 360f;
    Debug.Log(...);
    transform.DOKill();
    transform.rotation = Quaternion.Euler(0f, spawnAngle, 0f);
}
```

**변경 후:**
```csharp
float spawnAngle = DirectionAngles[index];
transform.rotation = Quaternion.Euler(0f, spawnAngle, 0f);
```

- HasReceivedTurnToFace 가드 제거 (NetworkTransform이 동기화)
- ViewConverter.IsFlipped 보정 제거 (LateUpdate에서 일괄 처리)
- DOKill 제거 (DOTween rotation 미사용)

---

## 수정 3: 클라이언트 자체 회전 로직 제거

### NetworkUnit.cs — LateUpdate 간소화

전체 delta 기반 회전 로직과 관련 필드를 제거. Red 위치/회전 보정만 남김.

**제거할 필드:**
- `_isPreRotating`
- `_prevPosition`, `_hasInitialPosition`
- `_lateUpdateLogCount`
- `_prevLateUpdateRot`
- `MeshYOffset` 상수
- `HasReceivedTurnToFace` 프로퍼티
- `MarkTurnToFaceReceived()` 메서드
- `SetPreRotating(bool)` 메서드
- `SetAttackRotating(bool)` 메서드
- `ResetMovementTracking()` 메서드
- `ResetPositionTracking()` 메서드

**LateUpdate 변경 후:**
```csharp
private void LateUpdate()
{
    // 서버, 싱글플레이에서는 처리 불필요
    if (IsServer || !NetworkContext.IsNetworkActive) return;

    // Red 클라이언트: 서버(Blue) 좌표/회전을 Red 뷰로 반전
    if (ViewConverter.IsFlipped)
    {
        transform.position = ViewConverter.ToView(transform.position);

        Vector3 euler = transform.eulerAngles;
        euler.y = (euler.y + 180f) % 360f;
        transform.rotation = Quaternion.Euler(euler);
    }
}
```

### NetworkCombatController.cs — TurnToFace 관련 제거

**제거:**
- `_facingChangedSubscription` 필드
- OnNetworkSpawn()에서 `OnUnitFacingChanged` 구독
- OnNetworkDespawn()에서 구독 해제
- `TurnToFaceClientRpc()` 메서드 전체

**ApplyStartWalkWithRetry 간소화:**
- `networkUnit?.ResetPositionTracking()` 호출 제거

### UnitView.cs — 불필요한 코드 제거

**제거할 필드/참조:**
- `_networkUnit` 필드 (공격 DORotate 보호용이었으므로)
- `_isWalkPending` 필드
- `_meshYOffset` SerializeField (서버 즉시 스냅이므로 Atan2 미사용... 확인 필요)

**TriggerAttackAnimation 변경:**
- `_networkUnit?.SetAttackRotating(true)` 제거
- `_isWalkPending = false` 제거

**PlayAttackAnimation 변경:**
- DORotate → 즉시 스냅 (수정 2에서 처리)
- OnComplete 콜백 제거

**StartWalkAnimation 변경:**
- `_isWalkPending` 관련 코드 제거
- 공격 중 Walk 보류 로직 제거 → 단순히 Walk CrossFade만 수행

**StopWalkAnimation 변경:**
- `_isWalkPending = false` 제거

**MoveAlongPath 변경:**
- i==1 구간의 `OnUnitFacingChanged` 발행 제거
- 전투 후 재개 시 `OnUnitFacingChanged` 발행 제거
- `WaitForSeconds(_rotationDuration)` 대기 제거 (서버 즉시 스냅이므로 대기 불필요)

### GameEvents.cs — FacingChanged 이벤트 제거

- `OnUnitFacingChanged` Subject 제거
- `UnitFacingChangedEvent` struct 제거

---

## 수정 4: 공격 방향 계산 유지 확인

서버에서 공격 시 타겟 방향 계산은 여전히 필요:

```csharp
// 서버에서 실행 — Atan2 기반 공격 방향 계산
float angle = CalculateAttackAngle(GetTargetWorldPos(targetId, targetIsUnit));
transform.rotation = Quaternion.Euler(0f, angle, 0f);
```

`CalculateAttackAngle()`, `GetTargetWorldPos()`, `_meshYOffset`은 서버에서 여전히 사용.
`_unitFactory`, `_buildingFactory` 참조도 유지.

---

## 수정 5: _isWalkPending 제거 후 동작 확인

**현재 _isWalkPending의 역할:**
공격 코루틴 중에 StartWalkAnimationClientRpc가 도착하면 Walk를 보류.

**제거 후 동작:**
- 공격 중 StartWalkAnimationClientRpc 도착 → 즉시 Walk CrossFade
- 같은 프레임 또는 직후에 TriggerAttackAnimationClientRpc 도착 → Attack CrossFade로 덮어씌움
- 결과: Walk가 1-2프레임 재생되지만 Attack이 즉시 덮어써서 육안으로 구분 불가

**대안:** 공격 코루틴 실행 중이면 Walk CrossFade를 무시하는 단순 가드:
```csharp
public void StartWalkAnimation()
{
    if (_attackCoroutine != null) return;  // 공격 중이면 무시
    ...
}
```

이 방식이 _isWalkPending보다 단순하고, 서버가 전투 루프 탈출 시 다시 Walk RPC를 보내므로 안전.

---

## 수정 파일 요약

| 파일 | 변경 내용 |
|------|----------|
| `Editor/AddNetworkTransformToPrefabs.cs` | SyncRotAngleY = true |
| `Presentation/Unit/UnitView.cs` | DORotate → 즉시 스냅, _isWalkPending → 단순 가드, FacingChanged 이벤트 발행 제거 |
| `Infrastructure/Network/NetworkUnit.cs` | LateUpdate 간소화 (Red 위치/회전 보정만), 회전 관련 필드/메서드 전면 제거 |
| `Infrastructure/Network/NetworkCombatController.cs` | TurnToFaceClientRpc 제거, FacingChanged 구독 제거, ApplyStartWalkWithRetry 간소화 |
| `Application/Events/GameEvents.cs` | OnUnitFacingChanged, UnitFacingChangedEvent 제거 |

---

## 수정 7: 적 감지 즉시 공격 트리거 (TickCombat 인터벌 우회)

**발견 경위:** 테스트 결과 이동 후 적 감지 시 Walk 애니메이션이 잠시 재생된 뒤 Attack으로 전환되는 현상.

**원인:**
`MoveAlongPath`는 `HasEnemyInRange` 즉시 이동을 멈추지만, Attack RPC는 `TickCombat`이 다음 Tick(최대 50ms)에 실행될 때까지 전송되지 않음.
이 0~50ms 동안 Walk가 재생됨.

**해결 방향:**
`MoveAlongPath`에서 적 감지 즉시 `GameEvents.OnUnitEnteredCombat` 이벤트 발행.
`NetworkCombatController`가 이를 구독하고 `TryFindTarget` → Attack RPC를 즉시 전송.
쿨다운/데미지 관리는 기존 TickCombat이 유지.

**변경 파일:**

| 파일 | 변경 내용 |
|------|----------|
| `GameEvents.cs` | `OnUnitEnteredCombat` Subject<int> 추가 |
| `UnitView.cs` | MoveAlongPath 적 감지 시 `OnUnitEnteredCombat` 발행 |
| `NetworkCombatController.cs` | `OnUnitEnteredCombat` 구독 + `ExecuteAttack()` 헬퍼 추출 |

**ExecuteAttack 헬퍼 추출:**
TickCombat의 공격 실행 로직을 별도 메서드로 추출하여 TickCombat과 즉시 트리거 양쪽에서 재사용:
```csharp
private void ExecuteAttack(UnitData unit, int targetId, bool targetIsUnit)
{
    TriggerAttackAnimationClientRpc(unit.Id, targetId, targetIsUnit);
    unit.AttackCooldownRemaining = unit.AttackCooldown;
    StartCoroutine(DelayedAttackDamage(unit, targetId, targetIsUnit, unit.HitFrameTime));
}
```

---

## 수정 6: 공격 중 Walk 대기 처리 (_isWalkPending 복원)

**발견 경위:** 테스트 결과 MULTI-5, MULTI-6에서 공격 애니메이션 상태로 이동하는 현상 확인.

**원인:**
수정 5에서 `_isWalkPending`을 제거하고 `if (_attackCoroutine != null) return` 가드를 넣었으나,
이 가드가 Walk 전환을 완전히 차단함. 타겟이 사라져 이동 명령이 와도 공격 코루틴이 끝날 때까지
Walk가 시작되지 않아 유닛이 공격 애니메이션 상태로 이동.

**해결:**
`_isWalkPending` 플래그를 단순하게 복원. 이전 BUG-B의 원인은 `ResetMovementTracking` retry 로직이었고,
`_isWalkPending` 플래그 자체는 올바른 패턴이었음.

**변경 방향 (UnitView.cs):**

`StartWalkAnimation()`:
```csharp
public void StartWalkAnimation()
{
    if (_attackCoroutine != null)
    {
        _isWalkPending = true;  // 공격 완료 후 Walk 시작하도록 대기 등록
        return;
    }
    _isWalkPending = false;
    // ... Walk CrossFade
}
```

`PlayAttackAnimation()` 코루틴 종료 시:
```csharp
_attackCoroutine = null;
if (_isWalkPending)
{
    _isWalkPending = false;
    StartWalkAnimation();
}
else
{
    // Idle CrossFade
}
```

`StopWalkAnimation()`:
```csharp
_isWalkPending = false;
```

**설계 원칙:**
- 공격 애니메이션은 클립 끝까지 재생 (스윙 중단 없음)
- 대기 중 이동 명령은 플래그로 보존 → 공격 완료 후 자동 전환
- 이전 retry 로직(ResetMovementTracking 등)은 복원하지 않음

---

## 수정 8: 공격 애니메이션 중복 재시작 방지 (Bug 1 수정)

**발견 경위:** 수정 7(OnUnitEnteredCombat) 적용 후 공격 애니메이션이 끝나기 전에 재시작되는 현상 간헐적 발생.

**원인:**
`TriggerAttackAnimation()`에서 `_attackCoroutine != null`일 때 `StopCoroutine` 후 새 코루틴을 시작.
수정 7의 0.1f bypass가 쿨다운 잔여 0.05~0.1초 구간에서 새 공격을 조기 발동시켜 이 경로를 타게 됨.

**해결:**
`_attackCoroutine != null`이면 완전 무시(return). `_isWalkPending`도 함께 초기화하여 Walk가 시작되지 않도록 함.
Attack Loop 애니메이션이 재생 중이므로 시각적 공백 없음. 다음 TickCombat 사이클이 새 공격을 발동.

**Bug 2(타겟 사망 후 Walk→Attack) 동시 해결:**
- `StartWalkAnimationClientRpc` 도착 → `_isWalkPending = true`
- `TriggerAttackAnimationClientRpc` 도착 → `_attackCoroutine != null` → `_isWalkPending = false` → return
- 기존 애니메이션 종료 → `_isWalkPending = false` → Walk 시작 안 됨 → Attack Loop 유지 ✓

**설계 원칙:**
- 공격 중 새 타겟 전환(회전 변경 포함)은 현재 스윙이 끝난 뒤 반영 → 시각/데미지 일관성 유지
- 스나이퍼 같은 긴 애니메이션 유닛: A를 향해 샷 진행 중 A 사망 → A에 데미지, B는 다음 사이클부터

### UnitView.cs — TriggerAttackAnimation 변경

**변경 전:**
```csharp
if (_attackCoroutine != null)
    StopCoroutine(_attackCoroutine);

// 이전 공격 코루틴이 StopCoroutine으로 중단되면 ...
_isWalkPending = false;
float angle = CalculateAttackAngle(GetTargetWorldPos(targetId, targetIsUnit));
_attackCoroutine = StartCoroutine(PlayAttackAnimation(angle));
```

**변경 후:**
```csharp
// 공격 애니메이션 재생 중이면 완전 무시 — 재시작 없음.
// _isWalkPending도 초기화하여 애니메이션 종료 후 Walk가 시작되지 않도록 함.
if (_attackCoroutine != null)
{
    _isWalkPending = false;
    return;
}

_isWalkPending = false;
float angle = CalculateAttackAngle(GetTargetWorldPos(targetId, targetIsUnit));
_attackCoroutine = StartCoroutine(PlayAttackAnimation(angle));
```

---

## 위험 요소 및 대응

| 위험 | 대응 |
|------|------|
| 서버 즉시 스냅으로 인한 회전 끊김 | NetworkTransform 0.1초 보간이 클라이언트에서 부드럽게 처리. Position과 동일 |
| Red 클라이언트 rotation +180° 보정 타이밍 | LateUpdate에서 매 프레임 적용 — NetworkTransform이 먼저 rotation 설정 → LateUpdate에서 보정 |
| _isWalkPending 제거 후 Walk 깜빡임 | _isWalkPending 복원으로 해결. 수정 6 참조 |
| MoveAlongPath 첫 스텝 대기 제거로 이동 즉시 시작 | 서버에서 즉시 스냅 후 이동 시작 → 클라이언트는 NetworkTransform이 회전+이동을 동시에 보간 |
| 공격 중 새 타겟 RPC 무시로 인한 시각 지연 | Attack Loop 재생 중이므로 공백 없음. 다음 TickCombat 사이클이 새 공격 발동. 수정 8 참조 |
| 연속 공격 사이클에서 CrossFade Attack→Attack 끊김 | 수정 9: 이미 Attack 상태면 CrossFade 생략, 현재 루프 남은 시간만큼 대기 |

---

## 수정 9: 연속 공격 사이클 CrossFade 끊김 제거

**발견 경위:** 수정 8 적용 후에도 연속 공격 시 살짝 재시작되는 느낌 잔존.

**원인:**
`_attackCoroutine = null` 직후 TickCombat이 새 공격 RPC를 발동하면,
`TriggerAttackAnimation()`에서 `_attackCoroutine == null` → 새 코루틴 시작
→ `CrossFadeInFixedTime(StateAttack, blend)` 호출 → Attack Loop 클립이 처음부터 재시작 → 끊김.

이것은 버그가 아니라 연속 공격 사이클의 정상 동작이지만, CrossFade가 클립을 리셋하기 때문에 시각적 끊김이 발생.

**해결:**
`PlayAttackAnimation` 진입 시 이미 Attack 상태이면 CrossFade 생략.
현재 루프의 남은 시간(`normalizedTime % 1f` 기반)만큼만 대기하여 자연스럽게 루프 이어짐.

### UnitView.cs — PlayAttackAnimation 변경

**변경 전:**
```csharp
_animator.speed = 1f;
_animator.CrossFadeInFixedTime(StateAttack, _toAttackBlend, 0);
yield return new WaitForSeconds(_toAttackBlend);
yield return null;
float clipLen = _animator.GetCurrentAnimatorStateInfo(0).length;
if (clipLen <= 0f) clipLen = 0.5f;
float remaining = Mathf.Max(0f, clipLen - _toAttackBlend);
yield return new WaitForSeconds(remaining);
```

**변경 후:**
```csharp
_animator.speed = 1f;

// 이미 Attack 상태에서 루프 중이면 CrossFade 생략 — 클립 처음부터 재시작하지 않음.
// CrossFadeInFixedTime(Attack→Attack)은 루프 중인 클립을 리셋하므로 끊김이 발생.
// 현재 루프의 남은 시간만큼만 대기하면 자연스럽게 다음 사이클로 이어짐.
bool isAlreadyAttacking = _animator.GetCurrentAnimatorStateInfo(0).IsName(StateAttack)
                          && !_animator.IsInTransition(0);
if (isAlreadyAttacking)
{
    // 현재 루프의 경과 시간 계산 → 남은 시간 대기
    var info = _animator.GetCurrentAnimatorStateInfo(0);
    float clipLen = info.length > 0f ? info.length : 0.5f;
    float elapsed = (info.normalizedTime % 1f) * clipLen;
    float remaining = Mathf.Max(0f, clipLen - elapsed);
    yield return new WaitForSeconds(remaining);
}
else
{
    _animator.CrossFadeInFixedTime(StateAttack, _toAttackBlend, 0);
    yield return new WaitForSeconds(_toAttackBlend);
    yield return null;
    float clipLen = _animator.GetCurrentAnimatorStateInfo(0).length;
    if (clipLen <= 0f) clipLen = 0.5f;
    float remaining = Mathf.Max(0f, clipLen - _toAttackBlend);
    yield return new WaitForSeconds(remaining);
}
```

---

## ⚠️ 상태: 새 설계로 대체됨 (2026-03-30)

수정 1~8은 적용 완료. 수정 9는 공격 멈춤 버그로 롤백.

연속 공격 사이클에서 CrossFade 끊김 문제를 패치로 해결하려다 근본적인 설계 문제를 발견.
서버가 매 공격 사이클마다 애니메이션 신호를 보내는 구조 자체가 문제였음.

→ 새 Task: `Assets/_Project/Docs/_Tasks/2026-03-30/[시간]_combat-animation-redesign/`
에서 서버/클라이언트 역할 분리 재설계로 대체 진행.

---

## Editor 작업 (사용자 실행 필요)

구현 완료 후:
1. `Hexiege > Add NetworkTransform To Unit Prefabs` 실행 (SyncRotAngleY ON 반영)
2. 멀티플레이 테스트 (Host=Blue, Client=Red)
