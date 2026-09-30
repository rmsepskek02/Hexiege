# BattleAxe 공격 타격 시점 교정 계획

이번 계획은 BattleAxe의 기존 공격 애니메이션과 휩쓸기 공격을 그대로 유지하면서, 서버가 사용하는 타격 시점을 실제 Attack marker인 **1.02초**에 맞추는 최소 교정입니다. production 에셋 연결을 먼저 엄격하게 확인한 뒤 직렬화 설정을 Unity를 통해 저장하고, 같은 불일치가 다시 생기면 영구 self-validation이 즉시 실패하도록 고정합니다.

**계획 상태:** **구현·자동 검증 완료 / 실기 CONDITIONAL PASS / OPEN** · 성능 저하로 정확한 육안 확인 제한

**기존 로직 제거:** 없음. BattleAxe profile, `SweepAttackBehavior`, 공통 C2/C3, 피해 writer와 Legacy rollback 경계를 유지한다.

## 1. 완료 조건

1. BattleAxe production `UnitStatsConfig.hitFrameTimes`가 정확히 `[1.02]`다.
2. `Base Layer/Attack`은 검증된 `BattleAxe_Attack.anim`을 사용한다.
3. Attack 클립은 30fps, 길이 3.1666667초이며 `OnAttackHit`이 1.02초에 정확히 1개다.
4. Blue/Red 프리팹이 모두 검증된 같은 BattleAxe Controller를 사용한다.
5. profile은 기존 `Supported / MeleeContact / Impact 1 / secondary true`를 유지하고 전체 partition은 변하지 않는다.
6. 직접 피해와 월드 전방 부채꼴 적 유닛 sweep 의미 및 현재 수치를 변경하지 않는다.
7. 영구 Unit Action self-validation이 위 production 연결과 marker/config 일치를 직접 검사하고 불일치 시 fail-closed한다.
8. Unity 자동 검증 메뉴 2개가 모두 PASS한 뒤 Android Build And Run을 진행한다.
9. 개별 BattleAxe 육안 판정은 여러 유닛 교정 후 통합 확인한다. **[2026-09-20 결과: 큰 문제는 없어 보였으나 렉으로 정밀 확인이 제한돼 CONDITIONAL PASS / OPEN]**

## 2. 원자적 구현 계획

### 단계 A — 1회성 Unity 설정으로 production 연결과 선행 상태 검증

**임시 대상:** `Assets/Editor/Setup/CorrectBattleAxeImpactMarker.cs`와 생성되는 `.meta`

1. production `UnitStatsConfig`에서 BattleAxe(UnitType 5) 행이 정확히 1개인지 확인한다.
2. 변경 전 `hitFrameTimes`가 정확히 `[1.1667]`인지 확인한다. 이미 다른 값이면 자동 덮어쓰지 않고 실패한다.
3. `BattleAxe.controller`의 `Base Layer`와 `Attack` 상태가 각각 정확히 하나인지 확인한다.
4. `Attack` 상태가 GUID `c97327687cff891418b039f23b5b214b`인 `BattleAxe_Attack.anim`을 속도 1로 사용하는지 확인한다.
5. Attack 클립이 30fps, 길이 3.1666667초이고 `OnAttackHit`이 1.02초에 정확히 1개인지 확인한다.
6. Blue/Red 프리팹이 GUID `4c3402b2e6bfbf843965a0fe1b11bbd4`인 Controller를 각각 정확히 한 Animator에서 사용하는지 확인한다.
7. 모든 선행 조건을 통과한 경우에만 Unity 직렬화 API로 `hitFrameTimes`를 `[1.02]`로 저장한다. YAML을 직접 편집하지 않는다.
8. 저장·재로드 후 값이 `[1.02]`로 유지되는지 다시 확인한다.
9. 성공 확인 후 1회성 스크립트와 `.meta`를 제거한다.

**규칙 근거:** `GameSystemRules_Units.md` 규칙 18·27, `NET-PRESENT-001`·`NET-PRESENT-002`, `WORKFLOW.md` [5-2].

### 단계 B — BattleAxe production timeline 영구 회귀 추가

**영구 대상:** `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

- QuakeSpirit production timeline 검증과 같은 fail-closed 원칙으로 BattleAxe 전용 production 검증을 추가한다.
- 아래 항목을 한 검증에서 함께 읽는다.
  - BattleAxe config 행 1개와 `[1.02]`
  - 정확한 Controller와 `Base Layer/Attack` 상태
  - 정확한 Attack clip
  - 30fps, 길이 3.1666667초, 단일 marker 1.02초
  - Blue/Red 프리팹의 동일 Controller 연결
- profile이 계속 `Supported / MeleeContact / Impact 1 / secondary true`인지 확인한다.
- manifest partition 숫자는 변경하지 않는다.
- 테스트 fixture만 맞추고 production 에셋 연결을 읽지 않는 우회 검증은 허용하지 않는다.

**규칙 근거:** `GameSystemRules_Units.md` 규칙 18·27, `NET-PRESENT-001`·`NET-PRESENT-002`.

### 단계 C — 보존 영역 정적 확인

- `UnitAttackShadowProfileResolver.cs`가 BattleAxe를 기존 Supported/MeleeContact/Impact1/secondary로 유지하는지 확인한다.
- `SpecialAttackRegistry.cs`의 `BattleAxe → SweepAttackBehavior` 연결을 확인한다.
- `SweepAttackBehavior.cs`와 `SpecialAttackConfig.asset`의 직접 피해 후 적 유닛 전방 부채꼴 sweep, reach 0.75, 반각 120도가 변경되지 않았는지 확인한다.
- 공통 C2/C3, 피해 writer, 다른 유닛 profile에 변경이 없는지 확인한다.
- Runtime/Editor C# 정적 컴파일 오류가 0건인지 확인한다.

**규칙 근거:** `GameSystemRules_Units.md` 규칙 23·24·25·26, `NET-PRESENT-003`.

### 단계 D — Unity 자동 검증

다음 두 메뉴를 순서대로 실행한다.

1. `Hexiege → Combat → Run Unit Action Self Validation`
2. `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit`

- 두 메뉴 모두 현재 구현에서 새로 생성된 명시적 PASS 로그가 있어야 한다.
- 어느 하나라도 FAIL이면 Android 빌드를 시작하지 않고 해당 실패를 교정한다.
- 과거 실행의 PASS 로그를 이번 구현 근거로 재사용하지 않는다.

### 단계 E — Android 빌드

- 두 Unity 자동 게이트 PASS 뒤 Android Build And Run을 시작한다.
- 빌드 시작·완료는 자동/배포 단계의 결과일 뿐 육안 PASS가 아니다.
- 사용자 방침에 따라 Codex가 빌드 완료를 추가 확인하거나 개별 육안 검증을 요구하지 않는다.

### 단계 F — 통합 육안 검증 대기

> **[2026-09-20 결과]** 사용자가 작업 완료 유닛들을 통합 확인했고 BattleAxe를 포함해 육안상 큰 문제는 보이지 않았다. 그러나 게임 렉 때문에 정확한 1.02초 접촉과 Sweep의 아군·후방·범위 밖 제외를 확정하지 못했다. 따라서 이 단계는 **CONDITIONAL PASS / OPEN**이다. 렉은 별도 성능 결함으로 이관하며 이 Task에서는 원인 분석·수정을 시작하지 않는다.

- BattleAxe 단독 Testcase는 작성하지 않는다.
- 여러 유닛 교정이 완료된 통합 빌드에서 사용자가 다음 항목을 한 번에 확인한다.
  - 도끼가 닿는 화면 순간과 직접·범위 피해 표현의 상대 시점
  - 주대상 중복 피해 없음
  - 아군·등 뒤·반경 밖 대상 제외
  - 반복 공격과 타겟 전환 시 장시간 멈춤 없음
- 그 전까지 자동 게이트 결과는 자동 PASS로만 기록하고 Task 전체 또는 육안 항목을 PASS/CLOSED로 부르지 않는다.

## 3. 예상 변경 파일

### 영구 변경

- `Assets/_Project/Resources/Config/UnitStatsConfig.asset`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

### 일시 생성 후 제거

- `Assets/Editor/Setup/CorrectBattleAxeImpactMarker.cs`
- `Assets/Editor/Setup/CorrectBattleAxeImpactMarker.cs.meta`

### 이번 계획에서 변경하지 않는 파일

- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs`
- `Assets/_Project/Scripts/Application/Combat/SpecialAttackRegistry.cs`
- `Assets/_Project/Scripts/Application/Combat/SweepAttackBehavior.cs`
- `Assets/_Project/Resources/Config/SpecialAttackConfig.asset`
- BattleAxe Controller, Attack clip, Blue/Red 프리팹
- 공통 C2/C3 및 피해 writer 관련 파일

## 4. 위험과 방지책

| 위험 | 방지책 |
|---|---|
| 과거 문서의 1.1667초 완료 기록을 production 사실로 재사용 | 실제 `Base Layer/Attack` clip의 단일 marker 1.02초를 기준으로 하고 과거 기록은 이력으로만 취급한다 |
| config만 바꾸고 프리팹·Controller·clip 연결이 달라짐 | 1회성 설정과 영구 검증이 네 연결을 한 번에 확인한다 |
| 잘못된 선행 상태를 자동 덮어써 새 변경을 숨김 | BattleAxe 행 1개와 변경 전 `[1.1667]`을 정확히 요구하고 다르면 fail-closed한다 |
| YAML 직접 수정으로 Unity 직렬화 상태 손상 | Unity 직렬화 API로만 변경·저장·재로드한다 |
| profile partition을 불필요하게 변경 | BattleAxe는 이미 Supported이므로 resolver와 partition을 비범위로 고정한다 |
| marker 교정 중 sweep 판정·수치 회귀 | Registry, behavior, reach 0.75, 반각 120도를 보존 영역으로 대조한다 |
| 자동 PASS를 육안 완료로 과대 표기 | 통합 사용자 확인 뒤에도 렉으로 정밀 항목이 제한됐으므로 `CONDITIONAL PASS / OPEN`을 유지한다 |

## 5. 문서 경계

- `Research.md`와 `Plan.md`에 실제 구현·자동 검증·실기 로그 결과를 반영한다.
- `Testcase.md`는 사용자가 별도로 지시하지 않았으므로 작성하지 않는다.
- 영구 상태 문서와 관련 메모리는 사용자가 승인한 이번 문서 갱신 단계에서 동기화한다.

## 6. 구현 결과 (2026-09-20)

계획한 최소 교정을 그대로 적용했다. production marker는 편집하지 않고 서버 timeline config만 1.02초로 맞췄으며, 1회성 도구는 검증·저장 PASS 뒤 제거했다. 영구 self-validation은 production 프리팹·Controller·정확한 Attack state clip·marker·config를 한 번에 읽는 fail-closed gate로 남겼다.

| 계획 항목 | 실제 결과 |
|---|---|
| 단계 A | BattleAxe config `[1.1667] → [1.02]` Unity 직렬화 저장·재로드 PASS, 임시 스크립트와 `.meta` 삭제 |
| 단계 B | `RunUnitActionSelfValidation.cs`에 BattleAxe production timeline 영구 회귀 추가 |
| 단계 C | profile·Sweep handler·reach 0.75·반각 120도·공통 C2/C3·피해 writer 보존 |
| 단계 D | Unit Action self-validation PASS, Unit Root Pose Cross Audit PASS |
| 단계 E | 사용자가 빌드와 실기 테스트를 완료하고 로그를 저장함 |
| 단계 F | 육안상 큰 문제 없음. 렉으로 정확한 1.02초 접촉과 Sweep 제외 조건은 미확정 → CONDITIONAL PASS / OPEN |

### 최신 실기 로그 판정

- 동일 세션 `010f6e1b76dde6fe70c38c660107db611595e93fbbbe9fff63f2b0ffbf64c9bb`에서 BattleAxe Host/Client 각 14기를 확인했다.
- Host 결과 420/실패 0과 Client 420 수락/거부 0이 일치했고, 양쪽 필수 표현 440/440, AoE bundle 420/420, 공간 mismatch 0이었다.
- Host 이동 47,070 frame에서 gate·writer·handoff·stationary Walk·drop 실패 0, ROOT 양쪽 PASS였다.
- Client WARN 181은 회복된 UnitView 초기화 지연·재시도와 정상 종료 disconnect이며 최종 viewUnavailable 0이다. BattleAxe 공격 실패로 분류하지 않는다.
- 최신 범위는 4/25종이다. 전체 roster·역할교대·Legacy rollback 완료로 확대하지 않는다.

### 변경 파일

**영구 구현 변경**

- `Assets/_Project/Resources/Config/UnitStatsConfig.asset`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

**실행 후 제거**

- `Assets/Editor/Setup/CorrectBattleAxeImpactMarker.cs`
- `Assets/Editor/Setup/CorrectBattleAxeImpactMarker.cs.meta`

**문서·메모리 동기화**

- 현재 Task의 `Research.md`, `Plan.md`
- `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`
- `Assets/_Project/Docs/PROJECT_STATUS.md`, `ROADMAP.md`, `WORK_HISTORY.md`
- 관련 game-programmer·qa-tester·공용 memory

**Testcase:** 사용자 미지시로 생성하지 않음.
