// ============================================================================
// IUgsAuthenticationService.cs
// 로그인 흐름에서 필요한 UGS 초기화·세션·인증 작업을 SDK 타입 없이 제공하는 계약.
// 로그인 정책과 오류 처리는 LoginUseCase가 담당한다.
// ============================================================================

using System.Threading.Tasks;

namespace Hexiege.Application
{
    /// <summary>
    /// 현재 UGS 상태 조회와 인증 작업을 제공한다.
    /// 구현체는 호출 시점의 SDK 상태를 사용하며, 자체 로그인 정책을 추가하지 않는다.
    /// </summary>
    public interface IUgsAuthenticationService
    {
        /// <summary>현재 UGS 초기화가 완료됐는지 여부.</summary>
        bool IsInitialized { get; }

        /// <summary>현재 UGS 세션의 로그인 상태. 호출할 때마다 현재 상태를 조회한다.</summary>
        bool IsSignedIn { get; }

        /// <summary>현재 UGS PlayerId. SDK 조회에서 발생한 오류는 호출자에게 전달한다.</summary>
        string PlayerId { get; }

        /// <summary>SDK 초기화를 요청한다. 로그인이나 익명 세션 폴백은 수행하지 않는다.</summary>
        Task InitializeAsync();

        /// <summary>현재 세션을 SDK의 기본 자격 보존 방식으로 로그아웃한다.</summary>
        void SignOut();

        /// <summary>
        /// 명시적 로그아웃에 필요한 현재 세션 정리를 수행한다.
        /// 자격 정리가 필요하면 로그인 여부와 관계없이 요청하고, 아니면 로그인된 세션만 종료한다.
        /// </summary>
        /// <param name="clearCredentials">저장된 익명 자격도 정리해야 하면 true.</param>
        void SignOutCurrentSession(bool clearCredentials);

        /// <summary>SDK 익명 로그인을 요청한다. 저장된 자격의 처리는 SDK에 맡긴다.</summary>
        Task SignInAnonymouslyAsync();

        /// <summary>지정한 OpenID Connect 제공자와 토큰으로 SDK 로그인을 요청한다.</summary>
        /// <param name="providerName">OpenID Connect 제공자 이름.</param>
        /// <param name="idToken">제공자가 발급한 ID Token.</param>
        Task SignInWithOpenIdConnectAsync(string providerName, string idToken);
    }
}
