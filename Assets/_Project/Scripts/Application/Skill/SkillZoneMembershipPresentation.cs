// ============================================================================
// SkillZoneMembershipPresentation.cs
// "지속 상태 구역(서리 감옥 · 수액 늪) N번에 이 유닛들이 들어왔고, 이 유닛들이 나갔다"는 사실을
// 화면 연출(구역 안 유닛 몸에 붙는 얼음 VFX — V4)에 전하는 불변 값 객체.
// 2026-11-11 지속 상태 구역 Task(_Tasks/2026-11-10/16_02_support-skill-persistent-zones) 구현 단위 ①에서 새로 만들었다.
//
// 무엇을 담나(초급자용 설명):
//   서버는 권위 틱마다 구역 안 유닛을 다시 센다. 지난 틱과 비교해 "바뀐 것"이 있을 때만 이 값을 하나 만든다.
//     - 어느 구역인가?     → Sequence (그 구역을 만든 스킬 발동 순번 = SkillActivationPresentation.Sequence와 같은 값)
//     - 누가 새로 들어왔나? → EnteredIds (유닛 Id 목록)
//     - 누가 나갔나?        → ExitedIds  (걸어 나간 유닛 + 죽은 유닛 Id 목록 — 죽은 유닛은 화면이 스스로 즉시 정지한다)
//   구역이 "끝났다"는 알림은 없다 — 화면이 발동 알림을 받은 순간 + 구역 지속 시간으로 스스로 끝낸다(사용자 결정 Z-2 N-B · Z-7).
//
// 왜 목록을 배열 복사본으로 들고 있나(TowersDisabledPresentation 선례):
//   서버 쪽 구역 서비스는 Id 버퍼(List)를 틱마다 재사용한다. 그 버퍼를 그대로 넘기면 다음 틱이 내용을 덮어써
//   화면이 엉뚱한 유닛을 보게 된다. 그래서 생성자에서 새 배열로 복사해 한 번 만든 값이 바뀌지 않게 한다.
//
// 누가 만들고 누가 받나:
//   - 만드는 곳: SkillZoneSystem.Tick(서버 권위 — 싱글은 로컬 서버) → GameEvents.OnSkillZoneMembershipConfirmed.
//   - 멀티 순수 클라이언트: NetworkSkillController가 ZoneMembershipChangedClientRpc로 받은 값을 다시 이 값으로 조립 →
//     GameEvents.OnSkillZoneMembershipReceived.
//   - 화면: UnitBuffVfxPresenter의 「구역 구성원 구동기」가 두 채널을 모두 구독한다(Host는 「확정」만, 순수 Client는 「수신」만).
//
// Application 레이어 — 순수 C#. Netcode · Core · Infrastructure 참조 없음.
// ============================================================================

using System.Collections.Generic;

namespace Hexiege.Application
{
    /// <summary>
    /// 지속 상태 구역 구성원 변경 표현 값(불변). 서버가 판정한 "이번 틱에 들어온 · 나간 유닛"을 화면 연출용으로 전달한다.
    /// 효과 판정에는 쓰이지 않는다(표현 전용 — 판정은 서버의 SkillZoneSystem이 한다).
    /// </summary>
    public readonly struct SkillZoneMembershipPresentation
    {
        // 빈 목록 공유 배열(읽기 전용으로만 노출되므로 공유해도 안전).
        private static readonly int[] EmptyIds = new int[0];

        private readonly int[] _entered;
        private readonly int[] _exited;

        /// <summary> 구역 순번(그 구역을 만든 스킬 발동 순번). </summary>
        public int Sequence { get; }

        /// <summary> 이번에 구역에 들어온 유닛 Id(읽기 전용, null 아님). </summary>
        public IReadOnlyList<int> EnteredIds => _entered ?? EmptyIds;

        /// <summary> 이번에 구역에서 나간 유닛 Id(걸어 나감 + 사망, 읽기 전용, null 아님). </summary>
        public IReadOnlyList<int> ExitedIds => _exited ?? EmptyIds;

        /// <summary> 들어온 유닛 수. </summary>
        public int EnteredCount => _entered?.Length ?? 0;

        /// <summary> 나간 유닛 수. </summary>
        public int ExitedCount => _exited?.Length ?? 0;

        /// <summary>
        /// 구성원 변경 표현 값 생성. 넘긴 목록은 새 배열로 복사해 보관한다(호출자가 버퍼를 다시 써도 이 값은 그대로).
        /// </summary>
        /// <param name="sequence">구역 순번(발동 순번).</param>
        /// <param name="entered">들어온 유닛 Id 목록(null이면 빈 목록).</param>
        /// <param name="exited">나간 유닛 Id 목록(null이면 빈 목록).</param>
        public SkillZoneMembershipPresentation(int sequence, IReadOnlyList<int> entered, IReadOnlyList<int> exited)
        {
            Sequence = sequence;
            _entered = Copy(entered);
            _exited = Copy(exited);
        }

        /// <summary> 들어온 유닛 Id를 새 배열로 복사해 돌려준다(RPC 인자용 — 원본은 바뀌지 않는다). </summary>
        public int[] CopyEnteredIds() => Copy(EnteredIds);

        /// <summary> 나간 유닛 Id를 새 배열로 복사해 돌려준다(RPC 인자용 — 원본은 바뀌지 않는다). </summary>
        public int[] CopyExitedIds() => Copy(ExitedIds);

        // 방어적 복사 — 원본 목록(재사용 버퍼 · RPC 배열)과 연결을 끊는다.
        private static int[] Copy(IReadOnlyList<int> source)
        {
            if (source == null || source.Count == 0) return EmptyIds;
            var copy = new int[source.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = source[i];
            return copy;
        }
    }
}
