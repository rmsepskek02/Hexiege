# Plan: 공격 중 타겟 방향 지속 추적

**날짜**: 2026-04-11

---

## 목표

공격 애니메이션 재생 중 타겟이 이동하면 유닛이 매 프레임 타겟 방향을 향하도록 수정.

---

## 수정 파일

### `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

#### 1. 필드 추가

```
// 공격 중 타겟 추적용 필드
private bool _inCombat = false;          // 현재 공격 중인지 여부
private int _combatTargetId = -1;        // 현재 타겟의 엔티티 Id
private bool _combatTargetIsUnit = true; // true=유닛, false=건물
```

#### 2. `Update()` 신규 추가

- `_inCombat == true`이고 `_unitData.IsAlive`인 경우에만 실행
- `GetTargetWorldPos(_combatTargetId, _combatTargetIsUnit)` 호출
- `CalculateAttackAngle(pos)` 호출
- `transform.rotation = Quaternion.Euler(0f, angle, 0f)` 스냅
- 기존 이동 중(Walk 중)에는 `_inCombat == false`이므로 `Update()`가 회전에 영향 없음

#### 3. `StartCombatAnimation()` 수정

- 기존 1회 스냅 로직 유지
- 추가: `_inCombat = true`, `_combatTargetId = targetId`, `_combatTargetIsUnit = targetIsUnit` 저장

#### 4. `ChangeTarget()` 수정

- 기존 1회 스냅 로직 유지
- 추가: `_combatTargetId = targetId`, `_combatTargetIsUnit = targetIsUnit` 갱신
- (`_inCombat`은 이미 true이므로 변경 불필요)

#### 5. `StopCombatAnimation()` 수정

- 기존 빈 메서드 유지 (Walk 전환 타이밍 충돌 방지)
- 추가: `_inCombat = false` — 회전 추적 중단

---

## 위험 요소

| 항목 | 내용 |
|------|------|
| 멀티플레이 | `Update()`는 서버/클라이언트 양쪽에서 실행. 클라이언트에서의 회전 변경은 NetworkTransform에 의해 즉시 서버 값으로 덮어씌워지므로 실질적 영향 없음. |
| 성능 | `GetTargetWorldPos` 내부는 `Dictionary` 조회 1회. 타겟 소멸 시 null 반환 후 forward 방향 fallback. 매 프레임 무거운 연산 없음. |
| 이동 중 간섭 | Walk 상태에서는 `_inCombat == false`이므로 `Update()` 회전 로직이 실행되지 않음. |

---

## 검증 포인트

1. 원거리 유닛이 공격 중 타겟(이동하는 근접 유닛)을 계속 바라본다
2. 타겟이 사망하거나 전투가 끝나면 회전 고정이 해제되고 Walk 방향으로 전환된다
3. 이동 중(Walk 상태) 원거리 유닛의 회전이 달라지지 않는다
4. 멀티플레이에서도 동일하게 동작한다
