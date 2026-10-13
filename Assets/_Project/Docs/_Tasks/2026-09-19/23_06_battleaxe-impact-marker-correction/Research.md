# BattleAxe 공격 타격 시점 교정 조사

BattleAxe는 공격 기능과 휩쓸기 범위 피해가 이미 구현되어 있었지만, 조사 당시 실제 도끼가 타격하는 애니메이션 표식은 **1.02초**이고 서버가 읽는 런타임 설정은 **1.1667초**로 서로 달랐습니다. 이번 작업은 새 공격 방식이나 피해 공식을 만드는 것이 아니라, 실제 production Attack 클립의 단일 표식에 서버 타임라인을 맞추고 이후 같은 불일치가 재발하면 자동 검증에서 즉시 차단되도록 만드는 교정입니다. 현재 런타임 설정은 **1.02초로 교정 완료**됐습니다.

**문서 상태:** **CONDITIONAL PASS / OPEN** · 구현과 Unity 자동 게이트 완료 · 실기 로그 회귀 신호 없음 · 성능 저하로 정확한 육안 확인 제한

## 1. 조사 결론

- BattleAxe는 `UnitType.BattleAxe = 5`다.
- 조사 당시 production `UnitStatsConfig`의 BattleAxe 항목은 공격 쿨다운 3.05초, 공격 사거리 0.75, `hitFrameTimes=[1.1667]`이었다. 현재는 `[1.02]`다.
- `BattleAxe.controller`의 `Base Layer/Attack` 상태는 GUID `c97327687cff891418b039f23b5b214b`인 `BattleAxe_Attack.anim`을 사용한다.
- 실제 Attack 클립은 30fps, 길이 3.1666667초이며 `OnAttackHit`이 **1.02초에 정확히 1개** 있다.
- Blue/Red BattleAxe 프리팹은 모두 GUID `4c3402b2e6bfbf843965a0fe1b11bbd4`인 같은 Controller를 사용한다.
- 따라서 조사 당시 production 에셋이 가리키는 실제 타격 시점 1.02초와 런타임 설정 1.1667초가 불일치 지점이었다. 현재 두 값은 1.02초로 일치한다.
- BattleAxe의 C3 profile은 이미 `Supported / MeleeContact / Impact 1 / secondary true`다. 이번 교정은 profile partition이나 지원 상태를 변경하지 않는다.
- `SpecialAttackRegistry`는 BattleAxe를 `SweepAttackBehavior`에 연결한다. 주대상 직접 피해 뒤 월드 XZ 전방 부채꼴 안의 적 유닛에게 같은 피해를 적용하는 현재 의미와 수치는 보존한다.

## 2. 확인된 production 증거

| 대상 | 확인된 사실 |
|---|---|
| `Assets/_Project/Scripts/Domain/Unit/UnitType.cs` | BattleAxe는 UnitType 5 |
| `Assets/_Project/Resources/Config/UnitStatsConfig.asset` | 조사 당시 cooldown 3.05, range 0.75, `hitFrameTimes=[1.1667]`; 2026-09-20 현재 `[1.02]` |
| `Assets/_Project/Animations/Units/BattleAxe/BattleAxe.controller` | `Base Layer/Attack`이 clip GUID `c97327687cff891418b039f23b5b214b` 참조 |
| `Assets/_Project/Animations/Units/BattleAxe/BattleAxe_Attack.anim.meta` | 위 GUID가 실제 BattleAxe Attack 클립과 일치 |
| `Assets/_Project/Animations/Units/BattleAxe/BattleAxe_Attack.anim` | 30fps, 길이 3.1666667초, `OnAttackHit` 1개 @ 1.02초 |
| Blue/Red BattleAxe 프리팹 | 양쪽 모두 Controller GUID `4c3402b2e6bfbf843965a0fe1b11bbd4` 사용 |
| `UnitAttackShadowProfileResolver.cs` | `Supported / MeleeContact / Impact 1 / secondary true` |
| `SpecialAttackRegistry.cs` | `BattleAxe → SweepAttackBehavior` 연결 |
| `SpecialAttackConfig.asset` | sweep reach 0.75, arc half-angle 120도 |

`Assets/_Project/Docs/_Tasks/2026-07-20/14_49_unit-combat-rules-correction/Research.md`는 실제 marker 1.02초와 당시 설정 1.1667초의 불일치, 그리고 과거 1.1667초 완료 기록과의 충돌을 기록한다. `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`는 이번 교정으로 marker/config 1.02초 일치 상태로 갱신됐다. 과거 완료 기록은 당시 이력으로 보존하되 현재 production 사실의 근거로 사용하지 않는다.

## 3. 원인 가설과 판정

| 가설 | 판정 | 근거 |
|---|---|---|
| 런타임 `hitFrameTimes`가 과거 1.1667초 값에 남아 실제 marker 1.02초와 어긋났다 | **채택·교정 완료** | 조사 당시 config만 1.1667초였고 실제 Attack state clip의 단일 marker는 1.02초였다. 현재 config도 1.02초다 |
| Blue/Red 프리팹이 서로 다른 Controller 또는 Attack clip을 사용한다 | 배제 | 양쪽 프리팹이 같은 Controller GUID를 사용한다 |
| BattleAxe profile 분류가 잘못돼 공격이 미지원 상태다 | 배제 | 이미 Supported/MeleeContact/Impact1/secondary다 |
| 휩쓸기 특수 공격 구현이 없어 보조 결과가 누락된다 | 배제 | Registry와 `SweepAttackBehavior` 연결이 존재한다 |
| 공통 C2/C3 또는 피해 writer를 수정해야 한다 | 이번 범위에서 배제 | 확인된 불일치는 BattleAxe production marker와 config 사이에 한정된다 |

## 4. 적용 규칙

- `GameSystemRules_Units.md` **규칙 18. 서버 데미지 타이밍 정밀화**: 실제 피해는 Animator가 아니라 서버 타이머로 적용한다.
- 같은 문서 **규칙 23. 특수 공격의 전략 핸들러 구조**: BattleAxe 전용 handler와 공통 피해 수렴점을 유지한다.
- 같은 문서 **규칙 24. 휩쓸기형 AoE 판정 = 월드 좌표 전방 부채꼴**: 현재 전방 부채꼴 대상 판정을 보존한다.
- 같은 문서 **규칙 25. 특수 공격 튜닝 파라미터 (SpecialAttackConfig)**: sweep reach와 반각을 변경하지 않는다.
- 같은 문서 **규칙 26. AoE 피격 연출 동시 방출**: 직접 피해와 보조 결과의 완결 묶음 의미를 유지한다.
- 같은 문서 **규칙 27. 특수 유닛 Attack 클립 OnAttackHit 이벤트 주입**: 실제 Attack marker와 런타임 타격 시점이 일치해야 한다. 문서의 과거 1.1667초 완료 기록보다 현재 production clip 1.02초를 우선한다.
- `GameSystemRules_UnitCombatSynchronization.md` **NET-PRESENT-001**: Animation Event는 표현·에셋 검증용이며 피해 writer가 아니다.
- 같은 문서 **NET-PRESENT-002**: 검증된 AttackTimeline과 완성 유닛의 Animation Event는 허용 오차 안에서 일치해야 한다.
- 같은 문서 **NET-PRESENT-003**: 직접·범위 결과는 정규 결과 키와 완결 묶음으로 같은 표현 시각에 결합한다.

## 5. 작업 범위

### 포함

- BattleAxe production `hitFrameTimes`를 `[1.1667]`에서 `[1.02]`로 교정
- production 프리팹 → Controller → `Base Layer/Attack` → Attack clip → 단일 marker 연결을 fail-closed로 검증하는 1회성 Unity 설정 절차
- `RunUnitActionSelfValidation`에 BattleAxe production timeline의 영구 회귀 조건 추가
- Unity 자동 검증 메뉴 2개 PASS 확인 후 Android Build And Run 진행

### 비범위

- `UnitAttackShadowProfileResolver`의 BattleAxe 분류 또는 전체 profile partition 변경
- `SweepAttackBehavior`, `SpecialAttackRegistry`, `SpecialAttackConfig`의 판정·수치 변경
- 직접 피해, 부채꼴 대상 수집, 방어력 계산, HP writer 변경
- 공통 C2/C3 Coordinator, 결과 전송, 표현 emitter 변경
- BattleAxe Attack 클립·Controller·Blue/Red 프리팹 직접 편집
- 다른 유닛 timeline의 동시 교정
- Testcase 작성과 개별 유닛 육안 PASS 선언

## 6. 검증 경계

- 자동 게이트는 production 연결, marker 개수·시각, config 일치와 기존 전투 회귀를 확인한다.
- 자동 게이트 PASS와 빌드 성공은 화면에서 도끼가 닿는 정확한 순간, 직접·범위 피해의 시각적 동시성, 장시간 멈춤 부재를 증명하지 않는다.
- 사용자는 여러 유닛 교정이 끝난 통합 빌드에서 육안 검증을 진행했다. 큰 문제는 없어 보였지만 렉으로 정밀 항목을 확정하지 못해 현재 판정은 **CONDITIONAL PASS / OPEN**이다.
- 구현과 자동 검증은 완료됐다. 최신 실기 로그와 사용자 관찰을 반영한 최종 범위 판정은 아래 7절을 따른다.

## 7. 구현·자동 검증·실기 로그 결과 (2026-09-20)

BattleAxe의 서버 타격 시점을 실제 도끼 타격 marker인 1.02초에 맞추고, production 연결이 다시 어긋나면 Unity 자동 검증에서 즉시 실패하도록 고정했다. 최신 Editor Host + Android Client 경기에서는 BattleAxe 공격과 공통 결과·표현·이동 동기화 실패가 발견되지 않았고 사용자의 육안 확인에서도 큰 문제는 보이지 않았다. 다만 게임 렉 때문에 정확한 접촉 순간과 Sweep 제외 조건은 독립적으로 확정하지 못했으므로 Task를 완전 PASS로 닫지 않는다.

### 7.1 구현 결과

- `UnitStatsConfig.asset`의 BattleAxe `hitFrameTimes`를 `[1.1667]`에서 `[1.02]`로 Unity 직렬화 교정했다.
- 1회성 `CorrectBattleAxeImpactMarker.cs`와 `.meta`는 production 연결·marker·저장값 PASS 확인 후 삭제했다.
- 영구 `RunUnitActionSelfValidation.cs`에 BattleAxe의 양 진영 프리팹 → Controller → `Base Layer/Attack` → 정확한 Attack clip → 단일 `OnAttackHit` 1.02초 → config `[1.02]` 연결을 함께 검사하는 fail-closed 회귀를 추가했다.
- `Supported / MeleeContact / Impact 1 / secondary true`, `SweepAttackBehavior`, reach 0.75, 반각 120도와 공통 C2/C3·피해 writer는 변경하지 않았다.
- `Hexiege → Combat → Run Unit Action Self Validation`과 `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit`은 모두 PASS했다.

### 7.2 최신 동일 세션 로그 근거

**sharedSessionKey:** `010f6e1b76dde6fe70c38c660107db611595e93fbbbe9fff63f2b0ffbf64c9bb`

| 축 | 결과 |
|---|---|
| BattleAxe 생성·관측 | Host production ID 14기, Client 재생성 14기. root sample Host/Client 각 13, stable coverage Host 6 / Client 7 |
| 명시 공격 진입 | unit 2: `AwaitingStationarySample → Ready(yaw 0) → Accepted → legacy schedule`; unit 36: `AwaitingStationarySample → Misaligned(yaw 12.078) → Ready(yaw 0) → Accepted → legacy schedule` |
| C2 결과 | Host `serverResults=420`, `resultFailures=0` ↔ Client `accepted=420`, `rejected=0` |
| Coordinator | schedules 433, results/ready 491/491, pending/failures/duplicates 0, retired 53, bundles/released 420/420, AoE manifest 완결, presentation writes 440 |
| C3 표현 | 양쪽 ready 491, expected/emitted 440/440, viewUnavailable/duplicate/transport/presentation failure 0 |
| 공간 | Host 378 samples, mismatch 0, max 0.087; Client 374 samples, mismatch 0, max 0.121; 허용 1.5 |
| 이동·ROOT | Host 47,070 frames에서 rejected/invalid/gate/writer selection/ownership conflict/handoff/stationary Walk/failure detail/drop 0. BattleAxe AStarPath/Chase/None/PendingRepath/PostCombatResume 관측. ROOT 양쪽 PASS, error/drop 0 |
| 로그 심각도 | Client ERROR/FATAL/예외 0. WARN 181은 UnitView 초기화 지연 100 + SpawnUnitClientRpc 재시도 80 + 정상 종료 disconnect 1이며 최종 viewUnavailable 0. Host ERROR/FATAL 0, 정상 종료 disconnect WARN 1 |

### 7.3 판정과 남은 경계

- **자동 검증:** PASS.
- **BattleAxe 공격·동기화 로그:** 회귀 신호 없음.
- **사용자 육안:** 작업 완료 유닛들에 큰 문제는 없어 보였음.
- **전체 판정:** **CONDITIONAL PASS / OPEN (performance-limited visual confirmation)**.
- 렉 때문에 도끼가 닿는 정확한 1.02초 순간과 Sweep의 아군·후방·범위 밖 제외는 독립 확정하지 못했다.
- 렉은 사용자 지시에 따라 이번 Task와 분리한 후속 성능 결함이다. 여기서는 원인을 추정하거나 성능 수정을 시작하지 않는다.
- 최신 실행은 4/25종 범위이므로 전체 roster, Host/Client 역할교대, Legacy rollback 완료로 일반화하지 않는다.
- **Testcase 미작성:** 사용자가 별도로 지시하지 않았으므로 생성하지 않았다.
