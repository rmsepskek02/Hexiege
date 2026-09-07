// ============================================================================
// MapFallbackTemplateBuilder.cs  (에디터 전용 · 영구 보존 도구)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  상단 메뉴  Hexiege > 무작위 맵 > 1. 폴백 템플릿 5개 다시 만들기          │
// │    → Assets/_Project/Resources/MapTemplates/*.bytes            (런타임용) │
// │    → Assets/_Project/Docs/_Reference/MapTemplateSource/*.md  (재생성 정보)│
// │                                                                          │
// │  상단 메뉴  Hexiege > 무작위 맵 > 2. 저장된 폴백 템플릿 검사              │
// │    → 저장된 파일이 지금 만들어지는 것과 **바이트 단위로 같은지** 확인하고, │
// │      다시 읽어 검증기를 완화 없이 통과하는지까지 확인한다.                │
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 하는가 (유니티 초급 개발자 기준):
//   무작위 맵 생성이 100번 시도해도 전부 실패했을 때 대신 쓰는 「폴백 템플릿」
//   5개(맵 유형마다 1개)를 만들어 파일로 저장한다.
//
//   🔴 이 파일에는 맵을 만드는 로직이 없다. 로직은 전부
//      Assets/_Project/Scripts/Domain/Map/MapFallbackTemplateFactory.cs 에 있고,
//      이 파일은 그것을 부른 뒤 결과를 **파일로 저장하기만** 하는 얇은 껍데기다.
//
//   왜 이렇게 나눴는가:
//     · 로직이 Domain(순수 C#)에 있으면 Unity 없이도 그대로 돌려 볼 수 있다.
//       그래서 「에디터에서 만든 결과」와 「Unity 없이 만든 결과」가 어긋날 수 없다.
//     · 에디터 도구가 얇으면 규칙이 바뀌었을 때 고칠 곳이 한 군데다.
//
//   🔴 이 도구는 삭제하면 안 된다(규칙 12의 부수 조건 1 · WORKFLOW.md [5-2]).
//      Assets/Editor/Tools/ 는 「영구 보존 도구」 폴더다. 바이너리만 남고 도구가
//      사라지면 저장 포맷이 바뀌었을 때 템플릿을 손으로 다시 만들어야 한다.
//
// 이 도구가 하지 않는 것:
//   · 게임 실행 중에는 아무것도 하지 않는다(에디터 메뉴를 눌렀을 때만 동작한다).
//   · 저장된 템플릿을 게임에서 읽어 오는 일은 하지 않는다. 그것은 맵 준비
//     조정자(G 단계)의 몫이다.
//
// 근거: GameSystemRules/GameSystemRules_RandomMap.md 규칙 12 / Plan.md §4-F
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hexiege.Domain;
using UnityEditor;
using UnityEngine;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 폴백 템플릿 5개를 만들어 저장하고, 저장된 것이 아직 맞는지 검사하는 에디터 도구.
    /// </summary>
    public static class MapFallbackTemplateBuilder
    {
        // ====================================================================
        // 경로 — Assets 폴더 기준 상대 경로로만 적는다
        // ====================================================================
        //
        // ⚠️ 절대 경로를 만들 때는 UnityEngine.Application.dataPath 를 반드시
        //    **완전 수식**한다. 이 파일은 namespace Hexiege.EditorTools 안에 있어
        //    바깥 네임스페이스 Hexiege 도 이름 탐색 대상이 되는데, 거기에
        //    Hexiege.Application 네임스페이스가 있어 그냥 Application 이라고 쓰면
        //    UnityEngine.Application 이 아니라 그 네임스페이스로 해석되어 CS0234 가 난다.

        /// <summary> 런타임이 읽는 템플릿 폴더(Assets 기준). </summary>
        private const string TemplateFolderRelativeToAssets = "_Project/Resources/MapTemplates";

        /// <summary> 재생성 정보 문서 폴더(Assets 기준). 런타임은 읽지 않는다. </summary>
        private const string SourceFolderRelativeToAssets = "_Project/Docs/_Reference/MapTemplateSource";

        /// <summary> 대화상자 제목. </summary>
        private const string DialogTitle = "무작위 맵 폴백 템플릿";

        // ====================================================================
        // 메뉴 1 — 다시 만들기
        // ====================================================================

        /// <summary>
        /// 폴백 템플릿 5개를 새로 만들어 파일로 저장한다.
        /// 하나라도 만들지 못하면 아무 파일도 쓰지 않고 중단한다.
        /// </summary>
        [MenuItem("Hexiege/무작위 맵/1. 폴백 템플릿 5개 다시 만들기", false, 1)]
        public static void RebuildTemplates()
        {
            // ── 1. 만든다 (파일은 아직 건드리지 않는다) ─────────────────────
            // 🔴 일부만 성공한 상태로 파일을 덮어쓰면, 유형 몇 개만 옛 템플릿이 남아
            //    「어떤 유형에서만 조용히 맵 준비가 실패하는」 가장 찾기 어려운 상태가 된다.
            //    그래서 5개가 모두 만들어진 뒤에야 저장한다.
            if (!MapFallbackTemplateFactory.TryBuildAll(
                    out IReadOnlyList<MapFallbackTemplate> templates, out string buildFailure))
            {
                ReportFailure("템플릿을 만들지 못했습니다.\n\n" + buildFailure +
                    "\n\n파일은 하나도 바꾸지 않았습니다.");
                return;
            }

            // ── 2. 만든 것이 정말로 쓸 수 있는 것인지 다시 확인한다 ─────────
            //    (검증기 통과는 TryBuildAll 안에서 이미 봤고, 여기서는 저장했다가
            //     다시 읽었을 때 같은 맵이 되는지를 본다.)
            for (int i = 0; i < templates.Count; i++)
            {
                if (!MapFallbackTemplateFactory.TryVerifyRoundTrip(templates[i], out string roundTripFailure))
                {
                    ReportFailure(templates[i].MapType + " 템플릿의 저장/읽기 왕복이 어긋납니다.\n\n" +
                        roundTripFailure + "\n\n파일은 하나도 바꾸지 않았습니다.");
                    return;
                }
            }

            // ── 3. 저장한다 ─────────────────────────────────────────────────
            string templateFolder = Path.Combine(
                UnityEngine.Application.dataPath, TemplateFolderRelativeToAssets);
            string sourceFolder = Path.Combine(
                UnityEngine.Application.dataPath, SourceFolderRelativeToAssets);

            // 오늘 날짜. 재생성 정보 문서에 "언제 만든 것인가"로 적힌다.
            string todayText = DateTime.Now.ToString("yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture);

            var report = new StringBuilder();

            try
            {
                Directory.CreateDirectory(templateFolder);
                Directory.CreateDirectory(sourceFolder);

                for (int i = 0; i < templates.Count; i++)
                {
                    MapFallbackTemplate template = templates[i];

                    string binaryPath = Path.Combine(templateFolder,
                        MapFallbackTemplateFactory.GetTemplateFileName(template.MapType));
                    File.WriteAllBytes(binaryPath, template.CanonicalBytes);

                    string documentPath = Path.Combine(sourceFolder,
                        MapFallbackTemplateFactory.GetSourceDocumentFileName(template.MapType));

                    // BOM 없는 UTF-8 로 쓴다. 프로젝트의 다른 문서와 형식을 맞추기 위해서다.
                    File.WriteAllText(documentPath,
                        MapFallbackTemplateFactory.BuildSourceDocument(template, todayText),
                        new UTF8Encoding(false));

                    report.Append(template.MapType).Append(" · 시드 ").Append(template.AdoptedSeed)
                        .Append(" · 광산 ").Append(template.NeutralMineCount)
                        .Append("개 · 초기 골드 ").Append(template.InitialGold)
                        .Append(" · ").Append(template.CanonicalBytes.Length).Append("바이트\n")
                        .Append("    SHA-256 ")
                        .Append(MapFallbackTemplateFactory.ToHex(template.Hash)).Append('\n');
                }
            }
            catch (Exception e)
            {
                // 파일 쓰기는 권한·잠금 등으로 실패할 수 있다. 조용히 넘기면
                // "성공했다고 했는데 파일이 그대로"인 상태가 되므로 반드시 드러낸다.
                ReportFailure("파일을 저장하지 못했습니다.\n\n" + e.Message);
                return;
            }

            // 새로 쓴 파일을 에디터가 인식하도록 다시 읽어들인다.
            AssetDatabase.Refresh();

            Debug.Log("[MapFallbackTemplateBuilder] 폴백 템플릿 5개 저장 완료\n" + report);

            EditorUtility.DisplayDialog(DialogTitle,
                "폴백 템플릿 5개를 다시 만들어 저장했습니다.\n\n" +
                "· 바이너리: Assets/" + TemplateFolderRelativeToAssets + "/\n" +
                "· 재생성 정보: Assets/" + SourceFolderRelativeToAssets + "/\n\n" +
                report,
                "확인");
        }

        // ====================================================================
        // 메뉴 2 — 저장된 것 검사
        // ====================================================================

        /// <summary>
        /// 저장된 템플릿 파일이 지금 만들어지는 것과 같은지, 그리고 다시 읽어
        /// 검증기를 완화 없이 통과하는지 확인한다. 파일은 바꾸지 않는다.
        /// </summary>
        [MenuItem("Hexiege/무작위 맵/2. 저장된 폴백 템플릿 검사", false, 2)]
        public static void VerifySavedTemplates()
        {
            // 먼저 Domain 쪽 자기 검증을 돌린다. 여기서 걸리면 저장된 파일을 볼 것도 없이
            // 만드는 로직 자체가 어긋난 것이다.
            if (!MapFallbackTemplateFactory.TryRunSelfCheck(out string selfCheckFailure))
            {
                ReportFailure("MapFallbackTemplateFactory 자기 검증 실패\n\n" + selfCheckFailure);
                return;
            }

            if (!MapFallbackTemplateFactory.TryBuildAll(
                    out IReadOnlyList<MapFallbackTemplate> templates, out string buildFailure))
            {
                ReportFailure("비교용 템플릿을 만들지 못했습니다.\n\n" + buildFailure);
                return;
            }

            string templateFolder = Path.Combine(
                UnityEngine.Application.dataPath, TemplateFolderRelativeToAssets);

            var report = new StringBuilder();
            int problemCount = 0;

            for (int i = 0; i < templates.Count; i++)
            {
                MapFallbackTemplate template = templates[i];
                string fileName = MapFallbackTemplateFactory.GetTemplateFileName(template.MapType);
                string path = Path.Combine(templateFolder, fileName);

                if (!File.Exists(path))
                {
                    problemCount++;
                    report.Append("[없음] ").Append(fileName).Append('\n');
                    continue;
                }

                byte[] saved;
                try
                {
                    saved = File.ReadAllBytes(path);
                }
                catch (Exception e)
                {
                    problemCount++;
                    report.Append("[읽기 실패] ").Append(fileName).Append(" — ")
                        .Append(e.Message).Append('\n');
                    continue;
                }

                if (!AreBytesEqual(saved, template.CanonicalBytes))
                {
                    problemCount++;
                    report.Append("[다름] ").Append(fileName)
                        .Append(" — 저장본 ").Append(saved.Length)
                        .Append("바이트 / 지금 만든 것 ").Append(template.CanonicalBytes.Length)
                        .Append("바이트. 메뉴 1로 다시 만들어야 합니다.\n");
                    continue;
                }

                // 저장본을 실제로 다시 읽어 검증기를 완화 없이 돌린다.
                MapDefinition decoded = MapDefinitionCodec.Decode(saved);

                if (decoded == null)
                {
                    problemCount++;
                    report.Append("[해석 실패] ").Append(fileName).Append('\n');
                    continue;
                }

                IMapArchetypeGenerator generator =
                    MapFallbackTemplateFactory.CreateGenerator(decoded.MapType);

                // 검증기는 「그 시도에서 확정된 유형별 제약」을 함께 요구한다. 템플릿은
                // 시드가 고정돼 있으므로 같은 시드로 한 번 더 생성해 그 제약을 얻는다.
                var request = new MapGenerationRequest
                {
                    MapVersion = decoded.MapVersion,
                    RootSeed = decoded.RootSeed,
                    AttemptIndex = MapFallbackTemplateFactory.TemplateAttemptIndex,
                    StartingMineSide = MapFallbackTemplateFactory.TemplateStartingMineSide,
                    NeutralMineCount = decoded.NeutralMineCount,
                    TestModeFlag = decoded.TestModeFlag,
                    InitialGold = decoded.InitialGold
                };

                MapGenerationResult replay = generator.Generate(request);

                if (!replay.IsAccepted)
                {
                    problemCount++;
                    report.Append("[재생성 거부] ").Append(fileName).Append(" — ")
                        .Append(replay.RejectionReason).Append('\n');
                    continue;
                }

                MapValidationResult validation =
                    MapDefinitionValidator.Validate(decoded, replay.Constraints, generator);

                if (!validation.IsPassed)
                {
                    problemCount++;
                    report.Append("[검증 실패] ").Append(fileName).Append(" — ")
                        .Append(validation).Append('\n');
                    continue;
                }

                report.Append("[정상] ").Append(fileName)
                    .Append(" · 시드 ").Append(decoded.RootSeed)
                    .Append(" · 광산 ").Append(decoded.NeutralMineCount)
                    .Append("개 · 초기 골드 ").Append(decoded.InitialGold)
                    .Append(" · ").Append(saved.Length).Append("바이트\n");
            }

            if (problemCount > 0)
            {
                Debug.LogError("[MapFallbackTemplateBuilder] 저장된 템플릿 검사 — 문제 " +
                    problemCount + "건\n" + report);
            }
            else
            {
                Debug.Log("[MapFallbackTemplateBuilder] 저장된 템플릿 검사 — 5개 모두 정상\n" + report);
            }

            EditorUtility.DisplayDialog(DialogTitle,
                (problemCount == 0
                    ? "저장된 폴백 템플릿 5개가 모두 정상입니다.\n\n"
                    : "문제 " + problemCount + "건을 찾았습니다.\n\n") + report,
                "확인");
        }

        // ====================================================================
        // 보조
        // ====================================================================

        /// <summary> 두 바이트열이 완전히 같은지 확인한다. </summary>
        /// <param name="a">바이트열 1</param>
        /// <param name="b">바이트열 2</param>
        /// <returns>길이와 모든 바이트가 같으면 true</returns>
        private static bool AreBytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }

            return true;
        }

        /// <summary>
        /// 실패를 콘솔과 대화상자 양쪽에 남긴다. 대화상자만 띄우면 나중에 확인할 수 없고,
        /// 콘솔에만 남기면 메뉴를 누른 사람이 못 보고 지나칠 수 있기 때문이다.
        /// </summary>
        /// <param name="message">사용자에게 보일 메시지</param>
        private static void ReportFailure(string message)
        {
            Debug.LogError("[MapFallbackTemplateBuilder] " + message);
            EditorUtility.DisplayDialog(DialogTitle, message, "확인");
        }
    }
}
