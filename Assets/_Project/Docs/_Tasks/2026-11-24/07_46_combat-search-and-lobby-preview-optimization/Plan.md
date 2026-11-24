# 전투 검색과 숨겨진 로비 미리보기 최적화 — 계획

전투 정답과 로비 표시를 바꾸지 않고 불필요한 반복 작업을 줄인다. 두 작업은 병행하되 각각의 기능 보존과 비용 근거를 따로 검증한다.

**검증 전 기존 로직 보존:** 대체할 기존 코드는 검증 전 주석 비활성화로 남긴다. 기존 동작과 같은 결과라는 근거 없이 삭제하거나 새 경로를 수용하지 않는다.

**문서 역할**
- 목적·질문: 두 승인 작업을 어떤 범위로 진행하고 무엇으로 수용/중단할지 정한다.
- 포함: 승인 상태, 불변 조건, 검증·진단 연결과 종료 기준.
- 제외·담당: 소스 조사·정량 근거는 [Research](Research.md). 과거 완료 장부는 이전 Task.
- 원본·갱신: 이번 실행 계획의 원본이며 구체 설계·검증 수신 뒤 현재 상태를 갱신한다.

## 1. 현재 단계

사용자는 두 작업 동시 진행과 새 Task 작성을 승인했다. 전투7파일·로비2파일 구현, source/SDK 제한 검증과 최종 영수증 대조를 완료했다. 이번 실제 ZIP 분석에서 전투 작업 감소·동일 입력14표본 mismatch0과 로비 hidden callback0/복귀를 확인해 관측 범위 내 수용했다. 정확한 source-build 해시 귀속·모든 기능·시각·CPU/FPS/GC/열은 미확정이며7절의 남은 경계를 따른다. 로컬 main/opt 병합·WIP 복원은8절과 같이 완료했고 사용자 승인 후 main 원격 push도 완료했다.

## 2. 병행 진행과 착수 조건

1. 전투: 실제 source의 전체 검색과 후보 입력/override·순서·Root/논리 tile·수명을 대조한다. 모든 기존 유효 후보의 보수적 포함과 기존 결과 동등성을 입증할 구체안을 먼저 기록한다. 큰 인덱스/새 측정 framework/게임 규칙 변경이 필요하면 범위를 조용히 넓히지 않는다.
2. 로비: 탭 전환과 camera/독립 root 수명을 확인하고 보이지 않을 때 rendering만 중단하는 최소안을 기록한다. Battle 복귀 시 캐러셀·RT·표시가 기존대로 복구되어야 하며 RT 메모리 해제를 절감으로 가정하지 않는다.
3. 서로 다른 제품 영역의 변경·검증 결과를 분리해 보존한다. 기존 dirty·이전 완료 최적화·이동 잔여 교정을 건드리지 않는다.

규칙 근거는 [Research3절](Research.md#3-규칙과-근거-경계)을 따른다. 기능 정답/표시 유지 계약이 우선이며 실제 비용 감소의 규모는 검증된 범위만 보고한다.

## 3. 테스트 항목과 진단 자료 — 초안

| 확인 항목 | 직접 근거 계획 | 로그·미관측 한계 |
|---|---|---|
| 전투 target/동률·범위·fallback/override 동일 | 기존 source 기반 동일 입력 비교 수단 재사용 검토 | 기존 runtime 로그만으로 모든 후보 누락/순서 동등성 입증 불가. 실제 항목·출력은 설계 수신 뒤 확정 |
| spawn/death·이동·재진입 시 후보 수명 | source 계약 대조와 경계 fixture 계획 | 경계 fixture와 이번 실기 관측 범위만 확인·전체 NGO 수명 미검증 |
| hidden camera off와 Battle 복귀 | 기존 Editor/native 화면·camera 상태 확인 수단 검토 | 화면/캐러셀·RT 수용과 GPU ms는 별개. 실행 전 PASS 아님 |
| 작업량 감소 | 같은 입력의 후보 검사 수 또는 hidden render 상태 | 구조 계산·예시와 실측을 분리. 전체 FPS/전력/발열 개선은 별도 실측 없으면 미확정 |

전투의 구체 로그·하네스는 설계 수신 뒤 확정한다. 로비 진단 필드는 아래5절을 따른다. 새 상시 계측을 만들었다고 쓰지 않으며 사용자에게 정확90초·채팅 시점을 요구하지 않는다.

## 4. 완료·중단 기준

각 작업은 기존 기능 보존 자료, 변경 source와 검증의 연결, 동일 조건에서 줄인 직접 작업을 확인한 범위로만 수용한다. 누락·동률/수명 보존을 증명하지 못하거나 로비 복귀/표시에 변화가 생기면 중단하고 원인·범위를 보고한다. Android GPU ms/FPS·실기 전체 회귀는 실제 실행 근거 없이 완료로 확대하지 않는다. 현재 전투 구현 연결 완료·C# 제한 검증 완료·최종 source/hash 영수증 대조 완료/로비 구현·SDK 제한 검증 완료 단계다. source fixture·SDK 결과는 수신 완료했으며5.1/6.3절을 따른다. 별도 formal QA·Git 실행은 없으며 빌드시도1회와 이후 실기 관측 결과는 아래 기록을 따른다.

## 5. 로비 camera gate 실행안

LobbyRootView.cs와 Lobby.unity의 serialized camera 연결1줄만 대상으로 한다. Awake에서 원래 enabled/same-scene/targetRT를 확인한 뒤 active이면서 Battle일 때만 원래 enabled를 복원한다. show 직전 enable, Disable/Destroy off, Enable 최신 tab 복구 순서를 검증한다. 초기 NetworkManager 누락은 기존 동작 fail-open, 참조 오류는 warn1/기존 enabled 보존이다. RT·캐릭터·Animator·tween·CanvasGroup은 그대로 두며 Camera.Render를 수동 호출하지 않는다.

| 확인 항목 | 예정 근거 | 수용 한계 |
|---|---|---|
| 최상위 탭 숨김·복귀 | lobby-preview-v1 state와 rendered/hiddenRendered | 이번 hidden1구간 callback0 확인·모든 탭 검증 아님 |
| Battle 복귀 첫 렌더 | resume-rendered/frameDelta + 화면 확인 | 로그로 pixel·자연스러움 보장 불가 |
| Disable/Enable·참조 오류 | 기존 source/경계 검증 | 원래 disabled camera를 임의 활성화하지 않음 |
| 캐러셀·내부 subpanel | 기존 표시/선택 동작 비교 | 최상위5탭만 gate, Battle 내부 RaceSelection 유지 |

상태 전환·render 계수 진단은 Editor/Dev에만 두고 매 frame 출력·Release 진단을 추가하지 않는다. 로비 구현·source fixture·SDK와 아래 실기 callback 관측까지 확인했고 시각·GPU 성능은 미검증이다.

### 5.1 로비 완료 범위와 잔여 검증

LobbyRootView117줄·씬 참조1줄 구현, 실제 source8조건 stub fixture와 current358 Editor/Release SDK 오류0(기존 경고24/31·변경 경고0)를 확인했다. Release 실제 DLL에 gate는 남고 trace callback/fields는 없다. 원래 RT/캐러셀 dirty는 보존했다. 상세 근거는 Research4.1절이다.

로그의 interval 계수 필드명은 rendered이며 gate+resume instance당64줄 상한이다. 실기 SRP 숨김 callback0·복귀 첫 render 재개는 확인했다. 사용자 표시·GPU/FPS/열은 미검증이며 정확 source/설치 해시 귀속은 미해결이다. 전투와 함께 확인한 최종 복합 SDK 결과는6.3절이며 로비 단독 근거와 구분한다.

## 6. 전투 후보 제한의 구체 방향

Research5절의 기존7파일에 유닛 live world index를 구현했다. 건물은 기존 전체 scan을 유지한다. 비용 비교는 Q(U+B)→Q(K+B)에 index 갱신·ordinal 정렬·roster 재구축 비용을 더한 것으로 한다. 건물 검색 제거·순 CPU 절감률을 미리 주장하지 않는다.

필수 검증은 전체 기존 target 포함, 실제 Dictionary 순서/삭제 slot 재사용 후 ordinal 재구축, unit-before-building·동률, Root3setter 갱신/Initialize/Destroy·binding 수명, zero/미등록/nonfinite overflow, 미지원·재진입·fault·큰 범위 fullscan fallback이다. 매 frame 전체 Transform scan을 새 비용으로 만들지 않는다. 이동 계산·방향·행동·권위·NGO/RPC 값은 보존한다.

최종 API 구현과 source fixture·SDK 제한 검증을 완료했다. 정확 진단 키·표본 범위와 확인 결과는6.3절을 따른다. source/hash 영수증 대조를 완료했고 이후 실기 관측의 제한 수용은 아래 결과 절을 따른다.

### 6.1 최종 API와 진단 범위

별도 optional ICombatUnitCandidateSource의 TryBeginUnitCandidates/EndCandidates로 lease를 획득·finally 반환한다. provider는 bucket/order/scratch 소유, Factory owner1 binding 교체·해제, Spawn mutation 후 event 전 revision 증가, 실제 Root3setter pose 경계와 UnitData 참조 identity 수명을 연결한다. old unit loop 본문·건물 fullscan·ordinal 정렬·overflow 포함은 그대로 유지한다. numeric unsupported/overflow·provider 미지원·재진입·index error는 기존 fullscan이다.

GameEnd summary1회와 bounded 동일 입력 비교를 사용하고 per-query 로그는 없다. queries/candidate/global/maintenance/fallback을 구분하며 baseline 진단 비용을 절감량에 포함하지 않는다. 정확 로그 키·표본 한도·source fixture/SDK 결과는 수신 완료했고6.3절에 기록했다. 전투는 구현·C# 제한 검증 완료·최종 source/hash 영수증 대조 완료다. 이후 실기 관측 수용·잔여 한계는 아래 결과 절을 따른다.


### 6.2 중간 구현·검증 이력 — 2026-11-24, 최종 결과는6.3절

7파일 구현 연결 완료, C# 제한 검증 완료·최종 source/hash 영수증 대조 완료이다. current358 SDK Editor/AndroidDev/Release 오류0(기존 경고24/27/31·새 경고 없음), 실제 provider+UseCase assembly 기반 mock fixture27검사·sparse203→3은 중간 근거다. 합성 후보 감소율을 실기 FPS/시간/GC로 바꾸지 않으며 기존 early-exit로 추가 절감0인 조건도 남긴다.

실제 진단 키는 COMBAT-SPATIAL WORK/COMPARE2줄, 최대16표본(8categories×2)이며 sampleMismatches/Faults/loopInspections를 연결한다. 최종 source/필드 영수증 전 결과가 바뀔 수 있으므로 중간 확인으로 보존하고 이 중간 시점의 최종 PASS는 보류했으며 현행 결과는6.3절이다. native/실기·빌드는 미실행이다.

### 6.3 최종 제한 검증과 수용 경계

SpatialFixture36검사와 복합358 source Editor/AndroidDev/Release 오류0(기존 경고24/27/31)을 확인했다. 실제 UseCase assembly/current provider와 managed Factory Transform stub 범위의 결과다. notify 누락 negative에서 정답 차이를 검출했으므로 모든 실제 Root pose hook을 유지하는 것이 필수다. expected target·unit/building 순서·slot 재사용·dirty/identity·numeric/fault/nested/dense 경계를 검증한 범위로만 수용한다.

warm nearest loop/getter/후보 각각203→3은 해당 통제 입력 작업량이며 실제 CPU/FPS·GC·열 절감률이 아니다. boolean의 추가 절감0 조건도 유지한다. WORK/COMPARE는 같은 DevGuid scope,8categories×2/max16표본이며 최악 포맷563UTF8 byte다. fixture mismatch0은 실기 전체 PASS가 아니다.

제품 범위는 Research5절 전투7파일+4절 로비2파일=9파일, 문서는 이 Task2파일이다. 구현·C# 제한 검증 완료/최종 source/hash 영수증 대조 완료이며 실기 관측 범위 수용·미검증 경계는 아래 결과 절을 따르며 Git 실행은 없다. 이후 빌드시도 상태는 아래 기록을 따른다. 최종 영수증과 독립 검수 근거 대조를 마쳤다.

최종 영수증은 `.utmp/combat-world-candidates-20261124/FinalVerificationReceipt.json`(SHA87860B…867FFA3)이며 Research5.3절에 근거를 연결했다. 메인의 독립36검사 재실행과7전투/2로비 source·358 source identity·fixture/DLL 해시 대조도 완료했다. 이 단계의 수용은 구현·C# 제한 검증까지이며 native/실기 성능 수용으로 확대하지 않는다. 추가 Root read/relocation·정렬/재구축·high-water 보관 비용은 남는다.

## 빌드시도 기록 — 2026-11-24

사용자의 이번 빌드시도 승인으로 메인이 Unity6000.3.5f2 Hexiege/Login/Android 창에서 source compile/Reload Domain 창이 닫힌 뒤 File→Build And Run을1회 클릭했다. 즉시 Detect Java Development Kit(JDK)/Checking JDK 단계를 관측하고 이후 감시를 중단했다. 빌드 완료·설치·앱 실행·실기 결과는 확인하지 않았다. 설정·키스토어·비밀번호·씬 저장의 수동 변경은0이다. 기존 source fixture/SDK 검증을 실기 완료로 바꾸지 않는다. 현행 로그 작업명은 `combat-search-and-lobby`다. 처음 안내한44자 이름은40자 제한 위반이어서 아래처럼 정정했다.

## 실제 테스트 ZIP과 짧은 작업명 — 2026-11-24

사용자가 테스트 저장·분석을 요청했다. 실제 파일은 `2026-11-24_18_52_combat-search-and-lobby_01.zip`이며 이 Task의 로그 별칭은 `combat-search-and-lobby`(직접 검산23자)로 계속 안내한다. 처음 메인이 안내한44자 이름은 수집기의1~40자 제한 위반이었다. 현재 규칙은 소문자/숫자로 시작하고 이후 소문자·숫자·하이픈만 허용한다. 도구 기능 변경은 없다.

해당 ZIP의 무결성과 실기 분석을 완료했다. 이번 관측 범위에서 두 최적화의 직접 작업 감소·제한된 동등성 근거를 확인했으며 상세 결과와 잔여 검증은 아래 실기 결과 절을 따른다. 정확 source/build 해시 귀속과 전체 기능·FPS 수용은 미확정이다. 문서 반영 승인이나 재저장을 다시 요구하지 않는다. 원시 로그는 보존하고 본문을 복제하지 않는다.

## 7. 실기 관측 수용과 남은 검증 — 2026-11-24

실제 ZIP4entry/Android5,851records의 무결성과 두 policy 발현을 확인했다. 정확 자료·근거는 Research6절이다. 앱 유지 snapshot의 footer 부재는 손상 FAIL이 아니며 삭제 보호를 유지한다. 현재 로그 별칭은 combat-search-and-lobby23자다.

| 항목 | 이번 직접 관측·수용 | 남은 한계 |
|---|---|---|
| 전투 후보 범위 | indexedGlobal409,943→candidateList125,207(69.4575% 목록 축소) | 전체 실제 loop·CPU/FPS 감소율 아님 |
| 동일 입력 비교 |14표본 loop285→183(35.7895%), mismatch/fault0 |8category 중7관측·Detect nearest not-found 미관측·전체 입력 보증 아님 |
| 안전 fallback/유지비 |54,647query 중 indexed16,350/fallback38,297·index fault false; binding43/pose18,284/roster51 visits1,637 | fallback 원인별 비율 미산정·추가 비용 포함 순 성능 미측정 |
| 숨김 로비 camera |21.852초/655frame SRP callback0·복귀 같은 Unity frame 재개 | hidden1구간·4탭별 기록/캐러셀 육안·pixel/GPUms·전체 수명 미관측 |
| 전투 관측 |Host/Client END·공격 결과/이동 범위 유효, Client query0 정상 | 전체25type·정확 timing·모든 기능 PASS 아님 |

관측 범위의 두 최적화는 직접 작업 감소와 제한된 동등성 근거를 확인했다. CPU/FPS/net GC/메모리/열 이득과 exact source-build 해시 귀속은 미확정이다. Editor 경기 전 DockArea NRE1은 별도 관측이며 이번 제품 인과 근거가 없다. 추가 테스트·교정을 일괄 요구하지 않고 현재 증거로 제한 수용한다. 새 제품·규칙·상시 문서·Testcase·Git·데이터 삭제 변경은 없다.

## 8. main 선별 반영·복원 완료와 남은 검증 — 2026-11-24

2026-11-24 병합 전 계획은 제품9파일 delta 선별·양쪽 작업 보존·의미 충돌 검토였다. 해당 실행을 아래와 같이 완료했으며 main 원격 push는 사용자 승인 후 완료했으며 병합 후 native/실기 검증은 별개다.

로컬 main `17ef4ceeb5b22fb0c8a2537a3d4987489b07a9ae`는 최신 main c697382a 기반 제품9파일+문서/meta13파일, 총22파일의 선별 반영을 완료했다. opt HEAD `80c035617e30891f79cb52b9566bf021a37342d9`는 부모 a64b31d와17ef4cee를 합쳐 최신 main을 포함한다. 23:34 재fetch 당시 origin/main은 c697382a이고 미푸시였으나, 이후 사용자의 “푸시도 해” 승인으로 main1commit push를 완료했다. server의 c697382a..17ef4cee main→main 정상 응답(exit0)과 local main/origin main의17ef4ceeb5b22fb0c8a2537a3d4987489b07a9ae 일치를 메인이 확인했다. 이 선별 반영 당시 branch는 codex/android-performance-optimization이고 opt branch push0·미커밋 작업을 보존했다. 당시17ef 선별 제품판에는 opt 측정/실험·Dev 진단을 넣지 않았고 그 실기 로그는 opt 측정판 근거다. 이후 전체 통합 범위는 마지막2026-11-25절을 따른다.

기존 tracked WIP36은 stash066a5b1ab19968bc775407c064471d3c1fb7743e patch3way로 복원하고 원래 untracked13은 사본과 byte exact로 복원했다. docs3/core5/other6 충돌을 해소했고 원래 staging3개(폰트/desktop meta/Burst txt)를 복구했다. 메인은 unmerged0과 기존 staged3 일치를 확인했다. 첫 stash2916의 ignore cleanup 실패 때 원래 index 사본 복구·49원문 exact 검증 후 두 번째066a 보관에 성공했으며 손실은 없었다. 백업·stash2개를 보존하고 Profiler46파일 약6GB와 raw 로그는 삭제·이동하지 않았다.

최종 실제 복원 작업본은 runtime390+Editor34=424 source이며 최신 main386runtime/26Editor와 opt 기존4runtime/8Editor 추가의 누락0을 확인했다. Editor/AndroidDev/Release/Tools4구성 오류0·경고25/28/32/0이다. 컴파일에서 발견한 UnitView 중복 field/OnDestroy2개는 동일 세트1개 제거로 정합했다. main schema11/합성 opt12를 구분한다. 이번 병합 턴의 native/UI·실기·빌드시도는 미실행이므로 이전 opt 실기 관측을 병합 후 새 검증으로 바꾸지 않는다.

근거는 private `.utmp/selective-merge-20261124/final-working/FinalValidationReceipt.json`(SHAAFB66C2F439C9916741E3739BC83342BE66EBB5D988DE2AB2825F02127FEE4CF), SourceInventory.json, SourcePreservationProof.json이다. main17ef에는 선별 문서판이 이미 포함돼 있고 이 작업본의 후속 마감 기록은 opt 미커밋 상태로 남는다. 문서 담당은 제품/Git를 변경하지 않았다. 정확 source·기능 전체·native 성능 수용 한계는 이전 결과 범위를 유지한다.

## 전체 합성 소스 검증 완료 — 2026-11-25

eaf05db4의 기존 최적화 전체·UI와 origin/main9c012141을 합친 source 검증을 완료했다. 합성 schema13은 main 신규 연막 wire/토큰과 opt DoT 출처를 함께 보존한다. 신규 main 연막4cs 및 관련 Projectiles/Tower/MistShrinePanel은 origin/main 대비 diff0을 확인했고 Game씬 ARF15줄은 eaf 기준 그대로다.

private `.utmp/all-optimization-integration-20261125/final-merge/CompileResults.json`에서 Editor/AndroidDev/Release/EditorTools 오류0·경고25/28/32/0이다. 실제 runtime394·Editor35(합429) source membership 누락0·외부 참조524을 확인했다. 이것은 합성 source/SDK 검증이며 새 native·멀티 실기·표시 전범위·성능 검증은 아니다. main용 설정은 enableFrameTimingStats0/Android define DOTWEEN으로 유지하며 과거 opt 측정 결과를 이 설정의 FPS 효과로 소급하지 않는다.

최종 Git merge commit0ebe909ac191ac05c71cf0d7f7c790c355d1de2c 생성 후 로컬 main fast-forward·origin main push·fetch 검증을 완료했고 local main/origin main/opt3refs가 일치했다. 과거 진단 opt 전용/제품 선별 기록은 당시 범위이며, 이번 승인은 기존 최적화·관련 작업 전체의 통합이다. 백업과 원래 작업·staging 보존을 계속 유지한다.

최종 source freeze 근거는 `.utmp/all-optimization-integration-20261125/final-merge/FinalValidationReceipt.json`이다. 최종 검증9partial 문자열 정합 후 EditorTools 재컴파일 오류0·runtime hash 불변을 확인했다. 독립 정적 검수 blocker0으로 main 연막 manifest·DoT bool·기존 최적화 보존을 확인했고 main ProjectSettings diff0이다. 이 검수는 새 native/실기 PASS가 아니며 제품 통합0ebe909a의 push·원격 확인까지 완료했고 후속 문서 기록은 별도 commit 대상으로 둔다.

## 전체 제품 통합·로그 메뉴 포함·push 완료

제품 통합 커밋 `0ebe909ac191ac05c71cf0d7f7c790c355d1de2c`를 생성해 로컬 main fast-forward·origin main push·fetch 확인을 완료했고 local main/origin main/opt3refs가 일치했다. 현재 작업 branch는 opt로 복귀했다. 기존 최적화·자연회복 UI뿐 아니라 LogArchiveZip 로그 메뉴도 eaf와 최종 제품 통합에 포함됐다. 결과창은 기본2버튼이며 확인은 외부 동작0, 웹페이지 명시 선택은 폴더+페이지를 함께 여는 bool webRequested 경로를 유지한다. 최종 EditorTools 컴파일 오류0이다.

`.utmp/full-main-merge-20261125/FinalRestoration.json`에서 로컬 자동생성3파일의 원래 index/worktree 원문 SHA 복원을 확인했다. backup·stash는 보존했다. 이 기록은 Git 반영과 원본 보존 결과이며 새 native/실기·연구 완료/스탯·FPS 검증은 아니다. 문서 후속 commit의 SHA를 제품 커밋과 혼동하지 않는다.
