using Hexiege.Application;
using Hexiege.Application.Combat.Sequencing;
using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// C3 조합 수명만 담당하는 read-only Presenter다. 효과/HP/Animator 의존성을 받지 않으며
    /// 모든 비교는 순수 Application scheduler로 전달한다.
    /// </summary>
    public sealed class UnitAttackResultPresentationShadow : MonoBehaviour
    {
        public void Initialize()
        {
            bool shouldBeActive = NetworkContext.ActiveCombatPipelineMode
                == CombatPipelineMode.PresentationShadow
                || NetworkContext.ActiveCombatPipelineMode == CombatPipelineMode.ResultPresentation;
            // NetworkGameFlow가 synchronized clock anchor와 함께 이미 시작했다면 상태를
            // 재초기화하지 않는다. 맵 조합이 먼저인 Legacy/싱글 경기만 명시적으로 닫는다.
            if (shouldBeActive)
            {
                if (!UnitAttackResultPresentationShadowBridge.IsActive)
                    UnitAttackResultPresentationShadowBridge.BeginMatch(true);
            }
            else
            {
                UnitAttackResultPresentationShadowBridge.BeginMatch(false);
            }
        }

        private void Update()
        {
            double localTime = Time.realtimeSinceStartupAsDouble;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // 실패 시점에만 최근 프레임 간격을 확인하며 프레임별 로그는 남기지 않는다.
            Hexiege.Infrastructure.UnitAttackShadowObserver.RecordPresentationFrame(localTime);
#endif
            UnitAttackResultPresentationShadowBridge.TickCoordinator(localTime);
        }

        private void OnDestroy()
        {
            UnitAttackResultPresentationShadowBridge.EndMatch();
        }
    }
}
