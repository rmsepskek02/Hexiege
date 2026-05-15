# Research — 혼잡도 기반 유닛 분산 (Congestion-Based Spread)

## 작업 목적 (자연어 설명)

유닛이 적 성을 향해 이동할 때 세로로 줄 서는 현상을 해결하기 위한 두 번째 접근법입니다.

첫 번째 시도(03_11_castle-approach-spread)에서는 성 주변 인접 타일에 유닛을 분산 배정했으나, 인접 타일들이 서로 너무 가까워 경로가 실질적으로 갈라지지 않았습니다. 또한 성 위치나 맵 구조에 의존적인 방식이었습니다.

이번 접근법은 **타일마다 "얼마나 많은 유닛이 지나갔는지" 혼잡도를 기록하고, 경로 계산 시 혼잡한 타일을 더 비싼 경로로 처리**합니다. 앞서 지나간 유닛의 경로가 비싸지면 다음 유닛은 자연스럽게 덜 혼잡한 다른 경로를 선택하게 됩니다. 이 방식은 맵 구조나 성의 위치와 무관하게 유닛 통행량에 반응하여 자동으로 분산 효과를 냅니다.

---

## v1 접근법(CastleApproachManager)이 실패한 이유

- 성 인접 타일(ring 1)들은 서로 최대 2칸 거리
- 바라크에서 성까지 15칸 이동 시, 경로는 마지막 1~2칸 전까지 동일
- 결과적으로 줄 서는 현상이 거의 개선되지 않음
- 성 위치 기준으로 목적지를 잡기 때문에 맵에 의존적

---

## 현재 코드 흐름 (v1 주석 처리 후 기준)

```
UnitProductionUseCase.Tick()
  → GameEvents.OnUnitProduced 발행
  → ProductionTicker.OnUnitProduced() 구독
    → MoveTowardEnemyCastle()
      → FindEnemyCastlePos(unit.Team) → enemyCastle: HexCoord
      → FindPathToNearestEmptyTile(unit, enemyCastle)
          └─ UnitMovementUseCase.RequestMove(unit, enemyCastle)
              └─ FlowFieldService.GetOrCompute(enemyCastle)  ← 공유 BFS, 모든 유닛 동일 경로
              └─ field.GetPath(unit.Position)
      → unitView.MoveTo(path)
      → OnMoveComplete = RegisterSiege(unit, castlePos)
```

### 핵심 코드 위치

| 메서드 | 파일 | 역할 |
|--------|------|------|
| `MoveTowardEnemyCastle` | `ProductionTicker.cs` | 유닛을 적 성 방향으로 이동 명령 |
| `FindPathToNearestEmptyTile` | `ProductionTicker.cs` | 목표 타일 주변 BFS로 도달 가능한 타일 탐색 |
| `RegisterSiege` | `ProductionTicker.cs` | siege 목록 등록 |
| `TickSiege` | `ProductionTicker.cs` | 주기적으로 더 가까운 빈 타일로 이동 유도 |
| `RequestMove` | `UnitMovementUseCase.cs` | FlowField로 경로 계산 후 반환 |

---

## 새 접근법 분석

### 핵심 메커니즘

1. **타일별 혼잡도 관리**: 유닛이 타일을 지나갈 때마다 해당 타일의 혼잡도 +1
2. **시간 기반 감쇠**: 일정 시간마다 모든 타일 혼잡도 -1 (하한 0)
3. **가중치 경로 계산**: A* 경로 탐색 시 `타일 비용 = 기본 1 + (혼잡도 × 가중치)`
4. **유닛별 개별 경로**: 공유 FlowField 대신 스폰 시 유닛별 개별 A* 실행

### 왜 FlowField에서 A*로 전환해야 하는가

FlowField는 모든 타일 비용이 동일한 단순 BFS 기반이라 혼잡도 개념을 적용할 수 없다. 가중치 A*는 타일별로 다른 비용을 처리하므로 혼잡도 반영이 가능하다.

### 감쇠가 필요한 이유

감쇠 없이 혼잡도가 무한 누적되면, 시간이 지날수록 단거리 경로가 모두 비싸져 유닛이 비정상적으로 먼 우회 경로를 선택하게 된다. 감쇠를 통해 오래된 혼잡도가 자연히 사라지므로 최근 통행량만 반영된다.

### 건물 파괴/건설 시 경로 재계산 (GameSystemRules 규칙 4)

규칙 4: "건물 건설/파괴 시 모든 유닛의 경로를 즉시 재계산"
건물이 변경되면 walkable 타일 구성이 바뀌므로, 현재 A* 이동 중인 유닛 전체의 경로를 재계산해야 한다.

### 전투 진입 시 혼잡도 기여 중단

GameSystemRules 규칙 10: "A* 이동 → 전투 이동" 전환 시점(감지 사거리 내 적 진입 = 추격 시작)부터 해당 유닛은 혼잡도를 더 이상 증가시키지 않는다. 전투 종료 후 A* 이동 재개 시 혼잡도 기여 재개 + 경로 재계산.

### 생산 주기와 반응형 혼잡도

각 건물에서 유닛은 순차 생산(동시 생산 불가). 앞 유닛이 지나간 타일에 혼잡도가 쌓이면 뒤에 생산된 유닛이 A* 계산 시 해당 타일을 더 비싸게 인식하여 다른 경로를 선택한다. 이 방식은 실제 이동 후 혼잡도가 생기므로 자연스럽게 동작한다.

---

## 신규 컴포넌트 필요 목록

| 컴포넌트 | 역할 |
|----------|------|
| `CongestionMap` | 타일별 혼잡도 추적, 감쇠 처리 (Application 레이어, 순수 C#) |
| `CongestionAwarePathfinder` | 혼잡도 반영 가중치 A* 경로 계산 (Application 레이어, 순수 C#) |
| `CongestionConfig` (ScriptableObject) | 감쇠 간격, 가중치 Inspector 조정용 |

---

## 영향 범위

### v1 비활성화 대상 (주석 처리)

| 파일 | 비활성화 내용 |
|------|--------------|
| `CastleApproachManager.cs` | 파일 전체 주석 처리 |
| `ProductionTicker.cs` | v1 추가 코드 주석 처리 |
| `GameBootstrapper.cs` | v1 추가 코드 주석 처리 |

### 신규 파일

| 파일 | 역할 |
|------|------|
| `Application/Services/CongestionMap.cs` | 타일별 혼잡도 추적, 감쇠 |
| `Application/Services/CongestionAwarePathfinder.cs` | 가중치 A* 경로 계산 |
| `Infrastructure/Config/CongestionConfig.cs` | ScriptableObject 정의 |
| `Resources/Config/CongestionConfig.asset` | Inspector 조정용 설정 파일 |

### 수정 파일

| 파일 | 변경 성격 |
|------|-----------|
| `ProductionTicker.cs` | v1 주석 처리 + MoveTowardEnemyCastle에서 CongestionAwarePathfinder 사용, 감쇠 타이머 추가, 건물 변경 이벤트 구독 |
| `UnitView.cs` | 타일 진입 시 혼잡도 증가 이벤트 발행, 전투/A* 재개 플래그 처리 |
| `GameBootstrapper.cs` | v1 주석 처리 + CongestionMap, CongestionAwarePathfinder, CongestionConfig 생성/주입/Clear |
| `GameEvents.cs` | `OnUnitEnteredTile` 이벤트 추가 |

### 변경 없는 파일

| 파일 | 이유 |
|------|------|
| `FlowFieldService.cs` | 다른 시스템에서 여전히 사용 중, 제거하지 않음 |
| `UnitMovementUseCase.cs` | RequestMove 인터페이스 유지 |
| `UnitCombatUseCase.cs` | 전투 판정 로직 무관 |

---

## GameSystemRules.md 규칙 검토

| 규칙 | 내용 | 준수 여부 |
|------|------|----------|
| 규칙 1 (기본 목표) | 유닛은 스폰된 순간부터 상대방 성을 향해 이동 | 준수 — 목적지는 여전히 적 성 |
| 규칙 2 (이동 방식) | A*로 경로를 계산, 타일 중심 이동 | 준수 — 가중치 A*로 규칙과 실제 구현이 일치 |
| 규칙 3 (공유 타일 상태) | 건물/성/팀/점령 정보 서버 중앙 관리 | 변경 없음 |
| 규칙 4 (경로 재계산 시점) | 건물 건설/파괴 시 즉시 재계산 | 반드시 구현 필요 |
| 규칙 10 (유닛 상태 머신) | A* 이동 → 전투 이동 전환 조건 | 전환 시점 = 혼잡도 기여 중단 기준 |
| 규칙 11 (A* 재개 방식) | 전투 종료 후 앞쪽 타일로 이동 후 A* 재개 | 재개 시 경로 재계산 추가 필요 |
