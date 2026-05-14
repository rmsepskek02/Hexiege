# Research: 클라이언트 유닛 이동 불일치 (타일 충돌)

**작성일:** 2026-03-07
**작업명:** client-movement-tile-conflict

---

## 증상

- Host(서버): 유닛 이동 정상
- Remote 클라이언트: 유닛이 스폰 타일에 머무르고 이동 안 됨
- 단, 공격 애니메이션은 정상 재생됨
- 가끔 1~2개 유닛만 이동 동기화됨

---

## 도메인/시각 동기화 분리 확인

| 항목 | 클라이언트 상태 |
|------|---------------|
| 유닛 스폰 | 정상 (SpawnUnitClientRpc) |
| 타일 색상 변화 | 정상 (NetworkTileSync) |
| 공격 애니메이션 | 정상 (TriggerAttackAnimation ClientRpc) |
| 유닛 시각 이동 | 실패 (MoveAlongPath 조기 종료) |

→ 도메인 상태는 동기화됨. 시각 이동(UnitView.MoveTo)만 실패.

---

## 코드 흐름 분석

### 서버 측 흐름 (정상)
1. 유닛 생산 완료 → `ProductionTicker.OnUnitProduced`
2. `RequestMove(unit, rallyTarget)` → 경로 계산 + 도메인 상태 업데이트
3. `unitView.MoveTo(path)` → 시각 이동 시작 (서버 화면에 반영)
4. `BroadcastMoveIfServer(unit.Id, path)` → `BroadcastMoveClientRpc` 전송

### 클라이언트 측 흐름 (실패 지점)
1. `SpawnUnitClientRpc` → 유닛 생성 (unit.Position = spawnCoord)
2. `ProductionTicker.OnUnitProduced` → `if (IsNetworkClient) return;` ← 조기 반환
3. `BroadcastMoveClientRpc` 수신 → `unitView.MoveTo(path)` 호출

### MoveAlongPath 내 실패 원인

```csharp
// UnitView.cs:317
if (_movementUseCase != null && _movementUseCase.IsTileBlockedBySameTeam(_unitData, to))
{
    List<HexCoord> newPath = _movementUseCase.RequestMove(_unitData, finalTarget);
    if (newPath != null) { path = newPath; i = 0; continue; }
    else { break; }  // ← 이동 중단
}

// 320번 라인 이후
_unitData.ClaimedTile = to;  // ← 타일 선점
```

---

## 근본 원인

### 서버 vs 클라이언트 이동 시작 타이밍 비교

| 구분 | 서버 | 클라이언트 |
|------|------|----------|
| 유닛 A 이동 시작 | t=0s (생산 완료 즉시) | t=T (RPC 수신 시) |
| 유닛 B 이동 시작 | t=1s (1초 뒤 생산) | t=T (같은 프레임) |
| 유닛 C 이동 시작 | t=2s | t=T (같은 프레임) |

- **서버**: 유닛들이 1초 간격으로 순차 이동 시작 → 유닛 A가 tile2를 지나간 후 유닛 B가 tile2를 통과 → 충돌 없음
- **클라이언트**: `BroadcastMoveClientRpc`가 한꺼번에 도착 → 모든 유닛이 동일 프레임에 `MoveTo` 시작

### 충돌 발생 순서 (같은 프레임)

```
Frame T:
  유닛 A 코루틴 시작: IsTileBlockedBySameTeam(tile2) = false → ClaimedTile = tile2 → yield
  유닛 B 코루틴 시작: IsTileBlockedBySameTeam(tile2) = true (A가 선점) → RequestMove 재탐색
    → unit_B.Position = spawnCoord (ProcessStep 아직 미실행, 업데이트 안 됨)
    → RequestMove(unit_B, finalTarget) → 같은 경로 반환 또는 null
    → null이면 break → 이동 중단
  유닛 C, D, E... 동일
```

"가끔 1~2개만 동기화"되는 이유: RPC 처리 순서상 가장 먼저 시작된 1~2개 유닛만 tile 선점에 성공.

---

## 공격 애니메이션이 재생되는 이유

`TriggerAttackAnimation()`은 `NetworkCombatController.ClientRpc`를 통해 독립적으로 호출됨. 유닛 이동과 무관하게 작동.

---

## 관련 파일

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` (304~441번 라인)
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitMovementController.cs` (328~352번 라인)
