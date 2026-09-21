// ============================================================================
// InternetReachabilityProbe.cs
// 「내 인터넷이 실제로 되는가」를 상대와 무관한 제3자에게 실제 요청을 보내 확인한다.
//
// [초급자용 설명 — 왜 이런 것이 필요한가]
//   네트워크 대전에서 「상대의 신호가 끊겼다」만 가지고는 누구 문제인지 알 수 없다.
//   상대가 앱을 강제 종료했을 수도 있고, 내 와이파이가 끊겼을 수도 있다.
//   둘은 화면에서 똑같이 보이지만 해야 할 일이 정반대다.
//     - 상대 문제 → 상대 이탈로 확정하고 연결을 정리한다
//     - 내 문제   → 아무것도 확정하지 않고 회선이 돌아오기를 기다린다
//   구분하는 방법은 하나다 — 「상대와 아무 상관 없는 곳」에 요청을 보내 본다.
//   그 요청이 성공하면 내 인터넷은 멀쩡하다는 뜻이고, 그러면 조용한 쪽은 상대다.
//
// [왜 UnityEngine.Application.internetReachability 를 쓰지 않는가]
//   ⚠️ 그 프로퍼티는 **경로(와이파이/데이터가 켜져 있는가)**
//   만 알려 주고 **실제 통신이 되는지는 알려 주지 않는다.** 와이파이에 붙어 있지만
//   인터넷이 나가 있는 공유기, 로그인이 필요한 공공 와이파이가 대표적인 반례다.
//   그래서 이 클래스는 **실제 요청을 한 번 보내는** 방식을 쓴다
//   (근거: TechnicalDesignDocument.md 「이탈 판정 절차」의 기술 사실 항목).
//
// [무엇에 요청을 보내는가 — UGS Cloud Save]
//   이미 이 프로젝트가 쓰고 있는 UGS(Unity Gaming Services) 가운데 Cloud Save 를 골랐다.
//     - 상대(Relay 로 연결된 그 사람)와 아무 관계가 없는 서버다 → 판정 대상이 오염되지 않는다
//     - 이미 로그인·초기화가 끝나 있어 새로 붙일 인증 절차가 없다
//     - 읽기 요청이라 **게임 데이터를 바꾸지 않는다**(부작용 없음)
//   같은 목적이라면 Lobby·Relay 도 후보였다(TechnicalDesignDocument.md 「이탈 판정 절차」).
//   Cloud Save 를 고른 이유는 **이 프로젝트에 이미 같은 호출 선례가 있어**
//   (Infrastructure/Cloud/PlayerProfileService.LoadProfileAsync) API 사용법이 검증된 쪽이라는 것이다.
//
// 🔴 [가장 중요한 원칙 — 모르면 「안 된다」 쪽으로 답한다]
//   이 확인의 결과는 「연결을 끊을지」를 가른다. 그래서 확실하지 않을 때는 반드시
//   Reachable 이 아닌 값을 돌려준다. 요청이 실패한 이유가 인터넷 단절인지 서버 장애인지
//   요청 한 번으로는 알 수 없고, **잘못 Reachable 이라고 답하면 돌아올 수 있었던 연결을
//   우리 스스로 끊어 버리기** 때문이다(GameSystemRules_RandomMap.md 규칙 17 ②).
//
// 재사용:
//   이 확인기는 결과 화면 이탈 판정(단계 6)에서 처음 만들었지만, 나중에 전적 기록
//   (이탈 시 승패 처리)에서도 같은 절차를 쓴다. 그래서 호출부에 묶지 않고 독립 클래스로 뒀다.
//   ⚠️ MonoBehaviour 가 아닌 일반 C# 클래스다(PlayerProfileService 와 같은 방식).
//
// Infrastructure 레이어 — 외부 서비스(UGS) 연동 담당.
// ============================================================================

using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
// GameLog 는 Application 레이어에 있다(LogRules.md 1.13). Infrastructure → Application 은 허용 방향이다.
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 인터넷 도달 확인 결과. 🔴 <b>Reachable 이 아닌 값은 모두 「확인하지 못했다」</b>이며,
    /// 호출부는 그 두 값을 구분하지 않고 똑같이 「연결을 끊지 않는다」로 처리해야 한다.
    /// </summary>
    public enum InternetReachabilityResult
    {
        /// <summary>요청이 실제로 성공했다 — 내 인터넷은 되고 있다.</summary>
        Reachable = 0,

        /// <summary>
        /// 요청을 보냈지만 실패했다.
        /// ⚠️ 이것은 「인터넷이 확실히 죽었다」가 아니라 <b>「되는지 확인하지 못했다」</b>는 뜻이다.
        /// 회선 단절 · UGS 장애 · 요청 제한(rate limit) 이 모두 여기로 들어온다.
        /// </summary>
        Unreachable = 1,

        /// <summary>
        /// 요청을 보낼 조건 자체가 아니었다(UGS 미초기화 · 로그인 전).
        /// 아무 요청도 나가지 않았으므로 인터넷 상태에 대해 <b>아무것도 말하지 않는다</b>.
        /// </summary>
        Skipped = 2
    }

    /// <summary>
    /// UGS Cloud Save 에 실제 읽기 요청을 한 번 보내 「내 인터넷이 되는가」를 확인한다.
    /// </summary>
    public class InternetReachabilityProbe
    {
        // ====================================================================
        // 설정값 (코드 상수 — Inspector 로 빼지 않는다)
        // ====================================================================
        //
        // ⚠️ 왜 [SerializeField] 로 만들지 않았는가: 이 클래스는 MonoBehaviour 가 아니라
        //    씬에 직렬화될 대상이 아니다. 그리고 이 프로젝트에서는 **씬에 저장된 값이
        //    코드 기본값을 이깁니다** — 한 번 씬에 저장되면 코드를 고쳐도 동작이 바뀌지 않아
        //    같은 함정에 여러 번 걸린 이력이 있다. 값을 바꿀 이유가 생기면 코드를 고친다.

        /// <summary>
        /// 요청 하나를 기다리는 한도(초). 이 시간을 넘기면 실패(Unreachable)로 본다.
        ///
        /// 🔴 왜 한도가 필요한가: 회선이 끊긴 상태에서는 요청이 성공도 실패도 하지 않고
        ///    <b>그냥 오래 매달려 있을 수 있다.</b> 한도가 없으면 판정이 영원히 미뤄진다.
        /// </summary>
        private const int ProbeTimeoutMilliseconds = 10000;

        /// <summary>
        /// 도달 확인용으로 읽어 볼 Cloud Save 키. 값이 있든 없든 상관없다 —
        /// 확인하려는 것은 <b>응답이 오는가</b>이지 내용이 아니다.
        /// (키 이름은 PlayerProfileService 가 쓰는 "nickname" 과 같은 것을 재사용한다.
        ///  새 키를 만들면 UGS Dashboard 스키마와 어긋날 수 있다.)
        /// </summary>
        private const string ProbeKey = "nickname";

        // ====================================================================
        // 공개 API
        // ====================================================================

        /// <summary>
        /// 내 인터넷이 실제로 되는지 확인한다. <b>예외를 던지지 않는다</b> —
        /// 모든 실패는 <see cref="InternetReachabilityResult"/> 값으로 돌려준다.
        ///
        /// 🔴 호출부는 <see cref="InternetReachabilityResult.Reachable"/> 일 때만
        ///    「내 인터넷은 멀쩡하다」로 읽어야 한다. 나머지 두 값은 똑같이 「모른다」다.
        /// </summary>
        /// <returns>도달 확인 결과.</returns>
        public async Task<InternetReachabilityResult> CheckAsync()
        {
            // ----------------------------------------------------------------
            // 1단계: 요청을 보낼 수 있는 상태인지 먼저 본다.
            //   UGS 초기화 전이거나 로그인 전이면 Cloud Save 호출 자체가 예외를 던진다.
            //   그 예외는 「인터넷이 안 된다」와 아무 상관이 없으므로, 요청을 보내기도 전에
            //   Skipped 로 갈라 둔다 — 그래야 로그를 읽는 사람이 원인을 혼동하지 않는다.
            // ----------------------------------------------------------------
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                GameLog.Dev.Warn("Network", nameof(InternetReachabilityProbe),
                    "도달 확인을 건너뛴다 — UGS 가 초기화되지 않았다",
                    $"ServicesState={UnityServices.State}");
                return InternetReachabilityResult.Skipped;
            }

            // AuthenticationService.Instance 접근 자체가 초기화 전에는 예외를 던질 수 있어
            // try 로 감싼다(위 State 검사를 통과했다면 보통은 안전하다).
            try
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    GameLog.Dev.Warn("Network", nameof(InternetReachabilityProbe),
                        "도달 확인을 건너뛴다 — UGS 로그인 상태가 아니다");
                    return InternetReachabilityResult.Skipped;
                }
            }
            catch (System.Exception e)
            {
                GameLog.Dev.Warn("Network", nameof(InternetReachabilityProbe),
                    "도달 확인을 건너뛴다 — 로그인 상태를 읽지 못했다",
                    $"Exception={e.GetType().Name}");
                return InternetReachabilityResult.Skipped;
            }

            // ----------------------------------------------------------------
            // 2단계: 실제 요청을 보낸다.
            //   키 하나만 읽어 응답 크기를 최소로 한다. 값이 없어도 실패가 아니다.
            // ----------------------------------------------------------------
            try
            {
                var keys = new HashSet<string> { ProbeKey };
                Task loadTask = CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                // ⚠️ 왜 Task.Delay 와 경쟁시키는가: 회선이 끊긴 상태에서는 요청이 아주 오래
                //    매달려 있을 수 있다. 먼저 끝나는 쪽을 택해 한도를 만든다.
                //    (LoadAsync 에 취소 토큰을 넘기는 방법은 SDK 버전에 따라 시그니처가 달라
                //     이 프로젝트에서 검증된 적이 없어 쓰지 않았다.)
                Task finished = await Task.WhenAny(loadTask, Task.Delay(ProbeTimeoutMilliseconds));

                if (finished != loadTask)
                {
                    GameLog.Dev.Warn("Network", nameof(InternetReachabilityProbe),
                        "도달 확인 실패 — 한도 안에 응답이 오지 않았다",
                        $"TimeoutMs={ProbeTimeoutMilliseconds}");
                    return InternetReachabilityResult.Unreachable;
                }

                // 요청이 끝났더라도 실패로 끝났을 수 있다. 예외를 여기서 다시 꺼낸다.
                // (await 하지 않으면 예외가 조용히 묻힌다 — LogRules 원칙 4 「삼킨 예외」와 같은 취지)
                await loadTask;

                GameLog.Dev.Info("Network", nameof(InternetReachabilityProbe),
                    "도달 확인 성공 — 내 인터넷은 되고 있다");
                return InternetReachabilityResult.Reachable;
            }
            catch (System.Exception e)
            {
                // 🔴 예외의 종류로 「인터넷 단절」과 「서버 장애」를 가르지 않는다.
                //    한 번의 요청으로는 구분할 수 없고, 잘못 성공으로 읽으면 멀쩡한 연결을
                //    스스로 끊게 된다. 그래서 전부 Unreachable(= 확인 못 했다)로 모은다.
                GameLog.Dev.Warn("Network", nameof(InternetReachabilityProbe),
                    "도달 확인 실패 — 요청이 예외로 끝났다",
                    $"Exception={e.GetType().Name}, Message={e.Message}");
                return InternetReachabilityResult.Unreachable;
            }
        }
    }
}
