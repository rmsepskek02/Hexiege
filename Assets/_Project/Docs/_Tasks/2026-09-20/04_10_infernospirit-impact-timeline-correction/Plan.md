# InfernoSpirit 공격 타임라인 최소 교정 계획

이번 계획은 InfernoSpirit의 기존 공격 동작과 직접 피해·단일 대상 DoT를 보존하면서, 서버 타격 시점을 production Attack clip의 유일한 표식인 **0.50초**에 맞추는 작업입니다. 전체 투사체 시스템을 새로 만들거나 InfernoSpirit의 정규 프로필 지원 상태를 바꾸지 않고, 현재 배선의 불일치만 원자적으로 교정합니다.

**계획 상태:** 구현 및 자동 검증 PASS · Android Build And Run 시작 · 사용자 실기 대기 / OPEN

**기존 로직 제거:** 없음.

## 1. 완료 조건

1. InfernoSpirit production `hitFrameTimes`가 정확히 `[0.50]`이다.
2. 양 진영 프리팹이 검증된 같은 Controller를 사용한다.
3. `Base Layer/Attack`은 검증된 30fps·3.0초 clip을 사용하며 `OnAttackHit`이 0.50초에 정확히 1개다.
4. InfernoSpirit 공격 VFX는 유지되고 tracer 미설정 상태를 숨기지 않는다.
5. 직접 피해 뒤 적 유닛 주 타깃 1명에만 DoT 50/초×3초를 부여하는 기존 의미와 배선을 유지한다.
6. Resolver는 `Unresolved / ProjectileImpact / Impact 1 / secondary true`를 유지한다.
7. 영구 self-validation이 위 production 연결과 config 일치를 직접 검사한다.
8. 기존 두 Unity 자동 검증 메뉴가 PASS한다.

## 2. 구현 단계

### A. 1회성 Unity 직렬화 교정

**임시 파일:** `Assets/Editor/Setup/CorrectInfernoSpiritImpactTimeline.cs`

- 변경 전 production 연결과 `[1.15]`를 정확히 확인한다.
- 양 진영 프리팹, Controller의 정확한 `Base Layer/Attack`, clip GUID, 30fps·3초·단일 0.50초 marker를 검증한다.
- tracer preset이 비어 있고 Inferno DoT 50/3이 production asset과 Game scene 배선에 존재하는지 확인한다.
- 모든 선행 조건이 맞을 때만 Unity `SerializedObject`로 `hitFrameTimes=[0.50]`을 저장한다.
- 저장·재로드 후 다시 확인하고 PASS 뒤 임시 파일과 `.meta`를 제거한다.

**규칙 근거:** Units 규칙 17~20·41~42, `NET-PRESENT-002/003`.

### B. 영구 production gate

**수정 파일:** `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

- InfernoSpirit 전용 검증을 기존 QuakeSpirit/BattleAxe production gate와 같은 fail-closed 방식으로 추가한다.
- 프리팹→Controller→정확한 Attack clip→단일 marker→config를 한 검증에서 읽는다.
- tracer 미설정과 production Inferno DoT 50/3 직렬화를 명시적으로 확인한다.
- Resolver의 `Unresolved / ProjectileImpact / Impact 1 / secondary true`와 기존 manifest partition을 유지한다.

**규칙 근거:** Units 규칙 17~20·41~42, `NET-PRESENT-002/003`, `NET-CANCEL-003`.

### C. 보존 영역 확인

- `InfernoAttackBehavior`, `SpecialAttackRegistry`, `UnitCombatUseCase`, `GameBootstrapper.Setup`, `SpecialAttackConfig.asset`은 변경하지 않는다.
- 직접 피해, DoT 대상·수치·틱, 피해 writer, 공통 C2/C3, 다른 유닛 profile은 변경하지 않는다.
- 전체 projectile/facing/performance 작업으로 확장하지 않는다.

### D. 자동 검증

1. `Hexiege → Combat → Run Unit Action Self Validation`
2. `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit`

두 메뉴 모두 새 실행 PASS가 확인되어야 구현 완료로 보고한다. 이후 사용자의 추가 승인 범위에 따라 Android Build And Run을 시작하되, 빌드 완료 확인과 실기 판정은 사용자 소유로 남긴다.

## 3. 예상 변경 파일

### 영구

- `Assets/_Project/Resources/Config/UnitStatsConfig.asset`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

### 일시 생성 후 제거

- `Assets/Editor/Setup/CorrectInfernoSpiritImpactTimeline.cs`
- 생성되는 `.meta`

### 변경하지 않음

- InfernoSpirit Controller, Attack clip, Blue/Red 프리팹
- `UnitAttackShadowProfileResolver.cs`
- `InfernoAttackBehavior.cs`, `SpecialAttackRegistry.cs`, `UnitCombatUseCase.cs`
- `SpecialAttackConfig.asset`, `GameBootstrapper.Setup.cs`

## 4. 검증 gate

- config 행은 UnitType 12로 정확히 하나이며 `[0.50]` 한 값만 가진다.
- 양 프리팹은 Controller GUID `a683311d0333a3a4b8ec6aca335e1e2f`를 정확히 한 Animator에서 사용한다.
- Attack state는 clip GUID `aa08d04a7746a8c47a25e9ad6f9b7993`, speed 1이다.
- clip은 30fps, 3.0초, loop이며 `OnAttackHit` 1개 @ 0.50초다.
- UnitEffectConfig의 InfernoSpirit attack preset은 존재하고 tracer preset은 비어 있다.
- SpecialAttackConfig production asset은 Inferno DoT 50/3을 명시적으로 직렬화하며 Game scene이 그 에셋을 참조한다.
- Resolver는 계속 Unresolved ProjectileImpact이고 전체 manifest partition은 변경되지 않는다.
- 위 조건 중 하나라도 다르면 self-validation은 즉시 실패한다.

## 5. 비범위

- InfernoSpirit `Supported` 승격
- 서버 발사체·실제 tracer·Launch/Impact 분리 구현
- periodic 결과의 정규 AttackResult 전환
- 공통 원거리·facing·성능 교정
- Testcase 및 QA 문서

## 6. 실제 구현 결과

### A. production 설정 교정

- InfernoSpirit(`UnitType 12`) `hitFrameTimes`를 `[1.15]`에서 `[0.50]`으로 저장했다.
- 첫 임시 자동 교정 스크립트는 `SerializedProperty.enumValueIndex`를 사용해 명시값 공백이 있는 `UnitType`을 선언 순서로 해석했고, 잘못 선택한 BoulderSpirit(`UnitType 14`)을 `0.50`으로 변경했다.
- 수정 스크립트는 `intValue`로 행을 선택하고 InfernoSpirit=`0.50`, BoulderSpirit=`1.15`를 원자적으로 검증·복구했다. 선행 조건이나 두 저장값 중 하나라도 다르면 다음 단계로 진행하지 않도록 했다.

### B. 영구 gate 교정

- `RunUnitActionSelfValidation.cs`에 추가된 InfernoSpirit production gate의 `UnitEffectConfig` 행 선택에도 같은 `enumValueIndex` 문제가 남아 있었다.
- 이 오류는 실제 InfernoSpirit 행이 아닌 다른 행을 검사해 `InfernoSpirit must retain the verified production attack preset.` 실패를 만들었다.
- 비교를 `intValue`로 교정하고, `enumValueIndex`는 선언 순서이며 `intValue`가 실제 직렬화 enum 값이라는 상세 주석을 추가했다.
- 임시 교정기와 영구 gate가 모두 같은 실제 `UnitType` 값을 선택하는지 대조했다.

### C. 변경하지 않은 영역

- InfernoSpirit Controller, Attack clip, Blue/Red 프리팹은 변경하지 않았다.
- 직접 피해, 단일 대상 DoT 50/초×3초, 피해 writer와 공통 C2/C3 경로는 변경하지 않았다.
- Resolver는 `Unresolved / ProjectileImpact / Impact 1 / secondary true`를 유지한다.
- Testcase는 사용자 명시 지시가 없으므로 작성하지 않았다.

## 7. 완료된 검증과 현재 판정

```text
[INFERNO-TIMELINE] PASS: InfernoSpirit=0.50, BoulderSpirit=1.15 production 저장값 재검증
[UAS-DIAG] self-validation PASS
[UAS-DIAG][PRODUCTION-TIMELINE] PASS: QuakeSpirit, BattleAxe, InfernoSpirit
[INFERNO-AUTO][UNIT-ACTION] PASS
[UAS-ROOT-CROSS-AUDIT] self-validation PASS
[INFERNO-AUTO][ROOT-POSE] PASS
[INFERNO-AUTO][FINAL] PASS ... requesting Android Build And Run
```

- Android Build And Run은 URP preprocess/build pipeline에 진입했다.
- 빌드는 **시작됨**이며 완료 여부를 확인하지 않았다. 사용자 지침에 따라 빌드 완료와 실기 확인은 사용자 소유다.
- 임시 교정 스크립트는 빌드 호출 반환 뒤 삭제하도록 설계되어 있어 빌드 진행 중 존재할 수 있다. 삭제 완료로 과대 기록하지 않는다.
- 현재 판정은 `자동 검증 PASS · 빌드 시작 · 사용자 실기 대기 / OPEN`이다. 자동 검증 PASS를 Android 실기 PASS로 일반화하지 않는다.
