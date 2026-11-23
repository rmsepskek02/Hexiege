// ============================================================================
// SkillEvasionRoll.cs
// 연막탄 「회피」 판정의 공용 순수 함수 모음 — 결정적 난수(해시) · 확률 비교 · 공격 열쇠 만들기 · 빗나간 지점.
// 2026-11-23 지원 스킬 연막탄 Task(_Tasks/2026-11-23/15_28_support-skill-smoke-bomb) 단계 S-1(Plan P-9 · P-10).
//
// 무엇을 하나(유니티 초급자용 설명):
//   연막 안의 아군 유닛은 적의 원거리 단일 공격을 50% 확률로 피한다. "50% 확률"을 정하려면 주사위가 필요하다.
//   그런데 유니티의 Random.Range 같은 "전역 주사위"를 쓰면 두 가지 문제가 생긴다.
//     ① 다른 시스템(맵 생성 · AI 등)이 주사위를 한 번 더 굴리면 그다음 결과가 전부 밀린다(순서에 따라 결과가 바뀐다).
//     ② 나중에 로그만 보고 "왜 이 공격이 빗나갔나"를 다시 계산할 수 없다.
//   그래서 주사위 대신 "해시"를 쓴다. 해시는 같은 입력에 언제나 같은 출력을 내는 계산이다.
//     · 입력 = 경기 seed(경기마다 서버가 한 번 뽑는 64비트 수) + 공격 열쇠(그 공격 한 번을 가리키는 번호)
//     · 출력 = 64비트 수(roll). 이것을 10000으로 나눈 나머지(0~9999)가 확률(만분율)보다 작으면 「회피」.
//   이렇게 하면 다른 시스템이 무엇을 하든 같은 공격의 결과는 바뀌지 않고, 로그에 남긴 seed와 열쇠로 그대로 다시 계산된다.
//
// 해시 함수는 무작위 맵이 쓰는 SplitMix64(Domain/Map/MapRandom.Mix64 · Combine)를 그대로 쓴다.
//   (.NET 기본 난수 · 유니티 정적 난수 · 문자열 해시 · 부동소수점을 쓰지 않는 이유는 MapRandom.cs 머리말에 있다.)
//
// 공격 열쇠(어떤 공격 한 번인가 — Plan P-9 · 열린 항목 O-10):
//   경로마다 "그 공격 한 번"을 가리키는 번호가 다르므로, 경로 꼬리표(KeyTag)를 섞어 서로 겹치지 않게 만든다.
//     · 멀티 Hitscan : C2 정규 결과 키(AttackResultKey — 공격자 인스턴스 · 공격 회차 · 타격 번호 · 피해자 · 효과 · 순번)
//     · 싱글 Hitscan : 결과 키가 없다(TickPendingHits가 presentationResultKey: default로 부른다 — Research 10절).
//                     → 대체 열쇠 = (공격자 Id, 그 공격자의 회피 판정 순번 1, 2, 3 …) — O-10 해결안.
//                     순번은 "연막 안 대상에게 쏜 그 공격자의 판정"에만 늘어나므로, 다른 공격자 · 다른 시스템과 무관하다.
//     · 투사체(S-3에서 사용 예정)   : 발사 번호표(ProjectileDeliveryKey — epoch · id)
//     · 즉발 탑(S-4에서 결정)       : 이 파일에 아직 없다 — S-4에서 표현 스냅샷 순번으로 만든다.
//
// 빗나간 지점(Plan P-10 · 사용자 결정 U-4 — 투사체 · 탑 화면이 쓴다, S-1에서는 판정 호출 없음):
//   M = C + n × (r + 0.3) + d × 0.5
//     C = 발사 순간 대상 중심, r = 대상 반경, d = 쏜 쪽 → 대상 방향(수평 단위벡터), n = d에 수직인 단위벡터.
//     좌 / 우는 같은 roll의 "가장 위 비트"로 정한다(확률 비교는 아래 자리 나머지를 쓰므로 서로 거의 독립이다).
//   0.3 · 0.5는 시험용 임시값이다(Plan O-9 — 화면을 보고 조정).
//
// Application 레이어 — Domain(MapRandom · UnitType) 의존. UnityEngine은 Vector3 계산에만 쓴다(SkillZoneSystem 선례).
// ============================================================================

using UnityEngine;
using Hexiege.Application.Combat.Sequencing;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 회피 판정이 어느 공격 경로에서 일어났는가(진단 · 요약 집계용). 정수 값은 로그에만 쓰인다.
    /// </summary>
    public enum SkillEvasionPath : byte
    {
        /// <summary> 없음(기본값). </summary>
        None = 0,
        /// <summary> 즉발 총탄(Pistoleer · Assault · Sniper) — 단계 S-2. </summary>
        Hitscan = 1,
        /// <summary> 서버 권위 투사체(유닛) — 단계 S-3에서 사용. </summary>
        Projectile = 2,
        /// <summary> 서버 권위 투사체(인간 대포탑 포탄) — 단계 S-3에서 사용. </summary>
        TowerShell = 3,
        /// <summary> 즉발 탑(RuneSpire 번개 · VineTower) — 단계 S-4에서 사용. </summary>
        TowerInstant = 4
    }

    /// <summary>
    /// 공격 열쇠를 무엇으로 만들었는가(진단용 — 로그의 KeySrc=).
    /// </summary>
    public enum SkillEvasionKeySource : byte
    {
        /// <summary> 없음(기본값). </summary>
        None = 0,
        /// <summary> 멀티 C2 정규 결과 키(AttackResultKey). </summary>
        ResultKey = 1,
        /// <summary> 싱글 대체 열쇠 = (공격자 Id, 그 공격자의 회피 판정 순번) — O-10 해결안. </summary>
        AttackerOrdinal = 2,
        /// <summary> 투사체 발사 번호표(epoch · id) — 단계 S-3에서 사용. </summary>
        ProjectileKey = 3
    }

    /// <summary>
    /// 연막 회피 판정의 공용 순수 함수(결정적 해시 · 확률 비교 · 공격 열쇠 · 빗나간 지점). 상태가 없어 어디서 불러도 같다.
    /// </summary>
    public static class SkillEvasionRoll
    {
        /// <summary> 확률의 정수 눈금 = 만분율(10000 = 100%). 0.5 → 5000. </summary>
        public const int ChanceScale = 10000;

        /// <summary> 빗나간 지점의 옆 거리 = 대상 반경 + 이 값(시험용 임시값 — Plan U-4 · O-9). </summary>
        public const float MissSideExtra = 0.3f;

        /// <summary> 빗나간 지점의 진행 방향(대상 뒤쪽) 거리(시험용 임시값 — Plan U-4 · O-9). </summary>
        public const float MissForwardExtra = 0.5f;

        // 경로 꼬리표 — 경로가 다르면 같은 숫자 열쇠라도 다른 roll이 나오게 섞는다(서로 겹치지 않게).
        private const ulong KeyTagResultKey = 0x5245534B45590001UL;       // "RESKEY" + 1
        private const ulong KeyTagAttackerOrdinal = 0x4154544F52440002UL; // "ATTORD" + 2
        private const ulong KeyTagProjectile = 0x50524F4A45430003UL;      // "PROJEC" + 3

        /// <summary>
        /// 이 공격자가 「즉발 총탄(Hitscan)」 회피 대상인가 — Pistoleer · Assault · Sniper(메인 기본안 M-1).
        /// 서버 권위 투사체 유닛은 여기가 아니라 발사 순간 판정(S-3)이고, 근접 · 파도 · 힐러는 「원거리 단일」이 아니라 대상이 아니다.
        /// </summary>
        /// <param name="type">공격자 유닛 종류.</param>
        /// <remarks>
        /// 같은 분류가 Infrastructure의 UnitAttackShadowProfileResolver(AttackDeliveryKind.Hitscan · Supported)에도 있다.
        /// Application은 Infrastructure를 참조할 수 없어(레이어 규칙) 여기에 같은 세 종류를 적었다 — 한쪽을 바꾸면 다른 쪽도 확인한다.
        /// (BloomFairy도 resolver에서 Hitscan으로 적혀 있지만 힐 전용 경로라 회피 대상이 아니다.)
        /// </remarks>
        public static bool IsHitscanEvadableAttacker(UnitType type)
            => type == UnitType.Pistoleer || type == UnitType.Assault || type == UnitType.Sniper;

        /// <summary>
        /// 확률(0~1 실수)을 만분율 정수로 바꾼다. 0 이하 · NaN → 0(판정 안 함), 1 이상 → 10000(항상 회피).
        /// 정수로 바꿔 비교하는 이유: 부동소수점 비교는 기기마다 끝자리가 다를 수 있어 결정성이 흔들린다.
        /// </summary>
        /// <param name="chance">회피 확률(0~1).</param>
        public static int ToChanceBasisPoints(float chance)
        {
            if (!(chance > 0f)) return 0;           // NaN도 여기서 걸린다(NaN > 0은 거짓)
            if (chance >= 1f) return ChanceScale;
            // 0.5 → 5000. 반올림은 「0.5는 위로」(AwayFromZero) — 0.00005처럼 작은 값도 0이 아니게 한 번 올림.
            int bp = (int)System.Math.Round(chance * ChanceScale, System.MidpointRounding.AwayFromZero);
            return bp < 0 ? 0 : (bp > ChanceScale ? ChanceScale : bp);
        }

        /// <summary>
        /// 경기 seed와 공격 열쇠로 64비트 roll을 만든다(SplitMix64 — 같은 입력이면 언제나 같은 값).
        /// </summary>
        /// <param name="matchSeed">경기 seed(서버가 경기마다 한 번 뽑는다).</param>
        /// <param name="attackKey">공격 열쇠(아래 Key… 함수로 만든다).</param>
        public static ulong ComputeRoll(ulong matchSeed, ulong attackKey)
            => MapRandom.Mix64(MapRandom.Combine(matchSeed, attackKey));

        /// <summary> roll → 0~9999 눈금(확률 비교에 쓰는 값 — 로그의 Roll=). </summary>
        /// <param name="roll">ComputeRoll 결과.</param>
        public static int RollBucket(ulong roll) => (int)(roll % ChanceScale);

        /// <summary>
        /// 회피했는가 = 눈금 &lt; 확률(만분율). 확률 0이면 항상 false(명중), 10000이면 항상 true(회피).
        /// 경계: 눈금 == 확률 − 1 → 회피, 눈금 == 확률 → 명중(Plan E-T10).
        /// </summary>
        /// <param name="roll">ComputeRoll 결과.</param>
        /// <param name="chanceBasisPoints">확률(만분율, ToChanceBasisPoints 결과).</param>
        public static bool IsEvaded(ulong roll, int chanceBasisPoints)
            => chanceBasisPoints > 0 && RollBucket(roll) < chanceBasisPoints;

        /// <summary> 빗나간 지점이 대상의 왼쪽인가(roll의 가장 위 비트 — 확률 비교와 다른 비트). </summary>
        /// <param name="roll">ComputeRoll 결과.</param>
        public static bool MissSideIsLeft(ulong roll) => (roll >> 63) != 0UL;

        // ── 공격 열쇠 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 멀티 Hitscan 열쇠 — C2 정규 결과 키의 일곱 칸을 모두 섞는다(같은 공격 한 번 = 같은 열쇠).
        /// </summary>
        /// <param name="key">서버 권위 Impact 승인이 만든 결과 키(유효해야 의미가 있다).</param>
        public static ulong KeyFromResultKey(AttackResultKey key)
        {
            ulong h = MapRandom.Combine(KeyTagResultKey, key.AttackerInstanceId.Value);
            h = MapRandom.Combine(h, key.SequenceId.Value);
            h = MapRandom.Combine(h, (ulong)(uint)key.HitIndex);
            h = MapRandom.Combine(h, ((ulong)(uint)key.VictimKind << 32) | (uint)key.VictimId);
            h = MapRandom.Combine(h, ((ulong)(uint)key.EffectKind << 32) | (uint)key.ResultOrdinal);
            return h;
        }

        /// <summary>
        /// 싱글 대체 열쇠(O-10) — 공격자 Id와 "그 공격자의 회피 판정 순번"(1부터)을 섞는다.
        /// </summary>
        /// <param name="attackerId">공격자 유닛 Id.</param>
        /// <param name="ordinal">그 공격자의 회피 판정 순번(1, 2, 3 …).</param>
        public static ulong KeyFromAttackerOrdinal(int attackerId, ulong ordinal)
        {
            ulong h = MapRandom.Combine(KeyTagAttackerOrdinal, (ulong)(uint)attackerId);
            return MapRandom.Combine(h, ordinal);
        }

        /// <summary>
        /// 투사체 열쇠 — 발사 번호표(epoch · id). 발사마다 유일하다(단계 S-3의 발사 순간 판정이 쓴다).
        /// </summary>
        /// <param name="epoch">서버 UseCase 수명 번호.</param>
        /// <param name="id">그 수명 안의 발사 번호.</param>
        public static ulong KeyFromProjectile(ulong epoch, ulong id)
        {
            ulong h = MapRandom.Combine(KeyTagProjectile, epoch);
            return MapRandom.Combine(h, id);
        }

        // ── 빗나간 지점 ───────────────────────────────────────────────────────

        /// <summary>
        /// 빗나간 지점(바닥 좌표)을 계산한다 — M = C + n × (r + 0.3) + d × 0.5(Plan P-10 · U-4).
        /// 서버 판정과 양쪽 화면이 같은 이 함수 하나를 쓴다(공습 「한 함수」 교훈 — 따로 계산하면 어긋난다).
        /// 쏜 위치와 대상 중심이 거의 같은 자리면(방향을 못 정하면) +Z 방향을 쓴다.
        /// </summary>
        /// <param name="launch">발사 위치(도메인 월드).</param>
        /// <param name="targetCenter">발사 순간 대상 중심(도메인 월드). 결과 Y는 이 값의 Y.</param>
        /// <param name="targetRadius">대상 반경(0 이하 · NaN이면 0으로 본다).</param>
        /// <param name="roll">같은 공격의 roll(좌우를 정한다).</param>
        public static Vector3 ComputeMissPoint(Vector3 launch, Vector3 targetCenter, float targetRadius, ulong roll)
        {
            float r = targetRadius > 0f ? targetRadius : 0f; // NaN → 0
            float dx = targetCenter.x - launch.x;
            float dz = targetCenter.z - launch.z;
            float length = Mathf.Sqrt(dx * dx + dz * dz);
            if (!(length > 0.0001f)) { dx = 0f; dz = 1f; length = 1f; }
            dx /= length; dz /= length;
            // 진행 방향 d = (dx, dz)의 왼쪽 수직 = (-dz, dx). 오른쪽은 그 반대.
            float side = MissSideIsLeft(roll) ? 1f : -1f;
            float nx = -dz * side, nz = dx * side;
            float sideDistance = r + MissSideExtra;
            return new Vector3(targetCenter.x + nx * sideDistance + dx * MissForwardExtra,
                               targetCenter.y,
                               targetCenter.z + nz * sideDistance + dz * MissForwardExtra);
        }
    }
}
