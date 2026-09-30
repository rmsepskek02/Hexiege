# 공격 결과 표현 만료와 렉 원인 분리 — Plan

먼저 같은 경기의 공격 묶음이 서버에서 언제 완성·발행됐고 Client에서 언제 RPC에 들어와 언제 판정됐는지 연결해 기록한다. 그 시간창의 Client 프레임 간격과 clock anchor를 함께 보면 만료가 수신 전인지 수신 후인지, 렉이 실제로 겹쳤는지 가를 수 있다. **아래 계획 본문은 구현 전 기록이다. 사용자가 승인한 같은 작업의 문서 작성 → 계측 구현 → Unity 검증 → Android 빌드 시도 실적은 문서 끝에 별도로 기록한다. 기존 로직 제거 없음.**

## 완료 조건과 보호 경계

- 목표는 원인 **분리**이지 Client C3 실패를 조용히 0으로 만드는 것이 아니다. 이전 경기 `a2d9c80e143ab3ee03081f3925c936873e23978929e15528ec0b9b2a4c622225`의 7건은 기존 FAIL/OPEN 그대로 둔다. 새 경기에서 같은 증상이 재현되지 않으면 원인 미확정으로 기록한다.
- `OnAttackHit`/클립·설정·공격 주기, 서버 피해 writer, 결과 exact key/묶음 완성·방출 정책, `PresentationDelaySeconds=0.10` 및 `CatchUpSeconds=0.50`을 변경하지 않는다. 관측 코드가 판정 순서·스케줄·RPC reliability/순서에 영향을 주면 안 된다. 근거: `GameSystemRules_Units.md` **규칙 17~19**, `GameSystemRules_UnitCombatSynchronization.md` **NET-TIME-002/003**, **NET-PRESENT-003**.
- 이 승인된 작업에서는 Research/Plan 작성 뒤 원인 분리용 최소 계측을 구현하고 정적·Unity gate를 거쳐 Android 빌드를 시도한다. Testcase·QA 문서, 성능 수정, 새 gameplay 동작은 선행하지 않는다.

## 같은 작업의 구현 항목 — 항목별 권위 근거와 확인 기준

| 단계 / 예정 관찰 지점 | 최소 계측·검증 | 권위 규칙 및 실패 방지 |
|---|---|---|
| 1. 재현 기준 고정: Android Host/Editor Client 동일 세션, LionKnight+LittleKnight 중심의 짧은 경기 | 먼저 기존 로그의 C2/C3/ROOT와 7 `Expired`를 기준선으로 보존한다. 신규 런은 각 peer의 role, sharedSessionKey, run identity, 생산 유형/수, 누락·overflow를 확인한다. 불재현도 그대로 기록한다. | 규칙 19, NET-PRESENT-003: 결과 수와 exact-key 대응 없이 시각적 수용만으로 표현 PASS를 선언하지 않음. 2/25종을 전체 migration으로 확대하지 않음. |
| 2. 서버 완성·발행: `NetworkCombatController.PublishCompletedAttackPresentationBundle` | 묶음의 서버 시각·서버 로컬 monotonic 시각을 **RPC 호출 직전** 캡처하고, 모든 result의 `ImpactServerTime`, due, 완성/발행 상태 및 exact key를 연결한다. 필요하면 진단 전용 publish timestamp를 기존 completed-bundle RPC에 운반하되 gameplay payload와 결과 의미·전송 신뢰성은 불변으로 한다. `publish-due`는 발행 전 지연 판별용이다. | 규칙 18~19, NET-TIME-002, NET-PRESENT-003: 서버 권위 결과와 표현 진단을 분리하며 AoE 묶음/순서·exact key를 보존. 진단 필드가 결과 writer가 되지 않음. |
| 3. Client RPC 진입·Bridge 관찰: `NetworkCombatController.AttackPresentationCompletedBundleClientRpc`와 `UnitAttackResultPresentationShadowBridge.ObserveCompletedBundle` | RPC 진입 즉시 Client local monotonic·추정 서버 시각, Bridge 인입/anchor 갱신 시각을 기록한다. 서버 publish timestamp와의 차이는 **clock-offset을 포함한 추정 지연**으로 표기하고 peer별 local time을 직접 뺀 확정 네트워크 latency로 쓰지 않는다. RPC 진입→Bridge 관찰은 동일 Client clock으로 측정한다. key/묶음 수·role/session 일치와 decode 실패도 집계한다. | NET-TIME-002/003, NET-PRESENT-003: 서버 시간 추정의 한계를 노출하고, 수신 지연과 판정 지연을 혼동하지 않으며 완성 묶음과 exact-key 일치를 검사. |
| 4. Coordinator 판정: Bridge `TickCoordinator`와 `AuthoritativeAttackPresentationCoordinator`의 기존 decision/failure 관찰점 | Client 수신 시점의 `due`, `due+0.50`, 현재 추정 서버 시간과 판정 시각/상태/이유를 같은 key로 잇는다. 수신 직후 이미 만료였는지, 수신 후 Tick까지 늦었는지, 그 사이 anchor가 다른 Result 등으로 갱신됐는지 출처/age를 보존한다. 순수 Coordinator의 판정 조건은 변경하지 않는다. | 규칙 19, NET-TIME-003, NET-PRESENT-003: 0.50 경계와 최대 1회 방출을 유지하고 진단용 관찰이 만료 결과를 Ready로 재분류하지 않음. |
| 5. Client frame gap: `Presentation/Effects/UnitAttackResultPresentationShadow.Update`의 Tick 호출 주변 | `Time.realtimeSinceStartupAsDouble`의 연속 Update 간격을 단순 최대·구간별 집계하고, 실패 주변의 고정 크기 최근 샘플만 보유한다. 수신→판정 구간과 큰 gap의 겹침을 표시하되 FPS 추정과 체감 렉을 원인으로 단정하지 않는다. 무제한 프레임별 로그 금지. | NET-TIME-002/003: 로컬 시간 경과와 동기 서버 시간의 관계를 검증하되 지연/만료 정책 불변. 규칙 18: 프레임 계측이 서버 피해 타이머에 간섭하지 않음. |
| 6. 식별·로그 비용 | 각 실패 detail은 `sharedSessionKey`, role, attacker unit/instance, `AttackSequenceId`, `HitIndex`, victim kind/id, effect kind, result ordinal 등 **실제 결과 exact key 필드**와 due/publish/receive/decision time·clock domain을 담는다. 일반 경로는 카운터·최대/분포 요약만, detail은 실패 전후 고정 상한과 overflow 수만 남긴다. 기존 `GameLog`/RuntimeLogger 규칙에 맞추고 raw `Debug.Log`·무한 ring·프레임별 문자열 생성은 피한다. | 규칙 19, NET-PRESENT-003의 exact-key/중복 방지; `LogRules.md`의 로깅 경계. 과한 진단 로그로 새 렉을 만들지 않는 것이 검증 조건. |

## 검증 순서와 판별 기준

1. **정적 gate:** 계측 구현 후 코드 경계·RPC payload 호환·role 분기, bounded buffer/overflow, 로그의 key·clock domain·session 표기, `0.10/0.50` 및 서버 피해 writer·애니메이션 무변경을 검토한다. 동일 clock-domain이 아닌 timestamp 차이를 확정 지연으로 명명하면 실패 처리한다. 근거: 규칙 17~19, NET-TIME-002/003, NET-PRESENT-003.
2. **Unity gate:** Coordinator의 due 직전/경계/직후, 수신 시 이미 Expired, 수신 후 긴 local frame gap, anchor 갱신, AoE 묶음/중복/overflow를 결정적 입력으로 검증한다. 기존 `Run Unit Action Self Validation`과 Root Pose Cross Audit self-validation은 실제 메뉴 실행 결과를 별도로 기록한다. gate PASS는 기기 경기·공식 CrossAudit PASS가 아니다. 근거: 규칙 19, NET-TIME-003, NET-PRESENT-003.
3. **Android 빌드 시도 계획:** 같은 승인 작업에서 정적·Unity gate 뒤 `File > Build And Run`을 **시도**하고 시작/완료/실패를 실제 관찰대로 구분한다. 빌드 시작 화면만으로 설치·기기 실행을 주장하지 않는다. 기기 실기는 사용자와 같은 세션의 Host/Client 로그가 있어야 분석한다. 근거: NET-TIME-002/003의 양 peer 시간 관측, NET-PRESENT-003의 결과 전달·방출 계약.
4. **실기 원인 분리:** 실패 exact key별 `impact→due→server publish→Client RPC→Bridge→Coordinator decision`을 연결하고, Client 수신 시 overdue 여부·수신 후 local gap·anchor 갱신/오차를 나란히 본다. publish가 늦으면 서버 선행 지연, publish 뒤 Client RPC가 늦으면 전송/Client 큐 구간, RPC 뒤 Tick이 늦고 frame gap이 겹치면 Client stall 가능성, anchor 급변이면 clock 추정 가능성으로 **증거 수준을 표시**한다. 서버↔Client clock offset과 통신 vs 메인스레드 RPC 큐가 분리되지 않으면 `inconclusive`로 남긴다. 기준 0.5초의 적합성은 측정 후 별도 교정 판단으로만 다룬다. 근거: NET-TIME-002/003, NET-PRESENT-003.
5. **판정:** 신규 경기의 C2 양측 대응, Client C3 `Expired`/다른 failure, ready·released bundle 수, ready 범위의 필수 표현, 누락/overflow, 사용자 육안·렉 보고를 각각 보고한다. 실패가 0인 한 경기라도 원인 확인 없이 이전 7건을 소급 PASS 처리하지 않는다. local ROOT와 공식 CrossAudit, 2/25 범위와 전체 roster/역할교대/rollback도 구분한다. 원인과 인과가 충분히 분리된 뒤에만 별도 수정 작업을 제안한다. 근거: 규칙 19, NET-PRESENT-003, NET-TIME-003.

## 2026-09-30 구현·검증·빌드 시도 실적

위 표와 검증 순서는 **작성 당시 계획**으로 남긴다. 같은 승인 작업에서 원인 분리용 계측을 구현하고 Unity self-validation 세 가지를 실행했으며 Android Build And Run을 시도했다. 이는 **계측 준비 상태**이지 C3 만료의 원인 확인이나 버그 수정 완료가 아니다.

| 계획 항목 | 확인된 구현 상태 |
|---|---|
| 서버 발행·Client 수신 | `NetworkCombatController.cs`: 완료 묶음 RPC에 진단용 publish 서버/로컬 시각을 동봉하고 Client RPC 수신 시각·프레임을 수집한다. 기존 Reliable 전송은 유지한다. |
| clock anchor·판정 연결 | Application `UnitAttackResultPresentationShadow.cs`: anchor source/revision 읽기 경계 추가. `UnitAttackShadowObserver.cs`: exact-key arrival 최대 512개, 최근 frame gap 128개, `Expired` 상세 최대 16건(`coordinator-expiry-timing-A/B`)과 overflow 집계. 수신→판정·anchor 정보를 기록하되 cross-peer 시간 차이는 추정치로 표기한다. |
| 프레임·결정적 gate | Presentation `UnitAttackResultPresentationShadow.cs`: 프레임별 로그 문자열 없이 로컬 시각 숫자를 수집한다. Editor `RunAuthoritativeAttackPresentationValidation.cs`: 0.5초 정확한 경계, 발행 시 이미 만료, 수신 후 판정 만료를 구분하는 결정적 gate 추가. |

제공된 Unity 실행 기록: **13:02:10 Unit Action self-validation PASS**, **13:02:53 ROOT self-validation PASS**, **13:04:05 Authoritative Attack Presentation self-validation PASS**(2026-09-30); Console 오류 0. 각 self-validation 결과는 생산 실기나 공식 CrossAudit Analyze의 증거가 아니다. `File > Build And Run` 직접 클릭 후 **Detect Java Development Kit (JDK) / Checking Java Development Kit** 진행 화면까지 관찰했다. 여기서 추적을 중단했으므로 빌드 완료·기기 설치·실행은 미확인이다.

**⚠️ 미완:** 변경 후 Android Host/Editor Client 동일 세션 실기가 없어 새로운 `coordinator-expiry-timing-A/B` 자료를 아직 분석하지 못했다. 이전 Client C3 7 `Expired` 및 LionKnight task **FAIL/OPEN**은 그대로다. 심한 렉의 원인, 렉과 만료의 인과, clock skew와 0.5초 정책의 적합성, 정확한 시각 접촉은 모두 미확정이다. 실제 계측 근거를 얻은 뒤 원인을 분리하고 별도 교정 여부를 판단한다. 피해 writer·공격/애니메이션·기존 `0.10/0.50`·Reliable 경계는 이번 계측에서 변경하지 않았다. Testcase·QA 문서, 상시 문서, 메모리는 이 기록 단계에서 수정하지 않는다.

**이번 구현 변경 파일(제공된 작업 범위):** `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`, `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs`, `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`, `Assets/_Project/Scripts/Presentation/Effects/UnitAttackResultPresentationShadow.cs`, `Assets/_Project/Scripts/Editor/Combat/RunAuthoritativeAttackPresentationValidation.cs`. **이번 문서 반영 파일:** 이 폴더의 `Research.md`, `Plan.md` 두 개뿐이다.

## 2026-09-30 사용자 실기 결과 — 재현 0건 / FAIL·OPEN 유지

기존 `⚠️ 미완` 문단은 **실기 전 시점의 기록**이다. 이번 `a81345042552685a831405817424d1a4b7e2260e56708f4e6f8febb7c3aacb5b` 경기에서는 역할이 **Editor Host / Android Client**였고 Host 생산은 LionKnight 8·LittleKnight 22기(2/25종)였다. Host/Client 모두 C3 schedules 226·results/ready 216·pending/failures 0, 216/216 묶음 방출 및 필수 표현 188/188이었다. Host C2 서버 결과 216/실패 0과 Client 수락 216/거부 0이 일치했고, Client 만료 진단은 216건 기록·overflow/pending 0, **Expired 0**이다. 공간 불일치 0, 양측 local ROOT PASS·오류 0이다. Host MOVE 64,671프레임에서 adapter failure·stationary Walk 0, 공간 전이 326/326; Client 복제 276샘플 invalid 0, transport gap/order violation 0. Host 종료 시 재접속 WARN 1건과 Android Unity ERROR/FATAL 0은 C3 만료로 분류하지 않는다. 사용자는 큰 육안 문제를 보지 못했다고 보고했다. 상세 근거 경로와 집계표는 같은 task Research의 새 실기 절에 둔다.

**⚠️ 남은 gate:** 이전 경기 Client `Expired` 7건은 소급 해소되지 않았다. 이번에는 만료가 없어 실패 전후 `coordinator-expiry-timing-A/B`가 발생하지 않았으므로 원인 분리용 시간 비교 자체가 불가능했다. 과거 심한 렉의 재현·프레임 시간도 이번 사용자 보고에서 평가되지 않았다. 따라서 원인 확인·근본 수정·LionKnight 정확한 접촉·공식 CrossAudit·25종/역할교대/rollback 검증은 **OPEN**, task 판정도 **FAIL/OPEN**이다. 다음 재현 시 동일 session/exact key의 발행→수신→판정과 frame gap/anchor를 대조한 뒤 별도 교정을 판단한다. 이 문서 갱신에서 코드·Testcase·QA 문서를 만들거나 수정하지 않는다.

## 2026-09-30 사용자 판정 분리 — 진단 OPEN 유지

최신 `a8134504…c3aacb5b` 실기와 육안 수용으로 **LionKnight `0:18`·`1:13` focused 교정은 PASS/CLOSED**다. 위 FAIL/OPEN 기록은 판정 분리 전 상태와 이 진단 task를 가리킨다. 이전 `a2d9…c622225` Client C3 `Expired` 7건은 원인·수정이 미확정인 **별도 결함 OPEN**이며, 진단 task도 OPEN이다. 다음에 재현될 때 exact key별 발행→수신→판정·frame gap·clock anchor를 비교한다. 무만료 한 경기로 결함 해결이나 정확한 접촉, 공식 CrossAudit, 전체 migration을 선언하지 않는다.
