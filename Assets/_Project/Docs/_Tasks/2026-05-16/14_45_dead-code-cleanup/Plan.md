# Plan — Dead Code & 중복 코드 정리

## 작업 목적 (자연어 설명)

2026-05-11 슬롯/점유 시스템 폐기 이후 코드베이스에 남아있는 사용되지 않는 코드를 제거하는 작업입니다.
폐기된 시스템의 잔재가 코드베이스에 그대로 남아있으면 나중에 읽는 사람이 이 코드가 살아있는 기능인지 폐기된 기능인지 구분하기 어렵습니다.
이번 작업으로 코드를 깔끔하게 정리하여 가독성과 유지보수성을 높입니다.

---

## ⚠️ 기존 로직 제거 근거 (WORKFLOW.md 규칙 — 최상단 기술)

이번 Plan에 포함된 코드 제거는 모두 **2026-05-11 슬롯/점유 시스템 폐기(GameSystemRules.md "재설계" 섹션)**에 직접 근거합니다.

> *"슬롯 기반 분산 방식을 전면 폐기하고, 겹침을 허용하는 단순한 구조로 전환했다. 이동 슬롯, 공격 슬롯, 타일 점유 한도 제거."*

각 항목의 제거 안전성 근거:
- **TileMoveSlotManager**: GameBootstrapper에서 "[비활성화]" 주석 명시, 인스턴스 생성 코드 없음
- **TileOccupancyManager 비활성 메서드**: 구독/호출 코드가 이미 주석 처리됨
- **UnitMovementUseCase 점유 메서드**: 메서드 본문이 비어있어 제거해도 동작 변화 없음
- **UnitData.ClaimedTile**: 주석에 "[비활성화]" 명시, 값이 어디서도 읽히지 않음
- **HexPathfinder.FindPathToNeighbor**: 프로젝트 전체 grep 결과 호출하는 곳 없음 (선언만 존재)
- **GameEvents.OnGamePaused/OnGameResumed**: 구독 코드는 있으나 OnNext() 발행 코드가 전혀 없음 — 이벤트가 절대 트리거되지 않는 상태. 사용자가 미사용 확인 후 제거 결정

**방침**: 이미 비활성화된(주석 처리된) 코드이므로 직접 제거 진행. 단, 제거 전 참조 재검증 필수.

---

## GameSystemRules.md 근거 매핑

| 제거 항목 | 근거 규칙 |
|---------|----------|
| TileMoveSlotManager (슬롯 시스템) | "재설계 (2026-05-11): 이동 슬롯, 공격 슬롯, 타일 점유 한도 제거" |
| TileOccupancyManager 점유 메서드 | "유닛 점유 정보는 경로 탐색에 사용하지 않으므로 공유 타일 상태에 포함하지 않는다" (규칙 3) |
| UnitMovementUseCase 점유 메서드 | "슬롯 기반 분산 방식 전면 폐기" |
| UnitData.ClaimedTile | "이동 슬롯 제거" |
| HexPathfinder.FindPathToNeighbor | 호출처 없음 (A* 직접 이동 방식으로 대체됨) — 2026-05-16 사용자 확인 후 제거 결정 |
| GameEvents.OnGamePaused/OnGameResumed | OnNext() 발행 코드 없음, 미사용 — 2026-05-16 사용자 확인 후 제거 결정 |

---

## 이번 작업에 포함하는 항목 (우선순위 높음)

### [제거 1] TileMoveSlotManager.cs — 파일 전체 삭제
- **파일**: `Assets/_Project/Scripts/Application/Services/TileMoveSlotManager.cs`
- **작업**: 파일 삭제
- **참조 재검증**: `TileMoveSlotManager`를 import하거나 사용하는 파일 없음 (Grep 확인 필요)
- **위험도**: 낮음 (GameBootstrapper에서 이미 비활성화 명시)

### [제거 2] TileOccupancyManager.cs — 비활성 메서드 제거
- **파일**: `Assets/_Project/Scripts/Application/Services/TileOccupancyManager.cs`
- **제거 대상 메서드**:
  - `OnUnitMoved()` (83줄)
  - `OnUnitRemoved()` (105줄)
  - `ReserveOccupancy()` (127줄)
  - `BfsFindAvailable()` (146줄)
  - `FindForwardAvailable()`
- **주의**: 클래스 자체는 유지 (다른 메서드나 DI 주입이 남아있을 수 있음)
- **위험도**: 낮음 (이미 호출 코드가 주석 처리됨)

### [제거 3] UnitMovementUseCase.cs — 점유 관련 메서드 제거
- **파일**: `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs`
- **제거 대상 메서드**:
  - `RegisterOccupancyMove()` (208줄) — 본문이 비어있음
  - `ReleaseOccupancy()` (228줄) — 본문이 비어있음
  - `GetOccupancySize()` (245줄) — 외부 호출 없음
- **주의**: 인터페이스(IUnitMovementUseCase 등)에 선언이 있다면 인터페이스에서도 함께 제거
- **위험도**: 낮음 (메서드 본문 비어있음)

### [제거 4] UnitData.cs — ClaimedTile 프로퍼티 제거
- **파일**: `Assets/_Project/Scripts/Domain/Unit/UnitData.cs`
- **제거 대상**: `ClaimedTile` 프로퍼티 (99줄)
- **주의**: Domain 레이어이므로 참조 범위가 넓을 수 있음 — 제거 전 전체 grep 필수
- **위험도**: 중간 (Domain 레이어, 참조 재검증 필요)

---

## 추가 제거 항목 (2026-05-16 사용자 확인 후 추가)

### [제거 5] HexPathfinder.FindPathToNeighbor() — 메서드 제거
- **파일**: `Assets/_Project/Scripts/Domain/Hex/HexPathfinder.cs`
- **제거 근거**: 프로젝트 전체 grep 결과 호출하는 곳이 단 한 곳도 없음. 선언만 존재.
- **위험도**: 낮음

### [제거 6] GameEvents.OnGamePaused / OnGameResumed — 관련 코드 전체 제거
- **파일들**:
  - `Assets/_Project/Scripts/Application/Events/GameEvents.cs` — Subject 선언 2개 제거
  - `Assets/_Project/Scripts/Presentation/UI/GameUIManager.cs` — Subscribe 구독 코드, _uis[i].OnGamePaused()/OnGameResumed() 호출 코드 제거
  - `Assets/_Project/Scripts/Presentation/UI/Core/IGameUI.cs` — OnGamePaused(), OnGameResumed() default 메서드 제거
- **제거 근거**: OnNext() 발행 코드 없음. 구독해도 절대 호출되지 않는 상태. 사용자가 미사용 확인.
- **위험도**: 낮음

---

## 이번 작업에서 제외하는 항목 (보류)

| 항목 | 보류 이유 |
|------|---------|
| SingletonMonoBehaviour.cs | 향후 사용 예정 유틸리티일 수 있음 |
| UnitFactory/BuildingFactory 중복 | 동작 정상, 별도 리팩토링 작업으로 분리 |

---

## 작업 순서

1. **참조 검증**: 제거 대상 식별자(클래스명, 메서드명, 프로퍼티명)를 Grep으로 전체 검색
2. **제거 1**: TileMoveSlotManager.cs 파일 삭제
3. **제거 2**: TileOccupancyManager.cs에서 비활성 메서드 제거
4. **제거 3**: UnitMovementUseCase.cs에서 점유 관련 메서드 제거 (인터페이스 동기화 포함)
5. **제거 4**: UnitData.cs에서 ClaimedTile 프로퍼티 제거
6. **컴파일 확인**: 유니티에서 컴파일 에러 없는지 확인

---

## 예상 위험 요소

| 위험 요소 | 대응 방안 |
|---------|---------|
| 인터페이스에 메서드 선언이 남아있는 경우 | 제거 전 인터페이스 파일 grep 후 동기화 |
| ClaimedTile이 예상치 못한 곳에서 참조되는 경우 | Domain 레이어 파일 전체 grep 후 제거 |
| Unity .meta 파일 처리 | TileMoveSlotManager.cs 삭제 시 .meta 파일도 함께 삭제 |

---

## 위임

- **담당 에이전트**: game-programmer
- **작업 유형**: 코드 제거 (구현 없음, 삭제 위주)
