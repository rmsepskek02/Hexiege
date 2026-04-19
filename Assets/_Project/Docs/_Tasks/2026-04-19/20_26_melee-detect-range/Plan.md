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

### 4. `Presentation/Unit/UnitView.cs` — MoveAlongPath 재경로 로직 수정

**변경 위치:** Lerp while 블록 내부 (라인 522~685 구간)

#### 버그 원인 (1차 구현의 무한루프)

1차 구현: detect → `path 교체` + `i=0; continue` → 새 경로 첫 스텝에서 또 detect → 또 `i=0` → **무한루프**

근본 원인: `i=0`으로 재시작해도 적이 여전히 detect range 내이므로 즉시 또 재경로 발생.

#### 수정 설계 — `isRerouting` 플래그

```
bool isRerouting = false  // for loop 바깥에 선언 (스텝 간 유지)
bool rerouteTriggered = false  // 이번 스텝에서 재경로 발생 여부

[Lerp while 내부]
  if (!isRerouting && HasEnemyInDetectRange && !HasEnemyInRange):
      reroutePath 계산
      if 유효:
          path = reroutePath
          isRerouting = true       ← 재경로 플래그 ON
          rerouteTriggered = true
          break

  if (HasEnemyInRange):
      isRerouting = false          ← 공격 사거리 진입 = 재경로 목적 달성
      [기존 전투 루프 그대로]

[Lerp while 이후]
  if (rerouteTriggered):
      i = -1   ← for loop i++ 후 i=0 → 새 경로 첫 스텝 실행
      continue
      // isRerouting=true 유지 → 첫 스텝에서 또 재경로 발생하지 않음
```

**핵심 차이:**
- `i=0` → `i=-1`: 의미 동일하지만 명시적으로 "i++에 의해 0이 됨"을 표현
- `isRerouting=true` 유지: 새 경로 실행 중에는 detect 체크 건너뜀 → 무한루프 차단
- 공격 사거리 진입 시 `isRerouting=false` 해제 → 이후 전투 종료 후 다시 detect 가능

**멀티플레이 고려:**
- `GameEvents.OnUnitEnteredCombat` 발행: 감지(DetectRange) 시점이 아닌 공격 사거리(AttackRange) 진입 시점 유지
- 재경로 이동 중 Walk 애니메이션 그대로 유지

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
6. `UnitView.cs` — `isRerouting` 플래그 방식으로 재경로 로직 수정 (무한루프 버그 수정) ← **현재 진행 중**

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
