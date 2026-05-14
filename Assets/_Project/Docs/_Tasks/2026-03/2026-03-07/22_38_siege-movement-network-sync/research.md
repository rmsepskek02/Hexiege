# Research: Siege/AI 이동 네트워크 동기화

**작성일:** 2026-03-07
**작업명:** siege-movement-network-sync
**증상:** 멀티플레이 시 클라이언트A와 클라이언트B 화면이 다르게 나타남 (유닛 위치/이동 불일치)

---

## 관련 파일

| 파일 | 역할 |
|------|------|
| `Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs` | Siege 이동 명령 발원지 |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitMovementController.cs` | 유닛 이동 네트워크 동기화 |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | 실제 이동 코루틴, ClaimedTile 관리 |
| `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs` | A* 경로 계산 |

---

## 현재 구조 분석

### ProductionTicker 이동 흐름

```
[서버/클라이언트 양쪽에서 실행]
OnUnitProduced()
  → MoveTowardEnemyCastle()
    → FindPathToNearestEmptyTile() (각자 독립 A* 계산)
    → unitView.MoveTo(path)           ← 네트워크 동기화 없음

TickSiege() — 1초 주기
  → FindPathToNearestEmptyTile() (각자 독립 A* 계산)
  → unitView.MoveTo(path)             ← 네트워크 동기화 없음
```

### NetworkUnitMovementController 현재 구조

- `RequestMove()`: 클라이언트가 플레이어 입력으로 이동 요청 시 사용 (현재 플레이어 직접 이동 삭제됨 → 미사용)
- `RequestMoveServerRpc()`: 서버에서 경로 검증 + 상대방에게 `SyncMovementClientRpc` 전송
- `SyncMovementClientRpc()`: 수신 클라이언트에서 `unitView.MoveTo(path)` 실행

### 문제점

1. **Siege 이동이 네트워크를 거치지 않음**
   - `ProductionTicker.TickSiege()`: 서버 + 클라이언트 양쪽 실행 (L169: `TickSiege(dt)` 클라이언트에서도 호출됨)
   - 각자 독립적으로 A* 경로 계산 → ClaimedTile 상태 차이로 경로 불일치 가능

2. **OnUnitProduced 이동도 동일 문제**
   - 서버: `OnUnitProduced()` → `unitView.MoveTo(path)` 직접 호출
   - 클라이언트: `SpawnUnitClientRpc` → `GameEvents.OnUnitProduced` 발행 → `ProductionTicker.OnUnitProduced()` → `unitView.MoveTo(path)` 직접 호출
   - 서버/클라이언트 각자 경로 계산

3. **ClaimedTile 미동기화**
   - `UnitView.MoveAlongPath()` L333: `_unitData.ClaimedTile = to` (로컬만 설정)
   - 서버/클라이언트의 ClaimedTile이 달라지면 A* 재계산 결과 불일치

4. **이동 중 전투 일시정지 타이밍 불일치**
   - `UnitView.MoveAlongPath()` L373: `HasEnemyInRange()` 클라이언트 독립 체크
   - Lerp 중 유닛 위치가 서버/클라이언트에서 다를 수 있어 일시정지 타이밍 차이

---

## NetworkUnitMovementController 구조 파악

현재 `RequestMove(unit, target, unitFactory, movementUseCase)`:
- 로컬 예측 이동 시작 → 서버에 `RequestMoveServerRpc` 전송
- 원래 플레이어 직접 이동용으로 설계 → 현재 미사용 (플레이어 이동 삭제됨)

`SyncMovementClientRpc(unitId, pathQ, pathR)`:
- 요청자 제외 클라이언트에게 경로 전달 → `unitView.MoveTo(path)` 실행
- 이 구조를 재활용 가능

---

## 결론

- 서버만 이동 경로를 결정하고, 클라이언트는 RPC로 수신하는 구조로 전환 필요
- `ProductionTicker`의 `OnUnitProduced`, `MoveTowardEnemyCastle`, `TickSiege`에서 이동 명령을 서버 전용으로 변경
- 서버가 `unitView.MoveTo(path)` 호출 시 동시에 `NetworkUnitMovementController`를 통해 클라이언트에 경로 브로드캐스트
- `NetworkUnitMovementController`에 "서버 발신" 전용 브로드캐스트 메서드 추가 필요
