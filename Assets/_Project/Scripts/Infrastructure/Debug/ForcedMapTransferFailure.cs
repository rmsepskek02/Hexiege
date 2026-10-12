// ============================================================================
// ForcedMapTransferFailure.cs
//
// 🔴 **에디터 전용 개발 도구.** 「다음 맵 준비·전송을 무조건 실패시킨다」는
//    1회용 토글의 저장소다. 실패 화면(재경기: 공통 UI 규칙 M-3 · 규칙 D-7 · 규칙 18 /
//    최초 경기: 공통 UI 규칙 M-2)을 사람 손으로 재현할 수 없어서 만들었다.
//
// 🔴 **어느 회차를 실패시킬지 고른다 — 최초 경기(로비) 또는 재경기(결과 화면).**
//    예약할 때 사유와 함께 「대상 회차」를 저장하고, 소비하는 쪽이 자기 회차를 넘겨
//    **대상이 아니면 읽기만 하고 소비하지 않는다**(그 예약은 그대로 남는다).
//    ⚠️ 대상을 따로 저장하지 않으면 예약이 「먼저 오는 회차」에 걸린다 — 재경기를 테스트하려는데
//       로비의 최초 경기가 먼저 소비해 버리는 식이라, 게이트를 단순히 넓히는 안은 기각됐다.
//    ⚠️ 예약 칸은 **하나**다. 다른 회차로 다시 예약하면 앞의 예약을 덮어쓴다.
//    ⚠️ 대상 회차가 저장돼 있지 않으면(옛 버전이 남긴 값 포함) **재경기**로 본다 —
//       이 도구가 원래 재경기 전용이었으므로 기존 동작이 그대로 유지된다.
//
// ┌─ 왜 이런 것이 필요한가 (초급자용 설명) ─────────────────────────────────┐
// │  재경기 맵 전송은 실측 99~134ms 만에 끝난다. 「전송 도중에 네트워크를 끊는 │
// │  버튼」을 만들어도 **사람이 그 0.1초 창에 버튼을 맞춰 누를 수 없다.**      │
// │  게다가 연결을 끊는 방식으로는 실패 사유가 `Disconnected` 하나만 나오는데, │
// │  규칙 18 이 **사유에 따라 화면 문구를 가르라**고 정했으므로 나머지 사유도   │
// │  봐야 한다. 그래서 「미리 켜 두고 재경기를 누르면 반드시 실패한다」는       │
// │  방식으로 타이밍 문제를 없앤다.                                           │
// │  (근거 문서: _Tasks/2026-09-16/06_27_post-game-leave-ui/Plan.md §8)       │
// └──────────────────────────────────────────────────────────────────────────┘
//
// 🔴 릴리스 빌드에서 **통째로 사라진다** — 파일 전체가 에디터 전용 컴파일 가드 안에 있다
//    (아래 using 바로 위의 전처리기 지시문 한 줄 · 파일 맨 끝에서 닫는다).
//    (플래그를 켜는 수단이 Unity 에디터 메뉴뿐이므로, 에디터가 아닌 곳에서는
//     애초에 켤 방법이 없다. 켤 수 없는 코드를 빌드에 넣어 둘 이유가 없다.)
//
// 🔴 저장 위치는 **에디터 환경설정 저장소**다(아래 클래스 본문의 키 상수가 그 대상) —
//    씬(.unity)·프리팹(.prefab)에 저장되지 않는다.
//    ⚠️ 이 머리말에 그 저장소의 타입 이름을 그대로 적지 않는다 — 「그 타입이 에디터 전용
//       컴파일 가드 밖에서 참조되지 않는가」를 세는 검사가 주석 때문에 오탐하게 된다.
//    이유 둘:
//      ① 이 프로젝트에서 **Inspector(직렬화) 값이 코드 기본값을 이겨서 두 번 물렸다.**
//         `[SerializeField]` 로 두면 코드에서 끄더라도 씬에 켜진 값이 남아 계속 실패한다.
//      ② 씬에 저장되면 **켠 채로 커밋될 수 있고, 그러면 다른 사람도 실패를 겪는다.**
//    그 저장소는 씬 파일 밖(사람별 로컬 설정)이라 커밋에 섞일 수 없다.
//
// 🔴 **한 번 쓰면 스스로 꺼진다**(1회용). 소비 지점은 `NetworkMapTransfer.StartHostRound`
//    한 곳이며, 거기서 `TryConsume()` 을 부르는 순간 저장값(사유와 대상 회차)이 함께 지워진다.
//    켠 것을 잊고 다음 테스트에서 "왜 계속 실패하지" 로 헤매는 것을 막기 위한 설계다.
//
// 조작 수단: 상단 메뉴 `Hexiege/Debug/...`
//            (구현은 Assets/Editor/Debug/ForceRematchMapFailureMenu.cs)
//
// Infrastructure 레이어 — 전송 계층(NetworkMapTransfer)과 같은 레이어에 둔다.
// ============================================================================

#if UNITY_EDITOR

using UnityEditor;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 🔴 <b>[에디터 전용]</b> 「다음 맵 준비·전송을 강제로 실패시킨다」는 1회용 토글.
    ///
    /// <para>
    /// 상태는 <see cref="EditorPrefs"/> 에만 있다(씬·프리팹·에셋에 저장되지 않는다).
    /// 사유 값이 <see cref="MapTransferErrorCode.None"/> 이면 「꺼져 있다」는 뜻이다.
    /// 사유와 함께 <b>대상 회차</b>(<see cref="MapTransferRoundKind.First"/> 또는
    /// <see cref="MapTransferRoundKind.Rematch"/>)도 저장한다.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>대상 회차가 아니면 소비하지 않는다.</b> 적용 여부를 판단하는 자리는
    /// <c>NetworkMapTransfer.StartHostRound</c> 이며, 거기서 <b>자기 회차</b>를 넘겨
    /// <see cref="TryConsume"/> 을 부른다. 대상이 아닌 회차가 지나가도 예약은 그대로 남는다.
    /// 프로브(한도 실측용 더미 전송)는 게임 흐름이 아니라 소비 지점이 아예 부르지 않는다.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>최초 경기를 대상으로 예약하면 그 판은 로비에서 시작되지 않는다</b> — 로비에 모달이 뜨는
    /// 모습을 보려는 목적으로만 쓴다. 그 뒤 재경기 실패 화면을 보려면 <b>정상으로 한 판을 시작한 뒤</b>
    /// 재경기를 대상으로 따로 예약한다(둘은 별개의 예약이다).
    /// </para>
    /// </summary>
    public static class ForcedMapTransferFailure
    {
        /// <summary>
        /// EditorPrefs 키. 프로젝트 이름을 앞에 붙인다 — EditorPrefs 는 <b>이 PC 의 Unity 전체</b>가
        /// 공유하는 저장소라, 이름이 짧으면 다른 프로젝트의 값과 부딪힐 수 있다.
        /// </summary>
        private const string ArmedCodeKey = "Hexiege.Debug.ForcedRematchMapFailure.ErrorCode";

        /// <summary>
        /// 대상 회차를 저장하는 키(위 키와 같은 접두어).
        /// 🔴 <b>이 키가 없을 때의 기본값은 재경기다</b> — 최초 경기의 숫자 값이 0 이라
        /// 「읽기 기본값 0」을 그대로 쓰면 저장이 없는 예약이 최초 경기로 읽힌다.
        /// </summary>
        private const string TargetRoundKey = "Hexiege.Debug.ForcedRematchMapFailure.TargetRound";

        /// <summary>
        /// 🔴 <b>고를 수 있는 사유는 이 셋뿐이다.</b> 나머지 error code 를 넣지 않는 이유:
        /// <list type="number">
        ///   <item><b><see cref="MapTransferErrorCode.Disconnected"/></b> — 규칙 18 이 문구를 가르는
        ///         <b>유일한 기준</b>이므로 반드시 필요하다.</item>
        ///   <item><b><see cref="MapTransferErrorCode.HashMismatch"/></b> — 「그 외」 사유의 대표.
        ///         즉시 실패 경로를 대표해서 확인한다. 미지원 버전·용량 초과·역직렬화 실패·공정성
        ///         검증 실패는 <b>화면에서 이것과 구별되지 않으므로</b>(규칙 18 의 기준이 연결 끊김
        ///         하나뿐이다) 따로 넣지 않았다.</item>
        ///   <item><b><see cref="MapTransferErrorCode.ResponseTimeout"/></b> — 규칙 16 의
        ///         <b>재전송 1회</b>를 거치는 유일한 경로다. 결과 화면의 「맵 준비 한도」
        ///         (<c>TransferTimeoutSeconds × (MaxResendCount + 1)</c>)가 정말 <b>두 창</b>을
        ///         재는지 확인하려면 이 경로가 필요하다.</item>
        /// </list>
        /// </summary>
        /// <param name="code">확인할 error code</param>
        /// <returns>강제 실패 사유로 쓸 수 있는 값이면 true</returns>
        public static bool IsSupportedCode(MapTransferErrorCode code)
        {
            return code == MapTransferErrorCode.Disconnected
                || code == MapTransferErrorCode.HashMismatch
                || code == MapTransferErrorCode.ResponseTimeout;
        }

        /// <summary>
        /// 대상 회차로 고를 수 있는 값인가. 최초 경기와 재경기 둘뿐이다.
        /// 프로브는 게임 흐름이 아니라(한도 실측용 더미 전송) 대상이 될 수 없다.
        /// </summary>
        /// <param name="round">확인할 회차</param>
        /// <returns>대상으로 쓸 수 있으면 true</returns>
        public static bool IsSupportedRound(MapTransferRoundKind round)
        {
            return round == MapTransferRoundKind.First
                || round == MapTransferRoundKind.Rematch;
        }

        /// <summary>지금 강제 실패가 예약돼 있는가.</summary>
        public static bool IsArmed
        {
            get { return PeekCode() != MapTransferErrorCode.None; }
        }

        /// <summary>
        /// 예약된 실패 사유를 <b>지우지 않고</b> 들여다본다(메뉴의 「현재 설정 확인」용).
        /// 예약이 없으면 <see cref="MapTransferErrorCode.None"/> 을 돌려준다.
        /// </summary>
        /// <returns>예약된 실패 사유(없으면 None)</returns>
        public static MapTransferErrorCode PeekCode()
        {
            int raw = EditorPrefs.GetInt(ArmedCodeKey, (int)MapTransferErrorCode.None);
            var code = (MapTransferErrorCode)raw;

            // 🔴 저장값을 믿지 않는다. 사람이 EditorPrefs 를 직접 만졌거나 예전 버전이 남긴
            //    값일 수 있으므로, 지원하지 않는 값은 「꺼짐」으로 본다(모르면 아무것도 하지 않는다).
            return IsSupportedCode(code) ? code : MapTransferErrorCode.None;
        }

        /// <summary>
        /// 예약된 <b>대상 회차</b>를 지우지 않고 들여다본다(메뉴의 「현재 설정 확인」용).
        /// 사유 예약이 없어도 값은 돌려준다 — 「켜져 있는가」는 <see cref="IsArmed"/> 로 따로 본다.
        /// </summary>
        /// <returns>
        /// 대상 회차. 저장이 없거나 지원하지 않는 값이면 <see cref="MapTransferRoundKind.Rematch"/>
        /// (이 도구가 원래 재경기 전용이었으므로 기존 동작을 유지하는 쪽이 기본값이다)
        /// </returns>
        public static MapTransferRoundKind PeekTargetRound()
        {
            int raw = EditorPrefs.GetInt(TargetRoundKey, (int)MapTransferRoundKind.Rematch);
            var round = (MapTransferRoundKind)raw;

            // 🔴 저장값을 믿지 않는다 — 위 PeekCode 와 같은 이유다. 모르는 값은 기본값(재경기)으로 본다.
            return IsSupportedRound(round) ? round : MapTransferRoundKind.Rematch;
        }

        /// <summary>
        /// 지정한 회차의 맵 준비·전송을 지정한 사유로 실패시키도록 예약한다.
        /// 지원하지 않는 사유나 회차면 아무것도 하지 않고 false 를 돌려준다.
        /// 이미 예약이 있으면 사유와 대상 회차를 <b>둘 다</b> 덮어쓴다(예약 칸은 하나다).
        /// </summary>
        /// <param name="code">실패시킬 사유(<see cref="IsSupportedCode"/> 를 통과해야 한다)</param>
        /// <param name="round">실패시킬 회차(<see cref="IsSupportedRound"/> 를 통과해야 한다)</param>
        /// <returns>예약됐으면 true</returns>
        public static bool Arm(MapTransferErrorCode code, MapTransferRoundKind round)
        {
            if (!IsSupportedCode(code) || !IsSupportedRound(round))
            {
                return false;
            }

            EditorPrefs.SetInt(ArmedCodeKey, (int)code);
            EditorPrefs.SetInt(TargetRoundKey, (int)round);
            return true;
        }

        /// <summary>
        /// 예약을 해제한다. 예약이 없어도 안전하다(멱등).
        /// 🔴 <b>자동 해제도 이 메서드를 거친다</b> — 아래 <see cref="TryConsume"/> 참조.
        /// </summary>
        public static void Disarm()
        {
            // 🔴 사유와 대상 회차를 함께 지운다. 사유만 지우면 다음 예약이 옛 대상 회차를 물려받을 수 있다.
            EditorPrefs.DeleteKey(ArmedCodeKey);
            EditorPrefs.DeleteKey(TargetRoundKey);
        }

        /// <summary>
        /// 🔴 <b>예약을 소비한다 — 읽는 즉시 스스로 꺼진다(1회용).</b>
        /// 단, <b>대상 회차가 지금 회차와 같을 때만</b> 소비한다.
        ///
        /// <para>
        /// [초급자용 설명] 왜 「읽고 지우기」를 한 메서드로 묶는가 —
        /// 「읽기」와 「끄기」를 따로 두면 호출부가 끄는 것을 빠뜨릴 수 있고, 그러면 켜 둔 것을 잊은 채
        /// <b>모든 판이 계속 실패</b>한다. 그게 이 도구로 가장 헷갈리기 쉬운 상황이므로
        /// 구조적으로 막는다(선례: <c>MapHandoff</c> 의 「읽고 비운다」).
        /// </para>
        ///
        /// <para>
        /// 🔴 <b>회차가 다르면 예약을 건드리지 않는다.</b> 재경기를 대상으로 예약해 두고 로비에서
        /// 최초 경기를 먼저 시작하면, 그 최초 경기는 이 메서드가 false 를 돌려주어 정상으로 진행되고
        /// 예약은 재경기까지 살아남는다.
        /// </para>
        /// </summary>
        /// <param name="currentRound">지금 시작하려는 회차(소비 지점이 자기 회차를 넘긴다)</param>
        /// <param name="code">소비했다면 예약돼 있던 사유가 들어간다(소비하지 않았으면 None)</param>
        /// <returns>예약이 있고 대상 회차도 같아서 소비했으면 true</returns>
        public static bool TryConsume(MapTransferRoundKind currentRound, out MapTransferErrorCode code)
        {
            code = PeekCode();

            if (code == MapTransferErrorCode.None)
            {
                return false;
            }

            if (PeekTargetRound() != currentRound)
            {
                // 예약은 있지만 다른 회차용이다. 사유 값을 밖으로 흘리지 않고 그대로 둔다.
                code = MapTransferErrorCode.None;
                return false;
            }

            // 🔴 소비와 동시에 해제한다. 여기가 「자동 해제」가 실제로 일어나는 유일한 자리다.
            Disarm();
            return true;
        }
    }
}

#endif
