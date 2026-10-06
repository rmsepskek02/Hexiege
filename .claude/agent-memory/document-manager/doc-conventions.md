# Documentation Conventions — rule citation & where status belongs

> Topic file for `document-manager`. Linked from `MEMORY.md` (an unlinked topic file does not exist).
> Everyday one-liners stay in the index; the reasoning and the failure cases live here.

---

## 1. Citing rule numbers in docs whose numbering restarts per section (2026-08-25)

Two documents under `Assets/_Project/Docs/GameSystemRules/` restart rule numbering from 1 in
**every** section: `GameSystemRules_Buildings.md` and `GameSystemRules_UI.md`.
For those two, a bare rule number identifies nothing — **always write the section name (H2 title)
next to the number.**

- ❌ `규칙 9`
- ✅ `방어 타워 시스템 규칙 9`

Check `[4]` of `python3 Tools/check_docs.py` catches this violation: it flags a reference whose
line mentions one of those two documents but carries no section name from that document.

**Do not memorize the section list or the ranges here.** The checker prints the live section
names and number ranges of both documents in its `[4]` block on every run — **that output is the
authoritative source**, and it changes whenever the rule documents change. Run it and read it.

Every other `GameSystemRules_*.md` numbers rules continuously across the whole document, so a
section name is not required there.

### 🔴 Failure case — repeated twice, so it is worth writing down

A single line may carry references to **two different sections**, e.g.

```
… 건물 철거 시스템 규칙 4·5 / 방어 타워 시스템 규칙 9 …
```

Read by eye, `규칙 9` gets attached to the *first* section on the line (건물 철거 시스템, whose
maximum is 6) and is then wrongly judged "a rule that does not exist".

**A rule number belongs to the section name immediately preceding it.** `extract_refs()` in
`check_docs.py` binds each number to the *nearest preceding* document/section mention — which is
also how a human should read it.

→ **Never adjudicate these by hand. Use the `[3]` and `[4]` results of
`python3 Tools/check_docs.py` as the evidence** (CLAUDE.md rule 10 — no guessing).

---

## 2. Which document carries implementation status (user-confirmed, 2026-08-25)

| Document | What it holds |
|---|---|
| `GameDesignDocument.md` | **Design document.** Only "what the game should be". **No implementation progress.** |
| `PROJECT_STATUS.md` | Single source for *current* implementation status |
| `GameSystemRules/*.md` | Single source for per-system implementation contract **and** status |
| `WORK_HISTORY.md` | Every past work item |
| `ROADMAP.md` | Work still to come |

### The distinction that actually caused confusion — memorize this split

| Wording | Kind | Belongs in the design document? |
|---|---|---|
| `밸런싱 미확정` · `수치 미확정` · `확정 기획` · `스탯 미확정` | **design state** | ✅ yes — the GDD keeps managing these |
| `✅ 구현 완료` · `실기 PASS` · `멀티 미검증` · `구현 현황:` · `미구현` | **implementation state** | ❌ no — remove, point at the single source |

⚠️ Watch for wording that is a *design* state but happens to use the word 「구현」 (e.g.
`유닛 타입 (구현 현황)`, `후속 구현`, `구현된 유닛`). The content stays; only the word is wrong.
Fix the wording rather than deleting the content.

### Standard replacement block (the form §3 of the GDD already used)

```
> **상세 규칙은 단일 소스 문서 참조:** [문서명](경로)
> **구현 상태는 위 단일 소스 문서를 참조한다** — 여기에 상태를 병기하면 사본이 낡는다.
```

🔴 **Attach the second line only when the referenced document actually contains implementation
status prose.** Claiming a status lives somewhere it does not is a guess (CLAUDE.md rule 10).
Real case: the 「방어 타워 시스템」 section of `GameSystemRules_Buildings.md` carries no
implementation-status prose, so only the first line was attached there.

---

## 3. What `check_docs.py` accepts as a rule definition — and the one-section rule (2026-08-25)

`parse_rule_docs()` registers a rule **only** from a line matching `^\*\*규칙\s*(\d+)[.\s]\s*(.*)`,
i.e. bold `**규칙 N. 제목**`. A `## 규칙 N. 제목` H2 is parsed as a **section**, not a rule — so a
document written that way registers **zero rules**, and checks [3]·[4]·[5] pass *vacuously* for it.
That is exactly how `_Map.md` · `_RandomMap.md` · `_Upgrade.md` sat outside the checker until
2026-08-25.

**Shape of a rule document the checker can read:**

```
# 제목                      ← H1, ignored
## 이 문서가 무엇인가 …      ← non-rule H2: becomes a section with an empty bucket → dropped
## <섹션명>                 ← the ONE H2 that wraps the whole rule block
**규칙 1. …**               ← rule definitions
### 하위 제목               ← H3 is NEVER read as a section; safe to keep
**규칙 2. …**
## 참고 문서                ← the next non-rule H2 naturally closes the rule section
```

🔴 **One section per document unless the numbering genuinely restarts.** `per_section` is computed as
`len(nums) != len(set(nums))` — a **duplicate rule number is the only thing** that turns on check [4]'s
"section name required" mode. Splitting a continuously-numbered document into several H2 sections does
not turn it on, but it does make [4] print ranges that mean nothing to a citer. Keep it to one.

### The `규칙 11-1` case — the received premise was wrong, verify before acting

The definition regex needs `.` or whitespace **immediately after the digits**, so
`**규칙 11-1. 장식 단계**` matches **nothing**: `\d+` takes `11`, `[.\s]` meets `-` and fails; backtracking
to `1` then meets `1` and fails too. Measured — it creates **no duplicate 11** and `per_section` stays
off. The widely repeated claim that "the regex grabs the `11` and double-counts rule 11" describes
`RE_RULE_MENTION` (the *reference* regex, `규칙\s*(\d+)(?:\s*[~-]\s*(\d+))?`), **not** the definition one.
Two different regexes; do not carry a claim about one over to the other.

Bold is still the right markup for `11-1`: demoting it to H3 would make it read as a sub-rule of
규칙 11, and the two subjects are unrelated (장식 단계 vs 건물 경로 차단). H3 would not register it as a
rule either, so H3 buys nothing and costs meaning.

**[🔴 2026-08-25 correction — the analysis above stays, the exception it describes is gone]**
The regex analysis remains valid and is the reason the hyphenated form must never be used again.
The `11-1` **exception itself was resolved on 2026-08-25 by user instruction**: `**규칙 11-1. 장식 단계**`
was renumbered to `**규칙 15. 장식 단계**` and moved to the end of the rule block (after 규칙 14, before
`## 용어 정의`). The five body bullets were not altered — only the number and the position changed.
Renumbering was safe because **nothing outside this document referenced `규칙 11-1`**: a repo-wide grep
(excluding `_Tasks/` · `_Logs/`) found it only on the definition line itself and in this memory folder.
The subject-unrelatedness noted above is precisely why a standalone number 15 fits better than a
sub-number of 규칙 11. `GameSystemRules_RandomMap.md` now registers **1~15 continuous**, and check `[1]`
(missing rule numbers) confirms no gap.

→ **Takeaway that outlives the case:** a rule number must be `숫자` followed by `.` or whitespace.
No hyphens, no sub-numbers. If a rule feels like a sub-rule, either fold it into the parent rule's
body or give it its own number at the end — never `N-1`.

**2026-08-25 conversion result** — `규칙 정의 원본` **7 → 10 문서**, all 7 checks 0건:

| 문서 | 새 섹션명 | 규칙 |
|---|---|---|
| `GameSystemRules_Map.md` | `## 맵 공정성 검증 규칙` | 1~5 |
| `GameSystemRules_RandomMap.md` | `## 무작위 맵 생성 규칙` | 1~15 |
| `GameSystemRules_Upgrade.md` | `## 유닛 강화 시스템 규칙` | 1~13 |

> The RandomMap row read `1~14 (+11-1, 미등록)` when this table was first written. **Changed to `1~15`
> on 2026-08-25** because `규칙 11-1` was renumbered to `규칙 15` (see the correction in the `규칙 11-1`
> case above) — the figure changed because the fact changed, not because the original count was wrong.

⚠️ **Two documents now describe a limitation that no longer exists** and were left untouched as
out of scope (CLAUDE.md rule 6) — report them, do not silently fix:
`Assets/_Project/Docs/WORKFLOW.md` [11] (the 「Map · RandomMap · Upgrade … 규칙 33개는 검사기에
존재하지 않는다」 paragraph) and `.claude/agents/document-manager.md` (the 「실무상 결론」 bullet).

**[🔴 2026-08-25 correction — both were fixed in the follow-up round; the ⚠️ above is now closed]**
The user authorized the follow-up, so those two passages are no longer stale:

| 자리 | 무엇이 있었나 | 무엇으로 바뀌었나 |
|---|---|---|
| `WORKFLOW.md` [11] | 「🔴 알려진 한계 — 규칙 33개가 검사기에 존재하지 않는다」 절 + 7개 vs 6개 표 | 「🔴 규칙 정의는 반드시 `**규칙 N. 제목**` 형식으로 쓴다」 **형식 규칙** 절. 표는 삭제(숫자를 10으로 고쳐 적으면 또 낡는다) |
| `.claude/agents/document-manager.md` | 「🔴 실무상 결론: 이 세 문서는 "[3] 0건 = 정상"을 믿지 말 것」 | 「규칙을 새로 쓸 때 굵은 글씨 형식을 쓸 것」 **행동 지시** |

🔴 **The limitation is gone but the format rule must stay written down.** Deleting the passage
outright would invite the next person to write `## 규칙 N.` and recreate the same blind spot. Both
rewrites therefore keep the *why* (`## 규칙 N.` parses as a section name → that document registers
zero rules → [3]·[4]·[5] pass vacuously) and drop only the now-false inventory.
**Never re-add a document count to either passage** — the checker's `[검사 범위]` block is the
authoritative source for those numbers.

---

## 4. Restructuring a rule document into chapters (2026-08-26, map rules correction)

`GameSystemRules_RandomMap.md` was reorganised into 7 chapters (공통 사양 / 공통 생성 절차 / 유형별
사양 / 생성 검증 / 런타임 동작 / 네트워크 / 용어 정의) without touching a single rule number.
What made it safe:

- **Chapters are `### H3`, not `## H2`.** The whole rule block stays inside the one existing H2
  (`## 무작위 맵 생성 규칙`), so the document still registers as a single section (§3 above). H3 is
  never parsed as a section, so chapter headings cost nothing. The glossary keeps its own H2 and
  simply became "7장" in its title.
- **Reordering rules inside the document is fine** — check `[1]` looks at `set(titles)`, not order.
  So chapter order (1,2 / 3,12,15 / 4~8 / 13 / 9,10,11 / 16,14) may differ from numeric order.
  State that mismatch in the document's own reading guide, or the next reader will "fix" it.
- **Never split one rule across two chapters.** Writing `**규칙 N.**` twice makes
  `per_section = len(nums) != len(set(nums))` flip to True, which turns on check `[4]`'s
  "section name required" mode for *every* future reference to that document.
- **Content moved to another document takes the receiving document's next free number.**
  규칙 11 (경로 막힘) moved to `GameSystemRules_Units.md` as **규칙 45** (its next number), and the
  map document kept 규칙 11 as a one-line pointer. Reusing the origin number would collide.
- **Order of edits that prevents loss**: write the new copy at the new position first, verify every
  bullet of the original survives, only then `Edit`-delete the old block. A session interruption in
  the middle then leaves a *duplicate*, which the checker catches — not a hole, which it cannot.
  (This actually happened: the run was cut by an API limit with 규칙 15 present twice.)

### Change-history tables are NOT all ascending — check before appending

| 문서 | 정렬 |
|---|---|
| `LogRules.md` 개정 이력 | ascending (newest at the **bottom**) |
| `TechnicalDesignDocument.md` 📝 변경 이력 | **descending** (newest at the **top**, 0.44.0 above 0.43.2) |
| `GameDesignDocument.md` 📝 변경 이력 | **descending** (1.14.0 above 1.13.0) |

A task brief that says "append the new row at the bottom, time-ascending" is wrong for the two
design documents. **Open the table and read the first two rows before appending**, then report the
deviation instead of silently following the brief.

### Deprecated wording has to survive somewhere, but only once

When a term is retired (「보호 통로」 → **필수 통로**), the completion check is a repo-wide grep whose
expected result is **exactly one hit: the glossary `_Avoid_` line**. So a deprecation note in another
document must be written *without* repeating the retired words — say "종전 이름" and point at the
`_Avoid_` row. Same trick for retired type names (`TerrainKind`/`BuildRule` → `TileKind`): explain the
old shape as 「지형 종류와 건설 규칙 두 필드」 rather than quoting the identifiers, or the grep never
reaches zero. Historical change-log rows are the one allowed exception and are never edited.

## 5. Replacing a structure leaves contradictions in the sentences written under the old one (2026-08-31)

The fallback structure was replaced with **one fixed template per map type (5 total)**, with the mine
count, initial gold and starting-mine side baked into the template. The replacement was written into
two places (the rule document's fallback subsection and the TDD's `deterministic fallback 정의`), but
**four sentences written under the previous structure survived elsewhere** and asserted the opposite —
that the values chosen at match start stay in force through the fallback path too. Both statements
looked locally correct; only reading them together showed they could not both hold.

- **The trap: the change lands where the new structure is described, not where the old premise was
  assumed.** Preambles, player-facing summaries in the GDD, and one-line builder-input clauses in the
  TDD carry the old premise without ever naming the structure, so a grep for the new structure's
  vocabulary finds none of them.
- **What actually finds them:** grep for the *claim the old structure made*, not for its name.
  Here `"폴백까지\|fallback까지\|같은 광산 수"` over `Docs/` minus `_Tasks/`·`_Logs/` returned every
  live occurrence plus two change-history rows (correctly left alone).
- **`check_docs.py` cannot see this class of defect** — it checks rule numbers and links, not whether
  two prose sentences can both be true. It reported 0 findings the whole time.
- **How the fix was worded:** state the substitution as an explicit spec ("using the fallback replaces
  mine count / starting-mine side / initial gold with the template's values; map type and the
  test-mode flag are what survive"), and name the single source for the template's values instead of
  repeating the numbers. In the GDD the same fact goes in plain player-facing language **with no
  numbers** — values belong to the rule document.
- Check whether the required log fields already cover the substituted values before proposing new
  ones; here 중립 광산 수 · 시작 광산 방향 · 실제 초기 골드 · 폴백 사용 여부 were already listed, so
  saying so in one line was enough.

### A stale number in a tool description is worse than no number

`AGENTS.md` described `check_docs.py` as reading only some of the `GameSystemRules/` documents, with a
count that had gone stale — it warned about a blind spot that no longer existed. **The fix is to
delete the number, not to correct it** (`WORKFLOW.md` [11]: the checker's `[검사 범위]` block is the
authority, never a copy). What replaced it is the fact that does not go stale: writing a rule in any
form other than the bold `**규칙 N. 제목**` drops that document's rules from the checks entirely and
the checker still reports 0.

---

## 6. Terminology unification: removing the banned word is only half of "done" (2026-09-01)

The map-document rounds keep producing the same class of defect, and grep keeps passing it.

### The failure that grep cannot catch

`GameSystemRules.md` (the **rule-document index** — it had been out of scope for every earlier round,
which is why three separate defects had accumulated in one small section) said "성 인접 **영역**→광산
인접 **영역**". The task was to move to the settled 「칸」 vocabulary. Four official names exist:

| Document | Names it uses |
|---|---|
| universal fairness doc — `GameSystemRules_Map.md` 규칙 4 | 시작 칸 · 도착 칸 |
| 11×21 spec — `GameSystemRules_RandomMap.md` 규칙 13 | 성 접근 칸 · 광산 덩어리 접근 칸 |

> ⚠️ Those descriptors sit **before** the rule number on purpose. `check_docs.py` [5] treats a
> parenthesis right after `규칙 N` as "content annotated for that rule" and compares it word-by-word
> against the real rule title — in **all 75 scanned files, agent memory included**. Writing
> `규칙 4 (universal fairness doc)` fails the check. Put the gloss ahead of the reference, or quote the
> actual title.

I wrote **「성 인접 칸」·「광산 덩어리 인접 칸」** — a fifth name, matching none of them. Swapping the
one banned word into the old sentence skeleton (`성 인접 ○○`) produced a hybrid automatically.

- The grep for 「영역」·「집합」·`region`·`set` returned **0** — the new name contains no banned word.
- `check_docs.py` returned 0 too; it reads rule numbers and links only.
- What caught it: the **first-time-reader read-through** of the section, which the instruction
  required as a separate verification step. Same detector as the 2026-08-26 catch.

### Rules that follow from it

1. **Completion has two halves**: (a) the banned form is gone, (b) what replaced it is *verbatim one of
   the official names*. Checking only (a) breeds banned-word-free variants.
2. **Do not compose an official name — copy it** from the single-source document. If the sentence then
   reads awkwardly, rewrite the sentence, never the name.
3. **An index bullet uses the vocabulary of the document it points at.** When a universal-rules doc and
   a concrete-spec doc name the same concept differently, pick the name the reader will find after
   following *that* link. (`GameSystemRules_Map.md` carries a note saying the two name sets are the
   same concept, so pointing at either is safe as long as you use its own names.)
4. **Distrust the judgment "this is a one-word swap."** Keeping the old skeleton and swapping a word is
   how hybrids appear.

### The index document needs its own summary-vs-source pass

Beyond the terminology, reading `GameSystemRules.md` 「맵 관련 작업」 as a newcomer surfaced a second
class: **the summary silently narrows the rule.** Its re-validation bullet listed a shorter trigger set
than `GameSystemRules_Map.md` 규칙 5 actually requires, and its initial-gold bullet omitted the
test-mode branch that 규칙 3 defines. Neither is a wrong word — both are *true but incomplete* lines
that a reader will treat as the whole rule. **When auditing an index, compare each bullet against the
rule it summarizes and ask whether the omission changes what a reader would do**, then report rather
than silently expanding scope.

### Count copies live in index documents

「다섯 맵 유형」 in a bullet and 「맵 5종」 in the file-list table are both copies of a number whose
single source is a chapter of another document. Rewrite the bullet so **no count is written at all**
and point at the source chapter — do not "correct" the number.

### Marking an API that will change later, without breaking it now

`GameSystemRules_AI.md` documents `HasGoldMine` for mine-tile lookup. That is **not** deprecated — it
is live code (verified 2026-09-01 in `Domain/Hex/HexTile.cs`, `Bootstrap/GameBootstrapper.Map.cs`,
`Presentation/Grid/HexGridRenderer.cs`). A design contract in `TechnicalDesignDocument.md` 「기존 코드
전환 요구」 replaces it with `MineKind` **when the random map is implemented** (that section sits under
「무작위 맵 시작 동기화 (확정 설계, 미구현)」).

- **Do not pre-apply a planned rename.** Changing the notation now makes the document disagree with the
  code that exists today, which is the failure mode the notation was supposed to prevent.
- Attach a **transition marker** instead: what it becomes, when, and the single source for the change.
  Say explicitly that the current wording is correct until then.
- Before writing any of that, **verify in `.cs` that the API is live** — an instruction calling
  something "deprecated" is a claim to check, not a fact to copy (`.claude/MEMORY.md` A-2).

#### 🔴 2026-09-03 — the marker was cashed in. The lesson above still stands.

**The "when" arrived.** Random-map **phase 1 (tile state contract transition)** shipped and passed an editor
playtest, the mine-flag storage field was deleted from the code (`grep` over `Assets/_Project/Scripts/` →
**0 hits**), and `AIOpponentController.CacheMineTiles` now reads `MineKind != MineKind.None`. So the two
`GameSystemRules_AI.md` sites were rewritten to `MineKind`, and the `⏳` marker was replaced with a `✅ 전환
완료` note. **The original account above is kept verbatim** — the entry is not "wrong", it recorded a correct
decision under conditions that have since changed, and deleting it would delete the reason the transition was
deferred (`.claude/MEMORY.md` B-6, B-7).

- **Why it was right not to change it on 2026-09-01, and right to change it on 2026-09-03:** the rule was
  never "never rename" — it was **"do not rename ahead of the code."** On 09-01 the field was live, so the
  new notation would have been false. On 09-03 the field is gone, so the old notation is false. **Same rule,
  opposite action.** The trigger to re-read a transition marker is a `.cs` measurement, never a calendar date
  or a hand-off memo saying the work is "done".
- **Cashing in a marker is a two-part job.** (1) change the notation in the documents; (2) **update this note
  itself.** Miss (2) and the next session reads "not deprecated — live code" and re-introduces the old name.
  A transition marker that outlives its transition becomes the misinformation it was written to prevent.
- 🔴 **A transition marker may cover several sites that do NOT all move together.** `GameSystemRules_AI.md`
  carried two of them: the **mine-tile lookup** (transitioned) and 규칙 26's **placement predicate**
  (still on the walkability test — phase 3). Both live in the same file, and the same `.cs` file
  (`AIOpponentController.cs`) now contains one converted site and one unconverted site. **Measure each site
  separately; never let one site's completion be written as the file's completion.** After editing, say in
  the document which sibling is still pending and why.
- **When a marker survives, re-check its stated *reason*, not just its status.** 규칙 26's marker justified
  waiting with "the code has no such tile-state axis yet". Phase 1 created that axis, so the reason had gone
  stale even though the conclusion (still wait) had not. The replacement reason is measurable: the fixed map
  sets `TileKind` **nowhere** (`.cs` 0 hits), so every tile is `Normal` and the two predicates cannot yet
  produce different answers. **A marker with a dead reason gets believed for the wrong cause and then gets
  cashed in at the wrong time.**
- **Heading text is part of the marker.** The enclosing heading said 「(확정 설계, 미구현)」; phase 1 made that
  false for one sub-section while leaving it true for the rest. Fix the heading and say **which part** moved —
  otherwise the reader either over-reads (whole feature done) or under-reads (nothing done). Here: new types
  exist, but 4 of the 6 have **zero call sites**, and *a type existing is not the contract working.*

### Naming a state so it cannot collide with a tile state

`GameSystemRules_Units.md` 규칙 45 called a **unit's** stuck state by the same code-font name as the
**tile** state `TileKind.Blocked`. Procedure used, and reusable:

1. `grep -rnw "<name>" Assets/_Project/Scripts --include=*.cs` — whole-word, to see whether code
   already owns the name. Here: **0 hits**, so the document was free to choose.
2. If code owns it, **do not rename** — add one sentence saying it is a different thing from the
   similarly named one.
3. If not, rename to something that shows the owner (`PathBlocked`), and **record in the document that
   the code has no such identifier yet**, with the date of the measurement.
4. Write the "why" **by meaning** — "a name that did not distinguish it from the tile state" — never by
   quoting the old bare name, or the deprecation grep will trip on your own sentence
   (`.claude/mistakes.md` 2026-08-26 and 2026-08-31).

### Consequence to hand back, not to fix

Renaming in one document leaves the old name in every other document that copied it — here
`GameDesignDocument.md` still carries the unit state's old name. When the fix scope is closed to a list,
that is a **finding to report**, and reporting it matters more than usual: an unreported half-rename is
worse than no rename, because the two documents now disagree.

### Bare enum members: convention, not a leftover

`TileKind.Normal` / `.NoBuild` / `.Blocked` are required when *referring* to a value. Bare members are
the established form in exactly two places — the definition table listing the members of `TileKind`,
and a comparison whose left operand is `TileKind` (`TileKind != Blocked`). A prefix scan will flag both;
classify them rather than "fixing" them. What is a real leftover is a bare member used referentially in
prose (found in `TechnicalDesignDocument.md` 「archetype generator 알고리즘」, `OuterGenerator`).

---

## 7. Closing the three leftovers of §6 (2026-09-01, same day, follow-up round)

All three were one-liners the previous round created or missed. Worth writing down because each is a
*shape* of leftover, not a one-off.

### A half-rename is closed in the second document's own vocabulary, not by copying the identifier

`GameSystemRules_Units.md` 규칙 45 renamed the unit state; `GameDesignDocument.md` still carried the old
bare name, so the two documents disagreed (§6 「Consequence to hand back」). The fix is **not** to write
the new identifier into the design document — GDD change-history 1.15.0 established that a design
document writes in Korean and code-contract notation belongs to the TDD. So the GDD gets

1. a plain-language phrase in the bullet (「길이 막힌 상태」), and
2. **one pointer line** naming the document+rule that owns the official name.

That closes the disagreement without putting a code identifier back into a design document. Word the
change-history entry **by meaning** ("코드 이름으로 적던 자리") — quoting the retired bare name would
re-break the deprecation grep.

### A count copy can survive in a second section of the same index document

The previous round removed the map-type count from `GameSystemRules.md` 「맵 관련 작업」 and reported it
done; the identical copy in that file's 「파일 목록」 table survived because it is a different section.
**Scope a count-copy sweep to the document, not to the section** — the same lesson as the TDD prefix
sweep (⑬ in change-history 0.45.0: 「개수를 못 박은 지시가 남긴 갈라짐이라, 이런 통일 작업은 절 단위가
아니라 문서 단위로 훑는다」). Rewrite so no count is written at all and point at the owning chapter;
watch that a *different* count on the same table row (there: 특수 공격 시스템 확장 5종) is a different
subject and must not be touched.

### Prefix-scan classifications that recur — decide once, reuse

A case-insensitive prefix-agnostic scan for the tile-state words over the 75 scanned `.md` returns ~57
hits. Beyond §6's two established classes (definition table, `TileKind` comparison), these recur:

| Hit | Verdict |
|---|---|
| `Open/Obstacle` in the TDD mine-sampling bullets | **archetype short names** — siblings on the neighbouring bullets are Canyon / Outer / ThreeLane, so they name map types, not tile states |
| GDD 「타일 상태」 numbered list, English glosses in parentheses | **parallel definition list** — all six items share the form 「우리말 (English)」; changing one produces the hybrid §6 warns about |
| `blocked 체크` in pathfinding prose (TDD, `PROJECT_STATUS.md`, `WORK_HISTORY.md`) | **different subject** — the A* goal-blocked check, not `TileKind` |
| committed change-history rows | never edited |

---

## 8. Pinning "undecided" into the docs, and the two blind spots the checker has (2026-09-01, closing round)

The round's goal was stated as **"drive open items to zero"** — anything the user had waved off as
"later" was to be either fixed or **nailed into a document**, so nothing survives only in the chat.
That framing changes what "done" means: **"I left it alone and told the user" is not done.**
Done is either an edit, or a written marker at the place a future reader will stand.

### 8-1. The project's existing "undecided" marker — reuse it, don't invent one

`TechnicalDesignDocument.md` (transfer-protocol bullets, and again in the timeout/retry list) already
carries the form:

> ⚠️ **근거 미확인 — 구현 시 NGO 실측으로 확정한다.**

Shape to copy: **inline at the end of the bullet it qualifies** (never a separate block that drifts
away from its subject), a `⚠️` + one bolded sentence naming *what* is unknown and *when* it gets
settled. Longer justification, if any, goes in an unbolded sentence right after — still on the bullet.

Three markers went in this way on `GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 M-3 · M-4
(single-play failure wording · single-play loading-UI handling · whether rematch failure shows a popup).

**Rule for where the marker lives: the owning document only.** The same three facts are also described
in `GameSystemRules_RandomMap.md` and `TechnicalDesignDocument.md`. Marking all three would recreate the
copy problem the markers exist to prevent. So: **marker in the single-source doc, and in each other
place a one-line pointer that says "undecided items exist, the list is over there" — never the list itself.**
Check both other places first: if a reader standing there already cannot tell, the pointer is required.

**Do not fill an undecided slot while marking it.** Two of these three were gaps in the spec
(no wording had ever been chosen; loading-UI handling was silent in *all three* docs). Writing a
plausible value there is adding a new spec under cover of a cleanup — mark it, don't author it.

### 8-2. 🔴 `.claude/mistakes.md` is inside the checker's scan set

Writing a mistake entry that cited a rule from one of the two per-section documents made
`check_docs.py` fail with `[4]` (**EXIT=1**) — pointing at `.claude/mistakes.md` itself.
`.claude/**` is part of 참조 검색 대상; a memory or mistakes file gets the same citation rules as a spec.

Worse, the obvious fix loops: **quoting the bad form as an illustration trips `[4]` a second time**
(the checker cannot tell a citation from a quotation of a citation). The way out is to *describe*
the wrong form in words rather than reproduce it — which is the same rule as
「폐기 표기를 설명문·이력에 인용하지 말 것」. Run the checker after touching `.claude/` files, not just `Docs/`.

### 8-3. What `check_docs.py` measures — and the two things it cannot see here

`RE_DOC_MENTION` is `GameSystemRules_\w+\.md`. Consequences worth knowing before trusting a 0:

- **Intra-document citations are invisible.** The house style for citing a sibling section inside
  `GameSystemRules_UI.md` is 「공통 UI 규칙 8」 with no filename — so `[3]`/`[4]`/`[5]` never look at it.
  Correctness there is on the writer, not the tool.
- **`TechnicalDesignDocument.md` and `GameDesignDocument.md` are not rule-definition sources**, so
  a rule number attributed to them is never validated either.
- **A wrong-but-existing number passes.** `[3]` only catches numbers that do not exist; `[5]` only
  compares text in parentheses. A citation like 「…`GameSystemRules_RandomMap.md` 규칙 13 실패 복구 절」
  where the failure-recovery section actually sits under 규칙 16 passes both silently.
  (Found exactly this in a `TechnicalDesignDocument.md` change-history row this round; recorded, not fixed
  — history rows are records.)

So `check_docs.py` measures rule-number existence, section disambiguation, parenthetical agreement,
link targets, and agent-memory integrity. It measures **nothing** about whether a summary faithfully
represents the rule it summarises, whether a value copy has gone stale, or whether prose casing is
consistent — all three of which were the actual substance of this round.

### 8-4. Auditing the index for summaries that narrow their rule — method and yield

`GameSystemRules.md` 「시스템별 빠른 참조」, every section except the map one, compared bullet-by-bullet
against the rule text. **16 findings.** Two classes, both worth naming because they fail differently:

1. **The summary drops a condition/trigger/branch** → the reader concludes "that part doesn't apply to me".
   The severe variant is **a whole rule block missing from the index** — e.g. the UI section listed no
   bullet at all for the loading-UI rules or the map-prepare-failure rules, so nobody arriving via the
   index learns those exist. Look for this by listing the rule doc's rule titles and asking which
   *headings* have no bullet, not which sentences.
2. **Value copy** — a count, number, or name list transcribed into the index. Worst case is a value the
   rule itself calls tunable (an Inspector field), and a value that also exists as a code constant,
   because then it is a three-way copy.

A third shape showed up that is neither: **the summary asserts a branch the rule marks as future work**
(index listed Victory/Defeat BGM split; the rule says V1 plays one end-of-game BGM). That is a summary
that is *wider* than its source, and it reads as "already built". Watch for it alongside the narrowing kind.

Also recurring: **the index drops a rule that says "these numbers are all provisional"**, which turns a
provisional spec into a settled-looking one — a 과대 표기 violation created purely by summarising.

**Findings go to the task's `Research.md` §6, not into the index in the same round.** With this many
sections, fixing while auditing makes the verification shallow. Record per finding: which section and
line / which rule it disagrees with / which class. **Also record the list of sections compared and what
was deliberately not compared** — otherwise the next round cannot tell coverage from silence.

### 8-5. Closing a "판단 보류" item when the handed-over rationale does not survive measurement

Asked to close a deferred item as "not a defect", with a rationale to use. Measuring first showed the
rationale's premise was false as stated (the lowercase word appeared 3× in the whole document, two of
them being the disputed lines themselves; the *capitalised* form was the one used as a common noun, 21×).

**The conclusion still held, but on different evidence** — the surrounding section writes English common
nouns in lowercase throughout (`mine` beside `MineKind`, `traversable` beside `StaticTraversable`), so
the disputed word is prose, not a misnamed identifier. **Write the conclusion with the evidence that
actually measured, and state plainly that the handed-over premise did not match.** Closing on a premise
you disproved is how a false claim gets laundered into a permanent record.

Procedure: **measure before writing the closing rationale, not after.** Count both cases of the word,
and count the *sibling* words in the same clause — one word alone cannot establish a house style.

---

## 9. Actually repairing the index summaries §8 found (2026-09-01, execution round)

The 18 findings §8-4 recorded were repaired in `GameSystemRules.md` in one round. What the execution
taught, beyond the audit method:

### 9-1. Each class has exactly one correct repair — do not mix them

| Class | Repair | Why not the other one |
|---|---|---|
| **Narrowing** (a condition, trigger, branch or whole rule block missing) | Rewrite so the missing thing is *visible*, then **point at the source rule** — never transcribe the detail | This is an index. Transcribing the detail is how the value copies got here in the first place |
| **Value copy** (count, number, name list) | **Delete the number/list** and replace it with `…의 단일 소스는 (문서) 규칙 N` | Correcting the number keeps the copy alive; it will drift again on the next spec change |
| **Overstatement** (index asserts what the rule defers to future work) | Rewrite so the **undecided-ness is what the reader sees** | Narrowing merely hides something; this one asserts something untrue |
| **Whole block missing** (no bullet anywhere for a rule group) | **Add a bullet that can reach it.** Naming the group's H2/H3 heading is enough | Folding it into a neighbouring bullet keeps it unfindable by heading |

The wording that worked for the pointer form, reused throughout: `X — Y 의 단일 소스는 (문서) 규칙 N`.
It states what the index is responsible for (that X exists) and hands off what it is not (Y's value).

### 9-2. Pre-existing values in a bullet you are widening: do not strip them reflexively

The MistShrine bullet carried `1초 discrete 틱` and `기본 OFF`. Both are numbers, but the rule that lists
what is provisional (`규칙 16`) **does not include the tick interval** — it is settled spec, not a tunable.
Deleting it would have *narrowed* the summary, which is the defect this round exists to remove.
**Check the source's own "undecided" list before treating a number as a copy to delete.** Where the values
stay, add the undecided marker *above* them rather than removing them.

### 9-3. The index has two reachability paths, and fixing one leaves the other

`GameSystemRules.md` reaches a rule group **twice**: the 「파일 목록」 table row and the per-section
summary. §8's audit covered only the summaries, so after repairing them the AI row of the table still
omits the same 「10. 아키텍처 및 구현 규칙」 block that finding A was about. **When a whole block was
missing from a summary, check the file-list row for the same document before calling it reached.**
Method: list the source document's H2 headings and diff them against the row's text.

### 9-4. Regex traps when writing index pointers (all of these pass or fail silently)

- `규칙 N(내용)` — a parenthesis **immediately** after the number is parsed as a 병기 label and checked by
  `[5]` against the rule's title+body. Put a space and a dash instead, or the check may fire on a correct
  reference. `규칙 N)` (closing paren) is not a label and is safe.
- `규칙 8-1` parses as the range 8~1. Harmless while both are in range, but it is not what you wrote.
- Only the **first** number after the word `규칙` is captured: `규칙 6·7` checks 6 only, `규칙 30~37 · 40~42`
  checks 30~37 only. Do not assume the checker validated the whole list you wrote.
- A rule number is bound to the **nearest preceding document mention within 80 characters**. Past that it
  is not checked at all — so a wrong number in a long history-table cell is invisible to `[3]`.
- `[4]` passes when *any* of that document's section names appears **anywhere on the line** — the section
  name does not need to sit beside the number.

### 9-5. Fixing a wrong pointer inside a change-history row

History rows are not retro-edited, but a **wrong pointer** is not narrative — it sends the reader to an
unrelated rule (here: 「실패 복구 절」 attributed to 규칙 13, which is 「생성 완료 검증」; the section is
under 규칙 16). Repair it, and note it: **verify the diagnosis in the source first, change only the wrong
occurrence** (the same row already carried the correct number in a later paragraph), **create no new
history entry**, and leave a one-sentence `**[YYYY-MM-DD 정정]**` inside the row's existing addendum.
Neither `[3]` (the number exists) nor `[5]` (no parenthesised label) can see this class.

## 10. The index's *other* reachability path — the file-list table (2026-09-01, follow-up round)

§9 repaired the per-system summary bullets of `GameSystemRules.md`. The **file-list table at the top of
the same file** carried the same two defect classes and was untouched — so a rule block could be missing
from **both** paths, or fixed in one and still invisible in the other. **When repairing one path, open the
other in the same round.**

**The audit is mechanical and cheap.** One command gives the ground truth for every row:
`grep -n "^#\{1,3\} " GameSystemRules/<doc>.md` — then read the row against that document's **H2 list, 1:1**.
13 rows took one pass. Findings that round: `_AI.md` row missing 「10. 아키텍처 및 구현 규칙」,
`_Skills.md` row missing 「발동 경로」·「추후 데이터로 확정할 항목」, and — found only by the full re-scan —
`_CanvasSortingOrder.md` row missing 「새 Canvas 추가 시 규칙」.

- **Granularity is H2.** That is what makes the audit decidable. It also draws the line for what counts as
  a defect: a row must carry every H2 that is a **rule block**. Sections that are reference data
  (`## 7. Human 종족 참조 정보`) are not systems and are not omissions.
- 🔴 **A row with no section summary of its own is the worst case.** `_CanvasSortingOrder.md` has no entry
  under 「시스템별 빠른 참조」 — only a one-line pointer inside the UI section — so the table row is its
  **only** description. Check which rows are singly-reachable before deciding what to skip.
- **Value copies in this table: delete the count, keep the name, add one short trailing pointer.** The row
  is a one-liner, so use the form the `_RandomMap.md` row already established:
  `… 검증 (유형 목록과 개수는 3장 「유형별 사양」이 단일 소스)` — one parenthetical at the end of the row,
  never one per item.
- **A number inside a cited section title is not a copy.** 「스킬 건물 3종 정의」 is the section's own name;
  quoting it verbatim is correct even though it contains 「3종」. Likewise `3×3` in 「3×3 스킬 UI」 is the
  official name (규칙 9 「3×3 그리드 슬롯 배치」), not a stale-able count. Delete a count only where it is
  the index's own arithmetic (「확장 5종」, 「스킬 타입 3종」).
- 🔴 **Before calling a row's number a copy, check what the repaired section summary did with it.** The
  `_Upgrade.md` row's 「공/방/속」 and 「×10 스케일」 look like copies, but §9's repair deliberately kept both
  in the summary and moved only the *scope* to 규칙 1. The table matching that decision is consistency,
  not a defect. Measure the sibling path before editing.
- **When the two paths disagree, fix neither alone.** The three `_AI_Scenario_*.md` rows omit four H2s each,
  but the section summaries omit exactly the same ones — widening only the table would split the paths.
  That is a one-batch decision and needs its own approval (CLAUDE.md 규칙 6·12).
- `check_docs.py` is blind to this whole class, exactly as in §9: it never asks whether a row represents
  its document. 0건 before and after tells you nothing here.

---

## 11. Writing a `_Tasks/` Research document (2026-09-03, random-map phase 2)

The task was **one `Research.md`, no `Plan.md`** — the caller reviews the research first and delegates the
plan separately. Four things about that shape are worth keeping.

### `check_docs.py` does not validate the document you just wrote

The checker's scan set **excludes `_Tasks/` and `_Logs/`** (it prints this in its own `[검사 범위]` block).
So running it after writing a Task document confirms **the rest of the repo is still clean** — it says
nothing about the new file. Report it that way. Claiming "0건 확인" as if it covered the new document is the
same class of vacuous pass as §3's `## 규칙 N.` case: a green result over a file that was never read.

- Corollary: a Task document may cite rule numbers and file paths that the checker will never verify.
  Verify those citations by hand, and say in the report that they were verified by hand.

### Every code line number in the document must be re-read before finishing

Writing `파일:행` from the memory of an earlier read drifts. In this round **6 of the cited ranges were off**
(a method's opening line vs. its body, a comment block's first line vs. the line that actually carries the
sentence, a two-line condition cited one line late). The fix is mechanical: after the draft, `sed -n` every
cited range and correct it. `.claude/mistakes.md` 2026-08-24 「행 번호로 위치를 가리켜 전부 어긋남」 is the
same failure — the lesson here is that *drafting from memory* is where it re-enters.

- Prefer a **range that contains the whole construct** (`:262~278` for a method) over a single line when the
  point is "this function does X", and a **single exact line** when the point is "this line says X".

### Judging whether two handed-over coordinate notations mean the same thing

Never compare the numbers. **Convert one side with the project's own conversion function and compare in one
system.** Here a runtime log gave `Q=5,R=16` and the rule document gave `(5,19)`; running the offset→cube
function from `Domain/Hex/HexGrid.cs` on the coordinate the bootstrap code actually builds reproduced the
log value exactly, which proved the log was cube and the rule was offset — and that the real mismatch was a
different grid height, not a wrong coordinate. Then re-derive the rule's own formula from that function to
show the rule is implementable with existing code.

- The generalisation of 「인계 수치는 논지까지 틀릴 수 있다」(index file, 2026-08-24): a handed-over figure can
  be **right and still not mean what the sender thought**, because it is in a different unit or frame.
- A configuration value read from a `.asset` is only authoritative once you show the scene actually
  references *that* asset: grep the asset's own GUID (from its `.meta`) inside the `.unity` file.

### 🔴 Before presenting options, check whether the question is already answered

**The original version of this section is kept below, struck through, because the round it came from is
exactly the failure it should warn about.** It recorded "how to lay out two options" as the lesson. The
real lesson is one level up: **that round should never have had two options.**

What happened (2026-09-03): the caller's brief said "present the impact of each option, do not decide" about
the map grid size. The brief was wrong — the grid was settled at 11×21 across six documents, one of which
calls it 「현재 도입 맵」, and a 2026-08-26 round had already measured 400 direction pairs and 126 of 231
tiles on that assumption. The caller had not read the 650-line rule document; it worked from greps and from
this agent's own earlier reports. So a settled specification was handed back to the user as an open choice,
and ~30 lines of a 388-line `Research.md` were written on that false premise.

- **A brief that asks for options is a claim to verify, not a fact to execute** (`.claude/MEMORY.md` A-2, the
  same rule that governs a "deprecated" label). Before laying out option A and option B, grep the rule
  documents for the thing being decided. If a rule document states it — especially in the present tense, or
  with a downstream measurement built on it — it is decided.
- **Say so instead of complying.** Answer: "이건 이미 정해져 있습니다 — `<파일:행>`. 미결이 아니라 코드가 안
  따라온 것입니다." That reply is worth more than a well-formed comparison table.
- The distinguishing question is **「문서 vs 문서」인가 「문서 vs 코드」인가**. Two documents disagreeing is a
  real open question. A document disagreeing with code is **미구현** — the document already decided.
- This is the same failure as the 2026-09-02 「문서에만 남겨놓고 미루기」 the user called out, with the sign
  flipped: there, a settled item was deferred; here, a settled item was reopened. Both come from not
  checking where the decision is written down.

~~**When the brief says "present the impact of each option, do not decide", the shape that works is one
table per option with the same five rows — 파일/문서 변경 · 즉시 따라오는 변화 · 회귀 위험 · 얻는 것 ·
미확인 — plus one closing line stating that options outside the two given were not examined. Symmetric rows
let the reader compare; the closing line stops the reader from assuming the space was exhaustively
searched.**~~

→ The table shape itself is still fine **once the question survives the check above.** Keep it for genuinely
open choices; do not let it become the reflex answer to any brief that says "options".


---

## 12. Recording the *results* of a multi-stage Plan (2026-09-08, random-map phase 2, stages F~J)

### Filling a `§9 실행 기록` table: the row is a summary with a pointer, not the record

The document already showed the shape at stage E — **one dense table row + one `### <단계> 단계 상세 (날짜)` section
appended at the end.** Follow it. A row that tries to hold everything makes the table unreadable, and a detail
section with no row leaves the table looking unfinished. What belongs in the row: status label, commit, the one
🔴 fact that changes how the stage is read, the headline measurement, and `상세는 아래 …` .

- **Status labels are not interchangeable.** This round used four distinct ones and the distinction is the point:
  `✅ 구현 완료 (**mono 로 실제 컴파일·실행 검증**)` / `✅ 구현 완료 · **실기 검증 완료(사용자 테스트)**` /
  `✅ 구현 완료 · ⚠️ **부분 실기 검증**`. Headless execution is not user verification, and neither is compilation.
- **When execution order differed from the plan, say so in *both* rows** (the one that ran early and the one that
  ran late), with the reason, plus once more at the top of the detail section. One mention gets read as a typo.
  Keep the table in the *plan's* letter order (F G H I J) — reordering the table to match execution destroys the
  mapping to §3's dependency graph.
- Detail sections may be appended **out of letter order** to mirror execution (J before I here) as long as the
  section header says why.

### Marking a superseded design without deleting the plan text

The brief was: the plan's design was scrapped; keep the original, add the fact. The shape that worked:

1. **In the original row, append a marker only** — `🔴 **[YYYY-MM-DD 폐기 — 원문은 그대로 둔다. 아래 「…」 참조]**`.
   The original sentence is not touched, so a later reader can still see what was planned.
2. **Right after the table, a blockquote note with four fixed parts**: 계획 / 실제 / **왜 갈렸는가** / **따라 바뀐 것**.
   The 「왜」 part is the whole reason the note exists — "the plan weighed X, the implementation weighed Y" —
   because "폐기됨" alone tells the next person nothing.
3. 🔴 **A scrapped design leaves knock-on descriptions elsewhere in the same section.** Here the folder-layout code
   block still said `재생성용 원본 (윗절반 지정값, …)`, which the change also invalidated. **Put it in the note
   ("따라 바뀐 것") rather than editing the code block** — editing it would erase the plan text the brief asked to keep.
   Grep the section for the *claim* the old design made (「윗절반」), not just its name (this is §5's method applied
   inside a single section).
4. Close the note by naming what **did** hold — "표의 나머지 행은 계획대로 지켜졌다". Otherwise the marker reads as
   if the whole table were void.

### 🔴 "도달 불가" is a distinct verdict from "미검증" — and it has to be pinned in more than one place

A behaviour was implemented and measured headlessly, but **cannot be exercised in the running game** (the AI never
reaches the tiles the new rule governs, because the map guarantees its opening tiles are ordinary). This is the
kind of finding the next reader turns into "they forgot to test it".

- Write it as **「검증 누락이 아니라 도달 불가다」** — the negation first, in those words.
- **Always pair it with the condition that makes it reachable later**, plus the number that shows the code is not
  dead ("3라인형은 맵의 3분의 1이 빗금 — 시나리오를 맞추는 순간 곧바로 실전에서 쓰인다"). Without that second half
  someone deletes the code as unused.
- **Pin it in at least three places**: the stage's detail section, the status document's 과대 표기 금지 list, and the
  roadmap row for the phase. It is the single most misreadable line in the whole round.

### A stage-scoped count is not a wrong figure — check the sentence's scope before "correcting" it

Handed-over note said 자체 점검 is 12 (Domain) / 14 (project). The Plan already said **「자체 점검 9종」** at stage E.
That is **not a contradiction to correct** — the E sentence reads 「이번 단계의 자체 점검 9종」, i.e. scoped to E, and later
stages added more. **Leave it, and say in the new section why it is not being corrected**, or the next round will
"fix" it back and forth.

- Decide by re-reading the sentence for a scope word (「이번 단계의」 · 「그 시점」), not by comparing numbers.
- Verify counts yourself before writing them: `grep -rn "public static.*TryRunSelfCheck" … ` gave Domain 12 +
  Application 2 = 14, matching the handed figures, so both could be written as 실측.

### Status documents when a phase is *partly* done (the hard case)

The phase's main acceptance test passed, and the phase is still **not complete** (one lettered stage left).

| 문서 | What to do |
|---|---|
| `PROJECT_STATUS.md` | Prepend a paragraph headed **`**⚠️ 진행 중 (날짜) — … / 🔴 N단계는 아직 끝나지 않았다(X 단계 남음):**`** — not `구현 완료`. The 과대 표기 금지 list leads with the incompleteness, not with it buried at ⑤ |
| `ROADMAP.md` | 🔴 **Do not take the row down and do not mark it 완료. Narrow its scope in place** — relabel the priority cell (`2단계 — A~J 완료 … · ⚠️ K 남음`) and append what remains. The document's own rule is 「완료된 항목은 남기지 않는다」, so leaving an unnarrowed row is as wrong as deleting it |
| `WORK_HISTORY.md` | A row per *event*, ascending by nothing — the table is 날짜 역순, newest at top. Two changes landing the same day that are genuinely separate concerns get **two rows**, not one merged row |

- **Also check the roadmap rows for *later* phases.** Stage I here absorbed items ①② that the 3단계 row still listed
  as its own. Append 「①②는 N단계에서 처리 완료」 to that row — otherwise two rows in the same table claim the same work.
- **The list of deferred items belongs in the Plan, not in the status docs.** Put it there as a numbered table
  (`## 13. ⏸ 완성 후 점검 대상`) with a closing line saying they are **「미해결 결함이 아니라 사용자가 범위 밖으로 확정한 항목」**,
  and give the status docs a count plus a pointer. Naming which of them will actually bite ("1·3·8 은 그대로 두면 실제
  문제가 되는 성격") is what makes the table usable next round.

### Repairing a stale description depends on the document's role, not on the sentence

Four spots described toast behaviour that a later commit changed. **Three different repairs, decided by role:**

| Spot | Role | Repair |
|---|---|---|
| `WORK_HISTORY.md` 2026-05-16 행 | dated history | **원문 유지 + `**[🔴 날짜 정정 — 원문은 그대로 두고 덧붙인다: …]**`** (memory rule 7) |
| `PROJECT_STATUS.md` dated 완료 표 | current state *inside* a dated block | same append form — the block is dated, so overwriting would falsify the record |
| `UIGuidelines.md` 분류 표 | living guideline, undated | **직접 교체** + pointer to the rule that owns it |

- 🔴 **A row whose scope is narrower than the stale claim may not be stale at all.** 「수동 생산 실패 토스트 3종」 stayed
  correct because its scope *is* the three production-failure keys. The repair there is a **pointer to the owning rule,
  and deliberately NOT writing the new total** — writing "현재 6종" would have created exactly the count-copy problem
  §6/§9-2 are about. Say so in the note (「개수는 여기에 적지 않는다 — 늘어나면 조용히 낡는다`」) so it is not re-added.


---

## 13. Closing a phase that the *same day's* records call unfinished (2026-09-08, random-map phase 2, stage K)

The previous round wrote "A~J done, **K left**, so phase 2 is NOT complete" into four documents. This round K
landed. The work is not "change 미완 to 완료" — each document needs a different repair, and one of them loses a
row that was carrying a warning the other documents depend on.

### The four repairs are not the same edit

| 문서 | Repair |
|---|---|
| `Plan.md` §9 | Fill the `⏳` row (dense row + `### K 단계 상세` appended before `## 12.`, §12's shape). The **I row's status label** also changes — see below |
| `PROJECT_STATUS.md` | The 진행 중 paragraph is **the same day's own record**, so do not rewrite it: swap the heading line to `✅ 구현 완료 …`, add a blockquote right under it saying what the heading used to say and why it changed, and put ~~취소선~~ + `**[🔴 해소 …]**` on the 과대 표기 금지 item that claimed the phase was unfinished. The `**현재 단계:**` opening sentence is a *second* place that says 미완 — it is easy to miss |
| `ROADMAP.md` | 🔴 The row comes **down**. The document's own rule is 「완료된 항목은 남기지 않는다」 and the previous round only kept the row because the phase was partly done. Prepend a new dated paragraph saying the row was removed and where the history/state went, and mark the previous round's top paragraph as "written before K" instead of deleting it |
| `WORK_HISTORY.md` | A **new row** for the stage (separate event, separate commit), and on the existing row **append a marker after its title** — `**[🔴 날짜 갱신 — 원문은 그대로 두고 덧붙인다: … 같은 날짜의 「…」 행 참조]**`. Do not edit that row's own claim; it was true when written |

### 🔴 Before deleting a roadmap row, re-home what it was pinning

§12 says 「도달 불가」 must be pinned in **three** places, one of them the roadmap row for the phase. Taking the
completed phase's row down **removes one of the three pins**. Check the surviving rows first: here the 3단계 row
already carried 「①의 AI 경로는 실기 도달 불가」, so the pin survived and was strengthened in place. **If no
surviving row carries it, move it before you delete, not after.**

- The same check applies to every other clause the deleted row was the only home of (여기서는 보류 8건 포인터 →
  the new top paragraph took it).

### Splitting a "부분 실기 검증" verdict — both halves stay visible in the status cell

Half of the verdict closed (the mining-post exception was exercised in the running game), half did not (the AI
path is still 도달 불가). Writing 「✅ 실기 검증 완료」 would erase the second half; leaving 「⚠️ 부분 실기 검증」
would hide the first.

- **The cell carries both**: `✅ 구현 완료 · ✅ **채굴소 예외 경로 실기 검증 완료(사용자 테스트)** · ⚠️ **AI 경로는 도달 불가 — 실기 확인 불가**`.
- In the 비고, append a dated marker that names **what the label used to say**, what closed, and — in the same
  sentence — that the remaining half **is not a missed test**. Pair the closed half with the count that shows it
  is a real path (1,800판 중 416건), so the closure is not read as anecdotal.
- The §12-(2) 「도달 불가」 block gets a one-line **재확인** note, not an edit: this round did not change it.

### 🔴 Retracting a paragraph of a commit message

Commit messages cannot be edited once pushed, so a wrong claim inside one keeps misleading readers forever. The
fix is to put the retraction **where a reader of that commit lands**: the Plan section that names the hash, and
the `WORK_HISTORY` row that names the hash. Say in the retraction itself that the message cannot be fixed and
that is why it lives here.

- Write **why the claim was wrong**, not just that it is withdrawn — here: the rule governs *what the loading
  screen draws*, and six lines later the same values are *required* in the log, so the rule already split
  「화면에는 안 그린다 / 로그에는 남긴다」 and there was never a conflict.
- Add the **residual-risk** assessment separately from the retraction, and 🔴 **label the parts that are general
  knowledge rather than measured in this project** (안드로이드 logcat 권한) — otherwise the retraction itself
  becomes the next unverified claim.

### An unexamined observation is recorded as excluded-path + unknown, and it becomes a roadmap row

The console had 5 errors / 4 warnings that nobody looked at. What is provable is only that the *map* keys were
not among them (the run logged `MapPreparationSucceeded`, terrain and mining posts worked).

- Write it as **「맵 경로는 배제되지만 정체는 미확인」**, explicitly **not** 「맵과 무관함이 확인됐다」 — the
  negation belongs in the sentence, the same way 「도달 불가」 does.
- It is **future work**, so it also gets a `ROADMAP.md` row (🟡 중간 **미확인**), not just a caveat in the status
  document. Caveat lists say what is not done; the roadmap says what someone will do about it.

---

## 14. A Research document that is mostly *measurement*, and the traps in it (2026-09-08, random-map phase 3)

Same shape as §11 (one `Research.md`, no `Plan.md`), but this round the value was in **numbers measured from
binary assets and scene files**, not in reading prose. Four things generalise.

### 14-1. A binary asset is the cheapest authoritative measurement available

The brief asked "how big is the map payload, really?" — the answer was five `.bytes` files in
`Assets/_Project/Resources/MapTemplates/`. Their **file size is the payload size**, because the codec writes
canonical bytes and nothing else. But do not stop at `ls -la`: **decode them with the codec's own field
order** (a short Python script mirroring `Encode`) and assert `consumed_offset == len(file)`. That one
assertion turns "the file is 343 bytes" into "the format parses to the end", which is what lets you derive a
size *formula* (`319 + 4N + 20D` here) and check it against two different files. Sizes alone would not have
caught the next item.

> **[🔴 2026-09-15 — the paragraph above is kept exactly as written; the lesson still holds and only the
> example's numbers have moved on.]** The format that example was measured against was **`MapVersion = 1`**.
> Deleting the map test mode took a fixed-width 4-byte `TestModeFlag` out of the canonical byte stream
> (commit `8c83317`), so **`MapVersion` is now `2` and the current formula is `315 + 4N`** — the constant
> term dropped by exactly 4 and **the `4N` term is unchanged**. The regenerated fallback templates measure
> Canyon **331** and FullyOpen · ObstacleOpen · Outer · ThreeLane **339** each (previously 335 / 343), and
> the 2026-09-15 device run matched `315 + 4N` on all 9 multiplayer rounds. ⚠️ **The `20D` decoration term
> was not re-measured** — decoration lists are still always empty, so no file in this round exercised it;
> do not quote that part as verified for `MapVersion = 2`.
> 🔴 **This is itself the lesson working as intended**: a formula goes stale the moment the format changes,
> which is exactly why the method is "decode with the codec's own field order and assert to the end" rather
> than "remember the number".

- Then compare against the **configured limit read from the scene**, not from memory: `m_MaxPayloadSize` sits
  in `Lobby.unity` / `Game.unity` as plain YAML. Both scenes must be checked — they can differ.
- 🔴 **A configured limit is not an effective limit.** Say so in the document and push the real measurement
  to the Plan. Transport headers eat into it, and Relay is a different number from localhost.

### 14-2. 🔴 An existing design document's arithmetic can be wrong — recompute, don't quote

`TechnicalDesignDocument.md` carried "약 274바이트 (타일 231 + 헤더 19 + 성·광산 24 + 장식 0)". Measurement
gave **335~343**. Two independent errors: the header was counted as 19 when `Encode` writes 40, and the four
list-length `int32` fields (16 bytes) were omitted entirely. The document itself had labelled the figure
*"파이썬 계산이며 실기 측정이 아니다"* — so it was an arithmetic slip, not a false claim.

- **Report which conclusions survive.** Here the conclusion built on it ("조각이 1개뿐") is still true at 343,
  so only the supporting number is wrong. Saying "the number is wrong" without saying "the conclusion holds"
  invites someone to redo settled work.
- **Do not fix the other document from inside a Research task.** Record it, name the two causes, and hand the
  decision back. This is the `_Tasks/` counterpart of the index file's 「인계 수치는 논지까지 틀릴 수 있다」.

### 14-3. Extracting an "undecided" list is a *counting* job with a conditional tail

The brief asked how many planning decisions the user must make. Reading the two rules
(`GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 M-3·M-4) yields **4** ⚠️ markers — but one of them says *"팝업으로
확정되면 규칙 8 에 따라 팝업/모달 타입을 함께 정해야 한다"*. That is a **conditional fifth and sixth decision**
that exists only under one answer.

- Present it as **「확정 4건, 한쪽으로 정해지면 +2건」**, not as 6. A flat count of 6 overstates what the user
  must decide today; a flat 4 hides work that appears the moment they answer.
- Pair each undecided item with **whether existing UI assets can express the answer** (measured: `ConfirmPopup`
  has exactly two buttons, `GameEndUI` exactly two). That converts an abstract question into a costed one —
  a three-choice restore has *no* asset today, and the user should know that before choosing.

### 14-4. A handed-over "there is no spec for X" claim splits in half more often than it holds

The phase-2 Plan's deferred item read *"맵 준비·투영 실패 처리 미명세 — 어느 문서에도 없다"*. Grepping `Docs/`
showed **preparation failure is fully specified** (규칙 16 + 규칙 M-2·M-3·M-4, including the 4 undecided
markers), while **projection failure genuinely has nothing** — `MapProjectionFailed` appears only as a
`LogRules.md` key definition and one roadmap line.

- **Split the verdict instead of accepting or rejecting the whole claim.** "절반만 그렇다" plus which half, with
  the grep that decided it. Same discipline as 「미검증 해소」 (index file, 2026-08-24): line up what the claim
  asserted, then show how far the evidence reaches.
- The generalisation: a deferred item written while a *different* phase was in flight often describes a state
  two rule-document revisions ago. **Re-grep every deferred item before carrying it forward.**

### 14-5. Scene files answer "what is scene-bound?" better than code does

The rule demanded a 「씬에 종속되지 않는 공용 전송 경로」. The decisive fact was not in any `.cs`:
`grep -c "NetworkObject" Lobby.unity` → **0**, `Game.unity` → **13**. Pair that with a one-pass classification
of every file in the network folder by *lifetime* (`MonoBehaviour + DontDestroyOnLoad` / plain C# / `static`
holder / scene-placed `NetworkBehaviour`) and the candidate list writes itself, each with its own cost.

- Confirm a single class's placement by **its script GUID from the `.meta`**, grepped in each `.unity` — the
  class name alone does not appear in scene YAML.
- When the deciding runtime behaviour lives in a package that is not on disk (`Library/PackageCache` absent in
  a headless checkout), **that is a §13 「확인 못 함」 row, not a guess.** Name what must be measured and where.

---

## 15. Replacing a 「미검증」 claim when the verification came back *partial* (2026-09-09, random-map phase 2 multiplayer)

Three documents said 「2단계의 검증은 전부 에디터 싱글플레이다 — 멀티는 실기 미검증」. A multiplayer run finally
happened, and the claim became **half false**. The deliverable of this round was not "change 미검증 to 검증" — it was
**the sentence that draws the boundary**, written once and pointed at from everywhere else.

### 15-1. The boundary sentence is the artifact; give it one home and point at it

Write it as a ✅/⚠️ pair, and put the ⚠️ half **first in the reader's mind** by naming the negation:
「**「매 판 다른 맵이 나오는가」는 이번에도 확인 불가다 — seed 가 고정이라 구조적으로 확인할 수 없다**」.

- **Structural impossibility is not a missed test** — same shape as §12's 「도달 불가」, and it is written the same
  way: the negation in the sentence, plus the condition that makes it reachable later (here: 3단계's seed 전송).
- **Degrade the strength of what *was* confirmed, in the user's own words.** The user wrote 「빗금 105칸을 세어본 건
  아니고 같은 맵이 나온 것만 확인했어」 — so the document says **「두 화면이 같은 맵임을 눈으로 본 것이지 칸 단위
  대조가 아니다」** and quotes them. Never upgrade an eyeball check into a comparison.
- Name **one** document as 「경계의 단일 소스」 (here `PROJECT_STATUS.md`'s new dated paragraph) and have the roadmap,
  the history row and the Plan section point at it. Four copies of a boundary drift apart within one round.

### 15-2. Four documents, four repairs — again, decided by role (extends §12/§13)

| Spot | Repair |
|---|---|
| `PROJECT_STATUS.md` dated 완료 문단의 **머리** (「… / 멀티 미검증」) | Leave the heading, **append a bracket marker** saying the heading was true on its date and is now partly resolved, plus where the boundary lives |
| the same block's 과대 표기 금지 **항목** | ~~strikethrough~~ + `**[🔴 날짜 부분 해소 — 원문은 그대로 둔다]**` + what closed and what did not. **「부분 해소」, never 「해소」** |
| `ROADMAP.md` **row for the next phase** | The row stays (the phase is future work). **Narrow it in place**: say which sub-item is still open, which closed, and 🔴 **what verification now remains** (here ㉠ 매 판 다른 맵 ㉡ 칸 단위 대조). A row that only says 「미검증」 after a partial run is now wrong |
| `WORK_HISTORY.md` | New dated row for the run, and a marker appended after the **title** of each older row whose caveat list this changes. Do not edit those rows' claims |

- The Plan that owns the phase gets a new `## 14. …` section holding the full record (§12's shape), and the
  status documents carry only the boundary plus a pointer.

### 15-3. 🔴 A "correct behaviour that looks like a bug" is recorded like 「도달 불가」

The user reported 「토스트가 안 뜬다」; the rules (`GameSystemRules_UI.md` 「건물 배치 패널 UI」 규칙 7·8) only toast on a
**self-owned** tile, and at match start **no team owns any hatched tile**. It appeared after they captured one.

- Write it with the negation and the mechanism: **「점령한 뒤에만 뜬다 — 결함이 아니라 규칙대로의 동작이다」**, then the
  measurement that makes it non-anecdotal (초기 소유 12칸 중 빗금 0 · 맵 전체 105칸 전부 중립).
- **Say which known finding it is structurally identical to** (here 「AI 가 빗금 타일에 닿을 수 없다」). Two findings
  sharing one cause should be linked, or the second gets "fixed" independently.
- Pin it in **three** places for the same reason 「도달 불가」 is pinned three times: status doc, roadmap, Plan section.

### 15-4. A side finding the user did **not** put in scope — record it, and do not give it a roadmap row

Measurement showed the map's initial gold is never applied (screenshot 5135; the table value for that map is 500;
`GameConfig.StartingGold` 5000 is what runs). It matters because a rule (`GameSystemRules_RandomMap.md` 규칙 16)
requires both sides to apply that value — **so skipping it leaves that rule unmet**, and saying so is the point.

- 🔴 **A `ROADMAP.md` row *is* a scoping decision** (「누군가 이것을 할 것이다」). When the user neither included nor
  excluded the item, a row overstates it. Record it as a **fact paragraph** in the roadmap's dated top block, the
  status document, and the Plan's deferred-item row — and write **「범위에 넣지 않고 사실만 적는다」** explicitly, with
  「하지 말라인지 이번엔 아니다인지 확정되지 않았다」. That wording is what keeps the next round from either
  silently dropping it or silently adopting it.
- Pair it with the rule it breaks. 「미사용이다」 alone reads as tidy-up; 「규칙 N 이 미충족으로 남는다」 does not.

### 15-5. A test-only constant change needs four sentences, and one of them guards a distinction

A temp fixed seed moved `1` → `11` (one constant + comments). What the record must carry:

1. **What changed, measured** (file + line + the literal), and that the branch structure did not.
2. 🔴 **That the number itself means nothing** — chosen because the map it produces is *useful for testing*, not
   because it is balanced. Otherwise the next reader treats it as a tuned value.
3. **How long it stays and what removes it** (here: kept while phase 3 transport is built, deleted by phase 3).
4. 🔴 **The distinction the change can blur.** Single-player was *always* random (`Guid` + `UtcNow`); only
   multiplayer is fixed. Without that sentence the docs read as 「무작위 맵인데 왜 고정이냐」. Measure both branches
   in the same function before writing it.

---

## 16. Closing a whole phase whose execution table was still all-⬜ (2026-09-14, random-map phase 3)

Nine stages A~I landed at once and the `§14 실행 기록` table had to be filled in a single pass, not stage by
stage as §12 assumed. Six things generalise, and three of them are about **what the handoff did not say**.

### 16-1. 🔴 A stage that was never started, and was *downgraded* rather than skipped

Stage A ("does a dynamically spawned `NetworkObject` survive a `LoadSceneMode.Single` reload?") was the plan's
**first stage and a declared precondition for B**. It was never run — and the reason is not negligence: the
design settled such that **the condition never applies in this scope** (transfer finishes in the lobby before
the scene load, and the confirmed map crosses the scene boundary in a static holder, not in an object).

- Keep the row at `⬜ 미착수` — do **not** invent a ✅. The label set is fixed; write the reason in 비고.
- The row must carry three things or it reads as a skipped step: ① **why the condition did not apply here**
  ② **where it *will* apply** (a named future item — here the rematch row) ③ a pointer to the detail section.
- 🔴 **Also close the plan's own "ask the user when A lands" slot** (§15 표 1번 here). The question never
  became askable, and saying so prevents the next round from re-opening it.
- The detail section for a not-started stage is still worth writing: it is the only place that records that
  the blocker was retired *deliberately*.

### 16-2. 🔴 The stage shipped, ran in 실기, and its own measurement items are still unmeasured

Stage B's plan named two 실측 항목 (NGO effective payload cap, Relay effective MTU). The code shipped, the
transfer ran twice in a real match — and **neither number was ever measured.** The handoff did not mention it.
Reading the source found `IsChunkSizeMeasured = false` and `ProvisionalChunkSizeBytes = 1024` with "근거가
없다" written in the comment.

- **Read the new files themselves before filling a row from a handoff.** A constant named `Provisional*` or a
  `Is…Measured = false` flag is the code telling you the stage is not finished; no handoff note will.
- The status cell carries **both halves** (§13's split): `✅ … 실기 검증 완료 · ⚠️ 부분 검증 — 한도 실측 2건이 남았다`.
- 🔴 **Pair it with the rule clause that stays unmet.** 규칙 16 says the value is fixed by NGO measurement and
  recorded in the TDD; until then that clause is 미충족. "아직 안 쟀다" alone reads as tidy-up.
- ⚠️ **A fact that makes the gap harmless is not a fact that closes it.** Here the payload is always one chunk,
  so the provisional size never bites — say that, *and* say it does not make the provisional value final.

### 16-3. Structural non-firing, again — and the ratio that makes it readable

Four of five new log keys never fired in the real run. They are **all failure keys and both matches succeeded**.
Same shape as §12's 「도달 불가」 and §15-1's structural impossibility: **negation first**, then the condition
that would make them fire (an artificial mismatch), then **who has not decided that yet** (the plan's own
open question). Write the ratio (「5종 중 1종」) in the cell so the reader sees the scope at a glance.

### 16-4. Where a *new* phase's deferred items go when the old list is a dated snapshot

The previous phase's list was headed 「2026-09-08 시점 — 사용자가 이번에 처리하지 않기로 보류한 항목」.
Appending phase-3 findings there would make **the heading lie about time** (the failure mode of the index
file's 2026-08-24 note: 「제목이 시간에 대해 거짓말하는가」).

- **New section in the current phase's Plan** (`## 16. ⏸ …에서 새로 드러난 보류·미해결 항목`), opened with a
  blockquote saying **why it is not in the old table**.
- **In the old table, touch only the rows whose state actually changed** — here 1 (closed by this phase) and
  2 (still open, and now *confirmed* to leave a rule unmet). Append markers; never edit the original sentence.
- **Add one pointer line under the old table** naming the new section and the count, plus 「1번은 닫혔고 2번은
  닫히지 않았다」. Without it the old list stays the place people look and the new one is never found.
- Lead the new section with a **kind column** (결함 아님 / 손봐야 함 / 사용자 확인 대기 / 규칙 미충족) so the
  reader does not have to read six items to learn which one bites.

### 16-5. 🔴 A narrowed row is the home for leftovers you found yourself — do not open new rows

§15-4 says a `ROADMAP.md` row is a scoping decision, so items the user neither included nor excluded get a fact
paragraph, not a row. But this round *also* produced leftovers of the finished phase (the two unmeasured
limits, the un-fired paths). Opening rows for those would scope them; dropping them would lose them.

- **Resolution: fold them into the phase's own row while narrowing it.** The row stays because the phase is
  partly done (§12's rule), and the narrowed text enumerates everything that remains — including what you
  discovered — under one heading. No new rows, nothing lost.
- Write the narrowed row as **「현재 이 행에 남은 것은 N가지다」 + ✅ 이번에 닫힌 것** and keep the original
  registration text below it with 「아래 원문은 등록 당시 기록이라 그대로 둔다」.
- Items the user explicitly told you to record as 보류 (here: a lobby-logging gap, an uninvestigated UI report)
  stay **out** of the roadmap even when they look actionable — the instruction 「보류로 기록하라」 is itself the
  scoping answer. Say in the report that you chose not to open rows, and why.

### 16-6. Handed-over figures were wrong twice, and a third class was simply unverifiable here

- Two measured disagreements (a file's line count 1500 → **1607**; a method's range `:447~492` → **`:447~497`**).
  Write the measured value, and keep a **two-column 대조표** (인계값 / 실측) in the Plan's 근거 구분 section so
  the next round can see which figures were re-measured rather than copied.
- 🔴 **A third class cannot be measured in this checkout at all**: Unity-side artifacts. The prefab and its
  `DefaultNetworkPrefabs.asset` registration are not here, and the new `.cs` files have **no `.meta`** — the
  agent-authored files never went through Unity. Write **「확인 불가」, never 「없다」**, name the indirect
  evidence that they exist (the real run logged a successful spawn with `NetworkObjectId=1`), and put it under
  the handed-over column of 근거 구분.
- The cheap check that decides this: `ls` the asset path plus `find Assets -name "<Class>*"`. If the `.cs` is
  there and the `.meta` is not, the whole Unity half of that commit is outside this working tree.

## 17. Recording facts and decisions that correct *your own* previous round (2026-09-14, random-map phase 3 follow-up)

The handed-over round was **not** an implementation. It was four facts and decisions about one number (5000)
and one feature (map test mode). Nothing was built; the deliverable is **what the documents now say**.
That shape has its own rules, and this round taught five of them.

### 17-1. 🔴 The correction was not a wrong number — it was a **sentence that invited the wrong reading**

The previous round wrote *"the map's `InitialGold` differs every match but the actual gold is fixed at 5000"*.
**Every word of that is true.** It still had to be corrected, because it **omitted where 5000 comes from**, and
a reader lands on the nearest explanation available — the map test mode sitting right next to it in the docs.

- This is a **different class from §12's superseded design and from 2026-08-20's numeric correction.**
  Nothing was false, so there is nothing to strike. **B-7 still applies: append, never rewrite** —
  the repair is `**[🔴 YYYY-MM-DD 정정 — 원문은 그대로 두고 덧붙인다: …]**` carrying **the missing half**.
- **How to spot this class before a user does:** for each 「X is wrong / X is missing」 sentence, ask
  **「does this sentence say where X came from?」** If not, the reader will supply a source, and the source
  they supply is whatever the surrounding document talks about most.
- The repair block must say the **negation first** (*"it is NOT the test mode"*) — the same ordering §15
  found for ✅/⚠️ boundary sentences. A reader who stops after one clause must stop on the correction.

### 17-2. 🔴 Two fields whose values are **coincidentally equal** — record both halves or the note is useless

`Economy.StartingGold` = 5000 and `TestStartingGold` = 5000 are different fields that happen to match.
The code comment already warned about it (`GameConfig.cs:160~162`); the **documents** had never said so.
Writing only *"they are different fields"* is half the job. The two consequences are what a reader needs:

1. **Today**: turning the test mode on changes nothing on screen, so the next person concludes
   **「the test mode is broken」**. Name that misreading explicitly — it is the whole reason the note exists.
2. **Tomorrow**: the moment the planned release change lands (5000 → 500), **the coincidence breaks**
   and the two values diverge. A note that only describes today goes stale exactly when it matters.

> Generalisation: when a doc records **「A and B happen to be equal」**, it must also record
> **「what that hides now」** and **「what scheduled change breaks it」**. Otherwise it is trivia.

### 17-3. A decision recorded but **deliberately not executed** — the impact scope *is* the record

The user decided to delete the map test mode and **decided not to do it this round**. So the artifact is
「결정됨 · 다음 작업」 plus the scope. 🔴 **A file list is not a scope.** The parts that actually decide
whether the work is a morning or a week were the **cascading** ones, and they came from reading the code:

- the flag sits **inside the canonical byte stream** (`MapDefinitionCodec`) → **the wire format changes**
- → the format **version constant must be bumped** (its own comment says so)
- → **binary assets encoded in the old format must be regenerated** (`Resources/**/*.bytes`)
- → ⚠️ **a previously-completed real-device verification becomes void** and must be re-run.

That last bullet is the one nobody volunteers. **When a change alters a serialized format, always ask which
already-closed verification was measured against the old format** — hashes, byte counts, round-trip restores.
Otherwise the next round says 「we already verified this」 about bytes that no longer exist.

- 🔴 **Rule documents come first.** If a rule document *defines* the thing being deleted (here 규칙 3·12),
  say so in the scope as a **precondition**, not a follow-up: deleting the code first leaves rules and code
  contradicting each other, and the checker cannot see it (a rule with no references is still a valid rule).
- The **✅ freebie** belongs in the scope too — this deletion removes the second of two composition-root
  frictions. Pin it **on the friction item**, and pin **what does not go away** next to it, or the next reader
  closes the whole item.

### 17-4. 🔴 A roadmap row that a previous round **refused to create** — overturn the refusal explicitly

`ROADMAP.md` said, in words, *"we are not creating a row because the user has not settled whether this is
「don't」 or 「not now」"*. That sentence is a **record of a decision**, not a stale line. When the user then
settles it, the repair is **not** to quietly add the row.

1. Add the row.
2. Go back to the refusal sentence and **append** the overturn: what changed, what the user actually said,
   and that the row now exists. Leave the original.
3. Say in the new top paragraph **why the row is justified now** — here: this document's own stated role is
   「앞으로 해야 할 작업」, and the only thing that had blocked it was the missing decision.

> §15 established that a side finding the user neither included nor excluded gets **no row** (a row is itself
> a scoping decision). **17-4 is the same rule read forwards**: once the user *does* scope it, the row is
> owed — and the earlier refusal is the evidence that the scoping decision is what changed, not your opinion.

### 17-5. 「확인했고 문제없음」 earns a **Plan item** but never a roadmap row

The multiplayer gold-mismatch suspicion turned out to be correct behaviour (server-authority `NetworkVariable`
converges the client onto the host's value). Recording it is worth real space — **the suspicion is cheap to
re-raise**, because the construction site reads as if both sides build their own. So write **the shape of the
suspicion first**, then the evidence that dissolves it. That is the same form as 「도달 불가」(§12) and
「correct behaviour that looks like a bug」(§15).

- **But it gets no roadmap row: there is nothing to do.** Say that in one clause so nobody adds one later.
- ⚠️ **And say what it does *not* resolve.** Converging on `Economy.StartingGold` is not the same as applying
  the map's value — two adjacent facts about the same number, and merging them would close a real gap by
  accident. **When a ✅ item sits next to a 🔴 item about the same subject, each needs a sentence saying the
  other is untouched.**

### 17-6. Lettered subsections: append **after** the meta section rather than renumber

§16 ran 가~바 with **사 = ※ 근거 구분** (a meta note covering the whole section). Two new items had to go in.
Renaming 사 would break every pointer to it; inserting 아·자 before it breaks 가나다 monotonicity.
**Appending 아·자 after 사 keeps every existing letter untouched** — the only property that actually matters —
so do that, then add one line under the summary table explaining why the meta note is not last.
Each appended item carries **its own 근거 구분** instead of relying on the earlier one.

### 17-7. Handed-over count mismatch, third variant: **the unit was wrong, not the number**

Handed over as *"12 files · 89 reference lines"*. Files measured **12 — exact match**. Lines measured
**115 with comments / 95 excluding comment-only lines** — neither is 89.

- 2026-08-18 was *the number is wrong*; 2026-08-24 was *the number is right but the conclusion is not*;
  this is **the number counts something I cannot reconstruct**. Two independent re-counts failing to land on
  the handed value means **the counting basis differed**, and the basis is not recoverable from the result.
- → Write the measured value, keep the matching half (**the file count agreed — say so**), and state
  **「확정할 수 없어 추정하지 않는다」** rather than inventing a basis that would explain 89 (규칙 10).
- Publish **both** of your own figures. A single "115" invites the same mismatch next round; "115 / 95 (basis)"
  lets the next person check which one they are reproducing.

## 18. Recording a defect that is **out of scope of the task that found it** (2026-09-14, rematch-map round 3)

One round, one artifact: a known defect the user explicitly said **「record it and leave it as future work」** —
no code, no fix. The whole job was **choosing where it lives**. Four things generalise.

### 18-1. 🔴 The placement question is decided by **what the defect is about**, not by who found it

Found while researching rematch **map** selection; the defect is in `GameEndUI` and fires **whether or not the
map changes**. Two failure modes sit on opposite sides:

- Filing it under the finding task (the rematch Research doc, or a map-phase Plan's deferred list) **buries it** —
  only someone doing map work ever opens those, and this defect is not map work.
- Scattering the same text across several documents **creates the divergence** the next editor has to reconcile.

> **Resolution: one home + pointers.** The home is picked by subject (here `ROADMAP.md`, because the user scoped
> it as future work and that is this document's stated role). The finding document gets **a pointer and nothing
> else** — say in the pointer *"the row is the single source; copying it here would split it in two."*
> Write the placement **rationale** into the new top paragraph, or the next round re-litigates it.

### 18-2. 「보류로 기록하라」 and 「나중에 작업할 사항으로 기록하라」 are **different instructions**

§16-5 says items the user told you to record as **보류** stay out of `ROADMAP.md` — the instruction is itself the
scoping answer. This round looked identical but is not: **「나중에 작업할 사항」 maps word-for-word onto the
document's own stated role (「앞으로 해야 할 작업」)**, so the row is owed (§17-4 read forwards).

- The distinguishing test is the **verb the user used about the future**, not how the finding arrived.
  「보류」 = set aside, no commitment. 「나중에 작업」 = committed, just not now.
- ⚠️ It also mattered that **there was no deferred-item list to put it in** — the Plan for this task did not exist
  yet and the user forbade creating one. When the 보류 home does not exist, saying so is part of the rationale.
- Say in the report which reading you took **and that the other reading exists**, so the user can overturn it
  cheaply (규칙 12). Do not silently pick one.

### 18-3. 🔴 Severity you cannot determine is **part of the record**, not a gap to fill in later

The handoff itself named two unchecked items. Both were kept verbatim in the row, each with **what it would
change if it went the other way** (「정리된다면 실질 피해는 …정도로 줄어든다」 / 「앞쪽에 누른다면 남은 20초가
충분할 수 있다」). That is what makes them actionable instead of decorative.

- Put the uncertainty **in the priority cell too** — `🟡 중간 (… · 🔴 심각도 미확정)`. A cell that reads only
  「중간」 is a claim you did not measure.
- A **fix direction** handed over as a suggestion stays a suggestion: name the rule that makes it plausible
  (here 규칙 M-3) **and** the existing method that makes it cheap (`RestoreRematchButton()`), then say in the
  same sentence that the unchecked items come first. Never promote it to a decision (규칙 1·11·12).
- 🔴 **Pair it with the neighbouring 미정 it shares machinery with.** Here §12-1 (countdown vs modal) and this
  row both touch the same coroutine — deciding them separately produces contradictory specs, so say so in
  **both** places.

### 18-4. 🔴 Re-measure the handoff **and your own previous round** — both were wrong, differently

Two mismatches, and the second is the one worth remembering:

| 출처 | 인계/기존 서술 | 실측 |
|---|---|---|
| 인계문 | `StopCountdown()` 4곳 = `Show`(:187) · … · `OnDestroy`(:342) | **개수 4 와 행 번호 4개는 맞고, 메서드 이름 2개가 틀렸다** (`:187`=`OnDestroy`, `:342`=`ReturnToLobby`) |
| 직전 회차의 **내 문서** | 「호출 3곳」 = `OnRestartClicked`·`Hide`·`OnDestroy`(:342) | **4곳** — `:187` 이 통째로 빠졌고 `:342` 의 이름도 오기 |

- This is a **fourth class** next to §17-7: *the count is right, the labels are not.* A line number is cheap to
  copy and hard to check; a method name looks like the verification but is not. **Verify the mapping, not the
  number** — `awk` back from the hit to the nearest enclosing signature and print both.
- 🔴 **The previous round being mine is not a reason to soften it.** B-7 applies unchanged: leave the original
  sentence, append `**[🔴 날짜 정정 — 원문은 그대로 두고 덧붙인다: …]**`, and **enumerate what survived
  re-measurement** (here: 30초 · scene value · `WaitForSecondsRealtime` · three of four line numbers).
  Without that list the reader cannot tell how far the correction reaches.
- Say **in the report** that one of the corrected figures was your own. The count of corrections is not the
  point; which documents now disagree with what they said yesterday is.

### 18-5. Appending an item to a closed numbered 요약 list

The Research doc's §13 summary ran 1~12. The new item goes in as **13, appended after 12** — never inserted
mid-list (the first attempt landed it between 11 and 12 and had to be undone).
Give it a `[날짜 추가]` marker, and 🔴 **say what neighbouring item it is *not* part of** — here item 11 counts
「문서·코드 어긋남 5건」, and this is a code-behaviour defect, so the 5 must not quietly become 6.

## 19. Revising a rule the user ordered changed, and repairing everything it falsified (2026-09-14, rematch-map round 4)

The user decided rematch always draws a new random map — no 「same map / new map」 choice. One round removed
six clauses from 규칙 14 and repaired every document that had been written on top of them. Seven things
generalise, and the first is the one that nearly went wrong.

### 19-1. 🔴 Before deleting a clause, grep for who *depends* on it — one half of a doomed paragraph was load-bearing

The handoff listed 「현재 맵 정의와 재경기 제안을 보관하는 재경기 컨텍스트는 … 로비 복귀 또는 연결 종료 시
폐기한다」 for deletion, reason: there is no 「제안」 and no 「재사용할 맵」 left. **True for the first half only.**
`grep -rn "규칙 14"` over `.claude/` found **two game-programmer memory files quoting that exact clause** as the
rule basis for shipped `MapHandoff.Clear()` wiring — and the confirmed map still has to cross the scene reload.

- **Resolution: narrow the sentence instead of deleting it** (drop 「재경기 제안」, keep the map definition's
  lifetime and discard timing), then **mark the deviation in the document itself** — an ⚠️ line saying this is
  where you departed from the instruction and that the user must confirm (규칙 12) — and lead the report with it.
- 🔴 **The cheap check that would have caught it:** grep the rule number *and* a distinctive fragment of the
  clause across `Docs/` **and `.claude/`**. Memory files cite rules by quoting them; a rule-number grep alone
  finds the citation, but only the fragment grep tells you *which sentence* they lean on.
- General form: **a paragraph is not the unit of deletion — a claim is.** Split the paragraph into claims and
  decide each; a paragraph that mixes a dead claim with a live one gets narrowed, never dropped.

### 19-2. 🔴 A removed clause can make existing code *compliant* — that belongs in the removal rationale

규칙 14 said 「양측이 서로 다른 mode 로 동시에 요청해도 자동 시작하지 않는다」, and the shipped
`RequestRematchServerRpc` starts immediately on the second request — recorded in three places as a rule
violation. Deleting the clause **closed the violation without touching code.**

- Write it in the removal row explicitly: *"this clause disappearing makes the current code match the rule"*,
  with the measured code path. Otherwise the next round re-opens a 「fix the violation」 task that no longer exists.
- Then go back to **every document that recorded the violation** and append the resolution there too — here the
  Plan's 「다음 범위 착수 시 반드시 함께 처리할 것」 list and the Research doc's §3-3. A resolution recorded only
  at the rule is invisible to the people reading the follow-up lists.

### 19-3. The removal-record block is the artifact — one home, a table of clause → why

Put a blockquote under the revised rule holding **every removed clause with its own reason**, and make every
other document point at it (「제거 조항과 근거의 단일 소스는 규칙 14 의 개정 블록」). Copying reasons into the
GDD/TDD/status docs splits them in two the first time one is refined.

- 🔴 **The hardest reason is worth its own sub-block.** Here: why the 「new candidate's hash equals the previous
  map's hash → discard」 clause went. Four steps, each measured: ① the check **cannot catch what the user was
  worried about** (identical terrain with a different seed still passes, because `MapVersion`/`RootSeed` lead the
  canonical bytes — `MapDefinitionCodec.cs:60~61`, read directly) ② in the normal path it therefore **never
  fires** ③ so the only path that *does* fire it is the fallback template ④ and a repeated fallback should
  **surface, not be retried away** — the Warn log key for that already exists, so *no work follows*.
- Note the shape: **negation first** (what the check does *not* do), then where it does fire, then the
  consequence. Same ordering §15/§17-1 found for boundary and correction sentences.

### 19-4. Four document kinds, four different repairs for the same falsified sentence

| Kind | Repair |
|---|---|
| Rule document (`GameSystemRules*`) | **Rewrite the text** + removal-record block. Rules are read as current state; a struck-through rule is a trap |
| 기획서 / 기술서 with a 개정 이력 table (GDD, TDD) | **Rewrite the body, add a NEW version row.** 🔴 **Never edit past rows** — they recorded what was true then |
| Status docs (`PROJECT_STATUS`, `ROADMAP`) | **Append `[🔴 날짜 갱신]` markers in place**, plus one new top paragraph; strike only the sub-clause that died (here a roadmap row's UI-asset sentence) |
| `_Tasks/` research record | **A new top section 「이 조사의 전제가 바뀌었다」 with a per-절 유효/무효 table**, then short markers on each affected 절. Never edit the findings |

- The research doc's table is the piece that pays off: **say for each section what survives**, not just what died.
  Here §8's measurements survived and *became the deletion's evidence* — the section is invalid as a problem
  statement and load-bearing as a fact. Both halves must be written or the next reader discards the measurement.

### 19-5. 🔴 The 「해소된 것」 list is as important as the 「제거한 것」 list

Four things stopped being work: UI assets (two of them), the request/accept flow change, single-player code
(zero, it already generated a new map every time — verified by reading `OnRestartClicked` → `LoadMap` →
`MapRootSeed.Create()`), and the rule violation of 19-2. **Every status document got that list**, because the
default failure mode of a scope reduction is that the removed work stays on someone's list.

- Pair it with **what did not get resolved** in the same breath — here 「재경기 맵은 여전히 미구현」. A revision
  that narrows scope reads as progress; say plainly that nothing was built.
- ⚠️ 「규칙을 현실에 맞춘 것이지 새 사양이 아니다」 is its own sentence, used wherever the doc now describes
  behaviour the code already had. Without it the single-player line reads as newly required work.

### 19-6. A rule *title* that contains the removed identifier

규칙 M-3 was titled 「멀티플레이 NewMap 재경기 실패」. Renaming it is right, but the title is quoted elsewhere
(a `ROADMAP.md` row cited 「규칙 M-3(… 멀티플레이 `NewMap` 재경기 실패)」).

- **Grep the old title string, not just the rule number**, and repair each citation by **appending** the new
  title plus 「규칙 번호와 인용한 조항은 그대로이므로 논지는 바뀌지 않는다」.
- Do **not** touch the ⚠️ 미정 markers living inside the renamed rule — they are about a different question
  (here: whether a failure popup appears) and survive the revision. Say so in the rule itself, or the next
  editor reads the revision as having answered them.
- `check_docs.py` cannot see any of this: `[5]` only compares parenthesised labels on **numeric** rule refs, so
  `규칙 M-3(…)` is invisible to it. Letter-numbered rules are a manual-verification zone.

### 19-7. Stale header version vs the 개정 이력 table — fix it in the row you are adding

Both GDD (header 1.16.1 / table 1.16.0+) and TDD (header **0.43.2** / table **0.46.0**) had drifted. When adding
a row, bump the header to the new version and put a **`[문서 머리말 정정]`** clause in that row naming the
versions that had not been reflected — the 1.16.1 row had already established this wording. **Past rows stay
untouched**; the correction lives only in the new row.

### 19-8. Grouping several findings into one roadmap row, and pointing at a row that already exists

Four post-match UI items (no failure notice / up to 20s with no indicator / the 30s auto-return countdown can
expire during it / an already-registered countdown defect) went in as **one row**, because they all touch the
same coroutine and deciding them apart yields contradictory specs.

- The already-registered row is **pointed at, never copied** (§18-1), and the *existing* row gets an appended
  line saying a third item now shares its coroutine. **Both directions**, or whoever opens one of them decides alone.
- 🔴 **Say which item the new work creates and which pre-existed.** Here item ③ is new — the map step is what
  first puts *time* between pressing rematch and the scene change; before it, the transition was immediate.
  An item the current work introduces is a different argument from one it merely uncovered.
- Close the row with **why it does not block** the map work (success path ~0.2s measured from the run log,
  failure reverts cleanly) — a grouped row otherwise reads as a prerequisite.

---

## 20. Extending a Plan when the user folds a **second, differently-shaped** piece of work into it (2026-09-14, rematch-map round 5)

An existing 497-line Plan (stages A~D, "wire up the rematch map") had to absorb a second job the user
just scoped in: **delete the map test mode**. Deletion is the opposite shape of the plan it joins —
A~D *add wiring that does not exist*, the new piece *removes wiring that does*. Eight lessons.

### 20-1. 🔴 Do not re-letter the existing stages — give the new block its own **prefix**

The obvious move is "deletion becomes A~C, the old A~D slide to D~G". **Do not.** The old letters were
referenced by name in **seven** sections (order diagram, failure mapping, rule mapping, risks, verification
table, file list, execution table). Shifting them breaks every pointer **silently** — nothing errors.

- Prefix the new block instead (`T1~T4`, T = TestMode) and **write one line in the doc saying why**,
  so the next reader does not "tidy" the lettering later.
- This is §17-6's rule in a new setting: **the only property that matters is that existing names keep
  pointing at the same thing.** Monotonic prettiness is not a property worth a single broken pointer.

### 20-2. 🔴 A deletion inside a **serialized format** cannot obey the "comment it out first" default

`WORKFLOW.md` [4] mandates disable-before-delete until verified. A flag that sits **inside the canonical
byte stream** has no half state: the field is either there (old format) or gone (new format).

- Say that explicitly, then **name the substitute revert mechanisms** — here ① one stage = one commit, so
  reverting the single code commit restores the format, and ② rule/design docs keep the removed clauses
  as strikethrough + "previous record" quote blocks.
- 🔴 **A deviation from a written project rule is raised as a user-confirm item**, not decided in passing.
  Writing the reasoning down is not the same as being allowed to do it.

### 20-3. 🔴 When the change voids an already-closed verification, the artifact is a **gate stage**

§17-3 taught that the cascade (canonical field → version bump → regenerate binaries → **a finished
verification becomes void**) is the impact scope. This round had to turn that last bullet into a plan.

- Make the re-verification **its own stage** and mark it a gate: *"do not start the next block until this
  passes"*. A bullet inside another stage's checklist gets skipped; a stage with a row in the execution
  table does not.
- **State what gets confused if the gate is skipped** — here: the later stage was already "one commit flips
  everything", so adding a format change on top means a failure has **two** candidate causes from two
  different jobs. That sentence is the whole argument for the gate; without it the gate reads as ceremony.
- Name the price too (**one extra real-device round**) and call it the cost of cause-separation.

### 20-4. A risk the normal path **cannot** catch needs a **non-test** completion criterion

Regenerating the five fallback templates is invisible to any passing test: fallback only fires when 100
generation attempts fail, and every real-device run so far recorded `UsedFallback=False`. **So the gate
stage passes even if the regeneration was skipped.**

- Give that stage a **mechanical** criterion instead — *"all five files shrank by 4 bytes"* — and a second,
  eyeball-able one (the generated source docs' version row reads 2).
- And repeat the rule that keeps the checklist honest: **do not put "does the fallback run?" on the
  real-device checklist** — it is not observable there (`.claude/mistakes.md` 2026-09-09).

### 20-5. 🔴 The original's **absolute sentences** go half-true the moment a second piece arrives

The Plan was full of clean absolutes: *"rule documents are not touched — not one line"*, *"scene/prefab/asset
work: 0"*, *"nothing in this scope is mcs-verifiable"*, *"we create no new files"*. Each is still true of
A~D and **false of the new block**.

- **Find them by grepping the document for its own absolutes** — `한 줄도` · `0건` · `없다` · `전부` — rather
  than by re-reading for meaning. They cluster in summary blockquotes and the "what we do NOT touch" list.
- Repair by **appending a scoped correction** (B-7): *"the sentence above is about block ②; here is what
  block ① does differently"*. Never edit the original — it stays correct for the half it described, and if
  the user reverses the stage order it becomes fully correct again.
- The most valuable one was the inverted case: *"nothing here is mcs-verifiable"* became **false in the
  good direction** — 8 of the 12 deletion files import no Unity namespace at all and the validator already
  ships a `TryRunSelfCheck` entry point. **A new block can make verification easier, not only harder**;
  measure before repeating the old block's limits.

### 20-6. Generated `.md` files are **not** hand-edit targets — grep the generator first

Five documents under `Docs/_Reference/` listed the flag being deleted and looked like ordinary doc-revision
targets. They are **written by an editor tool** (`…Builder.cs` writes what `…Factory.BuildSourceDocument`
returns). Editing them by hand would be undone by the next regeneration.

- → They belong to the **regeneration stage**, not the document stage, and the string that produces the
  offending row belongs to the **code stage**. One deletion, three stages, decided by *who writes the file*.
- **Before listing any `.md` in a revision stage, grep the repo for its path** — a hit inside a `.cs` file
  means the document is an output.

### 20-7. Handed-over scope mismatch, fifth class: **right as far as it went, but incomplete**

Handed over as *"rules 3 · 12 · 14 define the test mode"*. Measured: **also rules 13 and 16** in the same
document, plus the rules index, 3 spots in the GDD and 5 in the TDD.

- Distinct from the four known classes — 2026-08-18 *wrong number*, 2026-08-24 *right number wrong
  conclusion*, §17-7 *wrong unit*, §18 *right count wrong labels*. Here **every handed item was correct**;
  the list simply stopped early.
- Fix: widen the plan to the measured set, **and raise the widening as a user-confirm item** — enlarging
  scope is itself a scoping decision (§15's rule), even when the evidence is unambiguous.
- Say what the too-narrow version would produce: *"rule 3 would say the mode does not exist while rules 13
  and 16 still describe it"* — and that **`check_docs.py` cannot see that class** (it checks number
  references, not content contradictions).

### 20-8. Recording a decision that came from **silence**

The stage order was announced to the user with "say so if you want the reverse", and no answer came.
That is not the same as the user choosing it.

- Write **the procedure, not a verdict**: *"this was announced, an objection was invited, none arrived,
  so it is written this way — reversing it is free until stage T1 starts."*
- Pin **where the reversal stops being cheap** (here: the commit that changes the byte format). A silence-
  derived decision needs an explicit, dated exit, or it hardens into a claim the user never made.

---

## 21. Deleting a whole **feature** from the rule documents, not a clause (2026-09-14, rematch-map round 6 / stage T1)

§19 removed six clauses of one rule. This round removed **a feature** — the map test mode — from five rules
in one document plus the rules index, the GDD and the TDD. The instruction was explicit: *read all five
documents end to end, list every site in one pass, and do not come back later with "I found another one."*
Seven things generalise, and the first two are the ones a grep would never have produced.

### 21-1. 🔴 The hard half is prose that **depends on the feature without naming it**

Grep for the feature name (`MapTestModeEnabled` · 「테스트 모드」 · `5000`) found 11 sites in the rule
document. The read-through found **two more classes that carry no such word**:

- **An authority clause whose only referent was the deleted field.** 규칙 3 said *"싱글플레이는 로컬
  `GameConfig`가 권위다. 멀티플레이는 Host의 `GameConfig`만 권위이며 Client의 로컬 설정은 무시한다."*
  Nothing in it names the test mode. **The way to decide it is to read the function signature**:
  `MapPreparationUseCase.Prepare(ulong rootSeed, bool mapTestModeEnabled)` — the *only* config-derived
  input is the flag. Remove the flag and the sentence has no referent left.
  → **Do not delete such a sentence; re-aim it.** What actually survives is 「Client 가 자기 로컬 설정으로
  덮어쓰지 않는다」 and 「Host 가 생성·확정한 `MapDefinition` 이 권위」. Both were kept, re-worded.
  Same shape in the GDD and the TDD, one site each.
- **A "current value" statement that the deletion's cascade will falsify.** See 21-2.

> **Generalisable test:** for each sentence near the feature, ask **「이 문장이 가리키는 값이 무엇인가, 그
> 값이 사라져도 문장이 가리킬 것이 남는가」.** A sentence whose referent count drops to zero is a site even
> though it contains none of your search terms. Decide it by reading the **code's input list**, not the prose.

### 21-2. 🔴 「초기값 N」 and 「현재 지원 값은 N이다」 are different sentences — only one survives a bump

The deletion removes a field from the canonical byte stream, so `MapVersion` goes `1` → `2` in a later stage.
The handoff supplied a safe-list of four sites, all of the form 「초기값 `1`」 — true after the bump, so
nothing to do. **The read-through found a fifth the safe-list did not cover**: the transfer-package section
says **「현재 지원 값은 `1`이다」**. That one goes false the moment the code changes.

- **Grep for the modifier, not the number.** `초기값` vs `현재`/`지원 값`/`현행` around the same constant.
- In a **docs-before-code** stage the two get **opposite treatments in the same commit**: the clauses being
  deleted go now, the value stays and gets a **stage marker** naming the commit that will change it
  (*"코드가 아직 `1` 이므로 값을 미리 바꾸지 않는다"*). That is §6's "do not rename ahead of the code"
  applied inside a single round — **say in the document which half is which and why**, or the next reader
  reads the untouched number as an oversight.

> **[🔴 2026-09-15 — the lesson above is unchanged; this only records that its prediction came true.]**
> The later stage arrived (commit `8c83317`): **`MapVersion` is now `2`**. Both halves resolved exactly as
> the lesson said they would — the transfer-package section's 「현재 지원 값은 N이다」 **was changed to `2`**
> and its stage marker cashed in, while the four 「초기값 `1`」 sites **were correctly left alone and are
> still true**. 🔴 **This is the check that proves the rule was worth writing**: had the safe-list been
> trusted, one live sentence would now be false and four correct ones would have been "fixed" into lies.

### 21-3. The removal-record block is one table, and its hardest row is the one that **loses a referent**

Same shape as §19-3 (blockquote under the defining rule, 자리 → 제거한 조항 → 근거, everything else points
at it). Two additions this round:

- The 자리 column names **the rule and the sub-section** (`규칙 3 — 권위`, `규칙 3 — 직렬화`), because one
  rule contributed six of the eleven rows and 「규칙 3」 alone would not locate them.
- The row for the authority clause carries the **measurement** that justified re-aiming rather than
  deleting (the `Prepare(...)` signature). A row that only says 「대상이 없어졌다」 invites the next round
  to restore it.
- A ✅ **바뀌지 않는 것** line closes the block (표 값 · 폴백 교체 규정 · 전송 규정 · 검증 6번). Without it
  a reader of an eleven-row deletion table assumes the rule was gutted.

### 21-4. A deletion that changes a **serialized format** needs the cascade written where the format lives

§17-3 identified the cascade (canonical field → version bump → regenerate binaries → **a closed verification
becomes void**). This round put it in **two** places and nowhere else: the rule document's removal block, and
the TDD's 「상위 필드」 list — i.e. **at the two spots that actually define the byte layout.** Every other
site points at the rule block. Copying the cascade into the GDD would have put a byte-format argument into a
design document.

### 21-5. 🔴 Two coincidentally equal values bite again — this time at **deletion** time

§17-2 recorded that `Economy.StartingGold` = 5000 and `TestStartingGold` = 5000 are different fields whose
values happen to match, and that the coincidence hides the test mode's effect. **Deleting one of them creates
the mirror-image misreading**: a reader of the edited `GameConfig` code block sees the 5000 lines gone and
concludes the starting gold itself was deleted.

- So the edit is **not finished when the lines are gone** — attach one sentence saying **which value stays
  and that the two were only coincidentally equal**. Put it under the block, not inside it (the block is a
  code excerpt).
- Generalisation: **when you delete one of two look-alike values, the deletion note must name the survivor.**

### 21-6. Handed-over site list: right where it went, **short in a second document** (§20-7, second sighting)

| 출처 | 인계값 | 실측 |
|---|---|---|
| 규칙 문서 | 규칙 3 · 12 · 13 · 14 · 16 | **일치** (5개 전부) |
| `GameSystemRules.md` | `:56` | **일치** |
| `GameDesignDocument.md` | `:108` · `:626` · `:636` | **`:109` 가 빠져 있었다** (권위 줄 — 21-1의 부류) |
| `TechnicalDesignDocument.md` | `:221` · `:231` · `:394` · `:634` · `:1664~1665` (5자리) | **8자리** — `:205`(단계 범위 목록의 「테스트 모드 설정 필드」) · `:212` · `:511`(fallback 절의 「대체 대상이 아니다」) 이 빠져 있었다 |
| `GameSystemRules_UI.md` | 「통독으로 확인」 | **0건** — 고칠 자리가 없다는 것이 결과다 |

- **「0건이었다」도 결과로 보고한다.** 통독을 요구받은 문서에서 아무것도 안 고쳤으면, 그것이 누락인지
  실제 0건인지 보고가 말해 주어야 한다.
- The two TDD misses share a shape: **they are not spec sentences.** One is a *phase scope list*, the other a
  *"what is NOT replaced" clause*. Neither reads like a definition, so a site-list built from definitions
  misses both. **Sweep the document for the feature as an item in a list, not only as a rule.**

### 21-7. A scope list of a **finished** phase is a record — mark it, never delete the item

`TechnicalDesignDocument.md` 「무작위 맵 시작 동기화」 lists what 2단계 covered, and 「테스트 모드 설정 필드」
is one item. 2단계 is complete, so **the item is true as history**: the field really was added there.

- Deleting it falsifies the record; leaving it bare makes a reader hunt for a field that is going away.
- → **Keep the item, append a marker** saying it became a deletion target and pointing at the removal block,
  with the reason spelled out (*"2단계에서 실제로 추가됐던 것은 사실이므로 목록에서 지우지 않는다"*).
- Same judgement as §19-4's rule for 개정 이력 rows, extended to **any list that records a past scope**.

> **[🔴 2026-09-15 — the lesson above is unchanged; this records the marker being cashed in.]**
> The deletion happened (commit `8c83317`), so on 2026-09-15 the marker was **updated, not removed**:
> the item still says 2단계 added the field, and the note now reads **「삭제 대상」 → 「삭제 완료」**.
> 🔴 **This is §6's rule about markers meeting §21-7's rule about scope lists**: a marker that predicts a
> future change becomes misinformation the moment the change lands, so **whoever lands the change owes the
> marker an update in the same round.** The item itself is still never deleted — it is still true as history.

---

## 22. Widening a **type definition** and closing pinned 「미정」 markers in one round (2026-09-15, post-match opponent-left UI)

Scope of that round: **one file only** — `GameSystemRules/GameSystemRules_UI.md`, 「공통 UI 규칙」 section.
A confirmed design (result screen when the opponent leaves) had to be written in as rules.

### 22-1. A new widget that fits **neither** of a two-type taxonomy → widen the definition, never add a third type

`규칙 8` classified every popup as 팝업 (배경 탭으로 닫힘) or 모달 (배경 탭 불가), and 모달 was defined as
*"사용자의 명시적 **Y/N 선택**이 필요한 팝업"*. A new **1-button alert** fits neither: one button is not a
Y/N choice, and filing it as 팝업 would let a background tap dismiss it.

- The repair is **one word in the definition**: 「명시적 Y/N 선택」 → 「명시적 **응답**」.
- 🔴 **Say in the 개정 block what did *not* change** — the discriminator ("must the user press something
  inside the popup to move on?") is the same, so **no existing popup changes type**. Without that sentence a
  reader has to re-audit every popup the taxonomy already classified.
- What actually widened is worth naming precisely: **「응답의 선택지가 몇 개인가」**, not the criterion.
- The old text survives inside the 개정 block quote — this document's established B-7 shape
  (see the 2026-09-14 blocks on 규칙 M-3·M-4).

### 22-2. Widening a definition leaves a **stale enumeration** in the *neighbouring* rule

`규칙 9` said 모달은 *"반드시 **확인/취소 버튼**으로만 닫힌다"*. Once a 1-button 모달 exists, that enumeration
is literally false while the rule's intent is intact.

- The hand-off said "judge whether 규칙 9 needs fixing, and if not, don't touch it." It **did** need a touch,
  but not a rewrite: **append a reading note** ("확인/취소 버튼" means *the buttons inside the popup*, as
  opposed to the background) and leave both original lines untouched.
- 🔴 **General shape: when you widen a definition, grep the rules that quoted the *old narrow* wording.**
  A definition change is never confined to the cell you edited — a neighbouring rule spelled out the old
  narrowness as a concrete list, and a list goes stale where a criterion does not.

### 22-3. Closing an 「미정」 marker — close only the question that was pinned

`규칙 M-3`·`M-4` carried ⚠️ **팝업 여부 미정 — 구현 시 확정한다.** The round closed it.

- Append a **확정 block** under the marker; **never edit or delete the ⚠️ marker line** (title it
  `[🔴 YYYY-MM-DD 확정 — 위 ⚠️ 미정 표시는 원문 그대로 둔다]`).
- 🔴 **Close exactly what the marker pinned and re-pin the rest.** The marker asked *whether a popup appears*;
  the decision answered that plus its type. **문구 and 버튼 라벨 were never decided**, so the 확정 block ends
  with a fresh ⚠️ 미정 for those. Filling them would be inventing a spec (CLAUDE.md 규칙 10).
- The same block must say which neighbouring 미정 markers it did **not** touch (`M-4` first bullet still has
  two of them), or the next reader assumes the rule is fully settled.
- Where two rules shared one marker ("둘이 같은 문제다"), the second rule's 확정 block **points at the first**
  instead of restating the rationale.

### 22-4. A rule about multiplayer must state where it **does not** apply

`규칙 M-4` is singleplayer. The same round wrote opponent-left rules. Half of a sentence like
"this also applies to M-4" would have been wrong: **there is no opponent in singleplayer.**

- Add an explicit non-applicability line to the singleplayer rule naming the rules that do **not** reach it,
  and say what the closed 미정 **was** ("맵 준비 실패 시 팝업이 뜨는지 하나뿐").
- This is cheaper than it looks and prevents the reverse misreading — that the whole new section is
  multiplayer-only — because the exclusion is stated from the singleplayer side.

### 22-5. Picking a rule-number prefix in a document whose numbering restarts per section

`GameSystemRules_UI.md` 「공통 UI 규칙」 holds **numeric 1~11** (cross-cutting basics: 반응형 / SafeArea /
숨김·표시 / 폰트 / 골드 / 팝업) plus two later **letter-prefixed groups**: `L-1~L-4` (LoadingIndicator) and
`M-1~M-4` (무작위 맵 준비 실패 UI). Both letters are the first letter of the topic's English word.

- A new self-contained feature area therefore takes a **new prefix**, not the next number: chosen `D-`
  (Disconnect) for 「경기 종료 후 상대 이탈 UI」. Continuing `M-` would have filed 이탈 rules under *Map*.
- 🔴 **Write the convention into the section itself** (one line: this section uses `D-`, like `L-` and `M-`),
  or the next author has to re-derive it from two samples.
- Mechanical consequence: **`check_docs.py` cannot see letter-prefixed rules at all.** `RE_RULE_DEF` and
  `RE_RULE_MENTION` both require `규칙\s*(\d+)`, so `**규칙 D-1. …**` is not registered and `규칙 D-5`
  is not checked. `[4]`'s section map still prints 공통 UI 규칙(1~11) after adding six D- rules.
  That is **not a reason to use numbers** — it is a reason to hand-verify letter-prefixed cross-references,
  and a reason the checker returning 0건 says nothing about them.
- Bonus: a letter prefix cannot create a 결번 in `[1]`, which numeric insertion into a per-section-numbered
  document can.

### 22-6. Point at the owner of a value instead of copying it — even when the owner does not exist yet

Three decisions of that round (이탈 판정 = 30초 무반응 / 이탈 시 연결 종료 / 정상 퇴장 즉시 통보) belong to the
network-session spec, not the UI document, and **another agent was writing that document in the same round**.

- The UI section states what it does **not** own, says the copy would go silently false, and adds
  ⚠️ **가리킬 문서명 미정 — 확정되면 이 자리에 문서명을 적는다.**
- 🔴 **Do not guess the receiving document's name or rule number.** A guessed `Doc.md 규칙 N` either trips
  `[3]` or, worse, passes because the number happens to exist and points at the wrong rule
  (`check_docs.py` cannot catch that — see §8).
- The 미정 marker form is the project's own, so the pointer is a normal pinned gap rather than a loose end.

### 22-7. Numbers handed to you as "already covered by an existing clause" — re-read the clause

The hand-off's "don't rewrite, cite the existing clause" table mapped 결정 4 (이탈 시 카운트다운 **30초** 재시작)
onto `규칙 M-3`'s *"자동 로비 복귀 countdown 을 **전체 길이**로 다시 시작한다"*.

- They are **not** the same clause: M-3 restarts at *full length* on **map-preparation failure**, while the new
  decision restarts at **30초** on **opponent-leave**, with the default itself moving to 60초. Same mechanism,
  different trigger **and** different length.
- Repair: write the new rule with its own values, **cite M-3 for the other trigger**, and add an explicit
  ⚠️ "두 규칙을 섞어 읽지 않는다" line — then report the mismatch rather than silently following the mapping.
- 🔴 **A "this is already written down" hand-off item is a claim to verify, not an instruction.** The cheap
  check is: does the existing clause have the *same trigger*? A shared mechanism is not a shared rule.

### 22-8. Measuring the live value before calling a decision "a change"

결정 5 set the default auto-return countdown to **60초**. Before reporting it as a change, the live value was
read from the code *and* the scene: `GameEndUI._autoReturnSeconds = 30f` in code, `_autoReturnSeconds: 30` in
`Assets/_Project/Scenes/Game.unity`.

- Reading only the code default would have been unsound — **Inspector values override code defaults** in this
  project, so the `.unity` line is what decides whether the shipped value is 30.
- Same habit for a literal UI string: the existing countdown text is `{n}초 후 로비로 돌아갑니다.`, which is
  **different wording** from the new opponent-left text, so the decision adds a string rather than editing one.
- Document-only rounds still measure; the report then says "this rule is ahead of the code" with the evidence,
  instead of asserting a code change that nobody made.

### 22-9. Mechanical check for `|` inside table cells

The calling session had botched table cell separators three times that day. Cheap verification: count
**unescaped** `|` per row (`(?<!\\)\|`) and assert one distinct count per table block.
Run it over the **whole file**, not just the new tables — it costs nothing and catches pre-existing damage.
That round: 13 tables, 0 mismatches.

---

## 23. Recording the **first code round** of a multi-stage plan, when the visible deliverable is not visible yet (2026-09-21, post-game-leave-ui stages 1·2·3)

Three of ten stages shipped. The awkward part: **the thing that was built is a popup, and nothing calls it yet.**
That shape recurs — a *capability* lands one round, its *call sites* land later — and it is the easiest place to write
something false without noticing.

### 23-1. 🔴 "The API exists" and "anyone has seen it" are two claims — the second one is a count

`UIManager.ShowAlert` was declared, implemented and delegated. **Its call-site count was 0.**
So every sentence of the form *"the alert popup works"* would have been a fabrication: nothing on screen ever
rendered it. The check is mechanical and takes one grep — **count the real call sites, excluding the declaration
and the delegating implementation**, and say the number out loud in the status document.

- Write it as **「구현했다」와 「실기에서 확인됐다」는 다른 말이고 이 항목은 앞쪽까지만 참이다** — the project's
  existing over-claim wording, reused verbatim so it reads as the same class of caveat.
- ⚠️ **A same-named method on an unrelated class will pollute the grep.** Here `ProfileView` has its own private
  `ShowAlert(string)` that predates the work and has 2 callers. **Read each hit's owning type before counting** —
  3 hits looked like "it is called", and none of them were `IUIManager.ShowAlert`.
- What *can* be claimed is the **negative**: the existing two-button popups did not regress. That is a different
  sentence and belongs in a different bullet.

### 23-2. A regression check that covered 2 of 3 call sites is written as 2 of 3, with the reasoning for the third

Two existing `ShowConfirm` sites were exercised in 실기; the third could not be reproduced on demand.
The handed-over rationale ("same two-button structure, ButtonRow unchanged, so no risk") is **a judgement, not an
observation** — record it as such and keep the word **미확인** on the row. `.claude/mistakes.md` 2026-09-09 is the
precedent: a checklist item that is unobservable in that configuration is not a pass.

### 23-3. 🔴 The deliverable may be in a commit the doc session's checkout does not have

`ConfirmPopup.prefab` was edited by the **user**, inside Unity, and pushed. The document agent's working tree was
behind, so the file on disk still had no `TitleText` and no `_titleText` row. **Measuring it would have produced the
pre-change values and they would have looked authoritative.**

- **Check before quoting a Unity artefact**: grep the file for the identifier the change was supposed to add.
  Absent → the tree is behind; say so instead of measuring.
- Then split the sourcing explicitly: **「이 체크아웃에서 확인 불가」 + who measured it + where the single source lives.**
  Here the measured values already lived in `game-programmer/ui-system.md`, so the status document **pointed at it**
  rather than copying numbers that may be tuned later.
- This is §16-6's class (Unity output unverifiable here), but with a new cause — **not "needs the editor" but
  "the commit has not arrived"**. Both end the same way: name the limit, do not guess.

### 23-4. A plan whose §0 said "comment out first, delete after the test" — and the code has neither

`WORKFLOW` [4] wants deactivation-by-comment first, deletion after [6]. The current file has **no live code and no
commented-out code**, only a comment explaining *why* the line was removed. Whether an intermediate commented state
existed **cannot be determined without git, which 규칙 5 forbids.**

- **Do not infer the process from the end state.** Record the end state (measured) and the unknown (path taken),
  and let the main session — which can run git — close it. Writing "WORKFLOW [4] 위반" would have been an accusation
  built on an unverifiable premise; writing "규정대로 진행됐다" would have been the same error pointing the other way.
- The `§0 …의 최종 삭제` row in the execution table is the right home: status cell says **「현재 코드에 잔존 0건 ·
  경로는 확인 불가」**, and the 비고 cell says why.

### 23-5. 🔴 Two "user decides" items: they are **roadmap rows**, not rule edits — and the rule doc points *at* them

Both leftovers (whether to write the `SetActive`-instead-of-CanvasGroup exception into 규칙 5; whether to delete or
relocate the one-shot editor script) were tempting to "just settle". Neither was settled.

- **Where they live**: `ROADMAP.md`, as rows, using the project's existing `(사용자 판단 대기)` label — there was
  already a row in that shape (`_Logs/_editor/` `.meta` 규정 공백), so the form was copied rather than invented.
- **What the row must carry** so the decision is possible later: *what was done*, *the reasoning that made it look
  right*, *where that reasoning currently lives* (a code comment — which is exactly the fragility), and 🔴 *what was
  deliberately not touched*.
- **The rule document gets a pointer, not a decision.** 규칙 D-5's result block ends with 「단일 소스는 ROADMAP 의
  2026-09-21 신설 행 2개」. When the user decides, that pointer is closed together with the row — write that
  instruction into the row itself, or the pointer outlives the question (§6's marker lesson).
- ⚠️ **The second item is a conflict between two project rules, not an oversight** — `WORKFLOW` [5-2] classifies
  `Setup/` as "deletable after running", but the script is idempotent and is therefore the recovery tool for the
  prefab it built. Say that the two collide; that *is* the thing the user has to arbitrate.

### 23-6. A partial-completion round edits the three status documents in three different shapes (extends §12/§13/§15)

| 문서 | 이번 회차의 수선 |
|---|---|
| `PROJECT_STATUS.md` | `**최종 수정일:**` + **⚠️ 부분 구현** 문단을 맨 앞에 prepend(`✅ 구현 완료` 가 아니다 — 머리말의 기호가 경계의 첫 신호다) + `**현재 단계:**` 문단 맨 앞에 한 줄 |
| `ROADMAP.md` | 갱신 문단 prepend + **행을 내리지 않고 그 행 맨 앞에 「닫힌 것 / 남은 것」 블록** + 낡아진 다른 행에 정정 블록 + 판단 대기 행 2개 신설 |
| `WORK_HISTORY.md` | 표 맨 위 행 1개. **`[쉬운 말로]` / `[바뀐 것]` / `[실기 확인]` / `[⚠️ 과대 표기 금지]` / `[🔴 사용자 판단 대기]` / `[이 체크아웃에서 확인 불가]` / `[문서]`** 순서 |

- 🔴 **완료 표기를 `✅` 로 열지 않는다.** 3/10 단계에서 `✅ 구현 완료` 로 시작하면 아래에 아무리 미완을 적어도
  머리 한 줄만 읽고 간 사람에게는 거짓이 된다. `⚠️ 부분 구현` 으로 연다.
- **A stale row is corrected, not rewritten** — the 2026-09-14 (3차) row's ⓐ (the line that caused the lockout) and
  ⓔ (`RestoreRematchButton()` 호출 1곳) both became false. B-7: prepend the correction block, delete nothing.
  ⓔ was **already stale when it was written** (a subscription had been added the previous round) — the previous
  Research document had spotted it and deliberately left it; **this round is where that deferred fix cashes in.**
- 🔴 **Line numbers quoted in that row are all shifted** (441 → 465 lines). Say so in the correction block and tell
  the next reader to re-find by method name (`.claude/mistakes.md` 2026-08-24).

### 23-7. When there is no `Testcase.md`, WORKFLOW [8] is skipped — and the 실기 결과 needs a declared home

This task folder never had one (the user never asked for TC, so [5-1] was skipped) and asked again for none.
**Do not create it** (규칙 1). Instead write, in the Plan's new result section and in the history row, *that* [8] was
skipped and *why*, followed by where the 실기 results were put instead. Otherwise the next reader sees a round with
실기 numbers and no Testcase and assumes something was lost.

### 23-8. Research documents get an append-only "what is no longer current" section, split three ways

A Research document is a snapshot of the pre-change code; it is never edited in place. The append that works:

1. **더 이상 현재가 아닌 서술** — a table, 자리 / 조사 시점 / 현재(직접 실측).
2. ✅ **그 문서가 「고치지 않고 적어 둔다」고 한 자리 중 이번에 해소된 것** — and where the fix was applied.
   Note that the original judgement was *correct*; the row closes, the document is not wrong.
3. ⚠️ **그대로 유효한 서술** — 🔴 the most important of the three. When six lines change, a reader assumes the whole
   document rotted. List the sections that did not move (here: all of the network findings, because stages 4~6 never
   started) with the reason they did not move.

## 24. Recording a **reversed conclusion** and a principle that outranks the numbered rules (2026-09-21, host/client consistency)

No code. Two user confirmations: ① a project-wide principle (*"the player cannot tell whether they are Host or
Client, so UI and win/loss records must behave identically regardless of role"*) and ② a rule set for win/loss on
leaving. The deliverable is **where those two now live and what they overturned**. Six things generalise.

### 24-1. 🔴 The old diagnosis was not wrong — **its premise changed**. That is a third class of correction

`ROADMAP.md` carried options ①(screen-only) ②(host migration) ③(dedicated server) plus *"②·③ are structural
decisions to settle before release"*. All of that was written **assuming the match continues**. The user's new
direction is to **end the match** (「the opponent vanished, so I won」), and ending it needs no session at all.

- What changed is **one judgement — 「solving this needs ②·③」** — not the options and not the sentence about
  release. So the repair keeps every word and appends: **what the premise was · what it is now · which single
  judgement flipped · and 🔴 that ②·③ remain valid for the other purpose (actually continuing a match).**
- ⚠️ **Never write 「폐기」 for an option the user did not discard.** A reader who sees ② struck out concludes
  host migration is off the table for everything, which is a different and larger decision than the one taken.
- Distinguish the three classes now on record: §17-1 = *true but invites a wrong reading* · §17-4 = *a refusal
  the user later settled* · **24-1 = the reasoning is intact but was answering a different question.**
- The **priority/scale cell gets an append too** (§18-3), but scale that was never measured stays **미측정** —
  here 「②·③ drop out, so it is smaller」 is sayable, 「how big the Client-side check is」 is not (규칙 10).

### 24-2. 🔴 A principle that outranks numbered rules gets a home **above** the rules — never a new 규칙 N

Temptation: add 「규칙 D-7. 역할과 무관하게 일관되게 동작한다」 to `GameSystemRules_UI.md`. Wrong for two reasons:
it would be **one rule among peers** when it actually constrains all of them, and this project **never creates new
rule numbers** (code comments and past Task docs reference them).

- Home chosen: **`TechnicalDesignDocument.md`, as the first subsection of 「🌐 네트워크 설계」** — the principle
  governs network behaviour as a whole, and the section head is the only place that visibly outranks what follows.
- Write the **conflict-resolution direction into the principle itself**: *"개별 규칙이 이 원칙과 충돌하면 고칠
  대상은 개별 규칙이다."* Without that sentence a principle is decoration; with it, it is a tie-breaker.
- The rule documents get **pointer blocks in their section preamble**, not new rules:
  `GameSystemRules_UI.md` 「공통 UI 규칙」 preamble and `GameSystemRules_RandomMap.md` 규칙 17 as an appended
  bullet marked `[날짜 추가]`. **One home + pointers** (§18-1), applied to a principle rather than a defect.

### 24-3. 🔴 When told 「check the existing rule for conflict, do not edit its body」, **the verdict is the artifact**

규칙 17 already said *"Host 가 나가는 경우도 같은 통보 하나로 처리한다"* — it does not conflict; it is the
principle applied. **Recording 「대조 확인, 충돌 없음 (날짜)」 in the pointer bullet is the deliverable**, because
otherwise the next round opens the same two documents and redoes the same comparison from scratch.

- Say in the same bullet **what the rule does *not* cover** (here: how a leave turns into a win/loss record),
  so the pointer does not silently widen into 「규칙 17 covers everything about leaving」.

### 24-4. 🔴 Same number, possibly different clocks — say it is undecided instead of merging (§17-2 forwards)

The confirmed rule says the remaining side records a win **after 60 seconds**. This document already holds a
**60초 connection timeout** and a **30초 leave verdict**(규칙 17). Three numbers, two of them equal.

- **Do not merge the two 60s and do not silently rewrite the 30.** Write: which clock is being named, that the
  equal value is a coincidence until someone decides, and that the implementation round settles it.
- 🔴 **Raise it in the report as well** (규칙 12) — a spec that contains two plausible readings of its own number
  is exactly the 「모호한 경우」 the user must arbitrate; leaving it only in the document hides the question.

### 24-5. 🔴 A negative code finding must carry its search boundary — and you re-measure it yourself

Handed over: *"`wins`/`losses` have only a read path; no write path found."* Re-measured before writing it —
`PlayerProfileService.cs` `GetInt` reads `KeyWins`/`KeyLosses`, both `SaveAsync` call sites save **nickname**
(`SaveNicknameAsync` ×2), and a repo-wide grep for the literal keys hits only that file and a comment in
`LeaderboardService.cs`.

- Write **「찾지 못했다」, never 「없다」** — the phrasing *is* the boundary (규칙 10), and the handoff said so
  explicitly. Copy that distinction verbatim rather than compressing it into 「미구현」.
- The same boundary belongs in the roadmap row as a **first step** (「쓰기 경로부터 확인한다」), which turns an
  admission of uncertainty into the task's opening move instead of a disclaimer.

### 24-6. A rule set that is **confirmed but unimplemented** splits into spec (TDD) + work item (ROADMAP)

The user confirmed the leave/win-loss table *and* said records are 별건 작업. Two artifacts, two documents:

- **Spec → `TechnicalDesignDocument.md`** as a new subsection, carrying the table, the force-quit detection
  (「진행 중」 marker cleared on normal exit, leftover marker on next launch = loss) and 🔴 **the unsolved hole**
  (cut the line but leave the app running → **both sides record a win**; a device alone cannot tell 「I dropped」
  from 「they dropped」; unsolvable without server authority).
- **Work item → `ROADMAP.md`** as a new row (owed by §18-2: 「추후에 별건으로 작업」 = committed, not 보류),
  holding **only the scope and pointers** — the table is not copied.
- 🔴 **Write the hole as 「표가 성립하는 전제의 한계」, not as a defect in the table.** A hole filed as a defect
  invites the next reader to fix the rules; this one is only closed by the server verification in the same row.

### 24-7. 🔴 A correction that arrives **after you already wrote the round** — the fix is the same, the risk is not

The coordinator's supplement landed after the documents were written: the sentence *"a device alone cannot tell
「I dropped」 from 「they dropped」, so it cannot be prevented without server authority"* — **which I had just
written into two documents** — was itself too strong. Checking whether a **third party unrelated to the opponent**
(the internet) is reachable splits the two cases.

- **B-7 applies to a sentence you wrote five minutes ago exactly as to one from a year ago.** Strike nothing;
  append `**[🔴 날짜 정정 — 원문은 그대로 두고 덧붙인다: …]**` carrying *why the original was too strong*.
  §18-4 said this about a previous round's document; here the "previous round" is the same session.
- 🔴 **The change-history row you already added this round also needs the append**, not a rewrite — otherwise the
  row describes a document that no longer exists. Mark it `[🔴 같은 날 보완]` so the two edits read as one round.
- **Where the overstatement came from is the reusable part**: the claim was scoped to 「what the two peers can see
  of each other」 and silently generalised to 「what a device can know」. **When a document says 「X cannot be
  determined」, ask what else the device can reach** before writing 「impossible without a server」.

### 24-8. A hole that shrinks: say **how many are left and which item each remaining one belongs to**

After the procedure landed, the single hole became two — **Relay outage** and **selectively blocking only game
traffic**. The user placed them in *different* work items on purpose.

- 🔴 **Do not file an infrastructure failure under the anti-cheating item.** The user said Relay outage is a
  server-side problem and gets its own row; merging it with 위조 방지 hides that the two have different causes.
  Write the reason (「원인이 다르다 — 인프라 장애 vs 고의 조작」) into both places so nobody re-merges them.
- **A suggestion offered alongside a confirmation stays a suggestion.** 「무효 경기로 다루는 선택지가 있다」 came
  from the coordinator, not the user — so it is recorded with 🔴 **"메인 세션의 제안이며 확정이 아니다"** in the
  same sentence. Never let a proposal inherit the confirmation's authority just by sitting next to it.
- Separate 🔴 **measured in this project** from ⚠️ **general technical fact**. `internetReachability` 0 hits in
  `Assets/_Project/Scripts` is a measurement; *"`Application.internetReachability` only reports path existence"*
  is not, and the document says which is which.

### 24-9. Closing a 「미확정」 you yourself raised — and what the answer usually is

I had refused to merge 60(record) / 60(transport timeout) / 30(규칙 17) and asked the user. The answer: **60 and 60
are the same clock** (the transport timeout *is* the verdict moment), **30 is a different clock on a different
screen** (the result screen, where the win/loss is already decided and cannot change).

- 🔴 **Close it with a table of 「which screen · which value · what it does」**, not with a sentence. The confusion
  was never about the numbers; it was about *which screen the clock belongs to*.
- **Keep the 「미확정」 sentence under `~~취소선~~` and append the closure** — the fact that it was once open is
  what stops the next round from re-opening it.
- ⚠️ **A closure often carries a second, unrelated staleness with it.** Here the same answer revealed that
  「씬 값 미적용」 was out of date (`Game.unity:45473` · `Lobby.unity:7479` are both `60000` since `007f666`).
  The original sentence was *true for its own round*, so it is kept and the result is appended — with an explicit
  **「씬 값 미적용으로 읽지 않는다」**, because the stale reading is what a skimmer takes away.


---

## 25. A confirmation that **overturns one cell** of a published structure table, and a work item that moves between owners (2026-09-21, post-game-leave-ui stage 6)

No code. Three user confirmations about the result-screen leave watchdog: **who runs it**, **what is checked before
the verdict**, and **what "a response" is measured with**. The deliverable is where those land and what each one
does *not* overturn. Nine things generalise.

### 25-1. 🔴 A confirmation that flips 「who does it」 flips **one cell**, not the table

`TechnicalDesignDocument.md` carried a two-row table whose 무반응 row said *감지 주체 = 서버(Host) 의 자체 감시 →
브로드캐스트*. The confirmation ("both sides judge for themselves") kills the **감지 주체 cell** and nothing else:
the **30초** value, *"판정과 동시에 연결을 종료한다"*, and the whole **정상 퇴장 row** stay exactly as written.

- **Write the sentence 「바뀌는 것은 표의 『감지 주체』 한 칸이다」 literally.** Without it a reader who sees a
  correction block above a table downgrades the *whole* table, which is a much larger change than the one taken.
- The correction goes in a `> **[🔴 날짜 정정·보강 — 위 표와 위 항목들은 한 글자도 지우지 않고 …]**` block
  **under** the bullets that follow the table, so the table renders intact and the block reads as an appendix.

### 25-2. 🔴 A new mechanism that **looks like** a replacement for an existing RPC is usually a complement — say so

The watchdog and the stage-5 「정상 퇴장 통보 RPC」 answer the same question ("did the opponent leave?"), so the next
reader's default is *"we now have two ways to do one thing — delete one."*

- The test is **whether the new one fires in the cases the old one already covers.** Here it does not: the watchdog
  only ever runs when **no notice arrived**. So it is **그물**(a net under the notice), not a substitute.
- Write **「둘은 대체 관계가 아니라 보완 관계다 — 하나를 넣었다고 다른 하나를 빼지 않는다」** in the rule, in the
  TDD block *and* in the plan stage, because each of the three is read by someone who will not open the other two.

### 25-3. 🔴 Which work item owns a sub-item is decided by **where the mistake lands**, not by which concept it belongs to

「인터넷 도달 확인」 had been filed as item ⓓ of the *win/loss records* row, because that is the discussion it came
out of. It moved to the **result-screen stage 6**, because 규칙 17 says *"이탈로 판정하면 연결도 함께 종료한다"* —
so a misjudgement there is not a wrong caption, it is **the device cutting its own recoverable connection**, and the
damage surfaces in *this* task's UI (문구·버튼 잠금).

- 🔴 **When an item moves between rows, do not delete it from the old row.** Append **「만드는 순서만 바뀌었다 —
  이 작업은 재사용한다」**. A deleted ⓓ reads as 「no longer needed」, which is the opposite of what was decided.
- Say it in **both** rows and in the rule, each time naming *the other* as where the work happens first.

### 25-4. 🔴 A **priority order** is not a decision — the unverified question and its answer date are part of the record

Confirmed: *1순위 기존 RTT · 2순위 결과 화면 전용 하트비트*. **Not** confirmed: that RTT can show 「갱신이 멈췄다」.

- A 순위 table on its own reads as a decision. It only stays honest with **two extra lines**: 🔴 「『RTT 로 한다』를
  확정으로 적지 않는다」 and **when the question gets answered** (「확인 시점은 단계 6 착수 시」).
- Record what the existing API *is* (`GetCurrentRttMs()`, UnityTransport round-trip in ms) and **how many places read
  it today** (one — the ping display). The call-site count is what makes 「이미 있는 것을 쓴다」 checkable later.

### 25-5. 🔴 When told 「the stage's 완료 판정 may contradict the new design — check」, **the contradiction is the artifact**

Found exactly one: *"브로드캐스트 RPC 메서드명이 `ClientRpc` 로 끝난다"* presupposes **a server judging and telling
the other side**. The repair has three parts and the third is the one that is easy to miss:

1. **Append a marker inside the cell** (`**[🔴 날짜 추가 — 이 칸은 한 글자도 지우지 않았다: … 단일 소스는 §2-1]**`)
   so a reader of the *table* cannot miss it. A note further down the document does not reach that reader.
2. **Keep the line.** Deleting it turns the history into 「we planned it this way all along」.
3. 🔴 **State the limit of the overturn.** 「브로드캐스트를 없앤다」 is *also* not confirmed — stage 5's 정상 퇴장
   통보 keeps a `ClientRpc`, so what was settled is only 「양쪽이 각자 판정한다」. Write 「구현 착수 시 정한다」.
- 🔴 A table row is edited by **appending inside its cell** — never by rewriting the row and never by adding a
  parallel row. The stage numbers are referenced by name in the dependency list, §3, §4 and §6.

### 25-6. New material about stage N goes into a **new numbered sub-section** plus one pointer per affected table

`§2-1` under the stage table, then a one-line `> **[🔴 날짜 추가 — 위 표는 한 글자도 고치지 않았다]**` quote under
the §3 file table saying what is added and that **§2-1 is the single source**. Same reason as §20-1: never re-letter
or renumber anything a later section refers to.

- The sub-section carries the **[쉬운 말로]** opener (CLAUDE.md 규칙 13) even though it is not a new document —
  it is the part a non-implementer reads.

### 25-7. 🔴 Letter-prefixed rules are invisible to `check_docs.py` — cite them with document **and** H2 section by hand

Re-sighting of §22-5. `규칙 D-1 · D-2 · D-4` inside an ASCII decision diagram cannot carry a citation, so the
diagram gets a **following line** naming `GameSystemRules_UI.md` 「공통 UI 규칙」. A 0건 result says nothing here;
the check is reading the `**규칙 D-n.` headings in the target document yourself.

### 25-8. A handed-over **line number** is re-measured, and the repair is to stop citing line numbers

The handoff gave `NetworkGameManager.cs:225` for `GetCurrentRttMs()`; the measurement is **:224**. The fix is not
「correct the number」 — it is **cite the method name** (`.claude/mistakes.md` 2026-08-24 「행 번호로 위치를 가리켜
전부 어긋남」). Report the discrepancy anyway, so the sender knows their figure drifted.

### 25-9. A confirmation-only round touches TDD + ROADMAP, and **not** PROJECT_STATUS / WORK_HISTORY

Precedent is the *same day's* §24 round: 구현 0줄 means the implementation-status document has nothing to change and
the milestone table has no milestone. What it does get is **a TDD 개정 이력 row** (버전 minor bump, 최종 수정일
unchanged when it is the same day) and **a dated block prepended to `ROADMAP.md`**, labelled `(N차)` when an earlier
block that day already exists. Say in the report which documents were reviewed and left alone, and why.

---

## §26. The **second** confirmation-only round on the same subject — when the first one had no code and this one does

2026-09-21 (4차). The 3차 round pinned two decisions about the **result screen** (단계 6); this round pinned the
**same-shaped** decisions about **in-game** leave handling (`ReconnectionHandler`). Everything below is what made the
second round different from the first, even though both were 구현 0줄.

### 26-1. 🔴 "Same decision, second location" is a **pointer round**, not a copy round

The structural decision ("Host and Client each judge on their own side") was already pinned in a TDD section by the
previous round. This round's decision is *"that same structure also applies in-game"* — which is **not** a second
copy of the structure. The shape that worked:

- **New section owns only the delta**: what is different in-game (the code already exists, the clock is `t=60`).
- **One sentence naming the earlier owner**: *"구조 자체의 단일 소스는 위 「…」 절이고, 이 절이 정하는 것은
  「같은 구조가 인게임에도 그대로 걸린다」는 것이다."* Without that sentence a reader cannot tell whether the two
  sections disagree or agree.
- The shared **procedure** (internet-reachability check) is referenced, never restated.

### 26-2. 🔴 The difference that decides the whole round: **does code already exist?**

Round 1 (result screen) could change a plan freely — **cost of reversal was 0**. Round 2 touches a component that is
already running, so `WORKFLOW.md` **[4] 「기존 로직 제거 규칙 (예외 없음)」** applies: disable by commenting out,
delete only after [6] passes and before [7]. **Write this contrast as a table in the document itself**
(`확정 시점의 코드` / `되돌릴 비용` / `적용되는 규정`), because otherwise the next reader carries round 1's
"just edit the plan" reflex into round 2.
- Pair it with a pointer to the `.claude/mistakes.md` entry where **that exact rule was broken in this very cycle**
  (2026-09-21). A rule plus a fresh violation of it reads very differently from a rule alone.

### 26-3. A timeline that is **being changed** is written as the *current* structure plus one 🔴 line

The artifact is a `시점 | 무슨 일이 일어나는가 | 근거` table for **today's** behaviour (t=0 / t=0~60 / t=60 /
t=60~90 / t=90), each row carrying the file+line or scene value that proves it — and then a single quoted line
stating the decision (*"t=60 에 승부를 결정한다. t>60 유예는 없앤다"*). Do **not** write the future timeline as
if it were the current one: the code is unchanged, so the present-tense table stays true and only the decision is new.
- The strongest row is the empty one: **t=0~60 「아무 시계도 돌지 않는다」**. The reason the grace period is
  pointless is that the transport layer already spent that window retrying — say that as a *근거*, not as trivia.

### 26-4. 🔴 A removal list is a **덩어리**, and saying so prevents a half-removal

`_reconnectWaitSeconds` field · `WaitAndForceWin()` coroutine · `OnClientReconnected()` cancel path. One sentence —
**"하나만 빼면 나머지가 더 이상해진다"** — plus a **what-remains** sentence (*"남는 동작은 「끊김 감지 → 강제 승리」
하나"*). A removal list without the what-remains line invites someone to delete two of the three.

### 26-5. Where a value came from is part of the decision to delete it

For a constant being removed, record three things: **which phase introduced it** (cite the status document's row),
🔴 **that the discussion which chose the number was 「찾지 못했다」, not 「없다」** (규칙 10), and **the serialized
scene value** that overrides the code default. The last one is what actually breaks the change if missed — and this
cycle had already tripped on it twice (`m_DisconnectTimeoutMS`, `_autoReturnSeconds`), so name those precedents.

### 26-6. 🔴 Two numbers about the same feature that the round must keep **apart**

`ROADMAP.md` 「재접속 실제 구현」 carries **「인게임 재접속 대기 시간 값 미확정(120초 제안·보류)」**. This round's
확정 E removes *the grace period that exists today*. They are different values and 확정 E **does not close** the
open item. The repair is symmetric and both halves are needed:
- In the **new** section / new roadmap row: *"이 절이 닫지 않는 것"* + why.
- In the **old** item (B-2): an appended `>` block saying the 현황 line will change but **by a different row**, and
  that the 미확정 value survives. One-way pointers produce exactly the misreading they were meant to prevent
  (*"이미 정해졌다"*).
- Useful framing found here: the two work items touch the **same file** but are **opposite jobs** —
  「끝내는 쪽」(judge and end the match) vs 「이어 가는 쪽」(restore state and continue).

### 26-7. Connecting a number that was explained **only inside its own section**

The 승패 기록 table said *"회선 끊김 → 남은 쪽 60초 뒤 승리"* and a later block had already settled that this 60초
is the transport timeout's clock — but **nothing said which code runs at that instant**. The append is one bullet:
name the method (`OnClientDisconnected`), say the new section pins it as `t=60`, and 🔴 state explicitly that the
table and the clock-separation text **did not change** — only *"그 60초가 어느 코드에 떨어지는가"* was added.

### 26-8. Marking a row of a 「혼동 주의」 table as **going away** without falsifying the table

*"위 표의 「인게임 재접속 대기 30초」는 없어질 예정이다"* must be followed by 🔴 *"아직 코드는 그대로이므로 위 표는
현재 상태로서 여전히 참이고, 바뀌는 것은 「앞으로」다."* Also name the rows the decision **does not** touch — a
"this row is going away" note next to two unrelated rows otherwise casts doubt on all three.

### 26-9. Re-measuring a line number a previous round wrote, in a document you are already editing

The 최상위 원칙 절 cited `ReconnectionHandler.cs` **79행**; measurement said **81행** (same code, same conclusion).
B-7 applies even though the sentence is otherwise true: **append a `>` correction block**, say the conclusion is
unchanged, and add the forward rule (*"다음부터는 메서드 이름으로 가리킨다"*). Do not renumber the past record and
do not skip it because it is "only a line number" — a wrong line number in a 실측 예 is what makes the next reader
distrust the measurement.

### 26-10. `PROJECT_STATUS.md` is the document you *decide not to touch*, and the decision is an artifact

Its 미구현 row (*"재접속 실제 구현 없음 | ReconnectionHandler | 30초 대기 후 ForceWin만"*) is **still true today**
because the round wrote 0 lines of code. 「앞으로 바뀔 예정」 is `ROADMAP.md`'s role, and a "예정" marker there would
duplicate the roadmap row — a second place to go stale. Write the non-edit **into the roadmap block** as a
⚠️ bullet, and lead with it in the report. (Same conclusion as §25-9, reached from a different direction: there the
reason was 구현 0줄, here it is **role separation between 현황 and 예정**.)

---

## §27. 🔴 The mistake class `check_docs.py` cannot see — quoting a document that holds **both** a 확정 and the **current code**

2026-09-21, recorded while appending the `.claude/mistakes.md` entry for this same cycle. This section is **not** a
round convention like §12–§26; it is about **reading**. It exists because the same cause has now produced two
separate incidents, and the checker was silent for both.

### 27-1. 🔴 0건 guarantees the **documents** are consistent — never that the writer **read** them

`python3 Tools/check_docs.py` 0건 means: no rule-number gaps, no broken links, no citation of a rule that does not
exist, no ambiguous citation, no title mismatch, no orphan topic file, no agent-folder line loss. **That is the
whole list.** It says nothing about whether a statement made in conversation matches the document it is about.
- In the 2026-09-21 incident the documents were **correct**; what was violated was **the reading side**.
  A checker run would have printed 0건 before, during and after the mistake.
- So when a report says 「검사기 0건」, scope it: **0건 is about the documents I edited, not about the claims I made.**
  A class of mistake that leaves no trace in any `.md` cannot be closed by a tool that only reads `.md`.

### 27-2. When a document holds a 확정 **and** the current code, say which one you are quoting — in the same sentence

The form is one sentence with three clauses: **「지금 코드는 X, 확정은 Y, 아직 반영 전」.**
- Quoting only the code half sounds like **the 확정 was overturned** — that is exactly what happened on 2026-09-21
  (the 30초 유예 of `ReconnectionHandler` was described as still standing, one commit after 확정 E removed it on paper).
- `grep` over `.cs` answers **「지금 구현이 어디까지인가」** and nothing else. **「무엇이 맞는가」 is answered by the
  document** — open it. This is §24-1's premise-change rule seen from the other end: there the *premise* moved, here
  the *code* had not yet moved, and both times the failure was describing one clock while the reader assumed the other.

### 27-3. 🔴 Second sighting, **opposite symptom** — search `.claude/mistakes.md` by *what was not read*, not by symptom

Two entries of that file share one cause — `grep` used as a tool for reading **decisions**:

| 항목 | 읽지 않은 것 | 증상 |
|---|---|---|
| 2026-09-03 | `GameSystemRules/GameSystemRules_RandomMap.md` (통독 안 함) | a 확정 was raised back to the user **as 미결** |
| 2026-09-21 | `Assets/_Project/Docs/TechnicalDesignDocument.md` (확정 절을 안 읽음) | a 확정 was described **as the current behaviour** |

🔴 **The symptoms are mirror images, so the two 목차 lines do not look like the same family.** The file's own
reading instructions say to match on 「실수의 모양」 — refine that for this family: before a documentation round,
scan `.claude/mistakes.md` asking **「이 부류는 무엇을 읽지 않아서 생겼나」**, not 「제목이 내 작업과 닮았나」.
Grouping by *unread source* puts these two side by side; grouping by symptom never will.

### 27-4. The document side was **not** at fault — that two-way marking form is worth keeping

The TDD already separated the two clocks in both directions, which is why the document needed no repair:
- `Assets/_Project/Docs/TechnicalDesignDocument.md` **:788** — *"🔴 이 절은 확정을 적어 둔 것이고 구현은 아직 0줄이다."*
  (the 확정 side names its own non-implementation)
- the same document **:724** — *"아직 코드는 그대로이므로 위 표는 현재 상태로서 여전히 참이고, 바뀌는 것은 「앞으로」다."*
  (the current-state table names the 확정 that will change it — the §26-8 form)

**Evidence that the form has value: it is the reason the repair was 0 documents.** The incident cost conversation
turns, not a document fix. So keep doing both halves — a 확정 block that states 「구현 N줄」, and a current-state row
that points at the 확정 due to replace it — and when only one half exists, add the other rather than editing either.

---

## §28. Recording a round whose **whole subject is that a previous diagnosis was false** — and pinning a scope boundary

The round had no code: two measured facts, one user scoping decision, one mistake entry. What made it different
from §17 (facts-and-decisions-only) is that **one of the facts retroactively invalidated a sentence a shipped
commit was justified by**, and the guard that commit added **stays**.

### 28-1. 🔴 A falsified diagnosis is **not** a removal rationale — say so in the same block

The previous round wrote *"the popup would cover the D-1 wording"*, added a suppression guard, and shipped it.
The component that raises that popup turned out to be **in no scene**, so the overlap was never happening.
The temptation is to write it as 「그 가드는 불필요했다」. That is wrong and expensive: **place the component
later and the overlap becomes real.**
- **The repair is a scoped correction**: what is false is the sentence **read as present tense** (「지금 겹치고
  있다」), not the mechanism. Write **exactly that**: *"바뀌는 것은 「지금 겹치고 있다」는 서술 하나뿐"*.
- 🔴 **Add an explicit non-instruction**: `⚠️ 가드 자체는 지우지 않는다 — 이 정정은 코드 제거 근거가 아니다.`
  Without that line the next agent reads a correction as a deletion order.
- Pair it with `코드는 이 회차에 한 줄도 건드리지 않았다` so the block cannot be mistaken for a change log.
- This is the **fourth** class of "old diagnosis vs. new reality", after §17-1 (true but invited a misreading),
  §17-4 / §19 (the premise changed) and §24-1 (the conclusion flipped): here **the premise was never true**,
  and yet **the action taken on it remains correct**. Those two facts must sit in one block or readers pick one.

### 28-2. 🔴 「파일이 있다」 vs 「씬에 놓여 있다」 — the measurement that settles it, and where it belongs

A component's `.cs` existing proves nothing about whether it runs. Two independent measurements, both cheap:
- `grep -rl "<guid from the .cs.meta>" Assets --include=*.unity --include=*.prefab` → **0 hits = never placed**.
- the string it logs in `Start()`/`OnEnable()` → **0 hits in that day's log = never ran**.
Report **both**; one alone invites "maybe the log was rotated" / "maybe it is added at runtime".
- A header comment reading *"씬 구조 (Inspector에서 수동 배치)"* is a **precondition, not a fact** — write that
  sentence into the record, because it is what fooled the previous round.
- 🔴 **This fact belongs in the design document, not only in the roadmap row** — it changes what the current
  structure *does*, so the TDD section that describes the structure owns it and the roadmap row points at it.
- ⚠️ **It is the same shape as the 2026-09-21 프리팹 전제 조건 lesson** (code complete, rule still not satisfied),
  one level up: **there the prefab lacked a component, here the scene lacks the whole script.**

### 28-3. A fact that **disables the only reader of a value** is not an answer to the pinned 미정

`GetCurrentRttMs()` had exactly one reader — the unplaced component. So RTT is read by nobody at runtime.
It is tempting to close the pinned ⚠️ 「RTT 로 판별할 수 있는지 미확인」 with it. **Do not.**
- Write it as **사정, not 답**: `이것은 그 질문의 답이 아니라 「확인할 자리조차 실행되지 않았다」는 사정이다`,
  and state that the 미확인 항목은 **그대로 살아 있다**.
- Put that sentence in **both** places that carry the 순위표 (the TDD correction block and the Plan's 확정 C
  table) — a reader who only sees one of them will otherwise conclude 1순위 is dead.

### 28-4. ✅ When a **prediction gets reproduced**, the artifact is 「근거의 강도만 바뀌었다」

A 확정 written as a prediction was hit in a field test. The entry is worth writing (it proves the design right),
but it is **not** progress — implementation is still 0 lines.
- Lead with what did **not** change: `바뀐 것은 근거의 강도뿐이고 설계·순서·범위는 아무것도 바뀌지 않는다`.
- 🔴 **Record the role configuration, because a neighbouring fix may not be covered by the same test.**
  Here the reproduction was device=Host / editor=Client, and a fix shipped earlier only applies to the
  **opposite** configuration — so that fix stays **실기 미검증** and the record says so in the same block.
  (Generalised: after a two-machine test, ask which side each pending item runs on before crediting it.)
- Put the reproduction in the 확정's own section and a one-sentence version in the roadmap row; 🔴 **do not open
  a new roadmap row** — the row already exists and a reproduction does not change its scope (§17-5's rule).

### 28-5. 🔴 Pinning a **scope boundary** the conversation blurred — the artifact is a decidable question table

The user had to ask 「인게임 판정이 단계 몇 번인가」 because two things were discussed interchangeably: a numbered
stage and an unnumbered separate job. A paragraph saying 「범위가 아니다」 does not fix that; the reader needs the
**negative answer to the question they actually asked**.
- Shape that worked: a **question → answer table** whose first row is `인게임 이탈 판정은 단계 몇 번인가?` →
  🔴 `어느 단계도 아니다. 단계 1~10 에 그 항목이 없다.` Then: where it *does* live, when it starts, and
  🔴 **what makes the two confusable** (same verdict, different 「언제」).
- **Enumerate the ten stages in one line as prose** to show the absence is checkable, instead of asserting it.
- **Three places, one owner**: the Plan gets the section (`§2-2`) because scope is the Plan's job; the stage
  table gets a **one-line pointer** (a reader scanning the table is the one who gets confused); the 범위 밖
  table gets a **row** — that table is the existing home for "out of scope", and the new section explains *why*.
  The TDD section and the roadmap row keep only the 착수 순서 and point at `§2-2`.
- ⚠️ **Say that nothing was invalidated**: the stages were always result-screen-only. What changed is that it is
  now **written down**, plus the start order narrowed. Otherwise the append reads as a scope cut.

### 28-6. 🔴 A handed-over line count that matches **no file** — decide "not measurable here", not "wrong"

Handed: a 5,119-line log with a 12:01~12:05 excerpt. Measured: 1,222 lines, last entry `03:41:47`, and the
excerpt absent. The file is an **append-only 상시 로그** (two `=== 세션 시작 ===` markers inside the copy), so the
checkout simply holds an **earlier state** of the same file.
- 🔴 So it is **neither a mismatch to correct nor a claim to repeat**: write `이 체크아웃에서 재측정하지 못했다`,
  name the mechanism (append-only), and keep the handed excerpt **attributed to the sender**.
- This is the **sixth** handed-value class (after §16 / §17-7 / §18 / §20-7 / §21): **right about a file whose
  state this checkout is behind on** — the §23-3 cause (artifact in a commit not in this tree) applied to a file
  that **grows** rather than one that is missing.
- ⚠️ Record what you *did* measure in the same breath (guid 0 hits, log 0 hits in **this** copy), so the block
  is not read as "unverified".

### 28-7. The mistake entry: index it by **cause layer**, not by symptom

The symptom (*"asked for a test that could not be verified"*) matches an older entry; the cause does not — that
one picked the wrong **conditions**, this one described a component that **was not in the scene**. And the two
neighbouring entries share the phrase 「읽지 않아서」 but mean **documents**, not **placement**.
- Put the distinction **in the 목차 line itself** (`「문서를 읽지 않음」이 아니라 「실제 배치를 확인하지 않음」`),
  because the 목차 is scanned by symptom and would otherwise file it under the wrong family.
- Add a body paragraph naming **which** older entries it is *not* like and why — the entry's value is that
  separation, so it cannot live only in the index line.
- 🔴 Two of the lessons are about **how a test is requested**, not about code: *name which side must be Host*
  (role decides the code path) and *never put out-of-scope behaviour in a test list* (the user reads it as a
  deliverable of this round). Both belong in the entry, not in the design docs.

### 28-8. 🔴 Deciding **not** to touch `PROJECT_STATUS.md` when you found it already stale

The document's 「미착수 — 단계 4~10」 bullet was false (stages 4–7 shipped and ran in the day's log), but making
it true is **a different round's work** (it needs that phase's verification boundary).
- **Do not half-fix it**: adding only the new fact would leave a document that corrects one sentence while
  contradicting another, which is worse than one stale bullet.
- The deliverable is the **judgement plus the finding**: report it, and put a one-line note in the roadmap's
  round block (`손대지 않았다` + 🔴 `그 한 줄은 착수 전부터 이미 낡아 있었다` + why it is out of scope).
  🔴 **Where the note goes matters** — it goes in the document whose job is 「앞으로」, not into the stale
  document, because writing it there would itself be the edit you decided not to make.
- This is §26-9 (「손대지 않기로 한 판단」 자체가 산출물) with an extra half: **there the document was still true;
  here it is not, and the record must say who is expected to fix it.**

---

## §29. Repairing **stale records** only (no implementation) — splitting a bundled ✅ row, and grading "done" (2026-09-22, Phase 8 + post-game-leave-ui stages 4–7)

The round's whole job was **two stale records**, with code explicitly out of scope. Two shapes recur.

### 29-1. 🔴 A status cell that bundles **several components** is the unit that goes stale

`PROJECT_STATUS.md` 의 Phase 8 행은 `LobbyUI, NetworkStatusUI, ReconnectionHandler` **셋을 한 칸에 묶어 `✅ 완료`** 로 적고
있었다. 셋 중 둘이 결함이었는데 **행이 하나라 표기할 자리가 없었다.**
- **The cell value is the one thing you do change** (`✅ 완료` → `⚠️ 부분 완료 (날짜)`), and the 내용 칸은 건드리지 않는다.
  Then say so in the appended block: 「내용 칸은 한 글자도 지우지 않고 **상태 칸만** 갱신했다」 — otherwise B-7 looks broken.
- **Do not add table rows** for the parts (the table's key column is `Phase`; a part is not a phase).
  Append a **blockquote right after the table** holding a 부품 / 실제 상태 / 근거·단일 소스 3-column table.
- 🔴 **The component that was not examined this round gets a row too**, reading `이번 확인 대상이 아니다 — 기존 표기를 그대로
  유지한다`. Leaving it out reads as "unknown", which is a claim nobody measured.
- **Each defect row points at its own single source and copies nothing** — two defects of one row can live in
  **two different sections** of the same document (here `TechnicalDesignDocument.md` 「결과 화면 이탈 판정·통보 구조」의
  2026-09-22 블록 and 「인게임 이탈 처리 — `ReconnectionHandler` 정리」의 확정 D). Close the block with
  「이 자리는 **「Phase N 이 통째로 완료가 아니다」**만 알린다」.
- 🔴 **A hand-off may list only the defective parts.** Here `ReconnectionHandler` was in the row and the
  hand-off had to spell out *"do not drop it"* — if a bundled cell lists N names, the repair must account for **all N.**

### 29-2. A sentence that is **true about the code and false about the behaviour**

939행: *"`FindFirstObjectByType` 자동 탐색 적용됨 ✅ 완료"* — the auto-lookup really is in the code, so there is
nothing to strike. What is false is the **reading** it invites.
- Append a sub-bullet, and **write the split in one sentence**: 「자동 탐색이 있다는 서술은 맞고, 「그래서 연결이
  살아 있다」로 읽으면 틀린다」. (§17-1 의 부류 — 문장이 참인데 오독을 부른다 — 의 두 번째 사례다.)
- Two documents now carry the same fact, so the sub-bullet also says **which one is the 현황 표기**
  (the Phase-8 block) and which is the **단일 소스** (the TDD block). Three-way pointers, zero copies.

### 29-3. 🔴 「완료」는 등급이다 — `로그 확인` vs `실기 확인`

네 단계가 한꺼번에 ✅ 가 됐는데 **검증 강도가 달랐다.** `✅ 완료` 만 적으면 다음 사람이 **네 개 다 화면에서 확인됐다**고 읽는다.
- 상태 칸에 **등급을 붙인다**: `✅ **완료 · 로그 확인**` / `✅ **완료 · 실기 확인**`.
- 🔴 **그리고 「왜 로그까지만인가」를 적는다** — 여기서는 **단계 4·5·6 이 화면을 바꾸지 않기 때문**이고, 그 셋의 화면 검증은
  **단계 7 이 대신했다.** 그 한 줄이 없으면 등급이 **미완처럼** 읽힌다. 실기 확인 행에는 역방향으로
  「이 표에서 「실기 확인」은 이 행 하나뿐이다」를 적는다.
- 행의 **기존 비고는 지우지 않는다.** 착수 전에 쓴 `**다음 단계.**` 같은 말은 시점 기록이므로
  `**[🔴 날짜 덧붙임 · 원문은 그대로 둔다: 「다음 단계」는 … 시점의 기록이고 그 뒤 착수·완료됐다]**` 를 뒤에 붙인다.

### 29-4. A **number-less side fix** earns its own row — and the row's payload is the **test configuration**

커밋 하나가 §10 표에 행이 없었다(단계 번호가 없는 부수 수정). 행을 세우되 첫 칸을 `**(단계 번호 없음) …**` 로 열고
「§2 의 단계 분할에는 없다」를 적는다 — 그러지 않으면 단계 목록과 표의 개수가 어긋난 것으로 보인다.
- 🔴 **「실기 미검증」으로 끝내지 말고 「왜 지난 실기가 이 경로를 타지 않았는가」를 적는다.** 여기서는
  **역할 구성**이었다 — 그 테스트는 실기기=Host 였고 이 경로는 **에디터=Host + 실기기(Client) 강제 종료**를 요구한다.
  (§28-4 의 「역할 구성을 함께 적어야 이웃한 수정이 검증됐는지 갈린다」를 **행 하나에 적용한 형태**다.)
- 그리고 **판정 기준을 로그 문자열로** 못 박는다(`[Network/NetworkCombatController] 게임 종료 — 전투 틱 정지` 가
  **`ForceWin` 경로에서** 남는지). 「다시 테스트한다」는 기준이 아니다.

### 29-5. 「단계 4~10 미착수」처럼 **범위를 숫자로 적은 요약**을 고칠 때

- 원문을 지우지 않고 하위 불릿으로 붙이되, **세 부분**을 갖춘다 —
  ① 🔴 **무엇이 더 이상 참이 아닌가**(「지금 미착수인 것은 단계 8·9·10 과 §8 플래그다」) + **단일 소스는 §10 표**
  ② ⚠️ **그래도 살아 있는 결론**(단계 9·10 의 화면은 여전히 관측되지 않는다 — 원인인 플래그가 미착수이므로)
  ③ 🔴 **이번에 새로 생긴 미검증**. ②를 빼면 「전부 해소됐다」로 읽힌다.
- **같은 절의 이웃 불릿까지 고치지 않는다.** 여기서는 `규칙 D-3 후반부` · `규칙 D-4 30초 재시작 미구현` 두 불릿이
  단계 7 완료로 낡았을 **가능성**이 있었지만, 인계된 실기 확인 항목(문구·버튼·즉시 반영)에 그 둘이 없어
  **구현·검증 여부를 확정할 수 없었다** → 규칙 10·12 대로 **고치지 않고 보고**했다.
- 🔴 **§28-8 의 후속이 이 절이다.** 지난 회차는 `PROJECT_STATUS.md` 의 같은 부류 한 줄을 **「다른 회차의 일」로 넘겼고**,
  이번이 그 회차다 — **넘긴 판단은 언젠가 청구서로 돌아온다.** 넘길 때 **누가 고칠 것인지**를 적어 둔 것이 여기서 값을 했다.

### 29-6. 인계된 근거를 **어디까지 재측정할 수 있는가**로 갈라 적는다

한 회차 안에 세 종류가 섞여 있었다. 근거 칸에 **그 구분을 문장으로** 남긴다.
| 부류 | 이번 처리 |
|---|---|
| 이 체크아웃에서 **재측정 가능** — guid 로 `*.unity`·`*.prefab` 검색 0건 · `m_DisconnectTimeoutMS: 60000` 1건/`30000` 0건 | **직접 실측**으로 적었다 |
| 로그 발췌 — 이 트리의 로그 사본에 **그 구간이 없다**(작업 트리 1,222행 / 원격 5,119행) | **「메인 세션 실측」**으로 귀속하고, 내가 확인한 것은 **「그 로그 문구가 `.cs` 에 실재한다」**까지라고 적었다 |
| 커밋 해시 | 규칙 5 로 git 을 쓸 수 없으니 **전달값으로 귀속**하고 재검증하지 않는다(`※` 한 줄) |

> **[🔴 2026-10-06 correction — the row above is kept as written; its reason is no longer accurate.** `CLAUDE.md` rule 5 now allows a short list of read-only git commands, so a commit hash *can* be re-measured. Until it is, the row's treatment (attribute as handed over, one `※` line) is unchanged; **after** re-measuring it is a value I measured and is written as such.**]**

### 29-7. 🔴 보고로 올린 「확인 필요」가 **답을 받아 돌아왔을 때** — 두 가지 강도의 해소

§29-5 에서 *"확정 못 하면 고치지 않고 보고"* 한 두 불릿이 **같은 회차 안에서 코드 근거와 함께 돌아왔다.**
**질문을 구체적으로(「어느 값이 · 어느 경로에서」) 올려 둔 것이 그대로 답의 형태가 됐다** — 「확인이 필요합니다」로 끝냈으면
다시 물어야 했을 것이다.
- **해소의 강도가 두 종류였고 표기를 갈랐다.**
  | 부류 | 표기 | 함께 적을 것 |
  |---|---|---|
  | 있는 것을 확인 (`const 30f` → 진입점 → 코루틴 인자) | ✅ **구현됐다 · 검증 수준은 「코드 확인」** | ⚠️ **실기에서 초 단위로 관측한 기록은 없다** |
  | 🔴 **없는 것을 확인**(`interactable = false` **0건**) | ✅ **코드로 보장됨 — 「끄는 동작 코드 0줄」** | 「**관측이 아니라 부재의 증명**이라 실기 확인보다 오히려 강하다」 + ⚠️ **「누를 수 있는 상태인 것과 눌러서 동작하는 것은 다르다」** |
- 🔴 **부재의 증명은 강하지만 만능이 아니다.** 「끄는 코드가 없다 = 켜져 있다」는 성립하지만
  **「그 버튼이 제 일을 한다」는 별개**다. 그 한 문장을 빼면 부재의 증명이 실기 검증을 삼킨 것처럼 읽힌다.
- **실기 보고의 범위는 사용자가 말한 단위로만 적는다** — 「테스트 항목 2번이 정상이라고 보고했다」까지이고,
  그 항목 **안의 세부**를 하나씩 확인했다고 명시하지 않았다면 🔴 **「그 세부를 실기로 관측했다」로 적지 않는다.**
- **거짓이 된 것은 문장 전체가 아니라 「시제 한 마디」인 경우가 많다** — 「이번에 끝난 것은 … 한 행뿐」은
  **그 회차에 대해서는 여전히 참**이고 거짓이 된 것은 **「미구현」**뿐이다. 정정 블록이 그렇게 **범위를 좁혀** 적는다.
- **등급이 한 행 안에서도 갈리면 그 행에도 한 줄 넣는다** — §10 표 단계 7 은 `실기 확인` 행이지만
  그 안의 D-3·D-4 두 조각은 코드 수준까지다. 「이 행 안에서도 등급이 갈린다 + 단일 소스는 §11-5 의 두 불릿」.
- 🔴 **코드 자리는 행 번호가 아니라 이름으로 가리킨다**(필드·상수·메서드). 이 사이클에서 인계 행 번호가
  이미 두 번 어긋났다(§26 · §28-6).

---

## §30. Recording a stage that **shipped with zero 실기 검증** — the label, the reproduction condition, and the deferral that came back (2026-09-22 (2차), post-game-leave-ui stage 8)

One stage of a ten-stage plan was implemented and pushed. Nothing was run. The round's whole artifact is
**a status label plus the conditions under which someone could ever see it work.**

### 30-1. 🔴 「구현 완료」 and 「완료」 are different cells — and the missing half is a *procedure*, not a caveat

- The execution-table cell reads **`⚠️ 구현 완료 · 실기 미검증`**, never `✅ 완료`. §29-3 graded a finished thing
  (`로그 확인` / `실기 확인`); this is the grade **below both**.
- 🔴 **Put the reproduction condition in the same cell.** "Untested" without it is a dead end — nobody knows what to do.
  Here: *"한쪽이 재경기 버튼을 누른 직후, 그 누른 쪽 앱을 강제 종료해야 이 경로를 탄다."*
- **Write down what is *irrelevant* too, with its reason.** *"역할(Host/Client)은 무관하다 — 단계 6 이 양쪽 각자 판정이므로."*
  An unstated irrelevance gets re-derived (or wrongly assumed relevant) by the next reader.

### 30-2. 🔴 Two unverified items are **not** covered by one test when their required setups differ

This plan now carries two `실기 미검증` rows: the `ForceWin` fix (needs **the editor to be Host**) and stage 8
(**role-independent**, needs the requester's app killed right after pressing rematch).
- Say it in one sentence, in **both** places: 「한 번의 테스트로 둘이 함께 검증되지 않는다」.
- This is the mirror of §29-4 (a fix whose verification method *is* the role configuration). Same machinery,
  opposite direction: there one config made the path unreachable, here two items demand **different** configs.

### 30-3. A claim whose **conclusion survives while its reason moves**

*"알림 팝업이 화면에 뜨는 모습은 아무도 본 적이 없다 — 실호출처가 0건이기 때문이다."*
After this round the **conclusion is still true** and the **stated cause is false** (실호출처 1건).
- Do not strike the sentence, and do not "fix" it into the new cause. Append: 🔴 **「바뀐 것은 이유뿐이다 —
  띄우는 코드가 없어서 → 코드는 있으나 실기로 관측되지 않아서」.**
- This is a distinct class from §17-1 (true sentence, misleading reading) and §29-2 (true of the code, false of the
  behaviour): here **both halves of a because-sentence must be graded separately.**
- The same sentence lived in **four** documents (Plan §11-5, `GameSystemRules_UI.md` 규칙 D-5 블록,
  `PROJECT_STATUS.md`, `.claude/MEMORY.md` 공통 교훈). Grep the **claim** (`실호출처.*0건`), not the feature name.

### 30-4. 🔴 The staleness a previous round **deferred in writing** lands on the first round with scope for it

`ROADMAP.md` 의 2026-09-22 블록이 *"그 갱신은 이 회차의 범위가 아니므로 고치지 않고 보고했다"* 로 넘긴
`PROJECT_STATUS.md` 「🔴 미착수 — 단계 4~10」 한 줄이 **이번 회차의 대상 문서에 들어 있었다.**
- Close it **and record the closing where the deferral was written** (a new bullet in this round's ROADMAP block:
  「직전 회차가 넘긴 그 줄은 이번에 정정했다」). Otherwise the deferral note reads forever as still-open.
- This is §29-5 one step further: **writing down who would fix it paid off twice** — once when it was found,
  once when a later round could act.
- ⚠️ The correction is a **status reflection, not a new decision** — it may only say what other documents already
  record (here Plan §10 표), so it carries a pointer and **copies no table.**

### 30-5. 「씬·인스펙터 작업이 없었다」 is a **finding**, not silence

- Record it, because the sibling stage in the same plan (단계 1) **did** pull in prefab work, and because this
  project has a live 「파일은 있는데 씬에 없다」 case (`NetworkStatusUI`, §28-2).
- 🔴 **Prove placement before claiming it** — `.meta` 의 guid 로 `*.unity`·`*.prefab` 검색(여기서는 `Game.unity` 1건).
  Then say explicitly 「그 사례와 같은 상태가 아니다」, or the reader has to re-run the check.
- Name the design choice that removed the work (**직렬화 필드가 아니라 런타임 1회 탐색·캐시**) — that is the
  reusable part, not the absence itself.

### 30-6. A completion criterion measured as a **diff** cannot be re-measured without git — convert it to an end state

Criterion [3] was *"diff 추가분에서 `Coroutine`·`WaitForSeconds`·`Timer` grep 0건"*. 규칙 5 로 git 이 없으므로
**그 측정은 재현 불가능**하다.
- Do not quote it as if you re-measured it. Split: 「메인 세션 실측(diff) · **내가 잰 것은 diff 가 아니라 끝 상태**」,
  and state the end-state measurement (the stage's methods contain no timer; every `StartCoroutine` in the file
  belongs to the countdown paths of **other** stages).
- The same split covers criteria that need `git status` (「고치지 않은 파일」) — those stay **전달값** with the reason.
  (Seventh hand-off class: **the measurement method itself is unavailable to me**, distinct from a wrong value.)

### 30-7. The same-name unrelated method trap, second sighting

Counting `ShowAlert` 호출처 returns `ProfileView.ShowAlert(string)` — a **private helper with no relation to
`UIManager`**. Count the **qualified call** (`UIManager.Instance?.ShowAlert(`), and write the exclusion into the
evidence cell so the next counter does not "correct" your number upward. (§23-1 was the first sighting.)

### 30-8. The plan's file table named a file that did not change, and missed one that did

§3 은 단계 8 의 자리를 `GameEndUI.cs` + `NetworkGameEndController.cs` 로 적었는데, 실제로는
`NetworkGameEndController.cs` 는 무변경이고 **표에 없던 `RematchRequestPopup.cs`** 가 바뀌었다.
- Reason worth recording: 「요청 팝업이 응답 대기 중인가」는 **네트워크 상태가 아니라 화면 상태**라 Presentation 안에서 끝났다.
- The table is a **registration-time record** — put the difference in 「계획과 달라진 점」 and leave the table alone (§12-2).

## §31. The round where 실기 **confirmed the feature and found a bug** — and the bug is **older than the stage that surfaced it** (2026-09-22 (3차), post-game-leave-ui stage 8)

### 31-1. A 실행 기록 row when 실기 says "works, but"

`⚠️ 구현 완료 · 실기 미검증` does **not** become `✅ 완료` when the first 실기 run comes back mostly good.
The label that carries both halves is **`⚠️ 실기 확인 · 버그 N건 발견`** — the verification happened *and* something is open.
- Keep the old status text: record it inside the 비고 cell (`상태 칸은 `…` 이었다`), same shape as §12's `⬜ 미착수` note.
- The older detail sections (here §12-4 · §12-6 「실기 검증 0건」) are **dated snapshots — never edited.**
  Instead the new row says *"those sentences are no longer the current state; the single source for the current
  state is this row + the new section"*. That sentence belongs in the row, not in the old section.

### 31-2. 🔴 A defect the new stage **surfaced** is not a defect the new stage **caused**

The test for this: ask **why nobody saw it before**. If the answer is *"the code path had zero callers until this
stage"* (here `UIManager.ShowAlert` 실호출처 **0건** before stage 8), the hole predates the stage and the record
must say so in its own sentence. Writing it as "stage 8's bug" makes the next reader look for the mistake in the
stage 8 diff, where it is not.
- Pair it with the **scope statement**: the hole lives in `GameEndUI.ReturnToLobby()`, so it is **wider than the
  rule that found it** (규칙 D-6). Say「규칙 X 보다 범위가 넓다」explicitly — a rule-scoped bug report is fixed
  rule-scoped and the rest of the paths stay broken.

### 31-3. Two defects that share a root but **not** a symptom get a symptom table

13-1「the popup is visible on the wrong screen」 vs 13-2「nothing is visible but clicks do not land」.
Same root (*leaving a screen without cleaning up the popup that was open*), opposite observability.
A three-row table (사용자가 겪는 것 / 남는 것 / 근거 등급) is what keeps a reader from filing them as one item —
and the 근거 등급 row is what stops the 잠복 one from being read as observed.

### 31-4. 🔴 Writing 미결 논의 without concluding

When the user asks **"what happens process-wise?"** rather than reporting a bug, the artifact is
**facts + open questions**, never a recommendation (CLAUDE.md 규칙 6).
- Lead with the **common root** of the open items (here: *the window where the opponent is already gone but this
  side does not know yet*) — otherwise each item reads as an unrelated edge case.
- Per item: *current code's behaviour* (graded) → *does the user get stuck?* → *what is left over*.
  Answering「갇히지 않는다」 is itself a finding and belongs before the leftover.
- End with a numbered「아직 정해지지 않았다」list and one sentence saying **these are agenda items, not options**.
- A Host/Client asymmetry caused by a `ServerRpc` (executes when the accepting side *is* the server, goes nowhere
  when the server is the one who left) is pinned to the **최상위 원칙** section of `TechnicalDesignDocument.md`
  by pointer only.

### 31-5. Two more handed-over-mismatch sightings (running list: §17-3, §20-7, §18, §21)

- **The pointer named a section that does not exist.** Handoff said `TechnicalDesignDocument.md` 의
  「역할 무관 일관성 원칙」(2026-09-22). The real section is **「🔴 최상위 원칙 — 플레이어는 자신이 Host 인지
  Client 인지 알 수 없다」(2026-09-21 사용자 확정)**. Same content, different name *and* date.
  🔴 Grep the *claim* (here 「역할과 무관」) rather than the handed name, point at the section that exists, and
  record the discrepancy in a note — never invent the handed name into the document
  (`.claude/mistakes.md` 2026-09-01「예시 목록에 적을 이름을 문서에서 찾지 않고 지어냈다」).
- **The handed member list was short but the conclusion survived.** Handoff: *`IUIManager` has only
  `ShowConfirm`·`ShowAlert`·`HideBlockingOverlay`*; measured: **6 members** (+`ShowLoading`,
  `LoadSceneWithDelay`, `ShowBlockingOverlay`). The load-bearing claim —「팝업을 닫는 공개 API 가 없다」— is
  still true because the three extras are loading/overlay members. Record **both**: the corrected enumeration
  and the sentence saying which conclusion survives it (§14's "which conclusion survives the corrected number").

### 31-6. An ordering constraint is a **fact about a second call**, not a style preference

「팝업 닫기는 `ShowLoading(true, …)` 보다 앞」 is only recordable because `ConfirmPopup.Hide()` itself calls
`UIManager.HideBlockingOverlay()`, a **refcount decrement**. Write the mechanism next to the ordering rule;
an order with no stated mechanism gets "simplified" by the next editor.

### 31-7. Writing a section while another agent edits the same code in parallel

State it once, in the 근거 등급 block: *"the 실측 below was read before that agent's fix landed."*
And keep the fix in **방향** tense (「수정 방향」 · 상태 `⚠️ 수정 진행 중 · 실기 미검증`) — a parallel agent's
in-flight work is never written as done, whatever the handoff's confidence.

---

## §32. Closing 미결 논의 with a **user 확정** — new rule vs. extension, and a clock that judges nothing (2026-09-22 (4차), post-game-leave-ui)

The round right after §31: the same §13-3 that §31-4 wrote **without concluding** came back **decided**.
Two files: `Plan.md` (§13-3 확정 block) and `GameSystemRules_UI.md` (규칙 D-7 · D-8, new).

### 32-1. 🔴 The judgment "new rule or extension of D-1/D-2/D-4?" is decided by **four** testable questions

The hand-off explicitly left this to me, so the criteria are the artifact (they go **into the rule**, under
「🔴 왜 새 규칙으로 세웠는가」):

1. **Would the spec scatter?** If the reader must assemble three rules to get one meaning, it is one rule.
2. **Does an existing rule already know this *verb*?** 규칙 D-4 knew only **재시작**; the new spec introduces
   **정지**. 규칙 D-1 knew two states (평시/이탈); the new spec adds a **third**. A new verb is not a footnote.
3. **Is the trigger the same?** D-1·D-2·D-4 all fire on **상대 이탈 판정**; this one fires on **수락 접수**.
   Same screen ≠ same rule — filing it under D-4 would make the rule document itself cause the
   「섞어 읽기」 that D-4's own ⚠️ line warns against.
4. **Does appending break anything?** A new letter-number at the **end** breaks no existing citation, so the
   "never rearrange rule numbers" constraint costs nothing here.

🔴 Either way, **write the judgment into the document**, not only into the report — the next author otherwise
re-litigates it. And when the order says 「기존 본문은 한 글자도 수정하지 말 것」, the new rule carries the
relationship lines *about* the old rules instead (「이 규칙은 D-3 를 바꾸지 않으며 … 명시할 뿐이다」).

### 32-2. A third clock on the same screen → extend D-4's own 「섞어 읽지 않는다」 form into a 3-row table

D-4 already carried a two-way warning (이탈 30초 vs M-3 전체 길이). The new rule adds a third entry, so the
cheapest non-destructive repair is a **table inside the new rule** (계기 / 타이머에 하는 일 / 규정) that
**quotes D-4's warning as precedent** and adds one line: 🔴 **이 규칙만 「재시작」이 아니라 「정지」다.**
The old rules stay untouched and the reader still meets all three in one place.

### 32-3. 🔴 A timer that **judges nothing** — write the negation, and enumerate every stop site

A confirmed 10-second limit whose only effect is reverting a status line. Two sentences do the work:

- **「이 시계는 아무것도 판정하지 않는다 — 승패도, 이탈도, 연결 종료도 하지 않는다. 하는 일은 … 하나뿐이다.」**
  Say **why that sentence exists**: to stop a later round from hanging a 판정 on it.
- **Enumerate the stop sites exhaustively** (「멈추는 자리는 넷이 전부다」). A partial list reads as an example.

### 32-4. 🔴 Referencing a constant instead of copying a number — then check the constant's **own** spec

Order: *"값은 `NetworkMapTransfer.TransferTimeoutSeconds` 를 참조한다, 숫자를 복제하지 않는다."* Following it
is easy; the finding is what turned up on verifying it. 규칙 16 defines that 10초 as **one response window,
with 재전송 1회 → up to two**. So the UI limit can **expire before the failure it waits for**.

- 🔴 That is not a reason to invent a wider limit (CLAUDE.md 규칙 10·12). Record the mismatch, show why the
  confirmed spec survives it (**the clock judges nothing** → only the text reverts; a late 통보 is handled by
  its own row), say explicitly 「…같은 변경은 확정된 바 없다」, and **report it to the user**.
- General shape: **citing a constant is also citing the rule that set it** — read that rule before citing.

### 32-5. Two paths into **one** state is the spec's core — the table's third column is **why**

「수락한 쪽 = 즉시 자기 화면에서 / 요청한 쪽 = 서버 통보를 받고」 looks like an inconsistency until each row
carries its reason (`ServerRpc` evaporates when the Host is gone / the requester cannot know acceptance
happened and would time out alone). Write the reasons **in the rule**, not only in the Plan: without them the
next reader "simplifies" the two paths into one and reintroduces the role asymmetry the whole design fixes.

### 32-6. A decision **not to implement** because the behaviour already happens — log timestamps are the artifact

「진행 중이던 맵 준비 취소는 구현하지 않는다 — 이미 저절로 된다」 rests on three editor-log lines 8ms apart
(이탈 확정 → NetworkManager Shutdown → 맵 전송 객체 디스폰). Put them in a **시각 / 로그 table** in the Plan,
not a sentence: the claim is a *sequence*, and the sequence is what makes "already happens" checkable.
The rule document then says only 「이 규칙은 그 요구를 하지 않는다」 with a pointer.

### 32-7. Closing a 미결 list: map **each numbered item** to its verdict, in a table

§31-4 wrote 「아직 정해지지 않은 것 3가지」 as a numbered list. The 확정 block's first table is
**미결 안건 (위 원문의 번호) → 확정된 결론**, one row per item. Then, separately, the items the confirmation
**did not** close (here: §13-2's latent bug, still 사용자 판단 대기) — 🔴 stated as 「상태를 바꾸지 않았다」,
because silence about an untouched item reads as closure.
Also re-check the enclosing heading: 「…미결 논의 2건」 is now false, so a pointer block goes **under the
heading** (heading text untouched, B-7) naming the new single source.

### 32-8. Rejected alternatives belong with the **decision**, one line of them with the **rule**

Three rejected options (로딩 화면 / 버튼 차단 / 별도 팝업) went in full into the Plan's 확정 block. The rule
document keeps only the one whose rationale is **rule-level**: a loading screen sets `blocksRaycasts = true`
and therefore **disables 규칙 D-3**. ⚠️ Pair it with the case where the same widget **is** correct
(the loading screen after the rematch actually starts), or the ban reads wider than it is.

### 32-9. A 「회차별 바뀐 파일」 section already exists for a *previous* round — add a new one, never edit it

`§13-5` was 「이번 회차에 바뀐 파일」 of the 1차 round. This round appends **§13-6** with its own date label and
one ⚠️ line saying 13-5 is the earlier round's record. Editing 13-5 would have destroyed a dated artifact and
merged two rounds' file lists into an unreadable one.

### 32-10. Verification for a letter-prefixed round: the checker's 0건 is **not** about your rules

`check_docs.py` returned 0건 with `[4]` still printing 공통 UI 규칙(1~11) — D-7·D-8 are invisible to it
(§22-5). So hand-verify: every `규칙 D-n`/`M-n` cited exists in the same document, every relative link
resolves by path, and run the unescaped-`|`-per-row table check (§22-9) over **both** edited files
(this round: 16 + 25 tables, 0 mismatches).

---

## §33. **Overturning** an earlier 확정 because a *newer* rule contradicts it — and the third verdict for a 미정 marker (2026-09-22 (2차) / edited 2026-09-24, post-game-leave-ui)

Same subject as §32, one round later: 규칙 M-3's 2026-09-16 확정 (*"실패를 알림 팝업(모달)으로 알린다"*)
was reversed to 「상태 줄」, and 규칙 D-7 gained three additions. Both files append-only.

### 33-1. 🔴 A reversal whose cause is a **rule that did not exist yet** is not an 오판

The revision block must say so explicitly: *"2026-09-16 확정이 틀렸던 것이 아니다 — 그때는 규칙 D-3 의 이
조항이 아직 없었다."* Write **which rule appeared since** and that it is a collision, not a correction.
Otherwise every future reader reads the reversal as "the earlier round got it wrong", and the record of *why*
the earlier decision was reasonable is destroyed while its text is technically preserved.

- The block's first table is **구분 / 이전 확정 / 이번 개정**, one row per attribute (수단 · 문구 · 버튼 ·
  카운트다운 · 버튼 복원), including the rows that say **그대로** — a reversal's blast radius is defined by
  what it does *not* change.

### 33-2. 🔴 A 미정 marker has a **third** possible fate: 무효 (its referent disappeared)

`M-3` pinned 표시 문구 미정 **and** 버튼 라벨 미정. The revision closed the first and **deleted the button**.

- Writing 「버튼 라벨 미정도 닫혔다」 would mean *a label was chosen* — false.
- Leaving it untouched reads as *still to be decided* — also false.
- 🔴 The honest verdict is **「이 규칙에서 무효가 됐다 — 가리킬 대상이 없어졌다」**, plus the sentence that keeps
  the sibling alive: *"단, 규칙 M-4 가 팝업 확정을 유지하는 동안 그쪽의 버튼 라벨 미정은 살아 있다."*
- Do **not** strike the original ⚠️ line (B-7 keeps strikethrough for an entry that is wholly invalid, and here
  the order was 원문 무삭제) — the 무효 verdict lives in the appended block.

### 33-3. 🔴 A rule that *depended* on the revised one does **not** inherit the revision automatically

`M-4` said *"상세와 근거는 위 규칙 M-3 의 같은 미정 표시를 따른다"* and the 2026-09-16 확정 covered both.
Deciding **not** to propagate is itself a decision and needs its own block:

- Split the revision's rationale **item by item** against the dependent rule's context (table: 사유 / 적용되는가).
  Here ① 규칙 D-3 applies (single-player also has a lobby button), ② does not (no opponent → only one failure
  path), ③ is undecidable (the status line **exists** in single — measured — but no path was found by which a
  single-player rematch map failure reaches it).
- Then **pin the remainder as 미정** rather than deciding it (CLAUDE.md 규칙 10·12), and 🔴 state the resulting
  inconsistency out loud: *"지금은 두 규칙이 서로 다른 수단을 갖는다 … 그대로 두는 것 자체가 결정이라 적어 둔다."*
- Measurement wording matters: 「찾지 못했다」, never 「없다」.

### 33-4. 🔴 Merging N failure paths into **one screen** — the argument that carries the revision

The decisive reason was not aesthetics: rematch can fail three ways (수락 RPC evaporated / map prep failed /
prep limit expired) and **the popup was attached to only one of them**, so identical outcomes produced
different screens. 🔴 **「같은 결과면 같은 화면」** is the reusable form of the project's top principle.
When a round adds a state, count the *other* ways the same outcome is reached before choosing the widget.

### 33-5. A rejected option you already recorded can get **stronger** — append reasons, keep the conclusion

§13-3's rejected option 3 (a failure popup) had one reason. This round added two (모달 무력화 of 규칙 D-3;
a popup covers only one of three paths). Write it as 「기각안이 더 강해졌다 — 사유가 늘었고 결론은 같다」 and
leave the original text alone. A rejected option whose rationale grew is evidence the decision is stable.

### 33-6. 🔴 Correcting a ⚠️ warning **you yourself wrote last round** after the spec moved to meet it

§32-4's flagged mismatch (limit = one window vs. 규칙 16's two) came back **fixed in the spec**:
`TransferTimeoutSeconds * (MaxResendCount + 1)`, still zero numeric literals.

- Keep your own warning as **「정정 전 사양에 대한 기록」**, add the new single source, and state what did
  **not** change (the clock still judges nothing).
- 🔴 Say what the correction actually bought: Host (timeout + resend → failure) and Client (limit expiry) now
  end up at the **same screen at nearly the same time** — i.e. it was a 「역할 무관 일관성」 fix, not a length fix.
- And when a table row you wrote is now wrong in **one cell**, do not edit the table: append
  「위 표의 그 행은 이 확정으로 갱신된다 — 그 한 칸은 이 블록이 단일 소스다」, naming the cell.

### 33-7. A state machine that gained a fourth state needs its **priority**, not just the state

평시 / 준비 중 / 실패 / 이탈 — the artifact is 🔴 **「이탈 > 실패 > 평시」** *with its reason* (the leave verdict
arrives later and is the more important fact, so it overwrites the failure text; the reverse never holds).
A new state without a stated precedence is a bug report waiting to happen.

### 33-8. 🔴 Reading a file another agent is editing **right now** — timestamp the observation, never the verdict

Two greps minutes apart returned different line numbers and different hits in the same file (a constant that
did not exist in the first read existed in the second). Record it as **「내가 읽은 시점의 상태」** and add
🔴 **「이는 결함 주장이 아니라 진행 중 작업의 한 시점 관측이다」** — otherwise a snapshot of half-landed work
becomes a documented defect. Grade the halves separately (constant + rationale present / wiring not yet seen).

🔴 **And cash the snapshot in as soon as the parallel work lands** — a timestamped observation is honest
the day it is written and misinformation a day later, because the next reader takes 「내가 읽은 시점에는 아직
두 갈래였다」 as 「still unimplemented」. The repair is the same append shape: keep the original observation
(it was true then), add a `[✅ YYYY-MM-DD 갱신]` block whose first line is the **negation** (「‘두 갈래였다’는 더 이상
참이 아니다」), re-measure every item yourself rather than quoting the hand-off, and 🔴 end with what did **not**
change (여기서는 실기 검증 0건) plus 「이 갱신은 사실 최신화이며 새로 정한 것은 없다」 — otherwise a status
refresh reads as a new decision. Mirror one pointer line into the sibling document so both stop being stale.

### 33-9. When the session crosses midnight, date the **decision** and the **edit** separately

The 확정 happened in the 2026-09-22 conversation; the file was written on 2026-09-24. Label blocks by the
decision's date (matching its siblings) and add one line: *"문서 편집은 세션이 날을 넘겨 2026-09-24 에 했다."*
Neither a wrong date nor a hidden one.

---

## §34. Recording an implementation that is **narrower than the approved text** — and finding a home for a 「how to use it」 procedure (2026-09-24, post-game-leave-ui 단계 9 + §8 14-4)

Two things shipped in one round: the 문구 split 규칙 18 asked for, and the **에디터 전용 강제 실패 플래그** that
makes the failure screen observable at all. Both are `⚠️ 구현 완료 · 실기 미검증` (§30-1's cell), so the round's
artifacts are placement decisions and graded facts, not a completion claim.

### 34-1. 🔴 An implementation that is **narrower** than what the user approved is a deviation to record, not a silent win

확정 블록은 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 였고 구현은 **`#if UNITY_EDITOR`** 였다. Shape of the repair:

- **Keep the original string** (B-7) and add a **two-row table** (`구분 / 확정 / 구현`) — one row, because exactly
  one attribute moved. §33-1's table shape, minimal case.
- 🔴 **Lead the rationale with the negation of the obvious reading**: 「의도가 뒤집힌 것이 아니라 **더 좁게
  지켜졌다**」. The 확정's stated intent was *"릴리스 빌드에서 통째로 사라지게 한다"*, and a narrower guard
  satisfies it **more** strongly. Without that sentence the reader files it as 「승인받은 사양을 어겼다」.
- The load-bearing reason is a **fact about the tool's only entry point**: 조작 수단이 에디터 메뉴뿐이라
  개발 빌드에서는 **켤 방법이 없다** → 넣으면 **죽은 코드**다. Record the reason of that shape, not a preference.
- Add the **cost of reverting** (「되돌리려면 저장 수단부터 새로 만들어야 한다」) so a later round knows the
  deviation is not a one-character toggle.
- ⚠️ The original string also lives in **status documents** that quoted the 확정 (`PROJECT_STATUS.md` 의
  미착수 줄 두 곳). Grep the string, append a marker in each, and say 「위 괄호의 그 표기는 **확정 당시의
  표기**」 — never edit the quote.

### 34-2. 🔴 Where a 「how to use this tool」 procedure lives is decided by **reachability**, not by subject

The order said: put it where a person can find it, reachable from `AGENTS.md`. The decisive fact is that
**`_Tasks/` is not in the `AGENTS.md` index at all** — a procedure written only in the Plan is unfindable, no
matter how well it fits there topically.

- Chosen home: **`TechnicalDesignDocument.md` 「💻 개발 환경」 에 H3 절 신설**, because the tool *is* part of the
  dev environment, TDD is indexed, and TDD already owns this feature area's structure sections.
- **Precedent that settles the shape**: `LogRules.md` keeps the Logcat 메뉴 절차 (메뉴 항목 이름·순서·실행 시점)
  in the document that owns logging. A tool's manual belongs to a 상시 참조 문서, not to the task record.
- The new section's first line must say 🔴 **「이 절은 「도구를 어떻게 쓰는가」의 단일 소스이고, 무엇을 띄우는지
  (사양)는 규칙이 정한다」** with pointers — otherwise the procedure slowly absorbs the spec.
- Amend the index **row's description** (not a new row) so the index sentence names the procedure. A new row for a
  section of an existing document would give one file two index identities.
- 🔴 **Write the placement reason into the Plan** (「왜 여기가 아니라 거기인가」) — the next round asks the same
  question, and the answer is a property of the doc tree, not of this tool.
- TDD carries a 개정 이력 표 → new version row + header 버전·최종 수정일 bump (§19's rule), and the row says
  **설계 조항 변경 0건** so the version bump is not read as an architecture change.

### 34-3. Splitting the **문구** of an existing state is not adding a state — say it literally

규칙 D-7's 네 번째 상태(「재경기 실패」) had one string; now it has two, keyed on 「연결 끊김인가」.

- 🔴 First sentence of the block: **「상태가 다섯으로 늘어난 것이 아니다 — 상태는 그대로 넷이고, 갈라진 것은
  「실패」 상태의 문구뿐이다.」** A state machine's reader counts states; a silent split makes them recount.
- ⚠️ Also re-state what did **not** move: 우선순위 「이탈 > 실패 > 평시」(§33-7). The measurement that backs it is
  that the branch site is still **one** ternary chain — say that, because "priority unchanged" is otherwise a claim
  about intent rather than about code.
- **Second sighting of §33-6's cell-level repair**: the earlier 문구 표 row is now only the 「연결 끊김이 아닌
  경우」 문구, so append 「그 표의 그 칸은 이 블록이 단일 소스」 instead of editing the table.
- The 2-row 문구 table belongs in **one** document (the UI rule, which 규칙 18 itself deferred to), and 규칙 18
  gets a pointer only — 🔴 including the sentence that the deferral's destination **is this block**, so the two
  documents cannot drift into two copies.

### 34-4. 🔴 「모르면 중립 문구」 is a rule-level judgment — table it with the 「왜」 column

사유를 모르는 경로가 둘(수락 전송 실패 · 한도 만료)이고 둘 다 기본 문구다. The reusable part is not the
mapping but the argument:

- Two-row table whose second column is **why this path cannot know** (상대가 나갔을 수도, **내 회선일 수도**),
  then one 🔴 line: 「단정하면 **사용자에게 거짓을 말하게 된다**」(`CLAUDE.md` 규칙 10). §32-5's third-column
  form, applied to causes instead of roles.
- Close it with the **forward-looking** sentence — 「앞으로 이 화면에 「상대가 나갔다」는 뜻의 표시를 새로 놓을
  때도 같은 기준으로 판단한다」 — which is what turns one decision into a criterion.
- Record **why two enum members share one string** (`Unknown` vs `Other`): 「모른다」와 「알지만 연결 끊김은
  아니다」는 다른 사실이고 **로그를 읽는 사람에게** 필요하다. ⚠️ 그리고 「화면 문구는 둘이 같다」를 같은 자리에
  적는다, or the next reader adds a third string to justify the third member.

### 34-5. Documenting a debug tool: the artifact is 「진짜와 어떻게 구별하는가」 + which number it is the **only** observer of

- 🔴 A forced-failure tool that reuses the real ending path leaves logs that look like a real outage. So the section
  needs a **구별법** block: immediate-failure 사유 carry `[강제 실패] … (실제 장애가 아니다)` in the 결말 로그,
  while the **real-path 사유 (`ResponseTimeout`) has no marker in the 운영 결말 줄** — its evidence is a *different*
  line (회차 시작 시점의 개발 로그 `Forced=…`). ⚠️ Write that asymmetry down; it is exactly what a tester misreads.
- **Name what only this variant can observe.** `ResponseTimeout` is the only 사유 that goes through
  timeout → 재전송 1회 → timeout, so it is the **only way to check the 「맵 준비 한도」가 두 창을 재는지**
  (the number §33-6 recorded as corrected). A tool's per-option value is what the option *uniquely* verifies.
- The 성질 list that must be in the procedure, because each one is a trap: 릴리스 빌드에 없다 / 씬·프리팹에
  저장되지 않는다 / **한 번 쓰면 꺼진다** / **재경기 회차에만** 걸린다 / 새 실패 경로 0개.
- 🔴 **Design reasons that prevent user error are part of the manual**, not trivia: 「읽기」와 「끄기」를 한
  메서드로 묶어 호출부가 끄는 것을 빠뜨릴 수 없게 했다(선례 `MapHandoff`); 최초 경기에 걸리면 게임 자체가
  시작되지 않아 테스트가 불가능해진다.

### 34-6. 🔴 `grep -c` says 1 where declarations are 0 — the token's legitimate home is the comment that forbids it

Hand-off: *"`[SerializeField]` 0건"*. `grep -c SerializeField` on that file returns **1** — the hit is the header
comment explaining **why it was not used**.
- Write it as **「선언 0건 · 「쓰지 않은 이유」를 적은 주석 1회」**, never as a bare 0 or a bare 1.
- This is the mirror of `.claude/mistakes.md` 2026-08-26 (**quoting a banned token in an explanation blocks the
  「잔존 0건」 check**): here the quote is *correct* and my count had to account for it. Same mechanism as §30-7's
  same-name method trap — **the counted string is not the counted thing.**

### 34-7. Two unverified items from the **same** round: say they share one setup (the inverse of §30-2)

§30-2 recorded two 미검증 items whose setups **differ**. Here 단계 9 and the flag are verified by **one** test
(에디터를 Host 로 두고 한 판 → 메뉴에서 사유 → 재경기), and that setup happens to match the `ForceWin` item's
「에디터가 Host」 requirement while **not** matching 단계 8's.
- 🔴 Write the sameness as explicitly as §30-2 wrote the difference, and in the same cells — otherwise the next
  tester runs three sessions for what needs two.
- Keep the 「무관한 것」 habit: for this tool **the 실기기 is only the opponent**; 실패 판정은 Host 가 하고
  문구도 그 화면에 뜬다. That single sentence is what justified narrowing the guard (34-1).

### 34-8. A row that stays `⬜ 미착수` while its **description** goes stale (extends §29-1)

단계 10's status did not change this round, but its text (*"맵 준비 실패 알림 팝업 (M-3 · M-4)"*) is stale: 규칙 M-3
was revised to a 상태 줄 and that half shipped in an earlier round, so only **싱글 규칙 M-4** remains.
- §29-1 was a stale **status cell** with valid text. This is the opposite: **valid status, stale text.** Repair =
  leave the status cell untouched, append a marker that (a) says the status did **not** change this round,
  (b) names what disappeared and where its single source is, (c) 🔴 states that the sibling's 미정 표시 **is still
  alive** and that this round did not touch it (§33-2's 살아 있는 형제 line).
- Add the measurement that bounds the round: **`ShowAlert` 실호출처 여전히 1건, 이번 작업으로 늘지 않았다.**

### 34-9. Two small recurring notes

- **New `.cs` files with no `.meta`** (third sighting of §16-6's class): write 「이 체크아웃에서는 Unity 가 아직
  import 하지 않은 상태」 + 🔴 「결함 주장이 아니다」. Do not infer anything about the commit from it.
- 🔴 **Deciding not to write a commit hash is itself a record.** Earlier rounds carried hashes as **전달값**; this
  round got none, so every document says **「커밋 해시는 적지 않는다 — 규칙 5 로 git 을 실행할 수 없어 확인할
  수단이 없다」**. An empty 커밋 칸 with no sentence reads as a forgotten field.

---

## §35. The round whose subject is **"the previous round's top hypothesis was refuted in 실기"** — and why that is not only a mistake (2026-09-27, post-game-leave-ui 실기 검증)

The first end-to-end 실기 run of a ten-stage feature. Most 「🔴 실기 검증 0건」 markers in six documents became false
in one go, one new bug appeared, one 문구 form was confirmed by the user — and the previous round's
**「1순위 용의자」** diagnosis turned out to be wrong for a reason that was never in the code.

### 35-1. 🔴 The refuted hypothesis was **not** where the hand-off said it was — and the correction goes where its reader lands

Hand-off: *"그 가설은 `Plan.md` 와 커밋 메시지에 1순위 용의자로 기록돼 있다."* `Plan.md` had **zero** hits for
`용의자`; the record lived in **another agent's memory** (`.claude/agent-memory/game-programmer/ui-system.md`).
- Grep the **claim** (`용의자`), not the handed location — the §31-5 procedure, third sighting.
- 🔴 **Append the 정정 to the file that actually holds the hypothesis**, even when that file belongs to another
  agent (`.claude/agent-memory/**` is in document-manager's ownership). A falsified hypothesis left uncorrected
  is §33-8's "honest the day it is written, misinformation a day later" — and the reader who needs the correction
  is the one reading *that* block, not my Plan section.
- The append must carry **three** things so it cannot be misread as a spec decision: a signature
  (*"document-manager 가 실기 검증 결과를 반영하며 덧붙였다"*), 🔴 *"사실 최신화이며 코드에 관해 새로 정한 것은 없다"*,
  and the **original left untouched** (B-7). Keep the parallel agent's own conclusions (「비대칭 자체가 단서다」)
  alive as an *observation* while saying the measurement no longer supports acting on it.
- The commit message half of the hand-off stays **unjudged**: 규칙 5 means there is no way to read it. Say that
  instead of quietly dropping it.

### 35-2. 🔴 A refuted hypothesis is a **procedure that worked**, and the record must say so in its own sentences

The user's order was explicit — *"이것을 「실수」로만 적지 말 것"*. The reusable shape is **three sentences**:
1. **Setting the hypothesis was correct** — it narrowed an unobservable gap to one named candidate.
2. 🔴 **Not fixing it was correct** — only observation (진단 로그 4자리) was added, so **no healthy code was
   operated on.** Had the hypothesis been right, the same logs would have confirmed it: **either way the log answers.**
3. ✅ **The earlier round's own 유보 line did the work** — *"`StopCoroutine(자기)` 가 즉시 끊는가는 확인되지 않았다
   (Unity 없음, 실측 0)"*. Because an assumption was not promoted to a fact, this round is a **확인, not a 정정.**
- In `.claude/mistakes.md` this means the entry opens with a blockquote saying it holds **both** halves, and
  🔴 **the 목차 line itself names the good half** — a 목차 scanned for 증상 alone hands the next reader the wrong lesson.
- The 교훈 list then carries `✅` bullets beside the 🔴 ones, and one forward-looking rule:
  **「관측이 비어 있으면 먼저 관측을 만든다.」**

### 35-3. 🔴 When the cause is the **test environment**, the fix is a procedure — and procedures get no roadmap row

The real cause: the forced-failure tool logs its ending at **`[ERROR]`** (by design — it reuses the real failure
path), Unity's **Error Pause** was on, and a paused play mode runs no coroutines.
- **State the causal chain in the document that owns the tool**, as a numbered 성질/절차 item, not as a bug note:
  🔴 *"고칠 자리가 코드가 아니라 절차다."* Home chosen by §34-2's reachability rule (TDD 「개발 환경」 절), not by subject.
- ⚠️ **Say that the `[ERROR]` level is not a defect** — otherwise the next round "fixes" the log level and destroys
  the tool's own guarantee (「테스트에서 본 화면 = 실제로 나는 화면」).
- 🔴 **Do not add a ROADMAP row**, and write *why* in the ROADMAP block: 「앞으로 할 일이 아니라 쓰는 법이라 행을
  세우지 않았다」 + a pointer. A procedure with a roadmap row reads as unfinished work forever.
- ⚠️ The environment fact itself is **사용자 보고 only** — 🔴 *"Error Pause 가 켜져 있었다는 사실은 로그에 남지 않는다."*
  What I could measure was the **level** (`[ERROR]` 5건 / 전체 8건). Split those two grades explicitly.

### 35-4. 🔴 Erasing 「실기 검증 0건」 across a feature: the **per-item grade table is the artifact**

The order was 🔴 *"일괄로 「완료」로 바꾸지 말고 항목별로 갈라 적을 것."* One round produced **five** different grades:

| 등급 | 무엇이 그렇게 되는가 |
|---|---|
| ✅ **실기 확인** | 로그에 그 규정의 발화가 남았다(초 단위 수치 포함) |
| ✅ **실기 사용 확인** | 도구가 실제로 쓰였다 — 「사양이 검증됐다」와 **다른 말** |
| ⚠️ **사용자 화면 확인** | 로그에 남지 않는 것(문구·버튼)을 사람이 보았다 — 항목별 관측으로 읽지 않는다 |
| ⚠️ **재현 수단 없음** | 코드 보장까지. **왜 만들 수 없는지**를 함께 적는다(사유 3종은 전송 계층의 것이다) |
| 🔴 **미해결** | 원인 확정 · 수정 진행 중 · 미검증 |

- Every row carries an **이번 근거** column; a row without one silently inherits the round's best grade.
- 🔴 **The stale marker is never deleted** — the old header/bullet keeps its text plus a marker naming the new
  single source (「검증 상태」 머리말도 그 대상이다: a reader scanning headers only sees that line).
- **Rows whose status did not change get no marker** — 단계 8(규칙 D-6)은 이번 테스트가 검증하지 않았고, that
  non-verification is itself reported (35-7).

### 35-5. ✅ The log was **in this checkout** this time — so I closed two 「재측정 못 했다」 notes I had written myself

Earlier rounds recorded 「이 체크아웃의 로그 사본에 해당 구간이 없어 내가 재측정하지 못했다」 for 단계 5·6 (the log is
an append-only file that lagged behind). This round's log contained those lines.
- The repair is one appended marker per row whose **first line is the negation** and whose last line is
  🔴 **「바뀐 것은 근거의 강도뿐이고 상태·범위는 그대로다」** — an upgrade from 「메인 세션 실측」 to 「내가 직접
  재측정」 is not a status change (§28's ✅ shape, second sighting).
- This is §29-6's mechanism paying off again: **a note that says who will close it, and when, gets closed.**

### 35-6. Handed-over figure, **8th class — the number cannot be reproduced because the anchor line differed**

Hand-off: 맵 준비 한도 **20.006 / 20.013초**. Measured from the 진입 로그 (`재경기 준비 상태 진입`), **both** rounds
give **20.006초**; no anchor line produces 20.013.
- Record **my anchor** ("진입 로그 기준"), my two values, and 「그 값을 만드는 기준 줄을 찾지 못했다」.
  🔴 **Never overwrite the handed number and never guess the anchor** (규칙 10). Running list: §17-3 · §18 · §20-7 · §21 · §30.

### 35-7. 🔴 A hand-off list item whose **name** does not match what the log can show

The ✅ list said 「규칙 D-6 이탈 반영」. The log's `상대 이탈 반영 시작` (3건) is 규칙 D-1·D-2·D-4 — **단계 7**.
규칙 D-6 (요청 팝업 닫기 + 알림 팝업) has **zero** traces, for two separate reasons worth writing down:
**그 자리에 로그가 없다**(a 설계 선택 recorded back in §12) and **이번 이탈들은 요청 팝업이 떠 있지 않은 상태**였다.
- 🔴 **So its 미검증 is not closed, and the row is left untouched** — plus one line in the round's 인계 대조 표 and
  in the report. **「로그에 자리가 없는 규칙은 로그로 검증되지 않는다」** is the general form.
- Same-shaped sibling: 규칙 D-8 (버튼 문구는 로그에 남지 않는다). Grouping the two as 「이 검증이 닫지 않은 것」
  under the rule block keeps a reader from reading the ✅ table as total.

### 35-8. Confirming the **form** of existing strings (a line break) is not a new state — one table, cell-level pointers

The user split every status-line string into two lines at the 남은 초 sentence.
- 🔴 **One 4-row table is the single source of the 개행 형태**, placed in the rule that owns the status line
  (규칙 D-7). The three places that already carry strings — 규칙 D-1's 표 and D-7's own two earlier tables —
  get **cell-level pointers** (「그 칸은 이 블록이 단일 소스」), the third sighting of §33-6 / §34-3.
  A `⏎` legend plus 「두 문장 사이의 공백 한 칸이 줄바꿈으로 바뀐다」 is what makes the table implementable.
- 🔴 **Say why it must be all four states**: the status line is **one place** whose text swaps per state, so a
  per-state form difference breaks that property on screen.
- ⚠️ **What is excluded needs its measurement too** — 알림 팝업 본문 is out of scope **because it has no 남은 초**
  (verified in code: the constant has no `{0}`), plus 🔴 「팝업 본문에 남은 초를 새로 넣지 않는다」 so the exclusion
  cannot be closed later by adding one.
- 🔴 **The one item where the order cannot be applied literally stays 미정** — 평시 문구 is a single sentence, so
  "break before the seconds" would produce an **empty first line**. Write the two readings, say which argument
  supports each, and refuse to choose (규칙 10 · 12). **Filling it would invent a spec.**
- ⚠️ Add the risk the user cannot see from the text: **개행으로 텍스트 높이가 늘어 레이아웃이 밀릴 수 있다 · 실기 미확인**,
  and 🔴 the code state at **my** reading time (개행 0건) marked as a §33-8 timestamped observation.

### 35-9. A bug where the mechanism is correct and the **interleaving** is the defect

「실패 직후에 「재경기 준비 중」으로 되돌아간다」: the accept notice arrives by two paths (local immediate + server
`ClientRpc`) and the accepting side gets **both** — which the rule **deliberately** specifies. The defect is that
failure handling lands in between and clears the duplicate guard.
- 🔴 **Do not write 「경로가 둘이라 결함이다」** — name the rule that made it two and say 「결함은 그 사이에 실패 처리가
  끼어들 수 있다는 것」. Otherwise the fix removes a role-asymmetry guarantee.
- ✅ **My log measurement can only say it is *consistent with* the code diagnosis** — a 회차 비교 표 (에디터가 요청한
  쪽 4회 → 진입 1회 / 수락한 쪽 1회 → 진입 **2회**) is strong corroboration, and the mechanism stays credited to
  the main session's code reading (근거 등급).
- ⚠️ **Kill the "only with the debug tool" reading with a number**: the observed gap between 수락 접수 and 실패 처리
  완료 was **12ms**, so a fast real failure does the same. A severity claim without that number gets deprioritised.
- 🔴 **Say what the user loses**: the fourth state (실패) is void for 20 seconds *and* the auto-return timer is
  stopped during it. And keep the fix in **방향/진행 중** tense (§31-7).
- ⚠️ **Separate the benign look-alike** in the same log — one round logged 실패 상태 진입 twice (한도 만료 then a
  9ms-late 실패 통보) which is the rule working as written; write 「이것은 버그가 아니다」 beside it, or the two get
  filed as one.

---

## §36. Closing a pinned 「⚠️ 미정」 with the user's decision — and grading **scene-file measurement** apart from **on-screen confirmation** (2026-09-27 (2차), post-game-leave-ui 평시 문구)

The whole round was **one cell**: §35-8 had pinned the 평시(no-preceding-sentence) wording as ⚠️ 미정 with two readings,
the implementation round had shipped reading ① (a leading `\n`), and the user then confirmed reading ② (한 줄).
Nothing else was in scope.

### 36-1. The shape of closing a pin — fill the cell **without** deleting the pin

Three parts, all additive (`.claude/MEMORY.md` B-7):
1. **The table cell** gets **확정값 + `~~struck pin text~~` + a pointer to the 확정 block**
   (`| 평시 | <value> — ⏎ 0개 ~~⚠️ 미정 — …참조~~ → 아래 「✅ … 확정」 블록이 이 칸을 채웠다 |`).
   Striking the pin inside the cell is what keeps the cell readable *and* keeps the history — deleting the pin text
   would make the 미정 소절 below look unanswered.
2. **The 「why is it 미정」 소절 is not touched at all.** The answer goes **below** it as a new
   `**[✅ <date> 확정 — 읽기 ②(…)]**` block. A reader who arrives at the question walks straight into the answer.
3. 🔴 **The 확정 block must say which reading won *and why the other one's rationale does not hold*** — otherwise the
   next reader re-opens the same debate. Reading ①'s stated reason (「빈 줄로 줄 수를 맞춰 화면이 튀지 않게 한다」)
   was **measured to be inapplicable**, and saying so is the substance of the block.

### 36-2. 🔴 「씬 파일 실측」 and 「실기 화면 확인」 are **different grades** — split them in a table

The measurement resolved the *layout* risk but not the *appearance* question, so a single ✅ would have been a lie.
Two-row table inside the risk item:

| 대상 | 근거 | 등급 |
|---|---|---|
| the three wordings that got a `⏎` | ~~user said *"두 문구는 정상적으로 나왔어"*~~ → 🔴 **that report predates the newlines — see §36-8** | ~~✅ 사용자 화면 확인~~ → 🔴 **화면 미확인** (all four are) |
| **the newly-decided one-line form** | scene-file measurement only | 🔴 **이 변경 뒤 화면 미확인** |

- **Never delete the original 「위험 — … 🔴 실기 미확인」 item.** Append 「그 위험이 말한 *X* 는 해소됐다」 and then the
  grade table. A risk that is *half* resolved is written as half.
- The same split survives the follow-up round where the **code** reached the confirmed value: ✅ 코드 반영 확인 and
  🔴 화면 미확인 sit in the same block, with one sentence saying **「코드가 확정값이 된 것과 화면으로 본 것은 다른 사실」**.

### 36-3. What makes a layout claim measurable in a `.unity` file

The claim 「줄 수가 늘어도 형제 UI 가 밀리지 않는다」 is decided by **four** readings, and all four belong in the
document as a table (they are the reason the decision is not a preference):
1. the text object's **component list** — `ContentSizeFitter` 0건 · `LayoutGroup` 0건 (read the `m_Component` ids and
   resolve each `--- !u!<class> &<id>`: 224 RectTransform / 222 CanvasRenderer / 114 MonoBehaviour → check its
   `m_EditorClassIdentifier`),
2. the **parent's** component list (a `LayoutGroup` one level up would move the child anyway),
3. the anchors + `m_SizeDelta` (a fixed rectangle means size is independent of content),
4. `m_fontSize` + `m_enableAutoSizing: 0` + alignment (Center/Middle → extra lines spread around the middle).
- 🔴 **Only with 1+2 may you write 「성립하지 않는다」** about a height-cascade risk. Without the parent check it is a
  guess about the very mechanism the risk named.

### 36-4. A reusable grep trap — a constant whose **declaration wraps onto the next line**

`RematchFailedByOpponentLeftCountdownFormat` has `private const string … =` on one line and the string literal on the
next. **A single-line `grep "Format = \"…\""` misses it**, so a count of 「문구가 코드에 몇 곳 있나」 comes back short.
- Use `grep -n "<name> = " -A1` (or grep the *wording* rather than the assignment) whenever counting wordings.
- Record the wrap in the document cell itself, not only in the report — the next person recounts from the document.

### 36-5. Counting 「사본이 늘지 않았다」 when the new text legitimately repeats the wording

The 확정 block itself must quote the wording (that is what makes it the single source), so a raw count goes **up**.
The verifiable claim is therefore **not** 「히트 수가 같다」 but 「모든 새 히트가 단일 소스 블록 안에 있다」:
grep the wording with line numbers, then classify each hit as *inside the single-source block* / *pre-existing other
cell* / *new copy elsewhere* — and report that the third class is **0건**. The three cells the earlier round had
marked as 「이 블록이 단일 소스」 (규칙 D-1 문구 표 · 2026-09-22 (2차) ② 표 · 2026-09-24 표) are exactly the cells that
must **not** grow a copy.

### 36-6. A code-side copy is part of this job's verification, not of the code job

The same programmer round replaced a wording example inside a `[Tooltip]` with 「문구의 단일 소스는 네 상수다」.
- 🔴 It belongs in **§15-7** (the section that decided 「사본을 늘리지 않는다」), because the tooltip was the last place
  where that principle was broken on the code side. Say *why* it belongs there, or it reads as scope creep.
- ⚠️ **Qualify the 「상수 4개뿐」 claim**: wordings held **as string literals** are 4, and the remaining grep hit is an
  explanatory comment quoting the first half of one wording. 「4개뿐」 without that clause is false.

### 36-7. The 실행 기록 표 is not touched by a 확정 마무리 — and saying so is the artifact

This round added no stage. Before leaving §10 alone I read every row and confirmed **no row asserts anything about
개행 or the 평시 wording**, then wrote that sentence into the new 「바뀐 파일」 subsection. A silent non-edit is
indistinguishable from an oversight; one line makes it a decision.
- The previous round's own 「바뀐 파일」 절 (§15-9) is a dated record → **new numbered subsection (§15-10)**, never an edit
  (§24's rule, third sighting).

### 36-8. 🔴 A handed-over fact whose **quote was real and whose attachment was wrong** (9th class)

The calling session's prompt said the three newline-split wordings were *already confirmed on screen* — quoting the
user verbatim (*"두 문구는 정상적으로 나왔어"*). The quote existed; **what it was a report *about* did not match.**
That report was of the build **before** the newlines went in, and the user's newline *request* came **after** it —
nobody can have seen on screen a thing that did not exist yet.
- 🔴 **This is the form no existing check catches.** Grepping the quote proves only that the sentence is real.
  The previous handed-over-value classes were *wrong number* / *right number wrong conclusion* / *different counting
  basis* / *right count wrong labels* / *unmeasurable here*; this one is **wrong attachment** — who said it, when,
  and **which build they were looking at**.
- **Verification means**: ask the **calling session** to run `git show`/`git log` on the constant and compare the
  commit that introduced the change against the timestamp of the user's report (I must not run git — CLAUDE.md 규칙 5).
  **[🔴 2026-10-06 correction — original kept: the parenthesis is no longer accurate.** `CLAUDE.md` rule 5 now allows read-only `show`/`log`; I may run them myself. The *method* (compare the introducing commit with the report time) is unchanged.**]**
  In this case the newlines first appeared in the then-HEAD commit, i.e. *after* the report.
- **The repair is three-part and additive**: strike the grade cell and put the corrected grade beside it, append a
  정정 block carrying the evidence **and the fact that the calling session handed it over and I quoted it unverified**
  (`.claude/MEMORY.md`'s *「에이전트 프롬프트에 적힌 사실 주장은 검증 없이 인용하지 않는다」* — a repeat of the 2026-08-21
  incident), and 🔴 **say what the report *does* validly confirm** (here: 실패 사유별 문구 분기 / 규칙 18, **not** 개행),
  so the other documents citing it stay untouched and still true.
- ⚠️ **Before writing 「사용자가 화면으로 확인했다」 about a *form* (개행, 정렬, 색), ask which build the user saw.**
  A screen report confirms the thing that was on screen at that moment — a later form change inherits nothing from it.

---

## §37. A defect the **screen states and contradicts at once** — pinning a button's enable condition as a single source, and why "not test-only" is a **structural** claim, not an observation (2026-09-27 (3차), post-game-leave-ui 규칙 D-2)

**The round.** 실기 showed the status line saying *「the opponent left, so a rematch cannot start」* while the **「다시하기」 button came back enabled**. The user picked 안 A — enable only when **the opponent has not left AND the failure cause is not 「연결 끊김」** — and ruled out ever re-enabling a button once it is off. My job: append the revision to 규칙 D-2 without touching its body, put **pointers only** on D-3 and D-8, add a Plan subsection, and check the execution table for cells the new fact falsifies.

### §37-1. 🔴 A condition that two rules already half-own gets **one home and pointers**, and the home block says so in its first line

The enable condition is read by 규칙 D-2 (the body that turns it off), 규칙 D-3 (what stays on), 규칙 D-8 (텍스트 vs. interactable), and 규칙 M-3 (restore everything on failure). Four rules could each have carried a copy.
- The revision block's **first line** is *「이 블록이 … 단일 소스다. 다른 규칙과 다른 문서에는 포인터만 두고 조건을 옮겨 적지 않는다」*, naming the four rules. Without that sentence a later editor adds the condition to whichever rule they are reading.
- The pointer blocks on D-3 and D-8 **never restate the condition.** Each says three things: the condition narrowed, **this rule did not change**, and 🔴 **why this rule is what made the decision possible** (D-3: the exit is always open, so leaving the button off does not trap anyone; D-8: text restore was *already* separated from interactable, so narrowing the button half cannot re-freeze 「요청 중...」).
- That third sentence is what makes a pointer worth writing. *「바뀌지 않았다」* alone invites the reader to check for themselves; *「이 규칙이 그 개정의 전제다」* tells them what breaks if they revisit it.

### §37-2. 🔴 A rule that is **narrowed, not replaced**, needs the word "AND" in the document

*「`!상대 이탈` 은 제거되지 않았다 — AND 로 좁혀졌을 뿐이다」* is its own sentence, and the 활성/비활성 table spells out **활성 = both, 비활성 = either one**. A revision that reads like a replacement invites deleting the old body; a revision that reads like a narrowing does not.
- The neighbouring rule that **does** change behaviour (규칙 M-3, 「restore all options」) gets a row saying 🔴 **it is not overturned — one cause is carved out of it.** A verdict column with three values (`✅ 그대로` / `✅ 그대로` / `⚠️ 사유에 따라 갈린다`) makes the blast radius decidable without reading prose.

### §37-3. 🔴 「이것은 테스트 전용 문제가 아니다」 is a **structural** claim when every observation came from the debug flag

Every `Cause=OpponentDisconnected` in the logs was produced by the 개발용 강제 실패 플래그 (`Forced=Disconnected` 4건, server side). So the *screen contradiction* is observed, but **a real disconnect producing this cause is not.** Writing "this also happens in real disconnects" as if measured would be the 과대 표기 the project bans.
- The repair is a **separate evidence grade**: 🔴 **구조 추론(관측 아님)** in the grade table, and in the body *「근거는 관측이 아니라 구조다」* followed by the two facts that carry it — a real disconnect sends **no 정상 퇴장 통보**, so the only thing that closes the window is the silence watch, whose `TimeoutSeconds=30` appears on **every** 감시 시작 line.
- 🔴 **Measure the window inside one machine.** The handed-over interval (30.35초) subtracted an **editor** timestamp from a **device** timestamp. The screen that lied belonged to the device, so the device-only figure (30.234초) is the one that answers the question. Record both: *「인계값은 맞고 기준이 두 기기에 걸쳐 있었다」* — do not rewrite the handed value.
- ✅ **Look for the longer case before calling a duration typical.** A second round measured **60.270초** because the opponent left via its own 60-second auto-return instead of going silent. So *「약 30초」* is a **하한**, and 🔴 the upper bound *cannot* be stated by the rule — it depends on when the opponent disappears. The handed-over claim was right and short (§20-7's class), and the correction made the conclusion **stronger**.

### §37-4. 🔴 The adopted option is worth less than **why it is not a new rule** — and the rejected one is worth its own subsection

The decisive sentence is: the 문구 were already split on *「앞쪽은 다시 시도할 여지가 있고 뒤쪽은 없다」* (`GameSystemRules_RandomMap.md` 규칙 18), so **only the button was not following a judgement the document already made.** Write that as *「새 규칙이 아니라 문구와 버튼을 같은 판단 위에 올려놓는 것」* — it is what stops the next reader from hunting for a new rule number (§32's four questions, answered in the negative).
- **The rejected option gets its reasons, in the rule document, at rule level.** 「30초 뒤에 되살린다」 was rejected for (1) the button flickering and (2) **adding a fourth clock to a screen whose 규칙 D-7 already warns 「섞어 읽지 않는다」**. Reason (2) is the reusable one: it cites the existing warning instead of inventing a new principle.
- 🔴 **The cost of the adopted option is part of the record.** *「대가 — 일시적 순단이었다면 재시도할 수 없다 / 얻는 것 — 화면이 거짓을 말하지 않는다」*. A 확정 written without its cost reads as if there were no trade-off, and the next person reopens it.

### §37-5. 🔴 An execution table can be **not-false and still over-read** — mark the cell's scope, never add a row

The instruction was: no new rows, update only a cell the new fact contradicts, and if none contradicts, **say so in one line**. Neither answer was quite right.
- The 단계 7 cell records 사용자 실기 확인 of 「재경기 버튼 잠김」. That observation was of the **이탈 판정** trigger; the new half of the condition has a **different trigger** (실패 사유). So the cell is **not false** — but *「재경기 버튼 잠김 = 규칙 D-2 검증 완료」* becomes false the moment someone reads it that way.
- The repair is a **범위 마커 inside that one cell**: state what the observation was of, state that the condition gained a term whose new half is 화면 미확인, point at the single source, and 🔴 end with **`✅ 이 행의 기존 서술이 거짓이 된 것은 아니다 — 범위가 규칙보다 좁아진 것이다`**. The status cell and every other row stay untouched.
- ⚠️ **Then still write the survey result as a line of its own** (§36's closing lesson): which rows were checked and why they do not conflict (단계 9 claims only 문구; 단계 8's bug is the alert popup). 「어긋나는 칸이 없다」 and 「한 칸의 범위가 좁아졌다」 are different findings and both are the artifact.

### §37-6. 🔴 A handed-over ✅ that draws a **conclusion** from a count — verify the conclusion, not the count

The handoff read *「Guard=AcceptAlreadyHandled 가 매 회차 1줄 · 2차 준비 진입 0건 → 직전 커밋의 버그 수정이 실기에서 작동함」*. The counts matched (10 rounds each side, 0 double entries). **The conclusion did not.**
- In all six failing rounds the second accept signal arrived **2~4ms before** the failure notice, so the guard caught it **first** — the very ordering the bug needs (failure clearing the flag *between* the two signals) never occurred. And in all six the accepting side was the **Client**, whereas the bug was observed with the accepting side being the **server**.
- So the row is written as **「증상이 이 로그에 0건이다」**, not 「수정이 작동한다」, with a bullet naming the reproduction condition that is still unmet. This is the same shape as the 2026-08-24 lesson *「인계문이 X 가 정상 동작했다고 하면 X 를 실행하는 코드 경로의 조건문을 읽는다」* — here the "condition" is **timestamp order**, which only the log can show.
- **Handed-value mismatch class 10 — the criterion's *name* was wrong while the split was real.** *「Signal= 이 역할(에디터 ServerNotice / 실기기 LocalAccept)에 따라 갈렸다」* held only for the round the sender looked at; the log shows **both machines recording both values**. The real criterion is **「that round's accepting side」**. ✅ The conclusion (설계대로) survives — what changed is the name, and that is worth its own 결함이 아니다 entry so the next reader does not "fix" a non-bug.

### §37-7. 🔴 Retracting an **inference about the test environment** — the retraction's content is the list of what the log *can* show

The calling session had asserted *「round A had Unity's Error Pause on and round B had it off」*. 🔴 **Error Pause is an editor setting and leaves no trace in a runtime log**, so no log can decide it; the user caught it and it was retracted.
- The record replaces the assertion with a table of **only what the log shows**: round A's countdown started, **no 「만료」 line**, and an actual elapse of 67.319초 against a 60-second timer ⇒ **frames did not run for some interval**; round B's timer expired normally at 60.250초; and 🔴 **a play-session boundary between them** (`LoginBootstrapper`) ⇒ *「설정을 바꿀 기회는 있었지만 바꿨다는 기록은 없다」*.
- 🔴 **The user's own report is confirmation of the pause, not of its cause.** Quote it and label it that way; then write the **closing procedure** (turn Error Pause off, repeat the forced failure 2~3 times, see whether 60 seconds complete) and *「확인 전에는 이 항목을 ✅ 로 적지 않는다」*.
- ✅ **A second machine can corroborate the same interval from the other side** — the device measured `SilenceSeconds=30.6` over that window. Say what that does and does not add: it confirms the opponent was quiet, not why.

### §37-8. Recording 「this is not a defect」 for a symptom the user suspected

Round B's device switched to 「상대방이 떠났습니다」 while the editor showed a failure — the user suspected **양측 비대칭**. It was correct behaviour: the editor really did leave at 60 seconds, so the notice was **true**, and 🔴 *「떠난 쪽과 남은 쪽은 본래 다른 화면을 본다」*. Written as a numbered 「결함이 아닌 것」 list beside the Signal= naming correction, each ending with **why the design intends it** (우선순위 「이탈 > 실패」 per 규칙 D-7). Same shape as §24's 「알려진 현상」 and §31's 「correct behaviour that looks like a bug」 — the point is that the *next* person suspecting it finds the answer instead of re-opening it.

### §37-9. 🔴 The 미결 goes in **both** documents with the same sentence and no answer

*「상대가 새 요청을 보내왔다 = 상대가 살아 있다 = 실패 사유가 거짓으로 판명됐다. 꺼 둔 버튼과 실패 문구를 되돌려야 하는가?」* — outside this approval. It is pinned in the 규칙 D-2 revision block's last ⚠️ item **and** in the Plan subsection, each adding *「이것은 안건이지 선택지 목록이 아니다」* and 🔴 *「두 자리의 결론은 「정해지지 않았다」 하나뿐이다」*. Two homes are safe here precisely because neither carries content that can drift — an open question has no value to go stale.

### §37-10. Verification when the whole round is letter-numbered rules

`check_docs.py` = **0건**, and 🔴 **that says nothing about 규칙 D-2 · D-3 · D-8** — the checker cannot see letter-prefixed rules at all, and `_Tasks/` is outside its scan set, so the Plan file was not examined either. Hand checks that did the work:
- **Link targets resolved by path** from each file's own directory (`../_Tasks/…/Plan.md` from `GameSystemRules/`, `../../../GameSystemRules/…` from the task folder) plus the two log files cited.
- **Table pipe count per row — counting only *unescaped* pipes.** A naive `count('|')` reported three uneven tables in the Plan; all three were pre-existing rows quoting log lines with `\|`. Use `(?<!\\)\|` or the check produces false alarms every time a log string is quoted.
- **`grep -cF` on the exact sentences that must stay untouched** (규칙 D-2's two body lines, 규칙 D-3's opening line, 규칙 D-7's 등급 표 row) — each **1건**, i.e. present once and unmodified. 🔴 A block that is supposed to leave a body alone is verified by finding that body intact, not by remembering not to touch it.

## §38. Two more same-day test rounds close everything §37 left as 「screen-only」 — a third verdict for a pinned 미정, and grading by whether the log has a place for the claim at all (2026-09-28, post-game-leave-ui Plan §15-12 / §15-13)

This round did **zero** code/scene/prefab edits (confirmed by reading both sub-rounds' 「바뀐 파일」 lists — both say so explicitly). All of it is: a bug from §37 (규칙 D-2's 회귀) finally reproduced in its **original** race condition, that same rule's two branches observed splitting apart, and — across a first 실기 session and a second one the same day — every remaining ⚠️「화면 미확인」 item either closing or staying open with its own reason.

### §38-1. A pinned 「⚠️ 미정」 has a **third** ending: void, not decided

§36 already established the two-part close for a pin the user decides (확정값 + `~~취소선~~` pin + pointer to a 확정 block). This round adds a verdict §36 didn't have: **the question's own premise stops being possible, so the agenda item disappears** — nobody chose an answer.

The pinned question (규칙 D-2's 2026-09-27 (3차) block, last ⚠️ item) was *「상대가 새 재경기 요청을 보내오면 꺼 둔 버튼·실패 문구를 되돌릴 것인가?」* — asked because a live opponent sending a fresh request would prove the "disconnected" verdict false. §15-13-3 / the rule doc's 2026-09-28 (2차) block close it this way: when the failure cause is `OpponentDisconnected`, **both** sides' rematch buttons are disabled by the same code path (§38-4 below), so **neither side has a live button to send a new request from** — the situation the question worried about cannot occur. 🔴 **Writing this as 「정했다 — 되돌리지 않기로」 would be false** — nothing was decided; the premise evaporated. The wording used, verbatim, is *「어느 쪽으로 정한 것이 아니라 전제가 성립하지 않아 안건이 무효가 됐다」*, and the scope is stated narrowly too: this void verdict applies **only** to the `OpponentDisconnected` cause — for any other cause the button stays enabled, so the pinned question's premise (button is off) never applied there in the first place. Both homes (the rule block and Plan §15-11-7) keep the original 「정해진 바 없다」 sentence untouched and append the void note under it (B-7) — a decided-vs-void mixup is exactly the kind of thing a future reader would misread as "the team chose not to."

**When to reach for this third ending**: whenever a 미정 question is phrased as *"if condition X happens, do we do Y?"* — check whether the code now makes X structurally impossible before assuming someone has to pick Y or not-Y.

### §38-2. Grading by "does the log have a place for this claim at all" — four grades, never merged

Both sub-rounds spend most of their length verifying UI behaviour (button color, button enabled state, label line count) that **cannot appear in a runtime log by construction** — Unity doesn't log render/inspector state. The round's evidence-grade table (§15-12-1, §15-13-1) names this explicitly as its own row: 🔴 **「로그에 자리가 없어 판정하지 않는 것」 = 버튼의 활성 상태·색 / 글자의 줄 수·위치 / 버튼 문구 / Unity 의 Error Pause 설정**. Once that row exists, four other grades sit around it and must not be blended into each other:

1. **Direct log observation** — a server RPC line, a state-transition line, a timer's start/expiry line. Countable, re-measurable.
2. **🔴 Indirect log inference** — inferring a UI state from a *proxy* the log does record. Here: "did a `RequestRematchServerRpc` go out after the failure?" stands in for "was the 다시하기 button enabled?" Every table cell built this way carries its own limit spelled out next to it: ⚠️ *「눌리지 않았다」이고 「눌릴 수 없었다」가 아니다* — zero requests proves the button wasn't pressed, not that it couldn't have been. This grade is **new** relative to §27/§37's grade lists and is worth keeping distinct because it looks like direct evidence (it's a log line!) while carrying an inference gap direct evidence doesn't have.
3. **User screen confirmation** — §15-13 closed exactly two items this way (실기기 다시하기 회색 / 네 상태 줄 문구), each phrased as *"the user was shown the specific claim and confirmed it"*, not a general "it worked."
4. **Structural/code evidence** — reading the method body (`NotifyRematchMapFailedClientRpc` has no `ClientRpcParams`, so both sides receive the same cause) or the scene (no `ContentSizeFitter`/`LayoutGroup` on the status line's parent, so nothing can push neighbours around). §15-13-1 calls this out as its own row too and warns 🔴 **it does not merge with user screen confirmation even when both point the same way** — one round's layout claim stayed at "structural only, no screen report" specifically *because* the user's answer didn't mention layout even though the question offered the chance.

The reusable form for a table cell in this situation is: **claim | grade | what the grade does and does not cover**, with the "does not cover" clause mandatory whenever the grade is 2 or 4.

### §38-3. A self-declared closing condition, satisfied on a later round — and how far "closed" reaches

§15-11-4 (an earlier round, kept verbatim) had pinned its own closing condition for a suspected frame-freeze: *「Error Pause 를 껐음을 확인한 상태에서 강제 실패 ①을 2~3회 반복해 매번 60초를 채우는지 본다」*. §15-12 ran it once (not enough — "1회뿐이라 성립하지 않는다", correctly refusing to close early), and §15-13 ran it a second time, bringing the same-day total to **2 completions of a 60-second countdown with zero unexpected errors** — the condition's own number (2~3) was satisfied at the low end.

🔴 **What closes is exactly what the condition said, no more.** The condition's wording only ever supported "this is not a code problem," never "the cause was Error Pause" — that setting still leaves no trace in any runtime log, and no screenshot of it being off was ever received. So the closing note is written as: ✅ *「이 코드의 문제가 아니다」까지* / 🔴 *「원인이 Error Pause 였다」로는 적지 않는다*, and the original sentence in §15-11-4 (*"그 정지의 원인은 미확정이다"*) is left untouched — it is still true, just for a narrower reason than before. **The general lesson**: when you write a closing condition into a document, phrase it to name *what specifically* gets closed when it's met, because the round that later satisfies it will otherwise be tempted to close more than the condition earned.

### §38-4. Verifying the calling session's own procedure claim — a repeat of the §37 pattern, this time in a test script

§37 already recorded that "not test-only" needed a structural-inference grade because the observed instances all came from the debug flag. This round produced a fresh instance of the same class — **a claim in the *test procedure itself*, not in a status document**: the main session's step list read *"【실기기】 「다시하기」를 한 번 더 누릅니다 (실기기 쪽은 활성일 수 있습니다)"*. The user's screen showed the opposite (비활성), and reading the code settled which side was right: `NetworkGameEndController.NotifyRematchMapFailedClientRpc(int cause)` is a `[ClientRpc]` with **no** `ClientRpcParams`, so the host executes the same RPC body locally and both sides receive an identical `cause` — there is no code path that could make one side's button outcome differ from the other's. The XML comment on that method had already said why both must be restored together (요청한 쪽은 잠겨 있고 수락한 쪽은 아무 일도 안 일어난 상태— 둘 다 되돌려야 한다), so the wrong guess wasn't a coin flip, it was unread code.

🔴 **Why this is worth its own entry and not just "verify handed-over facts" again**: the earlier instances (§27, §29-4, §37) were all claims about **what the code currently does**, written into a status document. This one is a claim about **what a test procedure's step should expect**, and hedged with *"~일 수 있습니다"* rather than stated flatly. That hedge is singled out as the worst form a procedure sentence can take: 🔴 *"~일 수 있습니다"는 절차 문장에서 가장 위험한 형태다 — 읽는 쪽은 그것을 「그래도 된다」로 읽고, 실제 기댓값이 그 반대면 버그를 정상으로, 정상을 버그로 읽게 된다.* The corrective habit stated alongside it: **if you haven't confirmed the expected value, write "확인되지 않았다 — 어느 쪽이든 보고해 달라," never a hedge that sounds like a real answer.**

This lands in `.claude/mistakes.md` (2026-09-28, second of two entries that day) as its own item, distinct from the neighbouring entry about writing a procedure that assumes the reader's prior knowledge — 🔴 **same root (unverified content went into a procedure), different symptom** (one entry is a *missing* detail, this one is a *wrong* one).

### §38-5. Two opposite growth patterns for the "same" kind of log file — cite a line range, and check whether a later file is a superset

The editor's `RuntimeLog.txt` for the day is **not** overwritten between test sessions — it accumulates. This round's own numbers: 1st session ends at line 359; the 2nd session's header (`=== 세션 시작: 2026-09-28 02:19:15 ===`) starts at line 360; the file totals 718 by the end of the day. 🔴 **"I read `_editor/2026-09-28/RuntimeLog.txt`" says nothing about which session's data was actually read** — cite the line range every time (`1~359행` vs `360행 이후`), the way §15-13-1's evidence-grade row does.

The device log does the **opposite**: each test session gets a **new folder** (`02_01_logcat/` for the 1st session, `02_38_logcat/` for the 2nd), not a new line range in the same file. But the two folders are not necessarily disjoint either — this round's 2nd-session file turned out to be a **superset** containing both the 1st session's PID (`1742`, 01:51~01:58) and the 2nd session's PID (`24881`, 02:22~02:37) in one file, which was confirmed by re-checking that the values already recorded from the 1st-session folder reproduce identically inside the 2nd-session file before relying on that overlap for anything. 🔴 **Don't assume a same-day log file or folder is scoped to only the round you're currently writing about — for an append-style file, cite the range; for a per-round folder, check for overlap before citing it as if it were disjoint.**

### §38-6. Mixing two devices' clocks a second time — this time the wrong value was self-made, not handed over

§15-11-8 (an earlier round) had already caught this class once, in a **handed-over** number. This round caught it again, in a number **this agent itself wrote** in the immediately preceding sub-round: §15-12 recorded *「정상 퇴장 통보 전송부터 반영까지 349ms」*, computed from the sender's timestamp (device) and the receiver's timestamp (editor). §15-13's second session supplied the proof that this cannot be a real latency: in that session, the device's **send** timestamp (`02:22:32.712`) is *later* than the editor's **receive** timestamp (`02:22:32.563`) for the same message pair — a receive that precedes its own send is impossible, so the two machines' clocks are offset by at least 149ms. The fix is the same move as §15-11-8: measure the interval **inside one machine's own timeline** (here: editor's receive-line to editor's apply-line = 4ms) and state that as the usable number, while the original 349ms sentence is kept and a correction is appended under it rather than edited in place (B-7).

🔴 **The reusable rule, restated because this is the second time it was needed**: never compute an interval from two different devices' log timestamps, even when both devices are logging the "same" event, unless you've first confirmed the two clocks agree (and usually you can't). When you must relate a sender-side and receiver-side moment, report only "receiver saw it N ms after receiver's own prior line," never "sender's timestamp minus receiver's timestamp."

## §39. Closing several backlogged 「⚠️ 미정」 pins and a 「🔴 current code differs」 snapshot in one same-day round — and how to describe what a sibling agent is building right now (2026-09-28 (3rd), post-game-leave-ui Plan §15-14)

This round's task was explicitly framed by the calling session as "three already-decided facts that are behind on being written down" — not new investigation, not new design. That framing matters for how the round is structured: no new decisions are made here, only closing pins with citations to who decided what and when.

### §39-1. A 「🔴 current code differs from the rule」 snapshot block has the same expiry problem as any other 「current state」 sentence

Rule D-8's 2026-09-22 block recorded that `RestoreRematchButton()`'s `!_opponentLeft` guard wrapped *both* the button-text restore and the `interactable` flag together, so the text never came back after an opponent-left verdict. By the time this round re-read the same method, the code already separated them (text restore unconditional; `interactable` gated by two ANDed conditions), and the method's own comment block documented the change verbatim (*"[2026-09-22 수정] 문구 복원을 `_opponentLeft` 가드 밖으로 꺼냈다"*). 🔴 **The lesson isn't "that snapshot was wrong" — it was accurate when written. It's that any 「현재 코드는 이렇다」 sentence is a snapshot with an unstated expiry, and the next person to touch that rule must re-read the code rather than trust the sentence**, exactly the way `.claude/MEMORY.md`'s auto-injection-boundary section already warns for *prompted* claims — this is the same warning applied to a *self-written* claim from an earlier round of the same document.

Closing it followed the established §36 pattern (confirmed value + struck-through pin + a dated confirmation block appended below, original untouched) but added one thing that pattern hadn't needed before: because the discrepancy was between "code as documented" and "code as re-read," the confirmation block had to state *both* — the user's screen confirmation (behavior correct) *and* the code re-read (why the 2026-09-22 diagnosis no longer applies) — as two separate evidence rows, not folded into one sentence. Stating only the screen confirmation would have left a reader wondering whether the documented code bug was ever fixed; stating only the code re-read would have left "confirmed" resting on this agent's own reading rather than the user's test.

### §39-2. The same unresolved-item sentence was duplicated across three separate verification blocks — each needs its own short pointer, not a rewrite

Rule D-7's real-device verification write-ups (2026-09-27, 2026-09-28 1st session, 2026-09-28 2nd session) each independently noted, in their own words, that "Rule D-8 is not confirmed by this log" (button text doesn't appear in logs). Grepping for the claim (*"규칙 D-8"* near *"확인되지 않"/"닫지 않은 것"*) found all three. Each got the **same** one-line addition — a dated pointer naming the block that now closes it — rather than being rewritten to say "closed," because each sentence is a true statement about *that specific verification's* scope (a log-based check inherently cannot see button text) and remains true as a statement about what that log proved. The single source of "is D-8 closed" lives in one place (the rule's own confirmation block); the three verification write-ups only get pointers to it. This is the same shape as §27's "confirmed and current code coexisting in one document" problem, but for the mirror case: here it's *three* copies of the same negative claim rather than one document mixing tenses.

### §39-3. Closing several sibling pins in one round — one block, an explicit boundary line

Rule M-4 had accumulated three live pins from earlier rounds — display copy, loading-UI handling, button labels — plus two points the user settled that were never pinned at all (retry-count policy, which screen keeps/loses the result panel). All five were decided by the user in the same conversation turn, so they went into **one** confirmation block rather than one per pin (unlike §28's or §22's cases where a single narrow question closes). But the block had to add an explicit boundary sentence the source material forced: *"this confirmation settles copy/loading-UI/labels/retry-policy only — the concrete screen layout for how a first-match failure and a rematch failure each surface these is still undecided, and belongs to game-design-lead."* Without that sentence, a reader could easily read "the pins are closed" as "the feature is designed," which it is not — the four settled items are inputs to a design that hasn't happened yet. 🔴 **When one user turn closes several pins at once, state the boundary of what was settled as its own sentence, not just as an absence of a sixth bullet** — an absence is easy to miss, a boundary sentence is not.

### §39-4. Observing a sibling agent's in-progress work — grade it by "what was asked of this round," not by how finished it looks

While re-reading `NetworkGameEndController` for an unrelated fact, this agent found a fully-formed new file (`Infrastructure/Debug/ForcedAcceptSendFailure.cs`) and wired-in code implementing exactly the reproduction tool that a still-open item (rematch-failure path ①, "accept never reached the server") had been waiting on. The code read as essentially complete — header comment, editor menu pattern reused, release-build compile guard, the works.

🔴 **It was still recorded as "in progress," not "closed," and that call did not depend on judging the code's completeness.** Two reasons, independent of how the code looked: (1) the calling session's own framing named this file as something a sibling agent is *currently* editing, so any snapshot of it is explicitly time-boxed, the same caution as §29's mid-edit observations; (2) closing an item requires the same evidence grade the rest of this document's open-items table uses (실기 검증), and this round performed none for this tool — reading the source is a code-level observation, not a verification. The reusable rule: when a task's own framing tells you a file is being edited concurrently, describe what you saw as a timestamped observation and let the *stated* status (here: "tool being added") stand, rather than upgrading it based on how done the code looks. Looking finished is not evidence of having been tested.

### §39-5. Re-verifying a handed-over enum count that spans two unrelated failure layers

The handoff's "5 failure reasons, all asset problems" was re-confirmed against `MapPreparationErrorCode` — but the enum actually declares 7 members, not 5. The other two (`SelectionMineCountUnavailable`, `InitialGoldUndefined`) are real error codes, just from an earlier stage (match-selection, before the 100-attempt generation loop even starts) than the "generation-100-times-then-fallback-also-fails" path the handoff and this round's open item were both about. 🔴 **A count that matches is not fully verified until you've also confirmed the count refers to the same subset the claim is about** — re-reading only the five named members and confirming they exist would have missed that the enum has two more members sitting one layer up; the useful re-verification is "these five are exactly the fallback-failure subset, and the other two belong to a different stage," stated as its own sentence so the next reader doesn't assume the enum has only five values in it.

## §40. A dev tool that was "being added" last round actually worked in the field — closing one open item creates a new one, same root as an earlier bug, opposite ordering

Continuation of §39-4 (the sibling agent's in-progress reproduction tool for rematch-failure path ①, observed but not credited). This round's log showed that tool (a 4th forced-failure reason, an exception thrown before the accept RPC is sent) actually firing on real hardware, closing that open item — and, in the same log, revealed a new defect that is the mirror image of an earlier one (§38/regression to "preparing" state), sharing the same root cause but the opposite event order.

### §40-1. "The final state was correct" is not evidence the states in between were correct — only logs catch that

The user reported the screen matched expectations, and it did: after 20 seconds, the failure message returned and the client went to the lobby as expected. But between the failure message first appearing and that final return, the log showed the screen had silently reverted to "preparing rematch..." for those 20 seconds — a state the user never mentioned seeing wrong, because from the outside the run still ended correctly. 🔴 **When the final observable state is correct, a transient wrong state in between is invisible to screen-confirmation and only shows up in the log** — this is a sharper version of §22's "reading the code is not the same as confirming it runs" and §36's "scene measurement ≠ screen confirmation": here even a full screen confirmation of the *outcome* says nothing about a wrong *intermediate* state, because the outcome was engineered (by a second, later-arriving signal) to self-correct. The practical implication: a bug report that only says "it matched the timer and ended up in the lobby" cannot be treated as ruling out a transient regression — only a full log trace of the interval can.

### §40-2. The same bug can recur with the event order reversed — check both orders, not just the one that was fixed

The earlier finding (§15-6 in the Plan, §38 in this memory) was "accept processing finishes, then a failure sneaks in between the two accept signals." This round's finding is the same defect — same missing guard, same visible symptom (failure message reverts to "preparing..." for ~20s) — but with the order flipped: "failure processing finishes *first* (because the accept-send exception publishes failure synchronously, inline, before the local accept handler even runs), then a *late* accept signal for that same round arrives and is accepted as the round's first signal, re-entering the preparing state." Both orders are possible because the same event (`OnLocalRematchAccepted`) has two subscribers processed in a fixed order (network controller first, screen second), and the controller's own failure path can complete entirely inside that first subscriber's call before the second subscriber ever runs. 🔴 **A fix aimed at one race order does not automatically cover the reverse order** — when a defect's root cause is "two independent signals can interleave," enumerate both interleavings explicitly and check each one has its own guard, rather than assuming a single guard closes the whole class. Here, the existing guard (`_rematchAcceptSignalHandled`, tracking "have we handled this round's accept signal yet") correctly handles the *first* order (it prevents a second accept signal after the first was processed) but does nothing for the *second* order, because in that order the accept signal that gets through is legitimately "the round's first signal" — the guard was never designed to ask "but did this round already fail before this signal arrived?"

### §40-3. A code comment that argues against the naive fix is itself the evidence for why the approved fix needs two lines, not one

Before this round confirmed the bug, the codebase already carried an XML comment on the relevant guard field explicitly titled "why guarding with `_rematchFailed` is wrong" — reasoning that a bare `_rematchFailed` guard on the state-entry method would also block the *legitimate* retry path (opponent re-requests after a failure, this side accepts again) because nothing lowers that flag except the very method being guarded and the request button's own click handler — and the accepting side never touches the request button. That comment, written for a different reason at an earlier point, turned out to be exactly the argument for why the user-approved two-line fix has an explicit dependency order: line 1 (lower `_rematchFailed` when the opponent's new request arrives, closing the flag-lowering gap the comment identified) must land before line 2 (add the `_rematchFailed` guard to the state-entry method) is safe to add alone. 🔴 **When re-verifying an approved fix that has not been applied yet, check whether the existing code already explains why the naive version of that fix would be wrong** — it is stronger, self-consistent evidence for the fix's two-step structure than re-deriving the precondition from scratch, and it means the "in progress" state observed is not just "not yet done" but "correctly sequenced so far" (the precondition line was still missing, matching the guard also still being missing).

### §40-4. A dev-tool reason count copied into four documents goes stale in all four at once — grep for the count, fix only what this round's scope named, report the rest

The forced-failure tool's "3 reasons" count (`Disconnected` / `HashMismatch` / `ResponseTimeout`) was quoted, in the same words, in four places: two cells of the Plan's §10 execution table, the `TechnicalDesignDocument.md` section that is the tool's own single source of usage instructions, and the confirmation block inside `GameSystemRules_UI.md` rule D-7. Adding a 4th reason (the accept-send exception used this round) makes all four stale simultaneously, not just the two cited in the handoff. 🔴 **Grep for the count before assuming a two-cell fix is complete, but only touch what this round's explicit "where to fix" list named** — this round's instructions named only the Plan's §10 table cells; the other two (a design document and a rule document, neither explicitly listed) were left untouched and their staleness reported instead of fixed, per CLAUDE.md rule 6 (no scope creep) rather than silently expanding the edit because the same grep happened to find them.

---

## §41. Narrowing a rule's **scope** (not its value) after a user confirmation — auditing every place a "both modes" premise is baked in (2026-09-28, 싱글 맵 준비 실패 UI / 규칙 D-4)

The user confirmed "single-player needs no auto-return timer." The rule that defines the timer (규칙 D-4) never said *which mode* it applied to, so it silently meant "both." The round's job was a full read-through to find every place that premise sits, plus the two approved edits.

### §41-1. 🔴 A rule with **no scope clause** already has a scope — the widest one. Narrowing it is an amendment, not a clarification

규칙 D-4's table read `결과 화면 표시 → 60초` with no single/multi column. Nothing in it was *wrong*; it simply answered a question it was never asked, and the answer readers took was "both modes." 🔴 **Treat the absence of a scope clause as an assertion of the widest scope**, so adding one is a real amendment that needs the user's approval and a dated block — not a wording tidy-up you can do on your own initiative. The amendment block's first line must say **what did *not* change** (here: `표의 값(60초 · 30초)은 바꾸지 않았다 — 바뀐 것은 「어디에 적용되는가」 하나뿐이다`), because a reader who sees a fresh block on a rule about numbers assumes the numbers moved.

### §41-2. 🔴 The hard find is an **exclusion list that is one item short** — and it reads as an inclusion

The second approved site was not a statement about the timer at all. 규칙 M-4 (single-player) carried *「이탈 관련 규정(규칙 D-1 · D-2 · **D-4 의 이탈 재시작** · D-6)은 이 규칙에 적용되지 않는다」*. Every item listed was correct. The defect was the **item that was missing**: by naming only D-4's *leave-restart*, the sentence implied D-4's *base 60s* **was** in scope for single-player. 🔴 **An exclusion list makes a positive claim about everything it omits** — so when a rule's scope changes, grep for lists that *exclude* it, not only for prose that *asserts* it. The repair is a 4-row table whose rows say, per item, **"unchanged from the 2026-09-16 bullet"** vs **"this is the one that changed"**, so nobody re-audits the whole bullet.

### §41-3. Word-grep finds the mentions; only reading the sentence separates "multi, stated loosely" from "both modes"

`grep -rn "자동 복귀\|카운트다운\|60초"` over the living docs produced ~20 hits, most of them noise (AI scenario tables' "60초" build times). The hits that mattered split three ways, and **only reading the surrounding bullets decided which**:
- **Multi, phrased without the word 멀티** — `GameSystemRules_RandomMap.md` 규칙 16 and `TechnicalDesignDocument.md` 「복구 상태」 both read `재경기 맵 준비 실패는 … 자동 로비 복귀 countdown … 복원한다`, immediately **followed by a separate single-player bullet that mentions no countdown**. The pairing with the neighbouring bullet is what makes them multi-only; the sentence alone does not. **No repair needed.**
- **Genuinely ambiguous** — `GameDesignDocument.md` has the same sentence pair, but under a heading that opens `싱글·멀티플레이 모두 …`. Same words, different verdict, because the section header widened them. **Reported, not fixed** (it needs a 개정 이력 row, and the round's approval covered two sites).
- **A true code observation that is not a spec premise** — 규칙 M-3's 2026-09-22 실측 says the auto-return countdown is a `GameEndUI` 싱글·멀티 공용 경로. 🔴 **That stays true and must not be "corrected"** — it describes the code, and the code still does run the timer in single-player. The new blocks say so explicitly rather than contradicting it.

### §41-4. 🔴 Write the **implementation gap** into the rule document, in the same block

The confirmation was documentation-only; the code still starts the coroutine unconditionally. So each new block ends with `⚠️ 상태 — 사용자 확정 · 🔴 코드 미반영(미구현)` **plus the measurement that proves it**: `OnGameEnd()` starts the coroutine with no `IsNetworkActive` branch, and the network-only entry point (`ShowResult(...)`) is **defined with 0 call sites**, so the one live normal-path start is shared by both modes. 🔴 **Counting the *live* start sites is what makes "the code runs it in single-player too" a measurement rather than an inference** — a definition with no callers would otherwise let you believe the modes were already separated. The `_Tasks/` documents that recorded 「문서와 코드가 일치한다」 then get a pointer saying that row is now false **in the opposite direction**: the docs moved, the code did not.

> **[⚠️ 2026-09-29 — the paragraph above is kept verbatim; the method still holds, but its example is now a *past* case.]**
> 🔴 **`ShowResult(TeamId, TeamId)` no longer exists** — it was finally deleted on 2026-09-29 (WORKFLOW [4]'s post-test slot), so the live coroutine start sites went **4 → 3** and the gap this block was written to describe is **closed**: the code no longer runs the timer in single-player.
> - ✅ **The method is unchanged and was re-used the same day**: counting the *live* start sites is exactly what turned 「grep 4 · runs 3」 into 「grep 3 · runs 3」, which is the first time the baseline and reality agreed. **A definition with no callers still distorts a count — that is the transferable part.**
> - 🔴 **Do not cite `ShowResult(...)` as a present-tense example.** When this section is needed again, name the *shape* (a network-only entry point defined with 0 call sites) and find the current instance by measuring, not by reusing this name — the same reason §21 gives for not quoting a deleted identifier in new prose.
> - **Where the closure is recorded**: `GameSystemRules_UI.md` 규칙 D-4 · M-4 끝의 2026-09-29 블록, and `_Tasks/2026-09-28/11_50_single-map-failure-ui/Plan.md` §7-2 · §12.

### §41-5. Do **not** invent a session ordinal when the document's `(N차)` numbering is day-wide and shared

`GameSystemRules_UI.md` labels same-day blocks `2026-09-28 (2차)`, `(3차)`, `(4차)` — and those ordinals are **the day's session number, shared across 규칙 M-3 · M-4 · D-7 · D-8**, not per-rule counters. The day's 5th session existed but touched only a `_Tasks/` Plan, so the rule file has no `(5차)`. 🔴 **Guessing the next ordinal would have merged this round's blocks with a different session's record**, so both new blocks were named by **subject** (`2026-09-28 확정 — 적용 범위를 멀티플레이 한정으로` / `2026-09-28 확정 — 싱글에는 자동 복귀 타이머가 없다`) and cross-reference each other by that name. Record the choice in the Plan so the next round does not "fix" it back to an ordinal.

### §41-6. Line numbers you cite in a Plan go stale **because of your own edit** — say so instead of rewriting them

The Plan's §3-4 and §9-3 pointed at `510~519행` (규칙 D-4) and `285행` (규칙 M-4). Appending the two blocks pushed every later line down. 🔴 **Do not renumber**: the cited numbers are the record of what was measured before the edit. Append a note saying they are pre-amendment values and that rules are addressed **by name**, which does not shift. Same treatment for a stale `정의 1617행 · 호출 1728행` in the same Plan — the new block cites the method name and states `정의 1곳 · 호출 1곳` re-measured this round (excluding one comment mention, which `grep` counts but is not a call site).

### §41-7. A judgment item promoted to a **stage** takes a new letter at the end — and the old cell keeps the rejected branch

The user also approved building a single-player forced-failure dev tool, which the Plan had parked as 「판단 항목 §8-2 · 사용자 승인 대기」. Promoting it: add **stage G** at the end of §4 (never re-letter A~F — §5·§7·§8·§11·§12 reference those letters by name), leave §8-2 as the record of *how* it was decided, and strike only the `판정` cell. 🔴 **Keep the 「만들지 않을 경우」 row even though that branch will never happen — it *was* the argument for the approval.** The two 실기 test rows that read 「재현 수단이 없으면 시행 불가」 get their caveat struck with a pointer to stage G, and the new stage's table says in its own cells **what is deliberately not decided** (class name, menu path, injection point, reason count) because those belong to `game-programmer` (CLAUDE.md 규칙 3·6). One cell is a *fact*, not a preference: the forced failure must flow through the **same `null`-returning path** as a real failure, or the screen it reproduces verifies something else.

### §41-8. When the caller narrows a multi-part stale sentence to **one fragment**, say inside the document which fragment was judged

`TechnicalDesignDocument.md`'s 「여전히 남은 것 넷」 item ③ bundled two stale fragments (`팝업 여부 미정` and `countdown 전체 길이 재시작 미구현`). The round's scope covered only the second. 🔴 **The appended block therefore names the fragment it corrects and states outright that the other fragment and items ①②④ were *not judged this round*** (CLAUDE.md 규칙 10) — otherwise a reader takes a dated update block as having vetted the whole bullet. And item ③ itself is **not** resolved: the single-player failure UI is still unimplemented, which is the very task in progress, so the block also has to say what part of ③ survives.

## §42. Rewriting **one stage of an existing Plan from scratch** on a user's final approval — reconciling 「전면 재기술」 with B-7, and writing a **timing contract** the implementer cannot misread (2026-09-28 (5th), 싱글 맵 준비 실패 UI / 결정 1~7)

The round's input was seven already-decided items; the plan had to stop calling any of them 「검토 대기」. Nothing was implemented — documents only.

**1. 🔴 「전면 재기술하라」 and B-7 do not actually conflict — split the stage into three named sub-sections.**
The instruction said the old design (`void → bool`) was *폐기* and the stage must be rewritten. Deleting the old table would violate B-7. The shape that satisfies both:
`#### C-신` (current design) → `#### C-계약` (the contract) → `#### C-구(폐기 — 원문 보존)` (the original table, untouched, under a one-line marker).
- 🔴 **Keep the stage's letter.** `C` is referenced by name in §5 · §7 · §8 · §11 · §12 · §14; a new letter would break every pointer silently (§20-1 again).
- The heading of the stage itself is **not** renamed — only sub-headings are added under it, so a reader arriving from a pointer still lands in the right place.
- **Write the 폐기 사유 in the marker block, not in the new table** — the reason belongs to the transition, not to the design.

**2. 🔴 The hardest artifact of the round was a **timing** contract, and timing is exactly what a design table omits.**
Both §4-B and §4-C said *what* to do ("re-`Show()` the result screen") and neither said **when**. The failure mode: the event is published synchronously, so a subscriber that reacts in place is immediately undone — `LoadMap()` keeps running past the failure and closes the screen **twice more**.
- **Lead with the measured fact, as an ordered table** (call site → what it does), because "it closes it twice more" is not believable without the two addresses.
- 🔴 **Then state the trap in the form that kills the natural reading**: *「UniRx 발행이 동기라는 것은 맞다 — 그러나 동기이기 때문에 그 자리에서 반응하면 안 된다」*. A reader who knows the publish is synchronous will otherwise conclude the opposite.
- **Numbered imperative steps, one per actor** (subscriber sets a flag only / the caller re-`Show()`s after the call returns / the status line uses the setter and starts no countdown).
- 🔴 **Say that the contract is design-independent** — *「이벤트 방식이든 반환값 방식이든 공통으로 필요하다」*. Otherwise the next person reads it as a cost of the chosen design and deletes it when the design changes.

**3. 🔴 A 「채택」 changes three places per reviewed item, and the third is the heading.**
For each of V2·V3·V4: ① the 판정 cell gets `~~struck~~ → ✅ 채택` + a pointer, ② a 확정 블록 is appended after the table, ③ 🔴 **the section heading's own `(🔴 검토 대기)` is struck inside the heading** — otherwise the document's table of contents still says the opposite of its body.
- The summary table at the top of the section takes the same struck-plus-arrow treatment.
- 🔴 **The 확정 블록 must carry the cost, not only the decision** — here: *「멀티 무변경」이 문자 그대로는 지켜지지 않는다* (the adopted setter touches one line of a field-verified coroutine). A 확정 written without its cost gets reopened (§37-5).
- **Where the approval's rationale is a `CLAUDE.md` rule (here 규칙 7, 완성도 우선), quote the rule's own words and put them in *both* the 확정 블록 and the §5 rule-mapping table** — the mapping table is what the implementer reads per item.

**4. 🔴 An adopted alternative can be adopted **without** the mechanism it was argued from.**
V4 was argued as *"an event for this already exists — reuse `OnNetworkRematchMapFailed`"*. The user adopted **the event approach and rejected the reuse**. So the 확정 블록 has to say *「방향은 채택, 수단은 채택되지 않았다」* explicitly, with the two reasons (that channel's handler restarts the countdown — a measured call chain — and the type name carries `Network`).
- 🔴 **Prove the disqualifying side effect by reading the chain, not by naming the rule** — subscription line → handler → state method → `RestartCountdownFromFullLength()`. The rule (M-3) is what the chain *means*, not evidence that it runs.
- **When a hand-off says a new channel would violate a "don't create new events" rule, decide it by asking what that rule was protecting** — 규칙 18 forbids splitting *one* event by cause; a different event in a different mode is not that. Then name the **existing precedent** (the three `OnLocal*` channels) so the new channel is a convention, not an exception.

**5. 🔴 A removal item added late makes the section's own headline false — and its **first** item may already be obsolete.**
§0 said *「제거로 분류되는 것은 다음 1건뿐이다」* and that single row had itself been rendered moot by another of the same round's decisions. The repair: append row 2, then a note that says (a) the headline now reads 2건, (b) 🔴 the new row is the **first real** removal because row 1 was 「삭제가 아니라 확장」 and is now 폐기, (c) the WORKFLOW [4] schedule verbatim (disable by comment → final delete after [6], before [7]), (d) the 판정 기준 — **주석 기호만 떼면 원래 동작이 돌아오는 코드가 남아 있는가**.
- 🔴 **A removal that drags comments with it is one bundle, and the bundle must be stated in the row** — deleting the method would leave four comments pointing at a name that no longer exists. **Say in the row that the comment fixes are *not* removal and therefore not subject to disable-first**, or the implementer will comment them out too.
- **Absolutes elsewhere in §0 go half-true**: *「한 줄도 지우지 않는다」* survived (still 0 lines deleted) but *「`CountdownCoroutine()` 본문 … 그대로 남는다」* did not. Repair with a **two-row table of 「원문의 문장 → 지금은 어떻게 되었나」**, and 🔴 end it with the sentence that says which conclusion still stands (§20-6 shape).
- ✅ **A 「구현 중에 제거가 새로 필요해지면」 clause that actually fires is worth marking as fired** — it proves the clause was doing work.

**6. Risk-table rows get a **four-row pointer block**, never edits — and one of them may go *both* ways in one round.**
위험 2 died (no signature change), 위험 3 turned from 「건드리지 않는다」 into 「감수한다」, 위험 8 lost one of its two branches and kept the other, 위험 12 is resolved by the new removal **except during the comment-disabled window**. 🔴 **Write the surviving window explicitly plus why it is harmless (a commented-out method cannot be called)** — "resolved" alone invites deleting the row.

**7. Verification baselines: distinguish 「착수 전」 from 「구현 뒤 기대값」, and say when the count is judged.**
The `StartCoroutine(CountdownCoroutine` baseline is 4 sites; after the removal it becomes 3, and **during the disabled window a grep still counts 4 because grep counts commented code**. 🔴 So the addendum says *「이 수치는 최종 삭제 후에 판정한다」*. Without that sentence the same baseline produces a false alarm in one phase and a false pass in another (§14-1's lesson, one step further).

**8. A 「없는 것을 확인하는 테스트」 needs a number, and the number needs a derivation.**
*「시간이 지나도 자동으로 로비로 가지 않는다」* is unjudgeable without a wait. **90초** was set because it must exceed the multiplayer worst case (60 + 30), so a surviving screen cannot be confused with a leftover multiplayer timer. 🔴 **Write the derivation in the cell** — a bare number gets "rounded" by the next reader.
- For a widget the round never showed on screen, add the check **and its grade in the same cell**: 「모달은 `UIManager` 소유라 무관해 보이지만 z-order·오버레이 상호작용은 미확인 — 구조 추론이고 실기 화면으로 확인된 것이 아니다」.

**9. Round bookkeeping.** A 「이 절이 바꾸지 않은 것」 paragraph written by an earlier round is not an error to strike — 🔴 **it named a condition (*「채택 여부가 정해질 때까지」*) that this round satisfied**, so the appended block says so and then **lists every section it did touch**. In §12, add rows for the round's own decisions but 🔴 **leave stages A~G at `⬜ 미착수` and write the sentence that explains why**: 채택 is 「how it will be built」, not 「it was built」.

## §43. The round where the plan finally **shipped, was field-verified, and its disabled code was deleted** — grading 「completed」 by whether the log has a place for the claim, and 🔴 **splitting a build before trusting a device log** (2026-09-29, 싱글 맵 준비 실패 UI / 규칙 M-4 · D-4)

**1. 🔴 A count that goes stale because the *code* changed is a different repair from a count that was a typo — and the same document can hold both.** 규칙 D-4's block had been corrected once already (「셋」→「넷」) **without a strikethrough**, on the stated grounds that it was *「같은 날 같은 세션에 적은 숫자 오기」* — not a past state worth preserving. This round the same number went **넷 → 셋**, and the correct repair is the **opposite**: a dated append that leaves the original untouched. 🔴 **Write the distinction into the new block in one sentence** (*「오기가 아니라 삭제다 — 그때는 실제로 넷이었다」*), because a reader who sees two corrections of the same number in one block will otherwise assume one of them was wrong. The same pair appears in `ROADMAP.md`: the row's own 2026-09-28 block had said *「처음부터 4곳이었다」*, and the 2026-09-29 append has to say **that block was right at the time**.

**2. 🔴 「코드 미반영(미구현)」 is usually copied into more than one block — grep the phrase, then pick one owner.** `GameSystemRules_UI.md` carried it **three** times for the same work (규칙 M-4's 2026-09-28 (3차) block, 규칙 M-4's 2026-09-28 block, 규칙 D-4's 2026-09-28 block). The repair is §39's shape: **one full resolution block at the rule's end**, and **a one-line pointer** on each earlier occurrence saying the single source is that block. Do not resolve the same status three times — the three texts will drift.

**3. 🔴 The strongest evidence for a removed feature is a *duration*, not an absence.** 「싱글에 타이머가 없다」 is a negative claim, and 「코루틴 시작 로그 0건」 alone reads as 「the log did not record it」. What actually settles it is **the failure screen surviving 약 315.9초 (5분 16초)** — more than three times the 90초 test threshold the plan had derived (multi worst case 60 + 30). 🔴 **Pair the zero-count with a measured duration and say what the duration is being compared against**; a negative test without a threshold cannot be graded.

**4. 🔴 One log file is not one build — split the build before judging a spec.** A single device logcat held **two sessions with different PIDs**, and the earlier one was **single-player yet started a 60s countdown**, i.e. it read as a violation of the very rule the round implemented. The split was made with the **compiler-generated coroutine class number** — `<CountdownCoroutine>d__49` (1 hit, old PID) vs **`d__59`** (9 hits, new PID). **Different number ⇒ different assembly**, so the earlier session was a pre-fix build.
- 🔴 **Write the limit down or the next reader uses it backwards**: the test is strong only in the *different ⇒ different build* direction. **Same number does not prove the same build** (a change that does not touch that method leaves the number alone).
- Procedure: ① split by `PID` ② compare `<method>d__N` ③ if still undecidable, say 「어느 빌드인지 확정할 수 없다」 and **drop that span from the judgement** rather than averaging it.

**5. Prove 「동작 무변경」 of a comment-only deletion by hashing the code body, not by counting lines.** The deletion was **52 lines, 0 added** (51 starting with `//`, 1 blank) and the file went 2403 → 2351. What makes it a *measurement* is that the **SHA-256 of the code body with comments and string literals stripped and whitespace removed is identical before and after** — i.e. the compiler's input did not change by one character. 🔴 **That hash is a handed-over value** (I cannot run git), so it is labelled as such while the line count and the 0-hit greps are my own.

**6. 🔴 A deferred judgement is worth more than an immediate one — say why when you finally cash it.** §7-2 had refused to judge `StartCoroutine(CountdownCoroutine` **until after the final deletion**, because during the comment-disabled window `grep` counts code inside comments. Cashing it now gives **grep 3 = runs 3 for the first time**; before, it was **grep 4 · runs 3** because the fourth site sat inside a 0-call-site method. 🔴 **Record both halves** — the value *and* the fact that the same baseline would have produced a false pass mid-round.

**7. A baseline that legitimately *grew* needs the new value stated as the next round's baseline.** `LoadMap` call sites went **3 → 4** (the failure modal's retry added one) while **the signature and the original three stayed untouched**. 🔴 **The plan's命令 line still says 「3곳 그대로인가」, and it is not edited** — it is the pre-start baseline. The appended block says *「다음 회차의 기준선은 4곳이다」*, otherwise the old number becomes a permanent false alarm.

**8. 🔴 Splitting the status cell into 「코드 적용 완료」 and 「실기 검증」 is the artifact — and the grades differ per stage.** In one execution table the same round produced: **F** = log observation plus the 315.9s negative evidence (strongest) · **A · B · E** = log reaches halfway, the rest is **사용자 화면 확인** · **D** = 「멀티 회귀 증상 0건」 only. 🔴 **Never write 「회귀가 없다」 when what was measured is 「증상 0건」.** And list, in the same block, what the field session could **not** close: the run happened **before** the deletion, so 삭제 후 재검증 is **0건**; Unity compilation is unverified; the per-second setter's cost and the modal's z-order were never measured.

**9. 🔴 Judging whether to take a roadmap row down = following the row's own leftovers one by one, and checking what the row is the *only* pin for.** The row's own last block named 「단계 10」 as the remainder and this round closed it — yet the row stays, on three grounds that had to be measured rather than assumed: ⓐ **단계 8's 실기 검증 기록을 찾지 못했다** (「찾지 못했다」, not 「없다」) — its leftover fix **is in the code** (I measured `UIManager.Instance?.HideConfirmOrAlert()` inside `ReturnToLobby()`) **but the field record for it was not found** ⓑ a latent bug still 「사용자 판단 대기」 has **no dedicated row**, so this row is its only pin (§13's rule: re-home before deleting) ⓒ the round's own five unverified items survive. 🔴 **Then say what kind of thing is left**: 「남는 것은 「단계」가 아니라 「검증과 판단 대기」다」.

**10. 🔴 An out-of-scope item whose *scope question* is itself unresolved gets a row that says 「판단 필요」, and the row must say what the question is.** `ProjectMap` failure has no UI, but 「규칙 M-4 가 그 단계를 포함하는가」 is genuinely ambiguous — the rule says only 「맵 준비 실패」. The row therefore records **the question, not a fix**, plus 🔴 **severity you cannot determine** (0 field occurrences, and the expected screen is a *structural inference*). Grouping rule for the rest: label/문구 단일 소스 items merge into one row (same repair shape), while a scene placeholder string and a partial unification each get their own, because their owners and risks differ.

**11. 🔴 A methodology example can outlive its subject — mark it as past, never delete the method.** §41-4 used *「the network-only entry point (`ShowResult(...)`) is defined with 0 call sites」* as the measurement that turns an inference into an observation. The identifier is now gone. The append keeps the method (counting **live** start sites), states the gap is **closed**, and forbids citing the name in present tense — **name the shape** (a network-only entry point with 0 call sites) and re-measure for the current instance, the same reason §21 gives for not quoting a deleted identifier in new prose.

**12. 🔴 The comment-copy trap reached its 5th occurrence, and the widening now includes *identifiers*.** 3rd = screen strings · 4th = assignments and log `key=value` · **5th = an identifier (`NetworkContext.IsNetworkActive`) copied into an XML comment**, which polluted a 「did a new discriminator appear?」 baseline (4 lines → measured as code 5 + comment 2). 🔴 **The agent had read the `mistakes.md` entry before starting and still stepped in it** — so the conclusion 「지식이 아니라 절차」 is now five-times reproduced, and the only countermeasure remains 「주석을 쓴 직후 grep」.
- 🔴 **Verify the *end state* yourself and do not inherit 「사본을 제거했다」**: my own grep found **6 lines (5 code + 1 comment at 728)** against a **4-line pre-start baseline**, one of the two additions being the intended named property. **So one identifier copy still stands — 2건 중 1건만 제거됐다.** Code is out of a documentation round's scope (규칙 6), so the fact is recorded and reported, not fixed.
  - **[🔴 2026-09-29 correction — the sentence above is kept, but its conclusion is wrong; the correction is attached so the two are read together.]** 🔴 **The comment at 728 was there before the round started** (pre-start measurement `592` comment + `593` `if`, verbatim identical, pushed down as code grew), so **the copies this round created was 1 and it was removed — residual 0.** 🔴 **Why the arithmetic failed, and this is the transferable part: I never checked what the baseline was counting.** §7-2's *「착수 전 실측 = 4곳: 593 · 821 · 976 · 1980행」* counted **code sites only** — `592` is absent from that list — while my measurement counted **every grep line including comments.** **Subtracting two numbers that count different things produced a copy that does not exist.** ✅ **So the rule stands but gains a second half**: verify the end state yourself, **and reconcile it against what the baseline counted** — a baseline stated as 「N곳」 with a line list is often a *filtered* count, not the raw grep total. ⚠️ **What remains true**: line 728 does pollute a count of this identifier — but as **pre-existing** debt for whoever counts it next, not as this round's defect.
- 🔴 **When the thing the grep counts *is* an identifier, "point at it by name" is not a safe escape** — the fix was to make the comment point at the *wrapping property* instead.

**13. Four main-session mistakes in one round, and 🔴 the fourth is a 「하마터면」 — classify it as such and say why the file still keeps it.** (a) 「값이 씬에 있다」 was reported to the user as 「화면에 뜬다」 — the **erasing path was never counted** (`StopCountdown()`'s text clear sits *outside* the `if (_countdownCoroutine != null)` block, and `LoadMap()` calls that `Hide()` twice). (b) A **word grep hit was read as 「반영됨」** for a file that was never edited — 「그 말이 있다」 ≠ 「내가 넣었다」; 반영 여부는 **변경 목록**으로 판정한다. (c) An item count was passed on as 4 when it was 5 — 🔴 **the user's question is what recovered the missing item**, so a count error is a *loss* class; the procedure is to **number the items and read off the last number** instead of stating a total. (d) The two-session log (see 4) was **caught, not misdiagnosed** — so the entry is titled 「하마터면」 and says 「오진하지 않았다」 in the same line. 🔴 **The grounds for keeping it anyway**: this file already keeps events caught before commit (the comment-copy entries), and its own rule is 「사건을 지우지 않는다」 — but those are 「a mistake happened, only the consequence was stopped」, which is a different thing, and the title must not blur them.

---

## §44. Folding a **batch of four follow-ups** into a Plan whose stages are already finished and field-verified — and closing a 「판단 필요」 roadmap row that the *previous* round deliberately left open (2026-09-29 (2nd), 싱글 맵 준비 실패 UI / 규칙 M-4 범위)

**Round shape.** Stages A~G of an existing Plan had shipped, been field-verified, and had their disabled code finally deleted (§43). The user then approved **four follow-up items in one turn** and also approved **not creating a new task folder** — the items were to be appended to the same Plan. So the document already read like a *record*, and the new items had to sit next to it as a *plan* without contaminating that record.

**1. 🔴 A finished Plan gets follow-ups as a NEW numbered section, not as new rows inside §4 「단계 분할」.**
§4 had grown an appended stage `G` in an earlier round, so appending `H`~`K` there was the apparent precedent. It is the wrong one: §4's stages are now a **closed, field-verified set** with per-stage adoption blocks, and §12's table says so. New items with `⬜ 미착수` status inside that section make the section半 record半 plan.
- The section number is **appended** (`## 15.`) and the letters are **appended** (`H`…), with the standard sentence naming *which* sections reference the old letters/numbers by name (here: §5 · §7 · §8 · §11 · §12 · §14).
- **Do not add a pointer inside §4.** Discoverability comes from the three places the instruction already had you edit — §5 (rule basis), §11 (scope), §12 (execution table) — each pointing at `§15-x`. Editing §4 to "help" is scope creep into a record.

**2. 🔴 When a follow-up letter covers two jobs of different *risk grade*, split the row (`K-1` / `K-2`) — and write why.**
The user handed one bundle ("the remaining two"): one job decides **who owns a string**, the other **edits multi-player code that is field-verified**. Their completion criteria and risk differ, so one table row would go false the moment half finished. Say that in the update block: *「한 행에 묶으면 한쪽만 끝났을 때 상태 칸이 거짓이 된다」*.

**3. 🔴 The §0 「기존 로직 제거」 verdict for a batch is a table with a **deferred** row — and the deferred row is the valuable one.**
Four items produced **0 removals**, but the reasons were four *different* shapes, and one could not be decided at all:
- *addition* (a notification added at a failure point — quote the code comment that already says 「돌아가는 동작 자체는 그대로다 — 알리는 일만 더했다」),
- *serialized **value** change* (not a field deletion → format unchanged; but 🔴 **WORKFLOW [4]'s disable-first has no middle state for a scene value**, so the substitute revert mechanism is **writing the original value into the document** — here `124124` plus its fileIDs),
- *sentence edit* (a comment; the existing §0 row already carried this verdict — cite it rather than re-deriving),
- *replacement* (2 direct widget writes → setter calls; same shape as the row that said 「삭제가 아니라 확장」).
- 🔴 **The undecidable one (`K-1`) is recorded as 「미정 — 판정 자체가 이 항목의 내용이다」 with the trigger written in:** *if* the verdict becomes "merge", a literal disappears, so **that is the moment it gets promoted into §0** under WORKFLOW [4]. Never pre-promote it.
- Close the block by re-checking the section's own **absolute** head sentences: here *「1건뿐이다」* / *「이제 2건이다」* stayed **true**, and saying so explicitly is itself the output.

**4. 🔴 A 「판단 필요」 row from a previous round is closed by answering **its question**, not by describing the fix.**
The row had written the question verbatim (*「고칠 것인가가 아니라 규칙 M-4 가 이 단계를 포함하는가」*). The user's answer was a **scope reading**, so:
- The rule document gets a dated block whose **first line says what did NOT change** (values, wording, loading-UI policy, button labels, retry policy, rule number) and then **one three-row table of 「언제 실패하는가 → 적용되는가」** marking which row is the newly decided one.
- 🔴 **A scope widening is not a new rule number.** No new `M-`/`D-` letter, no renumbering.
- The row's own still-true caveats (**severity undetermined · 0 field occurrences · 「구조 추론이고 관측이 아니다」**) are **re-asserted as still true** in the update block. Closing a question does not close the caveats attached to it.
- **The row is not taken down** — 「정해진 것은 범위이고 구현은 0줄이다」.

**5. 🔴 Scope-limiting a widened rule is the hardest half, and the code says why it is not automatic.**
"Any reason, any point in time" reads as universal. Measurement showed the projection function is **shared by the single and multi call paths** (2 call sites). So 「싱글 한정」 is not something the structure grants — it is **something the implementation must do**, and that sentence belongs in the plan item *and* in the rule block. Also name the sibling rules that already own the multi case (`M-2` · `M-3`) so widening there is visibly a separate, unapproved job.

**6. 🔴 "Leave it as 「확인 후 정한다」" does not mean "do not measure".**
The instruction said not to assert how the projection stage signals failure. Measuring it anyway (`private void`, one `IsSucceeded` branch, log-only, no `return`, no throw **in that method**) and then writing a two-column table — **✅ 실측된 것 / ⚠️ 미확인** — serves both rules: the document gains facts, and the *design decision* stays open. 🔴 **Say which sub-question the measurement did NOT reach** (here: whether the use-case called inside can throw — that file was not read), otherwise the measurement reads as a complete answer.

**7. 🔴 Handover mismatch, 11th class — the claim named the wrong *measuring instrument*.**
Handover: *「그 주석 사본이 실제로 이번 회차의 §7-1 측정에서 걸렸다」*. §7-1's command is **scoped to one file**, so it measured **1** correctly; what the comment pollutes is a **repo-wide** grep (4 hits, 3 of them values). The quoted phrase existed, the pollution was real, the **named instrument was wrong**. Fix: keep the handover sentence, add your own measurement with the instrument named, and state the distinction (「오염되는 것은 리포지토리 전체 grep 이다」).
- Same round, 12th class (a repeat of "the count was short"): the handover listed the failure phrase in **2** places; measurement found **3**, the third in a **different mode's path** (multi). 🔴 **A third copy in another mode is not automatically in scope** — record it and make "is it in scope?" part of the item.

**8. 🔴 A count of copies must exclude strings that merely *start* the same.**
「로비로」 has 2 value sites, but a word grep also hits `"로비로 이동 중..."` and `"로비로 돌아가기"` — **different strings, not copies**. Write that exclusion into the table cell, or the next reader inflates the number.

**9. 🔴 Two copies differing only by a trailing period may be **spec**, not duplication.**
The status line is a **sentence** (its rule's wording table ends every row with a period); the modal body is the **rule-confirmed token** (no period). So 「같은 문구 두 벌」 is an unsafe reading, and a plan that says "merge them" would be **changing the spec**. The plan must therefore instruct **judging whether they are mergeable at all**, and say that a "merge" verdict is itself a **user-approval item**. 🔴 Write the two owning rules by name; that is what makes the caution checkable.

**10. Where to put the single source when two *layers* hold the same string.**
Naming the layers (`Bootstrap` + `Presentation`, plus a second `Presentation` file) is enough to show that "just move it into one file's constant" does not follow — the reference direction could invert. Leave the seat undecided, but 🔴 **state the constraint (layer boundaries) so the decision cannot be made carelessly later.**

**11. 🔴 A follow-up that reverses a closed risk gets 「범위가 늘었다」, not 「예상이 틀렸다」.**
A risk row had closed as *「씬 작업 0건 — 해소됐다」*. The new item edits the scene. The repair is a sentence in the **new** item (the risk cell is untouched): 「예상이 틀렸다가 아니라 범위가 늘었다 — 단계 A~G 가 씬을 건드리지 않았다는 사실은 그대로 참이다」.

**12. 🔴 A completion criterion for touching field-verified code is stated as a *count transition*, not as "check carefully".**
For the setter unification: setter call sites **3 → 5** and direct assignments to the widget **→ 0**, plus the explicit exclusion (*「null 검사 · 필드 선언 · 주석은 남는다 — 대입만 센다」*). And the verdict wording is inherited from the previous round: **「회귀가 없다」가 아니라 「증상 0건」**.

**13. Line numbers that have already drifted: do not fix them, and say what to point at instead.**
Three cited line numbers (a literal's site, and three `StopCountdown()` dependents) had all moved. Keep the old cells, add *「그 시점의 값이며 지금은 어긋났다」*, and re-point by **assignment target identifier** / **method name**. 🔴 Then check you did not immediately break your own rule in the new text.

**14. Deviations from the instruction, and how they were marked.**
- The instruction named **3 roadmap rows**; a **4th** row was the home of one of the four approved items, so it was updated too. 🔴 **Leaving it 「미착수」 while the Plan says the item is in scope is a knowingly false row** — consistency wins, and the deviation is led with in the report.
- The instruction asked for **H's** rule basis in §5 only; basis rows for `I`·`J`·`K-1`·`K-2` were added as well, because **WORKFLOW [4]** requires a rule basis per modification item and §4's own preamble names §5 as the single source. 🔴 All four begin with 「규칙이 아니라 …」 — **no rule was invented to fill the cell.**

**15. Verification for a round like this (the checker says almost nothing).**
`check_docs.py` = 0 covers only the rule document and the roadmap; the Plan lives under `_Tasks/` and is **outside the scan set**. So: ① pipe count per table row with `(?<!\\)\|` across **both** edited files, ② every `§15-x` anchor referenced from elsewhere exists as a heading, ③ every sentence that had to stay untouched verified with `grep -cF … = 1` (here 7 in the Plan, 3 in the rule doc, 5 in the roadmap). 🔴 **Adding lines to the agent-memory folder needs an increase-direction `--update-baseline`, which this round was forbidden to run — report it so it is not delayed** (a delayed increase makes the *next* decrease look smaller than it is).

---

## §45. The round where a **batch of follow-ups shipped and was field-verified**, two roadmap rows finally came **down**, and a doc tool's missing entries were backfilled (2026-09-29 (2nd), 싱글 맵 준비 실패 UI / 규칙 M-4 범위 · 강제 실패 플래그 ⑤⑥)

**Round shape.** §44 appended follow-ups `H`~`K-2` to a finished Plan as `⬜ 미착수`. This round they shipped, plus **two items that were not in the plan at all** (`L` a second scene placeholder, `M` a new dev toggle). Four roadmap rows had to be judged, and the TDD's dev-tool section turned out to be **two entries behind the code**.

**1. 🔴 Taking a roadmap row down is decided per row, and the answer can differ inside one batch — 2 down, 2 kept.**
Method (§43-9, applied four times): follow the row's own leftovers, then ask **what the row is the only pin for**.
- **Down** — the scene-placeholder row and the status-line-unification row: every leftover they named measured out at zero, *including the one that row itself had flagged as unverified* (「does `StopCountdown()`'s clear-then-refill premise break?」 — the field log showed the stop→refill pair in both touched sites).
- **Kept** — 🔴 **both for 「judgement pending」, not 「unfinished implementation」.** One row is the **only work-item home** for 「should this scope widening extend to multiplayer?」 (a rule document says *「정하지 않았다」* but never opens a work item — that is ROADMAP's job). The other still holds **one live copy in the other mode's path**.
- 🔴 **A fact that lived only in a row you are deleting must move to the round's update block first.** Here: 「the scene placeholder was **two** sites, not one — the plan had counted one」. Written into ROADMAP's top block *because* the row was going away, and said so in the same sentence.
- **The stale enumerations in earlier blocks** (「새로 세운 행 4개」 / 「별건 3개」) get **one pointer bullet** naming which of them are now gone. Originals untouched.

**2. 🔴 A capability that was *structurally unreachable* is the strongest thing a round like this can report — say why it was unreachable.**
The new failure point sits **after** an existing forced-failure toggle's stage, and that stage's failure makes the caller **return before the later stage is ever called**. So the old toggle could never produce the new screen. The log proves reachability by the **pair in one match**: `MapPreparationSucceeded` → forced marker → `MapProjectionFailed`. 🔴 **That ordering is the artifact**, not the failure line alone.
- Consequently the new toggle needed **its own store**, and the rationale is **「도달 불가」, not 「different kind of value」** — sharing a key means the earlier stage consumes it and the later stage **never fires, ever**. Record that contrast against the earlier sibling's rationale, or a reader assumes one rule covers both.

**3. 🔴 A doc section describing a tool can fall *two* entries behind, and both must be added with the *difference* spelled out.**
The TDD section listed ①~④; the code had ⑤ (added a day earlier) and ⑥ (this round). Backfilling is not 「add two rows」 — the new ones **break two invariants the section stated for ①~④**: they fire on the **first match too** (①~④ are rematch-only), and they are **single-player only**. Both had to be written as explicit contrasts.
- 🔴 **The `Error Pause` caveat is per-entry, not per-section.** ①②③ need it off (`[ERROR]` outcome), ④ does not (`[WARN]` only), and ⑤⑥ need it off again. Saying 「⑤·⑥ 은 ①②③ 과 같고 ④ 와 다르다」 is the whole point — a reader who generalised from ④ gets it wrong.
- 🔴 **Write the "both armed" trap into the doc, not just the code.** The confirm menu warns about it at runtime, but the person reads the doc first: *「준비와 투영을 동시에 예약하면 준비만 발동하고 투영 예약은 남는다」* plus the reason.

**4. 🔴 Grading is per item and the grades split four ways in one execution table.**
- **Log direct observation** — the new failure path (2 firings) and the touched multi sites (the stop→refill pairs).
- **End-state measurement only** — the comment-copy removal and the label single-source: 🔴 **there is nothing for a field test to measure** when the動作 diff is zero or only a value's *home* moved. Say that, or the empty 실기 column reads as a gap.
- **Scene-file measurement + user's on-screen confirmation** — and 🔴 **these stay two facts** (§36). The second scene site is the *more* important one because it is where the win/lose text appears.
- **「실기 사용 확인」** for the dev tool — it fired; that is not a spec verification.

**5. 🔴 「Unity compile unverified」 can be closed without running Unity — by two artifacts, and only that far.**
① the new file's **`.cs.meta` exists** ⇒ the editor **imported** it ② that code **ran twice in the field** ⇒ it **compiled**. 🔴 **But 「compile warnings = 0」 stays open**, because the log has no place for a warning tally. **「오류 없이 돌았다」 and 「경고가 없다」 are different claims** — write both halves or the reader collapses them.

**6. 🔴 A count the previous round measured can be *right about what it found and short about the world*.**
The plan measured **one** scene placeholder; there were **two**. 🔴 **The repair is not 「the number was wrong」 but 「the search reached one site」** — the old table stays, and the new item (`L`) carries the difference. And since **both sites hold the same revert value**, the revert note must say **how to tell them apart** (fileIDs), or restoring hits the wrong object.

**7. 🔴 A 「judge whether they are mergeable」 item can close as **「do not merge」**, and that is a *conclusion*, not 「미착수」.**
Three copies of one phrase differ only by a trailing period; the period is **spec** (status line = sentence / modal = rule-confirmed token). So the verdict is 「합치지 않는다」, the reason goes into **code comments at both sites**, and the row's state cell must say **종결**, not 미착수. 🔴 **The third copy, in the other mode's path, is what keeps the row alive** — same string, but a different rule owns it and that widening was never approved.

**8. Labels that *did* merge — say the dependency direction is allowed and already existed.**
Two button labels went 2 sites → 1. The claim worth writing is not 「moved to a constant」 but 🔴 **「the reference direction is a permitted one and that reference already existed ⇒ new dependency edges 0」**. Without that sentence the merge looks like it may have inverted a layer boundary.

**9. 🔴 The comment-copy trap reached its **6th** occurrence and the counted set widened *twice more in one round*.**
文구(3rd) → assignments·log `key=value`(4th) → identifiers(5th) → **ⓐ a markup token (an XML tag name written as prose) and ⓑ the type name a *prohibition* check counts**.
- 🔴 **ⓐ is new in kind**: the first five polluted a **grep count**; this one broke a **structural balance check** (open tags vs close tags, 9 vs 8). So the rule is no longer 「what a verification grep counts」 but **「any token an automated check reads」**.
- 🔴 **ⓑ inverts the false-positive direction**: the 5th made 「something appeared that did not exist」; ⓑ makes 「something that is not a violation is reported as one」 (the name inside a comment reads as a reference outside the guard).
- 🔴 **「point at it by name」 is now wrong for a third case**: a prohibited type is pointed at **by its *character*** (here: 「the editor's preference store」), never its name. And the only legitimate home for the banned token is **a comment saying why it is not used** (mirror image of the 2026-08-26 entry).
- **The agent caught both before commit again.** Six for six, always while knowing the trap ⇒ the countermeasure remains **procedure**, and the procedure's object list now includes structure checks, not only greps.
- ⚠️ **Do not grow the round's mistake count**: the handover said one mistake this round, so the entry is **appended as the 6th occurrence** and no new item is created (same discipline as the 2026-09-29 self-correction that was folded in rather than listed).

**10. A cumulative editor log needs its *span* named every time.**
`RuntimeLog.txt` reached 1,225 lines and now holds **two rounds** (13:xx = previous, 23:xx = this one). Every count (`[ERROR]` 4 in the file, **2 in this round's span**) is false without the span. 🔴 **State the span in the same sentence as the number**, and remember the device log is the opposite shape (a new folder per round).

**11. Where a label I was not given must not be invented.**
The calling session numbered its test procedure `F`~`J` but transmitted the *content* of only `I` and `J`. 🔴 **The results table is therefore written by content, with one line saying it is not a per-item mapping of that procedure** — naming `F`·`G`·`H` would have been a guess (규칙 10).

**12. Verification for this round.**
`check_docs.py` = 0 covers the rule document, the TDD, the status docs and the roadmap; **the Plan lives under `_Tasks/` and is outside the scan set**. So by hand: ① pipe count per edited table row with `(?<!\)\|` — 🔴 **an insert that keeps the *original* cell tail after your own new cell silently adds a pipe; I hit exactly that and had to repair five rows** ② every `§`-anchor referenced from another document exists ③ sentences that had to stay untouched verified with `grep -cF … = 1`. 🔴 **The baseline is stale in the increase direction (+237 lines in another agent's folder) and I was forbidden to run `--update-baseline` — report it, because a delayed increase makes the next decrease look smaller than it is.**


## §46. Closing 「미구현」 head-block markers in **three different rules of one document**, pinning a 「do **not** merge these copies」 judgement as a rule block, and taking **one fragment** out of a roadmap status cell without deleting the row (2026-09-29 (4th), 경기 종료 후 UI 마무리 / 규칙 D-3 · D-7 · D-8 · M-4)

**Round shape.** Documentation-only half of a round whose code half ran **in parallel in another agent**. Four items: a 「합치지 않는다」 judgement as one rule block, three stale sentences given update pointers, two pinned `⚠️ 미정` items closed, and one roadmap status fragment taken down. Zero lines of code or scene touched.

**1. 🔴 A `> **[… 미구현 …]**` block sitting at the *top* of a rule is the most-read line in that rule — and the pointer that closes it needs a blank line, or it is absorbed.**
Appending `> …` directly after the last `> …` line of a dated block produces **one continuous blockquote**: the reader sees your 2026-09-29 pointer as a continuation of the 2026-09-22 block. I hit this on **all three** sites and had to insert a blank line before each. 🔴 **Separating blank line is not cosmetic — it is what makes the new block a *new dated block*.** Check it by printing the two lines before each new header.

**2. 🔴 A stale line that asserts **two** things needs a pointer that names which half closed and *refuses* the other.**
`규칙 D-3`'s line said ① the rule's second half was never confirmed **and** ② the leave-detection itself was unimplemented. Only ② was falsified. The pointer therefore closes ② and adds: 「이 포인터가 닫는 것은 「미구현」 한 조각뿐이다 … **「판정할 수 있게 됐다」와 「판정했다」는 다른 사실이다**」. Without that sentence the pointer reads as closing the whole line — which would be an overstatement of exactly the kind `CLAUDE.md` 규칙 10 forbids.

**3. 🔴 The evidence for closing a marker is usually already inside the same document — grep the *claim*, not the subject.**
Grepping 「미구현」 across the rule document found a **sibling rule (D-4)** whose own 2026-09-22 block had already closed the same claim (「단계 7 에서 구현됐다」). So the pointer cites *that block by name* and copies nothing. Same for D-8: two lines inside D-7 already said 「규칙 D-8 은 이후 닫혔다」 and named the single source. ✅ **Three markers, three pointers, zero new facts written.**

**4. 🔴 A 「do not merge these N copies」 judgement must be written **about** the copies, never **with** them.**
The block names the three sites by **rule name + 자리** (status line / modal body / lobby-screen transfer notice) and its ⓐ ground is 「**끝의 마침표 유무**가 서로 다르고 그 차이가 사양이다」. Quoting the string would have made the block **the N+1-th copy** and poisoned the grep that counts copies. 🔴 **The verifiable claim is not 「hit count unchanged」 but 「every hit is at a line number *before* my new block starts」** — measure it that way (here: 5 hits, all before line 437; new blocks start at 438).
- ⓑ is the ground worth writing, because ⓐ was already in the code comments: **the two rules can be revised separately, so merging creates a 「거짓 단일 소스」** — one reviser silently changes the other screen.
- 🔴 **Say out loud that the N copies are the *intended* state**: 「셋이 각자의 값 자리에 따로 있는 것이 이 판정이 뜻하는 상태이며, 한 상수로 끌어모으는 것이 이 판정에 반한다」. Otherwise the next reader files the same 「중복」 report.

**5. 🔴 Closing two pinned `⚠️ 미정` items: pair them by the *original block's own numbering*, and the hard half is what did **not** spread.**
- Table columns = 미정 (the old block's number) / 닫힌 내용 / 근거. Numbering by the source block makes the closure decidable instead of narrative.
- 🔴 **「상태 줄 + 타이머 없음」, not 「상태 줄 + 전체 길이 재시작」.** The sibling rule bundles a status line *with* a countdown restart; the singleplayer side has **no clock to restart**, so half the bundle does not travel. Write the negation explicitly and point at the block that owns it.
- 🔴 **Narrow the scope in the same breath**: the sibling-matching only happens on the **rematch** path; the **first game** is still a modal. That makes the old sentence 「두 규칙이 서로 다른 수단을 갖는다 — M-3 은 상태 줄, M-4 는 팝업이다」 **half-true**, which is a scoped correction, not a deletion.
- 🔴 **The old 「찾지 못했다」 measurement gets 「없어서 만든 것」, not a retraction** — 「그 경로는 그 뒤에 신설됐다」. 「찾지 못했다」 was true when written.

**6. 🔴 Taking **one fragment** out of a roadmap status cell is a different operation from taking the row down.**
The cell already read `~~A~~ → ~~B~~ → C`; the edit adds **one more stage**: strike `C`, append `D`. Nothing is deleted, the row's total line count of the file is unchanged (383 → 383), and the pipe count must be re-counted (5 = 4 cells).
- 🔴 **The row survives because a *different* fragment is its only home** — 「멀티 확대 판단 대기」. Write that sentence in the new block, with the code measurement that shows the size (the multiplayer call site **discards** the projection function's result; the two existing multiplayer notify channels belong to other stages), so nobody reads the surviving row as leftovers of the finished work.
- 🔴 **Never convert 「발화 0건」 into 「없다」.** The replacement is **「빈도 미확정」 + name the watchdog**, because 「not observed」 and 「does not happen」 are different claims and the row was taken down on the ground that *nothing needs remembering*, not that nothing can happen.

**7. 🔴 Naming a watchdog without naming the key.**
Writing the log key into a document pollutes the very grep that asks 「does this fire on a *real* failure too?」 (`.claude/mistakes.md` 주석 사본 함정, 6 occurrences). Point at **file + section** instead, and replace the missing identifier with the **structural fact the reader actually needs**: the logging site sits **outside the compile branch that creates the forced-failure tool**, therefore it fires without the forced marker. Record the pre-work line number *and* 「행 번호는 코드가 움직이면 어긋나므로 자리는 파일과 절로 가리킨다」 in the same parenthesis.

**8. 🔴 My own deviation worth remembering: re-wording a handed-over rationale on a **different axis** is a silent spec change.**
The plan's ground ⓑ was 「**전투 씬에 들어가기 전** vs 전투 씬 안」 (a *when* axis). I first wrote 「맵을 보내고 받는 단계 vs 맵을 만들고 새기는 단계」 (a *which stage* axis) — same three sites, different discriminator, and it no longer matched the code comments a reader would compare it against. The code settled it (`OnMapTransferFailed 의 구독자는 **로비 화면의 ViewModel**`), so I reverted to the handed axis and added 「로비 화면」. 🔴 **Keep the axis of a handed-over rationale; if you think the axis is wrong, report it instead of swapping it.**

**9. Another agent editing the same files at the same time.** Observations of *their* half get a timestamp and an explicit 「이 블록은 그 작업의 결과를 판정하지 않는다」; the execution-record row says 「참고 관측(판정 아님)」. 🔴 **Completeness of what you see is not a verdict** (§39-4).

**10. Verification for a letter-prefixed-rule round.** `check_docs.py` = 0 / EXIT=0 says **nothing** about `M-`/`D-`/`L-` rules — it registers only `**규칙 N. 제목**`. So by hand: ① each cited block **name** exists in the document (5 cross-references here) ② pipe count per new table row with `(?<!\)\|` ③ `grep -cF … = 1` for every sentence that had to stay untouched (4 stale lines + the 미정 1·2 items + the 「서로 다른 수단」 line) ④ new quote hits are **all inside my own block** (303·304 kept 1 each; the new hits are 444·445) ⑤ the block line ranges written into the Plan re-measured *after* the blank-line repairs (three of five were off by one). 🔴 **The baseline was stale in the increase direction (another agent's folder, +162) and running `--update-baseline` was forbidden — reported instead.**

**11. 🔴 Never write Korean through `\uXXXX` escapes — write raw UTF-8, and print the line back before trusting it.**
Inserting a pointer line via a Python heredoc with escaped Hangul put **five corrupted syllables into a rule document** (갱신→갱슱, 셌다→셀다, 날을→닠을, 옮겨→옜길, 투영→툼영). Hangul escapes are one wrong nibble away from a different valid syllable, so **nothing fails** — the file just quietly says something else. I caught it only because I printed the inserted line back with `repr()`. 🔴 **The procedure is: raw UTF-8 in the heredoc, then read the inserted line back and eyeball it.** ⚠️ **The same escape mistake had already broken an anchor match earlier in the round** (규칙→귀칙, which failed loudly and was therefore harmless) — **the loud failure is the lucky case; the silent one lands in the document.**

**12. 🔴 A pointer that closes one bullet must say which *neighbouring* bullet it does **not** close.**
The 「닫히지 않은 것」 list had 「전수로 세지 않았다」 (now false) **directly under** 「진짜 실패는 발화 0건이다」 (still true). Both are about the same subject, so a reader arriving at the pointer will take both down. The pointer therefore ends with 「**「세었다」와 「실제로 난 적이 없다」는 다른 사실이다**」 — and the verification is deliberately *not* 「hit count = 1」 but **「the original is at line N, the second hit is my own refusal to close it」**.

**13. Two bookkeeping traps of a multi-turn documentation round.**
- 🔴 **Adding one line above your own earlier edits invalidates every line number you already wrote into the Plan.** One pointer line shifted five recorded block ranges by +1. **Re-measure the ranges after the last insertion, not after each one** — and keep the 「행 번호는 작업 직후의 값이다」 caveat regardless.
- **When the session crosses midnight, the round keeps its own date and the edit date goes in parentheses** (「조사·결정은 2026-09-29, 문서 편집은 세션이 날을 넘겨 2026-09-30 에 했다」, §25). 🔴 **Do not renumber the round** — `(4차)` is the day's round counter for 2026-09-29, and relabelling it would split this work from the three blocks that already cite it.

**14. The final sweep of a round: audit by *fact*, not by document.**
List the facts the round created (here five), then grep each one's **negation** (「아직 안 했다 / 미정 / 미구현 / 세지 않았다」) across the always-on documents. Three outcomes worth distinguishing in the report:
- **정면으로 어긋난다** → fix (in scope) or list (out of scope).
- 🔴 **거짓이 아니고 불완전하다** — a dated row that counted 2 sites where 6 now exist, or a 「완료」 that was scoped to a *different* set of sites. **Say why it is still true**, or the next reader "fixes" a record.
- 🔴 **어긋나는 문장이 0건인데 기록이 0건인 경우** — the round patched a leak whose *premise* a rule body asserts (「방치해도 갇히지 않는다」), and nothing anywhere records that the premise had been broken. ⚠️ **With 실기 미검증 you cannot write 「고쳤다」**, so the finding is 「무엇을 적을지는 테스트 뒤 판단 항목」 — reported, not written.

**15. 🔴 Correcting a sentence **you** wrote earlier in the same round is still a change — report it.**
The round's last turn rewrote one sentence of my own block (a double negative, 「「찾지 못했다」가 「없다」가 아니었던 것이 아니라…」, unpicked into 「「찾지 못했다」는 그 시점에 참이었고, 그 뒤에 「없어서 만든 것」이다」). 🔴 **It is not a B-7 violation** — B-7 protects *records*, and a sentence added minutes earlier in the same round is a **draft**, not a record (checkable: the previous commit does not contain it). ⚠️ **But I reported 「1줄」 while the diff was 2 lines, and the coordinator found the second one.** 🔴 **Count the diff, not your intentions**: every line whose bytes changed goes in the report, self-edits included. And prefer catching the double negative before it ships — 「A가 B가 아니었던 것이 아니라」 in a rule document is an invitation to read the opposite.

**16. A mistake record whose value is the *failure mode*, not the mistake.**
When adding to `.claude/mistakes.md`, the body is 「why nothing caught it」. For the escape corruption: the checker returns 0 because corrupted syllables touch no rule number, link or table structure; the `grep -cF` 무삭제 checks count the *original* sentence, not the new one. 🔴 **So the record's 교훈 must name a procedure no existing tool covers** (read the inserted line back), and ⚠️ **the same root cause that once failed loudly must be written as a contrast** — the loud failure is why the quiet one was not anticipated. ⚠️ **Corrupted glyphs go in a 「쓰려던 글자 → 들어간 글자」 table** so the record does not become the next contamination.

## §47. Recording the **field-test results** of a round whose bug was found by *reading* code — grading 「the log has a place for this claim」 apart from 「the user saw it」, and 🔴 what you may write when the **build itself cannot be identified** (2026-09-30, 경기 종료 후 UI 마무리 / 규칙 D-6)

**Round shape.** Documentation-only half of a cross-day round: research + implementation on 09-29, user test + documentation on 09-30. Code half ran **in parallel in another agent**. Five documents touched (one rule document, three standing documents, the Plan), zero lines of code or scene.

**1. 🔴 A rule that says it *guarantees* something, while the code was breaking exactly that guarantee, is its own class of record — and the rule text is not what gets edited.**
- The repair is **not** 「the rule was wrong」. The rule was right; the code had a hole in the very path the guarantee named. 🔴 **Do not touch the guarantee sentence — it is still the spec.**
- The appended block does three things, in this order: ⓐ names the guarantee **by position** (「본문 마지막 한 줄」) and **paraphrases** it, so no new copy of that sentence enters the document; ⓑ describes the hole **in plain language and by character** (「팝업이 파괴될 때 점유를 놓는 호출이 빠져 있었다」) and **points at the Plan's implementation section as the single source** — no identifier, no file, no line; ⓒ 🔴 **states that the breaking path was the very path the guarantee described.** ⓒ is the whole point: without it the reader cannot tell whether the rule or the code was at fault.

**2. 🔴 A bug found by reading code and never observed in the field closes as 「증상 0건」, never as 「고쳤다」.**
- Three parts, in this order: **the symptom's field-observation record was 0 before the work too** / **the leak path was deliberately reproduced this round** / **the symptom was not observed**. 🔴 **The first part is what makes the third meaningful** — without it a reader takes 「증상 0건」 as a regression check on a known, previously seen bug.
- ✅ **Cite the Plan sentence that ordered the wording, not the result.** Here §3-1 had written 「완료 보고는 「버그를 고쳤다」가 아니라 「빠진 호출을 더했고 증상이 관측되지 않았다」로 적는다」. Quoting that clause into the rule block and the status document freezes the wording so a later round cannot widen it.

**3. 🔴 When the build cannot be identified, say so — and then say what that does *not* invalidate.**
- The compiler-generated coroutine class number in the device log was **identical to the previous round's log**, and this round's fix lived in a different file that does not change that number. So 「this ran the fixed build」 is **unverifiable**, and the only ground is *the user built and ran it*.
- 🔴 **The formulation that overstates neither way: 「이 한계가 판정을 무효로 만들지 않지만 「증상 0건」의 강도를 그만큼 낮춘다」.** Stating only the limit reads as "the test was worthless"; stating only the verdict hides the limit.
- ⚠️ **Mirror image of §43's build-splitting lesson.** There the class number **split** two sessions inside one log file; here the same number **fails to split** two builds. 🔴 **The same measurement answers one question and not the other** — that number moves with the *declaring file's own* method ordering, so it says nothing about a fix in another file. Decide which question you are asking before you reach for it.

**4. 🔴 Grade by asking 「does the log have a place for this claim at all?」 — then never merge two grades that point the same way.**
- Four grades this round: **로그 직접 관측** / **사용자 화면 확인** (the log has no place) / **미실시** / **확인 불가** (no measuring instrument exists at all). The grade table is the artifact; the pass/fail list is not.
- 🔴 **「미실시」 is not 「실패」.** Say **why it was not required** — quote the plan's own sentence (here §8-4: 「이번 회차의 완료 조건이 아니다」) — and what the measurement showed (the forced-failure tool's usage records were 0 and every map-preparation log was on the success side). A stage the plan explicitly excluded from its completion condition does not become a failure by not running.
- ⚠️ **Split 「로그로 볼 것」 and 「육안으로 볼 것」 *before* the test, not after.** This round the two popups under test turned out to have **zero logging calls**, so three checklist items could only ever be eyeball-grade — and that surfaced while writing up results. 🔴 **Counting the log calls in each UI source under test is a one-command pre-flight step** and it changes what you may ask the user to look for.

**5. 🔴 A handed-over 「this fired for the first time」 is a claim to measure, and the measurement is a directory-wide grep of the past logs.**
- The verdict line existed in **five earlier log files** (and the 「request received → verdict」 pair existed too, with measurable gaps). So 「처음」 could not be written.
- 🔴 **But the refutation does not run the other way either.** Whether those earlier pairs had the request popup actually **showing** cannot be known — the popup has no logs — so the earlier rounds' 「실기 미검증」 verdicts are not provably wrong. ✅ **Record both halves and escalate the decision** (`CLAUDE.md` 규칙 12) instead of picking one.
- What the rule block then says is the narrow true thing: **「이번 회차에 이 규칙의 절차를 의도적으로 수행해 로그와 화면이 함께 확인됐다」** — and it says out loud that it is **not** writing 「처음」, with the reason.
- **Handed-over mismatch, 11th class — the claim of *novelty* was wrong, not the number.** Times, counts and mechanism were all right; only 「first」 was false. (12th class, same round: **a figure that does not reproduce because the baseline *line* differs** — 30.109 vs my 30.108, verdict line vs coroutine-body-start line. State your own basis and **do not correct the handed value**; §35's 8th class again.)

**6. Judging whether a roadmap row can come down when the row's own residual list and the handoff's list disagree.**
- 🔴 **Follow the row, not the handoff.** Read every dated block **inside** the row and collect what each said was outstanding — later blocks explicitly retire earlier ones (「아래 2026-09-24 블록의 「새로 생긴 미검증 2건」은 해소됐다」). 🔴 **So a handoff residual may already be closed inside the row** — one of four here was.
- Here 2 of the row's residuals closed and 3 stayed. 🔴 **Write 「하나만으로도 행이 남는다」 and then name which one** — otherwise the next reader has to re-derive the whole judgement to change anything.
- 🔴 **Put the handoff mismatch in the row itself**, because that is where the next person judging this row will look — not only in the report.
- ✅ Mechanics: **marker appended to the 우선순위 cell + dated block prepended to the 작업 cell**, both on one physical line, and the unescaped pipe count per row is re-counted (`(?<!\\)\|` = 5) because a cell edit is exactly where a pipe silently appears.

**7. Cross-day round: the round keeps the date it was opened; only the edit date moves.**
- The `_Tasks/` folder name and the round number are **not** renumbered (§25) — blocks elsewhere already cite them.
- 🔴 **But `WORK_HISTORY.md` gets a row dated the day it *completed*,** because that table records when a milestone landed. Put the split in the row in one clause (「조사·구현은 09-29, 테스트와 문서 반영은 09-30」) or the two dates read as a contradiction against the folder name.

**8. Execution-table rows that belong to a sibling agent stay empty — and you say so *under* the table.**
- Fill only the rows whose content is **the test result**, and mark the implementation half as 「그 에이전트 몫이라 내가 판정하지 않는다」. Anything you measured in code goes in with a **「참고 관측(판정 아님)」** qualifier.
- ⚠️ **A test the plan says is not part of the completion condition gets a note under the table, not a row in it** — adding a row silently grows the item count (§18's closed-list problem in the other direction).

**9. 🔴 Leaving a design document untouched is also a judgement, and 「불필요」 is a claim about *this round*, not about the document being current.**
- Verdict here: **unnecessary** — this round changed the dev tool 0 times and no design clause.
- 🔴 **While judging it I found the document stale for a different reason**: its section heading still reads 「미구현」 and its 「what measures 'no response'」 is still pinned as 미정 with an ordered preference, while this round's log shows the **second** option running at a 3-second interval. ⚠️ **That staleness was not created by this round** (the implementation landed two rounds earlier), so it is reported as an out-of-scope finding and left alone.
- 🔴 **Do not collapse the two statements.** 「이번 회차에 고칠 것이 없다」 and 「그 문서가 최신이다」 are different claims, and reporting only the first as 「불필요」 without the finding would be a false all-clear.

**10. Verification.** `check_docs.py` 0 findings says nothing about letter-prefixed rules (`D-` · `M-`) or about `_Tasks/`·`_Logs/`. Checked by hand: the new rule block is a **separate dated block** (blank line before it — §46-1), the relative link from inside `GameSystemRules/` resolves one level up, unescaped pipes per edited table row, and 🔴 **every inserted line re-printed and read with the eyes** (the Korean-escape procedure — the checker cannot see spelling).

## §48. Adding a **follow-up section** to a finished Plan when the deferred finding is confirmed — a defect that is symptomless **because another defect covers for it**, and 🔴 「관측 불가(구조적)」 as a grade of its own (2026-09-30 (2nd), 팝업 로그 커버리지 / §12-3 후속)

**Round shape.** One document, one appended section (`§14`, 182 lines) + three one-line pointers; 545 → 730 lines. Zero code, zero scene, zero git, `--update-baseline` left to the caller. A sibling agent was editing one of the subject source files **during** the round.

**1. 🔴 A handed-over sentence in quotation marks is a claim about the source document — verify it with `grep -cF` before you quote it.**
- The caller had **summarized** a Plan sentence and passed it as a quote (「고치는 판단은 그 값을 실기에서 본 다음이다」). It did not exist. What existed was 「근거 등급 — 코드 판독뿐」 and 「그것이 바로 이 계획이 넣으려는 줄들이 답해 줄 질문」.
- 🔴 **Repair: quote the wording that exists, in the new section's own head block, and record the mismatch without editing the handoff.** Quoting the non-existent sentence would have made the new section *the source* of a fabricated quote — the same shape as the comment-copy trap, one layer up.
- **This is the 13th handed-over-mismatch class: a summary presented as a quotation.** Distinct from 「가리킨 절 이름이 실재하지 않음」 (§33) — here the *section* was right and the *sentence* was invented.

**2. 🔴 Re-count handed-over counts exhaustively, in the same shape, even when the conclusion direction is right.**
- Handed: 2 occurrences of the defect, 3 control cases. Measured: **3 and 5** — the handoff had swept two of the three log files, not all three.
- 🔴 **The extra cases *strengthened* the conclusion, which is exactly why nobody would catch it.** A mismatch that argues for the same verdict never announces itself. Count first, agree second.

**3. Pin the **denominator** in the document, next to the numbers, before anyone reads them.**
- 「에디터 201행 중 19줄」 was **not** the file (792 lines) but the **first play session's segment** (lines 2–202). Editor logs accumulate across sessions (§10-0 of that Plan says so); one file held two sessions, and the file-wide figure was 26 lines.
- Two device-log counting traps, both worth one line in the document: logcat carries **a call stack under every app log line** (so word-counting inflates), and a `WARN` line carries a **different priority letter**, so pinning the letter drops exactly one line (1,154 vs 1,155).
- 🔴 **State the basis as a short block *above* the tables** — 「세는 기준을 먼저 못 박는다」. A number whose denominator is implicit is a figure that will be "corrected" by the next reader.

**4. When the document has no slot for the new material, append a new top-level `§N` and touch no existing number.**
- The caller suggested 「§10 의 실기 결과로 넣을 자리가 있으면 거기에」. There was none — `§10` was procedure end to end. So: new `§14` at the end, existing `§0`~`§13` untouched (§25's no-renumber rule).
- ✅ **Three one-line pointers instead of edits**: on the deferred finding (§12-3), and on the two places whose completion criteria the new facts invalidate (§9-4, §10-1). Each says 「원문은 고치지 않았다 · 단일 소스는 §14」 (B-7 + §33's cell-level pointer form).

**5. 🔴 A defect that is symptomless **because a second defect is covering for it** — the artifact is an ordering table, not a severity claim.**
- Three columns: **now** / **fix ② only** / **② plus the net already shipped**, with rows for 「the stray release」, 「what cleans up at destroy time」 and 「visible symptom」. The middle column is where the argument lives: fixing ② alone makes the overlay follow the player into the lobby.
- 🔴 **The thesis sentence is not 「고칠 만하다」 but 「지금이 아니면 순서가 뒤집힌다」** — the net landed first, so the order is right; the reverse order would have produced a real bug **with candidate causes split across two defects** (§20's gate-stage reasoning, applied to a fix order instead of a test order).
- ⚠️ **Say why it is harmless *today* and in the same breath why it is latent**: one unconditional call site, on the way out to the lobby, so there is nobody to steal from. 🔴 **「호출처가 1곳」은 오늘의 사실이고 사양이 아니다** — that clause is what makes it a latent defect rather than a non-defect.

**6. 🔴 「관측 불가 (구조적)」 is a grade of its own, and it is not 「미검증」.**
- Four grades this round: **① 로그 직접 관측 / ② 코드 판독 / ③ 🔴 관측 불가(구조적) / ④ 예상(판정 미정)**. Grade ③ carries its own sentence: 「지금 있는 수단으로는 볼 수 없다는 것까지가 결론이다. 「테스트를 안 했다」가 **아니다**.」
- Two independent reasons made it unobservable, and **both** belong in the record: the covering defect zeroes the counter first (reason 1, grade ①+②), and the log owner's own shutdown path can close the file sink before the object is destroyed, because Unity's destruction order is not guaranteed (reason 2, grade ② + the log that ends mid-session as evidence).
- 🔴 **When the caller's own test procedure cannot reach it, write that down** (`CLAUDE.md` 규칙 10): both proposed methods — 「60초 방치」 and 「에디터 Play 정지」 — were structurally impossible, one per reason.
- 🔴 **Leave the now-unreachable completion gate standing and mark it.** §10-1 said 「4·5번 줄 중 하나라도 없으면 완료가 아니다」; the pointer says the gate cannot be met by that procedure and that **lowering it is not this section's call** — it waits on the grade-④ prediction being judged.
- ⚠️ **One claim inside a graded block turned out to belong to a different grade** (「강제 종료 시 파괴 콜백 미보장」 is platform general knowledge, not code reading). 🔴 **Mark the exception inside the bullet rather than moving it** — and replace the part you cannot measure (what the tester did with the other device) with what you can (all three logs are the surviving side, disconnect-detection lines 3 · 1 · 1).

**7. 🔴 A prediction stays a prediction, and its judge is named.**
- 「고치면 관측이 가능해질 것이다」 is written with its mechanism (the log owner closes on **play-mode exit only**, and a scene change is not that) *and* with 「이것은 예상이다. 아무것도 확인하지 않았다」 plus the judging agent. ⚠️ **Also say what a correct prediction still would not prove** — that a means of observation exists is not that the net is verified.

**8. Self-corrections of sentences you wrote **this round** are changes, and they go in the report (three here).**
- A figure copied from the wrong denominator (file lines where app-log lines were meant); an evidence-grade mislabel inside a graded block; an unverified claim about the tester's actions. §46's lesson applied without being prompted — **count the diff, not the intent**.

**9. Verification for a `_Tasks/` edit — the checker is silent, so the real checks are four.**
- 🔴 **Forbidden-token grep restricted to the added line range**, not the file: log field keys, boolean field names, helper and close-method names, and the log message texts. Pre-existing hits must be **located by line number** to prove they are not yours (three hits here, all in the §7 baseline table).
- Per-row unescaped pipe count (`(?<!\\)\|`) over every new table — 7 tables, including one whose header first cell is empty.
- 🔴 **Every inserted line re-printed and read with the eyes** (the Korean-escape procedure), and `grep -cF == 1` on each anchor sentence that must stay unmodified.
- ⚠️ **State in the report that `_Tasks/` is outside the checker's scan set**, so 0 findings says nothing about the file you just wrote (§11).

**10. 🔴 「`--update-baseline` 금지」 in a caller's instruction means 「the main session runs it at the end」 — it is not a reason to defer a memory update.**
- This round I read it as a conflict with the standing rule 「증가 방향 갱신은 미루지 않는다」 and escalated (규칙 12), which cost a round trip. The caller's clarification: they run it themselves at the end of every round.
- ⚠️ A stale-baseline **notice** for another agent's folder is information, not a finding — it does not affect the exit code, and it belongs in the report as-is.

**11. A sibling agent editing one of the subject files **during** the round (third sighting — §33, §39).**
- The new section says explicitly that it **asserts nothing about that file's current content**, and that the code reading it relies on is 「2026-09-30 그 시점의 관측」. 🔴 Completeness of what you see is not the test; 「did this round verify it?」 is.
- ✅ A read-only `find -newermt` over the repository is a cheap way to state your own change scope exactly — it showed one file changed by me and three by the sibling.

## §49. The round where a 「관측 불가 (구조적)」 verdict was **overturned by a fix** — recording a defect that is symptomless **because two defects cover for each other**, and the third sighting of build identification (2026-10-01, 팝업 로그 커버리지 §14-10 · 규칙 D-6)

**Round shape.** Five documents: one rule document (new dated block), three standing documents, the Plan (new `§14-10` · `§14-11` + four one-line pointers + two execution-table cells + one note under the table). Zero code, zero scene, zero git, `--update-baseline` left to the caller. The subject fix was implemented by a **sibling agent**; my half is the field-test write-up.

**1. 🔴 A 「관측 불가 (구조적)」 grade can be overturned without the earlier verdict having been wrong — and the difference is the artifact.**
- §48 graded the destroy-time net as **③ 관측 불가(구조적)** for two independent reasons: (1) a second defect zeroed the counter first, (2) the editor's log sink can close before the object is destroyed.
- This round reason (1) **ceased to exist** because the second defect was fixed. The net fired and was logged.
- 🔴 **Write 「정정이 아니라 전제 변경에 따른 갱신」 explicitly.** What made that sentence available is that §14-4 had pinned its own scope: 「지금 있는 수단으로는 볼 수 없다는 것까지가 결론이다」. 🔴 **So a grade written with its scope sentence lets the *next* round upgrade it instead of retracting it** — that is the practical reason the scope clause is worth the words.
- ⚠️ **Say which reason survived.** Reason (2) is untouched: this round's observation came from a **scene change**, not from stopping play mode. A grade with two reasons is closed one reason at a time.

**2. 🔴 A defect that is symptomless because **two defects cover for each other** needs one sentence that all three standing documents share.**
- The sentence: 「결함 두 개가 서로를 가려 증상이 0건이던 상태」가 「각자 자기 몫을 책임지는 구조」로 바뀌었다. It goes in the status paragraph's **header**, in the history row's **title**, and in the roadmap row's dated block — because each of those is read by someone who will otherwise read 「증상 0건」 as 「정상」.
- 🔴 **「증상 0건」 ≠ 「정상」 must be written as a literal clause**, not implied by the ordering table. §48's three-column table argues the *fix order*; this sentence states the *state*. Both are needed, and they are different artifacts.
- ✅ The plain-language opening (`CLAUDE.md` 규칙 13) carries it best: a counter several windows share, one window not releasing, another releasing what it never took, and the second accidentally cleaning up after the first.

**3. 🔴 A handed-over 「before the fix it looked like this」 is a claim about the **old** logs — re-measure it there.**
- Handed: 「수정 전에는 바로 그 자리에서 막이 꺼졌습니다(05시대 3건이 그 모양)」. Measured: those three have **no overlay-off line after them either** — the count was already 0 and the no-negative guard swallowed the call. My own §14-3 had already recorded exactly that.
- 🔴 **The conclusion was unaffected, but *what distinguishes before from after* changed completely.** The discriminators are (a) the number of boolean values carried on the close line and (b) the counter value at that moment — **not** the presence of the overlay-off line.
- **14th handed-over-mismatch class: the verdict is right and the control case is wrong.** Distinct from §48's 2nd (counts short) and §47's 11th (novelty claim false): here the *mechanism offered as contrast* did not exist. 🔴 **A contrast you did not measure is the easiest thing to copy into a document, because it is never the sentence under dispute.**
- A 5th-class cousin in the same handoff: 「§14-8 의 정정이 맞았다」 pointed at a correction that **is not in §14-8** (grep for both of its keywords over the whole Plan: 0 hits). §48-1's shape again, one layer further out — there the *sentence* was invented, here the *correction itself* was.

**4. ✅ Build identification, third sighting — this time the discriminator is the **field composition of a log line**.**
- §43: the compiler-generated coroutine class number **split** two sessions inside one file. §47: the same number **failed to split** two builds, because it follows the *declaring file's own* method ordering. §49: the close line gained one boolean, so the **format itself** differs (8 pre-fix lines ↔ 3 post-fix lines, directory-wide grep).
- 🔴 **Decide what you are asking before choosing the instrument.** A line's field composition reveals changes **to the file that prints that line** and nothing else; say so in the same breath: 「증명하는 것은 「그 표시가 들어간 빌드였다」까지다」.
- ⚠️ This mismatch argued **for** the handoff's conclusion (it said only indirect evidence existed), so it would never have surfaced on its own — §48-2 again. Count first, agree second.

**5. 🔴 「증상 0건」 gets stronger when you measure the **precondition**, not just the absence.**
- The stray-release site was reached this round **while another popup legitimately held the overlay** (counter = 1, held since 53 seconds earlier, no release line in between). §14-3's three earlier cases were all at counter 0, and this round's device case too.
- 🔴 **So the claim becomes 「위험 전제가 처음 성립한 자리에서 수정이 작동했다」**, which is checkable, instead of 「증상이 없었다」, which is also true of a build that never reached the site.
- ⚠️ **「수정 전 빌드였다면 남의 몫을 가져갔을 것」 is 구조 추론, not observation** — no log of that build in this configuration exists. Keep it in its own grade row (§37's lesson).

**6. ✅ A completion gate has two different endings: 「내린다」 and 「충족됐다」.**
- §48 left §10-1's gate (「4·5번 줄 중 하나라도 없으면 완료가 아니다」) standing, marked it unreachable, and said lowering it was not its call. This round the **same procedure produced both lines**, so the gate is **met** — and the deferred decision ends as 「내릴 필요가 없다」.
- 🔴 **Write that ending in the gate's own section as one appended line** (B-7, original untouched), and point at the new section as the single source for both the measurement and the premise change.

**7. Denominator re-pinning, third round running — and the editor log now holds **three** sessions.**
- Handed `[WARN]` 에디터 7 is **file-wide across three play sessions**; this round's segment has **2**. The device figure (2) matched exactly.
- 🔴 **State the basis above the tables and leave the handed number alone** (§48-3). The session count grows silently: §48 wrote 「세션 2개」 and that is already stale for the same file.

**8. The sibling agent's rows stay empty — including rows whose state is an **approval**.**
- §47-8 again, with a new variant: two rows read 「⬜ 승인 대기」 while the logs show lines that only the approved work could print. 🔴 **「승인됐다」 is not mine to judge** — the note under the table records the observation as 「참고 관측 — 판정이 아니다」 and says adding rows would silently grow the item count (§18).
- I filled only the two rows that are mine: the user's field test and my own rule-document update.

**9. 🔴 My own edit makes *other* blocks' line-number references stale.**
- The caller pointed at ROADMAP 「194행」 (it is further down) and 「213·214행」 (correct at the time). Prepending my header block then pushed 213/214 down.
- 🔴 **Repair: in my own sentences point by **name**, and append to the stale reference 「그 두 숫자는 이 블록이 더해지면서 밀렸다」 without editing the earlier block.** §41-6 said line numbers you cite go stale because of your own edit; this is the outward-facing half — *someone else's* citation goes stale because of your edit.

**10. 🔴 A factual error in the handoff is a `mistakes.md` candidate, and the judgement goes to the user when they pre-judged 「불필요」.**
- The caller's preliminary verdict was 「mistakes.md 불필요 (새 실수 0)」, but that premise was formed without knowing their own control-case claim was wrong (point 3). There is precedent for 메인 세션 entries in that file (2026-09-29, a miscounted out-of-scope list).
- 🔴 **So: record the mismatch in its established home (the Plan's 인계 어긋남 table), report the candidacy, and do not write the entry on my own** (`CLAUDE.md` 규칙 12 — a conflict between my finding and the user's stated judgement is escalated, not resolved silently).
- Same shape for `TechnicalDesignDocument.md`: verdict 「이번 회차에 고칠 것이 없다」, and 🔴 **that is not 「그 문서가 최신이다」** — its 「미구현」 heading and 「무엇으로 응답을 재는가 미정」 pin are still stale from two rounds back (this round's log shows the watch running on a 3-second heartbeat again), and that stays an out-of-scope finding (§47-9).

**11. Verification.** `check_docs.py` says nothing about `_Tasks/`·`_Logs/` or letter-prefixed rules (`D-` · `M-`). Done by hand: forbidden-token grep (log field names, message texts, class names) **restricted to the lines I added** to the rule document — 23 tokens, all 0; per-row unescaped pipe count (`(?<!\)\|`) on both edited table rows (ROADMAP 5, WORK_HISTORY 3); every inserted line re-printed and read with the eyes (the Korean-escape procedure); and `grep -cF` = 1 on each anchor sentence that must stay unmodified.


## §50. A round where **two of five handed-down findings did not survive re-measurement** — and why the answer was to add the *verified* row instead of the requested one (2026-10-01 (2nd), rules-implementation-audit §12)

**What the round was.** The caller handed over six already-"measured" findings to append to a 6-bucket audit section, plus a completion table and a history write-up. Re-measuring every one of them, as the prompt itself demanded, changed the outcome of **two**.

**Rule confirmed: a handed-down finding is an input to measurement, never a result.** (Same rule as the 2026-08-18 handover-figures entry, now with a second kind of failure.) The two kinds seen here:

1. **The direction was reversed.** The claim was *"a formula in the design doc and in `.claude/MEMORY.md` is 4 bytes wrong."* Measuring the artifacts the formula describes settled it the other way — the **documents were right** and a **code comment** carried the pre-version-bump value. So the row went into the **code-comment bucket (5)**, not the **false-document bucket (4)**, and the two documents were **left untouched**.
   🔴 **How it was settled:** measure the thing itself, not the documents about it. `wc -c` on the generated template assets + the per-template parameter table → plug both candidate formulas in → one matches every file, the other misses every file. **A formula claim is always decidable this way; never arbitrate it by counting how many documents say which.**
2. **It already had a number.** The claim *"a rule document still describes a deleted field in the present tense"* was **already** an item in the same table, whose own 「where to fix」 cell said **two places**; the other place had just been closed by this round's code work. So the answer was **"this is the remaining half of an existing item"**, not a new row. 🔴 **This was the second time in the same audit that the caller asked for a number that already existed** — before adding any row to a table you authored, grep the table for the *claim*, not for the proposed number.

**Rule: when counts grow because the same falsehood was found in more places, say so in the heading area.** Three of the new rows were **additional copies** of falsehoods already counted; only one was a new falsehood. Writing just "6 → 10" invites the reading "ten distinct lies". The wording that works: *the kinds did not grow, the places to fix did* — then cite the table's own existing precedent for counting copies separately (an earlier pair in the same section was already split across two buckets precisely because the point was that there were **two** places).

**Rule: 「partially done」 must name the leftover and its item number on the same line.** Three items in the completion table were partial because the fix order had been scoped to the two known spots, which left **three files self-contradicting** (one sentence fixed, the file's head comment still carrying the old claim). Writing ✅ for those would have hidden a state that is *worse than before the fix*, because a file that disagrees with itself gives the next reader no way to tell which half is current.

**Rule: when the documented cause of a miss was 「a cell too hard to read」, the fix is N short rows, not one more cell.** The audit's own §1 found that the buried item had been **written down correctly all along** inside one roadmap cell, mixed with four other leftovers, strikethroughs and update blocks. So the tracking for the audit's leftovers was laid out as **one row per bucket**, each row short, with the details pushed to the audit document. 🔴 Putting all six buckets in one cell would have reproduced the exact mechanism being recorded.

**Rule: do not let 「the checker's scope is narrow」 become 「the checker was the problem」.** `check_docs.py` performs its seven checks correctly and two of them exist because of real, measured memory losses. The sentence that is accurate: *the checks are document-to-document and read no `.cs`; what was wrong is that a 0-finding result was passed on as a guarantee wider than its scope.* Write the scope as a **fact**, never as a verdict on the tool.

**Reusable measurement moves from this round**
- A comment block spanning several lines: cite it as a **range** and say so, rather than picking two line numbers out of it — the caller's "lines 13 and 15" were one continuous block whose middle line was a neutral continuation.
- A line-number claim inside a header comment is worth re-counting even when the caller already corrected it once: of the five locations handed over, **one was off by one** (the sentence sat one line above the number given) while another the caller had "corrected" was in fact right as corrected.
- To show a placement comment is false, prefer evidence from **the same file plus the asset**: the counter-sentence in the file, the line of *code* that actually does the opposite, and the prefab's own root component. Three independent layers beat one.
- Where a deprecated identifier must be pointed at, point with **「the two list fields of the production panel」 + a line number**. Then grep your own additions for every banned name and show the count is 0. One of the handed-down claims ("0 hits in code") held for only **one** of the two names on that line — the other is live in a script and a scene, so a careless fix would have deleted a field the rule still needs to describe.

---

## §51. The round that **recorded the first comment audit** (162 false comments, 89 files) — insertion-only documentation, hand-over figures that disagree with each other, and a four-way split of "could not be judged" (2026-10-06, rules-implementation-audit §12-6-2 ~ §12-6-9 + a new Plan)

**What the round was.** A coordinating session had finished a full read of the comments in 89 `.cs` files (UI + random map), fixed 162 false ones, and recorded none of it. The job: extend the section-12 "class 5" (false code comments) of the audit Research with the results, make five newly discovered facts into sections of their own, record the could-not-judge set, create the missing `Plan.md`, and correct four other records. **Everything was insertion-only** — the Research went 1,815 → 2,096 lines with **0 changed or deleted original lines**.

**Rule: the existing 10 rows of a class are not the result of an inspection — say so before extending it.** The earlier rows had been caught *sideways* while comparing rules with code. Extending the class without that sentence reads as "ten false comments exist". Put the sentence first, keep the old rows untouched, and **do not add the new population into the old totals** (different population; say that you did not, and put "how to sum" on the user's decision list).

**Technique: how to verify "insertion-only" mechanically.** Copy the file to the scratchpad first, then diff by lines with `difflib` and count orig lines that are `delete`/`replace`. A pointer appended *to the end of an existing table row* is a **changed line** (counts as a deletion), so put pointers on **new lines** next to the row, not inside it. To insert in the middle of a file, use a unique heading (or a `---` + heading pair) as the `Edit` anchor and re-emit the anchor unchanged. Also run an unescaped-pipe count per table block on every new table (zero mismatches here).

**Handed-over figures that did not survive (13th–19th kinds; every one was kept as handed over and the disagreement was written next to it)**
1. **A column that does not sum to its stated total** — the per-owner file column summed to 90, the stated total was 89. Resolved by *locating* the double-counted row (a file re-judged in its own row but living inside the UI folder that was already counted) — "appears to be" was written, not "is".
2. **A scope description that lists a component twice** — a map-evaluator file was listed separately although it sits inside the 20-file folder listed before it (so 26 is right, 27 would be wrong). Check by listing the folder, not by trusting the enumeration.
3. **The same fact stated both ways inside one hand-over** — two files were on the "0 false, never modified" list **and** on the "literal changed" list. Find the **only reading that satisfies both** (earlier round fixed them, last re-read found 0), label it *a reading, not what was handed over*, and do not put them under "unmodified".
4. **"The old number was wrong" that was really a scope split** — a title said 23 rows / 8 files; the hand-over said it should be 19 / 7. Re-measuring reproduced **both**: 19 / 7 is one family of markers, the other 4 rows / 1 file is a different family with a different delete condition. The correction block therefore *splits* the number and says which each half counts; it does not replace it. The same hand-over claimed the 4 rows were caught by no search — the loosest search (the one for "scheduled for deletion") does catch them.
5. **"The history document also says X" that is a dated history row** — a status table row in present tense is a wrong claim; a row dated months ago in a chronological history is a record that may have been true then (and a later row of the same document already records the replacement). Report them as **different problems**.
6. **A "remaining" list that omits leftovers of an earlier open item** — re-measure the **current state of every earlier open row** of the same class before accepting "these are the remaining ones". Here three earlier rows had since been closed by someone else, one still had two un-fixed files outside the audited scope, and one citation pattern still stood in three places in two files that nobody had listed.
7. **A column that mixes two kinds of number** — the "classification" column of the approval-wait list held class numbers on some rows and item numbers of the Research on others. Copy it verbatim into a column of its own and add a separate "what it corresponds to in the Research" column that you verified yourself.
Also: a count of the form "about N" was written as "about N" and the arithmetic disagreement (rows summed to ~1,500) written beside it.

**Rule: a "judged and true" list is a different state from "not judged".** Write the unmodified-file list explicitly, add "files not on this list are either fixed or out of scope, never *true*", and say what the list saves (the next round does not re-read them). Without it the next round re-reads everything.

**Rule: the could-not-judge set is four sets, and the splitting key is "what makes it resolvable".** (past → never, because the only source is version history and the project forbids it; engine internals → the package sources are absent from the checkout, cheapest path is to plant one log line and let a field run move it to the fourth set; screen → a screenshot or a profiler run, which belongs in the test-case file; field observation → one reproduction or a planted log.) 🔴 **If the per-set counts are not known, say "not known" and make assigning them step 0 of the plan** — do not apportion the total.
Add the paragraph that field tests confirm **behaviour**, not the **why** a comment claims (engine internals, performance, layout arithmetic); "the feature passed field testing" is not evidence for such a comment.
A permission question can sit **under** a set: whether version-history reads are allowed decides whether the "past" set can ever be resolved, so link them in both directions and do **not** touch `CLAUDE.md` (a rule change is the user's).

**Rule: do not copy the false sentences you are recording.** The five new facts are *about* false comments, and quoting them verbatim creates the very residual-grep copy the audit was removing. Paraphrase in prose; cite the **place** (file + line as of this date, marked "line numbers are as of working time"). Where a search term is needed to reproduce a measurement, use a term that is a real, valid identifier (a component name), not the discarded claim. The one place an old verdict is quoted is a reviewer's past "all true" verdict, because the quoted *act* is the finding.

**Rule: "I opened the section" ≠ "the clause governs this sentence".** One behaviour (reset un-mutes) lived in rule 27 while the rule whose *title* names the reset button (rule 25) says nothing about mute — so a word-driven reader lands on the wrong rule. For the record: ask of every rule citation *which clause defines this behaviour*, not *does the section contain the word*. The structural fix is a pointer in the rule document, which is an approval item, not a comment fix.

**Operating rule recorded (and its lineage).** If the file under judgement is pure C# (no engine / reactive / TMP / tween / netcode references), compile it with `mcs` and run it with `mono` **before** leaving a claim as "cannot judge". It is the same root as the earlier "check whether the type really touches the engine before handing a task to the user" lesson in the shared memory — not a new rule; promoting it into the shared memory is on the approval list.

**Own mistake of the round.** I ran a read-only version-control command (`status`) as my first step, reading rule 5 of `CLAUDE.md` as "destructive commands only". The coordinating session had made the same reading the same day. Recorded in `.claude/mistakes.md` (2026-10-06) and put to the user as an approval item rather than a personal-care note. Use `wc -l` / `ls` / reading the file for "what am I about to edit".

**Process note for a long, interruptible round.** The session was cut off once by a usage limit and the coordinator asked for **one section per write, with a line count after each**. Do it from the start: after each inserted section run the line-count + zero-deleted check; the file was in a valid, checked state after every step.
