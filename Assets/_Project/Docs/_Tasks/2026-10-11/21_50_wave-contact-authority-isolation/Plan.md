# 파도 접촉 표현 예외와 서버 권위 처리의 분리 — 계획

파도의 피해·회복 계산은 그대로 두고, 접촉 표현 발행 중 발생하는 예외가 사망·제거와 다른 대상 처리를 끊지 않도록 한다. 정상 경로에서는 같은 불변 snapshot을 같은 시점에 발행하며, 실패한 표현 때문에 gameplay를 취소하거나 다시 실행하지 않는다. [조사](Research.md)의 소스 감사 위험을 예방적으로 격리하는 계획이고 실기 결함 재현·교정 완료 선언이 아니다.

## 2026-10-12 Client clock 교정 기술 계획 — Main 승인/구현 GO 대기

**10_10 programmer 독립 최종 보강:** Spawn465 full-key/시각 및 contact313 양쪽 text/key·기록 정밀도 일치, 양쪽 VFX464 동일keys/기록 충돌0, wave465 양쪽 정상 GameEnd 취소. Server 실HP변화313/산술 오류0은 binary/full snapshot/Client replica HP 증거가 아니다. 실제 오류/예외0과 메서드 시그니처 `Exception` 문자열11014를 구분한다. 이번 전달·표시 scoped PASS만 보강하며 pending 실전 branch 미실행·상위 clock/정밀 시각/build hash OPEN을 유지한다. 상세 raw 행은 [Research](Research.md)에 추가했다.

**2026-10-12 10_10 최신 경기 판정:** Main full-key 대조 완료, device=server/editor=client epoch443cdc5dde2f8a57. Spawn465 전달 delta0, contact publish↔text success313 양쪽unique313/delta0, coordinator failures/channelFailures/dups0. Wave465 game-end expired1은 정상 취소다. 이번 경기 전달·표시만 **scoped PASS**; ClockPending/Recovered/Expired0이므로 수신보류 회복 실전 branch는 **미실행 OPEN**. Ingame clock backward→회복과 disconnect anomaly는 관측되었으나 상위 clock 원인/정밀 시각/global closure 미해결. 상세 모집단·경로·시각·검색 범위는 [최신 Research](Research.md)에 모았다. 이전 real-match 대기는 전달·표시 축만 supersede하며 새 구현/빌드/Task/TC는 없다.

**최종 인계 2026-10-12:** Main source3 final hash 독립 일치·actual CLR RED→GREEN100/100(2.309s)·정적 errors0/warnings20·0·20, Release NOT RUN. Refresh 후 Runtime/Editor DLL02:16:56.962/02:16:57.020 확인 → 새 Unit Action 실행02:19:29/UI02:20:22 전체 PASS(TORRENT-WAVE 포함) → 실제 Root02:22:11 PASS. Console errors0/warnings999+(stress diagnostics); 새 runtime warning0 아님. 약02:23 Build And Run 실제 클릭1회 뒤 모든 UI/log/process 관찰 없음. 완료/설치/runtime 사용자 소유·미확정. 첫02:15 stale-generation PASS는 별도 보존하며 아래 대기 상태는 이력이다. 현재 real-match 재테스트·clock/NGO 원인·정밀 시각/global closure OPEN. 전체 증거와 manifest는 [Research](Research.md)의 fresh 최종 인계를 따른다.

**2026-10-12 약02:14 최신 경계:** Main 최종 source exact3 hash 확인, actual CLR RED→GREEN100/100(2.309s), 정적 errors0/warnings20·0·20, Release NOT RUN. 첫 Unit Action PASS는 이전22:13:40 Library/ScriptAssemblies DLL의 실행이므로 **새 clock source native PASS로 인정하지 않는다**. Refresh/import 후 fresh gate 재실행 및 후속 빌드 결과 대기. 아래 GO 대기·최종 결과 미수신은 이전 단계이며, 상세 귀속은 [Research](Research.md)에 기록한다. 소스/CLR 성공을 native/runtime/NGO 원인 해결로 확대하지 않는다.

확정 contact를 받은 뒤 시계 실패로 버리는 Client 표현 경로를 교정하는 승인 계획이다. 서버 HP/transport는 그대로 두고 유효 snapshot을 bounded ClockPending으로 보류한 뒤 정상 Tick에서 기존 시간 계약을 재검사한다. 아래의 이전 예외 격리 P1–P5 및 native/빌드 결과와 별개인 **clock P1–P4**다. Main은 기술 Plan을 승인했으며 **이 문서 확인 후 programmer 구현 GO를 별도 전달**한다. 현재 코드 적용·새 native/빌드 실행은 없다.

### 재현 근거 및 한계

실제 source Coordinator + contracts/Team CLR **100회, exit0, 187.315ms**(Dev/native Time 없음). Future319.758ms wave79 failures1→회복 Tick 후 VFX0, 정상 동일 payload 재투입 VFX1/duplicate0. Backstep contact3 failures3→text0, 정상 재투입 text3/duplicate0. Receipt Math.Max 대조는 text2만 회복하고 마지막 future256.102508ms reject1이 남았다. Negative controls5/5는 GameEnd spawn 차단/contact 유지, .50 catch-up 만료, baseline, epoch, nonfinite다. contact 소수6자리·대표 position/team 입력이며 binary wire/실제 Presenter/native/NGO root cause 증거가 아니다. 자세한 귀속은 [Research 최신 clock 인계](Research.md#2026-10-12-최신-clock-재현승인-계획-인계--구현-go-전)를 따른다.

### Clock P1. 기존 gate regression RED

2026-10-12 후속 인계: Main이 디스크 Plan을 확인하고 programmer 구현 GO를 전달했다. 위 GO 대기 문장은 전달 전 이력이다. 코드 최종 실행 결과는 아직 인계되지 않았으므로 새 구현/검증 PASS를 기록하지 않는다.

이 절의 규칙 링크: [SYNC](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md), [UNITS](../../../GameSystemRules/GameSystemRules_Units.md), [LOG](../../../LogRules.md). 아래 2초/2.6초 구분은 승인된 구현 계약이며, 규칙 원문에 없는 새 수치를 규칙에 기재된 값으로 주장하지 않는다.

기존 TorrentWave production gate에 future spawn과 backstep contact의 실제 Coordinator 경로 회귀를 먼저 추가한다. 기존 구현에서 시간 회복 후 기존 입력의 VFX/text가 복구되지 않는 RED를 확인하고 기록한다. 위 CLR 재현은 이 예정 gate 실행을 대신하지 않는다. Negative controls 및 .25/.10/.50 계약을 보존한다.

### Clock P2. 정확히 3파일의 bounded ClockPending

P1 근거: SYNC NET-TIME-001~005 및 NET-PRESENT-003(시간축·delay·catch-up·순서·baseline·exact-key 회귀). 예정 RED/교정 결과는 실제 실행 후에만 기록한다.

Coordinator, Presenter, 기존 `Assets/Editor/Tools/TorrentWaveProductionValidation.cs`만 변경한다. 정확한 coordinator/presenter 경로와 최종 hash는 구현 manifest로 인계한다. 저장 전 검사는 payload 자체 유효성·epoch/key/identity·capacity 및 duplicate/conflict를 뜻한다. 시간 문제인 유효 입력만 ClockPending store로 보류한다. **Cross-snapshot contact↔Spawn envelope 검사는 두 snapshot 모두 available인 정상 Tick에서 수행**하며 contact-before-Spawn 입력에 관계 검사를 선행 강제하는 뜻이 아니다. **512/256/8192/4096 capacity 경계**를 fixture에 포함한다. Capacity 초과를 무제한 저장이나 정상 수용으로 숨기지 않는다. 이 표현 재검사는 authoritative result/new HP 적용이 아니다.

duplicate는 최초 receipt deadline을 연장하지 않는다. Conflict/expired key는 재활성화하지 않는다. owner/epoch terminal 전이에서는 pending·관련 소유권 상태를 정리한다. GameEnd spawn pending은 cancel하고 contact는 기존 age 허용 조건을 그대로 적용한다. 서버/transport/bootstrap/pool/HP 및 이전 contact 예외 격리 변경 금지.

### Clock P3. 단일 raw Tick과 receipt 2초 expiry

P2 근거: SYNC NET-AUTH-001/002, NET-PRESENT-003, NET-TIME-004/005, NET-DELIVERY-TRAVELING 및 UNITS 규칙 29~30. Client 보류는 서버 HP·접촉 판정을 다시 수행하지 않으며 정상 confirmed contact의 수신·표현과 gameplay를 분리한다.

Expired ordinal tombstone을 유지하여 같은 owner/epoch의 만료 ordinal을 재수신으로 재활성화하지 않는다. Duplicate는 deadline을 연장하지 않으며 conflict와 capacity 초과도 정상 수용으로 바꾸지 않는다.

- 수신 callback은 high-water를 전진시키지 않는다. clock 판단은 **단일 raw Tick sample**만 사용한다.
- raw 역행/nonfinite에서 입력은 보류하며 정상 Tick에서 재검사한다. payload 시각을 수정하거나 receipt Math.Max를 gameplay clock으로 사용하지 않는다.
- **future .25초 / delay .10초 / catch-up .50초**, duration/baseline/envelope 조건은 그대로 유지한다. 허용치를 높이거나 늦은 표현을 모두 정상 처리하지 않는다.
- **monotonic receipt 기준 2초는 ClockPending expiry 전용**이다. 첫 receipt 기준이며 duplicate로 연장하지 않는다. gameplay/presentation 시각을 이 시계로 대체하지 않는다.
- 정상 Tick 회복, genuine late expiry, GameEnd·epoch·owner terminal, capacity/conflict/expired 재수신을 서로 구별해 검사한다. 표현 재검사는 서버 피해·회복 재적용이 아니다.

### Clock P4. bounded exact-key 진단·실행 인계

P3 추가 계약: receipt 2초 expiry는 **clock 이상 때문에 ClockPending인 입력만** 대상으로 한다. 정상적인 .25초 이내 future due 대기와 기존 **2.6초 slot**은 보존하며 2초 clock deadline으로 단축하지 않는다. Raw Tick 회복 뒤에도 기존 due/duration/.10 delay/.50 catch-up/baseline/envelope 검사를 그대로 거친다.

P3 근거: SYNC NET-TIME-001~005 및 NET-AUTH-002. Receipt 단조 시계는 보류 수명만 제한하고 동기화 server/presentation clock을 대체하지 않는다.

pending/recovered/expired를 구별하고 exact key, receipt/raw/previous/payload, future delta, wait age를 bounded 진단으로 남긴다. 동일 key 중복·충돌·deadline·terminal cleanup 및 capacity 검사를 결과별로 인계한다. 매 Tick 무제한 기록을 추가하지 않는다.

P4 근거: SYNC NET-PRESENT-003 및 LOG §1.8/§1.14. Exact-key 증거와 bounded 진단을 남기되 sink 실패가 gameplay/다른 sink를 끊지 않고 중복·매 Tick spam을 만들지 않는다.

구현 manifest, 실제 regression RED→교정 후 결과, 현재 정적/fixture 실행 범위, Main의 새 native gate와 승인된 빌드 시도를 각각 구별한다. 새 실행이 없으면 NOT RUN으로 남긴다. 2026-10-11 22:16/22:21 native PASS와 약22:22 빌드 클릭은 이번 clock 변경의 검증이 아니며 빌드 완료는 사용자 소유다. 새 runtime/시각·자연 fault 회복/global closure는 주장하지 않는다. TC/QA/새 Task/Git/로그 보관 정책 실행은 이 clock 계획에 포함하지 않는다.

> **2026-10-11 최신 실행 인계:** Main Unit Action22:16:40 KST 전체 PASS(새 TORRENT-WAVE 포함), Root self-validation22:21:12 PASS(Editor.log line2546935); Console errors0/warnings50. 약22:22 Build And Run 한 번 클릭, 이후 완료 감시 없음·완료 사용자 소유. Release NOT RUN·독립 CLR 차단(no PASS) 유지. 아래 구현·정적 단계의 native/빌드 미실행 기록은 당시 이력이며 새 native 실행으로 supersede한다. 실기/global closure는 아니다.

## 2026-10-11 23_16 저장 경기 판정

Main 최종 분석: contact281 unique exact-key 및 로그 contact(6decimal)/hp/applied/heal 일치, duplicate/missing/extra0. Binary float/full snapshot/replicaHP 전체 동등성은 아니다. Server writer1398·양수HP281 처리와 Client clock 거부4(미래 spawn1+backstep contact3)를 분리한다. 실패 contact3 서버 HP 각200damage/apply-return/send 확인으로 서버 피해 손실이 아니다.

양쪽 VFXsuccess271 총수는 wave79/272 key 비대칭을 숨기므로 same-key equality가 아니다. Generic C2/C3 92/failure0 EVIDENCE≠전체 wave PASS. 현재 검색 범위 오류0는 전체 파일/이전20:27 이력 오류0가 아니며 자연 예외 회복 경로 미실행 경계를 유지한다. 이전 P4 native fixture PASS는 유효한 별도 증거다. Clock/scope 원인·정밀 시각·전체 runtime closure OPEN, 후속은 분석 후보만. 정확 원본 paths/행·key·시간·모집단: [Research](Research.md). 새 코드/빌드/old Task/정책 변경 없음.

## 최신 P4·P5 실행 인계 — 2026-10-11 (저장 경기 전 이력)

Main의 sky UI 실제 실행 인계: **2026-10-11 22:16:40 KST Unit Action 전체 gates PASS**, 새 실제 코드의 **TORRENT-WAVE production validation PASS 포함**. **22:21:12 KST Root cross-audit self-validation PASS**, 근거 Editor.log line2546935. Console errors0/warnings50이며 intentional fixture 경고를 포함한다(경고0 증거가 아님). 이는 native 자동 검증이며 새 멀티플레이 실기/정밀 시각/공식 경기 CrossAudit 완료가 아니다.

Main이 약 **22:22 KST File 메뉴 Build And Run을 한 번 성공적으로 클릭**했다. 빌드 시도만 확인했으며 이후 screenshot/log/process/완료 확인은 사용자 지시대로 하지 않았다. 빌드 성공·완료·설치·실기 결과는 미확정이고 완료 확인은 사용자 소유다. Release NOT RUN·독립 CLR Time/UniRx 차단(no PASS)은 그대로이며 로그 보관·정책 변경 없음.

P4는 준비된 source fixture 단계에서 실제 새 TORRENT-WAVE native gate PASS로 진행했다. P5는 native 두 검사 PASS 및 빌드 시작 클릭까지 확인했으며 빌드 완료/실기 수용은 완료 항목이 아니다. 아래 native 대기 및 최초 미실행 단계는 이전 인계 이력으로 보존한다.

## 최종 진행 판정 — 2026-10-11 (native 실행 전 이력)

- Main P1–P5 승인 후 exact2-file GO와 구현 인계 수신. P1–P3 contact clock/진단/개별 subscriber 및 비재귀 기록 격리 적용; gameplay clock과 HP→사망 전 snapshot→사망·제거 순서 유지.
- P4 기존 fixture 확장 적용: callback 관측과 외부 assertion 분리, throwing clock/handler와 healthy handler, lethal removal/multi-victim/damage·heal once 준비. 실제 native fixture 실행은 대기다.
- P5 현재 Runtime/Editor/Development 정적 검사 exit0/errors0, warnings20/0/20 (programmer 보고). Release NOT RUN; CLR Time/UniRx 제약으로 차단, PASS 없음.
- Main 최종 source2 hash 독립 일치 및 부분 소스 검토, Game/Stats 보호 불변 확인. 전체 manifest와 증거 귀속은 [Research 최종 인계](Research.md#최종-구현검사-인계--2026-10-11)에 한 번 기록한다.
- Native/빌드 NOT RUN·대기. 사용자 modal 종료/CU 재시도 승인 후 Main activate timeout 복구 중이며 기존 USER STOP 이력 보존. 새 성공/빌드 완료/실기 해결 판정 없음. 로그 보관·정리 정책 및 old Task 수정 없음.

아래 P1–P5는 승인된 계획과 최초 미실행 단계를 보존한다. 최신 진행 판정은 위 인계이며, 실행되지 않은 검증을 완료 체크로 승격하지 않는다.

## 1. 승인·변경 범위 — 최초 계획 이력

사용자가 새 문서와 후속 즉시 구현·검사·빌드 시도를 승인했다. Main이 이 기술 계획을 검토하여 코드 GO한 뒤 programmer가 적용한다. 현재 문서 작성만 완료하며 아래 항목은 모두 실행 대기다.

변경 예정 소스는 정확히 두 파일이다.

- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.WavePresentation.cs`
- `Assets/Editor/Tools/TorrentWaveProductionValidation.cs`

다른 소스·assets·Stats·scene·clip·VFX·게임 규칙·로그 보관 정책은 변경하지 않는다. 기존 Task도 수정하지 않으며 TC/QA/새 에이전트/Git 작업은 없다. 상위 상태·메모리는 명시적 최종 인계 후에만 동기화한다.

규칙 링크 약칭: [SYNC](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md), [UNITS](../../../GameSystemRules/GameSystemRules_Units.md), [LOG](../../../LogRules.md).

## 2. 적용 항목 — Main 검토 후 실행

### P1. 접촉 발행의 시계 실패만 격리

`PublishWaveContact`의 적용량·key 검사와 snapshot 생성 경계를 보호한다. 정상 시각은 기존 `WaveServerTime`을 그대로 사용한다. contact용 clock throw/비유한 시각은 실패로 기록하고 표현 발행만 종료한다. 대체 시간축·새 delay·HP 재실행은 추가하지 않는다. `TickWaves`의 genuine gameplay clock, Spawn/전선 경과시간·범위·속도·hit-set은 그대로 둔다.

규칙 근거: SYNC NET-AUTH-001/002·NET-TIME-001·NET-DELIVERY-TRAVELING, UNITS 규칙 18·29.

### P2. 진단과 개별 구독자 예외를 분리

contact 진단의 인자 준비와 호출을 보호하여 진단 실패가 유효 snapshot 전달을 막지 않게 한다. 현재 진단 클래스의 내부 catch/fault 집계는 보존한다. 구독자 목록을 발행 경계에서 snapshot으로 잡고 등록 순서대로 각 구독자를 한 번 호출하며 각각 catch한다. throwing 구독자 다음의 healthy 구독자도 같은 contact를 받는다. 구독자 실패 뒤 전체 발행/피해를 재시도하지 않는다.

기존 호출자는 그대로 두어 **HP·HP 동기화 이벤트 → contact snapshot 발행 시도 → 사망 이벤트·제거** 순서를 유지한다. 피해자/시전자 View 조회를 새 필수조건으로 넣지 않는다. 이 격리는 contact 발행의 예외 범위이며 전역 GameEvents, Spawn/Receive/RPC 전체 예외 정책 변경은 아니다.

규칙 근거: SYNC NET-AUTH-002·NET-PRESENT-003·NET-ACTION-IDEMPOTENT, UNITS 규칙 19·26·29·30, LOG §1.8.

### P3. 최종 catch에서 실패를 기록하되 기록 실패도 격리

예외 객체를 가진 최종 catch에서 원인별 Ops 기록을 한 번 남긴다. gameplay가 계속된다는 사실과 해당 표현 채널의 실패를 구분하고, 복구 여부에 따라 LOG §1.2의 심각도를 적용한다. 삼킨 예외의 운영 존속은 §1.3을 따른다. wave key/ordinal/피해자·단계 등 bounded context와 원래 exception을 보존하며 Dev fault 집계도 유지한다. 같은 사건을 상위에서 다시 기록하지 않는다.

logging guard는 재귀적으로 logger를 다시 호출하거나 같은 실패 clock을 재조회하지 않는다. 로그 기록 실패도 권위 처리로 escape하지 않게 한다. 기존 LogEvent/로그 API를 확인해 재사용하며 raw Debug.Log, 무제한 context, 매 frame spam, 새로운 로그 보관/삭제 정책을 만들지 않는다.

규칙 근거: LOG §1.2·1.3·1.4·1.8·1.9·1.14, SYNC NET-AUTH-001/002.

### P4. 기존 production fixture에 회귀 검사 추가

`TorrentWaveProductionValidation`의 callback 내부 `Require`를 관측값 수집 뒤 callback 밖 assertion으로 옮긴다. 예외 격리가 test assertion까지 삼켜 거짓 PASS가 되지 않도록 한다. 기존 실제 SpawnWave/TickWaves 경로에 아래 경계를 검사한다.

- 정상 snapshot 필드·접촉 시각·피해/회복량 및 once-only 유지.
- gameplay clock 읽기는 정상이고 **contact clock 읽기에서만** throw하도록 구분한 시나리오: 현재 HP 적용, lethal unit/building 사망 이벤트·제거, 다음 대상 처리 지속. gameplay clock throw를 숨기거나 fallback시키는 검사로 바꾸지 않는다.
- 첫 구독자가 throw하고 뒤 구독자는 정상인 시나리오: healthy 구독자는 동일 snapshot을 한 번 받고, lethal 제거와 다중 피해자·아군 회복이 계속 진행.
- nonpositive/full-HP heal, invalid snapshot/key, 구독자 없음과 기존 정상 종료/중복 회귀 보존. 실패 표현을 정상 delivery PASS로 세지 않음.
- fixture 구독·주입값·임시 데이터는 finally/dispose로 복원하고 scene save/setup을 호출하지 않음.

진단 내부 catch와 contact 경계 보호의 실행 범위도 보고한다. 추가 fault injection seam이 필요하면 두 파일 안의 최소 private 범위로 제한하고 Main 검토한다. 별도 TC나 새 테스트 실행 체계를 만들지 않는다.

규칙 근거: SYNC NET-AUTH-001/002·NET-PRESENT-003·NET-ACTION-IDEMPOTENT, UNITS 규칙 29·30, LOG §1.8, [WORKFLOW](../../../WORKFLOW.md)의 기존 자동 검증/TC 경계.

### P5. 현행 정적 검사 → Main native 검증 → 빌드 시작 시도

두 파일 적용 뒤 Main이 실제 변경 manifest와 보호 범위를 확인한다. 현재 Unity `6000.3.5f2`에 대응하는 Runtime/Editor/PDev rsp로 검사하며 각 결과·경고·generation을 분리한다. stale Release rsp/과거 DLL은 현재 Release PASS로 쓰지 않는다. 유효한 Release 검사 환경이 없으면 미실행으로 명시한다.

Main은 사용자 직접 로그 저장 modal 해제 후 기존 `Run Unit Action Self Validation`과 `Self Validate Unit Root Pose Cross Audit`를 새로 실행한다. 현재 소스 generation과 gate 결과를 확인한 뒤 승인된 Build And Run을 한 번 시작하고 즉시 관찰한 단계만 기록한다. 빌드 완료·설치·runtime/시각 수용을 추정하거나 완료 polling을 하지 않는다. modal이 남으면 선택을 대신하지 않고 blocked 경계를 보고한다.

규칙 근거: SYNC NET-AUTH-001/002·NET-PRESENT-003, UNITS 규칙 29·30, [WORKFLOW](../../../WORKFLOW.md)의 컴파일 확인·검증·빌드 및 완료 후 인계 경계. 자동 검사 PASS는 실기 재현/시각 수용이나 global migration PASS가 아니다.

## 3. 완료 인계 기준과 현재 결과 — 최초 미실행 이력

현재 결과는 새 Research/Plan 작성뿐이다. 구현·정적/native·빌드 결과는 아직 없다. 최종 인계는 실제 두 파일 변경 목록, source 검토, 예외별 fixture 결과, 현행 정적/native 결과, 빌드 **시도** 관찰과 미검증 축을 따로 적는다. 정상 Torrent 사용자 수용, 이전 6종 개별 수용 및 공통 랠리 후순위는 유지한다.

문서 검사는 `Tools/check_docs.py`로 실행한다. `_Tasks/`는 그 검사 범위 밖이므로 0건은 저장소의 검사 대상 문서에 한정되며, 이 두 문서의 파일 링크·규칙 ID·소스 함수 근거는 별도로 직접 확인한다.
