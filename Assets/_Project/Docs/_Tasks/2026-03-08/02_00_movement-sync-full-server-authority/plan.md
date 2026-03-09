# Plan: 유닛 이동 완전 서버 권위 전환

**작성일:** 2026-03-08
**작업명:** movement-sync-full-server-authority
**담당:** game-programmer

---

## 목표

모든 유닛 이동(랠리, Castle접근, Siege)을 서버가 계산·브로드캐스트.
클라이언트는 RPC 수신 시에만 이동 실행. 독립 계산 완전 제거.

---

## 변경 파일 및 내용

### 1. `ProductionTicker.cs`

**[A] `OnUnitProduced`: `IsNetworkClient` 조기 반환 복원 + 랠리 broadcast 추가**

```csharp
private void OnUnitProduced(UnitProducedEvent e)
{
    if (IsNetworkClient) return;  // ← 복원: 클라이언트는 서버 RPC로만 이동 처리

    if (_unitFactory == null || _unitMovement == null) return;
    ...
    if (e.RallyPoint.HasValue)
    {
        ...
        if (path != null)
        {
            unitView.OnMoveComplete = () => MoveTowardEnemyCastle(e.Unit, unitView);
            unitView.MoveTo(path);
            BroadcastMoveIfServer(e.Unit.Id, path);  // ← 추가: 랠리 이동 broadcast
        }
    }
    else
    {
        MoveTowardEnemyCastle(e.Unit, unitView);  // ← MoveTowardEnemyCastle 내부에서 broadcast
    }
}
```

**[B] `MoveTowardEnemyCastle`: Castle접근 broadcast 추가**

```csharp
private void MoveTowardEnemyCastle(UnitData unit, UnitView unitView)
{
    ...
    List<HexCoord> path = FindPathToNearestEmptyTile(unit, enemyCastle.Value);
    if (path != null)
    {
        unitView.OnMoveComplete = () => RegisterSiege(unit, enemyCastle.Value);
        unitView.MoveTo(path);
        BroadcastMoveIfServer(unit.Id, path);  // ← 추가: Castle접근 이동 broadcast
    }
    else
    {
        RegisterSiege(unit, enemyCastle.Value);  // 경로 없음 → siege 등록만 (broadcast 불필요)
    }
}
```

- `TickSiege`의 `BroadcastMoveIfServer` 호출은 그대로 유지

---

### 2. `NetworkUnitMovementController.cs`

**[A] `BroadcastMoveClientRpc`: 스폰 RPC 순서 경쟁 대응 — 유닛 미발견 시 재시도**

```csharp
[ClientRpc]
private void BroadcastMoveClientRpc(int unitId, int[] pathQ, int[] pathR)
{
    if (IsServer) return;
    ...
    GameObject unitObj = unitFactory.GetUnitObject(unitId);
    if (unitObj == null)
    {
        // 스폰 RPC보다 이동 RPC가 먼저 도착한 경우 — 1프레임 대기 후 재시도
        StartCoroutine(RetryBroadcastMove(unitId, pathQ, pathR));
        return;
    }
    ...
    unitView.MoveTo(path, serverAuthoritative: true);
}

private IEnumerator RetryBroadcastMove(int unitId, int[] pathQ, int[] pathR)
{
    const float timeout = 3f;
    float elapsed = 0f;

    if (_bootstrapper == null)
        _bootstrapper = FindFirstObjectByType<Hexiege.Bootstrap.GameBootstrapper>();

    while (elapsed < timeout)
    {
        yield return null;
        elapsed += Time.deltaTime;

        var factory = _bootstrapper?.GetUnitFactory();
        if (factory == null) continue;

        GameObject unitObj = factory.GetUnitObject(unitId);
        if (unitObj == null) continue;

        var unitView = unitObj.GetComponent<Hexiege.Presentation.UnitView>();
        if (unitView == null) yield break;

        List<HexCoord> path = new List<HexCoord>(pathQ.Length);
        for (int i = 0; i < pathQ.Length; i++)
            path.Add(new HexCoord(pathQ[i], pathR[i]));

        unitView.MoveTo(path, serverAuthoritative: true);
        Debug.Log($"[Network] RetryBroadcastMove 성공. UnitId={unitId}, 대기시간={elapsed:F2}s");
        yield break;
    }

    Debug.LogWarning($"[Network] RetryBroadcastMove 타임아웃. UnitId={unitId}");
}
```

---

## 수정 후 동작 흐름

```
[랠리 이동]
서버: OnUnitProduced → 랠리 경로 계산 → unitView.MoveTo → BroadcastMoveIfServer(rally)
클라이언트: SpawnUnitClientRpc → OnUnitProduced → IsNetworkClient → return
          BroadcastMoveClientRpc(rally) 수신 → MoveTo(path, serverAuthoritative:true)
          → 유닛 없으면 RetryBroadcastMove → 스폰 후 자동 이동

[Castle접근 이동]
서버: 랠리 완료 → MoveTowardEnemyCastle → 경로 계산 → unitView.MoveTo → BroadcastMoveIfServer(castle)
클라이언트: BroadcastMoveClientRpc(castle) 수신 → MoveTo(path, serverAuthoritative:true)
          OnMoveComplete = null → stale callback 없음

[Siege 이동]
서버: TickSiege → 경로 계산 → unitView.MoveTo → BroadcastMoveIfServer(siege)  (기존 유지)
클라이언트: BroadcastMoveClientRpc(siege) 수신 → MoveTo(path, serverAuthoritative:true)
```

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| 랠리/Castle 이동 시 클라이언트에서 약간의 지연(RPC 왕복) | 수용 가능. 서버가 즉시 broadcast하므로 지연 최소 |
| RetryBroadcastMove 실행 중 유닛이 삭제되는 경우 | timeout 3초 + unitObj null 체크로 안전하게 종료 |
| `_siegeUnits` 클라이언트 측 비어있음 | 정상. 클라이언트는 siege 상태 관리 불필요. TickSiege는 `_siegeUnits.Count == 0`으로 즉시 반환 |
| BroadcastMoveClientRpc가 없는 상태에서 path==null 경우 | RegisterSiege만 실행. 다음 TickSiege에서 경로 생기면 broadcast |

---

## 수정 범위 요약

| 파일 | 변경 유형 |
|------|---------|
| `ProductionTicker.cs` | OnUnitProduced에 IsNetworkClient 복원 + 랠리 broadcast 추가, MoveTowardEnemyCastle에 Castle broadcast 추가 |
| `NetworkUnitMovementController.cs` | BroadcastMoveClientRpc에 RetryBroadcastMove 코루틴 추가 |
