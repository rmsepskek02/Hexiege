# Research — 모든 유닛 사망 시 vfx_unit_death 이펙트 적용

## 작업 목적

현재 피스톨러(Pistoleer) 유닛이 사망할 때만 `vfx_unit_death` 파티클 이펙트가 재생되고 있다.
나머지 유닛들도 사망할 때 동일한 이펙트가 발생하도록 설정을 확장한다.
코드 변경 없이 에디터 스크립트를 통해 `UnitEffectConfig.asset`을 일괄 수정하는 방식으로 해결한다.

---

## 사망 VFX 재생 흐름

사망 VFX는 두 경로에서 호출된다:

### 싱글플레이 / 멀티플레이 서버
- `UnitView.cs:481` — `EffectManager.Instance?.PlayUnitDeath(_unitData.Type, transform.position)`
- GameEvents.OnUnitDied 구독자에서 `IsDead` Animator Bool을 true로 설정한 뒤 호출

### 멀티플레이 클라이언트
- `NetworkUnit.cs:183` — `OnNetworkDespawn()`에서 `EffectManager.Instance?.PlayUnitDeath(unitView.UnitData.Type, transform.position)`
- 서버가 `NetworkObject.Despawn(destroy:true)` 호출 → NGO가 클라이언트의 `OnNetworkDespawn` 실행 → 이펙트 재생 후 GO 파괴

### EffectManager 내부 동작
- `PlayUnitDeath(UnitType type, Vector3 pos)` → `UnitEffectConfig.GetDeath(type)` → `EffectPreset` 조회 → `Play(preset, pos, identity)` 호출
- `preset == null` 이면 아무것도 재생하지 않고 즉시 반환 (현재 대부분 유닛의 상태)

---

## 현재 UnitEffectConfig.asset 상태

경로: `Assets/_Project/Resources/Config/UnitEffectConfig.asset`

| unitType | 값 | attackPreset | deathPreset |
|----------|----|-------------|-------------|
| 0 (Pistoleer) | ✅ | EffectPreset_Pistoleer_Attack | **EffectPreset_Pistoleer_Death** |
| 1 ~ 27 (나머지 전체) | ❌ | null | **null** → VFX 없음 |

---

## 피스톨러 deathPreset 상세

파일: `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_Pistoleer_Death.asset`
- guid: `3ec9b19b1a309c44cb205ebf040e29cb`
- `_vfxPrefab`: `vfx_unit_death.prefab` (guid: `d4479632ec8a4394e974cb2671013fce`)
  - 경로: `Assets/_Project/Prefabs/VFX/Units/vfx_unit_death.prefab`
- `_sfxClip`: 사망 사운드 (guid: `271d925c26f6d8e45b5e650a33895446`)
- `_sfxVolume`: 1.0

이름이 "Pistoleer_Death"이지만 내용은 범용 사망 VFX+SFX이므로, 공통 이펙트로 재사용 가능하다.

---

## 영향 범위

- **코드 변경 없음**: `UnitView.cs`, `NetworkUnit.cs`, `EffectManager.cs`, `UnitEffectConfig.cs` 모두 이미 올바르게 구현되어 있음
- **에셋 변경만 필요**: `UnitEffectConfig.asset`의 각 유닛 항목에 `deathPreset` 연결 필요
- 총 유닛 수: 24종 (Pistoleer 제외 23종에 deathPreset 연결 필요)

---

## 두 가지 접근법 검토

### 접근법 A: 기존 EffectPreset_Pistoleer_Death.asset 재사용 (에디터 스크립트)
- 기존 에셋을 그대로 23개 유닛에 연결
- 장점: 에셋 파일 추가 없음, 빠름
- 단점: 에셋 이름이 "Pistoleer_Death"로 오해를 유발할 수 있음

### 접근법 B: 공통 EffectPreset 에셋 생성 후 전체 연결 (에디터 스크립트)
- `EffectPreset_Unit_Death_Common.asset` 신규 생성 후 전체 24종 연결
- 장점: 이름으로 의도가 명확, 향후 유지보수 혼동 없음
- 단점: 에셋 파일 1개 추가

→ **접근법 B 권장**: 이름으로 의도가 명확하여 장기적 유지보수에 유리함

---

## 관련 파일

| 파일 | 역할 |
|------|------|
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs:481` | 싱글/서버 사망 VFX 호출 지점 |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs:183` | 클라이언트 사망 VFX 호출 지점 |
| `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs:170` | PlayUnitDeath 구현 |
| `Assets/_Project/Scripts/Presentation/Effects/UnitEffectConfig.cs` | 유닛별 이펙트 설정 ScriptableObject |
| `Assets/_Project/Resources/Config/UnitEffectConfig.asset` | 수정 대상 에셋 |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_Pistoleer_Death.asset` | 참조할 기존 이펙트 프리셋 |
| `Assets/_Project/Prefabs/VFX/Units/vfx_unit_death.prefab` | 실제 VFX 프리팹 |
