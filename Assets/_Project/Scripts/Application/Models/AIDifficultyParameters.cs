// ============================================================================
// AIDifficultyParameters.cs
// Bootstrap에서 선택한 AI 난이도 설정을 그대로 전달하는 불변 값 스냅샷.
// 직렬화 설정과 분리하며 값의 검증·보정·배율 계산을 수행하지 않는다.
// ============================================================================

namespace Hexiege.Application
{
    /// <summary>
    /// AI 실행에 전달하는 한 난이도의 파라미터 값 묶음.
    /// 기존 설정에서 복사한 값을 보존하며 Infrastructure나 Unity에 의존하지 않는다.
    /// </summary>
    public readonly struct AIDifficultyParameters
    {
        /// <summary>AI 채굴소 수입 배율.</summary>
        public readonly float goldIncomeMultiplier;

        /// <summary>설정의 생산 시간 배율. 이 값의 전달이 생산 적용을 의미하지는 않는다.</summary>
        public readonly float productionTimeMultiplier;

        /// <summary>반응 시스템의 상태 점검 주기(초).</summary>
        public readonly float decisionInterval;

        /// <summary>긴급 생산 활성화에 사용하는 플레이어 대비 유닛 수 비율.</summary>
        public readonly float unitCountThreshold;

        /// <summary>빌드오더 가속을 검토할 골드 임계값.</summary>
        public readonly float goldExcessThreshold;

        /// <summary>빌드오더 가속 쿨다운(초).</summary>
        public readonly float buildOrderAccelCooldown;

        /// <summary>긴급 생산 활성화 쿨다운(초).</summary>
        public readonly float productionActivationCooldown;

        /// <summary>실패한 빌드오더 단계의 재시도 간격(초).</summary>
        public readonly float retryInterval;

        /// <summary>채굴소 탐지 및 재건 시도 주기(초).</summary>
        public readonly float mineCheckInterval;

        /// <summary>선택된 설정의 아홉 값을 변경 없이 보관한다.</summary>
        public AIDifficultyParameters(
            float goldIncomeMultiplier,
            float productionTimeMultiplier,
            float decisionInterval,
            float unitCountThreshold,
            float goldExcessThreshold,
            float buildOrderAccelCooldown,
            float productionActivationCooldown,
            float retryInterval,
            float mineCheckInterval)
        {
            this.goldIncomeMultiplier = goldIncomeMultiplier;
            this.productionTimeMultiplier = productionTimeMultiplier;
            this.decisionInterval = decisionInterval;
            this.unitCountThreshold = unitCountThreshold;
            this.goldExcessThreshold = goldExcessThreshold;
            this.buildOrderAccelCooldown = buildOrderAccelCooldown;
            this.productionActivationCooldown = productionActivationCooldown;
            this.retryInterval = retryInterval;
            this.mineCheckInterval = mineCheckInterval;
        }
    }
}
