# C1 Legacy 공격 타겟·방향 교정 Research

이 작업은 서버가 실제 피해를 예약하는 대상과 화면의 유닛이 바라보는 대상이 서로 달라지는 문제를 현재 멀티플레이 공격 경로에서 직접 고치는 조사다. 기존 Shadow 진단은 문제를 수치로 드러냈지만 게임 동작을 바꾸지 않았으므로, 이번에는 서버가 방향 정렬을 끝낸 뒤에만 같은 타겟으로 공격 애니메이션·쿨다운·피해 예약을 함께 시작하도록 경계를 바로잡는다.

## 1. 재현 결과

- 기존 멀티플레이 로그에서 Shadow Impact 1,567건 중 Legacy 피해 대상과 회차 대상 불일치가 442건이었다.
- Legacy 공격 예약 1,595건 중 220건은 5도 정렬 Commit 전에 시작됐다.
- 상세 로그 일부가 3,970건 제한에 걸렸지만 terminal 집계와 보존된 표본이 같은 두 현상을 확인했다.
- Host/Client Shadow 복제는 accepted 2,529건, rejected 0건으로 서버 상태 전달 자체는 정상 범주였다.
- B3 Root 교차 감사에서는 pose mismatch 0건이므로 이번 문제의 주원인은 NetworkTransform 복제가 아니라 공격 시작·타겟 생명주기 순서다.

## 2. 코드에서 확인한 원인

### 원인 A — 정렬 전에 공격 예약

`NetworkCombatController.OnUnitEnteredCombatHandler`는 사거리 진입 즉시 Attack 상태와 `StartCombatClientRpc`를 전송하고 `ExecuteAttack`을 호출한다. `ExecuteAttack`은 즉시 쿨다운을 소비하고 각 `HitFrameTimes`의 지연 피해를 예약한다. 반면 5도 정렬 판정은 읽기 전용 Shadow coordinator에서 나중에 평가되며 gameplay 시작을 막지 않는다.

### 원인 B — 예약된 회차 도중 표시 타겟 교체

쿨다운 중 기존 타겟이 무효가 되면 `_unitCombatTargets`와 `ChangeTargetClientRpc`가 즉시 새 타겟으로 바뀐다. 이미 예약된 지연 피해는 이전 타겟 ID를 계속 사용하므로, 화면과 Simulation Root는 새 타겟을 추적하면서 이전 타겟에 피해를 줄 수 있다.

### 원인 C — 점진 회전 뒤의 즉시 스냅 writer

`UnitView.BeginServerActionRotation`과 `StartCombatAnimation`은 Action 회전 소유권을 연 뒤 Root를 타겟 방향으로 즉시 덮어쓴다. 같은 클래스의 Update는 270도/초 점진 회전을 수행하므로 한 전투 진입에 서로 다른 속도 의미를 가진 두 Root writer가 존재한다.

### 원인 D — 피해 시점의 별도 도메인 Facing 갱신

`UnitCombatUseCase.ExecuteAttack`은 피해 직전에 타일 좌표로 6방향 Facing을 다시 계산한다. 이는 연속 월드 방향인 서버 Simulation Root와 별개 값이므로 타격 순간의 권위 AimDirection이 하나로 고정되지 않는다.

## 3. 수정 경계

- 현재 경기의 권위 writer는 계속 Legacy 하나만 사용한다.
- Shadow coordinator는 진단·복제만 담당하고 gameplay 분기 권위로 승격하지 않는다.
- 사거리 진입은 타겟 후보와 Action 회전 소유권만 연다. Attack 애니메이션, 쿨다운 소비와 피해 예약은 서버 pose가 정지 상태이고 조준 오차가 5도 이하인 같은 경계에서 시작한다.
- 커밋된 공격의 지연 타격이 끝나기 전에는 표시 타겟을 새 타겟으로 이전하지 않는다. 기존 타겟이 무효가 되면 기존 회차를 새 타겟에 넘기지 않고, 다음 공격 시작 경계에서 새 타겟을 선택한다.
- 멀티플레이 Root 즉시 스냅을 제거하고 기존 270도/초 서버 회전 writer만 유지한다. 아직 같은 pose gate를 사용하지 않는 싱글플레이의 기존 즉시 정렬은 이번 범위에서 보존한다.
- 멀티플레이 피해 적용 함수가 Root와 무관한 Facing을 다시 쓰지 않도록 한다. 싱글플레이의 기존 도메인 Facing 갱신은 별도 회귀를 막기 위해 보존한다.

## 4. 규칙 근거

- `GameSystemRules_Units.md`의 `U-TARGET-COMMIT`: 커밋된 회차 TargetId는 다른 타겟으로 이전하지 않는다.
- `U-ATK-ALIGN`: 서버 위치 이동이 멈추고 조준 오차가 5도 이하여야 Windup을 시작한다.
- `U-ATK-TIMELINE`: 정렬 완료·공격 시작·쿨다운 소비는 같은 서버 경계다.
- `GameSystemRules_UnitCombatSynchronization.md`의 `NET-FACING-002`: 타격 순간 SimulationFacing이 권위 방향이다.
- `NET-CANCEL-004`: 타겟 사망은 기존 회차를 다른 타겟으로 이전시키지 않는다.
- 전환 규칙 7의 single-writer 원칙: 이번 수정은 신규 Sequencer와 Legacy writer를 혼합하지 않고 현재 Legacy writer 내부 순서만 교정한다.

## 5. 범위 밖

- 신규 `SequenceAuthoritative` 경기 모드 활성화
- 전체 Projectile/TravelingArea 전달 방식 교체
- 결과 키 기반 VFX·HP 표현 전환
- 공격 에셋·클립·피해 수치 변경

이 항목들은 후속 권위 전환 단계다. 이번 완료 판정은 현재 Legacy 경기에서 타겟·방향·공격 시작 경계가 일치하는지에 한정한다.

## 6. 1차 멀티플레이 실기 결과와 추가 원인 (2026-08-26)

Editor Host와 Android Client가 같은 `sharedSessionKey=3aa730...643ec`로 연결된 경기에서 C1 v2를 검증했다. 시작 정렬 교정은 `legacyBeforeAligned=0`, production gate `2,147`회 중 Ready `695`, Deferred `1,452`, Invalid `0`으로 동작했다. Client Shadow 복제도 accepted `1,758`, rejected `0`이었다.

그러나 Host terminal은 `targetMismatches=31`, `dropped=733`으로 FAIL했다. 보존된 불일치 상세 5건은 모두 Legacy 피해 TargetId와 Display TargetId가 같고 Shadow TargetId만 이전 타겟이었다. 이는 실제 Legacy writer가 새 타겟으로 공격을 시작할 때 Shadow reducer의 이전 Recovery/Target scope가 남아 있었고, `ScheduleLegacyAttack`이 타겟 일치를 검증하지 않은 채 그 sequence를 예약에 연결했기 때문이다. 상세 로그 상한 뒤 26건의 형태는 보존되지 않았으므로 전체를 실제 화면 정상으로 확대 판정하지 않는다.

`dropped=733`은 Logcat 저장 실패가 아니라 observer 자체 상한이다. 정렬 중 5도 밖에 있는 정상 `AlignToAttack` 표본을 coordinator가 `InvalidInput`으로 반환하고 observer가 `shadow-intent-rejected` 오류로 반복 출력해 일반 로그 예산을 소진했다. 정상 Deferred와 계약 위반을 강타입으로 분리해야 한다.

같은 경기의 B3 `adapterFailures=1`은 게임 종료 감지 후 약 38ms 뒤 `post-combat-no-safe-forward-center`가 기록된 사례다. 경기 중 공간 실패와 종료 뒤 남은 코루틴의 관측을 분리하지 않은 생명주기 결함이며 C1 타겟 실패와는 독립적이다.

따라서 1차 실기 판정은 **C1 FAIL / 교정 필요**다. 5도 시작 gate는 유지하고, 실제 Legacy 공격 시작 경계가 새 Shadow sequence와 같은 TargetId를 원자적으로 확정하도록 상관관계를 고쳐야 한다. 채점 기준 완화나 mismatch 숨김으로 통과시키지 않는다.
