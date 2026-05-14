# Research: 타일 소유권 실시간 감지 시스템

**날짜**: 2026-04-26  
**작업 ID**: 17_00_tile-ownership-detection  
**담당**: game-programmer 에이전트

---

## 0. 작업 배경 (파생 경위)

이 작업은 **`15_00_phase1-target-reselect`(뒷무빙 수정)** 작업 중 발견된 구조적 문제에서 파생되었다.

뒷무빙 수정 작업에서 Phase 1(월드 좌표 직선 추적) 모드의 동작을 분석하던 중,  
유닛이 Phase 1으로 이동할 때 **지나가는 타일이 점령되지 않는다**는 사실이 확인되었다.

뒷무빙 수정(`15_00`)은 **Phase 1 타겟 재선택 로직**을 다루는 별도 작업이고,  
타일 소유권 실시간 갱신은 이동 시스템 전반에 걸친 **독립된 구조 개선**이므로 별도 task로 분리하였다.

---

## 1. 배경 및 문제

유닛이 Phase 0(타일 기반 Lerp 이동) 중에만 `ProcessStep`을 통해 타일 소유권이 갱신된다.  
Phase 1(월드 좌표 직선 추적) 중에는 유닛이 물리적으로 여러 타일을 지나가더라도  
**도메인이 이를 전혀 인지하지 못해 타일 소유권이 갱신되지 않는다.**

---

## 2. 현재 구조 분석 (Push 모델)

```
[Phase 0] 유닛이 타일 이동 완료
    → ProcessStep(from, to) 호출
        → _grid.SetOwner(to, unit.Team)
        → GameEvents.OnTileOwnerChanged 발행
        → _unitData.Position = to (도메인 위치 갱신)

[Phase 1] 유닛이 월드 좌표로 직선 이동
    → ProcessStep 호출 없음
        → _grid.SetOwner 없음
        → 이벤트 없음
        → _unitData.Position 고정 (Phase 1 진입 시점 타일)
```

### 핵심 원인

Phase 1은 **뷰(transform.position)만 움직이고 도메인(_unitData.Position)을 움직이지 않는다.**  
타일 소유권 갱신은 도메인 시스템(`ProcessStep`)을 통해서만 발생하므로,  
Phase 1에서 유닛이 아무리 많은 타일을 지나가도 소유권이 바뀌지 않는다.

---

## 3. 설계 방향 — TileOwnershipService (Pull 모델)

### 3-0. 대안 검토 및 선택 이유

이 문제를 해결하는 방법으로 세 가지 방향이 검토되었다.

| 방향 | 내용 | 탈락 이유 |
|---|---|---|
| A. Phase 1에서 ProcessStep 직접 호출 | 타일 경계를 넘을 때 ProcessStep 호출 → 도메인 Position 실시간 갱신 | ProcessStep은 `_unitData.Position`과 점유(Occupancy)를 함께 갱신하므로, Phase 1의 "도메인 위치 고정" 설계와 충돌. 3·4차 점유 개선과의 정합성 검토 비용이 큼. |
| B. UnitData에 CurrentTile 필드 추가 | `Position`(점유·경로용) + `CurrentTile`(실시간 물리 위치용) 두 개로 분리 | UnitData(Domain)에 "어느 상황에 어느 필드를 쓰는가"라는 혼동 가능한 개념이 추가됨. 레이어별 규칙 명확성이 낮아짐. |
| **C. TileOwnershipService (채택)** | 독립 서비스가 매 프레임 물리 위치 기반으로 타일 소유권만 판정 | 기존 도메인 설계(ProcessStep, 점유 시스템) 변경 없음. 이동 방식(Phase 0/1/2)과 완전히 독립적. 새 Phase가 추가되어도 자동 대응. |

**채택 이유 요약**: 기존 점유·경로 시스템을 건드리지 않으면서,  
이동 Phase와 무관하게 소유권 갱신을 보장하는 가장 안전한 방식이기 때문.

---

유닛이 타일에 알리는 방식(Push)이 아니라,  
**독립 서비스가 매 프레임 모든 유닛의 물리 위치를 확인하여 타일 소유권을 결정**한다.

```
[TileOwnershipService.Tick() — 매 프레임]
    → UnitSpawnUseCase.Units 순회 (모든 살아있는 유닛)
    → IEntityPositionProvider.GetUnitWorldPosition(id) → 뷰 좌표
    → ViewConverter.FromView(viewPos) → 도메인 좌표
    → HexMetrics.WorldToHex(domainPos) → HexCoord
    → 타일별 팀 존재 여부 집계
    → 점령 규칙 적용 → _grid.SetOwner + 이벤트 발행
```

---

## 4. 점령 규칙

| 케이스 | 동작 |
|---|---|
| 한 팀만 있음 | 즉시 그 팀으로 점령 |
| 양 팀 모두 있음 | 현재 상태 유지 (아무것도 하지 않음) |
| 유닛 없음 | 현재 상태 유지 (아무것도 하지 않음) |

**Phase 1 통과 시**: 유닛이 잠깐 지나가더라도 그 순간 소유권이 바뀌고 유지된다. (의도된 동작)

---

## 5. 관련 코드 위치

### 데이터 소스

| 항목 | 경로 | 설명 |
|---|---|---|
| 전체 유닛 목록 | `UnitSpawnUseCase.Units` | `IReadOnlyDictionary<int, UnitData>` — id → 유닛 데이터(Team, IsAlive) |
| 유닛 월드 좌표 | `IEntityPositionProvider.GetUnitWorldPosition(id)` | 뷰 좌표(transform.position) 반환. 소멸 시 Vector3.zero |
| 뷰→도메인 변환 | `ViewConverter.FromView(viewPos)` | 뷰 좌표 → 도메인 월드 좌표 (Red팀 좌표 반전 처리 포함) |
| 도메인→타일 변환 | `HexMetrics.WorldToHex(domainPos)` | 도메인 월드 좌표 → HexCoord |

### 출력 대상

| 항목 | 경로 | 설명 |
|---|---|---|
| 타일 소유권 설정 | `HexGrid.SetOwner(tile, team)` | 도메인 그리드 소유권 갱신 |
| 이벤트 발행 | `GameEvents.OnTileOwnerChanged` | `HexTileView`가 구독하여 색상 변경 |

### Tick 호출 위치

| 항목 | 경로 |
|---|---|
| 기존 패턴 | `GameBootstrapper.Update()` → `_unitCombat.TickCooldowns(Time.deltaTime)` |
| 추가 위치 | `GameBootstrapper.Update()` → `_tileOwnership.Tick()` |

---

## 6. 기존 ProcessStep과의 관계

`ProcessStep`은 **Phase 0 전용 도메인 이동 처리**로 그대로 유지된다.  
`TileOwnershipService`는 `SetOwner`와 이벤트 발행만 담당하며,  
`_unitData.Position` 갱신·점유(Occupancy) 변경은 하지 않는다.

두 시스템이 같은 `_grid.SetOwner`를 호출할 수 있는데,  
Phase 0에서는 ProcessStep이, Phase 1에서는 TileOwnershipService가 처리하므로  
**중복 호출이 발생하더라도 결과는 동일**하여 문제없다.

---

## 7. 네트워크 고려 사항

서버 권위 모델(NGO 2.9.2) 기준:
- `TileOwnershipService.Tick()`은 **서버(또는 싱글플레이)에서만 실행**
- 클라이언트는 `NetworkTileSync`를 통해 `OnTileOwnerChanged` 이벤트를 수신하는 기존 방식 그대로 유지
- 기존 `GameBootstrapper.Update()`에 서버 조건 가드(`!NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer`) 패턴 확인 필요

---

## 8. 변경 대상 파일

| 파일 | 변경 유형 |
|---|---|
| `Application/Services/TileOwnershipService.cs` | 신규 생성 |
| `Bootstrap/GameBootstrapper.cs` | TileOwnershipService 생성 + Update()에 Tick() 호출 추가 |
