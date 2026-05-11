# QA-Fix 반복 로그 — movement-combat-redesign

---

## Round 1 — 2026-04-30

### [QA] 발견된 문제

---

- **BUG-001: [SINGLE-01, SINGLE-14] — TileMoveSlotManager 와이어링 누락으로 슬롯 분산 전체 불작동**
  - 관련 파일: `Bootstrap/GameBootstrapper.cs:1046~1048`, `Infrastructure/Factories/UnitFactory.cs:287~289`
  - 원인 분석:
    `GameBootstrapper.SetupProduction()`에서 `_unitFactory.SetDependencyReferences()`를 호출할 때 마지막 파라미터인 `moveSlotManager`에 `_moveSlotManager`를 전달하지 않았다. `UnitFactory.SetDependencyReferences()`는 이 인자를 선택적 파라미터로 받으므로 기본값 null이 설정된다. 그 결과 `_depMoveSlotManager == null` 상태가 된다.
    `UnitFactory.CreateUnitObject()`에서도 `unitView.SetDependencies()` 호출 시 `_depMoveSlotManager`를 전달하지 않는다. 모든 UnitView에서 `_moveSlotManager == null` 상태가 되어, `RunTileTraversal` 내의 슬롯 클레임 분기가 `else`(타일 중심 사용)로 폴백된다. 결과적으로 이동 슬롯 분산 기능 전체가 작동하지 않고 모든 유닛이 타일 중심으로 이동하여 겹침 현상이 발생한다.
  - 수정 방향: `SetupProduction()`의 `SetDependencyReferences()` 호출에 `_moveSlotManager` 인자를 추가. `CreateUnitObject()`의 `SetDependencies()` 호출에도 `_depMoveSlotManager` 인자를 추가.

---

- **BUG-002: [SINGLE-09] — ResumeFromForwardTile 후 transform.position 불일치로 시각적 순간이동 발생**
  - 관련 파일: `Presentation/Unit/UnitView.cs` (ResumeFromForwardTile 메서드, MoveAlongPathV2, RunTileTraversal)
  - 원인 분석:
    근접 전투 종료 후 `ResumeFromForwardTile()`이 호출되면, `_unitData.Position`은 `ProcessStep()`을 통해 forwardTile로 갱신되지만 `transform.position`은 공격 슬롯 위치(적 근처 월드 좌표)를 그대로 유지한다.
    `MoveAlongPathV2`에서 `V2Result.Repath`로 `RunTileTraversal`이 재진입될 때, `prevActualTile = _unitData.Position`은 forwardTile이지만 첫 번째 타일 Lerp의 출발점인 `fromPos = transform.position`은 공격 슬롯 위치이다. 두 좌표가 크게 다를 경우, 1칸에 해당하는 이동 시간 안에 공격 슬롯 위치에서 forwardTile+1의 슬롯 위치까지 건너뛰는 것처럼 보여 순간이동처럼 나타난다.
  - 수정 방향: `ResumeFromForwardTile()` 내에서 `_unitData.Position`을 forwardTile로 갱신할 때 `transform.position`도 `ViewConverter.ToView(HexMetrics.HexToWorld(forwardTile))` + `UnitYOffset`으로 함께 갱신해야 한다. 또는 `MoveAlongPathV2` 외부 while 루프에서 Repath 진입 직후 `transform.position`을 새 경로 시작점 좌표로 스냅하는 방식도 검토 가능.

---

- **BUG-003: [SINGLE-03] — FindForwardAvailable 무한 대기 — 교착 가능성 (CONDITIONAL PASS)**
  - 관련 파일: `Presentation/Unit/UnitView.cs` (RunTileTraversal 내 대기 루프)
  - 원인 분석:
    전방 타일이 모두 가득 찬 상황에서 `while (actualTo == null && _unitData.IsAlive)` 루프가 무한 반복된다. 유닛이 살아있는 동안 매 프레임 `FindForwardAvailable`을 재시도하며 대기하는 설계는 의도적이다. 그러나 대기 중인 유닛들이 서로의 타일 자리를 점유하여 아무도 빠져나가지 못하는 교착(deadlock)이 이론상 발생 가능하다. 이 상황에서는 사망 외에 대기에서 탈출하는 경로가 없다.
  - 수정 방향: 실기에서 최대 인구 상황 재현 후 교착 여부 확인. 교착이 발생하면 대기 시간 제한(예: N프레임 이상 대기 시 현재 위치에서 멈추고 경고 로그 출력) 추가 검토.

---

- **BUG-004: [SINGLE-10] — 공격 슬롯 fallback 무제한 쌓임 (CONDITIONAL PASS)**
  - 관련 파일: `Application/Services/AttackPositionManager.cs:ClaimByApproach()`
  - 원인 분석:
    `ClaimByApproach()`에서 12방향 슬롯이 모두 가득 찬 경우 `MaxUnitsPerSlot` 제한 없이 가장 적게 배정된 슬롯에 계속 추가 배정한다. 현재 `MaxUnitsPerSlot` 상수는 정보 제공용으로만 선언되어 있고 실제 배정 차단에 사용되지 않는다. 한 슬롯에 이론상 무제한 유닛이 쌓일 수 있으나, 현실적인 게임 인구 규모에서는 거의 발생하지 않을 수 있다.
  - 수정 방향: 실기에서 대규모 전투 상황에서 겹침 정도를 관찰. 허용 범위를 벗어나면 `MaxUnitsPerSlot` 기반 상한 설정 및 대기 로직 추가 검토.

---

### [DEV] 수정 내용

- BUG-001:
  - `Bootstrap/GameBootstrapper.cs:1046~1052` — `SetupProduction()`의 `_unitFactory.SetDependencyReferences()` 호출 마지막 인자에 `_moveSlotManager` 추가. 기존 호출은 8개 인자(끝이 `_attackPositionManager`)에서 끝나 `moveSlotManager` 선택 파라미터가 기본값 null로 전달되었음. 이제 9번째 인자로 `_moveSlotManager`를 명시적으로 전달.
  - `Infrastructure/Factories/UnitFactory.cs:287~293` — `CreateUnitObject()`에서 `unitView.SetDependencies()` 호출 시 마지막 인자에 `_depMoveSlotManager` 추가. 서버/싱글플레이 경로에서 새 이동 슬롯 매니저가 UnitView에 정상 주입됨.
  - `Infrastructure/Factories/UnitFactory.cs:378~385` — `InitializeUnitView()`(클라이언트 경로)에서도 `unitView.SetDependencies()` 호출 시 `_depMoveSlotManager` 추가. 서버 경로와 동일한 의존성을 클라이언트 측 UnitView에도 주입.

- BUG-002:
  - `Presentation/Unit/UnitView.cs:2625~2648` — `ResumeFromForwardTile()` 내부에서 `_unitData.ClaimedTile = null;` 직후 `transform.position`을 forwardTile의 뷰 좌표로 강제 스냅하는 블록 추가. `HexMetrics.HexToWorld(forwardTile)` → `ViewConverter.ToView()` → `+ HexMetrics.UnitYOffset` 순서로 변환하여 Red팀 반전과 Y 오프셋을 모두 정확히 반영. 다음 Lerp 출발점이 forwardTile 중앙이 되어 1칸 이동 시간 안의 점프(시각적 순간이동) 현상 제거.

---

## Round 2 — 2026-05-06

### 확인 사항 (이전 수정 검증)

- **BUG-001, BUG-002 PASS**: TileMoveSlotManager 와이어링 정상 동작 확인. ResumeFromForwardTile transform.position 스냅 정상 동작 확인.
- **AttackKind 설정 오류 FIXED**: 유닛 스탯 재설정으로 EmberSpirit(Melee)/Pistoleer(Ranged) 정상 동작 확인. 로그 근거: EmberSpirit `kind=Melee` → MELEE_PURSUIT_START, Pistoleer `kind=Ranged` → STATIONARY_COMBAT_START.

---

### [QA] 발견된 문제

- **BUG-005: [MULTI-01, MULTI-02] — Lerp 진행 중 전투 진입 시 이동 슬롯 미해제 + 유닛이 타일 사이에 정지**
  - 관련 파일: `Presentation/Unit/UnitView.cs` (RunTileTraversal 내 ENEMY_DETECTED 체크, EnterStationaryCombat)
  - 원인 분석:
    `RunTileTraversal`에서 타일 A → B로 Lerp 진행 중 `ENEMY_DETECTED`가 발동하여 `EnterStationaryCombat`을 호출한다. 이 시점에 타일 B의 이동 슬롯(`SLOT_CLAIM`)은 이미 선점된 상태이나, `EnterStationaryCombat` 진입 시 타일 B 슬롯을 해제하지 않는다. 또한 `ProcessStep`이 아직 호출되지 않아 `_unitData.Position`은 여전히 타일 A를 가리킨다. 유닛은 A와 B 사이 물리 위치에서 Lerp가 중단된 채 멈추고, 타일 B의 슬롯은 유령 점유 상태로 남는다.
    로그 근거: `[UnitID:4] SLOT_CLAIM tile=(5,8)` → `[UnitID:4] STATIONARY_COMBAT_START currentTile=(5,7)` → `[UnitID:4] UNIT_DIED position=(5,7)` → `[UnitID:4] SLOT_RELEASE tile=(5,8)`. 전투 전체를 (5,7)과 (5,8) 사이 허공에서 수행하다 사망. 동일 패턴: UnitID 4, 8, 12, 16, 24 (Pistoleer 전체에서 반복). 유닛이 타일 중심이 아닌 허공에 멈추므로 "타일과 별개의 길로 이동하는 현상"이 발생하고, 유령 슬롯이 다른 유닛을 우회시켜 "유닛 뭉침"을 유발한다.
  - 수정 방향:
    `EnterStationaryCombat` 진입 직전(또는 `ENEMY_DETECTED` 처리 분기에서) 현재 Lerp 중인 목적지 슬롯을 즉시 해제하고, `transform.position`을 `_unitData.Position`(현재 논리 타일 = 타일 A)의 월드 좌표로 스냅한다. 유닛이 타일 A 위에 정확히 위치한 상태로 정지전투에 진입하면 슬롯 B는 해제되어 다른 유닛이 사용할 수 있다.

---

- **BUG-006: [MULTI-01, MULTI-03] — RESUME_FORWARD 후 즉시 ENEMY_DETECTED → 이동 슬롯 미해제 + 유닛 타일 사이에 정지**
  - 관련 파일: `Presentation/Unit/UnitView.cs` (ResumeFromForwardTile, RunTileTraversal 재진입 후 적 감지 분기)
  - 원인 분석:
    전투 종료 후 `ResumeFromForwardTile()`이 새 경로를 계산하고 `RunTileTraversal`을 재진입한다. 재진입 직후 다음 타일(N)의 이동 슬롯을 선점(`SLOT_CLAIM`)하고 Lerp를 시작한다. 이 시점에 감지 범위 내 적이 존재하면(`Melee-InRange` 또는 `Melee` 분기) 슬롯 N을 반환하지 않은 채 전투에 진입한다. 유닛은 물리적으로 resumeFrom 타일과 N 사이에 멈추며 슬롯 N이 유령 점유된다.
    로그 근거: `[UnitID:15] RESUME_FORWARD` → `[TILE_MOVE from=(5,6) to=(5,5)]` → `[SLOT_CLAIM tile=(5,5)]` → `[ENEMY_DETECTED]` → `[COMBAT_START mode=Melee-InRange currentTile=(5,6)]`. 전투 종료 시 슬롯 (5,5) 해제. 동일 패턴: UnitID 15, 14, 13, 6이 동일 타깃 사망 시 동시 발생하여 다수 유령 슬롯이 한꺼번에 생성된다.
  - 수정 방향:
    `ResumeFromForwardTile()` 또는 `RunTileTraversal` 재진입 직전에 감지 범위 내 적 존재 여부를 사전 체크한다. 적이 있으면 `TILE_MOVE`/`SLOT_CLAIM` 없이 바로 해당 전투 분기(`EnterMeleePursuit` 또는 `EnterStationaryCombat`)로 진입한다. 이동 슬롯을 선점하지 않으면 유령 점유 자체가 발생하지 않는다.

---

- **BUG-007: [MULTI-04] — Eager Repath가 전투 중 유닛에 OnPathInvalidated 호출 → MoveAlongPathV2 이중 코루틴 실행**
  - 관련 파일: `Bootstrap/GameBootstrapper.cs` (RepathAllAliveUnits, SetupEagerRepathOnBuildingChanges), `Presentation/Unit/UnitView.cs` (OnPathInvalidated, MoveAlongPathV2)
  - 원인 분석:
    건물 배치/파괴 이벤트 발생 시 `RepathAllAliveUnits()`가 전투 중 유닛을 포함한 모든 살아있는 유닛에 `OnPathInvalidated()`를 호출한다. `OnPathInvalidated()`는 `MoveTo(newPath)`를 통해 `StartCoroutine(MoveAlongPathV2(...))`를 실행한다. 이때 이미 `STATIONARY_COMBAT` 상태로 실행 중인 코루틴(1번)을 종료하지 않으므로 `MoveAlongPathV2` 코루틴이 동시에 2개 실행된다. 2번 코루틴은 현재 위치에서 새 경로를 계산하고 다음 타일로의 TILE_MOVE를 시작하여 이동 슬롯을 추가 선점한다.
    로그 근거: `[UnitID:1] STATIONARY_COMBAT_START at tile=(5,8)` → 00:12:08 시각에 모든 유닛 동시 PATH_CALC 발생 → `[UnitID:1] UNIT_DIED position=(5,8) hadMoveSlot=True SLOT_RELEASE tile=(5,9)`. 전투 중인 (5,8)에서 (5,9) 슬롯을 추가 획득한 것이 이중 코루틴의 증거이다.
  - 수정 방향:
    `RepathAllAliveUnits()`에서 현재 전투 상태(공격 슬롯을 보유 중이거나 전투 플래그가 활성화된)인 유닛은 건너뛴다. 또는 `OnPathInvalidated()`에서 전투 상태를 확인하여 전투 중이면 새 코루틴을 시작하지 않는다.

---

- **BUG-008: [MULTI-05] — DETOUR 목적지가 현재 타일로 선택되어 Lerp 거리 0 발생**
  - 관련 파일: `Presentation/Unit/UnitView.cs` (RunTileTraversal DETOUR 분기), `Application/Services/TileOccupancyManager.cs` (FindForwardAvailable)
  - 원인 분석:
    `FindForwardAvailable`이 전방 타일이 모두 가득 찬 상황에서 우회 후보로 현재 유닛이 위치한 타일 자체를 반환한다. 예시: UnitID:11이 (7,8)에 TILE_ARRIVED 한 직후 `DETOUR originalTo=(6,8) detourTo=(7,8)` 이 발생하여 `TILE_MOVE from=(7,8) to=(7,8)`이 실행된다. Lerp 거리가 0이므로 즉시 완료되어 시각적으로 유닛이 순간이동한 것처럼 보이고, 슬롯 클레임이 중복 발생한다.
    로그 근거: `[UnitID:11] TILE_ARRIVED tile=(7,8)` → `[DETOUR] originalTo=(6,8) detourTo=(7,8)` → `[TILE_MOVE] from=(7,8) to=(7,8)` → `[SLOT_CLAIM] tile=(7,8) slot=3`.
  - 수정 방향:
    `FindForwardAvailable`에서 유닛의 현재 위치 타일(`prevActualTile`)을 우회 후보에서 제외한다. 또는 `RunTileTraversal`에서 `actualTo == prevActualTile`인 경우를 감지하여 대기(`yield return null`)로 전환한다.
