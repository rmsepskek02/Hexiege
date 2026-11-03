// ============================================================================
// SoundConfig.cs
// 사운드 시스템의 데이터 묶음 ScriptableObject.
//
// 무엇을 담는가:
//   1. BGM 클립 4종 (로그인 / 로비 / 전투 / 게임종료) + 크로스페이드 시간
//   2. 유닛 타입별 SFX (공격음 / 사망음) + 클립별 볼륨 + 공격음 「우선 재생」 표시(2026-11-02 추가)
//   3. 건물 타입별 SFX (배치음 / 파괴음 / 업그레이드음) + 클립별 볼륨
//   4. 방어 타워 공격 SFX — 「건물 종류 + 종족」별 공격음 + 클립별 볼륨 (2026-11-03 추가)
//      세 종족 방어 타워는 같은 건물 종류(AutoTower)라 건물 종류만으로는 소리를 가를 수 없어 종족을 함께 키로 쓴다.
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
//   Domain 레이어 타입(UnitType, BuildingType, RaceId)만 키로 사용한다.
//   (RaceId는 2026-11-03 타워 공격음 칸에서 추가 — Domain/Common/RaceId.cs 의 Domain 타입이다.
//    규칙 문서 GameSystemRules_Sound 규칙 3의 같은 문장 개정은 테스트 뒤 문서 반영 단계에서 한다 — Plan 9절 S-1.)
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

        // 2026-11-02 추가 (효과음 task 1-C, _Tasks/2026-11-02/13_39_unit-attack-sfx-tiering/Plan.md 6-2):
        //   "우선 재생" 표시 칸. 체크한 유닛의 공격음은 효과음이 동시에 너무 많이 울려
        //   전체 한도(AudioManager의 _maxConcurrentSfx)가 꽉 찼을 때도 들리도록,
        //   지금 울리고 있는 「일반」 효과음 중 가장 먼저 시작한 것을 짧게 줄여 끄고 그 자리에서 재생된다.
        //   어느 유닛이 「특수 공격」인지는 소리 설계 결정이라 코드에 유닛 목록을 박지 않고 여기(데이터)에 둔다.
        //   bool 하나라 Domain 타입 외 키를 쓰지 않는다(GameSystemRules_Sound 규칙 3 준수).
        //   옛 SoundConfig.asset에는 이 칸이 없으므로 처음 읽을 때 false(일반)로 들어온다 — 기존 동작과 같다.
        [Tooltip("체크하면 공격음이 「우선」 효과음이 된다. 효과음 전체 한도가 찼을 때 가장 오래된 「일반」 효과음을 끊고 재생한다. 특수 공격 유닛에만 체크")]
        public bool attackIsPriority;

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
    // TowerAttackSoundEntry (2026-11-03 추가 — 타워 공격 표현·소리 개편)
    // 「건물 종류 + 종족」 하나에 대한 방어 타워 공격음.
    //   계획 원본: Assets/_Project/Docs/_Tasks/2026-11-02/15_48_tower-attack-presentation-restructure/Plan.md 6-1절
    //   왜 종족이 키에 들어가나: 인간 대포 타워 · 정령 룬 첨탑 · 초월 덩굴 타워는 모두 BuildingType.AutoTower 하나다.
    //   그래서 건물 종류만으로는 "대포 소리"와 "번개 소리"를 가를 수 없다.
    //   ⚠️ 새 줄을 Inspector에서 추가하면 Unity가 볼륨을 0으로 넣는다 — 클립을 넣을 때 볼륨도 꼭 함께 정한다(볼륨 0 = 무음).
    // ========================================================================

    /// <summary>
    /// 방어 타워 「건물 종류 + 종족」 하나에 대한 공격 효과음.
    /// </summary>
    [System.Serializable]
    public struct TowerAttackSoundEntry
    {
        [Tooltip("대상 건물 타입(방어 타워 = AutoTower)")]
        public BuildingType buildingType;

        [Tooltip("대상 타워 종족(Human = 대포 타워, Spirit = 룬 첨탑, Transcendence = 덩굴 타워)")]
        public RaceId race;

        [Tooltip("타워가 공격하는 순간 재생할 효과음. 없으면 비워둠(무음)")]
        public AudioClip attackClip;

        [Tooltip("공격음 기준 볼륨 (0~1). SFX 채널 볼륨과 곱해져 최종 볼륨이 됨. 0이면 들리지 않는다")]
        [Range(0f, 1f)]
        public float attackVolume;
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

        // 2026-11-03 추가 — 타워 공격 SFX 칸. 지금은 일부러 비워 둔다(Plan 13절 A-6):
        //   클립 없이 줄만 미리 만들면 볼륨이 0으로 들어가, 나중에 클립만 넣고 볼륨을 잊으면 무음이 되기 때문이다.
        //   클립과 볼륨은 효과음 2단계에서 함께 넣는다(Human = 대포 공격음, RuneSpire = 전용, VineTower = 덩굴 VFX 확정 뒤).
        [Header("타워 공격 SFX")]
        [Tooltip("건물 종류 + 종족별 타워 공격음. 클립은 효과음 2단계에서 연결. 같은 「건물 종류 + 종족」 줄이 둘 이상이면 첫 줄만 쓴다")]
        [SerializeField] private List<TowerAttackSoundEntry> _towerAttackSoundEntries = new List<TowerAttackSoundEntry>();

        // ====================================================================
        // 런타임 조회 캐시 (Initialize()에서 빌드)
        // ====================================================================

        /// <summary> 유닛 타입 → 사운드 엔트리. Initialize()에서 List를 변환해 채움. </summary>
        private Dictionary<UnitType, UnitSoundEntry> _unitCache;

        /// <summary> 건물 타입 → 사운드 엔트리. Initialize()에서 List를 변환해 채움. </summary>
        private Dictionary<BuildingType, BuildingSoundEntry> _buildingCache;

        /// <summary> (건물 타입, 종족) → 타워 공격음 엔트리. Initialize()에서 List를 변환해 채움. </summary>
        private Dictionary<(BuildingType, RaceId), TowerAttackSoundEntry> _towerAttackCache;

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

            // 타워 공격음: 옛 에셋에는 이 목록이 없어 null일 수 있으므로 빈 사전으로 시작한다.
            int towerCount = _towerAttackSoundEntries != null ? _towerAttackSoundEntries.Count : 0;
            _towerAttackCache = new Dictionary<(BuildingType, RaceId), TowerAttackSoundEntry>(towerCount);
            if (_towerAttackSoundEntries != null)
            {
                foreach (TowerAttackSoundEntry entry in _towerAttackSoundEntries)
                {
                    var key = (entry.buildingType, entry.race);
                    if (!_towerAttackCache.ContainsKey(key))
                        _towerAttackCache.Add(key, entry);
                }
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
        /// 지정 유닛 타입의 공격음이 「우선」 효과음인지 반환한다. 항목이 없으면 false(일반).
        /// 우선 효과음은 효과음 전체 한도가 찼을 때 가장 오래된 일반 효과음을 끊고 재생된다(AudioManager).
        /// </summary>
        /// <param name="type">조회할 유닛 타입.</param>
        /// <returns>우선 효과음이면 true, 일반이거나 항목이 없으면 false.</returns>
        public bool IsUnitAttackPriority(UnitType type)
        {
            if (_unitCache != null && _unitCache.TryGetValue(type, out UnitSoundEntry entry))
                return entry.attackIsPriority;
            return false;
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

        // ====================================================================
        // 방어 타워 공격 SFX 조회 (2026-11-03 추가)
        // ====================================================================

        /// <summary>
        /// 지정 「건물 타입 + 종족」 타워의 공격 효과음 클립을 반환. 없으면 null(무음).
        /// </summary>
        /// <param name="type">공격한 건물 타입(방어 타워 = AutoTower).</param>
        /// <param name="race">공격한 타워의 종족.</param>
        /// <returns>타워 공격 효과음 클립 또는 null.</returns>
        public AudioClip GetTowerAttackClip(BuildingType type, RaceId race)
        {
            if (_towerAttackCache != null && _towerAttackCache.TryGetValue((type, race), out TowerAttackSoundEntry entry))
                return entry.attackClip;
            return null;
        }

        /// <summary>
        /// 지정 「건물 타입 + 종족」 타워의 공격음 볼륨을 반환. 항목이 없으면 1.0(다른 조회와 같은 규칙).
        /// </summary>
        /// <param name="type">공격한 건물 타입(방어 타워 = AutoTower).</param>
        /// <param name="race">공격한 타워의 종족.</param>
        /// <returns>타워 공격음 볼륨 (0~1).</returns>
        public float GetTowerAttackVolume(BuildingType type, RaceId race)
        {
            if (_towerAttackCache != null && _towerAttackCache.TryGetValue((type, race), out TowerAttackSoundEntry entry))
                return entry.attackVolume;
            return 1f;
        }
    }
}
