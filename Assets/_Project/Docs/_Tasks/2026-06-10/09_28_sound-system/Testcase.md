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
