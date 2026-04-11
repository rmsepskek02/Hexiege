# Testcase: 공격 중 타겟 방향 지속 추적

**날짜**: 2026-04-11

---

## 사전 준비

- 씬: Game.unity
- 플레이 모드: **멀티플레이 (Host + Client, 에디터 + 빌드 또는 에디터 두 인스턴스)**
- 싱글플레이 모드는 상대방 유닛 자동 생산 로직 미구현으로 테스트 불가

---

## TC 목록

### TC-1: 원거리 유닛이 공격 중 이동하는 타겟을 계속 바라본다

**전제:** 멀티플레이. Human 종족으로 게임 시작. Pistoleer 또는 Sniper 생산 완료. 상대방 근접 유닛이 이동 중이다.

**동작:**
1. 원거리 유닛을 적 유닛 방향으로 이동시켜 전투를 시작한다.
2. 공격 애니메이션이 재생되는 동안 적 근접 유닛이 계속 이동하는 상황을 관찰한다.

**기댓값:**
- 원거리 유닛이 공격 애니메이션 재생 중에도 타겟의 이동 방향에 맞춰 몸을 회전한다.
- 타겟이 옆으로 이동해도 원거리 유닛이 그 방향을 향해 공격한다.

**결과:** PASS

---

### TC-2: 타겟이 사망하면 회전 추적이 중단된다

**전제:** 멀티플레이. 원거리 유닛이 공격 중인 상태. 사거리 내 다른 적 유닛 없음.

**동작:**
1. 원거리 유닛이 타겟을 공격하여 타겟이 사망하는 상황을 기다린다.
2. 타겟 사망 직후 원거리 유닛의 상태를 관찰한다.

**기댓값:**
- 타겟이 사망한 이후 원거리 유닛이 엉뚱한 방향을 향해 계속 회전하지 않는다.
- 이동 재개 시 이동 방향에 맞게 정상 회전한다.

**결과:** PASS

---

### TC-3: 이동 중 원거리 유닛의 회전이 영향받지 않는다 (회귀)

**전제:** 멀티플레이. Human 종족으로 게임 시작. Pistoleer 또는 Sniper 생산 완료.

**동작:**
1. 원거리 유닛을 특정 방향으로 이동시킨다.
2. 전투 없이 이동만 하는 상황을 관찰한다.

**기댓값:**
- 이동 중 유닛이 이동 방향에 맞게 정상적으로 회전한다.
- 이번 수정으로 이동 중 회전 동작이 달라지지 않는다.

**결과:** PASS

---

### TC-5: 멀티플레이 — B 사망 시 C가 이미 사거리 내에 있을 때 회전이 C 방향으로 전환된다

**전제:** 멀티플레이(Host + Client). 원거리 유닛 A가 근접 유닛 B를 공격 중. 다른 근접 유닛 C가 이미 A의 공격 사거리 내에 존재한다.

**동작:**
1. A가 B를 공격하며 B 방향을 추적하는 것을 확인한다.
2. B가 사망하는 순간 A의 회전 방향을 관찰한다.
3. B 사망 직후 A가 C를 새 타겟으로 공격하는지 확인한다.

**기댓값:**
- B 사망 후 A의 회전이 B의 마지막 위치에 영구 고정되지 않는다.
- A가 C 방향으로 회전하며 C를 향해 공격한다.
- 회전 전환이 1~2프레임 내에 이루어진다 (다음 공격 시작 전에 완료).

**결과:** PASS

---

### TC-4: 근접 유닛의 회전 동작이 변경되지 않는다 (회귀)

**전제:** 멀티플레이. Spirit 또는 Transcendence 종족으로 게임 시작. 근접 유닛 생산 완료.

**동작:**
1. 근접 유닛을 적 방향으로 이동시켜 전투를 시작한다.
2. 공격 중 동작을 관찰한다.

**기댓값:**
- 근접 유닛의 공격 동작이 이전과 동일하다.
- 이번 수정으로 근접 유닛 동작이 달라지지 않는다.

**결과:** PASS

---

### TC-6: 공격 중 새 유닛이 사거리 내로 진입해도 현재 타겟이 살아있으면 교체되지 않는다

**전제:** 멀티플레이. 원거리 유닛 A가 근접 유닛 B를 공격 중. B는 사거리 내에 살아있다.

**동작:**
1. A가 B를 공격하는 것을 확인한다.
2. 다른 근접 유닛 C가 A의 공격 사거리 안으로 진입하는 상황을 만든다 (C가 B보다 가까운 위치).
3. A의 타겟이 변경되는지 관찰한다.

**기댓값:**
- B가 살아있는 동안 A의 타겟이 C로 교체되지 않는다.
- A가 계속 B를 공격한다.

**결과:** PASS

---

### TC-7: 현재 타겟이 사망하면 사거리 내 다른 적으로 교체된다 (회귀)

**전제:** 멀티플레이. 원거리 유닛 A가 B를 공격 중. C가 사거리 내에 존재한다.

**동작:**
1. A가 B를 공격하여 B를 사망시킨다.
2. B 사망 직후 A의 타겟 변경 여부를 관찰한다.

**기댓값:**
- B 사망 후 A가 C를 새 타겟으로 공격하기 시작한다.
- TC-6 수정으로 인해 타겟 교체 자체가 막히지 않는다.

**결과:** PASS

---

## TC 결과 요약

| TC ID | 제목 | 결과 |
|-------|------|------|
| MULTI-001 | 공격 중 이동 타겟 방향 추적 | PASS |
| MULTI-002 | 타겟 사망 후 회전 추적 중단 | PASS |
| MULTI-003 | 이동 중 회전 동작 회귀 | PASS |
| MULTI-004 | 근접 유닛 회귀 | PASS |
| MULTI-005 | B 사망 시 C가 사거리 내에 있을 때 회전 전환 | PASS |
| MULTI-006 | 현재 타겟 생존 중 새 유닛 진입 시 타겟 유지 | PASS |
| MULTI-007 | 현재 타겟 사망 시 사거리 내 다른 적으로 교체 (회귀) | PASS |

---

## 정적 분석 결과 (qa-tester)

**변경 파일**: `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

**분석 일자**: 2026-04-11

---

### 검증 항목별 판정

#### 항목 1. Update()의 null 가드 — PASS

**근거**: `UnitView.cs` 164~176행. `Update()` 첫 번째 줄에서 `if (_combatTargetTransform == null) return;`으로 즉시 리턴한다. Unity는 파괴된 GameObject의 Transform을 자동으로 null로 처리하므로, 타겟이 사망하여 Destroy 되는 순간 다음 프레임부터 이 가드가 걸린다. null 역참조 위험 없음.

#### 항목 2. 멀티플레이 클라이언트 가드 — PASS

**근거**: `UnitView.cs` 170~171행. null 가드 통과 후 `if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;` 조건으로 클라이언트를 차단한다. 기존 `MoveTo()` 393행의 동일 패턴과 일치하며, 클라이언트에서 rotation을 직접 쓰면 NetworkTransform과 충돌한다는 주석 근거도 명확하다.

#### 항목 3. StopCombatAnimation의 null 처리 — PASS

**근거**: `UnitView.cs` 762~768행. `StopCombatAnimation()` 첫 줄에서 `_combatTargetTransform = null;`을 실행하고, 이후 메서드 바디는 의도적으로 비워두었다. 다음 프레임 Update()에서 즉시 리턴 처리된다.

#### 항목 4. ChangeTarget의 Transform 교체 — PASS

**근거**: `UnitView.cs` 733~743행. `ChangeTarget()` 내에서 1회성 스냅 회전 후 `_combatTargetTransform = GetTargetTransform(targetId, targetIsUnit);`으로 이전 타겟 참조를 새 타겟 참조로 즉시 덮어쓴다. 이전 타겟을 참조하는 코드 경로가 없으므로 교체 즉시 올바른 추적이 시작된다.

#### 항목 5. Walk 상태에서의 간섭 없음 — PASS

**근거**: `StopCombatAnimation()` 호출 시 `_combatTargetTransform = null`이 설정된다. Walk 전환은 이후 `StartWalkAnimation()` 또는 `MoveAlongPath()` 내부에서 일어나며, 이 시점에는 이미 `_combatTargetTransform == null`이므로 Update()는 즉시 리턴한다. Walk 중 회전 추적이 개입하는 경로가 존재하지 않는다.

#### 항목 6. 컴파일 오류 가능성 — PASS

**근거**:
- `_combatTargetTransform` 필드: `private Transform _combatTargetTransform = null;` (133행) — 정상 선언.
- `GetTargetTransform()`: 341~347행에 정의. `_unitFactory`, `_buildingFactory` null 조건부 연산자(`?.`) 사용으로 null 안전.
- `CalculateAttackAngle()`: 312~319행. `Vector3` 인자로 `_combatTargetTransform.position`을 넘기는 형태(175행)와 시그니처 일치.
- `NetworkContext.IsNetworkActive`, `NetworkContext.IsNetworkServer`: 기존 `MoveTo()` 393행에서 동일 패턴 사용 중 — 이미 컴파일 통과 확인된 참조.
- 신규 `using` 추가 없음 — `Transform`은 `UnityEngine` 네임스페이스에 포함, 이미 44행에서 `using UnityEngine;` 선언됨.
- 미정의 변수, 잘못된 메서드 호출 없음.

---

### TC별 정적 분석 판정 요약

| TC ID | 제목 | 정적 분석 판정 | 비고 |
|-------|------|--------------|------|
| SINGLE-001 | 공격 중 이동 타겟 방향 추적 | CONDITIONAL PASS | Update() 추적 로직 코드 정상. 실기 확인 필요 |
| SINGLE-002 | 타겟 사망 후 회전 추적 중단 | CONDITIONAL PASS | StopCombatAnimation null 처리 및 Unity Destroy 자동 null 확인. 실기 확인 필요 |
| SINGLE-003 | 이동 중 회전 동작 회귀 | PASS | Walk 상태에서 _combatTargetTransform == null 보장됨. 코드 경로 간섭 없음 |
| SINGLE-004 | 근접 유닛 회귀 | PASS | 신규 로직은 _combatTargetTransform이 null일 때 즉시 리턴. 근접 유닛 흐름에 영향 없음 |

SINGLE-001, SINGLE-002는 Update() 실행 타이밍 및 Unity Destroy 처리 순서가 실제 프레임에서 의도대로 동작하는지 실기로 확인이 필요하다. 코드 구조상 오동작 경로는 발견되지 않았다.

멀티플레이 TC는 에이전트 실기 불가 — 사용자 확인 필요. 정적 분석 기준 클라이언트 가드 조건(항목 2)이 올바르게 작성되어 있으며, 서버 전용 rotation 갱신 패턴이 기존 코드와 일관된다.
