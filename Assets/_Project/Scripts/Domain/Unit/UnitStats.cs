// ============================================================================
// UnitStats.cs
// 유닛 타입별 기본 스탯 정의.
//
// UnitType에 따른 MaxHp, AttackPower, AttackRange 기본값을 한 곳에서 관리.
// UnitSpawnUseCase에서 유닛 생성 시 참조.
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
            UnitType.Pistoleer => 30,
            UnitType.Assault   => 50,
            UnitType.Sniper    => 30,
            _                  => 10
        };

        /// <summary> 유닛 타입별 기본 공격력. </summary>
        public static int GetAttackPower(UnitType type) => type switch
        {
            UnitType.Pistoleer => 6,
            UnitType.Assault   => 1,
            UnitType.Sniper    => 10,
            _                  => 1
        };

        /// <summary> 유닛 타입별 기본 공격 사거리 (월드 단위). </summary>
        public static float GetAttackRange(UnitType type) => type switch
        {
            UnitType.Pistoleer => 1.0f,
            UnitType.Assault   => 2.0f,
            UnitType.Sniper    => 5.0f,
            _                  => 1.0f
        };

        /// <summary> 유닛 타입별 이동 속도 (칸/초). 높을수록 빠름. </summary>
        public static float GetMoveSpeed(UnitType type) => type switch
        {
            UnitType.Pistoleer => 1.0f,
            UnitType.Assault   => 1.0f,
            UnitType.Sniper    => 0.25f,
            _                  => 1.0f
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
            UnitType.Assault   => 0.2f,  // Assault_Attack.anim 클립 길이
            UnitType.Pistoleer => 2.0f,  // Pistoleer_Attack.anim 클립 길이
            UnitType.Sniper    => 3.0f,  // Sniper_Attack.anim 클립 길이
            _                  => 1.0f
        };

        /// <summary>
        /// 공격 애니메이션에서 타격 프레임(Animation Event OnAttackHit)까지의 시간 (초).
        /// 서버가 애니메이션 RPC를 전송한 뒤, 이 시간만큼 대기 후 실제 데미지를 적용.
        /// Attack.anim 타임라인에서 OnAttackHit 이벤트 위치(초)와 일치시켜야 함.
        ///
        /// 기본값은 0.2초. 각 유닛 ScriptableObject의 Inspector에서 실측값으로 덮어씀.
        /// Inspector 미설정 시(0f) 최소 1프레임 대기 후 데미지 적용 (안전망).
        /// </summary>
        public static float GetHitFrameTime(UnitType type) => type switch
        {
            // Animation Event(OnAttackHit) 위치 실측값 (초:프레임 → 초 변환, 30fps 기준)
            UnitType.Assault   => 0.133f, // 0:04 → 4/30
            UnitType.Pistoleer => 0.833f, // 0:25 → 25/30
            UnitType.Sniper    => 2.000f, // 2:00 → 2초
            _                  => 0.2f
        };
    }
}
