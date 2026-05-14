# Research: 공격 중 타겟 방향 지속 추적

**날짜**: 2026-04-11

---

## 문제 요약

원거리 유닛이 공격 애니메이션을 재생 중일 때, 타겟이 이동해도 유닛의 회전이 고정되어 있다.
특히 근접 유닛(적)이 원거리 유닛을 향해 계속 걸어오는 상황에서 원거리 유닛이 엉뚱한 방향을 보며 공격하는 것처럼 보인다.

---

## 현재 동작 분석

### 회전이 설정되는 시점

| 메서드 | 파일 | 동작 |
|--------|------|------|
| `StartCombatAnimation` (651~666) | `UnitView.cs` | 공격 시작 시 타겟 방향으로 1회 스냅 |
| `ChangeTarget` (678~685) | `UnitView.cs` | 타겟 변경 시 새 타겟 방향으로 1회 스냅 |

두 메서드 모두 **타겟 방향을 딱 1회만 설정**하고 이후 추적하지 않는다.

### Update() 부재

`UnitView.cs`에는 `Update()` 또는 `LateUpdate()` 메서드가 존재하지 않는다.
→ 현재 프레임마다 실행되는 회전 로직이 없음.

### 기존 인프라 (재사용 가능)

- `GetTargetWorldPos(int targetId, bool targetIsUnit)` (285~295): 팩토리를 통해 타겟 오브젝트의 `transform.position` 조회
- `CalculateAttackAngle(Vector3 targetWorldPos)` (272~279): Atan2 기반 Y축 각도 계산
- `_unitFactory`, `_buildingFactory`: 이미 필드로 보유 중

두 메서드가 이미 구현되어 있으므로 **`Update()`에서 이 두 메서드를 프레임마다 호출**하면 추적이 가능하다.

### 현재 combat 상태 추적 필드 부재

UnitView에 현재 전투 중인지 여부(`_inCombat`)와 현재 타겟 정보(`_combatTargetId`, `_combatTargetIsUnit`)를 저장하는 필드가 없다.

---

## 영향 범위

| 파일 | 변경 필요 여부 |
|------|--------------|
| `Presentation/Unit/UnitView.cs` | ✅ 필수 — 필드 추가 + Update() 추가 + 3개 메서드 수정 |
| 그 외 | ❌ 변경 불필요 |

---

## 멀티플레이 영향

`Update()`는 서버/클라이언트 모두에서 실행된다.

- **서버**: 매 프레임 `transform.rotation` 갱신 → NetworkTransform이 클라이언트에 보간 전달
- **클라이언트**: NetworkTransform이 이미 서버 rotation을 동기화하므로 클라이언트 자체 Update() 회전이 덮어씌워짐 (기존 동작과 동일)

별도 RPC 추가 불필요.

---

## 전투 종료 처리

`StopCombatAnimation()`은 현재 의도적으로 비어있다(멀티 Walk 전환 타이밍 충돌 방지).
여기에 `_inCombat = false` 추가는 Walk 전환 로직에 영향 없음 — 회전 추적만 중단시킴.

---

## 결론

- 신규 필드 3개 추가
- `Update()` 신규 추가 (공격 중에만 동작)
- `StartCombatAnimation`, `ChangeTarget`, `StopCombatAnimation` 소규모 수정
- 기존 기반 코드(`GetTargetWorldPos`, `CalculateAttackAngle`) 재사용
