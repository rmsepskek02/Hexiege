# Plan — 사운드 시스템 구축 (개정)

## 개요 (자연어 설명)

이 계획은 VFX와 SFX를 완전히 분리하면서도, 동시에 재생되어야 하는 VFX+SFX 쌍이 항상 함께 트리거되도록 보장하는 구조를 구축하는 내용입니다.

핵심 원칙은 두 가지입니다. 첫째, EffectManager는 파티클(VFX)만 담당하고 오디오는 전혀 알지 못합니다. 둘째, VFX와 SFX의 쌍은 "같은 호출 지점에서 두 매니저를 연달아 호출"하는 방식으로 유지합니다. 예를 들어 유닛이 공격할 때, UnitView에서 EffectManager(VFX)와 AudioManager(SFX)를 한 줄씩 연속으로 호출합니다. 코드에서 명시적으로 쌍이 보이므로 VFX만 재생되거나 SFX만 재생되는 실수를 방지할 수 있습니다.

---

## GameSystemRules 참조

| 규칙 | 출처 | 적용 위치 |
|------|------|----------|
| 규칙 1. 역할 분리 원칙 | GameSystemRules_Sound.md | EffectManager=VFX전용, AudioManager=BGM+SFX |
| 규칙 2. AudioManager 레이어 및 생명주기 | GameSystemRules_Sound.md | SingletonMonoBehaviour, DontDestroyOnLoad |
| 규칙 3. SoundConfig 레이어 의존 범위 | GameSystemRules_Sound.md | UiEffectKey를 SoundConfig에서 제외 |
| 규칙 4. UI SFX 관리 위치 | GameSystemRules_Sound.md | AudioManager가 직접 UiSoundEntry 보유 |
| 규칙 5. null-safe 호출 원칙 | GameSystemRules_Sound.md | 모든 AudioManager.Instance 호출에 ?. 적용 |
| 규칙 6. BGM 전환 시점 | GameSystemRules_Sound.md | SceneManager + GameEvents 구독 |
| 규칙 7. BGM 초기 재생 방식 | GameSystemRules_Sound.md | Initialize()에서 현재 씬 이름 확인 후 즉시 재생 |
| 규칙 8. BGM 크로스페이드 | GameSystemRules_Sound.md | AudioSource A/B 번갈아 사용, 1.0초 기본 |
| 규칙 9. Game 씬 로딩 중 BGM | GameSystemRules_Sound.md | OnGameStarted 전까지 Lobby BGM 유지 |
| 규칙 11. Victory/Defeat BGM 분리 (향후) | GameSystemRules_Sound.md | V1: 승패 구분 없이 동일한 게임종료 BGM 사용 |
| 규칙 10. SFX 2D 고정 | GameSystemRules_Sound.md | spatialBlend = 0 |
| 규칙 11. SFX 동시 재생 한도 | GameSystemRules_Sound.md | 8개 |
| 규칙 12. 멀티플레이 SFX 동기화 금지 | GameSystemRules_Sound.md | 로컬 재생만 |
| 규칙 13. VFX+SFX 쌍 호출 | GameSystemRules_Sound.md | 같은 메서드 내 연달아 호출 |
| 규칙 17. AudioMixerGroup 연결 | GameSystemRules_Sound.md | BGM/SFX AudioSource 모두 그룹 연결 필수, null 시 LogWarning |
| 규칙 15~18. 볼륨 채널/범위/저장 | GameSystemRules_Sound.md | Master/BGM/SFX, 0~1→dB, PlayerPrefs |
| UI 규칙 5. CanvasGroup 숨김/표시 패턴 | GameSystemRules_UI.md | 볼륨 조절 패널 Show/Hide |
| UI 규칙 1. Canvas Scaler 설정 | GameSystemRules_UI.md | 볼륨 슬라이더 UI 레이아웃 |
| UI 규칙 2. 앵커 기반 배치 원칙 | GameSystemRules_UI.md | 볼륨 슬라이더 UI 레이아웃 |
| UI 규칙 8. 팝업 타입 구분 | GameSystemRules_UI.md | 볼륨 패널은 팝업 타입 — 배경 탭으로 닫기 가능 |

---

## VFX+SFX 쌍 유지 메커니즘

EffectManager에서 SFX를 제거하면, 기존에 한 번의 호출로 동시에 재생되던 VFX+SFX 쌍이 깨진다. 이를 해결하는 방식은 **호출 지점에서 두 매니저를 연달아 호출**하는 것이다.

```csharp
// 변경 전 (EffectManager가 SFX까지 담당)
EffectManager.Instance?.PlayUnitAttack(type, pos, rot);   // VFX + SFX

// 변경 후 (각 매니저가 각자 담당)
EffectManager.Instance?.PlayUnitAttack(type, pos, rot);   // VFX만
AudioManager.Instance?.PlayUnitAttackSfx(type);           // SFX만
```

두 줄이 항상 같은 메서드 안에 붙어 있으므로, VFX와 SFX는 같은 프레임에 재생되며 분리될 위험이 없다.

**현재 실제 호출 위치 (3곳만):**

| 위치 | 현재 호출 | 변경 후 추가 |
|------|----------|-------------|
| `UnitView.cs:1520` — `OnAttackHit()` (Animation Event) | PlayUnitAttack(type, pos, rot) | + PlayUnitAttackSfx(type) |
| `UnitView.cs:481` — OnUnitDied 핸들러 | PlayUnitDeath(type, pos) | + PlayUnitDeathSfx(type) |
| `NetworkUnit.cs:183` — 멀티플레이 사망 동기화 | PlayUnitDeath(type, pos) | + PlayUnitDeathSfx(type) |

PlayBuildingDestroy/Upgrade/PlayUi는 현재 호출부가 없으므로, 향후 호출부를 추가할 때 동일한 패턴으로 작성한다.

---

## 신규/수정 파일 목록

```
[신규]
- Assets/_Project/Scripts/Presentation/Audio/AudioManager.cs
- Assets/_Project/Scripts/Infrastructure/Config/SoundConfig.cs
- Assets/_Project/Audio/AudioMixer.mixer  (Unity 에셋 — 에디터에서 생성)

[수정]
- Assets/_Project/Scripts/Presentation/Effects/EffectPreset.cs     (SFX 필드 제거)
- Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs    (SFX 코드 전체 제거)
- Assets/_Project/Scripts/Presentation/Unit/UnitView.cs            (AudioManager SFX 호출 추가)
- Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs    (AudioManager SFX 호출 추가)
- Assets/_Project/Scripts/Presentation/UI/InGameSettingsUI.cs      (_soundButton 리스너 + 볼륨 슬라이더 연동)
- Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs           (AudioManager 초기화)
- Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs            (null-safe 호출 확인)
```

---

## 구현 단계

### Step 1. AudioMixer 에셋 생성 (에디터 작업)

`Assets/_Project/Audio/` 폴더에 AudioMixer 에셋 생성.

**믹서 구조:**
```
AudioMixer (Master)
  ├── BGM Group
  └── SFX Group
```

**Exposed Parameters:**
| 파라미터 이름 | 대상 | 초기값 |
|------------|------|--------|
| `MasterVolume` | Master → Volume | 0 dB |
| `BGMVolume` | BGM → Volume | 0 dB |
| `SFXVolume` | SFX → Volume | 0 dB |

> 볼륨 변환: `Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f` (0이면 -80 dB로 무음)

---

### Step 2. SoundConfig.cs 작성 (Infrastructure/Config)

기존 UnitEffectConfig 패턴(List → Dictionary)과 동일한 구조로 작성.

GameSystemRules_Sound 규칙 3: SoundConfig는 Domain 레이어 타입(UnitType, BuildingType)만 키로 사용.
UI SFX(UiEffectKey)는 Presentation 타입이므로 SoundConfig에서 제외하고 AudioManager가 직접 보유.

```
SoundConfig : ScriptableObject
  [BGM]
  - AudioClip loginBgm
  - AudioClip lobbyBgm
  - AudioClip battleBgm
  - AudioClip gameEndBgm         // 게임 종료 시 재생 (승패 구분 없음 — 향후 Victory/Defeat 분리 예정)
  - float bgmCrossfadeDuration = 1.0f

  [SFX — UnitType별]
  - List<UnitSoundEntry> unitSoundEntries
    └── UnitSoundEntry { UnitType,
                         AudioClip attackClip, float attackVolume,   // 규칙 16: 클립별 볼륨
                         AudioClip deathClip,  float deathVolume }
  - Dictionary<UnitType, UnitSoundEntry> (Initialize()에서 빌드)

  [SFX — BuildingType별]
  - List<BuildingSoundEntry> buildingSoundEntries
    └── BuildingSoundEntry { BuildingType,
                             AudioClip placeClip,    float placeVolume,
                             AudioClip destroyClip,  float destroyVolume,
                             AudioClip upgradeClip,  float upgradeVolume }
  - Dictionary<BuildingType, BuildingSoundEntry> (Initialize()에서 빌드)

  [조회 메서드]
  - GetUnitAttackClip(UnitType) : AudioClip
  - GetUnitAttackVolume(UnitType) : float
  - GetUnitDeathClip(UnitType) : AudioClip
  - GetUnitDeathVolume(UnitType) : float
  - GetBuildingPlaceClip(BuildingType) : AudioClip
  - GetBuildingPlaceVolume(BuildingType) : float
  - GetBuildingDestroyClip(BuildingType) : AudioClip
  - GetBuildingDestroyVolume(BuildingType) : float
  - GetBuildingUpgradeClip(BuildingType) : AudioClip
  - GetBuildingUpgradeVolume(BuildingType) : float
  ※ UI SFX 조회 메서드 없음 — AudioManager가 직접 처리
```

---

### Step 3. AudioManager.cs 작성 (Presentation/Audio)

`SingletonMonoBehaviour<AudioManager>` 상속 → DontDestroyOnLoad 자동 적용.

**Inspector 설정:**
```
[Header("Mixer")]
AudioMixer _audioMixer
AudioMixerGroup _bgmGroup
AudioMixerGroup _sfxGroup

[Header("BGM AudioSource")]
AudioSource _bgmSourceA     // 크로스페이드 채널 A
AudioSource _bgmSourceB     // 크로스페이드 채널 B

[Header("SFX")]
Transform _sfxContainer
int _maxConcurrentSfx = 8

[Header("UI SFX")]  // GameSystemRules_Sound 규칙 4: UiEffectKey(Presentation)는 AudioManager에서 직접 관리
List<UiSoundEntry> _uiSoundEntries   // UiEffectKey → AudioClip + volume 매핑 (규칙 16)
  └── UiSoundEntry { UiEffectKey key, AudioClip clip, float volume }
Dictionary<UiEffectKey, UiSoundEntry>  // Initialize()에서 빌드
```

**외부 API:**
```
void Initialize(SoundConfig config)
void PlayBgm(BgmType type)
void PlayUnitAttackSfx(UnitType type)
void PlayUnitDeathSfx(UnitType type)
void PlayBuildingPlaceSfx(BuildingType type)
void PlayBuildingDestroySfx(BuildingType type)
void PlayBuildingUpgradeSfx(BuildingType type)
void PlayUiSfx(UiEffectKey key)
void PlaySfxClip(AudioClip clip, float volume = 1f)   // 직접 클립 재생 (확장성)
void SetMasterVolume(float value)   // 0~1 → dB 변환 → PlayerPrefs 저장
void SetBgmVolume(float value)
void SetSfxVolume(float value)
float GetMasterVolume()             // PlayerPrefs에서 읽기
float GetBgmVolume()
float GetSfxVolume()
```

**BGM 자동 전환 (GameSystemRules_Sound 규칙 6, 7, 9, 10, 11 적용):**
```
Awake() → SceneManager.activeSceneChanged 구독
  씬 이름 "Login" → PlayBgm(BgmType.Login)
  씬 이름 "Lobby" → PlayBgm(BgmType.Lobby)

Initialize() → SceneManager.GetActiveScene().name 확인 → 현재 씬 BGM 즉시 재생
  (SceneManager.activeSceneChanged는 현재 씬에서 발생하지 않으므로 초기 재생에 사용 불가)
  클립이 null이면 무음 전환 (규칙 10)

GameEvents.OnGameStarted 구독 → PlayBgm(BgmType.Battle)
  (Game 씬 로딩 중에는 Lobby BGM 유지, OnGameStarted 시점에 Battle BGM 크로스페이드 — 규칙 9)

GameEvents.OnGameEnd 구독
  → PlayBgm(BgmType.GameEnd)   // 싱글/멀티 공통, 승패 구분 없음
  // 향후 작업: LocalPlayerTeam.Set() 정상화 후 Victory/Defeat 분리 (규칙 11)
```

**BGM 크로스페이드:**
- AudioSource A/B 번갈아 사용
- 새 BGM 재생 시: 현재 소스는 `bgmCrossfadeDuration`초 동안 페이드아웃, 다른 소스는 페이드인
- Coroutine으로 처리. 새 전환 요청 시 기존 코루틴 StopCoroutine 후 재시작

**SFX 풀:**
- 기존 EffectManager SFX 풀 코드 그대로 이전
  (AudioSource Queue, `_maxConcurrentSfx`, `ReturnSfxAfterPlay` 코루틴)
- 모든 SFX AudioSource에 `outputAudioMixerGroup = _sfxGroup` 설정
- BGM AudioSource에 `outputAudioMixerGroup = _bgmGroup` 설정

**볼륨 PlayerPrefs:**
```
키: "MasterVolume", "BGMVolume", "SFXVolume"
Initialize() → 저장된 값 로드 → AudioMixer Exposed Parameter 적용
SetXxxVolume() → AudioMixer 적용 + PlayerPrefs.Save()
```

---

### Step 4. EffectPreset.cs 수정

SFX 필드를 제거하여 VFX 전용 에셋으로 변경.

**제거 대상:**
```csharp
// 제거
[SerializeField] private AudioClip _sfxClip;
[SerializeField] private float _sfxVolume = 1f;
public AudioClip SfxClip => _sfxClip;
public float SfxVolume => _sfxVolume;
```

**제거 후:**
```csharp
// VfxPrefab만 남음
[SerializeField] private GameObject _vfxPrefab;
public GameObject VfxPrefab => _vfxPrefab;
```

> 기존 .asset 파일에서 `SfxClip`에 클립이 연결되어 있다면, 해당 클립을 SoundConfig의 해당 항목에 옮겨 연결해야 함 (Inspector 수동 작업).

---

### Step 5. EffectManager.cs 수정

SFX 관련 코드를 전체 제거. AudioManager에 대한 참조 없음 — 완전히 독립적인 VFX 전용 매니저.

**제거 대상:**
```csharp
// 제거
private readonly Queue<AudioSource> _sfxPool
private int _activeSfxCount
[SerializeField] private Transform _sfxContainer
[SerializeField] private int _maxConcurrentSfx

private AudioSource GetOrCreateSfx()
private AudioSource CreateSfxSource()
private IEnumerator ReturnSfxAfterPlay(AudioSource, float)
```

**Play() 메서드 수정:**
```csharp
// SFX 블록 전체 제거 — AudioClip 관련 코드 없음
private void Play(EffectPreset preset, Vector3 pos, Quaternion rot)
{
    if (preset == null) return;
    if (preset.VfxPrefab != null)
    {
        VfxPoolItem item = GetOrCreateVfx(preset.VfxPrefab);
        item.Play(pos, rot);
    }
    // SFX 블록 제거됨
}
```

**Initialize() 수정:**
SFX 풀 사전 생성 코드 제거. Config 초기화만 남음.

> 제거 규칙: SFX 코드는 AudioManager로 완전히 이전되므로 제거 후에도 오디오 기능이 유지됨. 단, 테스트 통과 전까지 주석 처리로 비활성화.

---

### Step 6. 호출부 수정 — VFX+SFX 쌍 복원

EffectManager 호출 직후 AudioManager 호출을 추가하여 쌍을 유지한다.

#### UnitView.cs:1520 — OnAttackHit() (Animation Event)
```csharp
// 변경 전
EffectManager.Instance?.PlayUnitAttack(_unitData.Type, spawnPos, spawnRot);

// 변경 후
EffectManager.Instance?.PlayUnitAttack(_unitData.Type, spawnPos, spawnRot);  // VFX
AudioManager.Instance?.PlayUnitAttackSfx(_unitData.Type);                    // SFX
```

#### UnitView.cs:481 — OnUnitDied 핸들러
```csharp
// 변경 전
EffectManager.Instance?.PlayUnitDeath(_unitData.Type, transform.position);

// 변경 후
EffectManager.Instance?.PlayUnitDeath(_unitData.Type, transform.position);   // VFX
AudioManager.Instance?.PlayUnitDeathSfx(_unitData.Type);                     // SFX
```

#### NetworkUnit.cs:183 — 멀티플레이 사망 동기화
```csharp
// 변경 전
EffectManager.Instance?.PlayUnitDeath(type, pos);

// 변경 후
EffectManager.Instance?.PlayUnitDeath(type, pos);    // VFX
AudioManager.Instance?.PlayUnitDeathSfx(type);       // SFX
```

---

### Step 7. 로비 설정 패널 UI 작성 (Presentation/UI)

GameSystemRules_Sound 규칙 22: Lobby 씬에도 볼륨 슬라이더를 배치한다.

Lobby.unity 씬에 별도 설정 패널 GameObject 추가.

**구성:**
- Master/BGM/SFX 슬라이더 3개
- CanvasGroup으로 Show/Hide (GameSystemRules_UI 규칙 5)
- 팝업 타입 (배경 탭으로 닫기)
- 초기값: `AudioManager.Instance?.GetXxxVolume()` 로드
- onChange: `AudioManager.Instance?.SetXxxVolume(value)`

> Lobby 씬의 어떤 버튼/경로로 이 패널을 여는지는 Lobby UI 설계에 따라 결정. 본 단계에서는 패널 자체와 볼륨 슬라이더 연동만 구현.

---

### Step 8. LoginBootstrapper.cs 수정

AudioManager 초기화. AudioManager 오브젝트는 Login.unity 씬의 `[Audio]` 빈 GameObject에 컴포넌트로 부착. `SingletonMonoBehaviour`의 DontDestroyOnLoad로 이후 씬에서 유지됨.

```csharp
[Header("사운드")]
[SerializeField] private SoundConfig _soundConfig;

// Start() 내부 Firebase 초기화 이전:
AudioManager.Instance?.Initialize(_soundConfig);
```

Login.unity에 AudioManager 컴포넌트와 AudioMixer Inspector 연결 필요.

---

### Step 8. InGameSettingsUI.cs 수정

`_soundButton` 플레이스홀더를 볼륨 조절 패널 토글로 구현.

**추가할 Inspector 필드:**
```csharp
[Header("볼륨 패널")]
[SerializeField] private CanvasGroup _volumePanelGroup   // 볼륨 패널 CanvasGroup
[SerializeField] private Slider _masterSlider
[SerializeField] private Slider _bgmSlider
[SerializeField] private Slider _sfxSlider
```

**Initialize() 수정:**
```csharp
_soundButton.onClick.AddListener(OnSoundButtonClicked);

// 슬라이더 초기값: AudioManager에서 현재 볼륨 읽기
_masterSlider.value = AudioManager.Instance?.GetMasterVolume() ?? 1f;
_bgmSlider.value    = AudioManager.Instance?.GetBgmVolume() ?? 1f;
_sfxSlider.value    = AudioManager.Instance?.GetSfxVolume() ?? 1f;

// 슬라이더 onChange 리스너
_masterSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetMasterVolume(v));
_bgmSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetBgmVolume(v));
_sfxSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetSfxVolume(v));
```

볼륨 패널 Show/Hide는 GameSystemRules_UI 규칙 5 (CanvasGroup) 적용.

---

## 구현 순서 요약

```
Step 1. AudioMixer 에셋 생성 (에디터)
Step 2. SoundConfig.cs 작성
Step 3. AudioManager.cs 작성
Step 4. EffectPreset.cs 수정 (SFX 필드 제거)
Step 5. EffectManager.cs 수정 (SFX 코드 전체 제거)
Step 6. UnitView.cs / NetworkUnit.cs 수정 (AudioManager SFX 호출 추가)
Step 7. LoginBootstrapper.cs 수정
Step 8. InGameSettingsUI.cs 수정
```

---

## 예상 위험 요소

| 위험 | 내용 | 대응 |
|------|------|------|
| EffectPreset .asset SFX 연결 데이터 소실 | 기존 프리셋에 SfxClip이 연결된 경우 SoundConfig 이전 전에 필드 제거하면 데이터 손실 | SoundConfig 완성 후 Inspector에서 값 이전, 이후 필드 제거 |
| Lobby 씬 BGM 트리거 | Lobby 씬에 별도 Bootstrapper 없어 SceneManager 이벤트에 의존 | 씬 이름 상수를 AudioManager 내부에서 관리, 이름 변경 시 한 곳만 수정 |
| AudioMixer Inspector 연결 누락 | SFX AudioSource에 믹서 그룹 연결 안 되면 볼륨 제어 불가 | Initialize()에서 null 체크 + Debug.LogWarning |
| 개발 중 Game씬 직접 진입 | AudioManager.Instance == null | 모든 호출 `?.` 연산자로 null-safe 처리 |
| 크로스페이드 중 BGM 재전환 | 코루틴 중복으로 오디오 채널 꼬임 | 새 크로스페이드 시 기존 코루틴 StopCoroutine 후 재시작 |
| 승리/패배 BGM 팀 판별 | LocalPlayerTeam.Set()이 싱글플레이에서 호출되지 않아 팀 구분 불가 | V1: 승패 구분 없이 동일한 게임종료 BGM 재생, 팀 판별 수단 확보 후 별도 작업 |
