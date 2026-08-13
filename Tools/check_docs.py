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

사용법
------
    python3 Tools/check_docs.py

    # 특정 폴더만 검사하고 싶을 때
    python3 Tools/check_docs.py --root Assets/_Project/Docs

종료 코드: 문제가 없으면 0, 하나라도 있으면 1.

검사하지 않는 것
----------------
`_Tasks/` 와 `_Logs/` 는 **작성 시점의 상태를 남긴 이력 기록**이라 검사 대상에서 뺀다.
지금 기준으로 맞지 않는다고 소급 수정하면 이력이 왜곡된다.

한계 (중요)
-----------
"번호는 유효한데 **엉뚱한 규칙**을 가리키는 경우"는 잡지 못한다.
예를 들어 `Units 규칙 36` 이라 적었는데 실제로 인용한 내용이 규칙 37 이라면,
36 번이 실재하므로 이 검사는 통과한다. 그건 내용을 읽어야 알 수 있다.
→ 참조에 `규칙 37(HoT 힐 텍스트 집계)` 처럼 괄호로 내용을 병기해 두면
   검사 [5] 가 제목 키워드를 대조해 잡아낼 수 있다.
"""

import argparse
import os
import re
import sys
import glob

# ─────────────────────────────────────────────────────────────
# 설정
# ─────────────────────────────────────────────────────────────

# 이력 기록이라 검사에서 제외할 경로 조각
EXCLUDE_PARTS = ("/_Tasks/", "/_Logs/", "\\_Tasks\\", "\\_Logs\\")

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


def main():
    ap = argparse.ArgumentParser(description="Hexiege 문서 정합성 검사")
    ap.add_argument("--root", default="Assets/_Project/Docs",
                    help="검사할 문서 루트 (기본: Assets/_Project/Docs)")
    args = ap.parse_args()

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
