// ============================================================================
// BuildingEffectConfig.cs
// BuildingType(건물 종류)별 파괴/업그레이드 이펙트를 설정하는 ScriptableObject.
//
// 구조:
//   UnitEffectConfig와 동일한 List<Entry> + Dictionary 캐싱 패턴.
//   각 항목(BuildingEffectEntry)은 "건물 종류 + 파괴 프리셋 + 업그레이드 프리셋"을 묶는다.
//
// 에셋 경로: Assets/_Project/Resources/Config/BuildingEffectConfig.asset
//
// Presentation 레이어 — Unity 의존 허용 (ScriptableObject).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    // ========================================================================
    // BuildingEffectEntry
    // 한 건물 종류(BuildingType)에 대한 파괴/업그레이드 이펙트 프리셋 묶음.
    // ========================================================================
    [System.Serializable]
    public struct BuildingEffectEntry
    {
        [Tooltip("이 항목이 적용되는 건물 타입")]
        public BuildingType buildingType;

        [Tooltip("파괴 시 재생할 이펙트 (VFX + SFX). 없으면 비워둠")]
        public EffectPreset destroyPreset;

        [Tooltip("업그레이드 시 재생할 이펙트 (VFX + SFX). 없으면 비워둠")]
        public EffectPreset upgradePreset;
    }

    // ========================================================================
    // BuildingEffectConfig
    // 모든 BuildingType의 BuildingEffectEntry를 모으는 ScriptableObject.
    // ========================================================================
    [CreateAssetMenu(fileName = "BuildingEffectConfig", menuName = "Hexiege/Effects/BuildingEffectConfig")]
    public class BuildingEffectConfig : ScriptableObject
    {
        [Tooltip("각 건물 타입별 파괴/업그레이드 이펙트 프리셋. Inspector에서 편집")]
        [SerializeField] private List<BuildingEffectEntry> _entries = new List<BuildingEffectEntry>();

        /// <summary> 빠른 조회를 위한 캐시. Initialize()에서 빌드. </summary>
        private Dictionary<BuildingType, BuildingEffectEntry> _cache;

        /// <summary>
        /// List를 Dictionary로 변환하여 O(1) 조회를 준비한다.
        /// EffectManager.Initialize()에서 호출.
        /// </summary>
        public void Initialize()
        {
            _cache = new Dictionary<BuildingType, BuildingEffectEntry>(_entries.Count);
            foreach (BuildingEffectEntry entry in _entries)
            {
                if (!_cache.ContainsKey(entry.buildingType))
                    _cache.Add(entry.buildingType, entry);
            }
        }

        /// <summary>
        /// 지정한 건물 타입의 파괴 이펙트 프리셋을 반환.
        /// 항목이 없거나 프리셋이 비어있으면 null.
        /// </summary>
        /// <param name="type">조회할 건물 타입.</param>
        /// <returns>파괴 EffectPreset 또는 null.</returns>
        public EffectPreset GetDestroy(BuildingType type)
        {
            if (_cache == null) return null;
            return _cache.TryGetValue(type, out BuildingEffectEntry entry) ? entry.destroyPreset : null;
        }

        /// <summary>
        /// 지정한 건물 타입의 업그레이드 이펙트 프리셋을 반환.
        /// 항목이 없거나 프리셋이 비어있으면 null.
        /// </summary>
        /// <param name="type">조회할 건물 타입.</param>
        /// <returns>업그레이드 EffectPreset 또는 null.</returns>
        public EffectPreset GetUpgrade(BuildingType type)
        {
            if (_cache == null) return null;
            return _cache.TryGetValue(type, out BuildingEffectEntry entry) ? entry.upgradePreset : null;
        }
    }
}
