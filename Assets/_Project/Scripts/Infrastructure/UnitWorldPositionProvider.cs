// ============================================================================
// UnitWorldPositionProvider.cs
// IEntityPositionProvider 구현체.
// UnitFactory/BuildingFactory의 GameObject Dictionary를 통해
// 유닛/건물의 실시간 Transform.position을 반환.
//
// Lerp 중에도 정확한 시각 위치를 반환하므로
// HexCoord 기반 판정의 "타일 완료 후에만 갱신" 문제를 해결.
//
// Infrastructure 레이어 — Unity 의존 (GameObject, Transform).
// ============================================================================

using UnityEngine;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// UnitFactory/BuildingFactory를 통해 유닛/건물의 월드 좌표를 제공.
    /// </summary>
    public class UnitWorldPositionProvider : IEntityPositionProvider
    {
        private readonly UnitFactory _unitFactory;
        private readonly BuildingFactory _buildingFactory;

        public UnitWorldPositionProvider(UnitFactory unitFactory, BuildingFactory buildingFactory)
        {
            _unitFactory = unitFactory;
            _buildingFactory = buildingFactory;
        }

        /// <summary>
        /// 유닛의 현재 월드 좌표 반환. GameObject가 파괴되었으면 Vector3.zero.
        /// </summary>
        public Vector3 GetUnitWorldPosition(int unitId)
        {
            var obj = _unitFactory != null ? _unitFactory.GetUnitObject(unitId) : null;
            return obj != null ? obj.transform.position : Vector3.zero;
        }

        /// <summary>
        /// 건물의 현재 월드 좌표 반환. GameObject가 파괴되었으면 Vector3.zero.
        /// </summary>
        public Vector3 GetBuildingWorldPosition(int buildingId)
        {
            var obj = _buildingFactory != null ? _buildingFactory.GetBuildingObject(buildingId) : null;
            return obj != null ? obj.transform.position : Vector3.zero;
        }
    }
}
