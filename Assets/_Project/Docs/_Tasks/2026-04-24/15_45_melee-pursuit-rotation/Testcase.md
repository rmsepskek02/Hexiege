# Testcase — 근접유닛 추적 중 회전 개선

## TC 목록

---

### SINGLE-001: 근접유닛이 적 감지 후 추적 시 타겟 방향으로 서서히 회전한다

**전제:** 싱글플레이, 근접유닛(FlameSpirit, EmberSpirit, BearGuard, LionKnight 중 하나)이 경로를 따라 이동 중이다.

**동작:**
1. 근접유닛이 경로를 따라 이동하는 것을 확인한다.
2. 이동 경로 중간에 적 유닛이 감지 사거리(인접 1타일) 이내로 진입한다.
3. 근접유닛이 타일 이동을 멈추고 적을 향해 직선 추적을 시작하는 것을 관찰한다.

**기댓값:**
- 적을 감지하는 순간 유닛이 이전 이동 방향 그대로 고정된 채 이동하지 않는다.
- 추적이 시작되면 유닛이 적이 있는 방향으로 몸을 서서히 돌리면서 이동한다.
- 회전이 급격하게 튀지 않고 부드럽게 전환된다.

**결과:** PASS (2026-04-24 사용자 실기 확인)

---

### SINGLE-002: 근접유닛이 공격 사거리에 진입하면 회전 없이 전투가 시작된다

**전제:** 싱글플레이, 근접유닛이 적을 감지하고 직선 추적 중이다.

**동작:**
1. 근접유닛이 적을 향해 추적하며 회전하는 것을 확인한다.
2. 근접유닛이 공격 사거리에 진입하는 시점을 확인한다.
3. 전투 애니메이션 및 공격이 시작되는 것을 확인한다.

**기댓값:**
- 공격 사거리 진입 즉시 전투 루프로 진입한다.
- 전투 중에도 타겟을 향한 회전 추적이 정상 동작한다.
- 기존 전투 동작(공격 애니메이션, 데미지)에 이상이 없다.

**결과:** PASS (2026-04-24 사용자 실기 확인)

---

### SINGLE-003: 원거리유닛은 회전 개선 영향을 받지 않는다

**전제:** 싱글플레이, 원거리유닛(Pistoleer, Sniper 등)이 이동 중이다.

**동작:**
1. 원거리유닛이 적 방향으로 이동하는 것을 확인한다.
2. 원거리유닛이 공격 사거리에 진입하는 시점을 관찰한다.

**기댓값:**
- 원거리유닛은 Phase 1(월드 좌표 직선 추적)을 사용하지 않으므로 기존 동작과 동일하다.
- 원거리유닛의 이동·공격·회전에 이상이 없다.

**결과:** PASS (정적 분석 확인)

---

## QA 섹션 (qa-tester 에이전트 전용)

### 정적 분석

**변경 위치:** `Presentation/Unit/UnitView.cs` 850~866 라인 (Phase 1 직선 이동 블록)

**변경 내용:**
- `if (dist > 0.01f)` 단일문 블록 → 중괄호 블록으로 확장
- 이동 전 `CalculateAttackAngle(enemyViewPos)` + `Quaternion.RotateTowards` 회전 로직 추가

**점검 항목:**

| # | 항목 | 분석 결과 | 판정 |
|---|------|----------|------|
| 1 | `CalculateAttackAngle(enemyViewPos)` 호출 — `enemyViewPos`는 `Vector3`, 메서드 파라미터 `targetWorldPos`도 `Vector3`. 타입 일치 | 정상 호출 | PASS |
| 2 | `CombatRotationSpeed` — `private const float`, 145 라인에 정의. 동일 클래스 내 접근 | 접근 정상 | PASS |
| 3 | `Quaternion.RotateTowards` 세 번째 파라미터 `CombatRotationSpeed * Time.deltaTime` — `maxDegreesDelta`(초당 각도 × delta) 의미로 올바른 사용 | 사용법 정상 | PASS |
| 4 | 멀티플레이 가드 — `MoveAlongPath` 코루틴 시작 전 `StartMove()` 메서드(438 라인)에서 `if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;` 가드 존재. 클라이언트는 코루틴 자체를 실행하지 않으므로 Phase 1 회전 코드도 서버에서만 실행됨. NetworkTransform이 클라이언트에 보간 전달 | 가드 정상 | PASS |
| 5 | `dist > 0.01f` 조건 내부 — 도달 직전(0.01f 이하) 회전·이동 모두 생략. 의도적 설계 | 로직 이상 없음 | PASS |
| 6 | 이동 코드 보존 — 회전 코드 이후 `transform.position += moveDir.normalized * worldSpeed * Time.deltaTime;` 그대로 존재 | 누락 없음 | PASS |
| 7 | `CalculateAttackAngle` 내부 예외 처리 — `dir.sqrMagnitude < 0.001f`이면 현재 `eulerAngles.y` 반환(회전 유지). `moveDir.y = 0f` 후 수평 거리 0.01f 초과 조건 내부이므로 `sqrMagnitude > 0.0001f`가 보장됨. 0.001f 기준을 하회하는 케이스(수평 이동량이 극소)는 이론상 가능하나 현재 각도를 유지하므로 시각적으로 무해함 | 허용 가능 | PASS |

**컴파일 오류 여부:** 없음. 신규 참조 없음, 기존 private 멤버 사용, 타입 불일치 없음.

---

### TC 판정

| TC | 제목 | 정적 분석 판정 | 비고 |
|----|------|--------------|------|
| SINGLE-001 | 근접유닛 추적 중 서서히 회전 | CONDITIONAL PASS | 정적 분석 이상 없음. 실기 확인 필요 (부드러운 회전 체감은 실기에서만 검증 가능) |
| SINGLE-002 | 공격 사거리 진입 후 전투 시작 | CONDITIONAL PASS | 회전 코드가 Phase 1 내부에만 존재하고 전투 루프는 별도 경로 — 기존 전투 흐름에 영향 없음. 실기 확인 필요 |
| SINGLE-003 | 원거리유닛 영향 없음 | PASS | 원거리유닛은 Phase 1(`MoveAlongPath` 직선 추적 블록) 진입 조건(`HasEnemyInDetectRange`이지만 DetectRange = AttackRange이므로 즉시 전투로 전환)을 만족하지 않음. 회전 코드 실행 경로에 진입 불가. 코드 변경 영향 없음 확인 |

---

### 멀티플레이 TC 안내

MULTI- 계열 TC(에디터 Host + 빌드 Client 구성 필요)는 에이전트 실기 불가 — 사용자 확인 필요.

정적 분석 기준 멀티플레이 위험 요소:
- `StartMove()` 가드(438 라인)가 클라이언트의 코루틴 진입 자체를 차단하므로, Phase 1 회전 코드는 서버에서만 실행됨. NetworkTransform이 rotation을 클라이언트에 보간 전달하는 기존 구조와 동일한 패턴.
- 추가 RPC 없음, 추가 NetworkVariable 없음 — 기존 네트워크 동기화 흐름 변경 없음.
- 실기에서는 클라이언트 화면에서 회전이 NetworkTransform 보간으로 부드럽게 재생되는지 확인 권장.
