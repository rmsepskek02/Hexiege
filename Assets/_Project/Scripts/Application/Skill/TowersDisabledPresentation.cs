// ============================================================================
// TowersDisabledPresentation.cs
// "EMP로 이 방어탑들이 몇 초 동안 멈췄다"는 사실을 화면 연출(V6 탑 무력화 VFX)에 전하는 불변 값 객체.
// 2026-11-04 EMP Task 커밋 2(동기화 — Plan 3-4 N1)에서 새로 만들었다.
//
// 무엇을 담나(초급자용 설명):
//   서버가 EMP로 적 방어탑을 멈추게 하면, 내 화면과 상대 화면이 "같은 탑 위에 같은 시간 동안" 무력화 이펙트를
//   띄워야 한다. 그러려면 받는 쪽이 아래 질문에 답할 수 있어야 한다.
//     - 몇 번째 발동인가?        → Sequence (발동 표현 SkillActivationPresentation.Sequence와 같은 값)
//     - 몇 초 동안 멈추나?       → Duration (화면은 이 시간이 지나면 스스로 이펙트를 끈다 — 종료 알림은 없다)
//     - 어느 탑들이 멈췄나?      → TowerIds (건물 Id 목록)
//     - 누가 썼나?               → CasterTeam (서버 「확정」 채널에서만 실제 값 — 아래 「주의」 참조)
//
// 왜 「종료」 알림이 없나(사용자 결정 N1 · Plan 3-4):
//   서버는 무력화가 끝나도 클라이언트에 아무것도 보내지 않는다. 화면 구독자가 "받은 순간 + Duration"을
//   종료 시각으로 들고 있다가 스스로 끈다. 클라이언트에는 무력화 상태의 사본(미러)도 두지 않는다 —
//   클라이언트가 무력화를 알아야 하는 곳은 화면 이펙트뿐이기 때문이다(탑 공격 판정은 서버만 한다).
//   알려진 한계(R-7): 순수 클라이언트는 전송 지연만큼 늦게 시작하고 늦게 끝난다(길이는 같다).
//
// 주의 — CasterTeam은 「확정」 채널에서만 의미가 있다:
//   사용자가 확정한 RPC 모양 TowersDisabledClientRpc(sequence, duration, towerIds)에는 팀이 없다.
//   그래서 순수 클라이언트가 RPC를 받아 다시 만든 값(「수신」 채널)의 CasterTeam은 항상 TeamId.Neutral이다.
//   화면이 탑의 종족 · 위치를 찾을 때는 CasterTeam이 아니라 "탑 Id로 찾은 건물 자신의 팀"을 써야 한다.
//
// 왜 목록을 배열 복사본으로 들고 있나:
//   서버 쪽 SkillActivationUseCase는 탑 Id 목록 버퍼(List)를 발동마다 재사용한다. 그 버퍼를 그대로 넘기면
//   다음 발동이 내용을 덮어써 화면이 엉뚱한 탑을 보게 된다. 그래서 생성자에서 새 배열로 복사하고,
//   밖에는 읽기 전용(IReadOnlyList)으로만 보여 준다 → 한 번 만든 값은 절대 바뀌지 않는다.
//
// 누가 만들고 누가 받나:
//   - 만드는 곳: SkillActivationUseCase의 EMP 다리(서버 권위 — 싱글은 로컬 서버) → GameEvents.OnTowersDisabledConfirmed.
//   - 멀티 순수 클라이언트: NetworkSkillController가 TowersDisabledClientRpc로 받은 값을 다시 이 값으로 조립 →
//     GameEvents.OnTowersDisabledReceived.
//   - 화면: 다음 커밋(3)의 화면 구독자가 두 채널을 모두 구독한다. Host 화면은 「확정」만,
//     순수 클라이언트 화면은 「수신」만 들어오므로(수신부 IsServer 가드) 한 화면에서 두 번 시작되지 않는다.
//
// Application 레이어 — Domain(TeamId)만 사용. Netcode · Core · Infrastructure 참조 없음.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// EMP 방어탑 무력화 표현 값(불변). 서버가 확정한 무력화 1건(대상 탑 목록 · 지속 시간)을 화면 연출용으로 전달한다.
    /// 탑 공격 판정에는 쓰이지 않는다(표현 전용 — 판정은 서버의 BuildingStatusEffectSystem이 한다).
    /// </summary>
    public readonly struct TowersDisabledPresentation
    {
        // 비어 있는 목록을 매번 새로 만들지 않도록 공유하는 빈 배열(읽기 전용으로만 노출되므로 공유해도 안전).
        private static readonly int[] EmptyIds = new int[0];

        // 탑 Id 복사본. 생성자 밖에서는 절대 바꾸지 않는다(불변 보장).
        private readonly int[] _towerIds;

        /// <summary>
        /// 스킬을 쓴 팀. 서버 「확정」 채널에서만 실제 값이고, 순수 클라이언트 「수신」 채널에서는
        /// RPC에 팀이 없으므로 항상 <see cref="TeamId.Neutral"/>이다. 화면은 이 값 대신 탑 건물 자신의 팀을 써야 한다.
        /// </summary>
        public TeamId CasterTeam { get; }

        /// <summary>
        /// 발동 순번(서버가 성공시킨 발동마다 1씩 증가, 1부터). 같은 발동의 SkillActivationPresentation.Sequence와 같다.
        /// 지금은 기록 · 로그용이며, 화면이 V5(발동 고리)와 V6(탑 무력화)를 짝지을 일이 생기면 쓴다.
        /// </summary>
        public int Sequence { get; }

        /// <summary> 무력화 지속 시간(초). 화면은 받은 순간부터 이 시간이 지나면 이펙트를 끈다. 항상 0 이상. </summary>
        public float Duration { get; }

        /// <summary> 이번에 무력화(또는 남은 시간 갱신)된 탑들의 건물 Id 목록(읽기 전용). null이 아니다. </summary>
        public IReadOnlyList<int> TowerIds => _towerIds ?? EmptyIds;

        /// <summary> 대상 탑 수. </summary>
        public int TowerCount => _towerIds?.Length ?? 0;

        /// <summary>
        /// 무력화 표현 값 생성. 넘긴 목록은 새 배열로 복사해 보관한다(호출자가 버퍼를 다시 써도 이 값은 그대로).
        /// </summary>
        /// <param name="casterTeam">스킬을 쓴 팀(「수신」 채널은 알 수 없으므로 TeamId.Neutral을 넘긴다).</param>
        /// <param name="sequence">발동 순번.</param>
        /// <param name="duration">무력화 지속 시간(초). 음수 · NaN은 0으로 고쳐 보관한다(전송 값 방어).</param>
        /// <param name="towerIds">대상 탑 Id 목록. null이면 빈 목록으로 본다.</param>
        public TowersDisabledPresentation(TeamId casterTeam, int sequence, float duration, IReadOnlyList<int> towerIds)
        {
            CasterTeam = casterTeam;
            Sequence = sequence;
            // "0보다 큰 값"만 지속 시간으로 인정한다(NaN > 0 은 false 라서 NaN도 0이 된다).
            Duration = duration > 0f ? duration : 0f;

            // 방어적 복사 — 원본 목록(재사용 버퍼 · RPC 배열)과 연결을 끊는다.
            if (towerIds == null || towerIds.Count == 0)
            {
                _towerIds = EmptyIds;
            }
            else
            {
                _towerIds = new int[towerIds.Count];
                for (int i = 0; i < _towerIds.Length; i++) _towerIds[i] = towerIds[i];
            }
        }

        /// <summary>
        /// 탑 Id 목록을 새 배열로 복사해 돌려준다(네트워크 RPC 인자처럼 배열이 필요한 곳용 — 원본은 바뀌지 않는다).
        /// </summary>
        public int[] CopyTowerIds()
        {
            int count = TowerCount;
            var copy = new int[count];
            for (int i = 0; i < count; i++) copy[i] = _towerIds[i];
            return copy;
        }
    }
}
