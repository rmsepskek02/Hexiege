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
        // [2026-10-26 D7 후속] 카메라 쪽 당김(월드 좌표). 재생 시작 때 1번 정하고 매 프레임 Follow에서 더하기만 한다.
        //   대상 유닛 회전을 곱하지 않는 "월드" 값이다 — 유닛은 돌아서지만 카메라는 돌지 않기 때문이다(아래 「카메라 쪽 당김」 절).
        private Vector3 _damageViewPull;
        public ulong DamageLease => _damageLease;
        public void RejectTimedDamageAcquire()
        {
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            // [2026-10-26 폭 맞춤] 재생 도중 실패로 거부돼도 바꾼 방출 범위·개수를 원래 값으로 되돌린 뒤 풀에 넣는다.
            RestoreWidthFit();
            // [2026-10-26 D7 후속] 거부된 재생의 카메라 쪽 당김도 비운다(다음 재생이 어차피 덮어쓰지만 이중 안전장치).
            _damageViewPull = Vector3.zero;
            _active = false; _damageLoop = false; _damageChannel = null; gameObject.SetActive(false); _onReturn?.Invoke(this);
        }
        /// <summary>
        /// 화상·독 지속 이펙트를 대상 유닛에 붙여 재생한다.
        /// fit(대상 크기 맞춤)은 재생 시작 때 1번만 적용되며, 같은 owner refresh는 재시작하지 않으므로 크기도 유지된다.
        /// fit을 생략하면 Identity(배율 1, 보정 0, 반경 0) — 예전과 완전히 같은 크기·위치다.
        /// 프리셋의 「폭 맞춤」이 켜져 있으면 fit의 몸통 반경으로 불꽃 방출 범위·개수도 재생 시작 때 1번 맞춘다(아래 「폭 맞춤」 절).
        /// 프리셋의 「카메라 쪽 당김」 비율이 0보다 크면 몸통 반경 × 비율만큼 카메라 쪽으로 당겨 몸 앞에 그린다(아래 「카메라 쪽 당김」 절).
        /// </summary>
        /// <param name="state">서버가 정한 지속 피해 표현 상태.</param>
        /// <param name="pose">대상 유닛의 VisualRoot Transform(발밑 기준).</param>
        /// <param name="preset">화상/독 프리셋.</param>
        /// <param name="channel">진단 기록 채널.</param>
        /// <param name="fit">대상 유닛 크기 맞춤(균등 배율 + 발밑 보정 + 몸통 반경).</param>
        public void PlayTimedDamage(TimedDamageEffectPresentation state, Transform pose, EffectPreset preset, TimedDamageEffectChannel channel,
            RecipientFit fit = default)
        {
            if (_active || pose == null || !state.Active || !state.IsValid) throw new InvalidOperationException("Damage lease unavailable");
            _damageSystems ??= GetComponentsInChildren<ParticleSystem>(true);
            // [2026-10-26 폭 맞춤] 이중 안전장치 — 지난 재생의 원복이 어떤 경로로든 빠졌더라도 여기서 원래 값으로 먼저 되돌린다.
            //   지난 재생에서 폭 맞춤을 적용하지 않았다면(옵션 꺼짐 등) 아무것도 하지 않는다.
            RestoreWidthFit();
            if (_damageSystems.Length == 0) throw new InvalidOperationException("Damage particles missing");
            foreach (var ps in _damageSystems)
                if (!ps.main.loop || ps.main.stopAction != ParticleSystemStopAction.None) throw new InvalidOperationException("Damage loop contract");
            ResetTravelState(); _damageLease++; if (_damageLease == 0) _damageLease++;
            _damageState = state; _damageChannel = channel; _damageLoop = true; _damageStopped = false;
            // [C] 위치 보정 = 프리셋 보정 + 크기 표의 발밑 보정. 미리 더해 두면 Follow는 예전 계산 그대로다.
            _damageOffset = preset.LocalPositionOffset + fit.SafeFootOffset; _damageEuler = preset.EulerRotationOffset;
            // [2026-10-26 D7 후속] 카메라 쪽 당김을 재생 시작 때 1번 정한다. 아래 FollowTimedDamage(첫 위치 잡기)보다 먼저여야
            //   첫 프레임부터 당긴 위치에 나온다. 매 재생마다 덮어쓰므로 풀 재사용 때 이전 대상의 당김이 남지 않는다.
            //   옵션이 꺼진 프리셋(독)은 0 벡터 + 기록 없음(null) → 예전과 같은 위치·같은 로그.
            string viewPull = ResolveViewPull(preset, fit, out _damageViewPull);
            // [R-4 비활성화 — 2026-10-13] 대상과 무관한 고정 배율. 작은 유닛엔 너무 크고 큰 유닛엔 어색했다.
            //   최종 삭제는 사용자 테스트 통과 후(WORKFLOW [4]).
            // transform.localScale = Vector3.Scale(_originalLocalScale, preset.ScaleMultiplier);
            // 새 크기 = 프리팹 원본 × 프리셋 배율(기존 조정 수단 그대로) × 대상 맞춤 균등 배율.
            //   균등(한 값)이라 Hierarchy 스케일링 파티클이 납작/길쭉하게 찌그러지지 않는다(D-3).
            //   Return()이 _originalLocalScale로 되돌리므로 다음 재사용에 이 배율이 남지 않는다.
            transform.localScale = Vector3.Scale(_originalLocalScale, preset.ScaleMultiplier) * fit.UniformScale;
            FollowTimedDamage(_damageLease, pose); gameObject.SetActive(true); _active = true;
            // [2026-10-26 폭 맞춤] 크기(localScale)를 먼저 정한 뒤에 계산해야 "불꽃 한 장의 실제 폭"을 바르게 잰다.
            //   Play 전에 적용해야 첫 불꽃부터 넓힌 범위·늘린 개수로 나온다. 옵션이 꺼져 있으면 null(아무것도 안 바꿈).
            string widthFit = ApplyWidthFit(_damageSystems, preset, fit);
            foreach (var ps in _damageSystems)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Play(false);
                if (!ps.isPlaying) throw new InvalidOperationException("Recursive Damage particle playback inactive: " + ps.name);
            }
            // 폭 맞춤 기록은 옵션이 켜진 프리셋에서만 덧붙는다 → 옵션이 꺼진 독의 로그 줄은 예전과 글자 하나 다르지 않다.
            channel.Record("Play", state, "RecursivePS=" + _damageSystems.Length
                + "; FitScale=" + fit.UniformScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                + (widthFit != null ? "; WidthFit=" + widthFit : string.Empty)
                + (viewPull != null ? "; ViewPull=" + viewPull : string.Empty));
        }
        /// <summary>
        /// 화상·독 이펙트를 대상 유닛의 현재 자세(pose)에 맞춰 옮긴다. TimedDamageEffectPresenter가 매 프레임(LateUpdate) 부른다.
        /// 위치 = 대상 발밑 + 대상 회전 × (프리셋 보정 + 발밑 보정) + 카메라 쪽 당김(회전을 곱하지 않는 월드 값).
        /// </summary>
        /// <param name="lease">재생 시작 때 받은 lease. 지금 lease와 다르면(옛 재생) 아무것도 하지 않는다.</param>
        /// <param name="pose">대상 유닛의 VisualRoot Transform.</param>
        public void FollowTimedDamage(ulong lease, Transform pose)
        {
            if (lease != _damageLease || pose == null) return;
            // [2026-10-26 D7 후속] 원래 식 그대로에 _damageViewPull만 더했다. 당김이 0이면(독·옵션 꺼짐) 예전과 완전히 같은 위치다.
            transform.SetPositionAndRotation(pose.position + pose.rotation * _damageOffset + _damageViewPull,
                pose.rotation * Quaternion.Euler(_damageEuler));
        }
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
            // [2026-10-26 폭 맞춤] 강제 정리도 바꾼 방출 범위·개수를 원래 값으로 되돌린다(입자는 바로 위에서 이미 비웠다).
            RestoreWidthFit();
            _damageViewPull = Vector3.zero; // [2026-10-26 D7 후속] 강제 정리도 카메라 쪽 당김을 비운다.
            _damageLoop = false; _damageStopped = false; _damageChannel = null; _active = false;
            // Forced cleanup is not a natural return and does not enqueue a destroyed/disabled lease.
        }

        // ====================================================================
        // [2026-10-26 상태 이펙트 폭 맞춤] (Plan: _Tasks/2026-10-26/18_34_status-effect-width-fit)
        //
        // 무엇을 하나:
        //   몸이 옆으로 넓은 유닛(곰전사·탱크·대포수레 등)에 붙은 화상 불꽃이 몸 한가운데 한 장뿐이라 잘 안 보였다.
        //   불꽃 "한 장의 크기"는 지금처럼 키 기반 균등 배율로 두고(D-3), 재생 시작 때 1번만
        //     ① 불꽃이 나오는 자리를 한 점 → 몸통 폭 원판(수평 Circle)으로 넓히고
        //     ② 불꽃 개수(초당 방출 수 · 최대 입자 수)를 N배로 늘린다.
        //
        // 계산 (사용자 결정 D2·D3·D4, 2026-10-26):
        //   불꽃 폭 w   = 그 파티클 시스템의 시작 크기(가로) × 실제로 걸린 월드 배율
        //   몸통 폭 W   = 크기 표 radius × 2
        //   개수 N      = 반올림(W ÷ w), 1 이상 · 프리셋 상한(기본 3) 이하
        //   방출 반경 R = radius − w ÷ 2 (0 미만이면 0) → 불꽃 바깥 끝이 몸 윤곽을 넘지 않는다
        //   🔴 N이 1이면 아무것도 바꾸지 않는다 → 몸이 좁은 유닛(래빗·스트림 등)은 예전과 완전히 같다.
        //
        // 어떤 파티클 시스템에 적용하나:
        //   방출(Emission)이 켜진 시스템만. 화상 프리팹은 그 시스템이 Flame_Haze 하나다(루트는 방출 꺼짐).
        //   ⚠️ 독·힐 프리팹은 오라 레이어(Poison_Aura·Soft_Aura)도 방출이 켜져 있어, 나중에 그 프리셋에서 켤 때는
        //     "방출 켜진 시스템 전부"가 맞는지 다시 정해야 한다(Research 6-1). 이번에는 화상만 켰다.
        //
        // 풀 재사용 안전:
        //   풀은 프리팹 단위라 같은 아이템이 다음에 다른 유닛에 붙는다. 그래서
        //   - 원래 값은 처음 적용할 때 1번 저장하고(프리팹 그대로인 상태),
        //   - 자연 반환(Return) · 재생 실패 거부(Reject…) · 강제 정리(OnDisable) 세 경로 모두 되돌리고,
        //   - 다음 재생 시작 때도 먼저 되돌린다(이중 안전장치).
        //   옵션이 꺼진 재생은 저장도 적용도 하지 않는다 → 프리팹 값을 한 번도 건드리지 않는다.
        //
        // 규칙 34(상태 이펙트 loop 계약)와의 관계:
        //   Looping·Stop Action·Simulation Space·Burst는 건드리지 않는다. 바꾸는 것은 Shape(범위)·rate·maxParticles뿐이다.
        //   같은 대상 갱신(refresh)은 이 계산을 다시 하지 않는다(재생 시작 때 1번).
        //
        // 화면 표현 전용 — 피해·DoT 틱·서버 판정·RPC와 무관하다. Host와 Client는 같은 표·프리셋으로 같은 N·R을 계산한다
        //   (불꽃이 나오는 정확한 자리는 파티클 무작위라 화면마다 조금 다를 수 있으나 표현 전용이라 문제없다).
        // ====================================================================

        /// <summary>
        /// 폭 맞춤 원판의 radiusThickness. 1 = 원판 전체에서 고르게 방출(0.5면 바깥쪽 절반에서만 나와 몸 가운데가 빈다).
        /// Plan 3-3의 제안값이며, 육안 확인 후 바뀔 수 있다.
        /// </summary>
        public const float WidthFitRadiusThickness = 1f;

        /// <summary>폭 맞춤 원판을 수평(바닥과 나란한 XZ 평면)으로 눕히는 Shape 회전. Circle은 기본이 세로(XY) 평면이다.</summary>
        private static readonly Vector3 WidthFitShapeRotation = new Vector3(-90f, 0f, 0f);

        /// <summary>폭 맞춤 전에 저장해 둔 파티클 시스템 하나의 원래 모듈 값(프리팹 그대로의 값).</summary>
        private struct WidthFitOriginal
        {
            public bool ShapeEnabled;
            public ParticleSystemShapeType ShapeType;
            public float ShapeRadius;
            public float ShapeRadiusThickness;
            public Vector3 ShapeRotation;
            public float RateOverTimeMultiplier;
            public int MaxParticles;
        }

        /// <summary>원래 값을 저장한 시스템 목록(_damageSystems 또는 _bloomSystems 배열 자체). null = 아직 저장 안 함.</summary>
        private ParticleSystem[] _widthFitSystems;
        private WidthFitOriginal[] _widthFitOriginals;
        /// <summary>지금 모듈 값이 원래 값과 다를 수 있는지(= 폭 맞춤을 적용했고 아직 되돌리지 않았는지).</summary>
        private bool _widthFitApplied;

        /// <summary>
        /// 폭 맞춤 공식(D3 반올림 · D4 윤곽 안 · D2 상한)을 계산한다. 순수 계산이라 파티클을 건드리지 않는다.
        /// </summary>
        /// <param name="bodyRadius">몸통 반경(월드 미터). 크기 표의 radius.</param>
        /// <param name="flameWidth">불꽃 한 장의 실제 폭(월드 미터).</param>
        /// <param name="maxCount">개수 상한(1 미만이면 1로 본다).</param>
        /// <param name="count">불꽃 개수 N (1 이상 상한 이하). 1이면 "바꾸지 않음".</param>
        /// <param name="emitRadius">불꽃이 나올 원판의 실제 반경 R(월드 미터, 0 이상).</param>
        /// <returns>입력이 쓸 수 있는 값이면 true. 반경·폭이 0 이하·NaN·무한대면 false(폭 맞춤 안 함, count 1).</returns>
        public static bool TryComputeWidthFit(float bodyRadius, float flameWidth, int maxCount, out int count, out float emitRadius)
        {
            count = 1; emitRadius = 0f;
            if (!(bodyRadius > 0f) || float.IsInfinity(bodyRadius) || !(flameWidth > 0f) || float.IsInfinity(flameWidth)) return false;
            // 몸통 폭 ÷ 불꽃 폭. 불꽃 폭이 극단적으로 작으면 비율이 int 범위를 넘을 수 있어 넉넉한 값(1000)에서 자른다(어차피 상한으로 묶인다).
            float ratio = Mathf.Min(bodyRadius * 2f / flameWidth, 1000f);
            // Mathf.RoundToInt는 x.5를 짝수 쪽으로 반올림(2.5 → 2)하므로, 사람이 아는 반올림(2.5 → 3)은 Floor(x + 0.5)로 한다.
            int rounded = Mathf.FloorToInt(ratio + 0.5f);
            count = Mathf.Clamp(rounded, 1, Mathf.Max(1, maxCount));
            emitRadius = Mathf.Max(0f, bodyRadius - flameWidth * 0.5f);
            return true;
        }

        /// <summary>
        /// 프리셋 옵션이 켜져 있으면 몸통 반경으로 방출 범위·개수를 맞춘다. 반드시 크기(localScale)를 정한 뒤, Play 전에 부른다.
        /// </summary>
        /// <param name="systems">이 아이템의 파티클 시스템 전부(_damageSystems / _bloomSystems).</param>
        /// <param name="preset">재생 중인 상태 이펙트 프리셋.</param>
        /// <param name="fit">대상 유닛 크기 맞춤(몸통 반경 포함).</param>
        /// <returns>옵션이 꺼져 있으면 null(아무것도 안 바꿈). 켜져 있으면 로그용 요약(예: "Flame_Haze:3@0.270").</returns>
        private string ApplyWidthFit(ParticleSystem[] systems, EffectPreset preset, RecipientFit fit)
        {
            if (preset == null || !preset.WidthFitEnabled || systems == null) return null;
            float bodyRadius = fit.SafeRadius;
            if (bodyRadius <= 0f) return "NoRadius"; // 크기 표에 반경이 없거나 대상 타입을 모름 → 예전과 같은 한 점 방출
            CaptureWidthFitOriginals(systems);
            string summary = null;
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                // 방출이 꺼진 시스템(화상 루트 등)은 그려지는 것이 없다 → 대상 아님
                if (ps == null || !ps.emission.enabled) continue;
                string entry = ApplyWidthFitToSystem(ps, _widthFitOriginals[i], bodyRadius, preset.WidthFitMaxCount);
                summary = summary == null ? entry : summary + "|" + entry;
            }
            return summary ?? "NoEmitter";
        }

        /// <summary>시스템 하나에 폭 맞춤을 적용하고 로그용 한 토막을 돌려준다.</summary>
        private string ApplyWidthFitToSystem(ParticleSystem ps, WidthFitOriginal original, float bodyRadius, int maxCount)
        {
            var main = ps.main;
            // Hierarchy가 아니면 "부모 배율이 불꽃 크기와 원판 반경에 똑같이 곱해진다"는 아래 환산 전제가 깨진다 → 건드리지 않는다.
            //   (현재 화상·독·힐 프리팹은 전 레이어가 Hierarchy다.)
            if (main.scalingMode != ParticleSystemScalingMode.Hierarchy) return ps.name + ":skip(ScalingMode)";
            // Hierarchy 스케일링에서는 시작 크기·Shape 반경 모두 "이 시스템 Transform의 월드 배율(lossyScale)"이 곱해진다.
            //   가로(X) 축 기준으로 환산한다. 현재 프리셋 배율·크기 맞춤은 모두 균등이라 X·Z가 같다.
            float worldScaleX = Mathf.Abs(ps.transform.lossyScale.x);
            float flameWidth = ReadMaxSize(main.startSize3D ? main.startSizeX : main.startSize) * worldScaleX;
            if (!TryComputeWidthFit(bodyRadius, flameWidth, maxCount, out int count, out float emitRadius))
                return ps.name + ":skip(FlameWidth)";
            if (count <= 1) return ps.name + ":1"; // 🔴 1장 = 아무것도 안 바꿈(예전과 같음)

            var shape = ps.shape;
            // Shape 반경에는 Shape 자체의 Scale(X)도 곱해진다. 0이면 나눌 수 없으니 건드리지 않는다.
            float shapeScaleX = Mathf.Abs(shape.scale.x);
            if (worldScaleX <= 0f || shapeScaleX <= 0f) return ps.name + ":skip(ZeroScale)";
            _widthFitApplied = true; // 이 줄 아래부터 모듈 값이 바뀐다 → 이후 어떤 경로로 끝나도 RestoreWidthFit이 되돌린다.
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.rotation = WidthFitShapeRotation;
            shape.radiusThickness = WidthFitRadiusThickness;
            // 원하는 "월드" 반경 R을 넣으려면 그 오브젝트에 걸린 배율로 나눠야 한다(Hierarchy라 반경도 배율에 곱해지기 때문).
            shape.radius = emitRadius / (worldScaleX * shapeScaleX);
            // 개수: 초당 방출 수와 최대 입자 수를 함께 N배. Burst(한 번에 터뜨리기)는 건드리지 않는다(규칙 34 burst 계약).
            var emission = ps.emission;
            emission.rateOverTimeMultiplier = original.RateOverTimeMultiplier * count;
            main.maxParticles = original.MaxParticles * count;
            return ps.name + ":" + count + "@" + emitRadius.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>시작 크기 값의 최댓값(곡선이면 곡선 배수). 불꽃이 가장 넓을 때의 폭을 기준으로 해야 몸 윤곽을 넘지 않는다.</summary>
        private static float ReadMaxSize(ParticleSystem.MinMaxCurve size)
        {
            switch (size.mode)
            {
                case ParticleSystemCurveMode.Constant: return size.constant;
                case ParticleSystemCurveMode.TwoConstants: return Mathf.Max(size.constantMin, size.constantMax);
                default: return size.curveMultiplier; // 곡선 값은 0~1에 배수를 곱하는 방식이라 배수가 상한이다
            }
        }

        /// <summary>
        /// 원래 모듈 값을 저장한다. 같은 시스템 목록이면 처음 1번만 저장한다 — 저장은 늘 적용 "전"에 일어나므로
        /// 저장값은 언제나 프리팹 그대로의 값이다.
        /// </summary>
        private void CaptureWidthFitOriginals(ParticleSystem[] systems)
        {
            if (_widthFitSystems == systems && _widthFitOriginals != null) return;
            RestoreWidthFit(); // 다른 목록으로 바뀌는 비정상 경우에도 옛 목록부터 되돌린다
            _widthFitSystems = systems;
            _widthFitOriginals = new WidthFitOriginal[systems.Length];
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null) continue;
                var shape = ps.shape;
                _widthFitOriginals[i] = new WidthFitOriginal
                {
                    ShapeEnabled = shape.enabled, ShapeType = shape.shapeType, ShapeRadius = shape.radius,
                    ShapeRadiusThickness = shape.radiusThickness, ShapeRotation = shape.rotation,
                    RateOverTimeMultiplier = ps.emission.rateOverTimeMultiplier, MaxParticles = ps.main.maxParticles,
                };
            }
        }

        /// <summary>
        /// 폭 맞춤으로 바꾼 모듈 값을 원래 값으로 되돌린다. 적용한 적이 없으면 아무것도 하지 않는다
        /// (옵션이 꺼진 독·힐은 이 메서드가 모듈 값을 한 번도 쓰지 않는다).
        /// </summary>
        private void RestoreWidthFit()
        {
            if (!_widthFitApplied || _widthFitSystems == null || _widthFitOriginals == null) return;
            for (int i = 0; i < _widthFitSystems.Length; i++)
            {
                var ps = _widthFitSystems[i];
                if (ps == null) continue; // 파괴된 시스템은 되돌릴 대상이 없다
                var original = _widthFitOriginals[i];
                var shape = ps.shape;
                shape.shapeType = original.ShapeType;
                shape.radius = original.ShapeRadius;
                shape.radiusThickness = original.ShapeRadiusThickness;
                shape.rotation = original.ShapeRotation;
                shape.enabled = original.ShapeEnabled;
                var emission = ps.emission;
                emission.rateOverTimeMultiplier = original.RateOverTimeMultiplier;
                var main = ps.main;
                main.maxParticles = original.MaxParticles;
            }
            _widthFitApplied = false;
        }

        // ====================================================================
        // [2026-10-26 D7 후속 — 상태 이펙트를 몸 앞에 그리기] (Plan: _Tasks/2026-10-26/18_34_status-effect-width-fit 8절)
        //
        // 왜 필요한가:
        //   상태 이펙트(화상 불꽃 등)는 유닛 몸 한가운데에 서 있는 얇은 판(Billboard)처럼 그려진다.
        //   유닛 몸은 불투명 재질이라 먼저 그려지면서 "몸 표면까지의 깊이"를 남기고, 불꽃은 투명 재질이라
        //   나중에 그 깊이와 비교해 "몸 표면보다 카메라에서 먼 픽셀"은 그리지 않는다.
        //   그래서 카메라 쪽에 있는 몸 앞부분이 불꽃의 아래·가운데 대부분을 덮었다(Research 10-1·10-5).
        //
        // 무엇을 하나:
        //   재생 시작 때 1번, "카메라 쪽 방향 × (몸통 반경 × 프리셋 비율)"인 월드 벡터를 구해 두고,
        //   매 프레임 따라붙을 때(Follow) 위치에 더한다. 비율은 프리셋 값(화상 1.75, 독·힐 0 = 꺼짐).
        //
        // 🔴 왜 화면 모습이 안 바뀌나 (전제: 직교 카메라):
        //   직교(Orthographic) 카메라는 원근이 없어서, 화면 좌표가 "카메라 오른쪽 축·위쪽 축 성분"만으로 정해진다.
        //   카메라 쪽 방향(= 카메라가 바라보는 방향의 반대)은 그 두 축과 직각이라, 그 방향으로 옮겨도
        //   화면 위치·크기·모양이 전혀 바뀌지 않고 깊이(무엇의 앞에 그려지는가)만 바뀐다.
        //   ⚠️ 카메라를 원근(Perspective)으로 바꾸면 이 전제가 깨진다 — 당긴 만큼 화면 위치·크기가 조금 달라진다.
        //      그런 경우가 생기면 로그 값 끝에 "@Perspective"가 붙는다(동작은 같게 두고 알아볼 수 있게만 했다).
        //   기본 비율 1.75는 현재 카메라 기울기 55°에서 나온 값(1 ÷ cos 55° ≈ 1.74)이다. 기울기를 바꾸면 다시 정한다.
        //
        // 왜 유닛 회전을 곱하지 않나:
        //   기존 위치 보정(프리셋 보정 + 발밑 보정)은 유닛을 따라 도는 "유닛 기준(로컬)" 값이라 pose 회전을 곱한다.
        //   그러나 카메라는 유닛이 돌아도 돌지 않는다. 당김에 유닛 회전을 곱하면 유닛이 돌아설 때 불꽃이
        //   카메라 반대쪽(몸 뒤)으로 가 버린다. 그래서 당김은 회전 없이 월드 값 그대로 더한다.
        //
        // 카메라는 어떻게 얻나 (game-programmer 결정, 근거):
        //   - 재생 시작 때 Camera.main을 1번 읽는다. 기존 Presentation 코드(FloatingHpText)도 같은 방식이다.
        //   - 매 프레임 다시 읽지 않는 이유: 카메라 회전을 바꾸는 코드는 CameraController.ApplyTilt 한 곳뿐이고(X축 55°),
        //     양 팀이 같은 회전을 쓴다(Red 관점은 카메라를 돌리지 않고 좌표를 뒤집는다 — ViewConverter).
        //     카메라 이동(팬)·확대(orthographicSize)는 "바라보는 방향"을 바꾸지 않으므로 당김 방향도 그대로다.
        //   - EffectManager가 카메라를 넘겨주는 방식은 쓰지 않았다: 재생 함수 시그니처(기존 호출·검증 fixture 다수)를
        //     바꿔야 하는데, 얻는 이득(카메라 한 번 조회 절약)이 없다.
        //   - 카메라를 못 구하면(에디터 자체 검증처럼 씬에 카메라가 없을 때) 당김 0 + 로그 "NoCamera". 예외를 내지 않는다.
        //     → 당김이 없으면 "예전과 같은 위치"일 뿐이라 화면이 깨지지 않는 쪽이 안전하다.
        //   - 자체 검증은 ViewPullCameraOverride로 시험용 카메라(또는 "카메라 없음")를 넣는다. 런타임 코드는 이 값을 쓰지 않는다.
        //
        // 풀 재사용 안전:
        //   당김은 재생 시작 때마다 새로 덮어쓰고, 자연 반환(Return)·재생 실패 거부(Reject…)·강제 정리(OnDisable)에서도
        //   0으로 되돌린다(이중 안전장치). 폭 맞춤(파티클 모듈 값)과 달리 오브젝트 위치만 바꾸므로 원본 저장이 필요 없다.
        //
        // 규칙 34(상태 이펙트 loop 계약)와의 관계:
        //   오브젝트 위치만 바꾼다. Looping·Stop Action·Simulation Space·Burst·rate는 건드리지 않는다.
        //   같은 대상 갱신(refresh)은 다시 계산하지 않는다(재생 시작 때 1번).
        //
        // 화면 표현 전용 — 피해·DoT 틱·서버 판정·RPC·Simulation Root와 무관하다(NET-ROOT-002).
        //   Host와 Client는 같은 크기 표·프리셋·같은 카메라 회전으로 같은 당김을 계산한다.
        // ====================================================================

        /// <summary>
        /// [자체 검증 전용] 카메라 쪽 당김이 쓸 카메라를 바꿔 끼우는 통로. null(기본)이면 Camera.main을 쓴다.
        /// 값이 있으면 그 함수의 결과를 쓴다 — 함수가 null을 돌려주면 "카메라 없음"(당김 0)으로 처리한다.
        /// 🔴 런타임 게임 코드에서는 설정하지 않는다. 검증은 try/finally로 반드시 null로 되돌린다.
        /// </summary>
        public static Func<Camera> ViewPullCameraOverride { get; set; }

        /// <summary>
        /// 카메라 쪽 당김 벡터를 계산한다. 순수 계산이라 오브젝트를 건드리지 않는다.
        /// 당김 = (카메라가 바라보는 방향의 반대, 길이 1) × (비율 × 몸통 반경).
        /// </summary>
        /// <param name="ratio">프리셋의 당김 비율(EffectPreset.ViewPullRadiusRatio).</param>
        /// <param name="bodyRadius">몸통 반경(월드 미터). 크기 표의 radius.</param>
        /// <param name="cameraForward">카메라가 바라보는 방향(Camera.transform.forward). 길이는 상관없다.</param>
        /// <param name="pull">당김 벡터(월드 좌표). 계산할 수 없으면 0 벡터.</param>
        /// <returns>비율·반경이 0 이하·NaN·무한대이거나 카메라 방향이 0·NaN이면 false(당김 없음).</returns>
        public static bool TryComputeViewPull(float ratio, float bodyRadius, Vector3 cameraForward, out Vector3 pull)
        {
            pull = Vector3.zero;
            if (!IsPositiveFiniteForPull(ratio) || !IsPositiveFiniteForPull(bodyRadius)) return false;
            if (!IsFiniteForPull(cameraForward.x) || !IsFiniteForPull(cameraForward.y) || !IsFiniteForPull(cameraForward.z)) return false;
            // 길이가 거의 0이면 방향을 정할 수 없다(normalized가 0 벡터가 된다).
            if (cameraForward.sqrMagnitude < 1e-8f) return false;
            pull = -cameraForward.normalized * (ratio * bodyRadius);
            return true;
        }

        /// <summary>
        /// 프리셋·크기 맞춤·카메라로 이번 재생의 카메라 쪽 당김을 정한다. 재생 시작 때 1번만 부른다.
        /// </summary>
        /// <param name="preset">재생 중인 상태 이펙트 프리셋.</param>
        /// <param name="fit">대상 유닛 크기 맞춤(몸통 반경 포함).</param>
        /// <param name="pull">당김 벡터(월드 좌표). 당기지 않으면 0 벡터.</param>
        /// <returns>
        /// 옵션이 꺼져 있으면 null(로그에 아무것도 덧붙이지 않음 → 독·힐 로그는 예전과 글자 하나 다르지 않다).
        /// 켜져 있으면 로그용 값: 당긴 거리(예: "0.385"), 반경을 모르면 "NoRadius", 카메라를 못 구하면 "NoCamera".
        /// </returns>
        private static string ResolveViewPull(EffectPreset preset, RecipientFit fit, out Vector3 pull)
        {
            pull = Vector3.zero;
            float ratio = preset != null ? preset.ViewPullRadiusRatio : 0f;
            if (ratio <= 0f) return null;
            float bodyRadius = fit.SafeRadius;
            // 크기 표에 반경이 없거나 대상 타입을 모름(Identity) → 몸 두께를 모르니 당기지 않는다(예전과 같은 위치).
            if (bodyRadius <= 0f) return "NoRadius";
            Camera cam = ViewPullCameraOverride != null ? ViewPullCameraOverride() : Camera.main;
            if (cam == null) return "NoCamera"; // Unity null 검사 — 파괴된 카메라도 여기서 걸린다
            if (!TryComputeViewPull(ratio, bodyRadius, cam.transform.forward, out pull)) return "NoCamera";
            string distance = (ratio * bodyRadius).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            return cam.orthographic ? distance : distance + "@Perspective";
        }

        private static bool IsFiniteForPull(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static bool IsPositiveFiniteForPull(float v) => v > 0f && !float.IsInfinity(v);
    }
}
