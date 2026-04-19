# Plan — 랠리포인트 무시 버그 수정

## 수정 대상 버그

- **BUG-A**: 멀티플레이 Client의 랠리포인트 설정이 서버에 전달되지 않음
- **BUG-B**: UnitView 초기화 전 OnUnitProduced 발행으로 랠리 이동 누락

---

## BUG-A 수정 계획

### 접근법

`NetworkProductionController`에 `SetRallyPointServerRpc` 추가.
`ProductionPanelUI.CompleteRallyPointSetting()`에서 네트워크 유무에 따라 분기:
- 싱글플레이 / Host: 기존대로 UseCase 직접 호출
- 멀티플레이 Client: `SetRallyPointServerRpc` 호출

### 수정할 파일

#### 1. `NetworkProductionController.cs`

`SetRallyPointServerRpc` 추가:
- `barracksId`, `q`, `r`, `teamIndex` 파라미터 수신
- 팀 소유권 검증 (기존 ServerRpc와 동일한 패턴)
- `UnitProductionUseCase.SetRallyPoint(barracksId, coord)` 실행
- 서버 로컬 `ProductionState.RallyPoint` 갱신됨
- **ClientRpc 불필요**: 랠리포인트는 시각 마커(ProductionTicker)와 생산 완료 시 참조용이므로, 서버 상태만 정확하면 됨. 마커 표시는 클라이언트가 로컬로 처리해도 무방.

#### 2. `ProductionPanelUI.cs`

`CompleteRallyPointSetting()` 수정:
- `NetworkManager.Singleton`이 존재하고 `IsClient && !IsHost`이면 → `SetRallyPointServerRpc` 호출
- 그 외 (싱글 / Host) → 기존대로 `_production.SetRallyPoint()` 직접 호출
- `NetworkProductionController` 참조를 주입받거나 `FindFirstObjectByType`으로 탐색

> **아키텍처 주의**: `ProductionPanelUI`가 `NetworkProductionController`에 직접 접근하는 것은 Presentation → Infrastructure 의존이 생김. 이미 `NetworkProductionController`를 다른 ServerRpc 호출에서도 참조하고 있다면 동일한 패턴. 현재 구조에서 다른 방법이 없으므로 허용.

---

## BUG-B 수정 계획

### 접근법

`SpawnUnitClientRpc()` 내부에서 `OnUnitProduced` 발행 시점을 UnitView 초기화 성공 여부에 따라 조정:
- `InitializeUnitView()` 성공 시: 즉시 `OnUnitProduced` 발행 (기존과 동일)
- `InitializeUnitView()` 실패 시: `RetryInitializeUnitView` 코루틴 내부에서 초기화 성공 직후 `OnUnitProduced` 발행

`RetryInitializeUnitView` 코루틴에 `rallyPoint` 파라미터 추가하여 초기화 완료 시점에 이벤트 발행.

### 수정할 파일

#### 1. `NetworkProductionController.cs`

`SpawnUnitClientRpc()` 내부:
```
현재:
  1. InitializeUnitView() 시도
  2. 실패 → RetryInitializeUnitView 코루틴 시작
  3. 바로 OnUnitProduced 발행 (항상)

수정 후:
  1. InitializeUnitView() 시도
  2. 성공 → 즉시 OnUnitProduced 발행
  3. 실패 → RetryInitializeUnitView(unitFactory, unit, rallyPoint) 코루틴 시작
             (코루틴 내부에서 초기화 성공 후 OnUnitProduced 발행)
```

`RetryInitializeUnitView` 시그니처 변경:
- `IEnumerator RetryInitializeUnitView(UnitFactory unitFactory, UnitData unitData, HexCoord? rallyPoint)` 로 변경
- 초기화 성공 후 `GameEvents.OnUnitProduced.OnNext(new UnitProducedEvent(unitData, rallyPoint))` 발행

---

## 위험 요소

| 항목 | 내용 |
|------|------|
| BUG-A — Host 분기 | IsHost 조건을 정확히 처리하지 않으면 Host가 ServerRpc를 중복 호출할 수 있음 |
| BUG-A — OnRallyPointChanged | 클라이언트에서 마커 표시를 위해 로컬 `SetRallyPoint` 호출이 필요한지 검토 필요. ServerRpc 성공 후 서버가 ClientRpc로 확인을 보내지 않으므로 마커는 클라이언트 로컬에서 처리해야 함. |
| BUG-B — 이중 발행 방지 | 성공/실패 분기에서 `OnUnitProduced`가 두 번 발행되지 않도록 명확히 분기 처리 |

---

## 체크리스트

- [ ] `NetworkProductionController.cs` — `SetRallyPointServerRpc` 추가
- [ ] `ProductionPanelUI.cs` — 네트워크 분기 추가
- [ ] `NetworkProductionController.cs` — `SpawnUnitClientRpc` OnUnitProduced 발행 시점 수정
- [ ] `NetworkProductionController.cs` — `RetryInitializeUnitView` 시그니처 변경
