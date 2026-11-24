# 초월 연구소 자연회복 버튼 정렬 — 계획

자연회복 버튼이 한 행 전체를 차지하지 않도록 다른 연구 버튼과 같은1칸 크기로 정렬한다. 빈2칸을 채우기 위한 새 기능이나 사운드 변경은 추가하지 않는다.

**검증 전 보존:** 기존 로직을 대체할 때는 검증 전 주석 비활성화로 보존한다. 사용자 승인 표시 변경 외 연구 기능을 바꾸지 않는다.

**문서 역할**
- 목적·질문: 최소 표시 수정과 실제 화면 확인·빌드시도를 진행한다.
- 포함: 승인 범위, 수정/규칙 정합 계획, 확인과 완료 경계.
- 제외·담당: 원인·현행 규칙 근거는 [Research](Research.md). 사운드·연구 수치·다른 최적화는 제외한다.
- 원본·갱신: 이번 실행 계획이며 담당 설계·실제 결과 수신 뒤 현재 상태를 갱신한다.

## 1. 현재 단계와 최소 수정

사용자의 UI1건·새 Task·실제 Unity 확인 후 빌드시도 승인을 받았다. 현재 ResearchMatrixView의 runtime Row_Regen 전체 폭 생성 경로를 확인했고 최소안 구현·SDK 오류0과 실제 Unity 표시/연구 시작 확인을 완료했다. 빌드시도 후 완료·설치는 미확인이다. Inspector-only 영속 수정은 현재 동적 생성 구조와 맞지 않는다.

1. ResearchMatrixView에서 Regen cell weight를 _cellWeight로 바꾸고 입력/표현 컴포넌트 없는 빈 layout slot2개를 추가한다. 일반행과 label+3열/4children/spacing3을 맞춘다. 나머지2칸은 빈 공간을 허용한다.
2. Upgrade 규칙13의 전체 폭 문구는 이번 사용자 표시 변경에 필요한 부분만 정합한다. 해당 구절을 다른 셀과 같은1칸+빈2칸으로 정합하고 다른 규칙은 바꾸지 않는다.
3. 변경 source의 필요한 검증·컴파일 후 메인이 Unity에서 실제 초월 연구소 패널을 열어 크기·정렬과 기존 표시/버튼 동작을 확인한다.
4. 화면 확인 뒤 메인이 File→Build And Run을1회 시도하고 이후 완료 감시는 중단한다. 성공·설치·앱 실행은 확인 전 미확인으로 남긴다.

## 2. 확인 항목과 근거

| 확인 항목 | 직접 근거 | 한계 |
|---|---|---|
|1칸 크기/열 정렬·빈2칸 | 기존 다른 연구 버튼과 실제 Unity 화면 비교 | source weight만으로 실제 pixel 배치 PASS 아님 |
| 기존 연구 기능/다른 종족 표시 | 최소 diff·필요 자동 비교 및 화면 조작 | 미실행 연구 회귀를 전체 PASS로 확대하지 않음 |
| 빌드시도 | File 메뉴 실제 클릭 관측 | 빌드 완료·설치·실기 성공과 별개 |

RegenCanonicalGroup/Stat.Regen·비용 로직은 보존한다. ResearchPanelUI의 전체 폭 주석1줄만 추가 정합하며 새 UI logger는 만들지 않는다. 실제 Rect·화면은 직접 근거, 기존 연구 진단 로그는 기능 보조다. 구현 검증 결과 수신 뒤 이 표에 연결한다. 새 Testcase·상시 문서·메모리·수동 meta는 생성하지 않는다. 기존 dirty·원시 로그와 다른 Task를 보존하며 Git 쓰기는 하지 않는다. 현재 구현·SDK·Editor 표시/연구 시작의 제한 수용과 빌드시도는 완료했다. 연구 완료·스탯·Android 표시/동작·빌드 성공/설치는 미확인이다.

## 실제 확인·빌드시도 결과 — 2026-11-25

소스 수정 완료 후 runtime390 source SDK 오류0을 확인했다. 메인이 실제 Unity 화면에서 초월 싱글 경기를 재시작하고 연구소를 건설·열어, 동물/식물의3열과 자연회복1셀의 크기·첫 열 정렬이 같고 오른쪽2칸이 빈 공간인 것을 확인했다. 자연회복 버튼 클릭 뒤 Lv0→1 연구 진행창·진행바·취소 표시를 확인했다. 이는 표시와 연구 시작의 제한 수용이며 연구 완료·스탯 적용을 검증한 결과는 아니다.

플레이 종료를 확인한 뒤 메인이 File→Build And Run을1회 클릭해 Checking prerequisites/Starting Android build 모달을 관측했다. 사용자 요청대로 이후 빌드 완료 감시는 하지 않았다. 빌드 성공·기기 설치·앱 실행·모바일 표시/기능은 미확인이다. 사운드 작업은 제외했고 새 Testcase·추가 제품 수정·Git 작업은 하지 않았다. 기존 다른 dirty 작업은 보존했다.

## 3. 후속 레이아웃 비교 계획

자연회복1셀+빈2칸은 사용자 정상 수용이다. 후속은 Research5절의 PC-Editor(인간) 기준과 Laptop/S22 원본 비교로 제한한다. 원인 확정 후 교정은 사용자가 허용했으나 원인 확인 후 Game씬 ARF 교정을 적용했고 Editor 두 해상도 화면 재검증을 완료했고 S22 실기는 미검증이다.

1. 실제 panel Rect·anchor/CanvasScaler·화면 aspect와 고정 행 높이를 대조해 화면 차이가 발생하는 조건을 확인한다. anchorY0~0.5·width 기준 scaling·행130 조합의 aspect 의존을 확인했고 아래 ARF 교정을 적용했다.
2. 표시/내용을 유지하는 최소 교정안을 원인과 함께 정리한다. 사운드·연구 수치/기능 변경으로 확대하지 않는다.
3. 확정안과 실제 검증 결과 수신 뒤 이 Task의 상태를 갱신한다. 다른 기기의 미관/버튼 동작을 source만으로 PASS 처리하지 않는다.

비교 조사 당시 staged3파일 보존으로 pull이 거절되어 stash 승인을 기다렸으나 이후 전체 병합 승인을 받아 백업·stash·합성 검증을 진행했다. 기존 dirty·staging을 임의로 해제·보관하거나 Git 작업을 하지 않는다. 검사 도구 실행 가능 여부는 실제 사용 가능한 런타임으로 확인하고 실행 불가라면 미실행으로 명시한다.

## 후속 교정 적용·Editor 화면 재검증 완료

메인이 Unity Inspector에서 ResearchPanel에 AspectRatioFitter를 추가하고 WidthControlsHeight/ratio1.125(1080:960)로 설정·저장했다. 저장 때 발생한 불필요한 전체 재직렬화는 전문 담당이 원문 backup 후 HEAD 기준 ARF 추가2block·15insertions만 남겼다. 조사 전 씬 diff0을 기준으로 다른 object/field가 동일함을 역검증했다.

근거는 private `.utmp/regen-button-layout-20261125/SceneMinimalDiffReceipt.json`이며 Game씬 최종 SHACD9AAB5EA441933A1133CC06EFB3AFA6DB79BF7DFC8A537EB94ACDC448B9731C다. 메인이 Login 이후 실제 패널을 두 해상도에서 재검증해 Editor 표시를 제한 수용했다. S22 등 다른 기기 수용은 아직 미확인이다. 사운드·연구 기능·추가 규칙은 변경하지 않았다.

## 후속 최종 화면 확인 결과

메인이 실제 Unity에서 Login 자동 로그인→초월 선택→싱글 쉬움→연구소 건설→패널 열기 순서로 확인했다.1080×2340에서 panel 비율이 PC 기준과 같이 단축됐고 Gameview를 Mobile1080×1920으로 전환한 같은 패널에서도 전체 비율·버튼/철거의 상대 배치가 일치했다. 자연회복1셀+빈2칸도 유지됐다.1920 화면에서 자연회복 클릭 후 Lv0→1 진행창·진행바·취소 표시를 확인했다. 연구 완료·스탯 적용을 검증한 결과는 아니다.

Editor 두 해상도의 표시/연구 시작 범위는 확인했지만 실제 S22는 미검증이다. 이번 후속 turn에는 빌드 요청이 없어 새 빌드시도는 없었다(앞선 초기 버튼 수정의 빌드시도와 구분). 추가 소스코드 수정0이며 Game씬 ARF15줄과 참고 이미지3개/자동 meta가 이번 후속 범위다. 그 화면 검증 시점에는 pull 실패·stash 승인 대기였고 새 이미지를 push하지 않았다. 이후 병합 현행 상태는 마지막 절을 따른다. 사운드·다른 기존 dirty·Git 쓰기·원시 로그는 건드리지 않았다.

## 사용자 표시 수용·기존 최적화 전체 병합 승인

사용자가 이번 UI 수정이 잘됐음을 확인했다. Editor 두 해상도 검증과 사용자 전체 표시 수용을 기록하되 연구 완료/스탯·S22 별도 관측값 등 기존 미검증 범위를 임의로 채우지 않는다.

사용자는 최신 main을 pull·병합·push하고 UI뿐 아니라 기존 최적화 작업도 모두 main에 병합하라고 명시했다. 승인 직후에는 원격 main9c012141/HEAD80c03561을 기준으로 Git 백업·전체 코드 검토를 진행했고 이후 아래처럼 통합을 완료했다. 전체 병합·검증을 거쳐 제품 통합0ebe909a의 main push·원격 일치까지 완료했다. 이전 선별 제품 반영과 이번 전체 범위를 구분하고 양쪽 기존 작업·staged/dirty 보존을 우선한다.

기존 최적화 문서의 구현 완료와 성능 미검증, 과거 main 미포함 기록을 구분해 조사한다. 다른 Task/상시 문서 수정이 필요하면 범위부터 보고하고 승인받는다. 현재 변경은 이 Task2개뿐이며 문서 담당은 제품·Git를 변경하지 않는다.

## 전체 최적화·UI 병합 진행 — 2026-11-25

전체 제품+UI 작업을 eaf05db4로 보존했고 local 산출물 원문은 stash27409dce/b068b956에 보존했다. origin/main9c012141과의 병합 검증을 거쳐 최종0ebe909a로 main 반영·push·원격 확인을 완료했다. main 연막/회피와 opt DoT 출처 wire를 함께 유지해 합성 schema13으로 정합했다. TDD의 기존12/11/10은 시점별 원문으로 보존했다. 실제 코드/검증기·SDK 합성 검증은 아래 결과와 같이 완료했으며 병합 후 새 native/실기 수용은 아직 없다.

과거 opt측정판/main선별판 구분은 당시 기록이며 이번 승인은 UI만이 아닌 기존 최적화 전체 통합이다. 기존 기능·실측 한계와 양쪽 작업을 보존하고 최종 통합0ebe909a의 main push·원격 일치를 확인했다.

## 전체 합성 소스 검증 완료 — 2026-11-25

eaf05db4의 기존 최적화 전체·UI와 origin/main9c012141을 합친 source 검증을 완료했다. 합성 schema13은 main 신규 연막 wire/토큰과 opt DoT 출처를 함께 보존한다. 신규 main 연막4cs 및 관련 Projectiles/Tower/MistShrinePanel은 origin/main 대비 diff0을 확인했고 Game씬 ARF15줄은 eaf 기준 그대로다.

private `.utmp/all-optimization-integration-20261125/final-merge/CompileResults.json`에서 Editor/AndroidDev/Release/EditorTools 오류0·경고25/28/32/0이다. 실제 runtime394·Editor35(합429) source membership 누락0·외부 참조524을 확인했다. 이것은 합성 source/SDK 검증이며 새 native·멀티 실기·표시 전범위·성능 검증은 아니다. main용 설정은 enableFrameTimingStats0/Android define DOTWEEN으로 유지하며 과거 opt 측정 결과를 이 설정의 FPS 효과로 소급하지 않는다.

최종 Git merge commit0ebe909ac191ac05c71cf0d7f7c790c355d1de2c 생성 후 로컬 main fast-forward·origin main push·fetch 검증을 완료했고 local main/origin main/opt3refs가 일치했다. 과거 진단 opt 전용/제품 선별 기록은 당시 범위이며, 이번 승인은 기존 최적화·관련 작업 전체의 통합이다. 백업과 원래 작업·staging 보존을 계속 유지한다.

최종 source freeze 근거는 `.utmp/all-optimization-integration-20261125/final-merge/FinalValidationReceipt.json`이다. 최종 검증9partial 문자열 정합 후 EditorTools 재컴파일 오류0·runtime hash 불변을 확인했다. 독립 정적 검수 blocker0으로 main 연막 manifest·DoT bool·기존 최적화 보존을 확인했고 main ProjectSettings diff0이다. 이 검수는 새 native/실기 PASS가 아니며 제품 통합0ebe909a의 push·원격 확인까지 완료했고 후속 문서 기록은 별도 commit 대상으로 둔다.

## 전체 제품 통합·로그 메뉴 포함·push 완료

제품 통합 커밋 `0ebe909ac191ac05c71cf0d7f7c790c355d1de2c`를 생성해 로컬 main fast-forward·origin main push·fetch 확인을 완료했고 local main/origin main/opt3refs가 일치했다. 현재 작업 branch는 opt로 복귀했다. 기존 최적화·자연회복 UI뿐 아니라 LogArchiveZip 로그 메뉴도 eaf와 최종 제품 통합에 포함됐다. 결과창은 기본2버튼이며 확인은 외부 동작0, 웹페이지 명시 선택은 폴더+페이지를 함께 여는 bool webRequested 경로를 유지한다. 최종 EditorTools 컴파일 오류0이다.

`.utmp/full-main-merge-20261125/FinalRestoration.json`에서 로컬 자동생성3파일의 원래 index/worktree 원문 SHA 복원을 확인했다. backup·stash는 보존했다. 이 기록은 Git 반영과 원본 보존 결과이며 새 native/실기·연구 완료/스탯·FPS 검증은 아니다. 문서 후속 commit의 SHA를 제품 커밋과 혼동하지 않는다.
