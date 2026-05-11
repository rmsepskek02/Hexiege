// ============================================================================
// UnitStats.cs
// 유닛 타입별 전투 스탯을 조회하는 정적 클래스.
//
// 과거에는 switch 표현식으로 수치가 하드코딩돼 있었으나, 현재는
// Infrastructure의 UnitStatsConfig(ScriptableObject)에서 값을 주입받아
// 내부 Dictionary에 저장해 두는 방식으로 변경됐다.
//
// 초기화 흐름:
//   1) GameBootstrapper가 Inspector에 연결된 UnitStatsConfig를 읽는다.
//   2) Config 항목을 Domain-친화적인 StatValues 형태로 변환해
//      Initialize(data)에 전달한다.
//   3) 이후 UnitData / UnitSpawnUseCase 등 어디서든 기존 API
//      (GetMaxHp, GetAttackPower 등)를 호출하면 Dictionary에서 값이 반환된다.
//
// 메서드 시그니처는 과거와 100% 동일 — 호출부 수정 불필요.
//
// 종족별 독립 유닛 구조:
//   - 인간계(Human): Pistoleer, Assault, Sniper
//   - 정령계(Spirit): FlameSpirit, EmberSpirit, InfernoSpirit
//   - 초월계(Transcendence): BearGuard, FoxMagician, LionKnight
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

using System.Collections.Generic;

namespace Hexiege.Domain
{
    // ========================================================================
    // AttackKind
    // [2026-05-11 비활성화 — 이동 로직 분기 미사용]
    // 새 이동/전투 규칙(GameSystemRules.md)에서는 근접/원거리 모두 동일한 상태 머신
    // (A* 이동 → 전투 이동 → 공격)을 따릅니다. 차이는 공격 사거리 수치(AttackRange)뿐이며,
    // AttackKind는 더 이상 이동/전투 분기에 사용되지 않습니다.
    //
    // enum 자체와 UnitStats.GetAttackKind, StatValues.Kind 필드는 Inspector 호환과
    // 향후 UI 분류(아이콘 표시 등) 용도로 보존합니다. 단, UnitView/UnitMovement/UnitCombat의
    // 로직 분기에서는 사용하지 않으므로 값이 어떻게 설정되어도 동작에 영향이 없습니다.
    //
    // Domain 레이어 — 순수 C# 열거형, Unity 의존 없음.
    // ========================================================================
    public enum AttackKind
    {
        /// <summary>
        /// [2026-05-11 분기 미사용] 근접 유닛 식별자 (UI/분류 용도).
        /// 예: EmberSpirit, BearGuard, FlameSpirit, LionKnight.
        /// </summary>
        Melee = 0,

        /// <summary>
        /// [2026-05-11 분기 미사용] 원거리 유닛 식별자 (UI/분류 용도).
        /// 예: Pistoleer, Sniper, FoxMagician, InfernoSpirit, Assault.
        /// </summary>
        Ranged = 1
    }

    public static class UnitStats
    {
        // ====================================================================
        // StatValues
        // UnitStats 내부에서 한 유닛의 전투 수치를 담는 순수 C# 구조체.
        //
        // 이 구조체를 Domain 내부에 두는 이유:
        //   - Initialize() 파라미터를 깔끔하게 표현하고 싶지만,
        //     Infrastructure의 UnitStatEntry를 Domain이 직접 참조하면
        //     "Domain → Infrastructure" 의존이 생겨 레이어 규칙 위반.
        //   - 따라서 Infrastructure가 UnitStatEntry를 StatValues로 복사해
        //     Initialize(dict)에 넘겨주는 방식으로 분리한다.
        // ====================================================================
        public struct StatValues
        {
            public int MaxHp;
            public int AttackPower;
            public float AttackRange;
            public float DetectRange;
            public float MoveSpeed;
            public float AttackCooldown;
            public float[] HitFrameTimes;

            // ────────────────────────────────────────────────────────────
            // Kind
            //   유닛이 근접(Melee)인지 원거리(Ranged)인지 명시적으로 구분.
            //   새 이동/전투 규칙(GameSystemRules.md 규칙 13/15)에서
            //   "감지 → 직선 추적" vs "사거리 진입 → 즉시 공격" 분기를
            //   이 필드 하나로 결정한다.
            //
            //   Inspector에서 드롭다운으로 선택 가능 (Infrastructure의
            //   UnitStatEntry.attackKind 필드에 노출).
            // ────────────────────────────────────────────────────────────
            public AttackKind Kind;

            // ────────────────────────────────────────────────────────────
            // OccupancySize
            // [2026-05-11 비활성화 — 점유 시스템 폐기]
            // 타일 점유 추적이 사라졌으므로 이 값은 더 이상 이동 분기에 사용되지 않습니다.
            // 시그니처 호환(Initialize에서 set, GetOccupancySize에서 get)을 위해 필드는 보존합니다.
            // 향후 완전 제거 예정.
            // ────────────────────────────────────────────────────────────
            public float OccupancySize;
        }

        // 내부 Dictionary. Initialize()가 호출되기 전에는 null.
        private static Dictionary<UnitType, StatValues> _data;

        /// <summary>
        /// UnitStatsConfig(또는 동등한 소스)에서 변환한 StatValues 사전을 주입한다.
        /// GameBootstrapper에서 게임 시작 시 1회 호출하는 것을 상정.
        /// 재호출하면 기존 데이터를 교체.
        /// </summary>
        public static void Initialize(IReadOnlyDictionary<UnitType, StatValues> data)
        {
            if (data == null)
            {
                _data = null;
                return;
            }

            // 방어적 복사 — 외부에서 원본 Dictionary가 바뀌어도 영향 없도록.
            _data = new Dictionary<UnitType, StatValues>(data.Count);
            foreach (var kv in data)
            {
                _data[kv.Key] = kv.Value;
            }
        }

        /// <summary>
        /// 내부용 헬퍼. Dictionary가 초기화됐고 key가 있으면 해당 StatValues 반환,
        /// 아니면 found=false.
        /// </summary>
        private static bool TryGet(UnitType type, out StatValues values)
        {
            if (_data != null && _data.TryGetValue(type, out values))
            {
                return true;
            }
            values = default;
            return false;
        }

        /// <summary> 유닛 타입별 기본 최대 체력. </summary>
        public static int GetMaxHp(UnitType type)
            => TryGet(type, out var v) ? v.MaxHp : 10;

        /// <summary> 유닛 타입별 기본 공격력. </summary>
        public static int GetAttackPower(UnitType type)
            => TryGet(type, out var v) ? v.AttackPower : 1;

        /// <summary>
        /// 유닛 타입별 기본 공격 사거리 (타일 단위).
        /// 0.5 = 근접 (인접 타일에서만 공격 가능, HexCoord 폴백 시 rangeThreshold=1로 보정).
        /// 1.0 이상 = 해당 타일 수 범위 내 공격 가능.
        /// </summary>
        public static float GetAttackRange(UnitType type)
            => TryGet(type, out var v) ? v.AttackRange : 1.0f;

        /// <summary>
        /// 유닛 타입별 적 감지 사거리 (타일 단위).
        ///
        /// 감지 사거리 vs 공격 사거리(AttackRange)의 차이:
        ///   - AttackRange: 실제 데미지를 줄 수 있는 거리. 전투 성립 조건.
        ///   - DetectRange: 적을 인식하고 경로를 전환하기 시작하는 거리. 공격 사거리보다 크거나 같음.
        ///
        /// Config에 값이 들어있지 않거나 0 이하면 AttackRange와 동일한 값으로 폴백해
        /// 기존 "원거리 유닛은 AttackRange와 동일" 동작을 유지한다.
        /// </summary>
        public static float GetDetectRange(UnitType type)
        {
            if (TryGet(type, out var v))
            {
                // DetectRange가 설정돼 있으면 그 값을, 아니면 AttackRange로 폴백.
                return v.DetectRange > 0f ? v.DetectRange : v.AttackRange;
            }
            return GetAttackRange(type);
        }

        /// <summary> 유닛 타입별 이동 속도 (칸/초). 높을수록 빠름. </summary>
        public static float GetMoveSpeed(UnitType type)
            => TryGet(type, out var v) ? v.MoveSpeed : 1.0f;

        /// <summary>
        /// 유닛 타입별 공격 쿨다운 (초) = Attack 클립 길이.
        /// UnitFactory에서 Animator의 실제 클립 길이를 읽어 이 값을 덮어씀.
        /// Config 값은 클립이 없는 경우 fallback으로만 사용.
        /// </summary>
        public static float GetAttackCooldown(UnitType type)
            => TryGet(type, out var v) ? v.AttackCooldown : 1.0f;

        /// <summary>
        /// 공격 애니메이션에서 타격 프레임(OnAttackHit)까지의 시간(초) 배열.
        /// 서버가 애니메이션 RPC를 전송한 뒤, 배열의 각 시간마다 데미지를 개별 적용.
        ///
        /// 단일 히트 유닛은 원소 1개, 다중 히트 유닛(FlameSpirit 6히트, LionKnight 2히트)은
        /// 각 히트 시점을 오름차순으로 나열한 배열을 반환.
        ///
        /// Config 값이 없으면 안전망으로 [0.2f]를 반환.
        /// </summary>
        public static float[] GetHitFrameTimes(UnitType type)
        {
            if (TryGet(type, out var v) && v.HitFrameTimes != null && v.HitFrameTimes.Length > 0)
            {
                return v.HitFrameTimes;
            }
            return new[] { 0.2f };
        }

        /// <summary>
        /// 유닛 타입별 타일 점유 크기.
        /// [2026-05-11 비활성화 — 점유 시스템 폐기]
        /// 시그니처 호환을 위해 메서드는 보존하며, 항상 안전망 값(1f) 또는 Config 값을 반환합니다.
        /// 새 이동/전투 로직은 이 값을 참조하지 않습니다.
        /// </summary>
        public static float GetOccupancySize(UnitType type)
        {
            if (TryGet(type, out var v) && v.OccupancySize > 0f)
                return v.OccupancySize;
            return 1f;
        }

        /// <summary>
        /// 유닛 타입별 공격 방식(Melee / Ranged) 반환.
        /// Config에 값이 등록되지 않은 경우 안전망으로 AttackKind.Ranged를 반환.
        ///
        /// 폴백을 Ranged로 두는 이유:
        ///   - 기존 묵시 가정("DetectRange == AttackRange면 원거리")의 기본 동작이
        ///     "그 자리에서 즉시 공격(=Ranged)"이었기 때문에, 데이터 누락 시
        ///     기존 동작과 가장 가까운 결과가 나오도록 한다.
        ///   - 신규 유닛 추가 시 Inspector에서 명시적으로 Melee를 지정해야 함을
        ///     상기시키는 효과도 있다.
        /// </summary>
        public static AttackKind GetAttackKind(UnitType type)
        {
            if (TryGet(type, out var v))
                return v.Kind;
            return AttackKind.Ranged;
        }
    }
}
