// ============================================================================
// ISkillDataProvider.cs
// 종족(RaceId) → 스킬 로드아웃(슬롯 1~5의 SkillData 목록) 조회 인터페이스.
//
// 왜 인터페이스인가(의존성 역전):
//   스킬 데이터는 Infrastructure의 ScriptableObject(SkillLoadoutConfig)에 저작된다.
//   Application(SkillActivationUseCase)이 그 구체 SO에 직접 의존하면 역참조 규칙 위반이므로,
//   Application에 이 인터페이스를 선언하고 Infrastructure가 구현한다.
//   조합 루트(GameBootstrapper)가 구현체를 주입한다(SpecialAttackConfig 주입과 동일 패턴).
//
// 종족 키 분기(규칙 1·6):
//   Spirit(MagicSpirit)과 Transcendence(WillowShrine)가 같은 enum(MagicBuilding)을 공유하므로,
//   로드아웃은 enum이 아니라 RaceId로 분기한다. 각 종족은 스킬 건물이 정확히 1종이라
//   RaceId만으로 로드아웃이 유일하게 결정된다.
//
// Application 레이어 — Domain 의존. Unity/Netcode 직접 참조 없음.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 종족별 스킬 로드아웃을 조회하는 인터페이스(구현은 Infrastructure의 SkillLoadoutConfig).
    /// </summary>
    public interface ISkillDataProvider
    {
        /// <summary>
        /// 지정한 종족의 스킬 로드아웃(슬롯 순서대로, 최대 5개)을 반환한다.
        /// 정의가 없으면 빈 목록을 반환한다(null 아님).
        /// </summary>
        /// <param name="race">조회할 종족.</param>
        /// <returns>슬롯 순서대로 나열된 SkillData 목록(0-based 인덱스 = 슬롯 번호).</returns>
        IReadOnlyList<SkillData> GetLoadout(RaceId race);
    }
}
