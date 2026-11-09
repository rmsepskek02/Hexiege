// ============================================================================
// BuildingEffectConfig.cs
// BuildingType(건물 종류)별 파괴/업그레이드 이펙트와, 방어 타워의 종족별 「공격 표현 프로필」을 설정하는 ScriptableObject.
//
// 구조:
//   UnitEffectConfig와 동일한 List<Entry> + Dictionary 캐싱 패턴.
//   각 항목(BuildingEffectEntry)은 "건물 종류 + 파괴 프리셋 + 업그레이드 프리셋"을 묶는다.
//
// 방어 타워 공격 표현 프로필 (2026-11-03 추가 — 타워 공격 표현·소리 개편):
//   _towerAttackProfiles 목록의 한 줄 = 한 종족 방어 타워의 공격 표현(TowerAttackProfile.cs 참조).
//   공격 방식(투사체/번개/지면 분출) · 재생 기준점 · 발사/투사체/착탄 프리셋을 담는다.
//   화면 연출 코드는 종족 이름 대신 이 프로필의 「공격 방식」을 보고 재생 방법을 고른다.
//   종족 줄이 없거나 세 프리셋이 모두 비어 있으면 그 종족 타워는 공격 표현을 재생하지 않으며,
//   다른 종족 프로필로 대체(fallback)하지 않는다.
//   계획 원본: Assets/_Project/Docs/_Tasks/2026-11-02/15_48_tower-attack-presentation-restructure/Plan.md 3·4-2절
//
// 방어탑 무력화 상태 VFX (2026-11-04 추가 — EMP Task 커밋 3):
//   _towerDisabledPresets 목록의 한 줄 = 한 종족 방어탑이 EMP로 멈췄을 때 탑 위에 띄울 상태 VFX(V6) 프리셋.
//   공격 프로필과는 별도 칸이다(TowerDisabledPresetEntry 주석 참조).
//   계획 원본: Assets/_Project/Docs/_Tasks/2026-11-04/03_54_emp-tower-disable/Plan.md 3-5절(P1)
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

        [Tooltip("파괴 시 재생할 이펙트 (VFX). 없으면 비워둠")]
        public EffectPreset destroyPreset;

        [Tooltip("업그레이드 시 재생할 이펙트 (VFX). 없으면 비워둠")]
        public EffectPreset upgradePreset;

        // (옛 attackPreset 칸은 2026-11-03 타워 공격 표현 개편에서 삭제 — 타워 발사 프리셋은 BuildingEffectConfig._towerAttackProfiles 담당.)
    }

    // ========================================================================
    // TowerDisabledPresetEntry (2026-11-04 EMP Task 커밋 3 — Plan 3-5 P1, 사용자 결정 Q9)
    // 한 종족 방어탑이 EMP로 무력화됐을 때 탑 위에 띄울 상태 VFX(V6) 프리셋 한 줄.
    //
    // 왜 종족마다 한 줄인가(초급자용 설명):
    //   V6 프리팹(vfx_emp_disabled)은 하나지만 탑 모델은 종족마다 크기 · 높이가 다르다
    //   (인간 대포 타워 · 정령 룬 첨탑 · 초월 덩굴 타워). 그래서 같은 프리팹을 담은 EffectPreset을 종족마다 하나씩 두고,
    //   프리셋의 Scale Multiplier(크기)와 Local Position Offset(높이)만 종족별로 맞춘다.
    //
    // 모양의 출처:
    //   2026-11-03 개편에서 삭제된 옛 _autoTowerProjectiles(종족 한 줄마다 프리셋, 초월이라고 비우는 분기 없음)와
    //   "같은 모양의 새 칸"이다. 삭제된 칸을 되살린 것이 아니다(사용자 결정 Q9). 공격 프로필(_towerAttackProfiles)과도
    //   섞지 않는다 — VineTower 공격 프로필 줄이 "없는 것" 자체가 「공격 연출 없음」의 뜻이라, 무력화 칸을 넣으려고
    //   그 줄을 만들면 공격 연출 판정과 얽히기 때문이다(Plan P2 비추천 이유).
    //
    // 값 규칙(EffectManager.StartTowerStatusVfx가 읽는 방식):
    //   - 위치 보정은 Y(높이)만 쓴다. X · Z는 무시한다 — 상대 인간 대포탑은 Y축 180° 회전돼 있어
    //     X · Z 보정을 넣으면 모델과 어긋나기 때문이다(「방어 타워 시스템」 규칙 11 · 위험 R-6).
    //   - 회전 보정도 쓰지 않는다(회전 항등 — 원형 프리팹이라 두 화면 모두 같은 모양).
    //   - 탑 높이 측정 결과가 오기 전에는 세 종족 모두 배율 1 · 보정 0으로 둔다(값만 바꾸면 되고 코드는 그대로).
    // ========================================================================
    [System.Serializable]
    public struct TowerDisabledPresetEntry
    {
        [Tooltip("이 줄이 적용되는 방어탑 종족")]
        public RaceId race;

        [Tooltip("이 종족 방어탑이 무력화됐을 때 탑 위에 띄울 상태 VFX 프리셋(반복 재생 프리팹). " +
                 "Scale Multiplier = 크기, Local Position Offset의 Y = 높이(X · Z · 회전 보정은 쓰지 않는다). 비우면 이 종족은 재생 없음")]
        public EffectPreset preset;
    }

    // ========================================================================
    // BuildingEffectConfig
    // 모든 BuildingType의 BuildingEffectEntry와 방어 타워 공격 프로필을 모으는 ScriptableObject.
    // ========================================================================
    [CreateAssetMenu(fileName = "BuildingEffectConfig", menuName = "Hexiege/Effects/BuildingEffectConfig")]
    public class BuildingEffectConfig : ScriptableObject
    {
        // 2026-11-03 타워 공격 표현 개편: 옛 종족별 타워 프리셋 목록 2개(_autoTowerAttacks · _autoTowerProjectiles)는
        //   아래 _towerAttackProfiles로 옮긴 뒤(사용자 테스트 통과) 삭제했다. 옛 값은 커밋 7368d7f1의 에셋에 남아 있다.

        // ====================================================================
        // 방어 타워 공격 프로필 (2026-11-03 — Plan 3-1 · 4-2절)
        // ====================================================================

        [Header("방어 타워 공격 프로필")]
        [Tooltip("종족별 방어 타워(AutoTower)의 공격 표현 한 줄씩. 공격 방식 · 재생 기준점 · 발사/투사체/착탄 프리셋. " +
                 "종족 줄이 없거나 세 프리셋이 모두 비어 있으면 그 타워는 공격 연출을 재생하지 않는다(다른 종족으로 대체하지 않음). " +
                 "같은 종족 줄이 둘 이상이면 위의 첫 줄만 쓴다")]
        [SerializeField] private List<TowerAttackProfile> _towerAttackProfiles = new List<TowerAttackProfile>();

        /// <summary>
        /// 종족 → 타워 공격 프로필 캐시. Initialize()에서 만들고, Initialize 전에 조회가 오면 그때 한 번 만든다
        /// (옛 종족 조회가 Initialize 없이도 동작했으므로 같은 성질을 유지한다 — 에디터 검증 등).
        /// </summary>
        private Dictionary<RaceId, TowerAttackProfile> _towerProfileCache;

        /// <summary>
        /// 이 종족 방어 타워의 공격 프로필을 꺼낸다. 줄이 없거나 세 프리셋이 모두 비어 있으면 false.
        /// </summary>
        /// <param name="race">타워 종족.</param>
        /// <param name="profile">찾은 프로필. false면 default(비어 있음).</param>
        /// <returns>비어 있지 않은 프로필이 있으면 true.</returns>
        public bool TryGetTowerProfile(RaceId race, out TowerAttackProfile profile)
        {
            EnsureTowerProfileCache();
            if (_towerProfileCache.TryGetValue(race, out profile) && !profile.IsEmpty) return true;
            profile = default;
            return false;
        }

        /// <summary>
        /// 이 종족 방어 타워의 공격 방식. 프로필이 비어 있으면 Unset(비어 있음).
        /// 화면 코드(EffectManager · HitPresentationQueue)가 「번개라서 대상을 따라가야 하는가」 같은 판단에 쓴다.
        /// </summary>
        /// <param name="race">타워 종족.</param>
        /// <returns>공격 방식. 비어 있으면 TowerAttackKind.Unset.</returns>
        public TowerAttackKind GetTowerAttackKind(RaceId race)
            => TryGetTowerProfile(race, out TowerAttackProfile profile) ? profile.kind : TowerAttackKind.Unset;

        /// <summary>
        /// 방어 타워의 발사(공격 순간) 이펙트 프리셋 = 그 종족 프로필의 launchPreset.
        /// AutoTower가 아니거나 프로필이 비어 있으면 null(다른 종족으로 대체하지 않는다).
        /// 서명은 옛 종족 조회와 같다 — EffectManager와 에디터 검증이 고치지 않고 그대로 쓰도록(Plan 4-2).
        /// </summary>
        /// <param name="type">건물 종류.</param>
        /// <param name="race">타워 종족.</param>
        /// <returns>발사 EffectPreset 또는 null.</returns>
        public EffectPreset GetAttack(BuildingType type, RaceId race)
        {
            // 방어 타워(AutoTower)가 아닌 건물에는 공격 프로필이 없다.
            if (type != BuildingType.AutoTower) return null;
            return TryGetTowerProfile(race, out TowerAttackProfile profile) ? profile.launchPreset : null;
        }

        /// <summary>
        /// [서버 권위 투사체 단계 C] 투사체 타워의 트레이서 프리셋 = 그 종족 프로필의 tracerPreset.
        /// AutoTower가 아니거나 프로필이 비어 있으면 null.
        /// </summary>
        /// <param name="type">건물 종류.</param>
        /// <param name="race">타워 종족.</param>
        /// <returns>트레이서 EffectPreset 또는 null.</returns>
        public EffectPreset GetTowerTracer(BuildingType type, RaceId race)
        {
            if (type != BuildingType.AutoTower) return null;
            return TryGetTowerProfile(race, out TowerAttackProfile profile) ? profile.tracerPreset : null;
        }

        /// <summary>
        /// [서버 권위 투사체 단계 C] 투사체 타워의 착탄 프리셋 = 그 종족 프로필의 impactPreset.
        /// AutoTower가 아니거나 프로필이 비어 있으면 null.
        /// </summary>
        /// <param name="type">건물 종류.</param>
        /// <param name="race">타워 종족.</param>
        /// <returns>착탄 EffectPreset 또는 null.</returns>
        public EffectPreset GetTowerImpact(BuildingType type, RaceId race)
        {
            if (type != BuildingType.AutoTower) return null;
            return TryGetTowerProfile(race, out TowerAttackProfile profile) ? profile.impactPreset : null;
        }

        /// <summary>
        /// 타워 공격 프로필 목록을 「종족 → 프로필」 사전으로 만든다(아직 없을 때만).
        /// 같은 종족 줄이 둘 이상이면 첫 줄만 쓴다(_entries 캐시와 같은 규칙).
        /// </summary>
        private void EnsureTowerProfileCache()
        {
            if (_towerProfileCache != null) return;
            int count = _towerAttackProfiles != null ? _towerAttackProfiles.Count : 0;
            _towerProfileCache = new Dictionary<RaceId, TowerAttackProfile>(count);
            if (_towerAttackProfiles == null) return;
            foreach (TowerAttackProfile profile in _towerAttackProfiles)
            {
                if (!_towerProfileCache.ContainsKey(profile.race))
                    _towerProfileCache.Add(profile.race, profile);
            }
        }

        /// <summary>
        /// Inspector에서 값을 바꾸면 Unity가 부르는 함수. 타워 프로필 캐시를 비워,
        /// 다음 조회 때 바뀐 목록으로 다시 만들게 한다(Initialize 전 조회가 만든 옛 캐시가 남지 않도록).
        /// </summary>
        private void OnValidate()
        {
            _towerProfileCache = null;
        }

        // ====================================================================
        // 방어탑 무력화 상태 VFX (2026-11-04 EMP Task 커밋 3 — Plan 3-5 P1)
        // ====================================================================

        [Header("방어탑 무력화 상태 VFX (EMP)")]
        [Tooltip("종족별 방어탑(AutoTower)이 EMP로 무력화됐을 때 탑 위에 띄울 상태 VFX 프리셋 한 줄씩. " +
                 "종족 줄이 없거나 프리셋이 비어 있으면 그 종족 탑에는 아무것도 띄우지 않는다(다른 종족으로 대체하지 않음). " +
                 "같은 종족 줄이 둘 이상이면 위의 첫 줄만 쓴다. 위치 보정은 Y(높이)만 쓴다")]
        [SerializeField] private List<TowerDisabledPresetEntry> _towerDisabledPresets = new List<TowerDisabledPresetEntry>();

        /// <summary>
        /// 이 종족 방어탑의 무력화 상태 VFX 프리셋. 줄이 없거나 프리셋이 비어 있으면 null(다른 종족으로 대체하지 않는다).
        /// 옛 _autoTowerProjectiles 조회와 같은 방식 — 목록에서 race가 같은 첫 줄을 그대로 찾고, 종족별로 비우는 분기는 없다.
        /// 줄이 많아야 3개(종족 수)이고 무력화가 걸릴 때만 불리므로 사전(Dictionary) 캐시 없이 목록을 바로 훑는다
        /// (캐시가 없으니 Inspector에서 값을 바꿔도 다음 조회에 바로 반영된다).
        /// </summary>
        /// <param name="race">방어탑 종족.</param>
        /// <returns>무력화 상태 EffectPreset 또는 null.</returns>
        public EffectPreset GetTowerDisabled(RaceId race)
        {
            if (_towerDisabledPresets == null) return null;
            for (int i = 0; i < _towerDisabledPresets.Count; i++)
            {
                if (_towerDisabledPresets[i].race == race)
                    return _towerDisabledPresets[i].preset; // 첫 줄만 쓴다(비어 있으면 null 그대로)
            }
            return null;
        }

        // ====================================================================
        // 건물 종류별 파괴/업그레이드 이펙트
        // ====================================================================

        [Tooltip("각 건물 타입별 파괴/업그레이드 이펙트 프리셋. Inspector에서 편집")]
        [SerializeField] private List<BuildingEffectEntry> _entries = new List<BuildingEffectEntry>();

        /// <summary> 빠른 조회를 위한 캐시. Initialize()에서 빌드. </summary>
        private Dictionary<BuildingType, BuildingEffectEntry> _cache;

        /// <summary>
        /// List를 Dictionary로 변환하여 O(1) 조회를 준비한다.
        /// EffectManager.Initialize()에서 호출.
        /// 타워 공격 프로필 캐시도 여기서 새로 만든다(Inspector에서 값을 바꾼 뒤 다시 초기화하면 새 값이 반영되도록).
        /// </summary>
        public void Initialize()
        {
            _cache = new Dictionary<BuildingType, BuildingEffectEntry>(_entries.Count);
            foreach (BuildingEffectEntry entry in _entries)
            {
                if (!_cache.ContainsKey(entry.buildingType))
                    _cache.Add(entry.buildingType, entry);
            }

            _towerProfileCache = null;
            EnsureTowerProfileCache();
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
