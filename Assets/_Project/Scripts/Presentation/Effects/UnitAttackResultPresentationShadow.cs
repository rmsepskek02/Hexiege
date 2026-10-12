using System;
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
        // 실제 Update와 기존 production fixture가 같은 managed 경로를 사용한다.
        // NGO 준비 상태와 이번 경기 서비스의 수명 검사는 조합 루트 Bootstrap의 책임이다.
        public sealed class CanonicalTickClock
        {
            private Func<double> _read;
            public void Bind(Func<double> read) => _read = read;
            public void Close() => _read = null;
            public bool Tick(double localTime, Action<double, double> frameObserver = null)
            {
                if (!UnitAttackResultPresentationShadowBridge.IsActive) return false;
                if (_read == null)
                {
                    UnitAttackResultPresentationShadowBridge.ReportCanonicalClockFailure("ProviderUnavailable", "Clock binding is closed or missing.");
                    return false;
                }
                double serverTime;
                // 공급자 조회만 격리한다. Coordinator/표현 예외를 clock fault로 오분류하지 않는다.
                try { serverTime = _read(); }
                catch (Exception error)
                {
                    UnitAttackResultPresentationShadowBridge.ReportCanonicalClockFailure("ProviderFault", error.GetType().Name + ": " + error.Message);
                    return false;
                }
                if (!UnitAttackResultPresentationShadowBridge.IsValidCanonicalClock(serverTime, localTime))
                {
                    UnitAttackResultPresentationShadowBridge.ReportCanonicalClockFailure("InvalidClock", "Non-finite, negative, inactive transport or retired owner clock.");
                    return false;
                }
                // 관측에는 이미 조회한 숫자만 전달한다. 관측 실패가 실제 Tick을 막으면 안 된다.
                try { frameObserver?.Invoke(localTime, serverTime); }
                catch (Exception)
                {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    UnitAttackResultPresentationShadowBridge.ReportCanonicalDiagnosticFault();
#endif
                }
                return UnitAttackResultPresentationShadowBridge.TickCoordinator(serverTime, localTime);
            }
        }
        private readonly CanonicalTickClock _clock = new();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static readonly Action<double, double> FrameObserver = Hexiege.Infrastructure.UnitAttackShadowObserver.RecordPresentationFrame;
#endif

        public void Initialize(Func<double> canonicalServerClock)
        {
            _clock.Bind(canonicalServerClock);
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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _clock.Tick(Time.realtimeSinceStartupAsDouble, FrameObserver);
#else
            _clock.Tick(Time.realtimeSinceStartupAsDouble);
#endif
        }

        public void CloseClock() => _clock.Close();
        private void OnDisable() => CloseClock();

        private void OnDestroy()
        {
            CloseClock();
            UnitAttackResultPresentationShadowBridge.EndMatch();
        }
    }
}
