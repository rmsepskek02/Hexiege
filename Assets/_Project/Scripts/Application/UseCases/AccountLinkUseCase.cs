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
                // [개발] "계정 연동" 버튼은 익명 계정에서만 노출되므로, 여기에 도달했다면
                //   UI 상태와 실제 계정 상태가 어긋난 코드 버그다. 에디터에서 그대로 재현되므로
                //   축 B ①("플레이어 기기에서만 벌어지는가")이 "아니오" → 개발.
                //   축 A: 요청만 거부하고 앱은 계속 진행된다 → Warn.
                GameLog.Dev.Warn("Auth", nameof(AccountLinkUseCase),
                                 "익명 계정이 아니어서 Google 연동을 진행할 수 없다",
                                 "Provider=Google");
                return LinkResult.Failed;
            }

            try
            {
                await _authService.LinkWithGoogleAsync();
                // [개발] 성공 통보 → Info + 개발. 화면도 즉시 갱신되므로 운영 지표 가치가 없다.
                GameLog.Dev.Info("Auth", nameof(AccountLinkUseCase), "Google 연동 성공",
                                 "Provider=Google");
                return LinkResult.Success;
            }
            catch (AuthException e)
            {
                LastError = e;
                if (e.Reason == AuthErrorReason.CredentialAlreadyInUse)
                {
                    // [개발] 사용자가 이미 다른 계정에 쓰고 있는 Google 계정을 고른, 흔한 정상 실패다.
                    //   화면에는 LastError 메시지가 그대로 뜨고(원칙 2), 같은 사건을
                    //   FirebaseAuthService.ConvertException 이 운영 로그로 이미 남긴다(원칙 1).
                    GameLog.Dev.Warn("Auth", nameof(AccountLinkUseCase),
                                     "이미 다른 계정에 연동된 Google 계정",
                                     $"Provider=Google, Reason={e.Reason}");
                    return LinkResult.AlreadyLinkedElsewhere;
                }
                // [개발] 원인(예외 객체·Firebase 에러 코드)은 FirebaseAuthService.ConvertException 이
                //   LogEvent.FirebaseAuthOperationFailed 로 이미 운영 기록했다.
                //   LogRules.md 1.3 「원칙 간 우선순위」①에 따라 중복되는 이 호출부는 개발로 내린다.
                //   예외 객체는 e.Message 로 눌러 담지 않고 그대로 넘긴다(1.9).
                GameLog.Dev.Error("Auth", nameof(AccountLinkUseCase),
                                  "Google 연동 실패 — 원인은 FirebaseAuthService 가 운영 로그로 남긴다",
                                  e, $"Provider=Google, Reason={e.Reason}");
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
                // [개발] Google 경로와 완전히 같은 사건이므로 같은 축 값(Warn + 개발)을 쓴다.
                //   같은 사건에 다른 축 값을 주면 지표가 조용히 둘로 갈라진다.
                GameLog.Dev.Warn("Auth", nameof(AccountLinkUseCase),
                                 "익명 계정이 아니어서 이메일 연동을 진행할 수 없다",
                                 "Provider=Email");
                return LinkResult.Failed;
            }

            try
            {
                await _authService.LinkWithEmailAsync(email, password);
                // [개발] 성공 통보 → Info + 개발.
                //   ⚠️ 인자 email 은 로그에 넣지 않는다 — 이메일은 부분 마스킹도 금지다(LogRules.md 1.6).
                GameLog.Dev.Info("Auth", nameof(AccountLinkUseCase), "이메일 연동 성공",
                                 "Provider=Email");
                return LinkResult.Success;
            }
            catch (AuthException e)
            {
                LastError = e;
                if (e.Reason == AuthErrorReason.CredentialAlreadyInUse ||
                    e.Reason == AuthErrorReason.EmailAlreadyInUse)
                {
                    // [개발] Google 경로와 같은 사건 — 판정도 동일하게 Warn + 개발.
                    GameLog.Dev.Warn("Auth", nameof(AccountLinkUseCase),
                                     "이미 다른 계정에 연동된 이메일",
                                     $"Provider=Email, Reason={e.Reason}");
                    return LinkResult.AlreadyLinkedElsewhere;
                }
                // [개발] Google 경로와 같은 근거 — 원인은 FirebaseAuthService 가 운영으로 남긴다(원칙 1).
                GameLog.Dev.Error("Auth", nameof(AccountLinkUseCase),
                                  "이메일 연동 실패 — 원인은 FirebaseAuthService 가 운영 로그로 남긴다",
                                  e, $"Provider=Email, Reason={e.Reason}");
                return LinkResult.Failed;
            }
        }
    }
}
