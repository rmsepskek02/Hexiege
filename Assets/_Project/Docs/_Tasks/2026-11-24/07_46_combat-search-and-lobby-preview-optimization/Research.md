# 전투 검색과 숨겨진 로비 미리보기 최적화 — 조사

기존 전투 결과와 로비 화면을 유지하면서 두 종류의 반복 비용을 줄인다. 전투에서는 필요 없는 전체 후보 검사를 줄일 수 있는지 확인하고, 로비에서는 화면에 쓰이지 않는 미리보기 카메라의 렌더링을 막는다.

**기존 로직 보존:** 기존 로직을 대체·제거할 경우 검증 전까지 주석 비활성화로 보존한다. 검증되지 않은 새 경로를 완료로 표시하지 않는다.

**문서 역할**
- 목적·질문: 두 작업의 현행 소스·계약·제거 가능 작업과 위험을 확인한다.
- 포함: 승인 범위, 조사 근거, 설계/구현/검증의 현재 상태.
- 제외·담당: 파일별 실행·검증·종료 기준은 [Plan](Plan.md). 후보 비교·기존 완료 장부는 이전 Task.
- 원본·갱신: 이번 병행 작업의 조사 원본이며 담당자의 구체 설계와 실제 검증 결과를 받을 때 갱신한다.

## 1. 승인과 현재 상태

2026-11-24 사용자가 추천한 두 작업의 동시 진행과 새 Task 작성을 승인했다. 전투7파일·로비2파일 구현과 source/SDK 제한 검증·최종 영수증 대조를 마쳤다. 이번 ZIP 실기 분석에서 직접 작업 감소와 제한된 동등성·숨김 렌더/복귀 관측을 확인했다. 현재 수용 범위와 남은 검증은6절이며 전체 기능·정확 source-build 귀속·CPU/FPS/열 개선은 미확정이다. 기존 dirty 작업 보존과 로컬 main/opt 통합을 완료했고 사용자 승인 후 main 원격 push도 완료했다(7절).

선정 근거는 [이전 Research5절](../00_09_android-optimization-resume/Research.md#5-물리발열을-포함한-확장-비용-조사--2026-11-24)과 [이전 Plan](../00_09_android-optimization-resume/Plan.md)을 참조한다. 이전 Task의 완료25개 작업과 후보 장부를 덮어쓰거나 다시 구현하지 않는다.

## 2. 두 작업의 범위와 보존 조건

| 작업 | 확인할 문제·제거 대상 | 유지할 계약·현재 미확정 |
|---|---|---|
| 전투 공간 후보 제한 | 최근접/Detect 전체 유닛·건물 순회에서 보수적인 공간 후보로 검사 범위를 줄일 가능성 | 후보 누락 금지, 기존 동률 순회 순서·override·Root/논리 tile 차이·반경·spawn/death/재진입 수명 보존. 예상 기존7파일의 units-only live world index. 최종 API·source fixture/SDK 수신·대조 완료 |
| 숨겨진 로비 preview camera | Battle탭 밖에서 화면에 쓰이지 않는 preview render 반복 | hidden rendering만 끄고 Battle 복귀·캐러셀 표시/선택·RT/기존 인스턴스 수명 보존. 생성/해제 정책이나 보이는 화면 품질을 임의 변경하지 않음 |

전투의 현행 계획은 Q(U+B)→Q(K+B)+인덱스 갱신/정렬/재구축 비용이다. K는 유닛 후보 수이며 건물 B는 기존 전체 scan을 유지한다. 유닛 검사 제거 상한 Q(U−K)와 추가 유지 비용을 함께 비교해야 하며 K≈U이면 검사 감소0이다. 실제 분포·순 CPU 이득·ms/FPS는 미측정이다. 로비의 추가 render1/frame 회피는 조건부 구조 목표이지 실측 GPU 시간 감소가 아니다. 512² RT·30fps의 pixel 환산 같은 예시는 실제 GPU 실행/전체 frame 개선율과 구분한다. 전투 성능과 로비 절감 효과를 합산하지 않는다.

## 3. 규칙과 근거 경계

[규칙 인덱스](../../../GameSystemRules.md)를 거쳐 [Units 전투 진입 규칙9·상태 규칙10·타겟 선택13](../../../GameSystemRules/GameSystemRules_Units.md)과 [전투 동기화 계약](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md)을 읽고 기존 거리·타겟·권위/피해 계약을 유지한다. 공간 후보는 정답을 바꾸는 규칙 수정이 아니다. 로비는 [공통 UI 규칙](../../../GameSystemRules/GameSystemRules_UI.md)의 기존 화면 동작을 보존하는 숨김 상태 최적화이며 새 화면 정책을 추가하지 않는다.

전투의 최종 API·검증 연결은 아래5절이며, 로비의 확정 설계는 아래4절을 따른다. 현재 새 실기·Git 실행은 없으며 빌드시도는 아래 기록과 같다.

## 4. 로비 preview 확정 설계·구현 지시

변경 대상은 [LobbyRootView.cs](../../../../Scripts/Presentation/UI/Views/Lobby/LobbyRootView.cs)와 [Lobby.unity](../../../../Scenes/Lobby.unity)다. 씬에는 `_characterPreviewCamera`를 fileID63448771로 연결하는1줄을 추가한다. Awake에서 원래 enabled 상태·같은 scene·target RT를 검증하고, 유효할 때 `originalEnabled AND active AND CurrentTab==Battle`로 camera.enabled를 제어한다. 패널 show 직전에 enable하고 OnDisable/OnDestroy는 off, OnEnable은 최신 tab으로 복구한다.

초기 NetworkManager가 없으면 기존 패널 visibility와 원래 camera를 유지하는 fail-open이다. 참조 오류는 경고1회와 기존 enabled 보존으로 처리한다. RT/캐릭터/Animator/tween/CanvasGroup 동작은 바꾸지 않으며 수동 Camera.Render 호출을 추가하지 않는다. Battle 내부 subpanel에서도 RaceSelection을 상시 유지하므로 최상위5탭만 gate한다. 규칙 근거는 공통 UI 규칙5와 Settings의 로비 규칙1이며 화면 동작 정책 변경은 없다.

Editor/Development 전용 `[LOBBY-PREVIEW] schema=lobby-preview-v1`은 상태 전환과 SRP endCameraRendering의 rendered/hiddenRendered, 복귀 첫 render의 resume-rendered/frameDelta를 관측한다. 매 frame 로그는 없고 Release 진단은 제외한다. 이 로그는 render 유무 증거이며 pixel 품질·캐러셀 자연스러움은 사용자 육안 수용과 별개다. 구현·컴파일과 이번 실기 숨김 callback0/복귀는 확인했으며 전체 시각·성능 PASS는 아니다.

### 4.1 로비 구현·제한 검증 완료

근거는 private `.utmp/lobby-preview-gate-20261124/FinalVerificationReceipt.json`, ProductCompileResults.json, ReleaseBoundary.json이다. LobbyRootView.cs117줄 추가(SHA584afc18…3ddad4), Lobby.unity 참조1줄 추가(SHA8ec93263…ffaf8a)를 반영했다. 기존 dirty RT와 캐러셀 소스는 보존했다. current358 source membership 누락0, Editor 오류0/기존 경고24, Release 오류0/기존 경고31, 변경 파일 경고0이다. 실제 Release DLL reflection에서 gate는 있고 진단 callback/fields는 없음을 확인했다.

실제 source 메서드8조건 stub fixture 통과는 native Unity/SRP·첫 렌더 frame·시각·GPU/FPS/열·빌드 성공 검증이 아니다. 정확 진단은 `[LOBBY-PREVIEW] schema=lobby-preview-v1 stage=gate`의 reason/owner/camera/battle/enabledBefore/enabled/rendered/hiddenRendered/frame, `stage=resume-rendered frameDelta`, `wiring-invalid action=fail-open`이다. rendered는 interval 계수이며 gate+resume은 instance당64줄 상한이다. 전투 완료 후 복합 최종 SDK는 새로 확인해야 한다.

## 5. 전투 units-only live world index 채택 방향

유닛만 실제 world 위치 인덱스로 후보를 제한하고 건물은 기존 전체 순회를 유지한다. 예상 변경은 기존7파일이다. 새 framework·규칙 변경은 없다.

| 파일 | 구현 책임 |
|---|---|
| [IEntityPositionProvider.cs](../../../../Scripts/Application/Interfaces/IEntityPositionProvider.cs) | 선택적 capability로 지원 여부 구분 |
| [UnitWorldPositionProvider.cs](../../../../Scripts/Infrastructure/UnitWorldPositionProvider.cs) | index owner와 후보 제공 |
| [UnitCombatUseCase.cs](../../../../Scripts/Application/UseCases/UnitCombatUseCase.cs) | 후보 query와 기존 fullscan fallback |
| [UnitSpawnUseCase.cs](../../../../Scripts/Application/UseCases/UnitSpawnUseCase.cs) | roster revision |
| [UnitFactory.cs](../../../../Scripts/Infrastructure/Factories/UnitFactory.cs) | binding lifecycle |
| [UnitView.MovementAuthority.cs](../../../../Scripts/Presentation/Unit/UnitView.MovementAuthority.cs) | 실제 Root3setter에서 dirty 통지 후 실제 pose 갱신 반영 |
| [UnitView.cs](../../../../Scripts/Presentation/Unit/UnitView.cs) | Initialize/Destroy 수명 |

매 frame 전체 Transform을 훑지 않는다. roster revision이 바뀔 때 실제 Dictionary 열거 ordinal을 전량 다시 작성해 삭제 slot 재사용 후의 순서도 보존한다. 후보를 이 ordinal로 정렬하며 unit-before-building과 동일 거리 우선순위를 유지한다. provider zero/미등록/nonfinite는 항상 검사하는 overflow로 보내고, 미지원 provider·재진입·index fault·과도한 범위는 기존 fullscan을 사용한다.

이동 계산·방향·행동·NGO/RPC 값은 바꾸지 않는다. Root/논리 tile 차이를 숨기지 않으며 새 동기화/이동 writer를 추가하는 안이 아니다. 최종 API를 구현하고 source fixture/SDK 제한 검증을 완료했다. 정확 진단 키·source fixture/SDK 결과는5.3절에 수신 완료로 기록했다. native/실기 PASS는 아니다.

### 5.1 최종 API·구현 착수

기존 IEntityPositionProvider.cs 안에 별도 optional `ICombatUnitCandidateSource`를 둔다. `TryBeginUnitCandidates(units, rosterRevision, center, radius, out candidates)`/`EndCandidates`와 poseChanging/Changed 경계를 사용한다. provider가 world hash bucket·순서·scratch를 소유한다. Factory는 owner1 binding listener를 replace/clear하며 Spawn revision은 add/remove/replace 직후 event 전 증가한다. Authority3setter는 dirty→실제 pose 반영을 연결하고 Initialize/Destroy는 UnitData 참조 identity로 수명을 검증한다.

후보 lease는 finally로 반납한다. 기존 unit loop 본문과 건물 전체 scan은 유지하고 ordinal 정렬·overflow 항상 후보 원칙을 지킨다. numeric 미지원/overflow·미지원 provider·재진입·index 오류 등은 fullscan으로 돌아간다. 현재 제품7파일 연결은 완료했고 C# 제한 검증 완료·최종 source/hash 영수증 대조 완료이다. 아래5.2절은 중간 이력이며 현행 최종 결과는5.3절이다.

진단은 GameEnd1회 summary의 queries/candidate/global/maintenance/fallback과 제한된 동일 입력 표본 비교를 예정한다. query마다 출력하지 않으며 baseline 비교 자체 비용은 제품 효과와 분리한다. 요약이 있다는 사실만으로 결과 동등성·실기 성능 PASS를 선언하지 않는다.


### 5.2 전투 중간 확인 이력 — 2026-11-24, 최종 결과는5.3절

메인이7개 Before 해시와 private backup을 대조했다. MovementAuthority의 실제 position3setter는 before/after 통지만 추가하고 계산은 유지했으며 Spawn은3mutation revision, Factory는 single owner binding/register2/clear flush, UnitView는 Initialize/Destroy identity와 GameEnd summary를 연결했다. 건물 fullscan·미지원 Client fallback·overflow·ordinal은 유지하고 dense에서 전체 정렬을 피하는 경로를 포함한다.

current358 SDK Editor/AndroidDev/Release 오류0·기존 경고24/27/31을 중간 확인했다. Release 변경 파일 경고2는 이전 원본에도 있던 attackCrossFadeApplied/spatialRecoverablePending이며 새 경고가 아니다. actual provider source+actual UseCase assembly/current358 compile/mock Factory Transform의 private fixture27검사와 sparse203→3을 확인했다. 이는 합성 후보 약98.52% 감소 예시일 뿐 실기 FPS/시간/GC가 아니며 기존 bool early-exit 때문에 추가 절감0 상황도 있다. 이 중간 확인 시점에는 최종 결과·해시·영수증이 검증 중이었다. 현행 결과는5.3절이다.

진단은 `[COMBAT-SPATIAL] WORK`/`COMPARE`2줄로 분리하며 표본은8categories 각2개·최대16이다. sampleMismatches/Faults/loopInspections의 실제 값을 사용할 예정이고 baseline 비교 비용은 별도다. 최종 필드·source 영수증 수신 뒤 정합하며 현재 수치로 최종 수용을 선언하지 않는다.

### 5.3 최종 C# 제한 검증 결과

SpatialFixture36검사는 actual UseCase assembly/current provider source/Factory Transform managed stub 범위에서 통과했다. expected enemy2·unit-before-building·삭제 slot 재사용·dirty·same-id old Destroy·huge/NaN/zero·Client·nested·dense·owner 교체·getter throw를 확인했다. 의도적으로 notify 없이 Root를 바꾼 negative control은 정답 불일치를 검출했고 정상 notify 복구를 확인했다. 따라서 모든 실제 pose 변경의 안전 hook이 유지되는 것이 최적화 정답의 필수 조건이다. native Unity/NGO 전체 실행 검증은 아니다.

warm nearest는 같은 통제 입력에서 실제 loop3/203, getter(공격자 포함)3/203, 후보3/203이었다. 약98.52%는 해당 입력의 검사/조회 감소만 뜻하며 모바일 CPU·FPS·GC·열 개선율이 아니다. boolean은 기존 early-exit로 추가 절감0인 조건이 있다. runtime 비교 표본은8categories×2/max16이며 fixture mismatch0을 모든 실제 입력의 무오류로 확대하지 않는다. WORK/COMPARE는 같은 DevGuid scope로 묶고 최악 long-max 포맷 최대563UTF8 byte를 확인했다.

복합 current358 source Editor/AndroidDev/Release 오류0·기존 경고24/27/31이다. Release의2개 변경 파일 경고는 이전 원본에도 있던 것으로 새 경고가 아니다. 구현·C# 제한 검증은 완료했고 이 source 검증 이후의 빌드시도·실기 관측 결과는 아래 기록을 따른다. 최종 source/hash 영수증과 메인의 독립 검수를 대조 완료했다.

이번 제품 범위는 전투7파일(IEntityPositionProvider, UnitWorldPositionProvider, UnitCombatUseCase, UnitSpawnUseCase, UnitFactory, UnitView.MovementAuthority, UnitView)과 로비2파일(LobbyRootView, Lobby.unity), 총9파일이다. 문서는 이 Task Research.md/Plan.md2파일이며 private 검증은 .utmp에 보존한다. 새 Testcase·QA 문서·메모리·상시 문서·Git 변경은 없다.

최종 근거는 private `.utmp/combat-world-candidates-20261124/FinalVerificationReceipt.json`(SHA87860B8858152DB7DE4E34E722EFA4AF349C3DCE8147EC42DBFBB00BF867FFA3), FinalSourceHashes.json, FinalRuntimeSourceIdentity다. 메인이 RunFixture.ps1을 독립 재실행해36검사·warm loop/getter3/203·summary563B·fixture mismatch0을 확인했다. 전후7파일의 baseline/current 해시, current358 source identity, fixture source와 testedProduct.Editor DLL 해시, 로비2파일의 기존 영수증 해시도 일치 확인했다. source·C# 제한 검증과 이번 실기 관측 범위 수용은 마감하며 전체 native/NGO 수명·ms/FPS·열은 미검증이다. 인덱스의 추가 Root 읽기·relocation·roster/order 재구축·정렬 및 List high-water 보관 비용은 남고 메모리 byte는 미측정이다.

## 빌드시도 기록 — 2026-11-24

사용자의 이번 빌드시도 승인으로 메인이 Unity6000.3.5f2 Hexiege/Login/Android 창에서 source compile/Reload Domain 창이 닫힌 뒤 File→Build And Run을1회 클릭했다. 즉시 Detect Java Development Kit(JDK)/Checking JDK 단계를 관측하고 이후 감시를 중단했다. 빌드 완료·설치·앱 실행·실기 결과는 확인하지 않았다. 설정·키스토어·비밀번호·씬 저장의 수동 변경은0이다. 기존 source fixture/SDK 검증을 실기 완료로 바꾸지 않는다. 현행 로그 작업명은 `combat-search-and-lobby`다. 처음 안내한44자 이름은40자 제한 위반이어서 아래처럼 정정했다.

## 실제 테스트 ZIP과 짧은 작업명 — 2026-11-24

사용자가 테스트 저장·분석을 요청했다. 실제 파일은 `2026-11-24_18_52_combat-search-and-lobby_01.zip`이며 이 Task의 로그 별칭은 `combat-search-and-lobby`(직접 검산23자)로 계속 안내한다. 처음 메인이 안내한44자 이름은 수집기의1~40자 제한 위반이었다. 현재 규칙은 소문자/숫자로 시작하고 이후 소문자·숫자·하이픈만 허용한다. 도구 기능 변경은 없다.

해당 ZIP의 무결성과 실기 분석을 완료했다. 이번 관측 범위에서 두 최적화의 직접 작업 감소·제한된 동등성 근거를 확인했으며 상세 결과와 잔여 검증은 아래 실기 결과 절을 따른다. 정확 source/build 해시 귀속과 전체 기능·FPS 수용은 미확정이다. 문서 반영 승인이나 재저장을 다시 요구하지 않는다. 원시 로그는 보존하고 본문을 복제하지 않는다.

## 6. 실기 로그 분석 결과 — 2026-11-24

이번 관측 범위에서는 두 최적화의 유효한 직접 작업 감소와 제한된 동등성 근거를 확인했다. 모든 기능 수용·모든 유닛/상황·CPU/FPS/net GC/메모리/발열 개선은 증명하지 않았다. 추가 테스트를 일괄 요구하지 않으며 현재 근거와 미관측 범위를 구분한다.

### 6.1 원본 무결성과 귀속

`2026-11-24_18_52_combat-search-and-lobby_01.zip`은755,492B/SHA669e52da173be5b48b544553a1ea051d5fcb27a9c76c159cb788b43e0e338d37이며4entry의 bytes/SHA가 일치했다. private `.utmp/combat-search-log-review-20261124/archive-verification.json`과 record-validation.json에서 Android5,851records 연속·UTF8 byte/end 검증·tail0·remoteStable true를 확인했다. footer 부재는 앱 실행 유지 snapshot 지시와 부합하며 손상 FAIL이 아니다. 정상 종료 footer가 없어 원본 삭제 보호는 유지한다. 원본 삭제·재명명·업로드는 하지 않았다.

Android buildGuid3190119e…75709와 실제 policy 발현은 관측했지만 source exact build-hash manifest가 없어 원래 검증 source 해시까지 소급 확정하지 않는다. 앞의 합성 fixture203→3과 아래 실기 수치는 서로 다른 자료다.

### 6.2 전투 직접 작업량과 동등성 표본

메인이 Android raw90487 WORK와90505 COMPARE의 같은 scope를 직접 대조했다. queries54,647 중 indexed16,350(29.9193%), fallback38,297(70.0807%), indexFaulted false였다. indexedGlobal409,943→candidateList125,207은 목록 범위69.4575% 감소이며 전체 실제 loop 감소율이 아니다. COMPARE14개의 실제 동일 입력 표본은 loop285→183(102/35.7895% 감소), mismatch0/fault0이다. category2,2,2,2,0,2,2,2로8종 중7종 관측이며 Detect nearest not-found는 미관측이다.

유지 비용은 binding read43·poseUpdates18,284·roster51회/visits1,637을 관측했다. 건물 fullscan은 그대로다. fallback은 안전 경로이며70%라는 사실을 실패로 판정하지 않는다. 원인별 비율 분류 로그가 없어 각 fallback 원인 비율은 미산정이다. 순 CPU·FPS/net GC·메모리·열 이득은 미측정이다.

Host는19,680move frames·457centers/max error 출력0·fatal/adapter/invalid/repeat/drop0·END, Client는 replicated343/invalid0·END를 확인했다. Client index queries0은 서버 권위 검색의 정상 범위다. Host attack END는 commits253/impact281/gateInvalid0/targetMismatch0/dropped0, Client accepted610/rejected0이다. 전체25type·정확 타이밍·모든 전투 회귀 PASS로 확대하지 않는다.

### 6.3 로비 숨김 렌더와 복귀

Android raw434의18:47:07.927 off/frame41170부터 raw450의18:47:29.779 on/frame41825까지21.852초·655frame에서 rendered0/hiddenRendered0, 즉 해당 camera SRP callback0을 확인했다. raw466의18:47:29.783 resume frameDelta0은 같은 Unity frame에서 재개됐다는 뜻이지0ms·pixel 품질 보장이 아니다. 앞 visible 구간5,077frame/callback5,077은 일치했다.

로비 복귀 후 새 Android owner(raw94781/94888)와 Editor 새 owner(lines7269/7276)도 렌더 재개를 관측했다. hidden interval은1개이며 실제4탭 각각의 종류는 미기록이다. 캐러셀·pixel 품질의 사용자 육안 수용, 전체 scene 수명·manager 실패 경계는 미관측이다.

### 6.4 별도 오류와 남은 경계

Host 오류0, Editor는 경기 BEGIN18:49:39.735 전18:48:20.979의 NRE1개가 있다. stack은 UnityEditor.DockArea.WindowMaximizeStateOnDoubleClick/Internal_CallDelayFunctions여서 전투 제품 또는 이번 최적화 원인이라는 근거는 없다. 전체 오류0으로 쓰지 않으며 요청 없이 수정하지 않았다. 이번 단계는 직접 작업 감소·표본 동등성·숨김/복귀 관측의 제한 수용으로 마감한다.

## 7. main 제품 반영·opt 통합 및 작업 보존 결과 — 2026-11-24

2026-11-24 병합 전 계획은 제품9파일 delta 선별·양쪽 작업 보존·의미 충돌 검토였다. 해당 실행을 아래와 같이 완료했으며 main 원격 push는 사용자 승인 후 완료했으며 병합 후 native/실기 검증은 별개다.

로컬 main `17ef4ceeb5b22fb0c8a2537a3d4987489b07a9ae`는 최신 main c697382a 기반 제품9파일+문서/meta13파일, 총22파일의 선별 반영을 완료했다. opt HEAD `80c035617e30891f79cb52b9566bf021a37342d9`는 부모 a64b31d와17ef4cee를 합쳐 최신 main을 포함한다. 23:34 재fetch 당시 origin/main은 c697382a이고 미푸시였으나, 이후 사용자의 “푸시도 해” 승인으로 main1commit push를 완료했다. server의 c697382a..17ef4cee main→main 정상 응답(exit0)과 local main/origin main의17ef4ceeb5b22fb0c8a2537a3d4987489b07a9ae 일치를 메인이 확인했다. 이 선별 반영 당시 branch는 codex/android-performance-optimization이고 opt branch push0·미커밋 작업을 보존했다. 당시17ef 선별 제품판에는 opt 측정/실험·Dev 진단을 넣지 않았고 그 실기 로그는 opt 측정판 근거다. 이후 전체 통합 범위는 마지막2026-11-25절을 따른다.

기존 tracked WIP36은 stash066a5b1ab19968bc775407c064471d3c1fb7743e patch3way로 복원하고 원래 untracked13은 사본과 byte exact로 복원했다. docs3/core5/other6 충돌을 해소했고 원래 staging3개(폰트/desktop meta/Burst txt)를 복구했다. 메인은 unmerged0과 기존 staged3 일치를 확인했다. 첫 stash2916의 ignore cleanup 실패 때 원래 index 사본 복구·49원문 exact 검증 후 두 번째066a 보관에 성공했으며 손실은 없었다. 백업·stash2개를 보존하고 Profiler46파일 약6GB와 raw 로그는 삭제·이동하지 않았다.

최종 실제 복원 작업본은 runtime390+Editor34=424 source이며 최신 main386runtime/26Editor와 opt 기존4runtime/8Editor 추가의 누락0을 확인했다. Editor/AndroidDev/Release/Tools4구성 오류0·경고25/28/32/0이다. 컴파일에서 발견한 UnitView 중복 field/OnDestroy2개는 동일 세트1개 제거로 정합했다. main schema11/합성 opt12를 구분한다. 이번 병합 턴의 native/UI·실기·빌드시도는 미실행이므로 이전 opt 실기 관측을 병합 후 새 검증으로 바꾸지 않는다.

근거는 private `.utmp/selective-merge-20261124/final-working/FinalValidationReceipt.json`(SHAAFB66C2F439C9916741E3739BC83342BE66EBB5D988DE2AB2825F02127FEE4CF), SourceInventory.json, SourcePreservationProof.json이다. main17ef에는 선별 문서판이 이미 포함돼 있고 이 작업본의 후속 마감 기록은 opt 미커밋 상태로 남는다. 문서 담당은 제품/Git를 변경하지 않았다. 정확 source·기능 전체·native 성능 수용 한계는 이전 결과 범위를 유지한다.

## 전체 합성 소스 검증 완료 — 2026-11-25

eaf05db4의 기존 최적화 전체·UI와 origin/main9c012141을 합친 source 검증을 완료했다. 합성 schema13은 main 신규 연막 wire/토큰과 opt DoT 출처를 함께 보존한다. 신규 main 연막4cs 및 관련 Projectiles/Tower/MistShrinePanel은 origin/main 대비 diff0을 확인했고 Game씬 ARF15줄은 eaf 기준 그대로다.

private `.utmp/all-optimization-integration-20261125/final-merge/CompileResults.json`에서 Editor/AndroidDev/Release/EditorTools 오류0·경고25/28/32/0이다. 실제 runtime394·Editor35(합429) source membership 누락0·외부 참조524을 확인했다. 이것은 합성 source/SDK 검증이며 새 native·멀티 실기·표시 전범위·성능 검증은 아니다. main용 설정은 enableFrameTimingStats0/Android define DOTWEEN으로 유지하며 과거 opt 측정 결과를 이 설정의 FPS 효과로 소급하지 않는다.

최종 Git merge commit·main 이동·push/원격 확인은 메인 진행 단계로 아직 완료라고 기록하지 않는다. 과거 진단 opt 전용/제품 선별 기록은 당시 범위이며, 이번 승인은 기존 최적화·관련 작업 전체의 통합이다. 백업과 원래 작업·staging 보존을 계속 유지한다.

최종 source freeze 근거는 `.utmp/all-optimization-integration-20261125/final-merge/FinalValidationReceipt.json`이다. 최종 검증9partial 문자열 정합 후 EditorTools 재컴파일 오류0·runtime hash 불변을 확인했다. 독립 정적 검수 blocker0으로 main 연막 manifest·DoT bool·기존 최적화 보존을 확인했고 main ProjectSettings diff0이다. 이 검수는 새 native/실기 PASS가 아니며 Git 최종 결과 전 문서 쓰기를 동결한다.
