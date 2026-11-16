// ============================================================================
// BuildingStatusVfxTargetPolicy.cs
// 지원 스킬 「건물 상태 VFX」를 "어느 건물에" 붙일지 정하는 순수 규칙(화면 장치 BuildingStatusVfxPresenter가 쓴다).
// 2026-11-17 지원 스킬 새 상태 Task(_Tasks/2026-11-16/01_00_support-skill-new-statuses) Plan 6-1-9 (1) — S-10 사용자 결정
//   「3번 아군 생산건물 전부에 붙어야 좋을거같아」(전술 지령 건물용 이펙트 = 아군 생산 건물 전부).
//
// 왜 따로 뺐나(초급자용 설명):
//   건물 이펙트를 붙이는 스킬이 두 종류가 되었는데, 붙이는 대상의 기준이 서로 다르다.
//     ① 신목의 각성(건물 쉴드): 서버가 「발동 순간 살아 있던 아군 건물 전부」에 쉴드를 건다. 그 뒤 새로 지은 건물에는 없다(S-11).
//        → 이펙트도 발동 순간 건물에만(ActivationBuildings).
//     ② 전술 지령(생산 속도 ×1.5): 서버 효과가 「팀 단위」라 효과 중 새로 짓거나 업그레이드한 생산 건물도 빨라진다(S-9).
//        → 이펙트도 아군 「생산 건물」 전부, 효과 중 새로 생긴 생산 건물 포함(ProductionBuildings).
//   이 판단을 화면 MonoBehaviour 안에 섞어 두면 자가 검증(Editor 회귀 스위트)이 Unity 씬 없이 확인할 수 없다.
//   그래서 Unity · 전역 스탯 표(UnitStats · BuildingStats)에 기대지 않는 정적 함수로 분리했다.
//
// 「생산 건물」의 정의(코드 근거):
//   BuildingTypeHelper.IsProductionBuilding — 종족별 생산 라인 표(_buildingTable)에 있는 건물 = 1 · 2 · 3단계(업그레이드 형태) 모두.
//   서버 생산 진행(UnitProductionUseCase.RegisterBarracks)과 화면 생산 등록(ProductionTicker)도 같은 함수로 「생산 건물」을 고르므로
//   생산 속도 효과가 실제로 적용되는 건물과 이펙트가 붙는 건물이 정확히 같다.
//
// Application 레이어 — 순수 C#(Domain만 참조). 서버 판정 · 스키마 · RPC와 무관한 "표현 대상 선정" 규칙이다.
// ============================================================================

using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 건물 상태 VFX를 붙이는 대상 기준.
    /// </summary>
    public enum BuildingStatusVfxScope
    {
        /// <summary> 건물 이펙트를 붙이지 않는 스킬(포격 · EMP · 강화 4종 · 대지의 갑주 등). </summary>
        None = 0,

        /// <summary> 발동 순간 살아 있는 대상 팀 건물 전부(효과 중 새 건물 제외) — 「건물 각각」 효과(신목의 각성 쉴드). </summary>
        ActivationBuildings = 1,

        /// <summary> 대상 팀의 생산 건물 전부(효과 중 새로 짓거나 업그레이드한 생산 건물 포함) — 팀 단위 생산 속도 효과(전술 지령). </summary>
        ProductionBuildings = 2,
    }

    /// <summary>
    /// 건물 상태 VFX 대상 선정 규칙(순수 함수 — 화면 장치와 자가 검증이 함께 쓴다).
    /// </summary>
    public static class BuildingStatusVfxTargetPolicy
    {
        /// <summary>
        /// 스킬 데이터로 건물 이펙트의 대상 기준을 정한다.
        /// ① 타입 C(전역 상태변경)가 아니면 None.
        /// ② 「건물 각각」 대상 효과가 있으면 ActivationBuildings — 단계 C에서 만든 기존 동작 그대로(먼저 본다 → 신목의 각성은 바뀌지 않는다).
        /// ③ 「팀」 대상 생산 속도 효과가 있으면 ProductionBuildings(전술 지령).
        /// ④ 그 밖에는 None.
        /// </summary>
        /// <param name="skill">발동한 스킬 데이터. null이면 None.</param>
        /// <returns>대상 기준.</returns>
        public static BuildingStatusVfxScope Resolve(SkillData skill)
        {
            if (skill == null || skill.Mechanic != SkillMechanicType.GlobalStatusChange) return BuildingStatusVfxScope.None;
            if (skill.HasBuildingEffect()) return BuildingStatusVfxScope.ActivationBuildings;
            // 서버도 「팀」 대상 생산 속도만 받는다(SupportSkillEffectSystem.IsSupported) — 같은 조건이면 화면 Client 거울
            //   (UnitProductionUseCase.MirrorProductionSpeedFromSkill)과도 같은 판단이 된다.
            if (skill.TryFindEffect(StatusEffectKind.ProductionSpeedMul, out SkillEffectTarget target, out _)
                && target == SkillEffectTarget.Team)
                return BuildingStatusVfxScope.ProductionBuildings;
            return BuildingStatusVfxScope.None;
        }

        /// <summary>
        /// 이 건물이 대상 기준에 맞는 이펙트 대상인가(살아 있음 · 대상 팀 · 생산 기준이면 생산 건물).
        /// 건물 HP 값이나 전역 스탯 표를 읽지 않는다(IsAlive · Team · Type만).
        /// </summary>
        /// <param name="scope">대상 기준.</param>
        /// <param name="building">검사할 건물. null이면 false.</param>
        /// <param name="targetTeam">효과를 받는 팀(SkillData.ResolveTargetTeam 결과).</param>
        /// <returns>대상이면 true.</returns>
        public static bool IsTarget(BuildingStatusVfxScope scope, BuildingData building, TeamId targetTeam)
        {
            if (scope == BuildingStatusVfxScope.None || building == null || !building.IsAlive || building.Team != targetTeam) return false;
            if (scope == BuildingStatusVfxScope.ProductionBuildings) return BuildingTypeHelper.IsProductionBuilding(building.Type);
            return true; // ActivationBuildings — 종류와 무관하게 대상 팀 건물 전부(단계 C 기존 동작).
        }

        /// <summary>
        /// 효과 중에 새로 생긴 건물(건설 · 업그레이드)에도 이펙트를 붙이는 기준인가.
        /// ActivationBuildings는 발동 순간 대상만(S-11)이라 false, ProductionBuildings는 팀 단위 효과라 true.
        /// </summary>
        /// <param name="scope">대상 기준.</param>
        /// <returns>효과 중 새 건물에도 붙이면 true.</returns>
        public static bool AttachesLateBuildings(BuildingStatusVfxScope scope)
            => scope == BuildingStatusVfxScope.ProductionBuildings;

        /// <summary> 진단 로그의 Scope 값(고정 문자열 — 할당 없음). </summary>
        /// <param name="scope">대상 기준.</param>
        /// <returns>진단용 이름.</returns>
        public static string DiagName(BuildingStatusVfxScope scope)
        {
            switch (scope)
            {
                case BuildingStatusVfxScope.ActivationBuildings: return "ActivationBuildings";
                case BuildingStatusVfxScope.ProductionBuildings: return "ProductionBuildings";
                default: return "None";
            }
        }
    }
}
