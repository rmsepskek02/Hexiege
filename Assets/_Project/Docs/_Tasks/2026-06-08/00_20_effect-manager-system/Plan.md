# Plan — EffectManager 시스템 설계 및 구현

## 이 Plan이 무엇인지

게임 전체의 VFX(시각 이펙트)와 SFX(사운드)를 하나의 `EffectManager`로 통합 관리하는
시스템을 처음부터 설계하고 구현합니다.

> **기존 로직 제거 안내**
>
> `UnitEffectView.cs`는 버그(멀티플레이 클라이언트에서 VFX 미재생)가 있고
> 이번 시스템으로 완전 대체됩니다.
> 프리팹에서 컴포넌트를 제거하는 에디터 스크립트를 포함합니다.
> 최종 삭제는 [6] 사용자 테스트 통과 후에 수행합니다.

---

## 최적화 방침

| 항목 | 방식 | 이유 |
|------|------|------|
| VFX | Object Pool, **개수 제한 없음** | 눈에 보여서 일부만 재생되면 어색함 |
| SFX | Object Pool + **동시 8개 제한** | 소리는 많이 겹쳐도 자연스럽고, 제한해도 어색하지 않음 |

---

## 전체 구조

```
EffectPreset (ScriptableObject)
  ├── vfxPrefab   : GameObject   ← 파티클 시스템 프리팹
  ├── sfxClip     : AudioClip    ← 사운드 클립
  └── sfxVolume   : float        ← 사운드 볼륨 (기본 1.0)

UnitEffectConfig (ScriptableObject)        ← GameBootstrapper에 연결
  └── List<UnitEffectEntry>
        └── { unitType, attackPreset, deathPreset }

BuildingEffectConfig (ScriptableObject)    ← GameBootstrapper에 연결
  └── List<BuildingEffectEntry>
        └── { buildingType, destroyPreset, upgradePreset }

UiEffectConfig (ScriptableObject)          ← GameBootstrapper에 연결
  └── List<UiEffectEntry>
        └── { key(enum), preset }

EffectManager (씬 배치 MonoBehaviour, static Instance)
  ├── Dictionary<GameObject, Queue<VfxPoolItem>>  ← VFX Pool (프리팹별)
  ├── Queue<AudioSource>                           ← SFX Pool (공유, 8개)
  ├── PlayUnitAttack(UnitType, Vector3)
  ├── PlayUnitDeath(UnitType, Vector3)
  ├── PlayBuildingDestroy(BuildingType, Vector3)
  ├── PlayBuildingUpgrade(BuildingType, Vector3)
  ├── PlayUi(UiEffectKey)
  └── [내부] Play(EffectPreset, Vector3)   ← 실제 재생 로직
```

---

## 파일별 변경 내용

### [신규] EffectPreset.cs
**경로**: `Assets/_Project/Scripts/Presentation/Effects/EffectPreset.cs`

VFX 프리팹 + SFX 클립을 하나의 에셋으로 묶는 ScriptableObject.

```
필드:
  [SerializeField] GameObject  _vfxPrefab    ← 파티클 시스템 프리팹 (없으면 VFX 생략)
  [SerializeField] AudioClip   _sfxClip      ← 사운드 클립 (없으면 SFX 생략)
  [SerializeField] float       _sfxVolume = 1f

생성 메뉴: [CreateAssetMenu] "Hexiege/Effects/EffectPreset"
```

GameSystemRules 근거: 없음 (신규 시스템)

---

### [신규] UnitEffectConfig.cs
**경로**: `Assets/_Project/Scripts/Presentation/Effects/UnitEffectConfig.cs`

UnitType별 공격/사망 EffectPreset을 Inspector에서 설정하는 ScriptableObject.
`UnitStatsConfig`와 동일한 `List<Entry>` 구조.

```
[System.Serializable] struct UnitEffectEntry:
  UnitType       unitType
  EffectPreset   attackPreset    ← 공격 VFX + SFX 세트
  EffectPreset   deathPreset     ← 사망 VFX + SFX 세트

메서드:
  EffectPreset GetAttack(UnitType type)
  EffectPreset GetDeath(UnitType type)
  → Dictionary<UnitType, UnitEffectEntry>로 캐싱 (O(1) 조회)

에셋 경로: Assets/_Project/Resources/Config/UnitEffectConfig.asset
생성 메뉴: "Hexiege/Setup/UnitEffectConfig 생성"
```

GameSystemRules 근거: UnitStatsConfig와 동일한 패턴 유지

---

### [신규] BuildingEffectConfig.cs
**경로**: `Assets/_Project/Scripts/Presentation/Effects/BuildingEffectConfig.cs`

BuildingType별 파괴/업그레이드 EffectPreset ScriptableObject.

```
[System.Serializable] struct BuildingEffectEntry:
  BuildingType   buildingType
  EffectPreset   destroyPreset
  EffectPreset   upgradePreset

메서드:
  EffectPreset GetDestroy(BuildingType type)
  EffectPreset GetUpgrade(BuildingType type)

에셋 경로: Assets/_Project/Resources/Config/BuildingEffectConfig.asset
생성 메뉴: "Hexiege/Setup/BuildingEffectConfig 생성"
```

GameSystemRules 근거: 없음 (신규 시스템)

---

### [신규] UiEffectConfig.cs
**경로**: `Assets/_Project/Scripts/Presentation/Effects/UiEffectConfig.cs`

UI 이펙트를 enum 키로 조회하는 ScriptableObject.

```
enum UiEffectKey:
  ButtonClick, ButtonConfirm, ButtonCancel,
  PanelOpen, PanelClose,
  GoldGain, TimerComplete

[System.Serializable] struct UiEffectEntry:
  UiEffectKey    key
  EffectPreset   preset

에셋 경로: Assets/_Project/Resources/Config/UiEffectConfig.asset
생성 메뉴: "Hexiege/Setup/UiEffectConfig 생성"
```

GameSystemRules 근거: 없음 (신규 시스템)

---

### [신규] VfxPoolItem.cs
**경로**: `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.cs`

ParticleSystem 재생이 완료되면 스스로 EffectManager에 반환하는 컴포넌트.

```
필드:
  ParticleSystem      _ps
  GameObject          _sourcePrefab     ← Pool Dictionary 키
  Action<VfxPoolItem> _onReturn         ← 반환 콜백
  bool                _active           ← 중복 반환 방지

메서드:
  void Setup(GameObject prefab, Action<VfxPoolItem> onReturn)
  void Play(Vector3 pos, Quaternion rot)
  void Update()  → !_ps.isPlaying && _active → Return()
  void Return()  → 비활성화 + _onReturn?.Invoke(this)
```

GameSystemRules 근거: FloatingHpTextSpawner Pool 패턴과 동일

---

### [신규] EffectManager.cs
**경로**: `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs`

VFX Pool + AudioSource Pool + Config 참조를 통합 관리하는 핵심 매니저.

```
static Instance:
  static EffectManager Instance   ← Game씬 전용 (DontDestroyOnLoad 없음)
  OnDestroy() → Instance = null

필드:
  [SerializeField] Transform _vfxContainer      ← VFX 오브젝트 부모
  [SerializeField] Transform _sfxContainer      ← AudioSource 오브젝트 부모
  [SerializeField] int _maxConcurrentSfx = 8
  [SerializeField] int _initialVfxPoolSize = 5

  UnitEffectConfig     _unitConfig
  BuildingEffectConfig _buildingConfig
  UiEffectConfig       _uiConfig

  Dictionary<GameObject, Queue<VfxPoolItem>> _vfxPools
  Queue<AudioSource>   _sfxPool
  int                  _activeSfxCount

초기화:
  void Initialize(UnitEffectConfig, BuildingEffectConfig, UiEffectConfig)
  → GameBootstrapper에서 호출

외부 API:
  void PlayUnitAttack(UnitType type, Vector3 pos)
  void PlayUnitDeath(UnitType type, Vector3 pos)
  void PlayBuildingDestroy(BuildingType type, Vector3 pos)
  void PlayBuildingUpgrade(BuildingType type, Vector3 pos)
  void PlayUi(UiEffectKey key)

내부:
  void Play(EffectPreset preset, Vector3 pos)
    → preset null이면 조기 반환
    → vfxPrefab != null → VFX Pool에서 꺼내 재생
    → sfxClip != null && _activeSfxCount < _maxConcurrentSfx → SFX 재생
  VfxPoolItem GetOrCreateVfx(GameObject prefab)
  AudioSource GetOrCreateSfx()
  void OnVfxReturn(VfxPoolItem item)   ← VfxPoolItem 반환 콜백
  void OnSfxComplete(AudioSource src)  ← SFX 재생 완료 후 반환
```

**SFX 완료 감지**: Coroutine으로 `AudioSource.clip.length` 대기 후 반환

**static Instance 방식 선택 이유**:
`UnitView.OnAttackHit()`은 유닛 프리팹 컴포넌트에서 호출되므로
GameBootstrapper를 통한 DI 없이 `EffectManager.Instance`로 직접 접근하는 것이
가장 단순하고 일관성 있습니다.
`SingletonMonoBehaviour`는 `DontDestroyOnLoad`가 포함되어 Game씬 전용에 부적합하므로
static field를 직접 관리합니다.

GameSystemRules 근거: 없음 (신규 시스템)

---

### [수정] UnitView.cs
**경로**: `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

**변경 1 — OnAttackHit()에 공격 VFX/SFX 추가**
```csharp
public void OnAttackHit()
{
    if (_unitData == null || !_unitData.IsAlive) return;
    
    // 공격 이펙트 재생 (VFX + SFX 동시)
    EffectManager.Instance?.PlayUnitAttack(_unitData.Type, transform.position);
    
    StartCoroutine(HitReactionCoroutine());  // 기존 스케일 펀치 유지
}
```

**변경 2 — OnUnitDied 구독 블록에 사망 VFX/SFX 추가**
```csharp
// Destroy(gameObject) 직전에:
EffectManager.Instance?.PlayUnitDeath(_unitData.Type, transform.position);
```

GameSystemRules 근거: 없음 (비주얼 레이어 확장)

---

### [삭제] UnitEffectView.cs
**경로**: `Assets/_Project/Scripts/Presentation/Unit/UnitEffectView.cs`

EffectManager로 완전 대체되므로 삭제합니다.

삭제 순서:
1. 프리팹에서 컴포넌트 제거 에디터 스크립트 실행
2. [6] 사용자 테스트 통과 후 파일 최종 삭제

---

### [수정] GameBootstrapper.cs
**경로**: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

```csharp
[Header("Effect Manager")]
[SerializeField] private EffectManager _effectManager;
[SerializeField] private UnitEffectConfig _unitEffectConfig;
[SerializeField] private BuildingEffectConfig _buildingEffectConfig;
[SerializeField] private UiEffectConfig _uiEffectConfig;

// Map.cs Initialize 흐름에서:
if (_effectManager != null)
    _effectManager.Initialize(_unitEffectConfig, _buildingEffectConfig, _uiEffectConfig);
```

GameSystemRules 근거: 없음 (인프라 확장)

---

## 새 폴더 구조

```
Assets/_Project/Scripts/Presentation/Effects/   ← 신규 폴더
  ├── EffectPreset.cs
  ├── UnitEffectConfig.cs
  ├── BuildingEffectConfig.cs
  ├── UiEffectConfig.cs
  ├── VfxPoolItem.cs
  └── EffectManager.cs

Assets/_Project/Resources/Config/              ← 기존 (UnitStatsConfig 위치)
  ├── UnitEffectConfig.asset                  ← 신규
  ├── BuildingEffectConfig.asset              ← 신규
  └── UiEffectConfig.asset                   ← 신규
```

---

## 구현 순서

```
[1] Effects 폴더 생성
[2] EffectPreset.cs 신규
[3] UnitEffectConfig.cs + BuildingEffectConfig.cs + UiEffectConfig.cs 신규
[4] VfxPoolItem.cs 신규
[5] EffectManager.cs 신규
[6] UnitView.cs 수정 (OnAttackHit + OnUnitDied 연결)
[7] GameBootstrapper.cs 수정 (SerializedField + Initialize)
[8] 에디터 스크립트: UnitEffectConfig.asset / BuildingEffectConfig.asset 자동 생성
[9] 에디터 스크립트: UnitEffectView 컴포넌트 프리팹에서 제거
[10] 사용자에게 Inspector 연결 요청
    - Game.unity에 EffectManager GameObject 배치
    - VFX_Container / SFX_Container 빈 오브젝트 생성
    - GameBootstrapper에 연결
    - UnitEffectConfig에 vfx_pistoleer_attack.prefab 첫 번째로 연결
```

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| EffectManager.Instance가 null | `?.` null 조건부 연산자로 안전 처리 |
| SFX 완료 감지 타이밍 부정확 | `AudioClip.length` + 0.1초 여유를 두고 반환 |
| UnitEffectView가 일부 프리팹에 남아있을 경우 | 에디터 스크립트로 일괄 제거 |
| EffectPreset이 null인 유닛 타입 | null 체크 후 조기 반환 — 이펙트 없이 동작 |
| 건물 VFX 트리거 지점 미확인 | BuildingView가 별도로 존재하면 해당 View에서 연결, 없으면 GameEvents.OnBuildingDied 구독 |
