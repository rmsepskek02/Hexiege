# Research — 사운드 시스템 구축

## 개요 (자연어 설명)

이 작업은 게임 전반에 걸쳐 BGM과 효과음(SFX)을 재생·관리하는 사운드 시스템을 구축하는 작업입니다.

현재 프로젝트에는 인게임 VFX·SFX를 담당하는 EffectManager가 있지만, BGM 관리 기능이 없고 Login/Lobby 씬에서는 어떠한 소리도 재생되지 않습니다. 또한 볼륨 조절 UI는 플레이스홀더 버튼만 존재하고 실제 동작하지 않습니다.

이 작업을 통해:
- 모든 씬(Login, Lobby, Game)에서 BGM이 재생됩니다.
- 효과음이 AudioMixer를 통해 볼륨 제어가 가능한 구조로 관리됩니다.
- EffectManager는 VFX(파티클)만 담당하고, 사운드 전체는 새로운 AudioManager가 담당합니다.

---

## 1. 현재 상태 분석

### EffectManager.cs (`Presentation/Effects/EffectManager.cs`)

현재 VFX와 SFX를 모두 담당하고 있는 Game씬 전용 매니저.

**VFX 부분**
- GameObject prefab별 Queue 기반 오브젝트 풀
- 재생 완료 시 VfxPoolItem 콜백으로 자동 반환

**SFX 부분 (AudioManager로 이전 예정)**
- AudioSource 오브젝트 풀 (동시 8개 제한, `_maxConcurrentSfx`)
- `spatialBlend = 0` (완전 2D, 거리감 없음)
- `ReturnSfxAfterPlay` 코루틴: 클립 길이 + 0.1초 후 자동 풀 반환
- `_activeSfxCount`로 동시 재생 수 추적

**생명주기 방식**
- `static Instance` 직접 관리 (SingletonMonoBehaviour 미사용)
- `Awake()`: `Instance = this`
- `OnDestroy()`: `Instance = null`
- **DontDestroyOnLoad 없음** → Game씬 전용 설계 의도적

**외부 API (호출부는 변경 없이 유지)**
| 메서드 | 호출 위치 | 역할 |
|--------|----------|------|
| `PlayUnitAttack(type, pos, rot)` | UnitView Animation Event (`OnAttackHit`) | 공격 VFX + SFX |
| `PlayUnitDeath(type, pos)` | UnitView (유닛 사망 처리) | 사망 VFX + SFX |
| `PlayBuildingDestroy(type, pos)` | BuildingFactory | 건물 파괴 VFX + SFX |
| `PlayBuildingUpgrade(type, pos)` | BuildingFactory | 업그레이드 VFX + SFX |
| `PlayUi(UiEffectKey)` | UI 컴포넌트 | UI 효과 VFX + SFX |

**중요**: `PlayUnitAttack`은 Animation Event에서 호출되므로 애니메이션 히트 프레임에 타이밍이 맞춰져 있음. GameEvents로 대체하면 타이밍이 어긋남.

---

### GameEvents.cs (`Application/Events/GameEvents.cs`)

BGM 전환 트리거로 사용 가능한 기존 이벤트:

| 이벤트 | 발행 위치 | BGM 트리거 용도 |
|--------|----------|----------------|
| `OnGameStarted` | GameBootstrapper.LoadMap() 마지막 | Battle BGM 시작 |
| `OnGameEnd` | GameEndUseCase | 승리/패배 BGM 전환 |

→ 새로운 Subject 추가 불필요.

---

### InGameSettingsUI.cs (`Presentation/UI/InGameSettingsUI.cs`)

`_soundButton`이 플레이스홀더로 존재:
```csharp
[Tooltip("사운드 옵션 버튼 (현재는 플레이스홀더 — 클릭해도 동작 없음).")]
[SerializeField] private Button _soundButton;
// Initialize() 내부: _soundButton 리스너 등록 자체를 하지 않는다.
```
→ 이 버튼을 볼륨 조절 패널 열기 버튼으로 구현 예정.

---

### SingletonMonoBehaviour (`Core/SingletonMonoBehaviour.cs`)

AudioManager에 그대로 사용 가능한 기반 클래스:
- `Instance` 전역 접근점 제공
- 중복 인스턴스 자동 파괴
- **DontDestroyOnLoad** 포함 → Login씬 생성 후 Lobby, Game씬까지 유지

---

### 씬 구조 및 Bootstrapper

| 씬 | Bootstrapper | Build Index |
|----|-------------|-------------|
| Login.unity | LoginBootstrapper | (미확인) |
| Lobby.unity | 미확인 (별도 Bootstrapper 없음) | 0 |
| Game.unity | GameBootstrapper | 1 |

**주목**: Bootstrap 폴더에 LobbyBootstrapper 없음. Lobby 씬의 초기화 방식은 Plan 단계에서 확인 필요.

---

### 계획된 오디오 에셋 (VFXSFXList.md 기준)

폴더 구조 계획:
```
Assets/_Project/Audio/
├── BGM/           ← bgm_battle, bgm_lobby, bgm_login, bgm_victory, bgm_defeat
├── SFX/
│   ├── Units/     ← 유닛별 공격/사망
│   ├── Buildings/ ← 건물 배치/파괴/업그레이드
│   ├── UI/        ← 버튼, 팝업 등
│   ├── Tiles/     ← 타일 점령
│   └── Game/      ← 게임 시작/종료
└── Ambient/
```

SFX 100+ 항목 계획됨 (현재 제작된 에셋 0개).

---

## 2. 영향 범위

### 신규 생성 파일
| 파일 | 레이어 | 역할 |
|------|--------|------|
| `Presentation/Audio/AudioManager.cs` | Presentation | BGM + SFX 통합 관리 |
| `Infrastructure/Config/SoundConfig.cs` | Infrastructure | SFX/BGM 클립 매핑 ScriptableObject |
| `Assets/_Project/Audio/AudioMixer.mixer` | 에셋 | Master/BGM/SFX 믹서 그룹 |

### 수정 파일
| 파일 | 변경 내용 |
|------|----------|
| `EffectManager.cs` | SFX 관련 코드 제거, `AudioManager.Instance?.PlayXxx()`로 위임 |
| `InGameSettingsUI.cs` | `_soundButton` 리스너 연결, 볼륨 UI 연동 |
| `LoginBootstrapper.cs` | AudioManager 초기화 + Login BGM 재생 |
| `GameBootstrapper.cs` | Lobby BGM 재생 + (Game씬 BGM은 GameEvents 구독으로 처리) |

---

## 3. 아키텍처 제약 확인

| 제약 | 사운드 시스템 적용 여부 |
|------|----------------------|
| Domain → Core 참조 금지 | 해당 없음 (Presentation/Infrastructure만 관여) |
| NetworkBehaviour는 Infrastructure에만 | 해당 없음 (AudioManager는 Presentation) |
| GameBootstrapper 단일 의존성 조합 루트 | AudioManager는 SingletonMonoBehaviour로 자체 초기화, Bootstrapper는 Initialize() 호출만 |
| Application → Netcode 직접 참조 금지 | 해당 없음 |
| SFX 네트워크 동기화 | 없음 — 각 클라이언트 로컬 재생만 |

---

## 4. 주요 설계 결정 사항

| 항목 | 결정 |
|------|------|
| SFX 공간음 | 2D 유지 (`spatialBlend = 0`) |
| SFX 동시 재생 한도 | 8개 유지 |
| 멀티플레이 SFX | 로컬 재생만 (동기화 없음) |
| EffectManager 외부 API | 유지 — 내부에서 AudioManager 위임 |
| BGM 전환 방식 | SceneManager.activeSceneChanged (Login/Lobby) + GameEvents 구독 (Game) |
| AudioMixer | Master / BGM / SFX 3채널 |
| 볼륨 저장 | PlayerPrefs |
