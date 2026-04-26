# Research: 근접 유닛 과도 뭉침 개선 (포위 위치 할당)

**날짜**: 2026-04-26  
**작업 ID**: 18_30_melee-spread  
**담당**: game-programmer 에이전트

---

## 1. 문제 정의

여러 근접 유닛이 같은 건물/성을 공격할 때 과도하게 한 지점에 몰리는 현상.  
소수 겹침은 허용하되, 게임의 특성상 모든 유닛의 목표가 동일하기 때문에  
**과도하게 집중되는 현상**을 개선하는 것이 목적이다.

---

## 2. 근본 원인 분석

### 2-1. Phase 1 직선 추적의 수렴 문제

근접 유닛의 이동은 Phase 0(타일 기반) → Phase 1(월드 좌표 직선 추적) → Phase 2(재스냅)로 구성된다.

**Phase 1 이동 코드** (`UnitView.cs` ~Line 1145):
```
Vector3 moveDir = enemyViewPos - transform.position;
transform.position += moveDir.normalized * worldSpeed * Time.deltaTime;
```

- `enemyViewPos`는 적(건물/유닛)의 정확한 `transform.position`
- 모든 유닛이 같은 건물을 공격하면 → 모든 유닛의 `enemyViewPos`가 동일
- 모든 유닛이 **완전히 같은 단일 지점**으로 직선 이동 → 물리적 수렴 발생

### 2-2. 기존 분산 시스템의 한계

| 시스템 | 역할 | Phase 1 적용 여부 |
|---|---|---|
| `TileOccupancyManager` (MaxOccupancy=3) | 타일당 최대 유닛 수 제한, BFS 우회 | ❌ Phase 1에서는 타일 단위 이동 없음 |
| `TileSlotManager` (6슬롯) | 같은 타일 내 시각적 위치 분산 | ❌ Phase 1 진입 시 `ReleaseSlotIfClaimed()` 호출로 슬롯 해제 |
| `FlowFieldService` | 목적지별 경로 공유 | ❌ 같은 목적지 = 같은 경로 = 같은 타일로 유도 |

→ **Phase 1 진입 후에는 기존 분산 시스템이 모두 비활성 상태**가 되어 수렴이 발생한다.

### 2-3. Phase 0에서도 집중이 발생하는 이유

FlowFieldService는 목적지 하나에 대해 맵 전체의 방향 정보를 계산한다.  
같은 방향에서 접근하는 유닛들은 동일한 "최단 경로" 타일들로 유도되고,  
TileOccupancyManager가 MaxOccupancy=3을 초과하면 BFS 우회를 하지만  
우회 결과가 여전히 목적지 바로 앞 1~3개 타일에 집중된다.

즉 Phase 0에서도 앞쪽 소수 타일에 집중 → Phase 1에서 완전히 같은 지점으로 수렴.

---

## 3. 구현 방향 — 공격 위치 할당 (AttackPositionManager)

### 3-1. 핵심 아이디어

Phase 1 진입 시 `enemyViewPos` 대신 **적 주변 6개 인접 타일 중 배정된 타일의 중심**으로 이동.

각 유닛이 서로 다른 인접 타일을 배정받으면 Phase 1에서도 자연스럽게 분산된다.

```
[기존]  모든 유닛 → enemyViewPos (건물 중심)
[변경]  유닛 A → 적 북동쪽 타일 중심
        유닛 B → 적 동쪽 타일 중심
        유닛 C → 적 남동쪽 타일 중심
```

### 3-2. 슬롯 선택 규칙

1. 타겟 주변 6개 인접 타일(HexCoord) 조회
2. 각 인접 타일에 이미 배정된 유닛 수 확인
3. 유닛 현재 위치에서 **가장 가까운 빈(또는 가장 적게 배정된) 타일** 선택
4. 배정 후 Phase 1 목표 좌표를 해당 타일 중심으로 설정

→ "가장 가까운 빈 슬롯" 규칙으로 우회 없이 자연스러운 방향에서 접근 가능.

### 3-3. 공격 사거리 도달 방식

Phase 1 루프 안에서는 목표 위치(배정된 타일 중심)로 이동하면서  
매 프레임 `HasEnemyInRange(_unitData)` 체크가 동시에 수행된다.

적 인접 타일 중심 → 건물까지의 거리 = 0.866f (TileHeight)  
공격 사거리(MeleeContactDist) = 0.3f  

배정된 타일 중심을 향해 이동하는 도중에 공격 사거리(0.3f)에 진입하면  
자연스럽게 전투 루프로 전환된다 → **정확히 타일 중심까지 이동할 필요 없음**.

---

## 4. 새 시스템 설계 — AttackPositionManager

### 4-1. 역할

- 타겟(HexCoord)별로 인접 타일에 배정된 유닛 목록을 관리
- 유닛이 Phase 1 진입 시 슬롯 요청(Claim) → 배정된 타일의 월드 좌표 반환
- 유닛 사망/Phase 2 진입 시 슬롯 해제(Release)

### 4-2. 인터페이스 (예상)

```csharp
// 타겟 타일(targetCoord) 주변에서 unit의 현재 위치 기준 가장 가까운 슬롯을 배정.
// 반환: 배정된 인접 타일의 월드 뷰 좌표 (Phase 1 목표로 사용)
Vector3 ClaimAttackSlot(HexCoord targetCoord, int unitId, Vector3 unitWorldPos)

// 배정된 슬롯 해제 (유닛 사망, Phase 2 진입, 타겟 변경 시)
void ReleaseAttackSlot(HexCoord targetCoord, int unitId)

// 재경기/씬 전환 시 전체 초기화
void Clear()
```

### 4-3. 슬롯 용량 규칙

- 기본: 인접 타일 1개당 최대 2~3개 유닛 배정 (튜닝 가능)
- 모든 인접 슬롯이 꽉 차면 fallback: 가장 적게 배정된 타일 반환 (무한 대기 방지)

---

## 5. 영향 범위 (수정 대상 파일)

| 파일 | 변경 유형 | 위치 |
|---|---|---|
| `Application/Services/AttackPositionManager.cs` | **신규 생성** | — |
| `Presentation/Unit/UnitView.cs` | **수정** | Phase 1 루프 내 목표 좌표 계산 (~Line 1000~1167) |
| `Bootstrap/GameBootstrapper.cs` | **수정** | AttackPositionManager 생성 + UnitView에 주입 |
| `Presentation/Unit/UnitView.cs` | **수정** | `SetDependencies()` 파라미터 추가 |

### 5-1. UnitView 수정 핵심 포인트

**Phase 1 진입 시** (Line ~997):
```csharp
// 기존: enemyViewPos를 매 프레임 조회하여 직선 이동
Vector3 enemyViewPos = _positionProvider.GetUnitWorldPosition(targetId);

// 변경: Phase 1 진입 시 1회만 AttackPositionManager에서 배정된 타일 좌표 취득
Vector3 attackPos = _attackPositionManager.ClaimAttackSlot(targetCoord, _unitData.Id, transform.position);
```

**Phase 1 종료 시** (break 또는 전투 루프 종료 후):
```csharp
_attackPositionManager.ReleaseAttackSlot(targetCoord, _unitData.Id);
```

**유닛 사망 시** (`OnEntityDied` 구독 내):
```csharp
// ReleaseSlotIfClaimed, ReleaseOccupancyIfPending과 함께 공격 슬롯도 해제
_attackPositionManager?.ReleaseAttackSlot(_lastAttackTarget, _unitData.Id);
```

---

## 6. 기존 시스템과의 관계

| 시스템 | 관계 |
|---|---|
| `TileOccupancyManager` | 독립적 — Phase 0에서 타일 점유 분산은 그대로 유지 |
| `TileSlotManager` | 독립적 — Phase 0 타일 내 시각 분산은 그대로 유지 |
| `FlowFieldService` | 변경 없음 — 경로탐색 로직 무관 |
| `UnitCombatUseCase` | 변경 없음 — 전투 판정 로직 무관 |

---

## 7. 네트워크 고려 사항

- `AttackPositionManager`는 **서버(또는 싱글플레이)에서만 사용**.  
  Phase 1 이동 자체가 서버에서만 실행되므로 클라이언트 동기화 불필요.
- `TileSlotManager`와 동일 정책 적용 가능  
  ("슬롯 할당은 시각 전용이므로 서버/클라이언트가 별도로 관리해도 무방" — TileSlotManager 주석 참조)

---

## 8. 추가 고려사항 (타겟 변경 시)

Phase 1에서 타겟이 사망하여 `FindNearestEnemyInDetectRange`로 새 타겟을 잡는 경우(`continue`):
- 이전 타겟에 대한 슬롯을 해제하고 새 타겟에 대한 슬롯을 재배정해야 한다.
- 타겟 HexCoord 추적을 위한 `_currentAttackTargetCoord` 필드 필요.

---

## 9. 미결 이슈

- 슬롯 용량(인접 타일당 최대 유닛 수)은 게임 밸런스 테스트 후 결정  
  → 기본값으로 2 제안 (6 타일 × 2 = 최대 12개 유닛이 한 건물 공격 가능)
- 원거리 유닛은 Phase 1 사용 여부와 무관하게 공격 위치 할당 제외  
  (Phase 1은 근접유닛 전용이며, 원거리는 A*로 목적지에 직접 도달)
