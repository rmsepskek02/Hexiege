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
// GameLog / LogEvent 는 Application 레이어에 있다(LogRules.md 1.13 "GameLog 배치 위치").
// Infrastructure → Application 은 허용된 방향이다(Domain → Application → Core → Infrastructure → ...).
using Hexiege.Application;

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
                    // [로그 이관] LogAudit.md §3-5:76 — 축 A=Info / 축 B=개발.
                    // "[Network]" 접두사를 메시지에서 뺀 이유:
                    // GameLog 가 [System/Class] 카테고리를 앞에 붙여 주므로 남기면 이중 표기가 된다
                    // (LogRules.md 1.4 형식).
                    GameLog.Dev.Info("Network", nameof(UnityServicesInitializer), "UGS 초기화 시작");
                    await UnityServices.InitializeAsync();

                    // [로그 이관] LogAudit.md §3-5:78 — 축 A=Info / 축 B=개발.
                    // 성공 통보다. 실패했다면 아래 catch 의 운영 로그가 남는다.
                    GameLog.Dev.Info("Network", nameof(UnityServicesInitializer), "UGS 초기화 완료");
                }
                else
                {
                    // [로그 이관] LogAudit.md §3-5:82 — 축 A=Info / 축 B=개발.
                    // 재호출을 그냥 흘려보내는 의도된 멱등 경로다.
                    GameLog.Dev.Info("Network", nameof(UnityServicesInitializer), "UGS 이미 초기화됨 — 스킵");
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
                    //
                    // [로그 이관] LogAudit.md §3-5:123 — 축 A=Info / 축 B=개발.
                    // 의도된 흐름이고 에디터에서 그대로 재현되므로 축 B ①이 "아니오"다.
                    //
                    // ⚠️ 5단계(마스킹) 대상 — PlayerId 를 원본 그대로 출력하고 있다.
                    //    LogRules.md 1.6 은 UID·PlayerId 를 GameLog.HashId 로 치환하도록 규정한다.
                    //    이번 4단계(이관)에서는 값을 건드리지 않고 그대로 옮긴다.
                    //    마스킹을 파일마다 흩어서 처리하면 누락이 생기므로 5단계에서 일괄 처리한다.
                    GameLog.Dev.Info("Network", nameof(UnityServicesInitializer),
                        "기존 UGS 세션 보존 — 재로그인 생략",
                        $"PlayerId={PlayerId}");
                }
                else
                {
                    // 세션이 전혀 없는 경우(예: Login 씬을 거치지 않은 단독 테스트 진입).
                    // 멀티플레이가 최소한 동작하도록 익명 세션으로 폴백한다.
                    //
                    // [로그 이관] LogAudit.md §3-5:129 — 축 A=Warn(Info 에서 ↑승격) / 축 B=운영.
                    //
                    // 왜 Info 가 아니라 Warn 인가 (축 A — "복구되었나?"):
                    //   세션이 없다는 것 자체는 예상 밖이지만, 익명 로그인이라는 대체 경로로
                    //   계속 진행되므로 복구된 상태다 → Warn (LogRules.md 1.2 축 A).
                    //
                    // 왜 개발이 아니라 운영인가 (축 B — 두 질문 모두 "예"):
                    //   ① 릴리스에서 이 줄이 뜨면 Login 씬이 만든 OIDC 세션이 유실됐다는 뜻이고,
                    //      그 결과 PlayerId 가 통째로 바뀌어 멀티플레이 정체성이 달라진다.
                    //      개발 기기에서 재현되는 종류의 사건이 아니다.
                    //   ② 플레이어에게는 아무 통지도 가지 않아 이 로그 외에 추적 수단이 없다.
                    //
                    // Ops 메서드는 LogEvent 를 첫 인자로 반드시 받는다 — 운영 로그의 이벤트 키
                    // 생략이 컴파일 단계에서 불가능하도록 만든 구조다(LogRules.md 1.5 / 1.14 금지사항 6).
                    GameLog.Ops.Warn(LogEvent.UgsSessionMissingAnonymousFallback,
                        "Network", nameof(UnityServicesInitializer),
                        "UGS 세션 없음 — 익명 로그인으로 폴백 수행");

                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                onSuccess?.Invoke(PlayerId);

                // [로그 이관] LogAudit.md §3-5:134 — 축 A=Info / 축 B=개발.
                // 성공 통보라 축 B ①이 "아니오"다.
                // SignedIn / PlayerId 는 나중에 집계·필터링에 쓰일 값이므로 문장에 섞지 않고
                // key=value 자리로 뺀다(LogRules.md 1.4 — "key=value 가 곧 전송 데이터다").
                //
                // ⚠️ 5단계(마스킹) 대상 — 위 123행과 같은 이유로 PlayerId 원본이 그대로 나간다.
                GameLog.Dev.Info("Network", nameof(UnityServicesInitializer),
                    "UGS 초기화 완료",
                    $"SignedIn={IsSignedIn}, PlayerId={(string.IsNullOrEmpty(PlayerId) ? "(없음)" : PlayerId)}");
            }
            catch (Exception e)
            {
                // [로그 이관] LogAudit.md §3-5:139 — 축 A=Error / 축 B=운영.
                //
                // 축 A: 초기화가 실패하면 UGS 를 쓰는 기능(Lobby·Relay·멀티플레이)이 통째로
                //       죽고 이 흐름 안에 복구 경로가 없다 → Error (LogRules.md 1.3 분류 원칙 3).
                // 축 B: 실패 원인이 지역·회선·프로젝트 설정에 좌우돼 개발 기기에서 재현되지 않고,
                //       예외 정보가 원인을 알 수 있는 유일한 단서다 → 운영.
                //
                // 여기가 "최종 처리 지점"이다 — 예외 객체를 직접 손에 쥔 catch 블록
                // (LogRules.md 1.3 「원칙 간 우선순위」 ②). 이 로그가 원인을 가지고 있으므로
                // 같은 사건을 다시 남기는 상위 호출부(NetworkGameManager)는 개발로 내려간다.
                //
                // e.Message 문자열로 눌러 담지 않고 예외 객체를 그대로 넘기는 이유:
                //   예외 "타입"이 텔레메트리 집계의 핵심 축이기 때문이다. TimeoutException 300건과
                //   NullReferenceException 300건은 완전히 다른 사건인데, 메시지만 남기면
                //   그 구분이 사라진다(LogRules.md 1.9 예외 처리).
                //   메시지 본문은 sink 쪽에서 그대로 보존된다 —
                //   ConsoleSink 는 Debug.LogException 으로, FileSink 는 예외 전문을 함께 기록한다.
                GameLog.Ops.Error(LogEvent.UnityServicesInitializeFailed,
                    "Network", nameof(UnityServicesInitializer),
                    "UGS 초기화 실패", e);

                onFailure?.Invoke(e);
            }
        }
    }
}
