// ============================================================================
// FirebaseAuthService.cs
// Firebase Authentication SDK 를 감싸는 래퍼 클래스.
//
// 역할:
//   - Firebase SDK 초기화 및 의존성 확인
//   - 익명 / Google / 이메일 3가지 로그인 흐름 제공
//   - 이메일 회원가입 + 인증 메일 발송
//   - 비밀번호 재설정 메일 발송
//   - 익명 → 실계정 연동 (LinkWithCredentialAsync)
//   - 로그아웃 처리
//   - FirebaseException → 도메인 예외(AuthException)로 변환
//
// 위치: Infrastructure 레이어 — Firebase SDK 의존을 외부와 차단하기 위한 단일 진입점.
//
// 주의사항:
//   - 반드시 InitializeAsync() 가 먼저 완료돼야 다른 API 호출 가능
//   - Google 로그인 흐름은 GooglePlayGames 플러그인이 사전에 Activate 되어 있어야 함
//     (LoginBootstrapper.Awake() 에서 PlayGamesPlatform.Activate() 1회 호출)
//
// 유니티 초급 개발자를 위한 안내:
//   - "Firebase" 는 구글이 제공하는 모바일/웹 서비스 모음으로, 우리는 그 중 "Authentication" 만 사용.
//   - "Authentication" 은 회원가입 / 로그인 / 세션 관리 기능을 우리 대신 처리해 주는 서비스.
//   - "Credential(자격증명)" 은 로그인에 필요한 증거 — Google idToken, 이메일+비밀번호 등.
//   - "익명(Anonymous) 로그인" 은 이메일/비밀번호 없이 임시 계정을 만들어 주는 방식.
//   - 모든 메서드가 async/await 인 이유: Firebase 서버와 통신하는 동안 게임이 멈추지 않도록.
// ============================================================================

using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
#if UNITY_ANDROID
using GooglePlayGames;
#endif
using UnityEngine;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// Firebase Authentication SDK 래퍼.
    /// 상위 레이어(Application/Presentation)가 Firebase SDK 에 직접 의존하지 않도록 차단한다.
    /// 모든 로그인 흐름의 단일 진입점.
    /// </summary>
    public class FirebaseAuthService
    {
        // ====================================================================
        // 내부 상태
        // ====================================================================

        /// <summary>
        /// Firebase SDK 가 초기화되었는지 여부.
        /// InitializeAsync() 가 성공한 뒤 true 가 된다.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Firebase Auth 인스턴스 캐시.
        /// FirebaseAuth.DefaultInstance 는 매번 호출해도 안전하지만,
        /// 명시적 캐시로 의도를 분명히 한다.
        /// </summary>
        private FirebaseAuth _auth;

        // ====================================================================
        // 공개 상태 프로퍼티 (현재 사용자 정보)
        // ====================================================================

        /// <summary>현재 Firebase 로그인 상태. CurrentUser 가 null 이 아니면 true.</summary>
        public bool IsLoggedIn => _auth != null && _auth.CurrentUser != null;

        /// <summary>현재 사용자가 익명 계정인지 여부.</summary>
        public bool IsAnonymous => _auth?.CurrentUser?.IsAnonymous ?? false;

        /// <summary>현재 사용자의 Firebase UID. 로그인 전이면 빈 문자열.</summary>
        public string FirebaseUID => _auth?.CurrentUser?.UserId ?? string.Empty;

        /// <summary>현재 사용자의 표시 이름. Google 로그인 시에만 채워짐. 없으면 빈 문자열.</summary>
        public string DisplayName => _auth?.CurrentUser?.DisplayName ?? string.Empty;

        /// <summary>현재 사용자의 이메일. 익명 계정이면 빈 문자열.</summary>
        public string Email => _auth?.CurrentUser?.Email ?? string.Empty;

        /// <summary>현재 사용자의 이메일 인증 완료 여부. 익명 계정은 항상 false.</summary>
        public bool IsEmailVerified => _auth?.CurrentUser?.IsEmailVerified ?? false;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// Firebase SDK 초기화. 게임 시작 시 가장 먼저 한 번 호출해야 한다.
        /// 내부적으로 Google Play 서비스 등 필수 의존성을 검사하고 필요한 경우 자동 수정한다.
        /// </summary>
        /// <returns>초기화 성공 여부.</returns>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                // CheckAndFixDependenciesAsync:
                //   Firebase 가 필요로 하는 Google Play 서비스 등을 확인하고,
                //   누락된 경우 사용자에게 설치 안내를 띄우는 표준 절차.
                DependencyStatus status = await FirebaseApp.CheckAndFixDependenciesAsync();

                if (status != DependencyStatus.Available)
                {
                    GameLog.Ops.Error(LogEvent.FirebaseDependencyUnavailable, "Auth", nameof(FirebaseAuthService),
                                      "Firebase 의존성 해결 실패", $"Status={status}");
                    return false;
                }

                // DefaultInstance 는 google-services.json 의 설정을 기반으로 생성된 기본 Auth 인스턴스.
                _auth = FirebaseAuth.DefaultInstance;
                IsInitialized = true;

                // ⚠️ 5단계(마스킹) 대상 — LogRules.md 1.6 민감 데이터
                GameLog.Dev.Info("Auth", nameof(FirebaseAuthService), "초기화 완료",
                                 IsLoggedIn ? $"HasSession=true, Uid={FirebaseUID}, Anonymous={IsAnonymous}"
                                            : "HasSession=false");
                return true;
            }
            catch (Exception e)
            {
                GameLog.Ops.Error(LogEvent.FirebaseInitializeFailed, "Auth", nameof(FirebaseAuthService),
                                  "초기화 실패 — 호출부는 사유를 받지 못한다", e);
                return false;
            }
        }

        // ====================================================================
        // 익명 로그인
        // ====================================================================

        /// <summary>
        /// 익명(임시) 계정으로 로그인한다.
        /// Firebase 서버에 새 임의 UID 를 발급받아 익명 사용자를 만든다.
        /// 이메일/비밀번호 없이 즉시 로그인 가능하지만, 앱 재설치 / 기기 변경 시
        /// 동일 계정으로 복구할 수 없다(기기 종속).
        /// </summary>
        /// <returns>발급된 Firebase UID.</returns>
        public async Task<string> SignInAnonymouslyAsync()
        {
            EnsureInitialized();
            try
            {
                // SignInAnonymouslyAsync 는 AuthResult 를 반환한다.
                AuthResult result = await _auth.SignInAnonymouslyAsync();
                FirebaseUser user = result.User;

                // ⚠️ 5단계(마스킹) 대상 — LogRules.md 1.6 민감 데이터
                GameLog.Dev.Info("Auth", nameof(FirebaseAuthService), "익명 로그인 성공", $"Uid={user.UserId}");
                return user.UserId;
            }
            catch (FirebaseException e)
            {
                throw ConvertException(e, "익명 로그인");
            }
        }

        // ====================================================================
        // Google 로그인
        // ====================================================================

        /// <summary>
        /// Google Play Games → Firebase 로 이어지는 로그인 흐름.
        /// 1) Google Play Games 플러그인으로 Google 사용자 인증
        /// 2) Server Auth Code 발급 (Firebase 에게 보낼 자격증명 토큰)
        /// 3) PlayGamesAuthProvider 로 Firebase Credential 생성
        /// 4) Firebase 에 Credential 로 로그인 → Firebase UID 획득
        /// </summary>
        /// <returns>발급된 Firebase UID.</returns>
        public async Task<string> SignInWithGoogleAsync()
        {
            EnsureInitialized();

            // 1) Google Play Games 인증 → 2) Server Auth Code 획득
            //    GPGS 콜백은 비동기 콜백 기반이므로, TaskCompletionSource 로 async/await 화한다.
            string serverAuthCode = await RequestGoogleServerAuthCodeAsync();

            if (string.IsNullOrEmpty(serverAuthCode))
            {
                throw new AuthException(AuthErrorReason.GoogleSignInCancelled,
                    "Google 로그인이 취소되었거나 인증 코드 발급에 실패했습니다.");
            }

            try
            {
                // 3) Firebase 자격증명 생성
                Credential credential = PlayGamesAuthProvider.GetCredential(serverAuthCode);

                // 4) Firebase 에 Credential 로 로그인
                // SignInWithCredentialAsync 는 이 SDK 버전에서 FirebaseUser 를 반환한다.
                // (SignInAnonymouslyAsync 등 다른 메서드는 AuthResult 반환 — 버전별 차이)
                FirebaseUser user = await _auth.SignInWithCredentialAsync(credential);

                // ⚠️ 5단계(마스킹) 대상 — LogRules.md 1.6 민감 데이터
                GameLog.Dev.Info("Auth", nameof(FirebaseAuthService), "Google 로그인 성공",
                                 $"Uid={user.UserId}, DisplayName={user.DisplayName}");
                return user.UserId;
            }
            catch (FirebaseException e)
            {
                throw ConvertException(e, "Google 로그인");
            }
        }

        /// <summary>
        /// Google Play Games 플러그인으로부터 Server Auth Code 를 발급받는다.
        /// 콜백 기반 API 를 Task 로 감싸 async/await 에서 사용할 수 있도록 한다.
        /// </summary>
        /// <returns>Server Auth Code. 실패/취소 시 빈 문자열.</returns>
        private Task<string> RequestGoogleServerAuthCodeAsync()
        {
            // TaskCompletionSource: 콜백 결과를 Task 의 결과로 전달하기 위한 표준 헬퍼.
#if !UNITY_ANDROID
            GameLog.Dev.Warn("Auth", nameof(FirebaseAuthService), "Google Play Games 로그인은 Android 에서만 지원된다");
            return Task.FromResult(string.Empty);
#else
            var tcs = new TaskCompletionSource<string>();

            // 1) GPGS 인증 (사용자가 Google 계정을 선택 / 자동 선택)
            PlayGamesPlatform.Instance.ManuallyAuthenticate(signInStatus =>
            {
                if (signInStatus != GooglePlayGames.BasicApi.SignInStatus.Success)
                {
                    GameLog.Ops.Warn(LogEvent.GooglePlayGamesAuthFailed, "Auth", nameof(FirebaseAuthService),
                                     "GPGS 인증 실패", $"Stage=SignIn, Status={signInStatus}");
                    tcs.TrySetResult(string.Empty);
                    return;
                }

                // 2) Server Auth Code 발급 요청
                //    forceRefreshToken: true → 매번 새 토큰을 발급 (서버 측 안전성 위해 권장)
                PlayGamesPlatform.Instance.RequestServerSideAccess(
                    forceRefreshToken: true,
                    authCode =>
                    {
                        if (string.IsNullOrEmpty(authCode))
                        {
                            GameLog.Ops.Warn(LogEvent.GooglePlayGamesAuthFailed, "Auth", nameof(FirebaseAuthService),
                                             "GPGS Server Auth Code 발급 실패", "Stage=ServerAuthCode");
                            tcs.TrySetResult(string.Empty);
                        }
                        else
                        {
                            GameLog.Dev.Info("Auth", nameof(FirebaseAuthService), "GPGS Server Auth Code 발급 성공");
                            tcs.TrySetResult(authCode);
                        }
                    });
            });

            return tcs.Task;
#endif
        }

        // ====================================================================
        // 이메일 로그인 / 회원가입
        // ====================================================================

        /// <summary>
        /// 이메일 + 비밀번호로 로그인한다.
        /// 이메일 인증 완료 여부는 호출자(LoginUseCase)에서 검사한다.
        /// </summary>
        /// <param name="email">이메일 주소.</param>
        /// <param name="password">평문 비밀번호. Firebase 가 내부적으로 해시 처리한다.</param>
        /// <returns>발급된 Firebase UID.</returns>
        public async Task<string> SignInWithEmailAsync(string email, string password)
        {
            EnsureInitialized();
            try
            {
                AuthResult result = await _auth.SignInWithEmailAndPasswordAsync(email, password);
                // ⚠️ 5단계(마스킹) 대상 — LogRules.md 1.6 민감 데이터
                GameLog.Dev.Info("Auth", nameof(FirebaseAuthService), "이메일 로그인 성공",
                                 $"Uid={result.User.UserId}, EmailVerified={result.User.IsEmailVerified}");
                return result.User.UserId;
            }
            catch (FirebaseException e)
            {
                throw ConvertException(e, "이메일 로그인");
            }
        }

        /// <summary>
        /// 이메일 + 비밀번호로 신규 회원가입을 진행한다.
        /// 회원가입 후 자동 로그인 상태가 되며, 호출자는 즉시 SendEmailVerificationAsync() 를
        /// 호출하여 인증 메일 발송을 시작해야 한다.
        /// </summary>
        /// <param name="email">이메일 주소.</param>
        /// <param name="password">비밀번호 (Firebase 기본 6자 이상).</param>
        /// <param name="displayName">표시 이름. null 또는 빈 문자열이면 설정하지 않음.</param>
        public async Task SignUpWithEmailAsync(string email, string password, string displayName = null)
        {
            EnsureInitialized();
            try
            {
                AuthResult result = await _auth.CreateUserWithEmailAndPasswordAsync(email, password);
                // ⚠️ 5단계(마스킹) 대상 — LogRules.md 1.6 민감 데이터
                GameLog.Dev.Info("Auth", nameof(FirebaseAuthService), "회원가입 성공", $"Uid={result.User.UserId}");

                // 표시 이름이 있으면 프로필에 설정
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    var profile = new UserProfile { DisplayName = displayName };
                    await result.User.UpdateUserProfileAsync(profile);
                }
            }
            catch (FirebaseException e)
            {
                throw ConvertException(e, "회원가입");
            }
        }

        /// <summary>
        /// 현재 로그인한 사용자에게 이메일 인증 메일을 발송한다.
        /// 회원가입 직후 또는 인증 메일 재발송 시 호출.
        /// </summary>
        public async Task SendEmailVerificationAsync()
        {
            EnsureInitialized();
            if (_auth.CurrentUser == null)
                throw new AuthException(AuthErrorReason.NotLoggedIn,
                    "로그인된 사용자가 없어 인증 메일을 보낼 수 없습니다.");

            try
            {
                await _auth.CurrentUser.SendEmailVerificationAsync();
                // ⚠️ 5단계(마스킹) 대상 — LogRules.md 1.6 민감 데이터 — 이메일은 부분 마스킹도 금지, 출력 자체를 없앤다
                GameLog.Dev.Info("Auth", nameof(FirebaseAuthService), "인증 메일 발송");
            }
            catch (FirebaseException e)
            {
                throw ConvertException(e, "인증 메일 발송");
            }
        }

        /// <summary>
        /// 현재 사용자의 이메일 인증 완료 여부를 Firebase 서버에 재쿼리한다.
        /// ReloadAsync() 후 IsEmailVerified 를 확인하므로, 외부 메일 클라이언트에서
        /// 인증 링크 클릭 후 호출하면 최신 상태가 반영된다.
        /// </summary>
        /// <returns>인증 완료 여부.</returns>
        public async Task<bool> CheckEmailVerifiedAsync()
        {
            EnsureInitialized();
            if (_auth.CurrentUser == null)
                throw new AuthException(AuthErrorReason.NotLoggedIn,
                    "로그인된 사용자가 없어 인증 상태를 확인할 수 없습니다.");

            try
            {
                // ReloadAsync: 현재 사용자 정보를 Firebase 서버에서 재조회.
                //   캐시된 로컬 상태가 아닌 최신 인증 상태를 가져온다.
                await _auth.CurrentUser.ReloadAsync();
                bool verified = _auth.CurrentUser.IsEmailVerified;
                GameLog.Dev.Info("Auth", nameof(FirebaseAuthService), "인증 완료 확인", $"Verified={verified}");
                return verified;
            }
            catch (FirebaseException e)
            {
                throw ConvertException(e, "인증 상태 확인");
            }
        }

        /// <summary>
        /// 지정 이메일로 비밀번호 재설정 링크를 발송한다.
        /// 보안상 가입되지 않은 이메일에도 동일 응답을 보낸다(이메일 존재 여부 노출 방지).
        /// </summary>
        /// <param name="email">재설정 메일을 받을 이메일 주소.</param>
        public async Task SendPasswordResetEmailAsync(string email)
        {
            EnsureInitialized();
            try
            {
                await _auth.SendPasswordResetEmailAsync(email);
                // ⚠️ 5단계(마스킹) 대상 — LogRules.md 1.6 민감 데이터 — 이메일 출력 제거 대상
                GameLog.Dev.Info("Auth", nameof(FirebaseAuthService), "비밀번호 재설정 메일 발송");
            }
            catch (FirebaseException e)
            {
                // 보안 정책: 가입되지 않은 이메일이어도 동일한 응답을 호출자에 전달.
                //   여기서는 예외만 변환하고, 표시 메시지 통합은 LoginUseCase 에서 담당.
                throw ConvertException(e, "비밀번호 재설정 메일 발송");
            }
        }

        // ====================================================================
        // 계정 연동 (익명 → 실계정)
        // ====================================================================

        /// <summary>
        /// 현재 익명 계정을 Google 계정에 연동한다.
        /// 익명 계정의 Firebase UID 가 그대로 유지되므로 UGS 재연결 불필요.
        /// </summary>
        public async Task LinkWithGoogleAsync()
        {
            EnsureInitialized();
            if (_auth.CurrentUser == null || !_auth.CurrentUser.IsAnonymous)
                throw new AuthException(AuthErrorReason.NotAnonymous,
                    "익명 계정만 연동할 수 있습니다.");

            string serverAuthCode = await RequestGoogleServerAuthCodeAsync();
            if (string.IsNullOrEmpty(serverAuthCode))
                throw new AuthException(AuthErrorReason.GoogleSignInCancelled,
                    "Google 인증이 취소되었거나 실패했습니다.");

            try
            {
                Credential credential = PlayGamesAuthProvider.GetCredential(serverAuthCode);
                await _auth.CurrentUser.LinkWithCredentialAsync(credential);
                // ⚠️ 5단계(마스킹) 대상 — LogRules.md 1.6 민감 데이터
                GameLog.Dev.Info("Auth", nameof(FirebaseAuthService), "Google 연동 성공", $"Uid={FirebaseUID}");
            }
            catch (FirebaseException e)
            {
                throw ConvertException(e, "Google 연동");
            }
        }

        /// <summary>
        /// 현재 익명 계정을 이메일 계정에 연동한다.
        /// 연동 성공 시 자동으로 이메일 인증 메일을 발송한다.
        /// </summary>
        /// <param name="email">연동할 이메일 주소.</param>
        /// <param name="password">신규 비밀번호.</param>
        public async Task LinkWithEmailAsync(string email, string password)
        {
            EnsureInitialized();
            if (_auth.CurrentUser == null || !_auth.CurrentUser.IsAnonymous)
                throw new AuthException(AuthErrorReason.NotAnonymous,
                    "익명 계정만 연동할 수 있습니다.");

            try
            {
                Credential credential = EmailAuthProvider.GetCredential(email, password);
                await _auth.CurrentUser.LinkWithCredentialAsync(credential);
                // ⚠️ 5단계(마스킹) 대상 — LogRules.md 1.6 민감 데이터 — UID 해시 치환 + 이메일 출력 제거
                GameLog.Dev.Info("Auth", nameof(FirebaseAuthService), "이메일 연동 성공", $"Uid={FirebaseUID}, Email={email}");

                // 이메일 연동 직후 인증 메일 발송 — 호출자가 별도 호출하지 않아도 되도록 일관성 유지.
                await _auth.CurrentUser.SendEmailVerificationAsync();
            }
            catch (FirebaseException e)
            {
                throw ConvertException(e, "이메일 연동");
            }
        }

        // ====================================================================
        // 로그아웃
        // ====================================================================

        /// <summary>
        /// 현재 Firebase 세션을 종료한다.
        /// FirebaseAuth.SignOut() 은 동기 메서드지만, 인터페이스 일관성을 위해 Task 반환.
        /// 세션 종료 후 호출자는 Login 씬으로 이동해야 한다.
        /// </summary>
        public Task SignOutAsync()
        {
            EnsureInitialized();
            _auth.SignOut();
            GameLog.Dev.Info("Auth", nameof(FirebaseAuthService), "로그아웃 완료");
            return Task.CompletedTask;
        }

        public async Task DeleteCurrentUserAsync()
        {
            EnsureInitialized();
            if (_auth.CurrentUser == null)
                throw new AuthException(AuthErrorReason.NotLoggedIn,
                    "로그인된 사용자가 없어 계정을 삭제할 수 없습니다.");

            try
            {
                string uid = _auth.CurrentUser.UserId;
                await _auth.CurrentUser.DeleteAsync();
                // ⚠️ 5단계(마스킹) 대상 — LogRules.md 1.6 민감 데이터
                // 잠정 판정: 개발. 되돌릴 수 없는 조작이라 감사 기록 요구가 축 판정을 덮을 수 있다(질의 Q-4).
                GameLog.Dev.Info("Auth", nameof(FirebaseAuthService), "계정 삭제 완료", $"Uid={uid}");
            }
            catch (FirebaseException e)
            {
                throw ConvertException(e, "계정 삭제");
            }
        }

        // ====================================================================
        // ID Token 발급 (UGS OIDC 브릿지용)
        // ====================================================================

        /// <summary>
        /// 현재 로그인된 Firebase 사용자의 ID Token(JWT)을 발급한다.
        /// 이 토큰을 UGS 의 SignInWithOpenIdConnectAsync 에 전달하면
        /// Firebase 계정과 UGS PlayerId 를 1:1 로 연결할 수 있다.
        ///
        /// 유니티 초급 개발자를 위한 안내:
        ///   - "ID Token" 은 "이 사용자가 누구인지" 를 Firebase 가 서명해 발급한 증명서(JWT 문자열)다.
        ///     UGS 는 이 증명서를 검증해 동일 Firebase 계정이면 항상 같은 PlayerId 를 돌려준다.
        ///   - 이 메서드는 Firebase SDK 의 TokenAsync 를 직접 호출하는 유일한 지점이다.
        ///     상위 레이어(LoginUseCase)가 Firebase SDK 에 의존하지 않도록 여기서 캡슐화한다.
        /// </summary>
        /// <param name="forceRefresh">
        /// true 면 캐시를 무시하고 서버에서 새 토큰을 강제로 받아온다.
        /// false(기본)면 캐시된 토큰을 우선 사용하되 만료가 임박하면 SDK 가 자동 갱신한다.
        /// 일반적인 브릿지에는 false 로 충분하다(불필요한 서버 왕복 방지).
        /// </param>
        /// <returns>Firebase ID Token(JWT 문자열).</returns>
        public async Task<string> GetIdTokenAsync(bool forceRefresh = false)
        {
            EnsureInitialized();
            if (_auth.CurrentUser == null)
                throw new AuthException(AuthErrorReason.NotLoggedIn,
                    "로그인된 사용자가 없어 ID Token 을 발급할 수 없습니다.");

            try
            {
                // TokenAsync(false): 캐시된 토큰 우선, 만료 임박 시 SDK 가 알아서 갱신.
                string token = await _auth.CurrentUser.TokenAsync(forceRefresh);
                GameLog.Dev.Info("Auth", nameof(FirebaseAuthService), "ID Token 발급 완료 (UGS OIDC 브릿지용)");
                return token;
            }
            catch (FirebaseException e)
            {
                throw ConvertException(e, "ID Token 발급");
            }
        }

        // ====================================================================
        // 내부 헬퍼
        // ====================================================================

        /// <summary>
        /// 초기화 검증. InitializeAsync() 호출 전 다른 API 사용 시 즉시 예외 발생.
        /// </summary>
        private void EnsureInitialized()
        {
            if (!IsInitialized || _auth == null)
                throw new AuthException(AuthErrorReason.NotInitialized,
                    "FirebaseAuthService 가 초기화되지 않았습니다. InitializeAsync() 를 먼저 호출하세요.");
        }

        /// <summary>
        /// FirebaseException 을 도메인 예외(AuthException)로 변환한다.
        /// Firebase SDK 의 ErrorCode(int) 를 AuthError(enum) 로 캐스팅한 뒤 의미별로 분기.
        /// </summary>
        private AuthException ConvertException(FirebaseException e, string operation)
        {
            // FirebaseException.ErrorCode 는 int → AuthError(enum) 로 캐스팅 가능.
            AuthError code = (AuthError)e.ErrorCode;

            AuthErrorReason reason = code switch
            {
                AuthError.WrongPassword => AuthErrorReason.InvalidEmailOrPassword,
                AuthError.UserNotFound => AuthErrorReason.InvalidEmailOrPassword,
                AuthError.InvalidEmail => AuthErrorReason.InvalidEmail,
                AuthError.EmailAlreadyInUse => AuthErrorReason.EmailAlreadyInUse,
                AuthError.WeakPassword => AuthErrorReason.WeakPassword,
                AuthError.CredentialAlreadyInUse => AuthErrorReason.CredentialAlreadyInUse,
                AuthError.NetworkRequestFailed => AuthErrorReason.NetworkError,
                AuthError.TooManyRequests => AuthErrorReason.TooManyRequests,
                AuthError.RequiresRecentLogin => AuthErrorReason.RequiresRecentLogin,
                _ => AuthErrorReason.Unknown
            };

            string message = ResolveUserMessage(reason);
            GameLog.Ops.Warn(LogEvent.FirebaseAuthOperationFailed, "Auth", nameof(FirebaseAuthService),
                             "Firebase 인증 작업 실패", e, $"Operation={operation}, Code={code}, Reason={reason}");
            return new AuthException(reason, message, e);
        }

        /// <summary>
        /// AuthErrorReason 을 사용자에게 보여줄 한국어 메시지로 변환한다.
        /// AuthSystemRules.md 의 표시 메시지 규칙을 반영.
        /// </summary>
        private string ResolveUserMessage(AuthErrorReason reason) => reason switch
        {
            AuthErrorReason.InvalidEmailOrPassword =>
                "이메일 또는 비밀번호가 올바르지 않습니다.",
            AuthErrorReason.InvalidEmail =>
                "이메일 형식이 올바르지 않습니다.",
            AuthErrorReason.EmailAlreadyInUse =>
                "이미 사용 중인 이메일입니다.",
            AuthErrorReason.WeakPassword =>
                "비밀번호는 6자 이상이어야 합니다.",
            AuthErrorReason.CredentialAlreadyInUse =>
                "이미 다른 계정에 연동된 정보입니다.",
            AuthErrorReason.NetworkError =>
                "네트워크 설정을 확인하고 다시 시도하세요.",
            AuthErrorReason.TooManyRequests =>
                "요청이 너무 잦습니다. 잠시 후 다시 시도하세요.",
            AuthErrorReason.RequiresRecentLogin =>
                "보안을 위해 다시 로그인한 뒤 계정 삭제를 시도해 주세요.",
            AuthErrorReason.GoogleSignInCancelled =>
                "Google 로그인이 취소되었습니다.",
            AuthErrorReason.NotInitialized =>
                "인증 서비스가 초기화되지 않았습니다.",
            AuthErrorReason.NotLoggedIn =>
                "로그인이 필요한 작업입니다.",
            AuthErrorReason.NotAnonymous =>
                "익명 계정에서만 가능한 작업입니다.",
            _ => "일시적인 오류가 발생했습니다. 잠시 후 다시 시도하세요."
        };
    }

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
