// ============================================================================
// IAuthenticationService.cs
// 로그인·계정 연동 UseCase가 사용하는 인증 서비스 계약.
// SDK 초기화는 기존 호출자가 담당하고, UseCase에는 사용자 상태와 인증 작업만 제공한다.
// ============================================================================

using System.Threading.Tasks;

namespace Hexiege.Application
{
    /// <summary>
    /// 로그인·계정 연동에 필요한 사용자 상태와 비동기 인증 작업을 제공한다.
    /// Infrastructure의 FirebaseAuthService가 구현한다.
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>현재 로그인된 사용자가 있는지 여부.</summary>
        bool IsLoggedIn { get; }

        /// <summary>현재 사용자가 익명 계정인지 여부.</summary>
        bool IsAnonymous { get; }

        /// <summary>현재 사용자의 이메일 인증 완료 여부.</summary>
        bool IsEmailVerified { get; }

        /// <summary>현재 사용자의 Firebase UID. 로그인 전이면 빈 문자열.</summary>
        string FirebaseUID { get; }

        /// <summary>현재 사용자의 표시 이름. 없으면 빈 문자열.</summary>
        string DisplayName { get; }

        /// <summary>현재 사용자의 이메일. 없으면 빈 문자열.</summary>
        string Email { get; }

        /// <summary>익명 계정으로 로그인하고 Firebase UID를 반환한다.</summary>
        Task<string> SignInAnonymouslyAsync();

        /// <summary>Google 계정으로 로그인하고 Firebase UID를 반환한다.</summary>
        Task<string> SignInWithGoogleAsync();

        /// <summary>이메일과 비밀번호로 로그인하고 Firebase UID를 반환한다.</summary>
        Task<string> SignInWithEmailAsync(string email, string password);

        /// <summary>이메일 계정을 생성한다. 표시 이름은 지정하지 않을 수 있다.</summary>
        Task SignUpWithEmailAsync(string email, string password, string displayName = null);

        /// <summary>현재 사용자의 이메일 인증 메일을 발송한다.</summary>
        Task SendEmailVerificationAsync();

        /// <summary>현재 사용자의 이메일 인증 상태를 다시 확인하고 결과를 반환한다.</summary>
        Task<bool> CheckEmailVerifiedAsync();

        /// <summary>비밀번호 재설정 메일을 발송한다.</summary>
        Task SendPasswordResetEmailAsync(string email);

        /// <summary>현재 익명 계정을 Google 계정에 연동한다.</summary>
        Task LinkWithGoogleAsync();

        /// <summary>현재 익명 계정을 이메일 계정에 연동한다.</summary>
        Task LinkWithEmailAsync(string email, string password);

        /// <summary>현재 Firebase 세션을 종료한다.</summary>
        Task SignOutAsync();

        /// <summary>현재 Firebase 사용자를 삭제한다.</summary>
        Task DeleteCurrentUserAsync();

        /// <summary>Firebase ID Token을 발급한다. 기본값은 캐시된 토큰 우선이다.</summary>
        Task<string> GetIdTokenAsync(bool forceRefresh = false);
    }
}
