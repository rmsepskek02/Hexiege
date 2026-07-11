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
// Presentation 레이어 — Unity 의존 (MonoBehaviour, Coroutine, AudioSource).
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;
using Hexiege.Application; // [TIMING-LOG] CombatTimingLog 참조용 — 검증 완료 후 제거

namespace Hexiege.Presentation
{
    /// <summary>
    /// 게임 전체 VFX/SFX를 통합 관리하는 매니저.
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

        // [TIMING-LOG] 피격 VFX 프리셋 미연결 WARN을 타입당 1회만 남기기 위한 중복 방지 집합.
        //   검증 완료 후 이 필드와 PlayUnitHit 내 사용부를 함께 제거([TIMING-LOG] 마커 일괄 삭제).
        private static readonly HashSet<UnitType> _timingLogMissingHitTypes = new HashSet<UnitType>();

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
        }

        // ====================================================================
        // 초기화 — GameBootstrapper에서 호출
        // ====================================================================

        /// <summary>
        /// 매니저 초기화. Config 연결 + 각 Config의 Dictionary 빌드 + SFX Pool 사전 생성.
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
        public void PlayUnitAttack(UnitType type, Vector3 pos, Quaternion rot)
        {
            Play(_unitConfig?.GetAttack(type), pos, rot);
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
            // [TIMING-LOG] 피격 VFX 프리셋 미연결 감지 — 타입당 1회만 WARN. 검증 완료 후 제거([TIMING-LOG] 마커 일괄 삭제)
            EffectPreset hitPreset = _unitConfig?.GetHit(type);
            if (CombatTimingLog.Enabled && hitPreset == null && _timingLogMissingHitTypes.Add(type))
                CombatTimingLog.Warn("Combat/EffectManager", $"피격 VFX 프리셋 미연결 | type={type}");

            // 피격 VFX는 방향성이 없으므로 회전 없이(identity) 재생.
            Play(hitPreset, pos, Quaternion.identity);

            // 피격 SFX 없음 — SoundConfig에 피격 SFX 엔트리가 아직 없어 이번 작업에서 추가하지 않는다.
            //   추후 피격 효과음 에셋 확보 시, 이 줄 바로 아래에 AudioManager.Instance?.PlayUnitHitSfx(type)
            //   짝을 추가한다 (GameSystemRules_Sound 규칙 15 — VFX+SFX 쌍).
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

        /// <summary>
        /// UI 이펙트 재생 (주로 SFX). 위치 개념이 없으므로 Vector3.zero에서 재생.
        /// </summary>
        /// <param name="key">재생할 UI 이펙트 키.</param>
        public void PlayUi(UiEffectKey key)
        {
            // UI 이펙트는 화면 좌표 개념이 없으므로 원점에서 회전 없이(identity) 재생 (SFX는 위치/회전 무관).
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
        public void PlayTracer(UnitType type, Vector3 start, Vector3 target, Action onArrive)
        {
            EffectPreset preset = _unitConfig?.GetTracer(type);
            GameObject prefab = preset != null ? preset.VfxPrefab : null;

            // 트레이서 프리팹 미설정 → 비행 연출 없이 즉시 착탄 콜백 실행(폴백).
            //   이 경우 호출부의 피격 연출은 지연 없이 기존처럼 즉시 방출된다.
            if (prefab == null)
            {
                onArrive?.Invoke();
                return;
            }

            // Pool에서 트레이서를 꺼내(또는 새로 만들어) 발사.
            TracerProjectile tracer = GetOrCreateTracer(prefab);
            tracer.Launch(start, target, onArrive);
        }

        // ====================================================================
        // 내부 — 실제 재생 로직
        // ====================================================================

        /// <summary>
        /// 프리셋을 받아 VFX와 SFX를 동시에 재생하는 핵심 메서드.
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
                item.Play(pos, rot);
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
