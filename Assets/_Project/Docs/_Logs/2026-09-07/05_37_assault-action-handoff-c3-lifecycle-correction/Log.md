# QA-Fix 반복 로그 — 어설트 공격 진입 멈춤과 C3 종료 수명 교정

## Round 1 — 2026-09-07 05:37

### [QA] FAIL

- 동일 세션: Android Host / Editor Client `3a3e92c1d440168db5bf377933e14eb038b21d7edfdcec53a5467b02c3665ccc`
- 사용자 육안 관찰: Assault가 이동 중 적이 공격 사거리에 닿을 때 잠깐 멈춰 보임
- 코드 원인: `TargetAcquirePriority + NoIntent`가 일반 Held 표현 정책을 타면서 provisional Attack 전에 Walk 첫 자세를 정지시킴
- Assault 표본: `AwaitingStationarySample 40.780°` → `Misaligned 15.056°` → `Ready 0.423°` → commit, 최초 표본부터 commit까지 약 0.297초
- C2: 서버 결과 2,591건/실패 0, Client 수락 2,591건/거부 0
- B3: reject·invalid·writer 충돌·stationary Walk 위반·fatal repath 0
- ROOT: 양측 로컬 PASS
- C3 허위 실패: 생산 142개 = 유효 retire 111개 + 무공격 `Retire(None)` 실패 31개
- C3 실제 비교 실패: Host timing mismatch 8, unmatched emit 2, 방향 증거 오류 1; Client revision-lag 45, 방향 증거 부재 26

### [DEV] 교정 계획

- Walk 에셋은 보존한다.
- `TargetAcquirePriority`를 일반 Held와 분리해 `Walk → Attack` 직접 handoff를 만든다.
- 위치 정지·Action 회전·provisional Attack의 서버 권위 순서는 유지한다.
- Coordinator의 `Retire(None)`은 정상 no-op으로 맞춘다.
- 전용/전체 self-validation PASS 뒤에만 Build And Run을 시작한다.

### [DEV] 구현 및 자동 검증

- `TargetAcquirePriority + NoIntent`는 일반 stationary Held 대상에서 제외했다. 이동은 즉시 멈추되 Walk 첫 자세를 정지시키지 않고 provisional Attack으로 직접 전환한다.
- `UnitView`와 `UnitMovementAuthorityObserver`가 동일한 `ShouldHoldWalk` 판정을 사용하도록 유지해 표현과 진단의 의미가 갈라지지 않게 했다.
- 유효한 공격 표현 인스턴스가 한 번도 없었던 유닛의 `Retire(None)`은 정상 no-op으로 처리했다. 실제 유효 ID의 충돌·용량 초과 fail-closed 규칙은 유지한다.
- 회귀 검증에 `TargetAcquirePriority` 직접 handoff와 `111 valid retire + 31 Retire(None)` 경기형 표본을 추가했다.
- Unity 컴파일: PASS — 최신 강제 동기 컴파일 및 도메인 재로드 구간에 C# 컴파일 오류 없음.
- `Hexiege/Diagnostics/Self Validate Authoritative Attack Presentation`: PASS.
- `Hexiege/Combat/Run Unit Action Self Validation`: PASS.
- 사용자 실기기 재검증 전 상태이므로 이 Round의 최종 QA 판정은 아직 확정하지 않는다.

## Round 2 — 2026-09-07 09:43

### [QA] 부분 PASS / C3 표현 FAIL

- 동일 세션: Editor Host / Android Client `a13eb957a9d5ad7afc930e8a5a6e00735dc965a9b1b39c54d6924e08428dd898`
- Round 1 교정 확인: B3 reject·invalid·gate·writer·stationary Walk 실패 0, 공간 commit 1,193/1,193. C3 coordinator 양측 `failures=0`, `ready=2587`, `pending=0`으로 `Retire(None)` 허위 실패 제거 확인.
- C2/ROOT: 서버 결과 2,587/실패 0, Client 수락 2,587/거부 0, 양측 ROOT PASS.
- 사용자 육안 FAIL: 2단계 물정령 `StreamSpirit`의 공격 VFX가 보이지 않음.
- 에셋 감사: UnitEffectConfig → EffectPreset_StreamSpirit_Attack → vfx_streamspirit_attack 연결 정상.
- 구현 원인: StreamSpirit은 `Unresolved`라 정규 scope가 생성되지 않는데도 `impactEnabled=true + default scope`가 전달됐다. `OnAttackHit`이 scope lease 소비 실패로 VFX 호출 전에 반환해 기존 Legacy VFX까지 차단했다.
- 남은 실제 C3 비교 FAIL: Host direction 2/timing 14/unmatched emit 2, Client direction 67(revision-lag 50/scope mismatch 1/evidence invalid 16)/timing 1.
- Android 비-UAS 크래시·ANR·미처리 예외 0.

### [DEV] 다음 교정 계약

- `Supported`와 `Unresolved`의 표현 방출 정책을 분리한다.
- `Supported`는 유효 scope 1회 소비 gate를 유지한다.
- `Unresolved`는 C3 비교에서 격리하되 Legacy VFX·SFX·Tracer를 보존한다.
- 가짜 scope 발급이나 fail-closed 완화로 우회하지 않는다.
- 서버 피해·사거리·타겟·B3 Root writer는 변경하지 않는다.
- StreamSpirit형 회귀와 Supported scope fail-closed 회귀를 모두 자동 검증한 뒤 재빌드한다.

### [DEV] 구현

- 공격 표현 허가를 bool 조합에서 상호 배타적인 `Suppressed / Scoped / LegacyFallback` 3상태로 변경했다. 이로써 `impactEnabled=true + default scope`라는 모순 상태를 생성할 수 없게 했다.
- 서버는 25종 공통 공격 프로필의 지원 상태와 실제 scope 유효성을 함께 사용해 표현 모드를 결정한다. 유닛 이름에 따른 예외 분기는 추가하지 않았다.
- `Supported`는 유효 scope를 lease가 소비한 marker만 방출한다. scope 유실·소모·기본값은 계속 fail-closed다.
- `Unresolved`는 정규 scope를 만들지 않고 기존 Legacy VFX·SFX·Tracer를 보존한다. 기존 audit eligibility 격리를 그대로 사용하므로 C3 supported invalid 분모에는 들어가지 않는다.
- `Suppressed`는 provisional/커밋 전 marker를 계속 차단한다. Stop·타겟 변경·사망·이동 전환은 lease와 표현 모드를 함께 닫는다.
- 25종 manifest 전체에 대해 지원 상태별 모드 선택을 검사하고, StreamSpirit 형태의 default scope Legacy 방출과 Supported 무효 scope 억제를 같은 회귀 묶음에 추가했다.
- 최신 Android Development와 Editor 대상 Roslyn 정적 컴파일: 오류 0. 최종 소스 재검사도 `COMPILE_EXIT=0`. 기존 NGO obsolete 및 미사용 필드 Warning만 유지.
- 문서 정합성 검사 7종: `DOCS_EXIT=0`, 문제 0건.
- 실제 Unity 메뉴 self-validation과 Build And Run은 아직 미실행이다. 기존 Unity PID는 보존했다. MCP Unity 포트 8090은 Unity PID가 listen하고 TCP 연결도 수락하지만 WebSocket handshake와 메뉴 요청에 응답하지 않아 자동 실행이 불가능했다. Unity 재시작 후 전용/전체 self-validation PASS를 확인한 뒤 Build And Run을 시작한다.

## Round 3 — 2026-09-07 20:00

### [QA] 비교 Coordinator 정상 / C3 실제 표현 FAIL

- 같은 경기: Editor Host / Android Client, `sharedSessionKey=209be7cc0eeab2810994f220837e6f6b5e15602f10d507c1d3834b0a6b0bfc36`.
- 근거: `_Logs/_editor/2026-09-07/RuntimeLog.txt`의 `coordinator-END`~ROOT END(9000~9005), `_Logs/2026-09-07/20_00_logcat/RuntimeLog_device.txt`의 같은 terminal(71464~71504). 상시 Editor 로그는 날짜 내 여러 경기가 있으므로 session key로 구분한다.
- C2 Host 서버 결과 1,964건/실패 0, Client 수락 1,964건/거부 0. Host targetMismatches=0. B3 MOVE failure=0. 양측 ROOT 로컬 PASS(Host endpoint 43, Client 44); 별도 CrossAudit PASS로 확대하지 않는다.
- 양측 Coordinator: schedules=2010, results=1964, ready=1964, pending=0, failures=0, duplicates=0, retired=111. Host comparedLegacy=1602/maxLegacyTimingDelta=0.823814, Client 1671/0.771315.
- 양측 actualEmitter=Legacy, coordinatorSchema=c3-coordinator-comparison-v1, aoeCompleteManifest=false, gameplayWrites=0, presentationWrites=0. 이는 비교 입력 수렴 증거이며 시각 표현 교정 완료가 아니다.
- BUG-C3-TOKEN-TIMING: Host normal timingMismatch=20/maxTimingDelta=0.923348, recoveryTimingMismatch=5/maxRecoveryDelta=0.923814, unmatchedEmit=4. Host에서도 발생해 Client 네트워크 지연만으로 설명할 수 없다. 레거시 marker/tracer/flush 방출 경계의 실제 타이밍 문제를 후속 교정한다.
- BUG-C3-TOKEN-EVIDENCE: Host directionMismatch=2는 evidence invalid=2이며 sameRevision=0. Client directionMismatch=56은 revision-lag=43/evidence invalid=13/sameRevision=0. 동일 Impact revision의 실제 서버 조준 실패가 입증된 것은 아니며 표현 토큰의 방향 증거 수렴 문제로 분리한다.
- Client normal timingMismatch=3/maxTimingDelta=0.804798, recoveryTimingMismatch=2/maxRecoveryDelta=0.871315, unmatched=0. Host recovery는 TargetDeath 165/timeout 0, Client는 TargetDeath 131/AttackerStop 1/timeout 0.
- 전체 C3 terminal은 양측 FAIL이다. 9/25 타입(Assault, DustSpirit, EmberSpirit, FlameSpirit, LittleKnight, Pistoleer, SpearMan, StreamSpirit, TideSpirit)만 포함하므로 전체 유닛 완료 판정 불가.
- Android 종료 시 Lobby 404는 전투 외 정리 순서 문제로 별도 분류한다. C3 정상으로 숨기거나 타격 실패의 원인으로 단정하지 않는다.

### [DEV] 후속 계획

- `_Tasks/2026-09-07/20_24_c3-authoritative-presentation-token-correction/Research.md`와 `Plan.md`에서 공격 일정·선택적 marker·확정 결과 토큰의 소유권과 종료 순서를 구체화한다.
- 서버 피해 단일 writer와 기존 에셋을 유지한다. Host 실제 타이밍과 Client 방향 증거는 각각 회귀 gate를 갖는다. 허용 오차 상향·결과 폐기·진단 분모 제외로 FAIL을 제거하지 않는다.
- 새 Task의 구현·자동 검증·빌드·실기 결과는 아직 이 Round에 기록되지 않았다.

## Round 4 — 2026-09-09 00:40

### [QA] C2·B3 PASS / C3 최종 표현 FAIL

- 동일 세션: Editor Host / Android Client, `sharedSessionKey=afc490a9d344ef1dd2ea827a5eeea872fca2b049fc621bb043b6961c6534138d`.
- 양측 `ResultPresentation`, `c3-authoritative-result-presentation-shadow-v6`, `c3-complete-bundle-presentation-v2` 일치.
- C2: 서버 결과 863건/실패 0, Client 수락 863건/거부 0.
- C3 결과 전달: 완료 묶음 863건 모두 ready/release, pending·duplicate·transport failure 0, AoE complete manifest 확인.
- C3 실제 표현: 대상 800건 중 Host 747건 표시/53건 `viewUnavailable`, Client 737건 표시/63건 `viewUnavailable`. 양측 모두 `INCONCLUSIVE`이며 완료 기준상 PASS가 아니다.
- B3: 서버 98,252프레임, 공간 계획/커밋 714/714, stationary Walk·반복 재탐색·fatal·동일 프레임 retry·writer 충돌 0. Client 복제 561건 오류 0.
- ROOT: 양측 로컬 PASS. 추적 상한 64 경고가 있어 79개 전체 전수 교차 PASS로 확대하지 않는다.
- 관측 타입은 Assault, BoulderSpirit, EmberSpirit, FlameSpirit, LittleKnight, Pistoleer, SpearMan, StreamSpirit의 8/25다.
- Android 경기 중 전투 ERROR/FATAL 0. 종료 뒤 Lobby RemovePlayer 404 1건은 전투와 분리된 미해결 정리 문제다.

### [DEV] 확정 원인과 교정 방향

- `AttackResultPresentationInput`이 피해자 타입·팀을 갖지 않아 C3 adapter가 별도 `OnEntityDamaged` 캐시와 현재 Domain/View 조회를 필수로 사용한다.
- 실제 멀티플레이에서는 HP 동기화와 결과 묶음 순서가 바뀌고 치명타 뒤 View가 제거되므로, 결과가 완전해도 최종 표현 준비가 실패한다.
- 기존 자동 검증은 피해 이벤트를 먼저 발행해 캐시를 보장했으므로 실패 순서를 검증하지 못했다.
- 새 Task에서 결과를 self-contained 표현 스냅샷으로 확장하고, HP 텍스트·피격 VFX를 현재 View와 분리한다. View 펀치는 동일 수명이 확인될 때만 선택적으로 적용한다.
- 실제 실패 순서 회귀가 RED임을 먼저 확인하고 구현 뒤 PASS, 전체 UAS PASS인 경우에만 Build And Run한다.

## Round 5 — 2026-09-09 10:08

### [QA] C1/C2/B3 표본 정상 / C3 transport FAIL

- 동일 세션: Android Host / Editor Client `dae3d6225be0b847d0dcbc41d6527364253a9001b278a72714dac570b17ea755`.
- 양측 `ResultPresentation`, `c3-authoritative-result-presentation-shadow-v7`, `c3-complete-bundle-presentation-v3` 일치.
- C2: 서버 결과 1,251건/실패 0, Client 수락 1,251건/거부 0.
- C1 표본: 9/25 타입, 정렬 전 Legacy 시작 0, 상관 실패·타겟 불일치·production gate invalid 0. 25종 전체 완료로 확대하지 않는다.
- B3: 서버 56,131프레임, 공간 계획/커밋 947/947, reject·invalid·writer 충돌·stationary Walk·공간 commit 실패 0. 양측 로컬 ROOT PASS이며 CrossAudit 전체 PASS로 확대하지 않는다.
- C3: Miss 계열 완료 묶음 192건만 양측에 도달했다. 적중 결과 1,059건은 Host에서 `server-completed-bundle-publish-rejected`로 거부됐고, Host terminal `transportFailures=1059`, Client 시각 결과 0건이다.
- Android의 별도 미처리 예외·크래시·ANR은 0이며 화면 ERROR 1건은 위 C3 terminal FAIL이다.

### [DEV] 원인과 교정 계약

- 피해 fact에는 실제 대상 위치가 있었지만 C2 결과 생성기가 `HasImpactPosition=false`로 고정했고, 완료 묶음 주 결과가 fact 위치를 연결하지 않았다.
- 서버 피해 observation이 실제 Impact 위치를 소유하고 C2 결과가 이를 처음부터 보존하도록 교정한다. C3는 주 fact와 결과의 위치까지 일치 검증한다.
- 현재 모든 `hitPreset` 미설정은 정상 `SkippedNoAsset`이다. 미래 에셋을 위해 스냅샷과 VFX 경로는 유지하되, 설정된 프리셋의 실제 재생 실패만 `ChannelFailure`로 판정한다.
- 사망 VFX와 서버 피해·사거리·타겟·B3 writer는 변경하지 않는다.

### [DEV] production 데이터 흐름 교정

- 서버 피해 writer의 `AttackDamageObservation`이 주 피해자의 타입·팀·Impact 위치를 피해 적용과 같은 호출에서 고정해 소유하도록 변경했다. 사망 이벤트가 Domain/View를 제거하기 전에 캡처하며, 누락된 스냅샷은 더 이상 보조 fact가 보완하지 못하고 fail-closed한다.
- C2 `AttackImpactResult`는 observation의 실제 위치를 그대로 소유한다. C3 조립기는 C2 주 결과·주 피해 fact·writer observation의 위치·피해량·결과 HP·타입·팀이 모두 일치할 때만 완료 묶음을 전송한다.
- 양수 피해 fact가 없는 `StatusEffectApplied`도 writer observation의 스냅샷으로 독립형 표현 입력을 만들 수 있게 했다.
- 선택 피격 VFX는 `configured / emitted / skippedNoAsset`로 분리했다. 미설정은 정상 생략이고, 설정된 채널의 실제 방출 실패만 기존 `ChannelFailure`로 전체 판정을 실패시킨다. 사망 VFX는 변경하지 않았다.
- 관측 스키마를 v8로 올렸다. Runtime·Editor·Android Development 대상 정적 컴파일 오류 0, 문서 정합성 검사 문제 0건이다.
- Unity 에디터 메뉴 서버가 WebSocket 연결을 수락한 뒤 응답하지 않는 상태라 실제 두 메뉴 self-validation과 Build And Run은 아직 실행하지 못했다. 이 항목은 연결 복구 후 PASS 로그와 빌드 시작으로 갱신한다.
