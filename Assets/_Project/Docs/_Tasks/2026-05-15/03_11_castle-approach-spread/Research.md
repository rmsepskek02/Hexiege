# Research — 성 접근 방향 분산 (Castle Approach Spread)

## 작업 목적 (자연어 설명)

현재 게임에서 유닛이 생성되면 모두 같은 경로를 따라 세로 줄지어 적 성(Castle)을 향해 이동하는 현상이 발생합니다.  
원인은 모든 유닛이 하나의 동일한 목적지(적 성 타일 좌표)를 이동 목표로 삼기 때문입니다.  
이를 해결하기 위해 각 유닛이 성 주변의 서로 다른 타일을 목표로 이동하도록 분산 배정하는 구조를 파악하고, 어떤 코드를 얼마나 바꿔야 하는지 분석합니다.

---

## 현재 코드 흐름 분석

### 유닛 생산 → 성 이동 전체 흐름

```
UnitProductionUseCase.Tick()
  → GameEvents.OnUnitProduced 발행
  → ProductionTicker.OnUnitProduced() 구독
    → (랠리포인트 있음) unitView.OnMoveComplete = MoveTowardEnemyCastle
    → (랠리포인트 없음) MoveTowardEnemyCastle() 직접 호출
      → FindEnemyCastlePos(unit.Team) → enemyCastle: HexCoord
      → FindPathToNearestEmptyTile(unit, enemyCastle)
          └─ BFS 시작점: enemyCastle (성 타일)
          └─ _unitMovement.RequestMove(unit, enemyCastle) 호출
              └─ FlowFieldService.GetOrCompute(enemyCastle)
              └─ field.GetPath(unit.Position)
          └─ path 반환
      → unitView.MoveTo(path)
      → OnMoveComplete = RegisterSiege(unit, enemyCastle)
```

### 핵심 코드 위치

| 메서드 | 파일 | 역할 |
|--------|------|------|
| `MoveTowardEnemyCastle` | `ProductionTicker.cs:423` | 유닛을 적 성 방향으로 이동 명령 |
| `FindPathToNearestEmptyTile` | `ProductionTicker.cs:558` | 목표 타일 주변 BFS로 도달 가능한 타일 탐색 |
| `RegisterSiege` | `ProductionTicker.cs:449` | siege 목록 등록 (Castle 인접까지 지속 접근) |
| `TickSiege` | `ProductionTicker.cs:468` | 주기적으로 더 가까운 빈 타일로 이동 유도 |
| `RequestMove` | `UnitMovementUseCase.cs:108` | FlowField로 경로 계산 후 반환 |

---

## 근본 원인

**문제**: 모든 유닛이 동일한 `enemyCastle` HexCoord를 FlowField 목적지로 사용한다.

`FlowFieldService.GetOrCompute(enemyCastle)` 는 하나의 FlowField를 생성하고 모든 유닛이 이 FlowField를 공유한다.  
FlowField는 목적지까지의 "최적 경로"를 타일별로 미리 계산해 두므로, 맵 중앙에서 같은 목적지를 향하는 유닛들은 모두 동일한 경로를 따르게 된다.  
결과: 모든 유닛이 같은 방향에서 성으로 접근 → 세로 줄 형성.

---

## 관련 시스템 현황

### FlowFieldService (공유 경로)
- 같은 목적지(HexCoord)에 대해 BFS 1회 계산 후 결과를 캐시
- 유닛 수에 무관하게 같은 FlowField를 O(1)로 조회
- 목적지가 다르면 서로 다른 FlowField → 서로 다른 경로

### SiegeEntry (현재 구조)
```csharp
private class SiegeEntry
{
    public int UnitId;
    public TeamId Team;
    public HexCoord CastlePos;  // 모든 유닛이 같은 값
}
```
- `CastlePos`가 모든 유닛에서 동일 → `TickSiege`도 같은 목적지로 재이동 → 분산 없음

### AttackPositionManager
- 2026-05-11 재설계에서 주석 처리(비활성화)됨
- 공격 슬롯(근접 전투 시 겹침 방지) 용도였으며, 이동 경로 분산과는 별개 기능

---

## 영향 범위

### 변경 필요 파일

| 파일 | 변경 성격 |
|------|-----------|
| `ProductionTicker.cs` | `MoveTowardEnemyCastle`, `TickSiege`, `SiegeEntry`, `Initialize` 수정 |
| `GameBootstrapper.cs` | `CastleApproachManager` 생성 및 `ProductionTicker`에 주입 |
| (신규) `Application/Services/CastleApproachManager.cs` | 성 접근 타일 배정 서비스 |

### 변경 없는 파일

| 파일 | 이유 |
|------|------|
| `UnitView.cs` | 이동 로직 자체 변경 없음 — 목적지만 달라짐 |
| `UnitMovementUseCase.cs` | FlowField 경로 계산 그대로 유지 |
| `UnitCombatUseCase.cs` | 전투 판정 로직 무관 |
| `GameSystemRules.md` | 기존 규칙 위반 없음 (분석 아래 참조) |

---

## GameSystemRules.md 규칙 검토

- **규칙 1 (기본 목표)**: "유닛은 스폰된 순간부터 상대방 성을 향해 이동한다" — 접근 방향만 분산될 뿐, 여전히 적 성을 향해 이동하므로 위반 없음
- **규칙 2 (이동 방식)**: "A*로 경로를 계산" — FlowField(A* 기반) 그대로 유지, 위반 없음
- **규칙 3 (공유 타일 상태)**: 변경 없음
- **규칙 4 (경로 재계산 시점)**: 건물 변경 시 재계산 로직 무관

---

## 추가 발견 사항

- `FindPathToNearestEmptyTile`은 BFS로 목표 타일 근처에서 도달 가능한 첫 타일을 찾아준다. `RequestMove(unit, target)` 호출 시 `target`이 non-walkable(성 타일)이어도 FlowField가 마지막에 덧붙여 처리하므로 경로가 반환된다.
- 성 인접 타일(Adjacent)은 모두 walkable이므로, 성 인접 타일을 목적지로 사용하면 FlowField가 정상적으로 계산된다.
- 멀티플레이 정책: `MoveTowardEnemyCastle`은 서버에서만 실행됨. `CastleApproachManager`도 서버 전용으로 관리하면 됨.
