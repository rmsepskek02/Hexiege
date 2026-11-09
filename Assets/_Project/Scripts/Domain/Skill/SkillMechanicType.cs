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
//        타입 C 전용 실행기(GlobalStatusChangeExecutor)가 있고, 실행기 레지스트리(SkillExecutorRegistry)에
//        타입 A·B·C 세 실행기가 등록된다.
//   - AreaTowerDisable  : 지정한 지점의 원형 범위 안 "적 방어탑"을 지속시간 동안 공격 불가로 만든다.
//        (2026-11-04 EMP Task 커밋 1 — 인간 지원 스킬 EMP 전용. 실행기 = AreaTowerDisableExecutor.)
//        유닛에게는 아무 영향이 없다. 상태는 건물 전용 저장소(BuildingStatusEffectSystem)에 걸린다.
//
//   ⚠️ 정수 값 고정: 스킬 자산(SkillDefinition._mechanic)은 이 열거형을 "정수"로 저장한다.
//      기존 멤버(0·1·2)의 값을 바꾸면 이미 만든 자산이 다른 타입으로 읽힌다. 새 멤버는 항상 맨 뒤에 추가한다.
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
        /// </summary>
        GlobalStatusChange = 2,

        /// <summary>
        /// 지점 범위 적 방어탑 무력화(EMP). 조준 지점의 원형 반경 안에 있는 적 방어탑(AutoTower)을
        /// 지속시간 동안 공격하지 못하게 한다. 대상은 발동 순간에 정해진다(스냅샷). 유닛은 영향 없음.
        /// 2026-11-04 EMP Task(Plan 3-1 M1)에서 추가 — 값 3은 맨 뒤 추가라 기존 자산(0·1·2)과 겹치지 않는다.
        /// </summary>
        AreaTowerDisable = 3
    }
}
