// ============================================================================
// IMapFallbackTemplateSource.cs
// 「맵 유형을 주면 그 유형의 폴백 템플릿 canonical 바이트를 돌려주는」 조회 인터페이스.
//
// ─────────────────────────────────────────────────────────────────────────────
// 왜 인터페이스인가 (초급자용 설명 — 의존성 역전)
// ─────────────────────────────────────────────────────────────────────────────
//   폴백 템플릿 5개는 Assets/_Project/Resources/MapTemplates/ 아래에 .bytes 파일로
//   들어 있고, 런타임에 읽으려면 Unity 의 Resources.Load<TextAsset> 이 필요하다.
//   그런데 그 로드 코드를 맵 준비 조정자(MapPreparationUseCase)가 직접 부르면
//   두 가지가 한꺼번에 깨진다.
//
//     ① Application → Infrastructure 역참조 금지 규칙 위반
//        (Application 이 Unity/Infrastructure 의 구체 수단에 묶인다)
//     ② 조정자를 Unity 없이 시험할 수 없게 된다
//        — 이 저장소에는 Unity 가 없어서, Resources.Load 가 한 줄이라도 들어가면
//          「100회 실패 → 폴백」 경로를 여기서 단 한 번도 돌려 볼 수 없다.
//
//   그래서 「무엇이 필요한가」(이 인터페이스)는 Application 에 두고,
//   「어떻게 읽는가」(Resources.Load)는 Infrastructure 구현체에 둔다.
//   조합 루트(GameBootstrapper)가 구현체를 주입한다.
//   선례: IUnitFactory · IGameServices · ISkillDataProvider · IEntityPositionProvider.
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔴 경로·파일 이름을 이 인터페이스가 정하지 않는 이유
// ─────────────────────────────────────────────────────────────────────────────
//   템플릿 파일 이름 규칙의 단일 소스는 Domain 의 MapFallbackTemplateFactory 다
//   (ResourceFolderName · TemplateAssetNamePrefix · GetTemplateAssetName …).
//   만드는 쪽(에디터 도구)과 읽는 쪽(Infrastructure 구현체)이 같은 함수를 쓰게 해야
//   이름이 갈리지 않으므로, 이 인터페이스는 「맵 유형」만 받고 경로 문자열은 다루지 않는다.
//
// Application 레이어 — Domain 의존. Unity/Netcode/Infrastructure 직접 참조 없음.
// ============================================================================

using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 맵 유형별 폴백 템플릿의 canonical 바이트를 읽어 오는 인터페이스.
    /// 구현은 Infrastructure(Resources.Load&lt;TextAsset&gt;)가 맡는다.
    /// </summary>
    public interface IMapFallbackTemplateSource
    {
        /// <summary>
        /// 그 맵 유형의 폴백 템플릿 canonical 바이트를 읽는다.
        ///
        /// 🔴 실패를 예외로 던지지 않는 이유: 템플릿이 없다는 것은 「맵 준비 실패」로
        ///    조용히 처리되어야 하는 상황이며, 조정자가 그 사유를 로그 항목으로 담아
        ///    돌려줘야 하기 때문이다(규칙 12 「내부 error code」).
        /// </summary>
        /// <param name="mapType">폴백 템플릿이 필요한 맵 유형</param>
        /// <param name="canonicalBytes">읽어 온 canonical 바이트(실패 시 null)</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>바이트를 읽었으면 true</returns>
        bool TryLoadCanonicalBytes(MapType mapType, out byte[] canonicalBytes, out string failureReason);
    }
}
