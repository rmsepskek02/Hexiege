# Android 최적화 원인 확정과 통제 비교 계획

현재 계획은 멀티 전투의 GPU·렌더링과 대기를 구분하는 읽기조사·측정설계다. 로비/화면전환 후속은 보류하고 기존 결과·원본을 보존한다. 현행 계획은31절이며 아직 새 제품 교정이나 설정 변경을 시작하지 않았다.

> 최초에는 Research/Plan 작성만 승인됐고, 이후 사용자 「문서기준으로 남은작업들 진행해줘」로 **이 계획의 실행이 승인됐다.** 현재 단계1의 자료 조사·fallback 계측 구현과 native gate 확인을 진행했으며 실제 지연을 줄이는 추가 제품 교정은 아직 없다. 원인 미확정 상태에서 수정 파일을 확정하거나 기존 로직을 제거하지 않는다. 단계별 승인 범위·Git 변경·File 메뉴 Build And Run 시도 뒤 완료 감시 없음 등의 경계는 유지한다.

- 현재 완료/미완 장부: [Research.md](Research.md). 목표는 Android 30 FPS와 종료 끊김 감소다.
- 최초 문서 산출물은 Research/Plan과 필요한 Unity meta다. 지금은 승인된 계획을 실행 중이며 결과는 단계별 기록한다. Testcase.md 새 작성은 요청되지 않아 생성하지 않고, 아래 아직 수행하지 않은 설계를 실행 결과로 쓰지 않는다.

## 1. 상태별 다음 행동

| 구분 | 다음 행동 |
|---|---|
| 완료 | 원래 NCC/8marker와 추가 10소스의 구현·CLR·Unity 컴파일 증거 재사용. 기기 PASS와 구분. |
| 미완 | 종료burst의 로그·역할·빌드를 확정해 좁게 교정하고 통제 전후 비교. NCC/PC-RECOVERY는 Android Host. |
| 검증 필요 | APK/source 연결·전체 통계·기존 native/기능 장부의 최신 receipt 확인. 기존 수용 범위 먼저 재사용. |
| 보류 | NGO pool·compaction·로딩/Canvas는 원인 근거가 생긴 항목만 별도 구체안 작성. 자동 구현하지 않음. |

## 2. 근거와 보존할 규칙

GameSystemRules 인덱스와 아래 원본을 읽었다. 이 계획은 규칙 변경 승인이 아니다.

| 계획 항목 | 근거·보존 |
|---|---|
| 원인 관측·전투/수명 교정 | [전투 동기화](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md) NET-AUTH-001·NET-AUTH-002·NET-ROOT-001/002:서버 HP/판정·Root writer를 바꾸지 않고 표현/관측을 분리. |
| marker·표현 비용 교정 | [Units](../../../GameSystemRules/GameSystemRules_Units.md) 「전투 연출 동기화 규칙」17~19와 [전투 동기화](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md) NET-TIME-002:typed marker·권위 피해 시각·서버투사체Client0.10초/Host0초·표현 보류상한0.3초 보존. |
| 로그 식별·출력 교정 | [LogRules](../../../LogRules.md) §1.4/1.6/1.8/1.9/1.10:payload·분류·sanitize·sink·예외객체/스택·private raw보존. Info NoStacktrace는 의도정책변경으로 명시,전역 스택 끄기 금지. |
| 역할 통제 | [Network](../../../GameSystemRules/GameSystemRules_Network.md) 최상위 원칙은 제품 동작의 역할 일관성. 서버/클라이언트 workload가 같다는 뜻이 아니며 제품UI에 역할 선택을 추가하지 않음. |
| 종료·재경기·로딩phase | [MatchFlow](../../../GameSystemRules/GameSystemRules_MatchFlow.md) 규칙14/16/17과 [UI](../../../GameSystemRules/GameSystemRules_UI.md) 「공통 UI 규칙」L-1~L-4:결과·새맵검증/교체·이탈·loading 소유권/gate 보존. 측정 편의의 종료 지연/씬 우회 금지. |
| 기능 수용 | [Handoff](../../2026-10-25/19_44_android-30fps-profiling/Handoff.md) §5~9·[WORKFLOW](../../../WORKFLOW.md):기존수용/정적/native/기기/성능 분리,표본0·미발생을 PASS로 만들지 않음. |

## 3. 단계1 — 역할·빌드·느린 로그 확정

**진행 상태:** 기존 선택프레임 native export는 수정 뒤36thread·62샘플을 완주했고 메시지metadata/stack 부재를 확인했다. fallback 계측과 FullRegression/Root/Refactor8 native 확인 뒤 새19:48 Host 캡처를 받았다. [Research11절](Research.md#11-1948-새-host-캡처-최종-분석--원인-분해-성공전후-비교-미완)의 CSV각2,000프레임·APK/IL2CPP/Host 식별·1,866행겹침을 확인했다. 대량 Instantiate·UpdatePreloading이 큰 spike에서 확인됐고 별도 ServerRpc 직속 Console.Other35.44ms도 남아 있다. 정확 prefab/owner·scene phase·Other 메시지는 더 좁혀야 한다. prebuild GUID0으로 정확 source 연결과 독립 전후 성능 비교는 미완이다. 이번 분석의 제품 변경은0이다.

기존10:36/10:38 캡처·보존 runtime에서 먼저 아래를 연결한다. 없는 자료는 복원불가/미확정으로 남기고 다른 Editor로그를 대신 붙이지 않는다.

1. 파일·프레임범위/device/OS·프로세스·session과 Android 실제 network role을 확인한다. toolbar나 Editor의 IsServer만으로 Android 역할을 정하지 않는다.
2. HEAD+dirty10source digest·빌드옵션·APK SHA256·설치 package/version·실행 session을 이어 build identity를 기록한다. 기존 APK를 복원할 수 없으면 새 식별 가능한 기준부터 만든다. 토큰·비밀번호·UID원문은 기록하지 않는다.
3. frame1193의 Invoke→OnDisable와 직속 LogString을 분리해 정확 message/level/type/stack·건수·발생phase(포기/패배/이탈/로비/재경기)를 확인한다. TransportDisconnect 존재만으로 종료action을 단정하지 않는다.
4. PC-RECOVERY는 server-only trace 생성이 전제다. 순수Android Client면 이 변경을 종료burst 해결책으로 설명하지 않고 실제 Client 로그를 조사한다. Host라도 해당 경로가 관측되지 않으면 기여 미확정이다.

기존 자료로 연결되지 않으면 **좁은 계측안을 먼저 확정**한다. 후보는 OnDisable→trace retire/타입별end→ConsoleSink의 정적 이름 ProfilerMarker와 bounded 수치 카운터다. frame/session·role·unit type·종료reason·category/level·sink route/stack option을 건수와 연결하되 payload·순서·호출횟수를 바꾸지 않는다. 새 매-event 장문 로그로 burst를 키우지 않고 bounded 요약/Profiler 데이터로 남긴다. Allocation Call Stacks는 별도 짧은 원인 추적에만 켜고 기본 Deep Profile은 OFF다. 전문 프로그래머가 소스·guard·기록상한·회수/Release스트리핑을 제시하고, 승인된 이 계획 범위에서 메인이 구체안을 검토한 뒤 적용한다. 새로운 정책·범위 변경이면 해당 변경을 별도로 확정한다.

완료 조건은 “느린 호출X가 Android역할Y/빌드Z/종료phaseP에서 N회이며 정확 로그·비용 경로가 연결됨”이다. 연결 실패는 제품 수정 근거가 아니다.

## 4. 단계2 — 확정한 한 경로만 조건부 교정

단계1 원인에 맞춰 파일·메서드·보존 계약·비용 축을 추가한다. **현재 제품 수정 파일은 미확정**이다.

- 조립/객체 비용이면 출력 동등성을 유지하는 조립·소유권 개선을 고른다. 계약상 필요한 출력의 throttle/삭제는 하지 않는다.
- 스택 비용이면 실제 해당 Info의 선택 정책을 검토한다. NoStacktrace는 정책변경으로 명시하고 Warning/Error/Exception·운영증거·본문/횟수/순서는 보존한다.
- Instantiate/Canvas/preloading이면 소유객체·씬phase·원인부터 나눈다. 증거 없는 사전pool·비동기로딩·UI재구성은 하지 않는다.
- 종료/Retire/Clear/Dispose·재진입·공개Slot/bundle·snapshot 수명과 예약/목록순서·권위피해·표현key를 유지한다.

변경된 계약에 필요한 CLR/정적 확인만 수행하고 관련 기존하네스는 재사용한다. UnityAPI/NGO/실기는 CLR로 PASS하지 않는다. 최종 source 묶음에서 컴파일·필요gate를 확인하며 이미 완료한 검사와 제안을 분리한다.

## 5. 단계3 — 비교 빌드와 조건 고정

| 항목 | 조건 |
|---|---|
| 기기/role | SM-N971N Android12 동일장치 우선. **NCC·PC-RECOVERY는 전후 Android Host+Editor Client**. Client전용 원인이면 전후Android Client의 별도쌍을 만들며 Host와 섞지 않음. |
| source/APK | 기준·교정manifest/APKhash/설치·실행 연결. NCC는 marker-only070ddcaf와optimizedff8bdf95의 격리비교. 최신asset/투사체차이가 섞인 옛commit을 현재 10개 source의 전후라고 부르지 않음. |
| 부하 | 동일맵/유닛종류·팀·실제수/생산량/전투경과/종료action/비활성화수/phase/카메라/해상도/품질. 재경기는 새맵 규칙 유지·별도phase분석. |
| 계측/환경 | 동일Development/Autoconnect/DeepProfileOFF·모듈/보관상한·샘플링/CallStacks·로그세대·예열/충전/발열/배경앱. GPU미지원은 미확인. |
| 실행 | source컴파일→필요기존native gate→**File 메뉴 Build And Run 시도**. 클릭 뒤 완료 감시는 하지 않으며 사용자 완료/실행확인 뒤 진행상태 갱신. |

현재 작업을 reset/clean/역patch로 덮어쓰지 않는 가역적 별도 비교본/보관source를 준비한다. switch/worktree/checkout/apply/fetch 등 Git실행은 문서 승인이 자동 허용하지 않는다. 구체 실행 전 해당 사용자 Git지시/승인을 확인하고 현재branch·dirty자료를 보존한다. 인계patch 중복적용·Library/빌드파일 혼동을 막는다.

## 6. 단계4 — 최소 한 회의 정의된 사용자 테스트

role/build/원인과 두 빌드가 준비되면 **각 버전에서 같은 한 회 시나리오**를 먼저 수행한다. 부족한 phase만 추가하며 매 수정마다 전체 경기를 반복 요구하지 않는다.

1. 이번 첫 원인 확인은 **Android Host + Editor Client**로 Login부터 역할을 확인하고 **밀집 전투30초**를 저장한다. 실제 보관범위를 확인하며2,000프레임에 밀리면 phase를 나눈다. 이후 통제 전후 비교에서는 적은유닛/다수이동 장면도 필요한 범위로 따로 정의한다.
2. **포기 직전 Record를 새로 시작하고 결과 화면이 안정된 뒤10초까지** 별도 저장한다. 재경기/새맵로드/짧은정상플레이/로비는 별도phase로 둔다. 서로 다른 종료action을 전후쌍으로 비교하지 않는다.
3. **앱이 살아 있을 때 Record정지→Save**하고 기존 `D:\Projects\Hexiege\Hexiege\ProfilerCaptures`에 저장한 파일/metadata를 확인한다. 저장전 앱종료·연결변경으로 loaded old capture를 재저장하지 않는다. 메인은 사용자 완료 보고 후 기기의 role/build/session 로그를 자동 캡처해 새Profiler와 연결한다. runtime session/role/frame을 함께 보존한다.
4. phase전체의 표본수/프레임범위,frame평균/p95/관측최대,33.3/50/100ms초과건수와1,000프레임당수,GC Alloc/frame·GC.Collect횟수/시간,관심marker Total/Self/Calls·건당비용을 산출한다. 이 구분은 보고용이며 사용자승인없는 새 수용 임계값이 아니다.
5. 종료는 OnDisable수·category별로그수·직속/하위·stack route·종료~결과/재경기시간을 함께 비교한다. 대기/렌더/계산과 중첩Total·CPU/GPU를 분리한다. 부하가 다르면 원시값/차이를 보고하고 개선율을 보류한다.

이 시나리오는 성능 비교이며 인계 기능 전체PASS가 아니다. 실제 사용자 관찰·누락phase를 남기고 Release30FPS지속/발열체감은 Development와 별도다.

## 7. 원래 기능 인계는 별도 장부로 확인

[Handoff](../../2026-10-25/19_44_android-30fps-profiling/Handoff.md) §5~7의 기존기준을 사용한다. 기존 세부수용/receipt를 먼저 찾아 실제 범위를 재사용하고 없는 항목만 최신source의 실행상태를 채운다.

- Play정지/서비스정리 후 FullRegression·Root·RefactoringVerification의 실제Run BEGIN/8case/END를 확인한다. 메뉴클릭과 종료결과는 다르다.
- V01~V10 통제 기능은 **Editor Host+Android Client**다. 서버요청12case·종료뒤진입·heartbeat·재경기·fixture가 어느 항목을 덮는지 장부에 대응한다. 성능 쌍과 역할이 반대임을 유지한다.
- 넓은7계약은 실제API/fixture/실기 범위를 확인한다. 집중8case와온라인12+1+1이 인증/반올림/인구/영토/수입/partial전체를 검사한다고 해석하지 않는다.
- FAIL/NOT_RUN/NOT_EXERCISED와 이유를 남긴다. suite무언급PASS는 실제 실행회차에만 적용한다. 별도 Testcase.md는 만들지 않고 승인된 기존 검사를 실행·기록한다. fixture의 명시 cleanup 통과를 PlayMode lifecycle 증명으로 확대하지 않는다.

## 8. 산출물과 종료 기준

결과는 이Research/Plan 상태와 기존장부에 연결한다. raw/Profiler원본을 문서에 복제하지 않고 [LogRules](../../../LogRules.md) §1.10/기존보존정책에 따른다. 삭제된 중복을 재생성하거나 추가자료를 임의삭제하지 않는다.

종결은 실제role/build/phase/메시지원인,좁은교정의source/동등성/Unity확인,비교가능전후통계/종료burst판정,기능 장부의실제수용범위를 분리해 보고하는 것이다. 부족한 축은 미완/미확인으로 남기며 “전체 최적화 완료/렉 완전 해결”로 요약하지 않는다.


## 9. 새 Host 캡처 뒤의 실행 우선순위

**현재 승인·진행:** 사용자가 분석 문서 기록과 다음 교정 진행을 승인했다. [Research12절](Research.md#12-사용자-기록-승인다음-교정-조사-착수)처럼 전문 프로그래머가 source와 실제 호출 경로를 연결 중이다. 처음에는 구체 설계/교정 파일을 확정하기 전이었고, 후속으로 아래10절의 재경기로그 두 위치만 확정해 검증 중이다. 제품교정 효과·새실기PASS는 아직 기록하지 않는다.

새 자료는 테스트 실패가 아니라 계측의 원인 분해 성공이다. [Research11절](Research.md#11-1948-새-host-캡처-최종-분석--원인-분해-성공전후-비교-미완)의 두 파일은1,866프레임이 겹쳐 독립 전후쌍이 아니므로 평균 차이로 개선율을 만들지 않는다.

현재 자료에서 먼저 수행할 조사는 raw313/UI314의 ServerRpc→Instantiate204회의 정확 prefab·owner/생성 수명,raw1476/UI1477의 UpdatePreloading 목적지scene/실제phase 연결, CSV2 raw140/UI141의 OnDisable 밖 ServerRpc 직속 Console.Other23회35.44ms의 정확 메시지 연결이다. 기존 prefab registry·factory/RPC·씬전환 호출과 로그를 대조하고 증거가 부족한 부분만 별도 좁은 관측안으로 남긴다. **NGO pool·비동기로딩·Canvas 재구성은 아직 구현하지 않는다.** 수정 경로·소유권·회귀 위험이 확정되면 그 구체안을 별도로 검토한다.

사용자에게 같은 테스트를 즉시 다시 요청하지 않고 현 자료로 대상을 좁힌다. source prebuild GUID와 실행GUID의 연결,통제 전후쌍,출시성능·기능 장부는 계속 미완이다. Shadow의상한480/dropped769은 진단완전성 실패로 별도 기록하고 피해누락으로 환산하지 않는다. 파일삭제/업로드는 하지 않는다. 승인된 다음 교정은 원인·소유권·위험을 확정한 구체안으로 진행하고 실제 구현/검증이 끝난 뒤에만 완료 상태로 갱신한다.


## 10. 승인된 다음 교정의 구체 범위

메인이 사용자 진행 승인 범위에서 [Research13절](Research.md#13-재경기-로그의-좁은-교정안-확정구현-확인-중)의 두 위치만 확정했다. ConsoleSink의 정확 재경기 동적 Despawn Dev.Info 한 종류만 exception없는Info/정확 system·class·message 조건으로 NoStacktrace하고 Ops(Event=접두)는 제외한다. 이는 의도한 스택 정책 변경이며 내용·횟수·순서·Warning/Error/Exception·운영 경로는 보존한다.

GameEndUI는 extraFields가 있을 때 기본state 중간 문자열 복사만 줄이고6상태값의 원래 read순서·최종 필드/UTF-8를 유지한다. 실제2,560조합 동등성과 CLR 할당 결과를 기록했으나 extra없는경로의 절감은 없고 Android 효과는 미확인이다. prebuild source 목록13개로 확장해도 GUID연결 미완은 유지한다.

Ops 경계를 포함한 Development/Release각56,448조합(의도한NoStack차이18)·기존384조합과 최종 Unity 컴파일을 확인했다([Research13.1절](Research.md#131-sink-동등성unity-컴파일-후속-확인)). 최종QA helper/Ops Event=제외·manifest13entry 추가blocker없음과 메인/전문3source hash일치를 확인했다. 실제 File 메뉴 Build And Run클릭시도도 마쳤다([Research13.2절](Research.md#132-최종-sourceqa-확인과-재경기-전용-빌드-시도)). 클릭 뒤 진행/완료감시는 전혀 하지 않았고 성공/설치/새실기는 미확인이다. 대량 Instantiate의 정확 prefab/owner·로딩scenephase는 이 교정과 별개 보류다. NGO pool·씬/게임플레이·material 변경은 포함하지 않는다.


### 10.1 이번 재경기 로그 교정의 다음 측정

이번 교정은 Android Host + Editor Client에서 **재경기 수락 직전 Record를 켜고 새 경기 진입 후10초에 정지·Save**해 확인한다. 앱을 종료하지 않고 기존 `D:\Projects\Hexiege\Hexiege\ProfilerCaptures`에 `rematch`가 드러나는 이름으로 저장한다.6절의 이전 포기/전투 시나리오와 섞지 않고 정확 StartRematch Despawn 로그route/건수·실제role/build/session·새씬phase를 연결한다. 추가 newsource/씬/NGOpool 교정 없이 이번의 실제비용을 확인하며 비교조건이 다르면 개선율을 보류한다.


### 10.2 재경기 정책의 기기 적용 확인·효과 측정은 미완

[Research14절](Research.md#14-2026-10-28-2254-테스트정책-적용-확인과-자료-정리10-29-기록)의 새 Host 기기로그에서 정확 대상20/20 NoStack·Warning42/42 기존스택을 확인했다. Exception은 미발생이다.22:54 CSV2,000프레임의 평균33.561749/p9534.322117/최대367.316895ms는 산출했으나, **대상 재경기20로그의 구간이 CSV 밖**이고 관측NoStack15개는 PC-RECOVERY여서 이번 변경의 절감ms·향상률은 미확정이다. 정상종료/재경기/로비 시나리오를 이전포기와 전후쌍으로 섞지 않는다.

큰 로딩/생성/Other로그·source연결·통제 전후 비교의 미완은 유지한다. 추가 제품 수정이나 새 테스트 실패로 판정하지 않고 현 자료의 적용 확인과 측정 한계를 분리한다. 승인 삭제3파일170,274,612bytes와19:48:32의 고유134프레임 deep원본 손실·summaryCSV/완료export/기준/하네스/raw보존 내역은 Research에 기록했다. 이를 추가 삭제·raw업로드 권한으로 해석하지 않는다.


## 11. 다음 로딩 교정·생성 경로 조사 진행

사용자가 이번 작업안에 「진행해」로 승인했다. [Research15절](Research.md#15-2026-10-29-로딩-교정의-원인-연결대량-생성-조사-승인)에 따라 UpdatePreloading의 실제scene/phase/source와 ServerRpc의 대량 생성prefab·owner·수명을 연결한다. 구체 설계·수정 파일은 아직 확정 전이며 그 근거 없이 비동기전환·NGO pool을 먼저 구현하지 않는다.

현재는 승인된 후속 작업의 조사 착수 상태다. 원인 확정→좁은 교정안/보존 계약 검토→구현/적절한 검증의 순서로 기록하며 완료·성능 수용을 미리 선언하지 않는다. 앞선 재경기 정책 확인·효과ms 미확인·삭제/보존 이력은 유지하고 새 Testcase나 추가 데이터 정리를 자동 편입하지 않는다.


## 12. 이번 로딩 교정 확정 범위 — 랭킹 행 풀 지연 생성

[Research16절](Research.md#16-로딩-owner-확인과-rankingview-행-풀-지연-생성-교정)의 native EntityId/prefabGUID/source 연결로 로비 RankingView 초기화의 RankRow10생성18.28334594ms를 확인했다. 제품 교정은 RankingView의 첫 Render 직전10행 준비·이후재사용으로 한정한다. 로비init은0행,서비스/초기Warn/헤더·visibility·기존 행layout·부분실패중복방지·closed재진입 보호를 유지한다.

처음 랭킹을 열 때 비용이 옮겨갈 수 있음을 명시한다. 전체 UpdatePreloading278ms·ServerRpc204생성 전체의 해결책으로 확대하지 않는다. AsyncScene·NGOpool·material공유는 보류하며 다른 로딩owner는 별도 근거를 찾는다.

실제method CLR10case와 최종읽기QA blocker없음에 이어 Unity컴파일·focusedfixture 실제 실행PASS도 확인했다([Research16.1절](Research.md#161-unity-컴파일focused-fixture-실제-실행-확인)). Init0/First10/Reuse10/Partial3→Final10/Preview/Closed assertion·cleanup을 확인했으며 EditMode명시cleanup의 한계는 유지한다. 첫랭킹열기/재열기시각·실기전후측정은 미확인이다. 실제 File 메뉴 Build And Run 클릭 시도는 완료했고 이후 UI/로그/프로세스/APK 진행·완료 확인은 하지 않았다. 완료 여부는 사용자가 확인하며 성공·설치·기기실행은 미확인이다.204생성owner 추가 조사·전체async전환은 이번에 수행하지 않아 잔여로 유지한다. 새 Testcase나 임의scene/설정변경을 추가하지 않는다.


## 13. 남은 묶음 마무리와 통합 사용자 검증 안내

사용자가 남은 최적화를 먼저 마무리한 뒤 구체 검증 절차를 안내하도록 승인했다. [Research17절](Research.md#17-남은-최적화-묶음-선행통합-검증-안내-승인)의 남은3축을 현재자료/source로 조사→확정경로만구현→코드검증→통합사용자안내 순으로 진행한다. 개별 수정마다 재테스트를 반복 요구하지 않고 최종 묶음의 실제 변경·한계에 맞춰 안내한다. 조사중/구현예정은 완료가 아니다.

Ranking의 기존 nativePASS·빌드시도 상태는 재사용한다. 게임권위/씬gate/로그계약·수명/소유권을 유지하고 새 구현은 실제검증 결과와 연결한다. 아래표는 조사단계의 준비 틀로 보존하고, 실제 적용 뒤 구체조작·기댓값·저장시점은15절의 최종 통합절차로 확정했다. 아직 사용자 실기는 미실행이다. 별도 Testcase.md는 만들지 않는다.

| 통합 확인 구간 | 최종 안내에 채울 항목 | 현재 상태 |
|---|---|---|
| 빌드/실행 식별·역할 | 실행APK/source연결·Development옵션·기기/Host·Editor/Client 확인 방법 | 최종묶음 구현/빌드 뒤 확정 |
| 로비/로딩·랭킹 첫열기/재열기 | 실제 수정owner·로딩진입 경로·첫10행/재사용·표시/입력 회귀·저장시점 | Ranking native근거 있음,사용자 실기 미확인 |
| 대량생성·밀집전투 | 확정 생성대상/수·동일조건·marker/GC·게임 동작 확인·원본 저장 | 남은source조사/구체구현 뒤 확정 |
| 종료·재경기·남은로그 | 실제 교정한 메시지/route·종료action·결과/새경기phase·앱생존Save | 기존정책 적용근거와 새측정 요구 분리 예정 |
| 기존 기능 인계·사용자 보고 | 재사용할 기존gate·부족한 기능범위·관찰이상·파일/시간·누락단계 | 원래장부 유지,최신실기 자동PASS 금지 |

최종 안내 시 어디서 Record를 시작/정지할지,무엇을 눌러 어느 화면까지 갈지,정상으로 볼 관찰과 실패보고자료를 명확히 적는다. 같은기기/역할/빌드옵션/부하와 실제 저장범위를 유지하고 독립 전후쌍이 아니면 향상률을 보류한다. 실제 개선량·전체성능·Release30FPS 수용은 새 근거가 채워진 축만 갱신한다.


## 14. 확정한 대량 생성 교정안 — HexTile 저장 mesh bake

[Research18절](Research.md#18-대량-생성-owner-확정--hextile의-저장-mesh-bake-교정-준비)의 native187/204생성·246.899/252.809ms와 prefab/source근거로 HexTile 한 종류의 baked mesh 교정을 확정했다. 메인은 사용자 구현 승인 안에서 이특정prefab·baked mesh·필요Editor도구만 asset변경 예외로 허용했다. 다른prefab·씬·material 전체공유·NGOpool·전체async변환으로 확대하지 않는다.

실행 순서는 Editor bake도구준비→기하/2submesh/material참조/순서/collider/transform과runtimePB의존 동등성확인→확인된asset에만적용→Unity/필요fixture→최종사용자통합검증이다. runtime Awake rebuild 제거의 실제component/참조처리는 검증근거로 확정하며 원래geometry/상호작용을 보존하지 못하면 적용하지 않는다. 18.1절의 실제bake적용·동등성·Unity컴파일확인까지 완료했다. 실제Android성능/시각/상호작용 결과는 아직 없다.

최종테스트표에 아래 맵검증을 포함해 구체안과 결과를 채운다. 표추가는 사용자실기PASS가 아니다.

| 추가 통합 확인 | 안내에 포함할 조작 | 정상 기댓값/남길 근거 |
|---|---|---|
| 맵 타일 외관 | 새경기맵 전체와 가까운타일을 본다 | 타일기하·표면/옆면·material·겹침/빈틈이 기존과 동일 |
| 타일 클릭·건물 배치 | 기존UI로 타일을 선택하고 허용/불가 위치에 배치를 시도한다 | collider선택/강조와 기존배치조건·거부동작 유지 |
| 길찾기·이동 | 양팀유닛을 생산해 일반이동/장애물주변경로를 관찰한다 | 위치/높이·막힘/통과 판정이 기존계약대로 동작 |
| 재경기 맵교체 | 정상종료뒤 재경기를 수락해 새맵에 진입한다 | 기존새맵생성/검증/교체gate·타일표시/입력 유지 |
| 생성 비용 | 같은역할/조건에서 타일생성구간을 저장한다 | PB재계산의 제거 여부와 실제frame/GC비용을 원본으로 확인,278ms전체제거주장 금지 |

로딩scenephase·Other로그의 다른원인은 잔여로 남기고 현재근거없는 수정은 하지 않는다. 앞선Ranking 첫열기tradeoff와 로그정책실기확인/효과ms미확인도 그대로 유지한다.


<a id="15-최종-사용자-통합-검증-절차아직-미실행"></a>

## 15. 최종 사용자 통합 검증 절차

**최종 빌드 상태:** 실제 File 메뉴 Build And Run 클릭 시도 완료. 이후 UI/로그/프로세스/APK 진행·완료 감시는 하지 않았고 성공·설치·새 실기는 미확인이다. 사용자 완료/실행 확인 뒤 아래 절차를 진행한다.

현재 근거로 확정한 Ranking 지연풀·재경기 로그·HexTile bake를 한 경기→재경기→로비 순서로 확인한다. 작성당시는전부미실행안내였으나,이후사용자의테스트완료보고를받았다. **현재구간별측정/수용범위는17절과Research20절을따르며,캡처미포함을테스트미실시로바꾸지않는다.** bake native동등성/Ranking fixture/로그정책의 기존 확인은 재사용하지만 실제게임/기기성능을 대신하지 않는다. 전체로딩278ms·Other로그의 미확정원인을 모두해결했다는 뜻은 아니다.

### 공통 준비와 매 파일 저장 방법

- 사용자 빌드완료/앱실행 확인 뒤 **Android Host + Editor Client**로 경기한다. Profiler연결대상은 Editor가 아닌 **Android 기기의 Hexiege**로 고정한다. role/build/session은 새기기로그·identity/prebuild자료와 연결한다.
- Development + Autoconnect, **Deep Profiling OFF·Allocation Call Stacks OFF**, CPU/Memory와 같은계측설정을 유지한다. 기기/충전/발열·해상도/품질을 기록하며 기존게임값을 바꾸지 않는다.
- **매 구간마다 Record OFF→Clear→Record ON→표의 조작→Record OFF→Save**를 반복한다. Clear는 이미저장한파일을 지우는 것이 아니라 현재기록을 비우는 것이다. 저장완료 후 다음구간을 시작한다. 앱은 모든파일을 저장할 때까지 종료하지 않는다.
- 기록상한2,000프레임은30FPS에서 약66.7초다. 아래처럼20~30초/짧은전환으로 나누고 실제저장범위를 확인한다. 로딩/대기때문에 상한을넘기면 이미밀린초기프레임을 있다고 가정하지 말고 해당누락을보고한다. 한 번의 긴Record에 전체경기를 담지 않는다.
- 파일은 `D:\Projects\Hexiege\Hexiege\ProfilerCaptures`에 고유이름으로 저장한다. 예: `YYYYMMDD_HHMMSS_AndroidHost_firstmap.data`. Unity가정한확장자를유지하고 기존파일을 덮어쓰지 않는다. `firstmap / combat / surrender / rematch / lobby / ranking-first`를 이름에 넣는다.

### 한 번에 진행할 구간별 표

| 순서·파일 | Record ON 시점과 실제 조작 | Record OFF·Save 시점 | 정상 관찰/사용자 메모 | 결과 |
|---|---|---|---|---|
| 1 firstmap | 로비에서 두역할이 준비되고 **첫경기시작 직전**Clear/Record ON. 경기진입뒤 타일전체/가까운표면을 보고 타일선택·건물배치·유닛생산/장애물주변이동을 평소대로 확인한다. | **첫맵진입 후20초**에OFF/Save. 관찰이더필요하면 저장후Record밖에서계속한다. | 표면/옆면·재질·높이·틈/겹침 정상. **Blocked 빈공간은 선택되지 않고 NoBuild 빗금·금광·팀색이 유지**된다. 선택/해제 때 기존색 복원,허용배치/금지배치거부·유닛경로/통과가 기존과동일. 이상위치/행동 기록. | 사용자 완료보고·해당phase 측정연결 미확정 |
| 2 combat | 유닛이모여 **밀집전투가 시작되기 직전**새Clear/ON. 양쪽평소전투를30초관찰한다. | **밀집전투30초** 뒤OFF/Save. | 유닛종류/대략수·스킬/공격·카메라/진영 기록. 멈춤/공격·피해/표현 이상과 체감끊김 기록. | 사용자 완료보고·해당phase 측정연결 미확정 |
| 3 surrender | **포기 버튼을 누르기 직전**새Clear/ON→포기한다. | 결과화면이완전히표시되고 안정된 뒤 **10초**에OFF/Save. | 종료·결과버튼/입력차단 정상,중복결과/유닛잔상·정지/회복이상 없는지 기록. | 사용자 완료보고·해당phase 측정연결 미확정 |
| 4 rematch | 결과화면에서 상대요청/승낙을준비하고 **재경기 수락 직전**새Clear/ON→수락한다. | **새 경기 진입 후10초**에OFF/Save. | 새맵이정상교체되고 타일외관/클릭·배치/짧은유닛이동 정상,새회차에이전UI/상태잔류없음. 기존규칙상 우연히같은지형은오류로단정하지않음. | 사용자 완료보고·해당phase 측정연결 미확정 |
| 5 lobby | 재경기를짧게진행한후포기/결과까지간다. **결과에서 로비 버튼을 누르기 직전**새Clear/ON→로비로돌아간다. 랭킹은아직열지않는다. | **로비표시 후10초**에OFF/Save. | 로비loading종료/기본탭·버튼 정상. 랭킹을안열었을때의진입기록을보존한다. | 사용자 완료보고·해당phase 측정연결 미확정 |
| 6 ranking-first | 현재Development는임시25개preview다. 첫열기→다음/이전페이지→다른탭→재열기의행/페이지관찰은의미있다. **정렬/새로고침은preview에서비활성이정상**이며운영데이터검사가아니다. 이번에동일preview테스트반복을요청하지않는다. | **첫 클릭부터약20초**,최초표시와재열기를관찰한뒤OFF/Save. 통신이늦으면실제표시시간/미표시를기록하고상한을넘기지않는다. | 임시25개중페이지별최대10행·열배치/높이·페이지전환/재열기행중복없음은관찰가능하다. 서버실데이터·정렬/갱신수용은별도미확인. 첫열기Profiler구간도이번자료에서미확인. | 사용자 완료보고·해당phase 측정연결 미확정 |

타일기능확인은첫맵/재경기에서 짧게겸하고,긴관찰은Save뒤Record밖에서해도된다. 실패시즉시그구간을저장하고 파일명·화면/행동·대략시각·기대와다른현상을알린다. 진행하지못한행은 미실행/조건미발생으로표시하며 억지로정상처리하지않는다.

### 사용자 전달 자료와 분석 판정

6개원본파일(못진행한구간은이유),기기/역할·빌드완료/실행확인,유닛종류/수·관찰이상/체감·첫랭킹표시/재열기차이를전달한다. 앱종료전에저장완료를확인한다. raw로그/identity는같은실행의role/build/session과연결해분석하며 사용자에게토큰/비밀번호를요구하지않는다.

분석은phase별frame범위/N·평균/p95/관측최대·긴프레임수·GC·OnDisable/Console/타일생성/로딩의실제호출을분리한다. 이전파일과부하/시나리오/기기/role/계측·APK가통제된독립baseline쌍일때만전후개선율을검토한다. 현재는그baseline이없으므로 한새실행의결과와구조비용관측부터보고한다. 기존V01~V10·넓은7계약전체와Release30FPS/장시간안정성은별도미완이며이표만으로전부PASS하지않는다.


## 16. 13:27 사용자 테스트 뒤의 판정·검증 불가 범위

[Research19절](Research.md#19-2026-10-29-사용자-테스트-완료-보고와-1327-캡처의-확인-범위)에새파일/전체통계/Host·실행identity·로그정책/preview정정을반영했다. 사용자기능실행보고는유지하고,저장2,000프레임에NCC·생성/PB·씬integration·ServerRpc·Console/Disable 등46marker가미발생한것을테스트미실시나비용0으로읽지않는다. 평균33.356584/p9534.055458/최대55.013500ms는그저장범위값이고전후개선율은아니다.

현재판정은 (1)기존bake/Ranking native와CLR·선택NoStack실기적용확인유효, (2)대상생성·전투·종료/재경기·로딩의기기효과ms는phase미연결로미확인, (3)랭킹운영서비스/실데이터정렬·갱신은preview로검증불가, (4)GPU/Release·장시간/발열및Shadow dropped부분전수대응은별도미확인이다. 이유는Research표에서항목별로남기며“이번테스트실패”로합치지않는다.

이전15절의정렬·새로고침안내는현preview설정미반영이어서정정했다. 임시목록도실제Render/Ensurepool을쓰므로페이지/재사용관찰과기존fixture수용을취소하지않는다. 같은preview반복검사를다시요청하지않고서버실데이터/실페이지정확성은운영환경준비의별도범위로둔다. 이번분석에서제품/asset교정·설정변경·삭제는없고Editor읽기도구만확장했다.


## 17. 분할8개 분석 뒤의 현행 상태와 다음 우선순위

[Research20절](Research.md#split-eight-captures-20261029)이16절의13:27 단일자료 판정에 이어지는 현행 분석이다. 사용자 테스트 완료보고를 유지하며 이번8개에서 실제 타일생성·NCC전투·종료/재경기 흐름·로비 로딩을 새로 관측했다. 이전에 해당구간이 저장되지 않았다는 한계를 현재도 전부 미관측으로 유지하지 않는다. 15절 표는 당시에 전달한 절차이고 추가 반복테스트 요청이 아니다.

| 항목 | 현행 판정 |
|---|---|
| 첫맵/재경기 생성 | 실제 HexTile211회6.878613ms·PB0 확인. 기존 native기하/충돌 동등성 유효. 모든 외관·배치·경로를 에이전트가 실기PASS한 것은 아님 |
| 전투 | NCC/투사체 호출 관측. 최대636.781ms는 하위 동기로그598.9515ms가 지배하며 투사체순수계산600ms로 해석하지 않음 |
| 종료/재경기·로비 | 기기로그 경기흐름과 생성/로딩 관측. 마지막 로비프레임 RankRow 생성0이나 loading292.778ms 잔존;중첩비용 합산 금지 |
| Ranking | preview의 초기화/재사용 nativePASS는 유지. 운영데이터/정렬·갱신 및 실기첫열기 비용은 미확인. 동일preview 재테스트 요구 없음 |
| 성능 수용 | 파일마다2,000프레임 통계 산출. 겹치는프레임과 다른부하가 있어 통제전후 향상률·전체30FPS/Release수용 불가 |
| Shadow | 첫경기 dropped1,138로 전수대응 미완. 수집상한/EVIDENCE를 실제피해오류로 단정하지 않음 |

다음 우선순위는 (1) 시각·stack·경로로 연결한 NetworkCombatController 사망 Info의 동기출력 비용, (2) 남은 로비 loading의 owner/수명/씬단계다. 기존 NoStack 선택정책을 모든로그에 확대하거나 NGO pool/전체async변환을 근거없이 적용하지 않는다. 특히598ms의 stack생성/Android sink/OS별 비중은 아직 미확정이다. 조사 뒤 확정한 경로만 별도 구현·동등성/컴파일·필요회귀 검증과 연결한다. 이번 범위는 분석/기록이며 새 제품·asset 교정은 하지 않았다.

새로운 사용자 테스트를 지금 요구하지 않는다. 기존8원본과CSV/로그를 보존했고 삭제/Git 변경도 없다. 이후 교정 또는 비교계획이 확정되면 필요한 최소 구간과 미검증 이유를 구체적으로 안내한다.


### 17.1 사용자 승인 뒤 실행할 후속 교정

분석/기록 이후 사용자가 남은 교정 진행을 승인했다. 먼저 `OnUnitDied`의 정확한 사망 Info에 한정한 ConsoleSink 선택 NoStack 정책을 검토→확정 조건 구현→출력/정책 동등성·Unity컴파일·필요회귀 검증으로 진행한다. 다른 Info나 Warning/Error/Exception,본문·횟수·순서·게임동작은 보존하며 실제 구현결과를 받기 전에는 완료로 기록하지 않는다. 남은 로비 loading/맵준비 RPC도 기존 source/자료로 owner와 단계별 비용을 좁힌다.

최종 테스트의 최소 대상은 실제 변경 확정 후 갱신한다. 현시점 후보는 Android Host의 사망 Info가 발생하는 짧은 전투구간과 실제 추가 교정된 전환구간이며,아직 사용자에게 재실행을 요청한 것은 아니다. 599ms 전체절감·전체렉 해결은 주장하지 않는다. 15절의 당시 절차/미관측 문구보다17절의 현재 관측범위와 이 실행상태를 우선하며 과거 이력은 지우지 않는다. 새 Task/Testcase·제품변경 완료·기기성능 PASS를 이 문서갱신만으로 만들지 않는다.


### 17.2 사망 Info 교정의 중간 구현 상태

Research20.5의 정확한 사망 Info 조건만 ConsoleSink에 추가됐다(Ops `Event=` 제외). 조건부 호출스택 생략은 의도된 정책변경이며 다른레벨/예외와 로그내용·횟수·순서는 보존한다. 프로그래머 보고의 Dev/Release 각145,152 matrix/36route PASS와 실제Unity참조 csc 검증을 독립QA source검토와 구분해 기록했다. 결과txt가 없어 보고를 메인 직접실행 증거로 격상하지 않는다.

남은 순서는 실제Unity Editor 갱신확인·필요회귀→확정한최종빌드 시도→새기기 정책/성능 확인이다. 아직 최종빌드 시도·설치·실기효과는 확인하지 않았다. 로딩/맵준비 RPC는 읽기도구로분해중이며 제품추가교정 완료로 쓰지 않는다. 최소실기대상은 사망 Info가 발생하는 Android Host전투로 유지하되 최종 구현/검증을 마친 뒤 안내한다.


### 17.3 이번 교정 완료 범위와 미해결 범위

정확 사망 Info 선택 NoStack 제품교정 및2개 native hotspot분해를 완료했다. Research20.6에 RPC self120.307ms·로비asset GC/collect/destroy/integration/layout을 기록했다. 미계측managed내부와 명시GC0이라는 한계로 추가 동등교정을 확정하지 못했으므로 전체async/pool/material공유 등의 임의변경은 하지 않는다. 로비loading·맵준비RPC는 미해결이며 전체 남은 최적화 완료로 표시하지 않는다.

메인이 DLL갱신을 관측했으나 최종source hash연결/빌드시도 결과는 후속확인을 기다린다. 현시점 새기기 성능·설치성공은 미확인이고599ms 전체절감 주장도 하지 않는다. 최종 사용자 검증은 실제 사망 Info 정책 적용과 같은경로 시간관측에 집중하되 미해결로딩을 개선된것처럼 안내하지 않는다.


### 17.4 최종 빌드 시도 상태

메인이 최종 ConsoleSink hash일치·diff검사문제0·최종source 이후 runtime/Editor DLL갱신을 확인하고 실제 File 메뉴→Build And Run을 클릭했다. **시도 완료,빌드 성공·설치·실기 효과는 미확인**이다. 클릭 후 진행/완료 감시는 사용자 지시대로 하지 않았다. 다음 기기확인은 정확 사망 Info 정책과 발생구간의 시간관측이며599ms 전부절감 또는 전체해결로 간주하지 않는다. 로딩/RPC 미해결 상태는17.3을 유지한다.


## 18. 새4개 분석과 승인자료 정리의 진행 상태

Research21절에 새17:30〜17:34 자료를 기록했다. 사망 Info116/116 선택NoStack 적용·Warning74/74 stack보존은 확인됐으나 NoStack 자체 LogString이1.58초인 새 지연이 있어 '정책적용성공'과 '출력지연해결'을 분리한다. 추가3개 상세추출 후 경로/시간을 확인하며 OS·전송·sink 내부원인은 아직 미확정이다. 제품추가수정·전체해결/개선율 판정은 하지 않는다.

사용자 승인으로13:27 idle 원본/하이라이트2개128,556,375B 정리를 완료했고 삭제장부와2개요약CSV를 보존했다. 새4개·교정전8개·필수baseline은 유지한다. 가치미확정10:38/17:40 및 다른자료의 추가삭제는 하지 않는다. 상세분석 결과를 받은 뒤 같은문서에서 최종판정을 갱신한다.


### 18.1 상세분석 완료 뒤의 판정

Research21.2의3상세추출·104건 로그연결을 완료했다. NoStack 사망한건1580.097ms와 여러종류 Default/PC로그 지연이 확인돼 로그의 동기출력 구간이 여전히 중요하다. 정확메시지 연결은 metadata복원 아닌시각/경로/건수/순서 추론이며 내부CPU계산량·OS/전송/Editor수신 기여는 미확정이다. 선택정책 적용성공은 유지하고 지연해결·전체개선율·NoStack회귀는 판정하지 않는다.

이번분석/정리는 완료했고 추가제품·asset수정·새빌드·즉시추가테스트 요청은 없다. 원인확정 전 로그전체NoStack/비활성화나게임로직변경으로 확대하지 않는다. 로딩/RPC의 기존 미해결범위와GPU/Release·장시간미확인은 계속 남긴다. 승인삭제2파일만 정리됐고독립QA가부재/요약보존을확인했다.


### 18.2 최종 정리 장부와 중복 통계 주의

Research21.3에 승인한 중간17:33:44 원본/하이라이트2개476,506,655B의 추가삭제를 기록했다.119수치열2,000행 전체가 앞뒤 보존원본716+1,284행에 포함돼 요약통계 중복을 제거할 수 있지만 다른thread/metadata/GPU/rawbinary 전체동일은 미검증이다. 완전복제라고 하지 않으며 중간원시상세 재조사 가능성은 포기했다. 중간CSV/4batch sha장부·앞뒤원본·17:34 기반3상세/timejoin은 보존했다.

최종 삭제는 idle2개와합쳐4파일605,063,030B다. 최신17:34와 교정전필수기준은 보존했다.18절의 네파일 통계를 독립표본으로 합산하거나 포함된 긴프레임을별도사건으로 중복판정하지 않는다. 추가교정/빌드/테스트 및 임의추가삭제는 없다.


### 18.3 사용자 승인에 따른 추가 교정 준비

사용자가 문서기록과 추가교정을 승인했다. 주스레드 동기콘솔 출력 지연을 대상으로 LogRules/RuntimeLogger 보존계약 조사→프로그래머 구체안→메인 범위검토→확정구현·동등성/정책·Unity검증 순으로 진행한다. NoStack을 더 넓히는 것만으로 해결됐다고 하지 않는다. 현재 추가제품코드는 미구현이며 내부OS/전송원인은 미확정이다.

구체안 수신 후 이 절에 바뀌는 출력경로/시점·보존되는 원본/경고/오류/예외·수명/종료 처리 및 검증계획을 채운다. 정책변경은 원래와 동일하다고 숨기지 않고 명시한다. 새Task/Testcase나 Git/삭제작업을 추가하지 않으며,18.2의 최종삭제장부와 통계중복/원시상세 한계를 보존한다.


### 18.4 승인한 private 원본/Console 분리 구현계획

메인구체검토로 Development Android nonEditor 일반 Dev.Info만 기존 RuntimeLogger private파일성공시 중복Console출력을생략하도록 구현승인했다. 단일owner/실행별고유세션/씬간유지/quittingClose,전체원문·순서·필요managed caller정보보존,Ops Event=/Warning/Error/Exception과Editor/Release 기존출력유지가필수다. 파일고장은현재로그포함Consolefallback+faultlatch/재귀안전warn,queue/worker/throttle/자동삭제는없다. None/기존NoStack존중과 native wrapper 바이트동일아님을구분한다.

구현3곳+manifest/필요Editor focused검사→보존/분기/고장/재귀/수명검증→실제Unity컴파일→기기private원본과Logcat 동시회수·정책/성능확인순으로진행한다. 파일성공 Info가Logcat에없어지는변화는명시하며RuntimeLogger원문/경고오류예외를잃지않는다. 실제구현결과수신전완료로쓰지않고파일I/O·kill무손실/599ms전체제거를보장하지않는다. 현행규범LogRules와이전원문LogHistory H15를갱신했고추가범위확장은없다.


### 18.5 구현 검증 이후 최소 실기 대상

Research21.6의7sourcehash·972case/962record·parser/4compile·실제Unity2record fixture를확인했다. native는Editor공유핵심/호출자/파일범위이며AndroidFS·IL2CPP·새기기성능은미확인이다. archive는모든GameLog원문을보존하고일반Dev.Info만성공시native중복생략한다. archive주집계/native대조원칙과seq/UTF8/END/activeprefix·flush/close분리는LogRules1.10을따른다.

최소실기는Android Host+Editor Client로(1)유닛사망전투20〜30초,(2)종료직전부터결과안정10초까지별도캡처다. 매번Clear→Record→정지→Save하고앱종료전에저장한다. 이는콘솔/파일분리효과와종료burst누락확인범위다. 메인이기기private파일과Logcat을자동회수하므로사용자추가수집작업은필요없다. 실제 File Build And Run 메뉴클릭 시도를 완료했으며 사용자 요청에 따라 이후 진행·완료는 감시하지 않았다. 빌드 성공·설치·실행은 미확인이다. 로비GC/RPC120ms를이번교정완료로안내하지않는다.


## 19. 새기기 archive 확인 이후 현행 우선순위

Research22절에 새2개2,000프레임통계·archive2,137완결record·상세5개/hotspot2개를반영했다. 일반사망Info155개파일보존/Logcat0과캡처범위fallback/failure0으로파일분리성공을확인했다. 전체footer/closedreceipt가없으므로세션전체무손실/전체fallback0은미확정이다. 기존Editorfixture수용과실제기기관측을분리한다.

7.923초프레임의대부분은보존대상Warning12개의native LogString7.852초이며파일Write1.614ms/Caller20.425ms와구분한다. 공격Shadow failure와Poison stale/closed Warning의원인/출력정책을다음검토대상으로삼되 모든Warning비활성화·게임오류단정은하지않는다. 일반Info정책성공은큰멈춤해결이아니다. CallerStack비용및로딩/새경기준비RPC도남고정확phase/내부OS전송기여는미확정이다.

현재범위는분석/문서이며제품·asset추가교정/새빌드/재테스트요청/자료추가삭제없음이다. 통제전후쌍이없어향상률·전체완료/Release성능PASS를선언하지않는다. 원시archive/private로그는기존보관계약으로유지한다.


### 19.1 후속 교정 실행 승인과 조사·정리 순서

사용자가 추가교정과불필요자료의즉시정리를승인했다. 질문을반복하지않고(1)동기native Warning 출력,(2)정상stale/closed Drop 반복Warning,(3)UAS Shadow 수명판정의세가설을source/기존자료로조사한다. source조사결과로좁은출력정책/진단교정안을확정한뒤게임writer·HP·RPC를유지하며구현·경계동등성/필요회귀·Unity검증을진행한다. 아직구체안/실제구현결과는대기이며LogRules변경은확정안후검토한다.

QA가중간산출/중복을감사하고메인이명시목록으로안전정리한다. 고유raw·최신2캡처·비교필수baseline은보존하고실제삭제/보존근거·합계는후속기록한다. 현재문서기록만으로제품교정/정리완료를선언하지않는다. 새Task/Testcase나Git작업으로범위를확대하지않는다.


### 19.2 확정구체안 — 두진단Warning 출력운송 교정

사용자승인범위에서메인이정확두Warning의성공archive후native중복생략을승인했다. Shadow 정확message와전체고정grammar/순서/경계검증을통과한Poison Drop StaleStopOrClosed WriterDamage만선택하며Ops Event=/Exception객체·고장/재귀/사용불가는제외한다. 게임writer/HP/RPC·sourceowner실패/Stopguard·UAScap/drop판정은바꾸지않고원문/레벨/횟수/순서/callerstack을archive에보존한다. 다른Warn/Error/Exception·EditorRelease·nativefallback은유지한다.

현재RuntimeLogger/ConsoleSink/읽기Exporter3source구현중이다. 실제검증결과수신전완료로기록하지않는다. LogRules/H16에좁은정책예외/종전원문을반영했고모든Warning비활성화는아니다. 메인안전정리21중간DLL461,824B완료·후보0을기록했으며고유raw/최신캡처/필수baseline과source/검증근거/UnityLibrary는보존했다.


### 19.3 코드/QA 확정과 남은 실행 경계

Research22.6의최종3source/영수증·기존972/새2,255case·실기본문56건selector독립재현·4csc/경계검증을완료했다. 출력운송교정은구현/정적검증완료이며기존Warning판정·게임writer/HP/RPC는불변이다. 실제Unity갱신/native공유핵심검사후File BuildAndRun시도만진행할예정이고현재빌드미시도/새기기효과미확인이다.

최소새실기는AndroidHost+EditorClient로Poison과유닛사망이있는전투20〜30초,종료직전부터결과안정10초를별도저장한다. 일반정상통과와대상Warning선택발생수가실제로존재하는지를구분해판정하고무작정긴테스트는요청하지않는다. private파일/Logcat은메인이회수·대조한다. 로비GC/RPC·CallerStack미해결과내부OS전송기여미확정은유지한다. 추가csc중간DLL정리는검증후메인명시목록실행결과를받아기록한다.


### 19.4 native 공유핵심 확인과 정리 완료

최종Unity DLL/source연결과실제메뉴Run3f5b4371ee994f9fa602a178b4163047의공유핵심2record파일검사를완료했다. Editor공유핵심범위만PASS이며Android파일시스템/IL2CPP selector실기효과는미확인이다. 메인안전정리추가5 DLL105,472B완료,이전21개와합계26개567,296B이며원본/검증소스/영수증/compile로그는보존했다. 메인이 실제 Unity File→Build And Run 메뉴클릭 시도를 완료했다. 클릭후 UI/로그/프로세스/APK 진행·완료 감시는 하지 않았으며 빌드 성공·설치·실행·실기효과는 미확인이다.


## 20. 방 만들기 실패 안전진단의 승인 실행계획

사용자명시로그추가요청에따라Research23절의6파일계획을진행한다. 기존호출호환optionalcontext를UI→Host→Relay/Lobby에명시전달하고Infrastructure helper에서local attempt/elapsed·enum단계/결과·안전상태bool·SDK예외분류/숫자code/reason·HttpStatusUnknown만기록한다. UI요청/cleanup대기·Relay세단계·Lobby·StartHost/Heartbeat/Notify/Returned/catchFail을연결해어느단계에서멈췄는지판별한다.

순서는source조사/계약대조→6파일진단구현→민감값비출력·기존caller/실패/중복Ops·Release조건검증→Unity/필요focused검사→기기에서방만들기1회재현후privatearchive회수다. 네트워크call/retry/authinit/cleanup·게임동작은바꾸지않는다. 새진단에원문예외Message/stack/Response·개인식별정보/방이름/코드를복제하지않으며file-onlyInfo를Logcat부재로실패판정하지않는다.

현재실제구현/검증/새빌드는대기다. 기존두실패의SDK code/reason/HTTP가없어근본원인fix로기록하지않고최신PID무실패를과거실패해결로간주하지않는다. 기존기획/게임/로그규범이나새Task/Testcase는변경하지않는다.


### 20.1 구현·QA 확인과 최소 재현 절차

Research23.1의6source/5compile·Editor22/AndroidDev22/Release15 추출fixture와실제UnityDLL갱신을완료했다. 추가진단만구현했으며서비스원인fix나기기성공판정은아니다. 개인정보새복제/추가networkcall·retry/authinit/cleanup변경은없다. 완료중간DLL/PDB10개9,346,800B는메인이안전정리했고원시/검증근거/실제Library는보존했다.

새빌드가완료되면기기에서방만들기를 **1회** 눌러실패를재현한다. 성공하면성공이라고보고하며연타하지않는다. Profiler는필요없고앱/USB연결을유지한뒤메인이privatearchive+Logcat을회수한다. file-onlyInfo의attempt/단계/elapsed/SDK안전숫자값을기존원인Ops와연결해다음원인을좁힌다. 메인이 실제 File→Build And Run 메뉴클릭 시도를 완료했다. 클릭후 UI/로그/프로세스/APK 진행·완료 감시는 하지 않았으며 빌드 성공·설치·실행은 미확인이다. 기기재현은 아직 미실행이고 로그추가만으로 원인이 해결된 것은 아니다.


### 20.2 실제Editor 직접테스트 결과와 현재 우선범위

사용자요청에따라20.1의기기재현안보다메인Editor직접테스트를우선했다. Research23.2의실제방생성2회는모두대기화면/25진단연결성공이며elapsed2561.622/1950.159ms다. 해당행범위의실패/예외/Warning/Error0과후속방취소·Heartbeat정지·Lobby삭제·Disconnect완료/Play정지까지확인했다. 상대접속/경기는미실행이다.

Editor2회성공은과거Android Relay예외/Lobbytimeout원인해결을뜻하지않는다. 현재추가코드변경/빌드·기기재현반복요청은없고원시일별로그와영수증을보존한다. 신규기기빌드/테스트를기계적으로요청하지않으며다음판단은사용자증상/기존근거범위에맞춘다.


## 21. 11월4일 재개 분석의 현재 판정

Research24절은10월30일14:10 캡처를11월4일재분석한기록이다. summary2,000valid와상위3hotspot을확인했다. 평균33.379609/p9534.743038/최대53.471039ms·긴콘솔출력미관측과파일전용Info/정확Warning정책의실제동작은확인됐지만통제전후개선율/전체최적화완료는아니다. CallerStack의상위self비용과기존미해결로딩/RPC·UAS진단원인은남는다.

현재는캡처시간축CSV연결을기다리며정확전투/결과phase·전체수용판정을대기한다. archive4,329완결prefix의유효성과footer/closed없는전체종료한계를구분하고prebuildGUID0의직접빌드연결한계도유지한다. 추가제품교정/빌드/신규기기테스트반복요청은없다. 읽기도구변경/필요중간DLL정리의최종결과를받아동일문서에이어기록하며고유raw는private보존한다.


### 21.1 시간축 확인 뒤의 범위 확정

Research24.2의동일PID11anchor/clock2,000행검산으로두번째경기끝전약56초+종료결과후약10초포함을확정했다. 종료전1688프레임평균33.381757/최대53.471039ms,경계37.468151ms,후311프레임평균33.354806/최대35.967075ms다. 로비복귀14:10:52는캡처밖이므로로비loading수용은미완이다. 최종QA/PhaseAnalysisReceipt로구간내정책Warning23(UAS10/Poison13)·경계모호0을확인했고Profiler marker23과구간총계가일치한다. 개별sample1:1귀속이나경고원인/전체진단문제해결은아니다.

잔여우선순위는CallerStack비용이며기존로딩/RPC도미해결로둔다. 통제전후개선율·전체성능PASS를선언하지않고새사용자테스트반복요청도없다. 읽기도구최종native추출후standalone DLL1개43,520B정리는완료했으며기존약39KB예정과중복삭제로세지않는다. 고유raw/source/결과/CSV/receipt는보존했다.


### 21.2 최종 분석 확정

시간축2,000행·11anchor·1688+1+311구간과Warning23구간총계검산을완료했다. 두번째경기말기56.36초+경계한프레임+결과대기10.37초에서정책작동/콘솔긴지연미관측을확인했다. 로드/첫경기/로비복귀검증은포함하지않고전체archive116경고와window23을구분한다. 개별sampleID1:1귀속·통제전후향상률·전체문제해결은판정하지않는다. 이번분석완료이며추가제품수정/빌드/재테스트요청은없다.


## 22. 로비복귀 진단제외 baseline과 단일병목 교정

사용자가문서작성및다음작업을승인했다. Dev로그최적화의작동확인은shipping성능개선입증과구분한다. **이절이21.1의CallerStack 우선순위를대체하는현행실행계획**이다. 과거7.9초/53ms 등은같은조건전후쌍이아니므로향상률근거로사용하지않는다.

| 단계 | 진행내용 | 완료판정/현재상태 |
|---|---|---|
| 1 측정구성 설계 | 로비복귀 상세Dev진단 compute+callerstack을제외할명시구성을설계하고제외범위/활성조건/표준실행과차이를확정한다 | 아직설계/구현안됨;게임writer·HP·권위·RPC·씬/입력계약보존 |
| 2 최소phase 계측 | 종료→결과→복귀버튼→사용가능로비의경계와필요시간/프레임/GC만연결한다 | 아직미구현/미측정;계측자체부담·경계정의확인 |
| 3 baseline3회 | 동일기기·역할·종료동작·빌드설정을맞추고맵/부하통제가능성을먼저확정한다. 복귀시간/최대완전표본·경계표본/>100ms/지원GC와실행식별을저장한다 | 아직실행안됨;현재UI의동일맵고정은미지원근거상가정금지. 무작위3회는분포관측이며22.5의비교한계적용 |
| 4 병목하나 교정 | baseline에서확인된가장큰병목하나만구체설계·구현·필요검증한다 | 대상미확정;추정으로async/pool/씬수명변경확대금지 |
| 5 동일조건후측정3회 | 같은측정구성/조건으로3회재측정해동일지표를대조한다 | 아직실행안됨;조건불일치면개선율보류 |

진단부담제외와최소계측의구체설계결과를받은뒤파일/조건/검증절차를채운다. 현재이문서는실행승인과순서기록이며설계완료/새측정/제품성능효과가아니다. 고유raw·빌드/기기/역할식별은private보관계약으로유지한다. 신규Task/Testcase/메모리범위확대는없다. 통제비교가끝나도이번측정구성과로비복귀범위의결과로한정하며전체shipping성능/장시간발열·GPU/모든역할수용을자동확정하지않는다.


### 22.1 source 조사에 따른 설계 제약

Research25.1의실제source에서RootPose자동생성/초기화와UAS의관측구독/history가확인돼sink차단만으로진단compute제외를달성하지못한다. 기존전역측정mode는확인되지않았으며새구성설계가필요하다. Release표현에도쓰이는bridge Coordinator/IsActive/authoritative emitter는통째disable하지않고관측전용계산과분리해야한다.

복귀완료는ShowLoading(false)요청과실제Hide를구분하고UIManager의1초대기/동기LoadScene을포함한다. HasForegroundModal만으로legacy overlay/modalowners를포괄할수없으므로사용가능로비의입력/로딩완료조건을정확히설계한다. 현재이두범위는조사중이며코드수정/구현/새측정은없다. shipping동일mode나완료를일반론으로선언하지않는다.


### 22.2 최소phase 계측 승인·구현중 상태

Research25.2의6cs(+신규meta)범위로최소phase계측을구현중이다. 신규Infrastructure helper→GameEndUI Begin→LobbyRootView InitReady→UIManager 실제LoadingHidden/전체blocker InputReady,Exporter 정적marker/manifest를연결한다. AndroidDevnonEditor 시작1회private opt-in파일존재로활성화하며실패시비활성,Editor/Release 설정I/O없음이조건이다.

Begin/InitReady/LoadingHidden/InputReady와timeout·재진입·다른scene Abort를구분하고고정소량private NoStack Info만남긴다. 게임/1초대기/네트워크cleanup은유지한다. 상세진단분리는별도미완이며Release표현bridge보존·terminal구독해제항상실행을설계조건으로유지한다. 이번계측은22절단계2의준비작업이며진단제외baseline완료가아니다. 코드완료/검증결과수신전성공으로쓰지않고새빌드/사용자실기요청도아직없다.


### 22.3 phase 구현/정적검증 상태와 다음 선행조건

Research25.3의6cs+meta구현과5standalone csc·AndroidDev actual-source12case·UI snapshot2,048case각3구성검증을완료했다. 실제Unity/native/Android실기는아직미실행이다. phase의Frame/RealtimeMs/ElapsedMs/최종snapshot을연결하되clock epoch동일을가정하지않고DetailedDiagnostics=Unchanged를유지한다.

이계측만으로진단제외baseline준비완료가아니다. 상세진단분리의구체source계획은조사중이며추가코드변경/빌드/baseline측정·기기테스트요청은아직없다. Release표현bridge보존과terminal구독해제항상실행조건을유지해구체안을확정한뒤다음단계로간다. 메인이검증완료중간DLL/PDB10개9,376,640B를안전정리했고원본/소스/검증근거는보존했다.


### 22.4 현행 구현안 — Release opt-in 최소측정으로 대체

상세Dev관측자별가드대신Research25.4의Release opt-in안으로확정했다. helper+manifest2파일만확장중이며기존게임표현/권위/HP/RPC는보존한다. Androiddefine 명시toggle(다른define보존)·Release빌드옵션preflight와privateflag의동시조건으로활성화하고일반Release/Editor추가I/O는없다. 일반File BuildAndRun경로를사용한다.

구간중bounded4,096/60초 numeric샘플만모으고종결후고유private폴더CSV/receipt를쓴다. overflow는비교부적합,Update간격은CPU시간이아님,종료crossing추가표본/partial은완전프레임과별도판정한다. GC0횟수와지원검증된Used/Reserved만보고하고미지원null·GC.Alloc/GC시간/CPU-GPU미측정·FrameTimingStats0을명시한다. 빌드/metric식별·샘플수/SHA와저장비용을연결해baseline3회비교전지표적합성을확인한다.

현재Dev앱의adb경로접근만확인됐고새Release회수/hash·실기효과는미검증이다. 실패시회수불가로남기며권한우회/공유UI추가를하지않는다. 아직2파일구현중/빌드미시도/새실기미실행이고실행결과수신전완료를기록하지않는다. Testcase새작성은없다.


### 22.5 실기절차 전 통제조건 검토 — 동일맵 전제 정정

Research25.5에서싱글/멀티Host의MapRootSeed.Create경로와RandomMap규범의테스트모드제거를확인했다. 현재UI로맵을고정가능하다고가정한22절의동일맵3회전제는정정한다. 사용자에게불가능한조작을요청하지않고seed강제/옛테스트모드복원으로범위를확대하지않는다.

기기/역할/종료동작/빌드설정외에맵·부하의구체통제/재현가능성을실기절차전에검토한다. 무작위맵3회라면동일입력A/B가아닌시나리오분포관측으로보고하며확정향상률/통제baseline완료를선언하지않는다. 비교계획이충분하지않으면먼저보완하고사용자반복재테스트로채우지않는다. 현재mode최종검증중이며새빌드/실기요청은없다.


### 22.6 Release 준비 구현/정적검증과 실제 실행 순서

Research25.6의2파일Release확장·6compile·Release11/BuildPolicy134/기존Dev12·UI snapshot회귀는완료했다. 실제Unity/native/메뉴·빌드·기기Release/효과는미확인이다. 현재define/빌드옵션/기기flag는변경하지않았고사용자테스트도요청하지않았다. 현재계획의다음실행순서는아래와같다.

1. 실제Unity컴파일갱신과측정toggle메뉴를확인한다. 다른define을보존하며 `HEXIEGE_LOBBY_RETURN_MEASUREMENT`를명시ON한다.
2. Development/ScriptDebug/Autoconnect(ConnectProfiler)/DeepProfile을OFF로확인한Release설정에서일반File BuildAndRun을사용한다. 시도와성공/설치는구분한다.
3. 새Release앱의private opt-in flag를활성화하고앱을재시작한다. 준비실패는측정비활성으로판정한다.
4. **경기전에** 새ready.json을회수해새빌드/Release/수집mode/지표/준비상태를검증한다. ready SHA가attempt receipt에연결되는지확인한다. 회수불가/지원미확인은명시하고경기를먼저반복하지않는다.
5. 실현가능한고정role·복귀방법/종료동작·빌드조건으로시나리오를관측한다. 무작위맵3회를동일입력통제A/B로부르거나향상률을약속하지않으며22.5의맵/부하검토를유지한다.

수집자료는완전Update간격·endcrossing/outside/partial을나눠읽고CPU/GPU시간으로표시하지않는다. GC지원/단위미확인항목은Unavailable/null,overflow/abort/missingcrossing은비교부적합이다. 이순서는준비계획이며실행결과가아니다. 이번완료중간산출12개11,123,296B와이전phase10개를합친재개작업정리는22개20,499,936B이며원본/검증근거를보존했다.


### 22.7 실제native 준비/빌드 시도 이후 현행 상태

Research25.7의실제Unity컴파일갱신·측정메뉴체크ON·DevBuild/Autoconnect해제·ScriptDebug/DeepProfile OFF·기기privateflag0B준비를완료했다. define은실제메뉴체크로확인했고디스크persist까지검증했다고표현하지않는다. 실제File→BuildAndRun 클릭시도를완료했고이후UI/로그/프로세스/APK진행/완료감시는없다. 빌드성공/설치/실행/Release ready검증·효과는미확인이다.22.6의미실행/설정미변경은그시점이력이며현행은이절을따른다.

다음은사용자빌드완료보고→새ready.json의빌드/Release/수집mode회수검증→짧은1회수집확인순이다. 그후에만랜덤맵과통제가능조건에맞는실기절차를확정한다. 현재3회테스트요청은없고동일맵A/B향상률도약속하지않는다. 추가source변경/빌드감시는하지않는다.


### 22.8 첫 싱글 자동수집 결과와 판정 경계

Research25.8의새Release ready/attempt원본3종·SHA체인·phase완결을메인이확인했다. 싱글1회자동저장확인은PASS이며후속QA의4hash/31행연속/4phase/END/통계독립검산도완료했다. duration1154.127653ms는의도1초대기포함,완전29간격최대41.066115ms와종료crossing164.472423ms(frameGap2/안하한151.216192+밖13.256231)를분리한다. GC0차이1/UsedReserved31reads지원만확인했고GC시간/할당·CPU/GPU병목은미측정이다.

이결과의comparisonEligible는기술완결성이며로비전체성능향상/동일맵A/B/멀티수용은아니다. 새코드/빌드/추가실기요청은없으며독립수치검산까지이번저장확인을마무리했다. 고유첫Release3원본6,645B와ready/영수증은필수비교근거로보존한다.


### 22.9 저장 검증 마감과 다음 진단 범위

Research25.9의독립검산으로첫Release싱글1회의수집체인/기술완결성/수치검증을마감했다. GC는종결근처counter delta1이며정확발생시각·소요·164ms원인은미확정이다. row의crossing모호성과receipt의종결근처값을혼동해outside GC라고추정하지않는다.

다음은기존Dev소유자자료/현행source로끝경계비용을진단하는범위다. 새플레이/빌드/제품교정은요청하지않고개선율도확정하지않는다. 첫Release원본/ready/독립영수증은비교필수자료로보존하며새정리대상은없다.


## 23. 복귀 세부 경계와 전후 기기 맥락 수집 계획

Research26절의사용자승인에따라기존Release측정mode에1초대기·씬Load호출·로비초기화경계를추가하는안을먼저조사한다. 목적은첫싱글끝경계의151ms안쪽하한을분리하는것이며동기씬로드/의도대기/입력·게임동작은유지한다. 구체코드안은조사중이고구현·검증·새빌드·측정은아직없다.

| 준비 항목 | 범위/판정 경계 |
|---|---|
| 경계 source 조사 | 의도대기시작/끝·동기Load호출전후·로비초기화·기존InputReady와연결할정확지점확정;수치가CPU owner시간이라는가정금지 |
| 최소계측 검증 | 기존mode/비활성경계·저장체인·기존동작보존을확인한뒤실제Unity/기기검증구분 |
| 전후 기기맥락 | ADB읽기로thermalstatus/배터리·충전/절전/가용CPU정보를측정전후별도보관;미지원명시·과거부재소급추정금지·배터리온도와SoC구분 |
| 비교조건 | 동일기기/빌드/role/충전조건우선;랜덤맵/부하동일A/B가정금지;설정/상태변경을측정과구분 |
| 기존자료 | 첫싱글원본/ready/hash체인보존;164ms단일CPU/GC원인확정금지 |

현재targetFPS=-1/vSync0·약33ms는Android기본30fps pacing과구분해야한다. 이간격자체를최대성능한계로해석하거나FPS설정을자동변경하지않는다. 구체계측/상태회수안이확정되면기존문서에이어기록하며지금새측정완료/효과를선언하지않는다. 새Testcase작성은없다.


### 23.1 확정구체안 구현중과 기기지원 확인

Research26.1의helper/UIManager/LobbyRootView3파일에서6추가phase·buffer32·long ticket 구분을구현중이다. 의도대기/동기Load/게임순서는유지하고InputReadyEnd는연장하지않는다. 동일ticket저장대기후속점만받아누락은NotObserved로남기며phase배타합산/UGS작업완료로오인하지않는다. 구현·검증완료수신전성공으로쓰지않는다.

ADB5query로KST11월5일01:01시점의thermal/battery/절전/CPU현재값읽기지원을확인했다. 이는전후측정자료가아닌현재준비조회다. 다음측정전후별도로조회해같은기기/빌드/role/충전조건과함께해석한다. 온도/주파수만으로성능원인이나쓰로틀을확정하지않으며새빌드/사용자테스트요청은아직없다.


### 23.2 phase 해석 규칙

Research26.2의Unity6.3공식LoadScene설명에따라CallBegin→Returned와Returned→LobbyStartEntered를구분한다. LoadScene은다음프레임완료의semi-asynchronous동작이므로호출반환구간을전체전환CPU시간으로쓰지않는다. '기존동기Load API유지'는LoadSceneAsync로바꾸지않는다는범위표현이다.

역순/overlap phase를배타합산하지않고Single mode자동UnloadUnusedAssets는가능한engine정리근거로만다룬다. 이번164ms의원인확정이나GC시간으로바꾸지않으며wall분리후에도CPU self/GC시간미측정한계는유지한다. 새counter/코드범위확대는없다.


### 23.3 구현/정적검증 완료와 native 대기

Research26.3의최종3source·baseline원문동일·6csc·Release11/추가최종9case·DevMarker/Dev12/UI snapshot검증과QA독립source/결과리뷰를완료했다. sceneName Lobby ticket만관측하고고정End/phase32/저장대기후속점·Game/Login기존Wait1/Load1·UGS비동기dispatch를보존했다. 현재nativeUnity갱신확인중이며새빌드/실기효과는미확인이다.

추가경계는wall구간관측이며배타CPU합산/UGS완료/164ms원인확정으로쓰지않는다. 다음native/build결과를받아갱신하고실기ready검증전반복테스트를요청하지않는다. 이번checkpoint완료중간12 DLL/PDB11,144,596B는메인이안전정리했고고유첫Release/기기context/소스및검증원문은보존했다.


### 23.4 native/빌드 시도 이후 현행 실행 순서

Research26.4의실제Unity최종컴파일·DLL/sourcehash·AndroidRelease옵션OFF·디스크define/기존flag존재를확인했다. 메인이실제File→Build AndRun을클릭해시도했고이후진행/완료감시는없다. 성공·설치·실행/newready/새기기효과는미확정이다.23.3의native대기/빌드미시도는과거상태이고현행은이절을따른다.

사용자빌드완료보고→newready/sourcebuild/Release mode 확인+당시pre기기context수집→Single결과에서로비복귀1회→postcontext와phase회수→세부wall구간분석순이다. 기존준비thermal스냅샷을새측정전상태로사용하지않는다.3회반복·동일맵향상률수용을요청/약속하지않고첫Release기준과CPU/GC원인한계를유지한다. 추가source변경/빌드감시는없다.


## 24. 추가경계 실기 결과와 다음 원인 조사

Research27절의싱글1회ready/31행·10phase·hash체인은완결됐다. 의도대기1005.005ms,LoadScene호출반환0.144ms,반환후Start진입까지133.067ms,Start본문0.831ms를분리했다. Start본문그자체는133ms뒤에있어직접원인에서배제되지만로비UI전체/engine활성화·이전씬정리·Awake/OnEnable·callback·대기는미분해다. LoadScene반환을씬완료로보지않고CPU/GC원인을확정하지않는다.

다음은현재source/기존자료로이133ms후보경로를읽기진단하는범위다. 추가코드/빌드/사용자테스트요청은없다. endcrossing149.274ms는gap2/경계포함이며완전간격최대42.295ms와구분한다. thermal0등post는저장mtime뒤약458.623초조회여서즉시post/복귀시열제한없음으로판정하지않는다. 무작위1.154→1.139초차이에개선율을붙이지않는다. 고유원본3개7,401B·ready/context/분석근거를보존하고새정리대상은없다.


### 24.1 읽기 진단 마감과 교정 미확정

Research27.2의source조사에서Start본문VM/Bind/UGS dispatch의직접133ms원인은배제했지만Awake/OnEnable/TMP/layout·engine씬통합/이전씬정리·자동UnusedAssets/GC·callback·OS/frame대기는미분해후보다. 명시GC호출부재를engine자동정리부재로읽지않고현재lazyRanking에옛10rowclone비용을적용하지않는다.

기존Dev숫자를이번Release에비율/합계로환산하지않는다. 단한번수집은성능개선/기기역량부족의입증이아니며현재안전한제품교정대상은확정되지않았다. 추가코드/빌드/테스트요청없이현재읽기진단을기록하고원본/준비·수집/기기context근거를보존한다.


## 25. 시스템 trace 파싱 준비

Research28절의5초무조작수집시험은경로권한실패후공식perfetto출력경로에서성공했다. 이는수집도구준비단계이며실제복귀/133ms원인측정이아니다. 로컬trace_processor가용성·CPU/thread coverage·loss/지원상태를파싱검증한뒤다음범위를판단한다.

현재제품코드/설정/Git/빌드/추가사용자플레이요청은없다. 필요도구다운로드/설치는메인검토후진행하며raw시스템trace를외부서비스에업로드하지않는다. 실제payload검증전무손실/GC소유자/성능원인확정을하지않는다.


### 25.1 준비 검증 완료 범위와 후속 조건

Research28.1에서portableparser·기기config·회수/파싱·앱메타/Unity스레드이름·system sched/freq/clock자료까지확인했다. 앱sched/thread_state0이므로실제앱running/runnable/sleeping 귀속이나GC함수stack·133ms원인은미확정이다. Dozing은별도조회시점값만인정한다. 도구실제버전은v58.2이며v59.1wrapper pin과구분한다.

후속은Single→설정포기→결과화면대기가가능해긴전투를요구하지않는방향이다. 실제세션은준비알림후trace시작/복귀버튼1회로계획하지만아직사용자테스트요청없고기존부하동일/133ms재현을보장하지않는다. QA최종검수후경계를확정하며중간raw2개삭제는아직미실행이다. 제품/빌드/보안설정변경없이private로컬원본을보존한다.


### 25.2 준비 마감과 보존 상태

Research28.2의QA독립SQL/도구manifest검수까지완료했다. 지원준비PASS와앱run/wait/GC/133ms원인미확정을구분한다. 앞2idle중간raw로컬2개841,410B·원격중복trace/config5개1,309,013B는메인이SHA대조후정리했고최종namedtrace와모든설정/SQL/로그/영수증/도구·기존gameplay기준은보존했다. 삭제된2raw는재파싱불가한시험중간물임을명시한다.

Single포기후결과화면은자동복귀없이대기가능한것을source확인했다. 사용자준비후짧은복귀1회추적을다음에안내할계획이며아직추가요청하지않았다. 기존전투부하와같다고하지않고실제복귀원인분석/기능교정/성능효과를완료로기록하지않는다.


후속운영config `return-capture.pbtxt`636B/SHA `3167C046FA38CD010C3A9F006B91D2698EE9D86F1FAD41C0F4F21D60650FD7C4`와 `return-capture-operation-receipt.json`을대조했다. 검증idle config에서60초/16MB/input category추가3변경만있으며역치환원문exact다. 현재진행중인trace/새빌드는없다. 다음은사용자폰Single→설정포기확인→결과대기알림뒤메인이현재PID를재조회하고config stdin/--background의실제시작성공을확인한후복귀버튼1회를안내하는순서다.60초내미재현이면대상전환미수집으로판정하고무한재시도하지않는다. 이준비가실제인과분석은아니며원인/정밀clockjoin완전성은미확정이다.


## 26. coverage 실패에 따른 수집 설계 교정

Research29절의앱새측정은완결됐지만60초16MB RING trace는약84.7MBoverwrite로마지막9.96초만남아새복귀와시간범위가맞지않는다. 수집설계오류로기록하며사용자책임으로전가하지않는다. zero loss severity만으로수용하지않고bytes/chunks overwrite·실제보존clock범위·새attempt귀속을검증한다.117ms CPU/GC원인이나개선율은확정하지않는다.

현재private8초32MB DISCARD/input 구성과기본DryRun·명시Execute runner를준비중이다. 깨어있는결과화면/PID/스크린샷SHA·좌표검증후같은ADB실행으로수집시작확인+복귀1tap→bounded회수→capacity/clock/newattempt검증순이다. 아직검증/실행전이며지금추가플레이요청/폰조작/빌드·제품변경은없다. 실패trace/고유앱원본/모든분석근거는보존한다.


### 26.1 교정 도구 준비와 실행 미완 경계

Research29.2의최종privateconfig/runner와30case/Mock·순수guard검수,실제Android구문/일부guard검사를확인했다. 기본DryRun·명시Execute/스크린샷SHA·좌표/awake/keyguard/displayfocus/PID/session/source검증으로failclosed하며현재FullPreflight true/trace/tap은미실행이다. 기존60초16MB RING안은폐기권고로대체한다.

수집후에도accepted false를유지하고capacity/clock범위·새attempt/hash/정밀Unityclock검토전인과분석을하지않는다. 정수초tap/uptime은117ms정밀join근거가아니다. 즉시사용자플레이요청은없고다음준비확인후원하면사용자결과화면준비→메인검증/시작/단일click절차로진행할계획이다. 제품/Git/빌드변경·새수집없으며실패원본과앱측정/검증근거를보존한다. QA최종동결은후속으로받는다.


## 27. atomic 복귀 수집 이후 검증 순서

Research30절의명시승인/실제화면검증뒤단일tap·8초수집과유일새attempt회수를완료했다. DISCARD용량통계는손실0이고새기록이시스템범위에들지만Unityphase정밀clockjoin·앱chain독립QA/CPU분해는대기다. accepted false를유지하고원인/성능효과를확정하지않는다.

다음은기존수집자료의정밀시계/owner검수이며추가사용자테스트/tap·코드/빌드는없다. post는save뒤약4분35초이고prethermal미수집이라당시열상태를판정하지않는다. 실패60초자료와이번고유trace/app원본을보존하고과거결과를새성공으로대체하지않는다.


### 27.1 최종 분석 상태

Research30.1의독립검산으로수집검증과조건부분해를마감했다.12개clock후보라는모델범위에서119.839231ms 중Main Running약90.642〜92.240ms가있어단순scheduler대기만보다실행비용성분을지지한다. 유일정렬/CPU self/C#함수/GC시간은아니며후보별min/max를합산하지않는다.

Main함수slice가없어구체GC/Unload/Destroy/Awake원인은미확정이다. Gfx표식/늦은post기기조회로원인이나throttle부재를확정하지않는다. 현행상태는수집검증완료·조건부분해완료·함수원인미확정이며새제품교정/빌드/추가플레이요청/raw정리는없다. 다음결정을위해현재근거와조건을보존한다.


## 28. 현행 우선순위 — 밀집전투 대상 검토

사용자방향에따라로비/화면전환후속을보류하고밀집전투병목을우선한다. 현재기존자료/source검토중·대상미선정이며전문가선정결과를받기전교정파일/효과를임의확정하지않는다. 후보별실제호출·지배비용·role/build/부하·게임권위보존과검증가능성을검토한뒤구체안을기록한다.

| 상태 | 현행 범위 |
|---|---|
| 기존구현/검증 근거 | Research2/3절의NCC·추가코드,11/20〜24절의실제저장범위/로그정책근거를재사용하되전투전체개선으로격상하지않음 |
| 효과확인 제한 | 통제A/B/동일부하shipping성능미입증·Dev로그교정/CLR과실기효과구분 |
| 구현미선정 | 새밀집전투교정대상·구체파일/테스트는선정결과대기 |
| 후속보류 | 화면전환함수/GC원인·유일clock/thermal미확정;자료보존·사용자재우선선정시방법부터검토 |

새작업을선정하거나사용자추가테스트/빌드를지금요청하지않는다. 화면전환보류는해결판정이아니며기존trace/실패/측정원본과한계를유지한다. 이후선정결과를받으면이절에서실행계획을구체화한다.


### 28.1 승인한 DetectRange boolean 조기 종료 계획

UnitCombatUseCase.cs1파일의HasEnemyInDetectRange/At만대상으로한다. private3인자overload에기본false stopAtFirstMatch를추가해boolean2개만true로넘기고기존거리/최소거리유효조건안최초unit/building반환으로후속읽기를줄인다. nearest공개2개와공격사거리·좌표fallback/null/zero·순서/동률·권위/HP/RPC는불변이다.

Units6/9와권위계약을유지하고provider읽기외진단/이벤트/쓰기부작용이없는범위로검증한다. true/false·unit/building·후보좌표/기본좌표·null/zero/경계/동률·nearest기존결과를대조해불필요조회생략과게임의미보존을구분한다. 기존Dev반복호출은선정근거일뿐실제true빈도/Release효과/최대병목은미검증이다. 새Testcase파일은만들지않고구현/검증수신전완료로쓰지않는다.


### 28.2 첫 전투 제품 교정의 완료 범위

Research31.2의1파일DetectRange boolean조기종료구현·실제8메서드CLR6,366case/59,872assert·negativecontrol·4구성Roslyn/메인·QA검수를완료했다. true시후속조회감소와nearest/false동등성은fixture범위에서확인됐으며실제true빈도/FPS·Release성능향상/최대병목은미검증이다.

실제UnityEditor compile/native기기효과는NOT RUN,이번사용자빌드요청없어새빌드시도/추가테스트요청도없다. 현재구현/정적검증완료와실기효과미확인을구분한다. 완료중간8 DLL/PDB8,283,028B는정리했고소스/하네스/응답파일/로그/영수증·고유원본은보존했다. 화면전환후속보류와추가대상임의선정금지는유지한다.


### 28.3 승인된 실기 비교 준비 — 설계 대기

사용자가비교측정준비와File BuildAndRun시도를승인했다. 한빌드에서동일조건read-only paired 비교로boolean동등/true빈도/provider조회량·조건부시간을확인하는안을조사중이다. 아직설계미확정/계측구현전이며수신뒤이절에구체조건/부작용·검증/결과수집을채운다.

paired계측의시간차를게임FPS향상으로바꾸지않는다. 최종검증결과와실제FileBuildAndRun클릭을받아시도·성공/설치·실기결과를구분하고사용자지시의완료감시없음을유지한다. 지금승인기록만으로구현/빌드/측정완료를선언하지않는다. 새TC/메모리/별도Task는없다.


### 28.4 승인한 단일빌드 read-only paired 비교 설계

3파일은UnitCombatUseCase의conditional observer/동결snapshot·실제private false/true kernel,신규DetectRangePerformanceDiagnostics(+meta)의owner/수집체인,기존manifest의전용define/toggle/buildguard/source등록이다. `HEXIEGE_DETECT_RANGE_COMPARISON`과private `PerformanceMeasurements/EnableDetectRangeComparison`을동시에사용하며로비측정과독립이다. AndroidRelease Single/Host만허용하고Client/일반Release/Editor는비활성/0I/O다.

256query간격·256쌍/60초bounded로같은좌표snapshot의baseline/optimized actualprivate탐색을AB/BA교대한다. true/false·방문/조회수·kernel시간을수집하고snapshot생성/captureMs는별도다. fallbackskip/overflowinvalid·예외격리·경기중I/O0·ready→CSV/END/hash최종체인을검증한다. 게임은원래optimized권위값만사용하고HP/RPC/씬/시간수치는유지한다.

4compile/actual-sourcefixture·메인native확인뒤FileBuildAndRun시도,새빌드/경기1회로실기표본을수집하는안이다. 현재구현/검증전이며실제Transform비용절감/FPS향상은비교kernel결과로주장하지않는다. 구조가승인범위보다커지면메인에게보고하고추가확장전멈춘다. 새Testcase/별도Task는없다.


### 28.5 최종 계측 구현/검증과 빌드 전 상태

Research31.5의3cs+meta/최종hash·regression6,366/59,872·owner36checks·6csc/QA검증을완료했다. 수집시작은첫true적감지로수정했고이후false포함256stride/256pairs/60초cap은sampling만종료한다. GameEnd후Update에서만저장하며warmup2제외후시간표본부족은NotExercised다. completed/END는수집완결성이고효과PASS가아니다.

메인은private전용flag와Unity DLL갱신·새메뉴/토글ON을확인했지만최종define compile진행중/디스크persist확인전이므로빌드클릭전이다. 최종compile/옵션검증→FileBuildAndRun시도후그결과를받아갱신한다. 새기기ready/실기효과는미확인이다. snapshot생성비용/캐시kernel/ABBA편향한계를구분하며실제Transform/FPS이득을주장하지않는다. 중간12 DLL/PDB11,159,624B정리와모든원본/검증근거보존을확인했다.


### 28.6 빌드 시도 이후 사용자/회수 순서

메인의실제D Unity/DLL·최근errorCS0범위·Comparison메뉴ON과Android Release옵션/기기/APK설정을확인한뒤FileBuildAndRun클릭시도를완료했다. 당시디스크olddefine과in-memory체크/compile증거는구분한다. 클릭후UI/로그/프로세스/APK/ADB감시없으며성공/설치/실행/newready는미확인이다.

다음은사용자기기새앱실행보고뒤메인이ready buildGUID/mode를확인한다. 이후싱글AI또는AndroidHost1경기에서처음적감지true부터60초를관측하고정상종료/포기GameEnd후결과화면에서앱을유지하면메인이CSV/END/hash를회수한다. ProfilerSave는필요없다. kernel한정시간/조회량·bool동등과snapshot비용·시간표본부족을나눠판정하고FPS향상으로환산하지않는다. 새TC작성은없고실기결과전완료를선언하지않는다.


### 28.7 새 ready 확인과 경기 결과 대기

Research31.7의새buildGUID/ReleaseOptInDetectComparison·IL2CPP/isDebugfalse·전용define/stride256/cap256/60초/Stopwatch10MHz를확인했다. startup started/completed false·role빈값은현재실행상태판정에사용하지않는다. pre기기context를회수했으며실기CSV/END/hash/역할·동등성/valid시간표본은대기다.

안내예정은싱글AI1경기에서첫교전후최소60초진행하고정상종료/포기후결과10초앱유지다. 새기기명령/테스트는이문서작업에서실행하지않았고기존kernel/FPS해석한계를유지한다.


### 28.8 실기 검증 마감과 효과 한계

Research31.8의5파일/18chain·54행status0/liveboolean동등/overflow0·TimeLimit완결을확인했다. warmup후52행중true1에서조회8→6/inspection21→19감소, false51의조회/inspection동일을실기관측했다. candidateAt는0건이라검증확대하지않는다. 전체true401/false13,358은stride표본과별도로해석한다.

AB20빠름/6느림과BA4빠름/22느림의순서영향때문에kernel합0.2531→0.2073ms를개선율/FPS효과로쓰지않는다. pre/post열스냅샷도연속쓰로틀을배제하지못한다. 이번작은구현은유지하고boolean동등/조건부조회감소실기범위에서마감한다. 체감성능/최대병목효과는미입증이며추가사용자재테스트를자동요청하지않는다. 다음병목은기존근거우선검토후별도선정하고현재고유원본을보존한다.


## 29. 다음 전투 느린 프레임의 기존자료 분석

Research32절에따라10월30일14:10 원본/hash/phase를재사용해diagnostic stack·GC·Wait와제품CPU를분리한다. 알려진상위3진단프레임외NCC raw1510/render raw1419를후보로검토하되자식self없이는큰병목으로선정하지않는다.

동일원본의최소2hotspot추출안을프로그래머에게받아필요읽기Exporter 메뉴범위를메인이승인한뒤기록한다. 현재시작/검토중이며제품수정·새계측/빌드/기기테스트·원본삭제나새TC는없다. 비용/완료/교정대상선정은근거수신뒤판정한다.


### 29.1 승인된 1파일 읽기추출 계획

Research32.1의Exporter1파일새메뉴로기존14:10-35원본raw1510/1419만추출한다. StartSplitExport의선택sourceIndices/rawFrames/receiptPrefix를재사용하고기존메뉴default·원본readonly·각guard·1target/update·Load/hash/bytes/raw/output/END연결을보존한다.

순서는계획기록→1파일구현→actualSDK Editor csc1→메인실제UI실행→2출력/원본귀속검수→자식self/진단·Wait와제품비용판정이다. 구현/추출완료는결과수신뒤기록하며runtime/씬·빌드·기기테스트/새capture는없다. 현재큰제품병목미확정·ReleaseFPS비율환산금지는유지한다.


### 29.2 기존자료 분석 마감

Research32.2의읽기Exporter정적검증/실제Unity2hotspot완주·원본/출력체인을확인했다. NCC raw1510의self5.789ms는게임/진단혼재로제품단일원인미확정,render raw1419의Finish19.081ms에는Wait약13.995ms가포함돼전부CPU교정대상이아니다. 중첩합산/GPUdriver원인단정·ReleaseFPS환산은하지않는다.

현재자료에서는추가제품교정할큰병목을확정하지못했으므로이분석을마감한다. 억지코드수정/새빌드/사용자재테스트는없다. 전체성능무문제/Release30FPS보장을뜻하지않고기존범위/계측한계를유지한다. 완료중간DLL/PDB2개1,122,048B정리와고유원본/검증근거보존을확인했다.


## 30. 멀티1회 고부하 수집과 사용자 절차 — 설계 대기

Research33절의사용자승인에따라AndroidHost+EditorClient 고부하1회수집을보완한다. 연결확인→새빌드/role/mode ready검증→수동고부하시작→정의된전투구간→END/체인회수의최소안을전문가가조사중이다. 아직구현/실행준비완료로쓰지않으며구체절차확정전사용자에게불가능한조건을요구하지않는다.

설계수신후이절에사용자용표로사전실행준비·실제조작/시나리오·관찰조건·종결/회수·부족자료판정을채운다. 새Testcase파일은생성하지않는다. 일반Release전체Android로그지원은별도확인하고Detectkernel/전체프레임을분리한다. GPU원인을로그만으로확정하거나한경기로모든회귀/일반Release성능을보장하지않는다. 현재설계미확정으로새제품변경/빌드·기기실행은없다.


### 30.1 승인된 Host 수동90초 계측 구현·사용자 절차

Research33.1의7파일+meta와별도private collector안을확정했다. 기존v1은보존하며v2 Host전용flag/nonce/build·mapHash/MapVersion/NCCid/reference/IsSpawned가같은command만1회소비한다. 준비1Hz poll/ACK뒤수집중I/O0,90초cadence·NCCwall동일frameID·양팀alive1Hz/activeTarget/damage계수와선할당buffer를사용한다. role/경기·scene/NCC변경/overflow는abort·불충분판정이다. optionalRecorder는ready의실제probe결과만사용하며CPUself/FPS원인과구분한다.

구현순서는7파일계약/기존동작보존→static/actualsourcefixture/role·수명·IO/identity·bounded검증→메인실제Unity/native 준비→실제FileBuildAndRun시도(완료감시없음)다. 새실기/빌드는현재미실행이며구체계획반영뒤프로그래머가착수한다.

| 순서 | 사용자/메인 준비·조작 | 수용/부족 판정 |
|---|---|---|
| 사전준비 | 새앱AndroidHost+EditorClient 연결. 메인이양쪽actualready·active logpath/offset/probe/readback/anchor·build/map/NCC/token일치·command write/readback을검증 | 누락/역할오류/불일치는시작하지않음. v1flag는v2활성근거아님 |
| 부하준비 | 사용자양팀전투준비. 메인이ready에서합의한규모/교전조건을시작전확인 | 실제조건미충족이면고부하수용PASS금지;구체수치는후속구현/검증자료로확정 |
| 수동시작 | 준비알림뒤메인명시stage로command전달·ACK검증. 그뒤90초교전관측 | ACK전시작가정금지. 수집중파일I/O0 |
| 정상종결 | 사용자가정상종료/포기후결과에서앱을유지 | GameEnd뒤저장/END대기. kill/missingEND는Incomplete |
| 회수/분석 | 메인CSV/END/hash·규모/교전충족·Editor offset원문/성장hash·PID bounded logcat회수 | 완결성과고부하충족후완료. kernel/프레임/NCCwall/지원counter별도해석·GPU/전체회귀보장금지 |

collector는private scratch의메인명시stage만start/stop하고logcat PID한정20분/128MB hidden·ringclear/appkill/무한재시도없음이다. 절단은불완전,일반Release전체Info/stack지원으로과장하지않는다. 원시자료는private보존하며새Testcase파일을만들지않는다.


### 30.2 사용자용 실행 초안 — 구현 검증 전

실제flag는 `EnableHostBattleCapture`이며구v1flag와분리한다. 아직old앱이라새ready/경기요청전이다. collector최종경로/메뉴는프로그래머결과뒤확정하고아래는절차초안이다.

1. 사용자새앱실행보고뒤메인이Host bootready를검증하고PID한정bounded logcat을시작한다.
2. Android에서커스텀방을만들어Host가되고Editor Play로Client가입한다. 사용자가'멀티입장완료'를알린다.
3. 메인은EditorClientReady메뉴로활성로그anchor/write-readback·동일mapHash/MapVersion/NCCid와Host probe ACK를확인한다. 누락이면계속진행하지않는다.
4. 양쪽생산을이어가며부대를준비하고교전을유도한다. 자기진영유효타일랠리는경유지일뿐정지집결기능이아니므로각20기정지보관을필수/가능조건으로요구하지않는다. 사용자는교전이준비되면'전투준비'를알린다.
5. 메인이Host identity/양팀alive>=12·최근5초damage gate와명령ACK를확인하고90초시작을안내한다. 생산/교전을유지하되each12·피해1Hz qualifying30bin은잠정가시부하이고최대밀집보장이아니다.
6. 90초뒤EditorClient의게임내포기로정상종결하고양쪽결과10초동안앱/Play를유지한다. 메인이END/3CSV hash·로그offset범위·실제규모/교전충족을회수해판정한다.

중단/aborted는측정을닫은뒤부분저장하고성능수용으로쓰지않는다. 강제종료는완결을보장하지않는다. 실제gate충족/기기상태는미검증이며현재새경기요청/실행완료는없다.


### 30.3 최종 검증 진행 상태

현재구현/QA중이며중복property의Comparison전용컴파일결함은검증중발견·수정후최종csc재실행대기다. Editor compileout경로라native메뉴만으로완료검증하지않는다. 빌드/실기/최종검증완료로기록하지않고프로그래머최종결과수신뒤갱신한다.


### 30.4 최종 구현/collector 검증과 실행 절차

Research33.4의7cs+meta/6csc·Host98/kernel6,366/59,872·collector local/mock6·QA검수를완료했다. native최종갱신/빌드클릭/기기효과는아직대기다. collector는 `.utmp/multiplayer-battle-20261105/collect_host_battle.py`의명시boot→prepare→EditorClientReady→probe→start→collect와bounded logcat-start/stop만사용한다. 메인이양쪽ready/probe/readback/identity와실제exact앵커를확인하고사용자에게다음단계를안내한다.

사용자흐름은새앱→Android커스텀Host/EditorClient가입→입장완료알림→메인준비/앵커검증→생산·교전준비→전투준비알림→양팀alive12/최근피해/identity 및startACK검증→90초교전→EditorClient포기→결과10초앱/Play유지→메인회수다. 랠리정지집결은보장하지않고잠정부하충족은실제자료로판정한다.

Host자료기술/부하수용과양쪽로그검증은독립상태로남기며한쪽로그누락으로Host자료를자동폐기하지않는다. 전체상관분석은hostTelemetryEligible/logsVerified 둘다확인후진행한다. interrupted/abort/missingEND는한계를명시하고강제kill완결은보장하지않는다. 중간13 DLL/PDB11,253,716B정리는완료했고원본/검증근거를보존한다. 최종native/빌드시도결과수신뒤현행상태를갱신한다.


### 30.5 빌드 시도 이후 최종 사용자 절차

메인이최종source/native·AndroidRelease옵션과Samsung선택을확인한뒤FileBuildAndRun클릭시도를완료했다. 이후UI/로그/process/APK/ADB감시없으며성공/설치/newready·멀티측정은미확인이다.33.4의native/빌드대기는이후이절의관측으로보완한다.

1. 사용자가빌드완료/새앱실행을알리면메인이ModeReleaseOptInHostBattle·새build/nonce·지원metrics를회수확인한다. static boot파일존재외freshness/requiresRestart false/PreparationTimeout아님을확인한다.20분지난세션이면새경기준비를진행하지않고재시작/새readiness를확인한다.
2. 그뒤Android커스텀Host·EditorClient입장과'멀티입장완료'알림을받는다. Editor실제role/로그path·probe/readback·map/version/NCC/token과Host ACK를확인한다.
3. 사용자는생산/교전을준비하고'전투준비'를알린다. 메인실제loadgate/identity/startACK확인뒤90초교전을안내한다. 랠리는정지보관이아니며잠정부하미달이면충족PASS하지않는다.
4. 90초뒤EditorClient게임내포기·양쪽결과10초앱/Play유지후메인이END/3CSV/hash·Host부하수용·양쪽로그검증을독립회수판정한다.

현재새앱/실제Client/멀티수집은미실행이다. 일반Release전체로그/GPU원인/한번으로모든회귀를보장하지않으며추가코드나빌드변경은없다.


### 30.6 timeout 수집오류 교정과 재시작 순서

Research33.6의실기준비실패/private초안을PlayOFF확인뒤기존Task에반영했다. startup20분owner폐기분기를제거해manual대기를유지하고초기dynamicboot검증·종결stream보존재시작/freshprobe검증을적용하는교정이다. 검증126Host/9collector·원본negativecontrol은private/source동일근거이며native/기기완료가아니다.

현행순서는새앱dynamicboot→방생성/입장→EditorClientReady anchor미리확보→생산/전투준비→메인fresh logcat-start→probe→ACK확인/90초start다. logcat20분cap은유지하고초기앱실행부터긴대기시간으로소진시키지않는다. 기존prefix보존·probe무효화후재검증/RequiresRestart·freshness검사는필수다. 이번실패로그와미수집판정은보존한다. 코드반영/native/빌드시도후실제상태를이어갱신하며새Testcase는없다.


### 30.7 교정 빌드 시도 이후 미완 검증

Research33.7의실제Unity PlayOFF/소스뒤DLL갱신과FileBuildAndRun1회클릭을확인했다. 클릭후UI/log/process/APK/ADB완료감시는없으며새Android설치/기동/timeout교정실측은사용자실행보고뒤미완으로확인한다. 기존Assets적용hash/검증과freshstream→probe→start절차는유지한다.

중간DLL/PDB5개3,617,984B는경계/hash검증후정리했고소스/검증근거·실패원본은보존했다. 새실기효과/수집성공을아직선언하지않는다.


### 30.8 멀티 수집 완료와 분석 범위

Research33.8의첫7.70초GameEnd abort/loadfalse와앱재시작후90초TimeLimit/loadtrue를분리했다. 새run은Host자료/양쪽로그기술검증이true이고2,700frame/900NCC/546damage·42sampled qualifying bin을확보했다. 앱owner는Save뒤닫히므로다음측정은앱재시작이필요하고로비재입장만으로재arm하지않는다.

독립cadence2698개평균33.346496/p9535.891346/최대39.5065ms·>50ms/>100ms각0, NCCwall평균1.392339/p952.9604/최대10.356ms다. 이번약30fps안정관측과수집교정유효성은확인됐으나전후개선율/전체성능보장은아니다. GPU최신완료sample의동frame귀속/병목·GCRecorder0으로pause부재를단정하지않는다. 42bin과후반Blue감소로연속90초밀집도미입증이다.

상세QA로그/paired검수는Research33.9에최종반영했다. 전투초기start·준비근처freshstream·앱1회수집운영을유지하고이번원본/첫abort/영수증을보존한다. 새코드/빌드/추가사용자테스트/원본삭제는없다.


### 30.9 최종 분석 마감과 후속 제안

Research33.9의두run한정로그검색0과pairs213동등을확인했다. true6의snapshot조회168→54와false조회동일은관측됐지만AB/BA시간편향이강해kernel시간개선율은불가하다. Release전체로그/전체오류0·첫GameEnd원인/항복여부수용으로확대하지않는다.

90초수집/통계는수용하고게임원인·전후효과는미확정으로마감한다. GPUlatest값과렌더/대기의동프레임연결을다음검토로제안하되현재새제품교정/빌드/사용자재테스트는필요근거없이추가하지않는다. 모든run/로그/영수증은보존한다.


## 31. GPU·렌더링 원인분리의 현행 계획

먼저지금설정과자료가무엇을측정했는지확인한다. 그뒤기기가지원하는수단으로GPU작업과렌더/표시대기를구분하고확인된원인하나만작게고친다. 사용자가설명한병력감소는전투패배에따른정상플레이다. 부하가시간에따라달라진사실을오류로바꾸지않으며최대밀집90초검증과도구분한다.

| 단계 | 할 일 | 완료 기준/현재 상태 |
|---|---|---|
| 1 자료·설정감사 | 기존Host90초/로그·그래픽API·품질/해상도·프레임제한·기기/빌드/현재세션을읽기로확인한다 | 조사중. staticready만으로현재수집준비를수용하지않음 |
| 2 지원확인과원인분리 | 기기GPU실측/렌더·표시대기지원수단을확인하고동일프레임/시계연결·capture용량/완결성을검증한다 | 구체수단미확정. latestGPU를cadence와자동연결하지않음 |
| 3 작은교정 A/B | 근거있는원인하나에한정해같은조건baseline/후측정을설계한다 | 제품대상미선정. 무작위맵/부하차이와계측부담을고려해비교불가면향상률보류 |
| 4 검증/보고 | 실제효과·화면/입력/전투회귀·지원지표/남은문제를분리한다 | 아직미실행. 한경기/로그만으로GPU원인·전체회귀보장금지 |

구체실기절차는전문가조사결과뒤이절에채운다. 시작전freshready/build/role/도구지원·write/readback과기준표본을확인하고,기기온도·충전/전력·절전/발열안정조건을전후같은방식으로확보한다. 배터리온도/지연post만으로SoC열상태를확정하지않는다. 사용자조작/교전부하·카메라/해상도·capture시작/끝을정의하고cadence분포/긴구간·동일프레임으로검증된GPU/렌더/대기·지원불가항목·수집완결성과부하충족을출력한다.

새빌드가실제로필요한안이확정되면검증후사용자선호대로FileBuildAndRun시도까지만하고완료감시는하지않는다. 지금은계획문서만이며코드/설정/빌드변경·새Testcase생성은없다. 근거없는성공수치/원하는개선율을먼저정하지않는다. 원인미확정이면수정하지않고한계를보고하며고유raw/비교기준/실패자료를보존한다. 불필요정리는확인된중간물만영수증과함께별도처리한다.


### 31.1 감사에서 확인한 설정과 지원검증 선행순서

Research34.1의Android Mobile pipeline/카메라/프로젝트디스크값을기준목록으로보존한다. 실제API/quality/해상도·refresh/targetFPS·pipeline/현재session이같은지runtime으로먼저확인하며PC설정을Android에대입하지않는다. FrameTimingStats0과명시API설정만으로실측GPU지원/선택API를확정하지않는다.

지원probe가성공한수단으로동일frame/clock연결과GPU완료·CPUwait를구분한뒤pass owner를조사한다. 그림자/overdraw/VFX/후처리는검토후보일뿐일괄비활성화하지않는다. 확인된원인하나를선택한뒤단일변수A/B와화면·전투회귀를검증한다. 지원/clock이불충분하면그한계를기록하고억지교정하지않는다. 현재문서계획/읽기조사만이며코드/설정/빌드/사용자새테스트는없다.


### 31.2 실제 측정 절차 확정 전 지원 가드

공식문서6000.0의GPUProfiler/FrameTimingManager제약을Research34.2에연결했다. 실제6000.3.5f2/기기probe는아직없다. 먼저runtimeAPI/품질·session을확인하고지원/정확한지연frame연결이가능한수단만선택한다. 실패하면Unavailable로남기고CPUwait+pass구조감사로후퇴한다. 임의API교체/툴설치·GPUProfiler성공강요는없다.

사용자절차확정시같은Host/build/화면·줌/해상도/교전량band와열/전력/주사율기준표본을먼저확보한다. 시작ready/probe·현재session/anchor→bounded수집→END/지원/부하/clock검수후한변수A/B를검토한다. 무작위맵/정상패배부하변화는조건차이로기록하고원하는개선율을미리정하지않는다.60fps/VFXoff/피해변경을baseline으로강제하지않으며시각품질변경은별도승인후다룬다. cap/지원실패/role·scene변경/END누락은부분자료보존·미검증이며준비중절대deadline재도입은없다. 현재문서/읽기계획만완료했고새코드/설정/빌드/실기는없다.

### 31.3 2026-11-11 병합 후 재개와 검증 대응

최신 main fast-forward와 기존 변경 재적용을 수행했다. 백업·충돌·보존 검토의 실제 범위는 Research34.3을 따른다. 문서 충돌은 해결했지만 폰트와 자동 병합 코드의 최종 검토는 진행 중이므로 전체 병합 검증 완료로 기록하지 않는다. GPU 신규 구현은 아직 없으며 최신 소스에 맞춘 최소 계측안 확인부터 재개한다.

새 WORKFLOW의 테스트 항목별 진단 로그 대응 요구를 후속 절차에 적용한다. 아래는 구현 전 검증 설계이며 존재하지 않는 새 로그나 지원 지표를 완료 근거로 쓰지 않는다.

| 검증 항목 | 확인할 진단·자료 | 판정 경계 |
|---|---|---|
| 실제 빌드·그래픽 설정 | 새 계측안에서 확정할 runtime identity/ready와 설정 hash | 현재 세션·API·품질·해상도를 확인; static 파일만으로 준비 완료 금지 |
| GPU 지표 지원·프레임 연결 | 지원 probe 결과와 frame/clock identity | 실패는 Unavailable; latest 값을 동일 프레임으로 자동 귀속하지 않음 |
| 교전 중 렌더·대기 원인 | bounded 측정 CSV/END·NCC wall·교전량과 필요 로그 anchor | cadence와 CPU self를 구분하고 지원되는 자료만 해석 |
| 한 변수 교정 후 비교·회귀 | 같은 조건의 기준/후측정 및 해당 동작 진단 로그·사용자 육안 | 아직 대상·로그명 미확정; 구체안 수신 후 실제 이름·확인 방법을 채움 |

현재 감사 근거의 FrameTimingStats는 0이고 기기 지원은 미검증이다. 설정을 임의로 켜거나 새 사용자 테스트를 먼저 요구하지 않는다. 최종 코드 검토와 계측 설계 확정 뒤 필요한 절차만 안내한다.

### 31.4 병합 완료 이후 현재 진행 상태

2026-11-11 후속 확인: `.utmp/main-merge-20261111/MergeReceipt.json`에서 HEAD와 origin/main이 `099e8a1b`, 브랜치는 `codex/android-performance-optimization`, 미해결 경로는 0임을 확인했다. 폰트는 main 299 glyph를 원문 설정·참조·pixel까지 보존한 local 301 glyph의 strict superset 검증 후 해결했다(`font-audit/SupersetProof.json`, `ResolutionReceipt.json`). 코드 5개는 QA 소스 검토에서 main 49개·local 44개 변경 블록 누락 0이며 Bootstrapper 인터페이스 추가 외 main 본문 유지로 확인됐다. 이는 소스 보존 검토이며 실제 Unity·게임 실행 수용과는 다르다. 백업과 autostash는 보존하고 push는 하지 않았다.

앞 절의 폰트/병합 검토 진행 중 상태는 이 결과로 대체한다. GPU 계측은 이제 3파일을 실제 작성해 검증 중이므로 앞 절의 신규 미구현 표기도 당시 상태다. optional CSV 저장과 collector 실패 격리의 QA blocker 2건을 교정 중이며 최종 완료·기기 지원·성능 효과는 아직 미확정이다. 초기 컴파일 오류 23건은 검사 하네스의 asmdef/DOTween 중복 편성 문제로 하네스를 수정 중이라는 전달 결과이며 제품 또는 병합 결함으로 단정하지 않는다. 최종 구현·검증 결과 수신 뒤 실제 변경 범위와 남은 상태를 갱신한다.

### 31.5 구현 검증 결과와 다음 진단 빌드의 선행조건

`.utmp/gpu-render-diagnostics-20261111/FinalVerificationReceipt.json`을 대조했다. 변경은 DetectRangePerformanceDiagnostics owner·PerformanceCaptureBuildManifest·private collect_host_battle.py 총 3파일이다. 환경 snapshot과 optional delayed FrameTiming의 raw timestamp/별도 CSV·SHA/정확 END/행수 검증을 추가했고 기존 Host core 3CSV/수용 판정은 유지한다. optional CSV 저장·회수 실패가 core END/HostEligibility를 무효화하지 않도록 QA blocker 2건을 해결했으며 동일 최종 3파일 hash 검수에서 신규 중대 이슈는 없었다.

actual-source Host 상태/파일 검사 176건, 로컬 실파일·mock ADB collector 7건, 기존 탐색 kernel 6,366case/59,872assert가 통과했다. 최종 SDK csc 6구성은 모두 exit0/error0이고 경고는 순서대로 24/27/27/31/31/0, 변경 C# 2파일 경고 0이다. SourceMembershipProof는 현재 Project 소스 351/351 포함과 asmdef/firstpass 제외 경계를 확인했다. 초기 중복 DOTween 편성의 잘못된 하네스 실패 로그도 보존하며 이를 제품·병합 오류로 간주하지 않는다. 이 검증은 fixture/standalone 범위로 실제 Unity native·Android 기기 지원 검증을 대신하지 않는다.

**완료는 진단 신뢰성 보완과 정적 검증이다. GPU 원인 분리나 제품 성능·VFX 교정 완료가 아니다.** 설정은 변경하지 않았고 FrameTimingStats는 여전히 0, native/device 실행과 빌드 시도는 0이다. 원본 캡처·실패 자료·6GB 복구 백업을 보존한다.

다음 실제 GPU 수집에는 Frame Timing Stats를 명시적으로 활성화한 별도 진단 빌드 결정이 선행되어야 한다. 기존 APK를 다시 플레이하는 것만으로 지원 probe를 반복하지 않는다. 실제 API/품질/해상도·빌드/현재 세션 identity와 지원 여부를 먼저 확인한 뒤 동일 진단 옵션의 baseline을 확보한다. 임의 API·품질 변경이나 자동 빌드는 하지 않는다. Unity 메뉴/실제 컴파일 확인, 설정 변경 범위 확인, 승인된 File Build And Run 시도 후 사용자 실행 보고와 새 ready 검증 순서로 구체화하며 아직 새 테스트를 요청하지 않는다.

진단 해석 가드: frequency0은 clockUnavailable이고 delayed latest timing을 현재 cadence frame에 귀속하지 않는다. presentWait의 유한값 0은 필드 관측일 뿐 지원되는 대기 0의 증명이 아니다. CPU duration은 self가 아니며 epoch 정밀 join·GPU pass/owner·마지막 지연 tail은 미확정이다. 수집 API의 실제 할당/GPU 부하도 미측정이고 GLES에서는 측정 자체가 부하에 영향을 줄 수 있다. 해당 한계와 지원 실패를 사용자 검증 항목별 진단 자료에 함께 출력한다.

정리: 완료 중간 DLL/PDB 13개 11,902,016바이트만 경계·SHA·크기 확인 후 삭제했다(`.utmp/gpu-render-diagnostics-20261111/CleanupReceipt.json`). 최종 3소스는 동일하며 원본/Before/rsp/실패 로그/검증 영수증/병합 백업은 보존했다.

### 31.6 빌드 옵션 확인 후 실행으로 준비 순서 교정

사용자의 앱 실행 보고 후 회수한 새 runtime 환경은 frameTimingFeatureEnabled=false / Unavailable-FeatureDisabled였다. 실제 ProjectSettings와 최근 prebuild 기록도 0/false여서 GPU 지표 수집 준비가 되지 않은 빌드였다. 메인 세션이 빌드 준비 확인 전에 실행 안내를 앞세운 실수이며 사용자 조작 문제로 보지 않는다.

Play OFF 확인 후 진단 옵션 enableFrameTimingStats만 0→1로 변경했다. `.utmp/gpu-render-diagnostics-20261111/enable-frame-timing-20261111-092842/EnableReceipt.json`과 원문 backup/diff에 따르면 단 1바이트이며 역치환하면 이전 바이트와 동일하다. 다른 설정·게임 코드는 변경하지 않았다. 새 빌드/기기 runtime enabled·유효 GPU sample 반환은 아직 미확인이고 실제 GPU 원인·성능 효과도 미검증이다.

다음 순서는 실제 Unity 빌드 옵션 확인→File Build And Run 시도→사용자 실행 보고→새 build/session의 runtime feature enabled와 지원 probe 확인이다. 지원 실패를 0값으로 성공 처리하지 않는다. 계측 부담이 달라지므로 baseline/후측정 모두 동일 진단 옵션을 사용해야 하며 일반 제품 설정과의 향상률 비교는 하지 않는다. 진단 종료 뒤 원래 FrameTimingStats 0으로 복귀하고 설정/빌드 귀속을 확인해야 한다. 메인 실제 확인에서 열린 Unity의 Frame Timing Stats 체크박스는 처음 OFF였으므로 해당 항목만 ON으로 클릭하고 체크 표시를 확인했다. 최종 디스크 값 1과 hash는 앞선 옵션 교정 결과와 동일했다. Login/Android/Play OFF 상태에서 Project Settings를 닫고 실제 File > Build And Run을 1회 클릭했다. 클릭 이후 UI·로그·프로세스·APK·ADB 완료 감시는 하지 않았다. 빌드 성공·설치·새 실행·실기 GPU 지원은 미확인이다.

### 31.7 자연 교전 진단과 엄격한 밀집 판정 분리 계획

사용자 생산·교전 뒤 시작 gate는 Blue36/Red10·최근 피해 약65ms에서 거절됐고 측정은 0이었다. 메인은 started=false만 보고 앱 재시작 없이 재경기를 안내했지만 실제 재입장 후 completed=NotStarted-GameEnd/requiresRestart=true였다. owner는 시작 전 GameEnd에서도 Dispose한다. 이는 사용자 책임이 아닌 계측기 수명 안내 오류다. run-dac53f1d 회수는 completedChain=false·빈 token/session mismatch·Host/log 수용 false, frames/NCC/timing0행이며 새 GPU 데이터가 아니다. logcat20분 deadline/explicitStop=false 뒤 stop 요청과 Editor prefix3,028,221바이트를 포함한 원본은 보존한다.

사용자 앱/Play 종료와 보완·빌드 시도 승인 후 메인이 Login/Play OFF를 확인했다. 별도 render-diagnostic purpose의 자연 교전 시작에서 양팀12 강제 조건만 제거하는 보완을 진행한다. Host/peer/map/nonce/token/probe 및 최근 실제 damage guard는 유지한다. 기존 양팀12+30bin의 strict load 판정은 그대로 두고 자연 교전 진단과 밀집 성능 통계를 구분한다. 모든 prepare/probe/start는 END/completed/freshness/실제 requiresRestart를 확인하고 앱당1회 수명을 유지한다. 재경기 측정에는 반드시 앱을 재시작한다.

logcat은 입장 즉시가 아니라 부대 준비 후 fresh start→probe→capture로 연결해 준비 중20분 소진을 피한다. 준비 중 FrameTiming valid sample 지원 상태를 구분하는 추가 단계는 검토 중이며 확정 구현으로 쓰지 않는다. 품질/API/HP/RPC는 바꾸지 않고 FrameTimingStats1을 유지한다. 구현·정적 검증은 아래 최종 결과로 갱신한다. 실제 native·기기 지원/효과와 새 빌드 시도는 아직 대기다.

| 테스트 항목 | 확인할 진단 자료 | 판정 |
|---|---|---|
| 앱 세션 재사용 가능 여부 | 동적 readiness·END/completed·requiresRestart·owner 수명 | started=false만으로 수용하지 않음; 경기 종료 후 새 측정은 앱 재시작 |
| 자연 교전 시작 | render-diagnostic purpose·identity/probe ACK·최근 damage | 양팀12 미달을 곧바로 거부하지 않되 strict 밀집 수용과 분리 |
| 엄격한 부하 충족 | 양팀 alive와 qualifying bin 통계 | 기존12+30bin 유지, 미달이면 최대 밀집 효과 PASS 금지 |
| GPU 지원·자료 완결 | runtime feature/유효 timing 지원 단계(검토 중), CSV/END/hash | 새 지원 확인·구현 결과 전 실제 GPU 수집 성공으로 기록하지 않음 |

추가 재경기를 먼저 요구하지 않고 위 기준 보완과 검증·실제 준비 확인 후 필요한 최소 절차를 안내한다.

구체안: purpose 누락은 기존 StrictBattle로 취급해 하위 호환을 유지하고 RenderDiagnostic만 별도 시작 조건을 적용한다. purpose allowlist와 probe/start 동일 purpose·ACK/completed/collector 연결을 정확히 검증한다. accepted start ACK도 종결 owner 재사용 거부에 포함한다. 기술적90초 완결·strict 밀집 충족·GPU 진단 수용을 각각 판정한다. 준비1Hz의 유효 timestamp/양수 GPU 관측 status는 지원 관측일 뿐 전투 표본 저장과는 분리한다. owner/private collector와 필요한 최소 manifest 문구를 대상으로 6구성 csc·새 fixture를 검증할 예정이며 아직 결과 수신 전이다.

**자연 교전 모드 최종 구현·검증(2026-11-11):** `.utmp/render-purpose-20261111/FinalVerificationReceipt.json`/FinalSourceHashes와 QA 최종 대조를 확인했다. 실제 변경은 owner와 private collector 2파일이며 manifest는 바꾸지 않았다. 351개 현재 Project 소스 누락 없이 SDK csc6구성 오류0·변경 경고0, Host192/CollectorPurpose18/Collector9/kernel6,366case·59,872assert가 통과했다. 엔진/NGO/recorder fixture와 mock ADB 범위이며 실제 native·기기 실행은 아니다. 최종2hash QA 일치·미해결 중대 이슈0, 중간 DLL/PDB13개11,906,244바이트만 CleanupReceipt로 삭제하고 원본·소스·검증 자료는 보존했다.

최종 조건은 누락/null/empty purpose를 StrictBattle로 처리하고 기존12each+최근damage 및30 qualifying bin을 유지한다. 명시적 RenderDiagnostic은 각 팀 살아 있는 유닛1개 이상+최근 실제 damage와 기존 identity/probe guard를 요구한다. `--purpose RenderDiagnostic`을 prepare/probe/start에 일관되게 사용하고 회수에서도 정확 목적 binding을 확인한다. 기술적90초 완결/엄격 밀집 수용/GPU raw 지원·자료 완결/양쪽 로그 검증은 독립 축이다. Render 수용을 밀집 PASS나 GPU 병목·효과 입증으로 올리지 않는다.

모든 준비 경로는 END/completed(빈 token 포함)/accepted start ACK/freshness/requiresRestart를 검증한다. 앱당1회 owner 수명을 유지하며 재경기 수집은 반드시 앱을 재시작한다. 부대 준비 후 fresh bounded PID stream→새 목적 probe→start 순서다. preparationGpuObserved는 유효 raw timestamp와 양수 GPU 관측 여부일 뿐 수집 창 GPU 행을 추가하거나 지원 완결을 증명하지 않는다. delayed latest의 현재 cadence 귀속·CPU self 해석은 계속 금지한다. FrameTimingStats1을 유지했고 품질/API/HP/RPC는 바꾸지 않았다.

메인이 Unity Login/Android/Play OFF에서 실제 Compiling Scripts→Reloading Domain→준비 복귀를 관측했다. Assembly-CSharp.dll21:39:50은 owner19:17 수정 뒤 갱신됐으며 이는 Editor 반영 증거이지 Android 실행 성공이 아니다. 최종2소스 hash는 동일하고 FrameTimingStats 디스크1을 유지했다. 실제 File > Build And Run을1회 클릭했으며 이후 UI·로그·프로세스·APK·ADB 완료 감시는 전혀 하지 않았다. 빌드 성공·설치·새 기기 실행·GPU 지원·성능 효과는 미확인이다. 새 Testcase는 만들지 않는다.

다음 절차는 새 앱 실행 후 로비 보고→준비 GPU 관측 확인→멀티 입장→Editor anchor 확인→생산·자연 교전 준비 보고→그때 fresh logcat/probe/start(RenderDiagnostic)→자연 플레이 종료 후 결과 화면 유지→메인 회수·독립 축 검증이다. 90초는 자동 수집 상한이며 사용자가 반드시 유지할 조건이 아니다. 먼저 끝난 경기는 부분 자료로 해석하고 강제 항복·자동 재시험을 요구하지 않는다(후속 사용자 지적에 따른 운영 정정). 양팀12를 계속 유지하도록 요구하지 않으며 strict 밀집 충족은 별도 판정한다. 재경기에서 새 측정을 하려면 반드시 앱을 재시작한다.

### 31.8 실기 분석 결과와 자연 종료 중심 운영 정정

메인 Login/Play OFF 확인 후 기록했다. `.utmp/multiplayer-battle-20261105/run-7fe8227d/collected-20261111-234845/CollectionReceipt.json`과 같은 run의 `IndependentAnalysisReceipt.json`을 직접 대조했다. RenderDiagnostic/TimeLimit90초·aborted=false/capacity=false,2,699frame/900NCC/756damage/75sampled qualifying bin이며 core/hash/양쪽 anchor와 로그 회수 검증이 완료됐다. hostTechnicalComplete/denseLoadCriteriaMet/gpuDiagnosticEligible/logsVerified는 true다. strict hostTelemetryEligible=false는 목적 분리의 정상 결과이며 실패로 보지 않는다.

| 관측 대상 | 이번 표본 결과 | 해석 한계 |
|---|---|---|
| full Update 간격2,697개 | 평균33.350074/p9535.970193/p9938.504615/최대41.226346ms,>50·>100ms각0 | wait 포함 cadence이며 CPU self 아님 |
| NCC900 wall | 평균2.170229/p953.5304/최대7.5868ms | 전투 tick 경과시간이며 순수 CPU self 아님 |
| positive GPU2,700개 | 평균27.474437/p9535.657580/최대53.727888ms,>33.33ms251·>50ms1 | delayed timing이며 현재 cadence와 자동 연결 금지 |
| CPU main/render/present wait | 평균20.622915/6.141210/12.685081ms | 합산·단순 뺄셈으로 CPU 점유·C# self 산출 금지 |
| GC counter | delta14 | GC 시간·pause 원인 미측정 |

FrameTiming2,713행의 timestamp는 모두 고유하고0 ID는 없으나 인접 역순15건이다. mask31=2,700/mask15=13, frequency999999999이며 clock 폭90.444814초로 준비 head/지연 tail이 섞일 수 있다. 지원과 raw 자료 완결 관측은 성공했지만 개별 cadence frame 정렬·GPU pass/특정 VFX owner는 미확정이다.75sampled bin을 연속75초·최대 밀집90초로 표현하지 않는다.

로그 검수는 Android PID67행의 Exception/NullReferenceException/FATAL/[ERROR] 검색0, Editor anchor22,380행의 Exception/NullReferenceException/FATAL 검색0이다. 다만 Editor에는 UnitAttackShadowObserver END의 [ERROR]1건이 존재한다. 이는 종결 진단이다. 추가 소스 대조에서 UnitAttackShadowObserver.cs3298~3313은 _dropped를 LocalVerdict 분류기로 전달하고3381~3402의 분류기는 droppedCount>0만으로도 FAIL을 반환한다. 해당 END의 dropped202는 진단 완결성 FAIL의 충분조건이며 clientAccepted1056·clientRejected/targetMismatches/correlationFailures/terminalPreflightFailures/manifestFailures/gameplayWrites 각0이었다. 다만 다른 미출력 counter나 표현 상태까지 배제한 유일 원인 확정은 아니며 실제 전투 기능 오류 또는 전체 표현 정상 판정으로 확대하지 않는다. 성능 수집의 hash/자료 유효성과도 별도다. 검색 범위에서 크래시가 관측되지 않았다는 사실과 전체 오류0·피해 오류 없음은 다르다. Release 전체 Dev 로그·모든 경로를 포괄하지 않는다.

이번은 자연 플레이에서90초 상한까지 기록된 회수·기기 지원 관측 성공이며 제품 GPU 교정 효과 PASS가 아니다. 사용자가90초 조건 유지가 어렵다고 다시 지적했고, 앞 보완이12gate/종결 재사용만 바꾸고90초 상한은 남겼음을 인정했다. 앞으로 자연 종료 기록도 부분 자료로 해석하며90초 유지 강요·강제 항복·자동 재시험을 하지 않는다. 앱당1회 수명과 재경기 측정 시 앱 재시작 조건은 유지한다.

과거 baseline과는 main의 여러 변경·FrameTimingStats ON·무작위 맵/교전량이 달라 향상률을 계산하지 않는다. 열·전력 연속 trace도 없어 기기 한계를 단정하지 않는다. 다음은 렌더 비용 owner 분리 검토 제안이며 아직 특정 원인·교정 대상은 확정하지 않았다. 추가 코드·설정·빌드·사용자 재테스트 요청·원본 삭제는 없다. 원본과 hash/anchor/실패 자료는 보존한다.

### 31.9 RenderBloomExploration 구현 계획

2026-11-12 사용자는 다음 렌더 원인분리 준비를 승인했고 메인은 구체 구현을 승인했다. 읽기 감사에서 Game의 단일 활성 GlobalVolume이 SampleSceneProfile을 참조하며 Bloom active/intensity0.25/threshold1/scatter0.5/highQualityFiltering1임을 확인했다. Vignette0.2·Tonemapping mode1·MotionBlur inactive, 카메라 HDR/postFX와 빈 stack, Mobile scale0.8은 유지한다. 후보는 Bloom 하나이며 아직 병목 원인으로 확정한 것은 아니다. 전체 postFX/VFX 비활성화나 영구 품질 낮춤은 하지 않는다.

변경 범위는 기존 owner와 private collector2파일의 opt-in RenderBloomExploration이다. Volume 런타임 profile을1회 clone하고 공유 asset은 바꾸지 않는다. A는 원래 값/override 상태, B는 Bloom intensity.overrideState=true/value0을 적용하는 안으로, active=false만 사용할 때의 pipeline default fallback을 피한다. 실제 resolved stack 관측 가능성은 검토 중이다. ABBA 각8초/총32초 상한 후 즉시 Bloom을 복구하고 측정창도 Stop(reason=BloomExplorationComplete,aborted=false)한다. 자연 GameEnd 뒤 기존 방식으로 저장한다. 조작32초/core90초를 따로 유지하면 불필요한 잔여 측정이 생기므로 새 목적만32초 창으로 정한다. 기존 StrictBattle/RenderDiagnostic의90초 상한은 바꾸지 않는다. 자연 조기 종료는 부분 자료로 보존하고32초·90초 유지나 양팀12를 사용자 조건으로 요구하지 않는다.

정상 END/Stop/fault/scene 변경/Disable/Destroy 및 예외 경로에서 원래 profile·Bloom 값/override를 복원한다. 외부에서 profile을 교체했다면 타 소유 상태를 덮어쓰지 않는 소유권 guard를 둔다. 기존 RenderDiagnostic/StrictBattle은 바꾸지 않고 HP/RPC/맵·게임 규칙·품질 asset은 유지한다. 권위 경계는 GameSystemRules_UnitCombatSynchronization의 권위 결과/HP·사망 상태 계약, 원시 보관은 LogRules1.10, 테스트 항목별 진단 대응과 검증 축 분리는 WORKFLOW를 따른다.

| 테스트 항목 | 남길 진단 자료 | 수용·한계 |
|---|---|---|
| 목적/원본·복구 | ready/정확 purpose·profile 소유권·A/B 값과 override·복구 receipt | 외부 profile 소유권 침범 금지, 정상/종결/예외 경로 복구 확인 |
| ABBA 경계 | phase/readPhase/age/cohort·전이 시각·부분 종료 이유 |2초 전이 제외는 heuristic이며 raw GPU source phase 보장이 아님 |
| GPU/부하 관측 | raw timestamp/지원·CPU frequency·교전량·카메라/열 상태·END/hash | 지연 GPU clock이 cadence에 미매핑이므로 read phase만으로 정확 A/B GPU 귀속 금지 |
| 탐색 결과 | phase별 보수적 통계·제외/미완 cycle·지원0/표본 부족 | 탐색만; 원인·절감률·FPS 향상 확정 금지 |

현재 감사/후보 선정과 진단 수단 구현·정적 검증을 완료했다. 실제 Unity native·기기 동작과 성능 효과는 미검증이다(2026-11-12 최종 검증 결과 반영). 새 빌드는 이번 요청에서 시도하지 않았고 기기 A/B 지원·복구·효과는 미검증이다. 원본·기존 실패 자료는 보존한다. 자연 전투의 부하/카메라/열 변화와 무작위 맵 차이, 지연 head/tail·전이·미완 cycle 때문에 비교가 성립하지 않으면 표본 부족으로 보고하고 재시험을 자동 요구하지 않는다. 최종 구현/검증 결과를 받은 뒤 실제 변경과 미완 범위를 갱신한다.

계획 보완: readiness의 bloomSupportReady/status를1Hz로 관측하고 prepare에서 scope 지원을 검사해 생산 전 부적합을 거절하며 start에서 재검증한다. camera VolumeStack의 resolved 값은 읽기 지연·갱신 순서가 미확정이므로 별도 관측으로 남긴다.2초 전이 guard로 원시 GPU source phase 정확성을 보장하지 않는다. 변경은 owner/private collector2파일이며 아래 정적 검증을 완료했고 빌드 시도0이다.


**최종 구현·검증 결과:** `.utmp/bloom-exploration-20261112/FinalVerificationReceipt.json`과 FinalSourceHashes/CleanupReceipt를 직접 읽었다. owner·private collector2파일의 최종 hash를 QA가 live와 독립 대조해 일치했으며 중대 미해결0이다. 영수증 qa 필드의 pending은 생성 당시 상태이고 이후 최종 QA 메시지로 마감됐다. SDK csc6구성 오류0·변경 owner 경고0, actual-source Host235/Collector13/kernel6,366case·59,872assert가 통과했다. engine/recorder/NGO fixture·mock ADB 범위이며 실제 native 검증은 아니다.

Bloom profile과 각 component를 deep clone하고 B는 intensity override=true/value0, A는 원래 값/override로 전환한다.32초 창 정상 종료·원복 후 자연 GameEnd 저장을 구현했다.25초에 네 phase를 진입했더라도 정상32초/실제 phase 시간 조건을 만족하지 않으면 완료로 수용하지 않는다. shared/default 비누출·이전 runtime 참조 복원·외부 profile 교체·종료/예외 정리는 fixture에서 확인했으나 Unity native deep clone/메모리·시각 원복은 NOT RUN이다.

read cohort의 실제 양수 GPU 개수/평균/p95, A/B resolved 일치, read frame의 부하·카메라 변화를 출력한다. raw GPU source phase는 Unknown이며2초 전이 제외와 resolved-stack-at-read는 정밀 source phase 보장이 아니다. 이 통계는 탐색 자료이고 Bloom 원인·절감률·FPS 향상을 확정하지 않는다. 기존 RenderDiagnostic/StrictBattle은 유지한다. 준비 scope 지원 거절·목적 binding·앱당1회·자연 조기 종료 부분 보존 조건도 유지한다.

이번 asset/설정 쓰기는 없었으나 개별 asset before snapshot이 없어 현재 hash만으로 전체 asset 바이트 전후 동일을 독립 입증한 것은 아니다. FrameTimingStats1은 유지했고 품질/API/targetFPS/shader/HP/RPC/맵 변경은 없다. 완료는 진단 수단 준비이며 native Unity·Android·빌드·효과 검증은0/미실행이다. 중간 DLL/PDB13개11,930,492바이트만 CleanupReceipt로 삭제했고 원본/Before/source/rsp/tests/logs/결과/영수증은 보존했다.

## 32. 예상 절감 우선의 후보 선정과 첫 조사

사용자가 승인한 실제 교정 선정 기준은 예상 비용 절감이 큰 순서이며, 비슷하면 구현 작업량이 작은 순서다. 전체 후보·기존 효과·미검증 범위는 Research35 장부를 원본으로 삼는다. 관측시간/예상 절감/신뢰도/조사 작업량/구현 작업량을 구분하며 현재 절감ms·구현량은 미산정이다. 따라서 구현 순위는 미정이다. GPU 관측27ms와 tick7ms를 곧바로 절감 순위로 비교하지 않는다.

첫 행동은 기존 자료에 소스 연결이 있는 전투 tick의 짧은 읽기 조사다. 이것은 조사 착수 순서이며 최대 절감 P1 구현 선정이 아니다. 첫 AttackRange 읽기 조사를 완료했으며 구체 소스·자료 연결과 한계는 Research35에 보존했다. 조사 결과로 제거 가능한 반복 작업과 계약을 확인하고 근거 신뢰도·예상 절감 범위·구현량을 비교할 수 있을 때만 작은 교정을 선정한다. 실측이 없으면 미확정으로 남긴다.

Bloom 탐색 수단은 구현·정적 검증 완료 상태로 보류한다. 현재 새 빌드·native·기기 A/B는 없으며 영구 품질·VFX·권위/HP/RPC 변경도 없다. 사용자는 자연 플레이하며90초·팀별12기 유지나 강제 항복을 요구하지 않는다. 코드 구현/정적 검증/기기 적용/실제 효과를 분리하고 원본·실패 자료를 보존한다. 새 Task/Testcase는 만들지 않는다.


**다음 행동·착수 조건:** Research35의 AttackRange 조사 결과를 기준으로 boolean/nearest 호출자와 provider의 읽기 계약·fallback·경계값을 확인하고, 동일 입력 출력 동등성과 검사량 감소를 근거화한다. false 전체 탐색과 nearest 의미를 유지한다. 사용자가 첫 항목 진행을 승인했으며 읽기 계약·동등성·작업 감소 가능성을 검토 중이다. 읽기 계약 확인 후 메인이 UnitCombatUseCase1파일의 국소 교정안을 승인했고 구현·CLR/컴파일 검증을 완료했다. 실제 빈도·true 비율·frame 절감은 미측정이므로 구현 순위를 확정하지 않는다. 제품1파일 교정 외 새 소스·설정·빌드 변경은 없다.


**현재 실행안:** Research35의 국소안대로 boolean2경로만 stopAtFirstMatch:true로 바꾸고 private 기본false·nearest 전체 탐색·기존 유효 거리 조건·Hex fallback·DetectRange·권위 계약을 유지한다. source 전후 출력 동등/검사량·경계값/호출자 계약·컴파일·독립 QA의 아래 범위로 구현 완료를 판정한다. 예상 이득은 true일 때 후속 검사 생략이며 false는 전체 동일, 기기ms/FPS 효과는 미확정이다. 현재 새 Testcase/테스트 UI/빌드/실기 요청은 없다.


**AttackRange 최종 결과:** `.utmp/attack-range-existence-20261112/SmallHarnessResults.txt`와 CompileResults.json을 직접 읽었다. 실제 추출 baseline/new 메서드와 Unity Vector3/Mathf를 참조한 인공 입력 CLR30case/210assert가 통과했다. best 표본 provider 조회9→2·후보 검사9→2, false와 nearest 작업은 동일했다. 이는 실제 게임79% 개선·frame/FPS 이득을 뜻하지 않는다. SDK6구성 exit0/error0, 경고24/27/27/31/31/0이며 변경 소스 경고0이다. 메인5hunk diff 검토와 QA 정적 중대0을 확인했다.

완료 범위는 UnitCombatUseCase1파일의 boolean2호출/private 기본false/기존 거리 조건 내 조기 반환2곳과 위 CLR·컴파일·소스 검토다. 실제 Unity native/Android 실행·호출 빈도/true 비율·FPS/절감ms는 미검증이고 빌드는 시도하지 않았다. 새 계측·asset·설정 변경 없이 기존 dirty와 원본을 보존한다. 최종 영수증/실제 source hash와 QA 대조가 완료됐고 중간 DLL/PDB13개11,853,820바이트만 CleanupReceipt로 삭제했다. baseline/new source/tests/results/logs/rsp/영수증은 보존했다.


**실기 준비 중 빌드 귀속 불일치 확인(2026-11-12):** 메인은 run-34d51d4c의 RenderDiagnostic prepare/Client anchor·accepted probe까지 수행했지만 capture는 시작하지 않았다. 기기 앱의 ready는11:36KST 생성이고 package lastUpdateTime은11:35:59였다. 최신 prebuild11:07:41에는 교정 전 UnitCombatUseCase(89EE76… Before)가 기록되어 있으나 현재 교정 소스는14:05:04 수정·262E14… After다. 따라서 설치 앱은 교정 전이며 이번 교정 검증에 사용할 수 없음이 확정됐다.

메인은 source 귀속 확인 전에 생산·교전 보고를 요청했다가 즉시 대조 후 철회하고 사용자 대기를 요청했다. logcat-stop을 실행하고 원본을 보존했으며 앱/경기 조작·capture·Bloom 시작·새 빌드 시도는 없었다. 사용자 책임이 아닌 준비 안내 순서 오류다. 다음은 File Build And Run으로 새 교정 포함 빌드를 준비한 뒤 실제 설치 앱의 새 build/session과 prebuild source 귀속을 먼저 확인해야 한다. 현재 기기 효과 미검증 상태를 유지하고 기존 자료로 교정 적용·FPS 개선을 주장하지 않는다.


### 32.1 교정 포함 실기 표본 수용과 현재 완료 경계

Research35.3에 새 run-3bd7fabf의 최종163749 회수·교정 source/설치 APK 귀속·CSV/로그를 보존했다. 최초 stop receipt 전 false는 최종 true로 보완된 수집 단계 차이이며 테스트 실패가 아니다. full2,700 Update간격 평균33.320457/최대40.926692ms·>50ms0, NCC900wall평균1.849843/최대6.198ms, positiveGPU2,699개 평균27.456306/최대45.983311ms를 관측했다. 이번 DetectRange paired 결과를 AttackRange 직접 효과로 쓰지 않는다.

구현·CLR/컴파일/QA와 교정 포함 빌드의 실기 자료 회수는 완료다. 직접 AttackRange 시간 절감·FPS 개선율은 여전히 미증명이고 기존 무작위 맵/부하와 비교한 향상률도 계산하지 않는다. Editor의 이번 Shadow END dropped585 진단 FAIL은 성능 자료 유효성·제품 공격 오류와 분리한다. 상세 검색 범위·과거 Movement END는 Research35.3을 따른다. 사용자 결과 화면 추가 유지나 자동 재시험·새 교정/계측 확대를 요구하지 않는다. 원본을 보존하고 이번 항목을 해당 범위로 마감한다.


관리 메모리도 Research35.3에 보완했다. full2,700행 gcUsedBytes는 십진22.089728~28.553216MB의 관리 힙 관측이고 전체 앱/GPU 메모리가 아니다. GC counter delta15는 정지시간이 아니며 미지원 GC Allocated In Frame의0값으로 절감을 입증하지 않는다. 누수·pool 용량·반복 경기 잔존·이번 교정 GC 효과는 미검증이다.

사용자가 결과 기록과 다음 작업 진행을 승인했다. 다음 후보는 실제 비용·빈도·제거 가능 부분의 읽기 비교 결과를 받은 뒤 선정한다. 작업량이 작다는 이유만으로 구현하지 않으며 절감ms/FPS를 지어내지 않는다. 현재 새 제품 변경·설정·빌드·실기·삭제 없이 비교 조사 결과 대기다.


### 32.2 다음 조사: 렌더 pass와 VFX draw 구조

Research35.4의 렌더/전투 tick/GC 비교로는 제거 가능한 비용을 산정하지 못했으므로 구현 순위는 여전히 미정이다. 다음은 렌더 pass/VFX draw owner 구조와 기기 지원 수단을 확인한다. 관측 GPU 시간이 크다는 이유만으로 해당 시간을 절감 가능량으로 보거나 작은 교정을 바로 구현하지 않는다.

Unity6.3 공식 지원 조건상 현 Release 앱의 FrameTiming 지원을 Android Vulkan GPU Usage 모듈 지원과 혼동하지 않는다. 원격 Frame Debugger는 Development Build가 필요하고 draw 구조 확인용이며 GPU ms 증명 수단이 아니다. 지원 불가를0시간으로 처리하지 않는다. 현재 API/품질 변경·도구 구현·빌드·실기·사용자 재시험 요청 없이 구조 근거 확보 단계로 진행한다.

다음 최소 행동은 기존 Editor Frame Debugger로 대표 전투 화면1프레임의 shadow/opaque/transparent/postFX draw·material 구조를 보조 확인하는 것이다. 현재 실행하지 않았고 기기 GPU ms의 증거로 쓰지 않는다. 정밀 기기 pass 시간은 도구 지원 확인이 선행된다. GPU 교정은 owner 특정 전 비용·품질 위험이 미정, CPU 공간 탐색 구조 변경은 검증 부담, GC 수명/pool 변경은 잔존 위험을 별도로 비교한다. 작은 GC 교정을 우선으로 승격하지 않는다.

메인 최종 화면은 Unity Login/Android·Play OFF여서 현재 대표 전투 프레임이 없고 Frame Debugger 구조 검수는 미실행이다. 조사 준비와 실제 프레임 검수를 구분한다. 다음에는 기존 빌드를 재사용할 수 있는 Editor 대표 전투 프레임 확보가 필요하지만 기기 GPU ms 증명이 되지는 않는다. 이번 앱/Editor 재실행·씬 변경·임의 생산 조작·새 빌드·삭제는 없다.

### 32.3 최신 main 재병합·검증 결과

메인은 사용자 승인으로 최적화 브랜치에서099e8a1b→eaa883d4를 fast-forward하고 autostash d5b66064를 재적용했다. `.utmp/main-merge-20261112-current/BackupReceipt.json`에는163항목/총6,044,744,733바이트, 새 복사22개3,922,230바이트와 기존 검증 백업 재사용104개·기존 삭제 상태37개의 absence가 기록됐다. 원본과 복구 근거는 보존한다.

PROJECT_STATUS/WORK_HISTORY는 자동 병합됐고 현재 HEAD 원문의 비어 있지 않은 모든 행이 보존됐음을 직접 대조했다. 새 main의 지속 스킬 영역 상태·규칙 참조·이력과 기존 Android 최적화 기록을 함께 확인했다. 이 두 문서는 이번에 추가 수정하지 않았다. 폰트 충돌 해결과 아래 소스 보존·SDK/실제 Unity 검증을 완료했다(2026-11-12 최종 결과 반영). 실기 멀티·시각·성능 검증은 별도 미완이다. 새 성능 효과·기기 수용은 주장하지 않으며 최종 결과 수신 뒤 필요한 상태만 갱신한다.

폰트 수치 정정: 메인이 먼저 전달한 main300은 원문 검산 전 인계 수치 오류다. font-audit/main.asset/local.asset를 직접 세면 glyphTable/characterTable은 main315/local316이다. QA는 main 직렬화315레코드 exact 보존·missing0·「서」1개 추가·atlas2,034바이트 차이가 추가 UsedRect 안에만 있음을 검수했다. live는 local과 바이트 동일이다. 숫자 보고 오류와 보존 안전성 검증을 구분하며 SupersetProof 집계 축 정정과 native 검증은 아래 최종 결과로 마감했다.

후속 집계 축 확인: main unique ID300/local301과 직렬화 레코드315/316은 기존 중복 ID 레코드15개 때문에 다르다.315를 고유 글자 수로 쓰지 않으며 보존은315직렬화 레코드 multiset 누락0 기준이다. 기존 중복은 이번에 수정하지 않는다. Unity Runtime17:27:12/Editor17:27:16 갱신과 busy 종료는 메인 관측이며 전체 회귀 실행 결과는 아래 최종 PASS로 확인됐다.


**11/12 병합 검증 최종 마감:** HEAD와 origin/main은 eaa883d4로 같고 최적화 브랜치·unmerged0을 메인이 확인했다. 폰트는 main 직렬화315레코드와 local 추가1개를 exact 보존하고 atlas 차이2,034바이트는 추가 영역뿐이며 비캐시 설정/참조는 동일하다. PostApply 분류는109바이트 exact·11개행만 변경·6overlap·37기존 삭제 absence로163항목이며 총백업6,044,744,733바이트를 보존한다. overlap 코드3개는 upstream SkillZone 추가 외 기존 최적화 삭제/교체0으로 QA 확인했고 자동 병합 문서2개 보존도 별도 확인했다. 폰트 해결의 git add는 메인이 수행했으며 autostash d5b66064는 보존했다.

현재357개 runtime 소스 누락 없이 새 SkillZone을 포함한 SDK 대표4구성(Editor/DevIL2CPP/Release/Tools) 오류0, Unity Runtime17:27:12/Editor17:27:16 갱신 세대를 독립 검수했다. 실제 Unity 메뉴 Full Regression을17:29:26~17:30:21 실행해8개 terminal PASS를 확인했다. `.utmp/main-merge-20261112-current/native/NativeValidationReceipt.json`과 suffix 원문을 보존했다. 이는 source/asset/Editor fixture 검증이며 Android·실기 멀티·시각·성능 보증은 아니다. Login/Play OFF 유지, 새 APK 빌드·commit·push는 없다.

다음 실기 확인은 새 병합 빌드의 source 귀속을 먼저 확인한 뒤 공격·이동 정상 동작과 새 지속 스킬의 진입/이탈·만료/경기 종료 동작을 최소 확인하도록 권고한다. 새 테스트를 실행했거나 완료한 것으로 기록하지 않으며 자동 재시험·임의 빌드는 하지 않는다. 추가 리팩토링·코드/설정 수정 없이 이번 병합 검증을 위 범위로 마감한다.

### 32.4 제품 선별 커밋 준비 상태

제품 선별 커밋 object `14247c6fa4aafaa9a47bfb0c561765b8020524d0`을 준비했다(부모 eaa883d4). 제품23파일은 타일 mesh·존재 조회·표현/버퍼·문자열·랭킹과 승인 로그 정책 및 필수4문서이며 측정 define/FrameTiming 설정·helper/tools·raw·Task/실수 기록은 제품 main 대상에서 제외한다. 제품 분리본 대표4구성 컴파일과 합성 tree 문서 검사는 통과했지만 native 검증은 이전 혼합본 범위이고 실기 전체·FPS 효과는 미증명이다.

제품23파일을 로컬 main14247c6에 반영했고 현재 codex/android-performance-optimization은 제품 부모와 측정 전용 분리를 담은49db109a로 fast-forward했다. 원격 push는 실행하지 않았다. 측정 설정/도구와 기존 Task·문서 이력은 최적화 브랜치에 보존했다. Task/메타는 기존 GUID를 그대로 복사했으며 새 Task/Testcase를 만든 것이 아니다. autostash3c66f2c4 재적용의 문서5충돌은 먼저 private 백업 후 검증 feature 원문으로 해결했다. 자동 병합 WORK_HISTORY는 feature와 전체 텍스트 동일·고유행 누락0을 확인했다. 원본 autostash와 충돌 백업을 보존하며 이번 Git add는 메인이 담당한다. 새 빌드·실기는 없고 분리본4구성 컴파일/문서 검사 통과와 이전 혼합본 native 검증의 범위를 구분한다.
