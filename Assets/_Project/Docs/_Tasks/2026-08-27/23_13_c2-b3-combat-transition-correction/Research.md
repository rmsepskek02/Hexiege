# C2·B3 전투 전환 실기 FAIL 교정 Research

이번 작업은 공격 결과 Shadow 자체 검증은 통과했지만 실제 멀티플레이에서 드러난 두 전환 결함을 고치는 조사다. 하나는 전투가 끝난 유닛이 이미 올바른 권위 타일에 있으면서도 타일 중심으로 돌아가지 못하고 영구 정지하는 이동 결함이고, 다른 하나는 Legacy 공격 주기의 초과 시간을 Shadow 타임라인이 반영하지 않아 타격 결과가 늦거나 역순으로 분류되는 공격 결함이다. 서버 권위와 기존 Legacy gameplay writer는 유지하며, 신규 애니메이션·VFX 에셋은 추가하지 않는다.

## 1. 최신 실기 판정

최신 Android Host / Editor Client 동일 경기(`sharedSessionKey=d8cc…`)에서 Editor 자체 검증으로는 통과했던 C2가 실제 멀티플레이 게이트를 통과하지 못했다. 따라서 현 상태는 **Editor self-validation PASS / 멀티플레이 실기 FAIL**이다.

이번 경기에서 서로 다른 원인의 실패가 함께 확인됐다.

- **B3:** Unit 64 `LittleKnight`가 `post-combat-no-safe-forward-center` 상태에서 멈췄다.
- **C2:** 실제 Legacy 주기 경계와 Shadow due 계산이 어긋나 `NotDue`와 다중 타격 `OutOfOrder`가 발생했다.

## 2. B3 전투 종료 후 중심 복귀 결함

### 2.1 재현된 상태

Unit 64의 복귀 `finalTarget`은 `UnitData.Position`과 같은 권위 타일이었다. 이 타일은 이동 가능했지만 Simulation Root는 아직 그 타일 중심에 도달하지 않았다. 현재 구현은 시작 타일과 목표 타일이 같아 경로가 `null`로 나온 상태를 “안전한 전방 중심이 없음”으로 해석했고, `navigationBlocked`는 이후 walkability environment revision이 바뀔 때까지 기다렸다.

결과적으로 다음 두 사실이 잘못 합쳐졌다.

- 논리 위치는 이미 목표 타일이다.
- 공간 Root는 아직 목표 타일 중심에 도착하지 않았다.

같은 권위 타일이라는 이유만으로 중심 복귀가 완료된 것은 아니다. 반대로 이동 가능한 현재 권위 타일의 중심으로 걸어가는 데 A* 타일 경로가 필요하지 않다는 사실도 실패로 취급하면 안 된다.

### 2.2 확정 교정 계약

`finalTarget == UnitData.Position`이고 해당 권위 타일이 현재 walkable이면 이를 **도착-목표 Root 복귀**로 처리한다.

1. Simulation Root가 중심 허용 오차 밖이면 현재 pose에서 그 타일 중심까지 기존 Authoritative Locomotion으로 걸어서 복귀한다.
2. 중심에 도달한 뒤에만 복귀 완료를 커밋한다. 위치 스냅이나 논리 타일 재커밋으로 대신하지 않는다.
3. 같은 타일이라 A* 경로가 비어 있거나 `null`인 것은 이 경우의 `Blocked` 근거가 아니다.
4. 권위 타일이 이동 불가이거나 중심까지의 구간이 실제로 안전하지 않고 유효한 우회도 없으면 기존 `WaitingRepath`/`Blocked` 계약을 유지한다.
5. 실제 unreachable을 성공으로 바꾸거나 environment revision 대기를 무조건 해제하지 않는다.

## 3. C2 Legacy 주기와 Shadow 시간 원천 불일치

### 3.1 원인

Legacy scheduler는 관측 시각 `observedNow`에 틱을 받더라도 실제 주기 경계를 `observedNow - overshoot`로 계산해 초과 시간을 다음 주기에 반영한다. 그러나 coordinator는 관측한 현재 시각과 원래 hit offset을 그대로 Shadow commit/Impact 계산에 사용했다. 이 차이는 한 서버 틱, 최대 약 50ms까지 Shadow Impact due를 늦출 수 있다.

그 결과 실제 Legacy 피해 writer가 정상 순서로 실행됐어도 Shadow 쪽에서는 아직 due가 아니라고 보는 `NotDue`가 발생하고, 다중 타격에서는 앞선 hit completion을 뒤늦게 받아 `OutOfOrder`로 분류할 수 있다.

### 3.2 단조 시간을 보존하는 교정 방식

Shadow commit 시각을 과거의 실제 경계로 backdate하지 않는다. `CommitServerTime`은 관측한 현재 서버 시각을 유지해 reducer 시간 단조성을 보존한다. 대신 Legacy가 사용한 동일 overshoot를 회차별 유효 시간에 반영한다.

```text
effectiveCooldown = max(0, cooldown - overshoot)
effectiveImpactOffset(hit) = max(0, hitTime - overshoot)
```

Shadow Begin/Commit/Impact는 이 유효 cooldown과 hit offset을 하나의 회차 시간 원천으로 사용한다. 현재 관측 시각을 별도의 원본으로 다시 사용해 Impact due를 늦추거나, Shadow만 다른 overshoot 계산을 수행하지 않는다.

### 3.3 2026-08-28 자동 게이트 진단 정정

Unity 재실행으로 확정된 주된 self-validation FAIL 원인은 production 계산이 아니라 **fixture manifest 불일치**다. `UnitType.SpearMan`의 runtime manifest는 `ExpectedImpactCount=1`인데 fixture가 3-hit `HitFrameTimes`를 전달했다. 따라서 `TryResolveRuntime`이 `runtime-impact-count-mismatch`로 정상 거부하고 `CommittedNow=false`를 반환했으며, 뒤의 aggregate assertion이 이 선행 원인을 가려 잘못된 진단을 유도했다.

fixture는 runtime manifest가 지원하는 `FlameSpirit` 6-hit 입력으로 교체하고 reservation count도 6으로 일치시킨다. 검증은 runtime resolve 성공, commit 성공, reservation count와 hit별 timing을 단계별 assertion으로 나눠 최초 실패 지점을 직접 드러낸다. production 계산은 변경하지 않는다.

별도로 overshoot와 `HitFrameTimes`는 production에서 `float` 시간 도메인인데 기존 fixture가 `0.05d`인 `double`을 섞어 `[double][single]0.05 - 0.05d = 약 7.45e-10` 잔차를 만든 비대표성도 함께 교정한다. production 호출과 같은 `float` 입력으로 동일값 0 경계와 바로 아래·위 경계를 검사한다. 이 부수 fixture 문제는 최초·주된 FAIL 원인이 아니며, 정정은 최신 멀티플레이 실기 판정이나 서버 권위·Legacy gameplay writer 상태를 변경하지 않는다.

## 4. 결과 outcome 의미 불일치

`TargetUnavailable`과 `AttackerUnavailable`은 단순 로그 상태가 아니라 pre-impact authorization miss의 원인과 정식 결과 outcome을 연결하는 계약이어야 한다.

- Impact 승인 전에 공격자가 사라졌다면 결과는 `AttackerUnavailable` 의미로 닫혀야 한다.
- Impact 승인 전에 커밋 타겟이 사라졌다면 결과는 `TargetUnavailable` 의미로 닫혀야 한다.
- 같은 reservation·hit의 authorization miss와 Legacy 관측 결과가 서로 다른 원인을 주장하면 정상 결과로 확인하지 않고 fail-closed 한다.
- 결과는 기존 `AttackResultKey`와 정규 `ConfirmImpactResult` 계약을 통과해야 하며, 실패를 `HitApplied`나 `Miss`로 위조하지 않는다.

Legacy 피해·HP·사망 writer의 순서와 권위는 변경하지 않는다.

## 5. 최초 AlignToAttack의 시각 계약

최초로 공격 사거리에 들어가 `AlignToAttack`을 시작할 때 위치는 완전히 정지하지만, 화면이 Idle에서 제자리 회전하는 대신 기존 Attack 클립을 **선행 표현**으로 재생한다. 이때 아직 공격 회차가 커밋되지 않았으므로 다음을 금지한다.

- Impact 및 피해 결과 소비
- 타격 VFX·SFX·트레이서·피격 표현 방출
- Animation Event를 새 공격 회차의 타격으로 재사용

서버 SimulationFacing이 목표 방향 5도 이내에 들어온 커밋 경계에서 실제 Attack cycle을 명시적으로 restart하고 승인한다. 정식 Windup·Impact offset은 이 restart 경계부터 진행한다. 신규 Attack 클립이나 VFX는 만들지 않고 기존 에셋을 재사용한다.

## 6. 변경 범위와 비범위

변경 대상은 전투 종료 후 같은 권위 타일 중심 복귀, Legacy overshoot와 Shadow 타임라인의 결속, unavailable outcome 확인, 최초 Align 선행 표현이다.

다음 항목은 유지한다.

- 서버 Simulation Root 단일 writer와 NetworkTransform 복제
- Legacy 피해·HP·사망 이벤트·기존 RPC/VFX gameplay writer
- 5도 공격 회차 커밋과 8도 Impact 방향 유지 기준
- TargetId·AttackSequenceId·HitIndex·AttackResultKey 결속
- 차단 타일과 실제 unreachable의 기존 `WaitingRepath`/`Blocked` 규칙
- 신규 에셋 없음

이번 수정의 자동 검증 PASS만으로 멀티플레이 완료를 선언하지 않는다. 같은 역할 구성과 역할 교대 실기에서 B3 영구 정지 0, C2 `NotDue`·`OutOfOrder` 0, unavailable 정규 결과 확인 PASS를 확인해야 완료다.

## 7. 2026-08-28 Editor Host / Android Client 실기 재검증

최신 동일 경기(`sharedSessionKey=fda268f4…5687d`)는 Android Client에서 애플리케이션 `ERROR`·예외·Android FATAL이 0건이었고, Client Shadow 결과 거부도 0건이었다. 그러나 서버 권위 판정은 Editor Host가 수행하므로 이 사실만으로 공격을 PASS로 판정할 수 없다. Host `c2-attack-impact-result-shadow-v6` terminal은 `serverResults=942`, `resultFailures=97`, `verdict=FAIL`이었다.

사용자가 화면에서 발견한 “공격 사거리 안에 적이 없는데 공격하는 유닛”은 단순 표현 잔류만이 아니었다. `Assault` Unit 16→Target 62와 Unit 58→Target 77 두 Impact에서 예약된 Legacy/Shadow TargetId와 회차는 일치하고 방향 오차도 각각 `0.192°`, `0.248°`로 8° 기준 안이었지만, Shadow는 `AuthorizedMiss`, Legacy는 `Applied(10)`을 기록했다. 타겟이 살아 있고 Legacy가 적용됐으며 방향 조건도 통과했으므로 Shadow miss의 남은 원인은 권위 사거리 실패다. 실제 Legacy `ApplyAttackDamageCore`에는 “사거리 체크 없음 — 타겟 고정 설계”가 남아 있어, 현재 `U-IMPACT-TARGETLOCKED`의 Impact 시점 사거리 재검증 규칙과 충돌한다.

97건의 결과 불일치는 다음 세 부류로 분리한다.

- **실제 gameplay 결함:** `Applied + AuthorizedMiss` 2건. 사거리 밖 피해가 실제 적용됐다.
- **C2 시간·생명주기 결함:** `Applied + NotDue` 5건, `TargetUnavailable + NotDue` 66건, `AttackerUnavailable + NotDue` 12건. Legacy completion이 같은 hit의 수락된 Shadow authorization 없이 도착했다.
- **진단기 오탐:** `TargetUnavailable + Accepted + Miss` 12건. coordinator는 정규 `Miss`를 만들었지만 observer가 모든 non-`Applied` 결과를 `Cancelled`로만 기대해 정상 확인도 failure로 집계했다.

상세 `legacy-impact-dispatch` 7건은 모두 예약된 Legacy TargetId와 Shadow TargetId가 일치했다. 따라서 이번 증거는 다른 타겟으로 피해가 이전된 C1 TargetId 결함이 아니라, 동일 타겟에 대한 Impact 사거리 승인·C2 due 시간·결과 채점 계약의 결함이다. B3 MOVE terminal의 rejected/invalid/writer/adapter 오류는 0이고 양쪽 ROOT local terminal은 PASS였으므로 이번 교정 범위와 분리한다.

## 8. 2026-08-29 Editor Host / Android Client 실기 결과

이번 실기는 이전에 발견된 “사거리 밖인데 피해가 적용되는 문제”를 막는 안전장치가 실제 피해를 차단한다는 점은 확인했지만, 공격을 정상적인 결과로 끝내는 연결과 이동 재탐색에는 아직 실패가 남아 있음을 보여준다. 따라서 전체 판정은 **C2 FAIL / B3 FAIL**이며, 이번 한 경기만으로 사거리 밖 피해 문제가 완전히 해소됐다고 선언하지 않는다.

### 8.1 세션과 역할

- Editor 로그: `Assets/_Project/Docs/_Logs/_editor/2026-08-29/RuntimeLog.txt`
- Android 로그: `Assets/_Project/Docs/_Logs/2026-08-29/04_55_logcat/RuntimeLog_device.txt`
- 동일 경기 키: `sharedSessionKey=a27a5a928ffbe260336b3898a63819ec850a896acc91491c20503e9f189c86a2`
- 역할: Editor `host`, Android `client`
- 관측 유닛 종류: Host C2 `coveredUnitTypes=8/25`

### 8.2 C2 공격 결과 FAIL

Host는 공격 결과 1,508건을 처리했고 220건을 실패로 기록했다(`RuntimeLog.txt` 957행). Client는 정규 결과 1,288건을 수락했고 거부는 0건이었다(`RuntimeLog_device.txt` 63190행).

220건의 실패는 전부 다음과 같은 같은 형태였다.

```text
legacyStatus=AuthorizationUnavailable
legacyApplied=0
legacyHp=-1
completionStatus=NotDue
reason=completion-without-accepted-authorization
```

즉 **피해 writer는 220건 모두 피해를 적용하지 않아 fail-closed 했지만**, 그 타격을 `Miss` 같은 정식 종료 결과와 연결할 승인 정보도 만들지 못했다. 현재 흐름상 `TryCaptureUnitActionPose` 실패 또는 dispatch 비성공 때문에 authorization이 생성되지 않은 경우가 이 집계에 들어올 수 있다. 그러나 현행 로그는 어느 단계에서 왜 실패했는지를 남기지 않으므로, 220건 각각의 세부 원인을 사망·제거·위치 취득 실패 중 하나로 확정할 수 없다.

동시에 Host C2 terminal은 `targetMismatches=0`, `correlationFailures=0`, `dropped=0`, `terminalPreflightFailures=0`, `manifestFailures=0`, `gameplayWrites=0`이었다(`RuntimeLog.txt` 958행). 이번 경기에서 사거리 밖 실제 피해 재발은 관측되지 않았지만, 8/25 종류만 포함된 단일 경기이고 220건이 미완 종료됐으므로 **사거리 규칙 전체 PASS 또는 완전 해소 근거로 확대하지 않는다.**

### 8.3 B3 이동 FAIL

Host MOVE terminal은 이동 frame·공간 commit·재탐색 핵심 불변식 대부분을 정상으로 기록했다. `rejected=0`, `invalid=0`, writer·gate 실패 0, stationary Walk 위반 0, 공간 transition `1133/1133`, repeated recoverable·fatal·same-frame retry·stage divergence 0, drop/preflight 0이었다(`RuntimeLog.txt` 951~955행).

다만 `RejectedInvalidPath` adapter failure가 4건 발생해 최종 verdict는 FAIL이다.

| 유닛 | 발생 경로 | 로그 위치 |
|---|---|---|
| Unit 44 | `post-combat-corridor` | Editor 827행 |
| Unit 8 | `post-combat-corridor` | Editor 829행 |
| Unit 14 | `post-combat-corridor` | Editor 830행 |
| Unit 113 | `astar-corridor` | Editor 860행 |

네 건 모두 `failedWaypoint`, `candidatePathStart`, `candidatePath`가 `unavailable/invalid`였다. 사용자는 실기 화면에서 건물 배치와 주변 경로를 직접 확인했고, 관찰한 멈춤 사례는 완전 차단이 아니며 실제 우회로가 존재했다고 확인했다. 이 시각 관찰과 Host의 `RejectedInvalidPath`를 결합하면 **최소 관찰 사례는 도달 가능한데 재탐색이 실패한 B3 구현 결함**이다. 다만 현행 로그에는 당시 environment revision, 권위 start tile·Root, 목적 타일과 pathfinder 실패 단계가 없어, 네 건 모두 같은 원인인지와 정확한 실패 지점이 start normalization·cache/environment 무효화·pathfinder 중 어디인지는 확정할 수 없다.

### 8.4 정상 확인 범위와 남은 판정

- Android 파일에서 애플리케이션 `[ERROR]`, Android FATAL, `UnhandledException`은 0건이다.
- Host/Client ROOT **개별 observer**는 각각 45 endpoint, drop 0, evidence complete, local verdict PASS다(`RuntimeLog.txt` 959행, `RuntimeLog_device.txt` 63206행).
- 위 ROOT 결과는 양쪽 파일을 교차 비교한 공식 `Unit Root Pose Cross Audit PASS`가 아니다. 개별 local PASS를 공식 교차감사 PASS로 바꾸어 기록하지 않는다.
- 이번 결과는 “잘못된 피해 방지”는 작동했지만 “모든 타격을 정규 결과로 1:1 종료”하지 못했고, 이동에서는 최소 한 건의 reachable 재탐색 실패가 남은 상태다. 나머지 세 건의 원인 분리와 교정 확인에는 새 실기 근거가 필요하다.
