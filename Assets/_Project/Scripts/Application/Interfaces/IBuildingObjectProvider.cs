// ============================================================================
// IBuildingObjectProvider.cs
// 건물 생성·파괴 기능을 노출하지 않고, ID로 씬 오브젝트를 조회하는 기능만 제공한다.
// ============================================================================

using UnityEngine;

namespace Hexiege.Application
{
    /// <summary>
    /// 타겟 건물의 씬 오브젝트를 조회하는 인터페이스.
    /// Infrastructure의 BuildingFactory가 구현하며 View에는 조회 기능만 제공한다.
    /// </summary>
    public interface IBuildingObjectProvider
    {
        /// <summary>
        /// 건물 ID에 해당하는 씬 내 GameObject를 반환한다.
        /// 등록된 건물 오브젝트가 없으면 null을 반환한다.
        /// </summary>
        /// <param name="buildingId">조회할 건물의 ID.</param>
        /// <returns>등록된 건물 오브젝트 또는 null.</returns>
        GameObject GetBuildingObject(int buildingId);
    }
}
