# Plan — Pistoleer 공격 VFX 적용 (Object Pool)

## 이 Plan이 무엇인지

이 작업은 3단계로 이루어집니다:

1. **VFX Object Pool 시스템 구축** — VFX 오브젝트를 재사용하는 Pool을 새로 만듭니다.
2. **UnitEffectView 개선** — 기존 컴포넌트를 Pool 방식으로 바꾸고, 멀티플레이에서도 올바르게 동작하도록 수정합니다.
3. **Pistoleer 프리팹 연결** — 만든 VFX 프리팹을 Inspector에 연결합니다.

> **중요 — 기존 로직 변경**
>
> `UnitEffectView`의 Muzzle Flash 트리거가 `GameEvents.OnEntityAttacked`(서버 전용)에서
> `UnitView.OnAttackHit()` Animation Event(모든 클라이언트)로 변경됩니다.
>
> 변경 이유: 현재 방식은 멀티플레이 클라이언트에서 VFX가 보이지 않는 버그가 있습니다.
> "제거"가 아닌 "올바른 방식으로 교체"이므로 별도 비활성화 없이 바로 수정합니다.

---

## 파일별 변경 내용

### [신규] VfxPoolItem.cs
**경로**: `Assets/_Project/Scripts/Presentation/Unit/VfxPoolItem.cs`

ParticleSystem이 재생 완료되면 스스로 VfxPoolManager에 반환하는 컴포넌트.

```
역할:
  - VfxPoolManager에서 꺼낼 때 Play(위치, 회전) 호출
  - Update()에서 매 프레임 ParticleSystem.isPlaying 확인
  - 재생이 끝나면 비활성화 + VfxPoolManager에 반환 콜백 호출

필드:
  - ParticleSystem _ps
  - GameObject _sourcePrefab     ← Pool Key (어느 풀에 돌아갈지 식별)
  - Action<VfxPoolItem> _onReturn  ← 반환 콜백 (VfxPoolManager가 설정)
  - bool _isPlaying               ← 재생 중 여부 (중복 반환 방지)

메서드:
  - void Setup(GameObject prefab, Action<VfxPoolItem> onReturn)
  - void Play(Vector3 pos, Quaternion rot)
  - void Update() — isPlaying 종료 감지 → Return()
  - void Return()
```

GameSystemRules 근거: 없음 (신규 시스템, Presentation 레이어 Pure 비주얼)

---

### [신규] VfxPoolManager.cs
**경로**: `Assets/_Project/Scripts/Presentation/Unit/VfxPoolManager.cs`

VFX 타입별(프리팹 기준) Object Pool을 관리하는 전역 매니저.

```
역할:
  - 프리팹을 Key로 Queue<VfxPoolItem>를 관리
  - Play(prefab, pos, rot): 풀에서 꺼내 지정 위치에 재생
  - ReturnToPool(item): 재생 완료된 VfxPoolItem 반환
  - GameBootstrapper.Start()에서 Initialize(_container) 호출

static Instance 패턴:
  - static VfxPoolManager Instance — Game씬 전용 (DontDestroyOnLoad 없음)
  - OnDestroy 시 Instance = null 정리

필드:
  - Dictionary<GameObject, Queue<VfxPoolItem>> _pools
  - Transform _container          ← VFX 오브젝트 부모 (Inspector 연결)
  - int _initialPoolSizePerType = 5

메서드:
  - void Initialize(Transform container)
  - void Play(GameObject prefab, Vector3 pos, Quaternion rot)
  - void ReturnToPool(VfxPoolItem item)
  - VfxPoolItem CreateInstance(GameObject prefab)
```

**왜 SingletonMonoBehaviour를 사용하지 않는가**:
SingletonMonoBehaviour는 DontDestroyOnLoad가 포함되어 씬 전환 시에도 유지됩니다.
VfxPoolManager는 Game씬 전용이므로 씬이 바뀌면 함께 파괴되어야 합니다.
따라서 static field를 직접 관리합니다.

GameSystemRules 근거: 없음 (신규 시스템)

---

### [수정] UnitEffectView.cs
**경로**: `Assets/_Project/Scripts/Presentation/Unit/UnitEffectView.cs`

**변경 1 — Muzzle Flash 트리거 제거**
- `Start()`의 `GameEvents.OnEntityAttacked` 구독 삭제
- `PlayMuzzleFlash()` → `PlayAttackVfx()`로 이름 변경 + `public`으로 변경
- 이유: 서버 전용 이벤트 → Animation Event(모든 클라이언트)로 교체

**변경 2 — Pool 사용**
- `PlayAttackVfx()`: `Instantiate()` → `VfxPoolManager.Instance.Play()` 사용
- `PlayHitEffect()`: `Instantiate()` → `VfxPoolManager.Instance.Play()` 사용
- VfxPoolManager.Instance가 null이면 (씬 전환 등) 조기 반환으로 안전 처리

**변경 없는 부분**:
- `GameEvents.OnEntityDamaged` 구독은 유지 (Hit Effect는 기존 방식이 올바름)
- `_muzzleFlashPrefab`, `_hitEffectPrefab`, `_muzzleTransform` 필드 유지

GameSystemRules 근거: 없음 (비주얼 레이어 변경)

---

### [수정] UnitView.cs
**경로**: `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

**변경 1 — UnitEffectView 참조 추가**
```csharp
// Awake()에서
_effectView = GetComponent<UnitEffectView>();
// null이어도 동작 (UnitEffectView가 없는 유닛은 VFX 없음)
```

**변경 2 — OnAttackHit()에 VFX 연결**
```csharp
public void OnAttackHit()
{
    if (_unitData == null || !_unitData.IsAlive) return;
    
    _effectView?.PlayAttackVfx();   // ← 추가
    StartCoroutine(HitReactionCoroutine());
}
```

GameSystemRules 근거: 없음 (비주얼 레이어 확장)

---

### [수정] GameBootstrapper.cs
**경로**: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

```
추가 내용:
  [Header("VFX Pool")]
  [SerializeField] private VfxPoolManager _vfxPoolManager;

Start() 또는 Initialize() 내부:
  _vfxPoolManager.Initialize(_vfxContainer);
  // _vfxContainer는 씬의 빈 GameObject Transform
```

**씬 설정**:
- Game.unity에 `VfxPoolManager` 컴포넌트를 가진 GameObject 배치
- VFX 오브젝트 부모용 빈 GameObject 배치 (예: `VFX_Container`)
- GameBootstrapper Inspector에 두 오브젝트 연결

GameSystemRules 근거: 없음 (인프라 확장)

---

### [Inspector 연결] Pistoleer 프리팹
**대상**: `Unit_Pistoleer_Blue` / `Unit_Pistoleer_Red` 프리팹

```
UnitEffectView 컴포넌트:
  _muzzleFlashPrefab = vfx_pistoleer_attack.prefab
  _muzzleTransform   = 총구 위치 Transform (없으면 비워도 됨 — 유닛 중심 사용)
  _hitEffectPrefab   = (미정 — 이번 작업 범위 외)
```

**에디터 스크립트 (1회성)**:
`Hexiege/Setup/Pistoleer VFX 연결` 메뉴로 Inspector 작업을 자동화.

---

## 구현 순서

```
[1] VfxPoolItem.cs 신규 작성
[2] VfxPoolManager.cs 신규 작성
[3] UnitEffectView.cs 수정 (Pool 사용 + public PlayAttackVfx)
[4] UnitView.cs 수정 (OnAttackHit에 VFX 연결)
[5] GameBootstrapper.cs 수정 (VfxPoolManager SerializedField)
[6] Game.unity에 VfxPoolManager GameObject + VFX_Container 배치 안내
[7] Pistoleer 프리팹 Inspector 연결 에디터 스크립트
[8] 사용자에게 Inspector 연결 실행 요청
```

---

## 위험 요소 및 대응

| 위험 | 대응 |
|------|------|
| VfxPoolManager.Instance가 null인 상태에서 Play() 호출 | null 체크 후 조기 반환 |
| ParticleSystem이 즉시 재생 완료되는 경우 중복 반환 | `_isPlaying` 플래그로 방지 |
| 기존 `OnEntityAttacked` 구독 제거 | 더 올바른 AnimationEvent 방식으로 교체되므로 기능 유지됨 |
| Pistoleer 외 유닛에 UnitEffectView 없을 경우 | `_effectView = GetComponent<UnitEffectView>()` null 허용 (Nullable 연산자 `?.` 사용) |
