// ============================================================================
// SkillExecutorRegistry.cs
// SkillMechanicType → ISkillExecutor 매핑 테이블.
//
// 역할:
//   스킬의 실행 방식(A/B/C)에 맞는 실행기를 조회한다. 등록되지 않은 타입은 null을 반환하며,
//   SkillActivationUseCase는 실행을 건너뛴다(발동 무효).
//
// 등록 현황:
//   타입 A(InstantAreaDamage)  → InstantAreaDamageExecutor  (Phase 1)
//   타입 B(AreaDotDamage)      → AreaDotDamageExecutor      (Phase 1)
//   타입 C(GlobalStatusChange) → GlobalStatusChangeExecutor (Phase 2 — 전역 상태변경)
//
// 왜 인스펙터 배선이 아닌 코드 등록인가:
//   SkillMechanicType 키 매핑이라 ScriptableObject/인스펙터 배선이 필요 없다.
//   SpecialAttackRegistry(UnitType 키)와 동일한 성격의 순수 전략 테이블이다.
//
// Application 레이어 — Domain에만 의존. 외부 의존이 없어 SkillActivationUseCase가 내부에서 생성한다.
// ============================================================================

using System.Collections.Generic;
using Hexiege.Domain;

namespace Hexiege.Application
{
    /// <summary>
    /// 스킬 메커니즘 타입별 실행기 조회 테이블.
    /// </summary>
    public sealed class SkillExecutorRegistry
    {
        // SkillMechanicType → 실행기. 실행기는 상태가 없어 타입당 1개 인스턴스를 공유한다.
        private readonly Dictionary<SkillMechanicType, ISkillExecutor> _executors
            = new Dictionary<SkillMechanicType, ISkillExecutor>();

        /// <summary>
        /// 기본 실행기들을 등록한다(타입 A·B·C). 실행기는 상태가 없어 UseCase가 내부에서 1회 생성한다.
        /// </summary>
        public SkillExecutorRegistry()
        {
            // 타입 A — 즉발 범위 피해.
            _executors[SkillMechanicType.InstantAreaDamage] = new InstantAreaDamageExecutor();

            // 타입 B — 범위 지속 피해(장판).
            _executors[SkillMechanicType.AreaDotDamage] = new AreaDotDamageExecutor();

            // 타입 C — 전역 상태변경(버프/디버프/제어/회복). 조준 없음(전역 즉시).
            //   실제 유닛 순회·상태 부여·회복은 SkillActivationContext의 상태 부여 델리게이트를 통해
            //   UnitCombatUseCase가 담당한다(서버 권위). 실행기는 스킬 데이터만 읽는다.
            _executors[SkillMechanicType.GlobalStatusChange] = new GlobalStatusChangeExecutor();
        }

        /// <summary>
        /// 해당 메커니즘 타입의 실행기를 반환. 없으면 null(미등록 = 발동 무효).
        /// </summary>
        /// <param name="mechanic">조회할 스킬 메커니즘 타입.</param>
        /// <returns>등록된 실행기 또는 null.</returns>
        public ISkillExecutor TryGet(SkillMechanicType mechanic)
        {
            _executors.TryGetValue(mechanic, out var executor);
            return executor;
        }
    }
}
