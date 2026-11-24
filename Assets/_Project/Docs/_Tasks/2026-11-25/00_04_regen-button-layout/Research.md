# 초월 연구소 자연회복 버튼 정렬 — 조사

초월 연구소의 자연회복 업그레이드 버튼을 다른 연구 버튼과 같은1칸 크기와 정렬로 바꾼다. 나머지2칸은 비어 있어도 된다. 사운드는 정상이라는 사용자 정정에 따라 이번 작업에서 제외한다.

**문서 역할**
- 목적·질문: 현재 전체 폭의 원인과 표시 변경 범위를 확인한다.
- 포함: 사용자 승인, source/규칙 근거, 조사·구현·화면 검증의 현재 상태.
- 제외·담당: 구체 수정·확인 순서는 [Plan](Plan.md). 연구 효과/비용/사운드 변경은 범위 밖이다.
- 원본·갱신: 이번 UI 표시 작업의 조사 원본이며 설계·검증 수신 때 갱신한다.

## 1. 승인과 현재 상태

2026-11-25 사용자는 첫 번째 UI 수정만 진행하고 새 Task를 작성하도록 승인했다. 메인이 Unity 실제 화면을 조작해 확인한 뒤 File→Build And Run을 시도하는 것까지 승인했으며 이후 빌드 완료 감시는 하지 않는다. 소스 수정을 완료했고 runtime390 source SDK 오류0을 확인했다. 메인의 실제 Unity 화면·연구 시작 확인과 빌드시도1회를 마쳤으며 상세 수용/미검증 범위는 아래 결과 절을 따른다. 기존 다른 작업의 dirty는 보존한다.

## 2. 원인과 규칙 경계

[ResearchMatrixView.BuildMatrix](../../../../Scripts/Presentation/UI/ResearchMatrixView.cs)의 Row_Regen은 runtime 동적 생성이다. 수정 전173~177행은 CreateCell의 weight에 `_cellWeight * Columns.Length`를 넘겨3칸 폭으로 만들었다. 따라서 Unity Inspector 값만 바꾸는 영속 수정으로 끝낼 수 없다. 확정 수정은 아래4절과 같다.

[규칙 인덱스](../../../GameSystemRules.md)에서 찾은 [Upgrade 규칙13](../../../GameSystemRules/GameSystemRules_Upgrade.md)의 변경 전 원문은 “초월 = 2×3(동물·식물) + 자연회복 전체폭 버튼1개”였다. 이번 사용자 승인은 이 표시 기준을1칸 버튼/나머지2칸 빈 공간 허용으로 바꾸는 요청이다. 확정 설계에 따라 규칙13의 해당 표시 구절만1칸+빈2칸으로 정합했다. 연구 레벨·비용·효과·상태/권위는 변경하지 않는다.

## 3. 확인할 범위

자연회복 버튼의 폭·높이·열 정렬이 다른 버튼과 같은지, 빈2칸이 허용된 배치인지 확인한다. 표시 변경과 실제 연구 동작 수용은 별개이며 화면을 보기 전 PASS로 기록하지 않는다. 로그는 보조이며 최종 화면 크기·정렬의 직접 증거를 대체하지 않는다. 사운드 파일 변경·추가 사운드 조사는 중단·제외한다.

## 4. 확정 수정 범위

ResearchMatrixView.BuildMatrix의 Regen CreateCell weight를 `_cellWeight`로 일반 셀과 같게 하고 뒤에 활성 빈 layout slot2개를 둔다. 각 slot은 RectTransform+LayoutElement만 갖고 min/preferred width0·flexibleWidth=_cellWeight다. 일반행과 같은 label+3열의4children/spacing3으로 첫 열 시작점·폭·높이를 맞춘다. Button/Image/Graphic/ResearchCellView는 없어 입력·Refresh 셀 목록에 참여하지 않으며 기존 rows clear 수명으로 정리한다.

기능 변경 파일은 ResearchMatrixView1개이며 ResearchPanelUI18의 전체 폭 주석1줄도 정합한다. RegenCanonicalGroup/Stat.Regen 바인딩과 연구 비용·효과는 보존한다. 새 UI logger/framework 없이 실제 Rect와 화면으로 레이아웃을 판정하고 기존 연구 로그는 보조로만 사용한다. 실제 Unity에서1칸 크기·첫 열 정렬·빈2칸과 연구 시작 동작을 확인했다. 연구 완료·스탯·모바일 수용은 미검증이다.

## 실제 확인·빌드시도 결과 — 2026-11-25

소스 수정 완료 후 runtime390 source SDK 오류0을 확인했다. 메인이 실제 Unity 화면에서 초월 싱글 경기를 재시작하고 연구소를 건설·열어, 동물/식물의3열과 자연회복1셀의 크기·첫 열 정렬이 같고 오른쪽2칸이 빈 공간인 것을 확인했다. 자연회복 버튼 클릭 뒤 Lv0→1 연구 진행창·진행바·취소 표시를 확인했다. 이는 표시와 연구 시작의 제한 수용이며 연구 완료·스탯 적용을 검증한 결과는 아니다.

플레이 종료를 확인한 뒤 메인이 File→Build And Run을1회 클릭해 Checking prerequisites/Starting Android build 모달을 관측했다. 사용자 요청대로 이후 빌드 완료 감시는 하지 않았다. 빌드 성공·기기 설치·앱 실행·모바일 표시/기능은 미확인이다. 사운드 작업은 제외했고 새 Testcase·추가 제품 수정·Git 작업은 하지 않았다. 기존 다른 dirty 작업은 보존했다.

## 5. PC·Laptop·S22 레이아웃 비교 조사

사용자는 자연회복 버튼1셀+빈2칸 정렬이 정상임을 확인했다. 이 표시 수용은 유지한다. 후속 요청은 PC-Editor의 인간 연구소 화면을 기준으로 Laptop-Editor/S22에서 달라 보이는 레이아웃의 원인을 확인하고, 원인이 확정되면 교정하는 범위다. 메인/전문 담당이3이미지와 source를 직접 대조해 원인을 확인했고 ARF 교정을 적용했다. 실제 Editor1080×2340/1080×1920 화면 재검증을 완료했고 S22 실기는 미검증이다.

비교 원본은 [PC-Editor.png](../../../_Reference/img/PC-Editor.png), [Laptop-Editor.png](../../../_Reference/img/Laptop-Editor.png), [S22.jpg](../../../_Reference/img/S22.jpg)로 보존했다. 서로 다른 기기/Editor 화면이며 화면 전체 조건이 같다고 가정하지 않는다. 원래 panel anchorY0~0.5(화면 높이의 절반)와 width 기준 CanvasScaler·행 높이130 고정의 조합이 aspect에 따라 다른 레이아웃을 만든 것이 확인됐다. 특정 기기 결함으로 분류하지 않는다.

최신 origin/main pull은 기존 staged3개(google-services-desktop.json.meta, Maplestory Light SDF.asset, lib_burst_generated.txt)를 보존하기 위해 merge가 거절됐다. 그 비교 조사 당시에는 stash 승인 대기로 pull이 거절됐고 이후 사용자 전체 병합 승인에 따라 백업·stash 후 합성 검증을 진행했다. 당시 거절을 작업 손실로 기록하지 않는다. 이번 기록은 비교 조사이고 제품·다른 Task·Git 쓰기는 하지 않았다.

## 후속 교정 적용·Editor 화면 재검증 완료

메인이 Unity Inspector에서 ResearchPanel에 AspectRatioFitter를 추가하고 WidthControlsHeight/ratio1.125(1080:960)로 설정·저장했다. 저장 때 발생한 불필요한 전체 재직렬화는 전문 담당이 원문 backup 후 HEAD 기준 ARF 추가2block·15insertions만 남겼다. 조사 전 씬 diff0을 기준으로 다른 object/field가 동일함을 역검증했다.

근거는 private `.utmp/regen-button-layout-20261125/SceneMinimalDiffReceipt.json`이며 Game씬 최종 SHACD9AAB5EA441933A1133CC06EFB3AFA6DB79BF7DFC8A537EB94ACDC448B9731C다. 메인이 Login 이후 실제 패널을 두 해상도에서 재검증해 Editor 표시를 제한 수용했다. S22 등 다른 기기 수용은 아직 미확인이다. 사운드·연구 기능·추가 규칙은 변경하지 않았다.

## 후속 최종 화면 확인 결과

메인이 실제 Unity에서 Login 자동 로그인→초월 선택→싱글 쉬움→연구소 건설→패널 열기 순서로 확인했다.1080×2340에서 panel 비율이 PC 기준과 같이 단축됐고 Gameview를 Mobile1080×1920으로 전환한 같은 패널에서도 전체 비율·버튼/철거의 상대 배치가 일치했다. 자연회복1셀+빈2칸도 유지됐다.1920 화면에서 자연회복 클릭 후 Lv0→1 진행창·진행바·취소 표시를 확인했다. 연구 완료·스탯 적용을 검증한 결과는 아니다.

Editor 두 해상도의 표시/연구 시작 범위는 확인했지만 실제 S22는 미검증이다. 이번 후속 turn에는 빌드 요청이 없어 새 빌드시도는 없었다(앞선 초기 버튼 수정의 빌드시도와 구분). 추가 소스코드 수정0이며 Game씬 ARF15줄과 참고 이미지3개/자동 meta가 이번 후속 범위다. 그 화면 검증 시점에는 pull 실패·stash 승인 대기였고 새 이미지를 push하지 않았다. 이후 병합 현행 상태는 마지막 절을 따른다. 사운드·다른 기존 dirty·Git 쓰기·원시 로그는 건드리지 않았다.

## 사용자 표시 수용·기존 최적화 전체 병합 승인

사용자가 이번 UI 수정이 잘됐음을 확인했다. Editor 두 해상도 검증과 사용자 전체 표시 수용을 기록하되 연구 완료/스탯·S22 별도 관측값 등 기존 미검증 범위를 임의로 채우지 않는다.

사용자는 최신 main을 pull·병합·push하고 UI뿐 아니라 기존 최적화 작업도 모두 main에 병합하라고 명시했다. 현재 메인이 Git 백업과 전체 기능 코드 검토를 진행 중이며 원격 main9c012141/현재 HEAD80c03561 기준이다. 전체 병합 승인·검토 단계로 아직 병합/push 완료는 아니다. 이전 선별 제품 반영과 이번 전체 범위를 구분하고 양쪽 기존 작업·staged/dirty 보존을 우선한다.

기존 최적화 문서의 구현 완료와 성능 미검증, 과거 main 미포함 기록을 구분해 조사한다. 다른 Task/상시 문서 수정이 필요하면 범위부터 보고하고 승인받는다. 현재 변경은 이 Task2개뿐이며 문서 담당은 제품·Git를 변경하지 않는다.

## 전체 최적화·UI 병합 진행 — 2026-11-25

전체 제품+UI 작업을 eaf05db4로 보존했고 local 산출물 원문은 stash27409dce/b068b956에 보존했다. 현재 origin/main9c012141과 no-commit merge 중이며 merge commit/push는 아직 미완이다. main 연막/회피와 opt DoT 출처 wire를 함께 유지해 합성 schema13으로 정합했다. TDD의 기존12/11/10은 시점별 원문으로 보존했다. 실제 코드/검증기·SDK 합성 검증은 아래 결과와 같이 완료했으며 병합 후 새 native/실기 수용은 아직 없다.

과거 opt측정판/main선별판 구분은 당시 기록이며 이번 승인은 UI만이 아닌 기존 최적화 전체 통합이다. 기존 기능·실측 한계와 양쪽 작업을 보존하고 최종 병합 영수증 뒤 현행 상태를 갱신한다.

## 전체 합성 소스 검증 완료 — 2026-11-25

eaf05db4의 기존 최적화 전체·UI와 origin/main9c012141을 합친 source 검증을 완료했다. 합성 schema13은 main 신규 연막 wire/토큰과 opt DoT 출처를 함께 보존한다. 신규 main 연막4cs 및 관련 Projectiles/Tower/MistShrinePanel은 origin/main 대비 diff0을 확인했고 Game씬 ARF15줄은 eaf 기준 그대로다.

private `.utmp/all-optimization-integration-20261125/final-merge/CompileResults.json`에서 Editor/AndroidDev/Release/EditorTools 오류0·경고25/28/32/0이다. 실제 runtime394·Editor35(합429) source membership 누락0·외부 참조524을 확인했다. 이것은 합성 source/SDK 검증이며 새 native·멀티 실기·표시 전범위·성능 검증은 아니다. main용 설정은 enableFrameTimingStats0/Android define DOTWEEN으로 유지하며 과거 opt 측정 결과를 이 설정의 FPS 효과로 소급하지 않는다.

최종 Git merge commit·main 이동·push/원격 확인은 메인 진행 단계로 아직 완료라고 기록하지 않는다. 과거 진단 opt 전용/제품 선별 기록은 당시 범위이며, 이번 승인은 기존 최적화·관련 작업 전체의 통합이다. 백업과 원래 작업·staging 보존을 계속 유지한다.

최종 source freeze 근거는 `.utmp/all-optimization-integration-20261125/final-merge/FinalValidationReceipt.json`이다. 최종 검증9partial 문자열 정합 후 EditorTools 재컴파일 오류0·runtime hash 불변을 확인했다. 독립 정적 검수 blocker0으로 main 연막 manifest·DoT bool·기존 최적화 보존을 확인했고 main ProjectSettings diff0이다. 이 검수는 새 native/실기 PASS가 아니며 Git 최종 결과 전 문서 쓰기를 동결한다.
