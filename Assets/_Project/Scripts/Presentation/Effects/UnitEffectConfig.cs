// ============================================================================
// UnitEffectConfig.cs
// UnitType(유닛 종류)별 공격/사망 이펙트를 Inspector에서 설정하는 ScriptableObject.
//
// 구조:
//   UnitStatsConfig와 동일한 List<Entry> 패턴.
//   각 항목(UnitEffectEntry)은 "유닛 종류 + 공격 프리셋 + 사망 프리셋"을 묶는다.
//
// 조회 성능:
//   런타임에 List를 매번 순회하면 느리므로, Initialize()에서 한 번
//   Dictionary<UnitType, UnitEffectEntry>로 캐싱하여 O(1) 조회를 보장한다.
//
// 사용 흐름:
//   GameBootstrapper가 이 에셋을 EffectManager.Initialize()에 전달 →
//   EffectManager가 Initialize()를 호출해 Dictionary를 빌드 →
//   이후 PlayUnitAttack/PlayUnitDeath에서 GetAttack/GetDeath로 프리셋 조회.
//
// 에셋 경로: Assets/_Project/Resources/Config/UnitEffectConfig.asset
//
// Presentation 레이어 — Unity 의존 허용 (ScriptableObject).
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    // ========================================================================
    // UnitEffectEntry
    // 한 유닛 종류(UnitType)에 대한 공격/사망 이펙트 프리셋을 묶은 구조체.
    // List<UnitEffectEntry> 형태로 UnitEffectConfig에 보관되며 Inspector에서 편집.
    //
    // [System.Serializable]이 있어야 Inspector에 노출됨.
    // ========================================================================
    [System.Serializable]
    public struct UnitEffectEntry
    {
        [Tooltip("이 항목이 적용되는 유닛 타입")]
        public UnitType unitType;

        [Tooltip("공격 시 재생할 이펙트 (VFX + SFX). 없으면 비워둠")]
        public EffectPreset attackPreset;

        [Tooltip("사망 시 재생할 이펙트 (VFX + SFX). 없으면 비워둠")]
        public EffectPreset deathPreset;
    }

    // ========================================================================
    // UnitEffectConfig
    // 모든 UnitType의 UnitEffectEntry를 한곳에 모으는 ScriptableObject.
    // ========================================================================
    [CreateAssetMenu(fileName = "UnitEffectConfig", menuName = "Hexiege/Effects/UnitEffectConfig")]
    public class UnitEffectConfig : ScriptableObject
    {
        [Tooltip("각 유닛 타입별 공격/사망 이펙트 프리셋. Inspector에서 편집")]
        [SerializeField] private List<UnitEffectEntry> _entries = new List<UnitEffectEntry>();

        /// <summary>
        /// 빠른 조회를 위한 캐시. Initialize()에서 빌드.
        /// 중복 키가 있으면 처음 항목만 유지.
        /// </summary>
        private Dictionary<UnitType, UnitEffectEntry> _cache;

        /// <summary>
        /// List를 Dictionary로 변환하여 O(1) 조회를 준비한다.
        /// EffectManager.Initialize()에서 호출.
        /// 여러 번 호출되어도 안전하도록 매번 새로 빌드한다.
        /// </summary>
        public void Initialize()
        {
            _cache = new Dictionary<UnitType, UnitEffectEntry>(_entries.Count);
            foreach (UnitEffectEntry entry in _entries)
            {
                // 중복 unitType이 들어와도 예외 없이 첫 항목만 유지.
                if (!_cache.ContainsKey(entry.unitType))
                    _cache.Add(entry.unitType, entry);
            }
        }

        /// <summary>
        /// 지정한 유닛 타입의 공격 이펙트 프리셋을 반환.
        /// 항목이 없거나 프리셋이 비어있으면 null.
        /// </summary>
        /// <param name="type">조회할 유닛 타입.</param>
        /// <returns>공격 EffectPreset 또는 null.</returns>
        public EffectPreset GetAttack(UnitType type)
        {
            // Initialize() 전에 호출되면 캐시가 없으므로 안전하게 null 반환.
            if (_cache == null) return null;
            return _cache.TryGetValue(type, out UnitEffectEntry entry) ? entry.attackPreset : null;
        }

        /// <summary>
        /// 지정한 유닛 타입의 사망 이펙트 프리셋을 반환.
        /// 항목이 없거나 프리셋이 비어있으면 null.
        /// </summary>
        /// <param name="type">조회할 유닛 타입.</param>
        /// <returns>사망 EffectPreset 또는 null.</returns>
        public EffectPreset GetDeath(UnitType type)
        {
            if (_cache == null) return null;
            return _cache.TryGetValue(type, out UnitEffectEntry entry) ? entry.deathPreset : null;
        }
    }
}
