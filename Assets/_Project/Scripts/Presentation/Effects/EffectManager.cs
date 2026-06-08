// ============================================================================
// EffectManager.cs
// 게임 전체의 VFX(시각 이펙트)와 SFX(사운드)를 통합 관리하는 핵심 매니저.
//
// 역할:
//   1. VFX Object Pool — 파티클을 미리 만들어 재사용 (개수 제한 없음).
//   2. SFX Object Pool — AudioSource를 재사용 (동시 8개 제한).
//   3. Config(유닛/건물/UI) 참조 — 어떤 상황에 어떤 이펙트를 재생할지 조회.
//   4. 외부 API — PlayUnitAttack/PlayUnitDeath/... 한 줄 호출로 VFX+SFX 동시 재생.
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Hexiege.Domain;

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

        // ====================================================================
        // Inspector 설정
        // ====================================================================

        [Header("References")]
        [Tooltip("재생된 VFX 오브젝트들의 부모 컨테이너. 씬의 빈 GameObject 연결.")]
        [SerializeField] private Transform _vfxContainer;

        [Tooltip("SFX용 AudioSource 오브젝트들의 부모 컨테이너. 씬의 빈 GameObject 연결.")]
        [SerializeField] private Transform _sfxContainer;

        [Header("SFX 제한")]
        [Tooltip("동시에 재생 가능한 최대 SFX 개수. 초과 시 새 SFX는 무시됨.")]
        [SerializeField] private int _maxConcurrentSfx = 8;

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

        /// <summary> SFX Pool — 모든 사운드가 공유하는 AudioSource 큐. </summary>
        private readonly Queue<AudioSource> _sfxPool = new Queue<AudioSource>();

        /// <summary> 현재 재생 중인 SFX 개수. _maxConcurrentSfx와 비교하여 제한. </summary>
        private int _activeSfxCount = 0;

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

            // SFX Pool 사전 생성 — 동시 재생 한도만큼 AudioSource를 미리 만들어 둔다.
            for (int i = 0; i < _maxConcurrentSfx; i++)
            {
                AudioSource src = CreateSfxSource();
                src.gameObject.SetActive(false);
                _sfxPool.Enqueue(src);
            }
        }

        // ====================================================================
        // 외부 API — 상황별 이펙트 재생
        // ====================================================================

        /// <summary>
        /// 유닛 공격 이펙트 재생. UnitView.OnAttackHit()(Animation Event)에서 호출.
        /// </summary>
        /// <param name="type">공격한 유닛의 타입.</param>
        /// <param name="pos">재생할 월드 좌표 (보통 유닛 위치).</param>
        public void PlayUnitAttack(UnitType type, Vector3 pos)
        {
            Play(_unitConfig?.GetAttack(type), pos);
        }

        /// <summary>
        /// 유닛 사망 이펙트 재생. UnitView의 OnUnitDied 처리에서 호출.
        /// </summary>
        /// <param name="type">사망한 유닛의 타입.</param>
        /// <param name="pos">재생할 월드 좌표.</param>
        public void PlayUnitDeath(UnitType type, Vector3 pos)
        {
            Play(_unitConfig?.GetDeath(type), pos);
        }

        /// <summary>
        /// 건물 파괴 이펙트 재생.
        /// </summary>
        /// <param name="type">파괴된 건물의 타입.</param>
        /// <param name="pos">재생할 월드 좌표.</param>
        public void PlayBuildingDestroy(BuildingType type, Vector3 pos)
        {
            Play(_buildingConfig?.GetDestroy(type), pos);
        }

        /// <summary>
        /// 건물 업그레이드 이펙트 재생.
        /// </summary>
        /// <param name="type">업그레이드된 건물의 타입.</param>
        /// <param name="pos">재생할 월드 좌표.</param>
        public void PlayBuildingUpgrade(BuildingType type, Vector3 pos)
        {
            Play(_buildingConfig?.GetUpgrade(type), pos);
        }

        /// <summary>
        /// UI 이펙트 재생 (주로 SFX). 위치 개념이 없으므로 Vector3.zero에서 재생.
        /// </summary>
        /// <param name="key">재생할 UI 이펙트 키.</param>
        public void PlayUi(UiEffectKey key)
        {
            // UI 이펙트는 화면 좌표 개념이 없으므로 원점에서 재생 (SFX는 위치 무관).
            Play(_uiConfig?.Get(key), Vector3.zero);
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
        private void Play(EffectPreset preset, Vector3 pos)
        {
            // preset이 null이면 이펙트 미설정 유닛/건물이므로 아무것도 하지 않는다.
            if (preset == null) return;

            // ── VFX ── 프리팹이 있으면 Pool에서 꺼내 재생 (개수 제한 없음).
            if (preset.VfxPrefab != null)
            {
                VfxPoolItem item = GetOrCreateVfx(preset.VfxPrefab);
                item.Play(pos, Quaternion.identity);
            }

            // ── SFX ── 클립이 있고 동시 재생 한도 미만일 때만 재생.
            if (preset.SfxClip != null && _activeSfxCount < _maxConcurrentSfx)
            {
                AudioSource src = GetOrCreateSfx();
                src.transform.position = pos;
                src.clip = preset.SfxClip;
                src.volume = preset.SfxVolume;
                src.gameObject.SetActive(true);
                src.Play();

                _activeSfxCount++;
                // 클립 길이 후 자동 반환.
                StartCoroutine(ReturnSfxAfterPlay(src, preset.SfxClip.length));
            }
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
        // 내부 — SFX Pool 관리
        // ====================================================================

        /// <summary>
        /// SFX용 AudioSource를 Pool에서 꺼내거나, 없으면 새로 만든다.
        /// </summary>
        /// <returns>재생 준비된 AudioSource.</returns>
        private AudioSource GetOrCreateSfx()
        {
            if (_sfxPool.Count > 0)
                return _sfxPool.Dequeue();

            return CreateSfxSource();
        }

        /// <summary>
        /// SFX용 AudioSource를 담은 GameObject를 새로 생성한다.
        /// 2D 사운드(공간감 없음)로 설정하여 위치와 무관하게 일정하게 들리도록 한다.
        /// </summary>
        /// <returns>생성된 AudioSource.</returns>
        private AudioSource CreateSfxSource()
        {
            GameObject go = new GameObject("SfxSource");
            go.transform.SetParent(_sfxContainer, false);

            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            // spatialBlend 0 = 완전 2D. UI/전투 효과음은 거리감 없이 일정하게 들려야 자연스럽다.
            src.spatialBlend = 0f;
            return src;
        }

        /// <summary>
        /// 클립 재생이 끝난 뒤 AudioSource를 Pool에 반환하는 코루틴.
        /// 정확한 종료 시점을 잡기 어려우므로 클립 길이 + 0.1초 여유를 둔다.
        /// </summary>
        /// <param name="src">반환할 AudioSource.</param>
        /// <param name="duration">클립 길이(초).</param>
        private IEnumerator ReturnSfxAfterPlay(AudioSource src, float duration)
        {
            yield return new WaitForSeconds(duration + 0.1f);

            _activeSfxCount--;
            if (_activeSfxCount < 0) _activeSfxCount = 0; // 방어적 하한 보정.

            if (src != null)
            {
                src.Stop();
                src.clip = null;
                src.gameObject.SetActive(false);
                _sfxPool.Enqueue(src);
            }
        }
    }
}
