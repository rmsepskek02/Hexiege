using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Hexiege.Application;
using Hexiege.Infrastructure;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hexiege.Editor.Combat
{
    /// <summary>
    /// Game 씬의 서버 전투 모드 하나만 변경한다. 실행 중인 경기나 저장하지 않은 씬은
    /// 건드리지 않으며, 저장 결과에 다른 변경이 섞이면 원본 bytes로 복구한다.
    /// </summary>
    public static class SetupResultPresentationMode
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string Field = "_serverCombatPipelineMode";
        private const int Expected = (int)CombatPipelineMode.ResultPresentation;
        private const string Menu = "Hexiege/Combat/Result Presentation Mode/";
        private const int MaximumReportedDiffLines = 4;
        private const int MaximumReportedLineCharacters = 160;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly Regex ModeLine = new Regex(
            @"(?m)^([ \t]*_serverCombatPipelineMode: )[0-9]+(\r?)$", RegexOptions.CultureInvariant);
        private static readonly Regex ModeLineWithTerminator = new Regex(
            @"(?m)^[ \t]*_serverCombatPipelineMode: [0-9]+(?:\r?\n|\z)", RegexOptions.CultureInvariant);
        private static readonly Regex SensitiveLine = new Regex(
            @"(?i)(password|passwd|secret|token|api[ _-]?key|authorization|credential)",
            RegexOptions.CultureInvariant);

        [MenuItem(Menu + "Dry Run")]
        public static void DryRun() => Inspect(false, false);

        [MenuItem(Menu + "Validate")]
        public static void Validate() => Inspect(false, true);

        [MenuItem(Menu + "Apply")]
        public static void Apply() => Inspect(true, true);

        [MenuItem(Menu + "Self Validate")]
        public static void SelfValidate()
        {
            foreach (var mode in new[] { CombatPipelineMode.Legacy, CombatPipelineMode.PresentationShadow,
                         CombatPipelineMode.ResultPresentation })
            {
                Require(IsValidTarget(1, ScenePath, (int)mode), "Known current mode must be eligible.");
                Require(((int)mode != Expected) == (mode != CombatPipelineMode.ResultPresentation),
                    "Only ResultPresentation is already a NO-OP.");
            }
            Require(!IsValidTarget(0, ScenePath, 1) && !IsValidTarget(2, ScenePath, 1),
                "Missing or ambiguous targets must fail closed.");
            Require(!IsValidTarget(1, "Assets/Other.unity", 1) && !IsValidTarget(1, ScenePath, 99),
                "Wrong scene and unknown current mode must fail closed.");
            string original = "header\n  _serverCombatPipelineMode: 1\n  other: 7\n";
            string intended = "header\n  _serverCombatPipelineMode: 2\n  other: 7\n";
            Require(OnlyModeChanged(original, intended), "Only the target scalar may change.");
            Require(!OnlyModeChanged(original, intended.Replace("other: 7", "other: 8")),
                "A non-target serialized change must trigger rollback.");

            // 이 필드가 추가되기 전에 저장된 Game 씬에는 YAML 줄이 없지만 SerializedObject는
            // C# 기본값(PresentationShadow)을 정상적으로 돌려준다. Unity가 처음 저장할 때 정확히
            // 한 줄만 추가하는 경우를 허용해야 실제 마이그레이션이 가능하다.
            string absentLf = "header\n  _serverMovementPipelineMode: 1\n  other: 7\n";
            string insertedLf = "header\n  _serverMovementPipelineMode: 1\n  _serverCombatPipelineMode: 2\n  other: 7\n";
            Require(OnlyModeChanged(absentLf, insertedLf),
                "An absent serialized default must allow one LF mode-line insertion.");
            Require(!OnlyModeChanged(absentLf, insertedLf.Replace("other: 7", "other: 8")),
                "An absent-mode migration must reject unrelated LF changes.");

            string absentCrLf = absentLf.Replace("\n", "\r\n");
            string insertedCrLf = insertedLf.Replace("\n", "\r\n");
            Require(OnlyModeChanged(absentCrLf, insertedCrLf),
                "An absent serialized default must allow one CRLF mode-line insertion.");
            Require(!OnlyModeChanged(absentCrLf, insertedCrLf + "unexpected: 1\r\n"),
                "An absent-mode migration must reject unrelated CRLF changes.");
            Require(!OnlyModeChanged(absentLf, absentLf),
                "A migration must persist exactly one explicit mode line.");
            Require(!OnlyModeChanged(absentLf, insertedLf.Replace(
                    "  other: 7", "  _serverCombatPipelineMode: 2\n  other: 7")),
                "A migration must reject duplicate inserted mode lines.");
            Require(OnlyModeBytesChanged(
                    StrictUtf8.GetBytes(absentCrLf), StrictUtf8.GetBytes(insertedCrLf)),
                "The byte gate must allow the exact CRLF insertion.");
            Require(!OnlyModeBytesChanged(
                    StrictUtf8.GetBytes(absentCrLf), AddUtf8Preamble(StrictUtf8.GetBytes(insertedCrLf))),
                "The byte gate must reject a BOM change outside the inserted line.");

            // Unity SaveScene가 Windows 원본의 CRLF를 전부 LF로 정규화하더라도 그 저장본은
            // 검증 자료로만 사용한다. 의미 내용이 목표 모드 한 줄 외에 완전히 같을 때에만
            // 실행 전 bytes에 같은 위치의 모드 줄을 CRLF로 삽입해 원본 표현을 보존한다.
            byte[] crLfBaseline = StrictUtf8.GetBytes(absentCrLf);
            byte[] lfUnitySave = StrictUtf8.GetBytes(insertedLf);
            Require(TryBuildAbsentModeInsertion(crLfBaseline, lfUnitySave,
                    out byte[] crLfPreserved, out string insertionFailure),
                "A newline-normalizing Unity save must produce a safe insertion: " + insertionFailure);
            Require(EqualBytes(crLfPreserved, StrictUtf8.GetBytes(insertedCrLf)),
                "The safe insertion must preserve every CRLF baseline byte and add only the mode line.");

            byte[] bomCrLfBaseline = AddUtf8Preamble(crLfBaseline);
            byte[] bomExpected = AddUtf8Preamble(StrictUtf8.GetBytes(insertedCrLf));
            Require(TryBuildAbsentModeInsertion(bomCrLfBaseline, lfUnitySave,
                    out byte[] bomPreserved, out insertionFailure)
                    && EqualBytes(bomPreserved, bomExpected),
                "The safe insertion must preserve an existing UTF-8 BOM exactly.");
            Require(!TryBuildAbsentModeInsertion(crLfBaseline,
                    StrictUtf8.GetBytes(insertedLf.Replace("other: 7", "other: 8")),
                    out _, out insertionFailure),
                "The safe insertion must reject any semantic non-target change.");

            // Apply가 fail-closed로 멈췄을 때 씬 전체를 노출하지 않고도 Unity 저장기가 무엇을
            // 함께 바꿨는지 확인할 수 있어야 한다. 모드 줄은 비교에서 제외하고 앞 네 건만
            // 보여 주며, 자격 증명처럼 보이는 줄은 내용 자체를 출력하지 않는다.
            string diagnosticBefore = "header\r\n  _serverCombatPipelineMode: 1\r\n"
                + "  safeA: before\r\n  apiToken: super-secret-token\r\n"
                + "  safeC: before\r\n  safeD: before\r\n  safeE: before\r\n";
            string diagnosticAfter = "header\n  _serverCombatPipelineMode: 2\n"
                + "  safeA: after\n  apiToken: changed-secret-token\n"
                + "  safeC: after\n  safeD: after\n  safeE: after\n";
            string boundedDiagnostic = FormatBoundedLineDiffDiagnostic(
                StrictUtf8.GetBytes(diagnosticBefore), StrictUtf8.GetBytes(diagnosticAfter));
            Require(boundedDiagnostic.Contains("baselineBytes=")
                    && boundedDiagnostic.Contains("savedBytes=")
                    && boundedDiagnostic.Contains("baselineCRLF=7")
                    && boundedDiagnostic.Contains("savedLF=7"),
                "The bounded diagnostic must report byte lengths and line-ending counts.");
            Require(boundedDiagnostic.Contains("diff1=") && boundedDiagnostic.Contains("diff4=")
                    && !boundedDiagnostic.Contains("diff5="),
                "The bounded diagnostic must report no more than four differing lines.");
            Require(boundedDiagnostic.Contains("<redacted-sensitive-line>")
                    && !boundedDiagnostic.Contains("super-secret-token")
                    && !boundedDiagnostic.Contains("changed-secret-token")
                    && !boundedDiagnostic.Contains(Field)
                    && boundedDiagnostic.Length < 1400,
                "The bounded diagnostic must exclude the mode line, secrets and unbounded output.");
            Debug.Log("[UAS-PRESENT-MODE][self-validation][PASS] known transitions, exact scene/single target, explicit 1-to-1 replacement, absent-default LF/CRLF 0-to-1 insertion and non-target rollback gate.");
        }

        private static void Inspect(bool apply, bool requireExpected)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling,
                "Stop Play Mode and wait for compilation before inspecting the scene mode.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            Scene previousActive = SceneManager.GetActiveScene();
            if (!openedHere) Require(!scene.isDirty, "Game scene has unsaved changes. Save or resolve them first.");
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var targets = new List<NetworkGameFlow>();
                foreach (var root in scene.GetRootGameObjects())
                    targets.AddRange(root.GetComponentsInChildren<NetworkGameFlow>(true));
                Require(targets.Count == 1, "Expected exactly one NetworkGameFlow in Game scene; found=" + targets.Count);
                var serialized = new SerializedObject(targets[0]);
                var property = serialized.FindProperty(Field);
                Require(property != null && property.propertyType == SerializedPropertyType.Enum,
                    "Serialized combat mode field is missing or changed type.");
                int current = property.intValue;
                Require(IsValidTarget(targets.Count, scene.path, current), "Unsupported target scene or existing combat mode.");
                string details = "scene=" + scene.path + ", target=" + targets[0].name
                    + ", current=" + (CombatPipelineMode)current + ", expected=" + (CombatPipelineMode)Expected;
                if (current == Expected)
                {
                    Debug.Log("[UAS-PRESENT-MODE][NO-OP] " + details + ", saves=0");
                    return;
                }
                if (!apply)
                {
                    Require(!requireExpected, "Scene is not ready: " + details + ". Run Apply.");
                    Debug.Log("[UAS-PRESENT-MODE][dry-run][CHANGE] " + details + ", saves=0");
                    return;
                }

                byte[] baseline = File.ReadAllBytes(ScenePath);
                string metaPath = ScenePath + ".meta";
                byte[] metaBaseline = File.ReadAllBytes(metaPath);
                string original = File.ReadAllText(ScenePath);
                int originalModeLineCount = ModeLine.Matches(original).Count;
                Require(originalModeLineCount <= 1,
                    "Scene bytes contain ambiguous serialized combat mode lines; count=" + originalModeLineCount);
                Require(!scene.isDirty, "Opening the scene produced unsaved changes; refuse a mixed save.");
                try
                {
                    property.intValue = Expected;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(scene);
                    Require(EditorSceneManager.SaveScene(scene, ScenePath, false), "SaveScene returned false.");
                    byte[] savedBytes = File.ReadAllBytes(ScenePath);
                    if (originalModeLineCount == 0)
                    {
                        // 새 필드가 없던 씬은 Unity가 저장 과정에서 줄바꿈까지 전부 바꿀 수 있다.
                        // 먼저 Unity 저장본의 의미 내용이 모드 한 줄 외에 동일한지 확인한 다음,
                        // 검증된 위치에 그 한 줄만 실행 전 bytes 표현(CRLF/BOM 포함)으로 삽입한다.
                        Require(TryBuildAbsentModeInsertion(baseline, savedBytes,
                                out byte[] preservedBytes, out string insertionFailure),
                            "Unity save could not be reduced to one safe combat-mode insertion; reason="
                            + insertionFailure + ". "
                            + FormatBoundedLineDiffDiagnostic(baseline, savedBytes));
                        File.WriteAllBytes(ScenePath, preservedBytes);
                        savedBytes = File.ReadAllBytes(ScenePath);
                        Require(EqualBytes(preservedBytes, savedBytes),
                            "CRLF/BOM-preserving scene write failed exact-byte verification.");
                    }

                    string saved = StrictUtf8.GetString(savedBytes);
                    Require(OnlyModeBytesChanged(baseline, savedBytes),
                        "Save changed bytes outside the one permitted combat-mode line. "
                        + FormatBoundedLineDiffDiagnostic(baseline, savedBytes));
                    Require(EqualBytes(metaBaseline, File.ReadAllBytes(metaPath)), "Scene meta changed unexpectedly.");
                    serialized.Update();
                    Require(serialized.FindProperty(Field).intValue == Expected, "Saved object mode failed verification.");
                    Require(ModeLine.Match(saved).Value.Trim() == Field + ": " + Expected,
                        "Persisted scene mode failed verification.");
                    Debug.Log("[UAS-PRESENT-MODE][apply][PASS] " + details + ", saves=1, nonTargetBytes=unchanged");
                }
                catch (Exception failure)
                {
                    // 저장 도중 실패해도 사용자의 기존 씬/메타와 메모리상의 모드를 함께 복구한다.
                    // Unity 6에는 장면의 dirty 플래그만 지우는 공개 API가 없다. 따라서 먼저 메모리상의
                    // enum 값을 원래대로 되돌린 장면을 저장해 dirty 상태를 정상적으로 해제한 다음,
                    // 그 저장 과정에서 직렬화 순서 등이 달라질 가능성까지 없애기 위해 실행 전 bytes를
                    // 마지막에 다시 기록한다. 이렇게 해야 열린 장면은 깨끗한 상태를 유지하면서도
                    // 디스크의 scene/.meta는 실행 전과 정확히 같은 bytes로 돌아간다.
                    property.intValue = current;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(scene);
                    Require(EditorSceneManager.SaveScene(scene, ScenePath, false),
                        "Rollback SaveScene returned false.");
                    File.WriteAllBytes(ScenePath, baseline);
                    File.WriteAllBytes(metaPath, metaBaseline);
                    Require(EqualBytes(baseline, File.ReadAllBytes(ScenePath))
                        && EqualBytes(metaBaseline, File.ReadAllBytes(metaPath)), "Rollback byte verification failed.");
                    throw new InvalidOperationException("[UAS-PRESENT-MODE] Apply failed; scene and meta restored exactly.", failure);
                }
            }
            finally
            {
                if (openedHere && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            }
        }

        private static bool IsValidTarget(int count, string scenePath, int mode)
            => count == 1 && scenePath == ScenePath && CombatPipelineContractLatch.IsSupported((CombatPipelineMode)mode);

        private static bool OnlyModeChanged(string before, string after)
        {
            int beforeCount = ModeLine.Matches(before).Count;
            int afterCount = ModeLine.Matches(after).Count;
            if (afterCount != 1) return false;

            if (beforeCount == 1)
            {
                // 이미 명시적으로 저장된 씬은 숫자 값 외에는 단 한 byte도 달라질 수 없다.
                return ModeLine.Replace(before, "$1MODE$2")
                    == ModeLine.Replace(after, "$1MODE$2");
            }

            if (beforeCount != 0) return false;

            // 새 직렬화 필드가 생기기 전에 저장된 씬은 해당 줄 자체가 없다. 이 경우에는
            // 저장 결과에서 정확히 한 줄(줄바꿈 포함)만 제거했을 때 실행 전 내용과 byte-for-byte
            // 같은 경우만 허용한다. Unity가 다른 필드를 재정렬하거나 값을 바꿨다면 false가 되어
            // 호출부의 scene/.meta 원복 경로로 들어간다.
            Match insertedLine = ModeLineWithTerminator.Match(after);
            return insertedLine.Success
                && after.Remove(insertedLine.Index, insertedLine.Length) == before;
        }

        private static bool OnlyModeBytesChanged(byte[] before, byte[] after)
        {
            try
            {
                // UTF-8은 유효한 동일 문자열에 대해 byte 표현이 하나뿐이다. strict decoder로 BOM과
                // 잘못된/비정규 byte도 문자열 비교에서 빠져나가지 못하게 한 뒤 같은 줄 단위 gate를 쓴다.
                return OnlyModeChanged(StrictUtf8.GetString(before), StrictUtf8.GetString(after));
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        /// <summary>
        /// 직렬화 줄이 없던 씬을 Unity가 저장하면서 전체 줄바꿈을 바꾼 경우를 안전하게 복원한다.
        /// Unity 저장본은 위치와 의미 동등성을 증명하는 자료일 뿐이며 출력의 기반은 언제나 baseline이다.
        /// 따라서 성공 결과는 baseline의 BOM/줄바꿈/공백/모든 기존 문자를 그대로 가진다.
        /// </summary>
        private static bool TryBuildAbsentModeInsertion(
            byte[] baselineBytes,
            byte[] unitySavedBytes,
            out byte[] preservedBytes,
            out string failureReason)
        {
            preservedBytes = null;
            failureReason = "unknown";
            string baseline;
            string unitySaved;
            try
            {
                baseline = StrictUtf8.GetString(baselineBytes);
                unitySaved = StrictUtf8.GetString(unitySavedBytes);
            }
            catch (DecoderFallbackException)
            {
                failureReason = "invalid-utf8";
                return false;
            }

            if (ModeLine.Matches(baseline).Count != 0)
            {
                failureReason = "baseline-mode-line-count-not-zero";
                return false;
            }

            // BOM은 텍스트 의미가 아니라 원본 byte 표현이다. Unity가 저장 중 BOM을 제거하거나
            // 추가했더라도 의미 비교에서는 제외하되, 최종 출력은 baseline의 BOM을 그대로 가진다.
            bool baselineHasBom = baseline.Length > 0 && baseline[0] == '\uFEFF';
            string semanticBaseline = baselineHasBom ? baseline.Substring(1) : baseline;
            string semanticSaved = unitySaved.Length > 0 && unitySaved[0] == '\uFEFF'
                ? unitySaved.Substring(1)
                : unitySaved;
            string normalizedBaseline = NormalizeLineEndings(semanticBaseline);
            string normalizedSaved = NormalizeLineEndings(semanticSaved);
            Match inserted = ModeLineWithTerminator.Match(normalizedSaved);
            if (!inserted.Success || ModeLineWithTerminator.Matches(normalizedSaved).Count != 1)
            {
                failureReason = "saved-mode-line-count-not-one";
                return false;
            }

            string insertedLine = inserted.Value.TrimEnd('\n');
            if (insertedLine.Trim() != Field + ": " + Expected)
            {
                failureReason = "saved-mode-value-or-shape-mismatch";
                return false;
            }

            // 줄바꿈 표현만 통일한 뒤 목표 줄 하나를 제거했을 때 전체 문자열이 같아야 한다.
            // 필드 순서, 공백, 다른 값, EOF 줄바꿈 중 하나라도 바뀌면 여기서 fail-closed한다.
            string normalizedWithoutMode = normalizedSaved.Remove(inserted.Index, inserted.Length);
            if (!string.Equals(normalizedBaseline, normalizedWithoutMode, StringComparison.Ordinal))
            {
                failureReason = "non-target-semantic-content-changed";
                return false;
            }

            if (!TryMapNormalizedOffsetToOriginal(
                    semanticBaseline, inserted.Index, out int semanticOriginalOffset))
            {
                failureReason = "mode-insertion-offset-unmappable";
                return false;
            }
            int originalOffset = semanticOriginalOffset + (baselineHasBom ? 1 : 0);

            string baselineNewLine = FindBaselineNewLine(baseline, originalOffset);
            if (baselineNewLine == null)
            {
                failureReason = "baseline-newline-style-unavailable";
                return false;
            }

            string preserved = baseline.Insert(originalOffset, insertedLine + baselineNewLine);
            preservedBytes = StrictUtf8.GetBytes(preserved);
            if (!OnlyModeBytesChanged(baselineBytes, preservedBytes))
            {
                preservedBytes = null;
                failureReason = "constructed-output-failed-exact-byte-gate";
                return false;
            }

            failureReason = "none";
            return true;
        }

        private static string NormalizeLineEndings(string value)
            => value.Replace("\r\n", "\n").Replace("\r", "\n");

        private static bool TryMapNormalizedOffsetToOriginal(
            string original,
            int normalizedOffset,
            out int originalOffset)
        {
            int normalizedIndex = 0;
            int index = 0;
            while (index < original.Length && normalizedIndex < normalizedOffset)
            {
                if (original[index] == '\r' && index + 1 < original.Length && original[index + 1] == '\n')
                    index += 2;
                else
                    index++;
                normalizedIndex++;
            }

            originalOffset = index;
            return normalizedIndex == normalizedOffset;
        }

        private static string FindBaselineNewLine(string baseline, int insertionOffset)
        {
            // 삽입 위치가 줄 시작이면 바로 앞 줄의 종결자를 우선 사용한다. 혼합 줄바꿈 파일에서도
            // 새 줄 하나가 주변 표현을 따르며, 기존 bytes에는 어떤 정규화도 가하지 않는다.
            if (insertionOffset >= 2 && baseline[insertionOffset - 2] == '\r'
                    && baseline[insertionOffset - 1] == '\n')
                return "\r\n";
            if (insertionOffset >= 1 && baseline[insertionOffset - 1] == '\n') return "\n";
            if (insertionOffset >= 1 && baseline[insertionOffset - 1] == '\r') return "\r";

            for (int index = insertionOffset; index < baseline.Length; index++)
            {
                if (baseline[index] == '\r')
                    return index + 1 < baseline.Length && baseline[index + 1] == '\n' ? "\r\n" : "\r";
                if (baseline[index] == '\n') return "\n";
            }
            return null;
        }

        private static byte[] AddUtf8Preamble(byte[] content)
        {
            byte[] preamble = Encoding.UTF8.GetPreamble();
            byte[] combined = new byte[preamble.Length + content.Length];
            Buffer.BlockCopy(preamble, 0, combined, 0, preamble.Length);
            Buffer.BlockCopy(content, 0, combined, preamble.Length, content.Length);
            return combined;
        }

        /// <summary>
        /// fail-closed 저장 검사가 거부한 이유를 확인하기 위한 제한된 진단을 만든다.
        /// 원문 전체를 절대 덤프하지 않고, 목표 모드 줄을 제거한 뒤 서로 다른 앞 네 줄만
        /// 줄당 고정 길이로 출력한다. 민감정보 키워드가 있는 줄은 내용 대신 redacted 표식만 쓴다.
        /// </summary>
        private static string FormatBoundedLineDiffDiagnostic(byte[] baseline, byte[] saved)
        {
            CountLineEndings(baseline, out int baselineCrLf, out int baselineLf);
            CountLineEndings(saved, out int savedCrLf, out int savedLf);
            var output = new StringBuilder(512);
            output.Append("baselineBytes=").Append(baseline.Length)
                .Append(", savedBytes=").Append(saved.Length)
                .Append(", baselineCRLF=").Append(baselineCrLf)
                .Append(", baselineLF=").Append(baselineLf)
                .Append(", savedCRLF=").Append(savedCrLf)
                .Append(", savedLF=").Append(savedLf);

            string before;
            string after;
            try
            {
                before = StrictUtf8.GetString(baseline);
                after = StrictUtf8.GetString(saved);
            }
            catch (DecoderFallbackException)
            {
                return output.Append(", lineDiff=<invalid-utf8>").ToString();
            }

            string[] beforeLines = RemoveModeLines(before);
            string[] afterLines = RemoveModeLines(after);
            int maximum = Math.Max(beforeLines.Length, afterLines.Length);
            int reported = 0;
            for (int index = 0; index < maximum && reported < MaximumReportedDiffLines; index++)
            {
                string beforeLine = index < beforeLines.Length ? beforeLines[index] : null;
                string afterLine = index < afterLines.Length ? afterLines[index] : null;
                if (string.Equals(beforeLine, afterLine, StringComparison.Ordinal)) continue;

                reported++;
                output.Append(", diff").Append(reported).Append("=line").Append(index + 1)
                    .Append("[before:").Append(FormatBoundedLine(beforeLine))
                    .Append("|after:").Append(FormatBoundedLine(afterLine)).Append(']');
            }

            if (reported == 0) output.Append(", lineDiff=<none-after-mode-exclusion>");
            return output.ToString();
        }

        private static string[] RemoveModeLines(string text)
        {
            string[] lines = Regex.Split(text, "\\r\\n|\\n|\\r");
            var retained = new List<string>(lines.Length);
            foreach (string line in lines)
            {
                if (!ModeLine.IsMatch(line)) retained.Add(line);
            }
            return retained.ToArray();
        }

        private static string FormatBoundedLine(string line)
        {
            if (line == null) return "<missing>";
            if (SensitiveLine.IsMatch(line)) return "<redacted-sensitive-line>";

            string safe = line.Replace("\t", "\\t");
            if (safe.Length <= MaximumReportedLineCharacters) return safe;
            return safe.Substring(0, MaximumReportedLineCharacters) + "<truncated>";
        }

        private static void CountLineEndings(byte[] bytes, out int crLf, out int lf)
        {
            crLf = 0;
            lf = 0;
            for (int index = 0; index < bytes.Length; index++)
            {
                if (bytes[index] != (byte)'\n') continue;
                if (index > 0 && bytes[index - 1] == (byte)'\r') crLf++;
                else lf++;
            }
        }

        private static bool EqualBytes(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[UAS-PRESENT-MODE] " + message);
        }
    }
}
