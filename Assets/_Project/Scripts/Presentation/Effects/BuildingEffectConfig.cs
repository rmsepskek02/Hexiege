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

        [Tooltip("공격(발사) 시 재생할 이펙트 (VFX). 없으면 비워둠 — 조용히 스킵됨. 방어 타워(AutoTower) 발사 연출용")]
        public EffectPreset attackPreset;
    }

    // ========================================================================
    // BuildingEffectConfig
    // 모든 BuildingType의 BuildingEffectEntry를 모으는 ScriptableObject.
    // ========================================================================
    [CreateAssetMenu(fileName = "BuildingEffectConfig", menuName = "Hexiege/Effects/BuildingEffectConfig")]
    public class BuildingEffectConfig : ScriptableObject
    {
        [System.Serializable]
        public struct RaceAttack { public RaceId race; public EffectPreset preset; }
        [SerializeField] private List<RaceAttack> _autoTowerAttacks = new List<RaceAttack>();
        public EffectPreset GetAttack(BuildingType type, RaceId race)
        {
            if (type != BuildingType.AutoTower) return GetAttack(type);
            if (race == RaceId.Transcendence) return null;
            foreach (var entry in _autoTowerAttacks) if (entry.race == race) return entry.preset;
            return null;
        }
        /// <summary>
        /// [2026-10-21 서버 권위 투사체 단계 C] 투사체 타워(종족별)의 투사체(트레이서)·착탄 이펙트 프리셋 한 줄.
        /// 사용자 결정(Q10 ①): 인간 CannonTower는 CannonCart의 투사체(EffectPreset_Cannon_Tracer)·
        /// 착탄(EffectPreset_Cannon_Impact) 프리셋을 그대로 쓴다. 발사(총구) 이펙트는 위 _autoTowerAttacks의 프리셋이다.
        /// </summary>
        [System.Serializable]
        public struct RaceProjectile
        {
            [Tooltip("이 줄이 적용되는 타워 종족")]
            public RaceId race;

            [Tooltip("서버 투사체 비행 중 보이는 트레이서 프리셋(TracerProjectile이 붙은 VFX). 비우면 투사체 없이 착탄만 보인다")]
            public EffectPreset tracerPreset;

            [Tooltip("서버 착탄 지점(대상 표면)에서 1회 재생할 착탄 프리셋. 비우면 착탄 이펙트 없음")]
            public EffectPreset impactPreset;
        }

        [Tooltip("[단계 C] 투사체 타워 종족별 투사체·착탄 프리셋. 지금은 Human(CannonTower)만 — CannonCart 프리셋 재사용")]
        [SerializeField] private List<RaceProjectile> _autoTowerProjectiles = new List<RaceProjectile>();

        /// <summary>[단계 C] 투사체 타워의 트레이서 프리셋. AutoTower가 아니거나 줄이 없으면 null.</summary>
        /// <param name="type">건물 종류.</param>
        /// <param name="race">타워 종족.</param>
        public EffectPreset GetTowerTracer(BuildingType type, RaceId race)
        {
            if (type != BuildingType.AutoTower || _autoTowerProjectiles == null) return null;
            foreach (var entry in _autoTowerProjectiles) if (entry.race == race) return entry.tracerPreset;
            return null;
        }

        /// <summary>[단계 C] 투사체 타워의 착탄 프리셋. AutoTower가 아니거나 줄이 없으면 null.</summary>
        /// <param name="type">건물 종류.</param>
        /// <param name="race">타워 종족.</param>
        public EffectPreset GetTowerImpact(BuildingType type, RaceId race)
        {
            if (type != BuildingType.AutoTower || _autoTowerProjectiles == null) return null;
            foreach (var entry in _autoTowerProjectiles) if (entry.race == race) return entry.impactPreset;
            return null;
        }

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

        /// <summary>
        /// 지정한 건물 타입의 공격(발사) 이펙트 프리셋을 반환.
        /// 항목이 없거나 프리셋이 비어있으면 null (호출부에서 조용히 스킵).
        /// 방어 타워(AutoTower)가 적을 공격하는 순간의 발사 연출용.
        /// </summary>
        /// <param name="type">조회할 건물 타입.</param>
        /// <returns>공격 EffectPreset 또는 null.</returns>
        public EffectPreset GetAttack(BuildingType type)
        {
            if (_cache == null) return null;
            return _cache.TryGetValue(type, out BuildingEffectEntry entry) ? entry.attackPreset : null;
        }
    }
}
