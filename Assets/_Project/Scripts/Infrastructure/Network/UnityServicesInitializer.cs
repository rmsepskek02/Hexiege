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
//         본 클래스는 UGS 초기화만 담당한다.
//
// [2026-06-10 변경 — OIDC Bridge 도입]
//   실계정(Google/이메일) 인증은 Firebase ID Token 을 UGS OIDC Provider 에 전달하는
//   SignInWithOpenIdConnectAsync 흐름으로 LoginUseCase 내부에서 처리한다.
//   (익명 계정은 기존대로 UGS 익명 로그인 사용 — AuthSystemRules.md UGS 연결 규칙 2~3)
//
//   변경 이유: Firebase 와 UGS 의 사용자 ID 일관성을 위해 Firebase 계정을
//             UGS PlayerId 와 1:1 로 연결하는 OIDC Bridge 구조로 전환 (AuthSystemRules.md).
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
    /// 인증은 외부(LoginUseCase.BridgeToUGSAsync)에서 OIDC Bridge / 익명 로그인으로 별도 수행.
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
        /// LoginUseCase.BridgeToUGSAsync 의 OIDC Bridge(또는 익명 로그인)가 수행된 후 true.
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

                // ----------------------------------------------------------------
                // [2026-06-10 변경 — OIDC Bridge 도입]
                //
                // 변경 이유:
                //   기존에는 "항상 SignOut → SignInAnonymouslyAsync" 로 매번 새 익명 세션을
                //   만들었다(HTTP 401 토큰 만료 버그 회피 목적). 그러나 이제 Login 씬에서
                //   Firebase 계정을 UGS OIDC 로 연결한 세션이 만들어지므로, 이 익명 재로그인은
                //   그 OIDC 세션을 덮어써 PlayerId 가 매번 바뀌는 문제를 일으킨다.
                //
                //   OIDC 전환 후에는 토큰 갱신 책임이 UGS SDK 로 넘어가(IsSignedIn 인 동안
                //   SDK 가 Access Token 을 자동 갱신) "항상 재로그인" 자체가 불필요하다.
                //
                // 새 정책:
                //   - 이미 로그인된 세션(주로 Login 씬이 만든 OIDC 세션)이 있으면 그대로 보존.
                //   - 세션이 전혀 없을 때만 폴백으로 익명 로그인.
                //     (Login 씬을 거치지 않고 Game/Lobby 씬으로 바로 진입하는 현재 개발/테스트
                //      흐름에서 Lobby/Relay 가 동작하려면 최소한의 PlayerId 가 필요하기 때문.)
                //
                // ⚠️ 기존 로직은 삭제하지 않고 아래에 주석으로 비활성화만 해 둔다.
                //    (WORKFLOW.md 기존 로직 제거 규칙 — 사용자 테스트 통과 후 최종 삭제)
                // ----------------------------------------------------------------

                // === [구 로직 — 비활성화] 항상 재로그인 ===
                // if (AuthenticationService.Instance.IsSignedIn)
                // {
                //     AuthenticationService.Instance.SignOut();
                //     Debug.Log("[Network] 기존 UGS 세션 로그아웃 — 토큰 갱신을 위해 재로그인 수행.");
                // }
                // Debug.Log("[Network] UGS 익명 로그인 수행...");
                // await AuthenticationService.Instance.SignInAnonymouslyAsync();

                // === [신 로직] OIDC 세션 보존 + 세션 없을 때만 폴백 ===
                if (AuthenticationService.Instance.IsSignedIn)
                {
                    // Login 씬이 만든 OIDC(또는 익명) 세션이 살아 있으면 그대로 사용.
                    // SDK 가 토큰을 자동 갱신하므로 덮어쓰지 않는다.
                    Debug.Log($"[Network] 기존 UGS 세션 보존 — 재로그인 생략. PlayerId={PlayerId}");
                }
                else
                {
                    // 세션이 전혀 없는 경우(예: Login 씬을 거치지 않은 단독 테스트 진입).
                    // 멀티플레이가 최소한 동작하도록 익명 세션으로 폴백한다.
                    Debug.Log("[Network] UGS 세션 없음 — 익명 로그인으로 폴백 수행.");
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

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
