# Plan — OnEntityDied 이벤트 분리

## 이 작업은 무엇이고, 왜 하는가

지금까지 "엔티티가 죽었다"는 신호 하나로 유닛 사망과 건물 사망을 모두 표현해
왔다. 그러다 보니 신호를 듣는 시스템마다 "이게 유닛인가, 건물인가?"를 코드 안에서
다시 확인해야 했고, 같은 분기 코드가 9곳에 흩어졌다. 이번 작업은 그 단일 신호를
**건물 사망 신호(`OnBuildingDied`)** 와 **유닛 사망 신호(`OnUnitDied`)** 로 명확히
갈라서, 각 시스템이 자기에게 필요한 신호만 듣도록 정리한다. 코드만 봐도 "어떤 시스템이
어떤 사망에 반응하는지"가 명확해지고, 무용한 콜백 호출도 사라진다.

이 문서는 그 분리를 **어떻게**, **어떤 순서로**, **어떤 위험을 안고** 진행할지를
정한다. 작업이 끝나면 `OnEntityDied`는 완전히 제거된다(하위 호환 코드 없음).

---

## 1. 분리 설계

### 1-1. 이벤트 이름
- `GameEvents.OnUnitDied : Subject<UnitDiedEvent>`
- `GameEvents.OnBuildingDied : Subject<BuildingDiedEvent>`

기존 `OnEntityDied`는 **삭제**한다 (하위 호환 미유지 — 근거는 §4 참조).

### 1-2. DTO 구조

기존 `EntityDiedEvent`는 `Entity (IDamageable)` 1개 필드를 들고 있었고, 모든
구독자가 캐스팅을 통해 실제 타입을 알아냈다. 분리 후에는 발행 시점에 이미 타입이
결정되어 있으므로, 강타입(concrete type) 필드로 교체한다.

```csharp
/// <summary>
/// 유닛 사망 이벤트 데이터.
/// 발행: UnitCombatUseCase (싱글/서버), NetworkCombatController.HandleUnitDied (멀티 클라이언트 재발행)
/// 구독: UnitView (자신 GO 파괴), ProductionTicker (siege 목록 정리),
///       NetworkCombatController (서버 → ClientRpc 전파)
/// </summary>
public readonly struct UnitDiedEvent
{
    public readonly UnitData Unit;
    public UnitDiedEvent(UnitData unit) { Unit = unit; }
}

/// <summary>
/// 건물 사망 이벤트 데이터. 전투에 의한 파괴와 플레이어 철거 모두 포함.
/// 발행: UnitCombatUseCase, BuildingPlacementUseCase.DemolishBuilding,
///       NetworkCombatController.HandleBuildingDied
/// 구독: BuildingFactory, GameEndUseCase, FlowFieldService, GameBootstrapper,
///       ProductionTicker (생산건물 해제·walkable 훅), HexGridRenderer (금광 재표시),
///       NetworkCombatController (서버 → ClientRpc 전파)
/// </summary>
public readonly struct BuildingDiedEvent
{
    public readonly BuildingData Building;
    public BuildingDiedEvent(BuildingData building) { Building = building; }
}
```

**결정 근거**:
- 강타입 DTO로 두면 구독자에서 캐스팅이 사라지고, 컴파일러가 "잘못된 이벤트에 잘못된
  필드 접근"을 잡아준다.
- `EntityDamagedEvent`가 여전히 IDamageable + IsUnit 플래그를 유지하는 것과는
  별개로, 사망 이벤트는 "발행 측에서 이미 종류가 확정되어 있다"는 점에서 통합 DTO를
  유지할 이유가 없다.

### 1-3. 기존 `EntityDiedEvent` 처리
- **삭제**한다. 단일 커밋 안에서 정의·발행·구독을 모두 새 이벤트로 교체.
- "하위 호환 어댑터"(`OnEntityDied`를 동시에 발행)는 두지 않는다. 어댑터를 두면
  "분기 코드를 줄인다"는 이번 작업의 목적이 무력화되기 때문.

---

## 2. 발행 측 변경

### (P1) `UnitCombatUseCase.TryAttack` — `UnitCombatUseCase.cs:787`
```csharp
// AS-IS
GameEvents.OnEntityDied.OnNext(new EntityDiedEvent(target));

// TO-BE
if (target is UnitData u)
    GameEvents.OnUnitDied.OnNext(new UnitDiedEvent(u));
else if (target is BuildingData b)
    GameEvents.OnBuildingDied.OnNext(new BuildingDiedEvent(b));
```

> `target`은 `IDamageable`. 같은 메서드 안의 바로 다음 줄(`if (target is UnitData u) ...`)에서
> 이미 동일한 캐스팅을 하고 있으므로, 분기는 한 번만 작성하고 변수 `u`/`b`를 재사용해도 된다.

### (P2) `BuildingPlacementUseCase.DemolishBuilding` — `BuildingPlacementUseCase.cs:300`
```csharp
// AS-IS
GameEvents.OnEntityDied.OnNext(new EntityDiedEvent(building));

// TO-BE
GameEvents.OnBuildingDied.OnNext(new BuildingDiedEvent(building));
```
- XML 주석의 "OnEntityDied 이벤트 발행" 문구도 "OnBuildingDied" 로 갱신.
- `RemoveBuilding` XML 주석의 "철거 시에는 OnEntityDied 발행이 포함된 DemolishBuilding을 사용할 것" → "OnBuildingDied 발행"으로 갱신.

### (P3) `NetworkCombatController.HandleUnitDied` — `NetworkCombatController.cs:750`
```csharp
// AS-IS
GameEvents.OnEntityDied.OnNext(new EntityDiedEvent(unit));

// TO-BE
GameEvents.OnUnitDied.OnNext(new UnitDiedEvent(unit));
```

### (P4) `NetworkCombatController.HandleBuildingDied` — `NetworkCombatController.cs` (786~787 인근)
```csharp
// AS-IS
GameEvents.OnEntityDied.OnNext(new EntityDiedEvent(building));

// TO-BE
GameEvents.OnBuildingDied.OnNext(new BuildingDiedEvent(building));
```

### RPC 시그니처는 그대로 유지
`EntityDiedClientRpc(int entityId, bool isUnit)` — **변경하지 않는다.**
- 이유: RPC는 와이어 포맷이라 변경 시 호환성 영향이 크다. 서버는 `entityId/isUnit`을
  이미 분리해 보내고 있고, 클라이언트는 `HandleUnitDied`/`HandleBuildingDied`로 받자마자
  새 이벤트로 분기 발행할 수 있어 분리 효과를 손해보지 않는다.
- (옵션) 향후 별도 정리 작업으로 `UnitDiedClientRpc(int)`, `BuildingDiedClientRpc(int)`로
  쪼개는 것은 가능하지만, 본 작업 범위에 포함하지 않는다.

---

## 3. 구독 측 변경

### (S1) `BuildingFactory.Awake` — `BuildingFactory.cs:113~127`
```csharp
// AS-IS
GameEvents.OnEntityDied
    .Subscribe(e =>
    {
        if (e.Entity is not BuildingData building) return;
        if (_buildingObjects.TryGetValue(building.Id, out var go) && go != null)
        {
            _buildingObjects.Remove(building.Id);
            Destroy(go);
        }
    })
    .AddTo(this);

// TO-BE
GameEvents.OnBuildingDied
    .Subscribe(e =>
    {
        if (_buildingObjects.TryGetValue(e.Building.Id, out var go) && go != null)
        {
            _buildingObjects.Remove(e.Building.Id);
            Destroy(go);
        }
    })
    .AddTo(this);
```
- 타입 필터 라인 1줄이 통째로 사라진다.

### (S2) `UnitView.SubscribeEvents` — `UnitView.cs:377~`
```csharp
// AS-IS
GameEvents.OnEntityDied
    .Subscribe(e =>
    {
        if (_unitData != null && e.Entity == (IDamageable)_unitData)
        { ... Destroy(gameObject); }
    })
    .AddTo(this);

// TO-BE
GameEvents.OnUnitDied
    .Subscribe(e =>
    {
        if (_unitData != null && e.Unit == _unitData)
        { ... Destroy(gameObject); }
    })
    .AddTo(this);
```
- `(IDamageable)` 캐스팅 제거. 인스턴스 동일성 비교는 유지(자기 자신이 죽었는가).

### (S3) `ProductionTicker.OnEntityDied` — `ProductionTicker.cs:173, 396`
하나의 핸들러를 **두 개로 분리**한다.

```csharp
// AS-IS
GameEvents.OnEntityDied
    .Subscribe(OnEntityDied)
    .AddTo(this);
...
private void OnEntityDied(EntityDiedEvent e)
{
    if (e.Entity is BuildingData building && BuildingTypeHelper.IsProductionBuilding(building.Type))
    {
        _productionUseCase.UnregisterBarracks(building.Id);
        DestroyMarker(building.Id);
    }
    if (e.Entity is UnitData unit)
    {
        _siegeUnits.Remove(unit.Id);
    }
}

// TO-BE
GameEvents.OnBuildingDied.Subscribe(OnBuildingDied).AddTo(this);
GameEvents.OnUnitDied.Subscribe(OnUnitDied).AddTo(this);
...
private void OnBuildingDied(BuildingDiedEvent e)
{
    if (BuildingTypeHelper.IsProductionBuilding(e.Building.Type))
    {
        _productionUseCase.UnregisterBarracks(e.Building.Id);
        DestroyMarker(e.Building.Id);
    }
}
private void OnUnitDied(UnitDiedEvent e)
{
    _siegeUnits.Remove(e.Unit.Id);
}
```

### (S4) `ProductionTicker.SubscribeEvents` — walkable 변경 훅 (`ProductionTicker.cs:199~204`)
```csharp
// AS-IS
GameEvents.OnEntityDied
    .Subscribe(e =>
    {
        if (e.Entity is BuildingData) OnWalkableChanged();
    })
    .AddTo(_buildingChangeSubs);

// TO-BE
GameEvents.OnBuildingDied
    .Subscribe(_ => OnWalkableChanged())
    .AddTo(_buildingChangeSubs);
```

### (S5) `GameEndUseCase` — `GameEndUseCase.cs:41~`
```csharp
// AS-IS
_subscription = GameEvents.OnEntityDied.Subscribe(OnEntityDied);
private void OnEntityDied(EntityDiedEvent e)
{
    if (IsGameOver) return;
    if (e.Entity is BuildingData building && building.Type == BuildingType.Castle) { ... }
}

// TO-BE
_subscription = GameEvents.OnBuildingDied.Subscribe(OnBuildingDied);
private void OnBuildingDied(BuildingDiedEvent e)
{
    if (IsGameOver) return;
    if (e.Building.Type == BuildingType.Castle) { ... }
}
```
- 핸들러 이름은 `OnEntityDied` → `OnBuildingDied`로 일관 변경. 외부에서 호출하지 않으므로
  안전.

### (S6) `FlowFieldService` — `FlowFieldService.cs:78~83`
```csharp
// AS-IS
GameEvents.OnEntityDied
    .Subscribe(e => { if (e.Entity is BuildingData) InvalidateAll(); })
    .AddTo(_subscriptions);

// TO-BE
GameEvents.OnBuildingDied
    .Subscribe(_ => InvalidateAll())
    .AddTo(_subscriptions);
```
- 파일 상단 주석의 "GameEvents.OnEntityDied (건물) → InvalidateAll" 문구도
  "GameEvents.OnBuildingDied → InvalidateAll"로 갱신.

### (S7) `GameBootstrapper.SetupEagerRepathOnBuildingChanges` — `GameBootstrapper.cs:733~`
```csharp
// AS-IS
GameEvents.OnEntityDied
    .Subscribe(e => { if (e.Entity is BuildingData) RepathAllAliveUnits(); })
    .AddTo(_eagerRepathSubscriptions);

// TO-BE
GameEvents.OnBuildingDied
    .Subscribe(_ => RepathAllAliveUnits())
    .AddTo(_eagerRepathSubscriptions);
```
- 인근 라인 주석(`FlowFieldService가 OnBuildingPlaced / OnEntityDied(building)에서 InvalidateAll로 ...`)도 같이 갱신.

### (S8) `NetworkCombatController` 서버 측 핸들러 — `NetworkCombatController.cs:118, 469`
하나의 구독을 **두 개로 분리**한다. RPC는 그대로 `EntityDiedClientRpc(entityId, isUnit)` 사용.

```csharp
// AS-IS
_diedSubscription = GameEvents.OnEntityDied.Subscribe(OnEntityDied);
private void OnEntityDied(EntityDiedEvent e)
{
    if (!IsServer) return;
    if (e.Entity is UnitData u) EntityDiedClientRpc(u.Id, true);
    else if (e.Entity is BuildingData b) EntityDiedClientRpc(b.Id, false);
    else Debug.LogWarning(...);
}

// TO-BE
_unitDiedSubscription = GameEvents.OnUnitDied.Subscribe(OnUnitDied);
_buildingDiedSubscription = GameEvents.OnBuildingDied.Subscribe(OnBuildingDied);
private void OnUnitDied(UnitDiedEvent e)
{
    if (!IsServer || e.Unit == null) return;
    EntityDiedClientRpc(e.Unit.Id, true);
}
private void OnBuildingDied(BuildingDiedEvent e)
{
    if (!IsServer || e.Building == null) return;
    EntityDiedClientRpc(e.Building.Id, false);
}
```
- `_diedSubscription` 필드 1개 → `_unitDiedSubscription`/`_buildingDiedSubscription` 2개로 분리.
- `OnNetworkDespawn`의 Dispose 호출도 두 필드 모두 해제하도록 갱신.

### (S9) `HexGridRenderer.SubscribeGoldMineEvents` — `HexGridRenderer.cs:226~`
```csharp
// AS-IS
GameEvents.OnEntityDied
    .Subscribe(e =>
    {
        if (e.Entity is BuildingData building && building.Type == BuildingType.MiningPost)
            ShowGoldMine(building.Position);
    })

// TO-BE
GameEvents.OnBuildingDied
    .Subscribe(e =>
    {
        if (e.Building.Type == BuildingType.MiningPost)
            ShowGoldMine(e.Building.Position);
    })
```
- XML 주석 (b)의 "OnEntityDied" 문구도 "OnBuildingDied"로 갱신.

---

## 4. 기존 `OnEntityDied` 처리 — 하위 호환 미유지

- **결정**: 분리 작업과 동시에 `OnEntityDied`와 `EntityDiedEvent`를 **완전 삭제**한다.
- **근거**:
  1. 모든 발행자와 구독자가 같은 솔루션 안에 있고, 외부 모듈/플러그인이 이 이벤트를
     참조하지 않는다 (전체 검색으로 확인됨 — Research §1·§2).
  2. 어댑터(예: "둘 다 발행")를 두면 무용한 콜백 비용이 영구히 남는다.
     이번 작업의 목적이 그 비용 제거이므로 자기모순.
  3. 단일 PR/커밋 안에서 모든 발행·구독을 새 이벤트로 동시에 교체하면 컴파일러가
     누락된 호출 지점을 즉시 잡아주므로 안전.

---

## 5. 구현 순서 (의존 관계 기준)

분리 작업은 "정의 → 발행 → 구독" 순으로 진행하지 않으면 한순간이라도 컴파일이 깨진다.
이번 작업은 **이벤트를 완전 교체**하므로 한 번에 다음 순서로 진행한다.

1. **GameEvents.cs**
   - `UnitDiedEvent`, `BuildingDiedEvent` struct 추가
   - `OnUnitDied`, `OnBuildingDied` Subject 추가
   - `OnEntityDied`, `EntityDiedEvent` 제거
   - (이 시점에서 전 코드가 컴파일 깨짐 — 의도된 상태)

2. **발행 측 4곳 일괄 교체** (Research §1의 P1~P4)
   - `UnitCombatUseCase.cs`
   - `BuildingPlacementUseCase.cs`
   - `NetworkCombatController.cs` (HandleUnitDied / HandleBuildingDied)

3. **구독 측 9곳 일괄 교체** (Research §2의 S1~S9)
   - 우선순위는 없으나, 다음 순서가 인지 부담이 적다:
     a. 단순 치환(`is BuildingData` 한 줄 제거)인 것부터:
        BuildingFactory, FlowFieldService, GameBootstrapper, HexGridRenderer, GameEndUseCase, UnitView, ProductionTicker(walkable 훅)
     b. 핸들러 두 개로 쪼개는 것:
        ProductionTicker.OnEntityDied, NetworkCombatController 서버 측 핸들러

4. **주석·문서 정리**
   - 영향받는 파일들의 XML 주석/헤더 주석에서 "OnEntityDied" 문자열을 모두
     "OnUnitDied" 또는 "OnBuildingDied"로 갱신.
   - `UnitMovementUseCase.cs:61~71`의 주석 처리된 OnEntityDied 코드도 같은 김에 정리할지 결정.
   - `GameEvents.cs`의 사망 이벤트 섹션 헤더 주석 갱신.

5. **빌드 + 컴파일 에러 0 확인** → QA 단계로 이관.

---

## 6. 위험 요소

### 6-1. 발행자 누락
- 전체 검색(grep) 결과, OnEntityDied 발행 지점은 §1의 4곳뿐이다. 그러나 작업 시
  다시 한 번 다음 패턴으로 grep해야 한다:
  - `OnEntityDied.OnNext`
  - `new EntityDiedEvent`
- 둘 다 0건이 될 때까지 반복 확인.

### 6-2. 구독자 누락
- 누락된 구독자가 있어도 컴파일러가 잡아주지 않는다 (이벤트를 삭제하면
  `Subscribe`를 호출하는 코드가 컴파일 에러를 내므로 결국 잡히긴 하지만,
  주석 안의 문구나 문자열 비교는 잡히지 않음).
- 다음 패턴을 추가로 검색:
  - `GameEvents.OnEntityDied`
  - `EntityDiedEvent`
  - 위 두 키워드가 0건이 되면 코드상 완전 제거 확인.

### 6-3. 멀티플레이 사망 흐름 회귀
- **흐름**: 서버 사망 → 서버 `OnUnitDied`/`OnBuildingDied` 발행 →
  `NetworkCombatController` 서버 핸들러 → `EntityDiedClientRpc` →
  각 클라이언트 `HandleUnitDied`/`HandleBuildingDied` → 클라이언트 `OnUnitDied`/`OnBuildingDied` 재발행.
- 분리 후에도 이 체인이 끊기지 않는지 다음을 점검:
  - 서버에서 두 이벤트 각각이 ClientRpc로 변환되는가? (§3 S8)
  - 클라이언트에서 두 이벤트가 재발행되는가? (§2 P3, P4)
  - 클라이언트의 `BuildingFactory.OnBuildingDied`가 GO를 Destroy하는가? (§3 S1)
  - 클라이언트의 `UnitView.OnUnitDied`가 자신 GO를 Destroy하는가? (§3 S2)

### 6-4. 사망 이벤트 발행 순서
- `UnitCombatUseCase.TryAttack` 안에서 사망 이벤트 발행 직후 같은 메서드에서
  `_unitSpawn.RemoveUnit` / `_buildingPlacement.RemoveBuilding`을 호출한다.
- 새 이벤트로 교체할 때도 **이 순서는 보존**해야 한다 (구독자가 RemoveUnit/RemoveBuilding보다 먼저
  실행되어야 자신 GO를 정상 파괴할 수 있음).

### 6-5. ProductionTicker의 두 구독 분리
- 기존에는 `OnEntityDied` 하나에 두 가지 책임(생산건물 해제 + siege 정리)을 묶어 처리.
- 분리 후 두 핸들러 모두에서 `_productionUseCase == null` 가드를 유지하지 않으면
  싱글플레이 초기화 타이밍에 NRE 위험. 기존 코드와 동일하게 가드 유지.

### 6-6. NetworkCombatController의 구독 해제
- `_diedSubscription` 하나만 Dispose하던 곳에서 `_unitDiedSubscription`,
  `_buildingDiedSubscription` 두 개 모두 Dispose하지 않으면 메모리 누수.
- `OnNetworkDespawn`을 반드시 확인.

### 6-7. 작업 범위 외 동시 변경 금지
- 본 작업은 "이벤트 분리"에만 집중한다. 다음은 본 작업에서 **하지 않는다**:
  - `EntityDamagedEvent`의 `IsUnit` 플래그 분리 (별도 작업으로 분리 가능)
  - `EntityDiedClientRpc` RPC를 두 개로 쪼개기
  - `UnitMovementUseCase.cs:61~71`의 주석 코드 영구 삭제(주석 갱신만)
  - 사망 이벤트와 무관한 리팩토링

---

## 7. 검증 체크리스트 (구현 완료 후 QA가 확인)

- [ ] `OnEntityDied`, `EntityDiedEvent` 문자열이 코드(.cs) 전체에서 0건이다.
- [ ] 컴파일 에러 0, 경고 신규 발생 0.
- [ ] 싱글플레이 — 유닛이 적 유닛을 처치하면 처치된 유닛 GO가 화면에서 사라진다.
- [ ] 싱글플레이 — 유닛이 적 건물을 파괴하면 건물 GO가 사라지고, 그 자리에
      유닛이 진입할 수 있다.
- [ ] 싱글플레이 — Castle 파괴 시 GameEndUI가 표시된다.
- [ ] 싱글플레이 — ProductionPanelUI에서 생산건물(배럭) 철거 → 건물 GO 사라짐,
      골드 환불, 채굴소 위 건물 철거 시 금광이 다시 보임.
- [ ] 멀티플레이 — 서버 측 사망/철거가 모든 클라이언트에서 동일하게 반영된다.
- [ ] 멀티플레이 — Castle 파괴 시 양측에 게임 종료 화면이 표시된다.
- [ ] 건물 파괴 직후 살아있는 유닛들의 경로가 즉시 갱신된다 (`RepathAllAliveUnits` 동작).
- [ ] 채굴소 파괴 시 금광 오브젝트가 다시 표시된다 (`HexGridRenderer.ShowGoldMine`).
- [ ] siege 중인 유닛이 사망하면 siege 목록에서 제거된다 (`ProductionTicker._siegeUnits`).
