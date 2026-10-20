# Game System Rules — 유닛

- **목적·답하는 질문:** 유닛의 이동·정렬·타겟·공격·피해·회복에서 무엇이 참이어야 하는지 판단한다.
- **포함:** 게임플레이 불변 조건, 특수 공격·힐러 예외, 타격 시간축·전투 표현의 동작 계약
- **제외·담당 문서:** 권위·복제·순서 역전은 [전투 동기화](GameSystemRules_UnitCombatSynchronization.md), 클래스 배치는 [TDD](../TechnicalDesignDocument.md), 수치 미러는 [StatsReference](../StatsReference.md), 개별 연결·수용은 [감사표](../Assets/UnitCombatAssetMatrix.md)
- **권위원본·주요 참조:** 게임플레이 규범 원본; 실제 수치는 Config·생산 선택 클립·소비 경로와 대조
- **갱신 조건·방법:** 승인된 불변 조건·예외가 바뀔 때 해당 본문을 갱신한다. fixture·SHA·컴파일·native·빌드·회차별 수용 원문은 [구현·검증 상세 이력](../History/ImplementationVerificationHistory.md#units-handoff)에 보존하고 규칙에 누적하지 않는다.

현재 개별 유닛 수용·미검증 범위는 [감사표](../Assets/UnitCombatAssetMatrix.md), 전체 미완 작업은 [ROADMAP](../ROADMAP.md)을 따른다. 2026-08-27 실패와 인계 원문은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-01)에 보존한다.

---

## 목차

아래 목차는 시스템과 안정 ID별 원본 위치를 안내한다. 괄호는 각 시스템이 담고 있는 규칙 번호 범위이며, **규칙 번호는 다른 문서들이 참조하므로 변경하지 않는다.**

- [유닛 이동 시스템](#유닛-이동-시스템) — 규칙 1~8 · 45(건물 경로 차단, 2026-08-26 맵 문서에서 이관)
- [공격 회차 안정 ID](#공격-회차-안정-id) — 공격 단계·타겟 커밋·정렬·타임라인·전달 방식별 Impact 계약
- [전투 진입 규칙](#전투-진입-규칙) — 규칙 9~12
- [전투 연계 규칙](#전투-연계-규칙) — 규칙 13~16
- [전투 연출 동기화 규칙](#전투-연출-동기화-규칙) — 규칙 17~21
- [애니메이션 상태 동기화 규칙](#애니메이션-상태-동기화-규칙) — 규칙 22
- [특수 공격 시스템 규칙](#특수-공격-시스템-규칙) — 규칙 23~43
  - 기본 (규칙 23~27) / 확장 5종 — TorrentSpirit 파도(28~31) · BloomFairy 힐러(32~37) · MushroomBomber 착탄형 DoT(38~40) · InfernoSpirit 단일 대상 DoT(41~42) · QuakeSpirit 착탄형 즉발 AoE(43)
- [전투 규칙 — 방어력 데미지 감쇄](#전투-규칙--방어력-데미지-감쇄-연구소-유닛-강화-시스템-구현-완료--멀티-실기-pass) — 규칙 44

---

## 유닛 이동 시스템

### 작업 배경

유닛 겹침을 허용하여 이동/공격 슬롯·타일 점유 한도를 두지 않고, 근접/원거리 유닛에 공통 상태 전환을 적용한다. 슬롯 복잡도 때문에 단순 구조를 채택한 설계 이유와 초기 재설계 과정은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-01)에 보존한다.

서버는 A*로 현재 위치에서 목적지까지 통과해야 할 이동 가능 타일의 순서와 목적지를 계산한다. 기본 A* 이동의 각 경유 타일은 중심을 권위 checkpoint로 사용하며, Simulation Root가 중심 허용 오차 안에 실제로 도달한 뒤에만 순서대로 소비한다. 중심에서는 강제 정지하거나 위치를 스냅하지 않는다. incoming 선분은 현재 중심까지 완료하고, 다음 outgoing 선분의 첫 frame부터 이동과 회전을 함께 적용한다. 회전 한도와 corridor 안전을 이동 중 만족할 수 없을 때만 `AlignToMove`로 정지 정렬한다. 최종 목적지도 목적 타일 중심에 정확히 수렴한다.

- 정적 이동 불가 지형과 건물은 경로를 차단한다.
- 다른 유닛의 현재 타일·선점 타일은 경로 차단 정보로 사용하지 않는다.
- 같은 타일에 여러 유닛이 겹칠 수 있다.
- 서버의 틱별 후보 이동 구간은 현재 A* 경로의 이동 가능 타일 영역을 연결한 logical path corridor 안에 있어야 한다. 경로에 없는 타일이나 이동 불가 타일·건물을 가로지르지 않는다.
- A*와 corridor는 `path[0]`을 동일한 출발점으로 해석한다. 현재 유닛이 서 있는 `path[0]`이 경기 중 건물 생성 등으로 이동 불가가 된 경우 첫 구간에서 그 타일을 빠져나가는 point sweep은 허용한다. 이 예외는 `waypointIndex=1`에서 `path[0]` 영역을 출발하는 동안에만 유효하며, `path[0]`으로 재진입하거나 다음 경유 타일·경로 밖 이동 불가 타일을 통과하는 데 사용할 수 없다.
- 경로 계산, 경로 checkpoint 진행, 실제 공간 타일 커밋은 서로 분리한다. 경로를 계산·교체한 사실만으로 `UnitData.Position`, 위치 역인덱스 또는 이동 이벤트를 다음 타일로 넘기지 않는다. 기본 A*의 checkpoint는 Simulation Root가 해당 타일 중심 허용 오차 안에 실제로 들어온 경우에만 소비한다. 실제 공간 타일은 서버 Simulation Root가 인접 타일 경계를 넘어 들어간 순서대로만 커밋하며, 한 frame에 여러 경계를 통과하면 관측된 인접 전이를 순서대로 모두 반영한다. A*·Chase·PendingRepath·PostCombatResume는 같은 서버 공간 커밋기를 사용하고, 클라이언트 보정이나 비인접 논리 점프는 금지한다. 이동 중 출발점 보정이 필요하면 현재 커밋 타일을 `path[0]`에 유지하고 앞쪽 **인접** 타일을 첫 waypoint로 명시한다. Root 경계 통과 전 `ProcessStep`을 실제 점유 도착으로 간주하거나, 중심에 도달하지 않은 최근접 통과·평면 교차만으로 checkpoint를 소비하는 구현은 금지한다.
- 다음 후보 이동 구간이 corridor를 벗어나거나 다음 타일이 건물 생성 등으로 이동 불가가 되면 그 구간을 적용하지 않고 현재 위치에서 재탐색한다.
- 재탐색은 유닛별·Unity frame별 최대 1회만 실행하고 새 path는 다음 frame부터 처리한다. fail-closed는 안전하지 않은 candidate pose/path를 해당 틱에 commit하지 않는다는 뜻이며, 동일 path 1회나 PendingPath 시간차만으로 이동 목표 자체를 종료한다는 뜻이 아니다. 반복 판단과 재개 기준은 `U-MOV-REPATH`를 따른다.
- 1차 corridor 안전 판정은 유닛 간 중첩을 허용하는 기존 규칙에 맞춰 Simulation Root point의 sweep을 기준으로 한다. 유닛 footprint 반경은 별도 게임플레이 변경 없이는 경로 차단에 추가하지 않는다.
- 모든 유닛의 `MoveSpeed`는 실제 권위 trajectory 거리/초다. 중심을 통과하는 코너의 도착 시간은 실제 trajectory 길이를 따르며, 경유 타일마다 `1 / MoveSpeed` 시간을 강제하거나 중심에서 일정 시간 머물게 하지 않는다.

### U-MOV-PHASE. 이동 단계

유닛 이동은 다음 의미 단계를 가진다.

```text
Idle → Navigate → Move (직선/연속 선회)
          └→ AlignToMove → Move
Move / AlignToMove ├─ 적 감지 → AcquireTarget
                   └─ 경로 차단·무효 경로 → Navigate
```

`Move`에는 직선 진행과 정상 코너의 연속 선회가 모두 포함된다. `AlignToMove`는 새 이동 시작, 급격한 재경로, 반전 또는 corridor 안전 위반처럼 이동하면서 방향 일치를 보장할 수 없을 때만 사용하는 정지 정렬 단계다. 일반 경로의 인접 타일 전환만으로 진입하지 않는다. 구현 enum은 다를 수 있지만 직선 이동·연속 선회·정지 정렬의 의미와 전이 원인은 구분해 기록해야 한다.

### U-MOV-ALIGN. 이동 방향 정렬

1. 서버는 현재 위치와 현재 경유 중심으로 이번 틱의 `DesiredMoveDirection`을 계산한다. 기본 A*의 위치 trajectory는 현재 checkpoint까지 center-to-center로 진행한다. 다음 경유 중심은 현재 중심을 실제로 소비한 다음 선분부터 방향 입력이 되며, look-ahead를 현재 선분 위치에 섞어 중심을 자르거나 되돌아오는 곡선을 만들지 않는다.
2. 서버는 공통 최대 회전 속도 270°/s 안에서 `SimulationFacing`을 `DesiredMoveDirection` 쪽으로 갱신한다. 이동이 허용된 틱의 실제 비영 위치 변화는 갱신된 `SimulationFacing` 방향으로만 적용하며 두 방향의 검증 오차는 1° 이하다.
3. 정상 코너에서는 현재 중심을 소비한 다음 frame부터 다음 경로 방향으로 이동과 회전을 함께 적용한다. 타일 경계나 waypoint 변경만을 이유로 속도를 0으로 만들지 않으며, 60° 전환은 outgoing 선분에서 이동 중 점진 회전한다. 단, 150° 반전 또는 10°/15° 방향 안전을 이동 중 만족할 수 없는 코너에서는 `AlignToMove`가 우선한다.
4. 후보 이동 구간이 현재 회전 속도와 이동 속도로 corridor 안전을 만족하지 못하면 먼저 해당 틱의 이동량을 줄인다. 안전한 비영 이동도 만들 수 없을 때만 정지 정렬 또는 재탐색한다.
5. `Move` 중 방향 오차가 15°를 초과하거나 이동-방향 일치 또는 corridor 안전을 보장할 수 없으면 해당 틱의 이동량을 0으로 하고 `AlignToMove`로 전환한다.
6. `AlignToMove`에서는 최단 Yaw로 제자리 회전하고 오차가 10° 이하가 된 서버 틱부터 `Move`로 복귀한다. 10° 진입 / 15° 이탈 히스테리시스는 정상 코너를 생성하는 규칙이 아니라 안전 fallback이다.
7. 새 명령·재경로·전투 종료 후 이동 재개 시 위치를 스냅하지 않는다. 현재 pose에서 안전한 연속 trajectory를 만들 수 있으면 연속 선회하고, 만들 수 없을 때만 정지 정렬을 사용한다. 정렬 중에는 경로 시간·거리를 소비하지 않고 재개 시 누적 진행량을 한 번에 적용하지 않는다.
8. A* 이동, 추격, PendingRepath와 전투 종료 후 이동 재개는 동일한 Authoritative Locomotion과 단일 Simulation Root writer를 사용한다. 각 경로는 이동 의도만 제공하고 위치·회전·phase를 직접 쓰지 않는다.
9. `Move` 또는 `AlignToMove` 중 유효한 적이 AcquireRange에 들어오면 같은 서버 틱에 이동 의도를 중단하고 `AcquireTarget`을 우선한다. 후보 이동으로 처음 범위에 들어왔다면 그 candidate position까지만 commit하고 `NoIntent`를 기록하며, 다음 틱의 추가 이동을 허용하지 않는다.
10. 150° 이상의 반전, 바로 앞 차단과 공격 정렬은 연속 코너링 대상이 아니다. 반전은 정지 정렬, 차단은 재탐색, 공격은 `U-ATK-ALIGN`의 완전 정지 및 5° 진입 / 8° 유지 규칙을 사용한다.
11. 전투 종료 복귀의 `finalTarget`이 현재 `UnitData.Position`과 같고 그 권위 타일이 walkable이면, A* 경로가 비어 있거나 `null`이어도 복귀 실패가 아니다. Simulation Root가 중심 허용 오차 밖이면 기존 Authoritative Locomotion으로 그 타일 중심까지 걸어서 이동하고 실제 중심 도달 뒤 완료한다. 권위 타일이 차단됐거나 중심까지의 구간이 실제로 안전하지 않고 유효한 우회도 없을 때만 기존 `WaitingRepath`/`Blocked` 계약을 유지한다.

270°/s, 150°와 1°는 초기 공통값이다. 변경하려면 순수 trajectory 회귀와 역할교대 멀티플레이 검증을 모두 다시 통과하고 `StatsReference.md`에 반영한다.

`NetworkTransform`은 서버 결과를 복제할 뿐 trajectory나 권위 방향을 다시 계산하지 않는다. Visual Root 보간은 화면 표현만 다듬으며 Simulation Root의 정지·경로·방향 판정을 숨기는 교정 수단으로 사용하지 않는다.

### U-MOV-REPATH. 재탐색 진행성과 정지 상태

1. 재탐색의 생명주기는 코루틴이나 개별 `MoveTo` 호출이 아니라 **유닛의 이동 목표**에 귀속한다. 같은 유닛·같은 최종 목적지의 command가 다시 발행돼도 진전 이력과 재탐색 예산을 초기화하지 않는다.
2. PendingPath는 계산 시점과 소비 시점 사이에 유닛이 전진할 수 있는 예약 결과다. 소비 시 현재 권위 타일이 path에 없으면 이를 치명적 오류로 처리하지 않고 stale path로 폐기한다. 서버는 현재 권위 위치에서 다음 frame에 새 경로를 요청하며 같은 frame에 코루틴을 재시작하지 않는다.
3. corridor가 재탐색을 요구했는데 A*가 현재와 동일한 logical path를 반환하는 것은 그 자체로 무한 반복 증거가 아니다. 해당 후보는 같은 frame에 다시 실행하지 않고 `WaitingRepath`로 보류하며, 다음 frame의 최신 pose와 경로 환경 revision으로 다시 평가한다. 다만 동일 objective·동일 environment revision에서 동일한 unsafe path 또는 `Unreachable` 결과가 다시 확인되면 `Blocked`로 전환하고, 외부의 중복 `MoveTo`로 깨우지 않는다. 이후 environment revision이 증가했을 때 같은 objective를 유지한 채 다시 평가한다.
4. `A→B→A` 또는 동일 path 반복은 실제 위치 commit·checkpoint 소비·경로 환경 변경이 없는 **무진전 관측**으로 누적한다. 한 frame에서 동기 반복은 항상 차단하되, 이동 목표를 `Blocked`로 전환하는 상한은 여러 frame에 걸친 목표 단위 이력으로 판단한다.
5. 실제 위치 commit 또는 logical checkpoint 소비가 확인되면 무진전 이력을 초기화한다. 건물 생성·파괴처럼 walkability revision이 변경되면 `Blocked` 목표는 같은 목표 ID를 유지한 채 재평가할 수 있다. 동적 건물로 기존 경로가 무효화되어 현재 revision의 경로 질의가 `Unreachable`을 반환한 경우도 이 lifecycle을 따르며, 같은 revision의 중복 command는 재탐색 예산이나 상태를 초기화하지 않는다. 단, 서버 경로 그래프에 목적지까지의 유효한 우회 경로가 존재하면 `Blocked`는 정상 결과가 아니며 경로 질의·출발점·캐시 무효화 계약의 실패로 판정한다.
6. `WaitingRepath`와 `Blocked`는 정상 완료가 아니다. 이동 완료 callback, 다음 공성 단계, 힐러 종착 감시를 발행하지 않는다. 외부 ticker는 `IsMoving == false`만으로 같은 목표의 새 command를 만들지 않으며, 목표 변경·경로 환경 revision 변경·명시적 취소 중 하나가 있을 때만 재개한다.
7. 이동 종료와 보류는 일반 이동 frame 평가를 흉내 내서 만들지 않는다. 서버는 명시적인 lifecycle 전이로 `NoIntent`, `WaitingRepath`, `Blocked`, `Completed`를 commit하고 command/segment revision과 애니메이션 표현을 같은 권위 전환에서 갱신한다.
8. 서버의 틱별 위치 변화가 0이고 공격·사망 표현도 아닌 `Idle`, `AlignToMove`, `WaitingRepath`, `Blocked` 상태에서는 Walk 애니메이션 시간이 진행되어서는 안 된다. 전용 Idle/Held 표현 또는 검증된 정지 pose를 사용하며, 빙결 스킬의 `Frozen` 상태를 재경로 대기에 재사용하지 않는다. 단, 유효 타겟을 획득해 같은 틱에 공격 행동으로 소유권을 넘기는 `TargetAcquirePriority`의 `NoIntent`는 일반 Held가 아니다. 이 전환에서는 Walk 첫 자세·Idle·Held를 중간에 삽입하지 않고 현재 이동 표현에서 provisional Attack으로 직접 이어진다.
9. `Move` 상태에서만 Walk 표현을 진행한다. 클라이언트는 로컬 위치 변화로 상태를 추측하지 않고 서버가 복제한 행동/표현 상태를 적용한다.

---

### 이동 규칙

**규칙 1. 기본 목표**
유닛은 스폰된 순간부터 상대방 성을 향해 이동한다.

**규칙 2. 이동 방식**
A*로 경로를 계산하고, 각 경유 타일 중심을 순서대로 통과해 다음 타일 중심으로 이동한다. 중심 허용 오차에 실제로 도달해야 경유 checkpoint를 완료할 수 있지만, 중심에서 정지하거나 위치를 스냅하지 않는다. 정상 코너는 중심을 소비한 다음 outgoing 선분에서 이동 중 점진 회전하고, 안전하게 이동 선회할 수 없는 경우에만 정지 정렬 후 진행한다.
같은 타일에 여러 유닛이 위치할 수 있으며, 겹침은 허용한다.

**규칙 3. 공유 타일 상태 (Shared Tile State)**
모든 유닛이 접근할 수 있는 타일맵 상태를 서버에서 중앙 관리한다.
유닛이 이동하면 즉시 업데이트되며, 경로 탐색 시 이 정보를 기반으로 판단한다.

담고 있는 정보:
- 어떤 엔티티가 있는가 (건물 / 성)
- 어느 팀인가 (블루 / 레드)
- 점령 상태 (누구 영역인가)

유닛 점유 정보는 경로 탐색에 사용하지 않으므로 공유 타일 상태에 포함하지 않는다.

멀티플레이: 서버에만 존재. 유닛 이동 결정은 서버에서만 이루어지며 클라이언트는 NetworkTransform으로 결과를 받는다.
싱글플레이: 로컬에서 동일한 구조로 동작. 서버/클라이언트 구분 없이 단일 인스턴스로 실행된다.

**규칙 4. 경로 재계산 및 적용 시점**
건물 건설 / 건물 파괴 시 해당 시점에 모든 유닛의 경로를 즉시 재계산한다.

계산된 경로는 유닛이 다음 타일에 도착하는 시점에 교체된다 (Pending Path 방식).
이동 코루틴을 재시작하지 않으므로 유닛이 멈추지 않는다.

예약 경로를 소비할 때 유닛이 이미 전진해 현재 권위 타일이 포함되지 않으면 그 예약 경로는 stale로 폐기하고, 현재 권위 위치에서 다음 frame에 다시 계산한다. 이를 유닛 이동 완료나 치명적 오류로 판정하지 않는다.

예외: 유닛이 현재 이동 중인 타겟 타일 위에 건물이 생성된 경우,
해당 candidate 구간은 commit하지 않고 현재 pose를 보존한 뒤 다음 frame 재탐색으로 전환한다. 같은 frame 코루틴 재시작은 금지한다.

현재 경로가 동적 건물로 무효화되더라도 서버 경로 그래프에 목적지까지의 우회 경로가 있으면 같은 이동 목표와 command를 유지한 채 그 우회 경로로 계속 이동해야 한다. `Blocked`는 현재 environment revision에서 실제로 유효한 경로가 없을 때만 허용하며, 철거·파괴 등으로 revision이 증가하면 같은 목표를 1회 재평가한다.

**규칙 5. 이동 속도**
이동 속도는 유닛별 스탯으로 개별 관리한다.
A* 타일 이동과 전투 이동(월드 좌표 직선 이동) 모두 동일한 이동 속도 스탯을 사용한다.

**규칙 6. 전투 사거리 판정 기준**
전투 사거리 판정은 타일 기준이 아닌 월드 좌표 기준으로 처리한다.

**규칙 7. A* 이동 중 회전**
A* 이동 중 유닛은 항상 이동 방향(다음 타일 방향)을 정면으로 바라본다.
타일 전환 시 다음 타일 방향으로 서서히 회전한다.

**규칙 8. A* 재개 시 회전**
전투 종료 후 A* 재개는 규칙11의 안전한 복귀 checkpoint와 `U-MOV-ALIGN`을 따른다. 현재 pose에서 안전한 연속 이동·회전이 가능하면 진행하고 불가능할 때 정지 정렬한다. 뒤를 바라본 이동이나 위치 스냅은 허용하지 않는다. 이전 간략 정렬 설명은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-21)다.

**규칙 45. 건물로 경로가 완전히 막혔을 때의 유닛 동작** (2026-08-26 이관 — `GameSystemRules_RandomMap.md` 규칙 11에서 옮겨 옴)

맵 생성기는 건물 배치 후의 연결성을 보장하지 않는다. 따라서 **유닛 경로가 건물로 실제로 완전히 막히는 상황이 발생할 수 있으며, 그때의 유닛 동작은 이 규칙이 단일 소스**다. 맵 문서에는 「연결성을 보장하지 않는다」는 사실과 이 규칙을 가리키는 한 줄만 남는다.

**적 건물이 막은 경우**
가장 가까운 경로 차단 적 건물을 임시 공격 대상으로 선택한다. 파괴 후 원래 목적지로 다시 경로를 계산한다.

**아군 건물이 막은 경우**
- 유닛은 논리적 `PathBlocked` 상태가 된다.
  - 🔴 **이것은 유닛의 상태이며 타일의 상태인 `TileKind.Blocked` 와 다른 것이다.** 종전에는 이 유닛 상태를 타일 상태와 구분되지 않는 이름으로 적어, 읽는 사람이 「막힌 타일」과 「길이 막힌 유닛」을 가려낼 수 없었다. 한 이름이 서로 다른 두 대상을 가리키던 것을 나눈다는 점에서 좌표 변환과 상태 변환의 이름을 나눈 것(`GameSystemRules_RandomMap.md` 7장 용어 정의 「180도 회전」)과 같은 부류다.
  - `PathBlocked`는 이 규칙의 설계 상태 이름이며 구현 완료 표시가 아니다. 실제 현재 구현·남은 범위는 [감사표](../Assets/UnitCombatAssetMatrix.md)와 [ROADMAP](../ROADMAP.md), 당시 식별자0건 조사는 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-19)를 따른다.
- 걷기 애니메이션은 첫 프레임에서 멈추고 `Animator.speed = 0` 상태를 유지한다.
- 철거, 파괴 또는 랠리 변경으로 길이 열리면 복구하고 다시 경로를 계산한다.

> **상태: 확정된 후속 구현 범위 / 미구현.** 문서화만으로 구현 완료로 보지 않는다.
> **번호가 45인 이유**: 옮겨 온 내용은 **받는 쪽 문서의 다음 번호**를 따른다. 원래 번호(맵 문서 규칙 11)를 그대로 쓰면 이 문서 안에서 번호가 중복된다.

---

## 공격 회차 안정 ID

아래 안정 ID는 규칙 9~21의 전투 진입·연계·연출 규칙을 서버 권위 공격 회차 관점에서 구체화한다. 기존 번호 규칙을 대체하거나 재배열하지 않으며, 멀티플레이의 식별·복제·순서 역전 계약은 `GameSystemRules_UnitCombatSynchronization.md`의 `NET-ACTION-*`, `NET-FACING-002`, `NET-DELIVERY-*`, `NET-CANCEL-*`을 함께 따른다.

### U-COMBAT-PHASE. 서버 권위 전투 단계

서버 전투 행동은 `AcquireTarget → Chase → AlignToAttack → Windup → Impact → Recovery` 순서의 값 상태로 표현한다.

- `AcquireTarget`은 공격 후보를 선택하지만 아직 공격 회차를 커밋하지 않은 단계다.
- `Chase`는 서버 권위 이동으로 공격 접근 위치까지 진행하는 단계다.
- `AlignToAttack`은 위치 이동을 완전히 멈추고 공격 방향만 정렬하는 단계다. 이 단계의 `AttackSequenceId`는 0이다.
- `Windup`은 정렬 완료와 동시에 공격 회차를 커밋하고 검증된 공격 타임라인을 시작한 단계다.
- `Impact`는 같은 회차의 `HitIndex`별로 서버가 판정과 결과를 확정하는 단계다.
- `Recovery`는 더 이상 새 Impact를 만들지 않고 같은 회차를 종료하는 단계다.

`AlignToAttack`에 들어간 공격자는 회차 종료, 규칙에 따른 취소 또는 다음 이동 상태로의 권위 전환 전까지 위치를 이동하지 않는다. 이동 재개와 다음 공격은 현재 회차 상태를 덮어쓰지 않고 각각 새 행동 전환과 새 공격 회차로 처리한다.

서버 행동 단계와 Animator의 거친 표현 상태는 같은 값이 아니다. 최초 공격 사거리 진입의 `AlignToAttack`과 이미 공격 표현 중인 유닛의 새 타겟 `AlignToAttack`에서는 화면이 기존 Attack 클립을 선행 표현으로 재생하며 새 타겟 방향으로 회전할 수 있다. 최초 진입은 `Walk → provisional Attack`으로 직접 전환하며 두 상태 사이에 Walk 첫 자세·Idle·Held를 한 프레임이라도 삽입하지 않는다. 이때 위치는 완전히 정지하고, 선행 표현은 새 공격 회차의 시작이나 타격 승인을 뜻하지 않는다. 5도 정렬을 통과해 커밋할 때 화면의 같은 Attack 클립을 처음부터 restart하지 않는다. 선행 표현의 연속 재생 위치를 보존하되, 서버는 커밋 이후 아직 지나가지 않은 첫 Impact marker만 새 회차의 첫 승인 후보로 사용한다. 현재 표현 cycle의 모든 marker가 이미 지났다면 그 cycle에서는 Impact를 승인하지 않고 다음 cycle의 첫 marker부터 승인한다.

### U-TARGET-COMMIT. 회차 타겟 커밋

`AlignToAttack`까지의 타겟은 공격 후보이므로 서버가 변경하거나 취소할 수 있다. 방향 정렬이 완료되어 `Windup`에 들어가는 순간 서버는 단조 증가하는 `AttackSequenceId`와 그 회차의 `TargetId` 또는 권위 목표점을 함께 커밋한다.

서버가 `AcquireTarget`에서 후보를 발견한 사실과 Host/Client 표현에 활성 공격 타겟을 공개하는 것은 서로 다른 상태 전이다. Action 회전 소유권이 열리기 전 후보는 서버 내부에만 보류하며 표시 타겟 변경 RPC를 먼저 발행하지 않는다. 최초 진입은 Action 소유권이 열린 같은 원자 경계의 provisional Attack 시작 payload가 후보 타겟과 타격 억제를 함께 전달한다. 이미 Attack 표현을 유지하는 전투 연계의 타겟 교체도 이전 타격 허가를 먼저 닫고, Action 소유권과 Host 타겟 적용 성공을 확인한 뒤에만 원격 Client에 공개한다.

커밋된 회차의 타겟은 표시용 타겟 변경, 다음 타겟 탐색 또는 패킷 순서 역전으로 다른 대상에 이전하지 않는다. 타겟이 사망·소멸·무효화되어도 기존 회차는 규칙에 따라 빗나감·취소·종료될 뿐 다른 타겟을 대신 공격하지 않는다. 새 타겟은 반드시 새 `AttackSequenceId`를 가진 새 회차에서만 사용한다.

### U-ATK-ALIGN. 공격 정렬과 방향 유지

공격 정렬은 서버 Simulation Root의 단일 writer가 수행한다.

- `AlignToAttack` 동안 위치 이동량은 0이어야 하며, 서버는 목표 방향을 향해 최대 270°/s로 점진 회전한다.
- 서버 SimulationFacing과 권위 목표 방향의 각도 오차가 **5° 이하**일 때만 `Windup` 진입과 회차 커밋을 허용한다. 5°를 초과한 상태에서는 쿨다운·Windup·Impact를 시작하지 않는다.
- TargetLocked 공격은 커밋 후에도 서버가 같은 `TargetId`를 향해 Windup 동안 추적 회전한다. 각 Impact에서는 그 순간의 목표 방향과 SimulationFacing 오차가 **8° 이하**여야 방향 조건을 충족한다.
- Impact의 8° 기준을 벗어나면 서버는 방향 조건을 실패로 판정하며, 클라이언트 보간·표시 타겟·애니메이션 방향으로 이를 성공 처리하지 않는다.

타겟 잠금과 방향 잠금은 별개다. 회차의 `TargetId`를 유지한다는 사실이 과거 방향을 고정한다는 뜻은 아니며, 각 Impact의 실제 SimulationFacing을 `AuthoritativeAimDirection`으로 기록한다. 클라이언트는 현재 타겟 위치를 다시 읽어 과거 Impact 방향을 재계산하지 않는다.

### U-ATK-TIMELINE. 공격 커밋과 타격 시간축

공격 쿨다운 시작, `Windup` 시작 및 `AttackSequenceId` 발급은 정렬 완료 후의 같은 서버 커밋 경계에서 발생한다. 최초 전투 진입의 `AlignToAttack`은 기존 Attack 클립을 선행 표현으로 재생할 수 있지만, 새 Attack 회차의 Windup·Impact, 피해 예약과 타격 VFX·SFX는 정렬 중 미리 시작하지 않는다. 커밋 전 Animation Event와 클립 marker는 Impact·VFX·SFX·피해 결과를 만들거나 소비하지 않는다. 5도 정렬 커밋은 화면의 같은 Attack 클립과 정규화 진행 위치를 보존하고, 커밋 뒤 아직 지나가지 않은 marker만 서버 시간축의 Impact 후보로 연결한다. 커밋 전에 지나간 marker를 소급 승인하거나 현재 cycle을 되감지 않으며, 남은 marker가 없으면 다음 cycle부터 정규 타임라인을 승인한다.

다만 공격 사거리 안에 유효한 후보가 계속 존재하는 전투 연계에서는 타겟 교체만으로 기존 Attack 표현을 Walk·Idle·Held로 바꾸지 않는다. 기존 Attack 표현을 유지하면서 서버 Simulation Root가 새 후보 방향으로 점진 회전하고, 정지·5도 정렬을 통과한 뒤 새 회차의 타격 표현을 승인한다. 정렬 전에 로컬 Attack 클립의 타격 표식이 지나가더라도 새 회차의 VFX·SFX·피격 표현이나 피해 결과를 만들거나 소비하지 않는다.

각 `HitIndex`의 Impact 시각은 서버가 읽는 검증된 `AttackTimeline` 또는 `AttackProfile`의 Windup·타격 오프셋·Recovery를 정규 원본으로 사용한다. 서버 시간축이 실제 판정과 결과의 권위이며, 로컬 Animator 상태나 `OnAttackHit` Animation Event는 피해·회차 진행을 발생시키지 않는다.

Attack 표현을 restart하지 않고 연속 재생하는 경우 프로필 offset은 현재 연속 재생 anchor에서 커밋 뒤 도착할 다음 유효 marker occurrence를 선택하는 입력으로 사용한다. 서버는 선택된 occurrence의 절대 Impact 시각, 회차 ID, HitIndex와 revision을 하나의 커밋 결과로 고정해 판정 예약과 표현 스코프가 같은 marker를 가리키게 한다. 서버 예약만 `CommitServerTime + 원래 offset`을 사용하고 화면만 다음 marker로 넘기는 식의 서로 다른 시간축은 허용하지 않는다.

한 커밋 회차가 로컬 표현에 부여하는 타격 허가는 해당 프로필의 `HitIndex` 개수만큼만 유효하다. 단일 타격은 첫 marker, 다중 타격은 마지막 marker가 승인된 `HitIndex`를 소비하는 즉시 그 회차의 로컬 타격 허가를 닫는다. Attack 클립이 계속 루프하더라도 다음 단조 증가 회차가 도착하기 전 marker는 VFX·SFX·트레이서·피격 표현을 만들지 않는다. 단순한 `impactEnabled` 상태만으로 회차 ID가 없거나 이미 소모된 marker를 승인해서는 안 된다.

C3 프로필이 아직 `Unresolved`인 미완료 유닛은 정규 회차 허가가 없는 상태를 공격 취소로 해석하지 않는다. 지원 완료 전에는 기존 Legacy 공격 VFX·SFX·트레이서를 보존하고 C3 비교에서만 미지원으로 격리한다. 기본값/0 스코프를 발행한 뒤 공통 fail-closed gate에서 Legacy 표현까지 지우는 구현은 금지한다. 정규 프로필과 공격 타임라인이 완성되어 유효 스코프를 발행할 수 있을 때 해당 타입 전체를 원자적으로 스코프 기반 표현으로 전환한다.

### U-IMPACT-TARGETLOCKED. TargetLocked 근접·Hitscan 판정

TargetLocked `MeleeContact`와 `Hitscan`은 회차에 커밋된 동일 `TargetId`를 각 Impact마다 서버의 최신 권위 pose로 다시 검증한다. 서버는 다음 조건을 모두 확인한다.

1. 타겟이 생존하고 현재도 유효한 공격 대상이다.
2. 타겟이 해당 공격 프로필의 권위 사거리 안에 있다.
3. 해당 Impact의 목표 방향과 SimulationFacing 오차가 `U-ATK-ALIGN`의 8° 유지 기준 이하다.
4. Impact가 같은 `AttackSequenceId + HitIndex`의 검증된 서버 시각에 속한다.

하나라도 실패하면 그 Impact를 적중으로 승인하지 않는다. 실패한 Impact나 사망한 타겟을 다른 타겟으로 교체하지 않으며, 다음 대상 공격은 새 회차에서 시작한다. 다중 타격은 모든 `HitIndex`를 각각 같은 방식으로 재검증한다.

### U-IMPACT-LOCKEDPOINT. LockedPoint 발사 고정

LockedPoint 공격은 서버 `Launch` 시점의 권위 목표 위치와 발사 방향을 고정한다. Launch 뒤에는 현재 타겟의 이동 위치나 표시용 타겟 방향으로 목표점·방향을 바꾸지 않는다.

고정된 목표점과 방향은 같은 공격 회차의 착탄 레코드까지 유지한다. Single은 권위 착탄점과 `ImpactHitRadius`로 단일 결과를, Area는 권위 착탄점 중심의 범위 결과를 서버가 확정한다. 로컬 투사체 충돌이나 클라이언트 재조준은 결과를 만들거나 변경하지 않는다.

### U-IMPACT-INDEPENDENT. Homing·Traveling 독립 생명주기

`Homing`과 `TravelingArea`는 회차가 생성한 뒤 서버가 별도의 권위 생명주기로 진행한다.

TorrentSpirit의 생성 전 서버 승인 발사 예약에 한해서는 규칙 29의 primary 상실 예외를 따른다. 이는 다른 전달체의 일반 취소·Impact 적중 검사까지 완화하지 않으며, 발사 전 공격자 사망·게임 종료 취소와 같은 원래 타깃 identity를 유지한다. 예약 발사와 대상별 contact 결과는 서로 다른 사건이다.

- Homing은 서버가 추적 대상, 목표 갱신과 착탄을 관리하며 클라이언트 추적으로 판정을 대신하지 않는다.
- TravelingArea는 서버가 판정 영역을 이동시키고 대상별 첫 접촉을 독립 HitIndex 또는 명시적 접촉 인덱스로 기록한다.
- 공격자 사망 후 이미 발사·생성된 전달체를 계속 진행할지는 공격 프로필의 `PersistsAfterSourceDeath` 값으로 결정한다. Windup 중 아직 생성되지 않은 전달체는 공격자 사망 시 취소한다.
- `MeleeContact`·`Hitscan`과 아직 독립 전달체를 만들지 않은 future `HitIndex`는 공격자 사망 후 지속하지 않는다. 이미 예약된 지연 작업이 있더라도 판정 dispatch 전에 취소하고, 해당 future `HitIndex`의 결과를 생성하지 않는다.
- source 회차가 끝나거나 타겟이 바뀌어도 이미 생성된 전달체의 회차 ID·목표 처리 방식·결과 키를 다른 회차나 새 타겟으로 이전하지 않는다.

Periodic 효과는 최초 부여 Impact의 회차와 별도의 효과 인스턴스 ID·TickIndex로 진행하며, 갱신·덮어쓰기·종료는 해당 효과 규칙을 따른다.

---

## 전투 진입 규칙

**규칙 9. 감지 사거리와 공격 사거리**
모든 유닛(근접/원거리 불문)은 감지 사거리와 공격 사거리를 별도로 가진다.
감지 사거리는 항상 공격 사거리보다 길다.
근접/원거리 유닛의 차이는 공격 사거리 수치와 공격 방식에만 있으며, 상태 전환 로직은 동일하다.

**규칙 10. 유닛 상태 머신**
유닛은 다음 세 가지 상태 중 하나에 있다:

1. **A* 이동**: 상대방 성을 향해 타일 중심으로 이동 중
2. **전투 이동**: 타겟의 공격 접근 위치를 향해 이동 중. 서버가 현재 위치부터 접근 위치까지의 직선 구간이 이동 가능하다고 검증한 경우에만 월드 좌표 직선 이동을 사용하고, 중간에 건물·완전 차단 지형·이동 불가 타일이 있으면 서버 A* 경로로 우회한다.
3. **공격**: 멈추고 타겟을 공격 중

상태 전환 조건:

| 현재 상태 | 전환 조건 | 다음 상태 |
|-----------|-----------|-----------|
| A* 이동 | 감지 사거리 내 적 진입 | 전투 이동 |
| 전투 이동 | 타겟이 공격 사거리 내 도달 | 공격 |
| 전투 이동 | 타겟이 감지 사거리 이탈 | A* 이동 재개 |
| 공격 | 타겟이 공격 사거리 이탈 + 감지 사거리 내 | 전투 이동 |
| 공격 | 타겟이 감지 사거리 이탈 | A* 이동 재개 |
| 공격 (타겟 처치 후) | 감지 사거리 내 다른 적 + 공격 사거리 내 | 공격 (새 타겟) |
| 공격 (타겟 처치 후) | 감지 사거리 내 다른 적 + 공격 사거리 밖 | 전투 이동 |
| 공격 (타겟 처치 후) | 감지 사거리 내 적 없음 | A* 이동 재개 |

`공격 → 공격 (새 타겟)` 전이는 Animator 상태를 나갔다가 다시 들어가는 전이가 아니다. Attack 표현을 유지한 채 추적 타겟만 교체하고, 새 타겟의 실제 공격은 새 회차의 정렬·커밋 경계에서 시작한다. 다른 적이 공격 사거리 밖이라 추격이 필요하거나 공격 가능한 적이 없을 때만 Attack 표현을 종료한다.

**규칙 11. A* 이동 재개 방식**
전투 종료 또는 타겟 이탈로 A* 이동을 재개할 때:
현재 월드 위치에서 서버가 도달 가능하다고 검증한 후보 중, 성 방향으로 진행성을 유지하는 가장 가까운 전방 타일 중심을 복귀 checkpoint로 선택한다. 현재 위치에서 그 중심까지의 구간이 건물·완전 차단 지형·이동 불가 타일을 통과하지 않아야 하며, 안전한 직선 복귀가 불가능하면 서버 A*로 복귀 경로를 계산한다. Simulation Root는 해당 중심까지 걸어서 이동하고 스냅하지 않는다. 중심 도달 후 현재 권위 타일에서 성까지 A*를 다시 계산한다.

단순히 가장 가까운 타일 또는 walkable 인접 타일이라는 이유만으로 복귀 후보를 확정하지 않는다. 뒤쪽 타일로 복귀하거나, 이동 불가인 현재 타일을 그대로 성공 후보로 반환하거나, 유효한 전방 복귀 경로가 있는데 `Blocked`로 종료하는 것은 허용하지 않는다.

**규칙 12. 전투 이동 중 회전**
추격은 규칙10의 안전한 direct/A* 접근 경로와 공통 `U-MOV-ALIGN`의 이동 방향·회전 계약을 따른다. 타겟 추적 회전과 이동 trajectory를 별도 writer로 계산하지 않는다. 공격 진입 후의 정지·타겟 방향 정렬은 `U-ATK-ALIGN`이 원본이다. 이전 간략 타겟 방향 설명은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-22)다.

---

## 전투 연계 규칙

**규칙 13. 타겟 선택**
처음 감지된 적을 타겟으로 삼는다.
타겟을 처치한 후에는 감지 사거리 내에서 가장 가까운 적을 다음 타겟으로 선택한다.
감지 사거리 내에 적이 없으면 A* 이동을 재개한다.

**규칙 14. 공격 중 이동 금지**
모든 유닛은 공격 중 이동하지 않는다.

**규칙 15. 공격 중 회전**
공격 중 유닛은 항상 타겟 방향을 정면으로 바라본다.
타겟 방향으로 서서히 회전하며, 공격이 끝날 때까지 타겟 방향을 유지한다.

**규칙 16. 범위 공격 (AoE)**
범위 공격은 대미지 계산 방식의 차이이며, 이동 및 상태 전환 규칙은 동일하게 적용된다.
아군에게는 대미지를 주지 않는다.

---

## 전투 연출 동기화 규칙

데미지 판정(데이터)은 서버 권위로 유지하고, 화면 연출은 서버 공격 일정과 확정 결과를 표현 시간축에서 소비한다. 상세 계약은 `GameSystemRules_UnitCombatSynchronization.md`의 `NET-TIME-002`·`NET-PRESENT-003`을 따른다. (2026-07-12 신설, 2026-09-06 C3 단일 표현 소유권 교정)

**규칙 17. 타격 프레임 타이밍의 단일 출처**
일반 공격의 타격 시점(HitFrameTimes)은 유닛 Attack 애니메이션 클립의 `OnAttackHit` Animation Event 시간에서 추출한다.
유닛 생성 시 클립 이벤트에서 자동 추출하며, 수동 입력값은 클립에 이벤트가 없을 때 폴백으로 사용한다.
다중 히트 유닛은 클립의 여러 `OnAttackHit` 이벤트 시간을 오름차순으로 모두 수집한다.

Assault는 명시 `attackTimelineClip`의 30fps `0:05`(5/30초) marker를 사용한다. 정상 Factory 주기는 클립 길이 `0:10`(10/30초), 클립 미사용 기본 주기는 0.2초다. 이전 `0:33` 주기 의미는 미확정이며 임의 환산하지 않는다.

DustSpirit과 RabbitTrickster는 `UnitFactory.attackTimelineClip`에 실제 `Base Layer/Attack` motion을 명시한다. 서버/싱글·Client가 같은 선택 클립에서 주기와 이벤트를 읽고 양 팀 controller의 motion과 일치하는지 검증한다. 유사 이름 `Attack2`·`Attack3` 열거 순서로 바뀌어서는 안 된다. Rabbit의 생산 Attack은 2초/30fps이고 `OnAttackHit`·type25 fallback·검증 기대값은 `0:20`(0.6666667초)이다. 서버는 해당 offset으로 권위 피해를 예약한다.

**차지 유닛 typed marker 계약 (InfernoSpirit · FoxMagician):** 차지 유닛은 생산 Attack 클립에 `OnAttackCharge`(차지 시작)와 `OnAttackHit`(발사)를 함께 가진 유닛이며, 코드는 타입 목록이 아니라 이 클립 이벤트로 판별한다. 현재 InfernoSpirit `OnAttackCharge @ 5/30초(0.1667초)`·`OnAttackHit @ 1.5초`, FoxMagician `OnAttackCharge @ 1초`·`OnAttackHit @ 85/30초`다.
- `OnAttackCharge`는 차지 VFX만 켠다. 공격 SFX·tracer·recipient observation·LocalAttackHit·HP 경로로 가지 않는다. 멀티에서는 공격 회차 lease를 **확인만 하고 소비하지 않으며**(Armed·남은 횟수·SourceOnly 아님·현재 회차), 같은 회차에서 한 번만 낸다.
- `OnAttackHit`는 종전대로 lease를 소비하고 투사체(tracer)를 발사하며 그 순간 공격 SFX를 낸다(사운드 규칙 15 — tracer VFX와 짝). 차지 VFX를 다시 켜지 않는다. 차지 프리팹 길이는 두 이벤트 사이 구간(Inferno 1.3333초, Fox 1.8333초)에 맞춰 발사 순간 스스로 끝난다.
- **[2026-10-20 개정 — 사용자 승인 목표 계약 · 🔴 코드 미반영(미구현)]** 발사 전에 공격이 끊기면(대상 사망 · 공격자 사망 · 이동 명령 · 정지 명령) **서버가 발사를 취소한다.** 투사체·공격 SFX·피해가 없고, 차지 VFX는 즉시 꺼진 뒤 다음 동작으로 이어간다(취소 전용 애니메이션 없음). 대상이 사거리 밖으로 나간 것은 끊김이 아니며 **원래 대상에게 발사**한다. 발사 순간 대상이 사망·소멸했으면 발사하지 않는다. 차지 SFX는 아직 없다. 차지가 없는 투사체 유닛도 공격 시작부터 발사 전까지 같은 경계를 적용한다.
- **[2026-10-20 개정 — 사용자 승인 목표 계약 · 🔴 코드 미반영(미구현)]** SOLO/server/client 생성은 실제 `OnAttackHit`만 offset으로 추출하며 `OnAttackCharge`는 제외한다. **투사체 유닛은 이 offset이 서버 발사 시각이고, 피해는 서버 착탄 시각(발사 + (중심 거리 − 대상 반경) ÷ 투사체 속도 — 비행 시간은 발사 때 계산해 고정한다)에 확정된다. 즉발 유닛은 종전대로 권위 피해 offset이다.** type21 fallback은2.8333333초다. 실제 피해는 기존 권위 writer가 소유한다. Fox 값은 사용자 원래 `2:25@30fps = 85/30초`이며2.25초는 잘못된 환산이다. Unresolved/LegacyFallback·기존 writer·range·다른 타입 의미는 유지한다.

*(2026-10-20 개정 — 사용자 승인(서버 권위 투사체 Plan 4-2-1 F-1·F-2, 1-3 U-1·U-14): 위 두 항목은 **승인된 목표 계약이며 아직 구현·테스트 전이다.** 구현 전인 지금 코드는 개정 전 문장대로 「발사 전 끊김은 표현만 멈추고 예약된 서버 피해는 취소하지 않는다」·「`OnAttackHit` offset = 권위 피해 시점」으로 동작한다. 개정 전 원문: [서버 권위 투사체 Plan 4-2-3](../_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#4-2-3-규칙-문서-반영과-개정-전-원문))*

*(2026-10-19 개정 — 사용자 승인: 종전 「FoxMagician typed marker 계약」은 Fox 전용이었고 `OnAttackCharge`가 source VFX/SFX를 내며 `OnAttackHit` 로컬 callback은 metadata만 기록하고 VFX·tracer를 내지 않았다. 개정 전 문장 원문은 [작업 Plan 9-4](../_Tasks/2026-10-19/11_09_charge-unit-launch-timing/Plan.md#9-4-상시-문서-반영과-개정-전-원문), 2026-10-14 계약 원문은 [당시 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-02). 구현·사용자 싱글·멀티 시각 확인: [Research 9절](../_Tasks/2026-10-19/11_09_charge-unit-launch-timing/Research.md#9-implementation-result))*

Rabbit focused 수용과 Fox source/controlled 검증은 전체 migration·전체 SOLO 완료가 아니다. 현재 타입별 수용과 미실행 native/멀티/시각 범위는 [감사표](../Assets/UnitCombatAssetMatrix.md), [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-02)에서 구분한다.

**규칙 18. 서버 데미지 타이밍 정밀화**
멀티플레이 서버의 전투 Tick(50ms 격자)에서 쿨다운 만료를 감지할 때, 만료 후 초과 경과한 시간(오버슈트)을 데미지 딜레이에서 차감하여 격자 오차 누적을 제거한다.
쿨다운 감소는 실제 경과 시간과 1:1로 처리한다(이월 잔여분을 다음 Tick의 경과 시간에 다시 포함하지 않는다).
데미지는 항상 서버 타이머로만 적용하며, Animator 상태(`OnAttackHit`)에 종속시키지 않는다.

**규칙 19. 피격 표현 큐**
**[2026-10-20 개정 — 사용자 승인 목표 계약 · 🔴 코드 미반영(미구현)]** 투사체 유닛의 피해 숫자·피격 표현은 **서버 착탄 결과로 착탄 시각에 한 번** 표시한다. 착탄보다 먼저 표시하는 예외는 없다. *(종전의 FoxMagician 즉시 표시 예외는 삭제 — 사용자: VFX가 없던 시절의 규칙이다. 구현 전인 지금 코드에는 그 예외가 남아 있다. 개정 전 원문: [서버 권위 투사체 Plan 4-2-3](../_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#4-2-3-규칙-문서-반영과-개정-전-원문))* 큐에서 공격자 조회로 타입을 재분류하지 않는다. Supported bridge·유효 exact key·다른 Legacy/특수 피해 경로는 유지한다. 현행 charge/damage marker는 규칙17을 따른다. 이전2.25초 및 focused 수용 기록은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-03)이며 최신 typed 계약의 native queue·멀티 표시·시각 수용을 대신하지 않는다.

도메인 HP는 서버 값 도착 즉시 갱신한다(서버 권위 유지).
피격 연출(HP 텍스트·피격 VFX·타격 반응)은 서버의 확정 결과를 정규 결과 키로 결합한 C3 Coordinator가 표현 Impact 시점에 최대 한 번 방출한다. 공격 일정만으로 적중을 예측하지 않으며 로컬 `OnAttackHit`이나 트레이서 도착을 기다리지 않는다. 표시용 HP는 `NET-TIME-002`를 따르고 권위 HP 적용을 지연하지 않는다.
확정 결과는 피해자 타입·팀·Impact 위치·적용 피해·결과 HP를 포함한 self-contained 표현 스냅샷이어야 한다. HP 텍스트와 설정된 피격 VFX는 이 스냅샷만으로 재생하며, 별도 피해 이벤트나 현재 살아 있는 View를 기다리지 않는다. View 펀치는 동일 피해자 객체 수명이 확인될 때만 더하는 선택 표현이고, 사망·Despawn 또는 ID 재사용 때문에 확인할 수 없더라도 필수 표현을 실패시키지 않는다.
피격 VFX 에셋은 향후 타입별로 추가될 수 있는 선택 채널이다. 현재 `hitPreset`이 비어 있는 타입은 `SkippedNoAsset` 정상 생략으로 기록하며 실패나 대체 VFX 생성 대상으로 삼지 않는다. 다만 미래 에셋 추가 때 네트워크 계약을 다시 바꾸지 않도록 타입·팀·Impact 위치 스냅샷은 에셋 유무와 관계없이 모든 확정 적중 결과에 포함한다. 프리셋이 실제로 설정된 뒤 재생 호출이 실패한 경우만 채널 실패로 판정한다. 유닛 제거 시 재생되는 사망 VFX는 이 선택 피격 VFX와 별도 수명이며 C3 결과 표현 실패로 취소하거나 중복 실행하지 않는다.
타겟 전환·사망·공격자 사망·StopCombat은 미승인 미래 marker 허가를 닫는 입력이다. 이미 서버가 확정한 결과를 취소하거나 뒤늦게 큐 전체를 flush하는 명령이 아니다. 사망 전 확정 결과가 사망 알림 뒤 도착해도 정규 키·기존 catch-up age·늦은 참가 baseline으로 처리한다. 사라진 View나 재사용 ID로 타격 반응을 전달하지 않고 권위 상태는 보존한다.
타임아웃은 손상·유실을 진단하고 제한 버퍼를 종료하는 안전망이며 새로운 적중이나 정상 피격 표현을 생성하지 않는다. 공격자 FIFO·marker 대기·사망 flush는 전환 검증용 레거시 경로만 허용하며 신규 정상 경로에서 사용하지 않는다. 비교 검증 뒤 경기 고정 모드로 단일 방출자를 선택하고 사용자 실기 PASS 전 레거시 코드를 최종 삭제하지 않는다.
피격 VFX에는 사운드 규칙 15에 따라 대응 SFX를 짝으로 두거나, 없을 경우 주석으로 명시한다.

**규칙 20. 원거리 유닛 트레이서**
**[2026-10-20 개정 — 사용자 승인 목표 계약 · 🔴 코드 미반영(미구현)]** 투사체 유닛의 투사체는 **서버 발사 기록으로** 재생하고 **서버 착탄 시각·표면 지점**에 닿는다. Homing은 대상을 따라가고, 비행 중 대상이 사라지면 마지막 지점에서 착탄 이펙트만 낸다(피해·숫자 없음). 로컬 `OnAttackHit` marker는 총구 VFX·SFX만 낸다. 늦게 접속한 화면도 비행 중 투사체를 복원한다. *(종전: 「원거리 유닛은 `OnAttackHit` 시점에 연출 전용 발사체를 재생한다」 — 구현 전인 지금 코드는 이대로 동작한다. 개정 전 원문: [서버 권위 투사체 Plan 4-2-3](../_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#4-2-3-규칙-문서-반영과-개정-전-원문))*
트레이서는 순수 시각 표현이며 데미지 판정 타이밍(서버)에 영향을 주지 않는다.
Hitscan 트레이서는 같은 공격 일정의 표현 Impact 시각에 맞춰 진행률·잔여 비행 시간을 조절한다. 이미 지난 시각이면 축약하거나 생략하고 결과 표현을 추가 지연하지 않는다. 착탄 콜백은 시각 오브젝트 종료만 담당하며 피격 표현 큐를 방출하지 않는다. 서버 투사체 판정이 있는 ProjectileImpact는 별도 권위 착탄 계약을 유지한다.

**규칙 21. Attack 루프 이탈 시 전투 애니메이션 재전송 가드 해제**
애니메이션 상태의 현행 계약은 규칙22의 값 기반 동기화가 이 규칙의 옛 엣지 RPC 봉합책을 상위 대체한다. `_combatAnimationSent`는 피해(`ExecuteAttack`)·타겟 RPC 게이팅 역할과 구분하며, 규칙19의 확정 결과 표현을 로컬 marker 수신에 종속시키지 않는다. 옛 경쟁 조건·가드 해제 지침은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-20)다.

---

## 애니메이션 상태 동기화 규칙

**규칙 22. 유닛 애니메이션 상태의 값 기반 동기화** (2026-07-13 신설, 이동/Walk 애니메이션 동기화 작업)
멀티플레이에서 유닛의 애니메이션 상태(Walk / Attack 등)는 서버가 쓰고 모든 클라이언트가 읽는 **NetworkVariable(서버 권위 값)** 로 동기화한다. `NetworkUnit`에 `UnitAnimState`(None / Walk / Attack) NetworkVariable을 두고(ReadPermission=Everyone / WritePermission=Server), 클라이언트는 값이 변경될 때(`OnValueChanged`) 및 유닛 스폰 시 현재 값으로 애니메이션을 적용한다.

상태가 바뀌는 순간에만 쏘는 1회성 RPC(엣지 트리거)에 애니메이션 상태를 의존시키지 않는다 — 스폰 레이스(신규 유닛이 구독을 완료하기 전에 첫 신호가 도착)나 신호 유실 시 클라이언트가 틀린 상태에 갇히는 것을 구조적으로 방지하기 위함이다. 호스트/싱글플레이는 기존 로컬 Animator 직접 제어를 유지한다.

**적용 시점 봉합:** 애니메이션 상태 적용이 `UnitView.Initialize`(애니메이터 준비 완료)보다 이르면 무음 실패할 수 있으므로, `UnitView.Initialize` 말미에서 현재 상태 값을 **멱등하게 재적용**한다(`NetworkUnit.ReapplyAnimStateToView`). 재적용은 값 기반이라 몇 번 호출되어도 안전하다.

**Attack 표현의 경로 구분 (2026-10-03):** 일반 적 공격은 독립 Attack 레벨 값만으로 클립을 시작하지 않는다. 타겟·revision·표현 허가를 함께 전달하는 기존 정규 공격 경계를 유지하며 공통 레벨 시작 차단을 해제하지 않는다. 힐러는 규칙32의 **SeparateHeal 예외**로, 서버 쓰기/모든 Client 읽기의 별도 atomic snapshot NetworkVariable에 객체 수명·힐 회차·revision·시전자/대상·시작/종료 시각·Aim·marker offset·clip duration·active·Frozen/Held 우선순위·종료 사유를 함께 담는다. 일회성 시작/종료 RPC만으로 대체하지 않는다.

힐 snapshot은 스폰·unitId 바인딩·View 초기화 이후 현재값으로 복구한다. 준비 전 수신을 재생 성공으로 소비하지 않는다. 새 힐 회차는 해당 시전 모션을 재생하고 같은 회차/중복 revision은 restart하지 않는다. Frozen/Held 갱신 뒤 복귀는 같은 회차의 서버 경과시간으로 이어가며, 낮은 sequence/revision·닫힌 회차의 재활성화·다른 객체 수명은 거부한다. priority는 힐 snapshot과 원자적으로 소비하여 늦은 독립 Walk/Frozen/Held scalar가 최신 힐 표현을 덮지 못하게 한다. Client는 Animator 표현만 적용하고 Aim은 증거용으로 보존한다. Client Root/Visual offset 회전 writer를 새로 만들지 않으며 방향은 기존 서버 Root 복제를 따른다.

**후속 승인 교정:** future-start는 IsDue까지 기다리며 priority cache는 실제 적용 성공 후 갱신한다. 연속 유효 힐의 다음 sequence에는 중간 inactive Walk/Held를 넣지 않는다. 실제 Frozen/Held와 terminal/current-state 복원은 유지하며 클립/표현 정책 및 native 에셋 적용/검증 경계는 규칙32를 따른다.

데미지 판정은 규칙 18에 따라 서버 타이머로만 적용하며 애니메이션 상태 값과 분리한다. 조준 회전(타겟 방향 추적)은 애니메이션 상태와 별개의 타겟 참조로 처리한다(규칙 12·15). Attack 루프 안정화와 규칙 19의 결과 표현 방출은 독립 책임이다. marker가 없거나 늦어도 유효한 확정 결과를 다음 루프까지 보류하지 않는다. (규칙 21의 재전송 가드 해제는 본 규칙의 값 기반 동기화로 대체된다.)

---

## 특수 공격 시스템 규칙

특수 능력(휩쓸기 / 착탄 / 파도 / DoT / 힐 등)을 가진 유닛의 추가 피해·효과 처리 규칙 모음. (2026-07-17 신설, 도끼병 휩쓸기형 AoE 구현 작업 — 특수 유닛 5종 중 첫 구현) 기본 규칙 16(범위 공격은 대미지 계산 방식의 차이, 이동/상태 전환은 동일, 아군 무피해)을 전제로 한다.

특수 공격의 현재 개별 수치는 [StatsReference](../StatsReference.md), ×10 스케일 대상·불변 대상은 [강화 규칙1](GameSystemRules_Upgrade.md)을 따른다. 공격 주기·marker·거리·고정 비율은 ×10 대상이 아니다. 이전 수치 주의문은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-16)다.

**규칙 23. 특수 공격의 전략 핸들러 구조**
특수 공격은 유닛별 독립 핸들러 클래스로 구현하고 `UnitType` 키 레지스트리로 매핑한다.
- 계약: `ISpecialAttackBehavior.Apply(SpecialAttackContext)` — 특수 공격 1종 = 이 인터페이스를 구현한 클래스 1개.
- 컨텍스트 `SpecialAttackContext`: 공격자, 주 타깃, 유닛 목록, 재사용 피해 헬퍼, 월드 좌표 조회 수단, reach/arc 값을 담아 핸들러에 전달한다.
- 레지스트리는 `UnitType`을 특수 공격 핸들러에 매핑한다. 미등록 타입은 일반 단일 타깃 경로를 유지하고 힐러는 규칙32의 독립 경로를 따른다. 최초 BattleAxe 단독 등록 상태는 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-02)이며 현재 특수유닛 전체 등록 상태로 읽지 않는다.
- 피해 수렴점 `UnitCombatUseCase.ExecuteAttack`은 단일 타깃 피해 직후 특수 공격 훅 1줄(레지스트리 조회 후 `Apply`)만 호출한다. 신규 특수 유닛은 **핸들러 추가 + 레지스트리 등록 1줄**로 끝내며 `ExecuteAttack`을 다시 수정하지 않는다.
- 피해 로직 단일화: `ExecuteAttack`의 인라인 단일 피해(피해 적용 + 이벤트 발행 + 사망 처리)를 `ApplyDamageToVictim` 헬퍼로 추출하여 **주 타깃과 AoE 대상이 같은 경로**를 쓰게 한다(멀티플레이 HP 동기화 일관성 보장).
- 파일 위치: 계약 / 컨텍스트 / 레지스트리 / 핸들러 모두 `Scripts/Application/Combat/`.

**규칙 24. 휩쓸기형 AoE 판정 = 월드 좌표 전방 부채꼴**
휩쓸기형(도끼병) AoE는 타일 소속이 아니라 **월드 좌표 기준 전방 부채꼴**로 대상을 판정한다.
- 기준 방향(forward) = 공격자 → 주 타깃 방향(월드 XZ). `ExecuteAttack`이 공격 순간 공격자를 주 타깃 방향으로 향하게 하므로 주 타깃은 항상 전방에 포함된다(이동 중의 옛 방향이 아니라 타겟 방향이 기준).
- 피격 조건: 각 적 유닛에 대해 공격자로부터의 **XZ 평면 거리 ≤ `sweepReach`** 이고 forward와 이루는 **각도 ≤ `sweepArcHalfAngle`(반각)** 이면 피격. Y축(UnitYOffset)은 무시한다.
- 겹쳐 붙은 적(거리≈0)은 자연 포함되고, 등 뒤 적은 각도로 자연 제외된다.
- 제외 대상: 아군, 사망 유닛, 공격자 자신, 주 타깃(규칙 16 아군 제외 + 주 타깃 중복 피해 방지). 건물은 AoE 대상이 아니며 주 타깃일 때만 단일 피해를 받는다.
- 월드 좌표는 `IEntityPositionProvider`(서버 권위)로 조회한다(전투 사거리 판정 규칙 6과 동일 소스).
- 순회 중 사망 유닛으로 인한 컬렉션 변경을 피하기 위해 대상을 **먼저 리스트로 수집**한 뒤 일괄 적용한다.

**규칙 25. 특수 공격 튜닝 파라미터 (SpecialAttackConfig)**
특수 공격 수치는 `SpecialAttackConfig` ScriptableObject(`Infrastructure/Config`)로 Inspector에서 편집한다.
- `sweepReach`는 월드 반경, `sweepArcHalfAngle`은 도 단위 반각이다. 기본값·실제 적용값의 숫자는 [StatsReference](../StatsReference.md)의 특수 공격 설정을 참조한다. 당시 기본/실기값 원문은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-03)다.
- 유닛 `attackRange`(주 타깃 공격/추격 거리, UnitStatsConfig)와 특수 AoE `sweepReach`(SpecialAttackConfig)는 **별개 값**이다(혼동 주의).
- GameBootstrapper가 시작 시 SO 값을 읽어 특수 공격 핸들러에 **float 값으로 주입**한다(Application이 Infrastructure SO를 직접 참조하지 않음 — 레이어 규칙 준수). SO 미연결 시 코드 폴백값을 사용한다.
- ⚠️ **에셋 생성 ≠ 씬 배선**: `SpecialAttackConfig.asset`을 만들어 값을 넣어도 GameBootstrapper `_specialAttackConfig`에 연결하지 않으면 런타임은 폴백값을 쓴다. 신규 SO 튜닝값은 배선까지 확인할 것. 현재 에셋은 Inspector로 편집·연결하며 필요할 때 기본 CreateAssetMenu로 생성한다. 과거 `CreateSpecialAttackConfigAsset` 에디터 생성기는 삭제 완료됐으며 재실행 절차가 아니다. 도구 삭제는 현재 설정 재적용이나 배선 변경을 뜻하지 않는다.

**규칙 26. AoE 피격 연출 동시 방출**
동일 서버 타격의 AoE 결과는 같은 attacker instance·sequence·HitIndex 아래 피해자·효과·ResultOrdinal별 정규 키로 구분한다. 서버가 타격 처리 완료 후 발행한 명시적 결과 묶음 또는 완료 manifest의 개수·키를 모두 확인한 경우 같은 표현 Impact에 함께 방출한다.
- 커밋 일정에는 미래 피격 대상 수가 확정되지 않는다. `HitFrameTimes.Length`, 처음 받은 결과, ordinal 최댓값이나 공격자 큐 길이로 묶음 완료를 추정하지 않는다.
- 다중 타격의 HitIndex마다 별도의 묶음을 사용한다. 다른 HitIndex·회차·공격자의 결과를 섞지 않는다.
- 묶음 일부가 유실되면 제한 시간·용량 계약으로 실패를 기록한다. 피해·HP를 되돌리거나 누락 결과를 만들지 않는다. 미완성 묶음을 완전한 AoE 동시 방출 PASS로 보고하지 않는다.
- 이동 파도·Periodic은 접촉/틱별 권위 결과 시각을 유지하며 한 발사 회차 전체를 같은 시점에 모아 방출하지 않는다.
- 레거시 비교 모드의 FIFO는 전환 증거 수집에만 남긴다. 신규 단일 writer 경로의 방출량은 서버 결과 묶음으로 정한다.

**규칙 27. 특수 유닛 Attack 클립 OnAttackHit 이벤트 주입**

실제 생산 clip에 타입별 타격 marker를 authoring하고 `attackTimelineClip`·controller motion·양 팀 prefab·fallback 설정의 연결을 검사한다(규칙17). 남긴 `RunUnitActionSelfValidation`의 production 검증을 사용하며, 과거 Injector·비교 Validator는 삭제 완료됐으므로 재실행하지 않는다. 이 도구 정리는 clip·타격값·서버 권위를 바꾸지 않는다. TorrentSpirit의 발사 marker는1개만 두어 중복 파도를 막는다. QuakeSpirit의 이전 미주입·1.0초 placeholder는 현행 상태가 아니며 현재 생산 marker/config와 개별 수용은 [StatsReference](../StatsReference.md)·[감사표](../Assets/UnitCombatAssetMatrix.md)를 따른다. 주입·교정·도구 삭제 원문은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-09)다.

> **[2026-10-13 MushroomBomber 사실 정정 — 위 「MushroomBomber는 클립에 `OnAttackHit` 1개 주입 완료(2026-07-19)」는 원문 유지]:** 2026-10-09 실측에서 `MushroomBomber_Attack.anim`의 이벤트는 **0건**이었다(`_Tasks/2026-10-09/08_12_mushroombomber-vfx-split/Research.md` 4절) — 2026-07-19 주입이 그 시점 파일에는 남아 있지 않았다. 이후 2026-10-10 VFX 연결 작업(`_Tasks/2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/`)이 **1.0초 `OnAttackHit` 1개**를 추가했다. 2026-10-13 실측 현재: 이 클립(3초, 30fps)의 `m_Events`는 `time: 1`·`OnAttackHit` 1개이고, `MushroomBomber.controller`의 Attack 상태가 이 클립을 쓰며, `UnitStatsConfig`의 MushroomBomber `hitFrameTimes`는 `[1]`·`attackCooldown` 3이다. 그래서 「1개」라는 현재 상태는 파일과 맞는다.
> 같은 연결 작업으로 이 marker의 **역할이 바뀌었다**: MushroomBomber의 1.0초 marker는 이제 로컬 트레이서를 발사하지 않고(옛 호출은 주석 비활성화) 기존 레거시 HP·텍스트 맞춤 신호만 낸다. **[2026-10-20 개정 — 사용자 승인 목표 계약 · 🔴 코드 미반영(미구현)]** 포자 폭탄도 다른 투사체 유닛과 같은 **서버 투사체 기록(Homing)** 이 발사·비행·착탄을 소유한다(규칙 20 — C3 축 분류는 Unresolved 유지, Supported 승격 없음). *(구현 전인 지금 코드는 개정 전 원문대로 서버 writer가 확정한 공격 표현 스냅샷이 트레이서·착탄 이펙트를 소유하고 피해는 규칙 18의 서버 타이머다 — 이 스냅샷 경로는 이행 때 교체한다. 개정 전 원문: [서버 권위 투사체 Plan 4-2-3](../_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#4-2-3-규칙-문서-반영과-개정-전-원문))*

---

### 특수 공격 시스템 규칙 — 확장 (2026-07-17, TorrentSpirit 파도형 이동 AoE + 힐 구현)

**규칙 28. special-only 공격(단일 대상 공격이 없는 유닛)**
일부 특수 유닛은 단일 대상 기본 공격이 아예 없고 특수 로직이 공격 전체를 담당한다(예: TorrentSpirit 파도).
- `ISpecialAttackBehavior.ReplacesPrimaryAttack`가 true이면 `ExecuteAttack`이 주 타깃 단일 피해(`ApplyDamageToVictim`)를 **건너뛰고** 핸들러만 실행한다. false(도끼병 등)면 기존대로 주 타깃 단일 피해 후 핸들러.
- 주 타깃은 특수 로직(파도 등)이 다른 대상과 동일하게 처리하므로 별도 단일 피해가 필요 없다.
- 일반 유닛은 레지스트리에 없어 이 플래그를 조회하지 않으므로 무변경.

**규칙 29. 이동 파도형 AoE(서버 권위 전선 시뮬레이션)**
전방으로 이동하는 파도(TorrentSpirit)는 서버가 전선을 진행시키며 닿는 대상에 효과를 적용한다(규칙 18 준수 — 클라 파티클 위치에 종속 금지).

**2026-10-02 승인 — TorrentSpirit 전용 발사 예약 예외:** 일반 TargetLocked 추적과 원래 타깃 identity는 유지한다. 서버가 승인한 Torrent 공격은 예정 발사 시각 전에 원래 primary가 사망·소멸하거나 사거리에서 이탈해도 예약을 취소하지 않는다. 원래 primary가 살아 있고 조회 가능하면 발사 시 그 대상의 현재 서버 방향을 사용하고, 잃은 경우에는 **같은 원래 대상에 대해 마지막으로 유효했던 서버 aim**을 사용한다. 공격 시작 방향을 고정하는 LockedPoint 전환이나 새 타깃 대체는 아니다. provisional 애니메이션만으로 예약·파도를 생성하지 않으며 발사 전 공격자 사망·게임 종료·despawn/shutdown은 취소한다. 승인 예약은 StopCombat·표시 타깃 교체로 취소하지 않지만 원래 객체 identity를 유지하고 ID 재사용을 다른 대상으로 허용하지 않는다.

예약은 정확히 한 번 서버 파도를 생성하며, 이후 살아 있는 적 유닛·적 건물 피해와 아군 유닛 회복은 아래의 현재 contact 판정으로 적용한다. 죽은 primary의 피해를 위조하거나 Client VFX만 발사하는 우회는 금지한다. 기존 생성 파도의 수명 정책은 유지한다. 이 예외는 `U-TARGET-COMMIT`의 다른 타깃 이전 금지를 지키는 Torrent 전용 계약이지, 모든 TravelingArea/Homing 또는 MeleeContact/Hitscan의 안전 검사를 우회하는 규칙이 아니다. **규칙 승인은 구현·실기 완료나 Supported 승격을 뜻하지 않는다.** 현행 Legacy 월드 aim과 공통 SimulationFacingAim의 차이를 완전 정합으로 주장하지 않는다.

- 핸들러(`TorrentAttackBehavior`)는 파도 모양·방향(공격자→주 타깃, 월드 XZ)만 계산해 `SpawnWave`로 요청하고, 실제 전선 전진·판정·효과는 `UnitCombatUseCase.SpawnWave`/`TickWaves`(`ActiveWave`)가 담당한다.
- 판정 = **월드 좌표 직사각형**(폭 `waveWidth` × 전방 `waveLength`, 타겟 방향). 전선이 `waveTravelTime` 동안 전방 전진하며, 전방 성분 `p ≤ FrontDistance`(전선이 지남) AND `0 ≤ p ≤ Length` AND `|좌우| ≤ HalfWidth`인 대상을 닿음으로 본다.
- **각 대상 1회만**(유닛·건물 각각 별도 hit-set — 유닛 Id와 건물 Id는 카운터가 달라 값 충돌 가능하므로 분리).
- 대상: **적 유닛·적 건물 = 피해(공격력)**, **아군 유닛 = 힐**(건물은 힐 대상 아님). 시전자 자신·죽은 대상 제외.
- ⚠️ special-only 유닛은 반드시 파도 판정이 **건물도 순회**해야 한다 — 안 그러면 성 파괴(승리조건) 기여 불가. (도끼병류는 주 타깃 단일 피해가 건물을 처리하므로 무관.)
- 파도 피해/힐 연출은 대상별 권위 접촉 시각에 연결한다(규칙 26). 레거시 `ImmediatePresentation` 경로의 의미를 보존하되 C3 전환 후에는 접촉별 확정 결과를 단일 Coordinator가 소비하며 발사 marker를 기다리지 않는다.
- 틱 호출: 싱글=`GameBootstrapper.Update`(`!IsNetworkMode` 가드), 멀티=`NetworkCombatController`(IsServer 가드). 이중 틱 금지.

**규칙 30. 힐(회복) 서브시스템**
아군 회복은 피격과 대칭 구조로 처리한다(TorrentSpirit·BloomFairy 공용).
- `UnitData.Heal(amount)`: `Hp = Min(Hp+amount, MaxHp)`, 죽은 유닛엔 무동작.
- 힐 이벤트 `GameEvents.OnEntityHealed`(`EntityHealedEvent`) — 피격(OnEntityDamaged)과 **분리된 채널**(연출/색상 구분).
- 멀티 동기화: `NetworkHealthSync`가 HP **증가(힐)** 도 동기화한다(`SyncHealClientRpc` + 클라 `OnEntityHealed` 재발행). 기존엔 HP 감소만 동기화했으므로 힐 방향을 반드시 함께 열어야 한다.
- 연출: `FloatingHpTextSpawner`가 치유 색상(`_healColor`)으로 표시. 피격 텍스트와 풀·배치 로직 공유. (⚠️ 예외: HoT 누적 회복의 힐 텍스트는 틱마다가 아니라 효과 종료 시 1회만 표시하며 표시값은 즉발 힐과 동일한 현재 HP 형식 — 규칙 37. 즉발 힐은 종전대로 즉시 표시.)

**규칙 31. 다중 파티클 시스템 VFX 재생**

TorrentSpirit 경기별 전달·이동·표현 관측과 미확정은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-04)에 보존한다. ROOT pose 정합은 목표 진행이나 정확한 접촉 시각의 수용을 대신하지 않는다.
VFX 프리팹이 "빈 루트 + 여러 형제 파티클 시스템"으로 구성된 경우(예: TorrentSpirit 파도 — Main_Water_Surge / Ground_Impact_Ripples / Front_Foam_Crest), `ParticleSystem.Play()`는 형제를 재생하지 않으므로 풀 아이템(`VfxPoolItem`)이 **직속 자식 시스템 전부를 각각 재생**해야 한다(하나만 재생하면 일부만 보인다). 루트에 파티클이 있는 프리팹은 기존대로 루트 하나만 `Play(true)`로 하위까지 재생한다. 유닛 공격 VFX는 `EffectPreset`(vfx 프리팹을 감싸는 ScriptableObject)을 만들어 `UnitEffectConfig`의 해당 UnitType `attackPreset`에 연결한다.

TorrentSpirit은 `MatchEpoch + WaveDeliveryId` adapter로 실제 서버 spawn 위치·방향·시각과 확정 contact를 전달하며 `WavePresentationPresenter`가 파도와 확정 접촉 HP 텍스트를 같은 시계로 소비한다. 새 owner 활성 시에만 로컬 marker VFX/SFX·Legacy text의 중복 방출을 억제한다. HP는 즉시 적용하고 서버 전선은 실제 spawn 뒤 경과시간으로 진행한다. 원본 VFX·clip·서버 폭/길이/이동 시간은 유지한다. 기하 경계는 alpha 윤곽이 아니며 서버 stall·정확 접촉 동시성·성능 해결을 보장하지 않는다. Unresolved/LegacyFallback 유지와 개별 미확정 범위는 [감사표](../Assets/UnitCombatAssetMatrix.md), 표현 수치는 [StatsReference](../StatsReference.md)를 따른다. 전 경기 causal 로그와 overflow 진단은 관측용이며 gameplay cap이 아니다. 당시 cap·로그 수정·경기·빌드 미관찰 원문은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-05)다.

> **2026-10-13 추가 — EffectPreset 「Stop When Caster Dies」(시전자 귀속 공격 VFX):** `EffectPreset`에 이 옵션이 생겼다. 켜면 그 preset으로 재생한 **공격 VFX**는 공격한 유닛(시전자)에게 귀속돼, 시전자가 **죽거나 제거(파괴·디스폰·비활성화)되면 즉시 정지**하고(새 파티클 생성 중지 + 이미 나온 파티클도 즉시 삭제) 풀로 돌아간다. 기를 모으는 차지처럼 시전자 몸 앞에서 재생되는 연출에만 켠다. 현재 켜진 preset: `EffectPreset_FoxMagician_Attack`(`vfx_foxmagician_charge`)·`EffectPreset_InfernoSpirit_Attack`(`vfx_infernospirit_charge`).
> - 읽는 경로는 `UnitEffectConfig` `attackPreset` 재생뿐이다. 트레이서·착탄·사망 VFX, 타워 번개, 화상·독·힐 상태 이펙트는 이 옵션의 영향을 받지 않는다(규칙 19 본문대로 사망 VFX는 별도 수명).
> - 정지는 그 시전자가 소유한 이펙트에만 적용되며 같은 preset을 쓰는 다른 유닛의 이펙트는 멈추지 않는다. 각 화면이 자기 유닛 화면 컴포넌트로 정지한다(사망 이벤트 + 제거 시점 즉시 정지, 그리고 매 프레임 시전자 상태 확인 안전망).
> - **표현 전용이다.** 서버가 이미 확정한 결과·피해·착탄/HP 텍스트 표현을 취소하거나 바꾸지 않는다(규칙 19 「이미 서버가 확정한 결과를 취소하지 않는다」, `GameSystemRules_UnitCombatSynchronization.md` NET-AUTH-001).
> - 상태: 구현 2026-10-13(`Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.CasterOwned.cs`), 사용자 실기 확인 PASS(2026-10-13 2차 실기). 상세: `_Tasks/2026-10-13/13_53_vfx-code-followups/Plan.md` 10절 R2-1.

---

### 특수 공격 시스템 규칙 — 힐러 확장 (2026-07-18, BloomFairy 꽃요정 힐러 구현)

BloomFairy는 지금까지의 특수 유닛과 근본적으로 다른 **힐러**다. 적을 때리지 않고 부상당한 아군을 회복시킨다. 기존 전투 흐름(적 감지 → 공격)에 얹을 수 없어 **힐러 전용 경로**를 신설했고, 그 과정에서 만든 시간 지속 효과 시스템(HoT/DoT)은 이후 지속 피해(DoT) 유닛(InfernoSpirit·MushroomBomber 등)이 공용으로 재사용한다. 규칙 28~31(TorrentSpirit 파도·힐 서브시스템)을 전제로 한다.

**규칙 32. 힐러 전용 경로 (적 공격 흐름과 분리)**

BloomFairy는 아래 SeparateHeal 계약을 따른다. 개별 현재 수용·미확정은 [감사표](../Assets/UnitCombatAssetMatrix.md)에서, 단계별 코드·자동 gate·빌드 시도·실기 분석은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-06)에서 확인한다. 자동 PASS와 빌드 클릭은 사용자 최종 시각 수용이 아니다.

힐러(BloomFairy)는 적 주 타깃이 없어 `ExecuteAttack`/`ISpecialAttackBehavior`(적 공격 흐름)·`SpecialAttackRegistry`에 **등록하지 않는다**. 대신 상태머신이 "이 유닛은 힐러"임을 데이터로 인식하고 힐 루프를 타는 **독립 경로**로 처리한다.
- special-only(단일 대상 공격 없음)이지만 TorrentSpirit(규칙 28, special-only이나 적을 때림)과 달리 **대상이 아군**이라 적 공격 파이프라인과 완전히 분리된다.
- 힐러 식별은 UnitType 하드코딩이 아니라 데이터(역할/힐러 플래그)로 분기하여 향후 지원 유닛이 재사용할 수 있게 한다.
- 힐 발동은 애니 이벤트 `OnAttackHit`이 아니라 상태머신의 `HitFrameTimes` 타이머로 구동한다(데미지와 동일 관례 — `OnAttackHit()`은 이 코드베이스에서 데미지를 발동하지 않고 VFX/SFX·피격 연출만 담당하며, 실제 데미지도 서버 권위로 `HitFrameTimes` 타이밍에 적용된다). `OnAttackHit`을 힐 발동에 쓰면 데미지와 메커니즘이 갈리고 클립에 이벤트 미주입 시 회복이 영영 발동 안 되는 취약점이 생기므로, `OnAttackHit`은 힐 연출용으로만 남긴다.
- **SeparateHeal 표현 계약 (2026-10-03):** 힐 시작/종료는 서버/싱글 힐 루프만 발급하며 규칙22의 atomic current-state 복제로 전달한다. 일반 EnemyAttack/C3 공격 회차·lease와 별도의 lifetime/sequence/revision cursor를 사용한다. 새 힐 sequence의 시전 재생은 일반 공격의 연속 재생·커밋 시 되감기 금지에 대한 **힐러 전용 분류**이며 다른 타입의 restart 허가가 아니다. `NotApplicableAttack / SeparateHealPipeline`을 유지하고 가짜 정규 공격 scope/결과를 만들지 않는다.
- 이미 지난 힐 source marker는 catch-up에서 재방출하지 않고 회차당 최대1회만 소비한다. Frozen/Held는 힐 모션보다 우선하며 모션 종료는 서버 HoT 효과 수명을 종료하지 않는다. 이전3초 clip 끝자세 hold 정책은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-04)이며 아래 현재 정책을 따른다.
- **힐 표현 정책:** 새 유효 힐 sequence는 중간 inactive Walk/Held 없이 이어지고 실제 Frozen/Held·대상 없는 대기·사망·경기 종료는 유지한다. 서버 Action 소유권과 활성 priority1 아래 공통 회전을 사용하며 Client Root/Visual offset writer는 금지한다. future-start는 IsDue까지 기다리고 late-init catch-up·one-shot marker·sequence/revision/lifetime 멱등·terminal 차단을 지킨다. priority cache는 실제 표현 적용 뒤 갱신한다. 힐러 config cooldown은 clip 길이로 덮어쓰지 않으며 규칙36의 준비1초+발동후3초=4초 주기를 유지한다. 현재 clip4초·marker1초·시험 blend0.15초와 HoT 수치는 [StatsReference](../StatsReference.md), 과거 retime/에셋 적용·코드 비활성화 절차는 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-05)를 따른다.
- 힐 발동은 같은 `HealNow`에서 accepted `StartedAt + MarkerOffset`의 absolute due를 기다리고 yield 뒤 재조회한다. caster/authority/동일 active 회차·finite time을 확인한 뒤 기존 `CastHeal`을 단일 실행한다. 실제 발동과 configured cooldown deadline/Remaining 양 조건을 보존하며 entry/lifecycle owner를 보호한다. 이전 dt wait·endpoint hold의 코드 정리 상태와 단계별 실행 근거는 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-07)를 따른다.
- 회차 종료는 active=false와 종료 시각/사유로 복제하며 종료된 같은 회차를 되살리지 않는다. 사망·게임 종료·disable/despawn의 terminal close는 지연 callback을 차단한다. **모션 종료와 이미 부여된 HoT 효과 종료는 다른 수명**이다. Client marker/Animator/종료 신호가 `CastHeal`, HP, HoT 틱 또는 효과 결과를 만들거나 기존 효과를 취소하지 않는다. Aim payload는 방향 증거이고 Client Simulation Root/NetworkTransform·Visual offset 회전을 쓰지 않는다.
단계별 구현·이전 경기 관측은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-08)에 보존한다. 전달·ROOT PASS를 heal continuity/facing·Client HP/text 전수 수용으로 확대하지 않는다.

**규칙 33. 부상 아군 탐색 (팀 필터 반대)**
힐 대상은 기존 적 탐색(`unit.Team == self.Team || !IsAlive → 제외`)의 **팀 필터를 반대로** 한 아군 탐색으로 찾는다(기존 적 탐색 메서드는 무변경 — 회귀 방지, 아군 탐색은 별도 메서드로 신설).
- 조건: **같은 팀** AND **살아있음** AND **부상(`Hp < MaxHp`)**.
- **본인 포함**(자가 회복 허용) — 적 탐색과 달리 self를 후보에서 제외하지 않는다.
- **아군 유닛만** — 건물은 힐 대상이 아니다.
- 사거리: 힐/감지 사거리 4.0, 월드 좌표(`IEntityPositionProvider`, 규칙 6과 동일 소스).
- 우선순위: **잃은 체력 비율 `(MaxHp − Hp) / MaxHp` 최대** 우선, **동률이면 거리 최소**.

**규칙 34. HoT/DoT 공용 시간 지속 효과 시스템 (서버 권위 diff 틱)**
HoT recipient 표현은 실제 효과 레코드의 provenance/current-state를 따르고 단일 presenter가 대상 수명의 loop를 소유한다 — 대상 프리팹의 **전 파티클 시스템**을 loop로 재생하며, 파티클 시스템이 **0개일 때만** 거부하고 각 시스템마다 loop ON · Stop Action None · Local 시뮬레이션 · burst 0 · unscaled time 아님을 검사해 하나라도 어기면 재생 전에 거부한다(`VfxPoolItem.PlayBloomLoop`, 개수 고정 없음). *(2026-10-14 정정, 2026-10-19 병합 반영: 종전 표기 「all5PS」는 옛 프리팹(5개, `Bloom_Ring` 포함) 기준이었다. 프리팹 `vfx_bloomfairy_heal.prefab`은 2026-10-13 사용자 결정으로 `Bloom_Ring`을 빼 현재 4개다 — 커밋 `d3f9abb1`, `_Tasks/2026-10-13/13_53_vfx-code-followups/Plan.md` 4-A)* 같은 owner refresh는 restart하지 않으며 실제 종료 뒤 StopEmitting·natural drain·lease 반환을 구분한다. late-ready/lifecycle 보호를 유지하고 강제 teardown을 자연 종료 수용으로 해석하지 않는다. *(2026-10-15: PS 4개 프리팹의 힐 이펙트 재생과 대상 크기 맞춤은 사용자 실기 확인 PASS — `_Tasks/2026-10-13/13_53_vfx-code-followups/Research.md` 10절. 크기·높이 값은 사용자가 프리셋으로 계속 조정 중이며, 나머지 OPEN 축(강제 teardown·Client C3 failures64)은 바뀌지 않았다)* 개별 연결·미해결은 [VFXSFXList](../Assets/VFXSFXList.md), 당시 구현/관측·C3 진단은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-06)를 따른다.

"유닛에 붙는 시간 지속 효과"(회복 HoT / 피해 DoT)를 서버 권위로 관리하는 공용 시스템. BloomFairy(HoT) + 후속 DoT 유닛이 공용으로 재사용한다.
- 배치 레이어: `Application`(규칙 29 파도 틱과 대칭). 파도(`ActiveWave`/`TickWaves`)와 동일한 소유·틱 패턴을 따른다.
- 효과 레코드(대상별): 대상 UnitId, 종류(Heal/Damage), 남은 시간, 총량/지속, 누적 적용량. **대상별 동종 효과 1개만** 유지한다.
- **diff 틱 방식:** 이번 틱 적용량은 지금까지 적용됐어야 할 누적량에서 이미 적용한 량을 뺀 차이다. 분할·반올림 오차를 마지막 틱에서 정산하여 설정 총량에 도달한다. HoT는 연속 모드이고 DoT는 양수 TickInterval의 discrete 모드(규칙40)다. 실제 회복량·지속 수치는 [StatsReference](../StatsReference.md), 도입 당시 회복량 계산 예시는 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-07)다.
- Heal → `UnitData.Heal` + `OnEntityHealed`(규칙 30 인프라 재사용). Damage(후속 DoT) → 기존 피해 경로(`ApplyDamageToVictim` 계열) + `OnEntityDamaged`.
- **갱신 = 리셋(중첩 없음)**: 동일 대상에 동종 효과가 이미 있으면 남은 시간·총량을 리셋한다(스택 X).
- 종료 처리: 대상 사망 / 풀피 도달(HoT) / 지속 만료 시 레코드 제거. (HoT의 힐 플로팅 텍스트는 이 종료 시점에 회복 후 현재 HP로 1회 표시하되 대상 사망 시엔 생략 — 규칙 37.)
- 서버 틱 진입점(규칙 29 계승): 싱글=`GameBootstrapper.Update`(`!IsNetworkMode` 가드), 멀티=`NetworkCombatController`(IsServer 가드). **이중 틱 금지** — 파도 `TickWaves` 호출 지점 옆에 추가한다.

**규칙 35. 힐러 유휴 감시 (경로 끝 도달 후 지속 감시)**
힐러는 A* 경로의 끝(적 진영 앞)에 도달해 더 이동할 곳이 없어도 **부상 아군을 계속 감시**해야 한다.
- 일반 공격 유닛은 경로 끝에서 적을 감지하면 전투에 들어가지만, 힐러가 경로 끝에서 감시를 멈추면 **최전선에서 아무 일도 안 하고 서 있는 유휴 상태**가 된다(QA 이슈2에서 발견).
- 경로 종료 후에도 유휴 감시 루프(`HealerIdleWatchV3`)를 돌려, 사거리 내 부상 아군이 생기면 즉시 힐 루프로 진입한다. 부상 아군이 없으면 대기, 생기면 힐 → 이 사이클을 반복한다.

**규칙 36. 힐러 쿨다운 예외 — 발동 준비 미포함 (총 힐 주기 4.0s)**
⚠️ **이 프로젝트의 유일한 쿨다운 예외다. 되돌리지 말 것.**
- **다른 모든 유닛:** `AttackCooldown`은 발동 준비를 포함한 전체 주기다. 공격 시작 순간부터 cooldown을 세고 `HitFrameTimes`는 그 구간 안에 둔다. 개별 현재 주기·타격 시각은 [StatsReference](../StatsReference.md), 과거 LittleKnight 예시는 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-18)다.
- **BloomFairy(힐러)만 예외**: `AttackCooldown`(3.0s)이 힐 발동 준비(1.0s)를 **포함하지 않는다**. 힐이 실제로 발동(`HitFrameTimes[0]`=1.0s)된 **뒤에** 쿨다운 3.0s가 시작된다. → **실제 힐 주기 = 발동 준비 1.0s + 발동 후 쿨다운 3.0s = 4.0s**.
- 이것은 **의도된 설계이며 버그가 아니다.** 힐러는 "힐을 한 번 걸고 나서 다음 힐까지 3초를 쉰다"는 체감이 핵심이라, 발동 준비를 쿨다운에 포함시키지 않고 발동 후부터 카운트한다.
- ⚠️ 향후 누군가(사람 또는 AI)가 "BloomFairy만 다른 유닛과 쿨다운 계산이 다르다"고 오인해 다른 유닛과 똑같이(발동 준비 포함) 되돌리면 힐 주기가 3.0s로 짧아져 밸런스가 깨진다. **이 예외는 확정 설계이므로 유지한다.** (StatsReference.md BloomFairy 행 비고와 동일 내용.)

**규칙 37. HoT 힐 텍스트 집계 (완료 시 1회 표시·사망 시 생략)** (2026-07-19 신설, BloomFairy HoT 힐 텍스트 표시 방식 변경 — 실기 확정)
HoT의 HP는 틱마다 서서히 회복하고 HP바·멀티 동기화는 유지한다. 텍스트만 효과 종료 때 회복 후 현재HP(절대값)로1회 표시하여 틱 숫자 도배를 막고 즉발/파도 힐 형식과 통일한다. 현재 회복 수치는 [StatsReference](../StatsReference.md), 도입 당시 회복량 예시·표시 변경 과정은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-08)다.
- **틱 힐 텍스트 억제**: HoT의 각 회복 틱은 힐 플로팅 텍스트를 띄우지 않는다. 단 HP바 갱신·멀티 동기화를 위한 힐 이벤트는 종전대로 발행한다(회복 자체·HP바 상승·멀티 반영은 전혀 바뀌지 않는다 — 오직 텍스트만 skip).
- **완료 시 1회 표시**: HoT가 **정상 종료**(대상 풀피 도달 또는 지속시간 만료 — 규칙 34 종료 처리)될 때, **회복 후 현재 HP(절대값)로** 힐 텍스트를 **1회** 띄운다(기존 즉발/파도 힐과 동일한 표시 형식).
- **사망 시 생략**: 회복 도중 대상 유닛이 **사망**하면(규칙 34 종료 사유 중 대상 사망) 힐 텍스트를 **표시하지 않는다**.
- **적용 범위 = HoT 경로 한정**: TorrentSpirit 파도의 즉발 힐, 기타 즉발 힐, 그리고 모든 데미지 텍스트는 **무변경**이다(종전대로 발생 즉시 표시). 이 집계 규칙은 시간 지속 회복(HoT) 경로에만 적용된다.
- 구현(참고): 힐 이벤트 `EntityHealedEvent`에 `ShowText` 플래그를 둔다 — HoT 각 틱은 `ShowText=false`(HP 동기화 이벤트는 발행하되 텍스트 skip), 효과 정상 종료 시 `ShowText=true`로 1회 발행하며 **표시값은 기존 즉발/파도 힐과 동일하게 회복 후 현재 HP(절대값)** 다(표시 전용 `HealAmount` 플래그는 두지 않는다). `NetworkHealthSync`가 이 `ShowText` 플래그를 `SyncHealClientRpc`로 전파해 멀티에서도 완료 시 1회만 표시. 실제 회복량은 `ActiveTimedEffect.ActualHealed`에 틱마다 누적하며, 이 값이 **`>0`일 때만(실제 회복이 있었을 때만) 완료 텍스트를 표시**하고 **재부여(갱신) 시 리셋**한다(규칙 34 "갱신 = 리셋"과 정합 — 리셋된 새 효과가 끝날 때만 완료 텍스트).

---

### 특수 공격 시스템 규칙 — 착탄형 DoT 확장 (2026-07-19, MushroomBomber 버섯폭격기 착탄형 범위 딜러 구현)

MushroomBomber는 착탄 중심의 적 유닛에게 DoT를 부여하고 주 타깃에는 일반 직접 피해를 먼저 적용하는 착탄형 범위 딜러다. 원형 반경·1초 discrete 틱·대상별 적용 계약은 규칙38~40을 따른다. 직접 피해·DoT 초당량·지속·반경의 현재 값은 [StatsReference](../StatsReference.md), 초기 ×10 이전 수치와 구현 과정은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-09)다.

**규칙 38. 착탄형 AoE 판정 = 월드 좌표 원형 반경 (도끼병 부채꼴의 arc 없는 버전)**
착탄형(MushroomBomber) AoE는 타일 소속이 아니라 **착탄 중심 기준 월드 좌표 원형 반경**으로 대상을 판정한다. 휩쓸기형(규칙 24)과 판정 소스·안전 패턴은 같고 **모양만 부채꼴 → 원형**으로 다르다.
- **판정 중심 = 주 타깃(착탄 지점) 월드 위치** — 도끼병(공격자 위치 중심)과 달리 폭탄이 떨어진 자리(주 타깃)가 중심이다. 주 타깃은 유닛/건물 무관하게 착탄 중심 좌표를 제공한다.
- 피격 조건: 각 적 유닛에 대해 착탄 중심으로부터의 **XZ 평면 거리 ≤ `blastRadius`(월드 반경)** 이면 피격. Y축(UnitYOffset)은 무시하며, 각도(arc) 판정은 없다.
- **주 타깃 미제외:** 주 타깃 적 유닛도 반경0으로 DoT에 포함하고 일반 직접 피해와 함께 받는다(규칙39). 아군·사망 유닛·공격자 자신은 제외한다. 초기 직접 피해 수치 예시는 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-10)다.
- **건물은 순회 대상이 아님** — 핸들러가 유닛 목록만 순회하므로 건물은 자동 제외된다(DoT 없음 — 규칙 39).
- 월드 좌표는 `IEntityPositionProvider`(서버 권위, 규칙 6·24와 동일 소스). 순회 중 사망으로 인한 컬렉션 변경을 피하기 위해 대상을 **먼저 리스트로 수집한 뒤 일괄 적용**한다(규칙 24와 동일).
- 반경 판정 수집부(`CollectEnemyUnitsInRadius`)는 **static 헬퍼로 분리**되어 있어, QuakeSpirit(또 다른 착탄형)이 이 수집 결과 위에 배율을 얹어 재사용한다(순수 "수집"만 공용화). **재사용 사례(2026-07-20):** QuakeSpirit(규칙 43)이 이 헬퍼를 `internal static`으로 공용화해 반경 내 적 유닛 수집에 그대로 재사용하고(MushroomBomber 유닛만 로직 무변경), 그 위에 주 타깃 100%/나머지 50%(올림) **고정 배율**을 얹었다(거리 감쇠 아님). 건물용 `CollectEnemyBuildingsInRadius`는 신설해 유닛/건물 hit-set을 분리한다(규칙 29 계승).

**규칙 39. 주 타깃 직접 피해 + DoT AoE 특수 핸들러의 역할 분담**
일반 직접 피해와 특수 DoT는 별개 경로다. 직접 피해를 먼저 처리하고 `ReplacesPrimaryAttack=false`인 특수 핸들러가 착탄 주변 적 유닛에 DoT를 부여한다.
- 주 타깃이 적 유닛이면 직접 피해+DoT, 주변 적 유닛이면 DoT만 받는다.
- 주 타깃이 건물이면 직접 피해만 받되, 주 타깃 종류와 무관하게 주변 적 유닛 DoT 수집은 실행한다. 건물은 DoT 대상이 아니다.
- 아군은 직접/DoT 모두 피해를 받지 않는다.
직접 피해와 DoT의 현재 수치는 [StatsReference](../StatsReference.md), 도입 당시 직접 피해 예시 원문은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-11)다.

**규칙 40. DoT "초 단위 틱" 모드 (HoT/DoT 공용 시스템 규칙 34 확장)**

Mushroom 착탄 표현은 짧은 포자 확산 burst이며3초 지면 독구름/damage-zone이 아니다. 실제 서버 DoT3초·착탄 때 한 번 수집한 대상·HP writer·틱·범위·갱신 정책을 유지한다. 대상 poison은 실제 효과 수명을 따라 붙고 refresh로 끊거나 restart하지 않는다. 취소·만료·사망 뒤 방출 정지·natural drain·lease 반환을 구분하며 VFX 종료가 서버 DoT 종료나 새 피해를 만들지 않는다. 기존3초 clip/cycle·Stats와1초 OnAttackHit1개, tracer 종료 전용·동일 attack exact identity impact 단일 재생 계약을 유지한다. source freeze·정적·native 미실행·인계 원문은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-10), 개별 현재 연결·미확정은 [VFXSFXList](../Assets/VFXSFXList.md)·[감사표](../Assets/UnitCombatAssetMatrix.md)를 따른다.

공용 시간 지속 효과는 HoT 연속 diff와 DoT discrete 틱으로 구분한다. TickInterval=0은 연속 HoT, 양수는 discrete DoT이며 HoT 경로는 유지한다.
- DoT는1초 간격에 누적된 틱을 한 번씩 적용한다. 틱당 피해는 `CeilToInt(초당 피해 × 틱 간격)`이며 최소1을 보장한다.
- 총 피해 상한은 `Round(초당 피해 × 지속시간)`이다. 틱당 올림 결과가 누적되어도 설정 총량을 초과하지 않도록 clamp한다.
- 각 틱은 기존 피해 이벤트를 발행하여 남은 현재HP 텍스트를 표시한다. HoT 완료1회 텍스트와 다르며 파도/일반 텍스트는 바꾸지 않는다.
- 서버 타이머가 피해를 소유한다. 싱글은 로컬 권위, 멀티는 서버에서만 같은 효과를 틱하며 이중 적용하지 않는다.
- 동일 대상 동종 DoT 재부여는 남은 시간·총량·누적량·틱 상태를 리셋하고 stack하지 않는다. 틱 피해로 사망하면 기존 사망 처리 뒤 효과 레코드를 제거한다.
- Mushroom의 초당량·지속·반경은 SpecialAttackConfig에서 주입하고 Inferno는 별도 진입점/값을 사용한다(규칙42). 미연결 시 fallback을 쓰므로 생성만이 아니라 배선까지 확인한다.
현재 수치는 [StatsReference](../StatsReference.md), 도입 당시 DoT 예시·과거 공용화 구현 기록은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-12)다.

---

### 특수 공격 시스템 규칙 — 단일 대상 DoT 확장 (2026-07-20, InfernoSpirit 지옥불 정령 단일 대상 지속 피해 구현)

InfernoSpirit는 일반 원거리 직접 피해 뒤 주 타깃 적 유닛1명에게만 DoT를 추가한다. 착탄 반경이 없는 단일 대상이며 공용 discrete 틱 계약은 규칙40을 재사용한다. 피해·기간의 현재 값은 [StatsReference](../StatsReference.md)를 따른다.

**규칙 41. InfernoSpirit 단일 대상 on-hit DoT — 직접 피해와 특수 핸들러**
- 일반 직접 피해를 먼저 적용하고 `ReplacesPrimaryAttack=false`인 DoT 핸들러를 실행한다. 주 타깃 건물은 직접 피해만 받아 공성이 성립한다.
- DoT는 반경 수집 없이 같은 주 타깃 적 유닛1명만 대상으로 한다. 건물·아군을 제외하고 직접 피해로 이미 사망했으면 부여하지 않는다.
- 규칙40의1초 discrete 틱·올림/최소1·총량 clamp·각 틱 현재HP 텍스트·서버 권위·이중 틱 금지·갱신 리셋을 따른다.
도입 당시 직접 피해·DoT 예시와 구현 경위는 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-13)에 보존한다.

**규칙 42. 유닛별 DoT 값 분리 (별도 필드·델리게이트·진입점)**

Inferno burn은 기존 source attack VFX와 별도의 실제 DoT recipient 표현이다. 실제 효과의 부여·갱신·취소·만료·대상 사망을 따르며 같은 owner refresh는 noRestart, poison↔burn/다른 Damage overwrite는 이전 표현 종료·교체를 따른다. 방출 정지·drain·lease 반환을 구분하고 HP writer·대상·기간·틱·수치와 Mushroom 값 분리를 유지한다. 기존3초 clip/cycle·Stats·1초 OnAttackHit1개와 tracer 종료 전용·동일 attack exact identity impact 단일 재생 계약을 따른다. 이전 source VFX 수용을 새 burn 수용으로 재사용하지 않는다. source-freeze·정적·native 미실행 인계 원문은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-11), 현재 연결·수용 범위는 [VFXSFXList](../Assets/VFXSFXList.md)·[감사표](../Assets/UnitCombatAssetMatrix.md)를 따른다.

Mushroom과 Inferno는 공용 틱 계산을 쓰지만 진입점과 초당량·지속 필드를 분리한다. 한 유닛 설정 변경이 다른 유닛에 새지 않아야 한다. Mushroom은 `ApplyBlastDot`/`ApplyDot`/`_blastDot*`, Inferno는 `ApplyInfernoDot`/`_infernoDot*` 경로로 실제 값을 주입하며 Inferno에는 AoE 반경이 없다. 현재 Config·fallback 값은 [StatsReference](../StatsReference.md), 도입 당시 기본값과 배선 기록은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-14)다.

---

### 특수 공격 시스템 규칙 — 착탄형 즉발 AoE 확장 (2026-07-20, QuakeSpirit 대지의 정령 착탄형 즉발 범위 딜러 구현 — 특수 유닛 5종 전량 완료)

QuakeSpirit는 착탄 순간 주 타깃 직접 피해와 주변 스플래시를 즉시 적용하는 범위 딜러다. 지속 DoT와 구분한다. 현재 공격력·반경은 [StatsReference](../StatsReference.md)를 따른다.

**규칙 43. QuakeSpirit 착탄형 즉발 2단계 AoE (직접 100% + 스플래시 50%, 건물 포함)**
- 일반 직접 피해100%를 주 타깃에 먼저 적용하고 `ReplacesPrimaryAttack=false`인 특수 핸들러를 실행한다. 건물도 주 타깃이 될 수 있다.
- 주 타깃 위치를 중심으로 XZ 월드 원형 반경의 적 유닛·적 건물을 먼저 수집하여 즉시50% 스플래시를 적용한다. DoT·거리 감쇠·부채꼴 판정은 아니다.
- 스플래시는 `CeilToInt(공격력 × splashRatio)`로 올림한다. 주 타깃은 이미 직접 피해를 받았으므로 제외한다. 아군·사망 대상·공격자 자신도 제외한다.
- 유닛·건물ID는 카운터가 달라 충돌할 수 있으므로 hit-set을 각각 분리한다. Mushroom의 적 유닛만 DoT 수집 계약은 변경하지 않는다.
- 판정·피해는 서버 타이머가 소유하고 Client는 확정HP/표현만 수신한다. 규칙26의 완료 결과 묶음을 확인하여 권위 착탄 표현 시각에 폭발과 함께 표시한다.
- 반경과 splashRatio를 Config에서 주입하며 에셋 생성과 씬 배선은 별개다. 현행 필드값·fallback은 [StatsReference](../StatsReference.md)를 따른다.
도입 당시 공격력·스플래시 예시·공용 수집부 추가·당시 실기 원문은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-15)다.

QuakeSpirit의 이전 marker 미주입·placeholder로 인한 표시 지연과 후속 자동 검증·빌드 시도는 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-12)다. 현재 marker/config·프로필·개별 사용자 수용은 [감사표](../Assets/UnitCombatAssetMatrix.md)를 따르며 과거 Android 실기 대기를 현재 미수용으로 되돌리지 않는다.

---

<a id="전투-규칙--방어력-데미지-감쇄-연구소-유닛-강화-시스템-구현-완료--멀티-실기-pass"></a>

## 전투 규칙 — 방어력 데미지 감쇄

방어 감쇄는 아래 규칙44의 전투 계약이며 강화 시스템 전체는 [Upgrade](GameSystemRules_Upgrade.md), 개별 현재 구현·검증 범위는 [PROJECT_STATUS](../PROJECT_STATUS.md)를 따른다. 이전 기능/멀티 실기 상태 원문은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-23)이며 해당 PASS를 미검증 범위로 확대하지 않는다.

연구소 강화의 신규 스탯 **방어력**은 받는 피해를 비율로 줄인다. 모든 유닛은 방어력 0에서 시작하며(연구 전에는 감쇄 0% → 기존과 완전히 동일), 오직 연구로만 올린다. 방어력 감쇄는 유닛이 피해를 받는 **직격·스플래시 최종 데미지 지점**(타워→유닛 데미지 포함)에 동일하게 삽입되며, **DoT 틱값에는 미적용**한다(2026-07-23 확정).

**규칙 44. 방어력 데미지 감쇄 공식 — 모든 최종 데미지 지점 일괄 적용 (구현 완료)**
전투 최종 데미지를 피격 대상의 방어력으로 비율 감쇄한 뒤 적용한다. 구현: 순수 함수 `Domain/Combat/DamageCalculator.ApplyDefense(rawDamage, defense)`.
- **공식:** `데미지 = Max(1, Round(공격력 × (1 − 방어력 / (방어력 + 120))))`. K = 120(`DamageCalculator.DefenseConstantK`), 방어력 트랙 Lv0~5 = 0/8/16/24/32/40.
- **최소 데미지 1 보장(floor) 필수.** 감쇄율 하드캡 65%(`DamageCalculator.MaxMitigationRate`)를 미래 확장 안전장치로 코드에 포함(현 Lv5는 25%로 미도달). `rawDamage<=0`이면 그대로 반환(0공격력 유닛 회귀 방지).
- **삽입 지점 = 직격·스플래시 최종 데미지 수렴점 일괄** — 직격(`ApplyDamageToVictim`), 스플래시/파도(`ApplyFixedDamageToVictim` 경유 — QuakeSpirit·TorrentSpirit·BattleAxe), 그리고 **타워→유닛 데미지**(`TowerCombatUseCase.ExecuteTowerAttack`)까지 전부. 유닛 수렴 헬퍼는 `UnitCombatUseCase.ComputeFinalDamage`(Tank/CannonCart 건물 2배 분기 포함). **DoT 틱(`ApplyBlastDot`/`ApplyInfernoDot`, 규칙 40·42)에는 미적용**(아래 참조).
- **방어력은 피격 대상 팀의 연구 레벨로 결정** — 데미지 계산 시 대상(`UnitData`)의 팀·그룹으로 방어 레벨을 조회해 감쇄를 적용한다((B) 실시간 배율, `GameSystemRules_Upgrade.md` 규칙 4).
- **DoT 틱값에는 미적용 (2026-07-23 확정 — 기존 "DoT에도 적용"을 뒤집음):** 방어력 감쇄는 **직격·스플래시에만** 적용하고 DoT 틱값(MushroomBomber·InfernoSpirit)에는 적용하지 않는다. 공격력 연구도 고정 DoT 틱값에는 미반영이므로(규칙 6, Upgrade), DoT는 공격력 연구·방어력 연구 어느 쪽에도 영향받지 않는 고정 틱값이다.
- **타워 데미지 경로(포함):** 방어 타워가 유닛에게 주는 피해도 유닛 방어 감쇄 대상이며 직격과 동일 헬퍼를 쓴다([건물 규칙](GameSystemRules_Buildings.md) 방어 타워 규칙9). 도입 전 감쇄 미적용 상태 표기는 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-17)다.
- **하위호환:** 방어력0이면 감쇄율0이며 `DamageCalculator.ApplyDefense`는 `defense<=0`일 때 원본 피해를 반환한다. 연구 전 피해가 반올림/floor로 달라지지 않아야 한다. 당시 직격·스플래시·타워 회귀 실기 범위는 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-units-24)다.
