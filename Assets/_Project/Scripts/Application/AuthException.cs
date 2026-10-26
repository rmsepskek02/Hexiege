// ============================================================================
// AuthException.cs
// 인증 서비스의 오류를 SDK 의존 없이 전달하는 Application 계약.
// ============================================================================

using System;

namespace Hexiege.Application
{
    // ========================================================================
    // 도메인 예외 — Firebase SDK 의존을 차단하기 위한 1차 변환 결과
    // ========================================================================

    /// <summary>
    /// 인증 오류 사유 코드. View 측에서 사유별 분기 처리에 사용한다.
    /// </summary>
    public enum AuthErrorReason
    {
        Unknown,
        NotInitialized,
        NotLoggedIn,
        NotAnonymous,
        InvalidEmail,
        InvalidEmailOrPassword,
        EmailAlreadyInUse,
        WeakPassword,
        CredentialAlreadyInUse,
        NetworkError,
        TooManyRequests,
        RequiresRecentLogin,
        GoogleSignInCancelled
    }

    /// <summary>
    /// Firebase SDK 예외를 추상화한 도메인 예외.
    /// Application/Presentation 레이어에서는 이 예외만 catch 하면 충분하다.
    /// </summary>
    public class AuthException : Exception
    {
        public AuthErrorReason Reason { get; }

        public AuthException(AuthErrorReason reason, string message, Exception inner = null)
            : base(message, inner)
        {
            Reason = reason;
        }
    }
}
