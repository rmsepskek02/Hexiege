"""Checker regression fixtures use temporary repositories; real memory is never changed."""
import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from collections import Counter
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location("check_docs", Path(__file__).parents[1] / "check_docs.py")
checker = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = checker
SPEC.loader.exec_module(checker)


class CheckerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.docs = self.root / "Docs"
        (self.docs / "GameSystemRules").mkdir(parents=True)

    def write(self, relative, text):
        path = self.docs / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path

    def rule(self, suffix, body):
        return self.write(f"GameSystemRules/GameSystemRules_{suffix}.md", body)

    def inspect(self):
        files = [str(p) for p in self.docs.rglob("*.md") if not checker.is_excluded(str(p))]
        return checker.inspect_documents(str(self.docs), files)

    def messages(self):
        return "\n".join(p[3] for p in self.inspect()[1])

    def test_full_identifiers_and_real_header_formats(self):
        self.rule("A", "## A\n### 1. 일반 하위 제목\n**규칙 1. 하나**\n**규칙 1-A. 확장**\n"
                  "**1-1. 소조항**\n**1-1-a. 세부조항**\n**규칙 M-1. 화면**\n"
                  "**규칙 D-1. 이탈**\n**규칙 L-1. 로딩**\n### 규칙 R1 — 반응\n"
                  "### NET-AUTH-001. 권위\n### U-MOV-PHASE. 이동\n")
        docs, problems, _ = self.inspect()
        self.assertEqual([r.identifier for r in docs["GameSystemRules_A.md"]["rules"]],
                         ["1", "1-A", "1-1", "1-1-a", "M-1", "D-1", "L-1", "R1", "NET-AUTH-001", "U-MOV-PHASE"])
        self.assertFalse(problems)

    def test_exact_missing_and_unknown_document(self):
        self.rule("A", "**규칙 1. 하나**\n**규칙 3. 셋**")
        self.write("Refs.md", "GameSystemRules_A.md 규칙 0·2·999\nGameSystemRules_Missing.md 규칙 1")
        messages = self.messages()
        for expected in ("없는 규칙 0", "없는 규칙 2", "없는 규칙 999", "알 수 없는 규칙 문서", "결번 [2]"):
            self.assertIn(expected, messages)

    def test_section_gaps_cannot_be_hidden_by_other_section(self):
        self.rule("A", "## A\n**규칙 1. 하나**\n**규칙 3. 셋**\n## B\n**규칙 1. 하나**\n**규칙 2. 둘**\n**규칙 3. 셋**")
        self.write("Refs.md", "GameSystemRules_A.md A 규칙 2; B 규칙 2")
        messages = self.messages()
        self.assertIn("A: 결번 [2]", messages)
        self.assertIn("없는 규칙 2 / A", messages)
        self.assertNotIn("없는 규칙 2 / B", messages)

    def test_two_documents_and_sections_on_one_line(self):
        self.rule("A", "## A\n**규칙 1. 하나**\n## B\n**규칙 1. 둘**\n**규칙 2. 둘**")
        self.rule("B", "**규칙 1. 하나**")
        self.write("Refs.md", "GameSystemRules_A.md A 규칙 2; GameSystemRules_B.md 규칙 9")
        self.assertIn("없는 규칙 2 / A", self.messages())
        self.assertIn("없는 규칙 9", self.messages())

    def test_missing_section_is_ambiguous_and_unknown_section_fails(self):
        self.rule("A", "## A\n**규칙 1. 하나**\n## B\n**규칙 1. 둘**")
        self.write("Refs.md", "GameSystemRules_A.md 규칙 1\nGameSystemRules_A.md 「Missing」 규칙 1")
        self.assertIn("모호한 규칙 1", self.messages())
        self.assertIn("알 수 없는 섹션 Missing", self.messages())

    def test_section_name_ending_in_rule_and_inherited_clause(self):
        self.rule("A", "## 공통 UI 규칙\n**규칙 1. 하나**\n**규칙 2. 둘**\n## B\n**규칙 1. 하나**")
        self.write("Refs.md", "GameSystemRules_A.md 공통 UI 규칙 1, 규칙 2")
        self.assertFalse(self.inspect()[1])

    def test_identifier_lists_ranges_and_subnumber(self):
        self.assertEqual(checker.expand_identifiers("11-1·12~14")[0], ["11-1", "12", "13", "14"])
        self.assertEqual(checker.expand_identifiers("M-1~3")[0], ["M-1", "M-2", "M-3"])
        self.assertEqual(checker.expand_identifiers("NET-AUTH-001/002")[0], ["NET-AUTH-001", "NET-AUTH-002"])
        self.assertTrue(checker.expand_identifiers("3~1")[2])
        self.assertTrue(checker.expand_identifiers("3~")[2])
        self.assertTrue(checker.expand_identifiers("NET-A-001~U-A-002")[2])

    def test_forwarding_partial_number_set_and_broken_target(self):
        self.rule("A", "**규칙 1. 하나**\n원본 본문\n**규칙 2. 둘**\n본문 원본: [GameSystemRules_B.md](GameSystemRules_B.md). 기존 위치 안내.\n### 빈 호환 제목")
        self.rule("B", "**규칙 2. 둘**\n원본 본문\n**규칙 1. 하나**\n본문 원본: [GameSystemRules_A.md](GameSystemRules_A.md). 기존 위치 안내.")
        self.assertFalse(self.inspect()[1])
        self.rule("B", "**규칙 1. 하나**")
        self.assertIn("동일 ID", self.messages())

    def test_forwarding_does_not_hide_actual_body(self):
        self.rule("A", "**규칙 1. 하나**\n본문 원본: [Missing.md](Missing.md)\n본문1\n본문2\n본문3")
        docs, _, _ = self.inspect()
        self.assertFalse(docs["GameSystemRules_A.md"]["rules"][0].forwarding)

    def test_empty_and_unsupported_definition_are_not_success(self):
        self.assertIn("범위가 비어", self.messages())
        self.rule("A", "## 규칙 Q-1. 형식 누락\n규칙 본문")
        self.assertIn("미지원 규칙 정의", self.messages())
        self.assertIn("해석된 정의 없음", self.messages())

    def test_code_examples_are_not_definitions_or_references(self):
        self.rule("A", "**규칙 1. 하나**\n```md\n**규칙 999. 예시**\nGameSystemRules_A.md 규칙 999\n[x](missing.md)\n```\n<!-- GameSystemRules_A.md 규칙 999 -->")
        self.assertFalse(self.inspect()[1])

    def test_named_identifiers_without_document_and_unknown_id(self):
        self.rule("A", "### NET-AUTH-001. 권위\n### NET-AUTH-002. 권위\n### U-MOV-PHASE. 이동")
        self.write("Refs.md", "`NET-AUTH-001/002`, `U-MOV-PHASE`, `NET-MISSING-001`")
        self.assertIn("없는 명명 ID NET-MISSING-001", self.messages())

    def test_named_identifier_explicit_document_is_not_global_lookup(self):
        self.rule("A", "**규칙 1. 하나**")
        self.rule("B", "### NET-AUTH-001. 권위")
        self.write("Refs.md", "GameSystemRules_A.md NET-AUTH-001")
        self.assertIn("GameSystemRules_A.md: 없는 규칙 NET-AUTH-001", self.messages())

    def test_named_identifiers_after_general_prose_do_not_bind_previous_filename(self):
        self.rule("A", "**규칙 1. 하나**\n### U-MOV-PHASE. 이동")
        self.rule("B", "### NET-PRESENT-001. 권위")
        self.write("Refs.md", "GameSystemRules_A.md의 `U-MOV-PHASE`와 동기화 문서의 `NET-PRESENT-001`\nGameSystemRules_A.md 규칙 1인 반면, 최신 `NET-PRESENT-001`은 별도다.")
        self.assertFalse(self.inspect()[1])

    def test_previous_label_section_cannot_exempt_next_reference(self):
        self.rule("A", "## A section\n**규칙 1. 하나**\n**규칙 2. 둘**\n## B section\n**규칙 1. 둘**\n**규칙 2. 둘**")
        self.write("Refs.md", "GameSystemRules_A.md 규칙 1(B section), 규칙 2")
        # B in the first label can select rule 1, but cannot select rule 2.
        self.assertIn("모호한 규칙 2", self.messages())

    def test_forwarded_label_uses_original_body(self):
        self.rule("A", "**규칙 1. 하나**\n본문 원본: [GameSystemRules_B.md](GameSystemRules_B.md). 안내.")
        self.rule("B", "**규칙 1. 원본**\n원본에만있는설명")
        self.write("Refs.md", "GameSystemRules_A.md 규칙 1(원본에만있는설명)")
        self.assertFalse(self.inspect()[1])

    def test_historical_annotation_is_one_line_exact_reference_correction(self):
        self.rule("A", "## A\n**규칙 1. 하나**\n## B\n**규칙 1. 둘**")
        self.write("History.md", "<!-- docs-check: historical-section GameSystemRules_A.md | 1 | A -->\nGameSystemRules_A.md 규칙 1")
        self.assertFalse(self.inspect()[1])
        self.assertEqual(self.inspect()[2]["이력보정"], 1)
        self.write("History.md", "<!-- docs-check: historical-section GameSystemRules_A.md | 1 | A -->\n\nGameSystemRules_A.md 규칙 1")
        self.assertIn("보정 미적용", self.messages())

    def test_historical_annotation_cannot_hide_missing_id_or_section(self):
        self.rule("A", "## A\n**규칙 1. 하나**")
        self.write("History.md", "<!-- docs-check: historical-section GameSystemRules_A.md | 9 | A -->\nGameSystemRules_A.md 규칙 9")
        self.assertIn("없는 규칙 9 / A", self.messages())
        self.write("History.md", "<!-- docs-check: historical-section GameSystemRules_A.md | 1 | Missing -->\nGameSystemRules_A.md 규칙 1")
        self.assertIn("원본 섹션 없음", self.messages())

    def test_annotation_examples_in_fences_and_inline_prose_are_not_active(self):
        self.rule("A", "**규칙 1. 하나**")
        self.write("Examples.md", "```md\n<!-- docs-check: historical-section bad marker -->\n```\n형식 예: `<!-- docs-check: historical-section bad marker -->`")
        self.assertFalse(self.inspect()[1])

    def test_unknown_quoted_section_is_not_hidden_by_reference_label(self):
        self.rule("A", "## A\n**규칙 1. 첫째**")
        self.write("Refs.md", "GameSystemRules_A.md 「없는 섹션」 규칙 1(첫째)")
        self.assertIn("알 수 없는 섹션 없는 섹션", self.messages())

    def test_unknown_quoted_section_overrides_inherited_valid_section(self):
        self.rule("A", "## A\n**규칙 1. 첫째**\n## B\n**규칙 1. 둘째**")
        self.write("Refs.md", "GameSystemRules_A.md 「A」 규칙 1, 「없는 섹션」 규칙 1(첫째)")
        self.assertIn("알 수 없는 섹션 없는 섹션", self.messages())

    def test_plain_definition_candidate_and_prose_disambiguation(self):
        self.rule("A", "**규칙 1. 하나**\n규칙 2: 누락 제목\n규칙 1의 서술은 본문이다.")
        problems = self.inspect()[1]
        self.assertEqual(len(problems), 1)
        self.assertEqual(problems[0][0], "형식")

    def test_anchor_unicode_duplicate_html_and_encoded_space(self):
        self.rule("A", "**규칙 1. 하나**")
        target = self.write("공백 파일.md", "# 🔴 한글 제목!\n## 반복\n## 반복\n<a id=\"explicit\"></a>\n")
        self.write("Refs.md", "[a](<공백 파일.md#-한글-제목>)\n[b](%EA%B3%B5%EB%B0%B1%20%ED%8C%8C%EC%9D%BC.md#반복-1)\n[c](<공백 파일.md#explicit>)\n[d](https://example.com/missing.md#bad)")
        self.assertFalse(self.inspect()[1])
        self.assertEqual(checker.heading_slug("🔴️ 한글 제목!"), "-한글-제목")

    def test_missing_same_file_and_cross_file_anchors(self):
        self.rule("A", "**규칙 1. 하나**")
        self.write("Refs.md", "# 있음\n[a](#없음)\n[b](GameSystemRules/GameSystemRules_A.md#없음)\n[c](missing.md)")
        problems = self.inspect()[1]
        self.assertEqual(Counter(p[0] for p in problems), {"앵커": 2, "링크": 1})

    def test_exclusion_for_history_and_external_claude_docs(self):
        self.rule("A", "**규칙 1. 하나**")
        self.write("_Tasks/task.md", "[x](missing.md)")
        self.write("_Logs/log.md", "[x](missing.md)")
        claude = self.root / ".claude"
        for folder in ("skills", "plugins", "worktrees", "agents"):
            path = claude / folder / "test.md"
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("text", encoding="utf-8")
        files = checker.collect_files(str(self.docs), str(claude))
        self.assertEqual(checker.collect_claude_files(str(claude)), [str(claude / "agents/test.md")])
        self.assertFalse(any("/_Tasks/" in p.replace("\\", "/") or "/_Logs/" in p.replace("\\", "/") for p in files))

    def memory(self):
        memory = self.root / "memory"
        agent = memory / "agent"
        agent.mkdir(parents=True)
        (agent / "MEMORY.md").write_text("[topic](topic.md)\n", encoding="utf-8")
        (agent / "topic.md").write_text("one\ntwo\n", encoding="utf-8")
        baseline = {"folders": {"agent": {"lines": 4, "files": 2}}, "known_orphans": [], "_note": "keep", "change_log": [{"note": "old"}]}
        (memory / "_baseline.json").write_text(json.dumps(baseline), encoding="utf-8")
        return memory

    def test_memory_orphans_loss_and_missing_baseline(self):
        memory = self.memory()
        (memory / "agent/orphan.md").write_text("x", encoding="utf-8")
        folders = checker.collect_memory_files(str(memory))
        baseline, error = checker.load_baseline(str(memory))
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(checker.check_orphan_topics(folders, baseline), 1)
            # Remove fixture orphan from count, not from real filesystem.
            folders["agent"]["files"] = [f for f in folders["agent"]["files"] if f[0] != "orphan.md"]
            self.assertEqual(checker.check_folder_line_totals(folders, baseline, error), 1)
        (memory / "_baseline.json").unlink()
        self.assertIsNotNone(checker.load_baseline(str(memory))[1])

    def test_default_cli_is_read_only_and_baseline_reason_guard(self):
        self.rule("A", "**규칙 1. 하나**")
        memory = self.memory()
        files = [p for p in self.root.rglob("*") if p.is_file()]
        before = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
        args = ["checker", "--root", str(self.docs), "--memory-root", str(memory), "--no-claude-docs"]
        with patch.object(sys, "argv", args), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(checker.main(), 1)  # genuine one-line memory loss
        self.assertEqual(before, {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in files})
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(checker.update_baseline(str(memory), checker.collect_memory_files(str(memory))), 2)
        self.assertEqual(before[str(memory / "_baseline.json")], hashlib.sha256((memory / "_baseline.json").read_bytes()).hexdigest())
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(checker.update_baseline(str(memory), checker.collect_memory_files(str(memory)), reason="fixture decrease approved", today="2026-10-13"), 0)
        data = json.loads((memory / "_baseline.json").read_text(encoding="utf-8"))
        self.assertEqual(data["_note"], "keep")
        self.assertEqual(len(data["change_log"]), 2)


if __name__ == "__main__":
    unittest.main()
