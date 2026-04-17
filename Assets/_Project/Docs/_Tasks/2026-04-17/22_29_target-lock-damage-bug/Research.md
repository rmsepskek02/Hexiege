# Research — 타겟 고정(Target Lock) 데미지 불일치 버그

## 요약

바라보는 유닛(타겟)이 아닌 가장 가까운 유닛이 대미지를 입는 버그.
애니메이션(회전 방향)과 실제 데미지 적용 대상이 일치하지 않는 현상.

---

## 관련 파일

| 파일 | 역할 |
|------|------|
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs` | 멀티플레이 전투 서버 처리, TickCombat, ExecuteAttack |
| `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` | 타겟 탐색(FindFirstEnemyTarget), 데미지 적용(ApplyAttackDamage), 타겟 유효성 확인(IsCurrentTargetStillValid) |

---

## 버그 위치

**파일:** `NetworkCombatController.cs`
**메서드:** `TickCombat()` — 라인 260~284

---

## 버그 상세 분석

### 타겟 고정(Target Lock) 설계 의도

`_unitCombatTargets` Dictionary로 유닛별 현재 공격 타겟을 추적한다.
현재 타겟이 살아있고 사거리 내에 있는 동안 더 가까운 적이 나타나도 타겟을 유지하는 것이 목적.
이를 통해 공격 집중도(한 타겟에 집중 공격)를 보장.

### 문제 코드 (TickCombat 260~284)

```
// TryFindTarget → 가장 가까운 적 반환 (=새 적 C)
var attackResult = combat.TryFindTarget(unit);
int targetId = attackResult.Value.id; // ← 가장 가까운 적 C의 ID

// 이전 타겟 B가 등록되어 있고 새 타겟 C와 다르면
if (prev.targetId != targetId)
{
    // 이전 타겟 B가 아직 유효하면 → _unitCombatTargets 유지 (애니메이션은 B 향함)
    if (!combat.IsCurrentTargetStillValid(unit, prev.targetId, prev.isUnit))
    {
        // 유효하지 않은 경우만 타겟 교체
        _unitCombatTargets[id] = (targetId, isUnit);
        ChangeTargetClientRpc(id, targetId, isUnit);
    }
    // IsCurrentTargetStillValid = true → _unitCombatTargets 미변경 (B 유지)
    // 그러나 아래 ExecuteAttack은 항상 새 가까운 적 C의 targetId를 사용!
}

// ← 버그: 항상 TryFindTarget이 반환한 targetId(C)로 데미지 적용
ExecuteAttack(unit, targetId, targetIsUnit);
```

### 버그 발생 시나리오

1. 유닛 A가 유닛 B를 공격 중 (`_unitCombatTargets[A] = B`)
2. 유닛 C가 A에게 B보다 더 가까이 접근
3. 쿨다운 만료 순간 `TryFindTarget(A)` → C 반환 (더 가까우므로)
4. `IsCurrentTargetStillValid(A, B)` → **true** (B는 살아있고 사거리 내)
5. `_unitCombatTargets[A]` = B 유지 → `ChangeTargetClientRpc` 미전송
6. 클라이언트: A는 B를 바라보는 애니메이션 유지 ✔
7. 서버: `ExecuteAttack(A, C의 ID, ...)` → **C에게 데미지 적용** ✘

### 핵심 원인

`IsCurrentTargetStillValid`가 `true`를 반환했을 때,
애니메이션 타겟(B, `_unitCombatTargets`에 기록)과
데미지 타겟(C, `targetId` 지역변수)이 분리되는 구조.

`ExecuteAttack`이 `_unitCombatTargets`를 참조하지 않고 항상 `TryFindTarget`의 결과를 사용하기 때문.

---

## 영향 범위

- **싱글플레이**: 버그 없음. `TryAttack()`은 Target Lock 개념 자체가 없어 가장 가까운 적으로 애니메이션과 데미지 모두 즉시 전환 — 불일치가 발생하지 않음. (싱글플레이 Target Lock 도입 여부는 추후 별도 작업으로 결정)
- **멀티플레이 서버**: `NetworkCombatController.TickCombat()` — 버그 발생. 현재 집중 대상.
- **멀티플레이 클라이언트**: 시각적(애니메이션)은 기존 타겟 B를 바라보는 상태 유지되나, 서버에서 C에게 데미지가 적용됨.

---

## 수정 방향 (개요)

`TickCombat` 내에서 `IsCurrentTargetStillValid`가 `true`를 반환한 경우(= 기존 타겟이 유효),
`ExecuteAttack` 호출 시 `targetId` 대신 `prev.targetId`와 `prev.isUnit`을 사용해야 한다.

기존 타겟이 유효하지 않은 경우(또는 새로 전투에 진입한 경우)에는 `targetId` 그대로 사용.
