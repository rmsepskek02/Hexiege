# C3 Authoritative Result Presentation Shadow 교정 로그

## Round 1 — 동일 v3 실기 실패 분석

### 입력

- Editor Host: `Assets/_Project/Docs/_Logs/_editor/2026-08-31/RuntimeLog.txt`
- Android Client: `Assets/_Project/Docs/_Logs/2026-08-31/12_54_logcat/RuntimeLog_device.txt`
- 공통 세션: `49c170e7db50af7b728dcddd5cbfa568e807437ece91882486437b306e0cf82d`
- 계약: `observerSchema=c3-authoritative-result-presentation-shadow-v3`, `pipelineMode=PresentationShadow`, `combatSchema=1`

### 판정

- 네트워크/세션: PASS
- B3 이동/ROOT: PASS
- C2 서버 권위 결과: PASS — 1,470/1,470 수락, 거부·결과 실패·타겟 불일치·상관 실패 0
- C3 표현 상관관계: FAIL
  - Host: unmatched 915, direction 59, timing 29
  - Client: unmatched 866, direction 320, timing 23

### 확인된 실패 원인

1. 서버가 `impactEnabled=true` RPC를 먼저 보내고 이후 Shadow 회차를 생성·게시한다. Client의 승인 비트와 정확한 공격 회차 ID가 서로 다른 네트워크 채널에 있으므로 원자성이 없다.
2. scheduler가 규칙의 “공격자 회차별 64개” 대신 전역 64개 큐를 사용한다. 다수 유닛 경기에서 무관한 회차끼리 정상 후보를 축출할 수 있다.
3. Unmatched·direction·timing은 terminal 합계만 있고 원인 표본이 없어 다음 실패를 공격자·회차·단계별로 추적할 수 없다.
4. 방향 mismatch 기준 0.1°가 하드코딩되어 규칙의 8° 공격 정렬 계약과 분리되어 있다.

### 이번 Round의 교정 범위

- 공격 커밋 RPC에 정확한 공격자 instance·sequence·첫 hit index를 함께 실어 원자적으로 적용
- Marker scope의 별도 `NetworkVariable` 도착 의존 제거
- 회차별 bounded buffer 및 수명 정리
- C3 v4 bounded 실패 상세와 단계별 계수
- RED 회귀 추가 후 전체 self-validation PASS까지 반복

### 금지 상태

C2가 PASS여도 C3가 FAIL이므로 실제 presentation emitter 전환은 금지한다. Legacy gameplay writer와 emitter는 이번 교정 중 그대로 유지한다.

## Round 2 — v4 구조 교정 및 자동 gate

### 구현

- 공격 승인 RPC에 정확한 attacker instance·sequence·first hit를 함께 전달하고, Shadow 회차 생성 뒤 전송하도록 순서 교정
- UnitView Marker cursor를 RPC scope로 초기화하여 별도 복제 state 도착 경쟁 제거
- result/Legacy reorder 상한을 공격자 회차별 64개로 격리
- overflow·expire·retire 수명 분류와 consumed-stage 정리
- observer v4 단계별 unmatched, bounded failure detail, 최대 aim/timing delta
- 방향 mismatch를 8.0° 단일 계약으로 변경

### 자동 판정

- Unity compile error: 0
- `[UAS-DIAG]`: PASS
- `[C2-AUTHORITY]`: PASS
- `[C3-PRESENTATION-SHADOW]`: PASS
- 문서 정합성 검사: 문제 없음
- Build And Run: `Succeeded`, 1,533초
- AAB: `Build/Hexiege.aab`, 141,976,729 bytes, 2026-08-31 14:08:20
- 기기 실행: 새 프로세스 PID 23340 로그 유입 확인

### 다음 Round gate

새 빌드의 Host/Client BEGIN에서 양쪽 모두 v4·PresentationShadow·combat schema 1·동일 session key인지 먼저 확인한다. 입장 gate가 맞는 경기만 C2/C3 terminal 판정에 사용한다. C3 실기 PASS 전까지 emitter 전환은 금지한다.

## Round 3 — 동일 v4 실기 실패 분석

### 입력

- Android Host: `Assets/_Project/Docs/_Logs/2026-08-31/14_27_logcat/RuntimeLog_device.txt`
- Editor Client: `Assets/_Project/Docs/_Logs/_editor/2026-08-31/RuntimeLog.txt`
- 공통 세션: `8baeb13051d8550781197666a449cfbd8e90d7e882154f67465120ed810ecb15`
- 계약: 양쪽 `observerSchema=c3-authoritative-result-presentation-shadow-v4`, `PresentationShadow`, combat schema 1

### 판정

- 입장 계약: PASS
- B3 이동/ROOT: PASS
- C2: PASS — Host 결과 1,278 / 실패 0, Client 수락 1,278 / 거부 0
- C3: FAIL
  - Host: invalid 2,230, unmatched 33, direction 0, timing 38
  - Client: invalid 2,255, unmatched 37, direction 24, timing 97
- 비-UAS FATAL·ANR·예외: 현재 앱 PID와 경기 시간 범위에서 0

### 확정 원인

서버는 매 공격 주기마다 새 회차를 생성했지만, 정확한 presentation scope RPC가 최초 전투 진입 가드 `_combatAnimationSent.Add(id)` 안에 있었다. 따라서 첫 공격 뒤의 정상 연속 공격은 새 scope를 받지 못했고, Marker/Tracer가 instance 0 / sequence 0으로 대량 Invalid가 됐다. v4 exact correlation과 per-sequence buffer는 이전 unmatched/direction을 크게 줄였지만 이 수명 결합 결함까지 해결하지 못했다.

### Round 4 교정 gate

- Attack 클립 시작은 최초 한 번, presentation scope는 모든 서버 커밋마다 발행한다.
- 후속 scope는 Animator를 restart/CrossFade하지 않고 marker cursor만 새 회차 hit 0으로 갱신한다.
- 연속 N회 공격 fixture를 self-validation에 추가한다.
- 대량 Invalid를 제거한 새 실기에서 남은 direction/timing을 다시 판정한다.

## Round 4 — 연속 공격 회차 scope 교정 및 자동 gate

### 구현

- `_combatAnimationSent`는 Attack 표현 최초 시작 여부만 담당하도록 제한했다.
- 정상 서버 공격 커밋마다 정확한 attacker instance·sequence·first hit·revision scope를 발행한다.
- 이미 Attack 상태인 후속 커밋은 animation restart/CrossFade 없이 marker cursor만 새 회차 hit 0으로 교체한다.
- 연속 세 회차의 매 커밋 scope 발행, 단조 증가 sequence/revision, 중복·오래된 scope 거부, 후속 CrossFade 0을 self-validation에 추가했다.

### 자동 판정

- Unity compile error: 0
- `[UAS-DIAG]`: PASS
- `[C2-AUTHORITY]`: PASS
- `[C3-PRESENTATION-SHADOW]`: PASS
- 문서 정합성 검사: 7종 모두 문제 없음
- Build And Run: `Succeeded`, 1,775초
- AAB: `Build/Hexiege.aab`, 141,976,668 bytes, 2026-08-31 17:48:34
- 기기 실행: 새 프로세스 PID 2354 로그 유입 확인

### 다음 Round gate

새 빌드의 동일 경기 Host/Client 로그에서 v4·`PresentationShadow`·combat schema 1·동일 session key를 먼저 확인한다. C2 결과 보존과 함께 C3 Invalid가 제거됐는지 확인하고, 남은 unmatched·direction·timing은 서로 독립적으로 판정한다. 실기 PASS 전까지 emitter 전환은 금지한다.

## Round 5 — scope 소모 뒤 반복 Marker 방출 실패

### 입력

- Android Client: `Assets/_Project/Docs/_Logs/2026-08-31/19_02_logcat/RuntimeLog_device.txt`
- Editor Host: `Assets/_Project/Docs/_Logs/_editor/2026-08-31/RuntimeLog.txt`
- 공통 세션: `d8ed6d4a9ab3049d2b3bc9ab5d641d86909e0e33a73b37647388b2e81fc15890`
- 계약: 양쪽 `observerSchema=c3-authoritative-result-presentation-shadow-v4`, `PresentationShadow`, combat schema 1

### 판정

- 입장 계약: PASS
- B3 이동/ROOT 로컬: PASS — 이동 invalid·writer 충돌 0, 양쪽 ROOT PASS·증거 drop 0
- C2: PASS — Host 서버 결과 2,566 / 실패 0, Client 수락 2,566 / 거부 0
- C3: FAIL
  - Host: invalid 3,800, unmatched 48, direction 3, timing 26
  - Client: invalid 3,777, unmatched 88, direction 37, timing 59
- 비-UAS FATAL·ANR·최신 경기 예외: 0

### 확정 원인

매 커밋 scope 발행은 적용됐지만 한 scope의 마지막 HitIndex를 소비한 뒤 로컬 Impact 허가가 닫히지 않았다. `TryAcquirePresentationShadowMarkerScope`는 cursor 범위 초과로 false를 반환했으나 `OnAttackHit`은 별도 `_attackPresentationImpactEnabled`가 true라는 이유로 VFX·SFX·Tracer·피격 신호를 방출했다. 따라서 화면 타격과 정규 서버 회차가 없는 `instanceId=0/sequenceId=0` 관측이 실제 emitter 경로에서 누적됐다.

### Round 6 교정 gate

- 공격 표현 허가를 `Closed/Armed/Consumed` 1회성 상태로 통합한다.
- 유효 scope 소비 성공을 VFX·SFX·Tracer·피격 신호보다 앞선 최종 gate로 사용한다.
- 마지막 HitIndex 소비 즉시 닫고, 다음 단조 증가 scope에서만 다시 연다.
- 단일·다중 hit, 소모 뒤 반복 Marker, 원거리 Tracer 캡처, 중복·역순 scope, Stop/사망 clear를 production-shaped 회귀로 검증한다.
- Invalid 제거 전에는 direction/timing 잔여를 독립 원인으로 교정하지 않는다.

## Round 6 — 1회성 Impact lease 구현 및 자동 검증

### RED

- 기존 production과 같은 결함 상태에서 단일 hit scope를 한 번 소비한 뒤 `Armed`가 남도록 fixture를 구성했다.
- Unity self-validation이 `A single-hit presentation scope must authorize exactly one marker and close immediately after consumption.`으로 실패하는 것을 확인했다.
- 최초 Unity 실행은 변경 파일 재임포트 전의 오래된 assembly가 PASS를 출력했다. `Assets Refresh` 뒤 새 fixture의 컴파일 오류 두 건을 먼저 수정하고 RED를 다시 확인했다. 이후에는 변경 파일 재임포트 완료를 확인하기 전 PASS를 결과로 채택하지 않는다.

### 구현

- `AttackPresentationImpactLease`를 `Closed/Armed/Consumed` 상태와 유효 HitIndex 범위로 구현했다.
- 마지막 HitIndex 소비 직후 instance·sequence·cursor를 지우고 `Consumed`로 전환한다.
- `UnitView.OnAttackHit`의 네트워크 일반 공격은 유효 scope 소비 성공을 모든 observer·VFX·SFX·Tracer·피격 신호의 최종 선행 gate로 사용한다.
- 원거리 Tracer는 발사 시 소비한 불변 scope 값 복사본을 콜백까지 유지한다.
- Walk/Held/Stop/타겟 변경/suppression/사망/초기화는 남은 lease를 닫는다.
- 싱글플레이 및 BloomFairy 별도 힐 연출과 서버 권위 피해 writer는 보존했다.

### 자동 판정

- Unity Runtime/Editor compile error: 0
- `[UAS-DIAG]`: PASS
- `[C2-AUTHORITY]`: PASS
- `[C3-PRESENTATION-SHADOW]`: PASS
- 문서 정합성 검사: 7종 모두 문제 없음
- Android Build And Run: `Succeeded`, 1,431초(23분 51초)
- AAB: `Build/Hexiege.aab`, 141,976,144 bytes, 2026-08-31 20:30:33

### 다음 실기 gate

동일 경기 Host/Client 양쪽 v4·`PresentationShadow`·combat schema 1·동일 session key를 먼저 확인한다. C2 PASS를 보존하면서 C3 Invalid 0인지 판정하고, 그 뒤에만 unmatched·direction·timing 잔여를 독립 분류한다. 실기 PASS 전 emitter 전환 금지는 유지한다.

## Round 7 — 동일 v5 실기 C3 FAIL과 C3-only 재교정

### 입력

- Editor Host: `Assets/_Project/Docs/_Logs/_editor/2026-09-01/RuntimeLog.txt`
- Android Client: `Assets/_Project/Docs/_Logs/2026-09-01/21_55_logcat/RuntimeLog_device.txt`
- 공통 세션: `fd521f9476114d50473bdbbaf06dcef6d38f50efdcca1ae82a3c25533d611ff4`
- 계약: 양쪽 `observerSchema=c3-authoritative-result-presentation-shadow-v5`, `PresentationShadow`, combat schema 1

### 판정

- 입장 계약: PASS
- C2: PASS — Host 결과 2,828 / 실패 0, Client 수락 2,828 / 거부 0
- B3: PASS 유지 — Host rejected·invalid·gate·writer·Client write·stationary Walk·adapter·drop 0, recoverable repath 29 / repeated 0; Client 복제 2,226건의 invalid·duplicate·revision/scope 오류 0
- ROOT: Host/Client 로컬 PASS, evidence drop 0. peer 교차감사는 별도 gate
- C3 Host: FAIL — unmatched 385(Marker 195 / Tracer 182 / Enqueue 0 / Emit 8), direction 0, timing 112, 최대 timing 1.992135초
- C3 Client: FAIL — unmatched 562(Marker 294 / Tracer 264 / Enqueue 0 / Emit 4), direction 49(same-revision 0 / revision-lag 42 / scope-mismatch 7), timing 38, 최대 timing 1.977195초
- Android 실제 ERROR envelope: C3 `presentation-END` 1건. 비-UAS 미처리 예외·FATAL·ANR·native crash 0

### 원인 분리

1. Host의 Assault `EmittedTimeout`이 약 0.502~0.508초 차이로 반복 FAIL했다. 0.5초 fallback이 실제 marker와 같은 타격 시점 기준으로 채점되는 의미 결합이다.
2. 약 0.7~1.99초 Marker 차이는 timeout 경계와 별개이며, 연속 Attack의 다른 marker occurrence 또는 닫힌 구회차 표현이 결과에 결합되는지 exact scope로 추적해야 한다.
3. Client 방향 49건 중 동일 revision 실제 방향 오류는 0건이다. revision-lag와 scope-mismatch를 실제 시각 방향 오류와 같은 합계로만 판단하면 방향 writer를 잘못 고칠 수 있다.
4. Marker/Tracer unmatched는 scope 0 Invalid 재발이 아니다(`invalid=0`). 취소·타겟 전환·결과 부재 회차의 표현 수명과 exact 결과 결합을 별도로 교정해야 한다.

### 다음 Round C3-only gate

- C2 서버 권위 결과와 B3 이동은 재개봉하거나 재설계하지 않는다.
- Marker/Tracer/결과의 exact scope 수명과 단계별 1회 소비를 보강한다.
- 정상 marker, timeout fallback, target-death/stop 복구 표현의 시간 의미를 분리한다.
- same-revision 방향 mismatch만 실제 표현 방향 실패로 판정하고 lag/scope 수렴 실패는 별도 복구 증거로 닫는다.
- 임계치 상향으로 PASS시키지 않는다.
- C3는 계속 read-only `PresentationShadow`이며 실제 emitter 전환은 금지한다.

## Round 8 — C3 v6 exact-scope 수명·복구 의미 보강

### 구현

- attacker-only 로컬 신호를 exact presentation scope 이벤트로 교체하고 Marker/Tracer/result 양방향 rendezvous를 추가했다.
- 타겟 변경·사망·StopCombat·Held·새 scope 전환에서 이전 sequence를 retire해 늦은 result/timeout 재방출을 차단했다.
- 정상 타격 timing과 timeout/target-death/attacker-stop recovery timing을 분리하고 timeout 발생 자체를 C3 FAIL로 유지했다.
- Client 방향 판정은 분류 시점 최신 상태가 아니라 marker 시점의 불변 복제 revision을 사용한다.
- invalid/capacity fallback은 화면 복구와 구조 실패 판정을 분리했다.
- 서버 피해·HP·타겟 writer, B3 이동 writer, Legacy 단일 emitter는 수정하지 않았다.

### 자동 판정

- Unity compile error: 0
- `[UAS-DIAG]`: PASS
- `[UAS-DIAG][C2-AUTHORITY]`: PASS
- `[UAS-DIAG][C3-PRESENTATION-SHADOW]`: PASS (`c3-authoritative-result-presentation-shadow-v6`)
- Android Build And Run: signing password 미입력으로 중단. 구현/컴파일 실패가 아니며 비밀번호를 자동 입력하거나 문서화하지 않는다.

### 다음 실기 gate

서명 정보를 사용자가 직접 입력해 새 빌드를 만든 뒤 동일 v6 Host/Client 경기만 채점한다. C2/B3 보존과 C3 exact-scope·normal timing·recovery timeout·same-revision direction·replication convergence를 각각 독립 판정한다. 실기 PASS 전 emitter 전환 금지는 유지한다.

## Round 9 — 동일 v6 실기에서 이중 타격 시계 구조 FAIL 확정

### 입력

- Android Host: `Assets/_Project/Docs/_Logs/2026-09-02/13_07_logcat/RuntimeLog_device.txt`
- Editor Client: `Assets/_Project/Docs/_Logs/_editor/2026-09-02/RuntimeLog.txt`
- 공통 세션: `48fddc9131924acca141170c1498a7ebabd2ae899177c7171c6c6c6b5fff8ccf`
- 계약: 양쪽 `observerSchema=c3-authoritative-result-presentation-shadow-v6`, `PresentationShadow`, combat schema 1

### 판정

- 입장 계약: PASS
- C2: PASS — Host 서버 결과 1,714 / 실패 0, Client 수락 1,714 / 거부 0
- B3: PASS 유지 — Host movement reject·invalid·writer 충돌·stationary Walk·fatal repath·drop 0, Client 복제 오류 0
- ROOT: Host 38 / Client 37 stable endpoint와 rotation evidence, 양쪽 로컬 PASS·drop 0
- C3 Host: FAIL — unmatched Emit 1, normal timing 13, 최대 0.938416초, recovery timing 7, 최대 0.934293초
- C3 Client: FAIL — unmatched Marker 1, normal timing 4, 최대 0.708833초, recovery timing 3, 최대 1.498579초, direction 54
- Client 방향 분해: same-revision 실제 방향 실패 0 / revision-lag 35 / marker 시점 증거 부재 19 / scope mismatch 0
- Android 오류 envelope: C3 `presentation-END` terminal ERROR 1건. 별도 FATAL·ANR·미처리 예외·NullReference 0
- 이번 경기의 UnitType 커버리지는 9/25이며, 현재 실패 원인 확정에는 유효하지만 25종 전체 완료 판정에는 사용할 수 없다.

### 확정 원인

서버는 각 결과에 정확한 `ImpactServerTime`을 가지고 있지만 실제 피격 표현은 여전히 로컬 Animation Event가 시작한 트레이서의 도착 콜백과 레거시 큐 flush가 결정한다. 실제 로그에서도 Hitscan `TracerImpact`가 권위 결과보다 약 0.501~0.938초 늦었고, 타겟 사망 flush는 Host 최대 0.934초·Client 최대 1.499초 뒤 보류 표현을 방출했다. 새 C3는 이 어긋남을 정확히 검출했지만 실제 emitter를 소유하지 않으므로 문제를 제거할 수 없다.

또한 Client의 54개 방향 표본에는 같은 revision에서 틀린 화면 방향이 한 건도 없었다. 35건은 결과 revision 5에 대해 marker snapshot revision 3만 도착했고, 19건은 marker 순간의 복제 snapshot 자체가 없었다. 서버 Aim이나 B3 Root writer를 바꿀 근거가 아니라 공격 일정과 함께 exact revision·방향을 전달해야 할 근거다.

### 구조 교정 결정

- 허용 오차를 높이거나 Tracer·TargetDeath·방향 실패에 예외 조건을 추가하지 않는다.
- C1·C2 서버 권위 결과와 B3 writer는 유지한다.
- 서버가 확정한 공격 일정과 정규 결과 키를 입력으로 받는 C3 단일 Authoritative Presentation Coordinator를 만든다.
- Animation Event와 트레이서는 표현 소비자로 낮추고, 공격자 FIFO·timeout 정상 방출·사망 flush는 전환 검증 후 제거한다.
- 상세 계획은 `Assets/_Project/Docs/_Tasks/2026-09-02/15_29_c3-authoritative-presentation-coordinator/`의 Research/Plan을 따른다.
