이번 SOLO 공통 이동 복귀 교정은 **사용자 2회 싱글 테스트 육안 수용과 runtime 증거에 한정해 focused PASS/CLOSED**다. 기존 controlled 검증·소스 보존과 별도로 아래 두 경기 분석을 반영했다. RETURN 미종결14개는 후속 재교전·사망·종료 문맥으로 분류하며 자동 이동 실패로 세지 않는다. native 메뉴 실행 PASS는 이 로그로 입증할 수 없고 전체25종·멀티 동등성·global PASS도 아니다. 랠리·광산은 사용자 명시로 이번 범위에서 제외한다.

# Research — 병합 후 유닛 회귀 감사

<a id="main-merge-publication"></a>

## 사용자 승인 Git 반영 — Main 진행 중 / 결과 영수증 대기

사용자가 기존 Task에 병합 결과를 기록하고 현재20변경을 커밋한 뒤 최신 main fetch→main 전환·merge→원격 main push를 진행하도록 명시 승인했다.20변경은 승인 인계 시점의 전달값이며 문서 담당이 Git으로 재집계하거나 고정 staging manifest로 확정한 수가 아니다. 원시 테스트 로그는 제외한다. Main이 Git 조작·충돌 조율을 담당하며 문서 담당은 Git·코드 수정·새 Task 생성을 하지 않는다.

현재 Main의 fetch 성공·읽기 확인 결과를 수령했으며 merge는 아직 실행하지 않았다. commit/전환/merge/push 완료 영수증은 대기다. 최종 commit·병합 상태·원격 반영·로그 제외와 검증 범위는 Main 실제 결과 수령 후 이 절에 기록한다. 게시·병합 성공을 추가 native 실행·새 gameplay 검증·전25종/멀티/global PASS로 해석하지 않는다. 기존 SOLO 공통 복귀 focused PASS/CLOSED는 유지한다.

### Main 병합 전 읽기·fetch 인계 — merge 미실행

아래는 Main 전달값이며 문서 담당의 Git 재확인 결과가 아니다. feature HEAD=`532e2033`, local main=`a36ff2f7`, origin/main=`88f60e38`(2026-10-13T22:52:17+09)이다. fetch는 성공했고 원격 ref 변경은 없었다. 공통 `a36ff2f7`에서 feature 보존 commit1개와 main VFX commit1개가 각각 갈라졌다.

Main 확인상 main 측 C# 변경은 없고 VFX png/mat 추가, Bloom/Mushroom 관련3 prefab 변경, Docs/log meta 정리다. 같은 경로에 양쪽이 추가한 신규 Docs meta는 add/add 충돌이 예상되지만 아직 실제 충돌 발생·해결을 뜻하지 않는다. 현재20 dirty files는 모두 명시적 파일 staging·commit 예정이며 원시 로그는 미추적 상태로 제외하고 그 meta도 이번에 추가하지 않는다. 이 수는 Main 확인 당시의 전달값이다.

문서 반영 완료 뒤 Main은 명시적 파일 stage·commit→main을 origin/main으로 fast-forward→feature merge를 진행한다. 문서 담당은 Git·staging·충돌 해결을 실행하지 않으며 최종 적용 파일·보호·원격 결과 영수증을 기다린다.

<a id="solo-return-repath-open"></a>

## 2026-10-15 대상 소실 후 공통 이동 복귀 — SOLO focused PASS/CLOSED

최초 사용자는 대상 소실 뒤 Rabbit·RhinoBreaker·LionKnight 등의 정지를 보고했다. 아래 수정 전 분석과 구현 영수증은 당시 근거로 보존한다. 이후 사용자 2회 싱글 테스트·Main 최종 분석으로 이번 복귀 focused 범위를 수용했다. 과거 타격·UI 성공만으로 복귀를 통과시킨 것이 아니다.

<a id="solo-return-two-match-acceptance"></a>

### 2026-10-15 새 싱글 2경기 최종 분석·사용자 수용

**근거 구분:** 아래 행·시각·집계·분류는 Main 직접 분석 및 Main+coder 독립 후속 분류의 **전달값**이다. 문서 담당 재집계·원시 로그 독립 분석값이 아니며 옛 문서 수치에서 역산하지 않았다. 사용자는 두 번 테스트했고 육안상 문제가 없었다고 확인했다. 원본은 [Editor RuntimeLog](../../../_Logs/_editor/2026-10-15/RuntimeLog.txt), SHA256 `56667171184293663D8E6EE78F9F2EFA81BDC4934E8ADB2386E72874BA7BAB0F`, 2,335,707bytes다. 오전 교정 전 경기 append 구간은 이번 판정에서 제외한다. 새 session은 L1951/18:21:47이다.

| 경기 | 구간·종료 경계 | RETURN행 | start | resume-success | provider | end 개체 | terminal Open |
|---|---|---:|---:|---:|---:|---:|---:|
| M1 | bootstrap L1971; END L2990/18:24:19.694; 로비 L2994 | 56 | 14 | 14 | 19 | 9 | 0 |
| M2 | bootstrap L3043; END L5529/18:26:26.568; 로비 L5542 | 93 | 33 | 19 | 20 | 21 | 14 |

Main 최종값은 **RETURN 총149행**이다.147이라는 중간 후보값은 사용하지 않는다.149행 전부 Distance0/Coverage=bounded/Omitted0/FormatRejected0이며 cap0·blocked0·InvalidStart0다. 양쪽 terminal Completed0/Blocked0이고 총47start/33resume/14미종결이다. 미종결은 재교전이나 종료로 진단 episode가 resume-success 없이 끝난 상태일 수 있어 이를 false FAIL14로 해석하지 않는다.

**미종결14개의 후속 분류:** 12개는 실제 이동 뒤 재교전, Unit47은 실제 이동 증거 없이 즉시 재교전 뒤 HP 적용·사망, Unit54는 경기 END 뒤 진입했다. terminal 기준으로는 재교전 상태 경기 종료10개+사망3개+종료 후 진입1개다. 두 집계는 다른 분류 축이며 하나를 다른 하나의 실패 수로 바꾸지 않는다. Unit31은 L5419 RootZ -4.308649→L5441 -4.039527로 실제0.2691 전진했고 L5442 attack-entry는 END L5529 전이다.

아래는 M2 pending 전체 자리 목록(Main 전달값)이다. Key는 Unit,Episode이며 마지막 stage는 모두 end다. Root는 마지막 RootHex이고 이동 성공 표시가 아니다.

| Key | Type | start 행·시각 | 마지막 end 행·시각 | Root | terminal 후속 분류 |
|---|---:|---|---|---|---|
| 29,2 | 15 | 4344 /18:26:03.702 | 5572 /18:26:29.214 | 6;-1 | 재교전 상태 경기 종료 |
| 31,2 | 17 | 5420 /18:26:23.255 | 5574 /18:26:29.214 | 6;1 | 재교전 상태 경기 종료 |
| 33,1 | 16 | 3504 /18:25:43.564 | 3802 /18:25:53.020 | 6;1 | 사망: L3803 fatal HP20→0 |
| 35,1 | 11 | 4039 /18:25:57.534 | 5578 /18:26:29.215 | 6;-1 | 재교전 상태 경기 종료 |
| 36,1 | 14 | 4153 /18:26:00.844 | 5582 /18:26:29.218 | 6;-1 | 재교전 상태 경기 종료 |
| 37,1 | 13 | 4270 /18:26:01.941 | 5586 /18:26:29.220 | 6;-1 | 재교전 상태 경기 종료 |
| 41,1 | 13 | 5064 /18:26:16.717 | 5590 /18:26:29.222 | 6;-1 | 재교전 상태 경기 종료 |
| 44,1 | 16 | 4618 /18:26:08.970 | 5598 /18:26:29.225 | 6;-1 | 재교전 상태 경기 종료 |
| 46,1 | 4 | 3891 /18:25:54.245 | 4033 /18:25:57.531 | 7;1 | 사망: L4034 wave apply HP190→0 |
| 47,1 | 0 | 3884 /18:25:53.890 | 4141 /18:26:00.287 | 6;1 | 사망: L4142 fatal HP40→0 |
| 49,1 | 16 | 4943 /18:26:14.392 | 5610 /18:26:29.231 | 6;-1 | 재교전 상태 경기 종료 |
| 53,1 | 16 | 5000 /18:26:16.231 | 5618 /18:26:29.236 | 6;-1 | 재교전 상태 경기 종료 |
| 54,1 | 13 | 5533 /18:26:26.571 | 5621 /18:26:29.238 | 6;-1 | END L5529 뒤 진입 |
| 58,2 | 16 | 5399 /18:26:22.031 | 5625 /18:26:29.240 | 6;-1 | 재교전 상태 경기 종료 |

**타입별 RETURN episode 분포(Main 전달값; 타입명 임의 환산 없음):** M1 type17=9(Units15/13/16/8),20=1(Unit4),22=1(Unit5),23=1(Unit9),24=1(Unit10),26=1(Unit7). M2 type0=1(Unit47),10=1(Unit32),11=3(Units35/43/48),12=5(Unit28),13=5(Units37/41/50/45/54),14=1(Unit36),15=2(Unit29),16=6(Units33/44/49/53/58),17=2(Unit31),18=6(Unit27),4=1(Unit46). 분포의 episode와 고유 개체 수를 혼동하지 않는다.

**공간·전투 관측:** Root 위치가 있는 Pursuit 표본은 M1 147/M2 439=586으로 모두 RootTile=UnitTile다. 전체 Pursuit640 중 Root 없는 terminal54행은 이 정합 모집단에서 제외했다. checkpoint10건 모두 Reached/Consume10, attack-entry149다. M1 Lion Unit5·Rhino Unit9는 RETURN 직접 resume 사례다. Rabbit Unit11/19의 pursuit8표본은 정합 정상이나 별도 target-loss resume 표본은 없다. 이 미관측을 위해 강제 재시험을 요구하거나 이번 focused 결과를 전체 FAIL로 넓히지 않는다.

일반 HP dispatch M1 133/M2 130=263은 모두 Applied/full-key 중복0/negativeLate0이며 최대 지연42.2269968092/92.2595979185ms다. timing detail cap4(M1 Unit8/13, M2 Unit28/31) 뒤 타격은 전체 완전 관측이 아니며 RETURN/Pursuit cap0의 이번 복귀 판정과 분리한다. 공격 정밀 시각·native marker 전체 동등성을 이 숫자로 입증하지 않는다.

**경고 원인 정정·분리:** ERROR/Exception0, WARN은 M1 3/M2 8=11이며 timingcap4+TimedDamageEffectChannel staleDrop7이다. M2의 BurnDrop6개(L3227/3320/3484/3554/4035/4529)는 같은 key의 앞선 LocalDeath 닫힘 뒤 발생했다. M1 L2861은 Match0/defaultDamageState이며 Bloom Target14 LocalDeath L2844/2845와 HealStop→BindingRejected/StopBeforeReady L2858~2860에 인접한 기존 heal cleanup 기본 damage 관측 잡음이다. 일곱 건 모두 유효 damage 동일-key 중복 Stop이라고 쓰지 않는다. 이번 공통 이동 실패와 혼동하지 않으며 기록만 하고 수정하지 않았다.

**최종 판정:** 사용자 육안 정상과 위 runtime 증거에 한정해 **이번 SOLO 공통 이동 복귀 focused PASS/CLOSED**다. RuntimeLog에 native 메뉴 PASS 근거가 없다는 것은 메뉴 미실행의 증명이 아니므로 실행 여부를 단정하지 않는다. 이번 수용은 Unity self-validation native PASS, 전25종 검증, 멀티 동등성, global PASS가 아니다. 랠리·광산은 사용자 명시 제외다. 코드 source3EB17/validator6367 세대는 변경하지 않았고 추가 코드·빌드·TC·새 Task·Git·원시 로그 변경 없이 문서만 갱신한다. 문서 검사와 baseline은 Main 담당이다.

### 승인 상태와 구현 인계 완료

사용자는 기존 Research·Plan 갱신 후 SOLO 추격 위치 정합, 대상 소실 후 이동 재개, 이를 확인할 싱글 로그 구현에 「그렇게 진행해」로 승인했다. 아래 원인 분석·provider 재현은 수정 전 근거로 보존한다. 이번 승인은 기존 공격 타이밍·멀티 동작이나 광산·랠리포인트·성능·VFX 세부 조정의 변경 승인이 아니다. 구체 계획·Main 검토 승인에 이어 최종 구현 receipt와 Main 직접 검증을 수령했다. 아래 최종 결과가 현행 상태이며 중간 검사·초기 실패는 당시 이력으로 보존한다.

**계획 수령 당시 이력:** SOLO Legacy 공통 이동 적용에서 기존 공간 후보 평가·전이 policy·preflight를 통과한 실제 타일 경계를 순차 commit하고, endpoint의 중복 ProcessStep/OnEntered를 막는 계획을 수령했다. A*·Chase·복귀 align을 대상으로 하되 멀티 Legacy 분기, 복귀 planner·InvalidStart·blocked budget·공격 ownership·nonwalkable terminal contact는 보존한다. bounded SOLO 개발 로그는 실제 비영 이동과 정합을 확인한 resume-success를 다른 전이와 구분한다. 당시에는 실제 policy·commit·역 index·provider 및 거부/중복/비인접 경계 검증, actual View native gate 준비, Runtime/Editor compile이 예정이었고 receipt를 기다렸다. 예정 코드는 UnitView.cs와 RunUnitActionSelfValidation.cs의 총 2파일이며 정확한 경로는 Plan 4항에 명시했다. 이후 적용·최신 receipt 대기는 해소됐고 현행 결과는 아래 영수증을 따른다. native·실기는 여전히 미실행이며 추가 규칙 변경은 없다.

**2026-10-15 Main 독립 검토 인계:** 문서 담당의 「UnitView와 SelfValidation 파일 2개」 오독은 Plan 4항에서 총 2파일로 정정했다. 나머지 계획은 적합 판정이며 코더 조건부 GO가 전달된 상태다. 이 승인은 구현 완료·검증 PASS의 receipt가 아니다.

<a id="solo-return-final-receipt"></a>

### 2026-10-15 구현·controlled 검증 영수증 — Coverage 보완 후 현행 세대

**최신 상태:** RETURN 일반 줄의 Coverage 조건이 `_soloRecoveryOmitted > 0 || _soloRecoveryFormatRejected > 0`으로 보완돼 포맷 거부 이력 뒤 정상 짧은 줄도 incomplete를 유지한다. 문서 담당은 최신 UnitView·validator SHA256과 해당 조건을 직접 확인했다. 게임플레이 변경 없이 로그 조건 하나만 보완한 세대이며, 아래 1차 세대의7B7/5234 hash·compile을 현행으로 사용하지 않는다.

#### 현행 receipt — 2026-10-15 Coverage 보완 후

| 구분 | SHA256 / 실행 결과 |
|---|---|
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | `3EB17C328CA8DA1D352EBB6F5DB49BF30D28CF32A5E30BE9ED39A62B18A9D6C9` — 직접 일치 확인 |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | `6367A1599272D6DFAA069B76D6178E57AF5F521FA733B6F0FB2A88011C0011C5` — 직접 일치 확인·변경 없음 |
| worker Runtime DLL | `E9A7D35484AF4A4AB1FF7981ADC3A0FA48EC44FFC6EED39EB600DBD4C0756739` |
| worker Editor DLL | `402DA412832BF346D2ACC7DE5FEAA5DF6423FE7B2F3AFD22AA3DFF94EAE602F6` |
| Main fresh Runtime compile | `91839c` exit0/errors0/warnings23 |
| Main fresh Editor compile | `ce8fe0` exit0/errors0/warnings0 |
| Main managed 실제 policy/preflight/commit/index/events/provider | `b20bbb` 두 case exit0 |
| Main 독립 IL current | `9157ac` — 실제 로드 worker Runtime hash E9A7…6739 일치, stageTrue/exit0 |
| Main-check Runtime DLL | `46A6EF747A1E42F52CDE07F9621BB73B97DB31449D31539F3BF3E19BAE85A234` |
| Main-check Editor DLL | `5FB9FDF2996F006D4F9E4C3F27013C3C930954633284B6A458EEF1E927FB3EC5` |

source 일치·조건 검토 외 실행값은 Main 직접 검증 전달값이며 문서 담당 독립 실행은 아니다. Before의 기존 `c0571f`/D276…E038/stageFalse 대조군은 계속 유효하다. 현행 current와 함께 **SOLO stage 호출 연결만의 RED/GREEN**이며 View 움직임·전체 coroutine 실행은 아니다. worker와 Main-check DLL은 별도 산출물로 구분한다. 보호668의 앞선 최종 changed0/added0 비교는 이후 로그 조건 코드만 수정했으므로 Main이 유효함을 재확인했다. 클립·씬·config·멀티·타격·규칙 수치의 변경은 없고 Library 덮어쓰기·Unity native·사용자 실기·빌드는 없다. 기존 programmer 메모리 hash/교훈은 아래1차 인계와 동일하며 메모리 성장 baseline은 Main 담당이다.

#### 보완 전 1차 최종 시점 이력 — 아래 hash·compile을 현행으로 사용하지 않음

**[🔴 2026-10-15 정정 — 원문 유지: 아래7B7 source·5234 IL 및 DAFA Main-check Runtime은 Coverage 보완 전 세대다. 이후 최신값은 바로 위 현행 receipt로 대체됐다. 당시 검증 사실은 보존하지만 새 세대 검증으로 재사용하지 않는다.]**

최종 코드 2파일과 기존 programmer 메모리 1파일의 SHA256은 문서 담당이 직접 읽어 아래 전달값과 일치를 확인했다. source 내용 검토는 독립 compile/native 실행과 다르다. 실행 ID·exit·시간·보호 집계는 Main 직접 실행 인계이며 문서 담당 재실행값이 아니다.

| 파일 | 최종 SHA256 |
|---|---|
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | `7B7E2F379BB1F7FC7B9D8EDD44CEF41F93361AB5FC5E3F110058525A7D7887F1` |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | `6367A1599272D6DFAA069B76D6178E57AF5F521FA733B6F0FB2A88011C0011C5` |
| `.claude/agent-memory/game-programmer/combat-fixes.md` | `24FCCF3DCD08F7C03DF28D8A9874B8D86B90CD2737FB585C57CBC86A334285CD` |

**실제 코드 반영:** SOLO Legacy의 공통 `ApplyMovementAuthorityFrame`에 기존 공간 후보 평가·policy·preflight 후 Root와 순차 spatial commit이 연결됐다. A* 중간 경계에서 추격으로 전환되는 경우도 포함하며, SOLO endpoint `ProcessStep`/`OnEntered` 중복을 막고 `prevActualTile`을 committed 상태에서 읽는다. 멀티 Legacy 기존 endpoint 분기는 유지한다. 새 private `TryStageSoloSpatialCommit`은 이미 최종 nonwalkable contact 타일 안에 있고 논리 위치가 인접 마지막 walkable 타일인 제한 조건의 반복 contact를 보존한다. 중간 차단·비인접 이동·경로 밖 진입을 수용하거나 InvalidStart/blocked budget을 완화하는 변경이 아니다. 복귀 planner·공격 ownership·타격·멀티 계약·규칙 동작 수치는 변경하지 않는다.

**RETURN 로그 직접 검토:** `[UAS-SOLO-RETURN]`은 UNITY_EDITOR/DEVELOPMENT_BUILD의 개발 로그다. start/provider/blocked/resume-success/completed/end와 최초 cap을 구분하며 detail64 뒤 cap1, end는 detail 예산과 별도로 기록한다. 누적 Started/Resumed/Blocked/Completed/Open/Omitted/FormatRejected는 상세 출력 예산과 별개다. 일반 상세 필드는 Stage/Unit/Type/Mode/Episode/RootHex/LogicalHex/Distance/Provider/Reason/Frame과 누적값이며, cap 및 포맷 거부 fallback은 축약 필드와 Coverage=incomplete를 사용하므로 모든 줄이 full 필드라고 쓰지 않는다. `resume-success`는 pending 복귀가 실제 A* 비영 이동을 했고 pursuit/Chase가 아니며 RootHex와 논리 타일이 일치할 때만 닫힌다. 이미 목적 중심인 completed는 걸어서 재개한 성공과 분리한다. detail cap 이후 집계가 남더라도 모든 사건 상세가 관측됐다는 뜻은 아니다. 매 frame 출력은 없다.

**Main 직접 검증·보호 영수증:**

- 실제 managed `policy → preflight → commit → index/events → provider` 두 case는 최종 독립 실행 `74e090` exit0다. 앞선 `b3357b`는 중간 실행 이력으로 구분한다.
- 수정한 독립 IL Probe는 실제 로드 세대를 확인했다. before DLL `D2768709C5573A0CCE1A79EEE00740CF4711F601237A94ED8364CC9B2FD9E038`는 stageFalse/비영 exit(`c0571f`), current DLL `5234D1DDD34D4DE14B0A37F6D3C8727E8A9F26DEEACDA052EBBA772EC18D6114`는 stageTrue/exit0(`5118b0`)다. 이는 **SOLO stage 호출 연결만의 RED/GREEN**이며 View 움직임·전체 coroutine 실행이 아니다. 아래 초기 `244042` wiring 결과를 대체하며 그 값은 계속 근거에서 제외한다. Mono identity 통합 의심을 확정 원인으로 승격하지 않는다.
- fresh compile Runtime `01b1ca` exit0/errors0/warnings23, Editor `849cf3` exit0/errors0/warnings0. Main 최종 source hash 두 개도 위 값과 일치했다. Main-check Runtime DLL SHA256 `DAFA5B7B590AB3388BFE33F5316DD861A2FE9C7525CC5E97F79A464B0477A928`(17:44:35), Editor DLL `5FB9FDF2996F006D4F9E4C3F27013C3C930954633284B6A458EEF1E927FB3EC5`(17:44:39)는 최종 UnitView source17:41:53·validator17:38:04 이후 산출물이다. IL 검사 DLL hash와 별도 compile 산출물 hash를 같은 파일로 취급하지 않는다. Library 덮어쓰기는 없다.
- Main 최종 보호668파일 SHA 비교 changed0/added0: 클립·프리팹·씬·config 및 CombatUseCase/Factory/Relay/AttackShadowObserver를 포함한다. 이는 해당 보호 manifest 범위이지 리포지토리 모든 파일 무변경 주장이나 gameplay 수용이 아니다. Main `git diff --check` exit0도 별도 형식 검사다.

**검증 경계:** 기존 SelfValidation에 actual View를 호출하는 `ValidateSoloSpatialRecoveryBoundary` gate가 준비됐다. 코드상 경계 commit·역 index/events·provider·중복 endpoint·중간 차단 거부/무변경·최종 nonwalkable 반복 contact를 검증하지만, gate 코드 존재/compile을 native 실행으로 바꾸지 않는다. 전체 actual View/coroutine·Unity native 메뉴·사용자 실기·빌드는 NOT RUN이다. 다음은 Unity 검증과 대상 소실 후 실제 이동 복귀·RETURN 로그를 확인하는 실기이며, 현재 판정은 구현 완료·controlled 검증 통과/실기 OPEN이다. 기존 개별 수용은 보존하고 전체25종·전체 동등성·global PASS를 선언하지 않는다.

**문서·메모리 경계:** 상세 영수증은 이 절만 사용하며 상태 문서는 요약 링크를 둔다. programmer 기존 combat-fixes에는 공통 SOLO 경계 commit·endpoint 중복 방지·native와 managed 증거 분리 교훈 한 항목이 추가됐다. 같은 교훈을 공용 MEMORY에 중복 복제하지 않으며 문서 구조 변경이 없어 document-manager 메모리는 생략한다. 최종 문서 검사와 메모리 성장 baseline 갱신은 Main 담당이다.

**[2026-10-15 후속 수용 — 위 구현 인계 당시 원문 유지: 위 「실기 OPEN/NOT RUN/다음은 실기」는 구현 인계 시점의 기록이다. 이후 사용자2경기 육안 수용과 runtime 분석으로 이번 SOLO 복귀는 상단과 같이 focused PASS/CLOSED다. native 메뉴 실행 여부는 저장 로그로 미확인일 뿐이며 이번 closure의 blocker나 재시험 필수 조건이 아니다. RETURN149행·Pursuit 공간586표본은 별도 모집단이며 타격 상세 cap4를 이 표본의 cap으로 옮기지 않는다.]**

<a id="solo-return-interim-validation"></a>

### 2026-10-15 Main 중간 검사 당시 이력 — managed 한정 성공 / 초기 wiring 근거 제외

아래는 Main 직접 실행·검토의 전달 결과이며 문서 담당이 실행하거나 독립 재측정한 결과가 아니다. 최종 수정 receipt는 아직 수령하지 않았다.

- **Actual managed 경로:** Main tool `b3357b`에서 실제 managed `policy → preflight → commit → index/event → provider`의 두 case가 exit0였다. 해당 실행 경계의 성공이며 actual View native 실행, 전체 수용, 사용자 경기에서의 정지 해결은 증명하지 않는다.
- **IL wiring negative control 실패:** Main tool `244042`에서 coder의 초기 IL wiring Probe가 `Before.dll`에도 True를 반환했다. 수정 전 대조군을 구별하지 못했으므로 초기 wiring 값으로 RED/GREEN이나 교정 연결 성공을 기록하지 않는다. 이 실패는 검증 중 잡은 false positive이며 게임 코드 오작동의 추가 증거가 아니다.
- **원인·후속:** Mono assembly identity 통합은 의심 단계이지 확정 원인이 아니다. Main은 coder에게 별도 runtime 참조가 없는 IL probe와 실제 로드 위치·해시 확인을 즉시 요청했다. 수정 probe·유효한 대조군·최종 실행 결과의 receipt를 기다린다. 앞선 managed 두 case 결과와 실패한 wiring 증거 축을 섞지 않는다.

### 확인된 정적 결함과 실제 provider 재현

Main 정적 확인상 SOLO Legacy 추격은 UnitView2172~2220/5712에서 Root 월드 이동·checkpoint 소비를 갱신하지만 논리 `UnitData.Position`은 갱신하지 않는다. Reducer 경로에는 실제 spatial commit이 있으며 두 경로를 동일시하지 않는다. Root와 DomainXZ 월드 좌표 일치도 논리 타일 일치와 다르다. SOLO의 RequestLatestAuthoritativeRepath는 공용 RequestAuthoritativeRepath를 호출하고, UnitView3584~3620 및 UnitMovementUseCase134~156의 시작점 거리 guard가 이 불일치를 거절한다.

Main 실제 RepathProbe 실행 `cef5dd`는 exit0이다. [Probe 소스](../../../../../../Temp/unit-closure/RepathProbe.cs)의 actual RequestAuthoritativeRepath+FlowField에 로그 RootTile 분류 mapper를 주입하고32×40 walkable fixture를 사용했다. 문서 담당은 소스·exe 해시 일치를 직접 확인했으며 아래 실행 결과는 Main 직접 실행 인계다.

| 원본 입력 | 논리 타일 | 주입 RootTile | 거리 | 실제 provider 결과 | 경로 수 |
|---|---|---|---:|---|---:|
| L1393 Unit22 Rabbit | 6;12 | 6;14 | 2 | InvalidStart | 0 |
| 같은 입력의 대조군 | 6;12 | 6;13 | 1 | Success | 4 |
| L1724 Unit27 Rabbit | 6;13 | 5;15 | 2 | InvalidStart | 0 |
| 같은 입력의 대조군 | 6;13 | 6;14 | 1 | Success | 4 |

네 경우 모두 LogicalUnchanged=True다. Probe source SHA256은 `CA19EEE5DCED648AB6E4F0278BF783DD4D4707770AB7AC2687BA53CFC912D2F0`, exe는 `68F94B103F6647AB19BE5C73AC27C5EB3B036A22BCE8590C75C14C8DF41B2AD0`다. 이는 실제 provider의 guard 거절과 거리1 대조군 성공을 증명하지만 실제 맵·native 좌표 mapper·A* forward candidate loop·Unity coroutine 전체 정지 재현은 아니다. 대조군의 mapper 변경을 게임 수정이나 스냅 해결책으로 제안하지 않는다.

### 후속 경로의 한계·현재 판정

Main 정적 경로는 outer4471의 fallback null→guardRejectedInvalidPath→navigationBlocked4020/4031에서 환경·pending 변화 대기를 가리킨다. 그러나 BeginPostCombatTrace635의 NetworkActive/Server gate 때문에 SOLO 후속 return trace가 없으므로 이번 실제 사건이 navigationBlocked 뒤 어느 분기를 지났는지는 확정할 수 없다. Rhino/Lion도 같은 원인이라고 개체별 단정하지 않는다. 공격 종료 Action 반환·cooldown 대기는 b657 기준과 같고 활성 clock은 비paused·Remaining 유한으로 확인돼, 고정 대기 시간 자체가 새 원인으로 입증된 것은 아니다.

확인된 결함은 **SOLO chase의 실좌표·논리 타일 불일치가 복귀 repath guard 거절을 일으킨다**는 것이다. 기존 checkpoint29건·stall0는 추격 중 관측일 뿐 post-close 복귀 정상 보장이 아니다. 공통 복귀 결함을 OPEN으로 재개하며 기존228 HP·타이밍/UI 결과는 해당 범위의 참인 근거로 보존한다. **2026-10-15 상태 갱신:** 당시 분석만 승인됐으나 이후 사용자가 상단의 한정 교정 범위를 승인했다. 새 스냅·대기 시간 제거·사거리/guard 완화는 선택된 해결책이 아니며 실제 gameplay 패치 결과·새 Task/TC·빌드 증거는 없다.

<a id="solo-match-2026-10-15"></a>

## 2026-10-15 초월8종 싱글1경기 — 관측 범위 기술 통과·전체 수용은 별도

이번 분석은 새 코드 교정이 아니라 사용자 경기에서 기존 복원 결과가 어떻게 나타났는지 기록한다. 근거는 [Editor 원본 로그](../../../_Logs/_editor/2026-10-15/RuntimeLog.txt), SHA256 `CEE86E5E45D344B23A555151A24539652DC18572B42997567575696FDD9A62CC`다. 문서 담당은 파일 해시를 직접 확인했고 아래 통계·인과 구분은 Main 전체 분석 및 Main이 검토한 Fox 담당 독립 분석의 최종 인계다. 물리1954행/비공백1953행이며 초기1953은 비공백 행수였다. 경기10:59:39~11:01:18, 로비 복귀11:01:31, type20~27 초월8종이다. 전체 파일 시작·종료 시각과 경기 구간을 혼동하지 않는다.

### 일반 권위 타격·추격 관측

일반 timing/pursuit30개체와 Bloom1개체로 총31개체가 관측됐다. 일반 hp-dispatch228건은 모두 StatusApplied, 전체 키 `Unit:ReservedEpoch:Seq:Hit` 중복0, `now < due` 조기 적용0이다. 아래 값은 관측된 dispatch만 집계하며 로그 cap 뒤 전체 정상 건수를 확정하지 않는다. 전체 최대 지연은 Unit8/Eagle의71.52659289761232ms(원본 L482)이며 표의71.53은 반올림값이다. 일시 지연 관측이지 프레임 정체·기기 성능·렉의 원인이 확정된 것은 아니다.

| 유닛(type) | 적용 건수 | 최대 지연(ms) |
|---|---:|---:|
| BearGuard(20) | 6 | 8.45 |
| FoxMagician(21) | 37 | 22.975845 |
| LionKnight(22) | 27 | 8.323176 |
| RhinoBreaker(23) | 3 | 4.19 |
| EagleArcher(24) | 91 | 71.53 |
| RabbitTrickster(25) | 61 | 21.64 |
| MushroomBomber(26) | 3 | 4.72 |

추격 checkpoint29건은 모두 Reached/ConsumeTrue이며 attack-entry72, stall/cap0이다. close-post-combat-detect-lost48·reselect2·close-detect-lost2는 구분해 유지한다. 이 관측은 Root/Domain 전역 정합성이 모두 정상이라는 판정이 아니다. ERROR/Exception0, WARN14는 SOLO-TIME cap6와 TimedDamageChannel staleDrop8이다. cap 대상은 Eagle Unit8/13/15/19와 Fox Unit1/2이며 cap 이후 관측 완전성은 보장되지 않는다.

### Fox 예약·권위 피해·native metadata

5개체44예약은 Applied37+owner 취소7, 관측 예약 미결0·중복0·HP 차감 불일치0이다. 44건 모두 `Due−Anchor−Loop×4 = 2.8333332538604736초`로 일치했다. 조기 적용0, 적용 지연0.087147~22.975845ms다. 원래2:25 의도를 따른 예약·피해의 이번 관측 범위는 통과이며 새로운 피해 writer나 코드 변경은 없다.

native metadata41건 모두85/30이다. 36건은 unit/epoch/Now가 HP와 같고 나머지5건은 현재 문맥에서 취소된 예약과 관련된다. 다만 `ReservationJoin=unassigned`이므로 정확한 native↔예약 owner 상관관계가 입증됐다고 쓰지 않는다. HP37건 중 native 행 미관측1건은 Unit1/2 상한에 인접하며 VFX 누락으로 판정하지 않는다. charge1초 source VFX/SFX 시작·종료 로그가 없어 재생 여부는 판정할 수 없다.

**Fox UI 독립 검토 최종 확정:** 80행=received40/emitted40이며 도착 순서의 `attacker/victim/isUnit/hp/frame` tuple은40쌍 모두 같은 frame, 키·순서 불일치0이다. 모든 dispatch는 text-played다. 같은 localTime 시계 안의 수신→방출 차이는0.517~1.334ms, 음수0이며 최대 사례는 원본 L1184→L1185의 attacker1/victim12(건물), HP1050/frame100347이다. HitPresentationQueue localTime(realtime)과 authorityNow(scaled)를 서로 뺀 값이 아니다.

관측된 권위 HP37행 모두 대상·HP 기준 UI 대응이 있다. 추가 UI3건은 L1776(Unit1/HP2100), L1854(Unit2/HP1950), L1869(Unit1/HP1800)이며 상세 상한 뒤에도 UI가 계속됐다는 관측이다. 이3건의 Due는 검증할 수 없으므로 권위 타이밍 통과 모집단에 더하지 않는다. 같은 frame의 수신·방출/text-played는 해당 로그 경계의 성공이며 실제 픽셀 표시나 charge VFX/SFX 수용을 측정한 것은 아니다.

### Bloom 힐·종료 관측과 진단 경고

Bloom Unit6은 Starts10·Plays10·dispatch10=Applied7/TargetUnavailable3이다. 준비 시간은1.0007004~1.0097898초, 완료된 post-dispatch cooldown9회는3.0013572~3.0108354초이며10번째는 caster 사망으로 종료됐다. HoT7결과 중 Expired4건은 각 ActualHealed200, 대상 소실 중단3건은 각각15/58/10이다. 자기 힐seq10의 markerNearZeroPreserveYaw는 baseline0→0·drift0이다. 방향 벡터가 정해지지 않는 경우에 정상 각도를 새로 정의하거나 전체 시각 부드러움 PASS로 확장하지 않는다.

Bloom END의 Grant/Play/Stop/Drain/Return은 각7, Overflow/Fault0이지만 UnknownTrue가 남는다. Main 소스 확인상 UnitHealCastDiagnostics.cs63의 `CountDrop != 0` 조건이며 이번 target death Drop3에 따른 표시다. 이 경고만으로 회복 누락을 확정하거나 모든 자연 drain·전체 표현 수용 완료로 바꾸지 않는다. staleDrop8 중7건은 힐 HoT4건 만료·3건 대상 소실 뒤 cleanup이 ObserveDamageStop을 무조건 호출하면서 default damage state 참조에 invalid channelStop 경고를 남긴 경로다(Main 소스 확인: UnitCombatUseCase2637, TimedDamageEffectPresentation68). 나머지1건은 PoisonTarget18의 LocalDeath AttachmentClosed 뒤 같은 state 재Stop이 거부된 것이다. 피해 누락 증거는 아니지만 진단 잡음은 남아 있다. writer/UI 시각 PASS를 주장하거나 이를 수정한 것으로 기록하지 않는다.

### 이번 판정·후속 경계

원래 Fox 조기 피해·근접 불발은 타격 관측 범위에서 재발 근거가 없으며, 기록된 권위 타격·예약·추격 중 checkpoint 및 힐 시간의 기술 범위는 통과다. 다만 사용자 보고의 대상 소실 후 정지와 실제 provider 재현으로 공통 이동 복귀 결함은 상단과 같이 OPEN이다. cap·unassigned·UnknownTrue와 표현 미관측이 있어 경기 전체 PASS, 모든 native 실행 완료,25종 전체 테스트·멀티 동등성·시각 최종 수용은 선언하지 않는다. 기존 개별 수용을 취소하지 않는다. 코드 교정·새 Task/TC·추가 로그·빌드·강제 재실기는 요청·수행하지 않았으며 문서 검사는 Main이 실행한다.

## 2026-10-14 구현 인계 당시 목표·Fox 원래 의도 정정 — 경기 전 controlled GREEN 이력

현재 목표는 **마지막 사용자 수용 교정의 실제 SOLO 반영**이다. 아래 전 유닛 감사는 비교 근거 이력이며, original 표기 차이 8종은 자동 수정 목록도 모든 버그 목록도 아니다. 현재 config나 옛 Task의 잘못 환산된 literal을 정답으로 삼아 사용자의 보고를 기각하지 않는다.

**현행 범위 정정 — 원래 복원만, 제안한 범위 확장은 철회:** 사용자 재확인에 따라 General23 gate 통합·native occurrence lease 이행·멀티 gate 복사·새 overlap 규칙은 원래 복원의 잔여 작업이 아니다. 이들은 미구현 제안이며 Main이 이를 필수 정책 선택처럼 질문한 범위 확장 판단을 철회한다. 기존 멀티의 zeroAim/InvalidPose와 SOLO Legacy의 공격 허용 차이는 기존 구현 차이의 관측이지 새 사용자 정책 결정이 필요한 복원 blocker가 아니다. 이 차이를 없애는 generic source 쓰기 승인은 없고 새 ZeroException 미래 사양도 작성하지 않는다.

**복원 판정 범위:** Fox85/30 source8 적용·Main fresh compile/actual controlled HP GREEN은 확인됐다. native Animator/relay fixture·AssetDatabase/메뉴·멀티/시각/build는 NOT RUN이다. 기존 pursuit/clock 적용·검증은532e2033에 보존된 별도 근거로 유지한다.25종 기존 에셋/GUID 연결 보존 감사는 모든25종 현재 실행·시각 해결을 증명하지 않는다. 따라서 「모든 원래 교정 소실」이나 「모든 유닛 현재 실행 완료」 어느 쪽도 선언하지 않으며 original8 표기 차이를 미반영 수정8건으로 바꾸지 않는다. Fox 현행 수치 문서는 실제 사용자 원래2:25 명확화와 적용 source에 근거한 정당한 복원 기록으로 유지한다.

**FoxMagician 원래 피해 기대값은 사용자 재확인 `2:25@30fps = 85/30 = 2.8333333초`다.** 사용자는 잘못 적용된 2.25초 때문에 클립을 바꿨다고 설명했다. 따라서 이를 「기존 2.25초 정책에서 새 값으로 변경」이라고 설명한 Main의 판단은 철회한다. 아래 옛 Task 인용·2.25초 timer 관측·당시 수용 기록은 그때 저장·실행된 사실의 이력이지 원래 frame 의도를 증명하는 현행 권위가 아니다. 이전 「승인된 2.25초보다 이르지 않다」는 비교도 사용자 기대 충족 판정으로 사용하지 않는다.

Fox 현행 source 계약은 **charge `OnAttackCharge @ 1초` / damage `OnAttackHit @ 85/30초` / 권위 HP 적용**이다. Main 코드 GO 후 구현 담당8파일 적용·freeze receipt와 문서 담당8해시 일치를 확인했고, 이제 Main 새 scratch compile/Mono Probe exit0까지 인계됐다. charge는 기존 lease→source VFX/SFX만, OnHit는 metadata만 처리한다. Factory Fox 제외 조건 제거·type21 fallback/observer/gate 정합화가 적용됐다. UnitCombatUseCase 주석-only 보존은 구현 담당 receipt이며 writer/Immediate/range/Root는 변경하지 않는 계약이다. full8 manifest와 정확한 Main 실행 범위는 아래 receipt 절을 따른다. 문서 담당 hash/source 확인은 독립 컴파일 실행이 아니며 Main controlled GREEN도 native/멀티 수용이 아니다. Main curve prefix 보존은 확인됐다(2026-10-15 상태 갱신, Main 실행 인계). Set fixture 수정 후 최신8manifest는 갱신됐다. Spirit9 감사는 수령됐으며Main 기존23종/25후보 harness EXIT0는 수령됐고16종 최종 인계도 수령해 합본 감사를 완료했다.

**컴파일 준비·실행 경계:** 실제 D Unity Roslyn/참조 remap 준비 후 구현 담당 Runtime/Editor/Probe scratch compile exit0와 writer RED→GREEN에 더해 Main은 별도 `Temp/fox-main-audit`에서 새 Runtime/Editor/Probe compile exit0 및 실제 Mono Probe exit0를 인계했다. 문서 담당은 실행하지 않았으며 exact 참조별 매핑을 임의로 조립하지 않는다. Library 덮어쓰기·native/AssetDatabase/menu·멀티/빌드 실행을 이 결과로 주장하지 않는다. 자세한 Main 출력·미검증 경계는 아래 절을 따른다.

### 브랜치 이전·보존 receipt — 새 SOLO 복원 구현과 분리

- Main 인계: 사용자 승인으로 오래된 미사용 `index.lock`만 제거하고 raw txt/log/zip을 staging에서 제외해 보존 커밋을 만들었다. `git switch codex/unit-movement-attack-sync-audit` 및 `git merge --ff-only codex/docs-checker-maintenance`가 성공했으며 직후 작업 트리는 clean이었다. lock 제거·staging·switch/merge는 Main 실행 기록이고 문서 담당이 재실행하지 않았다.
- 문서 담당 현재 읽기 전용 확인: 작업 브랜치는 `codex/unit-movement-attack-sync-audit`; HEAD와 두 브랜치 ref가 모두 `532e2033448cff2332bc63f3809705239bb15a67`이다. 해당 보존 커밋의 변경 파일은 37개이며 이 문서 편집 전 `git status --short` 출력은 비어 있었다. 보존 커밋은 기존 작업의 이동이지 새 복원 patch 완료 증거가 아니다. Push 없음은 Main 인계이며 이 작업도 Git 쓰기를 하지 않는다.
- Main byte-preservation 전달값: 37파일 중 exact 10 / line-ending-only 27 / other 0 / missing 0. FF 후 Windows CRLF 변환에 따른 줄바꿈 차이이며 내용 소실 없음이라는 Main 검증이다. 문서 담당 독립 byte 비교는 하지 않았으므로 전달값으로 유지한다.

기존 [Plan](Plan.md)의 승인된 Fox8파일 계획은 코드 GO·적용 receipt까지 인계됐다. Main은 실제 적용 후 관련 Fox 현행 문서 갱신을 승인했으며 이번에는 Research/Plan과 StatsReference·Units·CombatSync·Matrix의 Fox 항목만 반영한다. VFXSFXList의 Fox 행에는 기존 타이밍 숫자가 없어 변경하지 않는다. 옛 Task 전체를 소급 재작성하지 않으며 코드·다른 유닛·다른 Task·TC/QA·새 메모리는 수정하지 않는다. 이전 `for-each-ref` 사건 기록은 사용자 개별 요청이 아니라 Main 인계·WORKFLOW 기록 의무에 따른 것으로 유지한다. 실제 새 문서 검사는 구현 검증과 별도로 보고한다.

### Fox8파일 적용 receipt — 구현 담당 검증과 Main 독립 검증 분리

원본은 [구현 담당 receipt](../../../../../../Temp/fox-plan-red/Receipt.md) 「Final Fox implementation / source freeze」다. 아래 표는 최초 적용 세대에서 문서 담당8개 해시가 receipt와 일치했던 이력이다. **2026-10-15 갱신:** anim empty-data 형식 및 validator Set fixture3호출 수정으로2hash가 바뀌었으므로 이 표를 현재 final generation으로 재사용하지 않는다. 당시8manifest를 그대로 보존하며 최신8manifest는 아래 별도 표를 따른다. General4파일 source 적용 근거가 아니다.

| 적용 파일 (`Assets/_Project/` 기준) | SHA256 |
|---|---|
| `Animations/Units/FoxMagician/FoxMagician_Attack.anim` | `3946C435B3109A04D5A9A0EAB40E7EEFB3484A4C225BF143CCECF80995ADAB9A` |
| `Scripts/Presentation/Unit/AnimationEventRelay.cs` | `7F3013FC7E08B67157B3E410EC626880C8DF5A8292E09B13D7F982E70B86796A` |
| `Scripts/Presentation/Unit/UnitView.cs` | `DDA788254323C49BB9A22DFCFA2465DA5D2AF360109C10BB7C2154F78A1FB2CB` |
| `Scripts/Infrastructure/Factories/UnitFactory.cs` | `53AD89CB705FC531165317FB2F8DD89288B2C3C2B9D8E1DA81A7198332F79396` |
| `Resources/Config/UnitStatsConfig.asset` | `947854E7D6ACEA1FFC235D784281FD5799467F44406B5D33DF34C95A067B58E6` |
| `Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs` | `85A96E783D24425EA4EBAB72B377226EF8A7344F348773B0D71981E2A4573D72` |
| `Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | `CC46AA1355168C8BE4CF8CD6830CC2A9F56CA0934A058F2AA064A959D68CECDE` |
| `Scripts/Application/UseCases/UnitCombatUseCase.cs` | `CBBA975EEF34E85128B3F371A87BC64F170A30BD6CE4E378915F19E1CF471167` |

#### Main 새 독립 compile·Mono Probe — controlled GREEN 한정

##### 2026-10-15 최신 Fox8 manifest·curve prefix 확인

Main이 확인한 최신 anim/validator2hash를 문서 담당이 현재 파일에서 다시 계산해 일치 확인했다. 나머지6파일도 직접 계산했고 최초 표와 동일하다. 변경은 Main 인계상 empty-data 형식 수정과 `NetworkContext.Set(isServer, isActive)` fixture3호출 교정뿐이며 production gameplay/HP writer 변화가 아니다. 최신 Editor compile exit0는 Main 직접 실행 인계다. 아래 최신 세대의 Main 기존23종/25후보 harness tool7add8d EXIT0·artifact4 frozenTrue를 수령했다. 이전 Probe와 이 실제 실행은 별도 출처/세대로 기록한다.

| 최신 파일 (`Assets/_Project/` 기준) | SHA256 |
|---|---|
| `Animations/Units/FoxMagician/FoxMagician_Attack.anim` | `129B0FEE44A2EA1D53A69CF63CAA1B44FC3931AC8E75F86E98A09DD475823C02` |
| `Scripts/Presentation/Unit/AnimationEventRelay.cs` | `7F3013FC7E08B67157B3E410EC626880C8DF5A8292E09B13D7F982E70B86796A` |
| `Scripts/Presentation/Unit/UnitView.cs` | `DDA788254323C49BB9A22DFCFA2465DA5D2AF360109C10BB7C2154F78A1FB2CB` |
| `Scripts/Infrastructure/Factories/UnitFactory.cs` | `53AD89CB705FC531165317FB2F8DD89288B2C3C2B9D8E1DA81A7198332F79396` |
| `Resources/Config/UnitStatsConfig.asset` | `947854E7D6ACEA1FFC235D784281FD5799467F44406B5D33DF34C95A067B58E6` |
| `Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs` | `85A96E783D24425EA4EBAB72B377226EF8A7344F348773B0D71981E2A4573D72` |
| `Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | `959E7B8DD91DFC8FCF22514B083445D2FB230EFF170405AFB6171783CADBDEFD` |
| `Scripts/Application/UseCases/UnitCombatUseCase.cs` | `CBBA975EEF34E85128B3F371A87BC64F170A30BD6CE4E378915F19E1CF471167` |

Main은 event 밖 curve prefix SHA256 `4C130FFAE806709D7EB8C03ECEBC871A9D10BE937FF690E77BCA1E27576EB4B0` 보존을 이미 실제 확인했다(Main 인계, 문서 담당 독립 재실행 아님). 현행11곳의 대기 표시는 이 사실로 정정한다. 이것은 native Animator/AssetDatabase/relay fixture·메뉴/멀티/시각/build 실행 PASS가 아니며 새 overlap 규칙도 아니다.

Main이 이번 generation을 `Temp/fox-main-audit`의 별도 출력으로 새 Runtime/Editor/Probe 컴파일해 모두 exit0, 실제 Mono Probe도 exit0로 인계했다. 문서 담당은 해당 디렉터리의 DLL/rsp/Probe 파일 존재만 확인했으며 실행 출력은 아래와 같이 **Main 직접 실행 전달값**으로 구분한다. coder8 manifest는 당시 적용 세대의 표로 유지하며 Main curve prefix 보존 확인을 수령했다. 최신8manifest는 아래에 갱신했고 이전 표는 최초 세대 이력으로 유지한다. Spirit9·Main 기존23종 harness 결과는 수령됐으며16종 최종 결과도 수령해 합본 감사를 완료했다.

| Main 실제 독립 관측 | 결과·범위 |
|---|---|
| actual YAML 입력 | `2.83333325` — 원하는 rational을 Probe 입력에 주입한 결과가 아님 |
| 권위 HP·첫 dispatch | `hpBefore=200`, `hpAt2.25=200`, `firstDamageAt=2.8333332538604736`, `events=1`, `immediate=True` |
| 실제 Factory predicate | `True`; native Factory 인스턴스·AnimationClip/AssetDatabase 추출 실행은 아님 |
| 기존 validator authority core | PASS — 실제 YAML/typed metadata 입력이며 AssetDatabase/native fixture PASS 아님 |
| 실제 formatter | `valid=True`, `maxBytes=995`, schema=`c3-authoritative-result-presentation-shadow-v12` |
| 미실행·대기 | native Animator/relay fixture/AssetDatabase·실제 메뉴·멀티 gameplay·시각·build NOT RUN. curve prefix Main 보존 확인 완료; Set fixture 수정 후 최신8manifest 갱신; Spirit9·Main 기존23종 harness 수령;25종 반영 감사 완료 |

Main GREEN은 실제 controlled writer 한정 독립 실행이며 Main 독립 RED는 아니다. 구현 담당 초기 RED·final GREEN은 별도 출처로 아래 보존한다. 기존 event/config의85/30 의미와 charge1초·native HP writer 금지 계약은 그대로이며 source4 General plan이나 전체 SOLO 해결로 확대하지 않는다.

#### 구현 담당 최초 인계의 실행·보존 이력 — 당시 Main 재실행 전

**다음 실행 근거는 구현 담당 전달값이며 위 Main 독립 실행과 별개다:**

- 실제 authority RED exit1: actual YAML2.25 입력에서 HP200→2.25초 HP50, Factory 실제 predicate Fox=False. Probe는 actual config를 읽고 실제 UnitSpawnUseCase/UnitCombatUseCase의 TryAttack/TickCooldowns/TickPendingHits를 실행했으며 원하는 offset을 입력에 주입하지 않았다. 최초 UniRx resolution/native console 문제는 BLOCKED 준비 이력으로 RED와 구분한다.
- final GREEN exit0: YAML2.83333325 입력에서2.25초 HP200 유지, 첫 damage=`2.8333332538604736`, damage events1/immediate=True, Factory 실제 predicate=True. rational85d/30과 serialized85f/30의 표현 차이는 약79ns이며1µs 비교 허용은 float 표현 경계다. 실제2.25초 조기 피해와 동일 원인으로 취급하지 않는다. actual native Factory/AnimationClip 추출은 실행하지 않았다.
- 최종 Runtime/Editor/Probe scratch compile exit0. Runtime에는 기존 deprecation/unused-field 경고가 있으며 warning-zero runtime 주장 없음. 실제 D Unity Roslyn/Bee responses와 fresh scratch Runtime 참조를 Editor에 사용했고 Library DLL을 덮어쓰지 않았다는 구현 담당 보고다. Main 독립 compile/run·최종 승인 대기다.
- 실제 formatter preflight `valid=True, manifest=True, maxBytes=995`, schema=`c3-authoritative-result-presentation-shadow-v12`. 기존 validator의 actual Fox authority core(YAML admission)는 PASS. typed hit data 입력이며 AssetDatabase/AnimationUtility/native relay/View fixture 실행은 아니다.
- 중간 formatter1001bytes는 실제 validation FAIL이었다. wrapper의 BLOCKED 표기는 그 실패를 바꾸지 않는다. Fox timeline schema v2의 `charges` 사용 후995가 final이며 callback fixture compile namespace 교정 이력도 receipt에 보존한다.

**보존·미검증:** clip event 밖 CRLF 정규화 prefix SHA256 `4C130FFAE806709D7EB8C03ECEBC871A9D10BE937FF690E77BCA1E27576EB4B0` 보존·config Fox 한 값만 변경·UnitCombatUseCase 주석만 변경·일반 marker false 분기 보존은 구현 담당 before/after 비교다. 문서 담당은 이를 해시/읽기 확인과 구분하고 전체 non-Fox runtime 수용을 주장하지 않는다. Root·range·HP writer/Immediate·VFX/SFX asset·resolver/Unresolved/LegacyFallback은 보존 계약이다. native asset 재로드/추출·callback fixture·메뉴·Host/Client TimerImpact gameplay·멀티·시각·빌드 실행은 NOT RUN. General4파일은 여전히 확인 사항 대기·미승인 patch이며 원래8종을 자동 수정하지 않는다. 기존 SOLO pursuit/clock 근거도 이번 Fox 결과로 재분류하지 않는다.

### 이번 실제 문서 검사 — 과거 결과 재사용 아님

**Fox 현행 문서 반영 뒤 새 실행(2026-10-14):** tool7390f3의 실제 완료exit1, 기존 참조6건·신규 오류 없음, 고아/감소 없음이다. 새 상시4문서→현행 Task receipt 앵커4개는 이번 검사에서 통과했다. 기준선 증가 안내는+3662이며 메모리/기준선은 이 범위에서 수정하지 않는다. 아래 초기 실행 환경 오류·이전 검사 결과는 당시 이력이다.

문서 담당 새 실행 `python3 Tools/check_docs.py`는 WindowsApps 실행 연결 오류로 시작되지 않았다. 앱 번들 Python으로 실행한 첫 시도는 cp949 출력에서 UnicodeEncodeError로 중단돼 전체 검사 결과로 사용하지 않는다. `PYTHONIOENCODING=utf-8` 및 `-X utf8`로 다시 실행한 검사(tool25218a)는 **실제 완료 exit1 / 참조 오류6건**이다. 아래 「문서 검사 경계 — Main 전달」의 여섯 위치·ID와 일치하며 새 오류는 없다. 고아 토픽/메모리 행수 감소는 이상 없음, 기존 기준선 증가 안내는 실제+3662행이다. 이번에는 메모리/기준선을 수정하지 않는다. 오류가 남은 문서는 현행 편집 범위 밖이며 검사0건/PASS를 주장하지 않는다.

Task는 검사 제외이므로 새로 사용한 Research/Plan 상대 링크와 Units 링크의 파일 존재를 별도로 확인했고 `git diff --check`도 exit0이다. 이 검사는 문서 참조/편집 형식의 범위이며 Fox 구현·타이밍·시각 수용 검증이 아니다.

<a id="artifact-restoration-audit"></a>

### 2026-10-15 마지막 artifact 반영 감사 — 25종 반영 감사 완료·추가 누락 없음

이번 판정은 사용자 기존 지시와 마지막 수용 의도가 기존 artifact/Factory/정규special 경로에 반영돼 있는지 확인하는 것이다. 새 native 이행·MP gate 복사·overlap 정책을 구현하는 감사가 아니다. 아래25행은 Spirit9종과16종 최종 receipt를 합친 반영 감사 결과다. 미수령은 복원 실패나 사용자 수용 철회를 뜻하지 않는다. 이전 old8 표기/역사 계보 질문과 이 반영 감사를 섞지 않고 모든 유닛 재테스트를 요구하지 않는다.

Spirit 담당 최종 read-only 감사와 Main 실제25clip 재추출 대조는 `b65751c6` 대비 Spirit9 artifact·Factory mapping 보존, 확정 missing0이다(Main 인계). Torrent는 일반 SOLO clock 제외가 누락이 아니라 정규special 경로이며 `TryReserveTorrentLaunch → Bootstrap Tick → SpawnWave → TickWaves → damage/heal` 연결의 **코드 반영 감사 완료**다. native 미실행은 이 코드 closure를 취소하지 않는다. 최신Fox source·Main controlled writer 및 기존 pursuit/clock 보존 근거는 위에서 별도로 유지한다.

| 유닛(ID) | 수령된 marker/주기(초) | artifact 반영 감사 |
|---|---|---|
| Pistoleer(0) | .8 /2 | 명시 clip·공통 pending/clock 보존; 추가 확정 누락 없음 |
| Assault(1) | .16666667 /.33333334 (fallback .2) | 수용된0:05·명시 clip 보존; 추가 확정 누락 없음 |
| Sniper(2) | 1.7333333 /3 | 수용된1:22·명시 clip 보존; 추가 확정 누락 없음 |
| LittleKnight(3) | .25,1.15 /3.0000002 (fallback3) | 기존2hit·반복 실행 보존; 새 frame 환산 없음; 추가 확정 누락 없음 |
| SpearMan(4) | .8333333 /2 | 수용된0:25·첫/반복 실행 통과; 추가 확정 누락 없음 |
| BattleAxe(5) | 1.02 /3.1666667 (fallback3.05) | primary·Sweep 연결 보존; AoE 제외 조건 미검사; 추가 확정 누락 없음 |
| Tank(6) | .16666667 /4 | 기존 damage branch·preset 보존; 추가 확정 누락 없음 |
| CannonCart(7) | .16666667 /4 | 기존 damage branch·공유 preset 보존; 추가 확정 누락 없음 |
| FlameSpirit(10) | `20/30,35/30,43/30,50/30,57/30,63/30` /3 | 보존·mapping 확인·확정 missing0 |
| EmberSpirit(11) | 1 /2.6667 | 보존·mapping 확인·확정 missing0 |
| InfernoSpirit(12) | 1.5 /3 | 보존·mapping 확인·확정 missing0 |
| DustSpirit(13) | 1.04 /3 | 보존·mapping 확인·확정 missing0 |
| BoulderSpirit(14) | `40/30` /4 | 보존·mapping 확인·확정 missing0 |
| QuakeSpirit(15) | 1.667 /5 | 보존·mapping 확인·확정 missing0 |
| TideSpirit(16) | 1.5 /3 | 보존·mapping 확인·확정 missing0 |
| StreamSpirit(17) | .5 /1.5 | 보존·mapping 확인·확정 missing0 |
| TorrentSpirit(18) | launch `70/30` /4 | artifact·정규special damage/heal 연결 code closed |
| BearGuard(20) | .43333334 /1.6666666 (fallback1.2) | 수용된0:13·양 모드 clip-derived 주기 보존; 추가 확정 누락 없음 |
| FoxMagician(21) | charge1 /hit85/30 /cycle4 | 원래2:25 복원·source-only charge·권위 hit·Immediate 보존; 추가 확정 누락 없음 |
| LionKnight(22) | .6,1.4333333 /2 (fallback3) | 수용된2hit 보존; fallback과 실효 주기 구분; 추가 확정 누락 없음 |
| RhinoBreaker(23) | 1.05 /2 (Attack·Attack2 동일) | 두 후보 실행 통과; native 선택은 기존 미확인 한계; 추가 확정 누락 없음 |
| EagleArcher(24) | Attack .1 /1; Attack2 fallback .1 /.700000048 | 두 후보 실행 통과; native 선택 미확인은 누락 증거 아님; 추가 확정 누락 없음 |
| RabbitTrickster(25) | .6666667 /2 | 수용된0:20·첫/반복 실행 통과; 추가 확정 누락 없음 |
| MushroomBomber(26) | 1 /3 | primary·Blast/DoT 연결 보존; secondary/drain/시각 미검사; 추가 확정 누락 없음 |
| BloomFairy(27) | heal marker1 /post-dispatch3 /clip4 | SOLO 권위·공통 heal loop·TimedEffects 정적 연결 확인; 추가 확정 누락 없음 |

**Spirit9 근거 요약:** Main은 실제25clip 재추출로 담당 결과를 대조했다. 기준 커밋 대비9종 artifact·Factory mapping 보존과 확정 누락0을 확인했고 Torrent 정규special damage/heal 연결도 확인했다.

#### Main 독립 existing23종/25후보 closure harness — 실제 exit0

Main 실제 tool `7add8d`의 standalone Mono 실행은 EXIT0, 실행 전후 artifact4 frozenTrue다(Main 직접 실행 인계; 문서 담당 재실행 아님). actual YAML·현재 Factory policy 복사 입력과 durable 적 대상 fixture/placement가 구분됨을 Main이 확인했다.23type/25candidate의 first+repeat와 기존 `ValidateSoloTimingAuthorityCore`·`ValidateSoloPursuitCheckpointProgress`가 PASS했으며 서로 다른 context도 복원했다. 새 General23 gate·native lease migration·overlap 정책을 적용한 결과가 아니라 **기존 implementation closure 검증**이다. 이 초기 실행 세대와 아래 최종 컴파일·실행 세대는 구분한다.

| 후보 경계 | Main 실제 인계 | 단정하지 않는 것 |
|---|---|---|
| RhinoBreaker | controller2clips 모두 marker1.05 /cycle2, first/repeat PASS | 이번에 새 타격값을 만든 작업 아님 |
| EagleArcher Attack | marker.1 /cycle1, first/repeat PASS | native가 유일하게 이 clip을 선택했다는 증거 아님 |
| EagleArcher Attack2 | marker 부재 시 fallback hit.1 /cycle.700000048, first/repeat PASS | fallback candidate 검증을 marker 보유나 native 선택 확정으로 바꾸지 않음 |
| 실행 제한 | Time/frameCount internal-call 경고 있음; assetInstantiation·timedEffectDrain NOT RUN | 경고를 Unity 설치/게임 오류로 확정하지 않고 cleanNative PASS도 선언하지 않음 |

**최종 독립 실행 근거:** [16종 최종 receipt](../../../../../../Temp/unit-closure/Receipt.md)를 수령했다. Main은 Runtime(`583e8c`, exit0/경고23), Editor(`c621d7`, exit0/경고0), Probe(`f48a05`, exit0)를 새로 컴파일했고 실제 Mono 실행(`36d81e`, exit0)에서23종·25후보 첫/반복 타격 및 기존 타이밍·추격 fixture2개가 PASS했다. 불균등 초기 context를 복원했고 source8+Probe.cs9의 실행 전후 freeze는 동일했다. 최신 diff 형식 검사(`590d54`)도 exit0이다.

Main 최신 SHA256: Probe.exe `49753627E1D1752F417FCDDC7ACC03E2322DBF791310B73644F8235E145F7588`, Runtime `86DD65505C171097E84E4DE046C53A0572CFB382ADAC2BDD5E0593162266877A`, Editor `BA5D5620ACABC09C71E500808D30405C317C82EEC49199786935F04175E97B44`. Probe.cs는 `99D9B86E59D6F691B751E9BA5A429D3FD7085F11EC696D11529ADCBAAB844695`로 불변이다. receipt의 Probe.exe `4D2B3B45C33D1E7E886E44CE92E82241254CB0D63586CFA281087EDE338C432D`는 구현 담당 앞선 세대 이력이며 Main 최신 산출물과 혼용하지 않는다. 최신 prefix 비교(`6d6690`)도 위 전체 `4C130…` 해시의 보존을 확인했다.

Bloom은 일반23종 harness 밖이다. Main 정적 trace는 SOLO 권위 허용→공통 heal loop→시작 시각+marker1→CastHeal→실제 dispatch 뒤3초 cooldown→Bootstrap TimedEffects 연결을 확인했다. Torrent와 함께 특수2종 연결 완료 근거이며 native coroutine 실행은 아니다. secondary AoE/DoT·TimedEffectDrain·에셋 인스턴스 생성·native 메뉴·멀티/시각·빌드는 미실행이다. Mono Time/frameCount 경고는 standalone 한계이며 clean native PASS를 뜻하지 않는다.

25종 반영 감사와 일반23종 실행의 최종 인계를 수령해 이 표를 종결했다. 기존 개별 수용은 유지하며 native 미실행을 복원 실패나 전체 재테스트 요구로 바꾸지 않는다. 상태3문서는 요약·링크만 반영한다. 최종 문서 검사는 Main이 실행하며 중복 실행·기준선 갱신은 하지 않는다.

### 이번 문서 변경 목록 — 실제 diff 기준

2026-10-15 최종 읽기 전용 diff의 문서 변경은 아래10개다. 전체 dirty 파일은19개로 문서10개·구현 담당 Fox8개(위 최신 manifest)·Main이 도구로 갱신한 `.claude/agent-memory/_baseline.json` 1개다. VFXSFXList에는 Fox 타이밍 숫자가 없어 변경하지 않았다. 새 Task·TC/QA·메모리 파일은 생성하지 않았고 메모리 본문 추가·문서 담당 코드 편집도 없다.

Main 최종 실제 문서 검사 `86bb19`는 exit1/이전 `ae94b5`와 정확히 같은 기존 참조6건·신규0, 메모리 고아·행수 감소0이며 기준선 낡음 경고는 해소됐다. Main의 증가 전용 기준선 갱신 `d7302d`는 exit0, 5개 agent 총+3662행 증가만 반영했다(삭제0·새 메모리 내용 편집0). 문서 담당은 기준선을 직접 편집하지 않았다. Main 최종 diff 형식 검사 `b45b56`는 exit0이며 source8+Probe.cs9의 freeze도 완전히 동일했다. 사용자 실기 수용은 미완료이며 native 실행·새 빌드 GO는 없다. 이번 기록 반영 외 추가 문서·구현 변경이나 검사 실행은 하지 않았다.

- `.claude/mistakes.md`
- `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UnitCombatSynchronization.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md`
- `Assets/_Project/Docs/StatsReference.md`
- `Assets/_Project/Docs/PROJECT_STATUS.md`
- `Assets/_Project/Docs/ROADMAP.md`
- `Assets/_Project/Docs/WORK_HISTORY.md`
- 기존 Task `Research.md`
- 기존 Task `Plan.md`

## 이전 전 유닛 과거 승인 계약 감사 — 25종 보고·Main 검토 이력 / Fox 현행 기준은 위 정정

사용자가 전 유닛의 **기존 교정·사용자 수용 계약을 먼저 복원한 뒤 현재 구현과 비교**하도록 승인했다. 현재 config/Stats/UnitCombatAssetMatrix/status만으로 과거 기대값을 정하지 않는다. UnitType.cs의 실제 이름·ID를 기준으로 Human8/Spirit9/Transcendence8의 각 유닛을 빠짐없이 조사한다(현재 enum 직접 읽기 기준). 타격 marker `ss:ff@30fps`, 권위 피해 offset 예외, 전체 cycle, 모델 offset/rotation, VFX 전달·종료·전투 후 복구를 별도 축으로 기록한다.

기대 계약은 옛 Task의 최종 승인/수용 문단과 후속 사용자 변경에서 추출해 경로·행/절·날짜를 기록했다. Human/Transcendence programmer 및 Spirit Copernicus 최종 감사는 Main 검토 완료다. 아래 두25행 표는 당시 수집·비교 초안으로 보존하고, 말미 「전 유닛 감사 최종 판정」이 현재 한정된 결론이다. 과거 수용 primary가 부족한 이름별 항목은 그대로 미확정이며 현재 config로 메우지 않는다. 이번 감사는 새 구현·Stats/규칙 동기화 승인이 아니다. 기존 pursuit·SOLO timing·Fox 실제 로그 근거도 보존한다.

## 이전 감사 범위와 판정 — 현행 목표·Fox 의도는 상단 정정 우선

### Historical expected 수집 초안 — 25종 / Main 현재 비교 검토용

**이 표는 과거 승인/수용 문단을 읽은 수집 결과이지 현재 모든 값의 정답/PASS 판정이 아니다.** ID는 현재 UnitType.cs로 재확인했다. `ss:ff@30fps`와 literal seconds는 구분한다. 옛 Task가 저장값을 초 단위로 검증했다는 사실만으로 원래 사용자의 프레임 표기 의도가 해소됐다고 하지 않는다. 행 번호는 이번 읽기 시점의 원본 위치이며 날짜·절을 병기한다. 현재 코드/자산 대조는 Human/Transcendence programmer·Spirit Copernicus 결과의 Main 검토 후 별도 열로 확정한다.

모델/VFX 열의 **M0**은 공통 [ActionSequence Plan](../../2026-07-22/13_26_unit-action-sequence-implementation/Plan.md) L792~813의 B1 구조·7/29 수용: identity VisualRoot, Root Motion off, 단일 Simulation writer/Client Root 쓰기 금지다. 모델 자식의 모든 yaw/import offset을 0으로 강제하는 뜻은 아니다. **M6**은 [4/28 mesh cleanup Plan](../../2026-04/2026-04-28/14_30_mesh-offset-cleanup/Plan.md) L5~10/L57~88의 Bear/Fox/Lion/Ember/Flame/Inferno mesh Y0·Idle/Walk rotation offset0 작업 기록이다. 초기 DirectionAngles -30 제안은 L41~56에서 +30으로 정정됐고 Attack import offset은 별도 유닛 테스트 대상이다. **R0**은 타겟 사망/전투 종료 뒤 연속 Attack·안전 재개를 보존하되, 개별 수용과 공통 rally/전체 migration을 분리하는 기준이다. 수치가 없는 유닛의 모델 yaw/VFX endpoint는 미확정으로 남긴다.

| 유닛(ID) | 과거 expected 시간·예외·주기 | 실제 옛 승인/수용 출처·변경/부족한 근거 | 모델·VFX/복구 기대 경계 |
|---|---|---|---|
| Pistoleer(0) | marker/HP0.8초=`0:24`, cycle2초. native continuous epoch와 진짜 re-entry 구분 | [10/4 Research](../../2026-10-04/21_58_pistoleer-attack-timing-correction/Research.md) L76/L93 및 [Plan](../../2026-10-04/21_58_pistoleer-attack-timing-correction/Plan.md) L91~93/§12.13. 설정2→0.8·명시 clip 교정; 이후 source-owner/re-entry 교정. 정밀 시각/최종 human visual는 그 Task에도 OPEN | M0/R0. 기존 source marker lease·닫힌 old occurrence 구분 보존; projectile 준비와 결과 수용 별개 |
| Assault(1) | 사용자 `0:05`=5/30초, production cycle`0:10`=10/30초; clip 미사용 fallback0.2초는 다른 값 | [10/8 Research](../../2026-10-08/10_55_assault-attack-timing-correction/Research.md) 「확인한 원본」L94~98/첫 「10/11 사용자 focused 수용」L3~5. 옛0:17·0:33은 이전 표기, 0:33 의미 미확정 | M0/R0. focused 로그 수용을 원63 expiry 해결/full visual로 확대하지 않음 |
| Sniper(2) | 사용자 `1:22`=52/30초, cycle3초 | [10/10 Plan](../../2026-10-10/23_30_sniper-attack-timing-correction/Plan.md) L11/L37~42. 1:73 옛 오표기→1:22, 저장 clip 보존. 사용자 focused 로그 수용은 위 Assault Research10/11 L3~5 | M0/R0. VFX death/despawn 소유권·정밀 접촉은 별도 |
| LittleKnight(3) | 공통 교정은 literal0.25/1.15초·2hit/3초를 전제로 함. 원래 `0:25,1:15`(Main 전달)와 동일 뜻으로 단정 못 함 | [8/31 Research](../../2026-08-31/23_30_b3-c3-runtime-failure-correction/Research.md) L77~88/[Plan](../../2026-08-31/23_30_b3-c3-runtime-failure-correction/Plan.md) L47: continuous anchor의 다음 marker occurrence 교정. [7/9 Research](../../2026-07-09/01_12_combat-hit-timing-sync/Research.md) L150~158은0.25/1.15 실기 기록. 10/11 사용자 개별 수용은 Main 공용 인계; 최초 두 frame 수치의 명시 승인 원문은 추가 필요 | M0/R0. 전용 Task 부재=미테스트 아님. safe post-combat 복구와 old MOVE 실패는 별도 |
| SpearMan(4) | 최종 사용자 `0:25`=25/30초, cycle2초 | [9/30 Plan](../../2026-09-30/18_02_spearman-attack-timing-correction/Plan.md) L11~20/L29/L43. 옛0.24초/0:24→0:25, 양 역할 focused PASS/CLOSED. precise offset는 미계측 | M0/R0. 단일 actual Attack/양팀 명시 연결 유지 |
| BattleAxe(5) | 9/19 교정의 명시 expected literal1.02초. 7월1.1667초와 원래1:17(Main 전달)의 관계는 승인 계보를 더 확인해야 함 | [9/19 Plan](../../2026-09-19/23_06_battleaxe-impact-marker-correction/Plan.md) L11/L28~33/L85:1.1667→1.02, 9/20 정밀 수용 제한. 10/11 사용자 개별 수용 Main 인계는 보존하나 원래1:17 재해석 승인으로 확대하지 않음 | M0/R0. [7/16 Plan](../../2026-07-16/18_06_battleaxe-aoe/Plan.md)의 전방 sweep·아군/뒤/범위밖 제외, 동시 AoE 표현 유지; 주기 변경 승인 추가 확인 |
| Tank(6) | 7/11 실제 offset≈0.167초 기록. 원래`0:17`(Main 전달)=17/30초와의 의도 변경 원문 미확보; 현재0.16667을 expected로 채우지 않음 | [7/9 Research](../../2026-07-09/01_12_combat-hit-timing-sync/Research.md) L150~158은 측정 이력이지 새 frame 승인 아님. [10/10 Plan](../../2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Plan.md) L36는 효과 간헐 누락 보고; timing 재승인 아님 | M0/R0. Tank/Cannon 기존 효과 참조·HP는 유지; 실제 projectile authority/endpoint 수용을 이름만으로 선언하지 않음 |
| CannonCart(7) | Tank와 동일하게≈0.167초 옛 실기와 원래`0:17`의 차이 남음. cycle4초 기대의 과거 사용자 원문 추적 필요 | 위 [7/9 Research](../../2026-07-09/01_12_combat-hit-timing-sync/Research.md) L156 및 [10/10 Plan](../../2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Plan.md) L36. 후속 VFX 참조 결정을 타격 frame 변경 승인으로 쓰지 않음 | M0/R0. Tank 공유 preset과 시각 간헐 누락은 별도 evidence |
| FlameSpirit(10) | 최종6hit `0:20,1:05,1:13,1:20,1:27,2:03`=20/30,35/30,43/30,50/30,57/30,63/30초; cycle3초 | [9/28 Plan](../../2026-09-28/21_55_flamespirit-multihit-attack-correction/Plan.md) L3/L35~39. 9/29 사용자 확인1:27이 옛1:28을 대체. 육안 긍정 수용과 회차별 정밀6hit 검증은 별개 | M0/M6/R0. 각 HitIndex/Stop·death 잔여 타격을 합산 PASS로 대체하지 않음 |
| EmberSpirit(11) | marker1.00초=`1:00`, 명시 clip2.6666667초; fallback cooldown2.2초 보존 | [9/28 Plan](../../2026-09-28/21_05_ember-spirit-attack-correction/Plan.md) L12~13/L29~35. 연결 focused PASS/CLOSED, period 재설계 없음. 4/12 Task2:20→2.33 기록은 과거 표기/초 환산 오류 가능성을 별도로 남김 | M0/M6/R0. 첫 Attack 열거 순서 대신 실제 motion 명시 |
| InfernoSpirit(12) | 9월 literal0.50초/cycle3초 교정 후 10/11 사용자 `1:15`=1.5초 재확정은 Main 공용 인계. 최신 숫자 승인 primary Task 위치는 Spirit 감사에서 보강 필요 | [9/21 Plan/Research](../../2026-09-21/02_42_infernospirit-movement-jump-correction/Research.md) §17~18의 이동/방향/VFX 사용자 수용과 10/10 0.5 실행은 다른 날짜다. [10/10 Plan](../../2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Plan.md) L27~31은 당시0.5와1:15 모순, 후속1.5 승인 전 제안 기록 | M0/M6/R0. Research L289~303:mesh X0, SpawnPoint X Blue−0.0085951/Red−0.00859513, Attack−58° 보존. 커밋 source-only VFX/SFX는 Stop 후 보존하고 target/tracer/HP는 닫음 |
| DustSpirit(13) | actual Attack marker literal1.04초·cycle3초; Attack2-first 방지 명시 clip | [9/27 Plan](../../2026-09-27/10_54_dustspirit-explicit-attack-clip-binding/Plan.md) L9~11 및 [Fox Plan](../../2026-09-22/19_48_foxmagician-production-timeline-correction/Plan.md) L444의9/28 당시 conditional. 10/11 사용자 수용은 후속 Main 인계이며 precision 확인과 구분 | M0/R0. Attack/Attack2 순서 독립, source·result/방향 복구 보존 |
| BoulderSpirit(14) | 최종 사용자`1:10`=40/30초·cycle4초 | [9/29 Plan](../../2026-09-29/03_13_boulderspirit-attack-correction/Plan.md) L5/L77~79. 옛1.15초→1:15=1.5 수용→사용자1:10 새 수용, 서로 다른 버전 | M0/R0. Little MOVE4 미재발을 원인 해결로 바꾸지 않음 |
| QuakeSpirit(15) | 9/14 gate expected literal1.667초·단일 impact. 원래 frame 표기와 cycle 숫자 승인 primary는 추가 수집 | [9/14 Plan](../../2026-09-14/00_56_quake-spirit-impact-marker-correction/Plan.md) L3/L11/L145:1.0 placeholder→1.667. 10/11 개별 수용 후속 Main 인계, 당시 precise strike는 conditional | M0/R0. 직접100%+다른 적 유닛/건물 splash50%, original target 제외; 기존 special handler 유지 |
| TideSpirit(16) | 사용자 저장`1:15`=1.5초·cycle3초 | [9/29 Plan](../../2026-09-29/19_09_tidespirit-attack-correction/Plan.md) L5~10/L43~47.1.15초→1.5, focused visual PASS/CLOSED | M0/R0. 설정 정합과 전체 movement/migration 별개 |
| StreamSpirit(17) | 9/22 production marker/HP0.50초. 원래`0:17`(Main 전달)과 관계 미확정; 초값 정렬 수용이 최초frame변경승인인지 분리 | [9/22 Plan](../../2026-09-22/12_13_streamspirit-production-timeline-correction/Plan.md) L9~12/L163~166. Legacy VFX 보존 focused PASS, marker→timer 실제차이도 기록됨. 0.5=0:15 환산을 사용자0:17 승인 변경으로 만들지 않음 | M0/R0. Unresolved/LegacyFallback 유지, projectile flight/impact exact-key는 완료 아님 |
| TorrentSpirit(18) | 사용자 launch`2:10`=70/30초·cycle4초; travel0.5초는 launch marker와 다름. HP/heal은 각 contact 시각 | [9/30 Plan](../../2026-09-30/23_19_torrentspirit-attack-timing-correction/Plan.md) L9~10/§6 및 [10/2 Plan](../../2026-10-02/23_45_common-recovery-and-torrent-launch-correction/Plan.md) L90~98. 0.5 임시launch→2:10; lost-primary launch 예외/기존 contact 결과 수용은 제한된 match 범위 | M0/R0. living original target의 current aim/상실 시 last-valid aim, current caster origin, prelaunch source death/gameend 취소. VFX가 stationary라는 옛 문장은 후속 이동 구현과 대조해야 함 |
| BearGuard(20) | 사용자 marker`0:13`=13/30초. 옛 Task는 clip`1:20`≈1.6666666초와 config cooldown1.2초를 **둘 다 보존**하며 cycle 결정 OPEN | [9/29 Plan](../../2026-09-29/23_28_bearguard-attack-timing-correction/Plan.md) L3/L40~55. focused0:13 사용자 수용은 주기1.2→clip.length 승인 여부를 확정하지 않음. 4/12 1:20→1.33초 표기도 과거 환산 기록 | M0/M6/R0. precise visual/전체 movement는 별개 |
| FoxMagician(21) | 의도예외: charge marker1.00초/HP2.25초/cycle4초. 기본marker로 HP 덮어쓰기 금지 | [9/22 Plan](../../2026-09-22/19_48_foxmagician-production-timeline-correction/Plan.md) §16 L337~396의2.25 복원/Factory 양경로 예외 및 §21 L438~444의9/28 사용자 현행수용. 이전2.25→1.0 가설은 철회. 새 SOLO 시각 우려는 별도 OPEN | M0/M6/R0. default-key writer immediate·source charge와 HP 분리, projectile 재튜닝 후속; VFX 종료/동일 공격 exact correlation 미측정 |
| LionKnight(22) | 사용자최종2hit`0:18,1:13`=.6,43/30초; 옛 Task는 cycle3초를 보존 | [9/30 Plan](../../2026-09-30/01_21_lionknight-attack-timing-correction/Plan.md) L3~5/L12/L40~42. 옛0.22/1.08초 및0:22/1:08 문구와 새frame를 분리. current clip2초(Main 전달)는 과거3초 보존 결정과 대조 대상 | M0/M6/R0. closed focused 수용과 C3 expiry7 별도 OPEN |
| RhinoBreaker(23) | 전용Task expected actual Attack literal1.05초. 원래`1:05`(Main 전달)=35/30초와 승인 계보 불명 | [9/13 Research](../../2026-09-13/20_34_rhino-breaker-impact-marker-correction/Research.md) L16~19/L35~40/L53~68:Attack2의1.05를 actual Attack에 주입. 10/11 개별 수용 Main 인계는 보존하지만 정확한frame재해석 승인과 구분 | M0/R0. actual Attack state만 교정/Attack2 보존, building/후속 target 전환·full scope와 분리 |
| EagleArcher(24) | 전용 최종 timing 변경의 primary 근거 미확보. 현재0.1/clip1 또는Attack2 .7 값을 expected로 대입하지 않음 | [4/28 integration Plan](../../2026-04/2026-04-28/14_00_new-asset-game-integration/Plan.md)와 [6/5 prefab Plan](../../2026-06-05/22_14_unit-prefab-component-setup/Plan.md) L59은 등록/구조 근거. 10/11 사용자 개별 테스트 수용 Main 인계는 유효; exact marker/cycle 변경 승인 원문은 이름별 부족 항목 | M0/R0. 전용Task 부재나 MigrationRequired로 미테스트 재분류 금지. 개별 modelYaw/VFX endpoint 숫자 primary 미확보 |
| RabbitTrickster(25) | 최종사용자`0:20`=20/30초·cycle2초 | [9/28 Plan](../../2026-09-28/10_03_rabbittrickster-explicit-attack-clip-binding/Plan.md) L72~87. 원래0:18 및 old literal0.18초 버전과 분리; 새0:20 저장·사용자수용이 latest | M0/R0. 명시Attack, alternate-first gate, 최종 focused수용과precision 별개 |
| MushroomBomber(26) | marker1초/cycle3초, 직접주타깃+별도3초DoT. marker/tracer/impact/poison 종료는 서로 다름 | [10/9 Research](../../2026-10-09/08_12_mushroombomber-vfx-split/Research.md) L11~13/L23~30 및 [7/19 Plan](../../2026-07-19/01_42_mushroombomber-impact-dot/Plan.md) L25~35 사용자 설계. 10/10 source freeze는 visual수용 아님; timing 최초1초 사용자지정 원문 추가 필요 | M0/R0. immutable attack due impact1회, tracer callback HP/queue 비구동, recipient Damage record sole owner/refresh noRestart·drain vsforced teardown 구분 |
| BloomFairy(27) | marker`1:00`/prep1+발동후cooldown3=명목cycle4초, HoT200/3; clip3→4 승인retime, healer clip.length로cooldown 덮기금지 | [10/3 Plan](../../2026-10-03/03_28_bloomfairy-heal-timing-correction/Plan.md) L254~273/§12 및 §23 L735~743. 후반1~3→1~4 retime·marker전반보존·heal blend.15시험값, 실제postwait3·duration3 scoped수용과 전체visualOPEN 분리 | M0/R0. gradual server facing/future-start wait/연속heal noHeld, HoT writer와 VFX current owner/Drain 별도. 현재clip4를 옛3에 대한 임의불일치로 판정하지 않음 |

**수집 결과의 수용 경계:** 10/11 Rhino/Quake/Battle/Dust/Little/Eagle 개별 사용자 수용은 이번 Main 공용 인계와 기존 실수 사건의 사용자 정정 기록에 있다. 개별 수용을 무효화하지 않지만 exact offset/frame의 primary 변경 승인까지 역산하지 않는다. Task가 conditional로 남아 있는 날짜와 후속 사용자 수용 날짜를 각각 표시한다. 현재 clip·config·Factory가 동일하다는 이유로 이 표의 불명확한 원래frame계약을 닫지 않는다. 10/13 「현재 구현 기준」 문서 정정은 현황 mirror이지 historical correctness oracle가 아니다.

### 현재 보존 여부의 한정된 Main 증거 — 최종 per-unit 판정 아님

Main이 read-only `git diff --stat b65751c6`로 `Assets/_Project/Animations/Units`, `Assets/_Project/Resources/Config/UnitStatsConfig.asset`, `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs`, `Assets/_Project/Prefabs/Units`를 비교한 출력은 EMPTY라는 인계를 받았다. 따라서 **이 정확한 경로들은 final10/12 combatcommit 이후 rollback 차이가 없다는 보존 근거**다. 원래 수정/프레임환산이 옳았다는 증명은 아니다. Game.unity의16줄 변경은 당시 Main 검토 진행 중이며 이 조사에서 source/asset 수정하지 않는다.

Main 전달 현재 clips는 Little .25/1.15, Battle1.02, Rhino1.05, Stream.5, Tank/Cannon.16667, Rabbit.66667, Bear clip1.6666666, Lion clip2, Bloom clip4다. 이 전달값의 실측범위/최종 per-unit source wiring은 담당 감사표의 Main 검토 후 기록하며, **현재 숫자를 expected로 복제하지 않는다**. Rabbit/Bloom은 실제 후속 승인으로 original값이 바뀐 사례다. Little/Tank/Cannon/Stream/Rhino의 원래frame와literal 값·Bear preserved cooldown·Lion3초 보존은 위 named 부족 근거를 해소해야 한다. 최종 비교 결론을 대기하되 runtime을 필수로 요구해 historical 감사를 막지 않는다. 지금은 수정 제안/구현 허가가 아니라 보고다.

### Main 독립 추출25종 marker vs historical 초안 — 현재 코드 교정 제안 아님

현재 열은 Main이 실제 clip time을 독립 추출해 전달한 값(초)이다. 일부는 인계에서 반올림됐으며 이 문서 담당의 자산 재실측은 아니다. 위 historical 표의 원본을 비교 기준으로 삼는다. **A축:10/12 이후 지정경로 보존**, **B축:옛 Task literal 값/후속 승인과 현재값 대조**, **C축:원래 ss:ff 사용자 의도 확인**을 나눈다. B축 숫자가 같아도 C축 미확정이 없어지지 않는다. clip event·config fallback·Factory 실효값·실제 HP/화면은 별도 관측이며 이 표는 marker 비교다.

| 유닛(ID) | 현재 Main 추출 marker(초) | historical 대조 결과/남은 named 근거 |
|---|---|---|
| Pistoleer(0) | .8 | 옛0:24/Task .8 일치; re-entry/source-owner 보존의 현재 full wiring은 담당 검토 별도 |
| Assault(1) | .166667 | 사용자0:05 후속 결정과 일치; old0:17/0:33을 다시 expected로 쓰지 않음 |
| Sniper(2) | 1.7333 | 사용자1:22 후속 결정과 일치; 반올림 값으로 exact float/pixel을 판정하지 않음 |
| LittleKnight(3) | .25/1.15 | 7/11·8/31 Task literal 값과 동일. original0:25/1:15의 해석·변경 승인 primary 부족은 남음 |
| SpearMan(4) | .8333 | 최종0:25 후속 사용자 수용과 일치; old0.24/0:24로 되돌리지 않음 |
| BattleAxe(5) | 1.02 | 옛 전용 Task explicit1.02 artifact와 동일, merge loss 아님. original1:17 의도/변경 계보는 미확정 |
| Tank(6) | .16667 | 옛7/11 실행값≈.167과 동일. original0:17 의도의 확정/변경 승인 primary 없음; 현재값을 정답으로 선언하지 않음 |
| CannonCart(7) | .16667 | Tank와 같은 original0:17 vs old literal 문제. laterVFX참조 수용은 frame변경 승인이 아님 |
| FlameSpirit(10) | .666667/1.166667/1.433333/1.666667/1.9/2.1 | 최종6frames0:20/1:05/1:13/1:20/1:27/2:03과 일치. 옛1:28이 아니라 후속1:27 계약 |
| EmberSpirit(11) | 1 | 전용 Task explicit1.00과 일치. marker일치와 clip-derived cycle/fallback2.2 보존은 구분 |
| InfernoSpirit(12) | 1.5 | Main 전달10/11 사용자1:15 재확정과 일치. old0.5와 다른 승인 날짜이며 최신 primary Task 위치 보강 항목은 유지 |
| DustSpirit(13) | 1.04 | 옛 Task explicit1.04 artifact와 동일. 원래 frame 의미를 새로 자동 환산하지 않음 |
| BoulderSpirit(14) | 1.333333 | 최종1:10 사용자 수용과 일치; old1.15초/후속1:15=1.5와 버전 분리 |
| QuakeSpirit(15) | 1.667 | 전용 Task explicit1.667과 동일. literal 반올림 계약과 원래 정확한frame 의도는 구분 |
| TideSpirit(16) | 1.5 | 사용자1:15 저장·focused 수용과 일치 |
| StreamSpirit(17) | .5 | old Task explicit.50 artifact와 동일, merge loss 아님. original0:17 의미/승인계보는 미확정 |
| TorrentSpirit(18) | 2.333333 | 사용자launch2:10와 일치; travel/contact HP 시각은 이 marker와 동일한 값이 아님 |
| BearGuard(20) | .43333 | 최종0:13 marker와 일치. 현재clip1.6666666 vs oldTask config1.2 보존/주기OPEN은 marker 판정과 별개 |
| FoxMagician(21) | event1 / damage config2.25 | 옛 Plan§16/§21 의도예외와 일치. event=HP 단일화 금지. 새 SOLO vs 이전buildMulti 시각 우려 OPEN |
| LionKnight(22) | .6/1.43333 | 최종0:18/1:13과 일치. 현재clip2 vs oldTask3초 보존은 cycle/Factory 계보 검토가 필요한 별도 항목 |
| RhinoBreaker(23) | 1.05 | old Task explicit1.05 artifact와 동일, merge loss 아님. original1:05 의미/변경승인은 별도 미확정 |
| EagleArcher(24) | Attack.1 / Attack2 event없음 | historical exact marker/cycle 변경 승인 primary 미확보. 현재.1은 expected를 만드는 근거가 아니며 개별 사용자 수용도 취소하지 않음 |
| RabbitTrickster(25) | .666667 | 최종사용자0:20 저장/수용과 일치. original0:18은 새 결정으로 superseded됨 |
| MushroomBomber(26) | 1 | 옛source-freeze marker1과 일치. 최초 timing 지정·VFX/DoT 수용 범위는 별도 primary 추적 항목 |
| BloomFairy(27) | 1 / clip stop4 | 승인된marker1/후반clip3→4 retime과 일치. configpost3 유지·명목cycle4를 clip3 기대값과 혼동하지 않음 |

**초안 결론:** 지금 입증된 보존은 정확한10/12 비교 경로에 한정된다. Axe/Dust/Rhino/Stream의 explicit decimal은 이미 옛 Task에 있던 승인된 artifact 값이지 이번 merge에서 새로 생긴 차이가 아니다. 원래 Stats frame 의미가 그 당시 적절히 확인됐는지는 여전히 별도 질문이다. Rabbit/Boulder/Flame/Bloom/Fox에는 후속 결정·예외가 실제 원문에 있어 original값으로 단순 rollback 판정을 할 수 없다. **「25종 모두 정확하다」는 결론 없음.** 모델 yaw/import 수치·VFX delivery/recovery의 전 유닛 현재 대조 및 부족한 primary 승인은 담당 결과의 Main 검토로 보강하며, runtime을 기다리는 항목으로 숨기지 않는다. 상시 규칙/Stats를 현재에 맞춰 고쳐 이 차이를 없애지 않는다.

- 2026-10-14 10:04는 Main이 측정해 전달한 작업 기준 시각이다. 사용자 후속 「기존 Task 갱신」과 SOLO impact timing 구현 요청을 명시 승인으로 받았다. 같은 Task를 유지하며 document-manager는 이 Research와 [Plan](Plan.md), 별도 승인된 `.claude/mistakes.md` 사건의 목차·본문 추가만 편집한다. 광산·랠리는 제외한다.
- **최신 사용자 확인: 재현 환경은 SINGLEPLAYER(싱글플레이).** SOLO 분기 분석이 우선이다. 멀티 B3 보존은 SOLO 실행 경로의 안전성 증명이 아니다.
- 기존 pursuit 적용·Main 독립 검토 및 타이밍 후속 소스5/CLR 근거는 유지한다. 현재 FoxMagician(type21) SOLO 실제 로그 focused 분석 인계를 완료했으나 사용자 시각 reportedMismatch/수용/원인은 OPEN이다. 비교 멀티는 이전 빌드이며 현재 빌드의 동일 조건 peer 비교가 아니다. 한 종족 근접이 괜찮다는 제한 관측은 전체 PASS가 아니다. 에이전트 native 메뉴·빌드는 NOT RUN, CLR 결과도 실기 시각 PASS가 아니다.
- 감사 기준: fetch된 `origin/main=88f60e38cf9f7a8dce0c8cc498fe89dfd09f06b6`, final 교정 비교 `0de4f868`, 원래 비교 `b65751c6`. 현행 checkout은 `codex/unit-movement-attack-sync-audit`이며 보존 커밋 `532e2033`까지 FF 이전 완료다(2026-10-14 Main 인계 및 문서 담당 ref 확인; 이전 checkout은 `codex/docs-checker-maintenance`). 이것은 새로운 SOLO 복원 patch 완료를 뜻하지 않는다.
- 기존 dirty 검사기·Skills 규칙·다른 Task·tests·삭제된 meta는 보호한다. programmer 후속 승인 범위는 SOLO impact timing/targeted bounded 진단이며 경로·네트워크·Stats·클립·프리팹·씬은 제외한다. 문서 담당은 소스/에셋/메모리·다른 Task를 쓰지 않고 fetch/merge/checkout/bisect/build/Unity/원시 Editor.log 접근·TC/QA도 하지 않는다.

### 2026-10-14 17:28 후속 부분 테스트 — 사용자 보고와 focused 분석 완료

사용자 후속 명확화: **한 종족 근접은 괜찮다는 제한 관측이며 FoxMagician(type21)의 SOLO 타이밍이 멀티보다 이르게 보인다. 비교 멀티는 이전 빌드다.** type21은 Main UnitType.cs 확인 근거다. 기존 charge1.0초/HP2.25초 의도 계약과 Factory 예외는 유지하지만 사용자 비교를 틀림/정상으로 기각하지 않는다. focused 분석은 아래와 같이 완료됐고 시각 timing reportedMismatch·원인·수용은 OPEN이다. 앞선 CLR GREEN/소스 수용과 이번 실제 timer 관측은 human visual PASS가 아니다.

Main StageUAS 원본 경로는 `Assets/_Project/Docs/_Logs/_editor/2026-10-14/RuntimeLog.txt`다. 분석은 [고정 스냅샷](../../../../../../Temp/solo-timing/Solo1728.snapshot.txt) 기준 SHA256 `CFFE86D0D96FA4B073ABBB02888CE16EBCF65FB62F077F355EF8D27FCDC120F0`이다. 크기958969bytes/2425행은 programmer 전달값이며 Main 크기 재계수 아님. 문서 담당은 원시 로그를 읽거나 독립 계수하지 않았고 아래 Main 독립 확인과 programmer 전달 근거를 분리한다. live-read 재발 기록은 mistakes 기존 사건에 추가 완료했으며 게임 원인 증거가 아니다. 아래 NOT RUN 표기는 구현 인계 당시 이력이고 현재 실제 부분 테스트는 이 절을 따른다.

| 관측 | 근거 출처·범위 |
|---|---|
| Fox25기, commit205, HP dispatch71 전부 StatusApplied, native201 | Main의 고정 입력 File.ReadLines 독립 재계수. dispatch Late 최소0.025999149670497ms/최대9.10580357142976ms/음수0 |
| 모든71의 nominal `anchor+4×loop+2.25` | programmer source/trace 검증 전달. Main 전체 exact native-marker/HP join 검증이 아님 |
| Unit3 Epoch1 제한 직접 확인 | Main bounded raw 확인: anchor53.52557466695, charge marker1초 이벤트54.53735957341, HP due55.77557466695→actual55.7825467687, Late6.972101757ms. 한 표본을 전체 native/pixel 연계로 확대하지 않음 |
| 큐64 received/emitted 쌍, ImmediateTrue·same-frame·text played, 최대1.779ms | 전체 값은 programmer 전달; Main은 raw2쌍만 직접 확인. 71과64를 같은 모집단 또는 유실 차이로 간주하지 않음 |
| coverage 상한 | Fox owner9개 cap·text cap128은 programmer 전달. commit205−dispatch71은 취소/사망/관측 범위 차이가 있어 실패 수가 아님 |

Sidecar profile와 key-generator 추적에서 Fox는 Unresolved/CanRunSequencer=false라 token/authorization에 들어가지 못해 result key가 default가 되고 Fox immediate=true로 흐른다는 programmer 근거다. 이 immediate 분기는 **SOLO와 멀티 양쪽**이며 SOLO-only 즉시 표기 원인이라고 하지 않는다. 해당 경로에 Coordinator100ms delay를 적용해 비교하지 않는다. SOLO HP 로컬 writer와 멀티 ClientRPC 수신은 실제 branch 차이지만 현재 멀티 trace의 수신 지연/화면 비교는 확인되지 않았다.

현재 관측된71 HP dispatch는 승인된2.25초 기준보다 이르지 않다. 이는 관측 timer에 한정된 사실이지 사용자의 이전 빌드 멀티 대비 시각 우려가 잘못됐다는 결론이 아니다. 새 root cause/추가 교정은 선택하지 않았다. 교정 선택에 부족한 근거는 **현재 빌드의 focused Fox 멀티 trace**와 같은 기준의 시각 연결이며 전체 유닛 재테스트 요구가 아니다. programmer는 source5 해시 불변을 보고했다. 이번 focused 분석/문서 반영에서 source/config/build/Unity 변경은 없고 기존 CLR·실제 timer 관측·human visual 수용은 계속 분리한다.

### 타이밍 후속의 구현 전 production CLR 기준선 — 당시 측정 이력

Main은 Probe.cs/exe/runtime assembly를 실행 전후 frozen 확인해 독립 실행 exit 0을 받았다. 입력은 120초·주기2초의 일반 Spear production 경로이며 실제 TryAttack/TickCooldowns/PendingHits/HP 적용을 사용한다(Main 전달 실행 근거; 문서 담당 재실행 아님). [Probe.cs](../../../../../../Temp/solo-timing/Probe.cs)·[RunProbe.ps1](../../../../../../Temp/solo-timing/RunProbe.ps1)은 ignored 로컬 재현 자료다.

| 입력 | final start drift(ms) | due drift/error(ms) |
|---|---:|---:|
| 30fps | 1933.339484 | 1933.339561 |
| 60fps | 966.672767 | 983.339511 |
| 45fps | 0.002857 | 11.114022 |
| irregular 30fps | 1933.332894 | 1966.666257 |

값은 Main 독립 실행 전달값이다. HP event count와 실제 HP 감소는 정확히 일치했다는 확인이며 수치 목록이 없는 aggregate를 새로 만들지 않는다. 실행의 native Time warning은 인쇄됐지만 일반 Spear 타격 경로는 TorrentTime을 통과하지 않는다는 검증 범위를 유지한다. 이 측정은 결정적 CLR scheduling 근거이지 native Animator·사용자 경기·pixel 타격의 재현/수용이 아니다. Factory 최종 cooldown/marker 덮어쓰기 확인 및 config-vs-clip 가설 철회는 유지한다.

당시 Main 승인 계약은 continuous epoch/미래 occurrence/captured target·sequence의 결합과 finite old-hit 소유권·최대1new commit/unit/frame이었다. 이후 구현·최종 manifest/검증은 다음 절에 기록 완료했다. 이 기준선의 Main 실행은 drift/HP 측정 exit0이며 독립 RED assertion 실행이 아니다. Temp Probe의 현재 파일/exe는 GREEN 세대로 바뀌었으므로 위 링크를 보존된 baseline binary로 간주하지 않는다. 기존 pursuit 근거를 timing 검증으로 재분류하지 않는다.

### 최종 타이밍 적용 및 검증 — Main 독립 확인 완료

[최종 Receipt](../../../../../../Temp/solo-timing/Receipt.md)를 전문 읽어 실행 근거로 사용했다(새 규칙 원본 아님). 실제 변경은 아래 기존 소스 5개이며 Main이 source hooks/authority ownership을 직접 검토하고 최종 해시 전후 일치를 확인했다.

| 적용 경로 | SHA256 |
|---|---|
| `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackPresentationPolicy.cs` | `B534BC0AA79D18FDEFBA13B405113B25B0BF869B8834A14EE6921F2CC589FA76` |
| `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` | `242F14CDBAF04B47E432E13CE7321A27563DEB5403BD81F3E7FB4C17FD281734` |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | `6427105CB972B868F9978EF02932EEC49AC99DDC0D999BA9764D46C9C20BA933` |
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | `C02D66CDF38AA09E87521B0CDFE59F3DBB90D0436129A593BFB46749DAF02FB2` |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | `4A7D92F4F22F3E0D32F89944162CE9D17C085BDF1C5144340CD8834D9C0459A9` |

positive sorted offset(중복 포함)의 절대 미래 occurrence만 새 clock을 사용한다. ZERO/invalid/unsorted offset은 변경 없는 Legacy fallback이며 zero 교정을 주장하지 않는다. Torrent/Healer는 밖이다. Bootstrap nonNetwork 블록은 동일 SoloNow를 cooldown/pending에 전달하는 변경만이며 다른 stage 순서 변경은 없다. retarget은 같은 playback anchor를 유지하고 새 playback은 새 epoch다. 이미 커밋된 hit의 captured target/due는 CC/Stop/commanded pause/close에서 유지한다. alive/current Domain에 대한 View disable/rebind는 취소 권위가 아니며 Domain death/removal/replacement가 owner를 무효화한다. 기존 ApplyAttackDamageCore/AppliedDamageObservation을 사용하고 새 range/facing/CC 피해 제약을 넣지 않았다.

| 독립/원본 증거 | 결과 및 한계 |
|---|---|
| programmer 원본 RED | tool `0d1016`/process `68173`, exit1. 반복 occurrence drift 한 frame 초과 assertion 실패. RED binary는 보존되지 않았고 현재 Probe.exe는 GREEN. Main 독립 RED 주장 없음 |
| Main 새 Compile·회귀 | command `67b91c`→completion `e8da5b`, 모두 exit0. Runtime303/appended15/omitted0/errors0, Editor32/project14/errors0, Release303/errors0/warnings27. Runtime23/Editor0 warnings는 Receipt의 새 compile 기록. 기존 pursuit GREEN 뒤 실제 새 clock/HP Probe와 private 실제 authority core 실행 |
| Main 전체 frozen 재실행 | `ca720e` exit0, `FullManifestAndArtifactsFrozen=True`. 전체5source+Probe.cs/exe+Runtime/Editor DLL 실행 전후 고정. 앞선 compile/run도 pinnedsource4+기존pursuit+Probe.cs/RunProbe 불변 확인 |
| 실제 실행한 보존 assertion | captured-target/old pending HP, CC/Stop pending 유지, pause는 미래만 이동, alive View close HP 유지, Domain death/removal/reuse/target replacement, Legacy zero cadence, client role/null stats/nextId 복원; 실제 production authority core 실행이며 native Editor entry는 아님 |
| 보호·범위 | Main 보호4(Skills/checker/Stats/Game) 해시 및 기존 UnitMovementContracts `0FF35145…33958` 불변. 광산/랠리/경로/네트워크/config/asset 변경 없음. 기존 Factory 가설 철회 유지 |
| 문서·수용 | Main 최종 검사 completion `b1cb09` 실제 exit1, 동일한 기존 참조6건·신규 실패 없음·메모리 고아/감소 없음. Task는 검사 제외이며 이 상태 줄 갱신에 추가 검사 없음. native 메뉴/빌드/실제 경기 NOT RUN. first blend·native frame exact/pixel·원래 사용자 증상의 인과는 OPEN |

| 120초 교정 후 CLR 입력 | final due lateness(ms) | final start drift(ms) | HP events/실제 감소 |
|---|---:|---:|---:|
| 60fps | 0.006231 | 0.006154 | 60/60 |
| 30fps | 0.006231 | 0.006154 | 60/60 |
| 45fps | 11.114022 | 0.002857 | 60/60 |
| irregular30fps | 33.332931 | 23.332901 | 60/60 |

Main 독립 전달값이며 각 lateness는 실제 최대 frame interval 안이다. native Animator 정밀도/실기 픽셀 동시성을 증명하지 않는다. actual Start callback은 ConfirmSoloAttackPlayback을 호출했고 loop당 Start1/retarget0; 별도 retarget assertion과 일반 loop 관측을 혼동하지 않는다.

`[UAS-SOLO-TIME]`는 실제 owner당 detail64+cap1+Domain terminal1이고 cap 후 incomplete를 명시한다. 실제 최대 UTF-8 body370/GameLog.Compose line465는 production preflight600/envelope 예산780 이하다(최악 runtime 상한 실측 주장 아님). dispatch는 captured Epoch/Sequence/Loop/Hit/Target/Due와 실제 Now/LateMs/AppliedAmount/Status/HP를 기록한다. native event state/clip 및 fallback-current-state는 read-only metadata이고 `ReservationJoin=unassigned`다. full native HP join을 만들거나 metadata로 HP/clock을 구동하지 않는다. native marker observer/인자 구성은 Editor/Development guard 안이다.

중간 View lifecycle 취소 오류는 Main source 검토에서 최종 전 수정됐고 실제 alive View-close HP 보존 및 Domain lifecycle CLR assertion을 통과했다. 별도 허용된 `.claude/mistakes.md` 목차/사건 기록만 추가하며 새 C3 범위·게임 규칙·실기 발생을 주장하지 않는다. 문서 담당 변경 목록은 Research/Plan과 이 사건 추가뿐이다.

### 기존 pursuit 구현 증거 경계

승인된 pure SOLO step helper(remaining distance clamp/actual reached), UnitView SOLO Legacy pursuit의 정확히 한 checkpoint 소비와 bounded GameLog.Dev 진단, 기존 validation 회귀 보강은 적용·소스 인계 검토 완료다. DomainProcessStep·Legacy→Reducer 전환·멀티·에셋·일반 광산/랠리/타이밍 교정은 하지 않았다. Factory 확인과 config-vs-clip 가설 철회는 유지한다. 소스 적용 수용은 runtime 수용이 아니다.

### 최종 적용 manifest 및 독립 검증 — 기존 pursuit 범위

소스 변경은 아래 기존 3파일만이며 Main 전달 diff는 +310/-6이다. [로컬 Receipt](../../../../../../Temp/solo-pursuit/Receipt.md)는 구현·검사 수령 근거이며 공개 게시/원시 로그 분석을 하지 않았다. SHA256은 Main이 frozen 실행 전후 모두 일치 확인한 값이다.

| 실제 적용 파일 | 최종 SHA256 |
|---|---|
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | `7656CF29710CCC48ECEDC2E9861C1EF250B91369678468F31BC90E53F18CA1BA` |
| `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitMovementContracts.cs` | `0FF35145D397E96138E8A10040628511A05BEE36E1A6BD1893A7073B03033958` |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | `63FC97A66E6B59ACFA94F26F6E033353D106395BEA13DAE06844908244690CB5` |

| 근거 축 | 최종 결과·한계 |
|---|---|
| programmer 원본 RED | helper의 이전 unclamped candidate/missing arrival로 첫 비최종 waypoint 소비 assertion 실패, 실제 exit 1; Receipt tool `a11046`/process `59583`. 현재 exe는 GREEN이며 RED binary 보존·Main 독립 RED·Unity 증상 재현은 아님 |
| 최종 helper 회귀 | programmer GREEN exit 0 및 Main frozen 독립 RunRegression GREEN exit 0. 실제 production helper/checkpoint/validation 추출 범위; Unity/native 원래 증상 재현은 아님 |
| Main 독립 정적 컴파일 | Compile exit 0. Runtime sources303/project303/appended15/omitted0/errors0/warnings23. Editor sources32(project14+Assets/Editor18)/appended0/omitted0/errors0/warnings0. 현재 Unity EditorMode rsp 기반 정적 축이며 Unity native 실행은 아님 |
| 문서 검사 | Main 최종 실제 exit 1, 아래 기존 참조 6건과 정확히 동일; 신규 실패 없음. Task 제외/수동 링크 확인 경계 유지 |
| 보호·적용 위치 | Main Skills/checker/Stats/Game 보호 해시 불변. 다른 코드·에셋 변경 없음. 현재 `codex/docs-checker-maintenance`, main은 fetch refs만이며 merge/push 없음 |
| 미실행·미수용 | native/빌드/실제 경기 NOT RUN. 전체 증상 해결·Root/domain 일치·runtime 수용은 미확정 |

진단은 `[UAS-SOLO-PURSUIT]` begin/repath/consume/attack/close/stall/root-domain 차이/cap 보고다. UnitData lifetime당 detail64+최초 cap1+terminal1, episode당 stall 최대2(2초 무진전 뒤, 실제 root 진행 시 clock reset). detail UTF-8 600 bytes+envelope 예약180의 preflight로 초과를 FormatRejected/incomplete로 표시하며 정상처럼 truncate하지 않는다. cap/거부 계수는 terminal까지 유지한다. Initialize의 참조 재사용 때 lifetime 계수를 reset하고 OnDisable에서 종료한다. 매 frame raw 출력은 없으며 DomainProcessStep을 추가하지 않아 Root/domain 차이는 여전히 후속 범위다.

**2026-10-14 helper 실행 증거 정정(Main 전달):** Main은 실제 GREEN 실행/exit 0만 독립 확인했다. script/model의 과거 RED 단계 읽기를 실제 RED 실행으로 잘못 전달한 초기 보고는 즉시 정정·철회됐다. Main 독립 RED 또는 독립 RED→GREEN을 주장하지 않는다. programmer 원본 RED와 최종 manifest/검사 범위는 위 표로 인계 완료됐으며, 한정된 알고리즘 실행과 Unity 원래 증상/실기 수용은 구분한다.

## 증상별 증거 경계

| 증상 | 사용자 관측 보고 | 정적으로 아는 것 | 재현·추가 증거 필요 |
|---|---|---|---|
| 근접 공격 불발 | SOLO에서 공격이 나가지 않음 | 근접 판정은 같은 타겟의 생존·거리·방향·회차 조건을 구분해야 함 | 어떤 유닛/타겟인지, 진입·예약·marker·HP 중 끊긴 단계 |
| 광산 통과 | SOLO 유닛이 광산을 통과 | 광산은 영구 이동 불가; 건설 불가 구역의 이동 허용과 다른 계약 | domain MineKind/walkability, 실제 이동 선분, 표시 위치 대조 |
| 공격 후 제자리 걷기 | 짧은 stationary Walk | 위치 정지와 표현 정지는 별도; 일반 Held와 공격 handoff도 구분 | SOLO Recovery→재개 경로, 해당 frame의 실제 이동·Animator 상태 |
| 공격 타이밍 어긋남 | 모션과 타격 시점 불일치 | 서버 판정 시각과 로컬 marker는 별도; 애니메이션 동일성은 실행 시계 동일성이 아님 | SOLO loop/cooldown/AttackSpeed/직접 marker 경로와 HP 시각 |

## 초기 비교 사실 — Main 전달, 범위 한정

다음은 2026-10-14 Main의 전체 경로 비교 보고다. 이 문서 작성자는 `origin/main` 해시와 UnitCombat diff를 직접 확인했으며, 나머지 동일성은 Main 전달 근거로 표시한다. 집계 수치는 독립 재계수 전 복제하지 않는다.

- `0de4f868→origin/main`: 전체 `*.anim`·`*.controller` 및 Unit 프리팹 diff가 없다는 Main 보고. 씬 Bootstrap 유닛 config 참조 변경은 발견되지 않았다는 보고다.
- `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs`, `Presentation/Unit/UnitView.cs`, `Infrastructure/Network/NetworkCombatController.cs`, UnitStatsConfig, TorrentLaunch/WavePresentation은 Main의 전체 blob 비교에서 동일. 축약 표기의 실제 전체 경로 목록은 programmer 인계 후 확정한다.
- 직접 확인: `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` diff는 HoT 텍스트 규칙 인용 주석 변경뿐이다. 이것을 전체 코드 무변경으로 확대하지 않는다.
- Main 보고: `Game.unity` diff는 텍스트 비움, `_autoReturnSeconds` 30→60, Disconnect 30000→60000. GameConfig는 FlatTop 10×20→11×21 및 test-gold 필드 제거. 변경 프리팹은 NetworkMapTransfer·ConfirmPopup·Bloom/Mushroom VFX 범위다.
- Map bootstrap/projection/gridRenderer/network-map 경로에는 변경이 있다. 동일 유닛 코드라도 다른 맵 입력·연결·SOLO 분기를 소비할 수 있다. 이전 교정 소실 주장은 파일 전체 diff/blob와 caller 연결을 확인하기 전 확정하지 않는다.

## 우선순위 조사 항목 — 원인 확정 아님

1. **SOLO 실행 분기:** `NetworkContext.ActiveUnitMovementPipelineMode` getter 내부에서 `MovementPipeline.IsMatchActive` latch가 false이면 Legacy를 반환하는 경로와, `UnitView.Initialize`가 `!NetworkActive`일 때 명시적으로 Legacy를 선택하는 경로를 구분한다(Main 정정 전달). latch를 NetworkContext의 static property로 취급하지 않는다. Legacy AStar/FlowField/Chase와 공격 loop의 실제 caller를 추적하며, 멀티 B3 보존으로 SOLO corridor 안전을 주장하지 않는다.
2. **광산과 이동 입력:** 맵 projection의 MineKind→walkability 및 SOLO 이동 선분 검사를 비교한다. 가설: 안전 검사를 우회하는 직선 이동이 있으면 A* 경로가 광산을 피하더라도 실제 root가 관통할 수 있다. 반증: 실제 사용된 모든 선분 경로에서 동일 차단 검사가 보장되는지 확인.
3. **근접 진입·재개:** SOLO Chase→attackLoop 및 Recovery→AStar/FlowField 전환의 guard·target·취소 순서를 확인한다. 코드에서 가능한 실패 조건과 실제 발화는 구분한다.
4. **SOLO 시간축:** cooldown, AnimationAttackSpeed, direct marker와 authoritative-marker 비활성화 조건을 같이 확인한다. 가설: scheduler와 clip 진행의 서로 다른 시간 원점/배율이면 불일치가 가능하다. 반증에는 같은 공격의 예약·marker·HP 시각이 필요하다.

위 목록은 초안 시점의 조사 항목이다. **2026-10-14 최종 인계 수신으로 감사 대기는 종료했으며, 아래 최종 근거가 코드 감사 판정이다.** 당시 새 로그 추가는 미승인이었고, 이후 승인된 진단 범위는 상단 「현재 구현 증거 경계」를 따른다.

### Main 추가 SOLO 코드 근거 — 전달값, 후보이지 확정 버그 아님

아래 행은 Main이 읽은 `88f60e38:Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` 기준이다. 문서 담당의 독립 재독과 변경 커밋 판정은 대기한다.

- `Initialize:1601`에서 NetworkActive가 아니면 Legacy 모드를 선택한다.
- `ApplyMovementAuthorityFrame:2083`의 Legacy 분기는 candidate를 즉시 `transform.position`에 적용한다. spatial corridor preflight는 그 뒤 Reducer 분기에 있다. 이 함수의 검사 분리 사실은 caller가 이미 candidate를 검증했는지까지 설명하지 않는다. 광산 통과 후보 판정에는 AStar/FlowField/Chase의 candidate 생성·사전 검사를 이어 읽어야 한다.
- `EnterCombatLoopV3:5623` SOLO 경로는 TryAttack의 pending-hit delay로 예약한다. 같은 타겟에서는 첫 Start event만 있으며 매 cycle native epoch는 없다는 Main 보고다. `StartCombatAnimation`은 TransferToAction과 snap rotation을 한다. scheduler·clip/AttackSpeed·direct marker의 반복 주기가 실제로 어긋나는지는 미확정이다.

이 차이는 SOLO 우선 조사 근거다. 오래전부터 있던 분기인지 새 map 입력으로 드러난 조건인지, 근접 불발/Walk-after-Attack과 같은 인과인지도 변경 이력·caller 감사 전에는 확정하지 않는다.

## 2026-10-14 최종 코드 감사 — 현재 판정

두 programmer 인계와 Main 독립 검증을 종합한다. 아래 행은 `88f60e38` 기준이며, `S=Assets/_Project/Scripts/`, `V=S/Presentation/Unit/UnitView.cs`로 표기한다. 정적 결함·조건부 후보와 실제 사용자 증상의 원인은 다른 판정이다. 원시 감사 tool 출력은 별도 영속 보고서로 보관되지 않았으며 이 절은 전달 근거의 요약이다.

1. **확인된 정적 결함 — SOLO 추격 waypoint 소비 정지.** `V:1601` SOLO Legacy 선택 → `V:2061` `_lastMovementTrajectoryReachedWaypoint=false` 초기화 → `V:2083~2095` Legacy candidate 위치 적용 후 반환. true 대입은 Reducer의 `V:2522`에만 있다(Main 독립 확인). 그런데 비동일 타일 추격의 `V:5530~5532`는 Advanced·이 flag·pursuitCheckpoint를 모두 요구해 index를 증가시킨다. Legacy 다중 waypoint가 첫 점 도착 뒤 소비되지 않는 코드 결함이다. consumer 도입 `7b4a8927d`(2026-08-25), Legacy return `65ee51fa5`(2026-08-18)는 Main blame 확인. 근접 불발과 맞닿는 후보지만 실제 발생 연결은 미증명. 반증 관측: 문제 SOLO 공격자가 해당 비동일 타일·다중점 분기를 사용하지 않았다면 이 결함은 그 사례의 원인이 아니다. 현재 그 경로/도착/공격 진입을 묶는 새 로그가 없다.
2. **확인된 unsafe-goal 경로 — 광산 목적지와 중간 통과 분리.** `S/Presentation/Input/InputHandler.cs:219` → `S/Presentation/UI/ProductionPanelUI.cs:565` → `S/Application/UseCases/UnitProductionUseCase.cs:465` → `S/Presentation/Production/ProductionTicker.cs:307` → `S/Application/UseCases/UnitMovementUseCase.cs:225` RequestMoveFlowField. `S/Domain/Hex/HexFlowField.cs:218~225`는 nonwalkable goal을 끝에 붙인다. `V:4928~4930`은 그 중심을 실제 root 목표로 쓰지만 `V:4973` domain ProcessStep은 제외한다. Legacy writer에는 그 물리 endpoint 차단 검사가 없다. append 도입 `dfe3edbd0`, skip-step `659fc0e3d`, writer `65ee51fa5`는 programmer 이력 전달. 광산을 **목적지로 지정한 사례**의 위험이지 사용자 **중간 광산 관통** 원인 확정이 아니다. 반증 관측: goal이 walkable이고 광산이 중간 선분에만 있으면 이 설명은 부족하다. goal/MineKind·실제 선분·domain 점유의 같은 시점 근거가 없으며 기존 랠리 보류를 확대해 수정하지 않는다.
3. **타이밍 가설 정정 — config/clip 차이를 runtime 주기 결함으로 판정한 설명은 철회.** Main 독립 에셋 확인: Assault(type1) looping clip 0.33333334초/speed1, config cooldown0.2·첫 hit0.16666667; BattleAxe(type5) looping clip3.166667초/speed1, config cooldown3.05·첫 hit1.02. 두 첫 OnAttackHit marker는 config와 일치하며 decimal 오해 근거가 아니다. **후속 programmer 확인:** `S/Bootstrap/GameBootstrapper.Setup.cs:59~88`→`S/Domain/Unit/UnitData.cs:169`의 config 복사만으로 최종값을 판단할 수 없다. `S/Infrastructure/Factories/UnitFactory.cs:273,287,519` 공통 SOLO/Server 생성 경로가 nonhealer 양수 clip 길이로 cooldown을 덮어쓰고 marker도 교체한다(Fox 의도 예외). 정상 생성 Assault 최종0.333333/BattleAxe3.1667이며, `38f56aebe` 및 `b65751c6`에서도 보존됐다는 programmer 보고다(Main은 Factory `259~299/510~551`의 Initialize→GetAttackTimelineClip→ApplyAttackClipCooldown→marker 교체→SetDependencies/Observe 경로를 직접 읽어 최종값 덮어쓰기 코드 사실을 독립 확인 완료했다; runtime은 미확정). 같은 타겟에서 새 Start가 없는 `V:5623`→`S/Application/UseCases/UnitCombatUseCase.cs:553,592` 반복 scheduling은 **frame drift 조건부 후보만** 남는다. 기존 Factory `:547`의 `[ASSAULT-TIMELINE]`/`[SNIPER-TIMELINE]`에서 roleSolo·fallbackCooldown·finalCooldown·finalHit로 최종값을 반증 가능하게 확인할 수 있으나 이번 새 실행 증거는 없다. LittleKnight type3와 LionKnight type22를 혼동하지 않는다(Lion 첫/둘째0.6/1.4333).
4. **Walk-after-Attack — 조건부 전환 후보.** `V:5623` ClearState→`V:7342` Resume에서 speed1 복구 후 root 이동은 뒤따른다. `_isMovementHeld` guard가 있으므로 실제 flag 상태 없이 stationary Walk를 확정하지 않는다. 같은 frame의 Recovery/held·speed·root 변화가 필요하다. 기존 `[UAS-PC-RECOVERY]`(`V:596~601`)는 해당 분기/필드가 관측되는 범위만 증거이며 SOLO 전체 Animator trace를 대신하지 않는다.
5. **맵 교정 소실·이중 변환 주장 미확정.** Main 맵 감사는 projection `S/Bootstrap/GameBootstrapper.Map.cs:191`→CreateUseCases가 같은 grid를 사용하고 RenderGoldMines `S/Presentation/Grid/HexGridRenderer.cs:459` 본문은 비교 diff hunk가 없으며 HexToWorld→ToView가 한 번이라고 보고했다. 기존 full-blob/asset 비교 범위를 유지하고 모든 코드·새 맵 runtime 정상으로 확대하지 않는다. `V:5458` clamp 부재는 새 사례의 조건부 후보이며 중간 광산 관통 원인은 OPEN이다. 실제 맵 hash·mine 좌표·root/view trajectory의 대조가 부족하다.

**분석 인계 완료 / runtime 미확정.** 분석 인계 당시 교정은 미승인이었으나 이후 사용자가 상단의 좁은 SOLO 범위를 승인했다. 이 절의 코드 근거와 이전 초안은 구현 전 감사 스냅샷이다. 실제 구현·검사 인계는 상단 최종 표에 완료 기록됐으며 원인 전체 확정·실기 PASS는 아니다.

## 규칙 원본과 증거 부족

기준 원본: [Units](../../../GameSystemRules/GameSystemRules_Units.md)의 규칙 2·6·10·11·14, U-MOV-REPATH·U-ATK-TIMELINE·U-IMPACT-TARGETLOCKED; [CombatSync](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md)의 NET-AUTH-001·NET-ROOT·NET-TIME-001~004; [RandomMap](../../../GameSystemRules/GameSystemRules_RandomMap.md)의 규칙 1·3·9·10·11. 광산과 건설 불가·완전 차단을 혼동하지 않는다. source/event marker는 사용자 기준 `ss:ff@30fps`로 해석하며 `ss+ff/30`초다.

부족한 근거: 문제 실행 빌드/커밋·유닛 종류·맵 유형/seed·타겟 종류·발생 시각·새 SOLO 로그/영상, `b65751c6→0de4f868→88f60e38` 전체 caller 비교, 증상별 실행 분기. 사용자가 재테스트할 수 있으나 현재 결과는 없다. 기존 로그에 필요한 필드가 없으면 관측 불가로 남기며 새 계측·실기 승인을 추정하지 않는다.

## 문서 검사 경계 — Main 전달

Main 실행 `check_docs`는 exit 1, 기존 상시 참조 문제 6건이다(전달값, 문서 담당 재계수 아님): `.claude/mistakes/unit-action-correction.md:45` NET-PRESENT-001·`:321` NET-PRESENT-003의 Units 범위 오인, `Assets/_Project/Docs/GameSystemRules.md:107` NET-DELIVERY-TRAVELING·NET-CANCEL-004의 Units 범위 오인, `Assets/_Project/Docs/History/ImplementationVerificationHistory.md:3397` UI 규칙 11의 섹션 모호성, `Assets/_Project/Docs/TechnicalDesignDocument.md:1521` UI 무작위 맵 제목 참조 부재. 이 위치는 Main 검사 시점 기준이며 이번 범위에서 수정하지 않는다.

새 `_Tasks`는 검사 대상에서 제외된다. Main은 이 Task의 `../../../` 링크 경로를 수동 확인했다. 따라서 검사 PASS·Task 자동 검증·게임플레이 정상 판정을 선언하지 않는다. 메모리 기준선 증가 알림 `+3662`도 Main 전달값이며 기존 변경에 관한 것으로, 이번 메모리 편집이나 기준선 갱신은 없다.

## 이전 전 유닛 감사 최종 판정 — Main 검토 이력 / Fox 원래 의도는 상단 정정 우선

**현재25종의 과거 교정 marker artifact는 유지된다. 그러나 original ss:ff와 현재 literal 초값의 차이는8종에 남아 있으며 「전부 정확/정상」 결론은 아니다.** 위25행 historical 출처와25행 current 값 표를 Main이 검토 승인했다. 표의 초안/추가인계 대기는 당시 단계로 보존하며 이 절이 현재 최종 한정 판정이다. 숫자는 현재 enum/Stats 원문 열과 Task 출처를 대조했다. Stats10/13의 구현값 mirror를 historical correctness 기준으로 삼지 않았다.

### 1. 보존·현재 연결 근거와 출처

- **Main 독립 확인:**25개 actual clip marker·config fallback·Factory 정책 대조, enum Human8/Spirit9/Transcendence8 확인. source5 최종 SHA가 앞선 SOLO 교정 인계와 동일하다. config fallback 자체는 최종 Factory runtime 값이 아니다.
- **보존 축:**`b65751c6`(final10/12 combatcommit) 대비 Animations/Units·UnitStatsConfig.asset·UnitFactory.cs·Prefabs/Units의 Main read-only diff stat는 EMPTY다. 이 정확한 경로의 clip/config/Factory/prefab rollback 근거는 없으며 원래 교정의 정확성 증명으로 확대하지 않는다.
- **담당 최종 인계/Main 검토:**Human+Transcendence16종/32프리팹 production wiring와 Spirit9종 대응 wiring·프리팹 감사가 완료됐다. 이 수령 근거와 Main의 직접 scene/25clip 확인은 별도 축이다. 새로운 native gameplay나 모든 모델 방향/시각 타이밍 측정이 아니다.
- **Game.unity Main 전체 diff 검토:**변경은 텍스트6곳 비움·autoReturn60·disconnect60으로 unit production wiring 변경이 아니다. 기존16줄 diff를 unit Factory 참조 회귀로 해석하지 않는다.
- marker별 actual값/과거 source 링크는 위25행 표에 보존했다. SpearMan은 [9/30 Plan](../../2026-09-30/18_02_spearman-attack-timing-correction/Plan.md) L3/L11/L29/「사용자 실기 결과 — focused PASS/CLOSED」L39~43의 사용자0:25 수용을 직접 확인했다. current.8333333은 그 결정 유지이며 historical unknown으로 남기지 않는다. Fox Plan§16/§21의 event1/HP2.25 예외도 과거 수용 근거가 있다. Rabbit0:20·Boulder1:10·Flame1:27·Bloom clip4는 실제 후속 결정으로 옛값을 대체했다.

### 2. original ss:ff 차이8종 — retained artifact와 다른 판정 축

원문 표기는 현재 StatsReference의 **보존된 old 표기 열**에서 직접 확인한 것이며 새 의도 승인으로 사용하지 않는다. 초 환산은30fps 산술 비교다. 현재값은 Main 독립 clip 추출이다.

| 이름 | 원문 ss:ff와 환산(초) | 현재 marker/옛 literal artifact(초) | 최종 한계 |
|---|---|---|---|
| LittleKnight | 0:25/1:15 →25/30/1.5 | .25/1.15 | 공통 oldTask literal과동일. originalframe최종승인 primary는 unverified historical final |
| BattleAxe | 1:17 →47/30 | 1.02 | old 전용Task explicit1.02 보존. 원문notation과 과거 교정초값의 차이지 이번merge 소실 아님 |
| Tank | 0:17 →17/30 | .16666667 | old 실행≈.167 보존, originalframe의 변경/최종승인 primary는 unverified historical final |
| CannonCart | 0:17 →17/30 | .16666667 | Tank와 같은 한계. config fallbackhit4와 actualmarker를 구분 |
| DustSpirit | 1:04 →34/30 | 1.04 | old 전용Task explicit1.04 보존. decimal을 새로frame로 재해석/자동수정하지 않음 |
| RhinoBreaker | 1:05 →35/30 | 1.05 | old actualAttack marker 주입Task의 explicit1.05 보존. originalnotation 의미 문제와 분리 |
| EagleArcher | 0:10 →10/30 | Attack.1/Attack2 event없음 | originalframe최종승인 primary와 실제 runtimeAttack선택 미확인. 개별 사용자수용은 취소하지 않음 |
| StreamSpirit | 0:17 →17/30 | .5 | 위7종에 더한8번째. oldTask explicit.50/focused수용 artifact 유지, originalnotation 재해석은 별도 |

이8종을 「8개 버그」·「8개 rollback」로 집계하지 않는다. oldTask 초값 유지 사실과 originalframe 의도 확인은 독립이다. source/clip이 과거와 같아도 최초 환산/교정의 의도는 추가 historical 근거가 필요할 수 있다. 반대로 Task에 명시된 후속 값이 있으면 옛 표기만으로 새 correction을 선택하지 않는다.

### 3. 주기·선택·보조 참조 한계

- **Bear/Lion:**oldTask config cooldown1.2/3 보존과 current clip length1.6666666/2는 서로 다른 입력 축이다. Main 확인 pre38f56 Factory부터 nonhealer clip.length로 실효주기를 읽는 정책이 있어 현재 실효1.6666/2는 새 cycle 교정/merge reversion이 아니다. Bear marker0:13/Lion0:18·1:13 사용자 수용을 cycle 설계 전체 승인으로 확대하지 않는다. Bloom은 사용자 승인 clip3→4와 healer cooldown3 예외라 Bear/Lion과 합치지 않는다.
- **Eagle 선택:**Attack marker.1과 Attack2 event없음은 actual asset 확인값이다. Factory의 controller.animationClips 열거에서 어느 것이 실제 선택됐는지 현재 native 관측 proof가 없다. 모호성은 explicit missing current proof이며 현재 잘못된 clip이 골라졌다는 결론/교정 선택은 없다.
- **Quake 보조 GUID:**Main exact-guid `.meta` 검색의 exit1/0개 결과로 `e36150d12a3f7474092bb70a6524596d`가 해소되지 않았다. controller Jump Attack 1(state141/motion154)의 dangling 후보지만 **정상 Attack(state128)의75f086… clip과1.667 marker는 존재**한다. pre-b657에도 동일 GUID라 existing unresolved asset reference이며 merge reversion·정상Attack누락·현재runtime원인 아님. 실제 animationClips runtime순서에서 문제가 발생했다고 단정하지 않는다.

### 4. 새 SOLO clock과 미완 근거

10/14 일반23종 shared SOLO occurrence clock 경로 변경은 이번 승인된 source 교정이다. clip/config/prefab의 과거 교정 소실이 아니다. positive sorted offset만 clock을 사용하고 ZERO/invalid/unsorted는 기존 Legacy fallback, Torrent/Healer는 제외다. prior pursuit 및 source5/CLR frozen GREEN 근거는 유지하지만 모든23종 실기 시각 수용으로 승격하지 않는다. 모델import/yaw·VFX endpoint·회차 종료/복구의 exact visual은 앞선 historical 표의 이름별 계약/한계 이상으로 검증하지 않았다.

Main의 이전 「현재2.25 timer 확인 뒤 peer 비교부터」 권고는 잘못된 요청 순서였다. `.claude/mistakes.md`에 별도4칸 사건으로 기록 완료했고 그 peer-test 문단은 이전 focused 범위 이력으로만 보존한다. 지금 첫 결론은 historical/current 계약 감사 보고이며 새멀티테스트가 먼저가 아니다. 새 교정·구현·build·Unity·QA·전체재테스트는 선택/승인되지 않았다. 사용자 Fox 시각 우려 및 native/pixel/전체 runtime 수용은 OPEN이다.

**최종 문서·보호:**Main은 새절 링크39개/누락0과 source5 해시 불변을 확인했고 check_docs 최종 실제exit1/정확히 동일한 기존참조6건을 인계했다. Task는 자동검사 제외이며 link 확인은 별도다. 이 Task-only 상태 갱신에 document-manager가 검사기를 병렬/추가 실행하지 않는다. 기존 source·Stats·규칙·다른문서·메모리 수정 없이 두 Task 문서와 별도 승인된 실수 사건 추가만 인계한다.
