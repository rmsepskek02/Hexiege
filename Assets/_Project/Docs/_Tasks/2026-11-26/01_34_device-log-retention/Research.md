# 기기 로그 목록·선택 삭제·30일 보관 정리 — 조사

로그 목록을 읽을 때 모든 원문을 다시 해시하는 부담을 줄이고, PC에 같은 원문이 보관돼 있는 종료 세션을 쉽게 정리한다. 오래 사용하지 않은 기기 로그는30일 기준으로 정리하되 현재 세션·불명 상태·PC 사본/ZIP은 보호한다.

**문서 역할**
- 목적·질문: 기존 지연/삭제 차단의 원인과 새 승인 정책의 안전 경계를 확인한다.
- 포함: source 조사, 승인 정책, 구현/검증 상태와 미확정 위험.
- 제외·담당: 실행·검증·실기 절차는 [Plan](Plan.md), 현행 로그 규범은 [LogRules](../../../LogRules.md).
- 원본·갱신: 이번 조사 원본이며 최종 source와 안전 검증 수신 때 갱신한다.

## 1. 승인과 현재 상태

2026-11-26 사용자가 목록 속도 개선·수동 삭제 완화·PC 미보관이어도30일 미사용 기기 로그 자동 정리를 제안대로 구현하도록 승인하고 새 Task 작성을 명시했다. 폴더 시간은 직접 확인한 KST01:34다. helper/도구 구현·source fixture/SDK 검증은 완료했고 독립 QA 최종 개발 검증을 완료했고 blocker0이다. 개발자가 수행한 실제 기기 원문 삭제·앱 시작 자동 정리는0이며, 이후 사용자 수동 삭제 수용은 아래 별도 결과를 따른다. 새 Testcase는 만들지 않는다.

## 2. 원인과 읽기 관측

기존 Refresh는 세션 N개의 전체 원문 SHA와 PC 사본을 읽어 목록 준비에 불필요한 전체 파일 I/O를 더했다. 기존 삭제는 Complete/footer·PID 재사용 가능성과 선택 전체 중 하나라도 차단되면 모두 중단하는 조건이 사용자의 종료 세션 정리를 막았다. 목록용 metadata와 삭제용 실제 byte 검증은 다른 단계다. receipt-only를 원문 검증으로 대체하지 않는다.

메인이 실제 기기를 읽기 조회한 값은25로그/약120.5MiB/max71.36MiB, bulk metadata0.688초·SHA조회0·삭제0이다.1회 읽기 관측이며 동일 전후 실측·속도 개선율은 아니다. 원문256MiB·receipt64KiB의 도구 읽기 상한 개선은 이번에 보류한다.

## 3. 승인 정책과 위험

- 수동: 실제 PC 동일 byte 사본+앱 종료·경로/링크/원본 안정성을 확인한 적격 세션을 목록/용량 제시 후1회 확인으로 삭제한다. footer가 없어도 가능하며 부적격은 건너뛴다. 앱/프로세스 상태 불명은 보호한다.
- 자동: Development Android 앱 시작 worker1회, lastUsedUTC 기준30일. 수집/ZIP 때 사용 시각을 갱신하고 없으면 생성·최근 mtime 보호를 함께 적용한다. PC 미보관도 자동 정리 가능하지만 현재/링크/미래/unknown은 보호한다. raw+허용 sidecar+그 후 빈 디렉터리만 대상으로 하며 PC/ZIP은 불변이다.
- offline PC ZIP 사용은 PC에 기록한 뒤 다음 기기 연결 때 동기화한다. 동기화 전 기기 expiry 보호는 보장할 수 없다. 사용자에게 설명된 한계다.

QA가 사용 시각 rollback·갱신/삭제 race·불명 프로세스 판정 위험을 발견해 전문 담당이 단조 max와 공유 `.Retention.lock` 보완을 완료했다. 발견 사항 보완 후 source fixture는 통과했고 독립 QA 최종 개발 검증을 완료했고 blocker0이다. 확정 metadata/lock과 결과는 아래 최종 절을 따른다. 기존 로그 writer를 비동기 queue/worker로 바꾸는 작업이 아니며 PC/Editor 자동삭제 금지는 유지한다.

## 구현·제한 검증 완료와 실제 UI 확인

private `.utmp/log-retention-20261126/FinalVerificationReceipt.json`(SHAE74380A7630613B6EA5A307E8EC952F4C77A9AAF0C2E49198DD2D675717EDDB4)과 source/결과를 확인했다. runtime395·Editor35, Editor/AndroidDev/Release/EditorTools 오류0·기존 경고25/28/32/0·변경 경고0이다. historical receipt.remoteStable=false의 자격 보완 후 Safety35/source DeleteSelected23assert8scenario와 EditorTools 재검증을 통과했고 runtime 해시는 불변이다. 생성 shell6case는 Git Bash/stub ps의 private fixture이며 Android native 삭제 실행은 아니다. QA는 live6파일/영수증/추출4해시 일치와 새 private 검증의 Safety35·DeleteSelected23assert8scenario 독립 재실행 PASS를 확인해 blocker0으로 마감했다. shell6은 저장 결과 대조이며 독립 Android 실행이 아니다.4SDK 오류0과 최종 source/receipt 해시 불변도 확인했다. 이 판정은 개발 검증 한정이고 개발자 실제 기기 삭제·앱 재실행·Android 자동 startup worker·강제 thread interleaving은 미실증이며 이후 사용자 수동 삭제 수용과 구분한다.

수동 삭제는 과거 remoteStable=false/footer 부재만으로 거절하지 않는다. ValidateReceipt의 과거 영수증 일관성은 유지하고 현재 PC raw/receipt 출처 hash·sameDevice·remote size/SHA 전체 일치·앱 종료를 실제 확인한다. 목록 metadata와 receipt-only는 이 검증을 대체하지 않는다.

metadata는 LastUsedUtc.txt, 공유 claim은 .Retention.lock, 루트 최근 요약은 RetentionAudit.txt다. 단조 max로 갱신하고 사고 잔존 lock은 보호한다. worker는 보호 항목도 lazy 열거해 기아를 피하고 최대32회는 적격 삭제 시도 상한이다. 남은 것은 다음 앱 시작 대상이다. ZIP은 raw+receipt 쌍만 포함하고 PC LastUsed metadata·PC/ZIP 원본은 자동 삭제하지 않는다.

메인은 실제 Unity의 새 창·Refresh·SM_N971N 선택·“PC 보관 후보/미검증” 표기와 빨간 적격 삭제 버튼 표시를 확인했다. 이 확인 때 원문 수집·삭제0, 기기 앱 재실행/자동 정리0이다. 실제 삭제·30일 기기 자동 정리 수용으로 확대하지 않는다.

## 사용자 수동 삭제 수용·main 반영 승인 — 2026-11-26

사용자가 “삭제 잘 되는 것 같아”라고 수동 삭제의 정상 동작을 확인하고 main 병합·push를 요청했다. 문서 반영은 “기존 문서에 반영”을 선택했으므로 이 Task에서 기록한다. 삭제 개수·대상, PC/ZIP 실물 검증,30일 자동 정리의 기기 실행, 목록 속도 전후 수치는 보고되지 않아 확정하지 않는다. 수동 사용자의 전체 정상 수용과 개발자의 실제 삭제 실행0을 구분한다.

승인 당시 HEAD25a04c2c/최신 origin/main14dc0c2a 기준으로 제품5cs+helper meta의 반영을 준비했고 이후 아래와 같이 main 병합·push를 완료했다. 기존 dirty와 원본·검증 한계를 보존하고 최종 Git 결과는 아래 완료 기록을 따른다. 추가 메모리·제품·Git 변경은 문서 담당이 수행하지 않는다.

## 제품 main 반영·push 완료 — 2026-11-26

제품 commit04d12455를 최신 main14dc0c2a와 충돌0으로 병합해 `bb8f48360c1e818a1c13e91dfa0876719953b674`를 만들고 local main fast-forward·push·fetch로 origin/main/main/opt3refs 일치를 확인했다. 현재 opt branch로 복귀했다. 생성 파일3개의 기존 staged/working 원문 해시 복원은 private `.utmp/log-retention-merge-20261126/Restoration.json`에서 모두 true다. raw/ZIP/Profiler/scratch는 push 대상에서 제외했다.

합성 runtime395/Editor36(총431source)의4구성 컴파일 오류0·경고25/28/32/0, source 변화0과 로그 제품6SHA가 사용자 확인본과 같음을 확인했다. 근거는 `.utmp/log-retention-merge-20261126/validation/MergeValidationReceipt.json`(SHA1FBDBE38DC4D99190681EB7E392785993E1653D67FE757C603A9DCD6ABC24FB6)이다. Git 반영 완료는 새 native/실기 삭제 검증이 아니다. 사용자 수동 삭제 수용만 확인됐고 자동30일 정리의 실제 Android 실행·속도 전후 실측·개별 삭제 수량/대상·PCZIP 실물 수용은 미확인으로 유지한다. 문서 후속 commit은 제품 통합 commit과 별개다.
