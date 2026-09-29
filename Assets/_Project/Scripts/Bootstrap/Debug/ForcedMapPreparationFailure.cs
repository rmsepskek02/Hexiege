// ============================================================================
// ForcedMapPreparationFailure.cs
//
// 🔴 **에디터 전용 개발 도구.** 「다음 싱글플레이 맵 준비를 무조건 실패시킨다」는
//    1회용 토글의 저장소다.
//
// ┌─ 왜 이런 것이 필요한가 (초급자용 설명) ─────────────────────────────────┐
// │  싱글플레이 맵 준비는 **정상 경로에서 실패하지 않는다.**                  │
// │    · 무작위 생성을 100회 시도하고, 그 전부가 거부돼야 폴백 템플릿으로     │
// │      넘어가며, **그 폴백까지 실패**해야 비로소 실패로 끝난다.             │
// │    · 실패 사유 다섯 가지는 **전부 에셋(폴백 템플릿·생성기 정의) 문제**라  │
// │      정상적인 프로젝트에서는 한 번도 나지 않는다.                         │
// │  그래서 이 토글이 없으면 공통 UI 규칙 M-4 가 정한 두 화면                 │
// │  (최초 경기 = 모달 팝업 / 다시하기 = 결과 화면 되살리기)을                │
// │  **아무도 한 번도 볼 수 없다.**                                          │
// └──────────────────────────────────────────────────────────────────────────┘
//
// 🔴 **기존 강제 실패 도구로는 이 경로를 태울 수 없다.**
//    ForcedMapTransferFailure(①②③) · ForcedAcceptSendFailure(④) 는 둘 다
//    **멀티플레이의 맵 전송·RPC 계층**이다. 싱글 맵 준비는 네트워크를 거치지 않으므로
//    그 플래그들이 닿는 자리가 아예 없다. 그래서 저장소를 하나 더 둔다.
//    ⚠️ 반대로 **이 토글은 멀티에 영향을 주지 않는다** — 소비 지점이 구조적으로
//       싱글 전용이다(멀티는 맵을 만들지 않고 로비에서 확정된 것을 새기기만 한다).
//
// 🔴 **왜 기존 두 클래스에 항목을 더하지 않고 클래스를 따로 두는가**
//    담는 값의 **종류가 다르다.** ForcedMapTransferFailure 는 맵 전송 계층의 오류 코드를,
//    ForcedAcceptSendFailure 는 「수락 발신이 예외로 끝난다」는 켜짐/꺼짐을 담는다.
//    이번 것은 그 둘과 다른 계층(싱글 맵 **생성**)의 사건이다. 종류가 다른 것을 한 저장소에
//    섞으면 「지금 켜져 있는 것이 무엇인가」를 값만 보고 알 수 없게 된다.
//    🔴 **대신 「현재 설정 확인」·「해제」는 반드시 세 저장소를 함께 다룬다** —
//       따로 켜지고 따로 꺼지면 사용자는 무엇이 걸려 있는지 알 수 없게 된다.
//       (그 처리는 Assets/Editor/Debug/ForceRematchMapFailureMenu.cs 한 파일에 모여 있다.)
//
// 🔴 릴리스 빌드에서 **통째로 사라진다** — 파일 전체가 에디터 전용 컴파일 가드 안에 있다
//    (아래 using 바로 위의 전처리기 지시문 한 줄 · 파일 맨 끝에서 닫는다).
//    ForcedMapTransferFailure.cs · ForcedAcceptSendFailure.cs 와 같은 구조다.
//
// 🔴 저장 위치는 `EditorPrefs` 다 — 씬(.unity)·프리팹(.prefab)에 저장되지 않는다.
//    (그렇게 하는 이유 두 가지는 ForcedMapTransferFailure.cs 머리말에 적어 두었다.
//     한 사실을 두 곳에 적지 않는다.)
//
// 🔴 **한 번 쓰면 스스로 꺼진다**(1회용). 소비 지점은
//    `GameBootstrapper.PrepareMap()` 한 곳이다.
//
// 조작 수단: 상단 메뉴 `Hexiege/Debug/강제 실패 — 싱글 맵 준비 (다음 1회)`
//            (구현은 Assets/Editor/Debug/ForceRematchMapFailureMenu.cs — 기존 4종과 같은 파일)
//
// Bootstrap 레이어 — 이 플래그를 보는 GameBootstrapper 도 Bootstrap 이다
//   (기존 두 플래그가 그것을 보는 컨트롤러와 같은 Infrastructure 에 있는 것과 같은 관례다).
// ============================================================================

#if UNITY_EDITOR

using UnityEditor;

namespace Hexiege.Bootstrap
{
    /// <summary>
    /// 🔴 <b>[에디터 전용]</b> 「다음 <b>싱글플레이 맵 준비</b>를 강제로 실패시킨다」는 1회용 토글.
    ///
    /// <para>
    /// 상태는 <see cref="EditorPrefs"/> 에만 있다(씬·프리팹·에셋에 저장되지 않으므로
    /// 켠 채로 커밋될 수 없다). 켜짐/꺼짐 두 가지뿐이라 사유 값을 담지 않는다 —
    /// 🔴 <b>실패 사유를 고를 여지가 없기 때문</b>이다. 공통 UI 규칙 M-4 가
    /// <b>사유를 문구로 구분하지 않는다</b>고 확정했으므로, 어떤 사유로 실패시켜도
    /// 화면은 완전히 같다.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>한 번 예약하면 두 화면 중 「먼저 오는 쪽」이 나온다.</b>
    /// <list type="bullet">
    ///   <item>로비에서 싱글 경기를 시작하면 → <b>최초 경기 실패</b> 화면(모달 팝업).</item>
    ///   <item>결과 화면에서 「다시하기」를 누르면 → <b>다시하기 실패</b> 화면
    ///         (결과 화면이 되살아나고 상태 줄에 한 줄이 뜬다).</item>
    /// </list>
    /// 둘을 다 보려면 <b>각각 한 번씩 예약</b>한다(1회용이므로 자동으로 꺼진다).
    /// </para>
    /// </summary>
    public static class ForcedMapPreparationFailure
    {
        /// <summary>
        /// EditorPrefs 키. 프로젝트 이름을 앞에 붙인다 — EditorPrefs 는 <b>이 PC 의 Unity 전체</b>가
        /// 공유하는 저장소라, 이름이 짧으면 다른 프로젝트의 값과 부딪힐 수 있다.
        /// </summary>
        private const string ArmedKey = "Hexiege.Debug.ForcedMapPreparationFailure.Armed";

        /// <summary>지금 강제 실패가 예약돼 있는가(메뉴의 「현재 설정 확인」용).</summary>
        public static bool IsArmed
        {
            get { return EditorPrefs.GetBool(ArmedKey, false); }
        }

        /// <summary>
        /// 다음 싱글플레이 맵 준비를 강제로 실패시키도록 예약한다.
        /// 이미 예약돼 있어도 안전하다(멱등).
        /// </summary>
        public static void Arm()
        {
            EditorPrefs.SetBool(ArmedKey, true);
        }

        /// <summary>
        /// 예약을 해제한다. 예약이 없어도 안전하다(멱등).
        /// 🔴 <b>자동 해제도 이 메서드를 거친다</b> — 아래 <see cref="TryConsume"/> 참조.
        /// </summary>
        public static void Disarm()
        {
            EditorPrefs.DeleteKey(ArmedKey);
        }

        /// <summary>
        /// 🔴 <b>예약을 소비한다 — 읽는 즉시 스스로 꺼진다(1회용).</b>
        ///
        /// <para>
        /// [초급자용 설명] 왜 「읽기」와 「끄기」를 한 메서드로 묶는가 — 따로 두면 호출부가 끄는 것을
        /// 빠뜨릴 수 있고, 그러면 켠 것을 잊은 채 <b>모든 경기가 계속 실패</b>한다. 그게 이 부류의
        /// 도구로 가장 헷갈리기 쉬운 상황이므로 구조적으로 막는다
        /// (선례: <c>ForcedMapTransferFailure.TryConsume</c> · <c>MapHandoff</c> 의 「읽고 비운다」).
        /// </para>
        /// </summary>
        /// <returns>예약이 있어서 소비했으면 true</returns>
        public static bool TryConsume()
        {
            if (!IsArmed)
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
