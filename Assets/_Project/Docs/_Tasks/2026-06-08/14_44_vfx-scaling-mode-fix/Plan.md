# Plan — VFX 크기 및 스폰 위치 개선

## 작업 목적 (자연어 설명)

두 가지 작업을 순서대로 진행한다.

**작업 1 — ScalingMode 수정 (완료):**
Unity 에디터 스크립트를 작성해서 유닛 VFX 프리팹 3개의 모든 ParticleSystem Scaling Mode를
`Local`에서 `Hierarchy`로 자동 변경한다. 이후 루트 스케일로 이펙트 크기를 조절한다.

**작업 2 — 스폰 위치 수정 (피스톨러 우선):**
유닛 프리팹에 `VfxSpawnPoint` 빈 GO를 추가하고, `UnitView`가 그 위치를 참조하도록 수정한다.
정확한 총구/무기 위치에서 VFX가 재생되며, 스폰포인트가 없는 유닛은 기존 발 위치로 폴백한다.

---

## 구현 방법

### [작업 1] ScalingMode 수정 에디터 스크립트 ✅ 완료
- **파일**: `Assets/_Project/Scripts/Editor/VfxScalingModeFixer.cs`
- **메뉴 경로**: `Hexiege/Setup/Fix VFX Scaling Mode (Units)`
- **동작**:
  1. 대상 프리팹 3개를 `AssetDatabase`로 로드
  2. 각 프리팹 내 모든 `ParticleSystem` 컴포넌트를 `GetComponentsInChildren<ParticleSystem>(true)` 로 수집
  3. `mainModule.scalingMode`를 `ParticleSystemScalingMode.Hierarchy` 로 변경
  4. `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets` 로 저장
  5. 결과를 `Debug.Log`로 출력
- 1회성 스크립트 — 실행 완료 후 삭제해도 무방

### [작업 2] VfxSpawnPoint 스폰 위치 수정
**수정 파일 1: `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`**
- `[SerializeField] private Transform _vfxSpawnPoint;` 필드 추가
- `OnAttackHit()` 수정:
  ```csharp
  Vector3 spawnPos = _vfxSpawnPoint != null ? _vfxSpawnPoint.position : transform.position;
  Quaternion spawnRot = Quaternion.LookRotation(transform.forward);
  EffectManager.Instance?.PlayUnitAttack(_unitData.Type, spawnPos, spawnRot);
  ```

**⚠️ 회전 출처 분리 (핵심 결정):**
- VfxSpawnPoint가 스켈레톤 본(손 부위) 하위에 배치되어 있음 → 월드 회전에 본 회전`(0, -90, -90)`이 포함됨
- `_vfxSpawnPoint.rotation` 사용 시: 본 회전이 섞여 엉뚱한 방향으로 VFX 발사
- **위치**: `_vfxSpawnPoint.position` 사용 → 본에 붙어있으므로 총구 월드 위치 정확
- **회전**: `Quaternion.LookRotation(transform.forward)` 사용 → 유닛 루트는 Y축만 회전하므로 facing 방향 정확

**수정 파일 2: `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs`**
- `PlayUnitAttack(UnitType type, Vector3 pos)` → `PlayUnitAttack(UnitType type, Vector3 pos, Quaternion rot)` 로 시그니처 확장
- `Play(preset, pos)` → `Play(preset, pos, rot)` 로 내부 전달
- `item.Play(pos, Quaternion.identity)` → `item.Play(pos, rot)` 로 수정
- 기존 `PlayUnitDeath` / `PlayBuildingDestroy` 등 나머지 메서드는 `Quaternion.identity` 유지 (방향 무관)

**Inspector 작업: `Unit_Pistoleer_Blue.prefab`, `Unit_Pistoleer_Red.prefab`**
- `VfxSpawnPoint` 빈 자식 GO는 이미 추가됨 (사용자 완료)
- `VfxSpawnPoint` 로컬 회전은 `(0, 0, 0)` — 유닛이 이미 적 방향으로 회전하므로 별도 회전 불필요
- `UnitView._vfxSpawnPoint` 슬롯에 연결 (Inspector 수동)

---

## 위험 요소

| 항목 | 내용 | 대응 |
|------|------|------|
| startSize 값 유지 | scalingMode만 바꾸고 startSize는 건드리지 않음 → 기존 파티클 크기 설계 유지 | 변경 전후 비교 확인 |
| vfx_unit_death 루트 스케일 | 이미 0.1로 설정되어 있으므로 Hierarchy로 바꾸면 파티클이 매우 작아질 수 있음 | 실행 후 루트 스케일 재조정 필요 |
| 프리팹 경로 변경 | 경로가 하드코딩되어 있으므로 프리팹 이동 시 스크립트 경로도 수정 필요 | 1회성이므로 무방 |
| 기타 유닛 폴백 | `_vfxSpawnPoint == null` 시 `transform.position` 사용 → 피스톨러 외 유닛은 기존 동작 유지 | 폴백 코드 반드시 포함 |
| VfxSpawnPoint 본 하위 배치 | VfxSpawnPoint가 스켈레톤 본 자식이라 rotation에 본 회전이 포함됨 → `_vfxSpawnPoint.rotation` 사용 금지 | position만 참조, rotation은 `transform.forward`로 대체 |

---

## 진행 순서 및 상태

- [x] **[작업 1]** VfxScalingModeFixer.cs 에디터 스크립트 작성 완료
- [x] **[작업 1]** 메뉴 `Hexiege/Setup/Fix VFX Scaling Mode (Units)` 실행 (사용자)
- [x] **[작업 1]** 각 프리팹 루트 Transform Scale 조절하여 크기 확인 — 루트 스케일로 크기 조절 가능 확인 ✅
- [x] **[작업 2]** `UnitView.cs` 수정 — `_vfxSpawnPoint` 필드 + `OnAttackHit()` 위치는 SpawnPoint, 회전은 `transform.forward`
- [x] **[작업 2]** `EffectManager.cs` 수정 — `PlayUnitAttack` 시그니처에 `Quaternion rot` 추가
- [x] **[작업 2]** `Unit_Pistoleer_Blue/Red.prefab` — `VfxSpawnPoint` GO 추가 + Inspector 연결 (사용자)
- [x] **[작업 2]** 회전 방향 불일치 수정 — `_vfxSpawnPoint.rotation` 대신 `Quaternion.LookRotation(transform.forward)` 사용
- [x] **[작업 3]** `vfx_unit_death.prefab` — 퍼짐 효과 제거 (3개 PS startSpeed → 0)
- [x] Unity Play Mode에서 실기 확인 — 전체 조정 완료 확인 ✅

**전체 완료 일시**: 2026-06-08

### [작업 3] vfx_unit_death 퍼짐 효과 제거
**원인**: 3개 ParticleSystem의 `startSpeed` + cone Shape(angle 25°)이 방사형 퍼짐 유발

| PS 이름 | minMaxState | 변경 전 | 변경 후 |
|---------|------------|--------|--------|
| `vfx_unit_death` (루트) | 0 (Constant) | scalar: 0.2 | scalar: 0 |
| `Lingerer` | 0 (Constant) | scalar: 0.3 | scalar: 0 |
| `PuffBurst` | 3 (Random) | scalar: 2.6 / minScalar: 1.8 | scalar: 0 / minScalar: 0 |

**파일**: `Assets/_Project/Prefabs/VFX/Units/vfx_unit_death.prefab` (YAML 직접 수정)
