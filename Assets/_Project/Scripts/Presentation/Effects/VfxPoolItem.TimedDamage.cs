using System;
using UnityEngine;
using Hexiege.Application;
namespace Hexiege.Presentation
{
    public partial class VfxPoolItem
    {
        private ParticleSystem[] _damageSystems;
        private bool _damageLoop, _damageStopped;
        private ulong _damageLease;
        private TimedDamageEffectPresentation _damageState;
        private TimedDamageEffectChannel _damageChannel;
        private Vector3 _damageOffset, _damageEuler;
        public ulong DamageLease => _damageLease;
        public void RejectTimedDamageAcquire()
        {
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            _active = false; _damageLoop = false; _damageChannel = null; gameObject.SetActive(false); _onReturn?.Invoke(this);
        }
        public void PlayTimedDamage(TimedDamageEffectPresentation state, Transform pose, EffectPreset preset, TimedDamageEffectChannel channel)
        {
            if (_active || pose == null || !state.Active || !state.IsValid) throw new InvalidOperationException("Damage lease unavailable");
            _damageSystems ??= GetComponentsInChildren<ParticleSystem>(true);
            if (_damageSystems.Length == 0) throw new InvalidOperationException("Damage particles missing");
            foreach (var ps in _damageSystems)
                if (!ps.main.loop || ps.main.stopAction != ParticleSystemStopAction.None) throw new InvalidOperationException("Damage loop contract");
            ResetTravelState(); _damageLease++; if (_damageLease == 0) _damageLease++;
            _damageState = state; _damageChannel = channel; _damageLoop = true; _damageStopped = false;
            _damageOffset = preset.LocalPositionOffset; _damageEuler = preset.EulerRotationOffset;
            transform.localScale = Vector3.Scale(_originalLocalScale, preset.ScaleMultiplier);
            FollowTimedDamage(_damageLease, pose); gameObject.SetActive(true); _active = true;
            foreach (var ps in _damageSystems)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Play(false);
                if (!ps.isPlaying) throw new InvalidOperationException("Recursive Damage particle playback inactive: " + ps.name);
            }
            channel.Record("Play", state, "RecursivePS=" + _damageSystems.Length);
        }
        public void FollowTimedDamage(ulong lease, Transform pose)
        { if (lease != _damageLease || pose == null) return; transform.SetPositionAndRotation(pose.position + pose.rotation * _damageOffset, pose.rotation * Quaternion.Euler(_damageEuler)); }
        public void RefreshTimedDamage(ulong lease, TimedDamageEffectPresentation state)
        { if (_active && _damageLoop && !_damageStopped && lease == _damageLease && _damageState.SameOwner(state)
            && state.Revision >= _damageState.Revision && state.Visual == _damageState.Visual) _damageState = state; }
        public void StopTimedDamage(ulong lease, string reason)
        {
            if (!_active || !_damageLoop || _damageStopped || lease != _damageLease) return;
            _damageStopped = true;
            foreach (var ps in _damageSystems) if (ps != null) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            _damageChannel.Record("StopEmitting", _damageState, reason);
        }
        private bool DamageCanReturn()
        {
            if (!_damageLoop) return true;
            if (!_damageStopped) return false;
            foreach (var ps in _damageSystems) if (ps != null && ps.IsAlive(false)) return false;
            return true;
        }
        private void OnDisable()
        {
            if (!_active || !_damageLoop) return;
            _damageChannel?.Record("Teardown", _damageState, "ForceDisable; NaturalDrain=False; Completeness=Unknown");
            foreach (var ps in _damageSystems) if (ps != null) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            _damageLoop = false; _damageStopped = false; _damageChannel = null; _active = false;
            // Forced cleanup is not a natural return and does not enqueue a destroyed/disabled lease.
        }
    }
}
