# Research — 패스파인딩 개선

**날짜:** 2026-04-25
**작업명:** pathfinding-improvement

---

## 개요

유닛이 많아졌을 때 멈추는 현상, 길이 열려있음에도 돌아가는 현상 등
패스파인딩 관련 다수의 문제가 보고됨.
코드를 직접 읽어 원인을 파악함.

---

## 관련 파일

| 파일 | 역할 |
|------|------|
| `Assets/_Project/Scripts/Domain/Hex/HexPathfinder.cs` | A* 알고리즘 구현 (순수 C#) |
| `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs` | 경로 계산 진입점, 차단 목록 구성 |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | 3-Phase 이동 코루틴 (Phase 0/1/2) |

---

## 현재 아키텍처 파악

### A* 알고리즘 (HexPathfinder.cs)

- 187개 타일(11×17) 대상 표준 A* 구현
- `SortedSet<Node>` 기반, F = G + H (H = 헥스 거리)
- **호출 시점: 이동 명령 시 1회** + Phase 2 재개 시

### 경로 계산 진입 (UnitMovementUseCase.RequestMove)

경로 계산 시 차단 목록(`blocked`)을 구성하는 방식:

```
같은 팀 유닛의 Position      ← 현재 서 있는 타일
같은 팀 유닛의 ClaimedTile   ← Lerp 이동 중 선점한 타일
적 팀                        ← 차단 목록에 포함하지 않음 (전투로 해결)
```

`HexPathfinder.FindPath` 호출 시 위 차단 목록을 넘겨 우회 경로를 계산.
경로가 없고 목표가 건물 타일이면 `FindPathToNeighbor`로 인접 타일까지 경로 계산.

### 3-Phase 이동 (UnitView.MoveAlongPath)

**Phase 0 — 타일 기반 A* 이동:**
- `path` 리스트를 인덱스 1부터 순회
- 각 스텝 시작 전 `IsTileBlockedBySameTeam(to)` 체크
  - 막혀있으면 → `RequestMove` 재호출 (경로 재계산)
  - 경로 없으면 → `break` (이동 중단)
  - 경로 있으면 → `i = 0; continue` (새 경로 처음부터)
- 막히지 않으면 → `ClaimedTile = to` 선점 후 Lerp 이동
- 타일 도착 후 감지 사거리 체크: 적 감지 시 `shouldPursue = true`로 Phase 1 진입

**Phase 1 — 월드 좌표 직선 추적:**
- 타일 단위 이동이 아닌 실시간 적 위치로 직선 이동
- 적이 공격 사거리 진입 → 전투 루프
- 적 감지 사거리 이탈 또는 파괴 → Phase 2로

**Phase 2 — 가장 가까운 타일 스냅 + A* 재개:**
- 현재 월드 좌표 → `WorldToHex`로 가장 가까운 타일 계산
- 해당 타일 중심으로 이동 후 `ProcessStep`으로 도메인 좌표 동기화
- `finalTarget`으로 `RequestMove` 재호출 → Phase 0 재개

---

## 문제 원인 분석

### 문제 1: 유닛이 많아졌을 때 멈추는 현상

**원인: 경로 없음 시 조용히 중단 (UnitView.cs:527-533)**

```
Phase 0 per-step 차단 체크:
  if (IsTileBlockedBySameTeam(to))
      newPath = RequestMove(finalTarget)
      if (newPath == null)
          break  ← 이동 완전 중단, 재시도 없음
```

유닛이 많아지면 아군의 Position + ClaimedTile이 그리드의 많은 타일을 점유.
A*가 우회 경로를 찾으려 해도 모든 후보가 차단되면 `null`을 반환.
`null` 반환 시 `break`로 이동이 완전히 종료되며, **재시도 로직이 없음.**

`IsTileBlockedBySameTeam` 판정이 실제보다 넓게 잡히는 구조적 이유:
- Lerp 이동 시간 동안 `ClaimedTile`이 유지됨
- MoveSpeed가 낮을수록(느린 유닛) 점유 시간이 길어짐
- n개 유닛이 동시에 이동하면 최대 n개 타일이 추가로 차단됨
- 유닛이 5~6개만 되어도 협로에서 완전 교착(deadlock) 발생 가능

**재현 조건:** 좁은 경로(1~2타일 폭)에 같은 팀 유닛이 4개 이상 동시 이동

---

### 문제 2: 길이 열려있음에도 돌아가는 현상

**원인: 경로 재계산 조건이 "차단됐을 때"만 (UnitView.cs:520-533)**

```
Phase 0 재계산 조건:
  IsTileBlockedBySameTeam(to) == true 일 때만 재계산
```

경로 계산 초기 시점에 아군이 막고 있어 우회 경로가 계산됨.
이후 그 아군이 이동해서 원래 짧은 경로가 열려도:
- 현재 경로의 다음 타일이 막혀있지 않으면 재계산이 전혀 발생하지 않음
- 더 좋은 경로가 생겼다는 트리거가 없음
- 유닛은 최초 계산된 우회 경로를 끝까지 고수

**재현 조건:**
1. 유닛 A가 이동 경로를 막고 있을 때 유닛 B가 이동 명령 수신 → 우회 경로 계산
2. 유닛 A가 이동하여 원래 경로가 열림
3. 유닛 B는 기존 우회 경로로 계속 이동 (더 긴 경로 유지)

---

### 문제 3: ClaimedTile 장기 점유 (부가 문제)

**원인: Lerp 이동 완료 전까지 ClaimedTile 유지**

```
ClaimedTile 해제 시점:
  - Lerp 이동 완료 후 (UnitView.cs:699)
  - 이동 전체 완료 후 (UnitView.cs:950)
  - Phase 1 진입 시 (UnitView.cs:719)
```

문제: 이동 중 전투 루프(`HasEnemyInRange`)에 진입하면 Lerp가 정지하지만
ClaimedTile은 해제되지 않음 (전투 중에는 `ClaimedTile = null` 코드가 없음).
→ 전투 중인 유닛의 ClaimedTile이 유지되어 후속 유닛 경로 탐색에 방해.

---

## 잠재적 개선 방향 (Plan.md에서 구체화)

### 방향 A: 경로 없음 시 대기 + 재시도

- `null` 반환 시 즉시 중단 대신 잠시 대기 후 재시도
- 구현 단순, 효과 확실, 리스크 낮음
- 단점: 대기 시간 튜닝 필요, 영구 교착 시 무한 대기 가능

### 방향 B: 주기적 경로 재계산 (더 짧은 경로 탐색)

- 일정 타일 이동마다 현재 경로보다 짧은 경로가 있는지 확인 후 교체
- 문제 2(돌아가는 현상) 해결에 직접적
- 단점: 추가 A* 계산 오버헤드 (187타일 규모라 미미하나 유닛 수 비례)

### 방향 C: 전투 중 ClaimedTile 해제

- 전투 루프 진입 시 `ClaimedTile = null`
- 기존 이동 중 ClaimedTile 관련 코드와 정합성 확인 필요
- 구현 간단, 문제 3 직접 해결

---

## 요약

| 문제 | 원인 코드 위치 | 심각도 |
|------|---------------|--------|
| 유닛 많으면 멈춤 | UnitView.cs:527-533 (경로 없음 → break) | 높음 |
| 돌아가는 현상 | UnitView.cs:520 (재계산 조건 협소) | 중간 |
| 전투 중 ClaimedTile 미해제 | UnitView.cs 전투 루프 내 | 낮음 |
