# QA-Fix 반복 로그 — C1 공격 연속 표현·회차 결속 교정

## Round 1 — 2026-08-27 12:20

### [QA] 발견된 문제

- BUG-C1-SEQ-001: 멀티플레이 Client가 정상적인 다음 공격 회차 상태 5건을 `SequenceRegression`으로 거부했다.
  - 관련 파일: `Assets/_Project/Docs/_Logs/2026-08-27/11_55_logcat/RuntimeLog_device.txt:14602`, `:34195`, `:34331`, `:34617`, `:35000`
  - terminal: `Assets/_Project/Docs/_Logs/2026-08-27/11_55_logcat/RuntimeLog_device.txt:96713`
  - 원인 분석: 새 공격 회차마다 reducer revision이 다시 시작하지만 서버 게시층과 Client classifier가 revision을 공격자 수명 전체의 전역 단조값처럼 비교했다. 직전 회차 Impact revision 4 뒤 다음 회차 commit revision 3이 서버에서 누락되고, 이어진 다음 회차 Impact revision 4가 Client에서 같은 revision의 다른 비-neutral 상태로 거부됐다.
  - 규칙 근거: `GameSystemRules_UnitCombatSynchronization.md`의 `NET-ACTION-SEQ`, `NET-ACTION-IDEMPOTENT`, `NET-CANCEL-005`.

- BUG-C1-LIFE-002: Host SpearMan unit 116의 실제 Legacy 피해는 적용됐지만 같은 회차 Shadow Impact가 `InvalidPhase`로 거부됐다.
  - 관련 파일: `Assets/_Project/Docs/_Logs/_editor/2026-08-27/RuntimeLog.txt:2059`
  - terminal: `Assets/_Project/Docs/_Logs/_editor/2026-08-27/RuntimeLog.txt:2240`
  - 원인 분석: 표시 전투 타겟이 제거될 때 `StopCombat`이 이미 커밋된 예약 Sequencer를 먼저 취소했다. 그러나 Legacy 예약 코루틴은 Impact 순간의 실제 대상 조건을 다시 확인해 `Applied`를 반환했다. 표시/다음 후보 생명주기가 커밋 회차의 TargetId·Impact 생명주기를 침범했다.
  - 규칙 근거: `GameSystemRules_Units.md`의 `U-TARGET-COMMIT`, `U-IMPACT-TARGETLOCKED`; `GameSystemRules_UnitCombatSynchronization.md`의 `NET-CANCEL-002~005`.

### [DEV] 수정 내용

- `UnitAttackShadowReplicationClassifier`를 공격자 수명 전체 revision 비교에서 `(AttackSequenceId, 회차 내부 Revision)` 비교로 교정했다. 새 양수 sequence는 revision 초기화를 허용하고, 같은 sequence의 stale/duplicate와 작은 sequence의 지연 상태는 계속 fail-closed한다.
- `NetworkUnit.PublishAttackShadowSnapshot`도 Client와 동일한 순수 classifier를 사용하게 통합하고 NetworkObject spawn/despawn마다 classifier 수명을 초기화·retire한다.
- Client 거부 로그에 직전 수락 sequence/revision/phase와 incoming 값을 함께 기록하도록 보강했다.
- `UnitAttackShadowCoordinator.StopCombat`은 현재 Sequencer가 소유한 Legacy Impact 예약이 남아 있으면 커밋 회차를 취소하지 않는다. 공격자 사망·despawn의 `Retire`는 기존처럼 예약까지 제거한다.
- 회귀 fixture에 `StopCombat → 예약 Impact`, `sequence 1/revision 4 → neutral → sequence 2/revision 3/4 → late sequence 1`을 추가했다.
- 서버 권위와 Legacy 피해 writer는 유지하며 Shadow는 `gameplayWrites=0`을 계속 보장한다.

### [DEV] 자체 검증

- 구현 전 RED: `[UAS-DIAG] self-validation FAIL: Display StopCombat must not cancel a committed cycle while a Legacy Impact reservation is pending.`
- 구현 후 GREEN: `Hexiege/Combat/Run Unit Action Self Validation` PASS.
- PASS 계약에 `pending-reservation StopCombat isolation`, `sequence-first per-cycle revision reset classification shared by server/client`가 포함됨을 확인했다.
- `python -X utf8 Tools/check_docs.py`: 문제 없음.
- Unity `File > Build And Run`: `Build Finished, Result: Success` / SM-N971N 설치·실행 완료.
- 실행 직후 기기 PID 로그에서 `FATAL EXCEPTION`, `AndroidRuntime`, 관리 예외와 `[ERROR]`가 없고 자동 로그인 및 Tap-to-Start 대기까지 도달함을 확인했다.

### [QA] 다음 실기 확인

- 같은 경기에서 Host terminal `targetMismatches=0`, Client terminal `clientRejected=0`인지 확인한다.
- 사거리 안에서 기존 타겟 사망 직후 새 타겟으로 전환할 때 Attack 표현이 유지되고, 회전 중 Walk/Held가 끼어들지 않는지 확인한다.
- 단일 타격 유닛과 다중 타격 유닛을 각각 포함하여 새 회차와 이전 예약의 TargetId·HitIndex가 섞이지 않는지 확인한다.

## Round 2 — 2026-08-27 16:52

### [QA] 최신 실기 판정

- 같은 경기 `sharedSessionKey=8a03985f...d27`에서 Android Client는 `clientAccepted=4863`, `clientRejected=0`, `verdict=EVIDENCE`였다.
- Editor Host의 유일한 C1 실패는 unit 14, sequence 5의 `targetMismatches=1`이었다. 그러나 예약 Legacy/Shadow TargetId는 모두 15, reducer는 `Accepted`, Shadow는 `AuthorizedHit`, Legacy는 `Applied`였고 실제 불일치는 없었다. `displayTargetBeforeApply=-1`만 달랐다.
- 따라서 구현 실패가 아니라 다음 후보용 표시 타겟이 Impact 전에 해제될 수 있다는 규칙을 진단기가 위반한 false positive로 확정했다.

### [DEV] 진단기 v5 교정

- dispatch 판정을 순수 seam으로 분리하고 표시 타겟을 진단 문맥으로만 유지했다.
- 예약 Legacy/Shadow TargetId, 양수 SequenceId, reducer `Accepted`, `AuthorizedHit ↔ Applied` 또는 `AuthorizedMiss ↔ 미적용` 결과가 모두 일치하면 정상으로 판정한다.
- 타겟·회차·phase·결과 불일치는 계속 fail-closed한다.
- 변경된 terminal 의미가 v4 로그와 섞이지 않도록 schema를 `c1-attack-target-facing-shadow-v5`로 올렸다.

### [DEV] 자체 검증

- RED: `A committed reservation with matching Shadow/Legacy target and result must remain healthy after the display target is cleared.`
- GREEN: Unity `Hexiege/Combat/Run Unit Action Self Validation` PASS. PASS 계약에 `reservation-bound dispatch consistency independent of display-target lifecycle`가 포함됐다.
- 이 교정은 Shadow observer만 변경했으며 서버 권위 Legacy 피해·HP·RPC·VFX writer와 게임 동작은 변경하지 않았다.
