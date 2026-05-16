# Research — Dead Code & 중복 코드 정리

## 작업 목적 (자연어 설명)

프로젝트가 진행되면서 기능이 폐기되거나 설계가 바뀌었을 때, 이전에 작성된 코드가 그대로 남아있는 경우가 생깁니다. 이런 코드는 실제로 동작에 영향을 주지는 않지만, 나중에 코드를 읽을 때 혼란을 주고 유지보수를 어렵게 만듭니다.

이번 작업은 **2026-05-11 슬롯/점유 시스템 폐기 이후 남겨진 코드**를 중심으로, 사용되지 않는 코드와 중복된 코드를 파악하는 조사 작업입니다. 실제 제거는 Plan.md 승인 이후에 진행합니다.

---

## 분석 대상

`Assets/_Project/Scripts/` 전체 .cs 파일 (약 115개)

---

## 발견 목록

### A. 명시적으로 비활성화된 Dead Code (슬롯/점유 시스템 폐기 잔재)

주석에 `[2026-05-11 비활성화]`가 명시되어 있거나, 호출 코드가 주석 처리된 것들.

#### A-1. TileMoveSlotManager.cs — 클래스 전체
- **파일**: `Assets/_Project/Scripts/Application/Services/TileMoveSlotManager.cs`
- **상태**: 어디서도 인스턴스 생성 및 메서드 호출 없음
- **근거**: GameBootstrapper 132-142줄에 "[2026-05-11 비활성화 — 슬롯 시스템 폐기]" 주석 명시
- **포함 메서드**: `ClaimSlot()`, `ReleaseSlot()`, `GetUnitCount()`, `TryGetSlot()`, `GetSlotWorldPosition()`

#### A-2. TileOccupancyManager.cs — 비활성화된 메서드들
- **파일**: `Assets/_Project/Scripts/Application/Services/TileOccupancyManager.cs`
- **비활성 메서드**:
  - `OnUnitMoved()` (83줄): UnitMovementUseCase에서 호출 코드 주석 처리됨 (162-171줄)
  - `OnUnitRemoved()` (105줄): GameEvents.OnEntityDied 구독이 주석 처리됨 (61-74줄)
  - `ReserveOccupancy()` (127줄): 어디서도 호출되지 않음
  - `BfsFindAvailable()` (146줄): FindForwardAvailable()에서만 호출되는데, FindForwardAvailable()도 호출되지 않음
  - `FindForwardAvailable()`: 외부 호출 없음

#### A-3. UnitMovementUseCase.cs — 점유 관련 메서드들
- **파일**: `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs`
- **비활성 메서드**:
  - `RegisterOccupancyMove()` (208줄): 본문이 비어있음 (껍데기만 존재)
  - `ReleaseOccupancy()` (228줄): 본문이 비어있음 (껍데기만 존재)
  - `GetOccupancySize()` (245줄): `UnitStats.GetOccupancySize()`를 그대로 호출하는 1줄 래퍼, 외부 호출 없음

#### A-4. UnitData.cs — ClaimedTile 프로퍼티
- **파일**: `Assets/_Project/Scripts/Domain/Unit/UnitData.cs:99`
- **상태**: set 해도 값을 실제로 활용하는 로직이 없음 (슬롯 시스템 폐기로 인해)
- **근거**: 주석 94-97줄에 "[2026-05-11 비활성화 — 슬롯 시스템 폐기]" 명시

---

### B. 의심 케이스 (추가 확인 필요)

#### B-1. HexPathfinder.FindPathToNeighbor() — 의심
- **파일**: `Assets/_Project/Scripts/Domain/Hex/HexPathfinder.cs:135`
- **상태**: 플로우 필드 이동 전환 이후 호출하는 곳을 찾지 못함
- **추가 확인 필요**: 근접 유닛 하이브리드 이동(Phase 0/1/2)에서 사용 여부 재확인

#### B-2. GameEvents.OnGamePaused / OnGameResumed — 의심
- **파일**: `Assets/_Project/Scripts/Application/Events/GameEvents.cs:530, 536`
- **상태**: 구독 코드는 GameUIManager에 있지만, 이벤트를 발행(OnNext)하는 코드가 없음
- **근거**: 주석에 "확장용 미사용" 명시
- **특이사항**: 향후 확장 대비용으로 남겨뒀을 가능성 있음

#### B-3. SingletonMonoBehaviour.cs — 의심
- **파일**: `Assets/_Project/Scripts/Core/SingletonMonoBehaviour.cs`
- **상태**: 프로젝트 어디에서도 이 클래스를 상속받는 클래스가 없음
- **추가 확인 필요**: 향후 사용 예정인 유틸리티인지 확인

---

### C. 중복 코드

#### C-1. UnitFactory vs BuildingFactory — 구조적 중복
- **파일들**:
  - `Assets/_Project/Scripts/Infrastructure/Factory/UnitFactory.cs`
  - `Assets/_Project/Scripts/Infrastructure/Factory/BuildingFactory.cs`
- **중복 내용**:
  - 이벤트 구독 패턴 동일
  - GameObject Dictionary 관리 동일 (`_unitObjects`, `_buildingObjects`)
  - 팀/종족 기반 프리팹 조회 로직 동일
  - Y 오프셋 적용 로직 동일
  - 부모 Transform 하위 배치 로직 동일
- **현재 영향**: 동작에는 문제 없음. 리팩토링 시 제네릭 베이스 팩토리로 추출 가능

---

## 정리 우선순위

| 우선순위 | 항목 | 이유 |
|---------|------|------|
| **높음** | A-1 TileMoveSlotManager (전체) | 명시적 폐기, 클래스 전체 제거 가능 |
| **높음** | A-2 TileOccupancyManager (비활성 메서드) | 명시적 폐기, 해당 메서드 제거 가능 |
| **높음** | A-3 UnitMovementUseCase (점유 메서드) | 빈 껍데기 메서드, 제거 대상 |
| **높음** | A-4 UnitData.ClaimedTile | 명시적 폐기, 프로퍼티 제거 가능 |
| 중간 | B-1 HexPathfinder.FindPathToNeighbor | 추가 확인 후 결정 |
| 낮음 | B-2 OnGamePaused/OnGameResumed | 향후 확장 가능성, 연기 |
| 낮음 | B-3 SingletonMonoBehaviour | 유틸리티 가능성, 연기 |
| 낮음 | C-1 Factory 중복 | 동작 정상, 리팩토링은 별도 작업으로 |

---

## 주의 사항

- A 그룹은 모두 슬롯/점유 시스템 폐기와 연관된 코드 → **한 번에 일괄 제거 권장**
- TileOccupancyManager는 클래스 자체를 지울 게 아니라 비활성 메서드만 제거 (클래스가 여전히 주입되고 있을 수 있음)
- Domain 레이어 파일(UnitData.cs, HexPathfinder.cs)은 제거 시 영향 범위 더 넓을 수 있음 → Plan 단계에서 참조 재검증 필요
