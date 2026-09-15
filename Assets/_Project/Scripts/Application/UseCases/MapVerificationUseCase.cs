// ============================================================================
// MapVerificationUseCase.cs
// 「Client 가 받은 맵이 정말 그 seed 에서 나올 수 있는 맵인지」를 스스로 다시 만들어
// 대조하는 검증기(무작위 맵 3단계 H — 계획서가 부르는 이름은 「D 방식」).
//
// ─────────────────────────────────────────────────────────────────────────────
// 무엇을 하는 클래스인가 (초급자용 설명)
// ─────────────────────────────────────────────────────────────────────────────
//   멀티플레이에서 맵을 만드는 사람은 Host 한 명뿐이다. Client 는 Host 가 만든 맵을
//   통째로 받아서 쓴다. 그런데 "받은 그대로 쓴다"만 하면, Host 가 맵을 손으로
//   뜯어고쳐 자기에게 유리하게 만들어도 Client 는 알 방법이 없다.
//   (해시 대조는 "보낸 것과 받은 것이 같은가"만 알려 줄 뿐, "그 맵이 정당하게
//    만들어진 맵인가"는 알려 주지 못한다 — 뜯어고친 맵의 해시를 다시 계산해서
//    보내면 해시는 멀쩡히 맞는다.)
//
//   그래서 Client 는 이렇게 한다.
//     1) 받은 바이트에서 seed 와 맵 유형 등 「맵을 만들 때 쓴 재료」를 읽어 낸다.
//     2) 그 재료로 **똑같은 생성기를 직접 돌려** 맵을 다시 만들어 본다.
//     3) 다시 만든 맵을 바이트로 바꿨을 때 **받은 바이트와 한 바이트도 다르지 않으면**
//        "이 맵은 정말 그 seed 에서 나온 맵이다"가 증명된다.
//     4) 그때 얻은 「유형별 제약(constraints)」으로 공정성 검증기를 돌린다.
//
//   ⚠️ 이것은 규칙 16 의 "Client 는 별도로 추첨·재생성·폴백하지 않는다"를 어기는 것이
//      **아니다.** 실제로 화면에 쓰는 맵은 어디까지나 **받은 바이트를 해석한 것 하나뿐**이고,
//      여기서 다시 만든 맵은 대조가 끝나면 그 자리에서 버린다. Client 가 맵을 고를
//      자유를 늘리는 것이 아니라 **없애는 쪽으로만** 작동한다(계획서 §6-5 가 단일 소스).
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔴🔴🔴 반드시 알아야 할 함정 — 이 한 줄이 성패를 가른다 🔴🔴🔴
// ─────────────────────────────────────────────────────────────────────────────
//   **다시 만든 정의에 InitialGold 를 「받은 값」으로 심은 뒤에 Encode 해야 한다.**
//
//   왜냐하면 이 값은 생성기가 정하는 값이 아니라 **준비 조정자
//   (MapPreparationUseCase)가 생성 결과에 얹어 주는 값**이고, canonical 바이트에는
//   그 얹힌 값이 들어가기 때문이다. 심지 않고 인코딩하면 다른 모든 타일이 완벽히
//   같아도 그 필드 때문에 바이트가 달라져 **어떤 조합으로도 일치하지 않는다.**
//
//   이것은 가정이 아니라 실제로 밟았던 함정이다. 2단계에서 값을 심지 않고 인코딩했더니
//   **시드 40개 전부, 200조합 전부 불일치**였고, 심은 뒤에는 40/40 복원 성공이었다.
//   (폴백 경로가 "값 교체 뒤 해시를 다시 계산한다"고 🔴 로 적어 둔 것과 같은 함정이다 —
//    MapPreparationUseCase 파일 머리말 ③ 참조.)
//
//   🔴 2026-09-14: 종전에는 심어야 하는 값이 **TestModeFlag 와 InitialGold 두 개**였다.
//      맵 테스트 모드가 규칙에서 삭제되면서(GameSystemRules_RandomMap.md 규칙 3 아래
//      2026-09-14 개정 블록) 그 필드가 canonical 바이트에서 빠져 **하나만 남았다.**
//      ⚠️ 하나로 줄었을 뿐 **함정 자체는 그대로 살아 있다** — 남은 한 줄을 지우면
//         2단계에서 밟았던 전량 불일치가 똑같이 재현된다.
//
//   ※ 지금 코드에서는 생성 요청(MapGenerationRequest)에도 그 값을 실어 보내므로
//     생성기가 이미 같은 값을 채워 준다. 그래도 **인코딩 직전에 한 번 더 명시적으로 심는다.**
//     "생성기가 알아서 채워 주겠지"에 기대면, 생성기 쪽 한 줄이 바뀌는 순간 이 검증이
//     조용히 전부 실패하기 때문이다. 여기서는 명시가 곧 방어다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 탐색 범위 — 미지수는 딱 둘뿐이다
// ─────────────────────────────────────────────────────────────────────────────
//   canonical 바이트에는 AttemptIndex(몇 번째 시도에서 성공했는가)와
//   StartingMineSide(시작 광산 A/B)가 들어 있지 않다. 나머지 재료는 전부 들어 있다.
//   그래서 이 둘만 모든 경우를 돌려 본다.
//       AttemptIndex 0~99 (규칙 12 의 최대 100회) × StartingMineSide A/B = 최대 200조합
//   200조합을 다 돌려도 못 찾으면 실패다. 재전송하지 않는다(규칙 16 — 공정성 검증
//   실패는 즉시 실패).
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔴 이 파일은 순수 C# 이다 — UnityEngine 도 Unity.Netcode 도 참조하지 않는다
// ─────────────────────────────────────────────────────────────────────────────
//   이웃한 MapPreparationUseCase 와 같은 이유다. Unity 타입을 하나라도 쓰면
//   Unity 없이 이 파일을 컴파일해 **시드를 수백 개 돌려 복원 성공률을 실측하는 일**이
//   불가능해진다. 로그(GameLog) 호출도 넣지 않는다 — 로그는 이 결과 객체를 받아 가는
//   Infrastructure(NetworkMapTransfer)가 한 자리에서 낸다.
//   ⚠️ 이 파일에 using UnityEngine / using Unity.Netcode / GameLog 을 넣지 말 것.
//
// 근거: GameSystemRules/GameSystemRules_RandomMap.md 규칙 12 · 규칙 13 · 규칙 16
//       TechnicalDesignDocument.md 「Client 검증 순서」 4번
//       _Tasks/2026-09-08/17_29_random-map-phase3-multiplayer/Plan.md §6 · §7-H
//
// Application 레이어 — Domain 의존. Unity/Netcode/Infrastructure 직접 참조 없음.
// ============================================================================

using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// Client 쪽 맵 검증이 실패했을 때의 내부 error code.
    /// 🔴 숫자 값은 로그(`ErrorCode=`)에 그대로 남으므로 바꾸지 말 것. 새 사유는 뒤에 추가한다.
    /// </summary>
    public enum MapVerificationErrorCode
    {
        /// <summary> 실패가 아니다(성공). </summary>
        None = 0,

        /// <summary> 받은 바이트가 null 이거나 길이가 0 이다. </summary>
        EmptyBytes = 1,

        /// <summary> canonical 바이트를 맵 정의로 해석하지 못했다(형식 손상). </summary>
        DecodeFailed = 2,

        /// <summary> 지원하지 않는 canonical 형식 버전이다(규칙 16 — 즉시 실패). </summary>
        UnsupportedMapVersion = 3,

        /// <summary> 이 빌드가 모르는 맵 유형이라 생성기를 만들 수 없다. </summary>
        UnsupportedMapType = 4,

        /// <summary> 그 유형이 허용하지 않는 중립 광산 수다(생성기를 돌릴 수조차 없다). </summary>
        MineCountNotAllowed = 5,

        /// <summary> 200조합을 전부 돌렸는데 받은 바이트와 같아지는 조합이 없었다. </summary>
        ReconstructionNotFound = 6,

        /// <summary> 조합은 찾았지만 공정성 검증(MapDefinitionValidator)을 통과하지 못했다. </summary>
        ValidationFailed = 7
    }

    /// <summary>
    /// 검증이 어느 갈래에서 멈췄는지.
    ///
    /// 🔴 이 값은 로그 키를 나누기 위한 것이 <b>아니다.</b> 세 갈래(역직렬화 실패 /
    ///    재생성 불일치 / 공정성 검증 실패)는 전부 「받은 맵을 쓸 수 없어 경기가 성립하지
    ///    않았다」는 <b>한 사건</b>이라 로그 키는 MapClientVerificationFailed 하나이고,
    ///    갈래는 `key=value` 필드(`VerifyStage=`)로 가른다(LogRules 1.5 의 3단계 E 블록).
    /// </summary>
    public enum MapVerificationStage
    {
        /// <summary> 아직 아무 단계도 실패하지 않았다(성공). </summary>
        None = 0,

        /// <summary> 바이트를 맵 정의로 해석하는 단계에서 멈췄다. </summary>
        Decode = 1,

        /// <summary> 같은 seed 로 다시 만들어 바이트를 대조하는 단계에서 멈췄다. </summary>
        Reconstruct = 2,

        /// <summary> 공정성 검증(MapDefinitionValidator.Validate) 단계에서 멈췄다. </summary>
        Validate = 3
    }

    /// <summary>
    /// <see cref="MapVerificationUseCase.Verify"/> 의 결과.
    /// 성공/실패 어느 쪽이든 "아는 데까지" 값을 채워 돌려준다 — 실패해도 로그에는
    /// 어떤 맵을 검증하려다 실패했는지가 남아야 하기 때문이다.
    /// </summary>
    public sealed class MapVerificationResult
    {
        /// <summary> 검증을 전부 통과했는가. </summary>
        public bool IsSucceeded { get; }

        /// <summary> 실패 시 내부 error code(성공이면 None). </summary>
        public MapVerificationErrorCode ErrorCode { get; }

        /// <summary> 실패한 갈래(성공이면 None). 로그의 `VerifyStage=` 필드가 된다. </summary>
        public MapVerificationStage Stage { get; }

        /// <summary>
        /// 사람이 읽는 실패 사유(성공이면 null).
        /// ⚠️ 이 문장에는 쉼표가 들어갈 수 있으므로 <b>`key=value` 에 넣지 말 것</b> —
        ///    구분자 ", " 와 충돌해 필드가 쪼개진다(LogRules 1.4). message 쪽에 넣는다.
        /// </summary>
        public string FailureReason { get; }

        /// <summary>
        /// 받은 바이트를 해석한 맵 정의. 해석 자체에 실패했으면 null.
        /// 🔴 실제로 경기에 쓰는 맵은 <b>이것</b>이다(다시 만든 맵이 아니다).
        /// </summary>
        public MapDefinition Definition { get; }

        /// <summary> 복원해 낸 시도 번호. 못 찾았으면 -1. </summary>
        public int AttemptIndex { get; }

        /// <summary> 복원해 낸 시작 광산 방향(A/B). 못 찾았으면 기본값. </summary>
        public MapStartingMineSide StartingMineSide { get; }

        /// <summary>
        /// 복원해 낸 유형별 제약. 못 찾았으면 null.
        /// 🔴 이 값이 D 방식의 존재 이유다 — 공정성 검증기가 이것을 <b>필수 인자</b>로 받는데
        ///    canonical 바이트에는 들어 있지 않아 다시 만들어 보는 것 말고는 얻을 방법이 없다.
        /// </summary>
        public IMapArchetypeConstraints Constraints { get; }

        /// <summary> 실제로 돌려 본 (시도 번호 × A/B) 조합 수. 로그·성능 측정용. </summary>
        public int SearchedCombinationCount { get; }

        /// <summary> 그중 생성기가 실제로 맵을 만들어 낸(거부되지 않은) 횟수. </summary>
        public int AcceptedGenerationCount { get; }

        /// <summary> 받은 바이트에서 읽은 canonical 형식 버전. </summary>
        public int MapVersion { get; }

        /// <summary> 받은 바이트에서 읽은 root seed. </summary>
        public ulong RootSeed { get; }

        /// <summary> 받은 바이트에서 읽은 맵 유형. </summary>
        public MapType MapType { get; }

        /// <summary> 받은 바이트에서 읽은 중립 광산 수. </summary>
        public int NeutralMineCount { get; }

        // 🔴 2026-09-14 제거: 여기에 int TestModeFlag 프로퍼티가 있었다.
        //    canonical 바이트에서 그 필드가 빠져 읽을 값이 없어졌다.

        /// <summary> 받은 바이트에서 읽은 실제 초기 골드. </summary>
        public int InitialGold { get; }

        /// <summary>
        /// 결과 객체를 만든다. 보통은 <see cref="MapVerificationUseCase"/> 안의
        /// 조립 헬퍼가 부르며, 바깥에서 직접 만들 일은 없다.
        /// </summary>
        /// <param name="isSucceeded">검증 통과 여부</param>
        /// <param name="errorCode">내부 error code</param>
        /// <param name="stage">실패한 갈래</param>
        /// <param name="failureReason">사람이 읽는 실패 사유</param>
        /// <param name="definition">받은 바이트를 해석한 맵 정의</param>
        /// <param name="attemptIndex">복원한 시도 번호(못 찾았으면 -1)</param>
        /// <param name="startingMineSide">복원한 시작 광산 방향</param>
        /// <param name="constraints">복원한 유형별 제약</param>
        /// <param name="searchedCombinationCount">돌려 본 조합 수</param>
        /// <param name="acceptedGenerationCount">생성기가 실제로 맵을 만든 횟수</param>
        /// <param name="mapVersion">canonical 형식 버전</param>
        /// <param name="rootSeed">root seed</param>
        /// <param name="mapType">맵 유형</param>
        /// <param name="neutralMineCount">중립 광산 수</param>
        /// <param name="initialGold">실제 초기 골드</param>
        public MapVerificationResult(bool isSucceeded, MapVerificationErrorCode errorCode,
            MapVerificationStage stage, string failureReason, MapDefinition definition,
            int attemptIndex, MapStartingMineSide startingMineSide,
            IMapArchetypeConstraints constraints, int searchedCombinationCount,
            int acceptedGenerationCount, int mapVersion, ulong rootSeed, MapType mapType,
            int neutralMineCount, int initialGold)
        {
            IsSucceeded = isSucceeded;
            ErrorCode = errorCode;
            Stage = stage;
            FailureReason = failureReason;
            Definition = definition;
            AttemptIndex = attemptIndex;
            StartingMineSide = startingMineSide;
            Constraints = constraints;
            SearchedCombinationCount = searchedCombinationCount;
            AcceptedGenerationCount = acceptedGenerationCount;
            MapVersion = mapVersion;
            RootSeed = rootSeed;
            MapType = mapType;
            NeutralMineCount = neutralMineCount;
            InitialGold = initialGold;
        }

        /// <summary>
        /// 로그·디버그용 한 줄 요약을 만든다.
        /// </summary>
        /// <returns>사람이 읽는 요약 문자열</returns>
        public override string ToString()
        {
            return "MapVerification " + (IsSucceeded ? "성공" : "실패") +
                " ver=" + MapVersion +
                " seed=" + RootSeed +
                " type=" + MapType +
                " mines=" + NeutralMineCount +
                " gold=" + InitialGold +
                " attemptIndex=" + AttemptIndex +
                " side=" + StartingMineSide +
                " searched=" + SearchedCombinationCount +
                " stage=" + Stage +
                " error=" + ErrorCode +
                (FailureReason == null ? "" : " reason=" + FailureReason);
        }
    }

    /// <summary>
    /// 받은 canonical 맵 바이트 하나만으로 「이 맵이 정당하게 생성된 맵인가」를 증명하는 검증기.
    /// 상태를 들고 있지 않으므로 정적 클래스다(선례: <see cref="MapProjectionUseCase"/>).
    /// </summary>
    public static class MapVerificationUseCase
    {
        // ====================================================================
        // 탐색 상한
        // ====================================================================

        /// <summary>
        /// 돌려 볼 시도 번호의 상한(미포함). 규칙 12 의 「최대 100회」와 같은 값이어야 하므로
        /// 숫자를 따로 적지 않고 <see cref="MapRandomStreams.MaxAttemptCount"/> 를 그대로 쓴다.
        /// 🔴 여기에 100 을 직접 적으면, 규칙이 바뀌었을 때 두 값이 조용히 갈라진다.
        /// </summary>
        public const int MaxAttemptIndexExclusive = MapRandomStreams.MaxAttemptCount;

        /// <summary>
        /// 최대 조합 수 = 시도 번호 100 × 시작 광산 방향 2 = 200.
        /// 이 수를 넘겨 돌리는 일은 없다(넘으면 복원 실패로 끝낸다).
        /// </summary>
        public const int MaxSearchCombinationCount = MaxAttemptIndexExclusive * 2;

        /// <summary>
        /// 탐색 순서를 한 자리에 고정해 둔다. 🔴 순서를 바꾸면 <b>결과는 같지만</b>
        /// 평균 탐색 횟수(성능)가 달라진다. A → B 인 이유는 특별한 것이 없다 —
        /// enum 선언 순서를 그대로 따랐을 뿐이며, 어느 쪽이 먼저든 정답은 하나뿐이다
        /// (두 방향이 같은 바이트를 낼 수 없다 — 시작 광산 열이 다르기 때문).
        /// </summary>
        private static readonly MapStartingMineSide[] SideSearchOrder =
        {
            MapStartingMineSide.CaseA,
            MapStartingMineSide.CaseB
        };

        // ====================================================================
        // 진입점
        // ====================================================================

        /// <summary>
        /// 받은 canonical 맵 바이트를 검증한다. 동기 함수이며 실패는 예외가 아니라 결과 값으로 돌아온다.
        ///
        /// 절차(TDD 「Client 검증 순서」 4번 · 계획서 §7-H):
        ///   ① Decode 로 MapVersion · RootSeed · MapType · NeutralMineCount ·
        ///      InitialGold 를 읽는다.
        ///      (2026-09-14 까지는 여기에 TestModeFlag 도 있었다 — 맵 테스트 모드 삭제로 빠졌다.)
        ///   ② (AttemptIndex 0~99) × (StartingMineSide A/B) 를 돌며 생성 →
        ///      🔴 <b>재생성 정의에 InitialGold 를 받은 값으로 심고</b> Encode →
        ///      받은 바이트와 완전 일치하는 조합을 찾는다.
        ///   ③ 찾은 시도의 Constraints 로 MapDefinitionValidator.Validate 를 돌린다.
        ///   ④ 어느 단계든 실패하면 즉시 실패다(재전송 없음 — 규칙 16).
        ///
        /// ⚠️ <b>해시 대조는 이 함수가 하지 않는다.</b> 그것은 이 함수를 부르기 <b>전에</b>
        ///    끝나 있어야 한다(규칙 16 의 순서: 해시 검증 → 역직렬화 → 공정성 검증).
        ///    부르는 쪽(NetworkMapTransfer)이 ComputeHash + HashEquals 로 먼저 거른다.
        ///
        /// 🔴 이 함수는 최악의 경우 맵 생성을 200번 돌린다. 부르는 쪽은 그 사실을 알고
        ///    (로딩 화면이 이미 떠 있는 상태에서) 불러야 한다. 실측 평균은 훨씬 작다 —
        ///    파일 머리말과 계획서 §14 의 실측 수치 참조.
        /// </summary>
        /// <param name="canonicalBytes">Host 에게서 받은 canonical 맵 바이트(해시 대조를 이미 통과한 것)</param>
        /// <returns>검증 결과. 성공하면 복원된 시도 번호·시작 광산 방향·제약이 함께 들어 있다</returns>
        public static MapVerificationResult Verify(byte[] canonicalBytes)
        {
            // ── 0. 입력 검사 ────────────────────────────────────────────────
            if (canonicalBytes == null || canonicalBytes.Length == 0)
            {
                return Failure(MapVerificationErrorCode.EmptyBytes, MapVerificationStage.Decode,
                    "받은 canonical 바이트가 비어 있다.", null, 0, 0);
            }

            // ── 1. 해석 ─────────────────────────────────────────────────────
            // Decode 는 형식이 어긋나면 예외가 아니라 null 을 돌려준다. 네트워크로 받은
            // 데이터는 손상됐을 수 있으므로 "조용히 실패로 처리"할 수 있어야 하기 때문이다.
            MapDefinition definition = MapDefinitionCodec.Decode(canonicalBytes);
            if (definition == null)
            {
                return Failure(MapVerificationErrorCode.DecodeFailed, MapVerificationStage.Decode,
                    "canonical 바이트를 맵 정의로 해석하지 못했다(길이 " + canonicalBytes.Length + ").",
                    null, 0, 0);
            }

            // 🔴 Decode 는 첫 필드에서 미지원 버전을 이미 걸러 null 을 돌려주므로, 지금은
            //    이 검사에 절대 걸리지 않는다. 그래도 남겨 두는 이유는 두 가지다.
            //      ① 규칙 16 이 "미지원 MapVersion 은 맵 준비 실패"라고 따로 규정하고 있어,
            //         그 판정이 코드에 눈에 보이게 있어야 한다.
            //      ② 지원 버전이 둘 이상이 되는 날, 이 줄이 없으면 "해석은 됐는데 이 경로가
            //         감당 못 하는 버전"이 조용히 통과한다.
            //    ⚠️ "어차피 안 걸리니까"라며 지우지 말 것.
            if (definition.MapVersion != MapDefinition.CurrentMapVersion)
            {
                return Failure(MapVerificationErrorCode.UnsupportedMapVersion, MapVerificationStage.Decode,
                    "지원하지 않는 canonical 형식 버전이다(받은 값 " + definition.MapVersion +
                    " / 지원 " + MapDefinition.CurrentMapVersion + ").",
                    definition, 0, 0);
            }

            // ── 2. 생성기 확보 ──────────────────────────────────────────────
            // 팩터리는 모르는 유형을 받으면 예외를 던진다. 네트워크로 받은 값이 그대로
            // 들어오는 자리라 예외를 터뜨리는 대신 미리 물어보고 "실패"로 돌려준다.
            if (!IsKnownMapType(definition.MapType))
            {
                return Failure(MapVerificationErrorCode.UnsupportedMapType, MapVerificationStage.Reconstruct,
                    "이 빌드가 모르는 맵 유형이다(받은 값 " + (int)definition.MapType + ").",
                    definition, 0, 0);
            }

            IMapArchetypeGenerator generator =
                MapFallbackTemplateFactory.CreateGenerator(definition.MapType);

            // 생성기는 허용하지 않는 광산 개수를 받으면 예외를 던진다(부르는 쪽 버그를
            // 재시도 루프에 숨기지 않으려는 의도적 설계). 손상된 입력 때문에 예외가 나지
            // 않도록 여기서 먼저 물어본다.
            if (!generator.IsNeutralMineCountAllowed(definition.NeutralMineCount))
            {
                return Failure(MapVerificationErrorCode.MineCountNotAllowed, MapVerificationStage.Reconstruct,
                    definition.MapType + " 생성기가 허용하지 않는 중립 광산 수다(받은 값 " +
                    definition.NeutralMineCount + ", 허용 " + generator.MinNeutralMineCount +
                    "~" + generator.MaxNeutralMineCount + ").",
                    definition, 0, 0);
            }

            // ── 3. 복원 탐색 (D 방식의 본체) ────────────────────────────────
            int searched = 0;
            int accepted = 0;

            for (int attemptIndex = 0; attemptIndex < MaxAttemptIndexExclusive; attemptIndex++)
            {
                for (int sideIndex = 0; sideIndex < SideSearchOrder.Length; sideIndex++)
                {
                    MapStartingMineSide side = SideSearchOrder[sideIndex];
                    searched++;

                    MapGenerationResult generated = generator.Generate(
                        BuildRequest(definition, attemptIndex, side));

                    // 「거부」는 오류가 아니다. 생성 당시에도 거부된 시도는 그냥 건너뛰었다.
                    if (!generated.IsAccepted)
                    {
                        continue;
                    }

                    accepted++;

                    if (!MatchesReceivedBytes(generated.Definition, definition, canonicalBytes))
                    {
                        continue;
                    }

                    // ── 4. 찾았다 → 공정성 검증 ─────────────────────────────
                    // 🔴 검증 대상은 **받은 정의(definition)** 다. 다시 만든 정의가 아니다.
                    //    둘은 바이트가 완전히 같다고 바로 위에서 확인했으므로 어느 쪽을 넣어도
                    //    결과는 같지만, **실제로 경기에 쓸 그 객체**를 검증하는 편이
                    //    "검증한 것과 쓰는 것이 다를 수 있는 길"을 아예 없앤다.
                    //    (폴백 경로도 같은 형태다 — 다시 만든 쪽에서는 제약만 가져오고
                    //     검증은 파일에서 읽은 정의로 한다. MapPreparationUseCase 참조.)
                    MapValidationResult validation = MapDefinitionValidator.Validate(
                        definition, generated.Constraints, generator);

                    if (!validation.IsPassed)
                    {
                        return new MapVerificationResult(false, MapVerificationErrorCode.ValidationFailed,
                            MapVerificationStage.Validate,
                            "복원에는 성공했지만 공정성 검증을 통과하지 못했다: " + validation,
                            definition, attemptIndex, side, generated.Constraints, searched, accepted,
                            definition.MapVersion, definition.RootSeed, definition.MapType,
                            definition.NeutralMineCount, definition.InitialGold);
                    }

                    return new MapVerificationResult(true, MapVerificationErrorCode.None,
                        MapVerificationStage.None, null,
                        definition, attemptIndex, side, generated.Constraints, searched, accepted,
                        definition.MapVersion, definition.RootSeed, definition.MapType,
                        definition.NeutralMineCount, definition.InitialGold);
                }
            }

            // ── 5. 200조합을 다 돌렸는데 없다 → 즉시 실패 ───────────────────
            // 🔴 여기 도달했다는 것은 "받은 맵이 그 seed 에서 나올 수 없는 맵"이라는 뜻이다.
            //    누군가 맵을 손으로 고쳤거나, 양쪽 빌드의 생성기 버전이 다르거나 둘 중 하나다.
            //    어느 쪽이든 같은 데이터를 다시 받아 봐야 결과가 같으므로 재전송하지 않는다.
            return Failure(MapVerificationErrorCode.ReconstructionNotFound, MapVerificationStage.Reconstruct,
                "조합 " + searched + "개를 전부 돌렸지만 받은 바이트와 같아지는 맵을 만들지 못했다" +
                "(생성 성공 " + accepted + "회).",
                definition, searched, accepted);
        }

        // ====================================================================
        // 내부 도우미
        // ====================================================================

        /// <summary>
        /// 이 빌드가 아는 맵 유형인가.
        /// 유형 목록을 여기에 새로 적지 않고 <see cref="MapFallbackTemplateFactory.TemplateMapTypes"/>
        /// 를 쓴다 — 그 목록이 "생성기가 있는 유형 전부"이므로, 유형이 늘어도 이 코드는 그대로 맞다.
        /// </summary>
        /// <param name="mapType">받은 바이트에서 읽은 맵 유형</param>
        /// <returns>생성기를 만들 수 있으면 true</returns>
        private static bool IsKnownMapType(MapType mapType)
        {
            System.Collections.Generic.IReadOnlyList<MapType> known =
                MapFallbackTemplateFactory.TemplateMapTypes;

            for (int i = 0; i < known.Count; i++)
            {
                if (known[i] == mapType) return true;
            }

            return false;
        }

        /// <summary>
        /// 한 조합의 생성 요청을 만든다.
        ///
        /// 🔴 InitialGold 를 <b>받은 값 그대로</b> 실어 보낸다.
        ///    이 값은 생성 결과(지형·광산 자리)에는 영향을 주지 않지만
        ///    canonical 바이트에는 그대로 들어가므로, 값이 다르면 바이트가 달라진다.
        ///    (파일 머리말의 「반드시 알아야 할 함정」 참조.
        ///     2026-09-14 까지는 TestModeFlag 도 같은 이유로 함께 실어 보냈다.)
        /// </summary>
        /// <param name="received">받은 바이트를 해석한 맵 정의(재료를 여기서 읽는다)</param>
        /// <param name="attemptIndex">돌려 볼 시도 번호</param>
        /// <param name="side">돌려 볼 시작 광산 방향</param>
        /// <returns>생성 요청</returns>
        private static MapGenerationRequest BuildRequest(MapDefinition received, int attemptIndex,
            MapStartingMineSide side)
        {
            return new MapGenerationRequest
            {
                MapVersion = received.MapVersion,
                RootSeed = received.RootSeed,
                AttemptIndex = attemptIndex,
                StartingMineSide = side,
                NeutralMineCount = received.NeutralMineCount,
                InitialGold = received.InitialGold
            };
        }

        /// <summary>
        /// 다시 만든 정의를 canonical 바이트로 바꿔 받은 바이트와 한 바이트도 다르지 않은지 본다.
        ///
        /// 🔴🔴 이 메서드 안의 한 줄이 D 방식의 성패를 가른다 — 아래 「심기」 한 줄이다.
        ///    빼면 시드 전부가 200조합 전부 불일치가 된다(파일 머리말 참조).
        ///    (2026-09-14 까지는 TestModeFlag 를 함께 심어 두 줄이었다.)
        /// </summary>
        /// <param name="rebuilt">생성기가 방금 만들어 낸 정의</param>
        /// <param name="received">받은 바이트를 해석한 정의(심을 값을 여기서 읽는다)</param>
        /// <param name="receivedBytes">받은 canonical 바이트(비교 대상)</param>
        /// <returns>바이트가 완전히 같으면 true</returns>
        private static bool MatchesReceivedBytes(MapDefinition rebuilt, MapDefinition received,
            byte[] receivedBytes)
        {
            // 🔴🔴 함정 방지 — 인코딩 직전에 이 값을 「받은 값」으로 심는다. 🔴🔴
            //    생성기가 요청에서 이미 같은 값을 채워 주지만, 그 한 줄이 바뀌는 순간
            //    이 검증이 조용히 전부 실패하게 되므로 여기서 다시 명시한다.
            //    🔴 2026-09-14 까지는 바로 위에 rebuilt.TestModeFlag 를 심는 줄이 하나 더 있었다.
            //       맵 테스트 모드가 삭제돼 심을 필드가 없어졌을 뿐, 이 한 줄의 필요성은 그대로다.
            rebuilt.InitialGold = received.InitialGold;

            // 목록(성·광산·장식)의 정렬 순서를 canonical 규약에 맞춘다.
            // Decode 결과는 이미 정렬된 순서 그대로이므로 다시 만든 쪽만 맞추면 된다.
            // (폴백 경로도 같은 자리에서 같은 일을 한다.)
            rebuilt.SortCanonical();

            byte[] candidate = MapDefinitionCodec.Encode(rebuilt);

            return AreBytesEqual(candidate, receivedBytes);
        }

        /// <summary>
        /// 두 바이트 배열이 완전히 같은지 본다(길이 + 전 바이트).
        ///
        /// ⚠️ MapDefinitionCodec.HashEquals 로도 같은 일을 할 수 있지만 쓰지 않는다 —
        ///    그 함수의 이름은 "해시를 비교한다"는 뜻이라, 맵 본문 바이트를 그것으로 비교하면
        ///    읽는 사람이 "여기서 해시를 비교하는구나"로 잘못 읽는다. 이름이 하는 말과
        ///    코드가 하는 일을 일치시키는 편이 낫다.
        /// </summary>
        /// <param name="a">바이트 배열 A</param>
        /// <param name="b">바이트 배열 B</param>
        /// <returns>완전히 같으면 true</returns>
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
        /// 실패 결과를 만든다. 해석까지는 됐다면 그 값들을 함께 실어 준다 —
        /// 실패해도 로그에 "어떤 맵을 검증하려다 실패했는지"가 남아야 하기 때문이다.
        /// </summary>
        /// <param name="errorCode">내부 error code</param>
        /// <param name="stage">실패한 갈래</param>
        /// <param name="failureReason">사람이 읽는 실패 사유</param>
        /// <param name="definition">해석된 맵 정의(해석 전 실패면 null)</param>
        /// <param name="searched">돌려 본 조합 수</param>
        /// <param name="accepted">생성기가 실제로 맵을 만든 횟수</param>
        /// <returns>실패 결과</returns>
        private static MapVerificationResult Failure(MapVerificationErrorCode errorCode,
            MapVerificationStage stage, string failureReason, MapDefinition definition,
            int searched, int accepted)
        {
            if (definition == null)
            {
                return new MapVerificationResult(false, errorCode, stage, failureReason, null,
                    -1, MapStartingMineSide.CaseA, null, searched, accepted,
                    0, 0UL, default(MapType), 0, 0);
            }

            return new MapVerificationResult(false, errorCode, stage, failureReason, definition,
                -1, MapStartingMineSide.CaseA, null, searched, accepted,
                definition.MapVersion, definition.RootSeed, definition.MapType,
                definition.NeutralMineCount, definition.InitialGold);
        }
    }
}
