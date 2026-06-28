# Plan — 사운드 시스템 Inspector 설정 자동화

## 개요 (자연어 설명)

이 작업은 사운드 시스템 코드가 완성된 상태에서, Unity Inspector에서 수동으로 연결해야 하는 항목들을 에디터 스크립트로 자동화하는 것이다. AudioMixer 에셋 생성은 Unity API 제약으로 자동화가 불가능하여 수동 단계로 분리한다. 나머지(AudioManager GO 배치, SoundConfig 에셋 생성, 각 씬의 볼륨 패널 UI)는 에디터 스크립트 3개로 완전 자동화한다.

---

## 사전 수동 작업 (에디터 스크립트 실행 전 필수)

> ⚠️ 에디터 스크립트는 이 단계가 완료된 후 실행해야 한다.

### AudioMixer 에셋 생성

1. `Assets/_Project/Audio/` 폴더 생성 (없으면)
2. Project 창 우클릭 → **Create > Audio Mixer** → 이름: `GameAudioMixer`
3. AudioMixer Inspector에서 BGM, SFX Group 추가
4. 각 그룹 Volume 파라미터 우클릭 → **Expose to script**:

| 파라미터 이름 (정확히 일치 필수) | 대상 |
|-------------------------------|------|
| `MasterVolume` | Master → Volume |
| `BGMVolume` | BGM → Volume |
| `SFXVolume` | SFX → Volume |

5. 저장 경로: `Assets/_Project/Audio/GameAudioMixer.mixer`

---

## 에디터 스크립트 구성

### Script 1. `SetupAudioManager.cs`
**메뉴**: `Hexiege/Setup/사운드 - AudioManager 설정`
**대상**: Login.unity + SoundConfig.asset

#### 1-1. SoundConfig.asset 생성
```
경로: Assets/_Project/Docs/../ → Assets/Resources/Config/SoundConfig.asset
(없는 경우에만 생성)
```
- `ScriptableObject.CreateInstance<SoundConfig>()` + `AssetDatabase.CreateAsset()`
- BGM 클립 슬롯은 null로 초기화 (사용자가 클립 연결)

#### 1-2. Login.unity — [Audio] GameObject 구성
```
[Audio]                  ← AudioManager 컴포넌트
  ├── BGM_A              ← AudioSource (loop=true, playOnAwake=false, outputMixerGroup=BGM)
  ├── BGM_B              ← AudioSource (loop=true, playOnAwake=false, outputMixerGroup=BGM)
  └── SFX_Container      ← 빈 Transform (SFX 풀 컨테이너)
```

#### 1-3. AudioManager Inspector 연결 (SerializedObject)
| 필드 | 연결 값 |
|------|---------|
| `_audioMixer` | GameAudioMixer.mixer |
| `_bgmGroup` | BGM AudioMixerGroup |
| `_sfxGroup` | SFX AudioMixerGroup |
| `_bgmSourceA` | BGM_A AudioSource |
| `_bgmSourceB` | BGM_B AudioSource |
| `_sfxContainer` | SFX_Container Transform |

#### 1-4. LoginBootstrapper._soundConfig 연결
- 씬의 [Bootstrap] 오브젝트에서 LoginBootstrapper 컴포넌트 찾기
- `_soundConfig` 필드에 SoundConfig.asset 연결

---

### Script 2. `SetupInGameVolumePanel.cs`
**메뉴**: `Hexiege/Setup/사운드 - 인게임 볼륨 패널 생성`
**대상**: Game.unity

#### 2-1. 설계 개요

사운드 버튼 클릭 시 팝업 내부 뷰가 **메인 버튼 뷰 → 볼륨 사이드바**로 전환된다.
슬라이더 조작이 즉시 AudioManager에 반영되며, '뒤로' 버튼으로 메인 뷰 복귀.

```
[메인 설정 뷰]                    [볼륨 사이드바 — 사운드 클릭 후]
┌──────────────────[X]┐           ┌──────────────────[X]┐
│                      │  ──────▶  │                      │
│      [ 사운드 ]       │           │  마스터 [━━━●━━] 80% │
│      [ 포기   ]       │           │  배경음 [━━━━━●] 60% │
│                      │           │  효과음 [━●━━━━] 30% │
│                      │           │                      │
│                      │           │      [ ← 뒤로 ]      │
└──────────────────────┘           └──────────────────────┘
  (볼륨 패널 숨김)                    (메인 버튼 숨김)
```

#### 2-2. InGameSettingsUI 팝업 찾기
- `FindFirstObjectByType<InGameSettingsUI>()`로 씬에서 컴포넌트 탐색
- Panel(`_panel`) 하위에 두 뷰(MainButtonContainer, VolumePanel) 생성

#### 2-3. MainButtonContainer — 기존 버튼 그룹화

기존 사운드/포기 버튼을 하나의 컨테이너로 감싸서 CanvasGroup으로 제어:
```
MainButtonContainer (CanvasGroup)  ← 볼륨 패널 표시 시 숨김
  ├── SoundButton  (기존 _soundButton)
  └── ForfeitButton (기존 _forfeitButton)
```

#### 2-4. VolumePanel 생성

```
VolumePanel (CanvasGroup, alpha=0, blocksRaycasts=false, interactable=false)
  ├── SliderContainer (VerticalLayoutGroup, spacing=16, padding=20)
  │     ├── MasterRow (HorizontalLayoutGroup)
  │     │     ├── Label        (TMP, "마스터", width=70)
  │     │     ├── MasterSlider (Slider, 0~1, value=1)
  │     │     └── ValueText    (TMP, "100%", width=50)
  │     ├── BGMRow (HorizontalLayoutGroup)
  │     │     ├── Label        (TMP, "배경음", width=70)
  │     │     ├── BGMSlider    (Slider, 0~1, value=1)
  │     │     └── ValueText    (TMP, "100%", width=50)
  │     └── SFXRow (HorizontalLayoutGroup)
  │           ├── Label        (TMP, "효과음", width=70)
  │           ├── SFXSlider    (Slider, 0~1, value=1)
  │           └── ValueText    (TMP, "100%", width=50)
  └── BackButton (Button, "← 뒤로", 하단 중앙)
```

#### 2-5. InGameSettingsUI 코드 추가 (game-programmer 위임)

현재 코드에 없는 필드/로직 추가 필요:

| 추가 필드 | 역할 |
|---------|------|
| `_mainButtonContainer` (CanvasGroup) | 메인 버튼 그룹 show/hide |
| `_backButton` (Button) | 볼륨 패널 → 메인 복귀 |

추가 로직:
- `OnSoundButtonClicked()`: MainButtonContainer 숨김 + VolumePanel 표시
- BackButton onClick: VolumePanel 숨김 + MainButtonContainer 표시
- `HideVolumePanel()`: 이미 존재 — Hide() 시 호출되므로 MainButtonContainer 복원 로직 추가

#### 2-6. InGameSettingsUI 필드 연결 (SerializedObject)
| 필드 | 연결 값 |
|------|---------|
| `_mainButtonContainer` | MainButtonContainer CanvasGroup |
| `_volumePanelGroup` | VolumePanel CanvasGroup |
| `_masterSlider` | MasterSlider |
| `_bgmSlider` | BGMSlider |
| `_sfxSlider` | SFXSlider |
| `_backButton` | BackButton |

---

### Script 3. `SetupLobbySettingsTab.cs`
**메뉴**: `Hexiege/Setup/사운드 - 로비 설정 탭 구성`
**대상**: Lobby.unity
**근거**: 프로필 탭 → 설정 탭 전환 + GameSystemRules_Sound 규칙 22

#### 3-1. 설계 개요

프로필 탭을 설정 탭으로 전환. 탭 진입 시 설정 항목 버튼들이 화면 중앙 세로로 표시되고,
버튼 클릭 시 해당 서브 화면으로 전환된다. 서브 화면에서만 좌측 상단 뒤로가기 버튼 표시.

```
[설정 탭 메인]                      [서브 화면 — 사운드]
┌─────────────────────────┐         ┌─────────────────────────┐
│                          │         │ ←  사운드 설정            │
│                          │         │                          │
│       [ 프로필 ]          │ ──────▶ │  마스터 [━━━●━━] 80%   │
│       [ 사운드 ]          │         │  배경음 [━━━━━●] 60%   │
│       [ ...   ]          │         │  효과음 [━●━━━━] 30%   │
│                          │         │                          │
└─────────────────────────┘         └─────────────────────────┘
  BackButton: 숨김                     BackButton: 표시
  (탭 자체가 루트이므로)
```

#### 3-2. Profile 탭 전환 — (제거됨)

> **기존 UI 불변 원칙에 따라 탭 아이콘/텍스트 변경 코드를 제거했다.**
> 셋업 스크립트는 탭 버튼의 아이콘(`ui_icon_settings`)·텍스트("설정") 교체를 수행하지 않는다.
> 탭 외형 변경이 필요하면 별도 작업으로 분리한다.

#### 3-3. 기존 ProfileView → LobbySettingsView 교체 — (제거됨)

> **기존 UI 불변 원칙에 따라 기존 ProfileView GO 비활성화 코드를 제거했다.**
> LobbySettingsView GO는 ProfilePanel 자식으로 새로 생성하되, 기존 ProfileView는 그대로 둔다.

#### 3-4. LobbySettingsView GO 구조

SetupLobbySettingsTab 에디터 스크립트가 생성하는 계층 (CanvasGroup 초기값 포함):

```
LobbySettingsView (LobbySettingsView.cs)
  ├── BackButton (Button + CanvasGroup, 초기: alpha=0)
  ├── MainView (CanvasGroup, 초기: alpha=1)
  │     └── ButtonList (VerticalLayoutGroup)
  │           ├── ProfileButton (Button)
  │           └── SoundButton (Button)
  └── SubViewContainer (CanvasGroup, 초기: alpha=0)
        ├── SoundSubView (CanvasGroup, 초기: alpha=0)
        │     └── SliderContainer (VerticalLayoutGroup)
        │           ├── MasterRow (Label + Slider + ValueText)
        │           ├── BGMRow    (Label + Slider + ValueText)
        │           └── SFXRow    (Label + Slider + ValueText)
        └── ProfileSubView (CanvasGroup, 초기: alpha=0)
```

### LobbySettingsView CanvasGroup 제어 흐름

[메인 화면 ShowMain()]
  _mainView (CG)        → alpha=1, interactable=true,  blocksRaycasts=true
  _subViewContainer (CG)→ alpha=0, interactable=false, blocksRaycasts=false
  _soundSubView (CG)    → alpha=0, interactable=false, blocksRaycasts=false
  _profileSubView (CG)  → alpha=0, interactable=false, blocksRaycasts=false
  _backButtonGroup (CG) → alpha=0, interactable=false, blocksRaycasts=false

[사운드 서브 화면 ShowSubView(_soundSubView)]
  _mainView (CG)        → alpha=0, interactable=false, blocksRaycasts=false
  _subViewContainer (CG)→ alpha=1, interactable=true,  blocksRaycasts=true
  _soundSubView (CG)    → alpha=1, interactable=true,  blocksRaycasts=true
  _profileSubView (CG)  → alpha=0, interactable=false, blocksRaycasts=false
  _backButtonGroup (CG) → alpha=1, interactable=true,  blocksRaycasts=true

#### 3-5. LobbySettingsView.cs 신규 컴포넌트 (game-programmer 위임)

```
LobbySettingsView : MonoBehaviour
  - _backButton, _mainView, _subViewContainer
  - _profileButton, _soundButton
  - _soundSubView, _profileSubView
  - 볼륨 슬라이더 3종 (_masterSlider, _bgmSlider, _sfxSlider)

  ShowMain()      — MainView 표시, SubView 숨김, BackButton 숨김
  ShowSubView(go) — 해당 SubView 표시, MainView 숨김, BackButton 표시
  OnBackClicked() — ShowMain() 호출
  Initialize()    — 버튼 리스너 등록, 슬라이더 초기값 설정
```

---

## 실행 순서

```
[수동] AudioMixer 에셋 생성 (GameAudioMixer.mixer) — Unity API 제약으로 코드 불가
   ↓
[에디터] Hexiege/Setup/사운드 - AudioManager 설정
         → Login.unity [Audio] GO 생성 + SoundConfig.asset 생성
   ↓
[에디터] Hexiege/Setup/사운드 - 인게임 볼륨 패널 생성
         → Game.unity InGameSettingsUI 팝업 내 볼륨 사이드바 UI 생성
   ↓
[에디터] Hexiege/Setup/사운드 - 로비 설정 탭 구성
         → Lobby.unity 프로필 탭 → 설정 탭 전환 + LobbySettingsView 구성
   ↓
[수동] SoundConfig.asset에 BGM 클립 연결 (오디오 클립 에셋이 준비된 후)
```

---

## 신규/수정 파일 목록

```
[신규 — 에디터 스크립트]
- Assets/Editor/Setup/SetupAudioManager.cs
- Assets/Editor/Setup/SetupInGameVolumePanel.cs
- Assets/Editor/Setup/SetupLobbySettingsTab.cs

[신규 — 런타임 코드]
- Assets/_Project/Scripts/Presentation/UI/LobbySettingsView.cs

[수정 — 런타임 코드]
- Assets/_Project/Scripts/Presentation/UI/InGameSettingsUI.cs
  (_mainButtonContainer, _backButton 필드 추가 + 뷰 전환 로직)

[신규 — 에셋 (스크립트 실행 후 생성)]
- Assets/Resources/Config/SoundConfig.asset

[수정 — 씬 (스크립트 실행 후 변경)]
- Assets/_Project/Scenes/Login.unity   ([Audio] GO 추가)
- Assets/_Project/Scenes/Game.unity    (볼륨 사이드바 UI 추가)
- Assets/_Project/Scenes/Lobby.unity   (설정 탭 전환 + LobbySettingsView 구성)
```

---

## GameSystemRules 참조

| 규칙 | 적용 항목 |
|------|---------|
| GameSystemRules_Sound 규칙 2 | AudioManager — Login.unity에 배치, DontDestroyOnLoad |
| GameSystemRules_Sound 규칙 17 | AudioMixer null 체크 — 에디터 스크립트에서 사전 검증 |
| GameSystemRules_Sound 규칙 18 | Exposed Parameter 이름: `MasterVolume`/`BGMVolume`/`SFXVolume` 정확히 일치 |
| GameSystemRules_Sound 규칙 22 | Lobby 씬 볼륨 패널 배치 |
| GameSystemRules_UI 규칙 5 | CanvasGroup Show/Hide 패턴 |
| GameSystemRules_UI 규칙 8 | 팝업 타입 — 볼륨 패널은 배경 탭으로 닫기 가능 |

---

## 예상 위험 요소

| 위험 | 대응 |
|------|------|
| AudioMixer 경로 불일치 | 스크립트 실행 시 존재 여부 확인 후 경고 다이얼로그 |
| Login.unity에 이미 [Audio] GO 있는 경우 | 중복 생성 방지: 동일 이름 GO 존재 확인 후 재사용 |
| LobbyRootView Settings 버튼 참조 방식 | FindProperty("_settingsButton")로 탐색 — 없으면 수동 연결 안내 |
| Game.unity InGameSettingsUI 의 _panel 구조 | `FindFirstObjectByType<InGameSettingsUI>()` 후 SerializedObject로 _panel 참조 탐색 |

---

## UI 규칙 위반 수정 (2차 검토)

### 발견된 위반 사항

GameSystemRules_UI 규칙 5 (CanvasGroup 숨김/표시 패턴) 위반:

| 파일 | 위반 코드 | 수정 |
|------|---------|------|
| LobbySettingsView.cs ShowMain() | _soundSubView.SetActive(false) | SetGroupVisible(_soundSubView, false) |
| LobbySettingsView.cs ShowMain() | _profileSubView.SetActive(false) | SetGroupVisible(_profileSubView, false) |
| LobbySettingsView.cs ShowSubView() | _soundSubView.SetActive(...) | SetGroupVisible(_soundSubView, ...) |
| LobbySettingsView.cs ShowSubView() | _profileSubView.SetActive(...) | SetGroupVisible(_profileSubView, ...) |
| LobbySettingsView.cs SetBackButtonVisible() | _backButton.gameObject.SetActive(visible) | SetGroupVisible(_backButtonGroup, visible) |
| SetupLobbySettingsTab.cs | backBtnGo.SetActive(false) | CanvasGroup alpha=0 초기화 |
| SetupLobbySettingsTab.cs | soundSubViewGo.SetActive(false) | CanvasGroup alpha=0 초기화 |
| SetupLobbySettingsTab.cs | profileSubViewGo.SetActive(false) | CanvasGroup alpha=0 초기화 |
| SetupLobbySettingsTab.cs | profileView.gameObject.SetActive(false) | 제거 (기존 UI 불변 원칙) |
| SetupLobbySettingsTab.cs | 탭 버튼 아이콘/텍스트 변경 | 제거 (기존 UI 불변 원칙) |

### 필드 타입 변경

LobbySettingsView.cs:
- _soundSubView: GameObject → CanvasGroup
- _profileSubView: GameObject → CanvasGroup
- _backButtonGroup: (신규) CanvasGroup 추가
