# document-manager 누적 지식

## Topic files (index — an unlinked topic file does not exist)

| Topic | What is in it |
|---|---|
| [doc-conventions.md](doc-conventions.md) | ① Citing rule numbers in the two docs whose numbering restarts per section (`GameSystemRules_Buildings.md` · `GameSystemRules_UI.md`) + the repeated misreading of a two-section line ② Which document carries implementation status — `GameDesignDocument.md` is a **design** document, and the `밸런싱 미확정`(design) vs `✅ 구현 완료`(implementation) split, with the standard replacement block ③ What `check_docs.py` accepts as a rule definition (`**규칙 N. 제목**` bold only, never `## 규칙 N.`), the one-H2-per-document rule that keeps `per_section` off, and the `규칙 11-1` regex case ④ How to restructure a rule document into chapters without touching rule numbers (H3 chapters inside the one H2, reorder freely, never split a rule, moved content takes the receiving document's next number, copy-verify-then-delete), which change-history tables are descending vs ascending, and how to word a deprecation note so the "grep must return 1 hit" check can still pass ⑤ Why replacing a structure leaves self-contradicting sentences behind — grep for the *claim* the old structure made rather than its name, `check_docs.py` is blind to this class, how to word the substitution as an explicit spec, and why a stale count in a tool description gets deleted rather than corrected ⑥ Terminology unification — removing the banned word is only half of "done" (the other half is using an official name *verbatim*, which grep cannot check), auditing an index document for summaries that silently narrow the rule they point at, count copies in index bullets, marking an API that a design contract will replace later **without** pre-applying the rename **and (2026-09-03) actually cashing that marker in once the code moved** — why the same rule produces the opposite action, why the marker note itself must be updated or it becomes the misinformation it prevented, that one marker can cover sites which do **not** move together (one file ended up with a converted and an unconverted site), that a surviving marker's stated *reason* goes stale before its conclusion does, and that the enclosing heading is part of the marker — the procedure for renaming a state so it stops colliding with a tile state, and which bare enum members are convention rather than leftovers ⑦ Closing a half-rename in the second document's own vocabulary (plain language + one pointer line, never the identifier), why a count copy survives in a second section of the same index document, and the recurring prefix-scan classifications (archetype short names / parallel definition lists / pathfinding prose) ⑧ Pinning "undecided" into a document — the project's existing `⚠️ **… 미정 — 구현 시 확정한다.**` marker form, marker in the owning doc only + a pointer line elsewhere, and never filling the slot while marking it; **`.claude/mistakes.md` is inside the checker's scan set** (and quoting a bad citation as an example trips `[4]` a second time); what `check_docs.py` cannot see (intra-document citations, TDD/GDD as rule sources, a wrong-but-existing rule number); the method and the two-plus-one classes for auditing the index for summaries that narrow — or overstate — the rule they point at; and how to close a deferred item when the handed-over rationale fails measurement ⑨ Actually repairing those index summaries — the one correct repair per class (narrowing / value copy / overstatement / whole block missing) and the reusable pointer wording, why a pre-existing number in a bullet you are widening is not automatically a copy to delete (check the source's own "undecided" list first), the index's **two** reachability paths so a repaired summary can still leave the file-list row unreached, the five regex traps in `check_docs.py` reference parsing that pass or fail silently, and how to fix a wrong pointer inside a change-history row without creating a new entry ⑩ Auditing the index's **other** path — the file-list table: the H2-list 1:1 method that makes it decidable, why H2 granularity also settles what counts as an omission, the rows that are singly-reachable (no section summary of their own), the one-trailing-pointer form for deleting a count from a one-line row, why a number inside a cited section title is a name rather than a copy, and the two cases where you must measure the sibling path before editing (a count the summary repair deliberately kept / two paths that omit the same thing) ⑪ Writing a `_Tasks/` Research document — why `check_docs.py` says nothing about the file you just wrote (`_Tasks/`·`_Logs/` are outside its scan set) and how to word that in the report, why every cited code line number must be re-read after drafting (6 were off in one round) and when to cite a range vs a single line, how to judge whether two handed-over coordinate notations mean the same thing (convert with the project's own function; a figure can be right and still not mean what the sender thought) plus proving a `.asset` value is the live one by its GUID in the `.unity`, and the five-row symmetric table shape for presenting options without concluding ⑫ Recording the *results* of a multi-stage Plan — the `§9 실행 기록` row-plus-detail-section shape and why the table keeps the plan's letter order even when execution swapped two stages, the four-part note that marks a superseded design without deleting the plan text (and why the knock-on description goes in the note rather than being edited), why **「도달 불가」 is a different verdict from 「미검증」** and the three places it must be pinned, how to tell a stage-scoped count from a wrong figure before "correcting" it, what each of the three status documents does when a phase is *partly* done (narrow the roadmap row, never take it down) plus checking the *later* phase's row for items this phase absorbed, where the deferred-item list belongs, and the three role-dependent repairs for a stale description — including the row that is not stale and takes a pointer with **no count copied into it** ⑬ Closing a phase that the *same day's* records call unfinished — the four documents take four different repairs (fill the `⏳` row / swap the heading and strike the "unfinished" caveat *and* the `현재 단계:` sentence / **take the roadmap row down** per that document's own rule / new history row + a marker appended to the old row's title), 🔴 **re-home whatever the deleted roadmap row was the only pin for (「도달 불가」, the deferred-item pointer) before deleting it**, how to split a 「부분 실기 검증」 verdict so both halves stay visible in the status cell itself, how to retract a paragraph of an already-pushed commit message (put it where a reader of that commit lands — the Plan stage detail and the history row that name the hash — with *why* it was wrong plus a residual-risk note whose general-knowledge parts are labelled as unmeasured), and recording an unexamined observation as 「배제된 경로 + 정체 미확인」 which then earns a roadmap row rather than only a caveat ⑭ A Research document whose value is **measurement** — decoding a binary asset with the codec's own field order so an `offset == length` assertion upgrades a file size into a size *formula*, why a configured transport limit is not an effective limit, recomputing (never quoting) an existing design document's arithmetic and reporting **which conclusion survives** the corrected number, presenting an undecided-item count as 「N건 + 조건부 M건」 paired with whether existing UI assets can even express each answer, splitting a handed-over 「어느 문서에도 없다」 claim in half by re-grepping it, and answering 「what is scene-bound?」 from `.unity` files (`NetworkObject` counts, script GUID from the `.meta`) plus a lifetime classification of the whole folder ⑮ Replacing a 「미검증」 claim when the run came back **partial** — the ✅/⚠️ boundary sentence is the artifact (negation first, structural impossibility is not a missed test, never upgrade an eyeball check into a comparison, quote the user's own downgrade), one document owns it and the rest point at it; the four role-decided repairs extended to a *partial* resolution (heading marker / struck 과대표기 item reading 「부분 해소」 / roadmap row of the **next** phase narrowed to what verification now remains / new history row plus title markers); a 「correct behaviour that looks like a bug」 recorded exactly like 「도달 불가」 and linked to the finding it shares a cause with; 🔴 why a side finding the user neither included nor excluded gets a fact paragraph but **no roadmap row** (a row is itself a scoping decision) and must be paired with the rule it leaves unmet; and the four sentences a test-only constant change needs — including the one that guards the single-random / multi-fixed distinction ⑯ Closing a phase whose execution table was still **all-⬜** — a stage that was never started and was **downgraded from blocker to observation item** (keep `⬜`, write why the condition never applied + where it *will* apply + close the plan's "ask the user then" slot), a stage that shipped and ran in 실기 while **its own 실측 항목 were never measured** (read the new source: a `Provisional*` constant or an `Is…Measured = false` flag is the code saying the stage is unfinished — no handoff will), why a fact that makes a gap harmless does not close it, structural non-firing written as a ratio, **where a new phase's deferred items go when the old list is a dated snapshot** (new section in the current Plan + markers only on the rows whose state changed + one pointer line under the old table + a kind column), 🔴 why leftovers **you** found are folded into the phase's narrowed roadmap row instead of new rows (and why items the user said to record as 보류 stay out of the roadmap entirely), and the third class of handed-over fact that is **unverifiable in this checkout** — Unity artifacts with no `.meta` → 「확인 불가」 not 「없다」, with the indirect evidence named ⑰ A round that records **facts and decisions only** (no implementation) and corrects the previous round's own wording — the correction whose target sentence was **true but invited the wrong reading** (nothing to strike, append the missing *source*; spot it by asking whether a 「X is wrong」 sentence says where X came from, and lead the repair with the negation), two fields whose values are **coincidentally equal** (record what the coincidence hides *now* and which scheduled change *breaks* it, or the note is trivia), a decision **deliberately not executed** where the impact scope is the artifact (a file list is not a scope — the cascade is: canonical byte field → format version bump → regenerate old-format binaries → 🔴 **an already-closed verification becomes void**; and a rule document that *defines* the thing is a **precondition**, not a follow-up), 🔴 overturning a roadmap row a previous round **refused in writing** (add the row, append the overturn to the refusal, say why it is justified now — §15's no-row rule read forwards), why 「확인했고 문제없음」 earns a Plan item but **never** a roadmap row (and must state what it does *not* resolve when a ✅ and a 🔴 item share a subject), appending lettered subsections **after** the meta 근거-구분 section instead of renumbering, and the third handed-over-count variant — **the unit was wrong, not the number** ⑱ Recording a defect that is **out of scope of the task that found it** — placement is decided by **what the defect is about** (one home picked by subject + pointers that say 「the row is the single source」, never a copy), why 「보류로 기록하라」 and 「나중에 작업할 사항으로 기록하라」 are **different instructions** that land in different documents (and that a missing 보류 list is part of the rationale), severity you cannot determine belongs **in the record and in the priority cell** with what each unchecked item would change, why a handed-over fix direction stays a suggestion and must be paired with the neighbouring 미정 it shares machinery with, the **fourth handed-over-mismatch class — the count is right but the labels are not** (and that the previous round being **your own** changes nothing about B-7), and how to append an item to a closed numbered 요약 list without letting a neighbouring count silently grow ⑲ Revising a rule on the user's order and repairing every document it falsified — 🔴 **grep for who depends on a clause before deleting it** (half of a paragraph marked for deletion was the rule basis of shipped code and was quoted verbatim in two `.claude/` memory files; **the unit of deletion is a claim, not a paragraph**, and a deviation from the instruction is marked in the document *and* led with in the report), a removed clause that makes existing code **compliant** (write it into the removal rationale, then close the violation wherever it was recorded), the removal-record block as the single home (clause → reason table, the hardest reason getting its own negation-first sub-block), the **four kinds of document and their four different repairs** (rule doc rewritten / a doc with a 개정 이력 table rewritten plus a **new** version row, past rows never touched / status docs marked in place / a `_Tasks/` research record given a top 「전제가 바뀌었다」 section with a per-section valid-invalid table that says what **survives**), why the 「해소된 것」 list matters as much as the 「제거한 것」 list (and why 「규칙을 현실에 맞춘 것이지 새 사양이 아니다」 is its own sentence), renaming a rule **title** that contains the removed identifier (grep the title string; `check_docs.py` `[5]` is blind to letter-numbered rules; the ⚠️ 미정 markers inside it survive), fixing a stale header version inside the row you are adding, and grouping findings that share one coroutine into a single roadmap row with **two-way** pointers to the row that already exists ⑳ Extending an existing Plan when the user folds a **second, oppositely-shaped** job into it — never re-letter the existing stages (give the new block its own prefix; the old letters are referenced by name in seven sections and shifting them breaks every pointer silently), why a deletion inside a **serialized format** cannot obey WORKFLOW [4]'s disable-first default and what replaces the revert mechanism, turning §17-3's *"a finished verification becomes void"* bullet into an actual **gate stage** (plus the sentence that justifies it: a skipped gate leaves a failure with candidate causes from two different jobs), giving a risk the normal path cannot catch a **non-test** completion criterion, how the original document's own **absolutes** (`한 줄도` · `0건` · `없다`) go half-true and get scoped corrections rather than edits — including the one that went false **in the good direction**, why five `.md` files turned out to be tool **outputs** and therefore moved from the document stage to the regeneration stage, the fifth handed-over-mismatch class (**right as far as it went, but incomplete** — and why widening scope is still a user-confirm item), and how to record a decision that came from the user's **silence** ㉑ Deleting a whole **feature** from the rule documents (not a clause) — 🔴 the hard half is prose that depends on the feature **without naming it**, and the two classes a grep never produces (an **authority clause whose only referent was the deleted field**, decided by reading the code's *input list* rather than the prose; and a **「현재 지원 값은 N이다」** that a later version bump falsifies while 「초기값 N」 survives — grep the modifier, not the number), the **opposite treatments** a docs-before-code stage applies in one commit (delete the clauses now, leave the value and attach a stage marker, and say in the document which half is which), the removal-record table whose 자리 column names the **sub-section** and whose hardest row carries the measurement that justified **re-aiming instead of deleting**, why the format cascade goes only in the two spots that define the byte layout, 🔴 why deleting one of **two coincidentally equal values** creates the mirror-image misreading and the deletion note must **name the survivor**, the second sighting of §20-7 (**the handed list was right where it went and short in a second document** — 5 → 8 sites, both misses being a *phase scope list* and a *"what is NOT replaced" clause* rather than definitions), why **「0건이었다」 is itself a result to report**, and why a scope list of a **finished** phase is a record that gets a marker, never a deletion ㉒ Widening a **type definition** and closing pinned 「미정」 markers in one round — a new widget that fits neither branch of a two-type taxonomy is handled by widening the definition by **one word** (never a third type) plus a 개정 block that names what did **not** change, why widening leaves a **stale enumeration in the neighbouring rule** (a concrete list goes stale where a criterion does not), closing an ⚠️ 미정 marker by appending a 확정 block that answers **only what was pinned** and re-pins the rest (문구·버튼 라벨 stay 미정 — filling them invents a spec), why a singleplayer rule needs an explicit **non-applicability** line when the same round writes opponent-dependent rules, how to pick a rule-number prefix in a per-section-numbered document (`L-`/`M-` are topic initials → new feature area takes a **new** prefix; 🔴 `check_docs.py` cannot see letter-prefixed rules **at all**, so 0건 says nothing about them), pointing at the owner of a value whose **document does not exist yet** (⚠️ 가리킬 문서명 미정 — never guess a filename or rule number), 🔴 why a hand-off item saying "this is already written down" is **a claim to verify** (same mechanism + different trigger ≠ same rule — the 30초/전체 길이 countdown case), measuring the live value in **both** code and `.unity` before calling a decision a change, and the unescaped-pipe-per-row table check run over the whole file |

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

「미검증」이 **부분적으로만** 해소된 회차의 갱신(경계 문장을 어디에 두고 나머지는 어떻게 가리키는가 · 완료 문단 머리/과대표기 항목/다음 단계 로드맵 행/이력 행의 서로 다른 4가지 수선 · 사용자가 범위에 넣지도 빼지도 않은 곁가지에 **로드맵 행을 세우지 않는** 이유 · 테스트용 임시 상수 변경의 기록 4요소)
→ [doc-conventions.md](doc-conventions.md) §15.

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
Plan 이 **단계별 실행 기록 표**를 이미 갖고 있으면 새 절을 만들지 말고 **그 표의 행을 채우고 `### <단계> 단계 상세 (날짜)` 를 문서 끝에 append** 한다
(상태 라벨 구분 · 계획과 실행 순서가 다를 때의 표기 · 폐기된 설계를 지우지 않고 표시하는 법 · 「도달 불가」 표기 · 부분 완료 시 현황 3문서 처리)
→ [doc-conventions.md](doc-conventions.md) §12.

**표가 통째로 ⬜ 인 채로 한 회차에 A~I 를 전부 채우는 경우**(미착수인 채 **강등된 단계**를 어떻게 적는가 ·
단계가 스스로 내건 **실측 항목이 안 끝난 것을 코드에서 찾아내는 법**(`Provisional*` · `Is…Measured = false`) ·
새 보류 항목을 **지난 단계의 날짜 박힌 표에 섞지 않는** 이유와 대신 하는 4가지 ·
**내가 찾아낸 잔여는 새 로드맵 행이 아니라 그 단계 행을 좁혀 담는다** · 인계값 불일치 2건과 **이 체크아웃에서 확인 불가한 Unity 산출물**)
→ [doc-conventions.md](doc-conventions.md) §16.

**구현 없이 「사실·결정만」 기록하는 회차**(직전 회차 **자기 서술**의 정정 — 문장이 **참인데 오독을 부르는** 부류는 취소선 없이 **빠진 출처**를 덧붙인다 ·
**값이 우연히 같은 두 필드**는 「지금 무엇을 가리는가」와 「예정된 어떤 변경이 그 우연을 깨는가」를 함께 적어야 한다 ·
**일부러 실행하지 않은 결정**은 **영향 범위가 곧 산출물**이고 파일 목록이 아니라 **연쇄**(직렬화 필드 → 형식 버전 → 옛 형식 바이너리 재생성 → 🔴 **끝난 검증이 무효화**)를 적는다 ·
**로드맵 행을 「세우지 않는다」고 적어 둔 과거 판단을 뒤집을 때**는 그 문장에 뒤집힌 사유를 덧붙인다 ·
「확인했고 문제없음」은 Plan 항목은 되어도 **로드맵 행은 안 된다** · 글자 붙은 소절은 **메타 절 뒤에 append**해 기존 글자를 건드리지 않는다 · 인계 수치 불일치 3번째 부류 = **세는 기준이 달랐다**)
→ [doc-conventions.md](doc-conventions.md) §17.

**찾은 작업의 범위 밖인 결함을 「나중에 작업할 사항」으로 기록하는 회차**(어디에 적을지는 **결함의 주제**가 정한다 — 집 하나 + 포인터 ·
「보류로 기록하라」와 「나중에 작업할 사항으로 기록하라」는 **서로 다른 지시**라 문서도 달라진다 ·
**확정 못 한 심각도는 기록의 일부**이고 우선순위 칸에도 적는다 · 인계 불일치 4번째 부류 = **개수는 맞고 이름이 틀렸다** ·
직전 회차가 **내 문서**여도 B-7 은 그대로 적용된다 · 닫힌 번호 목록에 항목을 덧붙일 때 이웃 항목의 개수가 조용히 늘지 않게 하는 법)
→ [doc-conventions.md](doc-conventions.md) §18.

**사용자 지시로 규칙을 개정하고 그 때문에 거짓이 된 문서를 전수 수선하는 회차**
(🔴 **조항을 지우기 전에 그 조항에 기대는 곳을 grep 한다** — 지우라고 지시받은 문단의 **절반이 이미 구현된 코드의 근거**였고
`.claude/` 의 메모리 파일 2개가 그 문장을 **인용**하고 있었다. 문단이 아니라 **주장이 삭제 단위**다 ·
🔴 **조항이 사라져서 기존 코드가 오히려 규칙에 맞게 되는 경우**는 제거 근거에 적고 **위반이라 적어 둔 문서를 전부 찾아가 해소를 덧붙인다** ·
**제거 조항 → 근거 표를 개정 블록 한 곳에 두고 나머지는 전부 포인터** ·
**문서 종류 4가지가 서로 다른 수선을 받는다**(규칙 문서=본문 교체 / 이력 표 있는 기획·기술서=본문 교체 + **새 버전 행**, 과거 행 무수정 /
현황 문서=제자리 갱신 표시 / `_Tasks/` 조사 기록=맨 위 「전제가 바뀌었다」 절 + **절별 유·무효 표**) ·
**「해소된 것」 목록이 「제거한 것」 목록만큼 중요하다** · 규칙 **제목**에 든 식별자를 바꿀 때 그 제목 문자열을 grep 해 포인터를 고친다 ·
머리말 버전이 이력 표보다 낡았으면 **행을 추가하는 김에 그 행 안에서 고친다** · 같은 코루틴을 건드리는 발견들을 **한 행으로 묶고 기존 행과 양방향 포인터**)
→ [doc-conventions.md](doc-conventions.md) §19.

**이미 있는 Plan 에 사용자가 「성격이 반대인 두 번째 작업」을 끼워 넣으라고 한 회차**
(🔴 **기존 단계의 알파벳을 다시 매기지 않는다** — 새 덩어리에 **별도 접두사**를 준다. 기존 글자는 7개 절에서 **이름으로 참조**되므로 밀면 조용히 어긋난다 ·
**직렬화 형식 안에 든 필드의 삭제는 「주석 비활성화 우선」(WORKFLOW [4])을 지킬 수 없다** — 중간 상태가 없으므로 되돌리기 수단을 **커밋 분할 + 문서 취소선**으로 대체하고 **편차 자체를 사용자 확인 항목으로 올린다** ·
§17-3 의 *"끝난 검증이 무효가 된다"* 를 **게이트 단계**로 세우는 법과 그것을 정당화하는 한 문장(건너뛰면 **서로 다른 두 작업의 원인이 섞인다**) ·
**정상 경로가 못 잡는 위험에는 실기가 아닌 기계적 완료 조건**(파일 크기)을 준다 ·
원문의 **절대 표현**(`한 줄도` · `0건` · `없다`)이 절반만 참이 되므로 **grep 으로 찾아 범위 한정 정정을 덧붙인다** — 그중 하나는 **좋은 방향으로** 거짓이 됐다(순수 C# 8파일 + 기존 자체 점검 진입점) ·
`.md` 5개가 **도구 생성물**이라 문서 단계가 아니라 재생성 단계로 옮겨진다 — **문서를 목록에 넣기 전에 그 경로를 `.cs` 에서 grep 한다** ·
인계 불일치 5번째 부류 = **맞는데 덜 적혀 있었다**(규칙 3·12·14 → 실측 3·12·13·14·16) ·
**사용자의 침묵에서 나온 결정**은 판정이 아니라 **절차**로 적고 **되돌리기가 비싸지는 시점**을 못 박는다)
→ [doc-conventions.md](doc-conventions.md) §20.

**규칙 문서에서 「조항」이 아니라 「기능」 하나를 통째로 지우는 회차**
(🔴 **낱말 없이 그 기능에 기대는 서술이 어려운 절반이다** — grep 이 절대 못 만드는 두 부류 =
**사라지는 필드가 유일한 지시 대상이던 권위 조항**(산문이 아니라 **코드의 인자 목록**을 읽어 판정한다)과
**「현재 지원 값은 N이다」**(「초기값 N」 은 버전 상향 후에도 참이므로 **숫자가 아니라 수식어를 grep 한다**) ·
문서가 코드보다 먼저 가는 단계는 **한 커밋에 정반대 처리 두 가지**를 담는다(조항은 지금 지우고, 값은 두고 단계 표식만 단다 — 어느 쪽이 어느 쪽인지 문서에 적는다) ·
제거 기록 표의 자리 칸은 **소절까지** 적고, 가장 어려운 행에는 **지우지 않고 방향만 돌린 근거가 된 실측**을 싣는다 ·
🔴 **값이 우연히 같은 두 필드 중 하나를 지우면 거울상 오독이 생기므로 삭제 주석이 「남는 쪽」을 지목해야 한다** ·
§20-7 두 번째 사례 = **인계 목록이 간 데까지는 맞고 다른 문서에서 짧았다**(5자리 → 실측 8자리, 빠진 둘은 **단계 범위 목록**과 **「대체 대상이 아니다」 조항**이라 정의문처럼 생기지 않았다) ·
**「0건이었다」도 보고해야 하는 결과다** · **끝난 단계의 범위 목록은 기록이므로 표시만 하고 지우지 않는다**)
→ [doc-conventions.md](doc-conventions.md) §21.

**확정된 설계를 규칙 문서 한 곳에 써 넣는 회차**(두 타입 분류 어디에도 안 들어가는 새 위젯은 **세 번째 타입을 만들지 말고 정의를 넓힌다** —
개정 블록에 **「바뀌지 않은 것」**(판정 기준)을 반드시 적어야 기존 분류를 재감사하지 않는다 · 정의를 넓히면 **이웃 규칙의 열거가 낡는다** ·
「미정」을 닫을 때는 **못 박혀 있던 질문만** 닫는다 · 멀티 규칙은 **어디에 적용되지 않는지**를 말해야 한다 ·
섹션마다 번호가 1부터 다시 시작하는 문서에서 **접두사를 고르는 법** · **아직 존재하지 않는 값의 주인을 가리키는 법** ·
*"기존 조항이 이미 커버한다"* 고 넘겨받은 숫자는 **그 조항을 열어 읽는다** · 「변경」이라고 부르기 전에 **현재 값을 먼저 잰다** ·
표 칸 안의 `|` 를 세는 기계적 점검)
→ [doc-conventions.md](doc-conventions.md) §22.

**여러 단계 계획의 「첫 코드 회차」를 기록하는데 정작 만든 것이 아직 화면에 안 보이는 경우**
(🔴 **「API 가 있다」와 「누가 본 적 있다」는 다른 주장이고 뒤쪽은 개수다** — 실호출처를 세되 **같은 이름의 무관한 메서드**에 속지 말 것 ·
2/3 만 확인된 회귀 점검은 **2/3 로 적고 나머지 하나는 「판정이지 관측이 아니다」**라고 적는다 ·
🔴 **산출물이 이 체크아웃에 없는 커밋에 들어 있을 수 있다** — Unity 산출물을 인용하기 전에 **추가됐어야 할 식별자를 grep** 해 트리가 뒤처졌는지 본다(§16-6 의 새 원인) ·
계획의 「주석 비활성화 후 삭제」와 현재 코드가 맞는지는 **git 없이는 판정 불가**이므로 **끝 상태(실측)와 경로(미확인)를 갈라 적는다** ·
🔴 **「사용자 판단 대기」 2건은 규칙 수정이 아니라 로드맵 행**이고 규칙 문서는 **그 행을 가리키기만** 한다 ·
부분 완료 회차는 현황 3문서가 **서로 다른 모양의 수선**을 받고 머리말을 **`✅` 가 아니라 `⚠️ 부분 구현`** 으로 연다 ·
`Testcase.md` 가 없는 작업은 **만들지 말고** WORKFLOW [8] 을 건너뛴 사실과 실기 결과를 둔 자리를 적는다 ·
Research 문서의 append 는 **「낡은 것 / 해소된 것 / 🔴 그대로 유효한 것」 셋으로 가른다**)
→ [doc-conventions.md](doc-conventions.md) §23.

**결론이 뒤집힌 회차 · 규칙 번호보다 위에 있는 원칙을 기록하는 회차**
(🔴 **옛 진단이 틀린 게 아니라 전제가 달라진 경우**는 §17-1·§17-4 와 또 다른 세 번째 부류다 — 선택지와 문장을 **한 글자도 지우지 말고**
「바뀐 것은 판단 하나」라고 적고 **다른 목적으로는 여전히 유효**함을 못 박는다(「폐기」라고 쓰지 않는다) ·
규모 칸도 덧붙이되 **재지 않은 규모는 미측정**으로 둔다 ·
🔴 **모든 규칙 위에 서는 원칙은 새 `규칙 N` 이 아니라 규칙보다 위 계층(TDD 절)에 두고** 규칙 문서에는 **섹션 머리말 포인터**만 넣는다 ·
원칙 본문에 **충돌 시 어느 쪽을 고치는지**를 적어야 tie-breaker 가 된다 ·
🔴 「기존 규칙과 충돌하는지 확인하고 본문은 고치지 마라」는 지시에서는 **「대조 확인, 충돌 없음」이라는 판정 자체가 산출물**이다 ·
**같은 숫자 다른 시계**는 합치지 말고 「어느 시계인지 미확정」으로 적고 **보고에서도 묻는다**(규칙 12) ·
🔴 **없다는 코드 실측은 「찾지 못했다」로 적고 탐색 범위를 함께 남기며 직접 재측정**한다 ·
확정됐지만 미구현인 규칙 집합은 **사양(TDD) + 작업 항목(ROADMAP)** 으로 쪼개고 표는 한 곳에만 둔다 ·
🔴 **방금 내가 쓴 문장을 같은 회차에 정정할 때도 B-7 은 그대로 적용**되고 **이미 추가한 변경 이력 행도 고쳐 쓰지 말고 덧붙인다** ·
「X 는 알 수 없다」를 쓰기 전에 **기기가 닿을 수 있는 다른 곳**을 먼저 따진다 ·
구멍이 줄면 **몇 개가 남았고 각각 어느 작업 항목 소속인지** 적고 **인프라 장애를 위조 방지와 묶지 않는다** ·
**확정 옆에 나란히 놓인 제안은 제안으로 표시**하고 **이 프로젝트 실측 vs 일반 기술 사실**을 갈라 적는다 ·
내가 올린 「미확정」을 닫을 때는 **「어느 화면 · 어느 값 · 무엇을 하는가」 표**로 닫고 원문은 취소선으로 남긴다 —
그 답이 **다른 낡은 서술 하나를 함께 드러내는** 일이 잦다)
→ [doc-conventions.md](doc-conventions.md) §24.

**확정이 「이미 발표된 구조 표의 한 칸」만 뒤집는 회차 · 작업 항목 사이를 옮겨 다니는 하위 항목**
(🔴 **「누가 하는가」가 뒤집히면 표가 아니라 칸 하나가 뒤집힌다** — 「바뀌는 것은 『감지 주체』 한 칸」이라고 **글자 그대로** 적지 않으면 독자가 표 전체를 내려 읽는다 ·
🔴 **기존 RPC 를 대체하는 것처럼 보이는 새 장치는 대개 보완**이다 — 판별 기준은 「기존 것이 이미 처리하는 경우에도 새것이 발화하는가」이고, **대체가 아님을 규칙·TDD·Plan 세 곳 모두에** 적는다 ·
🔴 **하위 항목의 소속은 개념이 아니라 「오판의 결과가 어디에 떨어지는가」로 정한다** — 옮길 때 **옛 행에서 그 항목을 지우지 말고** 「순서만 바뀌었다 · 재사용한다」를 덧붙인다 ·
🔴 **순위표는 결정이 아니다** — 「1순위로 한다」를 확정으로 적지 말고 **미확인 질문과 그 답이 나올 시점**을 함께 적는다 ·
🔴 「완료 판정이 새 설계와 어긋나는지 확인하라」는 지시에서는 **어긋난 자리 자체가 산출물**이고, 수선은 **칸 안에 마커 덧붙이기 + 줄 보존 + 뒤집힘의 한계 명시** 3종이다 ·
단계 번호가 여러 절에서 참조되면 새 내용은 **`§N-1` 소절 + 표마다 포인터 한 줄** ·
🔴 **글자 붙은 규칙은 검사기에 안 보인다** — 다이어그램 안의 `규칙 D-n` 은 **바로 아래 줄에 문서명 + H2 섹션명**을 붙인다 ·
**인계받은 행 번호는 직접 재측정하고 고치는 대신 메서드 이름으로 가리킨다** ·
구현 0줄 회차는 **TDD 개정 이력 + ROADMAP 상단 블록**만 건드리고 **PROJECT_STATUS · WORK_HISTORY 는 그대로 둔다**)
→ [doc-conventions.md](doc-conventions.md) §25.

**같은 주제의 「확정만 기록하는」 회차가 두 번째로 올 때 — 1차에는 코드가 없었고 2차에는 이미 있다**
(🔴 **「같은 결정, 두 번째 자리」는 복사 회차가 아니라 포인터 회차**다 — 새 절은 **차이분만** 갖고
*"구조 자체의 단일 소스는 앞 절이고, 이 절은 「같은 구조가 여기에도 걸린다」를 정한다"* 한 문장을 반드시 쓴다 ·
🔴 **회차 전체를 가르는 질문은 「코드가 이미 있는가」** — 1차는 되돌릴 비용이 0이라 계획만 고치면 됐고,
2차는 `WORKFLOW.md` [4] 가 걸려 **주석 비활성화 → [6] 통과 후 삭제**가 된다. **그 대비를 문서 안에 표로** 넣고
**같은 사이클에서 그 규칙이 깨진 `.claude/mistakes.md` 항목**을 나란히 가리킨다 ·
**바뀔 예정인 타임라인은 「현재 구조 표 + 결정 한 줄」**로 적고 미래 표로 바꿔 쓰지 않는다(가장 센 행은 **아무 시계도 안 도는 구간**) ·
**제거 목록은 덩어리라고 적고 「남는 동작」 한 줄을 반드시 붙인다** ·
**지울 상수는 「어느 Phase 가 넣었는가 · 논의 기록은 찾지 못했다(없다가 아니다) · 씬 직렬화 값」 셋을 적는다** ·
🔴 **같은 기능에 붙은 두 숫자를 갈라 두는 양방향 수선**(새 절엔 「이 절이 닫지 않는 것」, 옛 항목엔 「다른 행이 바꾼다 + 미정은 살아 있다」) ·
**자기 절 안에서만 설명되던 숫자를 코드 자리와 이어 붙이는 한 줄** ·
**「없어질 예정」 표시는 「아직 코드가 그대로라 표는 현재로서 참」과 함께** 적고 **무관한 이웃 행을 지목**한다 ·
**직전 회차가 쓴 행 번호를 재측정해 어긋나면 B-7 로 덧붙이고 「앞으로는 메서드 이름으로」를 함께 적되 — 같은 문서 안에서 그 방침을 곧바로 어기지 않는지 다시 본다** ·
🔴 **`PROJECT_STATUS.md` 를 「손대지 않기로 한 판단」 자체가 산출물**이다 — 현황은 여전히 참이고 「예정」은 ROADMAP 의 역할이다)
→ [doc-conventions.md](doc-conventions.md) §26.

**🔴 검사기가 볼 수 없는 실수 부류 — 「확정」과 「현재 코드」가 한 문서에 함께 있을 때 그것을 인용하는 법**
(회차 관습이 아니라 **읽는 법**에 관한 절이다. `check_docs.py` **0건은 「문서가 정확한가」만 보증하고 「기록한 쪽이 그 문서를 읽었는가」는 보증하지 않는다** ·
인용할 때는 **한 문장 안에 「지금 코드는 X, 확정은 Y, 아직 반영 전」** 세 절을 넣는다 — 코드 쪽만 말하면 **확정이 번복된 것으로 들린다** ·
🔴 **같은 원인으로 두 번째인데 증상이 정반대**라서(`.claude/mistakes.md` 2026-09-03 = 확정을 미결로 되돌림 ↔ 2026-09-21 = 확정을 현재 동작으로 설명)
**목차를 제목의 증상으로 훑으면 같은 부류로 보이지 않는다** → 「이 부류는 **무엇을 읽지 않아서** 생겼나」로 찾는다 ·
문서 쪽은 결함이 없었고 **그 양방향 표시 형태가 수선을 0건으로 만든 근거**다 — 확정 블록엔 「구현 N줄」, 현재 상태 행엔 「이것을 바꿀 확정」)
→ [doc-conventions.md](doc-conventions.md) §27.

**회차 전체의 주제가 「직전 진단이 거짓이었다」인 경우 · 대화가 흐려 놓은 범위 경계를 못 박는 회차**
(🔴 **거짓이 된 진단은 코드 제거 근거가 아니다** — 거짓인 것은 **현재 시제로 읽은 문장 하나**이고 그 위에서 넣은 가드는 **유효한 예방**이라
「가드는 지우지 않는다 · 코드는 한 줄도 건드리지 않았다」를 **같은 블록에** 적는다(§17-1·§19·§24-1 에 이은 네 번째 부류 = **전제가 애초에 참이 아니었는데 그 위에서 한 행동은 옳다**) ·
🔴 **「파일이 있다」와 「씬에 놓여 있다」를 가르는 실측 2종**(`.meta` guid 로 `*.unity`·`*.prefab` 검색 0건 + 그날 로그에 `Start()` 문자열 0건)을 **둘 다** 적고, 헤더 주석의 *"씬 구조"* 는 **전제이지 사실이 아니다**라고 못 박는다 ·
**어떤 값의 유일한 독자를 무력화한 사실은 못 박힌 「미정」의 답이 아니다** — 「답이 아니라 사정」이라고 적고 순위표가 있는 **두 자리 모두**에 적는다 ·
✅ **예측이 실기로 재현된 회차의 산출물은 「근거의 강도만 바뀌었다」**이고 🔴 **역할 구성을 함께 적어야** 이웃한 수정이 그 테스트로 검증됐는지 갈린다(반대 구성이면 여전히 미검증) · **로드맵 행은 새로 세우지 않는다** ·
🔴 **범위 경계의 산출물은 「질문 → 답」 표**다 — 첫 행이 *"인게임 판정은 단계 몇 번인가?"* → 「어느 단계도 아니다」여야 하고, 단계를 한 줄로 열거해 **부재를 점검 가능하게** 만든다. 집은 Plan(`§2-2`), 표에는 포인터 한 줄, 「범위 밖」 표에는 행 하나 ·
🔴 **어느 파일과도 맞지 않는 인계 행수는 「틀렸다」가 아니라 「이 체크아웃에서 재측정 못 했다」**로 적는다(상시 append 로그 = 이 트리가 뒤처진 파일. 인계값 6번째 부류) ·
실수 항목은 **증상이 아니라 원인 층으로 목차에 색인**하고 「어느 과거 항목과 다른가」를 본문에 적는다 ·
🔴 **이미 낡아 있던 `PROJECT_STATUS.md` 를 반만 고치지 않고 손대지 않기로 한 판단**과 그 사실을 **「앞으로」를 맡은 문서 쪽에** 적는 이유)
→ [doc-conventions.md](doc-conventions.md) §28.

**구현 없이 「낡은 기록만」 고치는 회차 — 묶인 `✅` 한 칸을 쪼개고 「완료」에 등급을 매긴다**
(🔴 **여러 부품을 한 칸에 묶은 상태 셀이 낡음의 단위다** — 상태 칸 값만 `⚠️ 부분 완료` 로 고치고 내용 칸은 무수정,
**행을 늘리지 말고 표 바로 뒤에 인용 블록 + 부품별 표**를 두며 **이번에 안 본 부품도 「확인 대상 아님」으로 행을 준다**.
한 행의 두 결함이 **같은 문서의 서로 다른 절**을 단일 소스로 가질 수 있다 ·
**코드로는 참이고 동작으로는 거짓인 문장**은 취소선 없이 「서술은 맞고 그 읽기는 틀린다」를 한 문장으로 적는다(§17-1 두 번째 사례) ·
🔴 **「완료」는 등급이다** — `로그 확인` / `실기 확인` 을 상태 칸에 붙이고 **「왜 로그까지만인가(그 단계는 화면을 바꾸지 않는다) · 화면 검증은 어느 단계가 대신했는가」**를 반드시 함께 적는다 ·
**단계 번호 없는 부수 수정은 행을 세우고 그 행의 본체는 「검증 방법 = 역할 구성」**이다(지난 실기가 반대 구성이라 그 경로를 타지 않았다 + 판정 기준을 로그 문자열로) ·
**「단계 N~M 미착수」 요약의 수선은 3부분**(더 이상 참이 아닌 것 + 🔴 **그래도 살아 있는 결론** + 새로 생긴 미검증)이고 **이웃 불릿은 확정 못 하면 고치지 않고 보고**한다 ·
🔴 **§28-8 에서 「다른 회차의 일」로 넘긴 그 한 줄이 이번 회차였다** — 넘길 때 누가 고칠지 적어 둔 것이 값을 했다 ·
인계 근거를 **재측정 가능 / 로그 발췌(메인 세션 귀속) / 커밋 해시(전달값)** 셋으로 갈라 적는다)
→ [doc-conventions.md](doc-conventions.md) §29.

**여러 단계 계획의 한 단계가 「구현·푸시됐는데 실기는 0건」인 회차**
(🔴 **`⚠️ 구현 완료 · 실기 미검증` 은 §29-3 의 두 등급보다 아래 칸**이고, 산출물은 라벨이 아니라 **같은 칸에 적는 재현 조건**이다 —
**무관한 것(역할 Host/Client)도 이유와 함께** 적지 않으면 다음 사람이 다시 따진다 ·
🔴 **미검증 2건의 실기 구성이 서로 다르면 한 번의 테스트로 함께 검증되지 않는다**고 **양쪽 자리에 모두** 적는다 ·
**결론은 살아 있는데 이유가 바뀐 문장**은 취소선 없이 **「바뀐 것은 이유뿐」**을 덧붙이고, 기능 이름이 아니라 **주장(`실호출처.*0건`)을 grep** 해 4개 문서를 찾는다 ·
🔴 **직전 회차가 「내 범위가 아니다」라고 적어 넘긴 낡은 줄은 그것을 범위에 가진 첫 회차가 닫고, 닫았다는 사실을 넘긴 자리에 적는다**(§29-5 의 후속) ·
**「씬·인스펙터 작업 0건」도 기록할 발견**이다 — guid 검색으로 배치를 먼저 증명하고 「`NetworkStatusUI` 와 같은 상태가 아니다」를 못 박는다 ·
**diff 로만 잴 수 있는 완료 판정은 git 없이 재측정 불가**하므로 **끝 상태 측정으로 바꿔 적고 둘을 갈라 표기**한다(인계 7번째 부류 = **측정 수단 자체가 내게 없다**) ·
**같은 이름의 무관한 메서드** 두 번째 사례 · **계획의 파일 표가 틀린 파일을 적고 바뀐 파일을 빠뜨린 경우**는 표를 고치지 않고 「계획과 달라진 점」에 적는다)
→ [doc-conventions.md](doc-conventions.md) §30.

## 도구
- `python3 Tools/check_docs.py` — 리포지토리 루트에서 실행. 읽기 전용. **0건 확인 후 보고.**
  ⚠️ **`_Tasks/`·`_Logs/` 는 검사 범위 밖**이므로 방금 쓴 Task 문서는 이 0건이 검증한 대상이 아니다 — 보고에 그렇게 적을 것 → [doc-conventions.md](doc-conventions.md) §11.
  ⚠️ **0건은 「내가 편집한 문서」에 대한 것이고 「내가 말한 주장」에 대한 것이 아니다** — 문서가 정확한데 그것을 읽지 않아 생긴 실수는 이 도구가 영원히 0건을 낸다 → [doc-conventions.md](doc-conventions.md) §27-1.
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
**[🔴 2026-09-14 추가 — 원문 유지]** 같은 방법으로 재측정: 2026-09-08 **41개** → 2026-09-14 **46개**(무작위 맵 3단계 E 의 맵 전송 키 5종). 수치는 **덮어쓰지 말고 이렇게 덧붙인다**(`.claude/MEMORY.md` B-7).
`LogRules.md` **1.5 끝의 개정 블록은 누적**된다(37 → 41 → 46). 규약: **앞 블록의 숫자는 그 시점의 값이라 그대로 두고, 현재 개수는 맨 마지막 블록이 답한다** — 새 블록에 그 한 줄을 이어 적는다. 머리말 형태는 `> **<무엇> 신설 — 멤버 ~~N개~~ → M개 (날짜 · 커밋 \`해시\`)**` 이고, **커밋 전 작업 트리라 해시가 없으면 지어내지 말고 단계 표기(예: `3단계 E`)로 대신한다**(CLAUDE.md 규칙 10 · 규칙 5 로 git 실행 불가).
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
