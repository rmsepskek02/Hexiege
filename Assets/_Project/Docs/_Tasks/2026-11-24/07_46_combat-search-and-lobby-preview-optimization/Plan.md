# 전투 검색과 숨겨진 로비 미리보기 최적화 — 계획

전투 정답과 로비 표시를 바꾸지 않고 불필요한 반복 작업을 줄인다. 두 작업은 병행하되 각각의 기능 보존과 비용 근거를 따로 검증한다.

**검증 전 기존 로직 보존:** 대체할 기존 코드는 검증 전 주석 비활성화로 남긴다. 기존 동작과 같은 결과라는 근거 없이 삭제하거나 새 경로를 수용하지 않는다.

**문서 역할**
- 목적·질문: 두 승인 작업을 어떤 범위로 진행하고 무엇으로 수용/중단할지 정한다.
- 포함: 승인 상태, 불변 조건, 검증·진단 연결과 종료 기준.
- 제외·담당: 소스 조사·정량 근거는 [Research](Research.md). 과거 완료 장부는 이전 Task.
- 원본·갱신: 이번 실행 계획의 원본이며 구체 설계·검증 수신 뒤 현재 상태를 갱신한다.

## 1. 현재 단계

사용자는 두 작업 동시 진행과 새 Task 작성을 승인했다. 전투7파일·로비2파일 구현, source/SDK 제한 검증과 최종 영수증 대조를 완료했다. 이번 실제 ZIP 분석에서 전투 작업 감소·동일 입력14표본 mismatch0과 로비 hidden callback0/복귀를 확인해 관측 범위 내 수용했다. 정확한 source-build 해시 귀속·모든 기능·시각·CPU/FPS/GC/열은 미확정이며7절의 남은 경계를 따른다.

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

## 8. 승인된 main 선별 반영 계획 — 2026-11-24

사용자가 두 최적화 수용과 최신 main/충돌 확인·병합, 필요한 commit/branch 전환을 승인했다. 이 문서를 포함하는 선별 반영 커밋은 최신 main c697382a를 기준으로 한다. 원격 push는 미실행이다. 양쪽 작업 훼손 방지가 최우선이다.

1. 메인이 기존 staged/dirty·untracked와 branch 상태를 보존하고 최신 main과 이번9파일의 기준/차이를 고정한다. 기존 MEMORY·측정·실험 작업을 덮어쓰지 않는다.
2. 제품9파일의 이번 delta만 선별한다. branch 측정/실험 도구·Dev 진단은 main에 함께 복사하지 않는다.
3. 전문 담당이 UnitCombatUseCase/UnitView/LobbyRootView3개 동시 변경의 의미 충돌과 main 신규 skill/VFX/UI 보존을 검토한다. 충돌 마커 제거만으로 동등성을 선언하지 않는다.
4. 선별본에 맞는 source/컴파일·필요 검증을 확인한 뒤 메인이 Git 통합을 수행한다. 이전 혼합 작업 트리의 검증을 새 main 선별본 검증으로 대체하지 않는다.
5. 실제 영수증 수신 후 반영 commit/branch·보존 결과·남은 한계를 이 Task에서 갱신한다. 게시 범위는 아래 기록을 따르며 원격 push는 미실행이다.

이 단계는 기존 Task의 병합 마감이며 새 Task/Testcase·상시 문서·메모리를 추가하지 않는다. 상시 문서 변경이 필요하면 범위 보고·승인 뒤 별도 반영한다. 문서 담당의 제품·Git 변경은 없다.

제품판 구분: 이 문서의 측정·진단 근거는 opt 측정판에서 확보했다. main 선별 제품판에는 해당 Dev 진단·실험 코드/도구를 포함하지 않으며, opt 전용 원문·private 증거는 제품 산출물이 아니다.

## 선별 제품판 게시 범위·검증

이 문서를 포함하는 선별 반영 커밋은 최신 main c697382a를 기반으로 이번 제품9파일과 문서/metadata13파일만 포함한다. opt 측정/실험·Dev 진단은 opt branch에 유지하고 기존 main 기능 원문을 보존한다. 이 문서의 실기 진단 근거는 opt 측정판 자료이며 제품판에 진단 코드가 포함된다는 뜻은 아니다.

선별 main 후보의 runtime386 source Editor/AndroidDev/Release와 EditorTools26 source, 총4구성 컴파일 오류0을 확인했다. 경고는 각각25/28/32/0이며 기존 혼합 opt358 source 결과와 구분한다. 이 컴파일은 native/실기 재검증이나 FPS 보장이 아니다. 원격 push는 미실행이다.
