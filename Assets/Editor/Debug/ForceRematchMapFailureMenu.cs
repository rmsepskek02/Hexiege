// ============================================================================
// ForceRematchMapFailureMenu.cs  (에디터 전용 · 개발용 강제 실패 토글 메뉴)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  1) Hexiege > Debug > Failure에서 **어느 회차를 실패시킬지** 먼저 고른다:   │
// │       · 다음 재경기   (결과 화면의 재경기)                                 │
// │       · 다음 최초 경기 (로비에서 두 명이 모인 직후)                         │
// │     두 묶음 모두 ① 연결 끊김 / ② 해시 불일치 / ③ 응답 없음 을 고를 수 있다.  │
// │     (④ 수락 전송 예외는 재경기 묶음에만 있다.)                              │
// │  2) 재경기 묶음: 멀티플레이를 시작해 경기를 끝내고 **재경기**를 누른다.       │
// │     → ①②③ 은 그 회차의 맵 준비·전송이 고른 사유로 반드시 실패한다.          │
// │     → ④ 는 **맵 단계에 가기도 전에** 수락 전송이 예외로 끝난다.             │
// │     최초 경기 묶음: 방을 만들고 상대가 접속하면 곧바로 실패한다.             │
// │  3) 🔴 **한 번 쓰이면 자동으로 꺼진다.** 다시 실패시키려면 또 고른다.        │
// │     예약 칸은 하나라 다른 회차로 다시 고르면 앞의 예약을 덮어쓴다.           │
// │  4) Hexiege > Debug > Failure > 현재 설정 확인을 누르면 예약을 확인한다.    │
// │     끄고 싶으면 같은 Failure 메뉴의 「해제」를 누른다.                     │
// └────────────────────────────────────────────────────────────────────────┘
//
// 🔴 **저장소가 둘이라는 것을 사용자가 알 필요는 없다.**
//   ①②③ 은 맵 전송 계층의 오류 코드(ForcedMapTransferFailure)를,
//   ④ 는 수락 발신 예외 토글(ForcedAcceptSendFailure)을 각각 저장한다. 종류가 다르기 때문이다.
//   ⚠️ 그래서 **「현재 설정 확인」·「해제」는 반드시 둘을 함께 다룬다** —
//      따로 켜지고 따로 꺼지면 사용자는 지금 무엇이 걸려 있는지 알 수 없게 된다.

// 🔴 **[2026-09-28 추가] 저장소가 셋이 됐다 — 위 문단의 「둘」은 더 이상 사실이 아니다.**
//   세 번째는 **싱글플레이 맵 준비**를 강제 실패시키는 토글(ForcedMapPreparationFailure)이다.
//   ⚠️ 위 ①~④ 는 전부 **멀티플레이**용이라 싱글 경로에 닿지 않는다.
//   ┌─ 싱글 항목 사용법 ─────────────────────────────────────────────────────┐
//   │  1) Hexiege > Debug > Failure > 싱글 맵 준비 (다음 1회)                 │
//   │  2) 그 뒤에 **먼저 오는 쪽**이 실패한다.                                │
//   │     · 로비에서 싱글 경기를 시작하면 → 최초 경기 실패 화면(모달 팝업)     │
//   │     · 결과 화면에서 「다시하기」를 누르면 → 결과 화면이 되살아나고        │
//   │       상태 줄에 실패가 한 줄로 뜬다                                      │
//   │  3) 🔴 한 번 쓰이면 자동으로 꺼진다 — 둘을 다 보려면 각각 한 번씩 예약.  │
//   └────────────────────────────────────────────────────────────────────────┘
//   🔴 **「현재 설정 확인」·「해제」는 셋을 함께 다룬다** — 위 ⚠️ 와 같은 이유이며,
//      저장소가 늘어도 사용자가 보는 확인·해제는 **한 곳뿐**이어야 한다.
//   왜 이 도구가 필요한지의 단일 소스는
//   Assets/_Project/Scripts/Bootstrap/Debug/ForcedMapPreparationFailure.cs 머리말이다.
//
// 🔴 **[2026-09-29 추가] 저장소가 넷이 됐다 — 위 두 문단의 「둘」·「셋」은 더 이상 사실이 아니다.**
//   네 번째는 싱글플레이의 **맵 투영**(만든 맵을 격자에 새기는 단계)을 강제 실패시키는 토글이다.
//   ⚠️ 싱글 맵에는 이제 항목이 **둘**이며, 서로 **다른 단계**를 실패시킨다.
//   ┌─ 싱글 두 항목의 차이 (초급자용) ───────────────────────────────────────┐
//   │  · 「싱글 맵 준비」 — 1단계. 이번 판의 설계도를 **만드는** 데 실패한다.  │
//   │  · 「싱글 맵 투영」 — 2단계. 만든 설계도를 격자에 **새기는** 데 실패한다.│
//   │    메뉴: Hexiege > Debug > Failure > 싱글 맵 투영 (다음 1회)            │
//   │  🔴 1단계를 실패시키면 2단계는 **아예 불리지 않는다.** 그래서 2단계를    │
//   │     보려면 반드시 「투영」 항목을 따로 예약해야 한다 — 준비 항목으로는   │
//   │     이 단계를 한 번도 태울 수 없다.                                     │
//   │  ⚠️ 화면에 뜨는 것은 두 단계가 **완전히 같다**(규칙 M-4 가 사유를 문구로 │
//   │     가르지 않는다). 로그의 사유 문장으로만 어느 단계인지 구별된다.      │
//   └────────────────────────────────────────────────────────────────────────┘
//   🔴 **「현재 설정 확인」·「해제」는 넷을 함께 다룬다** — 이유는 위와 같다.
//   왜 이 도구가 필요한지의 단일 소스는
//   Assets/_Project/Scripts/Bootstrap/Debug/ForcedMapProjectionFailure.cs 머리말이다.
//
// 🔴 **[2026-10-12 추가] 「다음 최초 경기」 묶음이 생겼다 — 같은 저장소(맵 전송 사유)에 대상 회차만 더했다.**
//   무엇을 위한 것인가: 멀티플레이 **최초 경기** 맵 준비가 실패했을 때 방장(호스트) 화면에 뜨는
//   **모달 팝업**(공통 UI 규칙 M-2 · 재시도 / 로비)을 눈으로 확인하기 위해서다. 종전의 강제 실패는
//   재경기 전용이라 이 화면을 한 번도 볼 수 없었다.
//   ┌─ 최초 경기 묶음 사용법 (초급자용) ─────────────────────────────────────┐
//   │  1) **에디터가 호스트(방을 만드는 쪽)** 여야 한다 — 예약은 이 PC 의 에디터에만 저장되고,  │
//   │     맵 준비는 호스트에서만 일어나기 때문이다.                                         │
//   │  2) Hexiege > Debug > Failure > 다음 최초 경기 아래 항목 하나를 고른다.            │
//   │  3) 에디터에서 방을 만들고 다른 기기(또는 다른 에디터)로 그 방에 접속한다.            │
//   │     → 두 명이 모인 직후 호스트 화면에 확인/취소 모달이 떠야 한다.                     │
//   │  4) ③ 응답 없음만 즉시 실패가 아니라 약 20초 뒤에 뜬다(그동안 로딩 화면이 유지된다).  │
//   └──────────────────────────────────────────────────────────────────────────┘
//   🔴 **재경기 묶음과 최초 경기 묶음은 서로를 막지 않는다.** 예약에 대상 회차가 함께 저장되고,
//      대상이 아닌 회차는 예약을 건드리지 않고 지나가기 때문이다. 재경기를 보려면 최초 경기를
//      **정상으로** 한 판 시작해야 하므로 그때는 재경기 묶음을 고른다.
//   ⚠️ 저장소는 늘지 않았다(여전히 넷). 그래서 「현재 설정 확인」·「해제」는 코드 변경 없이 새 예약도
//      함께 다루고, 확인 메뉴는 대상 회차를 추가로 보여 준다.
//
// 무엇을 위한 것인가:
//   맵 준비가 **실패했을 때의 화면**을 실제로 눈으로 확인하기 위한 도구다 — 재경기는 결과 화면
//   (공통 UI 규칙 M-3 · D-7 · 규칙 18), 최초 경기는 로비의 모달 팝업(규칙 M-2). 정상 경로에서는
//   실패가 나지 않으므로 이 토글이 없으면 그 화면을 한 번도 볼 수 없다.
//   근거 문서: Assets/_Project/Docs/_Tasks/2026-09-16/06_27_post-game-leave-ui/Plan.md §8
//
// 🔴 왜 「네트워크를 끊는 테스트 버튼」이 아닌가:
//   · 맵 전송 구간이 실측 99~134ms 라 **사람이 그 창에 버튼을 맞춰 누를 수 없다.**
//   · 연결을 끊는 방식으로는 사유가 `Disconnected` 하나만 나오는데,
//     규칙 18 이 **사유에 따라 문구를 가르라**고 정했으므로 나머지 사유도 봐야 한다.
//
// 🔴 ④ 수락 전송 예외는 왜 「사유」가 아니라 별 항목인가:
//   ①②③ 은 **맵을 주고받는 단계**의 실패다. ④ 는 그보다 앞 —
//   「수락 신호를 서버로 보내다가 예외가 났다」는, 전송이 시작되기도 전의 사건이다.
//   그래서 값의 종류가 달라(error code 가 아니다) 저장소를 따로 두었다.
//   🔴 **확인하는 것은 「양쪽 화면이 어떻게 갈리는가」다** —
//     · 수락을 누른 쪽 : 실패 화면이 뜬다(예외를 catch 가 받아 실패를 로컬 발행한다).
//     · 요청한 쪽      : **아무것도 모른다.** 신호가 서버에 닿지 않았으므로
//                        「요청 중...」이 그대로 남고 자기 자동 복귀 타이머로 로비에 간다.
//   상세한 근거는 Assets/_Project/Scripts/Infrastructure/Debug/ForcedAcceptSendFailure.cs 머리말.
//
// 🔴 맵 전송 사유가 셋뿐인 이유(그 이상 넣지 말 것):
//   · Disconnected    — 규칙 18 이 문구를 가르는 **유일한 기준**이라 반드시 필요하다.
//   · HashMismatch    — 「그 외」 사유의 대표. 즉시 실패 경로를 확인한다.
//                       (미지원 버전·용량 초과·역직렬화 실패·공정성 검증 실패는
//                        **화면에서 이것과 구별되지 않는다** — 기준이 연결 끊김 하나뿐이므로.)
//   · ResponseTimeout — 규칙 16 의 **재전송 1회**를 거치는 유일한 경로. 결과 화면의
//                       「맵 준비 한도」가 정말 **두 창**을 재는지 확인하려면 필요하다.
//
// 저장 위치·자동 해제·릴리스 빌드에서 사라지는 이유는 이 메뉴가 조작하는 대상인
//   Assets/_Project/Scripts/Infrastructure/Debug/ForcedMapTransferFailure.cs
// 의 파일 머리말에 적어 두었다(한 사실을 두 곳에 적지 않는다).
//
// 주의:
//   - Assets/Editor/ 하위이므로 자동으로 에디터 전용 컴파일 — 빌드에 포함되지 않는다.
//   - 이 파일의 Debug.Log 는 **에디터 콘솔 안내**다. 게임 런타임 로그가 아니므로
//     RuntimeLogger(GameLog) 경로를 쓰지 않는다 — Assets/Editor/Setup/ 의 기존
//     셋업 스크립트 14개와 같은 관례다. 강제 실패가 **실제로 발동한** 사실은
//     런타임 쪽(NetworkMapTransfer)에서 GameLog 로 남긴다.
// ============================================================================

using UnityEditor;
using UnityEngine;
using Hexiege.Infrastructure;
using Hexiege.Bootstrap;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 🔴 <b>[에디터 전용]</b> 「다음 맵 준비를 강제로 실패시킨다」는 1회용 토글 메뉴.
    ///
    /// <para>
    /// 상태를 들고 있는 것은 <b>네 클래스</b>이고, 이 클래스는 그것들을 켜고·끄고·보여 주기만 한다 —
    /// ①②③ 맵 준비·전송(사유와 대상 회차)은 <see cref="ForcedMapTransferFailure"/>,
    /// ④ 수락 전송은 <see cref="ForcedAcceptSendFailure"/>,
    /// 싱글 맵 준비는 <see cref="ForcedMapPreparationFailure"/>,
    /// 싱글 맵 투영은 <see cref="ForcedMapProjectionFailure"/>.
    /// ⚠️ <b>[2026-09-29] 앞의 둘은 멀티플레이용이고(①②③ 은 최초 경기·재경기 중 예약 때 고른 회차,
    /// ④ 는 재경기), 뒤의 둘만 싱글플레이용이다.</b>
    /// 🔴 사용자가 보는 메뉴는 <b>같은 묶음 하나</b>이며, 「현재 설정 확인」·「해제」도
    /// 넷을 함께 다룬다(따로 켜지고 따로 꺼지면 무엇이 걸려 있는지 알 수 없으므로).
    /// </para>
    /// </summary>
    public static class ForceRematchMapFailureMenu
    {
        /// <summary>에디터 콘솔 안내에 붙이는 머리표(다른 로그와 섞여도 찾기 쉽게).</summary>
        private const string LogPrefix = "[Debug/강제 실패] ";

        // ────────────────────────────────────────────────────────────────────
        // 예약 (재경기 — 맵 준비·전송 사유 셋 중 하나)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 다음 재경기를 <b>연결 끊김</b>으로 실패시킨다.
        /// 🔴 규칙 18 이 「상대가 나갔다」 문구를 쓰는 <b>유일한 사유</b>다.
        /// </summary>
        [MenuItem("Hexiege/Debug/Failure/다음 재경기/① 연결 끊김 (Disconnected)")]
        private static void ArmDisconnected()
        {
            Arm(MapTransferErrorCode.Disconnected, MapTransferRoundKind.Rematch,
                "결과 화면 상태 줄이 「상대가 나갔다」는 뜻의 실패 문구로 바뀌어야 한다 " +
                "(GameEndUI 의 RematchFailedByOpponentLeftCountdownFormat)");
        }

        /// <summary>
        /// 다음 재경기를 <b>해시 불일치</b>로 실패시킨다(「그 외」 사유의 대표 · 즉시 실패).
        /// </summary>
        [MenuItem("Hexiege/Debug/Failure/다음 재경기/② 해시 불일치 (HashMismatch)")]
        private static void ArmHashMismatch()
        {
            Arm(MapTransferErrorCode.HashMismatch, MapTransferRoundKind.Rematch,
                "결과 화면 상태 줄이 **기본** 실패 문구로 바뀌어야 한다 " +
                "(GameEndUI 의 RematchFailedCountdownFormat — 「상대가 나갔다」 쪽이 아니어야 한다)");
        }

        /// <summary>
        /// 다음 재경기를 <b>응답 없음</b>으로 실패시킨다.
        /// 🔴 이 사유만 <b>즉시 실패가 아니다</b> — 실제 timeout 10초 → 재전송 1회 → 다시 10초를
        /// 거쳐 실패한다(규칙 16). 그래서 결과가 나오기까지 약 20초가 걸리는 것이 정상이다.
        /// </summary>
        [MenuItem("Hexiege/Debug/Failure/다음 재경기/③ 응답 없음 (ResponseTimeout · 약 20초 소요)")]
        private static void ArmResponseTimeout()
        {
            Arm(MapTransferErrorCode.ResponseTimeout, MapTransferRoundKind.Rematch,
                "약 20초 뒤(10초 + 재전송 + 10초)에 **기본** 실패 문구가 떠야 한다. " +
                "그 20초 동안은 상태 줄이 「재경기 준비 중」 표시를 유지해야 한다");
        }

        // ────────────────────────────────────────────────────────────────────
        // 예약 (최초 경기 — 맵 준비·전송 사유 셋 중 하나)  [2026-10-12 추가]
        // ────────────────────────────────────────────────────────────────────
        // 🔴 위 재경기 항목과 **같은 저장소**(맵 전송 사유)를 쓰고 대상 회차만 최초 경기로 지정한다.
        //    메뉴 경로의 폴더 이름이 다르므로 한 폴더에서 두 회차를 섞어 고를 수 없다.
        //    확인할 화면은 사유와 무관하게 같다 — 규칙 M-2 가 사유로 문구를 가르지 않는다.

        /// <summary>
        /// 다음 <b>최초 경기</b>(로비에서 두 명이 모인 직후)를 <b>연결 끊김</b> 사유로 실패시킨다.
        /// ⚠️ 화면에서는 다른 사유와 구별되지 않는다 — 로그의 사유 표식으로만 구별된다.
        /// </summary>
        [MenuItem("Hexiege/Debug/Failure/다음 최초 경기/① 연결 끊김 (Disconnected)")]
        private static void ArmFirstDisconnected()
        {
            Arm(MapTransferErrorCode.Disconnected, MapTransferRoundKind.First, FirstRoundExpectation);
        }

        /// <summary>
        /// 다음 <b>최초 경기</b>를 <b>해시 불일치</b> 사유로 실패시킨다(즉시 실패).
        /// 최초 경기 실패 모달을 가장 빨리 보는 항목이다.
        /// </summary>
        [MenuItem("Hexiege/Debug/Failure/다음 최초 경기/② 해시 불일치 (HashMismatch)")]
        private static void ArmFirstHashMismatch()
        {
            Arm(MapTransferErrorCode.HashMismatch, MapTransferRoundKind.First, FirstRoundExpectation);
        }

        /// <summary>
        /// 다음 <b>최초 경기</b>를 <b>응답 없음</b> 사유로 실패시킨다.
        /// 🔴 이 사유만 즉시 실패가 아니다 — 실제 timeout 10초 → 재전송 1회 → 다시 10초를 거치므로
        /// 모달이 뜨기까지 약 20초 동안 <b>로딩 화면이 유지되는 것이 정상</b>이다.
        /// </summary>
        [MenuItem("Hexiege/Debug/Failure/다음 최초 경기/③ 응답 없음 (ResponseTimeout · 약 20초 소요)")]
        private static void ArmFirstResponseTimeout()
        {
            Arm(MapTransferErrorCode.ResponseTimeout, MapTransferRoundKind.First,
                FirstRoundExpectation + "\n  · 🔴 이 사유만 약 20초(10초 + 재전송 + 10초) 뒤에 모달이 뜬다. " +
                "그 20초 동안은 로딩 화면(게임에 접속하는 중)이 유지되어야 한다.");
        }

        // ────────────────────────────────────────────────────────────────────
        // 예약 (맵 전송과 무관한 별 경로 — 수락 발신)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 다음 재경기의 <b>수락 전송</b>을 강제로 예외로 끝낸다.
        ///
        /// <para>
        /// 🔴 ①②③ 과 달리 <b>맵 준비·전송 단계에 도달하지 않는다.</b> 수락 신호를 서버로
        /// 보내는 그 순간 예외가 나므로, 기존 <c>try/catch</c> 가 그 예외를 받아
        /// 재경기 실패를 <b>로컬로</b> 발행한다.
        /// </para>
        ///
        /// <para>
        /// 🔴 <b>확인할 것은 양쪽 화면이 갈리는 모습이다.</b> 수락을 누른 쪽에는 실패 화면이
        /// 뜨지만, 요청한 쪽에는 <b>신호가 아예 닿지 않아</b> 「요청 중...」이 그대로 남는다.
        /// 그 상태가 의도된 동작인지는 아직 확인된 바가 없다 — 그것을 보기 위한 항목이다.
        /// </para>
        /// </summary>
        [MenuItem("Hexiege/Debug/Failure/다음 재경기/④ 수락 전송 예외 (AcceptSendException)")]
        private static void ArmAcceptSendException()
        {
            ForcedAcceptSendFailure.Arm();

            Debug.Log(LogPrefix + "예약 완료 → 다음 재경기의 **수락 전송**이 예외로 끝난다.\n" +
                      "  · 확인할 것(수락을 누른 쪽): 결과 화면 상태 줄이 **기본** 실패 문구로 바뀌어야 한다 " +
                      "(사유는 Unknown 이므로 「상대가 나갔다」 쪽이 아니어야 한다).\n" +
                      "  · 🔴 확인할 것(요청한 쪽): **아무 변화가 없어야 한다** — 신호가 서버에 닿지 않았으므로 " +
                      "「요청 중...」이 그대로 남고 자기 자동 복귀 타이머로 로비에 간다.\n" +
                      "  · 🔴 맵 준비·전송 단계에는 도달하지 않는다(그보다 앞에서 끝난다).\n" +
                      "  · 🔴 한 번 쓰이면 자동으로 해제된다 — 연속으로 실패시키려면 다시 고를 것.");
        }

        // ────────────────────────────────────────────────────────────────────
        // 예약 (싱글플레이 — 맵 준비 자체를 실패시킨다)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 다음 <b>싱글플레이 맵 준비</b>를 강제로 실패시킨다 (공통 UI 규칙 M-4 의 두 화면 확인용).
        ///
        /// <para>
        /// 🔴 <b>위 ①~④ 와 계층이 다르다.</b> 그 넷은 멀티플레이의 맵 전송·RPC 를 건드리는데,
        /// 싱글플레이는 네트워크를 거치지 않고 자기 기기에서 맵을 만든다. 그래서 그 넷으로는
        /// 이 경로를 <b>한 번도 태울 수 없었다.</b>
        /// </para>
        ///
        /// <para>
        /// 🔴 <b>확인할 것은 상황에 따라 다른 두 화면이다.</b>
        /// <list type="bullet">
        ///   <item><b>최초 경기</b>(로비에서 싱글 시작): 확인/취소 <b>모달 팝업</b>이 떠야 한다.
        ///         확정 버튼은 다시 시도하고, 취소 버튼은 로비로 나간다.</item>
        ///   <item><b>다시하기</b>(결과 화면의 버튼): 팝업이 아니라 <b>결과 화면이 되살아나고</b>
        ///         상태 줄에 실패가 한 줄로 떠야 한다. 🔴 <b>이때 팝업이 함께 뜨면 결함이다</b> —
        ///         팝업이 「로비로」 버튼을 가리기 때문이다(규칙 D-3).</item>
        /// </list>
        /// 🔴 <b>그리고 두 경우 모두 자동 로비 복귀 카운트다운이 돌지 않아야 한다</b>
        /// (규칙 D-4 의 싱글 적용 범위 — 싱글에는 그 타이머가 없다).
        /// </para>
        /// </summary>
        [MenuItem("Hexiege/Debug/Failure/싱글 맵 준비 (다음 1회)")]
        private static void ArmSingleMapPreparationFailure()
        {
            ForcedMapPreparationFailure.Arm();

            Debug.Log(LogPrefix + "예약 완료 → 다음 **싱글플레이** 맵 준비가 실패한다.\n" +
                      "  · 확인할 것(최초 경기 = 로비에서 싱글 시작): 확인/취소 **모달 팝업**이 떠야 한다 " +
                      "(확정=다시 시도 · 취소=로비로).\n" +
                      "  · 확인할 것(다시하기 = 결과 화면의 버튼): 팝업이 아니라 **결과 화면이 되살아나고** " +
                      "상태 줄에 실패가 한 줄로 떠야 한다. 🔴 이때 팝업이 함께 뜨면 결함이다.\n" +
                      "  · 🔴 두 경우 모두 **자동 로비 복귀 카운트다운이 돌지 않아야** 한다(싱글에는 타이머가 없다).\n" +
                      "  · 🔴 멀티플레이에는 영향이 없다(멀티는 이 경로로 맵을 만들지 않는다).\n" +
                      "  · 🔴 한 번 쓰이면 자동으로 해제된다 — 두 화면을 다 보려면 각각 한 번씩 예약할 것.");
        }

        /// <summary>
        /// 다음 <b>싱글플레이 맵 투영</b>(만든 맵을 격자에 새기는 단계)을 강제로 실패시킨다
        /// (공통 UI 규칙 M-4 의 적용 범위가 「어떤 시점이건」으로 확정된 뒤 필요해진 항목).
        ///
        /// <para>
        /// 🔴 <b>바로 위 「싱글 맵 준비」 항목으로는 이 단계를 한 번도 태울 수 없다.</b>
        /// 맵을 쓰는 일은 ① 설계도를 <b>만든다</b> ② 그것을 격자에 <b>새긴다</b> 두 단계인데,
        /// 위 항목은 ①을 실패시키고 <b>①이 실패하면 ②는 아예 불리지 않는다.</b>
        /// 그래서 ②의 실패를 보려면 이 항목이 따로 있어야 한다.
        /// </para>
        ///
        /// <para>
        /// 🔴 <b>확인할 것은 「싱글 맵 준비」와 완전히 같은 두 화면이다</b>(최초 경기 = 모달 팝업 /
        /// 다시하기 = 결과 화면 되살리기). ⚠️ <b>화면이 같은 것이 정상이다</b> — 규칙 M-4 가
        /// <b>실패 사유를 문구로 가르지 않는다</b>고 확정했고, 두 단계 모두 「이번 판의 맵을 쓸 수 없다」는
        /// 한 사건이기 때문이다. 🔴 <b>어느 단계를 실패시킨 것인지는 로그의 사유 문장으로 구별한다.</b>
        /// </para>
        ///
        /// <para>
        /// ⚠️ <b>멀티플레이에는 영향이 없다.</b> 소비 지점이 싱글·멀티 공용 메서드라서 그 자리에
        /// 「싱글인가」 조건을 함께 걸어 두었다 — 멀티에서는 예약을 <b>읽지도 않는다.</b>
        /// </para>
        /// </summary>
        [MenuItem("Hexiege/Debug/Failure/싱글 맵 투영 (다음 1회)")]
        private static void ArmSingleMapProjectionFailure()
        {
            ForcedMapProjectionFailure.Arm();

            Debug.Log(LogPrefix + "예약 완료 → 다음 **싱글플레이** 맵 투영(격자에 새기는 단계)이 실패한다.\n" +
                      "  · 확인할 것(최초 경기 = 로비에서 싱글 시작): 확인/취소 **모달 팝업**이 떠야 한다 " +
                      "(확정=다시 시도 · 취소=로비로).\n" +
                      "  · 확인할 것(다시하기 = 결과 화면의 버튼): 팝업이 아니라 **결과 화면이 되살아나고** " +
                      "상태 줄에 실패가 한 줄로 떠야 한다. 🔴 이때 팝업이 함께 뜨면 결함이다.\n" +
                      "  · ⚠️ 화면은 바로 위 「싱글 맵 준비」 항목과 **완전히 같아야** 한다 — 사유를 문구로 " +
                      "가르지 않는 것이 규칙이다. 어느 단계였는지는 **로그의 사유 문장**으로 구별한다.\n" +
                      "  · 🔴 두 경우 모두 **자동 로비 복귀 카운트다운이 돌지 않아야** 한다(싱글에는 타이머가 없다).\n" +
                      "  · 🔴 멀티플레이에는 영향이 없다(멀티에서는 예약을 읽지도 않는다).\n" +
                      "  · 🔴 한 번 쓰이면 자동으로 해제된다 — 두 화면을 다 보려면 각각 한 번씩 예약할 것.");
        }

        // ────────────────────────────────────────────────────────────────────
        // 확인 · 해제  (🔴 네 저장소를 **함께** 다룬다)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 지금 강제 실패가 예약돼 있는지 콘솔에 알린다.
        /// 🔴 <b>이 항목이 있어야 하는 이유</b>: 예약은 씬이 아니라 EditorPrefs 에 있어
        /// <b>화면 어디에도 보이지 않는다.</b> 확인 수단이 없으면 「왜 계속 실패하지」로 헤맨다.
        ///
        /// <para>
        /// 🔴 <b>저장소 넷을 함께 본다</b>(①②③ = 맵 전송 사유 · ④ = 수락 발신 예외 ·
        /// 싱글 맵 준비 · 싱글 맵 투영). 하나라도 빠뜨리면 「확인했는데 왜 실패하지」라는,
        /// 확인 수단이 없는 것보다 나쁜 상태가 된다.
        /// </para>
        /// </summary>
        [MenuItem("Hexiege/Debug/Failure/현재 설정 확인")]
        private static void ShowCurrent()
        {
            MapTransferErrorCode code = ForcedMapTransferFailure.PeekCode();
            bool acceptSendArmed = ForcedAcceptSendFailure.IsArmed;
            bool singleMapArmed = ForcedMapPreparationFailure.IsArmed;
            bool singleProjectionArmed = ForcedMapProjectionFailure.IsArmed;

            if (code == MapTransferErrorCode.None && !acceptSendArmed
                && !singleMapArmed && !singleProjectionArmed)
            {
                Debug.Log(LogPrefix + "예약 없음 — 싱글·멀티 모두 정상 경로로 진행된다. " +
                          "(①②③ 맵 전송 · ④ 수락 전송 · 싱글 맵 준비 · 싱글 맵 투영 모두 꺼짐)");
                return;
            }

            // 켜져 있는 것만 한 줄씩 나열한다 — 둘이 동시에 켜져 있을 수도 있으므로
            // 「어느 하나」가 아니라 **전부**를 보여 준다.
            string report = LogPrefix + "예약됨 — 아래 항목이 켜져 있다.";

            if (code != MapTransferErrorCode.None)
            {
                // 🔴 대상 회차를 함께 보여 준다 — 사유만 보이면 「왜 재경기가 실패하지 않지」로 헤맨다
                //    (예약이 최초 경기용이면 재경기는 정상으로 진행된다).
                MapTransferRoundKind targetRound = ForcedMapTransferFailure.PeekTargetRound();
                string roundText = targetRound == MapTransferRoundKind.First
                    ? "다음 **최초 경기**(로비에서 두 명이 모인 직후)"
                    : "다음 **재경기**";
                report += "\n  · ①②③ 맵 준비·전송: " + roundText + " 회차가 " + code + " 로 실패한다. " +
                          "(대상이 아닌 회차는 정상으로 진행되고 예약은 남는다)";
            }

            if (acceptSendArmed)
            {
                report += "\n  · ④ 수락 전송: 다음 재경기의 **수락 전송**이 예외로 끝난다" +
                          "(맵 단계에 도달하지 않는다).";
            }

            if (singleMapArmed)
            {
                report += "\n  · 싱글 맵 준비: 다음 **싱글플레이** 맵 준비가 실패한다" +
                          "(최초 경기든 다시하기든 먼저 오는 쪽).";
            }

            if (singleProjectionArmed)
            {
                report += "\n  · 싱글 맵 투영: 다음 **싱글플레이** 맵 투영(격자에 새기는 단계)이 실패한다" +
                          "(최초 경기든 다시하기든 먼저 오는 쪽).";
            }

            // 🔴 싱글 두 항목이 동시에 켜져 있으면 **준비 쪽만** 발동한다 — 준비가 실패하면
            //    투영은 아예 불리지 않으므로, 투영 예약은 소비되지 않고 그대로 남는다.
            //    사용자가 「투영을 예약했는데 왜 준비 실패가 뜨지」로 헤매지 않게 단서를 적는다.
            if (singleMapArmed && singleProjectionArmed)
            {
                report += "\n  🔴 싱글 두 항목이 함께 켜져 있다 — **준비 쪽이 먼저 발동하고 투영은 불리지 않는다.** " +
                          "투영 예약은 소비되지 않고 남으므로 다음 싱글 경기에서 발동한다.";
            }

            // ⚠️ 적용 범위가 항목마다 다르므로 단서를 함께 적는다 —
            //    ①②③ 은 예약 때 고른 한 회차에만, ④ 는 재경기에만, 싱글 항목은 최초 경기와 다시하기
            //    중 먼저 오는 쪽에 적용된다.
            report += "\n  (①②③ 은 예약한 대상 회차 하나에만 적용된다 · ④ 는 재경기에만 적용된다 " +
                      "· 싱글 항목 둘은 최초 경기와 다시하기 중 먼저 오는 쪽에 적용된다 " +
                      "· 모두 한 번 쓰이면 자동 해제된다)";

            Debug.Log(report);
        }

        /// <summary>
        /// 예약을 해제한다. 예약이 없어도 안전하다(멱등).
        /// ⚠️ 한 번 쓰이면 저절로 꺼지므로 보통은 누를 필요가 없다 —
        ///    「켜 두었는데 테스트를 안 하기로 했다」는 경우를 위한 항목이다.
        ///
        /// <para>
        /// 🔴 <b>저장소 넷을 함께 끈다.</b> 「해제」를 눌렀는데 하나라도 남으면 사용자는
        /// 껐다고 믿은 채 계속 실패를 겪게 된다 — 그래서 조건 없이 전부 지운다.
        /// </para>
        /// </summary>
        [MenuItem("Hexiege/Debug/Failure/해제")]
        private static void DisarmMenu()
        {
            // 🔴 맵 전송 예약은 사유와 대상 회차가 한 덩어리라 Disarm 한 번이 둘을 함께 지운다.
            bool mapWasArmed = ForcedMapTransferFailure.IsArmed;
            bool acceptSendWasArmed = ForcedAcceptSendFailure.IsArmed;
            bool singleMapWasArmed = ForcedMapPreparationFailure.IsArmed;
            bool singleProjectionWasArmed = ForcedMapProjectionFailure.IsArmed;

            ForcedMapTransferFailure.Disarm();
            ForcedAcceptSendFailure.Disarm();
            ForcedMapPreparationFailure.Disarm();
            ForcedMapProjectionFailure.Disarm();

            Debug.Log(LogPrefix + (mapWasArmed || acceptSendWasArmed
                                   || singleMapWasArmed || singleProjectionWasArmed
                ? "해제했다 — 싱글·멀티 모두 이제 정상 경로로 진행된다. " +
                  "(맵 전송 예약=" + (mapWasArmed ? "있었음" : "없었음") +
                  " · 수락 전송 예약=" + (acceptSendWasArmed ? "있었음" : "없었음") +
                  " · 싱글 맵 준비 예약=" + (singleMapWasArmed ? "있었음" : "없었음") +
                  " · 싱글 맵 투영 예약=" + (singleProjectionWasArmed ? "있었음" : "없었음") + ")"
                : "예약이 없어 해제할 것이 없었다."));
        }

        // ────────────────────────────────────────────────────────────────────
        // 공통 처리
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 최초 경기 묶음 세 항목이 함께 쓰는 「확인할 것」 안내문.
        /// 🔴 사유와 무관하게 같은 화면이 떠야 한다(규칙 M-2 가 사유로 문구를 가르지 않는다).
        /// </summary>
        private const string FirstRoundExpectation =
            "양쪽 화면에서 로딩이 내려가고 연결과 방을 즉시 정리한 뒤 로비 메인으로 돌아가야 한다.\n" +
            "  · 양쪽에 같은 알림 모달이 떠야 한다: 제목=알림 · 본문=경기를 시작할 수 없습니다. · 버튼=확인 하나.\n" +
            "  · 확인 버튼은 모달만 닫으며, 모달 밖(배경)을 눌러도 닫히지 않아야 한다.\n" +
            "  · 재시도 버튼은 없다. 다시 경기하려면 새 방을 만든다.";

        /// <summary>
        /// 예약 처리 + 콘솔 안내를 한곳에 모은다(맵 전송 사유 여섯 항목이 같은 문장 틀을 쓴다).
        /// </summary>
        /// <param name="code">실패시킬 사유</param>
        /// <param name="round">실패시킬 회차(최초 경기 / 재경기)</param>
        /// <param name="expectation">그 사유로 실패했을 때 화면에서 무엇을 확인해야 하는가</param>
        private static void Arm(MapTransferErrorCode code, MapTransferRoundKind round, string expectation)
        {
            // 예약 칸은 하나라 새 예약이 앞의 예약을 덮어쓴다. 덮어쓰는 경우 조용히 바뀌면 헷갈리므로
            // 앞의 내용을 먼저 읽어 두었다가 콘솔에 알린다.
            MapTransferErrorCode previousCode = ForcedMapTransferFailure.PeekCode();
            MapTransferRoundKind previousRound = ForcedMapTransferFailure.PeekTargetRound();

            if (!ForcedMapTransferFailure.Arm(code, round))
            {
                // 여기 오는 것은 코드 실수뿐이다(지원 목록에 없는 사유나 회차를 넘긴 경우).
                Debug.LogError(LogPrefix + "예약하지 못했다 — 지원하지 않는 사유 또는 회차다: " + code + " / " + round);
                return;
            }

            string roundText = round == MapTransferRoundKind.First ? "최초 경기" : "재경기";
            string overwriteNote = previousCode != MapTransferErrorCode.None
                ? "\n  · ⚠️ 앞의 예약(" + previousCode + " / " +
                  (previousRound == MapTransferRoundKind.First ? "최초 경기" : "재경기") +
                  ")을 덮어썼다 — 예약 칸은 하나다."
                : "";

            Debug.Log(LogPrefix + "예약 완료 → 다음 **" + roundText + "** 회차가 " + code + " 로 실패한다.\n" +
                      "  · 확인할 것: " + expectation + "\n" +
                      "  · 대상이 아닌 회차는 정상으로 진행되고 예약은 남는다 " +
                      "(최초 경기를 대상으로 하면 그 판은 로비에서 시작되지 않는다).\n" +
                      "  · 🔴 한 번 쓰이면 자동으로 해제된다 — 연속으로 실패시키려면 다시 고를 것." +
                      overwriteNote);
        }
    }
}
