using Hexiege.Infrastructure;
using UnityEngine;

namespace Hexiege.Presentation
{
    public interface IPresentationPoseProvider
    {
        Transform GetUnitTransform(int unitId);
        Transform GetBuildingTransform(int buildingId);
        Vector3 GetUnitPosition(int unitId);
        Vector3 GetBuildingPosition(int buildingId);
    }

    /// <summary>
    /// Resolves visual-space poses without changing the simulation-space provider used by
    /// movement, range, targeting, or the A2 authoritative pose seam.
    /// </summary>
    public sealed class PresentationPoseProvider : IPresentationPoseProvider
    {
        private readonly UnitFactory _unitFactory;
        private readonly BuildingFactory _buildingFactory;

        public PresentationPoseProvider(
            UnitFactory unitFactory,
            BuildingFactory buildingFactory)
        {
            _unitFactory = unitFactory;
            _buildingFactory = buildingFactory;
        }

        public Transform GetUnitTransform(int unitId)
        {
            GameObject unit = _unitFactory != null
                ? _unitFactory.GetUnitObject(unitId)
                : null;
            if (unit == null)
                return null;

            VisualRootProjector projector = unit.GetComponent<VisualRootProjector>();
            return projector != null
                ? projector.PresentationTransform
                : unit.transform;
        }

        public Transform GetBuildingTransform(int buildingId)
        {
            GameObject building = _buildingFactory != null
                ? _buildingFactory.GetBuildingObject(buildingId)
                : null;
            return building != null ? building.transform : null;
        }

        public Vector3 GetUnitPosition(int unitId)
        {
            Transform value = GetUnitTransform(unitId);
            return value != null ? value.position : Vector3.zero;
        }

        public Vector3 GetBuildingPosition(int buildingId)
        {
            Transform value = GetBuildingTransform(buildingId);
            return value != null ? value.position : Vector3.zero;
        }
    }
}
