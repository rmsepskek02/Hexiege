# Plan: 근접 유닛 과도 뭉침 개선 (포위 위치 할당)

**날짜**: 2026-04-26 (버그 반영 업데이트: 2026-04-27)
**작업 ID**: 18_30_melee-spread  
**담당**: game-programmer 에이전트  
**Research**: [Research.md](Research.md)

---

## 이 작업이 하려는 것 (자연어 설명)

근접 유닛들이 같은 적을 공격할 때 모두 한 점으로 쏠려 뭉치는 현상을 개선한다.

각 적 주변에 최대 18개의 공격 위치 슬롯을 만들어두고, 유닛마다 서로 다른 슬롯을 배정한다.  
유닛은 배정받은 슬롯 위치까지 이동한 뒤, 전투 거리까지 적에게 직진하여 공격을 시작한다.  
먼저 도착한 유닛이 가장 가까운 슬롯을 차지하고, 이후 유닛들은 남은 슬롯으로 자연스럽게 퍼진다.

---

## 변경 개요

| 항목 | 내용 |
|------|------|
| 6슬롯 → 18슬롯 | 각 인접 타일마다 중심·좌측경계·우측경계 3위치 |
| 버그 수정 | 슬롯 위치 도달 후 `enemyViewPos`로 목표 전환하여 전투 진입 보장 |
| 수정 파일 | `AttackPositionManager.cs`, `UnitView.cs` |

---

## 수정 대상 파일

| 파일 | 변경 유형 |
|------|-----------|
| `Assets/_Project/Scripts/Application/Services/AttackPositionManager.cs` | **수정** — 18슬롯 설계로 재작성 |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | **수정** — 슬롯 도달 후 타겟 직진 추가 |
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | 기존 구현 유지 |
| `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs` | 기존 구현 유지 |

---

## Step 1 — AttackPositionManager.cs 수정 (18슬롯 설계)

### 1-1. 슬롯 위치 정의

타겟 주변 6개 인접 타일 각각에 대해 3개 위치를 생성한다.

```
인접 타일 i (0~5):
  - 중심:    HexMetrics.HexToWorld(neighbors[i])
  - 좌측경계: (HexMetrics.HexToWorld(neighbors[i]) + HexMetrics.HexToWorld(neighbors[(i+1)%6])) / 2
  - 우측경계: (HexMetrics.HexToWorld(neighbors[i]) + HexMetrics.HexToWorld(neighbors[(i+5)%6])) / 2
```

단, 좌/우 경계 위치는 해당 인접 타일이 실제로 존재하는 경우에만 생성한다.  
최소 6개(중심만), 최대 18개 위치가 생성된다.

**거리 참고**:
- 중심 위치: 타겟으로부터 0.866f
- 좌/우 경계 위치: 타겟으로부터 약 0.75f

### 1-2. 내부 데이터 구조 변경

```csharp
// 기존: Dictionary<HexCoord, Dictionary<int, HexCoord>>
//        외부 키: 타겟 타일, 내부 키: 유닛Id, 값: 배정된 인접 타일 HexCoord

// 변경: Dictionary<HexCoord, Dictionary<int, Vector3>>
//        외부 키: 타겟 타일, 내부 키: 유닛Id, 값: 배정된 슬롯 월드 좌표(뷰 좌표계)
private readonly Dictionary<HexCoord, Dictionary<int, Vector3>> _assignments
    = new Dictionary<HexCoord, Dictionary<int, Vector3>>();
```

슬롯 점유 수 계산은 `_assignments[targetCoord]`의 값들(Vector3)을 순회하여 같은 위치 개수를 카운트한다.  
Vector3 비교는 `Vector3.Distance < 0.01f`로 동등 여부를 판단한다.

### 1-3. ClaimAttackSlot 수정

동작 순서:
1. `grid.GetWalkableNeighborCoords(targetCoord)` 호출 → walkable 인접 타일 목록
2. 인접 타일이 없으면 `Vector3.zero` 반환 (폴백)
3. 각 인접 타일로부터 3개 위치(중심, 좌, 우) 생성
4. 각 위치의 점유 유닛 수 계산
5. **1순위**: 점유 수가 가장 적은 위치 / **2순위**: unitViewPos에서 가장 가까운 위치
6. 선택된 위치를 `_assignments[targetCoord][unitId]`에 저장
7. 선택된 위치를 뷰 좌표계로 변환 후 반환:
   ```
   ViewConverter.ToView(domainPos) + Vector3.up * HexMetrics.UnitYOffset
   ```

### 1-4. ReleaseAttackSlot / Clear 수정

데이터 구조가 `Dictionary<int, Vector3>`으로 바뀌므로 기존 로직과 동일하게 유지된다.  
(unitId로 제거, 빈 딕셔너리 정리)

---

## Step 2 — UnitView.cs 수정 (슬롯 도달 후 타겟 직진)

### 2-1. 버그 원인 요약

현재 `moveTarget = _currentAttackPos`(슬롯 위치)에 도달하면 이동이 멈춘다.  
슬롯 위치(0.75~0.866f)가 전투 판정 거리(유닛 0.3f, 건물 0.5f)보다 멀어 전투가 시작되지 않는다.

### 2-2. 수정 위치

`UnitView.cs` Phase 1 루프 내 `moveTarget` 계산 부분 (~Line 1245):

**기존**:
```csharp
Vector3 moveTarget = (_attackPositionManager != null
                      && _currentAttackTargetCoord.HasValue
                      && _currentAttackPos != Vector3.zero)
    ? _currentAttackPos
    : enemyViewPos;
```

**변경 후**:
```csharp
// 슬롯이 배정된 경우:
//   슬롯 위치에 아직 도달하지 않은 동안 → 슬롯 위치로 이동 (분산 효과)
//   슬롯 위치 근처에 도달하면 → _currentAttackPos를 즉시 zero로 초기화
//                              → 이후 프레임부터 slotAssigned=false가 되어 enemyViewPos로 영구 전환
// 슬롯 미배정이면 기존처럼 enemyViewPos 직접 추적.
bool slotAssigned = _attackPositionManager != null
                    && _currentAttackTargetCoord.HasValue
                    && _currentAttackPos != Vector3.zero;

// Y축 무시한 수평 거리로 슬롯 도달 여부 판단 (SlotReachThreshold: 0.15f)
// 도달 시 _currentAttackPos를 즉시 초기화 → 단방향 전환 보장
// (reachedSlot을 매 프레임 거리로만 판단하면 슬롯↔enemyViewPos 진동(oscillation)이 발생함)
if (slotAssigned && Vector2.Distance(
    new Vector2(transform.position.x, transform.position.z),
    new Vector2(_currentAttackPos.x, _currentAttackPos.z)) < 0.15f)
{
    _currentAttackPos = Vector3.zero;
    slotAssigned = false;
}

Vector3 moveTarget = slotAssigned
    ? _currentAttackPos
    : enemyViewPos;
```

**동작 흐름**:
1. Phase 1 진입 → 슬롯 배정 → 슬롯 위치로 직진
2. 슬롯 위치 0.15f 이내 도달 → `_currentAttackPos = Vector3.zero` → `slotAssigned = false` → 이후 프레임부터 enemyViewPos 영구 추적
3. 전투 거리(0.3f/0.5f) 진입 → `HasEnemyInRange` TRUE → 전투 시작

---

## 아키텍처 제약 확인

| 제약 | 확인 |
|------|------|
| Domain ← Application 참조 금지 | AttackPositionManager는 Application 레이어. HexGrid(Domain)를 참조하지만 허용 패턴 |
| UnityEngine 참조 (Application 레이어) | Vector3 사용. TileSlotManager 선례와 동일 |
| 멀티플레이 동기화 불필요 | Phase 1 이동 자체가 서버 전용 |

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| `GetWalkableNeighborCoords(targetCoord)`가 빈 리스트 반환 | `Vector3.zero` 반환 → UnitView 폴백으로 기존 동작 유지 |
| Phase 1 탈출 경로 누락 → 슬롯 해제 안 됨 | 기존 `StopMovement()` + `OnEntityDied` 방어선 유지 |
| Vector3 동등 비교 부동소수점 오차 | `Vector3.Distance < 0.01f` 임계값으로 처리 |
| `reachedSlot` 전환 후 다시 슬롯으로 돌아가는 진동 | 슬롯 도달 시 `_currentAttackPos = Vector3.zero`로 초기화 → `slotAssigned`가 영구적으로 false가 되어 단방향 전환 보장 (매 프레임 거리 재계산으로 인한 oscillation 방지) |

---

## 슬롯 용량 초기값

```csharp
// 슬롯 위치 1개당 최대 배정 유닛 수.
// 18위치 × 2 = 최대 36개 유닛이 하나의 타겟을 동시 공격 가능.
private const int MaxUnitsPerSlot = 2;
```

`MaxUnitsPerSlot`을 초과하는 경우에도 가장 적게 배정된 위치를 fallback으로 선택 — 무한 대기 없음.
