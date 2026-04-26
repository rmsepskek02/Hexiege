# Research: Phase 1 타겟 재선택 — 뒷무빙 현상 수정

**날짜**: 2026-04-26  
**작업 ID**: 15_00_phase1-target-reselect  
**담당**: game-programmer 에이전트

---

## 1. 배경

4차 개선(occupancy-from-fix) 실기 테스트 중 발견된 현상:  
**적 유닛을 처치한 이후, 주변의 다른 적을 바로 공격하지 않고  
처음 적을 감지했던 타일 방향으로 잠시 뒤로 이동(뒷무빙)한 뒤에야 다음 목적지로 향한다.**

---

## 2. 이동 Phase 구조 요약

유닛의 이동은 외부 while 루프 안에서 3개 Phase로 구성된다:

| Phase | 설명 |
|-------|------|
| Phase 0 | 타일→타일 Lerp 이동. 경로 배열을 순회하며 한 타일씩 이동. |
| Phase 1 | 월드 좌표 직선 추적. 감지 사거리 내 적을 향해 자유롭게 이동. |
| Phase 2 | 가장 가까운 타일 중심으로 스냅 + A* 재경로 계산. Phase 0 재개. |

Phase 1은 Phase 0의 Lerp 완료 후 감지 사거리 내 적이 있을 때, 또는 Lerp 도중 `interruptedByDetect`로 진입.  
Phase 1 종료 후 항상 Phase 2(스냅)를 거쳐 Phase 0으로 복귀.

---

## 3. 근본 원인

### 문제 지점 A — Phase 1: 타겟 사망 시 무조건 Phase 2로 이탈

**파일**: `Presentation/Unit/UnitView.cs`, Phase 1 while 루프 상단 (약 1010~1017번 줄)

```csharp
// 매 프레임 추적 대상의 최신 월드 좌표를 조회
Vector3 enemyViewPos = targetIsUnit
    ? _positionProvider.GetUnitWorldPosition(targetId)
    : _positionProvider.GetBuildingWorldPosition(targetId);

// provider가 Vector3.zero를 반환 = 대상 파괴 또는 미등록 → Phase 2로.
if (enemyViewPos == Vector3.zero)
    break;  // ← 여기서 무조건 Phase 2로 빠짐
```

`break` 시점에 Phase 1의 while 루프가 종료되고 Phase 2(스냅)가 실행된다.  
Phase 2에서 `nearestTile = WorldToHex(현재 월드 좌표)` → 스냅 → `RequestMove` 재계산.

**문제**: 현재 타겟이 사망해도 감지 사거리 안에 **다른 적이 여전히 있을 수 있다**.  
이 경우에도 무조건 Phase 2를 거쳐 "처음 감지 타일" 근처로 스냅한 뒤, 새 경로로 이동 재개.  
→ 플레이어 시점에서 유닛이 "뒤로 갔다가 다시 앞으로 가는" 동작으로 보인다.

### 문제 지점 B — Phase 1 전투 후 `continue` 시 stale targetId 사용

**파일**: `Presentation/Unit/UnitView.cs`, Phase 1 전투 블록 끝 (약 1088~1093번 줄)

```csharp
// 전투 후: 감지 사거리 밖 = 적 이탈 → Phase 2로.
if (!_combatUseCase.HasEnemyInDetectRange(_unitData))
    break;

// 감지 사거리 안에 다른 적이 있는 경우 계속 추적 루프로.
continue;  // ← stale targetId로 루프 상단으로 복귀
```

`continue` 후 루프 상단에서 **죽은 targetId**의 위치를 조회 → `enemyViewPos == Vector3.zero` → 즉시 break.  
감지 사거리 안에 다른 적이 있어도 Phase 2로 빠지는 문제.

---

## 4. 뒷무빙 시나리오 (상세)

1. 유닛이 Phase 0 Lerp 중, 또는 Phase 0 스텝 완료 후 적을 감지 → Phase 1 진입
2. **감지 당시 유닛의 타일 = "감지 타일"** (Phase 2 스냅의 기준이 되는 영역)
3. Phase 1: 추적 대상(적 A)을 향해 이동 → 공격 → 적 A 사망
4. `enemyViewPos == Vector3.zero` → break → Phase 2 진입
5. `nearestTile = WorldToHex(현재위치)` ≈ 감지 타일 또는 그 인근
6. ProcessStep + `RequestMove(unit, finalTarget)` → 새 경로 시작
7. **새 경로의 시작이 "감지 타일"** → 유닛이 그 위치에서 다시 앞으로 이동 시작
8. 플레이어 시점: "유닛이 뒤로 갔다가 다시 앞으로"

---

## 5. 수정 방향

### 공통 원칙
두 가지 경로로 Phase 2에 진입하는 빈도를 줄이고, Phase 2에 진입하더라도 뒤로 이동하지 않도록 한다.

### Phase 1 while 루프 상단 수정 (문제 지점 A)

타겟이 사망했을 때(`enemyViewPos == Vector3.zero`), 무조건 break하기 전에  
감지 사거리 내 다음 적을 재선택. 있으면 `targetId/targetIsUnit` 교체 후 `continue`.

```csharp
if (enemyViewPos == Vector3.zero)
{
    // 현재 타겟 사망 — 감지 사거리 안에 다음 적이 있으면 즉시 재선택.
    if (_combatUseCase.HasEnemyInDetectRange(_unitData))
    {
        var nextTarget = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
        if (nextTarget.HasValue)
        {
            targetId = nextTarget.Value.id;
            targetIsUnit = nextTarget.Value.isUnit;
            continue;  // 새 타겟으로 Phase 1 재개 (Phase 2 스냅 없음)
        }
    }
    break;  // 감지 사거리 내 적 없음 → Phase 2로
}
```

### Phase 1 전투 후 재진입 수정 (문제 지점 B)

전투 종료 후 다른 적이 있어 `continue`할 때, 죽은 targetId를 유지하지 않고 가장 가까운 적으로 재선택.

```csharp
// 감지 사거리 밖 = 이동 재개할 적 없음 → Phase 2로
if (!_combatUseCase.HasEnemyInDetectRange(_unitData))
    break;

// 감지 사거리 안 다른 적 재선택 — 기존 targetId는 사망했을 수 있음
var nextEnemy = _combatUseCase.FindNearestEnemyInDetectRange(_unitData);
if (!nextEnemy.HasValue)
    break;  // FindNearest가 null이면 더 이상 타겟 없음

targetId = nextEnemy.Value.id;
targetIsUnit = nextEnemy.Value.isUnit;
continue;  // 새 타겟으로 Phase 1 루프 재개
```

### Phase 2 후방 스냅 방지 (문제 지점 C)

Phase 1이 종료되어 Phase 2에 진입할 때, `nearestTile`이 **현재 도메인 위치(`_unitData.Position`)보다 `finalTarget`에서 더 멀면(뒤쪽 타일)** 스냅 자체가 뒷무빙을 유발한다.

예시:
- 유닛이 Phase 0 Lerp 도중 감지(`interruptedByDetect`) → Phase 1 진입 (현재 위치 ≈ 이전 타일 A)
- 적이 A 바로 옆에서 사망, 유닛이 거의 이동 못 한 채 Phase 2 진입
- `nearestTile = A = _unitData.Position` → 스냅 없음이 맞으나, 점유 등록 시 같은 타일에 +1 누수 발생
- 또는 유닛이 A와 B 사이 t=0.3에서 감지 → Phase 1 → 적이 A 방향에서 사망 → `nearestTile = A` (A가 더 가까움) → A로 스냅 = 뒷무빙

**수정**: Phase 2 시작 시 `HexCoord.Distance(nearestTile, finalTarget)`와 `HexCoord.Distance(_unitData.Position, finalTarget)`을 비교하여, nearestTile이 더 멀면 `_unitData.Position`으로 대체한다.  
또한 nearestTile이 `_unitData.Position`과 같을 때 `RegisterOccupancyMove`를 호출하면 4차 개선 구조상 `ReserveOccupancy(same_tile)+1` 누수가 발생하므로, 같은 타일이면 호출을 건너뛴다.

---

## 6. 수정 대상 파일

| 파일 | 변경 위치 |
|------|----------|
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | Phase 1 while 루프 상단 (enemyViewPos == zero 처리) |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | Phase 1 전투 후 continue 처리 (1088~1093번 줄 인근) |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | Phase 2 nearestTile 결정 직후 (후방 스냅 방지 + 동일 타일 점유 누수 방지) |

---

## 7. 변경하지 않는 것

- Phase 0 Lerp 내부 전투 처리 — 변경 없음
- Phase 2 스냅 로직 — 변경 없음 (감지 사거리 내 적 없을 때는 Phase 2가 여전히 필요)
- `HasEnemyInDetectRange`, `FindNearestEnemyInDetectRange` 구현 — 변경 없음

---

## 8. 부가 효과 및 주의사항

**긍정적 효과**:
- 적 처치 후 다음 적을 바로 추적 → 전투 흐름이 자연스러워짐
- Phase 2 스냅 빈도 감소 → 불필요한 "타일 중심 복귀" 줄어듦

**주의사항**:
- `HasEnemyInDetectRange`는 `_unitData.Position`(마지막 ProcessStep 타일)을 기반으로 판정.  
  Phase 1에서는 도메인 Position이 업데이트되지 않으므로, 실제 물리 위치와 ±1타일 오차 가능.  
  → 이 오차는 기존 Phase 1에서도 동일하게 존재하는 허용된 구조이며, 이번 수정에서 새로 생기는 것이 아님.
- 근접 유닛 뭉침 현상은 이 수정으로 완전히 해결되지 않음.  
  근접 유닛이 같은 타겟을 향해 모두 모이는 것은 구조적 특성. 별도 개선 필요.
