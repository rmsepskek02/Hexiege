// ============================================================================
// ResourcesMapFallbackTemplateSource.cs
// IMapFallbackTemplateSource 의 실제 구현 — Resources 폴더에서 폴백 템플릿
// .bytes 파일을 읽어 canonical 바이트를 돌려준다.
//
// ─────────────────────────────────────────────────────────────────────────────
// 왜 Infrastructure 에 있는가 (초급자용 설명)
// ─────────────────────────────────────────────────────────────────────────────
//   Resources.Load 는 Unity 가 제공하는 「바깥 세상에서 데이터를 가져오는 수단」이다.
//   그런 수단은 Infrastructure 레이어의 몫이고, Application 은 그것을 직접 부르면
//   안 된다(Application → Infrastructure 역참조 금지).
//   그래서 Application 에 선언된 IMapFallbackTemplateSource 를 이 클래스가 구현하고,
//   조합 루트(GameBootstrapper)가 MapPreparationUseCase 에 주입한다.
//
//   이 폴더(Infrastructure/Config)에 둔 이유는, 여기가 이미
//   「Resources 아래의 저작 데이터를 런타임에 읽어 오는 것들」이 모여 있는 자리이기
//   때문이다(GameConfig · AIConfig · ToastMessageConfig …).
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔴 경로 문자열을 이 파일에 적지 않는다
// ─────────────────────────────────────────────────────────────────────────────
//   파일 이름·폴더 이름의 단일 소스는 Domain 의 MapFallbackTemplateFactory 다.
//   템플릿을 만드는 에디터 도구(MapFallbackTemplateBuilder)와 읽는 이 클래스가
//   같은 함수(GetTemplateResourcePath)를 쓰기 때문에 이름이 갈릴 수 없다.
//   경로를 여기에 직접 적는 순간 「만드는 쪽만 고치고 읽는 쪽은 안 고친」 버그가
//   런타임에만, 그것도 100회 실패했을 때만 드러난다.
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ .bytes 확장자는 선택이 아니다
// ─────────────────────────────────────────────────────────────────────────────
//   Unity 는 확장자가 .bytes 인 파일만 TextAsset 으로 임포트한다. 다른 확장자면
//   Resources.Load<TextAsset> 이 null 을 돌려준다. 그래서 템플릿 파일은 반드시
//   Assets/_Project/Resources/MapTemplates/MapTemplate_<유형>.bytes 여야 한다.
//
// Infrastructure 레이어 — Unity 의존 O, Application 인터페이스 구현.
// ============================================================================

using Hexiege.Application;
using Hexiege.Domain;
using UnityEngine;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// Resources 폴더에서 폴백 템플릿 .bytes 를 읽는 IMapFallbackTemplateSource 구현체.
    /// </summary>
    public sealed class ResourcesMapFallbackTemplateSource : IMapFallbackTemplateSource
    {
        /// <summary>
        /// 그 맵 유형의 폴백 템플릿 canonical 바이트를 Resources 에서 읽는다.
        ///
        /// 실패해도 예외를 던지지 않는다 — 템플릿이 없다는 것은 「맵 준비 실패」로
        /// 조용히 처리되고 그 사유가 로그 항목에 담겨야 하는 상황이기 때문이다(규칙 12).
        /// </summary>
        /// <param name="mapType">폴백 템플릿이 필요한 맵 유형</param>
        /// <param name="canonicalBytes">읽어 온 canonical 바이트(실패 시 null)</param>
        /// <param name="failureReason">실패 사유(성공 시 null)</param>
        /// <returns>바이트를 읽었으면 true</returns>
        public bool TryLoadCanonicalBytes(MapType mapType, out byte[] canonicalBytes,
            out string failureReason)
        {
            canonicalBytes = null;

            // 경로는 Domain 의 단일 소스에서 만든다(예: "MapTemplates/MapTemplate_Canyon").
            // Resources.Load 는 확장자를 붙이지 않는다.
            string resourcePath = MapFallbackTemplateFactory.GetTemplateResourcePath(mapType);

            var asset = Resources.Load<TextAsset>(resourcePath);

            // Unity 오브젝트는 == null 로 확인해야 "파괴됨" 상태까지 함께 잡힌다.
            if (asset == null)
            {
                failureReason = "Resources 에 템플릿이 없다: " + resourcePath +
                    " (파일 이름 " + MapFallbackTemplateFactory.GetTemplateFileName(mapType) + ").";
                return false;
            }

            byte[] bytes = asset.bytes;

            if (bytes == null || bytes.Length == 0)
            {
                failureReason = "템플릿 파일이 비어 있다: " + resourcePath + ".";
                return false;
            }

            canonicalBytes = bytes;
            failureReason = null;
            return true;
        }
    }
}
