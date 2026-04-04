# Testcase: NetworkTransform Rotation 동기화로 전환

**작성일:** 2026-03-29

---

## 사전 준비

**Editor 스크립트 실행 필요:**
1. Unity 메뉴 `Hexiege > Add NetworkTransform To Unit Prefabs` 실행
2. 콘솔에서 SyncRotAngleY 변경 확인

---

## 이동 방향 테스트

### MULTI-1: Red 클라이언트 기본 이동 방향 일치

**전제:** 멀티플레이 (Host=Blue, Client=Red). 양쪽 유닛 스폰 완료.

**동작:**
1. Red 클라이언트에서 아무 유닛을 선택하고 먼 타일로 이동 명령
2. 이동 시작 시점부터 유닛의 바라보는 방향을 관찰

**기댓값:**
- 이동 시작 직후 유닛이 이동 방향을 바라봄
- 스폰 방향으로 고정되거나 깜빡이는 현상 없음
- NetworkTransform 보간에 의해 부드럽게 회전

**결과:** PASS

---

### MULTI-2: Red 클라이언트 복수 유닛 이동

**전제:** 멀티플레이 (Host=Blue, Client=Red). 유닛 3개 이상 스폰.

**동작:**
1. Red 클라이언트에서 여러 유닛을 각기 다른 방향으로 이동 명령
2. 모든 유닛의 방향이 올바른지 관찰

**기댓값:**
- 모든 유닛이 각자의 이동 방향을 올바르게 바라봄
- 첫 번째와 이후 유닛 간 차이 없음

**결과:** PASS

---

### MULTI-3: 회전 반전 보정 (Red 클라이언트 = +180°)

**전제:** 멀티플레이 (Host=Blue, Client=Red).

**동작:**
1. Blue Host에서 유닛이 남쪽(하단)으로 이동하는 것을 확인
2. 같은 유닛이 Red 클라이언트에서 북쪽(상단)으로 이동하는 것을 확인
3. 양쪽 모두 유닛이 이동 방향을 올바르게 바라보는지 확인

**기댓값:**
- Blue: 유닛이 남쪽 방향을 바라봄
- Red: 같은 유닛이 북쪽 방향을 바라봄 (반전)
- 양쪽 모두 이동 방향과 바라보는 방향이 일치

**결과:** PASS

---

## 공격 방향 테스트

### MULTI-4: 공격 시 타겟 방향 회전

**전제:** 멀티플레이 (Host=Blue, Client=Red). Red 유닛이 적과 교전 가능 거리.

**동작:**
1. Red 유닛이 적을 만나 공격 시작
2. 공격 시 유닛의 바라보는 방향이 적 방향인지 확인

**기댓값:**
- 공격 시작과 동시에 적 방향으로 회전
- 이전 이동 방향이 잠시 유지되는 현상 없음

**결과:** PASS

---

### MULTI-5: 공격 후 이동 재개 (BUG-B 재현 테스트)

**전제:** 멀티플레이 (Host=Blue, Client=Red). Red 유닛이 적과 전투 중.

**동작:**
1. Red 유닛이 이동 중 적을 만나 공격
2. 적 사망 또는 사거리 이탈 후 이동 재개 관찰
3. 제자리 Walk 현상이 발생하는지 확인

**기댓값:**
- 전투 종료 후 즉시 Walk 애니메이션으로 전환
- 제자리에서 걷는 현상(Walk-in-place) 없음
- 이동과 Walk 애니메이션이 동기화

**결과:** 타겟이 없어진 경우 공격애니메이션 상태로 이동하는 현상이 있음 
공격 중 타겟이 없어져서 공격애니메이션이 끝나지 않은 채 이동하는 것 뿐만아니라 이동하면서 공격애니메이션이 또 재생되었음

---

### MULTI-6: 연속 공격 후 이동 재개

**전제:** 멀티플레이 (Host=Blue, Client=Red). 적 유닛 다수 밀집.

**동작:**
1. Red 유닛이 적 다수 방향으로 이동
2. 첫 적 공격 → 사망 → 이동 → 두 번째 적 공격 관찰
3. 모든 전환이 매끄러운지 확인

**기댓값:**
- Walk → Attack → Walk → Attack 전환이 자연스러움
- 어떤 시점에서도 제자리 걷기 없음

**결과:** 제자리 걷기는 없으나 공격애니메이션 상태로 이동하는 현상이 있음

---

## 회귀 테스트

### MULTI-7: Blue Host 유닛 동작

**전제:** 멀티플레이 (Host=Blue, Client=Red).

**동작:**
1. Blue Host에서 자기 유닛의 이동, 공격, 방향 전환 관찰
2. 기존과 동일하게 정상 동작하는지 확인

**기댓값:**
- Blue Host 유닛의 모든 동작이 기존과 동일
- 즉시 스냅으로 인한 부자연스러운 회전 없음

**결과:** PASS

---

### SINGLE-1: 싱글플레이 정상 확인

**전제:** 싱글플레이 모드.

**동작:**
1. 유닛 이동, 공격, 방향 전환 모두 테스트
2. 기존과 동일하게 동작하는지 확인

**기댓값:**
- 싱글플레이 동작 변화 없음

**결과:** 멀티 우선 테스트 중. 보류

---

### MULTI-8: 회전 보간 자연스러움 확인

**전제:** 멀티플레이 (Host=Blue, Client=Red).

**동작:**
1. 유닛이 방향 전환할 때 클라이언트에서 회전이 부드러운지 관찰
2. 즉시 스냅(뚝뚝 끊김) vs 보간(부드러움) 확인

**기댓값:**
- NetworkTransform 보간으로 부드러운 회전
- DORotate 때와 유사한 자연스러움 (0.1초 보간)

**결과:** PASS

---

## QA 섹션

### 정적 분석 결과 (2026-03-29)

**판정: PASS** (컴파일 오류 없음, 잔여 참조 없음)

제거된 심볼 전수 검색 결과 프로젝트 전체에서 잔여 참조 미발견:
- `TurnToFaceClientRpc`, `_isWalkPending`, `HasReceivedTurnToFace`, `MarkTurnToFaceReceived`
- `SetPreRotating`, `SetAttackRotating`, `ResetMovementTracking`, `ResetPositionTracking`
- `OnUnitFacingChanged`, `UnitFacingChangedEvent`, `_networkUnit`, `_rotationDuration`
- `using DG.Tweening` (UnitView.cs, NetworkCombatController.cs 에서 정상 제거)

Minor: `AddNetworkTransformToPrefabs.cs` 구버전 주석 → 수정 완료

---

### 테스트 결과 요약

| ID | 항목 | 결과 |
|----|------|------|
| MULTI-1 | Red 클라이언트 기본 이동 방향 | PASS |
| MULTI-2 | Red 클라이언트 복수 유닛 이동 | PASS |
| MULTI-3 | 회전 반전 보정 (+180°) | PASS |
| MULTI-4 | 공격 시 타겟 방향 회전 | PASS |
| MULTI-5 | 공격 후 이동 재개 (BUG-B 재현) | FAIL → 후속 수정 필요 |
| MULTI-6 | 연속 공격 후 이동 재개 | PARTIAL (제자리 걷기 없음, 공격 애니메이션 지속 이슈) |
| MULTI-7 | Blue Host 유닛 동작 회귀 | PASS |
| MULTI-8 | 회전 보간 자연스러움 | PASS |
| SINGLE-1 | 싱글플레이 정상 확인 | 보류 |

---

### 잔여 이슈: 공격 중 타겟 소멸 시 애니메이션 처리

**현상 (MULTI-5, MULTI-6):**
- 공격 중 타겟이 사라지면 공격 애니메이션이 끝나지 않은 채 이동 시작
- 이동 중 공격 애니메이션이 다시 재생되는 경우도 발생

**원인:**
`if (_attackCoroutine != null) return` 가드가 Walk 전환을 완전히 차단.
이동 명령이 도달해도 공격 코루틴이 완료될 때까지 Walk가 시작되지 않음.

**해결 방향:**
공격 애니메이션 클립 끝까지 재생 후 Walk로 자동 전환 (`_isWalkPending` 플래그 복원).
상세 내용은 Plan.md 수정 6 참조.
