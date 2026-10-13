# BloomFairy 회복 타이밍 교정 조사

**§23 최신 역할교대 saved-log 분석(2026-10-04) — first-entry/due/postdispatch cooldown/등록 기준 duration focused 관측 수용·전체 Bloom OPEN:** Android Host/Editor Client 및 Editor Host/Android Client 신규 두 경기에서 Expired27/27이 등록 후3.001368~3.107432초·now>=Deadline, early0이었다. 각 경기 FirstEntry7/7 및 exact Play/Marker55/63 일치. 별도 B 시작/rebind wave clock 경고는 미해결 관찰이며 HoT 인과 증거가 아니다. visual/VFX·Client HP/text 전수·skill HoT/invalid-clock/movement-replaced coverage는 미시험이다. §21 실패 baseline/§22 구현·메뉴 이력 보존, 이번은 로그 분석·문서만. 상세 기존 Bloom Research/Plan §23.

**§22 최종 native 인계(2026-10-04) — source1파일·static4 errors0·실제 새 기존 두 메뉴 PASS / 새 HoT runtime·전체 OPEN:** final source16:24:35 뒤 실제 Assets Refresh로 native Assembly-CSharp16:28:31/Editor16:28:32 갱신, reload busy 해소 및 Unit Action/Root SelfValidate 새 실행 PASS를 메인이 확인했다. 새 production HoT fixture·Analyze·게임 runtime 증거는 아니며 이번 빌드 없음. 아래 import/메뉴 대기는 당시 이력, §21 조기 종료 baseline·정책/수치 불변과 runtime OPEN 유지. 상세 기존 Bloom Research/Plan §22.6.

**§22 최신 구현 인계(2026-10-04) — 공통 continuous Heal 등록 기준 duration source 완료·새 static4경로 errors0 / 두 Unity 메뉴 대기·runtime OPEN:** 메인 review로 UnitCombatUseCase.cs 한 파일 반영을 확인했다. Bloom+skill HoT의 scaled 등록/deadline 계산이며 수치·authority·에셋/UnitView·ticker·DoT 불변이다. 실제 Assets Refresh 뒤 Unity import 진행 중이고 native DLL은 아직 이전14:15본이다. 이번 빌드 없음·새 메뉴 PASS 주장 없음. §21 조기 종료 증거는 교정 전 이력, 새 duration runtime 수용은 OPEN이다. 상세 기존 Bloom Research/Plan §22.5.

**§22 최신 단계 — 등록 기준 공통 연속 Heal duration 교정 제안 / PLAN·미구현·구체 계획 승인 대기:** 메인이 검토한 programmer proposal을 기록했다. 문서 추가만 승인됐으며 코드·검증·빌드는 실행하지 않았다. §21 관측 수용과 잔여 OPEN은 그대로 보존한다. 상세 §22; 전체 Bloom OPEN.

**§21 최종 로그 판정 — first-entry/due/postdispatch cooldown focused 관측 수용·전체 OPEN:** 전문 Timing 집계와 메인 raw/source 검토의 최종 인계를 추가했다(§21.5). 실제 HoT 등록→종료 약70ms 짧은 잔여는 별도 OPEN·제안만이며 코드 미수정이다. visual/VFX·Client HP/text 전수·movement-replaced 재진입 수용 없음. 아래 Timing 대기는 당시 단계이며 §20 이후 새 빌드/자동 검증을 실행한 것이 아니라 saved-log 분석·문서화만 했다.

**§21 최신 두 실기 — first-entry·반복 전달 회귀의 로그 한정 관측 수용 / Timing·HoT 최종 대기·visual/VFX 및 전체 OPEN:** 생산13/13의 FirstEntry accepted와83회 exact Host/Client Play가 메인 분석으로 확인됐다. 최신 terminal revision 수신 누락 근거는 없으나 callback pose는 pixel/model 시각 검증이 아니다. 사용자 육안 확인은 VFX 제작 후 선호하며 제작 승인이 아니다. 상세 §21, 아래 runtime 대기는 이전 단계 이력으로 보존한다.

**§20 최종 상태 — 최소 source 구현 완료·새 static4경로 errors0·실제 두 기존 메뉴 PASS·빌드1회 시도 / runtime 재시험 OPEN:** 메인 최종 native 인계로 새 Unit Action/Root SelfValidate PASS 및 Build And Run 직후 JDK checking 진행창까지만 확인했다. 완료·설치·runtime 미확인/성공 주장 없음. 상세 §20.4; 아래 메뉴/build 대기는 당시 이력이다. 클립/수치·authority·post3 정책 불변, fresh-first-entry production runtime fixture 미실행.

**§20 최신 코드 최종 인계 — 초기화 순서 교정 반영·새 정적4경로 errors0 / 새 메뉴·빌드 사실 대기·실기 OPEN:** Sagan 최종 보고와 메인 source review가 일치한다. UnitView의 same-object component 확보를 Initialize/첫 권위 검사 전에 연결했고 기존 권위·쿨다운·owner 계약과 이전 순서 주석을 보존했다. 상세 §20.3. 아래 구현 대기는 당시 이력이며 §19 사용자 실패·private cache 미관측을 새 runtime PASS로 소급하지 않는다.

**최신 승인 단계 — §20 첫 힐 의존성 확보 순서 교정 계획 / 구현 인계 대기:** 사용자 승인으로 §19 제안을 최소 교정·검증 후 빌드 시도 범위로 진행할 계획이다. 실제 NetworkUnit을 첫 authority guard 전에 resolve/capture하되 서버 권위·쿨다운·stale owner 계약을 유지한다. Sagan의 읽기 전용 proposal 및 메인 GO/실제 코드 인계 대기이며 아직 새 구현/메뉴/빌드 결과는 없다. §19 FAIL·private cache runtime 미관측·새 실기 OPEN은 유지하고 아래 미승인 제안은 당시 이력이다.

**최신 상태 — §19 새 초기화 순서 회귀 진단 / FAIL·OPEN:** 마지막 §18 교정에서 추가한 첫 힐 authority guard가 뒤의 lazy HealNow 캐시 초기화에 의존해 fresh network healer의 진입을 차단할 수 있는 source 결함이 확인됐다. 사용자 반복 힐 부재·제자리 Walk 및 최신 양 peer BLOOM-HEAL0과 부합한다. private 캐시값/정확한 정지 인스턴스 runtime probe는 없어 모든 인스턴스 인과 증명은 아니다. 에셋/클립 원인이나 서버 권위 완화로 해석하지 않는다. 이번은 진단 문서만이며 수정은 제안·미구현, 아래 자동 PASS/빌드 시도/§17~§18 증거는 이력으로 보존한다.

**이번 승인 계획의 목적과 보존 원칙:** 회복량·주기는 그대로 두고 힐 방향과 연속 모션 연결을 교정한다. 기존 끝 자세 강제 Play/speed0 및 힐 snap 경로는 검증 전 삭제하지 않고 최소 비활성화로 보존한다. 이 단계는 task 두 파일의 계획 갱신뿐이며 코드·에셋·메모리는 수정하지 않는다.

이번 교정의 목적은 꽃요정이 회복을 시전할 때 상대 기기에서도 회복 모션이 보이고, 멈춘 자리에서 걷는 모습이 끼어들지 않도록 하는 것이다. 앞선 표식·생산 연결 교정만으로는 이 문제가 해결되지 않았다. 회복량과 주기는 바꾸지 않고, 서버가 결정한 회복 행동을 화면에 전달하는 빠진 연결을 조사한다.

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

### 이전 단계의 목적 (원문 보존)

꽃요정이 회복을 시전할 때, 회복 모션의 1초 지점에 연출 표식을 넣어 기존 회복 시작 시간과 맞추는 작업이다. 실제 체력 회복은 이미 서버의 타이머가 담당하므로, 회복량이나 회복 주기를 바꾸지 않고 빠진 표식과 실제 생산 연결만 확인한다.

## 1. 승인 및 증거 경계

- 사용자가 Research.md/Plan.md 작성, 구현 및 빌드 **시도**를 명시 승인했다. 추가 승인 질문 없이 메인이 계획 공유 후 game-programmer에 인계한다.
- 현재는 초기 조사/계획 단계다. Galileo의 읽기 전용 조사 반환과 검증된 구현 인계는 대기 중이다. 조율·결과 검토는 메인만 담당한다.
- 이번 문서 작성자는 두 Task 문서만 생성한다. Git, Testcase, QA, 무관 파일 변경은 금지한다. StatsReference/Matrix 및 최종 결과는 검증된 인계 전 갱신하지 않는다.
- 메인 제공 최신 범위: Torrent focused 작업은 완료로 분리하고 clock/text loss는 별도 보류한다. 기존 공용 메모리의 날짜별 OPEN 기록을 이번 Bloom 작업의 완료/실패 증거로 재사용하지 않는다.

## 2. 디스크에서 확인한 생산 에셋 및 수치

| 확인 대상 | 현재 사실 |
|---|---|
| `Assets/_Project/Animations/Units/BloomFairy/BloomFairy_Attack.anim` | `m_SampleRate: 30`, `m_Events: []`; 저장된 OnAttackHit 없음 |
| `Assets/_Project/Animations/Units/BloomFairy/BloomFairy.controller` | Attack 상태 Motion은 fileID 7400000 / GUID `b934af543a2aa8f4dbc72962f233fa9f`로 위 Attack 클립 참조 |
| `Assets/_Project/Prefabs/Units/Transcendence/Unit_BloomFairy_Blue.prefab` 및 `Unit_BloomFairy_Red.prefab` | 두 Animator 모두 controller GUID `c6477081339691f409654646f2f94bd1` 참조 |
| `Assets/_Project/Resources/Config/UnitStatsConfig.asset` | `unitType: 27`, `hitFrameTimes: [1]`, `attackCooldown: 3`, `isHealer: 1`, HP500/공격0/사거리4/감지4/속도1/생산20초/골드150/인구1 |
| `Assets/_Project/Resources/Config/SpecialAttackConfig.asset` | `_bloomHealAmount: 200`, `_bloomHealDuration: 3` |
| `Assets/_Project/Resources/Config/UnitEffectConfig.asset` | `unitType:27`의 attackPreset/hitPreset/tracerPreset은 fileID0. 공격 VFX preset 미연결; deathPreset은 별도 연결 |

메인의 Unity 직접 관찰은 frame30=`1:00` 선택까지다. 선택된 프레임은 이벤트 생성·저장 증거가 아니다. 위 클립 GUID와 Controller GUID는 각각 `.meta`와 대조했다. 씬의 생산 등록·실행 경로 최종 확인은 programmer 조사 반환에서 보강한다. 기존 `Assets/Editor/Setup/WireFloraProductionLine.cs`는 FloralNursery(31)→BloomFairy(27), requiredStage2 배선 도구이고 `RegisterBloomFairyPrefabs.cs`는 UnitFactory의 Blue/Red 등록 도구다. 이번에 이 셋업들을 재실행하지 않는다.

## 3. 회복 경로와 불변 조건

- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs`: 저장된 UnitStats 행의 HitFrameTimes·AttackCooldown·IsHealer를 Domain 스탯으로 전달한다. 직렬화 값이 코드 기본값보다 우선한다.
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`의 `EnterHealLoopV3`: 부상 아군 탐색 → `PlayHealCastAnimation(targetId)` → `HitFrameTimes[0]` 대기 → `CastHeal` → 발동 후 AttackCooldown 대기. 현재 설정상 준비1초+발동 후3초=총4초 예외다.
- `PlayHealCastAnimation`은 기존 공격 scope를 닫고 힐 모션을 재생한다. `OnUnitHealCastStarted`에는 시전자 Id만 발행하며 대상 Id payload는 없다. 이 사실을 일반 공격 target/scope 전달 완료로 해석하지 않는다.
- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`의 `CastHeal`: 제거·사망·만피 대상은 무시하고 TransPlant 연구 배율 적용 후 HoT 부여. 주석의 과거20HP 예시가 아닌 현재 저장값200을 기준으로 한다.
- 서버/싱글 전용 timed-effect 틱과 `NetworkHealthSync`를 통한 HP·ShowText 동기화는 기존 경로를 유지한다. Units 규칙34의 diff 틱, 갱신=리셋/중첩 없음, 규칙37의 정상 종료 시 현재HP 텍스트1회/사망 시 생략을 바꾸지 않는다.
- `UnitAttackShadowProfileResolver.cs`의 Bloom 행은 `SeparateHealPipeline / NotApplicableAttack / Hitscan / impactCount0`이다. 연출 marker 추가는 Supported 승격이나 정규 공격 결과 생성이 아니다.

## 4. 근거와 아직 확인하지 않은 것

**메인의 programmer 조사 검증 인계(초기 문서 보강):** 최소 구현은 Attack clip의 OnAttackHit1초, `Assets/_Project/Scenes/Game.unity` UnitFactory `_transcendencePrefabs`의 type27 `attackTimelineClip`에 GUID `b934af543a2aa8f4dbc72962f233fa9f` 명시 바인딩, `RunUnitActionSelfValidation.cs`의 소형 Bloom production gate다. UnitView/Network/HoT 로직, VFX 생성 및 Client 반복 phase 교정은 수정하지 않는다. 이는 조사·계획 인계이며 구현 저장/검증 완료 증거가 아니다. 앞 절의 조사 반환 대기 기록은 최초 작성 시점 이력이다.

CLAUDE.md, WORKFLOW.md 전문, GameSystemRules.md 인덱스, Units 규칙30~37, UnitCombatSynchronization의 서버 권위·Animation Event·Periodic·마이그레이션 계약, StatsReference Bloom 행 및 Matrix Bloom 행을 기준으로 한다. `.claude/MEMORY.md`, document-manager 정의/메모리와 mistakes 인덱스를 확인했으며 production marker/config 교차 확인 및 enumValueIndex 오선택 실패 기록을 적용한다.

Matrix의 현재 Bloom 행은 기본 이벤트0/타이머1초, 별도 Healer 경로, Provisional+MigrationRequired다. 이번 목표는 이벤트0이라는 에셋 누락을 좁게 교정하는 것이다. 실제 저장·재로드, 실제 연출 소비, 반복 시전, Host/Client 표시 및 빌드 완료는 아직 확인하지 않았다. PASS나 사용자 완료를 기록하지 않는다.

## 5. 검증된 디스크 구현 인계 (2026-10-03)

메인이 실제 저장 파일을 확인해 구현을 인계했다. 문서 담당자도 이벤트 YAML, Game type27 참조 및 gate 본문을 다시 읽었다. 앞 절의 이벤트0/조사대기 표기는 교정 전 이력이며 현재 상태는 아래와 같다.

- `Assets/_Project/Animations/Units/BloomFairy/BloomFairy_Attack.anim`: OnAttackHit 정확히1개/1.000초 저장, 기존30fps/3초 loop 유지.
- `Assets/_Project/Scenes/Game.unity`: `_transcendencePrefabs` type27 attackTimelineClip=fileID7400000/GUID `b934af543a2aa8f4dbc72962f233fa9f`; 기존 Blue/Red 참조 유지.
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`: `ValidateBloomFairyProductionTimeline` 및 성공 시 출력 경로 추가. 정확한 production clip/controller/양 prefab/씬 등록/marker/config와 N/A 별도 힐 경로를 확인하는 소형 gate다. **소스의 PASS 출력 추가는 실제 Unity 실행 PASS가 아니다.**

구현 파일은 위3개뿐이라는 메인 검증 인계다. UnitView/Network/HoT 및 서버 설정은 변경하지 않았다. 공격 VFX preset은 여전히 미연결이며 VFX 제작·Client 반복 phase 교정도 하지 않았다. StatsReference Bloom 행과 Matrix Bloom/누락 이벤트 집계는 승인된 이번 인계 범위로 갱신했다. Unity validation·빌드 관찰은 메인 사실 인계 대기, 사용자 실기 및 완료 판정은 OPEN이다. 상태/메모리/TC는 변경하지 않는다.

**programmer 최종 인계 추가:** 세 변경 파일은 추가 구간을 제외하면 각각 원본 SHA와 일치하며 UnitStats/SpecialAttack/Effect config·controller/prefabs는 무변경이다(인계 근거; 문서 담당자가 별도 hash 재산출한 것은 아님). Unity 재컴파일/domain reload 중 최초 self-validation 클릭은 구 assembly 실행으로 새 Bloom PASS가 없었다. 이를 교정 검증 성공으로 기록하지 않는다. 메인의 새 assembly 두 메뉴 재실행과 빌드 사실을 기다린다.


## 6. 새 assembly Unity 자동 검증 (2026-10-03, 메인 직접 관찰)

재컴파일/domain reload 이후 메인이 새 메뉴 실행과 로그를 직접 확인해 다음 사실을 인계했다. §5의 구 assembly 실행 및 자동 검증 대기 표기는 이전 단계 이력이며, 이번 결과로 새 production gate 실행을 확인했다.

- Console 03:42:38: Unit Action self-validation **PASS**, 새 `BLOOM-HEAL-TIMELINE` 로그 확인. 근거: 메인이 확인한 `Editor.log` 4143312행.
- 메인이 이후 새 메뉴를 클릭한 Root Cross Audit SelfValidate도 **PASS**. 근거: `Editor.log` 4143397행. 실제 경기의 formal CrossAudit Analyze 판정이 아니라 자동 SelfValidate 결과다.
- 최신 Console 캡처: errors0 / warnings10. 경고 원인별 분류나 전체 무경고를 뜻하지 않는다.
- 별도 standalone Roslyn 검사는 실행하지 않았다. 이를 실행·통과했다고 기록하지 않는다.

이 결과는 Unity 자동 게이트 실행 PASS에 한정한다. marker/timer의 런타임 동시성, 반복 시전·Host/Client 표시, focused 사용자 실기 PASS 또는 Supported 승격을 뜻하지 않는다. **메인의 Build And Run 시도는 아직 대기**이며 빌드 시작·완료·설치·실행 성공은 관찰되지 않았다. 사용자 완료 판정은 OPEN이다. 서버 회복 정책·VFX 미연결·N/A 별도 힐 경로 및 구현3파일 범위는 유지한다.

## 7. 최종 인계 — 빌드 시도 관찰 (2026-10-03)

메인이 File 메뉴의 Build And Run을 클릭했고 직후 화면 갱신에서 `DetectJavaDevelopmentKit / CheckingJDK`를 직접 확인했다. 따라서 승인된 **빌드 시도는 수행 완료**다. 빌드 완료·설치·실행 성공은 확인하지 않았으며 추가 빌드 polling은 하지 않는다. §6의 시도 대기 표기는 이전 단계 이력이다.

최종 상태는 **구현3파일 반영 + 새 assembly Unity Unit Action/Bloom production gate 및 Root Cross Audit SelfValidate PASS + 빌드 시도/JDK 확인 관찰 + 사용자 실기 대기(OPEN)**다. 독립 Roslyn 미실행, focused runtime PASS 없음, Supported 승격 없음. 이번 최종 갱신은 Research.md/Plan.md/StatsReference.md/UnitCombatAssetMatrix.md 네 문서에 한정하며 다른 문서·에이전트 메모리·TC는 변경하지 않는다.

## 8. 실기 FAIL과 힐 표현 경로 교정 조사 (2026-10-03 후속)

표식과 생산 연결이 올바르더라도 Client에서 그 클립을 시작하지 못하면 회복 모션은 나타나지 않는다. 이번 후속은 저장 에셋 검증과 실제 회차 전달·재생 검증을 분리한다.

### 8.1 최신 실기 증거와 한계

- **메인/사용자 인계 근거:** Editor Host / Android Client, session `9a2df951...3962e`, 03:59~04:02. BloomFairy 9기: UnitId `4, 9, 22, 27, 42, 47, 61, 66, 79`. Attack 미재생·제자리 Walk가 보고되어 모션 판정은 FAIL/OPEN이다. 세션 전체 키·원시 로그 위치·exact 시전 로그는 이번 문서 담당자가 재수집하지 않았으며, 축약 키를 완전 키로 복원하지 않는다.
- Client local ROOT PASS는 pose 경계의 결과이지 Animator 모션 PASS가 아니다. Client MOVE frames=0은 해당 서버 이동 관측 분모가 없다는 한계이며, 제자리 Walk가 없었다는 증거가 아니다.
- Heal/HP exact 로그가 없어 시전 회차와 HoT 부여·각 HP 변화·종료 텍스트의 정확한 시각/누락 여부를 판정할 수 없다. HP 정상·실패 어느 쪽도 이 모션 실패에서 추정하지 않는다.
- §6의 Unity 자동 PASS는 기존 production marker/config/binding 게이트의 실제 실행 이력이다. Client 런타임 회차 재생이나 후속 교정 PASS로 재사용하지 않는다. §7의 JDK 관찰도 빌드 완료 증거가 아니다.

### 8.2 직접 읽은 소스의 확정 단절

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`의 `EnterHealLoopV3`는 서버/싱글 힐 경로에서 대상 탐색 → `PlayHealCastAnimation` → 준비 타이머 → `CastHeal` → 발동 후 쿨다운을 수행한다. `PlayHealCastAnimation`은 Host/싱글에서 직접 CrossFade하고, 네트워크에는 `OnUnitHealCastStarted`로 시전자 Id만 발행한다.
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`의 `OnUnitHealCastStartedHandler`는 `SetUnitAnimState(unitId, UnitAnimState.Attack)`만 호출한다. 일반 공격은 별도 `StartCombatClientRpc` payload로 표현을 시작하지만, 이 힐 handler에는 대응하는 시작 전달이 없다.
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs`의 `ApplyAnimState` Attack 분기는 `UnitAttackPresentationPolicy.ShouldStartAttackFromReplicatedLevelState()`를 통과해야 한다. `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackPresentationPolicy.cs`의 해당 정책은 **무조건 false**다. 따라서 힐의 Attack 레벨 값만으로 Client 클립을 시작할 수 없다. 이를 일반 공격 안전 가드를 풀어 해결하지 않는다.
- 이 단절은 Client Attack 미재생의 소스 근거다. 제자리 Walk의 모든 발생 시각·writer 경쟁·Host 증상까지 exact 런타임 원인으로 증명한 것은 아니다. 힐 중 Walk 덮어쓰기와 종료 후 복귀는 후속 구현 인계에서 별도로 확인한다.

### 8.3 권위 계약과 충돌 주의

`GameSystemRules_Units.md` 규칙32~37 전문 및 `GameSystemRules_UnitCombatSynchronization.md`의 `NET-AUTH-001/002`, `NET-ROOT-001~004`, `NET-PRESENT-001~004`를 확인했다. 힐러 독립 경로, 서버 타이머/HoT 단일 writer, Client 표현 전용, Client Simulation Root 쓰기 금지와 승인 계획은 양립한다.

**원문 주의 및 사용자 확정 보강:** Units 규칙22는 일회성 RPC만으로 애니메이션 상태를 유지하는 것을 금지하고 스폰·Initialize 뒤 현재 값의 멱등 재적용을 요구한다. 이번에는 **별도 힐 atomic snapshot NetworkVariable**로 회차 식별·시각·방향·시작/종료 상태를 함께 복제한다. 새 sequence는 해당 힐 회차 모션을 재생하고 같은 sequence 재적용은 멱등하게 유지하며, 종료 상태와 사망 취소는 늦은 시작보다 우선한다. late spawn/init에서도 current-state 복원이 가능해야 한다. 순수 Client는 모션만 소비한다. 이 힐 전용 예외의 명문화는 승인됐지만 구현 완료 증거는 아니다. 일반 공격의 연속 재생·되감기 금지와 Attack 레벨 시작 차단은 유지한다.

### 8.4 승인된 후속 문서 범위 — 최종 구현 인계 전 미수정

- Units 규칙22·32: 힐 전용 값 기반 atomic snapshot/회차 재생·멱등·종료/사망 취소 및 Client 표현 책임을 명문화한다. 규칙33~37은 대상·시간·회복·텍스트 불변 확인 대상이며 원수치는 변경하지 않는다.
- UnitCombatSynchronization: Root/AUTH/PRESENT 경계를 보존한 `SeparateHeal` 예외 근거를 명시한다. 새 힐 sequence의 재생을 일반 공격의 연속 재생 계약이나 정규 공격 C3/Supported 승격으로 확대하지 않는다.
- UnitCombatAssetMatrix Bloom 행 및 StatsReference Bloom 비고: 최종 구현과 일치할 때 실제 힐 모션 전달 상태와 검증 경계만 반영한다. 수치 변경 없음.
- 실수 기록 **추가 완료（후속 명시 승인）**: 과거 asset gate로 runtime gate를 대신해 production 힐 이벤트의 발행→구독→Client 실제 재생 연결 확인을 누락한 사건을 `.claude/mistakes/unit-action-correction.md`의 2026-10-03 BloomFairy 항목 목차·본문에 영어로 기록했다. 초기 후보 보고 뒤 사용자가 필수 즉시 기록을 지시한 결과다. 소스 충돌 확정과 runtime exact 인과 미확정을 분리했으며, 기록 완료가 교정 완료는 아니다. 기존 메모리는 수정하지 않았다.

**방향 경계 보강 — 메인의 진행 중 구현 인계:** snapshot의 Aim은 회차 방향 증거용이다. 구현 방향은 Client Root/Visual offset에 새 회전을 쓰지 않고 기존 복제된 서버 Root 방향을 보존하는 것이다. Aim 필드가 있다는 사실만으로 새로운 Visual aim 회전 적용을 기록하지 않는다. 최종 구현 검토·검증 결과는 아직 대기 중이다.

후속 세부 구현을 메인에게 인계받은 뒤 위 승인 범위에서 task와 관련 규칙/Matrix/Stats를 최종 구현에 맞춘다. 최초 문서 단계는 두 task만 변경했고, 이후 별도 명시 승인으로 위 실패 기록1파일을 추가했다. 이번 정합성 수정은 두 task에 한정한다. 코드·Git·앱 도구·TC/QA·새 빌드 실행 및 실제 PASS 판정은 하지 않았다. 추가 메모리 읽기/수정 및 skill setup 부가파일 생성은 하지 않는다.

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

## 10. 최신 실기 분석 — 힐 전달 성공, 방향·모션 연결 OPEN (2026-10-03)

사용자가 분석 후 갱신을 승인했고 메인이 로그 분석·읽기 전용 소스 감사의 최종 사실을 인계했다. 이번 절은 그 승인 인계에 근거하며 문서 담당자가 새 실행·빌드·코드 교정을 수행한 결과가 아니다. §9의 실기 전 상태와 앞선 FAIL은 당시 이력으로 보존한다. **전체 판정은 OPEN**이다.

### 10.1 경기 분리와 exact 회차 증거

- Editor Host / Android Client, sharedSessionKey `6239bf4719287cdd515f7d5ac1f17bc7c903d4dbe1f25c25ff3e6c6393a9faf1`. 분석 입력은 `Assets/_Project/Docs/_Logs/2026-10-03/12_39_logcat/RuntimeLog_device.txt`의 **current 12:36~12:39**와 `Assets/_Project/Docs/_Logs/_editor/2026-10-03/RuntimeLog.txt`의 같은 세션이다. device에 섞인 OLD04:02와 editor daily의 다른 경기 기록은 제외했다. resource/team 표기만으로 실제 화면 팀을 추정하지 않는다.
- BloomFairy 8기, UnitId **4, 8, 18, 23, 34, 38, 50, 54**. lifetime-unit-sequence join에서 **Host Start50 = Host Play50 = Client Play50**, target/serverStart도 일치하며 missing/extra/conflict/duplicatePlay는 모두0이다. 이 경기에서 힐 시전 전달·재생 누락은 관측되지 않았다.
- 양쪽 각각 Marker49 / MotionEnd45. Play50과 분모가 다르며 미완 회차의 사유를 단순 차감으로 확정하지 않는다.
- 서버 CastOutcome49 = Applied43 + TargetUnavailable6. HoTResult43 = Expired8 + FullHP7 + Superseded20 + TargetUnavailable8. **server ActualHealed 합4110**은 서버 회복 결과이며 Client HP 틱·텍스트 exact 상관검증이 아니다.

### 10.2 시각과 모션 경계

- Host CastDispatch elapsed **0.9665045~1.0258641초, 평균1.0031611초**. 로그/타이머의 clock domain을 구분하며 이 수치로 육안상 early heal을 확정하지 않는다.
- Client Play **32/50**에서 Now < StartedAt. 전체 최소차 **-0.3732135초**, 최대 양의 차 **+0.3592207초**이며 음수 offset을0으로 clamp한 뒤 즉시 재생한다. 이는 받은 시계값과 시작값의 차이다. 실제 조기 화면 재생량·network RTT·clock regression을 입증하지 않는다.
- MotionEnd → 다음 연속 Play **42쌍**의 peer별 wall gap: Host **0.901~3.020초 / 평균1.114881초**, Client **0.664~3.091초 / 평균1.094690초**. 서로 다른 peer의 wall/서버 시각을 하나의 절대 시간축으로 합치지 않는다.
- 기본3초 모션 끝에서 speed0으로 최종 pose를 hold하고 실제 힐 주기는4초인 소스와 약1초 정지 구간은 양립한다. **그 hold 자체는 규칙 위반이 아니다.** Unit23의 긴 Start 간격6.021912초에는 다음 seq4 전 inactive Priority4 대기가 포함되므로 강제3초 freeze로 단정하지 않는다.

### 10.3 Unit23 실제 inactive revision 전달 — 누락 가설의 정정

이전 감사의 일반적인 coalescing 가능성 caveat를 이번 경기의 rev9 누락 주장으로 적용하지 않는다. **이번에는 Walk → Held → Walk 입력이 실제 Client에 전달됐다.** 아래 행 번호는 메인 최종 로그 인계 anchor다.

| 경계 | 서버/Host 증거 | Client 증거 |
|---|---|---|
| seq3 모션 종료 | MotionEnd 36010, 12:38:18.660 | 렌더 프레임/pose는 미확보 |
| inactive 종료와 Walk | rev6 EndReason1 12:38:19.772 → rev7 Walk EndReason0 | rev7 Receive94030 / View End94040, 12:38:20.892 / .894 |
| Held | rev8, 12:38:20.988 | Receive94607 / View End94617, 12:38:22.134 / .135 |
| Walk 복귀 | rev9, 12:38:21.005 | Receive94638, 12:38:22.138 — **누락 아님** |
| seq4 시작/재생 | Play36048, 12:38:21.679 | Receive95122, 12:38:22.732 |

이 입력 순서가 전달됐다는 사실과 실제 표시된 pose의 연속성은 별개다. render frame·bone pose·facing angle 로그가 없어 육안상 끊김의 정확한 발생 경로·원인까지 확정하지 않는다.

### 10.4 통합 소스 감사와 권위 정합 검토

아래는 메인의 최종 읽기 전용 소스 감사 인계이며 정책 변경이나 새 구현 완료 보고가 아니다.

- `UnitView.cs:5250`: 서버 Action 소유권 guard 뒤 Root를 즉시 회전한다. `UnitView.cs:872`의 일반 RotateTowards(270deg/s)는 전투 target용이며 힐에는 지속 추적이 없다. Units **규칙12·15의 점진 회전과 heal snap의 정합 검토가 필요**하다. 이 미해결을 승인된 새 snap 예외로 기록하지 않는다.
- `UnitView.cs:5319`: 새 sequence에서 Attack CrossFade. `UnitView.cs:5340`: elapsed3초에서 최종 pose와 speed0. 기존 규칙22·32/SeparateHeal의 최종 pose hold는 유지한다.
- `UnitHealCastPresentation.cs:66`: future start도 offset0으로 즉시 재생. future-start 대기는 아직 구현되지 않았다.
- `UnitView.cs:5299`: inactive에서 Frozen/Held/Walk 복원, `UnitView.cs:6484`: Held는 Walk 첫 pose 사용. 실제 Walk-Held-Walk 수신 근거와 함께 stationary handoff 검토 대상이다.
- RootMotion OFF, 자동 Walk↔Attack transition 없음, asset Root endpoints 일치. 이것만으로 전체 bone/화면 연결이 맞다고 확정하지 않는다. Aim은 방향 증거용이고 새 Client Root/Visual offset 회전 writer는 없다.

**검토 제안만 존재 / 미구현:** heal-only 서버 점진 facing, future-start wait, stationary handoff. 사용자 승인 없는 규칙·수치 교정은 하지 않았으며 일반 attack의5도 gate를 힐에 임의 추가하지 않는다. 준비1초+발동 후3초, HoT200/3초·서버 단일 writer 및 기존 종료/텍스트 정책을 유지한다.

### 10.5 진단 PASS와 별도 예외의 범위

- Host MOVE82265 frames에서 gate/adapter/ownership conflict·stationaryWalk·spatial/repath 등 보고 실패0. **heal-specific 정지/모션 연속성 coverage를 뜻하지 않는다.**
- local ROOT Host63 / Client48 endpoints PASS, errors0/drop0. Host capacity64 경고가 있어 관측 밖 유닛까지 일반화하지 않는다. 각 peer local PASS는 formal CrossAudit Analyze나 heal-facing/transition PASS가 아니다.
- 별도 Client `ProductionPanelUI.HandleToggleAuto → Update` NullReferenceException **1건**, device64421/64432는 같은 예외의 hook/raw 중복이며 12:37:12.424, 첫 힐 이전이다. 힐 끊김과의 인과는 미확인이고 성능 결함 원인도 별도로 남긴다.
- §9의 자동 메뉴 PASS/빌드 시도 이력은 유지한다. 이번 분석은 새 빌드 완료 관찰이나 새 자동 메뉴 실행이 아니다. **힐 회차 전달 성공 / 방향·모션 연결·Client HP/text 정밀 검증 미해결 — OPEN**, focused PASS/CLOSED·Supported 승격 없음.

### 10.6 이번 분석 후 갱신 범위

Research/Plan, StatsReference Bloom 비고, UnitCombatAssetMatrix Bloom 상태, Units 규칙32 및 Synchronization SeparateHeal의 **검증 상태만** 갱신한다. 규칙의 요구·원수치·코드·에셋은 변경하지 않는다. 기존 document-manager `doc-conventions.md` §11에 지속적 증거 해석 교훈을 추가하고 기존 인덱스/공용 메모리는 짧은 포인터만 반영한다. 새 파일·task·TC/QA·실수 기록 추가·Git/UI/빌드 없음. 경기 수치와 진행 상태의 권위 기록은 이 절이다.

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

## 13. 18_34 로그의 세 observer run 분석 — 2026-10-03

### 13.1 목적·범위·현재 판정

이번 분석은4초 native 클립·0.15초 힐 전용 blend·1초 marker 및 서버 준비1+post3초 교정 뒤 힐 전달/재생 시점과 사용자 보고 방향 증상을 구분하는 것이다. **전달·타이밍 seam 확인 / facing FAIL(사용자 보고), 원인 OPEN**이다. 사용자 방향 증상은 실제 frame/pose/각도 trace로 재현하거나 원인을 확정하지 못했다. 전체 PASS/CLOSED·facing 해결·모델 offset 교정 필요를 단정하지 않는다.

사용자의 영구 선호에 따라 분석 결과를 재질문 없이 기존 문서에 반영했다. 이 분석 요청은 코드·에셋·빌드 변경 승인이 아니다. 정책·스탯값·marker1초(30fps1:00)·저장 clip4초(4:00)·힐-only blend0.15초·준비1+post3=cycle4·HoT200/3초·서버 writer는 불변이다. §12의 구현/자동 gate/빌드 시도는 이력으로 보존하며 빌드 완료·설치·실행 관찰로 확대하지 않는다.

### 13.2 원시 자료와 run 분리

- Device: `Assets/_Project/Docs/_Logs/2026-10-03/18_34_logcat/RuntimeLog_device.txt`
- Editor: `Assets/_Project/Docs/_Logs/_editor/2026-10-03/RuntimeLog.txt`

A sharedSessionKey: `4427b92cc9755aa988460dc4f62cea4e3f0e265082db5fc6ef2a9036fd3a8089`.
B/C sharedSessionKey: `3a936e70c2c4cea741e0b1124215b2aa8b539d884efe134d5c9acf4b764aef90`.

**3개 distinct observer run이며 한 경기로 합치지 않는다.** B/C는 shared key가 같아도 새 BEGIN/runId/startedAt 경계를 갖는다. 각 run 안에서 exact(lifetime/unit/sequence/target/serverStart)로 join하며 run을 가로질러 회차를 합치지 않는다. BEGIN 원문 anchor는 문서 작성 시 다시 확인했다.

| Run / 시간 | Host runId / BEGIN line / startedAt | Client runId / BEGIN line / startedAt | Bloom unit IDs |
|---|---|---|---|
| A /18:24:56~18:27:23 | a24eae2e8a39443a9a7d3042a62255c7 /36646 /20.130628 | b5c012f16da943a9b92c981fd3e5eb39 /739 /22.815744 |25,28,43,60 |
| B /18:28:37~18:30:25 |3bd493ec6c60427d93d1b59ffae99e82 /37424 /2.805204 |76fca92aa2504f629ed9a7f99164af90 /51898 /3.816479 |83,89,91,95 |
| C /18:30:26~18:32:45 |83b9c23ccb014e22bab869b89badb4f3 /37848 /111.196595 |408511bc8c414134b37d1defcdaab886 /83719 /112.379819 |106,108,114,117,122,129,137 |

### 13.3 회차 전달·재생 시점

| 관측 | A | B | C |
|---|---:|---:|---:|
| Host Start = Host Play = Client Play |20 |19 |85 |
| exact join missing/extra/conflict/duplicate |0 |0 |0 |
| Host marker /Client marker |19/19 |19/19 |84/84 |
| Client early Play |0 |0 |0 |
| Active Receive의 Now<StartedAt |18 |16 |80 |
| 실제 Client Play의 Now−StartedAt(초) |0.0004675~0.032755 |0.0011786~0.0735309 |0.0002689~0.2620651 |
| Self Start |6 |8 |6 |

Start/Play 합은124회이고 각 run의 exact join 손실/추가/중복은0이다. Active Receive 음수 집계는 revision 수신도 포함하므로 새 sequence-only 개수로 부르지 않는다. future-start 값 수신은 존재하지만 이번 관측의 실제 early Play는0으로, 앞선 즉시 재생 seam과 구분한다. 위 clock delta는 RTT나 정확한 화면 지연량·clock regression 증거가 아니다. marker 수를 Start 수와 같다고 주장하거나 말단 미관측 marker를 실패로 단정하지 않는다.

Self Start20/124는 규칙33의 self 허용과 양립한다. source의 zero-distance 대상은 yaw를 유지한다. 이것을 회전 실패·오류 target의 증거로 삼지 않는다.

### 13.4 서버 Cast/HoT 및 observer 경계

| 서버 관측 | A | B | C |
|---|---|---|---|
| CastOutcome |19=Applied18+TargetUnavailable1 |19=Applied19 |84=Applied79+FullHP2+TargetUnavailable3 |
| Dispatch elapsed min~max /평균(초) |0.994846~1.027263 /1.008301 |0.987279~1.020110 /1.006948 |0.981686~1.032442 /1.007499 |
| HoTResult |18: Expired7/FullHP3/Superseded4/TargetUnavailable4 |19: Expired1/FullHP1/Superseded16/TargetUnavailable1 |78: Expired13/FullHP6/Superseded59/TargetUnavailable0 |
| reported ActualHealed 합 |2080 |1779 |5621 |
| local ROOT Host/Client endpoints |62/61 |20/23 |42/41 |
| server MOVE frames |146739 |39658 |36916 |

HoTResult/ActualHealed는 **보고된 completed subset**이며 아군 HP/text 전수·Client 상관검증이 아니다. C의 Applied79와 HoTResult78을 임의로 전수 완료 또는 효과 손실로 해석하지 않는다. Dispatch elapsed는 서버 경계이며 Client Play clock delta와 혼합해 정확한 heal/visual 동시성을 확정하지 않는다.

각 run의 local ROOT는 Host/Client 모두 PASS/errors0이나 formal CrossAudit·target-facing·model-pose PASS가 아니다. server MOVE gate/writer/adapter/conflict/stationaryWalk/dropped는 모든 run에서0이며 heal-facing 표본 전수를 뜻하지 않는다. 별도 Host18:28:02.946 `connection.state_change` ERROR는 첫 run 종료 뒤 다음 BEGIN 전에 발생했다. heal 인과·회전 오류·성능 결함으로 연결하지 않는다.

### 13.5 메인 source 감사와 원인 미확정

- UnitView의 `TransferToAction` 및 release 경로는 정상 소유권 전환으로 확인됐다. 서버 전용 `UpdateHealFacing`는 guard 뒤 공통 `RotateActionTowards`를 사용하며 `GetTargetTransform`은 실제 target Root를 가져온다.
- model/VisualRoot yaw offset writer는 없고 projector의 관점 반전은 함께 적용된다. cache 누락이 실제 발생했다는 증거는 없다.
- `HealTarget`은 시작 때1회 cache하며 회차 중 재조회하지 않는 source 제약이 있다. 이번 run에서 stale/missing cache가 발생했다는 확정 증거와 원인 연결은 없다.

**Aim 의미 추가 source 근거:** `PlayHealCastAnimation`의 AimX/Z는 HealTarget cache 설정 후 state 구성 시 현재 `transform.forward`에서 얻는 값으로, 힐 대상 방향이 아니다. Client는 이 Aim으로 회전하지 않는다. 향후 expected yaw는 실제 서버 caster→target 벡터에서 계산하고 payload Aim과 구분해야 한다. 이것 자체나 cache/guard/reacquire 구조를 이번 방향 오류의 확정 원인으로 단정하지 않는다.
- zero-distance 분기는 실제로 XZdir.sqrMagnitude < .001인 모든 coincident target에서 yaw를 유지한다(self만의 분기가 아님). Self Start20회는 측정 사실이고, 다른 아군과 위치가 겹쳐 이 분기를 탔을 가능성은 이번 증거로 확정되지 않았다. 규칙33 self 허용을 바꾸거나 모델 offset을 임의로 조정하지 않는다.
- `UnitHealCastDiagnostics.cs`에는 yaw/aim/angle/ownership guard skip 정보가 없다. ROOT sparse 자료는 projection 일관성 경계이며 실제 heal-facing frame을 측정하지 않는다.

이는 targeted trace를 위한 관측 통계 수준이다. **사용자 방향 증상 재현·정확한 원인 확정은 불가하며 원인 OPEN**이다. 공통 회전 수학/서버 소유권 구조가 있다는 사실만으로 facing PASS를 선언하지 않는다.

### 13.6 다음 제안 — 별도 승인 필요, 미구현

힐 회차 시작·marker·종료/회전 수렴 경계에서 server caster/target position, expected yaw/current Root yaw/angle, ownership·guard skip·cache·alive/self와 Client Root/Visual/rendered forward를 exact 회차로 함께 기록하는 진단 증거 추가를 제안한다. 이것은 아직 승인된 코드 구현이나 해결책이 아니다. **모델 offset 변경·Client Root writer 추가 fix 금지**, 일반 attack gate/힐 수치·정책 변경 없음.

이번 실제 문서 수정은 task2+관련 상태4문서 및 기존 연결 메모리의 지속적 증거 경계다. 원시 경기 수치는 이 절만 권위 기록으로 두고 메모리에는 복제하지 않는다. 검사기는 task/log를 제외하므로 §13의 run 경계·수치·파일 anchor는 수동 확인하며 검사0은 나머지 문서 정합성을 뜻한다.

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
## 15. 최신 두 테스트 분석 — 전달 확인 / marker 이전 HP 적용 FAIL / 시각 방향 원인 OPEN

이번 분석은 새 진단 로그와 메인의 영상 관측으로 회복 방향과 실제 회복 시작의 관계를 확인한다. Sagan의 읽기 전용 분석 및 메인의 원시 로그/source 재확인 인계를 근거로 한다. 진단 로그가 정상 전달됐다는 사실을 전체 시각 PASS로 바꾸지 않는다. §10~§14는 각 단계 이력으로 보존하며 최신 판정은 이 절이다.

### 15.1 자료와 두 run의 분리

- Editor: `Assets/_Project/Docs/_Logs/_editor/2026-10-03/RuntimeLog.txt`.
- Device: `Assets/_Project/Docs/_Logs/2026-10-03/22_48_logcat/RuntimeLog_device.txt` 및 `23_04_logcat/RuntimeLog_device.txt`(같은 날짜/상위 경로).
- 23_04 device 파일은 22_48의 첫 테스트 전체 prefix를 중복 포함한다. capture 중복을 새 경기나 gameplay 중복으로 세지 않는다. 아래 **두 distinct run**만 집계한다.

| 구분 | sharedSessionKey | Host / Client runId | BEGIN 원시 행 | Bloom UnitId |
|---|---|---|---|---|
| A 첫 테스트 |7eb106dca494d6a350c4be85cec55e04faa98eb0c893e42ee3d9d110234dd97b |a31ceac4d903444687b43481b908eee5 / f49fe0346c62409386ce3716f546a417 |Editor39184 / 22_48 device744 |5,8,22,25,42,44 |
| B 두 번째 |00536f922facc1fe5ba0e552a0539e929f336d7f3d5c72cea2c86464c9839528 |1b17172080af4de08c701a8852cbdf97 / c84c4f92b311437ab51b8e52d8482999 |Editor41735 / 23_04 device83938 |10,12,25,29,41,43,55,57,69,71 |

양쪽 모두 Editor Host / Android Client다. BEGIN startedAt은 A Host27.752301/Client30.009974, B Host15.777788/Client18.221689이다. runId와 peer별 clock을 함께 보존한다.

### 15.2 전달·bounded 방향 관측

| 측정 | A | B |
|---|---:|---:|
| Host Start / Client Play exact 회차 |40 / 40 |70 / 70 |
| join missing / extra |0 / 0 |0 / 0 |
| Host / Client marker |38 / 38 |68 / 68 |
| nonself marker Host/Client paired subset |29 |41 |
| 위 subset max abs angle Host / Client(도) |0.436 / 1.100 |0 / 0.213 |
| self checkpoint Host 건수 |64 |155 |
| self max 시작 baseline drift Host / Client(도) |0 / 0 |0 / 0.003 |
| dispatch / 첫 positive HP 적용 |38 / 35 |68 / 61 |
| Host marker elapsed(초) |1.001~1.036 |1.000~1.043 |
| 최소 dispatch elapsed(초) |0.741 |0.939 |

self checkpoint 건수는 회차/유닛 수가 아닌 관측 건수다. self/near-zero는 target expected angle을 정의할 수 없는 경우이며 wrap-safe 시작 yaw drift를 본다. self Root 이상은 이번 관측에서 발견되지 않았으나 전체 사용자 방향 증상 해소를 뜻하지 않는다. nonself marker의 작은 오차도 위 paired subset만의 결과다. projection yaw residual은 Host0/Client 약0.001도이며 callback transform proxy이지 pixel/bone 방향 검증이 아니다.

각 run의 Start 중2회는 dispatch 전에 닫혔다. A dispatch 중 첫 tick 이전 Superseded3회, B TargetUnavailable5회+첫 tick 이전 Superseded2회로 FirstPositive 차이를 설명한다. 실제 회복 적용이 없었던 회차를 유실된 positive 로그로 자동 분류하지 않는다.

새 probe parts는 관측 범위에서 모두 존재했고 missing parts0, Pose/Facing overflow 및 FormatIncomplete0이었다. 실제 관측 최장 줄은539 UTF-8 bytes다. §14.6의 typical upper838 정적 점검과는 다른 측정이며 보편적 길이/모든 미래 capture 완전성 보장은 아니다.

### 15.3 정상 점진 회전 사례와 확정된 early HP

**정상 점진 회전 B:** Unit12→43, lifetime13/sequence12/revision22. 시작23:01:27.000, server StartedAt123.618930/Local125.956941에서 error−174.016도→다음−162.363도; marker23:01:28.030/Local126.985092에서 error0도였다. 첫 HP260→264는23:01:28.090/Local127.051782다(Editor43849/43887/43892). 이는 큰 시작 오차가 점진적으로 수렴한 사례이며 snap/jump 증거로 부르지 않는다.

**EARLY HP 확정(같은 Host Local clock·exact 회차):**

- A Unit5 self, lifetime6/sequence1: HP440→447의 Local455.20668970379819가 실제 marker Local455.40170340136052보다 **0.19501369756233초 먼저**다. Editor39369는 HoTFirstPositive,39371은 Marker,39372는 같은 marker의 PoseRoot다(메인39369/39372 대조).
- B Unit29→22, lifetime30/sequence4: HP840→847의 Local115.16164943310658가 marker Local115.1909207270408보다 **0.02927129393422초 먼저**다. Editor43394는 HoTFirstPositive,43395는 Marker,43396는 같은 marker의 PoseRoot다(메인43394/43396 대조).

회복 등록/틱 호출이 아니라 writer 이후 실제 HP 증가와 accepted marker를 비교했다. 따라서 **TIMING EARLY HP FAIL**은 관측 사실이다. exact Client HP 수신·화면 HPbar/VFX onset과의 일치나 모든 회차의 early 발생은 입증되지 않았다. 자기 회복에서만 생긴다고 단정하지 않는다.

**연속 self cycle의 제한된 추가 확인:** A Unit5/lifetime6 sequence1→2의 Start interval은3.722193초(설계4초보다0.277807초 짧음)이며 사이에 End/no-target 기록이 없다. Start1 Local454.394247→Dispatch455.132741은0.738493초, Dispatch→Start2 Local458.116440은2.983700초다(Editor39321/39366/39426, Sagan 인계·메인 Start 행 직접 확인). 이 사례는 실제 연속 주기가 정확한4초가 아님을 보여준다. sequence1 MotionEnd 부재는 rendered tail cut의 증거가 아니며 영상 모션 잘림 원인으로 확정하지 않는다. B Unit29 sequence4→5는4.065436초이고 중간 no-target End가 있으므로 연속 짧은 주기 사례로 합치지 않는다. 실제 첫 frame delta/정확한 원인 미확정은 유지한다.

### 15.4 source 의심 경로·다음 제안의 승인 경계

메인 source5277 주변 확인: UnitView의 로컬 `t=0; while { t += Time.deltaTime; yield null; }` 구조는 첫 진입에 이전 frame delta를 먼저 더하며 절대 `StartedAt + MarkerOffset` due-time을 직접 기다리지 않는다. 위 조기 dispatch/HP 증거와 결합하면 **안전하지 않은 시간 구조가 early cast 원인이라는 강한 의심**이다. 실제 첫 frame delta가 기록되지 않아 정확한 lag 원인·전체 runtime 인과 연쇄는 확정하지 않는다.

다음 제안은 서버에서 절대 authoritative dueTime=`StartedAt+MarkerOffset`을 기준으로 발동을 판정하는 것이다. 애니 이벤트를 HP 권위로 바꾸지 않는다. **교정 구현은 아직 승인/실행되지 않았다.** Units 규칙18의 서버 타이머 권위, 규칙32의 독립 힐 경로·연출 marker 분리,33의 self 허용,34~37의 HoT/텍스트 계약 및 Sync SeparateHeal/NET-AUTH/NET-ROOT를 유지한다. native4초(30fps `4:00`), marker1초(`1:00`), 힐-only0.15초, 준비1+post3 및 HoT200/3은 바꾸지 않는다. source/config 상수는 실측 주기·시각 동시성 PASS가 아니다.

### 15.5 영상 실제 관측 범위와 미확보 항목

메인이 직접 확인한 `C:/Users/rmsep/OneDrive/바탕 화면/Record/Movie_001.mp4`는162.47초/1080×1920/평균32.64fps의 variable-frame-rate 영상이다(애니 수치 표기30fps와 별개). 관측은 전체 overview10초 간격,88~96초 crop0.5초 간격,118~130초1초 간격 및24초 full resolution이다. 전 프레임 분석이라고 하지 않는다.

서 있는 힐 유사 모션과 녹색 완료 텍스트는 보였으나 유닛이 작고 HPbar·unitId·UTC가 없어 pixels로 정확한 target-facing 각도나 exact-key 회차를 확정할 수 없다. 사용자 설명상 두 번째 테스트(B) 문맥이나 PTS→UTC exact 동기화가 없으므로 특정 영상 frame을 위 로그 키/HP 시각에 임의 연결하지 않는다. 녹색 완료 텍스트를 첫 회복 시각으로 취급하지 않는다. 사용자 보고 시각 방향 문제와 그 원인은 **OPEN**이다.

### 15.6 현재 결론·문서 담당 범위

전달 exact join과 제한된 marker-facing 수렴/self drift 관측은 확인했다. marker 이전 실제 Host HP 증가2사례는 **FAIL**, 시각 방향·정밀 Client/pixel timing·최종 원인은 OPEN이다. 모델 offset/Client Root writer 교정은 허가되지 않았다. 이번은 분석 문서 갱신만이며 정책·스탯·코드/에셋·빌드/UI/Git·TC/QA/new task 변경이 아니다. §14의 진단 구현/정적 결과와 §12의 이전 자동 gate/빌드 시도는 이력으로 보존한다.

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

## 17. 2026-10-04 Movie_002·새 두 run 분석 — due 경계 관측 수용 / 전체 OPEN

이번은 §16 교정 뒤 실제 로그/영상 분석이며 새 코드 교정 계획이나 전체 BloomFairy 완료 선언이 아니다. Sagan 검증 수치 및 메인의 원시 사례·영상 직접 확인 인계를 따른다. §15 EARLY HP FAIL은 교정 전 이력이고 §16 자동 gate/빌드 시도 경계도 보존한다.

### 17.1 원시 자료·역할·run 분리

Device `Assets/_Project/Docs/_Logs/2026-10-04/00_39_logcat/RuntimeLog_device.txt`에는 과거 prefix가 포함된다. Editor 자료는 `Assets/_Project/Docs/_Logs/_editor/2026-10-04/RuntimeLog.txt`다. 새 두 observer run은 **Android Host / Editor Client**이며 이전 Editor Host/Android Client 자료와 역할을 혼동하지 않는다.

sharedSessionKey는 두 run 모두 `5e937415dcff1327953564cd900242d4b72362bd4befcff2b7842581d014ed3e`다. 동일 shared key를 단일 run으로 합치거나 과거 prefix를 새 경기로 세지 않는다. 직접 확인한 Attack observer BEGIN anchor는 다음과 같다.

| run | Host runId / BEGIN | Client runId / BEGIN | Host 시간 범위 | Client 시간 범위 |
|---|---|---|---|---|
| A |7b81b770ad314e6b87d50753949546dd / device176607 |2eca9fd4e3a544708d7c635d19821625 / Editor43 |00:29:21.020~00:32:32.307 |00:29:21.276~00:32:32.398 |
| B |6823cc32c7cc4d7ea30cc13adc2ead26 / device213012 |556a9b29b016481ca2b4b64661620769 / Editor9335 |00:32:33.938~00:35:22.040 |00:32:34.250~00:35:22.103 |

Host/Client Attack BEGIN startedAt은 A4.766825/33.058583, B224.004035/224.261080이다. 다른 observer의 clock/runId를 이 값으로 대체하지 않는다.

### 17.2 due·marker·첫 positive HP의 정확한 분모

| 측정 | A | B |
|---|---:|---:|
| Host Start / Client Play |75 / 75 |114 / 113 |
| Due Dispatch / matching Client marker |71 / 71 |110 / 110 |
| Dispatch elapsed 최소/중앙값/최대(초) |1.000007 / 1.016584 / 1.033652 |1.000032 / 1.000475 / 1.017476 |
| 첫 positive HP / Host marker 비교 회차 |48 |74 |
| first HP−같은 Host marker 최소/중앙값/최대(ms) |33.300 / 99.973 / 100.100 |33.326 / 100.009 / 116.739 |
| 위 subset HP가 marker 이전/동시 |0 / 0 |0 / 0 |
| 연속 cycle 관측 최소(초) |3.933656 |3.934839 |

**181 dispatch 모두 Now≥Due**였다. **122 실제 첫 positive HP 비교 회차 모두 같은 Host marker 이후**다. 이는 Due guard 관측과 실제 HP가 있는 subset의 marker 순서 수용이며 전체189 Start 모두 HP를 적용했다는 뜻이 아니다. 이 비교는 동일 Host clock 안에서만 한다. Host/Client LocalNow를 서로 빼서 지연·HP 순서를 산출하지 않는다.

B의 Client Play1회 차이는 Unit79/lifetime80/sequence18 Start와 despawn End가 같은 Host frame에 있었고 dispatch가 없는 취소 회차다. 이를 전달 loss로 판정하지 않는다. 취소 회차를 제외하지 않은 Start114 대비 Play113을 무조건100% 또는 손실1로 재표기하지 않는다.

연속 cycle 최소는 여전히4초 미만이다. §16은 marker due만 교정했고 발동 후3초/global ticker는 무변경이므로 이 잔여 항목은 **OPEN**이다. 해당 숫자를 rendered clip tail 잘림 원인이나 완전한 timing PASS로 확대하지 않는다.

### 17.3 Movie_002 실제 영상 관측과 한계

메인 직접 관측 `Movie_002.mp4`는393.11초/1080×1920/평균31.36fps/60tbr다. 영상 metadata와 게임 클립30fps 표기를 구분한다. overview는0~380초20초 간격으로 두 경기 문맥을 확인했고 full resolution150초·300초, crop148~151.75초 및299~306.75초를0.25초 간격으로 확인했다.

흰 Bloom 유사 유닛의 pose 변화/팔 시전 모션과 녹색 HP 완료 텍스트(예:300.5/302.25초 주변 아군)를 관측했다. **이 subset에서 모션이 재생된다는 것만 확인**하며 전체 프레임/모든 회차 continuity·pixel facing·HP onset PASS는 아니다. source unitId/UTC/HPbar가 읽히지 않고 PTS↔UTC exact sync가 없어 특정 영상 frame을 로그 키에 연결하지 않는다. 녹색 완료 텍스트는 첫 HP delta가 아니다.

### 17.4 현재 판정·후속 범위

§16 due 교정은 관측181 dispatch의 due 이전 발동0 및 비교 가능한122 positive HP의 marker 이전/동시0 범위에서 수용한다. 이는 이전 §15 실패의 이번 subset 비재발 증거이지 전체 Bloom closure·모든 Clock/타겟/priority 조건·정밀 Client HP/영상 facing 수용이 아니다. 사용자 최종 visual acceptance는 없고 전체는 **OPEN**이다. residual cooldown cycle과 시각 방향/연속성 한계를 보존한다.

정책/스탯·native4초/marker1초(30fps4:00/1:00)·blend0.15초·HoT200/3 및 서버 writer는 변경하지 않는다. 이번은 분석 문서 반영만이며 새로운 구현·TC/QA·task·UI/build/Git 행동을 승인하지 않는다. 추가 각도/outcome 분모·raw 사례 인계는 검증된 내용만 이 절에 보강하며 없던 수치를 추정하지 않는다.

### 17.5 최종 outcome 보강 — 첫 positive HP 없는 59 dispatch

**근거 구분:** 아래는 코드 담당(Sagan)의 인라인 PowerShell 집계와 메인의 핵심 due/HP 독립 재집계·선택 원시 사례 검토 인계다. 메인은181 DueDispatch·122 HP/marker exact join 및 최소/최대값을 독립 재집계했고, 각도·cycle·취소의 선택 raw anchor를 검토했다. 59 outcome·각도·MOVE 전체 건수는 코드 담당 집계이며 메인이 전량 독립 재측정한 값으로 표기하지 않는다. 문서 담당자가 새 실기/로그 재분석을 수행한 결과도 아니다. 첫 positive HP 비교122회는 lifetime/Unit/sequence/target/StartedAt의 exact join으로 같은 Host marker181회 중 결합했으며, 모두 marker 후33.300~116.739ms다.

| 첫 positive HP가 없는 결과 | A | B | 합계 |
|---|---:|---:|---:|
| CastFullHP |4 |1 |5 |
| CastTargetUnavailable |7 |17 |24 |
| Applied 후 첫 gain 전 Superseded |12 |17 |29 |
| Applied 후 첫 gain 전 TargetUnavailable |0 |1 |1 |
| 합계 |23 |36 |59 |

181 dispatch=positive HP122+위 결과59로 분모가 닫힌다. 59건을 원인 불명 HP loss나 완료된 힐 전달 실패로 분류하지 않는다. 같은 대상의 동종 효과 갱신=리셋은 규칙34의 계약이고, 완료 텍스트는 규칙37에 따라 첫 HP 증가와 별개다. 이는 서버 outcome 분류이지 Client HP/text 전수 일치 증명은 아니다.

B의 Play 부재 사례는 U79/L80/sequence18, device252701 Start→252944 End, frame14966·Local486.749563535 동일, Reason=despawn이며 dispatch가 없다. 종료 전파의 세부 경로는 미확정이므로 완료 힐 loss 또는 모든 취소 전파 정상으로 확대하지 않는다.

### 17.6 잔여 cycle의 raw anchor와 source 한계

| cycle 최소 사례 | 이전 Start → 다음 Start(device / Local) | 이전 Dispatch(device) | cycle / dispatch→다음 Start(초) |
|---|---|---|---|
| A U4/L5 sequence1→2 |177385 / 253.213307150 → 178179 / 257.146962854 |177665 |3.933656 / 2.933551 |
| B U86/L87 sequence6→7, target89 |225562 / 450.501232029 → 227360 / 454.436071216 |226281 |3.934839 / 2.934521 |

같은 caster의 위 인접 Start 사이에 End는 없다. B의 **post-dispatch 절대 최소2.934479초는 다른 U109/L110 sequence1→2 사례**이며 cycle 최소 사례의 값과 섞지 않는다. post3/global ticker는 미교정·OPEN이고 clip tail 잘림의 원인으로 입증된 것은 아니다.

**메인 최신 source 확인 인계(규칙36 관련, 원인 추론만):** UnitView는 CastHeal 뒤 AttackCooldownRemaining을 config3초로 설정하고0까지 기다린다. NetworkCombatController.Update568~588은 전역 이전 tick 이후 realElapsed를 계산하며, TickCombat707~712는 healer 분기 전에 모든 유닛에서 이 elapsed를 차감한다. 따라서 전역 tick 사이 counter reset은 reset 이전 시간을 차감에 포함할 수 있는 unsafe phase seam이다. 관측 post-wait2.9335초와 부합하는 **유력 원인**이나 cast별 실제 global tick phase가 로그에 없어 완전한 runtime 인과 증명은 아니다. 서버 dispatch+3초 deadline은 이를 피할 수 있는 제안일 뿐 이번 분석에서 승인·구현되지 않았으며 새 Plan으로 전환하지 않는다.

### 17.7 callback 각도·자가 시전 checkpoint — pixel 판정 아님

| 관측 | A Host / Client | B Host / Client |
|---|---|---|
| non-self callback marker 표본수 / 최대 오차(도) |57 / 0 · 57 / 0.266 |80 / 0.427 · 79 / 1.127 |
| self checkpoint 표본수 / 최대 drift(도) |65 / 0 · 51 / 0 |99 / 0 · 73 / 0.505 |

A Client marker 최대는 Editor2545 U7/L8/sequence4 target4다. B 최대 callback 사례 anchor는 Host device229118 / Client Editor13480, U74/L75/sequence12 target89다. B Client self 최대는 Editor15648 U79/L80/sequence15, yaw357.482·baseline357.987·drift−0.505도다. 이 수치는 callback 시점 Root/자가 baseline의 proxy이지 렌더된 모델 픽셀 방향이나 전 회차 facing PASS가 아니다. §17.3 영상 한계 및 사용자 최종 visual acceptance 부재를 그대로 유지한다.

### 17.8 이번 run의 오류·이동 관측과 최종 인계

새 두 run의 ERROR/FATAL/Exception은0이다. Host MOVE A38,544frames(core device212552 / fatal-repeat0 device212573 / END EVIDENCE212586), B37,763frames(core268231 / fatal-repeat0 268252 / END268265)에서 adapter/gate/writer 실패0이다. 이는 이번 이동 관측 범위이며 과거 결함의 보편적 fix나 힐 시각 PASS가 아니다. FPS/frame-time 성능 자료가 없어 lag 해소·성능 개선은 주장하지 않는다(영상 metadata fps와 게임 성능은 별개).

최종 상태는 **due 경계 focused 관측 수용 / 전체 OPEN**이다. first-positive 부재 outcome은 설명됐지만 cooldown residual·시각 방향/연속성·exact Client HP onset은 미완이다. 과거 증거를 보존하고 이번 보강은 기존 Research/Plan과 관련 상태 요약에만 반영한다. 문서 정합성 검사와 Task 수치/표 수동 최종 검토는 메인 세션에 인계하며, 이 문서 보강 자체를 검사 PASS로 기록하지 않는다.

## 18. 2026-10-04 발동 후 설정 쿨다운 deadline 교정 — 승인 계획 / 구현 예정

> **최종 후속 — 코드/정적·새 기존 두 자동 gate PASS / 빌드 시도 관측, 실기 OPEN:** 현재 최종 결과는 §18.7을 따른다. 아래 구현 예정·정적/UI/build 대기는 당시 이력이다. 빌드 완료·설치·runtime은 미관찰이며 성공을 추정하지 않는다.

> **최신 정적 인계:** UnitView 한 파일의 최종 정적4경로 errors0 인계까지 확보했다. 아래 정적 대기는 당시 단계이며 현재 내용은 §18.6을 따른다. 새 기존 두 메뉴/UI·이번 빌드 시도는 실행 대기, runtime/전체 OPEN이다.

> **후속 코드 반영 상태:** 메인 source review로 UnitView 패치의 실제 반영을 확인했다는 인계를 받았다. 아래 계획/proposal은 당시 이력이며 현재 코드 내용은 §18.5를 따른다. 최종 정적·메뉴·빌드 결과는 별도 인계 대기이고 runtime/전체 OPEN이다.

힐을 실제로 발동한 뒤 다음 시전을 시작하기까지 설정된 대기 시간을 온전히 지키도록, 기존 힐 루프의 대기 기준만 교정하는 승인 계획이다. §17의 두 run 증거와 전체 OPEN 판정은 보존한다. §17 당시 미승인 제안이었던 post-dispatch deadline은 **이번 사용자 후속 승인으로 구현 예정 범위가 됐지만, 구현·검증·빌드 시도 완료를 뜻하지 않는다.**

### 18.1 근거와 최소 영향 범위

`GameSystemRules_Units.md` 규칙36은 BloomFairy의 준비1초와 **실제 발동 후 쿨다운3초**를 구분한다. §17.6의 전역 tick elapsed/counter reset seam은 관측된 짧은 post-wait와 부합하는 유력 source 원인이나 cast별 tick phase가 없어 완전한 runtime 인과 증명은 아니다. 이번은 해당 규칙을 구현에서 지키기 위한 시간 기준 교정이며 정책이나 스탯 재설계가 아니다.

코드 담당의 최소 구현 예정 범위는 `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`의 서버/싱글 힐 루프다. 기존 §16의 authoritative dispatch를 실제 수행한 시각과 configured `AttackCooldown`으로 deadline을 정하고, 같은 권위 clock에서 deadline 도달 전 다음 시전을 시작하지 않도록 대기한다. 현재 설정값3초를 사용하되 새로운 고정 상수로 스탯을 대체하지 않는다. 예약 StartedAt·nominal marker due·첫 positive HP·Client marker 시각을 실제 dispatch 기준으로 대신하지 않는다. 기존 global counter 기반 wait는 삭제하지 않고 주석 비활성화로 보존한다. 공통 global ticker 및 다른 유닛 쿨다운은 변경하지 않는다. 구체 코드 제안·구현은 별도 코드 담당이 수행한다.

### 18.2 유지할 계약과 결과 한계

규칙32~35·37 및 동기화 문서 SeparateHeal의 서버 권위/수명·종료 계약은 유지한다. §16의 due guard·단일 CastHeal·사망/종료/권위 상실 차단, 대상 탐색·HoT200/3·HP/텍스트 writer·연구 배율·회전 소유권·Frozen/Held·연속 재생 정책은 이번 교정 대상이 아니다. native clip4초/marker1초(30fps4:00/1:00)·blend0.15초·config cooldown3초 및 다른 타입 정책도 불변이다.

목표는 **실제 dispatch부터 설정 쿨다운이 끝나기 전의 조기 다음 시전 차단**이다. frame 스케줄링·대상/priority/수명 조건으로 이후 시작이 늦어질 수 있으므로 매 cycle이 정확히4.000초라는 보장이 아니다. 시각 방향/clip tail/모션 연속성 원인이나 fix PASS도 아니며 새로운 runtime·사용자 visual acceptance 전 전체 OPEN이다.

### 18.3 승인된 후속 절차 — 아직 결과 없음

코드 담당의 최소 교정 후 정적 검증과 **기존 Unit Action self-validation 및 Unit Root Pose Cross Audit SelfValidate 두 메뉴**의 새 실행 PASS를 확인한 뒤, 메인이 Build And Run을1회 시도하는 순서다. 실패/미확인 gate를 이전 PASS로 대체해 빌드 단계로 넘기지 않는다. 빌드 완료·설치·실행을 감시하거나 추정하지 않으며 완료 monitoring/polling은 하지 않는다. 이는 실행 담당의 승인 범위이고 문서 담당자는 UI/빌드/validator를 실행하지 않는다.

이번 작성은 기존 Research/Plan의 §18 계획 추가만이다. 새 TC/QA·task·상시 문서/메모리·validator·Git·UI 행동은 없으며 실제 코드/검증/빌드 시도 결과의 최종 인계는 별도 지시 후 기록한다.

### 18.4 코드 proposal 리뷰 완료에 따른 승인 계획 보강 — 구현 결과 아님

**근거 주체 교정(기존 표현 이력: “사용자가 전달한 코드 proposal 리뷰 결과”):** 사용자는 narrow fix 범위를 승인했고, 실제 코드 proposal은 코드 담당 Sagan이 작성하여 메인이 리뷰한 뒤 문서 담당자에게 인계했다. 그 리뷰 결과를 반영한다. 실제 `CastHeal` 직전에 `HealNow`를 새로 조회하여 `dispatchAt`을 잡고 당시 configured cooldown을 캡처한다. cooldown은 양수·finite여야 하며 deadline은 `dispatchAt + captured configured cooldown`이다. 대기 종료는 **absolute deadline 도달과 AttackCooldownRemaining≤0을 모두 충족**해야 한다. 기존 글로벌 counter 설정·감소는 유지하고 counter만 기다리던 이전 wait는 주석 보존한다. §16 due guard/권위·회차 검증은 그대로 유지한다.

이동 교체 뒤 새 힐 루프가 deadline을 우회하지 않도록 UnitView 안에 **caster identity/lifetime·dispatch 회차·clock 모드·deadline의 최소 record**를 유지하고, 재진입 시 `Play` 직전에도 해당 owner의 잔여 cooldown을 존중한다. 정상 모션 종료와 이동 교체는 cooldown record를 폐기하지 않는다. death/gameend/disable/despawn/reinit에서는 해당 owner만 폐기하며, 오래된 owner의 정리가 새 record를 지우지 않게 한다.

stale old coroutine은 새 state 종료·회전 release·counter clear·pursuit reset을 하지 않는다. 이는 새 owner를 보호하기 위한 종료/정리 격리이며 새 회전 writer나 표현 정책 변경이 아니다. 기존 keyed `CastDispatch`에 `DispatchAt/ConfiguredCooldown/CooldownDue`, 대기 종료에 `Now/Elapsed/Remaining`을 bounded log로 남길 예정이다. 다른 정책·수치·공통 ticker·HP/HoT/텍스트·§17 증거와 전체 OPEN은 유지한다. 구현 및 새 검증/빌드 시도 결과는 아직 기록하지 않는다.

### 18.5 실제 코드 반영 인계 — 최종 정적·메뉴·빌드 결과 대기

**근거 구분:** 메인이 source review로 패치 실제 반영을 확인하여 전달한 내용이다. 문서 담당자의 독립 코드 검사/QA/실기 결과가 아니다. 실제 변경 범위는 `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` **한 파일**이며 계획을 완료된 runtime 교정으로 읽지 않는다.

- 실제 CastHeal 직전 fresh HealNow의 dispatchAt과 당시 양수·finite configured cooldown을 캡처하는 absolute deadline이 반영됐다. post-wait 및 entry/Play 직전 재진입에서 absolute due와 Remaining≤0 두 조건을 존중한다. 글로벌 counter 설정/감소는 무변경이고 기존 counter-only wait는 주석 보존됐다.
- UnitView 내부 caster identity/lifetime·dispatch 회차·clock 모드·deadline record가 이동 교체→새 힐 루프 우회를 막는 용도로 반영됐다. 정상 모션 종료/이동 교체는 cooldown을 유지하고 death/gameend/disable/despawn/reinit는 해당 owner만 폐기한다.
- lifecycle owner 보호로 stale old coroutine의 새 state 종료·회전 release·counter clear·pursuit reset을 막는다. 기존 keyed dispatch의 DispatchAt/ConfiguredCooldown/CooldownDue와 complete의 Now/Elapsed/Remaining, cancel/reject를 bounded로 기록하는 경로가 반영됐다.

§16 due guard·단일 서버 CastHeal·기존 수명/권위 차단과 타겟/회전/HP/HoT/텍스트·native clip/marker·Frozen/Held·스탯·다른 유닛 정책은 유지한다. §17 source 원인은 유력 seam이지 cast별 완전 runtime causal proof가 아니며 exact4초 cycle·visual facing/연속성·전체 fix는 미입증이다.

**최종 정적 결과·새 기존 두 메뉴 gate·이번 Build And Run 시도 결과는 아직 인계되지 않았다.** 이전 §16 결과를 이번 patch 이후 결과로 재사용하지 않는다. 정적 및 두 기존 메뉴 PASS 뒤 Build And Run1회 시도·완료 monitoring 제외라는 승인 순서는 유지하며, 미실행/실패/성공 여부를 추정하지 않는다. §17 runtime 증거는 이전 patch 상태의 증거로 보존하고 새 runtime·사용자 visual acceptance 전 전체 OPEN이다. 관련 상시 문서는 §18.5 코드 인계와 §17 증거를 구분하는 포인터만 갱신한다.

### 18.6 코드 담당 최종 정적·추가 narrow cleanup 인계 — UI/build 실행 대기

코드 담당의 최종 완료 인계와 메인의 현재 source owner/end-cleanup/reentry/normal-wait review 확인을 기록한다. 문서 담당자가 컴파일/QA를 실행한 결과가 아니다. 실제 source 변경은 **UnitView 한 파일**이다. 추가 좁은 정리는 `EndHealPresentation`의 현재 healer pursuit lifecycle clear이며, old coroutine 자체가 새 state를 없애지는 않는다. reentry `CooldownComplete` 로그는1회만 남기고 record를 clear한다. §18.5의 owner 보호·absolute+Remaining 대기·이전 counter wait 주석 보존 및 다른 정책 불변은 유지한다.

| 최종 정적 경로 | errors | warnings |
|---|---:|---:|
| Runtime |0 |20 |
| Editor |0 |0 |
| Development |0 |20 |
| Release |0 |24 |

코드 담당 인계상 `Temp/bloom-cooldown-compile/{runtime,editor,development,release}.{rsp,dll,ref.dll,pdb,log}` 보조 artefact가 생성됐고 source 목록 차이0, Editor는 새 Runtime.ref를 사용했다. 컴파일 전후 source hash는 동일했다. 보조 artefact는 source/asset 변경과 구분한다. 경고 수 감소는 **최종 정적 카운트**이지 기존 warning의 원인 교정·Unity Console 경고 감소 주장이나 runtime acceptance가 아니다.

**새 기존 두 메뉴와 이번 Build And Run은 아직 실행 대기다.** 최종 정적 인계를 이전 자동 gate PASS로 대체하지 않는다. 새 두 메뉴 PASS 후1회 빌드 시도/no completion monitoring 경계를 유지하며, 메뉴/빌드 결과는 후속 인계 전 기록하지 않는다. §17 evidence 보존·source probable cause/정확한4초 비보장·시각 방향/연속성·전체 OPEN 유지.

### 18.7 최종 기존 두 자동 gate·빌드 시작 시도 인계 — runtime OPEN

**근거 구분:** 메인이 실제 UI 조작과 새 Editor.log tail을 확인하여 전달한 최종 인계다. 문서 담당자의 UI/validator/빌드 실행이나 새 coroutine runtime fixture 결과가 아니다. 새 native Refresh 뒤 Runtime/Editor DLL이05:57:24에 생성된 것을 확인했다.

메인은 기존 Unit Action 메뉴를 새로 실제 클릭하고 Editor.log의 고정 마지막524,288byte를 읽어 **Bloom/overall/Production/C2/C3/AUTH-PRESENT/TORRENT production PASS**를 확인했다. warning fixture anchor **06:01:09.751은 경고 로그 위치이며 모든 PASS의 실행 시각을 대신하지 않는다.** 이어서 `Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit`를 실제 클릭했고 UI status와 새 Editor.log tail에서 **ROOT PASS**를 확인했다. 이는 기존 두 자동 gate의 새 실행 PASS이며 이전 경기 Analyze 또는 새 cooldown coroutine runtime fixture가 아니다. §18.6 최종 정적 카운트와 warning 교정 비주장 경계도 유지한다.

메인이 `File → Build And Run`을 **1회 실제 클릭**한 직후 `Importing - Importing...` 및 `Settings/Mobile_RPAsset.asset` 화면을 관측했다. 이는 이번 빌드 시작 시도/import 관측까지만의 근거다. **이후 모든 Unity UI/build log polling은 중단했으며 완료·설치·runtime 실행은 미관찰, 빌드 성공 추정 없음.** §16의 JDK/이전 빌드 시도와 이번 시도를 합산해 완료 증거로 쓰지 않는다.

최종 상태는 **UnitView 한 파일 교정·정적4경로 errors0·새 기존 두 자동 gate PASS·빌드1회 시도 관측 / 새 실기 OPEN**이다. source phase seam은 유력 원인이지 완전 runtime 인과 증명은 아니며 exact4초 cycle·시각 방향/연속성·전체 fix/visual acceptance는 미입증이다. §17 runtime과 계획/중간 인계는 이력으로 보존한다. 최종 inventory는 문서12개(관련9개+기존 memory3개), source1개(UnitView), Temp 보조20개다. Temp20은4경로별 rsp/dll/ref.dll/pdb/log로 source/asset과 구분한다. 이번 최종 문서 단계는 관련9개만 갱신하고 memory3개 durable lesson은 그대로 둔다. 정합성 검사 실행/최종 수동 검토는 메인 담당이며 이 인계 자체를 검사 PASS로 기록하지 않는다.

**최종 inventory 보강 — 도구 자동 baseline1 추가:** 메인 인계상 `check_docs --update-baseline`을 증가 방향으로만 실행해 `.claude/agent-memory/_baseline.json`을 도구가 자동 갱신했다. game-programmer3824→3830(+6), qa1344→1350(+6), 감소 없음/사유 인자 불필요이며 문서 담당자의 직접 편집이 아니다. 최종 구분은 **source1/docs12/baseline1/Temp20 별도 보조 artefact**다. 최종 정합성 checker는 문서 마무리 뒤 메인 실행 예정이며 이 baseline 갱신을 최종 검사 PASS로 대신하지 않는다.

## 19. 2026-10-04 최신 회귀 — 첫 힐 캐시 초기화 순서 결함 진단 / FAIL·OPEN

사용자는 최신 경기에서 반복 힐이 나오지 않고 제자리 Walk가 나타난 원인을 요청했다. **마지막 §18 교정에서 도입된 첫 진입 guard와 캐시 초기화의 의존 순서 결함**을 진단하며 에셋/클립 교정이나 일반 서버 권위 완화가 아니다. 사용자 요청은 원인 분석이지 수정 승인이 아니므로 코드·에셋·UI·빌드·Git·새 task/TC/QA는 수행하지 않는다. 아래는 메인 raw 재집계·source review 인계에 근거하며 문서 담당자의 runtime probe/독립 재현 결과가 아니다.

### 19.1 최신 로그 범위 — 과거 힐 기록과 분리

| 자료 | 최신 역할/범위 | 메인 확인 결과 |
|---|---|---|
| `Assets/_Project/Docs/_Logs/2026-10-04/13_02_logcat/RuntimeLog_device.txt` | Android Host, 전체4552줄 | BLOOM-HEAL0, Blue Bloom 생산6기: IDs5/9/22/25/40/43 |
| `Assets/_Project/Docs/_Logs/_editor/2026-10-04/RuntimeLog.txt` | Editor Client, 최신12:56~13:01의12:/13: prefix6558줄 | BLOOM-HEAL0. 재생성 Bloom5는 daily22628, init22634 확인. 다른 ID의 Client 재생성 전수 확인은 주장하지 않음 |

양 peer sharedSessionKey는 `2d0898b24994462500f4a64df0f88f9078c820fc934ed98bf6207eaf3dbabc98`다. Editor daily의00:30~00:35 BLOOM-HEAL 기록은 §17의 이전 경기 이력이며 최신 성공 회차로 합치지 않는다. 생산은 확인됐지만 latest 힐 시작/재생/dispatch 진단 자체가0이다. 이 사실과 사용자 증상을 기록하되 HP/HoT 손실 전량·private cache값·정지 유닛 exact binding을 로그0만으로 단정하지 않는다.

### 19.2 source-confirmed 순서 결함과 runtime 입증 한계

메인 source review 기준 `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`의 해당 시점 행 번호다.

- `EnterHealLoopV3` 진입5298의 `HasHealAuthority` 호출은5245의 `_healNetworkUnit != null`/`IsSpawned`/`IsServer` 조건을 검사한다.
- `_healNetworkUnit`의 유일한 대입은72의 lazy `HealNow` getter다. `Initialize`는 `_movementNetworkUnit` 및 local networkUnit을 확보하지만 `_healNetworkUnit`은 초기화하지 않는다.
- fresh caster의 첫 진입은 아직 PlayHealCastAnimation/HealNow에 도달하지 않았으므로 cache가 null인 경우 authority guard에서 먼저 반환한다. 같은 guard를 반복 재시도해도 뒤의 initializer에 도달하지 못한다.
- Update884의 HealNow 호출은 `_healPresentation.Current.Active`일 때만 실행된다. 아직 active가 아닌 첫 시전을 미리 prime하지 않는다. ObserveHealPose1018도 invalid state를 HealNow 전에 거부하므로 이 경로의 우연한 초기화를 기대할 수 없다.
- movement-first3812/reentry4323/stationary5532 진입은 같은 entry guard를 사용한다. source 결함은 특정 이동 진입 한 곳에만 국한되지 않는다.

따라서 **첫 guard가 그 뒤에서만 생성되는 필수 cache를 요구하는 의존 순서 결함은 소스에서 확인됐다.** runtime private cache값을 breakpoint/추가 로그로 관측하지는 않았으므로 최신 모든6기에서 해당 null 상태를 직접 입증했다거나 사용자 정지 인스턴스와 전부 exact 결합했다고 쓰지 않는다. 이 결함은 최신 heal 진단0·반복 힐 부재와 강하게 부합하나 제자리 Walk의 모든 rendered 경로까지 증명한 것은 아니다. 초기화 실패를 새 cooldown이 만료되지 않은 현상/클립 손상으로 바꿔 설명하지 않는다.

### 19.3 기존 자동 검증의 정확한 coverage gap

메인이 확인한 기존 `ValidateBloomHealPresentationBoundary`는 state를 직접 만든 뒤 `ApplyBloomNetworkFixture`/`ApplyCurrentHealCast`를 호출한다. **production 첫 시전의 EnterHealLoopV3→HasHealAuthority→HealNow 순서를 통과하지 않는다.** 따라서 §18 static/두 자동 메뉴 PASS는 그 실행/검증 범위의 이력으로 유효하지만 fresh View의 실제 첫 진입 회귀를 검사한 증거는 아니다. 모든 validation을 무효로 선언하지 않으며, 현재 source 결함과 관련된 **first-entry validation scope gap**만 명시한다. 새 fixture/QA는 실행·작성하지 않는다.

### 19.4 미구현 수정 제안·현재 상태·이번 파일 범위

제안은 **첫 authority guard 전에 실제 NetworkUnit 의존성을 독립적으로 확보/초기화**하여 cache 생성이 HealNow 읽기에 숨겨져 있지 않도록 하는 것이다. IsSpawned/IsServer·identity/lifetime·absolute dispatch deadline/Remaining·stale owner 차단 및 HP/HoT 서버 단일 writer를 그대로 유지해야 한다. guard 삭제/항상 true·Client gameplay/회전 writer 허용은 제안하지 않는다. fresh Initialize 뒤 clock/presentation을 미리 prime하지 않은 실제 첫 힐 진입을 향후 승인된 회귀 증거에 포함해야 하지만, 이는 미실행 예방 기준이며 이번 수정/TC/QA 승인이 아니다.

최신 판정은 **사용자 힐/모션 회귀 FAIL·OPEN / source 초기화 순서 결함 확인 / 수정 미구현**이다. §17의 이전 due/marker 수용 및 §18 구현·정적/자동 PASS·빌드 시도는 별도 이력으로 보존한다. 이번 변경은 기존 Research/Plan의 §19·상단 task status 및 `.claude/mistakes/unit-action-correction.md`의 사건 목차/본문뿐이며 상시7문서·메모리·baseline을 확장하지 않는다. 실패 기록은 규칙 권위 원본이 아니다. 최종 checker는 메인 인계 후 실행하며 이 문서 기록 자체를 검사 PASS로 주장하지 않는다.

## 20. 2026-10-04 첫 힐 authority 의존성 초기화 순서 교정 — 사용자 승인 계획

힐을 처음 시작할 때 필요한 실제 네트워크 컴포넌트를 먼저 확보하고, 그 뒤 기존 서버 권위를 검사하도록 순서만 교정하는 계획이다. 사용자는 §19의 의존 순서 교정과 빌드 **시도**를 승인했다. 코드 담당 Sagan의 현재 단계는 읽기 전용 proposal·메인 GO 대기이며, 이 절은 코드 구현 전에 저장하는 기존 task 계획이다. **승인/계획은 구현 완료·runtime 수용이 아니다.**

### 20.1 근거와 최소 예정 범위

GameSystemRules 인덱스 및 Units 규칙32~37/Sync SeparateHeal을 기준으로 한다. 규칙32의 서버/싱글 힐 별도 경로와 SeparateHeal의 motion-only Client·identity/lifetime/terminal 경계는 완화하지 않는다. §19 source review에서 첫 HasHealAuthority가 뒤의 HealNow에서만 초기화되는 cache를 요구하는 순서 결함은 확인됐지만, 최신 모든 인스턴스의 private cache와 exact 정지 binding은 runtime에서 관측되지 않았다. 이 한계를 새 수정 계획으로 소급 해소하지 않는다.

최소 source 예정 범위는 `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`의 실제 NetworkUnit resolve/capture와 첫 힐 authority 검사 연결이다. 현재 View/객체에 속한 실제 component를 **첫 gate 이전에 독립적으로 확보**하고, HealNow/활성 presentation을 먼저 읽어야만 의존성이 생기는 순환을 제거한다. 구체 helper/대입 지점과 patch는 Sagan proposal→메인 review/GO 후 확정한다. cache가 존재한다는 사실만으로 권위를 허가하지 않고 기존 IsSpawned/IsServer 및 owner identity/lifetime 검사를 유지한다. 싱글과 pure Client 구분, reinit/stale owner 안전성도 유지해야 한다.

### 20.2 보존 계약과 검증·빌드 인계 경계

§16 due guard·단일 CastHeal 및 §18 actual dispatch+configured cooldown absolute deadline/Remaining 양 조건·entry cooldown/lifecycle owner 보호는 불변이다. 공통 ticker·다른 타입·타겟/회전·Frozen/Held·HP/HoT200/3·완료 텍스트/연구 배율·native4초/marker1초·blend0.15초와 모든 원스탯은 변경하지 않는다. guard 삭제/항상 true·authority bypass/Client writer나 에셋 교정은 범위 밖이다. 기존 보존 주석/이력 로직도 삭제하지 않는다.

코드 최종 인계 뒤 새 정적 결과와 기존 Unit Action/Root Pose Cross Audit SelfValidate 두 메뉴 결과를 메인 확인 사실로만 기록한다. 승인된 Build And Run 시도는 메인이 실제 조작한 인계 이후에만 결과를 기록하며 빌드 완료·설치·runtime monitoring/polling을 하지 않는다. 이전 §18 static/자동 PASS/import 관측을 이번 교정 이후 결과로 재사용하지 않는다. 새 TC/QA/validator 수정·runtime fixture 생성은 승인 범위가 아니다. 기존 fixture의 실제 fresh-first-entry traversal 공백은 계속 구분한다.

이번 단계의 실제 문서 변경은 기존 Research/Plan §20 및 상단 task 상태뿐이다. 메모리·실패 기록·상시 참조 문서는 지금 확장하지 않는다. source/최종 검증/메뉴/빌드 인계를 기다려 필요한 최신 상태만 후속 동기화한다. §19 실패 증거와 private cache 미관측·새 runtime OPEN·exact4초 cycle 비보장 및 visual acceptance 부재는 유지하며 최종 checker는 모든 문서 완료 후 메인 담당이다.

### 20.3 코드担当 최종·메인 source review 인계 — 새 UI 결과 대기

Sagan proposal→메인 review/GO→실제 구현→최종 보고와 메인 source review를 근거로 한다. 아래 시각·크기·정적 수치는 문서 담당 재측정이 아니라 해당 인계값이다. 실제 변경 source는 `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` 한 파일이며 최종14:13:08/427,138bytes 이후 그대로다.

`EnsureHealNetworkUnit`은 같은 GameObject의 실제 component를 확보하며 Unity destroyed-null이면 다시 조회한다. Initialize 및 첫 HasHealAuthority 전에 호출하여 lazy HealNow getter를 지나야만 cache가 생기던 순환 의존을 해소했다. 기존 authority guard 자체와 IsSpawned/IsServer·identity/lifetime·deadline/Remaining·stale-owner 보호는 유지했고 이전 검사 순서도 주석으로 보존했다. 클립/에셋·HP/HoT·스탯·공통 ticker/다른 타입 정책 변경은 없다.

첫 진입 Dev 진단은 lifetime별 accepted/rejected bitmask로 제한하며 새 UnitData에서만 reset한다. 필드는 UnitId, Lifetime, CastAllocated=false, Accepted, CacheBefore/After, SameObject, NetworkActive/Server, Spawned, ComponentServer, Enabled, Alive, LocalNow, Frame이다. 실제 cast 할당 전에 검사 결과를 기록하는 것이며 fake cast/start를 만들지 않는다. 이 진단 추가는 private cache가 이미 runtime에서 관측됐다는 뜻이 아니다.

최종 새 정적 컴파일14:14:14~14:14:54: Runtime errors0/warnings20, Editor0/0, Development0/20, Release0/24. 경고 수는 정적 카운트이며 기존 warning 교정 주장이 아니다. 이번 보조 산출물은 기존 Temp 컴파일 경로의 dll/ref.dll/pdb/log×4 **16개 갱신**이고 RSP4개는 변경 없이 재사용했다. §18의20개 생성 이력과 달리 이번20개 변경으로 집계하지 않는다.

메인 native Refresh 인계: Runtime DLL14:15:48, Editor DLL14:15:50, reload 완료 뒤 새 Unit Action 메뉴 클릭까지다. **새 Unit Action 결과·Root SelfValidate 실행/결과·이번 Build And Run은 아직 확인 인계가 없다.** 이전 PASS를 대신 쓰지 않고 추가 UI/build polling·완료 모니터링을 하지 않는다. §19 실패는 교정 전 이력, source correction confirmed/new runtime OPEN이며 fresh production first-entry·반복 힐·stationary Walk 해소·시각 수용 및 exact4초 보장은 아직 미입증이다. 최종 checker는 전체 문서 마무리 뒤 메인 담당이다.

### 20.4 최종 실제 두 기존 메뉴 PASS·빌드 시도 인계 — runtime OPEN

메인의 이번 실제 native 조작/관측 인계를 기록한다(문서 담당자 UI 실행·재측정 아님). 새 Unit Action Self Validation 클릭 후 새 bounded Editor.log에서 overall/Bloom/Production/C2/C3/AUTH-PRESENT PASS를 확인했다. 이어 실제 Self Validate Unit Root Pose Cross Audit 클릭 후 새 UI status와 새 bounded log의 PASS를 확인했다. 이번 source 이후 새 실행이며 이전 §18 PASS나 실경기 Analyze의 재사용이 아니다. 별도 전체 PASS 시각은 전달되지 않아 DLL 생성 시각/다른 anchor로 대체하지 않는다.

이후 File→Build And Run을 **1회 실제 클릭**했고 즉시 `Detect Java Development Kit(JDK)` / `Checking Java Development Kit(JDK)...` 진행창만 관측했다. 여기서 Unity UI 및 빌드 로그 확인을 모두 중단했다. **빌드 완료·설치·runtime 실행은 미확인, 빌드 성공 주장 없음.** §18 Importing 화면은 이전 시도의 별도 이력이다.

최종 source 구현 완료·정적4경로 errors0(상세 §20.3)·새 기존 두 자동 gate PASS·빌드 시도만 확인한 상태다. 새 fresh-first-entry production runtime fixture는 실행하지 않았다. 기존 fixture의 first-entry traversal coverage gap은 유지하며 private cache live probe/모든 instance 인과·반복 힐/제자리 Walk 해소·시각 수용·exact4초 보장도 미입증이다. **새 runtime 재시험/전체 수용 OPEN**, §19 사용자 실패는 교정 전 이력으로 보존한다. 클립/스탯/HP/HoT·authority·postdispatch3초·기존 deadline/Remaining·owner 안전성 불변이다.

이번 §20 문서 inventory: 기존 task2, 실패 기록1, PROJECT_STATUS/ROADMAP/WORK_HISTORY/StatsReference/UnitCombatAssetMatrix5, 필수 기존 memory3(공용·game-programmer linked combat-fixes·qa linked patterns의 durable lesson만) = **문서11개**. source1은 코드 담당 변경이며 문서 담당 변경이 아니다. Temp16개 갱신(dll/ref.dll/pdb/log×4), RSP4개 불변 재사용; 이전 §18의20개 생성/baseline 도구 갱신과 별도다. 신규 파일·TC/QA/validator/정책 변경 없음, baseline 직접 편집 없음. 최종 check_docs는 모든 문서 완료 뒤 메인이 실행하며 아직 결과를 주장하지 않는다.

## 21. 2026-10-04 두 신규 실기 로그 분석 — 첫 진입·반복 전달 관측 수용

사용자가 두 번 시험한 뒤 현재는 로그만으로 확인하도록 요청했다. 이 절은 **메인의 확정 raw 집계·선택 원시 사례 검토 인계**를 기록하며 문서 담당자가 로그를 재분석한 결과가 아니다. §19 실패 prefix와 §20 자동 검증/빌드 시도는 각각 이력으로 보존하고 새 실기와 혼합하지 않는다. Timing/HoT 전문 담당 최종 인계는 아직 대기다. source/에셋/규칙/수치 변경·새 task/TC/QA/영상 분석·UI/build 실행은 없다.

### 21.1 파일·run identity와 생산/첫 진입 분모

Device는 `Assets/_Project/Docs/_Logs/2026-10-04/15_29_logcat/RuntimeLog_device.txt`, Editor는 `Assets/_Project/Docs/_Logs/_editor/2026-10-04/RuntimeLog.txt`다. device의 과거12:56~13:01 실패 prefix를 제외한다. 신규 두 run은 **Editor Host / Android Client**이며 shared key `b562e5978afb857736cdddc13845173f2dce3ec4bf2d48805bdfac76370765e6`를 재사용하므로 시간/runId로 분리한다.

| 구분 | 첫 run | 두 번째 run |
|---|---|---|
| 신규 시간 범위 | 15:23~15:25:17 | 15:25:18~15:28:09 |
| Host ROOT runId | 7446784415ad431e8cdf8839988320fa | 839e2b5b8b644795a70b88554c421256 |
| Client ROOT runId | 9c7be760796f45f4b99528d72e336a4c | 927365f000fb41a59be3650cf6db3d14 |
| Bloom 생산·Team | 4기:6/21/37/50, 모두Red | 9기:74/77/86/89/97/101/109/112/123, 모두Red |
| Host 생산 행 | 26657/26922/27369/27707 | 28163/28174/28557/28766/29424/29759/30626/30928/31489 |
| FirstEntry accepted/생산 | 4/4 | 9/9 |

13기 전수에 FirstEntry Accepted=True, CacheBefore/After=True, SameObject=True, Spawned=True, Server=True와 Start가 있으며 거부0이다. 이 양성 진입·반복 전달 증거는 §19의 BLOOM-HEAL0과 구별된다. 교정 전 private cache 미관측을 소급 breakpoint 증명으로 바꾸지 않는다.

### 21.2 exact cast delivery·marker 없는 종료와 최신 revision 수신

메인은 lifetime/unit/sequence exact key로 대조했다. Host Start=Host Play=Client Play가 첫17/두 번째66(총83), missing/extra/duplicate0이다. Play의 StartedAt/target/revision/active값 conflict0. Host/Client Marker도 첫16/두 번째64 exact key 일치, missing/extra/duplicate0이다.

Marker 없는3회는 다음 수명 종료 anchor로 분리하며 marker/완료 힐 누락으로 단정하지 않는다.

| cast key | Host Play→End | Client Play→종료 |
|---|---|---|
| 첫 U6 seq7 lifetime7 | 27205→27226 despawn | device35577→35819 death |
| 두 번째 U97 seq5 lifetime98 | 30394→30415 despawn | device120306→120680 death |
| 두 번째 U112 seq2 lifetime113 | 31134→31180 despawn | device132094→132611 death |

Publish/Receive는 첫23/20, 두 번째74/71이다. 차이6건은 **Active=False인 중간 inactive End revision**이며 active 시작 누락이 아니다. 같은 Host frame에서 다음 inactive 새 revision 발행과 Client 최종 inactive 수신을 원시 사례로 확인했다: 첫 U6 seq5 rev6미수신→rev7수신, U37 seq1 rev2→rev3, U37 seq2 rev5→rev6; 두 번째 U86 seq5 rev6→rev7, U101 seq5 rev6→rev7, U109 seq2 rev3→rev4. Client receive anchors: 첫32467/42936/45097, 두 번째106755/128958/130679 및 이후131205/131324.

**각 cast 최신 published revision17/66 전부 Client exact-key received, latestmissing0**이다. 현재 latest terminal 누락 근거는 없다. 동일 frame 갱신 coalescing과 강하게 정합적이지만 packet trace가 없으므로 절대적인 네트워크 원인 확정은 아니다. priority revision에서 LifecycleEndReason이0으로 덮어써지는 것은 별도 진단 정보 보존 한계이며 heal 누락/재활성화 증거가 아니다.

### 21.3 callback 각도·self drift 및 MOVE/ROOT 경계

| marker callback pose | 첫 Host/Client | 두 번째 Host/Client |
|---|---|---|
| non-self defined 표본 | 9/9 | 43/42 |
| signed AngleError의 absolute max | 0.193°/1.787° | 0.284°/0.055° |
| self undefined | 4/4 | 14/14 |
| dead/missing target lookup undefined | 3/3 | 7/8 |

self 또는 dead/missing target로 AngleErrorUndefined인 표본은 angle failure로 세지 않는다. 위 값은 **callback sample이지 rendered frame/model/pixel facing PASS가 아니다.** self marker Root Drift는 Host18/18 모두0°, Client18개 최대19.067°(U101 seq6 lifetime102, device130059,15:27:30.418), 나머지0~0.002°다. Client Root writer가 없는 경계에서 server Root 보간 잔여 가능성과 부합하지만 callback baseline 기준 변화만 측정한 것이므로 힐이 Client를 자체 회전시켰다는 proof는 아니다. visual 판단은 OPEN이다.

Host MOVE terminal-core 첫 행28011/frames95605, 두 번째 행31764/frames83144: gate/writer/adapter/handoff/stationaryWalkViolations/dropped0, spatialFatal/repeatRecoverable0이다. 양 peer local ROOT END4개 PASS. 이들은 관측된 이동/pose 경계이지 formal CrossAudit나 heal visual/제자리 Walk 픽셀 해소의 증거가 아니다.

### 21.4 Timing/HoT 최종 인계 대기와 현재 판정

| 항목 | 이번 최종 결과 |
|---|---|
| 준비 due·실제 dispatch/marker/첫 HP 시간 | 미확인 — 전문 담당 최종 인계 대기 |
| postdispatch3초·deadline/Remaining 및 반복 cycle | 미확인 — 전문 담당 최종 인계 대기 |
| HoT outcome/HP/text exact 검증 | 미확인 — 전문 담당 최종 인계 대기 |

**현재 판정: first-entry+반복 delivery 회귀의 focused 로그 관측 수용(13/13 accepted,83 exact plays). Timing 최종 대기·visual/VFX 미확인·전체 BloomFairy OPEN.** 사용자는 VFX 제작 후 육안 확인을 선호하지만 VFX 제작 승인은 아니다. callback/ROOT/MOVE·전달 equality를 HP timing/시각 연속성 수용으로 확대하지 않는다. 이후 Timing/HoT 최종 인계를 이 절에 후속 추가한다.

이번 최소 갱신은 기존 Research/Plan2+상태3+Stats/Matrix2+실패 기록1=문서8개다. 신규 durable lesson이 없고 로그 자동 갱신 선호도 이미 저장돼 있으므로 세션 수치/WIP를 memory에 복제하지 않고 기존 memory/baseline을 보존한다. 최종 checker는 모든 인계 반영 뒤 메인이 실행한다.

### 21.5 Timing/HoT 최종 인계 — 관측 수용과 별도 효과 duration 잔여

**역할 coverage 한계(메인 최종 검토):** §19 실패는 Android Host/Editor Client, 이번 두 run 수용은 Editor Host/Android Client 구성만이다. first-entry source는 공통이어도 교정 후 Android Host 역할교대 first-entry 실기 coverage는 미확인이며, 이번 두 경기 재발0을 플랫폼/모든 역할의 보편 해결 proof로 확대하지 않는다. 새 역할교대 시험 요청·실행은 하지 않는다.

전문 담당의 read-only source/log 집계 후 **메인의 raw anchor·같은 clock·source 검토로 확정한 인계**다. 문서 담당 독립 재집계가 아니다. 이하 원시 행은 Editor daily 기준이다. 첫/두 번째 attack observer runId는 `b21b50915faf4953a03fe1e2c1258183` / `83a8879b1f914b13a65ef3ca92d11498`이며 §21.1 ROOT runId와 observer를 혼동하지 않는다. key는 Unit/Lifetime/seq다. 아래 결과가 §21.4의 당시 Timing 대기를 후속 해소하되 시각/전체 수용으로 확대하지 않는다.

| Timing/서버 효과 항목 | 첫 run A | 두 번째 run B |
|---|---|---|
| Start / 실제 dispatch | 17/16 | 66/64 |
| Applied / HoTResult | 13/13 | 57/57 |
| firstpositive | 13 | 55 |
| due 이전 dispatch | 0/16 | 0/64 |
| exact accepted Host marker 이전 firstpositive | 0/13 | 0/55 |
| dispatch→다음 Start <3초 | 0/13 | 0/57 |
| dispatch→다음 Start min~max | 3.001279~3.937269초 | 3.000644~5.935103초 |
| Start→Start 전체 min~max (n) | 4.002969~4.940824초 (13) | 4.002124~6.935440초 (57) |
| 사이 End 없는 Start→Start min~max (n) | 4.002969~4.090177초 (10) | 4.002124~4.121491초 (54) |
| 첫 positive HP timing: 인계의 dispatch/marker→firstpositive 범위 | 18.215~117.777ms | 14.304~114.000ms |

큰 간격은 no-target 종료 뒤 재시작을 포함하며 그 자체 정지 FAIL이 아니다. 첫 positive HP timing 범위는 메인이 전달한 dispatch/marker 기준 표기로 기록하며 두 기준 event가 항상 같은 시각이라는 추가 주장은 하지 않는다. 확정 판정은 **due 이전 dispatch0 및 비교 가능한 exact Host marker 이전 positive HP0**이다. cooldown 최솟값 raw: A6/7/6→7 dispatch27158→completion27191→nextStart27192, B109/110/3→4 31236→31301→31302. `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`의5471 due guard/5487 CastHeal,5389/5514 absolute deadline **AND** Remaining 경계와 대조됐다. movement-replaced 재진입은 별도 직접 coverage가 없으며 여기서 수용하지 않는다(no-target End 뒤 재진입 관측과 구별).

firstpositive 없는 dispatch12건은 다음과 같이 분리된다.

- CastOutcome TargetUnavailable: A3+B7=10.
- Applied 뒤 첫 tick 전 대상 사망: B109/110/2 target98, Applied30902→death30909→result30910, Elapsed0/ActualHealed0.
- 같은 frame 재부여로 대체: B112/113/1 target101, frame61749의31020~31024 사이101/102/6 self heal로 교체,31023 Superseded0.

이들은 서버 쿨다운/HP·대상 사망 규칙을 유지한 outcome이며 unknown HP loss로 분류하지 않는다. HoTResult의 outcome 분포는 A FullHP2/Expired4/TargetUnavailable6/Superseded1, B13/9/5/30이다. 서버 실제 회복 합은 A1668+B5579=7247로 집계됐지만 Client HP/text 전수 검증은 아니다.

**별도 OPEN — 공통 HoT의 실제 등록→종료 duration:** Expired13개는 모두 로그의 Elapsed3/Duration3/ActualHealed200이나 같은 Host local clock의 실제 간격은 다음과 같다. 누적 Elapsed 필드만으로 등록 후 정확히3초가 보장됐다고 하지 않는다.

| Expired 실제 구간 | key·raw anchors | 같은 Host clock 등록→종료 |
|---|---|---|
| A 최소 | 6/7/4, firstpositive26935→result26943 | Registered1003.3570641439909→Local1006.3331061437075 =2.976042초 |
| A 최대 | 6/7/2,26814→26841 | 3.081698초 |
| B 최소 | 101/102/8, firstpositive31339→result31382 | Registered1201.9483101403061→Local1204.8783515447844 =2.930041초 |
| B 최대 | 74/75/4,28556→28622 | 3.081361초 |

메인 source/clock 확인: `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`2349/2380의 실제 register는 Time.timeAsDouble,2429는 Elapsed+dt이며 `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`636의 global elapsed를 TickTimedEffects에 전달한다. 같은 clock 비교이며 네트워크 lag 원인 주장은 없다. 등록 전 구간을 첫 global tick의 dt가 credit할 수 있는 기존 공통 누적 경로와 source상 정합적이지만 각 instance의 exact ticker 시작 private state가 로그에 없어 전수 runtime causal proof는 아니다. **최대 약70ms 짧은 실제 효과 duration 잔여의 후속 분석/교정은 OPEN·제안만, 이번 코드 수정·규칙 변경 승인 없음.** postdispatch cooldown deadline과 공통 효과 duration을 같은 문제/완료 판정으로 합치지 않는다.

**최종: first-entry+due+postdispatch cooldown focused 로그 관측 수용. 전체 Bloom OPEN.** 시각/VFX, Client HP/text 전수, movement-replaced 재진입, 공통 HoT 실제3초 duration 잔여는 별도 미완이다. §20 빌드 시도 이후 이번 작업은 saved-log 분석·기존8문서 갱신만이며 재빌드/새 자동 검증/영상·UI/TC/QA 실행 없음. 원수치·클립·authority·3초 정책·기존 history와 memory를 보존하고 최종 checker는 메인 담당이다.

## 22. 등록 기준 공통 연속 Heal duration 교정 조사 — PLAN·미구현

### 22.1 확인된 메커니즘과 증명 경계

§21.5의 같은 Host scaled local clock에서 Expired 등록→종료 최소는 A2.976042초/B2.930041초다. 기존 source는 등록 시각을 Time.timeAsDouble로 기록하지만 진행은 공통 ticker의 dt를 Elapsed에 누적한다. 따라서 등록 이전 구간이 포함된 첫 dt를 새 효과에 credit할 수 있는 의존 관계는 source상 확인됐다. 각 효과의 당시 private ticker phase는 로그에 없으므로 모든 instance의 정확한 인과 경로를 runtime으로 증명했다고 하지 않는다. 네트워크 지연 문제로 분류하지 않으며 §21 원수치·outcome·raw anchors를 수정하지 않는다.

### 22.2 메인 검토 programmer proposal

계획 변경 파일은 `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` 한 개다. 공통 **continuous Heal**의 production 상태에 double RegisteredAt/Deadline을 두고, add/refresh마다 동일한 scaled Time.timeAsDouble 표본으로 설정한다. Tick당 now는 한 번 표본화하고 등록 이후 경과(now−RegisteredAt)로 진행을 계산하여 등록 전 dt credit을 없앤다. 정상 만료 Expired는 float Elapsed 반올림이 아니라 double now>=Deadline으로만 판정한다.

invalid/nonfinite/regressed clock에서는 진행을 보류하고 dt fallback을 넣지 않는다. target unavailable/fullHP 처리는 유지한다. 계산은 positive Duration 안으로 clamp하되 기존 duration<=0 즉시 처리 정책은 보존한다. 이 경계의 정확한 구현 처리는 후속 implementation review 대상이며 아직 구현 완료가 아니다.

기존 누적 RoundToInt 목표량 차분과 만료 시 남은 회복량 적용을 유지한다. 반올림 때문에 deadline 직전 반 HP 미만 구간에서 누적 목표량이 이미200이 될 수 있다. 따라서 **마지막 HP가 반드시 등록3초 이후에만 적용된다는 약속은 하지 않는다.** FullHP 조기 종료를 제외한 Expired/정상 완료 텍스트는 deadline 전에 완료시키지 않는 계약이다. 새 등록/refresh는 누적량·ActualHealed 등 해당 회차 totals를 모두 reset하고 중첩하지 않는다. 대체되는 이전 source는 사용자 runtime 수용 전까지 주석으로 보존한다.

### 22.3 공통 영향 범위·불변 계약

Bloom 타입명 예외가 아니라 Bloom과 skill HoT가 사용하는 모든 공통 continuous Heal에 적용할 계획이다. skill HoT도 등록 기준 duration 계산의 영향을 받음을 명시한다. continuous Damage의 dt 경로, discrete DoT, global ticker/호출자, 자연회복·MistShrine·Torrent는 변경하지 않는다.

Units 규칙34/37의 HoT 회복·차분/완료 텍스트 메커니즘, 규칙32 서버 권위·분리 힐 경로와 규칙36 발동 후 쿨다운, Synchronization SeparateHeal/network authority를 유지한다. 검토된 제안에서 규칙 변경/충돌은 제안되지 않았다. HoT200/3초·준비1초·post3초·clip4초·marker1초·blend0.15초 및 에셋/Stats는 그대로다.

### 22.4 승인·후속 검증 경계

이번은 Research/Plan 두 파일의 문서화만이다. 구체 Plan을 사용자에게 공유하고 승인받은 뒤에만 구현 단계로 넘어간다. 새 static 결과·TC/QA/validator·메뉴·빌드·실기 증거는 없다. 기존 gate PASS를 이 새 duration 교정의 증거로 재사용하지 않는다.

승인 후 검증 계획은 기존 static 경로와 새 runtime의 동일 scaled clock registered→Expired 비교다. 생존·비만피·미대체 target에서 첫 서버 tick now>=Deadline에 만료하고 등록→Expired>=3 scaled초인지 확인한다. FullHP·대상 사망·재부여 종료는 정상 만료와 분리한다. 사용자 visual/VFX 검증은 계속 유보이며 전체 Bloom OPEN이다.

### 22.5 사용자 승인 후 실제 구현·정적 인계 — 메뉴 대기·runtime OPEN

사용자가 구체 §22 계획에 「진행해」로 승인한 뒤 programmer가 구현하고 메인이 최종 source를 검토했다. 위 PLAN·승인 대기 문구는 당시 이력이다. 실제 변경은 `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` 한 파일(최종2339 이후 관련 경로)이다. 공통 continuous Heal의 production RegisteredAt/Deadline/LastClock을 새 등록·refresh 시 설정하고 Tick당 continuous Heal용 now를 한 번 표본화한다. 등록 이후 scaled 경과로 진행하며 double deadline으로 Expired를 판정한다. invalid/nonfinite 또는 clock regression에서는 diff0으로 진행을 보류하고 dt fallback 없이 기존 target/fullHP 처리를 유지한다. duration<=0은 기존 즉시 처리 정책을 보존한다.

누적 RoundToInt 차분·totals reset/비중첩·최종 remaining amount·서버 HP/완료 텍스트 계약은 유지했다. deadline 직전 반올림으로 총량200에 먼저 도달할 수 있으므로 마지막 HP가 반드시3초 이후라는 보장은 없다. 대체된 이전 source는 runtime 수용 전 주석 보존 계약을 따른다. 기존 keyed firstpositive/Expired 진단에 DeadlineLocal/RegisteredLocal/Since를 추가했다. 새 로그 필드 존재는 runtime 표본 수집/통과 증거가 아니다.

최종 정적 인계(errors/warnings): Runtime0/20, Editor0/0, Development0/20, Release0/24. Editor 경로의 fresh runtime.ref.dll 사용을 확인했다. 보조 산출물은 dll/ref.dll/pdb/log×4의 **Temp16 갱신**이며 RSP4는 변경 없이 재사용했다. assets·UnitView·global ticker/호출자·continuous Damage/discrete DoT는 변경하지 않았다. 공통 continuous Heal 적용 범위에는 Bloom 및 skill HoT가 포함되며 자연회복·MistShrine·Torrent, HoT200/3·prep1·post3·clip4·marker1·blend0.15·SeparateHeal/서버 권위는 불변이다.

메인 native 인계 시점에는 실제 Assets Refresh 후 Unity import 진행 중이며 native DLL은 아직 이전14:15본이다. **이번 두 기존 Unity 메뉴 결과는 대기, 이번 빌드 실행 없음.** 새 메뉴 PASS/새 native DLL 적용 완료를 추정하지 않는다. §21 실기는 새 duration 구현 전 baseline이고 이전 조기 종료 수치/인과 한계는 보존한다. 새 same-clock 등록→Expired>=3 scaled초 runtime 확인·skill HoT 영향·시각/VFX 및 기존 미완은 OPEN, 전체 Bloom OPEN이다. 이번 동기화는 task2+현재 상태5문서뿐이다. 기존 event-local 교훈과 중복되는 session WIP는 memory에 넣지 않고 baseline도 변경하지 않았다. checker는 메인 담당이다.

### 22.6 최종 실제 native 메뉴 인계 — 기존 gate PASS·runtime OPEN

메인이 새 time branch와 guard 전체를 검토한 final source 시각은16:24:35다. 실제 Assets Refresh 뒤 native Assembly-CSharp16:28:31/Editor16:28:32로 갱신됐고 Google.VersionHandler reload busy가 해소됐다. §22.5의 import 진행/native 이전본·메뉴 대기는 그 시점 이력이며 이 실제 후속 인계로 대체한다.

메인의 실제 Unit Action Self Validation 클릭(menu90) 후 offset1215806070 이후 새 bounded Editor.log에서 overall/Bloom/Production/C2/C3/AuthPresent PASS를 확인했다. 이어 실제 CombatDiagnostics Self Validate Root Pose 클릭(menu114) 후 새 UI status `UAS-ROOT-CROSS-AUDIT self-validation PASS`를 확인했다. 이는 새 native 반영 뒤 **기존 두 SelfValidate 메뉴의 새 실행 PASS**이며, 새로운 production HoT fixture·Analyze·게임 runtime 확인은 아니다.

메인 최종 후속으로 같은 offset 이후 bounded Editor.log에서도 Root PASS를 확인했다(UI와 새 log 양쪽 확인). 전체/힐 및 Root 결과는 중복 원문을 나열하지 않고 위 검증 범위로만 기록한다.

source 한 파일·static4 errors0와 Temp16 갱신/RSP4 재사용은 §22.5 그대로다. 이번 빌드 실행 없음, 신규 TC/QA/validator 없음. §21 조기 종료 baseline은 보존하며 새 same-clock 등록→Expired duration 수용·skill HoT runtime 영향·사용자 visual/VFX 및 기존 coverage 미완은 OPEN이다. 정책/수치/authority·다른 경로 불변, 전체 Bloom OPEN. 최종 수정 문서는 같은7개(task2+현재 상태5)이며 memory/baseline 변경·checker 실행 없음; checker는 메인 담당이다.


#### 최종 전체 inventory — source1·문서8·Temp16 갱신 / RSP4 재사용

메인 후속 독립 static log 확인도 Runtime0/20·Editor0/0·Development0/20·Release0/24와 일치했다. source는16:24:35 이후 변경되지 않았다. 아래 source는 programmer 변경, 문서는 이번 동기화/실수 기록이며 Temp는 정적 검증 보조 산출물이다.

- source: [UnitCombatUseCase.cs](D:/Projects/Hexiege/Hexiege/Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs).
- 문서: [Research.md](D:/Projects/Hexiege/Hexiege/Assets/_Project/Docs/_Tasks/2026-10-03/03_28_bloomfairy-heal-timing-correction/Research.md).
- 문서: [Plan.md](D:/Projects/Hexiege/Hexiege/Assets/_Project/Docs/_Tasks/2026-10-03/03_28_bloomfairy-heal-timing-correction/Plan.md).
- 문서: [PROJECT_STATUS.md](D:/Projects/Hexiege/Hexiege/Assets/_Project/Docs/PROJECT_STATUS.md).
- 문서: [ROADMAP.md](D:/Projects/Hexiege/Hexiege/Assets/_Project/Docs/ROADMAP.md).
- 문서: [WORK_HISTORY.md](D:/Projects/Hexiege/Hexiege/Assets/_Project/Docs/WORK_HISTORY.md).
- 문서: [StatsReference.md](D:/Projects/Hexiege/Hexiege/Assets/_Project/Docs/StatsReference.md).
- 문서: [UnitCombatAssetMatrix.md](D:/Projects/Hexiege/Hexiege/Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md).
- 문서: [mistakes.md](D:/Projects/Hexiege/Hexiege/.claude/mistakes.md).

| 정적 경로 | 갱신 산출물4개 | 변경 없이 재사용한 입력 |
|---|---|---|
| runtime | [runtime.dll](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/runtime.dll) · [runtime.ref.dll](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/runtime.ref.dll) · [runtime.pdb](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/runtime.pdb) · [runtime.log](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/runtime.log) | [runtime.rsp](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/runtime.rsp) |
| editor | [editor.dll](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/editor.dll) · [editor.ref.dll](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/editor.ref.dll) · [editor.pdb](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/editor.pdb) · [editor.log](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/editor.log) | [editor.rsp](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/editor.rsp) |
| development | [development.dll](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/development.dll) · [development.ref.dll](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/development.ref.dll) · [development.pdb](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/development.pdb) · [development.log](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/development.log) | [development.rsp](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/development.rsp) |
| release | [release.dll](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/release.dll) · [release.ref.dll](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/release.ref.dll) · [release.pdb](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/release.pdb) · [release.log](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/release.log) | [release.rsp](D:/Projects/Hexiege/Hexiege/Temp/bloom-cooldown-compile/release.rsp) |

Temp16은 이번 갱신이며 RSP4를 변경20개로 합산하지 않는다. memory/baseline·정책/수치/에셋 변경 없음, 추가 source·빌드·TC/QA/validator 없음. 최종 checker는 메인이 실행한다.

## 23. 2026-10-04 18_16 역할교대 두 실기 — 등록 기준 duration focused 관측 수용

### 23.1 인계 근거·경기 분리·key

programmer의 읽기 전용 집계와 메인의 raw count/source branch 검토가 일치한 최종 인계를 기록한다. 본 문서 작업은 별도 raw recount나 신규 시험 실행이 아니다. 파일은 [device18_16](D:/Projects/Hexiege/Hexiege/Assets/_Project/Docs/_Logs/2026-10-04/18_16_logcat/RuntimeLog_device.txt) 및 [Editor daily](D:/Projects/Hexiege/Hexiege/Assets/_Project/Docs/_Logs/_editor/2026-10-04/RuntimeLog.txt)다. 아래 D/E anchors는 각각 이 device/Editor raw 행이다.

- A:18:07~18:10:07, Android Host/Editor Client, session 인계 축약표기 `476a…cf4d`.
- B:18:11~18:13:48, Editor Host/Android Client, session 인계 축약표기 `4300…0f54`.

과거15시 prefix와 빈 재시작 observer tail은 신규 gameplay 집계에서 제외한다. exact 회차 key는 **Lifetime/UnitId/sequence** 순서(예:79/78/4)로 기록한다. §21의 Unit/Lifetime/seq 표기와 순서를 혼동하지 않는다. §19/§21의 이전 역할 한계는 당시 이력이며 이번 관측 수용은 두 역할 구성을 포함한다. 모든 플랫폼/미시험 상황에 대한 보편 해결 증명은 아니다.

### 23.2 first-entry·전달·서버 outcome

| 항목 | A | B |
|---|---|---|
| FirstEntry accepted/생산 분모 |7/7 |7/7 |
| Host Start/Dispatch/Outcome (각각) |55/55/55 |63/63/63 |
| 양 peer Play/Marker exact key·target·StartedAt |55/55 일치 |63/63 일치 |
| 위 비교 차이/중복 |0/0 |0/0 |
| Cast Applied / unavailable / FullHP |51/3/1 |58/5/0 |
| firstpositive |43 |45 |
| HoTResult Expired / Superseded / FullHP / Unavailable |13/29/6/3 |14/36/0/7 |

first-entry 및 반복 전달은 두 역할에서 focused 관측 수용이다. exact Play/Marker 일치가 pixel/model visual 또는 Client HP/text 전수 일치를 증명하지 않는다. 사용자의 최신 요청은 로그 분석만이며 새 육안/VFX 수용은 없다.

### 23.3 등록 기준 duration·due·첫 HP·postdispatch cooldown

| 경계 | A | B |
|---|---|---|
| Expired 표본 |13 |14 |
| 실제 등록→Expired |3.016200~3.083146초 |3.001368~3.107432초 |
| 최소/최대 raw anchors |D153486 / D159454 |E40668 / E41160 |
| dispatch−due |0.000002~0.033637초 |0.000074~0.093995초 |
| firstpositive 등록 이후 간격 |0.033305~0.116592초 |0.018420~0.112276초 |
| dispatch→다음 Start (분모) |3.016056~5.915462초 (48쌍) |3.000006~5.723154초 (56쌍) |

Expired27/27은 Elapsed3/Duration3/ActualHealed200이며 같은 Host clock에서 **now>=Deadline 전량 충족·early0/27**이다. §22 등록 기준 duration gate는 이번 관측 표본에서 수용한다. §21의 최소2.976042/2.930041초 조기 종료는 교정 전 baseline으로 보존하고 삭제하거나 새 경기 실패로 합산하지 않는다. server tick 지연을 포함한 실제 간격이며 정확히3.000초 만료를 보장한 결과는 아니다.

due 이전 dispatch0/118, registered 또는 exact Host marker 이전 firstpositive 위반0/88이다. firstpositive88건의 registration은 dispatch now와 일치한다. dispatch→다음 Start3초 미만0/104로 postdispatch cooldown도 focused 관측 수용이다. target 종료/재시작 등을 포함한 큰 간격이 있으므로 정확한4초 cycle을 보장하지 않는다. 등록 기준 정상 만료 수용과 반올림된 총량/마지막 HP의 시각은 별개이며 §22의 RoundToInt 경계 설명은 유지한다.

firstpositive가 없는 Applied는 A8건 모두 Superseded, B13건은 Superseded12+Unavailable1(key66/65/3,E39872)로 분리된다. B Applied key116/115/1의 HoTResult 부재는 E43448 등록→E43457 firstHP→E43462 GameEnd(등록 후 약1.487627초, deadline 전)→E43489 game-end lifecycle로 확인됐다. 새 조기 expiry 결함으로 분류하지 않는다.

### 23.4 실제 gameplay와 별도 startup/rebind 경고

live Host MOVE의 failures/dropped는 양 경기0: A37290frames(D175142), B112833frames(E43588). 양 peer local ROOT END4개 PASS는 formal CrossAudit Analyze나 heal visual PASS가 아니다. D176067의 frames0 empty MOVE FAIL은 별도 빈 observer run으로 보존하며 live gameplay 실패로 합산하지도 기록을 지우지도 않는다.

B 시작/rebind의 D176908~D176922(18:11:11)에서 wave presentation synced-server now3.483881이 previous3.798735보다 작은 clock regression으로 contract failure2, hpAuthorityUnchanged=true가 기록됐다. 이는 **B 경기 startup 중·힐 이전**이며 “경기 시작 전”이라고 표현하지 않는다. 이 warning과 HoT의 scaled registration clock 사이 인과 증거는 없고 VFX가 이미 해결됐다는 증거도 아니다. 별도 관찰/미해결 경계로 유지하며 duration 수용과 섞지 않는다.

메인의 추가 bounded WARN/ERROR inventory(신규18:07~18:13 범위)는 Editor163/device145줄이며 대부분 deferred UnitView spawn initialization 진단이다. device의 ERROR1은 위 empty MOVE이고 wave warning2도 별도 존재한다. 이 집계는 실제 exception 유무의 최종 결론이 아니며 **전체 로그가 error-free라고 하지 않는다.** 메인은 B 결과 부재의 GameEnd raw와 production double deadline guard도 독립 검토했다. 현재 상태 문서에는 이 상세 warning 건수를 반복하지 않는다.

### 23.5 최종 수용 범위·이번 작업 inventory

**두 역할의 first-entry/due/postdispatch cooldown/등록 기준 duration focused 관측 수용. 전체 Bloom OPEN.** 시각/VFX·Client HP/text 전수·skill HoT·invalid-clock 처리·movement-replaced coverage는 이번 시험으로 수용하지 않는다. 특히 Bloom 표본이 공통 skill HoT 또는 invalid/regressed-clock hold 경로의 실기 검증을 대신하지 않는다.

이번은 saved-log 분석 인계를 기존 task2+현재 상태5문서(총7)에 반영한 것뿐이다. source/에셋/수치/규칙/authority 변경, 코드 주석 자동 cleanup, 신규 Task/TC/QA/validator·UI/빌드/Git/checker 실행은 없다. §21/§22 실패 baseline과 구현·정적/메뉴 inventory는 당시 이력으로 보존한다. 신규 durable lesson 없이 memory WIP를 복제하지 않고 baseline도 변경하지 않았다. 최종 checker는 메인 담당이다.
