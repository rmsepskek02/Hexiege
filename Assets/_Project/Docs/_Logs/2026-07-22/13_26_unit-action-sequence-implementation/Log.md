# Unit ActionSequence QA-Fix 로그

## 2026-08-03 — Tracer B2 서버 이동·SimulationFacing Shadow

### Round 1 — 이동 reducer와 Shadow 관측

- pure Application `UnitMovementReducer`와 command/segment scope 계약 추가
- 10° 진입 / 15° 이탈 히스테리시스, target-acquire priority, revision fail-closed 적용
- 서버 read-only observer와 클라이언트 sentinel 연결
- Legacy 이동·회전 writer와 서버 권위 피해·RPC·VFX 유지
- 초기 observer v2 시험에서 Android 로그 손실 `droppedLogs=1233`으로 FAIL

### Round 2 — Android manifest 손실 방지

긴 coverage manifest가 Android Logcat에서 절단되어 유닛별 증거와 전체 해시를 재구성할 수 없는 문제를 교정했다.

- entry를 자르지 않는 UTF-8 byte-bounded 청크 적용
- 청크 ordinal, 전체 청크 수, payload 문자/byte 수 기록
- manifest SHA-256과 summary/END terminal 3줄 예약
- 최대 29청크 성공, 30청크 preflight 거부와 malformed/dropped evidence fail-closed self-validation 추가
- Android Host 장문 manifest 15/15청크와 SHA-256 재구성 PASS

### Round 3 — endpoint false invalid 제거

CannonCart가 목표점에 정확히 도착해 현재 위치=목표 위치이고 expected writer delta=0인 정상 프레임을 invalid 처리했다. 실제 이동 구현 오류가 아니라 관측 입력의 “이동 의도 없음” 표현 누락이었다.

- desired XZ 거리와 expected writer delta가 모두 epsilon 이하일 때만 `NoIntent`로 정규화
- 먼 목표+delta 0은 이동 의도로 유지
- endpoint+material delta는 invalid 유지
- 동일 scope의 move → endpoint → target priority → resume lifecycle 회귀 추가
- 최초 fixture가 부동소수점 10° 경계에서 의도와 다르게 Align으로 판정되어 fixture를 명확한 12°→9° lifecycle로 교정
- 순수 double 10° 경계 검증은 그대로 유지
- 최종 self-validation PASS, observer schema `b2-movement-shadow-v5`

### Round 4 — 잘못된 브랜치 시험 폐기

Git 브랜치가 B2 작업 브랜치가 아닌 상태에서 실행한 5종 시험은 Editor/Android 모두 `[UAS-MOVE-SHADOW]`가 0건이었다. B2 변경이 GitHub Desktop stash에 보관된 사실을 확인해 `codex/unit-movement-attack-sync-audit`에 복원하고 self-validation·Android 재빌드 후 다시 시험했다. 로그가 없던 시험은 25종 집계에 포함하지 않았다.

### Round 5 — 25종 멀티플레이 coverage

Android 1대와 Unity Editor counterpart를 사용했으며 Windows/Standalone build는 사용하지 않았다. 각 경기는 `sharedSessionKey`로 Editor 파일 로그와 Android Logcat을 짝지었다.

| 그룹 | 서버 역할 | 유닛 타입 | 결과 |
|------|-----------|-----------|------|
| 1 | Editor Host | Assault, LittleKnight, Pistoleer, SpearMan | Blue/Red EVIDENCE, observer v4 |
| 2 | Editor Host | BattleAxe, CannonCart, Sniper, Tank | v5 재시험 Blue/Red EVIDENCE |
| 3 | Android Host | BearGuard, MushroomBomber, RabbitTrickster, RhinoBreaker | Blue/Red EVIDENCE |
| 4 | Android Host | BloomFairy, EagleArcher, FoxMagician, LionKnight | Blue/Red EVIDENCE |
| 5 | Editor Host | DustSpirit, EmberSpirit, FlameSpirit, InfernoSpirit, TideSpirit | Blue/Red EVIDENCE |
| 6 | Editor Host | BoulderSpirit, QuakeSpirit, StreamSpirit, TorrentSpirit | Blue/Red EVIDENCE |

인정 세션 합계는 accepted reducer decision 545,190회, manifest entry 420개다. 모든 인정 세션에서 invalid/duplicate/stale/illegal/scope/unknown, client reducer invocation, Simulation Root write attempt, exception, dropped log, manifest preflight failure와 terminal overflow가 0이고 manifest ordinal·entry count·SHA-256이 일치했다.

그룹 5는 reducer 200,291회, manifest 20/20청크·87항목, SHA-256 `16f57a6d8ac2383f4d41365511f835ecd68d8779e9e2c329ac42ded7fbb31a16`이다. 그룹 6은 reducer 113,236회, manifest 13/13청크·58항목, SHA-256 `771a8b22a4ef023d64815f1754bc006e205894bb45a6e9baa25758ccbde16853`이다.

### 최종 QA 판정

**PASS — B2 read-only Shadow 및 25/25종·Blue/Red 누적 증거 게이트.**

첫 4종은 observer v4, 나머지 21종은 endpoint adapter가 보강된 v5로 검증했으며 reducer schema는 모두 `b2-movement-reducer-v1`이다. 각 유닛을 Host/Client 양 역할에서 각각 시험한 것은 아니다. `legacyMovedWhileShadowAlign`은 신규 계약과 Legacy writer의 분류된 차이이며 B3 전환에서 제거해야 한다. 실제 이동 writer 전환, 공격 타겟·공격 방향, 시각 Impact·실제 피해 적용 시점은 아직 미완료다.

## 2026-08-19 — B3 이동·행동 경계 2차 교정

### Round 1 — v2 실기 실패 분해

- 동일 세션 Host 66,838 frame: ownership conflict 737, stationary Walk 58, rejected/invalid 10/10
- Android Client 복제 1,540표본과 양쪽 Root Pose는 오류 0
- 지연된 공격 타겟 이벤트, Held 애니메이션 재가동, reducer 이전 scope 소비를 각각 독립 원인으로 확정

### Round 2 — 서버 권위 유지 교정

- 서버 gameplay 사거리 진입만 `Movement → Action` 회전 소유권을 연다.
- 지연된 서버 Start/Change 타겟 이벤트는 Action 소유권이 없으면 무시하고, 서버 Stop 이벤트는 소유권을 닫지 않으며 v3 observer에 정보성으로 집계한다.
- Held가 유지되는 동안 Walk 시작·재개 경로보다 정지 상태를 우선하며 실제 Animator speed를 재검증한다.
- command/segment scope를 reducer 평가 전에는 예약만 하고 수락 뒤 확정해 planner 실패 재시도의 revision을 보존한다.
- 순수 회귀에 지연 서버 이벤트 차단과 scope 재시도 불변식을 추가했다.
- Unity Roslyn 런타임·Editor assembly 컴파일 PASS. 기존 NGO obsolete 경고 외 신규 오류 없음.

### 현재 판정

**B3 FAIL / OPEN.** Editor self-validation과 `b3-movement-authority-v3` Android Host/Client 실기 재검증 전에는 완료로 판정하지 않는다.

### Round 4 — trajectory–corridor 안전 후보 교정 구현

- `UnitTrajectoryCorridorResolver` 순수 정책을 추가해 full smooth → 축소 smooth → full direct → 축소 direct → 제자리 정렬 순으로 corridor 내부 후보를 선택한다.
- `UnitView`의 서버 단일 이동 writer가 이 정책을 사용하도록 연결했다. 현재 pose 자체도 안전하지 않을 때만 `RepathRequired`를 반환한다.
- 이전 빌드와 증거가 섞이지 않도록 관측 스키마를 `b3-movement-authority-v4`로 변경했다.
- v4 summary에 full/reduced smooth·direct, stationary alignment, repath-required 선택 횟수를 남겨 실기에서 fallback 사용 여부를 확인할 수 있게 했다.
- 상태: 컴파일·Editor self-validation·Android Host/Client 실기 재검증 전, **B3 FAIL / OPEN 유지**.

### Round 5 — v4 실기와 A* start/corridor 불일치 확정

- 동일 세션: `d9924ba306e2077ad7113852407698022d30353439962fa32bfdf73dad450d12`.
- Android Host v4: authority 핵심 위반 0, `corridorReducedDirect=20`, `corridorStationaryAlignment=5`, `corridorRepathRequired=454`.
- Editor Client v4: replicated samples 1,234, 오류 0. 양쪽 Root Pose 64유닛·7종 PASS.
- `REPATH-FAIL-CLOSED=66`(14유닛), NoProgress 55, InvalidPath 11. 첫 scope `0/0` 실패 52건.
- 원인: A*는 non-walkable current tile을 출발점으로 허용하지만 corridor sample 0은 이를 거부한다.
- 판정: **B3 FAIL / OPEN**. 첫 구간 `path[0]` egress만 허용하는 규칙·pure 회귀·production adapter 교정 후 재검증한다.

### Round 6 — `path[0]` start-tile egress 교정 구현

- pure `UnitTrajectoryCorridorSamplePolicy`로 허용 범위를 첫 segment의 `path[0]`에 한정했다.
- `UnitView` point sweep에 연결하고 중간 non-walkable·경로 밖·재진입 fail-closed를 유지했다.
- 관측 스키마 `b3-movement-authority-v5`, summary 지표 `corridorStartTileEgressFrames`를 추가했다.
- 상태: Unity 컴파일·self-validation·Android Host/Client 재검증 전, **B3 FAIL / OPEN 유지**.

### Round 7 — v6 실기 원인 확정 및 도착 커밋 교정 착수

- 세션 `1b14b299...dea0`, Android Host 60,248 frame과 Editor Client 복제 1,943표본에서 기존 authority 핵심 위반과 치명 예외는 0이었다.
- Root Pose는 매칭 전 observer 제한 시간이 끝나 유닛 0·INCOMPLETE이므로 이번 판정 근거에서 제외했다.
- `REPATH-FAIL-CLOSED` 80건·18유닛의 terminal evidence가 모두 `rootHex != UnitData.Position`을 기록했다. 78건은 논리 위치와 A* path start가 같고 실제 Root만 1~2타일 뒤였다.
- `AlignPathStartToTransform`이 역방향 첫 스텝 보정 중 Root 도착 전에 `ProcessStep`을 호출해 `UnitData.Position`과 위치 역인덱스를 앞당기는 직접 원인을 확정했다.
- 교정 기준: 논리 위치를 미리 변경하지 않고 현재 논리 타일→앞쪽 인접 타일을 첫 segment로 합성한다. Root가 해당 waypoint에 실제 도착한 뒤 기존 checkpoint/`ProcessStep` 경계에서만 위치·점유를 커밋한다.
- `RequestMoveFrom`은 명시적 출발점에서 경로만 계산하고, pure `UnitPathStartTransitionPolicy`는 인접성·후속 경로 시작점을 검증해 현재 논리 타일을 path[0]에 보존한다.
- `AlignPathStartToTransform`의 선행 `ProcessStep`을 제거하고 합성 경로를 연결했다. 비인접·경로 불일치는 원본 유지로 fail-closed한다.
- `GameSystemRules_Units.md`에 경로 계산/논리 커밋 분리, 실제 Root waypoint 도착 시 원자 커밋, 비인접 선점·클라이언트 보정·선행 `ProcessStep` 금지를 공통 규칙으로 반영했다.
- 새 실기 빌드는 `observerSchema=b3-movement-authority-v7`로 식별한다.
- Unity Roslyn 런타임·Editor assembly 컴파일 PASS. 기존 NGO `RequireOwnership` obsolete와 미사용 필드 경고 외 신규 오류 없음.
- 문서 정합성 검사 0건. 메뉴 self-validation·새 실기 전이므로 **B3 FAIL / OPEN 유지**.

### Round 8 — v7 실기와 route checkpoint/공간 점유 혼합 원인 확정

- 동일 세션 `c7dfe0bd...ccd3`, Android Host 67,736 frame과 Editor Client 복제 1,788표본을 결합했다.
- Host authority 핵심 위반, Client 복제 오류, 치명 예외는 0이었다. 양쪽 Root Pose는 64유닛·2,765표본·5종, 오류 0으로 PASS했다.
- `REPATH-FAIL-CLOSED`는 42건이었다. parse 가능한 `astar-corridor` 39건은 모두 `rootHex != UnitData.Position`, `UnitData.Position == sourcePathStart`, `OutsideCorridor`였고 Root와 도메인 위치 거리가 1~5타일까지 누적됐다.
- 남은 직접 원인: smooth planner의 최근접 waypoint 소비는 route 진행 신호인데, `MoveAlongPathV3`가 이를 실제 타일 도착으로 간주해 즉시 `ProcessStep`을 호출한다. Chase는 반대로 Root만 이동하고 전투 복귀에서 별도 논리 점프를 만든다.
- 규칙을 “checkpoint 소비와 실제 공간 타일 커밋 분리”로 정정했다. 공용 서버 writer의 Root 경계 교차만 인접 공간 전이를 커밋하며 A*·Chase·PendingRepath·PostCombatResume가 이를 공유한다.
- 구현은 deterministic RED→GREEN slice가 완료될 때까지 새 실기 빌드를 요청하지 않는다.
- 판정: **B3 FAIL / OPEN**. v7은 선행 경로 보정 결함 하나를 제거했지만 공간 점유의 근본 결합은 해결하지 못했다.

#### v8 구현·컴파일·독립 QA 코드 게이트

- pure sampled spatial transition policy와 facing-preserving Application preflight/commit을 구현했다. 전체 인접 전이를 검증한 뒤에만 공간 타일과 위치 역인덱스를 commit해 부분 적용을 차단한다.
- A*·Chase·PendingRepath·PostCombatResume는 공용 서버 writer의 공간 커밋기를 공유한다. ReducerAuthoritative route checkpoint의 직접 `ProcessStep`은 제거했고 Legacy rollback 동작은 보존했다.
- staged reducer는 `Prepare → snapshot publish → commit` 순서를 사용한다. Chase nontrajectory facing 동기화를 추가했고 per-frame corridor captured lambda를 제거했다.
- observer schema를 `b3-movement-authority-v8`로 올리고 공간 표본·preflight·commit·비인접/recovery 계수를 추가했다.
- Runtime Roslyn PASS. 기존 NGO obsolete와 `EffectManager` 미사용 경고 외 신규 오류·경고 없음. Runtime 뒤 Editor Roslyn을 순차 실행해 PASS했다.
- 독립 QA 최초 리뷰의 P1 3건은 교정 후 재검토에서 모두 CLOSED됐다. 차단 이슈 0.
- Unity 메뉴 `[UAS-DIAG]` self-validation과 새 Android Development Build 실기는 아직 실행하지 않았다.
- 판정: **v8 코드 게이트 PASS / B3 FAIL·OPEN 유지**. 다음은 메뉴 self-validation이며, 통과 전 Android 빌드·실기 완료를 주장하지 않는다.

### Round 9 — v8 Android 공간 preflight 반복 실패

- Unity 메뉴 `[UAS-DIAG]` self-validation PASS 뒤 새 Android Development Build로 시험했다.
- `sharedSessionKey=f5d...`, Android Host·Editor Client 양쪽 schema `b3-movement-authority-v8`, 역할·세션 식별자 일치.
- Host authority, Client replication, 양쪽 local Root PASS.
- 수동 동등 cross-audit PASS: identities 52, overlap 50, matched 50, mismatch 0, ratio `0.962`, max axis `0.000917m`, max rotation `0°`.
- Host `spatialPreflightFailures=24`: `OutsideLogicalPath=11`, spatial policy `Ready` 뒤 Application preflight false 13.
- Application false 13건은 모두 Blue SpearMan unit 58, `(8,7) → (8,6)` 전이에서 약 1초 반복됐다.
- 원인: corridor candidate 허용 범위와 strict ordered spatial policy가 다른 seam에서 판정된다. recoverable preflight false가 `Rejected → cleanup → 외부 동일 목표 재명령`으로 이어져 반복된다.
- 교정 범위: typed preflight result(reason/offending tile), corridor/ordered spatial viability 동일 seam, OutsideLogicalPath·missing·non-walkable의 `RepathRequired → WaitingRepath`, fatal dependency/invalid만 `Rejected`, 같은 frame 재실행 금지, observer staged accounting 분리.
- 종료 게이트: pure 회귀·self-validation 뒤 새 Android 경기에서 spatial failure/recoverable reject/cleanup 반복/fatal reject 0, authority·replication 오류 0, 양쪽 Root/cross-audit PASS.
- 판정: 네트워크·복제·Root는 PASS했지만 서버 공간 preflight 반복이 남아 **B3 FAIL / OPEN**. 마지막 국소 교정 전 역할교대·25종·Legacy rollback 금지.

### Round 3 — v3 Android Host 실기와 corridor 실패 확정

- 같은 경기 Host movement 55,120 frame에서 rejected/invalid/ownership conflict/stationary Walk 모두 0
- Client 복제 1,435표본 오류 0, Host/Client Root Pose 64유닛·7종 PASS
- 지연 서버 이벤트 246건은 의도대로 차단
- 별도 `astar-corridor` fail-closed 76건·21유닛: no-progress budget 69, invalid path 7
- 원인: full-distance 후보 거부 뒤 이동량 축소와 제자리 정렬 없이 같은 A* path를 반복 요청
- 판정: 9.11 행동 경계 교정은 PASS 증거 확보, B3 전체는 9.12 구현 전 FAIL / OPEN

### Round 10 — v9 typed 공간 후보/preflight 구현 코드 게이트

- bool preflight를 status·offending tile·transition index를 가진 `UnitSpatialPreflightResult`로 교체하고 최종 후보를 `Accepted / CandidateUnsafe / RouteInvalidated / Fatal`로 분리했다.
- corridor fallback probe와 최종 stage가 같은 logical path·타일 표본·Application preflight seam을 사용한다. divergence는 fail-closed 계수와 예외로 남긴다.
- `OutsideLogicalPath`·non-adjacent·missing tile·non-walkable은 unsafe commit 없이 `RepathRequired → WaitingRepath`, fatal dependency·invalid chain만 `Rejected`로 연결했다. recoverable 뒤 cleanup과 same-frame 재실행은 observer END 실패 항목이다.
- `planned`는 실제 commit attempt 직전에만 증가하고 `committed`는 성공 뒤에만 증가한다. END는 equality를 강제하며 recoverable signature/frame 이력은 양수 전이의 성공 commit에서만 clear한다.
- observer schema를 `b3-movement-authority-v9`로 올렸다. `spatialFinalRecoverableRepaths`는 0 강제가 아니라 resolved/nonrepeat/no-cleanup coverage 대상이고 repeated/same-frame/cleanup/fatal/stage divergence/commit failure는 실패다.
- lifecycle retire는 서버 phase baseline, 클라이언트 복제 baseline과 recoverable signature/frame을 제거한다. identity mismatch도 stale baseline을 retire하되 충돌을 집계하며 object-id 재사용 회귀를 추가했다.
- Runtime/Editor Roslyn compile PASS. 기존 NGO obsolete·`EffectManager` 미사용 경고만 존재한다. 독립 정적 QA P0~P2 없음.
- Unity 메뉴 `[UAS-DIAG]` self-validation 실제 실행과 새 Android v9 Host·Editor Client 실기는 아직 미실행이다.
- 판정: **v9 구현·컴파일·정적 QA 코드 게이트 PASS / B3 FAIL·OPEN 유지**. 다음은 메뉴 self-validation, 그 뒤 새 Android gate다.
