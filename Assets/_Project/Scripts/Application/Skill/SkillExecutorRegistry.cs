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
//   AreaTowerDisable           → AreaTowerDisableExecutor   (2026-11-04 EMP Task 커밋 1 — 지점 범위 적 방어탑 무력화)
//   PersistentStatusZone       → PersistentStatusZoneExecutor (2026-11-11 지속 상태 구역 — 서리 감옥 · 수액 늪)
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
        /// 기본 실행기들을 등록한다(타입 A·B·C + 방어탑 무력화). 실행기는 상태가 없어 UseCase가 내부에서 1회 생성한다.
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

            // 지점 범위 적 방어탑 무력화(EMP — 2026-11-04 EMP Task 커밋 1). 조준 지점 원 안의 적 방어탑을 지속시간 동안 공격 불가로.
            //   실제 탑 선정 · 상태 부여는 SkillActivationContext의 무력화 다리를 통해 BuildingStatusEffectSystem이 담당한다.
            //   ⚠️ 등록만으로 EMP가 동작하지는 않는다 — EMP 자산은 커밋 4 전까지 「사용 불가」 · 타입 A 자리표시로 남아 있어
            //      SkillActivationUseCase가 실행기 조회 전에 거부한다(이 실행기는 아직 어떤 자산도 부르지 않는다).
            _executors[SkillMechanicType.AreaTowerDisable] = new AreaTowerDisableExecutor();

            // 지속 상태 구역(서리 감옥 · 수액 늪 — 2026-11-11 지속 상태 구역 Task, Z-16).
            //   조준 지점에 구역을 남겨 구역 안 대상 유닛에게 빙결 · 이동 배율을 건다.
            //   실제 구역 보관 · 구성원 판정 · 만료는 SkillActivationContext의 구역 다리를 통해 SkillZoneSystem이 담당한다.
            _executors[SkillMechanicType.PersistentStatusZone] = new PersistentStatusZoneExecutor();
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
