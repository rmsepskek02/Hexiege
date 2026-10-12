using System;
using UnityEngine;
using UniRx;
using Hexiege.Application;
using Hexiege.Core;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    /// <summary>파도와 확정 접촉 텍스트의 단일 표현 소비자다. 객체 수명은 Bootstrap에 귀속한다.</summary>
    public sealed class WavePresentationPresenter : MonoBehaviour
    {
        private readonly WavePresentationCoordinator _coordinator = new();
        private UnitCombatUseCase _combat;
        private FloatingHpTextSpawner _texts;
        private IDisposable _endSubscription;
        private ulong _epoch;
        private double _lastTime;
        private int _reportedFailures;
        private bool _invalidClockEpisode;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private WavePresentationDiagnostics _diagnostics = new("presentation");
#endif

        /// <summary>재초기화 전에 구독을 해제하고 현재 경기 clock/baseline을 연결한다.</summary>
        public void Initialize(UnitCombatUseCase combat, FloatingHpTextSpawner texts)
        {
            Detach();
            if (combat == null || texts == null || !texts.IsReadyForDamagePresentation || EffectManager.Instance == null
                || !EffectManager.Instance.WavePresentationReady) return;
            _combat = combat; _texts = texts; _epoch = combat.WaveEpoch;
            _lastTime = combat.WaveServerTime;
            if (double.IsNaN(_lastTime) || double.IsInfinity(_lastTime) || _lastTime < 0)
            { _combat = null; return; }
            _coordinator.Reset(_epoch, _lastTime, combat.WavePresentationBaselineId);
            _reportedFailures = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _diagnostics = new("presentation"); _diagnostics.Begin(_epoch, _lastTime);
#endif
            _invalidClockEpisode = false;
            combat.WaveSpawned += ReceiveSpawn;
            combat.WaveContactConfirmed += ReceiveContact;
            _endSubscription = GameEvents.OnGameEnd.Subscribe(_ => {
                ObserveEnd("game-end", false);
                _coordinator.Ended = true;
            });
            combat.SetWavePresentationOwned(true);
        }

        private void EnsureEpoch(double time)
        {
            if (_epoch == _combat.WaveEpoch) return;
            ObserveEnd("epoch-change", true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _diagnostics = new("presentation");
#endif
            _epoch = _combat.WaveEpoch;
            _lastTime = time;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _diagnostics.Begin(_epoch, _lastTime);
#endif
            // 비유한 raw 시각을 정상 값으로 위조하지 않는다. reset은 서버 snapshot baseline만 사용한다.
            double baseline = _combat.WavePresentationBaseline;
            if (!double.IsNaN(time) && !double.IsInfinity(time) && time >= 0) baseline = Math.Max(time, baseline);
            _coordinator.Reset(_epoch, baseline, _combat.WavePresentationBaselineId);
        }
        private void ReceiveSpawn(WavePresentationSpawn spawn)
        {
            double raw = _combat.WaveServerTime;
            EnsureEpoch(raw);
            _coordinator.Accept(spawn, raw, Time.realtimeSinceStartupAsDouble);
        }
        private void ReceiveContact(WavePresentationContact contact)
        {
            double raw = _combat.WaveServerTime;
            EnsureEpoch(raw);
            _coordinator.Accept(contact, raw, Time.realtimeSinceStartupAsDouble);
        }
        private void Update()
        {
            if (_combat == null) return;
            double time = _combat.WaveServerTime;
            if (double.IsNaN(time) || double.IsInfinity(time))
            {
                if (!_invalidClockEpisode)
                    GameLog.Dev.Warn("Combat", nameof(WavePresentationPresenter), "Invalid wave presentation clock");
                _invalidClockEpisode = true;
            }
            else _invalidClockEpisode = false;
            // Tick은 raw 한 표본만 읽는다. 로컬 monotonic 표본은 ClockPending 만료에만 사용한다.
            EnsureEpoch(time); _lastTime = time;
            _coordinator.AdvanceBaseline(_combat.WavePresentationBaseline, _combat.WavePresentationBaselineId);
            _coordinator.Tick(time, EmitSpawn, EmitContact, Time.realtimeSinceStartupAsDouble);
            if (_coordinator.Failures != _reportedFailures)
            {
                _reportedFailures = _coordinator.Failures;
                GameLog.Dev.Warn("Combat", nameof(WavePresentationPresenter), "Wave presentation contract failure",
                    $"failures={_reportedFailures}, hpAuthorityUnchanged=true");
            }
        }
        private bool EmitSpawn(WavePresentationSpawn spawn, double age)
        {
            var effects = EffectManager.Instance;
            if (effects == null) return false;
            var owner = _combat;
            var item = effects.PlayWave(spawn, () => owner.WaveServerTime);
            if (item == null) return false;
            AudioManager.Instance?.PlayUnitAttackSfx(UnitType.TorrentSpirit);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try {
            double emitted = owner.WaveServerTime;
            _diagnostics.Record("vfx-emitter-success", spawn.Key, emitted, detail:
                $"spawn={spawn.SpawnTime:F6};schedulerNow={_lastTime:F6};presentation={emitted - WavePresentationCoordinator.Delay:F6};age={age:F6};particleSystems={item.ParticleSystemCount};playbackActive={item.IsPlaybackActive};viewX={item.transform.position.x:F3};viewY={item.transform.position.y:F3};viewZ={item.transform.position.z:F3};flipped={ViewConverter.IsFlipped}");
            } catch { _diagnostics.ObserveFault("vfx-emitter-result"); }
#endif
            return true;
        }
        private bool EmitContact(WavePresentationContact contact)
        {
            if (_texts == null) return false;
            Vector3 position = ViewConverter.ToView(contact.Position);
            bool success = contact.Heal ? _texts.TryShowHealAt(contact.ResultingHp, position)
                : _texts.TryShowDamageAt(contact.ResultingHp, contact.Team, position);
            ObserveTextResult(contact, success, position);
            return success;
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveTextResult(WavePresentationContact contact, bool success, Vector3 view)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!success) return; // 최초 실패는 Coordinator의 기존 ChannelFailed set에서 관측한다.
            try {
            double emitted = _combat.WaveServerTime;
            _diagnostics.Record("text-emitter-success", contact.Key, emitted, contact.Ordinal, contact.VictimId,
                contact.VictimIsUnit ? "unit" : "building",
                $"contact={contact.ContactTime:F6};schedulerNow={_lastTime:F6};presentation={emitted - WavePresentationCoordinator.Delay:F6};lag={emitted - WavePresentationCoordinator.Delay - contact.ContactTime:F6};heal={contact.Heal};hp={contact.ResultingHp};applied={contact.AppliedAmount};domainX={contact.Position.x:F3};domainY={contact.Position.y:F3};domainZ={contact.Position.z:F3};viewX={view.x:F3};viewY={view.y:F3};viewZ={view.z:F3}");
            } catch { _diagnostics.ObserveFault("text-emitter-result"); }
#endif
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveEnd(string reason, bool final)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try {
                _diagnostics.Finish(reason, _lastTime, final: final);
                _coordinator.ObserveEnd(reason, _lastTime, final);
                if (!final) _combat?.FinishWaveDiagnostics(reason, false);
            } catch { _diagnostics.ObserveFault("presenter-end"); }
#endif
        }

        private void Detach()
        {
            ObserveEnd("detach", true);
            if (_combat != null)
            { _combat.SetWavePresentationOwned(false);
              _combat.WaveSpawned -= ReceiveSpawn; _combat.WaveContactConfirmed -= ReceiveContact; }
            _endSubscription?.Dispose(); _endSubscription = null;
            _combat = null;
            _coordinator.Close();
        }
        private void OnDestroy() { Detach(); _combat = null; }
    }
}
