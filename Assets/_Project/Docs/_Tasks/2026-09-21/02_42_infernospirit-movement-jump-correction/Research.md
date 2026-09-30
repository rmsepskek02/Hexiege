# InfernoSpirit 이동 순간이동 교정 조사

InfernoSpirit가 이동하거나 공격으로 전환할 때 화면에서 순간이동하듯 튀는 현상이 실제 기기에서 확인됐다. 서버가 유닛 위치를 크게 이동시킨 증거는 없고, 가장 큰 사건은 이동 위치가 그대로인 상태에서 애니메이션이 Walk에서 Attack으로 바뀌는 순간 Humanoid 몸 중심만 크게 변한 경우다. 다만 Humanoid 몸 중심은 화면 메시 자체가 아니므로, 먼저 플레이어가 실제로 보는 모델의 안정적인 기준점을 측정한 다음 애니메이션 결함과 네트워크 표시 결함을 나누어 교정한다.

**문서 상태:** `prefab 메시 X 교정 실기에서 이동 순간이동 해소 확인 · 공격 VFX SpawnPoint 잔여 교정 대기 / OPEN`

## 1. 사용자 증상과 조사 목표

- 사용자 관찰: InfernoSpirit가 렉에 걸린 것처럼 간헐적으로 순간이동해 공격 방향도 정확히 보기 어렵다.
- 이번 목표: 순간이동이 실제 렌더 메시의 Walk↔Attack 전환 불연속인지, Client 위치 복제/보간 간격인지 같은 사건에서 분리하고 확인된 축만 최소 수정한다.
- 이번 비목표: 전역 성능 최적화, 서버 이동 알고리즘 재작성, 공격 방향 각도 재조정.

## 2. 실측 근거

| 항목 | 값 |
|---|---|
| 공통 세션 | `d32663e1caed7fd16b38a4166bef66647bb50e6a28344db48b3706067028d9b1` |
| Client 로그 | `Assets/_Project/Docs/_Logs/2026-09-20/23_50_logcat/RuntimeLog_device.txt` |
| Host 로그 | `Assets/_Project/Docs/_Logs/_editor/2026-09-20/RuntimeLog.txt` |
| Root Pose | Host/Client `summary-END` PASS, errors/drop 0 |
| 공격 표현 | Client `expectedVisual=417`, `presentationEmits=417`, failures 0 |

### 2.1 Client motion events

- 총 72건: `animator-body-jump` 61, `unavailable-ambiguous` 11.
- 9개 유닛이 각각 8건 제한에 도달했고 overflow도 9건이다.
- `longFrame > 0.1s`는 0건이다.
- raw frame seconds는 평균 `0.0341`, 최대 `0.0602`다.
- body delta는 평균 `0.1442`, 최대 `0.4923`이다.
- Simulation Root와 Visual Root 최대 delta는 각각 `0.2023`이다.

Client의 `0.2023`은 Host 최대보다 크므로 작은 화면 snap 가능성이 있다. 그러나 현행 observer는 같은 사건의 Host/Client 표본을 자동으로 결합하지 못한다. 따라서 현재 11건은 `unavailable-ambiguous`이며 `client-only-replication-gap`으로 확정하지 않는다.

### 2.2 Host motion events

- 총 72건이 모두 `animator-body-jump`로 분류됐다.
- 이 가운데 9건은 각 유닛의 최초 `Animator.bodyPosition=(0,0,0) → 실제 위치` 초기화 표본이다. gameplay 점프로 세지 않는다.
- 초기화 9건을 제외한 63건의 body delta는 평균 `0.1501`, 최대 `4.7021`이다.
- Simulation Root 최대 delta는 `0.0309`이고 authoritative root jump는 기록되지 않았다.
- 핵심 유효 사건: unit 86, `23:47:29.784`, `phase=NoIntent`, Animator `81563449 → 1130333774` 전환. Simulation/Visual delta는 모두 `0`, body delta는 `4.7021`이다.
- 이어진 Attack→Walk 전환에서 body delta `0.2688`이 기록됐다.

이 사건은 서버 위치·Visual Root가 멈춰 있는데 애니메이션 평가 결과의 몸 중심만 바뀌었다. 애니메이션/retarget 불연속을 우선 조사할 충분한 근거지만, visible mesh jump의 최종 증거는 아니다.

### 2.3 motion-jump 진단이 유발한 Unity 경고 폭주

같은 device 로그를 Unity 경고와 스택으로 직접 집계했다.

| 항목 | 직접 집계 |
|---|---:|
| Unity 경고 `Setting and getting Body Position/Rotation ... should only be done in OnAnimatorIK or OnStateIK` | **27,727건** |
| `ObserveInfernoMotionJumpFrame` 스택 | **27,323건** |
| `get_bodyPosition` 스택 | **27,314건** |
| 최초 발생 | `23:45:39.510` |

`UnitView.LateUpdate()`는 development/editor 빌드에서 매 프레임 `ObserveInfernoMotionJumpFrame()`을 호출하고, 이 메서드는 Humanoid일 때 `_animator.bodyPosition`을 읽었다. 경고 직후 스택이 `Animator:get_bodyPosition → UnitView:ObserveInfernoMotionJumpFrame`을 가리키므로 경고 폭주의 호출 원인은 확정됐다.

경고·스택을 매 프레임 Android logcat으로 출력하면 CPU·문자열·로그 I/O 비용이 생기고 게임 update가 몰리는 현상을 크게 악화시킬 수 있다. 따라서 사용자가 본 렉과 Client Simulation/Visual `0.2023`에도 강한 교란 요인이다. 그러나 다른 성능 원인이 함께 있을 수 있으므로 경고 폭주를 전체 렉의 단독 원인으로 확정하지 않는다.

이 사실 때문에 현재 Client gap 표본은 오염된 실행의 결과다. `Animator.bodyPosition` 접근을 제거하고 해당 경고가 0건인 새 실행에서도 gap이 남기 전에는 NetworkTransform 또는 Client 표현 보간을 수정하지 않는다.

## 3. 현재 원인 분리

### 3.1 배제하거나 수정하지 않을 축

- Host Simulation Root 최대 delta `0.0309`이므로 최신 세션에는 서버 권위 위치 점프 증거가 없다.
- 이동 rejected/invalid/gate/writer/handoff, spatial/repath, replication revision, Root projection 계수는 실패 0이다.
- 따라서 서버 Authoritative Locomotion, 경로, 속도, repath를 이번 교정의 시작점으로 삼지 않는다.
- `ApplyRootMotion`은 0을 유지한다. Root Motion을 켜서 애니메이션 이동으로 덮지 않는다.

### 3.2 아직 확정하지 못한 축

1. `Animator.bodyPosition` 4.7021 변화가 실제 visible mesh 전체의 점프인지.
2. 특정 hips/root bone 또는 Humanoid body 계산만 변하고 메시 외곽은 안정적인지.
3. Client Simulation/Visual `0.2023`이 실제 receive 간격인지, 일반 frame sampling 차이인지.
4. 둘이 동시에 존재해 사용자가 하나의 큰 순간이동처럼 보는지.

## 4. stable presentation anchor 조사

다음 구현은 금지된 `Animator.bodyPosition` 접근을 완전히 제거하고 화면 메시와 직접 연결된 안정적인 위치 기준으로 대체한다.

- 주 기준: 초기화 시 1회 탐색해 캐시한 대표 `SkinnedMeshRenderer.rootBone.position`.
- 보조 기준: root bone 미가용 또는 실제 메시 외곽 대조가 필요할 때의 `SkinnedMeshRenderer.bounds.center`. 주 기준을 대체하는 추정값으로 조용히 사용하지 않는다.
- 선택 기준: Walk와 Attack에서 모두 존재하고, LOD/VFX/무기 renderer에 흔들리지 않으며, 팀 관점 반전 뒤 같은 의미를 유지하고, 매 프레임 renderer 탐색·배열 생성 같은 할당을 만들지 않는다.
- 첫 유효 프레임을 baseline으로만 저장해 `(0,0,0) → 실제 위치` 초기화 사건을 제거한다.
- Simulation Root, Visual Root, stable render anchor를 같은 frame에 비교한다.

재현 실패 조건은 다음과 같다.

> Walk→Attack 또는 Attack→Walk 전환에서 Simulation Root와 Visual Root는 이동 allowance 이내인데 stable render anchor가 allowance를 초과하면 실제 presentation jump다.

## 5. 교정 분기

### 분기 A — 실제 render anchor jump 확인

- InfernoSpirit Walk/Attack clip의 body/root/retarget 불연속을 조사한다.
- correction은 animation 또는 Visual Root 아래 presentation child 범위에 한정한다.
- 게임 Simulation Root를 되감거나 스냅·clamp하지 않는다.
- `ApplyRootMotion=0`과 서버 단일 writer를 유지한다.
- 한 번에 clip/import/presentation offset을 모두 바꾸지 않고 원인을 재현하는 한 변수만 수정한다.

### 분기 B — render anchor 정상, Client root gap 확인

- 먼저 Client receive/interpolation 시각과 exact paired Host pose 증거를 확보한다.
- 현행 NetworkTransform 계약인 server authority, canonical world, `Interpolate=true`, `PositionLerpSmoothing=false`, `LegacyLerp`를 보존한다.
- 전체 NetworkTransform 보간을 끄거나 Client에 Simulation Root writer를 추가하지 않는다.
- 필요하면 Visual Root 아래 표현 전용 위치 보간 seam을 설계하되, 공격/VFX/피격 위치 소비자가 authoritative/current pose를 계속 사용하도록 분리한다.

## 6. 로그 제한의 결함과 보완 방향

현재 유닛당 8건 제한은 초기화·작은 body 사건으로 먼저 소진될 수 있다. 이번 9개 유닛 모두 8건과 overflow에 도달했으므로 후반 Walk↔Attack 사건이 누락될 수 있다.

- 초기화 표본은 event budget을 소비하지 않는다.
- phase 전환 사건을 일반 이동 사건과 분리하는 phase-aware budget을 사용하거나, 유닛별 첫 severe event를 반드시 보존한다.
- 정상 프레임은 계속 출력하지 않는다.
- 이 보완은 검증기 자체를 늘리는 목적이 아니라 실제 결함 교정에 필요한 전환 증거를 잃지 않기 위한 선행 seam이다.

## 7. 방향 문제와의 경계

현재 production `InfernoSpirit_Attack.anim`과 self-validation 기대값은 `m_OrientationOffsetY=-58`이다. 최신 Client Impact는 `n=49`, 평균 `+28.343°`, 범위 `+24.778° ~ +30.883°`로 이전 `-45°` 평균 `+13.370°`보다 악화됐다. AttackStart 평균도 `+19.333° → +25.199°`로 악화됐다.

`-58°`는 최종값이 아니지만 이번 이동 교정에서 추가 변경하지 않는다. 움직임이 안정된 뒤 별도 회전 교정 Task에서 원인과 값을 다시 정한다.

## 8. 관련 규칙

- `GameSystemRules_Units.md` `U-MOV-PHASE`: Walk/Move와 전투 진입의 의미 전이를 구분한다.
- 같은 문서 `U-MOV-ALIGN`: 서버 단일 trajectory와 Client 비권위 원칙을 유지하고 위치 스냅을 금지한다.
- 같은 문서 `U-MOV-REPATH`: 이동 상태와 Walk 표현을 서버 상태에서 파생하며 반복 command로 이동 목표를 재설정하지 않는다.
- 같은 문서 규칙 15: 공격 중 타겟 방향 유지. 이번 이동 교정에서 각도값을 섞지 않는 근거다.
- 같은 문서 규칙 17: Animation Event는 타격 표현 시점이며 이동 writer가 아니다.
- 같은 문서 규칙 22: 애니메이션 상태는 서버 값 기반으로 동기화하되 피해·조준·이동 책임과 분리한다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-AUTH-001`: 서버가 위치·방향의 유일한 권위자다.
- 같은 문서 `NET-ROOT-001~003`: Simulation Root와 Visual Root/Animator 표현 책임을 분리한다.
- 같은 문서 `NET-FACING-001`: NetworkTransform은 Client Simulation Root의 유일한 writer다.
- `LogRules.md` 1.5·1.7·1.14 금지 사항 8: 개발 진단 로그 경계와 이벤트 기반 bounded 기록을 유지한다.

## 9. 비범위

- 서버 경로·repath·이동 속도·타일 commit 변경
- 피해·공격 회차·타겟 선택·Impact 타이밍 변경
- `m_OrientationOffsetY=-58` 추가 변경
- 전역 성능 결함 해결 또는 성능 PASS 판정
- 다른 24종 유닛 일괄 수정
- Testcase 작성 및 QA 실행

## 10. 조사 결론

현재 가장 강한 증거는 Walk→Attack에서 상위 두 Root가 멈춘 채 Humanoid body만 `4.7021` 변한 사건이지만, 이 값을 얻은 접근 자체가 Android 경고 27,727건을 유발했다. 1차 교정에서 `Animator.bodyPosition`과 `get_bodyPosition` 호출을 motion observer에서 완전히 제거해 테스트 환경을 오염시키던 경로를 차단했다.

InfernoSpirit의 유일한 `SkinnedMeshRenderer`와 그 `rootBone`을 `UnitView.Initialize`에서 캐시하며, production 검증에서 이 root bone이 Animator의 Hips와 같은 Transform인지 확인한다. `LateUpdate`는 캐시된 `Transform.position`만 읽는다. `renderer.bounds.center`는 메시 외곽을 대조하는 보조 증거이며 root bone을 대신하는 fallback으로 사용하지 않는다. 첫 valid anchor 표본은 baseline 저장 전용으로 처리해 로그와 사건 예산을 소비하지 않는다.

사건 제한은 유닛별 총 8건을 유지하면서 일반 사건을 최대 7건으로 제한하고 첫 Walk↔Attack severe 사건 1건을 예약한다. 전체 key 제한 128도 유지했으며, production 크기 `64 units × 8 events`, `128 keys` 경계를 self-validation에 추가했다. 이 교정은 NetworkTransform, Visual Root, 이동 writer, animation clip, prefab, orientation을 변경하지 않았다.

Runtime/Editor C# 정적 컴파일은 오류 0건이었다. Unity 메뉴 `Run Unit Action Self Validation`과 `Self Validate Unit Root Pose Cross Audit`은 PASS했고 Unity Console error는 0건이었다. 다만 이 결과는 코드 계약과 production hierarchy를 확인한 자동 검증이다. Android 실기기에서 새 빌드를 실행하지 않았으므로 렉 또는 순간이동 해결 완료로 판정하지 않는다.

다음 판정 gate는 새 빌드의 실제 생산 경로에서 InfernoSpirit 이동과 Walk↔Attack을 반복하고, body position Unity 경고 및 `get_bodyPosition` 스택이 0건인지 확인한 뒤 새 rendered-anchor 사건을 Host/Client 역할별로 분석하는 것이다. 그 결과로만 animation/presentation jump 분기 A 또는 Client root gap 분기 B를 선택한다.

## 11. 1차 교정 후 Android 실기 재검증 — 2026-09-21

사용자는 새 빌드에서 InfernoSpirit의 기본 이동과 공격 직전 이동이 모두 부자연스럽다고 확인했다. 같은 경기의 Host와 Android Client 로그를 비교한 결과, 1차 교정은 `get_bodyPosition` 폭주를 제거했지만 실제 화면 결함은 남았고 공격 방향 진단의 `get_bodyRotation` 호출도 제거하지 못했다. 따라서 이 Task는 완료가 아니라 **2차 교정 대기 / OPEN**이다.

### 11.1 증거 범위

| 항목 | 값 |
|---|---|
| 공통 세션 | `7baaffab77f02b028c5257c17c0ab66dd2b2b9b7d1957acb3f05c9aae70ed83b` |
| Android Client | `Assets/_Project/Docs/_Logs/2026-09-21/13_28_logcat/RuntimeLog_device.txt` |
| Editor Host | `Assets/_Project/Docs/_Logs/_editor/2026-09-21/RuntimeLog.txt` |
| 중복 가능 캡처 | `13_27`은 같은 테스트를 다시 저장한 것으로 보이므로 독립 표본으로 합산하지 않음 |
| InfernoSpirit | 9개: `10, 21, 29, 33, 45, 53, 57, 69, 76` |

Root Pose periodic PASS는 projection 구조와 안정 endpoint가 계약 안에 있음을 확인한다. 연속 프레임의 sub-frame jitter나 Walk↔Attack 순간의 렌더 기준점 이동이 없다는 뜻은 아니다.

### 11.2 확정된 사실

#### Host 권위 이동

- Host motion 64건은 `presentation-anchor-jump` 63건, `long-frame-catch-up` 1건이다.
- Host Simulation Root 최대 delta는 `0.0826`이다. 이 최댓값도 `rawFrame=0.101342s`인 긴 프레임 catch-up에서만 발생했다.
- 따라서 이번 세션에는 서버가 정상 프레임에서 유닛을 크게 순간이동시킨 authoritative root jump 증거가 없다.

#### Client 기본 이동

- Client motion 70건은 `presentation-anchor-jump` 58건, `unavailable-ambiguous` 12건이다.
- Simulation Root delta가 `0.1`을 넘은 사건은 8건, `0.2`를 넘은 사건은 1건이다.
- 최대는 unit `57`의 `0.4744`이며 `rawFrame=0.052084s`였다. 같은 Host 실행보다 큰 Client whole-root/Visual 이동이므로 receive/interpolation update가 뭉쳐 보였다는 강한 증거다.
- 다만 현재 로그에는 같은 pose를 직접 묶는 exact peer event join이 없다. 따라서 이를 아직 `client-only-replication-gap`으로 확정하지 않고 `unavailable-ambiguous`로 유지한다.

#### 공격 직전 렌더 표현

- Client rendered-anchor 최대 delta는 `0.4845`이고 `0.2` 초과 사건은 16건이다.
- 여러 사건에서 Simulation/Visual Root delta는 약 `0.05`인데 rendered-anchor는 `0.3~0.48` 이동했다.
- Host Walk↔Attack 전환은 17건이며, 그중 13건은 Simulation Root가 `0.01` 미만인 정지 상태인데 rendered-anchor가 `0.05~0.11` 이동했다. 전환 최대 anchor delta는 `0.1067`이다.
- 그러므로 사용자가 본 공격 직전 이상에는 서버 이동과 별개인 animation/presentation displacement가 실제로 포함된다.

#### 남아 있는 금지 Animator API와 로그 손실

- `get_bodyPosition`은 0건으로 제거됐다.
- 그러나 Android Client에서 `Animator:get_bodyRotation`과 동일 Unity 경고가 각각 **296건** 발생했다. 구간은 `13:24:45.592~13:26:20.216`이다.
- 스택은 `UnitView.ObserveInfernoAttackFacing → ConsumePendingInfernoAttackStartFacing/LateUpdate`다. 이전 27,727건의 매 프레임 폭주보다는 작지만 공격 구간의 성능과 시간 표본을 계속 오염시킨다.
- Android motion line은 `rendererBounds` 중간에서 잘려 `phase`, `transition`, `allowance` 등 후반 필드가 누락됐다. 현재 schema는 Android-safe compact 계약을 충족하지 못한다.

### 11.3 아직 확정하지 않은 가설

- Inferno mesh/Hips 기준점은 Unit Root에서 수평으로 약 `0.8` 떨어져 있다. Root가 회전하면 이 기준점은 원호를 그리므로 작은 회전도 큰 위치 이동처럼 보일 수 있다.
- 그러나 약 `0.8` 오프셋이 production prefab hierarchy의 local transform 때문인지, Animator/avatar/clip root transform과 Humanoid retarget pose 때문인지는 아직 확정하지 않았다.
- Client 최대 `0.4744`는 replication/interpolation clumping의 강한 후보지만 exact Host/Client pose 결합이 없으므로 NetworkTransform 자체의 결함으로 단정하지 않는다.
- `bodyRotation` 경고 296건이 공격 구간 렉을 악화시킬 수는 있으나 기본 이동 전체의 단독 원인이라고 단정하지 않는다.

## 12. 조사 결론 갱신

현재 결함은 최소 두 축으로 분리된다. 기본 이동에서는 Client가 Host보다 큰 whole-root/Visual delta를 보였고, 공격 직전에는 정지한 Host Root 위에서도 rendered-anchor가 움직였다. 여기에 `bodyRotation` 경고와 Android 로그 절단이 새 표본의 신뢰도를 낮추고 있다.

따라서 다음 교정은 NetworkTransform 값을 바로 바꾸는 작업이 아니다. 먼저 모든 Animator body API를 일반 `LateUpdate`/marker 진단에서 제거하고, 렌더 기준점의 Root 대비 수평 오프셋과 같은 frame 회전량을 함께 남겨 실제 이동량이 회전 원호 예측과 맞는지 검증한다. 동시에 Android 한 줄 제한 안에서 phase·transition·allowance가 보존되도록 motion schema를 압축한다. 이 깨끗한 재실기에서도 Client Root 최대 delta가 비정상으로 남을 때만 receive/interpolation 분기를 연다.

## 13. 2차 계측 후 Android 실기 분석 — 2026-09-21 16_07

이번 로그는 화면에서 크게 튄 InfernoSpirit의 몸 중심 이동이 실제 유닛 Root 순간이동과 같은 현상이 아니라, 회전축에서 옆으로 떨어진 메시 기준점이 Root 회전에 따라 원호를 그린 현상임을 수치로 분리했다. InfernoSpirit만 별도의 이동 알고리즘을 사용하는 증거는 없으며, 공통 이동·NetworkTransform을 바꾸기 전에 양 진영 production prefab의 메시 자식 X 편심만 단일 변수로 교정하는 것이 현재 증거에 맞는 최소 수정이다.

**현행 문서 상태:** `주된 화면 점프 원인 판정 완료 · prefab X 단일 변수 교정 승인 / 사용자 실기 전 OPEN`

### 13.1 증거 범위와 실측 결과

| 항목 | 값 |
|---|---|
| Android Client 로그 | `Assets/_Project/Docs/_Logs/2026-09-21/16_07_logcat/RuntimeLog_device.txt` |
| Inferno motion event | 56건 |
| 분류 | `presentation-anchor-jump` 51건 · `unavailable-ambiguous` 5건 |
| 회전 원호 일치 | `arcMatch=true` 56/56건 |
| 최대 절대 residual | `0.0046` |
| 최대 rendered anchor delta | `0.5204` |
| 최대 Simulation Root delta | `0.1993` |
| 최대 Root 회전량 | `37.3146°` |
| long frame | 0건 |
| Walk↔Attack transition | 7건 |

- 56건 전부에서 관측된 Root 대비 anchor 상대 이동이 `2 × rootToAnchorOffset × sin(rotationDelta/2)` 원호 예측과 허용 오차 안에서 일치했다.
- 최대 사건은 anchor가 `0.5204` 이동한 반면 같은 사건의 Simulation Root 이동은 그보다 작았고, Root 회전량은 `37.3146°`였다. 따라서 플레이어가 본 큰 점프의 주된 성분은 Root의 병진 순간이동이 아니라 오프센터 anchor의 회전 원호다.
- `long=true`가 0건이므로 이번 56건을 긴 프레임 catch-up으로 설명할 근거는 없다.
- 5건의 `unavailable-ambiguous`에는 Simulation Root가 최대 `0.1993` 움직인 사건이 남아 있다. 이 5건은 prefab 교정 후 다시 관찰해야 하며, 현재 증거만으로 Client 복제/보간 결함이 해소됐다고 판정하지 않는다.

### 13.2 production prefab 비교

| production prefab의 비교 대상 메시 자식 | local X |
|---|---:|
| `Unit_InfernoSpirit_Blue.prefab` | `-0.8129883` |
| `Unit_InfernoSpirit_Red.prefab` | `-0.813` |
| `Unit_FlameSpirit_Blue.prefab` 동등 계층 | `0` |
| `Unit_EmberSpirit_Blue.prefab` 동등 계층 | `0` |

InfernoSpirit 양 진영만 메시 자식이 회전축에서 약 `0.813` 옆으로 떨어져 있고, 로그의 `rootToAnchorOffset`도 약 `0.816~0.823`으로 같은 크기를 보인다. 양 진영의 거의 같은 X 값과 56/56 원호 일치는 화면 점프의 직접 원인이 production prefab hierarchy의 X 편심임을 지지한다.

사용자는 InfernoSpirit에 별도의 X 위치 보정을 적용한 적이 없으며 이 값을 누락·실수로 판단했다. 이에 따라 Blue/Red의 해당 메시 자식 local X만 `0`으로 바꾸는 단일 변수 교정을 승인했다. 이 사용자 확인은 값의 의도 여부에 대한 근거이며, 실제 화면 결함 해소 여부는 교정 후 실기에서 별도로 확인한다.

### 13.3 원인 판정과 남은 경계

- **주된 화면 점프 원인:** production InfernoSpirit Blue/Red prefab 메시 자식의 local X 편심과 Root 회전의 결합.
- **원인이 아닌 것으로 판정한 것:** InfernoSpirit 전용 이동 알고리즘. 현재 이동 writer와 NetworkTransform은 다른 유닛과 공통이며, 이번 로그는 큰 anchor 이동을 회전 원호로 설명한다.
- **이번 판정으로 완료되지 않은 것:** `unavailable-ambiguous` 5건의 Root 이동, 공격 방향 `-58°`, Client 복제/보간, 전역 성능 문제.
- prefab X 교정 후에도 큰 Simulation Root delta가 반복되면 그때 exact Host/Client 결합 증거를 확보해 복제/보간 분기를 다시 연다. X 교정 결과를 보기 전에 공통 이동 코드나 NetworkTransform을 변경하지 않는다.

이 분석은 원인 판정과 다음 최소 교정의 승인까지 기록한 것이다. 교정 후 사용자가 실제 생산 경로에서 이동과 공격 진입을 확인하기 전에는 PASS 또는 CLOSED로 갱신하지 않는다.

## 14. prefab 메시 X 교정 후 Android 실기 결과와 VFX 잔여 원인 — 2026-09-21 21_16

사용자는 메시 자식 X를 중앙으로 옮긴 새 빌드에서 InfernoSpirit의 이동 중 순간이동 현상이 사라졌다고 확인했다. 같은 실행의 Android 로그도 이전의 큰 `presentation-anchor-jump`가 재발하지 않았음을 보여 준다. 따라서 이동 문제는 이번 실기 범위에서 해소된 것으로 판정한다. 다만 공격 VFX가 모델과 어긋나는 현상이 새로 확인됐고, production prefab을 대조한 결과 VFX 생성점이 메시 교정 전 좌표계에 남아 있는 것이 직접 원인이다.

**현행 문서 상태:** `이동 순간이동 실기 해소 확인 · VFX SpawnPoint 상대 X 교정 및 재실기 전 OPEN`

### 14.1 최신 Android 실기 판정

| 항목 | 값 |
|---|---:|
| Android Client 로그 | `Assets/_Project/Docs/_Logs/2026-09-21/21_16_logcat/RuntimeLog_device.txt` |
| Inferno motion event | 37건 |
| `unavailable-ambiguous` | 37건 |
| `presentation-anchor-jump` | 0건 |
| `rootToAnchorOffset` 범위 | `0.0035 ~ 0.0158` |
| Root Pose 최종 판정 | `PASS`, `errors=0` |
| 공격 결과 spatial | 50 samples, mismatch 0, `maxDelta=0.001` |

- 이전 실행에서 약 `0.816`이었던 Root-to-anchor 편심이 이번에는 최대 `0.0158`로 줄었다. 메시가 회전축에서 크게 떨어져 원호를 그리던 현상이 제거됐다는 정량 증거다.
- 37건은 모두 `simDelta`가 event allowance를 넘은 `unavailable-ambiguous`이며, 실제 렌더 anchor만 별도로 튄 `presentation-anchor-jump`는 0건이다. 사용자의 육안 확인과 함께 볼 때 이번 교정 대상이었던 메시 편심 순간이동은 재현되지 않았다.
- Root Pose는 `summary-END verdict=PASS`, `errors=0`이고 공격 결과 위치도 50개 표본에서 mismatch가 0이다. 서버 이동 writer, Root 투영, 공격 결과 위치를 VFX 정렬 문제의 원인으로 볼 근거는 없다.
- `unavailable-ambiguous`는 Client의 프레임별 Root 전달 간격을 확정적으로 정상 판정하는 값은 아니다. 그러나 사용자가 이번 실행에서 순간이동을 보지 않았고 presentation jump도 0이므로, VFX 교정을 위해 공통 이동·NetworkTransform을 다시 변경하지 않는다.

### 14.2 공격 VFX 위치 불일치의 정적 원인

메시 X 교정 전후 값과 현재 `VfxSpawnPoint`를 production Blue/Red prefab에서 직접 비교했다.

| 진영 | 교정 전 메시 X | 현재 메시 X | 현재 `VfxSpawnPoint` X | 교정 전 메시 기준 상대 X |
|---|---:|---:|---:|---:|
| Blue | `-0.8129883` | `0` | `-0.8215834` | `-0.0085951` |
| Red | `-0.813` | `0` | `-0.82159513` | `-0.00859513` |

메시는 중앙으로 이동했지만 `VfxSpawnPoint`는 교정 전 절대 X에 남았다. 그 결과 VFX만 모델에서 약 `0.8216` 옆으로 떨어져 생성된다. 이것은 로그의 공격 결과 위치 mismatch가 아니라 prefab 내부 표현 자식의 상대 좌표 불일치다.

교정값은 단순 `0`이 아니다. 기존 메시와 생성점 사이에 있던 약 `-0.008595`의 작은 상대 간격은 의도된 세부 배치로 보존하고, 메시가 이동한 양만큼 생성점을 함께 옮긴다.

- Blue: `-0.8215834 - (-0.8129883) = -0.0085951`
- Red: `-0.82159513 - (-0.813) = -0.00859513`

따라서 다음 최소 교정은 Blue/Red `VfxSpawnPoint.localPosition.x`만 위 상대값으로 바꾸는 것이다. Y/Z, rotation, scale, 메시, 애니메이션 `-58°`, 이동·NetworkTransform·공격 결과 코드는 수정하지 않는다.

### 14.3 완료 경계

이번 실기로 이동 순간이동 축은 해소 판정한다. Task 전체는 VFX 좌표 교정 뒤 사용자가 Blue/Red의 여러 공격 방향과 이동→공격 전환에서 모델과 VFX가 맞는지 확인하기 전까지 `OPEN`으로 유지한다. 자동 검증과 빌드 시작만으로 완료 처리하지 않는다.

## 15. VFX SpawnPoint 교정 후 간헐 미표시 실기 분석 — 2026-09-22

사용자는 SpawnPoint 위치 교정 빌드에서 VFX가 가끔 보이지 않는 것 같다고 보고했다. 최신 Android Host와 Editor Client의 같은 경기 로그를 비교한 결과, 공격 결과 전송이나 프리팹 위치 문제가 아니라 클라이언트가 일부 인페르노 타격 표식에서 VFX 호출 지점까지 도달하지 않은 정황이 확인됐다. 현재 로그는 원시 Animation Event 자체가 빠진 경우와 이벤트는 왔지만 회차 허가가 없어 조기 차단된 경우를 구분하지 못하므로, 값을 다시 조정하기 전에 이 경계를 한 번의 재실기로 분리해야 한다.

**현행 문서 상태:** `VFX 위치 교정 후 간헐 미표시 재현 정황 확인 · 경계 진단 구현 및 재실기 전 OPEN`

### 15.1 동일 경기 교차 증거

공통 세션 키는 `7ee32ccbe2dc391f2ecbe57391d86fa66b5fbabbbf120e2f5f1b47950b5b9520`이다.

| 역할 | InfernoSpirit `ImpactMarker` |
|---|---:|
| Android Host | 32 |
| Editor Client | 28 |

- 유닛 10은 Host 13회 / Client 12회, 유닛 15는 Host 9회 / Client 6회였다.
- 유닛 18은 7/7회, 유닛 25는 3/3회로 일치했다.
- `ImpactMarker`는 `UnitView.OnAttackHit`에서 유효 회차 허가를 소비한 뒤, `EffectManager.PlayUnitAttack` 호출 직전에 기록된다. 따라서 Client에서 부족한 4회는 해당 화면에서 공격 VFX 호출까지 도달하지 않은 정황이다.
- 유닛 15의 차이 3회는 타겟 표시 정보가 사라지거나 바뀌는 구간과 겹친다. 타겟 수명과 로컬 Animator/회차 허가의 타이밍 경쟁이 우선 가설이지만, 현 로그만으로 Animation Event 미발생과 허가 소비 실패 중 하나를 확정하지 않는다.
- 공격 결과 coordinator 실패, 결과 거부, presentation transport 실패, spatial mismatch는 0건이다. `hitVfx=0/0/47`은 피해자 피격 VFX 통계이며 인페르노 공격 VFX가 아니다.
- 공격 프리셋과 `vfx_infernospirit_charge` 연결은 존재하고 VFX pool은 활성 항목이 없으면 새 인스턴스를 만드는 구조라 고정 누락이나 pool 개수 제한이 이번 간헐 현상의 우선 원인은 아니다. 다만 실제 ParticleSystem 재생 성공은 기존 로그에 없었다.

### 15.2 닫아야 할 관측 공백

다음 재실기는 InfernoSpirit 공격 Animation Event 1회마다 아래 결과를 한 줄의 bounded evidence로 남긴다.

1. 원시 Animation Event가 `OnAttackHit`에 도착했는지
2. `Scoped` 공격 회차 허가를 실제 소비했는지와 exact scope
3. EffectManager, 공격 preset, VFX prefab, pool item이 존재했는지
4. ParticleSystem 개수와 `Play` 직후 실제 활성 상태

이 로그로 Host/Client의 raw event 수부터 다르면 로컬 Animator 표식 누락, raw event는 같지만 `gate-suppressed`가 다르면 회차 허가 전달/소비 문제, 허가는 같지만 playback failure가 있으면 EffectManager/preset/pool/particle 문제로 분기한다. 이번 단계에서는 공격 주기, 타겟, 피해, VFX 위치, 애니메이션 클립을 변경하지 않는다.

## 16. 간헐 미표시 경계 진단 실기 결과 — 2026-09-22 04_11

새 계측 빌드에서 사용자가 InfernoSpirit 공격 VFX 누락을 다시 재현했다. 같은 경기의 Android Host와 Editor Client는 원시 Animation Event 수가 정확히 같았고 VFX 에셋·풀·ParticleSystem 실패도 없었다. 차이는 Client가 타겟 사망·전투 종료를 먼저 적용한 뒤 이미 진행 중이던 공격 클립의 marker를 `Suppressed`로 거부한 9회뿐이다.

**현행 문서 상태:** `원인 확정 · Stop과 이미 커밋된 marker의 수명 분리 교정 승인 · 구현/재실기 전 OPEN`

### 16.1 동일 경기 확정 증거

공통 세션 키는 `2c94c9a265cc09f039171f7c50361a7bd58f8e3483584c56ccbbf183478ee2e2`다.

| 역할 | raw event | VFX started | gate suppressed | playback failure | overflow |
|---|---:|---:|---:|---:|---:|
| Android Host | 70 | 70 | 0 | 0 | false |
| Editor Client | 70 | 61 | 9 | 0 | false |

- Client 차단은 Unit 10에서 2회, Unit 19에서 4회, Unit 23에서 3회였다.
- 차단된 9회는 모두 `rawEvent=true`, `mode=Suppressed`, `outcome=gate-suppressed`였다. 원시 Animation Event 누락은 아니다.
- 정상 재생 131회는 모두 manager/preset/prefab/pool item이 존재하고 `particleSystems=1`, `playbackActive=true`였다. 공격 VFX 에셋·풀·ParticleSystem이 이번 누락의 원인이 아니다.
- 7건은 같은 유닛의 `AttackEndPose` 뒤 약 `0.01~0.29초` 안에 marker가 도착했다. 나머지 2건도 타겟 `EntityDiedClientRpc` 직후 약 `0.03~0.09초` 안에 도착했다.
- `StopCombatAnimation()`은 공격 클립을 즉시 끝내지 않으면서 `RetireAndCloseAttackPresentationScope()`로 marker 허가를 먼저 닫는다. 네트워크 종료 신호가 로컬 Animation Event보다 먼저 도착한 Client에서만 이미 커밋된 마지막 공격의 발사 VFX가 사라진다.

### 16.2 공통 영향 범위

이 결함은 InfernoSpirit 전용 VFX 코드가 아니라 모든 일반 유닛이 공유하는 `UnitView.OnAttackHit`과 `StopCombatAnimation` 사이의 수명 경쟁이다. 현재 실기 발생이 확정된 타입은 InfernoSpirit뿐이지만 다음 두 범위를 함께 회귀해야 한다.

- `LegacyFallback`: Tank, CannonCart, InfernoSpirit, StreamSpirit, FoxMagician, EagleArcher, MushroomBomber, TorrentSpirit. 정규 scope 없이 로컬 marker를 보존하므로 같은 순서 경쟁에 직접 노출된다.
- `Scoped`: exact scope lease가 있으나 공통 Stop이 lease를 marker 전에 닫을 수 있다. 같은 결함이 실기에서 확인된 것은 아니지만 순수 순서 회귀로 반증해야 한다.
- BloomFairy는 별도 회복 경로이므로 이번 일반 공격 marker 교정 범위에서 제외한다.

### 16.3 교정 원칙

전투 타겟 수명과 이미 커밋된 공격자 발사 연출의 수명을 분리한다.

1. 서버 commit 명령을 수락할 때 revision과 marker 개수를 가진 bounded source-marker 예약을 연다.
2. 정상 marker는 source 예약과 기존 exact scope를 함께 소비해 기존 VFX/SFX/Tracer/피격 신호를 방출한다.
3. 일반 Stop·타겟 사망이 먼저 도착하면 미래 공격과 타겟 의존 표현은 닫되, 이미 커밋된 source VFX/SFX marker만 남은 횟수만큼 보존한다.
4. 보존 marker는 공격자 위치의 VFX/SFX만 1회 방출하고 target/tracer/local-hit을 만들지 않는다. 사라진 타겟이나 새 타겟으로 이전하지 않는다.
5. 명시적 impact suppression, 공격자 사망, despawn, 새 revision 교체는 남은 예약을 즉시 폐기한다.
6. marker를 먼저 소비한 뒤 Stop이 오거나 Stop 뒤 루프 marker가 반복돼도 추가 방출은 0회여야 한다.

이 원칙은 `GameSystemRules_Units.md` 규칙 17·19와 `GameSystemRules_UnitCombatSynchronization.md`의 `NET-AUTH-002`, `NET-PRESENT-001~003`, `NET-CANCEL-001~005`에 근거한다. 이미 승인된 표현을 일반 Stop이 소급 취소하지 않되, 구회차 marker를 새 타겟이나 새 회차에 재사용하지 않는다.

## 17. source-marker 보존 교정 최종 실기 — 2026-09-22 11_44

사용자가 교정 빌드를 실제 게임에서 다시 시험했고 육안상 정상으로 판단했다. 같은 경기 `sharedSessionKey=dc055118048e26ecaac1a0bd0c19b72b7c9b380364ffb88b07499b55ef646c8f`의 Editor Host와 Android Client를 교차 분석했다.

| 역할 | raw event | VFX started | source-only 보존 | gate suppressed | playback failure |
|---|---:|---:|---:|---:|---:|
| Editor Host | 65 | 64 | 0 | 1 | 0 |
| Android Client | 64 | 64 | 11 | 0 | 0 |

- 이전 Client 실패 기준선의 `gateSuppressed=9`는 이번 실행에서 0이 됐다.
- Client는 Stop이 marker보다 먼저 도착한 확정 공격 11회를 `SourceOnly`로 보존했고, 모든 시도가 실제 ParticleSystem 재생까지 도달했다.
- 양쪽 실제 VFX 시작 수는 64/64이며 유닛별 시작 수도 일치했다. 반복 marker, overflow, playback failure는 없었다.
- Host의 추가 raw event 1회는 생산 직후 Unit 29의 공격 정렬 오차 `98.363°`에서 발생한 커밋 전 marker다. `production-start-gate=Misaligned` 뒤 `gate-suppressed`된 정상 fail-closed이며, 확정 공격 VFX 누락이 아니다.
- 이번 교정은 공격자 source VFX/SFX만 보존하고 사라진 타겟의 tracer·local hit·damage를 만들지 않는 계약을 실제 네트워크 도착 순서에서 확인했다.

사용자가 이동 순간이동 해소, VFX 위치 교정, 간헐 누락 해소를 최종 확인했으므로 이 Task는 `PASS/CLOSED`다. 다만 InfernoSpirit은 여전히 `LegacyFallback` 범위이며 전체 25종·역할교대·Legacy rollback 통합 회귀 또는 ActionSequence 전체 완료를 뜻하지 않는다.
