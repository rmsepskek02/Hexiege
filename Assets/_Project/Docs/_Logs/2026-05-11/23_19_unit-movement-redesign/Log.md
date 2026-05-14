# QA-Fix 반복 로그 — unit-movement-redesign

---

## Round 1 — 2026-05-12

### [QA] 발견된 문제

---

- **BUG-001: [REPATH] — 건물 생성/파괴 시 전투 추격 중인 유닛이 잠시 멈추는 현상**

  **자연어 설명**
  
  건물을 건설하거나 파괴하면 게임이 살아있는 모든 유닛의 경로를 다시 계산합니다(RepathAllAliveUnits). 이때 "전투 추격 중(적을 향해 직선으로 이동 중인)" 유닛도 경로 재계산 대상에 포함됩니다.

  기존에는 `IsInCombat()` 함수가 유닛이 "공격 애니메이션을 재생 중인 상태"만 전투로 인식하고, 공격 직전의 "추격 단계"는 전투로 인식하지 않았습니다. 이 때문에 추격 중인 유닛에게도 경로 재계산이 적용되어 기존 추격 코루틴이 갑자기 중단되고 새 이동 코루틴이 시작됩니다. 이 전환 과정에서 유닛이 한 프레임 이상 멈추는 현상이 발생합니다.

  - 관련 파일:
    - `Bootstrap/GameBootstrapper.cs` — `RepathAllAliveUnits()` (건물 변화 시 전 유닛 repath 트리거)
    - `Presentation/Unit/UnitView.cs` — `IsInCombat()` (전투 판정 기준), `OnPathInvalidated()` (repath 처리), `EnterCombatPursuitV3()` (추격 코루틴)
  
  - 원인 분석:
    `IsInCombat()` 함수가 `_combatTargetTransform != null`만 검사합니다. 그런데 `_combatTargetTransform`은 유닛이 실제로 "공격"을 시작해야 설정됩니다(`StartCombatAnimation` 호출 시). 적을 감지하고 추격 중인 단계(`EnterCombatPursuitV3` 실행 중, 아직 공격 사거리에 미도달)에서는 `_combatTargetTransform == null`이어서 `IsInCombat()` = false가 됩니다. 이 상태에서 건물 변화가 발생하면 `OnPathInvalidated` 조건을 통과해 추격 코루틴이 끊깁니다.

  - 수정 방향:
    `EnterCombatPursuitV3` 시작 시 `_isInCombatPursuit = true`를 설정하고, 종료 시 `false`로 되돌리는 bool 필드를 추가합니다. `IsInCombat()`을 `_combatTargetTransform != null || _isInCombatPursuit`로 수정하여 추격 단계도 전투 중으로 판정하도록 합니다.

---

- **BUG-002: [MOVEMENT] — 타겟 제거 후 권총병이 텔레포트 수준으로 빠르게 이동하는 현상**

  **자연어 설명**
  
  권총병(Pistoleer)이 적을 공격해서 제거한 직후, 이동 속도(0.5 칸/초)를 훨씬 초과하는 빠른 속도로 이동하거나 순간이동처럼 보이는 현상이 발생합니다. 이 현상은 타겟 제거 직후에만 나타나며 이동 중에는 정상 속도를 유지합니다.

  모든 상황에서 유닛은 항상 유닛 스탯에 설정된 이동 속도로만 움직여야 합니다.

  - 관련 파일:
    - `Presentation/Unit/UnitView.cs` — `ResumeFromForwardTileV3()` (타겟 제거 후 A* 재개 처리), `EnterCombatPursuitV3()` (추격 이동)
    - `Application/UseCases/UnitMovementUseCase.cs` — `FindForwardClosestTile()` (재개 타일 탐색)
  
  - 원인 분석:
    현재 권총병의 감지 사거리(DetectRange)와 공격 사거리(AttackRange) 모두 1.0 타일로 동일하게 설정되어 있습니다. 새 규칙상 DetectRange는 AttackRange보다 커야 합니다.

    타겟 제거 후 `ResumeFromForwardTileV3()`가 호출되면:
    1. 현재 물리적 위치 기준으로 "앞쪽 가장 가까운 타일"을 탐색합니다
    2. `ProcessStep()`으로 도메인 위치를 해당 타일로 강제 업데이트합니다 (추격 중 건너뛴 다수 타일을 한번에 점프)
    3. `transform.position`을 해당 타일 중심으로 즉시 스냅합니다

    이 스냅 이후의 Lerp 이동 속도 자체는 정상이지만, 스냅 직전/직후의 위치 변화가 시각적으로 순간이동처럼 보일 수 있습니다. 또한 추격 중 `_unitData.Position`이 오랫동안 갱신되지 않아서(ProcessStep이 A* 이동 단계에서만 호출됨) 스냅 시 예기치 않게 먼 거리를 이동할 수 있습니다.

    정확한 원인 파악을 위해 런타임 로그 추가가 필요합니다.

  - 수정 방향:
    1. **런타임 로그 추가**: `ResumeFromForwardTileV3()` 내에서 스냅 전후 위치, forwardTile, 이전 도메인 위치를 로그로 기록하여 실제 위치 변화를 파악합니다.
    2. **Inspector 수정**: 권총병(Pistoleer), 돌격소총병(Assault), 저격총병(Sniper), InfernoSpirit, FoxMagician의 DetectRange를 AttackRange보다 크게 설정합니다. (현재 동일 → 새 규칙 위반)
    3. 런타임 로그 분석 후 스냅 위치가 잘못됐다면 `FindForwardClosestTile` 또는 `ResumeFromForwardTileV3` 로직을 보완합니다.

---

### [DEV] 수정 내용

- BUG-001:
  - `Presentation/Unit/UnitView.cs` — `IsInCombat()`이 `_combatTargetTransform != null || _isInCombatPursuit`를 반환하도록 수정해 추격 단계도 전투 상태로 판정. `EnterCombatPursuitV3()` 진입 직후 `_isInCombatPursuit = true` 설정, 모든 `yield break` 경로(NullDependency, FindNearestEnemyInDetectRangeNull, TargetDestroyedNoMore, EnemyEscapedDetect, !_unitData.IsAlive, NoEnemyInDetectRange, FindNearestNull) 직전과 while 루프 자연 종료 후 함수 끝에 `_isInCombatPursuit = false` 명시적 리셋 추가(코루틴 try/finally 미지원 대응). `OnPathInvalidated()`의 `REPATH_SKIP_COMBAT` 로그에 `reason=InCombat isPursuit={_isInCombatPursuit}` 진단 정보 추가.

- BUG-002:
  - `Presentation/Unit/UnitView.cs` — `EnterCombatPursuitV3()` TargetDestroyedNoMore 경로에 `PURSUIT_END_TARGET_DEAD` 로그 추가(unitPos/domainPos/targetId). `ResumeFromForwardTileV3()`의 `ProcessStep` 호출 직전에 `RESUME_DOMAIN_JUMP` 로그(from/to/tileDist) 추가, `transform.position = forwardView` 직전에 `RESUME_SNAP` 로그(domainPosBefore/forwardTile/transformPosBefore/transformPosAfter/snapDistSq) 추가. 코드 동작 변경 없이 진단 로그만 추가.

- MovementLogger 경로:
  - `Application/Services/MovementLogger.cs` — `LogRelativePath` 상수를 기존 `2026-05-07/19_43_movement-combat-full-rewrite/RuntimeLog.txt` → `2026-05-11/23_19_unit-movement-redesign/RuntimeLog.txt`로 변경(현재 작업 폴더 반영).

---

## Round 2 — 2026-05-13

### [QA] 발견된 문제

- **BUG-002: [SNAP→WALK] — 전투 종료 후 유닛이 순간이동하는 현상**

  **자연어 설명**

  Round 1 진단 로그(`RESUME_SNAP`)로 확인된 실제 원인은 다음과 같습니다.

  전투가 종료되어 A* 이동을 재개할 때, `ResumeFromForwardTileV3()`는 "앞쪽 가장 가까운 타일"(forwardTile)을 찾은 직후 `transform.position`을 그 타일 중심으로 **즉시 스냅**했습니다. 추격 중에는 유닛이 적 근처(공격 슬롯)까지 물리적으로 이동했지만 도메인 위치(`_unitData.Position`)는 갱신되지 않으므로, forwardTile은 추격 전 타일에서 한 칸 앞쪽이 되고, 결과적으로 공격 슬롯 → forwardTile 중심까지의 거리(약 1타일)를 한 프레임 안에 점프하는 시각적 순간이동이 발생했습니다.

  이는 **규칙 9(A* 이동 재개 방식)** 위반입니다. 규칙 9는 현재 월드 위치 기준 앞쪽 타일 중심까지 "이동한 뒤" A*를 재개하라고 명시하고 있으며, 즉시 스냅이 아니라 동일 이동 속도로 걸어가야 합니다.

  - 관련 파일: `Presentation/Unit/UnitView.cs` — `ResumeFromForwardTileV3()`, `MoveAlongPathV3()`
  - 원인 분석: 규칙 9 위반. `transform.position = forwardView` 즉시 스냅으로 구현하여 1타일 순간이동 발생.
  - 수정 방향: 즉시 스냅 제거 → Lerp로 걸어서 이동. forwardTile은 호출 측에서 1회만 계산해 함수에 파라미터로 전달(이중 `FindForwardClosestTile` 호출 시 한 타일 더 앞쪽이 선택되어 도메인-뷰 좌표가 다시 어긋나는 부작용 방지).

### [DEV] 수정 내용

- `Presentation/Unit/UnitView.cs` — `ResumeFromForwardTileV3(HexCoord finalTarget)` → `ResumeFromForwardTileV3(HexCoord finalTarget, HexCoord forwardTile)`로 시그니처 변경. 함수 내부의 `FindForwardClosestTile` 호출과 `transform.position = forwardView` 즉시 스냅, 그리고 `RESUME_SNAP` 로그 블록을 제거. 도메인 갱신(`ProcessStep`), Walk 애니메이션 재개, `RequestMove`로 새 path 발급 로직은 그대로 유지. `RESUME_DOMAIN_JUMP` 로그도 유지(추격 중 다중 타일 점프 진단에 필요).

- `Presentation/Unit/UnitView.cs` — `MoveAlongPathV3()` 내 전투 종료 직후 흐름을 다음 5단계로 재구성.
  1. `ViewConverter.FromView(transform.position)`로 도메인 위치를 역산한 뒤 `FindForwardClosestTile`을 **1회만** 호출하여 `forwardTile`을 결정.
  2. `forwardTile`의 도메인 월드 좌표를 `ViewConverter.ToView` + `UnitYOffset`으로 변환한 `alignView`를 정렬 목표로 설정.
  3. `alignDist = Vector3.Distance(transform.position, alignView)`. 동일 이동 속도(규칙 5)를 보장하기 위해 `alignDuration = (alignDist / HexMetrics.TileHeight) * moveSeconds`로 환산. `alignDuration <= 0.0001f`이면 Lerp 생략.
  4. 기존 A* Lerp와 동일한 구조의 while 루프로 `Vector3.Lerp(alignFromPos, alignView, t)` 진행. 매 프레임 `HasEnemyInDetectRange` 체크 → 감지 시 `alignInterruptedByCombat = true`로 break. 사망(`!_unitData.IsAlive`) 시 자연스럽게 while 종료.
  5. 정렬 Lerp 종료 후 분기 — `alignInterruptedByCombat`이면 `interruptedByCombat = true`로 설정 후 외부 for 루프 break(같은 path 유지 → 다음 사이클에서 detect 체크가 다시 추격으로 전환). 정상 종료면 `transform.position = alignView`로 최종 스냅한 뒤 `ResumeFromForwardTileV3(finalTarget, forwardTile)` 호출. 그 외 후속 처리(needRepath/interruptedByCombat 설정, RESUME_FAILED 처리)는 기존과 동일.

- 진단 로그 추가:
  - `RESUME_ALIGN_START` — forwardTile, alignDist, alignDuration 기록.
  - `RESUME_ALIGN_INTERRUPT` — 정렬 Lerp 도중 새 적 감지 시 enemyId/isUnit/currentPos 기록.
  - `RESUME_ALIGN_END` — 정렬 Lerp 정상 종료 시 forwardTile/pos 기록.

- 영향 범위:
  - 규칙 9 충족: 재개 시 시각적 순간이동 제거, 정렬도 동일 이동 속도로 "걸어서" 처리됨.
  - BUG-001(`_isInCombatPursuit` 플래그) 관련 코드는 일절 수정하지 않음.
  - `RESUME_DOMAIN_JUMP` 로그는 그대로 유지되어 추격 중 도메인 점프 거리 자체는 계속 관찰 가능.
