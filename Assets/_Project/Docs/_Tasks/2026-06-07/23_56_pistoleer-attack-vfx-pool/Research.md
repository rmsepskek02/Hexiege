# Research — Pistoleer 공격 VFX 적용 (Object Pool)

## 이 작업이 무엇인지

Unity AI로 제작한 `vfx_pistoleer_attack.prefab`을 Pistoleer 유닛의 공격에 연결하는 작업입니다.
단순히 연결하는 것에 그치지 않고, 여러 유닛이 동시에 공격할 때도 성능 저하 없이 VFX가 재생될 수 있도록
**Object Pool** 방식을 적용합니다.

VFX를 재생할 때마다 새로 만들고 삭제하면 메모리 쓰레기(GC)가 발생합니다.
Object Pool은 VFX 오브젝트를 미리 만들어두고 재사용하는 방식으로 이 문제를 해결합니다.

---

## 현재 코드 상태

### UnitEffectView.cs (이미 존재)

`Assets/_Project/Scripts/Presentation/Unit/UnitEffectView.cs`

Unity AI가 생성한 컴포넌트. 유닛 프리팹에 직접 부착하는 방식.

```
_muzzleFlashPrefab  — 공격 이펙트 프리팹 (Inspector 연결)
_hitEffectPrefab    — 피격 이펙트 프리팹 (Inspector 연결)
_muzzleTransform    — 총구 위치 Transform (Inspector 연결, null이면 유닛 중심 사용)
```

**Muzzle Flash 트리거**: `GameEvents.OnEntityAttacked` 구독
→ `PlayMuzzleFlash()` : `Instantiate()` 단순 생성 (Object Pool 없음)

**Hit Effect 트리거**: `GameEvents.OnEntityDamaged` 구독
→ `PlayHitEffect()` : `Instantiate()` 단순 생성 (Object Pool 없음)

---

### ⚠️ 버그 발견: Muzzle Flash 멀티플레이에서 서버에서만 재생됨

`GameEvents.OnEntityAttacked`는 **서버에서만 발행**됩니다:
- `UnitCombatUseCase.ExecuteAttack()` → 서버 전용
- `TowerCombatUseCase.ExecuteAttack()` → 서버 전용

클라이언트(Client)에는 이 이벤트가 전달되지 않기 때문에
현재 구현대로면 멀티플레이에서 **서버(Host)에서만 Muzzle Flash가 보이고 Client에서는 안 보입니다**.

#### 올바른 트리거: Animation Event (OnAttackHit)

공격 애니메이션의 타격 프레임에서 모든 클라이언트에 동일하게 실행됩니다.

흐름:
```
[서버가 StartCombatClientRpc 전송]
→ [모든 클라이언트에서 Attack 애니메이션 재생]
→ [타격 프레임에서 AnimationEventRelay.OnAttackHit() 호출]
→ [UnitView.OnAttackHit() 호출]
→ [여기서 VFX 재생하면 모든 클라이언트에서 정상 작동]
```

Pistoleer의 OnAttackHit 타이밍: **0.833초** (메모리 기록)

#### Hit Effect 는 정상

`GameEvents.OnEntityDamaged`는 서버가 발행 → `NetworkHealthSync.SyncHealthClientRpc`로
클라이언트에도 전파 → 클라이언트 측에서도 `OnEntityDamaged` 재발행
→ 멀티플레이에서도 정상 작동.

---

### FloatingHpTextSpawner.cs (Object Pool 선례)

`Assets/_Project/Scripts/Presentation/UI/FloatingHpTextSpawner.cs`

이미 Queue 기반 Object Pool을 사용하는 패턴이 존재합니다:
- `Queue<FloatingHpText> _pool` — 비활성 오브젝트 큐
- `GetFromPool()` — 꺼내기 (없으면 새로 Instantiate)
- `ReturnToPool()` — 반환 (비활성화 후 Enqueue)
- `CreateInstance()` — 생성 + 반환 콜백 설정
- GameBootstrapper에서 Initialize()로 의존성 주입

→ VfxPoolManager는 이 패턴을 따릅니다.

---

### SingletonMonoBehaviour.cs

`Assets/_Project/Scripts/Core/SingletonMonoBehaviour.cs`

DontDestroyOnLoad 싱글톤 베이스. 
VfxPoolManager는 Game씬 전용이므로 **DontDestroyOnLoad는 사용하지 않습니다**.
대신 static field로 간단하게 접근 가능하도록 구현합니다.

---

### GameBootstrapper.cs (VfxPoolManager 등록 위치)

`Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

`FloatingHpTextSpawner`가 이미 SerializedField로 등록되어 있습니다.
동일한 방식으로 VfxPoolManager를 씬에 배치하고 GameBootstrapper에 연결합니다.

---

### 기존 VFX/Pool 관련 파일

- VFX 관련 스크립트: **없음**
- Pool 관련 스크립트: **없음** (FloatingHpTextSpawner만 Pool 내장)

---

## 영향 범위

| 파일 | 변경 유형 | 이유 |
|------|----------|------|
| `UnitEffectView.cs` | 수정 | Pool 사용, Muzzle Flash 트리거 방식 변경 |
| `UnitView.cs` | 수정 | `OnAttackHit()`에서 VFX 트리거 연결 |
| `VfxPoolItem.cs` | 신규 | ParticleSystem 완료 후 자동 Pool 반환 컴포넌트 |
| `VfxPoolManager.cs` | 신규 | VFX 타입별 Pool 관리자 |
| `GameBootstrapper.cs` | 수정 | VfxPoolManager SerializedField + Initialize 호출 |
| Pistoleer 프리팹 | Inspector 연결 | UnitEffectView에 VFX 프리팹 연결 |

---

## 위험 요소

1. **GameEvents.OnEntityAttacked 구독 제거**
   - 기존 동작(서버에서만 VFX 재생) 대신 AnimationEvent 기반으로 교체
   - 모든 클라이언트에서 동작하게 되어 더 올바른 방식

2. **ParticleSystem 재생 완료 감지**
   - `!particleSystem.isPlaying` 체크는 파티클이 모두 소멸된 후에 true가 됨
   - 파티클 수명(duration + startLifetime)이 끝나야 반환 → 설정값에 따라 반환 타이밍이 달라짐

3. **VfxPoolManager가 UnitView보다 늦게 초기화될 경우**
   - `UnitView.OnAttackHit()`은 게임 시작 후에만 호출됨
   - GameBootstrapper.Start()에서 VfxPoolManager가 초기화되므로 타이밍 문제 없음
