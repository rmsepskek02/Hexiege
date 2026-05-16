# Research — 건물 생성/파괴 시 유닛 이동 멈춤 현상 수정

## 작업 개요 (자연어 설명)

게임에서 건물이 생성되거나 파괴될 때, 이동 중인 유닛들이 잠깐 제자리에 멈추는 현상이 있다.
이 현상이 왜 발생하는지 원인을 파악하고, 유닛이 이동을 멈추지 않으면서도 새로운 경로로
자연스럽게 전환할 수 있는 방법을 조사한다.

---

## 현재 동작 흐름

건물이 생성되거나 파괴되면 아래 순서로 처리된다.

```
건물 생성/파괴
    → GameEvents.OnBuildingPlaced 또는 OnEntityDied 이벤트 발행
    → [두 가지 구독자가 동시에 반응]
        1) FlowFieldService.InvalidateAll()       ← 경로 캐시 전체 삭제
        2) GameBootstrapper.RepathAllAliveUnits() ← 살아있는 모든 유닛 순회
            → 각 UnitView.OnPathInvalidated()
                → IsInCombat() == false 이면
                    → 현재 이동 코루틴(MoveAlongPathV3) 강제 중단
                    → 새 경로로 코루틴 재시작
```

### 관련 파일 및 위치

| 역할 | 파일 | 핵심 위치 |
|------|------|-----------|
| 경로 캐시 무효화 | `FlowFieldService.cs` | `Initialize()` 내 이벤트 구독 (L73~83) |
| 모든 유닛 재경로 트리거 | `GameBootstrapper.cs` | `SetupEagerRepathOnBuildingChanges()` (L684), `RepathAllAliveUnits()` (L708) |
| 유닛 경로 무효화 처리 | `UnitView.cs` | `OnPathInvalidated()` (L492), `IsInCombat()` (L550) |
| 유닛 이동 메인 코루틴 | `UnitView.cs` | `MoveAlongPathV3()` (L603~) |

---

## 멈춤 발생 원인

### 일반적인 경우 (A* 이동 중인 유닛)

`OnPathInvalidated()`가 `IsInCombat() == false` 판정을 받으면 `MoveTo(newPath)`를 호출한다.
`MoveTo()`는 **현재 실행 중인 코루틴을 `StopCoroutine`으로 즉시 중단**하고, 새 코루틴을 시작한다.
이 중단-재시작 사이의 **1~2 프레임 공백**이 눈에 보이는 멈춤으로 나타난다.

### 전투 추격 중인 유닛 (BUG-001 — 2026-05-12 기록)

추격 단계에서는 `_combatTargetTransform == null`, `_isInCombatPursuit == true` 상태다.
`IsInCombat()`이 `_combatTargetTransform != null || _isInCombatPursuit`로 판정하므로
추격 중 플래그가 제대로 설정되어 있으면 repath가 차단된다.
→ 단, `_isInCombatPursuit`가 정확히 set/reset되지 않는 타이밍 문제가 있으면 여전히 멈출 수 있다.

---

## 제안된 해결 방향

### 핵심 아이디어: 코루틴 유지 + 타일 도착 시점에 경로 교체

코루틴을 중단하지 않고, 코루틴이 **다음 타일 중심에 도달하는 순간** 새 경로로 교체한다.

**구현 방식:**
1. `UnitView`에 `_pendingPath` 필드를 추가한다.
2. `OnPathInvalidated()`에서 `MoveTo(newPath)` 대신 `_pendingPath = newPath`만 저장한다.
3. `MoveAlongPathV3()` 내 각 타일 도착 시점에 `_pendingPath`가 있으면 경로를 교체하고 계속 진행한다.

```
현재: 코루틴 중단 → [1~2 프레임 멈춤] → 새 코루틴 시작

개선: 코루틴 유지 → 현재 Lerp 완료 → 타일 도착 순간 경로 교체 → 계속 이동
```

### 예외 케이스: 현재 Lerp 중인 타일에 건물이 생긴 경우

유닛이 **지금 향하고 있는 바로 그 타일**에 건물이 놓이면, 경로를 나중에 교체하면 건물을 뚫고 지나가게 된다.

**대응 방법:**

`OnPathInvalidated()` 시점에 유닛이 현재 이동 중인 목표 타일(다음 타일)의 walkable 여부를 확인한다.

| 상황 | 처리 |
|------|------|
| 현재 Lerp 목표 타일이 여전히 walkable | `_pendingPath`만 저장 → 부드럽게 교체 |
| 현재 Lerp 목표 타일이 막힘 (건물 생성됨) | 기존처럼 즉시 코루틴 재시작 (멈춤 불가피하지만 케이스가 매우 드묾) |

---

## 구현 시 주의사항

- `_pendingPath` 교체는 코루틴 내부 while 루프의 **각 타일 도착 직후**에 처리해야 한다. 이미 있는 `needRepath` 변수([UnitView.cs:659](../../../Scripts/Presentation/Unit/UnitView.cs))와 유사한 패턴으로 붙일 수 있다.
- 멀티플레이에서는 서버에서만 코루틴이 실행되므로 영향 범위는 서버(싱글 포함)로 한정된다.
- `_pendingPath`는 교체 후 즉시 null로 초기화해야 다음 프레임에 중복 적용되지 않는다.
- `MoveTo()`가 외부에서 직접 호출되는 경우(랠리포인트 등)에는 `_pendingPath`도 함께 null로 초기화해야 한다.

---

## 영향 범위

- `UnitView.cs` — `OnPathInvalidated()`, `MoveAlongPathV3()`, `MoveTo()` 수정 대상
- `GameBootstrapper.cs` — `RepathAllAliveUnits()` 는 그대로 유지 (트리거 구조 변경 없음)
- `FlowFieldService.cs` — 변경 없음

---

## 현재 상태 정리

| 항목 | 상태 |
|------|------|
| 멈춤 현상 재현 | 확인됨 (건물 생성/파괴 시 이동 유닛 전체 순간 멈춤) |
| 원인 파악 | 완료 (코루틴 중단-재시작 공백) |
| 해결 방향 결정 | 완료 (pending path 교체 + 목표 타일 walkable 체크) |
| Plan.md 작성 | 미완료 |
