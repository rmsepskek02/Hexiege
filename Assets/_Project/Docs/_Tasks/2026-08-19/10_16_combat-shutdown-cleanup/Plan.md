# Plan — 게임 종료 · 서버 종료 시점의 전투 뒷정리

> **기존 로직 제거 없음** (WORKFLOW.md [4] 「기존 로직 제거 규칙」)
> 이 작업은 **가드 한 줄 추가 + 구독 하나 추가**로만 이루어진다. 기존 코드를 지우거나 주석 처리하는 항목은 하나도 없다.
> 따라서 "비활성화 우선" 절차가 적용될 대상이 없다.

---

## 1. 이 작업이 무엇인지 — 일반 언어 설명 (CLAUDE.md 규칙 13)

게임에서 승패가 갈리면 화면에는 승리/패배 팝업이 뜨고, 플레이어 입장에서는 게임이 끝난 것으로 보입니다.
그런데 지금은 **화면 뒤에서 유닛들이 몇 초 더 계속 싸우고 있습니다.** 승패는 이미 확정돼 있으니 결과가 바뀌지는
않지만, 서버는 그 몇 초 동안 아무도 보지 않는 전투를 계속 계산하고, 그 결과를 상대방에게 계속 전송합니다.

문제는 그다음입니다. 잠시 뒤 서버(네트워크)가 실제로 꺼집니다. **아직 싸우고 있던 유닛이 하필 이 "꺼지는 순간"에
죽으면**, 게임은 "이 유닛이 죽었다"는 소식을 이미 꺼져 버린 네트워크로 보내려다 오류를 냅니다.
사용자가 눈으로 보는 화면은 멀쩡하지만, 로그에는 붉은 오류가 남습니다.

그래서 이번 작업은 두 가지를 합니다.

1. **소식을 보내기 전에 "네트워크가 아직 살아 있는지" 먼저 확인**하게 만든다 — 이미 같은 성질의 수정이
   직전 커밋에서 한 자리에 들어갔고, 이번은 **아직 남아 있는 나머지 자리들**입니다.
2. **승패가 갈린 순간 전투 계산 자체를 멈춘다** — 그러면 아무도 안 보는 계산이 사라지고, 동시에 **위 1번의
   "위험한 순간"에 죽을 유닛 자체가 없어집니다.**

즉 1번은 사고가 나도 다치지 않게 하는 안전벨트이고, 2번은 애초에 사고가 날 상황을 없애는 조치입니다. 둘은 짝입니다.

---

## 2. 배경

- 이 두 건은 **로그 이관 작업 중 발견**되어, 그 작업의 범위를 벗어나므로 별도 기록만 해 두었던 항목이다.
- 직전 커밋 **`cc864054` (fix(log): 엔진 오류 수집 + RPC 가드 + 연구 흐름 로그)** 가 **같은 성질의 문제**를 이미 한 자리 고쳤다.
  - 고친 자리: `NetworkCombatController.Update()` 310행 → `if (!IsSpawned || !IsServer) return;`
  - 그 주석(291~309행)에 남은 재현 근거: 2026-08-18 실기, `[로비로]` 경로에서
    `"Rpc methods can only be invoked after starting the NetworkManager!"` **2회 재현**.
- **이번 작업은 그 나머지**다. 대상 파일은 두 건 모두
  `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs` 하나다.
- 실측 근거 로그: `Assets/_Project/Docs/_Logs/_editor/2026-08-19/RuntimeLog.txt` 681~692행 부근.

---

## 3. ① `OnUnitDied` 가드 구멍

### 3-1. 현재 상태 (실측 — 코드 직접 확인)

```csharp
private void OnUnitDied(UnitDiedEvent e)      // 847행
{
    if (!IsServer) return;                     // ← 가드가 이것뿐
    if (e.Unit == null) return;
    ...
    EntityDiedClientRpc(unitId, true);         // ← 871행 : 네트워크 전송
    ...
    netObj.Despawn(destroy: true);             // ← 907행 : 네트워크 API 호출
}
```

직전에 고친 `Update()`(310행)와 **구조가 같은데 `IsSpawned` 만 빠져 있다.**
게다가 이 핸들러는 RPC 전송(871행)뿐 아니라 **`NetworkObject.Despawn()`(907행)** 도 호출한다 —
네트워크가 이미 정지한 뒤 호출되면 문제가 될 수 있는 API가 한 자리가 아니라 **두 자리**다.

### 3-2. 왜 ①은 `Update`(A)보다 덜 위험한가

| | `Update` — A, 이미 수정됨 | `OnUnitDied` — 이번 건 |
|---|---|---|
| 트리거 | **매 프레임 자동** | 이벤트 구독 — 유닛이 죽을 때만 |
| 디스폰 이후 | 씬이 바뀌기 전까지 **계속 돈다** → 가드가 없으면 반드시 터진다 | `OnNetworkDespawn`(226행)이 `_unitDiedSubscription?.Dispose()` 로 **구독을 끊는다** → 그 뒤로는 아예 호출되지 않는다 |
| 노출 구간 | 디스폰 이후 씬 전환까지 계속 | **`Shutdown()` 과 디스폰 사이로 한정** |

즉 ①은 "가드가 없으면 언젠가 반드시 터지는" 종류가 아니라, **좁은 구간에 유닛 사망이 겹쳐야만 터지는** 종류다.
그래서 A보다 우선순위는 낮지만, **구간이 존재하는 한 확률은 0이 아니다.**

### 3-3. 위험 구간 — 실측 27ms

`_Logs/_editor/2026-08-19/RuntimeLog.txt`:

```
13:34:01.467  [NetworkGameManager]  NetworkManager Shutdown 완료
13:34:01.494  [NetworkResourceSync] 디스폰 — 구독 해제 완료      ← +27ms
13:34:01.495  [NetworkTileSync]     디스폰 — 이벤트 구독 해제
13:34:01.496  [ReconnectionHandler] 디스폰 — 콜백 해제 완료
```

- **27ms** 가 "네트워크는 이미 꺼졌는데 구독은 아직 살아 있는" 구간의 실측 폭이다.
- ⚠️ **정확히 하자면**: `NetworkCombatController` 자체는 디스폰 로그를 남기지 않는다. 위 `.494 / .495 / .496`
  세 줄은 각각 다른 씬 NetworkObject 의 디스폰 로그다. 세 오브젝트가 **같은 NGO 종료 패스**에서 연달아
  디스폰됐으므로, `NetworkCombatController.OnNetworkDespawn`(=구독 해제)도 같은 구간에 있다고 본다.
  "27ms" 는 그 **종료 패스의 실측 폭**이지 이 컨트롤러 전용 계측값이 아니다.
- 이 27ms 안에 유닛이 하나라도 죽으면 `OnUnitDied` → `EntityDiedClientRpc` 로 A와 같은 RPC 오류가 난다.

### 3-4. 채택안

```csharp
if (!IsSpawned || !IsServer) return;
```

- `Update`(310행)와 **완전히 동일한 형태**를 쓴다. 새로운 판단 기준을 만들지 않는다.
- 순서(`IsSpawned` 를 앞에)도 그대로 따른다 — 단락 평가로 미스폰 상태에서 `IsServer` 를 건드리지 않는다.
- 선례: `NetworkUnit.cs:291`(`ReapplyAnimStateToView` — `if (!IsSpawned || IsServer) return;`) 외
  `NetworkCombatController.Update:310`.

### 3-5. ⚠️ 전수 확인 결과 — **구멍은 `OnUnitDied` 하나가 아니다**

`OnNetworkSpawn` 의 `if (IsServer)` 블록(172~213행)에서 구독하는 핸들러는 **총 6개**다. 전부 확인했다.

| # | 구독(행) | 핸들러(행) | 현재 가드 | 네트워크 호출 | 판정 |
|---|---|---|---|---|---|
| 1 | `OnUnitDied` (182) | `OnUnitDied` (847) | `!IsServer` 만 | `EntityDiedClientRpc`(871), `netObj.Despawn`(907) | **구멍** |
| 2 | `OnBuildingDied` (184) | `OnBuildingDied` (920) | `!IsServer` 만 | `EntityDiedClientRpc`(931) | **구멍 — 1과 완전 동일 구조** |
| 3 | `OnUnitWalkStarted` (193) | `OnUnitWalkStartedHandler` (703) | **가드 없음** | `SetUnitAnimState` → NetworkVariable 쓰기 | **구멍(성격 다름 — 3-5-2)** |
| 4 | `OnUnitEnteredCombat` (197) | `OnUnitEnteredCombatHandler` (778) | **가드 없음** | `StartCombatClientRpc`(814·828) + `ExecuteAttack` | **구멍 — 가장 노출이 크다** |
| 5 | `OnUnitHealCastStarted` (202) | `OnUnitHealCastStartedHandler` (725) | **가드 없음** | `SetUnitAnimState` → NetworkVariable 쓰기 | **구멍(성격 다름)** |
| 6 | `OnUnitFreezeChanged` (207) | `OnUnitFreezeChangedHandler` (737) | **가드 없음** | `SetUnitAnimState` → NetworkVariable 쓰기 | **구멍(성격 다름)** |

> **하나만 고치면 같은 버그가 다른 경로로 그대로 재발한다.** 특히 2번(`OnBuildingDied`)은 1번과 글자 그대로 같은 구조이고,
> 4번은 **가드가 아예 없는 채로 RPC 를 두 자리에서 보낸다.**

#### 3-5-1. `EntityDiedClientRpc` / `StartCombatClientRpc` 를 보내는 3개 (1·2·4) — 같은 한 줄로 처리

세 곳 모두 핸들러 진입부에 `if (!IsSpawned || !IsServer) return;` 를 둔다.

- 3·4·5·6번은 원래 `IsServer` 가드도 없다. 서버에서만 구독하므로 **동작상 문제는 없었지만**,
  `IsSpawned` 를 넣는 김에 `Update`·`OnUnitDied` 와 형태를 통일한다(같은 파일 안에서 가드 모양이 갈리면
  다음 사람이 "여긴 왜 다르지" 를 판단해야 한다).

#### 3-5-2. `SetUnitAnimState` 를 타는 3개 (3·5·6) — **한 곳에서 막는다**

세 핸들러는 모두 같은 파일의 `SetUnitAnimState`(749행) 하나를 통과한다.
그리고 `SetUnitAnimState` 는 `TickCombat`(528행)과 `OnUnitEnteredCombatHandler`(810·825행)에서도 호출된다.

→ **핸들러 3곳에 각각 넣는 대신 `SetUnitAnimState` 진입부에 `if (!IsSpawned) return;` 한 줄을 둔다.**
   호출 지점 5곳이 한 번에 덮이고, 중복 가드가 흩어지지 않는다.

이 세 건이 "성격이 다르다"고 표기한 이유:

- 이들이 하는 일은 RPC 전송이 아니라 **`NetworkUnit.SetAnimState`(`NetworkUnit.cs:170`) 의 NetworkVariable 쓰기**다.
  `SetAnimState` 자체는 `if (!IsServer) return;` 만 갖고 있고 `IsSpawned` 는 보지 않는다.
- **⚠️ 확인 필요(추정하지 않음 — CLAUDE.md 규칙 10)**: 디스폰 이후 NetworkVariable 쓰기가 RPC 와 똑같이
  오류를 내는지는 **이 환경에서 NGO 패키지 소스를 열 수 없어 확정하지 못했다.** 실기 로그에도 해당 오류가
  찍힌 기록이 없다. 따라서 이 세 건은 **"오류가 확인된 수정"이 아니라 "같은 구간을 같은 방식으로 막는 예방"** 이다.
  `NetworkUnit.cs:291`(`ReapplyAnimStateToView`)이 이미 `IsSpawned` 를 판단 기준으로 쓰고 있다는 점만이 근거다.
- `NetworkUnit.SetAnimState` 쪽에 가드를 넣는 것이 더 근본적일 수 있으나, **다른 파일이므로 이번 범위에 넣지 않는다**
  (CLAUDE.md 규칙 6). §9 범위 밖에 제안으로 남긴다.

---

## 4. ② 게임 종료 후에도 전투 루프가 계속 돈다

### 4-1. 현재 상태 (실측 — 코드 직접 확인)

`NetworkCombatController` 가 구독하는 이벤트는 §3-5 표의 **6개뿐이고, 게임 종료 이벤트는 그중에 없다.**
`GameEvents.OnGameEnd` 를 이 컨트롤러는 **구독하지 않는다.**

그래서 승패가 확정된 뒤에도 `Update` → `TickCombat` 이 50ms 간격으로 계속 돈다.

```
13:33:58.849  [NetworkCombatController] 서버: 건물 사망 | BuildingId=1     ← 성(Castle) 파괴
13:33:58.860  [NetworkGameEndController] 서버: 게임 종료 감지 — 결과 전파 시작 | WinnerTeam=Blue
   ↓                                     ← 이 구간 내내 TickCombat 이 계속 돈다
13:34:01.467  [NetworkGameManager] NetworkManager Shutdown 완료
```

**2.607초** (13:33:58.860 → 13:34:01.467).

### 4-2. 실제 피해 — 버그라기보다 낭비

| 항목 | 실제 영향 |
|---|---|
| 승패 | **이미 확정. 바뀌지 않는다** (`GameEndUseCase.IsGameOver` 가 재판정을 막고, `NetworkGameEndController._announced` 가 재전파를 막는다) |
| 화면 | 승리/패배 팝업이 떠 있어 **사용자에게 보이지 않는다** |
| RPC | `StartCombat` / `ChangeTarget` / `StopCombat` / `EntityDied` 가 **2.6초간 불필요하게 계속 전송** |
| 서버 부하 | 유닛 전수 순회 + 타워/파도/HoT/자연회복/연구/쿨다운/물안개/상태효과 틱이 2.6초 더 실행 |

### 4-3. ①과의 상호작용 — **이게 이번 작업에서 둘을 같이 하는 이유다**

②를 고치지 않으면 ①의 위험 창이 **27ms 가 아니라 사실상 2.6초로 벌어진다.**

- 전투가 계속 돌면 → 유닛이 계속 죽는다 → **"죽을 기회" 자체가 2.6초 내내 존재한다.**
- 실제로 위 로그에서 게임 종료 직전 구간(13:33:35~13:33:57)에만 유닛 사망이 **9건** 찍혀 있다.
  즉 종료 시점 부근의 유닛 사망 빈도는 결코 낮지 않다.
- ②로 전투를 멈추면 종료 후 신규 사망이 사라지고, ①이 감당해야 할 구간은 **§3-3의 27ms 로 되돌아간다.**

**정리: ②는 사고 확률을 낮추고, ①은 사고가 나도 오류가 안 나게 한다. 어느 한쪽만으로는 부족하다.**

### 4-4. 채택 방향

**`GameEvents.OnGameEnd`(`Application/Events/GameEvents.cs:1064` — `Subject<GameEndEvent>`)를 서버에서 구독해
전투 틱을 멈춘다.**

### 4-5. 결정 사항 1~5 — 확정된 답

#### 결정 1. 멈추는 방식 → **플래그 채택** (구독 해제 방식 기각)

**채택: `private bool _combatStopped = false;` 를 두고 `Update` 진입부에서 반환한다.**

```csharp
if (!IsSpawned || !IsServer || _combatStopped) return;
```

**구독 해제 방식을 기각한 근거(추정 아님 — 코드 확인):**

`GameEndUseCase.OnBuildingDied`(`GameEndUseCase.cs:59~80`)는 **`GameEvents.OnBuildingDied` 의 디스패치 도중
동기적으로** `GameEvents.OnGameEnd.OnNext(...)` 를 호출한다(79행).
따라서 우리 `OnGameEnd` 핸들러도 **`OnBuildingDied` 디스패치가 끝나기 전에** 실행된다.

그 안에서 `_buildingDiedSubscription.Dispose()` 를 호출하면 → **자기가 디스패치되고 있는 Subject 의 구독자
목록을 디스패치 도중에 변경**하게 된다. 만약 `NetworkCombatController.OnBuildingDied` 가 `GameEndUseCase` 보다
**나중에** 구독돼 있었다면, **게임을 끝낸 바로 그 성(Castle) 파괴에 대한 `EntityDiedClientRpc` 가 영영 전송되지
않는다** → 클라이언트에 성이 남고, 클라이언트 `GameEndUseCase` 도 발동하지 않는다.

> 실측상 이번 로그에서는 `NetworkCombatController` 의 건물 사망 처리(.849)가 게임 종료 감지(.860)보다 **먼저**
> 찍혀 있어 이번 실행에서는 순서가 안전한 쪽이었다. 그러나 **구독 순서는 설계로 보장된 것이 아니라
> `GameBootstrapper` 초기화와 `OnNetworkSpawn` 의 상대 순서에 달려 있다.** 보장되지 않는 순서에 정합성을
> 의존시키는 구조는 채택하지 않는다.

플래그는 이 위험이 아예 없고, 사망 전파(①의 대상)는 그대로 살아 있어야 하므로 **"틱만 멈추고 구독은 유지"** 가
의도에도 정확히 맞는다.

**기각한 또 하나의 대안 — `GameEndUseCase.IsGameOver` 폴링:**
`IGameServices`(`Application/Interfaces/IGameServices.cs`)에 `GameEndUseCase` 접근자가 **없다**(확인함).
새 인터페이스 멤버를 추가해야 하므로 범위 초과이며(규칙 6), 결정적으로 **멀티 포기(Forfeit) 경로에서는
`IsGameOver` 가 설정되지 않는다** — `ForfeitServerRpc`(`NetworkGameEndController.cs:311`)는
`GameEndUseCase` 를 거치지 않고 `OnGameEnd` 만 직접 발행한다. 폴링으로는 포기 종료를 못 잡는다.
반대로 **`OnGameEnd` 구독은 포기 경로까지 함께 덮는다.**

#### 결정 2. 재경기 대응 → **`OnNetworkSpawn`(서버 분기)과 `OnNetworkDespawn` **양쪽**에서 `false` 로 초기화**

이 작업의 **최대 위험**이다(§7-1). 이 파일에는 이미 **완전히 같은 형태의 선례가 있다**:

- `OnNetworkSpawn` 176~177행: `_attackTimer = 0f; _lastCarry = 0f;`
  (주석: *"NGO 씬 오브젝트가 다음 게임에서 재스폰될 때 이전 게임의 잔여분이 남지 않도록 리셋"*)
- `OnNetworkDespawn` 249~254행: `_unitCombatTargets.Clear(); _combatAnimationSent.Clear(); _attackTimer = 0f; _lastCarry = 0f;`

→ **`_combatStopped = false;` 를 이 두 자리에 나란히 추가한다.** 새 관례를 만들지 않고 옆줄에 붙인다.

재경기 경로 확인 결과(`NetworkGameEndController.StartRematch`, 432~481행):
동적 NetworkObject 를 명시적으로 Despawn 한 뒤 `SceneManager.LoadScene("Game", LoadSceneMode.Single)` 로
Game 씬을 재로드한다. 씬 오브젝트는 `IsSceneObject == true` 라 **일부러 건드리지 않고** NGO 의 씬 재로드에 맡긴다.
따라서 인스턴스가 새로 만들어져 필드 초기화만으로 충분할 가능성이 높으나, **`.claude/MEMORY.md` 의 확립된 교훈
("Scene NetworkObjects → Despawn/Respawn 시 리셋")과 같은 파일의 기존 선례를 그대로 따라 양쪽 모두에 명시한다.**
NGO 가 인스턴스를 재사용하든 새로 만들든 **어느 쪽이어도 안전한 형태**를 택하는 것이다.

#### 결정 3. 구독 해제 짝 → **`OnNetworkDespawn` 에서 기존 6개와 같은 형태로 해제**

```csharp
private System.IDisposable _gameEndSubscription;   // 필드 (기존 6개 옆)
...
_gameEndSubscription?.Dispose();                   // OnNetworkDespawn (기존 6개 옆)
_gameEndSubscription = null;
```

`OnNetworkDespawn` 의 기존 주석(223~225행)이 경고하는 그대로다 — *"어느 한 쪽이라도 누수되면 다음 게임에서
동일 핸들러가 중복 실행된다."*

#### 결정 4. 서버 전용인가 → **예. `OnNetworkSpawn` 의 `if (IsServer)` 블록 안(기존 6개와 같은 자리)에 둔다**

전투 틱(`Update`)은 애초에 `IsServer` 가드 뒤에서만 돈다. 클라이언트는 멈출 것이 없다.
클라이언트가 구독해도 무해하지만, **구독 위치가 갈리면 "왜 이것만 밖에 있지" 라는 판단 비용이 생기므로**
기존 6개와 같은 블록에 둔다.

#### 결정 5. `OnGameEnd` 가 클라이언트에서도 발행되는가 → **발행된다. 그러나 서버 전용 구독이므로 무관하다** (확인 완료)

`NetworkGameEndController.AnnounceWinnerClientRpc`(204~224행):

```csharp
if (!IsServer)
{
    GameEvents.OnGameEnd.OnNext(new GameEndEvent(winnerTeam));   // 219행
}
```

- **순수 클라이언트에서 `OnGameEnd` 는 실제로 재발행된다.** (`GameEndUseCase.cs:69~74` 가 클라이언트에서의
  발행을 생략하기 때문에, 클라 UI 를 위해 여기서 대신 발행한다.)
- 다만 발행 조건이 `!IsServer` 이므로 **서버에서는 이 경로로 발행되지 않고**, 우리 구독은 `IsServer` 일 때만
  걸리므로 **양쪽이 서로 만나지 않는다.** → 문제 없음.
- 서버에서 `OnGameEnd` 가 발행되는 경로는 두 가지이며 **둘 다 이번 구독으로 덮인다**:
  ① 정상 종료 — `GameEndUseCase.cs:79`, ② 멀티 포기 — `NetworkGameEndController.cs:311`.
- 서버에서 `OnGameEnd` 가 **두 번 이상 발행될 수 있는가**: `ForfeitServerRpc` 는 `_announced` 로 막히지만
  `GameEndUseCase` 와는 별개 플래그다. 다만 **플래그를 `true` 로 세우는 동작은 멱등**이라 두 번 와도 무해하다.

### 4-6. 함께 검토 — 이미 떠 있는 `DelayedAttackDamage` 코루틴

**확인 결과: 틱을 멈춰도 이미 시작된 코루틴은 살아 있고, 그 코루틴은 데미지를 적용할 수 있다.**

- `ExecuteAttack`(648~654행)이 `unit.HitFrameTimes` 원소마다 `StartCoroutine(DelayedAttackDamage(...))` 를 건다.
  이 파일에서 `StartCoroutine` 은 **653행 단 한 곳**이다(전수 확인).
- `DelayedAttackDamage`(593~609행)는 `WaitForSeconds(delay)` 후 `combat.ApplyAttackDamage(...)` 를 호출한다.
  → 여기서 유닛이 죽으면 `OnUnitDied` 가 발행되고 → **①의 경로로 `EntityDiedClientRpc` 가 나간다.**
- 즉 **틱만 멈추면 "최대 마지막 타격 프레임 시간"만큼 잔여 데미지가 더 발생할 수 있다.**

**채택: 전투 정지 핸들러에서 `StopAllCoroutines()` 를 함께 호출한다.**

- 근거: 이 컴포넌트가 시작하는 코루틴은 `DelayedAttackDamage` **하나뿐**(653행 단일 `StartCoroutine`)이므로
  `StopAllCoroutines()` 가 다른 기능까지 함께 멈춰 버릴 위험이 없다. 선별 정지를 위해 코루틴 핸들 목록을 새로 관리하는 것은
  같은 효과에 상태만 늘리는 선택이다.
- **다만 이것은 "안전벨트를 하나 더 매는" 성격이다.** 잔여 코루틴이 죽인 유닛의 사망 전파는 ①의 가드가 이미
  막아 주므로, `StopAllCoroutines()` 가 없어도 오류로 이어지지는 않는다. **①이 있으므로 ②의 이 부분은
  "정확성"이 아니라 "군더더기 제거"다.**
- ⚠️ **부작용 검토**: 게임 종료 시점에 공중에 떠 있던 마지막 히트가 적용되지 않는다 → 승패는 이미 확정이고,
  화면은 결과 팝업에 가려져 있으므로 **관측 가능한 차이가 없다.**

---

## 5. 근거 규칙 (WORKFLOW.md [4])

### 5-1. `GameSystemRules` — **직접 근거 규칙 해당 없음**

`GameSystemRules.md` 인덱스와 하위 파일 전체를 대상으로 `게임 종료` / `Shutdown` / `디스폰` / `IsSpawned` 를
검색한 결과, **"게임 종료 후 서버 전투 틱을 어떻게 처리하는가", "네트워크 정지 후 RPC 호출 금지"를 규정한
규칙은 존재하지 않는다.** 이번 두 수정에 대응하는 직접 근거 규칙은 **해당 없음**이다.

간접 접점(정합성을 깨지 않는지 확인한 대상):

| 규칙 | 내용 | 이번 변경과의 관계 |
|---|---|---|
| `GameSystemRules_Units.md` 규칙 18 (서버 데미지 타이밍 정밀화) | 데미지는 항상 서버 타이머로만 적용 | 틱을 멈춰도 **타이밍 계산식은 손대지 않는다** — 오버슈트/이월분 로직 무변경 |
| `GameSystemRules_Buildings.md` 방어 타워 시스템 규칙 9 (서버 권위 처리) | 타겟 선택·사거리·데미지는 서버에서만 | 타워 틱도 `TickCombat`(359행) 안에 있으므로 **함께 멈춘다** — 서버 권위 전제는 유지 |
| `GameSystemRules_Units.md` 규칙 22 (유닛 애니메이션 상태의 값 기반 동기화) | `UnitAnimState` 를 NetworkVariable 로 서버가 쓴다 | `SetUnitAnimState` 에 `IsSpawned` 가드를 넣어도 **정상 구간의 동작은 완전히 동일**하다 |

> **규칙 신설 제안은 §9(범위 밖)에 적었다** — 이 자리에서 규칙 문서를 고치지 않는다(규칙 6).

### 5-2. `LogRules.md` — 로그를 **1줄** 추가하는 경우에만 적용

전투 정지 시점에 개발용 로그 1줄을 남기는 것을 **권장안으로 제안**한다(사용자 승인 대상).

- 축 A = `Info` / 축 B = `개발` → `GameLog.Dev.Info` (LogRules 1.2 — 축을 둘 다 정한다)
- **1.14 금지 8(매 틱·매 프레임 로깅 금지) 위반 아님** — 게임당 **1회**, 상태 **전이** 시점에만 찍힌다.
- **1.14 금지 9(같은 사건 두 곳 로깅 금지) 위반 아님** — `NetworkGameEndController` 의 "게임 종료 감지" 는
  *결과 전파*라는 다른 사건이고, 이 줄은 *전투 틱 정지*라는 별개 사건이다. 이 사건을 기록하는 곳은 여기뿐이다.
- 형식(1.4): 카테고리 `"Network"` / `nameof(NetworkCombatController)` / 메시지 `"게임 종료 — 전투 틱 정지"`
- `key=value` 는 **붙이지 않는다** — 집계 축이 될 값이 없다(1.4 *"key=value가 곧 전송 데이터다"*).
  승리 팀은 이미 `NetworkGameEndController` 가 `WinnerTeam=` 으로 찍고 있어 중복이다.
- ⚠️ 이 로그가 필요 없다고 판단되면 **빼도 무방하다.** 그 경우 §6 의 해당 줄만 제외하면 된다.

---

## 6. 파일별 변경 계획

### 6-1. `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs` — **유일한 변경 파일**

| # | 위치 | 변경 | 근거 |
|---|---|---|---|
| 1 | 필드 (62행 부근, 기존 Disposable 6개 옆) | `_gameEndSubscription` 필드 추가 | 결정 3 |
| 2 | 필드 (104행 부근, `_lastCarry` 옆) | `_combatStopped` bool 필드 추가 (초기값 `false`) | 결정 1 |
| 3 | `OnNetworkSpawn` 176행 옆 (`IsServer` 블록 안) | `_combatStopped = false;` 초기화 추가 | 결정 2 |
| 4 | `OnNetworkSpawn` 207행 뒤 (`IsServer` 블록 안, 기존 구독 6개 아래) | `GameEvents.OnGameEnd.Subscribe(OnGameEndHandler)` 추가 | 결정 4 |
| 5 | `OnNetworkSpawn` 211행 로그 문구 | 구독 완료 로그 문구에 `GameEnd` 추가 | 기존 로그와 실제 구독 목록 불일치 방지 |
| 6 | `OnNetworkDespawn` 246행 뒤 | `_gameEndSubscription?.Dispose(); = null;` 추가 | 결정 3 |
| 7 | `OnNetworkDespawn` 254행 옆 | `_combatStopped = false;` 초기화 추가 | 결정 2 |
| 8 | **신규 핸들러** (`OnUnitFreezeChangedHandler` 740행 뒤 부근) | `OnGameEndHandler(GameEndEvent e)` — `_combatStopped = true;` + `StopAllCoroutines();` + Dev 로그 1줄 | 결정 1 · §4-6 · §5-2 |
| 9 | `Update` 310행 | `if (!IsSpawned || !IsServer || _combatStopped) return;` | 결정 1 |
| 10 | `OnUnitDied` 851행 | `if (!IsServer) return;` → `if (!IsSpawned || !IsServer) return;` | ① §3-4 |
| 11 | `OnBuildingDied` 922행 | `if (!IsServer) return;` → `if (!IsSpawned || !IsServer) return;` | ① §3-5 (2번) |
| 12 | `OnUnitEnteredCombatHandler` 781행 앞 | `if (!IsSpawned || !IsServer) return;` 추가 | ① §3-5 (4번) |
| 13 | `SetUnitAnimState` 751행 앞 | `if (!IsSpawned) return;` 추가 | ① §3-5-2 (3·5·6번을 한 곳에서 처리) |

- **주석**: 추가하는 가드마다 *왜* 인지를 초급 개발자 기준으로 적는다(CLAUDE.md 규칙 8).
  `Update`(291~309행)의 기존 설명 주석이 이미 그 형식의 본보기이므로, **같은 설명을 복붙하지 않고
  "310행과 같은 이유" 로 참조**한다(같은 설명이 6곳에 중복되면 유지보수가 갈린다).
- **변경하지 않는 것**: 전투 계산식, 오버슈트/이월분 로직, RPC 시그니처, 이벤트 채널 구조, 기존 6개 구독의 동작.

### 6-2. 코드 외 파일

- **없다.** 프리팹/씬/`.asset` 변경 없음 → **Inspector 작업 없음** → WORKFLOW [5-2] 에디터 스크립트 불필요.

---

## 7. 위험 요소

### 7-1. 🔴 **최우선 — 재경기에서 전투가 아예 시작되지 않는 사고**

`_combatStopped` 를 `true` 로 세워 놓고 **다음 경기에서 `false` 로 되돌리지 않으면**,
재경기 시 `Update` 가 진입부에서 즉시 반환한다. 그 결과:

- 유닛이 서로 붙어도 **아무도 공격하지 않는다**
- 타워가 쏘지 않는다 (`tower.Tick` 이 `TickCombat` 안에 있다 — 359행)
- 파도·HoT·자연회복·**연구 진행**·스킬 쿨다운·물안개·상태효과가 **전부 멈춘다** (359~415행 전부 `TickCombat` 안)
- **게임이 영원히 끝나지 않는다** (성이 파괴될 수 없으므로)

**이 사고는 "화면이 조금 이상한" 수준이 아니라 게임이 성립하지 않는 수준이다.**

방어:

1. §결정 2대로 **`OnNetworkSpawn`(서버 분기) + `OnNetworkDespawn` 양쪽**에서 `false` 로 초기화한다.
2. 같은 파일의 `_attackTimer` / `_lastCarry` 가 **이미 정확히 그 두 자리에서 리셋되고 있으므로**, 새 필드를
   그 줄 바로 옆에 붙여 **"이 자리는 경기마다 리셋하는 자리" 라는 것이 눈으로 보이게** 한다.
3. **검증에서 재경기 1회를 반드시 포함한다**(§8-3). 이 항목은 생략 불가다.

### 7-2. 🟠 전투 정지가 "전투"보다 넓은 범위를 멈춘다 — 의도된 것이지만 명시해 둔다

`_combatStopped` 는 `TickCombat` **전체**를 멈추므로 §7-1에 나열한 8개 서브시스템이 함께 멈춘다.
게임 종료 후에는 전부 의미가 없는 계산이므로 **의도된 동작**이지만,
"전투만 멈추는 줄 알았는데 연구도 멈췄다" 는 오해가 나오지 않도록 **코드 주석에 이 목록을 명시**한다.

- ⚠️ 단, **멀티 순수 클라이언트의 쿨다운/상태 미러는 `GameBootstrapper.Update`(클라 분기)가 별도로 감소시키므로
  이 변경의 영향을 받지 않는다**(확인함 — `GameBootstrapper.cs:578~590`). 서버만 멈춘다.

### 7-3. 🟠 `OnGameEnd` 가 서버에서 두 번 발행될 수 있다

정상 종료(`GameEndUseCase.cs:79`)와 포기(`NetworkGameEndController.cs:311`)는 서로 다른 플래그로 관리된다.
두 번 들어와도 `_combatStopped = true` 는 멱등이고 `StopAllCoroutines()` 도 멱등이므로 **무해**하다.
→ 핸들러에 별도 중복 가드를 두지 **않는다**(불필요한 상태를 늘리지 않는다).

### 7-4. 🟡 ①의 가드가 27ms 창을 **완전히** 닫는지는 실기 확인 대상

`IsSpawned` 가 `false` 로 바뀌는 시점이 NGO 내부에서 `Shutdown()` 직후인지 `OnNetworkDespawn` 호출과
동시인지 **이 환경에서는 패키지 소스를 열 수 없어 확정하지 못했다**(CLAUDE.md 규칙 10 — 추정하지 않음).
`Update` 에 같은 가드를 넣은 `cc864054` 가 실기에서 효과가 있었다는 것이 현재 가진 유일한 근거다.
→ **§8 검증에서 "오류가 사라졌는지" 를 로그로 직접 확인해야 한다.** 코드 리뷰만으로 PASS 판정하지 않는다.

### 7-5. 🟡 `StopAllCoroutines()` 의 사정거리

지금은 이 컴포넌트의 코루틴이 `DelayedAttackDamage` 하나뿐이라 안전하다(653행 단일 `StartCoroutine`).
**앞으로 이 컴포넌트에 다른 코루틴이 추가되면 그것도 함께 멈춘다.** 주석에 이 전제를 명시한다.

### 7-6. 🟢 회귀 위험이 낮은 이유

- 추가되는 가드는 **모두 "네트워크가 살아 있을 때는 통과"** 하는 조건이다 → 정상 플레이 구간의 동작은 **완전히 동일**하다.
- 새 플래그는 게임 종료 이후에만 `true` 가 된다 → 정상 플레이 구간에서는 항상 `false`.
- 계산식/RPC 시그니처/이벤트 구조 변경 없음 → 클라이언트 측 코드 변경 불필요.

---

## 8. 검증 방법 (자연어 — 실기 로그에서 무엇을 볼 것인가)

> ⚠️ WORKFLOW [5-1]·[5-3]에 따라 **Testcase.md 작성과 QA 에이전트 요청은 사용자가 명시적으로 지시한 경우에만**
> 진행한다. 아래는 **사용자 실기 테스트([6])에서 무엇을 보면 되는지**를 정리한 것이다.

### 8-1. ② 전투가 실제로 멈췄는가 — 로그로 확인

멀티(호스트 + 클라이언트)로 한 판을 끝까지 진행한 뒤, 호스트 로그 파일에서 다음을 본다.

1. `"서버: 게임 종료 감지 — 결과 전파 시작"` 줄의 **시각**을 적는다. (이번 실측: `13:33:58.860`)
2. **그 줄 이후로** 아래가 **한 줄도 없어야 한다**:
   - `"서버: 유닛 사망"` (`NetworkCombatController`)
   - `"서버 유닛 생산 완료"` 는 관계없다 — 생산은 다른 컨트롤러이며 이번 범위 밖이다
3. 새로 넣은 `"게임 종료 — 전투 틱 정지"` 줄이 **정확히 1회** 찍혀야 한다. 2회 이상이면 §7-3을 재검토한다.

**수정 전과 비교할 지점**: 수정 전 로그에서는 종료 감지(`.860`) 이후 `Shutdown`(`13:34:01.467`)까지 **2.6초**가
비어 있지 않고 전투가 계속됐다. 수정 후에는 이 구간에 전투 관련 줄이 남지 않아야 한다.

### 8-2. ① RPC 오류가 사라졌는가 — 로그로 확인

종료 후 `[로비로]` 를 눌러 나가는 경로를 **최소 3회 반복**한다(2026-08-18에 이 경로에서 2회 재현된 오류다).

- 로그 전체에서 **`"Rpc methods can only be invoked after starting the NetworkManager!"`** 문자열이
  **0건**이어야 한다.
- `NetworkManager Shutdown 완료` 와 `디스폰 — 구독 해제` 사이 구간(§3-3의 27ms)에 **어떤 오류/경고도
  없어야 한다.**
- 전투가 격렬한 상태에서 성이 파괴되도록 만들어 보면 이 구간에 사망이 겹칠 확률이 올라간다.

### 8-3. 🔴 재경기에서 전투가 정상 시작되는가 — **생략 불가**

커스텀 게임으로 한 판을 끝낸 뒤 **재경기를 수락**하고, 다음을 확인한다.

1. 새 경기에서 유닛끼리 붙었을 때 **공격 모션이 나오고 체력이 실제로 깎인다**
2. 방어 타워가 사거리 안 적을 **쏜다**
3. 연구소 연구가 **진행되어 완료된다** (진행 바가 멈춰 있으면 실패)
4. 로그에 `"서버: 유닛 사망"` 이 **다시 찍히기 시작한다**
5. 새 경기가 **정상적으로 끝난다** (성이 파괴되어 승패가 난다)

**위 5개 중 하나라도 실패하면 §7-1 사고다. 즉시 중단하고 `_combatStopped` 초기화 자리를 다시 본다.**

### 8-4. 정상 플레이 회귀 확인

게임 종료 전 구간에서 다음이 **수정 전과 동일**해야 한다.

- 유닛 공격 주기·애니메이션이 어긋나지 않는다 (가드는 통과 조건이므로 영향이 없어야 한다)
- 빙결/둔화 상태의 애니메이션 정지·재개가 정상 동작한다 (`SetUnitAnimState` 가드 추가 영향 확인)
- 힐러 힐 모션이 정상 재생된다 (같은 가드의 영향 확인)
- 클라이언트 화면에서 유닛/건물 사망이 정상적으로 사라진다

### 8-5. 싱글플레이 회귀 확인

싱글플레이에서는 `IsSpawned == false` 이므로 이 컨트롤러의 `Update` 는 원래도 즉시 반환한다.
**싱글플레이 동작은 이번 변경의 영향을 받지 않아야 한다** — 한 판을 끝까지 진행해 이상이 없음만 확인한다.

---

## 9. 범위 밖 (이번에 하지 않는 것 — 별도 승인 대상)

1. **싱글플레이의 같은 낭비** — `GameBootstrapper.Update`(530~590행)도 게임 종료 후 쿨다운/파도/HoT/자연회복/
   연구/물안개 틱을 계속 돌린다. 싱글에는 네트워크가 없어 **RPC 오류는 발생하지 않고 낭비만 있다.**
   같은 방식으로 멈출 수 있으나 **다른 파일이므로 이번 범위에 넣지 않는다**(규칙 6).
2. **`NetworkUnit.SetAnimState`(`NetworkUnit.cs:170`)의 `IsSpawned` 가드** — 더 근본적인 자리이나 다른 파일이다.
   이번에는 `NetworkCombatController.SetUnitAnimState` 쪽에서 막는다(§3-5-2).
3. **다른 네트워크 컨트롤러의 같은 구멍 전수 점검** — `NetworkProductionController` / `NetworkBuildingController` /
   `NetworkUpgradeController` 등에도 같은 형태의 핸들러가 있을 수 있다. **이번에는 확인하지 않았다**(범위 밖이라
   읽지 않았으므로 있다/없다를 말하지 않는다 — 규칙 10). 별도 작업으로 제안한다.
4. **`GameSystemRules` 규칙 신설** — "게임 종료 시 서버 권위 틱을 정지한다", "네트워크 정지 후 RPC/NetworkVariable
   쓰기 금지" 는 현재 어느 규칙 문서에도 없다(§5-1). **규칙으로 명문화하지 않으면 다음 작업에서 되돌아온다**
   (`.claude/MEMORY.md` MistShrine 교훈: *"문서에 없으면 되돌려진다"*).
   → 사용자 승인 시 `GameSystemRules_Units.md` 에 규칙 추가를 별도 제안한다.
5. **`Testcase.md` 작성 / QA 에이전트 요청** — WORKFLOW [5-1]·[5-3]에 따라 사용자의 명시적 지시가 있을 때만 진행한다.
6. **전투 계산식·타이밍 튜닝** — 오버슈트/이월분 로직(규칙 18)은 **손대지 않는다.**

---

## 10. 구현 결과 (2026-08-19 · 커밋 `55d24d83`)

> 위 §1~§9는 **구현 전 계획 시점의 원문**이며 고치지 않았다. 이 절은 그 계획이 실제로 무엇이 되었는지를 덧붙인 것이다.

### 10-1. 자연어 요약

계획대로 **가드 6곳 + 게임 종료 시 전투 틱 정지**가 들어갔고, 사용자가 **3경기를 연속으로(= 재경기 2회)** 돌려
실기 검증을 마쳤다. 계획서가 §7-1에서 **최우선 위험**으로 지목했던 *"재경기에서 전투가 아예 시작되지 않는 사고"* 는
**2회 연속으로 발생하지 않았다** — 이것이 이번 검증의 핵심이다.

### 10-2. 최종 가드 표 (실측 — 커밋 후 코드 직접 확인)

| # | 자리 | 변경 전 | 변경 후 (행) |
|:-:|---|---|---|
| 1 | `OnUnitDied` (994행) | `!IsServer` 만 | `if (!IsSpawned \|\| !IsServer) return;` (**1004행**) |
| 2 | `OnBuildingDied` (1073행) | `!IsServer` 만 | 동일 (**1079행**) |
| 3 | `OnUnitEnteredCombatHandler` (914행) | **가드 전무** | 동일 (**925행**) |
| 4~6 | Walk · HealCast · FreezeChanged | 가드 전무 | **`SetUnitAnimState`(865행) 한 곳**에 `if (!IsSpawned) return;` (**885행**) — 호출 지점 5곳을 한 번에 덮는다 |
| — | `Update` (커밋 `cc864054` 에서 선행 수정) | `!IsSpawned \|\| !IsServer` | `if (!IsSpawned \|\| !IsServer \|\| _combatStopped) return;` (**380행**) |

- **`OnUnitDied` 만 고쳤으면 성 파괴 경로(`OnBuildingDied`)로 그대로 재발했을 것이다** — 게임을 끝내는 바로 그 경로다.
  계획서 §3-5가 전수 확인을 한 이유가 여기서 드러났다.
- **`OnUnitEnteredCombatHandler` 에 붙인 `IsServer` 는 동작 변화가 없다** — 그 구독이 `OnNetworkSpawn` 의
  `if (IsServer)` 블록 안에서만 이루어져 **원래도 서버에서만 호출되던 자리**다. 형태 통일이 목적이다(§3-5-1).
- **길목 하나(`SetUnitAnimState`)를 막는 방식**을 택한 이유는 호출부마다 붙이는 것보다 **새 호출부가 생겨도
  자동으로 덮인다**는 점이다.

### 10-3. `_combatStopped` — 세우는 곳 / 검사하는 곳 / 리셋하는 곳

| 역할 | 자리 (행) |
|---|---|
| 필드 선언 | 126행 (`private bool _combatStopped = false;`) |
| **세운다** | `OnGameEndHandler`(843행) → **845행** `_combatStopped = true;` + **848행** `StopAllCoroutines()` |
| **검사한다** | `Update` 진입부 **380행** |
| **리셋한다 ①** | `OnNetworkSpawn`(IsServer 블록) **205행** |
| **리셋한다 ②** | `OnNetworkDespawn` **311행** |
| 구독 | 필드 80행 / 구독 250행 / 해제 292~293행 |

리셋을 **양쪽**에 둔 것은 계획 §결정 2 그대로다. 같은 파일의 `_attackTimer` · `_lastCarry` 가 정확히 그 두 자리에서
리셋되는 선례 옆에 붙였다.

**구독 해제 방식은 기각한 채로 유지했다** — `GameEndUseCase` 가 `OnBuildingDied` **디스패치 도중 동기적으로**
`OnGameEnd` 를 발행하므로, 그 안에서 `Dispose()` 하면 디스패치 중 구독자 목록을 바꾸게 되고 구독 순서에 따라
**게임을 끝낸 성의 `EntityDiedClientRpc` 가 영영 안 나갈 수 있다**(§결정 1).

### 10-4. 실기 검증 결과 — **전부 통과**

근거 로그: `_Logs/_editor/2026-08-19/RuntimeLog.txt` **1408줄** (세 번째 세션 = 706행 `=== 세션 시작: 2026-08-19 23:26:46 ===` 이후).
**사용자가 3경기를 연속으로 진행했다 = 재경기를 2회 했다.**

| # | 항목 | 결과 · 근거 |
|:-:|---|---|
| **①** | 가드 구멍 | ✅ **해결.** 3경기 내내 `[ERROR]` **0건** · `"Rpc methods can only be invoked after starting the NetworkManager!"` **0건**. 게임 종료 → 로비 → 재경기를 **두 번 반복**했다 |
| **②** | 전투 틱 정지 | ✅ **동작.** `게임 종료 — 전투 틱 정지` 가 **872 · 1090 · 1394행**에 **경기당 정확히 1회**. 872행(23:29:15.657) 직후 Shutdown(23:29:45.826)까지 `NetworkCombatController` 의 전투 로그가 **한 줄도 없다** |
| **재경기** | 🔴 **플래그 리셋** | ✅ **2회 연속 통과.** 1경기 정지 후 2경기 구간(873~1090행)에 `NetworkCombatController` 로그 **40건**(그중 `서버: 유닛 사망` **34건**), 2경기 정지 후 3경기 구간(1091~1394행)에 **68건**(`서버: 유닛 사망` **63건**) — **전투가 정상 재개됐다** |

**재경기 검증이 이 작업의 핵심이다.** 플래그 리셋이 잘못됐다면 §7-1 그대로 2경기에서 유닛이 아예 싸우지 못하고
게임이 끝나지 않았을 것이다. 그 시나리오가 **두 번 연속 통과**했다.

### 10-5. 계획서가 「확인 필요」로 남겼던 항목의 현재 상태

| § | 항목 | 현재 상태 |
|:-:|---|---|
| **7-4** | `IsSpawned` 가 `Shutdown()` 직후 전이하는지 | **여전히 미확정.** 이 환경에서 NGO 패키지 소스를 열 수 없다는 사정은 그대로다. 추가된 것은 **실기 관측 하나뿐** — 3경기를 돌리는 동안 해당 오류가 나지 않았다. **⚠️ 이것으로 "27ms 창을 완전히 닫는다" 고 단정하지 않는다**(CLAUDE.md 규칙 10) |
| **3-5-2** | 디스폰 후 NetworkVariable 쓰기가 RPC 와 같은 오류를 내는지 | **여전히 미확정.** Walk · HealCast · FreezeChanged 3건은 계속 **「오류가 확인된 수정」이 아니라 「예방」** 으로 표기한다 |
| **9-2** | `NetworkUnit.SetAnimState` 의 `IsSpawned` 가드 | **손대지 않았다.** 여전히 `IsServer` 만 보고 `IsSpawned` 를 안 본다. 더 근본적인 자리지만 다른 파일이라 `NetworkCombatController.SetUnitAnimState` 쪽에서 막았다 |
| **9-3** | 다른 네트워크 컨트롤러의 동일 구멍 | **점검하지 않았다.** `NetworkProductionController` · `NetworkBuildingController` · `NetworkUpgradeController` 등을 열지 않았으므로 **있다/없다를 말할 수 없다**(규칙 10) |
| — | **B(전역 훅의 엔진 오류 수집 · 커밋 `cc864054`)** | **이번에도 미검증.** 3경기를 돌렸는데 엔진 오류가 한 건도 나지 않아 잡을 것이 없었다. **역설적이지만 ①·② 수정이 잘 돼서 B 를 검증할 기회가 사라진 것이다** — B 로 잡으려던 대상이 바로 그 RPC 에러였다 |

### 10-6. 변경 파일 리스트업 (WORKFLOW [12])

```
[수정 — 코드]
- Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs

[수정 — 에이전트 메모리]
- .claude/agent-memory/game-programmer/MEMORY.md

[추가 — 에이전트 메모리]
- .claude/agent-memory/game-programmer/network-infra.md
```

- **코드 변경은 `NetworkCombatController.cs` 단 1개 파일**이다(계획 §6-1 그대로).
- 프리팹 · 씬 · `.asset` 변경 **없음** → Inspector 작업 없음(계획 §6-2 그대로).
- 문서 갱신분(이 문서 · `LogRules.md` · `PROJECT_STATUS.md` · `ROADMAP.md` · `WORK_HISTORY.md`)은 [11] 작업이라 위 목록과 별도다.
