# B3 중심 경로·전투 추격·복귀 교정 Research

이 작업은 유닛이 기본 경로의 타일 중심을 건너뛰거나, 적을 추격하다 건물을 향해 반복해서 멈추는 문제를 바로잡기 위한 조사 기록이다. 지금까지 완료한 서버 권위, Simulation Root와 Visual Root 분리, 행동 reducer와 네트워크 복제 구조는 유지한다. 실제 이동을 쓰는 B3 안에서 잘못 정의된 경유점 완료 조건과 전투 추격·복귀 경로만 교정한다.

## 1. 사용자 관측과 재현 증거

- 사용자는 기본 이동이 타일 중심에서 중심으로 진행하고, 적을 감지한 전투 상황에서만 그 경로를 벗어나는 설계로 기억하고 있다.
- 2026-08-24 Android Host 로그의 세 유닛이 이동 가능한 우회로가 있는 상황에서 `NonWalkable` 타일로 반복 진입을 시도했다.
- 동일 경기에서 서버 단일 writer, Root 공간 전이, 클라이언트 직접 쓰기 금지와 복제 자체는 정상 범위였다. 실패는 권위 구조가 아니라 B3가 만든 이동 의도와 경로의 의미에서 발생했다.
- 근거 로그: `Assets/_Project/Docs/_Logs/2026-08-24/17_03_logcat/RuntimeLog_device.txt`
  - Unit 75와 22: `(5, 8) → (4, 8)` 후보가 `NonWalkable`로 반복 거부
  - Unit 104: `(3, 14) → (3, 15)` 후보가 `NonWalkable`로 반복 거부
  - terminal summary: `failureDetails=3`, `spatialRepeatedRecoverableRepaths=3`

## 2. 규칙 모순

`GameSystemRules_Units.md`에는 다음 두 계약이 동시에 있었다.

1. 재설계 설명: 일반 경유 타일 중심 통과를 강제하지 않는다.
2. 규칙 2: A*로 타일 중심에서 타일 중심으로 이동한다.

B3 구현과 self-validation은 첫 번째 계약을 선택했다. 중간 waypoint에 도달하지 않아도 look-ahead 영역에서 더 이상 거리가 줄지 않으면 checkpoint를 소비하도록 만들었고, 60도 코너 검증도 “중심 생략을 포함한 연속 선회”를 정상으로 고정했다. 따라서 규칙 2를 기준으로 보면 진단 PASS가 실제 요구를 증명하지 못했다.

## 3. 코드 대조 결과

### 3.1 기본 A* checkpoint

- `UnitServerTrajectoryPlanner.TryPlan`은 마지막 목적지만 정확히 수렴한다.
- 중간 waypoint는 중심 허용 오차 도달이 아니라 look-ahead 구역의 최근접 통과로 `ReachedCurrentWaypoint=true`가 된다.
- `UnitPathCheckpointTracker`는 전달받은 `reachedWaypoint`가 참인지만 확인하므로 잘못된 최근접 판정을 그대로 순서 진행으로 확정한다.

### 3.2 전투 추격

- `UnitView.EnterCombatPursuitV3`는 매 frame `enemyViewPos - transform.position`으로 직선 후보를 만든다.
- 이 호출은 A*와 달리 logical corridor를 전달하지 않는다.
- 공간 preflight가 건물 타일을 정상적으로 거부해도 caller는 일반 성 목적지 A*를 받은 뒤, 적이 계속 감지되면 같은 무경로 직선 Chase로 재진입한다.
- 따라서 “건물을 막힘으로 판정하는 기능”은 정상이나 “타겟까지 안전한 경로를 만드는 기능”이 빠져 있다.

### 3.3 전투 종료 복귀

- `FindForwardClosestTile`은 가장 가까운 타일의 인접 6칸 중 성에 더 가까운 walkable 타일 하나를 고른다.
- 후보가 없으면 `nearestTile`을 그대로 반환하는데, 그 타일 자체가 이동 가능한지와 현재 Root에서 중심까지 안전하게 도달 가능한지는 보장하지 않는다.
- 호출부는 두 타일짜리 corridor로 직선 복귀를 먼저 시도한다. 안전하지 않으면 일반 목적지 A*로 넘어가지만, 복귀 checkpoint 선택과 우회 경로의 의미가 하나의 계약으로 묶여 있지 않다.

## 4. 영향 범위

| 단계 | 판정 | 이유 |
|---|---|---|
| A1 | 유지 | 행동 계약·reducer는 타일 중심 기하와 독립적이다. |
| A2 | 유지 | 서버 권위 Pose와 관측 seam은 계속 필요하다. |
| B1 | 유지 | Simulation Root/Visual Root, 프리팹 구조와 NetworkTransform 계약은 변경하지 않는다. |
| B2 | 구현 유지·검증 갱신 | movement phase, scope, 10°/15°와 shadow 체계는 유지하되 새 중심/추격 사례를 회귀 검증에 추가한다. |
| B3 | 부분 재기준화 | 실제 trajectory checkpoint, Chase route, PostCombatResume target을 수정한다. |

공격 방향, AttackSequence, Impact와 피해 적용 시점은 이번 범위에 포함하지 않는다.

## 5. 원인 가설과 판별 기준

1. **중심 생략 checkpoint가 규칙 불일치의 직접 원인이다.** 중심 허용 오차 밖에서는 중간 checkpoint가 절대로 소비되지 않도록 하면 기존 60도 fixture의 일부가 먼저 실패해야 한다.
2. **무경로 direct Chase가 건물 반복 접근의 직접 원인이다.** 공격 접근 위치까지 안전한 직선 또는 서버 path를 제공하면 `NonWalkable` 동일 후보 반복이 사라져야 한다.
3. **복귀 후보의 안전성 미검증이 전투 후 정지 위험을 만든다.** 안전하고 도달 가능한 전방 중심만 선택하고 필요 시 복귀 A*를 사용하면 non-walkable 현재 타일 폴백이 사라져야 한다.
4. **현재 진단 seam이 실제 호출 형태를 덮지 못한다.** “유닛과 적 사이에 아군 건물이 있는 Chase”와 “중심을 벗어난 뒤 안전한 전방 중심으로 복귀” fixture를 추가하면 기존 구현에서 실패하고 교정 구현에서 통과해야 한다.

## 6. 유지해야 하는 아키텍처 불변식

- 서버만 Simulation Root의 위치·방향과 이동 phase/scope를 쓴다.
- A*·Chase·PendingRepath·PostCombatResume는 같은 Authoritative Locomotion과 공간 preflight/commit을 사용한다.
- 위치 변화 방향과 SimulationFacing 오차는 1° 이하다.
- NetworkTransform은 서버 결과만 복제하고 Visual Root는 판정에 쓰지 않는다.
- 한 frame에 재탐색은 최대 한 번이며, 같은 unsafe 후보를 같은 frame에 재시도하지 않는다.
- 유닛 겹침은 허용하고 Collider 또는 유닛 footprint를 새 경로 차단 규칙으로 추가하지 않는다.

## 7. 조사 결론

문제는 A1부터 B2까지의 서버 권위 아키텍처가 아니라 B3의 세 하위 계약이다. 기본 A* 중심 checkpoint, 타겟을 고려한 안전 Chase, 안전한 전방 중심 Rejoin을 하나의 서버 이동 경로 체계로 묶어 교정해야 한다. 과거 B3 PASS 문자열은 당시 순수 계약의 통과 기록으로 보존하되, 새 규칙의 완료 증거로 재사용하지 않는다.

## 8. 2026-08-24 v10 실기 결론

- 같은 경기 `sharedSessionKey=30780b...ce11`에서 Android Host와 Editor Client가 모두 `b3-movement-authority-v10`을 기록했다.
- Host는 중심 checkpoint 766회를 최대 오차 `0.000000000`으로 완료했고, 안전한 direct Chase 806 frame과 서버 경로 Chase 2,977 frame을 사용했다.
- Host의 이동 terminal은 `rejected=0`, `invalid=0`, `gateFailures=0`, `clientRootOrReducerWriteAttempts=0`, `attackWriterOwnershipConflicts=0`, `stationaryWalkViolations=0`, `failureDetails=0`, `adapterFailures=0`, `droppedLogs=0`, `verdict=EVIDENCE`였다.
- Host와 Client의 Root observer는 각각 53개 stable endpoint와 53개 회전 증거를 관측·기록했고 local verdict는 모두 PASS였다.
- 공식 CrossAudit의 `stable-endpoint-count-or-contract-mismatch`는 Host `summary-END`가 `rotationEvidenceSchema=root-rotation-re`에서 잘린 결과다. 개별 endpoint·회전 증거와 MOVE terminal은 완전하므로 게임 구현 실패 증거가 아니라 Android Logcat 한 줄 길이 계약을 위반한 채점기/관측 요약 결함이다.

따라서 이번 v10 중심 경로·건물 안전 Chase·전방 중심 복귀 집중 교정은 **PASS**로 닫는다. 긴 terminal 요약은 다음 코드 변경에서 Android-safe compact terminal로 교정하고, 이 진단기 결함만을 이유로 동일 빌드를 다시 만들거나 동일 경기를 반복하지 않는다. 25종·양방향 역할교대·Legacy rollback은 삭제하지 않고 Tracer C 이후 권위 전환 전 통합 회귀 게이트로 유지한다.
