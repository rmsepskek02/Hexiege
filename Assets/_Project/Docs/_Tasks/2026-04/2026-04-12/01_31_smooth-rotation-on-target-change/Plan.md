# Plan: 타겟 변경 시 회전 부드럽게 전환

**날짜**: 2026-04-12

---

## 목표

공격 중 타겟이 교체될 때 회전이 즉시 스냅되지 않고, 지정 속도로 부드럽게 새 타겟 방향으로 전환된다.

---

## 핵심 원칙

- `Update()`에서 `Quaternion.RotateTowards`를 사용하여 매 프레임 목표 방향으로 점진 회전
- 서버가 직접 매 프레임 rotation 값을 갱신 → NetworkTransform이 그 값을 클라이언트에 동기화
- DORotate처럼 서버가 독립 애니메이션을 실행하는 방식이 아니므로 이중 보간 문제 없음

---

## 수정 파일

### `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

#### 1. 회전 속도 상수 추가

기존 상수 선언부 근처에 추가:

```
// 공격 중 타겟 방향으로 회전하는 속도 (초당 각도).
// 값이 크면 빠르게 전환(스냅에 가까움), 값이 작으면 느리게 전환(보간).
// 270°/s 기준: 타겟이 정반대 방향(180°)에 있어도 약 0.67초 내 전환 완료.
private const float CombatRotationSpeed = 270f;
```

#### 2. `Update()` 수정

기존 즉시 스냅:
```
float angle = CalculateAttackAngle(_combatTargetTransform.position);
transform.rotation = Quaternion.Euler(0f, angle, 0f);
```

변경 후:
```
float angle = CalculateAttackAngle(_combatTargetTransform.position);
Quaternion targetRotation = Quaternion.Euler(0f, angle, 0f);
transform.rotation = Quaternion.RotateTowards(
    transform.rotation,
    targetRotation,
    CombatRotationSpeed * Time.deltaTime
);
```

#### 3. `ChangeTarget()` 즉시 스냅 제거

기존: 타겟 변경 시 새 방향으로 즉시 스냅 후 Update()에 넘김.
변경: 즉시 스냅 라인 제거. Update()가 RotateTowards로 부드럽게 전환.

제거 대상 코드:
```
// 새 타겟 방향으로 즉시 스냅 회전
float angle = CalculateAttackAngle(GetTargetWorldPos(targetId, targetIsUnit));
transform.rotation = Quaternion.Euler(0f, angle, 0f);
```

`StartCombatAnimation()`의 1회 스냅은 유지:
- 전투 최초 진입 시 즉시 방향 전환은 자연스럽다.
- 유닛이 이동 방향에서 공격 방향으로 전환되는 명확한 신호 역할을 한다.

---

## 위험 요소

| 항목 | 내용 |
|------|------|
| NetworkTransform 이중 보간 | RotateTowards는 서버가 매 프레임 직접 rotation 값을 변경하는 방식. DORotate처럼 서버가 독립 보간을 실행하지 않으므로 클라이언트는 NetworkTransform 1틱 지연만 발생. 기존과 동일. |
| 타겟 이동 추적 지연 | RotateTowards는 목표와의 각도 차이가 작으면 사실상 즉시 도달(프레임당 270/60 ≈ 4.5°). 일반적인 유닛 이동 속도에서 추적 지연은 체감 불가. |
| ChangeTarget() 스냅 제거 후 딜레이 | 기존에는 ChangeTarget() 호출 시 즉시 스냅 후 Update()가 이어받았음. 이제는 Update()가 처음부터 RotateTowards로 전환. 최대 0.67초(180° 차이) 동안 유닛이 공격 중에 회전 중인 상태가 보임. 의도된 동작. |
| CombatRotationSpeed 수치 조정 | 270°/s는 초기값. 실기 테스트 후 사용자 피드백에 따라 조정 가능. |

---

## 검증 포인트

1. 타겟 변경 시 회전이 즉시 스냅되지 않고 부드럽게 전환된다
2. 타겟이 이동할 때 회전 추적에 눈에 띄는 지연이 없다
3. 전투 최초 진입 시 즉시 타겟 방향으로 스냅된다 (StartCombatAnimation 유지)
4. Host와 Client 모두 동일한 자연스러운 회전이 보인다
