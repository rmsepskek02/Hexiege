# 로그 보관용 ZIP 생성 도구 — Research

테스트할 때마다 생기는 원시 로그 파일(에디터 로그·실기기 로그)을 사용자가 손으로 골라 압축하고 이름을 붙이는 수고를 없애려는 작업이다. 원래는 AI가 압축·이름 붙이기·업로드까지 전부 자동으로 하려 했지만, 클라우드에서 도는 AI 세션은 사용자 PC의 파일을 볼 수 없고 그 원본 로그는 Git에도 올라가지 않는다(일부러 제외해 두었다). 그래서 **Unity 에디터 메뉴 하나로 "아직 보관하지 않은 로그만 골라 ZIP + 설명서(manifest)를 만드는 일"까지를 PC에서 자동으로 하고, 업로드는 사용자가 비공개 로그 저장소에 직접 올리는** 방식으로 정했다. 올린 뒤에는 메인 세션이 그 파일을 내려받아 설명서와 대조해 검증하고 분석한다.

이 문서는 그 도구를 만들기 전에 현재 규칙·파일 배치·기존 도구가 어떤 상태인지 조사한 기록이다. 결정 사항과 구현 방법은 [Plan](Plan.md)에 있다.

> **근거 표기:** [확인] = 이 문서 작성 중 이 체크아웃(HEAD `639ac1aa`)에서 직접 읽은 것. [전달] = 메인 세션이 전한 배경·사용자 결정(2026-10-20)으로, 이 문서에서 재확인하지 못한 것. [추정] = 근거가 간접적인 판단.

---

## 1. 배경과 사용자 결정 (2026-10-20)

| 항목 | 내용 | 근거 |
|---|---|---|
| 자동 업로드를 포기한 이유 1 | 클라우드 세션은 사용자 PC 파일에 접근할 수 없다 | [전달] |
| 자동 업로드를 포기한 이유 2 | 원본 RuntimeLog 본문은 Git에 없다. `.gitignore` 812~817행이 에디터 일일 로그·날짜 폴더 아래 `RuntimeLog.txt`·`RuntimeLog_device.txt`·`RuntimeLog_device_2.txt`·`RuntimeLog_host.txt`·`RuntimeLog_client.txt`를 제외한다(메인이 말한 814행은 `RuntimeLog_device.txt` 줄이다) | [확인] `.gitignore` 811~817 |
| 소스 저장소 경유 비권장 | 소스 저장소 `rmsepskek02/Hexiege`는 public, 로그 저장소 `Hexiege-Logs`는 private. 소스 저장소에 ZIP을 커밋했다가 지우는 방식은 Git 이력에 영구히 공개로 남는다(로그에 기기명·시리얼·MatchId·닉네임 포함) | [전달] 공개 여부 · [확인] LogRules 「원시 테스트 로그 공유·보관」이 소스 저장소 공개 Release 전송을 이미 금지 |
| 채택 | Unity 에디터 메뉴로 ZIP + manifest 생성 → 사용자가 `Hexiege-Logs`의 월별 Release(`로그 보관 YYYY-MM`)에 직접 업로드 → 메인 세션이 Release에서 내려받아 manifest 대조 검증·분석 | [전달] |
| 저장 경로 | `Assets/_Project/Docs/_Logs/_zip/YYYY-MM-DD/`. 같은 날짜 폴더가 있으면 그 안에 추가(파일명에 날짜·시각·회차가 있어 구분됨) | [전달] |
| Git 제외 | `_Logs/_zip/` 하위 전체 + `_zip.meta`만 제외(`_Logs/` 전체 제외가 아님) | [전달] 사용자 승인 2026-10-20 |

## 2. 현재 규칙 — LogRules 「원시 테스트 로그 공유·보관」 [확인]

`Assets/_Project/Docs/LogRules.md` §1.10 아래 「원시 테스트 로그 공유·보관」 절(561~583행)이 로그 보관 정책의 단일 원본이다. 이번 도구와 맞물리는 조항은 아래와 같다.

| 조항 (요지) | 도구에 주는 요구 |
|---|---|
| 보관 위치는 private `rmsepskek02/Hexiege-Logs` 월별 Release ZIP. 소스 저장소 공개 Release 전송 금지 | 도구는 소스 저장소에 아무것도 올리지 않는다. 업로드 대상 표기는 `Hexiege-Logs`로 고정 |
| 매 업로드 직전 정확한 owner/repo와 `private=true` 확인, 실패·불명확·public이면 중단 | 업로드하는 사람(이번 방식에서는 사용자)에게 그대로 적용된다. 도구는 업로드하지 않는다 |
| 인증값·시크릿을 문서·로그·manifest에 저장 금지 | 도구는 인증을 다루지 않는다. manifest에는 안정적인 Release 페이지 URL만 적는다 |
| 월별 Release 제목 `로그 보관 YYYY-MM`, 새 ZIP `YYYY-MM-DD_HH_MM_작업명_회차.zip` | 파일명 형식 그대로 |
| 플랫폼 파일명 정규화(한글 제거 시 ASCII 대응·영수증 기록, bytes·SHA256 불변) | 작업명을 처음부터 영문 slug로 받으면 GitHub가 이름을 바꿀 일이 없다 [추정 — 영문·숫자·하이픈만으로 된 이름은 2026-10-12 사례처럼 한글이 제거되는 경우에 해당하지 않음] |
| 내부 경로는 저장소 상대 경로 유지. 에디터 일일 로그는 캡처 시점 snapshot임을 기록, 원본의 기록 시점·크기·hash 구분. 포맷/내용 재작성 금지 | ZIP entry 이름 = 저장소 상대 경로. 에디터 로그는 한 번 읽은 바이트로 hash와 ZIP 기록을 모두 해야 snapshot이 성립 |
| 새 회차는 기존 보관 manifest의 경로·hash와 대조해 이미 보관한 동일 로그를 반복 포함하지 않는다. 동일 경로 새 내용은 snapshot 시각/hash로 구별 | 기존 manifest의 (경로, SHA256) 집합과 대조 |
| manifest 필수 항목: 선택 원본 경로, 파일 수·크기·개별 SHA256, ZIP 크기·SHA256, Release/다운로드 위치, 세션·역할(확인된 경우), 빌드 상태와 미확정 경계 | manifest 필드 목록 |
| 업로드 응답만으로 보관 성공 판정 금지. 실제 다운로드 후 전체 entry 대조 | 메인 세션 검증 단계. 도구는 「업로드·검증 전」 상태만 기록 |
| 로컬·remote archive 자동/주기 삭제 금지. 로컬 정리는 대상별 사용자 승인 | 도구는 원본 로그도 만든 ZIP도 지우지 않는다 |
| `_Logs/` 전체 또는 전체 txt ignore 금지. 로그 문서/meta를 광범위한 제외 패턴에 넣지 않는다. folder metadata는 소스 Git 유지 | 이번 `_zip/` + `_zip.meta` 제외는 이 조항의 **좁은 예외**로 문서에 명시해야 한다(아래 §6-3) |

같은 문서 §1.12(669~675행)의 실기기 캡처 규칙도 관련된다: 같은 이름 파일이 있으면 `RuntimeLog_device_2.txt`처럼 번호가 붙는다(덮어쓰지 않음). [확인]

## 3. 현재 파일 배치 [확인]

| 대상 | 경로 | 비고 |
|---|---|---|
| 에디터 일일 로그 | `Assets/_Project/Docs/_Logs/_editor/{yyyy-MM-dd}/RuntimeLog.txt` | 일 단위 이어쓰기(LogRules §1.10 「파일 누적 관리 방침」). 플레이 중에는 계속 append된다 |
| 실기기 캡처 | `Assets/_Project/Docs/_Logs/{yyyy-MM-dd}/{HH_mm}_logcat/RuntimeLog_device.txt` (중복 시 `_2`) | `Assets/Editor/Tools/LogcatCapture.cs`의 `Hexiege/Logs/Save Device Logs`(225행) |
| 임시 진단 세션 | `Assets/_Project/Docs/_Logs/{yyyy-MM-dd}/HH_MM_[작업명]/RuntimeLog.txt` | LogRules §1.11 `BeginSession` |
| 보관본 출력 위치 | `Assets/_Project/Docs/_Logs/_zip/` | **현재 없음**(이 체크아웃에 폴더 부재) |
| 이 체크아웃의 raw 본문 | 0개 (`find … -name "RuntimeLog*.txt"` 0건, `git ls-files` 0건) | raw `.txt.meta`는 8개 추적 중. 클라우드 체크아웃이므로 도구의 실제 대상 개수는 여기서 셀 수 없다 |

HEAD `639ac1aa`의 `.gitignore`로는 `_Logs/_zip/x.zip`과 `_Logs/_zip.meta`가 **무시되지 않는다**(`git check-ignore` 종료 코드 1). 즉 `.gitignore` 수정 전에 도구를 실행하면 ZIP과 manifest가 Git 추적 후보가 된다. `.gitignore` 수정은 game-programmer가 병행하며 이 문서 담당은 수정하지 않았다. *(2026-10-20 같은 회차 재확인: 작업 트리의 `.gitignore`에 미커밋 변경으로 822행 `/Assets/_Project/Docs/_Logs/_zip/`, 823행 `/Assets/_Project/Docs/_Logs/_zip.meta`가 추가되어 두 경로가 무시된다 — `git check-ignore -v`로 확인. 커밋 여부는 이 문서에서 판정하지 않는다.)* [확인 + 전달]

## 4. 기존 도구 [확인]

### 4-1. `Assets/Editor/Tools/LogcatCapture.cs`
- 메뉴 `Hexiege/Logs/Clear Device Buffer (Deletes Logs)`(186행, priority 1), `Hexiege/Logs/Save Device Logs`(225행, 2), `Hexiege/Logs/Clean Old Editor Logs (Deletes Assets)`(387행, 13).
- 저장 후 `EditorUtility.RevealInFinder`로 폴더를 연다(346행). 새 도구의 「완료 후 폴더 열기」와 같은 수단이 이미 프로젝트에 있다.
- WORKFLOW [5-2] 「에디터 스크립트 최소 유지 기준」 표에 `Tools/LogcatCapture.cs` 행이 있다.

### 4-2. `Tools/archive_runtime_logs.py` (2026-10-12 기존 로그 일회성 보관용)
- 저장소 루트 `Tools/` 아래 Python. `Temp/log-archive/YYYY-MM-DD_기존로그.zip` 형식만 허용(142행).
- 대상 basename 5종: `RuntimeLog.txt`, `RuntimeLog_device.txt`, `RuntimeLog_device_2.txt`, `RuntimeLog_host.txt`, `RuntimeLog_client.txt`(15~17행). 에디터 로그와 날짜 폴더 아래 임의 깊이를 허용(27~46행).
- manifest 스키마 `runtime-log-local-archive-v1`: `entries[]`의 각 항목이 `path`·`bytes`·`sha256`을 가진다(122~124, 223~232행). `session_role: NOT_INSPECTED`, `build_status: NOT_INFERRED`.
- 이 도구로 만든 `2026-10-12_기존로그.zip`(remote `2026-10-12_legacy-logs.zip`)과 manifest(remote `2026-10-12_legacy-logs.manifest.json`)는 `Hexiege-Logs` Release tag `logs-2026-10`에 업로드·전체 검증 완료됐다(raw 169개). 안정 URL `https://github.com/rmsepskek02/Hexiege-Logs/releases/tag/logs-2026-10`. [확인 — `_Tasks/2026-10-12/01_41_log-rules-history-archive/Plan.md` 45·50행의 기록. Release 자체는 이 문서에서 조회하지 않았다]

## 5. 영향 범위

| 영역 | 영향 |
|---|---|
| 게임 런타임 코드 | 없음. 에디터 전용(`Assets/Editor/`) 도구이며 빌드에 포함되지 않는다 |
| 기존 로그 원본 | 읽기만 한다. 수정·이동·삭제 없음 |
| 기존 메뉴 | `Hexiege/Logs/` 아래 메뉴 1개 추가. 기존 3개 메뉴 동작은 바뀌지 않는다 |
| Git | `_zip/` 산출물은 `.gitignore`로 제외(병행 작업). 도구는 Git 명령을 실행하지 않는다 |
| Unity 임포트 | `Assets/` 아래에 ZIP·JSON이 생기므로 Unity가 임포트하고 `.meta`를 만든다 [추정 — Unity 일반 동작]. `_zip/` 하위 전체 + `_zip.meta`를 제외하는 이유가 이것이다 |
| 상시 문서 | WORKFLOW [5-2] 표에 행 1개, LogRules 「원시 테스트 로그 공유·보관」에 사용 절차·ignore 예외 |
| GameSystemRules | 해당 규칙 없음. 게임 규칙이 아니라 개발 도구·로그 보관 절차다(인덱스 `GameSystemRules.md` 확인) |

## 6. 조사 중 발견한 확인 필요 사항

### 6-1. 🔴 기존 legacy 보관분과의 중복 판정
사양은 "기존 `_zip` manifest에 (경로, SHA256)으로 기록되지 않은 것"을 대상으로 한다. 그런데 2026-10-12 legacy 보관본의 manifest는 `_zip/` 안에 없다(그때 출력 위치는 `Temp/log-archive/`). 따라서 도구를 처음 실행하면 이미 보관된 169개 가운데 사용자 PC에 남아 있는 파일이 **다시 포함된다** [추정 — 사용자 PC에 원본이 남아 있는지는 확인 불가. 그 Task 기록상 로컬 raw 169개는 삭제하지 않았다]. LogRules의 "이미 보관한 동일 로그를 반복 포함하지 않는다" 조항과 어긋난다. 대응 선택지(사용자 결정 필요): (A) 사용자가 Release의 legacy manifest를 `_zip/` 아래에 내려받아 두고 도구가 `_zip/` 하위의 모든 `*.manifest.json`을 읽는다(legacy 스키마의 `entries[].path`/`sha256`도 읽을 수 있어야 함) / (B) 첫 실행은 중복을 허용한다. → Plan U-1. *(2026-10-20 사용자 결정: 2026-10-20 이후 날짜 폴더만 대상으로 하는 날짜 하한 — Plan §4-1. 위 (A)와는 다른 방식이다.)*

### 6-2. 대상 파일 종류가 legacy 도구보다 좁다
사양 대상은 `_Logs/YYYY-MM-DD/*/RuntimeLog_device.txt`와 `_Logs/_editor/YYYY-MM-DD/RuntimeLog.txt` 두 종류다. 현행 규칙상 생길 수 있는 `RuntimeLog_device_2.txt`(§1.12 중복 번호)와 임시 진단 세션의 `_Logs/YYYY-MM-DD/HH_MM_작업명/RuntimeLog.txt`(§1.11)는 대상이 아니다. legacy 도구는 이것들을 포함했다. 사양을 그대로 따르되 빠지는 종류가 있음을 도구 안내와 문서에 적을지, 대상에 넣을지는 사용자 확인 사항이다. → Plan U-2. *(2026-10-20 사용자 결정: 포함 — 기기 로그 `RuntimeLog_device*.txt` 전부, 진단 세션 `RuntimeLog.txt`, 에디터 `RuntimeLog.txt`. Plan §4-1.)*

### 6-3. `_zip.meta` 제외와 LogRules의 "folder metadata 유지" 문장
LogRules 「원시 테스트 로그 공유·보관」 마지막 항목은 "실제 코드/asset/doc/folder metadata는 소스 Git에 유지"하고 "로그 문서/meta를 광범위한 제외 패턴에 함께 넣지" 말라고 한다. `_zip.meta`는 folder metadata이므로, 사용자 승인된 예외로 같은 절에 명시하지 않으면 두 문장이 충돌한다. → Plan 상시 문서 문안에 예외 문장 포함.

### 6-4. 새 달의 Release가 아직 없을 때
도구가 여는 Release URL은 `…/releases/tag/logs-YYYY-MM`이다. 현재 확인된 것은 `logs-2026-10` 하나다. 다음 달 첫 실행 때는 그 tag의 Release가 없어 페이지가 열리지 않을 수 있다 [추정]. 사용자가 제목 `로그 보관 YYYY-MM`, tag `logs-YYYY-MM`으로 Release를 새로 만들어야 한다는 안내가 필요하다. tag 이름 `logs-YYYY-MM`은 LogRules에 아직 적혀 있지 않다(제목만 있음). → Plan 상시 문서 문안에 포함.

### 6-5. `_zip/`이 로컬 전용이라는 점
`_zip/`은 Git에서 제외되므로 중복 판정의 근거(이전 manifest)가 그 PC에만 있다. 다른 PC에서 실행하거나 `_zip/`을 지우면 이미 보관한 로그가 다시 포함된다 [추정 — 사양의 판정 방식에서 바로 따라나오는 결과]. 지우지 않는 것이 기본이고(LogRules 자동 삭제 금지), 다시 포함되더라도 내용이 덮어써지지는 않으므로 데이터 손실 위험은 아니다. → Plan 위험 표.

### 6-6. LogRules §1.10 「파일 누적 관리 방침」 표의 `.gitignore` 행 (범위 밖 — 제안만)
547행 "raw만 좁은 경로로 분리하는 정책"은 `_zip/` 산출물 제외가 추가되면 "raw만"이 문자 그대로는 맞지 않게 된다. 이번 지시 범위(「원시 테스트 로그 공유·보관」 절)가 아니어서 고치지 않았다. 같은 문서 2장 「폴더 구조」 트리(739~752행)에도 `_zip/`이 없다. 둘 다 사용자 승인 시 별도로 고칠 제안 항목이다.
