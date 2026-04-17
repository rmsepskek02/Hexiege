# Testcase — 타겟 고정(Target Lock) 데미지 불일치 버그 수정

## TC 목록

---

### MULTI-001: 기존 타겟 생존 중 더 가까운 적 접근 시 데미지 대상 확인

**전제:** 멀티플레이(Host + Client). 유닛 A가 유닛 B를 공격 중이며 B는 살아있음. B보다 A에게 더 가까운 유닛 C가 A의 공격 사거리 안으로 진입.

**동작:**
1. 유닛 A가 유닛 B를 공격하는 상태에서 HP 수치를 관찰
2. 유닛 C가 A의 사거리 안으로 이동하여 A보다 B보다 가까워짐
3. A의 공격 쿨다운이 만료되어 다음 공격이 실행됨

**기댓값:**
- A는 계속 B를 바라보며 B에게 데미지가 적용됨
- C의 HP는 변하지 않음 (A에게 공격받지 않음)
- B가 사망한 이후에야 A가 C로 타겟을 전환함

**결과:** PASS — 사용자 멀티플레이 실기 확인 완료. A는 B를 바라보며 B에게 데미지 적용, C HP 변화 없음 확인.

---

### MULTI-002: 기존 타겟 사망 후 가장 가까운 적으로 타겟 전환

**전제:** 멀티플레이. 유닛 A가 유닛 B를 공격 중이며 B의 HP가 낮음. 유닛 C가 A의 사거리 안에 있고 B보다 가까운 위치.

**동작:**
1. A가 B를 공격하여 B가 사망함
2. B 사망 직후 A의 다음 공격 쿨다운이 만료됨

**기댓값:**
- B 사망 후 A는 C로 타겟을 전환하여 C를 바라봄
- C에게 데미지가 적용됨

**결과:** PASS — 사용자 멀티플레이 실기 확인 완료. B 사망 후 A가 C로 전환하여 C에게 데미지 적용 확인.

---

### MULTI-003: 기존 타겟이 사거리를 벗어난 후 새 타겟으로 전환

**전제:** 멀티플레이. 유닛 A가 유닛 B를 공격 중. B가 이동하여 A의 공격 사거리 밖으로 나감. 유닛 C가 A의 사거리 안에 있음.

**동작:**
1. B가 A의 사거리를 벗어남
2. A의 쿨다운이 만료됨

**기댓값:**
- A가 C로 타겟을 전환하여 C를 바라봄
- C에게 데미지가 적용됨

**결과:** PASS — 사용자 멀티플레이 실기 확인 완료. B 사거리 이탈 후 A가 C로 전환하여 C에게 데미지 적용 확인.

---

## QA 섹션 (qa-tester 에이전트 전용)

### 정적 분석 대상

**수정 파일**: `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
**수정 위치**: `TickCombat()` 메서드 253~297행

**핵심 검증 포인트**:

1. **MULTI-001 정적 분석**
   - `IsCurrentTargetStillValid(unit, prev.targetId, prev.isUnit)` 반환값이 `true`일 때
     `else` 분기에서 `damageTargetId = prev.targetId`로 교체되는지 확인 (281~283행)
   - `ExecuteAttack(unit, damageTargetId, damageTargetIsUnit)` 호출 시 기존 타겟 ID가 전달되는지 확인 (297행)
   - `_unitCombatTargets`와 `ChangeTargetClientRpc`는 호출되지 않아야 함 (애니메이션 유지)

2. **MULTI-002 정적 분석**
   - `IsCurrentTargetStillValid` 반환값이 `false`일 때 (B 사망 시 `FindTargetById`가 null 반환)
     `_unitCombatTargets[id] = (targetId, isUnit)` 교체 + `ChangeTargetClientRpc` 전송되는지 확인 (275~276행)
   - `damageTargetId`는 새 타겟 C의 ID 유지 (초기값 덮어쓰기 없음)

3. **MULTI-003 정적 분석**
   - `IsCurrentTargetStillValid` 내부 `IsTargetInRange`에서 B가 사거리 밖이면 `false` 반환
     → MULTI-002와 동일 분기 진행되는지 확인

4. **쿨다운 중 분기 영향 없음 확인** (299~323행)
   - `attackResult.HasValue`가 `false`인 경우(쿨다운 중) 코드는 수정되지 않음
   - `FindNearestEnemy` + `IsCurrentTargetStillValid` 가드 로직 그대로 유지

5. **`OnUnitEnteredCombatHandler` 영향 없음 확인** (396~444행)
   - 이 메서드는 `ExecuteAttack(unit, targetId, targetIsUnit)` 호출 — 신규 타겟 대상이므로 수정 불필요
   - 해당 메서드는 이번 수정에서 건드리지 않았음

---

## 정적 분석 결과 (qa-tester)

**분석 일자**: 2026-04-17
**분석 대상**: `NetworkCombatController.cs` `TickCombat()` 253~297행 수정분
**참조 파일**: `UnitCombatUseCase.cs` 396~488행

### 검증 포인트별 판정

| 번호 | 검증 포인트 | 판정 | 근거 |
|------|------------|------|------|
| 1 | MULTI-001: else 분기에서 damageTargetId = prev.targetId 교체 | PASS | 278~283행 else 분기 진입 시 damageTargetId 덮어씌움 확인 |
| 2 | MULTI-001: ExecuteAttack에 기존 타겟 ID 전달 | PASS | 297행 ExecuteAttack(unit, damageTargetId, damageTargetIsUnit) 호출 확인 |
| 3 | MULTI-001: ChangeTargetClientRpc 미호출 | PASS | else 분기(278~284행) 내 RPC 호출 없음 확인 |
| 4 | MULTI-002: IsCurrentTargetStillValid = false 시 새 타겟 C로 교체 | PASS | 274~276행 _unitCombatTargets[id] = (targetId, isUnit) + ChangeTargetClientRpc 전송 확인 |
| 5 | MULTI-002: damageTargetId 초기값 C 유지 | PASS | IsCurrentTargetStillValid = false 시 else 분기 미진입 → damageTargetId = targetId(C) 그대로 유지 |
| 6 | MULTI-003: IsTargetInRange = false → MULTI-002와 동일 분기 진입 | PASS | IsTargetInRange(443~488행)에서 사거리 밖이면 false 반환 → IsCurrentTargetStillValid = false → 동일 분기 확인 |
| 7 | 쿨다운 중 분기(299~323행) 수정 영향 없음 | PASS | attackResult.HasValue = false 구간은 이번 수정에 포함되지 않음 확인 |
| 8 | OnUnitEnteredCombatHandler 수정 영향 없음 | PASS | 해당 메서드는 이번 수정에서 건드리지 않았음 확인 |

### 종합 판정

**CONDITIONAL PASS** — 정적 분석 전 항목 이상 없음. MULTI- 접두사 TC 전체는 Host+Client 구성 필요로 에이전트 실기 불가. 사용자 실기 확인 필요.

**실기 확인 항목:**
- MULTI-001: 유닛 C가 A 사거리 진입 후 A의 공격이 B에 계속 적용되는지 (C HP 변화 없음)
- MULTI-002: B 사망 직후 A가 C로 전환하여 C에게 데미지 적용되는지
- MULTI-003: B가 사거리 이탈 후 A가 C로 전환하여 C에게 데미지 적용되는지
