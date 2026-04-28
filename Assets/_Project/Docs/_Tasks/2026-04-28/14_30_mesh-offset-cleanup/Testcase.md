# Testcase — Mesh Y Offset 제거 및 통일

## 작업 목적 (자연어 설명)

6종 유닛 프리팹의 메시 자식 회전값과 `_meshYOffset`을 모두 0으로 통일하고,
이동 애니메이션 Rotation Offset도 0으로 맞춘 뒤 코드 정리를 완료했다.
이 과정에서 DirectionAngles 값이 잘못된 방향으로 수정된 것을 발견하여 함께 수정했다.

---

## TC 목록

### TC-SINGLE-01: 생산 후 이동 방향이 시각적으로 올바른지 확인

**전제:** BearGuard, FoxMagician, LionKnight, EmberSpirit, FlameSpirit, InfernoSpirit 중 하나를 싱글플레이에서 생산할 수 있는 환경

**동작:**
1. 싱글플레이 게임을 시작한다
2. 배럭에서 위 6종 유닛 중 하나를 생산한다
3. 유닛이 적 Castle 방향으로 이동하는 것을 관찰한다

**기댓값:**
- 유닛이 실제로 이동하는 방향을 향해 바라본다 (이동 방향과 바라보는 방향이 일치)
- Unity Inspector에서 루트 오브젝트의 Y 회전값이 이동 방향에 맞는 각도로 표시된다

**결과:** PASS (2026-04-29 사용자 확인)

---

### TC-SINGLE-02: 공격 시 타겟 방향을 정확히 바라보는지 확인

**전제:** TC-SINGLE-01과 동일한 환경

**동작:**
1. 유닛을 생산하여 적 유닛 또는 Castle 근처로 이동시킨다
2. 유닛이 공격을 시작하면 바라보는 방향을 관찰한다

**기댓값:**
- 공격 시 유닛이 타겟 방향을 정확하게 바라본다
- 이동 방향 변경과 공격 방향이 서로 영향을 주지 않는다

**결과:** PASS (2026-04-29 사용자 확인)

---

### TC-SINGLE-03: 인간 유닛 3종(Assault, Pistoleer, Sniper)의 동작이 기존과 동일한지 확인

**전제:** 싱글플레이 환경

**동작:**
1. Human 종족으로 Assault, Pistoleer, Sniper를 각각 생산한다
2. 이동 및 공격 동작을 관찰한다

**기댓값:**
- 이전에 정상 동작이 확인된 Human 유닛 3종이 이번 변경 후에도 동일하게 올바른 방향으로 이동하고 공격한다

**결과:** PASS (2026-04-29 사용자 확인)

---

## QA 섹션

### 정적 분석 결과

**변경 내용:**

1. **DirectionAngles 수정** (`UnitView.cs`)
   - 변경 전: `{ 0f, 60f, 120f, 180f, 240f, 300f }` (Phase 1에서 잘못 적용된 값)
   - 변경 후: `{ 60f, 120f, 180f, 240f, 300f, 0f }` (FlatTop 월드 좌표 atan2 기준 올바른 값)
   - 근거: FlatTop 헥스에서 NW(Q=0, R-1)의 실제 Unity 월드 각도 = atan2(0, +1) = 0°

2. **`_meshYOffset` 제거** (`UnitView.cs`)
   - SerializeField 변수 삭제
   - `CalculateAttackAngle()`에서 `- _meshYOffset` 제거
   - 공격 방향은 Atan2 순수 값으로 처리 (메시 Y=0이므로 보정 불필요)

3. **DirectionAngles 주석 수정** (`UnitView.cs`)
   - 이전: "NE(0)=0, E(1)=60, ..." (잘못된 값)
   - 이후: "NE(0)=60, E(1)=120, ..." (올바른 값)

**버그 없음** — 컴파일 에러 0건. 사용자 실기 테스트 전 항목 PASS.
