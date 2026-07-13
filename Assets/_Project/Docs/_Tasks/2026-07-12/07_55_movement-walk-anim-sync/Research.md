# Research — 이동/Walk 애니메이션 동기화 버그 수정

**작업일:** 2026-07-12
**로드맵:** Phase F-1 (전투 타격 타이밍 동기화 후속) — 🔴 높음
**선행 작업:** `_Tasks/2026-07-09/01_12_combat-hit-timing-sync/` (전투 타격 타이밍 동기화, 실기/로그 검증 PASS)

---

## 이 작업은 무엇이고 왜 하는가 (비개발자용 설명)

멀티플레이에서 유닛이 움직일 때, **내 화면과 상대 화면이 서로 다르게 보이는 문제**가 있습니다. 대표적으로 두 가지 증상이 보고됐습니다.

1. **걷는데 걷는 모션이 안 나온다** — 유닛은 분명히 앞으로 이동하는데 다리 애니메이션(Walk)이 재생되지 않고 미끄러지듯 움직입니다. 특히 **방금 생산된 유닛**, 그중에서도 **생산되는 순간 이미 사거리 안에 적이 있던 경우**에 자주 발생합니다.
2. **유닛이 뒤로 밀리듯 이동한다** — 유닛이 잠깐 뒤로 당겨졌다가 다시 가는 것처럼 보입니다. 유닛이 **빽빽하게 몰려 싸우는 지역**에서 자주 발생합니다.

이 문제가 생기는 근본 이유는, 현재 게임이 걷기·공격 같은 **애니메이션 상태를 "신호 한 번"으로만 전달**하기 때문입니다. 서버(방장)가 "지금부터 걸어!"라는 신호를 딱 한 번 보내는데, 그 신호가 도착하는 순간에 상대방 화면 쪽 유닛이 아직 준비(구독)가 안 되어 있으면 신호를 놓치고, 그 뒤로는 다시 알려주는 신호가 없어서 **틀린 상태에 계속 갇혀** 있게 됩니다. 갓 생산된 유닛이 특히 취약한 이유가 바로 이 "준비 되기 전에 신호가 먼저 도착하는" 타이밍 문제(스폰 레이스) 때문입니다.

이 작업의 목표는 **애니메이션 상태를 "신호 한 번"이 아니라 "현재 상태 값 자체"로 공유**하도록 구조를 바꾸는 것입니다. 상태 값을 공유하면, 유닛이 늦게 준비되더라도 준비되는 순간 서버의 현재 값을 자동으로 받아 올바른 애니메이션을 재생하므로 신호를 놓칠 수가 없습니다. 함께, 뒤로 밀려 보이는 이동 문제도 원인을 계측으로 확인한 뒤 바로잡습니다.

이 문서(Research)는 **현재 코드가 어떻게 동작하는지, 왜 이런 증상이 나오는지**를 코드를 직접 읽어 정리한 것입니다. 실제 수정 방법과 순서는 Plan.md에서 다룹니다.

---

## 1. 사용자 실기 보고 (증상)

| # | 증상 | 발생 조건 (사용자 관찰) |
|---|------|------------------------|
| 증상 1 | 걷기(Walk) 애니메이션 미재생 — 유닛은 이동하나 다리 모션 없이 미끄러짐 | 주로 **갓 생산된 유닛**, 특히 **생산 시점에 공격 사거리 내 적이 이미 있는 경우** 다발 |
| 증상 2 | 유닛이 뒤로 밀리듯 이동 | **유닛 밀집 지역**(교전 밀집)에서 다발 |
| 증상 3 | 어느 쪽 화면(호스트/클라이언트)에서 발생하는지 미확인 | — (계측으로 판별 필요) |
| 증상 4 | 선행 태스크에서 이관된 잔여 타임아웃 2.7% | 타겟 전환 순간, 피격 표시가 최대 ~0.5초 지연 (동일 뿌리로 추정) |

증상 4의 근거: 선행 태스크 Research 9-3절 — "타겟 전환 순간 서버의 사거리/타겟 판정과 클라이언트 애니메이션 상태 사이의 짧은 틈새 … 8-2절의 이동/Walk 애니메이션 동기화 문제와 **동일한 뿌리(전투 상태 RPC 갭)**로 추정된다. 별도 후속 태스크로 이관한다."

---

## 2. 현재 구조 (코드 직접 확인)

확인한 파일:
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` (1663줄)
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs`
- `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs`

### 2-1. 위치(Position) 동기화 — 견고함

- 서버 전용 이동 코루틴 `MoveAlongPathV3`(UnitView.cs)가 유닛 위치를 서버에서만 계산한다. 클라이언트는 코루틴 진입 즉시 `yield break`로 빠진다 (UnitView.cs `MoveAlongPathV3` 멀티플레이 가드, 765~769줄).
- 클라이언트는 `NetworkTransform`이 서버 위치/회전을 보간 동기화한다. Red 클라이언트는 `NetworkUnit.LateUpdate()`에서 위치·회전을 맵 중심 기준으로 반전 보정한다 (NetworkUnit.cs 263~281줄).
- **결론: 위치 자체의 전송은 값 기반(NetworkTransform)이라 견고하다.** 증상 2(뒤로 밀림)는 위치 전송 문제가 아니라 **서버가 계산한 이동 경로 자체가 뒤로 가는 경우**일 가능성이 높다(2-4절).

### 2-2. 애니메이션 동기화 — 엣지 트리거 RPC에만 의존 (문제의 핵심)

멀티플레이에서 클라이언트 애니메이션은 **서버가 상태가 바뀌는 순간에만 쏘는 1회성 RPC(엣지 트리거)** 로만 갱신된다. 현재 상태(레벨) 자체를 동기화하는 장치가 없다.

| RPC | 발행 지점(서버) | 클라이언트 처리 | 성격 |
|-----|----------------|----------------|------|
| `StartWalkAnimationClientRpc(unitId)` | `MoveAlongPathV3` 코루틴 시작 시 `GameEvents.OnUnitWalkStarted.OnNext` → `OnUnitWalkStartedHandler` | `GameEvents.OnNetworkWalkStarted` → `UnitView.StartWalkAnimation` (Walk CrossFade) | 코루틴 시작 시 **1회만** 발행 |
| `StartCombatClientRpc(unitId, targetId, ...)` | `OnUnitEnteredCombatHandler`(적 감지 즉시) — `_combatAnimationSent` 가드 | `GameEvents.OnNetworkCombatStarted` → `UnitView.StartCombatAnimation` (Attack CrossFade + 타겟 회전 추적 시작) | 전투 진입 순간 1회 |
| `ChangeTargetClientRpc` | `TickCombat`에서 타겟이 바뀔 때 | `GameEvents.OnNetworkCombatTargetChanged` → `UnitView.ChangeTarget` (회전 대상만 교체) | 타겟 변경 순간 |
| `StopCombatClientRpc(unitId)` | `TickCombat`에서 적이 사라졌을 때 | `GameEvents.OnNetworkCombatStopped` → `UnitView.StopCombatAnimation` | 전투 종료 순간 1회 |

핵심 관찰:
- **`StopCombatAnimation()`은 애니메이션을 바꾸지 않는다.** 회전 추적(`_combatTargetTransform`, `_combatTargetId`)만 null로 초기화하고 애니메이터는 그대로 둔다(UnitView.cs 1651~1660줄, 의도적 no-op). 클라이언트에서 Attack→Walk 전환은 오직 `StartWalkAnimationClientRpc`가 도착해야만 일어난다(주석 1641~1646줄).
- 즉, **Walk 신호 1건을 유실하면 다음 Walk 신호가 올 때까지 클라이언트는 무한정 틀린 상태(정지 또는 Attack)에 갇힌다.** 현재 상태를 재확인·재적용하는 장치가 전혀 없다.

### 2-3. 스폰 레이스 — 갓 생산된 유닛이 취약한 이유

클라이언트 유닛 초기화 순서(코드 확인):
1. NGO가 프리팹을 자동 Instantiate → `NetworkUnit.OnNetworkSpawn()`에서 `_unitId` NetworkVariable로 유닛을 팩토리에 등록(NetworkUnit.cs 119~159줄). **이 단계에서는 아직 `UnitView`가 이벤트를 구독하지 않는다.**
2. `SpawnUnitClientRpc` 수신 → `UnitFactory.InitializeUnitView()` → `UnitView.Initialize(unitData)`로 `_unitData` 세팅 → `UnitView.SetDependencies(...)` 호출(UnitFactory.cs 350·368줄).
3. **`SetDependencies` 안에서 비로소** `GameEvents.OnNetworkWalkStarted` / `OnNetworkCombatStarted` 등을 구독한다(UnitView.cs 390~441줄).

문제: `GameEvents`의 각 스트림은 (ReplaySubject가 아닌) 일반 Subject로 추정되어, **구독 이전에 발행된 `OnNext`는 재생되지 않고 그대로 소실**된다. 따라서 1단계~3단계 사이(=유닛이 화면에 나타났지만 아직 구독 전)에 도착한 Walk/StartCombat RPC는 **수신은 되지만 해당 유닛 구독자가 없어 버려진다.**

- 코드 주석이 이 레이스를 이미 인지하고 있다: `StartCombatClientRpc` 주석(NetworkCombatController.cs 770~773줄) — "유닛 생성 직후 RPC가 도착해 UnitView가 아직 구독하지 못한 경우라도 … 다음 이벤트부터 처리한다(첫 Combat 이벤트는 다음 TickCombat에서 다시 발행됨)."
- 그러나 **Walk RPC(`OnUnitWalkStarted`)는 `MoveAlongPathV3` 코루틴 시작 시 딱 1회만 발행**되며(UnitView.cs 780~781줄), 재발행 장치가 없다. StartCombat은 다음 TickCombat에서 재발행될 여지라도 있지만 **Walk는 유실되면 다음 `MoveTo`(새 코루틴 시작) 전까지 영영 복구되지 않는다.**
- "갓 생산 + 사거리 내 적" 케이스: 유닛이 생산되어 랠리 지점으로 이동을 시작(Walk 발행)함과 거의 동시에 적을 감지해 전투 진입(StartCombat 발행)한다. 두 신호가 **모두** 구독 전에 도착해 유실될 수 있는 조합이라, 걷기도 공격 모션도 안 나오고 굳어 보인다. → 증상 1의 발생 조건과 정확히 일치.

### 2-4. 위치 괴리 — "뒤로 밀림" 가설

- 전투 추격(`EnterCombatPursuitV3`) 중에는 `transform.position`(실제 렌더 위치)이 공격 슬롯(=적 근처)으로 이동하지만, 도메인 타일 위치(`_unitData.Position`)는 추격 전 타일에 그대로 머문다(의도적 괴리, UnitView.cs 926~943줄 BUG-002 이력 주석).
- 전투 종료 시 `FindForwardClosestTile`로 "성 방향 앞쪽 타일"(forwardTile)을 1회 계산하고, 그 타일 중심까지 동일 이동 속도로 정렬 Lerp를 수행한 뒤(`ResumeFromForwardTileV3`) 새 경로로 A*를 재개한다(UnitView.cs 944~980줄).
- **가설:** 밀집 지역은 타겟 전환·전투 진입/이탈이 빈번하다. 추격 중 유닛이 전방으로 오버슛한 상태에서 전투가 끝나면, 정렬 Lerp의 목적지(forwardTile 중심)가 현재 `transform.position`보다 **뒤에 위치**할 수 있고, 그 경우 정렬 이동이 시각적으로 "뒤로 밀림"으로 보인다. 이는 규칙 11("뒤쪽 타일로 복귀하는 경우는 허용하지 않는다") 위반의 잠재 지점이다.
- 이 증상은 위치 기반(NetworkTransform)으로 클라이언트에도 충실히 재현되므로 **호스트·클라이언트 양쪽에서 동일하게 보일 것으로 추정**되나, 확정하려면 계측이 필요하다(증상 3).

### 2-5. 선행 태스크의 봉합과 잔여

- 선행 태스크 수정 3(규칙 21 신설, `_combatAnimationSent` 가드 해제): 서버가 Walk RPC를 보낼 때 `OnUnitWalkStartedHandler`에서 `_combatAnimationSent.Remove(unitId)`를 수행하여(NetworkCombatController.cs 540~547줄), "서버는 전투 중인데 클라이언트만 Walk에 갇혀 StartCombat 재전송이 억제되던" 고착(8-2절 Assault 62 사례)을 해소했다.
- 그러나 이는 **엣지 트리거 구조 안에서의 국소 봉합**이다. Walk RPC 자체가 유실되면 `OnUnitWalkStartedHandler`도 클라이언트에서 효과를 내지 못하고, 가드 해제도 클라이언트 애니메이션 복구로 이어지지 않는다. 구조가 그대로 남아 잔여 타임아웃 2.7%(증상 4)로 관측된다.

---

## 3. 영향 범위

| 레이어 | 파일 | 관련 지점 |
|--------|------|----------|
| Presentation | `UnitView.cs` | `StartWalkAnimation` / `StartCombatAnimation` / `ChangeTarget` / `StopCombatAnimation`, `MoveAlongPathV3`의 Walk 발행·정렬 Lerp·재경로, 멀티 이벤트 구독(`SetDependencies`) |
| Infrastructure | `NetworkCombatController.cs` | `StartWalkAnimationClientRpc` / `StartCombatClientRpc` / `StopCombatClientRpc` / `ChangeTargetClientRpc`, `OnUnitWalkStartedHandler`, `OnUnitEnteredCombatHandler`, `_combatAnimationSent` 가드, `TickCombat` |
| Infrastructure | `NetworkUnit.cs` | `_unitId` NetworkVariable 패턴(레벨 동기화 신설 시 참조 모델), `OnNetworkSpawn`/`OnNetworkDespawn` |
| Infrastructure | `UnitFactory.cs` | 클라이언트 `InitializeUnitView`(구독 시점), 서버 `CreateUnitObject` 경로 |
| Application | `GameEvents.cs` | `OnUnitWalkStarted` / `OnNetworkWalkStarted` / `OnNetworkCombatStarted` / `OnNetworkCombatStopped` 등 이벤트 스트림 |

- **싱글플레이 경로는 이번 증상과 무관**하다. 싱글플레이는 RPC 없이 `SetDependencies`에서 `GameEvents.OnCombatStarted` 등을 직접 구독하고, `MoveAlongPathV3`가 로컬에서 Animator를 직접 제어하므로 스폰 레이스가 발생하지 않는다(UnitView.cs 354~389줄). 다만 Phase 2에서 상태 동기화 구조를 바꿀 때 싱글플레이 경로가 회귀하지 않도록 보존해야 한다.

---

## 4. 현재 시점의 원인 요약

| 증상 | 추정 원인 | 근거 |
|------|----------|------|
| 증상 1 (Walk 미재생) | 엣지 트리거 Walk RPC가 스폰 레이스로 유실 + 재발행 장치 없음 | 2-2, 2-3절 (UnitView 780~781, NetworkCombatController 770~773) |
| 증상 4 (잔여 타임아웃 2.7%) | 동일한 엣지 트리거 구조 — 타겟 전환 순간 서버 전투 판정과 클라이언트 애니메이션 상태 틈새 | 2-5절, 선행 Research 9-3 |
| 증상 2 (뒤로 밀림) | **가설** — 전투 종료 정렬 Lerp의 forwardTile이 현재 transform보다 뒤에 위치(규칙 11 잠재 위반). 밀집 = 전투 진입/이탈 빈발 | 2-4절 (UnitView 944~980) — **Phase 1 계측으로 실증 필요** |
| 증상 3 (호스트/클라 미확인) | 미상 | 계측 필요 |

증상 1·4는 원인이 코드로 확인됐고, 증상 2·3은 **가설 단계**라 Plan의 Phase 1(계측)로 실증한 뒤 확정한다.

---

## 5. 선행 사례 (참조)

- 선행 태스크 문서: `_Tasks/2026-07-09/01_12_combat-hit-timing-sync/Research.md` (8-2절 고착 원인, 9-3절 잔여 2.7% 이관), 동 폴더 `Plan.md`.
- 계측 방법론(LogRules.md 형식, host/client 파일 분리, 방출 경로 자동 판정)은 선행 태스크에서 검증된 방식을 재사용한다.
- 참조할 값 기반 동기화 모델: `NetworkUnit._unitId`(`NetworkVariable<int>`, ReadPermission=Everyone / WritePermission=Server, `OnValueChanged` 콜백 + 스폰 시 현재 값 즉시 읽기). 애니메이션 상태 레벨 동기화의 직접적인 구현 모델이 된다(Plan Phase 2).

---

## 6. Phase 1 계측 결과 (2026-07-12)

> Plan Phase 1(계측)의 임시 로그 코드로 실제 게임을 돌려, 지금까지 **가설**이던 증상 2·3의 원인과 발생 위치를 숫자로 확정했다. 아래는 로그를 집계한 결과이며, 이 결과로 Plan의 Phase 3 내용을 조건부에서 **확정**으로 전환한다(Plan 말미 "Phase 1 실증에 따른 확정" 참조).

**수집 로그:**
- `_Logs/2026-07-12/07_55_movement-walk-anim-sync/RuntimeLog_host.txt` (9,897줄)
- `_Logs/2026-07-12/07_55_movement-walk-anim-sync/RuntimeLog_client.txt` (3,346줄)
- 구성: 멀티플레이 Host + Client 2인 구성, host/client 별도 파일 저장(Plan Phase 1 파일 분리 기준 준수).

### 6-1. 증상 1(Walk 미재생) 원인 확정 — 스폰 레이스

| 항목 | 계측 결과 |
|------|----------|
| 클라이언트 스폰 유닛 수 | 223기 |
| 첫 Walk RPC를 정확히 1건씩 유실한 유닛 | 222기 (전체의 99.6%) |
| 유실 메커니즘 | Walk RPC **수신 시각**이 `UnitView` 이벤트 **구독 완료 시각**보다 23~37ms **선행** |
| 대표 사례 | unit 604 — Walk RPC 수신 42.011초 vs 구독 완료 42.048초 (37ms 선행) |

- **판정:** 증상 1은 "종종" 발생하는 산발적 문제가 아니라 **모든 신규 유닛에서 첫 Walk RPC가 구조적으로 유실**된다. 다만 후속 Walk RPC(다음 `MoveTo` 코루틴 시작)가 대개 이를 가려서, 사용자에게는 "갓 생산된 유닛 일부"에서만 증상으로 관측됐다. 2-3절(스폰 레이스)의 코드 분석이 정량적으로 실증됐다.
- **Combat RPC:** 이번 데이터에서는 유실 0건. 전투 진입이 구독 완료 이후에 발생했기 때문이며(구독→전투 순서), **동일한 엣지 트리거 구조적 취약점을 공유**한다. 스폰 시점과 전투 진입 시점이 겹치는 조건(증상 1의 "갓 생산 + 사거리 내 적")에서는 Combat RPC도 유실될 수 있는 잠재 위험이 그대로 남아 있다.

### 6-2. 증상 2(뒤로 밀림) 발생 위치 확정 — 서버 이동 로직

| 항목 | 계측 결과 |
|------|----------|
| 역방향(뒤로) 이동 WARN 총건수 | 282건 |
| 그중 호스트(서버) 파일 발생 | 282건 (100%) |
| 클라이언트 파일 발생 | 0건 |
| 282건 중 "새 경로 발급(이동 시작) 직후 첫 타일 스텝"에서 발생 | 281건 |
| 정렬 이동(전투 종료 후 forwardTile 정렬 Lerp) 방향 | 460건 전부 **정방향** (역방향 0건) |

- **판정 1 — 발생 위치는 서버:** 역방향 이동 WARN이 **전부 호스트(서버) 파일**에서만 나왔고 클라이언트 파일은 0건이다. 증상 2는 `NetworkTransform` 보간이 만드는 클라이언트 측 시각 아티팩트가 **아니다**(보간 무죄). 서버가 계산한 이동 경로 자체가 뒤로 가고, 그것이 값 기반으로 클라이언트에 충실히 재현된 것이다. 이로써 증상 3(호스트/클라 미확인)도 확정 — **원인은 서버, 증상은 양쪽에서 동일하게 보임**.
- **판정 2 — 발생 지점은 재경로 첫 스텝:** 282건 중 281건이 **"새 경로 발급 직후 첫 타일 스텝"**에서 발생했다. 즉 재경로/이동 재시작 시, 경로가 실제 `transform.position`이 아니라 **뒤처진 도메인 타일(`_unitData.Position`) 기준**으로 계산되어 첫 걸음이 목적지 역방향으로 향한다.
- **판정 3 — 정렬 로직은 무죄:** 2-4절에서 "뒤로 밀림"의 유력 가설로 지목했던 **전투 종료 정렬 Lerp(forwardTile)는 460건 전부 정방향**으로, 역방향 0건이다. 가설(정렬 Lerp의 forwardTile이 현재 transform보다 뒤)은 **기각**되고, 원인은 **재경로 출발점 좌표계 불일치**로 재확정됐다.
- **계측 좌표계 검증 완료:** 이동 벡터와 목적지 방향을 모두 **뷰 좌표(view)로 통일**해 내적을 계산했다. 역방향 판정이 좌표계 혼용으로 인한 측정 아티팩트가 아님을 확인했다.

### 6-3. 결과에 따른 원인표 갱신 (4절 대비)

| 증상 | 4절 시점 | Phase 1 실증 후 |
|------|---------|----------------|
| 증상 1 (Walk 미재생) | 원인 코드 확인, 빈도 미상 | **확정** — 신규 유닛 222/223기 첫 Walk RPC 유실(스폰 레이스), 후속 RPC가 가림 |
| 증상 2 (뒤로 밀림) | 가설 (정렬 Lerp forwardTile 후위) | **확정** — 서버 재경로 첫 스텝 281건, 정렬 Lerp 무죄(460건 정방향), 원인=재경로 출발점 좌표계 |
| 증상 3 (호스트/클라) | 미상 | **확정** — 원인은 서버, NetworkTransform 보간 무죄(클라 WARN 0), 증상은 양쪽 동일 |
| 증상 4 (잔여 타임아웃 2.7%) | 동일 엣지 구조 추정 | 유지 — Phase 2 레벨 동기화 후 소멸 여부 재계측으로 확인 |

---

## 7. 최종 검증 및 완료 (2026-07-12 검증 / 2026-07-13 문서화)

> Phase 2·3 구현을 실제 게임으로 3차까지 재검증하여 통과했다. 아래는 최종 검증 결과와 각 증상의 종결 상태다. (구현/코드 상세는 game-programmer 메모리 참조. 본 절은 검증 사실만 기록한다.)

### 7-1. 최종(3차) 검증 결과 — 클라 로그 15,316줄

| 지표 | 결과 |
|------|------|
| 생성 유닛 수 | 634기 |
| 애니메이션 상태 적용 보장 유닛 | **634기 전원** (무귀속 15기 → **0기**) |
| `UnitView.Initialize` 후 재적용(`ReapplyAnimStateToView`) 발동 | 634회 |
| 역방향(뒤로) 이동 WARN | 282건 → **41건** (보정 353건, 실이동 278건) |
| 육안 확인 | 걷기 미재생·뒤로 밀림 **모두 소멸** |

### 7-2. 증상별 종결 상태

- **증상 1(Walk 미재생) — 종결:** Phase 2 레벨 동기화(NetworkVariable + 스폰 시 현재값 자동 적용)로 첫 Walk RPC 스폰 레이스 유실이 구조적으로 소멸. 무귀속 유닛 15기 → 0기.
- **증상 1 후속(적용 시점 무음 실패) — 봉합:** 상태 적용이 `UnitView.Initialize`보다 이르면 애니메이터 미준비로 무음 실패(재시도 없음, unit=-1 125건/무귀속 15기)하던 문제를 `Initialize` 말미 `NetworkUnit.ReapplyAnimStateToView()`(멱등) 재적용으로 봉합(3차 634회 발동).
- **증상 2·3(뒤로 밀림) — 급감:** `MoveTo`의 `AlignPathStartToTransform`(첫 스텝이 최종 목적지와 역방향일 때만 실제 transform 전방 타일 `FindForwardClosestTile`에서 재발급)로 282→41.
- **증상 2 잔여 41건 — 오탐으로 종결(코드 무수정):** 41건 전부 스폰 직후 유닛의 정상 우회 경로이고, 판정 지표가 "첫 걸음이 최종 목적지(먼 성) 직선 방향인가"라 정상 우회를 역방향으로 오탐한 것이다. 육안 밀림 없음 → 계측 지표의 한계로 판단하고 코드 수정 없이 종결.
- **증상 4(잔여 타임아웃 2.7%):** Phase 2 레벨 동기화로 클라이언트 Attack 루프가 안정화되며 함께 해소(육안·로그상 재현 안 됨).

### 7-3. 정리 및 계획 대비 변경

- **정리:** `[MOVESYNC-LOG]` 임시 계측 코드 전량 제거. Phase 2에서 주석 처리했던 엣지 경로(`StartWalkAnimationClientRpc` / `OnNetworkWalkStarted`) 최종 삭제.
- **Plan 대비 변경 — `_combatAnimationSent` 유지:** Plan의 비활성화 대상 D는 이 가드를 제거 대상으로 봤으나, 실제로는 애니메이션이 아니라 **데미지(`ExecuteAttack`)·타겟 RPC 게이팅 기능 가드**로 확인되어 주석 처리하지 않고 유지했다(직전 combat-timing 태스크 수정 3과 동일 판단). 규칙 21/22 문안에 이 유지 사유를 반영했다.
