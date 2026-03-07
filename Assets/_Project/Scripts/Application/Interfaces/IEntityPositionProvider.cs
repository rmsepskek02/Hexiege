// ============================================================================
// IEntityPositionProvider.cs
// 유닛/건물의 실제 월드 좌표를 반환하는 인터페이스.
//
// Application 레이어에서 Infrastructure(UnitFactory 등)에 직접 의존하지 않도록
// 인터페이스로 추상화. UnitWorldPositionProvider가 구현.
//
// Application 레이어 — Unity 의존 (Vector3만 사용).
// ============================================================================

using UnityEngine;

namespace Hexiege.Application
{
    /// <summary>
    /// 유닛/건물의 실제 월드 좌표(Transform.position)를 반환하는 인터페이스.
    /// Lerp 중에도 시각적 위치를 정확히 반환하여 사거리 판정에 사용.
    /// </summary>
    public interface IEntityPositionProvider
    {
        /// <summary>
        /// 유닛의 현재 월드 좌표 반환. GameObject가 없으면 Vector3.zero.
        /// </summary>
        Vector3 GetUnitWorldPosition(int unitId);

        /// <summary>
        /// 건물의 현재 월드 좌표 반환. GameObject가 없으면 Vector3.zero.
        /// </summary>
        Vector3 GetBuildingWorldPosition(int buildingId);
    }
}
