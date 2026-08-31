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
