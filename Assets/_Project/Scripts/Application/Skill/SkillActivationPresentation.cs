// ============================================================================
// SkillActivationPresentation.cs
// "스킬이 발동됐다"는 사실을 화면 연출(VFX)에 전하기 위한 불변 값 객체(C-1 발동 연출 동기화).
//
// 무엇을 담나(초급자용 설명):
//   서버가 스킬 발동을 확정하면, 내 화면과 상대 화면이 "같은 자리에 같은 이펙트"를 띄워야 한다.
//   그러려면 받는 쪽이 아래 질문에 답할 수 있어야 한다.
//     - 어느 건물이 썼나?           → BuildingId
//     - 어느 팀이 썼나?             → Team  (팀 → 종족 → 로드아웃을 찾는 열쇠)
//     - 몇 번 칸 스킬인가?          → Slot  (로드아웃에서 SkillData를 꺼내는 열쇠)
//     - 어디에 썼나?                → HasAim / AimWorld (도메인 좌표 그대로 — 화면 변환은 받는 쪽이 한다)
//     - 몇 번째 발동인가?           → Sequence (같은 건물이 다시 쓴 발동과 늦게 오는 착탄을 짝지을 열쇠)
//     - 착탄까지 몇 초 걸리나?      → ImpactDelay (0이면 즉발)
//
// 왜 반경 · VFX 프리팹은 싣지 않나(Plan 3-1 안 A — 사용자 채택):
//   받는 쪽이 Team · Slot으로 자기 빌드의 SkillData를 다시 찾으면 반경 · 프리팹을 얻을 수 있다.
//   데이터를 한 곳(스킬 자산)에만 두는 원칙(스킬 규칙 7)을 지키고 전송량도 줄인다.
//   두 기기의 빌드(로드아웃 순서)가 같다는 것은 전투 표현 스키마(7) 일치 확인이 보장한다.
//
// 왜 좌표를 "도메인 좌표"로 싣나:
//   빨강 팀 화면은 맵이 180° 뒤집혀 보인다. 서버 · 페이로드는 항상 도메인 좌표만 다루고,
//   각 화면이 재생 직전에 ViewConverter.ToView로 자기 화면 좌표로 바꾼다(공격 표현 선례와 같은 분담).
//
// 누가 만들고 누가 받나:
//   - 만드는 곳: SkillActivationUseCase.Activate(서버 권위 — 싱글은 로컬 서버) → GameEvents.OnSkillActivationConfirmed.
//   - 멀티 순수 클라이언트: NetworkSkillController가 ClientRpc로 받은 값을 다시 이 값으로 조립 →
//     GameEvents.OnSkillActivationReceived.
//   - 화면: Presentation의 화면 구독자(SkillActivationVfxPresenter)가 두 채널을 모두 구독해 VFX를 재생한다.
//     Host 화면은 「확정」만, 순수 클라이언트 화면은 「수신」만 들어오므로 한 화면에서 두 번 재생되지 않는다.
//
// Application 레이어 — Domain(TeamId) + UnityEngine.Vector3만 사용. Netcode 참조 없음.
// ============================================================================

using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 스킬 발동 표현 값(불변). 서버가 확정한 발동 1건을 화면 연출용으로 전달한다.
    /// 피해 · HP · 쿨다운 판정에는 쓰이지 않는다(표현 전용).
    /// </summary>
    public readonly struct SkillActivationPresentation
    {
        /// <summary> 스킬을 발동한 건물의 Id. </summary>
        public int BuildingId { get; }

        /// <summary> 스킬을 발동한 팀(받는 쪽이 팀 → 종족 → 로드아웃으로 SkillData를 찾는 열쇠). </summary>
        public TeamId Team { get; }

        /// <summary> 발동한 슬롯 번호(0-based, 0~4). 로드아웃에서 SkillData를 꺼내는 열쇠. </summary>
        public int Slot { get; }

        /// <summary> 조준 좌표가 있는지(지점 지정 스킬이면 true, 즉시 발동이면 false). </summary>
        public bool HasAim { get; }

        /// <summary> 조준 좌표(도메인 월드, XZ 평면 · Y=0). HasAim이 false면 의미 없음. 화면 변환 전 값이다. </summary>
        public Vector3 AimWorld { get; }

        /// <summary>
        /// 발동 순번(이 경기에서 서버가 성공시킨 발동마다 1씩 증가, 1부터 시작).
        /// 같은 건물의 다음 발동과 늦게 오는 착탄을 짝지을 때 쓴다(메테오 착탄 폭발 등 — 지금은 짝지을 곳이 없다).
        /// </summary>
        public int Sequence { get; }

        /// <summary> 발동부터 착탄(피해)까지의 시간(초). 0이면 즉발. </summary>
        public float ImpactDelay { get; }

        /// <summary>
        /// 발동 표현 값 생성.
        /// </summary>
        /// <param name="buildingId">발동한 건물 Id.</param>
        /// <param name="team">발동한 팀.</param>
        /// <param name="slot">발동한 슬롯 번호(0-based).</param>
        /// <param name="hasAim">조준 좌표 유효 여부.</param>
        /// <param name="aimWorld">조준 좌표(도메인 월드). hasAim이 false면 무시된다.</param>
        /// <param name="sequence">발동 순번.</param>
        /// <param name="impactDelay">착탄 지연(초). 음수 · NaN은 0으로 고쳐 보관한다(전송 값 방어).</param>
        public SkillActivationPresentation(
            int buildingId,
            TeamId team,
            int slot,
            bool hasAim,
            Vector3 aimWorld,
            int sequence,
            float impactDelay)
        {
            BuildingId = buildingId;
            Team = team;
            Slot = slot;
            HasAim = hasAim;
            // 조준이 없으면 좌표를 0으로 고정해 "의미 없는 값"이 화면 쪽으로 흘러가지 않게 한다.
            AimWorld = hasAim ? aimWorld : Vector3.zero;
            Sequence = sequence;
            // "0보다 큰 값"만 지연으로 인정한다(NaN > 0 은 false 라서 NaN도 0이 된다).
            ImpactDelay = impactDelay > 0f ? impactDelay : 0f;
        }
    }
}
