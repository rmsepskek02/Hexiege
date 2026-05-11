# 유닛 이동/전투 시스템 재설계 — Plan

작성일: 2026-04-30
관련 문서:
- 새 규칙: `Assets/_Project/Docs/GameSystemRules.md`
- 조사 결과: `./Research.md`

---

## 1. 이 작업이 무엇을 왜 하는가 (자연어 설명)

지금 우리 게임의 유닛 이동/전투 코드는, 작은 버그가 보일 때마다 한 줄씩 패치를 덧대다가 결국 **어디를 손대도 다른 곳이 망가지는 상태**가 되었습니다. 그래서 이번에는 **새 규칙(GameSystemRules.md)을 처음부터 그대로 옮기는 깨끗한 버전**을 만들기로 했습니다.

이 Plan 문서는 그 작업을 **어떤 순서로 / 어떤 파일을 / 어떻게 수정할지**를 정리한 실행 계획서입니다. 코드는 아직 작성하지 않습니다.

핵심은 다음과 같습니다.

- **기존 코드는 지우지 않고 주석으로 막아둡니다.** 새 코드와 비교해서 검증할 수 있도록 남겨둬야, 행동이 달라졌을 때 어느 쪽이 맞는지 즉시 확인할 수 있습니다.
- **시스템(매니저) 단위로 작은 조각을 먼저 만들고 → 마지막에 UnitView를 새 코루틴으로 교체합니다.** 매니저들이 먼저 안정되어야, 그것을 호출하는 UnitView를 안전하게 새로 쓸 수 있습니다.
- **사용자 확인이 필요한 결정 포인트는 코드 작성 전에 미리 묻습니다.** 슬롯의 단일/다중 점유 정책, 근접/원거리 구분 데이터 위치, 즉시 재계산 트리거 정책 같은 것들은 임의로 결정하지 않습니다.

---

## 2. 기존 로직 제거 계획 — 비활성화 우선 원칙 (Plan 최상단 필수)

이 작업의 모든 "삭제"는 **물리적 삭제가 아니라 주석 처리(블록 주석 `/* ... */` 또는 `#if FALSE`)**로 시작합니다. 이유는 두 가지입니다.

1. **새 흐름이 동일한 결과를 내는지 검증할 때** 기존 코드를 옆에 둬야 비교 가능합니다.
2. **롤백이 즉시 가능합니다.** 새 흐름에서 발견하지 못한 시나리오가 실기에서 드러나면, 주석을 푸는 것만으로 즉시 복구 가능합니다.

### 2-1. 비활성화 대상 (주석 처리)

| 위치 | 대상 | 안전 근거 |
|------|------|---------|
| `UnitView.MoveAlongPath()` 전체 | Phase 0/1/2 단일 코루틴 본문 | 새 코루틴(`MoveAlongPathV2()`)을 별도 추가하고 `MoveTo()`에서 V2를 호출 → 기존 본문은 호출되지 않음 |
| `UnitView` 7차 개선 Phase 2 forward 보정 (1453~1481) | nearestTile 인접 forward 후보 탐색 | 새 헬퍼(예: `FindForwardResumeTile`)로 단일화. 기존 분기는 호출 경로에서 빠짐 |
| `UnitView` 6차 개선 forward filter 5곳 | Phase 0 Lerp / 스텝 후 / Phase 1 진입 / 타겟 사망 / 전투 후 재선택 | 새 흐름은 "Phase 2의 앞쪽 타일 단일 결정"으로 통합 — forward 검사 자체가 한 곳으로 모임 |
| `UnitView` 슬롯 도달 후 enemyViewPos 전환 (1379~1397) | `Vector2.Distance < 0.15f` 분기 | 새 규칙: 슬롯 도달 = 전투 시작이므로 분기 자체가 불필요 |
| `TileSlotManager`의 6슬롯 정의 + ClaimSlot 6번까지 fallback | SlotOffsetRatios 6항목 | 새 규칙: 3슬롯(앞/뒤좌/뒤우) 고정. 기존 6슬롯 배열은 `// LEGACY:` 주석으로 보존 |
| `AttackPositionManager`의 동점 비교 로직 (`bestCount/bestDistSq`) | 가장 적게 점유된 위치 우선 분기 | 새 규칙: 접근 방향 각도 가까운 빈 슬롯 우선. 기존 분기는 보존하되 호출되지 않도록 새 메서드(`ClaimAttackSlotByApproach`)로 분리 |
| `TileOccupancyManager.FindAvailableTile`의 fallback BFS (필터 OFF 재시도) | useForwardFilter false로 두 번째 BFS | 새 규칙: 앞쪽에 빈 타일이 없으면 대기. fallback BFS는 호출되지 않게 분기 자체를 비활성화 |
| `UnitView._pendingOccupancyTile` 추적 + ReleaseOccupancyIfPending | 점유 누수 방지용 보조 변수 | 새 흐름은 "다음 타일 결정 시 슬롯 + 점유 동시 예약"으로 모이므로 별도 변수 불필요. 단, 이 비활성화는 가장 마지막에 진행하여 새 흐름에서 누수가 없는지 충분히 검증 후 적용 |

### 2-2. 제거(주석 처리)해도 안전한 근거 요약

- **외부 API 시그니처가 그대로 유지됨** — `UnitView.MoveTo(path)`, `UnitView.StartCombatAnimation(...)`, `UnitView.StopCombatAnimation()`은 변경하지 않음. 호출 측 코드(InputHandler, Network*Controller, ProductionTicker 등)는 영향 없음.
- **이벤트(GameEvents) 발행 패턴 유지** — `OnUnitWalkStarted`, `OnUnitEnteredCombat`, `OnCombatStarted`, `OnCombatStopped`, `OnEntityDied` 모두 같은 시점에 발행됨. NetworkCombatController/NetworkUnitMovementController가 의존하는 발행 시점이 보장됨.
- **Domain 데이터 구조는 그대로** — `UnitData.Position/Facing/ClaimedTile/AttackCooldown` 등 도메인 필드 의미가 변하지 않음.

### 2-3. 비활성화 후 정리 시점

- 비활성화 상태 그대로 PR을 마치지 않습니다.
- **사용자 실기 검증 + qa-tester 통과 + 충분한 시나리오 확인 후** "작업 사이클 종료" 단계에서 한 번에 물리 삭제합니다 (별도 task 문서 또는 같은 task의 마지막 단계).
- 즉, 이번 task의 산출물 코드 시점에는 기존 코드가 주석 형태로 함께 남아 있는 것이 정상입니다.

---

## 3. 신규 시스템 설계 (새 규칙에 맞는 클래스/컴포넌트 구조)

### 3-1. 책임 분리 도식

```
[UnitView]                      ← MonoBehaviour. 코루틴 + 시각 처리 + Animator
   ↓ 사용
[TileMoveSlotManager]            ← (신규 또는 TileSlotManager 재구성)
   - 타일별 3슬롯 점유 추적
   - 슬롯 위치(월드 좌표) 반환
   - 점유치 2 처리 (대형 유닛)

[AttackSlotManager]              ← AttackPositionManager 개편
   - 적별 12방향 슬롯 점유
   - 접근 방향 각도 가까운 빈 슬롯 반환

[TileOccupancyManager]           ← 그대로
   - 타일별 점유 합계 (소형1/대형2)
   - 가득 시 앞쪽 빈 타일 BFS, 없으면 대기

[FlowFieldService]               ← 그대로 (eager 재계산 트리거 추가)
   - 같은 목적지 공유 캐시
   - 건물 변경 시 InvalidateAll + 살아있는 모든 유닛 RequestMove

[UnitMovementUseCase]            ← 인터페이스 유지, 일부 헬퍼 추가
   - RequestMove, ProcessStep, IsWalkable, GetOccupancySize

[UnitCombatUseCase]              ← 그대로
   - HasEnemyInRange, HasEnemyInDetectRange, FindNearestEnemy*, TryAttack
```

### 3-2. 새 흐름 (UnitView.MoveAlongPathV2 의사 코드)

새 코루틴은 다음과 같은 단계를 명확히 분리합니다.

```
MoveAlongPathV2(path):
  while unit.IsAlive:
    nextTile = path[i]

    # 규칙 5: 점유 가득 시 앞쪽 빈 타일 우회 또는 대기
    actualTile = TileOccupancyManager.FindForwardAvailableOrWait(nextTile, finalTarget)

    # 규칙 10: 다음 타일 결정 즉시 슬롯 할당
    moveSlotPos = TileMoveSlotManager.ClaimSlot(actualTile, unit)

    # 규칙 17: 슬롯 위치를 이동 목표로
    while not arrivedAt(moveSlotPos):
      # 규칙 8/13: 매 프레임 적 감지/사거리 체크
      if isMelee and HasEnemyInDetectRange:
        TileMoveSlotManager.ReleaseSlot(actualTile, unit)  # 이동 슬롯 해제
        ENTER_MELEE_PURSUIT(target)                          # 공격 슬롯으로 직진
        # 전투 종료 시 RESUME_FROM_FORWARD_TILE() (규칙 15)
      elif HasEnemyInRange:  # 원거리 또는 슬롯 안에서 닿는 적
        # 규칙 13/15: 원거리는 슬롯 유지 + 즉시 공격
        ENTER_STATIONARY_COMBAT(target)
        # 전투 종료 시 그대로 다음 타일로 (Phase 2 불필요)

      transform.position += direction * moveSpeed * deltaTime

    # 슬롯 도달 = 타일 도착 (규칙 10)
    UnitMovementUseCase.ProcessStep(unit, prev, actualTile)

    # 규칙 4: 다음 타일은 공유 타일 상태(플로우 필드)에서 재조회
    nextStep = FlowField.NextStepFrom(actualTile, finalTarget)
    if nextStep is None: break  # 도착

  Cleanup()
```

```
ENTER_MELEE_PURSUIT(target):
  attackSlotPos = AttackSlotManager.ClaimByApproach(target, unit, approachVec)
  while unit.IsAlive:
    if HasEnemyInRange: ENTER_COMBAT_LOOP(target); break
    if not HasEnemyInDetectRange: AttackSlotManager.Release; RESUME_FROM_FORWARD_TILE; return

    enemyPos = positionProvider.GetWorldPos(target)
    if enemyPos == zero:                            # 타겟 사망
      next = FindNearestEnemyInDetectRange(unit)
      if next: target = next; AttackSlotManager.Reclaim(...); continue
      else: AttackSlotManager.Release; RESUME_FROM_FORWARD_TILE; return

    transform.position += dirTo(attackSlotPos) * moveSpeed * deltaTime
```

```
RESUME_FROM_FORWARD_TILE():
  # 규칙 15 근접: 현재 월드 위치 기준 앞쪽 가장 가까운 타일에서 A* 재개
  forwardTile = FindForwardClosestTile(transform.position, finalTarget)
  TileMoveSlotManager.ClaimSlot(forwardTile, unit)
  ProcessStep(unit, prev, forwardTile)
  path = RequestMove(unit, finalTarget)
```

(원거리 유닛은 `ENTER_MELEE_PURSUIT` 분기를 거치지 않고 `ENTER_STATIONARY_COMBAT`만 사용 → 자동으로 슬롯 유지 + 그대로 A* 재개)

### 3-3. 신규/변경 클래스 시그니처 초안

```csharp
// TileMoveSlotManager (신규 또는 TileSlotManager 재구성)
public class TileMoveSlotManager
{
    public Vector3 ClaimSlot(HexCoord tile, int unitId, Vector3 unitWorldPos, float occupancySize);
    public void   ReleaseSlot(HexCoord tile, int unitId);
    public void   Clear();
}
```

```csharp
// AttackSlotManager (AttackPositionManager 개편 — 클래스명은 사용자 결정)
public class AttackSlotManager
{
    public Vector3 ClaimByApproach(HexCoord targetCoord, int unitId,
                                   Vector3 unitWorldPos, Vector3 approachVec,
                                   float contactRadius);
    public void   Release(HexCoord targetCoord, int unitId);
    public void   Clear();
}
```

```csharp
// TileOccupancyManager (인터페이스 추가)
public class TileOccupancyManager
{
    // 새 규칙: 앞쪽 빈 타일 또는 대기
    public HexCoord? FindForwardAvailable(HexCoord preferred, float unitSize,
                                          HexGrid grid, HexCoord destination);
    // 기존 메서드는 그대로 유지 (legacy)
}
```

```csharp
// UnitData / UnitStats — 근접/원거리 명시
public enum AttackKind { Melee, Ranged }   // 또는 bool IsMelee
public struct StatValues { ...; public AttackKind Kind; }
```

(이 시그니처들은 초안이며, 코드 작성 단계에서 사용자 확인 후 확정)

---

## 4. 구현 단계별 작업 목록 (파일별 변경 내용)

각 단계는 사용자 확인 → 코드 작성 → qa-tester 검증 → 다음 단계 순으로 진행합니다.

### Step 0. 사전 결정 (코드 작성 전 사용자 확인 필요)

모든 항목 확정 완료 (2026-04-30).

- (D1) ✅ 슬롯 1개 차지 + 점유 카운트 2 소비
- (D2) ✅ 1 per slot + fallback — 슬롯이 모두 차면 가장 적게 찬 슬롯에 fallback
- (D3) ✅ `UnitStats`에 `AttackKind` enum 추가하여 근접/원거리 명시적으로 구분
- (D4) ✅ 현재 계획대로 진행, 테스트 후 필요시 추가 정책 도입
- (D5) ✅ 유닛→적 벡터 기준 (유닛 현재 위치에서 타겟 위치를 향하는 방향)
- (D6) ✅ `TileMoveSlotManager`로 클래스명 변경 — 이동 슬롯/공격 슬롯 책임 명확히 구분

### Step 1. UnitStats 근접/원거리 명시화

(D3 결과에 따라 분기)

- 파일: `Domain/Unit/UnitStats.cs`, `Infrastructure/Config/UnitStatsConfig.cs`, `Bootstrap/GameBootstrapper.cs`
- 작업: `AttackKind` enum 추가, `StatValues`에 필드 추가, ScriptableObject Inspector에 노출, `InitializeUnitStatsFromConfig()`에서 매핑.
- 안전성: 기존 코드는 영향 없음 (필드 추가만).

### Step 2. TileMoveSlotManager 신설/재구성

(D6 결정에 따라 클래스명)

- 파일: `Application/Services/TileSlotManager.cs` (재구성) 또는 `Application/Services/TileMoveSlotManager.cs` (신규)
- 작업:
  - 3슬롯(앞/뒤좌/뒤우) 정의. 슬롯 위치는 타일 중심 + 방향 벡터 × 비율(예: 0.25 × TileWidth).
  - `ClaimSlot(tile, unitId, unitWorldPos, occupancySize)` — 가까운 빈 슬롯 → 거리 같으면 1→2→3 우선 → 점유 카운트 2 소비.
  - `ReleaseSlot(tile, unitId)` — 점유 해제.
  - 기존 6슬롯 배열은 `// LEGACY:` 주석으로 보존(Step 8에서 물리 삭제 검토).
- 안전성: 새 메서드 시그니처를 기존과 다르게 하면 기존 호출 측이 영향 없음. 기존 호출 측(UnitView)은 Step 7에서 일괄 교체.

### Step 3. AttackSlotManager 배정 규칙 변경

- 파일: `Application/Services/AttackPositionManager.cs`
- 작업:
  - 새 메서드 `ClaimByApproach(...)` 추가 — 접근 방향 각도 가까운 빈 슬롯, 가득 시 측면/뒤로 순차 fallback.
  - 기존 `ClaimAttackSlot(...)`은 보존 (legacy).
  - (D2 결과에 따라) MaxUnitsPerSlot 정책 결정.
- 안전성: 기존 메서드 호출은 그대로 동작. 새 코루틴만 새 메서드 호출.

### Step 4. TileOccupancyManager의 "앞쪽 또는 대기" 정책

- 파일: `Application/Services/TileOccupancyManager.cs`
- 작업:
  - 새 메서드 `FindForwardAvailable(preferred, size, grid, destination)` — forward 필터 통과 타일만 반환, 통과 후보 없으면 null (대기).
  - 기존 `FindAvailableTile(... fallback BFS ...)`은 보존.
- 안전성: 기존 메서드 호출 그대로.

### Step 5. FlowFieldService 즉시 재계산 트리거

(D4 결과에 따라 분기)

- 파일: `Application/Services/FlowFieldService.cs`, `Bootstrap/GameBootstrapper.cs`
- 작업:
  - 기존 `OnBuildingPlaced` / `OnEntityDied(building)` 구독은 유지 (lazy invalidate).
  - 추가로 `OnBuildingPlaced` / 건물 사망 시 살아있는 모든 유닛에게 `RequestMove(finalTarget)`을 다시 호출하는 헬퍼 (UnitMovementUseCase 또는 GameBootstrapper에 추가).
  - 진행 중 코루틴이 새 path를 받아 자연스럽게 재진입하도록 UnitView 측 노출 메서드 (`OnPathInvalidated` 등) 추가.
- 안전성: 새 path 수신 후 코루틴 재진입은 새 코루틴(V2)에서만 처리. 기존 코루틴은 영향 없음.

### Step 6. UnitMovementUseCase 헬퍼 추가

- 파일: `Application/UseCases/UnitMovementUseCase.cs`
- 작업:
  - `FindForwardClosestTile(unitWorldPos, finalTarget)` — 현재 월드 위치 기준 앞쪽 가장 가까운 walkable 타일.
  - `RegisterOccupancyMove`/`ReleaseOccupancy`는 기존 시그니처 유지.
- 안전성: 새 메서드 추가만.

### Step 7. UnitView.MoveAlongPathV2 신설 (가장 큰 단계)

- 파일: `Presentation/Unit/UnitView.cs`
- 작업:
  - 새 코루틴 `MoveAlongPathV2(path)` 추가 — 의사 코드 3-2 그대로 구현.
  - Phase 0/1/2를 별도 코루틴(또는 헬퍼 메서드)으로 분리: `RunTileTraversal`, `EnterMeleePursuit`, `EnterStationaryCombat`, `ResumeFromForwardTile`.
  - 근접/원거리 분기: `_unitData.Stats.Kind == Melee`로 판정. 원거리는 `EnterMeleePursuit`을 호출하지 않음.
  - 기존 `MoveAlongPath()` 본문은 `/* ... */` 블록 주석으로 비활성화.
  - `MoveTo(path)`에서 `MoveAlongPathV2`를 호출하도록 전환.
- 안전성: V2 호출 후 단계별 qa-tester 시나리오 검증 → 문제 발견 시 한 줄 변경(`MoveAlongPathV2` → `MoveAlongPath`)으로 즉시 롤백.

### Step 8. (검증 통과 후) 비활성화 코드 물리 삭제

- 본 task 마지막 단계 또는 별도 task로 분리.
- qa-tester 다중 시나리오 통과 + 사용자 실기 확인 후 진행.

---

## 5. 예상 위험 요소 및 아키텍처 제약

### 5-1. 멀티플레이 RPC 송수신 패턴 영향

- 새 흐름이 기존 이벤트 발행 시점을 그대로 유지하므로 NetworkCombatController/NetworkUnitMovementController/NetworkHealthSync는 영향 없음 — **단, Step 7 코드 작성 시 모든 발행 지점을 기존과 1:1 매칭하는지 점검 필수**.
- 위험: 새 코루틴이 `OnUnitWalkStarted`를 한 번도 발행하지 않으면 클라이언트가 영원히 Idle/Walk 상태로 멈춰 보일 수 있음.

### 5-2. NetworkContext 가드 누락

- `if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;` 가드를 새 코루틴 진입부에 동일하게 두지 않으면 클라이언트가 자체 이동을 시도해 NetworkTransform과 충돌.

### 5-3. ViewConverter 일관성

- 새 흐름에서 모든 좌표 변환은 `ViewConverter.ToView/FromView`를 거쳐야 함. 도메인 좌표(HexMetrics.HexToWorld)와 뷰 좌표를 섞으면 Red팀에서만 좌표가 어긋남.

### 5-4. Domain → Core 의존 금지

- Domain 레이어 파일에 `using Hexiege.Core` 또는 `UnityEngine` 추가 금지. 좌표 계산은 Core/Application 레이어에서만 수행. `UnitStats`에 새 enum을 추가할 때 Domain에 두되 다른 의존이 추가되지 않도록 주의.

### 5-5. Application → Unity.Netcode 직접 참조 금지

- `NetworkContext` 정적 홀더만 통해 분기.

### 5-6. 코루틴 분리에 따른 cleanup 누수

- 새 흐름은 코루틴이 여러 헬퍼로 분리됨. 각 헬퍼에서 `try/finally` 또는 명시적 cleanup 콜로 슬롯/점유를 해제하지 않으면 누수 발생. 사망/StopMovement/씬 전환 모든 경로 검증 필요.

### 5-7. NGO 2.9.2 제약

- ServerRpc/ClientRpc 메서드명은 반드시 해당 접미사로 끝나야 함. 새 컨트롤러 추가 시 동일 규칙.
- NetworkBehaviour는 Infrastructure에만 둠.

### 5-8. 단일 코루틴 → 다중 헬퍼 호출 시 상태 공유

- `_currentAttackTargetCoord`, `_currentAttackPos`, `_claimedSlotTile` 등 상태 필드가 헬퍼 간에 공유됨. 한 헬퍼에서 정리하지 않은 상태가 다음 헬퍼로 흘러가면 점유 누수. 헬퍼 진입/종료 시 상태 초기화 규약 명시 필요.

### 5-9. 점유치 2 유닛의 슬롯 배정

- 슬롯이 3개인데 점유치 2 유닛 2마리가 동시에 같은 타일에 들어오려 하면 카운트 4 → 한도 3 초과. "들어가지 못함 = 우회 또는 대기"로 처리되는지 명확히 검증.

### 5-10. 기존 5/6/7차 개선이 막던 시나리오 재발 위험

- "타겟 사망 후 뒷무빙", "Phase 2 후방 스냅", "Lerp 중 forward 적 무시" 등은 모두 새 흐름의 단일 헬퍼(`ResumeFromForwardTile` + `EnterMeleePursuit` 내 forward filter 미적용 정책)로 흡수됨. 새 흐름이 정말로 같은 시나리오를 모두 커버하는지 qa-tester 시나리오로 명시 검증 필요.

### 5-11. 멀티플레이 동기화 정확도

- 슬롯 위치(월드)가 도메인/뷰 좌표 변환을 거쳐 Red/Blue 양쪽에서 동일하게 보이는지 확인. 슬롯 좌표 계산이 `ViewConverter.IsFlipped`에 의존하면 안 됨.

---

## 6. 기존 코드에서 재활용 가능한 부분

| 재활용 대상 | 출처 | 활용 방안 |
|------------|------|---------|
| `FlowFieldService` 캐시 + InvalidateAll 패턴 | `Application/Services/FlowFieldService.cs` | 그대로 사용. eager 재계산 헬퍼만 추가 |
| `TileOccupancyManager`의 forward 필터 + +1 여유 BFS | `Application/Services/TileOccupancyManager.cs` | 새 메서드(`FindForwardAvailable`)에서 그대로 활용. fallback BFS만 호출되지 않도록 |
| `AttackPositionManager`의 12방향 angular 슬롯 + ToView/UnitYOffset 변환 | `Application/Services/AttackPositionManager.cs` | 새 메서드(`ClaimByApproach`)에서 슬롯 위치 계산 로직 그대로 재사용. 점유 사전 구조도 유지 |
| `UnitView.ApplyDirection` / `CalculateAttackAngle` / `Update()` 회전 추적 | `Presentation/Unit/UnitView.cs` | 그대로. 새 코루틴에서 호출만 그대로 유지 |
| 사망 이벤트 구독 + ReleaseSlotIfClaimed / ReleaseAttackSlotIfClaimed | `Presentation/Unit/UnitView.cs` | 그대로. 새 헬퍼 이름으로 리네이밍 가능 |
| `UnitMovementUseCase.RequestMove` / `ProcessStep` / `IsWalkable` / `GetOccupancySize` | `Application/UseCases/UnitMovementUseCase.cs` | 그대로 |
| `UnitCombatUseCase.HasEnemyInDetectRange/InRange` / `FindNearestEnemyInDetectRange` / `TryAttack` | `Application/UseCases/UnitCombatUseCase.cs` | 그대로 |
| `NetworkContext` 가드 패턴 | `Application/NetworkContext.cs` | 새 코루틴 진입부 + 분기마다 그대로 적용 |
| `ViewConverter.ToView/FromView/FlipDirection` | Core | 그대로 |
| `GameEvents.*` 발행 시점 매핑 | 전역 | 새 흐름에서 발행 지점을 1:1 매칭 |
| 기존 코루틴의 전투 루프 골격 (`HasEnemyInRange while → AttackCooldown while → 재진입 또는 break`) | UnitView 802~940 | 새 헬퍼 `EnterCombatLoop`로 분리하여 그대로 활용 |

---

## 7. 작업 진행 체크리스트

코드 작성은 사용자 명시적 승인 후 시작합니다.

- [x] Step 0: 사용자에게 D1~D6 결정 질문
- [x] Step 1: UnitStats `AttackKind` 추가 (Domain + ScriptableObject + Bootstrapper)
- [x] Step 2: TileMoveSlotManager 신설/재구성
- [x] Step 3: AttackPositionManager에 `ClaimByApproach` 추가
- [x] Step 4: TileOccupancyManager에 `FindForwardAvailable` 추가
- [x] Step 5: FlowFieldService eager 재계산 트리거 + UnitMovementUseCase 헬퍼
- [x] Step 6: UnitMovementUseCase에 `FindForwardClosestTile` 추가
- [x] Step 7: UnitView.MoveAlongPathV2 신설 + 기존 본문 주석 처리
- [ ] qa-tester 시나리오 검증 (뒷무빙, 뭉침, 교착, 팅김, 전체 후퇴, 근접/원거리 전환)
- [ ] 사용자 실기 검증
- [ ] Step 8: 비활성화 코드 물리 삭제 (별도 단계 또는 별도 task)

---

## 8. 종료 후 메모리 업데이트 항목 (참고)

본 task 종료 시 game-programmer 메모리에 다음을 기록할 가능성이 큽니다.

- 이동/전투 코루틴 분리 패턴 (단일 거대 코루틴 → 헬퍼 분리)
- TileMoveSlotManager 3슬롯 정의 + 점유치 2 처리 규약
- AttackSlotManager 접근 방향 기반 배정 규약
- forward filter를 단일 헬퍼(`ResumeFromForwardTile`)로 모은 패턴
- UnitStats에 AttackKind 명시 도입
- 건물 변경 시 eager 재계산 트리거
