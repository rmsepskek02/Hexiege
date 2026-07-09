# Plan — 인게임/로비 볼륨·프로필 UI 로직 연결

> 작업 폴더: `Assets/_Project/Docs/_Tasks/2026-07-09/06_09_ingame-lobby-volume-profile-ui/`
> 작성일: 2026-07-09
> 선행 문서: 같은 폴더의 `Research.md`

---

## 이 계획이 무엇을 하려는가 (자연어 설명)

`Research.md`에서 확인한 대로, 게임씬/로비씬의 설정·프로필 UI **레이아웃은 이미 완성**되어 있고 지금은 버튼을 눌러도 아무 일도 일어나지 않는 상태입니다. 이 계획은 그 껍데기 뒤에 **실제 동작 로직을 붙이는 작업**을 단계별로 정리합니다.

핵심은 다음과 같습니다.

- 사운드 매니저에 지금 없는 **"음소거(Mute)" 기능**을 새로 만듭니다. 이때 슬라이더에 저장된 볼륨 값은 그대로 두고, **실제로 나오는 소리만 무음으로 끄는 방식**을 씁니다(음소거를 풀면 원래 볼륨이 그대로 돌아옵니다).
- **전체 소리켜기 / 전체 음소거 / 초기화 / 뒤로가기** 버튼과 슬라이더의 색상(켜짐=초록, 음소거=빨강)을 규칙대로 연결합니다.
- 인게임 설정 메뉴의 **프로필 버튼**을 사운드 버튼과 똑같은 열기/닫기 방식으로 연결합니다(안쪽 내용은 아직 비워 둡니다).
- 로비 **SettingPanel**의 구버전 스크립트 잔재와 앵커 규칙 위반을 정리합니다.
- 인게임과 로비가 **똑같은 볼륨/뮤트 동작**을 하도록, 공통 로직을 한 곳(순수 C# 클래스)에 모아 양쪽이 함께 씁니다.

**중요**: 이 문서는 계획서까지입니다. 실제 코드 구현은 사용자가 이 Plan.md를 검토·승인한 뒤 `game-programmer` 에이전트에게 별도로 위임하여 진행합니다. (CLAUDE.md 규칙 3·11)

---

## ⚠️ 기존 로직 제거 관련 (WORKFLOW [4] · CLAUDE.md 기존 로직 제거 규칙)

이번 작업에서 `InGameSettingsUI.cs`와 `LobbySettingsView.cs`의 기존 코드를 신규 요구사항에 맞게 바꾸는 것은 "제거"가 아니라 **"요구사항 확장에 따른 재작성"**이다. 다만 재작성 과정에서 **완전히 삭제되는 기존 메서드/필드**(예: `LobbySettingsView`의 Profile 관련 `_profileButton`·`_profileSubView` 필드 및 관련 메서드)가 발생한다.

이 경우 **검증(사용자 실기 테스트 통과) 전까지는 "삭제" 대신 "주석 처리(비활성화)"를 기본**으로 한다.

- 삭제 후보: `LobbySettingsView`의 Profile 전용 필드/메서드(프로필은 이제 `ProfileView.cs` + `ProfilePanel`이 전담하므로 SettingPanel에서 불필요).
- 삭제해도 안전한 근거: 로비 프로필 기능은 이미 분리된 `ProfilePanel`(`ProfileView.cs`)이 담당하며(`Research.md` 2절 확인), `SettingPanel`은 사운드 등 게임 설정 전용이 되었다(GameSystemRules_UI 로비 규칙 1).
- 주석 처리된 코드의 **최종 삭제는 [6] 사용자 테스트 통과 후, [7] 문서/메모리 업데이트 전**에 수행한다.

---

## 아키텍처 제약 (위반 시 컴파일 오류/레이어 오염)

| 대상 | 레이어 | 제약 |
|------|--------|------|
| `AudioManager` | Presentation | 뮤트 추가도 Presentation 내부. `AudioMixer.SetFloat` 직접 제어(기존 방식 유지). |
| `UIColorConfig` | Infrastructure | 색상 토큰만 추가. Presentation 타입 참조 금지. |
| `VolumeControlBinder`(신규) | Presentation | 순수 C# 클래스(MonoBehaviour 아님). `AudioManager`(Presentation)·`UIColorConfig`(Infrastructure)만 의존. Slider/Button 등 UnityEngine.UI 참조는 Presentation이므로 허용. |
| InGame/Lobby View 스크립트 | Presentation | 씬 하이러키/프리팹은 공유하지 않음(각 씬 독립). 공통 로직만 `VolumeControlBinder`로 공유. |

`AudioManager.Instance` 접근은 항상 null-safe(`AudioManager.Instance?.`) 패턴 유지(GameSystemRules_Sound 규칙 5).

---

## 파일별 변경 계획

### A. `AudioManager.cs` (수정) — 뮤트 기능 신규 추가

**근거 규칙**: GameSystemRules_Sound 규칙 23(볼륨 컨트롤 버튼 — 전체 소리켜기/음소거), 규칙 24(상호 배타), 규칙 21(PlayerPrefs 저장/로드 패턴), 규칙 19(dB 변환), 규칙 26(음소거 내부 구현 — 본 작업에서 확정).

사용자 확정 결정사항 1·6 반영:

- **신규 PlayerPrefs 키**: `"Muted"`(제안값, 사용자 확인 가능) — 0/1 저장. 규칙 21과 동일한 저장/로드 패턴.
- **신규 공개 API**:
  - `SetMuted(bool muted)` — 뮤트 상태를 설정하고 즉시 반영 + `PlayerPrefs` 저장.
  - `IsMuted()` — 현재 뮤트 여부 반환(로드 포함).
- **뮤트 동작 방식(결정사항 1 — 저장값 보존형)**:
  - 뮤트 시: 저장된 논리 볼륨값(PlayerPrefs)은 **변경하지 않고**, `AudioMixer` 출력만 무음으로 강제한다. 무음 구현은 각 채널 dB를 -80dB로 설정(제안값; dB 변환 하한과 별개의 "완전 무음" 값). Master 채널만 -80dB로 눌러도 전체 무음이 되므로, Master만 제어할지 3채널 모두 제어할지는 구현 시 `game-programmer`가 믹서 라우팅 확인 후 결정(제안: Master 채널 무음이 가장 단순·안전).
  - 뮤트 해제 시: 저장된 논리 볼륨값을 다시 `ApplyVolume`으로 적용하여 원래 소리 복원.
- **내부 정합성**: 뮤트 상태에서 `SetMasterVolume/SetBgmVolume/SetSfxVolume`이 호출되어도, 저장은 그대로 하되 실제 출력은 뮤트가 우선하도록 처리(단, 결정사항 3에 따라 슬라이더 조작은 뮤트 해제를 유발하므로 이 경합은 UI 레이어에서 먼저 해제됨 — B 참조).
- **초기화 시점**: `Initialize()`에서 저장된 볼륨 로드 후, 저장된 뮤트 상태도 로드하여 뮤트면 무음 적용(규칙 20·21 흐름에 뮤트 로드 1줄 추가).

> 위험: `AudioMixer.SetFloat`은 실패 시 조용히 false 반환(2026-07-08 교훈). 뮤트 적용도 기존 `ApplyVolume`의 진단 로깅 경로를 재사용하여 실패를 감지한다.

### B. `VolumeControlBinder.cs` (신규) — 공통 볼륨/뮤트 UI 로직

**위치**: `Assets/_Project/Scripts/Presentation/UI/Common/VolumeControlBinder.cs`
**형태**: 순수 C# 클래스(MonoBehaviour 아님). InGame/Lobby 양쪽 View가 인스턴스를 생성해 위임.
**근거 규칙**: GameSystemRules_Sound 규칙 22(볼륨 UI 두 위치 동일 동작), 규칙 23~26; GameSystemRules_UI 공통 규칙 5(CanvasGroup 표시/숨김).

**책임(양쪽 씬 공통 동작을 한 곳에 집약 — 결정사항 7)**:

- 3개 슬라이더(Master/BGM/SFX)를 `AudioManager` 저장값으로 초기화하고, 값 변경 시 `Set*Volume` 호출.
- **슬라이더 조작 시 뮤트 자동 해제(결정사항 3)**: 어느 슬라이더든 사용자가 조작하면 `AudioManager.SetMuted(false)` 호출 후 저장값대로 소리 복원 → 버튼 표시·색상 갱신.
- **전체 음소거/전체 소리켜기 버튼(결정사항 4, 규칙 23·24)**: 뮤트면 "전체 소리켜기"만, 켜짐이면 "전체 음소거"만 표시. 표시/숨김은 **CanvasGroup**(alpha/blocksRaycasts/interactable)로 상호 배타 처리(공통 규칙 5).
- **초기화 버튼(결정사항 2, 규칙 25)**: Master/BGM/SFX를 모두 1.0으로 설정 **+ 뮤트 해제**. 슬라이더 위치·색상·버튼 표시 모두 갱신.
- **슬라이더 색상(결정사항 5, 규칙 26)**: 소리 켜짐=`UIColorConfig.soundOnColor`, 음소거=`UIColorConfig.soundMutedColor`. 하드코딩 금지, `_colorConfig?.soundOnColor ?? Color.green` 안전 가드 패턴.
- **뒤로 버튼(규칙 23)**: 볼륨 컨트롤 영역을 닫고 이전 화면 복귀 — 실제 패널 전환 콜백은 각 View가 주입(씬마다 하이러키가 다르므로).

> 씬별 하이러키/프리팹은 공유하지 않으므로(결정사항 7), Binder는 Slider·Button·CanvasGroup·색상 대상 이미지 등 **참조를 생성자/셋업 메서드로 주입받는** 형태로 설계한다. 각 View는 자기 씬의 오브젝트를 찾아 Binder에 넘긴다.

### C. `UIColorConfig.cs` (수정) — 슬라이더 색상 토큰 추가

**근거 규칙**: GameSystemRules_Sound 규칙 26; 기존 SSOT 패턴(결정사항 5).

- 신규 필드 2개 추가:
  - `public Color soundOnColor = Color.green;` — 소리 켜짐 상태 슬라이더 색상.
  - `public Color soundMutedColor = Color.red;` — 음소거 상태 슬라이더 색상.
- 기존 `goldInsufficientColor` 등과 동일하게 `[Header]`/`[Tooltip]` 주석 부여(초급자용 상세 주석 — CLAUDE.md 규칙 8).

### D. Game.unity 설정 스크립트 (수정) — 인게임 프로필·볼륨 버튼 연결

**대상**: `InGameSettingsUI.cs` (구버전 → 요구사항 확장 재작성).
**근거 규칙**: GameSystemRules_UI 인게임 설정 규칙 6(프로필 서브 패널), 공통 규칙 5(CanvasGroup); GameSystemRules_Sound 규칙 22~26.

- **프로필 버튼(결정사항 11, 규칙 6)**: 사운드 버튼과 **동일한 서브 패널 전환 패턴**으로 연결. 프로필 버튼 탭 → `MainButtonContainer` CanvasGroup 숨김 + 프로필 서브 패널 CanvasGroup 표시. 서브 패널 뒤로가기 → 역전환. **내부 콘텐츠는 빈 상태/플레이스홀더**(내부 구성 미정 — Research 미정 항목).
- **VolumeButtonContainer 4버튼(OnButton/OffButton/ResetButton/BackButton)**: `VolumeControlBinder`에 위임하여 연결. Game씬 `VolumeButtonContainer`는 앵커가 이미 정상이므로 **구조 변경 없이 로직만 연결**(Research 1절 확인).
- **신규 Serialized 필드**: ProfileButton, 프로필 서브 패널 CanvasGroup, OnButton/OffButton/ResetButton/BackButton, 3개 Slider 및 색상 대상 이미지 등. → Editor 1회성 스크립트로 자동 배선(F 참조).
- 완전 삭제되는 구버전 메서드/필드가 있으면 상단 "기존 로직 제거" 규칙에 따라 주석 처리 우선.

### E. Lobby.unity SettingPanel 스크립트 (수정 + 이동) — Setting 전용 축소 재작성

**대상**: `LobbySettingsView.cs`.
**근거 규칙**: GameSystemRules_UI 로비 규칙 1(ProfilePanel/SettingPanel 분리), 규칙 2(사운드 규칙 참조), 공통 규칙 2(앵커)·규칙 5(CanvasGroup); GameSystemRules_Sound 규칙 22~26.

- **스크립트 루트 이동(결정사항 8)**: 컴포넌트를 현재 자식 오브젝트에서 `SettingPanel` 패널 루트로 이동하여 다른 탭 패널 컨벤션(스크립트+CanvasGroup을 루트에)에 맞춘다. (Editor 1회성 스크립트 — F 참조)
- **Setting 전용 축소 재작성(결정사항 8)**: Profile 관련 필드/로직(`_profileButton`, `_profileSubView` 등) 제거(→ 주석 처리 우선). 사운드 볼륨/뮤트/리셋/볼륨버튼 로직은 `VolumeControlBinder`에 위임하여 추가.
- **2단 네비게이션 유지(결정사항 9)**: `MainView → ButtonList → SoundButton → SubViewContainer → SoundSubView` 구조는 **삭제·단순화하지 않고 유지**(향후 다른 설정 항목 추가 예정).
- **클래스명 변경 제안(사용자 확인 필요)**: 역할이 Setting 전용으로 좁혀졌으므로 `LobbySettingsView` → `LobbySettingView`(현 이름 유지도 가능). 최종 명칭은 사용자 확인 후 결정 — **미정(제안값)**.
- **VolumeButtonContainer 앵커 재계산(결정사항 10, 공통 규칙 2)**:
  - `sizeDelta`/`anchoredPosition`을 **(0,0)으로 되돌린다**(고정 픽셀 보정 제거).
  - `anchorMax.y`를 `SliderContainer`의 `anchorMin.y`(=0.15)를 **넘지 않도록** 재계산하여 겹침 제거.
  - **제안값(사용자 확인 필요)**: `VolumeButtonContainer` anchorMin=(0.15, 0.0), anchorMax=(0.85, 0.15), sizeDelta=(0,0), anchoredPosition=(0,0). 정확한 최종 비율은 사용자가 Editor에서 결과를 보고 조정 — **미정(제안값)**. (Game씬 쪽은 이미 정상이라 변경 없음)

### F. Editor 1회성 스크립트 (신규, 실행 후 삭제 가능) — WORKFLOW [5-2]

씬/Inspector 수동 작업을 자동화하는 1회성 Editor 스크립트. 메뉴 경로는 `Hexiege/...` 형태. 사용자에게 실행 요청 후 확인.

1. **(a) 신규 Serialized 필드 자동 연결**: InGame 설정 스크립트 + Lobby Setting 스크립트의 신규 필드(ProfileButton, On/Off/Reset/Back 버튼, 3 Slider, 색상 대상 이미지, 서브 패널 CanvasGroup 등)를 씬 오브젝트에서 찾아 자동 배선.
2. **(b) Lobby `VolumeButtonContainer` 앵커값 자동 적용**: E의 제안 앵커값(사용자 확인 후 확정값)으로 `anchorMin`/`anchorMax`/`sizeDelta`/`anchoredPosition` 설정.
3. **(c) Setting 스크립트 컴포넌트 이동**: Lobby `LobbySettingsView`(→ 재작성 스크립트)를 자식 오브젝트에서 `SettingPanel` 루트로 이동. (컴포넌트 이동은 값 보존에 주의 — `game-programmer`가 안전한 방식으로 구현)
4. **(d) 씬 반영**: 변경 오브젝트에 `EditorUtility.SetDirty()` + 씬 저장(`EditorSceneManager.MarkSceneDirty`/`SaveScene`). (2026-07-08 교훈: SetDirty 없으면 씬 저장에 반영 안 됨)

---

## 예상 위험 요소

| 위험 | 설명 | 완화 |
|------|------|------|
| 뮤트-슬라이더 경합 | 뮤트 중 슬라이더 조작·초기화 등에서 실제 출력과 저장값이 어긋날 수 있음 | 결정사항 3(슬라이더 조작 시 뮤트 우선 해제)·결정사항 2(초기화 시 뮤트 해제)로 상태 전이를 단일화 |
| `AudioMixer.SetFloat` 무음 실패 | 파라미터 이름 불일치 시 조용히 실패 | 기존 `ApplyVolume` 진단 로깅 경로 재사용 |
| 컴포넌트 루트 이동 시 참조 유실 | 자식→루트 이동 과정에서 Serialized 참조가 끊길 수 있음 | Editor 스크립트로 이동 후 (a)에서 재배선, 실기 확인 |
| 앵커 재계산 오차 | 제안 앵커값이 실제 화면에서 어긋날 수 있음 | 사용자 Editor 실기 확인 후 확정값 적용 |
| 로비 로그아웃/계정연동 손상 | SettingPanel 정리가 ProfilePanel 기능에 영향 | ProfilePanel/`ProfileView.cs`는 건드리지 않음(결정사항 12) |
| 씬 저장 누락 | SetDirty 누락 시 배선/앵커가 저장 안 됨 | (d) 단계에서 SetDirty + 씬 저장 필수 |

---

## 규칙 문서 후속 반영(작업 완료 후, WORKFLOW [12])

- **GameSystemRules_Sound 규칙 26**의 "음소거 내부 구현 미정" 노트: 이번 작업에서 "저장값 보존 + Mixer 무음 강제 + PlayerPrefs 뮤트 저장" 방식으로 확정 → 실기 통과 후 노트 삭제하고 규칙 보완.
- **GameSystemRules_UI 인게임 설정 규칙 6**의 "프로필 서브 패널 내부 구성 미정" 노트: 이번 작업은 열기/닫기 패턴만 확정하므로 노트 유지(내부 콘텐츠는 별도 작업).

---

## 작업 순서 요약 (승인 후 game-programmer 위임)

1. `UIColorConfig.cs`에 색상 토큰 2개 추가(C).
2. `AudioManager.cs` 뮤트 API/상태/저장 추가(A).
3. `VolumeControlBinder.cs` 신규 작성(B).
4. Game 설정 스크립트 재작성 — 프로필·볼륨 버튼 연결(D).
5. Lobby Setting 스크립트 축소 재작성(E).
6. Editor 1회성 스크립트로 배선·앵커·컴포넌트 이동·씬 저장(F).
7. (사용자 실기 → 필요 시 앵커/무음 방식 조정) → 주석 처리 코드 최종 삭제 → 문서/규칙 반영.

> 실제 구현 착수는 **사용자의 Plan.md 명시적 승인 이후**에만 진행한다(WORKFLOW [4], CLAUDE.md 규칙 11).
