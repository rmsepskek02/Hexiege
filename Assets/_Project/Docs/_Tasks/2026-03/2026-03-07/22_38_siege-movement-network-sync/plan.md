# Plan: Siege/AI 이동 네트워크 동기화

**작성일:** 2026-03-07
**작업명:** siege-movement-network-sync
**담당:** game-programmer
**완료일:** 2026-03-07 ✅

---

## 목표

ProductionTicker의 Siege/랠리 이동 명령을 서버 권위로 통일.
서버만 경로를 결정하고, 클라이언트는 RPC로 수신하여 UnitView를 이동시킨다.

---

## 변경 파일

### 1. `NetworkUnitMovementController.cs`

**추가할 메서드: `BroadcastServerMove(unitId, path)`**

```csharp
// 서버에서만 호출. AI 이동(Siege/랠리) 명령을 모든 클라이언트에 전파.
public void BroadcastServerMove(int unitId, List<HexCoord> path)
{
    if (!IsServer) return;

    int[] pathQ = new int[path.Count];
    int[] pathR = new int[path.Count];
    for (int i = 0; i < path.Count; i++)
    {
        pathQ[i] = path[i].Q;
        pathR[i] = path[i].R;
    }
    // 모든 클라이언트(서버 자신 제외)에게 전송
    BroadcastMoveClientRpc(unitId, pathQ, pathR);
}

[ClientRpc]
private void BroadcastMoveClientRpc(int unitId, int[] pathQ, int[] pathR)
{
    if (IsServer) return; // 서버는 이미 로컬에서 MoveTo() 실행 완료

    // 경로 복원 후 UnitView 이동
    List<HexCoord> path = ...;
    GameObject unitObj = _bootstrapper.GetUnitFactory().GetUnitObject(unitId);
    unitObj.GetComponent<UnitView>()?.MoveTo(path);
}
```

- 기존 `RequestMove()` / `RequestMoveServerRpc()` / `SyncMovementClientRpc()`는 현재 미사용이므로 그대로 유지 (제거하지 않음)

---

### 2. `ProductionTicker.cs`

**Initialize()에 `NetworkUnitMovementController` 참조 추가**

```csharp
private NetworkUnitMovementController _networkMovement; // 추가

public void Initialize(..., NetworkUnitMovementController networkMovement)
{
    ...
    _networkMovement = networkMovement;
}
```

**`MoveTowardEnemyCastle()` 수정**

```csharp
private void MoveTowardEnemyCastle(UnitData unit, UnitView unitView)
{
    ...
    if (path != null)
    {
        unitView.OnMoveComplete = () => RegisterSiege(unit, enemyCastle.Value);
        unitView.MoveTo(path);
        // 서버이면 클라이언트에 브로드캐스트
        BroadcastMoveIfServer(unit.Id, path);
    }
    ...
}
```

**`TickSiege()` 수정**

```csharp
if (newDist < currentDist)
{
    unitView.OnMoveComplete = () => { ... };
    unitView.MoveTo(path);
    // 서버이면 클라이언트에 브로드캐스트
    BroadcastMoveIfServer(unitId, path);
}
```

**`OnUnitProduced()` 수정**

```csharp
if (path != null)
{
    unitView.OnMoveComplete = () => MoveTowardEnemyCastle(e.Unit, unitView);
    unitView.MoveTo(path);
    // 서버이면 클라이언트에 브로드캐스트
    BroadcastMoveIfServer(e.Unit.Id, path);
}
```

**`TickSiege()` 클라이언트 분기 — 이동 명령 제거**

```csharp
// 현재: 클라이언트에서도 TickSiege() 실행하여 unitView.MoveTo() 호출
// 변경: 클라이언트의 TickSiege()에서 unitView.MoveTo() 호출 제거
//       → siege 등록/해제 상태 관리는 유지 (UnitView의 IsMoving 체크 등)
//       → 실제 이동은 BroadcastMoveClientRpc 수신으로만 처리
```

**헬퍼 추가**

```csharp
private void BroadcastMoveIfServer(int unitId, List<HexCoord> path)
{
    if (_networkMovement != null &&
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsServer)
    {
        _networkMovement.BroadcastServerMove(unitId, path);
    }
}
```

---

### 3. `GameBootstrapper.cs`

`SetupProduction()` 등 ProductionTicker 초기화 시 `NetworkUnitMovementController` 주입.

---

## 싱글플레이 호환

- `_networkMovement == null` 또는 `!IsServer` 분기로 싱글플레이 경로는 기존과 동일하게 유지
- 싱글플레이: `BroadcastMoveIfServer()` 내부에서 NetworkManager null → 브로드캐스트 생략

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| `OnMoveComplete` 콜백이 서버/클라이언트에서 모두 등록되면 siege 체인이 중복 실행 | 클라이언트의 `OnUnitProduced` → 콜백 등록 제거 (이동 자체를 클라이언트에서 하지 않으므로 콜백도 불필요) |
| 클라이언트 siege 상태(_siegeUnits) 서버와 불일치 | RegisterSiege는 서버만 실행, 클라이언트 _siegeUnits는 제거하거나 무시 처리 |
| BroadcastMoveClientRpc 수신 전 unitObj가 아직 스폰 안 된 경우 | `SpawnUnitClientRpc`는 `OnUnitProduced` 이전에 처리되므로 타이밍 안전. 단, null 체크 필요 |

---

## 구현 범위 요약

- 수정 파일: `NetworkUnitMovementController.cs`, `ProductionTicker.cs`, `GameBootstrapper.cs`
- 신규 메서드: `BroadcastServerMove()`, `BroadcastMoveClientRpc()`, `BroadcastMoveIfServer()`
- 삭제 또는 비활성: 클라이언트 `TickSiege` 내부의 `unitView.MoveTo()` 호출, 클라이언트 `OnUnitProduced` 내부의 이동 명령
