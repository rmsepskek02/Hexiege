# 어설트 공격 진입 멈춤과 C3 종료 수명 교정 — 계획

이번 교정은 서버가 사거리 진입에서 위치를 멈추는 규칙을 유지하면서, 화면에서만 불필요한 정지 Walk 자세를 제거한다. 공격하지 않은 유닛의 정상 종료도 C3 실패로 세지 않게 고친다. Walk 에셋, 서버 피해 계산, 공격 사거리와 타겟 선택, NetworkTransform은 변경하지 않는다.

## 1. 근거 규칙

| 교정 | 근거 |
|---|---|
| 사거리 진입 시 같은 틱에 이동 중단 | `U-MOV-PHASE` 9, `U-ATK-ALIGN` |
| 공격 handoff에서 Attack 선행 표현 유지 | `GameSystemRules_Units.md` 규칙 7의 전투 표현 단락, `NET-PRESENT-001` |
| 커밋 전 타격 표현·피해 차단 | `U-ATK-TIMELINE`, `NET-PRESENT-001` |
| 서버 단일 위치·피해 writer 유지 | `NET-MOVE-001`, `NET-ACTION-AUTHORITY` |
| 공격 수명이 없는 종료는 정상 부재 | `NET-CANCEL-004`, `NET-PRESENT-003`의 instance 수명 계약 |
| 비교 실패를 허용치 상향으로 숨기지 않음 | `NET-PRESENT-003`, 기존 C3 전환 계획 |

## 2. 이동에서 공격으로의 표현 handoff

1. `UnitMovementPresentationPolicy`가 `TargetAcquirePriority`를 입력으로 받아 일반 stationary 상태와 구분한다.
2. 위치가 전진하지 않는 일반 `NoIntent`·`AlignToMove`는 기존처럼 Held Walk를 사용한다.
3. `TargetAcquirePriority`는 Held를 사용하지 않고 이동 표현 소유권을 Action에 넘긴다.
4. `UnitView`는 같은 프레임에 서버 위치 정지, Action 회전 소유권 획득, provisional Attack 시작 순서를 유지한다.
5. provisional Attack은 커밋에서 restart하지 않고 승인 전 marker만 억제한다.

## 3. C3 종료 수명

1. Coordinator의 `Retire(None)`은 기존 Shadow scheduler와 동일한 정상 no-op으로 처리한다.
2. 유효 인스턴스만 retire tombstone 용량을 소비한다.
3. 유효 형식의 충돌, 결과 누락, 만료, 용량 초과는 기존처럼 실패한다.
4. 142개 중 111개만 공격한 형태의 회귀를 추가해 유효 retire 111, 무공격 종료 실패 0을 보장한다.

## 4. 자동 회귀

- 일반 stationary 결정은 Held=true
- 전진 결정과 후보 위치 커밋은 Held=false
- `TargetAcquirePriority + NoIntent`는 Held=false
- provisional Attack은 영향 표식을 차단하면서 Attack 표현을 시작
- `Retire(None)` 전후 FailureCount와 RetiredCount 무변경
- 유효 retire, 중복 retire, capacity fail-closed 보존
- 기존 A1~C3 전체 self-validation 보존

## 5. 중단 조건

- C2 피해·HP writer가 변경되면 중단한다.
- 사거리나 타겟 선택 결과가 달라지면 중단한다.
- 일반 재경로/차단에서 stationary Walk가 다시 진행되면 중단한다.
- C3 실제 레거시 비교 실패를 severity나 허용 오차 변경으로 숨기지 않는다.

## 6. 검증과 빌드

1. 전용 C3 Coordinator self-validation 실행
2. 전체 Unit Action self-validation 실행
3. 둘 중 하나라도 FAIL이면 구현을 다시 교정
4. 모두 PASS일 때만 Android Build And Run 시작
5. 빌드 완료와 실기 결과 확인은 사용자가 수행

## 7. Round 2 — C3 미지원 타입의 Legacy 표현 보존

1. C3 공격 프로필 지원 상태를 표현 marker gate의 명시적 입력으로 만든다.
2. `Supported` 타입은 현재처럼 유효한 정규 scope의 1회 소비가 성공해야만 VFX·SFX·Tracer를 방출한다.
3. `Unresolved` 타입은 정규 scope를 발행하거나 소비한 것처럼 꾸미지 않고, 기존 Legacy VFX·SFX·Tracer 경로를 그대로 사용한다.
4. default/0 scope에 `impactEnabled=true`를 실어 보내는 모순 상태를 제거한다.
5. 지원 상태는 서버가 정한 match-fixed 프로필/파이프라인 계약에서 결정하고 Client가 임의 추측하지 않는다.
6. C2 피해·HP writer, 공격 타겟·사거리·쿨다운, B3 Root writer는 변경하지 않는다.

### 회귀 gate

- StreamSpirit 형태의 `Unresolved + default scope + Animation marker`는 Legacy 공격 VFX를 정확히 한 번 방출한다.
- 같은 표본은 C3 supported 비교 분모·invalid·correlation failure에 들어가지 않고 manifest의 `Unresolved` 증거로만 남는다.
- `Supported + valid scope`는 기존처럼 정확한 HitIndex 수만 방출한다.
- `Supported + default/소모/오래된 scope`는 VFX·SFX·Tracer를 방출하지 않는다.
- 지원 상태 전환이 Animator restart, 서버 피해 시각 또는 Root 회전을 변경하지 않는다.
- Unity 전용/전체 self-validation PASS 뒤에만 Android Build And Run을 시작한다.
