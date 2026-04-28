# Plan: Phase 1 전투 후 Phase 2 후방 스냅 오작동 수정

**날짜**: 2026-04-27  
**작업 ID**: 01_17_phase2-backward-snap-fix  
**담당**: game-programmer 에이전트  
**Research**: [Research.md](Research.md)

---

## 이 작업이 하려는 것 (자연어 설명)

근접 유닛의 이동·전투 로직은 아래와 같이 동작해야 한다.

1. A* 알고리즘으로 타일에서 타일로 이동하며 목표는 적의 성을 향한다.
2. 적 감지 사거리 내에서 적을 감지하는 경우 월드 좌표 기준으로 적을 향해 직선 이동한다.
3. 12방향 각도 기반 슬롯 규칙에 의거하여 분산되며 접근한다.
4. 해당 적이 제거되었을 경우 현재 위치에서 적 감지 사거리 내에 있는 적을 탐지한다.
   - **4-1.** 적이 있는 경우: 그 적을 타겟으로 삼아 2번과 같이 이동한다.
   - **4-2.** 적이 없는 경우: 현재 월드 좌표에서 **뒤로 이동하지 않고** 앞으로 향하며, 다시 1번의 알고리즘대로 적의 성을 향해 나아간다.

현재 4-2번이 지켜지지 않아 유닛이 이전 타일로 뒷무빙하거나 앞뒤로 왔다갔다하는 현상이 발생한다. 이번 수정으로 4-2번을 올바르게 동작시킨다.

---

## 원인 요약

### 원인 1 — Phase 2 후방 강제 스냅 (Step 1으로 수정 완료)

Phase 1이 끝나고 Phase 2에 진입할 때 "15_00 5차 개선"이 발동하면서 문제가 생겼다.

이 로직은 Phase 2에서 `nearestTile`(불정령 현재 위치에서 가장 가까운 타일)이 `finalTarget`(성) 기준으로 `_unitData.Position`(Phase 1 시작 타일 T0)보다 멀면, 강제로 `_unitData.Position = T0`으로 교체한다.

15_00 당시에는 Phase 1에서 불정령이 슬롯 위치에서 멈췄기 때문에 `nearestTile ≈ T0`이어서 이 로직이 거의 발동하지 않았다. 18_30 이후에는 불정령이 전투 거리까지 전진하므로 `nearestTile`이 적의 타일이 되고, 그 적이 성 방향에서 어긋난 위치에 있으면 5차 개선이 매번 발동해 T0으로 강제 후방 스냅한다.

→ **Step 1(5차 개선 제거)으로 해결.**

### 원인 2 — 헥스 뒤쪽 대각선 적 감지 (Step 2로 수정 예정)

Step 1 적용 후에도 밀집 지역에서 잔존하는 앞뒤 왕복의 원인.

헥스 격자에서 대각선 인접 타일 거리 ≈ 0.901f는 감지 범위(0.916f) 이내다. 즉 앞쪽 대각선과 뒤쪽 대각선 모두 같은 거리에서 감지된다. `FindNearestEnemyInDetectRange`는 세계 거리 기준 가장 가까운 적을 반환하는데, 앞뒤가 동거리일 때 반복 순서에 따라 뒤쪽 적이 먼저 선택되어 불정령이 뒤로 이동하게 된다.

→ **Step 2(앞쪽 적 필터링)로 해결 예정.**

---

## 수정 내용

### 수정 파일

| 파일 | 변경 유형 |
|------|-----------|
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | **Step 1 완료** — Phase 2 5차 개선 조건 블록 제거 |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | **Step 2 예정** — Phase 0/1 뒤쪽 적 필터링 추가 |

---

## Step 1 — Phase 2 5차 개선 조건 블록 제거

**위치**: `UnitView.cs` 라인 1319-1336

**변경 전**:
```csharp
// [5차 개선 — 2026-04-26] nearestTile 후방 스냅 방지.
// Phase 1에서 직선 추적하다가 적이 죽거나 사거리를 벗어나면 Phase 2로 진입한다.
// 이때 transform.position(현재 뷰 좌표)을 가장 가까운 타일로 스냅하는데,
// 그 가장 가까운 타일(nearestTile)이 finalTarget(원래 목적지)에서 보면
// _unitData.Position(이전 타일)보다 더 멀 수 있다.
// 이 경우 그대로 스냅하면 유닛이 시각적으로 "뒤로 한 칸 물러난 뒤 다시 전진"하는
// 뒷무빙처럼 보인다.
//
// 해결: nearestTile이 finalTarget 기준으로 _unitData.Position보다 멀면(=후방이면)
// nearestTile을 _unitData.Position으로 대체한다.
// 즉, 뒤로 가는 스냅을 차단하고 현재 도메인 위치를 그대로 유지.
int nearestDistToFinal = HexCoord.Distance(nearestTile, finalTarget);
int domainDistToFinal  = HexCoord.Distance(_unitData.Position, finalTarget);
if (nearestDistToFinal > domainDistToFinal)
{
    // nearestTile이 더 멀다 = 뒷쪽 타일 → 앞쪽(도메인 위치)으로 대체
    nearestTile = _unitData.Position;
}
```

**변경 후**:
```csharp
// [2026-04-27] 5차 개선(nearestTile 후방 스냅 방지) 제거.
// 기존 5차 개선은 Phase 1에서 불정령이 슬롯 위치(적으로부터 0.866f)에서 멈추던
// 시절을 전제로 설계됐다.
// 18_30 슬롯 도달 수정 이후 불정령이 전투 거리(0.3f)까지 전진하므로,
// Phase 2 진입 시 nearestTile이 적의 타일이 된다.
// 적이 성 방향에서 어긋난 위치에 있으면 5차 개선이 T0으로 강제 후방 스냅 →
// 뒷무빙 및 앞뒤 왕복 현상의 직접 원인이었다.
//
// nearestTile(현재 위치에서 가장 가까운 타일)을 그대로 사용한다.
// 전투 후 스냅 = 전투가 일어난 위치 근처 타일 → 올바른 동작.
// 전투 없이 Phase 2 진입(적 이탈 등)의 경우 nearestTile이 성 방향에서 1홉
// 어긋날 수 있으나, Phase 0 A* 재계산으로 즉시 복구되어 시각적으로 무해하다.
```

---

## 위험 요소

| 위험 | 영향 | 대응 |
|------|------|------|
| 전투 없이 Phase 2 진입 시 nearestTile이 측면 타일로 스냅 | Phase 0에서 1홉 더 우회 가능성 | A* 재계산으로 즉시 복구. 시각적 영향 미미 |
| `nearestTile != _unitData.Position` 점유 이동 가드(라인 1390) | 변경 없음 — 유지 | 이 조건은 점유 누수 방지용으로 별개 로직. 그대로 둠 |
| Step 2: 뒤쪽 적 필터링으로 일부 적을 건너뛸 수 있음 | 뒤쪽 적이 동거리일 때 앞쪽 적을 먼저 선택하므로 1스텝 뒤에 감지 | Phase 0이 한 스텝 더 전진 후 감지. 전투 지연 1스텝 수준으로 무해 |
| Step 2: 앞뒤가 정확히 동일 Castle-distance일 때 | 동거리 적은 앞쪽으로 간주(≤ 조건) — 포함됨 | 정상 동작 |

---

## 아키텍처 제약

- `UnitView.cs` 단독 수정. 다른 파일 변경 없음.
- 점유 이동 가드(`nearestTile != _unitData.Position`, 라인 1390)는 5차 개선과 별개 로직이므로 반드시 유지.
- Step 2 필터링은 `UnitView.cs` 내부에서 직접 처리 — `UnitCombatUseCase.cs` 변경 없음.

---

## Step 2 — Phase 0/1 뒤쪽 적 추적 차단 (2026-04-28 추가)

**위치 1**: `UnitView.cs` — Phase 0 Lerp 중 감지 체크 (라인 811-817)

**변경 전**:
```csharp
if (_combatUseCase != null && _unitData.IsAlive
    && _combatUseCase.HasEnemyInDetectRange(_unitData)
    && !_combatUseCase.HasEnemyInRange(_unitData))
{
    interruptedByDetect = true;
    break;
}
```

**변경 후**:
```csharp
if (_combatUseCase != null && _unitData.IsAlive
    && _combatUseCase.HasEnemyInDetectRange(_unitData)
    && !_combatUseCase.HasEnemyInRange(_unitData))
{
    // [6차 개선 — 2026-04-28] 뒤쪽 적 감지 무시.
    // 헥스 대각선 인접 타일(≈0.901f)은 앞쪽·뒤쪽 모두 감지 범위 이내라서
    // 뒤쪽 대각선에 있는 적이 먼저 선택되어 불정령이 뒤로 쫓아가는 현상이 발생한다.
    // 감지된 적이 성 기준으로 불정령 현재 위치보다 뒤에 있으면 Phase 1 진입을 건너뛴다.
    HexCoord? nearestCoord = _combatUseCase.FindNearestEnemyPositionInDetectRange(_unitData);
    HexCoord spiritTile = HexMetrics.WorldToHex(ViewConverter.FromView(transform.position));
    bool enemyIsForward = nearestCoord.HasValue &&
        HexCoord.Distance(nearestCoord.Value, finalTarget)
        <= HexCoord.Distance(spiritTile, finalTarget);
    if (enemyIsForward)
    {
        interruptedByDetect = true;
        break;
    }
}
```

---

**위치 2**: `UnitView.cs` — Phase 0 스텝 완료 후 감지 체크 (라인 992-1004)

**변경 전**:
```csharp
if (_combatUseCase != null && _unitData.IsAlive
    && _combatUseCase.HasEnemyInDetectRange(_unitData)
    && !_combatUseCase.HasEnemyInRange(_unitData))
{
    _unitData.ClaimedTile = null;
    ReleaseSlotIfClaimed();
    shouldPursue = true;
    break;
}
```

**변경 후**:
```csharp
if (_combatUseCase != null && _unitData.IsAlive
    && _combatUseCase.HasEnemyInDetectRange(_unitData)
    && !_combatUseCase.HasEnemyInRange(_unitData))
{
    // [6차 개선 — 2026-04-28] 뒤쪽 적 감지 무시.
    // to(현재 도착한 타일)를 기준으로 적이 성 방향인지 판단한다.
    HexCoord? nearestCoord = _combatUseCase.FindNearestEnemyPositionInDetectRange(_unitData);
    bool enemyIsForward = nearestCoord.HasValue &&
        HexCoord.Distance(nearestCoord.Value, finalTarget)
        <= HexCoord.Distance(to, finalTarget);
    if (enemyIsForward)
    {
        _unitData.ClaimedTile = null;
        ReleaseSlotIfClaimed();
        shouldPursue = true;
        break;
    }
}
```

---

**위치 3**: `UnitView.cs` — Phase 1 최초 타겟 선택 (라인 1042 인근)

**변경 전**:
```csharp
if (_combatUseCase != null && _positionProvider != null)
{
    var pursuitTarget = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
    if (pursuitTarget.HasValue)
    {
        int targetId = pursuitTarget.Value.id;
        // ... Phase 1 루프 전체 ...
    }
}
```

**변경 후**:
```csharp
if (_combatUseCase != null && _positionProvider != null)
{
    var pursuitTarget = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
    if (pursuitTarget.HasValue)
    {
        // [6차 개선 — 2026-04-28] 뒤쪽 적 필터링.
        // shouldPursue가 true로 Phase 1에 진입했더라도,
        // 감지된 적이 불정령 현재 위치보다 성에서 멀면(뒤쪽) Phase 1을 건너뛴다.
        // → Phase 2로 넘어가 현재 위치 기준 A* 재계산.
        HexCoord? initialCoord = _combatUseCase.FindNearestEnemyPositionInDetectRange(_unitData);
        HexCoord spiritTileNow = HexMetrics.WorldToHex(ViewConverter.FromView(transform.position));
        bool initialEnemyForward = initialCoord.HasValue &&
            HexCoord.Distance(initialCoord.Value, finalTarget)
            <= HexCoord.Distance(spiritTileNow, finalTarget);

        if (initialEnemyForward)
        {
            int targetId = pursuitTarget.Value.id;
            // ... Phase 1 루프 전체 (기존과 동일) ...
        }
    }
}
```

---

**위치 4**: `UnitView.cs` — Phase 1 타겟 사망 재선택 (라인 1088-1112)

```csharp
if (_combatUseCase != null && _combatUseCase.HasEnemyInDetectRange(_unitData))
{
    var nextTarget = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
    if (nextTarget.HasValue)
    {
        // [6차 개선 — 2026-04-28] 뒤쪽 적 재선택 방지.
        HexCoord? nextCoord = _combatUseCase.FindNearestEnemyPositionInDetectRange(_unitData);
        HexCoord spiritNow = HexMetrics.WorldToHex(ViewConverter.FromView(transform.position));
        if (nextCoord.HasValue &&
            HexCoord.Distance(nextCoord.Value, finalTarget) > HexCoord.Distance(spiritNow, finalTarget))
        {
            // 뒤쪽 적만 남음 → Phase 2로 이탈
            ReleaseAttackSlotIfClaimed();
            break;
        }

        ReleaseAttackSlotIfClaimed();
        targetId = nextTarget.Value.id;
        targetIsUnit = nextTarget.Value.isUnit;
        // ... 슬롯 재배정 + continue (기존과 동일) ...
    }
}
```

---

**위치 5**: `UnitView.cs` — Phase 1 전투 종료 재선택 (라인 1204-1225)

```csharp
var nextEnemy = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
if (!nextEnemy.HasValue)
{
    ReleaseAttackSlotIfClaimed();
    break;
}

// [6차 개선 — 2026-04-28] 뒤쪽 적 재선택 방지.
HexCoord? nextEnemyCoord = _combatUseCase.FindNearestEnemyPositionInDetectRange(_unitData);
HexCoord spiritCurrent = HexMetrics.WorldToHex(ViewConverter.FromView(transform.position));
if (nextEnemyCoord.HasValue &&
    HexCoord.Distance(nextEnemyCoord.Value, finalTarget) > HexCoord.Distance(spiritCurrent, finalTarget))
{
    // 뒤쪽 적만 남음 → Phase 2로 이탈
    ReleaseAttackSlotIfClaimed();
    break;
}

ReleaseAttackSlotIfClaimed();
targetId = nextEnemy.Value.id;
// ... 슬롯 재배정 + continue (기존과 동일) ...
```

---

## Step 3 — 18슬롯(타일 기반) → 12방향 각도 기반 슬롯으로 교체 (2026-04-28 추가)

### 배경

Step 1, 2 적용 후에도 잔존하는 앞뒤 왕복 현상의 원인은 슬롯 배정 방식 자체에 있다.  
현재 `AttackPositionManager`는 공격 목표 인접 타일 중심을 슬롯으로 사용하고, 불정령의 현재 위치에 가장 가까운 슬롯을 배정한다. 불정령이 전진한 후 새 타겟이 생기면, 새 타겟의 "가장 가까운 슬롯"이 불정령의 출발 타일(현재 위치 뒤쪽)이 되는 경우가 발생한다.

근본 해결은 **슬롯 위치를 항상 공격 목표 근처에 두는 것**이다. 그러면 불정령은 어떤 슬롯을 배정받아도 앞으로 이동하게 된다.

### 변경 설계

**18슬롯(타일 중심 기반)** → **12방향 각도 기반(공격 목표 중심)**으로 교체한다.

- 공격 목표를 중심으로 360°를 12등분한다 (0°, 30°, 60°, …, 330°).
- 각 방향에 대해 `이동 목표 위치 = 타겟 위치 + 방향벡터 × 전투 거리(contactDistance)`를 계산한다.
- 각 방향을 "슬롯"으로 간주하여 불정령 ID와 1:1 매핑한다. 이미 점유된 방향은 건너뛴다.
- **슬롯 선택 기준**: 불정령의 현재 위치에서 슬롯 위치까지 거리가 가장 짧은 것을 선택한다.  
  (슬롯 위치가 모두 타겟 근처이므로, 가장 가까운 방향 = 불정령이 바라보는 방향과 가장 가깝다는 의미)

**핵심 변화**: 슬롯 위치가 항상 타겟 위치 근처(전투 거리)이기 때문에, 불정령의 현재 위치가 어디에 있더라도 배정된 슬롯은 항상 앞쪽(타겟 방향)에 있다. 후방 슬롯 배정이 구조적으로 불가능해진다.

### 2단계 이동 제거

기존 Phase 1 이동 구조:
```
① 배정된 슬롯 위치까지 이동 (타겟에서 0.866f 떨어진 타일 중심)
② 슬롯 도달(0.15f) 후 enemyViewPos(타겟 월드 좌표)로 직접 이동
```

새 구조:
```
① 배정된 슬롯 위치(타겟 중심 각도 기반, contact_distance 반경)로 이동
   → 슬롯 위치 자체가 이미 전투 거리이므로 도달 즉시 전투 가능
   (slotAssigned / _currentAttackPos 개념 단순화 또는 제거 가능)
```

슬롯 위치가 곧 전투 위치이기 때문에 별도의 2단계 이동이 불필요하다.

### 수정 파일 및 변경 내용

| 파일 | 변경 내용 |
|------|-----------|
| `Assets/_Project/Scripts/Application/Services/AttackPositionManager.cs` | 슬롯 생성 방식 전면 교체: 타일 인접 중심 → 12방향 각도 기반 위치. `ClaimAttackSlot` 반환값이 월드 좌표(Vector3)인 구조는 유지 |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | Phase 1 이동 목표를 `_currentAttackPos`(슬롯 위치) 단일 목표로 단순화. 슬롯 도달 후 `enemyViewPos`로 전환하는 2단계 이동 로직 제거 |

### 변경 전/후 비교 (Phase 1 이동 목표 결정 부분)

**변경 전** (`UnitView.cs:1381-1383`):
```csharp
// slotAssigned가 true이면 슬롯 위치로, false이면 타겟으로 직접 이동
Vector3 moveTarget = slotAssigned ? _currentAttackPos : enemyViewPos;
```

**변경 후**:
```csharp
// 슬롯 위치 자체가 이미 전투 거리이므로 항상 슬롯 위치로 이동
// (슬롯 도달 = 전투 가능 위치 도달)
Vector3 moveTarget = _currentAttackPos;
```

### 위험 요소

| 위험 | 영향 | 대응 |
|------|------|------|
| 각도 기반 슬롯 위치가 벽/장애물과 겹칠 수 있음 | 불정령이 이동 불가 위치로 이동 시도 | Phase 1은 월드 좌표 직선 이동이므로 타일 벽과 무관. 단, 타겟 근처에서 막히는 시각적 문제 가능 |
| 기존 18슬롯 점유 해제 로직(`ReleaseAttackSlotIfClaimed`) 과 인터페이스 불일치 | 슬롯 해제가 정상 동작하지 않으면 누수 발생 | 기존 슬롯 해제 인터페이스 유지. 내부 구현만 각도 기반으로 교체 |
| 12방향이 18방향보다 적어 밀집 시 슬롯 포화 가능 | 슬롯 포화 시 마지막 슬롯이 배정 | 현재 18슬롯도 실제로는 인접 타일 수 기반이라 6~12개 수준. 12방향도 충분하며, 포화 시 동작은 기존과 동일 수준 |
| `contactDistance` 값 결정 | 너무 작으면 겹침, 너무 크면 전투 불가 | 기존 전투 사거리(0.3f) 또는 슬롯 도달 임계값과 동일하게 설정. 테스트로 검증 필요 |
