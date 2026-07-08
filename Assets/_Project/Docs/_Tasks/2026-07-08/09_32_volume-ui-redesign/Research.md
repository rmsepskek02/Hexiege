# Research — 볼륨 UI 개편 (VolumeButtonContainer + 뮤트 시스템)

## 이 작업이 무엇이고 왜 하는가 (자연어 설명)

지금 게임에는 소리 크기를 조절하는 화면이 두 군데(게임 중 설정 메뉴, 로비 설정 탭) 있습니다.
두 화면 모두 "마스터 / 배경음 / 효과음" 슬라이더 3개만 있고, **한 번에 소리를 모두 껐다 켜는 "음소거" 기능**과
**슬라이더를 기본값으로 되돌리는 "초기화" 기능**이 없습니다.

이번 작업은 이 두 화면에 **VolumeButtonContainer**라는 버튼 묶음을 추가하는 것입니다. 이 묶음에는
"전체 소리켜기 / 전체 음소거 / 초기화 / 뒤로" 버튼이 들어갑니다. 그리고 소리가 켜져 있는지 꺼져 있는지에 따라
슬라이더 색을 초록/빨강으로 바꿔서 사용자가 상태를 눈으로 바로 알 수 있게 합니다.

핵심은 **"음소거"가 슬라이더 값을 지우지 않는다**는 점입니다. 음소거를 누르면 소리만 완전히 꺼지고,
다시 켜면 이전에 맞춰둔 볼륨이 그대로 돌아옵니다. 이 "껐다 켜는" 처리는 소리를 담당하는 AudioManager가
전담하고, 화면(UI)은 버튼을 누를 때 AudioManager에게 "음소거해줘 / 풀어줘" 한 줄만 부탁하는 구조로 만듭니다.

정리하면 이번 작업은 세 부분입니다.
1. AudioManager에 "음소거" 기능(껐다 켜기 + 저장/복원)을 새로 넣는다.
2. 두 UI 스크립트(게임 중 설정, 로비 설정)에 새 버튼 처리와 슬라이더 색 처리를 넣는다.
3. 실제 버튼/슬라이더가 화면에 배치되도록 **신규** 에디터 스크립트로 UI 계층을 만든다.

---

## 관련 규칙 근거 (읽은 문서)

- `GameSystemRules_Sound.md` 볼륨/뮤트 규칙 18~27 (뮤트 담당 주체, 볼륨 보존, IsMuted 저장/로드, 자동 언뮤트, 초기화)
- `GameSystemRules_UI.md` "사운드 설정 UI (VolumeButtonContainer)" 규칙 1~7, 로비 탭 구조 규칙 1~4, 인게임 설정 메뉴 규칙 1-1, 공통 규칙 5(CanvasGroup)·6(폰트)
- `WORKFLOW.md` task 문서 작성 형식

---

## 1. 현재 InGameSettingsUI.cs 구조

경로: `Assets/_Project/Scripts/Presentation/UI/InGameSettingsUI.cs`

### 현재 SerializeField 필드

| 필드 | 타입 | 역할 |
|------|------|------|
| `_panel` | AnimatedPanel | 팝업 박스 본체(페이드) |
| `_closeButton` | Button | X 닫기 버튼 |
| `_soundButton` | Button | 사운드 옵션 → 볼륨 패널 토글 |
| `_forfeitButton` | Button | 게임 포기 |
| `_mainButtonContainer` | CanvasGroup | 사운드/포기 버튼 묶음 (볼륨 패널 열면 숨김) |
| `_volumePanelGroup` | CanvasGroup | 볼륨 패널 표시/숨김 |
| `_backButton` | Button | 볼륨 패널 → 메인 버튼 그룹 복귀 |
| `_masterSlider` / `_bgmSlider` / `_sfxSlider` | Slider | 볼륨 슬라이더 3종 |
| `_masterValueText` / `_bgmValueText` / `_sfxValueText` | TextMeshProUGUI | 퍼센트 텍스트 |

### 현재 동작 요약
- `Initialize()`에서 버튼 리스너 등록 + `SetupVolumeSliders()`로 슬라이더 초기값/리스너 연결.
- `SetupVolumeSliders()` → `SetupOneSlider()`로 각 슬라이더에 `AudioManager.SetXxxVolume` 연결.
- 볼륨 패널 토글은 `_volumePanelGroup.alpha` 값(0.5 기준)으로 표시 여부를 판단(규칙 5, CanvasGroup).
- 포기 버튼은 `ShowConfirm()` → `IForfeitService.RequestForfeit()`.

### 없는 것 (이번에 추가 필요)
- 프로필 버튼(현재 미구현, 설계상 메인 버튼 3종 = 사운드/프로필/포기).
- 전체 소리켜기(OnButton) / 전체 음소거(OffButton) / 초기화(ResetButton) 버튼과 처리.
- 뮤트 상태에 따른 슬라이더 색상(초록/빨강) 제어 및 토글 버튼 표출 전환.

---

## 2. 현재 LobbySettingsView.cs 구조

경로: `Assets/_Project/Scripts/Presentation/UI/LobbySettingsView.cs`

### 현재 SerializeField 필드

| 필드 | 타입 | 역할 |
|------|------|------|
| `_backButton` | Button | 좌상단 뒤로가기(서브→메인 복귀, 탭 네비게이션) |
| `_backButtonGroup` | CanvasGroup | 뒤로가기 버튼 표시/숨김 |
| `_mainView` | CanvasGroup | 설정 버튼 목록 화면 |
| `_subViewContainer` | CanvasGroup | 서브 화면 컨테이너 |
| `_profileButton` / `_soundButton` | Button | 프로필/사운드 서브 화면 진입 |
| `_soundSubView` / `_profileSubView` | CanvasGroup | 사운드/프로필 서브 화면 |
| `_masterSlider` / `_bgmSlider` / `_sfxSlider` | Slider | 볼륨 슬라이더 3종 |
| `_masterValueText` / `_bgmValueText` / `_sfxValueText` | TextMeshProUGUI | 퍼센트 텍스트 |

### 현재 동작 요약
- `Awake()` → `Initialize()`에서 버튼 리스너 + `SetupVolumeSliders()`.
- 메인 ↔ 서브 화면 전환은 `SetGroupVisible()`(CanvasGroup, 규칙 5).
- 사운드 서브 화면 진입 시 `RefreshVolumeSliderValues()`로 저장값 재동기화.

### 없는 것 (이번에 추가 필요)
- 사운드 서브 화면 안의 VolumeButtonContainer(OnButton / OffButton / ResetButton — **뒤로 버튼 없음**, 탭 구조상 `_backButton`이 별도로 담당).
- 뮤트 상태에 따른 슬라이더 색상 제어 및 토글 버튼 표출 전환.

> 참고: `GameSystemRules_UI.md` 로비 탭 구조 규칙 1은 프로필 탭과 설정 탭을 분리하고 설정 탭을 `SettingPanel`로 규정한다.
> 현재 코드/에디터 스크립트는 `LobbySettingsView`가 `ProfilePanel` 하위에 구성되어 있어(과거 구조), 규칙과 명칭 차이가 있다.
> 이번 작업 범위는 **볼륨 UI(VolumeButtonContainer) 추가**이며, 프로필/설정 탭 분리(ProfilePanel↔SettingPanel 재구성)는 별도 작업이므로 여기서 다루지 않는다.

---

## 3. 현재 AudioManager.cs 볼륨 관련 요소

경로: `Assets/_Project/Scripts/Presentation/Audio/AudioManager.cs`

### PlayerPrefs 키 (현재)
- `PrefMasterVolume = "MasterVolume"`
- `PrefBgmVolume = "BGMVolume"`
- `PrefSfxVolume = "SFXVolume"`
- (`"IsMuted"` 키 없음 — 추가 필요)

### AudioMixer Exposed Parameter 상수
- `ParamMasterVolume = "MasterVolume"`, `ParamBgmVolume = "BGMVolume"`, `ParamSfxVolume = "SFXVolume"`

### 볼륨 관련 메서드 (현재)

| 메서드 | 역할 |
|--------|------|
| `SetMasterVolume(float)` / `SetBgmVolume(float)` / `SetSfxVolume(float)` | 믹서 적용 + PlayerPrefs 저장 (`SetVolume` 경유) |
| `GetMasterVolume()` / `GetBgmVolume()` / `GetSfxVolume()` | 저장값 반환 (`LoadVolume`, 기본 1.0) |
| `SetVolume(param, prefKey, value)` (private) | Clamp → `ApplyVolume` → `PlayerPrefs.SetFloat` + `Save()` |
| `ApplyVolume(param, value)` (private) | 0~1 → dB 변환 후 `_audioMixer.SetFloat` |
| `LoadVolume(prefKey)` (private) | `PlayerPrefs.GetFloat(key, 1f)` |

### `Initialize(SoundConfig)` 흐름 (현재)
Config 연결 → UI 캐시 → 믹서 그룹 배선 → SFX 풀 생성 → **저장 볼륨 3종 로드·적용** → 현재 씬 BGM 재생.
→ **뮤트 상태 복원 로직 없음** (추가 필요, 규칙 25).

### 없는 것 (이번에 추가 필요)
- `_isMuted` 상태 플래그.
- `SetMuted(bool)` 공개 메서드 (규칙 23).
- `IsMuted()` 조회 메서드 (UI가 슬라이더 색·버튼 표출 판단에 사용).
- `"IsMuted"` PlayerPrefs 키 + 뮤트 시 -80dB 적용 상수.
- `Initialize()`에서 저장된 `"IsMuted"` 복원.
- 볼륨 설정 경로(`SetVolume`)에서 뮤트 중이면 자동 언뮤트 (규칙 26).

---

## 4. 추가/변경이 필요한 필드·메서드 분석

### AudioManager.cs (추가)
- 상수: `PrefIsMuted = "IsMuted"`, `MuteDb = -80f`.
- 필드: `bool _isMuted`.
- 메서드: `SetMuted(bool)`, `IsMuted()`.
- `Initialize()`: 볼륨 로드 후 `"IsMuted"` 읽어 뮤트였다면 마스터 출력 -80dB 적용, `_isMuted=true` 세팅.
- `SetVolume()`: 진입 시 `_isMuted`면 언뮤트 처리(마스터 복원 + `"IsMuted"=0` 저장) 후 값 적용 → 규칙 26 자동 언뮤트.

### InGameSettingsUI.cs (추가)
- SerializeField: `_profileButton`(Button, 미구현 표시용), `_onButton`(전체 소리켜기), `_offButton`(전체 음소거), `_resetButton`(초기화).
- SerializeField: 슬라이더 채움(Fill) 색을 바꾸기 위한 `Image` 3종 (`_masterFill` / `_bgmFill` / `_sfxFill`).
- 로직: 뮤트 토글(OnButton→언뮤트, OffButton→뮤트) → `AudioManager.SetMuted()` 호출 후 화면 상태 갱신.
- 로직: 초기화 → 슬라이더 3종을 1.0으로 설정(→ onValueChanged 통해 `SetXxxVolume(1)` → 자동 언뮤트) 후 화면 갱신.
- 로직: 뮤트 상태에 따라 슬라이더 색(초록/빨강) + On/Off 버튼 표출을 갱신하는 공통 메서드.
- 로직: 슬라이더 조작 시(onValueChanged 후) 뮤트가 풀렸을 수 있으므로 화면 상태 재갱신.

### LobbySettingsView.cs (추가)
- SerializeField: `_onButton`, `_offButton`, `_resetButton` (뒤로 버튼은 기존 `_backButton` 사용, VolumeButtonContainer에 포함 안 함).
- SerializeField: 슬라이더 Fill `Image` 3종.
- 로직: InGameSettingsUI와 동일한 뮤트 토글 / 초기화 / 색·버튼 표출 갱신 (동일 규칙, 두 화면 동일 동작).

> 슬라이더 색상 제어 대상은 각 슬라이더의 Fill 이미지로 한다(에디터 스크립트가 생성하는 `Fill` 오브젝트의 `Image`).
> UI 스크립트는 `AudioManager.Instance?.IsMuted()` 결과에 따라 이 Fill 색을 초록/빨강으로 바꾼다.

---

## 5. 신규 에디터 스크립트 작성 범위

기존 에디터 스크립트는 **수정 금지**(사용자 제약). 신규 스크립트만 작성한다.

### 기존(참조만, 수정 안 함)
- `Assets/Editor/Setup/SetupInGameVolumePanel.cs` — Game 씬에 MainButtonContainer + VolumePanel(슬라이더 3종 + 뒤로 버튼)까지 구성.
- `Assets/Editor/Setup/SetupLobbySettingsTab.cs` — Lobby 씬에 LobbySettingsView(메인/서브/슬라이더 3종) 구성.

두 기존 스크립트에는 **VolumeButtonContainer(On/Off/Reset)·슬라이더 Fill 참조·프로필 버튼**이 없다.

### 신규 작성 (2개)
- `Assets/Editor/Setup/SetupInGameSettingsPanel.cs` (신규)
  - Game 씬의 InGameSettingsUI 팝업을 대상으로, 기존 구조 위에 VolumeButtonContainer(OnButton / OffButton / ResetButton / BackButton)를 구성하고, 메인 버튼 그룹에 프로필 버튼을 추가하며, 슬라이더 Fill `Image` 참조와 새 버튼 필드를 SerializedObject로 연결.
- `Assets/Editor/Setup/SetupLobbySettingPanel.cs` (신규)
  - Lobby 씬의 LobbySettingsView 사운드 서브 화면에 VolumeButtonContainer(OnButton / OffButton / ResetButton — **BackButton 없음**)를 구성하고, 슬라이더 Fill `Image` 참조와 새 버튼 필드를 연결.

> 신규 스크립트는 기존 스크립트와 동일한 헬퍼 패턴(`GetOrCreateUIChild` / `GetOrAdd` / `StretchFull` / 폰트 규칙 6)을 자체 구현하여, 이미 셋업된 씬 위에 **덧붙이는(증분)** 방식으로 동작하도록 한다.

---

## 6. 영향 범위 정리

| 대상 | 영향 |
|------|------|
| AudioManager (Presentation) | 뮤트 상태 추가. BGM/SFX 재생 로직에는 영향 없음(마스터 출력만 -80dB). 기존 `SetXxxVolume`에 자동 언뮤트 분기 추가. |
| InGameSettingsUI (Presentation) | 필드/핸들러 추가. 기존 볼륨 패널 토글·포기 흐름은 유지. 씬(Game.unity) Inspector 참조 재배선 필요(신규 에디터 스크립트). |
| LobbySettingsView (Presentation) | 필드/핸들러 추가. 기존 탭 네비게이션 유지. 씬(Lobby.unity) Inspector 참조 재배선 필요(신규 에디터 스크립트). |
| Game.unity / Lobby.unity | 신규 에디터 스크립트 실행으로 UI 계층·참조 변경 → 씬 저장 발생. |
| PlayerPrefs | `"IsMuted"` 키 신규 추가(기본 0). 기존 볼륨 키 3종은 그대로. |
| AudioMixer 에셋 | Master Exposed Parameter를 -80dB로 쓰는 것 외 별도 파라미터 추가 불필요(별도 Mute 파라미터 만들지 않음). |
| 멀티플레이 | 볼륨/뮤트는 로컬 설정이라 네트워크 동기화 없음(사운드 규칙 14 정책 일관). 영향 없음. |

### 확인이 필요한 잠재 이슈
- **슬라이더 색상 적용 대상**: Fill 이미지 기준으로 잡았으나, 트랙 배경까지 함께 물들일지 여부는 디자인 확인 필요(규칙 3은 "슬라이더 색상"으로만 규정). Plan에서는 Fill 기준으로 진행하되 결정 포인트로 표기.
- **인게임 프로필 버튼**: 설계상 메인 버튼 3종(사운드/프로필/포기)에 포함되나 화면은 미구현. 버튼만 생성하고 핸들러는 비워둔다(향후 작업).
- **로비 명칭(ProfilePanel vs SettingPanel)**: 현재 구조는 과거 명칭 기준. 이번 작업은 볼륨 UI 추가에 한정하고 탭 재구성은 범위 밖.
