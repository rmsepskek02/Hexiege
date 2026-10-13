# BloomFairy 회복 타이밍 교정 계획

**§23 최신 역할교대 saved-log 분석(2026-10-04) — first-entry/due/postdispatch cooldown/등록 기준 duration focused 관측 수용·전체 Bloom OPEN:** Android Host/Editor Client 및 Editor Host/Android Client 신규 두 경기에서 Expired27/27이 등록 후3.001368~3.107432초·now>=Deadline, early0이었다. 각 경기 FirstEntry7/7 및 exact Play/Marker55/63 일치. 별도 B 시작/rebind wave clock 경고는 미해결 관찰이며 HoT 인과 증거가 아니다. visual/VFX·Client HP/text 전수·skill HoT/invalid-clock/movement-replaced coverage는 미시험이다. §21 실패 baseline/§22 구현·메뉴 이력 보존, 이번은 로그 분석·문서만. 상세 기존 Bloom Research/Plan §23.

**§22 최종 native 인계(2026-10-04) — source1파일·static4 errors0·실제 새 기존 두 메뉴 PASS / 새 HoT runtime·전체 OPEN:** final source16:24:35 뒤 실제 Assets Refresh로 native Assembly-CSharp16:28:31/Editor16:28:32 갱신, reload busy 해소 및 Unit Action/Root SelfValidate 새 실행 PASS를 메인이 확인했다. 새 production HoT fixture·Analyze·게임 runtime 증거는 아니며 이번 빌드 없음. 아래 import/메뉴 대기는 당시 이력, §21 조기 종료 baseline·정책/수치 불변과 runtime OPEN 유지. 상세 기존 Bloom Research/Plan §22.6.

**§22 최신 구현 인계(2026-10-04) — 공통 continuous Heal 등록 기준 duration source 완료·새 static4경로 errors0 / 두 Unity 메뉴 대기·runtime OPEN:** 메인 review로 UnitCombatUseCase.cs 한 파일 반영을 확인했다. Bloom+skill HoT의 scaled 등록/deadline 계산이며 수치·authority·에셋/UnitView·ticker·DoT 불변이다. 실제 Assets Refresh 뒤 Unity import 진행 중이고 native DLL은 아직 이전14:15본이다. 이번 빌드 없음·새 메뉴 PASS 주장 없음. §21 조기 종료 증거는 교정 전 이력, 새 duration runtime 수용은 OPEN이다. 상세 기존 Bloom Research/Plan §22.5.

**§22 최신 계획 — 등록 기준 공통 연속 Heal duration / PLAN·미구현·사용자 구체 계획 승인 대기:** 메인 검토 proposal의 좁은 실행 계획을 추가했다. 지금 승인 범위는 task 두 문서뿐이다. 코드·검증·빌드 결과 없음, §21 실기 증거·전체 OPEN 유지. 구현은 이 구체 Plan 공유·승인 후에만 진행한다.

**§21 최종 분석 인계:** first-entry/due/postdispatch cooldown focused 로그 관측 수용. 실제 HoT 등록→종료 약70ms 짧은 공통 효과 duration 잔여는 별도 OPEN·제안만/코드 미수정이다. visual/VFX·Client HP/text 전수·movement-replaced 재진입 미확인으로 전체 Bloom OPEN. 아래 Timing 대기는 당시 이력; 이번은 saved-log 분석·문서만, §20 이후 새 빌드/자동 검증 없음. 상세 Research §21.5.

**§21 최신 실기 로그 판정:** first-entry13/13 accepted·총83 exact Host/Client Play로 첫 진입/반복 전달 회귀의 로그 한정 관측 수용. Timing/HoT 전문 최종 인계 대기, visual/VFX 미확인·전체 Bloom OPEN이다. 상세 Research §21, 아래 §20 runtime 대기는 이전 단계 이력이다. 새 수정/TC/QA/영상/UI/build 승인은 없다.

**§20 최종 구현·검증 인계:** 최소 UnitView source 완료·새 static4경로 errors0·실제 Unit Action/Root SelfValidate 두 기존 메뉴 PASS·Build And Run1회 시도 확인. JDK checking 진행창 이후 UI/build log 확인 중단, 완료·설치·runtime 미확인/빌드 성공 주장 없음. fresh-first-entry production runtime fixture 미실행·재시험 OPEN. 이전 순서 주석/guard·authority·deadline/Remaining·owner·클립/수치/post3 정책 보존. 상세 §20.4, 아래 대기는 당시 이력이다.

**§20 최신 구현·보존 상태:** Sagan 최종/메인 review 인계로 UnitView same-object NetworkUnit helper의 Initialize·첫 guard 선연결과 이전 순서 주석 보존을 확인했다. 기존 guard/쿨다운/stale owner 불변, 새 정적4경로 errors0. 새 메뉴 결과·빌드 사실은 대기, runtime OPEN이다. 상세 §20.3/Research §20.3; 아래 proposal/GO 대기는 당시 이력이다.

**§20 현재 승인 계획·기존 로직 보존 — 첫 힐 authority 의존성 순서 교정:** 실제 NetworkUnit을 첫 guard 전에 resolve/capture하고 기존 서버/수명 검사를 유지할 계획이다. guard 제거·권위 완화·기존 cooldown/stale owner/보존 주석 삭제는 하지 않는다. Sagan 읽기 전용 proposal 및 메인 GO/코드 인계 대기, 아직 새 구현·검증·메뉴·빌드 결과 없음. §19 FAIL 이력·private cache runtime 미관측·새 실기 OPEN을 유지한다. 아래 diagnosis-only/미승인 제안은 당시 단계다.

**최신 상태 — §19 첫 힐 초기화 순서 회귀 진단 / FAIL·OPEN:** 마지막 §18 교정의 entry authority guard가 아직 초기화되지 않은 lazy HealNow cache를 요구하는 source 결함이 확인됐다. 최신 양 peer BLOOM-HEAL0과 사용자 반복 힐 부재·제자리 Walk에 부합하나 private cache/정지 인스턴스 exact runtime probe는 없다. 이번은 원인 분석 문서만, guard 전 독립 component 초기화는 제안·미구현이다. 아래 §18 자동 PASS/빌드 시도는 이력이며 실제 fresh 첫 진입 회귀 검증 증거가 아니다.

**§18 최종 인계 — UnitView 교정/정적·새 기존 두 자동 gate PASS / 빌드1회 시도 관측·실기 OPEN:** 메인이 새 Unit Action 및 Root Pose Cross Audit SelfValidate PASS를 확인한 뒤 Build And Run을1회 클릭하고 Importing/Settings/Mobile_RPAsset.asset만 관측했다. 이후 UI/build log polling 중단, 완료·설치·runtime 미관찰/성공 추정 없음. 상세 §18.6·Research §18.7을 따른다. 아래 구현 예정·정적/gate/build 대기는 당시 단계 이력이다.

**§18 최신 최종 정적 인계:** UnitView 한 파일 반영과 최종 Runtime0/20·Editor0/0·Development0/20·Release0/24(errors/warnings) 인계를 확보했다. source 목록 차이0·컴파일 전후 hash 동일이며 경고 감소는 정적 카운트일 뿐 기존 warning 교정 증명이 아니다. 새 기존 두 메뉴·이번 빌드 시도는 실행 대기, 전체 OPEN. 현재 source/cleanup·보조 artefact 경계는 §18.5 및 Research §18.6 참조; 아래 정적 대기는 당시 이력이다.

**§18 최신 코드 인계 — UnitView 한 파일 반영 확인 / 최종 정적·메뉴·빌드 결과 대기:** 메인 source review 인계에 따라 cooldown owner record·entry guard·실제 dispatch absolute deadline+Remaining 두 조건·post-wait·lifecycle owner 보호·bounded 로그가 반영됐다. 기존 post-dispatch counter-only wait는 삭제 없이 주석 보존됐고 글로벌 counter 설정/감소는 유지한다. 아래 승인 계획/proposal은 당시 이력이며 상세는 §18.4, 이전 runtime 증거는 §17이다. 이번 patch 이후 정적/gate/빌드 및 runtime PASS를 추정하지 않으며 전체 OPEN이다.

**§18 이번 승인 계획·기존 post-dispatch counter wait 보존:** 실제 authoritative dispatch 시각+configured AttackCooldown deadline으로 힐 발동 후 대기 기준을 교정할 계획이다. 기존 global counter 기반 post-dispatch wait는 검증 전 삭제하지 않고 주석 비활성화로 보존한다(WORKFLOW [4]). 코드 담당의 구체 proposal은 진행 중이며 구현 완료가 아니다. §16 due guard·공통 global ticker·다른 유닛 정책·설정 쿨다운3초 및 나머지 계약은 유지하고 정확한4초 cycle을 보장하지 않는다. 아래 기존 서두와 §17 증거는 이전 단계 기록으로 보존한다.

**§16 현재 승인 변경·기존 로직 보존:** marker보다 실제 HP가 먼저 적용된 §15 증거에 따라 서버 준비 대기의 이전 frame delta 누적을 같은 회차 StartedAt+1초 absolute due-time 비교로 대체할 계획이다. 기존 누적 코드는 사용자 실기 수용 전까지 삭제하지 않고 주석으로 비활성화 보존한다. 아직 코드 적용 완료가 아니며, 회전·힐 효과/발동 후 cooldown3·native clip/event는 바꾸지 않는다. 구현 및 빌드 시작 시도 승인과 빌드 완료/실기 PASS는 별개다.

**이번 승인 계획의 목적과 보존 원칙:** 회복량·주기는 그대로 두고 힐 방향과 연속 모션 연결을 교정한다. 기존 끝 자세 강제 Play/speed0 및 힐 snap 경로는 검증 전 삭제하지 않고 최소 비활성화로 보존한다. 이 단계는 task 두 파일의 계획 갱신뿐이며 코드·에셋·메모리는 수정하지 않는다.

이번 계획은 꽃요정의 회복 행동이 상대 기기에도 같은 회차의 모션으로 전달되도록 빠진 연결을 교정하는 것이다. 멈춘 자리에서 걷는 모습이 회복 모션을 덮지 않도록 시작과 종료를 구분하되, 실제 회복 판정·회복량·주기는 기존 서버 경로를 유지한다.

**최신 상태 — §16 UnitView absolute due 교정 구현·정적4경로 errors0·기존 두 자동 gate 새 실행 PASS·빌드 시도 완료 / 실기 재검증 OPEN.** Build And Run1회 클릭 직후 JDK 확인 preflight만 관측했으며 완료·설치·실행은 미관찰, UI/build log polling 중단이다. 회전·HP/HoT·클립/marker·global cooldown/post3 무변경, 이전 dt wait는 주석 보존이다. 새 due coroutine runtime fixture/실기 수용은 없으며 재시험·시각 방향·최종 원인 OPEN이다. §15 early HP FAIL은 교정 전 이력으로 보존한다.

이전 최신 상태 원문(§15 분석 단계 이력 보존): **최신 상태 — 2026-10-03 두 테스트 §15: 전달/제한된 Root 관측 확인, TIMING EARLY HP FAIL·시각 방향/최종 원인 OPEN.** 23_04의 첫 테스트 prefix 중복을 제외한 두 run이다. 실제 Host HP 증가가 marker보다 앞선2사례를 확인했으며 source 시간 구조는 강한 의심, 절대 서버 due-time 교정은 제안·미구현이다. 영상 pixels/exact Client HP timing PASS는 없다. 정책·수치·코드/에셋·빌드 변경 없음.

이전 최신 상태 원문(§14 진단 구현/정적 인계 이력 보존): **최신 상태 — 2026-10-03 진단 코드4파일 최종 인계·정적4경로 errors0 / 새 실기 OPEN.** §14.6의 최종 Runtime0/22·Editor0/0·Dev0/22·Release0/26(errors/warnings)를 따른다. Unity 메뉴·새 runtime PASS·빌드 시도는 없으며 사용자 보고 facing FAIL·원인/정밀 timing OPEN이다. transform proxy는 rendered frame이 아니며 exact Client HP/pixel onset은 미확보다.

이전 최신 상태 원문(§14 계획 단계 이력 보존): **최신 상태 — 2026-10-03 진단 로그 추가 승인 계획 READY / 구현·새 로그 대기.** §14에 self/near-zero baseline·bounded 회전/guard checkpoint·서버/Client 방향·실제 marker/dispatch/첫 HP 적용 상관 계획을 추가한다. 진단만 승인됐으며 정책·수치·회전 writer·에셋·빌드 변경은 아니다. facing FAIL(사용자 보고)·원인 OPEN을 유지한다.

이전 최신 상태 원문(§13 분석 단계 이력 보존): **최신 상태 — 2026-10-03 18_34 세 run 분석 / 전달·타이밍 seam 확인, facing FAIL(사용자 보고)·원인 OPEN.** 최신 자료·run 분리·수치·source 감사와 미구현 trace 제안은 §13을 따른다. §12의 코드/native 에셋/자동 gate/빌드 시도 완료는 이전 단계 이력으로 보존한다. 이번 분석 문서 반영은 코드/에셋/정책/수치/빌드 변경을 뜻하지 않는다.

이전 최신 상태 원문(§12 단계 이력 보존): **최신 상태 — 2026-10-03 교정 코드·에셋·자동 gate·빌드 시도 완료 / 시각 실기 OPEN.** native Setup은17:42:31 `[BloomHealSetup] native retime PASS`(changed=True, curves=130, front/tail samples preserved, clip=4s, marker=1s, both heal blends=0.15s)로 완료됐다. 디스크 양 prefab `_healCastBlend: 0.15` 및 OnAttackHit time1 확인. 클립/prefab3파일은 실제 변경이다. 이는 에셋/retime 검증이지 visual PASS가 아니다. **새 코드/에셋 자동 검증 완료:** Unit Action17:43:56의 BLOOM-HEAL-TIMELINE·UAS overall·production·C2·C3 PASS, Root Cross Audit SelfValidate17:44:41 PASS. Console errors0/warnings25(기존 compile20+fixture5)이며 Android warnings0·시각 실기 PASS를 뜻하지 않는다. native 디스크 m_StopTime4/m_SampleRate30/단일 OnAttackHit time1 및 양 prefab blend0.15 확인. **빌드 시도 완료:** File→Build And Run1회 실제 클릭 직후 Importing(iteration2)–Finishing import Settings/Mobile_RPAsset.asset 관찰까지만 확인했다. 이후 Unity UI/Editor/build log polling 및 완료·설치·게임 실행 확인은 하지 않았다. 빌드 성공 추정 없음. Setup·자동 메뉴·빌드 시도는 완료이며, 남은 시각 실기 검증은 OPEN이다. Unity domain reload 및 Google startup은 끝나 로드 지연이 해소됐으나 지연 원인은 미확정이다. 상세 코드4+Setup2+실제 에셋3 목록과 정적 결과는 §12를 따른다.

이전 상태(Setup 전 인계 이력): **최신 상태 — 2026-10-03 구현 인계 초안 / 코드4+Setup2 작성·정적4경로 errors0, 에셋/UI 미완료·신규 실기 OPEN.** 상세 전체 파일 목록과 정적 결과는 §12를 따른다. 클립/prefab는 아직 미적용(현재3초), 새 Unity 메뉴/빌드는 미실행이다. Refresh 뒤 DLL17:26:34/35 생성은 확인됐으나 Reloading Domain busy26초 진행 관찰 단계로 domain load 완료는 미확인이다. 상시4문서는 코드 인계/에셋 실행 대기 상태를 유지하고 후속 UI 결과를 기다린다.

이전 최신 상태 원문(§11 승인 계획 단계 이력 보존): **최신 상태 — 2026-10-03 승인 교정 계획 READY / 구현·새 검증·실기 OPEN.** 사용자가 기존 task 수정→교정 구현→빌드 시도를 명시 승인했다. 4초 힐 모션·연속 회차 연결·서버 점진 회전·future-start 대기와 힐러 cooldown3 보존을 §11에 계획으로 추가한다. 아직 이 교정의 코드/에셋 변경, 새 자동 PASS 또는 빌드 시도 결과를 기록한 것이 아니다. 상시문서는 최종 구현 증거 인계 후 승인 정책과 실제 구현을 구분하여 동기화한다.

이전 최신 상태 원문(§10 분석 단계 이력 보존): **최신 상태 — 2026-10-03 12:36~12:39 실기 분석: 힐 회차 전달·재생 일치 확인 / 방향·모션 연결 미해결, OPEN.** 새 세션의 Host Start/Play 및 Client Play 50회가 exact 회차로 일치한다. 이것은 화면 방향·연결 동작·Client HP/text의 정확한 동기화 PASS가 아니다. 최신 증거·소스 감사·미구현 제안은 §10, 앞선 구현9파일/meta3·자동 검증·빌드 시도 이력은 §9를 따른다. 이번 분석 후 갱신에서는 코드·규칙·수치·빌드를 변경하지 않았다.

이전 최신 상태 원문(§9 단계 이력 보존): **최신 상태 — 2026-10-03 최종 인계: 힐 동기화 구현 확인 + 새 Unit Action 04:52:02/Root SelfValidate 04:54:03 자동 PASS + 빌드 시도·시작 관찰 / 사용자 실기 재검증 OPEN.** 실제 빌드 완료·설치·실행은 미관찰이며 UI/build polling은 중단했다. 이전 모션 FAIL의 해결은 아직 입증되지 않았다. 최종 구현·코드9파일/meta3·문서7파일 목록과 검증 근거는 §9를 따른다. §1~§8의 초기3파일 제한·대기 상태는 과거 단계 이력으로 보존한다.

**후속 계획에서도 기존 로직 삭제 없음.** 일반 공격의 Attack 레벨 시작 차단과 서버 HP/HoT writer를 유지한다. 기존 힐 전달 경로를 대체할 필요가 있으면 구현자가 최소 비활성화 지점·안전 근거·변경 파일을 먼저 메인에게 인계한다. 승인 계획을 근거로 무관 로직을 삭제하지 않는다.

### 이전 단계의 목적과 계획 (원문 보존)

꽃요정의 회복 모션에 1초 지점의 연출 표식 하나를 저장하고, 실제 생산에 쓰이는 모션과 회복 타이머가 같은 설정을 가리키는지 확인한다. 체력을 회복시키는 기존 서버 경로, 회복 후 3초 대기와 총4초 주기, 3초간200 회복 및 종료 시 텍스트 표시는 그대로 둔다.

**기존 로직 제거 없음.** task 문서·구현·빌드 시도는 사용자 명시 승인 완료다. 메인이 이 계획을 공유한 뒤 game-programmer에 인계하며 다시 승인받을 필요는 없다. 이 초기 문서에는 실행 결과를 쓰지 않는다.

## 1. 최소 변경 및 정확한 연결

**focused 범위는 1초 marker와 정확한 클립 바인딩 확인이다. 과거 전체25종 migration 완료는 이 구현의 선행 조건이 아니다.** 현재 Bloom attack VFX preset은 미연결(`UnitEffectConfig.asset` type27 attackPreset fileID0)이며 이번에 새 preset을 만들거나 연결하지 않는다. marker 저장과 실제 VFX 표시 성공은 서로 다른 주장이다.

1. `Assets/_Project/Animations/Units/BloomFairy/BloomFairy_Attack.anim`에 `OnAttackHit` 이벤트 **정확히1개, 1.0초(30fps 1:00)**를 저장한다. 현재 이벤트 목록이 비어 있으므로 marker-only가 기본 변경이다. 곡선·길이·loop·속도·Walk 클립·원본 FBX는 건드리지 않는다. 근거: Units 규칙32, UnitCombatSynchronization `NET-PRESENT-001`·`NET-PRESENT-002`.
2. `Assets/_Project/Scenes/Game.unity` UnitFactory `_transcendencePrefabs`의 **type27** `attackTimelineClip`에 GUID `b934af543a2aa8f4dbc72962f233fa9f`, fileID7400000을 명시 바인딩한다. Blue/Red prefab → `BloomFairy.controller`의 정확한 Attack 상태 → 같은 클립 연결도 함께 확인한다. Controller GUID는 `c6477081339691f409654646f2f94bd1`이다. 다른 행·prefab/controller Motion을 재배선하지 않는다. 근거: `NET-PRESENT-002`, `NET-ROOT-004`.
3. `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`에 기존 구조를 따르는 소형 Bloom production gate를 추가한다. 위 생산 바인딩·정확한 Attack clip·OnAttackHit1개/1초·실제 UnitStats type27 `[1]`/쿨다운3/힐러true를 함께 읽고 불일치 시 실패시킨다. `NotApplicableAttack / SeparateHealPipeline`을 유지하며 Supported 승격·가짜 scoped 결과는 금지한다. 저장 스탯은 이미 맞으므로 변경하지 않는다. 타입 비교는 실제 enum 숫자/intValue를 사용하며 enumValueIndex나 행 인덱스로 선택하지 않는다. 근거: Units 규칙32·36, `NET-PRESENT-002`.

**메인 검증 programmer 최소 계획으로 범위 확정:** 구현 변경 파일은 위 Attack.anim, Game.unity, RunUnitActionSelfValidation.cs 3개다. UnitView/Network/HoT 로직 수정, VFX 생성, Client 반복 phase fix는 하지 않는다. 구현 승인은 이미 유효하다.

## 2. 반드시 보존할 경로

- HP는 서버/싱글 권위 타이머와 Application HoT 경로만 쓴다. OnAttackHit은 연출 전용이며 `CastHeal` 호출이나 가짜 scoped 결과를 추가하지 않는다. 근거: Units 규칙30·32·34, `NET-AUTH-001`·`NET-AUTH-002`.
- 대상 선택·자가 회복·건물 제외·유휴 감시·대상 유효성 정책을 그대로 둔다. 근거: Units 규칙33·35.
- 준비1초 후 HoT 부여, 부여 후 쿨다운3초, 실제 주기4초 예외를 유지한다. 일반 공격의 전체 쿨다운 방식으로 통일하지 않는다. 근거: Units 규칙36.
- 저장된 HoT200/3초, 연구 배율, diff 틱·중첩 없음·갱신리셋, 정상 종료 시 현재HP 텍스트1회 및 사망 시 생략을 유지한다. marker 순간에 회복 텍스트를 당겨 띄우지 않는다. 근거: Units 규칙34·37, `NET-DELIVERY-TIMED`.
- `SeparateHealPipeline / NotApplicableAttack`를 유지한다. targetId를 갖지 않는 기존 heal-cast 신호를 Supported 정규 공격 scope로 포장하지 않는다. VFX/SFX 미연결 에셋 제작이나 공통 queue 개선도 이번 범위가 아니다. 근거: Units 규칙32, 동기화 문서 §10 및 `NET-PRESENT-002`.

## 3. 구현 인계 및 증거 수집 경계

Galileo 조사 결과는 메인으로만 반환한다. 메인이 실제 변경 파일, 저장 후 이벤트 개수·시각, Blue/Red 연결, 저장 config 값 및 변경하지 않은 정책을 검토한 뒤 문서 담당자에게 검증된 구현 인계를 준다. 이 조사에서 더 큰 정책 문제를 발견하면 변경 전에 알리고 분리한다.

구현자가 수행하는 컴파일/기존 자동 검증 결과는 실제 출력·시각·범위를 붙여 인계한다. Testcase 생성이나 qa-tester 호출은 하지 않는다. 자동 게이트 성공을 반복 힐·정확한 화면 동시성·전체 migration PASS로 확대하지 않는다. 기존 Production timeline 게이트가 healer N/A를 공격 Supported로 강제하지 않도록 한다.

메인은 승인된 빌드를 **시도**하고 시작·진행·실패·완료 관찰을 구분한다. 시작 화면이나 JDK preflight만으로 빌드 완료를 쓰지 않는다. 사용자 실기와 완료 판정은 사용자가 소유한다.

## 4. 문서 후속 순서

초기 생성은 이 Research.md와 Plan.md 두 파일뿐이다. 검증된 구현 인계 전 StatsReference/Matrix·상태 문서·메모리는 수정하지 않는다. 인계 후 승인 범위 안에서 실제 이벤트 저장 상태와 수치를 반영하되 Provisional/MigrationRequired 및 healer N/A 경계를 보존한다. 최종 결과는 사용자 확인 전 OPEN/미검증으로 둔다. Git·TC/QA·무관 변경은 금지한다.

## 5. 구현 인계 결과 (2026-10-03, 디스크 확인 한정)

계획한 세 파일의 변경이 실제 디스크에 반영됐다는 메인의 검증 인계를 받았다. 회복 정책을 고치지 않고 marker와 생산 연결 및 자동 검증 경로만 추가했다. 초기 계획/미구현 서술은 이전 단계 이력으로 보존한다.

| 구현 파일 | 확인한 변경 |
|---|---|
| `Assets/_Project/Animations/Units/BloomFairy/BloomFairy_Attack.anim` | OnAttackHit1개/1.000초; 30fps/3초 loop 불변 |
| `Assets/_Project/Scenes/Game.unity` | UnitFactory type27 attackTimelineClip=fileID7400000/GUID `b934af543a2aa8f4dbc72962f233fa9f` |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | 소형 `ValidateBloomFairyProductionTimeline` 및 성공 출력 추가; NotApplicableAttack/SeparateHealPipeline 보존 |

**검증 상태:** 위 목록은 구현3파일에 한정한다. 서버config/runtimeheal, UnitView/Network/HoT는 무수정이며 VFX 생성·Client 반복 phase fix도 없다. HoT200/3초 diff, 준비1초+발동 후3초=총4초, 종료 텍스트 정책 유지. Unity 자동 검증·빌드 결과는 메인 관찰 인계 대기이고 사용자 실기 전 OPEN이다. source PASS 문자열을 실행 결과로 기록하지 않는다. 전체25종 migration은 이 focused 교정의 완료 선행조건이 아니며 Supported 승격도 없다.

**이번 문서 변경 목록(구현 목록과 별도):** 기존 Research.md/Plan.md의 이 절 및 StatsReference.md Bloom 행, UnitCombatAssetMatrix.md Bloom 행/공통 marker 보유·누락 집계. 상태/메모리/TC/QA 추가는 없으며 사용자 테스트 승인 전 후속 완료 갱신은 하지 않는다.

**programmer 최종 보강:** 세 파일 모두 추가한 절/항목을 제외하면 원본 SHA와 일치한다고 인계했다. UnitStats/SpecialAttack/Effect config와 controller/prefabs는 불변이다. Unity는 재컴파일/domain reload 중이며 최초 클릭한 self-validation은 구 assembly를 실행해 새 Bloom PASS 출력이 없었다. 따라서 그 실행을 이번 교정 PASS로 인정하지 않는다. 메인이 새 assembly에서 두 메뉴를 재실행한 사실과 빌드 관찰을 인계할 때까지 대기한다.


## 6. 새 assembly Unity 자동 검증 (2026-10-03, 메인 직접 관찰)

재컴파일/domain reload 이후 메인이 새 메뉴 실행과 로그를 직접 확인해 다음 사실을 인계했다. §5의 구 assembly 실행 및 자동 검증 대기 표기는 이전 단계 이력이며, 이번 결과로 새 production gate 실행을 확인했다.

- Console 03:42:38: Unit Action self-validation **PASS**, 새 `BLOOM-HEAL-TIMELINE` 로그 확인. 근거: 메인이 확인한 `Editor.log` 4143312행.
- 메인이 이후 새 메뉴를 클릭한 Root Cross Audit SelfValidate도 **PASS**. 근거: `Editor.log` 4143397행. 실제 경기의 formal CrossAudit Analyze 판정이 아니라 자동 SelfValidate 결과다.
- 최신 Console 캡처: errors0 / warnings10. 경고 원인별 분류나 전체 무경고를 뜻하지 않는다.
- 별도 standalone Roslyn 검사는 실행하지 않았다. 이를 실행·통과했다고 기록하지 않는다.

이 결과는 Unity 자동 게이트 실행 PASS에 한정한다. marker/timer의 런타임 동시성, 반복 시전·Host/Client 표시, focused 사용자 실기 PASS 또는 Supported 승격을 뜻하지 않는다. **메인의 Build And Run 시도는 아직 대기**이며 빌드 시작·완료·설치·실행 성공은 관찰되지 않았다. 사용자 완료 판정은 OPEN이다. 서버 회복 정책·VFX 미연결·N/A 별도 힐 경로 및 구현3파일 범위는 유지한다.

## 7. 최종 인계 — 빌드 시도 관찰 (2026-10-03)

메인이 File 메뉴의 Build And Run을 클릭했고 직후 화면 갱신에서 `DetectJavaDevelopmentKit / CheckingJDK`를 직접 확인했다. 승인된 **빌드 시도는 수행 완료**이며 추가 빌드 polling은 하지 않는다. 빌드 완료·설치·실행 성공은 미확인이다. §6의 시도 대기 기록은 이전 단계 이력으로 보존한다.

계획 대비 구현은 승인된3파일에 한정하며 새 assembly의 두 Unity 자동 메뉴는 실행 PASS다. 사용자 실기는 아직 대기(OPEN)로, focused runtime PASS나 작업 완료 판정은 하지 않는다. 독립 Roslyn은 미실행, healer N/A/SeparateHealPipeline·Provisional/MigrationRequired·기존 회복 정책·VFX 미연결은 유지한다. 이번 최종 갱신은 Research.md/Plan.md/StatsReference.md/UnitCombatAssetMatrix.md 네 문서뿐이며 다른 문서·에이전트 메모리·TC는 보존한다.

## 8. 승인된 힐 모션 동기화 교정 계획 (2026-10-03 후속)

앞선3파일 marker/binding/gate 교정 뒤 실제 Client 모션이 실패했다. Research §8의 세션·증거 한계와 확정 소스 단절을 기준으로, 힐러 독립 표현 경로만 교정한다. 아래는 승인 방향이며 저장된 구현 결과나 실기 통과 보고가 아니다.

### 8.1 최소 구현 방향과 근거

1. **서버/싱글만 힐 회차를 시작·종료한다.** 시전자 객체 수명, 회차 식별, 시작/종료 상태, 서버 시각 및 권위 방향을 한 회차로 묶어 전달한다. Client가 부상 아군을 재탐색하거나 자체 타이머로 회차·회복 결과를 생성하지 않는다. 근거: Units 규칙32·33·36, `NET-AUTH-001`.
2. **별도 힐 atomic snapshot NetworkVariable로 현재 회차를 복구한다.** 회차 식별·서버 시각·권위 방향·시작/종료 상태를 분리된 레벨 값이나 edge-only RPC가 아니라 한 snapshot으로 복제한다. 새 sequence는 그 회차의 힐 모션을 재생하고 같은 sequence는 멱등하게 재적용해 restart하지 않는다. late spawn·View init 지연 뒤에도 current-state 복원이 가능해야 한다. snapshot 타입·필드명·배치 파일은 programmer 세부 인계로 확정한다. 근거: Units 규칙22·32 및 사용자 확정 SeparateHeal 예외 방향. 일반 공격의 `NET-PRESENT-002` 연속 재생/되감기 금지는 그대로 유지한다.
3. **Client는 힐 모션만 재생한다.** 서버 시각에 대응하는 진행 상태를 Animator에 적용한다. payload의 Aim은 회차 방향의 **증거용**이며 새 Client Root 회전이나 Visual offset 회전 writer를 만들지 않는다. 방향 표현은 기존 복제된 서버 Root 방향을 그대로 보존한다. `CastHeal`, HP, HoT 틱, 적 공격 scope/결과를 만들지 않고 Simulation Root/NetworkTransform에 쓰지 않는다. 이는 메인의 진행 중 구현 방향 인계이며 최종 코드 검토·구현 완료 증거는 아니다. 근거: Units 규칙32·34, `NET-AUTH-001/002`, `NET-ROOT-001~003`, `NET-PRESENT-001`.
4. **중복·지연·종료·사망·객체 재사용을 차단한다.** 종료 state와 dead cancel을 명시하며 이미 수락한 시작을 다시 재생하거나 종료된 회차를 늦은 시작으로 되살리지 않는다. 종료가 먼저 온 경우, disable/despawn/경기 종료 및 다음 객체 수명에서 오래된 callback이 최신 모션을 덮지 않도록 한다. 정리·재적용 근거와 회차 상한 보존을 구현 인계에 명시한다. 근거: `NET-PRESENT-002`, `NET-CANCEL-003/005`, `NET-AUTH-001`; 적용은 별도 힐 표현 수명이며 일반 공격 결과 정책 변경이 아니다.
5. **힐 표현 중 Walk 끼어듦을 막는다.** 활성 힐 회차의 표현 소유권과 서버 이동 상태를 구분하고, 종료 후에도 실제 이동/정지 상태에 맞게 복귀한다. 멈춘 유닛에 무조건 Walk를 켜거나 Client Root 이동으로 증상을 가리지 않는다. 빙결·Held·종료 우선순위는 기존 계약을 유지하고 세부 경쟁 경로를 구현 인계로 확인한다. 근거: Units 규칙22·32·35, `NET-ROOT-001/002`.
6. **일반 공격 안전 차단을 보존한다.** `ShouldStartAttackFromReplicatedLevelState() => false`를 전역 true로 바꾸지 않는다. 기존 `StartCombatClientRpc`의 원자 payload/연속 재생/marker 승인 계약을 힐 때문에 완화하지 않는다. 힐은 `SeparateHealPipeline / NotApplicableAttack`, Provisional/MigrationRequired를 유지한다. 근거: Units 규칙32, `NET-PRESENT-001/002`, 동기화 문서 §10.

### 8.2 불변 수치와 범위

기존 OnAttackHit 1개/1.000초와 생산 clip binding을 보존한다. 준비1초 + 발동 후 쿨다운3초 = 총4초, HoT200/3초와 연구 배율, 서버 diff 틱·갱신리셋/중첩 없음, 정상 종료 현재HP 텍스트1회·사망 시 생략은 변경하지 않는다(Units 규칙32~37). 모션 종료와 HoT 효과 종료는 다른 수명이므로 새 모션 종료 신호로 기존 효과를 취소하거나 텍스트를 당겨 표시하지 않는다. VFX preset 제작/연결, 다른 유닛 정책 변경 및 Supported 승격은 범위 밖이다.

구현 검토 대상은 `UnitView.cs`의 힐 발행/표현/Walk 경계, `NetworkCombatController.cs`의 힐 전용 전달, `NetworkUnit.cs`의 현재 회차 복구·표현 적용 경계와 필요한 기존 이벤트 payload다. 공통 Attack 정책은 보존 대상이다. 정확한 추가/변경 파일·메서드·정리 지점은 programmer 인계로 확정하며, 과거3파일만을 이번 구현 목록으로 재사용하지 않는다.

### 8.3 구현 인계와 판정 경계

메인은 실제 변경 파일, production 발행→구독→수신→클립 적용, Host/Client 중복 재생 방지, 서버 시각/방향·생명주기 차단·Walk 복귀 및 불변 수치를 검토해 문서 담당자에게 인계한다. 컴파일·기존 자동 검증은 새 실행 출력과 범위를 받아 기록하고 이전 asset gate PASS로 대체하지 않는다. 이번 문서 단계에서는 TC/QA를 생성·제안·진행하지 않는다.

승인된 후속 빌드는 메인이 시도하되 시도/시작/진행/완료를 분리한다. 기존 JDK 관찰이나 사용자가 실기를 수행했다는 사실만으로 Codex가 빌드 완료를 직접 관찰했다고 쓰지 않는다. 새 실기에서 같은 회차의 Host/Client 모션, 제자리 Walk, 반복·종료·생명주기 및 Heal/HP exact 근거를 분리해 인계받기 전 **FAIL / OPEN**을 유지한다. Client ROOT PASS·MOVE frames0·자동 게이트만으로 모션 PASS나 전체 migration 완료를 기록하지 않는다.

### 8.4 문서 후속 순서

기존 Research/Plan을 먼저 갱신했으며, 이후 사용자가 프로젝트 필수 즉시 기록을 명시 지시하여 `.claude/mistakes/unit-action-correction.md` 목차·본문에 **2026-10-03 BloomFairy level-only heal signal was blocked while the asset gate missed Client playback** 사건을 영어로 추가했다. 따라서 Research §8.4의 초기 기록 후보는 이제 **기록 완료**이며, 후속 구현이나 실기 교정 완료를 뜻하지 않는다. 이번 정합성 수정에서는 실패 기록을 추가 변경하지 않는다.

코드 변경 뒤 세부 구현을 인계받아 task에 실제 변경 목록·관찰 결과를 추가한다. **사용자 승인된 후속 상시 문서 범위**는 Units 규칙22·32의 힐 전용 값 동기화 계약, UnitCombatSynchronization의 SeparateHeal 예외 근거, StatsReference/UnitCombatAssetMatrix Bloom 현재 상태다. 최종 구현과 일치할 때만 반영하며 원수치는 전혀 변경하지 않는다. Aim payload를 새 Client/Visual 회전 구현으로 기록하지 않는다. 새로운 규칙 충돌은 변경 전에 사용자에게 보고한다. 기존 메모리·상태 문서·TC/QA·코드·Git·앱 도구는 이번 단계의 변경 대상이 아니다. 추가 메모리 읽기/수정 및 skill setup 부가파일 생성도 하지 않는다.

## 9. 최종 구현 및 새 자동 검증·빌드 시도 인계 (2026-10-03)

꽃요정의 회복 모션을 시작하는 신호가 일반 공격 안전 차단에 막히던 연결을, 서버가 보관하고 복제하는 힐 전용 현재값으로 교정했다. 상대 기기는 실제 회복을 계산하지 않고 같은 시전의 모션만 재생한다. **구현 인계·자동 검증 완료와 사용자 실기 완료는 별개**다. §1~§8의 초기 범위·대기 상태는 각 단계 이력이며 현재 결과는 이 절을 따른다.

### 9.1 구현 사실과 불변 경계

- 메인이 최종 구현을 동결·확인해 인계했고 문서 담당자는 새3파일과 주요 production/fixture 경계를 직접 읽었다. 아래 파일 목록은 메인의 변경 범위 인계이며 문서 담당자가 Git으로 산출한 목록이 아니다.
- 서버 전용 `NetworkUnitHealCastState` NetworkVariable(Read Everyone / Write Server)는 `UnitHealCastPresentation`의 Lifetime, Sequence, Revision, UnitId, TargetId, StartedAt, EndedAt, EndReason, AimX/AimZ, MarkerOffset, ClipDuration, Active, Priority를 atomic 직렬화한다. 일반 EnemyAttack/C3와 독립이다.
- typed `OnUnitHealCastStarted/Ended` → 서버 `NetworkCombatController`의 역할/객체 수명 확인 → `NetworkUnit.PublishHealCast` → 현재값/수신 경계 → `UnitView.ApplyHealPresentation`으로 연결했다. 준비 전 수신은 성공 재생으로 소비하지 않고 late spawn/unitId/init 뒤 current-state를 재적용한다.
- cursor는 lifetime 및 sequence/revision 상한을 보존한다. 새 힐 sequence를 재생하고 같은 회차 중복은 restart하지 않는다. priority 변경 후 재개는 같은 회차의 서버 경과시간을 사용한다. 종료된 같은 sequence의 재활성화·역순·다른 수명은 거부하며 terminal close 뒤 늦은 입력을 차단한다.
- Frozen/Held가 일반 힐 모션보다 우선한다. 활성 힐을 Walk가 덮지 않으며 정지 유휴 감시는 Held와 서버 Action 회전 소유권을 사용한다. 이동 복귀는 기존 소유권 경계를 따른다. 3초 loop 클립의 끝 자세를 유지하고 source marker는 회차당 최대1회만 소비한다. 늦은 재생에서 이미 지난 marker를 재방출하지 않는다.
- **Aim payload는 증거용**이다. Client Root/NetworkTransform·Visual offset 회전 writer를 추가하지 않았고 기존 복제된 서버 Root 방향을 보존한다. 일반 Attack 레벨 시작 차단, 일반 공격의 연속 재생·SourceOnly/C3 정책과 NotApplicableAttack/SeparateHealPipeline은 유지한다.
- 준비1초 + 발동 후 쿨다운3초 = 실제 주기4초, HoT200/3초 및 연구 배율·diff 틱·갱신리셋/중첩 없음·정상 종료 현재HP 텍스트1회/사망 시 생략은 불변이다. `UnitCombatUseCase` 변경은 진단 상관 입력·로그 추가이며 새 HP writer가 아니다. 모션 close가 이미 부여된 HoT를 취소하지 않는다. 원수치·config·기존 marker/binding 에셋 변경 없음, 새 VFX preset 연결 없음.

### 9.2 개발용 상관 로그 — 실제 소스 필드

`UnitHealCastDiagnostics.Event`는 `UNITY_EDITOR / DEVELOPMENT_BUILD` 조건부이며 기존 `GameLog.Dev` 경로로 상태 전이만 기록한다. Release에서 호출/인자 평가를 제거한다. 전역 누적 사전이 아니라 View/effect 수명에 상관 상태를 보관한다. **새 로그 코드가 있다는 사실은 실기 로그 확보가 아니다.**

- 접두사 `[BLOOM-HEAL]`; 공통 필드: Peer(Host/Client/Single), Lifetime, UnitId, HealSequence, Revision, TargetId, Active, Priority, EndReason, StartedAt, EndedAt.
- 단계: Start, Publish, Receive, Play, MotionEnd, End, Marker/MarkerSuppressed, CastDispatch, CastOutcome, HoTResult.
- 단계별 추가 필드: Publish/Receive의 NetworkObjectId·Now; Play의 Now·Offset·CatchUp; CastDispatch의 Now·Elapsed; End의 Reason(호출 경로에 따라 추가).
- CastOutcome은 TargetUnavailable/FullHP/Applied를 구분하고 Applied에 Total·Duration·HP를 기록한다. HoTResult는 Superseded/TargetUnavailable/TargetDead/FullHP/Expired와 Elapsed·Duration·ActualHealed 및 대상이 있는 경로의 HP를 기록한다.
- snapshot에는 Aim/marker offset/clip duration이 있으나 공통 진단 로그는 그 값을 출력하지 않는다. 전송 필드와 로그 필드를 혼동하지 않는다. 이 구현의 로그만으로 모든 HP 틱/텍스트 Emit을 exact하게 측정한다고 쓰지 않는다.

### 9.3 최종 검증과 관찰 — 메인 직접 관찰/인계

| 경계 | 결과 및 제한 |
|---|---|
| 최종 정적 컴파일 | Runtime errors0/warnings20, Editor0/0, PlayerDevelopment0/20, Release0/24; 각 exit0. 메인 인계이며 경고0/실기 PASS 주장이 아니다 |
| 새 Unity assembly/reload | ScriptAssemblies 04:49:46, reload 뒤 Console errors0/warnings20 |
| 새 Unit Action 메뉴 | Console **04:52:02**, full UAS-DIAG/Production/C2/C3 **자동 PASS**, Console errors0/warnings25. 새 Bloom timeline 근거 Editor.log **4873940**, full PASS **4873951**. 구4143312 결과를 재사용하지 않음 |
| 실제 Client boundary fixture | Run()의 새 boundary 호출(line67)이 PASS 로그 전에 실행. 실제 NGO 직렬화 → NetworkUnit receive/current-value → View 적용·late init, 중복/priority/종료·terminal·marker 소비 및 Client Root 무변경 fixture를 포함. 이는 자동 adapter 증거이지 실제 네트워크 경기 증거가 아님 |
| 새 Root 메뉴 | Console **04:54:03**, Editor.log **4873995**, Root Cross Audit **SelfValidate PASS**. 실제 경기 formal CrossAudit Analyze가 아님 |
| 이번 빌드 시도 | 메인이 File → Build And Run을1회 클릭. 즉시 화면에서 **Checking prerequisites / Starting Android build** 관찰. 승인된 시도/시작 관찰이며 **빌드 완료·설치·실행 성공은 미관찰**. 사용자 지시로 모든 UI/build polling 중단 |
| 사용자 실기 | 새 구현 재테스트 **OPEN**. 이전 session9a2df951...3962e 모션 FAIL 해결·Heal/HP exact 상관·Host/Client 실제 동시성은 미확인 |

### 9.4 전체 구현 파일 목록 — 코드9 + 신규 meta3

- [추가] `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitHealCastPresentation.cs` — 별도 힐 입력·cursor, 수락/재생/marker 소비/close/catch-up
- [추가] `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitHealCastState.cs` — NGO atomic 직렬화
- [추가] `Assets/_Project/Scripts/Application/Combat/UnitHealCastDiagnostics.cs` — Editor/Development 전용 상관 로그
- [수정] `Assets/_Project/Scripts/Application/Events/GameEvents.cs` — typed 힐 시작/종료 이벤트
- [수정] `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs` — 서버 snapshot 게시·현재값 Client 적용·late init/종료 복구
- [수정] `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs` — 서버 힐 시작/종료 구독·역할/수명 확인·priority 게시
- [수정] `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` — 힐 회차 발급·Animator/cursor·one-shot marker·Walk/유휴 감시 소유권·생명주기
- [수정] `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` — 기존 CastHeal/HoT에 진단 상관 입력·결과 로그 추가
- [수정] `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` — production 힐 Client boundary fixture 추가

신규 companion meta(3개):
- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitHealCastPresentation.cs.meta`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitHealCastState.cs.meta`
- `Assets/_Project/Scripts/Application/Combat/UnitHealCastDiagnostics.cs.meta`

이 목록은 이번 힐 동기화 후속 구현이며 §5의 이전 marker/binding/gate3파일 작업과 구분한다. 문서 담당자는 위 코드/meta를 수정하지 않았다.

### 9.5 문서 변경 목록 — 구현 파일과 별도

이번 전체 문서 변경 대상(7개; 상시/task6개 + 별도 즉시 기록 및 최종 인계 보강1개):
- `Assets/_Project/Docs/_Tasks/2026-10-03/03_28_bloomfairy-heal-timing-correction/Research.md`
- `Assets/_Project/Docs/_Tasks/2026-10-03/03_28_bloomfairy-heal-timing-correction/Plan.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md` — 규칙22/32 힐 전용 값 복제 계약
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` — SeparateHeal 예외 근거
- `Assets/_Project/Docs/StatsReference.md` — Bloom 상태/비고, 원수치 불변
- `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md` — Bloom 상태/검증 경계
- `.claude/mistakes/unit-action-correction.md` — 2026-10-03 사건 목차·본문 추가 및 최종 구현/자동 검증/빌드 시도 인계 append

별도 명시 승인으로 실패 기록 사건을 추가했고 최종 인계 보강도 같은 사건 끝에 append했다. 초기 구현대기 상태는 이력으로 남기고 최신 구현·자동 검증 PASS/빌드 시도/runtime OPEN을 분리했다. 기존 메모리·상태 문서·TC/QA는 수정하지 않는다. Git·앱 도구·추가 빌드 polling도 수행하지 않는다. **구현 및 두 새 자동 메뉴 PASS / 빌드 시도·시작 관찰 / focused runtime 재검증 OPEN**을 최종 경계로 유지한다.

## 10. 최신 테스트 인계와 남은 검토 — OPEN (2026-10-03)

분석 후 갱신만 승인된 단계다. 이번에는 코드·빌드·규칙/수치 변경을 하지 않는다. §9의 구현/자동 PASS/빌드 시작 관찰 및 이전 FAIL을 이력으로 보존하며, 최신 원시 로그·세부 소스/Unit23 anchor는 **Research §10**을 따른다.

### 10.1 확인된 결과와 미검증 범위

- 세션 `6239bf4719287cdd515f7d5ac1f17bc7c903d4dbe1f25c25ff3e6c6393a9faf1`, Editor Host / Android Client, device12_39의 OLD04:02 제외/current12:36~12:39와 editor daily 같은 세션만. Bloom8기:4,8,18,23,34,38,50,54.
- HostStart50/HostPlay50/ClientPlay50은 lifetime-unit-sequence/target/serverStart가 일치하고 missing/extra/conflict/duplicate0. Marker49/MotionEnd45 각각. **전달·재생 일치는 확인했으나 방향·모션 연결은 미해결, OPEN**이다.
- CastOutcome49=Applied43+TargetUnavailable6; HoTResult43=Expired8+FullHP7+Superseded20+TargetUnavailable8; serverActualHealed4110. Client HP/text exact coverage 아님.
- Dispatch elapsed0.9665045~1.0258641초/평균1.0031611초. Client32/50은 Now<StartedAt(min-0.3732135초/max positive+0.3592207초), offset0 clamp 뒤 즉시 재생. clock regression/RTT/정확한 early visual timing은 미입증이다.
- MotionEnd→연속Play42쌍 Host0.901~3.020초/평균1.114881초, Client0.664~3.091초/평균1.094690초. 3초 clip end hold +4초 cycle의 약1초 정지는 자체 규칙 위반이 아니다.
- Unit23은 rev7 Walk/rev8 Held/rev9 Walk가 실제 Client에 수신됐다. **이번 rev9 누락 가설은 적용하지 않는다.** render frame/pose/facing angle 미확보라 육안 원인 확정은 아니다.
- HostMOVE82265 실패0과 ROOT local Host63/Client48 PASS는 heal continuity/facing PASS나 formal Analyze가 아니다. 별도 UI NRE는12:37:12.424 첫 힐 이전1건(hook/raw 중복), 힐 끊김 인과 미확인.

### 10.2 다음 검토 제안 — 승인된 구현이 아님

1. 서버 Action guard 뒤 heal Root 즉시 회전과 Units 규칙12·15의 점진 회전 정합 검토. **heal-only server gradual facing**은 제안·미구현이며 새 Client Root/Visual writer나 일반attack5도 gate 추가를 허가하지 않는다.
2. future-start 입력을 offset0으로 즉시 재생하는 현 경계 검토. **future-start wait**는 제안·미구현; 시계 차이를 곧바로 RTT/조기 화면량/clock regression으로 부르지 않는다.
3. inactive Frozen/Held/Walk 복원과 실제 Walk-Held-Walk 수신 순서의 **stationary handoff** 검토. 제안·미구현이며 frame/pose/facing 증거 없이는 정확한 육안 원인을 확정하지 않는다.

규칙22·32/SeparateHeal 최종 pose hold, 준비1초+발동 후3초=4초, HoT200/3초·연구 배율·서버 writer·diff 틱·갱신/종료 텍스트 정책을 유지한다. 모션 close와 HoT 종료를 합치지 않는다. 후속 코드 교정·새 검증·빌드는 이번 승인에 포함되지 않는다.

### 10.3 문서·메모리 검증 계획

이번 변경은 task2개와 관련 상태 문서4개, 기존 연결 토픽/인덱스/공용 메모리의 지속적 증거 경계에 한정한다. 세션 수치/WIP는 메모리에 복제하지 않는다. 기존 이력을 삭제하지 않고 새로운 검사결과는 실제 실행 후 보고한다. `check_docs.py`는 task를 제외하므로 Research §10/Plan §10은 수동 정합 검토하고, 메모리 baseline은 직접 편집 없이 증가분을 검사기의 `--update-baseline`로만 반영한다. 전체 PASS/CLOSED는 기록하지 않는다.

## 11. 승인된 힐 방향·연속 모션 교정 계획 — 2026-10-03

### 11.1 승인 범위와 유지 계약

§10의 미구현 제안 단계 뒤 사용자가 기존 task 수정→교정 구현→빌드 **시도**를 명시 승인했다. 기존 최종 pose hold 정책의 변경도 설명 후 승인됐다. 따라서 이 절이 최신 승인 계획이며, §1~§10의 marker-only/hold 유지/후속 구현 미승인 기록은 당시 이력으로 보존한다. **계획 승인이지 구현 완료·새 자동 PASS·실기 PASS가 아니다.**

- marker는 정확히1개, 1.000초(30fps **1:00**)를 유지한다. 서버 준비1초+발동 후 cooldown3초=총4초 주기, HoT200/3초, 연구 배율·서버 단일 HP writer·diff 틱·갱신/종료 텍스트 정책은 바꾸지 않는다.
- 현재 생산 클립3초(30fps **3:00**)를 목표4초(**4:00**)로 늘린다. 원본0~1초를 보존하고 발동 후1~3초 구간만1~4초로 늘린다. 전체 clip speed 변경으로 marker나 준비 시간을 옮기지 않는다.
- 기존 일반 힐의 끝프레임 강제 Play/speed0 hold를 대체한다. 실제 Frozen/Held/대기·사망·disable/despawn·경기 종료의 우선순위와 terminal close는 유지한다. 모션 종료와 이미 부여된 HoT 종료를 합치지 않는다.
- 일반 공격의 replicated Attack 레벨 시작 차단·C3/SourceOnly 정책은 그대로다. 힐 전용 교정을 일반 공격 우회로로 확장하지 않는다. **일반 공격5도 gate를 힐에 임의 추가하지 않는다.**
- 기존 로직은 검증 전 삭제하지 않는다. 대체될 힐 snap/endpoint hold 등은 최소 범위에서 비활성화하고 기존 경로·복원 근거를 보존한 뒤 실제 구현 인계에서 지점을 명시한다.

### 11.2 구현 연결과 5초 주기 회귀 차단

1. **UnitFactory Server/Client 양 경로 cooldown 보존:** 현재 두 초기화 경로(조사 시278/383줄)는 Attack clip.length를 AttackCooldown에 덮어쓴다. 클립만4초로 바꾸면 준비1초+cooldown4초=5초 회귀가 생기므로 힐러 분기로 config cooldown3초를 보존해야 한다. 일반 공격의 기존 clip-derived cooldown 정책은 유지한다. 양 생성 경로와 준비/쿨다운 합산을 회귀 fixture로 검증한다.
2. **native 클립 retime:** 예정 Setup은 AnimationUtility로 키 시간 `t<=1`을 보존하고 `t>1 → 1+(t-1)*1.5`로 변환한다. 후반 tangent는 시간 확대의 역수로 보정하고1초 boundary key를 보장한다. marker1초·원본 전반·후반 끝4초와 생산 binding을 검증하며, 안전한 보존을 입증할 수 없는 곡선/입력은 fail-closed한다. Walk·원본 FBX·다른 유닛 클립을 바꾸지 않는다.
3. **연속 힐 handoff:** 다음 유효 힐 target이 있으면 중간 inactive Walk/Held를 발행하지 않고 새 sequence로 이어간다. 새 sequence는 재생하고 같은 sequence/revision은 멱등 소비한다. Blue/Red prefab의 힐 전용 blend **0.15초(30fps 4.5프레임)**는 시험값이며 최종 시각 검증된 값이 아니다. target이 없거나 실제 Frozen/Held 대기이면 상태 복원은 유지한다.
4. **서버 점진 facing:** 기존 서버 RotateTowards 경로/일반 점진 회전 원칙을 재사용한다. 힐 시작 snap을 대체하되 서버 Action 회전 소유권 guard를 지키고, Client Root/Visual offset writer를 추가하지 않는다. Aim payload 전달은 Client 회전 허가가 아니다.
5. **future-start wait:** 힐 cursor/View는 Now<StartedAt인 새 회차를 offset0 즉시 재생하지 않고 시작 시각까지 대기한다. 대기 중 최신 atomic snapshot·sequence/revision·lifetime·terminal/Frozen/Held를 재검증한다. late spawn/init은 current-state 복원과 경과시간 catch-up을 유지하고 지난 marker를 재발행하지 않는다. 전역 clock 교정이나 일반 공격 지연 정책 변경이 아니다.

### 11.3 예정 구현 파일 및 문서 파일 분리

기존 코드4파일(예정, 이 문서 단계에서는 수정하지 않음):

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitHealCastPresentation.cs`
- `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

새 Setup 예정파일: `Assets/Editor/Setup/RetimingBloomFairyHealPresentation.cs` 및 `.meta`. native retime 실행으로 변경될 예정 에셋은 `Assets/_Project/Animations/Units/BloomFairy/BloomFairy_Attack.anim`, `Assets/_Project/Prefabs/Units/Transcendence/Unit_BloomFairy_Blue.prefab`, `Assets/_Project/Prefabs/Units/Transcendence/Unit_BloomFairy_Red.prefab`이다. 새 Setup/에셋은 구현 담당 범위이며 문서 담당은 생성·실행하지 않는다.

이번 계획 단계의 실제 문서 변경은 기존 Research.md/Plan.md 두 파일뿐이다. 최종 구현 인계 후 GameSystemRules 인덱스와 Units22/32·SeparateHeal·StatsReference/Matrix를 승인 정책과 실제 구현 경계에 맞춰 후속 동기화한다. 이번 단계는 메모리·baseline·신규 task/TC·QA·Git/UI/빌드를 변경하지 않는다.

### 11.4 검증과 인계 gate

규칙 근거는 Units12·15의 점진 회전, 18의 서버 타이머 권위, 22의 상태 동기화/current-state 복원, 32의 힐 전용 계약 및 33~37의 서버 효과·표식·주기 계약과 UnitCombatSynchronization의 서버 권위/SeparateHeal 표현 분리다. 회전 권위와 Root writer 소유권의 근거는 NET-AUTH/ROOT 계약으로 구분한다. 마지막 pose hold 변경은 사용자 승인된 힐 전용 정책 변경이며 상시4문서의 최종 동기화는 구현 인계 뒤 수행한다. 일반 공격 정책의 예외 확대가 아니다.

주요 위험은 클립 길이의 cooldown 재주입에 따른5초 회귀,1초 경계 curve/tangent 변형, blend 중 marker 중복·누락, future-start 대기 중 stale/terminal 소비, 실제 Frozen/Held를 연속 힐로 오인하는 것이다. 해당 회귀 gate를 통과하기 전 해결 확정이나 기존 경로 삭제를 하지 않는다.

- 생산4초/30fps, 단일 marker1초, 전반0~1초 보존·후반 retime/tangent/boundary와 Blue/Red binding·힐-only blend를 실제 Setup 결과로 확인한다.
- Server/Client UnitFactory cooldown3 유지와 총4초 회차, 일반 공격 cooldown 무변경을 확인한다. serialization→NetworkUnit receive→View 및 late-init/current snapshot, future-start wait/cancel, 같은 회차 멱등·중복 marker 차단·새 회차 blend, 실제 Frozen/Held/death/end 복원을 회귀 검증한다.
- 힐 server gradual facing의 소유권, Client Root writer 없음, 연속 회차 중 Walk/Held 비삽입과 실제 대기 복원을 확인한다. §10의 exact Start/Play join이나 local ROOT/MOVE PASS를 새 facing/continuity PASS로 재사용하지 않는다.
- 최종 파일 목록·새 static/Unity 검사 결과는 메인 확정 인계 후 기록한다. 기존 §9 자동 PASS는 이번4초 교정의 PASS가 아니다. 빌드는 승인된 **시도**만 기록하고 완료·설치·실행을 추정하거나 UI/build polling하지 않는다.
- 후속 동일 경기의 회차 전달, 방향·연속 모션·대기 handoff 시각 검증과 Client HP/text 정밀 상관검증은 OPEN이다. 전체 PASS/CLOSED·Supported 승격 없음.

## 12. 승인 교정 최종 구현·에셋·자동 검증·빌드 시도 인계 — 2026-10-03

### 12.1 완료 범위와 남은 실기 검증

메인이 승인 범위의 기존 코드4파일 및 새 Setup.cs/.meta 작성 완료와 마지막 수정 뒤 정적 컴파일4경로 오류0을 확인했다. native Setup은17:42:31 `[BloomHealSetup] native retime PASS`(changed=True, curves=130, front/tail samples preserved, clip=4s, marker=1s, both heal blends=0.15s)로 완료됐다. 디스크 양 prefab `_healCastBlend: 0.15` 및 OnAttackHit time1 확인. 클립/prefab3파일은 실제 변경이다. 이는 에셋/retime 검증이지 visual PASS가 아니다. **새 코드/에셋 자동 검증 완료:** Unit Action17:43:56의 BLOOM-HEAL-TIMELINE·UAS overall·production·C2·C3 PASS, Root Cross Audit SelfValidate17:44:41 PASS. Console errors0/warnings25(기존 compile20+fixture5)이며 Android warnings0·시각 실기 PASS를 뜻하지 않는다. native 디스크 m_StopTime4/m_SampleRate30/단일 OnAttackHit time1 및 양 prefab blend0.15 확인. **빌드 시도 완료:** File→Build And Run1회 실제 클릭 직후 Importing(iteration2)–Finishing import Settings/Mobile_RPAsset.asset 관찰까지만 확인했다. 이후 Unity UI/Editor/build log polling 및 완료·설치·게임 실행 확인은 하지 않았다. 빌드 성공 추정 없음. Setup·자동 메뉴·빌드 시도는 완료이며, 남은 시각 실기 검증은 OPEN이다. §9의 이전 자동 PASS·빌드 시작 및 §10의 이전 경기 증거는 보존하되 이번 교정의 새 PASS로 재사용하지 않는다.

**이전 대기 관찰(후속 완료 전 이력):** Unity Refresh 이후 새 DLL 생성 시각은17:26:34/35로 인계됐다. 당시 domain load 완료는 확인되지 않았고 **Reloading Domain busy26초 진행 화면**이 관찰됐다. DLL 생성만으로 domain load 완료·native Setup 실행·Unity 메뉴 성공을 판단하지 않았다. 현재 domain load/Setup/자동 메뉴 완료 상태는 아래 후속 관찰 정정과 §12.4를 따른다.

**후속 관찰 정정:** 실제 domain reload 및 Google startup 종료 뒤 현재 로드 지연은 해소됐다. 지연 원인은 확정되지 않았다. native Setup 실행 결과는17:42:31에 확인됐으며 두 자동 메뉴 및 빌드 시도 완료의 최신 확정 근거는 §12.4를 따른다.

### 12.2 코드 구현 인계 내용

- server Update의 IsHealer 분기는 서버+Action 소유권+활성 priority1 경계에서 공통 `RotateActionTowards`를 사용한다. 기존 힐 snap은 주석으로 보존하며 Client Root/Visual offset writer는 추가하지 않는다.
- 연속 힐 target 재탐색 뒤 다음 유효 target이 있으면 `EndHealPresentation(cycle-complete)` inactive를 끼우지 않고 바로 다음 sequence로 이어간다. 실제 target 없는 대기·Frozen/Held·terminal은 유지한다.
- Maintain은 IsDue 전에 힐 모션을 재생하지 않는다. priority cache는 실제 적용 후 갱신한다. 기존 endpoint 강제 Play/speed0 hold는 주석으로 보존하고 일반 힐 speed1을 사용한다.
- UnitFactory 양 생성 경로는 shared `ApplyAttackClipCooldown`을 통해 IsHealer config cooldown3초를 보존한다. 일반 유닛은 clip-derived cooldown을 유지한다. 준비1초+post3초=cycle4초이며 clip4 덮어쓰기의5초 회귀를 차단한다.
- future-start/priority-pending/terminal/current-value/cooldown/native-retime-helper fixture를 보강했다. fixture 코드 작성 및 정적 컴파일은 새 Unity 실행 PASS와 구분한다.
- 새 Setup은 native AnimationUtility staging 검사 후 `t<=1` 보존, `t>1 → 1+(t-1)*1.5`, 후반 tangent 역수 보정·curve boundary 검증을 거쳐4초 클립과 양 prefab 힐 전용0.15초 시험 blend를 저장하도록 작성됐다. **17:42:31 native Setup을 실제 실행하여 위 저장과130곡선 front/tail sample 보존을 확인했다. 시각 PASS는 아니다.**
- marker1초(30fps `1:00`), HoT200/3초·연구 배율·서버 단일 writer·diff 틱/텍스트 정책은 불변이다. 일반 EnemyAttack/C3/SourceOnly 안전 정책은 변경하지 않는다.

### 12.3 전체 파일 목록 — 코드 완료 / 에셋 적용 완료 / 문서 분리

**작성 완료 기존 코드4파일:**

1. `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
2. `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitHealCastPresentation.cs`
3. `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs`
4. `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

**생성 완료 Setup2파일:**

5. `Assets/Editor/Setup/RetimingBloomFairyHealPresentation.cs`
6. `Assets/Editor/Setup/RetimingBloomFairyHealPresentation.cs.meta`

**native Setup 실행으로 실제 변경된 에셋3파일(17:42:31 적용 확인):**

7. `Assets/_Project/Animations/Units/BloomFairy/BloomFairy_Attack.anim`
8. `Assets/_Project/Prefabs/Units/Transcendence/Unit_BloomFairy_Blue.prefab`
9. `Assets/_Project/Prefabs/Units/Transcendence/Unit_BloomFairy_Red.prefab`

**관련 기존 문서6파일 — 구현 코드 목록과 별개:**

- `Assets/_Project/Docs/_Tasks/2026-10-03/03_28_bloomfairy-heal-timing-correction/Research.md`
- `Assets/_Project/Docs/_Tasks/2026-10-03/03_28_bloomfairy-heal-timing-correction/Plan.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UnitCombatSynchronization.md`
- `Assets/_Project/Docs/StatsReference.md`
- `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`

상시4문서는 승인 정책/코드 인계와17:42:31 native Setup/에셋 저장·17:43:56/17:44:41 새 자동 PASS·빌드 시도 관찰 경계로 갱신한다. **문서 담당의 이번 수정 범위**는 task2+상시4문서뿐이며 문서 담당은 메모리/baseline·새 task/TC/QA·코드/에셋·Git/UI/빌드를 변경하거나 실행하지 않았다. 전체 작업의 코드/에셋 변경·Unity 실행·빌드 시도는 메인/구현 담당의 확정 인계로 위에 기록한 것이다.

### 12.4 정적 결과와 새 자동 검증·빌드 시도

| 경로 | errors | warnings | 결과 경계 |
|---|---:|---:|---|
| Runtime | 0 | 20 | 마지막 코드 수정 뒤 정적 컴파일 |
| Editor | 0 | 0 | 마지막 코드 수정 뒤 정적 컴파일 |
| PlayerDevelopment | 0 | 20 | 마지막 코드 수정 뒤 정적 컴파일 |
| Release | 0 | 24 | 마지막 코드 수정 뒤 정적 컴파일 |

**새 코드/에셋 자동 검증 완료:** Unit Action17:43:56의 BLOOM-HEAL-TIMELINE·UAS overall·production·C2·C3 PASS, Root Cross Audit SelfValidate17:44:41 PASS. Console errors0/warnings25(기존 compile20+fixture5)이며 Android warnings0·시각 실기 PASS를 뜻하지 않는다. native 디스크 m_StopTime4/m_SampleRate30/단일 OnAttackHit time1 및 양 prefab blend0.15 확인. **빌드 시도 완료:** File→Build And Run1회 실제 클릭 직후 Importing(iteration2)–Finishing import Settings/Mobile_RPAsset.asset 관찰까지만 확인했다. 이후 Unity UI/Editor/build log polling 및 완료·설치·게임 실행 확인은 하지 않았다. 빌드 성공 추정 없음. Setup·자동 메뉴·빌드 시도는 완료이며, 남은 시각 실기 검증은 OPEN이다.

## 13. 18_34 세 observer run 후속 분석·현재 상태 — 2026-10-03

### 13.1 확정 인계와 판정

**전달·타이밍 seam 확인 / facing FAIL(사용자 보고), 원인 OPEN.** 기존 Research §13이 원시 자료·전체 runId/BEGIN anchor·exact 수치·source 감사의 권위 기록이다. §12 구현/native Setup/새 자동 gate/빌드 시도 이력을 보존하되 실기 facing PASS나 빌드 성공으로 확대하지 않는다.

| Run | 시간 / shared key 경계 | Bloom IDs | Host Start/Play=Client Play | marker Host/Client | Client early Play |
|---|---|---|---:|---:|---:|
| A |18:24:56~18:27:23 /4427b92c…a8089 |25,28,43,60 |20 |19/19 |0 |
| B |18:28:37~18:30:25 /3a936e70…aef90 |83,89,91,95 |19 |19/19 |0 |
| C |18:30:26~18:32:45 /B와 같은 key이나 새 BEGIN/run |106,108,114,117,122,129,137 |85 |84/84 |0 |

B/C는 같은 shared key라도 observer run이 다르므로 반드시 분리한다. 합124회 exact join에서 각 run missing/extra/conflict/duplicate0. 음수 Active Receive는 revision 포함 값이며 새 sequence-only나 early Play를 뜻하지 않는다. 실제 Play clock delta·서버 Dispatch elapsed는 다른 경계이고 RTT/정확한 시각 동기화 증거가 아니다.

### 13.2 닫지 않는 검증 경계

보고된 HoT completed subset/ActualHealed는 Client HP/text 전수 검증이 아니다. Self Start는 규칙33 self 허용·zero-distance yaw 유지와 양립한다. 세 run local ROOT PASS·MOVE 실패0은 formal CrossAudit·heal target-facing/model-pose PASS가 아니다. 첫 run 뒤 다음 BEGIN 전의 connection ERROR는 heal/회전 증상 인과 미확인이다.

zero-distance source 조건은 XZdir.sqrMagnitude < .001인 모든 coincident target이며 self 전용 분기가 아니다. Self Start20회 측정과 다른 아군 겹침으로 이 분기가 실행됐을 가능성(미확정)을 구분한다.

source의 Action transfer/release·서버 전용 공통 점진 회전·actual target Root 조회는 확인됐으나, target cache 시작1회/회차 중 재조회 없음은 source 제약일 뿐 이번 발생 증거가 아니다. 기존 로그에 yaw/aim/angle/guard skip가 없고 ROOT 표본은 sparse projection 경계여서 사용자 방향 증상을 재현·원인 확정하지 못했다.

**Aim 의미 추가 source 근거:** `PlayHealCastAnimation`의 AimX/Z는 HealTarget cache 설정 후 state 구성 시 현재 `transform.forward`에서 얻는 값으로, 힐 대상 방향이 아니다. Client는 이 Aim으로 회전하지 않는다. 향후 expected yaw는 실제 서버 caster→target 벡터에서 계산하고 payload Aim과 구분해야 한다. 이것 자체나 cache/guard/reacquire 구조를 이번 방향 오류의 확정 원인으로 단정하지 않는다.

### 13.3 다음 단계 제안과 승인 경계

별도 승인 후 힐 시작·marker·종료/회전 수렴에 server caster/target position, expected/current Root yaw·angle·ownership/guard skip/cache/alive/self 및 Client Root/Visual/rendered forward의 exact 회차 trace를 추가하는 제안만 남긴다. **제안·미구현이며 모델 offset 변경/Client Root writer fix를 승인하지 않는다.**

native4초(30fps4:00)·marker1초(1:00)·힐-only0.15초 시험 blend·server prep1+post3=cycle4·HoT200/3·단일 서버 writer 및 규칙/원수치는 유지한다. 사용자 영구 선호에 따라 분석 결과 문서 반영은 재질문 없이 진행하지만 분석 요청은 코드·에셋·빌드 권한이 아니다. 이번 변경은 기존 문서/연결 메모리만이며 신규 task/TC/QA·코드/에셋/UI/빌드/Git 없음. Research §13/Plan §13은 수동 정합 확인하고 나머지는 checker로 검사한다.

## 14. 승인된 힐 방향·실제 시점 진단 로그 계획 — 2026-10-03

### 14.1 목적·승인·현 상태

사용자가 “우선 로그 추가해서 확인해보자”라고 명시 승인했다. §13의 각도/소유권/실제 marker trace 제안 중 아래 **읽기 전용 개발 진단 구현**을 승인 계획으로 구체화한다. 새 task를 만들지 않는다. self-heal 방향 의심은 검증할 가설이지 원인 확정이 아니다. **PLAN READY / 진단 코드 구현·새 로그·실기 결과 대기**, facing FAIL(사용자 보고)·원인 OPEN을 유지한다.

현재 타이밍 확인은 두 층이다. §12 native 저장/자동 gate는 marker1초(30fps1:00)·clip4초(4:00)·blend0.15초와 config 연결을 확인했고, §13 실기는 exact Start/Play와 서버 dispatch elapsed 및 early Play0을 확인했다. 이것은 **실제 marker frame→첫 HP 적용→렌더 화면 동시성의 exact 증거가 아니다.** 이번 로그로 실제 사건을 분리해 연결하며 관측할 수 없는 경계는 미확보로 남긴다.

### 14.2 규칙 근거와 변경하지 않는 영역

`LogRules.md`의 런타임 두 축·형식/stripping/금지(1.2·1.4~1.7·1.14), Units 규칙12·15의 점진 회전·18의 서버 타이머 권위·22의 current-state 복원·32~37의 힐 전용 서버 writer/self/HoT/주기/텍스트, Synchronization SeparateHeal 및 NET-AUTH/NET-ROOT/NET-TIME의 권위·표현 시간축 분리를 직접 확인했다.

- 데이터/pose를 관측만 한다. 새 Root/Visual/Animator writer·회전 gate·target 재탐색/재획득·회차 흐름·HP/HoT 정책을 진단 편의로 바꾸지 않는다.
- self 허용, near-zero 기준, 서버 prep1+post3=cycle4·HoT200/3초·marker1초·native4초·0.15초 시험 blend를 유지한다. 일반 EnemyAttack/C3/SourceOnly 및 일반 공격 각도 gate를 변경하지 않는다.
- 모델 offset 변경/Client Root writer fix는 범위 밖이다. gameplay 판단에 diagnostic flag/각도/표본 예산을 사용하지 않는다.

### 14.3 회차·방향 필드와 bounded checkpoint

기존 힐 diagnostic 경로를 우선 확장한다. 예정 필드명과 세부 schema/파일·예산은 구현 담당의 최종 인계로 확정한다.

1. **상관 키:** peer role, observer runId/startedAt·공유 식별 경계, caster lifetime/unitId/sequence/revision, targetId, phase/reason, server/synchronized/local 관측 시각과 frame. 같은 shared key라도 새 run은 분리한다. 인증/계정 session token·UID 원본은 기록하지 않는다.
2. **시작 baseline:** server caster/target 실제 Root position과 XZ delta·sqrMagnitude, self 여부·alive/available/cache target identity, Root yaw/forward와 시작 직전 baseline, payload Aim/forward를 별도 필드로 기록한다. `XZdir.sqrMagnitude < .001`은 self뿐 아니라 모든 coincident target에 적용된다. near-zero에서는 expected target yaw/angle을 **undefined/not-applicable**로 표시하고 시작 yaw 유지 여부를 관측한다. 임의 expectedYaw0으로 실패를 만들지 않는다.
3. **정상 거리 expected 방향:** 실제 서버 caster→target 벡터에서 expected yaw를 계산하고 current Root yaw·angle을 기록한다. payload AimX/Z=start current forward를 target expected direction으로 재해석하지 않는다. 회차 시작 cache와 실제 조회 결과를 구분하되 로그용 재조회로 gameplay cache 동작을 고치지 않는다.
4. **회전 checkpoint/수렴/guard skip:** 시작·실제 marker·회차 종료와 rotation eligibility/ownership/priority/target availability 전이, 최초 수렴/near-zero·guard reason 변화 경계만 기록한다. server role·Action ownership·priority·freeze/held·target cache/lookup 성공 여부와 회전 전후 yaw/angle을 함께 남긴다. 같은 reason을 매 프레임 반복 출력하지 않는다. checkpoint 개수·active observer 상태/필드 크기에 명시적 상한을 두고 dropped/overflow·미완료 coverage를 드러낸다. 진단 예산 고갈로 gameplay를 막지 않는다.
5. **Client 관측:** 동일 회차의 실제 재생 시작/marker/종료 및 필요한 bounded checkpoint에서 replicated Root position/yaw/forward, VisualRoot/local/world direction·projection/flip 상태·rendered-forward proxy, Animator state/normalized time·frame을 관측한다. target vector는 진단 참고로만 사용하며 Client 회전·HP 판정에 쓰지 않는다. rendered-forward proxy는 bone pose/실제 픽셀 방향/사용자 시각의 확정 증거가 아니다. 잡을 수 없는 항목은 unavailable로 남긴다.

### 14.4 실제 marker·dispatch·첫 HP 적용의 연결

- 기존 accepted one-shot 힐 marker 소비 경계에서 **실제 marker 관측**을 기록한다. expected marker offset1초와 실제 callback/소비 시각을 구분하고 source marker를 추가 발행하거나 다시 소비하지 않는다. late catch-up에서 이미 지난 marker가 없으면 missing/skip reason을 기록할 뿐 가상의 시각을 만들지 않는다.
- 서버 `CastHeal` dispatch/outcome와 가능하면 해당 부여 효과의 **첫 실제 positive HP delta 적용**을 기존 writer 안의 read-only seam으로 연결한다. HP before/after·delta, effect identity/target·origin cast key, writer 시각 및 종료/갱신 사유를 기록한다. 첫 tick 호출/HoT 등록을 실제 HP 회복으로 바꾸어 부르지 않는다.
- **확정 범위:** 기존 회차/effect key로 실제 marker·dispatch/outcome·writer-owned 첫 positive tick을 연결하는 것으로 충분하다. exact Client HP를 얻기 위한 RPC/event schema 추가는 하지 않는다. exact Client HP 및 VFX pixel onset은 이번 범위에서 unavailable이며 실제 화면 동시성을 만들어 주장하지 않는다.
- 갱신/풀피/사망·Superseded로 positive 적용이 없거나 effect 수명이 cast 모션을 넘어가면 별도 결과로 남긴다. 기존 lifetime/회차→effect origin을 안전하게 연결할 수 없는 seam은 correlation unavailable로 보고하고, 구현자가 필요 파일/수명 비용·근거를 최종 인계한다. fabricated C3 scope·새 gameplay writer·effect retarget은 금지한다.
- 같은 서버 clock domain의 marker/dispatch/첫 HP 경계와 Client synchronized/local/frame 경계를 표시한다. 비교 가능한 시계끼리만 delta를 산출하며 receive/Play delta를 RTT·정확한 조기 시각·네트워크 지연량으로 부르지 않는다. Client HPbar/text 수신·렌더 전수가 없으면 정확한 화면 동기화 PASS를 쓰지 않는다.

### 14.5 로그 구현·검증·최종 인계

정상 checkpoint는 **Info + 개발**, bounded observer 증거 overflow/format 불완전은 gameplay 실패와 구분하여 개발 진단 coverage로 보고한다. 기존 `GameLog.Dev`/diagnostic facade와 파일/Console sink를 재사용하고 raw Debug.Log/매 틱·매 프레임 스팸·값 안의 ', ' 구분자 혼입을 피한다. UNITY_EDITOR/DEVELOPMENT_BUILD stripping과 block-body callback을 지키며 release에서 문자열/observer state 비용이 남지 않는지 검증한다. 민감 데이터 마스킹과 lifecycle retire·bounded state 정리를 유지한다.

구현 담당은 기존 helper·View·서버 HP seam을 조사한 뒤 **실제 파일 목록/진단 필드·상한/기존 정적 컴파일 결과 및 최종 diff 검토**를 인계한다. 검토 대상은 zero-distance/self·정상 거리, guard skip/priority 변화·late-init/current snapshot/duplicate·death/end·첫 positive HP 없음/갱신·bounded overflow의 관측 경계다. **새 회귀 fixture/self-validator 작업, validator/menu 실행, TC/QA 파일 생성은 이번 승인 범위가 아니다.** 새 Unity 메뉴 실행을 계획하거나 PASS로 기록하지 않는다. 변경은 진단 관측만이며 실제 코드 목록은 최종 인계로 확정한다.

이번 계획 문서 변경은 기존 Research/Plan 두 파일뿐이다. 코드·에셋·상시 정책/스탯·메모리·빌드/Git/UI 변경 없음. 최종 구현 인계 뒤 같은 task에 실제 파일/검증 상태를 추가하고 필요 상태 문서를 좁게 동기화한다. checker는 최종 코드 인계 후 문서 갱신이 끝난 시점에 실행하며 task 자체는 수동 정합 확인한다. **이 단계에서 진단 구현 완료·새 PASS·증상 재현·원인 확정은 주장하지 않는다.**

### 14.6 최종 진단 구현 인계 — 코드4파일 / 정적4경로 오류0 / 실기 OPEN

메인의 patch/관측 seam 검토와 최종 진단 점검·정적 컴파일 인계가 완료됐다. 프로젝트 patch는 다음 기존 코드4파일뿐이며 최종 점검 담당은 추가 코드 수정을 하지 않았다. 새 validator/메뉴/빌드/실기 실행은 없다.

1. `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
2. `Assets/_Project/Scripts/Application/Combat/UnitHealCastDiagnostics.cs`
3. `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`
4. `Assets/_Project/Scripts/Presentation/Unit/VisualRootProjector.cs`

현재 인계된 구현 경계:

- 힐 회차 checkpoint는 최대24개, 각 checkpoint의 pose5줄과 전이 때만 FacingStep을 출력한다. 진단 scalar poll은0.25초이며 매 프레임 출력이 아니다. 전이 출력은 최대8개로 제한하고 overflow는 별도 record로 명시한다. 상한/진단 상태는 gameplay 권한이나 회전 정책으로 사용하지 않는다.
- self/near-zero는 `AngleErrorUndefined`로 분리한다. expected target angle을 임의0으로 만들지 않으며, 시작 baseline 대비 yaw Drift는 wrap-safe로 관측한다. payload Aim/current forward와 실제 caster→target expected 방향은 구분한다.
- server/Client Root·Visual/model transform 방향은 callback 시점의 **transform proxy**다. rendered frame/bone pose/VFX pixel onset의 직접 측정으로 부르지 않는다.
- 실제 accepted marker의 Now/frame과 서버 dispatch/outcome, 기존 writer가 적용한 **첫 실제 positive HP delta 이후**의 관측을 lifetime/unit/sequence/target 키로 연결한다. tick 호출/효과 등록을 실제 회복으로 대체하지 않는다.
- RPC/event schema 변경 없이 기존 키를 사용한다. exact Client HP·pixel onset은 unavailable이며 새로운 Client Root/HP writer나 모델 offset 교정은 아니다.
- 정책·스탯·회전/타겟/효과 수명·native clip4초/marker1초/힐-only0.15초·준비1+post3·HoT200/3·서버 writer는 유지한다.

**최종 정적 결과(이번 진단 patch):**

| 경로 | errors | warnings | 확정 근거 |
|---|---:|---:|---|
| Runtime |0 |22 |최종 점검 담당 인계 |
| Editor |0 |0 |메인의 보정 editor.rsp 재실행 exit0/output empty |
| PlayerDevelopment |0 |22 |최종 점검 담당 인계 |
| Release |0 |26 |최종 점검 담당 인계 |

Editor 첫 시도 실패는 임시 rsp에 기존 `RetimingBloomFairyHealPresentation.cs`가 빠진 compile 입력 문제였다. 담당이 source 입력을 보강했으나 중단 전 최종 결과는 확보하지 못했고, 메인이 `D:/dev/6000.3.5f2/Editor/Data/NetCoreRuntime/dotnet.exe`와 `DotNetSdkRoslyn/csc.dll`로 보정 editor.rsp를 재실행하여 위0/0을 확정했다. 이는 Unity 메뉴/runtime PASS가 아니다. 컴파일4경로 rsp/output artefact는 Temp만 사용했으며 프로젝트 코드 변경 목록에 넣지 않는다.

Compose의 typical upper838 UTF-8 bytes는 최종 점검 담당의 format 점검 인계값이며 실제 device line/수신 완전성 증거가 아니다. `HoTFirstPositive`는 기존 writer 뒤 첫 실제 positive HP 적용을 exact key로 기록한다. 실제 marker Now/frame·self/near-zero undefined angle·wrap-safe 시작 drift·24 checkpoint/8 transition/0.25초 poll 및 lifecycle 경계는 위 구현 내용을 유지한다.

**현재 판정:** 이번 진단 코드/정적 인계는 완료했다. 이전 §12 자동 gate PASS를 이번 진단 patch의 결과로 재사용하지 않는다. §13의 전달/Play seam 관측과 사용자 보고 facing FAIL·원인 OPEN은 보존한다. 새 facing/timing 실기 증거·증상 재현·원인 확정은 **OPEN**이며 로그 구현 자체를 해결 PASS로 쓰지 않는다. 새 Unity 메뉴·빌드 시도·실기 실행을 추가 주장하지 않는다.

이번 문서 담당 수정은 Research/Plan 두 파일뿐이다. 코드/에셋·상시문서·메모리/baseline·TC/QA·Git/UI/빌드 변경 없음. 최종 inventory/정적 인계·관측 경계는 수동 확인하고 checker는 나머지 문서 정합성을 검사한다(task 제외). broad 상태/메모리 갱신은 하지 않는다.
## 15. 최신 두 테스트 분석 인계와 다음 교정 제안 — 구현 승인 아님

### 15.1 목적과 최신 판정

새 진단 로그로 자기 회복 방향과 실제 회복 시점을 확인한 결과를 기록한다. 원시 자료/run identity·전체 수치·영상 관측 범위는 Research §15.1~§15.5가 단일 상세 anchor다. 22_48/23_04 device는 첫 테스트 전체 prefix가 중복되므로 **두 run만** 센다. Editor Host/Android Client의 Start→Client Play40/40 및70/70 exact missing/extra0은 전달 확인이지 전체 visual PASS가 아니다.

paired nonself marker subset29/41의 작은 Root 각도 오차와 self checkpoint의 Host drift0/Client 최대0.003도는 해당 callback 관측만 수용한다. projection/Visual/model transform proxy는 실제 rendered pixel/bone pose가 아니다. 사용자 방향 증상·원인은 OPEN이다.

### 15.2 early HP FAIL과 근거

A Unit5 self lifetime6/sequence1의 HP440→447이 marker보다0.19501369756233초 먼저, B Unit29→22 lifetime30/sequence4의 HP840→847이0.02927129393422초 먼저였다. 같은 Host Local clock의 writer-owned FirstPositive와 accepted marker exact 회차 비교다. Editor39369/39371~39372 및43394/43395~43396을 따른다. **TIMING EARLY HP FAIL**을 확정하며 exact Client HP/pixel onset이나 전 회차 오류로 확대하지 않는다.

B Unit12→43 lifetime13/sequence12/revision22의 큰 시작 각도 오차는 다음 checkpoint를 거쳐 marker error0으로 점진 수렴했다(Editor43849/43887/43892). 이 사례를 jump나 self 원인으로 재해석하지 않는다.

### 15.3 다음 제안·미구현 경계

A Unit5/lifetime6 sequence1→2는 End/no-target 기록 없이 Start interval3.722193초로 설계4초보다0.277807초 짧았다(Editor39321/39366/39426). 실제 cycle4의 exact 충족은 이 사례에서 성립하지 않는다. MotionEnd 부재/짧은 주기를 실제 rendered tail cut 또는 영상 증상의 원인으로 단정하지 않는다. B Unit29 sequence4→5의4.065436초는 중간 no-target End가 있어 연속 사례와 분리한다. 상세 Local clock 값은 Research §15.3을 따른다.

UnitView source5277 주변의 delta 누적은 첫 진입에 이전 frame delta를 먼저 더하고 절대 StartedAt+MarkerOffset을 기다리지 않는 구조다. 조기 dispatch/HP 증거와 부합하는 강한 원인 의심이나 첫 frame delta 미기록으로 정확한 lag·모든 runtime 인과 연쇄는 미확정이다.

**다음 사용자 승인 대상 제안:** 서버 absolute authoritative dueTime=StartedAt+MarkerOffset 판정으로 변경 검토. 서버 타이머를 권위로 유지하고 marker/Client를 HP writer로 삼지 않는다. 이는 **제안만이며 이번 코드 구현 승인/실행은 없다**. Units 규칙18(서버 타이머)·32(별도 힐/marker 연출)·33(self)·34~37(HoT/텍스트), Sync SeparateHeal·NET-AUTH/NET-ROOT를 보존한다. native4초/marker1초(30fps4:00/1:00)·0.15초 blend·준비1+post3·HoT200/3·정책/스탯 무변경. 신규 validator/fixture/Unity 메뉴·빌드도 없다.

### 15.4 영상·다음 증거 경계

메인 Movie_001.mp4 관측은162.47초/1080×1920/VFR 평균32.64fps: overview10초 간격,88~96초 crop0.5초 간격,118~130초1초 간격,24초 full resolution이다. 힐 유사 모션/녹색 완료 텍스트는 관측했지만 작은 유닛·HPbar/unitId/UTC 부재 및 PTS→UTC 동기화 부재로 exact frame↔key/HP timing 또는 pixels-facing PASS는 없다. B 문맥은 사용자 제공 구분이며 exact sync가 아니다. source 상수도 실측 timing PASS로 대체하지 않는다.

최신 기준은 전달/제한된 Root 관측 확인, EARLY HP FAIL, 시각 방향·Client/pixel timing·최종 원인 OPEN이다. §10~§14를 삭제하지 않으며 관련 상시 문서는 최신 evidence pointer만 보강한다. 최종 checker는 task가 제외되므로 이 절은 수동 정합 확인한다. 문서 담당 실제 변경 inventory를 별도로 보고한다.

## 16. 힐 발동 absolute due-time 교정 — 구현·정적/자동 gate·빌드 시도 인계 완료 / 실기 OPEN

### 16.1 목적·승인·이력

사용자의 “그렇게 진행해 빌드시도까지해” 승인은 힐 시전 시작 서버 시각에서1초가 지난 뒤에만 기존 힐 발동을 허용하도록 좁게 교정하고, 검증 후 빌드 시작을 시도하는 범위다. §15의 marker 이전 실제 HP 적용2사례와 연속 self3.722193초 관측은 이전 실패 증거로 보존한다. 정확한 첫 frame delta/lag 원인과 영상 모션 잘림 인과는 여전히 미확정이다. §15 당시 제안·미승인 문구는 당시 이력이며, **이 절에서 계획 승인으로 대체되지만 구현 완료로 대체되는 것은 아니다.**

### 16.2 예정 변경·규칙 근거

- **서버/싱글 힐 windup 대기만 교정:** 기존 시전 snapshot의 `StartedAt`과 현재 보존된 marker offset1초(30fps `1:00`)로 `dueTime = StartedAt + MarkerOffset`을 정한다. 동일 authoritative clock의 현재 시각이 dueTime 미만이면 기다리고, 도달 후 첫 유효 프레임에서 기존 dispatch/CastHeal을 회차당 한 번 실행하도록 계획한다. 별도 timer origin을 새로 잡거나 이전 frame delta를 준비시간에 더하지 않는다. 근거: Units 규칙18의 서버 타이머 권위·32의 독립 힐 타이머, Sync NET-TIME-001 및 NET-AUTH-001.
- **기존 delta 누적 준비 루프는 삭제하지 않고 주석 비활성화 보존:** §15의 조기 발동 및 짧은 연속 주기와 부합하는 unsafe timing seam을 대체한다. 사용자 실기 수용 전 제거하지 않는다. 되살린 두 경로가 동시에 실행되거나 double dispatch되지 않게 한다. 근거: CLAUDE 비활성화 우선 원칙, Units 규칙32의 단일 힐 발동·34의 서버 단일 효과 writer/이중 틱 금지.
- **발동 전 기존 유효성/취소 유지:** 시전자/대상 생명주기, 회차 식별, 사망·game end·disable/despawn·기존 Frozen/Held 처리와 target unavailable 결과를 유지한다. 이 변경으로 새 target 재선정·회차 부활·priority 우회가 생기지 않게 한다. 근거: Units 규칙22·32·33·35, Sync SeparateHeal/NET-AUTH-001. 기존 동결/대기 의미와 absolute deadline이 충돌하는 추가 정책 결정이 필요하면 구현 전에 메인/사용자에게 알린다.
- **발동 이후 경로 무변경:** 기존 CastHeal·HoT200/3초 diff·동종 갱신/종료·HP 동기화·완료 현재HP 텍스트·발동 후 cooldown3초를 보존한다. 쿨다운을 시작 포함3초로 바꾸거나 clip4초로 덮어쓰지 않는다. 근거: Units 규칙34·36·37. 설계 준비1+post3=4초를 유지하되 서버 frame overshoot와 실제 회차 간격을 실측 없이 exact4초 PASS로 부르지 않는다.
- **회전·표현 무변경:** 기존 서버 점진 RotateActionTowards/Action ownership 및 self/near-zero yaw 보존, Client Root 쓰기 금지, atomic snapshot/current-state 복구·future-start wait·late catch-up·one-shot marker·연속 sequence를 보존한다. native clip4초(30fps `4:00`), OnAttackHit1초, heal-only blend0.15초와 Animator 재생은 수정하지 않는다. 근거: Units 규칙22·32·33, Sync SeparateHeal·NET-AUTH-001/002·NET-ROOT-001~003·NET-PRESENT-001. 애니 marker가 힐 발동/HP의 권위 입력이 되어서는 안 된다.

### 16.3 예정 파일·범위 제한

프로그래머 읽기 전용 제안의 예정 source는 **`Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` 한 파일뿐**이다. PlayHealCastAnimation 뒤 해당 state를 capture하여 StartedAt+MarkerOffset을 계산하고 같은 HealNow가 due에 도달할 때까지 기다린다. 시전자 authority/liveness·동일 lifetime/sequence·active를 확인하며 invalid time은 fail-closed로 발동하지 않는다(새 clock 보정이나 delta fallback으로 우회하지 않는다). 기존 dt wait는 주석 보존하고 기존 진단 발동 지점에 Due/actual now를 함께 남긴다. 매 frame 출력·RPC/schema 추가는 아니다. 실제 변경 위치/결과는 최종 source 인계 뒤 확정하며 문서 담당은 코드를 수정하지 않는다. 다른 source·validator/fixture·asset/setup/meta 변경 및 새 clock/동결/쿨다운 정책은 자동 승인하지 않는다.

**별도 발견·OPEN:** 기존 cooldown3의 global ticker 때문에 실제 연속 cycle이 여전히 약간 짧아질 가능성은 후속 확인 대상이다. 이번 승인 범위는 marker due만이다. **새 post-dispatch deadline이나 cooldown 타이머 교정은 계획에 포함하지 않으며 구현하지 않는다.** §15의 짧은 주기를 이번 windup 교정만으로 완전히 해결한다고 주장하지 않는다.

이번 단계 실제 문서 변경은 기존 Research.md/Plan.md 두 파일뿐이다. 새 task/TC/QA·상시 규칙/정책/스탯·메모리/baseline 변경 없음. source/검증/빌드 인계 전 상시 문서에 구현 완료를 쓰지 않는다.

### 16.4 검증·빌드 시도·인계 기준

최종 source에서 due 이전 dispatch 없음, 같은 clock/StartedAt 사용, 단일 dispatch·기존 취소/target/priority 유지 및 delta 누적 경로의 주석 보존을 확인하고, 위험에 비례한 기존 정적 컴파일 결과를 errors/warnings로 구분해 인계받는다. **승인 범위 정정:** 신규 validator/회귀 fixture·TC/QA 작업은 하지 않지만, 사용자의 기존 반복 지시에 따라 메인이 빌드 전에 기존 **Unit Action Self Validation**과 **Root Pose Cross Audit SelfValidate** 두 Unity 메뉴를 실행한다. 이는 기존 자동 prebuild gate이며 새 gate 신설·full live QA·실제 경기 CrossAudit Analyze가 아니다. 이전의 포괄적인 “추가 Unity 메뉴 실행 없음” 문구는 이 기존 메뉴 실행을 제외한 것으로 정정한다. 새 두 메뉴의 정확한 실행/결과는 아직 인계 대기이며 이전 §12/§14 gate를 새 교정의 PASS로 재사용하거나 자동 PASS를 runtime PASS로 부르지 않는다.

빌드 담당이 구현/검증 뒤 **Build And Run 시작 시도만** 수행하는 것은 승인됐다. 계획 작성 당시 시도 미실행이었으며 이후 실제 클릭/JDK preflight 인계는 §16.6에 기록한다. 실제 클릭/직후 관측까지만 인계하며 완료·설치·게임 실행을 polling하거나 성공을 추정하지 않는다. 문서 담당은 UI/빌드를 실행하지 않는다.

새 실기에서는 동일 회차의 서버 due/dispatch/첫 positive HP와 실제 marker를 기존 exact key·동일 clock으로 비교한다. due 이전 발동 차단은 목표이며, Animator marker·Client HPbar·VFX pixels와 완전 동시를 보장하지 않는다. 영상/로그 exact sync 부재와 callback proxy 한계는 유지한다. 실기 수용 전 timing 수정 PASS·시각 방향 해결·원인 전체 확정을 주장하지 않는다.

### 16.5 현재 상태

**구현·정적4경로·기존 두 자동 gate 새 실행·빌드 시작 시도 인계 완료.** §15 TIMING EARLY HP FAIL은 교정 전 실기 이력으로 보존하며 새 due 경로 재시험·사용자 시각 방향·최종 원인은 OPEN이다. 빌드 완료·설치·실행은 미관찰이며 아래 단일 클릭/JDK preflight 경계를 따른다.

### 16.6 최종 코드/정적·기존 두 자동 gate·빌드 시도 인계 — 실기 재검증 OPEN

코드 담당 최종 인계 및 메인 source review에 따라 실제 patch는 **`Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` 한 파일**이다. 기존 dt 준비 wait는 주석 보존했다. PlayHealCastAnimation 뒤 accepted current state의 StartedAt+MarkerOffset으로 due를 계산하고 동일 HealNow로 대기하며 yield 뒤 clock을 재조회한다. CanContinueHealWindup은 capture한 caster reference·enabled/liveness·active/current lifetime/sequence/unit/target, initial/current network authority 및 spawned server를 확인하고 CastHeal 직전에도 guard한다. invalid due/now는 finite 검사로 fail-closed한다. 기존 diagnostics에 Due/Now/Elapsed 및 DispatchRejected를 남긴다. 새 RPC/schema·회전/HP/HoT/clip/global cooldown/post3 교정은 없다.

| 최종 정적 경로 | errors | warnings |
|---|---:|---:|
| Runtime |0 |22 |
| Editor |0 |0 |
| Development |0 |22 |
| Release |0 |26 |

위는 최종 코드 담당/메인 source review 인계에 따른 정적 결과다. **첫 자동 prebuild gate:** 메인이 기존 Unit Action 메뉴를 새로 클릭했고 새 Editor.log tail에 UAS overall/Production/Bloom/C2/C3 PASS 및 fixture warning23:49:20.968을 확인했다. warning 시각은 로그 anchor이며 메뉴의 모든 PASS 시각을 그 값으로 대신하지 않는다. 이는 기존 자동 gate이지 새로운 due coroutine의 runtime test가 아니다.

**두 번째 기존 자동 prebuild gate 확인:** 메인이 Root Pose Cross Audit SelfValidate를 새로 클릭했고 observed status 및 새 Editor.log tail의 PASS를 확인했다. 첫 Unit Action overall/Bloom/Production/C2/C3 역시 새 실행 PASS다. 두 메뉴는 자동 gate이며 실제 경기 Analyze·새 heal coroutine runtime fixture·사용자 실기 수용이 아니다.

**빌드 시작 시도 확인:** 메인이 Unity File→Build And Run을 **1회** 실제 클릭했다. 직후 capture에서 modal 제목 `Detect Java Development Kit (JDK)`와 본문 `Checking Java Development Kit (JDK)...`를 관측했다. 이는 빌드 시도/preflight 증거일 뿐 완료·설치·runtime 실행 관측이 아니다. 사용자 지시에 따라 이후 모든 Unity UI 및 build log polling을 중단했다. 성공 추정·추가 빌드 행동 없음. §12 이전 시도와 이번 시도를 합쳐 하나의 완료 증거로 쓰지 않는다. 시각/timing 실기 재검증 OPEN이며 global cooldown ticker 가능성은 별도 OPEN·미교정이다.

## 17. 2026-10-04 새 두 run·Movie_002 분석 인계 — 새 구현 승인 아님

Android Host/Editor Client의 동일 shared key인 새 두 observer run을 BEGIN/runId로 분리했다. 과거 device prefix는 중복 집계하지 않는다. 정확한 run identity/raw anchor·수치·영상 coverage는 **Research §17.1~§17.8**이 상세 단일 anchor다.

관측181 dispatch 모두 Now≥Due였고, 비교 가능한 실제 첫 positive HP122회 모두 동일 Host marker 이후였다. 이는 §16의 **due 경계 관측 수용**이지 모든 Start/HP·전체 timing/facing PASS가 아니다. B의 Start114/Play113 차이는 Unit79/lifetime80/sequence18의 같은 Host frame Start+despawn End·dispatch 없는 취소다. loss로 단정하지 않는다. cross-peer LocalNow 차감은 하지 않는다.

연속 cycle은 관측 최소3.933656/3.934839초로4초 미만이 남았다. post3/global cooldown ticker는 §16 승인에서 제외되어 무변경이며 **잔여 OPEN**이다. 새 post-dispatch deadline 교정이나 시각 방향 fix는 이번 분석으로 승인되지 않는다.

Movie_002의 overview/full-resolution/crop 표본은 모션과 녹색 완료 텍스트를 확인했으나 unitId/UTC/HPbar·PTS↔UTC exact sync가 없다. 특정 영상 frame↔cast key 결합, 완료 텍스트=첫 HP, 전체 continuity/pixel facing PASS 주장은 하지 않는다.

§15 EARLY HP FAIL 및 §16 구현/정적·두 자동 gate/JDK 빌드 시도는 이전 단계 이력으로 보존한다. native4초/marker1초(30fps4:00/1:00)·blend0.15초·준비1+post3·HoT200/3·서버 writer/정책/스탯 불변. 사용자 visual acceptance 없이 전체 Bloom CLOSED로 전환하지 않는다. 이번은 분석 문서 갱신만이며 코드/assets/UI/build/Git·TC/QA/new task 없음. 추가 검증된 각도/outcome/raw 인계는 Research에만 상세 보강한다.

**최종 분석 보강:** 첫 positive HP 없는59 dispatch는 FullHP/대상 unavailable/첫 gain 전 효과 교체·대상 unavailable로 설명되며 unknown HP loss가 아니다. callback 각도·self checkpoint는 Root proxy이고 영상 pixel facing 수용을 대신하지 않는다. 이번 run 오류·이동 실패 비재발은 과거 전체 교정·성능 PASS가 아니다. 상세 outcome 분모·raw anchors·잔여 cycle 및 source 추론은 Research §17.5~§17.8에만 기록했다. 전체 OPEN과 미승인·미구현 후속 경계를 유지하며, 새 구현 계획은 추가하지 않는다. 검사 실행 및 Task 수동 최종 검토는 메인 세션이 담당한다.

## 18. 2026-10-04 발동 후 설정 쿨다운 deadline 교정 — 사용자 승인 계획

> **후속 상태:** 메인 source review로 실제 UnitView 패치 반영 확인 인계를 받았다. 아래는 보존된 승인 계획이며 현재 코드 인계/검증 대기 경계는 §18.4를 따른다.

실제 힐 발동 뒤 설정된 쿨다운을 온전히 기다리도록 최소 범위로 교정한다. 사용자 승인 범위는 **actual authoritative dispatch + configured AttackCooldown deadline**, 정적 검증·기존 두 자동 메뉴 PASS 뒤 Build And Run1회 시도이며 완료 monitoring은 제외한다. §17의 미승인 제안 표기는 당시 이력으로 유지하고 이번 승인을 구분한다. 이 절은 **구현 예정 계획이며 완료 인계가 아니다.** 기존 global counter wait는 검증 전 삭제하지 않고 주석 비활성화로 보존한다.

### 18.1 최소 구현 예정 항목과 규칙 근거

| 항목 | 예정 변경 / 유지 경계 | 근거 |
|---|---|---|
| 서버/싱글 힐 post-dispatch 대기 | 코드 담당이 `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`의 힐 루프에서 실제 authoritative dispatch 시각+configured AttackCooldown을 deadline으로 사용하고 같은 권위 clock으로 기다린다. 현재 config3초를 보존하며 nominal due/첫 HP/Client marker를 기준으로 바꾸지 않는다. | Units 규칙36: 준비 미포함·실제 발동 후 쿨다운 |
| 이전 대기 보존·타입 격리 | 기존 global counter 기반 wait는 주석 비활성화로 남긴다. 공통 global ticker와 다른 유닛 쿨다운 정책은 변경하지 않는다. 구체 코드 제안은 별도 코드 담당의 책임이다. | Units 규칙32·36: 힐러 별도 경로·유일 예외 |
| 기존 권위·생명주기 유지 | §16 due guard/단일 dispatch 및 기존 취소·사망·종료·권위 차단을 유지한다. Client HP/효과 생성·새 회전 writer·RPC/schema 변경 없음. | Units 규칙32~35·37 및 Sync SeparateHeal |

전역 tick phase seam은 §17.6의 **유력 원인이지 완전한 runtime causal proof가 아니다.** 목표는 실제 dispatch부터 설정 쿨다운이 끝나기 전 다음 시전의 조기 시작을 막는 것이며, frame/대상/priority 조건까지 포함한 **정확한4.000초 cycle 보장은 아니다.** native clip4초/marker1초·blend0.15초·HoT200/3·HP/완료 텍스트·연구 배율·target/facing/연속 재생·Frozen/Held 정책과 스탯은 불변이다. facing/clip tail fix 또는 전체 PASS로 확대하지 않는다.

### 18.2 승인된 검증·빌드 시도 순서 / 실행 예정

1. 별도 코드 담당이 최소 교정과 정적 검증을 수행하고 최종 변경 범위·결과를 인계한다. 신규 TC/QA·validator 수정은 하지 않는다.
2. 실행 담당이 기존 Unit Action self-validation 및 `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit` 두 메뉴의 새 실행 PASS를 확인한다. 이전 실행 PASS나 실제 경기 Analyze와 혼동하지 않는다.
3. 정적 검증과 두 메뉴 PASS 뒤 메인이 File→Build And Run을1회 시도한다. gate 실패/미확인 상태를 PASS로 추정해 진행하지 않는다. 빌드 완료·설치·실행 확인을 위한 UI/build log monitoring/polling은 하지 않으며 시작 시도와 빌드 성공은 구분한다.

현재 이 절에 구현/정적/메뉴/빌드 완료 결과는 없다. 새로운 runtime·사용자 최종 visual acceptance 전 전체 OPEN이다. 문서 담당자는 기존 task 두 파일의 계획만 추가하며 상시 문서·메모리·TC/QA·신규 task·validator/Git/UI를 변경·실행하지 않는다. 최종 구현·검증·빌드 시도 인계는 별도 지시를 받아 기록한다.

### 18.3 코드 proposal 리뷰 완료 — 승인된 구체 보강 / 구현 예정

**근거 주체 교정(기존 표현 이력: “사용자 전달 코드 proposal 리뷰 완료 사항”):** 사용자는 narrow fix 범위를 승인했고, 실제 코드 proposal은 코드 담당 Sagan→메인 리뷰→문서 담당자 인계 순서로 전달됐다. 아래는 그 리뷰 완료 사항이며 당시 코드 적용/검증 완료 인계가 아니다. §18.1 최소 범위에 다음 조건을 보강한다.

| 보강 항목 | 구현 예정 계약 |
|---|---|
| 실제 발동 기준 캡처 | actual `CastHeal` 직전에 fresh `HealNow`를 `dispatchAt`으로 잡고 당시 configured cooldown을 캡처한다. cooldown은 양수·finite로 검사하며 `CooldownDue = dispatchAt + captured configured cooldown`을 사용한다. nominal due/첫 HP/Client marker로 대체하지 않는다. |
| 두 조건 대기 | absolute deadline 도달 **AND AttackCooldownRemaining≤0**을 모두 만족한 뒤 다음 시전으로 진행한다. 기존 글로벌 counter 설정/감소는 무변경이며 이전 counter-only wait는 삭제 없이 주석 보존한다. |
| 재진입 owner record | UnitView 내부에 caster identity/lifetime·dispatch 회차·clock 모드·deadline 최소 record를 유지한다. movement-replaced 뒤 새 힐 루프도 해당 owner의 deadline을 우회하지 않도록 `Play` 직전 재진입에서 존중한다. |
| 유지/폐기 경계 | 정상 모션 종료·이동 교체는 cooldown record를 유지한다. death/gameend/disable/despawn/reinit에서는 해당 owner만 폐기하며 stale owner 정리가 새 record를 지우지 않는다. |
| stale coroutine 격리 | stale old coroutine은 새 state 종료·회전 release·counter clear·pursuit reset을 하지 않는다. 새 owner와 활성 state를 보호하며 기존 권위/회전 writer 경계는 유지한다. |
| bounded 증거 로그 | 기존 keyed `CastDispatch`에 `DispatchAt/ConfiguredCooldown/CooldownDue`, wait 종료에 `Now/Elapsed/Remaining`을 bounded log로 남긴다. |

시간 조건은 Units 규칙36, 별도 힐/owner·수명 정리는 Units 규칙32 및 Sync SeparateHeal의 기존 경계에 따른다. §16 due guard·단일 dispatch·사망/권위 차단과 다른 정책·스탯·공통 ticker·HP/HoT/텍스트·clip/marker·방향/연속 재생은 유지한다. 원인 추론의 한계·정확한4초 cycle 비보장·전체 OPEN 및 §18.2 정적/기존 두 메뉴 PASS 후 Build And Run1회 시도·완료 monitoring 제외는 그대로다.

### 18.4 실제 코드 반영 인계 — 검증 결과 추정 금지

메인 source review 인계상 실제 패치는 `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` 한 파일이다. fresh actual dispatchAt+캡처 configured cooldown의 absolute due, Remaining≤0을 함께 기다리는 post-wait 및 entry/Play 직전 guard, caster identity/lifetime·dispatch 회차·clock 모드·deadline record, lifecycle owner 보호가 반영됐다. 정상 모션 종료/이동 교체는 cooldown을 유지하고 death/gameend/disable/despawn/reinit는 해당 owner만 폐기한다. stale old coroutine이 새 state 종료/회전 release/counter clear/pursuit reset을 하지 않도록 격리하며 기존 global counter 설정/감소는 유지하고 counter-only wait는 주석 보존했다. bounded keyed dispatch/complete/cancel/reject 로그도 반영됐다. 구체 항목·근거 구분은 Research §18.5에 기록한다.

이번 patch 이후 **최종 정적·기존 두 메뉴 새 gate·빌드 시도 결과는 별도 인계 대기**다. §16 이전 PASS/JDK 시도를 소급 재사용하지 않는다. 정적+기존 두 메뉴 PASS 뒤 Build And Run1회 시도/no completion monitoring 순서는 유지하며 아직 결과를 추정하지 않는다. 원수치·정책·에셋 및 §17 증거 보존, exact4초 cycle·runtime 전체 fix·visual acceptance 미입증/전체 OPEN. 최종 결과는 이후 인계로 보강한다.

### 18.5 코드 담당 최종 정적 및 cleanup 인계 — UI/build 대기

UnitView 한 파일의 최종 정적 인계는 Runtime errors0/warnings20, Editor0/0, Development0/20, Release0/24다. source 목록 차이0·Editor 새 Runtime.ref 사용·컴파일 전후 source hash 동일 및 Temp 보조 artefact 목록은 Research §18.6에 기록했다. 경고 수 감소를 기존 warning 교정으로 읽지 않는다. 메인이 현재 source owner/end-cleanup/reentry/normal-wait를 review 확인했고, 추가 좁은 정리는 EndHealPresentation 현재 healer pursuit lifecycle clear 및 reentry CooldownComplete 로그1회+record clear다. old coroutine 자체는 새 state를 없애지 않는다.

새 기존 두 메뉴와 이번 Build And Run은 아직 실행 대기다. 두 메뉴 새 PASS 확인 뒤 Build And Run1회 시도/no completion monitoring만 승인됐으며 결과 추정/이전 gate 재사용 없음. §17 runtime·기존 계획 이력 보존, source cause probable/정확한4초 cycle 비보장·runtime/전체 OPEN 유지.

### 18.6 최종 새 기존 두 자동 gate·빌드 시도 결과 — 실기 OPEN

메인 최종 UI/log 인계에 따라 새 native Refresh 후 Runtime/Editor DLL05:57:24 생성을 확인했다. 기존 Unit Action 메뉴 새 클릭 후 Editor.log 고정 마지막524,288byte에서 Bloom/overall/Production/C2/C3/AUTH-PRESENT/TORRENT production PASS를 확인했다. warning fixture anchor06:01:09.751은 전체 PASS 시각의 대체값이 아니다. 이어 Root Pose Cross Audit SelfValidate를 새로 클릭하고 UI status·새 Editor.log tail의 ROOT PASS를 확인했다. 기존 두 자동 gate의 새 실행이며 이전 경기 Analyze/새 coroutine runtime fixture가 아니다.

두 메뉴 PASS 뒤 메인이 File→Build And Run1회 실제 클릭 직후 `Importing - Importing...`·`Settings/Mobile_RPAsset.asset` 화면만 관측했다. 이후 모든 Unity UI/build log polling 중단, 완료·설치·runtime 미관찰/빌드 성공 추정 없음. 앞선 정적 결과·source/owner cleanup 확인과 이번 gate/빌드 시도 경계를 구분한다. 정책/스탯·에셋·다른 타입 무변경, §17 evidence 및 이전 계획/대기 이력 보존, exact4초/전체 runtime fix/시각 수용 미입증·전체 OPEN이다.

최종 inventory는 문서12(관련9+기존 memory3 durable lesson), source1(UnitView), Temp 보조20(rsp/dll/ref.dll/pdb/log×4경로)이며 상세는 Research §18.6~§18.7이다. 이번 단계는 관련9개 문서만 동기화하고 메모리 WIP는 추가하지 않는다. 메인이 최종 정합성 검사/Task 수동 검토를 실행하며 문서 담당자의 검사 PASS 주장은 없다.

**최종 inventory 보강:** 메인의 증가-only `check_docs --update-baseline` 실행으로 도구가 `.claude/agent-memory/_baseline.json`을 자동 갱신했다(직접 편집 아님/감소 없음). 최종 inventory는 source1/docs12/baseline1/Temp20 보조 artefact로 구분한다. 증가 수치·근거는 Research §18.7에만 기록한다. 최종 checker는 문서 마무리 뒤 메인 실행 예정이며 아직 그 결과를 기록하지 않는다.

## 19. 2026-10-04 첫 힐 초기화 순서 회귀 진단 — 수정 승인 아님

사용자의 최신 원인 요청에 따라 메인 raw/source 인계를 문서화한다. **마지막 §18 교정에서 도입된 초기화 의존 순서 결함**이며 에셋/클립 문제나 서버 권위 완화 제안이 아니다. 최신 Android Host/Editor Client 동일 shared session `2d0898b24994462500f4a64df0f88f9078c820fc934ed98bf6207eaf3dbabc98`에서 Bloom 생산은 확인되나 양쪽 BLOOM-HEAL0이다. 최신 로그 분모4552/6558·생산IDs·재생성 anchor·과거00:30~00:35 자료 제외는 Research §19.1의 단일 상세 근거다.

source상 EnterHealLoopV3 entry guard가 `_healNetworkUnit`을 요구하지만 그 유일한 초기화는 뒤의 lazy HealNow getter다. Initialize는 이 cache를 채우지 않고 Active-only Update/invalid-state pose 경로도 fresh 첫 진입을 prime하지 않는다. movement-first/reentry/stationary 모두 같은 guard를 사용해 초기화 전에 차단될 수 있다. source 순서 결함은 확인됐지만 private cache/정확한 정지 인스턴스 runtime probe는 없어 모든 instance 인과 입증은 아니다. 근거 행 번호/경로와 한계는 Research §19.2 참조.

기존 ValidateBloomHealPresentationBoundary는 state 직접 생성→ApplyBloomNetworkFixture/ApplyCurrentHealCast 경로이며 실제 EnterHealLoopV3/HasHealAuthority/HealNow의 fresh first-cast traversal을 검사하지 않는다. **검증 scope gap이지 모든 validation 무효 주장이 아니다.** §18 static/메뉴 PASS를 첫 진입 회귀 PASS로 재사용하지 않는다.

**미구현 제안만:** authority guard 전에 실제 NetworkUnit 의존성을 독립 확보/초기화하되 IsSpawned/IsServer·identity/lifetime·deadline/Remaining·stale owner·서버 HP/HoT/정책을 유지한다. guard 삭제/authority bypass·Client writer·에셋 변경은 제안하지 않는다. 향후 승인된 fresh-first-entry 회귀 증거가 필요하지만 새 구현 계획 승인·TC/QA 실행은 아니다.

최신 **FAIL·OPEN / diagnosis only / fix 미구현**이다. Research/Plan 및 기존 실패 기록1개만 갱신, source/assets/UI/build/Git/신규 task/TC·상시문서/메모리 확장 없음. §17~§18/기존 실패 이력 보존, 최종 checker는 메인 담당이다.

## 20. 2026-10-04 첫 힐 의존성 초기화 순서 교정 — 구현 전 승인 계획

사용자가 §19의 narrow correction과 빌드 시도를 승인했다. 첫 힐에 필요한 실제 NetworkUnit을 authority guard 전에 독립 확보하여 lazy clock getter에 숨겨진 초기화 의존을 없애되 기존 서버 정책을 유지한다. **현재는 Sagan 읽기 전용 proposal→메인 review/GO 대기이며 완료 보고가 아니다.** 기존 guard·쿨다운·stale owner·보존 주석은 삭제하거나 느슨하게 하지 않는다. GameSystemRules 인덱스와 관련 Units/SeparateHeal 규칙을 확인한 뒤 계획을 기록한다.

### 20.1 최소 예정 변경과 규칙 근거

| 예정 항목 | 최소 범위 및 유지 조건 | 규칙 근거 |
|---|---|---|
| 실제 component 선확보 | 코드 담당이 UnitView의 현재 객체 실제 NetworkUnit을 첫 authority gate 전에 resolve/capture한다. HealNow/Active presentation 읽기로 cache가 먼저 생기기를 기대하지 않는다. 구체 patch 지점은 메인 proposal review/GO 후 확정한다. | Units 규칙32·Sync SeparateHeal: 서버/싱글 힐 경로, Client motion-only |
| 서버/owner 판정 유지 | component 확보 뒤에도 IsSpawned/IsServer·caster identity/lifetime 및 현재 owner 검사를 유지한다. pure Client·reinit·stale 객체를 현재 서버 owner로 오인하지 않는다. authority bypass/guard 항상 true 없음. | Units 규칙32·Sync SeparateHeal 및 NET-AUTH/ROOT 경계 |
| 이전 교정 보존 | §16 due·단일 dispatch, §18 configured cooldown deadline+Remaining·재진입 record·lifecycle/stale owner 보호를 유지한다. 공통 ticker/다른 타입·원수치·HP/HoT/텍스트·타겟/회전·표현/에셋·기존 주석 보존은 무변경이다. | Units 규칙32~37·Sync SeparateHeal |

최소 source 예정 파일은 `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`다. 신규 asset/TC/validator/새 task는 만들지 않는다. 초기화 순서의 source 결함 확인과 runtime private cache/정지 unit exact binding 미관측은 별개이며, 이번 계획을 모든 인스턴스 원인 증명·visual fix 또는 exact4초 cycle 보장으로 확대하지 않는다.

### 20.2 구현 후 인계 순서 — 결과는 아직 없음

1. Sagan proposal을 메인이 review/GO한 뒤 최소 구현·source/정적 최종 결과를 인계한다. 문서 담당자는 source를 수정하거나 검증을 실행하지 않는다.
2. 메인이 새 기존 Unit Action 및 Root Pose Cross Audit SelfValidate 두 메뉴 사실을 확인해 전달하면 기록한다. 기존 helper fixture의 fresh production first-entry traversal 공백을 실제 entry regression PASS로 포장하지 않는다. 새 TC/QA/validator/runtime fixture 작업은 하지 않는다.
3. 승인된 Build And Run 시도는 메인의 실제 조작 인계 이후에만 기록한다. 빌드 완료·설치·runtime monitoring/polling·성공 추정은 하지 않는다. 이전 §18 gate/빌드 시도는 새 결과의 대체 증거가 아니다.

이번 저장은 기존 task 두 파일 §20 계획/상단 상태만 변경한다. 최종 source/검증/새 메뉴/빌드 사실 대기, §19 실패·기존 이력 보존 및 새 runtime/사용자 visual acceptance OPEN 유지. 최종 인계 뒤 사실상 필요한 관련 상태만 동기화하며 불필요한 상시 문서/메모리 확장은 하지 않는다. 최종 checker는 메인이 모든 문서 완료 후 실행한다.

### 20.3 실제 최소 patch 최종 인계 — static 완료·새 gate/build 대기

Sagan proposal을 메인이 승인·review하고 최종 보고와 대조했다. 실제 UnitView 한 파일에서 `EnsureHealNetworkUnit`이 같은 GameObject의 실제 component를 확보(destroyed-null 재조회)하고 Initialize 및 첫 HasHealAuthority 전에 실행된다. 기존 검사 순서 주석은 보존하고 guard 판정·서버 권위·dispatch deadline/Remaining·수명/owner 보호는 바꾸지 않았다(Units 규칙32~37/Sync SeparateHeal 보존).

lifetime별 accepted/rejected bitmask의 bounded Dev FirstEntry 진단은 새 UnitData에서만 reset하며 실제 cast 전 CacheBefore/After·SameObject·authority/lifetime/시각 등 사실을 기록한다. CastAllocated=false이며 fake cast는 없다. 필드·source 최종 시각/크기·정적/산출물 상세의 단일 근거는 Research §20.3이다.

새 static Runtime0/20·Editor0/0·Development0/20·Release0/24(errors/warnings) 완료. Temp16개(dll/ref.dll/pdb/log×4) 갱신, RSP4개는 불변 재사용이다. native Runtime/Editor DLL14:15:48/14:15:50 뒤 reload·새 Unit Action 클릭 인계까지 확보했지만 **새 메뉴 결과·Root SelfValidate·Build And Run 사실은 대기**다. 이전 gate/build 이력을 이번 결과로 재사용하지 않는다. 기존 실패 기록과 필요한 현재 source 현황만 후속 표시하며 새 메모리/TC/QA/validator·정책/수치/에셋 변경은 없다. 새 runtime/사용자 수용 OPEN, private cache·모든 instance 인과/증상 해소·exact4초 보장 미입증; 최종 checker는 메인 담당이다.

### 20.4 최종 native 결과와 남은 수용 경계

메인 최종 인계: 이번 실제 Unit Action Self Validation 클릭 후 새 bounded Editor.log에서 overall/Bloom/Production/C2/C3/AUTH-PRESENT PASS, 이어 실제 Self Validate Unit Root Pose Cross Audit 클릭 후 새 UI status와 새 bounded log PASS 확인. 기존 자동 gate의 새 실행이며 fresh-first-entry production runtime fixture/실경기 Analyze는 아니다. 기존 first-entry coverage gap을 runtime PASS로 포장하지 않는다.

File→Build And Run1회 실제 클릭 직후 Detect Java Development Kit(JDK)/Checking Java Development Kit(JDK)... 진행창만 관측하고 UI/build log 확인 모두 중단했다. 완료/설치/runtime 미확인·빌드 성공 추정 없음. source·static 최종은 §20.3, 새 메뉴/build 상세와 이번 문서11/source1/Temp16갱신·RSP4재사용 구분은 Research §20.4를 따른다. 이전 실패/승인 계획·gate/build 이력 보존, 클립/수치/authority/post3 정책 유지, runtime 재시험·사용자 수용 OPEN이다. 필수 memory에는 의존성 선확보/검증 범위 durable lesson만 추가하고 최종 checker는 메인이 실행한다.

## 21. 2026-10-04 두 신규 실기 분석 인계 — 추가 구현 계획 아님

메인의 확정 분석으로 §20 교정 뒤 첫 진입과 반복 시전 전달이 관측됐다. device15_29의 과거12:56~13:01 실패 prefix 제외, Editor Host/Android Client 신규15:23~15:25:17 및15:25:18~15:28:09를 reused shared key라도 ROOT runId/시간으로 분리한다. 생산 TeamRed4+9기 전수 FirstEntry accepted/Start, Host Start/Play 및 Client Play17/66 exact key 일치·누락/추가/중복0·주요 payload conflict0이다. Marker16/64 양쪽 일치, 없는3회는 death/despawn 사례다. raw 경로·키·분모·행 anchors는 Research §21.1~§21.2가 단일 상세 근거다.

중간 Active=False revision6개 미수신은 동일 Host frame의 다음 inactive 발행/수신과 정합적이고 cast별 최신 revision17/66 전부 수신이다. packet trace 없는 coalescing 확정이나 heal 누락/재활성화로 확대하지 않는다. LifecycleEndReason의 priority revision 덮어쓰기는 별도 진단 한계다. callback 정의된 각도/self drift·undefined lookup 구분, Host MOVE 실패0/local ROOT4 PASS는 Research §21.3 참조; rendered model/pixel 방향·formal CrossAudit/시각 연속성 PASS가 아니다.

**first-entry+repeat delivery 회귀 focused 로그 관측 수용 / Timing·HoT 최종 대기 / visual·VFX 및 전체 OPEN.** Timing/HoT 항목은 미확인 유지(Research §21.4)하고 최종 전문 인계 뒤 후속 추가한다. 사용자의 VFX 후 육안 확인 선호는 VFX 제작 승인이 아니다. 기존 source/에셋/규칙/원수치/서버 권위/post3 정책은 변경하지 않는다. 이번은 기존8문서 최소 분석 동기화뿐이며 새 durable lesson 없이 memory WIP 복제 없음. 신규 task/TC/QA·영상 분석·UI/build/Git/checker 실행 없음, 기존 모든 증거 이력 보존·최종 checker 메인 담당이다.

### 21.1 최종 Timing/HoT 인계 — 후속 미완 구분

**역할 coverage:** §19는 Android Host/Editor Client 실패였고 이번 관측 수용은 Editor Host/Android Client 두 run만이다. 공통 source 교정과 교정 후 Android Host first-entry 실기 수용은 별개이며 후자는 미확인이다. 모든 플랫폼/역할 해결로 일반화하지 않고 새 역할교대 시험을 요청·실행하지 않는다.

전문 담당 집계와 메인의 same-clock/raw/source 확정 인계로 due 이전 dispatch0/80·exact accepted Host marker 이전 firstpositive0/68, dispatch→다음 Start3초 미만0/70을 확인했다. first-entry·반복 전달에 더해 **due/postdispatch cooldown focused 관측 수용**이다. 큰 no-target 종료 후 재시작 gap은 정지 FAIL이 아니고 exact4초 cycle 보장도 아니다. movement-replaced 재진입 직접 coverage는 없으므로 수용하지 않는다. key/run identity·각 분모·간격·raw/source anchors·12 no-firstpositive outcome·서버 회복 및 HoTResult 상세는 Research §21.5가 단일 근거다.

Expired13개의 누적 Elapsed3/Duration3/ActualHealed200과 실제 같은 Host clock 등록→종료는 구분한다. 실제 최소2.976042/2.930041초는 기존 공통 TickTimedEffects의 global elapsed 누적이 등록 전 시간을 첫 tick에 포함할 수 있는 source 정합성을 보여주지만 instance별 exact ticker 시작 private state 미로그로 전수 인과 증명은 아니다. **별도 효과 duration 약70ms 잔여 OPEN·후속 분석/교정 제안만**이며 이번 source/정책 수정은 없다. 네트워크 lag로 설명하지 않는다.

전체 Bloom OPEN, visual/VFX·Client HP/text 전수 및 위 미완 수용 없음. VFX 후 사용자 육안 선호는 제작 승인 아님. §20 자동 gate/build 시도 이력은 보존하고 이번은 saved-log 분석/기존8문서 동기화만, 추가 code/assets/TC/QA/UI/build/Git/checker 실행 없음. session WIP memory 복제 없이 기존 memory/baseline 보존, 최종 checker 메인 담당이다.

## 22. 등록 기준 공통 연속 Heal duration 교정 계획 — 미구현·승인 대기

### 22.1 목적·승인 경계

§21에서 남은 등록→Expired 약70ms 조기 종료를 대상으로, 등록 전 시간을 새 효과의 진행 시간에 넣지 않도록 한다. source 메커니즘 확인과 개별 instance private ticker phase 미관측은 Research §22.1대로 구분한다. §21 first-entry/due/postdispatch cooldown 관측 수용을 뒤집거나 전체 Bloom PASS로 확대하지 않는다. 이번은 문서만 승인됐고 **아래 구체 계획을 사용자에게 공유해 승인받기 전에는 구현하지 않는다.**

### 22.2 예정 패치 — UnitCombatUseCase.cs 한 파일

1. 공통 continuous Heal production 상태에 double RegisteredAt/Deadline을 둔다. add/refresh 시 같은 scaled Time.timeAsDouble 표본으로 등록과 deadline을 설정하며 누적 목표/적용량·ActualHealed 등 totals를 새 회차로 reset한다. 기존 재부여 대체·비중첩 정책은 유지한다.
2. Tick당 now를 한 번 읽고 now−RegisteredAt으로 진행을 계산한다. 등록 전 global dt는 credit하지 않는다. invalid/nonfinite/regressed clock에서는 진행을 보류하고 dt fallback은 하지 않되 기존 target unavailable/fullHP 처리를 보존한다.
3. positive Duration 범위로 계산값을 clamp한다. 기존 duration<=0 즉시 처리 정책을 보존하며 정확한 경계 구현은 implementation review에서 확인한다. 정상 Expired 판정은 float Elapsed rounding이 아닌 double now>=Deadline을 사용한다.
4. 기존 누적 RoundToInt 차분과 만료 시 remaining amount 적용을 유지한다. deadline 직전 반 HP 미만 구간에서 누적 회복량200에 도달할 수 있으므로 마지막 HP 적용 시각>=3초를 보장하지 않는다. FullHP 종료 예외를 제외한 Expired/정상 완료 텍스트는 deadline 전 발행하지 않는다.
5. 대체되는 기존 source는 사용자 runtime 수용 전까지 주석으로 보존한다. 새로운 타입명 예외·권위 우회·클립/수치 조정은 넣지 않는다.

### 22.3 영향 범위와 변경하지 않는 것

공통 continuous Heal 전체(Bloom 및 skill HoT)에 적용한다. skill HoT의 duration 계산에도 영향을 주는 공통 교정이지 Bloom-only 예외가 아니다. continuous Damage dt·discrete DoT·global ticker/호출자·자연회복·MistShrine·Torrent는 변경하지 않는다.

Units34/37 메커니즘, Units32 authority/36 cooldown, Synchronization SeparateHeal/network authority를 유지한다. 검토 proposal은 규칙 충돌/변경을 제안하지 않는다. HoT200/3·prep1·post3·clip4·marker1·blend0.15 및 기존 에셋/Stats 값은 불변이라 Stats 갱신은 필요 없다. 다른 상태 문서/메모리도 계획 단계만으로 갱신하지 않는다.

### 22.4 승인 후 검증 계획 — 아직 실행하지 않음

기존 static 경로로 변경 영향/컴파일을 확인하고, 새 runtime에서 같은 scaled clock의 RegisteredAt/Deadline과 Expired를 비교한다. 생존·비만피·미대체 target의 정상 만료는 **첫 서버 tick now>=Deadline 및 등록→Expired>=3 scaled초**를 요구한다. tick 지연 때문에 정확히3.000초 종료를 약속하지 않는다. FullHP·사망/대상 소실·재부여 대체를 별도 outcome으로 분리하고 회복량 차분/최종 remaining amount·비중첩 계약도 확인한다.

새 TC/QA/validator는 만들지 않으며 이번 static/자동 메뉴/빌드 결과는 없다. 과거 gate PASS는 이 새 교정의 증명이 아니다. 실행 범위는 이후 별도 승인에 따른다. visual/VFX는 계속 유보, §21 역할 coverage 한계와 Client HP/text·movement-replaced 미완을 유지하고 전체 Bloom OPEN이다. 현재 수정 inventory는 Research/Plan 두 파일뿐이며 checker는 메인 담당이다.

### 22.5 승인 후 구현 인계와 다음 gate — static 완료·메뉴 대기

사용자의 「진행해」 승인 뒤 UnitCombatUseCase.cs 한 파일 구현 및 메인 source review가 완료됐다. 앞선 미구현/승인 대기는 계획 당시 이력으로 보존한다. 실제 continuous Heal은 새 등록/refresh의 production RegisteredAt/Deadline/LastClock과 Tick당 단일 scaled now를 사용한다. 등록 전 dt credit 없이 등록 기준 진행·double deadline Expired, invalid/nonfinite/regression diff0 보류(기존 target/fullHP 처리 유지), duration<=0 기존 즉시 정책을 반영했다. RoundToInt 누적 차분/totals·remaining amount·비중첩·서버 텍스트 계약은 유지하며 deadline 직전 총량 반올림 가능성은 남는다. 기존 keyed firstpositive/Expired에는 DeadlineLocal/RegisteredLocal/Since가 추가됐다.

최종 static(errors/warnings)은 Runtime0/20·Editor0/0·Development0/20·Release0/24이며 Editor에 fresh runtime.ref.dll을 사용했다. Temp16(dll/ref.dll/pdb/log×4) 갱신, RSP4는 unchanged 재사용이다. 범위는 Bloom+skill HoT 공통 continuous Heal이고 assets/UnitView/ticker/DoT·정책/수치 변경은 없다. 실제 source/산출물/보존 경계는 Research §22.5를 따른다.

실제 Assets Refresh 뒤 native Unity import 진행 중·native DLL은 이전14:15본으로, **새 Unit Action/Root SelfValidate 결과는 아직 대기**다. 이번 빌드는 승인/실행하지 않았다. 이후 메인의 실제 메뉴 인계만 기록하고 static PASS를 새 duration runtime 통과로 확대하지 않는다. §22.4의 same-clock 신규 runtime gate는 미실행이며 §21 조기 종료 baseline을 보존한다. skill HoT runtime 영향·visual/VFX·Client HP/text 전수·역할 및 movement-replaced coverage도 미완, 전체 OPEN이다. 신규 TC/QA/validator·checker 실행 없음. 이번 task2+관련 상태5문서만 최소 동기화하고 중복 memory/WIP·baseline 변경은 하지 않는다.

### 22.6 최종 native gate 완료 — runtime 수용은 별도 OPEN

메인 전체 time branch/guard review의 final source16:24:35 이후 실제 Assets Refresh로 native Assembly-CSharp16:28:31/Editor16:28:32가 갱신됐고 Google.VersionHandler reload busy가 해소됐다. 실제 Unit Action Self Validation(menu90) 클릭 후 새 bounded Editor.log(offset1215806070 이후)의 overall/Bloom/Production/C2/C3/AuthPresent PASS, 이어 실제 Root Pose SelfValidate(menu114) 클릭 후 새 UI status `UAS-ROOT-CROSS-AUDIT self-validation PASS`를 메인이 확인했다. 앞선 import/메뉴 대기는 당시 이력이다.

source1파일·static4 errors0 및 실제 새 기존 두 메뉴 PASS까지 완료됐다. **새 production HoT fixture·Analyze·게임 runtime은 미실행, 이번 빌드 없음.** 기존 메뉴 통과를 등록 후3 scaled초 duration runtime 증명으로 확대하지 않는다. §22.4 신규 same-clock gate·skill HoT 영향·visual/VFX 및 §21 기존 미완/역할 coverage는 OPEN이고 조기 종료 baseline을 보존한다. 새 TC/QA/validator·수치/에셋/정책 변경 없음, 전체 Bloom OPEN. 상세 native 근거는 Research §22.6; 같은7문서 최종 동기화만 수행했고 memory/baseline 변경 및 checker 실행은 없다.

최종 추가 인계: Root는 UI status뿐 아니라 offset1215806070 이후 새 bounded Editor.log에서도 PASS 확인됐고, 메인의 독립 static log 확인은 기존4경로 수치와 일치한다. source16:24:35 이후 추가 변경 없음. 이후 별도 진단 도구 실수(ReadToEnd/광범위 Exception filter)를 `.claude/mistakes.md` 목차·하단에 기록해 **최종 문서 inventory는8개**다. 게임 실패/HoT 정책 결함과 구분하며 로그 mutation은 없다. source1·문서8·Temp16 갱신/RSP4 unchanged의 전체 clickable 경로는 Research §22.6 최종 inventory를 따른다. memory/baseline·checker 변경/실행 없음.

## 23. 18_16 역할교대 saved-log 분석 인계 — 추가 구현 계획 아님

### 23.1 새 runtime 관측 수용과 이력 보존

programmer 최종 읽기 전용 집계 및 메인의 raw/source 검토 인계로 A(Android Host/Editor Client,18:07~18:10:07,`476a…cf4d`)와 B(Editor Host/Android Client,18:11~18:13:48,`4300…0f54`)를 분리했다. device18_16의 과거15시 prefix 및 빈 재시작 observer tail은 gameplay 분모에서 제외한다. 파일/행/key는 Research §23.1~§23.4를 따른다. 새 key 표기는 **Lifetime/UnitId/sequence**(예:79/78/4)다.

각 경기 생산7/7 FirstEntry accepted, Host Start/Dispatch/Outcome55/63 및 양 peer exact key·target·StartedAt Play/Marker55/63 일치·차이/중복0이다. due 이전 dispatch0/118, 등록 또는 accepted Host marker 이전 firstpositive 위반0/88, dispatch now와 registration 일치88건이다. 다음 Start3초 미만0/104도 확인돼 두 역할에서 first-entry/due/postdispatch cooldown focused 관측 수용이다. 정확히4초 cycle 보장이나 모든 상황 수용은 아니다.

**§22 신규 duration gate: Expired27/27 관측 수용.** A13/B14 모두 Elapsed3/Duration3/ActualHealed200 및 now>=Deadline, 실제 등록→Expired A3.016200~3.083146/B3.001368~3.107432초·early0/27이다. raw 최소/최대는 A D153486/D159454, B E40668/E41160이다. §21 early baseline은 교정 전 증거로 보존하며 §22의 구현/메뉴 후 runtime 대기는 당시 단계로 구분한다. 이 수용은 마지막 HP가 반드시3초 이후라는 보장이나 skill HoT/invalid-clock 처리 검증이 아니다.

Applied/HoTResult·no-firstpositive outcome과 B116/115/1의 GameEnd-before-deadline 결과 부재는 Research §23.2~§23.3대로 분리한다. live Host MOVE failures/dropped0 및 local ROOT4 PASS는 formal CrossAudit/visual PASS가 아니다. B startup·힐 이전 wave presentation clock warning2(hpAuthorityUnchanged=true)와 frames0 empty MOVE FAIL은 별도 관찰로 유지한다. “경기 시작 전”/HoT clock 인과/이미 VFX 해결로 확대하지 않는다.

### 23.2 남은 범위와 실행 제한

전체 Bloom OPEN: visual/VFX·Client HP/text 전수·skill HoT·invalid-clock·movement-replaced coverage는 미시험이다. 사용자 최신 요청은 로그만이며 새 육안 수용 또는 VFX 제작 승인이 없다. 이전 역할 미확인 문구는 당시 이력으로 보존하되 이번 focused 관측은 두 역할을 포함한다.

이번은 기존7문서 분석 동기화만 승인됐다. source/정책/수치/에셋 변경·자동 code cleanup·신규 Task/TC/QA/validator·UI/build/Git/checker 실행 없이 마친다. 기존 memory/event-local 교훈은 유지하고 WIP/baseline을 변경하지 않는다. 후속 구현/검증을 자동 실행하지 않으며 최종 checker는 메인 담당이다.
