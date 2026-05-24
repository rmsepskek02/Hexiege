// ============================================================================
// AccountLinkUseCase.cs
// 익명 계정을 실계정(Google / 이메일)으로 연동하는 흐름을 조율한다.
//
// 역할:
//   - 익명 → Google 연동
//   - 익명 → 이메일 연동
//   - 연동 결과를 LinkResult enum 으로 표준화
//
// 연동 특성:
//   - Firebase UID 는 연동 후에도 동일하게 유지됨 → UGS PlayerId 재발급 불필요.
//   - 연동 성공 시 ProfileView 가 즉시 UI 를 갱신해야 한다(IsAnonymous = false).
//
// 의존성:
//   - FirebaseAuthService (Infrastructure) — Firebase SDK 호출 단일 진입점.
//
// Application 레이어 — 순수 C# + Task.
// ============================================================================

using System;
using System.Threading.Tasks;
using UnityEngine;
using Hexiege.Infrastructure;

namespace Hexiege.Application
{
    /// <summary>
    /// 계정 연동 결과 코드.
    /// </summary>
    public enum LinkResult
    {
        /// <summary>연동 성공.</summary>
        Success,
        /// <summary>이미 다른 Firebase 계정에 연결된 자격증명 — 연동 중단.</summary>
        AlreadyLinkedElsewhere,
        /// <summary>기타 실패 (네트워크 / 자격증명 오류 등).</summary>
        Failed
    }

    /// <summary>
    /// 익명 계정을 실계정에 연동하는 흐름을 조율하는 UseCase.
    /// ProfileView 의 "계정 연동" 버튼에서 호출.
    /// </summary>
    public class AccountLinkUseCase
    {
        // ====================================================================
        // 의존성
        // ====================================================================

        private readonly FirebaseAuthService _authService;

        /// <summary>마지막으로 발생한 인증 예외. View 측에서 메시지 조회용.</summary>
        public AuthException LastError { get; private set; }

        // ====================================================================
        // 생성자
        // ====================================================================

        public AccountLinkUseCase(FirebaseAuthService authService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        // ====================================================================
        // Google 연동
        // ====================================================================

        /// <summary>
        /// 현재 익명 계정을 Google 계정에 연동한다.
        /// 이미 다른 Firebase 계정에 연동된 Google 계정이면 AlreadyLinkedElsewhere 반환.
        /// </summary>
        public async Task<LinkResult> LinkWithGoogleAsync()
        {
            LastError = null;

            if (!_authService.IsAnonymous)
            {
                Debug.LogWarning("[AccountLinkUseCase] 익명 계정이 아닙니다. 연동을 진행할 수 없습니다.");
                return LinkResult.Failed;
            }

            try
            {
                await _authService.LinkWithGoogleAsync();
                Debug.Log("[AccountLinkUseCase] Google 연동 성공.");
                return LinkResult.Success;
            }
            catch (AuthException e)
            {
                LastError = e;
                if (e.Reason == AuthErrorReason.CredentialAlreadyInUse)
                {
                    Debug.LogWarning("[AccountLinkUseCase] 이미 다른 계정에 연동된 Google 계정.");
                    return LinkResult.AlreadyLinkedElsewhere;
                }
                Debug.LogError($"[AccountLinkUseCase] Google 연동 실패: {e.Message}");
                return LinkResult.Failed;
            }
        }

        // ====================================================================
        // 이메일 연동
        // ====================================================================

        /// <summary>
        /// 현재 익명 계정을 이메일 계정에 연동한다.
        /// 연동 성공 시 Firebase 가 자동으로 인증 메일을 발송한다(FirebaseAuthService 내부 처리).
        /// </summary>
        /// <param name="email">연동할 이메일 주소.</param>
        /// <param name="password">신규 비밀번호.</param>
        public async Task<LinkResult> LinkWithEmailAsync(string email, string password)
        {
            LastError = null;

            if (!_authService.IsAnonymous)
            {
                Debug.LogWarning("[AccountLinkUseCase] 익명 계정이 아닙니다. 연동을 진행할 수 없습니다.");
                return LinkResult.Failed;
            }

            try
            {
                await _authService.LinkWithEmailAsync(email, password);
                Debug.Log("[AccountLinkUseCase] 이메일 연동 성공.");
                return LinkResult.Success;
            }
            catch (AuthException e)
            {
                LastError = e;
                if (e.Reason == AuthErrorReason.CredentialAlreadyInUse ||
                    e.Reason == AuthErrorReason.EmailAlreadyInUse)
                {
                    Debug.LogWarning("[AccountLinkUseCase] 이미 다른 계정에 연동된 이메일.");
                    return LinkResult.AlreadyLinkedElsewhere;
                }
                Debug.LogError($"[AccountLinkUseCase] 이메일 연동 실패: {e.Message}");
                return LinkResult.Failed;
            }
        }
    }
}
