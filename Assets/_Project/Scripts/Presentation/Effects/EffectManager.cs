// ============================================================================
// EffectManager.cs
// 게임 전체의 VFX(시각 이펙트)를 통합 관리하는 핵심 매니저.
//
// [SOUND_SYSTEM_REFACTOR] SFX는 AudioManager로 분리됨 — 이제 VFX 전용이다.
//   SFX가 필요한 호출부는 EffectManager.Instance?.PlayXxx() 바로 아래 줄에서
//   AudioManager.Instance?.PlayXxxSfx()를 짝지어 호출한다 (GameSystemRules_Sound 규칙 15).
//
// 역할:
//   1. VFX Object Pool — 파티클을 미리 만들어 재사용 (개수 제한 없음).
//   2. Config(유닛/건물/UI) 참조 — 어떤 상황에 어떤 VFX를 재생할지 조회.
//   3. 외부 API — PlayUnitAttack/PlayUnitDeath/... 한 줄 호출로 VFX 재생.
//
// 왜 static Instance인가:
//   UnitView.OnAttackHit()는 유닛 프리팹의 Animation Event에서 호출되므로
//   GameBootstrapper를 통한 의존성 주입(DI) 없이 EffectManager.Instance로
//   직접 접근하는 것이 가장 단순하고 일관성 있다.
//   SingletonMonoBehaviour는 DontDestroyOnLoad가 포함되어 Game씬 전용에
//   부적합하므로, static field를 직접 관리한다.
//   (Game씬을 벗어나면 OnDestroy에서 Instance를 null로 정리한다.)
//
// 최적화 방침:
//   - VFX: Pool, 개수 제한 없음 (눈에 보여서 일부만 재생되면 어색함).
//   - SFX: Pool + 동시 8개 제한 (소리는 겹쳐도 자연스럽고, 제한해도 어색하지 않음).
//
// 부착 위치: Game씬의 EffectManager GameObject (GameBootstrapper에서 Initialize 호출).
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, VFX Pool).
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

namespace Hexiege.Presentation
{
    /// <summary>
    /// 유닛 공격 VFX가 어느 경계까지 진행됐는지 나타내는 읽기 전용 결과.
    /// 기존 재생 흐름을 바꾸지 않고 InfernoSpirit 간헐 누락 진단에만 사용한다.
    /// </summary>
    public enum UnitAttackVfxPlaybackStatus
    {
        Started = 0,
        PresetUnavailable = 1,
        PrefabUnavailable = 2,
        PoolItemUnavailable = 3,
        ParticleSystemUnavailable = 4,
        PlaybackInactive = 5,
    }

    /// <summary>유닛 공격 VFX 1회 재생 시도의 불변 진단 결과.</summary>
    public readonly struct UnitAttackVfxPlaybackResult
    {
        public UnitAttackVfxPlaybackStatus Status { get; }
        public int ParticleSystemCount { get; }
        public bool PlaybackActive { get; }

        public UnitAttackVfxPlaybackResult(
            UnitAttackVfxPlaybackStatus status,
            int particleSystemCount,
            bool playbackActive)
        {
            Status = status;
            ParticleSystemCount = particleSystemCount;
            PlaybackActive = playbackActive;
        }
    }

    /// <summary>
    /// 게임 전체 VFX를 관리하는 매니저. 오디오는 AudioManager가 별도로 담당한다.
    /// Game씬 전용 static Instance (DontDestroyOnLoad 없음).
    /// </summary>
    public class EffectManager : MonoBehaviour
    {
        // ====================================================================
        // static Instance — 외부에서 EffectManager.Instance로 접근
        // ====================================================================

        /// <summary>
        /// 전역 접근점. UnitView 등 프리팹 컴포넌트에서 DI 없이 직접 사용.
        /// Instance가 null일 수 있으므로 호출 측은 항상 ?. 연산자로 안전 처리해야 함.
        /// </summary>
        public static EffectManager Instance { get; private set; }

        // ====================================================================
        // Inspector 설정
        // ====================================================================

        [Header("References")]
        [Tooltip("재생된 VFX 오브젝트들의 부모 컨테이너. 씬의 빈 GameObject 연결.")]
        [SerializeField] private Transform _vfxContainer;

        // SOUND_SYSTEM_REFACTOR: SFX 컨테이너/동시 재생 제한은 AudioManager로 이전 완료. 검증 후 삭제 예정.
        // [Tooltip("SFX용 AudioSource 오브젝트들의 부모 컨테이너. 씬의 빈 GameObject 연결.")]
        // [SerializeField] private Transform _sfxContainer;
        //
        // [Header("SFX 제한")]
        // [Tooltip("동시에 재생 가능한 최대 SFX 개수. 초과 시 새 SFX는 무시됨.")]
        // [SerializeField] private int _maxConcurrentSfx = 8;

        [Header("VFX Pool 초기 크기")]
        [Tooltip("프리팹 종류마다 미리 생성해 둘 VFX 인스턴스 수. (현재는 lazy 생성이라 미사용 — 향후 사전 워밍업용)")]
        [SerializeField] private int _initialPoolSizePerType = 5;

        // ====================================================================
        // Config 참조 (Initialize()에서 주입)
        // ====================================================================

        private UnitEffectConfig _unitConfig;
        private BuildingEffectConfig _buildingConfig;
        private UiEffectConfig _uiConfig;

        // ====================================================================
        // Pool 내부 상태
        // ====================================================================

        /// <summary>
        /// VFX Pool — 원본 프리팹별로 별도의 Queue를 둔다.
        /// 같은 프리팹끼리만 같은 Queue에서 재사용해야 하므로 Dictionary 구조.
        /// </summary>
        private readonly Dictionary<GameObject, Queue<VfxPoolItem>> _vfxPools
            = new Dictionary<GameObject, Queue<VfxPoolItem>>();

        /// <summary>
        /// 트레이서(원거리 발사체) Pool — VFX Pool과 동일하게 원본 프리팹별로 Queue를 둔다.
        /// 트레이서는 파티클 소멸이 아니라 "착탄"으로 수명이 끝나므로 별도 풀로 관리한다.
        /// </summary>
        private readonly Dictionary<GameObject, Queue<TracerProjectile>> _tracerPools
            = new Dictionary<GameObject, Queue<TracerProjectile>>();

        /// <summary>
        /// [2026-10-13 R2-1] 지금 재생 중인 "시전자 귀속" 공격 이펙트 목록(기 모으기 charge 등).
        /// 시전자 사망·제거 시 그 시전자의 이펙트만 골라 즉시 끄기 위해 기억해 둔다.
        /// 아이템이 풀로 돌아오면(OnVfxReturn) 목록에서 빠진다. 귀속 이펙트는 몇 개뿐이라 순회 비용이 작다.
        /// </summary>
        private readonly HashSet<VfxPoolItem> _casterOwnedItems = new HashSet<VfxPoolItem>();

        /// <summary>StopCasterOwnedAttackEffects에서 순회 중 목록 변경을 피하려고 재사용하는 임시 버퍼(매번 new 하지 않음).</summary>
        private readonly List<VfxPoolItem> _casterStopBuffer = new List<VfxPoolItem>();

        // SOUND_SYSTEM_REFACTOR: SFX 풀 상태는 AudioManager로 이전 완료. 검증 후 삭제 예정.
        // /// <summary> SFX Pool — 모든 사운드가 공유하는 AudioSource 큐. </summary>
        // private readonly Queue<AudioSource> _sfxPool = new Queue<AudioSource>();
        //
        // /// <summary> 현재 재생 중인 SFX 개수. _maxConcurrentSfx와 비교하여 제한. </summary>
        // private int _activeSfxCount = 0;

        // ====================================================================
        // Unity 생명주기 — static Instance 관리
        // ====================================================================

        /// <summary> 씬 진입 시 Instance 등록. </summary>
        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// 파괴 시 Instance 정리.
        /// 다른 EffectManager가 이미 Instance를 차지한 경우(중복 배치 등)는 건드리지 않음.
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            // 씬 정리 중에는 귀속 이펙트도 함께 파괴되므로 참조만 놓아 준다.
            _casterOwnedItems.Clear();
        }

        // ====================================================================
        // 초기화 — GameBootstrapper에서 호출
        // ====================================================================

        /// <summary>
        /// 매니저 초기화. Config를 연결하고 각 Config의 조회 Dictionary를 빌드한다.
        /// GameBootstrapper의 게임 시작 흐름에서 호출.
        /// </summary>
        /// <param name="unitConfig">유닛 이펙트 Config (null 허용).</param>
        /// <param name="buildingConfig">건물 이펙트 Config (null 허용).</param>
        /// <param name="uiConfig">UI 이펙트 Config (null 허용).</param>
        public void Initialize(
            UnitEffectConfig unitConfig,
            BuildingEffectConfig buildingConfig,
            UiEffectConfig uiConfig)
        {
            _unitConfig = unitConfig;
            _buildingConfig = buildingConfig;
            _uiConfig = uiConfig;

            // 각 Config의 List → Dictionary 변환 (null이면 건너뜀).
            _unitConfig?.Initialize();
            _buildingConfig?.Initialize();
            _uiConfig?.Initialize();

            // SOUND_SYSTEM_REFACTOR: SFX 풀 사전 생성은 AudioManager로 이전 완료. 검증 후 삭제 예정.
            // // SFX Pool 사전 생성 — 동시 재생 한도만큼 AudioSource를 미리 만들어 둔다.
            // for (int i = 0; i < _maxConcurrentSfx; i++)
            // {
            //     AudioSource src = CreateSfxSource();
            //     src.gameObject.SetActive(false);
            //     _sfxPool.Enqueue(src);
            // }
        }

        // ====================================================================
        // 외부 API — 상황별 이펙트 재생
        // ====================================================================

        /// <summary>
        /// 유닛 공격 이펙트 재생. UnitView.OnAttackHit()(Animation Event)에서 호출.
        /// 피스톨러처럼 방향성이 있는 VFX를 위해 회전(rot)을 함께 받는다.
        /// </summary>
        /// <param name="type">공격한 유닛의 타입.</param>
        /// <param name="pos">재생할 월드 좌표 (보통 총구 또는 유닛 위치).</param>
        /// <param name="rot">재생할 월드 회전 (VFX 발사 방향). 방향성이 없는 VFX는 영향 없음.</param>
        /// <param name="caster">
        /// [2026-10-13 R2-1] 공격한 유닛의 UnitView. 프리셋의 StopWhenCasterDies가 켜져 있으면(기 모으기 charge 등)
        /// 이 유닛이 죽거나 제거될 때 이펙트를 즉시 멈춘다. null이면(에디터 테스트 스포너 등) 예전처럼 끝까지 재생.
        /// </param>
        public UnitAttackVfxPlaybackResult PlayUnitAttack(UnitType type, Vector3 pos, Quaternion rot, UnitView caster = null)
        {
            EffectPreset preset = _unitConfig?.GetAttack(type);
            if (preset == null)
            {
                return new UnitAttackVfxPlaybackResult(
                    UnitAttackVfxPlaybackStatus.PresetUnavailable,
                    0,
                    false);
            }

            if (preset.VfxPrefab == null)
            {
                return new UnitAttackVfxPlaybackResult(
                    UnitAttackVfxPlaybackStatus.PrefabUnavailable,
                    0,
                    false);
            }

            VfxPoolItem item = GetOrCreateVfx(preset.VfxPrefab);
            if (item == null)
            {
                return new UnitAttackVfxPlaybackResult(
                    UnitAttackVfxPlaybackStatus.PoolItemUnavailable,
                    0,
                    false);
            }

            item.Play(
                pos,
                rot,
                preset.LocalPositionOffset,
                preset.EulerRotationOffset,
                preset.ScaleMultiplier,
                preset.ForwardTravelDistance,
                preset.ForwardTravelDuration);

            // [R2-1] 시전자 귀속 프리셋(현재 FoxMagician·InfernoSpirit 기 모으기)만 시전자에게 묶는다.
            //   반드시 Play() 뒤에 묶는다 — Play가 이전 재생의 귀속 상태를 먼저 지우기 때문이다.
            //   진단 결과(status·개수·재생 여부) 계산은 아래 기존 코드 그대로다.
            if (preset.StopWhenCasterDies && caster != null && item.BeginCasterOwnership(caster, caster.Data))
                _casterOwnedItems.Add(item);

            int particleSystemCount = item.ParticleSystemCount;
            bool playbackActive = item.IsPlaybackActive;
            UnitAttackVfxPlaybackStatus status = particleSystemCount <= 0
                ? UnitAttackVfxPlaybackStatus.ParticleSystemUnavailable
                : playbackActive
                    ? UnitAttackVfxPlaybackStatus.Started
                    : UnitAttackVfxPlaybackStatus.PlaybackInactive;
            return new UnitAttackVfxPlaybackResult(status, particleSystemCount, playbackActive);
        }

        /// <summary>
        /// [2026-10-13 R2-1] 이 시전자에게 귀속된 공격 이펙트(기 모으기 charge 등)를 모두 즉시 멈추고 풀에 반환한다.
        /// UnitView가 사망 이벤트와 OnDisable(파괴·디스폰)에서 호출한다. Host·Client 각자 자기 화면에서 실행된다.
        /// [P4 — 2026-10-19 차지 유닛 발사 시점] 「공격 취소」 경계에서도 호출된다: 차지 도중 공격이 끊기면
        /// UnitView.StopOwnedChargeEffects가 이 함수로 차지 VFX를 즉시 끈다(사용자 결정 D2 — 화면 표현 전용, 피해·lease 무변경).
        /// 사망 이펙트·트레이서·착탄·타워 번개·지속 이펙트처럼 귀속되지 않은 이펙트는 건드리지 않는다.
        /// </summary>
        /// <param name="caster">사망·제거되는 유닛의 UnitView(파괴 중이어도 같은 객체 참조로 찾는다).</param>
        /// <param name="reason">정지 사유(진단용 짧은 토큰).</param>
        /// <returns>실제로 정지한 이펙트 수.</returns>
        public int StopCasterOwnedAttackEffects(UnitView caster, string reason)
        {
            if (caster is null || _casterOwnedItems.Count == 0) return 0;
            // 정지하면 Return → OnVfxReturn이 목록에서 빼므로, 순회 중 변경을 피하려고 먼저 버퍼에 골라 담는다.
            _casterStopBuffer.Clear();
            foreach (VfxPoolItem item in _casterOwnedItems)
            {
                // 파괴된 아이템(씬 정리 등)은 건드리면 예외가 나므로 제외한다(아래에서 목록에서도 정리).
                if (item != null && item.IsOwnedByCaster(caster)) _casterStopBuffer.Add(item);
            }
            int stopped = 0;
            for (int i = 0; i < _casterStopBuffer.Count; i++)
                if (_casterStopBuffer[i].StopCasterOwned(reason)) stopped++;
            _casterStopBuffer.Clear();
            // 파괴된 아이템 참조가 남아 있으면 정리한다(사망 시점에만 실행되므로 비용 무시 가능).
            _casterOwnedItems.RemoveWhere(owned => owned == null);
            return stopped;
        }

        /// <summary>
        /// [2026-10-26 수정안 (a)] 이 시전자에게 귀속된 공격 이펙트(차지 등)를 사망 대역(ghost)으로 넘긴다.
        /// 시전자의 사망 표현이 투사체 화면 착탄까지 미뤄질 때 UnitView가 부른다 — 차지 VFX도 사망 표현과 같은 순간에 멈추게 하려는 것이다.
        /// </summary>
        /// <param name="caster">사망한 시전자 UnitView.</param>
        /// <param name="ghost">사망 대역 GameObject(떼어 낸 Visual Root).</param>
        /// <returns>넘긴 이펙트 수.</returns>
        public int HandOverCasterOwnedAttackEffects(UnitView caster, GameObject ghost)
        {
            if (caster is null || ghost == null || _casterOwnedItems.Count == 0) return 0;
            int moved = 0;
            foreach (VfxPoolItem item in _casterOwnedItems)
                if (item != null && item.HandOverCasterOwnershipToGhost(caster, ghost)) moved++;
            return moved;
        }

        /// <summary>
        /// [2026-10-26 수정안 (a)] 사망 대역에 귀속된 공격 이펙트를 모두 즉시 멈추고 풀에 반환한다(미뤄 둔 사망 표현을 진행할 때).
        /// </summary>
        /// <param name="ghost">사망 대역 GameObject(이미 파괴 중이어도 같은 참조로 찾는다).</param>
        /// <param name="reason">정지 사유(진단용 짧은 토큰).</param>
        /// <returns>정지한 이펙트 수.</returns>
        public int StopGhostOwnedAttackEffects(GameObject ghost, string reason)
        {
            if (ghost is null || _casterOwnedItems.Count == 0) return 0;
            _casterStopBuffer.Clear();
            foreach (VfxPoolItem item in _casterOwnedItems)
                if (item != null && item.IsOwnedByGhost(ghost)) _casterStopBuffer.Add(item);
            int stopped = 0;
            for (int i = 0; i < _casterStopBuffer.Count; i++)
                if (_casterStopBuffer[i].StopCasterOwned(reason)) stopped++;
            _casterStopBuffer.Clear();
            _casterOwnedItems.RemoveWhere(owned => owned == null);
            return stopped;
        }

        /// <summary>서버에서 실제 생성된 파도만 재생한다. 기존 marker 공격 API와 소유권을 섞지 않는다.</summary>
        public bool WavePresentationReady => _unitConfig?.GetAttack(UnitType.TorrentSpirit)?.VfxPrefab != null;

        public bool BloomPresentationReady => _unitConfig?.GetHeal(UnitType.BloomFairy)?.VfxPrefab != null;
        /// <summary>
        /// 화상(InfernoSpirit)·독(MushroomBomber) 지속 이펙트를 대상 유닛에 붙여 시작한다.
        /// </summary>
        /// <param name="state">서버가 정한 지속 피해 표현 상태.</param>
        /// <param name="pose">대상 유닛 VisualRoot Transform.</param>
        /// <param name="channel">진단 기록 채널.</param>
        /// <param name="recipientType">
        /// 이펙트가 붙을 "맞은 유닛"의 타입. 있으면 UnitEffectConfig 크기 표로 크기·발밑 위치를 맞춘다.
        /// null이면(또는 표에 없으면) 배율 1 — 예전과 같은 크기.
        /// </param>
        public VfxPoolItem StartTimedDamageEffect(Hexiege.Application.TimedDamageEffectPresentation state, Transform pose,
            Hexiege.Application.TimedDamageEffectChannel channel, UnitType? recipientType = null)
        {
            if (!state.IsValid || !state.Active || (state.Visual != Hexiege.Application.TimedDamageVisual.Burn
                && state.Visual != Hexiege.Application.TimedDamageVisual.Poison)) return null;
            var type = state.Visual == Hexiege.Application.TimedDamageVisual.Burn ? UnitType.InfernoSpirit : UnitType.MushroomBomber;
            var preset = _unitConfig?.GetTimedDamage(type);
            if (pose == null || preset?.VfxPrefab == null) return null;
            var item = GetOrCreateVfx(preset.VfxPrefab);
            // 크기 맞춤은 재생 시작 때 1번만 표에서 읽는다(매 프레임 렌더러 크기를 재지 않음 — D-1).
            RecipientFit fit = ResolveRecipientFit(recipientType);
            try { item.PlayTimedDamage(state, pose, preset, channel, fit); }
            catch { item.RejectTimedDamageAcquire(); throw; }
            // Sound15: recipient poison/burn SFX is not authored; no invented sound fallback.
            return item;
        }

        /// <summary>
        /// BloomFairy 힐 지속 이펙트를 대상에 붙여 시작한다(크기 맞춤 없음 — 배율 1).
        /// 대상 타입을 아는 호출은 아래 recipientType 오버로드를 쓴다.
        /// </summary>
        public VfxPoolItem StartBloomEffect(Hexiege.Application.BloomHealEffectPresentation state, Transform pose
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            , Hexiege.Application.BloomHealEffectDiagnostics diagnostics
#endif
            )
        {
            return StartBloomEffectCore(state, pose, RecipientFit.Identity
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                , diagnostics
#endif
                );
        }

        /// <summary>
        /// BloomFairy 힐 지속 이펙트를 대상에 붙여 시작하고, 대상 유닛 크기에 맞춘다(D-2: 화상과 같은 방식).
        /// </summary>
        /// <param name="state">서버가 정한 힐 표현 상태.</param>
        /// <param name="pose">대상 유닛 VisualRoot Transform.</param>
        /// <param name="recipientType">힐을 받는 유닛 타입. 크기 표에 없으면 배율 1.</param>
        public VfxPoolItem StartBloomEffect(Hexiege.Application.BloomHealEffectPresentation state, Transform pose,
            UnitType recipientType
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            , Hexiege.Application.BloomHealEffectDiagnostics diagnostics
#endif
            )
        {
            return StartBloomEffectCore(state, pose, ResolveRecipientFit(recipientType)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                , diagnostics
#endif
                );
        }

        /// <summary>대상 유닛 타입으로 크기 표를 조회한다. 타입을 모르거나 표가 없으면 Identity(배율 1).</summary>
        private RecipientFit ResolveRecipientFit(UnitType? recipientType)
        {
            if (!recipientType.HasValue || _unitConfig == null) return RecipientFit.Identity;
            return _unitConfig.GetRecipientFit(recipientType.Value);
        }

        private VfxPoolItem StartBloomEffectCore(Hexiege.Application.BloomHealEffectPresentation state, Transform pose,
            RecipientFit fit
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            , Hexiege.Application.BloomHealEffectDiagnostics diagnostics
#endif
            )
        {
            var preset = _unitConfig?.GetHeal(UnitType.BloomFairy);
            if (pose == null || preset?.VfxPrefab == null) return null;
            var item = GetOrCreateVfx(preset.VfxPrefab);
            try
            {
                if (!item.BeginBloomAcquire()) throw new System.InvalidOperationException("Bloom pool checkout already owned");
                if (item.PlayBloomLoop(state, pose, preset, fit
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                , diagnostics
#endif
                    ) == 0) { item.RejectBloomAcquire(); return null; }
            }
            catch { item.RejectBloomAcquire(); throw; } // 최종 운영 예외 기록은 presenter 경계에서 수행한다.
            // Sound15: Bloom heal SFX는 미제작이다. current/refresh 복구에서 임의 사운드를 만들지 않는다.
            return item;
        }

        public VfxPoolItem PlayWave(Hexiege.Application.WavePresentationSpawn spawn, System.Func<double> clock)
        {
            var preset = _unitConfig?.GetAttack(UnitType.TorrentSpirit);
            if (preset == null || preset.VfxPrefab == null || !spawn.IsValid) return null;
            var item = GetOrCreateVfx(preset.VfxPrefab);
            if (item == null) return null;
            item.PlayWave(spawn, clock, preset.LocalPositionOffset, preset.EulerRotationOffset,
                preset.ScaleMultiplier, WaveVisualCalibration.Production);
            return item;
        }

        /// <summary>
        /// 유닛 사망 이펙트 재생. UnitView의 OnUnitDied 처리에서 호출.
        /// </summary>
        /// <param name="type">사망한 유닛의 타입.</param>
        /// <param name="pos">재생할 월드 좌표.</param>
        public void PlayUnitDeath(UnitType type, Vector3 pos)
        {
            // 사망 VFX는 방향성이 없으므로 회전 없이(identity) 재생.
            Play(_unitConfig?.GetDeath(type), pos, Quaternion.identity);
        }

        /// <summary>
        /// 유닛 피격 이펙트 재생. HitPresentationQueue가 피격 연출 방출 시점에 호출.
        /// 피격 VFX는 방향성이 없으므로 회전 없이(identity) 재생 — PlayUnitDeath와 동일 패턴.
        /// 프리셋이 미설정(GetHit == null)이면 Play(null)이 조용히 스킵한다.
        /// </summary>
        /// <param name="type">피격당한 유닛의 타입.</param>
        /// <param name="pos">재생할 월드 좌표 (보통 피격 유닛 위치).</param>
        public void PlayUnitHit(UnitType type, Vector3 pos)
        {
            TryPlayUnitHit(type, pos);

            // 피격 SFX 없음 — SoundConfig에 피격 SFX 엔트리가 아직 없어 이번 작업에서 추가하지 않는다.
            //   추후 피격 효과음 에셋 확보 시, 이 줄 바로 아래에 AudioManager.Instance?.PlayUnitHitSfx(type)
            //   짝을 추가한다 (GameSystemRules_Sound 규칙 15 — VFX+SFX 쌍).
        }

        /// <summary>
        /// 유닛 피격 VFX가 실제로 설정되어 재생을 시작했는지 반환한다. 확정 결과 표현기는
        /// 이 반환값으로 "API를 호출했지만 프리셋이 없어 화면에는 아무것도 없었던 경우"를
        /// 성공으로 잘못 집계하지 않는다.
        /// </summary>
        public bool TryPlayUnitHit(UnitType type, Vector3 pos)
        {
            EffectPreset preset = _unitConfig?.GetHit(type);
            if (preset == null || preset.VfxPrefab == null) return false;
            Play(preset, pos, Quaternion.identity);
            return true;
        }

        /// <summary>피격 VFX 프리팹이 실제로 배선되어 있는지 재생 전에 확인한다.</summary>
        public bool CanPlayUnitHit(UnitType type)
        {
            EffectPreset preset = _unitConfig?.GetHit(type);
            return preset != null && preset.VfxPrefab != null;
        }

        /// <summary>
        /// 건물 파괴 이펙트 재생.
        /// </summary>
        /// <param name="type">파괴된 건물의 타입.</param>
        /// <param name="pos">재생할 월드 좌표.</param>
        public void PlayBuildingDestroy(BuildingType type, Vector3 pos)
        {
            // 건물 파괴 VFX는 방향성이 없으므로 회전 없이(identity) 재생.
            Play(_buildingConfig?.GetDestroy(type), pos, Quaternion.identity);
        }

        /// <summary>
        /// 건물 업그레이드 이펙트 재생.
        /// </summary>
        /// <param name="type">업그레이드된 건물의 타입.</param>
        /// <param name="pos">재생할 월드 좌표.</param>
        public void PlayBuildingUpgrade(BuildingType type, Vector3 pos)
        {
            // 건물 업그레이드 VFX는 방향성이 없으므로 회전 없이(identity) 재생.
            Play(_buildingConfig?.GetUpgrade(type), pos, Quaternion.identity);
        }

        /// <summary>
        /// 건물 공격(발사) 이펙트 재생. 방어 타워(AutoTower)가 적을 공격하는 순간 재생한다.
        /// HitPresentationQueue의 "타워 즉시 방출 경로"에서 각 클라이언트 로컬로 호출된다.
        /// 총구 화염처럼 방향성이 있는 VFX를 위해 회전(rot)을 함께 받는다(PlayUnitAttack과 동일 패턴).
        /// 프리셋이 미설정(GetAttack == null)이면 Play(null)이 조용히 스킵한다.
        /// </summary>
        /// <param name="type">공격한 건물(타워)의 타입.</param>
        /// <param name="pos">재생할 월드 좌표 (보통 타워 위치).</param>
        /// <param name="rot">재생할 월드 회전 (타워 → 타겟 발사 방향). 방향성이 없는 VFX는 영향 없음.</param>
        public void PlayBuildingAttack(BuildingType type, Vector3 pos, Quaternion rot)
        {
            Play(_buildingConfig?.GetAttack(type), pos, rot);

            // 타워 발사 SFX 없음 — SoundConfig/AudioManager에 타워 발사 효과음 엔트리가 아직 없어
            //   이번 작업에서 추가하지 않는다. 추후 발사 효과음 에셋 확보 시, 이 줄 바로 아래에
            //   AudioManager.Instance?.PlayBuildingAttackSfx(type) 짝을 추가한다
            //   (GameSystemRules_Sound 규칙 15 — VFX+SFX 쌍).
        }
        public System.Func<double> AttackEffectClock = () => Time.timeAsDouble;
        public bool PlayConfirmedMushroomTracer(Vector3 source, Vector3 impact, double remaining)
        {
            if (double.IsNaN(remaining) || double.IsInfinity(remaining) || remaining <= 0d) return false;
            var prefab = _unitConfig?.GetTracer(UnitType.MushroomBomber)?.VfxPrefab;
            if (prefab == null) return false;
            var tracer = GetOrCreateTracer(prefab);
            tracer.Launch(source, impact, null, (float)remaining, true); // callback has no HP/result authority.
            bool started = false;
            foreach (var ps in tracer.GetComponentsInChildren<ParticleSystem>(true)) started |= ps.isPlaying;
            return tracer.gameObject.activeInHierarchy && started;
        }
        /// <summary>
        /// 서버가 확정한 공격 표현 스냅샷(타워 발사 / 버섯 착탄)을 이 화면에서 재생한다.
        /// 피해·HP와 무관한 화면 표현 전용이다.
        /// </summary>
        /// <param name="s">서버 확정 스냅샷.</param>
        /// <param name="source">시작점(화면 좌표).</param>
        /// <param name="impact">공격 순간 대상 위치(화면 좌표).</param>
        /// <param name="outcome">진단 기록용 결과 문자열.</param>
        /// <param name="boltTarget">
        /// [2026-10-13 B] 정령(Spirit) 타워 번개가 따라갈 "원래 맞은 유닛". Spirit 타워일 때만 쓰인다.
        /// default(해석 실패)면 추적 없이 공격 순간 위치까지 길이만 맞춘다. Human 타워·버섯 착탄은 무시한다.
        /// </param>
        /// <param name="boltUntrackedReason">boltTarget이 default인 이유(진단용). null 허용.</param>
        public bool PlayAttackEffect(Hexiege.Application.AttackEffectPresentation s, Vector3 source, Vector3 impact, out string outcome,
            TowerBoltTarget boltTarget = default, string boltUntrackedReason = null)
        {
            outcome = "InvalidSnapshotOrVisual";
            if (!s.IsValid || (s.Visual != Hexiege.Application.AttackEffectVisual.Tower && s.Visual != Hexiege.Application.AttackEffectVisual.MushroomImpact)) return false;
            if (s.Visual == Hexiege.Application.AttackEffectVisual.Tower && s.Race == RaceId.Transcendence) { outcome = "UnspecifiedTranscendence"; return false; }
            EffectPreset preset = s.Visual == Hexiege.Application.AttackEffectVisual.Tower
                ? _buildingConfig?.GetAttack(BuildingType.AutoTower, s.Race) : _unitConfig?.GetImpact(UnitType.MushroomBomber);
            if (preset?.VfxPrefab == null) { outcome = "MissingConfiguredPreset"; return false; }
            Vector3 direction = impact - source; direction.y = 0f;
            var rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
            var item = GetOrCreateVfx(preset.VfxPrefab);
            var systems = item.GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length == 0) { outcome = "ParticleSystemUnavailable"; item.RejectTimedDamageAcquire(); return false; }
            foreach (var ps in systems) { var main = ps.main; main.stopAction = ParticleSystemStopAction.None; }
            item.Play(s.Visual == Hexiege.Application.AttackEffectVisual.Tower ? source : impact, rotation,
                preset.LocalPositionOffset, preset.EulerRotationOffset, preset.ScaleMultiplier, preset.ForwardTravelDistance, preset.ForwardTravelDuration);
            bool started = false; foreach (var ps in systems) started |= ps.isPlaying;
            outcome = started ? "AuthoritySnapshotStarted" : "PlaybackInactive";
            // [2026-10-13 B] 정령 타워만: 번개 길이를 실제 거리에 맞추고 원래 맞은 유닛을 끝점으로 추적한다.
            //   Human 타워(현행 유지)와 MushroomImpact는 이 분기를 타지 않아 동작이 이전과 같다.
            //   Type+Race 선택(위 preset 조회)과 Transcendence 미지정 no-fallback은 그대로다.
            // [R2-2] 끝점 높이 = 맞은 유닛의 가슴 높이(UnitEffectConfig 크기 표의 키 × 비율, 표에 없으면 0.6m).
            //   대상 데이터를 모르면(해석 실패) 같은 대체값 0.6m를 쓴다. 화면 표현 전용 값이다.
            if (s.Visual == Hexiege.Application.AttackEffectVisual.Tower && s.Race == RaceId.Spirit)
                outcome += "; Endpoint=" + item.BeginTowerBolt(s, source, impact, preset.LocalPositionOffset,
                    preset.EulerRotationOffset, boltTarget, boltUntrackedReason, ResolveBoltChestHeight(boltTarget.Data));
            // Sound15: impact and tower SFX are unspecified, no additional sound fallback.
            return started;
        }

        /// <summary>
        /// [2026-10-13 R2-2] 타워 번개가 맞을 가슴 높이(발밑 기준 월드 미터)를 정한다.
        /// 대상 유닛 타입을 알면 UnitEffectConfig.GetChestHeight, 모르거나 설정이 없으면 0.6m 대체값.
        /// </summary>
        /// <param name="target">원래 맞은 유닛의 Domain 데이터. 해석 실패면 null.</param>
        private float ResolveBoltChestHeight(UnitData target)
        {
            if (target == null || _unitConfig == null) return UnitEffectConfig.FallbackChestHeight;
            return _unitConfig.GetChestHeight(target.Type);
        }

        /// <summary>
        /// UI VFX 재생. 위치 개념이 없으므로 Vector3.zero에서 재생한다.
        /// </summary>
        /// <param name="key">재생할 UI 이펙트 키.</param>
        public void PlayUi(UiEffectKey key)
        {
            // UI VFX는 원점에서 회전 없이(identity) 재생한다. UI SFX는 AudioManager가 별도로 담당한다.
            Play(_uiConfig?.Get(key), Vector3.zero, Quaternion.identity);
        }

        /// <summary>
        /// 원거리 유닛의 트레이서(발사체)를 발사한다. UnitView.OnAttackHit()(원거리 분기)에서 호출.
        /// 발사 지점(start)에서 타겟 지점(target)까지 직선 비행 후, 착탄 순간 onArrive를 1회 호출한다.
        ///
        /// 착탄 콜백(onArrive)의 역할:
        ///   호출부(UnitView)가 여기에 "OnLocalAttackHit 발행"을 넣어, 피격 연출(HP 텍스트·피격 VFX·
        ///   타격 반응) 방출을 트레이서 착탄 시점에 맞춘다. 즉 트레이서 비행 시간이 곧 피격 연출 지연이 된다.
        ///
        /// 폴백:
        ///   해당 유닛 타입에 트레이서 프리셋이 없으면(GetTracer == null) 비행 연출 없이
        ///   onArrive를 즉시 호출한다 → 기존처럼 "즉시 피격 연출"로 동작한다(트레이서 미설정 유닛 대비).
        /// </summary>
        /// <param name="type">공격하는 원거리 유닛의 타입.</param>
        /// <param name="start">발사 지점(월드). 보통 유닛의 총구/발사점.</param>
        /// <param name="target">도착 지점(월드). 발사 시점의 타겟 위치(값 복사).</param>
        /// <param name="onArrive">착탄 시 1회 호출할 콜백. 피격 연출 방출을 트리거한다. null 허용.</param>
        /// <param name="authoritativeDuration">서버 표시 시점까지 남은 비행 시간(초). 음수면 프리팹 속도(_speed)를 쓴다.</param>
        /// <param name="playImpactOnArrive">
        /// [R5 임시 — C 방식(서버 투사체) 구현 시 교체] true면 트레이서가 "도착하는 순간" 공격자 타입의
        /// 착탄 이펙트(impactPreset)를 도착 지점에서 1회 재생한다. Legacy 트레이서 호출부(UnitView)만 true를 넘긴다.
        /// </param>
        public void PlayTracer(UnitType type, Vector3 start, Vector3 target, Action onArrive,
            float authoritativeDuration = -1f, bool playImpactOnArrive = false)
        {
            EffectPreset preset = _unitConfig?.GetTracer(type);
            GameObject prefab = preset != null ? preset.VfxPrefab : null;

            // 트레이서 프리팹 미설정 → 비행 연출 없이 즉시 착탄 콜백 실행(폴백).
            //   이 경우 호출부의 피격 연출은 지연 없이 기존처럼 즉시 방출된다.
            // [R5 임시] 이 폴백에서는 착탄 이펙트를 재생하지 않는다.
            //   "날아가서 도착"한 투사체가 없으므로 도착 지점 이펙트도 없다. 그래서 착탄 이펙트만
            //   설정되고 트레이서가 아직 없는 유닛(Tank/CannonCart)은 트레이서 프리셋이 연결될 때까지
            //   화면 변화가 없다(데이터만 채우면 그때부터 자동으로 켜진다).
            if (prefab == null)
            {
                onArrive?.Invoke();
                return;
            }

            // [R5 임시 — C 방식(서버 투사체) 구현 시 교체]
            //   기존 착탄 콜백(피해 텍스트 등)을 그대로 먼저 실행하고, 그 직후 착탄 이펙트를 재생하도록
            //   콜백을 한 겹 감싼다. 착탄 이펙트가 설정되지 않은 타입이면 감싸지 않아 기존 동작과 완전히 같다.
            Action arrive = onArrive;
            if (playImpactOnArrive)
            {
                EffectPreset impact = ResolveLegacyArrivalImpact(type);
                if (impact != null)
                {
                    // 발사 순간의 시작/도착 좌표를 값으로 복사해 둔다(비행 중 대상이 사라져도 안전).
                    Vector3 impactStart = start;
                    Vector3 impactPoint = target;
                    arrive = () =>
                    {
                        onArrive?.Invoke();
                        PlayLegacyArrivalImpact(impact, impactStart, impactPoint);
                    };
                }
            }

            // Pool에서 트레이서를 꺼내(또는 새로 만들어) 발사.
            TracerProjectile tracer = GetOrCreateTracer(prefab);
            tracer.Launch(start, target, arrive, authoritativeDuration);
        }

        /// <summary>
        /// [R5 임시 — C 방식(서버 투사체) 구현 시 교체]
        /// Legacy 트레이서가 도착할 때 재생할 "공격자 타입"의 착탄 이펙트 프리셋을 고른다.
        /// 프리셋이 없거나 프리팹이 비어 있으면 null(= 추가로 아무것도 하지 않음).
        ///
        /// MushroomBomber는 항상 null이다. 버섯의 착탄은 서버가 확정한 스냅샷 경로
        /// (PlayAttackEffect의 MushroomImpact)가 단독으로 소유하므로, 여기서 또 재생하면 두 번 터진다.
        /// (현재 UnitView는 버섯의 Legacy 트레이서 호출 자체를 꺼 두었지만, 실수로 다시 켜져도 안전하도록 막는다.)
        /// </summary>
        /// <param name="attackerType">공격한(트레이서를 쏜) 유닛 타입.</param>
        /// <returns>재생할 착탄 프리셋 또는 null.</returns>
        private EffectPreset ResolveLegacyArrivalImpact(UnitType attackerType)
        {
            if (attackerType == UnitType.MushroomBomber) return null;
            EffectPreset impact = _unitConfig?.GetImpact(attackerType);
            return impact != null && impact.VfxPrefab != null ? impact : null;
        }

        /// <summary>
        /// [R5 임시 — C 방식(서버 투사체) 구현 시 교체]
        /// 트레이서 도착 지점에서 착탄 이펙트를 일반 1회성(풀링) 재생으로 터뜨린다.
        /// 위치 = 트레이서가 도착한 지점(발사 순간의 대상 발밑 위치), 회전 = 날아온 방향(수평)이다.
        /// 프리셋의 위치 오프셋·회전 오프셋·배율은 Play()가 그대로 적용한다. 피해·HP와는 무관한 화면 표현이다.
        /// </summary>
        /// <param name="impact">재생할 착탄 프리셋(null 아님).</param>
        /// <param name="start">트레이서 발사 지점(방향 계산용).</param>
        /// <param name="arrival">트레이서 도착 지점(이펙트 위치).</param>
        private void PlayLegacyArrivalImpact(EffectPreset impact, Vector3 start, Vector3 arrival)
        {
            // 높이 차이는 무시하고 땅 위에서 본 방향만 쓴다(물보라가 기울어지지 않도록).
            Vector3 direction = arrival - start;
            direction.y = 0f;
            Quaternion rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction)
                : Quaternion.identity;
            Play(impact, arrival, rotation);
            // Sound15: 착탄 SFX는 아직 지정되지 않아 추가하지 않는다(PlayAttackEffect와 동일).
        }

        // ====================================================================
        // [2026-10-20 서버 권위 투사체] 서버 발사 기록으로 그리는 투사체 · 착탄 이펙트
        //   R5 임시 경로(PlayTracer의 playImpactOnArrive)를 대신하는 정식 경로다. 스위치가 켜진 유닛만 쓴다.
        //   위치·시각은 화면 소비자(ProjectilePresentationPresenter)가 서버 기록으로 정하고, 여기서는
        //   기존 트레이서/착탄 프리셋(UnitEffectConfig의 tracerPreset·impactPreset)을 재사용해 꺼내 주기만 한다.
        // ====================================================================

        /// <summary>
        /// 서버 투사체 1발의 화면 오브젝트(트레이서)를 외부 구동 모드로 꺼낸다.
        /// 트레이서 프리셋이 없는 유닛이면 null(투사체 없이 착탄 이펙트·숫자만 착탄 순간에 나온다).
        /// </summary>
        /// <param name="attackerType">쏜 유닛 종류(트레이서 프리셋 선택).</param>
        /// <param name="start">발사 지점(화면 좌표).</param>
        /// <returns>외부 구동 중인 트레이서. 없으면 null.</returns>
        public TracerProjectile BeginServerProjectile(UnitType attackerType, Vector3 start)
        {
            EffectPreset preset = _unitConfig?.GetTracer(attackerType);
            GameObject prefab = preset != null ? preset.VfxPrefab : null;
            if (prefab == null) return null;
            TracerProjectile tracer = GetOrCreateTracer(prefab);
            tracer.BeginDriven(start);
            return tracer;
        }

        /// <summary>외부 구동 트레이서를 거둔다(null·이미 파괴된 경우 무시).</summary>
        /// <param name="tracer">BeginServerProjectile이 돌려준 트레이서.</param>
        public void EndServerProjectile(TracerProjectile tracer)
        {
            if (tracer != null) tracer.EndDriven();
        }

        /// <summary>
        /// 서버 착탄 지점에서 쏜 유닛의 착탄 이펙트(impactPreset)를 1회 재생한다(명중·대상 소실 공통 — Q4).
        /// 착탄 이펙트가 설정되지 않은 유닛이면 아무것도 하지 않고 false.
        /// </summary>
        /// <param name="attackerType">쏜 유닛 종류(착탄 프리셋 선택).</param>
        /// <param name="point">착탄 지점(화면 좌표 — 대상 표면점).</param>
        /// <param name="flightDirection">날아온 방향(수평 회전 계산용).</param>
        /// <returns>재생할 프리셋이 있었으면 true.</returns>
        public bool PlayServerProjectileImpact(UnitType attackerType, Vector3 point, Vector3 flightDirection)
        {
            EffectPreset impact = _unitConfig?.GetImpact(attackerType);
            if (impact == null || impact.VfxPrefab == null) return false;
            // 높이 차이는 무시하고 땅 위에서 본 방향만 쓴다(R5 착탄 이펙트와 같은 회전 규칙).
            flightDirection.y = 0f;
            Quaternion rotation = flightDirection.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(flightDirection)
                : Quaternion.identity;
            Play(impact, point, rotation);
            // Sound15: 착탄 SFX는 아직 지정되지 않아 추가하지 않는다(PlayAttackEffect · R5와 동일).
            return true;
        }

        // ====================================================================
        // [2026-10-21 서버 권위 투사체 단계 C] 투사체 타워(인간 CannonTower) 화면 표현
        //   ProjectilePresentationPresenter가 서버 발사 기록(건물 공격자)을 꺼낼 때·착탄할 때 부른다.
        //   프리셋은 BuildingEffectConfig에서 고른다: 발사(총구) = _autoTowerAttacks(EffectPreset_HumanAutoTower_Attack),
        //   투사체·착탄 = _autoTowerProjectiles(CannonCart의 EffectPreset_Cannon_Tracer / _Impact 재사용 — Q10 ①).
        //   피해·HP와 무관한 표현 전용이다.
        // ====================================================================

        /// <summary>
        /// [단계 C] 투사체 타워의 서버 투사체 화면 오브젝트(트레이서)를 외부 구동 모드로 꺼낸다.
        /// 트레이서 프리셋이 없으면 null(투사체 없이 착탄 이펙트·숫자만 착탄 순간에 나온다 — 유닛과 같은 규칙).
        /// </summary>
        /// <param name="towerType">타워 건물 종류(AutoTower).</param>
        /// <param name="race">타워 종족.</param>
        /// <param name="start">발사 지점(화면 좌표 — 포구).</param>
        /// <returns>외부 구동 중인 트레이서. 없으면 null.</returns>
        public TracerProjectile BeginServerTowerProjectile(BuildingType towerType, RaceId race, Vector3 start)
        {
            EffectPreset preset = _buildingConfig?.GetTowerTracer(towerType, race);
            GameObject prefab = preset != null ? preset.VfxPrefab : null;
            if (prefab == null) return null;
            TracerProjectile tracer = GetOrCreateTracer(prefab);
            tracer.BeginDriven(start);
            return tracer;
        }

        /// <summary>
        /// [단계 C] 투사체 타워의 "발사 신호"(포구 발사 이펙트)를 포구 위치에서 1회 재생한다.
        /// 사용자 결정 「발사 이펙트도 대포 입구 위치로」 — 위치는 서버가 보낸 포구(발사 기록 Source)를 그대로 쓴다.
        /// 회전은 기존 타워 발사 이펙트(PlayAttackEffect · PlayBuildingAttack)와 같이 "타워 → 대상" 수평 방향이다.
        /// 프리셋의 Local Position Offset은 회전된 정면 기준으로 더해진다(지금 값 0,0,0 — 포구 그 자리).
        /// 타워 발사 SFX는 아직 지정되지 않았다(사운드 규칙 15 — 추가하지 않는다).
        /// </summary>
        /// <param name="towerType">타워 건물 종류.</param>
        /// <param name="race">타워 종족.</param>
        /// <param name="muzzle">포구 위치(화면 좌표).</param>
        /// <param name="towardTarget">포구 → 대상 방향(수평 회전 계산용).</param>
        /// <returns>발사 프리셋이 있어 재생했으면 true.</returns>
        public bool PlayServerTowerLaunchCue(BuildingType towerType, RaceId race, Vector3 muzzle, Vector3 towardTarget)
        {
            EffectPreset preset = _buildingConfig?.GetAttack(towerType, race);
            if (preset == null || preset.VfxPrefab == null) return false;
            towardTarget.y = 0f;
            Quaternion rotation = towardTarget.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(towardTarget)
                : Quaternion.identity;
            Play(preset, muzzle, rotation);
            return true;
        }

        /// <summary>
        /// [단계 C] 투사체 타워의 착탄 이펙트를 서버 착탄 지점(대상 표면)에서 1회 재생한다(명중·대상 소실 공통 — Q4).
        /// </summary>
        /// <param name="towerType">타워 건물 종류.</param>
        /// <param name="race">타워 종족.</param>
        /// <param name="point">착탄 지점(화면 좌표).</param>
        /// <param name="flightDirection">날아온 방향(수평 회전 계산용).</param>
        /// <returns>착탄 프리셋이 있어 재생했으면 true.</returns>
        public bool PlayServerTowerProjectileImpact(BuildingType towerType, RaceId race, Vector3 point, Vector3 flightDirection)
        {
            EffectPreset impact = _buildingConfig?.GetTowerImpact(towerType, race);
            if (impact == null || impact.VfxPrefab == null) return false;
            flightDirection.y = 0f;
            Quaternion rotation = flightDirection.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(flightDirection)
                : Quaternion.identity;
            Play(impact, point, rotation);
            return true;
        }

        // ====================================================================
        // 내부 — 실제 재생 로직
        // ====================================================================

        /// <summary>
        /// 프리셋을 받아 VFX를 재생하는 핵심 메서드.
        /// preset이 null이면 아무것도 하지 않는다 (이펙트 미설정 유닛/건물 대비).
        /// </summary>
        /// <param name="preset">재생할 이펙트 프리셋. null이면 조기 반환.</param>
        /// <param name="pos">재생할 월드 좌표.</param>
        /// <param name="rot">재생할 월드 회전. 방향성 있는 VFX(피스톨러 등)에 사용. 방향 무관 호출은 identity 전달.</param>
        private void Play(EffectPreset preset, Vector3 pos, Quaternion rot)
        {
            // preset이 null이면 이펙트 미설정 유닛/건물이므로 아무것도 하지 않는다.
            if (preset == null) return;

            // ── VFX ── 프리팹이 있으면 Pool에서 꺼내 재생 (개수 제한 없음).
            if (preset.VfxPrefab != null)
            {
                VfxPoolItem item = GetOrCreateVfx(preset.VfxPrefab);
                item.Play(
                    pos,
                    rot,
                    preset.LocalPositionOffset,
                    preset.EulerRotationOffset,
                    preset.ScaleMultiplier,
                    preset.ForwardTravelDistance,
                    preset.ForwardTravelDuration);
            }

            // SOUND_SYSTEM_REFACTOR: SFX 재생은 AudioManager로 이전 완료. 검증 후 삭제 예정.
            //   이제 EffectManager는 VFX 전용. SFX는 호출부에서 AudioManager.Instance?.PlayXxxSfx()로
            //   같은 줄 바로 아래에서 짝지어 호출한다 (GameSystemRules_Sound 규칙 15).
            // // ── SFX ── 클립이 있고 동시 재생 한도 미만일 때만 재생.
            // if (preset.SfxClip != null && _activeSfxCount < _maxConcurrentSfx)
            // {
            //     AudioSource src = GetOrCreateSfx();
            //     src.transform.position = pos;
            //     src.clip = preset.SfxClip;
            //     src.volume = preset.SfxVolume;
            //     src.gameObject.SetActive(true);
            //     src.Play();
            //
            //     _activeSfxCount++;
            //     // 클립 길이 후 자동 반환.
            //     StartCoroutine(ReturnSfxAfterPlay(src, preset.SfxClip.length));
            // }
        }

        // ====================================================================
        // 내부 — VFX Pool 관리
        // ====================================================================

        /// <summary>
        /// 지정 프리팹의 VFX 인스턴스를 Pool에서 꺼내거나, 없으면 새로 만든다.
        /// </summary>
        /// <param name="prefab">원본 VFX 프리팹 (Pool 키).</param>
        /// <returns>재생 준비된 VfxPoolItem.</returns>
        private VfxPoolItem GetOrCreateVfx(GameObject prefab)
        {
            // 이 프리팹 전용 Queue 확보 (없으면 새로 생성).
            if (!_vfxPools.TryGetValue(prefab, out Queue<VfxPoolItem> queue))
            {
                queue = new Queue<VfxPoolItem>();
                _vfxPools.Add(prefab, queue);
            }

            // Queue에 대기 항목이 있으면 재사용, 없으면 새 인스턴스 생성.
            // Unity에서 GameObject가 Destroy되면 C# 참조는 남아있지만
            // Unity의 == 연산자가 null로 평가하므로 반드시 null 체크가 필요하다.
            // (씬 정리, 딜레이 코루틴 재개 등으로 Queue 안에 파괴된 항목이 남을 수 있음)
            while (queue.Count > 0)
            {
                VfxPoolItem item = queue.Dequeue();
                if (item != null)   // 파괴되지 않은 항목만 반환
                    return item;
                // 파괴된 항목은 버리고 다음 항목 시도
            }

            return CreateVfxInstance(prefab);
        }

        /// <summary>
        /// VFX 인스턴스를 프리팹에서 생성하고 VfxPoolItem 컴포넌트를 보장한다.
        /// </summary>
        /// <param name="prefab">원본 VFX 프리팹.</param>
        /// <returns>생성된 VfxPoolItem (비활성 상태).</returns>
        private VfxPoolItem CreateVfxInstance(GameObject prefab)
        {
            GameObject go = Instantiate(prefab, _vfxContainer);

            // 프리팹에 VfxPoolItem이 없으면 자동 부착 (프리팹 사전 준비 불필요).
            VfxPoolItem item = go.GetComponent<VfxPoolItem>();
            if (item == null)
                item = go.AddComponent<VfxPoolItem>();

            // 재생 완료 시 OnVfxReturn으로 돌아오도록 콜백 연결.
            item.Setup(prefab, OnVfxReturn);

            go.SetActive(false);
            return item;
        }

        /// <summary>
        /// VfxPoolItem이 재생을 마치고 호출하는 반환 콜백.
        /// 해당 원본 프리팹의 Queue에 다시 넣어 재사용 가능 상태로 만든다.
        /// </summary>
        /// <param name="item">반환되는 VfxPoolItem.</param>
        private void OnVfxReturn(VfxPoolItem item)
        {
            if (item == null || item.SourcePrefab == null) return;

            // [R2-1] 풀로 돌아온 아이템은 더 이상 어떤 시전자에게도 귀속돼 있지 않다(자연 종료든 강제 정지든).
            _casterOwnedItems.Remove(item);

            if (!_vfxPools.TryGetValue(item.SourcePrefab, out Queue<VfxPoolItem> queue))
            {
                // 비정상: 키가 사라진 경우 새 Queue를 만들어 안전하게 보관.
                queue = new Queue<VfxPoolItem>();
                _vfxPools.Add(item.SourcePrefab, queue);
            }

            queue.Enqueue(item);
        }

        // ====================================================================
        // 내부 — 트레이서(발사체) Pool 관리 (VFX Pool과 동일 패턴)
        // ====================================================================

        /// <summary>
        /// 지정 프리팹의 트레이서 인스턴스를 Pool에서 꺼내거나, 없으면 새로 만든다.
        /// </summary>
        /// <param name="prefab">원본 트레이서 프리팹 (Pool 키).</param>
        /// <returns>발사 준비된 TracerProjectile.</returns>
        private TracerProjectile GetOrCreateTracer(GameObject prefab)
        {
            // 이 프리팹 전용 Queue 확보 (없으면 새로 생성).
            if (!_tracerPools.TryGetValue(prefab, out Queue<TracerProjectile> queue))
            {
                queue = new Queue<TracerProjectile>();
                _tracerPools.Add(prefab, queue);
            }

            // Queue에 대기 항목이 있으면 재사용, 없으면 새 인스턴스 생성.
            // Unity의 == 연산자는 Destroy된 GameObject를 null로 평가하므로 반드시 null 체크한다.
            // (씬 정리 등으로 Queue 안에 파괴된 항목이 남을 수 있음)
            while (queue.Count > 0)
            {
                TracerProjectile item = queue.Dequeue();
                if (item != null)   // 파괴되지 않은 항목만 반환
                    return item;
                // 파괴된 항목은 버리고 다음 항목 시도
            }

            return CreateTracerInstance(prefab);
        }

        /// <summary>
        /// 트레이서 인스턴스를 프리팹에서 생성하고 TracerProjectile 컴포넌트를 보장한다.
        /// </summary>
        /// <param name="prefab">원본 트레이서 프리팹.</param>
        /// <returns>생성된 TracerProjectile (비활성 상태).</returns>
        private TracerProjectile CreateTracerInstance(GameObject prefab)
        {
            GameObject go = Instantiate(prefab, _vfxContainer);

            // 프리팹에 TracerProjectile이 없으면 자동 부착 (프리팹 사전 준비 불필요).
            // 단, 비행 속도(_speed)를 프리팹별로 조정하려면 프리팹에 미리 붙여 값을 지정한다.
            TracerProjectile item = go.GetComponent<TracerProjectile>();
            if (item == null)
                item = go.AddComponent<TracerProjectile>();

            // 착탄 후 OnTracerReturn으로 돌아오도록 콜백 연결.
            item.Setup(prefab, OnTracerReturn);

            go.SetActive(false);
            return item;
        }

        /// <summary>
        /// TracerProjectile이 착탄을 마치고 호출하는 반환 콜백.
        /// 해당 원본 프리팹의 Queue에 다시 넣어 재사용 가능 상태로 만든다.
        /// </summary>
        /// <param name="item">반환되는 TracerProjectile.</param>
        private void OnTracerReturn(TracerProjectile item)
        {
            if (item == null || item.SourcePrefab == null) return;

            if (!_tracerPools.TryGetValue(item.SourcePrefab, out Queue<TracerProjectile> queue))
            {
                // 비정상: 키가 사라진 경우 새 Queue를 만들어 안전하게 보관.
                queue = new Queue<TracerProjectile>();
                _tracerPools.Add(item.SourcePrefab, queue);
            }

            queue.Enqueue(item);
        }

        // ====================================================================
        // SOUND_SYSTEM_REFACTOR: 아래 SFX Pool 관리 코드는 AudioManager로 이전 완료.
        //   검증(실기 테스트) 통과 후 이 주석 블록 전체를 최종 삭제할 예정.
        //   (WORKFLOW.md "기존 로직 제거" 규칙 — 검증 전까지 비활성화 유지)
        // ====================================================================

        // /// <summary>
        // /// SFX용 AudioSource를 Pool에서 꺼내거나, 없으면 새로 만든다.
        // /// </summary>
        // /// <returns>재생 준비된 AudioSource.</returns>
        // private AudioSource GetOrCreateSfx()
        // {
        //     if (_sfxPool.Count > 0)
        //         return _sfxPool.Dequeue();
        //
        //     return CreateSfxSource();
        // }
        //
        // /// <summary>
        // /// SFX용 AudioSource를 담은 GameObject를 새로 생성한다.
        // /// 2D 사운드(공간감 없음)로 설정하여 위치와 무관하게 일정하게 들리도록 한다.
        // /// </summary>
        // /// <returns>생성된 AudioSource.</returns>
        // private AudioSource CreateSfxSource()
        // {
        //     GameObject go = new GameObject("SfxSource");
        //     go.transform.SetParent(_sfxContainer, false);
        //
        //     AudioSource src = go.AddComponent<AudioSource>();
        //     src.playOnAwake = false;
        //     // spatialBlend 0 = 완전 2D. UI/전투 효과음은 거리감 없이 일정하게 들려야 자연스럽다.
        //     src.spatialBlend = 0f;
        //     return src;
        // }
        //
        // /// <summary>
        // /// 클립 재생이 끝난 뒤 AudioSource를 Pool에 반환하는 코루틴.
        // /// 정확한 종료 시점을 잡기 어려우므로 클립 길이 + 0.1초 여유를 둔다.
        // /// </summary>
        // /// <param name="src">반환할 AudioSource.</param>
        // /// <param name="duration">클립 길이(초).</param>
        // private IEnumerator ReturnSfxAfterPlay(AudioSource src, float duration)
        // {
        //     yield return new WaitForSeconds(duration + 0.1f);
        //
        //     _activeSfxCount--;
        //     if (_activeSfxCount < 0) _activeSfxCount = 0; // 방어적 하한 보정.
        //
        //     if (src != null)
        //     {
        //         src.Stop();
        //         src.clip = null;
        //         src.gameObject.SetActive(false);
        //         _sfxPool.Enqueue(src);
        //     }
        // }
    }
}
