# Plan: Phase 1 타겟 재선택 — 뒷무빙 현상 수정

**날짜**: 2026-04-26  
**작업 ID**: 15_00_phase1-target-reselect  
**담당**: game-programmer 에이전트  
**관련 Research**: Research.md

---

## 목표

1. Phase 1 루프에서 현재 타겟이 사망했을 때, 감지 사거리 내 다른 적이 있으면 **Phase 2(스냅)를 거치지 않고 즉시 새 타겟으로 전환**한다.
2. Phase 2에 진입하더라도 `nearestTile`이 뒤쪽 타일이면 **앞쪽(현재 도메인 위치)으로 경로를 시작**하여 뒷무빙을 차단한다.

---

## 변경 파일 목록

| 파일 | 변경 유형 |
|------|----------|
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | Phase 1 2개 위치 + Phase 2 1개 위치 수정 |

---

## Step 1 — Phase 1 while 루프 상단: 타겟 사망 시 재선택

**위치**: Phase 1 while 루프 최상단, `enemyViewPos == Vector3.zero` 처리 블록  
(현재 코드 약 1015~1017번 줄)

**변경 전**:
```csharp
// provider가 Vector3.zero를 반환 = 대상 파괴 또는 미등록 → Phase 2로.
if (enemyViewPos == Vector3.zero)
    break;
```

**변경 후**:
```csharp
// provider가 Vector3.zero를 반환 = 대상 파괴 또는 미등록.
if (enemyViewPos == Vector3.zero)
{
    // [5차 개선 — 2026-04-26] 타겟 사망 시 즉시 다음 적 재선택.
    // 기존에는 무조건 break → Phase 2(스냅 + 재경로)로 빠졌다.
    // 그 결과 유닛이 처음 감지 타일 근처로 스냅되어 "뒷무빙"처럼 보였다.
    // 감지 사거리 내에 다른 적이 있으면 Phase 2를 건너뛰고 바로 새 타겟을 추적한다.
    if (_combatUseCase != null && _combatUseCase.HasEnemyInDetectRange(_unitData))
    {
        var nextTarget = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
        if (nextTarget.HasValue)
        {
            // 새 타겟으로 전환 후 루프 상단으로 — Phase 2 스냅 없음.
            targetId = nextTarget.Value.id;
            targetIsUnit = nextTarget.Value.isUnit;
            continue;
        }
    }
    // 감지 사거리 내 적 없음 → 기존과 동일하게 Phase 2로 이탈.
    break;
}
```

---

## Step 2 — Phase 1 전투 블록 끝: continue 전 타겟 재선택

**위치**: Phase 1의 전투(HasEnemyInRange) 블록 마지막 부분  
(현재 코드 약 1088~1093번 줄)

**변경 전**:
```csharp
// 전투 후: 감지 사거리 밖 = 적 이탈 → Phase 2로.
if (!_combatUseCase.HasEnemyInDetectRange(_unitData))
    break;

// 감지 사거리 안에 다른 적이 있는 경우 계속 추적 루프로.
continue;
```

**변경 후**:
```csharp
// 전투 후: 감지 사거리 밖 → Phase 2로.
if (!_combatUseCase.HasEnemyInDetectRange(_unitData))
    break;

// [5차 개선 — 2026-04-26] 감지 사거리 내 다음 적 재선택 후 Phase 1 재개.
// 기존 continue는 죽은 targetId로 루프 상단을 다시 돌았고,
// enemyViewPos == zero → 즉시 break(Phase 2) 되는 문제가 있었다.
// 여기서 명시적으로 새 타겟을 선택하여 이 문제를 차단한다.
var nextEnemy = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
if (!nextEnemy.HasValue)
    break;  // 감지 사거리 내 타겟 없음 → Phase 2로

targetId = nextEnemy.Value.id;
targetIsUnit = nextEnemy.Value.isUnit;
continue;  // 새 타겟으로 Phase 1 루프 재개
```

---

## Step 3 — Phase 2: nearestTile 후방 스냅 방지 + 점유 누수 방지

**위치**: Phase 2 `nearestTile` 결정 직후 (현재 코드 약 1143~1148번 줄 인근)

**변경 전**:
```csharp
// Phase 2: 현재 위치에서 가장 가까운 타일로 스냅
HexCoord nearestTile = HexMetrics.WorldToHex(domainPos);

// 점유 이동 등록 후 새 경로 계산
_movementUseCase.RegisterOccupancyMove(_unitData.Position, nearestTile, _unitData.Type);
```

**변경 후**:
```csharp
// Phase 2: 현재 위치에서 가장 가까운 타일로 스냅
HexCoord nearestTile = HexMetrics.WorldToHex(domainPos);

// [5차 개선 — 2026-04-26] nearestTile이 현재 도메인 위치보다 finalTarget에서 더 멀면(뒤쪽),
// 스냅 자체가 뒤로 이동하는 것처럼 보이는 현상을 유발한다.
// 이 경우 nearestTile 대신 현재 도메인 위치(_unitData.Position)를 유지한다.
int nearestDistToFinal = HexCoord.Distance(nearestTile, finalTarget);
int domainDistToFinal  = HexCoord.Distance(_unitData.Position, finalTarget);
if (nearestDistToFinal > domainDistToFinal)
{
    // nearestTile이 더 멀다 = 뒷쪽 타일 → 앞쪽(도메인 위치)으로 대체
    nearestTile = _unitData.Position;
}

// nearestTile == _unitData.Position일 때 RegisterOccupancyMove를 호출하면
// 4차 개선 구조상 ReserveOccupancy(같은 타일)+1만 발생하고
// ProcessStep의 from==to 조건에서 FROM-1을 건너뛰어 점유 누수가 생긴다.
// 같은 타일이면 호출 자체를 생략하여 누수를 방지한다.
if (nearestTile != _unitData.Position && _movementUseCase != null)
{
    _movementUseCase.RegisterOccupancyMove(_unitData.Position, nearestTile, _unitData.Type);
}
```

---

## 변경 위치 확인 방법

UnitView.cs에서 다음 키워드로 검색:
1. `enemyViewPos == Vector3.zero` → Step 1 위치
2. `HasEnemyInDetectRange` + `continue` 인근 → Step 2 위치
3. `HexMetrics.WorldToHex(domainPos)` 또는 `nearestTile` + `RegisterOccupancyMove` 인근 → Step 3 위치

Step 1, 2는 `if (_combatUseCase != null && _positionProvider != null)` 블록 내부의  
`while (_unitData.IsAlive)` Phase 1 루프 안에 있다.  
Step 3는 Phase 1 루프 종료 직후 Phase 2 블록 내부에 있다.

---

## 엣지 케이스 검증

| 케이스 | 동작 |
|--------|------|
| 타겟 사망, 감지 사거리 내 다음 적 없음 | `HasEnemyInDetectRange = false` → break → Phase 2. 기존과 동일. ✓ |
| 타겟 사망, 다음 적 있음 | `FindNearestEnemyInDetectRange` → 새 targetId → continue. Phase 2 없음. ✓ |
| 전투 후 다음 적 있으나 FindNearest = null | break → Phase 2. 안전 분기. ✓ |
| Phase 1 외부(positionProvider null) | Phase 1 전체 스킵 → 기존 Phase 2 동작. 변경 없음. ✓ |
| 유닛 사망 중 | `_unitData.IsAlive` 체크로 루프 종료. 변경 없음. ✓ |
| Phase 2: nearestTile == _unitData.Position | RegisterOccupancyMove 생략 → 점유 누수 없음. ✓ |
| Phase 2: nearestTile이 finalTarget보다 가깝거나 같음(앞쪽) | 대체 없음 → 기존 스냅 동작. ✓ |
| Phase 2: nearestTile이 finalTarget보다 멂(뒷쪽) | _unitData.Position으로 대체 → 뒷무빙 없음. ✓ |
| Phase 2: finalTarget이 default(invalid) | HexCoord.Distance는 0 반환 → domainDist도 0 → 조건 불성립 → nearestTile 유지. 안전. ✓ |

---

## 구현 후 검증 체크리스트 (Testcase.md 기준)

1. 적 처치 후 주변에 다른 적이 있을 때 즉시 새 적을 향해 이동하는가
2. 적 처치 후 주변 적 없을 때 Phase 2 스냅 + 재경로가 정상 작동하는가
3. 뒷무빙(처음 감지 타일로 복귀) 현상이 사라졌는가
4. 모든 적이 제거된 뒤 Phase 2 진입 시 유닛이 뒤가 아닌 앞(finalTarget 방향) 타일로 스냅하는가
5. 4차 개선 점유 시스템 이상 없음 — 같은 타일에 과도하게 유닛이 겹치는 퇴행이 없는가
