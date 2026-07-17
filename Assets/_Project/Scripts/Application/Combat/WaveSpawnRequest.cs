// ============================================================================
// WaveSpawnRequest.cs
// TorrentSpirit 파도(이동 전선)를 서버 시뮬레이터에 생성 요청할 때 전달하는 값 묶음.
//
// 왜 별도 구조체인가:
//   TorrentAttackBehavior(특수 공격 핸들러)는 "파도의 모양/방향/파라미터"만 결정하고,
//   실제 전선 전진·닿은 유닛 판정·피해/힐 적용은 UnitCombatUseCase 내부의 서버 권위
//   시뮬레이터가 담당한다(규칙 18 — 서버 데미지 타이밍). 이 둘을 잇는 다리로,
//   핸들러가 계산한 기하 정보와 튜닝값을 한 번에 넘긴다(매개변수 나열 방지).
//
// 좌표계:
//   Origin/Forward 는 월드 XZ 평면 기준(Y=0으로 눌린 상태). 서버 권위 좌표
//   (IEntityPositionProvider)로 산출한다. Red 팀 X축 반전은 거리/직사각형 판정을
//   보존하므로 뷰 좌표 그대로 사용해도 무방하다(SweepAttackBehavior와 동일 근거).
//
// Application 레이어 — Domain 의존 + UnityEngine.Vector3(Unity 의존 허용).
// ============================================================================

using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 파도 1개를 생성하기 위한 요청 데이터(불변).
    /// 피해량은 별도로 싣지 않고 <see cref="Caster"/>의 AttackPower를 사용한다
    /// (전 유닛 일관 — 도끼병도 attackPower 사용).
    /// </summary>
    public readonly struct WaveSpawnRequest
    {
        /// <summary> 파도를 시전한 유닛(피해 공격자/힐 시전자·팀·시전자 제외 판정의 기준). </summary>
        public readonly UnitData Caster;

        /// <summary> 파도 전선의 시작 지점(월드 XZ, 보통 시전자 위치). </summary>
        public readonly Vector3 Origin;

        /// <summary> 파도 진행 방향(월드 XZ, 정규화됨. 공격자 → 주 타깃). </summary>
        public readonly Vector3 Forward;

        /// <summary> 파도 가로 폭(월드 단위). 좌우로 절반씩 판정. </summary>
        public readonly float Width;

        /// <summary> 파도 전방 길이(월드 단위). 전선이 이 거리만큼 전진하며 훑는다. </summary>
        public readonly float Length;

        /// <summary> 전선이 Length를 전부 지나가는 데 걸리는 시간(초). </summary>
        public readonly float TravelTime;

        /// <summary> 아군 회복량(양수). </summary>
        public readonly int Heal;

        public WaveSpawnRequest(UnitData caster, Vector3 origin, Vector3 forward,
            float width, float length, float travelTime, int heal)
        {
            Caster = caster;
            Origin = origin;
            Forward = forward;
            Width = width;
            Length = length;
            TravelTime = travelTime;
            Heal = heal;
        }
    }
}
