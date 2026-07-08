# Plan — 볼륨 UI 개편 (VolumeButtonContainer + 뮤트 시스템)

## ⚠️ 기존 로직 제거 여부 (최상단 명시 — WORKFLOW 규칙)

이 작업에는 **기존 로직의 제거가 없다.** 모든 변경은 기존 코드 위에 **추가/확장**이다.
- AudioManager: 기존 볼륨 메서드는 그대로 두고 뮤트 관련 필드·메서드·분기만 추가한다.
- InGameSettingsUI / LobbySettingsView: 기존 슬라이더·네비게이션 로직은 유지하고 버튼/색상 처리만 추가한다.
- 에디터 스크립트: 기존 파일은 **수정하지 않고** 신규 2개만 작성한다.

따라서 "비활성화(주석 처리) 후 검증" 절차 대상이 되는 제거 로직은 없다.

---

## 이 작업이 무엇이고 왜 하는가 (자연어 설명)

게임 중 설정 메뉴와 로비 설정 탭의 사운드 화면에, **소리를 한 번에 껐다 켜는 음소거 버튼**과
**슬라이더를 100%로 되돌리는 초기화 버튼**을 추가합니다. 음소거를 눌러도 맞춰둔 볼륨 값은 지워지지 않고,
다시 켜면 그대로 복원됩니다. 소리가 켜졌는지 꺼졌는지는 슬라이더 색(초록/빨강)으로 바로 보여줍니다.

이 "껐다 켜기"의 실제 처리는 소리를 담당하는 AudioManager가 전부 맡고, 화면은 버튼을 누를 때
AudioManager에게 한 줄만 부탁합니다. 그래서 작업은 (1) AudioManager에 음소거 기능 추가 →
(2) 두 UI 스크립트에 버튼/색상 처리 추가 → (3) 실제 버튼이 화면에 놓이도록 신규 에디터 스크립트 작성,
순서로 진행합니다.

---

## 작업 순서 개요

1. **AudioManager.cs** — 뮤트 시스템 추가 (game-programmer)
2. **InGameSettingsUI.cs** — 새 버튼/색상 처리 추가 (game-programmer)
3. **LobbySettingsView.cs** — 새 버튼/색상 처리 추가 (game-programmer)
4. **신규 에디터 스크립트 2종** 작성 (game-programmer)
5. **에디터 스크립트 실행 및 씬 저장** (사용자 실행)

> 코드 구현은 game-programmer 에이전트에 위임(CLAUDE.md 규칙 3). 본 Plan 승인 후 진행.

---

## 1단계: AudioManager.cs 뮤트 시스템 추가

근거: `GameSystemRules_Sound.md` 뮤트 규칙 23~27, 볼륨 규칙 18(뮤트는 채널 볼륨과 별개 상태).

### 추가할 상수
| 이름 | 값 | 근거 |
|------|-----|------|
| `PrefIsMuted` | `"IsMuted"` | 규칙 25 (뮤트 상태 저장 키) |
| `MuteDb` | `-80f` | 규칙 24 (뮤트 시 -80dB) |

### 추가할 필드
- `private bool _isMuted;` — 현재 뮤트 상태 (규칙 23, 상태 소유 주체는 AudioManager).

### 추가할 메서드
- `public void SetMuted(bool muted)` (규칙 23, 24, 25)
  - `muted == true`: 마스터 Exposed Parameter를 `MuteDb(-80f)`로 직접 적용(`_audioMixer.SetFloat(ParamMasterVolume, MuteDb)`). **PlayerPrefs 볼륨 3종은 건드리지 않는다**(규칙 24, 값 보존).
  - `muted == false`: `ApplyVolume(ParamMasterVolume, LoadVolume(PrefMasterVolume))`로 저장된 마스터 볼륨을 다시 적용(복원).
  - 공통: `_isMuted = muted;` → `PlayerPrefs.SetInt(PrefIsMuted, muted ? 1 : 0)` → `PlayerPrefs.Save()` (규칙 25, 즉시 저장).
- `public bool IsMuted() => _isMuted;` — UI가 슬라이더 색·토글 버튼 표출 판단에 사용.

> 뮤트는 마스터 채널 출력만 -80dB로 낮춰 전체 소리를 끈다. BGM/SFX 채널 파라미터와 PlayerPrefs 볼륨 값은 그대로 유지되어, 언뮤트 시 이전 볼륨이 자연히 복원된다(규칙 24).

### 변경할 메서드
- `Initialize(SoundConfig)` — 볼륨 3종 로드·적용(기존) **직후**에 뮤트 복원 추가 (규칙 25):
  - `int muted = PlayerPrefs.GetInt(PrefIsMuted, 0);` (기본 0 = 언뮤트)
  - `muted == 1`이면 `_isMuted = true;` + 마스터 출력 `MuteDb` 적용. (기존 볼륨 적용을 덮어써 -80dB가 되도록 순서상 볼륨 적용 뒤에 수행.)
- `SetVolume(param, prefKey, value)` (private) — 진입부에 자동 언뮤트 분기 추가 (규칙 26):
  - `if (_isMuted) { SetMuted(false); }` 를 먼저 수행하여 마스터 출력을 복원(언뮤트)한 뒤, 요청된 채널 값을 적용·저장한다.
  - 이렇게 하면 UI가 별도로 언뮤트를 호출하지 않아도, 슬라이더 조작(어느 채널이든) 시 자동으로 소리가 켜진다(규칙 26). UI는 이후 `IsMuted()`를 다시 읽어 화면 상태를 반영한다(UI 규칙 5).

> 초기화(리셋, 규칙 27)는 별도 메서드 없이 UI가 슬라이더 3종을 1.0으로 세팅 → `SetXxxVolume(1)` 경유 → 위 자동 언뮤트로 처리한다. (별도 `ResetVolumes()` 추가 여부는 아래 "결정 포인트" 참조.)

---

## 2단계: InGameSettingsUI.cs 업데이트

근거: `GameSystemRules_UI.md` VolumeButtonContainer 규칙 1~7, 인게임 설정 메뉴 규칙 1-1, 공통 규칙 5·6. `GameSystemRules_Sound.md` 규칙 23·26·27.

### 추가할 SerializeField (기존 필드와 구분 — 아래는 전부 신규)
| 신규 필드 | 타입 | 역할 | 근거 |
|-----------|------|------|------|
| `_profileButton` | Button | 프로필 버튼(미구현, 버튼만 존재) | UI 규칙 1-1 |
| `_onButton` | Button | 전체 소리켜기(언뮤트) — 뮤트 상태일 때만 표출 | VolumeButtonContainer 규칙 2·4 |
| `_offButton` | Button | 전체 음소거(뮤트) — 언뮤트 상태일 때만 표출 | 규칙 2·4 |
| `_resetButton` | Button | 초기화(슬라이더 3종 100%) | 규칙 2·7 |
| `_masterFill` / `_bgmFill` / `_sfxFill` | Image | 슬라이더 색상 제어 대상(초록/빨강) | 규칙 3 |

> 기존 `_backButton`(볼륨 패널 → 메인 복귀)은 VolumeButtonContainer의 "뒤로" 역할을 그대로 수행한다(규칙 2, 인게임에는 뒤로 버튼 포함).

### 뮤트 버튼 토글 로직 개요 (규칙 4·6)
- OnButton 클릭 → `AudioManager.Instance?.SetMuted(false)` → 화면 상태 갱신.
- OffButton 클릭 → `AudioManager.Instance?.SetMuted(true)` → 화면 상태 갱신.
- UI는 AudioMixer/PlayerPrefs를 직접 만지지 않고 `SetMuted` 한 줄만 호출한다(규칙 6, Sound 규칙 23).

### 초기화 버튼 로직 개요 (규칙 7, Sound 규칙 27)
- ResetButton 클릭 → 슬라이더 3종 `value = 1f` 설정(기존 onValueChanged 리스너가 `SetXxxVolume(1)` 호출 → 1단계 자동 언뮤트로 언뮤트됨) → 퍼센트 텍스트·색상 갱신.

### 슬라이더 색상 / 버튼 표출 제어 로직 개요 (규칙 3·4·5)
- 공통 메서드(가칭 `RefreshMuteVisuals()`) 하나로 처리:
  - `bool muted = AudioManager.Instance?.IsMuted() ?? false;`
  - 슬라이더 Fill 색: `muted ? 빨강 : 초록` 을 3종 Fill에 동시 적용(규칙 3).
  - On/Off 버튼 표출(규칙 4·5, CanvasGroup 또는 SetActive는 규칙 5에 따라 CanvasGroup/표시 전환 방식 사용): `muted`면 OnButton 표출·OffButton 숨김, 아니면 반대.
- 호출 시점: `Initialize()` 마지막, 볼륨 패널 열 때(`ShowVolumePanel`), 뮤트 토글 후, 초기화 후, **슬라이더 onValueChanged 콜백 이후**(자동 언뮤트 반영, 규칙 26).

---

## 3단계: LobbySettingsView.cs 업데이트

근거: `GameSystemRules_UI.md` VolumeButtonContainer 규칙 1~7(로비는 뒤로 버튼 제외), 로비 탭 규칙 4(인게임과 동일 구조). `GameSystemRules_Sound.md` 규칙 23·26·27.

### 추가할 SerializeField (전부 신규)
| 신규 필드 | 타입 | 역할 |
|-----------|------|------|
| `_onButton` | Button | 전체 소리켜기(언뮤트) |
| `_offButton` | Button | 전체 음소거(뮤트) |
| `_resetButton` | Button | 초기화 |
| `_masterFill` / `_bgmFill` / `_sfxFill` | Image | 슬라이더 색상 제어 대상 |

> 로비 VolumeButtonContainer에는 **뒤로 버튼을 포함하지 않는다**(규칙 2). 화면 복귀는 기존 `_backButton`(탭 네비게이션)이 담당한다.

### 로직 개요
- 뮤트 토글 / 초기화 / 색·버튼 표출 갱신은 2단계(InGameSettingsUI)와 **동일한 동작**(두 화면 동일 규칙, 규칙 로비 탭 4).
- 갱신 호출 시점: `Initialize()` 마지막, 사운드 서브 화면 진입 시(`ShowSubView(_soundSubView)`), 뮤트 토글 후, 초기화 후, 슬라이더 onValueChanged 이후.

---

## 4단계: 신규 에디터 스크립트 작성

근거: 기존 스크립트 수정 금지(사용자 제약). `GameSystemRules_UI.md` 공통 규칙 2(앵커 비율)·5(CanvasGroup)·6(폰트 Maplestory Bold SDF).

### 4-1. `Assets/Editor/Setup/SetupInGameSettingsPanel.cs` (신규)
메뉴: `Hexiege/Setup/사운드 - 인게임 설정 패널(볼륨 버튼) 구성` (기존 메뉴명과 중복 회피).

생성/보강할 UI 계층 (기존 `SetupInGameVolumePanel` 결과 위에 증분):
```
(팝업 박스 _panel)
 ├── MainButtonContainer (CanvasGroup)          ← 기존
 │     ├── SoundButton                          ← 기존
 │     ├── ProfileButton   (신규, 미구현 표시)   ← 추가
 │     └── ForfeitButton                        ← 기존
 └── VolumePanel (CanvasGroup, 초기 숨김)         ← 기존
       ├── SliderContainer                       ← 기존(슬라이더 3종, Fill Image 참조 확보)
       │     ├── MasterRow(… Fill)
       │     ├── BGMRow(… Fill)
       │     └── SFXRow(… Fill)
       └── VolumeButtonContainer (신규)           ← 추가
             ├── OnButton    (전체 소리켜기)
             ├── OffButton   (전체 음소거)
             ├── ResetButton (초기화)
             └── BackButton  (뒤로 — 기존 _backButton 재사용 또는 이 컨테이너로 이동)
```
연결(SerializedObject): `_profileButton`, `_onButton`, `_offButton`, `_resetButton`, `_masterFill`/`_bgmFill`/`_sfxFill`(각 슬라이더 `Fill` 오브젝트의 Image).
표시/숨김은 CanvasGroup 또는 표시 전환(규칙 5), 폰트는 Maplestory Bold SDF(규칙 6), 배치는 앵커 비율(규칙 2).

### 4-2. `Assets/Editor/Setup/SetupLobbySettingPanel.cs` (신규)
메뉴: `Hexiege/Setup/사운드 - 로비 설정 패널(볼륨 버튼) 구성`.

생성/보강할 UI 계층 (기존 `SetupLobbySettingsTab` 결과의 SoundSubView에 증분):
```
LobbySettingsView
 └── SubViewContainer
       └── SoundSubView (CanvasGroup)
             ├── SliderContainer                 ← 기존(슬라이더 3종, Fill Image 참조 확보)
             └── VolumeButtonContainer (신규)      ← 추가 (뒤로 버튼 없음)
                   ├── OnButton    (전체 소리켜기)
                   ├── OffButton   (전체 음소거)
                   └── ResetButton (초기화)
```
연결(SerializedObject): `_onButton`, `_offButton`, `_resetButton`, `_masterFill`/`_bgmFill`/`_sfxFill`.
규칙 2·5·6 동일 적용. 뒤로 버튼은 만들지 않는다(기존 `_backButton` 사용).

> 두 신규 스크립트는 기존 스크립트의 헬퍼(`GetOrCreateUIChild`/`GetOrAdd`/`StretchFull`/폰트 로드)를 자체 복제하여 독립 동작하고, 이미 셋업된 씬에서 재실행해도 안전하도록 `GetOrCreate` 방식으로 작성한다.

---

## 5단계: 에디터 스크립트 실행 및 씬 저장

- 사용자에게 Unity 메뉴 실행 요청:
  1. `Hexiege/Setup/사운드 - 인게임 설정 패널(볼륨 버튼) 구성` → Game.unity 저장.
  2. `Hexiege/Setup/사운드 - 로비 설정 패널(볼륨 버튼) 구성` → Lobby.unity 저장.
- 실행 후 Inspector에서 신규 필드가 모두 연결됐는지 확인.
- 신규 에디터 스크립트는 1회성이므로 검증 완료 후 삭제 가능(WORKFLOW [5-2]).

---

## 위험 요소

| # | 위험 | 대응 |
|---|------|------|
| 1 | 뮤트 방식이 마스터 -80dB라, 뮤트 중 마스터 슬라이더를 조작하면 자동 언뮤트되며 그 값이 즉시 마스터로 적용됨(정상 동작이나 사용자가 "왜 소리가 켜지지?" 혼동 가능) | 규칙 26이 의도한 동작. UI가 색·버튼을 즉시 갱신해 상태를 명확히 보여줌 |
| 2 | 슬라이더 색상 대상이 Fill 이미지 하나뿐이라, 값이 0에 가까우면 색이 거의 안 보일 수 있음 | 결정 포인트에서 트랙 배경 병행 채색 여부 확인 |
| 3 | 신규 에디터 스크립트가 기존 스크립트 산출물 위에 겹쳐 생성 시 중복 오브젝트 위험 | `GetOrCreate` 방식 + 기존과 동일한 오브젝트명 사용으로 중복 방지 |
| 4 | 인게임 프로필 버튼은 화면 미구현 — 클릭 시 무반응이면 사용자 혼동 | 버튼만 생성, 핸들러 비움(향후 작업). 필요 시 "준비 중" 토스트는 별도 논의 |
| 5 | 두 UI의 뮤트 시각 갱신 로직이 중복됨(유지보수 부담) | 동일 규칙이므로 각 스크립트 내 사설 메서드로 통일. 공통 헬퍼 컴포넌트 분리는 완성도 검토 후 game-programmer 판단(규칙 7) |
| 6 | `Initialize()` 뮤트 복원 순서 오류 시(볼륨 적용 전에 뮤트 적용) 언뮤트처럼 보일 수 있음 | 반드시 "볼륨 3종 적용 → 그 뒤 뮤트 -80dB" 순서 준수 |

---

## 결정 포인트 (확정 완료)

1. **슬라이더 색상 적용 대상**: Fill + 트랙 Background 모두 채색 (**B 확정**)
2. **초기화 처리 방식**: UI 슬라이더 값 1.0 세팅 → 기존 onValueChanged 경유 (**A 확정**)
3. **인게임 프로필 버튼**: 에디터 스크립트에서 `_profileButton` 필드 연결만, 클릭 이벤트 없음 — 향후 프로필 구현 시 추가 (**확정**)
