# FoxMagician production 공격 타임라인 교정 계획

> **최신 상태 — 2026-09-28 사용자 재테스트 반영:** Fox 피해 표시 지연 focused 교정은 사용자 수용. 약간 이른 현행 타이밍은 유지하고 재튜닝은 향후 projectile 구현으로 이관한다. 전체 migration은 미완이며 최신 근거는 §21이다. 이전 OPEN 기록은 당시 이력이다.

이번 작업은 FoxMagician의 현행 서버 피해 시점을 실제 공격 애니메이션 marker와 맞추는 최소 교정이다. 화면에 없는 권위 발사체나 tracer를 새로 꾸며내거나 지원 완료로 승격하지 않는다. exact production 연결과 미지원 경계를 자동 검증한 뒤 두 Unity 검증 메뉴를 실행하고, PASS와 최신 C# 컴파일 오류 0건을 확인하면 Android Build And Run을 시작한다. 빌드 완료 확인과 실제 게임 테스트는 사용자가 진행한다.

**문서 상태:** `marker/config 및 bounded read-only diagnostic 구현·자동 검증 PASS · 후속 빌드 시작 전 · 실제 경기 로그 미검증으로 FoxMagician VFX/1.00초 runtime 상관관계는 미판정`

## 1. 완료 조건

1. FoxMagician production Attack clip의 유일한 `OnAttackHit` marker가 `1.00초`임을 exact asset으로 검증한다.
2. `UnitStatsConfig`의 명시값 `unitType=21` entry가 `hitFrameTimes=[1.00]` 하나만 갖는다.
3. production controller의 Attack state가 해당 clip을 직접 사용하고 state speed `1.0`을 유지한다.
4. clip은 `4.00초 · 30fps · Loop Time=ON`을 유지한다.
5. Blue/Red production prefab이 exact controller를 사용하고 `ApplyRootMotion=OFF`를 유지한다.
6. 양 prefab UnitView가 각 prefab의 유일한 VfxSpawnPoint를 참조하며 local position `(0, 0, 0.011)`을 유지한다.
7. `UnitEffectConfig → EffectPreset_FoxMagician_Attack → vfx_foxmagician_charge` 연결과 ParticleSystem 존재를 검증한다.
8. resolver는 `Unresolved / ProjectileImpact / TimerImpact / projectile-system-unresolved`을 유지하고 tracer 미연결 상태를 허위로 채우지 않는다.
9. 공통 runtime/resolver/animation/controller/prefab/VFX asset 동작은 변경하지 않는다.
10. Runtime/Editor C# 정적 컴파일 오류 0건, 문서 정합성 오류 0건이다.
11. 두 Unity self-validation 메뉴가 PASS이고 Console error가 0건이다.
12. Android Build And Run은 시작까지만 수행한다. 빌드 완료와 실제 게임 판정은 사용자가 진행한다.

## 2. 단계 A — 서버 TimerImpact 시점 교정

`UnitStatsConfig.asset`에서 명시값 `unitType=21`인 FoxMagician entry의 `hitFrameTimes`를 `2.25`에서 `1.00`으로 바꾼다. `attackCooldown=4.00`과 다른 스탯은 변경하지 않는다.

직렬화 entry 탐색과 검증에는 `enumValueIndex`를 사용하지 않는다. `(int)UnitType.FoxMagician`, 즉 저장된 명시값 `21`을 사용한다.

이 변경은 현재 서버가 사용하는 TimerImpact의 결과 시점만 옮긴다. Animation Event가 피해 writer가 되지 않으며 Client marker가 서버 결과를 생성하지 않는다.

**변경 파일:**

- `Assets/_Project/Resources/Config/UnitStatsConfig.asset`

**규칙 근거:** Units 규칙 17·19, `NET-AUTH-001`, `NET-PRESENT-001~003`.

## 3. 단계 B — FoxMagician production gate 추가

Unit Action Self Validation에 FoxMagician exact production gate를 추가한다. 기존 production 검증의 exact asset 추적 방식을 재사용하되 FoxMagician의 지원 상태와 에셋 사실만 검사한다.

검증 항목은 다음과 같다.

- UnitType 직렬화 entry를 `intValue=21`로 찾는다.
- `FoxMagician_Attack.anim`이 `4.00초 · 30fps · Loop Time=ON`인지 확인한다.
- 유일한 `OnAttackHit @ 1.00초`를 확인한다.
- `FoxMagician.controller`의 Attack state motion이 exact clip이고 speed가 `1.0`인지 확인한다.
- Blue/Red prefab의 Animator가 exact controller를 사용하고 `ApplyRootMotion=false`인지 확인한다.
- 양 prefab의 UnitView가 유일한 `VfxSpawnPoint`를 참조하며 local position이 `(0, 0, 0.011)`인지 확인한다.
- `UnitEffectConfig`의 FoxMagician attack preset, preset의 exact VFX prefab과 ParticleSystem 존재를 확인한다.
- runtime `hitFrameTimes`가 `[1.00]`인지 확인한다.
- resolver가 `Unresolved / ProjectileImpact / TimerImpact / projectile-system-unresolved`인지 확인한다.
- 권위 projectile/tracer가 아직 연결되지 않았다는 경계를 허위 PASS 없이 유지한다.

StreamSpirit 전용 runtime observer는 복제하지 않는다. 이번 구현은 설정과 production gate까지만 수행하고, 실제 기기 로그로 원인을 분리하기 어려울 때만 후속 단계에서 별도의 bounded 진단을 설계한다.

**변경 파일:**

- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

**규칙 근거:** Units 규칙 15·17·22, `NET-PRESENT-001~003`, `NET-DELIVERY-PROJECTILE`, `NET-CANCEL-001`.

## 4. 단계 C — 상태 문서 갱신

구현과 자동 검증 결과를 `UnitCombatAssetMatrix.md`의 FoxMagician 행과 이 Task 문서에 반영한다.

- marker/config 정렬과 production gate 결과만 사실대로 기록한다.
- 권위 발사체 미구현과 `MigrationRequired / Unresolved / LegacyFallback` 상태는 유지한다.
- 실제 게임 테스트 전에는 Complete 또는 전체 ProjectileImpact 지원 완료로 표시하지 않는다.
- 빌드 시작과 빌드 완료, 사용자 실기 결과를 서로 구분한다.

**변경 파일:**

- `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`
- 이 Task의 `Research.md`
- 이 Task의 `Plan.md`

**규칙 근거:** `WORKFLOW.md` 완료 후 업데이트 체크리스트, `GameSystemRules_UnitCombatSynchronization.md`의 지원/미지원 fail-closed 경계.

## 5. 단계 D — 정적·Unity 자동 검증

구현 뒤 다음 순서로 검증한다.

1. Runtime C# 정적 컴파일 오류 0건.
2. Editor C# 정적 컴파일 오류 0건.
3. `Tools/check_docs.py` 문서 정합성 오류 0건.
4. Unity 메뉴 `Hexiege → Combat → Run Unit Action Self Validation` 실행 후 PASS 확인.
5. Unity 메뉴 `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit` 실행 후 PASS 확인.
6. Unity Console error 0건 확인.

두 메뉴 중 하나라도 PASS가 아니거나 C#·Console 오류가 있으면 Build And Run을 시작하지 않고 원인을 교정한다.

## 6. 단계 E — 빌드 시작과 사용자 실기 인계

자동 gate를 모두 통과하면 Unity에서 Android Build And Run을 **시작만** 한다. 빌드 완료를 기다리거나 완료 여부를 대신 판정하지 않는다.

사용자 실기에서는 실제 생산 경로로 FoxMagician을 여러 기 생산해 다음을 확인한다.

- 피해 숫자와 공격 VFX가 같은 공격 동작의 `1.00초` marker 부근에서 보이는지.
- 첫 공격과 연속 공격에서 VFX가 빠지거나 한 회차에 두 번 나오지 않는지.
- 공격 직전 멈춤, 이동 순간이동, 공격 방향 이탈이 새로 발생하지 않는지.
- 타겟 사망·변경 직전의 표현이 새 타겟으로 옮겨 붙지 않는지.
- Host/Client 결과와 표현 로그가 현행 LegacyFallback 계약을 벗어나지 않는지.

실기 로그 분석 전에는 이 Task를 CLOSED로 바꾸지 않는다.

## 7. 후속 Task 경계 — 서버 권위 발사체

이번 Task 이후에도 FoxMagician은 `MigrationRequired / Unresolved / LegacyFallback`이다. 다음 별도 작업에서만 다음 내용을 설계·구현한다.

- 공격 회차에 결합된 권위 발사체 ID와 발사 시점.
- 서버 궤적 또는 도착 판정과 착탄 위치.
- 공격자·타겟 사망, despawn, 취소 시 발사체 생명주기.
- 발사·tracer·착탄·결과의 Host/Client exact-key 상관관계.
- 중복·지연·순서 역전·late result 억제.
- resolver의 `Supported / ProjectileImpact` 승격 조건.

이번 `1.00초` TimerImpact 정렬은 이 후속 구현의 대체물이 아니다.

## 8. 위험과 대응

| 위험 | 대응 |
|---|---|
| 실제 발사체 없이 Supported 승격 | resolver 상태와 reason을 exact 검증하고 변경 금지 |
| 다른 유닛 스탯 행 오염 | `enumValueIndex` 사용 금지, `unitType intValue=21` exact entry만 수정 |
| 잘못된 clip을 검사해 허위 PASS | prefab controller → Attack state motion → clip GUID 전체 연결 검사 |
| 정적 VFX 연결을 실제 재생 PASS로 오인 | 빌드 후 사용자 실기와 로그 전에는 미확정 유지 |
| StreamSpirit 전용 observer 복제로 공통 구조 복잡화 | 이번 단계에서는 production gate와 설정만 추가 |
| 공통 runtime 또는 asset에 불필요한 변경 | 변경 예정 파일을 제한하고 조사 대상 파일은 불변으로 검토 |
| 자동 PASS를 실기 완료로 오인 | Build 시작까지만 자동 진행, 완료·실기 판정은 사용자 담당 |

## 9. 변경 예정 파일 요약

### 확정 변경

- `Assets/_Project/Resources/Config/UnitStatsConfig.asset`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`
- `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`
- 이 Task의 `Research.md`, `Plan.md`

### 조사·검증 대상이지만 변경하지 않음

- `Assets/_Project/Animations/Units/FoxMagician/FoxMagician_Attack.anim`
- `Assets/_Project/Animations/Units/FoxMagician/FoxMagician.controller`
- `Assets/_Project/Prefabs/Units/Transcendence/Unit_FoxMagician_Blue.prefab`
- `Assets/_Project/Prefabs/Units/Transcendence/Unit_FoxMagician_Red.prefab`
- `Assets/_Project/Resources/Config/UnitEffectConfig.asset`
- `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_FoxMagician_Attack.asset`
- `Assets/_Project/Prefabs/VFX/Units/vfx_foxmagician_charge.prefab`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs`
- 공통 runtime presentation/observer 코드

Testcase.md는 사용자 지시에 따라 생성하지 않는다.

## 10. 구현 진행 상태 — 2026-09-22

| 단계 | 상태 | 결과 |
|---|---|---|
| 조사 및 가설 판정 | 완료 | `2.25초` TimerImpact와 `1.00초` marker 불일치 확정 |
| A 서버 TimerImpact 시점 교정 | 완료 | 명시값 `unitType=21`의 `hitFrameTimes`만 `[1.00]`으로 교정 |
| B production gate 추가 | 완료 | exact clip/controller/prefab/VFX/resolver 미지원 경계 검증 추가, runtime observer는 불변 |
| C 상태 문서 갱신 | 완료 | Asset Matrix와 Task 문서에 구현 및 자동 검증 결과 반영 |
| D 정적·Unity 자동 검증 | 완료 | Tundra success, Runtime/Editor CS 0건, 문서 검사 문제 없음, 두 Unity self-validation PASS, Console error 0 |
| E Build And Run 시작 | 시작 완료 | `File → Build And Run` 실행 후 `Checking prerequisites / Starting Android build` 표시 확인. 빌드 완료 확인과 실기 테스트는 사용자 담당 |

## 11. 자동 검증 결과와 현재 인계 상태 — 2026-09-22

- Unity forced synchronous recompile은 Tundra build success이며 Runtime/Editor C# 오류 `CS`는 0건이다.
- UTF-8 환경의 `Tools/check_docs.py`는 문제 없음으로 완료됐다.
- `Run Unit Action Self Validation`은 PASS했고 `[UAS-DIAG][PRODUCTION-TIMELINE] PASS`에 FoxMagician exact production timeline contracts가 포함됐다.
- `Self Validate Unit Root Pose Cross Audit`은 PASS했다.
- Unity Console은 error 0건, 기존 warning 1건이다.
- 단계 D까지 완료했고 단계 E의 Android Build And Run 시작도 확인했다. 화면에 `Checking prerequisites / Starting Android build`가 표시됐으며 빌드 완료 여부는 확인하지 않았다. 빌드 완료 확인과 실제 게임 테스트는 사용자가 진행한다.
- 실제 production 공격과 runtime VFX 재생은 아직 검증하지 않았다. 따라서 이 Task는 실기 PASS 또는 Complete가 아니다.
- 서버 권위 발사체와 tracer는 미구현 상태이므로 `MigrationRequired / Unresolved / LegacyFallback`을 유지한다.

## 12. 최신 실기 로그 판정 — 2026-09-22

최신 실기에서는 FoxMagician 89기가 생산됐고 공통 공격·결과·표현·이동 계약에서 오류가 확인되지 않았다. Host는 commits 114, impactSamples 109이며 correlationFailures·invalid·targetMismatches·dropped·manifestFailures가 모두 0이다. Coordinator는 schedules 119 / results 109 / ready 109 / pending 0 / failures 0 / duplicates 0이고, Client는 accepted 109 / rejected 0이다. 공통 결과 표현도 expectedVisual 108 / presentationEmits 108이며 viewUnavailable·duplicateAttempts·transportFailures·failures가 모두 0이다.

양측 local movement/ROOT는 PASS이고 이동·복제 오류는 없다. latest cross analyzer의 `INCONCLUSIVE`는 day-level Host 로그에 같은 movement observer schema가 여러 세션 들어 있어 역할 분류를 거부한 패키징 문제이므로 gameplay movement failure로 분류하지 않는다.

그러나 공통 terminal에는 FoxMagician의 `attack-start → OnAttackHit marker → source VFX attempt/result → server TimerImpact`가 같은 공격 key로 연결되어 있지 않다. `presentationEmits=108`도 공통 결과 표현 수치이며 FoxMagician source VFX의 실제 재생 성공 수치가 아니다. 따라서 실제 VFX 재생과 정확한 1.00초 정렬은 미판정으로 유지한다. 기존 단계 B의 “실기 로그가 부족하면 별도 bounded 진단을 설계한다”는 조건이 충족됐다.

## 13. 후속 bounded runtime diagnostic 계획

이 단계는 gameplay를 고치는 구현이 아니라, 현재 미관측인 FoxMagician production 흐름을 한 회차 단위로 증명하거나 실패 원인을 분리하기 위한 읽기 전용 진단이다.

### 13.1 관측 범위와 필터

- `UnitType.FoxMagician`이면서 현행 표현 모드가 `LegacyFallback`인 공격만 받는다.
- 공격 시작, `OnAttackHit` marker, source VFX attempt, VFX started/failure, ParticleSystem 수와 playback 상태, 서버 TimerImpact를 관측한다.
- 각 단계는 구현 조사에서 확인한 **같은 현행 공격 key**를 그대로 전달·재사용한다. 시간 근접, 공격자 FIFO 또는 “가장 오래된 미완료 회차”를 새 상관관계 규칙으로 만들지 않는다.
- Host와 Client는 동일 key를 기록하되 서버 TimerImpact는 서버에서만 관측한다.

### 13.2 bounded state와 terminal summary

- 상태 저장소는 고정 상한을 두고 완료 회차를 우선 retire한다. 중복, 미결합, overflow와 detail drop을 서로 다른 카운터로 남긴다.
- 정상 세부 로그에도 전체 line budget과 UTF-8 길이 제한을 적용하고 terminal summary용 예산을 별도로 보존한다.
- terminal summary에는 최소한 starts, markers, vfxAttempts, vfxStarted, VFX failure 원인, particle/playback evidence, timerImpacts, complete/incomplete, unmatched 단계별 수치, duplicates, overflow, dropped와 최대 `start→marker`, `start→TimerImpact`, `marker→TimerImpact` 차이를 포함한다.
- 완료 판정은 Host/Client의 실제 VFX started 및 playback 증거와 Host TimerImpact가 동일 key로 결합된 표본에만 적용한다. 공통 presentation emit이나 정적 preset 연결로 대체하지 않는다.
- 모든 terminal과 detail은 `gameplayWrites=0`을 명시한다.

### 13.3 read-only 불변식

- 피해 승인·적용, HP, 공격 회차 발급, 타겟, 방향, 쿨다운, TimerImpact 예약/실행 순서를 쓰지 않는다.
- Animator 상태·재생 위치, marker gate, source-marker lease, VFX/SFX 호출과 ParticleSystem 재생 결과를 변경하지 않는다.
- 진단 누락·overflow·불완전 회차는 관측 실패로 남기며 fallback 재생이나 gameplay 보정으로 복구하지 않는다.
- `MigrationRequired / Unresolved / LegacyFallback`과 권위 projectile/tracer 미지원 상태를 유지한다.

**규칙 근거:** Units 규칙 17~20, `NET-AUTH-001~002`, `NET-ACTION-IDEMPOTENT`, `NET-TIME-001~004`, `NET-PRESENT-001~004`, `NET-DELIVERY-PROJECTILE`.

## 14. 구현 조사 대상과 변경 후보

아래 목록은 후속 game-programmer가 실제 호출 경로와 현행 공격 key 전달 가능성을 확인하기 위한 후보이며, 아직 확정 변경 파일 목록이 아니다. 조사 결과 한 파일 안에서 기존 이음매를 재사용할 수 있으면 범위를 줄이고, 새 helper가 필요하면 같은 read-only·bounded 계약 안에서만 확정한다.

### 14.1 우선 후보

- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`
  - FoxMagician 전용 bounded flow, 단계별 카운터와 terminal summary 후보.
  - 기존 전역 line budget과 terminal reserve를 침범하지 않는지 조사한다.
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
  - 공격 시작 수신, `OnAttackHit`, `EffectManager.PlayUnitAttack` 반환 직후에 같은 현행 공격 key를 전달할 수 있는지 조사한다.
  - VFX 호출 순서와 반환값은 읽기만 하고 변경하지 않는다.
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
  - 실제 Legacy TimerImpact writer 반환 직후 같은 현행 공격 key를 전달할 수 있는지 조사한다.
  - 피해 writer와 예약·적용 순서는 변경하지 않는다.
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`
  - FoxMagician+LegacyFallback 필터, read-only, bounded capacity, 단계별 배선과 `gameplayWrites=0` 불변식을 자동 검증할 필요가 있는지 조사한다.

### 14.2 확정 불변 파일·상태

후속 진단 구현에서도 다음은 변경하지 않는다.

- `Assets/_Project/Resources/Config/UnitStatsConfig.asset`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs`
- `Assets/_Project/Animations/Units/FoxMagician/FoxMagician_Attack.anim`
- `Assets/_Project/Animations/Units/FoxMagician/FoxMagician.controller`
- `Assets/_Project/Prefabs/Units/Transcendence/Unit_FoxMagician_Blue.prefab`
- `Assets/_Project/Prefabs/Units/Transcendence/Unit_FoxMagician_Red.prefab`
- `Assets/_Project/Resources/Config/UnitEffectConfig.asset`
- `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_FoxMagician_Attack.asset`
- `Assets/_Project/Prefabs/VFX/Units/vfx_foxmagician_charge.prefab`
- resolver 지원 분류, 피해 writer, animation/controller/prefab/VFX asset, projectile/tracer 지원 상태

구현 조사 뒤 실제 변경 파일을 이 절에 확정하고, 범위가 후보를 벗어나면 이유와 read-only 경계 유지 근거를 먼저 기록한다. Testcase.md는 만들지 않는다. 구현과 자동 gate가 끝나더라도 실기 전에는 Complete/PASS로 표시하지 않으며, Android Build And Run 완료 확인은 사용자 담당이고 에이전트는 시작만 수행한다.

## 15. 후속 진단 구현 및 자동 검증 결과 — 2026-09-23

후속 bounded runtime diagnostic 구현을 완료했다. 현행 `Unresolved / LegacyFallback` gameplay는 그대로 두고, 다음 실기에서 FoxMagician의 source VFX와 1.00초 TimerImpact 정렬을 직접 판정할 수 있는 증거만 추가했다.

### 15.1 확정 변경 파일

- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`
- 이 Task의 `Research.md`, `Plan.md`

설정·animation·controller·Blue/Red prefab·EffectPreset·VFX prefab·resolver는 변경하지 않았다. Testcase.md도 만들지 않았다.

### 15.2 exact correlation과 bounded 상태

- `foxmagician-production-timeline-v1`은 FoxMagician의 `LegacyFallback`만 받는다.
- 서버 `ExecuteAttack`에서 개발 진단 전용 correlation ticket을 예약해 타격 코루틴에 전달하고, presentation revision 발급 뒤 `(unitId, revision)` key에 Bind한다.
- TimerImpact는 exact ticket mapping으로 flow를 찾는다. 시간 근접, 공격자 FIFO 또는 가장 오래된 미완료 회차 추정을 사용하지 않는다.
- flow와 ticket mapping은 64회차 상한을 함께 지킨다. 완료 flow 우선 retire, overflow·duplicate·unmatched·dropped 분리, unmatched TimerImpact의 고아 mapping 회수 규칙을 적용했다.
- terminal은 VFX started/failure뿐 아니라 `particlePositive`, `playbackActive`, `maxParticleSystems`와 세 시간축 최대 오차를 포함한다.
- 모든 detail/terminal은 `gameplayWrites=0`이며 관측 실패를 gameplay 보정으로 복구하지 않는다.

### 15.3 자동 검증

| 검증 | 결과 |
|---|---|
| Unity Runtime/Editor 재컴파일 | C# 오류 0건 |
| `Tools/check_docs.py` UTF-8 | 문제 0건 |
| `Hexiege → Combat → Run Unit Action Self Validation` | PASS |
| `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit` | PASS |
| Unity Console | error 0건, 기존 warning 1건 |

첫 self-validation 실행에서는 새 Fox 검증이 production UTF-8 preflight 메서드를 public으로 바꿔 기존 C1의 non-public reflection gate와 충돌했다. 메서드를 기존 `internal` seam으로 복원하고 Fox 검증도 동일 reflection 경로를 사용하도록 교정한 뒤 재컴파일과 두 메뉴를 다시 실행해 PASS를 확인했다.

**현재 상태:** 구현과 자동 검증은 완료됐지만 새 runtime evidence는 아직 없다. Android Build And Run은 다음 단계에서 시작만 하며, 완료 확인과 실제 FoxMagician 생산 테스트는 사용자가 담당한다. 새 Host/Client 로그에서 exact ticket, VFX particle/playback, TimerImpact가 같은 flow로 결합된 것을 확인하기 전에는 이 Task를 Complete 또는 실기 PASS로 표시하지 않는다.

## 15. 후속 diagnostic 구현 및 자동 검증 결과 — 2026-09-23

§13의 bounded read-only diagnostic이 구현됐다. 후보 파일을 조사한 결과 새 파일 없이 §14.1의 네 파일만 수정했으며, §14.2의 설정·resolver·에셋·지원 상태 불변 범위는 유지했다. 구현은 실제 공격 흐름을 바꾸지 않고 FoxMagician production 사건을 exact correlation ticket으로 연결하는 개발 진단이다.

### 15.1 계획 대비 구현

| 계획 항목 | 구현 결과 |
|---|---|
| 전용 필터 | `UnitType.FoxMagician + LegacyFallback`만 관측 |
| 스키마 | `foxmagician-production-timeline-v1` |
| bounded flow | 최대 64, 완료된 가장 오래된 flow 우선 retire |
| 관측 단계 | attack start / marker / VFX attempt / VFX result / 서버 TimerImpact |
| VFX 실제 재생 증거 | started/failure, `particlePositive`, `playbackActive`, `maxParticleSystems` 집계 |
| exact correlation | 공격 실행 경계에서 개발 진단 ticket 예약 → `ExecuteAttack` coroutine으로 전달 → presentation revision 발급 시 `(unitId, revision)` key에 Bind → TimerImpact에서 같은 ticket 소비 |
| FIFO 금지 | 시간 근접·공격자 FIFO·가장 오래된 미완료 flow 추정 결합을 사용하지 않음 |
| 양방향 binding | ticket→key와 key→ticket dictionary 모두 64 상한, 완료 retire와 unmatched TimerImpact에서 cleanup |
| read-only | 모든 detail/terminal `gameplayWrites=0`; gameplay·Animator·VFX 상태 쓰기 없음 |
| terminal | 단계별 수량, particle/playback, complete/incomplete, unmatched, duplicate, overflow/drop, 최대 시간 차이와 expected `1.000초` 출력 |

### 15.2 확정 수정 파일

- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`
  - 64-flow 저장소, 양방향 ticket binding, 단계별 기록, retire/cleanup과 terminal summary.
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
  - presentation revision 기준 attack start·marker·VFX attempt/result 관측 배선.
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
  - correlation ticket 예약·`ExecuteAttack` 전달·presentation revision Bind·실제 TimerImpact 반환 직후 기록.
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`
  - 스키마, FoxMagician+LegacyFallback 필터, capacity 64, exact ticket, read-only, 필수 배선과 terminal 필드 검증.

### 15.3 자동 검증 결과

| 검증 | 결과 |
|---|---|
| Runtime/Editor Unity recompile | C# `CS` 오류 0건 |
| `Tools/check_docs.py` | 문제 0건 |
| `Run Unit Action Self Validation` | PASS |
| `Self Validate Unit Root Pose Cross Audit` | PASS |
| Unity Console | error 0건 / warning 1건 |

### 15.4 남은 gate와 현재 상태

- 후속 Android Build And Run은 아직 시작하지 않았다.
- `foxmagician-production-timeline-v1`이 포함된 실제 Host/Client 경기 로그는 아직 없다.
- 따라서 실제 VFX started/playback, particle 수, exact flow 완결성과 marker↔TimerImpact 1.00초 정렬은 미검증이다.
- 자동 검증 PASS를 Task Complete/PASS 또는 runtime timing PASS로 확대하지 않는다.
- `MigrationRequired / Unresolved / LegacyFallback`과 권위 projectile/tracer 미지원 상태를 유지한다.
- Testcase.md는 만들지 않는다. 빌드는 향후 에이전트가 시작만 할 수 있고 완료 확인과 실기 판정은 사용자가 담당한다.

## 16. VFX 시작과 피해 Impact 분리 교정 계획 — 2026-09-27

### 16.1 목표

FoxMagician의 source charge VFX는 production marker 1.00초에 시작하고, 실제 서버 피해는 VFX 후반부인 2.25초에 적용되도록 두 시간축을 다시 분리한다. 이전 교정처럼 marker와 피해 offset을 같은 값으로 강제하지 않는다.

### 16.2 구현 항목

1. `UnitStatsConfig.asset`
   - 명시값 `unitType=21`의 단일 `hitFrameTimes`를 `1.00`에서 기존 baseline `2.25`로 복원한다.
   - 공격력·쿨다운·사거리와 다른 유닛 행은 변경하지 않는다.
2. `UnitFactory.cs` (구현 직전 발견으로 범위 추가)
   - 서버 생산과 Client 생성의 공통 `HitFrameTimes` 적용 지점에서 FoxMagician은 검증된 config 2.25초를 보존한다. 현재 두 경로가 Attack clip의 `OnAttackHit @ 1.00`으로 이 값을 다시 덮어쓰므로 설정 변경만으로는 효과가 없다.
   - 나머지 유닛은 기존 animation event 추출을 그대로 사용한다. Fox의 VFX marker 1.00초도 그대로 둔다.
3. `RunUnitActionSelfValidation.cs`
   - Fox production gate의 `vfxMarkerTime=1.00`과 `damageImpactTime=2.25`를 별도 상수로 검증한다.
   - Attack clip의 유일한 `OnAttackHit`은 1.00초에 유지한다.
   - runtime config의 단일 `hitFrameTimes`는 2.25초를 요구한다.
   - `vfxMarkerTime < damageImpactTime < attackCooldown` 순서와 기존 controller/clip/prefab/VFX/resolver 경계를 함께 fail-closed한다.
   - 양쪽 UnitFactory 생성 경로가 Fox 설정을 보존하는지 검증한다.
4. `UnitAttackShadowObserver.cs`
   - Fox 진단의 expected impact offset을 2.25초로 갱신하고 terminal/preflight/self-validation이 같은 값을 사용하게 한다.
   - 대량 생산에서 64개 동시 flow가 포화된 원인을 보강하되, 완료 flow 우선 retire·exact ticket·bounded/read-only 계약은 유지한다.
   - 정상 detail을 무제한으로 늘리지 않고 terminal 판정에 필요한 집계가 보존되도록 detail drop 정책을 점검한다.
5. Task 문서
   - 과거 2.25→1.00 변경 기록은 삭제하지 않고, 최신 실기에서 잘못된 가설로 반증됐음을 후속 정정으로 남긴다.
   - Testcase.md는 사용자 별도 지시가 없으므로 작성하지 않는다.

### 16.3 규칙 근거

- `GameSystemRules_Units.md` 규칙 17: Animation Event는 표현 marker이며 피해 writer가 아니다.
- 같은 문서 규칙 19: 실제 피해는 서버 권위 단일 경로에서 적용한다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-AUTH-001~002`: 표현 도착이나 Client 사건이 gameplay 피해를 쓰지 않는다.
- 같은 문서 `NET-TIME-001~004`: 공격 회차의 예약 시각과 실제 결과 시각을 명시하고 지연을 다른 회차에 재결합하지 않는다.
- 같은 문서 `NET-PRESENT-001~004`: source marker와 확정 결과는 같은 회차로 결합하되 동일 시각이라고 가정하지 않는다.
- 같은 문서 `NET-DELIVERY-PROJECTILE`: 권위 projectile이 없는 상태에서 허위 tracer/착탄을 만들거나 Supported로 승격하지 않는다.

### 16.4 회귀 위험과 대응

| 위험 | 대응 |
|---|---|
| marker를 다시 피해 시점으로 간주 | production gate에서 두 값을 서로 다른 이름과 값으로 검증 |
| 피해 결과 표현이 다음 공격 marker까지 지연 | exact rendezvous가 marker 선도 후 같은 scope 결과 도착 시 즉시 방출하는 기존 계약을 보존하고 self-validation 범위를 확인 |
| 다른 유닛 설정 오염 | 명시값 `unitType=21` 단일 행만 수정하고 전체 production gate 실행 |
| 설정을 바꿔도 런타임이 marker로 덮어씀 | UnitFactory 양쪽 생성 경로에서 Fox 예외를 검증하고 실제 `UnitData.HitFrameTimes`를 확인 |
| 2.25초를 최종 튜닝 PASS로 오판 | 구현 상태는 사용자 실기 전 OPEN; 육안상 VFX 후반부와 HP/피해 숫자 일치 확인 후 확정 |
| 진단 보강이 gameplay를 변경 | `gameplayWrites=0`, writer/쿨다운/타겟/Animator/VFX 호출 불변을 자동 검증 |
| 대량 로그 포화 재발 | flow capacity와 detail budget을 분리하고 terminal 집계를 우선 보존 |

### 16.5 검증 및 인계

1. Runtime/Editor C# 오류 0건을 확인한다.
2. `Tools/check_docs.py` 문제 0건을 확인한다.
3. Unity 메뉴 `Run Unit Action Self Validation` PASS를 확인한다.
4. Unity 메뉴 `Self Validate Unit Root Pose Cross Audit` PASS를 확인한다.
5. 두 메뉴 PASS 뒤 Android Build And Run은 시작만 한다. 빌드 완료 확인과 실기는 사용자가 담당한다.
6. 실기에서는 VFX 시작 직후가 아니라 후반부에 HP 감소·피해 숫자가 발생하는지, VFX 누락·중복과 공격 방향 회귀가 없는지 확인한다.

### 16.6 예정 수정 파일

- `Assets/_Project/Resources/Config/UnitStatsConfig.asset`
- `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs` (생성 시 marker 덮어쓰기 발견으로 추가)
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`
- 이 Task의 `Research.md`, `Plan.md`

`UnitView.cs`, `NetworkCombatController.cs`, Attack clip/controller, 양 팀 unit prefab, VFX prefab, resolver와 피해 writer는 이번 교정에서 변경하지 않는다.

## 17. 2026-09-27 구현·자동 검증 결과 및 인계

- 계획 16의 설정/서버·Client 생성 경로/진단/production gate/규칙·에셋 문서 변경을 구현했다. Testcase.md는 별도 지시가 없어 만들지 않았다.
- Fox VFX marker `1.00초`, 서버 피해 설정 `2.25초`, 공격 쿨다운 `4.00초`의 순서와 양쪽 `UnitFactory` 예외를 검증했다. 다른 유닛은 기존 marker 사용을 유지한다.
- 진단 terminal의 1,000바이트 사전 검사 실패를 수정했고 최악값 995바이트다. 최초 FAIL을 숨기지 않고 수정 후 Unity `Run Unit Action Self Validation` PASS와 `Self Validate Unit Root Pose Cross Audit` PASS를 확인했다. 최종 Console error 0건, 문서 검사 문제 0건이다.
- 두 PASS 후 Unity `File → Build And Run`을 실행했으나 `Can not sign the application — Unable to sign the application; please provide passwords!` 대화상자가 표시돼 실제 빌드는 시작되지 않았다. 프로젝트는 `androidUseCustomKeystore: 1`, `{inproject}: hexiege-release.keystore`, alias `hexiege`를 사용한다. 서명 설정을 변경하거나 비밀번호를 추측하지 않는다.
- 사용자가 Unity에 키스토어/alias 비밀번호를 직접 입력한 뒤 Build And Run을 다시 실행해야 한다. 빌드 완료 확인과 실기 테스트는 사용자가 담당한다. 실제 VFX 후반부와 HP/피해 숫자 일치, 누락·중복, 방향 회귀를 확인하고 로그를 저장해야 최종 판정한다.
- 현재 상태: **코드 교정·자동 검증 PASS / 서명 비밀번호 부재로 Android 빌드 미시작 / 실기 미검증 / Task OPEN**. `MigrationRequired / Unresolved / LegacyFallback`은 유지한다.

## 18. 2026-09-27 동일 경기 로그 확보 후 계획 판정 정정

§17의 빌드 미시작·실기 미검증은 당시 인계 상태로 보존한다. 후속 Editor Host/Android Client 동일 `sharedSessionKey=67d84b7da7a97b206fe075c2e654380618b2be790896c23f4f164865a2c1d6a1`의 실제 로그 분석은 [Research.md §18](Research.md)의 원본 경로와 재계산 방법을 따른다. Host는 시작 250·marker/VFX 시작 246·TimerImpact 245·완료 242, Client는 시작 250·marker/VFX 시작/완료 217이다. 양쪽 VFX 실패·unmatched·중복·overflow·dropped는 0이며 Client TimerImpact 0은 서버 전용 관측 경계다. Host에서 상세로 결속 가능한 10회차의 marker→TimerImpact는 `1.239891~1.278933초`(평균 `1.255681초`), 그중 실제 피해 적용 6회차는 `1.239891~1.259609초`(평균 `1.250256초`)다. 상세 64줄 상한 때문에 전 회차에 이 범위를 확대하지 않는다.

계획 §16.5의 **로그상 시점 분리·양쪽 VFX 재생 시작 확인**은 위 범위에서 충족했다. 그러나 `playbackActive=True`는 시작 직후 재생 상태이지 VFX의 시각적 종료 증거가 아니다. Host 미완료 8·Client 미완료 33도 취소와 누락 중 어느 쪽인지 단정하지 않는다. **다음 확인은 사용자가 별도로 수행할 Fox 육안 테스트**(VFX 후반부의 HP 감소·피해 숫자와 누락/중복·방향 회귀)이며, 이 결과를 받기 전에는 Task를 닫거나 PASS로 바꾸지 않는다. 별도 다음 구현 후보는 문서에 남은 서버 권위 projectile·tracer·발사/착탄 exact-key 이관 및 전체 25종·역할교대·Legacy rollback 회귀지만, 이번 문서 갱신에서 착수하지 않는다. `MigrationRequired / Unresolved / LegacyFallback` 유지.

## 19. Fox unscoped 확정 결과 표시 대기 교정 — 승인된 후속 범위

1. `UnitCombatUseCase.ExecuteAttack`의 주 타깃 피해 writer 경계에서 `attacker.Type == UnitType.FoxMagician && !presentationResultKey.IsValid`인 경우만 `immediatePrimaryPresentation`을 참으로 정하고 `ApplyDamageToVictim`의 기존 `immediatePresentation` 인자로 전달한다. 기존 이벤트·bool RPC·큐의 immediate 경로를 재사용하며, 큐가 공격자를 조회해 gameplay 타입을 재분류하는 새 분기는 만들지 않는다. Supported bridge 소유권과 유효 exact key 경로는 유지한다. 다른 유닛·타워·파도·특수 피해 경로는 변경하지 않는다. 근거: `GameSystemRules_Units.md` 규칙 19, `GameSystemRules_UnitCombatSynchronization.md` NET-PRESENT-003~004.
2. VFX 시작 marker는 로컬 표현만 담당한다. HP writer·피해 예약 2.25초·VFX marker 1.00초·쿨다운 4초와 UnitFactory의 Fox 설정 보존은 변경하지 않는다. 표시 지연을 수치 튜닝이나 로컬 VFX 종료 기반 피해 적용으로 숨기지 않는다. 근거: `GameSystemRules_Units.md` 규칙 17~18, 동기화 규칙 NET-AUTH-001~002 및 NET-PRESENT-001.
3. 후속 진단은 bounded/unscoped 큐 receive·emit과 텍스트 spawner의 실제 생성 성공 bool을 기록하는 범위다. VFX 종료는 계측하지 않으며 공격별 exact correlation도 제공하지 않는다. 따라서 새 로그로 VFX 종료→동일 공격의 숫자 표시 간격을 확정했다고 보고하지 않는다. 생성 성공은 화면상 가시성·렌더 프레임 확인과도 다르다. 정규 키를 꾸미거나 시간 근접으로 다른 공격에 결합하지 않는다. 구현·실행 여부는 담당자 결과와 새 로그로 확인한다. 근거: NET-PRESENT-001~004, NET-TIME-004.
4. 구현 담당자가 추가 중이라고 전달한 real writer fixture의 범위는 Fox/Dust/Lion × scoped/unscoped × unit/building이며 HP 1회 적용과 이벤트 immediate/key 보존을 확인한다. 이는 marker→queue→text 전체 runtime 경로를 실행한 검증이 아니다. 별도 연결 검증이 추가되지 않는 한 다음 marker 없이 실제 텍스트 단일 방출 및 후속 marker 재방출 없음은 검증 완료로 기록하지 않는다. 두 Unity 메뉴의 새 PASS와 최신 컴파일 오류 0을 확인하기 전 과거 PASS를 재사용하지 않는다. 새 TC/Task는 만들지 않는다.
5. 사용자 재테스트는 Fox와 **DustSpirit(1단계 땅정령)**을 함께 생산한다. 17_21 경기의 실제 생산은 Fox+Boulder이며 Dust 실전 통과 증거가 아니다. Fox VFX 후반부의 HP/숫자 표시와 중단·대상 사망 뒤 뒤늦은 숫자를 확인하고, Dust의 3초 주기·1.04초 marker·대상 전환은 별도 판정한다. 빌드는 요청된 경우 시작만 하며 완료 확인은 사용자 담당이다.

**[🔴 2026-09-27 correction — original kept]** §16.4의 “exact rendezvous가 marker 선도 뒤 즉시 방출한다”는 보호는 유효 key 경로에만 해당한다. Fox 진단용 exact ticket을 unscoped 실제 표시 경로의 보장으로 간주하지 않는다. 근거는 Research §19의 동일 `79c8f7eb…ca9dec` 경기와 큐 분기다. 구현 보고 및 새 실기 전에는 이 계획을 구현 완료/PASS로 올리지 않는다. projectile/tracer 구현은 에셋 준비 전 보류한다.

**통합 확인:** 현재 코드에서 위 writer-owned `immediatePrimaryPresentation` 분기는 확인했다. 이는 코드 반영 확인이지 구현 전체 검증 완료가 아니다. 구현 담당자의 최종 결과와 메인 세션의 Unity 두 메뉴 실행 결과를 받은 뒤 상태를 갱신하며, 새 실기는 계속 OPEN이다.

## 20. 2026-09-28 구현·자동 검증 완료 및 실기 인계

**2026-09-28 최신 상태 — 구현·자동 검증 PASS / 실기 OPEN:** Fox unscoped 주 타깃 피해 writer가 기존 immediatePresentation을 지정하고 이벤트·RPC·큐의 기존 즉시 표시 경로를 재사용하도록 구현했다. 피해 2.25초/VFX 시작 1.00초는 유지한다. 메인 세션 직접 실행·Editor.log 확인 인계 기준, `Run Unit Action Self Validation` 02:46:05 PASS(새 `ValidateFoxMagicianPrimaryPresentation` 포함), `Self Validate Unit Root Pose Cross Audit` 02:49:34 PASS. Runtime/Editor 정적 컴파일 오류 0, Console error 0 / warning 20. 이번 교정 빌드는 미시도이며 사용자 Fox+DustSpirit 재테스트 대기다. 진단은 인스턴스당 최대 128줄의 unscoped receive/emit·TryShowDamage 성공만 기록하고 공격자 부재는 생략한다. **VFX 종료·공격별 exact correlation은 미계측이며 marker→queue→text 전체 실동작 PASS가 아니다.**

- 구현 파일: `UnitCombatUseCase.cs`, `HitPresentationQueue.cs`, `FloatingHpTextSpawner.cs`, `RunUnitActionSelfValidation.cs`(구현 담당자 완료 인계).
- real writer fixture 12조합: Fox/Dust/Lion × scoped/unscoped × unit/building. HP 1회 적용·이벤트 immediate/key 보존 검증이며 실제 marker→queue→text 통합 실행 검증으로 확대하지 않는다.
- 이전 §19의 구현/자동 검증 대기 기록은 당시 이력으로 보존하고 이 절이 최신 상태다. 신규 실기 결과와 VFX 종료 타임스탬프는 아직 없다. DustSpirit은 이전 경기에서 생산되지 않아 실전 정상 판정 불가다.
- 증거 출처: 메뉴 시각·Console 수치는 메인 세션 직접 관측 인계, 정적 컴파일은 구현 담당자 인계다. 문서 담당자의 Editor.log 직접 읽기는 접근 거부로 재확인하지 못했다.

## 21. 2026-09-28 사용자 재테스트 결과 반영

원본은 `Assets/_Project/Docs/_Logs/2026-09-28/03_50_logcat/RuntimeLog_device.txt`와 `Assets/_Project/Docs/_Logs/_editor/2026-09-28/RuntimeLog.txt`다. Editor Host / Android Client 동일 `sharedSessionKey=be8b171a5d2d58c113c4702b8ea5d8d054bf5855322f2ad45c097796bd94d4f5`의 **두 경기**를 runId/startedAt으로 분리했다. `03_41_logcat`은 겹치는 gameplay 캡처이므로 합산하지 않았다. Host attack runId는 `0e16018819d949288f14f378c63f6d83` / `bebba109c44540b7bf81caa3a49c4958`, Client는 `fc947d78f77d4d27bb90a2bcf0462472` / `74702837e689471c960b66edb37c34ab`이다.

**Fox 표시 지연 focused 교정: 사용자 수용 / 현행 타이밍 유지.** 양 peer에서 received/emitted 61쌍씩이 모두 같은 frame, immediate=True, dispatch=text-played다. localTime 차이의 최대는 Host 7.097ms / Client 2.445ms, 평균은 1.07182ms / 1.71633ms다. 이는 큐 수신→텍스트 생성 호출 성공 구간이며 VFX 종료→동일 공격 피해 표시의 exact 측정이 아니다. VFX 실제 시작은 Host 14+65=79, Client 14+64=78, 실패 0이다. 미완료는 Host 1/Client 1이고 원인은 미확정이다. 종료 disconnect 경고를 그 원인으로 단정하지 않는다. 사용자는 약간 이른 느낌은 있지만 현재 Fox 타이밍을 명시적으로 수용했으며 재튜닝은 향후 발사체 구현 때로 미룬다. VFX 1.00초/피해 2.25초를 유지한다. 이번 표시 지연 focused 범위는 수용됐으나 완벽한 화면 일치, 권위 projectile/tracer, 전체 migration·25종·역할교대·rollback 완료로 확대하지 않는다. MigrationRequired / Unresolved / LegacyFallback 유지.

**DustSpirit: focused runtime evidence 확인 / CONDITIONAL PASS·OPEN.** 실제 생산 Dust 49기/Fox 10기이며 이번 실제 생산 타입 중 Supported는 Dust뿐이다. 서버 결과 84+166=250과 Client 수락 250이 일치하고 각 peer 필수 표현은 83+153=236/236, 결과·표현 실패/중복/공간 mismatch/recovery 0이다. Host 이동 52,714+94,799=147,513 frame에서 gate/handoff/stationaryWalk/errors 0이며 이 이동 집계는 경기 전체이지 Dust 전용 집계는 아니다. 각 경기 양 peer local ROOT PASS지만 공식 CrossAudit 실행 결과는 아니다. 초기화 지연 59건은 59건 완료, retry failure 0이다. 게임 로그 ERROR/FATAL/실제 Exception 발생 없음(스택의 Exception 매개변수 문자열은 제외). Dust 공격 접촉·대상 전환 등에 대한 명시적 사용자 육안 수용은 없어 Complete/최종 PASS로 닫지 않는다. 이전 17_21 경기의 Dust 부재 판단은 당시 사실로 보존하며 이번 두 경기에서 해소됐다.

이번 변경은 승인된 결과 문서 반영만이며 코드·TC·새 Task·빌드 실행은 하지 않았다. 다음 유닛은 별도 조사·선정 대상으로 남기며 이 문서 갱신이 구현 착수 승인은 아니다.
