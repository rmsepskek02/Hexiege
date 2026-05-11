# Plan — 이동/전투 시스템 전면 재구현

작성일: 2026-05-06  
Research: `_Tasks/2026-05-06/19_43_movement-combat-full-rewrite/Research.md`  
규칙 근거: `Docs/GameSystemRules.md`

---

## 이 작업이 무엇인지

규칙 없이 만들어진 이동/전투 코드를 전부 걷어내고, GameSystemRules.md의 18개 규칙을 유일한 기준으로 삼아 처음부터 다시 구현한다.

구현 순서는 규칙의 흐름을 따른다:  
기본 이동 → 점유 관리 → 이동 슬롯 → 전투 진입 → 공격 슬롯 → 전투 종료

각 Step마다 어떤 규칙을 구현하는지 명시한다.

---

## ⚠️ 코드 수정 방침 (2026-05-07 비교 분석 반영)

각 파일을 코드 수준에서 직접 비교 분석한 결과, **"전체 재구현"이 필요한 파일은 UnitView뿐**이다.  
나머지 파일들은 규칙에 맞는 부분은 그대로 두고, 위반 부분만 수술적으로 제거/추가한다.

| 파일 | 방침 | 근거 |
|------|------|------|
| `UnitView.cs` | **V2 메서드 삭제 + V3 신규 작성** | V2 코루틴 전체가 규칙 위반 구조 |
| `TileOccupancyManager.cs` | **`FindAvailableTile` 2개 삭제** | `FindForwardAvailable`은 이미 규칙 5 완전 준수 |
| `AttackPositionManager.cs` | **`ClaimAttackSlot` 삭제** | `ClaimByApproach`는 이미 규칙 18 완전 준수 |
| `TileMoveSlotManager.cs` | **`GetUnitCount` 1개 추가** | 슬롯 정의/배정 모두 규칙 17 준수 |
| `UnitMovementUseCase.cs` | **`FindAvailableTile` 2개 삭제** | `FindForwardAvailable`/`FindForwardClosestTile`은 이미 규칙 준수 |
| `TileSlotManager.cs` | **파일 삭제** | 레거시, V3 흐름에서 불필요 |
| `GameBootstrapper.cs` | **초기화 코드 수정** | TileSlotManager 제거에 따른 주입 코드 정리 |

---

## ⚠️ 기존 로직 제거 선언

아래 코드는 잘못된 구현으로 롤백 가치가 없으므로 Step 1에서 즉시 삭제한다.

| 삭제 대상 | 처리 |
|---|---|
| `UnitView.MoveAlongPathV2` 메서드 전체 | **삭제** |
| `UnitView.MoveAlongPath` 메서드 전체 (yield break) | **삭제** |
| `UnitView.RunTileTraversal` / `EnterMeleePursuit` / `EnterStationaryCombat` 등 V2 헬퍼 | **삭제** |
| `UnitView` V1 전용 상태 필드 (`_claimedSlotTile`, `_currentAttackTargetCoord`, `_currentAttackPos`, `_pendingOccupancyTile`) | **삭제** |
| `UnitView` V1 전용 cleanup 헬퍼 (`ReleaseSlotIfClaimed`, `ReleaseAttackSlotIfClaimed`, `ReleaseOccupancyIfPending`) | **삭제** |
| `UnitView._slotManager` 필드 (TileSlotManager 참조) | **삭제** |
| `TileSlotManager` 전체 | **삭제** (사용처 제거 포함) |
| `TileOccupancyManager.FindAvailableTile` (2개 오버로드) | **삭제** |
| `AttackPositionManager.ClaimAttackSlot` | **삭제** |
| `UnitMovementUseCase.FindAvailableTile` (2개 오버로드) | **삭제** |

**유지 확정 (V3 흐름에서 그대로 사용):**

| 유지 대상 | 근거 |
|---|---|
| `UnitView` V2 상태 필드 (`_v2MoveSlotTile`, `_v2AttackSlotTargetCoord`, `_v2InStationaryCombat`) | V3에서 필드명 그대로 재사용 |
| `UnitView.ReleaseV2MoveSlotIfClaimed` / `ReleaseV2AttackSlotIfClaimed` | 로직 올바름, V3에서 그대로 호출 |
| `TileOccupancyManager.FindForwardAvailable` | 이미 규칙 5 완전 준수 (fallback BFS 없음) |
| `AttackPositionManager.ClaimByApproach` | 이미 규칙 18 완전 준수 (접근 방향 기준) |
| `UnitMovementUseCase.FindForwardAvailable` | 이미 규칙 5 준수 |
| `UnitMovementUseCase.FindForwardClosestTile` | 이미 규칙 15 준수 |
| `UnitMovementUseCase.RequestMove` / `ProcessStep` / `RegisterOccupancyMove` | 이미 규칙 2/4 준수 |

---

## Step 1 — 기존 코드 비활성화 및 환경 정리

**근거 규칙**: 없음 (구현 전 정리 단계)

### 수정 파일
- `Presentation/Unit/UnitView.cs`
- `Bootstrap/GameBootstrapper.cs`
- `Application/Services/TileSlotManager.cs` (삭제)
- `Application/Services/TileOccupancyManager.cs`
- `Application/Services/AttackPositionManager.cs`
- `Application/UseCases/UnitMovementUseCase.cs`

### 내용

**UnitView.cs:**
1. `MoveAlongPathV2`, `MoveAlongPath` 메서드 전체 삭제.
2. `RunTileTraversal`, `EnterMeleePursuit`, `EnterStationaryCombat` 등 V2 전용 헬퍼 메서드 전체 삭제.
3. V1 전용 상태 필드(`_claimedSlotTile`, `_currentAttackTargetCoord`, `_currentAttackPos`, `_pendingOccupancyTile`) 삭제.
4. V1 전용 cleanup 헬퍼(`ReleaseSlotIfClaimed`, `ReleaseAttackSlotIfClaimed`, `ReleaseOccupancyIfPending`) 삭제.
5. `_slotManager` 필드 및 `SetDependencies`의 `TileSlotManager slotManager` 파라미터 제거.
6. `StopMovement`에서 V1 cleanup 호출 코드 제거.
7. `MoveTo(path)` 호출이 새 코루틴 `MoveAlongPathV3(path)`를 시작하도록 변경.

**TileSlotManager.cs:**
- 파일 삭제 + `GameBootstrapper` 내 관련 초기화/주입 코드 제거.

**TileOccupancyManager.cs:**
- `FindAvailableTile(preferred, unitSize, grid)` 삭제.
- `FindAvailableTile(preferred, unitSize, grid, destination)` 삭제.
- `BfsFindAvailable` 내 useForwardFilter=false fallback 분기 제거 (단독 호출처였던 FindAvailableTile이 사라지므로).

**AttackPositionManager.cs:**
- `ClaimAttackSlot` 삭제.

**UnitMovementUseCase.cs:**
- `FindAvailableTile(preferred, unitSize)` 삭제.
- `FindAvailableTile(preferred, unitSize, destination)` 삭제.

---

## Step 2 — 기본 A* 이동 구현

**근거 규칙**: 규칙 1(기본 목표), 규칙 2(이동 방식), 규칙 4(경로 재계산), 규칙 9(이동 속도)

### 수정 파일
- `Presentation/Unit/UnitView.cs` — `MoveAlongPathV3` 신규 작성

### 내용

`MoveAlongPathV3`는 외부 while 루프로 시작한다.

```
외부 while (IsAlive):
  1. RequestMove(unit, finalTarget) → path 획득
     실패 시: 1프레임 대기 후 재시도 (대기 = yield return null)

  2. path의 각 타일을 순서대로 이동:
     - 타일 센터를 향해 Lerp 이동 (이동 속도: UnitStats)
     - Lerp 완료 시 ProcessStep(from, to) 호출
     - 다음 타일로 반복

  3. finalTarget 도달 시 외부 while 탈출 (공격 루프로 진입 등)
```

**규칙 4 주의사항**:
- 타일 도착마다 다음 타일을 FlowField에서 조회 (path를 한 번에 끝까지 따라가지 않음)
- 건물 건설/파괴 시 `OnPathInvalidated()` → 외부 while 재시작으로 경로 재계산
- 아군 점유 타일은 경로 차단으로 보지 않음 (FlowField는 walkable만 체크)

---

## Step 3 — 점유 시스템 정리

**근거 규칙**: 규칙 5(점유가 가득 찬 타일 처리), 규칙 6(점유 한도)

### 비교 분석 결과

| 메서드 | 규칙 준수 여부 | 처리 |
|--------|--------------|------|
| `CanFit` | ✓ 준수 | 유지 |
| `OnUnitMoved` | ✓ 준수 | 유지 |
| `OnUnitRemoved` | ✓ 준수 | 유지 |
| `ReserveOccupancy` | ✓ 준수 | 유지 |
| `FindForwardAvailable` | ✓ 준수 (fallback BFS 없음) | 유지, V3에서 사용 |
| `FindAvailableTile` (2개) | ✗ 위반 (fallback BFS 있음) | **Step 1에서 삭제** |
| `BfsFindAvailable` | 내부 본체 | useForwardFilter=false 분기 제거 후 유지 |
| `Clear` | ✓ 준수 | 유지 |

### 수정 파일 추가
- `Application/UseCases/UnitMovementUseCase.cs`
  - V3 흐름에서는 `FindForwardAvailable` 래퍼만 사용
  - `FindAvailableTile` 래퍼(2개)는 Step 1에서 삭제

---

## Step 4 — 이동 슬롯 구현

**근거 규칙**: 규칙 7(타일 내 시각 분산), 규칙 10(이동 슬롯), 규칙 17(이동 슬롯 배치)

### 비교 분석 결과

| 항목 | 규칙 준수 여부 | 처리 |
|------|--------------|------|
| 슬롯 정의 (1:앞, 2:뒤좌, 3:뒤우) | ✓ 규칙 17과 일치 | 유지 |
| 슬롯 배정 기준 (가장 가까운 빈 슬롯, 동률→번호 순) | ✓ 규칙 17과 일치 | 유지 |
| `ClaimSlot` | ✓ 준수 | 유지 |
| `ReleaseSlot` | ✓ 준수 | 유지 |
| `GetUnitCount(tile)` | ✗ 미구현 | **추가 필요** |

### TileMoveSlotManager 추가 내용

```
GetUnitCount(tile) → int:
  - _occupied[tile].UnitToSlot.Count 반환
  - 등록된 항목 없으면 0 반환
  - 규칙 10 분기 판단에 사용:
    - 0명: 타일 중심을 이동 목표로
    - 1명 이상: ClaimSlot으로 슬롯 배정
```

### MoveAlongPathV3 슬롯 흐름 (규칙 10)

```
다음 타일 결정 시:
  1. 이전 타일 슬롯 해제 (ReleaseV2MoveSlotIfClaimed 기존 헬퍼 사용)
  2. 새 타일 점유 확인 (FindForwardAvailable) → null이면 대기
  3. 새 타일의 현재 유닛 수 확인 (GetUnitCount):
     - 0명: 타일 중심을 이동 목표로 사용
     - 1명 이상: ClaimSlot으로 슬롯 배정 → 슬롯 월드 좌표를 이동 목표로 사용
  4. 이동 목표를 향해 Lerp 이동
  5. 이동 목표 도달 = 타일 도착으로 판정 → ProcessStep 호출
```

**규칙 7 연동**: 슬롯 위치가 곧 시각적 분산 위치이며, 전투 판정도 이 위치 기준(규칙 8).

---

## Step 5 — 전투 진입 구현

**근거 규칙**: 규칙 8(사거리 판정), 규칙 11(공격 슬롯), 규칙 13(유닛 타입별 진입), 규칙 14(타겟 선택)

### 수정 파일
- `Presentation/Unit/UnitView.cs` — `MoveAlongPathV3` 내 전투 진입 분기

### 타일 이동 중 감지 체크 (규칙 8, 13)

```
각 타일 도착 직후:
  - HasEnemyInDetectRange(unit) 확인 (월드 좌표 기준)
  - true이면 → 유닛 타입별 전투 진입
```

### 근접 유닛 전투 진입 (규칙 13, 11)

```
1. 이동 슬롯 해제 (ReleaseV2MoveSlotIfClaimed — 기존 헬퍼 사용)
2. 타겟 선택 (규칙 14: 처음 감지된 적)
3. 공격 슬롯 배정 (AttackPositionManager.ClaimByApproach → 슬롯 월드 좌표 획득)
4. 슬롯 위치를 향해 직선 이동 (월드 좌표 Lerp, 이동 속도 동일)
5. 전투 거리(AttackRange) 도달 → 공격 루프 진입
6. 매 프레임 DetectRange 확인 → 이탈 시 슬롯 해제 + A* 재개
```

### 원거리 유닛 전투 진입 (규칙 13, 11)

```
감지 사거리 == 공격 사거리 (원거리 유닛 특성)

공격 사거리 내 적 진입 시:
  1. 이동 슬롯 유지 (해제하지 않음)
  2. 현재 이동 슬롯 위치에 물리적으로 스냅
  3. 현재 위치에서 즉시 공격 루프 진입
  4. 공격 중 이동 없음
```

### 타겟 선택 (규칙 14)

```
최초 감지: DetectRange 내 첫 번째 적
타겟 처치 후: DetectRange 내 가장 가까운 적
없으면: A* 재개
```

---

## Step 6 — 공격 슬롯 확인

**근거 규칙**: 규칙 18(공격 슬롯 배치)

### 비교 분석 결과

| 항목 | 규칙 준수 여부 | 처리 |
|------|--------------|------|
| 슬롯 정의 (12방향, 30° 간격) | ✓ 규칙 18과 일치 | 유지 |
| `ClaimByApproach` (접근 방향 기준 배정) | ✓ 규칙 18과 일치 | 유지, V3에서 사용 |
| `ClaimAttackSlot` (배정 수 최소 기준) | ✗ 규칙 18 위반 | **Step 1에서 삭제** |
| `ReleaseAttackSlot` | ✓ 준수 | 유지 |
| `Clear` | ✓ 준수 | 유지 |

V3에서는 `ClaimByApproach`만 호출한다. 별도 구현 없이 기존 메서드를 그대로 사용.

---

## Step 7 — 전투 종료 및 슬롯 해제

**근거 규칙**: 규칙 12(사망 시 슬롯 해제), 규칙 15(전투 종료 후 A* 재개)

### 수정 파일
- `Presentation/Unit/UnitView.cs`

### 근접 유닛 전투 종료 (규칙 15)

```
타겟 처치 또는 DetectRange 이탈:
  1. 공격 슬롯 해제 (ReleaseV2AttackSlotIfClaimed — 기존 헬퍼 사용)
  2. 현재 월드 위치 기준 → 앞쪽(finalTarget 방향) 가장 가까운 walkable 타일 탐색
     (FindForwardClosestTile — UnitMovementUseCase, 이미 규칙 15 준수)
  3. 해당 타일에 이동 슬롯 배정 → A* 재개
```

### 원거리 유닛 전투 종료 (규칙 15)

```
타겟 처치 또는 공격 사거리 이탈:
  이동 슬롯을 유지하고 있으므로 별도 처리 없이 외부 while 루프로 복귀
  → 다음 타일 이동 시작
```

### 사망 처리 (규칙 12)

```
OnEntityDied 이벤트 (기존 구독 구조 유지):
  - 이동 슬롯 해제 (ReleaseV2MoveSlotIfClaimed — 기존 헬퍼 사용)
  - 공격 슬롯 해제 (ReleaseV2AttackSlotIfClaimed — 기존 헬퍼 사용)
  - TileOccupancyManager는 UnitMovementUseCase 내 OnEntityDied 구독이 처리
  - 코루틴 정지
```

---

## 구현 순서 요약

| Step | 작업 | 규칙 | 주요 변경 |
|------|------|------|---------|
| 1 | 기존 코드 정리 | — | V2 삭제, 구 메서드 제거 |
| 2 | 기본 A* 이동 (`MoveAlongPathV3`) | 1, 2, 4, 9 | 신규 작성 |
| 3 | 점유 시스템 정리 | 3, 5, 6 | FindAvailableTile 2개 삭제만 |
| 4 | 이동 슬롯 (`GetUnitCount` 추가) | 7, 10, 17 | GetUnitCount 1개 추가만 |
| 5 | 전투 진입 구현 | 8, 11, 13, 14 | V3에 전투 진입 분기 추가 |
| 6 | 공격 슬롯 확인 | 18 | ClaimAttackSlot 삭제만 (ClaimByApproach 사용) |
| 7 | 전투 종료 및 슬롯 해제 | 12, 15 | V3에 종료 흐름 추가 |

---

## 버그 수정 — 이동 슬롯 GetUnitCount 체크 누락 (2026-05-11)

**발견 시점**: 사용자 플레이 테스트 중 확인  
**규칙 근거**: GameSystemRules.md 규칙 10

### 현상
유닛이 혼자 타일을 이동할 때도 슬롯 오프셋 위치로 이동함. 타일 중심으로 이동해야 함.

### 원인
`UnitView.cs` MoveAlongPathV3 내 이동 목표 결정 코드(약 915번째 줄)에서  
`_moveSlotManager.GetUnitCount(to)` 체크 없이 항상 `ClaimSlot`을 호출하고 있음.

규칙 10: 혼자인 경우 → 타일 중심, 2명 이상인 경우 → 슬롯 위치

### 수정 내용
`Presentation/Unit/UnitView.cs` — 이동 목표 결정 블록

```
수정 전:
  _moveSlotManager가 있으면 → 항상 ClaimSlot 호출

수정 후:
  GetUnitCount(to) == 0 → 기존 슬롯 해제 + 타일 중심을 이동 목표로 사용
  GetUnitCount(to) >= 1 → 기존대로 ClaimSlot 호출
```

---

## 예상 위험 요소

| 위험 | 대응 |
|------|------|
| 멀티플레이: 서버/클라이언트 실행 분기 | 기존 `NetworkContext.IsNetworkServer` 가드 패턴 그대로 유지 |
| `RepathAllAliveUnits` 호출 중 전투 중 유닛 처리 | `_v2InStationaryCombat` 플래그로 repath 수신 차단 (BUG-007 방향 유지) |
| 기존 `UnitCombatUseCase` / `NetworkCombatController` 전투 로직 연동 | 공격 트리거(`HasEnemyInRange`, `TryAttack`) 인터페이스는 변경 없음 |
| Phase 2 스냅 관련 코드 (`ProcessStep from==to`) | V3에서 동일한 from≠to 가드 유지 |
| `BfsFindAvailable` 내부 fallback 분기 제거 | `FindAvailableTile` 삭제 후 해당 분기는 호출처가 없으므로 같이 제거 |
