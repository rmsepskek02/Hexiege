// ============================================================================
// SkillEvasionSystem.cs
// 연막탄 「회피」 판정 서비스 — 경기 seed를 들고, 맞을 유닛의 연막 확률을 묻고, 결정적 해시로 회피를 판정한다(서버 권위).
// 2026-11-23 지원 스킬 연막탄 Task(_Tasks/2026-11-23/15_28_support-skill-smoke-bomb) 단계 S-1(Plan P-2 · P-9 · 메인 기본안 M-8).
//
// 무엇을 하나(유니티 초급자용 설명):
//   연막(아군 구역) 안에 선 우리 편 유닛을 적이 원거리로 쏘면, 서버가 "이번 공격을 피했나"를 정한다.
//   이 클래스가 그 판정 창구다.
//     · 확률 묻기 : GetEvadeChance(맞을 유닛 Id) — 구역 서비스(SkillZoneSystem)에 그대로 묻는다(연막 밖이면 0).
//     · 판정      : Roll — 경기 seed + 공격 열쇠로 roll을 만들고(SkillEvasionRoll), 확률과 비교해 회피 여부를 돌려준다.
//                   판정 1건마다 진단 줄 1줄(처음 96건까지 — 그 뒤는 개수만 센다).
//     · 싱글 열쇠 : NextAttackerOrdinalKey — 싱글 Hitscan처럼 C2 결과 키가 없는 경로의 대체 열쇠(공격자 Id + 순번 — O-10 해결안).
//     · 닫기      : Close — 경기 끝에 요약 1줄(경로별 판정 · 회피 수 · seed)을 남긴다.
//   판정을 부르는 쪽(전투 UseCase의 관문)은 "피했으면 피해 writer를 부르지 않는다".
//
// 경기 seed(열린 항목 O-11 — 프로그래머 확정):
//   조합 루트(GameBootstrapper.Setup)가 맵을 불러올 때마다 이 서비스를 새로 만들며, 그때 MapRootSeed.Create()로 새 seed를 뽑는다
//   (GUID 앞 8바이트 XOR 현재 시각 — 무작위 맵 root seed와 같은 뽑기 함수, 값은 별개). 재경기 = 새 맵 로드 = 새 seed.
//   순수 Client도 이 서비스를 만들지만 판정을 하지 않는다(발사 · 피해 판정은 서버만 — 스킬 규칙 25). 그래서 Client의 seed는 쓰이지 않는다.
//   seed는 판정 줄마다(Seed=) · 경기 끝 요약(Seed=)에 남는다 → 로그만으로 roll을 다시 계산할 수 있다(Plan E-L13).
//
// 회귀 안전(핵심): 연막 구성원이 하나도 없으면 GetEvadeChance가 조회 없이 0 → 부르는 쪽이 판정 자체를 하지 않는다
//   → 연막탄을 쓰기 전에는 전투가 이 클래스가 없던 때와 완전히 같다.
//
// Application 레이어 — Domain · Application 의존. Netcode · Core · Infrastructure 참조 없음(역할은 NetworkContext 정적 홀더로만 읽는다).
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 연막탄 회피 판정 서비스(서버 권위). 맵을 불러올 때마다 새로 만들어지고 경기 seed를 하나 갖는다.
    /// </summary>
    public sealed partial class SkillEvasionSystem
    {
        // 연막 구역(회피 확률)을 들고 있는 서비스. null이면 확률이 항상 0(판정 없음 — 안전).
        private readonly SkillZoneSystem _zones;
        // 이번 경기 seed(서버가 맵 로드마다 새로 뽑는다).
        private readonly ulong _matchSeed;
        // 싱글 대체 열쇠용 — 공격자 Id → 지금까지의 회피 판정 순번(마지막으로 쓴 값).
        private readonly Dictionary<int, ulong> _attackerOrdinals = new Dictionary<int, ulong>();
        // 경기 끝 요약을 이미 남겼는가(Close 멱등).
        private bool _closed;

        /// <summary>
        /// 회피 판정 서비스 생성. 조합 루트(GameBootstrapper.Setup)가 맵을 불러올 때마다 새로 만든다.
        /// </summary>
        /// <param name="zones">같은 경기의 구역 서비스(연막 확률 조회). null이면 확률이 항상 0.</param>
        /// <param name="matchSeed">이번 경기 seed(MapRootSeed.Create() — 검증 fixture는 고정값을 넣는다).</param>
        public SkillEvasionSystem(SkillZoneSystem zones, ulong matchSeed)
        {
            _zones = zones;
            _matchSeed = matchSeed;
        }

        /// <summary> 이번 경기 seed(진단 · 검증용). </summary>
        public ulong MatchSeed => _matchSeed;

        /// <summary> 경기 끝 요약을 남기고 닫혔는가(검증용). </summary>
        public bool IsClosed => _closed;

        /// <summary>
        /// 맞을 유닛의 회피 확률(0~1). 연막 밖 · 구역 서비스 없음이면 0(→ 부르는 쪽은 판정하지 않는다).
        /// </summary>
        /// <param name="unitId">맞을 유닛 Id.</param>
        public float GetEvadeChance(int unitId) => _zones != null ? _zones.GetEvadeChance(unitId) : 0f;

        /// <summary>
        /// 싱글 대체 열쇠를 하나 만든다(O-10 해결안) — 이 공격자의 회피 판정 순번을 1 늘리고 (공격자 Id, 순번)을 섞는다.
        /// 연막 안 대상에게 쏜 판정에서만 부르므로, 다른 공격자 · 다른 시스템이 무엇을 하든 이 공격자의 열쇠 순서는 바뀌지 않는다.
        /// </summary>
        /// <param name="attackerId">공격자 유닛 Id.</param>
        /// <param name="ordinal">이번 판정의 순번(1부터 — 로그에 남긴다).</param>
        public ulong NextAttackerOrdinalKey(int attackerId, out ulong ordinal)
        {
            _attackerOrdinals.TryGetValue(attackerId, out ulong last);
            ordinal = last + 1UL;
            _attackerOrdinals[attackerId] = ordinal;
            return SkillEvasionRoll.KeyFromAttackerOrdinal(attackerId, ordinal);
        }

        /// <summary>
        /// 회피를 판정한다(서버 권위 — 순수 Client에서는 부르지 않는다). 같은 seed · 같은 열쇠 · 같은 확률이면 결과가 언제나 같다.
        /// 판정 진단 줄은 부르는 쪽이 HP 결과를 안 뒤 <see cref="DiagOnRoll"/>로 남긴다(명중이면 HP가 줄어든 뒤라야 HpAfter가 맞다).
        /// </summary>
        /// <param name="attackKey">공격 열쇠(SkillEvasionRoll.Key… 함수 결과).</param>
        /// <param name="chanceBasisPoints">확률(만분율 — SkillEvasionRoll.ToChanceBasisPoints 결과).</param>
        /// <param name="roll">만든 64비트 roll(진단용).</param>
        /// <returns>회피했으면 true.</returns>
        public bool Roll(ulong attackKey, int chanceBasisPoints, out ulong roll)
        {
            roll = SkillEvasionRoll.ComputeRoll(_matchSeed, attackKey);
            return SkillEvasionRoll.IsEvaded(roll, chanceBasisPoints);
        }

        /// <summary>
        /// 경기 종료 — 요약 1줄을 남기고 닫는다(멱등). 조합 루트가 GameEvents.OnGameEnd에서 부른다.
        /// 닫힌 뒤에도 판정은 막지 않는다(경기 끝 직후 늦게 도착한 타격이 예외 없이 지나가게) — 요약만 한 번이다.
        /// </summary>
        public void Close()
        {
            if (_closed) return;
            _closed = true;
            // [진단 E-L11 · E-L12 · E-L13] 경기 끝 요약 1줄(경로별 판정 · 회피 수 · seed · 생략된 줄 수).
            DiagOnClose();
        }
    }
}
