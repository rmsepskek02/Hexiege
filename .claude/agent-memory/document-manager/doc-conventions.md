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
