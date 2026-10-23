// ============================================================================
// UgsAuthenticationService.cs
// LoginUseCase가 요청한 UGS 작업을 SDK에 전달하는 상태 없는 어댑터.
// SDK 인스턴스를 캐시하거나 생성 시 초기화하지 않으며, 로그인 정책·로그·예외 처리를 추가하지 않는다.
// ============================================================================

using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// Application의 인증 계약을 Unity Gaming Services SDK에 연결한다.
    /// 각 조회와 작업은 기존 코드와 같은 시점에 SDK 상태를 읽는다.
    /// </summary>
    public class UgsAuthenticationService : IUgsAuthenticationService
    {
        /// <summary>현재 SDK 초기화 상태를 조회한다.</summary>
        public bool IsInitialized => UnityServices.State == ServicesInitializationState.Initialized;

        /// <summary>현재 SDK 인증 인스턴스의 로그인 상태를 조회한다.</summary>
        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;

        /// <summary>현재 SDK 인증 인스턴스의 PlayerId를 조회한다.</summary>
        public string PlayerId => AuthenticationService.Instance.PlayerId;

        /// <summary>SDK 초기화 Task를 그대로 반환한다. 별도 로그인이나 실패 처리를 수행하지 않는다.</summary>
        public Task InitializeAsync() => UnityServices.InitializeAsync();

        /// <summary>SDK의 매개변수 없는 로그아웃을 호출하여 기본 자격 보존 동작을 유지한다.</summary>
        public void SignOut() => AuthenticationService.Instance.SignOut();

        /// <summary>
        /// 명시적 로그아웃의 SDK 인스턴스를 한 번만 조회해 기존 세션 정리 조건을 적용한다.
        /// 자격 정리가 필요한데 인스턴스를 얻지 못하면 호출자에게 실패를 전달한다.
        /// </summary>
        /// <param name="clearCredentials">저장된 익명 자격도 정리해야 하면 true.</param>
        public void SignOutCurrentSession(bool clearCredentials)
        {
            // 기존 처리는 SignedIn일 때만 기본 SignOut()을 호출해 cached 자격이 남았다.
            // if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
            //     AuthenticationService.Instance.SignOut();
            var authentication = AuthenticationService.Instance;
            if (clearCredentials && authentication == null)
                throw new InvalidOperationException("익명 계정 로그아웃을 완료하지 못했습니다.");
            if (authentication != null && (clearCredentials || authentication.IsSignedIn))
            {
                // SDK는 SignedOut 상태에서도 이 호출을 허용하므로 남아 있는 익명 자격도 정리한다.
                // 실계정은 기본 자격 보존과 이후 OIDC 복원을 그대로 유지한다.
                authentication.SignOut(clearCredentials: clearCredentials);
            }
        }

        /// <summary>SDK 익명 로그인 Task를 그대로 반환한다.</summary>
        public Task SignInAnonymouslyAsync() => AuthenticationService.Instance.SignInAnonymouslyAsync();

        /// <summary>SDK OpenID Connect 로그인 Task를 그대로 반환한다.</summary>
        /// <param name="providerName">OpenID Connect 제공자 이름.</param>
        /// <param name="idToken">제공자가 발급한 ID Token.</param>
        public Task SignInWithOpenIdConnectAsync(string providerName, string idToken) =>
            AuthenticationService.Instance.SignInWithOpenIdConnectAsync(providerName, idToken);
    }
}
