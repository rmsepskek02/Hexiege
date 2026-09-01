# document-manager 누적 지식

## Topic files (index — an unlinked topic file does not exist)

| Topic | What is in it |
|---|---|
| [doc-conventions.md](doc-conventions.md) | ① Citing rule numbers in the two docs whose numbering restarts per section (`GameSystemRules_Buildings.md` · `GameSystemRules_UI.md`) + the repeated misreading of a two-section line ② Which document carries implementation status — `GameDesignDocument.md` is a **design** document, and the `밸런싱 미확정`(design) vs `✅ 구현 완료`(implementation) split, with the standard replacement block ③ What `check_docs.py` accepts as a rule definition (`**규칙 N. 제목**` bold only, never `## 규칙 N.`), the one-H2-per-document rule that keeps `per_section` off, and the `규칙 11-1` regex case ④ How to restructure a rule document into chapters without touching rule numbers (H3 chapters inside the one H2, reorder freely, never split a rule, moved content takes the receiving document's next number, copy-verify-then-delete), which change-history tables are descending vs ascending, and how to word a deprecation note so the "grep must return 1 hit" check can still pass ⑤ Why replacing a structure leaves self-contradicting sentences behind — grep for the *claim* the old structure made rather than its name, `check_docs.py` is blind to this class, how to word the substitution as an explicit spec, and why a stale count in a tool description gets deleted rather than corrected ⑥ Terminology unification — removing the banned word is only half of "done" (the other half is using an official name *verbatim*, which grep cannot check), auditing an index document for summaries that silently narrow the rule they point at, count copies in index bullets, marking an API that a design contract will replace later **without** pre-applying the rename, the procedure for renaming a state so it stops colliding with a tile state, and which bare enum members are convention rather than leftovers ⑦ Closing a half-rename in the second document's own vocabulary (plain language + one pointer line, never the identifier), why a count copy survives in a second section of the same index document, and the recurring prefix-scan classifications (archetype short names / parallel definition lists / pathfinding prose) ⑧ Pinning "undecided" into a document — the project's existing `⚠️ **… 미정 — 구현 시 확정한다.**` marker form, marker in the owning doc only + a pointer line elsewhere, and never filling the slot while marking it; **`.claude/mistakes.md` is inside the checker's scan set** (and quoting a bad citation as an example trips `[4]` a second time); what `check_docs.py` cannot see (intra-document citations, TDD/GDD as rule sources, a wrong-but-existing rule number); the method and the two-plus-one classes for auditing the index for summaries that narrow — or overstate — the rule they point at; and how to close a deferred item when the handed-over rationale fails measurement ⑨ Actually repairing those index summaries — the one correct repair per class (narrowing / value copy / overstatement / whole block missing) and the reusable pointer wording, why a pre-existing number in a bullet you are widening is not automatically a copy to delete (check the source's own "undecided" list first), the index's **two** reachability paths so a repaired summary can still leave the file-list row unreached, the five regex traps in `check_docs.py` reference parsing that pass or fail silently, and how to fix a wrong pointer inside a change-history row without creating a new entry ⑩ Auditing the index's **other** path — the file-list table: the H2-list 1:1 method that makes it decidable, why H2 granularity also settles what counts as an omission, the rows that are singly-reachable (no section summary of their own), the one-trailing-pointer form for deleting a count from a one-line row, why a number inside a cited section title is a name rather than a copy, and the two cases where you must measure the sibling path before editing (a count the summary repair deliberately kept / two paths that omit the same thing) |

## 이 프로젝트의 문서 관습 (실측으로 확인된 것만)

### 원문 보존 방침
- 기존 서술을 **지우지 않는다.** `~~취소선~~` + `> **[이전 기록 — YYYY-MM-DD 갱신 전]**` 인용 블록으로 남기고 현행을 덧붙인다.
- `LogRules.md` 는 문서 상단에 **개정 이력 표**를 두고 **시간 오름차순**으로 행을 추가한다(맨 아래가 최신). 순서 주의.

### 상시 참조 3문서의 갱신 형태
| 문서 | 형태 |
|---|---|
| `PROJECT_STATUS.md` | 상단 `**최종 수정일:**` 갱신 + 그 아래에 `**구현 완료 (날짜) — 검증 상태:**` 문단을 **맨 앞에 prepend**. `**현재 단계:**` 문단 맨 앞에도 한 줄 추가 |
| `ROADMAP.md` | 상단 문단 prepend + **우선순위 요약 표**의 해당 행을 `🔴 높음 (미착수)` → `✅ 완료 (날짜, 검증상태)` 로 교체. 새로 생긴 잔여 항목은 표에 행 추가 |
| `WORK_HISTORY.md` | **마일스톤 표**(날짜 역순)의 맨 위에 행 1개 추가. 한 행이 매우 길다(수천 자) — 그게 이 파일의 관습이다 |

### 과대 표기 금지 (CLAUDE.md 규칙 10)가 문서에 나타나는 형태
- 완료 항목 옆에 **`⚠️ 과대 표기 금지 — 아직 완료가 아닌 것:`** 목록을 반드시 병기한다.
- "컴파일 통과"와 "실기 검증 PASS"는 **명확히 구분**해서 적는다. 확인받지 않은 커밋은 "통과했다"고 쓰지 않는다.
- 사용자가 범위 밖으로 결정한 항목은 **"미해결 결함이 아니라 범위 밖 확정 항목"** 이라고 명시한다.

### 규칙 번호
- **절대 재배열·신설하지 않는다.** 코드 주석과 과거 Task 문서가 번호를 참조한다.
- 해석을 명문화할 때는 **새 번호 대신 기존 규칙 본문에 문장을 추가**한다.
- `GameSystemRules_UI.md` · `GameSystemRules_Buildings.md` 는 섹션마다 번호가 1부터 반복 → 참조 시 **섹션명(H2) 병기 필수**.
  섹션 목록·번호 범위는 외우지 말 것 — **`python3 Tools/check_docs.py` 의 `[4]` 블록 출력이 권위 소스**다.
  한 줄에 섹션이 둘 나오는 참조의 오독 사례와 판정 절차 → [doc-conventions.md](doc-conventions.md) §1.
- 구현 진행 상태를 어느 문서에 적는가(기획 상태 vs 구현 상태 구분, 표준 대체 문구) → [doc-conventions.md](doc-conventions.md) §2.
- **규칙 제목은 `**규칙 N. 제목**`(굵은 글씨)로 적는다.** `## 규칙 N.` H2 로 적으면 검사기가 규칙이 아니라 **섹션**으로 읽어
  그 문서의 규칙이 **0개로 등록**되고 [3]·[4]·[5] 가 전부 공허하게 통과한다. 규칙 블록을 감싸는 H2 는 **문서당 하나**로 둘 것
  (여러 개로 쪼개도 `per_section` 은 안 켜지지만 [4] 출력이 무의미해진다) → [doc-conventions.md](doc-conventions.md) §3.

## Task Plan.md 사후 갱신 패턴
계획 본문(§1~§10)은 **원문 그대로 두고**, 문서 끝에 `# 11. 구현 결과 (날짜 추가)` 절을 append 한다.
포함 항목: 자연어 요약 / 실적표 / 계획과 달라진 점 / **⚠️ 미완 단서** / **변경 파일 리스트업**(WORKFLOW [12]).

## 도구
- `python3 Tools/check_docs.py` — 리포지토리 루트에서 실행. 읽기 전용. **0건 확인 후 보고.**
- **git 명령 금지**(규칙 5). 변경 파일 목록은 `grep`/코드 실측으로 재구성한다.

## 자주 쓰는 실측 명령 (숫자를 문서에 적기 전 반드시 재확인)
```
grep -rn "Debug\.Log" Assets/_Project/Scripts --include=*.cs | wc -l     # 잔존(주석 포함)
grep -rn "LogEvent\.Unknown" Assets/_Project/Scripts --include=*.cs      # 0건 유지 확인
```
`GameLog` 호출은 **여러 줄에 걸치는 경우가 있어** 한 줄 정규식 grep 으로 세면 누락된다 →
파이썬으로 `GameLog\.(Ops|Dev)\.(Info|Warn|Error)\s*\(` 매치 후 뒤쪽 300자에서 인자를 읽어야 정확하다.

## 인계받은 수치가 실측과 어긋난 사례 (2026-08-18)
- 인계 메모의 **`system` 문자열 분포**(Network 163 등, 합 262)가 실측(Network 273 · Auth 46 · Bootstrap 26 · UI 22 · Cloud 11 · Factory 7 · Audio 4 · HexGrid 1 · Input 1, 합 391)과 **달랐다.**
  → **문서에 옮겨 적지 않고 사용자에게 보고**했다. 인계 수치는 항상 재실측한다.
- 인계 메모의 *"클래스명이 계획과 달라졌다"* 는 항목은 Plan 이 언급한 클래스 13종을 전수 조회해도 **특정할 수 없었다** → 추정하지 않고 그대로 두고 보고.

## 🔴 죽은 경로는 문서가 아니라 **설정 파일**에 숨어 있었다 (2026-08-20 후속)
에이전트 정의 6개(`.claude/agents/*.md`)를 고치고 *"잔존 0건"* 이라고 적었지만, **`.claude/settings.json` 8행 `SessionStart` 훅**이
`cat "d:/Dmain/dev/Portfolio/Hexiege/Hexiege/CLAUDE.md"` 였다 — **매 세션 시작마다 실패해 절대 규칙 자동 로딩이 죽어 있었다.**
> **교훈:** `.md` 만 grep 하고 "정리 끝"이라고 쓰지 말 것. **`.claude/` 전체를 확장자 구분 없이** 훑는다 — `grep -rn "Dmain\|C:.Users\|D:/dev" .claude/`
- **JSON 을 고친 뒤엔 반드시 파싱 검증**: `python3 -c "import json;json.load(open('.claude/settings.json'))"`
- `Edit` 의 `old_string` 끝에 **공백을 흘리면 본문 공백이 지워진다**(실제로 `-c \"import` 의 공백 1칸을 날렸다가 복구). 문자열 경계는 공백까지 확인할 것.
- **의도적으로 남기는 `Dmain` 2건**: ① `project-orchestrator/MEMORY.md` 239행의 **금지 예시** ② 이 파일의 사고 기록. `_Tasks/` 의 `D:/Projects/...` 는 **과거 기록이라 소급 수정하지 않는다.**
- 서브에이전트 위임 규약처럼 **"다른 에이전트에게 넘길 형식"을 지시하는 문장**은 죽은 경로의 **증식원**이다. 경로 표기 규약이 적힌 자리를 따로 찾아볼 것.

## 과거 기록의 수치를 정정할 때 (2026-08-20 확립)
**원문 보존 방침과 「정정 반영」 요구가 충돌한다.** 해법은 **삭제·수정이 아니라 덧붙이기**다.
과거 항목의 문장은 **그대로 두고**, 바로 뒤에 `**[🔴 YYYY-MM-DD 수치 정정 — 원문은 그대로 두고 덧붙인다: …]**` 를 붙인다.
취소선은 **항목 전체가 무효가 됐을 때만** 쓰고, 숫자 하나가 정밀해진 경우에는 쓰지 않는다.
- 실사례: 종료~디스폰 위험 구간을 **`27ms` 단일값**으로 적어 온 3문서(`PROJECT_STATUS`·`ROADMAP`·`WORK_HISTORY`)에
  **6~41ms**(4회 실측 25/27/41/6) 정정을 덧붙였다. 근거 `_Logs/_editor/2026-08-19/RuntimeLog.txt` 255·692·874·1398행 부근.
- **코드 주석의 같은 표기는 고치지 않는다** — 코드는 문서 작업의 범위가 아니다(규칙 6). 남아 있다는 사실만 문서에 적는다.

## 「실기 미검증」을 완료 항목에 붙이는 형태 (2026-08-20)
`✅ 완료` 와 `⚠️ 실기 PASS` 는 **다른 말**이다. 정적 확인만 한 작업은 **`⚠️ 코드 적용 완료 · 실기 미검증`** 으로 적는다.
그리고 **재현 0회인 예방 수정**은 아래 3가지를 반드시 병기한다 —
① 재현 횟수(0회)와 비교 대상(같은 부류가 2회 터진 사례) ② **그럼에도 고친 이유** ③ **고쳐졌음을 적극적으로 보일 수 없다**는 사실과, 대신 실기에서 볼 것(**「멀쩡하던 것이 망가지지 않았는가」**).

## 실측 명령 추가 (2026-08-20)
```
grep -c "\.Subscribe(" Assets/_Project/Scripts/Infrastructure/Network/*.cs   # 이벤트 구독 파일 특정
tr -cd '{' < <파일> | wc -c ; tr -cd '}' < <파일> | wc -c                     # 중괄호 균형(전후 비교)
```
`LogEvent` 멤버 수는 **한 줄 grep 으로 세지 말고** `Application/Interfaces/ILogSink.cs` 의 `public enum LogEvent` 본문을
파이썬으로 파싱해 센다(주석 제거 후 쉼표 분할). 2026-08-20 실측 **37개**.
- **`return` 문 개수 비교는 오탐이 난다** — 주석이 인용한 `` `if (IsServer) return;` `` 문자열까지 잡힌다(`NetworkTileSync` 2→4). 숫자를 그대로 옮기기 전에 해당 줄을 눈으로 확인할 것.

### 이 부류 작업의 안전 절차 (실제로 이렇게 했다)
1. `Read` 로 **전체를 먼저 읽는다**(그래야 무손상 판정의 기준선이 생긴다).
2. `Edit` **순수 삽입** — 기존 줄은 한 줄도 손대지 않는다. `Write` 금지.
3. `wc -l` 로 **작업 전후 행수**를 보고하고, **삽입 구간을 제거하면 원문과 완전 일치**함을 파이썬으로 증명한다:
   `rest = lines[:31] + lines[44:]` → 길이와 양 끝 줄이 원문과 같은지 확인. (`.claude/MEMORY.md` 108 → 121행, 삽입 13행)
- **위치**: 상단부(`## 절대 규칙 참조` 앞). 다만 **기존 절 순서를 재배치하지 않는다**(재배치는 순수 삽입이 아니다).

### git 결과를 문서에 옮길 때 (규칙 5 + 규칙 10 동시 충족)
나는 git 을 실행할 수 없으므로 **커밋 해시·증감 행수는 호출 세션이 전달한 값**이다.
→ 문서에 **`※ 근거 구분(규칙 10):` 한 줄**을 붙여 **내가 직접 실측한 값**(행수·파일 수·링크 상태)과 **전달받은 값**을 갈라 적는다. 섞어 적으면 나중에 재검증할 자리를 못 찾는다.

## 🔴 소실분 복구는 "되돌리기"가 아니라 "유일본 고르기"다 (2026-08-21 실행)

2026-08-17 `-378행` 을 복구할 때, **457행 원문을 통째로 되돌리지 않았다.** 되돌리면 같은 내용이
여러 문서에 갈라져 **다음 정리 때 또 지워진다**(그게 애초의 손실 원인이다).
- **항목별 판정 절차**: 핵심 식별자(클래스명·에셋 경로·상수·메뉴 경로)를 뽑아
  `Assets/_Project/Docs/**` · `.claude/**` · `CLAUDE.md` · 토픽 파일 전부에 grep →
  **어디에도 없으면 유일본만 복구**, 있으면 **복구하지 않고 그 위치를 보고에 적는다.**
- **실적: 457행 중 유일본은 7건뿐이었다.** 나머지는 전부 다른 문서에 살아 있었다.
- **`_Tasks/` · `_Logs/` 는 원본으로 치지 않는다** — 이력 아카이브라 에이전트가 찾아가지 못한다.
  grep 할 때 `grep -v "_Tasks/\|_Logs/"` 로 **살아 있는 출처만** 센다(이 구분이 판정을 가른다).
- **유일본은 `MEMORY.md` 가 아니라 토픽 파일로 보낸다**(`.claude/MEMORY.md` 「Agent Memory Management Rules」 C-8 인덱스 원칙). 그리고
  **어느 토픽에 넣었는지 인덱스 한 줄 설명에 반영**한다 — 안 하면 방금 고친 고아 토픽 문제를 재생산한다.
- **한 블록에 유일본과 중복이 섞여 있으면** 맥락 유지를 위해 블록째 복구하되
  **어느 줄이 유일본인지 주석으로 명시**한다(판정을 확신 못 하면 복구 쪽 — 중복은 지울 수 있지만 소실은 못 되돌린다).
> **검증 한 줄:** 복구 후 **폴더 총 행수가 늘어야** 한다. 2,411 → **2,517행(+106)**, `MEMORY.md` 92 → 132행.

### 인덱스 복원 시 설명 문구 병합 (2026-08-21)
옛 인덱스를 되살릴 때 **현행 설명이 더 정확한 항목이 있다.** 기계적으로 옛 문구로 덮지 말고 **병합**한다.
- 실사례: `network-infra.md` — 현행의 `_combatStopped`(종료 시 서버 틱 정지) 설명 **+** 원문의 "Phase 1~8 범위"를 합쳤다.
- 옛 인덱스보다 **나중에 생긴 토픽**은 목록에 없다 → 복원 후 **폴더 실물과 대조**해 누락을 채운다
  (이번엔 `logging.md`·`skill-aim-coordinate.md` 2개가 그랬다: 16 → 18).

## 🔴 「분산」 작업의 진짜 목적은 행수가 아니라 **오해 제거**다 (2026-08-24 실행)

`qa-tester` 502 · `project-orchestrator` 325 · `game-design-lead` 254 를 「인덱스 + 토픽」으로 나눴다.
**착수해 보니 행수는 부차적이었다.** `project-orchestrator/MEMORY.md` 에 **「프로젝트 현재 상태」라는 똑같은 제목의 절이 7개**
(2026-08-08 / 07-31 / 06-23 / 04-13 / 04-06 / 03-26 / 「이전 상태 (2026-03-19)」)나 있었고,
**에이전트가 2026-03-19 를 현재로 읽을 수 있었다.** 총괄이 위임 시 「현재 상태 요약」을 넘기므로 오해가 전파된다.
- **판단 기준: 제목이 시간에 대해 거짓말하는가.** 「현재 상태 (과거 날짜)」·「Current branch: …」 같은 문구가 그것이다.
- **남기는 절은 제목에 기준일을 박는다** — `프로젝트 현재 상태 — 기준일 2026-08-08 (이 절만이 현재 상태다)`.
- **아카이브 파일 서두에 "현재 상태는 `MEMORY.md` 를 보라"를 반드시 쓴다.** 날짜 역순 정렬.
- 옮길 때 **본문은 한 글자도 고치지 않고**, 바꾼 것은 **오독되던 절 제목뿐**이라고 보고에 명시한다.

### 대량 이동의 안전 절차 (2026-08-24 확립 — 2026-08-17 `-378행` 과 같은 모양의 작업)
1. **작업 전에 `python3 Tools/check_docs.py` 로 기준선(0건/EXIT=0)을 먼저 찍는다.**
2. 목적지 파일은 **새 파일이면 생성, 기존 파일이면 append 전용**. `MEMORY.md` 는 **`Read`→`Edit` 만**.
3. **이관 직후 파이썬으로 대조**: 원본의 비어 있지 않은 모든 행이 목적지에 있는가 → 누락 0건 확인.
   `miss=[i for i in moved if L[i-1].strip() and L[i-1] not in set(dst.split('\n'))]`
   이걸 **삭제 Edit 을 시작하기 전에** 돌린다(원본이 아직 온전할 때만 기준선이 성립한다).
4. 삭제는 `Edit` 의 `old_string` 에 지울 블록 **전문**을 넣는다. 오타가 나면 **Edit 이 실패할 뿐 손실은 없다** —
   그래서 스크립트로 파일을 통째로 다시 쓰는 것보다 안전하다. 350행이면 350행을 그대로 옮겨 적는다.
5. **`---` 구분선·빈 줄은 아카이브로 안 따라가므로 폴더 총합이 조금 줄 수 있다.** 실제로 qa-tester 가 -7행이 됐고
   **인덱스 절(+11행)을 넣어 +4 로 돌려놨다.** 검사 `[7]` 은 임계값 0이라 이 -7 도 실패로 잡힌다 — 인덱스를 **마지막에** 넣고 재확인할 것.
6. 실적: qa-tester 756 → 760 · project-orchestrator 549 → 592 · game-design-lead 254 → 285(본체 502→167 · 325→115 · 254→97).

### 검사기는 5종 → 7종 (커밋 `3370daf4`)
`[6]` 인덱스 미링크 토픽(고아) / `[7]` 에이전트 폴더 총합 행수 감소(임계값 0, 기준값 `.claude/agent-memory/_baseline.json`).
- **`_baseline.json` 은 직접 편집하지 않는다.** 갱신은 `--update-baseline` 뿐이고 **증가 방향일 때만**. 감소 반영은 사용자 승인 + 사유.
- 「검사 5종」이라 적힌 자리는 **3곳**이었다: `WORKFLOW.md` [11]③ · `AGENTS.md` 도구 표 · `.claude/agents/document-manager.md`.
  → **도구를 고치면 그 도구를 서술한 문서를 전부 grep 한다**(`grep -rn "5종\|검사 항목\|check_docs"`). `CLAUDE.md` 100행은 항목 수를 안 적어서 낡지 않았다.
- `[6]` 의 `known_orphans` 에 있던 `project-orchestrator/roadmap-3d.md` 는 인덱스에 링크해 해소했다.
  **예외 목록에서 뺄지는 내가 정하지 않고 보고한다**(그 파일은 내 편집 대상이 아니다).

### 🔴 죽은 윈도우 경로 — 마지막 잔존은 `.claude/` 밖에 있었다 (2026-08-24)
2026-08-20 에 `.claude/agents/*.md` 6개 + `settings.json` + `.claude/MEMORY.md` 를 정리하고 *"잔존 0건"* 이라 적었는데,
**`AGENTS.md` 116행 · 155행에 2건이 더 남아 있었다**(116행 「에이전트 메모리」 표 마지막 행 = 행 전체가 `C:\Users\rmsep\.claude\projects\...\memory\`,
155행 「완료 후 업데이트 체크리스트」 마지막 행의 경로). 못 찾은 이유는 두 가지다 —
① **찾던 문자열이 달랐다**: 그동안 `Dmain`·`D:/dev` 로만 훑었는데 이 둘은 `C:\Users\rmsep` / `C:/Users/rmsep` 였다.
② **찾던 위치가 달랐다**: `.claude/` 하위만 봤는데 `AGENTS.md` 는 **리포지토리 루트**다.
> **전수 명령(둘 다 필요):** `grep -rn "Dmain\|D:/dev\|C:.\\\\Users\|C:/Users\|c:/Users" . --exclude-dir=Build_BackUpThisFolder_ButDontShipItWithYourGame --exclude-dir=.git`
- **행 전체가 죽은 경로면 삭제, 조건은 살아 있고 경로만 죽었으면 교체.** 116행은 삭제(내용이 경로뿐), 155행은 `.claude/MEMORY.md`(공용)로 **교체**했다 — 「모든 작업 완료 후 갱신」이라는 조건 자체는 유효하므로 지우면 지시가 사라진다(갱신 규칙 3).
- `WORK_HISTORY.md` · `ROADMAP.md` · `_Tasks/` 의 `C:\Users` 매치는 **"이 경로를 지웠다"고 적은 기록**이라 그대로 둔다.

### 🔴 검사 [7] 은 감소를 실제로 잡아낸다 — 그리고 기준값 갱신이 수동인 것이 요점이다 (2026-08-24 실증)
`qa_history.md` 중복 절 12행을 지우자 `[7]` 이 **qa-tester 756 → 754 (-2행), 파일 3개 → 3개**로 **즉시 실패(EXIT=1)** 시켰다.
- **자동 갱신이었다면 이 감소가 조용히 새 기준이 됐을 것**이다. `--update-baseline` 로만 내려가므로 **"이 삭제는 의도된 것"이라는 판단을 사람이 하도록 강제**된다.
- ⚠️ **기준값이 낡아 있으면 감소 폭이 왜곡된다**: baseline 은 2026-08-21 의 756 인데 직전 라운드 이관으로 실제는 **760** 이었다(증가분 미반영). 그래서 실제 삭제는 **-6행**인데 `[7]` 은 **-2행**으로 보고했다. → **증가 방향 갱신을 미루지 말 것.**
- `--update-baseline` 은 **`change_log` 에 사유를 자동으로 쓰지 않는다** — 감소 반영 시 사유가 필요하면 그 사실을 보고한다(이 파일은 내 편집 대상이 아니다).

## 🔴 「미검증 해소」 작업은 **해소 범위를 갈라 적는 것이 본체다** (2026-08-24 확립)

커밋 `bcf45ec1`(가드 8곳)의 「⚠️ 실기 미검증」을 해소하라는 요청이었다. **인계문은 「통과」였지만 실측하니 부분 해소였다.**
- **8곳 중 서버 발화가 로그에 남은 것은 2곳뿐**이었다 — 나머지 6곳은 *가드에 로그를 0건 추가한다*(Plan §6-4)는 설계 때문에 **호출당 로그가 없어 셀 수 없다.** 결함이 아니라 **그 설계의 대가**이므로 그렇게 적는다.
- 그래서 표기를 **`✅ 실기 PASS` 가 아니라 `✅ 회귀 없음 · ⚠️ N곳 발화 확인 불가`** 로 했다. 「해소」와 「완전 해소」는 다른 말이다.
> **절차:** 「미검증」을 지우기 전에 **그 항목이 주장하던 것을 한 줄씩 세로로 늘어놓고**, 이번 근거가 **어느 줄까지 닿는지** 표로 만든다. 닿지 않는 줄은 **원문의 경고를 그대로 살려 둔다.**

### 🔴 인계 수치는 **논지까지 틀릴 수 있다** — 숫자만 고치고 끝내면 안 된다 (2026-08-24)
2026-08-18 사례는 *숫자가 틀린* 경우였는데, 이번엔 **숫자가 맞는 문단의 결론이 틀렸다.**
인계문의 「재경기 — `_combatStopped` 리셋 정상」은 경기별 건수(137/129/359)가 맞았는데도 **결론이 성립하지 않았다** —
가드가 `if (!IsSpawned || !IsServer || _combatStopped) return;` 이고 **2·3경기 에디터가 클라이언트**라 `!IsServer` 에서 먼저 반환된다.
즉 **그 플래그는 평가된 적이 없다.** 문서에는 「검증됨」이 아니라 **「미검증 유지 + 재확인 조건」**으로 적고 `ROADMAP` 에 항목을 새로 세웠다.
- **판별법:** 인계문이 *"X 가 정상 동작했다"* 고 하면 **X 를 실행하는 코드 경로를 열어 조건문을 읽는다.** 로그 건수는 **그 경로가 돌았다는 뜻이 아닐 수 있다.**
- 이것은 `.claude/MEMORY.md` MistShrine 교훈 ①(*"판정 로직은 그 로직이 실제로 실행되는 조건까지 확인할 것"*)의 **문서 작업판**이다.
- 실측이 어긋난 3건(유닛 사망 건수 · 재시도 대기 *"30ms"* → 0.01~0.06초 · 로그 문자열의 `| IsServer=True` 접미 유무)은 **실측값으로 쓰고 「인계값 → 실측값」 대조표**를 문서에 남겼다.

### 로그 회차끼리 비교할 때 — **역할 구성을 먼저 맞춘다** (2026-08-24)
`[WARN]` 1,099건이 08-24 로그에만 있고 08-19 로그에 0건이라 *"이번에 생겼다"* 로 읽혔지만,
**08-19 는 3경기 내내 호스트**(`IsServer=False` 스폰 0건)였고 그 경고는 **클라이언트 전용 경로**의 것이었다.
**차이는 "코드가 바뀐 것"이 아니라 "기록한 쪽이 바뀐 것"이다.** → 회차 비교 전에 `grep -c "네트워크 스폰 | IsServer=False"` 로 구성을 센다.
- 이런 발견은 **「알려진 현상(정상 동작)」 성격으로** 적고, **다음 사람이 같은 오판을 하지 않도록 함정 자체를 문단으로 남긴다**(사용자 지시).
- 실측 명령: `grep "\[WARN\]" <로그> | sed 's/^\[[0-9:.]*\] \[WARN\] //' | sed 's/|.*//' | sort | uniq -c | sort -rn` — **문구별 분류가 먼저**다.

### `_Tasks/Plan.md` 사후 갱신 — §13 다음은 §14 (2026-08-24)
「§13-4 실기 미검증」 절이 있는 Plan 에 결과를 반영할 때는 **본문을 지우지 않고**
① §13-4 **맨 앞에 `> [✅ 날짜 해소 …]` 인용 블록**으로 해소 표시만 덧붙이고(범위가 부분이면 **어디까지인지 그 블록에 적는다**)
② 문서 끝에 **`## 14. 실기 검증 결과 (날짜 추가)`** 를 append 한다. 서두는 **자연어 설명**(규칙 13), 이어서 세션 구성표 · 핵심 지표 · **인계값 대조표** · **해소되지 않은 것**.

## 🔴 `.claude/mistakes.md` — AI 실수 누적 기록 신설 (2026-08-24)

**자동 주입되는 문서에는 포인터만, 무제한 누적은 별도 파일에.** 이것이 이 파일의 설계 이유다 —
실수는 계속 쌓여 길어지므로 `CLAUDE.md` 같은 상시 로드 문서에 넣으면 매 세션 비용이 무한히 커진다.
도달 경로: `CLAUDE.md` 체크리스트 [1] → `WORKFLOW.md` 「작업 시작 전 확인」 → `AGENTS.md` · `.claude/mistakes.md`.
- **한 파일에 목차(위) + 본문(아래).** 목차/본문을 두 파일로 나누면 **목차만 고치고 본문은 안 고치는 어긋남**이 생긴다.
- 목차 줄은 **`- YYYY-MM-DD  제목`** 로 통일(목차만 기계적으로 뽑아 쓸 수 있어야 한다). 기간 사건은 **최초 발생일**을 목차 날짜로, 전체 기간은 본문에.
- 항목 4칸 = **무엇을 틀렸나 / 왜 그랬나 / 어떻게 드러났나 / 교훈**. **교훈은 실행 가능한 행동으로** — "조심한다"는 아무것도 바꾸지 못한다.
- **시간 오름차순**(`LogRules.md` 개정 이력 표와 같은 관습) → 새 항목은 목차·본문 **양쪽 맨 아래**에 append. 두 곳을 함께 고치는 것이 이 파일의 유일한 주의점.
- 교훈이 검사기로 승격돼도 **항목은 남긴다**(도구는 되돌려질 수 있고, 그때 왜 생겼는지 아는 건 이 기록뿐).
- **`.claude/agent-memory/` 밖이라 검사 `[6]`·`[7]` 집계에 안 걸린다** — 기준값 갱신 불필요(실측 확인).
- 마크다운 링크 대신 **백틱 경로 표기**를 썼다 — 이 프로젝트 문서의 관습이고 검사 `[2]`에 걸릴 여지도 없다.

### 인덱스 등록 위치를 고른 근거 (2026-08-24)
`AGENTS.md` 「에이전트 메모리」 절이 아니라 **「작업 사이클 (Task)」 절**에 넣었다. 이유 3가지 —
① 이 파일을 읽게 만드는 규칙이 `WORKFLOW.md`(그 절의 문서)에 있다 ② **특정 에이전트의 메모리가 아니라 메인 세션·전 에이전트 공용**이라 per-agent 소유로 오해될 자리를 피했다 ③ 그 절의 인용 블록(`Read`→`Edit`/`Write` 금지)은 **`MEMORY.md` 갱신 규칙**이라 이 파일에 적용되지 않으며, 검사 `[6]`·`[7]` 대상 목록과 섞이면 혼선이 생긴다.
`.claude/MEMORY.md` 는 **「주요 문서 경로」 표에 한 줄** — 그 표가 `AGENTS.md`·`WORKFLOW.md` 처럼 **「자동 주입 없음」 주석을 단 포인터**들의 기존 자리이고, 표 행 하나면 순수 삽입 1줄로 끝난다.

### 「완료 후 업데이트 체크리스트」에는 넣지 않았다 — 타이밍이 충돌한다 (규칙 12)
그 표는 **「작업 완료 시」** 갱신 대상인데 `mistakes.md` 의 운영 규칙은 **「인지 즉시, 미루지 말 것」**이다.
그대로 행을 넣으면 **문서 세트 안에 타이밍 모순**이 박힌다 → **추가하지 않고 사용자에게 보고**했다.

### `CLAUDE.md` 는 문서 작업 대상이 아니다 (2026-08-21 사용자 확정)
*"`CLAUDE.md` 는 사용자와 메인 세션이 소통하기 위한 문서, 에이전트 지식은 메모리 파일에 기록한다."*
→ 에이전트 운영 규칙을 명문화할 자리는 **`.claude/MEMORY.md`**(공용, 모든 에이전트가 작업 전 읽음)이고 `AGENTS.md` 는 **참조만** 건다.
