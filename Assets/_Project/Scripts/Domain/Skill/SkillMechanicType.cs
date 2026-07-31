// ============================================================================
// SkillMechanicType.cs
// 스킬(스킬 건물 액티브 능력)의 "실행 방식(메커니즘)"을 구분하는 열거형.
//
// 무엇을 구분하나(초급자용 설명):
//   스킬은 효과가 서로 근본적으로 달라(즉발 범위 피해 / 장판 지속 피해 / 전역 상태변경)
//   각 타입마다 별도의 "실행기(executor)"가 처리한다. 이 열거형은 "어떤 실행기를 쓸지"를
//   가리키는 키다. 실제 수치(반경·피해·지속)는 데이터(ScriptableObject)로 주입한다.
//
//   - InstantAreaDamage (A): 지정한 지점의 원형 범위에 1회(즉발) 피해. (규칙 11)
//   - AreaDotDamage     (B): 지정한 지점의 원형 범위에 지속시간 동안 도트(DoT) 피해. (규칙 12)
//   - GlobalStatusChange(C): 전역 상태변경(버프/디버프/제어/회복). (규칙 13)
//        ⚠️ Phase 1에서는 "선언만" 두고 실행기는 구현하지 않는다(Phase 2에서 구현).
//           enum 멤버는 미리 선언해 두어 데이터 스키마(SkillDefinition)가 안정적으로
//           직렬화되도록 하고, 실행기 레지스트리에는 Phase 1엔 A·B만 등록한다.
//
// Domain 레이어 — 순수 C#. UnityEngine / Hexiege.Core 참조 금지.
// ============================================================================

namespace Hexiege.Domain
{
    /// <summary>
    /// 스킬의 실행 방식(메커니즘) 타입. 타입별로 전용 실행기가 처리한다(규칙 11~13).
    /// </summary>
    public enum SkillMechanicType
    {
        /// <summary> 타입 A — 즉발 범위 피해(지점 지정, 원형 반경, 1회 피해). 규칙 11. </summary>
        InstantAreaDamage = 0,

        /// <summary> 타입 B — 범위 지속 피해 장판(지점 지정, 원형 반경, 지속 DoT). 규칙 12. </summary>
        AreaDotDamage = 1,

        /// <summary>
        /// 타입 C — 전역 상태변경(버프/디버프/제어/회복). 규칙 13.
        /// Phase 1에서는 멤버 선언만 존재하며 실행기는 미구현(Phase 2 구현 예정).
        /// </summary>
        GlobalStatusChange = 2
    }
}
