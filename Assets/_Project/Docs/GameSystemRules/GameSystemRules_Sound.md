# Game System Rules — 사운드 시스템

사운드 시스템(BGM, SFX, 볼륨 제어) 구현 시 따라야 하는 규칙 모음.

---

## 목차

- [아키텍처 규칙](#아키텍처-규칙)
- [BGM 규칙](#bgm-규칙)
- [SFX 규칙](#sfx-규칙)
- [볼륨 규칙](#볼륨-규칙)

---

## 아키텍처 규칙

**규칙 1. 역할 분리 원칙**
EffectManager는 VFX(파티클)만 담당하며 오디오 코드가 한 줄도 포함되어서는 안 된다.
BGM과 SFX 전체는 AudioManager가 전담한다.

**규칙 2. AudioManager 레이어 및 생명주기**
AudioManager는 Presentation 레이어에 배치하며 `SingletonMonoBehaviour<AudioManager>`를 상속한다.
DontDestroyOnLoad를 통해 Login → Lobby → Game 씬 전반에 걸쳐 유지된다.
Login.unity 씬의 `[Audio]` 오브젝트에 컴포넌트로 부착한다.

**규칙 3. SoundConfig 레이어 의존 범위**
SoundConfig는 Infrastructure/Config 레이어에 배치하며, Domain 레이어 타입(UnitType, BuildingType)만 키로 사용할 수 있다.
Presentation 레이어 타입(UiEffectKey 등)을 SoundConfig에서 참조하는 것은 레이어 위반이다.

**규칙 4. UI SFX 관리 위치**
UI SFX(`UiEffectKey` 키)는 SoundConfig에 포함하지 않는다.
UI SFX 매핑은 AudioManager가 직접 `[SerializeField] List<UiSoundEntry>` 형태로 보유한다.
UiEffectKey는 Presentation 레이어 타입이므로 AudioManager(Presentation)에서만 참조 가능하다.

**규칙 5. null-safe 호출 원칙**
모든 `AudioManager.Instance` 접근은 반드시 `?.` 연산자를 사용한다.
개발 중 Login 씬을 거치지 않고 Game 씬에 직접 진입하는 경우 Instance가 null일 수 있으므로,
null인 경우 사운드 없이 동작하는 것이 정상 상태이다.

---

## BGM 규칙

**규칙 6. BGM 전환 시점 정의**
BGM은 아래 시점에 전환된다.

| 트리거 | BGM | 전환 방식 |
|--------|-----|----------|
| AudioManager Initialize() — 현재 씬 이름 확인 | 씬별 BGM | 즉시 재생 |
| SceneManager.activeSceneChanged → "Login" | Login BGM | 크로스페이드 |
| SceneManager.activeSceneChanged → "Lobby" | Lobby BGM | 크로스페이드 |
| GameEvents.OnGameStarted | Battle BGM | 크로스페이드 |
| GameEvents.OnGameEnd (싱글/멀티 공통) | 게임종료 BGM | 크로스페이드 |

> ⚠️ **미결 — Splash BGM**: Splash 화면을 Login 씬 내부에서 처리할지 별도 씬으로 분리할지 확정되지 않음.
> - Login 씬 내부 처리 시: Login BGM이 그대로 재생되므로 추가 규칙 불필요.
> - 별도 Splash 씬 사용 시: 이 표에 `SceneManager.activeSceneChanged → "Splash"` 행을 추가하고 Splash BGM 클립을 SoundConfig에 추가해야 함.
> 확정 후 이 노트를 삭제하고 규칙을 업데이트한다.

**규칙 7. BGM 초기 재생 방식**
`SceneManager.activeSceneChanged`는 씬이 변경될 때만 발생하므로, AudioManager가 처음 초기화되는 씬의 BGM은 이벤트로 트리거할 수 없다.
`Initialize()` 내에서 `SceneManager.GetActiveScene().name`으로 현재 씬 이름을 확인하여 즉시 재생해야 한다.

**규칙 8. BGM 크로스페이드**
BGM 전환은 항상 크로스페이드로 처리한다. 즉시 전환(Stop → Play)은 금지한다.
기본 크로스페이드 시간: 1.0초 (SoundConfig의 `bgmCrossfadeDuration`으로 조정 가능).
크로스페이드 도중 새 전환이 요청되면 기존 코루틴을 StopCoroutine한 뒤 새 크로스페이드를 즉시 시작한다.
이때 StopCoroutine만으로는 페이드아웃 중이던 채널이 계속 재생되어 이전 BGM이 새 BGM과 겹친다. 따라서 코루틴 중단 직후, 페이드아웃 중이던 채널(active가 아닌 채널)을 즉시 `Stop()`(+ volume 0, clip null)하여 강제 정리해야 한다. (2026-07-08 BUG-1 수정)
AudioSource A/B 두 채널을 번갈아 사용하여 크로스페이드를 구현한다.

**규칙 9. Game 씬 로딩 중 BGM**
Game 씬 전환 직후부터 `GameEvents.OnGameStarted`가 발행되기 전까지는 Lobby BGM이 그대로 유지된다.
`OnGameStarted` 발행 시점에 Battle BGM으로 크로스페이드한다.

**규칙 10. BGM 클립 미할당 처리**
SoundConfig에서 특정 씬/상황에 해당하는 BGM 클립이 비어있으면(null) 무음으로 전환한다.
이전 BGM을 유지하지 않는다.

**규칙 11. Victory/Defeat BGM 분리 (향후 작업)**
V1에서는 싱글/멀티플레이 구분 없이 `GameEvents.OnGameEnd` 발생 시 동일한 게임종료 BGM을 재생한다.

향후 조건이 충족되면 아래와 같이 분리한다:
- 선행 조건: `LocalPlayerTeam.Set()`이 싱글/멀티플레이 모두에서 올바르게 호출됨을 보장
- 싱글플레이: `GameEndEvent.Winner == TeamId.Red` → Victory BGM, `TeamId.Blue` → Defeat BGM
- 멀티플레이: `GameEndEvent.Winner == LocalPlayerTeam.Current` → Victory BGM, 아니면 Defeat BGM
- 이 작업은 팀 판별 수단 확보 후 별도 태스크로 진행한다.

---

## SFX 규칙

**규칙 12. SFX 2D 고정 정책**
모든 SFX AudioSource의 `spatialBlend`는 0(완전 2D)으로 설정한다.
헥스 그리드를 위에서 내려다보는 시점 특성상 거리감 기반 3D 오디오는 적용하지 않는다.

**규칙 13. SFX 동시 재생 한도**
동시에 재생 가능한 최대 SFX 개수는 8개다.
한도 초과 시 새 SFX 재생 요청을 무시한다(드랍).
이 값은 AudioManager Inspector의 `_maxConcurrentSfx`에서 조정한다.

**규칙 14. 멀티플레이 SFX 동기화 금지**
SFX는 각 클라이언트가 자신의 화면에서 발생하는 이벤트에 맞춰 로컬에서만 재생한다.
네트워크를 통한 SFX 동기화는 하지 않는다.

**규칙 15. VFX+SFX 쌍 호출 규칙**
VFX와 SFX가 동시에 재생되어야 하는 경우, 같은 메서드 내에서 두 매니저를 연달아 호출한다.

```csharp
// 올바른 예 — 같은 호출 지점에서 연달아 호출
EffectManager.Instance?.PlayUnitAttack(type, pos, rot);   // VFX
AudioManager.Instance?.PlayUnitAttackSfx(type);           // SFX

// 금지 — 한 매니저만 호출하거나 서로 다른 위치에서 호출
EffectManager.Instance?.PlayUnitAttack(type, pos, rot);   // SFX 누락
```

VFX가 있는 모든 이펙트에는 대응하는 SFX 호출이 바로 아래 줄에 있어야 한다.
VFX 또는 SFX 중 하나만 재생하는 경우에도 나머지를 주석으로 명시한다.

**규칙 16. SFX 클립별 볼륨 조절**
SFX 클립은 글로벌 SFX 채널 볼륨과 별개로 클립별 기준 볼륨(`volume`, 0~1)을 가진다.
SoundConfig의 각 SFX 엔트리(UnitSoundEntry, BuildingSoundEntry 등)에 `attackVolume`, `deathVolume` 등 항목별 볼륨 필드를 포함한다.
UI SFX 엔트리(UiSoundEntry)도 동일하게 `volume` 필드를 포함한다.
실제 재생 시 `AudioSource.volume = clipVolume`으로 적용하며, AudioMixer SFX 채널 볼륨과 곱해진 최종 볼륨으로 출력된다.
이 방식으로 SFX 제작 단계에서 에셋별 밸런스를 Inspector에서 직접 조정할 수 있다.

**규칙 17. AudioMixerGroup 연결**
BGM AudioSource(A/B 채널 모두)는 BGM AudioMixerGroup에, SFX 풀에서 생성하는 모든 AudioSource는 SFX AudioMixerGroup에 연결해야 한다.
MixerGroup이 연결되지 않으면 해당 채널의 볼륨 제어가 적용되지 않는다.
`Initialize()` 내에서 `_bgmGroup` 또는 `_sfxGroup`이 null이면 각각 `Debug.LogWarning`으로 경고한다.

---

## 볼륨 규칙

**규칙 18. 볼륨 채널 구성**
오디오는 아래 3개 채널로 분리 관리한다.

| 채널 | Exposed Parameter 이름 | 제어 범위 |
|------|----------------------|----------|
| Master | `MasterVolume` | 전체 볼륨 |
| BGM | `BGMVolume` | BGM만 |
| SFX | `SFXVolume` | SFX만 |

AudioMixer의 Exposed Parameters를 통해 AudioManager가 각 채널 볼륨을 설정한다.

**규칙 19. 볼륨 값 범위 및 dB 변환**
볼륨은 UI 슬라이더와 PlayerPrefs에서 0~1 범위의 float로 관리한다.
AudioMixer에 적용할 때는 dB로 변환한다.

```csharp
float dB = Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f;
// 0.0001 하한을 두어 Log10(0)으로 인한 -Infinity 방지
```

**규칙 20. 볼륨 초기값**
저장된 PlayerPrefs 값이 없는 경우 기본값은 1.0이다.

**규칙 21. 볼륨 저장 및 로드**
볼륨 설정은 PlayerPrefs에 저장하여 게임 재시작 후에도 유지된다.

| 채널 | PlayerPrefs 키 |
|------|---------------|
| Master | `"MasterVolume"` |
| BGM | `"BGMVolume"` |
| SFX | `"SFXVolume"` |

`Initialize()` → 저장된 값 로드 → AudioMixer 적용.
`SetXxxVolume()` → AudioMixer 적용 → `PlayerPrefs.Save()` 즉시 호출.

**규칙 22. 볼륨 설정 UI 위치**
볼륨 슬라이더(Master/BGM/SFX)는 아래 두 곳에 모두 배치한다.

| 씬 | UI 위치 |
|----|---------|
| Lobby.unity | 로비 설정 패널 (별도 구현) |
| Game.unity | InGameSettingsUI의 사운드 버튼 → 볼륨 조절 패널 |

두 UI 모두 동일한 PlayerPrefs 키를 읽고 쓰므로, 어느 씬에서 설정해도 다른 씬에 자동 반영된다.
