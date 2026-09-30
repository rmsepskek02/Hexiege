# InfernoSpirit 공격 타임라인 불일치 조사

InfernoSpirit는 공격 동작 자체는 실행되고 있지만, 화면에서 공격 효과와 피격 표현을 발생시키는 표식은 **0.50초**이고 서버가 직접 피해와 단일 대상 DoT를 시작하는 설정은 **1.15초**로 달랐습니다. 이번 작업은 새 투사체 시스템을 만드는 것이 아니라, 현재 production이 실제로 사용하는 하나의 공격 표식에 서버 타임라인을 맞추고 동일한 불일치가 재발하면 자동 검증에서 차단하는 최소 교정입니다.

**문서 상태:** 구현 및 자동 검증 PASS · Android Build And Run 시작 · 사용자 실기 대기 / OPEN

## 1. production 연결과 현재 수치

| 대상 | 확인 결과 |
|---|---|
| UnitType | `InfernoSpirit = 12` |
| Blue/Red 프리팹 | 모두 Controller GUID `a683311d0333a3a4b8ec6aca335e1e2f` 사용 |
| Controller | `Base Layer/Attack`이 clip GUID `aa08d04a7746a8c47a25e9ad6f9b7993` 사용 |
| Attack clip | 30fps, 길이 3.0초, loop, `OnAttackHit` 정확히 1개 @ **0.50초** |
| UnitStatsConfig | cooldown 3.0초, range 4.0, `hitFrameTimes=[1.15]` |
| 공격 VFX | `EffectPreset_InfernoSpirit_Attack` → `vfx_infernospirit_charge.prefab` |
| tracer | `UnitEffectConfig`의 InfernoSpirit `tracerPreset`이 비어 있음 |
| marker 실행 의미 | `UnitView.OnAttackHit`이 0.50초에 공격 VFX/SFX를 실행하고, tracer 없음 폴백으로 피격 표현 콜백도 즉시 실행 |
| 서버 피해 | `ExecuteAttack`이 `hitFrameTimes`를 사용해 1.15초에 직접 피해를 적용한 뒤 `InfernoAttackBehavior`로 같은 대상에 DoT 부여 |
| SpecialAttackConfig | production asset에 `_infernoDotPerSecond: 50`, `_infernoDotDuration: 3`가 실제 직렬화되어 있음 |
| 배선 | Game scene의 `GameBootstrapper`가 production `SpecialAttackConfig.asset`을 참조하고 Setup이 두 값을 `UnitCombatUseCase`에 주입 |
| C3 profile | `Unresolved / ProjectileImpact / Impact 1 / secondary true`, reason `projectile-and-periodic-result-unresolved` |

`1.15초`를 뒷받침하는 별도 Animation Event, tracer 비행시간 또는 서버 발사체 생명주기는 production에 없다. 따라서 현재 production에서 0.50초는 발사 VFX 표식이면서 tracer 없음 폴백 때문에 피격 표현 시점까지 겸하고, 1.15초는 서버 설정에만 남은 독립 지연이다.

## 2. 가설별 판정

| 가설 | 판정 | 근거 |
|---|---|---|
| 0.50초가 현재 production의 실제 표현 Impact다 | **채택** | 정확한 Attack state의 유일한 marker이며 공격 VFX와 tracer 없음 피격 콜백을 같은 호출에서 실행한다 |
| 1.15초가 별도 실제 착탄 시점이다 | **배제** | config 외에 이를 재현하는 marker, tracer duration, 발사체 또는 착탄 callback이 없다 |
| 0.50초 발사와 1.15초 착탄이 이미 분리 구현돼 있다 | **배제** | 발사 VFX는 있으나 tracer preset과 서버 발사체가 없고 피격 표현은 0.50초에 즉시 폴백된다 |
| production 배선 또는 Inferno DoT 직렬화 누락이 원인이다 | **배제** | 양 진영 프리팹·Controller·clip 연결, SpecialAttackConfig YAML, Game scene 참조와 Setup 주입이 모두 존재한다. 기존 AssetMatrix의 YAML 미직렬화 기록은 현재 production과 불일치한다 |

## 3. 최소 교정 결론

- `UnitStatsConfig`의 InfernoSpirit `hitFrameTimes`를 `[1.15]`에서 `[0.50]`으로 Unity 직렬화 API로 교정한다.
- Attack clip marker, Controller, Blue/Red 프리팹, 직접 피해·단일 대상 DoT 의미와 수치는 변경하지 않는다.
- `UnitAttackShadowProfileResolver`는 그대로 `Unresolved / ProjectileImpact / secondary true`로 유지한다. 이번 교정은 전체 원거리 투사체 및 periodic 정규 결과 모델을 완성하지 않는다.
- 영구 self-validation이 production 프리팹 → Controller → 정확한 Attack state clip → 단일 0.50초 marker → config `[0.50]`, tracer 미설정, Inferno DoT production 직렬화·씬 배선을 한 번에 fail-closed로 확인한다.

## 4. 적용 규칙

- `GameSystemRules_Units.md` 규칙 17~20: 서버 타이머와 화면 타격 표현의 시간축을 일치시키고 서버 권위를 유지한다.
- 같은 문서 규칙 41~42: 직접 피해 뒤 동일 주 타깃 적 유닛에만 Inferno DoT를 적용하며 건물 DoT와 AoE를 추가하지 않는다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-002/003`: marker·피격 표현·서버 결과의 단계와 exact scope를 혼동하지 않는다.
- 같은 문서 `NET-CANCEL-003`: 이미 확정된 결과와 미래 marker 허가의 취소 경계를 변경하지 않는다.

## 5. 위험과 비범위

- 0.50초 교정은 현재의 tracer 없는 production을 일치시키는 작업이다. 향후 실제 projectile/tracer를 추가하면 Launch와 Impact를 별도 설계하고 타임라인을 다시 정해야 한다.
- InfernoSpirit를 `Supported` 또는 `Hitscan`으로 승격하지 않는다.
- 전체 원거리 투사체 시스템, facing, 성능 문제, 다른 유닛 타임라인은 비범위다.
- Testcase와 QA 문서는 사용자 지시가 없으므로 작성하지 않는다.

## 6. 구현 결과

- `UnitStatsConfig`의 InfernoSpirit(`UnitType 12`) `hitFrameTimes`를 `[1.15]`에서 `[0.50]`으로 교정했다.
- 첫 임시 자동 교정 스크립트는 Unity `SerializedProperty.enumValueIndex`를 사용해 명시값에 공백이 있는 `UnitType`을 선언 순서로 잘못 해석했다. 그 결과 InfernoSpirit 대신 BoulderSpirit(`UnitType 14`)의 값을 `0.50`으로 변경하는 실패가 발생했다.
- 수정한 임시 교정기는 `intValue`로 실제 직렬화 enum 값을 비교하고, InfernoSpirit=`0.50`과 BoulderSpirit=`1.15`를 한 번에 재검증하여 잘못 변경된 BoulderSpirit을 복구한 뒤 InfernoSpirit 교정을 확정했다.
- 영구 gate인 `RunUnitActionSelfValidation.cs`의 InfernoSpirit `UnitEffectConfig` 행 선택에도 `enumValueIndex`가 남아 있었다. 이 때문에 올바른 production preset이 존재해도 다른 행을 선택하여 `InfernoSpirit must retain the verified production attack preset.`으로 실패했다.
- 영구 gate의 행 선택을 `intValue` 비교로 교정하고, 선언 순서와 실제 직렬화 값의 차이를 설명하는 상세 주석을 추가했다.
- Attack clip, Controller, 양 진영 프리팹, 직접 피해·단일 대상 DoT 의미, Resolver의 `Unresolved / ProjectileImpact / Impact 1 / secondary true` 상태는 변경하지 않았다.

## 7. 자동 검증 및 빌드 상태

재실행에서 다음 결과를 확인했다.

```text
[INFERNO-TIMELINE] PASS: InfernoSpirit=0.50, BoulderSpirit=1.15 production 저장값 재검증
[UAS-DIAG] self-validation PASS
[UAS-DIAG][PRODUCTION-TIMELINE] PASS: QuakeSpirit, BattleAxe, InfernoSpirit
[INFERNO-AUTO][UNIT-ACTION] PASS
[UAS-ROOT-CROSS-AUDIT] self-validation PASS
[INFERNO-AUTO][ROOT-POSE] PASS
[INFERNO-AUTO][FINAL] PASS ... requesting Android Build And Run
```

- Android Build And Run은 실제 URP preprocess/build pipeline에 진입했다.
- 이번 Task의 빌드는 **시작됨**으로만 기록한다. 빌드 완료 확인과 실기 결과 판정은 사용자 소유이며 현재 상태는 `사용자 실기 대기 / OPEN`이다.
- 임시 교정 스크립트는 빌드 호출이 반환된 뒤 삭제하도록 설계되어 있으므로 빌드 진행 중에는 존재할 수 있다. 이 문서 갱신에서는 빌드 완료나 임시 파일 삭제 완료를 확인하지 않았다.

## 8. 재발 방지 결론

- Unity `SerializedProperty`의 enum 필드에서 `enumValueIndex`는 enum 선언 순서이고 `intValue`는 실제 직렬화 값이다.
- 명시값에 공백이 있는 `UnitType`을 식별할 때는 반드시 `intValue`를 사용한다.
- 일회성 교정기만 고쳐서는 충분하지 않다. 같은 행을 선택하는 영구 self-validation gate의 조건도 함께 대조해 동일 오류가 남지 않았는지 확인해야 한다.
