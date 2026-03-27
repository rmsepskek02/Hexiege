# Testcase: 이동 전 회전 타이밍 수정

**작성일:** 2026-03-27

---

## 테스트 케이스

### MULTI-1: 이동 시작 시 회전 선행 확인

**전제:** HOST + CLIENT 각 1명, 유닛 생산 완료 후 이동 중

**동작:**
1. 유닛이 새로운 방향으로 이동을 시작하는 순간 관찰

**기댓값:**
- 유닛이 이동 방향으로 먼저 몸을 돌린 후 이동을 시작함
- 회전 중에는 제자리에 머물러 있다가 회전 완료 후 걷기 시작

**결과:** PASS ✅ (2026-03-27 실기 확인)

---

### MULTI-2: 연속 이동 명령 시 회전 안정성

**전제:** HOST + CLIENT, 유닛이 이동 중

**동작:**
1. 유닛이 이동하는 동안 여러 번 경로가 바뀌는 상황 발생

**기댓값:**
- 방향이 바뀔 때마다 회전이 정상적으로 작동
- 회전이 고착되거나 움직이지 않는 현상 없음

**결과:** PASS ✅ (2026-03-27 실기 확인)

---

### MULTI-3: 공격 후 이동 시 회전 정상 동작

**전제:** HOST + CLIENT, 유닛이 적을 공격 후 이동 재개

**동작:**
1. 유닛이 적을 공격하다가 적이 사망하거나 범위 이탈로 이동 재개

**기댓값:**
- 이동 재개 시 회전 선행 정상 작동
- 공격 방향과 다른 방향으로 이동 시 회전 후 이동

**결과:** PASS ✅ (2026-03-27 실기 확인)

---

## QA 정적 분석 메모

### 수정 내용
- `NetworkUnit._isPreRotating` 플래그 추가
- LateUpdate 델타 회전에 `!_isPreRotating` 조건 추가
- `ResetMovementTracking()`에 `_isPreRotating = false` 안전망 추가
- `TurnToFaceClientRpc`에서 `SetPreRotating(true)` + DORotate `.OnComplete(() => SetPreRotating(false))`

### 근본 원인 및 수정 근거
- DOTween(Update 단계 실행) vs NetworkUnit.LateUpdate 충돌 구조
- LateUpdate가 DOTween 이후에 실행되어 매 프레임 rotation 덮어씌움
- `_isPreRotating` 플래그로 DORotate 실행 중 LateUpdate 차단 → 충돌 해소
- DOKill 등 중단 시 OnComplete 미호출 → `ResetMovementTracking()` 안전망으로 플래그 고착 방지
