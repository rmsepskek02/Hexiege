# InfernoSpirit 공격 몸 방향 증거 구현 계획

이번 계획은 처음에는 `InfernoSpirit_Attack.anim`의 `m_OrientationOffsetY=-45`를 유지한 채 공격 시작·Impact·종료의 실제 Animator 몸 방향을 수집했고, 그 실측을 보존한 상태에서 사용자가 저장한 `-58°` 한 변수만 후속 비교한다. 진단 코드는 서버 판정이나 화면 방향을 수정하지 않는 read-only observer여야 한다.

**현행 상태 (2026-09-21):** `-58°` 방향 비교와 motion-jump v11 실기가 모두 FAIL했다. motion-jump의 `LateUpdate → Animator.bodyPosition` 접근은 Android Unity 경고 27,727건을 유발해 테스트 환경도 오염시켰다. 방향 값은 현재 production/validator의 `-58°`를 임의 변경하지 않고 별도 회전 교정으로 보류하며, 이동 결함은 새 Task `2026-09-21/02_42_infernospirit-movement-jump-correction`에서 교정 구현 전 상태로 이어간다.

**계획 상태:** 기존 `-45°` facing evidence 실기 FAIL·원인 분리 완료 / motion-jump v11 구현·Runtime/Editor 정적 컴파일 0·실기 미검증 / `-58°` 단일 변수 실기 비교 승인·빌드 전

**기존 로직 제거:** 없음. 검증 전 기존 공격·회전·표현·로그 경로를 비활성화하거나 삭제하지 않는다.

## 1. 완료 조건

1. InfernoSpirit Attack 포즈가 실제 평가된 시작, `OnAttackHit`, 종료 직전의 세 사건이 각각 최대 한 번 기록된다.
2. 각 기록에 역할/관점, 유닛, 공격자, 타겟, 회차, Revision, Animator 상태와 normalized time이 포함된다.
3. Simulation Root, Visual Root, Animator body, 타겟 방향과 XZ signed yaw가 같은 표본에 포함된다.
4. Animator body를 유효하게 읽지 못하면 대체값을 정상값처럼 기록하지 않고 명시적 미관측 사유를 남긴다.
5. 매 프레임 로그와 무제한 저장소가 없고, 중복 사건은 억제되며 공격 수명 종료 시 관측 상태가 retire된다.
6. 진단은 어떤 Transform, Animator 상태, NetworkVariable, 공격 회차, 타겟 또는 피해 결과에도 쓰지 않는다.
7. signed-yaw 계산·무효 입력·세 사건 분류·중복 억제가 static self-validation에서 fail-closed로 검증된다.
8. `Run Unit Action Self Validation`과 `Self Validate Unit Root Pose Cross Audit`이 새 실행에서 모두 PASS한 뒤 Android Build And Run을 시작한다.
9. 자동 검증 PASS와 빌드 시작을 실기 방향 PASS로 과대 기록하지 않는다.

## 2. 구현 단계

### A. Animator 몸 방향 one-shot 수집 훅

**수정 파일:** `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

- InfernoSpirit의 공격 표현에만 관측을 활성화해 이번 진단의 로그 양을 제한한다.
- 공격 시작 호출 직후 즉시 몸 방향을 읽지 않는다. 시작 표본을 pending으로 예약하고 Animator가 Attack 포즈를 평가한 다음 프레임 후반에 `AttackStartPose`를 한 번만 수집한다.
- `OnAttackHit`의 기존 marker 처리 흐름에서 권위나 emitter 순서를 바꾸지 않고 `ImpactMarker` 표본을 읽기 전용으로 추가한다.
- Attack 종료 전환으로 현재 포즈와 타겟을 지우기 직전에 `AttackEndPose` 표본을 수집한다.
- 표본은 기존 `PresentationTransform`과 Animator의 실제 body rotation을 읽되, Humanoid body 정보가 유효하지 않으면 `Unavailable`로 전달한다.
- 타겟은 현재 View 검색으로 회차 사실을 다시 만들지 않는다. 해당 시점에 기존 표현 경로가 보유한 타겟 ID/Transform과 공격 scope 식별자를 함께 넘기고, 값이 없으면 없음을 보존한다.
- 시작 pending은 취소·비활성화·종료에서 반드시 폐기해 다음 공격의 표본으로 재사용하지 않는다.

**규칙 근거:** Units 규칙 15·17·22, `NET-AUTH-001`, `NET-ROOT-001~003`, `NET-PRESENT-001`.

### B. bounded 방향 증거와 순수 분류

**수정 파일:** `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`

- `AttackStartPose`, `ImpactMarker`, `AttackEndPose`를 명시적 사건으로 받는 몸 방향 관측 진입점을 추가한다.
- XZ 평면에서 target direction을 기준으로 Simulation Root, Visual Root, Animator body의 signed yaw를 계산한다.
- body↔Visual Root signed yaw를 별도로 계산해 상위 투영은 정상인데 애니메이션만 회전했는지 판별할 수 있게 한다.
- 0 길이·NaN·Infinity 방향, 유효하지 않은 body evidence, 식별 불가 상태를 정상 각도 `0`으로 바꾸지 않는다.
- 기존 공격자별 bounded 저장·중복 키·retire 정책을 재사용한다. 새 전역 무제한 컬렉션을 만들지 않는다.
- 동일 `(attacker, sequence, revision, event)`는 최대 한 번만 기록한다. provisional sequence `0`은 별도 상태로 표시하며 커밋 회차와 같은 증거로 합치지 않는다.
- 정상 표본은 구조화된 단일 개발 로그로 남긴다. 실패/미관측 이유는 분리된 필드로 기록하고 같은 사건을 여러 줄로 중복 출력하지 않는다.
- observer는 방향을 읽고 분류할 뿐 `Transform.rotation`, `Animator`, NetworkTransform, 타겟 또는 공격 결과를 수정하지 않는다.

**규칙 근거:** Units 규칙 15·22, `NET-ROOT-001~003`, `NET-FACING-002`, `NET-PRESENT-003`, `LogRules.md` 1.5·1.7·1.14 금지 사항 8.

### C. static self-validation

**수정 파일:** `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

다음 순수 사례를 기존 Unit Action self-validation에 추가한다.

1. 타겟과 Root/VisualRoot/body가 모두 같은 방향이면 세 yaw가 허용 오차 안에서 0이다.
2. Root/VisualRoot가 타겟과 같고 body만 +45° 또는 -45°이면 body yaw와 body↔VisualRoot yaw의 부호·크기가 정확히 구분된다.
3. 타겟 방향이 0이거나 입력에 NaN/Infinity가 있으면 유효 증거로 분류하지 않는다.
4. body evidence가 unavailable이면 Root/VisualRoot 결과와 body 결과를 섞지 않는다.
5. 세 사건 이름 외의 값은 거부하고 동일 exact key 중복은 한 번만 수락한다.
6. production `InfernoSpirit_Attack.anim`의 `m_OrientationOffsetY=-45`가 유지되는지 fail-closed로 확인해 진단 도중 무단 보정 변경을 막는다.

정적 검증은 실제 Animator 포즈를 재현했다고 주장하지 않는다. 계산·분류·production 전제만 검증하며 실제 몸 방향은 사용자 실기 로그에서 판정한다.

**규칙 근거:** Units 규칙 15·17·22, `NET-ROOT-002~004`, `NET-PRESENT-001/003`.

## 3. 예상 변경 파일

### 작업 문서

- `Assets/_Project/Docs/_Tasks/2026-09-20/16_34_infernospirit-attack-facing-evidence/Research.md`
- `Assets/_Project/Docs/_Tasks/2026-09-20/16_34_infernospirit-attack-facing-evidence/Plan.md`

### 구현

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

### 변경하지 않음

- `InfernoSpirit_Attack.anim` (`m_OrientationOffsetY=-45` 유지)
- InfernoSpirit Animator Controller와 Blue/Red 프리팹
- `VisualRootProjector.cs`, NetworkTransform 설정
- 서버 타겟·회전·피해·DoT writer와 C2/C3 결과/표현 writer
- 규칙 원본, 상태 문서, 에이전트 메모리
- `Testcase.md`

## 4. 로그 판정표

| 실측 패턴 | 판정 | 다음 교정 후보 |
|---|---|---|
| Root yaw ≤ 5°, VisualRoot yaw ≤ 5°, body yaw가 세 사건 모두 비슷한 상수 | Animator/클립의 고정 축 오프셋 | 실측 상수에 근거한 orientation 보정 Task |
| Root/VisualRoot 정상, body yaw가 특정 사건에서만 크게 변함 | Attack clip 내부 구간 회전 곡선 | 클립 회전 곡선 또는 Avatar 적용 방식 교정 Task |
| Host는 정상, Client 또는 특정 팀 관점에서만 약 180°/고정 오차 | 관점 투영과 Animator 방향의 결합 오류 | Visual Root 팀 변환 경계 교정 Task |
| Root·VisualRoot·body가 함께 타겟과 어긋남 | 상위 타겟/정렬/복제 문제 | 회차 타겟과 SimulationFacing 수명 교정 Task |
| 방향은 서로 일치하지만 target ID/revision이 사건 사이에서 바뀜 | 타겟 표현 수명 또는 회차 결합 문제 | exact scope/target handoff 교정 Task |
| `bodyEvidenceValid=false`만 존재 | 실제 몸 방향 미관측 | rig 유형에 맞는 명시적 렌더링 기준 Transform 설계 후 재측정 |

하나의 표본만으로 고정 보정값을 확정하지 않는다. 동일 역할·팀·유닛에서 여러 공격 회차와 타겟 방향을 확보하고 같은 패턴이 반복되는지 확인한다.

## 5. 검증 순서

1. C# 정적 컴파일 오류가 없는지 확인한다.
2. `python3 Tools/check_docs.py`로 문서 정합성 0건을 확인한다.
3. Unity 메뉴 `Hexiege → Combat → Run Unit Action Self Validation`을 실행해 새 몸 방향 계산/분류 gate를 포함한 PASS를 확인한다.
4. Unity 메뉴 `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit`을 실행해 기존 Root/Visual Root 계약 PASS를 확인한다.
5. 두 메뉴의 **이번 실행 PASS**가 모두 확인된 뒤 Android Build And Run을 시작한다.
6. 빌드 완료 및 실기 판정은 사용자 소유로 남긴다.

## 6. 사용자 실제 생산 테스트

- 테스트 맵의 임의 생성기가 아니라 실제 게임 생산 경로로 InfernoSpirit를 생산한다.
- Host와 Client 양쪽에서 공격 시작, 타격 동작, 종료를 육안으로 관찰한다.
- 가능하면 서로 다른 방향의 타겟, 타겟 사망 후 교체, 연속 공격을 포함한다.
- 비교 기준으로 기존 정상 근거리 유닛 하나도 같은 경기에서 생산하되, 이번 몸 방향 로그 판정의 주 대상은 InfernoSpirit로 제한한다.
- 여러 공격 회차가 기록된 뒤 Editor와 기기 로그를 저장한다.
- 저장 로그에서 세 사건이 모두 실제로 발생했는지 먼저 확인한 뒤 §4 판정표를 적용한다. 오류 로그가 없다는 사실만으로 방향 PASS를 선언하지 않는다.

## 7. 위험과 대응

| 위험 | 대응 |
|---|---|
| Start 호출 직후 이전 포즈를 읽음 | Animator 평가 후 one-shot pending을 소비한다 |
| Generic rig에서 `Animator.bodyRotation`이 유효하지 않음 | 대체값을 만들지 않고 `Unavailable`로 남긴 뒤 렌더링 기준 Transform 설계를 후속으로 분리한다 |
| provisional 시작의 sequence 0을 정규 회차로 오인 | provisional 필드와 sequence 0을 보존하고 커밋 회차와 결합하지 않는다 |
| 로그가 공격 루프마다 누적되어 렉을 키움 | InfernoSpirit 전용·세 사건 한정·exact-key dedupe·기존 bounded retire를 적용한다 |
| 진단 코드가 게임 방향을 바꿈 | observer를 read-only로 유지하고 쓰기 호출이 없는지 정적 검토한다 |
| 자동 검증 PASS를 실기 PASS로 오인 | 정적 계산/구조 PASS와 사용자 렌더링 판정을 문서와 보고에서 분리한다 |

## 8. 비범위

- 육안 추측에 따른 추가 각도 보정
- 전체 유닛 방향 일괄 수정
- 성능/렉 결함 분석
- InfernoSpirit 피해·DoT·타임라인 변경
- C3 profile 지원 상태 변경
- Testcase 및 QA 실행

## 9. 기존 계획 실행 결과와 판정

기존 A~C 단계의 InfernoSpirit 전용 `animated-facing` 구현과 self-validation이 완료되었고, 두 Unity 검증 메뉴 PASS 뒤 Android 빌드 및 실제 게임 생산 테스트가 진행되었다. 최신 공통 세션 `aa5875a11546bdb8183d87b7c22aec276396670aa12029df35c075ac7b54bcde`에서 다음이 확인됐다.

- 방향 유효 표본: Client Start 49 / Impact 28 / End 12, Host Start 52 / Impact 47.
- AttackStartPose의 Root/Visual target yaw 절대값은 평균 Client 8.30°·Host 8.18°, 최대 Client 41.83°·Host 35.45°다. Animator body 최대 오차는 Client 62.46°·Host 40.52°다.
- ImpactMarker에서는 Root/Visual이 Host 0.00°, Client 평균 0.07°·최대 0.47°까지 수렴했지만 body↔Visual 잔여 yaw는 Host 평균 12.47°, Client 평균 약 13.38°다.
- 따라서 공격 시작 전 정렬되지 않은 Attack 선행 표현과 Impact의 Animator 몸체 잔여 회전은 서로 다른 교정 대상이다.
- `m_OrientationOffsetY=-45`는 유지한다. 이번 Task에서 `-57/-58` 같은 추정 보정값을 적용하지 않는다.
- 이동 구조 실패 계수는 0이지만 기존 observer가 연속 프레임 위치 delta를 기록하지 않아 사용자가 본 순간이동은 판정 불가다.

기존 계획의 “증거 수집” 목적은 달성됐지만 사용자 실기 결과는 방향 PASS가 아니다. Task는 순간이동 관측 범위를 추가한 OPEN 상태로 유지한다.

## 10. 추가 구현 — InfernoSpirit motion-jump evidence

이번 추가 구현은 순간이동을 바로 고치는 작업이 아니다. 화면에서 점프가 발생한 정확한 프레임의 세 계층 위치와 프레임 시간을 한 사건으로 묶어, 다음 교정이 서버 이동·표현 계층·애니메이션·클라이언트 복제·성능 중 어디를 다뤄야 하는지 결정하는 작업이다.

### D. 프레임 간 위치 표본 유지와 사건 감지

**예상 수정 파일:** `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

- InfernoSpirit View에만 직전 프레임의 Simulation Root, Visual Root, `Animator.bodyPosition`, frame/time을 bounded 상태로 유지한다.
- 매 프레임 값을 비교하되 로그를 매 프레임 출력하지 않는다.
- 현재 유효 이동 속도와 `deltaTime`으로 `speed × deltaTime + tolerance` allowance를 계산하고, Root 또는 body delta가 allowance를 넘거나 계층 간 delta가 갈라지는 경우만 사건 후보로 만든다.
- 긴 `deltaTime`은 별도 문맥으로 보존해 정상 시간의 위치 점프와 프레임 정지 뒤 catch-up을 구분한다.
- `Animator.bodyPosition`을 유효하게 읽을 수 없으면 Root 값으로 대체하지 않고 unavailable 사유를 남긴다.
- 비활성화·despawn·재사용 시 이전 표본을 제거해 다른 생명주기의 위치를 연결하지 않는다.

**규칙 근거:** `U-MOV-PHASE`, `U-MOV-ALIGN`, `U-MOV-REPATH`, `NET-ROOT-001~003`, `NET-FACING-001`.

### E. bounded 사건 분류와 구조 로그

**예상 수정 파일:** `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs` 또는 조사 후 확인되는 기존 Unit Action 진단 observer의 단일 소유 지점

- 하나의 사건에 role/viewpoint, identity, frame/time/deltaTime, 세 계층의 prev/current/delta, expected allowance, movement command/segment/revision, Animator current/next/transition을 결합한다.
- 현재 production seam에서 실제로 읽을 수 있는 replication revision·도착 증거만 포함한다. receive timestamp를 제공하지 않는 경우 새 사실처럼 만들지 않고 가용성 조사 결과와 unavailable 상태를 기록한다.
- 다음 분류를 순수 판정으로 제공한다.
  - `authoritative-root-jump`: Simulation Root부터 allowance를 초과한다.
  - `visual-projection-jump`: Simulation Root는 정상인데 Visual Root만 초과하거나 두 계층 delta가 불일치한다.
  - `animator-body-jump`: 상위 두 Root는 정상인데 body만 초과한다.
  - `long-frame-catch-up`: 긴 frame과 큰 delta가 같은 사건에서 발생한다.
  - `client-only-replication-gap`: Host에는 대응 점프가 없고 Client 복제 증거에서 간격을 확인할 수 있을 때만 사용한다.
  - `unavailable/ambiguous`: 필요한 증거가 없거나 둘 이상의 원인을 분리할 수 없다.
- 동일 `(role, unit identity, lifecycle, frame, classification)` exact key는 한 번만 출력하고, 유닛별/경기별 상한과 overflow 요약을 둔다.
- 정상 프레임은 로그를 남기지 않으며 하나의 사건을 여러 계층에서 중복 출력하지 않는다.
- observer는 read-only이고 위치·Animator·이동·복제 상태를 수정하지 않는다.
- 진단은 개발 빌드 경계에 두고 임시 운영 로그로 남기지 않는다.

**규칙 근거:** `NET-AUTH-001`, `NET-ROOT-001~003`, `NET-FACING-001`, `LogRules.md` 1.5·1.7·1.14 금지 사항 8·9.

### F. motion-jump 순수 self-validation

**예상 수정 파일:** `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

1. allowance 이하의 정상 이동은 사건을 만들지 않는다.
2. Simulation Root부터 큰 delta가 나면 `authoritative-root-jump`다.
3. Root는 정상이고 Visual Root만 튀면 `visual-projection-jump`다.
4. 두 Root는 정상이고 body만 튀면 `animator-body-jump`다.
5. 긴 frame과 큰 delta가 함께 있으면 catch-up 문맥이 보존된다.
6. Client-only 분류는 필요한 peer/replication 증거가 있을 때만 허용한다.
7. body 또는 replication 증거가 없으면 0으로 꾸미지 않고 unavailable/ambiguous가 된다.
8. exact-key 중복, bounded 상한, overflow, disable/despawn/retire 후 상태 제거를 검증한다.
9. observer가 어떤 gameplay/Transform/Animator writer도 호출하지 않는 경계를 유지한다.

정적 검증은 실제 순간이동이 사라졌다고 판정하지 않는다. 실제 게임에서 사건 로그가 수집된 뒤에만 원인과 수정 범위를 선택한다.

## 11. 추가 구현 완료 조건

1. 정상 프레임 로그가 0이고, 임계 사건에만 `[UAS-DIAG][INFERNO-MOTION-JUMP]` 구조 로그가 한 줄 출력된다.
2. 세 위치 계층과 allowance, frame 시간, Animator 상태가 같은 사건에 결합된다.
3. 가용한 command/segment/revision과 replication 증거가 포함되며, 없는 값은 명시적으로 unavailable이다.
4. 여섯 분류가 증거 수준을 넘지 않고 fail-closed로 적용된다.
5. exact-key rate limit, bounded 상한, overflow 및 생명주기 retire가 동작한다.
6. read-only observer이고 gameplay 동작·애니메이션·복제·성능을 수정하지 않는다.
7. Runtime/Editor C# 정적 컴파일 오류가 0이다.
8. Unit Action Self Validation과 Unit Root Pose Cross Audit의 새 실행이 PASS한 뒤에만 빌드를 시작한다.
9. 자동 검증과 빌드 성공을 순간이동 수정 PASS로 기록하지 않는다.

## 12. 추가 구현 검증·사용자 실기 순서

1. 문서 정합성 검사를 통과한다.
2. motion-jump 순수 분류와 기존 facing 계약을 포함한 Unit Action Self Validation을 실행한다.
3. 기존 Root 투영 계약 회귀를 확인하기 위해 Unit Root Pose Cross Audit Self Validate를 실행한다.
4. 두 메뉴의 이번 실행 PASS 후 Android Build And Run을 시작한다.
5. 사용자는 실제 게임 생산 경로에서 InfernoSpirit 이동·추격·공격 진입을 반복하고, 순간이동을 본 경기의 Editor/기기 로그를 저장한다.
6. 로그에서 어느 계층이 먼저 allowance를 초과했는지 판정한 뒤에만 별도 수정 Task를 작성한다.

## 13. 추가 구현 비범위와 후속 회전 Task

이번 추가 구현에서는 다음을 수정하지 않는다.

- 공격 시작 회전과 provisional Attack 정책
- `InfernoSpirit_Attack.anim` 또는 `m_OrientationOffsetY=-45`
- Walk clip/import 설정
- NetworkTransform 보간
- 서버 이동·경로·repath
- 성능/렉
- Testcase 및 QA 실행

회전 교정은 별도 Task에서 진행한다.

1. visible Attack clip의 최초 시작을 기존 5° ready gate 뒤로 옮기되, 타겟 원자 전달과 커밋 전 Impact 억제 계약을 보존한다.
2. Impact의 약 12~14° body 잔여 오차가 clip `RootQ`/body 곡선인지 Humanoid retarget/import 결과인지 분리한 뒤 orientation 값을 확정한다.

## 14. 승인된 `-58°` 단일 변수 비교 계획

### 14.1 통제 변수와 근거

- 기준 표본은 `-45°` 상태의 Impact body↔Visual residual이다: Host 평균 `+12.47°`(`+11.80° ~ +14.18°`), Client 평균 약 `+13.38°`(약 `+11.77° ~ +16.24°`).
- 사용자는 Unity Inspector에서 orientation을 `-58°`로 직접 변경·저장했다. 디스크 `InfernoSpirit_Attack.anim`의 `m_OrientationOffsetY=-58`도 확인됐다.
- 약 `+13°`의 Impact 잔여량에 대응해 기존 값보다 `-13°` 추가한 후보이며, 비교 빌드에서는 이 값 외의 gameplay/animation/network 변수를 바꾸지 않는다.
- 기존 `-45°` 로그·통계·판정은 비교 기준이므로 삭제하거나 `-58°` 값으로 덮어쓰지 않는다.

### 14.2 빌드 전 production contract 동기화

**후속 구현 필요 파일:** `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

- 현재 production asset이 `-58°`이므로 self-validation의 InfernoSpirit orientation 기대값도 `-58°`로 동기화해야 한다.
- 이는 검증 상수를 임의 완화하는 작업이 아니라, 사용자가 승인하고 저장한 단일 변수 실험값과 production contract를 정확히 일치시키는 변경이다.
- 기대값을 바꾼 뒤에도 정확한 clip 연결과 단일 orientation 값 검증은 fail-closed로 유지한다.
- self-validation 코드가 아직 `-45°`를 기대하는 상태에서는 예상 실패를 무시하고 빌드하지 않는다. 구현 담당자가 `-58°`로 동기화하고 새 실행 PASS를 확인한 뒤 빌드한다.

**규칙 근거:** Units 규칙 15·17·22, `NET-ROOT-002~004`, `NET-PRESENT-001/003`.

### 14.3 비교 전 검증 순서

1. production contract 기대값을 `-58°`로 동기화한다.
2. Runtime/Editor C# 정적 컴파일 오류 0건을 확인한다.
3. `Hexiege → Combat → Run Unit Action Self Validation`의 새 실행 PASS를 확인한다.
4. `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit`의 새 실행 PASS를 확인한다.
5. 두 메뉴 PASS 뒤 Android Build And Run을 시작한다. 빌드 완료 확인은 사용자 소유다.

motion-jump v11 구현과 Runtime/Editor 정적 컴파일 0건은 이미 완료됐다. 다만 v11 실기 로그는 아직 없으므로 이번 빌드에서 방향 비교와 함께 관측할 수 있을 뿐, 빌드 전에 순간이동 수정 PASS로 간주하지 않는다.

### 14.4 사용자 실기 판정

| 결과 | 판정 | 다음 단계 |
|---|---|---|
| Host/Client Impact `bodyFromVisualYaw`가 모두 0° 근처로 수렴 | `-58°`가 Impact 고정 offset 후보로 지지됨 | 반복 표본의 평균·범위와 육안 접촉 방향을 확인한 뒤 최종값 판단 |
| 두 역할 모두 음수로 일관되게 overshoot | 과보정 | overshoot 크기만큼 되돌린 `-57°` 등의 새 단일 변수 후보를 별도 승인 후 비교 |
| Host와 Client의 부호·크기가 다름 | 역할/관점 차이 | 공통 offset 확정 보류, Visual Root/retarget 경계 재분리 |
| Impact 구간 내 분산이 크거나 부호가 변함 | 고정 offset으로 해결 불가 | clip 내부 회전 곡선/retarget 평가 조사 |

판정은 Host와 Client를 별도로 계산한다. 평균만 0에 가까워도 한 역할의 범위가 악화되면 PASS로 일반화하지 않는다.

### 14.5 변경하지 않는 영역

- 공격 시작 정책과 provisional Attack 표시 시점
- AttackStart의 정렬 전 선행 표현 문제
- Walk 상태·Walk clip·Animator 전이 정책
- NetworkTransform과 Visual Root 투영
- 서버 이동·경로·repath
- 성능/렉
- 피해·DoT·C2/C3 gameplay writer
- Testcase 및 QA 문서

특히 `-58°` Impact 비교는 기존 AttackStart 최대 Root/Visual `35~42°`, body 최대 `62°` 문제를 해결했다고 간주하지 않는다.

## 15. 실기 결과와 이동 교정 Task 이관 (2026-09-21 추가)

### 15.1 실기 판정

공통 세션 `d32663e1caed7fd16b38a4166bef66647bb50e6a28344db48b3706067028d9b1`에서 `-58°` Client Impact는 `n=49`, 평균 `+28.343°`, 범위 `+24.778° ~ +30.883°`로 확인됐다. 이전 `-45°`의 `n=41`, 평균 `+13.370°`, 범위 `+11.766° ~ +15.940°`보다 악화됐고 8° 이내 표본은 0건이다. AttackStart 평균도 `+19.333° → +25.199°`로 악화됐다. 따라서 §14의 수렴 기대는 충족되지 않았다.

motion-jump는 Client 72건(`animator-body-jump` 61, `unavailable-ambiguous` 11), Host 72건(`animator-body-jump` 72)이 기록됐다. Host 초기화 오탐 9건을 제외한 body delta는 평균 `0.1501`, 최대 `4.7021`이며, 최대 사건은 Walk→Attack 전환에서 Simulation/Visual delta가 모두 0인 상태였다. Client Simulation/Visual 최대 delta는 `0.2023`이지만 paired Host 자동 결합이 없어 복제 gap으로 확정하지 않는다. 양쪽 모두 `longFrame > 0.1s`는 0건이다.

Root Pose 양쪽 `summary-END` PASS·errors/drop 0과 공격 표현 `417/417`, failures 0은 구조 회귀가 없다는 보조 증거다. 순간이동과 `-58°` 방향은 실기 FAIL이므로 이 PASS를 전체 완료로 확대하지 않는다.

### 15.2 기존 진단 계획의 한계

`Animator.bodyPosition`은 Humanoid 몸 중심이고 visible mesh 위치와 동일하다는 계약이 없다. 따라서 `4.7021` 사건은 애니메이션 전환을 우선 조사하게 만드는 강한 증거이지만, 그 수치만으로 화면 메시 점프를 확정해 clip을 바로 수정할 수는 없다. 또한 유닛별 8건 cap이 초기화와 평상시 작은 사건에 먼저 소진되어 후반의 전환 사건을 놓칠 수 있다.

### 15.3 새 Task로 넘기는 교정 계약

1. 실제 렌더 기준점을 정하고 초기 `(0,0,0)` 표본을 제거한다.
2. Walk→Attack에서 Simulation/Visual Root가 allowance 이내인데 stable render anchor가 초과하면 실제 표현 점프로 실패시킨다.
3. 실제 render anchor 점프면 animation/presentation child 범위에서 clip body/root/retarget 불연속을 교정하고 `ApplyRootMotion=0`을 유지한다.
4. render anchor가 정상이고 Client root gap만 확인되면 exact paired Host와 Client receive/interpolation 증거를 먼저 확보한 뒤 표현 전용 보간 seam을 설계한다.
5. 서버 locomotion, 전체 NetworkTransform 보간 비활성화, Client Simulation Root writer, 속도·경로·repath·피해·회차·방향값 변경은 금지한다.
6. phase-aware budget 또는 첫 severe event 보존으로 실제 후반 전환 증거가 cap 뒤에 사라지지 않게 한다.

상세 Research/Plan은 `Assets/_Project/Docs/_Tasks/2026-09-21/02_42_infernospirit-movement-jump-correction/`가 소유한다. Testcase는 사용자 지시에 따라 작성하지 않는다.

## 16. 긴급 우선순위 정정: `Animator.bodyPosition` 진단 제거

기기 로그 직접 집계에서 Unity body position/rotation 경고 27,727건, `ObserveInfernoMotionJumpFrame` 스택 27,323건, `get_bodyPosition` 스택 27,314건이 확인됐다. 첫 경고는 `23:45:39.510`이며 `UnitView.LateUpdate`의 매 프레임 `_animator.bodyPosition` 읽기가 원인이다.

따라서 §15.3의 stable render anchor 추가보다 먼저 아래 실제 교정을 수행한다.

1. `UnitView`의 motion-jump 경로에서 `Animator.bodyPosition` 접근과 관련 previous/current 상태를 완전히 제거한다.
2. InfernoSpirit의 `SkinnedMeshRenderer`를 초기화 시 1회 탐색·캐시하고, `rootBone.position`을 stable rendered anchor의 주 값으로 사용한다.
3. `bounds.center`는 root bone이 없거나 주 값과 메시 외곽의 대조가 필요할 때만 보조 증거로 사용한다. 매 frame renderer 탐색·배열 생성은 금지한다.
4. 첫 유효 anchor는 baseline만 만들며 event와 per-unit budget을 소비하지 않는다.
5. Android 로그의 Unity body position/rotation 경고 0건을 필수 gate로 추가한다.
6. 경고 제거 후 같은 생산 테스트를 다시 수집한 뒤에도 Client root gap이 남을 때만 NetworkTransform/표현 보간 분기를 검토한다.

경고·스택 폭주가 CPU와 로그 출력 비용을 추가해 테스트 렉과 Client update 몰림을 크게 악화시켰을 가능성은 높지만 단독 원인으로 확정하지 않는다. 그래서 이번 Client `0.2023`만으로 NetworkTransform을 먼저 바꾸지 않는다.

이 작업은 검증기 보강이 아니라 진단 코드가 실제 테스트 환경을 망가뜨린 결함을 제거하는 기능 교정이다. 상세 파일·규칙·완료 gate는 새 correction Plan이 소유한다.
