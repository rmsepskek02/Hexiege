# 현재 PC 매칭 환경 차이 조사

다른 PC와 기기에서는 되지만 현재 PC와 이 PC에서 만든 빌드에서는 실패한다는 매칭 문제를 저장 로그와 SDK/환경에 대조한다. 진단 뒤 사용자가 승인한 익명 명시 로그아웃과 매칭 실패 복구 교정을 같은 Task에서 진행한다. 구현・정적 검사・빌드 시도・실제 매칭 성공을 각각 구분해 기록한다.

- 작업 시작: 2026-10-20 12:19 KST. 생성 직전 시계 03:19:26 UTC를 한국 시각으로 변환해 확인
- 사용자 승인: 최초 로그/환경 진단과 문서 작성 뒤, 같은 Task 계획 갱신・최소 구현・빌드 시도・테스트 절차 안내까지 명시 승인
- 브랜치/기준: `codex/ui-improvements`, HEAD/origin/main `088a2a7e` 동일은 메인 인계. 직전 `ec000217` 뒤 QuakeSpirit 모델 rename 1개 커밋 fast-forward 성공과 매칭 해결은 별개
- 현재 상태: 2026-10-21 사용자 확인과 최신 양쪽 로그 대조로 이번 랜덤 매칭 정상 진입・상대 인터넷 단절 시 Host 30초 복구・복구 후 재매칭 교정은 수용 완료. 넓은 인증/취소 경계의 미관측은 별도이며 이번 수용을 다시 여는 필수 관문이 아님. 최신 근거는 13절
- 문서 담당 범위: 승인 단계별 Task/관련 문서 갱신 이력은 각 절을 따른다. 이번 30초 교정 계획은 [Research.md](Research.md)/[Plan.md](Plan.md)・MatchFlow 접속 준비 절・MatchFlowUI M-2만 동기화. 제품 코드는 구현 담당자, 빌드 조작은 메인이 수행. 새 Task/TC/QA・원시 로그・SDK・Git 변경 없음

## 1. 조사 방식과 근거 경계

1~6절은 진단 단계의 누적 기록이다. 당시 읽기 전용/추가 승인 필요 표현은 그 시점의 경계로 보존하며 구현 승인 기록은 7~8절, 최신 사용자 수용과 종료 범위는 13절을 따른다.

diagnose 스킬의 피드백→재현→원인 후보→필요한 계측→수정/회귀 절차를 따른다. 현재 피드백의 시작은 이미 저장된 실패 로그의 단계/예외를 다시 대조하는 것이다. 이것을 결정적 자동 재현 루프나 새 SDK/서버 실행 PASS로 기록하지 않는다. 읽기 분석만으로 원인이 분리되지 않으면 필요한 최소 관측/계측을 제안하고 승인 뒤 진행한다. 새 fixture나 실제 서버 요청을 만들지 않는다.

| 증거 | 의미・한계 |
|---|---|
| 다른 PC main/다른 기기 정상 | 사용자 환경 비교 보고. 정확한 정상 실행 빌드 해시/서비스 응답이 확보된 대조 실험은 아님 |
| 현재 PC/현재 PC 빌드 실패 | 사용자 보고와 아래 저장 실패 로그. PC 환경 단일 원인으로 아직 확정하지 않음 |
| 현재 동일 소스 기준 | 메인 HEAD/origin/main 확인 인계. 로컬 설정/빌드 산출물/서비스 환경까지 동일하다는 뜻 아님 |
| 읽기 전용 소스・SDK・설정 | 실패 단계/가능 경계의 근거이며 실제 HTTP 원응답이나 수정 성공의 증거 아님 |

기존 UI 레이아웃의 동일 패널 크기・공통 고정 슬롯・충분한 여백 유지/기능 추가 없음 합의와 인풋필드 사용자 수용은 별도 보존한다. 매칭 진단으로 이를 미완료로 되돌리거나 기존 UI Task를 수정하지 않는다.

## 2. 저장 로그의 초기 확인

원본은 [에디터 RuntimeLog](../../../_Logs/_editor/2026-10-20/RuntimeLog.txt), [Android RuntimeLog_device](../../../_Logs/2026-10-20/12_07_logcat/RuntimeLog_device.txt)다. 원시 로그는 읽기만 하고 계정・티켓 식별자 원문을 문서에 복제하지 않는다. private 보관과 업로드/삭제 권한은 [LogRules §1.10](../../../LogRules.md#110-파일-기록--에디터-상시-활성)을 따른다.

| 실행 | 확인한 증상 | 아직 모르는 것 |
|---|---|---|
| Editor 01:44:10~01:54:20 | 티켓 생성 뒤 대기, 01:54:20.167 NetworkGameManager의 MatchmakingUnhandledException과 MatchmakerManager 116행의 timeout | 상대 부재/큐/설정/서비스 상태 등 실제 timeout 원인 |
| Android 01:44 실행 | 티켓 생성 뒤 GetTicketStatusAsync 경로, Unknown 21999/Something went wrong와 MatchmakerServiceException | 실제 HTTP 상태/본문・원래 서비스 오류 |
| Android 12:06 실행 | 같은 Unknown/MatchmakerServiceException과 ArgumentNullException bytes, Encoding.GetString→ResponseHandler.TryDeserializeResponse/CreateHttpException 경로 | 빈/누락 응답의 출처와 첫 오류/오류 처리 예외의 인과 |

문서 담당자도 해당 로그의 NetworkGameManager 예외・21999・ArgumentNullException/SDK 응답 처리 stack을 직접 검색 대조했다. Editor 파일에는 12:06:25의 다른 세션 머리말도 있으므로 위 초기 timeout 구간과 후속 세션을 섞지 않고 추가 대조한다. 시각은 각 로그의 기록값이며 서로 다른 기기의 절대 시각 차이로 지연을 계산하지 않는다. Editor timeout과 Android SDK 예외가 같은 원인이라고 합치지 않는다.

## 3. 확정・후보・미확정

- 로그에서 티켓 생성 다음 상태 조회/응답 처리로 넘어간 흔적이 있다. 생성 성공의 전체 HTTP 계약이나 매칭 성립을 의미하지 않는다.
- Android의 응답 오류 처리 중 null bytes를 문자열로 바꾸는 예외 경로가 관측됐다. HTTP 원상태/본문이 기록되지 않아 서비스 실제 원인은 미확정이다. SDK stack의 마지막 예외를 최초 실패 원인으로 대체하지 않는다.
- NetworkGameManager catch의 로그만 남고 UI OnError가 통지되지 않는 흐름은 별도의 오류 후 화면 잔류 후보로 메인이 조사 중이다. 매칭 실패의 서버 원인과 화면 복구 문제를 분리한다. 정확한 소스 대조 인계 전 수정 대상으로 확정하지 않는다.
- AndroidResolver의 useFullCustomMavenRepoPathWhenNotExport False→True 로컬 차이는 관측 사실이나 Editor 오류를 설명하거나 근본 원인을 확정하는 근거는 아니다. 임의 복구하지 않는다.
- 정상 PC와 실패 PC의 실제 SDK/환경/빌드 설정 비교는 구현 담당자가 읽기 전용으로 진행 중이다. 추가 인계 전 새 결과/수치를 만들지 않는다.

## 4. 관련 원본과 범위

[GameSystemRules 인덱스](../../../GameSystemRules.md)에서 [Network](../../../GameSystemRules/GameSystemRules_Network.md), [MatchFlow](../../../GameSystemRules/GameSystemRules_MatchFlow.md), [MatchFlowUI](../../../GameSystemRules/GameSystemRules_MatchFlowUI.md), [네트워크 TDD](../../../TechnicalDesignDocument_Network.md)를 찾았다. Network의 역할 독립성, MatchFlow의 준비 단계/맵 전송 실패와 매칭 단계의 분리, 화면 복구의 해당 원본을 보존한다. 준비 실패 규칙을 티켓 조회 실패에 그대로 끌어와 새 사양을 만들지 않는다.

현재 산출물은 진단 근거/다음 조사 단계다. 프로덕션 계측・예외/UI 복구 수정・SDK/프로젝트 설정 변경은 별도 계획과 사용자 승인이 필요하다. 로그 공유/업로드/삭제・새 테스트 실행 승인은 포함하지 않는다.

## 5. 문서 검토 상태

저장한 두 문서의 목적・로컬 링크・확정/미확정 범위를 직접 검수했다. `python3 Tools/check_docs.py`를 한 번 시도했으나 WindowsApps python3 프로세스 시작이 ResourceUnavailable로 실패했다. 검사 본문은 실행되지 않았고 다른 runtime을 탐색하지 않았다. `_Tasks/` 제외 한계도 있어 검사 PASS를 주장하지 않는다. 문서 자체가 새 서버 요청이나 코드 재현/수정 성공의 증거는 아니다. 후속 읽기 분석 인계는 이 Task에 추가한다.

## 6. 후속 읽기 대조와 원인 후보의 순위

초기 3절의 조사 중 문장은 당시 상태다. 아래는 구현 담당자의 최종 읽기 대조 인계와 메인/문서 담당자의 일부 직접 원문 확인을 분리해 반영한다. 코드/SDK/설정 변경・새 실서버 요청・계정/세션 삭제는 하지 않았다.

### identity와 실행 순서

12:06 Editor와 기기 로그의 UGS PlayerId 해시는 같고 Firebase UID 해시는 다르다는 담당자 대조・메인 일부 확인 인계다. 해시 원문도 문서에 복제하지 않는다. `Application/GameLog.cs:301~329`의 HashId는 salt 없는 SHA-256 앞 8바이트이고 실패 시 별도 고정 문자열을 반환함을 문서 담당자도 확인했다. 따라서 동일 UGS identity를 강하게 지지하지만 유한 해시 충돌 가능성까지 제거한 원본 식별자 확정으로 표현하지 않는다. Firebase 계정이 달라도 UGS identity가 다를 것이라는 전제는 성립하지 않는다.

동일 로그 시간축 대조 인계는 다음과 같다. 서로 다른 기기 간 기록 시계가 교정됐다는 근거 없이 정밀 네트워크 지연으로 계산하지 않는다.

| 구간 | 관측 순서 |
|---|---|
| 12:06 | 기기 티켓 생성 12:06:32.397 → Editor 티켓 생성 12:06:38.537 → 기기 상태 조회 오류 12:06:41.281 |
| 첫 실행 | 기기 티켓 생성 01:40:59.500 → Editor 생성 01:44:10.550 → 기기 오류 01:44:11.263. 첫 기기 초기화 근거가 없어 이 구간 identity를 직접 확정하지 않음 |

`MatchmakerManager.cs:46,95,99`의 각 polling은 1초 간격이다. Unity 공식 [요청 제한](https://docs.unity.com/en-us/mps-sdk/matchmaking/mm-rate-limits)은 player auth의 Get ticket status를 초당 1회로 명시하고, [문제 해결 안내](https://docs.unity.com/en-us/mps-sdk/matchmaking/mm-troubleshooting)는 player ID 단위 제한과 같은 identity의 여러 클라이언트 문제를 설명한다. 메인과 문서 담당자가 공식 원문을 확인했다. 두 클라이언트가 같은 identity로 각각 1초 polling하면 합산 초당 약 2회가 될 수 있다는 추론이지 실제 HTTP 429 관측은 아니다.

### SDK・서비스 timeout・UI 전달의 분리

- 설치 Multiplayer 2.0.0 `Runtime/Matchmaker/Apis/TicketsApi.cs:164`는 상태 조회의 200/400/404/429를 매핑한다. `Http/ResponseHandler.cs:70~115`는 오류 응답 역직렬화 중 null Data를 Encoding.UTF8.GetString에 넘길 수 있고 Exception 계열 ResponseDeserializationException으로 감싼다. `SDK/WrappedMatchmakerService.cs:336~338,385~392` 일반 catch는 이를 Unknown 21999로 변환한다는 소스 대조다. 문서 담당자도 map/문자열 변환/일반 catch를 확인했다. **21999는 원 HTTP 코드가 아니며 실제 400/404/429는 아직 미확보**다. 일반 status 0/NetworkError 21998 경로와도 구분한다.
- Editor 01:54 timeout은 `MatchIdAssignment.StatusOptions.Timeout` 서비스 응답을 받은 `MatchmakerManager.cs:116` 분기임을 직접 확인했다. 앱의 로컬 10분 상수라고 설명하지 않는다.
- UI 잔류는 별도 전달 결함이다. `NetworkGameManager.cs:620~625`는 catch에서 로그만 남기고 throw/OnError를 하지 않으며, `Presentation/UI/ViewModels/BattleViewModel.cs:273~299`는 상위 catch에 도달해야 실패 시 IsMatchmaking을 내린다. 메인과 문서 담당자가 두 파일을 직접 확인했다. 실패 삼킴 때문에 상위 실패 정리가 실행되지 않는 경로는 확인됐지만 이것을 서비스 실패의 최초 원인으로 합치지 않는다.
- manifest/lock/설치 패키지의 Multiplayer 2.0.0・Authentication 3.6.0・Core 1.16.0은 일치한다는 담당자 대조다. queue는 hexiege-random이고 추가 attributes는 없으며 초기화 옵션이 없어 production 기본 경로다. 실제 environment ID는 로그에 없어 확정하지 않는다. 정상 PC 설정 대조와 Resolver 인과는 여전히 미확정이다.

### 순위가 있는 가설과 반증 조건

메인은 아래 네 축의 요약을 사용자에게 제시했고 실패한 매칭 조합 질문의 응답을 기다린다. 순위는 읽기 근거의 우선순위이며 확률/확정 원인 판정이 아니다.

| 순위 | 후보/근거 | 구분에 필요한 예측・반증 |
|---|---|---|
| 1 | 동일 UGS identity 동시 polling 합산 제한 | Editor를 완전히 중지한 문제 기기↔정상 PC의 서로 다른 identity 조합에서만 오류가 사라지면 지지. 원 HTTP 429가 없어 아직 확정 불가 |
| 2 | 큐/풀/서버 응답에 따른 매칭 불성립 | identity 경합 없는 실행에서도 서비스 timeout/원 400・404 등이 남으면 별도 큐/요청 원인을 검토. Editor timeout과 Android 오류가 같은 이유일 필요 없음 |
| 3 | 환경/전송 응답 또는 SDK 오류 변환의 관측 손실 | 원 상태/본문이 확보되면 SDK 일반 Unknown과 실제 서비스/전송 문제를 분리 가능. null bytes만으로 PC 네트워크나 Resolver를 원인 확정하지 않음 |
| 4 | 오류 뒤 UI 잔류의 별도 전달 경로 | 예외가 상위로 전달되지 않으면 매칭 실패 상태가 내려오지 않는 소스 경로는 확인됨. 단독 서버 원인 가설이 아니라 실패 뒤 화면 문제의 독립 축 |

최소 대조 후보는 **Editor 중지 후 문제 기기↔정상 PC**다. 계정/데이터 삭제 없이 실패 조합부터 확인하며 아직 실행하지 않았다. 사용자 응답과 필요 승인 전 코드 구현・SDK 업데이트・세션 삭제・네트워크 설정 변경을 하지 않는다. 현재는 원인 후보가 좁혀진 상태이며 해결 PASS가 아니다.

## 7. 익명 세션 복원 경로와 추가 로그

앞 절은 진단 당시의 미승인/읽기 전용 상태다. 이후 읽기 대조에서 `LoginUseCase.cs:394,475`의 인자 없는 UGS SignOut과 설치 AuthenticationServiceInternal의 `clearCredentials=false` 기본값을 확인했다. 기본 로그아웃은 AccessToken을 정리하지만 SessionToken/PlayerId를 보존하며, 익명 SignIn은 캐시 세션으로 기존 player를 복원할 수 있다. 익명 Bridge 경로의 Firebase UID는 빈 값 확인에 쓰이고 익명 UGS identity에 바인딩되지 않는다. 문서 담당자도 해당 구현/기본값을 읽었다.

따라서 다른 Firebase 익명 UID가 같은 UGS identity로 복원될 수 있는 코드 경로가 있다. 이번 과거 로그인 경위나 빌드가 토큰을 복제했다는 원인까지 입증한 것은 아니다. 앱 데이터 삭제・개발 profile 변경・계정 삭제는 수행하지 않는다.

사용자는 실패 조합이 현재 PC Editor↔현재 PC 빌드 기기이며 양쪽 명시 로그아웃 후 익명 재시도도 실패했다고 확인했다. 새 근거는 [18:19 기기 로그](../../../_Logs/2026-10-20/18_19_logcat/RuntimeLog_device.txt)와 같은 날짜 Editor 로그다. 메인 대조 인계상 두 실행의 Bridge는 각각 2회이며 전후 UGS 해시는 유지되고 최신 양쪽 해시도 같다. 원본 식별자/해시는 복제하지 않는다.

Editor 티켓 생성 18:15:48.955 → 기기 생성 18:15:49.613 → Editor 상태 조회 18:15:52.642의 21999/null bytes라는 기록 순서다. HTTP 429는 여전히 미확보다. 과거 로그와 새 실패를 서비스 원인 확정으로 합치지 않는다.

## 8. 승인된 최소 교정과 실행 인계 대기

사용자는 **같은 Task 문서 갱신 뒤 바로 구현・빌드 시도・테스트 절차 안내**를 명시 승인했다. 이 승인을 6절의 미실행 진단 상태와 구분한다. 제품 구현은 전문 담당자, 검토/Unity 빌드 조작은 메인이 수행하며 실제 결과는 후속 근거를 받아 추가한다.

1. 명시적 익명 로그아웃에서만 UGS 저장 자격을 정리하여 다음 Firebase/UGS 익명 시작을 모두 새 게스트로 만든다. 이전 닉네임/전적은 승계하지 않는다. 단순 앱 종료/재실행・자동 Bridge는 무조건 새 계정으로 바꾸지 않으며 실계정 OIDC/서버 진행도는 유지한다. 별도 기존 게스트 이어하기・전체 profile migration・소유권 테이블은 추가하지 않는다.
2. 매칭 실패를 상위로 전달해 대기/타이머/로딩 상태를 복구하고 재시도가 가능하게 한다. 요청 회차・취소 중 티켓 생성 완료・늦은 결과 정리는 이번 영향 범위에서 필요한 최소 안전을 포함한다. SDK 업데이트/네트워크 설정 원복으로 확대하지 않는다.

정책 원본은 [AuthSystemRules 「로그아웃」 규칙 2~3](../../../AuthSystemRules.md#로그아웃)에 이번 사용자 합의만 동기화했다. 기존 세션 보존/실계정/자동 로그인 계약을 바꾸지 않는다. 정책 승인/문서 반영을 구현・빌드・429 원인 확정・실기 성공으로 표시하지 않는다. 이전 UI 수용과 원시 로그 private 보관・무수정/무삭제/무업로드를 유지한다.

승인 반영 뒤 문서 검사를 한 번 재시도했으나 WindowsApps python3 시작 ResourceUnavailable로 본문은 실행되지 않았다. 다른 runtime 탐색 없이 링크 대상 존재와 Auth 정책 diff/저장 한글을 직접 검수했다. 자동 검사 PASS는 아니다.

## 9. 중간 구현・검사와 빌드 미시작 기록

메인 인계에 따라 최소 교정 소스 3개가 구현됐다. 구현 담당자의 실제 메서드 추출 대체환경 검사는 RED 3개/GREEN 19개 PASS 인계이며, 전체 초기 Runtime 컴파일은 errors 0/warnings 23이었다. **그 뒤 추가 보강이 있어 위 초기 컴파일을 최종본 검사로 재사용하지 않는다.** 보강 후 전체 컴파일은 담당자가 진행 중이며 최종 결과/파일 근거는 인계 뒤 확정한다. 실제 사용자 매칭 성공・HTTP 429 원인 확정은 아니다.

메인은 Unity Build Profiles에서 Android・APK를 선택하도록 기존 AAB toggle을 해제하고 Development 설정을 유지하는 데까지 준비했다. 이후 Computer Use가 Escape 중단 신호를 보고해 UI 조작을 중단했다. **실제 Build 버튼과 파일 선택을 실행하기 전이며 빌드는 미시작이다. APK 산출물이 없고 빌드 성공도 아니다.** 빌드 설정 준비를 빌드 실행/완료로 표현하지 않는다.

원시 로그・식별자 복제 없이 중간 인계만 기록했다. 이 절은 최종 소스 컴파일・APK・실기 검증을 기다리는 상태이며 이전 입력 수용/로그 보호 경계를 유지한다. 문서 담당자는 UI를 추가 조작하거나 빌드를 재시작하지 않았다.

## 10. 최종 정적 검사와 사용자 직접 빌드 진행

9절의 빌드 미시작/APK 없음은 에이전트 UI 중단 당시의 상태다. 이후 사용자는 빌드를 직접 시작했다고 알리고 나머지 작업 진행을 요청했다. 메인은 Editor.log에서 Android `BuildPlayerAndRun` 실제 실행과 bee/UnityLinker 단계를 관측했다. **현재 사용자 직접 빌드 진행 중이며 성공・APK 산출물・설치/실행은 아직 확정하지 않는다.** 에이전트가 중단했던 설정 준비와 사용자의 실제 Build 실행은 구분한다.

최종 구현 담당자 인계는 Runtime errors 0/warnings 23과 실제 메서드 추출 GREEN 19 PASS이며, 그 뒤 소스 변경은 없었다. 문서 담당자도 아래 `runtime-final.log`에서 오류/경고를 재집계하고 `green-probe.log`의 GREEN_PASS=19를 확인했다. 이는 정적/대체환경 결과이며 실제 서비스 매칭・실기 회귀의 결과가 아니다.

임시 근거 폴더: `C:/Users/rmsep/.codex/visualizations/2026/10/16/01a145df-1845-7ef1-bbdf-d5056d2f32d6/matchmaking-auth-fix/`

최종 소스는 LoginUseCase・NetworkGameManager・BattleViewModel의 3개다. 메인/담당자 인계상 마지막 수정 시각은 LoginUseCase 19:05:29, NetworkGameManager/BattleViewModel 19:09:24이며 최종 컴파일 뒤 변경 없음 경계로 기록한다. 빌드 결과는 추가 실제 관측을 받은 뒤 갱신한다. 원시 로그/계정 식별자 복제 없이 기존 입력 수용과 보관 규범을 유지한다.

<a id="11-원래-실패-조합의-사용자-매칭-수용"></a>

## 11. 원래 실패 조합의 사용자 매칭 수용

사용자는 새 빌드 기기와 Editor 양쪽에서 **명시 로그아웃 후 익명으로 시작해 매칭이 성공했다**고 보고하고 로그 저장・문서 갱신을 승인했다. 원래 실패한 실행 조합에서 새 게스트 로그인 후 실제 매칭 성공은 사용자 수용이다. 정확한 빌드 해시/설치 산출물 동일성과 HTTP 원 429를 실증한 결과로 확대하지 않는다. 10절의 빌드 진행 중/실기 미확정은 당시 기록으로 보존한다.

추가 근거는 [19:43 기기 로그](../../../_Logs/2026-10-20/19_43_logcat/RuntimeLog_device.txt)와 [같은 날짜 Editor 로그](../../../_Logs/_editor/2026-10-20/RuntimeLog.txt)다. 메인이 최신 19:34~19:43 구간을 직접 대조한 범위는 아래와 같으며 원시 식별자・해시・토큰은 문서에 복제하지 않는다.

- 기기는 19:35:23.476 로그아웃→19:35:53.399 익명 성공→19:35:53.873 UGS Bridge 순서다. 최신 기기 UGS 기록은 로그아웃 전 19:34 기록과 다르다.
- Editor는 19:38:08.027 익명→19:38:08.466 Bridge다. 최신 Editor UGS 기록은 이전 18:15 기록과 같고 최신 구간에는 로그아웃 기록이 없으며 마지막 로그아웃은 18:15:59다. **양쪽 명시 로그아웃은 사용자 보고에 귀속하고, 로그로 양쪽 모두 새 ID를 만들었다고 주장하지 않는다.**
- 최신 양쪽 UGS 기록은 서로 다르고 MatchId 기록은 일치한다. Editor 19:38:13.935 매칭 완료/Host, 기기 19:38:14.459 완료/Client, 게임 시작 RPC는 각각 19:38:36.325/19:38:37.144다. 서로 다른 기기 시각 차이를 네트워크 지연으로 계산하지 않는다.
- 이 새 구간의 MatchmakingUnhandledException/UgsSignOutFailed는 0이다. 전체 ERROR 0은 아니다. 양쪽 19:39 UAS-MOVE-AUTH END ERROR 각 1개가 있으며 이번 매칭 성공과 분리한다. 해당 전투 진단의 원인/영향 분석은 실행하지 않았다.

| 확인 축 | 현재 범위 |
|---|---|
| 사용자 수용 | 새 빌드 기기↔Editor, 양쪽 명시 로그아웃→익명 시작 뒤 실제 매칭 성공. 로그는 기기 자격 전환/현재 양쪽 identity 분리와 배정/게임 시작을 확인하며 Editor 새 ID 생성은 입증하지 않음 |
| 기확인 소스/검사 | 교정 소스 3개, 최종 Runtime 오류 0/기존 경고 23, 실제 메서드 추출 19개 PASS. 실기 보고와 별도 근거 |
| 원 서비스 오류 | 실제 HTTP 429는 미확보. 기존 동일 identity/제한 가설을 확정 원인으로 승격하지 않음 |
| 별도 실기 미확인 | 단순 앱 재실행의 게스트 보존, 실계정 OIDC/진행도 보존, 이전 닉네임/전적 미승계, 취소→재시작과 늦은 결과 정리, 오류 안내/로딩 복구와 재시도 |

따라서 원래 실패 조합의 집중 매칭 수용과 남은 분기 검증을 구분한다. 현재 수용만으로 보존한 legacy 주석 삭제/코드 정리를 실행하지 않는다. 사용자 승인에 따라 Task・관련 현재 상태/남은 일/마일스톤・공용/관련 에이전트 메모리를 필요한 최소로 동기화하며 제품 코드・원시 로그・이전 UI Task・Git은 변경하지 않는다.

이번 반영은 기존 Task 2개・PROJECT_STATUS/ROADMAP/WORK_HISTORY・공용 MEMORY・programmer 인덱스/network-infra・QA 인덱스/patterns의 10개 파일이다. 새 TC/QA 실행・추가 테스트 요청・Task 전체 종결은 하지 않았다. 최종 문서 검사는 메모리 증가 기준 반영 옵션을 포함해 `python3 Tools/check_docs.py --update-baseline` 한 번 시도했으나 기존 WindowsApps python3 ResourceUnavailable로 시작하지 못했다. 검사 본문/기준값 갱신은 실행되지 않았고 baseline 파일을 수동 수정하지 않았다. 신규 링크/앵커・수용 범위와 저장 내용을 직접 대조했고 변경 형식 검사는 exit 0이었다. 자동 검사 PASS/진단 0건은 아니다.

## 12. 오프라인 후속 로그와 접속 준비 30초 교정 승인

후속 [19:59 기기 로그](../../../_Logs/2026-10-20/19_59_logcat/RuntimeLog_device.txt)와 Editor 누적 로그를 대조한 메인 인계다. 실계정 Editor↔익명 기기의 19:57/19:58 정상 매칭 2회 이후, 기기를 오프라인으로 둔 실행에서 기기 티켓 상태 조회가 Cannot connect/NetworkError 21998로 실패하고 티켓 삭제 정리도 실패했다. 문서 담당자도 기기 조회/정리 실패와 Editor Host 시작 기록을 직접 확인했다. 식별자는 복제하지 않는다.

온라인 Editor는 19:58:49 Found 이후 19:58:51 Host 시작까지 진행했지만 Client가 접속하지 않아 `게임에 접속하는 중...` 로딩이 남았다. 기존 맵 package timeout은 맵 준비보다 앞선 이 대기 구간을 덮지 못한다. 앞선 정상 매칭/익명 교정 수용은 유지하고 이번 현상을 별도 준비 수명 문제로 분리한다. 원래 429 가설을 이번 21998 오프라인 오류와 합치지 않는다.

사용자는 **우선 30초로 두고 교정 진행**을 승인했다. 시작은 매칭 Found에서 접속중 로딩을 켜는 순간, 종료는 Game 씬 전환 시작이다. 실패를 먼저 확인하면 즉시 로딩 해제・해당 회차 정리・실패 팝업을 수행하고 전달 가능한 상대에게 알린다. 미전달/상대 미접속이어도 각자의 해당 구간은 최대 30초다.

이 동작은 [MatchFlow 규칙 16의 접속 준비 상한](../../../GameSystemRules/GameSystemRules_MatchFlow.md#매칭-성립-후-최초-접속-준비-상한)과 [MatchFlowUI M-2](../../../GameSystemRules/GameSystemRules_MatchFlowUI.md#무작위-맵-준비-실패-ui)에 최소 동기화했다. 티켓 매칭 대기・기존 맵 10초/재전송・결과/재경기/이탈 타이머와 별개이며 기존 숫자/번호를 바꾸지 않았다.

예상 교정 범위는 NetworkGameManager・BattleViewModel과 직접 필요한 기존 LobbyManager다. 정확한 실제 소스・검사・빌드 범위는 구현 담당자 인계 뒤 확정한다. 요청 회차 소유권・취소/늦은 결과 보호와 기존 비활성 주석 보존을 유지한다. 지금은 승인된 계획 단계이며 구현/30초 실기 통과・Task 전체 완료가 아니다. 새 TC/QA/SDK/Git・원시 로그 변경은 하지 않는다.

### 구현 착수 뒤 확정한 최소 범위

메인 계획 검토 뒤 구현 담당자에게 진행이 전달됐다. 현재 예상 실제 수정은 NetworkGameManager・BattleViewModel 두 파일로 좁혔으며 최종 적용 목록/검사는 후속 인계로 확정한다.

Lobby/RelayManager는 SDK await가 끝난 뒤 공유 상태를 쓰므로 timeout 뒤 늦은 결과를 무시하는 것만으로 다음 회차를 보호할 수 없다. 이전 준비 회차의 SDK 작업 종료/정리가 끝날 때까지 새 session 생성을 기다리는 barrier를 두는 방향이다. 커스텀 생성/참가에도 이 안전 대기만 적용하도록 승인됐고, 커스텀의 기존 대기/timeout 사양은 바꾸지 않는다.

새 서버 데이터/heartbeat 없이 기존 실패 RPC와 NGO disconnect를 사용한다. 미접속 상대에게 통보할 수 없으면 각자의 승인된 30초 상한으로 복구한다. 감시 종료는 Host의 NGO LoadScene Started 반환 또는 Client의 Game SceneEvent.Load 관측에 한정한다. 단순 StartHost 성공・RPC 송신・로드 요청 호출만으로 성공 종료하지 않는다. 아직 구현/검사 완료・실기 수용을 의미하지 않는다.

### 최종 소스・검사 인계

위 착수 문장의 예상/미확정은 당시 상태다. 이번 실제 제품 수정은 `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameManager.cs`, `Assets/_Project/Scripts/Presentation/UI/ViewModels/BattleViewModel.cs` 두 파일이다. 같은 Task Research/Plan과 MatchFlow/MatchFlowUI 두 규범을 합해 **이번 교정 범위는 6개 파일**이며 Task 전체 누적 변경 수와 구분한다. 앞선 Auth/UI/Status/메모리・SDK・원시 로그・Git은 이번에 변경하지 않았다.

- Found 회차별 30초를 감시하고 실제 Host LoadScene Started/Client Game Load에만 성공 종료한다. 성공 뒤 준비 참조/구독을 해제해 후속 custom/재경기를 이전 timer가 막지 않는다.
- 실패/timeout은 한 번만 M-2 로딩/모달 복구를 알리고 기존 peer 실패 통보/NGO disconnect를 사용한다. ViewModel은 SDK 정리 완료를 기다리지 않고 로딩/로비를 복원하며 중복 실패 팝업을 막는다.
- 이전 SDK 종료・직렬 cleanup의 static barrier와 캡처한 NGO 소유자를 사용해 새 manager/새 요청의 상태를 늦은 결과가 덮지 않게 한다. 기존 scene verifier를 보존/복원하면서 만료 뒤 늦은 Game 로드를 거부한다. cleanup 대기는 취소 가능하며 정리 실패는 관측/예외 전달하고 안전하지 않은 다음 SDK 생성을 차단한다. custom 생성/참가의 안전 대기만 연결하고 기존 custom 대기 사양은 유지한다.
- 대체된 legacy는 보존했다. 이번 구현을 실제 기기에서 검증하기 전 최종 삭제하지 않는다.

임시 근거: `C:/Users/rmsep/.codex/visualizations/2026/10/16/01a145df-1845-7ef1-bbdf-d5056d2f32d6/random-preparation-deadline/`. 메인과 문서 담당자가 `runtime-final.log`, `android-final.log`, `probe.log`를 대조했다.

| 검사 축 | 최종 확인 | 한계 |
|---|---|---|
| Runtime 정적 컴파일 | exit 0・errors 0・warnings 23, UNITY_ANDROID/UNITY_EDITOR 포함 | Android APK/Unity native 실행 아님 |
| Android 조건 정적 컴파일 | exit 0・errors 0・warnings 30, UNITY_ANDROID 포함/UNITY_EDITOR 제외 | 기기 빌드/실기 수용 아님 |
| 실제 메서드 추출 회귀 | 31개 PASS 직접 확인. 30초 경계/한 번 실패・정리, late Lobby/Relay/Join/NGO 로드, 성공 종료/다음 회차, queued cancel, cleanup 실패/새 SDK 차단 포함 | SDK/clock/NGO는 대역이며 실제 서버/네트워크 통합 아님 |
| 소스 동일성 | `manifest.csv` 두 해시를 메인과 문서 담당자가 각각 현 파일과 대조해 일치 확인 | 소스 일치가 사용자 수용을 대신하지 않음 |
| 산출물 경계 | 기존 소스 partial 두 개를 scratch 응답 파일에 보충해 컴파일. DLL은 scratch 출력, Library 무변경 | 새 제품 파일을 추가한 것이 아님 |

당시 UI/native/Android APK 빌드・실서버 실기는 실행하지 않았다. 기존 기기 앱은 30초 교정 전 빌드이므로 앞선 정상 수용을 이 교정의 통과로 재사용하지 않는다. 최종 문서 정합성 검사는 아래 인계 기록을 따르며 기존 Python 한계를 숨기지 않는다. Task 전체 완료나 모든 회귀 PASS로 보고하지 않는다.

최종 저장 뒤 문서 검사를 한 번 실행했으나 기존 ResourceUnavailable로 검사기 본문은 실행되지 않았다. 다른 runtime 탐색이나 PASS/진단 0건 주장은 하지 않는다. 네 문서의 로컬 링크/신규 앵커・승인 범위・실기 한계를 직접 검수했고 메인은 두 소스 manifest와 변경 형식 검사 exit 0을 확인했다. 이번 6개 파일은 아래와 같다.

- `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameManager.cs`
- `Assets/_Project/Scripts/Presentation/UI/ViewModels/BattleViewModel.cs`
- `Assets/_Project/Docs/_Tasks/2026-10-20/12_19_matchmaking-environment-diagnosis/Research.md`
- `Assets/_Project/Docs/_Tasks/2026-10-20/12_19_matchmaking-environment-diagnosis/Plan.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_MatchFlow.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_MatchFlowUI.md`

## 13. 랜덤 매칭 세 단계 사용자 수용 완료 — 2026-10-21

사용자는 안내한 정상 진입・인터넷 단절 뒤 복구・복구 후 재매칭을 확인하고 기록을 승인했다. 메인은 Editor 일일 로그와 기기 `00_36_logcat` 로그를 대조했다. 기기 파일의 10월 20일 누적분은 제외하고 10월 21일 해당 구간만 판정했다. 아래 시각은 각 로그의 기록 시각이며 서로 다른 기기의 시계 차를 지연 측정으로 사용하지 않는다. 원시 식별자/해시/토큰은 복제하지 않는다.

| 단계 | 메인 로그 대조 근거 | 수용 범위 |
|---|---|---|
| 정상 진입 | Editor 00:30:58.610 Found → 00:31:05.063 씬 로드 시작 → 00:31:28.535 게임 시작 RPC, 기기 00:31:29.106 시작 RPC. 양쪽 동일 매치 확인 | Editor Host・기기 Client 정상 경기 진입 |
| 단절 뒤 복구 | Editor 00:31:42.863 Found → 00:32:12.869 준비 상한 도달(30.006초) → 12.870 M-2 UI → 13.155 Disconnect 완료. 기기는 00:31:44.353 티켓 조회 Cannot resolve host, 44.468 정리 실패 | 상대 통보/접속이 없는 Host도 30초 상한 뒤 실패 안내・연결 정리. 기기는 Found 전 티켓 실패이므로 준비 deadline 0회가 정상 |
| 복구 후 재매칭 | Editor 00:33:40.360 Found → 46.027 씬 로드 → 47.441 시작 RPC, 기기 47.638 시작 RPC. 양쪽 동일 매치 확인. 00:35:56 최종 종료까지 준비 오류 재발 미관측 | 복구 후 새 매칭과 게임 진입, 성공 씬 전환 뒤 이전 준비 감시의 오실패 미관측 |

Editor 준비 deadline과 M-2 UI는 각 1회, 기기 준비 deadline은 0회다. 첫 정상 게임의 시작 RPC가 Found 약 30초 뒤여도 씬 로드 시작은 약 6초 뒤다. 이 상한은 **씬 전환 시작 전 준비**에 적용하며 실제 씬 로딩 완료/경기 시작까지 30초로 제한하는 규칙이 아니다.

이번 랜덤 매칭 정상 진입・단절 상대에 대한 30초 복구・복구 후 재매칭 교정은 사용자 수용 완료로 종료한다. 12절의 APK/native/실서버 미실행은 최종 정적 인계 당시 기록이며 최신 사용자 실행을 부정하지 않는다. 정확한 APK 해시/빌드 실행 로그는 별도로 제공되지 않았다. 기존 Runtime 0/23・Android 조건 0/30・추출 회귀 31 PASS는 정적/대역 근거로 그대로 보존한다.

기기 기존 게스트 자동복원은 로그 확인됐다. 재실행 전후 닉네임/전적 상세 보존・실계정 닉네임/전적 보존・익명 이전 진행도 미승계・취소 직후 경쟁/늦은 결과・역할 교환 등 넓은 경계는 별도 미관측이다. 원 HTTP 429도 여전히 미확정이다. 이를 이번 수용의 취소나 필수 반복 검사로 삼지 않으며 모든 인증/경쟁 분기 PASS로 확대하지 않는다. 소스 legacy 주석 삭제・규칙 변경・새 TC/QA・원시 로그/Git 변경은 이번 문서 반영 범위가 아니다.

이번 승인 기록은 기존 Task 2개・현황/로드맵/이력 3개・공용 및 programmer/QA 메모리 5개, 총 10개 기존 파일에 한정했다. 최종 자동 문서 검사와 메모리 baseline 갱신은 한 번 시도했으나 ResourceUnavailable로 본문 실행 불가여서 PASS가 아니며 baseline도 수동 변경하지 않았다. `_Tasks` 기계 검사 제외와 별개로 최신 링크 대상/앵커 의미・저장본・완료 범위를 수동 검수했다.

자동복원 근거 보완: 메인의 앞선 19_59 로그 대조에서 기기 19:35 bridge와 새 프로세스의 19:51 자동 로그인 bridge의 동일 UGS, 19:57 명시 로그아웃 뒤 새 UGS를 확인했다. 따라서 일반 재실행의 게스트 유지 전체를 미관측으로 남기지 않으며, 재실행 전후 닉네임/전적 상세 값 보존만 별도 경계다. 식별자 값은 복제하지 않는다.
