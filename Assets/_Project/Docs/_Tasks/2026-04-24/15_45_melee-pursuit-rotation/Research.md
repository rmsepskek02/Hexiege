# Research — 근접유닛 추적 중 회전 개선

## 배경

20_26_melee-detect-range 작업의 후속 작업.
Phase 1(월드 좌표 직선 추적) 구현 완료 후, 추적 중 유닛 회전이 자연스럽지 않음을 확인.
적을 감지하기 전 타일 이동 방향의 회전값을 그대로 유지한 채 Phase 1 이동이 진행됨.

---

## 현재 회전 처리 구조

### Phase 0 — 타일 기반 A* 이동

**위치:** `UnitView.cs` 549~556 라인

```
타일 이동 방향 계산 (HexDirection)
→ ApplyDirection(dir) 호출
→ Quaternion.Euler로 즉시 스냅
```

타일에서 타일로 이동할 때마다 해당 이동 방향으로 **즉시 스냅(순간 전환)** 방식으로 회전.

---

### Phase 1 — 월드 좌표 직선 추적

**위치:** `UnitView.cs` 846~852 라인

```csharp
Vector3 moveDir = enemyViewPos - transform.position;
moveDir.y = 0f;
float dist = moveDir.magnitude;
if (dist > 0.01f)
    transform.position += moveDir.normalized * worldSpeed * Time.deltaTime;
```

`transform.position`은 매 프레임 적 방향으로 이동하지만, **`transform.rotation` 업데이트 코드가 전혀 없음.**

---

## 문제 원인

Phase 0 → Phase 1 전환 시점에 마지막 타일 이동 방향(즉시 스냅된 회전값)이 그대로 고정된다.
Phase 1에서 적을 향해 위치를 이동시키는 동안 회전은 전혀 변경되지 않아
유닛이 바라보는 방향과 실제 이동 방향이 일치하지 않는다.

---

## 기존 회전 관련 코드 자산

### CalculateAttackAngle() — `UnitView.cs` 357~364 라인

```csharp
private float CalculateAttackAngle(Vector3 targetWorldPos)
{
    Vector3 dir = targetWorldPos - transform.position;
    dir.y = 0f;
    if (dir.sqrMagnitude < 0.001f)
        return transform.eulerAngles.y;
    return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg - _meshYOffset;
}
```

타겟의 월드 위치 기준으로 Y축 회전 각도를 계산하는 메서드. Phase 1 회전 로직에 그대로 재사용 가능.

### CombatRotationSpeed 상수 — `UnitView.cs` 145 라인

```csharp
private const float CombatRotationSpeed = 270f;
```

전투 중 타겟 추적 회전에 사용하는 초당 각도 상수. Phase 1 이동 중 회전에도 동일하게 활용 가능.

### Update() 전투 중 회전 로직 — `UnitView.cs` 207~218 라인

```csharp
float angle = CalculateAttackAngle(_combatTargetTransform.position);
Quaternion targetRotation = Quaternion.Euler(0f, angle, 0f);
transform.rotation = Quaternion.RotateTowards(
    transform.rotation,
    targetRotation,
    CombatRotationSpeed * Time.deltaTime
);
```

`Quaternion.RotateTowards`를 사용하여 매 프레임 타겟 방향으로 서서히 회전. Phase 1에서 동일한 패턴 적용 예정.

---

## 변경 범위

| 파일 | 위치 | 내용 |
|------|------|------|
| `Presentation/Unit/UnitView.cs` | 846~852 라인 (Phase 1 직선 이동 블록) | 회전 로직 추가 |

다른 파일 변경 없음.

---

## 멀티플레이 고려사항

- Phase 1 코드는 서버 측 `MoveAlongPath` 코루틴에서만 실행 (클라이언트는 NetworkTransform 자동 동기화)
- `Update()`의 전투 중 회전 로직과 동일하게, 서버에서만 `transform.rotation` 갱신 → NetworkTransform이 클라이언트에 보간 전달
- Phase 1은 이미 서버만 실행하는 코드 블록 내부이므로 별도 서버 가드 불필요
