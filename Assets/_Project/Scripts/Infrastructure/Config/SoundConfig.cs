// ============================================================================
// SoundConfig.cs
// 사운드 시스템의 데이터 묶음 ScriptableObject.
//
// 무엇을 담는가:
//   1. BGM 클립 4종 (로그인 / 로비 / 전투 / 게임종료) + 크로스페이드 시간
//   2. 유닛 타입별 SFX (공격음 / 사망음) + 클립별 볼륨
//   3. 건물 타입별 SFX (배치음 / 파괴음 / 업그레이드음) + 클립별 볼륨
//
// 무엇을 담지 않는가:
//   UI SFX(UiEffectKey)는 Presentation 레이어 타입이므로 여기서 다루지 않는다.
//   (GameSystemRules_Sound 규칙 3, 4 — UI SFX는 AudioManager가 직접 보유)
//
// 설계 패턴 (UnitEffectConfig / BuildingStatsConfig와 동일):
//   Inspector에서는 편집이 쉬운 List<...Entry> 형태로 데이터를 보관하고,
//   런타임에는 Initialize()에서 List를 Dictionary로 변환하여 O(1) 조회를 한다.
//   List는 사람이 편집하기 좋고, Dictionary는 빠른 조회에 좋기 때문에 둘을 함께 쓴다.
//
// 생성 방법:
//   Project 창 우클릭 → Create → Hexiege → Config → SoundConfig
//   → Assets/_Project/Resources/Config/SoundConfig.asset 로 저장 권장.
//
// Infrastructure/Config 레이어 — Unity 의존 허용 (ScriptableObject).
//   Domain 레이어 타입(UnitType, BuildingType)만 키로 사용한다.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Infrastructure
{
    // ========================================================================
    // UnitSoundEntry
    // 한 유닛 타입에 대한 공격/사망 SFX 묶음.
    // Inspector의 List에서 한 줄(하나의 유닛 타입)에 모든 사운드가 나란히 노출됨.
    // ========================================================================

    /// <summary>
    /// 유닛 타입 하나에 대한 공격/사망 효과음 묶음.
    /// </summary>
    [System.Serializable]
    public struct UnitSoundEntry
    {
        [Tooltip("대상 유닛 타입")]
        public UnitType unitType;

        [Header("공격 SFX")]
        [Tooltip("공격 시 재생할 효과음. 없으면 비워둠")]
        public AudioClip attackClip;

        [Tooltip("공격음 기준 볼륨 (0~1). SFX 채널 볼륨과 곱해져 최종 볼륨이 됨")]
        [Range(0f, 1f)]
        public float attackVolume;

        [Header("사망 SFX")]
        [Tooltip("사망 시 재생할 효과음. 없으면 비워둠")]
        public AudioClip deathClip;

        [Tooltip("사망음 기준 볼륨 (0~1)")]
        [Range(0f, 1f)]
        public float deathVolume;
    }

    // ========================================================================
    // BuildingSoundEntry
    // 한 건물 타입에 대한 배치/파괴/업그레이드 SFX 묶음.
    // ========================================================================

    /// <summary>
    /// 건물 타입 하나에 대한 배치/파괴/업그레이드 효과음 묶음.
    /// </summary>
    [System.Serializable]
    public struct BuildingSoundEntry
    {
        [Tooltip("대상 건물 타입")]
        public BuildingType buildingType;

        [Header("배치 SFX")]
        [Tooltip("건물 배치 시 재생할 효과음. 없으면 비워둠")]
        public AudioClip placeClip;

        [Tooltip("배치음 기준 볼륨 (0~1)")]
        [Range(0f, 1f)]
        public float placeVolume;

        [Header("파괴 SFX")]
        [Tooltip("건물 파괴 시 재생할 효과음. 없으면 비워둠")]
        public AudioClip destroyClip;

        [Tooltip("파괴음 기준 볼륨 (0~1)")]
        [Range(0f, 1f)]
        public float destroyVolume;

        [Header("업그레이드 SFX")]
        [Tooltip("건물 업그레이드 시 재생할 효과음. 없으면 비워둠")]
        public AudioClip upgradeClip;

        [Tooltip("업그레이드음 기준 볼륨 (0~1)")]
        [Range(0f, 1f)]
        public float upgradeVolume;
    }

    // ========================================================================
    // SoundConfig
    // BGM 클립 + 유닛/건물 SFX 묶음을 모으는 ScriptableObject.
    // ========================================================================

    /// <summary>
    /// 사운드 시스템 전체 데이터 묶음.
    /// BGM 클립과 유닛/건물 SFX를 보관하며, AudioManager가 이 에셋을 참조해 재생한다.
    /// </summary>
    [CreateAssetMenu(fileName = "SoundConfig", menuName = "Hexiege/Config/SoundConfig")]
    public class SoundConfig : ScriptableObject
    {
        // ====================================================================
        // BGM
        // ====================================================================

        [Header("BGM 클립")]
        [Tooltip("로그인 씬 BGM. 비워두면 무음")]
        [SerializeField] private AudioClip _loginBgm;

        [Tooltip("로비 씬 BGM. 비워두면 무음")]
        [SerializeField] private AudioClip _lobbyBgm;

        [Tooltip("전투(게임 진행 중) BGM. 비워두면 무음")]
        [SerializeField] private AudioClip _battleBgm;

        [Tooltip("게임 종료 BGM (승패 구분 없음). 비워두면 무음")]
        [SerializeField] private AudioClip _gameEndBgm;

        [Tooltip("BGM 전환 시 크로스페이드 시간(초). 기본 1.0")]
        [SerializeField] private float _bgmCrossfadeDuration = 1.0f;

        /// <summary> 로그인 씬 BGM 클립. 없으면 null. </summary>
        public AudioClip LoginBgm => _loginBgm;

        /// <summary> 로비 씬 BGM 클립. 없으면 null. </summary>
        public AudioClip LobbyBgm => _lobbyBgm;

        /// <summary> 전투 BGM 클립. 없으면 null. </summary>
        public AudioClip BattleBgm => _battleBgm;

        /// <summary> 게임 종료 BGM 클립. 없으면 null. </summary>
        public AudioClip GameEndBgm => _gameEndBgm;

        /// <summary> BGM 크로스페이드 시간(초). </summary>
        public float BgmCrossfadeDuration => _bgmCrossfadeDuration;

        // ====================================================================
        // SFX — 유닛 / 건물
        // ====================================================================

        [Header("유닛 SFX")]
        [Tooltip("유닛 타입별 공격/사망 효과음. Inspector에서 편집")]
        [SerializeField] private List<UnitSoundEntry> _unitSoundEntries = new List<UnitSoundEntry>();

        [Header("건물 SFX")]
        [Tooltip("건물 타입별 배치/파괴/업그레이드 효과음. Inspector에서 편집")]
        [SerializeField] private List<BuildingSoundEntry> _buildingSoundEntries = new List<BuildingSoundEntry>();

        // ====================================================================
        // 런타임 조회 캐시 (Initialize()에서 빌드)
        // ====================================================================

        /// <summary> 유닛 타입 → 사운드 엔트리. Initialize()에서 List를 변환해 채움. </summary>
        private Dictionary<UnitType, UnitSoundEntry> _unitCache;

        /// <summary> 건물 타입 → 사운드 엔트리. Initialize()에서 List를 변환해 채움. </summary>
        private Dictionary<BuildingType, BuildingSoundEntry> _buildingCache;

        // ====================================================================
        // 초기화
        // ====================================================================

        /// <summary>
        /// List를 Dictionary로 변환하여 O(1) 조회를 준비한다.
        /// AudioManager.Initialize()에서 호출.
        /// 같은 타입이 List에 중복으로 들어 있어도 첫 항목만 채택한다.
        /// </summary>
        public void Initialize()
        {
            _unitCache = new Dictionary<UnitType, UnitSoundEntry>(_unitSoundEntries.Count);
            foreach (UnitSoundEntry entry in _unitSoundEntries)
            {
                if (!_unitCache.ContainsKey(entry.unitType))
                    _unitCache.Add(entry.unitType, entry);
            }

            _buildingCache = new Dictionary<BuildingType, BuildingSoundEntry>(_buildingSoundEntries.Count);
            foreach (BuildingSoundEntry entry in _buildingSoundEntries)
            {
                if (!_buildingCache.ContainsKey(entry.buildingType))
                    _buildingCache.Add(entry.buildingType, entry);
            }
        }

        // ====================================================================
        // 유닛 SFX 조회
        // ====================================================================

        /// <summary>
        /// 지정 유닛 타입의 공격 효과음 클립을 반환. 없으면 null.
        /// </summary>
        /// <param name="type">조회할 유닛 타입.</param>
        /// <returns>공격 효과음 클립 또는 null.</returns>
        public AudioClip GetUnitAttackClip(UnitType type)
        {
            if (_unitCache != null && _unitCache.TryGetValue(type, out UnitSoundEntry entry))
                return entry.attackClip;
            return null;
        }

        /// <summary>
        /// 지정 유닛 타입의 공격음 볼륨을 반환. 항목이 없으면 1.0.
        /// </summary>
        /// <param name="type">조회할 유닛 타입.</param>
        /// <returns>공격음 볼륨 (0~1).</returns>
        public float GetUnitAttackVolume(UnitType type)
        {
            if (_unitCache != null && _unitCache.TryGetValue(type, out UnitSoundEntry entry))
                return entry.attackVolume;
            return 1f;
        }

        /// <summary>
        /// 지정 유닛 타입의 사망 효과음 클립을 반환. 없으면 null.
        /// </summary>
        /// <param name="type">조회할 유닛 타입.</param>
        /// <returns>사망 효과음 클립 또는 null.</returns>
        public AudioClip GetUnitDeathClip(UnitType type)
        {
            if (_unitCache != null && _unitCache.TryGetValue(type, out UnitSoundEntry entry))
                return entry.deathClip;
            return null;
        }

        /// <summary>
        /// 지정 유닛 타입의 사망음 볼륨을 반환. 항목이 없으면 1.0.
        /// </summary>
        /// <param name="type">조회할 유닛 타입.</param>
        /// <returns>사망음 볼륨 (0~1).</returns>
        public float GetUnitDeathVolume(UnitType type)
        {
            if (_unitCache != null && _unitCache.TryGetValue(type, out UnitSoundEntry entry))
                return entry.deathVolume;
            return 1f;
        }

        // ====================================================================
        // 건물 SFX 조회
        // ====================================================================

        /// <summary>
        /// 지정 건물 타입의 배치 효과음 클립을 반환. 없으면 null.
        /// </summary>
        /// <param name="type">조회할 건물 타입.</param>
        /// <returns>배치 효과음 클립 또는 null.</returns>
        public AudioClip GetBuildingPlaceClip(BuildingType type)
        {
            if (_buildingCache != null && _buildingCache.TryGetValue(type, out BuildingSoundEntry entry))
                return entry.placeClip;
            return null;
        }

        /// <summary>
        /// 지정 건물 타입의 배치음 볼륨을 반환. 항목이 없으면 1.0.
        /// </summary>
        /// <param name="type">조회할 건물 타입.</param>
        /// <returns>배치음 볼륨 (0~1).</returns>
        public float GetBuildingPlaceVolume(BuildingType type)
        {
            if (_buildingCache != null && _buildingCache.TryGetValue(type, out BuildingSoundEntry entry))
                return entry.placeVolume;
            return 1f;
        }

        /// <summary>
        /// 지정 건물 타입의 파괴 효과음 클립을 반환. 없으면 null.
        /// </summary>
        /// <param name="type">조회할 건물 타입.</param>
        /// <returns>파괴 효과음 클립 또는 null.</returns>
        public AudioClip GetBuildingDestroyClip(BuildingType type)
        {
            if (_buildingCache != null && _buildingCache.TryGetValue(type, out BuildingSoundEntry entry))
                return entry.destroyClip;
            return null;
        }

        /// <summary>
        /// 지정 건물 타입의 파괴음 볼륨을 반환. 항목이 없으면 1.0.
        /// </summary>
        /// <param name="type">조회할 건물 타입.</param>
        /// <returns>파괴음 볼륨 (0~1).</returns>
        public float GetBuildingDestroyVolume(BuildingType type)
        {
            if (_buildingCache != null && _buildingCache.TryGetValue(type, out BuildingSoundEntry entry))
                return entry.destroyVolume;
            return 1f;
        }

        /// <summary>
        /// 지정 건물 타입의 업그레이드 효과음 클립을 반환. 없으면 null.
        /// </summary>
        /// <param name="type">조회할 건물 타입.</param>
        /// <returns>업그레이드 효과음 클립 또는 null.</returns>
        public AudioClip GetBuildingUpgradeClip(BuildingType type)
        {
            if (_buildingCache != null && _buildingCache.TryGetValue(type, out BuildingSoundEntry entry))
                return entry.upgradeClip;
            return null;
        }

        /// <summary>
        /// 지정 건물 타입의 업그레이드음 볼륨을 반환. 항목이 없으면 1.0.
        /// </summary>
        /// <param name="type">조회할 건물 타입.</param>
        /// <returns>업그레이드음 볼륨 (0~1).</returns>
        public float GetBuildingUpgradeVolume(BuildingType type)
        {
            if (_buildingCache != null && _buildingCache.TryGetValue(type, out BuildingSoundEntry entry))
                return entry.upgradeVolume;
            return 1f;
        }
    }
}
