// ============================================================================
// UnityServicesInitializer.cs
// Unity Gaming Services (UGS) 초기화 담당 클래스.
//
// 역할:
//   - UnityServices.InitializeAsync() 를 통한 UGS 전체 초기화
//   - AuthenticationService 를 통한 익명 로그인 처리
//   - 이미 로그인된 상태라면 재로그인 스킵
//   - 초기화 완료/실패 콜백 제공
//
// 위치: Infrastructure 레이어 — 외부 서비스 연동 담당
// ============================================================================

using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// Unity Gaming Services 초기화 및 익명 인증 처리.
    /// 네트워크 기능을 사용하기 전에 반드시 InitializeAsync() 를 호출해야 함.
    /// </summary>
    public class UnityServicesInitializer
    {
        // ====================================================================
        // 상태 프로퍼티
        // ====================================================================

        /// <summary>UGS 초기화가 완료됐는지 여부.</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>익명 로그인이 완료됐는지 여부.</summary>
        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;

        /// <summary>현재 로그인된 플레이어 ID. 로그인 전에는 null.</summary>
        public string PlayerId => AuthenticationService.Instance.PlayerId;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// Unity Gaming Services 를 초기화하고 익명 로그인을 수행.
        /// 이미 초기화/로그인된 상태라면 해당 단계를 스킵.
        /// </summary>
        /// <param name="onSuccess">초기화 성공 시 호출. playerId 전달.</param>
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

                // 익명 로그인
                await SignInAnonymouslyAsync();

                onSuccess?.Invoke(PlayerId);
                Debug.Log($"[Network] 초기화 완료. PlayerId: {PlayerId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Network] 초기화 실패: {e.Message}");
                onFailure?.Invoke(e);
            }
        }

        // ====================================================================
        // 내부 메서드
        // ====================================================================

        /// <summary>
        /// 익명 로그인 수행. 이미 로그인된 경우 스킵.
        /// </summary>
        private async Task SignInAnonymouslyAsync()
        {
            if (AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log($"[Network] 이미 로그인됨. PlayerId: {PlayerId}");
                return;
            }

            Debug.Log("[Network] 익명 로그인 시도...");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"[Network] 익명 로그인 완료. PlayerId: {PlayerId}");
        }
    }
}
