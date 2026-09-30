# InfernoSpirit 공격 몸 방향 증거 조사

InfernoSpirit가 실제 게임에서 타겟과 어긋난 방향을 바라보며 공격하는 것처럼 보였지만, 현재 로그는 서버 `Simulation Root`와 표현 계층의 `Visual Root`까지만 관측한다. Animator가 Attack 클립을 평가한 뒤 화면에 보여 주는 몸 방향은 기록하지 않으므로, 현재 증거만으로는 애니메이션 축 보정·팀 관점 변환·타겟 복제 중 어느 축이 원인인지 판정할 수 없다.

**문서 상태:** `-58°` 방향 비교 실기 FAIL / motion-jump v11의 금지된 bodyPosition 접근이 Android 경고 27,727건을 유발해 테스트 환경 오염 확인 / 이동 교정은 `2026-09-21/02_42_infernospirit-movement-jump-correction` Task로 이관 / OPEN

## 1. 사용자 재현과 확인된 사실

| 항목 | 확인 내용 |
|---|---|
| 대상 유닛 | `InfernoSpirit` |
| 육안 증상 | 공격 중 타겟과 다른 방향을 바라보는 것처럼 보임 |
| 원래 Attack clip 설정 | `InfernoSpirit_Attack.anim`의 `m_OrientationOffsetY=-45` |
| 사용자 비교 실험 | `-45`를 `0`으로 변경하자 어긋남이 더 커짐 |
| 당시 수집 설정 | 사용자가 `0` 비교 뒤 `-45`로 복원한 상태에서 아래 Host/Client 방향 증거를 수집 |
| `-45`의 성격 | 애니메이션을 처음 추가해 육안 테스트할 때 사용자가 적용한 모델/클립 축 보정값 |
| 최신 로그의 서버 정렬 | `AlignToAttack`에서 `Ready`까지 수렴했고 공격·결과·표현 실패 카운터는 0 |
| 최신 Root 관측 | Host/Client의 Simulation Root→Visual Root 관측은 PASS |
| 남은 증거 공백 | Attack 포즈가 적용된 Animator의 실제 몸 방향과 타겟 방향 사이 각도 |

`0`에서 더 악화된 비교 결과는 `-45`가 임의의 최근 변경이 아니라 원본 애니메이션 축을 일부 보정하고 있다는 가설을 지지한다. 그러나 이것만으로 정확한 보정값이나 원인을 확정할 수는 없다. 따라서 `-60`, `-90` 같은 추가 각도를 육안 추측으로 적용하지 않는다.

> **후속 통제 실험 (2026-09-20):** 아래 §9의 `-45°` Impact 실측으로 Host 평균 `+12.47°`, Client 평균 약 `+13.38°`의 고정 잔여 오차가 확인된 뒤, 사용자가 Unity Inspector에서 orientation을 `-58°`로 직접 변경·저장하고 이 한 변수만 비교하는 빌드 테스트를 승인했다. 디스크의 `InfernoSpirit_Attack.anim`도 `m_OrientationOffsetY=-58`로 확인했다. 이는 근거 없는 추가 보정이 아니라 약 `13°`의 측정 잔여량을 음수 방향으로 더한 통제 실험이며, 결과 확정 전 최종값으로 간주하지 않는다.

## 2. 현재 관측 경계

현재 전투 진단은 다음 경계를 검증한다.

1. 서버가 타겟 방향으로 `Simulation Root`를 정렬하는가.
2. NetworkTransform과 `VisualRootProjector`가 Host/Client의 `Visual Root`에 기대한 방향을 전달하는가.
3. 같은 공격 회차의 일정·결과·표현 스코프가 충돌 없이 결합되는가.

하지만 Animator는 `Visual Root` 아래 모델 뼈대를 평가한다. Attack clip의 orientation 보정이나 root/body 회전 곡선이 모델을 돌려도 상위 `Visual Root.forward`는 정상일 수 있다. 그러므로 기존 Root PASS는 “부모 표현 계층이 정상”이라는 뜻이지 “렌더링된 몸이 타겟을 본다”는 뜻이 아니다.

## 3. 진단 가설

| ID | 가설 | 실측 시 예상 패턴 |
|---|---|---|
| H1 | `-45`는 원본 클립 축에 필요한 고정 보정이며 현재도 부족하거나 과함 | Root/VisualRoot 오차는 작고 Animator 몸 오차가 세 지점에서 비슷한 상수로 유지 |
| H2 | Attack 클립 내부 회전 곡선 때문에 공격 구간별 몸 방향이 변함 | 시작·Impact·종료 중 특정 구간에서만 Animator 몸 오차가 커짐 |
| H3 | Red/Client 관점의 180도 Visual Root 변환과 Animator 방향 해석이 중복 또는 누락됨 | Host/Blue는 정상이고 Client/Red에서만 약 180도 또는 일정한 큰 오차 |
| H4 | 타겟 교체·복제 지연으로 렌더링 시점의 타겟과 공격 회차 타겟이 다름 | Root와 몸이 같은 방향이지만 기록된 현재 타겟과 회차/Revision이 어긋남 |
| H5 | 서버 정렬 또는 NetworkTransform 방향 자체가 간헐적으로 틀림 | Animator 몸뿐 아니라 Simulation Root와 Visual Root도 같은 시점에 타겟 오차 증가 |

현재 증거는 H1을 우선 가설로 만들 뿐 확정하지 않는다. H1~H5를 같은 로그 표본에서 분리할 수 있어야 다음 교정값을 결정할 수 있다.

## 4. 필요한 실제 렌더링 방향 증거

InfernoSpirit의 공격 한 회차에서 아래 세 사건만 관측한다.

| 사건 | 수집 시점 | 이유 |
|---|---|---|
| `AttackStartPose` | Attack 전환 요청 직후가 아니라 Animator가 포즈를 실제 평가한 뒤 최초 1회 | 이전 Walk/Idle 포즈를 Attack 시작 증거로 오인하지 않기 위해 |
| `ImpactMarker` | `OnAttackHit` Animation Event가 실행된 순간 | 사용자가 타격 동작으로 인식하는 핵심 프레임의 몸 방향을 확인하기 위해 |
| `AttackEndPose` | Attack 종료 전환으로 포즈를 잃기 직전 | 클립 후반 회전 곡선과 종료 시 타겟 유지 여부를 분리하기 위해 |

각 사건은 다음 값을 같은 구조로 남긴다.

- 역할과 관점: Host/Client, 팀 또는 관점 변환 여부
- 식별: 유닛 타입, 공격자 인스턴스, 타겟 종류/ID, `AttackSequenceId`, Revision, 가능한 경우 HitIndex
- Animator 상태: 현재/전이 상태, normalized time, Humanoid body 방향을 유효하게 읽었는지 여부와 실패 사유
- 방향: Simulation Root forward, Visual Root forward, Animator body forward, 타겟 방향
- 각도: 각 방향에서 타겟 방향까지의 XZ 평면 signed yaw와 Animator body↔Visual Root signed yaw

유효한 회차 또는 Humanoid body 방향을 얻지 못한 경우 `0`이나 대체 방향을 정상 증거로 만들지 않는다. 실제 값과 `Unavailable` 사유를 구분해 기록한다.

## 5. 로그 존속과 bounded 정책

- 매 프레임 기록하지 않고 위 세 사건의 상태 전이에서만 기록한다.
- 동일 공격자·회차·Revision·사건은 최대 한 번만 수락한다.
- 기존 `UnitAttackShadowObserver`의 공격자별 제한 저장소와 retire 수명을 재사용하며, 무제한 전역 목록을 추가하지 않는다.
- 회차가 없는 provisional 시작은 회차 `0`을 정규 회차처럼 꾸미지 않고 provisional 여부를 함께 기록한다.
- 시작 포즈 예약은 다음 Animator 평가 뒤 한 번만 소비하고, Attack이 취소되거나 View가 종료되면 폐기한다.
- 정상 증거는 개발 진단 로그로 남기고 릴리스에서 호출과 문자열 조립이 제거되는 기존 로깅 경계를 따른다.
- 증거를 얻지 못한 상태를 PASS로 해석하지 않는다. `bodyEvidenceValid=false`는 방향 일치가 아니라 미관측이다.

이는 `LogRules.md` 1.5의 이벤트 키 계약, 1.7의 개발 로그 스트리핑, 1.14 금지 사항 8의 매 틱·매 프레임 로깅 금지를 따른다.

## 6. 관련 규칙

- `GameSystemRules_Units.md` 규칙 15: 공격 중 유닛은 타겟 방향을 유지해야 한다.
- 같은 문서 규칙 17: `OnAttackHit`은 Attack clip의 타격 표식이므로 Impact 몸 방향 관측 지점으로 사용한다. 실제 피해 권위로 사용하지 않는다.
- 같은 문서 규칙 22: Attack Animator 상태는 서버 권위 상태를 표현하지만 조준 회전·피해 판정과 책임을 섞지 않는다.
- `GameSystemRules_UnitCombatSynchronization.md` `NET-AUTH-001`: Animator와 로컬 Animation Event를 서버 결과 입력으로 사용하지 않는다.
- 같은 문서 `NET-ROOT-001~003`: Simulation Root, Visual Root, 실제 화면 방향의 책임을 분리한다.
- 같은 문서 `NET-FACING-002`: 공격 방향의 권위 원본과 타겟 잠금 경계를 유지한다.
- 같은 문서 `NET-PRESENT-001`: Animation Event는 로컬 표현 표식으로만 사용한다.
- 같은 문서 `NET-PRESENT-003`: 공격 방향 비교는 역할·회차·Revision을 섞지 않고 같은 식별 범위에서 해석한다.

## 7. 영향 범위와 비범위

### 영향 범위

- InfernoSpirit Attack 표현 시작·Impact marker·종료의 one-shot 관측 훅
- 기존 공격 observer의 bounded 몸 방향 증거와 순수 signed-yaw 분류 함수
- 몸 방향 계산과 유효성 분기를 확인하는 Editor static self-validation

### 비범위

- `m_OrientationOffsetY=-45` 추가 변경
- 서버 회전 속도, 타겟 선택, 피해·DoT, C2/C3 writer 변경
- NetworkTransform 또는 Visual Root에 보정 회전을 새로 쓰는 작업
- 다른 24종 유닛에 대한 일괄 보정
- 실제 측정 전에 애니메이션 클립 회전 곡선이나 모델 import 축을 수정하는 작업
- Testcase 및 QA 문서 작성

## 8. 조사 결론

이번 단계의 목적은 방향 문제를 바로 각도 값으로 봉합하는 것이 아니라, “서버 Root는 맞는데 화면 몸만 틀리는가”를 한 공격 회차 안에서 확정하는 것이다. 세 사건의 실제 몸 방향 증거를 얻은 뒤에만 고정 orientation 보정, 클립 구간 곡선 교정, 팀 관점 투영 교정, 타겟 수명 교정 중 하나를 다음 Task로 선택한다.

## 9. 구현·자동 검증·실기 수집 결과

기존 계획의 InfernoSpirit 전용 `animated-facing` 증거가 구현되었고, Unit Action Self Validation과 Unit Root Pose Cross Audit은 모두 PASS했다. 이후 Android 빌드와 실제 게임 생산 경로 테스트가 완료되었으며, 최신 Host/Client 공통 세션은 `aa5875a11546bdb8183d87b7c22aec276396670aa12029df35c075ac7b54bcde`이다. 자동 검증 PASS는 구조와 계산 계약이 유지됐다는 뜻이며, 아래 실기 방향 FAIL을 덮지 않는다.

### 9.1 유효 방향 표본

| 역할 | `AttackStartPose` | `ImpactMarker` | `AttackEndPose` |
|---|---:|---:|---:|
| Client | 49 | 28 | 12 |
| Host | 52 | 47 | 미수집 |

### 9.2 공격 시작과 Impact가 서로 다른 결함을 보여 준다

| 시점 | Root/Visual의 타겟 yaw 절대값 | Animator body 관측 | 판정 |
|---|---|---|---|
| 공격 시작 | Client 평균 8.30°·최대 41.83°, Host 평균 8.18°·최대 35.45° | 최대 Client 62.46°, Host 40.52° | Attack 전환이 5° 정렬 완료 전에 화면에 보이는 표본이 존재한다. |
| Impact | Host 0.00°, Client 평균 0.07°·최대 0.47° | body↔Visual 잔여 yaw: Host 평균 12.47°(11.80~14.18), Client 평균 약 13.38°(대략 11.77~16.24) | 상위 회전·복제는 타겟에 수렴했지만 실제 Animator 몸에는 별도 잔여 회전이 남는다. |

공격 시작 오차는 `U-ATK-ALIGN`의 5° ready gate가 서버 회차 커밋과 타격 승인은 막아도, 현재의 provisional Attack 선행 표현 자체는 막지 않는 구조에서 발생한다. 따라서 사용자는 정렬 중인 Attack 포즈를 실제 공격 시작으로 보게 된다. Impact에서는 Root/Visual이 사실상 정렬됐는데 몸만 약 12~14° 어긋나므로, 이 현상은 NetworkTransform이나 Simulation Root 보정으로 해결할 문제가 아니다.

현재 `InfernoSpirit_Attack.anim`의 `m_OrientationOffsetY=-45`는 유지한다. `-45`를 제거했을 때 더 악화된 사용자 비교와 이번 실측은 이 값이 원본 축 보정에 필요함을 지지하지만, 남은 12~14°가 클립 내부 `RootQ`/body 곡선 때문인지 Humanoid retarget/import 결과인지는 아직 분리되지 않았다. 따라서 이번 범위에서 `-57` 또는 `-58` 같은 새 보정값을 적용하지 않는다.

### 9.3 회전 후속 교정 경계

회전 교정은 이 Task의 순간이동 관측 추가와 섞지 않고 별도 Task에서 다음 두 단계로 나눈다.

1. 화면에 보이는 Attack 클립의 최초 시작을 기존 5° ready gate 뒤로 옮겨, 정렬 중 Attack 포즈가 공격 시작처럼 보이지 않게 한다. 타겟 전달의 원자성과 커밋 전 Impact 억제는 유지한다.
2. Impact 표본의 잔여 body yaw가 클립 내부 회전 곡선인지 Humanoid import/retarget 결과인지 분리한 뒤 orientation 값을 확정한다. 원인 분리 전에 수치만 추가 보정하지 않는다.

관련 근거는 `GameSystemRules_Units.md`의 `U-COMBAT-PHASE`, `U-ATK-ALIGN`, `U-ATK-TIMELINE` 및 규칙 15·17·22, `GameSystemRules_UnitCombatSynchronization.md`의 `NET-ROOT-001~003`, `NET-FACING-002`, `NET-PRESENT-001`이다.

## 10. 새 실기 증상: InfernoSpirit 이동 순간이동

사용자는 같은 실기에서 InfernoSpirit가 이동 중 간헐적으로 순간이동하는 모습을 확인했다. 이동 terminal, replication, Root observer의 기존 구조 실패 계수는 모두 0이었다.

- 이동 rejected/invalid/gate failure: 0
- movement/action writer conflict: 0
- spatial commit failure: 0
- repeated/fatal repath: 0
- Client revision/order conflict: 0
- Simulation Root→Visual Root projection error: 0

이 결과는 기존 관측 범위에서 구조 실패가 검출되지 않았다는 뜻일 뿐, 순간이동이 없었다는 뜻이 아니다. 기존 Root observer는 유닛별 `initial/moved/stable` 표본 위주여서 같은 유닛의 연속 프레임 위치 delta를 측정하지 않는다. 따라서 권위 Root 점프, Visual 계층 점프, Animator 몸체 내부 이동, 긴 프레임 뒤 따라잡기, Client 복제 간격을 현재 로그로 구분할 수 없다.

정적 확인에서 InfernoSpirit Blue/Red prefab의 `m_ApplyRootMotion=0`이다. Walk clip에 RootT X/Z 곡선은 존재하지만 GameObject root motion 적용은 꺼져 있다. 그러나 Animator body 또는 mesh 내부 위치 변화는 현재 Root observer가 보지 않으므로 애니메이션 쪽 원인도 배제할 수 없다.

## 11. 순간이동 관측 공백을 메우는 범위

다음 구현은 InfernoSpirit 전용 read-only 관측이다. 매 프레임의 값은 메모리에서 직전 값과 비교하지만, 정상 프레임에는 로그를 남기지 않는다. 예상 이동량 초과, 긴 프레임 또는 계층 간 위치 불일치가 발생한 사건에만 구조화된 단일 로그를 출력한다.

### 11.1 사건 로그 필드

- 역할·관점, 유닛/오브젝트 식별자
- frame, time, `deltaTime`
- Simulation Root의 이전/현재 위치와 delta
- Visual Root의 이전/현재 위치와 delta
- `Animator.bodyPosition`의 이전/현재 위치와 delta, 유효/미관측 사유
- `속도 × deltaTime + 허용치`로 계산한 예상 이동 allowance
- 현재 코드 경계에서 읽을 수 있는 movement command/segment/revision
- Animator current/next state와 transition 여부
- 현재 코드 경계에서 실제로 얻을 수 있는 network/replication 증거
- `authoritative-root-jump`, `visual-projection-jump`, `animator-body-jump`, `long-frame-catch-up`, `client-only-replication-gap`, `unavailable/ambiguous` 중 판정 가능한 분류

네트워크 receive timestamp처럼 현재 production 코드가 제공하는지 확인되지 않은 값은 필수 필드로 꾸미지 않는다. 제공 seam을 먼저 조사하고, 증거가 부족하면 `unavailable/ambiguous`로 남긴다.

### 11.2 bounded·수명 계약

- InfernoSpirit에만 활성화한다.
- 정상 프레임은 출력하지 않고 사건 발생 시 한 줄만 남긴다.
- 동일 유닛·frame·분류 exact key를 rate-limit하고 전체 저장량을 bounded로 제한한다.
- despawn, disable, observer retire에서 직전 프레임 상태를 제거한다.
- Transform, Animator, NetworkVariable, 이동 명령과 복제 상태를 읽기만 하며 수정하지 않는다.
- 개발 빌드 진단 경계를 사용하고 운영 로그로 승격하지 않는다.

이는 `GameSystemRules_Units.md`의 `U-MOV-PHASE`, `U-MOV-ALIGN`, `U-MOV-REPATH`, `GameSystemRules_UnitCombatSynchronization.md`의 `NET-ROOT-001~003`, `NET-FACING-001` 및 `LogRules.md` 1.5·1.7·1.14 금지 사항 8을 따른다.

### 11.3 이번 범위의 비범위

- 공격 시작 회전 또는 Attack clip orientation 수정
- Walk/Attack clip 및 import 설정 수정
- NetworkTransform 보간 수정
- 성능/렉 수정
- 서버 이동·경로·repath 수정
- Testcase 및 QA 문서 작성

## 12. `-58°` Impact 단일 변수 통제 실험

### 12.1 변경 사실과 역사 보존

- 기존 `-45°` 수집 당시의 원본 표본과 §9 판정은 삭제하거나 새 값으로 덮어쓰지 않는다.
- 사용자가 Unity Inspector에서 InfernoSpirit Attack orientation을 `-45° → -58°`로 직접 변경하고 저장했다.
- 문서 갱신 시 디스크 `Assets/_Project/Animations/Units/InfernoSpirit/InfernoSpirit_Attack.anim`의 `m_OrientationOffsetY=-58`을 확인했다.
- 이 문서 작업에서는 코드와 애니메이션 에셋을 수정하지 않았다.

### 12.2 후보값 근거

`-45°` 상태의 `ImpactMarker`에서 상위 방향은 이미 타겟에 수렴했지만 Animator body가 Visual Root보다 양수 방향으로 남았다.

| 역할 | `-45°` Impact body↔Visual residual |
|---|---:|
| Host | 평균 `+12.47°`, 범위 `+11.80° ~ +14.18°` |
| Client | 평균 약 `+13.38°`, 범위 약 `+11.77° ~ +16.24°` |

두 역할의 평균 잔여량이 약 `+13°`이므로 orientation을 추가로 `-13°` 이동한 `-58°`가 단일 변수 후보가 된다. 이 산술은 Impact 고정 잔여 오차를 0에 가깝게 만드는 비교 가설일 뿐, 전체 공격 방향 문제의 완료 증거가 아니다.

### 12.3 검증 기대와 판정

- Host와 Client를 합산하지 않고 각각 비교한다.
- `ImpactMarker`의 `bodyFromVisualYaw`가 두 역할 모두 `0°` 근처로 수렴하는지 확인한다.
- 값이 음수로 넘어가면 과보정이다. 예를 들어 Host/Client가 일관되게 약 `-1°`로 overshoot하면 `-57°` 같은 후속 후보를 새 단일 변수 실험으로 정한다.
- 역할별 부호나 크기가 다르게 나오면 하나의 공통 orientation 값으로 성급히 확정하지 않고 관점/retarget 차이를 다시 분리한다.
- 같은 역할에서도 표본 분산이 커지거나 공격 구간에 따라 부호가 바뀌면 고정 offset만으로 해결할 수 없는 clip 곡선 문제로 판정한다.

### 12.4 해결로 간주하지 않는 범위

`-58°` 비교는 Impact의 고정 잔여 오차만 다룬다. `-45°` 실기에서 확인된 아래 AttackStart 문제는 그대로 별도 결함이다.

- Root/Visual 정렬 전 선행 Attack 표현: Client 최대 `41.83°`, Host 최대 `35.45°`
- AttackStart Animator body 오차: Client 최대 `62.46°`, Host 최대 `40.52°`

따라서 `-58°`에서 Impact가 0°에 가까워져도 AttackStart 정렬 전 선행 표현, 공격 시작 정책 또는 5° ready gate 문제가 해결됐다고 기록하지 않는다.

### 12.5 현재 진단 구현 상태

- motion-jump observer schema v11 구현은 완료됐다.
- Runtime/Editor C# 정적 컴파일 오류는 0건이다.
- motion-jump v11의 실제 게임 로그는 아직 수집하지 않았으므로 실기 판정은 미검증이다.
- 이번 비교에서 코드, 공격 시작 정책, Walk, NetworkTransform, 서버 이동, 성능 문제는 변경하지 않는다.

## 13. `-58°` 실기 결과와 motion-jump 원인 분리 (2026-09-21 갱신)

이번 실행은 Editor Host와 Android Client가 공통 `sharedSessionKey=d32663e1caed7fd16b38a4166bef66647bb50e6a28344db48b3706067028d9b1`을 사용한 실제 게임 생산 테스트다. 근거 로그는 다음 두 파일이다.

- Client: `Assets/_Project/Docs/_Logs/2026-09-20/23_50_logcat/RuntimeLog_device.txt`
- Host: `Assets/_Project/Docs/_Logs/_editor/2026-09-20/RuntimeLog.txt`

### 13.1 `-58°`는 개선이 아니라 악화

| Client 비교 | `-45°` | `-58°` | 판정 |
|---|---:|---:|---|
| `ImpactMarker.bodyFromVisualYaw` | `n=41`, 평균 `+13.370°`, 범위 `+11.766° ~ +15.940°` | `n=49`, signed/abs 평균 `+28.343°`, 범위 `+24.778° ~ +30.883°`, `|yaw|<=8°` 0건 | 악화 |
| `AttackStartPose.bodyFromVisualYaw` 평균 | `+19.333°` | `+25.199°` | 악화 |

따라서 `-58°`는 최종 보정값이 아니다. 다만 production asset과 self-validation 기대값은 현재 `-58°`인 상태다. 이번 이동 교정 Task에서는 방향 값을 다시 바꾸지 않으며, 이동 결함과 분리된 후속 회전 교정에서 사용자 승인 아래 한 변수씩 다룬다.

### 13.2 motion-jump v11 실측

| 항목 | Client | Host |
|---|---:|---:|
| 사건 수 | 72 | 72 |
| 분류 | `animator-body-jump` 61, `unavailable-ambiguous` 11 | `animator-body-jump` 72 |
| 유닛/제한 | 9유닛 × 각 8건, overflow 9 | 9유닛 × 각 8건, overflow 9 |
| `longFrame > 0.1s` | 0 | 0 |
| raw frame | 평균 `0.0341s`, 최대 `0.0602s` | 평균 `0.0198s`, 최대 `0.0389s` |
| Simulation/Visual 최대 delta | `0.2023 / 0.2023` | `0.0309 / 0.0309` |
| body delta | 평균 `0.1442`, 최대 `0.4923` | 초기화 오탐 9건 제외 평균 `0.1501`, 최대 `4.7021` |

Host의 72건 중 9건은 각 유닛의 첫 `Animator.bodyPosition=(0,0,0) → 실제 위치` 표본이다. 이는 관측 초기화 산물이며 gameplay 순간이동 9건으로 세지 않는다.

핵심 유효 사건은 Host unit 86의 `23:47:29.784`다. `phase=NoIntent`, Animator `81563449 → 1130333774` 전환에서 Simulation Root와 Visual Root delta는 모두 `0`인데 `Animator.bodyPosition`만 `4.7021` 이동했다. 이어진 Attack→Walk 전환에서도 body delta `0.2688`이 기록됐다. 큰 점프가 발생한 프레임은 장시간 frame hitch가 아니었다.

그러나 `Animator.bodyPosition`은 Humanoid가 계산한 몸 중심이지 화면 메시 자체의 위치가 아니다. 이 값만으로 visible mesh가 4.7021만큼 실제로 튀었다고 확정하지 않는다. 다음 Task에서는 hips/root bone 또는 `SkinnedMeshRenderer.bounds.center`처럼 실제 렌더 결과와 안정적으로 대응하는 presentation anchor를 먼저 선정해 같은 전환을 다시 측정한다.

### 13.3 현재 판정 경계

- Host Simulation Root 최대 delta `0.0309`는 권위 이동 점프 증거가 아니다. 서버 경로·속도·repath는 이번 결과로 수정하지 않는다.
- Client Simulation/Visual 최대 delta `0.2023`은 작은 화면 snap 가능성을 남기지만, 현재 Client observer는 같은 사건의 Host 표본을 자동 결합하지 못해 `unavailable-ambiguous`다. 이를 `client-only-replication-gap`으로 확정해서 기록하지 않는다.
- 사건 프레임의 `longFrame > 0.1s`는 0건이므로 큰 body 점프를 장시간 프레임 정지로 설명할 수 없다. 반면 Client raw frame 평균 `0.0341s`, 최대 `0.0602s`이므로 전역 성능 PASS도 아니다.
- Host/Client Root Pose `summary-END`는 PASS, errors/drop은 0이다. 공격 결과 표현도 Client `expectedVisual=417`, `presentationEmits=417`, failures 0이다. 이는 Root/표현 구조 회귀가 없다는 뜻이며 이동 순간이동 해소 PASS가 아니다.
- 8건 cap 때문에 각 유닛의 후반 전환이 잘릴 수 있다. 다음 관측은 phase-aware budget 또는 첫 severe event 보존 정책을 사용해야 한다.

이 결과에 따라 이동 문제는 새 Task `Assets/_Project/Docs/_Tasks/2026-09-21/02_42_infernospirit-movement-jump-correction/`로 이관한다. 상태는 **실기 FAIL, 교정 구현 전**이다.

## 14. 추가 직접 검증: motion-jump 진단 자체의 Android 경고 폭주

기기 로그를 Unity 경고 원문과 스택별로 다시 집계한 결과, motion-jump v11이 테스트 환경을 오염시킨 사실이 확인됐다.

| 직접 집계 항목 | 건수 |
|---|---:|
| `Setting and getting Body Position/Rotation ... should only be done in OnAnimatorIK or OnStateIK` | **27,727** |
| `UnitView:ObserveInfernoMotionJumpFrame()` 스택 | **27,323** |
| `UnityEngine.Animator:get_bodyPosition()` 스택 | **27,314** |

- 첫 경고는 `23:45:39.510`이며 이후 매 프레임 수준으로 반복됐다.
- 실제 호출 경로는 `UnitView.LateUpdate()`가 매 프레임 `ObserveInfernoMotionJumpFrame()`을 호출하고, 그 안에서 `_animator.bodyPosition`을 읽는 경로다.
- Unity는 body position/rotation과 IK 관련 접근을 `OnAnimatorIK` 또는 `OnStateIK` 밖에서 수행하지 말라고 명시적으로 경고했다. 따라서 `LateUpdate`의 읽기 자체가 경고 원인이다.
- 이 경고·스택 폭주는 CPU와 Android 로그 출력 비용을 추가하고 Client update 처리가 몰리는 현상을 크게 악화시켰을 가능성이 높다.
- 다만 **성능 영향은 강한 가능성**이며, 이번 렉이나 Client delta `0.2023`의 단독 원인으로 확정하지 않는다. 경고 0건 상태로 다시 수집하기 전에는 네트워크 gap 크기를 독립 결함으로 판정할 수 없다.

따라서 `Animator.bodyPosition`은 다음 교정에서 완전히 제거한다. 실제 렌더 기준점은 초기화 때 캐시한 `SkinnedMeshRenderer.rootBone.position`을 주 값으로 사용하고, `SkinnedMeshRenderer.bounds.center`는 root bone 미가용 또는 대조가 필요한 경우의 보조 증거로만 사용한다. 첫 유효 값은 baseline만 만들고 사건을 발행하지 않는다. Android의 해당 Unity 경고 **0건**이 새 필수 gate다.

이 변경은 검증기를 더 만드는 작업이 아니다. 진단 코드가 실제 테스트 기기의 로그와 프레임 처리를 오염시켜 렉 판단을 방해한 결함을 제거하는 **실제 기능 교정**이다. NetworkTransform 또는 Client 보간 변경은 이 경고를 제거한 재빌드·재수집에서도 root gap이 남을 때만 검토한다.
