# Plan — 모든 유닛 사망 시 vfx_unit_death 이펙트 적용

## 작업 목적

피스톨러(Pistoleer) 외 모든 유닛이 사망할 때도 `vfx_unit_death` 이펙트가 재생되도록,
공통 사망 이펙트 프리셋 에셋을 생성하고 `UnitEffectConfig.asset`의 전체 유닛에 연결한다.

> ⚠️ **기존 로직 제거 없음**: 코드 변경이 없으므로 이 규칙은 해당 없음.

---

## 선택한 접근법

**접근법 B**: 공통 EffectPreset 에셋 신규 생성 후 전체 유닛에 연결

- 이름이 명확한 에셋(`EffectPreset_Unit_Death_Common.asset`)을 만들어 의도를 분명히 함
- `EffectPreset_Pistoleer_Death.asset`도 공통 에셋으로 교체하여 일관성 유지
- 코드 변경 없음 — 에셋 작업만으로 완료

---

## 구현 단계

### [1] 공통 사망 이펙트 에셋 생성 (에디터 스크립트)

에디터 스크립트 `SetUnitDeathVfxAll.cs`를 작성한다:

1. `EffectPreset_Unit_Death_Common.asset` 생성
   - `_vfxPrefab`: `vfx_unit_death.prefab` (guid: `d4479632ec8a4394e974cb2671013fce`)
   - `_sfxClip`: 기존 사망 SFX (guid: `271d925c26f6d8e45b5e650a33895446`)
   - `_sfxVolume`: 1.0
2. `UnitEffectConfig.asset`을 로드
3. 모든 `_entries`의 `deathPreset`을 `EffectPreset_Unit_Death_Common`으로 일괄 설정
4. 에셋 저장 (`AssetDatabase.SaveAssets()`)

**메뉴 경로**: `Hexiege/Setup/Set Unit Death VFX (All Units)`

### [2] 사용자 에디터 스크립트 실행

메뉴 → 실행 → 완료 확인 후 스크립트 파일 삭제 (1회성)

---

## 수정 파일 목록

### 신규 생성
- `Assets/Editor/SetUnitDeathVfxAll.cs` — 1회성 에디터 스크립트 (실행 후 삭제)
- `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_Unit_Death_Common.asset` — 공통 사망 이펙트 프리셋

### 에셋 수정 (에디터 스크립트 실행 결과)
- `Assets/_Project/Resources/Config/UnitEffectConfig.asset` — 전체 24종 `deathPreset` → 공통 에셋으로 연결

---

## 영향 범위

- **코드 파일**: 변경 없음
- **런타임 동작 변경**: 피스톨러 외 23종 유닛 사망 시 `vfx_unit_death` + 사망 SFX 재생됨
- **기존 피스톨러 동작**: 동일한 VFX+SFX 유지 (에셋만 변경, 내용은 동일)
- **멀티플레이**: 서버(+싱글플레이)는 `UnitView`에서, 클라이언트는 `NetworkUnit.OnNetworkDespawn`에서 자동 처리됨

---

## 위험 요소

- EffectPreset_Pistoleer_Death.asset을 더 이상 참조하지 않게 되므로 사용하지 않는 에셋이 됨
  → 삭제해도 무방하나 이 작업에서는 삭제하지 않음 (필요 시 별도 정리)
- 에디터 스크립트 실행 전에 반드시 Unity 에디터에서 recompile이 완료되어야 함

---

## 테스트 시나리오

1. 싱글플레이에서 각 종족별 유닛(인간, 정령, 초월계)을 전투시켜 사망 VFX 확인
2. 멀티플레이에서 Host와 Client 양쪽에서 사망 VFX 확인
