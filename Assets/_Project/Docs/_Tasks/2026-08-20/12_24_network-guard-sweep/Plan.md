# Plan — 네트워크 종료 시점 가드 전수 보강 (network-guard-sweep)

작성일: 2026-08-20
작업 폴더: `Assets/_Project/Docs/_Tasks/2026-08-20/12_24_network-guard-sweep/`
선행 작업: `_Tasks/2026-08-19/10_16_combat-shutdown-cleanup/Plan.md` (커밋 `55d24d83`)

---

## 0. 이 작업이 무엇인지 — 자연어 설명 (CLAUDE.md 규칙 13)

### 쉬운 말로

멀티플레이 경기가 끝나고 "로비로" 를 누르면, 게임은 **네트워크 연결을 먼저 끊고** 그 다음에 **씬을 갈아끼웁니다.**
이 두 동작 사이에는 아주 짧지만 **분명히 존재하는 틈**이 있습니다. 실기 로그로 재어 보니 **6~41밀리초**였습니다.
화면이 1초에 60번 그려지므로 한 프레임이 약 17밀리초입니다. 즉 **이 틈 안에서 게임은 한두 프레임을 더 그립니다.**

문제는 그 한두 프레임 동안 게임의 여러 부품이 **아직 자기가 멀티플레이 중인 줄 알고** 계속 일한다는 점입니다.
유닛 생산 타이머가 돌고, 골드 수입이 쌓이고, 타일 점령이 갱신됩니다.
그리고 그 결과를 "상대방에게 알려야지" 하며 **네트워크로 메시지를 보내려 합니다.**
그런데 네트워크는 이미 꺼진 뒤라, 유니티가 아래 오류를 뱉습니다.

```
Rpc methods can only be invoked after starting the NetworkManager!
```

### 왜 지금 하는가

**직전 작업(어제)에서 이미 같은 버그를 한 번 고쳤습니다.** 전투를 담당하는 부품에서 이 오류가 **실기 테스트 중 2회 실제로 터졌고**, 그 부품의 구멍 6곳을 막았습니다.
이번 작업은 **"같은 종류의 구멍이 다른 부품에도 있는지" 를 전부 뒤져 본 결과**를 정리한 것입니다. **7곳을 찾았고, 조사 중에 2곳을 더 찾아** 총 9곳이 후보가 되었습니다.

### 무엇을 바꾸는가

바꾸는 것은 **각 부품의 맨 앞에 한 줄짜리 안전장치를 넣는 것뿐**입니다.
지금 코드는 *"내가 서버 역할인가?"* 만 묻습니다. 여기에 *"이 부품이 아직 네트워크에 살아 있는가?"* 라는 질문을 하나 더 붙입니다.
게임이 정상적으로 돌아가는 동안에는 두 질문 모두 "예" 이므로 **플레이 중 동작은 조금도 달라지지 않습니다.**
달라지는 것은 **연결이 끊긴 뒤의 한두 프레임**뿐이고, 거기서는 원래 아무 일도 일어나면 안 되는 것이 맞습니다.

### 이 문서에서 정직하게 밝혀 두는 것 세 가지

1. **이번 9곳은 아직 한 번도 오류를 낸 적이 없습니다.** 어제 고친 전투 부품은 2회 터졌지만, 이번 것들은 0회입니다. 위험도가 낮습니다. 그럼에도 왜 고치는지는 §4 에 적습니다.
2. **이 조사는 조사자가 근거를 두 번 잘못 짚은 끝에 나온 결과입니다.** 그 경위와 교훈을 §2 에 남깁니다.
3. **확정하지 못하고 남긴 항목이 있습니다.** §9 에 따로 모아 두었으며, 추정으로 메우지 않았습니다(CLAUDE.md 규칙 10).

---

## 1. 배경 — 직전 작업의 연장선

### 1-1. 직전 작업(커밋 `55d24d83`)이 고친 것

`NetworkCombatController` 의 종료 시점 가드 **6곳** + 게임 종료 시 전투 틱 정지(`_combatStopped`).

원인은 한 문장으로 정리됩니다.

> **`IsServer` 는 *"내가 서버 역할인가"* 를 묻는 값이지, *"이 오브젝트가 아직 살아 있는가"* 가 아니다.**

`NetworkManager.Shutdown()` 이 호출된 뒤에도 씬이 실제로 바뀌기 전까지 `IsServer` 는 **여전히 참**입니다.
그래서 `if (!IsServer) return;` 만 있는 자리는 종료 직후의 늦은 실행을 **하나도 걸러 내지 못합니다.**

이때 필요한 값이 `IsSpawned` 입니다. 이것은 *"이 `NetworkObject` 가 아직 스폰된 상태인가"* 를 묻는 값이라,
Shutdown/디스폰 이후에는 거짓이 되어 늦은 실행을 정확히 막아 줍니다.

해당 오류는 **2026-08-18 실기 테스트에서 [로비로] 경로로 2회 재현**되었습니다
(근거: `NetworkCombatController.cs` 355~357행 주석, 직전 Plan §10).

### 1-2. 이번 작업의 범위

같은 부류의 구멍이 **다른 컨트롤러에도 있는지** 전수 조사하고, 발견된 자리에 **같은 형태의 가드**를 넣습니다.
새 기능도, 구조 변경도, 리팩터링도 하지 않습니다(CLAUDE.md 규칙 6).

---

## 2. ⚠️ 조사 과정에서 근거를 두 번 잘못 짚었다 — 경위와 교훈

이 절은 **결과보다 중요할 수 있어 앞에 둡니다.** 같은 실수를 다음 작업에서 반복하지 않기 위한 기록입니다.

### 빗나감 ① — "다른 컨트롤러엔 `Update()` 가 없으니 안전하다"

**틀렸다.** 진입점은 `Update()` 만이 아니다. 조사 시점에 **코루틴을 보지 않았다.**
`Update()` 가 없어도 코루틴·이벤트 구독·콜백은 얼마든지 종료 이후에 실행될 수 있다.

### 빗나감 ② — "코루틴이 있으니 위험하다"

**이것도 틀렸다.** 코루틴을 찾아낸 뒤 *"코루틴이라 위험하다"* 고 판단했는데,
**본문을 열어 보니 `yield return` 이 하나도 없어** 사실상 한 프레임 안에 끝나고 마는 코드였다.
"코루틴" 이라는 **이름**만 보고 "여러 프레임에 걸쳐 살아남는다" 고 넘겨짚은 것이다.

> **이번 Plan 작성 중 같은 함정을 실제로 한 번 더 만났다.**
> `ReconnectionHandler` 의 `WaitAndForceWin()` 은 **진짜로 `yield return new WaitForSeconds(30)` 을 한다**(194~203행).
> 실기 로그에서 이 코루틴은 **하필 위험 구간 한복판에서 시작**한다(`13:34:01.489` — Shutdown `.467` 과 디스폰 `.494` 사이).
> 이름과 타이밍만 보면 명백한 후보다. 그런데 **`OnNetworkDespawn` 에서 `StopCoroutine` 으로 확실히 정리한다**(106~119행).
> → **구멍이 아니다.** 본문을 끝까지 읽지 않았다면 없는 구멍을 고칠 뻔했다.

### 빗나감 ③ — "`grep` 상 구독 해제를 안 한다"

`OnNetworkDespawn` 본문에 `Dispose()` 문자열이 없다는 이유로 두 파일이 *"해제를 안 한다"* 고 분류됐다.
**틀렸다.** 두 파일 모두 **헬퍼 메서드를 경유해** 해제하고 있었다.

| 파일 | 실제 해제 경로 | 확인 위치 |
|---|---|---|
| `NetworkResourceSync` | `OnNetworkDespawn` → `UnsubscribeResourceChanged()` → `Dispose()` | 128행 → 159~163행 |
| `NetworkTileSync` | `OnNetworkDespawn` → `UnsubscribeTileOwnerChanged()` → `Dispose()` | 106행 → 134~138행 |

이 오판이 그대로 갔다면 **위험 구간을 "게임 내내" 로 잘못 적어**, 실제(수십 밀리초)보다 훨씬 부풀린 근거로 계획을 세웠을 것이다.

### 교훈 (이 문서가 남기는 한 문장)

> **진입점의 이름만 보고 판단하지 말고, 본문과 호출 경로를 끝까지 따라간다.**
> `Update()` 가 없다 / 코루틴이다 / `grep` 에 안 잡힌다 — 셋 다 **판단의 근거가 되지 못한다.**

이 교훈은 새로 만든 것이 아니라, **`LogRules.md` 1.14 금지 9 가 2026-08-18 에 이미 같은 문장으로 적어 둔 것**이다
(*"중복 판정은 호출 경로를 끝까지 따라가서 한다 — 두 번 연속 빗나갔다"*).
**로그 판정에서 배운 교훈이 가드 판정에서 똑같이 필요했다.** 규칙을 읽고도 다른 분야라 여겨 적용하지 않은 것이 이번 실수의 뿌리다.

---

## 3. 조사 결과 (전부 실측)

### 3-1. 대상 — 7곳 / 5파일 (사전 확정분)

전부 `Assets/_Project/Scripts/Infrastructure/Network/` 하위입니다.
**각 진입점이 정확히 무엇을 건드리는지 파일을 직접 열어 확인했습니다.**

| # | 파일 | 진입점 (행) | 현재 가드 | 이 진입점이 건드리는 것 |
|---|---|---|---|---|
| 1 | `NetworkResourceSync.cs` | `OnResourceChangedOnServer` (170) | **없음** | **`NetworkVariable` 쓰기** — `_blueGold.Value` / `_redGold.Value` (175·178행) |
| 2 | `NetworkTileSync.cs` | `OnTileOwnerChangedOnServer` (144) | **없음** | **`ClientRpc`** — `BroadcastTileChangeClientRpc(q, r, teamIndex)` (147행) |
| 3 | `NetworkGameEndController.cs` | `OnGameEndServer` (161) | `!IsServer` 만 (163) | **`ClientRpc`** — `AnnounceWinnerClientRpc(winnerTeamIndex, isRandomMatch)` (183행) |
| 4 | `NetworkHealthSync.cs` | `OnEntityDamaged` (118) | `!IsServer` 만 (120) | **`ClientRpc`** — `SyncHealthClientRpc(...)` (148행) |
| 5 | `NetworkHealthSync.cs` | `OnEntityHealed` (161) | `!IsServer` 만 (163) | **`ClientRpc` 2종 분기** — 건물이면 `SyncBuildingHealClientRpc` (169행), 유닛이면 `SyncHealClientRpc` (179행) |
| 6 | `NetworkProductionController.cs` | `OnProductionStarted` (194) | `!IsServer` 만 (196) | **`ClientRpc`** — `ProductionStartedClientRpc(barracksId, type, requiredTime)` (204행) |
| 7 | `NetworkProductionController.cs` | `OnUnitProduced` (278) | `!IsServer` 만 (280) | **`ClientRpc`** — `SpawnUnitClientRpc(...)` (300행) |

**7곳 전부가 `ClientRpc` 호출 또는 `NetworkVariable` 쓰기를 한다** — 즉 전부 "네트워크가 살아 있음" 을 전제하는 동작입니다.

> `NetworkVariable` 쓰기(#1)가 RPC 와 **똑같은 오류를 내는지는 확정하지 못했다.** 직전 작업도 같은 판단을 남겼습니다
> (`NetworkCombatController.cs` 883~884행: *"디스폰 이후 NetworkVariable 쓰기가 RPC 와 똑같이 오류를 내는지는 확정하지 못했다"*).
> **추정하지 않고 §9 확인 필요 항목으로 남깁니다**(CLAUDE.md 규칙 10).

### 3-2. 조사 중 추가로 발견한 2건 — **사전 목록에 없던 것 (승인 필요)**

전수 조사를 다시 돌리는 과정에서 **같은 부류의 구멍 2건을 더 찾았습니다.**
요청 범위를 넘기지 않기 위해 **임의로 포함하지 않고, 아래에 제안만 합니다**(CLAUDE.md 규칙 6).

| # | 파일 | 진입점 (행) | 현재 가드 | 건드리는 것 | 성격 |
|---|---|---|---|---|---|
| **8** | `NetworkProductionController.cs` | `OnProductionQueueChanged` (223) | `!IsServer` 만 (225) | **`ClientRpc`** — `SyncQueueStateClientRpc(...)` (263행) | **#6·#7 과 완전히 같은 부류.** 같은 파일 · 같은 `SubscribeToProductionEvents()` 블록(184~185행)에서 나란히 구독되는 **세 번째 형제**다 |
| **9** | `NetworkGameEndController.cs` | `_localRematchRequestedSub` / `_localRematchAcceptedSub` / `_localRematchDeclinedSub` (124~131) | **없음** | **`ServerRpc` 3종** — `RequestRematchServerRpc` / `AcceptRematchServerRpc` / `DeclineRematchServerRpc` | 성격이 다르다 — **사용자 버튼 입력**으로만 발행되고, `IsServer` 블록 **밖에서** 구독되어 클라이언트에서도 살아 있다 |

**#8 은 함께 고치는 것을 권합니다.** 형제 셋 중 둘만 막고 하나를 남기면, 나중에 읽는 사람이 *"이 하나는 왜 다른가"* 를 고민하게 되고
**같은 파일 안에서 가드 모양이 갈립니다.** 직전 작업도 같은 이유로 `Update`·`OnUnitDied`·`OnBuildingDied` 의 형태를 통일했습니다
(`NetworkCombatController.cs` 892행 주석: *"같은 파일 안에서 가드 모양이 갈리지 않도록 형태를 통일한다"*).

**#9 는 이번 범위에서 빼는 것을 권합니다.** 위험 구간(수십 밀리초) 안에 사용자가 재경기 버튼을 눌러야 발동하므로 실현 가능성이 사실상 없고,
`IsServer` 블록 밖 구독이라 **가드 형태 자체가 달라져야 해서**(`!IsSpawned` 만) 판단이 별건입니다. §10 범위 밖에 근거와 함께 적어 둡니다.

> **결론: 이번에 고치는 대상은 7곳(사전 확정) + #8(승인 시) = 최대 8곳 / 5파일.**

### 3-3. 전수 조사를 어떻게 했는지 (재현 가능한 근거)

| 확인 | 방법 | 결과 |
|---|---|---|
| `Infrastructure/Network/` 전 파일의 이벤트 구독 | `grep -n "\.Subscribe("` 을 21개 파일 전체에 적용 | **구독이 있는 파일은 위 5개뿐.** `NetworkBuildingController` · `NetworkUpgradeController` · `NetworkMistShrineController` · `NetworkSkillController` · `NetworkUnitMovementController` · `NetworkGameFlow` · `NetworkGameManager` · `ReconnectionHandler` — **전부 0건** |
| 프로젝트 전체의 기존 `IsSpawned` 사용처 | `grep -rn "IsSpawned" Assets/_Project/Scripts/` | 23건. 그중 `this.IsSpawned` 가드는 직전 커밋이 넣은 6곳 + `NetworkUnit:291` 뿐 |
| 코루틴 진입점 | `ReconnectionHandler` 의 `WaitAndForceWin` **본문을 끝까지 읽음** | `OnNetworkDespawn` 이 `StopCoroutine` 으로 정리 → **구멍 아님** (§2 참조) |

### 3-4. 위험 구간은 하나뿐이다 — 그리고 실측값은 **27ms 가 아니라 6~41ms 다**

**5파일 모두 `OnNetworkDespawn` 에서 구독을 해제합니다.** 직접 확인했습니다.

| 파일 | 해제 위치 | 방식 |
|---|---|---|
| `NetworkResourceSync` | 128행 | `UnsubscribeResourceChanged()` **헬퍼 경유** → 161행 `Dispose()` |
| `NetworkTileSync` | 106행 | `UnsubscribeTileOwnerChanged()` **헬퍼 경유** → 136행 `Dispose()` |
| `NetworkGameEndController` | 140~148행 | `_gameEndSubscription` 외 3건 직접 `Dispose()` |
| `NetworkHealthSync` | 104~108행 | `_damagedSubscription` · `_healedSubscription` 직접 `Dispose()` |
| `NetworkProductionController` | 156~161행 | 3건 직접 `Dispose()` |

따라서 가드 없이 실행될 수 있는 구간은 **`NetworkManager.Shutdown()` 과 `OnNetworkDespawn` 사이**뿐입니다.

**⚠️ 실측값 정정.** 근거 로그 `_Logs/_editor/2026-08-19/RuntimeLog.txt` 에는 이 구간이 **4회** 기록되어 있고, 값은 하나가 아닙니다.

| 회차 | `NetworkManager Shutdown 완료` | 첫 `디스폰` 로그 | 구간 |
|---|---|---|---|
| 1 (255→258행) | `05:00:54.174` | `05:00:54.199` | **25 ms** |
| 2 (692→695행) | `13:34:01.467` | `13:34:01.494` | **27 ms** |
| 3 (874→877행) | `23:29:45.826` | `23:29:45.867` | **41 ms** |
| 4 (1398→1399행) | `23:35:57.538` | `23:35:57.544` | **6 ms** |

- **직전 커밋의 코드 주석에 적힌 `27ms` 는 4회 중 2회차 한 표본입니다.** 틀린 값은 아니지만 **대표값도, 최댓값도 아닙니다.**
- **실측 최댓값은 41ms** 입니다. 60fps 기준 한 프레임이 약 16.7ms 이므로, 이 구간에는 **최대 2~3 프레임이 들어갑니다.**
- 이 Plan 은 **6~41ms** 로 적습니다. 다만 **기존 코드 주석의 `27ms` 표기는 이번 작업에서 고치지 않습니다** — 문구 정정은 요청 범위 밖이고(규칙 6), 새로 추가하는 주석은 §7 방침대로 **직전 커밋 주석을 가리키기만** 합니다.

---

## 4. 직전 건과의 위험도 차이 — 숨기지 않는다

| | `NetworkCombatController` (어제 수정 완료) | **이번 7~8곳** |
|---|---|---|
| 실행 방식 | **`Update()` — 매 프레임 자동으로 돈다** | 이벤트가 발행될 때만 돈다 |
| 게임 종료 직후 | **유닛이 일제히 타겟을 잃어 정리 목록이 한꺼번에 차고, `StopCombatClientRpc` 가 무더기로 나간다** | 특별히 몰리지 않는다 |
| 실기 재현 | **2회** (2026-08-18, [로비로] 경로) | **0회** |
| 발견 경위 | **실제로 터진 오류를 추적** | **예방 목적의 전수 조사** |

**이번 건은 직전 건보다 위험도가 명백히 낮습니다.** 이 문서는 그것을 숨기지 않습니다.

### 그럼에도 고치는 이유 세 가지

1. **"터진 적 없으니 놔둔다" 는 판단은 이미 한 번 틀렸다.**
   `NetworkCombatController` 도 어제 아침까지는 "터진 적 없는 코드" 였습니다. 그리고 **터졌습니다.**
   0회라는 사실은 *"구멍이 없다"* 가 아니라 *"아직 조건이 겹치지 않았다"* 는 뜻입니다.

2. **조건이 겹칠 수 있다는 것을 §5 에서 실제로 확인했다.**
   이번 조사에서 `Time.timeScale` 이 **Shutdown 직전에 1로 복원된다**는 사실을 찾았습니다(§5-1).
   즉 위험 구간은 **정지 화면이 아니라 정상 속도로 흐르는 1~2 프레임**입니다. "가능성이 낮다" 와 "구조적으로 불가능하다" 는 전혀 다르며, 여기는 **전자**입니다.

3. **비용이 거의 0 이다.**
   변경은 조건 한 개 추가이고, 정상 구간에서는 `IsSpawned` 가 항상 참이라 **동작이 달라지는 지점이 없습니다**(§8 에서 검증).
   위험도가 낮은 만큼 **고치는 비용도 낮으므로**, 미루는 쪽의 이득이 없습니다.

---

## 5. 각 이벤트가 위험 구간(6~41ms) 안에 발행될 수 있는가 — 발행처를 따라가 판단

**추정하지 않고 각 이벤트의 발행처와 그 발행처를 구동하는 틱 주체까지 따라갔습니다.**

### 5-1. 먼저 밝혀야 할 전제 — 위험 구간은 **정상 속도로 흐른다**

이 절 전체의 결론을 좌우하는 사실입니다.

```
GameEndUI.ShowResult (245행 / 317행)   →  Time.timeScale = 0f   (게임 정지)
        ↓  (플레이어가 [로비로] 를 누르거나 카운트다운 만료)
GameEndUI.ReturnToLobby (340행)
        ├─ 343행:  Time.timeScale = 1f        ←★ 시간이 먼저 복원된다
        └─ 354행:  NetworkGameManager.BackToLobby("Lobby")
                       └─ 924행: ShutdownNetworkManager() → NetworkManager.Shutdown()
                       └─ 928행: OnNetworkBackToLobby 발행 → 씬 전환
```

**`Time.timeScale = 1f` 이 `Shutdown()` 보다 먼저 실행됩니다**(`GameEndUI.cs` 343행 vs 354행).
따라서 위험 구간에서 `Time.deltaTime` 은 **0 이 아니라 정상값**이고, `Update()` 기반 틱이 **전부 정상 속도로 돕니다.**
어제 `NetworkCombatController` 가 이 구간에서 RPC 를 쏟아 낸 이유도 바로 이것입니다.

### 5-2. 위험 구간에서 계속 도는 틱 주체 — 실측

| 틱 주체 | 가드 유무 | 위험 구간에서 도는가 |
|---|---|---|
| `ProductionTicker.Update()` (`Presentation/Production/`, 238행) | **`IsSpawned` · 게임 종료 가드 없음.** 246행의 유일한 분기는 *"멀티 클라이언트면 스킵"* 뿐 | **돈다.** 260행 `_productionUseCase.Tick(dt)` + 265행 `_resourceUseCase.TickIncome(dt, ...)` 실행 |
| `GameBootstrapper.Update()` → `_tileOwnership.Tick()` (640~644행) | `!NetworkContext.IsNetworkActive \|\| NetworkContext.IsNetworkServer` | **돈다.** 호스트는 `IsNetworkServer=true` 라 통과하고, `NetworkContext.Reset()` 이후에는 `!IsNetworkActive` 가 참이 되어 **역시 통과한다.** 어느 쪽이든 막히지 않는다 |
| `NetworkCombatController.TickCombat` | **직전 커밋이 `!IsSpawned \|\| !IsServer \|\| _combatStopped` 로 막음** (380행) | **돌지 않는다** |

### 5-3. 이벤트별 판정

| 진입점 | 발행처 (실측) | 구동 주체 | 위험 구간 발행 가능성 |
|---|---|---|---|
| `OnResourceChangedOnServer` (#1) | `ResourceUseCase` 102·114행 (`AddGold`/`SpendGold` 내부) | `ProductionTicker.Update` → `TickIncome` (265행). 누적값이 1골드를 넘는 순간 `AddGold` 호출 → 발행 | **가능.** 채굴소 수입 누적은 매 프레임 진행되고 정수 경계를 넘을 때마다 발행된다. 1~2 프레임 안에 경계를 넘을 확률은 낮지만 **구조적으로 막혀 있지 않다** |
| `OnTileOwnerChangedOnServer` (#2) | `TileOwnershipService` 190행 · `UnitSpawnUseCase` 106·250행 · `BuildingPlacementUseCase` 157~339행 | `GameBootstrapper.Update` → `_tileOwnership.Tick()` (643행) | **가능.** `Tick()` 은 `dt` 를 받지 않고 **유닛의 물리 위치**로 판정하며(189~190행), 소유 팀이 바뀐 타일마다 발행한다. 유닛 이동은 `timeScale=1` 복원 후 계속되므로 점령 진행 중이면 발행된다 |
| `OnProductionStarted` (#6) | `UnitProductionUseCase` 580·644행 | `ProductionTicker.Update` → `_productionUseCase.Tick(dt)` (260행) | **가능.** 큐에 대기 항목이 있고 앞 항목이 이 프레임에 완료되면 다음 항목 생산이 시작되며 발행된다 |
| `OnUnitProduced` (#7) | `UnitProductionUseCase` 721행 | 위와 동일 | **가능.** 생산 타이머가 이 프레임에 만료되면 발행된다 |
| `OnProductionQueueChanged` (#8) | `UnitProductionUseCase` **13곳** (117~731행) | 위와 동일 | **가장 가능성이 높다.** 발행 지점이 13곳으로 가장 많고, 생산 시작·완료 양쪽 경로가 모두 이 이벤트를 함께 발행한다(582·646·731행) |
| `OnEntityDamaged` (#4) | `UnitCombatUseCase` 1314·1598·1790·2091행 · `TowerCombatUseCase` 235행 | 멀티 서버에서는 **전부 `NetworkCombatController.TickCombat` 이 구동** | **낮다.** 구동 주체가 380행 가드로 이미 멈춰 있다. 게임 종료 경로에서는 `_combatStopped` 가, 그 외 경로에서는 `!IsSpawned` 가 막는다 |
| `OnEntityHealed` (#5) | `MistShrineUseCase` 580·600행 · `UnitCombatUseCase` 1388·2130행 | 위와 동일 | **낮다.** 위와 같은 이유 |
| `OnGameEndServer` (#3) | `GameEndUseCase` 79·103행 | 성 파괴 / 항복 / 재접속 타임아웃 | **낮지만 0 이 아니다.** 정상 경로에서는 종료가 **먼저** 오고 Shutdown 이 나중이라 이미 `_announced=true` 로 두 번째 호출이 차단된다(164행). 다만 **종료 없이 Shutdown 되는 경로**(연결 끊김 등)에서는 `_announced` 가 거짓인 채 구간에 들어간다 |

### 5-4. 이 절의 결론

**#1 · #2 · #6 · #7 · #8 은 "구조적으로 발행될 수 없다" 고 말할 근거가 없습니다.** 오히려 구동 주체가 **가드 없이 정상 속도로 돌고 있음**을 확인했습니다.
**#4 · #5 는 직전 커밋 덕분에 구동 주체가 이미 멈춰 있어 가능성이 낮습니다.** 그럼에도 §4-1 의 이유로 함께 막습니다.
**#3 은 정상 종료 경로에서는 `_announced` 가 막지만, 그 외 경로는 확정하지 못했습니다** — §9 에 남깁니다.

---

## 6. 채택안과 판단 사항 1~3의 답

### 6-1. 채택안

```csharp
if (!IsSpawned || !IsServer) return;
```

**직전 커밋과 동일한 형태**입니다. 새 형태를 만들지 않는 것이 이 작업의 핵심입니다 — 같은 문제를 프로젝트 안에서 **한 가지 모양으로만** 표현합니다.

#### `||` 단락 평가 — `IsSpawned` 를 **앞에** 두는 이유

`||` 는 **앞이 참이면 뒤를 평가하지 않습니다**(단락 평가, short-circuit).
싱글플레이처럼 애초에 스폰되지 않은 상태에서는 `IsSpawned` 가 거짓 → `!IsSpawned` 가 참 → **`IsServer` 를 건드리지 않고 곧바로 반환**합니다.
`NetworkUnit.cs` 291행이 **같은 이유로 같은 순서**를 씁니다(290행 주석: *"IsSpawned 먼저 검사(|| 단락 평가) — 싱글플레이(미스폰)에서는 IsServer를 건드리지 않고 반환"*).

> **정확히 적어 둔다.** `NetworkUnit:291` 의 실제 코드는 `if (!IsSpawned || IsServer) return;` 로, **두 번째 조건의 부호가 반대**다.
> 그 메서드(`ReapplyAnimStateToView`)는 **클라이언트 전용**이라 서버를 걸러 내야 하기 때문이다.
> 이번에 인용하는 것은 **`IsSpawned` 를 앞에 두는 순서**이지 조건의 부호가 아니다.

#### 선례 (전부 실측 확인)

| 위치 | 코드 | 성격 |
|---|---|---|
| `NetworkCombatController:380` (`Update`) | `if (!IsSpawned \|\| !IsServer \|\| _combatStopped) return;` | 자기 자신의 `IsSpawned` |
| `NetworkCombatController:925` (`OnUnitDied`) | `if (!IsSpawned \|\| !IsServer) return;` | 자기 자신 |
| `NetworkCombatController:1004` (`OnBuildingDied`) | `if (!IsSpawned \|\| !IsServer) return;` | 자기 자신 |
| `NetworkCombatController:1079` (`OnUnitEnteredCombatHandler`) | `if (!IsSpawned \|\| !IsServer) return;` | 자기 자신 |
| `NetworkCombatController:885` (`SetUnitAnimState`) | `if (!IsSpawned) return;` | 자기 자신 — **길목 한 곳에서 5개 호출부를 덮는 형태** |
| `NetworkUnit:291` (`ReapplyAnimStateToView`) | `if (!IsSpawned \|\| IsServer) return;` | 자기 자신 — **순서의 선례**(부호는 반대) |
| `NetworkGameEndController:457` | `if (netObj != null && netObj.IsSpawned == true && ...)` | **다른 오브젝트**의 `IsSpawned` 검사 — 이번 가드와 **형태가 다르다** |
| `UnitFactory:533` | `if (networkObject != null && networkObject.IsSpawned)` | **다른 오브젝트**의 `IsSpawned` 검사 — 이번 가드와 **형태가 다르다** |

> 마지막 두 건은 *"이 프로젝트가 `IsSpawned` 를 신뢰하고 이미 쓰고 있다"* 는 근거는 되지만,
> **이번에 넣는 "자기 자신의 진입부 가드" 와는 형태가 다릅니다.** 같은 것처럼 뭉뚱그리지 않고 구분해 적습니다(규칙 10).

### 6-2. 판단 1 — 가드 없는 2곳에 `IsServer` 를 새로 붙이면 동작이 바뀌는가

**답: 바뀌지 않는다. 두 곳 모두 `!IsSpawned || !IsServer` 를 그대로 적용한다.**

직전 작업이 `OnUnitEnteredCombatHandler` 에 대해 내린 판단과 **같은 근거가 성립하는지 구독 지점을 직접 확인했습니다.**

| 파일 | 구독 지점 | `IsServer` 블록 안인가 |
|---|---|---|
| `NetworkResourceSync` | `OnNetworkSpawn` **103~105행**: `if (IsServer) { SubscribeResourceChanged(); ... }` | **예** |
| `NetworkTileSync` | `OnNetworkSpawn` **84~90행**: `if (IsServer) { SubscribeTileOwnerChanged(); ... }` | **예** |

두 곳 모두 **구독 자체가 `if (IsServer)` 블록 안에서만 이루어지므로, 원래도 서버에서만 호출되던 자리**입니다.
따라서 `IsServer` 조건이 새로 붙어도 **이미 항상 참이던 조건을 명시적으로 적는 것**일 뿐이며, 걸러지는 호출이 새로 생기지 않습니다.
→ **`IsSpawned` 만 추가하는 선택지는 채택하지 않습니다.** 같은 파일·같은 프로젝트 안에서 가드 모양이 갈리는 비용이 더 큽니다.

> `SubscribeResourceChanged()` (145행) 와 `SubscribeTileOwnerChanged()` (119행) 는 **`public` 이 아니라 `private`** 이고,
> 호출부는 각각 `OnNetworkSpawn` 한 곳뿐임을 확인했습니다 — 외부에서 서버가 아닌 상태로 구독을 열 수 있는 경로가 없습니다.

### 6-3. 판단 2 — 길목 하나로 덮을 수 있는 자리가 있는가

**답: 없다. 호출부(진입점)마다 붙인다.**

직전 작업은 Walk·HealCast·Freeze 세 핸들러를 `SetUnitAnimState` **한 곳**에서 막았습니다. 그 자리가 길목이었던 이유는
**세 호출부가 모두 같은 메서드 하나를 통과해 같은 동작(`NetworkUnit.SetAnimState` → `NetworkVariable` 쓰기)에 도달**했기 때문입니다.

이번 7~8곳은 그렇지 않습니다. **각 진입점이 서로 다른 RPC 를 직접 호출합니다** — 실측:

| 진입점 | 직접 호출하는 대상 | 공유하는가 |
|---|---|---|
| `OnResourceChangedOnServer` | `_blueGold.Value` / `_redGold.Value` | 없음 |
| `OnTileOwnerChangedOnServer` | `BroadcastTileChangeClientRpc` | 없음 |
| `OnGameEndServer` | `AnnounceWinnerClientRpc` | 없음 |
| `OnEntityDamaged` | `SyncHealthClientRpc` | 없음 |
| `OnEntityHealed` | `SyncBuildingHealClientRpc` **또는** `SyncHealClientRpc` (분기) | 없음 |
| `OnProductionStarted` | `ProductionStartedClientRpc` | 없음 |
| `OnUnitProduced` | `SpawnUnitClientRpc` | 없음 |
| `OnProductionQueueChanged` | `SyncQueueStateClientRpc` | 없음 |

**공유 길목이 존재하지 않습니다.** 8개 진입점이 8개(분기 포함 9개)의 서로 다른 RPC/쓰기에 1:1 로 대응합니다.

> **`[ClientRpc]` 메서드 안쪽에 가드를 넣는 것은 해결책이 아니다.**
> 오류는 RPC **본문이 실행될 때**가 아니라 **호출(invoke)될 때** 발생합니다(*"Rpc methods can only be **invoked** after starting the NetworkManager"*).
> 따라서 가드는 **호출하기 전**, 즉 핸들러 진입부에 있어야 합니다.

`NetworkProductionController` 의 세 핸들러가 공유하는 `_services?.GetUnitProduction()` → `production.GetState(...)` 도로는
**RPC 호출보다 앞에 있긴 하지만 `OnUnitProduced` 는 지나지 않으므로**(278~309행) 길목이 아닙니다.

### 6-4. 판단 3 — 가드에 로그를 추가할 것인가

**답: 넣지 않는다.**

| 근거 | 내용 |
|---|---|
| 선례 | 직전 작업의 가드 6곳 **어디에도 로그가 없다.** 추가된 로그는 `게임 종료 — 전투 틱 정지` **1줄뿐**이고, 그것은 가드가 아니라 **상태 전이** 지점이다(`LogRules.md` 1053~1056행) |
| `LogRules` 1.14 **금지 8** | *"매 틱·매 프레임 로깅 금지 — 상태 **전이** 시점에만 남긴다. 스팸은 정작 필요한 줄을 묻어 버린다"*. 이번 가드는 **매 이벤트마다 평가**되며 전이 지점이 아니다 |
| 정상 흐름이다 | 가드에 걸리는 것은 **버그가 아니라 설계대로 종료되는 중**이라는 뜻이다. `LogRules` 축 A 기준으로 `Warn`/`Error` 대상이 아니고, `Info` 로 남길 가치도 없다 |
| 실효성 | 정말 필요한 정보(*"이 자리가 종료 후에 실제로 불렸는가"*)는 로그가 아니라 **에러가 사라졌는가**로 확인된다(§9 검증 방법) |

> **단, 진단이 필요해지면**: `LogRules` **1.11** 이 허용하는 **`임시` 로그**를 그때 한시적으로 넣고
> **1.14 금지 10**(*"`임시` 로그 코드를 남긴 채 작업 종료 금지"*)에 따라 반드시 제거합니다. 이번 계획에는 포함하지 않습니다.

---

## 7. 파일별 변경 계획

### 공통 방침

- 각 진입점의 **첫 줄**에 `if (!IsSpawned || !IsServer) return;` 을 둡니다.
- 이미 `if (!IsServer) return;` 이 있는 자리는 **그 줄을 대체**합니다(줄이 늘지 않습니다).
- **주석은 짧게 쓰고 직전 커밋의 상세 주석을 가리킵니다.** 같은 설명을 8곳에 복제하면 나중에 한 곳만 고쳐져 서로 어긋납니다.
  직전 커밋이 이미 쓰는 형식을 그대로 따릅니다(`NetworkCombatController.cs` 919~921·1076~1078행).
- **`_services == null` 등 기존 가드는 순서를 포함해 한 줄도 건드리지 않습니다.**
- **기존 로직 제거는 없습니다** — 전부 조건 추가입니다. 따라서 WORKFLOW [4] 「기존 로직 제거 규칙」의 비활성화(주석 처리) 절차는 **해당 없음**입니다.

### 삽입할 주석 형식 (8곳 공통)

```csharp
// 이 오브젝트가 아직 네트워크에 살아 있고 서버일 때만 진행한다.
//   이유는 NetworkCombatController.Update() 진입부 가드와 같다 — 그쪽의 상세 주석 참조.
//   (요약: IsServer 는 "내가 서버 역할인가" 이지 "이 오브젝트가 아직 살아 있는가" 가 아니다.
//    위험 구간은 NetworkManager.Shutdown() 과 디스폰 사이다.)
if (!IsSpawned || !IsServer) return;
```

### 7-1. `Assets/_Project/Scripts/Infrastructure/Network/NetworkResourceSync.cs`

| 항목 | 내용 |
|---|---|
| 대상 | `OnResourceChangedOnServer(ResourceChangedEvent evt)` — **170행** |
| 현재 | 가드 없음. 171행에서 곧바로 `switch (evt.Team)` 진입 |
| 변경 | 171행 `switch` 앞에 `if (!IsSpawned || !IsServer) return;` **추가** |
| 근거 | `NetworkVariable` 쓰기(175·178행)가 네트워크 생존을 전제한다. `IsServer` 추가가 안전한 근거는 §6-2 |
| 추가 주석 | 위 공통 형식 + *"`IsServer` 가 새로 붙지만 구독이 `OnNetworkSpawn` 의 `IsServer` 블록(103행) 안에서만 이루어지므로 원래도 서버에서만 호출되던 자리다"* |

### 7-2. `Assets/_Project/Scripts/Infrastructure/Network/NetworkTileSync.cs`

| 항목 | 내용 |
|---|---|
| 대상 | `OnTileOwnerChangedOnServer(TileOwnerChangedEvent evt)` — **144행** |
| 현재 | 가드 없음. 147행에서 곧바로 `BroadcastTileChangeClientRpc(...)` 호출 |
| 변경 | 147행 앞에 `if (!IsSpawned || !IsServer) return;` **추가** |
| 근거 | `ClientRpc` 호출. `IsServer` 추가가 안전한 근거는 §6-2 |
| 주의 | **165행 `BroadcastTileChangeClientRpc` 안의 `if (IsServer) return;` (169행) 은 건드리지 않는다** — 그것은 "서버는 이미 처리했으니 중복 방지" 라는 **완전히 다른 목적**의 가드다. 부호도 반대다 |

### 7-3. `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameEndController.cs`

| 항목 | 내용 |
|---|---|
| 대상 | `OnGameEndServer(GameEndEvent e)` — **161행** |
| 현재 | **163행** `if (!IsServer) return;` / 164행 `if (_announced) return;` |
| 변경 | 163행을 `if (!IsSpawned || !IsServer) return;` 로 **대체**. **164행 `_announced` 가드는 그대로 둔다** |
| 근거 | 183행 `AnnounceWinnerClientRpc` 호출 |
| 주의 | `_announced` 는 *"중복 발표 방지"* 이고 새 가드는 *"네트워크 생존"* 이다. **목적이 다르므로 합치지 않는다.** 순서도 바꾸지 않는다 |

### 7-4. `Assets/_Project/Scripts/Infrastructure/Network/NetworkHealthSync.cs` (2곳)

| 항목 | 내용 |
|---|---|
| 대상 ① | `OnEntityDamaged(EntityDamagedEvent e)` — **118행** |
| 현재 | **120행** `if (!IsServer) return;` / 121행 `if (e.Entity == null) return;` |
| 변경 | 120행을 `if (!IsSpawned || !IsServer) return;` 로 **대체** |
| 대상 ② | `OnEntityHealed(EntityHealedEvent e)` — **161행** |
| 현재 | **163행** `if (!IsServer) return;` / 164행 `if (e.Entity == null) return;` |
| 변경 | 163행을 `if (!IsSpawned || !IsServer) return;` 로 **대체** |
| 근거 | ①은 148행 `SyncHealthClientRpc`, ②는 169행 `SyncBuildingHealClientRpc` **또는** 179행 `SyncHealClientRpc` |
| 주의 | ②의 **167~171행 건물/유닛 분기 로직은 손대지 않는다.** 새 가드는 그 분기보다 **앞**에 있어 두 경로를 모두 덮는다 |

### 7-5. `Assets/_Project/Scripts/Infrastructure/Network/NetworkProductionController.cs` (2곳 + 승인 시 1곳)

| 항목 | 내용 |
|---|---|
| 대상 ① | `OnProductionStarted(ProductionStartedEvent e)` — **194행** |
| 현재 | **196행** `if (!IsServer) return;` |
| 변경 | 196행을 `if (!IsSpawned || !IsServer) return;` 로 **대체** |
| 대상 ② | `OnUnitProduced(UnitProducedEvent e)` — **278행** |
| 현재 | **280행** `if (!IsServer) return;` |
| 변경 | 280행을 `if (!IsSpawned || !IsServer) return;` 로 **대체** |
| 대상 ③ | `OnProductionQueueChanged(ProductionQueueChangedEvent e)` — **223행** — **§3-2 승인 시에만** |
| 현재 | **225행** `if (!IsServer) return;` |
| 변경 | 225행을 `if (!IsSpawned || !IsServer) return;` 로 **대체** |
| 근거 | ①은 204행 `ProductionStartedClientRpc`, ②는 300행 `SpawnUnitClientRpc`, ③은 263행 `SyncQueueStateClientRpc` |
| 주의 | ①·③의 `production == null` / `state == null` 가드(198~202행 · 227~231행)는 **순서를 포함해 그대로 둔다.** 새 가드는 그 앞에 온다 |
| 주의 | **`ServerRpc` 계열(`RequestEnqueueServerRpc` 등)과 그 UI 래퍼(108~148행)는 이번 범위가 아니다** — §10 참조 |

### 7-6. 변경 요약

| 파일 | 추가 | 대체 | 합계 |
|---|---|---|---|
| `NetworkResourceSync.cs` | 1 | 0 | 1 |
| `NetworkTileSync.cs` | 1 | 0 | 1 |
| `NetworkGameEndController.cs` | 0 | 1 | 1 |
| `NetworkHealthSync.cs` | 0 | 2 | 2 |
| `NetworkProductionController.cs` | 0 | 2 (+1) | 2 (+1) |
| **합계** | **2** | **5 (+1)** | **7 (+1)** |

**변경 파일: 5개. 새 파일 없음. 삭제 없음. 프리팹·씬·에셋 변경 없음.**

---

## 8. 근거 규칙 (`GameSystemRules`) — WORKFLOW [4]

### 8-1. 직접 근거 규칙 — **해당 없음**

`GameSystemRules.md` 인덱스와 `GameSystemRules/` 하위 파일 전체를 대상으로
`IsSpawned` / `Shutdown` / `디스폰` 을 검색한 결과 **0건**입니다.

> **네트워크 종료 시점의 가드나 디스폰 이후 동작을 규정한 규칙은 존재하지 않습니다.**
> 따라서 이번 수정에 대응하는 **직접 근거 규칙은 「해당 없음」** 입니다. (직전 작업도 같은 검색으로 같은 결론에 도달했습니다 — 직전 Plan §5-1)

### 8-2. 간접적으로 맞닿는 규칙 — **전부 "유지" 임을 확인**

이번 변경이 기존 규칙을 **위반하거나 약화시키지 않는지** 확인한 결과입니다.

| 규칙 | 규칙이 요구하는 것 | 이번 변경과의 관계 |
|---|---|---|
| `GameSystemRules_Units.md` **규칙 3** (공유 타일 상태) | *"멀티플레이: 서버에만 존재. 클라이언트는 결과를 받는다"* | `NetworkTileSync` 가드는 **네트워크가 끊긴 뒤의 전파만** 막는다. 정상 구간의 서버 권위 전파는 그대로 |
| `GameSystemRules_Buildings.md` **방어 타워 시스템 규칙 9** (서버 권위 처리) | *"타겟 선택·사거리 판정·데미지 처리는 서버에서만 실행. 클라이언트는 결과를 받아 시각 표현에만 사용"* | `NetworkHealthSync` 가드는 **결과 전달 경로**에만 붙는다. 데미지 계산 자체는 손대지 않는다 |
| `GameSystemRules_UI.md` **생산 패널 UI 규칙 6** (슬롯 구성) · **규칙 9·10** (골드 차감 시점) | 큐 슬롯 구성과 골드 차감 타이밍 | `NetworkProductionController` 가드는 **큐 상태를 클라이언트에 전달하는 경로**에만 붙는다. 큐 규칙·차감 시점 계산은 무변경 |
| `GameSystemRules_Upgrade.md` (서버 권위) | 연구 진행의 서버 권위 | 이번 변경 대상 아님 |

> **`GameSystemRules_UI.md` 와 `GameSystemRules_Buildings.md` 는 섹션마다 규칙 번호가 1부터 다시 시작하므로 섹션명을 함께 적었습니다.**

### 8-3. 규칙 신설 제안 (이번 작업에서는 하지 않음)

직전 작업이 남긴 제안이 그대로 유효합니다 — *"네트워크 정지 후 RPC/NetworkVariable 접근을 금지한다"* 는 규칙을
`GameSystemRules_Units.md` 등에 신설하는 안입니다.
**이번에도 하지 않습니다.** 규칙 신설은 별건 승인 사항이고 요청 범위 밖입니다(CLAUDE.md 규칙 6).
다만 **같은 제안이 두 작업 연속으로 나온 것**은 기록해 둡니다 — 세 번째가 나오면 규칙화를 진지하게 검토할 근거가 됩니다.

---

## 9. 위험 요소 — **가드 추가로 정상 동작이 막히지 않는가**가 최우선

### 9-1. 최우선 위험 — 정상 구간에서 걸러지면 안 되는 호출이 걸러지는 것

| 물음 | 검증 | 판정 |
|---|---|---|
| 게임 플레이 중 `IsSpawned` 가 거짓이 되는 순간이 있는가 | 이 5개 컴포넌트는 **씬에 미리 배치된 씬 오브젝트**이며, 스폰은 Host 시작 시 1회, 디스폰은 종료 시 1회다. 중간에 오르내리지 않는다 | **낮음** |
| `IsServer` 를 새로 붙이는 2곳에서 걸러지는 호출이 생기는가 | 두 곳 모두 구독이 `if (IsServer)` 블록 안 (§6-2 실측) | **없음** |
| 싱글플레이가 영향받는가 | 싱글에서는 이 컴포넌트들이 스폰되지 않아 **구독 자체가 일어나지 않는다**(`NetworkResourceSync.cs` 21행 · `NetworkTileSync.cs` 23행 주석이 명시). 핸들러가 호출될 일이 없다 | **없음** |
| 기존 `_announced` / `null` 가드와 충돌하는가 | 전부 **앞에 추가**만 하며 기존 가드의 순서·조건을 바꾸지 않는다 (§7) | **없음** |
| 클라이언트 수신 경로(`[ClientRpc]` 본문)가 영향받는가 | **손대지 않는다.** 변경은 서버 측 **발신** 경로에만 | **없음** |

**⚠️ 그럼에도 가장 조심할 지점**: `NetworkTileSync` 는 **같은 파일 안에 부호가 반대인 `if (IsServer) return;`(169행)** 이 있습니다.
**두 가드를 혼동해 잘못된 쪽을 고치면 클라이언트 타일 색이 통째로 죽습니다.** §7-2 에 명시했으며 구현 시 최우선 확인 대상입니다.

### 9-2. 부수 위험

| 위험 | 평가 |
|---|---|
| 게임 종료 시점에 마지막 골드/타일/생산 동기화 1건이 유실될 수 있다 | **의도된 동작이다.** 그 직후 씬이 로비로 전환되므로 표시할 화면 자체가 사라진다. 직전 작업도 같은 판단을 했다 |
| `#3 OnGameEndServer` 에 가드를 넣어 **승자 발표가 막히는** 것 아닌가 | **아니다.** 정상 경로에서는 게임 종료 → 발표 → (수 초 뒤) Shutdown 순서다. 실기 로그가 이를 뒷받침한다: 종료 `13:33:58.860` → 발표 `.861` → Shutdown `13:34:01.467` (**2.6초 차**). 발표는 Shutdown 훨씬 전에 끝난다 |
| 성능 영향 | `IsSpawned` 는 필드 조회다. 이벤트당 1회 평가로 측정 가능한 영향 없음 |
| 컴파일 위험 | `IsSpawned` 는 `NetworkBehaviour` 의 공개 프로퍼티이고 5파일 전부 `NetworkBehaviour` 상속이다. 새 `using` 불필요 |

### 9-3. ⚠️ 확정하지 못하고 남긴 항목 — **추정으로 메우지 않았다** (CLAUDE.md 규칙 10)

| # | 항목 | 왜 확정하지 못했는가 | 이번 계획에 미치는 영향 |
|---|---|---|---|
| A | **디스폰 이후 `NetworkVariable` 쓰기가 RPC 와 같은 오류를 내는가** (#1 `NetworkResourceSync`) | 실기에서 관측된 적이 없고 NGO 내부 동작이다. 직전 커밋도 같은 물음을 미해결로 남겼다(`NetworkCombatController.cs` 883~884행) | **없음.** 오류가 나든 안 나든 종료 후 쓰기는 무의미하므로 막는 것이 맞다 |
| B | **5개 컴포넌트의 디스폰 순서** | `NetworkCombatController` 는 디스폰 로그를 남기지 않아 로그로 순서를 알 수 없다. 로그에 보이는 3건(`NetworkResourceSync` → `NetworkTileSync` → `ReconnectionHandler`)은 1~2ms 간격이다 | **주의 필요.** `NetworkContext.Reset()` 이 `NetworkCombatController.OnNetworkDespawn`(314행)에 있어, 그것이 **먼저** 실행되면 `GameBootstrapper` 의 싱글플레이 분기가 켜지면서 `_unitCombat`·`_towerCombat` 틱이 되살아난다. 그 사이 `NetworkHealthSync` 가 아직 구독 중이면 #4·#5 의 가능성이 §5-3 판정보다 **높아진다.** → **이번 가드는 그 경우에도 안전 측이다**(막는 방향) |
| C | **종료 없이 Shutdown 되는 경로에서 `#3` 이 실제로 발생하는가** | 연결 끊김·강제 종료 경로의 실기 로그가 없다 | **없음.** 가드는 어느 쪽이든 안전 측이다 |
| D | **`#8` 을 포함할지** | **사용자 승인 사항** (§3-2) | 승인 전까지 7곳만 진행 |

---

## 10. 검증 방법 — 자연어로

### 10-1. 정직하게 먼저 밝힐 것

**이 종류의 버그는 "고쳐졌음" 을 적극적으로 보여 주기 어렵습니다.**

- 정상 동작은 **아무 일도 일어나지 않는 것**입니다. 화면에 나타나는 새 결과가 없습니다.
- 버그 자체가 **수십 밀리초 안에 조건이 겹쳐야** 나타나므로, **의도적으로 재현할 방법이 없습니다.**
- **이번 7~8곳은 애초에 오류가 난 적이 0회**입니다. 따라서 *"오류가 사라졌다"* 는 확인조차 성립하지 않습니다.

그래서 검증의 실질은 **"멀쩡히 돌아가던 것이 망가지지 않았는가"** 를 보는 쪽입니다. 그것이 §9-1 을 최우선 위험으로 둔 이유입니다.

### 10-2. 실기에서 무엇을 보면 되는가

**① 정상 동작이 그대로인지 (가장 중요)** — 호스트 1대 + 클라이언트 1대로 한 경기를 끝까지 진행하며 확인합니다.

| 확인 대상 | 무엇을 보는가 | 이 항목이 죽으면 |
|---|---|---|
| 골드 | 양쪽 화면의 골드 숫자가 **서로 같은 값으로** 계속 오르는가 | `NetworkResourceSync` 가드 오류 |
| 타일 색 | 유닛이 지나간 타일이 **양쪽 화면 모두에서** 팀 색으로 바뀌는가 | `NetworkTileSync` 가드 오류 (특히 §9-1 의 혼동 위험) |
| 체력 | 유닛이 맞을 때 체력바와 피해 숫자가 **양쪽에서** 줄어드는가 / 힐 받을 때 회복 표시가 뜨는가 | `NetworkHealthSync` 가드 오류 |
| 생산 | 생산 진행 바가 **클라이언트 화면에서도** 차오르는가 / 완성된 유닛이 양쪽에 나타나는가 | `NetworkProductionController` 가드 오류 |
| 큐 (#8 포함 시) | 취소·자동 생산 토글 결과가 **클라이언트 큐 화면에도** 반영되는가 | `OnProductionQueueChanged` 가드 오류 |
| 승패 | 성이 파괴됐을 때 **양쪽 화면에 승리/패배가 뜨는가** | `NetworkGameEndController` 가드 오류 |

**② 종료 경로에서 오류가 없는지** — 경기 종료 후 **[로비로]** 를 눌러 로비까지 돌아가는 동안,
유니티 콘솔에 빨간 오류가 뜨지 않는지 봅니다. 특히 아래 문구입니다.

```
Rpc methods can only be invoked after starting the NetworkManager!
```

이 경로는 **어제 오류가 2회 실제로 재현된 바로 그 경로**입니다. 여러 번 반복하면 확인 가치가 올라갑니다.

**③ 싱글플레이가 멀쩡한지** — 싱글 경기를 한 판 돌려 골드·타일·생산·전투가 평소와 같은지 봅니다.
이 컴포넌트들은 싱글에서 스폰되지 않으므로 **원리상 영향이 없어야** 합니다(§9-1).

**④ 로그 파일 확인** — `_Logs/_editor/<날짜>/RuntimeLog.txt` 에서 `[ERROR]` 가 0건인지 봅니다.
`LogRules` **1.9** 의 전역 훅이 엔진 오류까지 수집하므로, RPC 오류가 났다면 이 파일에 남습니다.

> **⚠️ 다만 그 훅 자체가 아직 실기로 검증되지 않았습니다** — `LogRules.md` **1.13** 이 *"B(엔진 오류 수집) 미검증"* 으로 기록해 두었고,
> 3경기를 더 돌렸는데도 오류가 한 건도 나지 않아 확인 기회가 없었습니다.
> **따라서 "로그에 `[ERROR]` 가 없다" 를 "오류가 없었다" 의 증거로 쓰면 안 됩니다.** 콘솔을 함께 봐야 합니다.

### 10-3. Testcase / QA 는 진행하지 않는다

**WORKFLOW [5-1]~[5-3]** 에 따라 TC 작성과 QA 테스트는 **사용자가 명시적으로 지시한 경우에만** 진행합니다.
이번 요청에는 그 지시가 없으므로 **먼저 제안하지도, 진행하지도 않습니다.**
위 §10-2 는 [6] 사용자 테스트에서 무엇을 보면 되는지를 안내하는 것이며 TC 문서가 아닙니다.

---

## 11. 범위 밖 — 손대지 않는 것과 그 이유

| 대상 | 현재 상태 | 왜 이번에 손대지 않는가 |
|---|---|---|
| **`NetworkUnit.SetAnimState`** (`NetworkUnit.cs` **170~176행**) | `if (!IsServer) return;` (173행) — `IsSpawned` 없음 | **호출부가 단 한 곳**이고(`NetworkCombatController.cs` 895행), 그 상위인 `SetUnitAnimState` 가 **직전 커밋에서 이미 `!IsSpawned` 로 막혔다**(885행). 길목이 이미 닫혀 있어 여기에 또 넣는 것은 중복이다. `.SetAnimState(` 전수 검색으로 호출부가 1곳뿐임을 확인했다 |
| **`NetworkGameEndController` 재경기 3종** (`_localRematch*` → `ServerRpc`, 124~131행) | 가드 없음 | §3-2 #9. 사용자 버튼 입력이 있어야만 발동하므로 수십 ms 안에 걸릴 여지가 사실상 없고, `IsServer` 블록 **밖** 구독이라 가드 형태(`!IsSpawned` 만)와 판단 근거가 별건이다. **별도 승인 사항으로 남긴다** |
| **`ServerRpc` 계열 전반** (`NetworkProductionController` 324행 이하, `NetworkBuildingController`, `NetworkUpgradeController`, `NetworkSkillController` 등) | 대부분 진입부 가드 없음 | **호출 주체가 UI(사용자 입력)** 이고, 종료 시점에는 UI 자체가 닫혀 있다. **이번 조사 대상은 "이벤트 구독으로 자동 실행되는 경로"** 이므로 성격이 다르다. 별건으로 다뤄야 한다 |
| **`NetworkVariable.OnValueChanged` 콜백들** (`NetworkResourceSync` 195·208행 등) | `if (IsServer) return;` (부호 반대) | **클라이언트 수신 경로**다. RPC 를 **보내지 않으므로** 이번 오류와 무관하다 |
| **`ReconnectionHandler.WaitAndForceWin`** | `yield return WaitForSeconds(30)` | `OnNetworkDespawn` 이 `StopCoroutine` 으로 정리한다(115~119행). **본문을 끝까지 읽어 구멍이 아님을 확인했다** (§2) |
| **`GameSystemRules` 규칙 신설** | 미존재 | §8-3. 별건 승인 사항 |
| **코드 주석의 `27ms` 표기 정정** | 직전 커밋이 4곳에 기재 | §3-4. 실측 범위는 6~41ms 지만 **문구 정정은 요청 범위 밖**이다(규칙 6). 이번 Plan 에 정확한 값을 남기는 것으로 갈음한다 |
| **`ProductionTicker` 에 종료 가드 추가** | 가드 없음 (§5-2) | **매력적이지만 범위 밖이다.** 길목으로는 더 근본적이나 `Presentation` 레이어이고, 생산·수입 틱 전체를 멈추는 것은 **동작 변경**이라 별도 설계 판단이 필요하다. **제안만 하고 진행하지 않는다** |

---

## 12. 승인받을 사항 (구현 전 확인)

1. **7곳(사전 확정분) 진행 승인** — §7
2. **#8 `OnProductionQueueChanged` 포함 여부** — §3-2. **포함을 권장**(형제 셋 중 둘만 막으면 가드 모양이 갈림)
3. **#9 재경기 3종 제외 승인** — §11
4. (참고) **`ProductionTicker` 종료 가드**는 별건 제안으로만 남깁니다 — §11

> **승인 전까지 코드는 한 줄도 수정하지 않습니다**(CLAUDE.md 규칙 11, WORKFLOW [4]).
> 승인 후 구현은 **game-programmer 에이전트에 위임**합니다(CLAUDE.md 규칙 3).
