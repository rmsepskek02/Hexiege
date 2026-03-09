# Plan: 클라이언트 유닛 이동 근본 원인 수정

**작성일:** 2026-03-08
**작업명:** client-movement-root-cause-fix
**담당:** game-programmer

---

## 변경 파일 및 내용

### 1. `ProductionTicker.cs`

**[A] `_networkMovement` 필드 및 Initialize 파라미터 제거**

SerializeField 의존 제거. `BroadcastMoveIfServer`에서 동적 탐색.

```csharp
// 제거
private NetworkUnitMovementController _networkMovement;

// Initialize 시그니처에서 마지막 파라미터 제거
public void Initialize(
    UnitProductionUseCase production,
    ResourceUseCase resource,
    UnitMovementUseCase unitMovement,
    BuildingPlacementUseCase buildingPlacement,
    UnitFactory unitFactory,
    GameConfig config)   // ← networkMovement 파라미터 제거
```

**[B] `OnUnitProduced`에서 `IsNetworkClient` 조기 반환 제거**

클라이언트가 독립적으로 랠리 이동 처리.

```csharp
private void OnUnitProduced(UnitProducedEvent e)
{
    // 제거: if (IsNetworkClient) return;

    if (_unitFactory == null || _unitMovement == null) return;
    ...
    // BroadcastMoveIfServer(e.Unit.Id, path); ← 제거
}
```

**[C] `MoveTowardEnemyCastle`에서 `BroadcastMoveIfServer` 제거**

```csharp
private void MoveTowardEnemyCastle(UnitData unit, UnitView unitView)
{
    ...
    unitView.MoveTo(path);
    // BroadcastMoveIfServer(unit.Id, path); ← 제거
}
```

**[D] `BroadcastMoveIfServer` 동적 탐색으로 재구현** (TickSiege 전용 유지)

```csharp
private void BroadcastMoveIfServer(int unitId, List<HexCoord> path)
{
    if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

    // SerializeField 의존 제거 → 동적 탐색 (null이면 Find)
    if (_networkMovement == null)
        _networkMovement = FindFirstObjectByType<NetworkUnitMovementController>();

    _networkMovement?.BroadcastServerMove(unitId, path);
}
```

- `_networkMovement` 필드는 유지하되, Initialize에서 주입받지 않고 지연 탐색으로 채움

---

### 2. `UnitView.cs`

**`MoveTo`에 `serverAuthoritative` 파라미터 추가**

서버 권위 이동(Siege RPC) 시 타일 충돌 체크 생략.

```csharp
public void MoveTo(List<HexCoord> path, bool serverAuthoritative = false)
{
    if (_moveCoroutine != null) { StopCoroutine(_moveCoroutine); _moveCoroutine = null; }
    _moveCoroutine = StartCoroutine(MoveAlongPath(path, serverAuthoritative));
}

private IEnumerator MoveAlongPath(List<HexCoord> path, bool serverAuthoritative = false)
{
    ...
    // 서버 권위 이동: 타일 충돌 체크 및 ClaimedTile 선점 생략
    if (!serverAuthoritative &&
        _movementUseCase != null && _movementUseCase.IsTileBlockedBySameTeam(_unitData, to))
    {
        ...
    }

    if (!serverAuthoritative)
        _unitData.ClaimedTile = to;

    // ProcessStep은 유지 (unit.Position 업데이트 필요)
    ...
}
```

---

### 3. `NetworkUnitMovementController.cs`

**`BroadcastMoveClientRpc`와 `SyncMovementClientRpc`에 `serverAuthoritative: true` 전달**

```csharp
[ClientRpc]
private void BroadcastMoveClientRpc(int unitId, int[] pathQ, int[] pathR)
{
    if (IsServer) return;
    ...
    unitView.MoveTo(path, serverAuthoritative: true);  // ← 추가
}

[ClientRpc]
private void SyncMovementClientRpc(int unitId, int[] pathQ, int[] pathR, ...)
{
    if (IsServer) return;
    ...
    unitView.MoveTo(path, serverAuthoritative: true);  // ← 추가
}
```

---

### 4. `GameBootstrapper.cs`

**`SetupProduction()`에서 `networkMovement` 파라미터 제거**

```csharp
// 변경 전
_productionTicker.Initialize(
    _unitProduction, _resource, _unitMovement,
    _buildingPlacement, _unitFactory, _config,
    isNetworkMode ? _networkUnitMovement : null);

// 변경 후
_productionTicker.Initialize(
    _unitProduction, _resource, _unitMovement,
    _buildingPlacement, _unitFactory, _config);

// isNetworkMode 변수 및 _networkUnitMovement 관련 코드 제거 (이 메서드 내에서)
```

---

## 수정 후 동작 흐름

```
[초기 이동]
서버: OnUnitProduced → 랠리 경로 계산 → unitView.MoveTo (서버 화면 반영)
클라이언트: SpawnUnitClientRpc → GameEvents.OnUnitProduced 발행
           → ProductionTicker.OnUnitProduced (IsNetworkClient 체크 없음)
           → 랠리 경로 독립 계산 (동일 랠리포인트 → 동일 경로)
           → unitView.MoveTo → MoveAlongPath → 정상 이동

[Siege 이동]
서버: TickSiege → BroadcastMoveIfServer (동적 탐색) → BroadcastMoveClientRpc
클라이언트: 수신 → unitView.MoveTo(path, serverAuthoritative: true)
           → 타일 충돌 체크 없이 경로 그대로 실행
```

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| 초기 이동 시 서버/클라이언트 경로 미세 불일치 | 랠리 경로는 동일 입력(랠리포인트, A*)이므로 결정론적. 미세 차이는 NetworkTileSync로 보정됨 |
| `FindFirstObjectByType` 매번 호출 시 성능 | 1회 캐싱 (`_networkMovement != null`이면 재탐색 불요) |
| `serverAuthoritative`로 ClaimedTile 미설정 시 다른 로컬 이동과 충돌 | Siege RPC는 단계별(1초 간격)이므로 충돌 빈도 낮음. 싱글플레이에선 미적용(default=false) |

---

## 수정 범위 요약

| 파일 | 변경 유형 |
|------|---------|
| `ProductionTicker.cs` | IsNetworkClient 제거, BroadcastMoveIfServer 동적탐색, OnUnitProduced/MoveTowardEnemyCastle 에서 Broadcast 제거 |
| `UnitView.cs` | MoveTo/MoveAlongPath serverAuthoritative 파라미터 추가 |
| `NetworkUnitMovementController.cs` | MoveTo 호출에 serverAuthoritative:true 추가 |
| `GameBootstrapper.cs` | SetupProduction networkMovement 파라미터 제거 |
