// ============================================================================
// ForceRematchMapFailureMenu.cs  (에디터 전용 · 개발용 강제 실패 토글 메뉴)
//
// ┌─ 사용법 ────────────────────────────────────────────────────────────────┐
// │  1) 상단 메뉴  Hexiege > Debug > 강제 실패 — 다음 재경기  에서            │
// │     실패시킬 사유 하나를 고른다(연결 끊김 / 해시 불일치 / 응답 없음).     │
// │  2) 그대로 멀티플레이를 시작해 경기를 끝내고 **재경기**를 누른다.         │
// │     → 그 회차의 맵 준비·전송이 고른 사유로 반드시 실패한다.               │
// │  3) 🔴 **한 번 쓰이면 자동으로 꺼진다.** 다시 실패시키려면 또 고른다.     │
// │  4) 지금 켜져 있는지 궁금하면  Hexiege > Debug > 강제 실패 — 현재 설정    │
// │     확인  을 누른다. 끄고 싶으면 그 아래 「해제」를 누른다.               │
// └────────────────────────────────────────────────────────────────────────┘
//
// 무엇을 위한 것인가:
//   재경기 맵 준비가 **실패했을 때의 결과 화면**(공통 UI 규칙 M-3 · D-7 · 규칙 18)을
//   실제로 눈으로 확인하기 위한 도구다. 정상 경로에서는 실패가 나지 않으므로
//   이 토글이 없으면 그 화면을 한 번도 볼 수 없다.
//   근거 문서: Assets/_Project/Docs/_Tasks/2026-09-16/06_27_post-game-leave-ui/Plan.md §8
//
// 🔴 왜 「네트워크를 끊는 테스트 버튼」이 아닌가:
//   · 맵 전송 구간이 실측 99~134ms 라 **사람이 그 창에 버튼을 맞춰 누를 수 없다.**
//   · 연결을 끊는 방식으로는 사유가 `Disconnected` 하나만 나오는데,
//     규칙 18 이 **사유에 따라 문구를 가르라**고 정했으므로 나머지 사유도 봐야 한다.
//
// 🔴 사유가 셋뿐인 이유(그 이상 넣지 말 것):
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

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 🔴 <b>[에디터 전용]</b> 「다음 재경기의 맵 준비·전송을 강제로 실패시킨다」는 1회용 토글 메뉴.
    /// 상태를 들고 있는 것은 <see cref="ForcedMapTransferFailure"/> 이고, 이 클래스는 그것을
    /// 켜고·끄고·보여 주기만 한다.
    /// </summary>
    public static class ForceRematchMapFailureMenu
    {
        /// <summary>에디터 콘솔 안내에 붙이는 머리표(다른 로그와 섞여도 찾기 쉽게).</summary>
        private const string LogPrefix = "[Debug/강제 실패] ";

        // ────────────────────────────────────────────────────────────────────
        // 예약 (셋 중 하나)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 다음 재경기를 <b>연결 끊김</b>으로 실패시킨다.
        /// 🔴 규칙 18 이 「상대가 나갔다」 문구를 쓰는 <b>유일한 사유</b>다.
        /// </summary>
        [MenuItem("Hexiege/Debug/강제 실패 — 다음 재경기/① 연결 끊김 (Disconnected)")]
        private static void ArmDisconnected()
        {
            Arm(MapTransferErrorCode.Disconnected,
                "결과 화면 상태 줄이 「상대가 나갔다」는 뜻의 실패 문구로 바뀌어야 한다 " +
                "(GameEndUI 의 RematchFailedByOpponentLeftCountdownFormat)");
        }

        /// <summary>
        /// 다음 재경기를 <b>해시 불일치</b>로 실패시킨다(「그 외」 사유의 대표 · 즉시 실패).
        /// </summary>
        [MenuItem("Hexiege/Debug/강제 실패 — 다음 재경기/② 해시 불일치 (HashMismatch)")]
        private static void ArmHashMismatch()
        {
            Arm(MapTransferErrorCode.HashMismatch,
                "결과 화면 상태 줄이 **기본** 실패 문구로 바뀌어야 한다 " +
                "(GameEndUI 의 RematchFailedCountdownFormat — 「상대가 나갔다」 쪽이 아니어야 한다)");
        }

        /// <summary>
        /// 다음 재경기를 <b>응답 없음</b>으로 실패시킨다.
        /// 🔴 이 사유만 <b>즉시 실패가 아니다</b> — 실제 timeout 10초 → 재전송 1회 → 다시 10초를
        /// 거쳐 실패한다(규칙 16). 그래서 결과가 나오기까지 약 20초가 걸리는 것이 정상이다.
        /// </summary>
        [MenuItem("Hexiege/Debug/강제 실패 — 다음 재경기/③ 응답 없음 (ResponseTimeout · 약 20초 소요)")]
        private static void ArmResponseTimeout()
        {
            Arm(MapTransferErrorCode.ResponseTimeout,
                "약 20초 뒤(10초 + 재전송 + 10초)에 **기본** 실패 문구가 떠야 한다. " +
                "그 20초 동안은 상태 줄이 「재경기 준비 중」 표시를 유지해야 한다");
        }

        // ────────────────────────────────────────────────────────────────────
        // 확인 · 해제
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 지금 강제 실패가 예약돼 있는지 콘솔에 알린다.
        /// 🔴 <b>이 항목이 있어야 하는 이유</b>: 예약은 씬이 아니라 EditorPrefs 에 있어
        /// <b>화면 어디에도 보이지 않는다.</b> 확인 수단이 없으면 「왜 계속 실패하지」로 헤맨다.
        /// </summary>
        [MenuItem("Hexiege/Debug/강제 실패 — 현재 설정 확인")]
        private static void ShowCurrent()
        {
            MapTransferErrorCode code = ForcedMapTransferFailure.PeekCode();

            if (code == MapTransferErrorCode.None)
            {
                Debug.Log(LogPrefix + "예약 없음 — 재경기는 정상 경로로 진행된다.");
                return;
            }

            Debug.Log(LogPrefix + "예약됨 → 다음 **재경기** 회차가 " + code +
                      " 로 실패한다. (최초 경기에는 적용되지 않으며, 한 번 쓰이면 자동 해제된다)");
        }

        /// <summary>
        /// 예약을 해제한다. 예약이 없어도 안전하다(멱등).
        /// ⚠️ 한 번 쓰이면 저절로 꺼지므로 보통은 누를 필요가 없다 —
        ///    「켜 두었는데 테스트를 안 하기로 했다」는 경우를 위한 항목이다.
        /// </summary>
        [MenuItem("Hexiege/Debug/강제 실패 — 해제")]
        private static void DisarmMenu()
        {
            bool wasArmed = ForcedMapTransferFailure.IsArmed;
            ForcedMapTransferFailure.Disarm();

            Debug.Log(LogPrefix + (wasArmed
                ? "해제했다 — 재경기는 이제 정상 경로로 진행된다."
                : "예약이 없어 해제할 것이 없었다."));
        }

        // ────────────────────────────────────────────────────────────────────
        // 공통 처리
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 예약 처리 + 콘솔 안내를 한곳에 모은다(세 메뉴 항목이 같은 문장 틀을 쓴다).
        /// </summary>
        /// <param name="code">실패시킬 사유</param>
        /// <param name="expectation">그 사유로 실패했을 때 화면에서 무엇을 확인해야 하는가</param>
        private static void Arm(MapTransferErrorCode code, string expectation)
        {
            if (!ForcedMapTransferFailure.Arm(code))
            {
                // 여기 오는 것은 코드 실수뿐이다(지원 목록에 없는 사유를 넘긴 경우).
                Debug.LogError(LogPrefix + "예약하지 못했다 — 지원하지 않는 사유다: " + code);
                return;
            }

            Debug.Log(LogPrefix + "예약 완료 → 다음 **재경기** 회차가 " + code + " 로 실패한다.\n" +
                      "  · 확인할 것: " + expectation + "\n" +
                      "  · 최초 경기에는 적용되지 않는다(로비에서 게임이 시작되지 않으면 테스트 자체가 불가능하므로).\n" +
                      "  · 🔴 한 번 쓰이면 자동으로 해제된다 — 연속으로 실패시키려면 다시 고를 것.");
        }
    }
}
