// ============================================================================
// FirebaseAuthService.Google.cs
// Google Play Games 로그인과 익명 계정의 Google 연동 흐름을 모아 둔다.
// 초기화와 공통 상태는 FirebaseAuthService.cs의 같은 partial 클래스에서 공유한다.
// ============================================================================

using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
#if UNITY_ANDROID
using GooglePlayGames;
#endif
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    public partial class FirebaseAuthService
    {
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

                // UID 는 개인 식별자라 원본 대신 해시를 남긴다 (LogRules.md 1.6).
                GameLog.Dev.Info("Auth", nameof(FirebaseAuthService), "Google 로그인 성공",
                                 $"Uid={GameLog.HashId(user.UserId)}, DisplayName={user.DisplayName}");
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
                // UID 는 개인 식별자라 원본 대신 해시를 남긴다 (LogRules.md 1.6).
                GameLog.Dev.Info("Auth", nameof(FirebaseAuthService), "Google 연동 성공", $"Uid={GameLog.HashId(FirebaseUID)}");
            }
            catch (FirebaseException e)
            {
                throw ConvertException(e, "Google 연동");
            }
        }

    }
}
