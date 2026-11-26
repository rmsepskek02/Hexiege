# 기기 로그 목록·선택 삭제·30일 보관 정리 — 계획

목록 조회는 metadata만 빠르게 읽고, 삭제는 실제 원문이 PC에 안전하게 보관됐는지 따로 확인한다. 사용자가 승인한30일 기기 보관 정책과 수동 정리 편의를 구현하면서 현재 로그와 불명 대상은 건드리지 않는다.

**검증 전 보존:** 기존 로직을 대체할 때 검증 전 주석 비활성화로 보존한다. 자동 fixture는 개발 검증이며 실제 사용자 기기 삭제 수용을 대신하지 않는다.

**문서 역할**
- 목적·질문: 승인 정책을 구현하고 무손실/보호 경계를 검증한다.
- 포함: 구현 순서, 중단/수용 기준과 실제 확인 절차.
- 제외·담당: 원인·읽기 관측은 [Research](Research.md), 현행 규범은 [LogRules](../../../LogRules.md).
- 원본·갱신: 실행 계획 원본이며 source/QA/실행 결과 수신 뒤 상태를 갱신한다.

## 1. 현재 단계

목록 bulk metadata·수동1confirm·30일 미사용 앱 시작 정리 구현과 rollback/race/프로세스 unknown 보호 보완을 완료했다. source fixture/SDK 결과는 아래 최종 절과 같고 독립 QA 최종 개발 검증을 완료했고 blocker0이다. 개발자 실제 원문 삭제0/Git 쓰기0이며256MiB 상한은 유지한다.

## 2. 구현·보호 범위

1. 목록: 세션 size/metadata를 bulk로 읽고 원문 SHA/PC 전체 읽기를 목록 준비에서 제거한다. metadata조회 성공을 삭제 적격 검증으로 쓰지 않는다.
2. 수동: 선택 항목 각각의 실제 PC/remote size·SHA·앱 종료/경로/링크/원본 변경을 확인한다. 적격 목록과 용량을1회 확인하고 나머지는 skip한다. footer/전체 record 완결은 삭제의 별도 필수조건으로 강제하지 않는다. 취소·미보관·현재·불명·변경은 보호한다.
3. 자동: DevAndroid 시작1회 worker로30일 미사용만 검사한다. lastUsedUTC는 수집/ZIP 갱신, 없으면 생성·최근 mtime 보호를 적용한다. 현재/링크/미래/unknown은 제외하고 raw·허용 sidecar·빈 폴더만 정리한다. PC 사본/ZIP은 삭제하지 않는다.
4. 사용 시각은 단조 max와 공유 lock으로 회귀/race를 막는 안을 검증한다. offline PC 사용 기록의 다음 기기 연결 동기화 전 한계도 유지한다. 정확 source/허용 sidecar·worker/lock 동작은 최종 결과에 맞춘다.

## 3. 검증 대응·완료 기준

| 항목 | 개발 검증 자료 | 실제 범위/한계 |
|---|---|---|
| 목록 불필요 I/O 제거 | source 경로·metadata 요청/원문 SHA 분리 | 단일0.688초를 old 대비 향상률로 쓰지 않음 |
| 수동 적격/skip/취소·PC byte 변조 | 기존 도구 경계 fixture/실제 원문 검사 경로 | receipt만으로 성공 판정 금지·기기 삭제 아직0 |
|30일·fallback timestamp·미래/unknown/link/현재 보호 | helper 순수 경계·filesystem fixture 계획 | PC 미보관 자동 정리는 승인 정책이나 실제 기기 삭제 미실행 |
| rollback/race·불명 프로세스 | 단조 max·공유 lock·unknown fail-closed 검증 | 최종 QA 결과 전 완료 선언 금지 |
| PC/ZIP 보존·offline 사용 | 기존 archive/usage 경로와 동기화 경계 | 동기화 전 기기 expiry 보호 보장 불가 |

실제 사용자 절차는 최종 검증 후 이 Plan에서 안내한다. 목록 확인·PC복사·ZIP 결과 확인과 실제 삭제 실행은 별개이며 승인 없이 기기 원본을 삭제하는 테스트를 하지 않는다. 자동 정책의 앱 시작 스케줄도 실기 배포/실행 전 검증 범위를 넘겨 주장하지 않는다. source/SDK·fixture·native UI·실기 삭제를 분리하고 unknown 보호나 원본 안정성이 깨지면 중단해 교정한다. 새 상시 도구 framework·보관 quota·PC/ZIP 자동삭제·256MiB 상한 확대는 범위 밖이다.

## 구현·제한 검증 완료와 실제 UI 확인

private `.utmp/log-retention-20261126/FinalVerificationReceipt.json`(SHAE74380A7630613B6EA5A307E8EC952F4C77A9AAF0C2E49198DD2D675717EDDB4)과 source/결과를 확인했다. runtime395·Editor35, Editor/AndroidDev/Release/EditorTools 오류0·기존 경고25/28/32/0·변경 경고0이다. historical receipt.remoteStable=false의 자격 보완 후 Safety35/source DeleteSelected23assert8scenario와 EditorTools 재검증을 통과했고 runtime 해시는 불변이다. 생성 shell6case는 Git Bash/stub ps의 private fixture이며 Android native 삭제 실행은 아니다. QA는 live6파일/영수증/추출4해시 일치와 새 private 검증의 Safety35·DeleteSelected23assert8scenario 독립 재실행 PASS를 확인해 blocker0으로 마감했다. shell6은 저장 결과 대조이며 독립 Android 실행이 아니다.4SDK 오류0과 최종 source/receipt 해시 불변도 확인했다. 이 판정은 개발 검증 한정이고 개발자 실제 기기 삭제·앱 재실행·Android 자동 startup worker·강제 thread interleaving은 미실증이며 이후 사용자 수동 삭제 수용과 구분한다.

수동 삭제는 과거 remoteStable=false/footer 부재만으로 거절하지 않는다. ValidateReceipt의 과거 영수증 일관성은 유지하고 현재 PC raw/receipt 출처 hash·sameDevice·remote size/SHA 전체 일치·앱 종료를 실제 확인한다. 목록 metadata와 receipt-only는 이 검증을 대체하지 않는다.

metadata는 LastUsedUtc.txt, 공유 claim은 .Retention.lock, 루트 최근 요약은 RetentionAudit.txt다. 단조 max로 갱신하고 사고 잔존 lock은 보호한다. worker는 보호 항목도 lazy 열거해 기아를 피하고 최대32회는 적격 삭제 시도 상한이다. 남은 것은 다음 앱 시작 대상이다. ZIP은 raw+receipt 쌍만 포함하고 PC LastUsed metadata·PC/ZIP 원본은 자동 삭제하지 않는다.

메인은 실제 Unity의 새 창·Refresh·SM_N971N 선택·“PC 보관 후보/미검증” 표기와 빨간 적격 삭제 버튼 표시를 확인했다. 이 확인 때 원문 수집·삭제0, 기기 앱 재실행/자동 정리0이다. 실제 삭제·30일 기기 자동 정리 수용으로 확대하지 않는다.

## 사용자 확인 절차와 남은 범위

`Hexiege/Logs/Collect Device Logs and Create ZIP`에서 기기를 선택해 목록을 확인한다. 삭제할 세션은 앱을 종료한 상태에서 PC 사본을 준비하고, 대상 선택 뒤 적격 목록/용량을1회 확인한다. 부적격은 사유와 함께 건너뛰며 취소하면 삭제하지 않는다. footer 없는 과거 세션도 현재 byte/앱 종료 조건을 만족하면 적격일 수 있다. 원문256MiB 상한은 그대로이며 PC/ZIP 보관은 유지된다.

이번 개발 검증에서는 실제 기기 삭제를 실행하지 않았다. 실제 삭제 확인이 필요하면 그 실행 범위를 별도로 정하고 기존 원본·PC 사본 검증을 보존한다. 새 앱의 시작worker 실행/30일 대상·lock 잔존 보호는 기기 배포/실행 전 미검증이다. offline PC 사용 기록은 다음 명시적 기기 목록 동기화 전 기기 expiry 보호를 보장하지 않는다.

## 변경 파일과 적용 시점

제품은 다음5개 C#과 신규 helper meta1개, 총6경로다.

- `Assets/Editor/Tools/AndroidDevelopmentLogArchive.cs`
- `Assets/Editor/Tools/LogArchiveZip.cs`
- `Assets/_Project/Scripts/Infrastructure/Debug/RuntimeLogger.cs`
- `Assets/_Project/Scripts/Infrastructure/Debug/LogSessionOwner.cs`
- `Assets/_Project/Scripts/Infrastructure/Debug/DevelopmentLogRetention.cs`
- `Assets/_Project/Scripts/Infrastructure/Debug/DevelopmentLogRetention.cs.meta`

문서는 `LogRules.md`와 이 Task의 Research.md/Plan.md다. Unity가 생성한 Task 문서/폴더 meta는 제품 코드와 별도로 기존 GUID를 유지하며 수동 생성·재작성하지 않는다. private fixture/결과/영수증은 .utmp 검증 자료이며 제품 경로에 포함하지 않는다. 현재 자동 생성 meta 목록은 `2026-11-26.meta`, `01_34_device-log-retention.meta`, `Research.md.meta`, `Plan.md.meta`다.

수동 목록·PC복사·선택 삭제의 Editor 도구 변경은 새 앱 빌드 없이 사용할 수 있다. 앱 시작의30일 미사용 자동 정리는 해당 helper가 포함된 새 Development Android 빌드부터 적용된다. 아직 새 앱 실행/자동 기기 정리는 검증하지 않았다.

## 사용자 수동 삭제 수용·main 반영 승인 — 2026-11-26

사용자가 “삭제 잘 되는 것 같아”라고 수동 삭제의 정상 동작을 확인하고 main 병합·push를 요청했다. 문서 반영은 “기존 문서에 반영”을 선택했으므로 이 Task에서 기록한다. 삭제 개수·대상, PC/ZIP 실물 검증,30일 자동 정리의 기기 실행, 목록 속도 전후 수치는 보고되지 않아 확정하지 않는다. 수동 사용자의 전체 정상 수용과 개발자의 실제 삭제 실행0을 구분한다.

현재 HEAD25a04c2c/최신 origin/main14dc0c2a 기준으로 메인이 제품5cs+helper meta의 commit·main 병합·push를 준비한다. 이 문장은 승인/진행 상태이며 아직 병합·원격 반영 완료가 아니다. 기존 dirty와 원본·검증 한계를 보존하고 최종 Git 결과 수신 뒤 현행 상태만 갱신한다. 추가 메모리·제품·Git 변경은 문서 담당이 수행하지 않는다.
