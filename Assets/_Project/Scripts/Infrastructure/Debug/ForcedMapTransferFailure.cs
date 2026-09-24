// ============================================================================
// ForcedMapTransferFailure.cs
//
// 🔴 **에디터 전용 개발 도구.** 「다음 재경기의 맵 준비·전송을 무조건 실패시킨다」는
//    1회용 토글의 저장소다. 실패 화면(공통 UI 규칙 M-3 · 규칙 D-7 · 규칙 18)을
//    사람 손으로 재현할 수 없어서 만들었다.
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
// 🔴 저장 위치는 `EditorPrefs` 다 — 씬(.unity)·프리팹(.prefab)에 저장되지 않는다.
//    이유 둘:
//      ① 이 프로젝트에서 **Inspector(직렬화) 값이 코드 기본값을 이겨서 두 번 물렸다.**
//         `[SerializeField]` 로 두면 코드에서 끄더라도 씬에 켜진 값이 남아 계속 실패한다.
//      ② 씬에 저장되면 **켠 채로 커밋될 수 있고, 그러면 다른 사람도 실패를 겪는다.**
//    EditorPrefs 는 씬 파일 밖(사람별 로컬 설정)이라 커밋에 섞일 수 없다.
//
// 🔴 **한 번 쓰면 스스로 꺼진다**(1회용). 소비 지점은 `NetworkMapTransfer.StartHostRound`
//    한 곳이며, 거기서 `TryConsume()` 을 부르는 순간 저장값이 지워진다.
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
    /// 🔴 <b>[에디터 전용]</b> 「다음 <b>재경기</b> 맵 준비·전송을 강제로 실패시킨다」는 1회용 토글.
    ///
    /// <para>
    /// 상태는 <see cref="EditorPrefs"/> 에만 있다(씬·프리팹·에셋에 저장되지 않는다).
    /// 값이 <see cref="MapTransferErrorCode.None"/> 이면 「꺼져 있다」는 뜻이다.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>최초 경기에는 적용되지 않는다.</b> 적용 여부를 판단하는 자리는
    /// <c>NetworkMapTransfer.StartHostRound</c> 이며, 거기서 회차가
    /// <see cref="MapTransferRoundKind.Rematch"/> 일 때만 이 토글을 본다.
    /// 최초 경기를 실패시키면 로비에서 게임 자체가 시작되지 않아 테스트가 불가능해진다.
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
        /// 다음 재경기 회차를 지정한 사유로 실패시키도록 예약한다.
        /// 지원하지 않는 사유면 아무것도 하지 않고 false 를 돌려준다.
        /// </summary>
        /// <param name="code">실패시킬 사유(<see cref="IsSupportedCode"/> 를 통과해야 한다)</param>
        /// <returns>예약됐으면 true</returns>
        public static bool Arm(MapTransferErrorCode code)
        {
            if (!IsSupportedCode(code))
            {
                return false;
            }

            EditorPrefs.SetInt(ArmedCodeKey, (int)code);
            return true;
        }

        /// <summary>
        /// 예약을 해제한다. 예약이 없어도 안전하다(멱등).
        /// 🔴 <b>자동 해제도 이 메서드를 거친다</b> — 아래 <see cref="TryConsume"/> 참조.
        /// </summary>
        public static void Disarm()
        {
            EditorPrefs.DeleteKey(ArmedCodeKey);
        }

        /// <summary>
        /// 🔴 <b>예약을 소비한다 — 읽는 즉시 스스로 꺼진다(1회용).</b>
        ///
        /// <para>
        /// [초급자용 설명] 왜 「읽고 지우기」를 한 메서드로 묶는가 —
        /// 「읽기」와 「끄기」를 따로 두면 호출부가 끄는 것을 빠뜨릴 수 있고, 그러면 켠 것을 잊은 채
        /// <b>모든 재경기가 계속 실패</b>한다. 그게 이 도구로 가장 헷갈리기 쉬운 상황이므로
        /// 구조적으로 막는다(선례: <c>MapHandoff</c> 의 「읽고 비운다」).
        /// </para>
        /// </summary>
        /// <param name="code">예약돼 있었다면 그 사유가 들어간다(없으면 None)</param>
        /// <returns>예약이 있어서 소비했으면 true</returns>
        public static bool TryConsume(out MapTransferErrorCode code)
        {
            code = PeekCode();

            if (code == MapTransferErrorCode.None)
            {
                return false;
            }

            // 🔴 소비와 동시에 해제한다. 여기가 「자동 해제」가 실제로 일어나는 유일한 자리다.
            Disarm();
            return true;
        }
    }
}

#endif
