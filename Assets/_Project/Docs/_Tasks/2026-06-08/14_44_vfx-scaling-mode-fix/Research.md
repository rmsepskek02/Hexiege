# Research — VFX 크기 및 스폰 위치 개선

## 작업 목적 (자연어 설명)

유닛 이펙트(공격/사망 VFX)와 관련된 두 가지 문제를 해결한다.

**문제 1 — 크기:** VFX가 게임에서 너무 크게 보임.
원인은 Unity ParticleSystem의 `Scaling Mode`가 `Local`로 설정되어 있어,
루트 GameObject의 Transform 스케일을 줄여도 파티클 크기에 반영이 안 되기 때문이다.
세 프리팹의 모든 ParticleSystem 컴포넌트의 `Scaling Mode`를 `Hierarchy`로 일괄 변경하면
루트 스케일 하나로 전체 이펙트 크기를 조절할 수 있게 된다.

**문제 2 — 스폰 위치:** 공격 VFX가 유닛 발 위치에서 발생함.
현재 `UnitView.OnAttackHit()`이 `transform.position`(유닛 루트, 발 위치)을 그대로 전달하고 있어
총구/무기 위치가 아닌 발 아래에서 이펙트가 재생된다.
유닛 프리팹에 빈 GO `VfxSpawnPoint`를 추가하고 그 위치를 전달하면 정확한 지점에서 재생된다.

---

## 대상 프리팹

| 프리팹 경로 | 포함된 ParticleSystem 수 | 현재 루트 스케일 |
|------------|------------------------|----------------|
| `Assets/_Project/Prefabs/VFX/Units/vfx_pistoleer_attack.prefab` | 4개 | (1, 1, 1) |
| `Assets/_Project/Prefabs/VFX/Units/vfx_tank_attack.prefab` | 4개 | (1, 1, 1) |
| `Assets/_Project/Prefabs/VFX/Units/vfx_unit_death.prefab` | 3개 | (0.1, 0.1, 0.1) |

## 현재 상태 분석

### scalingMode 값 의미
| 값 | 모드 | 동작 |
|----|------|------|
| 0 | Hierarchy | 부모 Transform 스케일까지 상속 |
| 1 | Local | 자신의 로컬 스케일만 참조 (부모 무시) |
| 2 | Shape | 생성 범위(Shape)만 영향, 파티클 크기에는 무영향 |

세 프리팹의 모든 ParticleSystem이 `scalingMode: 1` (Local)로 설정되어 있음. → **YAML 직접 확인으로 근거 확보**

### Local 모드인 이유
- Unity에서 ParticleSystem을 새로 생성하면 기본값이 Local(1)
- 이 프로젝트에서는 VFX가 유닛의 자식이 아닌 `EffectManager._vfxContainer`에 스폰되므로 Local이어야 할 이유 없음
- 의도적 설계가 아닌 Unity 기본값 방치로 판단

### EffectManager 스폰 방식 확인
```csharp
// EffectManager.cs:281
GameObject go = Instantiate(prefab, _vfxContainer);
```
`_vfxContainer`는 씬의 빈 GameObject (스케일 1,1,1). 유닛 스케일 변화의 영향 없음.
→ Hierarchy 모드로 변경해도 유닛 스케일 애니메이션(Scale Punch)에 의한 부작용 없음.

## 스폰 위치 문제 분석

### 현재 코드 흐름
```csharp
// UnitView.cs:1503
EffectManager.Instance?.PlayUnitAttack(_unitData.Type, transform.position);
// transform.position = 유닛 루트 GO 위치 (발 위치)
```

### VfxSpawnPoint 방식 선택 이유
- **오프셋 방식(A)**: Inspector에서 Vector3 값으로 조절. 편하지만 모델 크기/포즈가 달라지면 맞지 않음
- **스폰포인트 방식(B)**: 유닛 프리팹에 빈 GO를 총구 위치에 직접 배치. 정확하고 모델 기준으로 고정됨 → **채택**

### 영향 범위
| 항목 | 내용 |
|------|------|
| `UnitView.cs` | `_vfxSpawnPoint` SerializeField 추가, `OnAttackHit()` 위치 참조 수정 |
| `Unit_Pistoleer_Blue.prefab` | `VfxSpawnPoint` 자식 GO 추가 후 Inspector 연결 |
| `Unit_Pistoleer_Red.prefab` | 동일 |
| 기타 유닛 | `_vfxSpawnPoint`가 null이면 `transform.position` 폴백 → 기존 동작 유지 |

## 영향 범위 (전체)

**ScalingMode 수정:**
- **변경 대상**: 3개 VFX 프리팹 내 ParticleSystem 컴포넌트의 scalingMode 필드
- **코드 변경 없음**: EffectManager, UnitEffectConfig 등 C# 코드는 수정 불필요
- **런타임 동작 변화**: 루트 Transform 스케일이 파티클 크기/속도/생성범위에 전부 반영됨
- **부작용 리스크**: 낮음. `_vfxContainer`(스케일 1,1,1) 하위에서 재생되므로 의도치 않은 스케일 상속 없음

**스폰 위치 수정:**
- **변경 대상**: `UnitView.cs` + `Unit_Pistoleer_Blue/Red.prefab` (피스톨러 우선 적용)
- **기타 유닛**: `_vfxSpawnPoint == null` 시 기존 `transform.position` 폴백 → 영향 없음
