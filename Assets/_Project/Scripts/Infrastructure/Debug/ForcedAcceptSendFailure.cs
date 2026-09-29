// ============================================================================
// ForcedAcceptSendFailure.cs
//
// 🔴 **에디터 전용 개발 도구.** 「다음 재경기 수락을 서버로 보내다가 예외가 나게 한다」는
//    1회용 토글의 저장소다.
//
// ┌─ 왜 이런 것이 필요한가 (초급자용 설명) ─────────────────────────────────┐
// │  재경기가 안 되는 길은 셋이다(그 목록은                                   │
// │  NetworkGameEndController.SendAcceptRematchSafely 의 catch 주석에 있다).  │
// │    ① 서버로 수락을 아예 못 보냈다                                        │
// │    ② 서버가 새 맵 준비 실패를 통보해 왔다                                │
// │    ③ 준비 한도가 지났는데 통보가 없다                                    │
// │  ②·③ 은 기존 강제 실패 플래그(ForcedMapTransferFailure)로 재현할 수      │
// │  있지만, **①은 재현 수단이 하나도 없었다.** ① 이 나는 자리는             │
// │  「맵을 주고받기 **전**」, 즉 수락 RPC 를 발신하는 순간이라 전송 계층의    │
// │  플래그가 닿지 않는다. 그래서 이 파일을 따로 만든다.                      │
// │                                                                          │
// │  ① 을 사람 손으로 만들 수 없는 이유: 발신이 실패하려면 「수락 버튼을      │
// │  누르는 그 순간에 NetworkManager 가 이미 내려가 있어야」 한다. 사람이     │
// │  그 순간을 맞출 수 없다(기존 3종이 0.1초 창을 맞출 수 없는 것과 같은      │
// │  이유다).                                                                │
// └──────────────────────────────────────────────────────────────────────────┘
//
// 🔴 **왜 기존 ForcedMapTransferFailure 에 넣지 않고 클래스를 따로 두는가**
//    그 클래스가 담는 값은 `MapTransferErrorCode` — **맵 전송 계층의 오류 코드**다.
//    이번 것은 전송 오류가 아니라 「RPC 를 보내다가 예외가 났다」이고, 애초에
//    전송이 시작되기도 전의 사건이다. **종류가 다른 것을 한 저장소에 섞으면**
//    「지금 켜져 있는 것이 전송 실패인가 발신 실패인가」를 값만 보고 알 수 없게 된다.
//    그래서 저장소는 따로 두고, **기존 패턴(에디터 환경설정 저장소 · 1회용 ·
//    에디터 전용 컴파일 가드)만**
//    똑같이 본뜬다.
//
// 🔴 릴리스 빌드에서 **통째로 사라진다** — 파일 전체가 에디터 전용 컴파일 가드 안에 있다
//    (아래 using 바로 위의 전처리기 지시문 한 줄 · 파일 맨 끝에서 닫는다).
//    ForcedMapTransferFailure.cs 와 같은 구조다.
//
// 🔴 저장 위치는 **에디터 환경설정 저장소**다(아래 클래스 본문의 키 상수가 그 대상) —
//    씬(.unity)·프리팹(.prefab)에 저장되지 않는다.
//    ⚠️ 이 머리말에 그 저장소의 타입 이름을 그대로 적지 않는다 — 「그 타입이 에디터 전용
//       컴파일 가드 밖에서 참조되지 않는가」를 세는 검사가 주석 때문에 오탐하게 된다.
//    (이유 두 가지는 ForcedMapTransferFailure.cs 머리말에 적어 두었다.
//     한 사실을 두 곳에 적지 않는다.)
//
// 🔴 **한 번 쓰면 스스로 꺼진다**(1회용). 소비 지점은
//    `NetworkGameEndController.SendAcceptRematchSafely` 한 곳이다.
//
// 조작 수단: 상단 메뉴 `Hexiege/Debug/강제 실패 — 다음 재경기/④ ...`
//            (구현은 Assets/Editor/Debug/ForceRematchMapFailureMenu.cs — 기존 3종과 같은 파일)
//
// Infrastructure 레이어 — 이 플래그를 보는 NetworkGameEndController 도 Infrastructure 다.
// ============================================================================

#if UNITY_EDITOR

using UnityEditor;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 🔴 <b>[에디터 전용]</b> 「다음 재경기 <b>수락 전송</b>을 강제로 예외로 끝낸다」는 1회용 토글.
    ///
    /// <para>
    /// 상태는 <see cref="EditorPrefs"/> 에만 있다(씬·프리팹·에셋에 저장되지 않는다).
    /// 켜짐/꺼짐 두 가지뿐이라 <see cref="ForcedMapTransferFailure"/> 처럼 사유 값을 담지 않는다 —
    /// <b>실패 사유를 고를 여지가 없기 때문</b>이다. 이 경로의 사유는 언제나
    /// <c>RematchMapFailureCause.Unknown</c> 이다(왜 못 보냈는지 그 자리에서 알 수 없으므로.
    /// 근거는 <c>NetworkGameEndController.SendAcceptRematchSafely</c> 의 catch 주석).
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>수락을 누른 쪽에서만 효과가 보인다.</b> 강제로 막는 것은 「내가 서버로 보내는 신호」이고,
    /// 상대(요청한 쪽)에게는 <b>아무 신호도 가지 않는다.</b> 그래서 요청자 화면은 「요청 중...」이
    /// 그대로 남고 자기 자동 복귀 타이머가 만료될 때까지 아무것도 모른다 —
    /// <b>이 도구로 확인하려는 것이 바로 그 양쪽 화면의 갈림</b>이다.
    /// </para>
    /// </summary>
    public static class ForcedAcceptSendFailure
    {
        /// <summary>
        /// EditorPrefs 키. 프로젝트 이름을 앞에 붙인다 — EditorPrefs 는 <b>이 PC 의 Unity 전체</b>가
        /// 공유하는 저장소라, 이름이 짧으면 다른 프로젝트의 값과 부딪힐 수 있다.
        /// </summary>
        private const string ArmedKey = "Hexiege.Debug.ForcedAcceptSendFailure.Armed";

        /// <summary>지금 강제 실패가 예약돼 있는가(메뉴의 「현재 설정 확인」용).</summary>
        public static bool IsArmed
        {
            get { return EditorPrefs.GetBool(ArmedKey, false); }
        }

        /// <summary>
        /// 다음 재경기 수락 전송을 강제로 예외로 끝내도록 예약한다.
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
        /// 빠뜨릴 수 있고, 그러면 켠 것을 잊은 채 <b>모든 재경기 수락이 계속 실패</b>한다.
        /// 그게 이 부류의 도구로 가장 헷갈리기 쉬운 상황이므로 구조적으로 막는다
        /// (선례: <see cref="ForcedMapTransferFailure.TryConsume"/> · <c>MapHandoff</c> 의 「읽고 비운다」).
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

    /// <summary>
    /// 🔴 <b>[에디터 전용]</b> 위 <see cref="ForcedAcceptSendFailure"/> 가 예약돼 있을 때
    /// <c>NetworkGameEndController.SendAcceptRematchSafely</c> 가 던지는 예외.
    ///
    /// <para>
    /// 🔴 <b>[초급자용 설명] 왜 「NGO 가 실제로 던지는 예외」를 흉내내지 않고 전용 타입을 만들었는가</b><br/>
    /// 받는 쪽 <c>catch</c> 는 <c>System.Exception</c> 을 받고 로그에 <c>Exception={타입 이름}</c> 을
    /// 싣는다. 즉 <b>여기서 고른 타입 이름이 그대로 로그에 뜬다.</b> 그래서 고르는 기준은
    /// 「로그를 보는 사람이 무엇을 알 수 있는가」다.
    /// </para>
    ///
    /// <list type="number">
    ///   <item><b>이름 자체가 「강제로 만든 것」이라고 말한다.</b> 진짜 장애 로그와 섞이지 않는다.
    ///         NGO 의 것(<i>"Rpc methods can only be invoked after starting the NetworkManager!"</i>)을
    ///         흉내내면 <b>나중에 같은 로그를 본 사람이 진짜인지 강제인지 되짚어야</b> 한다.</item>
    ///   <item><b>NGO 가 그 상황에서 던지는 예외의 정확한 타입을 이 작업에서 확인하지 않았다.</b>
    ///         확인하지 않은 것을 「그럴 것이다」로 흉내내면 그 자체가 추정이고
    ///         (CLAUDE.md 규칙 10), 틀렸을 때는 오히려 <b>덜 진짜 같은</b> 로그가 남는다.</item>
    ///   <item>흉내내기의 이점(진짜 같음)은 <b>이미 다른 수단으로 확보된다</b> —
    ///         같은 로그 줄에 <c>Forced=</c> 표식을 함께 싣고, 던지기 전에 안내 Warn 한 줄을 남긴다.
    ///         🔴 <b>중요한 것은 예외 타입이 진짜 같은 것이 아니라 「기존 catch 경로를 그대로 타는 것」</b>이고,
    ///         그것은 <b>던지는 자리를 try 블록 안에 두는 것</b>으로 달성된다.</item>
    /// </list>
    /// </summary>
    public class ForcedAcceptSendException : System.Exception
    {
        /// <summary>
        /// 기본 메시지로 만든다. 메시지에 「실제 장애가 아니다」를 넣어 두는 이유는,
        /// 혹시 이 예외가 Unity 콘솔에 스택과 함께 찍히더라도 그 한 줄만 보고 판단할 수 있게 하기 위해서다.
        /// </summary>
        public ForcedAcceptSendException()
            : base("[강제 실패] 개발용 플래그로 재경기 수락 전송을 일부러 실패시켰다(실제 장애가 아니다).")
        {
        }
    }
}

#endif
