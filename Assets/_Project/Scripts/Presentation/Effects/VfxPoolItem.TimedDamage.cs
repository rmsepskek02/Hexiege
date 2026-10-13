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
        /// <summary>
        /// 화상·독 지속 이펙트를 대상 유닛에 붙여 재생한다.
        /// fit(대상 크기 맞춤)은 재생 시작 때 1번만 적용되며, 같은 owner refresh는 재시작하지 않으므로 크기도 유지된다.
        /// fit을 생략하면 Identity(배율 1, 보정 0) — 예전과 완전히 같은 크기·위치다.
        /// </summary>
        /// <param name="state">서버가 정한 지속 피해 표현 상태.</param>
        /// <param name="pose">대상 유닛의 VisualRoot Transform(발밑 기준).</param>
        /// <param name="preset">화상/독 프리셋.</param>
        /// <param name="channel">진단 기록 채널.</param>
        /// <param name="fit">대상 유닛 크기 맞춤(균등 배율 + 발밑 보정).</param>
        public void PlayTimedDamage(TimedDamageEffectPresentation state, Transform pose, EffectPreset preset, TimedDamageEffectChannel channel,
            RecipientFit fit = default)
        {
            if (_active || pose == null || !state.Active || !state.IsValid) throw new InvalidOperationException("Damage lease unavailable");
            _damageSystems ??= GetComponentsInChildren<ParticleSystem>(true);
            if (_damageSystems.Length == 0) throw new InvalidOperationException("Damage particles missing");
            foreach (var ps in _damageSystems)
                if (!ps.main.loop || ps.main.stopAction != ParticleSystemStopAction.None) throw new InvalidOperationException("Damage loop contract");
            ResetTravelState(); _damageLease++; if (_damageLease == 0) _damageLease++;
            _damageState = state; _damageChannel = channel; _damageLoop = true; _damageStopped = false;
            // [C] 위치 보정 = 프리셋 보정 + 크기 표의 발밑 보정. 미리 더해 두면 Follow는 예전 계산 그대로다.
            _damageOffset = preset.LocalPositionOffset + fit.SafeFootOffset; _damageEuler = preset.EulerRotationOffset;
            // [R-4 비활성화 — 2026-10-13] 대상과 무관한 고정 배율. 작은 유닛엔 너무 크고 큰 유닛엔 어색했다.
            //   최종 삭제는 사용자 테스트 통과 후(WORKFLOW [4]).
            // transform.localScale = Vector3.Scale(_originalLocalScale, preset.ScaleMultiplier);
            // 새 크기 = 프리팹 원본 × 프리셋 배율(기존 조정 수단 그대로) × 대상 맞춤 균등 배율.
            //   균등(한 값)이라 Hierarchy 스케일링 파티클이 납작/길쭉하게 찌그러지지 않는다(D-3).
            //   Return()이 _originalLocalScale로 되돌리므로 다음 재사용에 이 배율이 남지 않는다.
            transform.localScale = Vector3.Scale(_originalLocalScale, preset.ScaleMultiplier) * fit.UniformScale;
            FollowTimedDamage(_damageLease, pose); gameObject.SetActive(true); _active = true;
            foreach (var ps in _damageSystems)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Play(false);
                if (!ps.isPlaying) throw new InvalidOperationException("Recursive Damage particle playback inactive: " + ps.name);
            }
            channel.Record("Play", state, "RecursivePS=" + _damageSystems.Length
                + "; FitScale=" + fit.UniformScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
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
