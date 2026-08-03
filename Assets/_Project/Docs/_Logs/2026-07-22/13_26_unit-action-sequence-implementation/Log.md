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
