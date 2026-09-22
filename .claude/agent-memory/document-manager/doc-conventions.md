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
