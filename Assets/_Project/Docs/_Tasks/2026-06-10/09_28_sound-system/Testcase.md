# Testcase — 사운드 시스템 구축

## 개요

이 문서는 사운드 시스템 구현에 대한 실기 테스트 케이스 목록입니다.
BGM 크로스페이드, SFX 재생 및 풀 관리, 볼륨 채널 제어, 멀티플레이 독립 재생, 개발 환경 안전성을 검증합니다.

Inspector 작업(AudioMixer, SoundConfig 클립 할당, AudioManager 연결) 완료 후 테스트를 진행합니다.

---

## BGM 테스트

### SINGLE-BGM-01: Login 씬 진입 시 Login BGM 자동 재생

**전제:** AudioManager와 SoundConfig가 올바르게 연결된 상태, Login BGM 클립이 할당되어 있음

**동작:**
1. 게임을 처음 실행하여 Login 씬에 진입한다

**기댓값:**
- Login BGM이 즉시 재생된다

**결과:**

---

### SINGLE-BGM-02: Lobby 씬 전환 시 BGM 크로스페이드

**전제:** Login 씬에서 Login BGM이 재생 중인 상태

**동작:**
1. 로그인 완료 후 Lobby 씬으로 전환한다

**기댓값:**
- Login BGM이 1초에 걸쳐 페이드아웃되며 Lobby BGM이 페이드인된다
- 두 BGM이 자연스럽게 겹치며 전환된다

**결과:**

---

### SINGLE-BGM-03: Game 씬 로딩 직후 BGM 유지

**전제:** Lobby 씬에서 Lobby BGM이 재생 중인 상태

**동작:**
1. 싱글플레이 시작 버튼을 눌러 Game 씬으로 전환한다
2. 게임 로딩이 완료된 직후, 유닛과 건물이 배치되기 전 상태를 확인한다

**기댓값:**
- Lobby BGM이 그대로 재생 유지된다
- Battle BGM으로 전환되지 않는다

**결과:**

---

### SINGLE-BGM-04: 게임 시작 시 Battle BGM 전환

**전제:** Game 씬 로딩이 완료되어 있고 Lobby BGM이 재생 중인 상태

**동작:**
1. 게임이 실제로 시작되어 유닛과 건물이 배치된다

**기댓값:**
- Lobby BGM이 크로스페이드되며 Battle BGM으로 전환된다

**결과:**

---

### SINGLE-BGM-05: 게임 종료 시 GameEnd BGM 전환

**전제:** Game 씬에서 Battle BGM이 재생 중인 상태

**동작:**
1. 상대방 성채를 파괴하거나 패배하여 게임이 종료된다

**기댓값:**
- Battle BGM이 크로스페이드되며 게임종료 BGM으로 전환된다

**결과:**

---

### SINGLE-BGM-06: BGM 클립 미할당 시 무음 전환

**전제:** SoundConfig에서 GameEnd BGM 클립이 비어있는 상태

**동작:**
1. 게임을 종료한다

**기댓값:**
- Battle BGM이 페이드아웃되고 무음 상태로 전환된다
- 이전 BGM이 유지되지 않는다

**결과:**

---

### SINGLE-BGM-07: 크로스페이드 도중 새 BGM 요청 시 즉시 전환

**전제:** 크로스페이드가 진행 중인 상태 (예: Lobby BGM에서 Battle BGM으로 전환 중)

**동작:**
1. 크로스페이드 진행 중에 즉시 게임이 종료되어 GameEnd BGM 요청이 발생한다

**기댓값:**
- 진행 중인 크로스페이드가 중단된다
- 즉시 GameEnd BGM으로 새 크로스페이드가 시작된다

**결과:**

---

## SFX 테스트

### SINGLE-SFX-01: 유닛 공격 시 SFX 재생

**전제:** Game 씬에서 SoundConfig에 유닛 공격 SFX 클립이 할당되어 있음

**동작:**
1. 유닛이 적을 공격하는 애니메이션이 재생된다

**기댓값:**
- 공격 VFX와 동시에 공격 SFX가 재생된다

**결과:**

---

### SINGLE-SFX-02: 유닛 사망 시 SFX 재생

**전제:** Game 씬에서 SoundConfig에 유닛 사망 SFX 클립이 할당되어 있음

**동작:**
1. 유닛 체력이 0이 되어 사망한다

**기댓값:**
- 사망 VFX와 동시에 사망 SFX가 재생된다

**결과:**

---

### SINGLE-SFX-03: 동시 SFX 8개 한도 초과 시 드랍

**전제:** Game 씬에서 SFX 최대 동시 재생 수가 8개로 설정되어 있음

**동작:**
1. 여러 유닛이 동시에 공격하여 SFX 요청이 8개를 초과한다

**기댓값:**
- 처음 8개 SFX는 정상 재생된다
- 초과된 요청은 조용히 무시된다 (오류 발생 없음)

**결과:**

---

### SINGLE-SFX-04: 일시정지 중 SFX 재생 후 정상 반환

**전제:** Game 씬 싱글플레이에서 설정 메뉴로 게임이 일시정지된 상태 (시간 흐름 정지)

**동작:**
1. 일시정지 상태에서 SFX가 재생된다 (예: 설정 패널의 UI 효과음)
2. SFX 재생이 완료될 때까지 기다린다

**기댓값:**
- SFX 재생이 완료된 후 정상적으로 풀에 반환된다
- 일시정지 해제 후에도 SFX 재생 한도(8개)가 정상 동작한다

**결과:**

---

## 볼륨 테스트

### SINGLE-VOL-01: 볼륨 슬라이더 조절 시 즉시 적용

**전제:** Game 씬에서 설정 패널의 볼륨 슬라이더가 표시된 상태

**동작:**
1. Master 슬라이더를 0으로 내린다
2. Master 슬라이더를 다시 최대값으로 올린다
3. BGM 슬라이더를 0으로 내린다
4. SFX 슬라이더를 0으로 내린다

**기댓값:**
- 슬라이더를 움직이는 즉시 해당 채널 볼륨이 변경된다
- BGM 슬라이더 0 → BGM 무음
- SFX 슬라이더 0 → SFX 무음
- Master 슬라이더 0 → 전체 무음

**결과:**

---

### SINGLE-VOL-02: 볼륨 설정 재시작 후 유지

**전제:** 볼륨 슬라이더에서 BGM을 50%로 설정한 상태

**동작:**
1. 게임을 완전히 종료한다
2. 게임을 재시작하여 Lobby 씬에 진입한다
3. 설정 패널을 열어 슬라이더 위치를 확인한다

**기댓값:**
- BGM 볼륨이 이전에 설정한 50%로 복원된다
- 슬라이더 위치도 50%를 가리킨다

**결과:**

---

### SINGLE-VOL-03: 볼륨 초기값 최대 (저장값 없을 때)

**전제:** 볼륨 설정이 저장되지 않은 최초 실행 상태 (PlayerPrefs 초기화 상태)

**동작:**
1. 게임을 처음 실행하여 설정 패널을 연다

**기댓값:**
- Master, BGM, SFX 슬라이더가 모두 최대값(1.0)에 위치한다

**결과:**

---

## 멀티플레이 테스트

### MULTI-SFX-01: 멀티 사망 이펙트 각 클라이언트 독립 재생

**전제:** 멀티플레이 세션(Host + Client 구성)에서 SFX 클립이 할당된 상태

**동작:**
1. Host 화면에서 유닛이 사망한다

**기댓값:**
- Host와 Client 모두 각자 화면에서 사망 SFX가 재생된다
- 네트워크를 통한 SFX 동기화 메시지 없이 각 클라이언트가 로컬에서 재생한다

**결과:**

---

## 개발 환경 테스트

### SINGLE-DEV-01: Game 씬 직접 진입 시 오류 없음

**전제:** Login 씬을 거치지 않고 Unity Editor에서 Game 씬을 직접 실행 (AudioManager 없음)

**동작:**
1. Unity Editor에서 Game 씬을 직접 Play한다

**기댓값:**
- 사운드가 재생되지 않지만 오디오 관련 오류나 NullReferenceException이 발생하지 않는다
- 게임 플레이는 정상 동작한다

**결과:**

---

## 정적 분석 결과 (qa-tester)

### 분석 기준

Plan.md 신규/수정 파일 목록 기준으로 구현된 코드를 검토함.
GameSystemRules_Sound.md 규칙 1~22 전체 준수 여부 확인.

### 아키텍처 의존성

- SoundConfig: `Infrastructure/Config` 레이어 배치 — 규칙 3 준수
- AudioManager: `Presentation/Audio` 레이어 배치, `SingletonMonoBehaviour<AudioManager>` 상속 — 규칙 2 준수
- SoundConfig 키: `UnitType`, `BuildingType` (Domain 타입만 사용) — 규칙 3 준수
- `UiEffectKey` → AudioManager 직접 보유(`List<UiSoundEntry>`) — 규칙 4 준수

### BGM 로직

- Initialize()에서 `SceneManager.GetActiveScene().name` 확인 후 즉시 재생 — 규칙 7 준수
- `SceneManager.activeSceneChanged` 구독: "Login" → Login BGM, "Lobby" → Lobby BGM — 규칙 6 준수
- `GameEvents.OnGameStarted` → Battle BGM 크로스페이드 — 규칙 9 준수 (로딩 중 Lobby BGM 유지)
- `GameEvents.OnGameEnd` → 게임종료 BGM (승패 구분 없음) — 규칙 11 V1 준수
- BGM 클립 null 시 무음 전환 (이전 BGM 유지 안 함) — 규칙 10 준수
- 크로스페이드 도중 새 전환 요청 시 StopCoroutine 후 재시작 — 규칙 8 준수

### SFX 풀 및 동시 재생 한도

- `_maxConcurrentSfx = 8` 기본값 — 규칙 13 준수
- 한도 초과 시 드랍(무시) — 규칙 13 준수
- `spatialBlend = 0` (2D 고정) — 규칙 12 준수
- 네트워크 SFX 동기화 없음 — 규칙 14 준수

### VFX+SFX 쌍 호출 (3곳 확인)

- `UnitView.cs:1520` OnAttackHit(): EffectManager VFX + AudioManager SFX 연달아 호출 — 규칙 15 준수
- `UnitView.cs:481` OnUnitDied 핸들러: EffectManager VFX + AudioManager SFX 연달아 호출 — 규칙 15 준수
- `NetworkUnit.cs:183` 멀티플레이 사망 동기화: EffectManager VFX + AudioManager SFX 연달아 호출 — 규칙 15 준수

### null-safe 처리

- 모든 `AudioManager.Instance` 호출에 `?.` 연산자 사용 — 규칙 5 준수
- Game 씬 직접 진입 시 Instance null → 사운드 없이 정상 동작

### 볼륨 제어

- 3채널 구성: MasterVolume, BGMVolume, SFXVolume (Exposed Parameters) — 규칙 18 준수
- 0~1 범위 float → `Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f` dB 변환 — 규칙 19 준수
- 저장값 없을 때 기본값 1.0 — 규칙 20 준수
- PlayerPrefs 저장/로드 — 규칙 21 준수
- 볼륨 슬라이더: Lobby 씬 + Game 씬(InGameSettingsUI) 양쪽 배치 — 규칙 22 준수

### AudioMixerGroup 연결

- BGM AudioSource A/B → BGM Group 연결 — 규칙 17 준수
- SFX 풀 AudioSource → SFX Group 연결 — 규칙 17 준수
- Initialize()에서 _bgmGroup / _sfxGroup null 시 LogWarning — 규칙 17 준수

### QA 수정 사항 (3건)

- **수정 1**: `ReturnSfxAfterPlay` 코루틴에서 `WaitForSeconds` → `WaitForSecondsRealtime` 변경
  - 이유: `WaitForSeconds`는 `Time.timeScale`을 따르므로 게임 일시정지(timeScale=0) 중 SFX가 완료되어도 풀 반환이 이루어지지 않음 (SINGLE-SFX-04 케이스)
  - `WaitForSecondsRealtime`은 실제 시간 기준이므로 일시정지 중에도 정상 반환됨

- **수정 2**: `Initialize()` 재호출 시 SFX 풀 중복 생성 방지 코드 추가
  - 이유: `_sfxPool`이 이미 채워진 상태에서 `Initialize()`가 다시 호출되면 AudioSource가 중복 생성됨
  - 진입부에 `if (_sfxPool != null && _sfxPool.Count > 0) return;` 가드 추가 (또는 `_sfxPool`이 null인지 확인)

- **수정 3**: 무음 전환 후 `_activeBgmSource` 상태 명확화
  - 이유: BGM 클립이 null인 경우 페이드아웃만 수행하고 새 BGM을 재생하지 않으면, `_activeBgmSource`가 여전히 이전 소스를 가리켜 다음 크로스페이드 시 채널 선택이 어긋날 수 있음
  - 무음 전환 완료 후 `_activeBgmSource = fadeIn` (현재 아무것도 재생 안 하는 채널)으로 상태를 명확히 설정

### 정적 분석 종합 판정

PASS — 규칙 1~22 전체 준수 확인. 수정 3건 적용 완료.
실기 테스트는 Inspector 작업 완료 후 진행 예정.

---

## 인게임 볼륨 패널 UI 테스트 (InGameSettingsUI)

### UI-INGAME-01: 사운드 버튼 클릭 시 메인 버튼 그룹 숨김 + 볼륨 패널 표시

**전제:** SetupInGameVolumePanel 에디터 스크립트가 정상 실행되어 InGameSettingsUI의 모든 필드가 연결된 상태. Game 씬에서 InGameSettingsUI 팝업이 열린 상태(Show() 호출됨). 볼륨 패널은 초기 숨김 상태(alpha=0).

**동작:**
1. 인게임 설정 팝업에서 [사운드] 버튼을 클릭한다

**기댓값:**
- MainButtonContainer CanvasGroup의 alpha가 0으로 변경된다
- MainButtonContainer의 interactable과 blocksRaycasts가 false로 변경된다
- VolumePanel CanvasGroup의 alpha가 1로 변경된다
- VolumePanel의 interactable과 blocksRaycasts가 true로 변경된다
- 볼륨 슬라이더 3종(마스터/배경음/효과음)이 현재 저장된 값으로 표시된다

**결과:**

---

### UI-INGAME-02: 볼륨 패널에서 뒤로가기 버튼 클릭 시 메인 버튼 그룹 복원 + 볼륨 패널 숨김

**전제:** UI-INGAME-01 완료 후 볼륨 패널이 표시된 상태

**동작:**
1. 볼륨 패널 하단의 [← 뒤로] 버튼을 클릭한다

**기댓값:**
- VolumePanel CanvasGroup의 alpha가 0으로 변경된다
- VolumePanel의 interactable과 blocksRaycasts가 false로 변경된다
- MainButtonContainer CanvasGroup의 alpha가 1로 복원된다
- MainButtonContainer의 interactable과 blocksRaycasts가 true로 복원된다
- [사운드] 버튼과 [포기] 버튼이 다시 보이고 터치 가능하다

**결과:**

---

### UI-INGAME-03: X(닫기) 버튼으로 팝업 닫을 때 볼륨 패널과 메인 버튼 초기화

**전제:** 볼륨 패널이 열린 상태(사운드 버튼 클릭 후)에서 X 닫기 버튼을 클릭하는 상황

**동작:**
1. UI-INGAME-01을 통해 볼륨 패널을 표시한다
2. X(닫기) 버튼을 클릭한다

**기댓값:**
- 팝업 전체가 Hide() 처리된다
- HideVolumePanel()이 내부적으로 호출되어 VolumePanel이 숨겨진다
- MainButtonContainer가 복원된다 (alpha=1, interactable=true, blocksRaycasts=true)
- 싱글플레이라면 Time.timeScale이 1로 복원된다

**결과:**

---

### UI-INGAME-04: 볼륨 패널 열린 상태에서 X 닫기 후 재오픈 시 메인 버튼 그룹이 표시된 초기 상태

**전제:** 볼륨 패널이 열린 채로 팝업을 X로 닫은 직후

**동작:**
1. UI-INGAME-03 완료 후 다시 인게임 설정 버튼(우상단)을 클릭하여 팝업을 재오픈한다

**기댓값:**
- 팝업이 Show()될 때 볼륨 패널은 숨겨진 상태이다 (이전 열린 상태가 잔류하지 않음)
- MainButtonContainer가 표시된 상태이다 (alpha=1)
- [사운드], [포기] 버튼이 정상 표시되고 터치 가능하다

**결과:**

---

## 로비 설정 탭 네비게이션 테스트 (LobbySettingsView)

### UI-LOBBY-01: 설정 탭 진입 시 메인 뷰 표시, 뒤로가기 버튼 없음

**전제:** SetupLobbySettingsTab 에디터 스크립트가 정상 실행된 상태. 로비 씬에서 하단 탭바의 설정 탭 아이콘을 터치할 수 있는 상태.

**동작:**
1. 로비 씬 하단 탭바에서 [설정] 탭을 클릭한다

**기댓값:**
- LobbySettingsView의 메인 화면(MainView)이 표시된다 (alpha=1)
- [프로필] 버튼과 [사운드] 버튼이 화면 중앙에 표시된다
- SubViewContainer는 숨겨진 상태이다 (alpha=0)
- BackButton이 화면에 보이지 않는다 (SetActive(false))

**결과:**

---

### UI-LOBBY-02: 사운드 버튼 클릭 시 사운드 서브 화면 진입, 뒤로가기 버튼 표시

**전제:** UI-LOBBY-01 완료 후 메인 화면이 표시된 상태

**동작:**
1. 메인 화면에서 [사운드] 버튼을 클릭한다

**기댓값:**
- MainView가 숨겨진다 (alpha=0, blocksRaycasts=false)
- SubViewContainer가 표시된다 (alpha=1, blocksRaycasts=true)
- SoundSubView만 활성화되고 ProfileSubView는 비활성 상태이다
- 볼륨 슬라이더 3종(마스터/배경음/효과음)이 현재 저장된 값으로 표시된다
- BackButton이 좌상단에 표시된다 (SetActive(true))

**결과:**

---

### UI-LOBBY-03: 프로필 버튼 클릭 시 프로필 서브 화면 진입, 뒤로가기 버튼 표시

**전제:** UI-LOBBY-01 완료 후 메인 화면이 표시된 상태

**동작:**
1. 메인 화면에서 [프로필] 버튼을 클릭한다

**기댓값:**
- MainView가 숨겨진다 (alpha=0)
- SubViewContainer가 표시된다 (alpha=1)
- ProfileSubView만 활성화되고 SoundSubView는 비활성 상태이다
- BackButton이 좌상단에 표시된다

**결과:**

---

### UI-LOBBY-04: 서브 화면에서 뒤로가기 버튼 클릭 시 메인 뷰 복귀, 뒤로가기 버튼 사라짐

**전제:** UI-LOBBY-02 또는 UI-LOBBY-03 완료 후 서브 화면이 표시된 상태

**동작:**
1. 좌상단 BackButton을 클릭한다

**기댓값:**
- SubViewContainer가 숨겨진다 (alpha=0, blocksRaycasts=false)
- MainView가 다시 표시된다 (alpha=1, blocksRaycasts=true)
- 모든 서브 화면(SoundSubView, ProfileSubView)이 비활성화된다
- BackButton이 사라진다 (SetActive(false))

**결과:**

---

### UI-LOBBY-05: 로비 사운드 서브 화면 볼륨 슬라이더 조절 시 즉시 적용

**전제:** UI-LOBBY-02 완료 후 사운드 서브 화면이 표시된 상태. AudioManager가 존재하는 상태(Login 씬을 거쳐 진입).

**동작:**
1. 마스터 볼륨 슬라이더를 0으로 내린다
2. 마스터 볼륨 슬라이더를 다시 1로 올린다
3. 배경음 슬라이더를 0.5로 조절한다
4. 효과음 슬라이더를 0으로 내린다

**기댓값:**
- 슬라이더를 움직이는 즉시 해당 채널 볼륨이 AudioManager에 반영된다
- 각 슬라이더 우측의 퍼센트 텍스트가 슬라이더 값에 맞게 갱신된다 (예: "50%", "0%")
- 마스터 0 → BGM/SFX 모두 무음
- BGM 0 → BGM 무음, SFX는 영향 없음

**결과:**

---

### UI-LOBBY-06: 타 탭 이동 후 설정 탭 재진입 시 메인 뷰로 초기화됨

**전제:** UI-LOBBY-02 완료 후 사운드 서브 화면이 표시된 상태

**동작:**
1. 하단 탭바에서 다른 탭(예: 편성 탭)으로 이동한다
2. 다시 [설정] 탭으로 돌아온다

**기댓값:**
- LobbySettingsView가 메인 화면 상태로 표시된다 (Initialize() → ShowMain() 재실행)
- BackButton이 없는 상태이다
- 이전에 열었던 서브 화면 상태가 잔류하지 않는다

**결과:**

---

## 에디터 스크립트 검증 (정적 / 에디터 실행)

아래 TC는 Unity Editor에서 직접 MenuItem을 실행하여 확인한다.

### EDITOR-01: SetupAudioManager — GameAudioMixer.mixer 없을 때 경고 다이얼로그 표시 후 중단

**전제:** `Assets/_Project/Audio/GameAudioMixer.mixer` 파일이 존재하지 않는 상태 (에디터 실행 전 임시 이동/제거)

**동작:**
1. Unity 상단 메뉴 → Hexiege/Setup/사운드 - AudioManager 설정을 실행한다

**기댓값 (정적 검증):**
- `EditorUtility.DisplayDialog("AudioMixer 없음", ...)` 가 호출된다 (L53-64)
- 이후 `return`으로 진행이 중단된다 (L65)
- Login.unity가 열리지 않고, [Audio] GO가 생성되지 않는다

**결과:**

---

### EDITOR-02: SetupAudioManager — 이미 [Audio] GO 있을 때 중복 생성 없이 재사용

**전제:** SetupAudioManager를 이미 한 번 실행하여 Login.unity에 [Audio] GO와 AudioManager 컴포넌트가 존재하는 상태

**동작:**
1. Unity 상단 메뉴 → Hexiege/Setup/사운드 - AudioManager 설정을 다시 실행한다

**기댓값 (에디터 실행 검증):**
- Hierarchy에 [Audio] GO가 하나만 존재한다 (중복 생성 없음)
- `GameObject.Find("[Audio]")`가 기존 GO를 반환하고 신규 생성 분기를 타지 않는다 (L94-96)
- SoundConfig.asset이 새로 생성되지 않고 기존 것을 재사용한다
- 이미 할당된 SoundConfig 클립이 초기화되지 않는다

**결과:**

---

### EDITOR-03: SetupInGameVolumePanel — _panel 미연결 상태에서 실행 시 안내 메시지 후 중단

**전제:** Game.unity에 InGameSettingsUI 컴포넌트는 존재하지만 `_panel` 필드가 Inspector에서 연결되지 않은 상태

**동작:**
1. Unity 상단 메뉴 → Hexiege/Setup/사운드 - 인게임 볼륨 패널 생성을 실행한다

**기댓값 (에디터 실행 검증):**
- `EditorUtility.DisplayDialog("_panel 없음", ...)` 가 호출된다 (L82-88)
- 이후 `return`으로 진행이 중단된다
- MainButtonContainer, VolumePanel GO가 생성되지 않는다
- InGameSettingsUI 필드 연결 작업이 수행되지 않는다

**결과:**

---

## 2차 정적 분석 결과 — Inspector 설정 자동화 (2026-06-11)

### 분석 대상

| 파일 | 변경 유형 |
|------|----------|
| `Assets/_Project/Scripts/Presentation/UI/InGameSettingsUI.cs` | 수정 — `_mainButtonContainer` 숨김/복원 로직 추가 |
| `Assets/_Project/Scripts/Presentation/UI/LobbySettingsView.cs` | 신규 — 로비 설정 탭 네비게이션 컴포넌트 |
| `Assets/Editor/Setup/SetupAudioManager.cs` | 신규 — Login.unity AudioManager 자동 구성 |
| `Assets/Editor/Setup/SetupInGameVolumePanel.cs` | 신규 — Game.unity 볼륨 패널 UI 자동 구성 |
| `Assets/Editor/Setup/SetupLobbySettingsTab.cs` | 신규 — Lobby.unity 설정 탭 자동 구성 |

---

### InGameSettingsUI.cs

| 항목 | 점검 내용 | 판정 |
|------|----------|------|
| `HideVolumePanel()` — `_volumePanelGroup` null 시 early return | L344: `if (_volumePanelGroup == null) return;` 로 안전하게 보호됨 | PASS |
| `HideVolumePanel()` — `_mainButtonContainer` null 체크 | L351: `if (_mainButtonContainer != null)` 조건부 처리 | PASS |
| `ShowVolumePanel()` — `_mainButtonContainer` null 체크 | L331: `if (_mainButtonContainer != null)` 조건부 처리 | PASS |
| `Initialize()` 시점 `HideVolumePanel()` 호출 안전성 | `_volumePanelGroup`이 null이면 early return → `_mainButtonContainer` 복원도 실행되지 않음. 에디터 스크립트로 필드가 연결된 이후에는 문제없으나, 미연결 상태에서 `_mainButtonContainer`가 초기에 보이는 상태여야 한다면 보장이 없음 | WARNING |
| `OnSoundButtonClicked()` — `alpha >= 0.5f` 토글 조건 | ShowVolumePanel/HideVolumePanel이 1f 또는 0f만 설정하므로 0.5 임계값은 실질적으로 안전 | PASS |
| `Hide()` 내부 `HideVolumePanel()` 호출 후 `_mainButtonContainer` 복원 | HideVolumePanel L351-356에서 복원 처리됨. 단 상기 WARNING 조건 동일 적용 | PASS |
| 볼륨 슬라이더 null-safe (`?.` / `?? 1f`) | SetupVolumeSliders, RefreshVolumeSliderValues 모두 `?.` 와 `?? 1f` 적용 확인 | PASS |
| 리스너 중복 등록 방지 | `Initialize()` 재호출 시 모든 버튼에 `RemoveAllListeners()` 후 재등록 | PASS |

**WARNING 상세 (경미)**: `Initialize()` 시 `_volumePanelGroup`이 null이면 `HideVolumePanel()`이 early return되어 `_mainButtonContainer` 복원 로직이 실행되지 않는다. SetupInGameVolumePanel 에디터 스크립트가 정상 실행된 후에는 두 필드 모두 연결되므로 실제 런타임에서는 문제가 발생하지 않는다. 다만 에디터 스크립트 미실행 상태에서 Inspector 미연결 시 `_mainButtonContainer`가 초기 표시 상태가 아닌 채로 시작할 수 있다.

---

### LobbySettingsView.cs

| 항목 | 점검 내용 | 판정 |
|------|----------|------|
| `Awake()` → `Initialize()` 호출 | L104-106: Awake에서 Initialize 1회 호출 | PASS |
| 리스너 중복 등록 방지 | 모든 버튼 `RemoveAllListeners()` 후 재등록 | PASS |
| `ShowMain()` null 체크 | `SetGroupVisible`이 null 체크 내장 (L287), `SetBackButtonVisible`도 null 체크 내장 (L302) | PASS |
| `ShowSubView(null)` 처리 | L229: `if (subView == null) return;` early return | PASS |
| 사운드 서브 화면 진입 시 슬라이더 refresh | L238-239: `RefreshVolumeSliderValues()` 호출 확인 | PASS |
| AudioManager null-safe | SetupVolumeSliders, RefreshVolumeSliderValues 전체 `?.` 와 `?? 1f` 적용 | PASS |
| BackButton SetActive 방식 | 앵커 고정 배치이므로 SetActive로 처리 — LayoutGroup 영향 없음 (L301-303 주석 명시) | PASS |
| 서브 화면 전환 시 기존 서브 화면 비활성화 | L234-235: `_soundSubView`와 `_profileSubView` 각각 `subView == x` 조건으로 SetActive 처리 | PASS |

---

### SetupAudioManager.cs

| 항목 | 점검 내용 | 판정 |
|------|----------|------|
| AudioMixer 없을 때 경고 후 중단 | L53-65: `DisplayDialog` 후 `return` — 정상 중단 | PASS |
| BGM/SFX 그룹 없을 때 경고 후 중단 | L127-134: `DisplayDialog` 후 `return` | PASS |
| 그룹 체크 시점 — 불완전 GO 잔류 가능성 | BGM/SFX 그룹 체크(L127)는 [Audio] GO와 AudioSource 자식이 이미 생성된 후에 수행됨. 그룹 없으면 return하되 씬에 불완전한 [Audio] GO가 저장되지 않고 잔류할 수 있음. 씬 저장(L174)이 return 이후이므로 실제 저장은 안 되지만, 에디터 세션 내 Hierarchy에는 잔류 | WARNING |
| `LoginBootstrapper` 없을 때 처리 | L166-169: `LogWarning` 후 계속 진행 (팝업 없음, 비필수 필드이므로 적절) | PASS |
| SoundConfig 이미 있을 때 재사용 | L71-77: `LoadAssetAtPath` 결과가 null이 아니면 재사용, null일 때만 `CreateInstance` | PASS |
| `[Audio]` GO 중복 생성 방지 | L94-96: `GameObject.Find("[Audio]")` 결과가 null일 때만 신규 생성 | PASS |
| BGM AudioSource outputAudioMixerGroup 연결 | L140-141: bgmSourceA, bgmSourceB에 bgmGroup 연결 | PASS |
| SFX Group AudioManager 필드 연결 | L149: `_sfxGroup`에 sfxGroup SerializedObject 연결 | PASS |

**WARNING 상세 (경미)**: BGM/SFX 그룹 체크는 [Audio] GO와 자식 AudioSource가 생성된 후에 실행된다. 그룹이 없어 `return`되면 씬 저장(L174) 전에 종료되므로 디스크에 저장은 안 된다. 그러나 에디터 Hierarchy에는 [Audio] GO와 자식 오브젝트가 잔류하여 씬을 수동 저장하면 불완전한 상태로 저장될 수 있다. 실용적으로는 믹서 설정 완료 후 재실행하면 재사용 처리되므로 치명적이지 않으나, 명시적으로 `DestroyImmediate(audioGo)` 후 return하거나 체크 순서를 앞으로 이동하면 더 안전함.

---

### SetupInGameVolumePanel.cs

| 항목 | 점검 내용 | 판정 |
|------|----------|------|
| `_panel` null 체크 | L81-88: `_panel` property가 null이거나 objectReferenceValue가 null이면 `DisplayDialog` 후 return | PASS |
| `_soundButton` / `_forfeitButton` null 처리 | L97-98: `?.objectReferenceValue as Button` → null이면 SetParent 조건 블록 스킵됨 (L123-124) | PASS |
| 슬라이더 Unity UI 표준 구조 | Background / Fill Area / Fill / Handle Slide Area / Handle 계층 모두 생성 확인 | PASS |
| `slider.fillRect` 설정 | L305: `slider.fillRect = fillRt;` | PASS |
| `slider.handleRect` 설정 | L321: `slider.handleRect = handleRt;` | PASS |
| `slider.targetGraphic` 설정 | L322: `slider.targetGraphic = handleImg;` | PASS |
| InGameSettingsUI 필드 연결 누락 여부 | `_mainButtonContainer`, `_volumePanelGroup`, `_masterSlider`, `_bgmSlider`, `_sfxSlider`, `_backButton`, `_masterValueText`, `_bgmValueText`, `_sfxValueText` 총 9개 필드 연결 확인 — 누락 없음 | PASS |

---

### SetupLobbySettingsTab.cs

| 항목 | 점검 내용 | 판정 |
|------|----------|------|
| ProfilePanel null 체크 | L73-77: `profilePanelGo == null`이면 `DisplayDialog` 후 return | PASS |
| ProfileView 없을 때 처리 | L104-106: `profileView != null` 조건부로 SetActive — null safe | PASS |
| LobbySettingsView 필드 연결 누락 여부 | `_backButton`, `_mainView`, `_subViewContainer`, `_profileButton`, `_soundButton`, `_soundSubView`, `_profileSubView`, `_masterSlider`, `_bgmSlider`, `_sfxSlider`, `_masterValueText`, `_bgmValueText`, `_sfxValueText` 총 13개 필드 — 누락 없음 | PASS |
| TabBarView 없을 때 처리 | L82: `if (tabBar != null)` 조건부 — null safe | PASS |
| BackButton 초기 숨김 | L135: `backBtnGo.SetActive(false);` — 정상 | PASS |
| MainView/SubViewContainer 초기 상태 | MainView: alpha=1, SubViewContainer: alpha=0, interactable=false, blocksRaycasts=false — 정상 | PASS |

---

### 2차 정적 분석 종합 판정

CONDITIONAL PASS — 전체 구현 로직은 올바르며 중대 버그 없음. 경미한 WARNING 2건 기록.

**WARNING 목록 (Minor — 수정 권장)**:
1. `InGameSettingsUI.HideVolumePanel()`: `_volumePanelGroup` null 시 `_mainButtonContainer` 복원 스킵. 에디터 스크립트 정상 실행 후에는 런타임 무영향이나 미연결 상태에서의 방어 로직 부재.
2. `SetupAudioManager.Run()`: BGM/SFX 그룹 체크가 GO 생성 후에 수행되어, 그룹 없을 때 return 시 Hierarchy에 불완전한 [Audio] GO가 에디터 세션 내 잔류. 씬 저장 전 return이므로 디스크 저장 문제는 없음.
