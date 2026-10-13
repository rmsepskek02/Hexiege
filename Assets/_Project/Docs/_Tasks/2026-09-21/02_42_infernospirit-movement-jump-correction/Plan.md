# InfernoSpirit 이동 순간이동 교정 계획

이번 작업은 서버 이동 전체를 다시 만드는 것이 아니다. 실제 게임 로그에서 서버 위치는 정상 범위였고, Walk에서 Attack으로 바뀌는 순간 애니메이션 몸 중심이 크게 움직였다. 먼저 화면에 그려지는 모델의 위치를 직접 재서 실제 점프를 확정하고, 애니메이션 전환이 원인이면 그 표현만 교정한다. 화면 모델은 안정적인데 Client 위치 전달만 끊기면 서버 권위 계약을 보존한 표현 전용 보간으로 분기한다.

**문서 상태:** `prefab 메시 X 교정 실기에서 이동 순간이동 해소 확인 · 공격 VFX SpawnPoint 상대 X 교정 승인 / OPEN`

## 1. 완료 조건

1. `(0,0,0) → 실제 위치` 초기화 표본이 gameplay jump 또는 event budget으로 집계되지 않는다.
2. InfernoSpirit의 stable presentation anchor가 Walk·Attack 양쪽에서 같은 의미로 측정된다.
3. Walk→Attack/Attack→Walk에서 Simulation Root, Visual Root, stable render anchor가 같은 frame과 allowance로 비교된다.
4. 실제 render anchor jump가 확인되면 animation/presentation child 범위의 최소 교정으로 allowance 안에 수렴한다.
5. render anchor가 정상이고 Client root gap이 확인되면 paired Host/Client 증거를 갖춘 표현 전용 보간만 적용한다.
6. 서버 locomotion, 경로·속도·repath, 피해·공격 회차, 방향값은 변경되지 않는다.
7. Client Simulation Root writer는 NetworkTransform 하나로 유지되고 `ApplyRootMotion=0`을 유지한다.
8. Root Pose Host/Client errors/drop 0, 공격 표현 failure 0 및 기존 Unit Action 계약이 회귀하지 않는다.
9. 실제 게임 생산 테스트에서 순간이동이 재현되지 않고 로그의 stable render anchor도 allowance 이내여야 PASS다. 자동 검증만으로 완료하지 않는다.

## 2. 단계 A — 실제 렌더 기준점 확정

### A-0. 최우선 실제 교정 — 금지된 Animator API 접근 완전 제거

- `UnitView.LateUpdate → ObserveInfernoMotionJumpFrame` 경로에서 `_animator.bodyPosition`을 읽지 않도록 관련 current/previous 값과 unavailable 사유를 완전히 제거한다.
- 기기에서 Unity 경고가 27,727건 발생했고 `ObserveInfernoMotionJumpFrame` 27,323건, `get_bodyPosition` 27,314건의 스택이 연결됐다. 첫 발생은 `23:45:39.510`이다.
- 이 경고·스택 폭주는 테스트 기기의 CPU·문자열·logcat I/O를 오염시켜 렉과 Client update 몰림을 크게 악화시켰을 가능성이 높다. 단독 원인으로 단정하지 않되, 오염된 표본으로 NetworkTransform을 먼저 수정하지 않는다.
- 이 제거는 검증기 정리가 아니라 실제 테스트 환경의 렉 교란을 제거하는 기능 교정이다.

**예상 파일:**

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

**규칙 근거:** LogRules 1.14 금지 사항 8은 매 프레임 로그 폭주를 금지한다. `NET-AUTH-001`, `NET-ROOT-001~003`, Units 규칙 22에 따라 Animator 진단이 이동 권위나 테스트 실행을 오염시키지 않아야 한다.

### A-1. 런타임 hierarchy 조사

- InfernoSpirit Blue/Red의 `SkinnedMeshRenderer`와 `rootBone` 구성을 조사한다.
- 대표 renderer와 `rootBone`을 초기화 시 1회 탐색해 캐시하고 `rootBone.position`을 stable rendered anchor의 주 값으로 사용한다.
- `bounds.center`는 root bone이 없거나 메시 외곽 대조가 필요한 경우의 보조값이다. 주 값이 없을 때 정상값처럼 조용히 대체하지 않는다.
- 매 frame renderer 탐색, `GetComponentsInChildren` 반복, 배열·LINQ 할당을 금지한다.

**예상 파일:**

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Prefabs/Units/Spirit/Unit_InfernoSpirit_Blue.prefab` (조사 대상, 필요 근거 없이는 수정하지 않음)
- `Assets/_Project/Prefabs/Units/Spirit/Unit_InfernoSpirit_Red.prefab` (조사 대상, 필요 근거 없이는 수정하지 않음)

**규칙 근거:** `NET-ROOT-001~003`은 Simulation Root와 표현 계층을 분리하고, Units 규칙 22는 Animator 표현을 이동 권위와 분리한다.

### A-2. stable presentation anchor 표본

- 기존 Simulation/Visual 표본에 cached root bone 기반 stable render anchor previous/current/delta와 anchor 종류·유효성 사유를 추가한다. `Animator.bodyPosition` 필드는 제거한다.
- 첫 유효 표본은 baseline으로만 저장하고 사건을 발행하지 않는다.
- renderer/root bone 교체·disable·despawn 시 baseline과 캐시를 retire한다.

**예상 파일:**

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`

**규칙 근거:** `U-MOV-PHASE`, `U-MOV-ALIGN`, `NET-AUTH-001`, `NET-ROOT-001~003`. LogRules 1.5·1.7·1.14 금지 사항 8에 따라 정상 frame은 출력하지 않고 전환 사건만 bounded 기록한다.

## 3. 단계 B — 초기화 오탐과 사건 예산 교정

- `(0,0,0) → 실제 body/anchor` 첫 표본을 initialization으로 명시하고 gameplay 분류에서 제외한다.
- 일반 이동 초과와 Walk↔Attack phase 전환 사건이 같은 8건을 선착순으로 소모하지 않도록 phase-aware budget을 적용한다.
- 최소 계약은 각 유닛의 첫 severe transition event를 보존하는 것이다.
- 전체 unit/key 상한, exact-key dedupe, overflow 요약, 생명주기 retire는 유지한다.
- self-validation에는 초기화 비집계, 전환 사건 보존, cap/overflow/retire 경계를 추가한다.

**예상 파일:**

- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

**규칙 근거:** `U-MOV-PHASE`, `U-MOV-REPATH`, Units 규칙 22, LogRules 1.5·1.7·1.14 금지 사항 8. 이 단계는 검증기 확장이 목적이 아니라 실제 결함 전환 증거를 cap 뒤에 잃지 않기 위한 선행 조건이다.

## 4. 단계 C — 분기 A: animation/presentation jump 교정

stable render anchor가 Walk↔Attack에서 allowance를 넘으면 다음 순서로 한 변수씩 교정한다.

1. Walk/Attack clip의 body/root/retarget 기준과 전환 시 첫 pose를 비교한다.
2. clip/import 설정으로 해결 가능한 불연속이면 해당 animation 축만 수정한다.
3. 원본 clip을 안전하게 바꿀 수 없고 모델 presentation child 기준점만 불연속이면 Visual Root 아래 InfernoSpirit 표현 child에서 보정한다.
4. 보정은 Simulation Root와 Visual Root에 쓰지 않고 서버 위치를 되감거나 clamp하지 않는다.
5. `ApplyRootMotion=0`을 유지하고 다른 24종 유닛에는 적용하지 않는다.

**조건부 예상 파일:**

- `Assets/_Project/Animations/Units/InfernoSpirit/InfernoSpirit_Walk.anim`
- `Assets/_Project/Animations/Units/InfernoSpirit/InfernoSpirit_Attack.anim`
- InfernoSpirit Blue/Red prefab 또는 InfernoSpirit 전용 presentation 보정 컴포넌트
- 위 파일은 stable render anchor가 실제 점프를 증명한 뒤 선택하며 한 번에 모두 수정하지 않는다.

**규칙 근거:** `U-MOV-ALIGN`은 위치 스냅과 복수 writer를 금지한다. Units 규칙 15·17·22는 공격 방향·marker·Animator 표현의 책임을 분리한다. `NET-AUTH-001`, `NET-ROOT-001~003`, `NET-FACING-001`은 animation correction이 서버 권위 Root를 쓰지 못하게 한다.

## 5. 단계 D — 분기 B: Client 표현 gap 교정

`Animator.bodyPosition` 경고를 제거한 Android 재수집에서 해당 경고가 0건이고, stable render anchor가 Root를 정상 추종하지만 Client Root만 Host보다 크게 건너뛸 때만 이 분기로 간다.

1. 같은 유닛·lifecycle·frame/time 구간의 Client receive/interpolation 증거와 exact paired Host pose를 확보한다.
2. peer 결합이 없으면 `unavailable-ambiguous`를 유지하고 `client-only-replication-gap`으로 승격하지 않는다.
3. 현행 NetworkTransform 계약 `server authority + canonical world + Interpolate=true + PositionLerpSmoothing=false + LegacyLerp`를 유지한다.
4. 전체 보간을 끄거나 Client Simulation Root writer를 추가하지 않는다.
5. 필요한 경우 Visual Root 아래 표현 전용 위치 보간 seam을 둔다. 공격 방향, 사거리, 타겟, VFX/피격 위치의 authoritative/current pose 소비자는 그 seam을 판정 원본으로 사용하지 않는다.

**조건부 예상 파일:**

- `Assets/_Project/Scripts/Presentation/Unit/VisualRootProjector.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- 필요 시 `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs`의 read-only receive evidence

**규칙 근거:** `U-MOV-ALIGN`, `U-MOV-REPATH`, `NET-AUTH-001`, `NET-ROOT-001~003`, `NET-FACING-001`. 현행 NetworkTransform 값은 `NET-ROOT-004`의 production 계약이므로 즉흥 변경하지 않는다.

## 6. 단계 E — 회귀 검증과 실제 교정 확인

### E-1. 정적·자동 게이트

1. Runtime/Editor C# 정적 컴파일 오류 0건.
2. Unit Action Self Validation에서 stable anchor 유효/무효, initialization, allowance, phase-aware budget, severe 보존, overflow, retire를 fail-closed 검증.
3. Unit Root Pose Cross Audit Self Validate PASS.
4. production InfernoSpirit의 `ApplyRootMotion=0`, NetworkTransform 계약 및 현재 orientation `-58°`가 의도치 않게 변하지 않았는지 확인.
5. Android 로그에서 `Setting and getting Body Position/Rotation ... should only be done in OnAnimatorIK or OnStateIK`, `ObserveInfernoMotionJumpFrame → get_bodyPosition` 스택이 모두 0건인지 확인.

**규칙 근거:** Units 규칙 17·22, `NET-AUTH-001`, `NET-ROOT-001~003`, `NET-FACING-001`, LogRules 1.5·1.7·1.14 금지 사항 8.

### E-2. 실제 게임 gate

- 실제 생산 경로에서 InfernoSpirit 2개 이상을 이동시켜 Walk→Attack→Walk를 반복한다.
- Host/Client 공통 세션에서 Simulation/Visual/stable render anchor 사건을 결합한다.
- 실제 전환마다 root는 allowance 이내이고 stable render anchor가 allowance를 넘지 않아야 한다.
- Client root gap 분기라면 paired Host 증거와 교정 전/후 최대·평균 delta를 역할별로 비교한다.
- Root Pose errors/drop 0, movement gate/writer/handoff/stationary-Walk failure 0, 공격 표현 failure 0을 함께 확인한다.
- 육안으로 순간이동이 사라졌다는 사용자 확인 전에는 PASS/CLOSED로 기록하지 않는다.

## 7. 방향값 보류 계약

- 현재 production asset과 validator 기대값은 `m_OrientationOffsetY=-58`이다.
- 최신 Client Impact `+28.343°`는 이전 `-45°`의 `+13.370°`보다 악화됐다.
- 그러나 이번 이동 교정에서는 `-58°`를 복원·재조정하거나 새 후보값을 적용하지 않는다.
- 이동이 안정된 뒤 별도 회전 교정에서 AttackStart와 Impact를 분리해 한 변수씩 다시 비교한다.

**규칙 근거:** Units 규칙 15·17·22와 `NET-ROOT-001~003`. 이동 표현 교정과 공격 방향 보정을 섞지 않아 원인과 결과의 상관관계를 보존한다.

## 8. 위험과 대응

| 위험 | 대응 |
|---|---|
| `Animator.bodyPosition`을 visible mesh로 오인 | stable render anchor가 실제로 allowance를 넘을 때만 animation 수정 |
| 초기화 0값을 gameplay jump로 집계 | 첫 유효 표본은 baseline 전용, budget 미소비 |
| 작은 사건이 cap을 모두 소비 | phase-aware budget 또는 첫 severe transition 보존 |
| Client snap을 서버 이동 버그로 오인 | exact paired Host/Client 증거 전에는 `unavailable-ambiguous` 유지 |
| animation 보정으로 게임 Root까지 이동 | animation/presentation child 범위만 수정, `ApplyRootMotion=0` 유지 |
| 보간 수정으로 판정 위치가 늦어짐 | 표현 seam과 authoritative/current pose 소비자를 분리 |
| 검증기만 늘고 실제 결함이 남음 | stable anchor 재현 뒤 분기 A/B의 실제 correction과 사용자 실기까지 완료 조건에 포함 |
| 진단 코드가 테스트 렉을 다시 유발 | `Animator.bodyPosition` 접근 완전 제거 + Android Unity 경고 0건을 빌드 실기 gate로 강제 |

## 9. 비범위

- 서버 path/corridor/checkpoint/repath/MoveSpeed 변경
- 전역 성능 최적화
- 피해·DoT·공격 회차·타겟 선택·타격 시점 변경
- 공격 orientation 추가 변경
- 다른 유닛 일괄 교정
- Testcase 작성 및 QA 실행

## 10. 변경 예정 파일 요약

### 첫 구현에서 확정

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

첫 구현은 `Animator.bodyPosition` 접근 제거, cached `SkinnedMeshRenderer.rootBone.position` anchor, `bounds.center` 보조 증거, initial baseline no-event, Android 경고 0건 gate까지 포함한다. NetworkTransform 변경은 이 경고가 없는 재수집에서도 gap이 남을 때만 선택한다.

### 증거 분기 뒤에만 선택

- 분기 A: InfernoSpirit Walk/Attack animation 또는 Blue/Red presentation child
- 분기 B: `VisualRootProjector.cs`, `UnitView.cs`, 필요 시 `NetworkUnit.cs`의 read-only receive evidence

### 변경하지 않음

- 서버 Authoritative Locomotion·경로·속도·repath
- NetworkTransform production 계약
- 피해·공격 회차·타겟·Impact writer
- `m_OrientationOffsetY=-58`

Testcase.md는 사용자 지시에 따라 생성하지 않는다.

## 11. 1차 구현 결과

### 11.1 구현 완료

- `UnitView` motion observer에서 `Animator.bodyPosition`과 `get_bodyPosition` 접근을 완전히 제거했다.
- InfernoSpirit의 유일한 `SkinnedMeshRenderer`와 `rootBone`을 `Initialize`에서 캐시한다. production 계약은 root bone과 Animator Hips가 같은 Transform인 것이다.
- `LateUpdate`에서는 캐시된 root bone `Transform.position`만 읽는다. 매 frame renderer 탐색이나 Animator body API 호출을 하지 않는다.
- `renderer.bounds.center`는 같은 frame의 메시 외곽 대조용 보조 증거로만 기록하며 root bone 부재를 정상값처럼 대체하지 않는다.
- 첫 valid rendered-anchor 표본은 baseline 전용이다. 사건 로그, per-unit 예산, overflow를 소비하지 않는다.
- per-unit 총 8건 중 일반 사건은 최대 7건만 허용하고 첫 Walk↔Attack severe 사건 1건을 예약한다. global key 제한 128은 유지한다.
- self-validation에 production 크기 `64 units × 8 events`, `128 keys`의 제한·예약·overflow 계약을 추가했다.

### 11.2 변경하지 않은 범위

- NetworkTransform 설정과 writer
- Visual Root 투영·보간
- 서버 이동 writer와 경로·repath
- InfernoSpirit Walk/Attack clip
- InfernoSpirit Blue/Red prefab
- 공격 orientation `-58°`

따라서 이번 결과로 animation 자체 또는 Client root 전달을 교정했다고 주장하지 않는다. 첫 단계는 경고 폭주를 만든 잘못된 관측 경로를 제거하고 다음 실기 판정을 신뢰할 수 있게 만든 것이다.

### 11.3 완료된 검증

| 검증 | 결과 |
|---|---|
| Runtime C# 정적 컴파일 | 오류 0건 |
| Editor C# 정적 컴파일 | 오류 0건 |
| `Hexiege → Combat → Run Unit Action Self Validation` | PASS |
| `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit` | PASS |
| Unity Console | error 0건 |

자동 검증 PASS는 cached renderer/rootBone/Hips 계약과 bounded limiter 계약이 성립한다는 뜻이다. Android 실기기 재테스트를 대신하지 않는다.

## 12. 다음 실행 gate

1. 변경을 포함한 새 Android 빌드를 만든다.
2. 실제 생산 경로에서 InfernoSpirit를 생산해 이동과 Walk→Attack→Walk 전환을 반복한다.
3. 육안으로 렉성 순간이동과 전환 불연속을 확인한다.
4. Android 로그에서 body position/rotation Unity 경고와 `ObserveInfernoMotionJumpFrame → get_bodyPosition` 스택이 모두 0건인지 확인한다.
5. 새 rendered-anchor 사건의 Simulation Root, Visual Root, root bone anchor, bounds 보조값을 Host/Client 역할별로 분석한다.
6. stable anchor가 전환 때 allowance를 넘으면 단계 C를, anchor는 안정적이고 paired Client root gap만 남으면 단계 D를 진행한다.

사용자의 실기 확인과 새 로그 분석 전에는 렉·순간이동 해결 완료, PASS 또는 CLOSED로 갱신하지 않는다.

## 13. 1차 실기 결과에 따른 2차 교정 순서

공통 세션 `7baaffab77f02b028c5257c17c0ab66dd2b2b9b7d1957acb3f05c9aae70ed83b`에서 기본 이동과 공격 직전 결함이 모두 재현됐다. Host 정상 프레임의 권위 Root 점프는 없었고, Client Root 최대 `0.4744`와 정지 Host의 Walk↔Attack rendered-anchor 최대 `0.1067`이 서로 다른 축으로 관측됐다. 다음 작업은 아래 순서를 바꾸지 않는다.

### 13.1 우선순위 A — 모든 Animator body API 제거

- `bodyPosition`뿐 아니라 `bodyRotation`도 `LateUpdate`, marker 소비, 공격 시작 방향 진단에서 제거한다.
- Facing evidence는 cached Hips/root bone의 `Transform.forward` 또는 허용된 bone Transform rotation 후보로 대체한다. 기존 body 방향 값과 같은 의미인지 먼저 코드·production hierarchy에서 확인하고 이름만 바꿔 대체하지 않는다.
- Android에서 `Animator:get_bodyPosition`, `Animator:get_bodyRotation`, 관련 Unity Body Position/Rotation 경고가 모두 0건이어야 다음 원인 판정으로 진행한다.

**반증 가능한 예측:** body API가 공격 구간 오염의 원인이면 새 빌드에서 해당 경고와 스택은 0건이 된다. 경고가 0건인데도 기본 이동·공격 직전 delta가 그대로 남으면 body API는 결함의 단독 원인이 아니며 우선순위 B·D 증거를 계속 조사한다.

**규칙 근거:** `GameSystemRules_Units.md` 규칙 22는 Animator 표현과 서버 이동 권위를 분리한다. `GameSystemRules_UnitCombatSynchronization.md` `NET-ROOT-001~003`은 Animation Root를 Simulation Root writer로 사용하지 못하게 한다. LogRules 1.5·1.7·1.14 금지 사항 8에 따라 진단이 테스트 실행을 오염시키지 않아야 한다.

### 13.2 우선순위 B — 오프센터 메시 원인을 한 변수씩 판별

다음 세 원인을 한 번에 수정하지 않고 순서대로 확인한다.

1. production Blue/Red prefab의 Unit Root, Visual Root, Animator, renderer, rootBone/Hips local position·rotation을 비교한다.
2. Animator/avatar와 Walk/Attack clip의 root transform, orientation, Humanoid retarget 결과를 비교한다.
3. Walk↔Attack transition 직전·직후 같은 사건에서 Root 회전량과 root-to-anchor 수평 오프셋을 기록한다.

각 사건은 `root-to-anchor horizontal offset`, `root rotation delta`, `observed anchor delta`를 함께 가져야 한다. 오프셋이 약 `0.8`이라면 회전으로 예상되는 원호 이동과 실제 anchor delta를 비교해 회전축 이탈 가설을 판정한다.

**반증 가능한 예측:** 실제 anchor delta가 `2 × offset × sin(rotationDelta/2)`에 허용 오차 안에서 반복 수렴하면 회전축에서 벗어난 메시/pose가 주원인이다. Root 회전이 거의 없는데 anchor만 움직이면 clip/retarget/transition pose displacement가 주원인이다. 두 값 모두 작으면 다른 presentation child 또는 sampling 원인을 찾는다.

- Simulation/Network Root를 이 보정에 사용하거나 되감지 않는다.
- `ApplyRootMotion`을 켜지 않는다.
- 원인이 확정되기 전 prefab, clip, orientation 값을 바꾸지 않는다.

**규칙 근거:** `U-MOV-ALIGN`, Units 규칙 15·17·22, `NET-AUTH-001`, `NET-ROOT-001~003`, `NET-FACING-001`.

### 13.3 우선순위 C — Android-safe compact motion schema

- 현재 `rendererBounds` 중간에서 잘리는 긴 한 줄을 축약한다.
- 판정 필수 필드인 classification, unit/lifecycle, phase, transition, allowance, Simulation/Visual/anchor delta를 앞쪽에 둔다.
- 원시 previous/current 벡터와 bounds 보조 정보는 짧은 별도 bounded detail line으로 분리하거나 필요한 사건에서만 출력한다.
- line 수와 유닛별·전체 cap, overflow, retire 계약은 유지한다. 단순히 로그를 여러 무제한 줄로 나누지 않는다.

**반증 가능한 예측:** 새 Android 로그의 모든 motion 사건에서 phase·transition·allowance가 절단 없이 파싱되어야 한다. 하나라도 후반 필드가 누락되면 schema 교정은 FAIL이다.

**규칙 근거:** LogRules 1.5·1.7 및 1.14 금지 사항 8의 Android-safe·bounded 진단 계약.

### 13.4 우선순위 D — 깨끗한 재실기 뒤에만 Client 복제/보간 분기

- A~C 완료 후 같은 생산·이동·Walk↔Attack 조건으로 새 Android 빌드를 재시험한다.
- 같은 유닛과 pose를 결합할 수 있는 Host/Client exact event key 또는 receive/interpolation 시각 증거를 먼저 확보한다.
- 새 실행에서도 Client Root가 `0.1`을 반복 초과하거나 Host 정상 범위를 크게 넘을 때만 NetworkTransform receive/interpolation 경로를 조사한다.
- 현행 `server authority + canonical world + Interpolate=true + PositionLerpSmoothing=false + LegacyLerp`는 증거 전 변경하지 않는다.
- Client Simulation Root writer 추가, 전체 보간 비활성화, 게임 Root clamp는 금지한다.

**반증 가능한 예측:** 경고 0·로그 무절단 상태에서 Host는 정상인데 같은 keyed 구간의 Client Root만 큰 delta를 보이면 Client receive/interpolation clumping 가설이 강화된다. Host와 Client가 함께 움직이면 네트워크 단독 원인이 아니고, Client delta가 정상화되면 현 설정을 유지한다.

**규칙 근거:** `U-MOV-ALIGN`, `U-MOV-REPATH`, `NET-AUTH-001`, `NET-ROOT-001~004`, `NET-FACING-001`.

## 14. 2차 교정 완료 gate

다음 조건을 모두 만족하기 전에는 구현 완료, PASS 또는 CLOSED로 기록하지 않는다.

1. Runtime/Editor C# 정적 컴파일 오류 0건.
2. Unit Action Self Validation과 Unit Root Pose Cross Audit PASS.
3. Android의 `get_bodyPosition`, `get_bodyRotation`, Body Position/Rotation Unity 경고 0건.
4. Android motion 필수 필드 `phase/transition/allowance` 절단 0건.
5. production Blue/Red의 root-to-anchor 오프셋 원인이 prefab hierarchy, clip/retarget pose, transition 중 하나로 증거 기반 분류됨.
6. 정지 Host Walk↔Attack에서 rendered-anchor delta가 allowance 안에 수렴하거나, 남은 delta가 의도된 animation motion임을 수치로 설명할 수 있음.
7. Client Root 비정상 delta는 exact Host/Client 결합 증거로 확정·교정되거나, 새 실행에서 정상 범위로 수렴함.
8. 서버 이동 writer, NetworkTransform 단일 writer, `ApplyRootMotion=0`, 공격 회차·피해·타겟 계약이 유지됨.
9. 사용자가 실제 생산 테스트에서 기본 이동과 공격 직전 이동이 자연스럽다고 확인함.

Testcase는 사용자 지시에 따라 작성하지 않는다. 이 문서 갱신은 다음 구현의 승인이나 구현 완료를 뜻하지 않는다.

## 15. 16_07 실기 결과에 따른 prefab X 단일 변수 교정 계획

최신 Android 실기에서는 56건 모두 렌더 anchor의 상대 이동이 약 `0.813` 떨어진 회전축의 예측 원호와 일치했다. 사용자는 이 X 편심을 의도적으로 보정한 적이 없고 누락·실수로 판단했으며, InfernoSpirit Blue/Red production prefab의 해당 메시 자식 local X만 `0`으로 교정하는 작업을 승인했다. 이번 라운드는 원인을 다시 섞지 않도록 이 값 하나만 바꾸고, 자동 검증 뒤 Android Build And Run은 시작만 한다.

### 15.1 수정 범위

다음 두 production prefab에서 로그가 추적한 `SkinnedMeshRenderer/rootBone` 계층의 해당 메시 자식 local position X만 `0`으로 변경한다.

- `Assets/_Project/Prefabs/Units/Spirit/Unit_InfernoSpirit_Blue.prefab`: `-0.8129883 → 0`
- `Assets/_Project/Prefabs/Units/Spirit/Unit_InfernoSpirit_Red.prefab`: `-0.813 → 0`

Blue와 Red는 같은 교정값을 사용한다. 한쪽만 고치거나 근사 잔여값을 남기지 않는다.

### 15.2 반드시 불변인 항목

- 해당 메시 자식의 local Y, local Z
- 해당 메시 자식과 상위 계층의 rotation 및 scale
- Animator Controller와 animation clip
- 공격 방향 보정 `m_OrientationOffsetY=-58`
- `ApplyRootMotion=0`
- NetworkTransform 설정과 server-authority 단일 writer 계약
- 서버 Authoritative Locomotion, 경로, 속도, checkpoint, repath 및 공통 이동 코드
- 공격 회차, 타겟, 피해, DoT, VFX/피격 표현 계약
- 다른 24종 유닛 prefab

**규칙 근거:** `GameSystemRules_Units.md`의 `U-MOV-ALIGN`과 규칙 7은 서버 공통 이동·회전을 유지하고 위치 스냅이나 별도 writer를 금지한다. 규칙 15·17·22는 공격 방향, 타격 표현, Animator 표현을 Simulation Root 이동 책임과 분리한다. `GameSystemRules_UnitCombatSynchronization.md`의 `NET-AUTH-001`, `NET-ROOT-001~004`, `NET-FACING-001`은 prefab 표현 교정이 서버 권위 Root와 NetworkTransform writer를 변경하지 못하게 한다.

### 15.3 영구 self-validation gate

`Run Unit Action Self Validation`의 production asset 검증에 다음 exact gate를 영구 추가한다.

1. InfernoSpirit Blue의 대상 메시 자식 local X가 정확히 `0`이어야 한다.
2. InfernoSpirit Red의 대상 메시 자식 local X가 정확히 `0`이어야 한다.
3. 두 production prefab 중 하나라도 대상 계층을 찾지 못하거나 X가 0이 아니면 fail-closed한다.
4. 기존 `m_OrientationOffsetY=-58`, `ApplyRootMotion=0`, Animator/renderer/rootBone 계약 검증은 그대로 유지한다.

이 gate는 이번 실수를 다시 prefab에 넣었을 때 자동 검증 단계에서 즉시 막기 위한 회귀 방지다. 테스트를 통과시키기 위해 런타임에서 X를 강제로 덮어쓰는 코드는 추가하지 않는다.

### 15.4 구현·검증 순서

1. Blue/Red production prefab의 대상 메시 자식 local X만 `0`으로 변경한다.
2. permanent exact gate를 Unit Action Self Validation에 추가한다.
3. Runtime/Editor C# 정적 컴파일 오류 0건을 확인한다.
4. `Hexiege → Combat → Run Unit Action Self Validation` PASS를 확인한다.
5. `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit` PASS를 확인한다.
6. Unity Console error 0건을 확인한다.
7. Android `Build And Run`을 **시작만** 한다.
8. 빌드 완료 대기·성공 확인·기기 실행·실기 테스트는 사용자가 수행한다. Codex는 빌드 완료를 기다리거나 확인하지 않는다.

### 15.5 사용자 실기 gate

- 실제 생산 경로에서 InfernoSpirit를 2개 이상 생산한다.
- 일반 이동 중 방향 전환에서 몸 전체가 원호로 크게 휘둘리거나 순간이동처럼 보이지 않는지 확인한다.
- 적 접근 후 Walk→Attack과 Attack→Walk 전환을 반복해 공격 직전 위치가 자연스러운지 확인한다.
- 모델, 선택 표시, 체력바, 공격 VFX가 서로 어긋나지 않는지 확인한다.
- 새 로그에서 `rootToAnchorOffset`, `anchorDelta`, `arcMatch`, `unavailable-ambiguous`를 다시 분석한다.
- `unavailable-ambiguous`의 Simulation Root 큰 이동이 반복되면 prefab 교정과 분리해 Client 복제/보간 분기를 재개한다.

### 15.6 완료 판정

두 Unity 자동 검증 PASS와 빌드 시작은 코드·에셋 gate 통과일 뿐 실기 완료가 아니다. 사용자가 실제 게임에서 일반 이동과 공격 진입이 자연스럽고 부가 정렬 이상이 없다고 확인하기 전에는 이 Task를 PASS 또는 CLOSED로 기록하지 않는다.

Testcase는 사용자 지시에 따라 생성하거나 수정하지 않는다.

## 16. 21_16 실기 결과에 따른 공격 VFX SpawnPoint 상대 X 교정 계획

메시 X 교정 후 사용자는 이동 순간이동이 사라졌다고 확인했고, Android 로그에서도 `presentation-anchor-jump=0`, Root Pose PASS, 공격 결과 spatial mismatch 0을 확인했다. 남은 결함은 메시만 중앙으로 이동한 뒤 `VfxSpawnPoint`가 교정 전 절대 X에 남아 생긴 prefab 내부 표현 좌표 불일치다. 이번 라운드는 Blue/Red 생성점 X 한 변수만 교정하며 이동·공격 판정 코드는 건드리지 않는다.

### 16.1 수정 범위

다음 production prefab의 `VfxSpawnPoint.localPosition.x`만 기존 메시 기준 상대 간격으로 변경한다.

- `Assets/_Project/Prefabs/Units/Spirit/Unit_InfernoSpirit_Blue.prefab`: `-0.8215834 → -0.0085951`
- `Assets/_Project/Prefabs/Units/Spirit/Unit_InfernoSpirit_Red.prefab`: `-0.82159513 → -0.00859513`

교정값은 각 진영의 교정 전 `VfxSpawnPoint X - Mesh X`로 계산했다. 생성점을 `0`으로 만들지 않고 기존 세부 배치의 상대 차이를 보존한다.

**규칙 근거:** `GameSystemRules_Units.md` 규칙 17은 Animation Event와 VFX를 표현 책임으로 제한하고 실제 공격 결과·이동 writer와 분리한다. 규칙 22와 `GameSystemRules_UnitCombatSynchronization.md`의 `NET-ROOT-001~004`, `NET-PRESENT-001~003`은 표현 자식 교정이 Simulation Root, 서버 권위 위치, 공격 결과를 다시 쓰지 못하게 한다.

### 16.2 반드시 불변인 항목

- `VfxSpawnPoint`의 local Y, local Z, rotation, scale
- Blue/Red 메시 자식의 현재 local position X=`0`, Y/Z, rotation, scale
- Animator Controller와 모든 animation clip
- 공격 방향 보정 `m_OrientationOffsetY=-58`
- `ApplyRootMotion=0`
- 서버 Authoritative Locomotion, path, corridor, checkpoint, repath 및 공통 이동 코드
- NetworkTransform 설정과 server-authority 단일 writer 계약
- 공격 회차, 타겟, 피해, DoT, Impact 결과와 presentation spatial writer
- 다른 24종 유닛 prefab

### 16.3 영구 self-validation relative offset gate

`Run Unit Action Self Validation`의 production prefab 검증에 다음 gate를 추가한다.

1. Blue/Red 각각에서 대상 메시와 `VfxSpawnPoint` 계층을 정확히 찾지 못하면 fail-closed한다.
2. 메시 local X는 기존 gate대로 `0`을 유지해야 한다.
3. `VfxSpawnPoint.localPosition.x - Mesh.localPosition.x`가 Blue는 `-0.0085951`, Red는 `-0.00859513`이어야 한다.
4. 직렬화 부동소수점 비교 허용 오차는 `0.0001` 이하로 제한한다.
5. VFX relative offset gate를 통과시키기 위한 런타임 위치 덮어쓰기나 보정 코드는 추가하지 않는다.
6. 기존 `m_OrientationOffsetY=-58`, `ApplyRootMotion=0`, Animator/renderer/rootBone, NetworkTransform 계약 검증은 유지한다.

이 gate는 메시와 VFX 생성점 중 하나만 다시 이동해 둘의 상대 위치가 깨지는 회귀를 자동 검증 단계에서 막는다.

### 16.4 구현·검증·빌드 순서

1. Blue/Red production prefab의 `VfxSpawnPoint` local X만 각각 계산된 상대값으로 변경한다.
2. Unit Action Self Validation에 production relative offset gate를 추가한다.
3. Runtime/Editor C# 정적 컴파일 오류 0건을 확인한다.
4. `Hexiege → Combat → Run Unit Action Self Validation` PASS를 확인한다.
5. `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit` PASS를 확인한다.
6. Unity Console error 0건을 확인한다.
7. Android `Build And Run`을 **시작만** 한다.
8. 빌드 완료 대기·성공 확인·기기 실행·실기 테스트는 사용자가 수행한다. Codex는 빌드 완료를 기다리거나 확인하지 않는다.

### 16.5 사용자 실기 gate와 완료 판정

- 실제 생산 경로에서 Blue/Red InfernoSpirit의 공격 VFX가 모델의 올바른 위치에서 발생하는지 확인한다.
- 서로 다른 공격 방향에서 생성점이 몸체와 함께 회전하며 옆으로 벗어나지 않는지 확인한다.
- 유닛과 건물 공격에서 같은 정렬이 유지되는지 확인한다.
- 이동→공격→이동 전환에서 해결된 순간이동이 재발하지 않는지 확인한다.
- 새 Android 로그에서 Root Pose 오류와 presentation spatial mismatch가 0인지 재확인한다.

두 Unity 메뉴 PASS와 Build And Run 시작은 정적·자동 gate 통과일 뿐 최종 완료가 아니다. 사용자가 실제 게임에서 Blue/Red VFX 정렬과 이동 비회귀를 확인하기 전에는 이 Task를 `OPEN`으로 유지한다.

Testcase는 사용자 지시에 따라 생성하거나 수정하지 않는다.

## 17. 인페르노 공격 VFX 간헐 미표시 경계 진단 계획

최신 동일 경기에서 Android Host의 InfernoSpirit `ImpactMarker`는 32회, Editor Client는 28회였다. 기존 표식은 회차 gate 뒤와 VFX 호출 전에만 기록되므로, 빠진 4회가 Animation Event 자체 누락인지 scope gate 조기 차단인지 알 수 없다. 이번 라운드는 공격 동작을 추정 수정하지 않고 한 번의 재실기로 원인 경계를 확정할 수 있는 bounded 진단만 추가한다.

### 17.1 구현 범위

- `UnitView.OnAttackHit`
  - InfernoSpirit 원시 Animation Event 진입을 기준으로 시도 1회를 만든다.
  - 유닛 비가용과 scope gate 조기 차단도 반환 전에 결과로 기록한다.
  - VFX 호출 후 EffectManager 결과와 spawn 위치를 동일 시도에 결합한다.
- `EffectManager`
  - 기존 유닛 공격 VFX 재생 동작은 유지하면서 preset, prefab, pool item, ParticleSystem, playback 활성 경계를 읽기 전용 결과로 반환한다.
- `VfxPoolItem`
  - 캐시된 ParticleSystem 개수와 `Play` 직후 활성 여부를 상태 변경 없이 노출한다.
- `UnitAttackShadowObserver`
  - `inferno-attack-vfx-attempt-v1` 스키마로 한 Animation Event당 한 줄만 기록한다.
  - 세션당 128회 상한과 단일 overflow 경고를 둔다.
  - 종료 줄에 raw event, started, gate suppressed, playback failure 합계를 Host/Client별로 남긴다.

**규칙 근거:** `GameSystemRules_Units.md` 규칙 17은 Animation Event와 VFX를 표현 책임으로 제한한다. 규칙 22 및 `GameSystemRules_UnitCombatSynchronization.md`의 `NET-AUTH-001`, `NET-PRESENT-001~003`은 진단이 서버 공격 결과, 피해 writer, Simulation Root를 변경하지 못하게 한다. Android-safe bounded 로그는 `LogRules.md`의 제한·종료 증거 계약을 따른다.

### 17.2 변경하지 않는 범위

- 공격 쿨다운, 회차 생성, 타겟 선택·변경, 피해 및 DoT
- Animator Controller, Attack clip, Animation Event 시각
- 공격 방향 보정 `m_OrientationOffsetY=-58`
- Blue/Red prefab과 `VfxSpawnPoint` 좌표
- VFX preset 수치와 `vfx_infernospirit_charge` prefab
- 이동, NetworkTransform, Root writer와 보간
- 다른 24종 유닛의 진단 출력

### 17.3 검증 및 빌드 순서

1. Runtime/Editor C# 정적 컴파일 오류 0건을 확인한다.
2. 문서 정합성 검사 0건을 확인한다.
3. `Hexiege → Combat → Run Unit Action Self Validation` PASS를 확인한다.
4. `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit` PASS를 확인한다.
5. Unity Console error 0건을 확인한다.
6. Android `Build And Run`을 시작한다.
7. 빌드 완료 대기·성공 확인·기기 실행·실기 테스트는 사용자가 수행한다. Codex는 빌드 완료를 기다리거나 확인하지 않는다.

### 17.4 다음 실기 판정

- Host/Client `rawEvents`가 다르면 로컬 Animation Event/Animator 전환 경로를 교정한다.
- `rawEvents`는 같은데 Client `gateSuppressed`가 많으면 공격 회차 lease 전달·보존·소비 경로를 교정한다.
- scope 소비는 같은데 `playbackFailures`가 있으면 해당 outcome에 따라 manager/preset/prefab/pool/ParticleSystem만 교정한다.
- `started`까지 일치하지만 육안 누락이 남으면 파티클 renderer, lifetime, 카메라 가시성 및 겹침을 다음 단일 변수로 조사한다.

이번 진단 빌드만으로 VFX 누락 해결 PASS 또는 Task CLOSED로 기록하지 않는다. Testcase는 사용자 지시에 따라 생성하지 않는다.

## 18. Stop 선행 시 커밋 source marker 보존 교정 계획

04_11 실기에서 Host/Client raw event는 70/70으로 같았지만 Client가 9회를 `gate-suppressed`로 차단했다. 이번 구현은 Stop을 지연하거나 Legacy marker를 무조건 허용하지 않는다. 서버가 이미 커밋한 현재 공격의 발사 VFX/SFX만 정확한 개수로 보존하고, 타겟 의존 표현과 미래·반복 marker는 계속 차단한다.

### 18.1 순수 source-marker 예약

`UnitAttackPresentationPolicy.cs`에 presentation revision과 남은 marker 수를 소유하는 작은 값 타입을 추가한다.

- 유효한 commit revision과 `HitFrameTimes.Length`로만 arm한다.
- 정상 상태에서 marker를 소비하면 `Full` 방출을 반환한다.
- 일반 Stop 뒤에는 `SourceOnly`로 전환하고 남은 marker를 최대 지정 개수만 소비한다.
- explicit suppression, attacker death/despawn, invalid/new revision 경계에서는 close한다.
- 소비 완료 뒤 Attack clip이 계속 루프해도 추가 marker는 거부한다.
- `Scoped`는 기존 exact scope 소비까지 성공해야 `Full`이고, `LegacyFallback`도 새 source 예약 소비 없이는 더 이상 무제한 방출하지 않는다.

**규칙 근거:** Units 규칙 17·19, `NET-AUTH-002`, `NET-ACTION-IDEMPOTENT`, `NET-PRESENT-001~003`, `NET-CANCEL-001~005`.

### 18.2 UnitView 표현 경계 분리

`UnitView`는 marker 판정을 `Suppressed / Full / SourceOnly` 세 결과로 처리한다.

- `Full`: 기존 공격 VFX/SFX와 exact target 기반 tracer/local-hit을 유지한다.
- `SourceOnly`: 공격자 위치 VFX/SFX만 재생하고 즉시 반환한다. 타겟 조회, marker/result 결합, tracer, `OnLocalAttackHit`은 실행하지 않는다.
- `Suppressed`: 현재처럼 아무 표현도 방출하지 않는다.
- `StopCombatAnimation`은 target·rotation 소유권을 종료하되 source 예약만 terminal 상태로 보존한다.
- `SuppressPendingAttackImpactPresentation`, 사망/despawn, 새 commit/새 revision은 예약을 강제로 닫는다.

이 분리로 사망한 타겟을 새 타겟으로 잘못 이전하지 않으면서, 이미 시작된 마지막 공격의 발사 표현만 Host/Client에 동일하게 남긴다.

### 18.3 영구 회귀 gate

`RunUnitActionSelfValidation`에 production adapter와 같은 순서 조합을 추가한다.

1. Legacy `commit → Stop → marker`: source-only 정확히 1회, 두 번째 marker 0회.
2. Legacy `commit → marker → Stop`: full 정확히 1회, Stop 뒤 marker 0회.
3. Scoped `commit → Stop → marker`: source-only 1회, exact target 표현 0회.
4. explicit suppression 또는 attacker cancellation 뒤 marker: 0회.
5. 새 revision은 이전 예약을 폐기하고 새 marker 수로 다시 arm한다.
6. 다중 hit는 남은 marker 수만 보존하며 초과 marker를 거부한다.
7. 기존 Unresolved Legacy 보존과 Supported fail-closed 계약을 동시에 유지한다.

### 18.4 진단과 실기 판정

- Inferno VFX terminal은 source-only 보존 횟수를 별도 집계해 일반 started와 gate suppression을 구분한다.
- 새 동일 경기에서 Host/Client `rawEvents`, `started`, `sourceOnlyPreserved`, `gateSuppressed`, `playbackFailures`를 비교한다.
- `started`는 source-only 재생을 포함한 전체 VFX 시작 수이고 `sourceOnlyPreserved`는 그 부분집합이다. 정상 목표는 양쪽 raw event가 같을 때 `started`도 같고 playback failure가 0이며, Client에서 기존 gate suppression에 대응하는 source-only 보존이 나타나는 것이다.
- Stop 뒤 허가 없는 반복 marker가 source-only로 추가 재생되면 FAIL이다.

### 18.5 검증·빌드 순서

1. Runtime/Editor C# 정적 컴파일 오류 0건.
2. 문서 정합성 검사 0건.
3. `Hexiege → Combat → Run Unit Action Self Validation` PASS.
4. `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit` PASS.
5. Unity Console error 0건.
6. Android `Build And Run`은 시작만 한다.
7. 빌드 완료 확인과 실제 생산 테스트는 사용자가 수행한다.

이번 자동 검증과 빌드 시작만으로 Task를 완료 처리하지 않는다. 새 실기에서 InfernoSpirit 누락이 사라지고 반복·오표현이 없음을 확인한 뒤, 다른 LegacyFallback 타입은 각 타입 작업 시 같은 terminal 지표로 순차 확인한다. Testcase는 사용자 지시에 따라 생성하지 않는다.

## 19. 구현 및 정적 검증 상태 — 2026-09-22

18절 계획을 구현했다.

- `UnitAttackPresentationPolicy`에 revision·잔여 marker 수를 소유하는 bounded source-marker lease와 `Suppressed / Full / SourceOnly` 판정을 추가했다.
- `UnitView`는 commit 시 source 예약을 열고, 일반 Stop에서는 exact target scope만 폐기한 뒤 남은 source marker를 `SourceOnly`로 보존한다.
- `SourceOnly`는 공격자 위치 VFX/SFX만 재생하며 target 조회, tracer, local-hit, damage 결과에는 관여하지 않는다.
- 명시적 impact suppression, 공격자 사망/despawn, 새 revision은 예약을 닫는다. Legacy도 예약 수를 초과한 반복 marker를 허용하지 않는다.
- Inferno VFX terminal 스키마를 `inferno-attack-vfx-attempt-v2`로 올리고 `sourceOnlyPreserved` 집계를 추가했다.
- self-validation에 Stop/marker 양쪽 도착 순서, Scoped/Legacy, 취소, 새 revision, 다중 hit, 잘못된 입력 회귀를 추가했다.

정적 결과는 Runtime C# 오류 0건, Editor C# 오류 0건이다. `Tools/check_docs.py`도 전체 검사에서 문제 0건으로 통과했다.

현재 Unity 프로세스는 존재하지만 모든 `MainWindowHandle`이 0이고 컴퓨터 사용 연결의 앱 목록도 비어 있어, 두 Unity 메뉴 self-validation과 Console 확인 및 Build And Run 시작은 실행하지 못했다. 강제 종료는 미저장 작업 손실 가능성이 있으므로 자동 수행하지 않았다. 따라서 상태는 `구현·정적 검증 완료 / Unity 메뉴 검증·빌드 시작 대기 / 실기 전 OPEN`으로 유지한다.

## 20. 최종 실기 판정 — 2026-09-22 11_44

사용자 재실기와 동일 경기 Host/Client 로그에서 목표 결함이 해소됐다.

- Android Client: `rawEvents=64`, `started=64`, `sourceOnlyPreserved=11`, `gateSuppressed=0`, `playbackFailures=0`.
- Editor Host: `rawEvents=65`, `started=64`, `sourceOnlyPreserved=0`, `gateSuppressed=1`, `playbackFailures=0`.
- Host의 차단 1회는 정렬 오차 `98.363°`인 커밋 전 provisional marker이므로 정상 차단이다. 실제 확정 VFX 시작 수와 유닛별 분포는 양쪽 64회로 일치한다.
- Client에서 Stop 선행 11회를 source-only로 보존했고 target/tracer/local-hit을 위조하지 않았다. 초과 marker, playback failure, overflow는 0이다.
- 사용자는 결과가 정상으로 보인다고 확인했다.

따라서 이동 순간이동, InfernoSpirit 프리팹 X 누락, VFX 위치, Stop 선행 VFX 누락을 다룬 이 Task는 `PASS/CLOSED`로 종료한다. 성능 결함은 사용자 지시에 따라 별도 후속이고, InfernoSpirit의 `LegacyFallback` 이관 및 전체 25종·역할교대·Legacy rollback 통합 회귀는 별도 P0로 유지한다. Testcase는 사용자 지시에 따라 생성하지 않았다.
