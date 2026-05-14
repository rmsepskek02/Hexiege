# Plan — 근접유닛 추적 중 회전 개선

## 개요

Phase 1(월드 좌표 직선 추적) 이동 중 유닛이 타겟 방향을 바라보며 서서히 회전하도록 개선.
기존 전투 중 회전 로직(`CalculateAttackAngle` + `Quaternion.RotateTowards`)을 Phase 1 이동 블록에 동일하게 적용.

---

## 변경 파일

### `Presentation/Unit/UnitView.cs` — Phase 1 직선 이동 블록

**변경 위치:** 846~852 라인 (직선 이동 처리 코드)

**현재 코드:**
```csharp
// --------------------------------------------------------
// 직선 이동 (Y축 무시 — 수평 이동만)
// --------------------------------------------------------
Vector3 moveDir = enemyViewPos - transform.position;
moveDir.y = 0f;
float dist = moveDir.magnitude;
if (dist > 0.01f)
    transform.position += moveDir.normalized * worldSpeed * Time.deltaTime;
```

**변경 후 코드:**
```csharp
// --------------------------------------------------------
// 직선 이동 (Y축 무시 — 수평 이동만) + 타겟 방향 회전
// --------------------------------------------------------
Vector3 moveDir = enemyViewPos - transform.position;
moveDir.y = 0f;
float dist = moveDir.magnitude;
if (dist > 0.01f)
{
    // 이동 방향으로 서서히 회전 (CalculateAttackAngle은 enemyViewPos 기준 각도 계산)
    float pursuitAngle = CalculateAttackAngle(enemyViewPos);
    Quaternion targetRot = Quaternion.Euler(0f, pursuitAngle, 0f);
    transform.rotation = Quaternion.RotateTowards(
        transform.rotation,
        targetRot,
        CombatRotationSpeed * Time.deltaTime
    );

    transform.position += moveDir.normalized * worldSpeed * Time.deltaTime;
}
```

---

## 설계 근거

| 항목 | 내용 |
|------|------|
| 회전 속도 | 기존 `CombatRotationSpeed = 270f` 그대로 사용 (반대 방향에서도 약 0.67초 내 전환) |
| 각도 계산 | 기존 `CalculateAttackAngle(enemyViewPos)` 재사용 (`_meshYOffset` 보정 포함) |
| 회전 방식 | `Quaternion.RotateTowards` — 급격한 스냅 없이 매 프레임 조금씩 회전 |
| 적용 조건 | `dist > 0.01f` 내부 — 거의 도달한 경우 불필요한 회전 연산 방지 |

---

## 변경 불필요 파일

- `UnitCombatUseCase.cs` — 감지 범위 로직 변경 없음
- `UnitStats.cs`, `UnitData.cs` — 회전 관련 데이터 없음
- `NetworkCombatController.cs` — 서버 회전 → NetworkTransform 자동 동기화로 충분

---

## 위험 요소

| 위험 요소 | 상세 | 대응 |
|-----------|------|------|
| 회전과 이동 방향 불일치 | `enemyViewPos`가 매 프레임 변경되므로 회전과 이동 방향은 항상 일치 | 동일 변수 사용으로 자동 해결 |
| `dist ≤ 0.01f`일 때 회전 없음 | 거의 도달한 경우 의도적으로 회전 생략 | 이미 공격 사거리에 들어온 상태이므로 문제 없음 |
| 멀티플레이 이중 보간 | Update() 전투 회전과 동일한 패턴이므로 동일하게 서버 전용 실행 | Phase 1 자체가 서버 코루틴 내부이므로 별도 가드 불필요 |

---

## 구현 순서

1. `UnitView.cs` Phase 1 직선 이동 블록 수정 (단일 파일, 단일 위치)
