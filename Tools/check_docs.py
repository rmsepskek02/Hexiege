#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Hexiege 문서 정합성 검사기
==========================

문서끼리 서로를 잘못 가리키고 있는 곳을 찾아내는 읽기 전용 도구다.
문서를 고치지 않고 **문제 목록만 출력**한다.

왜 필요한가
-----------
`GameSystemRules_UI.md` 와 `GameSystemRules_Buildings.md` 는
**섹션마다 규칙 번호를 1부터 다시 매긴다.**

    GameSystemRules_UI.md
      공통 UI 규칙           규칙 1~11
      생산 패널 UI           규칙 1~28
      MistShrine 패널 UI     규칙 1~15    ← "규칙 14" 가 여러 개 존재
      ...

그래서 "UI 규칙 14" 라고만 적으면 **어느 섹션인지 알 수 없다.**
사람이 눈으로 훑어서는 놓치기 쉬우므로 기계가 잡는다.

에이전트 메모리도 함께 지킨다 (검사 [6]·[7])
---------------------------------------------
이 도구는 문서 참조뿐 아니라 **`.claude/agent-memory/` 의 무결성**도 본다.

에이전트가 자기 기억 노트를 "정리"하다가 내용을 통째로 날리는 사고가 반복됐기 때문이다.

    2026-08-17  game-programmer/MEMORY.md  -378행
                파일 수는 19 → 19 로 그대로인데 폴더 총합만 2,503 → 2,125.
                즉 다른 파일로 옮긴 게 아니라 그냥 사라진 것이다.
                → 3일 동안 아무도 몰랐고, 발견한 것도 우연이었다.
    2026-08-20  같은 파일 -18행 (폴더 총합 기준 -5행)

사람 눈으로는 못 잡는다. 노트는 겉보기에 멀쩡하고,
없어진 줄이 무엇이었는지는 없어진 뒤에는 알 방법이 없다. 그래서 기계가 잡는다.

준거는 `.claude/MEMORY.md` 「🔴 에이전트 메모리 갱신 규칙」이며,
그중 **기계가 집행할 수 있는 5번·6번**을 각각 검사 [6]·[7] 로 구현했다.

    갱신 규칙 5  링크 없는 토픽 파일은 존재하지 않는 것과 같다        → 검사 [6]
    갱신 규칙 6  폴더 전체 행수 합이 줄지 않아야 한다                 → 검사 [7]
                 (이동과 삭제를 구분하는 유일한 검증법)

사용법
------
    python3 Tools/check_docs.py

    # 특정 폴더만 검사하고 싶을 때
    python3 Tools/check_docs.py --root Assets/_Project/Docs

    # 에이전트 메모리 폴더 위치를 바꾸고 싶을 때 (기본: .claude/agent-memory)
    python3 Tools/check_docs.py --memory-root .claude/agent-memory

    # 검사 [7] 의 기준값을 현재 상태로 갱신 (사람이 명시적으로 실행. 아래 주의 참조)
    python3 Tools/check_docs.py --update-baseline

    # 감소가 하나라도 포함된 갱신은 사유를 반드시 함께 준다 (없으면 거부된다)
    python3 Tools/check_docs.py --update-baseline --reason "중복 절 제거, 사용자 승인 완료"

종료 코드: 문제가 없으면 0, 하나라도 있으면 1.
           `--update-baseline` 은 갱신에 성공하면 0, 사유 없이 감소를 반영하려다 거부되면 2.

⚠️ 기준값은 자동으로 갱신되지 않는다
------------------------------------
검사 [7] 은 `.claude/agent-memory/_baseline.json` 에 적힌 이전 행수와 지금을 비교한다.
**이 도구는 그 파일에 절대 쓰지 않는다.** `--update-baseline` 을 붙였을 때만 쓴다.

자동 갱신을 허용하면 **감소가 그대로 새 기준이 되어**,
사고 직후 도구를 한 번 돌리는 것만으로 사고가 지워지기 때문이다.
"줄었다"는 판단은 사람이 확인하고 사람이 반영한다.

⚠️ 감소를 반영하려면 `--reason` 이 필요하다 (2026-08-24 추가)
-------------------------------------------------------------
기준값 파일의 `_갱신하는_법` 은 **감소를 반영할 때 사유를 `change_log` 에 남기라**고 요구한다.
그런데 종전의 `--update-baseline` 은 "남겨라"라고 **출력만 하고 그냥 갱신했다.**
안 남겨도 아무 일도 일어나지 않으니, 실제로 2026-08-24 의 qa-tester -2행 반영 때
`change_log` 항목은 사람이 손으로 적어야 했다. 다음 사람은 안 적을 것이고,
그러면 감소가 사유 없이 조용히 새 기준이 된다 — 이 도구가 막으려던 바로 그 상태다.

그래서 규칙을 "지키자"에서 "기계가 잡는다"로 옮겼다:

    감소가 하나라도 포함  +  --reason 없음   →  🔴 거부. 기준값 파일을 건드리지 않고 끝낸다.
    감소가 하나라도 포함  +  --reason 있음   →  갱신 + change_log 에 항목 자동 추가
    증가만 있음                              →  --reason 없이 그대로 통과 (승인 불필요)

증가만 있을 때 `--reason` 을 요구하지 않는 근거는 기준값 파일 `_갱신하는_법` 이다 —
"증가 방향(정상적인 성장)은 자유롭게 갱신해도 된다". 승인이 필요 없는 방향이므로
`change_log` 항목도 남기지 않는다(항목이 불어나면 정작 중요한 감소 기록이 묻힌다).
증가만인데도 굳이 기록을 남기고 싶으면 `--reason` 을 붙이면 된다 — 그때는 기록된다.

⚠️ 기준값이 낡으면 감소폭이 실제보다 작게 보인다 (2026-08-24 추가)
-------------------------------------------------------------------
검사 [7] 은 **감소만** 봤다. 기준값이 실제보다 **작을 때**(= 증가가 반영되지 않아 낡았을 때)는
아무 말도 하지 않았고, 그래서 드리프트가 소리 없이 쌓였다.

    2026-08-24 실측: 실제 삭제는 **-6행**이었는데 [7] 은 **-2행**으로 보고했다.
                     기준값이 2026-08-21 의 756 에 머물러 그 사이의 증가(→760)가
                     반영돼 있지 않았기 때문이다.

낡은 기준값은 검사를 죽이지는 않지만 **눈금을 어긋나게 한다.** 그래서 [7] 은 이제
`실제 > 기준값` 인 폴더를 발견하면 「기준값이 낡음」을 함께 알린다.

🔴 다만 그것은 **문제로 집계하지 않고 종료 코드에도 넣지 않는다.** 증가는 정상적인 성장이라
   집계하면 문서 작업 때마다 종료 코드가 1 이 되어 `WORKFLOW.md` [11]③ 의
   "0건 확인" 절차가 막힌다. 보이기만 하면 사람이 갱신하므로 안내로 충분하다.

검사하지 않는 것
----------------
`_Tasks/` 와 `_Logs/` 는 **작성 시점의 상태를 남긴 이력 기록**이라 검사 대상에서 뺀다.
지금 기준으로 맞지 않는다고 소급 수정하면 이력이 왜곡된다.
이 원칙은 경로와 무관하게 유효하므로 **검사 [6]·[7] 의 파일 수집에도 그대로 적용**한다
(현재 `.claude/agent-memory/` 아래에는 그런 폴더가 없어 실제로 걸러지는 파일은 0개다).

한계 (중요)
-----------
"번호는 유효한데 **엉뚱한 규칙**을 가리키는 경우"는 잡지 못한다.
예를 들어 `Units 규칙 36` 이라 적었는데 실제로 인용한 내용이 규칙 37 이라면,
36 번이 실재하므로 이 검사는 통과한다. 그건 내용을 읽어야 알 수 있다.
→ 참조에 `규칙 37(HoT 힐 텍스트 집계)` 처럼 괄호로 내용을 병기해 두면
   검사 [5] 가 제목 키워드를 대조해 잡아낼 수 있다.
"""

import argparse
import datetime
import json
import os
import re
import sys
import glob

# ─────────────────────────────────────────────────────────────
# 설정
# ─────────────────────────────────────────────────────────────

# 이력 기록이라 검사에서 제외할 경로 조각
EXCLUDE_PARTS = ("/_Tasks/", "/_Logs/", "\\_Tasks\\", "\\_Logs\\")

# 에이전트 메모리 폴더의 기본 위치 (리포지토리 루트 기준 상대경로).
# 리포지토리 루트 밖의 파일을 도구가 직접 아는 것은 새로운 방식이 아니다 —
# collect_files() 도 AGENTS.md / CLAUDE.md 를 같은 방식으로 이미 알고 있다.
DEFAULT_MEMORY_ROOT = ".claude/agent-memory"

# 검사 [7] 의 기준값 파일 이름. 메모리 폴더 바로 아래에 둔다.
# .md 가 아니라 .json 인 이유: "메모리 정리" 작업의 시야에 안 들어와서 같이 지워질 확률이 낮고,
# collect_files() 의 *.md 수집에도 걸리지 않아 기존 검사 [2] 를 건드리지 않는다.
BASELINE_FILENAME = "_baseline.json"

# 각 에이전트 폴더의 인덱스 파일 이름. 이 파일만 "토픽이 아닌 목차"로 취급한다.
MEMORY_INDEX_NAME = "MEMORY.md"

# 규칙 정의 줄:  **규칙 14. 제목**
RE_RULE_DEF = re.compile(r"^\*\*규칙\s*(\d+)[.\s]\s*(.*?)\*?\*?$", re.M)

# H2 섹션 헤딩 (### 는 제외)
RE_H2 = re.compile(r"^##\s(?!#)\s*(.+?)\s*$")

# 마크다운 파일 링크
RE_LINK = re.compile(r"\[[^\]]*\]\(([^)#]+\.md)(#[^)]*)?\)")

# 한 줄 안의 문서명 언급 / 규칙 번호 언급
RE_DOC_MENTION = re.compile(r"GameSystemRules_\w+\.md")
RE_RULE_MENTION = re.compile(r"규칙\s*(\d+)(?:\s*[~-]\s*(\d+))?\s*(?:[(（]([^)）]{2,40})[)）])?")

# 규칙 번호가 문서명에서 이만큼 떨어져 있으면 그 문서를 가리킨다고 보지 않는다.
REF_MAX_DISTANCE = 80


def extract_refs(line):
    """
    한 줄에서 (문서명, 시작번호, 끝번호, 병기내용) 목록을 뽑는다.

    한 줄에 문서가 여러 개 언급될 수 있으므로(예: "Buildings 규칙 2 … UI 규칙 5"),
    각 규칙 번호는 **가장 가까운 앞쪽 문서명**에 붙인다.
    정규식 하나로 훑으면 이 짝짓기가 어긋나 오탐이 난다.
    """
    docs_at = [(m.start(), m.group(0)) for m in RE_DOC_MENTION.finditer(line)]
    if not docs_at:
        return []

    refs = []
    for m in RE_RULE_MENTION.finditer(line):
        prior = [(pos, name) for pos, name in docs_at if pos < m.start()]
        if not prior:
            continue
        pos, name = prior[-1]                      # 가장 가까운 앞쪽 문서
        if m.start() - pos > REF_MAX_DISTANCE:     # 너무 멀면 무관한 언급으로 본다
            continue
        lo = int(m.group(1))
        hi = int(m.group(2)) if m.group(2) else lo
        refs.append((name, lo, hi, m.group(3)))
    return refs


def is_excluded(path):
    p = path.replace(os.sep, "/")
    return any(part.replace("\\", "/") in p for part in EXCLUDE_PARTS)


def collect_files(root):
    """검사 대상 마크다운 파일 목록. 이력 폴더는 뺀다."""
    found = [p for p in glob.glob(os.path.join(root, "**", "*.md"), recursive=True)
             if not is_excluded(p)]
    # 리포지토리 루트의 지침 문서도 참조를 담고 있어 함께 본다.
    for extra in ("AGENTS.md", "CLAUDE.md"):
        if os.path.exists(extra):
            found.append(extra)
    return sorted(set(found))


def parse_rule_docs(root):
    """
    규칙 문서를 훑어 {파일명: 정보} 를 만든다.

    정보:
      max          그 문서에 존재하는 가장 큰 규칙 번호
      per_section  True 면 섹션마다 번호가 1부터 반복된다(= 번호만으로는 특정 불가)
      sections     [(섹션명, 최소번호, 최대번호)]
      titles       {번호: [제목, ...]}   번호가 반복되면 제목이 여러 개다
      bodies       {번호: [본문, ...]}   제목 다음 줄부터 다음 규칙 헤딩 직전까지
    """
    docs = {}
    pattern = os.path.join(root, "GameSystemRules", "*.md")
    for path in sorted(glob.glob(pattern)):
        text = open(path, encoding="utf-8").read()
        nums, titles, bodies, sections = [], {}, {}, []
        section, bucket = None, []
        cur_num, cur_body = None, []

        def flush_body():
            # 진행 중이던 규칙의 본문을 저장한다.
            if cur_num is not None:
                bodies.setdefault(cur_num, []).append("\n".join(cur_body))

        for line in text.splitlines():
            h2 = RE_H2.match(line)
            if h2:
                flush_body()
                cur_num, cur_body = None, []
                if section and bucket:
                    sections.append((section, min(bucket), max(bucket)))
                section, bucket = h2.group(1), []
                continue
            m = re.match(r"^\*\*규칙\s*(\d+)[.\s]\s*(.*)", line)
            if m:
                flush_body()
                n = int(m.group(1))
                nums.append(n)
                titles.setdefault(n, []).append(m.group(2).rstrip("*").strip())
                cur_num, cur_body = n, []
                if section is not None:
                    bucket.append(n)
                continue
            if cur_num is not None:
                cur_body.append(line)
        flush_body()
        if section and bucket:
            sections.append((section, min(bucket), max(bucket)))

        if nums:
            docs[os.path.basename(path)] = {
                "path": path,
                "max": max(nums),
                "per_section": len(nums) != len(set(nums)),
                "sections": sections,
                "titles": titles,
                "bodies": bodies,
            }
    return docs


# ─────────────────────────────────────────────────────────────
# 에이전트 메모리 검사용 공용 함수 (검사 [6]·[7] 이 함께 쓴다)
# ─────────────────────────────────────────────────────────────

def count_lines(path):
    """
    파일의 행수를 센다.

    `splitlines()` 를 쓰는 이유: 마지막 줄에 개행 문자가 없어도 1행으로 세기 위해서다.
    (`wc -l` 은 개행 개수를 세므로 이런 파일을 1행 적게 센다. 기준값 파일도 이 기준으로 적혀 있다.)
    """
    with open(path, encoding="utf-8") as f:
        return len(f.read().splitlines())


def collect_memory_files(memory_root):
    """
    에이전트 메모리 파일 목록을 만든다.

    🔴 검사 [6] 과 [7] 은 **반드시 이 함수 하나만** 써서 목록을 만든다.
       서로 다른 기준으로 파일을 모으면 "고아는 아닌데 총합에는 안 잡히는 파일" 같은
       모순이 생기고, 총합이 어긋나 [7] 에 오탐이 난다.

    반환값:
        {에이전트폴더명: {"dir": 폴더경로,
                          "files": [(폴더기준 상대경로, 실제경로, 행수), ...]}}

    수집 규칙:
      - `<memory_root>/<에이전트>/` 아래의 모든 `.md` (하위 폴더까지 재귀)
      - `is_excluded()` 로 `_Tasks/`·`_Logs/` 는 제외 (파일 상단 「검사하지 않는 것」 참조)
      - `memory_root` 바로 아래에 있는 파일(예: _baseline.json)은 어느 에이전트에도 속하지 않으므로 무시
    """
    result = {}
    for entry in sorted(os.listdir(memory_root)):
        agent_dir = os.path.join(memory_root, entry)
        if not os.path.isdir(agent_dir):
            continue

        files = []
        pattern = os.path.join(agent_dir, "**", "*.md")
        for path in sorted(glob.glob(pattern, recursive=True)):
            if is_excluded(path):
                continue
            rel = os.path.relpath(path, agent_dir).replace(os.sep, "/")
            files.append((rel, path, count_lines(path)))

        if files:
            result[entry] = {"dir": agent_dir, "files": files}
    return result


def load_baseline(memory_root):
    """
    검사 [7] 의 기준값 파일을 읽는다.

    반환값: (데이터 dict, 오류메시지 str)
            읽는 데 성공하면 오류메시지가 None, 실패하면 데이터가 None.

    🔴 파일이 없을 때 "통과"시키지 않고 오류를 돌려주는 것이 중요하다.
       기준값 파일이 소실되면 검사 [7] 이 조용히 죽어 버리는데,
       그러면 "검사가 있으니 안심"이라는 잘못된 믿음만 남는다.
       호출부에서 이 오류를 문제 1건으로 집계한다.
    """
    path = os.path.join(memory_root, BASELINE_FILENAME)
    if not os.path.exists(path):
        return None, f"기준값 파일이 없다: {path}"
    try:
        with open(path, encoding="utf-8") as f:
            return json.load(f), None
    except (OSError, ValueError) as e:
        return None, f"기준값 파일을 읽을 수 없다: {path} ({e})"


def check_orphan_topics(memory, baseline):
    """
    ── 검사 [6] 고아 토픽 파일 ────────────────────────────────

    무엇을 잡는가:
        에이전트 폴더 안에 있지만 **그 폴더의 MEMORY.md 어디에서도 링크되지 않은** 토픽 파일.

    왜 잡는가:
        `.claude/MEMORY.md` 갱신 규칙 5 — **"링크 없는 토픽 파일은 존재하지 않는 것과 같다."**
        에이전트는 인덱스(MEMORY.md)를 보고 토픽을 찾아간다.
        인덱스에서 링크가 빠지면 파일은 디스크에 멀쩡히 남아 있는데 아무도 열지 않는다.
        내용이 지워진 것과 결과가 같으면서, 파일이 남아 있어 **더 발견하기 어렵다.**

    기존 검사 [2] 와의 관계 — 방향만 뒤집은 것이다:
        [2] 링크가 가리키는 파일이 없는가   (링크 → 파일)
        [6] 아무도 가리키지 않는 파일이 있는가 (파일 → 링크)
        그래서 같은 RE_LINK 를 그대로 재사용한다. 새 정규식이 필요 없다.

    링크로 인정하는 범위 — **마크다운 링크(`[표시](파일.md)`)만 인정한다.**
        본문 안의 백틱 언급(`` `network-infra.md` ``)은 링크가 아니다.
        준거 규칙 원문이 "반드시 인덱스에서 링크한다"(37행)이고,
        엄격한 기준을 써도 현재 정상 토픽 20개가 전부 통과해 오탐이 0이기 때문이다.
        백틱까지 인정하면 질문이 "인덱스에서 찾아갈 수 있는가"에서
        "어딘가에 이름이 적혀 있는가"로 바뀌어 규칙의 취지가 무너진다.

    반환값: 종료 코드에 반영할 문제 건수
    """
    print()
    print("=" * 68)
    print("[6] 인덱스에서 링크되지 않은 에이전트 메모리 토픽 파일")
    print("=" * 68)
    print("  대상: 각 에이전트 폴더의 MEMORY.md 가 아닌 모든 .md")
    print("  판정: 같은 폴더 MEMORY.md 본문에 마크다운 링크로 등장하지 않으면 고아")
    print("  근거: .claude/MEMORY.md 갱신 규칙 5 — 링크 없는 토픽 파일은 존재하지 않는 것과 같다")
    print()

    # 기준값 파일의 known_orphans 에 등록된 것은 "이미 알고 있는 미해소 항목"이라
    # 출력에는 내되 종료 코드에는 반영하지 않는다. (파일이 없으면 예외도 없는 셈)
    known = set()
    if baseline:
        for item in baseline.get("known_orphans", []):
            known.add(str(item.get("path", "")).replace("\\", "/"))

    issues = 0
    found = False
    known_found = False

    for agent, info in memory.items():
        index_path = os.path.join(info["dir"], MEMORY_INDEX_NAME)

        # 인덱스 자체가 없는 폴더는 "무엇에 링크돼야 하는지"를 물을 대상이 없다.
        # 토픽만 덩그러니 있는 상태이므로 그 사실을 알리고 넘어간다.
        if not os.path.exists(index_path):
            topics = [f for f in info["files"] if f[0] != MEMORY_INDEX_NAME]
            if topics:
                print(f"  {info['dir']}: {MEMORY_INDEX_NAME} 가 없는데 토픽 {len(topics)}개가 있다")
                found = True
                issues += 1
            continue

        # 인덱스 본문에서 링크 대상을 전부 뽑아 둔다.
        # 폴더 기준 상대경로로 정규화해 하위 폴더 토픽(`[x](sub/foo.md)`)도 맞출 수 있게 한다.
        with open(index_path, encoding="utf-8") as f:
            index_text = f.read()
        linked = {os.path.normpath(m.group(1)).replace(os.sep, "/")
                  for m in RE_LINK.finditer(index_text)}

        for rel, path, lines in info["files"]:
            if rel == MEMORY_INDEX_NAME:
                continue
            if rel in linked:
                continue

            key = f"{agent}/{rel}"
            # 819행짜리가 끊긴 것과 6행짜리가 끊긴 것은 무게가 전혀 다르다.
            # 그래서 행수를 반드시 함께 낸다 — 사람이 우선순위를 판단할 수 있어야 한다.
            if key in known:
                print(f"  [알려진 예외] {path} ({lines}행)")
                print(f"      → {agent}/{MEMORY_INDEX_NAME} 에서 링크되지 않음"
                      f" (기준값 파일 known_orphans 에 등록됨 — 종료 코드에 미반영)")
                known_found = True
            else:
                print(f"  {path} ({lines}행)")
                print(f"      → {agent}/{MEMORY_INDEX_NAME} 에서 링크되지 않음")
                found = True
                issues += 1

    if not found and not known_found:
        print("  이상 없음")
    elif not found:
        print()
        print("  새로 생긴 고아 없음 (위 항목은 알려진 예외).")

    if found:
        print()
        print("  조치: 해당 에이전트의 MEMORY.md 인덱스에 `[파일명](파일명)` 링크를 추가한다.")
    return issues


def check_folder_line_totals(memory, baseline, baseline_error):
    """
    ── 검사 [7] 에이전트 폴더 총합 행수 급감 ──────────────────

    무엇을 잡는가:
        에이전트 폴더의 **파일 수 / 총행수**를 기준값과 대조해, 총행수가 줄었으면 보고한다.

    왜 잡는가 — 이것이 이동과 삭제를 구분하는 유일한 방법이다:
        `.claude/MEMORY.md` 갱신 규칙 6 — "토픽으로 옮길 때는 폴더 전체 행수 합이 줄지 않아야 한다
        — **이동과 삭제를 구분하는 유일한 검증법**".
        내용을 다른 파일로 옮긴 것이라면 총합은 그대로여야 한다. 총합이 줄었다면 옮긴 게 아니라 지워진 것이다.

        실측 사고 (2026-08-17):
            game-programmer 폴더 파일 수 19 → 19 **그대로**, 총합 2,503 → 2,125 = **정확히 -378행**.
            파일 수만 보면 아무 일도 없었다. 총합을 봐야만 보인다.
            → 3일 동안 아무도 몰랐다.
        그래서 **파일 수와 총행수를 둘 다** 기록하고 둘 다 본다.

    🔴 임계값이 0인 이유 (= 감소를 전부 보고한다):
        2026-08-20 사고는 폴더 총합 기준 **-5행**이었다.
        "-5% 이상" 이든 "-30행 이상" 이든, 어떤 임계값을 두더라도 이 사고는 놓친다.
        그런데 이건 가정이 아니라 **실제로 일어난 사고**다.
        임계값을 두는 순간 이 검사는 "큰 사고만 잡는" 도구가 된다.

        오탐(정상적인 중복 제거 등) 비용은 낮다 — 사람이 확인하고 `--update-baseline` 하면 끝난다.
        오히려 그 절차가 갱신 규칙 3("삭제는 틀렸다고 확인했을 때만, 지운 이유를 남긴다")을
        기계적으로 강제하는 효과가 있다. 감소가 검사에 걸리므로 이유 없이 지나갈 수 없다.

    ── 함께 알리는 것: 「기준값이 낡음」 (2026-08-24 추가) ──────────
        위 감소 판정은 기준값을 자로 쓴다. 그런데 그 자가 낡으면 눈금이 어긋난다.

        실측 (2026-08-24):
            실제 삭제는 **-6행**이었는데 이 검사는 **-2행**으로 보고했다.
            기준값이 2026-08-21 의 756 에 머물러 그 사이의 증가(→760)가 반영되지 않아
            "760에서 754로 -6" 이 아니라 "756에서 754로 -2" 로 계산됐기 때문이다.
            드리프트가 쌓일수록 이후 감소폭은 실제보다 계속 작게 보인다.

        그래서 `실제 > 기준값` 인 폴더를 발견하면 얼마나 차이 나는지 알린다.

        🔴 이것은 **문제로 집계하지 않고 반환값(종료 코드)에도 넣지 않는다.**
           증가는 정상적인 성장이다. 집계해 버리면 메모리에 한 줄만 보태도 종료 코드가 1 이 되고,
           그러면 `WORKFLOW.md` [11]③ 의 "0건 확인" 절차가 메모리와 무관한 문서 작업까지 전부 막는다.
           보이기만 하면 사람이 갱신하므로 안내로 충분하다.

        🔴 출력에서 감소와 **명확히 구분**한다. 감소는 사고일 수 있는 🔴 문제이고,
           증가 미반영은 눈금 정비 안내다. 한 덩어리로 섞어 내면 감소의 심각성이 희석된다.

    반환값: 종료 코드에 반영할 문제 건수 (「기준값이 낡음」 안내는 여기 들어가지 않는다)
    """
    print()
    print("=" * 68)
    print("[7] 에이전트 메모리 폴더의 총합 행수 감소")
    print("=" * 68)
    print("  기준값과 대조해 총행수가 1행이라도 줄었으면 보고한다(임계값 0).")
    print("  근거: .claude/MEMORY.md 갱신 규칙 6 — 폴더 전체 행수 합이 줄지 않아야 한다")
    print()

    # 기준값 파일이 없거나 깨졌으면 조용히 통과시키지 않는다.
    # 검사가 죽었는데 "이상 없음"이 찍히면 아무도 모른 채 보호막이 사라진다.
    if baseline is None:
        print(f"  {baseline_error}")
        print("  → 기준값이 없으면 '전보다 줄었는가'를 판정할 수 없다. 문제 1건으로 집계한다.")
        print("  조치: python3 Tools/check_docs.py --update-baseline")
        return 1

    recorded = baseline.get("folders", {})
    issues = 0
    found = False

    # 「기준값이 낡음」 안내용으로 따로 모은다.
    # 🔴 감소 목록과 절대 섞지 않는다 — 섞으면 사고(감소)가 성장(증가) 사이에 묻힌다.
    #    한 줄에 (에이전트, 기준행수, 실제행수, 기준파일수, 실제파일수) 를 담는다.
    stale = []

    # (1) 기준값에 있는 폴더가 지금 어떻게 됐는지 본다.
    for agent in sorted(recorded):
        want = recorded[agent]
        want_files = int(want.get("files", 0))
        want_lines = int(want.get("lines", 0))

        info = memory.get(agent)
        if info is None:
            # 폴더가 통째로 사라진 경우. 가장 큰 손실이므로 반드시 잡는다.
            print(f"  {agent}: 폴더가 사라졌다"
                  f" (기준값 {want_files}개 파일 / {want_lines}행)")
            found = True
            issues += 1
            continue

        now_files = len(info["files"])
        now_lines = sum(lines for _, _, lines in info["files"])

        if now_lines > want_lines:
            # 증가 = 정상적인 성장이라 문제가 아니다. 다만 기준값이 그만큼 낡았다는 뜻이므로
            # 나중에 안내로 따로 낸다 (issues 에는 넣지 않는다).
            stale.append((agent, want_lines, now_lines, want_files, now_files))
            continue
        if now_lines == want_lines:
            continue  # 유지 — 정상

        delta_lines = now_lines - want_lines            # 항상 음수
        delta_files = now_files - want_files

        # 숫자만 던지면 사람이 판단을 못 한다. 어떤 패턴인지 해석까지 붙인다.
        if delta_files == 0:
            reading = "파일 수 그대로 + 총합 감소 → 이동이 아니라 삭제다 (2026-08-17 사고와 같은 모양)"
        elif delta_files > 0:
            reading = "파일 수 증가 + 총합 감소 → 토픽으로 분산하다가 일부를 붙여넣지 못한 것으로 보인다"
        else:
            reading = "파일 수 감소 + 총합 감소 → 토픽 파일이 삭제됐다"

        print(f"  {agent}: {want_lines}행 → {now_lines}행 ({delta_lines:+}행)"
              f" / 파일 {want_files}개 → {now_files}개 ({delta_files:+})")
        print(f"      {reading}")
        found = True
        issues += 1

    # (2) 기준값에 없는 새 폴더. 조용히 건너뛰면 새 에이전트가 통째로 보호 밖에 놓인다.
    for agent in sorted(memory):
        if agent in recorded:
            continue
        info = memory[agent]
        now_files = len(info["files"])
        now_lines = sum(lines for _, _, lines in info["files"])
        print(f"  {agent}: 기준값 미등록 (현재 {now_files}개 파일 / {now_lines}행)")
        print("      → 이 폴더는 아직 감소 감시를 받지 못한다")
        found = True
        issues += 1

    if not found:
        print("  이상 없음")
    else:
        print()
        print("  조치: 감소가 의도된 것인지 사람이 먼저 확인한다.")
        print("        의도된 것이라면 사용자 승인을 받고 사유를 붙여 갱신한다:")
        print('          python3 Tools/check_docs.py --update-baseline --reason "왜 줄었는지"')
        print("        🔴 감소를 기준값에 반영하는 것은 '이 삭제는 의도된 것'이라는 판단이다.")
        print("           확인 없이 갱신하면 사고가 그대로 새 기준이 된다.")
        print("           그래서 감소를 포함한 갱신은 --reason 없이는 거부되고,")
        print("           준 사유는 기준값 파일 change_log 에 자동으로 기록된다.")

    # ── 안내: 기준값이 낡음 ────────────────────────────────────
    # 🔴 위 감소 목록과 시각적으로 확실히 갈라 놓는다. 여기 있는 것은 사고가 아니라 눈금 정비다.
    #    issues 를 건드리지 않으므로 종료 코드에도 영향이 없다.
    if stale:
        total_lines = sum(now - want for _, want, now, _, _ in stale)
        print()
        print("  ── 안내: 기준값이 낡음 (문제 아님 · 종료 코드 미반영) ──────────")
        for agent, want_lines, now_lines, want_files, now_files in stale:
            print(f"  {agent}: 기준값 {want_lines}행 ↔ 실제 {now_lines}행"
                  f" ({now_lines - want_lines:+}행)"
                  f" / 파일 {want_files}개 ↔ {now_files}개 ({now_files - want_files:+})")
        print(f"  합 {total_lines:+}행 — 증가분이 기준값에 반영되지 않았다.")
        print()
        print("  왜 그냥 두면 안 되나: 기준값은 감소를 재는 자다. 자가 낡으면 눈금이 어긋난다.")
        print("    2026-08-24 실측 — 실제 삭제 -6행이 이 검사에는 -2행으로 보였다.")
        print("    (기준값이 2026-08-21 의 756 에 멈춰 그 사이의 증가 →760 이 빠져 있었다.)")
        print("  조치: python3 Tools/check_docs.py --update-baseline")
        print("        증가만이면 --reason 없이 통과한다(승인이 필요 없는 방향).")
    return issues


def diff_baseline_folders(old_folders, new_folders):
    """
    기준값 갱신 전후를 비교해 **승인이 필요한 것과 필요 없는 것**으로 갈라 놓는다.

    왜 따로 함수로 빼는가:
        같은 분류를 세 군데에서 쓰기 때문이다 —
        ① 화면 출력, ② `--reason` 을 요구할지 말지 판정, ③ change_log 에 적을 내용.
        세 곳이 각자 계산하면 "출력엔 감소라고 찍혔는데 거부는 안 된다" 같은 어긋남이 생긴다.

    판정 기준은 **행수**다 — 검사 [7] 과 같은 잣대를 써야 도구 안에서 말이 엇갈리지 않는다.

    반환값: {"decreased": [...], "increased": [...], "added": [...], "removed": [...]}
        각 값은 사람이 그대로 읽을 수 있는 한 줄 문자열의 목록이다.

        decreased  총행수가 줄었다              → 🔴 승인 필요 (사고일 수 있다)
        removed    폴더가 통째로 사라졌다        → 🔴 승인 필요 (가장 큰 손실이라 감소로 친다)
        increased  총행수가 늘었다              → 정상적인 성장, 승인 불필요
        added      기준값에 없던 새 폴더        → 감시 대상에 새로 들어오는 것뿐, 승인 불필요

        ⚠️ 행수는 그대로인데 파일 수만 바뀐 경우는 increased 에 넣는다.
           총합이 보존됐다는 것은 "옮기기만 했다"는 뜻이라 갱신 규칙 6 을 만족하기 때문이다.
    """
    decreased, increased, added, removed = [], [], [], []

    for agent in sorted(set(old_folders) | set(new_folders)):
        before = old_folders.get(agent)
        after = new_folders.get(agent)

        if before is None:
            added.append(f"{agent}: 신규 등록 → 파일 {after['files']}개 / {after['lines']}행")
            continue
        if after is None:
            removed.append(f"{agent}: 폴더가 통째로 사라짐"
                           f" (기준값 파일 {before.get('files')}개 / {before.get('lines')}행 → 0)")
            continue

        before_lines = int(before.get("lines", 0))
        before_files = int(before.get("files", 0))
        d_lines = after["lines"] - before_lines
        d_files = after["files"] - before_files
        if d_lines == 0 and d_files == 0:
            continue  # 바뀐 것이 없다

        line = (f"{agent}: {before_lines}행 → {after['lines']}행 ({d_lines:+}행)"
                f" / 파일 {before_files}개 → {after['files']}개 ({d_files:+})")
        (decreased if d_lines < 0 else increased).append(line)

    return {"decreased": decreased, "increased": increased,
            "added": added, "removed": removed}


def update_baseline(memory_root, memory, reason=None, today=None):
    """
    `--update-baseline` 로만 호출된다. 기준값 파일을 현재 상태로 다시 쓴다.

    🔴 기본 실행에서는 절대 호출하지 않는다.
       도구가 실행될 때마다 자동으로 현재값을 기준값으로 덮어쓰면,
       **감소가 그대로 새 기준이 되어** 다음 실행부터는 아무 문제도 아니게 된다.
       즉 사고 직후 도구를 한 번 돌리는 것만으로 사고가 지워진다.

    ── `--reason` 을 요구하는 이유 (2026-08-24 추가) ────────────────
    기준값 파일 `_갱신하는_법` 은 감소를 반영할 때 사유를 `change_log` 에 남기라고 요구한다.
    그런데 종전 구현은 그 문구를 **출력만 하고 그냥 갱신했다.** 즉 규칙은 "지키자" 쪽에 있고
    검사는 없었다. 실제로 2026-08-24 의 qa-tester -2행 반영 때 `change_log` 항목은
    사람이 손으로 적어야 했고, 다음 사람이 안 적으면 감소가 사유 없이 새 기준이 된다.

    그래서 이 함수는 이제 이렇게 동작한다:

        감소 있음 + reason 없음  →  🔴 거부. **파일을 열지도 않고** 그대로 끝낸다(종료 코드 2).
        감소 있음 + reason 있음  →  갱신 + change_log 에 항목 자동 추가
        증가만                   →  reason 없이 통과. change_log 항목은 남기지 않는다.

    증가만일 때 기록을 남기지 않는 근거는 기준값 파일 `_갱신하는_법` 이다 —
    "증가 방향(정상적인 성장)은 자유롭게 갱신해도 된다". 승인이 필요 없는 방향인데다,
    검사 [7] 이 「기준값이 낡음」을 알리게 된 뒤로는 증가 반영이 잦아진다.
    잦은 기록으로 목록이 불어나면 정작 중요한 감소 기록이 그 사이에 묻힌다.
    그래도 남기고 싶으면 `--reason` 을 붙이면 된다 — 그때는 증가만이어도 기록한다.

    인자:
        reason  사용자가 준 사유 문자열. None 이면 "사유 없음".
        today   change_log 에 적을 날짜(YYYY-MM-DD). None 이면 오늘 날짜.
                테스트에서 날짜를 고정하려고 열어 둔 인자다.

    반환값: 갱신했으면 0, 사유 없이 감소를 반영하려다 거부했으면 2.

    무엇이 어떻게 바뀌는지 전부 출력한다 — 사람이 눈으로 확인하고 승인할 수 있어야 한다.
    known_orphans 와 밑줄 접두 설명 키 등 사람이 손으로 적은 내용은 건드리지 않고 그대로 옮긴다.
    """
    path = os.path.join(memory_root, BASELINE_FILENAME)
    old, _ = load_baseline(memory_root)
    old_folders = (old or {}).get("folders", {})

    new_folders = {}
    for agent in sorted(memory):
        info = memory[agent]
        new_folders[agent] = {
            "files": len(info["files"]),
            "lines": sum(lines for _, _, lines in info["files"]),
        }

    diff = diff_baseline_folders(old_folders, new_folders)
    # 폴더가 통째로 사라진 것은 가장 큰 손실이므로 감소와 똑같이 승인 대상으로 묶는다.
    losses = diff["removed"] + diff["decreased"]
    gains = diff["added"] + diff["increased"]

    print("=" * 68)
    print("기준값 갱신 (--update-baseline)")
    print("=" * 68)
    print(f"  대상 파일: {path}")
    print()

    if losses:
        print("  🔴 감소 (사용자 승인이 필요한 방향)")
        for line in losses:
            print(f"    - {line}")
        print()
    if gains:
        print("  증가 / 신규 (승인 불필요)")
        for line in gains:
            print(f"    + {line}")
        print()
    if not losses and not gains:
        print("  바뀐 값이 없다. 기준값은 이미 현재 상태와 같다.")
        print()

    # ── 🔴 거부 지점: 감소가 있는데 사유가 없으면 파일을 건드리지 않고 끝낸다 ──
    # 여기서 return 하기 전에는 절대 파일을 열지 않는다. "거부했는데 반쯤 써 놨다" 가 되면
    # 사고가 오히려 커진다.
    if losses and not reason:
        print("  🔴 거부 — 감소가 포함된 갱신인데 사유(--reason)가 없다.")
        print("     기준값 파일을 건드리지 않고 그대로 끝낸다.")
        print()
        print("     감소를 기준값에 반영하는 것은 '이 삭제는 의도된 것'이라는 판단이다.")
        print("     사유 없이 반영하면 사고가 그대로 새 기준이 되어 다음 실행부터는")
        print("     아무 문제도 아니게 된다. 그래서 사유를 필수로 받는다.")
        print("     (준거: .claude/MEMORY.md 「에이전트 메모리 갱신 규칙」 3번 —")
        print("      삭제는 틀렸다고 확인했을 때만 하고 지운 이유를 함께 남긴다)")
        print()
        print("  조치: ① 위 감소가 의도된 것인지 사람이 확인하고 사용자 승인을 받는다.")
        print("        ② 승인받았으면 사유를 붙여 다시 실행한다:")
        print('           python3 Tools/check_docs.py --update-baseline --reason "왜 줄었는지"')
        print("        준 사유는 기준값 파일 change_log 에 자동으로 기록된다.")
        print("=" * 68)
        return 2

    # ── 여기부터 실제 쓰기 ──
    # 사람이 적어 둔 항목(밑줄 접두 설명 키·known_orphans·기존 change_log)을 보존한 채
    # folders 만 교체한다. dict(old) 로 통째로 복사하므로 모르는 키도 자동으로 살아남는다.
    data = dict(old) if old else {}
    data["folders"] = new_folders

    # 사유가 주어졌으면 change_log 에 항목을 자동으로 추가한다.
    # 사람이 손으로 적던 것을 도구가 적게 만드는 것이 이 변경의 핵심이다 —
    # "남겨라"라고 출력만 하면 안 남기는 사람이 반드시 나온다.
    if reason:
        entry = {
            "date": today or datetime.date.today().isoformat(),
            "note": ("Tools/check_docs.py --update-baseline --reason 으로 자동 기록. "
                     f"감소 {len(losses)}건 · 증가/신규 {len(gains)}건."),
        }
        if losses:
            entry["🔴 감소"] = list(losses)
        if gains:
            entry["증가"] = list(gains)
        entry["사유"] = reason
        data.setdefault("change_log", []).append(entry)

    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")

    print("  기준값 파일을 갱신했다.")
    if reason:
        print(f"  change_log 에 항목을 추가했다 (사유: {reason})")
    elif gains:
        print("  증가만 있어 사유 없이 반영했다 — change_log 항목은 남기지 않는다.")
        print("  (기록을 남기고 싶으면 --reason 을 붙여 다시 실행하면 된다.)")
    print("=" * 68)
    return 0


def main():
    ap = argparse.ArgumentParser(description="Hexiege 문서 정합성 검사")
    ap.add_argument("--root", default="Assets/_Project/Docs",
                    help="검사할 문서 루트 (기본: Assets/_Project/Docs)")
    # 🔴 --root 를 재활용하지 않고 별도 인자를 신설한 이유:
    #    parse_rule_docs() 는 root 아래에 GameSystemRules/ 폴더가 있다고 전제한다.
    #    --root 를 .claude/agent-memory 로 돌리면 그 폴더가 없어 docs 가 빈 딕셔너리가 되고,
    #    docs 에 의존하는 검사 [1]·[3]·[4]·[5] 가 전부 **조용히 "이상 없음"** 을 낸다.
    #    즉 기존 검사를 무력화하지 않고는 --root 를 돌려 쓸 수 없다.
    ap.add_argument("--memory-root", default=DEFAULT_MEMORY_ROOT,
                    help=f"에이전트 메모리 루트 (기본: {DEFAULT_MEMORY_ROOT})")
    ap.add_argument("--update-baseline", action="store_true",
                    help="검사 [7] 의 기준값을 현재 상태로 갱신한다. "
                         "이 플래그를 붙였을 때만 파일에 쓴다 — 기본 실행은 읽기 전용이다.")
    # 🔴 --reason 을 선택 인자로 두되 '감소가 있으면 필수'로 만든 이유:
    #    증가만 있는 갱신까지 사유를 요구하면, 기준값을 최신으로 유지하는 일 자체가 번거로워져
    #    사람들이 갱신을 미루게 된다. 그러면 검사 [7] 의 눈금이 낡아 감소폭이 작게 보인다
    #    (2026-08-24 실측: 실제 -6행이 -2행으로 보였다). 즉 마찰은 감소에만 걸어야 한다.
    ap.add_argument("--reason", default=None,
                    help="--update-baseline 과 함께 쓴다. 감소가 하나라도 포함된 갱신은 "
                         "이 사유 없이는 거부된다. 준 사유는 기준값 파일 change_log 에 "
                         "자동으로 기록된다. 증가만 있는 갱신에는 필요 없다.")
    args = ap.parse_args()

    # --reason 만 주고 --update-baseline 을 빠뜨리면 아무 일도 일어나지 않는다.
    # 조용히 무시하면 "사유를 남겼다"고 착각한 채 지나가므로 여기서 확실히 막는다.
    if args.reason and not args.update_baseline:
        print("[오류] --reason 은 --update-baseline 과 함께 써야 한다.")
        print("       기본 실행은 읽기 전용이라 사유를 적을 곳이 없다.")
        return 2

    # ── --update-baseline: 검사를 돌리지 않고 기준값만 갱신하고 끝낸다 ──
    # 검사와 갱신을 한 번에 하면 "갱신했으니 당연히 0건"이 찍혀 결과가 무의미해진다.
    if args.update_baseline:
        if not os.path.isdir(args.memory_root):
            print(f"[오류] 에이전트 메모리 폴더를 찾을 수 없다: {args.memory_root}")
            return 2
        return update_baseline(args.memory_root,
                               collect_memory_files(args.memory_root),
                               reason=args.reason)

    if not os.path.isdir(args.root):
        print(f"[오류] 문서 폴더를 찾을 수 없다: {args.root}")
        print("       리포지토리 루트에서 실행해야 한다.")
        return 2

    files = collect_files(args.root)
    docs = parse_rule_docs(args.root)
    issues = 0

    # ── [1] 규칙 번호 결번 ────────────────────────────────────
    # 섹션마다 1부터 다시 매기는 문서는 "중복"이 정상이므로 결번만 본다.
    print("=" * 68)
    print("[1] 규칙 번호 결번")
    print("=" * 68)
    found = False
    for name, info in docs.items():
        if info["per_section"]:
            # 섹션별로 각각 연속인지 확인
            for sec, lo, hi in info["sections"]:
                present = {n for n in info["titles"] if lo <= n <= hi}
                gaps = [n for n in range(lo, hi + 1) if n not in present]
                if gaps:
                    print(f"  {name} / {sec}: 결번 {gaps}")
                    found = True
                    issues += 1
        else:
            present = set(info["titles"])
            gaps = [n for n in range(1, info["max"] + 1) if n not in present]
            if gaps:
                print(f"  {name}: 결번 {gaps}")
                found = True
                issues += 1
    if not found:
        print("  이상 없음")

    # ── [2] 깨진 파일 링크 ────────────────────────────────────
    print()
    print("=" * 68)
    print("[2] 존재하지 않는 파일을 가리키는 링크")
    print("=" * 68)
    found = False
    for path in files:
        for i, line in enumerate(open(path, encoding="utf-8"), 1):
            for m in RE_LINK.finditer(line):
                target = os.path.normpath(os.path.join(os.path.dirname(path), m.group(1)))
                if not os.path.exists(target):
                    print(f"  {path}:{i} → {m.group(1)}")
                    found = True
                    issues += 1
    if not found:
        print("  이상 없음")

    # ── [3] 규칙 번호 범위 초과 참조 ──────────────────────────
    print()
    print("=" * 68)
    print("[3] 실재하지 않는 규칙 번호를 가리키는 참조")
    print("=" * 68)
    found = False
    for path in files:
        for i, line in enumerate(open(path, encoding="utf-8"), 1):
            for doc, lo, hi, _ in extract_refs(line):
                if doc in docs and max(lo, hi) > docs[doc]["max"]:
                    rng = f"{lo}~{hi}" if hi != lo else f"{lo}"
                    print(f"  {path}:{i} → {doc} 규칙 {rng}"
                          f"  (실제 최대 {docs[doc]['max']})")
                    found = True
                    issues += 1
    if not found:
        print("  이상 없음")

    # ── [4] 섹션명 없는 모호한 참조 ───────────────────────────
    print()
    print("=" * 68)
    print("[4] 섹션명이 없어 어느 규칙인지 특정 불가한 참조")
    print("=" * 68)
    print("  대상: 섹션마다 규칙 번호가 1부터 반복되는 문서")
    for name, info in docs.items():
        if info["per_section"]:
            secs = ", ".join(f"{s}({lo}~{hi})" for s, lo, hi in info["sections"])
            print(f"    - {name}: {secs}")
    print()
    found = False
    for path in files:
        for i, line in enumerate(open(path, encoding="utf-8"), 1):
            for doc, lo, hi, _ in extract_refs(line):
                if doc not in docs or not docs[doc]["per_section"]:
                    continue
                # 참조 앞뒤에 섹션명이 적혀 있으면 통과로 본다.
                if any(sec in line for sec, _, _ in docs[doc]["sections"]):
                    continue
                rng = f"{lo}~{hi}" if hi != lo else f"{lo}"
                print(f"  {path}:{i} → {doc} 규칙 {rng}")
                found = True
                issues += 1
    if not found:
        print("  이상 없음")

    # ── [5] 병기된 내용과 실제 규칙 제목 불일치 ───────────────
    print()
    print("=" * 68)
    print("[5] 괄호로 병기된 내용이 실제 규칙 제목과 겹치지 않는 참조")
    print("=" * 68)
    print("  참조에 `규칙 37(HoT 힐 텍스트 집계)` 처럼 내용을 병기한 경우만 검사한다.")
    print("  ※ 낱말 겹침으로 판단하므로 오탐이 있을 수 있다 — 사람이 확인할 것.")
    print()
    found = False
    for path in files:
        for i, line in enumerate(open(path, encoding="utf-8"), 1):
            for doc, n, hi, label in extract_refs(line):
                if label is None or hi != n:
                    continue
                if doc not in docs or n not in docs[doc]["titles"]:
                    continue
                # 라벨의 낱말이 규칙의 제목 **또는 본문** 어딘가에 하나라도 나타나면 통과.
                # 제목만 보면 오탐이 난다 — 규칙 제목은 짧고 세부 내용은 본문에 있기 때문이다.
                words = [w for w in re.split(r"[\s·,/()+]+", label) if len(w) >= 2]
                if not words:
                    continue
                haystack = docs[doc]["titles"][n] + docs[doc]["bodies"].get(n, [])
                if any(any(w in h for h in haystack) for w in words):
                    continue
                print(f"  {path}:{i} → {doc} 규칙 {n}")
                print(f"      병기된 내용 : {label}")
                for t in docs[doc]["titles"][n]:
                    print(f"      실제 제목   : {t}")
                found = True
                issues += 1
    if not found:
        print("  이상 없음")

    # ── [6]·[7] 에이전트 메모리 무결성 ────────────────────────
    # 메모리 폴더가 없어도 문서 검사 전체를 중단시키지는 않는다.
    # 기존 5종은 메모리 폴더와 무관하므로, 없다고 문서 검사를 못 돌게 만들면 그 자체가 회귀다.
    if os.path.isdir(args.memory_root):
        memory = collect_memory_files(args.memory_root)
        baseline, baseline_error = load_baseline(args.memory_root)
        issues += check_orphan_topics(memory, baseline)
        issues += check_folder_line_totals(memory, baseline, baseline_error)
    else:
        print()
        print("=" * 68)
        print("[6]·[7] 에이전트 메모리 무결성")
        print("=" * 68)
        print(f"  건너뜀 — 메모리 폴더가 없다: {args.memory_root}")

    # ── 요약 ──────────────────────────────────────────────────
    print()
    print("=" * 68)
    if issues:
        print(f"총 {issues}건의 문제를 찾았다.")
        print()
        print("[4] 가 잡힌 경우: 참조에 섹션명을 함께 적으면 된다.")
        print('  예)  "Buildings 규칙 14"  →  "Buildings 방어 타워 규칙 14"')
    else:
        print("문제 없음.")
    print("=" * 68)
    return 1 if issues else 0


if __name__ == "__main__":
    sys.exit(main())
