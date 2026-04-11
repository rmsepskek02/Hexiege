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
// Id + isUnit 조합 대신 Transform 참조를 직접 저장 → 매 프레임 팩토리 조회 불필요
// 타겟 오브젝트 파괴 시 Unity가 자동으로 null 처리
private Transform _combatTargetTransform = null;
```

#### 2. `Update()` 신규 추가

- `_combatTargetTransform != null`인 경우에만 실행
- 멀티플레이 클라이언트에서는 rotation 쓰기 건너뜀 (NetworkTransform 충돌 방지)
  - 조건: `NetworkContext.IsNetworkActive && NetworkManager.Singleton?.IsServer == false`
- `_combatTargetTransform.position` 직접 읽어 `CalculateAttackAngle()` 호출
- `transform.rotation = Quaternion.Euler(0f, angle, 0f)` 스냅

#### 3. `StartCombatAnimation()` 수정

- 기존 1회 스냅 로직 유지
- 추가: `_combatTargetTransform` 저장
  - `GetTargetWorldPos` 대신 팩토리에서 GameObject를 꺼내 `.transform` 저장

#### 4. `ChangeTarget()` 수정

- 기존 1회 스냅 로직 유지
- 추가: `_combatTargetTransform` 갱신 (새 타겟의 Transform 참조로 교체)

#### 5. `StopCombatAnimation()` 수정

- 기존 빈 메서드 유지 (Walk 전환 타이밍 충돌 방지)
- 추가: `_combatTargetTransform = null` — 회전 추적 중단

---

## 위험 요소

| 항목 | 내용 |
|------|------|
| 멀티플레이 클라이언트 | `Update()` 내 가드로 rotation 쓰기 건너뜀. 기존 클라이언트 자체 회전 로직 제거 원칙과 일치. |
| 타겟 소멸 | Unity가 파괴된 오브젝트의 Transform 참조를 자동 null 처리 → `Update()` 가드에서 자연 차단. |
| 이동 중 간섭 | `_combatTargetTransform`은 `StopCombatAnimation` 호출 시 null로 초기화 → Walk 상태에서 `Update()` 미실행. |
| 성능 | Transform 참조 직접 접근 — 팩토리 딕셔너리 조회 없음. 매 프레임 가벼운 연산. |

---

## 검증 포인트

1. 원거리 유닛이 공격 중 타겟(이동하는 근접 유닛)을 계속 바라본다
2. 타겟이 사망하거나 전투가 끝나면 회전 추적이 중단된다
3. 이동 중(Walk 상태) 원거리 유닛의 회전이 달라지지 않는다
4. 멀티플레이에서도 동일하게 동작한다
