// ============================================================================
// VfxPoolItem.cs
// 파티클(VFX) 하나의 재생 수명을 관리하고, 재생이 끝나면 스스로 Pool에 반환하는
// 컴포넌트.
//
// 왜 필요한가:
//   매번 Instantiate/Destroy를 하면 GC(가비지 컬렉션) 부하가 발생한다.
//   미리 만들어 둔 파티클 오브젝트를 EffectManager의 Pool에 보관하고,
//   필요할 때 꺼내 재생하고, 재생이 끝나면 다시 Pool에 반환하여 재사용한다.
//   FloatingHpTextSpawner와 동일한 Object Pool 패턴이다.
//
// 자동 반환 원리:
//   ParticleSystem.IsAlive(true) → 파티클이 아직 살아있는 동안 true,
//   모든 파티클이 소멸하면 false를 반환한다.
//   Update()에서 매 프레임 검사하여 false가 되는 순간 Return()을 호출한다.
//
// 부착 위치:
//   EffectManager가 VFX 프리팹을 Instantiate할 때 이 컴포넌트가 없으면
//   자동으로 AddComponent한다. 따라서 프리팹에 미리 붙여둘 필요는 없다.
//   (다만 ParticleSystem 컴포넌트는 프리팹 루트에 있어야 한다.)
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, ParticleSystem).
// ============================================================================

using System;
using UnityEngine;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 파티클 재생이 끝나면 스스로 EffectManager Pool에 반환하는 컴포넌트.
    /// EffectManager의 VFX Pool 항목 단위.
    /// </summary>
    public partial class VfxPoolItem : MonoBehaviour
    {
        /// <summary> 루트에 있는 파티클 시스템. 있으면 이것만 Play(하위 포함)로 재생. Awake에서 캐시. </summary>
        private ParticleSystem _ps;

        /// <summary>
        /// 루트에 파티클 시스템이 없고 "여러 형제 시스템"으로 구성된 프리팹을 위한 직속 자식 시스템 목록.
        /// 예: TorrentSpirit 파도(vfx_torrentspirit_attack) — 빈 루트 아래
        ///     Main_Water_Surge / Ground_Impact_Ripples / Front_Foam_Crest 3개가 형제로 붙어 있다.
        /// ParticleSystem.Play()는 "형제" 시스템을 재생하지 않으므로, 이 경우 각 형제를 개별 재생해야
        /// 한다. (안 그러면 첫 번째 하나만 보이고 나머지는 안 나온다.)
        /// </summary>
        private ParticleSystem[] _childSystems;

        /// <summary>
        /// 이 인스턴스가 어떤 원본 프리팹에서 만들어졌는지.
        /// EffectManager의 Pool Dictionary 키로 사용된다.
        /// (같은 프리팹끼리만 같은 Queue에 반환해야 하므로 필요.)
        /// </summary>
        private GameObject _sourcePrefab;

        /// <summary> Pool 반환 콜백. EffectManager.OnVfxReturn으로 연결. </summary>
        private Action<VfxPoolItem> _onReturn;

        /// <summary>
        /// 현재 재생 중인지 여부.
        /// 중복 반환(Return이 두 번 호출되는 것)을 막기 위한 플래그.
        /// </summary>
        private bool _active;

        /// <summary>프리팹이 가진 원본 로컬 스케일. 풀 재사용 때 이전 프리셋 배율이 누적되지 않도록 기준으로 사용한다.</summary>
        private Vector3 _originalLocalScale;
        private bool _systemsInitialized;

        /// <summary>현재 재생의 전진 이동 시작 위치와 방향.</summary>
        private Vector3 _travelStartPosition;
        private Vector3 _travelDirection;
        private float _travelDistance;
        private float _travelDuration;
        private float _travelElapsed;
        private bool _isTravelling;
        private Func<double> _waveClock;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private Hexiege.Application.WavePresentationDiagnostics _waveDiagnostics;
#endif
        private Hexiege.Application.WavePresentationSpawn _wave;
        private Vector3 _waveViewOrigin, _waveViewForward, _waveOffset;
        private Quaternion _waveRotation;
        private float _waveAge;
        private ParticleSystem[] _waveSystems;
        private ParticleSystemScalingMode[] _waveOriginalScaling;
        private bool[] _waveOriginalAutoSeed;
        private uint[] _waveOriginalSeed;
        private bool _waveDriven;
        public const float WaveSimulationStep = 1f / 120f;

        private ParticleSystem[] _bloomSystems;
        private bool _bloomLoop, _bloomStopped;
        private bool _bloomAcquirePending;
        private ulong _bloomLease;
        private Hexiege.Application.BloomHealEffectPresentation _bloomState;
        private Vector3 _bloomOffset, _bloomEuler, _bloomScale;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private Hexiege.Application.BloomHealEffectDiagnostics _bloomDiagnostics;
#endif
        public ulong BloomLease => _bloomLease;
        public bool BloomLoopActive => _active && _bloomLoop && !_bloomStopped;
        public int BloomRecursiveSystemCount => _bloomSystems?.Length ?? 0;
        public Hexiege.Application.BloomHealEffectPresentation BloomSnapshot => _bloomState;

        /// <summary>풀에서 빌렸지만 아직 Play되지 않은 상태를 소유한다. 실패 회수는 자연 drain과 다르다.</summary>
        public bool BeginBloomAcquire()
        {
            if (_active || _bloomAcquirePending) return false;
            _bloomAcquirePending = true;
            _bloomSystems ??= GetComponentsInChildren<ParticleSystem>(true);
            // Instantiate의 PlayOnAwake도 즉시 닫는다. 실패한 loop가 원점에 남으면 안 된다.
            foreach (var ps in _bloomSystems) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(false);
            return true;
        }
        public void RejectBloomAcquire()
        {
            if (!_bloomAcquirePending) return;
            _bloomAcquirePending = false;
            _active = false; _bloomLoop = false; _bloomStopped = false;
            foreach (var ps in _bloomSystems) if (ps != null) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(false);
            _onReturn?.Invoke(this); // pending을 먼저 해제하여 중복 실패 callback도 1회만 반환한다.
        }

        /// <summary>새 lease에서만 5PS를 시작한다. refresh는 이 메서드를 호출하지 않는다.</summary>
        public ulong PlayBloomLoop(Hexiege.Application.BloomHealEffectPresentation state, Transform pose, EffectPreset preset
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            , Hexiege.Application.BloomHealEffectDiagnostics diagnostics
#endif
            )
        {
            if (pose == null || !state.IsValid || !state.Active || _active) return 0;
            _bloomSystems ??= GetComponentsInChildren<ParticleSystem>(true);
            if (_bloomSystems.Length != 5) return 0;
            foreach (var ps in _bloomSystems)
                if (!ps.main.loop || ps.main.useUnscaledTime || ps.main.stopAction != ParticleSystemStopAction.None
                    || ps.main.simulationSpace != ParticleSystemSimulationSpace.Local || ps.emission.burstCount != 0) return 0;
            ResetTravelState();
            _bloomLease++; if (_bloomLease == 0) _bloomLease++;
            _bloomState = state; _bloomLoop = true; _bloomStopped = false; _active = true;
            _bloomOffset = preset.LocalPositionOffset; _bloomEuler = preset.EulerRotationOffset;
            _bloomScale = preset.ScaleMultiplier;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _bloomDiagnostics = diagnostics;
#endif
            FollowBloomPose(_bloomLease, pose);
            gameObject.SetActive(true);
            foreach (var ps in _bloomSystems)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                // authored 5PS burst=0 계약을 위에서 검증했다. emission 설정을 변형하지 않는다.
                ps.Play(false);
            }
            ObserveBloom("Play", "RecursivePS=5");
            _bloomAcquirePending = false;
            return _bloomLease;
        }
        public bool RefreshBloom(ulong lease, Hexiege.Application.BloomHealEffectPresentation state)
        {
            if (!BloomLoopActive || lease != _bloomLease || !_bloomState.SameOwner(state)
                || !state.Active || state.Revision <= _bloomState.Revision) return false;
            _bloomState = state; // No Clear/Play/Stop: native loop continuity is retained.
            return true;
        }
        public void FollowBloomPose(ulong lease, Transform pose)
        {
            if (!BloomLoopActive || lease != _bloomLease || pose == null) return;
            transform.SetPositionAndRotation(pose.position + pose.rotation * _bloomOffset,
                pose.rotation * Quaternion.Euler(_bloomEuler));
            var world = Vector3.Scale(Vector3.Scale(_originalLocalScale, _bloomScale), pose.lossyScale);
            var parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(world.x / parentScale.x, world.y / parentScale.y, world.z / parentScale.z);
        }
        public bool StopBloom(ulong lease, string reason)
        {
            if (!BloomLoopActive || lease != _bloomLease) return false;
            _bloomStopped = true;
            // 마지막 유효 world pose를 유지한다. 더 이상 target을 조회/추종하지 않는다.
            foreach (var ps in _bloomSystems) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            ObserveBloom("StopEmitting", "RecursivePS=5, Reason=" + reason);
            return true;
        }
        public bool StopBloom(ulong lease, Hexiege.Application.BloomHealEffectPresentation terminal)
        {
            if (!BloomLoopActive || lease != _bloomLease || !_bloomState.SameOwner(terminal)
                || terminal.Active || terminal.Revision <= _bloomState.Revision) return false;
            // 실제 서버 Stop의 revision/Active=false를 먼저 보존한다. 이후 Drain/Return도
            // 동일 terminal snapshot을 기록하며, local attachment close는 이 값을 꾸미지 않는다.
            _bloomState = terminal;
            return StopBloom(lease, "ServerStop");
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveBloom(string stage, string detail)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _bloomDiagnostics?.Record(stage, _bloomState, _bloomLease, detail);
#endif
        }

        /// <summary> EffectManager가 Pool 반환 시 키로 사용하는 원본 프리팹. </summary>
        public GameObject SourcePrefab => _sourcePrefab;

        /// <summary>
        /// 이 인스턴스가 실제로 제어하는 ParticleSystem 개수.
        /// 공격 VFX 진단이 프리팸과 풀 획득은 성공했지만 재생할 파티클이 없는 경우를
        /// 정상 재생과 구분할 수 있게 한다. 상태를 변경하지 않는 읽기 전용 값이다.
        /// </summary>
        public int ParticleSystemCount => _ps != null ? 1 : _childSystems?.Length ?? 0;

        /// <summary>
        /// Play 호출 직후 하나 이상의 파티클이 살아 있는지 확인하는 읽기 전용 진단 값.
        /// 풀 반환이나 재생 시점을 바꾸지 않는다.
        /// </summary>
        public bool IsPlaybackActive => _active && AnyAlive();

        /// <summary>
        /// 파티클 시스템 캐시.
        /// 루트에 없으면 자식에서 탐색 (프리팹 구조 유연성 확보).
        /// </summary>
        private void Awake()
        { InitializeSystems(); }

        // Awake는 Play Mode의 Unity lifecycle에서 호출된다. Edit Mode에서 만든 native
        // fixture와 비활성 checkout도 동일 초기화를 Setup으로 보장한다. 단 한 번만
        // 원본 scale을 잡으므로 재사용 중인 변형 scale을 원본으로 덮어쓰지 않는다.
        private void InitializeSystems()
        {
            if (_systemsInitialized) return;
            _originalLocalScale = transform.localScale;
            // 1순위: 루트에 파티클 시스템이 있으면 그것으로 충분(Play(true)가 하위 시스템까지 재생).
            _ps = GetComponent<ParticleSystem>();

            // 루트에 없으면: 빈 루트 아래 여러 형제 파티클 시스템으로 구성된 프리팹이다.
            //   GetComponentInChildren는 "첫 번째" 하나만 반환하고 Play()도 형제는 재생하지 않으므로,
            //   직속 자식 파티클 시스템을 전부 모아 각각 재생해야 한다(안 그러면 일부만 보인다).
            if (_ps == null)
            {
                var list = new System.Collections.Generic.List<ParticleSystem>();
                foreach (Transform child in transform)
                {
                    var childPs = child.GetComponent<ParticleSystem>();
                    if (childPs != null) list.Add(childPs);
                }

                // 직속 자식에도 없으면(더 깊이 중첩된 구조) 마지막 폴백으로 하위 전체에서 하나 탐색.
                if (list.Count == 0)
                {
                    var nested = GetComponentInChildren<ParticleSystem>();
                    if (nested != null) list.Add(nested);
                }

                _childSystems = list.ToArray();
            }
            _systemsInitialized = true;
        }

        /// <summary>
        /// 생성 직후 1회 초기화. EffectManager가 인스턴스를 만들 때 호출.
        /// 원본 프리팹과 반환 콜백을 저장해 둔다.
        /// </summary>
        /// <param name="prefab">이 인스턴스의 원본 프리팹 (Pool 키).</param>
        /// <param name="onReturn">재생 완료 시 호출할 Pool 반환 콜백.</param>
        public void Setup(GameObject prefab, Action<VfxPoolItem> onReturn)
        {
            InitializeSystems();
            _sourcePrefab = prefab;
            _onReturn = onReturn;
        }

        /// <summary>
        /// 지정한 위치/회전에서 파티클을 재생한다.
        /// EffectManager가 Pool에서 꺼낸 직후 호출.
        /// </summary>
        /// <param name="pos">재생할 월드 좌표.</param>
        /// <param name="rot">재생할 회전.</param>
        /// <param name="localPositionOffset">rot을 기준으로 월드 변환할 로컬 위치 보정값.</param>
        /// <param name="eulerRotationOffset">rot 뒤에 추가로 적용할 Euler 회전 보정값(도).</param>
        /// <param name="scaleMultiplier">프리팹 원본 로컬 스케일에 곱할 재생별 배율.</param>
        /// <param name="forwardTravelDistance">rot의 forward 방향으로 이동할 연출 거리. 0이면 이동하지 않음.</param>
        /// <param name="forwardTravelDuration">전진 거리에 도달할 시간(초). 0이면 이동하지 않음.</param>
        public void Play(
            Vector3 pos,
            Quaternion rot,
            Vector3 localPositionOffset,
            Vector3 eulerRotationOffset,
            Vector3 scaleMultiplier,
            float forwardTravelDistance,
            float forwardTravelDuration)
        {
            // 풀에서 직전에 사용한 프리셋의 위치/회전/스케일/이동 상태가 남지 않도록
            // 모든 재생별 상태를 원본 기준으로 명시 초기화한다.
            ResetTravelState();
            transform.position = pos + rot * localPositionOffset;
            transform.rotation = rot * Quaternion.Euler(eulerRotationOffset);
            transform.localScale = Vector3.Scale(_originalLocalScale, scaleMultiplier);

            _travelStartPosition = transform.position;
            _travelDirection = rot * Vector3.forward;
            _travelDistance = Mathf.Max(0f, forwardTravelDistance);
            _travelDuration = Mathf.Max(0f, forwardTravelDuration);
            _isTravelling = _travelDistance > 0f && _travelDuration > 0f;

            gameObject.SetActive(true);
            _active = true;

            // 루트 PS가 있으면 그것만(하위 포함) 재생. 없으면 형제 시스템을 전부 재생.
            if (_ps != null)
            {
                // 이전 재생 잔여 파티클을 제거하고 처음부터 재생. Play()=Play(true)로 하위까지 함께.
                _ps.Clear();
                _ps.Play();
            }
            else if (_childSystems != null)
            {
                // 형제 파티클 시스템을 각각 개별 재생(각자의 하위 포함). 하나만 재생하면 나머지가 안 보인다.
                for (int i = 0; i < _childSystems.Length; i++)
                {
                    var cs = _childSystems[i];
                    if (cs == null) continue;
                    cs.Clear();
                    cs.Play();
                }
            }
        }

        /// <summary>파도 재생 직후의 실제 풀·파티클 상태만 수동적으로 관측한다.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveWavePlayback()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try { _waveDiagnostics?.Record("pool-playback", _wave.Key, _waveClock(), detail:
                $"spawn={_wave.SpawnTime:F6};age={_waveAge:F6};systems={_waveSystems.Length};active={IsPlaybackActive};viewX={transform.position.x:F3};viewY={transform.position.y:F3};viewZ={transform.position.z:F3}"); }
            catch { _waveDiagnostics?.ObserveFault("pool-playback"); }
#endif
        }
        /// <summary>파도 설정을 지우기 전에 반환·교체·파괴 경계를 기록한다.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ObserveWaveReturn(string reason)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_waveDriven || _waveDiagnostics == null) return;
            try {
                double now = _waveClock();
                _waveDiagnostics.Record("pool-retire", _wave.Key, now, detail: $"reason={reason};age={_waveAge:F6};active={IsPlaybackActive}");
                _waveDiagnostics.Finish(reason, now);
                _waveDiagnostics = null;
            } catch { _waveDiagnostics?.ObserveFault("pool-return"); }
#endif
        }

        /// <summary>실측 메뉴와 runtime이 같은 fixed-step 재생·축별 보정을 사용한다.</summary>
        public void PlayWave(Hexiege.Application.WavePresentationSpawn spawn, Func<double> clock,
            Vector3 offset, Vector3 euler, Vector3 scale, WaveVisualCalibration calibration)
        {
            ObserveWaveReturn("replaced");
            ResetTravelState();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _waveDiagnostics = new Hexiege.Application.WavePresentationDiagnostics("pool");
            try { _waveDiagnostics.Begin(spawn.Key.Epoch, clock()); }
            catch { _waveDiagnostics.ObserveFault("pool-clock"); }
#endif
            _wave = spawn; _waveClock = clock; _waveAge = 0f; _waveDriven = true;
            _waveViewOrigin = Hexiege.Core.ViewConverter.ToView(spawn.Origin);
            _waveViewForward = Hexiege.Core.ViewConverter.IsFlipped ? -spawn.Forward : spawn.Forward;
            _waveRotation = Quaternion.LookRotation(_waveViewForward);
            _waveOffset = _waveRotation * calibration.Offset(offset);
            transform.SetPositionAndRotation(_waveViewOrigin + _waveOffset, _waveRotation * Quaternion.Euler(euler));
            transform.localScale = Vector3.Scale(_originalLocalScale, calibration.Scale(scale));
            if (_waveSystems == null)
            {
                _waveSystems = GetComponentsInChildren<ParticleSystem>(true);
                _waveOriginalScaling = new ParticleSystemScalingMode[_waveSystems.Length];
                _waveOriginalAutoSeed = new bool[_waveSystems.Length];
                _waveOriginalSeed = new uint[_waveSystems.Length];
                for (int i = 0; i < _waveSystems.Length; i++) _waveOriginalScaling[i] = _waveSystems[i].main.scalingMode;
            }
            gameObject.SetActive(true); _active = true;
            for (int i = 0; i < _waveSystems.Length; i++)
            {
                var ps = _waveSystems[i];
                _waveOriginalAutoSeed[i] = ps.useAutoRandomSeed;
                _waveOriginalSeed[i] = ps.randomSeed;
                var main = ps.main;
                main.scalingMode = calibration.HierarchySizeScaling ? ParticleSystemScalingMode.Hierarchy : _waveOriginalScaling[i];
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.useAutoRandomSeed = false;
                ps.randomSeed = (uint)(spawn.Key.Id % uint.MaxValue) + (uint)i * 101u;
                ps.Simulate(0f, false, true, false);
            }
            AdvanceWaveToAge((float)Math.Max(0d, clock() - Hexiege.Application.WavePresentationCoordinator.Delay - spawn.SpawnTime));
            ObserveWavePlayback();
        }

        /// <summary>과거 emitter 경로를 bounded substep으로 재현한다. Local/World 모두 같은 이동 이력을 쓴다.</summary>
        public void AdvanceWaveToAge(float requestedAge)
        {
            if (!_waveDriven || float.IsNaN(requestedAge) || float.IsInfinity(requestedAge)) return;
            // 현재 0.5초 production + authored tail. 손상 메시지/지연으로 무제한 simulation하지 않는다.
            float age = Mathf.Clamp(requestedAge, _waveAge, 2f);
            int steps = 0;
            while (_waveAge + WaveSimulationStep <= age && steps++ < 240)
            {
                _waveAge += WaveSimulationStep;
                float front = _wave.Length * Mathf.Clamp01(_waveAge / _wave.Duration);
                transform.position = _waveViewOrigin + _waveOffset + _waveViewForward * front;
                foreach (var ps in _waveSystems) ps.Simulate(WaveSimulationStep, false, false, false);
            }
            // fixed-step emitter 이력은 보존하고 표시 Root만 해당 서버 표현 시각에 정확히 놓는다.
            transform.position = _waveViewOrigin + _waveOffset + _waveViewForward
                * (_wave.Length * Mathf.Clamp01(age / _wave.Duration));
        }

        /// <summary>
        /// 매 프레임 파티클 소멸 여부를 검사.
        /// 재생 중(_active)이고 파티클이 모두 사라졌으면(IsAlive == false) Pool에 반환.
        /// IsAlive(true): 자식 파티클까지 포함하여 살아있는지 확인.
        /// </summary>
        private void Update()
        {
            if (_active && _damageLoop)
            {
                if (DamageCanReturn()) { _damageChannel.Record("Drain", _damageState, "NaturalDrain=True"); Return(); }
                return;
            }
            if (!_active) return;

            if (_bloomLoop)
            {
                // 활성 loop는 particle gap으로 반환하지 않는다. scaled pause 중 tail도 유지한다.
                if (!_bloomStopped) return;
                foreach (var ps in _bloomSystems) if (ps != null && ps.IsAlive(false)) return;
                ObserveBloom("Drain", "RecursivePS=5");
                Return();
                return;
            }

            if (_waveDriven)
                AdvanceWaveToAge((float)Math.Max(0d, _waveClock() - Hexiege.Application.WavePresentationCoordinator.Delay - _wave.SpawnTime));
            else UpdateForwardTravel();

            // 파티클이 모두 소멸했으면 Pool에 반환. 루트 PS든 형제 시스템 묶음이든 "하나라도 살아있으면" 유지.
            // 파티클 시스템이 아예 없는 비정상 케이스는 AnyAlive가 false → 즉시 반환하여 영구 점유 방지.
            if (!AnyAlive() && (!_waveDriven || _waveAge >= _wave.Duration))
            {
                Return();
            }
        }

        /// <summary>
        /// 현재 VFX를 공격 당시 forward 방향으로 선형 이동시킨다.
        /// 이 이동은 화면 연출에만 사용되며 게임의 피해/힐 판정이나 유닛 위치를 변경하지 않는다.
        /// </summary>
        private void UpdateForwardTravel()
        {
            if (!_isTravelling) return;

            _travelElapsed = Mathf.Min(_travelElapsed + Time.deltaTime, _travelDuration);
            float progress = _travelElapsed / _travelDuration;
            transform.position = _travelStartPosition + _travelDirection * (_travelDistance * progress);

            if (_travelElapsed >= _travelDuration)
                _isTravelling = false;
        }

        /// <summary>풀 반환 또는 새 재생 전에 모든 전진 이동 상태를 기본값으로 되돌린다.</summary>
        private void ResetTravelState()
        {
            _waveDriven = false; _waveClock = null; _waveAge = 0f;
            if (_waveSystems != null)
                for (int i = 0; i < _waveSystems.Length; i++)
                {
                    _waveSystems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = _waveSystems[i].main;
                    main.scalingMode = _waveOriginalScaling[i];
                    _waveSystems[i].randomSeed = _waveOriginalSeed[i];
                    _waveSystems[i].useAutoRandomSeed = _waveOriginalAutoSeed[i];
                }
            _travelStartPosition = Vector3.zero;
            _travelDirection = Vector3.zero;
            _travelDistance = 0f;
            _travelDuration = 0f;
            _travelElapsed = 0f;
            _isTravelling = false;
        }

        /// <summary>
        /// 이 VFX의 파티클이 하나라도 살아있는지 검사. 루트 PS(_ps) 또는 형제 시스템(_childSystems)
        /// 전체를 확인하여, 전부 소멸하면 false(= Pool 반환 시점)를 반환한다.
        /// </summary>
        private bool AnyAlive()
        {
            if (_ps != null) return _ps.IsAlive(true);

            if (_childSystems != null)
            {
                for (int i = 0; i < _childSystems.Length; i++)
                {
                    if (_childSystems[i] != null && _childSystems[i].IsAlive(true))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 비활성화 후 Pool에 반환.
        /// 중복 호출을 _active 플래그로 차단한다.
        /// </summary>
        private void OnDestroy()
        {
            if (_active && _damageLoop) _damageChannel?.Record("Teardown", _damageState, "Destroy; NaturalDrain=False; Completeness=Unknown");
            if (_active && _bloomLoop)
            {
                foreach (var ps in _bloomSystems) if (ps != null) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ObserveBloom("TeardownClear", "NaturalDrain=False");
            }
            ObserveWaveReturn("destroy");
        }

        public void Return()
        {
            if (!DamageCanReturn()) return;
            if (_active && _damageLoop) { _damageChannel.Record("Return", _damageState, "NaturalDrain=True"); _damageLoop = false; _damageChannel = null; }
            if (!_active) return;
            if (_bloomLoop && !_bloomStopped) return;
            // StopEmitting은 새 입자 생성만 멈춘다. 외부에서 Return을 요청해도 살아 있는
            // 기존 입자는 자르지 않는다. acquire 실패만 별도 Reject 경로로 즉시 clear한다.
            if (_bloomLoop)
                foreach (var ps in _bloomSystems) if (ps != null && ps.IsAlive(false)) return;
            if (_bloomLoop) ObserveBloom("Return", "RecursivePS=5");
            _bloomLoop = false; _bloomStopped = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _bloomDiagnostics = null;
#endif
            ObserveWaveReturn("pool-return");
            _active = false;

            ResetTravelState();
            transform.localScale = _originalLocalScale;

            gameObject.SetActive(false);
            _onReturn?.Invoke(this);
        }
    }
}
