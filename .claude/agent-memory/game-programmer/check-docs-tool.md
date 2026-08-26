# `Tools/check_docs.py` — 문서/메모리 검사 도구

> This file was split out of `MEMORY.md` (2026-08-25) — the index only keeps a 6-line pointer.
> The Korean block below is the moved original text, kept verbatim (`.claude/MEMORY.md`
> 「📝 Documentation Language」: existing Korean is not translated wholesale).
> New sections are written in English.

## 기본 사용법 · 함정 (색인에서 옮겨 온 원문)


- 읽기 전용 검사기. 기본 실행 `python3 Tools/check_docs.py` → **0건 / 종료 코드 0** 이 기준선이다.
- 검사 **7종**: `[1]~[5]` 문서 참조 정합성(기존) · **`[6]` 고아 토픽 · `[7]` 폴더 총합 행수 감소**(2026-08-21 추가).
- `[6]`·`[7]` 은 `.claude/agent-memory/` 를 본다. 경로 인자는 **`--memory-root`**(기본 `.claude/agent-memory`).
  🔴 **`--root` 를 메모리 폴더로 돌리지 마라** — `parse_rule_docs()` 가 하위 `GameSystemRules/` 를 전제해서
  그 폴더가 없으면 `docs` 가 빈 딕셔너리가 되고 **`[1][3][4][5]` 가 조용히 "이상 없음"** 을 낸다.
- `[7]` 기준값은 **`.claude/agent-memory/_baseline.json`** (폴더별 `files`/`lines`). **임계값 0 — 감소는 전부 보고.**
  도구는 이 파일에 **절대 쓰지 않는다**. 갱신은 `--update-baseline` 플래그로 사람이 명시적으로만 한다
  (자동이면 사고가 그대로 새 기준이 되어 도구를 한 번 돌리는 것만으로 사고가 지워진다).
- **`--reason` 사용법 (2026-08-24 커밋 `2b3f2c6a` 신설)** — 감소를 기준값에 반영하려면
  `python3 Tools/check_docs.py --update-baseline --reason "왜 줄었는지"` 가 필요하다.
  사유 없이는 **거부(EXIT=2)** 되고 **기준값 파일은 열리지도 않는다**(불변).
  사유를 주면 `change_log` 에 날짜·폴더별 증감·사유가 **자동 기록**된다.
  - **증가만이면 `--reason` 없이 통과**한다(`change_log` 미기록. `--reason` 을 주면 opt-in 기록).
  - **`--reason` 만 주고 `--update-baseline` 을 빠뜨리면 오류로 차단**된다 — 조용히 무시하면
    "사유를 남겼다"고 착각한 채 지나가기 때문이다.
  - 같은 커밋에서 `실제 > 기준값` 인 폴더를 **안내 블록으로 출력**하게 됐다(드리프트 알림).
    **문제로 집계하지 않고 종료 코드에도 넣지 않는다** — 집계하면 `WORKFLOW.md` [11]③ 의 "0건 확인"이 막힌다.
- 🔴 **`.claude/MEMORY.md` 를 참조할 때 행 번호를 쓰지 말고 「갱신 규칙 N」으로 쓴다.**
  2026-08-24 에 `32~44행` 표기가 실제로는 `51~61행` 으로 밀려 docstring·`[6]`·`[7]` 출력·`_baseline.json`
  참조가 전부 어긋났다(가리키던 자리는 아키텍처 제약 표였다). 행 번호는 문서가 자라면 반드시 거짓이 된다.
- 같은 파일의 **`known_orphans`** 목록에 있는 `[6]` 항목은 출력에만 보이고 종료 코드엔 안 들어간다.
  **줄이는 방향은 자유, 추가는 사람 승인.** 현재 등록: `project-orchestrator/roadmap-3d.md`(224행).
- ⚠️ 「`MEMORY.md` 200행 초과」 검사는 **계획에 있었으나 폐기됐다.** 호출 세션의 프로브 4건에서
  `MEMORY.md` 가 에이전트 시스템 프롬프트에 **아예 자동 주입되지 않음**이 확인돼(200행에서 잘리는 게 아니라
  0행이 실린다) 전제가 무너졌다. *(내가 직접 측정한 값이 아니라 전달받은 값이다.)*
  → 그래서 신규 검사 번호가 `[6]`·`[7]` 이다. 옛 Plan 문서의 `[6][7][8]` 3종 번호와 어긋나니 주의.

---

## Scope split: "where rules are defined" vs "where references are searched" (2026-08-25)

The checker carries **two ranges with different jobs**, deliberately kept on separate CLI args.

| Range | Source | Function |
|---|---|---|
| ① rule definitions | `--root`/`GameSystemRules/*.md` **only** | `parse_rule_docs(root)` (line 308) |
| ② reference search | `--root` subtree + repo-root `AGENTS.md`·`CLAUDE.md` + `.claude/**` | `collect_files(root, claude_root)` (line 286) |

**Only ② was widened.** Because the two are separate, widening ② cannot disturb rule-definition
parsing — that is the whole point of the split. Merging them into one arg is exactly what makes
the tool die silently: point `--root` at `.claude` and `GameSystemRules/` disappears, `docs`
becomes `{}`, and checks `[1][3][4][5]` all print "이상 없음" while checking nothing.

What was added (each line number verified against the code on 2026-08-25):

- `DEFAULT_CLAUDE_ROOT = ".claude"` (line 181) and `CLAUDE_EXCLUDE_DIRS = ("skills", "plugins")`
  (line 187) — `skills/`·`plugins/` are externally distributed docs this project does not own,
  so counting them as problems would permanently block the "0건 확인" step of `WORKFLOW.md` [11].
- `is_excluded_claude_dir(path, claude_root)` (line 245) — compares only the **first** path
  segment under `claude_root`. Kept separate from `is_excluded()` (`_Tasks/`·`_Logs/`) on purpose:
  the exclusion *reasons* differ ("history, never retro-edit" vs "external, not ours to fix"),
  and merging them would erase which reason applied.
- `collect_claude_files(claude_root)` (line 265).
- Signature change `collect_files(root)` → `collect_files(root, claude_root=None)` (line 286).
- CLI `--claude-root` (line 898) and `--no-claude-docs` (line 905). **This is opt-OUT**:
  `.claude/` is included by default and must be explicitly disabled. Rationale recorded in the
  code: a check you have to switch on is a check nobody switches on.
- New `[검사 범위]` block printed before check `[1]` (line 954) — so a run that says "이상 없음"
  can be told apart from a run where nothing was in scope to begin with.

Measured effect: reference-search targets **35 → 73** `.md` files (`.claude/` contributed 38).
These are point-in-time counts from 2026-08-25; both numbers grow as docs are added — what
matters is the **delta**, i.e. roughly the whole `.claude/` tree minus `skills/`·`plugins/`.
(Right after this file itself was created the same run printed 75 / 40.)

### Regression technique worth reusing

**Keep the opt-out flag and diff against it.** `python3 Tools/check_docs.py --no-claude-docs`
reproduces the pre-change output exactly, apart from the newly added `[검사 범위]` block.
Leaving an opt-out flag behind gives a free before/after oracle for any scope widening —
no git needed (CLAUDE.md rule 5 forbids git anyway).

---

## 🔴 Known limitation — 33 rules are invisible to the checker (UNRESOLVED)

Confirmed by reading the code on 2026-08-25. **No code was changed.**

`parse_rule_docs()` recognizes a rule definition **only in bold form** `**규칙 N. 제목**`:

    check_docs.py:341   m = re.match(r"^\*\*규칙\s*(\d+)[.\s]\s*(.*)", line)

and at the end registers a document only `if nums:` (line 357). A document containing **zero
bold rules is therefore never added to `docs` at all** — not "registered with max 0", absent.

⚠️ Module-level `RE_RULE_DEF` (line 198) *looks* like the rule-definition regex but is
**dead code — referenced nowhere.** The pattern actually in use is the inline one at line 341.
Editing `RE_RULE_DEF` changes nothing. (`RE_H2`, line 201, *is* used — at line 333.)

Measured over `Assets/_Project/Docs/GameSystemRules/` (13 `.md`):

| Registered — 7 docs | bold rule count |
|---|---|
| AI 36 · Buildings 47 · CanvasSortingOrder 3 · Skills 26 · Sound 27 · UI 70 · Units 44 | all bold |

| NOT registered — 6 docs | why |
|---|---|
| **Map (5) · RandomMap (15) · Upgrade (13)** | headings are H2 `## 규칙 N.`, not bold → **33 rules lost** |
| AI_Scenario_Human · _Spirit · _Transcendence | no numbered rules at all (0) |

**Impact: a rule-number reference pointing at Map / RandomMap / Upgrade passes check [3]
unconditionally.** You can cite a rule number that does not exist and nothing catches it.
Evidence — every consumer of `docs` bails out when the document is absent:

    [3] check_docs.py:1025   if doc in docs and max(lo, hi) > docs[doc]["max"]:
    [4] check_docs.py:1049   if doc not in docs or not docs[doc]["per_section"]: continue
    [5] check_docs.py:1075   if doc not in docs or n not in docs[doc]["titles"]: continue

⚠️ **Extra trap for whoever fixes this.** In those 3 docs the line `## 규칙 N.` is matched by
`RE_H2` and parsed as a **section name**, not as a rule. So merely re-formatting the headings
to bold is *not* a fix: the rules would then sit in no enclosing H2 section, and `per_section`
/ `sections` / check `[4]` are built from those sections. A fix has to decide what the H2
sections become as well. Consider this before choosing a direction.

**Status: unresolved.** Reported to the user on 2026-08-25; handling not yet decided.
