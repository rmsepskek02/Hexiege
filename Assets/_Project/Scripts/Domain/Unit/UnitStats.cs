// ============================================================================
// UnitStats.cs
// 유닛 타입별 기본 스탯 정의.
//
// UnitType에 따른 MaxHp, AttackPower, AttackRange 기본값을 한 곳에서 관리.
// UnitSpawnUseCase에서 유닛 생성 시 참조.
//
// 종족별 독립 유닛 구조:
//   - 인간계(Human): Pistoleer, Assault, Sniper
//   - 정령계(Spirit): FlameSpirit, EmberSpirit, InfernoSpirit
//   - 초월계(Transcendence): BearGuard, FoxMagician, LionKnight
//
// 모든 스탯은 StatsReference.md 기준 확정값.
//
// Domain 레이어 — 순수 C#, Unity 의존 없음.
// ============================================================================

namespace Hexiege.Domain
{
    public static class UnitStats
    {
        /// <summary> 유닛 타입별 기본 최대 체력. </summary>
        public static int GetMaxHp(UnitType type) => type switch
        {
            // ── 인간계 ──
            UnitType.Pistoleer    => 30,
            UnitType.Assault      => 50,
            UnitType.Sniper       => 30,
            // ── 정령계 ──
            UnitType.EmberSpirit  => 30,
            UnitType.FlameSpirit  => 50,
            UnitType.InfernoSpirit => 100,
            // ── 초월계 ──
            UnitType.FoxMagician  => 20,
            UnitType.BearGuard    => 200,
            UnitType.LionKnight   => 50,
            _                     => 10
        };

        /// <summary> 유닛 타입별 기본 공격력. </summary>
        public static int GetAttackPower(UnitType type) => type switch
        {
            // ── 인간계 ──
            UnitType.Pistoleer    => 6,
            UnitType.Assault      => 1,
            UnitType.Sniper       => 10,
            // ── 정령계 ──
            UnitType.EmberSpirit  => 5,
            UnitType.FlameSpirit  => 2,    // 6히트 공격 (총 데미지 = 2 × 6 = 12)
            UnitType.InfernoSpirit => 25,
            // ── 초월계 ──
            UnitType.FoxMagician  => 8,
            UnitType.BearGuard    => 10,
            UnitType.LionKnight   => 9,    // 2히트 공격 (총 데미지 = 9 × 2 = 18)
            _                     => 1
        };

        /// <summary>
        /// 유닛 타입별 기본 공격 사거리 (타일 단위).
        /// 0.5 = 근접 (인접 타일에서만 공격 가능, HexCoord 폴백 시 rangeThreshold=1로 보정).
        /// 1.0 이상 = 해당 타일 수 범위 내 공격 가능.
        /// </summary>
        public static float GetAttackRange(UnitType type) => type switch
        {
            // ── 인간계 ──
            UnitType.Pistoleer    => 1.0f,
            UnitType.Assault      => 2.0f,
            UnitType.Sniper       => 5.0f,
            // ── 정령계 ── (StatsReference.md 확정값)
            UnitType.FlameSpirit  => 0.5f,   // 근접
            UnitType.EmberSpirit  => 0.5f,   // 근접
            UnitType.InfernoSpirit => 4.0f,   // 원거리
            // ── 초월계 ── (StatsReference.md 확정값)
            UnitType.BearGuard    => 0.5f,   // 근접
            UnitType.FoxMagician  => 3.0f,   // 원거리
            UnitType.LionKnight   => 0.5f,   // 근접
            _                     => 1.0f
        };

        /// <summary>
        /// 유닛 타입별 적 감지 사거리 (타일 단위).
        ///
        /// 감지 사거리 vs 공격 사거리(AttackRange)의 차이:
        ///   - AttackRange: 실제 데미지를 줄 수 있는 거리. 전투 성립 조건.
        ///   - DetectRange: 적을 인식하고 경로를 전환하기 시작하는 거리. 공격 사거리보다 크거나 같음.
        ///
        /// 근접유닛(AttackRange=0.5)은 DetectRange=1.0으로 설정하여
        /// 인접 타일(HexCoord Distance=1, 월드 0.866f)에 있는 적을 감지 가능하게 함.
        /// 감지 후 공격 사거리 내로 접근하여 실제 공격이 시작됨.
        ///
        /// 원거리유닛은 AttackRange와 동일값을 반환하여 기존 동작을 완전히 유지.
        /// (감지되는 순간 이미 공격 사거리 내에 있음)
        /// </summary>
        public static float GetDetectRange(UnitType type) => type switch
        {
            // ── 근접유닛 (AttackRange=0.5) — 인접 타일(1.0)까지 감지 확장 ──
            UnitType.FlameSpirit  => 1.0f,
            UnitType.EmberSpirit  => 1.0f,
            UnitType.BearGuard    => 1.0f,
            UnitType.LionKnight   => 1.0f,
            // ── 원거리유닛 — AttackRange와 동일 (기존 동작 유지) ──
            _                     => GetAttackRange(type)
        };

        /// <summary> 유닛 타입별 이동 속도 (칸/초). 높을수록 빠름. </summary>
        public static float GetMoveSpeed(UnitType type) => type switch
        {
            // ── 인간계 ──
            UnitType.Pistoleer    => 0.5f,
            UnitType.Assault      => 1.0f,
            UnitType.Sniper       => 0.25f,
            // ── 정령계 ── (StatsReference.md 확정값)
            UnitType.FlameSpirit  => 2.0f,
            UnitType.EmberSpirit  => 0.5f,
            UnitType.InfernoSpirit => 1.0f,
            // ── 초월계 ── (StatsReference.md 확정값)
            UnitType.BearGuard    => 1.0f,
            UnitType.FoxMagician  => 0.5f,
            UnitType.LionKnight   => 2.0f,
            _                     => 1.0f
        };

        /// <summary>
        /// 유닛 타입별 공격 쿨다운 (초) = Attack 클립 길이.
        /// 쿨다운 만료 = 한 공격 애니메이션 사이클 완료.
        ///
        /// UnitFactory에서 Animator의 실제 클립 길이를 읽어 이 값을 덮어씀.
        /// 이 값은 클립 길이와 항상 일치해야 함 (참조용).
        /// </summary>
        public static float GetAttackCooldown(UnitType type) => type switch
        {
            // ── 인간계 ──
            UnitType.Assault      => 0.2f,    // Assault_Attack.anim 클립 길이
            UnitType.Pistoleer    => 2.0f,    // Pistoleer_Attack.anim 클립 길이
            UnitType.Sniper       => 3.0f,    // Sniper_Attack.anim 클립 길이
            // ── 정령계 ── (StatsReference.md 확정값: 괄호 안 = 클립 총 길이)
            UnitType.FlameSpirit  => 3.0f,    // 3:00 = 3.0초 (6히트 공격)
            UnitType.EmberSpirit  => 2.33f,   // 2:20 = 2.33초
            UnitType.InfernoSpirit => 3.0f,    // 3:00 = 3.0초
            // ── 초월계 ── (StatsReference.md 확정값)
            UnitType.BearGuard    => 1.33f,   // 1:20 = 1.33초
            UnitType.FoxMagician  => 4.0f,    // 4:00 = 4.0초
            UnitType.LionKnight   => 2.33f,   // 2:20 = 2.33초 (2히트 공격)
            _                     => 1.0f
        };

        /// <summary>
        /// 공격 애니메이션에서 타격 프레임(Animation Event OnAttackHit)까지의 시간 (초).
        /// 서버가 애니메이션 RPC를 전송한 뒤, 이 시간만큼 대기 후 실제 데미지를 적용.
        /// Attack.anim 타임라인에서 OnAttackHit 이벤트 위치(초)와 일치시켜야 함.
        ///
        /// 다중 히트 유닛(FlameSpirit 6히트, LionKnight 2히트)은 첫 번째 히트 프레임만 적용.
        /// 다중 히트 구현은 별도 작업에서 처리 예정.
        ///
        /// 기본값은 0.2초. Inspector 미설정 시(0f) 최소 1프레임 대기 후 데미지 적용 (안전망).
        /// </summary>
        public static float GetHitFrameTime(UnitType type) => type switch
        {
            // Animation Event(OnAttackHit) 위치 실측값 (초:프레임 → 초 변환, 30fps 기준)
            // ── 인간계 ──
            UnitType.Assault      => 0.133f,  // 0:04 → 4/30
            UnitType.Pistoleer    => 0.833f,  // 0:25 → 25/30
            UnitType.Sniper       => 2.000f,  // 2:00 → 2초
            // ── 정령계 ── (StatsReference.md 첫 번째 히트 프레임)
            UnitType.FlameSpirit  => 0.667f,  // 0:20 → 20/30
            UnitType.EmberSpirit  => 1.000f,  // 1:00 → 1초
            UnitType.InfernoSpirit => 1.250f,  // 1:15 → (1*30+15)/30 = 45/30
            // ── 초월계 ── (StatsReference.md 첫 번째 히트 프레임)
            UnitType.BearGuard    => 0.667f,  // 0:20 → 20/30
            UnitType.FoxMagician  => 2.417f,  // 2:25 → (2*30+25)/30 = 85/30
            UnitType.LionKnight   => 0.250f,  // 0:15 = 0.250초 (2히트 공격 — 첫 번째 히트 프레임)
            _                     => 0.2f
        };
    }
}
