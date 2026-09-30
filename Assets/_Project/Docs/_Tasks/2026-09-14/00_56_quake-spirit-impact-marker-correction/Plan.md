# QuakeSpirit 공격 타격 시점·표현 프로필 교정 계획

이번 계획은 QuakeSpirit의 기존 공격 애니메이션을 바꾸지 않고, 그 안에 이미 존재하는 실제 타격 표식 **1.667초**를 서버 타임라인과 표현 프로필의 공통 기준으로 만드는 데 목적이 있습니다. QuakeSpirit만 최소 범위로 교정하고, 정상 동작 근거가 있는 공통 C2/C3 결과·피해 경로는 손대지 않습니다.

**계획 상태:** 구현·자동 검증 및 focused Android Host/Client 로그 검증 완료 · **CONDITIONAL PASS / OPEN** · 육안 확인 항목 대기

**기존 로직 제거:** 없음. Legacy 피해 권위와 Quake 특수 공격 handler를 그대로 보존한다.

## 1. 완료 조건

1. QuakeSpirit의 런타임 `hitFrameTimes`가 `[1.667]`이다.
2. `Base Layer/Attack` clip의 `OnAttackHit`은 1.667초에 정확히 1개다.
3. Blue/Red 프리팹이 동일한 검증 대상 Controller를 유지한다.
4. QuakeSpirit profile은 `Supported`, `MeleeContact`, Impact 1개, 보조 결과 있음이다.
5. 전체 profile partition은 Supported 16 / Unresolved 8 / N/A 1이다.
6. 정상 C2/C3 writer/emitter와 Quake 피해·AoE 의미에는 변경이 없다.
7. 아래 Unity 메뉴 2개가 모두 PASS한 뒤 Android Build And Run을 시작한다.
8. 최종 실기 판정은 사용자가 새 빌드에서 수행한다.

## 2. 원자적 구현 단계

### 단계 A — 직렬화 설정을 1.667초로 교정

**대상:** `Assets/_Project/Resources/Config/UnitStatsConfig.asset`

- Inspector 직렬화 값이므로 `Assets/Editor/Setup/` 아래에 1회성 Editor 셋업 스크립트를 둔다.
- QuakeSpirit 항목이 정확히 1개인지, 기존 값이 `[1.0]`인지 먼저 fail-closed로 확인한다.
- 예상 상태가 맞을 때만 `[1.667]`로 변경하고 저장·재로드 후 값이 유지되는지 확인한다.
- 동시에 Controller/Attack 상태/clip GUID, Blue/Red Controller GUID, clip 길이·sample rate, `OnAttackHit` 1개와 1.667초를 검증한다.
- 예상과 다른 값이나 중복 이벤트가 있으면 자동 덮어쓰지 않고 실패시킨다.
- 셋업 실행과 저장 검증이 끝나면 1회성 스크립트와 그 `.meta`는 제거한다.

**규칙 근거:** `GameSystemRules_Units.md` 규칙 27, `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-001`·`NET-PRESENT-002`, WORKFLOW [5-2].

### 단계 B — QuakeSpirit profile을 Supported로 승격

**대상:** `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs`

- QuakeSpirit 전용 `Unresolved` 생성 블록을 공통 `Supported` 경로로 전환한다.
- 다음 의미는 그대로 유지한다.
  - 타겟 고정
  - `MeleeContact`
  - Impact 1개
  - 보조 결과 있음
  - Legacy 실행 종류 `TimerImpactWithSecondaryEffect`
- Supported secondary profile의 기존 표준 사유를 사용하고 새로운 우회 상태를 만들지 않는다.

**규칙 근거:** `GameSystemRules_Units.md` 규칙 18·26·43, `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-002`·`NET-PRESENT-003`.

### 단계 C — self-validation fixture와 partition 갱신

**대상:** `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

- 25종 partition 기대값을 15/9/1에서 **16/8/1**로 변경한다.
- QuakeSpirit fixture를 다음 계약으로 바꾼다.
  - `Supported`
  - `MeleeContact`
  - Impact 1개
  - 보조 결과 있음
  - secondary 지원 profile의 표준 reason
- 1.667초 timeline의 단일 marker와 런타임 config 일치를 production asset 기준으로 검증한다.
- `Unresolved` 타입의 Legacy 표현 보존, Supported 타입의 1회성 marker lease 등 기존 공통 회귀는 유지한다.

**규칙 근거:** `NET-PRESENT-001`·`NET-PRESENT-002`·`NET-PRESENT-003`.

### 단계 D — 구현 정적 검토

- Runtime/Editor C# 컴파일 오류를 확인한다.
- Quake 관련 설정과 profile 참조를 다시 검색해 과거 placeholder 사유나 15/9/1 기대가 생산·검증 경로에 남지 않았는지 확인한다.
- Quake handler, 반경 1.0, 스플래시 0.5, 피해 writer에 의도치 않은 변경이 없는지 대조한다.

### 단계 E — Unity 메뉴 검증

다음 두 메뉴를 **둘 다** 실행한다.

1. `Hexiege → Combat → Run Unit Action Self Validation`
2. `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit`

- 두 메뉴 모두 명시적 PASS 로그가 있어야 다음 단계로 진행한다.
- 한 메뉴라도 FAIL이면 빌드하지 않고 해당 실패를 진단한다.
- 이전 빌드나 이전 세션의 PASS 로그를 이번 구현의 근거로 재사용하지 않는다.

### 단계 F — Android 빌드 시작

- 두 Unity 메뉴 PASS 후 `Build And Run`을 시작한다.
- Codex는 빌드 시작까지 수행하고, 빌드 완료·설치·실기 판정은 사용자 확인으로 남긴다.
- 빌드가 시작됐다는 사실만으로 작업을 PASS/CLOSED로 기록하지 않는다.

## 3. 변경 예정 파일

### 영구 변경

- `Assets/_Project/Resources/Config/UnitStatsConfig.asset`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

### 구현 완료 뒤 상태에 맞춰 갱신할 문서

- 현재 Task의 `Research.md`, `Plan.md`, `Testcase.md`
- `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`
- 테스트 완료 및 사용자 승인 뒤 WORKFLOW [7]~[11]의 상시 상태 문서와 관련 메모리

### 일시 파일

- `Assets/Editor/Setup/` 아래 QuakeSpirit timeline 교정용 1회성 Editor 스크립트와 `.meta`
  - 실행·저장 검증 후 제거하므로 최종 변경 목록에는 남기지 않는다.

## 4. 위험과 방지책

| 위험 | 방지책 |
|---|---|
| 1.667초를 단순 추정값으로 적용 | 실제 clip event와 전용 미리보기 값이 모두 1.667초임을 확인하고, Editor 셋업이 production asset 연결을 다시 검증한다 |
| Config만 바꾸고 resolver/fixture가 과거 상태에 남음 | 단계 A~C를 한 원자 작업으로 취급하고 placeholder 사유와 partition 잔존을 검색한다 |
| Quake 승격 과정에서 다른 유닛 지원 상태가 변함 | partition 변화는 Quake 1종 이동인 +1/-1만 허용한다 |
| 보조 결과를 단일 결과로 축소 | `HasSecondaryResults=true`와 `TimerImpactWithSecondaryEffect`를 명시적 완료 조건으로 둔다 |
| 공통 C2/C3 경로를 함께 수정해 회귀 확대 | 해당 경로는 비범위로 고정하고 변경하지 않았음을 정적 대조한다 |
| Animation Event를 피해 writer로 오해 | 서버 타이머가 피해를 적용하고 marker는 표현 lease만 소비하는 기존 계약을 유지한다 |
| 자동 검증 PASS를 실기 PASS로 과대 표기 | 두 메뉴는 코드 게이트, Android 실기는 별도 사용자 판정으로 분리한다 |

## 5. 사용자 실기 확인 초점

- QuakeSpirit이 적 유닛에게 접근한 뒤 공격 전에 장시간 멈추거나 Walk 상태에 고착되지 않는가
- 땅을 내려치는 순간에 대미지 텍스트와 폭발 표현이 자연스럽게 나타나는가
- 주대상은 직접 피해를 받고, 주변 적 유닛과 적 건물은 같은 Impact에 스플래시 피해를 받는가
- 주대상에 직접 피해와 스플래시가 중복 적용되지 않는가
- 아군 유닛과 아군 건물은 피해를 받지 않는가
- 한 번만 공격하고 멈추지 않고 쿨다운 뒤 반복 공격하는가
- 타겟 사망·교체 뒤 다음 유효 타겟으로 정상 전환하는가
- 다른 기존 지원 유닛의 공격, 대미지 텍스트와 VFX가 망가지지 않았는가
- 새 통합 빌드에서 로비의 `새로고침` 문구가 일반 문자로 보이며 U+27F3 폰트 경고가 재발하지 않는가

## 6. 판정 범위

- Unity 메뉴 2개 PASS + 컴파일 성공: **코드 게이트 PASS**
- 새 Android 빌드에서 Quake focused 실기 확인: **이번 Task의 최종 PASS 판단 근거**
- 확인 전 상태: **OPEN / 실기 미검증**
- 이번 결과로 25종 전체, 역할교대, Legacy rollback 또는 ActionSequence 전체 완료를 선언하지 않는다.

## 7. 구현 및 자동 검증 결과 (2026-09-14)

계획한 QuakeSpirit 전용 교정은 구현과 Unity 자동 게이트까지 완료됐다. 다만 Android Build And Run은 시작 화면까지만 확인했으며, 빌드 완료와 실제 기기 전투 결과는 사용자 확인 전이므로 Task는 닫지 않는다.

| 항목 | 실제 결과 |
|---|---|
| 1회성 셋업 | PASS. QuakeSpirit `hitFrameTimes` 1.0→1.667 저장, `OnAttackHit=1.667`, 실제 Attack clip/Controller/Blue·Red 프리팹 연결 검증 |
| 일시 파일 | 셋업 실행과 저장 검증 후 스크립트 및 `.meta` 제거 |
| 표현 프로필 | `Supported / MeleeContact / Impact 1 / secondary true` |
| manifest | Supported 16 / Unresolved 8 / N/A 1 |
| Unit Action 메뉴 | A1/B2/C2/C3 전체 PASS, errors 0 |
| Root Pose Cross Audit 메뉴 | 정확한 경로 `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit`, PASS, errors 0 |
| Android Build And Run | 01:20경 시작 확인 뒤 최신 빌드 설치·실행 및 focused 멀티플레이 로그 확보 |
| 최종 판정 | **CONDITIONAL PASS / OPEN — 로그 검증 PASS / 화면 타격 시점·제외 대상·육안 회귀 확인 대기** |

### 계획과 달라진 점

- 초안에 적힌 두 번째 Unity 메뉴 경로에서 `Combat` 단계가 누락돼 있었다. 실제 메뉴 경로로 문서 전체를 교정했다.
- 1회성 셋업 스크립트는 계획대로 실행 후 제거돼 최종 영구 변경 파일에 남지 않는다.

### 구현 완료 파일

- `Assets/_Project/Resources/Config/UnitStatsConfig.asset`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

### 남은 검증

- QuakeSpirit의 정확한 내려치기 화면 프레임과 체감 장시간 정지 없음
- 아군·범위 밖 대상 제외의 명시적 확인
- 기존 지원 유닛 공격 표현의 육안 회귀 여부
- 새 빌드에서 일반 한글 `새로고침`이 실제 화면에 보이는지 확인(최신 실행 U+27F3 경고는 0건)

## 8. Focused 멀티플레이 검증 결과 (2026-09-14)

Editor Host와 Android Client의 동일 세션 로그에서 QuakeSpirit 10기와 총 4/25종을 확인했다. Host 공격 결과 770건은 실패 0, Client 수락 770/거부 0이고, 양쪽 필수 표현 909/909, AoE 묶음 770/770, 공간 mismatch 0이다. 주대상 200과 적 유닛·건물 스플래시 100이 같은 결과 시점에 함께 나타났으며 이동·복제·ROOT도 오류 0으로 종료했다.

이 결과는 계획의 네트워크 공격·피해·AoE·표현 전달·이동 회귀 축을 충족한다. 하지만 로그는 화면의 정확한 내려치기 순간, 아군·범위 밖 제외, 체감 장시간 정지 없음과 비교 유닛의 육안 표현을 증명하지 못하므로 Task를 닫지 않는다. 전체 25종·역할교대·Legacy rollback은 별도 통합 회귀다.
