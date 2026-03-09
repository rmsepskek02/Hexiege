# Plan: 멀티플레이 이동 동기화 — 서버 권위 전투 멈춤

**작성일:** 2026-03-09
**작업명:** movement-sync-root-fix

---

## 근본 원인

서버와 클라이언트가 `MoveAlongPath` 내에서 **독립적으로** `HasEnemyInRangeByCoord`를 실행.
프레임 타이밍 차이로 서로 다른 적 위치 상태를 보게 되어 다른 시점에 멈춤 → 영구적 위치 괴리.

---

## 해결 방향: 서버 권위

- **서버만** 적 감지 판단 → `OnUnitMovementPaused` 이벤트 발행 → `StopUnitClientRpc`로 클라이언트에 전달
- 클라이언트는 독립 판단 없이 RPC 수신 시 같은 타일에서 정지 (스냅)
- 적 사망 후 `TickSiege`가 새 경로 발행 → 양쪽 재개

---

## 수정 내용 (5개 파일 + 보완 1개)

### `GameEvents.cs`
- `UnitMovementPausedEvent` 클래스 추가 (UnitId, Tile 필드)
- `OnUnitMovementPaused` Subject 추가

### `UnitView.cs`
- **`StopAndSnapToTile(HexCoord tile)`** 메서드 추가: 코루틴 중단 + 타일 중앙으로 위치 스냅 (클라이언트 RPC 수신용)
- **MoveAlongPath 전투 체크 교체**: 기존 양쪽 독립 판단 → 서버 전용 판단으로 변경
  - `NetworkContext.IsNetworkServer` 조건으로 서버만 실행
  - 적 감지 시: `ClaimedTile=null`, `_moveCoroutine=null`, `OnUnitMovementPaused` 발행, `yield break`
  - 클라이언트: 전투 체크 없음 (StopUnitClientRpc 수신으로 처리)

### `NetworkUnitMovementController.cs`
- `OnNetworkSpawn`에서 서버만 `OnUnitMovementPaused` 구독 → `StopUnitClientRpc` 전송
- **`StopUnitClientRpc(unitId, tileQ, tileR)`** 추가: 클라이언트에서 `UnitView.StopAndSnapToTile()` 호출

### `ProductionTicker.cs`
- `_combatUseCase` 필드 추가, `Initialize` 파라미터에 추가
- `TickSiege`: `if (isClient) continue;` 다음에 `HasEnemyInRangeByCoord` 체크 추가 → 전투 중 이동 명령 생략
- **`OnUnitMovementPaused` 구독 추가**: 경로 중단 유닛을 `_siegeUnits`에 등록 → 적 소멸 후 TickSiege가 재개 가능하게 보완

### `GameBootstrapper.cs`
- `_productionTicker.Initialize()` 호출에 `_unitCombat` 인자 추가

---

## 수정 후 흐름

```
[서버 - 적 감지]
MoveAlongPath → HasEnemyInRangeByCoord(true) → yield break
  → OnUnitMovementPaused 발행
  → NetworkUnitMovementController → StopUnitClientRpc
  → ProductionTicker.OnUnitMovementPaused → _siegeUnits 등록

[클라이언트]
StopUnitClientRpc 수신 → StopAndSnapToTile(from 타일) → 정지

[적 사망 후 재개]
TickSiege (1초): HasEnemyInRangeByCoord(false) → 새 경로 발행
  → unitView.MoveTo(path) (서버)
  → BroadcastServerMove → BroadcastMoveClientRpc (클라이언트)
```

---

## 기획 보존 확인

| 항목 | 결과 |
|------|------|
| 사거리 내 적 발견 시 멈춤 | ✅ 유지 (서버 판단) |
| 전투 (데미지/사망/애니메이션) | ✅ 변경 없음 |
| 서버/클라이언트 동일 타일에서 멈춤 | ✅ StopUnitClientRpc로 보장 |
| 싱글플레이 전투 체크 | ✅ 변경 없음 |
| 적 사망 후 이동 재개 | ✅ TickSiege가 처리 |
