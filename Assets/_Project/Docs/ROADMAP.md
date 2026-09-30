# Hexiege - 작업 로드맵

**SpearMan 0:25 현행 후속(2026-09-30): focused 실기 PASS/CLOSED, `MigrationRequired` 별도 OPEN.** 사용자 육안 수용. 두 역할 교대 경기(`39af4c6d…52373a` Editor Host/Android Client, `9b0404e3…3a4f` Android Host/Editor Client)에서 SpearMan 47+28기를 실제 생산했다. LittleKnight도 각각 48/31기 생산한 **2/25종 혼합 경기**이며, 경기 전체 Host 결과 597/298·실패 0과 Client 수락 597/298·거부 0, 양쪽 C3 ready·필수 표현 597/597·474/474 및 298/298·271/271·실패 0, local ROOT PASS, Host MOVE adapterFailures 0이었다. 자동 검사 두 메뉴도 앞서 PASS했다. 이로써 SpearMan focused 시각 교정은 닫지만, 정확한 marker→권위 피해 offset·공식 CrossAudit Analyze·나머지 23종·Legacy rollback/v2 migration은 열어 둔다. 과거 LionKnight C3 만료 7건·LittleKnight MOVE 4건도 별도 OPEN이다. 아래 SpearMan 자동 검사·저장 문단은 당시 이력이다. 상세: `_Tasks/2026-09-30/18_02_spearman-attack-timing-correction/{Research,Plan}.md` 최신 절.

**SpearMan 0:25 이전 단계(2026-09-30, 자동 검사 전): 저장·정적 확인 / Unity·실기 대기, focused OPEN.** 생산 Attack의 단일 `OnAttackHit`와 type 4 `hitFrameTimes`가 30fps `0:25 = 25/30 ≈ 0.8333333초`로 저장됐고 cooldown 2초는 유지됐다. Game 씬 type 4 명시 clip 연결·영구 production gate 코드와 StatsReference `0:25(2:00)`도 확인했다. **당시 다음 단계는 Unity 재컴파일·Unit Action/Root Pose Cross Audit 두 self-validation의 실제 실행 결과 확인**이었다. 그 뒤 자동 검사 결과는 위 현행 문단을 따른다. 당시 빌드·설치·경기·육안 결과는 없었으며 PASS/CLOSED가 아니었다. 투사체·hit VFX 없음과 `MigrationRequired`, 공식 CrossAudit·25종/역할교대/Legacy rollback 미검증을 유지한다. 아래 LionKnight 문단의 “SpearMan 후보·Task 미확정”은 SpearMan 선택 전 당시 이력이다. 상세: `_Tasks/2026-09-30/18_02_spearman-attack-timing-correction/{Research,Plan}.md`.

**LionKnight focused 현행 판정(2026-09-30): PASS/CLOSED.** 사용자 수용 `a8134504…c3aacb5b` Editor Host/Android Client LionKnight 8기·LittleKnight 22기(2/25종) 혼합 경기 전체에서 C2 216/216, C3 결과·ready 216/216·실패 0·묶음 방출 216/216·필수 표현 양측 188/188, Client `Expired` 0, 양쪽 local ROOT PASS였다. **별도 OPEN:** 이전 `a2d9…c622225` Client C3 `Expired` 7건의 원인은 계측 후 한 경기에서 재현되지 않아 미확정이며 `12_37_attack-presentation-expiry-diagnosis`에서 다음 재현 때 발행→수신→판정·frame gap·clock anchor를 비교한다. 정확한 접촉/피해 offset·공식 CrossAudit·25종/역할교대/Legacy rollback/v2 완료와 LittleKnight 과거 MOVE 4건도 남는다. 다음 교정 후보 SpearMan은 검토 중일 뿐 이 작업의 구현·확정 Task가 아니다. 아래 LionKnight FAIL/OPEN 문단은 첫 경기 당시 이력이다.

**LionKnight focused 후속(2026-09-30, FAIL/OPEN):** 30fps `0:18`·`1:13` Attack marker와 type 22 설정·Game 명시 연결·영구 gate는 저장됐으나 `a2d9c80e…c622225` Android Host/Editor Client의 LionKnight+LittleKnight 2/25 경기에서 Client C3 `Expired` 7건이 발생했다. Host C2/C3는 224/실패 0, Client C2는 224 수락/거부 0이지만 C3 ready·방출은 217/224이며 필수 표현 192/192는 **ready 범위만**이다. 첫 만료는 LionKnight UnitId 2, 나머지 유형 미확정. 육안상 큰 문제 없음과 심한 렉은 사용자 보고이고 FPS/frame-time 미계측으로 원인 불명이다. **다음 gate는 만료의 원인 분리·교정 후 같은 범위 재검증**이며, 정확한 접촉 시점·공식 CrossAudit·25종/역할교대/Legacy rollback/v2 Complete와 기존 LittleKnight MOVE 4건은 계속 OPEN이다. `_Tasks/2026-09-30/01_21_lionknight-attack-timing-correction/` 최신 절 참조.

**BearGuard focused 판정(2026-09-30):** 0:13 타격은 사용자 육안 수용과 `b986fbbc…61d7d94b` Host/Client 2/25 혼합 재테스트의 경기 전체 결과 641/641·양쪽 필수 표현 608/608·실패 0, local ROOT PASS로 **focused PASS/CLOSED**다. 첫 경기 ROOT INCOMPLETE는 생산 전 관찰 종료로 별도 이력이다. 후속 OPEN은 BearGuard 단독 정밀 marker→피해 offset, 공식 CrossAudit Analyze, LittleKnight 기존 Host MOVE adapterFailures 4, 설계 주기 `1:20` 대 실제 cooldown `1.2초`, 25종/역할교대/Legacy rollback/v2 Complete다. 아래 “실기 전” 문단은 당시 단계 기록이다. `_Tasks/2026-09-29/23_28_bearguard-attack-timing-correction/` 최신 절 참조.

**BearGuard 이전 단계(2026-09-29, Unity 두 메뉴 PASS / 실기 전):** 30fps `0:13`의 단일 Attack marker·type 20 설정 `0.43333334초`, Game 씬 명시 Attack 연결, 영구 production gate를 반영했다. 재컴파일 후 Unit Action self-validation(23:43:36)과 Root Pose Cross Audit self-validation(23:45:06)이 각각 PASS했다. 승인된 `File > Build And Run` 클릭 뒤 `Checking prerequisites / Starting Android build` 화면까지만 확인했다. **빌드 완료·설치·기기 실기·화면 판정은 아직 미확인**이며 다음 gate는 사용자 소유 실기와 그 근거의 구분된 평가다. 설계 주기 `1:20 = 1.666666…초`와 보존한 실제 cooldown `1.2초` 차이, LittleKnight 이전 MOVE 4건과 공식 CrossAudit·25종/역할교대/rollback은 계속 OPEN. `_Tasks/2026-09-29/23_28_bearguard-attack-timing-correction/` 최신 절 참조.

**BearGuard 이전 후속(2026-09-29, 구현 저장 직후):** 당시 Unity 메뉴·Build And Run은 아직 미실행이었다. 위 2026-09-29 문단은 그다음 당시 단계이며, 현재 focused 판정은 2026-09-30 최상단을 따른다.

**TideSpirit 최신 판정(2026-09-29, focused 시각 PASS/CLOSED):** 새 `ae04f2a9…e6bd03` Editor Host/Android Client 혼합 경기에서 TideSpirit 32기·LittleKnight 12기 생산, 경기 전체 결과 Host/Client 160/160·필수 표현 양쪽 129/129·실패 0, Host MOVE adapterFailures 0·양쪽 ROOT local PASS를 확인했다. 사용자는 Tide 타격을 육안상 문제없다고 수용했다. Tide 단독 정밀 marker→피해 offset은 미계측; 공식 CrossAudit·25종 전체·역할교대·Legacy rollback은 남는다. LittleKnight 과거 이동 4건은 이번 미재발만으로 해결되지 않아 별도 OPEN이다. 아래 Tide “실기 OPEN”은 실기 이전 상태 이력이며 이제 현행 후속은 미검증 전체 gate와 LittleKnight 별도 이동 문제다. `_Tasks/2026-09-29/19_09_tidespirit-attack-correction/` 최신 절 참조.

**TideSpirit 이전 후속(2026-09-29, 구현·Unity PASS / 실기 전):** 사용자가 저장한 30fps `1:15 = 1.50초` 단일 marker에 맞춰 type 16 설정과 Game 씬 명시 Attack 연결·영구 생산 gate를 적용했다. Unity 두 self-validation은 재컴파일 후 PASS했다. Build And Run은 `Detect Java Development Kit(JDK)` 진행까지만 관찰했고 **당시 완료·설치·새 혼합 경기·사용자 육안 판정은 미확인**이었다. 당시 다음 판단은 사용자 TideSpirit 시각 결과와, 같은 경기 비교 유닛 LittleKnight의 Host MOVE-AUTH `adapterFailures`/`Unreachable` 관찰을 구분해 반영하는 것이었다. 이전 LittleKnight 4건 FAIL은 원인·수정 미확정 OPEN; 이 과정은 공식 cross-audit·25종 전체·역할교대·rollback을 대체하지 않는다. 아래 Boulder 문단의 “다음 작업 미확정/TideSpirit 후보”는 TideSpirit 선택 **이전 이력**이다. 근거: `_Tasks/2026-09-29/19_09_tidespirit-attack-correction/` 최신 절.

**BoulderSpirit focused 완료(2026-09-29, 1:10):** 생산 Attack marker·type 14 fallback·영구 gate를 30fps `1:10 = 40/30초 ≈ 1.3333333초`로 맞추고 Unity 두 self-validation을 통과했다. 새 `0f0bbb32…923eb60` Android Host/Editor Client 경기에서 Boulder 7기 생산, **Boulder+LittleKnight 혼합 2/25종 경기 전체** 결과 Host/Client 283/283·양쪽 필수 표현 265/265·실패 0, Host MOVE adapterFailures 0·양쪽 local ROOT PASS였고 사용자가 새 타격을 **“육안상 문제없었어”**라고 수용했다. 따라서 Boulder 공격 시각은 **focused PASS/CLOSED**로 완료 이력에 둔다. 단독 marker→피해 정밀 offset과 공식 cross-audit·전체 25종·역할교대·Legacy rollback은 미완이다. 아래 1:15(1.50초) 사용자 수용·453/414는 이전 버전 이력, 그 경기의 LittleKnight MOVE-AUTH 4건은 이번 미재발만으로 해결되지 않아 **별도 OPEN**이다. **다음 작업은 미확정:** 기록된 LittleKnight 이동 실패 진단을 우선 검토할 근거가 있고, 미교정 TideSpirit 같은 유닛별 공격 교정도 후보이나 사용자 선택 전 새 계획·Task로 확정하지 않는다. Build And Run 완료 관찰은 주장하지 않는다.

**BoulderSpirit 후속(2026-09-29):** 30fps `1:15`=1.50초 교정·생산 Attack 연결·영구 gate 후 사용자 화면에서는 약간 늦지만 수용 가능하다. 동일 경기 `06d034ab…e1a6c4`의 **BoulderSpirit+LittleKnight 합계** 공격 결과 Host 453/실패 0 ↔ Client 453 수락/거부 0, 양쪽 필수 표현 414/414·실패 0이다. Boulder 단독 정확한 1.50초·subframe은 미계측. **별도 우선 후속:** Host LittleKnight UnitId 16/19/21/26 `post-combat-no-safe-forward-center`의 MOVE-AUTH adapterFailures 4/FAIL을 진단·재경기한다. FPS/frame-time 계측이 없어 화면 지연 원인으로 연결하지 않는다. 전체 25종·역할교대·Legacy rollback 및 공식 cross-audit는 계속 OPEN. 기존 09-28 후보·상태 문단은 당시 이력이다.

**EmberSpirit 후속(2026-09-28):** 실제 Attack 명시 연결·production gate 및 두 Unity 메뉴 PASS 뒤 새 빌드 실기에서 EmberSpirit 총 52기가 생산됐다. 동일 Editor Host/Android Client 두 경기의 **경기 전체** 결과 Host 299/231·실패 0 ↔ Client 299/231 수락·거부 0, 양 peer 필수 표현 264/264·196/196·실패 0이며 사용자가 육안으로 수용했다. **Ember focused PASS/CLOSED**로 완료 이력에 옮긴다. Client WARN 188건은 0으로 처리하지 않는다. 정확한 animation→피해 subframe, 공식 CrossAudit, 25종 전체·역할교대·Legacy rollback은 남는다. 다음 교정 유닛은 아래 매트릭스의 미교정 후보 중 사용자가 선택하기 전까지 확정하지 않고 새 Task도 만들지 않는다.

**RabbitTrickster 현재 상태(2026-09-28 marker 후속 교정):** 생산 Attack의 단일 `OnAttackHit` 0:20(0.6666667초), type 25 설정·영구 검증 기대값, 명시 Attack 연결을 맞췄다. 새 Editor Host/Android Client 동일 경기 `3fc75ef4…b7beded`에서 Rabbit 각 15기·LittleKnight 각 13기가 생산됐고, 경기 전체 Host 138 schedules/129 results/실패 0 ↔ Client 129 수락/거부 0, 양쪽 필수 표현 122/122·실패/중복/전송 실패 0이었다. 사용자 육안 수용으로 **Rabbit 명시 연결·타이밍 focused PASS/CLOSED**다. 경기 전체 수치는 Rabbit 단독 결과가 아니며 정확한 animation→권위 피해 offset 미계측, 공식 Root Pose CrossAudit 미실행이다. 이전 `7d4f85a6…e2654a5` 집계는 0.18초 이력으로만 보존한다. 이 focused 작업은 완료로 옮기되 다음 교정 유닛은 사용자가 선택하기 전까지 지정하거나 새 Task를 만들지 않는다. Fox 재튜닝 보류·Dust 육안 최종 수용 OPEN·전체 25종/역할교대/Legacy rollback/C3 migration 미완 및 별도 성능 렉은 그대로다.

> **아래 RabbitTrickster 현재 단계 문단은 0.18초 버전 당시 기록이다. 현행값·다음 단계는 위 후속 교정 문단을 따른다.**

**RabbitTrickster 현재 단계(2026-09-28): 구현 완료 / Unity 자동 검증 PASS / 사용자 확인 빌드·실기 수행 / 타격 시각 focused OPEN.** 실제 Game UnitFactory Rabbit 행의 Attack 명시 연결과 production·Attack3-first gate 추가는 메인 patch 검토 및 두 Unity 메뉴 PASS 확인을 마쳤다. 메인의 Build And Run 시작 화면 확인 후 사용자가 빌드 완료와 Editor Host/Android Client 실기를 확인했다. 동일 세션 `7d4f85a6b980b02035e532b36068e5ca349ae35610294a423d3a4ebe2e2654a5`의 경기 전체 집계는 Host 223 schedules/212 results/0 failure, Client 212 accepted/0 rejected·필수 표현 199/199이다. 사용자는 공격 애니메이션에 비해 Rabbit 피해 적용이 약간 빠르게 보인다고 관찰했지만 실제 접촉 프레임과 선행 시간차는 미계측이다. **다음 단계:** 생산 Attack 클립의 실제 접촉 프레임을 측정하고 같은 조건의 피해 적용 시점과 대조한 뒤 교정 필요 여부를 판단한다. 현행 2초/0.18초·Supported 상태는 유지하며 타이밍 변경은 아직 정하지 않는다. 이전 검증·빌드 시작 이력과 최신 실기 분석은 Task에 보존한다: `_Tasks/2026-09-28/10_03_rabbittrickster-explicit-attack-clip-binding/`. Fox 재튜닝 보류·Dust 육안 최종 수용 OPEN·전체 migration 미완은 그대로다.

**이전 후보 추천(2026-09-28, 구현 전 이력): RabbitTrickster.** melee이므로 준비되지 않은 projectile 에셋 없이 조사할 수 있다. 메인 세션 에셋 확인 인계에 따르면 실제 Attack motion은 `8d21000f295b9774a9cb2540a71b8ce7`(RabbitTrickster_Attack.anim, 2초·OnAttackHit 1개), 함께 등록된 Attack3는 `1b667bee5c36e85459f1dd2b5fbd9ef9`(1.1초·이벤트 없음)이며 Game 씬 type25에 attackTimelineClip 명시가 없다. Matrix의 복수 Attack 순서 위험과 맞고 Dust의 명시 연결 접근을 검토할 근거다. **구조적 순서 의존 위험이지 실기 오선택이 관측된 버그가 아니다.** 다음 승인 뒤 실제 연결·추출 경로부터 조사하며 이번에는 구현/새 Task를 시작하지 않는다. Fox 재튜닝은 발사체 구현 시점으로 미루고 Dust 육안 최종 수용은 별도 OPEN으로 유지한다.

**최종 수정일:** 2026-09-28

**현재 상태(2026-09-28):** Fox 표시 지연 focused 교정은 사용자 수용, 현행 타이밍 유지·재튜닝은 발사체 구현 시점으로 이관. Dust 49기 focused 로그는 CONDITIONAL PASS/OPEN(육안 최종 수용 대기). 상세 증거는 기존 Fox Task §21 및 Dust Task 재테스트 결과 참조; 전체 migration 완료 아님.

**이전 증거:** 09-27 시점 분리·17_21 지연 분석과 09-28 자동 검증 직후 상태는 Fox Task §18~20에 보존한다. 현재 판정은 위 요약을 따른다.


**이전 순서(2026-09-27):** Dust 새 빌드 실기를 기다리던 단계는 09-28 focused 로그 확보로 진전됐다. 육안 최종 수용과 전체 25종·역할교대·rollback은 남는다.

**Fox 후속:** 사용자 수용으로 표시 지연 focused 교정은 마무리하고 projectile/tracer 에셋 준비 전 재튜닝은 보류한다. 이전 분석과 판정 변경은 기존 Task에 보존한다.

**현재 최우선 게이트 (2026-09-22):** InfernoSpirit focused 이동·VFX 교정은 사용자 실기와 Host/Client 실제 VFX 64/64 일치로 PASS/CLOSED됐다. 다음은 아직 교정하지 않은 유닛을 사용자와 선택해 같은 production 에셋→타임라인→자동 gate→focused 실기 순서로 진행한다. 별도 P0인 전체 25종·Host/Client 역할교대·Legacy rollback 통합 회귀 전에는 InfernoSpirit `LegacyFallback` 제거 또는 ActionSequence 전체 완료를 선언하지 않는다.

**현재 최우선 게이트 (2026-09-20 BattleAxe):** production marker/config를 1.02초로 정렬하고 영구 Unity gate를 통과했다. Editor Host + Android Client focused 로그는 결과 420/420·필수 표현 440/440·AoE bundle 420/420·공간/이동/ROOT 오류 0으로 수렴했고 사용자 육안에서도 큰 문제는 없어 보였다. 다만 게임 렉 때문에 정확한 1.02초 접촉과 Sweep 아군·후방·범위 밖 제외를 확정하지 못해 **CONDITIONAL PASS / OPEN**이다. 렉은 이번 교정과 분리한 후속 성능 결함으로 두고, 다음 unit-by-unit 교정을 계속한다. 이번 4/25종 범위를 전체 roster·역할교대·Legacy rollback 완료로 확대하지 않는다.

**현재 최우선 게이트 (2026-09-14 QuakeSpirit):** production marker/config 1.667초 정렬과 Supported 승격 뒤 Editor Host + Android Client focused 로그에서 QuakeSpirit 10기, 결과 770/770·실패/거부 0, 양쪽 필수 표현 909/909, 직접 200·스플래시 100, 이동·복제·ROOT 오류 0을 확인했다. 최신 Android 실행의 U+27F3 경고도 0이다. 다만 사용자의 화면 판정이 없으므로 정확한 내려치기 프레임, 아군·범위 밖 제외, 체감 장시간 정지 없음과 기존 비교 유닛 육안 회귀는 남아 있다. 현재 판정은 **CONDITIONAL PASS / OPEN**이며 이번 4/25종 범위를 전체 roster·역할교대·Legacy rollback 완료로 확대하지 않는다.

**현재 최우선 게이트 (2026-09-14):** RhinoBreaker marker 교정은 Blue/Red 유닛 공격과 Blue 건물 공격 로그에서 결과·표현·이동 오류 0으로 **CONDITIONAL PASS**다. 다음 통합 빌드에서 실제 접촉 시점·개별 대상 전환과 특수문자 제거가 반영된 `새로고침` 문구를 최종 확인한다. 그와 병행해 아직 교정하지 않은 유닛을 한 종류씩 계속 진행하되 다음 유닛은 사용자 승인 전 확정하지 않는다. 전체 25종·Host/Client 역할교대·Legacy rollback은 유닛별 교정 뒤의 최종 통합 회귀로 유지한다.

**최우선 게이트 (2026-09-13):** production typed target payload seam 교정은 이전 실패와 동일한 9/25종 범위의 Editor Host + Android Client 실기에서 PASS했다. 이전 근거리 6종 handoff 실패 87건은 0건이 됐고 C2 결과·C3 필수 표현·대미지 위치·ROOT도 오류 0을 유지했다. 9/25는 의도적 동일 조건 비교 범위이며 현재 Task의 미완료가 아니다. 다음 P0는 전체 25종·Host/Client 역할교대·Legacy rollback 통합 회귀다.

**이전 실패 기준선 (2026-09-01):** 동일 v5 Editor Host·Android Client 경기에서 C2 결과 2,828건과 B3 이동·복제는 오류 0으로 PASS를 유지했지만 C3는 Host unmatched 385·timing 112, Client unmatched 562·timing 38로 FAIL했다. Client 방향 49건은 same-revision 0, revision-lag 42, scope-mismatch 7이므로 방향 writer를 재설계하지 않는다. 이 실패는 후속 C3 및 공격 진입 교정의 출발점으로 보존하며, 현행 단계는 위 2026-09-13 게이트를 따른다.

> **이 문서의 역할: 앞으로 해야 할 작업.** 완료된 항목은 이 문서에 남기지 않는다.
> 완료 이력은 [WORK_HISTORY.md](WORK_HISTORY.md), 현재 상태는 [PROJECT_STATUS.md](PROJECT_STATUS.md).

**최우선 작업:** 아직 교정하지 않은 다음 유닛의 production 타임라인을 같은 fail-closed 절차로 교정한다. QuakeSpirit·RhinoBreaker·BattleAxe의 남은 정밀 육안 항목은 성능 결함이 분리된 뒤 통합 재확인한다.
**현재 단계:** QuakeSpirit·RhinoBreaker·BattleAxe는 자동 또는 focused 멀티 로그 축의 CONDITIONAL PASS이며 정밀 육안 항목 때문에 OPEN이다. B3 이동, C2 결과, C3 결과 기반 표현과 production 공격 진입 seam의 집중 PASS를 유지한다. 전체 25종 역할교대·Legacy rollback 통합 회귀 전에는 전체 roster PASS나 ActionSequence 완료로 일반화하지 않는다.
**작업 이력:** [WORK_HISTORY.md](WORK_HISTORY.md) 참조

> B2 PASS는 당시 10°/15° 일반 정렬 계약의 이력이다. v2.1은 이를 안전 fallback으로 재분류했으므로 B2 및 현재 B3 정확성 로그만으로 연속 이동 완료를 선언하지 않는다. 공격 방향·Impact·피해 시점과 ActionSequence 전체 권위 전환도 계속 미완료다.
**2026-09-03 갱신 — 무작위 맵 작업이 3단계로 갈라졌다.** 종전에는 무작위 맵 구현이 우선순위 표에 **한 행**으로만 있었는데, **1단계(타일 상태 계약 전환)가 구현·에디터 실기 검증까지 끝나** 그 행의 범위 서술이 더는 맞지 않게 됐다. 그래서 완료된 1단계는 표에서 내리고, 남은 일을 **2단계(맵 생성기 5종 · 검증기 · 폴백)** 와 **3단계(AI 배치 후보 판정 등 잔여 전환 · 맵 전송 · 재경기)** 두 행으로 나눴다. **무작위 맵 트랙의 다음 단계는 2단계**이며, 현재 진행 중인 유닛 전투 교정 트랙을 대체하지 않고 병행한다. 3단계는 2단계가 만들어 낼 타일 종류가 있어야 결과가 달라지므로 그 뒤에 한다.

---

## 우선순위 요약

| 우선순위 | 작업 | 카테고리 | 예상 규모 |
|---------|------|---------|---------|
| 🟡 중간 (**여전히 미검증** — 2026-08-24 (3차) 갱신) | **전역 훅의 엔진 오류 수집(B) 실기 확인** — 2026-08-19 회차에 엔진 오류가 **나지 않아** `[ERROR]`·`UnhandledEngineError`·`Suppressed=` 가 전부 0건이었고, **커밋 `55d24d83` 검증에서 3경기를 더 돌렸는데도 여전히 0건**이다(1408줄 기준). **훅이 동작한다는 증거가 아직 없다.** **⚠️ 역설이지만 기록해 둔다 — B 로 잡으려던 대상이 바로 그 RPC 에러였고, `55d24d83` 의 가드 수정이 그 에러를 없앴다. 즉 수정이 잘 돼서 검증 기회가 사라졌다.** 이제 *"우리가 만들지 않은 엔진 오류"* 가 우연히 나는 회차를 기다려야 한다. 다음에 엔진 오류가 나는 회차에서 로그 파일에 `Event=UnhandledEngineError` 줄이 남는지, `Suppressed=` 가 함께 찍히는지 확인한다 **[🔴 2026-08-24 3차 재확인 — 원문은 그대로 두고 덧붙인다: `bcf45ec1` 검증 세션에서 **3경기를 더 돌렸는데도 `[ERROR]` 가 13,003행 전체에 0건**이라 이번에도 **잡을 대상이 없었다.** 누적하면 2026-08-19 회차 + `55d24d83` 3경기 + 이번 3경기로 **연속 0건**이다. *수정이 잘 돼서 검증 기회가 사라지는 역설*이 계속되고 있으며, **훅이 동작한다는 증거는 여전히 없다.** ⚠️ 이 세션은 **클라이언트 쪽 로그를 처음 수집한 회차**라 표본 성격이 달라졌는데도 0건이었다는 점만 추가된다]** | QA | 소 |
| 🟡 중간 (**미검증** — 2026-08-24 등록) | **`_combatStopped` 재경기 리셋의 실기 확인** — 커밋 `55d24d83` 의 리셋(`NetworkCombatController.cs` **205행 `OnNetworkSpawn`** · **311행 디스폰**)이 2026-08-19 에 「재경기 2회 연속 통과」로 확인됐으나, **2026-08-24 세션으로는 재확인되지 않았다.** 가드가 **380행 `if (!IsSpawned \|\| !IsServer \|\| _combatStopped) return;`** 이라 **2·3경기처럼 에디터가 클라이언트(`IsServer=False`)면 `_combatStopped` 를 평가하기 전에 `!IsServer` 에서 반환**된다. 실제로 2·3경기의 사망 로그는 전부 `EntityDiedClientRpc 수신 → 클라 처리` 경로였고(`서버: 유닛 사망` **0건** · `서버 측 … 이벤트 구독 완료` **0건**) **상대 호스트의 틱이 돈 것**이다. 2026-08-24 실측 내역 — 1경기(서버) 유닛 사망 129 · 건물 5 / 2경기(클라) 수신 64 → 유닛 62 · 건물 2 / 3경기(클라) 수신 179 → 유닛 175 · 건물 4. **확인 조건: 에디터가 호스트로 연속 2경기.** ⚠️ *"판정 로직은 **그 로직이 실제로 실행되는 조건까지** 확인할 것"*(`.claude/MEMORY.md` MistShrine 교훈 ①)의 같은 함정이다 | 검증 | 소 |
| 🟢 낮음 (**미착수** — 2026-08-24 등록) | **`_baseline.json` 의 `measured_at` 을 `--update-baseline` 이 갱신하지 않는다** — 갱신 시 `folders` 만 바뀌고 `measured_at` 은 그대로라, **다음 갱신 후에는 *"언제 실측한 값인가"* 가 거짓이 된다.** 방금 고친 **드리프트(ⓑ)와 정확히 같은 성격**이고, 위 항목의 「하드코딩된 위치·상태 참조」 4건과도 같은 부류다. 오늘(2026-08-24) 갱신했고 오늘 날짜와 우연히 일치하므로 **지금은 맞다** — 다음 갱신 때 어긋난다. ※ 문서 에이전트는 `Tools/check_docs.py` 와 `_baseline.json` 을 직접 편집하지 않는다 | 도구 | 소 |
| 🟢 낮음 (범위 밖 — 후속 후보, 2026-08-20 등록) | **`ProductionTicker` 에 종료 가드 추가** — `Presentation/Production/ProductionTicker.cs` **238행 `Update()`** 에 종료 가드가 없다. 246행의 유일한 분기는 *"멀티 클라이언트면 스킵"* 뿐이라, 위험 구간에서 **생산 틱(260행)과 수입 틱(265행)이 정상 속도로 돈다.** **길목으로는 이번 8곳보다 근본적**이지만 `Presentation` 레이어이고 틱 전체를 멈추는 것은 **동작 변경**이라 별도 설계 판단이 필요하다 | 코드 정리 | 중 |
| 🟢 낮음 (범위 밖 — 후속 후보, 2026-08-20 등록) | **`NetworkGameEndController` 재경기 3종(`_localRematch*`)의 가드** — `ServerRpc` 3종(`RequestRematchServerRpc`·`AcceptRematchServerRpc`·`DeclineRematchServerRpc`)이고 **`IsServer` 블록 밖에서 구독**되어 클라이언트에서도 살아 있다. 따라서 필요한 가드는 **`!IsSpawned` 만**으로 이번 8곳과 **형태가 다르다.** 위험 구간(수십 ms) 안에 사용자가 재경기 버튼을 눌러야 발동하므로 실현 가능성도 사실상 없다 | 코드 정리 | 소 |
| 🟢 낮음 (범위 밖 — 후속 후보, 2026-08-20 등록) | **`ServerRpc` 계열 전반의 종료 가드** — `NetworkProductionController` · `NetworkBuildingController` · `NetworkUpgradeController` · `NetworkSkillController` 등 대부분 진입부 가드가 없다. **호출 주체가 UI 입력**이라 *"이벤트 구독으로 자동 실행되는 경로"* 를 본 이번 조사와 **성격이 다르다.** 별건으로 다뤄야 한다 | 코드 정리 | 중 |
| 🟢 낮음 (범위 밖 — 후속 후보) | **`NetworkUnit.SetAnimState` 의 `IsSpawned` 가드** — 현재 `if (!IsServer) return;` 만 있어 `IsSpawned` 를 보지 않는다. 애니메이션 상태 쓰기의 **더 근본적인 자리**지만 다른 파일이라 2026-08-19 작업에서는 `NetworkCombatController.SetUnitAnimState` 쪽에서 막았다. **디스폰 후 NetworkVariable 쓰기가 실제로 오류를 내는지 자체가 미확정**이라 착수 전 그 확인이 먼저다. **[2026-08-20 재확인]** 여전히 `NetworkUnit.cs` **173행 `if (!IsServer) return;`** 뿐이다(선언 170행). 이번 전수 보강에서도 **의도적으로 제외** — `.SetAnimState(` 호출부가 `NetworkCombatController.SetUnitAnimState` **한 곳뿐**이고 그 상위가 이미 `!IsSpawned` 로 막혀 있어 **중복**이기 때문이다 | 코드 정리 | 소 |
| 🟡 중간 (미완 · [6] 통과 후) | **`SetCameraStartPositionForTeam` 주석 처리분 최종 삭제** — 호출부 0곳으로 확인되어 `GameBootstrapper.Setup.cs` **537행**에 비활성화 블록으로 남아 있다(파일 헤더 목차 14행에 `— 제거 예정` 병기). WORKFLOW.md [4] 대로 **사용자 테스트 통과 후 · 문서 업데이트 전**에 삭제한다 | 코드 정리 | 소 |
| 🟡 중간 (**여전히 미검증** — 2026-08-19 갱신) | **`NetworkUpgradeController` 구독 누락 수정의 실기 확인** — 커밋 `da5eeaab`. **2026-08-19 회차에서 정상 경로는 로그로 확인되었으나**(착수 6 ↔ 완료 브로드캐스트 6 1:1, `Level=1`→`2`) **원래 버그의 원인인 스폰 레이스가 발생하지 않아**(`NetworkControllerSpawnedWithoutGameServices` **0건**) **지연 구독 경로 자체가 필요해진 상황이 없었다.** 즉 확인된 것은 **「버그가 재현되지 않았고 정상 경로는 확인됐다」** 까지다. **회선 상태에 좌우되는 간헐 버그**라 재현이 사실상 불가능하다. 로그에 *"스폰 시점에 IGameServices 를 얻지 못했다"* 가 **`NetworkUpgradeController` 이름으로** 찍힌 판에서 연구 완료가 정상일 때만 직접 증거가 된다. **"안 났으니 고쳐졌다"로 결론 내리지 않는다** | QA | 소 |
| 🟢 낮음 (미확인) | **`Presentation/UI/LobbyUI.cs` 가 현재 쓰이는지 확인** — 로비가 `LobbyRootView` 계열로 재구성됐는데 이 파일은 옛 구조 그대로다. 이관 작업에서는 **확인 없이 판단하지 않고 10건 전부 이관**했다(CLAUDE.md 규칙 10·12). 쓰이지 않는다면 죽은 코드 정리 대상 | 코드 정리 | 소 |
| 🟢 낮음 (사용자 판단 대기) | **`_Logs/_editor/` 의 `.meta` 취급 규정 명문화** — `.gitignore` 는 이미 `LogRules.md` 1.10 규정에 부합한다. 남은 것은 `.meta` 를 커밋할지에 대한 **규정 공백**이다. 사용자가 커밋 `23a8da06` 에서 `.meta` 를 실제로 커밋했으나, **그 관찰만으로 규정이 확정된 것으로 적지 않는다**(CLAUDE.md 규칙 10) | 문서/규정 | 소 |
| 🟢 낮음 (범위 밖으로 미룸) | **`FileSink.EditorLogsRootRelativeToAssets` 접근 수준 조정** — 현재 `private` 이라 `LogcatCapture.cs` 가 같은 경로 문자열을 **복제**하고 「FileSink 와 동기화 필요」 경고 주석을 달아 두었다. `internal const` 로 올리면 복제를 없앨 수 있다 | 코드 정리 | 소 |
| 🔵 초기 정상·지속 관찰 중 | 매치메이킹 404(호스트 결정 단계) 수정 — 호스트 결정을 매치 결과 조회(P2P 클라 404) → Lobby CreateOrJoin 원자 선점(A방식)으로 전환 (2026-07-17, 커밋 `a3dbc73`). 초기 실기 정상, 간헐 버그라 지속 멀티 실기 검증 필요 | QA/버그 | 중 |
| 🔴 P0 | 서버 권위 Unit ActionSequence 통합 회귀 — B3 이동·C2 결과·C3 표현·공격 진입 seam의 집중 PASS를 보존하면서 전체 25종, Host/Client 역할교대, Legacy rollback을 검증한다. 이 gate 전 ActionSequence 전체 완료·Legacy 제거 선언 금지 | 전투/네트워크 | 특대 |
| 🟠 CONDITIONAL PASS / OPEN | QuakeSpirit production marker/config 1.667초 정렬·Supported 승격 뒤 Android focused 로그 PASS — 결과 770/770, 필수 표현 909/909, 직접 200·스플래시 100, 이동·복제·ROOT 오류 0. 정확한 화면 타격 프레임·아군/범위 밖 제외·체감 정지·비교 유닛 육안 회귀 대기 | 애니메이션/전투 | 중 |
| ✅ Tracer A0 | SpearMan schedule/dispatch Shadow — Host 204/204·고유 204·누락/중복/타겟/facing 불일치 0. Client는 header only이며 실제 피해 결과 검증이 아님 (2026-07-22) | 전투/계측 | 중 |
| ✅ Tracer A1 | Pure Application UnitAction 계약+stateful reducer — C# 9/Application·Editor compile PASS, Unity Editor 메뉴 PASS, reflection Validate* 10 PASS, Standards/Spec P0~P3 0 (2026-07-22) | 아키텍처/전투 | 중 |
| ✅ Tracer A2 | Server-authoritative pose seam shadow — Host 완료 회차 상관관계 누락·중복 0, attacker-dead 2건 DeadTerminal, capacity eviction·예외 0, Client observer 0. 기존 피해·RPC·VFX 권위 유지 (2026-07-27) | 전투/네트워크 | 대 |
| ✅ Tracer B0 | Visual Root migration readiness — 50/50 read-only 감사, errors 0, mutation/assetsModified 0, 연속 실행 aggregate manifest 동일 (2026-07-27) | 에셋/검증 | 중 |
| ✅ Tracer B1 | 50개 프리팹 Simulation Root / Visual Root migration·rollback과 Android/Editor 역할교대 양방향 NetworkTransform 이동 pose 검증 PASS. Match A/B pose mismatch 0. 공격 방향·Impact·피해 시점은 범위 밖 | 전투/네트워크 | 대 |
| ✅ Tracer B2 | 서버 이동·SimulationFacing pure reducer + read-only Shadow — Android/Editor 역할교대, 25/25종·Blue/Red 증거, 최종 오류 카운터 0, 손실 없는 manifest/SHA 검증 PASS. 실제 writer 전환은 아님 (2026-08-03) | 전투/네트워크 | 대 |
| ✅ 집중 PASS / 통합 회귀 유지 | Tracer B3 v10 중심 이동·건물 안전 Chase·전방 중심 복귀 — Android Host MOVE EVIDENCE, 중심 766/최대 오차 0, Chase 806/2,977, 오류 0, 양쪽 local ROOT 53/53 PASS. 긴 Host terminal 절단 채점기 교정과 25종·반대 역할·Legacy rollback은 다음 통합 회귀에 포함 | 전투/네트워크 | 특대 |
| ✅ 집중 PASS / 통합 회귀 유지 | Tracer C1 연속 Attack·독립 회차 결속 — 최신 Editor Host/Android Client에서 예약 TargetId·sequence·결과 불일치 0, Client 4,863건 수락·거부 0. 표시 타겟 수명주기 오탐을 observer v5로 교정하고 Unity self-validation PASS. 25종·반대 역할은 후속 새 빌드 통합 회귀에 유지 | 전투/네트워크 | 특대 |
| ✅ 8/25종 집중 PASS / 통합 회귀 유지 | Tracer C2 Impact·결과·연속 표현·사망 future-hit 취소 — Android Host/Editor Client에서 서버 결과 2,182건·사망 115건, 과거 핵심 오류와 결과/타겟/gate 실패 0. MOVE 62,434 frame 및 양쪽 ROOT PASS. 나머지 17종·반대 역할·Legacy rollback은 단일 emitter 전환 전 통합 회귀에 유지 | 전투/네트워크 | 특대 |
| ✅ 동일 범위 PASS / 통합 회귀 유지 | Tracer C3 결과 기반 표현 + 공격 진입 typed target payload seam — 최신 Editor Host/Android Client 동일 9/25종 범위에서 handoff 87→0, C2 2,703/실패 0, C3 2,410/2,410, 위치 mismatch 0. 전체 25종·역할교대·Legacy rollback은 다음 P0 통합 회귀 | 전투/네트워크 | 특대 |
| ✅ 설계 완료 | 유닛 이동·공격 규칙 v2 일괄 개정 + 25종 공격 에셋 감사. 런타임 완료를 의미하지 않음 (2026-07-20) | 설계/문서 | 대 |
| 🔴 P0 | AttackTimeline 잔여 교정 — QuakeSpirit 1.667초와 BattleAxe 1.02초 교정은 자동·focused gate CONDITIONAL PASS. 기본 Attack marker 미연결 잔여(MushroomBomber, BloomFairy), Inferno·Stream 등 설정/클립 불일치, 복수 Attack 클립 선택 순서 제거 | 애니메이션/전투 | 대 |
| 🟡 후속 분리 | **인게임 심각한 렉 성능 결함 조사** — BattleAxe 통합 육안 테스트에서 사양만으로 단정하기 어려운 심한 렉이 관찰됐다. 사용자 지시로 유닛 타임라인 교정과 분리해 나중에 별도 Task로 재현·계측·원인 분석한다. 현재 원인은 미확정이며 이번 BattleAxe Task에서는 성능 수정에 착수하지 않는다 | 성능/진단 | 대 |
| 🔴 P0 | 전투 결과 단일 writer/emitter 복구 — `ApplyDamageToVictim`과 Quake의 `ApplyFixedDamageToVictim`을 ActionSequence 결과 적용기로 수렴 | 아키텍처/전투 | 중 |
| ✅ 완료 | 코드 리팩토링 7개 그룹 전체 | 아키텍처 | 대 |
| ✅ 완료 | 코드 정리(클린업) Phase 1 — 히스토리성 주석/폐기 코드 제거 (약 30개 파일) | 코드 정리 | 소 |
| ✅ 완료 | 코드 구조 개선 Phase 2 — switch→Dictionary lookup table(BuildingTypeHelper) + HexMetrics 중복 setup 제거 (2026-06-25) | 코드 정리 | 중 |
| ✅ 완료 | GameBootstrapper.Setup.cs 하드코딩 배열 파생 — 환불 캐시의 stage1Buildings/nonProductionBuildings 하드코딩 배열을 BuildingTypeHelper 공개 API 파생으로 교체. 신규 건물 추가 시 `_buildingTable` 한 줄로 환불 캐시 자동 반영 (2026-06-25) | 코드 정리 | 소 |
| ✅ 완료 | IUnitFactory 인터페이스 도입 — IGameServices.GetUnitFactory() 반환 타입을 UnitFactory(Infrastructure) → IUnitFactory(Application)로 변경. Application → Infrastructure 역방향 의존 제거 (2026-06-26) | 아키텍처 | 소 |
| ✅ 완료 | 로그인 시스템 C# 구현 (Firebase Auth + GPGS) | 기능 | 중 |
| ✅ 완료 | 게임 화면 UI TC 62개 실기기 테스트 + END UI 버그 수정 | QA/버그 | 중 |
| ✅ 완료 | BuildingPlacementUI 씬 계층 재설계 (BP-001/BP-002 해결) | UI | 중 |
| ✅ 완료 | 패널 버튼 크기 불일치 수정 (PRD-001, BAP-001) — LayoutElement 균등화 | UI | 소 |
| 🔵 조건부 완료 | AI 시스템 — 코드 + Inspector 작업(AIConfig/Scenario 3종족 에셋 생성, DifficultySelectView GO 배치) 완료. 핵심 흐름(유닛 생산/건물 업그레이드) 실기 조건부 완료(2026-07-16, PASS·특별한 문제 미발견). 후속: 반응 시스템(R1~R3)·3종족 시나리오 무작위 동작 정밀 검증 | 기능 | 대 |
| 🔴 높음 | AI 시스템 — 반응 시스템(R1 유닛열세/R2 골드과잉/R3 채굴소 파괴) 트리거·동작 + 3종족(Human/Spirit/Transcendence) 시나리오 무작위 선택 동작 정밀 실기 검증 (핵심 흐름은 확인됨, 세부 정밀 검증만 잔여) | 기능 | 소 |
| 🔴 높음 | 신규 유닛 프리팹 실기 테스트 + 후속 작업 (Animation Event 부착, UnitFactory 등록, StatsReference 스탯 확정) | 기능 | 대 |
| 🔴 높음 | 게임 화면 UI 크기/레이아웃 수정 잔여 (HUD-007, SET-004, SET-007/END-001, MULTI-END-002 — 5항목) | UI | 소 |
| 🔴 높음 (**2단계 — 1단계 완료 2026-09-03**) | **FlatTop 11×21 무작위 대전 맵 구현 — 2단계: 싱글플레이에서 무작위 맵이 실제로 도는 것까지.** 1단계(타일 상태 계약 전환)는 **구현·에디터 실기 검증 완료**라 이 표에서 내렸다(이력은 [WORK_HISTORY.md](WORK_HISTORY.md), 상태는 [PROJECT_STATUS.md](PROJECT_STATUS.md)). **2단계 범위(2026-09-03 재조정 — 경계를 「싱글 = 2단계 / 멀티 = 3단계」로 다시 그었다):** 결정적 PRNG 4스트림, `SymmetricMapBuilder`, 유형별 생성 전략 5종, exact 180° 대칭, 광산/초기 골드, 초기 골드 전용 테스트 모드 설정 필드, 건설 불가·차단 지형 생성, 검증기·폴백 5개와 제작 도구, **그리고 만든 맵을 실제 전장으로 옮기는 것까지** — `MapDefinition` → `HexGrid` 투영, `GameConfig` 격자 11×21, 렌더러(막힌 타일 빈 공간·건설 불가 해치), AI 배치 후보 판정 전환, 건설·점령 전용 조건. **완료 판정은 「싱글 경기에서 매번 다른 맵이 나오고 정상 플레이된다」이며 실기로 확인한다.** 종전 분할은 2단계를 생성기·검증기·폴백까지로 두어 완료해도 게임 동작이 그대로였다. **3단계는 멀티 전송·해시 대조·실패 UI·재경기만 남는다.** ⚠️ **1단계가 만든 타입 6종 중 `MapType`·`DecorationDefinition`·`MapDefinition`·`MapDefinitionCodec` 4종은 아직 호출부가 0곳**이며, 이 2단계가 그 호출부를 만드는 작업이다 (설계 확정·문서 동기화 2026-07-20) | 기능/맵 | 대 |
| 🔴 높음 (**3단계 — 2026-09-03 등록**) | **무작위 맵 3단계 — 잔여 전환 · 맵 전송 · 재경기.** ① **AI 건물 배치 후보 판정 전환** — `Application/Services/AIOpponentController.cs` `FindPlacementTile()` 의 후보 판정(**807~809행**)과 **같은 파일 770~773행 XML 주석**이 아직 이동 가능 여부로 판정한다. 「일반 건설」 조건으로 옮긴다(단일 소스 `TechnicalDesignDocument.md` 「기존 코드 전환 요구」 · 기획 계약 `GameSystemRules/GameSystemRules_AI.md` 규칙 26). ② **건설·점령의 전용 조건 전환** — 일반 건물 배치가 아직 이동 가능 여부를 그대로 쓰고(`TileKind == Normal` 미확인), 점령에 `TileKind != Blocked` 확인이 없다. ③ canonical chunk 전송, `SameMap`/`NewMap`, 경로 완전 차단 대응. ⚠️ **①②는 지금 고쳐도 결과가 달라지지 않는다** — 현재 고정 맵은 `TileKind` 를 설정하는 코드가 한 곳도 없어 모든 타일이 `Normal` 이고, 「이동은 되지만 건설만 막히는 타일」을 만들어내는 것이 2단계이기 때문이다. **2단계 완료 후에 착수한다** | 기능/맵 | 중 |
| 🟡 중간 (미착수) | **MistShrine 멀티플레이 실기 검증** — 이번 사이클 검증은 **에디터 싱글플레이로만** 이루어졌다. 범위 판정·중첩 해소·회복량 계산은 싱글·멀티가 같은 코드 경로를 공유하지만, **건물 HP 동기화(`SyncBuildingHealClientRpc`)·클라이언트 표시·RPC 팀 검증·쿨다운 로컬 미러·이중 틱 여부는 멀티에서만 도는 경로이며 한 번도 실행되지 않았다.** Host+Client 구성으로 확인 필요 | QA | 소 |
| 🟡 중간 (미착수) | **MistShrine 물안개 지속 VFX + 사용 버튼 아이콘 제작** — 현재 물안개는 **눈에 보이지 않고**(등록 VFX는 `vfx_mistshrine_destroy`/`vfx_mistshrine_upgrade`뿐 — 규칙 26), 사용 버튼은 **임시 텍스트 라벨**(UI 규칙 15). VFX 재생 시 사운드 규칙 15(VFX+SFX 쌍) 준수 | 에셋 | 중 |
| 🟡 중간 | 스킬 건물 구체 스킬 목록·수치 확정(기획) — 각 슬롯 1~5 스킬·쿨다운·반경·지속·피해·상태효과를 ScriptableObject 데이터로 확정. 개별 스킬 쿨다운 도입 여부·회복 스킬 지점 지정 전환 여부 포함 | 기획 | 중 |
| 🔴 높음 | 전역 GameProtocolVersion/build 호환성 관리 — matchmaking 동일 버전 필터, custom lobby Relay 전 검사, NGO connection approval/rejection, reconnect 버전 검증, update-required UX | 네트워크/호환성 | 대 |
| 🔴 높음 | Login.unity 씬 로그인 UI 조립 | UI | 소 |
| 🔴 높음 | UGS OIDC 브릿지 활성화 — UGS Dashboard OIDC 제공자(`oidc-firebase`) 등록 (현재 `id provider not found`로 멀티플레이 제한) | 인프라 | 소 |
| 🟡 중간 | BuildFailed/EnqueueFailed UI 피드백 (멀티) | UI | 소 |
| 🟡 중간 | 게임 내 밸런싱 (골드/HP/생산시간) | 기획 | 중 |
| 🟡 중간 | 로비 UI 비주얼 폴리싱 (에셋 제작 완료 2026-05-30) | UI | 중 |
| 🟢 낮음 | 재접속 실제 구현 | 기능 | 중 |
| 🟡 중간 | 피격 VFX 프리셋 연결 — `UnitEffectConfig.hitPreset` Inspector 배선(Human 6종 등 미연결) | 에셋 | 소 |
| 🟡 중간 | 멀티플레이 원거리 공격 방향(facing) 버그 — 원거리 유닛이 공격 시 타겟을 정확히 안 바라봄. 진단상 에셋 아닌 멀티 원거리 facing 공유 회전 로직(서버 계산+NetworkTransform) 문제로 추정(근접 유닛 미노출). 수정 시 근접 유닛 회귀 주의. 상세: `_Tasks/2026-07-20/03_22_infernospirit-dot-and-attack-facing/Plan.md` (InfernoSpirit 작업에서 진단·보류) | QA/버그 | 소 |
| ⬜ 백로그 | 튜토리얼 | 기능 | 대 |
| ⬜ 백로그 | Firebase 백엔드 (랭킹/IAP) | 기능 | 대 |

> **※ 2026-08-10 ×10 스케일 반영 정정:** 위 표의 **MushroomBomber · InfernoSpirit · QuakeSpirit** 행과 상단 **"현재 단계"** 문단에 남아 있던 ×10 개편(2026-07-31) **이전** 전투 수치(직접 피해·DoT 틱값)를 현재 값으로 정정했다. **비율·반경(100% / 50% / `×0.5` / 반경 1.0)과 쿨다운·사거리·이동속도·골드 비용은 ×10 대상이 아니므로 그대로다.** 날짜가 박힌 완료 이력(Phase D-1 방어 타워 완료, Phase F-4 클립 이벤트 주입 이력 등)은 **당시 기록이므로 원문을 보존**한다. 수치의 단일 진실 소스는 [StatsReference.md](StatsReference.md)다.

---

## Phase A — 네트워크 버그 수정

### 🔵 A-2. 매치메이킹 404 수정 — 호스트 결정 Lobby CreateOrJoin 전환 (A방식) — 초기 정상·지속 관찰 중 (2026-07-17)
- **증상**: 랜덤 매칭은 성사되나 **직후 호스트 결정 단계에서 HTTP 404** → 게임 연결 끊김.
- **원인**: `MatchmakerManager.DetermineIsHostAsync` 내부 `GetMatchmakingResultsAsync`가 전용 서버(Multiplay)용 서버 지향 API인데 P2P(Relay) 클라이언트가 호출 → 조회 대상 리소스 없어 404. 매칭 자체는 정상, 호스트 결정 단계만 실패.
- **해결(A방식)**: 호스트 결정을 매치 결과 조회 → **Lobby CreateOrJoin 원자 선점**으로 전환. 모든 플레이어가 같은 `matchId`를 `lobbyId`로 `CreateOrJoinLobbyAsync` 호출 → 없으면 생성=호스트 / 있으면 참가=클라. 서버 원자 처리로 정확히 한 명만 호스트.
- **변경 파일(3개, Infrastructure/Network)**: `LobbyManager.cs`(추가: `CreateOrJoinLobbyByMatchIdAsync`, `RefreshCurrentLobbyAsync`), `MatchmakerManager.cs`(비활성화 주석: `DetermineIsHostAsync`/`GetStableHash`), `NetworkGameManager.cs`(추가: `StartMatchmadeGameAsync`/`HostMatchmadeGameAsync`/`JoinMatchmadeGameAsync`, `StartMatchmakingAsync` 분기 교체, 구 클라 참가 경로 비활성화 주석). 클라 참가는 CreateOrJoin으로 일원화, RelayJoinCode 채워짐만 폴링 대기(최대 15회).
- **상태**: 초기 매칭 실기에서 404 없이 정상 연결 확인. 단 **간헐(intermittent) 버그라 지속 테스트 중** — 확정 PASS 아님. 비활성화(주석)한 레거시 코드(`DetermineIsHostAsync`/`GetStableHash`, 구 클라 참가 경로)와 미사용 `FindLobbyByMatchIdAsync`의 최종 삭제는 지속 테스트 확정 후.
- **잔여 리스크**: ① SDK 시그니처 에디터 컴파일 최종 확인 권장, ② "정확히 한 명만 호스트"·간헐 재현 지속 멀티 실기(Host+Client) 검증, ③ 클라 RelayJoinCode 대기 15초 타임아웃.
- **브랜치/커밋**: `claude/matchmaker-404-error-pi9qdn` / `a3dbc73`. task: `_Tasks/2026-07-16/19_09_matchmaker-404-host-determination/`.

---

## Phase B — 네트워크 미완성 기능

### B-0. 전역 GameProtocolVersion/build 호환성 관리 🔴 높음 (2026-07-19 등록)

- **범위**: matchmaking same-version filter, custom lobby의 Relay 진입 전 호환성 검사, NGO connection approval/rejection, reconnect 시 버전 재검증, update-required UX.
- **경계**: FlatTop 무작위 맵 구현에 종속시키지 않고 모든 멀티플레이 진입·재접속 경로에 공통 적용한다.
- **맵 기능과의 경계**: 무작위 맵은 canonical binary 식별용 임시 `MapVersion(int, 초기값 1)`만 사용하며 unknown map format deserialize 차단과 map prep mismatch 실패만 담당한다. 이 값으로 앱/접속 호환성을 판정하지 않는다.

### B-1. 로비 UI 비주얼 폴리싱
- **현황**: MVVM 코드 완료 (2026-03-15). UI 에셋 제작 완료 (2026-05-30) — 아이콘 13종(탭/기능/로비버튼), 버튼 배경 2종(Primary/Secondary), 스피너 1종(HexOrb)
- **남은 작업**: 제작된 에셋을 로비 씬 Inspector에 연결하여 비주얼 폴리싱 진행

### B-2. 재접속 실제 구현
- **파일**: `Assets/_Project/Scripts/Infrastructure/Network/ReconnectionHandler.cs`
- **현황**: 30초 대기 후 ForceWin만 구현
- **구현 필요**: NGO Reconnect API 활용, 재접속 후 게임 상태 복원

---

## Phase C — 게임플레이 완성도

### C-0. 싱글플레이 AI 시스템
**🔵 코드 구현 완료 (2026-06-07)**: LocalPlayerDifficulty / AIConfig / AIScenarioConfig ScriptableObject / AIOpponentController(빌드오더+반응시스템+BFS) / GameBootstrapper 연동 / 로비 난이도 선택 UI(DifficultySelectView) 전체 완료. 3종족 시나리오 에셋 개편 완료 (2026-06-10): Human/Spirit/Transcendence 각 1개 에셋 × 3시나리오, Domain 레이어 아키텍처 정리.

**🔵 핵심 흐름 실기 조건부 완료 (2026-07-16)**: 유닛 생산·건물 업그레이드 등 핵심 흐름을 반복 실기로 확인 — PASS, 특별한 문제 미발견.

**남은 작업 (세부 정밀 검증)**:
1. 반응 시스템(R1 유닛열세 / R2 골드과잉 / R3 채굴소 파괴)의 트리거·동작 정밀 검증
2. 3종족(Human/Spirit/Transcendence) 시나리오 무작위 선택 동작 확인

---

### C-1. 게임 내 밸런싱
현재 수치는 임시값. 플레이테스트 후 조정 필요.

| 항목 | 현재값 | 조정 방향 |
|------|--------|---------|
| 시작 골드 | 500 | 테스트 후 결정 |
| 채굴소 수입 | 10골드/초 | 타일 경제 밸런스 체크 |
| Pistoleer HP/공격/사거리 | 300 / 60 / 1.0 | DPS=30, cooldown≈2.0s — 2026-08-10 ×10 스케일 반영 정정 |
| Assault HP/공격/사거리 | 400 / 10 / 2.0 | cooldown≈0.2s ※2 — 2026-08-10 ×10 스케일 반영 정정 (HP 400은 단순 ×10이 아닌 별도 조정값) |
| Sniper HP/공격/사거리 | 300 / 180 / 5.0 | DPS=60, cooldown≈3.0s — 2026-08-10 ×10 스케일 반영 정정 (공격력 180은 단순 ×10이 아닌 별도 조정값) |
| Castle HP (Human `Castle` / Spirit `SpiritNexus` / Trans `ElderTree`) | 2000 / 1500 / 3000 | 확정 — 플레이테스트 후 조정. 2026-08-10 ×10 스케일 반영 정정 |
| AutoTower 공격력/쿨다운 (Human · CannonTower) | 150 / 5.0s | 확정 — 공격력은 ×10 스케일(2026-07-31) 반영값, 쿨다운은 ×10 대상 아님 |
| AutoTower 공격력/쿨다운 (Spirit · RuneSpire) | 150 / 3.5s | 확정 — 가장 강한 타워. 공격력 ×10 반영, 쿨다운 불변 |
| AutoTower 공격력/쿨다운 (Trans · **VineTower**) | 150 / 5.0s | 확정 — 공격력 ×10 반영, 쿨다운 불변. ⚠️ Transcendence 방어 타워는 **VineTower**이며 MistShrine이 아니다(MistShrine은 공격하지 않는 별도 힐 건물 `HealShrine`=6) |
| MistShrine 회복량 / 범위 반경 (Trans · HealShrine) | **미확정** | **미확정(밸런싱 예정)** — 2026-08-10 물안개 힐 재설계로 재산정 대상. 종전 표기값(이 표의 옛 값 1 HP/s는 ×10 이전, `StatsReference.md`의 10 HP/s는 ×10 적용값, 범위 3)은 **모두 재설계 이전 수치**다 |
| MistShrine 물안개 지속시간 / 쿨다운 (Trans · HealShrine) | **미확정** | **미확정(밸런싱 예정)** — 재설계로 신규 필요한 수치. **지속시간 < 쿨다운**(다운타임 필수, 상시 유지 설정 금지) |
| MistShrine 회복 텍스트 표시 주기 (Trans · HealShrine) | **미확정** (임시 3초) | **미확정(밸런싱 예정)** — 회복 틱(1초)과 표시 주기를 분리. 화면 도배 방지용 임시값 |

> **※ 수치의 단일 진실 소스(SSOT)는 [StatsReference.md](StatsReference.md)다.** 위 표는 밸런싱 논의용 **요약**이므로, 두 문서의 값이 갈리면 **항상 `StatsReference.md`를 따른다**(이 표를 근거로 `StatsReference.md`를 고치지 말 것). 2026-08-10, ×10 스케일 개편(2026-07-31) 이전 값이 이 표에 남아 있던 것을 `StatsReference.md` 기준으로 정정했다 — Pistoleer · Assault · Sniper · Castle HP 4행. ⚠️ **Assault HP(400)와 Sniper 공격력(180)은 단순 ×10이 아니라 별도 밸런스 조정이 반영된 값**이므로, 계산으로 유도하지 말고 `StatsReference.md` 원문을 그대로 옮길 것. 이 표의 이동속도·사거리·쿨다운·골드 비용은 ×10 대상이 아니다.
>
> **※2 Assault 쿨다운 표기 불일치(이번 정정 범위 밖):** 이 표의 `cooldown≈0.2s`는 `StatsReference.md`의 실제 값 `0:17(0:33)` = **0.33초**와 어긋난다. 쿨다운은 ×10 대상이 아니어서 이번(2026-08-10) 정정에서 손대지 않았으며, 어느 쪽이 맞는지 확정한 뒤 정정한다. 확정 전까지 Assault DPS는 재산정하지 않는다.

> **MistShrine 관련 수치 근거:** [GameSystemRules_Buildings.md](GameSystemRules/GameSystemRules_Buildings.md) **MistShrine 물안개 힐 시스템 규칙 16**(밸런싱 미확정 표)이 단일 소스다.
> **2026-08-12 갱신 — 구현은 완료됐지만 수치는 여전히 미확정이다.** 현재 게임에서 도는 값은 **전부 임시값**이며(`_Tasks/2026-08-10/14_12_mistshrine-heal-implementation/Plan.md` §3), **어떤 문서에서도 확정 수치로 인용해서는 안 된다.**
> 밸런싱 확정 시 바꿀 곳은 **`Resources/Config/SpecialAttackConfig.asset` 한 파일뿐**이다(코드 기본값은 폴백일 뿐이며 Inspector 값이 우선). 반경 값이 표시용/판정용으로 이중화돼 있던 문제는 2026-08-11에 제거되어, **에셋만 고치면 화면의 범위 원까지 함께 따라간다.**

---

## Phase D — 콘텐츠 확장 (백로그)

### D-1. 방어/마법 타워
- **미완:** 구체 스킬 목록·수치·아이콘 확정(기획). 현재는 종족별 플레이스홀더 5슬롯 테스트용.
  상세: [GameSystemRules_Skills.md](GameSystemRules/GameSystemRules_Skills.md)

### D-3. 유닛 AI 상태머신
- 추가 목표: Idle → Patrol → Retreat 상태 확장 (현재 미구현)

### D-4. 신규 유닛 프리팹 완성 (16종)
**🔧 에디터 스크립트 완료 (2026-06-05)**: Human 5종(LittleKnight/SpearMan/BattleAxe/Tank/CannonCart)·Spirit 6종(DustSpirit/BoulderSpirit/QuakeSpirit/TideSpirit/StreamSpirit/TorrentSpirit)·Transcendence 5종(RabbitTrickster/RhinoBreaker 등) × Blue/Red 총 32개 프리팹 자동 컴포넌트 부착. `Assets/Editor/Setup/SetupNewUnitPrefabs.cs`(**1회성 — 실행 후 삭제됨**)

**진행 (2026-07-21)**: 특수 유닛 5종 **전량 구현 완료 + InfernoSpirit DoT까지 완료** — BattleAxe(휩쓸기형 부채꼴, 2026-07-17), TorrentSpirit(파도형 이동 AoE + 힐 서브시스템, 2026-07-17), BloomFairy(힐러 전용 경로 + HoT/DoT 공용 시스템, 2026-07-18), MushroomBomber(착탄형 원형 반경 DoT + DoT 초 단위 틱 모드 + 식물 라인 생산 배선, 2026-07-19), InfernoSpirit(단일 대상 DoT — 규칙 40 초 단위 틱 재사용, 값 별도 진입점 분리, 2026-07-20), QuakeSpirit(착탄형 즉발 2단계 AoE — 주 타깃 100%/스플래시 50%, 스플래시가 건물도 포함, MushroomBomber 원형 반경 헬퍼 `internal static` 공용화 재사용 + 건물 순회 신설 + 유닛/건물 hit-set 분리, 2026-07-20 멀티 로그 검증). 특수 공격 전략 핸들러 아키텍처(핸들러 + 레지스트리 1줄) 확립. **잔여 없음**(BloomFairy는 힐러 전용 경로로 의도적 미등록). **알려진 이슈(보류, 별도 task)**: QuakeSpirit `OnAttackHit` 미주입 타이밍 어긋남, 멀티 원거리 facing 버그. 상세: `PROJECT_STATUS.md` / `WORK_HISTORY.md`, task `_Tasks/2026-07-16/18_06_battleaxe-aoe/` · `_Tasks/2026-07-17/12_59_torrentspirit-wave-aoe/` · `_Tasks/2026-07-18/03_40_bloomfairy-healer/` · `_Tasks/2026-07-19/01_42_mushroombomber-impact-dot/` · `_Tasks/2026-07-20/03_22_infernospirit-dot-and-attack-facing/` · `_Tasks/2026-07-20/10_24_quakespirit-impact-aoe/`.

**남은 작업**:
1. 실기 테스트 — 프리팹 컴포넌트 부착 정상 동작 확인
2. Animation Event 부착 (각 유닛 공격 애니메이션 타이밍)
3. UnitFactory 종족별 리스트에 신규 16종 등록
4. StatsReference.md 스탯 확정 후 UnitStatsConfig Inspector 입력

---

## Phase E — 플랫폼/폴리싱

### E-1. 사운드/BGM
- BGM (로비/인게임/승리/패배)
- 효과음 (공격, 건물 건설, 골드 획득, 유닛 사망)

### E-2. 튜토리얼
- 첫 실행 시 인터랙티브 튜토리얼
- 헥스 클릭 → 건물 건설 → 유닛 생산 → 공성 흐름 안내

### E-3. Firebase 백엔드
- 실시간 글로벌 리더보드 (Firestore onSnapshot)
- 승/패 기록 저장 (Firestore)
- Android 인앱결제 (Google Play Billing — 스킨)
- Firebase Functions (경기 결과 처리, IAP 영수증 검증)

### E-4. 로그인 시스템 구현 (Login.unity)
- Login.unity 씬 신규 생성 (Build Index 분리)
- LoginUI (익명/Google Play Games/이메일+비밀번호 선택 화면)
- ProfileView 계정 연동 탭 구현 (익명 → 실계정 전환)
- AuthSystemRules.md 기준 구현

---

## Phase F — 전투 타격 타이밍 동기화 후속 (2026-07-12 등록)

전투 타격 타이밍 동기화 작업(`_Tasks/2026-07-09/01_12_combat-hit-timing-sync/`) 완료 후 남은 후속 항목.

### F-3. 피격 VFX 프리셋 연결 🟡 중간
- **작업**: `UnitEffectConfig.hitPreset`을 Inspector에서 각 유닛에 배선. Human 6종 등 미연결.
- **비고**: 코드 아님, 에셋 연결 작업.

### F-4. 미구현 특수 타격 클립 이벤트 주입 🟢 낮음
- **🔨 과거 잔여 기록 (2026-07-20)**: 당시 QuakeSpirit Attack clip에는 `OnAttackHit`이 없고 `hitFrameTimes`가 placeholder 1.0초여서 별도 후속 task로 분리했다. 이 문장은 당시 상태를 보존한 이력이며 현행 미완료 목록이 아니다.
- **✅ 후속 교정 완료 / 실기 대기 (2026-09-14)**: production `Base Layer/Attack` clip의 `OnAttackHit` 1.667초와 `UnitStatsConfig.hitFrameTimes` 1.667초를 정렬하고 QuakeSpirit을 Supported로 승격했다. 영구 fail-closed self-validation과 Root Pose Cross Audit은 errors 0 PASS다. Android Build And Run은 시작까지만 확인했으므로 완료·설치·사용자 실기 결과는 OPEN이다.
- **남은 대상**: QuakeSpirit marker/config 구현 작업은 없다. 남은 것은 Android 빌드 완료·설치 확인과 실제 타격 시점·AoE·반복 공격·타겟 전환·기존 유닛 회귀의 사용자 판정이다. BloomFairy는 힐러 전용 경로로 의도적 미등록이다.

### F-5. Firebase/EDM 저장소 방침 정리 🟡 중간 (2026-07-13 등록)
- **현황**: 이번 세션에서 발견 — 신규 환경에 저장소를 클론하면 Firebase/EDM(External Dependency Manager) 관련 임포트가 누락되어 재임포트가 필요한 문제.
- **작업**: Firebase/EDM 패키지·플러그인을 저장소에 어떻게 커밋/제외할지(버전 관리 방침)를 정리하여 신규 클론 시 재임포트 없이 빌드 가능하도록 정비.
- **비고**: 인프라/저장소 관리 작업(코드 아님).
- **2026-07-13 진행**: 관련하여 `#if HEXIEGE_ENABLE_FIREBASE_AUTH` 컴파일 게이트를 제거(커밋 4fe1cf0)해 SDK 로컬 임포트 시 실제 Firebase 코드가 무조건 컴파일되도록 복원(스텁 컴파일로 로그인 무조건 실패하던 버그 해소). **남은 작업은 SDK/EDM 자체의 버전 관리(커밋/제외) 방침 확정**으로, 게이트 제거와 별개.
