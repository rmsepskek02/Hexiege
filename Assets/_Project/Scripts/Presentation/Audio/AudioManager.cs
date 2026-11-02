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
// SFX 풀 (규칙 12~13):
//   AudioSource를 미리 만들어 재사용한다. 모든 SFX는 2D(spatialBlend=0)로 고정한다.
//   (종전 문장 「동시 재생 8개로 제한한다」는 2026-11-02 효과음 task에서 아래 규칙으로 바뀌었다)
//
// SFX 동시 재생 판단 (규칙 13 개정 — 2026-11-02, _Tasks/2026-11-02/13_39_unit-attack-sfx-tiering/Plan.md 6절):
//   재생 요청이 올 때마다 아래 순서로 "재생할지"를 정한다. 판단은 각 화면이 로컬에서만 한다(규칙 14).
//   ① 같은 클립 최소 간격 — 같은 오디오 파일이 직전에 시작한 지 약 0.05초가 안 됐으면 재생하지 않는다.
//   ② 같은 클립 동시 개수 — 같은 오디오 파일이 이미 3개 울리고 있으면 재생하지 않는다.
//      (같은 파일을 여러 유닛이 나눠 쓰면 그 유닛들이 3개를 함께 쓴다 — 의도된 동작)
//   ③ 전체 한도(기본 16개) — 꽉 찼을 때
//        · 「일반」 요청: 재생하지 않는다.
//        · 「우선」 요청(특수 공격·방어 타워 공격음): 울리고 있는 「일반」 효과음 중 가장 먼저 시작한 것을
//          아주 짧게(약 0.03초) 줄여 끄고 그 자리에서 재생한다. 울리는 것이 모두 「우선」이면 재생하지 않는다.
//   우선 효과음도 ①②는 똑같이 받는다(우선은 ③ 전체 한도에서만 앞선다).
//
// 볼륨 (규칙 18~21):
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
    // SfxPriority (2026-11-02 추가 — 효과음 task Plan 6-2 「재생 진입점」)
    // 효과음 하나의 재생 등급. PlaySfxClip(clip, volume, priority)의 세 번째 인자로 넘긴다.
    // 효과음 전체 한도(_maxConcurrentSfx)가 꽉 찼을 때만 차이가 난다.
    //   - 유닛 공격음: SoundConfig 유닛 항목의 「우선 재생」 체크(attackIsPriority)로 정해진다.
    //   - 방어 타워 공격음: 타워 task가 만들 API가 항상 High로 부를 예정이다.
    //   - 그 밖(사망음·건물음·UI 효과음)은 Normal이다.
    // ========================================================================

    /// <summary>
    /// 효과음 재생 등급. 효과음 전체 한도가 찼을 때 어떻게 처리할지를 정한다.
    /// </summary>
    public enum SfxPriority
    {
        /// <summary> 일반 — 전체 한도가 차 있으면 재생하지 않는다. 우선 효과음이 오면 먼저 끊길 수 있다. </summary>
        Normal = 0,

        /// <summary> 우선 — 전체 한도가 차 있으면 가장 먼저 시작한 일반 효과음을 짧게 줄여 끄고 재생한다. </summary>
        High = 1,
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

        // 음소거 여부 저장 키. 0=소리 켜짐, 1=음소거 (규칙 27 — 음소거 플래그의 영속화 조항).
        //   슬라이더 볼륨값(0~1)과 달리 정수(0/1)로 저장한다.
        private const string PrefMuted = "Muted";

        // ====================================================================
        // 음소거 무음 dB 값
        //   음소거는 "저장된 볼륨값(PlayerPrefs)은 그대로 두고, 실제 출력만 무음으로
        //   강제"하는 방식이다(규칙 27 본문 — 저장값 보존형). 이를 위해 Master 채널의
        //   AudioMixer 출력을 -80dB(사실상 완전 무음)로 눌러버린다.
        //   Master 채널 하나가 전체 출력을 결정하므로, Master만 -80dB로 만들면
        //   BGM/SFX의 논리 볼륨을 건드리지 않고도 전체가 무음이 된다.
        // ====================================================================

        private const float MutedDb = -80f;

        // ====================================================================
        // AudioMixer Exposed Parameter 이름 — 믹서에서 노출한 파라미터와 일치해야 함 (규칙 18).
        // ====================================================================

        private const string ParamMasterVolume = "MasterVolume";
        private const string ParamBgmVolume = "BGMVolume";
        private const string ParamSfxVolume = "SFXVolume";

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

        // [비활성화 2026-11-02 — 효과음 task Plan 6-1 D] 옛 기본값 8과 옛 설명.
        //   전체 한도를 16으로 올리고 「꽉 차면 우선 효과음이 일반을 끊는다」로 바뀌었다.
        //   주의: 이 값은 Login.unity에 저장된 값이 코드 기본값보다 우선한다. 씬 값은
        //   1회성 메뉴 「Hexiege/Setup/SFX Tiering - Apply One-time Asset Changes」로 16으로 바꾼다.
        //   사용자 테스트 통과 뒤 이 주석을 지운다(WORKFLOW [4]).
        // [Tooltip("동시에 재생 가능한 최대 SFX 개수. 초과 시 새 SFX는 무시됨 (규칙 13).")]
        // [SerializeField] private int _maxConcurrentSfx = 8;

        [Tooltip("동시에 재생 가능한 최대 SFX 개수(전체 한도, 기본 16). 꽉 차면 「일반」 효과음은 재생하지 않고, " +
                 "「우선」 효과음은 가장 먼저 시작한 일반 효과음을 끊고 재생한다 (규칙 13). 1보다 작으면 1로 본다.")]
        [SerializeField] private int _maxConcurrentSfx = 16;

        [Tooltip("같은 오디오 파일(클립)을 동시에 울릴 수 있는 최대 개수 (기본 3). 1보다 작으면 1로 본다.")]
        [SerializeField] private int _maxSameClipConcurrent = 3;

        [Tooltip("같은 클립의 최소 재생 간격(초, 기본 0.05). 직전 같은 클립이 시작한 뒤 이 시간 안에 온 요청은 재생하지 않는다. 0이면 간격 제한 없음.")]
        [SerializeField] private float _minSameClipIntervalSec = 0.05f;

        [Tooltip("우선 효과음이 일반 효과음을 끊을 때, 끊기는 소리를 0까지 줄이는 시간(초, 기본 0.03). '딱' 소리를 막는다. 0이면 바로 멈춘다.")]
        [SerializeField] private float _evictFadeOutSec = 0.03f;

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

        // [비활성화 2026-11-02 — 효과음 task Plan RM-2 · 6-2 「끊기」]
        //   옛 방식은 「재생 중 개수」 숫자 하나만 들고 있었다. 새 방식은 우선 효과음이 일반 효과음을
        //   골라 끊어야 하므로 "무엇이 울리고 있는지" 목록(_activeSfx)이 필요하고, 개수는 그 목록 길이로 센다.
        //   숫자를 따로 들고 있으면 끊을 때와 자동 반환 때 두 번 줄어드는 사고가 날 수 있어 목록 하나로 합쳤다.
        //   사용자 테스트 통과 뒤 이 주석을 지운다(WORKFLOW [4]).
        // /// <summary> 현재 재생 중인 SFX 개수. _maxConcurrentSfx와 비교하여 제한. </summary>
        // private int _activeSfxCount = 0;

        /// <summary>
        /// 지금 울리고 있는 효과음 한 건의 기록.
        /// PlayId는 재생마다 새로 붙는 번호(식별값)다. 자동 반환 코루틴은 이 번호로 "내가 맡은 재생이
        /// 아직 목록에 있는지" 확인하고, 이미 우선 효과음에 끊겨 목록에서 빠졌으면 아무것도 하지 않는다.
        /// (같은 AudioSource가 그새 다른 소리에 다시 쓰이고 있을 수 있으므로 소스로 찾으면 안 된다)
        /// </summary>
        private struct ActiveSfx
        {
            public int PlayId;           // 재생마다 새로 붙는 번호
            public AudioSource Source;   // 이 재생에 빌려준 AudioSource
            public AudioClip Clip;       // 재생 중인 클립 — 「같은 클립」 판정에 쓴다
            public SfxPriority Priority; // 일반/우선 — 끊을 대상을 고를 때 쓴다
        }

        /// <summary>
        /// 지금 울리고 있는 효과음 목록. 재생을 시작한 순서대로 뒤에 붙으므로 앞쪽일수록 먼저 시작한 소리다.
        /// 목록 길이 = 현재 재생 중 개수(전체 한도 비교에 쓴다).
        /// </summary>
        private readonly List<ActiveSfx> _activeSfx = new List<ActiveSfx>();

        /// <summary> 클립별 마지막 재생 시작 시각(Time.unscaledTime). 「같은 클립 최소 간격」 판정용. </summary>
        private readonly Dictionary<AudioClip, float> _lastClipStartTime = new Dictionary<AudioClip, float>();

        /// <summary>
        /// 다음 재생에 붙일 번호. 재생마다 1씩 늘린다.
        /// (int 최대값까지 가려면 초당 100번씩 약 8개월을 재생해야 하므로 넘침은 고려하지 않는다)
        /// </summary>
        private int _nextSfxPlayId;

        /// <summary> UniRx 구독 묶음. OnDestroy에서 일괄 해제. </summary>
        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        /// <summary>
        /// 현재 음소거 상태인지 여부. Initialize()에서 PlayerPrefs로부터 로드한다.
        /// true면 Master 채널 출력을 -80dB로 눌러 전체를 무음 처리한다(저장 볼륨값은 보존).
        /// </summary>
        private bool _muted;

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
            // [개발] 둘 다 Warn + 개발 — Inspector 배선 누락(설정 오류)이다.
            //   LogRules 1.3 원칙 3 단서: 설정 오류는 Error 로 올리지 않고 Warn + 개발로 낮춘다.
            //   축 B ①: 배선은 프리팹/씬에 박혀 있어 있으면 모든 기기에 있고 없으면 모든 기기에 없다
            //           → "플레이어 기기에서만 벌어지는가"가 항상 "아니오" → 개발.
            if (_bgmGroup == null)
            {
                GameLog.Dev.Warn("Audio", nameof(AudioManager),
                                 "BGM AudioMixerGroup 미배선 — BGM 볼륨 제어가 동작하지 않는다",
                                 "Group=Bgm");
            }
            if (_sfxGroup == null)
            {
                GameLog.Dev.Warn("Audio", nameof(AudioManager),
                                 "SFX AudioMixerGroup 미배선 — SFX 볼륨 제어가 동작하지 않는다",
                                 "Group=Sfx");
            }

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
            // [비활성화 2026-11-02 — 효과음 task Plan RM-2] 옛 개수 숫자 초기화.
            //   새 방식은 개수를 「재생 중 목록(_activeSfx)」 길이로 센다. 재호출 시 아직 울리고 있는 소리는
            //   목록에 그대로 두어, 각자의 자동 반환 코루틴이 끝날 때 정상적으로 빠지고 풀로 돌아오게 한다
            //   (목록을 비우면 그 소리들이 개수에서 빠진 채 계속 울려 한도를 넘게 된다).
            // _activeSfxCount = 0;

            // SFX 풀 사전 생성 — 동시 재생 한도만큼 AudioSource를 미리 만들어 둔다.
            // (2026-11-02: Inspector 값이 1보다 작게 잘못 들어가도 최소 1개는 만들도록 보정값을 쓴다)
            for (int i = 0; i < EffectiveMaxConcurrentSfx; i++)
            {
                AudioSource src = CreateSfxSource();
                src.gameObject.SetActive(false);
                _sfxPool.Enqueue(src);
            }

            // 저장된 볼륨 로드 후 믹서에 적용 (규칙 20, 21).
            ApplyVolume(ParamMasterVolume, LoadVolume(PrefMasterVolume));
            ApplyVolume(ParamBgmVolume, LoadVolume(PrefBgmVolume));
            ApplyVolume(ParamSfxVolume, LoadVolume(PrefSfxVolume));

            // 저장된 음소거 상태 로드 (규칙 27 — 음소거 플래그의 영속화 조항). 기본값 0 = 소리 켜짐.
            //   음소거였다면 위에서 적용한 Master 볼륨을 -80dB로 다시 눌러 무음으로 만든다.
            //   (BGM/SFX의 논리 볼륨값은 그대로 유지 — 음소거 해제 시 그대로 복원됨)
            _muted = PlayerPrefs.GetInt(PrefMuted, 0) == 1;
            if (_muted)
                ApplyDb(ParamMasterVolume, MutedDb);

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

        /// <summary>
        /// 유닛 공격 효과음 재생. UnitView.OnAttackHit()에서 VFX와 짝으로 호출.
        /// SoundConfig 유닛 항목에 「우선 재생」이 체크돼 있으면 우선 효과음으로 재생한다(2026-11-02).
        /// </summary>
        /// <param name="type">공격한 유닛 타입.</param>
        public void PlayUnitAttackSfx(UnitType type)
        {
            if (_config == null) return;
            // [비활성화 2026-11-02 — 효과음 task Plan 6-2] 옛 호출: 우선 여부 없이 모두 일반으로 재생.
            // PlaySfxClip(_config.GetUnitAttackClip(type), _config.GetUnitAttackVolume(type));
            SfxPriority priority = _config.IsUnitAttackPriority(type) ? SfxPriority.High : SfxPriority.Normal;
            PlaySfxClip(_config.GetUnitAttackClip(type), _config.GetUnitAttackVolume(type), priority);
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

        /// <summary>
        /// UI 효과음 재생 (규칙 4 — AudioManager가 직접 보유한 매핑 사용).
        /// UI 효과음은 「일반」 등급이다(2026-11-02 결정 — UI 소리를 만들 때 다시 정한다).
        /// </summary>
        /// <param name="key">재생할 UI 이펙트 키.</param>
        public void PlayUiSfx(UiEffectKey key)
        {
            if (_uiCache == null) return;
            if (_uiCache.TryGetValue(key, out UiSoundEntry entry))
                PlaySfxClip(entry.clip, entry.volume);
        }

        // [비활성화 2026-11-02 — 효과음 task Plan RM-2] 옛 재생 진입점.
        //   옛 동작: 클립이 없거나 재생 중 개수가 한도(8)에 닿으면 새 요청을 버린다. 우선순위·같은 클립 제한은 없었다.
        //   새 진입점(바로 아래)이 같은 이름으로 대신한다 — 기존 호출 PlaySfxClip(clip, volume)은 그대로 컴파일되며
        //   「일반」 등급으로 동작한다. 사용자 테스트 통과 뒤 이 주석을 지운다(WORKFLOW [4]).
        // /// <summary>
        // /// 임의의 클립을 SFX 채널로 직접 재생한다 (확장성용 진입점).
        // /// 클립이 null이거나 동시 재생 한도를 초과하면 무시한다 (규칙 13).
        // /// </summary>
        // /// <param name="clip">재생할 클립.</param>
        // /// <param name="volume">클립별 기준 볼륨 (0~1). 기본 1.0 (규칙 16).</param>
        // public void PlaySfxClip(AudioClip clip, float volume = 1f)
        // {
        //     // 클립이 없거나 동시 재생 한도 초과 시 무시.
        //     if (clip == null || _activeSfxCount >= _maxConcurrentSfx) return;
        //
        //     AudioSource src = GetOrCreateSfx();
        //     src.clip = clip;
        //     src.volume = Mathf.Clamp01(volume);
        //     src.gameObject.SetActive(true);
        //     src.Play();
        //
        //     _activeSfxCount++;
        //     // 클립 길이 후 자동 반환.
        //     StartCoroutine(ReturnSfxAfterPlay(src, clip.length));
        // }

        /// <summary>
        /// 임의의 클립을 SFX 채널로 직접 재생한다 (모든 효과음 재생이 거치는 단일 진입점).
        /// 재생 여부는 규칙 13(2026-11-02 개정)대로 정한다:
        ///   ① 같은 클립이 직전 시작 뒤 최소 간격(_minSameClipIntervalSec) 안이면 재생하지 않는다.
        ///   ② 같은 클립이 이미 _maxSameClipConcurrent개 울리고 있으면 재생하지 않는다.
        ///   ③ 전체 한도(_maxConcurrentSfx)가 찼으면 — 일반 요청은 재생하지 않고, 우선 요청은 가장 먼저
        ///      시작한 일반 효과음을 짧게 줄여 끈 뒤 재생한다(모두 우선이면 재생하지 않는다).
        /// 클립이 null이면 아무 일도 하지 않는다(설정 칸이 비어 있는 정상 상태 — 로그 없음).
        /// </summary>
        /// <param name="clip">재생할 클립.</param>
        /// <param name="volume">클립별 기준 볼륨 (0~1). 기본 1.0 (규칙 16).</param>
        /// <param name="priority">재생 등급. 기본 Normal(일반). 특수 공격·방어 타워 공격음은 High(우선).</param>
        public void PlaySfxClip(AudioClip clip, float volume = 1f, SfxPriority priority = SfxPriority.Normal)
        {
            // 클립이 없으면 재생할 것이 없다(옛 줄의 null 검사는 그대로 유지 — Plan RM-2).
            if (clip == null) return;

            // ①② 같은 클립 제한 — 우선 효과음도 똑같이 받는다.
            if (!PassesSameClipRules(clip, priority)) return;

            // ③ 전체 한도 — 자리가 없으면 우선 요청만 일반 효과음을 끊어 자리를 만든다.
            if (!TryMakeRoomForSfx(clip, priority)) return;

            StartSfx(clip, volume, priority);
        }

        // ====================================================================
        // SFX 동시 재생 판단 (규칙 13 개정 — 2026-11-02)
        // ====================================================================

        /// <summary> Inspector 값이 1보다 작게 잘못 들어가도 최소 1로 보는 전체 한도. </summary>
        private int EffectiveMaxConcurrentSfx => Mathf.Max(1, _maxConcurrentSfx);

        /// <summary> Inspector 값이 1보다 작게 잘못 들어가도 최소 1로 보는 같은 클립 동시 개수 한도. </summary>
        private int EffectiveMaxSameClipConcurrent => Mathf.Max(1, _maxSameClipConcurrent);

        /// <summary> 같은 클립 최소 간격(초). 음수·NaN이면 0(제한 없음)으로 본다("> 0" 비교는 NaN에서 false). </summary>
        private float EffectiveMinSameClipIntervalSec => _minSameClipIntervalSec > 0f ? _minSameClipIntervalSec : 0f;

        /// <summary> 끊을 때 페이드 시간(초). 음수·NaN이면 0(바로 멈춤)으로 본다. </summary>
        private float EffectiveEvictFadeOutSec => _evictFadeOutSec > 0f ? _evictFadeOutSec : 0f;

        /// <summary>
        /// 같은 클립 제한(①최소 간격 ②동시 개수)을 통과하는지 검사한다. 통과하지 못하면 개발 로그 요약에 센다.
        /// </summary>
        /// <param name="clip">재생하려는 클립(null 아님).</param>
        /// <param name="priority">재생하려는 소리의 등급(로그용 — 판단에는 쓰지 않는다. 우선도 같은 제한을 받는다).</param>
        /// <returns>재생해도 되면 true.</returns>
        private bool PassesSameClipRules(AudioClip clip, SfxPriority priority)
        {
            // ① 최소 간격 — 같은 프레임·아주 가까운 시점에 같은 소리가 겹쳐 한 덩어리로 커지는 것을 막는다.
            //   시간은 Time.unscaledTime(일시정지 timeScale=0과 무관한 실제 시간)을 쓴다 — 자동 반환과 같은 기준.
            float interval = EffectiveMinSameClipIntervalSec;
            if (interval > 0f && _lastClipStartTime.TryGetValue(clip, out float lastStart)
                && Time.unscaledTime - lastStart < interval)
            {
                RecordSfxDecision(SfxDecision.DroppedSameClipInterval, clip, priority);
                return false;
            }

            // ② 동시 개수 — 같은 오디오 파일(AudioClip 참조가 같은 것)이 몇 개 울리고 있는지 센다.
            if (CountActiveSfxWithClip(clip) >= EffectiveMaxSameClipConcurrent)
            {
                RecordSfxDecision(SfxDecision.DroppedSameClipLimit, clip, priority);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 전체 한도에 자리가 있는지 확인하고, 없으면 우선 요청에 한해 일반 효과음을 끊어 자리를 만든다.
        /// </summary>
        /// <param name="clip">재생하려는 클립(로그용).</param>
        /// <param name="priority">재생하려는 소리의 등급.</param>
        /// <returns>재생할 자리가 있으면 true.</returns>
        private bool TryMakeRoomForSfx(AudioClip clip, SfxPriority priority)
        {
            // while인 이유: 플레이 중 Inspector에서 한도를 낮추면 재생 중 개수가 한도보다 클 수 있다.
            //   그때는 자리가 하나 날 때까지 일반 효과음을 차례로 끊는다(보통은 한 번만 돈다).
            while (_activeSfx.Count >= EffectiveMaxConcurrentSfx)
            {
                // 일반 요청은 기존처럼 버린다.
                if (priority != SfxPriority.High)
                {
                    RecordSfxDecision(SfxDecision.DroppedGlobalLimit, clip, priority);
                    return false;
                }

                // 우선 요청 — 끊을 일반 효과음을 찾는다. 모두 우선이면 우선끼리는 끊지 않고 버린다
                //   (우선끼리 끊으면 포격음 같은 중요한 소리가 서로 잘리기 때문 — Plan Q-16).
                int victimIndex = FindOldestNormalSfxIndex();
                if (victimIndex < 0)
                {
                    RecordSfxDecision(SfxDecision.DroppedAllPriority, clip, priority);
                    return false;
                }

                EvictActiveSfxAt(victimIndex);
                RecordSfxDecision(SfxDecision.EvictedNormal, clip, priority);
            }
            return true;
        }

        /// <summary>
        /// 실제 재생을 시작하고 「재생 중 목록」에 기록한 뒤, 클립 길이 뒤 자동 반환을 예약한다.
        /// </summary>
        /// <param name="clip">재생할 클립.</param>
        /// <param name="volume">클립별 기준 볼륨 (0~1).</param>
        /// <param name="priority">재생 등급.</param>
        private void StartSfx(AudioClip clip, float volume, SfxPriority priority)
        {
            AudioSource src = GetOrCreateSfx();
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.gameObject.SetActive(true);
            src.Play();

            // 재생마다 새 번호를 붙여 목록에 넣는다. 자동 반환 코루틴은 이 번호로만 자기 재생을 찾는다.
            int playId = ++_nextSfxPlayId;
            _activeSfx.Add(new ActiveSfx { PlayId = playId, Source = src, Clip = clip, Priority = priority });
            _lastClipStartTime[clip] = Time.unscaledTime;

            // 클립 길이 후 자동 반환.
            StartCoroutine(ReturnSfxAfterPlay(playId, clip.length));
        }

        /// <summary> 재생 중 목록에서 같은 클립이 몇 개인지 센다(목록은 최대 한도 개수라 단순 반복으로 충분). </summary>
        /// <param name="clip">셀 클립.</param>
        /// <returns>같은 클립 재생 개수.</returns>
        private int CountActiveSfxWithClip(AudioClip clip)
        {
            int count = 0;
            for (int i = 0; i < _activeSfx.Count; i++)
            {
                if (_activeSfx[i].Clip == clip)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 재생 중 목록에서 가장 먼저 시작한 「일반」 효과음의 위치를 찾는다.
        /// 목록은 시작 순서대로 쌓이므로 앞에서부터 처음 만나는 일반 효과음이 가장 오래된 것이다.
        /// </summary>
        /// <returns>위치(0부터). 일반 효과음이 없으면 -1.</returns>
        private int FindOldestNormalSfxIndex()
        {
            for (int i = 0; i < _activeSfx.Count; i++)
            {
                if (_activeSfx[i].Priority == SfxPriority.Normal)
                    return i;
            }
            return -1;
        }

        /// <summary> 재생 번호로 재생 중 목록의 위치를 찾는다. </summary>
        /// <param name="playId">찾을 재생 번호.</param>
        /// <returns>위치(0부터). 이미 목록에서 빠졌으면(끊겼으면) -1.</returns>
        private int FindActiveSfxIndex(int playId)
        {
            for (int i = 0; i < _activeSfx.Count; i++)
            {
                if (_activeSfx[i].PlayId == playId)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 재생 중인 효과음 하나를 끊는다. 목록에서는 즉시 빼서(= 재생 중 개수에서 즉시 빠짐) 새 소리의 자리를 만들고,
        /// 소리 자체는 아주 짧게 줄인 뒤 멈춰 풀로 돌려준다('딱' 소리 방지 — Plan Q-17).
        /// 이 재생의 자동 반환 코루틴은 나중에 깨어나도 목록에서 번호를 못 찾으므로 아무것도 하지 않는다
        /// → 개수가 두 번 줄거나, 그새 다른 소리에 다시 쓰인 AudioSource를 멈추는 일이 생기지 않는다.
        /// </summary>
        /// <param name="index">재생 중 목록의 위치.</param>
        private void EvictActiveSfxAt(int index)
        {
            ActiveSfx victim = _activeSfx[index];
            _activeSfx.RemoveAt(index);

            if (victim.Source == null) return; // 이미 파괴된 소스(씬 정리 등)는 돌려줄 것이 없다.

            float fade = EffectiveEvictFadeOutSec;
            if (fade <= 0f || !isActiveAndEnabled)
            {
                // 페이드 0이거나 코루틴을 돌릴 수 없는 상태면 바로 멈춘다.
                ReleaseSfxSource(victim.Source);
                return;
            }
            // 페이드 중인 소스는 풀에 없으므로 그동안 새 소리는 다른 소스를 쓴다(풀이 비면 새로 만든다).
            StartCoroutine(FadeOutAndReleaseSfx(victim.Source, fade));
        }

        // ====================================================================
        // SFX 동시 재생 판단 — 개발 로그 (LogRules: Info + 개발, 1초에 최대 1줄 요약)
        // ====================================================================

        /// <summary> 동시 재생 제한이 내린 판단의 종류. 개발 로그 요약의 칸 이름이 된다. </summary>
        private enum SfxDecision
        {
            DroppedSameClipInterval = 0, // ① 같은 클립 최소 간격 때문에 재생 안 함
            DroppedSameClipLimit = 1,    // ② 같은 클립 동시 개수 때문에 재생 안 함
            DroppedGlobalLimit = 2,      // ③ 전체 한도가 차서 일반 요청을 재생 안 함
            DroppedAllPriority = 3,      // ③ 전체 한도가 찼고 울리는 것이 모두 우선이라 우선 요청을 재생 안 함
            EvictedNormal = 4,           // ③ 우선 요청이 일반 효과음을 끊고 재생함
        }

        /// <summary> SfxDecision 종류 수(배열 크기). enum에 값을 더하면 함께 늘린다. </summary>
        private const int SfxDecisionKindCount = 5;

        /// <summary>
        /// 판단 요약 로그 최소 간격(초). 큰 전투에서는 판단이 초당 수십 번 일어나므로 한 줄씩 남기면 로그가 폭주한다
        /// (LogRules 1.14-8 「매 틱 로깅 금지」). 그래서 판단마다 숫자만 세고, 1초에 최대 1줄로 묶어 남긴다.
        /// </summary>
        private const float SfxDecisionLogIntervalSec = 1f;

        /// <summary> 마지막 요약 로그 이후 판단 종류별 누적 횟수. </summary>
        private readonly int[] _sfxDecisionCounts = new int[SfxDecisionKindCount];

        /// <summary> 마지막 요약 로그를 남긴 시각(Time.unscaledTime). 처음에는 바로 남기도록 아주 작은 값. </summary>
        private float _lastSfxDecisionLogTime = float.NegativeInfinity;

        /// <summary>
        /// 동시 재생 제한 판단 하나를 센다. 직전 요약 뒤 1초가 지났으면 그동안의 누적을 한 줄로 남기고 0으로 되돌린다.
        /// 로그 분류(LogRules 1.2): 축 A Info(의도된 흐름 — 규칙대로 버리거나 끊은 것), 축 B 개발(에디터에서 재현 가능).
        /// 릴리스 빌드에서는 이 메서드 호출 자체가 컴파일 단계에서 사라진다(아래 두 Conditional — LogRules 1.7).
        /// 주의: 마지막 요약 뒤에 생긴 판단은 다음 판단이 올 때까지 기록되지 않는다 — 요약의 숫자는 「최소」 횟수다.
        /// </summary>
        /// <param name="decision">내린 판단.</param>
        /// <param name="clip">그 판단을 부른 재생 요청의 클립.</param>
        /// <param name="priority">그 재생 요청의 등급.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void RecordSfxDecision(SfxDecision decision, AudioClip clip, SfxPriority priority)
        {
            _sfxDecisionCounts[(int)decision]++;

            float now = Time.unscaledTime;
            if (now - _lastSfxDecisionLogTime < SfxDecisionLogIntervalSec) return;
            _lastSfxDecisionLogTime = now;

            // [개발] Info + 개발 — 위 summary 참고. 값에는 구분자 ", "가 들어가지 않는다(클립 이름은 파일 이름).
            GameLog.Dev.Info("Audio", nameof(AudioManager),
                             "효과음 동시 재생 제한 판단 요약(직전 요약 이후 누적)",
                             $"Last={decision}, LastClip={clip.name}, LastPriority={priority}, " +
                             $"IntervalDrop={_sfxDecisionCounts[(int)SfxDecision.DroppedSameClipInterval]}, " +
                             $"SameClipDrop={_sfxDecisionCounts[(int)SfxDecision.DroppedSameClipLimit]}, " +
                             $"GlobalDrop={_sfxDecisionCounts[(int)SfxDecision.DroppedGlobalLimit]}, " +
                             $"AllPriorityDrop={_sfxDecisionCounts[(int)SfxDecision.DroppedAllPriority]}, " +
                             $"Evicted={_sfxDecisionCounts[(int)SfxDecision.EvictedNormal]}, " +
                             $"Active={_activeSfx.Count}, Max={EffectiveMaxConcurrentSfx}");

            System.Array.Clear(_sfxDecisionCounts, 0, _sfxDecisionCounts.Length);
        }

        // ====================================================================
        // SFX 풀 관리
        // ====================================================================

        /// <summary> SFX용 AudioSource를 풀에서 꺼내거나 없으면 새로 만든다. </summary>
        /// <returns>재생 준비된 AudioSource.</returns>
        private AudioSource GetOrCreateSfx()
        {
            // 2026-11-02: 풀 안의 소스가 이미 파괴됐으면(Unity null) 건너뛴다 — 파괴된 소스에 재생을 걸면 예외가 난다.
            while (_sfxPool.Count > 0)
            {
                AudioSource pooled = _sfxPool.Dequeue();
                if (pooled != null)
                    return pooled;
            }
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

        // [비활성화 2026-11-02 — 효과음 task Plan 6-2 「끊기」의 함정]
        //   옛 자동 반환은 "AudioSource를 받아 개수를 1 줄이고 풀에 돌려준다"였다. 새 방식에서는 우선 효과음이
        //   중간에 끊은 소리가 있으므로, 옛 방식대로면 끊긴 소리의 코루틴이 나중에 또 돌아
        //   ① 개수를 한 번 더 줄이거나 ② 그새 다른 소리에 다시 쓰이고 있는 AudioSource를 멈춰 버린다.
        //   그래서 아래 새 코루틴은 재생 번호(playId)로 자기 재생이 아직 목록에 있는지 확인한 뒤에만 반환한다.
        //   사용자 테스트 통과 뒤 이 주석을 지운다(WORKFLOW [4]).
        // /// <summary>
        // /// 클립 재생이 끝난 뒤 AudioSource를 풀에 반환하는 코루틴.
        // /// 정확한 종료 시점을 잡기 어려우므로 클립 길이 + 0.1초 여유를 둔다.
        // /// </summary>
        // /// <param name="src">반환할 AudioSource.</param>
        // /// <param name="duration">클립 길이(초).</param>
        // private IEnumerator ReturnSfxAfterPlay(AudioSource src, float duration)
        // {
        //     // WaitForSecondsRealtime 사용 — 싱글플레이 일시정지(timeScale=0) 중에도
        //     // 클립 재생이 끝나면 정상적으로 반환되어 _activeSfxCount를 풀 수 있도록 한다.
        //     yield return new WaitForSecondsRealtime(duration + 0.1f);
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

        /// <summary>
        /// 클립 재생이 끝난 뒤 그 재생을 「재생 중 목록」에서 빼고 AudioSource를 풀에 반환하는 코루틴.
        /// 정확한 종료 시점을 잡기 어려우므로 클립 길이 + 0.1초 여유를 둔다(종전과 같음).
        /// 깨어났을 때 재생 번호가 목록에 없으면(= 우선 효과음에 이미 끊겨 반환됨) 아무것도 하지 않는다.
        /// </summary>
        /// <param name="playId">이 코루틴이 맡은 재생 번호.</param>
        /// <param name="duration">클립 길이(초).</param>
        private IEnumerator ReturnSfxAfterPlay(int playId, float duration)
        {
            // WaitForSecondsRealtime 사용 — 싱글플레이 일시정지(timeScale=0) 중에도
            // 클립 재생이 끝나면 정상적으로 반환되어 재생 중 개수가 풀리도록 한다.
            yield return new WaitForSecondsRealtime(duration + 0.1f);

            int index = FindActiveSfxIndex(playId);
            if (index < 0) yield break; // 이미 끊겨 반환된 재생 — 두 번 반환하지 않는다.

            AudioSource src = _activeSfx[index].Source;
            _activeSfx.RemoveAt(index);
            ReleaseSfxSource(src);
        }

        /// <summary>
        /// 끊긴 효과음의 볼륨을 짧게 0까지 줄인 뒤 멈추고 풀에 돌려주는 코루틴.
        /// 이 재생은 이미 「재생 중 목록」에서 빠져 있으므로 여기서는 개수를 건드리지 않는다.
        /// </summary>
        /// <param name="src">끊을 AudioSource.</param>
        /// <param name="fadeSec">줄이는 시간(초, 0보다 큼).</param>
        private IEnumerator FadeOutAndReleaseSfx(AudioSource src, float fadeSec)
        {
            float startVolume = src.volume;
            float elapsed = 0f;
            while (elapsed < fadeSec)
            {
                yield return null;
                if (src == null) yield break; // 기다리는 사이 소스가 파괴됐으면 할 일이 없다.

                // 효과음 반환과 같은 실제 시간 기준(일시정지 중에도 줄어든다).
                elapsed += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / fadeSec));
            }
            ReleaseSfxSource(src);
        }

        /// <summary>
        /// AudioSource를 멈추고 비활성화해 풀에 돌려준다(이미 파괴됐으면 무시).
        /// 「재생 중 목록」에서 빼는 일은 호출하는 쪽이 먼저 한다.
        /// </summary>
        /// <param name="src">돌려줄 AudioSource.</param>
        private void ReleaseSfxSource(AudioSource src)
        {
            if (src == null) return;
            src.Stop();
            src.clip = null;
            src.gameObject.SetActive(false);
            _sfxPool.Enqueue(src);
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
        // 음소거 외부 API (규칙 23·25·27 — 공개 API 목록과 저장값 보존형 구현은 규칙 27)
        // ====================================================================

        /// <summary>
        /// 현재 음소거 상태인지 반환한다.
        /// </summary>
        /// <returns>true=음소거, false=소리 켜짐.</returns>
        public bool IsMuted() => _muted;

        /// <summary>
        /// 음소거 상태를 설정하고 즉시 반영 + PlayerPrefs에 저장한다 (규칙 27 — 공개 API와 영속화).
        /// 이 메서드를 호출하는 쪽은 전체 소리켜기·전체 음소거 버튼이다(규칙 23).
        ///
        /// 동작 방식(규칙 27 본문 — 저장값 보존형):
        ///   - 음소거 ON: 저장된 볼륨값(PlayerPrefs)은 건드리지 않고 Master 채널 출력만
        ///     -80dB로 눌러 전체를 무음으로 만든다.
        ///   - 음소거 OFF: 저장된 Master 볼륨값을 다시 적용하여 원래 소리를 복원한다.
        ///     (BGM/SFX 채널은 애초에 건드리지 않았으므로 그대로 유지된다)
        /// </summary>
        /// <param name="muted">true=음소거, false=소리 켜짐.</param>
        public void SetMuted(bool muted)
        {
            _muted = muted;

            // 음소거 여부를 즉시 저장(규칙 21과 동일한 즉시 저장 패턴).
            PlayerPrefs.SetInt(PrefMuted, muted ? 1 : 0);
            PlayerPrefs.Save();

            ApplyMuteState();
        }

        /// <summary>
        /// 세 볼륨(Master/BGM/SFX)을 모두 기본값 1.0으로 되돌리고 음소거를 해제한다.
        /// 볼륨을 1.0으로 되돌리는 것은 규칙 25가, 음소거가 함께 풀리는 것은
        /// 규칙 27의 자동 언뮤트 조항이 정한 동작이다.
        /// 볼륨 컨트롤의 "초기화" 버튼이 호출한다.
        /// 각 값은 PlayerPrefs에도 저장되어 재시작 후에도 유지된다.
        /// </summary>
        public void ResetAllVolumes()
        {
            // SetMuted(false)를 먼저 호출해도 되지만, 아래 SetXxxVolume이 내부에서
            // 자동 언뮤트를 수행하므로 볼륨 3종을 1.0으로 설정하는 것만으로 음소거도 풀린다.
            SetMasterVolume(1f);
            SetBgmVolume(1f);
            SetSfxVolume(1f);

            // 슬라이더 조작이 아닌 초기화 경로에서도 확실히 음소거가 풀리도록 명시적으로 한 번 더 보장.
            SetMuted(false);
        }

        // ====================================================================
        // 볼륨 내부 처리
        // ====================================================================

        /// <summary>
        /// 볼륨을 믹서에 적용하고 PlayerPrefs에 저장한다.
        /// 음소거 상태에서 볼륨을 조작하면(슬라이더 등) 자동으로 음소거를 해제한다(규칙 27의 자동 언뮤트 조항).
        /// </summary>
        /// <param name="param">AudioMixer Exposed Parameter 이름.</param>
        /// <param name="prefKey">PlayerPrefs 저장 키.</param>
        /// <param name="value">0~1 볼륨 값.</param>
        private void SetVolume(string param, string prefKey, float value)
        {
            // 음소거 중 볼륨을 조작하면 사용자가 소리를 다시 듣고 싶다는 의도이므로
            // 먼저 음소거를 해제한다(규칙 27의 자동 언뮤트 조항). SetMuted(false)가 Master 출력을
            // 저장 볼륨값으로 복원하며, 아래에서 새 값으로 다시 덮어쓴다.
            if (_muted)
                SetMuted(false);

            float clamped = Mathf.Clamp01(value);
            ApplyVolume(param, clamped);

            PlayerPrefs.SetFloat(prefKey, clamped);
            PlayerPrefs.Save(); // 규칙 21 — 즉시 저장.
        }

        /// <summary>
        /// 현재 _muted 값에 따라 Master 채널 출력을 갱신한다.
        ///   음소거 ON  → Master를 -80dB로 눌러 무음.
        ///   음소거 OFF → 저장된 Master 볼륨값을 다시 적용하여 복원.
        /// </summary>
        private void ApplyMuteState()
        {
            if (_muted)
                ApplyDb(ParamMasterVolume, MutedDb);
            else
                ApplyVolume(ParamMasterVolume, LoadVolume(PrefMasterVolume));
        }

        /// <summary>
        /// 0~1 볼륨을 dB로 변환하여 AudioMixer에 적용한다 (규칙 19).
        /// </summary>
        /// <param name="param">AudioMixer Exposed Parameter 이름.</param>
        /// <param name="value">0~1 볼륨 값.</param>
        private void ApplyVolume(string param, float value)
        {
            // Log10(0) = -무한대 방지를 위해 하한 0.0001을 둔다 (규칙 19).
            float dB = Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f;
            ApplyDb(param, dB);
        }

        /// <summary>
        /// 계산된 dB 값을 AudioMixer Exposed Parameter에 직접 적용한다.
        /// 음소거(-80dB)와 일반 볼륨(dB 변환값) 적용 경로가 이 메서드를 공유하여
        /// SetFloat 실패 진단 로깅(2026-07-08 교훈)을 한 곳에서 처리한다.
        /// </summary>
        /// <param name="param">AudioMixer Exposed Parameter 이름.</param>
        /// <param name="dB">적용할 dB 값.</param>
        private void ApplyDb(string param, float dB)
        {
            // _audioMixer 미연결 시 볼륨 제어 불가 — 원인 추적을 위해 경고 로그 남김.
            if (_audioMixer == null)
            {
                // [개발] Warn + 개발 — 위 두 건과 같은 성격의 Inspector 배선 누락이다(1.3 원칙 3 단서).
                GameLog.Dev.Warn("Audio", nameof(AudioManager),
                                 "AudioMixer 미배선 — 볼륨/음소거를 적용할 수 없다");
                return;
            }

            // SetFloat은 Exposed Parameter 이름이 믹서에 없으면 false를 반환한다.
            //   볼륨/음소거가 적용되지 않는 원인이 파라미터 이름 불일치인지 확인하기 위해 결과를 검사한다.
            bool result = _audioMixer.SetFloat(param, dB);
            if (!result)
            {
                // [개발] Warn + 개발.
                //   SetFloat 이 false 를 돌려주는 원인은 "Exposed Parameter 이름이 믹서에 없다" 하나뿐이고,
                //   그건 AudioMixer 에셋의 설정 오류다 → 배선 누락과 같은 부류(1.3 원칙 3 단서).
                //   에셋은 모든 기기에 동일하게 들어가므로 축 B ① 이 항상 "아니오" → 개발.
                GameLog.Dev.Warn("Audio", nameof(AudioManager),
                                 "AudioMixer.SetFloat 실패 — Exposed Parameter 이름이 믹서에 없다",
                                 $"Param={param}, Db={dB}");
            }
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
