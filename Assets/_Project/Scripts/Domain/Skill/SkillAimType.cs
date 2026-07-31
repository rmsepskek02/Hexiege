// ============================================================================
// SkillAimType.cs
// 스킬의 "조준 방식(발동 경로)"을 구분하는 열거형.
//
// 무엇을 구분하나(초급자용 설명):
//   스킬을 발동하는 방법은 두 가지다.
//   - Instant     : 버튼을 탭하는 즉시 발동(조준 단계 없음). 타입 C(전역 상태변경)가 사용. (규칙 15)
//   - PointTarget : 버튼을 누른 채 지도의 한 지점을 조준해 발동. 타입 A·B(범위 피해/장판)가 사용. (규칙 16)
//
//   조준 방식에 따라 입력 흐름이 달라진다:
//   - Instant     → SkillActivationUseCase.Activate(buildingId, slot, aimCoord=null)
//   - PointTarget → 조준 컨트롤러(SkillAimController)로 좌표를 만든 뒤 Activate(..., aimCoord)
//
// Domain 레이어 — 순수 C#. UnityEngine / Hexiege.Core 참조 금지.
// ============================================================================

namespace Hexiege.Domain
{
    /// <summary>
    /// 스킬 조준 방식. Instant=즉시 발동(조준 없음), PointTarget=지점 지정 발동(규칙 15·16).
    /// </summary>
    public enum SkillAimType
    {
        /// <summary> 즉시 발동 — 버튼 탭 즉시 실행, 조준 단계 없음(타입 C). 규칙 15. </summary>
        Instant = 0,

        /// <summary> 지점 지정 발동 — 지도 한 지점을 조준해 실행(타입 A·B). 규칙 16. </summary>
        PointTarget = 1
    }
}
