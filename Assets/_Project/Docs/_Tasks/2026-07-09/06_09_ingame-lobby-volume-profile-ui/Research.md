# Research — 인게임/로비 볼륨·프로필 UI 로직 연결

> 작업 폴더: `Assets/_Project/Docs/_Tasks/2026-07-09/06_09_ingame-lobby-volume-profile-ui/`
> 작성일: 2026-07-09

---

## 이 작업이 무엇이고 왜 하는가 (자연어 설명)

사용자가 Unity Editor에서 **게임씬(인게임)과 로비씬의 설정/프로필 UI 레이아웃을 이미 직접 재설계**해 두었습니다. 이번 작업은 이렇게 완성된 UI(버튼, 슬라이더, 패널)의 **껍데기 뒤에 실제로 동작하는 로직(코드)을 연결**하는 것입니다. 새 화면을 만드는 작업이 아니라, 이미 배치된 화면에 "이 버튼을 누르면 무슨 일이 일어난다"를 붙이는 작업입니다.

구체적으로 바뀐 UI는 세 가지입니다.

1. **인게임 설정 메뉴**에 **프로필 버튼**이 새로 추가되었습니다. 지금은 눌러도 아무 일도 일어나지 않습니다(로직 미연결).
2. **인게임 사운드 볼륨 패널**에 **전체 소리켜기 / 전체 음소거 / 초기화 / 뒤로가기** 4개 버튼이 담긴 `VolumeButtonContainer`가 새로 추가되었습니다. 역시 로직 미연결 상태입니다.
3. **로비씬**에서는 예전에 "설정" 탭 하나에 합쳐져 있던 프로필/사운드 기능이 **ProfilePanel(프로필 전용)과 SettingPanel(설정/사운드 전용)이라는 완전히 분리된 두 개의 탭**으로 나뉘었습니다.

이 UI들을 실제로 동작시키려면 사운드 매니저에 지금은 없는 **"음소거(Mute)" 기능**을 새로 추가해야 하고, 조사 과정에서 발견된 **구조적 문제(구버전 스크립트 잔재, 앵커 규칙 위반)도 함께 정리**해야 합니다. 이 문서는 그 현재 상태를 코드/씬 파일 근거와 함께 기록합니다. 실제 구현 방법과 규칙 근거는 같은 폴더의 `Plan.md`에서 다룹니다.

---

## 사전 정리 완료 사항 (이번 작업과 별개)

이번 세션 초반에, 이번 볼륨/뮤트 작업과는 **별개의 선행 죽은 코드 정리**가 이미 완료되었다. 참고용으로만 기록하며, 이번 작업 항목(Plan.md)에는 포함하지 않는다.

- `UIColorConfig.cs`의 `confirmButtonColor` / `cancelButtonColor` 필드가 삭제되었다. `ConfirmPopup`이 색상 tint 방식에서 이미지 에셋 기반으로 바뀌어 더 이상 버튼 색상 tint가 필요 없어졌기 때문이다.
- 이에 따라 `ConfirmPopup.cs`의 `Awake()`와 `_colorConfig` 필드도 함께 제거되었다.
- **검증(직접 파일 확인)**: 현재 `UIColorConfig.cs`에 `confirmButtonColor`/`cancelButtonColor`가 존재하지 않음을 확인했다.

---

## 현재 상태 — 씬 파일(YAML) 파싱 및 코드 직접 확인 결과

아래 내용은 씬 파일과 스크립트를 직접 읽어 검증한 사실이며, 추정이 아니다.

### 1. Game.unity — InGameSettingsPanel

경로: `[UI] > SafeAreaContainer > InGameSettingsPanel`

```
InGameSettingsPanel  (CanvasGroup, 스크립트: InGameSettingsUI.cs ← 구버전)
  Panel  (anchorMin 0.1,0.2  anchorMax 0.9,0.8)
    CloseButton
    MainButtonContainer  (VerticalLayoutGroup)
      SoundButton
      ProfileButton          ← 신규 추가된 오브젝트, 로직 미연결
      ForfeitButton
    VolumePanel
      SliderContainer  (VerticalLayoutGroup) → MasterRow / BGMRow / SFXRow
        (각 Row: HorizontalLayoutGroup + ContentSizeFitter, 자식: Label / Slider / ValueText)
      VolumeButtonContainer  (VerticalLayoutGroup, anchorMin=(0,0) anchorMax=(1,0.5), sizeDelta=(0,0) ← 앵커 정상)
        OnButton / OffButton / ResetButton / BackButton  (각각 Label 자식 보유)  ← 신규 추가, 로직 미연결
```

- `InGameSettingsPanel`에 부착된 `InGameSettingsUI.cs`는 **구버전**으로, 신규 UI를 다룰 필드가 전혀 없다: `ProfileButton`, `OnButton`, `OffButton`, `ResetButton` 등에 대한 Serialized 참조가 없어 신규 오브젝트들과 **미연결 상태**다.
- Game씬 쪽 `VolumeButtonContainer`는 `sizeDelta=(0,0)`로 **앵커가 이미 정상**이다. 따라서 이 컨테이너는 앵커 구조 변경이 필요 없고 **로직 연결만** 하면 된다.

### 2. Lobby.unity — ProfilePanel (이미 부분 정리됨)

경로: `[UI] Canvas > SafeAreaContainer > LobbyRoot > ContentArea > ProfilePanel`

```
ProfilePanel  (CanvasGroup + ProfileView.cs 직속 부착)   ← 탭 패널 컨벤션 준수
  LobbyProfileView  (순수 컨테이너, 스크립트 없음 — 구버전 LobbySettingsView.cs를 떼어내고 이름 변경 완료)
    BackButton
    MainView → ButtonList → ProfileButton / LogoutButton
    SubViewContainer → ProfileSubView
```

- `ProfilePanel`은 다른 탭 패널(`BattlePanel`/`ShopPanel`/`RankingPanel`)과 **동일한 컨벤션**(View 스크립트 + `CanvasGroup`을 탭 패널 루트에 직접 부착)을 이미 따르고 있다.
- `ProfileView.cs` (`Presentation/UI/Views/Lobby/Profile/`)는 **계정 연동(Google/이메일)/로그아웃만** 처리하며, "전적" 표시 기능은 없다.
- 이 패널의 기존 로그아웃/계정 연동 기능은 **정상 동작 상태로 유지**되어야 하며, 이번 작업에서 건드리지 않는다.

### 3. Lobby.unity — SettingPanel (미정리 상태 — 문제 있음)

```
SettingPanel  (CanvasGroup만 있음, 스크립트 없음)   ← 컨벤션 위반: 스크립트가 루트에 없음
  LobbySettingsView  (구버전 LobbySettingsView.cs 그대로 부착 — Profile 관련 필드도 아직 보유, 이제 불필요)
    BackButton
    MainView → ButtonList → SoundButton
    SubViewContainer → SoundSubView  (anchorMin=(0,0.55)  anchorMax=(1,0.95))
      SliderContainer  (anchorMin=(0.05,0.15)  anchorMax=(0.95,0.85)) → MasterRow / BGMRow / SFXRow
      VolumeButtonContainer  (anchorMin=(0.15,0)  anchorMax=(0.85,0.55),
                              sizeDelta=(0, 587.3472),  anchoredPosition=(0, -685.23834))  ← 고정 픽셀값 사용
        OnButton / OffButton / ResetButton
```

#### 발견된 문제 1 — 앵커 규칙 위반 (GameSystemRules_UI 공통 규칙 2)

`VolumeButtonContainer`의 `anchorMax.y = 0.55`가 `SliderContainer`의 `anchorMin.y = 0.15`보다 커서, **앵커 비율만 보면 두 컨테이너가 세로 0.15~0.55 구간에서 겹친다**. 현재는 큰 `sizeDelta.y`(587.3472)와 음수 `anchoredPosition.y`(-685.23834)라는 **고정 픽셀값으로 억지로 컨테이너를 화면 아래로 밀어내** 시각적으로만 겹쳐 보이지 않게 만든 상태로 판단된다. 고정 픽셀 보정이므로 해상도가 달라지면 레이아웃이 깨진다. (공통 규칙 2: 고정 픽셀 크기 대신 앵커 비율 기반 배치, `sizeDelta` 등 고정 픽셀값 사용 금지)

> 참고: Game씬의 `VolumeButtonContainer`는 `sizeDelta=(0,0)`으로 정상이므로 이 문제는 **Lobby SettingPanel에만** 해당한다.

#### 발견된 문제 2 — 탭 패널 스크립트 부착 컨벤션 위반

`LobbySettingsView.cs`가 `SettingPanel` 루트가 아닌 **자식 오브젝트에 부착**되어 있어, 다른 모든 탭 패널의 "View 스크립트 + `CanvasGroup`을 패널 루트에" 컨벤션과 어긋난다. 또한 이 스크립트는 프로필/사운드 겸용 구버전이라 **Profile 관련 필드(`_profileButton`, `_profileSubView` 등)를 아직 보유**하고 있는데, 프로필이 별도 `ProfilePanel` 탭으로 분리된 지금은 불필요하다.

#### 로비 탭 패널 스크립트 부착 컨벤션 (직접 확인)

`BattlePanel`, `ShopPanel`, `RankingPanel`, `ProfilePanel` 모두 **View 스크립트 + `CanvasGroup`을 탭 패널 루트 GameObject에 직접 부착**하는 동일 패턴을 쓴다. `SettingPanel`만 이 컨벤션에서 벗어나 있다.

---

## 기존 스크립트/설정 현황 (직접 확인)

### AudioManager.cs (`Presentation/Audio/`)

- 볼륨 API: `SetMasterVolume/GetMasterVolume`, `SetBgmVolume/GetBgmVolume`, `SetSfxVolume/GetSfxVolume` 존재.
- 내부 헬퍼: `SetVolume(param, prefKey, value)`(믹서 적용 + `PlayerPrefs.Save()`), `ApplyVolume(param, value)`(dB 변환 + `AudioMixer.SetFloat`), `LoadVolume(prefKey)`(기본값 1.0).
- PlayerPrefs 키: `"MasterVolume"` / `"BGMVolume"` / `"SFXVolume"`.
- dB 변환: `Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f` (하한 0.0001로 -∞ 방지).
- `AudioMixer.SetFloat` 실패 시 `Debug.LogWarning` 진단 로깅 이미 존재 (2026-07-08 교훈 반영됨).
- **뮤트(Mute) 개념이 전혀 없음.** `SetMuted`/`IsMuted`/뮤트용 PlayerPrefs 키 없음. → 이번 작업에서 신규 추가 필요.

### UIColorConfig.cs (`Infrastructure/Config/`)

- 보유 필드: `normalTextColor`(white), `goldInsufficientColor`(red), `populationFullColor`(red), `winColor`, `loseColor`, `demolishRefundColor`(green).
- **`soundOnColor` / `soundMutedColor` 없음.** → 이번 작업에서 신규 추가 필요(하드코딩 금지, 기존 SSOT 패턴 재사용).
- 각 UI 컴포넌트는 `_colorConfig?.field ?? 폴백` 안전 가드 패턴으로 참조하도록 설계됨.

### 관련 규칙 문서 상태

`GameSystemRules_UI.md`와 `GameSystemRules_Sound.md`는 이번 세션 초반에 신규 규칙이 이미 반영된 **최신 상태**임을 확인했다.

- `GameSystemRules_UI.md`: "인게임 설정 메뉴 → 프로필 서브 패널 규칙 6", "로비 설정/프로필 UI 규칙 1·2".
- `GameSystemRules_Sound.md`: "볼륨 규칙 22~26"(볼륨 UI 위치, 볼륨 컨트롤 버튼 구성, 상호 배타 표시, 초기화, 슬라이더 색상). 규칙 26에는 "음소거 내부 구현 방식 미정" 노트가 아직 남아 있음.

---

## 영향 범위 요약

| 대상 | 영향 | 이번 작업 범위 |
|------|------|---------------|
| `AudioManager.cs` | 뮤트 API/상태/저장 신규 추가 | 포함 |
| `UIColorConfig.cs` | `soundOnColor`/`soundMutedColor` 신규 필드 | 포함 |
| `InGameSettingsUI.cs`(또는 후속 스크립트) | 프로필 버튼·볼륨 버튼 로직 연결, 신규 Serialized 필드 | 포함 |
| `LobbySettingsView.cs`(SettingPanel 스크립트) | Setting 전용 축소 재작성 + 루트 이동 | 포함 |
| 공통 볼륨/뮤트 로직 클래스 (신규, `Presentation/UI/Common/`) | InGame/Lobby 공유 순수 C# 클래스 | 포함 |
| `ProfileView.cs` / 로비 ProfilePanel 콘텐츠 | 로그아웃/계정연동 유지, 전적 등은 별도 작업 | **범위 밖** |
| 인게임 프로필 서브 패널 내부 콘텐츠 | 열기/닫기 패턴만 연결, 내부는 플레이스홀더 | **범위 밖(내부 콘텐츠 미정)** |
| Game.unity / Lobby.unity 씬 | Serialized 필드 배선, Lobby 앵커 재계산, Setting 스크립트 루트 이동 | 포함(Editor 1회성 스크립트) |

---

## 미정 / 사용자 확인 필요 항목 (추정 금지 — CLAUDE.md 규칙 10)

- **인게임 프로필 서브 패널 내부 구성**: 어떤 정보(전적/계정)를 어떻게 표시할지 미정. 이번 작업은 사운드 버튼과 동일한 CanvasGroup 열기/닫기 패턴만 연결하고 내부는 빈 상태/플레이스홀더로 둔다. (GameSystemRules_UI 인게임 설정 규칙 6의 "미정" 노트와 동일)
- **Lobby SettingPanel `VolumeButtonContainer`의 최종 앵커 수치**: 겹침이 사라지도록 재계산하되, 정확한 최종 비율은 사용자가 Editor에서 결과를 보고 조정할 수 있도록 Plan.md에 **제안값(사용자 확인 필요)**으로만 기재한다.
- **로비 ProfilePanel "전적" 표시 기능**: 별도 작업. 이번 범위 밖.
