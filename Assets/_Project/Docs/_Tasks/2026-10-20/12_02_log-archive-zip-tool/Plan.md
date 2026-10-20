# 로그 보관용 ZIP 생성 도구 — Plan

Unity 에디터 메뉴 **`Hexiege > Logs > Create Log Archive ZIP`** 하나를 누르면, 아직 비공개 로그 저장소에 보관하지 않은 테스트 로그만 골라 ZIP 파일과 그 내용 설명서(manifest)를 만들어 주는 도구를 추가한다. 사용자는 작업 이름만 영어로 적으면 되고, 파일 이름·회차 번호·중복 제외·지문(SHA256) 계산은 도구가 한다. 업로드는 사용자가 비공개 저장소 `Hexiege-Logs`의 월별 Release 페이지에 직접 올리고(도구가 그 페이지를 열어 준다), 메인 세션이 올라간 파일을 내려받아 설명서와 맞는지 검증한 뒤 분석한다. 원본 로그는 읽기만 하고 고치거나 지우지 않는다.

> **승인 상태 (2026-10-20):** 사용자 승인 "task 문서 작성하고 바로 진행해"로 **이 Plan은 승인됨** 상태다. 구현은 game-programmer가 병행한다(코드·`.gitignore`). 아래 U-1·U-2는 승인 범위 밖에서 드러난 확인 사항이었고 *(2026-10-20 같은 날 사용자 결정으로 닫힘 — §4-1. 대상 선택은 §2 원문이 아니라 §4-1이 현행 사양이다)*.
>
> **현행 진행 상태 (2026-10-20):** Task 문서 작성 · 상시 문서 반영(§6) 완료. 도구 구현·컴파일·메뉴 실행·업로드·다운로드 검증은 **모두 확인 전**이다(§8 실행 기록).

## 최상단 — 기존 로직 제거

**없음.** 새 파일 추가와 `.gitignore`에 좁은 제외 줄 추가뿐이며, 기존 메뉴·로그 기록 코드·legacy 도구(`Tools/archive_runtime_logs.py`)는 바꾸지 않는다(WORKFLOW [4] 「기존 로직 제거 규칙」 대상 아님).

## 1. 근거 규칙

GameSystemRules 인덱스(`Assets/_Project/Docs/GameSystemRules.md`)를 확인했으며 게임 규칙에 해당 항목은 없다. 이 작업의 근거는 아래 두 문서다.

| 항목 | 근거 |
|---|---|
| 보관 위치·Release 제목·ZIP 이름·manifest 항목·중복 금지·내부 경로·snapshot·자동 삭제 금지·업로드 후 다운로드 검증 | [LogRules](../../../LogRules.md) §1.10 「원시 테스트 로그 공유·보관」 |
| 파일명 정규화(ASCII)·bytes/SHA256 불변 | 같은 절 「플랫폼 파일명 정규화 예외」 항목 |
| 대상 로그 경로 | LogRules §1.10 「파일 누적 관리 방침」(에디터 일일 로그) · §1.12 「캡처 도구 사용 절차」(실기기) |
| 에디터 도구로 유지·표에 등록 | [WORKFLOW](../../../WORKFLOW.md) [5-2] 「에디터 스크립트 최소 유지 기준」 |
| 원본·archive 삭제 금지, Git 쓰기 금지 | LogRules 「자동 삭제하지 않는다」 · CLAUDE.md 규칙 5 |

## 2. 도구 사양 (2026-10-20 승인 원문 기준)

| 항목 | 사양 | 근거 |
|---|---|---|
| 파일 | `Assets/Editor/Tools/LogArchiveZip.cs` | WORKFLOW [5-2] |
| 메뉴 | `Hexiege/Logs/Create Log Archive ZIP` | 기존 `Hexiege/Logs/` 메뉴군(`LogcatCapture.cs`)과 같은 자리 |
| 입력 | 작업명(영문 slug) 입력 창. 마지막 입력값을 EditorPrefs에 기억 | 정규화 예외 — 처음부터 ASCII면 remote 이름 대응이 필요 없다 |
| 대상 | `_Logs/YYYY-MM-DD/*/RuntimeLog_device.txt` 와 `_Logs/_editor/YYYY-MM-DD/RuntimeLog.txt` 중, 기존 `_zip/` manifest에 (경로, SHA256)으로 기록되지 않은 것 *(2026-10-20 사용자 결정으로 대상 종류 확대·날짜 하한 추가 — 현행 사양은 §4-1)* | 「새 회차는 기존 보관 manifest의 경로·hash와 대조」 |
| 에디터 로그 | 기록 중일 수 있으므로 `FileShare.ReadWrite`로 열어 snapshot. **한 번 읽은 바이트로 SHA256 계산과 ZIP 기록을 모두 한다**(따로 두 번 읽으면 그 사이 append로 hash와 ZIP 내용이 달라진다). 내용이 바뀌었으면 새로 포함 | 「선택한 Editor daily는 캡처 시점의 snapshot」·「동일 경로의 새 내용은 snapshot 시각/hash로 구별」 |
| 저장 위치 | `Assets/_Project/Docs/_Logs/_zip/YYYY-MM-DD/`. 날짜 폴더가 있으면 그 안에 추가 | 사용자 결정 |
| 파일명 | `YYYY-MM-DD_HH_MM_<작업명>_<NN>.zip` + 같은 이름의 `.manifest.json`. `NN`은 같은 날짜+작업명 기준 01부터 증가 | 「`YYYY-MM-DD_HH_MM_작업명_회차.zip`」 |
| ZIP 내부 | 저장소 상대 경로 유지(`Assets/_Project/Docs/_Logs/...`). 내용 재작성 없음 | 「내부 경로는 원래 repository-relative 경로를 보존」 |
| manifest 내용 | 원본 경로·파일 수·각 크기·개별 SHA256 / ZIP 크기·SHA256 / 생성 시각 / snapshot 시각 / 빌드 상태 `"not inferred"` / 업로드 대상 Release 이름(`로그 보관 YYYY-MM`)·URL / "업로드는 사용자 수행" 표기 | 「manifest에는 선택 원본 경로, 파일 수·크기·개별 SHA256, ZIP 크기·SHA256, Release/다운로드 위치, …빌드 상태와 미확정 경계」 |
| 완료 후 | 저장 폴더 열기 + Release 페이지 `https://github.com/rmsepskek02/Hexiege-Logs/releases/tag/logs-YYYY-MM` 열기 | 사용자 결정. tag 형식은 기존 `logs-2026-10`(Research §4-2) |
| 대상 없음·오류 | 안내 창을 띄우고 끝낸다. 이때 반쪽짜리 ZIP·manifest를 성공으로 남기지 않는다 | — |
| 금지 | 원본 로그 수정·이동·삭제, 업로드, Git 명령, 만든 ZIP의 자동 삭제 | LogRules 「자동 삭제하지 않는다」 · CLAUDE.md 규칙 5 |

### 2-1. manifest 표기 경계
- Release 이름·URL은 **업로드 예정 위치**이며 업로드·검증 결과가 아니다. 도구가 만든 manifest만으로 "보관 확인"을 기록하지 않는다(LogRules 「업로드 완료 응답만으로 보관 성공을 판정하지 않습니다」).
- 세션·역할은 LogRules가 "확인된 경우"에만 요구한다. 도구는 로그 내용을 해석하지 않으므로 [제안] 빌드 상태와 같이 `"not inferred"`로 둔다(legacy manifest도 `session_role: NOT_INSPECTED`). 정확한 key 이름은 구현 보고로 확정해 §8에 적는다.

## 3. 수정·추가 파일

| 파일 | 담당 | 내용 |
|---|---|---|
| `Assets/Editor/Tools/LogArchiveZip.cs` (+ Unity가 만드는 `.meta`) | game-programmer | §2 도구 |
| `.gitignore` | game-programmer | `_Logs/_zip/` 하위 전체 + `_Logs/_zip.meta`만 제외하는 좁은 줄 추가(2026-10-20 사용자 승인). `_Logs/` 전체 제외 금지 |
| `Assets/_Project/Docs/WORKFLOW.md` | document-manager | §6-1 |
| `Assets/_Project/Docs/LogRules.md` | document-manager | §6-2 |
| 이 Task의 Research.md / Plan.md | document-manager | 신규 |

## 4. 확인 사항 (사용자 답 필요)

| ID | 질문 | 영향 | 현재 처리 |
|---|---|---|---|
| U-1 | 2026-10-12 legacy 보관본 manifest가 `_zip/`에 없어 첫 실행 때 이미 보관한 로그가 다시 포함될 수 있다(Research §6-1). (A) 사용자가 legacy manifest를 `_zip/` 아래에 받아 두고 도구가 `_zip/` 하위 모든 `*.manifest.json`(legacy 스키마 `entries[].path`/`sha256` 포함)을 읽는다 / (B) 첫 실행 중복을 허용한다 | LogRules 「반복 포함하지 않습니다」 준수 여부 | 결정 전. 사양 원문(`_zip` manifest만 대조)대로 |
| U-2 | `RuntimeLog_device_2.txt`(§1.12 중복 번호)와 임시 진단 세션 `RuntimeLog.txt`(§1.11)를 대상에 넣을지(Research §6-2) | 그 로그들은 도구로 보관되지 않는다 | 결정 전. 사양 원문(두 종류)대로. 상시 문서에 "포함되지 않음"을 적었다 |

> 위 표는 질문 당시 원문이다. 「현재 처리」 칸의 "결정 전"은 아래 §4-1 결정으로 닫혔다(2026-10-20).

### 4-1. 사용자 결정 (2026-10-20, 메인 세션 전달)

| ID | 결정 | 위 선택지와의 대응 | 현행 사양 |
|---|---|---|---|
| U-1 | "A — 2026-10-20 이후 날짜 폴더 로그만 대상. 기존 2026-10-12 보관분은 Release에 이미 있음" | 🔴 **위 표의 (A)(legacy manifest를 `_zip/`에 두고 읽기)와 내용이 다르다.** 결정 내용은 **날짜 하한**이며 legacy manifest를 읽는 방식이 아니다. 이 문서는 결정 **내용**을 현행 사양으로 기록한다 | 날짜 폴더 이름(`YYYY-MM-DD`)이 **「시작 날짜」 이후(당일 포함)**인 로그만 고른다. 시작 날짜는 도구 옵션이며 기본값 2026-10-20(아래 「공백 처리」). 그 안에서는 종전대로 `_zip/` manifest의 (경로, SHA256)과 대조해 이미 넣은 것을 뺀다. legacy manifest는 읽지 않는다 |
| U-2 | "포함 — 기기 로그 `RuntimeLog_device*.txt` 전부, 진단 세션 폴더 `HH_MM_작업명/RuntimeLog.txt`, 에디터 `RuntimeLog.txt`" | 위 표 U-2의 "넣는다" | 대상 = `_Logs/YYYY-MM-DD/*/RuntimeLog_device*.txt`(`_2` 등 번호 붙은 파일 포함) + `_Logs/YYYY-MM-DD/HH_MM_작업명/RuntimeLog.txt` + `_Logs/_editor/YYYY-MM-DD/RuntimeLog.txt` |

**결정 문구만으로 정해지지 않아 확인이 필요한 점 (구현 보고로 확정해 §8에 적는다):**
- **당일 포함 — 확정 (2026-10-20 사용자 결정).** 시작 날짜와 같은 날짜 폴더도 대상이다. *(처음에는 [해석]으로 적었던 항목이다.)*
- **에디터 일일 로그에도 적용 — 확정 (2026-10-20 사용자 결정).** `_editor/YYYY-MM-DD/`도 시작 날짜보다 앞서면 고르지 않는다. *(처음에는 [해석]으로 적었던 항목이다.)*
- **공백 처리 — (나) 채택 (2026-10-20 사용자 결정).** 처음에는 "2026-10-13~2026-10-19 날짜 폴더 로그는 이 도구로 보관되지 않는다"를 [추정] 공백으로 적었다. 결정: 도구에 **「시작 날짜」 옵션**(기본값 2026-10-20, 당일 포함, 에디터 일일 로그에도 적용)을 둔다. 사용자가 **한 번** 시작 날짜를 2026-10-13으로 두고 실행해 10-13~10-19 공백을 묶어 보관한다.
  - 그 회차에서 **기존 2026-10-12 보관분과 겹치는지는 도구가 확인하지 않는다.** 업로드 뒤 메인 세션이 기존 manifest(`2026-10-12_legacy-logs.manifest.json`)와 대조한다.
  - 시작 날짜를 입력받는 방식(작업명과 같은 창인지)과 마지막 값을 기억하는지는 결정에 없다 — 구현 보고로 확정해 §8에 적는다. 기본값이 2026-10-20으로 고정이라는 점은 결정이다.
- 진단 세션 폴더와 logcat 폴더는 둘 다 `_Logs/YYYY-MM-DD/` 바로 아래 하위 폴더라 경로 깊이로는 구별되지 않는다. 구별은 파일 이름(`RuntimeLog.txt` vs `RuntimeLog_device*.txt`)으로 한다. 같은 날짜 폴더 아래의 `Log.md`·`.meta`·`CrossAuditResult.txt` 등은 대상이 아니다(LogRules 「원시 테스트 로그 공유·보관」의 Git 유지 대상과 같은 경계).

## 5. 위험

| 위험 | 대응 |
|---|---|
| `.gitignore` 반영 전에 도구를 실행하면 ZIP·manifest가 Git 추적 후보가 된다(현재 `git check-ignore` 미일치, Research §3) | `.gitignore` 변경이 들어간 뒤 실행. 공개 저장소에 커밋되면 이력에 영구히 남는다 |
| `_zip/`은 로컬 전용이라 다른 PC·폴더 삭제 시 중복 판정 근거가 사라진다 | 지우지 않는 것이 기본(자동 삭제 금지). 다시 포함돼도 덮어쓰기·손실은 없음 |
| 새 달 Release(`logs-YYYY-MM`)가 없으면 열린 페이지가 없음 | 상시 문서에 "그 달 Release가 없으면 제목 `로그 보관 YYYY-MM`, tag `logs-YYYY-MM`으로 만든다" 안내 |
| 기록 중인 에디터 로그를 두 번 읽어 hash와 ZIP 내용이 어긋남 | §2 「에디터 로그」 한 번 읽기 요구 |
| 업로드 대상 착오(public 저장소) | 업로드 직전 private 확인 의무는 업로드하는 사용자에게 그대로 적용(§6-2 문안). 도구는 `Hexiege-Logs` URL만 연다 |

## 6. 상시 문서 반영 문안 (2026-10-20 이번 실행에서 반영함)

### 6-1. WORKFLOW [5-2] 「에디터 스크립트 최소 유지 기준」
- 표의 `Tools/LogcatCapture.cs` 행 아래에 행 추가:
  `| \`Tools/LogArchiveZip.cs\` | 로그 ZIP 보관본 생성(업로드는 사용자). [LogRules](LogRules.md) §1.10 「원시 테스트 로그 공유·보관」 |`
- 표 위 첫 문장의 목적 나열 "로그 저장·네트워크 강제 실패·전투 검사"에 "로그 보관본 생성"을 넣었다. 표에 행만 추가하면 그 문장이 유지 대상을 셋으로 읽히게 하므로 같은 주장을 함께 고친 것이다(지시 범위 내 정합성 수선 — 보고에 명시).

### 6-2. LogRules 「원시 테스트 로그 공유·보관」
기존 항목은 한 글자도 바꾸지 않고, 마지막 항목 뒤·호환 앵커 앞에 「보관본 생성 도구」 블록을 추가했다(원문 교체가 없어 LogHistory 이관 대상 없음). 블록 내용:
1. 사용 절차 6단계(메뉴 실행 → 작업명 → 자동 선택 → 생성 위치·이름 → 사용자 업로드(private 확인·그 달 Release 없으면 생성) → 메인 세션 다운로드 검증).
2. 도구 경계(원본·ZIP 삭제 없음, 업로드·Git 없음, manifest의 Release 위치는 예정 위치).
3. 대상에서 빠지는 것(U-2)과 legacy 보관분 중복 판정 한계(U-1), `_zip/` 로컬 전용 한계.
4. **Git 제외 예외:** `_Logs/_zip/` 하위 전체와 `_Logs/_zip.meta`(2026-10-20 사용자 승인). 위 "folder metadata 유지·광범위 제외 금지" 조항의 좁은 예외이며 `_Logs/` 전체 제외 금지는 그대로라는 문장.
5. 구현·실행 확인 상태는 이 Plan을 가리키는 상태 표기(도구 사용법이 동작 확인을 뜻하지 않음).

### 6-3. 테스트 후 고칠 자리 (상태 도장)
- LogRules 블록 머리의 상태 표기 "구현·실행 확인 전"은 사용자 실행 결과가 나오면 그 줄을 현재 사실로 고친다(공용 메모리 B-5).
- U-1·U-2 답이 나오면 블록 3번 문장을 답에 맞게 고친다. *(2026-10-20 반영함: LogRules 블록의 2번 단계와 「현재 대상에서 빠지는 것」 항목을 §4-1 결정으로 고쳤다.)*

### 6-4. 이번에 고치지 않은 것 (제안)
- LogRules §1.10 「파일 누적 관리 방침」 표 `.gitignore` 행 "raw만 좁은 경로로 분리"와 2장 「폴더 구조」 트리에 `_zip/` 미기재(Research §6-6). 지시 범위 밖이라 사용자 승인 시 별도 수정.
- PROJECT_STATUS / ROADMAP / WORK_HISTORY: WORKFLOW [9]의 "항상" 갱신 대상이지만 구현·테스트 완료 후 [7] 확인 단계에서 다룬다.

## 7. 검증 방법

1. 구현 보고 후: 메뉴 경로·파일명 형식·ZIP 내부 경로·manifest 항목이 §2와 일치하는지 코드 대조(정적 대조는 실행 PASS가 아니다).
2. 사용자 실행: 대상 있음(ZIP·manifest 생성, 폴더·Release 페이지 열림) / 바로 다시 실행(에디터 로그가 그대로면 "대상 없음") / 플레이 후 다시 실행(에디터 로그만 새 snapshot으로 포함, `NN` 증가).
3. 업로드 후 메인 세션: Release에서 ZIP·manifest를 실제로 내려받아 크기·SHA256, entry 경로·개수·크기·개별 SHA256 대조(LogRules 해당 항목). 통과 전에는 보관 확인으로 기록하지 않는다.
4. `git check-ignore -v`로 `_zip/` 산출물과 `_zip.meta`가 무시되고 다른 `_Logs/` 문서·meta는 무시되지 않는지 확인.

## 8. 실행 기록

| 단계 | 상태 | 근거 |
|---|---|---|
| Research / Plan 작성 | ✅ 2026-10-20 | 이 문서 |
| 상시 문서 반영(§6-1·§6-2) | ✅ 2026-10-20 | `python3 Tools/check_docs.py` 결과는 이 단계 보고에 기록 |
| 도구 구현 | ⬜ 확인 전 (game-programmer 병행 — 이 기록 시점에 `Assets/Editor/Tools/LogArchiveZip.cs` 없음) | — |
| `.gitignore` | 🟡 작업 트리에 반영 관측(미커밋) — 822·823행, `git check-ignore -v`로 `_zip/` 산출물·`_zip.meta` 무시 확인. 구현 보고로 확정 | Research §3 |
| Unity 컴파일 · 메뉴 실행 | ⬜ 확인 전 | — |
| 업로드 · 다운로드 검증 | ⬜ 확인 전 | — |
| U-1 · U-2 사용자 답 | ✅ 2026-10-20 결정 기록 · LogRules 반영 (해석 2건 확정 · 공백은 「시작 날짜」 옵션으로 처리 결정) | §4-1 |
