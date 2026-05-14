# Plan — 성 접근 방향 분산 (Castle Approach Spread)

## 작업 목적 (자연어 설명)

유닛들이 적 성(Castle)에 접근할 때 한 방향으로 줄지어 이동하지 않고, 성 주변 여러 타일에서 다양한 방향으로 자연스럽게 분산되어 접근하도록 개선합니다.

**동작 방식:**  
성 주변에는 최대 6개의 인접 타일이 있습니다. 각 유닛이 생성될 때 이 인접 타일 중 현재 배정된 유닛이 가장 적은 타일을 자동으로 배정받습니다. 유닛마다 서로 다른 목적지가 생기므로 경로탐색 결과가 자연히 분기되고, 결과적으로 유닛들이 성 주변 여러 방향에서 접근하는 모습이 됩니다.

---

## GameSystemRules.md 근거

- **규칙 1 (기본 목표)**: 성 인접 타일을 목표로 설정해도 결국 성을 향해 이동하므로 충족
- **규칙 2 (이동 방식)**: FlowField(A* 기반) 경로 그대로 사용 — 목적지만 달라짐
- **규칙 3 (공유 타일 상태)**: 변경 없음
- 새로운 규칙 추가 불필요 — 기존 이동 로직 위에서 목적지만 분산하는 확장

---

## ⚠️ 기존 로직 제거 없음

이번 작업은 기존 코드를 제거하지 않습니다. `MoveTowardEnemyCastle`의 이동 목적지(`enemyCastle.Value`)를 배정된 접근 타일(`approachTile`)로 교체하는 수준의 변경입니다.

---

## 구현 계획

### [1] 신규 파일: `Assets/_Project/Scripts/Application/Services/CastleApproachManager.cs`

**역할:** 성 인접 타일별 배정 유닛 수를 관리하고, 새 유닛에게 가장 덜 배정된 타일을 반환한다.

**주요 내용:**
- `Dictionary<HexCoord, int> _assignedCounts` — 접근 타일별 배정 수 추적
- `Dictionary<int, HexCoord> _unitAssignments` — 유닛 Id → 배정된 타일 (Release 시 타일 식별용)
- `AssignApproachTile(HexCoord castleCoord)`: 성 인접 walkable 타일 중 `_assignedCounts`가 가장 낮은 타일 반환. 없으면 `null` (폴백용)
- `Release(int unitId)`: 유닛 사망/제거 시 해당 유닛의 배정 해제 (`_assignedCounts` 감소)
- `Clear()`: 재경기 시 전체 초기화
- `HexGrid` 참조: 성 인접 타일의 walkable 여부 확인에 사용 (생성자 주입)

**레이어**: Application (UnityEngine.Vector3 없이 순수 도메인/코어 의존)

---

### [2] 수정 파일: `Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs`

#### 2-1. 필드 추가
```
private CastleApproachManager _castleApproachManager;
```

#### 2-2. Initialize() 파라미터 추가
`CastleApproachManager castleApproach` 파라미터 추가 후 필드에 저장.

#### 2-3. MoveTowardEnemyCastle() 수정 (핵심)

변경 전:
- `enemyCastle.Value`를 `FindPathToNearestEmptyTile`의 목적지로 사용

변경 후:
- `_castleApproachManager.AssignApproachTile(enemyCastle.Value)` 호출
- 반환된 `approachTile`이 있으면 그것을 이동 목적지로 사용
- `approachTile`이 없으면(null) 기존처럼 `enemyCastle.Value` 폴백 사용
- `RegisterSiege`에 `approachTile`도 함께 전달 (siege 재이동 시 동일 타일 목표 유지)

#### 2-4. SiegeEntry 클래스 수정
`HexCoord ApproachTile` 필드 추가.  
(siege 유닛이 재이동할 때 원래 배정된 접근 타일을 목표로 삼기 위함)

#### 2-5. RegisterSiege() 수정
`HexCoord approachTile` 파라미터 추가 → `SiegeEntry.ApproachTile`에 저장.

#### 2-6. TickSiege() 수정
- 이동 목적지를 `entry.CastlePos` 대신 `entry.ApproachTile`로 변경
- 거리 비교 기준도 `entry.ApproachTile` 기준으로 변경
- Castle 인접 도착 판정(`distance <= 1`)은 `entry.CastlePos` 기준 그대로 유지

#### 2-7. OnEntityDied 구독 추가 (유닛 사망 시 배정 해제)
```
GameEvents.OnEntityDied 구독 → UnitData인 경우 _castleApproachManager.Release(unit.Id) 호출
```

---

### [3] 수정 파일: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

- `CastleApproachManager` 인스턴스 생성 (`new CastleApproachManager(_grid)`)
- `_productionTicker.Initialize(...)` 호출 시 인스턴스 함께 전달
- 재경기(Rematch) 시 `_castleApproachManager.Clear()` 호출 추가

---

## 위험 요소 및 예외 처리

| 상황 | 처리 방법 |
|------|-----------|
| 성 인접 타일이 모두 건물로 막힌 경우 | `AssignApproachTile`이 null 반환 → `enemyCastle.Value` 폴백 사용 |
| 성이 없는 경우 | 기존과 동일: `FindEnemyCastlePos` null 반환 → 조기 return |
| 멀티플레이 | `MoveTowardEnemyCastle`은 서버에서만 실행 → `CastleApproachManager`도 서버 전용, 별도 동기화 불필요 |
| ApproachTile 없이 RegisterSiege 호출 | `approachTile`이 null/default인 경우 `CastlePos`를 ApproachTile로 폴백 사용 |

---

## 변경 없는 파일

| 파일 | 이유 |
|------|------|
| `UnitView.cs` | 이동 로직 그대로 — 목적지 HexCoord만 달라짐 |
| `UnitMovementUseCase.cs` | FlowField 경로 계산 변경 없음 |
| `FlowFieldService.cs` | 새 목적지로 자동으로 새 FlowField 계산 (기존 동작 그대로) |
| `GameSystemRules.md` | 규칙 위반 없음 |

---

## 구현 완료 후 확인 사항

- 싱글플레이: 유닛 여러 마리 생성 시 성 주변 여러 방향에서 접근하는지 육안 확인
- 성 인접 타일이 6개 미만(지형 경계)인 경우에도 정상 분산되는지 확인
- 유닛 사망 후 새 유닛이 같은 슬롯을 재사용하는지 확인 (배정 해제 정상 동작)
