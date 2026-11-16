// ============================================================================
// ShieldBrokenPresentation.cs
// "발동 N번이 건 쉴드 가운데 이 유닛들 · 이 건물들의 쉴드가 깨졌다(0이 됐다)"는 사실을 화면 연출에 전하는 불변 값 객체.
// 2026-11-16 지원 스킬 새 상태 Task(_Tasks/2026-11-16/01_00_support-skill-new-statuses) 단계 C — Plan 6-1-2 「소진 알림」(S-8).
//
// 무엇을 담나(초급자용 설명):
//   신목의 각성 쉴드는 맞을 때마다 줄어들고, 0이 되면 「깨진다」. 깨진 대상의 쉴드 이펙트는 시간이 남아 있어도 바로 꺼져야 한다.
//   그런데 Client 화면은 HP만 동기화받아 쉴드가 얼마 남았는지 모른다 → 서버가 "깨진 대상 목록"을 알려 줘야 한다.
//     - 어느 발동의 쉴드인가? → Sequence (그 쉴드를 건 스킬 발동 순번 = SkillActivationPresentation.Sequence와 같은 값)
//     - 어느 유닛이 깨졌나?   → UnitIds
//     - 어느 건물이 깨졌나?   → BuildingIds (유닛 Id와 건물 Id는 값이 겹칠 수 있어 목록을 따로 둔다)
//   발동 순번을 함께 보내는 이유: 깨진 직후 같은 대상에 새 쉴드가 걸렸다면(다른 스킬 건물에서 다시 사용),
//   화면은 "옛 순번"의 깨짐 알림으로 "새 순번"의 이펙트를 끄면 안 된다 — 화면은 순번이 같은 기록만 끈다.
//
// 왜 목록을 배열 복사본으로 들고 있나(SkillZoneMembershipPresentation 선례):
//   서버 쪽 서비스는 Id 버퍼(List)를 재사용한다. 그 버퍼를 그대로 넘기면 다음 틱이 내용을 덮어쓰므로 생성자에서 새 배열로 복사한다.
//
// 누가 만들고 누가 받나:
//   - 만드는 곳: SupportSkillEffectSystem.Tick 머리(서버 권위 — 싱글은 로컬 서버) → GameEvents.OnShieldBrokenConfirmed.
//   - 멀티 순수 클라이언트: NetworkSkillController가 ShieldBrokenClientRpc로 받은 값을 다시 이 값으로 조립 →
//     GameEvents.OnShieldBrokenReceived.
//   - 화면: UnitBuffVfxPresenter(유닛 쉴드 이펙트) · BuildingStatusVfxPresenter(건물 쉴드 이펙트)가 두 채널을 모두 구독한다
//     (Host는 「확정」만, 순수 Client는 「수신」만 받는다 — 수신부 ClientRpc의 IsServer 가드).
//
// Application 레이어 — 순수 C#. Netcode · Core · Infrastructure 참조 없음.
// ============================================================================

using System.Collections.Generic;

namespace Hexiege.Application
{
    /// <summary>
    /// 쉴드 깨짐 표현 값(불변). 서버가 판정한 "이번 틱 사이에 쉴드가 0이 된 유닛 · 건물"을 화면 연출용으로 전달한다.
    /// 효과 판정에는 쓰이지 않는다(표현 전용 — 판정은 서버의 SupportSkillEffectSystem이 한다).
    /// </summary>
    public readonly struct ShieldBrokenPresentation
    {
        // 빈 목록 공유 배열(읽기 전용으로만 노출되므로 공유해도 안전).
        private static readonly int[] EmptyIds = new int[0];

        private readonly int[] _units;
        private readonly int[] _buildings;

        /// <summary> 그 쉴드를 건 스킬 발동 순번. </summary>
        public int Sequence { get; }

        /// <summary> 쉴드가 깨진 유닛 Id(읽기 전용, null 아님). </summary>
        public IReadOnlyList<int> UnitIds => _units ?? EmptyIds;

        /// <summary> 쉴드가 깨진 건물 Id(읽기 전용, null 아님). </summary>
        public IReadOnlyList<int> BuildingIds => _buildings ?? EmptyIds;

        /// <summary> 깨진 유닛 수. </summary>
        public int UnitCount => _units?.Length ?? 0;

        /// <summary> 깨진 건물 수. </summary>
        public int BuildingCount => _buildings?.Length ?? 0;

        /// <summary>
        /// 쉴드 깨짐 표현 값 생성. 넘긴 목록은 새 배열로 복사해 보관한다(호출자가 버퍼를 다시 써도 이 값은 그대로).
        /// </summary>
        /// <param name="sequence">그 쉴드를 건 발동 순번.</param>
        /// <param name="unitIds">깨진 유닛 Id 목록(null이면 빈 목록).</param>
        /// <param name="buildingIds">깨진 건물 Id 목록(null이면 빈 목록).</param>
        public ShieldBrokenPresentation(int sequence, IReadOnlyList<int> unitIds, IReadOnlyList<int> buildingIds)
        {
            Sequence = sequence;
            _units = Copy(unitIds);
            _buildings = Copy(buildingIds);
        }

        /// <summary> 깨진 유닛 Id를 새 배열로 복사해 돌려준다(RPC 인자용 — 원본은 바뀌지 않는다). </summary>
        public int[] CopyUnitIds() => Copy(UnitIds);

        /// <summary> 깨진 건물 Id를 새 배열로 복사해 돌려준다(RPC 인자용 — 원본은 바뀌지 않는다). </summary>
        public int[] CopyBuildingIds() => Copy(BuildingIds);

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
