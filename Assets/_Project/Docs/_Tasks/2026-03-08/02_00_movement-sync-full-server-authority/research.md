# Research: 유닛 누적 시 동기화 어긋남 — 완전 서버 권위 전환 필요

**작성일:** 2026-03-08
**작업명:** movement-sync-full-server-authority

---

## 증상

- 처음 소수 유닛: 정상 동기화
- 유닛이 누적(10개+)되면: 양쪽 화면 동기화 어긋남, 유닛 겹침, 점프

---

## 근본 원인 (3중)

### 원인 1 (핵심): `unit.Position` 도메인 상태 분기

현재 구조: 클라이언트가 랠리/Castle접근 이동을 **독립 계산**하여 직접 실행.

```
서버: RequestMove(unit, rally) → ProcessStep 호출 → unit.Position 갱신 (서버 시간 기준)
클라이언트: RequestMove(unit, rally) → ProcessStep 호출 → unit.Position 갱신 (클라이언트 시간 기준)
```

`RequestMove` 내부 blocked 집합 구성:
```csharp
foreach (var other in _unitSpawn.Units.Values)
{
    blocked.Add(other.Position);          // 현재 Position
    if (same team) blocked.Add(other.ClaimedTile); // 이동 중 선점 타일
}
```

유닛이 많아질수록 서버/클라이언트가 계산 시점에 서로 다른 `unit.Position`/`ClaimedTile` 상태를 가짐
→ A* 경로가 달라짐 → `ProcessStep` 호출 순서 달라짐 → 도메인 상태 누적 분기

결과: siege RPC가 서버의 `unit.Position` 기준으로 경로를 계산해 클라이언트에 전송
→ 클라이언트 unit.Position과 불일치 → `MoveAlongPath`의 `Vector3.Lerp(fromPos, toPos, t)`에서
path[0] 기준 위치로 순간이동(snap) → 유닛 겹침·점프 현상

### 원인 2: stale `OnMoveComplete` 콜백

`BroadcastMoveClientRpc` 수신 시 `MoveTo` 호출:
```csharp
public void MoveTo(List<HexCoord> path, bool serverAuthoritative = false)
{
    if (_moveCoroutine != null) { StopCoroutine(...); }  // 기존 코루틴 중단
    // OnMoveComplete는 초기화되지 않음! ← 버그
    _moveCoroutine = StartCoroutine(MoveAlongPath(path, serverAuthoritative));
}
```

시나리오:
1. 클라이언트: 랠리 이동 중. `OnMoveComplete = MoveTowardEnemyCastle`
2. siege `BroadcastMoveClientRpc` 도착 → `MoveTo` → 랠리 코루틴 중단
3. siege 이동 완료 → stale 콜백 `MoveTowardEnemyCastle` 발동
4. `MoveTowardEnemyCastle` → `_unitMovement.RequestMove(...)` (로컬, 비권위) → `unitView.MoveTo(path)` (비서버권위!)
5. 비권위 로컬 이동 시작 → ClaimedTile 설정 → 더 큰 분기

### 원인 3: 스폰 RPC 순서 보장 없음

- `SpawnUnitClientRpc`: NetworkProductionController (NetworkObject A)
- `BroadcastMoveClientRpc`: NetworkUnitMovementController (NetworkObject B)

NGO는 서로 다른 NetworkObject 간 RPC 도달 순서를 보장하지 않음.
이동 RPC가 스폰 RPC보다 먼저 도착하면 `unitFactory.GetUnitObject(unitId) == null`
→ 이동 명령 유실 → 클라이언트에서 해당 유닛이 영구적으로 정지

---

## 현재 구조 vs 올바른 구조

### 현재 (broken)
```
서버: OnUnitProduced → 랠리 경로 계산 → MoveTo (서버)          // siege만 broadcast
클라이언트: SpawnUnitClientRpc → OnUnitProduced 독립 실행       // 로컬 계산
         → RequestMove → ProcessStep → unit.Position 독립 갱신
         → MoveTowardEnemyCastle 독립 실행
         → RegisterSiege 독립 실행
         → TickSiege: isClient=true → skip
         → BroadcastMoveClientRpc 수신 → MoveTo(serverAuth)
           but stale OnMoveComplete exists → stale callback 발동!
```

### 올바른 (Full Server Authority)
```
서버: OnUnitProduced → 랠리 경로 계산 → MoveTo → BroadcastMoveIfServer(rally)
     랠리 완료 → MoveTowardEnemyCastle → MoveTo → BroadcastMoveIfServer(castle)
     Castle 접근 완료 → RegisterSiege → TickSiege → MoveTo → BroadcastMoveIfServer(siege)

클라이언트: SpawnUnitClientRpc → OnUnitProduced → if(IsNetworkClient) return  // 아무것도 안 함
          BroadcastMoveClientRpc 수신 → MoveTo(serverAuthoritative:true)
          ProcessStep은 실행됨 (unit.Position 갱신) → 서버 경로 기준으로 동기화
          OnMoveComplete는 절대 설정되지 않음 → stale callback 없음
```

---

## 영향 받는 파일

- `Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitMovementController.cs`
