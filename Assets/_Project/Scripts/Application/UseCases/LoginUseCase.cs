// ============================================================================
// LoginUseCase.cs
// 로그인 흐름을 조율하는 Application 레이어 클래스.
//
// 역할:
//   - 익명 / Google / 이메일 3가지 로그인 흐름을 통합 API 로 제공
//   - Firebase 로그인 결과를 LoginResult enum 으로 표준화
//   - UGS 브릿지(실계정: Firebase ID Token → UGS OIDC / 익명: UGS 익명 로그인) 수행
//   - 이메일 인증 상태 확인 / 인증 메일 발송 / 비밀번호 재설정 메일 발송
//
// 의존성:
//   - FirebaseAuthService (Infrastructure) — 실제 Firebase SDK 호출 담당
//
// 주의:
//   - 본 클래스는 Firebase SDK 네임스페이스를 직접 import 하지 않는다.
//     모든 Firebase 호출은 FirebaseAuthService 를 통해서만 이루어진다.
//   - UGS(Unity Gaming Services) 와의 연결은 UGS SDK 를 직접 사용하지만,
//     UGS 는 멀티플레이의 인프라 계층 의존성이므로 Application 에서 직접 사용해도
//     아키텍처상 허용된다(LobbyManager, RelayManager 와 동일한 방향).
//
// Application 레이어 — 순수 C# + Task.
// ============================================================================

using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using Hexiege.Infrastructure;

namespace Hexiege.Application
{
    /// <summary>
    /// 로그인 결과 코드.
    /// View 측이 결과별 다음 화면 분기에 사용한다.
    /// </summary>
    public enum LoginResult
    {
        /// <summary>로그인 성공 + 후속 단계(UGS 브릿지) 진행 가능.</summary>
        Success,
        /// <summary>로그인은 성공했으나 이메일 인증이 완료되지 않아 차단됨. 인증 화면으로 이동 필요.</summary>
        NeedsEmailVerification,
        /// <summary>로그인 실패 (자격증명 오류 / 네트워크 오류 등).</summary>
        Failed
    }

    /// <summary>
    /// 로그인 흐름 조율 UseCase.
    /// View → UseCase → FirebaseAuthService → Firebase SDK 흐름의 중간 레이어.
    /// </summary>
    public class LoginUseCase
    {
        // ====================================================================
        // 의존성
        // ====================================================================

        private readonly FirebaseAuthService _authService;

        /// <summary>마지막으로 발생한 인증 예외. View 측에서 메시지 조회용.</summary>
        public AuthException LastError { get; private set; }

        // ====================================================================
        // 공개 상태 프로퍼티 (FirebaseAuthService 위임)
        // ====================================================================

        /// <summary>현재 Firebase 로그인 상태.</summary>
        public bool IsLoggedIn => _authService.IsLoggedIn;

        /// <summary>현재 사용자가 익명 계정인지 여부.</summary>
        public bool IsAnonymous => _authService.IsAnonymous;

        /// <summary>현재 Firebase UID.</summary>
        public string FirebaseUID => _authService.FirebaseUID;

        /// <summary>현재 사용자 이메일.</summary>
        public string Email => _authService.Email;

        /// <summary>현재 사용자 표시 이름.</summary>
        public string DisplayName => _authService.DisplayName;

        /// <summary>현재 사용자의 이메일 인증 완료 여부.</summary>
        public bool IsEmailVerified => _authService.IsEmailVerified;

        // ====================================================================
        // 생성자
        // ====================================================================

        /// <summary>
        /// 의존성을 외부에서 주입받는다 — LoginBootstrapper 에서 생성/주입.
        /// </summary>
        public LoginUseCase(FirebaseAuthService authService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        // ====================================================================
        // 자동 로그인 (세션 복원)
        // ====================================================================

        /// <summary>
        /// 앱 시작 시 Firebase 세션이 남아있는지 확인한다.
        /// 세션이 유효하면 UGS 브릿지까지 자동 수행한다.
        /// </summary>
        /// <returns>자동 로그인 성공 여부.</returns>
        public async Task<bool> TryAutoLoginAsync()
        {
            if (!_authService.IsLoggedIn)
            {
                Debug.Log("[LoginUseCase] 자동 로그인: 세션 없음.");
                return false;
            }

            try
            {
                Debug.Log($"[LoginUseCase] 자동 로그인: 세션 발견 (UID={_authService.FirebaseUID}). UGS 브릿지 진행.");
                await BridgeToUGSAsync(_authService.FirebaseUID);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LoginUseCase] 자동 로그인 중 UGS 브릿지 실패: {e.Message}");
                return false;
            }
        }

        // ====================================================================
        // 익명 로그인
        // ====================================================================

        /// <summary>
        /// 익명 계정으로 로그인한 후 UGS 브릿지까지 수행한다.
        /// </summary>
        public async Task<LoginResult> SignInAnonymouslyAsync()
        {
            LastError = null;
            try
            {
                string uid = await _authService.SignInAnonymouslyAsync();
                await BridgeToUGSAsync(uid);
                return LoginResult.Success;
            }
            catch (AuthException e)
            {
                LastError = e;
                return LoginResult.Failed;
            }
        }

        // ====================================================================
        // Google 로그인
        // ====================================================================

        /// <summary>
        /// Google 계정으로 로그인한 후 UGS 브릿지까지 수행한다.
        /// </summary>
        public async Task<LoginResult> SignInWithGoogleAsync()
        {
            LastError = null;
            try
            {
                string uid = await _authService.SignInWithGoogleAsync();
                await BridgeToUGSAsync(uid);
                return LoginResult.Success;
            }
            catch (AuthException e)
            {
                LastError = e;
                return LoginResult.Failed;
            }
        }

        // ====================================================================
        // 이메일 로그인 / 회원가입
        // ====================================================================

        /// <summary>
        /// 이메일 + 비밀번호로 로그인한다.
        /// 이메일 인증이 완료되지 않은 계정은 NeedsEmailVerification 반환 (로그인 차단).
        /// </summary>
        public async Task<LoginResult> SignInWithEmailAsync(string email, string password)
        {
            LastError = null;
            try
            {
                string uid = await _authService.SignInWithEmailAsync(email, password);

                // 이메일 인증이 안된 경우 즉시 로그아웃하지 않고 NeedsEmailVerification 만 반환.
                // 호출자(View)가 EmailVerifyView 로 이동하여 인증 메일 재발송/완료 확인을 진행할 수 있도록.
                if (!_authService.IsEmailVerified)
                {
                    Debug.Log("[LoginUseCase] 이메일 미인증 계정 — NeedsEmailVerification 반환.");
                    return LoginResult.NeedsEmailVerification;
                }

                await BridgeToUGSAsync(uid);
                return LoginResult.Success;
            }
            catch (AuthException e)
            {
                LastError = e;
                return LoginResult.Failed;
            }
        }

        /// <summary>
        /// 이메일 회원가입을 수행한다.
        /// 성공 시 자동으로 인증 메일이 발송된다 (FirebaseAuthService 내부 처리).
        /// 회원가입 직후의 사용자는 이메일 미인증 상태이므로, View 는 곧바로
        /// EmailVerifyView 로 이동해야 한다(UGS 브릿지는 인증 완료 후 진행).
        /// </summary>
        public async Task<LoginResult> SignUpWithEmailAsync(string email, string password, string displayName)
        {
            LastError = null;
            try
            {
                await _authService.SignUpWithEmailAsync(email, password, displayName);

                // 회원가입 직후 자동 로그인 상태 — 곧바로 인증 메일 발송.
                await _authService.SendEmailVerificationAsync();
                return LoginResult.NeedsEmailVerification;
            }
            catch (AuthException e)
            {
                LastError = e;
                return LoginResult.Failed;
            }
        }

        /// <summary>
        /// 인증 메일 재발송. EmailVerifyView 의 "재발송" 버튼에서 호출.
        /// </summary>
        public async Task<bool> SendEmailVerificationAsync()
        {
            LastError = null;
            try
            {
                await _authService.SendEmailVerificationAsync();
                return true;
            }
            catch (AuthException e)
            {
                LastError = e;
                return false;
            }
        }

        /// <summary>
        /// 이메일 인증 완료 여부를 Firebase 서버에 재쿼리한다.
        /// 완료된 경우 자동으로 UGS 브릿지를 수행한다.
        /// </summary>
        /// <returns>인증 완료 + UGS 브릿지 성공 시 true.</returns>
        public async Task<bool> CheckEmailVerifiedAsync()
        {
            LastError = null;
            try
            {
                bool verified = await _authService.CheckEmailVerifiedAsync();
                if (!verified) return false;

                // 인증 완료 → UGS 브릿지까지 진행
                await BridgeToUGSAsync(_authService.FirebaseUID);
                return true;
            }
            catch (AuthException e)
            {
                LastError = e;
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LoginUseCase] CheckEmailVerifiedAsync 중 UGS 브릿지 실패: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 비밀번호 재설정 메일을 발송한다.
        /// 보안상 가입되지 않은 이메일에도 동일하게 성공 처리한다(이메일 존재 여부 노출 방지).
        /// </summary>
        public async Task<bool> SendPasswordResetEmailAsync(string email)
        {
            LastError = null;
            try
            {
                await _authService.SendPasswordResetEmailAsync(email);
                return true;
            }
            catch (AuthException e)
            {
                // AuthSystemRules.md 규칙: 가입되지 않은 이메일도 동일 메시지("이메일을 확인하세요")
                //   → 호출자(View)가 LastError 무시하고 항상 성공 메시지를 표시.
                LastError = e;
                return false;
            }
        }

        // ====================================================================
        // 로그아웃
        // ====================================================================

        /// <summary>
        /// Firebase 세션을 종료한다.
        /// 호출자(ProfileView)는 이후 SceneManager.LoadScene("Login") 으로 이동해야 한다.
        /// </summary>
        public async Task SignOutAsync()
        {
            try
            {
                // UGS 측 세션도 정리해 둔다 — 다음 로그인 시 새 UID 로 재바인딩되도록.
                if (AuthenticationService.Instance != null &&
                    AuthenticationService.Instance.IsSignedIn)
                {
                    AuthenticationService.Instance.SignOut();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LoginUseCase] UGS SignOut 중 예외(무시): {e.Message}");
            }

            await _authService.SignOutAsync();
        }

        // ====================================================================
        // UGS 브릿지
        // ====================================================================

        /// <summary>
        /// Firebase 계정을 UGS PlayerId 와 연결한다(OIDC Bridge).
        /// 로그인 방식(익명/Google/이메일) 과 무관하게 동일하게 호출되는 후속 단계지만,
        /// 계정 종류에 따라 내부 처리가 두 갈래로 나뉜다.
        ///
        /// 1) 실계정(Google / 이메일):
        ///    Firebase 가 발급한 ID Token(JWT)을 UGS OIDC Provider("oidc-firebase")에 전달한다.
        ///    동일 Firebase 계정이면 기기를 바꿔도 항상 같은 UGS PlayerId 가 반환되므로,
        ///    랭킹·재화 등 계정 귀속 데이터를 안전하게 저장할 수 있다.
        ///    (AuthSystemRules.md — UGS 연결 규칙 2)
        ///
        /// 2) 익명 계정(Anonymous):
        ///    익명 Firebase 계정은 기기 종속이라 다기기 일관성이 애초에 불필요하고,
        ///    OIDC Provider 가 익명 토큰을 식별 주체로 인정하지 않으므로
        ///    기존대로 UGS 익명 로그인을 사용한다.
        ///    (AuthSystemRules.md — UGS 연결 규칙 3)
        ///
        /// UGS 연결 실패 시: 멀티플레이 기능만 제한되며, 로그인 자체는 성공으로 처리.
        ///   (AuthSystemRules.md 오류 처리 규칙 3 — UGS 실패가 로그인 차단으로 이어지지 않도록)
        ///
        /// 유니티 초급 개발자를 위한 안내:
        ///   - "OIDC(OpenID Connect)" 는 "한 서비스(Firebase)의 로그인 증명을
        ///     다른 서비스(UGS)가 그대로 신뢰하게 해 주는 표준 규약" 이다.
        ///   - 우리는 Firebase ID Token 을 UGS 에 보여 주고 "이 사람 Firebase 가 보증해" 라고 알려,
        ///     UGS 가 같은 사람에게 항상 같은 PlayerId 를 주도록 만든다.
        /// </summary>
        /// <param name="firebaseUID">
        /// 로그인된 사용자의 Firebase UID. 비어 있으면 로그인되지 않은 상태로 간주해 즉시 반환한다.
        /// (실제 인증에 쓰이는 값은 ID Token 이며, UID 는 유효성 가드 + 로그용으로만 사용)
        /// </param>
        public async Task BridgeToUGSAsync(string firebaseUID)
        {
            if (string.IsNullOrWhiteSpace(firebaseUID))
            {
                Debug.LogError("[LoginUseCase] BridgeToUGSAsync: firebaseUID 가 비어 있습니다.");
                return;
            }

            try
            {
                // UGS 초기화 (멱등성 — 이미 초기화되어 있으면 즉시 반환)
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    Debug.Log("[LoginUseCase] UGS 초기화 시작...");
                    await UnityServices.InitializeAsync();
                }

                // 이전 세션이 남아 있다면 정리 — 다른 Firebase 계정으로 재바인딩되는 경우 대비.
                // (예: 익명 → 로그아웃 → 다른 Google 계정으로 로그인하는 흐름)
                if (AuthenticationService.Instance.IsSignedIn)
                {
                    AuthenticationService.Instance.SignOut();
                }

                if (_authService.IsAnonymous)
                {
                    // [익명 계정] OIDC 식별 주체가 없으므로 기존 익명 로그인 유지.
                    Debug.Log("[LoginUseCase] 익명 계정 — UGS 익명 로그인 수행.");
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                else
                {
                    // [실계정] Firebase ID Token 을 UGS OIDC Provider 에 전달 → PlayerId 1:1 연결.
                    //   "oidc-firebase" 는 UGS Dashboard 에 등록한 Provider 이름이며,
                    //   UGS 규약상 OIDC Provider 이름은 "oidc-" 접두사로 시작해야 한다.
                    //   토큰은 캐시 우선(false)으로 발급 — 만료 임박 시 Firebase SDK 가 자동 갱신한다.
                    Debug.Log("[LoginUseCase] 실계정 — Firebase ID Token 으로 UGS OIDC 브릿지 수행.");
                    string firebaseToken = await _authService.GetIdTokenAsync(false);
                    await AuthenticationService.Instance.SignInWithOpenIdConnectAsync(
                        "oidc-firebase",
                        firebaseToken);
                }

                Debug.Log($"[LoginUseCase] UGS 브릿지 완료. PlayerId={AuthenticationService.Instance.PlayerId}");
            }
            catch (Exception e)
            {
                // 로그인 자체를 실패로 처리하지 않는다 — 규칙 3 참조.
                Debug.LogWarning($"[LoginUseCase] UGS 브릿지 실패(멀티플레이 제한): {e.Message}");
            }
        }
    }
}
