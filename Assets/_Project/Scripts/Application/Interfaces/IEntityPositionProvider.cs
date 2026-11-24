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
using System.Collections.Generic;
using Hexiege.Domain;

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
    /// <summary>
    /// 선택형 최적화 capability. 기존 위치 getter나 임의 provider의 의미는 바꾸지 않는다.
    /// 후보 목록은 EndUnitCandidates까지 단일 호출이 대여하며, 지원하지 못하면 false로 원래 검색을 쓴다.
    /// </summary>
    public interface ICombatUnitCandidateSource
    {
        bool TryBeginUnitCandidates(IReadOnlyDictionary<int, UnitData> units, ulong rosterRevision,
            Vector3 center, float radius, out IReadOnlyList<UnitData> candidates);
        void EndUnitCandidates();
        void UnitPoseChanging(UnitData unit, int rootInstanceId);
        void UnitPoseChanged(UnitData unit, int rootInstanceId, Vector3 worldPosition);
        void UnitBindingChanged(int unitId);
        void ClearUnitBindings();
        void ObserveCombatIndexEnd();
        void RecordQueryComparison(bool matched, int indexedInspections, int baselineInspections,
            bool faulted, int indexedId, int baselineId, int category);
    }

}
