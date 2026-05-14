# Plan: 공격 중 타겟 방향 지속 추적

**날짜**: 2026-04-11

---

## 목표

공격 애니메이션 재생 중 타겟이 이동하면 유닛이 매 프레임 타겟 방향을 향하도록 수정.

---

## 핵심 원칙

- 타겟의 **Transform 참조**를 공격 시작 시 한 번 저장하고, `Update()`에서 그 참조의 `.position`을 읽어 회전 갱신
- 멀티플레이에서는 **서버에서만 rotation 쓰기** — 클라이언트는 NetworkTransform이 서버 rotation을 자동 동기화
  - 기존 코드(`StartCombatAnimation` 주석: "서버에서 즉시 스냅 → NetworkTransform이 클라이언트에 보간 전달")와 동일한 패턴

---

## 수정 파일

### `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

#### 1. 필드 추가

```
// 공격 중 타겟 Transform 추적용 필드
// 타겟 오브젝트 파괴 시 Unity가 자동으로 null 처리
private Transform _combatTargetTransform = null;

// [방어적 백업] Transform 참조가 null이 될 경우 팩토리에서 재조회하기 위한 ID 백업
// 멀티플레이 타이밍 문제(StopCombatAnimation / ChangeTarget 호출 순서 역전 등)로
// _combatTargetTransform이 null로 덮어써지는 경우에도 추적을 복구할 수 있도록 한다
private int _combatTargetId = -1;
private bool _combatTargetIsUnit = true;
```

#### 2. `Update()` 신규 추가

- `_combatTargetTransform != null`인 경우 Transform 참조를 직접 사용 (기존 방식)
- `_combatTargetTransform == null`이지만 `_combatTargetId != -1`인 경우 → 팩토리에서 Transform 재조회 시도
  - 재조회 성공: `_combatTargetTransform`에 저장 후 즉시 추적 재개
  - 재조회 실패(진짜 없는 타겟): 추적 중단 (ID도 -1로 초기화)
- 멀티플레이 클라이언트에서는 rotation 쓰기 건너뜀 (NetworkTransform 충돌 방지)
  - 조건: `NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer`
- `_combatTargetTransform.position` 직접 읽어 `CalculateAttackAngle()` 호출
- `transform.rotation = Quaternion.Euler(0f, angle, 0f)` 스냅

#### 3. `StartCombatAnimation()` 수정

- 기존 1회 스냅 로직 유지
- 추가: `_combatTargetTransform` 저장 + `_combatTargetId`, `_combatTargetIsUnit` 백업 저장

#### 4. `ChangeTarget()` 수정

- 기존 1회 스냅 로직 유지
- 추가: `_combatTargetTransform` 갱신 + `_combatTargetId`, `_combatTargetIsUnit` 백업 갱신

#### 5. `StopCombatAnimation()` 수정

- 기존 빈 메서드 유지 (Walk 전환 타이밍 충돌 방지)
- 추가: `_combatTargetTransform = null`, `_combatTargetId = -1` — 회전 추적 완전 중단

---

## 추가 버그 수정: 타겟 고착성 (2026-04-11 테스트 중 발견)

### 문제

현재 타겟이 살아있고 사거리 내에 있는데도, 새로운 유닛이 사거리 안으로 들어오면 타겟이 교체된다.

**원인**: `FindFirstEnemyTarget()`은 사거리 내 **가장 가까운** 적을 항상 반환한다. `TickCombat`은 이 결과를 현재 타겟과 비교하여 다르면 무조건 `ChangeTargetClientRpc`를 전송한다. 현재 타겟의 생존 여부와 사거리 내 위치 여부를 확인하지 않는다.

### 수정 파일 및 내용

#### `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`

- `IsTargetInRange()`가 이미 `private`으로 존재함
- 신규 `public` 메서드 `IsCurrentTargetStillValid(UnitData attacker, int targetId, bool targetIsUnit)` 추가
  - `FindTargetById()`로 타겟 도메인 객체 조회
  - 타겟이 null이거나 사망 → false
  - 타겟이 살아있으면 `IsTargetInRange()`로 사거리 확인 후 반환

#### `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`

`TickCombat` 내 타겟 교체 판단 로직 2곳 수정:

**① 쿨다운 만료 경로 (현재 타겟 != 새 타겟일 때)**
- 교체 전 `combat.IsCurrentTargetStillValid(unit, prev.targetId, prev.isUnit)` 확인
- 현재 타겟이 유효하면 → 교체 안 함
- 현재 타겟이 유효하지 않으면(사망 또는 사거리 이탈) → 교체 실행

**② 쿨다운 중 경로 (FindNearestEnemy 결과가 현재 타겟과 다를 때)**
- 동일하게 `IsCurrentTargetStillValid` 확인 후 교체 여부 결정

---

## 위험 요소

| 항목 | 내용 |
|------|------|
| 멀티플레이 클라이언트 | `Update()` 내 가드로 rotation 쓰기 건너뜀. 기존 클라이언트 자체 회전 로직 제거 원칙과 일치. |
| 타겟 소멸 | Unity가 파괴된 오브젝트의 Transform 참조를 자동 null 처리 → 백업 ID로 재조회 시도 → 팩토리에도 없으면 추적 중단. |
| 이동 중 간섭 | `StopCombatAnimation` 호출 시 Transform + ID 모두 초기화 → Walk 상태에서 `Update()` 미실행. |
| 성능 | Transform 참조 있을 때는 팩토리 조회 없음. null일 때만 1회 재조회 — 빈도 낮고 가벼운 연산. |
| 타이밍 역전 (멀티플레이) | StopCombatAnimation 이후 ChangeTarget 호출 순서가 역전되어도 ID 백업으로 다음 프레임에 자동 복구. |
| 타겟 고착 과잉 방지 | IsCurrentTargetStillValid는 사거리 이탈 시에도 false → 사거리를 벗어난 적에게 계속 고착되지 않음. |

---

## 검증 포인트

1. 원거리 유닛이 공격 중 타겟(이동하는 근접 유닛)을 계속 바라본다
2. 타겟이 사망하거나 전투가 끝나면 회전 추적이 중단된다
3. 이동 중(Walk 상태) 원거리 유닛의 회전이 달라지지 않는다
4. 멀티플레이에서 B 사망 → C로 타겟 전환 시 회전이 C 방향으로 정상 전환된다
5. 공격 중 새 유닛이 사거리 내로 진입해도 현재 타겟이 살아있으면 교체되지 않는다
6. 현재 타겟이 사망하거나 사거리를 벗어나면 가장 가까운 적으로 교체된다
