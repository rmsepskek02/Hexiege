# Plan — 근접유닛 적 감지 사거리 개선

## 개요

근접유닛의 적 감지 사거리(DetectRange)를 공격 사거리(AttackRange)와 분리.
근접유닛이 인접 타일의 적을 감지하면 경로를 재조정하여 교전.

---

## 설계 원칙

| 개념 | 설명 | 근접유닛 값 |
|------|------|-----------|
| **DetectRange** | 적을 인식하고 경로를 전환하는 사거리 | 1.0f (타일 단위) |
| **AttackRange** | 실제 공격이 가능한 사거리 (변경 없음) | 0.5f → 월드 0.3f (MeleeContactDist) |

원거리유닛: DetectRange = AttackRange (기존 동작 유지, 변경 없음)

---

## 변경 파일 목록

### 1. `Domain/Unit/UnitStats.cs` — DetectRange 메서드 추가

**추가 위치:** `GetAttackRange()` 메서드 아래

```
GetDetectRange(UnitType type):
  근접유닛 (FlameSpirit, EmberSpirit, BearGuard, LionKnight): 1.0f
  원거리유닛 나머지: AttackRange와 동일값 반환
```

- 타일 단위로 정의 (world 변환은 UseCase에서 처리)
- 원거리유닛은 DetectRange = AttackRange → 기존 동작 완전 유지

---

### 2. `Domain/Unit/UnitData.cs` — DetectRange 프로퍼티 추가

**추가 위치:** `AttackRange` 프로퍼티 아래 (라인 61)

```
public float DetectRange { get; }
```

**두 생성자 모두 수정:**
- 기본 생성자 (라인 104): `detectRange` 파라미터 추가, `DetectRange = detectRange;`
- 네트워크 재생성 생성자 (라인 138): 동일

---

### 3. `Application/UseCases/UnitCombatUseCase.cs`

#### 3-1. 상수 추가

```
// 근접유닛 감지 사거리 (타일 1개 = TileHeight 0.866f)
// 인접 타일 중심(0.866f)을 포함하는 최소 거리 + Epsilon
private const float MeleeDetectDist = HexMetrics.TileHeight;  // 0.866f
```

#### 3-2. `HasEnemyInDetectRange()` 메서드 추가

`HasEnemyInRange()` (라인 253) 아래에 추가.

```
public bool HasEnemyInDetectRange(UnitData attacker)
```

- `FindFirstEnemyTarget()` 대신 `FindFirstEnemyInDetectRange()` 호출
- UnitView.MoveAlongPath()에서 이동 정지 판단에 사용

#### 3-3. `FindFirstEnemyInDetectRange()` private 메서드 추가

`FindFirstEnemyTarget()` (라인 266) 구조를 복사하여 DetectRange 기준으로 판정:

```
private IDamageable FindFirstEnemyInDetectRange(UnitData attacker)
```

- 근접유닛: `unitMaxDist = MeleeDetectDist + Epsilon` (0.916f)
- 원거리유닛: `unitMaxDist = attacker.AttackRange * TileHeight + Epsilon` (기존 동일)
- 건물 감지 거리도 동일하게 DetectRange 기준으로 조정

#### 3-4. `FindNearestEnemyInDetectRange()` public 메서드 추가

```
public (int id, bool isUnit)? FindNearestEnemyInDetectRange(UnitData attacker)
```

- `FindNearestEnemy()`와 동일한 구조, DetectRange 기준 탐색
- UnitView에서 감지 후 재경로 대상 타겟 ID 확보에 사용

---

### 4. `Application/UseCases/UnitCombatUseCase.cs` — 추적용 ID 반환 메서드 추가

#### 4-1. `FindNearestEnemyInDetectRange()` 추가

```
public (int id, bool isUnit)? FindNearestEnemyInDetectRange(UnitData attacker)
```

- `FindFirstEnemyInDetectRange()`가 반환한 `IDamageable`에서 `(Id, isUnit)` 추출
- UnitView의 Phase 1 추적 루프에서 매 프레임 적 위치 조회에 사용

---

### 5. `Presentation/Unit/UnitView.cs` — 하이브리드 이동 시스템

**변경 위치:** ProcessStep 완료 직후 detect 체크 블록 + 새 Phase 1/2 루프 추가

#### 버그 원인 히스토리

| 단계 | 문제 | 원인 | 수정 |
|------|------|------|------|
| 1차 구현 | 게임 멈춤(무한루프) | Lerp 내 detect → `i=0` 재시작 → 또 detect → 무한반복 | `isRerouting` 플래그 추가 |
| 2차 구현 | 제자리걸음 | `i=-1` → `path[-1]` IndexOutOfRange → 코루틴 종료, Walk 애니만 잔존 | `i=-1` → `i=0` 수정 |
| 3차 구현 | 이상한 이동(snap-back) | Lerp 도중 detect → `_unitData.Position`이 이전 타일 → 재경로 오류 | detect 체크를 ProcessStep 이후로 이동 |
| 4차 구현 | 적 타일로 빙 돌아감 | A* 재경로가 타일 단위로 계산 → 적이 타일 중간에 있으면 엉뚱한 타일로 경로 | **하이브리드 이동** 도입 |

#### 최종 설계 — 하이브리드 이동 (Phase 1 + Phase 2)

**핵심 아이디어:**
- 감지 시 A* 재경로 대신, **월드 좌표로 적에게 직선 이동** (Phase 1)
- 적이 감지 사거리를 벗어나면, **현재 위치에서 가장 가까운 타일 중심으로 이동** 후 A* 재개 (Phase 2)
- 타일 ↔ 월드 전환 시 도메인 위치(`_unitData.Position`)를 반드시 동기화

```
[ProcessStep 완료 후 detect 체크]
  if (HasEnemyInDetectRange && !HasEnemyInRange):
      (targetId, targetIsUnit) = FindNearestEnemyInDetectRange()
      if 유효:
          ClaimedTile = null
          break → 타일 for 루프 탈출, Phase 1 진입

[Phase 1 — 월드 좌표 직선 추적 루프]
  while (alive):
      enemyWorldPos = positionProvider.GetUnitWorldPosition(targetId)  // 매 프레임 현재 위치
      dir = (enemyWorldPos - transform.position).normalized
      transform.position += dir * moveSpeed * deltaTime

      if HasEnemyInRange:
          isInPursuit = false
          [기존 전투 루프]    ← 공격 사거리 진입 → 전투
          break
      
      if !HasEnemyInDetectRange:
          break              ← 적 감지 사거리 이탈 → Phase 2

      yield return null

[Phase 2 — 가장 가까운 타일 중심으로 이동 후 A* 재개]
  // 현재 위치(뷰 좌표) → 도메인 좌표 역변환 → 가장 가까운 타일
  Vector3 domainPos = ViewConverter.FromView(transform.position)
  HexCoord nearestTile = HexMetrics.WorldToHex(domainPos)
  
  // 타일 중심으로 Lerp 이동
  Vector3 tileCenter = ViewConverter.ToView(HexMetrics.HexToWorld(nearestTile)) + UnitYOffset
  while (transform.position != tileCenter):
      Lerp to tileCenter
      yield return null
  
  // 도메인 위치 동기화 (이 시점부터 _unitData.Position이 정확)
  _unitData.Position = nearestTile  // 또는 ProcessStep 등가 처리
  
  // 원래 목적지로 A* 재계산 + 타일 루프 재개
  path = RequestMove(_unitData, finalTarget)
  if 유효:
      i = 0; continue  // 타일 for 루프 재진입
```

**핵심 포인트:**
- Phase 1: 타일 경유 없이 매 프레임 적의 실제 월드 위치를 향해 직선 이동
- Phase 1 → 전투: 공격 사거리 진입 시 기존 전투 루프 그대로 진입
- Phase 1 → Phase 2: 적이 감지 사거리 이탈 시에만 (실제로 거의 발생하지 않음)
- Phase 2: 가장 가까운 타일 중심으로 이동 → 도메인 위치 동기화 → A* 재개
- `isRerouting` 플래그 불필요 → 제거 (Phase 1 루프가 별도 while로 분리되어 무한루프 없음)

**멀티플레이 고려:**
- `GameEvents.OnUnitEnteredCombat` 발행: 공격 사거리(AttackRange) 진입 시점 유지 (변경 없음)
- Phase 1 이동 중 Walk 애니메이션 그대로 유지

---

## 변경 불필요 파일

- `NetworkCombatController.cs` — 공격 판정은 AttackRange 기준 유지
- `HexGrid.cs` — 타일 탐색 로직 무변경
- `UnitMovementUseCase.cs` — 기존 A* 경로탐색 재사용

---

## 구현 순서

1. `UnitStats.cs` — GetDetectRange() 추가 ✅
2. `UnitData.cs` — DetectRange 프로퍼티 + 생성자 수정 ✅
3. `UnitCombatUseCase.cs` — HasEnemyInDetectRange, FindFirstEnemyInDetectRange, FindNearestEnemyPositionInDetectRange 추가 ✅
4. `UnitSpawnUseCase.cs` — UnitData 생성자에 DetectRange 전달 ✅
5. `UnitCombatUseCase.cs` — `MeleeDetectDist = HexMetrics.TileHeight` → `0.866f` 리터럴로 수정 ✅ (CS0133 에러 수정)
6. `UnitView.cs` — `isRerouting` 플래그 방식으로 재경로 로직 수정 (무한루프 버그 수정) ✅
7. `UnitView.cs` — `i = -1` → `i = 0` 수정 (제자리걸음 버그 수정) ✅
8. `UnitView.cs` — detect 체크를 Lerp while 내부 → ProcessStep 완료 후로 이동 (snap-back 버그 수정) ✅
9. `UnitCombatUseCase.cs` — `FindNearestEnemyInDetectRange()` 추가 (추적 대상 ID 반환) ✅
10. `UnitView.cs` — 하이브리드 이동 시스템 구현 (Phase 1: 월드 좌표 직선 추적 / Phase 2: 타일 복귀 후 A\* 재개) ✅

---

## 테스트 결과 (2026-04-24)

- 감지 후 이동(Phase 1 직선 추적): **동작 확인 ✅**
- 회전값 개선 필요: Phase 1(월드 좌표 직선 추적) 중 유닛 회전이 자연스럽지 않음
  → **후속 작업으로 분리** (현재 작업 범위 외)

---

## 위험 요소 및 주의사항

| 위험 요소 | 상세 | 대응 |
|---------|------|------|
| 원거리유닛 동작 변화 | DetectRange = AttackRange이면 기존 동일 | GetDetectRange()에서 원거리는 AttackRange 반환 |
| 멀티플레이 감지 이벤트 타이밍 | OnUnitEnteredCombat 발행 시점이 달라질 수 있음 | 공격 사거리 진입 시점으로 맞춤 |
| 재경로 무한루프 | `i=0` 재시작 후 또 detect → 또 재경로 | `isRerouting` 플래그로 재경로 중 detect 체크 차단 |
| 재경로 후 원래 경로 소실 | path가 교체되어 원래 랠리포인트 경로가 사라짐 | 재경로 경로는 짧음(인접 타일), 전투 후 MoveAlongPath 재호출로 재경로 |
| UnitData 생성자 변경 | detectRange 파라미터 추가 → UnitSpawnUseCase도 수정 필요 | UnitSpawnUseCase에서 UnitStats.GetDetectRange(type) 전달 ✅ |
| HexCoord 폴백 | FindFirstEnemyTargetByHexCoord는 이미 threshold=1 → DetectRange와 일치 | 폴백은 별도 수정 불필요 |
