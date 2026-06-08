# Research — EffectManager 시스템 설계 및 구현

## 이 작업이 무엇인지

게임의 모든 VFX(시각 이펙트)와 SFX(사운드)를 하나의 시스템으로 통합 관리하는
**EffectManager**를 새로 만드는 작업입니다.

목표:
- 모든 유닛(25종), 건물, UI의 이펙트를 일관된 방식으로 관리
- Object Pool로 성능 최적화 (GC 발생 없음)
- VFX는 개수 제한 없이 전부 재생 / SFX는 동시 8개 제한
- Inspector에서 데이터만 넣으면 모든 유닛에 자동 적용

---

## 기존 코드 현황

### UnitEffectView.cs (교체 대상)
`Assets/_Project/Scripts/Presentation/Unit/UnitEffectView.cs`

Unity AI가 생성한 컴포넌트. 아래 두 가지 버그/한계가 있어 이번 작업에서 교체됩니다.

**버그**: Muzzle Flash 트리거가 `GameEvents.OnEntityAttacked` (서버 전용) 구독
→ 멀티플레이 클라이언트에서 공격 VFX가 보이지 않음

**한계**: 유닛 프리팹 하나하나에 컴포넌트를 붙이고 Inspector를 설정해야 함
→ 25종 유닛 전부에 적용하기 어려움, 관리 분산

---

### 이벤트별 클라이언트 전파 현황 (중요)

EffectManager가 어떤 이벤트를 구독해야 하는지 판단하기 위해 조사.

| 이벤트 | 발행 위치 | 클라이언트 도달 |
|--------|-----------|----------------|
| `GameEvents.OnEntityAttacked` | 서버 전용 (UnitCombatUseCase) | ❌ 클라이언트 미도달 |
| `GameEvents.OnEntityDamaged` | 서버 발행 → `SyncHealthClientRpc` → 클라이언트 재발행 | ✅ 모두 도달 |
| `GameEvents.OnUnitDied` | 서버 발행 → `EntityDiedClientRpc` → 클라이언트 재발행 | ✅ 모두 도달 |
| `OnAttackHit()` (Animation Event) | 애니메이션 재생 중인 모든 클라이언트에서 로컬 실행 | ✅ 모두 실행 |

**결론**: 공격 VFX/SFX는 `OnEntityAttacked` 대신 `OnAttackHit()` Animation Event 기반으로 트리거해야 모든 클라이언트에서 정상 재생됩니다.

---

### FloatingHpTextSpawner.cs (Pool 패턴 선례)
`Assets/_Project/Scripts/Presentation/UI/FloatingHpTextSpawner.cs`

이미 Queue 기반 Object Pool이 구현되어 있습니다:
- `Queue<FloatingHpText> _pool`
- `GetFromPool()` → 꺼내기 (없으면 Instantiate)
- `ReturnToPool()` → 반환 (비활성화 + Enqueue)
- GameBootstrapper에서 `Initialize()` 호출로 의존성 주입

EffectManager의 VFX Pool과 AudioSource Pool도 동일한 패턴으로 구현합니다.

---

### UnitStatsConfig.cs (매핑 테이블 선례)
`Assets/_Project/Scripts/Infrastructure/Config/UnitStatsConfig.cs`

`List<UnitStatEntry>` 구조로 UnitType별 수치를 ScriptableObject에서 관리합니다.
`UnitEffectConfig`도 동일한 구조 (`List<UnitEffectEntry>`)로 구현합니다.

```csharp
// UnitStatsConfig 구조 (선례)
[System.Serializable]
public struct UnitStatEntry
{
    public UnitType unitType;
    public int maxHp;
    public int attackPower;
    // ...
}
```

---

### UnitType enum — 25종 유닛
`Assets/_Project/Scripts/Domain/Unit/UnitType.cs`

```
Human   : Pistoleer(0), Assault(1), Sniper(2), LittleKnight(3), SpearMan(4),
          BattleAxe(5), Tank(6), CannonCart(7)
Spirit  : FlameSpirit(10), EmberSpirit(11), InfernoSpirit(12), DustSpirit(13),
          BoulderSpirit(14), QuakeSpirit(15), TideSpirit(16), StreamSpirit(17), TorrentSpirit(18)
Trans   : BearGuard(20), FoxMagician(21), LionKnight(22), RhinoBreaker(23),
          EagleArcher(24), RabbitTrickster(25), MushroomBomber(26), BloomFairy(27)
```

총 25종 — 이 모든 유닛에 이펙트가 일관되게 적용되어야 합니다.

---

### BuildingType enum — 32종 건물
`Assets/_Project/Scripts/Domain/Building/BuildingType.cs`

Castle, MiningPost, AutoTower 등 비생산 건물 + 종족별 생산 건물 포함.
건물 파괴 이펙트, 업그레이드 이펙트가 필요합니다.

---

### GameBootstrapper 초기화 패턴
`Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

```csharp
// 현재 패턴 (FloatingHpTextSpawner 예시)
[Header("Floating HP Text")]
[SerializeField] private FloatingHpTextSpawner _floatingHpTextSpawner;
[SerializeField] private FloatingHpText _floatingHpTextPrefab;

// GameBootstrapper.Map.cs에서
_floatingHpTextSpawner.Initialize(_positionProvider, _floatingTextContainer, _floatingHpTextPrefab);
```

EffectManager도 동일한 방식으로 씬에 배치 + GameBootstrapper에서 Initialize.

---

### UnitView.cs — OnAttackHit / OnUnitDied 연결 지점
`Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

**OnAttackHit()**: Animation Event → 현재 스케일 펀치만 실행
→ 여기서 EffectManager.PlayUnitAttack() 추가

**OnUnitDied 구독**: `GameEvents.OnUnitDied` 구독 → `Destroy(gameObject)` 직전
→ EffectManager.PlayUnitDeath() 호출 추가

---

## 영향 범위

| 파일 | 변경 유형 | 설명 |
|------|----------|------|
| `EffectPreset.cs` | 신규 (ScriptableObject) | VFX + SFX 한 세트 정의 |
| `UnitEffectConfig.cs` | 신규 (ScriptableObject) | UnitType별 EffectPreset 테이블 |
| `BuildingEffectConfig.cs` | 신규 (ScriptableObject) | BuildingType별 EffectPreset 테이블 |
| `UiEffectConfig.cs` | 신규 (ScriptableObject) | UI 이펙트 테이블 |
| `VfxPoolItem.cs` | 신규 (MonoBehaviour) | 재생 완료 후 자동 Pool 반환 |
| `EffectManager.cs` | 신규 (MonoBehaviour) | VFX Pool + AudioSource Pool 통합 관리 |
| `UnitView.cs` | 수정 | OnAttackHit/OnUnitDied에서 EffectManager 연결 |
| `UnitEffectView.cs` | 삭제 | EffectManager로 완전 대체 |
| `GameBootstrapper.cs` | 수정 | EffectManager SerializedField + Initialize |
