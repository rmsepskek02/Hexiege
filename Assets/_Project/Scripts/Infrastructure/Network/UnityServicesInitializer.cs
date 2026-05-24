// ============================================================================
// UnityServicesInitializer.cs
// Unity Gaming Services (UGS) 초기화 담당 클래스.
//
// 역할:
//   - UnityServices.InitializeAsync() 를 통한 UGS 전체 초기화
//   - 초기화 완료/실패 콜백 제공
//
// [2026-05-24 변경 — 로그인 시스템 도입]
//   기존: 익명 로그인(SignInAnonymouslyAsync) 까지 자동 수행.
//   변경: 인증은 LoginUseCase.BridgeToUGSAsync(firebaseUID) 로 위임.
//         본 클래스는 UGS 초기화만 담당하며, 인증 자체는 Firebase UID 기반
//         SignInWithCustomIdAsync 흐름이 LoginUseCase 내부에서 처리한다.
//
//   변경 이유: Firebase 와 UGS 의 사용자 ID 일관성을 위해 Firebase UID 를
//             UGS Custom ID 로 등록하는 구조로 전환 (AuthSystemRules.md).
//
// 위치: Infrastructure 레이어 — 외부 서비스 연동 담당.
// ============================================================================

using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// Unity Gaming Services 초기화 클래스.
    /// 네트워크 기능 사용 전 InitializeAsync() 호출 필요.
    /// 인증은 외부(LoginUseCase) 에서 SignInWithCustomIdAsync 로 별도 수행.
    /// </summary>
    public class UnityServicesInitializer
    {
        // ====================================================================
        // 상태 프로퍼티
        // ====================================================================

        /// <summary>UGS 초기화가 완료됐는지 여부.</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 현재 로그인 상태.
        /// Firebase UID 기반 SignInWithCustomIdAsync 가 수행된 후 true.
        /// 본 클래스는 직접 로그인을 수행하지 않으므로, 호출 시점의 상태만 반환.
        /// </summary>
        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;

        /// <summary>현재 로그인된 UGS PlayerId. 로그인 전에는 null.</summary>
        public string PlayerId => AuthenticationService.Instance.PlayerId;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// Unity Gaming Services 초기화만 수행한다.
        /// 인증(로그인)은 본 메서드에서 수행하지 않는다 — LoginUseCase.BridgeToUGSAsync() 위임.
        ///
        /// 멱등성: 이미 초기화된 상태에서 재호출해도 안전.
        /// </summary>
        /// <param name="onSuccess">UGS 초기화 성공 시 호출. 인자: 현재 PlayerId (있으면).</param>
        /// <param name="onFailure">초기화 실패 시 호출. 예외 전달.</param>
        public async Task InitializeAsync(Action<string> onSuccess = null, Action<Exception> onFailure = null)
        {
            try
            {
                // UGS 초기화 (중복 호출 안전 — 내부적으로 멱등성 보장)
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    Debug.Log("[Network] Unity Gaming Services 초기화 시작...");
                    await UnityServices.InitializeAsync();
                    Debug.Log("[Network] Unity Gaming Services 초기화 완료.");
                }
                else
                {
                    Debug.Log("[Network] Unity Gaming Services 이미 초기화됨, 스킵.");
                }

                IsInitialized = true;

                // 항상 재로그인하여 서버로부터 유효한 토큰을 발급받는다.
                // IsSignedIn이 true여도 서버 토큰이 만료되어 있으면 Lobby 등 UGS API 호출 시
                // HTTP 401 Unauthorized 에러가 발생하므로, 기존 세션을 끊고 새로 로그인한다.
                // ※ 추후 Login 씬 흐름(Firebase UID 기반 로그인)이 완성되면 이 로직 재검토 필요.
                if (AuthenticationService.Instance.IsSignedIn)
                {
                    AuthenticationService.Instance.SignOut();
                    Debug.Log("[Network] 기존 UGS 세션 로그아웃 — 토큰 갱신을 위해 재로그인 수행.");
                }
                Debug.Log("[Network] UGS 익명 로그인 수행...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

                onSuccess?.Invoke(PlayerId);
                Debug.Log($"[Network] UGS 초기화 완료. " +
                          $"SignedIn={IsSignedIn}, PlayerId={(string.IsNullOrEmpty(PlayerId) ? "(없음)" : PlayerId)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Network] UGS 초기화 실패: {e.Message}");
                onFailure?.Invoke(e);
            }
        }
    }
}
