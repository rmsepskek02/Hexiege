# C1 공격 연속 표현·회차 결속 교정 Research

이 작업은 공격하던 유닛이 새 적으로 타겟을 바꿀 때 걷기 자세가 잠깐 끼어드는 현상을 없애고, 공격 동작을 유지한 채 새 타겟을 향해 자연스럽게 회전하도록 고치는 조사다. 화면의 자연스러움만 복원하는 것이 아니라, 새 타겟에 대한 서버 공격 회차와 타격 표현이 정렬 완료 전 섞이지 않도록 함께 바로잡는다. 신규 애니메이션 에셋은 만들지 않고 기존 Attack 클립과 서버 권위 구조를 유지한다.

이 문서는 `2026-08-26/18_02_c1-legacy-attack-alignment-correction`의 후속 교정이다. 앞선 C1의 정지·5도 시작 게이트와 Legacy 서버 피해 writer는 유지하며, 실기에서 드러난 표현 회귀와 진단 계약 결함을 추가로 다룬다.

## 1. 사용자 실기 증상

- 현재 공격 타겟이 사망하거나 공격 사거리에서 벗어난 뒤, 공격 사거리 안의 다른 적으로 타겟이 교체된다.
- 타겟 교체 순간 Attack이 아닌 다른 애니메이션이 짧게 재생된 뒤 Attack으로 돌아온다.
- 기존 구현에서는 타겟을 바꿔도 Attack 루프를 유지하며 방향만 바꿨고, 사용자가 확인한 시각적 부자연스러움은 없었다.
- 목표 표현은 공격 가능한 적이 사거리 안에 계속 존재하는 동안 Attack 상태를 유지하고, 새 타겟 방향으로 서버 Root가 회전하는 형태다.

## 2. 재현 가능한 직접 원인

`NetworkCombatController.TickCombat`은 기존 타겟이 무효이고 새 타겟을 선택하면 다음 순서로 처리한다.

1. 새 타겟을 `_unitCombatTargets`에 기록한다.
2. 정렬 표본과 `_combatAnimationSent`를 초기화한다.
3. `UnitAnimState.Held`를 발행한다.
4. 새 타겟 변경 RPC를 보낸다.
5. 정지·5도 게이트가 Ready가 되면 `UnitAnimState.Attack`과 공격 시작 RPC를 다시 보낸다.

`UnitView.HoldMovementAnimation`의 Held 표현은 전투 대기 자세가 아니다. 모든 프리팹에 존재한다는 이유로 Walk 클립의 첫 프레임을 재생한 뒤 Animator 속도를 0으로 만든다. 따라서 실제 화면 전이는 `Attack → Walk 첫 프레임 정지 → Attack`이다. 이 코드는 사용자가 본 증상을 결정적으로 재현한다.

반면 `UnitView.ChangeTarget`은 원래 “회전만 업데이트하고 애니메이션 상태는 변경하지 않는다”는 계약을 갖고 있다. 이번 회귀는 이 책임 분리 위에 NetworkCombatController가 Held 상태 전환을 추가하면서 생겼다.

## 3. 최신 멀티플레이 로그 분석

동일 `sharedSessionKey=69661e...cff71` 경기에서 Editor는 Host, Android 기기는 Client였다. 역할과 경기 연결을 확인한 뒤 각 발화 주체의 terminal만 분리해 판정했다.

### 정상 범위

- Host/Client Root pose observer: 모두 PASS, 위치·회전 구조 오류 0건.
- Host movement authority: rejected·invalid·writer conflict·stationary walk violation 모두 0건.
- C1 production gate: `legacyBeforeAligned=0`, `correlationFailures=0`, `productionGateInvalid=0`.
- 별도 런타임 예외나 C1 이외 ERROR는 발견되지 않았다.

### 실패 범위

- Host C1 v3: `targetMismatches=1`, `dropped=1655`, verdict FAIL.
- Client C1 v3: `clientAccepted=709`, `clientRejected=316`, 모두 `SequenceRegression`, verdict FAIL.
- Host mismatch 표본은 Pistoleer unit 15의 회차 4에서 Legacy/Shadow Target 12와 관측된 Display Target 11이 달랐다.

이 mismatch는 실제 위험을 보여 주지만 현재 observer가 Legacy 피해 적용 뒤 `_unitCombatTargets`를 읽는다. 피해가 타겟 사망과 동기 이벤트를 발생시킨 뒤 표시 타겟을 관측할 수 있으므로, “Impact 직전부터 화면이 11이었다”와 “Impact 처리 중 다음 타겟 11로 바뀌었다”를 구분할 수 없다. 교정 후에는 피해 적용 전의 회차 표시 타겟과 적용 후의 다음 후보 타겟을 분리해 기록해야 한다.

`dropped=1655`는 Android 저장 실패가 아니다. 정상 `shadow-commit`을 공격 회차마다 일반 로그로 출력하여 observer의 정상 로그 예산을 소진한 결과다. terminal 예약 줄은 남았지만 필수 mismatch 상세가 뒤에서 발생하면 유실될 수 있으므로 진단 계약 결함이다.

Client `SequenceRegression` 316건은 기존에 수락한 양수 Sequence 뒤 `SequenceId=0`인 Align/neutral snapshot이 같은 revision 의미로 도착할 때 classifier가 회차 역행으로 분류한 형태다. 서버 게시 정책과 클라이언트 단조 분류기가 같은 neutral 전이 계약을 사용하도록 맞춰야 하며, 판정 기준을 완화해 숨기지 않는다.

## 4. 규칙 충돌과 확정할 표현 의미

현재 `U-ATK-TIMELINE`은 정렬 중 공격 애니메이션을 먼저 시작하지 않는다고 적혀 있다. 이를 “Animator가 Attack 상태에 있으면 안 된다”로 해석하면 사용자가 승인한 연속 표현과 충돌한다. 다음 두 의미를 분리해야 한다.

- **Attack 표현 상태 유지:** 이미 공격 중이던 유닛이 사거리 안의 새 유효 후보로 전환할 때 기존 Attack 표현을 계속 재생하며 회전할 수 있다.
- **새 공격 회차 시작:** 새 타겟의 Windup·Impact·VFX·SFX·피해 예약은 서버가 정지와 5도 정렬을 확인한 뒤에만 시작한다.

타겟 교체 자체는 애니메이션 상태 전이를 만들지 않는다. 기존 회차의 TargetId나 Impact를 새 타겟으로 이전하지 않으며, 새 타겟의 실제 공격은 새 AttackSequenceId에서 시작한다.

## 5. 완성도 우선 설계 결론

1. 공격 사거리 안에 유효한 공격 후보가 계속 있으면 Attack 표현 상태를 유지한다.
2. 타겟 교체 중에도 서버 Simulation Root의 기존 270도/초 단일 writer가 새 타겟으로 회전한다.
3. 타겟 교체 경계에서 Held·Walk·Idle을 끼워 넣지 않는다.
4. 정렬 완료 전 로컬 Animation Event가 새 회차 Impact, VFX, SFX 또는 피격 표현을 승인하지 못하게 한다.
5. 새 회차가 커밋되면 같은 Attack 상태 안에서 서버 시작 경계와 표현 회차를 한 번만 결속한다. 중복·오래된 RPC나 snapshot은 재시작시키지 않는다.
6. 공격 가능한 적이 없어지거나 새 후보가 공격 사거리 밖이라 Chase/A* 이동이 필요할 때만 Attack 표현 상태를 종료한다.

신규 CombatReady/Turn 애니메이션은 만들지 않는다. Root 회전과 기존 Attack 클립으로 먼저 25종을 검증하며, 이번 작업은 에셋 제작이나 프리팹 애니메이션 교체를 포함하지 않는다.

## 6. 추가로 교정해야 하는 범위

### 이번 구현에 포함

- 타겟 교체 시 Held 발행 제거와 Attack 표현 연속성 정책.
- 새 타겟 정렬 전/커밋 후 표현 이벤트의 명시적 게이트.
- 같은 Attack 상태에서 새 회차를 중복 없이 결속하는 revision/sequence 전이.
- 클라이언트 neutral snapshot의 올바른 단조 분류와 수명 종료/ID 재사용 보존.
- observer 정상 commit 로그 억제, 필수 실패 상세과 terminal 예산 분리.
- Legacy 피해 적용 전 Display Target 표본과 적용 후 다음 후보 표본 분리.
- 최초 진입의 Walk→Attack, 타겟 교체의 Attack→Attack, 전투 이탈의 Attack→Walk를 구분하는 순수 정책 검증.

### 전달 방식별 후속 교정으로 분리

문서의 `U-IMPACT-TARGETLOCKED`는 근접·Hitscan Impact마다 생존·사거리·8도 방향을 재검증하도록 정하지만, `UnitCombatUseCase.ApplyAttackDamageCore`는 공격 시작 후 사거리 이탈에도 피해를 적용한다고 명시한다. 이는 실제 규칙/코드 충돌이다.

그러나 ProjectileImpact·TravelingArea는 발사 뒤 타겟이 사거리에서 벗어나도 별도 권위 생명주기로 진행할 수 있다. 아직 25종 중 다수의 전달 방식이 Provisional/MigrationRequired이므로 모든 Legacy 피해에 사거리 재검증을 일괄 추가하면 발사체·파도 의미를 파괴한다. 이번 표현 교정에서 blanket 변경하지 않고, AttackProfile 전달 방식이 확정된 TargetLocked 경로부터 별도 계획으로 수정한다.

### 기존 미완성 에셋 위험

QuakeSpirit, RhinoBreaker, MushroomBomber, BloomFairy의 Animation Event/Attack state 연결 문제와 여러 Projectile 계열의 미완성 서버 전달체는 이번 타겟 교체 회귀의 원인이 아니다. 기존 `UnitCombatAssetMatrix` 상태를 유지하며 C1 표현 PASS를 25종 공격 전체 완료로 오인하지 않는다.

## 7. 수정하지 않을 것

- Legacy 서버 피해 writer와 HP 권위.
- 공격력·사거리·회전 속도·쿨다운 수치.
- 이동 B3 trajectory와 NetworkTransform 설정.
- 신규 애니메이션·VFX·SFX·프리팹 제작.
- 싱글플레이의 별도 Legacy 전투 구조를 근거 없이 멀티플레이와 동시에 교체하는 작업.

## 8. 2026-08-27 11:51 C1 v4 멀티플레이 FAIL 재진단

새 Development Build를 Editor Host + Android Client로 실행한 동일 경기
`sharedSessionKey=d66ec5a475349d9e4b39f5205211ba74bba8dee1e87b1cfa7bd0f976f623a3cf`를
역할별로 분리해 확인했다. 이번 판정은 이전 v3 로그가 아니라 양측 모두
현재 판정에는 표시 타겟 수명주기를 예약 Impact 정합성과 분리한 `observerSchema=c1-attack-target-facing-shadow-v5` terminal만 사용한다. v4 이하는 이력 분석용이며 v5와 혼합 채점하지 않는다.

### 확인된 정상 범위

- Host는 `commits=2015`, `legacySchedules=2015`, `legacyBeforeAligned=0`,
  `correlationFailures=0`, `productionGateInvalid=0`을 기록했다.
- observer 필수 증거는 `dropped=0`, `terminalPreflightFailures=0`,
  `manifestFailures=0`이며 v3의 정상 로그 예산 고갈은 재발하지 않았다.
- Shadow는 `gameplayWrites=0`을 유지하여 Legacy 서버 피해와 HP 권위를 변경하지 않았다.
- 같은 경기 Root CrossAudit는 37개 유닛 pose를 결합해 위치·회전 mismatch 0으로 PASS했다.

### BUG-C1-SEQ-001 — 회차가 바뀔 때 revision 기준이 잘못 유지됨

Android Client는 상태 1,102건을 수락했지만 5건을 `SequenceRegression`으로 거부했다.
거부 입력은 새 `sequenceId`의 `revision=4`였다. 원인은 각 공격 회차가 독립
`UnitActionSequencer`로 생성되어 revision이 회차마다 다시 시작하는데,
`NetworkUnit.PublishAttackShadowSnapshot`과
`UnitAttackShadowReplicationClassifier`가 revision을 공격자 인스턴스 전체의
전역 단조값처럼 먼저 비교한 데 있다.

서버는 직전 회차 Impact의 revision 4 이후 다음 회차 commit revision 3을
stale로 게시 거부한다. 이어 같은 다음 회차 Impact가 revision 4가 되면 서버 게시층은
통과하지만 Client는 같은 revision의 서로 다른 비-neutral 상태로 보고
`SequenceRegression`으로 거부한다. 따라서 정상 새 회차의 Windup snapshot이 유실되고
Impact만 늦게 보이는 실제 복제 계약 결함이다.

올바른 정렬 키는 공격자 수명 안에서 `(AttackSequenceId, Revision)`이다. 양수
`AttackSequenceId`가 커지면 새 회차이므로 revision 재시작을 허용하고, 같은 회차
안에서만 revision을 단조 비교해야 한다. `SequenceId=0`의 neutral Align은 다음 후보
생명주기로 수락하되 마지막 양수 sequence 상한은 보존한다.

### BUG-C1-LIFE-002 — 표시 StopCombat이 커밋 예약을 선취소함

Host의 SpearMan unit 116은 TideSpirit unit 86을 대상으로 회차 1을 커밋했지만,
Impact 직전 표시 타겟은 이미 `-1`이었다. Shadow 예약의 Sequencer는
`StopCombat`에 의해 취소되어 `EvaluateImpact=InvalidPhase`였고, Legacy writer는 같은
순간 대상 생존·유효 조건을 통과해 `legacyResult=Applied`를 반환했다.

이는 진단기 오판이 아니라 표시/다음 후보 생명주기가 이미 커밋된 Impact 예약을
변경한 구현 결함이다. `U-TARGET-COMMIT`, `U-IMPACT-TARGETLOCKED`,
`NET-CANCEL-002·004·005`에 따라 표시 타겟 제거나 다음 후보 탐색은 커밋 예약을
다른 타겟으로 이전하거나 무조건 취소할 수 없다. 예약은 자기 TargetId와 Sequencer를
Impact까지 보유하고 그 시점의 생존·유효성·사거리·8도 방향으로 Hit/Miss를 확정해야 한다.

### 교정 결론

1. 서버 게시와 Client 수신이 공유하는 `(AttackSequenceId, Revision)` 순수 비교기를 둔다.
2. 같은 회차의 stale/duplicate와 과거 sequence는 계속 fail-closed한다.
3. 커밋된 Legacy 예약이 남아 있는 동안 표시 `StopCombat`은 그 예약 Sequencer를 취소하지 않는다.
4. 공격자 사망·명시적 취소처럼 Legacy 결과도 폐기되는 경계만 예약 취소로 전파한다.
5. 위 두 실기 형태를 self-validation fixture로 고정한 뒤에만 새 빌드를 허용한다.
