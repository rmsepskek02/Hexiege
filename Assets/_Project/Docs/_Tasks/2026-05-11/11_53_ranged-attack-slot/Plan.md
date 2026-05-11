# Plan — 원거리 유닛 전투 지점 겹침 방지

작성일: 2026-05-11  
Research: `_Tasks/2026-05-11/11_53_ranged-attack-slot/Research.md`  
규칙 근거: `Docs/GameSystemRules.md` 규칙 13, 18

---

## 이 작업이 무엇인지

원거리 유닛이 전투 중 멈출 때 공격 슬롯을 클레임하도록 추가하여, 후속 유닛이 다른 방향 슬롯으로 자연스럽게 분산되도록 한다.

근접 유닛은 이미 AttackPositionManager의 공격 슬롯으로 분산 처리된다. 원거리 유닛도 동일한 슬롯 시스템을 사용하되, **슬롯 위치로 이동하지 않고 현재 자리에서 멈춘 채 공격**한다는 점이 다르다.

---

## 변경 파일

`Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` 한 파일만 수정.

---

## 수정 1 — Lerp 중 전투 진입 시 순간이동 제거

**위치**: `MoveAlongPathV3` 내 원거리 전투 감지 분기 (현재 라인 ~1008~1025)

**변경 전:**
```csharp
// 목적지 타일 슬롯 위치로 강제 스냅 (규칙 13 오해)
transform.position = toPos;
elapsed = targetDuration;
```

**변경 후:**
```csharp
// 현재 물리 위치에서 즉시 멈춤 — 스냅 없음.
// Lerp 루프를 강제 탈출하기 위해 elapsed를 최대값으로 설정.
elapsed = targetDuration;
```

`transform.position` 강제 대입 줄만 제거. elapsed 조작은 그대로 유지(Lerp 루프 탈출용).

추가로 목적지 타일에 클레임한 이동 슬롯은 더 이상 도착하지 않으므로 해제한다:
```csharp
ReleaseV2MoveSlotIfClaimed();
```

---

## 수정 2 — 원거리 전투 진입 시 공격 슬롯 클레임

**위치**: `EnterStationaryCombatV3` 메서드 (현재 라인 ~1365)

**변경 전:**
```csharp
private IEnumerator EnterStationaryCombatV3()
{
    if (!_combatUseCase.HasEnemyInRange(_unitData)) yield break;
    yield return EnterCombatLoopV3();
}
```

**변경 후:**
```csharp
private IEnumerator EnterStationaryCombatV3()
{
    if (!_combatUseCase.HasEnemyInRange(_unitData)) yield break;

    // [규칙 18] 원거리도 공격 슬롯을 클레임하여 후속 유닛이 다른 방향으로 분산.
    // 단, 근접 유닛과 달리 슬롯 위치로 이동하지 않고 현재 자리에서 그대로 공격한다.
    HexCoord? targetCoord = _combatUseCase.FindNearestEnemyPositionInDetectRange(_unitData);
    if (targetCoord.HasValue && _attackPositionManager != null)
    {
        Vector3 unitDomainPos = ViewConverter.FromView(transform.position);
        Vector3 targetDomainPos = HexMetrics.HexToWorld(targetCoord.Value);
        Vector3 approachVec = targetDomainPos - unitDomainPos;
        float contactRadius = /* 타겟이 유닛이면 */ 0.3f, /* 건물이면 */ 0.5f;

        _attackPositionManager.ClaimByApproach(
            targetCoord.Value, _unitData.Id, unitDomainPos, approachVec, contactRadius);
        _v2AttackSlotTargetCoord = targetCoord;
    }

    yield return EnterCombatLoopV3();

    // 전투 종료 후 공격 슬롯 해제.
    ReleaseV2AttackSlotIfClaimed();
}
```

contactRadius 결정은 타겟이 유닛인지 건물인지 판별해야 하므로, `FindNearestEnemyInDetectRange` 결과의 `isUnit` 필드를 사용한다.

---

## 수정 3 — 전투 종료 후 toPos 스냅 제거 (2026-05-11 추가)

**발견 시점**: 사용자 플레이 테스트 중 확인  
**규칙 근거**: GameSystemRules.md 규칙 15 — "현재 물리 위치 기준으로 A*를 재개한다"

**현상**: 원거리 전투 종료 시 유닛이 목적지 타일 위치(toPos)로 순간이동.

**원인**: Lerp while 루프 탈출 후 `transform.position = toPos` 가 전투 중단 케이스에서도 실행됨.

**수정 내용**: `MoveAlongPathV3` 내 Lerp while 루프 진입 전에 `interruptedByCombat` 플래그 추가.

```
Lerp while 루프 전: bool interruptedByCombat = false;

전투 진입 시: interruptedByCombat = true; (continue 제거, 자연스럽게 while 루프 탈출)

Lerp while 루프 종료 후:
  if (interruptedByCombat)
      needRepath = true;
      break;  // for 탈출 → 외부 while에서 현재 _unitData.Position 기준 A* 재시작
  else
      transform.position = toPos;  // 정상 도착 시에만 스냅
      ProcessStep(...);
      (기존 TILE_ARRIVED 흐름)
```

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| 전투 종료 후 슬롯 미해제 시 누수 | `EnterStationaryCombatV3` 마지막에 `ReleaseV2AttackSlotIfClaimed()` 추가 |
| 유닛 사망 시 슬롯 미해제 | 기존 `OnEntityDied` 핸들러가 이미 `ReleaseV2AttackSlotIfClaimed()` 호출 — 추가 처리 불필요 |
| `StopMovement` 시 슬롯 미해제 | 기존 `StopMovement`가 이미 `ReleaseV2AttackSlotIfClaimed()` 호출 — 추가 처리 불필요 |
| 타겟 isUnit 판별 | `FindNearestEnemyInDetectRange`의 `isUnit` 필드로 판별 |
| A* 재시작 시 `_unitData.Position`이 이전 타일 | 정상 동작 — 마지막으로 ProcessStep이 완료된 타일에서 재경로 탐색 |

---

## 구현 순서

1. `MoveAlongPathV3` — `transform.position = toPos` 제거 + `ReleaseV2MoveSlotIfClaimed()` 추가 ✅
2. `EnterStationaryCombatV3` — 공격 슬롯 클레임 + 전투 종료 후 해제 ✅
3. `MoveAlongPathV3` — `interruptedByCombat` 플래그 추가 + 전투 후 toPos 스냅 제거
