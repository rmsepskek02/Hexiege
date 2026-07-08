// ============================================================================
// AudioManager.cs
// 게임 전체의 BGM(배경음악)과 SFX(효과음)를 전담하는 매니저.
//
// 역할 (GameSystemRules_Sound 규칙 1):
//   - EffectManager는 VFX(파티클)만 담당하고, 오디오는 전혀 알지 못한다.
//   - BGM과 SFX 전체는 이 AudioManager가 전담한다.
//
// 생명주기 (규칙 2):
//   SingletonMonoBehaviour<AudioManager>를 상속하여 DontDestroyOnLoad가 자동 적용된다.
//   Login.unity 씬의 [Audio] 오브젝트에 컴포넌트로 부착하면
//   Login → Lobby → Game 씬 전반에 걸쳐 단 하나의 인스턴스가 유지된다.
//
// BGM 자동 전환 (규칙 6, 7):
//   - SceneManager.activeSceneChanged 구독: 씬 이름이 "Login"/"Lobby"이면 해당 BGM.
//   - GameEvents.OnGameStarted 구독: 전투 BGM.
//   - GameEvents.OnGameEnd 구독: 게임 종료 BGM (승패 구분 없음 — 규칙 11).
//   - Initialize() 시점에 현재 씬 이름을 직접 확인하여 첫 BGM을 즉시 재생.
//     (activeSceneChanged는 "씬이 바뀔 때"만 발생하므로, 처음 진입한 씬에는 발생하지 않음)
//
// BGM 크로스페이드 (규칙 8):
//   AudioSource A/B 두 채널을 번갈아 사용한다. 새 BGM을 틀 때 현재 채널은 페이드아웃,
//   다른 채널은 페이드인하여 두 곡이 자연스럽게 겹치며 전환된다.
//
// SFX 풀 (규칙 10~13):
//   AudioSource를 미리 만들어 재사용하며, 동시 재생 8개로 제한한다.
//   모든 SFX는 2D(spatialBlend=0)로 고정한다.
//
// 볼륨 (규칙 15~18):
//   Master/BGM/SFX 3채널을 AudioMixer Exposed Parameter로 제어한다.
//   값은 0~1 float을 PlayerPrefs에 저장하고, 재생 시 dB로 변환하여 믹서에 적용한다.
//
// null-safe 호출 (규칙 5):
//   개발 중 Login 씬을 거치지 않고 Game 씬에 직접 진입하면 Instance가 null일 수 있다.
//   따라서 외부 호출은 모두 AudioManager.Instance?.Xxx() 형태로 사용해야 한다.
//
// Presentation 레이어 — Unity 의존 (MonoBehaviour, Coroutine, AudioSource, AudioMixer).
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UniRx;
using Hexiege.Core;          // SingletonMonoBehaviour
using Hexiege.Application;   // GameEvents
using Hexiege.Infrastructure; // SoundConfig
using Hexiege.Domain;        // UnitType, BuildingType

namespace Hexiege.Presentation
{
    // ========================================================================
    // BgmType
    // 재생할 BGM 종류를 식별하는 enum. PlayBgm(BgmType)에 전달한다.
    // ========================================================================
    public enum BgmType
    {
        Login,    // 로그인 씬 BGM
        Lobby,    // 로비 씬 BGM
        Battle,   // 전투(게임 진행 중) BGM
        GameEnd,  // 게임 종료 BGM (승패 구분 없음)
    }

    // ========================================================================
    // UiSoundEntry
    // UI 효과음 하나(UiEffectKey)에 대응하는 클립과 볼륨.
    // GameSystemRules_Sound 규칙 4: UiEffectKey는 Presentation 타입이므로
    // SoundConfig(Infrastructure)가 아닌 AudioManager가 직접 보유한다.
    // ========================================================================
    [System.Serializable]
    public struct UiSoundEntry
    {
        [Tooltip("대상 UI 이펙트 키")]
        public UiEffectKey key;

        [Tooltip("재생할 UI 효과음 클립. 없으면 비워둠")]
        public AudioClip clip;

        [Tooltip("UI 효과음 기준 볼륨 (0~1)")]
        [Range(0f, 1f)]
        public float volume;
    }

    /// <summary>
    /// 게임 전체 BGM/SFX를 전담하는 매니저.
    /// SingletonMonoBehaviour 상속 → DontDestroyOnLoad로 씬 전반에 유지된다.
    /// </summary>
    public class AudioManager : SingletonMonoBehaviour<AudioManager>
    {
        // ====================================================================
        // 씬 이름 상수 — 씬 이름이 바뀌면 이 한 곳만 수정하면 된다.
        // ====================================================================

        private const string SceneLogin = "Login";
        private const string SceneLobby = "Lobby";

        // ====================================================================
        // PlayerPrefs 키 — 볼륨 저장/로드에 사용 (규칙 21).
        // ====================================================================

        private const string PrefMasterVolume = "MasterVolume";
        private const string PrefBgmVolume = "BGMVolume";
        private const string PrefSfxVolume = "SFXVolume";

        // 뮤트(전체 음소거) 상태 저장 키 (규칙 25). 1 = 뮤트, 0 = 언뮤트.
        //   볼륨 3종 키와 별개로, "소리를 껐다 켜는" 상태만 따로 저장한다.
        private const string PrefIsMuted = "IsMuted";

        // ====================================================================
        // AudioMixer Exposed Parameter 이름 — 믹서에서 노출한 파라미터와 일치해야 함 (규칙 18).
        // ====================================================================

        private const string ParamMasterVolume = "MasterVolume";
        private const string ParamBgmVolume = "BGMVolume";
        private const string ParamSfxVolume = "SFXVolume";

        // ====================================================================
        // 뮤트 상수 (규칙 24)
        // ====================================================================

        // 뮤트 시 마스터 채널에 직접 적용할 dB 값.
        //   -80dB는 사실상 무음이다. 볼륨 값(0~1)을 dB로 변환하는 ApplyVolume을 거치지 않고
        //   이 값을 마스터 Exposed Parameter에 직접 써서 전체 출력만 껐다 켠다.
        private const float MuteDb = -80f;

        // ====================================================================
        // Inspector 설정
        // ====================================================================

        [Header("Mixer")]
        [Tooltip("Master/BGM/SFX 볼륨을 제어하는 AudioMixer 에셋.")]
        [SerializeField] private AudioMixer _audioMixer;

        [Tooltip("BGM AudioSource를 연결할 믹서 그룹.")]
        [SerializeField] private AudioMixerGroup _bgmGroup;

        [Tooltip("SFX AudioSource를 연결할 믹서 그룹.")]
        [SerializeField] private AudioMixerGroup _sfxGroup;

        [Header("BGM")]
        [Tooltip("크로스페이드 채널 A. 빈 자식 오브젝트에 AudioSource 부착 후 연결.")]
        [SerializeField] private AudioSource _bgmSourceA;

        [Tooltip("크로스페이드 채널 B. 빈 자식 오브젝트에 AudioSource 부착 후 연결.")]
        [SerializeField] private AudioSource _bgmSourceB;

        [Header("SFX")]
        [Tooltip("SFX용 AudioSource 오브젝트들의 부모 컨테이너. 빈 자식 오브젝트 연결.")]
        [SerializeField] private Transform _sfxContainer;

        [Tooltip("동시에 재생 가능한 최대 SFX 개수. 초과 시 새 SFX는 무시됨 (규칙 13).")]
        [SerializeField] private int _maxConcurrentSfx = 8;

        [Header("UI SFX")]
        [Tooltip("UI 이펙트 키별 효과음 매핑 (규칙 4). Inspector에서 편집")]
        [SerializeField] private List<UiSoundEntry> _uiSoundEntries = new List<UiSoundEntry>();

        // ====================================================================
        // 런타임 상태
        // ====================================================================

        /// <summary> BGM/SFX 클립 데이터를 담은 Config. Initialize()에서 주입. </summary>
        private SoundConfig _config;

        /// <summary> UI 효과음 빠른 조회용 캐시. Initialize()에서 빌드. </summary>
        private Dictionary<UiEffectKey, UiSoundEntry> _uiCache;

        /// <summary>
        /// 현재 "활성"으로 사용 중인 BGM AudioSource (현재 곡을 재생 중인 쪽).
        /// 크로스페이드 시 이 채널은 페이드아웃되고, 반대 채널이 페이드인된다.
        /// 전환이 끝나면 _activeBgmSource를 반대 채널로 교체한다.
        /// </summary>
        private AudioSource _activeBgmSource;

        /// <summary> 진행 중인 크로스페이드 코루틴. 새 전환 시 StopCoroutine 후 재시작. </summary>
        private Coroutine _crossfadeRoutine;

        /// <summary> SFX용 AudioSource 재사용 풀. </summary>
        private readonly Queue<AudioSource> _sfxPool = new Queue<AudioSource>();

        /// <summary> 현재 재생 중인 SFX 개수. _maxConcurrentSfx와 비교하여 제한. </summary>
        private int _activeSfxCount = 0;

        /// <summary> UniRx 구독 묶음. OnDestroy에서 일괄 해제. </summary>
        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        /// <summary>
        /// 현재 뮤트(전체 음소거) 상태 (규칙 23 — 상태 소유 주체는 AudioManager).
        /// true면 마스터 출력이 -80dB로 꺼진 상태이며, 채널별 볼륨 값은 그대로 보존된다.
        /// UI는 이 값을 IsMuted()로 조회하여 슬라이더 색·토글 버튼 표출을 결정한다.
        /// </summary>
        private bool _isMuted;

        // ====================================================================
        // Unity 생명주기
        // ====================================================================

        /// <summary>
        /// 싱글톤 등록 + 씬/게임 이벤트 구독.
        /// base.Awake()가 DontDestroyOnLoad와 중복 인스턴스 제거를 처리한다.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            // 중복 인스턴스로 파괴되는 경우 Instance가 자기 자신이 아니므로 구독하지 않는다.
            if (Instance != this)
                return;

            // 씬 전환 시 BGM 자동 전환 (규칙 6).
            //   activeSceneChanged는 UniRx가 아닌 Unity의 C# 이벤트이므로 직접 +=/-= 로 관리한다.
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            // 게임 시작 → 전투 BGM (규칙 6, 9).
            GameEvents.OnGameStarted
                .Subscribe(_ => PlayBgm(BgmType.Battle))
                .AddTo(_disposables);

            // 게임 종료 → 게임 종료 BGM (규칙 6, 11 — 승패 구분 없음).
            GameEvents.OnGameEnd
                .Subscribe(_ => PlayBgm(BgmType.GameEnd))
                .AddTo(_disposables);
        }

        /// <summary>
        /// 파괴 시 구독 해제.
        /// SceneManager 이벤트와 UniRx 구독을 모두 정리하여 파괴된 객체 참조를 막는다.
        /// </summary>
        protected override void OnDestroy()
        {
            // 중복 인스턴스가 아닌 진짜 인스턴스일 때만 구독을 정리한다.
            if (Instance == this)
            {
                SceneManager.activeSceneChanged -= OnActiveSceneChanged;
                _disposables.Dispose();
            }

            base.OnDestroy();
        }

        // ====================================================================
        // 초기화 — LoginBootstrapper에서 호출
        // ====================================================================

        /// <summary>
        /// 매니저 초기화. Config 연결 + 믹서 그룹 배선 + 볼륨 로드 + 현재 씬 BGM 즉시 재생.
        /// LoginBootstrapper.Start()에서 호출.
        /// </summary>
        /// <param name="config">BGM/SFX 클립 데이터 (null 허용 — null이면 사운드 없이 동작).</param>
        public void Initialize(SoundConfig config)
        {
            _config = config;
            _config?.Initialize();

            // UI 효과음 캐시 빌드.
            BuildUiCache();

            // 믹서 그룹 미연결 경고 (규칙 17).
            if (_bgmGroup == null)
                Debug.LogWarning("[AudioManager] BGM AudioMixerGroup이 연결되지 않았습니다. BGM 볼륨 제어가 동작하지 않습니다.");
            if (_sfxGroup == null)
                Debug.LogWarning("[AudioManager] SFX AudioMixerGroup이 연결되지 않았습니다. SFX 볼륨 제어가 동작하지 않습니다.");

            // BGM AudioSource A/B에 믹서 그룹 연결 (규칙 17).
            SetupBgmSource(_bgmSourceA);
            SetupBgmSource(_bgmSourceB);

            // 처음 활성 BGM 채널은 A로 시작.
            _activeBgmSource = _bgmSourceA;

            // 재호출 시 기존 풀 정리 — Initialize()를 다시 호출하더라도 AudioSource가 중복 생성되지 않도록
            // 기존 풀에 남아있는 오브젝트를 모두 파괴하고 카운터를 리셋한다.
            while (_sfxPool.Count > 0)
            {
                AudioSource existingSrc = _sfxPool.Dequeue();
                if (existingSrc != null)
                    Destroy(existingSrc.gameObject);
            }
            _activeSfxCount = 0;

            // SFX 풀 사전 생성 — 동시 재생 한도만큼 AudioSource를 미리 만들어 둔다.
            for (int i = 0; i < _maxConcurrentSfx; i++)
            {
                AudioSource src = CreateSfxSource();
                src.gameObject.SetActive(false);
                _sfxPool.Enqueue(src);
            }

            // 저장된 볼륨 로드 후 믹서에 적용 (규칙 20, 21).
            ApplyVolume(ParamMasterVolume, LoadVolume(PrefMasterVolume));
            ApplyVolume(ParamBgmVolume, LoadVolume(PrefBgmVolume));
            ApplyVolume(ParamSfxVolume, LoadVolume(PrefSfxVolume));

            // 저장된 뮤트 상태 복원 (규칙 25).
            //   반드시 위의 볼륨 3종 적용 "뒤"에 수행해야 한다. 볼륨을 나중에 적용하면
            //   마스터가 정상 볼륨으로 덮어써져 언뮤트처럼 보이는 버그가 생기기 때문이다.
            //   저장값이 없으면 기본 0(언뮤트)이다.
            int savedMuted = PlayerPrefs.GetInt(PrefIsMuted, 0);
            if (savedMuted == 1)
            {
                _isMuted = true;
                // 마스터 출력만 -80dB로 직접 적용 (볼륨 값 3종은 건드리지 않음 — 규칙 24).
                if (_audioMixer != null)
                    _audioMixer.SetFloat(ParamMasterVolume, MuteDb);
            }

            // 현재 씬 BGM 즉시 재생 (규칙 7).
            //   activeSceneChanged는 처음 진입한 씬에는 발생하지 않으므로 여기서 직접 처리.
            PlayBgmForScene(SceneManager.GetActiveScene().name);
        }

        /// <summary> UI 효과음 List를 Dictionary로 변환하여 O(1) 조회를 준비. </summary>
        private void BuildUiCache()
        {
            _uiCache = new Dictionary<UiEffectKey, UiSoundEntry>(_uiSoundEntries.Count);
            foreach (UiSoundEntry entry in _uiSoundEntries)
            {
                if (!_uiCache.ContainsKey(entry.key))
                    _uiCache.Add(entry.key, entry);
            }
        }

        /// <summary> BGM AudioSource 한 채널에 믹서 그룹과 루프 설정을 적용. </summary>
        /// <param name="src">설정할 BGM AudioSource (null이면 무시).</param>
        private void SetupBgmSource(AudioSource src)
        {
            if (src == null) return;
            src.outputAudioMixerGroup = _bgmGroup; // 규칙 17
            src.playOnAwake = false;
            src.loop = true;       // BGM은 반복 재생.
            src.spatialBlend = 0f; // BGM도 2D로 고정.
        }

        // ====================================================================
        // BGM 외부 API
        // ====================================================================

        /// <summary>
        /// 지정한 BGM 종류로 크로스페이드 전환한다.
        /// 클립이 null이면 무음으로 전환한다 (이전 BGM을 유지하지 않음 — 규칙 10).
        /// </summary>
        /// <param name="type">재생할 BGM 종류.</param>
        public void PlayBgm(BgmType type)
        {
            // Config가 없으면 재생할 클립을 알 수 없으므로 조용히 무시.
            if (_config == null) return;

            AudioClip clip = type switch
            {
                BgmType.Login => _config.LoginBgm,
                BgmType.Lobby => _config.LobbyBgm,
                BgmType.Battle => _config.BattleBgm,
                BgmType.GameEnd => _config.GameEndBgm,
                _ => null,
            };

            StartCrossfade(clip);
        }

        /// <summary>
        /// 씬 이름에 해당하는 BGM을 재생한다.
        /// "Login"/"Lobby"만 매핑되며, 그 외 씬(예: Game)은 BGM을 바꾸지 않는다.
        /// (Game 씬은 OnGameStarted 이벤트 시점에 Battle BGM으로 전환 — 규칙 9)
        /// </summary>
        /// <param name="sceneName">현재 활성 씬 이름.</param>
        private void PlayBgmForScene(string sceneName)
        {
            if (sceneName == SceneLogin)
                PlayBgm(BgmType.Login);
            else if (sceneName == SceneLobby)
                PlayBgm(BgmType.Lobby);
            // Game 씬 등은 여기서 처리하지 않음 (OnGameStarted가 Battle BGM 담당).
        }

        /// <summary> 씬 전환 시 호출되는 콜백. 새 씬의 BGM으로 전환. </summary>
        /// <param name="previous">이전 씬.</param>
        /// <param name="next">전환된 새 씬.</param>
        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            PlayBgmForScene(next.name);
        }

        // ====================================================================
        // BGM 크로스페이드 (규칙 8)
        // ====================================================================

        /// <summary>
        /// 새 클립으로 크로스페이드를 시작한다.
        /// 진행 중인 크로스페이드가 있으면 중단하고 즉시 새로 시작한다 (규칙 8).
        /// </summary>
        /// <param name="newClip">전환할 BGM 클립. null이면 무음으로 페이드아웃.</param>
        private void StartCrossfade(AudioClip newClip)
        {
            if (_bgmSourceA == null || _bgmSourceB == null) return;

            // 이미 같은 클립을 재생 중이면 중복 전환을 막아 끊김을 방지.
            if (_activeBgmSource != null && _activeBgmSource.clip == newClip && _activeBgmSource.isPlaying)
                return;

            // 진행 중인 크로스페이드가 있으면 중단.
            //   StopCoroutine은 코루틴 실행만 멈출 뿐, 페이드아웃 도중이던 AudioSource의
            //   재생은 멈추지 않는다. 따라서 코루틴을 멈춘 직후 "활성 채널이 아닌 쪽"
            //   (= 이전 전환에서 페이드아웃 중이던 채널)을 직접 정지시켜야
            //   이전 BGM이 새 BGM과 겹쳐 계속 재생되는 문제를 막을 수 있다 (규칙 8).
            if (_crossfadeRoutine != null)
            {
                StopCoroutine(_crossfadeRoutine);

                // 활성 채널(_activeBgmSource)이 아닌 반대 채널이 페이드아웃 중이던 채널이다.
                AudioSource staleSource = (_activeBgmSource == _bgmSourceA) ? _bgmSourceB : _bgmSourceA;
                if (staleSource != null)
                {
                    staleSource.volume = 0f;
                    staleSource.Stop();
                    staleSource.clip = null;
                }

                _crossfadeRoutine = null;
            }

            _crossfadeRoutine = StartCoroutine(CrossfadeRoutine(newClip));
        }

        /// <summary>
        /// 현재 채널을 페이드아웃하고 반대 채널을 페이드인하여 BGM을 전환하는 코루틴.
        /// 전환이 끝나면 _activeBgmSource를 반대 채널로 교체한다.
        /// </summary>
        /// <param name="newClip">전환할 클립. null이면 새 채널 재생 없이 페이드아웃만.</param>
        private IEnumerator CrossfadeRoutine(AudioClip newClip)
        {
            float duration = _config != null ? _config.BgmCrossfadeDuration : 1.0f;

            // 페이드아웃 대상 = 현재 활성 채널, 페이드인 대상 = 반대 채널.
            AudioSource fadeOut = _activeBgmSource;
            AudioSource fadeIn = (_activeBgmSource == _bgmSourceA) ? _bgmSourceB : _bgmSourceA;

            // 새 클립이 있으면 페이드인 채널에서 0 볼륨으로 재생 시작.
            if (newClip != null)
            {
                fadeIn.clip = newClip;
                fadeIn.volume = 0f;
                fadeIn.Play();
            }

            float fadeOutStartVolume = fadeOut != null ? fadeOut.volume : 0f;

            // duration이 0 이하이면 즉시 전환 (0으로 나누기 방지).
            if (duration <= 0f)
            {
                if (fadeOut != null) { fadeOut.volume = 0f; fadeOut.Stop(); fadeOut.clip = null; }
                if (newClip != null) fadeIn.volume = 1f;
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    // BGM은 게임 일시정지(timeScale=0)와 무관하게 진행되어야 하므로 unscaledDeltaTime 사용.
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);

                    if (fadeOut != null)
                        fadeOut.volume = Mathf.Lerp(fadeOutStartVolume, 0f, t);
                    if (newClip != null)
                        fadeIn.volume = Mathf.Lerp(0f, 1f, t);

                    yield return null;
                }

                // 페이드아웃 채널 정리.
                if (fadeOut != null)
                {
                    fadeOut.volume = 0f;
                    fadeOut.Stop();
                    fadeOut.clip = null;
                }
                if (newClip != null)
                    fadeIn.volume = 1f;
            }

            // 활성 채널 교체 — 항상 fadeIn 채널로 교체한다.
            // 무음 전환(newClip == null)일 때도 교체하여, 다음 BGM 재생 시
            // fadeOut 대상이 "이미 멈춘 채널"이 아닌 명확한 대기 채널이 되도록 한다.
            _activeBgmSource = fadeIn;

            _crossfadeRoutine = null;
        }

        // ====================================================================
        // SFX 외부 API
        // ====================================================================

        /// <summary> 유닛 공격 효과음 재생. UnitView.OnAttackHit()에서 VFX와 짝으로 호출. </summary>
        /// <param name="type">공격한 유닛 타입.</param>
        public void PlayUnitAttackSfx(UnitType type)
        {
            if (_config == null) return;
            PlaySfxClip(_config.GetUnitAttackClip(type), _config.GetUnitAttackVolume(type));
        }

        /// <summary> 유닛 사망 효과음 재생. UnitView/NetworkUnit 사망 처리에서 VFX와 짝으로 호출. </summary>
        /// <param name="type">사망한 유닛 타입.</param>
        public void PlayUnitDeathSfx(UnitType type)
        {
            if (_config == null) return;
            PlaySfxClip(_config.GetUnitDeathClip(type), _config.GetUnitDeathVolume(type));
        }

        /// <summary> 건물 배치 효과음 재생. </summary>
        /// <param name="type">배치된 건물 타입.</param>
        public void PlayBuildingPlaceSfx(BuildingType type)
        {
            if (_config == null) return;
            PlaySfxClip(_config.GetBuildingPlaceClip(type), _config.GetBuildingPlaceVolume(type));
        }

        /// <summary> 건물 파괴 효과음 재생. </summary>
        /// <param name="type">파괴된 건물 타입.</param>
        public void PlayBuildingDestroySfx(BuildingType type)
        {
            if (_config == null) return;
            PlaySfxClip(_config.GetBuildingDestroyClip(type), _config.GetBuildingDestroyVolume(type));
        }

        /// <summary> 건물 업그레이드 효과음 재생. </summary>
        /// <param name="type">업그레이드된 건물 타입.</param>
        public void PlayBuildingUpgradeSfx(BuildingType type)
        {
            if (_config == null) return;
            PlaySfxClip(_config.GetBuildingUpgradeClip(type), _config.GetBuildingUpgradeVolume(type));
        }

        /// <summary> UI 효과음 재생 (규칙 4 — AudioManager가 직접 보유한 매핑 사용). </summary>
        /// <param name="key">재생할 UI 이펙트 키.</param>
        public void PlayUiSfx(UiEffectKey key)
        {
            if (_uiCache == null) return;
            if (_uiCache.TryGetValue(key, out UiSoundEntry entry))
                PlaySfxClip(entry.clip, entry.volume);
        }

        /// <summary>
        /// 임의의 클립을 SFX 채널로 직접 재생한다 (확장성용 진입점).
        /// 클립이 null이거나 동시 재생 한도를 초과하면 무시한다 (규칙 13).
        /// </summary>
        /// <param name="clip">재생할 클립.</param>
        /// <param name="volume">클립별 기준 볼륨 (0~1). 기본 1.0 (규칙 16).</param>
        public void PlaySfxClip(AudioClip clip, float volume = 1f)
        {
            // 클립이 없거나 동시 재생 한도 초과 시 무시.
            if (clip == null || _activeSfxCount >= _maxConcurrentSfx) return;

            AudioSource src = GetOrCreateSfx();
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.gameObject.SetActive(true);
            src.Play();

            _activeSfxCount++;
            // 클립 길이 후 자동 반환.
            StartCoroutine(ReturnSfxAfterPlay(src, clip.length));
        }

        // ====================================================================
        // SFX 풀 관리
        // ====================================================================

        /// <summary> SFX용 AudioSource를 풀에서 꺼내거나 없으면 새로 만든다. </summary>
        /// <returns>재생 준비된 AudioSource.</returns>
        private AudioSource GetOrCreateSfx()
        {
            if (_sfxPool.Count > 0)
                return _sfxPool.Dequeue();
            return CreateSfxSource();
        }

        /// <summary>
        /// SFX용 AudioSource를 담은 GameObject를 새로 생성한다.
        /// 2D 사운드(공간감 없음)로 설정하고 SFX 믹서 그룹에 연결한다 (규칙 12, 17).
        /// </summary>
        /// <returns>생성된 AudioSource.</returns>
        private AudioSource CreateSfxSource()
        {
            GameObject go = new GameObject("SfxSource");
            go.transform.SetParent(_sfxContainer, false);

            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            // spatialBlend 0 = 완전 2D (규칙 12). 헥스 그리드 부감 시점이라 거리감 불필요.
            src.spatialBlend = 0f;
            // SFX 믹서 그룹 연결 (규칙 17) — 볼륨 제어를 위해 필수.
            src.outputAudioMixerGroup = _sfxGroup;
            return src;
        }

        /// <summary>
        /// 클립 재생이 끝난 뒤 AudioSource를 풀에 반환하는 코루틴.
        /// 정확한 종료 시점을 잡기 어려우므로 클립 길이 + 0.1초 여유를 둔다.
        /// </summary>
        /// <param name="src">반환할 AudioSource.</param>
        /// <param name="duration">클립 길이(초).</param>
        private IEnumerator ReturnSfxAfterPlay(AudioSource src, float duration)
        {
            // WaitForSecondsRealtime 사용 — 싱글플레이 일시정지(timeScale=0) 중에도
            // 클립 재생이 끝나면 정상적으로 반환되어 _activeSfxCount를 풀 수 있도록 한다.
            yield return new WaitForSecondsRealtime(duration + 0.1f);

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

        // ====================================================================
        // 볼륨 외부 API (규칙 18~21)
        // ====================================================================

        /// <summary> 마스터 볼륨 설정. 믹서 적용 + PlayerPrefs 저장. </summary>
        /// <param name="value">0~1 볼륨 값.</param>
        public void SetMasterVolume(float value) => SetVolume(ParamMasterVolume, PrefMasterVolume, value);

        /// <summary> BGM 볼륨 설정. 믹서 적용 + PlayerPrefs 저장. </summary>
        /// <param name="value">0~1 볼륨 값.</param>
        public void SetBgmVolume(float value) => SetVolume(ParamBgmVolume, PrefBgmVolume, value);

        /// <summary> SFX 볼륨 설정. 믹서 적용 + PlayerPrefs 저장. </summary>
        /// <param name="value">0~1 볼륨 값.</param>
        public void SetSfxVolume(float value) => SetVolume(ParamSfxVolume, PrefSfxVolume, value);

        /// <summary> 저장된 마스터 볼륨을 반환. 없으면 1.0 (규칙 20). </summary>
        /// <returns>0~1 볼륨 값.</returns>
        public float GetMasterVolume() => LoadVolume(PrefMasterVolume);

        /// <summary> 저장된 BGM 볼륨을 반환. 없으면 1.0 (규칙 20). </summary>
        /// <returns>0~1 볼륨 값.</returns>
        public float GetBgmVolume() => LoadVolume(PrefBgmVolume);

        /// <summary> 저장된 SFX 볼륨을 반환. 없으면 1.0 (규칙 20). </summary>
        /// <returns>0~1 볼륨 값.</returns>
        public float GetSfxVolume() => LoadVolume(PrefSfxVolume);

        // ====================================================================
        // 뮤트 외부 API (규칙 23~26)
        // ====================================================================

        /// <summary>
        /// 전체 음소거(뮤트) 상태를 전환한다 (규칙 23, 24, 25).
        ///
        /// - muted == true (뮤트): 마스터 출력만 -80dB로 낮춰 전체 소리를 끈다.
        ///   이때 채널별 슬라이더 값과 PlayerPrefs 볼륨 3종은 변경하지 않고 그대로 보존한다.
        /// - muted == false (언뮤트): PlayerPrefs에 저장된 마스터 볼륨을 다시 읽어 복원한다.
        ///
        /// 즉 뮤트/언뮤트는 볼륨 값을 덮어쓰지 않고 "출력만 껐다 켜는" 동작이다.
        /// 상태 변경 후 PlayerPrefs "IsMuted"에 즉시 저장하여 재실행 시에도 유지된다 (규칙 25).
        /// UI는 이 메서드 한 줄만 호출하며 AudioMixer/PlayerPrefs를 직접 만지지 않는다 (규칙 23).
        /// </summary>
        /// <param name="muted">true=음소거, false=음소거 해제.</param>
        public void SetMuted(bool muted)
        {
            if (muted)
            {
                // 마스터 출력만 -80dB로 직접 적용. 볼륨 값(0~1) → dB 변환을 거치지 않는다.
                //   BGM/SFX 채널 파라미터와 PlayerPrefs 볼륨은 그대로 유지되므로,
                //   언뮤트 시 이전 볼륨이 자연히 복원된다 (규칙 24).
                if (_audioMixer != null)
                    _audioMixer.SetFloat(ParamMasterVolume, MuteDb);
            }
            else
            {
                // 저장된 마스터 볼륨을 다시 적용(복원) — 규칙 24.
                ApplyVolume(ParamMasterVolume, LoadVolume(PrefMasterVolume));
            }

            _isMuted = muted;
            PlayerPrefs.SetInt(PrefIsMuted, muted ? 1 : 0);
            PlayerPrefs.Save(); // 규칙 25 — 즉시 저장.
        }

        /// <summary>
        /// 현재 뮤트 상태를 반환한다 (규칙 23).
        /// UI가 슬라이더 색(초록/빨강)·토글 버튼 표출을 판단하는 데 사용한다.
        /// </summary>
        /// <returns>true=음소거 상태, false=언뮤트 상태.</returns>
        public bool IsMuted() => _isMuted;

        // ====================================================================
        // 볼륨 내부 처리
        // ====================================================================

        /// <summary>
        /// 볼륨을 믹서에 적용하고 PlayerPrefs에 저장한다.
        /// </summary>
        /// <param name="param">AudioMixer Exposed Parameter 이름.</param>
        /// <param name="prefKey">PlayerPrefs 저장 키.</param>
        /// <param name="value">0~1 볼륨 값.</param>
        private void SetVolume(string param, string prefKey, float value)
        {
            // 뮤트 상태에서 슬라이더를 조작하면 자동으로 언뮤트한다 (규칙 26).
            //   슬라이더를 움직이는 행위는 "소리를 다시 켜겠다"는 의도로 간주한다.
            //   SetMuted(false)가 마스터 출력을 저장된 볼륨으로 먼저 복원한다. 그 뒤
            //   아래에서 요청된 채널 값을 적용하므로, 마스터를 조작한 경우에도 최종적으로
            //   방금 조작한 값이 올바르게 반영된다.
            if (_isMuted)
                SetMuted(false);

            float clamped = Mathf.Clamp01(value);
            ApplyVolume(param, clamped);

            PlayerPrefs.SetFloat(prefKey, clamped);
            PlayerPrefs.Save(); // 규칙 21 — 즉시 저장.
        }

        /// <summary>
        /// 0~1 볼륨을 dB로 변환하여 AudioMixer에 적용한다 (규칙 19).
        /// </summary>
        /// <param name="param">AudioMixer Exposed Parameter 이름.</param>
        /// <param name="value">0~1 볼륨 값.</param>
        private void ApplyVolume(string param, float value)
        {
            // _audioMixer 미연결 시 볼륨 제어 불가 — 원인 추적을 위해 경고 로그 남김.
            if (_audioMixer == null)
            {
                Debug.LogWarning("[AudioManager] _audioMixer가 null입니다.");
                return;
            }

            // Log10(0) = -무한대 방지를 위해 하한 0.0001을 둔다 (규칙 19).
            float dB = Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f;

            // SetFloat은 Exposed Parameter 이름이 믹서에 없으면 false를 반환한다.
            //   SFX 볼륨이 조절되지 않는 원인이 파라미터 이름 불일치인지 확인하기 위해 결과를 검사한다.
            bool result = _audioMixer.SetFloat(param, dB);
            if (!result)
                Debug.LogWarning($"[AudioManager] AudioMixer.SetFloat 실패: param={param}, dB={dB}");
        }

        /// <summary>
        /// PlayerPrefs에서 볼륨을 읽는다. 저장값이 없으면 기본 1.0 (규칙 20).
        /// </summary>
        /// <param name="prefKey">PlayerPrefs 키.</param>
        /// <returns>0~1 볼륨 값.</returns>
        private float LoadVolume(string prefKey)
        {
            return PlayerPrefs.GetFloat(prefKey, 1f);
        }
    }
}
