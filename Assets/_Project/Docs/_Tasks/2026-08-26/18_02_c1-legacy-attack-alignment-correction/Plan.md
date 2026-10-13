# C1 Legacy 공격 타겟·방향 교정 Plan

이 계획은 유닛이 타겟을 발견하자마자 공격부터 시작하던 순서를 바꿔, 서버가 먼저 정지·방향 정렬을 확인하고 그 뒤 한 번만 공격을 시작하도록 만든다. 공격 중 타겟이 사라져도 이미 예약된 공격을 다른 타겟의 화면 표현과 섞지 않으며, 서버 권위와 현재 아키텍처는 그대로 유지한다.

> **기존 로직 처리:** Legacy 피해·특수 공격·RPC 경로를 삭제하지 않는다. 신규 Sequence 권위도 활성화하지 않는다. 기존 즉시 공격 진입과 Root 스냅만 같은 Legacy writer 안에서 비활성화하고, 사용자 멀티플레이 검증 전에는 되돌릴 수 있는 구조로 보존한다.

## 1. 진단기 보강

수정 파일:

- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`

구현:

1. 실제 production 호출 순서와 같은 fixture에서 5.001도에는 공격 시작이 거부되고 5.000도에 정확히 한 번 허용되는지 검증한다. 근거: `U-ATK-ALIGN`, `U-ATK-TIMELINE`.
2. 정렬 대기 중 타겟 변경은 기존 후보를 취소할 수 있지만, 공격이 시작된 뒤 새 타겟은 다음 시작 경계까지 보류되는지 검증한다. 근거: `U-TARGET-COMMIT`, `NET-CANCEL-004`.
3. Legacy 예약 시 Shadow sequence가 0인 경우와 예약 target이 현재 회차 target과 다른 경우를 별도 terminal 카운터로 유지한다. 근거: `NET-ACTION-SEQ`, `NET-FACING-002`.
4. 로그 제한에 도달해도 terminal 필수 집계가 사라지지 않도록 bounded evidence와 전체 카운터를 분리한다.

## 2. 순수 공격 시작 게이트

추가 파일:

- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackStartGate.cs`

구현:

1. Unity/NGO에 의존하지 않는 값 판정으로 pose 유효성, 타겟 생존·유효성, 두 표본 사이 정지, 5도 진입 경계를 평가한다.
2. 실패 이유를 `InvalidPose`, `InvalidTarget`, `Moving`, `Misaligned`로 구분해 진단기가 실제 거부 이유를 검증할 수 있게 한다.
3. 첫 표본은 정지를 증명하지 못하므로 공격을 시작하지 않고 다음 서버 표본을 기다린다.

근거: `U-COMBAT-PHASE`, `U-ATK-ALIGN`, `NET-FACING-002`.

## 3. 서버 Legacy 공격 시작 순서 교정

수정 파일:

- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`

구현:

1. 사거리 진입 이벤트에서는 타겟 후보만 등록하고 애니메이션을 Held로 둔다. 즉시 `ExecuteAttack`하지 않는다.
2. 서버 Tick에서 쿨다운이 0일 때 같은 타겟의 pose를 연속 두 번 관측하고, 정지+5도 정렬을 통과한 경우에만 Attack 상태·Start/Change RPC·쿨다운 소비·피해 예약을 같은 경계에서 수행한다.
3. 공격 예약 이후 쿨다운 중에는 표시 타겟을 교체하지 않는다. 기존 타겟이 무효면 다음 회차 준비 상태로 닫고 새 타겟을 별도 시작으로 처리한다.
4. StopCombat·사망·Despawn에서 정렬 표본과 보류 상태를 함께 제거한다.
5. 기존 Shadow 관측은 동일 타겟과 같은 경계에서 계속 실행해 교정 전후 수치를 비교한다.

근거: `U-TARGET-COMMIT`, `U-ATK-ALIGN`, `U-ATK-TIMELINE`, `NET-CANCEL-003~004`.

## 4. Simulation Root 단일 회전 writer

수정 파일:

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

구현:

1. 멀티플레이 `BeginServerActionRotation`과 `StartCombatAnimation`의 즉시 Root 스냅을 제거한다. 싱글플레이는 동일 gate 도입 전까지 기존 즉시 정렬을 보존한다.
2. 기존 서버 Update의 270도/초 `RotateTowards`만 공격 중 Simulation Root를 쓴다.
3. 순수 클라이언트는 계속 NetworkTransform만 Root를 쓴다.

근거: `U-ATK-ALIGN`, `NET-FACING-002`, single-writer 원칙.

## 5. 피해 경계의 별도 Facing writer 제거

수정 파일:

- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`

구현:

1. 멀티플레이 피해 적용 직전 타일 좌표 기반 `UnitData.Facing` 재계산을 제거한다. 싱글플레이 호출은 기존 갱신을 보존한다.
2. 특수 공격의 실제 전방 판정은 이미 공격자·주 타겟 월드 벡터를 사용하므로 의미를 보존한다.
3. 이동/공격 시작 경계에서 확정된 서버 Simulation Root가 방향의 단일 원본이 되도록 한다.

근거: `NET-FACING-002`.

## 6. 자동 검증 완료 조건

1. Unity C# 컴파일 오류 0건.
2. 기존 A1~B3와 C1 self-validation 전체 PASS.
3. 5.000/5.001도, 첫 표본, 이동 중 표본, 정렬 후 단일 시작, 정렬 대기 중 타겟 변경 fixture PASS.
4. `ExecuteAttack` 호출 경로가 정렬 게이트 통과 뒤로 제한되고 전투 진입 즉시 호출이 0건이다.
5. 멀티플레이 공격 Root 즉시 스냅 writer가 0건이며 서버 점진 writer와 클라이언트 NetworkTransform 책임이 유지된다.
6. 신규 Sequence 권위나 유닛별 혼합 모드가 활성화되지 않는다.

실기 검증은 구현 후 사용자가 Editor/Android 역할을 교대해 수행한다. 이 계획에서는 별도 Testcase 문서나 QA 실행을 생성하지 않는다.

## 7. 구현 및 자동 검증 결과 (2026-08-26)

- `origin/main` 병합 뒤 양측의 로그 체계·B3 이동 코드·C1 Shadow 코드를 함께 보존했다. 병합 커밋은 `ada617db`다.
- `UnitAttackStartGate`를 추가하고 멀티플레이 Legacy 시작을 연속 정지 표본과 조준 오차 5도 이하가 확인된 서버 Tick으로 옮겼다.
- 공격 예약 뒤에는 타겟 표시를 같은 회차에 고정하고, 멀티플레이 Root 즉시 스냅과 피해 시점의 별도 `UnitData.Facing` writer를 제거했다. 싱글플레이 Legacy 동작은 보존했다.
- Shadow observer를 v2로 보강해 production gate 표본과 정렬 전 Legacy 시작을 terminal에서 fail-closed 집계하고, 정상 상세 로그는 제한해 Android 로그 유실 위험을 줄였다.
- Unity 실제 컴파일 오류 0건, `Run Unit Action Self Validation` PASS. PASS에는 `C1 production stationary/5-degree attack-start gate`, `exhaustive 25-type attack manifest`, `separate LegacyAttackToken correlation`, `target lock/multi-hit lifecycle`가 포함됐다.
- `Tools/check_docs.py`는 0건 PASS다. Android/Editor 역할교대 멀티플레이 실기는 아직 수행하지 않았으므로 C1 전체 완료 판정은 보류한다.

## 8. 1차 실기 FAIL 교정 계획 (2026-08-26)

> **기존 로직 처리:** Legacy 서버 권위·피해·특수 공격·RPC·VFX writer는 계속 유지한다. Shadow를 gameplay writer로 승격하지 않으며, 타겟 불일치를 숨기거나 terminal 기준을 완화하지 않는다. 이번 교정은 같은 Legacy 공격 시작 경계에서 read-only Shadow 상관관계를 정확히 준비하는 작업이다.

### 8.1 stale Shadow target 재현 fixture

`RunUnitActionSelfValidation`에 타겟 A의 이전 회차가 남은 상태에서 production Legacy가 타겟 B로 시작하는 실제 로그 형태를 추가한다. 새 예약이 A의 sequence를 빌리거나 TargetId가 다르면 FAIL하고, 같은 서버 시각에 B의 새 sequence를 발급한 경우만 PASS한다. 같은 타겟 연속 공격, 타겟 사망 뒤 변경, 다중 타격 완료 뒤 다음 회차도 함께 검증한다.

근거: `U-TARGET-COMMIT`, `U-ATK-TIMELINE`, `NET-ACTION-SEQ`, `NET-CANCEL-004`.

### 8.2 production 시작 경계의 원자적 Shadow 상관

정지·5도 production gate가 Ready인 서버 Tick에서 Shadow coordinator가 이전 회차를 현재 시각까지 진행하고, 완료 가능한 회차만 종료한 뒤 실제 Legacy TargetId로 새 Align/Commit을 준비한다. `ScheduleLegacyAttack`은 현재 snapshot이 유효한 sequence이고 예약 TargetId와 정확히 같을 때만 correlated token을 만든다. 다른 타겟의 stale sequence를 조용히 재사용하지 않는다.

게임플레이 시작 여부는 계속 Legacy gate가 결정한다. Shadow 준비 실패는 진단 FAIL로 남기되 Legacy 피해 writer를 새 Sequencer로 바꾸지 않는다.

근거: `U-ATK-ALIGN`, `U-ATK-TIMELINE`, `NET-ACTION-SEQ`, 전환 규칙 1·7.

### 8.3 정상 Deferred와 증거 유실 분리

첫 정지 표본·이동 중·5도 밖 정렬 진행은 정상 Deferred다. 이를 오류 상세로 반복 출력하지 않고 상태별/타입별 최초 표본과 전체 카운터만 남긴다. `dropped`는 필수 mismatch·invalid·terminal 증거가 실제 상한 때문에 유실된 경우에만 증가시키고, 의도적으로 생략한 정상 반복은 별도 suppressed counter로 집계한다.

근거: `U-ATK-ALIGN`, `NET-TIME-001`, Android-safe bounded evidence 계약.

### 8.4 게임 종료 뒤 B3 관측 종료

서버가 `OnGameEnd`를 받으면 Movement authority observer에도 gameplay 종료를 알린다. 이후 남은 이동 코루틴의 adapter failure는 경기 결과 FAIL에 포함하지 않는다. 종료 전 실패는 기존처럼 fail-closed를 유지하며 종료 이벤트가 오류를 지우는 용도로 사용되지 않도록 self-validation 경계를 추가한다.

근거: 서버 행동 생명주기와 `NET-CANCEL-005`.

### 8.5 완료 조건

1. stale A → 새 B 회차 fixture에서 B의 새 sequence와 Legacy token이 동일 경계·동일 TargetId로 연결된다.
2. `ScheduleLegacyAttack`은 sequence 0 또는 target mismatch를 correlated 예약으로 승인하지 않는다.
3. 정상 Misaligned/Moving 반복은 `dropped`와 local FAIL을 만들지 않는다.
4. 실제 mismatch·invalid·preflight 유실은 계속 terminal FAIL이다.
5. GameEnd 이전 B3 adapter failure는 FAIL, GameEnd 이후 표본은 종료 후 억제로 분류된다.
6. 기존 A1~B3와 C1 self-validation 전체 및 Unity 컴파일이 PASS한다.
7. 다음 Android/Editor 역할교대 실기에서 `legacyBeforeAligned=0`, `targetMismatches=0`, `dropped=0`, client rejected 0을 확인하기 전 C1을 완료로 승격하지 않는다.

## 9. 2차 교정 구현 및 자동 검증 결과 (2026-08-26)

1. `UnitAttackShadowCoordinator`는 공격자당 하나의 진행 회차를 덮어쓰는 방식 대신 실제 Legacy 공격 시작마다 독립 Shadow 회차를 만든다. 각 `LegacyAttackToken` 예약이 자기 `UnitActionSequencer`를 Impact 종료까지 소유하므로 이전 회차의 늦은 타격이 다음 타겟 회차로 섞이지 않는다.
2. production 정지·5도 gate가 통과시킨 바로 그 pose와 타겟 생존·유효 값을 `ExecuteAttack`에 전달한다. 후보 Shadow 회차는 같은 서버 경계에서 Align→Commit을 완료한 뒤에만 최신 복제 scope가 된다. sequence 0, 다른 TargetId, Commit 실패는 예약하지 않고 `correlationFailures`로 fail-closed한다.
3. 일반 Tick의 Shadow intent는 정렬 상태만 관측하고 sequence를 선발급하지 않는다. 정상 정렬 대기는 `expectedDeferred`로 집계하고 오류 상세를 반복 출력하지 않는다. observer schema는 `c1-attack-target-facing-shadow-v3`다.
4. 지원되지 않은 10종 및 힐러 1종은 기존 manifest 분류를 유지하며 production correlation FAIL로 오인하지 않는다. 서버 권위 Legacy 피해·HP·RPC·VFX writer는 변경하지 않았다.
5. GameEnd 뒤 들어온 B3 adapter callback은 `postGameAdapterFailuresSuppressed`로 분리한다. 경기 종료 전 adapter failure는 기존처럼 FAIL이다.
6. Runtime/Editor Roslyn 컴파일은 오류 0건이고 Unity `Run Unit Action Self Validation`은 PASS했다. 새 fixture는 겹친 두 Legacy 회차가 서로 다른 TargetId·SequenceId를 유지하고, 이전 회차의 늦은 Impact는 평가하되 최신 회차만 복제하는 것을 검증한다.
7. 전체 완료는 아직 아니다. 새 Android Development Build와 Editor counterpart의 같은 경기에서 `observerSchema=c1-attack-target-facing-shadow-v3`, `legacyBeforeAligned=0`, `correlationFailures=0`, `targetMismatches=0`, `dropped=0`, client rejected 0을 확인해야 한다.
