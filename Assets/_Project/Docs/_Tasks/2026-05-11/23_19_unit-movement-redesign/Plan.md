# Plan — 유닛 이동/전투 시스템 재설계

## 작업 목적

이동 슬롯과 공격 슬롯 시스템을 전면 폐기하고, 근접/원거리 구분 없이 모든 유닛이 동일한 3단계 상태 머신으로 동작하도록 교체한다.

기존 슬롯 기반 분산 방식은 수십 개 유닛이 동시에 동작하는 환경에서 조합 경우의 수가 폭발해 버그가 잦아졌다. 새 방식은 겹침을 허용하는 대신 규칙 수를 최소화하여 구현 안정성을 확보한다.

---

## ⚠️ 기존 로직 제거 항목 (최상단 고지)

아래 항목은 모두 **비활성화(주석 처리)** 방식으로 처리한다.  
최종 삭제는 [6] 사용자 테스트 통과 후, [7] 문서/메모리 업데이트 전에 수행한다.

| 대상 | 처리 방식 | 제거해도 안전한 근거 |
|------|-----------|---------------------|
| `TileMoveSlotManager` 생성 및 주입 코드 (GameBootstrapper) | 주석 처리 | 규칙 17 폐기. 이 클래스를 참조하는 유일한 곳이 UnitView._moveSlotManager이며, null 방어(폴백 존재) |
| `TileOccupancyManager` 생성 및 주입 코드 (GameBootstrapper) | 주석 처리 | 규칙 6 폐기. UnitMovementUseCase가 null 방어(occupancyManager = null이면 체크 무력화) |
| `AttackPositionManager` 생성 및 주입 코드 (GameBootstrapper) | 주석 처리 | 규칙 18 폐기. UnitView._attackPositionManager null 방어(폴백 존재) |
| `UnitView` — 슬롯/점유 관련 필드 및 코드 | 주석 처리 | 위 세 매니저 주입이 null이 되면 실제로 동작하지 않으므로 비활성화 안전 |
| `UnitMovementUseCase` — RegisterOccupancyMove, ReleaseOccupancy, FindForwardAvailable | 주석 처리 | 규칙 5·6 폐기. UnitView 호출부를 제거하면 데드코드가 되어 안전 |
| `UnitStats.StatValues.OccupancySize` 필드 및 `GetOccupancySize()` 메서드 | 주석 처리 | TileOccupancyManager 비활성화 후 호출처가 없음 |
| `UnitData.ClaimedTile` 필드 | 주석 처리 | 이동 슬롯 관련 용도. 현재 FlowField 도입 이후 실제 경로 차단에 미사용 상태로 확인됨 |

---

## 변경 항목 상세

### 1. UnitView.cs — 상태 머신 재구성 (가장 큰 변경)

**근거:** 규칙 8 (유닛 상태 머신), 규칙 9 (A* 이동 재개 방식), 규칙 7 (감지/공격 사거리)

**제거(주석 처리):**
- `_moveSlotManager` 필드 및 주입 코드
- `_attackPositionManager` 필드 및 주입 코드
- `_v2MoveSlotTile`, `_v2AttackSlotTargetCoord`, `_pendingOccupancyTile` 필드
- `_v2InStationaryCombat` 필드 (원거리 정지 전투 전용 플래그 — 이 개념 자체가 폐기)
- `ReleaseV2MoveSlotIfClaimed()`, `ReleaseV2AttackSlotIfClaimed()` 메서드
- `RegisterOccupancyMove()`, `ReleaseOccupancy()` 호출부
- Phase 1에서 `AttackPositionManager.ClaimByApproach()`로 슬롯 위치를 받아 이동하는 코드
- 원거리 유닛 전용 "Phase 0 중 공격 사거리 내 적 감지 시 즉시 정지" 분기

**유지:**
- Phase 0 (A* 타일→타일 Lerp 이동) 코드 본체 — 슬롯/점유 호출부만 제거
- Phase 2 (`FindForwardClosestTile()` 호출 로직) — 전투 종료 후 A* 재개에 동일하게 사용
- `HasEnemyInDetectRange()` / `HasEnemyInRange()` 호출 로직
- `ProcessStep()` 호출 (타일 도착 시 Domain Position 갱신)

**새 흐름 (근접/원거리 동일):**
```
A* 이동 중 매 프레임 → HasEnemyInDetectRange 체크
  → true : 전투 이동 진입 (타겟 월드 좌표로 직선 이동)
      → 매 프레임 HasEnemyInRange 체크
          → true  : 공격 상태 진입 (멈추고 TryAttack)
          → false (감지 범위 내) : 계속 추격
          → false (감지 범위 이탈) : FindForwardClosestTile → A* 재개
      → 공격 완료 후 :
          → 감지 내 + 공격 사거리 내  : 공격 상태 유지 (새 타겟)
          → 감지 내 + 공격 사거리 밖  : 전투 이동 재개
          → 감지 내 적 없음           : FindForwardClosestTile → A* 재개
```

---

### 2. UnitMovementUseCase.cs — 점유 관련 코드 비활성화

**근거:** 규칙 2 (겹침 허용), 규칙 6 폐기 (타일 점유 한도 제거)

**제거(주석 처리):**
- `_occupancyManager` 필드 및 생성자 파라미터
- `OnEntityDied` 구독 (점유 자동 해제 목적)
- `RegisterOccupancyMove()` 메서드 전체
- `ReleaseOccupancy()` 메서드 전체
- `GetOccupancySize()` 메서드 전체
- `FindForwardAvailable()` 메서드 전체
- `ProcessStep()` 내 `_occupancyManager.OnUnitRemoved(from, ...)` 호출부

**유지:**
- `RequestMove()` — A* 경로 요청, 변경 없음
- `ProcessStep()` — 방향 계산, Position 갱신, 이벤트 발행 (점유 코드만 제거)
- `FindForwardClosestTile()` — 전투 종료 후 앞쪽 타일 탐색, 변경 없음
- `IsWalkable()` — 변경 없음

---

### 3. UnitCombatUseCase.cs — 감지 사거리 판정 통합

**근거:** 규칙 7 (감지 사거리 > 공격 사거리, 근접/원거리 동일)

현재 `FindFirstEnemyInDetectRange()`는 근접유닛(AttackRange < 1.0)에 `MeleeDetectDist(0.866f)` 고정값을 사용하고, 원거리유닛은 `AttackRange × TileHeight`를 그대로 사용한다. 원거리 유닛은 DetectRange == AttackRange로 감지와 공격이 동일하다.

새 규칙에서는 모든 유닛이 `DetectRange × TileHeight`를 감지 사거리로 사용한다.

**변경:**
- `FindFirstEnemyInDetectRange()` 내부의 `isMelee` 분기 제거
- 모든 유닛에 `attacker.DetectRange × HexMetrics.TileHeight`를 감지 사거리로 적용
- 기존 `MeleeDetectDist` 상수는 주석 처리

**유지:**
- `FindFirstEnemyTarget()` — 공격 사거리 판정 로직, 변경 없음
- `TryAttack()`, `ApplyAttackDamage()`, `ExecuteAttack()` — 변경 없음
- `HasEnemyInRange()`, `HasEnemyInDetectRange()` — 변경 없음 (내부 호출 메서드가 바뀌므로 자동 반영)

---

### 4. UnitStats.cs — OccupancySize 비활성화, AttackKind 주석 정리

**근거:** 규칙 6 폐기 (타일 점유 한도 제거)

**제거(주석 처리):**
- `StatValues` 구조체 내 `OccupancySize` 필드
- `GetOccupancySize()` 메서드 전체

**수정:**
- `AttackKind` enum의 주석 — 기존 "슬롯 해제 → 공격 슬롯으로 이동" 설명을 새 규칙 기준으로 업데이트

**유지:**
- `AttackKind` enum 자체 — 공격 방식(근접/원거리) 분류 목적으로 유지. 이동 로직 분기에는 더 이상 사용하지 않음
- 나머지 모든 스탯 조회 메서드

---

### 5. UnitData.cs — ClaimedTile 비활성화

**근거:** 규칙 2 폐기 (이동 슬롯 없음), 플로우 필드 도입 이후 실제 경로 차단에 미사용

**제거(주석 처리):**
- `ClaimedTile` 필드 전체

---

### 6. GameBootstrapper — 제거된 서비스 비활성화

**근거:** TileMoveSlotManager·TileOccupancyManager·AttackPositionManager 폐기

**제거(주석 처리):**
- `TileMoveSlotManager` 생성 코드 및 UnitView 주입 코드
- `TileOccupancyManager` 생성 코드 및 UnitMovementUseCase 주입 코드
- `AttackPositionManager` 생성 코드 및 UnitView 주입 코드

---

### 7. Inspector 작업 — 원거리 유닛 DetectRange 수치 분리

**근거:** 규칙 7 (감지 사거리 항상 공격 사거리보다 길어야 함)

현재 원거리 유닛의 `DetectRange`는 `AttackRange`와 동일한 값으로 설정되어 있다 (스탯 Config Inspector 기준). 새 규칙에서 DetectRange > AttackRange 조건을 충족하려면 원거리 유닛별로 DetectRange를 별도 수치로 설정해야 한다.

Inspector에서 `UnitStatsConfig`의 각 원거리 유닛(`Pistoleer`, `Sniper`, `Assault`, `FoxMagician`, `InfernoSpirit`) DetectRange 값을 AttackRange보다 크게 조정한다.

구체적 수치는 구현 후 플레이 테스트를 통해 조율하며, 확정된 값은 `GameSystemRules.md`에 기록한다.

---

## 작업 순서

1. **GameBootstrapper** — 세 매니저 주입 코드 주석 처리 (가장 안전한 비활성화 시작점)
2. **UnitView.cs** — 슬롯/점유 관련 필드·메서드 주석 처리 + 새 상태 머신 코드 작성
3. **UnitMovementUseCase.cs** — 점유 관련 메서드 주석 처리
4. **UnitCombatUseCase.cs** — DetectRange 판정 통합
5. **UnitData.cs** — ClaimedTile 주석 처리
6. **UnitStats.cs** — OccupancySize 주석 처리, AttackKind 주석 업데이트
7. **Inspector** — 원거리 유닛 DetectRange 수치 분리 설정

---

## 위험 요소

| 항목 | 위험 | 대응 |
|------|------|------|
| `NetworkCombatController` | 서버 측 전투 로직에서 AttackKind 분기나 슬롯 관련 코드가 있을 수 있음 | 구현 전 해당 파일 확인 필요 |
| 원거리 유닛 DetectRange | DetectRange 미설정 시 DetectRange == AttackRange로 폴백되어 감지 즉시 공격 상태로 전환됨 | Inspector 설정 작업(7번)을 구현 완료 후 반드시 병행 |
| `ProcessStep()` 수정 | FROM 점유 해제 코드만 제거하는 정밀 수정이므로 방향 계산·Position 업데이트 로직에 영향 없어야 함 | 수정 후 컴파일 에러 즉시 확인 |
| UnitView 코루틴 흐름 | 파일이 매우 크고 Phase 0/1/2 전환 코드가 복잡하게 얽혀 있어 누락 가능성 있음 | game-programmer 에이전트 위임 시 Research.md와 함께 전달 |
