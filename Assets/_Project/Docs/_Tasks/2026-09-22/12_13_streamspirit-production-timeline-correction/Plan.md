# StreamSpirit production 공격 타임라인 교정 계획

이번 작업은 StreamSpirit의 현재 서버 피해 시점을 실제 공격 애니메이션 marker와 맞추는 최소 교정이다. 화면에 없는 발사체를 새로 꾸며내거나 지원 완료로 승격하지 않는다. 과거 StreamSpirit VFX가 공통 gate에 막혔던 회귀까지 함께 고정한 뒤 두 자동 검증 메뉴를 실행하고, PASS와 Console 오류 0건을 확인하면 Android Build And Run을 시작한다. 빌드 완료 확인과 실제 게임 테스트는 사용자에게 넘긴다.

**문서 상태:** `단계 A~E 완료 · 사용자 실기 focused PASS / 전체 이관 범위는 미완료`

## 1. 완료 조건

1. StreamSpirit production Attack clip의 유일한 `OnAttackHit` marker가 `0.50초`임을 exact asset으로 검증한다.
2. `UnitStatsConfig`의 `unitType=17` entry가 `hitFrameTimes=[0.50]` 하나만 갖는다.
3. production controller의 Attack state가 해당 clip을 직접 사용한다.
4. Blue/Red production prefab이 해당 controller를 사용하고 `ApplyRootMotion=0`을 유지한다.
5. `UnitEffectConfig → EffectPreset_StreamSpirit_Attack → attack VFX prefab` 연결이 유효하다.
6. resolver는 `Unresolved / ProjectileImpact / projectile-timeline-unresolved`을 유지한다.
7. Unresolved StreamSpirit의 LegacyFallback marker VFX 1회 보존과 Supported scope fail-closed 계약이 회귀하지 않는다.
8. Runtime/Editor C# 정적 컴파일 오류 0건, 문서 정합성 오류 0건이다.
9. 두 Unity self-validation 메뉴가 PASS이고 Console error가 0건이다.
10. Android Build And Run은 시작까지만 수행한다. 빌드 완료와 실제 게임 판정은 사용자가 진행한다.

## 2. 단계 A — 서버 TimerImpact 시점 교정

`UnitStatsConfig.asset`에서 `unitType=17`인 StreamSpirit entry의 `hitFrameTimes`를 `0.17`에서 `0.50`으로 바꾼다. `attackCooldown=1.15`와 다른 스탯은 변경하지 않는다.

이 변경은 현재 서버가 사용하는 TimerImpact의 결과 시점만 옮긴다. Animation Event가 피해 writer가 되지 않으며, Client marker가 서버 결과를 생성하지 않는다.

**변경 파일:**

- `Assets/_Project/Resources/Config/UnitStatsConfig.asset`

**규칙 근거:** Units 규칙 17·19, `NET-AUTH-001`, `NET-PRESENT-001~003`.

## 3. 단계 B — StreamSpirit production gate 추가

Unit Action Self Validation에 StreamSpirit 전용 검증을 추가한다. 기존 QuakeSpirit·BattleAxe·InfernoSpirit production 검증의 exact asset 추적 방식을 재사용하되, StreamSpirit의 지원 상태는 다르게 판정한다.

검증 항목은 다음과 같다.

- UnitType 직렬화는 enum 배열 순서가 아니라 `intValue=17`인 entry로 찾는다.
- `StreamSpirit_Attack.anim` 길이와 유일한 `OnAttackHit @ 0.50초`를 확인한다.
- `StreamSpirit.controller`의 Attack state motion이 exact clip인지 확인한다.
- Blue/Red prefab의 Animator가 exact controller를 사용하고 `ApplyRootMotion=false`인지 확인한다.
- 양 prefab의 UnitView와 `VfxSpawnPoint` 직렬화 연결이 유효한지 확인한다.
- `UnitEffectConfig`의 StreamSpirit attack preset과 preset의 VFX prefab 연결을 확인한다.
- runtime `hitFrameTimes`가 `[0.50]`인지 확인한다.
- resolver가 `Unresolved / ProjectileImpact / projectile-timeline-unresolved`인지 확인한다.
- 기존 LegacyFallback source-marker 1회 방출, loop 재방출 차단, Supported invalid scope 차단 검증을 그대로 통과해야 한다.

공격 클립의 `Loop Time=ON`은 공통 연속 전투 구조이므로 변경하지 않는다. 공격 회차마다 CrossFade를 재시작하는 코드도 추가하지 않는다.

**변경 파일:**

- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

**규칙 근거:** Units 규칙 17·22, `NET-PRESENT-001~003`, `NET-DELIVERY-PROJECTILE`. 과거 StreamSpirit VFX 부재 사건의 회귀 계약은 `.claude/mistakes/unit-action-correction.md`를 따른다.

## 4. 단계 C — 상태 문서 갱신

구현과 자동 검증 결과를 `UnitCombatAssetMatrix.md`의 StreamSpirit 행과 이 Task 문서에 반영한다.

- marker/config 정렬과 production gate PASS는 기록한다.
- 권위 발사체 미구현과 `Unresolved / LegacyFallback` 상태는 그대로 명시한다.
- 실제 게임 테스트 전에는 Complete 또는 전체 ProjectileImpact 지원 완료로 표시하지 않는다.
- 빌드 시작 사실과 사용자 실기 대기 상태를 구분한다.

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

두 메뉴 중 하나라도 PASS가 아니거나 Console error가 있으면 Build And Run을 시작하지 않고 원인을 교정한다.

## 6. 단계 E — 빌드 시작과 사용자 실기 인계

자동 gate를 모두 통과하면 Unity에서 Android Build And Run을 **시작만** 한다. 빌드 완료를 기다리거나 완료 여부를 대신 판정하지 않는다.

사용자 실기에서는 실제 생산 경로로 StreamSpirit를 여러 기 생산해 다음을 확인한다.

- 피해 숫자와 공격 VFX가 같은 공격 동작의 marker 부근에서 보이는지.
- 첫 공격과 연속 공격에서 VFX가 빠지거나 한 회차에 두 번 나오지 않는지.
- 타겟 사망·변경 직전에도 이전 확정 회차 VFX가 새 타겟으로 옮겨 붙지 않는지.
- 이동·회전·공격 방향에 새 회귀가 없는지.
- Host/Client에서 공격 결과 수와 표현 수가 예상 계약을 벗어나지 않는지.

실기 로그 분석 전에는 이 Task를 CLOSED로 바꾸지 않는다.

## 7. 후속 Task 경계 — 서버 권위 발사체

이번 Task 이후에도 StreamSpirit는 `Unresolved / LegacyFallback`이다. 다음 별도 작업에서만 다음 내용을 설계·구현한다.

- 공격 회차에 결합된 권위 발사체 ID와 발사 시점.
- 서버 궤적 또는 도착 판정과 착탄 위치.
- 타겟 사망·despawn·회피 불가 정책과 공격자 사망 취소 규칙.
- 발사·착탄 VFX의 Host/Client exact-key 상관관계.
- 중복·지연·순서 역전·late result 억제.
- resolver의 `Supported / ProjectileImpact` 승격 조건.

이번 `0.50초` TimerImpact 정렬은 이 후속 구현의 대체물이 아니다.

## 8. 위험과 대응

| 위험 | 대응 |
|---|---|
| 실제 발사체 없이 Supported 승격 | resolver 상태를 exact 검증하고 변경 금지 |
| 과거 StreamSpirit 공격 VFX 부재 재발 | LegacyFallback source marker와 preset chain을 함께 검증 |
| 다른 유닛 스탯 행 오염 | `unitType intValue=17` exact entry만 수정 |
| 잘못된 clip을 검사해 허위 PASS | prefab controller → Attack motion → clip GUID 전체 연결 검사 |
| loop를 끄거나 Attack을 매 회차 재시작 | 기존 Loop Time과 continuous Attack state 유지 |
| 자동 PASS를 실기 완료로 오인 | Build 시작까지만 자동 진행, 실제 생산 테스트와 로그 분석 후 상태 판정 |

## 9. 변경 예정 파일 요약

### 확정 변경

- `Assets/_Project/Resources/Config/UnitStatsConfig.asset`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`
- `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`
- 이 Task의 `Research.md`, `Plan.md`

### 조사·검증 대상이지만 변경하지 않음

- `Assets/_Project/Animations/Units/StreamSpirit/StreamSpirit_Attack.anim`
- `Assets/_Project/Animations/Units/StreamSpirit/StreamSpirit.controller`
- `Assets/_Project/Prefabs/Units/Spirit/Unit_StreamSpirit_Blue.prefab`
- `Assets/_Project/Prefabs/Units/Spirit/Unit_StreamSpirit_Red.prefab`
- `Assets/_Project/Resources/Config/UnitEffectConfig.asset`
- `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_StreamSpirit_Attack.asset`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs`

Testcase.md는 사용자 지시에 따라 생성하지 않는다.

## 10. 구현 진행 상태 — 2026-09-22

| 단계 | 상태 | 결과 |
|---|---|---|
| A 서버 TimerImpact 시점 교정 | 완료 | StreamSpirit `hitFrameTimes=[0.50]` |
| B production gate 추가 | 완료 | exact clip/controller/prefab/VFX/resolver 경계 검증 추가 |
| C 상태 문서 갱신 | 완료 | Asset Matrix와 Task 문서에 구현 상태 반영 |
| D 정적·Unity 자동 검증 | 완료 | 두 Unity self-validation PASS, 최신 코드 C# 컴파일 오류 0건 |
| E Build And Run 및 사용자 실기 | 완료 | Android 빌드 후 Editor Host + Android Client 2경기 focused 로그 확보 |

## 11. 단계 E 실기 결과와 판정 — 2026-09-22

- 공통 세션 키는 `c50c68f41779b35a5a63f32479e8ad88b4886c71205ad1dba0aff69e5c0c9ad7`이다.
- 첫 경기: Host 286/286, Client 288/288 VFX가 실제 시작됐고 재생 실패·unmatched·duplicate·overflow는 모두 0이었다.
- 둘째 경기: Host 317/317, Client 315/315 VFX가 실제 시작됐고 재생 실패·unmatched·duplicate·overflow는 모두 0이었다.
- 합계 Host VFX `603/603`, Client VFX `603/603`이며 각 결과는 `particleSystems=1`, `playbackActive=True`였다.
- terminal의 incomplete 6/2 및 4/6은 network-despawn terminal에 남은 미완료 회차이며 `vfxFailures`로 분류되지 않았다. dropped 상세 로그 때문에 각 미완료 원인이 단순 진행 중, 정상 취소, 타겟 소멸 중 무엇인지는 이번 자료만으로 확정하지 않는다.
- 일반 UAS END의 `coveredUnitTypes 3/25`, `4/25`와 `dropped 686`, `817`에 따른 FAIL은 전체 roster·상세 로그 한도 판정이며 StreamSpirit 전용 terminal의 focused PASS와 분리한다.
- Host 최대 marker↔timer 차이는 첫 경기 `0.095232초`, 둘째 경기 `0.093398초`였다. Client 최대 start→marker `0.732351초`는 timing variance로 기록하지만 실제 VFX 누락으로 판정하지 않는다.
- 최신 Host/Client ROOT summary는 PASS이고 구조·이동 오류는 0건이다. 사용자의 사전 육안 확인과 전용 로그 모두 VFX 누락을 재현하지 않았다.

이 Task의 production marker/config 정렬 및 LegacyFallback VFX 보존 범위는 focused PASS다. 그러나 서버 권위 발사체, 발사/착탄 exact-key 상관관계, full 25-type role-swap/rollback은 완료 조건에 포함되지 않았고 여전히 별도 후속 Task 대상이다. 따라서 StreamSpirit의 `MigrationRequired / Unresolved` 상태와 7절의 후속 Task 경계는 유지한다.
