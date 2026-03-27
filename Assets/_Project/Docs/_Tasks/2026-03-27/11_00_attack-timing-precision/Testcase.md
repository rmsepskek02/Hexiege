# Testcase: 공격 타이밍 정밀화

**작성일:** 2026-03-27

---

## 테스트 케이스

### SINGLE-1: 타격 프레임 데미지 타이밍 (Pistoleer)

**전제:** 싱글플레이, 양 팀 유닛 교전 중

**동작:**
1. Pistoleer 유닛이 적을 감지하고 공격 모션 시작 관찰

**기댓값:**
- 공격 모션 중 타격 프레임(0:25, 약 0.833초)에 적 HP가 감소
- HP 감소가 공격 모션 시작보다 먼저 발생하지 않음

**결과:** PASS ✅ (2026-03-27 실기 확인)

---

### SINGLE-2: 타격 프레임 데미지 타이밍 (Assault)

**전제:** 싱글플레이, 양 팀 유닛 교전 중

**동작:**
1. Assault 유닛이 적을 감지하고 공격 모션 시작 관찰

**기댓값:**
- 공격 모션 중 타격 프레임(0:04, 약 0.133초)에 적 HP가 감소

**결과:** PASS ✅ (2026-03-27 실기 확인)

---

### SINGLE-3: 타격 프레임 데미지 타이밍 (Sniper)

**전제:** 싱글플레이, 양 팀 유닛 교전 중

**동작:**
1. Sniper 유닛이 적을 감지하고 공격 모션 시작 관찰

**기댓값:**
- 공격 모션 중 타격 프레임(2:00, 2.0초)에 적 HP가 감소

**결과:** PASS ✅ (2026-03-27 실기 확인)

---

### SINGLE-4: 쿨다운 통일 확인

**전제:** 싱글플레이, 유닛 교전 중

**동작:**
1. 유닛이 연속으로 공격하는 것을 관찰

**기댓값:**
- 공격 간격이 각 유닛의 AttackCooldown 수치와 시각적으로 일치
- 이전 버전 대비 공격 빈도 변화 없음

**결과:** PASS ✅ (2026-03-27 실기 확인)

---

### MULTI-1: 멀티플레이 타격 프레임 데미지 동기화

**전제:** HOST + CLIENT, 양 팀 유닛 교전 중

**동작:**
1. HOST/CLIENT 양쪽 화면에서 공격 모션과 HP 감소 타이밍 관찰

**기댓값:**
- 공격 모션이 재생된 후 타격 프레임 시점에 HP가 감소
- HP 감소가 공격 모션보다 먼저 발생하지 않음 (이전 버그 해소)

**결과:** PASS ✅ (2026-03-27 실기 확인)

---

### MULTI-2: 타겟 고정 (Target Lock)

**전제:** HOST + CLIENT, Sniper 유닛 교전 중

**동작:**
1. Sniper가 적을 감지하고 공격 모션 시작
2. 타격 프레임(2초) 도달 전 타겟이 사거리 밖으로 이동

**기댓값:**
- 타겟이 사거리를 벗어나도 데미지 적용됨

**결과:** CONDITIONAL PASS — 코드 상으로는 올바르게 구현됨 (IsTargetInRange 제거 확인). 실기 검증은 Sniper 2초 딜레이 중 타겟 이탈 조건 재현이 어려워 코드 리뷰로 대체.

---

## QA 정적 분석 메모

### 수정된 파일 및 핵심 변경
- `Domain/Unit/UnitStats.cs`: `GetHitFrameTime()` static 메서드 추가 (Assault=0.133f, Pistoleer=0.833f, Sniper=2.000f)
- `Domain/Unit/UnitData.cs`: `HitFrameTime` 프로퍼티 추가
- `Application/UseCases/UnitCombatUseCase.cs`:
  - `TryFindTarget()`: 타겟 탐색만 (데미지/쿨다운 없음)
  - `ApplyAttackDamage()`: 타겟 고정 설계 — IsInRange 체크 없음, IsAlive만 재확인
  - `TickCooldowns(float dt)`: 싱글플레이 전용 쿨다운 일괄 감소
- `Presentation/Unit/UnitView.cs`: `Update()` 쿨다운 감소 코드 삭제
- `Bootstrap/GameBootstrapper.cs`: `Update()` 추가 — 싱글 `TickCooldowns()` 호출
- `Infrastructure/Network/NetworkCombatController.cs`:
  - `TickCombat()`: `TryFindTarget()` + 쿨다운 리셋 + `DelayedAttackDamage` 코루틴
  - `DelayedAttackDamage()`: HitFrameTime 대기 후 `ApplyAttackDamage()` 호출

### 잠재적 주의 사항
- Sniper HitFrameTime=2.0초: 딜레이 중 공격자 사망 시 `attacker.IsAlive` 체크로 취소됨 (정상)
- 딜레이 중 타겟 사망 시 `target.IsAlive` 체크로 취소됨 (정상)
- 타겟 이탈은 취소하지 않음 — 타겟 고정 설계 의도
