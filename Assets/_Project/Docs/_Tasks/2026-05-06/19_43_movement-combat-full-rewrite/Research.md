# Research — 이동/전투 시스템 전면 재구현

작성일: 2026-05-06  
규칙 문서: `Docs/GameSystemRules.md`

---

## 이 작업이 무엇인지

현재 이동/전투 시스템은 규칙이 정해지기 전에 구현된 결과물이다.
고칠수록 다른 부분이 망가지고 버그 패치가 쌓이는 이유가 여기에 있다.
설계의 근거가 없는 상태에서 만들어진 코드라, 부분 수정이 아닌 전면 재구현이 필요하다.

이 작업은 GameSystemRules.md의 18개 규칙을 유일한 기준으로 삼아 이동과 전투 로직을 처음부터 다시 만든다.

---

## 현재 관련 파일 목록

### Presentation
| 파일 | 역할 |
|------|------|
| `Presentation/Unit/UnitView.cs` | 이동 코루틴(`MoveAlongPathV2`), 슬롯 헬퍼, 전투 진입/종료 흐름 전체 |

### Application/Services
| 파일 | 역할 |
|------|------|
| `Application/Services/TileMoveSlotManager.cs` | 규칙 10/17 기반 이동 슬롯 3개 관리 (신규 생성됨) |
| `Application/Services/TileSlotManager.cs` | 레거시 6슬롯 — 비활성 예정 상태 |
| `Application/Services/TileOccupancyManager.cs` | 타일당 점유 합계 추적, 앞쪽 우회 BFS |
| `Application/Services/AttackPositionManager.cs` | 타겟 주변 12방향 공격 슬롯 관리 |
| `Application/Services/MovementLogger.cs` | 이동 로그 기록 (유지) |

### Application/UseCases
| 파일 | 역할 |
|------|------|
| `Application/UseCases/UnitMovementUseCase.cs` | `FindForwardAvailable`, `FindForwardClosestTile` 등 이동 보조 메서드 |

### Bootstrap
| 파일 | 역할 |
|------|------|
| `Bootstrap/GameBootstrapper.cs` | `_moveSlotManager`, `_attackPositionManager` 초기화/주입 |

---

## 현재 구현의 문제점 (규칙 기준)

### MoveAlongPathV2 (UnitView.cs)

규칙 없이 짜여진 코루틴이 점진적으로 패치되어 내부 조건이 서로 충돌하는 구조가 됐다.
최근 05-06에만 버그 수정 두 건(BUG-005~008, 규칙 13/15 위반 재수정)이 발생했다는 것 자체가 설계 기반의 부재를 보여준다.

구체적 위반 사례:
- **규칙 13 위반**: 원거리 유닛이 전투 진입 시 이동 슬롯을 해제함 → 규칙은 "이동 슬롯 유지한 채 공격"
- **규칙 5 위반**: `FindAvailableTile`의 forward BFS 실패 시 방향 무관 fallback BFS를 수행 → 규칙은 "뒤로 우회 절대 금지, 없으면 대기"
- **규칙 11 위반**: 공격 슬롯 해제 조건이 "타겟 감지 사거리 이탈"과 정확히 일치하지 않음
- **규칙 15 위반**: 전투 종료 후 A* 재개 타일 선택이 규칙(앞쪽 가장 가까운 타일)과 다른 경로를 탐
- **근본 문제**: `RunTileTraversal`, `EnterMeleePursuit`, `EnterStationaryCombat` 등으로 잘게 나뉜 구조가 각 헬퍼 간 상태 공유를 복잡하게 만들어, 어디서 슬롯을 잡고 어디서 해제해야 하는지 명확하지 않음

### TileOccupancyManager.cs

`FindAvailableTile`에 forward fallback이 남아 있어 규칙 5 위반.
`FindForwardAvailable`을 별도로 만들었지만, 기존 메서드와 공존하면서 호출 측에서 혼용될 여지가 있음.

### AttackPositionManager.cs

규칙 18의 12방향(30°) 슬롯 자체는 방향이 맞지만, 슬롯 배정 기준("접근 방향과 가장 가까운 빈 슬롯")과 비교해 현재 구현이 "배정된 유닛 수가 적은 위치 우선"으로 달리 동작한다.

### TileMoveSlotManager.cs

규칙 17의 삼각 배치 자체는 방향이 맞지만, 구현 내부 로직이 V2 코루틴의 잘못된 흐름을 전제로 설계돼 있어 재구현 대상이다.

---

## 유지할 것

아래는 규칙과 충돌하지 않거나, 이동/전투와 무관한 시스템이다.

| 유지 대상 | 이유 |
|-----------|------|
| `FlowFieldService` / `HexFlowField` | 규칙 2의 A* 경로탐색 역할을 정확히 수행 |
| `HexGrid`, `HexTile` | Domain 레이어 — 변경 없음 |
| `UnitData`, `UnitStats`, `UnitCombatUseCase` | 전투 판정 수치 로직은 유지 |
| `UnitMovementUseCase.RequestMove` | FlowField 조회 부분은 규칙 2와 일치 |
| `UnitMovementUseCase.ProcessStep` | 타일 이동 완료 처리 (도메인 Position 갱신) |
| `TileOwnershipService` | 타일 소유권 추적 — 이동/전투 재구현과 별개 |
| `MovementLogger` | 로그 유틸리티 — 유지하며 계속 사용 |
| `GameBootstrapper` 전체 구조 | 초기화 코드만 일부 수정 예정 |
| Domain 레이어 전체 | 수정 없음 |

---

## 제거/재구현 대상 요약

| 파일 | 처리 방식 |
|------|---------|
| `UnitView.cs` — `MoveAlongPath` (yield break) | **삭제**: 이미 비활성, V3로 대체 |
| `UnitView.cs` — `MoveAlongPathV2` 본문 전체 | **비활성화 후 삭제**: #if false 처리 → 검증 후 제거 |
| `UnitView.cs` — 관련 헬퍼 메서드(`RunTileTraversal`, `EnterMeleePursuit`, `EnterStationaryCombat` 등) | **비활성화 후 삭제** |
| `UnitView.cs` — V2 전용 상태 필드(`_v2MoveSlotTile`, `_v2AttackSlotTargetCoord`, `_v2InStationaryCombat`) | **V3 전환 후 제거** |
| `TileMoveSlotManager.cs` | **전체 재구현** |
| `TileSlotManager.cs` | **삭제**: 레거시, V3 흐름에서 불필요 |
| `TileOccupancyManager.cs` | **전체 재구현**: forward-only BFS만 남기고 fallback 제거 |
| `AttackPositionManager.cs` | **전체 재구현**: 배정 기준 교체 |
| `UnitMovementUseCase.cs` — `FindAvailableTile`, `FindForwardAvailable`, `FindForwardClosestTile` 등 | **재구현**: 규칙 5, 15 기준으로 정리 |
| `GameBootstrapper.cs` — 관련 초기화 코드 | **수정**: 재구현된 매니저로 교체 |
