# Plan — 타겟 고정(Target Lock) 데미지 불일치 버그 수정

## 목표

멀티플레이에서 바라보는 유닛(애니메이션 타겟)과 데미지를 받는 유닛이 다른 버그 수정.
`IsCurrentTargetStillValid`가 true를 반환할 때 데미지도 기존 타겟에 적용되도록 수정.

---

## 수정 파일

| 파일 | 변경 내용 |
|------|-----------|
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs` | `TickCombat()` 내 `ExecuteAttack` 호출 시 사용하는 targetId 결정 로직 수정 |

---

## 수정 내용

### 대상 메서드: `TickCombat()` — NetworkCombatController.cs

#### 현재 코드 (버그 있음)

```csharp
if (_unitCombatTargets.TryGetValue(id, out var prev))
{
    if (prev.targetId != targetId)
    {
        if (!combat.IsCurrentTargetStillValid(unit, prev.targetId, prev.isUnit))
        {
            _unitCombatTargets[id] = (targetId, isUnit);
            ChangeTargetClientRpc(id, targetId, isUnit);
        }
        // IsCurrentTargetStillValid = true → _unitCombatTargets 미변경 (B 유지)
        // 그러나 아래 ExecuteAttack에서 여전히 새 C의 targetId 사용 ← 버그
    }
}
else
{
    _unitCombatTargets[id] = (targetId, isUnit);
}

ExecuteAttack(unit, targetId, targetIsUnit); // ← 항상 TryFindTarget 결과(C) 사용
```

#### 수정 후 코드

```csharp
// 실제 데미지를 적용할 타겟 — 기본값은 TryFindTarget이 반환한 가장 가까운 적
int damageTargetId = targetId;
bool damageTargetIsUnit = isUnit;

if (_unitCombatTargets.TryGetValue(id, out var prev))
{
    if (prev.targetId != targetId)
    {
        if (!combat.IsCurrentTargetStillValid(unit, prev.targetId, prev.isUnit))
        {
            // 기존 타겟이 유효하지 않음 → 새 타겟으로 교체
            _unitCombatTargets[id] = (targetId, isUnit);
            ChangeTargetClientRpc(id, targetId, isUnit);
        }
        else
        {
            // 기존 타겟이 아직 유효 → 애니메이션과 데미지 모두 기존 타겟 유지
            damageTargetId = prev.targetId;
            damageTargetIsUnit = prev.isUnit;
        }
    }
    // else: 같은 타겟 → RPC 없음, damageTargetId = targetId (변경 없음)
}
else
{
    _unitCombatTargets[id] = (targetId, isUnit);
}

// damageTargetId: 기존 타겟이 유효하면 기존 타겟, 아니면 새 타겟
ExecuteAttack(unit, damageTargetId, damageTargetIsUnit);
```

---

## 변경 요약

- `damageTargetId` / `damageTargetIsUnit` 지역변수를 도입하여 데미지 타겟을 별도로 추적
- `IsCurrentTargetStillValid`가 `true`를 반환할 때 `damageTargetId`를 기존 타겟(`prev.targetId`)으로 설정
- `ExecuteAttack` 호출 시 `targetId` 대신 `damageTargetId` 사용
- 애니메이션 RPC 로직(`ChangeTargetClientRpc`, `_unitCombatTargets` 업데이트)은 변경 없음

---

## 위험 요소

- 변경 범위가 `TickCombat()` 내부 지역 변수 추가 + `ExecuteAttack` 인자 변경으로 최소화됨
- `IsCurrentTargetStillValid` 로직은 기존과 동일하게 유지 — 사이드 이펙트 없음
- `OnUnitEnteredCombatHandler`에서 `ExecuteAttack`을 호출하는 경로는 이미 신규 타겟을 대상으로 하므로 수정 불필요

---

## 테스트 기준

- 유닛 A가 B를 공격 중 C가 더 가까이 접근 → A는 B를 바라보며 B에게 데미지
- B가 사망하거나 사거리를 벗어난 후에야 A가 C로 타겟 전환
