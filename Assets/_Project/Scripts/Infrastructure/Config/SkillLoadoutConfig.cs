// ============================================================================
// SkillLoadoutConfig.cs
// 종족(RaceId)별 스킬 로드아웃(슬롯 1~5)을 저작하는 ScriptableObject.
// ISkillDataProvider 구현을 겸한다(Application 인터페이스를 Infrastructure가 구현 — 의존성 역전).
//
// 종족 키 분기(규칙 1·6):
//   Spirit(MagicSpirit)과 Transcendence(WillowShrine)가 같은 enum(MagicBuilding)을 공유하므로,
//   로드아웃은 enum이 아니라 RaceId로 분기한다. 각 종족은 스킬 건물이 정확히 1종이라
//   RaceId만으로 로드아웃이 유일하게 결정된다.
//
// 저작 방법(사용자 Inspector 작업):
//   _entries에 종족별 항목을 추가하고, 각 항목의 skills 배열에 SkillDefinition SO를 슬롯 순서대로
//   최대 5개까지 넣는다(규칙 4). 슬롯 0-based(=슬롯 번호)로 사용된다.
//
// 성능:
//   GetLoadout은 종족별 SkillData 목록을 최초 1회 만들어 캐시한다(매 호출마다 재생성 회피).
//
// Infrastructure 레이어 — ScriptableObject. Application(SkillData/ISkillDataProvider)/Domain 참조.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Application;

namespace Hexiege.Infrastructure
{
    /// <summary>
    /// 종족별 스킬 로드아웃 데이터(ScriptableObject). ISkillDataProvider를 구현해 UseCase에 주입된다.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillLoadoutConfig", menuName = "Hexiege/Skill Loadout Config")]
    public sealed class SkillLoadoutConfig : ScriptableObject, ISkillDataProvider
    {
        /// <summary>
        /// 한 종족의 로드아웃 항목(직렬화용). Unity가 Dictionary를 직렬화하지 못하므로 리스트로 저작한다.
        /// </summary>
        [System.Serializable]
        public sealed class RaceLoadoutEntry
        {
            [Tooltip("이 로드아웃을 사용하는 종족.")]
            public RaceId race;

            [Tooltip("슬롯 1~5 스킬(최대 5개, 규칙 4). 배열 순서 = 슬롯 순서(0-based).")]
            public SkillDefinition[] skills;
        }

        [Tooltip("종족별 스킬 로드아웃 목록. 각 종족당 항목 1개.")]
        [SerializeField] private List<RaceLoadoutEntry> _entries = new List<RaceLoadoutEntry>();

        // 종족별 SkillData 목록 캐시(최초 조회 시 1회 생성). 빈 목록도 캐시해 재빌드를 막는다.
        private readonly Dictionary<RaceId, IReadOnlyList<SkillData>> _cache
            = new Dictionary<RaceId, IReadOnlyList<SkillData>>();

        /// <summary>
        /// 지정한 종족의 스킬 로드아웃(슬롯 순서대로, 최대 5개)을 반환한다. 정의가 없으면 빈 목록.
        /// </summary>
        /// <param name="race">조회할 종족.</param>
        public IReadOnlyList<SkillData> GetLoadout(RaceId race)
        {
            // 캐시 히트.
            if (_cache.TryGetValue(race, out var cached)) return cached;

            var list = new List<SkillData>(5);

            // 해당 종족 항목을 찾아 SkillDefinition → SkillData로 변환.
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    RaceLoadoutEntry entry = _entries[i];
                    if (entry == null || entry.race != race || entry.skills == null) continue;

                    for (int s = 0; s < entry.skills.Length; s++)
                    {
                        SkillDefinition def = entry.skills[s];
                        if (def == null) continue;       // 빈 슬롯은 건너뛴다(다음 슬롯이 앞당겨짐).
                        if (list.Count >= 5) break;       // 최대 5개(규칙 4).
                        list.Add(def.ToSkillData());
                    }
                    break; // 종족당 항목 1개만 사용.
                }
            }

            IReadOnlyList<SkillData> result = list;
            _cache[race] = result;
            return result;
        }

        /// <summary>
        /// 캐시를 비운다(에디터에서 데이터 수정 후 재빌드가 필요할 때). 런타임 재경기 시에도 호출 가능.
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }
    }
}
