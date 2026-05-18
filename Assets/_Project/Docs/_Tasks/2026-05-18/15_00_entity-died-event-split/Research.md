# Research — OnEntityDied 이벤트 분리

## 이 작업은 무엇이고, 왜 하는가

지금까지 우리 게임은 "엔티티(유닛/건물)가 죽었다"라는 한 가지 신호(`OnEntityDied`)를
유닛과 건물이 함께 사용해 왔다. 사망 신호가 발행되면 그것을 듣는 모든 구독자들이
"이게 유닛 사망인가? 건물 사망인가?"를 코드 안에서 직접 확인(`e.Entity is BuildingData …`)
한 다음, 자기에게 해당하는 경우에만 동작하도록 if 문으로 거르고 있다.

이번 "건물 철거" 작업을 진행하면서 `BuildingFactory`가 한 줄 더 같은 패턴을 추가하게 됐다.
즉, 사망 신호의 종류를 구독자 측에서 매번 다시 분류해주는 코드가 또 늘어난 셈이다.
이런 분기 코드가 시스템 전체에 분산되어 있어 "어디서 어떤 사망을 처리하는지"가
한눈에 들어오지 않고, 새 구독자를 추가할 때마다 같은 if 패턴을 또 써야 한다.

이 작업은 사망 신호 자체를 **건물용(`OnBuildingDied`)** 과 **유닛용(`OnUnitDied`)** 으로
명시적으로 갈라서, 구독자가 자기에게 필요한 신호만 듣게 만든다. 결과적으로
- 구독자 측의 타입 체크 코드(`is BuildingData` / `is UnitData`)를 제거하고,
- 어떤 시스템이 어떤 종류의 사망에 반응하는지가 코드만 봐도 분명해지며,
- 앞으로 사망 관련 후속 작업(예: 유닛 사망 특화 이펙트, 건물 사망 특화 보상 처리)을
  더 깔끔하게 분리할 수 있게 된다.

---

## 1. 현재 코드의 OnEntityDied 발행 위치 (누가 언제 쏘는가)

### (P1) `UnitCombatUseCase.TryAttack` — 전투에 의한 사망 (싱글/멀티 공용)
- **파일**: `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs:787`
- **시점**: 데미지 적용 후 `target.IsAlive == false`가 되는 순간
- **발행 내용**: `GameEvents.OnEntityDied.OnNext(new EntityDiedEvent(target))`
  - `target`은 `IDamageable` 타입이며, 실제로는 `UnitData` 또는 `BuildingData`
  - 같은 메서드 내에서 사망 직후 `_unitSpawn.RemoveUnit` 또는
    `_buildingPlacement.RemoveBuilding`을 호출하여 Domain 딕셔너리도 정리한다.
- **컨텍스트**:
  - 싱글플레이: TryAttack 자체가 클라이언트(=로컬)에서 실행됨.
  - 멀티플레이: TryAttack은 서버에서만 실행됨(클라이언트는 즉시 return false).
    그래서 멀티에서는 이 발행이 "서버"에서만 일어난다.

### (P2) `BuildingPlacementUseCase.DemolishBuilding` — 플레이어 철거 (이번 작업에서 신규)
- **파일**: `Assets/_Project/Scripts/Application/UseCases/BuildingPlacementUseCase.cs:300`
- **시점**: ProductionPanelUI의 철거 버튼 또는 NetworkBuildingController의
  `RequestDemolishServerRpc` → `DemolishBuildingClientRpc` 경로에서 호출.
- **발행 내용**: `GameEvents.OnEntityDied.OnNext(new EntityDiedEvent(building))`
  - 항상 `BuildingData` 인스턴스가 들어간다 (유닛 철거는 존재하지 않음).
- **컨텍스트**:
  - 싱글: 로컬 1회 발행.
  - 멀티: 서버에서 1회 + 모든 클라이언트(`DemolishBuildingClientRpc` 안에서 동일 메서드 재호출)에서 각각 1회 발행.
    즉 멀티에서는 동일 buildingId에 대해 (서버) + (각 클라이언트) 모두에서 발행된다.

### (P3) `NetworkCombatController.HandleUnitDied` — 멀티 클라이언트 재발행 (유닛)
- **파일**: `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs:750`
- **시점**: `EntityDiedClientRpc(entityId, isUnit=true)` 수신 후
  로컬 `UnitData`를 찾아 `unit.TakeDamage(unit.Hp)` + `unitSpawn.RemoveUnit`까지 한 뒤
- **발행 내용**: `GameEvents.OnEntityDied.OnNext(new EntityDiedEvent(unit))`
- **컨텍스트**: 멀티 클라이언트에서만 실행 (서버는 이미 P1에서 발행했으므로 재발행 불필요).

### (P4) `NetworkCombatController.HandleBuildingDied` — 멀티 클라이언트 재발행 (건물)
- **파일**: `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs` (HandleBuildingDied 내부, 위 출력 마지막 줄에서 잘림 — 동일 패턴으로 786~787 라인 인근에서 `GameEvents.OnEntityDied.OnNext(new EntityDiedEvent(building))` 발행 후 `RemoveBuilding`)
- **시점**: `EntityDiedClientRpc(entityId, isUnit=false)` 수신 후
- **발행 내용**: `GameEvents.OnEntityDied.OnNext(new EntityDiedEvent(building))`
- **컨텍스트**: 멀티 클라이언트에서만 실행.

> 발행 위치 요약: 사망 신호가 도달하는 "끝점"은 결국 4곳이지만,
> P1·P2(전투/철거)는 "건물·유닛 모두 가능", P3는 "유닛만", P4는 "건물만"으로
> 이미 발행 시점에 종류가 결정되어 있다.

---

## 2. 현재 코드의 OnEntityDied 구독 위치 (누가 받아서 무엇을 하는가)

### (S1) `BuildingFactory.Awake` — 건물 GO 파괴
- **파일**: `Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs:113`
- **현재 필터**: `if (e.Entity is not BuildingData building) return;`
- **동작**: `_buildingObjects[building.Id]` 조회 후 `Destroy(go)` 및 딕셔너리에서 제거.
- **유닛 사망 시**: 즉시 return — 무용한 호출이 매번 들어옴.

### (S2) `UnitView.SubscribeEvents` — 유닛 자신의 GO 파괴
- **파일**: `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs:377`
- **현재 필터**: `if (_unitData != null && e.Entity == (IDamageable)_unitData)` —
  타입 체크가 아니라 **인스턴스 동일성 비교**(자기 자신이 죽었는가)로 분기.
- **동작**: Animator `IsDead=true` + `Destroy(gameObject)`.
- **건물 사망 시**: 인스턴스가 다르므로 무시되지만, 모든 살아있는 유닛 수만큼 콜백이 호출됨.

### (S3) `ProductionTicker.OnEntityDied` — 생산건물 등록 해제 + 마커 + Siege 정리
- **파일**: `Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs:174, 396`
- **현재 동작**:
  - `e.Entity is BuildingData building && IsProductionBuilding(building.Type)` →
    `_productionUseCase.UnregisterBarracks(building.Id)` + `DestroyMarker(building.Id)`
  - `e.Entity is UnitData unit` → `_siegeUnits.Remove(unit.Id)`
- **분리 후에도 둘 다 필요한 유일한 구독자**.

### (S4) `ProductionTicker.SubscribeEvents` — 건물 walkable 변경 알림용 별도 구독
- **파일**: `Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs:199`
- **현재 필터**: `if (e.Entity is BuildingData) OnWalkableChanged();`
- **동작**: 현재는 빈 훅(`OnWalkableChanged()`). 향후 혼잡도 후처리 자리.

### (S5) `GameEndUseCase.OnEntityDied` — Castle 파괴 → 게임 종료
- **파일**: `Assets/_Project/Scripts/Application/UseCases/GameEndUseCase.cs:41, 45`
- **현재 필터**: `if (e.Entity is BuildingData building && building.Type == BuildingType.Castle)`
- **동작**: 승자 결정 후 `GameEvents.OnGameEnd.OnNext(...)`

### (S6) `FlowFieldService` — 건물 사망 시 경로 캐시 무효화
- **파일**: `Assets/_Project/Scripts/Application/Services/FlowFieldService.cs:78`
- **현재 필터**: `if (e.Entity is BuildingData) InvalidateAll();`
- **동작**: 모든 캐시 경로를 무효화.

### (S7) `GameBootstrapper.SetupEagerRepathOnBuildingChanges` — Eager 재경로
- **파일**: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:733`
- **현재 필터**: `if (e.Entity is BuildingData) RepathAllAliveUnits();`
- **동작**: 살아있는 모든 유닛에 `OnPathInvalidated()` 호출.

### (S8) `NetworkCombatController.OnEntityDied` — 서버 → ClientRpc 전파
- **파일**: `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs:118, 469`
- **현재 동작**:
  - `IsServer`일 때만 활성.
  - `e.Entity is UnitData` → `entityId=u.Id, isUnit=true`
  - `e.Entity is BuildingData` → `entityId=b.Id, isUnit=false`
  - 그 외 → 경고 로그 후 무시.
  - `EntityDiedClientRpc(entityId, isUnit)` 호출로 모든 클라이언트에 전파.
- **분리 후에도 둘 다 필요**한 구독자 (S3과 동일하게).

### (S9) `HexGridRenderer` — 채굴소 파괴 시 금광 오브젝트 재표시
- **파일**: `Assets/_Project/Scripts/Presentation/Grid/HexGridRenderer.cs:226`
- **현재 필터**: `if (e.Entity is BuildingData building && building.Type == BuildingType.MiningPost)`
- **동작**: `ShowGoldMine(building.Position)`

> 구독자 요약: 9개 구독 지점 중 7개는 **건물 또는 유닛 한쪽만** 필요로 한다.
> 둘 다 분기해서 사용하는 곳은 `ProductionTicker.OnEntityDied`(S3)와
> `NetworkCombatController.OnEntityDied`(S8) 두 군데뿐이다.

---

## 3. 구독자별 현재 분기/필터 요약 표

| # | 구독자 | 필터 | 분리 후 어느 이벤트만 들으면 되는가 |
|---|---|---|---|
| S1 | BuildingFactory | `is BuildingData` | `OnBuildingDied`만 |
| S2 | UnitView | `e.Entity == _unitData` (인스턴스 비교) | `OnUnitDied`만 |
| S3 | ProductionTicker.OnEntityDied | `is BuildingData` 또는 `is UnitData` | **둘 다** (분리 후 핸들러 두 개로) |
| S4 | ProductionTicker (walkable 훅) | `is BuildingData` | `OnBuildingDied`만 |
| S5 | GameEndUseCase | `is BuildingData && Castle` | `OnBuildingDied`만 |
| S6 | FlowFieldService | `is BuildingData` | `OnBuildingDied`만 |
| S7 | GameBootstrapper (eager repath) | `is BuildingData` | `OnBuildingDied`만 |
| S8 | NetworkCombatController(서버) | 둘 다 분기 | **둘 다** (분리 후 핸들러 두 개로) |
| S9 | HexGridRenderer | `is BuildingData && MiningPost` | `OnBuildingDied`만 |

---

## 4. 이벤트 분리 시 영향받는 파일 목록

### 정의 (1)
- `Assets/_Project/Scripts/Application/Events/GameEvents.cs`

### 발행 측 (4)
- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`
- `Assets/_Project/Scripts/Application/UseCases/BuildingPlacementUseCase.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
  (HandleUnitDied / HandleBuildingDied 두 군데)

### 구독 측 (8)
- `Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs` (두 군데)
- `Assets/_Project/Scripts/Application/UseCases/GameEndUseCase.cs`
- `Assets/_Project/Scripts/Application/Services/FlowFieldService.cs`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs` (서버 측 OnEntityDied 핸들러)
- `Assets/_Project/Scripts/Presentation/Grid/HexGridRenderer.cs`

### 영향 없음 (참고)
- `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs:61~71`
  — 이미 주석 처리된 OnEntityDied 코드. 정리는 별개 작업.

---

## 5. 분리하지 않을 경우의 문제점

1. **분기 코드의 분산**
   현재 9개 구독 지점 중 7개가 똑같은 `is BuildingData` / `is UnitData` 분기를
   반복하고 있다. 새 구독자가 추가될 때마다 같은 분기 코드가 또 늘어난다.

2. **무용한 콜백 비용**
   유닛 한 마리가 죽을 때마다 BuildingFactory·HexGridRenderer·FlowFieldService·
   GameBootstrapper·GameEndUseCase 등 "건물 전용 구독자"가 전부 한 번씩 깨워졌다가
   필터에 걸려 return된다. 반대로 건물 하나가 죽을 때마다 화면에 살아있는 모든
   UnitView가 인스턴스 비교에 들어간다. 이벤트 빈도가 높을수록 누적 비용이 커진다.

3. **의도 표현 부족**
   "사망 신호"라는 한 단어가 두 가지 의미(전투 결과 / 플레이어 철거 / 유닛 처치 / 건물 파괴)를
   섞어 전달하기 때문에, 새로 합류한 개발자가 사망 처리 흐름을 파악하려면
   모든 구독자의 필터를 읽고 머릿속에서 다시 분류해야 한다.

4. **확장성 제약**
   향후 "유닛 사망에만 발생할 효과"(예: 영혼 회수, 처치 시 마나 회복)나
   "건물 사망에만 발생할 효과"(예: 잔해 표시, 폭발 사운드)를 추가할 때마다
   매번 분기 코드를 새로 작성해야 한다.

5. **레이어 일관성**
   이미 `EntityDamagedEvent`에는 `IsUnit (bool)` 필드가 들어가 있어, "엔티티 단일 이벤트"가
   분리되어야 한다는 신호가 코드에도 남아있다. 사망 이벤트만 통합 상태로 두는 것은
   설계 의도와 어긋난다.

---

## 6. 분리 설계 시 고려해야 할 사실 (Plan 단계로 넘기는 입력)

- `EntityDiedEvent`는 `Entity (IDamageable)` 1개 필드만 가짐. 구독자에서 형변환 필수.
- `OnEntityDied`는 `Subject<EntityDiedEvent>` 형태. UniRx 표준.
- 멀티플레이에서 사망 이벤트는 (서버 발행 1회) + (각 클라이언트 재발행 1회) 구조.
  분리 후에도 이 흐름 자체는 보존되어야 한다.
- `NetworkCombatController.OnEntityDied`는 서버에서 둘을 분기해 `EntityDiedClientRpc(entityId, isUnit)`로 통합 RPC를 보내고 있다. RPC 시그니처를 그대로 둘지(추천), 두 개 RPC로 쪼갤지는 Plan에서 결정.
- `BuildingFactory`의 OnEntityDied 구독은 이번 "건물 철거" 작업에서 추가됐고, 아직 메인 브랜치에 커밋 안 된 상태. 분리 작업 시 같이 정리하면 깔끔.
- `HexGridRenderer.SubscribeGoldMineEvents`의 주석 (a)/(b)도 함께 갱신 필요.
