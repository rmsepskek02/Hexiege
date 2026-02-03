// ============================================================================
// UnitType.cs
// 유닛 종류를 구분하는 열거형.
//
// 프로토타입에서는 Pistoleer(권총병) 1종만 사용.
// MVP에서 Gunner(기관총병), Sniper(저격총병) 등 추가 예정.
//
// 용도:
//   - UnitData에서 유닛의 종류 식별
//   - UnitFactory에서 프리팹 선택 시 키로 사용
//   - UnitAnimationData ScriptableObject 매핑
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

namespace Hexiege.Domain
{
    public enum UnitType
    {
        Pistoleer = 0   // 권총병: 빠른 생산(5초), 낮은 스탯 (GDD 기준)
    }
}
