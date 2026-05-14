# Research — 랠리포인트 무시 버그

## 개요

멀티플레이 Client(Red팀)에서 랠리포인트를 설정해도 생산된 유닛이 랠리포인트를 무시하고 이동하는 버그.

---

## 관련 파일

| 파일 | 역할 |
|------|------|
| `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs` | 랠리포인트 버튼 클릭 처리, `CompleteRallyPointSetting()` 호출 |
| `Assets/_Project/Scripts/Presentation/Input/InputHandler.cs` | 타일 클릭 감지 → `CompleteRallyPointSetting()` 호출 |
| `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs` | `SetRallyPoint()` — `ProductionState.RallyPoint` 로컬 갱신 |
| `Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs` | `OnUnitProduced` 핸들러 — 랠리포인트 이동 실행 |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkProductionController.cs` | 생산 관련 ServerRpc/ClientRpc 모음 |

---

## 랠리포인트 설정 흐름 (현재 구조)

```
[플레이어] 랠리포인트 버튼 탭
    → ProductionPanelUI.OnRallyPointClick()
        → IsSettingRallyPoint = true
        → _popup?.Hide()

[플레이어] 타일 클릭
    → InputHandler (frame check 통과)
        → ProductionPanelUI.CompleteRallyPointSetting(coord)
            → UnitProductionUseCase.SetRallyPoint(barracksId, coord)
                → ProductionState.RallyPoint = coord  ← 로컬 갱신만 발생
                → GameEvents.OnRallyPointChanged 발행 (마커 표시용)
```

## 유닛 생산 완료 흐름 (현재 구조 — 서버)

```
서버: CompleteProduction(state)
    → state.RallyPoint 참조
    → GameEvents.OnUnitProduced.OnNext(new UnitProducedEvent(unit, state.RallyPoint))
    → NetworkProductionController.OnUnitProduced()
        → SpawnUnitClientRpc(... rallyQ, rallyR, hasRally)
```

---

## 버그 원인 분석

### BUG-A (핵심): SetRallyPointServerRpc 미구현

`ProductionPanelUI.CompleteRallyPointSetting()` 에서 `_production.SetRallyPoint()`를 직접 호출함.
싱글플레이/Host에서는 `_production` = UseCase 직접 참조이므로 정상 작동.
멀티플레이 Client에서는 클라이언트 로컬 `ProductionState`만 갱신되고 **서버의 `ProductionState.RallyPoint`는 null로 유지됨**.

결과:
- 서버에서 생산 완료 시 `state.RallyPoint == null`
- `SpawnUnitClientRpc`에 `hasRally=false`로 전송
- 모든 클라이언트에서 랠리포인트 없이 이동 처리됨

**근거**: `NetworkProductionController.cs`에 `SetRallyPointServerRpc` 메서드가 존재하지 않음.
다른 생산 작업(Enqueue, Cancel, Toggle)은 모두 ServerRpc를 통해 서버에 전달되는 반면, SetRallyPoint만 누락되어 있음.

### BUG-B (부가): UnitView 초기화 전 OnUnitProduced 발행

`NetworkProductionController.SpawnUnitClientRpc()` 내부 순서:

```
1. unitFactory.InitializeUnitView(unit) 시도
2. 실패 시 RetryInitializeUnitView 코루틴 시작 (비동기)
3. 바로 다음 줄에서 GameEvents.OnUnitProduced 발행 (동기)
```

`ProductionTicker.OnUnitProduced()`에서:
```
var unitObj = _unitFactory.GetUnitObject(e.Unit.Id);
if (unitObj == null) return;  ← UnitView 미초기화 시 여기서 리턴
```

NGO 프리팹 전달이 SpawnUnitClientRpc보다 늦게 도착하는 경우 랠리포인트 이동이 시도조차 되지 않고 누락됨.
이 경우 재시도 로직이 없으므로 영구적으로 무시됨.

---

## 영향 범위

| 시나리오 | BUG-A | BUG-B |
|----------|-------|-------|
| 싱글플레이 (Host 단독) | 정상 | 정상 |
| 멀티플레이 Host (Blue팀) | 정상 | NGO 지연 시 발생 가능 |
| 멀티플레이 Client (Red팀) | **항상 발생** | NGO 지연 시 발생 가능 |
