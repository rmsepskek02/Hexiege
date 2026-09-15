// ============================================================================
// MapFallbackTemplateFactory.cs
// 「폴백(fallback) 템플릿」 5개를 만드는 순수 로직.
//
// ─────────────────────────────────────────────────────────────────────────────
// 폴백 템플릿이 무엇인가 (유니티 초급 개발자 기준 설명)
// ─────────────────────────────────────────────────────────────────────────────
//   무작위 맵은 "지형을 그린다 → 규칙대로인지 검증한다 → 통과하면 쓴다" 를 반복한다.
//   드물게 100번을 시도해도 전부 검증에 걸리는 일이 있을 수 있는데, 그때 경기를
//   시작하지 못하면 안 되므로 **미리 검증해 둔 고정 맵**을 대신 쓴다. 그 고정 맵이
//   폴백 템플릿이고, 맵 유형(5종)마다 하나씩 있다.
//
//   🔴 폴백은 「검증을 건너뛰는 비상구」가 아니라 「생성을 건너뛰는 비상구」다.
//      템플릿도 MapDefinitionValidator 전체를 완화 없이 통과해야 한다. 통과하지
//      못하면 조용히 쓰는 것이 아니라 맵 준비 실패로 처리해야 한다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 이 템플릿을 어떻게 만드는가 — 손으로 그리지 않고 「생성기에서 뽑는다」
// ─────────────────────────────────────────────────────────────────────────────
//   사람이 손으로 그린 맵이 검증 7가지(크기 전제 + 규칙 13의 6가지)를 동시에
//   만족한다는 보장은 없다. 그래서 이 파일은 맵을 직접 그리지 않는다. 대신
//
//        유형별로  광산 수 = 그 유형의 최대값,  시작 광산 = CaseA 로 고정하고
//        시드 0 부터 1씩 올려 가며 생성기를 돌려
//        MapDefinitionValidator 를 통과하는 **첫 시드**를 채택한다.
//
//   이 방식의 이점 두 가지:
//     ① 채택된 것은 **정의상** 검증을 통과한 맵이다(통과한 것만 채택하므로).
//     ② 재생성에 필요한 정보가 「유형 + 시드 + 광산 수 + A/B」 네 값으로 줄어든다.
//        저장 포맷이 바뀌어도 이 도구를 다시 돌리면 똑같은 맵이 다시 나온다.
//
//   🔴 생성기는 「같은 시드 → 같은 맵」이 보장된 결정적(deterministic) 코드다
//      (MapRandom / MapRandomStreams). 그래서 위 네 값만 적어 두면 재현된다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 템플릿이 담는 값
// ─────────────────────────────────────────────────────────────────────────────
//   · 광산 수      : 그 유형의 최대값. 숫자를 손으로 적지 않고
//                    IMapArchetypeGenerator.MaxNeutralMineCount 를 읽는다.
//   · 시작 광산    : MapStartingMineSide.CaseA 로 고정한다(좌우 대응 변환은 폐기됨 —
//                    변환하면 보호 10타일 집합이 바뀌어 검증을 깨뜨린다).
//   · InitialGold  : 광산 수에서 파생한다(규칙 3 표: 1→700 … 6→200).
//
//   🔴 2026-09-14 제거: 이 자리에 「TestModeFlag 는 항상 0 으로 담는다」와,
//      테스트 모드일 때 조정자가 초기 골드를 5000 으로 덮어쓰므로 그때는 Hash 를 반드시
//      다시 계산해야 한다는 경고가 있었다. 「맵 테스트 모드」 자체가 규칙에서 삭제돼
//      (규칙 3 아래 2026-09-14 개정 블록) 덮어쓰는 주체도 대상도 사라졌다.
//      🔴 그 결과 폴백 경로에서 템플릿 바이트가 바뀔 일이 아예 없어졌다 —
//         「정상 모드 그대로일 때의 Hash」라는 단서가 필요 없는 하나뿐인 Hash 가 된다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 이 파일이 하지 않는 것
// ─────────────────────────────────────────────────────────────────────────────
//   · 파일을 읽고 쓰지 않는다. 바이트열을 만들어 돌려줄 뿐이다.
//     (실제 저장은 에디터 도구 Assets/Editor/Tools/MapFallbackTemplateBuilder.cs,
//      실제 로드는 G 단계의 맵 준비 조정자가 한다.)
//   · Resources 폴더를 뒤지지 않는다. 파일 이름 규칙만 알려 준다.
//
//   🔴 로직이 왜 Domain(순수 C#)에 있는가:
//      에디터 도구는 Unity 가 있어야 실행되지만, 이 파일은 Unity 없이도 컴파일하고
//      돌릴 수 있다. 그래서 결과물 생성과 검증을 여기서 그대로 재현할 수 있고,
//      에디터 도구는 같은 로직을 부르는 얇은 껍데기라 두 경로가 어긋날 수 없다.
//
// 근거: GameSystemRules/GameSystemRules_RandomMap.md 규칙 12(폴백) · 규칙 13(검증) ·
//       규칙 3(초기 골드 표) / Plan.md §4-F
//
// Domain 레이어 — 순수 C#. System / System.Collections.Generic 만 사용하며
// UnityEngine · Hexiege.Core · System.Linq 를 참조하지 않는다.
// ============================================================================

using System;
using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 완성된 폴백 템플릿 하나. 맵 자체와 「이것을 어떻게 다시 만드는가」에 필요한
    /// 값들을 함께 담는다(재생성 정보 문서가 이 값들을 그대로 적는다).
    /// </summary>
    public sealed class MapFallbackTemplate
    {
        /// <summary> 이 템플릿의 맵 유형. </summary>
        public MapType MapType { get; }

        /// <summary> 채택한 root seed(검증을 통과한 첫 시드). </summary>
        public ulong AdoptedSeed { get; }

        /// <summary> 생성에 쓴 시도 번호. 템플릿은 항상 0이다. </summary>
        public int AttemptIndex { get; }

        /// <summary> 시작 광산 배치 경우. 템플릿은 항상 CaseA 다. </summary>
        public MapStartingMineSide StartingMineSide { get; }

        /// <summary> 중립 광산 개수(그 유형의 최대값). </summary>
        public int NeutralMineCount { get; }

        // 🔴 2026-09-14 제거: 여기에 int TestModeFlag 프로퍼티가 있었다(항상 0).
        //    「맵 테스트 모드」가 규칙에서 삭제돼 담을 값이 없어졌다.

        /// <summary> 초기 골드(광산 수에서 파생). </summary>
        public int InitialGold { get; }

        /// <summary> 완성된 맵 정의. Hash 필드까지 채워져 있다. </summary>
        public MapDefinition Definition { get; }

        /// <summary> 저장할 canonical 바이트열(MapDefinitionCodec.Encode 결과). </summary>
        public byte[] CanonicalBytes { get; }

        /// <summary> 위 바이트열의 SHA-256(32바이트). 재생성 정보 문서에 적는 값이다. </summary>
        public byte[] Hash { get; }

        /// <summary> 채택까지 실제로 돌려 본 시드 개수(첫 시드에 통과하면 1). </summary>
        public int SeedsTried { get; }

        /// <summary>
        /// 템플릿 하나를 만든다. 값 묶음이므로 만들어진 뒤에는 바뀌지 않는다.
        /// </summary>
        /// <param name="mapType">맵 유형</param>
        /// <param name="adoptedSeed">채택한 root seed</param>
        /// <param name="attemptIndex">시도 번호(0)</param>
        /// <param name="startingMineSide">시작 광산 배치 경우(CaseA)</param>
        /// <param name="neutralMineCount">중립 광산 개수</param>
        /// <param name="initialGold">초기 골드(광산 수에서 파생)</param>
        /// <param name="definition">완성된 맵 정의</param>
        /// <param name="canonicalBytes">canonical 바이트열</param>
        /// <param name="hash">그 바이트열의 SHA-256</param>
        /// <param name="seedsTried">채택까지 돌려 본 시드 개수</param>
        public MapFallbackTemplate(MapType mapType, ulong adoptedSeed, int attemptIndex,
            MapStartingMineSide startingMineSide, int neutralMineCount,
            int initialGold, MapDefinition definition, byte[] canonicalBytes, byte[] hash,
            int seedsTried)
        {
            MapType = mapType;
            AdoptedSeed = adoptedSeed;
            AttemptIndex = attemptIndex;
            StartingMineSide = startingMineSide;
            NeutralMineCount = neutralMineCount;
            InitialGold = initialGold;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            CanonicalBytes = canonicalBytes ?? throw new ArgumentNullException(nameof(canonicalBytes));
            Hash = hash ?? throw new ArgumentNullException(nameof(hash));
            SeedsTried = seedsTried;
        }

        /// <summary> 재생성 정보 문서에 적기 좋은 한 줄 요약을 만든다. </summary>
        /// <returns>유형 · 시드 · 광산 수 · 시작 광산 · 초기 골드 · 바이트 수를 담은 문자열</returns>
        public override string ToString()
        {
            return MapType + " seed=" + AdoptedSeed + " mines=" + NeutralMineCount +
                " side=" + StartingMineSide + " gold=" + InitialGold +
                " bytes=" + CanonicalBytes.Length;
        }
    }

    /// <summary>
    /// 폴백 템플릿 5개를 만드는 정적 도구. 파일 입출력은 하지 않는다.
    /// </summary>
    public static class MapFallbackTemplateFactory
    {
        // ====================================================================
        // 고정값 — 템플릿의 「어떻게 만들었는가」를 한 자리에 모아 둔다
        // ====================================================================

        /// <summary> 템플릿 생성에 쓰는 시도 번호. 항상 0이다(시드만 바꿔 가며 찾는다). </summary>
        public const int TemplateAttemptIndex = 0;

        // 🔴 2026-09-14 제거: 여기에 public const int TemplateTestModeFlag = 0; 이 있었다.
        //    템플릿이 담는 「맵 테스트 모드」 표식이며, 그 모드가 규칙에서 삭제돼
        //    (규칙 3 아래 2026-09-14 개정 블록) 담을 값도 그 값을 쓰던 호출부도 사라졌다.

        /// <summary>
        /// 템플릿의 시작 광산 배치 경우. 🔴 CaseA 로 고정한다.
        /// (좌우 대응 변환은 폐기됐다 — 변환하면 보호 10타일 집합이 달라져
        ///  규칙 2·13 검증을 깨뜨리기 때문이다.)
        /// </summary>
        public const MapStartingMineSide TemplateStartingMineSide = MapStartingMineSide.CaseA;

        /// <summary> 시드 탐색을 시작하는 값. </summary>
        public const ulong FirstCandidateSeed = 0UL;

        /// <summary>
        /// 시드 탐색 상한. 여기까지 전부 실패하면 「완화」하지 않고 실패로 보고한다.
        /// (실측 통과율이 유형별 99.8~100% 라 실제로는 첫 시드에 잡힌다.)
        /// </summary>
        public const int MaxCandidateSeedCount = 1000;

        /// <summary>
        /// 런타임이 읽는 Resources 하위 폴더 이름.
        /// 실제 경로는 Assets/_Project/Resources/MapTemplates/ 다.
        /// </summary>
        public const string ResourceFolderName = "MapTemplates";

        /// <summary> 템플릿 에셋 이름 앞머리. </summary>
        public const string TemplateAssetNamePrefix = "MapTemplate_";

        /// <summary>
        /// 템플릿 파일 확장자. 🔴 .bytes 여야 Unity 가 TextAsset 으로 임포트해
        /// Resources.Load&lt;TextAsset&gt; 로 읽을 수 있다. 다른 확장자면 런타임에 읽지 못한다.
        /// </summary>
        public const string TemplateFileExtension = ".bytes";

        // 템플릿을 만들 맵 유형 5종. 배열을 그대로 노출하면 바깥에서 원소를 바꿀 수 있으므로
        // IReadOnlyList 프로퍼티로만 내보낸다.
        private static readonly MapType[] AllMapTypes =
        {
            MapType.FullyOpen,
            MapType.ObstacleOpen,
            MapType.Canyon,
            MapType.Outer,
            MapType.ThreeLane
        };

        /// <summary> 템플릿을 만들어야 하는 맵 유형 5종(선언 순서 고정). </summary>
        public static IReadOnlyList<MapType> TemplateMapTypes => AllMapTypes;

        // ====================================================================
        // 이름 규칙 — 만드는 쪽과 읽는 쪽이 같은 이름을 쓰게 하는 단일 소스
        // ====================================================================

        /// <summary>
        /// 이 유형의 템플릿 에셋 이름(확장자 없음). 예: "MapTemplate_Canyon".
        /// </summary>
        /// <param name="mapType">맵 유형</param>
        /// <returns>확장자 없는 에셋 이름</returns>
        public static string GetTemplateAssetName(MapType mapType)
        {
            return TemplateAssetNamePrefix + mapType;
        }

        /// <summary>
        /// 이 유형의 템플릿 파일 이름. 예: "MapTemplate_Canyon.bytes".
        /// </summary>
        /// <param name="mapType">맵 유형</param>
        /// <returns>확장자를 포함한 파일 이름</returns>
        public static string GetTemplateFileName(MapType mapType)
        {
            return GetTemplateAssetName(mapType) + TemplateFileExtension;
        }

        /// <summary>
        /// Resources.Load 에 넘길 경로. 예: "MapTemplates/MapTemplate_Canyon".
        /// 🔴 Resources.Load 는 확장자를 붙이지 않는다.
        /// </summary>
        /// <param name="mapType">맵 유형</param>
        /// <returns>Resources 기준 상대 경로(확장자 없음)</returns>
        public static string GetTemplateResourcePath(MapType mapType)
        {
            return ResourceFolderName + "/" + GetTemplateAssetName(mapType);
        }

        // ====================================================================
        // 생성기 만들기
        // ====================================================================

        /// <summary>
        /// 맵 유형에 해당하는 생성기를 새로 만든다.
        /// 생성기는 상태를 들고 있지 않으므로 매번 새로 만들어도 된다.
        /// </summary>
        /// <param name="mapType">맵 유형</param>
        /// <returns>그 유형의 생성기</returns>
        public static IMapArchetypeGenerator CreateGenerator(MapType mapType)
        {
            switch (mapType)
            {
                case MapType.FullyOpen: return new OpenGenerator();
                case MapType.ObstacleOpen: return new ObstacleOpenGenerator();
                case MapType.Canyon: return new CanyonGenerator();
                case MapType.Outer: return new OuterGenerator();
                case MapType.ThreeLane: return new ThreeLaneGenerator();
                default:
                    // 유형이 늘었는데 여기를 안 고친 경우다. 조용히 넘기면 그 유형만
                    // 폴백 없이 남으므로 즉시 드러나도록 예외로 만든다.
                    throw new ArgumentOutOfRangeException(nameof(mapType),
                        "폴백 템플릿을 만들 수 없는 맵 유형이다: " + mapType);
            }
        }

        // ====================================================================
        // 템플릿 만들기
        // ====================================================================

        /// <summary>
        /// 한 유형의 폴백 템플릿을 만든다. 시드 0부터 올려 가며 생성기를 돌려
        /// 검증을 통과하는 첫 시드를 채택한다.
        ///
        /// 🔴 검증을 완화하지 않는다. 상한까지 전부 실패하면 실패로 돌려준다.
        /// </summary>
        /// <param name="mapType">맵 유형</param>
        /// <param name="template">완성된 템플릿(실패 시 null)</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>템플릿을 만들었으면 true</returns>
        public static bool TryBuild(MapType mapType, out MapFallbackTemplate template,
            out string failureReason)
        {
            template = null;

            IMapArchetypeGenerator generator = CreateGenerator(mapType);

            // 광산 수는 숫자를 적지 않고 생성기에게 묻는다. 유형별 최대값이 바뀌면
            // 이 도구를 다시 돌리기만 하면 되고, 이 파일은 고칠 필요가 없다.
            int neutralMineCount = generator.MaxNeutralMineCount;

            if (!generator.IsNeutralMineCountAllowed(neutralMineCount))
            {
                // 최대값인데 허용되지 않는다면 생성기 쪽 정의가 어긋난 것이다.
                failureReason = mapType + ": 생성기가 알려 준 최대 광산 수(" + neutralMineCount +
                    ")를 스스로 허용하지 않는다.";
                return false;
            }

            int initialGold = MapDefinitionValidator.GetExpectedInitialGold(neutralMineCount);

            if (initialGold < 0)
            {
                failureReason = mapType + ": 광산 수 " + neutralMineCount +
                    " 에 해당하는 초기 골드가 규칙 표에 없다.";
                return false;
            }

            // 실패 사유를 모아 두면 상한까지 실패했을 때 무엇에 걸렸는지 보고할 수 있다.
            string firstRejection = null;

            for (int offset = 0; offset < MaxCandidateSeedCount; offset++)
            {
                ulong seed = FirstCandidateSeed + (ulong)offset;

                if (TryBuildWithSeed(mapType, seed, generator, neutralMineCount, initialGold,
                        offset + 1, out template, out string attemptReason))
                {
                    failureReason = null;
                    return true;
                }

                if (firstRejection == null) firstRejection = "시드 " + seed + " → " + attemptReason;
            }

            template = null;
            failureReason = mapType + ": 시드 " + FirstCandidateSeed + " 부터 " +
                MaxCandidateSeedCount + "개를 모두 돌렸으나 검증을 통과한 맵이 없다. " +
                "첫 실패 사유 = " + firstRejection;
            return false;
        }

        /// <summary>
        /// 시드를 직접 지정해 템플릿을 만든다(재생성 정보 문서의 시드로 같은 결과가
        /// 다시 나오는지 확인할 때 쓴다). 탐색하지 않으므로 그 시드가 검증에 걸리면 실패다.
        /// </summary>
        /// <param name="mapType">맵 유형</param>
        /// <param name="rootSeed">쓸 root seed</param>
        /// <param name="template">완성된 템플릿(실패 시 null)</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>그 시드로 템플릿을 만들었으면 true</returns>
        public static bool TryBuildWithSeed(MapType mapType, ulong rootSeed,
            out MapFallbackTemplate template, out string failureReason)
        {
            IMapArchetypeGenerator generator = CreateGenerator(mapType);
            int neutralMineCount = generator.MaxNeutralMineCount;
            int initialGold = MapDefinitionValidator.GetExpectedInitialGold(neutralMineCount);

            if (initialGold < 0)
            {
                template = null;
                failureReason = mapType + ": 광산 수 " + neutralMineCount +
                    " 에 해당하는 초기 골드가 규칙 표에 없다.";
                return false;
            }

            return TryBuildWithSeed(mapType, rootSeed, generator, neutralMineCount, initialGold,
                1, out template, out failureReason);
        }

        /// <summary>
        /// 시드 하나로 생성 + 검증까지 해 보는 내부 공통 경로.
        /// 탐색 루프와 「시드 지정」 진입점이 같은 코드를 쓰게 해서 둘이 어긋나지 않게 한다.
        /// </summary>
        /// <param name="mapType">맵 유형</param>
        /// <param name="rootSeed">쓸 root seed</param>
        /// <param name="generator">그 유형의 생성기</param>
        /// <param name="neutralMineCount">중립 광산 개수</param>
        /// <param name="initialGold">초기 골드(광산 수에서 파생)</param>
        /// <param name="seedsTried">여기까지 돌려 본 시드 개수(기록용)</param>
        /// <param name="template">완성된 템플릿(실패 시 null)</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>생성과 검증을 모두 통과했으면 true</returns>
        private static bool TryBuildWithSeed(MapType mapType, ulong rootSeed,
            IMapArchetypeGenerator generator, int neutralMineCount, int initialGold,
            int seedsTried, out MapFallbackTemplate template, out string failureReason)
        {
            template = null;

            var request = new MapGenerationRequest
            {
                MapVersion = MapDefinition.CurrentMapVersion,
                RootSeed = rootSeed,
                AttemptIndex = TemplateAttemptIndex,
                StartingMineSide = TemplateStartingMineSide,
                NeutralMineCount = neutralMineCount,
                InitialGold = initialGold
            };

            MapGenerationResult generated = generator.Generate(request);

            if (!generated.IsAccepted)
            {
                failureReason = "생성 거부: " + generated.RejectionReason;
                return false;
            }

            // 🔴 여기가 폴백의 핵심이다 — 완화 없이 검증기 전체를 돌린다.
            MapValidationResult validation = MapDefinitionValidator.Validate(
                generated.Definition, generated.Constraints, generator);

            if (!validation.IsPassed)
            {
                failureReason = "검증 실패: " + validation;
                return false;
            }

            MapDefinition definition = generated.Definition;

            // 목록 순서를 canonical 정렬로 맞춰 둔다. Encode 가 내부에서 정렬한 복사본을
            // 쓰므로 바이트열에는 영향이 없지만, 이 정의를 그대로 들여다볼 때도
            // 저장된 순서와 같아 보이도록 맞춰 두는 편이 헷갈리지 않는다.
            definition.SortCanonical();

            byte[] canonicalBytes = MapDefinitionCodec.Encode(definition);
            byte[] hash = MapDefinitionCodec.ComputeHash(canonicalBytes);

            // Hash 필드는 Encode 입력에서 제외되므로, 채워 넣어도 바이트열이 달라지지 않는다.
            definition.Hash = hash;

            template = new MapFallbackTemplate(mapType, rootSeed, TemplateAttemptIndex,
                TemplateStartingMineSide, neutralMineCount, initialGold,
                definition, canonicalBytes, hash, seedsTried);

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 5개 유형의 템플릿을 한 번에 만든다. 하나라도 실패하면 전체를 실패로 돌려준다
        /// (일부만 있는 폴백 집합은 「어떤 유형에서만 조용히 맵 준비가 실패하는」
        ///  가장 찾기 어려운 형태의 구멍이 되기 때문이다).
        /// </summary>
        /// <param name="templates">유형별 템플릿 목록(선언 순서, 실패 시 null)</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>5개를 모두 만들었으면 true</returns>
        public static bool TryBuildAll(out IReadOnlyList<MapFallbackTemplate> templates,
            out string failureReason)
        {
            var built = new List<MapFallbackTemplate>(AllMapTypes.Length);

            for (int i = 0; i < AllMapTypes.Length; i++)
            {
                if (!TryBuild(AllMapTypes[i], out MapFallbackTemplate template, out string reason))
                {
                    templates = null;
                    failureReason = reason;
                    return false;
                }

                built.Add(template);
            }

            templates = built;
            failureReason = null;
            return true;
        }

        // ====================================================================
        // 확인 도구 — 왕복(Encode → Decode) 과 전체 비교
        // ====================================================================

        /// <summary>
        /// 저장할 바이트열을 다시 읽었을 때 원본과 같은 맵이 나오는지 확인한다.
        /// (저장은 됐는데 읽으면 다른 맵이 되는 사고를 막는 검사다.)
        /// </summary>
        /// <param name="template">확인할 템플릿</param>
        /// <param name="failureReason">다른 점(같으면 null)</param>
        /// <returns>왕복이 일치하면 true</returns>
        public static bool TryVerifyRoundTrip(MapFallbackTemplate template, out string failureReason)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));

            MapDefinition decoded = MapDefinitionCodec.Decode(template.CanonicalBytes);

            if (decoded == null)
            {
                failureReason = "저장한 바이트열을 다시 읽지 못했다(Decode 가 null).";
                return false;
            }

            if (!TryCompareDefinitionsExactly(template.Definition, decoded, out string diff))
            {
                failureReason = "왕복 결과가 원본과 다르다: " + diff;
                return false;
            }

            // Decode 는 Hash 필드를 복원하지 않는다(바이트열에 애초에 들어 있지 않다).
            // 그래서 「해시가 복원됐는가」가 아니라 「다시 계산하면 같은 값인가」를 본다.
            byte[] recomputed = MapDefinitionCodec.ComputeHash(decoded);

            if (!MapDefinitionCodec.HashEquals(recomputed, template.Hash))
            {
                failureReason = "왕복한 맵의 해시가 다르다(원본 " + ToHex(template.Hash) +
                    " / 왕복 " + ToHex(recomputed) + ").";
                return false;
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 두 맵 정의를 필드 하나까지 전부 비교한다.
        /// MapArchetypeGeneratorBase.TryCompareDefinitions 는 지형과 중립 광산만 보므로,
        /// 왕복 검사에는 성·시작 광산·장식·스칼라 필드까지 보는 이 메서드를 쓴다.
        /// </summary>
        /// <param name="a">맵 정의 1</param>
        /// <param name="b">맵 정의 2</param>
        /// <param name="failureReason">다른 점(같으면 null)</param>
        /// <returns>모든 필드가 같으면 true</returns>
        public static bool TryCompareDefinitionsExactly(MapDefinition a, MapDefinition b,
            out string failureReason)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            // ── 스칼라 필드 ─────────────────────────────────────────────────
            if (a.MapVersion != b.MapVersion)
            {
                failureReason = "MapVersion 이 다르다(" + a.MapVersion + " vs " + b.MapVersion + ").";
                return false;
            }

            if (a.RootSeed != b.RootSeed)
            {
                failureReason = "RootSeed 가 다르다(" + a.RootSeed + " vs " + b.RootSeed + ").";
                return false;
            }

            if (a.MapType != b.MapType)
            {
                failureReason = "MapType 이 다르다(" + a.MapType + " vs " + b.MapType + ").";
                return false;
            }

            if (a.Width != b.Width || a.Height != b.Height)
            {
                failureReason = "격자 크기가 다르다(" + a.Width + "x" + a.Height +
                    " vs " + b.Width + "x" + b.Height + ").";
                return false;
            }

            if (a.Orientation != b.Orientation)
            {
                failureReason = "Orientation 이 다르다(" + a.Orientation + " vs " + b.Orientation + ").";
                return false;
            }

            if (a.NeutralMineCount != b.NeutralMineCount)
            {
                failureReason = "NeutralMineCount 가 다르다(" + a.NeutralMineCount +
                    " vs " + b.NeutralMineCount + ").";
                return false;
            }

            // 🔴 2026-09-14 제거: 여기에 TestModeFlag 비교가 있었다. 필드가 없어졌다.

            if (a.InitialGold != b.InitialGold)
            {
                failureReason = "InitialGold 가 다르다(" + a.InitialGold +
                    " vs " + b.InitialGold + ").";
                return false;
            }

            // ── 타일 배열 ───────────────────────────────────────────────────
            if (a.Tiles.Length != b.Tiles.Length)
            {
                failureReason = "타일 개수가 다르다(" + a.Tiles.Length + " vs " + b.Tiles.Length + ").";
                return false;
            }

            for (int i = 0; i < a.Tiles.Length; i++)
            {
                if (a.Tiles[i] != b.Tiles[i])
                {
                    failureReason = "타일이 다르다(인덱스 " + i + ": " + a.Tiles[i] +
                        " vs " + b.Tiles[i] + ").";
                    return false;
                }
            }

            // ── 오브젝트 목록 ───────────────────────────────────────────────
            if (!TryComparePlacements(a.Castles, b.Castles, "성", out failureReason)) return false;
            if (!TryComparePlacements(a.StartingMines, b.StartingMines, "시작 광산", out failureReason)) return false;

            if (a.NeutralMines.Count != b.NeutralMines.Count)
            {
                failureReason = "중립 광산 개수가 다르다(" + a.NeutralMines.Count +
                    " vs " + b.NeutralMines.Count + ").";
                return false;
            }

            for (int i = 0; i < a.NeutralMines.Count; i++)
            {
                if (a.NeutralMines[i] != b.NeutralMines[i])
                {
                    failureReason = "중립 광산 자리가 다르다(자리 " + i + ": " + a.NeutralMines[i] +
                        " vs " + b.NeutralMines[i] + ").";
                    return false;
                }
            }

            if (a.Decorations.Count != b.Decorations.Count)
            {
                failureReason = "장식 개수가 다르다(" + a.Decorations.Count +
                    " vs " + b.Decorations.Count + ").";
                return false;
            }

            for (int i = 0; i < a.Decorations.Count; i++)
            {
                if (!a.Decorations[i].Equals(b.Decorations[i]))
                {
                    failureReason = "장식이 다르다(자리 " + i + ").";
                    return false;
                }
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 성·시작 광산처럼 「타일 + 팀」 목록 두 개를 순서까지 그대로 비교한다.
        /// </summary>
        /// <param name="a">목록 1</param>
        /// <param name="b">목록 2</param>
        /// <param name="label">실패 메시지에 쓸 이름</param>
        /// <param name="failureReason">다른 점(같으면 null)</param>
        /// <returns>같으면 true</returns>
        private static bool TryComparePlacements(List<MapObjectPlacement> a,
            List<MapObjectPlacement> b, string label, out string failureReason)
        {
            if (a.Count != b.Count)
            {
                failureReason = label + " 개수가 다르다(" + a.Count + " vs " + b.Count + ").";
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (!a[i].Equals(b[i]))
                {
                    failureReason = label + " 배치가 다르다(자리 " + i + ": 타일 " + a[i].TileIndex +
                        "/" + a[i].Team + " vs 타일 " + b[i].TileIndex + "/" + b[i].Team + ").";
                    return false;
                }
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 바이트열을 소문자 16진 문자열로 바꾼다(해시를 문서에 적을 때 쓴다).
        /// </summary>
        /// <param name="bytes">바꿀 바이트열</param>
        /// <returns>16진 문자열. 입력이 null 이면 빈 문자열</returns>
        public static string ToHex(byte[] bytes)
        {
            if (bytes == null) return string.Empty;

            // 16진 두 글자씩이므로 길이를 미리 잡아 두면 문자열을 다시 늘릴 일이 없다.
            var text = new System.Text.StringBuilder(bytes.Length * 2);

            for (int i = 0; i < bytes.Length; i++)
            {
                // "x2" = 소문자 16진 두 자리(예: 0x0a → "0a").
                text.Append(bytes[i].ToString("x2",
                    System.Globalization.CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }

        // ====================================================================
        // 재생성 정보 문서 — 「이 바이너리를 어떻게 다시 만드는가」
        // ====================================================================
        //
        // 🔴 왜 문서 문구까지 Domain 에 있는가:
        //    바이너리만 남으면 저장 포맷이 바뀌었을 때 손으로 다시 만들어야 한다(규칙 12).
        //    그래서 재생성 정보를 함께 남기는데, 그 문구를 에디터 도구 쪽에 두면
        //    Unity 없이 만든 문서와 에디터로 만든 문서가 조금씩 달라진다. 문자열을 만드는
        //    일은 Unity 가 필요 없으므로 여기에 두어 두 경로가 같은 글을 쓰게 한다.
        //    (파일로 저장하는 일만 에디터 도구가 한다.)

        /// <summary> 재생성 정보 문서의 확장자. </summary>
        public const string SourceDocumentFileExtension = ".md";

        /// <summary> 재생성 정보 문서 이름 앞머리. </summary>
        public const string SourceDocumentNamePrefix = "MapTemplateSource_";

        /// <summary>
        /// 이 유형의 재생성 정보 문서 파일 이름. 예: "MapTemplateSource_Canyon.md".
        /// </summary>
        /// <param name="mapType">맵 유형</param>
        /// <returns>확장자를 포함한 문서 파일 이름</returns>
        public static string GetSourceDocumentFileName(MapType mapType)
        {
            return SourceDocumentNamePrefix + mapType + SourceDocumentFileExtension;
        }

        /// <summary>
        /// 재생성 정보 문서(마크다운) 본문을 만든다.
        /// 생성 일자를 제외한 모든 내용은 템플릿에서 나오므로, 같은 템플릿이면 같은 글이 나온다.
        /// </summary>
        /// <param name="template">문서로 남길 템플릿</param>
        /// <param name="generatedDateText">생성 일자 표기(예: "2026-09-07")</param>
        /// <returns>마크다운 본문</returns>
        public static string BuildSourceDocument(MapFallbackTemplate template, string generatedDateText)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (generatedDateText == null) throw new ArgumentNullException(nameof(generatedDateText));

            string binaryFileName = GetTemplateFileName(template.MapType);
            var text = new System.Text.StringBuilder(2048);

            text.Append("# 폴백 템플릿 재생성 정보 — ").Append(template.MapType).Append("\n\n");

            text.Append("이 문서는 `Assets/_Project/Resources/MapTemplates/").Append(binaryFileName)
                .Append("` 를 **다시 만들기 위한 정보**다.\n");
            text.Append("바이너리만 남으면 저장 포맷이 바뀌었을 때 손으로 다시 만들어야 하므로, ")
                .Append("아래 값들을 함께 남긴다.\n");
            text.Append("아래 표의 값만 있으면 누구든 **바이트 단위로 같은 파일**을 다시 만들 수 있다");
            text.Append("(맵 생성기는 같은 시드에 항상 같은 맵을 만드는 결정적 코드다).\n\n");

            text.Append("## 재생성에 필요한 값\n\n");
            text.Append("| 항목 | 값 |\n");
            text.Append("|---|---|\n");
            text.Append("| 맵 유형 | ").Append(template.MapType)
                .Append(" (enum 값 ").Append((int)template.MapType).Append(") |\n");
            text.Append("| 채택 시드(root seed) | ").Append(template.AdoptedSeed).Append(" |\n");
            text.Append("| 시도 번호(attempt index) | ").Append(template.AttemptIndex).Append(" |\n");
            text.Append("| 중립 광산 수 | ").Append(template.NeutralMineCount)
                .Append(" (이 유형이 허용하는 최대값) |\n");
            text.Append("| 시작 광산 배치 | ").Append(template.StartingMineSide).Append(" |\n");
            // 🔴 2026-09-14 제거: 여기서 출처 문서에 「테스트 모드 표식」 행을 한 줄 찍었다.
            //    필드가 사라져 적을 값이 없다. (이 도구를 다시 돌리면 그 행 없이 문서가 다시 쓰인다.)
            text.Append("| 초기 골드 | ").Append(template.InitialGold)
                .Append(" (광산 수에서 파생) |\n");
            text.Append("| 맵 포맷 버전(MapVersion) | ").Append(template.Definition.MapVersion).Append(" |\n");
            text.Append("| 격자 크기 | ").Append(template.Definition.Width).Append(" x ")
                .Append(template.Definition.Height).Append(" |\n\n");

            text.Append("## 이 시드를 채택한 이유\n\n");
            text.Append("시드 ").Append(FirstCandidateSeed)
                .Append(" 부터 1씩 올려 가며 생성기를 돌려, **`MapDefinitionValidator.Validate` 를 ")
                .Append("완화 없이 통과한 첫 시드**를 채택했다.\n");
            text.Append("채택까지 돌려 본 시드 개수 = **").Append(template.SeedsTried).Append("개**");
            text.Append(template.SeedsTried == 1
                ? "(첫 시드가 바로 통과했다).\n\n"
                : ".\n\n");
            text.Append("🔴 폴백은 「검증을 건너뛰는 비상구」가 아니라 「생성을 건너뛰는 비상구」다. ");
            text.Append("템플릿도 검증기 전체를 통과해야 한다.\n\n");

            text.Append("## 결과물\n\n");
            text.Append("| 항목 | 값 |\n");
            text.Append("|---|---|\n");
            text.Append("| 파일 | `").Append(binaryFileName).Append("` |\n");
            text.Append("| Resources 경로 | `").Append(GetTemplateResourcePath(template.MapType))
                .Append("` |\n");
            text.Append("| 크기 | ").Append(template.CanonicalBytes.Length).Append(" 바이트 |\n");
            text.Append("| SHA-256 | `").Append(ToHex(template.Hash)).Append("` |\n");
            text.Append("| 성 | ").Append(template.Definition.Castles.Count).Append("개 |\n");
            text.Append("| 시작 광산 | ").Append(template.Definition.StartingMines.Count).Append("개 |\n");
            text.Append("| 중립 광산 | ").Append(template.Definition.NeutralMines.Count).Append("개 |\n");
            text.Append("| 장식 | ").Append(template.Definition.Decorations.Count)
                .Append("개 (규칙 15에 따라 최초 구현은 항상 0) |\n");
            text.Append("| 생성 일자 | ").Append(generatedDateText).Append(" |\n\n");

            text.Append("중립 광산이 놓인 자리(열, 행): ");
            for (int i = 0; i < template.Definition.NeutralMines.Count; i++)
            {
                int index = template.Definition.NeutralMines[i];
                if (i > 0) text.Append(" · ");
                text.Append('(').Append(template.Definition.ToCol(index)).Append(", ")
                    .Append(template.Definition.ToRow(index)).Append(')');
            }
            text.Append("\n\n");

            text.Append("## 다시 만드는 법\n\n");
            text.Append("Unity 상단 메뉴에서 **Hexiege > 무작위 맵 > 1. 폴백 템플릿 5개 다시 만들기** 를 실행한다.\n");
            text.Append("그 도구는 `MapFallbackTemplateFactory`(Domain, 순수 C#)를 부를 뿐이므로, ");
            text.Append("Unity 없이 그 클래스를 직접 돌려도 같은 결과가 나온다.\n\n");

            // 🔴 2026-09-14: 종전에는 여기에 「주의 — 테스트 모드와 해시」 절을 찍었다.
            //    맵 테스트 모드가 켜지면 조정자가 초기 골드를 5000 으로 덮어쓰므로 위 SHA-256 이
            //    그대로 쓰이지 않는다는 경고였는데, 그 모드가 규칙에서 삭제돼
            //    (규칙 3 아래 2026-09-14 개정 블록) 덮어쓰는 일 자체가 없어졌다.
            //    대신 「폴백이 무엇을 유지하고 무엇을 교체하는가」만 남긴다.
            text.Append("## 폴백에서 유지되는 값과 교체되는 값\n\n");
            text.Append("규칙 12에 따라 폴백을 쓰면 **맵 유형은 경기 선택 값이 유지**되고, ");
            text.Append("광산 수·시작 광산 방향·초기 골드는 템플릿 값으로 교체된다.\n");
            text.Append("템플릿의 초기 골드(").Append(template.InitialGold)
                .Append(")는 광산 수에서 규칙 3의 표로 파생한 값이다.\n\n");
            text.Append("위 SHA-256 은 이 바이트열 그대로의 값이며, 폴백 경로에서 값을 덮어쓰는 자리가 없으므로 ");
            text.Append("실제 경기에 쓰이는 해시와 같다.\n");

            return text.ToString();
        }

        // ====================================================================
        // 자기 검증 — 이 프로젝트에는 유닛 테스트 어셈블리가 없다
        // ====================================================================

        /// <summary>
        /// 5개 유형 전부 템플릿을 만들 수 있고, 전부 검증을 통과하고,
        /// 왕복과 재현성까지 성립하는지 확인한다. 실패하면 사유를 채우고 false.
        /// (테스트 어셈블리가 생기면 이 메서드를 그대로 호출하면 되도록 Conditional 을 붙이지 않았다.)
        /// </summary>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>모두 통과하면 true</returns>
        public static bool TryRunSelfCheck(out string failureReason)
        {
            // ── 0. 유형 목록이 enum 5종과 일치하는가 ────────────────────────
            // 유형이 늘었는데 목록에 안 넣으면 그 유형만 폴백 없이 남는다.
            var declared = new HashSet<MapType>(AllMapTypes);

            if (declared.Count != AllMapTypes.Length)
            {
                failureReason = "[0] 템플릿 유형 목록에 중복이 있다.";
                return false;
            }

            foreach (MapType mapType in (MapType[])Enum.GetValues(typeof(MapType)))
            {
                if (!declared.Contains(mapType))
                {
                    failureReason = "[0] 맵 유형 " + mapType + " 의 폴백 템플릿이 목록에 없다.";
                    return false;
                }
            }

            // ── 1. 5개 전부 만들어지는가 ────────────────────────────────────
            if (!TryBuildAll(out IReadOnlyList<MapFallbackTemplate> templates, out string buildReason))
            {
                failureReason = "[1] " + buildReason;
                return false;
            }

            if (templates.Count != AllMapTypes.Length)
            {
                failureReason = "[1] 템플릿이 " + AllMapTypes.Length + "개가 아니다(실제 " +
                    templates.Count + "개).";
                return false;
            }

            for (int i = 0; i < templates.Count; i++)
            {
                MapFallbackTemplate template = templates[i];

                // ── 2. 담긴 값이 규정대로인가 ───────────────────────────────
                if (template.MapType != AllMapTypes[i])
                {
                    failureReason = "[2] 템플릿 순서가 유형 목록과 다르다(자리 " + i + ").";
                    return false;
                }

                IMapArchetypeGenerator generator = CreateGenerator(template.MapType);

                if (template.NeutralMineCount != generator.MaxNeutralMineCount)
                {
                    failureReason = "[2] " + template.MapType + " 의 광산 수가 최대값이 아니다(실제 " +
                        template.NeutralMineCount + ", 최대 " + generator.MaxNeutralMineCount + ").";
                    return false;
                }

                if (template.StartingMineSide != TemplateStartingMineSide)
                {
                    failureReason = "[2] " + template.MapType + " 의 시작 광산이 " +
                        TemplateStartingMineSide + " 가 아니다(실제 " + template.StartingMineSide + ").";
                    return false;
                }

                // 🔴 2026-09-14 제거: 여기에 「템플릿의 테스트 모드 표식이 0 인가」를 보는
                //    검사가 있었다. 표식 자체가 사라져 확인할 대상이 없다.

                int expectedGold = MapDefinitionValidator.GetExpectedInitialGold(
                    template.NeutralMineCount);

                if (template.InitialGold != expectedGold)
                {
                    failureReason = "[2] " + template.MapType + " 의 초기 골드가 규칙 표와 다르다(실제 " +
                        template.InitialGold + ", 표 " + expectedGold + ").";
                    return false;
                }

                if (template.Definition.MapType != template.MapType)
                {
                    failureReason = "[2] " + template.MapType + " 의 맵 정의에 담긴 유형이 다르다(실제 " +
                        template.Definition.MapType + ").";
                    return false;
                }

                // ── 3. 완화 없이 검증기를 다시 돌려도 통과하는가 ────────────
                // TryBuild 안에서 이미 돌렸지만, 「템플릿으로 굳은 뒤에도 통과하는가」를
                // 따로 확인한다. 굳히는 과정에서 무언가를 건드렸다면 여기서 드러난다.
                var requestForConstraints = new MapGenerationRequest
                {
                    MapVersion = MapDefinition.CurrentMapVersion,
                    RootSeed = template.AdoptedSeed,
                    AttemptIndex = TemplateAttemptIndex,
                    StartingMineSide = TemplateStartingMineSide,
                    NeutralMineCount = template.NeutralMineCount,
                    InitialGold = template.InitialGold
                };

                MapGenerationResult replay = generator.Generate(requestForConstraints);

                if (!replay.IsAccepted)
                {
                    failureReason = "[3] " + template.MapType + " 을 같은 시드로 다시 만들었더니 거부됐다: " +
                        replay.RejectionReason;
                    return false;
                }

                MapValidationResult validation = MapDefinitionValidator.Validate(
                    template.Definition, replay.Constraints, generator);

                if (!validation.IsPassed)
                {
                    failureReason = "[3] " + template.MapType + " 템플릿이 검증을 통과하지 못한다: " +
                        validation;
                    return false;
                }

                // ── 4. 왕복(Encode → Decode) 이 일치하는가 ──────────────────
                if (!TryVerifyRoundTrip(template, out string roundTripReason))
                {
                    failureReason = "[4] " + template.MapType + " " + roundTripReason;
                    return false;
                }

                // ── 5. 같은 시드로 다시 만들면 바이트까지 같은가 ────────────
                if (!TryBuildWithSeed(template.MapType, template.AdoptedSeed,
                        out MapFallbackTemplate again, out string againReason))
                {
                    failureReason = "[5] " + template.MapType + " 을 같은 시드로 다시 만들지 못했다: " +
                        againReason;
                    return false;
                }

                if (again.CanonicalBytes.Length != template.CanonicalBytes.Length)
                {
                    failureReason = "[5] " + template.MapType + " 을 다시 만들었더니 길이가 다르다(" +
                        template.CanonicalBytes.Length + " vs " + again.CanonicalBytes.Length + ").";
                    return false;
                }

                for (int b = 0; b < template.CanonicalBytes.Length; b++)
                {
                    if (template.CanonicalBytes[b] != again.CanonicalBytes[b])
                    {
                        failureReason = "[5] " + template.MapType +
                            " 을 다시 만들었더니 바이트가 다르다(위치 " + b + ").";
                        return false;
                    }
                }

                // ── 6. 해시가 바이트열과 맞는가 ─────────────────────────────
                if (template.Hash.Length != MapDefinition.HashByteLength)
                {
                    failureReason = "[6] " + template.MapType + " 의 해시 길이가 " +
                        MapDefinition.HashByteLength + "바이트가 아니다(실제 " +
                        template.Hash.Length + ").";
                    return false;
                }

                if (!MapDefinitionCodec.HashEquals(template.Hash,
                        MapDefinitionCodec.ComputeHash(template.CanonicalBytes)))
                {
                    failureReason = "[6] " + template.MapType +
                        " 의 해시가 저장할 바이트열의 해시와 다르다.";
                    return false;
                }
            }

            // ── 7. 유형마다 다른 파일 이름이 나오는가 ───────────────────────
            var names = new HashSet<string>();

            for (int i = 0; i < AllMapTypes.Length; i++)
            {
                string fileName = GetTemplateFileName(AllMapTypes[i]);

                if (!fileName.EndsWith(TemplateFileExtension, StringComparison.Ordinal))
                {
                    failureReason = "[7] 파일 이름이 " + TemplateFileExtension + " 로 끝나지 않는다: " +
                        fileName;
                    return false;
                }

                if (!names.Add(fileName))
                {
                    failureReason = "[7] 파일 이름이 겹친다: " + fileName;
                    return false;
                }
            }

            // ── 8. 음성 대조 — 망가뜨린 바이트열은 왕복에서 반드시 걸려야 한다 ──
            // 검사가 실제로 무언가를 잡아내는지 확인하는 자리다. 잡지 못한다면
            // 위 [4]의 통과는 「검사가 동작한다」는 근거가 되지 못한다.
            MapFallbackTemplate first = templates[0];
            var corrupted = new byte[first.CanonicalBytes.Length];
            Array.Copy(first.CanonicalBytes, corrupted, corrupted.Length);

            // 타일 배열이 시작되는 지점을 건드린다. canonical 바이트열의 앞머리는
            // MapVersion · RootSeed · MapType · Width · Height · Orientation ·
            // NeutralMineCount · InitialGold 순서이므로
            // int 7개(28바이트) + ulong 1개(8바이트) = 36바이트 뒤부터 타일이 시작된다.
            // (순서의 단일 소스는 MapDefinitionCodec.Encode 다.)
            // 🔴 2026-09-14: TestModeFlag(int 4바이트)가 빠지면서 40 → 36 으로 줄었다.
            const int tileArrayOffset = 36;
            corrupted[tileArrayOffset] = (byte)(corrupted[tileArrayOffset] == 0 ? 1 : 0);

            MapDefinition corruptedDefinition = MapDefinitionCodec.Decode(corrupted);

            if (corruptedDefinition == null)
            {
                failureReason = "[8] 음성 대조: 한 바이트만 바꿨는데 Decode 가 통째로 실패했다" +
                    "(타일 값 자리가 아닌 곳을 건드렸을 수 있다).";
                return false;
            }

            if (TryCompareDefinitionsExactly(first.Definition, corruptedDefinition,
                    out string corruptedDiff))
            {
                failureReason = "[8] 음성 대조: 바이트를 바꿨는데도 같은 맵으로 판정됐다.";
                return false;
            }

            // ── 9. 재생성 정보 문서가 실제로 재생성에 쓸 값을 담고 있는가 ──
            // 「문서를 남겼다」와 「그 문서로 다시 만들 수 있다」는 다른 말이다.
            // 여기서는 재생성에 반드시 필요한 값(시드 · 광산 수 · 해시)이 글 안에
            // 실제로 들어 있는지, 그리고 같은 템플릿이면 같은 글이 나오는지를 본다.
            for (int i = 0; i < templates.Count; i++)
            {
                MapFallbackTemplate template = templates[i];
                const string sampleDate = "2026-09-07";

                string document = BuildSourceDocument(template, sampleDate);

                if (document.IndexOf(ToHex(template.Hash), StringComparison.Ordinal) < 0)
                {
                    failureReason = "[9] " + template.MapType + " 재생성 정보에 SHA-256 이 없다.";
                    return false;
                }

                if (document.IndexOf("| 채택 시드(root seed) | " + template.AdoptedSeed + " |",
                        StringComparison.Ordinal) < 0)
                {
                    failureReason = "[9] " + template.MapType + " 재생성 정보에 채택 시드가 없다.";
                    return false;
                }

                if (document.IndexOf(sampleDate, StringComparison.Ordinal) < 0)
                {
                    failureReason = "[9] " + template.MapType + " 재생성 정보에 생성 일자가 없다.";
                    return false;
                }

                if (!string.Equals(document, BuildSourceDocument(template, sampleDate),
                        StringComparison.Ordinal))
                {
                    failureReason = "[9] " + template.MapType +
                        " 재생성 정보가 부를 때마다 달라진다.";
                    return false;
                }

                if (!GetSourceDocumentFileName(template.MapType)
                        .EndsWith(SourceDocumentFileExtension, StringComparison.Ordinal))
                {
                    failureReason = "[9] " + template.MapType + " 재생성 정보 파일 이름이 " +
                        SourceDocumentFileExtension + " 로 끝나지 않는다.";
                    return false;
                }
            }

            failureReason = null;
            return true;
        }

        /// <summary>
        /// 자기 검증 실패를 에디터에서 즉시 드러낸다. 빌드에는 들어가지 않는다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void AssertSelfCheck()
        {
            if (!TryRunSelfCheck(out string failureReason))
            {
                throw new InvalidOperationException(
                    "MapFallbackTemplateFactory 자기 검증 실패: " + failureReason);
            }
        }
    }
}
