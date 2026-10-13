# B3 중심 경로·전투 추격·복귀 교정 Plan

이 계획은 기본 이동을 다시 타일 중심의 연속으로 만들고, 전투 중에는 건물을 통과하려 하지 않도록 안전한 추격 경로를 제공하며, 전투가 끝나면 안전한 전방 타일 중심으로 걸어서 복귀시키는 작업이다. 서버 권위와 지금까지 만든 A1·A2·B1·B2 구조는 그대로 사용한다. 구현량을 줄이기 위해 규칙을 약화하지 않으며, 실제 모바일 멀티플레이에서 반복 정지 문제가 사라지는 것을 완료 기준으로 삼는다.

> **최종 상태 (2026-08-24): v10 집중 교정 PASS.** 2.1~2.5 구현, Runtime/Editor C# 컴파일과 Unity self-validation을 통과했다. 같은 경기 `sharedSessionKey=30780b...ce11`의 Android Host·Editor Client에서 MOVE full EVIDENCE와 양쪽 local ROOT PASS를 확보했다. Host는 중심 checkpoint 766회·최대 오차 0, direct-safe Chase 806 frame·path Chase 2,977 frame을 기록했고 authority/adapter/stationary Walk/drop 오류는 모두 0이었다. CrossAudit의 최종 INCONCLUSIVE는 Host의 긴 ROOT `summary-END`가 Logcat에서 잘려 필수 회전 terminal 필드가 사라진 진단 형식 결함이며, 개별 증거와 게임 구현 실패가 아니다. 이 결함은 다음 코드 변경에서 compact terminal로 교정하고 동일 빌드만을 위한 재시험은 하지 않는다. 25종·양방향 역할교대·Legacy rollback은 Tracer C 이후 권위 전환 전 통합 회귀 게이트로 유지한다.

> **기존 로직 처리:** 중간 waypoint의 최근접 통과·평면 교차 소비와 무경로 direct Chase는 새 규칙과 직접 충돌한다. 최초 구현에서는 해당 분기를 명확히 비활성화하고 대체 경로를 한 서버 writer 안에 연결한다. 사용자 실기 통과 전에는 과거 동작의 의도와 제거 근거를 주석 및 이 작업 문서에 보존한다. 최종 삭제는 사용자 테스트 통과와 문서/메모리 업데이트 승인 후에만 진행한다. 별도의 두 번째 위치 writer나 경기 중 전환 가능한 혼합 모드는 만들지 않는다.

## 1. 적용 규칙

- `GameSystemRules_Units.md` 규칙 2: 기본 A*의 각 경유 중심을 실제로 통과하고 순서대로 완료한다.
- `U-MOV-ALIGN`: 중심 통과, corridor 안전, 위치 진행과 SimulationFacing 1° 일치를 동시에 보장한다.
- 규칙 4 및 `U-MOV-REPATH`: 동적 건물로 막히면 같은 frame 재시도 없이 현재 위치에서 유효한 우회 경로를 찾는다.
- 규칙 10: 전투 추격은 안전한 직선일 때만 direct를 사용하고, 아니면 공격 접근 위치까지 서버 경로로 우회한다.
- 규칙 11: 전투 이탈 후 안전하고 도달 가능한 전방 중심으로 걸어서 복귀하고 그 중심에서 A*를 다시 계산한다.
- `NET-FACING-001`: 모든 경로는 서버 단일 Authoritative Locomotion과 NetworkTransform 복제를 유지한다.

## 2. 구현 순서

### 2.1 회귀 신호를 먼저 교정

수정 파일:

- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

작업:

1. 중간 waypoint가 중심 허용 오차 밖에서는 소비되지 않는 fixture를 추가한다.
2. 60도 코너는 이동·회전 일치와 중심 통과를 모두 만족해야 PASS하도록 바꾼다.
3. 유닛과 적 사이에 이동 불가 건물이 있는 실제 Chase 형태의 fixture를 추가한다.
4. 중심 밖 전투 위치에서 안전한 전방 중심을 선택하고, 직선이 막히면 복귀 경로를 요구하는 fixture를 추가한다.
5. 기존 구현에서 새 fixture가 실패하는 것을 확인한 뒤 구현을 시작한다.

### 2.2 중심 보존 trajectory와 checkpoint

수정 파일:

- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitMovementContracts.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

작업:

1. 중간 waypoint의 완료 조건을 중심 허용 오차 도달로 변경한다.
2. 기본 A* 위치 trajectory에는 다음 선분 look-ahead를 섞지 않는다. 현재 중심을 실제 소비한 다음 outgoing 선분의 첫 frame부터 다음 방향으로 점진 회전한다.
3. 회전 한도와 corridor 안에서 중심을 연속 통과할 수 있는 후보를 우선한다.
4. 연속 통과가 안전하지 않으면 이동량 축소 후 `AlignToMove`를 사용한다. 중심 스냅이나 checkpoint 선소비는 사용하지 않는다.
5. `UnitPathCheckpointTracker`는 순서뿐 아니라 planner가 만든 중심 도달 증거만 소비하도록 호출 계약을 명확히 한다.

### 2.3 타겟 접근 경로

수정 후보 파일:

- `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- 필요 시 `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitMovementContracts.cs`

작업:

1. 현재 Root에서 타겟의 공격 접근 위치까지 직선 구간을 서버 공간 정책으로 검사한다.
2. 안전하면 기존 direct Chase 의도를 같은 writer에 전달한다.
3. 막혀 있으면 타겟의 현재 위치가 아니라 공격 사거리를 만족하는 이동 가능 접근 타일을 결정하고 서버 A* 경로를 사용한다.
4. 움직이는 타겟으로 경로를 갱신할 때 같은 objective·environment revision의 중복 명령과 같은 frame 재시도를 억제한다.
5. 공격 사거리에 들어온 candidate까지만 commit하고 `NoIntent`로 공격 단계에 넘기는 기존 우선순위를 유지한다.

### 2.4 안전한 RejoinPath

수정 파일:

- `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

작업:

1. 기존 `FindForwardClosestTile`의 “후보 없음 → nearestTile 반환”을 성공 결과로 사용하지 않는다.
2. 전방성, walkability, 현재 Root에서의 도달 가능성을 모두 만족하는 복귀 중심을 선택한다.
3. 안전한 직선이면 같은 Authoritative Locomotion으로 중심까지 이동한다.
4. 직선이 막히면 복귀 중심까지 A*를 사용하며, 유효한 후보가 없을 때만 동일 objective를 `Blocked`로 유지한다.
5. 중심 도달 이후 현재 권위 타일에서 성 목적지 A*를 새로 계산한다. 위치 스냅과 비인접 도메인 commit은 금지한다.

### 2.5 진단 갱신

수정 후보 파일:

- `Assets/_Project/Scripts/Infrastructure/Debug/UnitMovementAuthorityObserver.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

작업:

1. A* checkpoint가 중심 허용 오차 안에서 완료됐는지 bounded evidence로 기록한다.
2. Chase가 direct-safe 또는 target-approach-path 중 무엇을 선택했는지 기록한다.
3. 동일 타겟·동일 장애물에 대한 repeated recoverable repath를 최종 FAIL로 유지한다.
4. 기존 UTF-8 전체 줄 preflight, bounded 로그, terminal summary와 새 로그 체계를 그대로 사용한다.

## 3. 구현 중 보존 대상

- A1 행동 계약·reducer와 공격 회차 데이터
- A2 pure pose sample과 서버 권위 관측 seam
- B1 Simulation Root/Visual Root 및 50개 프리팹 구조
- B2 movement phase/scope, 10°/15° 히스테리시스와 25종 coverage 체계
- B3 match-fixed pipeline mode, prepare/publish/commit 원자성, 공간 preflight/commit, root/domain 분리, client identity/scope 단조성
- Legacy rollback은 경기 시작 시 고정되며 ReducerAuthoritative 경기 안에서 혼합하지 않는다.

## 4. 위험 요소와 대응

| 위험 | 대응 |
|---|---|
| 중심 통과 때문에 모든 60도 코너가 정지-회전-이동으로 퇴행 | incoming 선분은 중심까지 완료하고 outgoing 선분의 첫 frame부터 이동 중 점진 회전한다. fixture에서 중심 소비와 outgoing 비영 이동 선회를 연속된 두 선분으로 검증한다. |
| 움직이는 타겟 때문에 A*를 매 frame 재계산 | 타겟 접근 목적과 environment/target 위치 revision을 묶고, 의미 있는 접근 목표 변경에서만 새 경로를 요청한다. |
| 타겟 타일이 non-walkable이라 A* 목적지로 사용할 수 없음 | 타겟 자체가 아니라 공격 사거리를 만족하는 이동 가능 접근 타일을 목적지로 선택한다. |
| 복귀 과정에서 뒤로 돌아가거나 스냅 발생 | 전방성·도달 가능성을 후보 단계에서 검증하고 Root 실제 이동 후에만 공간 상태를 commit한다. |
| 진단 PASS지만 실제 호출 경로는 미검증 | 실제 `EnterCombatPursuitV3`와 같은 건물 차단 형태를 순수 seam에 고정하고 이후 Android Host/Client 역할교대로 검증한다. |
| 기존 A1~B2 회귀 | self-validation 전체를 다시 실행하고 B3 집중 실기 PASS 후에만 25종 회귀를 재개한다. |

## 5. 자동 검증 완료 조건

1. Unity C# 컴파일 오류 0건.
2. `RunUnitActionSelfValidation` 전체 PASS.
3. 중심 밖 checkpoint 소비, non-walkable direct Chase, unsafe Rejoin fixture가 각각 기존 구현에서는 FAIL하고 교정 후 PASS.
4. A1·A2·B1·B2 기존 self-validation 항목이 모두 계속 PASS.
5. 임시 진단 로그나 두 번째 Root writer가 남지 않음.

## 6. 사용자 실기 완료 조건

자동 검증 후 별도 사용자 테스트 단계에서 판정한다.

1. Android Host / Editor Client 한 경기와 역할교대 한 경기.
2. 유닛과 적 사이에 아군 또는 적 건물이 있는 상황에서 건물을 향해 제자리 걷기 하지 않고 가능한 우회로로 추격.
3. 전투 종료 후 뒤로 스냅하지 않고 안전한 전방 중심으로 복귀.
4. 일반 A*에서 각 타일 중심 경로를 유지하면서 정상 코너는 불필요하게 끊기지 않음.
5. terminal summary에서 writer 충돌, client Root write, stationary Walk, repeated recoverable repath가 0.
6. 집중 시나리오 PASS 뒤 기존 25종 coverage 회귀를 재개.

### 6.1 실기 결과

- [x] Unity `[UAS-DIAG]` 전체 self-validation PASS
- [x] Android Host MOVE `verdict=EVIDENCE`, 오류·drop·writer 충돌 0
- [x] 중심 checkpoint 766회, 최대 중심 오차 0
- [x] 건물 안전 Chase 분기 사용: direct-safe 806 frame / path 2,977 frame
- [x] Editor Client와 Android Host local ROOT 각각 stable endpoint 53/53, 회전 증거 53/53, local PASS
- [x] 사용자가 일반 이동·건물 생성 뒤 우회·전투 복귀를 실기 확인
- [ ] 공식 CrossAudit PASS — Host 긴 terminal 한 줄 절단으로 INCONCLUSIVE. 채점기 compact terminal 교정 후 다음 새 빌드의 통합 회귀에서 확인
- [ ] 25종·반대 역할 경기·Legacy rollback — Tracer C 이후 Sequence 권위 전환 전 통합 회귀로 이관

### 6.2 RootCrossAudit terminal 채점기 교정

- [x] periodic ROOT summary는 상세 진단 필드를 그대로 유지
- [x] `summary-END`는 CrossAudit가 실제 소비하는 role/flip/verdict/coverage/error/drop/endpoint/rotation 완전성 필드만 기록
- [x] production formatter 최악값 전체 줄 803 UTF-8 byte 확인
- [x] terminal 출력 직전 동일 full-line preflight를 적용하고 초과 시 잘린 END 대신 짧은 `terminal-preflight-failure`를 기록해 fail-closed
- [x] `[UAS-DIAG]`가 production compact formatter의 필수 필드와 최악값 길이를 reflection seam으로 검증
- [x] Runtime/Editor Roslyn 순차 컴파일 PASS. 기존 NGO obsolete·미사용 필드 경고만 유지
- [x] Unity `[UAS-DIAG]`와 RootCrossAudit SelfValidate 실제 메뉴 실행 PASS — compact ROOT terminal 계약, 세션 결합, 증거 완전성, 회전 분류와 fail-closed 회귀 포함

기존 v10 실기 파일의 잘린 END를 사후에 정상으로 추정 복구하지 않는다. 과거 파일은 INCONCLUSIVE 이력으로 보존하며, compact END는 다음 새 빌드부터 자동 적용한다.

**채점기 최종 판정 (2026-08-25): PASS.** Runtime/Editor Roslyn, production formatter 최악값 preflight, Unity `[UAS-DIAG]`, RootCrossAudit SelfValidate를 모두 통과했다. 채점기 자체를 위한 추가 빌드·실기 재시험은 필요하지 않으며 다음 Tracer C 빌드부터 compact END를 사용한다.

## 7. 이번 구현에서 제외

- 공격 방향과 타겟 방향의 일치 교정
- 공격 애니메이션과 서버 Impact·피해 적용 시점 교정
- 유닛 Collider 또는 footprint 기반 충돌
- 프리팹 재마이그레이션과 NetworkTransform 설정 변경
- 이동 속도·감지 거리·공격 거리 밸런스 변경
