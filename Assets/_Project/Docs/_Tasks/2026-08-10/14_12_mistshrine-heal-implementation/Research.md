# Research — MistShrine 물안개 힐 시스템 구현

**작성일:** 2026-08-10
**작업 폴더:** `Assets/_Project/Docs/_Tasks/2026-08-10/14_12_mistshrine-heal-implementation/`
**작업 성격:** **구현 사이클** (코드 작성 대상 조사)

---

## 이 조사가 무엇이고 왜 하는가 (자연어 설명 — 기술 용어 없이)

초월 종족에는 **MistShrine(물안개 신전)** 이라는 건물이 있습니다.
"회복시켜 주는 건물"로 이름과 자리만 잡혀 있을 뿐, **지금은 아무 회복도 하지 않습니다.**
지을 수 있고 적에게 맞으면 부서지기만 하는, 사실상 장식에 가까운 상태입니다.

오전(09:34) 작업에서 이 건물이 앞으로 **어떻게 동작해야 하는지**를 정해 문서에 적어 두었습니다.
사용하면 건물 주위에 물안개가 깔리고, 물안개가 유지되는 동안 그 안에 있는 내 편 부대와 내 편 건물이
1초마다 체력을 회복하며, 물안개가 걷히면 회복이 멈추고 잠시 뒤 다시 쓸 수 있게 되는 방식입니다.
다만 **그 작업은 문서만 고쳤고 코드는 한 줄도 건드리지 않았습니다.**

이번 작업은 그 정해진 동작을 **실제로 게임 안에서 작동하게 만드는 작업**입니다.
그리고 이 문서는 그 작업을 시작하기 전에, **지금 게임 코드가 어떤 상태인지**를 직접 읽어서 확인한 결과입니다.

이번 조사에서 확인하려 한 것은 크게 세 가지입니다.

1. **이미 만들어져 있어서 그대로 가져다 쓸 수 있는 것은 무엇인가** —
   예를 들어 건물을 눌렀을 때 뜨는 창의 공통 틀, 남은 대기시간을 시계처럼 보여주는 표시,
   회복된 체력이 숫자로 떠오르는 표시 같은 것들입니다.
2. **아예 없어서 새로 만들어야 하는 것은 무엇인가** —
   조사 결과 **"건물의 체력을 회복시키는 통로 자체가 게임에 존재하지 않는다"** 는 점이 가장 큰 문제였습니다.
   지금까지 회복은 오직 부대(유닛)만 받을 수 있었고, 건물은 맞아서 깎이기만 할 뿐 회복은 한 번도 필요한 적이 없었습니다.
3. **문서에 적힌 대로 만들면 위험한 부분은 없는가** —
   조사 중 **문서의 표현과 실제 코드가 어긋나는 지점 두 곳**을 발견했습니다(§7).
   그대로 따라 만들면 "회복시켜야 할 대상이 오히려 피해를 입는" 결과가 나올 수 있는 부분이라,
   구현 방식을 조정해야 한다는 점을 미리 정리해 두었습니다.

**이 문서 자체는 조사 기록이며, 여기서도 코드는 전혀 수정하지 않았습니다.**
실제 코드 작성은 이 문서와 뒤이은 Plan 문서를 근거로 **game-programmer 에이전트**가 담당합니다.

---

## 0. 조사 범위와 전제

| 항목 | 내용 |
|------|------|
| 근거 규칙 | `GameSystemRules/GameSystemRules_Buildings.md` — **MistShrine 물안개 힐 시스템 규칙 1~27**<br>`GameSystemRules/GameSystemRules_UI.md` — **MistShrine 패널 UI 규칙 1~15** |
| 선행 조사 | `_Tasks/2026-08-10/09_34_mistshrine-heal-redesign/Research.md` (문서 정합 조사) |
| 이번 조사의 차이 | 선행 조사는 "재사용 가능/불가"의 **판정**까지였다. 이번은 **구현에 필요한 시그니처·호출 위치·상태 키·틱 진입점**까지 코드를 직접 읽어 특정했다. |
| 현재 구현 상태 | **구현 미착수.** `HealShrine`을 대상으로 동작하는 코드는 프로젝트 전체에 **0건**이다(§1-1 근거). |

> **확인 방법 원칙(CLAUDE.md 규칙 10):** 아래 모든 항목은 해당 파일을 실제로 읽어 기록했다.
> 읽지 못했거나 확정하지 못한 것은 **"미확인"** 으로 명시했다.

---

## 1. 현재 구현 상태 (실측)

### 1-1. `HealShrine` 참조 전수 조사

`Assets/_Project/Scripts` 전체에서 `HealShrine` 문자열을 검색한 결과 **5건**이며, **전부 주석 또는 enum 선언**이다.

| 파일 | 위치 | 성격 |
|------|------|------|
| `Domain/Building/BuildingType.cs` | 8행(주석), 38행 `HealShrine = 6,       // 회복 건물` | **enum 값 선언만** |
| `Presentation/UI/BuildingActionPanelUI.cs` | 4행 주석 | 공용 액션 패널이 다루는 비생산 건물 예시로 언급 |
| `Bootstrap/GameBootstrapper.Setup.cs` | 215행·587행 주석 | 액션 패널/스탯 초기화 대상 열거 |

→ **회복 로직·전용 패널·물안개 상태·쿨다운·자동 모드 그 어느 것도 존재하지 않는다.** 과대 표기 금지.

### 1-2. config 에셋 실측

`Assets/_Project/Resources/Config/BuildingStatsConfig.asset` — `buildingType: 6`(HealShrine) 항목은 **존재하며 값이 들어 있다**.

```
- buildingType: 6
  humanMaxHp: 0 / spiritMaxHp: 0 / transcendenceMaxHp: 500
  humanGoldCost: 0 / spiritGoldCost: 0 / transcendenceGoldCost: 100
  humanAttackPower/Cooldown/Range … 전부 0 (초월 포함)
  upgradeCost: 0
```

→ **HP 500 / 건설비 100은 이미 config에 반영되어 있다**(StatsReference.md와 일치).
→ **회복량·물안개 지속시간·쿨다운·범위 반경 필드는 `BuildingStatsConfig`에 존재하지 않는다**(§11).

---

## 2. `BuildingData`(Domain) — 회복 경로 부재 (필수 조사 1)

**파일:** `Assets/_Project/Scripts/Domain/Building/BuildingData.cs` (전 116행, 전체 확인)

| 항목 | 실측 |
|------|------|
| `Hp` | `public int Hp { get; private set; }` — **외부에서 직접 대입 불가** |
| `MaxHp` | `public int MaxHp { get; }` — 생성자에서만 설정 |
| 감소 경로 | `public void TakeDamage(int damage)` — `if (!IsAlive) return; Hp -= damage; if (Hp < 0) Hp = 0;` |
| **증가 경로** | **없음.** 회복 메서드가 전혀 없다 |
| `IDamageable` 구현 | `TakeDamage`만 인터페이스 계약 |

**대조 — `UnitData.Heal`** (`Domain/Unit/UnitData.cs` 200~206행, 실측):

```csharp
public void Heal(int amount)
{
    if (!IsAlive) return;
    if (amount <= 0) return;
    Hp += amount;
    if (Hp > MaxHp) Hp = MaxHp;
}
```

→ **MaxHp 클램프는 도메인 메서드 내부에 둔다**는 것이 프로젝트의 확립된 위치다(규칙 5 "최대 체력 대상은 회복되지 않는다"가 자연히 성립).
→ `BuildingData`에 **동일 형태의 `Heal(int amount)`** 을 추가하는 것이 유일한 정합 해법이다(Buildings 규칙 24).

> **부수 효과 주의:** `BuildingData.Heal`이 생기면 `NetworkHealthSync.SyncBuildingHealth`의
> "`BuildingData.Hp`도 `TakeDamage`를 통해서만 변경 가능"이라는 주석(311행)이 사실과 달라진다 → 주석 갱신 필요.

---

## 3. 기존 힐 서브시스템 — 1초 discrete 틱의 실제 진입점 (필수 조사 2)

**파일:** `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`

### 3-1. 힐 수렴점

`private void ApplyHealToUnit(UnitData healer, UnitData target, int amount, bool showText = true)` (1378행)

```csharp
target.Heal(amount);
GameEvents.OnEntityHealed.OnNext(
    new EntityHealedEvent(target, target.Hp, isUnit: true,
        healerId: healerId, healerIsUnit: true, showText: showText));
```

- **`isUnit: true`가 하드코딩**되어 있다 → 건물 회복은 이 메서드를 재사용할 수 없고 **별도 발행 지점**이 필요하다.
- `showText:false`여도 이벤트는 발행한다(주석 1386~1387행) — **HP 동기화는 유지, 텍스트만 억제**하는 분리 메커니즘이 이미 존재한다(규칙 15 구현에 그대로 활용 가능).

### 3-2. `ActiveTimedEffect` 틱 분기 — ⚠️ 규칙 8을 문자 그대로 재사용할 수 없다

`TickTimedEffects(float dt)` (1922행) 구조:

```csharp
// 대상 조회 — 유닛 딕셔너리에서만 찾는다
if (!_unitSpawn.Units.TryGetValue(effect.TargetId, out UnitData target) ...) { remove; continue; }

if (effect.TickInterval > 0.0001f)      // discrete 틱 모드
{
    if (TickDiscreteDamageEffect(effect, target, dt)) _activeTimedEffects.RemoveAt(i);
    continue;                            // ← Kind 검사 없음
}
// 연속(프레임 diff) 모드 — 여기서만 Kind == Heal 분기가 있다
```

`TickDiscreteDamageEffect` → `ApplyOneDamageTick` → `ApplyTimedDamageToUnit(attacker, target, tickAmount)` (2023~2069행).

**실측 결론 3가지:**

| # | 사실 | 근거 |
|:-:|------|------|
| A | **discrete 틱 경로는 Damage 전용이다.** `effect.Kind`를 전혀 보지 않고 무조건 피해를 적용한다 | 1944~1949행 + 2058~2069행 |
| B | **대상은 유닛 전용이다.** `_unitSpawn.Units.TryGetValue(effect.TargetId, ...)` — 건물은 대상이 될 수 없고, `TargetId` 단일 키라 **유닛 Id와 건물 Id가 충돌**한다 | 1932행, `ActiveTimedEffect.TargetId` 주석(2142행 "효과가 붙은 **대상 유닛** Id") |
| C | **아우라(규칙 9)와 모델이 다르다.** `ActiveTimedEffect`는 "대상에 붙는 레코드 + 총량 분할" 모델이라 **범위 이탈 즉시 끊김**을 표현할 수 없다 | `TotalAmount`/`Elapsed`/`AppliedAmount` 필드 구조(2140~2177행) |

→ **규칙 8의 "규칙 40과 동일한 틱 방식을 재사용"은 "같은 1초 격자 틱 *패턴*을 따른다"로 읽어야 하며, `_activeTimedEffects` 코드 경로 재사용을 뜻할 수 없다.** (§7-1 불일치 항목)

### 3-3. 재사용할 정확한 진입점 — 자연회복 패턴이 정답

`TickNaturalRegen(float dt)` (339행) → `ApplyTeamRegen(TeamId, ref float accum, float dt)` (347행):

```csharp
accum += perSecond * dt;
int wholeHp = (int)accum;
if (wholeHp <= 0) return;
accum -= wholeHp;
// 선수집(_regenBuffer) 후 일괄 적용 — 순회 중 변경 회피
ApplyHealToUnit(null, _regenBuffer[i], wholeHp, showText: false);
```

- `_activeTimedEffects`를 **전혀 쓰지 않는 독립 누적기**다 → **자연회복이 이미 "독립 채널"의 실물 선례**다(Upgrade 규칙 7이 요구한 분리를 이 방식으로 달성).
- 매 틱 **대상을 다시 수집**한다 → **아우라 의미론과 정확히 일치**한다.
- `showText:false`로 스팸을 억제한다 → 규칙 15의 텍스트 주기 분리와 동일 기법.

→ **물안개 힐은 `TickNaturalRegen`/`TickWaves`와 같은 "독립 상태 목록 + 매 틱 범위 재수집" 모델로 구현해야 한다.**

---

## 4. 힐 채널 분리 현황 (필수 조사 3)

| 채널 | 자료구조 | 키 | 상호 간섭 |
|------|---------|-----|----------|
| BloomFairy HoT / 스킬 HoT | `List<ActiveTimedEffect> _activeTimedEffects` | `(TargetId, Kind)` — `AddOrRefreshTimedEffect`가 같은 키면 **덮어씀**(1869~1910행) | 같은 `Heal` 버킷끼리 **덮어씀** |
| 초월 자연회복 | `float _regenAccumBlue` / `_regenAccumRed` + `List<UnitData> _regenBuffer` (114~117행) | 팀 단위 누적 | **독립** — 위 목록과 무관 |
| DoT(Blast/Inferno/스킬 장판) | 위와 동일 목록, `Kind=Damage` | `(TargetId, Damage)` | Heal과는 키가 달라 무간섭 |

`TimedEffectKind`는 현재 **`Heal` / `Damage` 2개뿐**(1408~1414행).

→ **물안개 힐을 `TimedEffectKind.Heal`에 넣으면 BloomFairy 힐과 서로 덮어쓴다**(Buildings 규칙 14 ⚠️ 경고와 일치).
→ **자연회복과 동일하게 `_activeTimedEffects` 바깥의 독립 자료구조**로 두면 채널 분리가 **구조적으로 보장**된다(신규 `TimedEffectKind` 추가조차 불필요).

---

## 5. 원형 반경 수집 헬퍼 (필수 조사 4)

| 헬퍼 | 파일·접근성 | 시그니처 | 팀 필터 |
|------|------------|---------|--------|
| `CollectEnemyUnitsInRadius` | `Application/Combat/BlastAttackBehavior.cs` · `internal static` | (뷰 좌표 기반, 공격자 `UnitData` 전제) | `적만` |
| `CollectEnemyBuildingsInRadius` | `Application/Combat/QuakeAttackBehavior.cs` · `internal static` | 동상 | `적만` |
| `CollectEnemyUnitsInRadiusDomain` | `UnitCombatUseCase.cs` 1824행 · **`private`** | `(TeamId casterTeam, Vector3 centerWorld, float radiusSqr, List<UnitData> result)` | `unit.Team == casterTeam` → **continue(아군 제외)** |
| `CollectEnemyBuildingsInRadiusDomain` | `UnitCombatUseCase.cs` 1842행 · **`private`** | `(TeamId casterTeam, Vector3 centerWorld, float radiusSqr, List<BuildingData> result)` | `building.Team == casterTeam` → **continue** |

**Domain 버전 본문(1824~1853행) 실측 — 아군 버전에서 그대로 쓸 수 있는 부분:**

```csharp
foreach (var unit in _unitSpawn.Units.Values) { ... }              // 컬렉션 순회
foreach (var building in _buildingPlacement.Buildings.Values) { ... }
if (unit == null || !unit.IsAlive) continue;                        // 생존 가드
Vector3 rel = Flatten(_mapper.HexToWorld(unit.Position)) - centerWorld;   // 도메인 월드 변환
if (rel.sqrMagnitude <= radiusSqr) result.Add(unit);                // sqr 비교(제곱근 회피)
```

| 재사용 가능 | 새로 써야 하는 부분 |
|------------|-------------------|
| 순회 대상(`_unitSpawn.Units.Values` / `_buildingPlacement.Buildings.Values`), 생존 가드, `Flatten(_mapper.HexToWorld(pos))` 도메인 월드 변환, `sqrMagnitude <= radiusSqr` 비교 | **팀 조건 반전**(`!= casterTeam → continue` 로 뒤집기), **시전 건물 자신·Castle 미제외**(규칙 4 — 기존 헬퍼에는 제외 로직이 없어 자연히 포함되나, 명시적 주석 필요), **풀피 대상 사전 제외**(규칙 5·15 — `Hp >= MaxHp` 스킵으로 이벤트 스팸 억제, 자연회복 368행 선례) |

**접근성 제약:** Domain 버전 2종은 **`private`** 이므로 다른 클래스에서 호출할 수 없다.
→ 아군 수집 헬퍼는 ① `UnitCombatUseCase` 내부에 추가하거나 ② **신규 전용 UseCase 내부에 자체 구현**해야 한다.
`IHexCoordinateMapper`·`UnitSpawnUseCase`·`BuildingPlacementUseCase`는 이미 생성자 주입 가능한 공개 타입이므로 ②가 응집도 면에서 유리하다(`TowerCombatUseCase`가 같은 3종을 직접 주입받는 선례 — `TowerCombatUseCase.cs` 53~56행).

---

## 6. `SkillActivationUseCase` 쿨다운 패턴과 틱 위치 (필수 조사 5)

**파일:** `Application/UseCases/SkillActivationUseCase.cs` (전 268행, 전체 확인)

### 6-1. 상태·API

| 멤버 | 실측 |
|------|------|
| `Dictionary<int, float> _cooldownRemaining` (57행) | 건물 Id → 남은 시간 |
| `Dictionary<int, float> _cooldownTotal` (59행) | 건물 Id → 발동 시점 총 쿨다운(오버레이 radial fill 비율용) |
| `public void StartCooldownLocal(int buildingId, float cooldown)` (178행) | `cooldown <= 0`이면 두 딕셔너리에서 **제거**, 아니면 둘 다 세팅 |
| `public void TickCooldowns(float dt)` (195행) | 만료 키를 `_expiredBuffer`에 모아 **순회 후 제거**(컬렉션 변경 예외 회피), `_tickKeyBuffer`/`_expiredBuffer` **재사용 버퍼로 GC 절감** |
| `GetCooldownRemaining` / `GetCooldownTotal` / `IsOnCooldown` / `ClearAll` (230~252행) | 조회 + 전체 초기화 |

`StartCooldownLocal`의 이름이 "Local"인 이유(파일 상단 22~24행 주석): **서버 발동 시에도 호출되고, 멀티 클라이언트는 브로드캐스트를 받아 표시용 미러로도 호출**한다 → 같은 메서드가 두 역할을 겸한다.

### 6-2. 틱 진입점 — 이중 틱 방지 근거 (코드 실측)

| 실행 주체 | 위치 | 가드 |
|----------|------|------|
| 싱글 / **멀티 순수 클라(미러)** | `Bootstrap/GameBootstrapper.cs` **506~510행** | `if (_skillActivation != null && (!IsNetworkMode() \|\| !NetworkContext.IsNetworkServer))` |
| 멀티 서버(호스트) | `Infrastructure/Network/NetworkCombatController.cs` **331행** (`TickCombat` 내부) | `Update()` 진입부 **248행 `if (!IsServer) return;`** |

전투/HoT 계열의 대응 위치도 동일 패턴이다:

| 대상 | 싱글 | 멀티 서버 |
|------|------|----------|
| `_unitCombat.TickTimedEffects` | `GameBootstrapper.cs` 489행 (`!IsNetworkMode()` 블록 내) | `NetworkCombatController.cs` 312행 |
| `_unitCombat.TickNaturalRegen` | 491행 | 318행 |
| `_towerCombat.Tick` | 530~533행 | 297행 (+ `TowerCombatUseCase.Tick` 107행 내부 가드 `if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;`) |

> **이중 틱 방지 근거 확보:** 두 가지 가드 형태가 공존한다.
> ① **호출부 가드**(`!IsNetworkMode()`) — HoT·자연회복이 사용.
> ② **UseCase 내부 가드**(`NetworkContext` 검사) — `TowerCombatUseCase`가 추가로 사용(호스트 이중 호출 방어).
> MistShrine 시전/회복 틱은 **①+② 이중 방어**를, 쿨다운 미러 틱은 **스킬과 동일한 ① 형태**(클라도 돌아야 하므로)를 써야 한다.
> ⚠️ **`NetworkCombatController.Update`는 `_attackInterval`(50ms) 격자로 발화**하고 `realElapsed`를 넘긴다(245~265행) → 멀티 서버에서 물안개 틱 해상도는 **최대 50ms 지연**을 갖는다. 1초 틱이므로 실무상 무해하나 인지 필요.

---

## 7. 자동 모드 네트워크 3단 구조 (필수 조사 6)

**파일:** `Infrastructure/Network/NetworkProductionController.cs`

| 단계 | 실측 |
|:-:|------|
| ① 래퍼 | `public void RequestToggleAuto(int barracksId, UnitType unitType, TeamId team)` (141행) → `ToggleAutoServerRpc(barracksId, (int)unitType, (int)team);` — **enum을 int로 캐스팅해 전송** |
| ② ServerRpc | `[ServerRpc(RequireOwnership = false)] public void ToggleAutoServerRpc(int barracksId, int unitTypeInt, int teamIndex, ServerRpcParams rpcParams = default)` (805행) |
| ②-1 팀 검증 | `TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red; if ((TeamId)teamIndex != expectedTeam) { LogWarning; return; }` (814~819행) |
| ②-2 서비스 조회 | `UnitProductionUseCase production = _services?.GetUnitProduction();` — **`_services`는 `OnNetworkSpawn`에서 1회 캐시, 재조회 없음**(821행 주석) |
| ③ ClientRpc | 상태 변경 → `GameEvents.OnProductionQueueChanged` → 서버 구독 → `SyncQueueStateClientRpc(...)` (610행) 전체 브로드캐스트 |

**그대로 본뜰 수 있는가:** ①②는 그대로 본뜰 수 있다. ③은 **본뜨면 안 된다.**

- 생산은 큐 상태가 커서 "이벤트 → 전체 상태 브로드캐스트" 구조를 쓴다.
- MistShrine 자동 모드는 **bool 하나**(규칙 19)이므로, **`NetworkSkillController.SkillActivatedClientRpc`(177행 부근)의 간결한 직접 브로드캐스트 형태**가 더 적합하다.

**주의점 2가지:**

1. **`_services` 캐시 방식이 파일마다 다르다.** `NetworkProductionController`는 `OnNetworkSpawn` 1회 캐시(재조회 없음), **`NetworkSkillController`/`NetworkUpgradeController`는 `ResolveServices()` 지연 재조회**(`NetworkSkillController.cs` 99~104행)를 쓴다. 신규 컨트롤러는 **후자(`ResolveServices()`)** 를 따라야 한다(MEMORY 교훈 — 연구소 스폰 레이스).
2. **팀 검증 규칙 `senderClientId == 0 → Blue`는 프로젝트 전역 관례**다(`NetworkProductionController` 354·814행, `NetworkSkillController` 152행에서 동일 확인).

---

## 8. `NetworkHealthSync` — 건물 힐 경로 부재 (필수 조사 7)

**파일:** `Infrastructure/Network/NetworkHealthSync.cs`

| 지점 | 실측 코드 | 결론 |
|------|----------|------|
| `OnEntityHealed(EntityHealedEvent e)` **146행** | `if (!e.IsUnit \|\| !(e.Entity is UnitData unit)) return;` | **건물 힐 이벤트를 명시적으로 버린다** |
| 주석 **140행** | `유닛 힐만 다룬다(현재 파도/힐러 대상은 유닛). 건물 힐은 아직 사용처가 없어 무시.` | 의도적 미구현임이 문서화되어 있다 |
| `SyncHealClientRpc(int unitId, int serverHp, int healerId, bool healerIsUnit, bool showText)` **205행** | 파라미터에 `isUnit`이 **없다** | 유닛 전용 RPC |
| `SyncUnitHeal(...)` **271행** | `unitSpawn.GetUnit(unitId)` → `int diff = serverHp - unit.Hp; if (diff > 0) unit.Heal(diff);` + `showText`면 `OnEntityHealed` 재발행 | **건물 대응 함수(`SyncBuildingHeal`)가 없다** |

대조 — **피해** 경로는 이미 건물을 지원한다:
`SyncHealthClientRpc(int entityId, bool isUnit, ...)` (172행) → `isUnit ? SyncUnitHealth(...) : SyncBuildingHealth(...)` (185~192행).

→ **멀티에서 건물 HP 회복을 동기화하려면 힐 경로에 건물 분기를 신설해야 한다.**
설계 선택지는 두 가지이며 Plan에서 확정한다:
- **(a) 기존 RPC 확장** — `SyncHealClientRpc`에 `bool isUnit` 파라미터 추가 + `SyncBuildingHeal` 신설. 피해 경로(`SyncHealthClientRpc`)와 형태가 대칭이 되어 일관적. 단 **기존 시그니처 변경**(호출부 1곳: 152행).
- **(b) 건물 전용 RPC 신설** — `SyncBuildingHealClientRpc` 추가. 기존 코드 무변경(회귀 위험 최소). 단 RPC가 하나 늘어난다.

---

## 9. UI 계층 실측 (필수 조사 8)

### 9-1. `BuildingPanelBase` (전 393행, 전체 확인)

| 제공 기능 | 실측 위치 |
|----------|----------|
| `SerializeField`: `_popup`(AnimatedPanel) / `_headerText` / `_cancelButton` / `_demolishButton` / `_demolishRefundText` / `_colorConfig` | 63~84행 (모두 `protected` → **자식 Inspector에도 노출**) |
| 의존성: `_buildingPlacement` / `_resource` / `_networkBuildingController` / `_currentBuilding` | 91~100행 |
| `protected void InitializeBase(BuildingPlacementUseCase, ResourceUseCase, NetworkBuildingController)` | 150행 |
| 훅: `OnShow(BuildingData)` / `OnBeforeClose()` / `BeforeDemolish()` | 224 / 258 / 344행 |
| 공개 상태: `IsOpen`, `ClosedFrame`, `CurrentBuildingId` | 127 / 134 / 137행 |
| **건물 파괴 시 자동 닫힘** | 176~182행에서 `GameEvents.OnBuildingDied.Subscribe(OnBuildingDied).AddTo(this)` → 380행 `e.Building.Id == _currentBuilding.Id`면 `Close()` |
| 철거 처리 | 297행 `OnDemolishButtonClick` — 멀티는 `_networkBuildingController.RequestDemolish`, 싱글은 `BeforeDemolish()` → 환불 → `DemolishBuilding` |
| 배경 탭 닫기 | `UIManager.Instance?.ShowBlockingOverlay(Close)` (207행) / `HideBlockingOverlay()` (245행) |

> **`.AddTo(this)` 근거 주석(112~115행, 원문):**
> *"여기서 직접 OnDestroy를 정의하지 않는 이유는, 자식 중 ResearchPanelUI가 이미 자체 OnDestroy를 갖고 있어 베이스의 OnDestroy를 숨겨 버리기 때문이다."*
> → MistShrine 패널이 자체 `OnDestroy`를 선언하면 같은 문제가 재발한다. **구독 해제는 반드시 `.AddTo(this)`.**

### 9-2. 3×3 그리드 슬롯 관리

| 패널 | 필드 | 숨김 방식 |
|------|------|----------|
| `BuildingActionPanelUI` | `[SerializeField] List<Button> _allSlotButtons` (43행) / `List<Button> _activeSlotButtons` (47행) / `private List<CanvasGroup> _slotCanvasGroups` (64행, Inspector 미노출) | `BuildSlotCanvasGroups()` (101행)가 **CanvasGroup 없으면 자동 부착**, 초기 `alpha=0`. `SetActive(false)` 금지 이유가 53~61행 주석에 명시(GridLayout 정렬 붕괴) |
| `BuildingSkillPanelUI` | `List<Button> _skillSlotButtons` / `List<Image> _skillSlotIcons` / `List<SkillCooldownOverlay> _skillSlotOverlays` / `List<TMP_Text> _skillSlotLabels` (49~64행) + `private List<CanvasGroup> _slotCanvasGroups` (80행) | `BuildSlotCanvasGroups()` (123행) 동일 패턴 |

→ **UI 규칙 10·11(3×3 9슬롯 + `CanvasGroup.alpha=0` 숨김)은 두 선례가 이미 동일 방식으로 구현**하고 있어 그대로 따르면 된다.

### 9-3. 롱프레스 구현 — **EventTrigger 방식** (규칙 5 근거)

`ProductionPanelUI.cs` 실측:

| 요소 | 위치 |
|------|------|
| `private const float LongPressThreshold = 0.5f;` | 256행 |
| `private bool _isPointerDown; / _longPressTriggered;` | 255·257행 |
| 배선 | **351행** `var trigger = button.gameObject.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();` → `PointerDown` 엔트리(353행) / `PointerUp` 엔트리(359행) **코드에서 동적 부착** |
| 탭 판정 | 375~379행 `OnUnitPointerUp` → `if (!_longPressTriggered) OnUnitTap(_activeUnitType);` |
| 롱프레스 판정 | 384~387행 `Update`에서 `Time.unscaledTime - _pointerDownTime >= LongPressThreshold` → `_longPressTriggered = true; OnUnitLongPress(...)` |
| 자동 중 탭 = 해제 | 440~443행 `if (state != null && state.IsAutoMode && state.AutoTypes.Contains(type)) { HandleToggleAuto(type); return; }` |
| 멀티/싱글 분기 | 528~529행 `if (_networkProductionController != null && NetworkContext.IsNetworkActive) RequestToggleAuto(...) else 로컬 토글` |

→ **EventTrigger는 프리팹에 미리 붙일 필요가 없다**(코드가 자동 부착). Inspector 작업 부담이 그만큼 줄어든다.

### 9-4. `SkillCooldownOverlay` (전 105행, 전체 확인)

| 항목 | 실측 |
|------|------|
| 시그니처 | `public void SetCooldown(float remaining, float total)` / `public void Hide()` |
| 동작 | `remaining <= 0f \|\| total <= 0f` → 숨김. 아니면 `_fillImage.fillAmount = Mathf.Clamp01(remaining / total)`, `_remainingText.text = Mathf.CeilToInt(remaining).ToString()` |
| `SerializeField` | `_fillImage`(Image, Filled/Radial360/Clockwise) / `_remainingText`(TMP) / `_canvasGroup` |
| 부가 동작 | 보일 때 `blocksRaycasts = true` → **쿨다운 중 버튼 입력 자동 차단** |
| 스킬 전용 로직 | **없음**(순수 표시 컴포넌트) |

**갱신 주체 선례:** `BuildingSkillPanelUI.Update()` (269~289행)가 **패널이 열린 동안 매 프레임** `GetCooldownRemaining`/`GetCooldownTotal`을 읽어 `SetCooldown`을 호출한다. MistShrine 패널도 동일 구조를 쓴다.

### 9-5. 범위 표시 — 재사용 후보 실물 존재

`Presentation/Effects/SkillAimReticle.cs` 실측:
- `SerializeField`: `_ringRenderer` / `_fillRenderer` / `_dotRenderer`(SpriteRenderer 3겹), `_overlayMaterial`(Material), `_fillColor`, `_edgeColor`, `_ringThickness`, `_yOffset`, `_baseDiameter`, `_visualMultiplier`, `_sizeScale`
- API: `public void Show(Vector3 worldPos, float radius)` / `public void Hide()`

→ **UI 규칙 12가 요구하는 "반투명 채움 + 외곽선" 구성이 이미 구현되어 있다**(fill + ring 2겹). 재사용 또는 이 구조를 본뜬 전용 컴포넌트 신설 모두 가능하다(Plan에서 확정).

셰이더: `Assets/_Project/Shaders/SkillAimOverlay.shader` — 셰이더명 `"Hexiege/SkillAimOverlay"`, 머티리얼 경로 `Assets/_Project/Materials/SkillAimOverlay.mat`(`SkillSetup_Scene.cs` 481~482행 상수).

### 9-6. 회복 텍스트 경로 — 신규 UI 불필요 확인

`Presentation/UI/FloatingHpTextSpawner.cs` `ShowHeal(EntityHealedEvent evt)` (205행) 실측:

```csharp
if (!evt.ShowText) return;                                   // 213행 — 텍스트 억제 게이트
Vector3 worldPos = evt.IsUnit
    ? _positionProvider.GetUnitWorldPosition(evt.Entity.Id)
    : _positionProvider.GetBuildingWorldPosition(evt.Entity.Id);   // 216~218행 — 건물 분기 이미 존재
if (worldPos == Vector3.zero) return;
```

- **건물 월드 좌표 분기가 이미 있다** → 규칙 15의 "신규 UI 불필요"가 코드로 확인된다.
- 단 216행 주석이 `현재 힐 대상은 유닛만`으로 **코드보다 좁게** 서술되어 있다 → 구현 시 갱신 대상.
- 구독은 `Initialize`에서 `_healedSubscription?.Dispose(); _healedSubscription = GameEvents.OnEntityHealed.Subscribe(ShowHeal);` (144~145행) — **전역 1개소**이므로 건물 힐 이벤트를 발행하기만 하면 자동으로 텍스트가 뜬다.

---

## 10. `InputHandler` 패널 분기 (필수 조사 9)

**파일:** `Presentation/Input/InputHandler.cs`

**필드**(54~63행): `_productionUI` / `_actionPanelUI` / `_researchPanelUI` / `_skillPanelUI`
**주입**(99~115행): `Initialize(..., ProductionPanelUI, BuildingActionPanelUI, ResearchPanelUI, BuildingSkillPanelUI skillPanelUI = null, SkillAimController = null)` — **뒤 2개는 기본값 null**

**분기 본문**(262~295행) 실측 순서:

```csharp
if (isMine && isAlive)
{
    if (BuildingTypeHelper.IsProductionBuilding(type) && _productionUI != null)      → 생산 패널
    else if (type == BuildingType.Research && _researchPanelUI != null)              → 연구 패널
    else if ((type == FlightFacility || type == MagicBuilding) && _skillPanelUI != null) → 스킬 패널
    else if (BuildingTypeHelper.CanShowActionPanel(type) && _actionPanelUI != null)  → 공용 액션 패널
    // Castle: 어느 분기도 아님 → 무반응
}
```

`BuildingTypeHelper.CanShowActionPanel` (155~163행): 생산 건물 제외 + `Castle` 제외 → **그 외 전부 true**. 즉 **현재 `HealShrine`은 공용 액션 패널(이름 + 철거)로 열린다.**

→ **MistShrine 전용 패널은 스킬 패널 분기와 액션 패널 분기 *사이*에 `else if (type == BuildingType.HealShrine && _mistShrinePanelUI != null)` 를 추가**하면 된다.
`CanShowActionPanel`은 **수정 불필요**(전용 분기가 먼저 걸리고, 패널 미배선 시 액션 패널로 자연 폴백 — 연구·스킬 패널과 동일한 안전망).

> ⚠️ **부가 발견(§13-2):** 227~233행의 `ClosedFrame` 가드는 `_buildingUI` / `_productionUI` / `_actionPanelUI` **3개만** 검사한다.
> **`_researchPanelUI`와 `_skillPanelUI`는 빠져 있다** — 기존 결손이다.

---

## 11. `GameBootstrapper` 주입 패턴 (필수 조사 10)

**파일:** `Bootstrap/GameBootstrapper.cs` / `.Setup.cs` / `.Map.cs`

| 단계 | 위치 | 실측 |
|------|------|------|
| SerializeField 선언 | `GameBootstrapper.cs` 103·106·134행 | `_researchPanelUI` / `_buildingSkillPanelUI` / `_networkSkillController` |
| UseCase 필드 | 197행 | `private SkillActivationUseCase _skillActivation;` |
| **UseCase 생성** | `.Setup.cs` 363~373행 | `_skillActivation = new SkillActivationUseCase(_buildingPlacement, aimWorld => ..., _unitCombat, _skillLoadoutConfig, team => team == TeamId.Blue ? GameRaceContext.BlueRace : GameRaceContext.RedRace);` — **Core/Infrastructure 의존은 전부 람다·인터페이스로 주입**(Domain→Core 금지 우회) |
| **패널 초기화** | `.Setup.cs` `SetupBuildings()` 605~640행 | `if (_researchPanelUI != null) { bool isNetworkMode = IsNetworkMode(); ... _researchPanelUI.Initialize(_unitUpgrade, _resource, upgradeController, _buildingPlacement, buildingController); }` — **null 가드 + `isNetworkMode ? 컨트롤러 : null`** 이 확립된 형태 |
| **입력 배선** | `.Setup.cs` `SetupInput()` 538~543행 | `_inputHandler.Initialize(_gridInteraction, _mainCamera, _buildingPlacement, _buildingUI, _productionUI, _buildingActionPanelUI, _researchPanelUI, _buildingSkillPanelUI, _skillAimController);` |
| **IGameUI 등록** | `.Map.cs` 47~60행 | `_uiManager.Register(_buildingSkillPanelUI); _uiManager.Register(_researchPanelUI);` — 중복 등록 방지는 `GameUIManager.Register()` 내부 처리(43행 주석) |
| UseCase 접근자 | `GameBootstrapper.cs` 357행 | `public SkillActivationUseCase GetSkillActivationUseCase() => _skillActivation;` — `IGameServices` 구현 |
| 건물 파괴 구독 선례 | `.Setup.cs` 416~422행 | `_labDestroyedSub?.Dispose(); _labDestroyedSub = GameEvents.OnBuildingDied.Subscribe(e => { if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return; if (e.Building?.Type != BuildingType.Research) return; _unitUpgrade?.OnLabDestroyed(e.Building.Id, _resource); });` — **서버 가드 + 타입 필터 + 재구독 전 Dispose** |

**`IGameServices`**(`Application/Interfaces/IGameServices.cs`): Infrastructure가 Bootstrap을 직접 참조하지 않도록 하는 추상화. `GetSkillActivationUseCase()` 등 Getter를 노출한다.
→ 신규 MistShrine UseCase도 **`IGameServices`에 Getter를 추가**해야 신규 NetworkBehaviour가 접근할 수 있다.

---

## 12. `BuildingStatsConfig` 및 수치 주입 (필수 조사 11)

### 12-1. `BuildingStatsConfig`

**파일:** `Infrastructure/Config/BuildingStatsConfig.cs`
`BuildingTypeEntry` 필드 실측(47~106행): `buildingType`, `{human,spirit,transcendence}MaxHp`, `…GoldCost`, `…AttackPower`, `…AttackCooldown`, `…AttackRange`, `upgradeCost`.

- **전부 종족 3분할 구조**다. MistShrine은 **초월 전용 단일 건물**이라 3분할이 낭비다.
- **회복량·지속시간·쿨다운·반경에 해당하는 필드가 없다.**
- 소비 경로: `.Setup.cs` 119~170행 → `(BuildingType, RaceId)` 딕셔너리 구축 → `BuildingStats.Initialize(dict)`(Domain 정적 홀더).
  즉 `BuildingStatsConfig`에 필드를 추가하면 **Domain `BuildingStats`의 저장 구조(`StatValues`)와 Getter까지 연쇄 수정**이 필요하다.

### 12-2. `SpecialAttackConfig` — 특수 수치의 선례 (더 적합)

**파일:** `Infrastructure/Config/SpecialAttackConfig.cs`
`[Header]` 단위로 기능별 수치를 평평하게 담는다: BattleAxe Sweep(2개), TorrentSpirit Wave(4개), BloomFairy Heal(2개), Blast(3개), Inferno(2개), Quake(2개)…

주입 형태(`.Setup.cs` 283~313행 실측):

```csharp
float bloomHealAmount = _specialAttackConfig != null ? _specialAttackConfig.BloomHealAmount : 200f;
float bloomHealDuration = _specialAttackConfig != null ? _specialAttackConfig.BloomHealDuration : 3f;
// … 이후 UnitCombatUseCase 생성자에 float 원시값으로 전달
```

- **`null` 폴백 기본값이 코드에 함께 존재**한다 → 에셋 미연결이어도 동작하며, **Inspector 값이 코드 기본값을 덮어쓴다**(MEMORY 제약과 일치).
- Application이 ScriptableObject를 직접 참조하지 않고 **float 원시값만 받는다**(레이어 규칙 준수).
- 에셋 생성 에디터 스크립트 선례: `Assets/Editor/Setup/CreateSpecialAttackConfigAsset.cs`.

→ **MistShrine 튜닝 수치 4종(회복량/지속시간/쿨다운/반경) + 텍스트 표시 주기는 `SpecialAttackConfig` 패턴이 정확히 들어맞는다.**
HP·건설비는 이미 `BuildingStatsConfig`에 있으므로 **건드리지 않는다**.

---

## 13. 건물 파괴 정리 경로 (필수 조사 12)

### 13-1. `GameEvents.OnBuildingDied` 구독 지점 전수

| 파일 | 위치 | 용도 | 해제 방식 |
|------|------|------|----------|
| `Presentation/Grid/HexGridRenderer.cs` | 219행 | 채굴소 파괴 시 금광 오브젝트 재표시 | `.AddTo(...)` |
| `Presentation/Production/ProductionTicker.cs` | 175·208행 | 배럭 `ProductionState` 해제·랠리 마커 제거 / walkable 갱신 | `.AddTo(this)` / `.AddTo(_buildingChangeSubs)` |
| `Presentation/UI/BuildingPanelBase.cs` | 178행 | **패널 자동 닫힘**(상속만 하면 획득) | `.AddTo(this)` |
| `Presentation/Effects/HitPresentationQueue.cs` | 20행 주석 | 사망 타겟 겨눈 잔여 연출 즉시 방출 | (실측 미확인 — 주석만 확인) |
| `Bootstrap/GameBootstrapper.Setup.cs` | 417행 | **연구소 파괴 시 진행 연구 취소·100% 환불** | `_labDestroyedSub?.Dispose()` 후 재구독 |

### 13-2. MistShrine 정리에 채택할 패턴

규칙 25가 요구하는 ① 물안개 제거 ② 자동 모드 제거 ③ 쿨다운 제거는 **전부 서버 권위 상태**다.
→ **`GameBootstrapper.Setup.cs` 416~422행의 연구소 패턴**(서버 가드 + 타입 필터 + `Dispose` 후 재구독)이 정확한 선례다.
→ 패널 닫힘은 `BuildingPanelBase` 상속으로 **자동 획득**되므로 별도 처리 불필요.

---

## 14. 영향 범위 (레이어별 파일 목록)

> 아래는 **이번 구현이 건드릴 것으로 예상되는 파일**이며, 확정 목록은 Plan.md가 갖는다.

### 신규 (7)

| 레이어 | 파일(가칭) |
|------|-----------|
| Application | `UseCases/MistShrineUseCase.cs` |
| Application | `Interfaces/INetworkMistShrineController.cs` |
| Infrastructure | `Network/NetworkMistShrineController.cs` |
| Presentation | `UI/MistShrinePanelUI.cs` |
| Presentation | `Effects/MistShrineRangeIndicator.cs` (또는 `SkillAimReticle` 재사용 시 불필요) |
| Editor | `Assets/Editor/Setup/MistShrineSetup_Scene.cs` (1회성) |
| Editor | `Assets/Editor/Setup/MistShrineSetup_Config.cs` (1회성, 통합 가능) |

### 수정 (10)

| 레이어 | 파일 | 수정 사유 |
|------|------|----------|
| Domain | `Building/BuildingData.cs` | `Heal(int)` 추가 |
| Application | `Interfaces/IGameServices.cs` | `GetMistShrineUseCase()` 추가 |
| Application | `Events/GameEvents.cs` | `EntityHealedEvent` 주석(254·260행 "현재는 유닛만") 갱신 — **구조 변경 불필요**(`IDamageable` + `IsUnit` 이미 건물 표현 가능) |
| Infrastructure | `Config/SpecialAttackConfig.cs` | MistShrine 튜닝 5값 추가 |
| Infrastructure | `Network/NetworkHealthSync.cs` | 건물 힐 동기화 분기 신설(§8) |
| Infrastructure | `Network/NetworkCombatController.cs` | 서버 틱에 MistShrine 틱 추가(331행 인근) |
| Presentation | `Input/InputHandler.cs` | `HealShrine` 분기 + 필드/`Initialize` 파라미터 추가 |
| Presentation | `UI/FloatingHpTextSpawner.cs` | 216행 주석 갱신(코드 변경 없음) |
| Bootstrap | `GameBootstrapper.cs` | SerializeField·UseCase 필드·`Get…` 접근자·`Update` 틱 |
| Bootstrap | `GameBootstrapper.Setup.cs` / `.Map.cs` | UseCase 생성·패널 초기화·입력 배선·`IGameUI` 등록·파괴 구독 |

### 에셋 / 씬 (Inspector 작업)

| 항목 | 상태 |
|------|------|
| MistShrine 패널 프리팹 + 3×3 슬롯 배선 | **미제작** |
| 범위 표시 오브젝트 + 머티리얼 | 셰이더·머티리얼 경로는 존재(`SkillAimOverlay`), **MistShrine용 오브젝트는 미제작** |
| `NetworkMistShrineController` 씬 오브젝트(NetworkObject) | **미배치** |
| **물안개 지속 VFX** | **미제작** — 등록 VFX는 `vfx_mistshrine_destroy` / `vfx_mistshrine_upgrade` 뿐 |
| **사용(시전) 버튼 아이콘** | **미제작**(UI 규칙 15) |

---

## 15. 조사 중 발견한 부가 이슈 / 문서–코드 불일치

### ⚠️ 15-1. Buildings 규칙 8의 "규칙 40 틱 방식 재사용"은 코드로 성립하지 않는다 (설계상 중요)

**규칙 원문:** *"`GameSystemRules_Units.md` 규칙 40(MushroomBomber / InfernoSpirit DoT의 초 단위 틱 시스템)과 **동일한 틱 방식을 재사용**한다."*

**실측 반증 3가지(§3-2):**
1. discrete 틱 경로(`TickDiscreteDamageEffect` → `ApplyOneDamageTick` → `ApplyTimedDamageToUnit`)는 **`Kind`를 검사하지 않고 무조건 피해를 적용**한다. `Kind=Heal` + `TickInterval=1f` 레코드를 넣으면 **회복 대상이 매초 피해를 입는다.**
2. 대상 조회가 `_unitSpawn.Units`뿐이라 **건물은 대상이 될 수 없고**, `TargetId` 단일 키라 **유닛 Id와 건물 Id가 충돌**한다.
3. 레코드 모델(`TotalAmount` 분할)은 **범위 이탈 즉시 끊김(규칙 9 아우라)** 을 표현할 수 없다.

**해석·대응:** 규칙 8은 **"1초 격자 discrete 틱이라는 *동작 방식*을 동일하게 한다"** 로 읽어야 하며, 코드 경로 재사용 지시로 읽으면 안 된다.
자연회복(`TickNaturalRegen`)이 이미 **`_activeTimedEffects`를 쓰지 않는 독립 누적기**로 "1초 단위 정수 HP 적용 + 매 틱 재수집"을 구현하고 있으므로, **이 쪽이 규칙 8·9·14를 동시에 만족하는 정확한 선례**다.
→ 이 해석을 Plan에 명시하고, 사용자 승인 시 **규칙 8 문구 보강**(별도 문서 작업)을 제안한다.

### ⚠️ 15-2. `InputHandler`의 `ClosedFrame` 가드가 패널 2종을 누락하고 있다 (기존 결손)

`InputHandler.cs` 230~232행은 `_buildingUI` / `_productionUI` / `_actionPanelUI`만 검사한다.
`_researchPanelUI`·`_skillPanelUI`가 빠져 있어, 두 패널을 배경 탭으로 닫은 프레임의 클릭이 타일 선택으로 흘러갈 수 있다.
**이번 작업과 무관한 기존 결손**이므로 임의 수정하지 않고 기록만 남긴다.
단 **MistShrine 패널을 추가할 때 같은 누락을 반복하지 않도록** Plan에 명시한다(신규 패널을 가드에 포함).

### 15-3. `NetworkHealthSync` 주석이 신규 도메인 메서드와 어긋나게 된다

311행 `BuildingData.Hp도 TakeDamage를 통해서만 변경 가능.` → `Heal` 추가 후 사실과 달라진다. 함께 갱신 필요.

### 15-4. `EntityHealedEvent` 주석이 코드보다 좁다

`GameEvents.cs` 254행 `회복된 엔티티(현재는 유닛만 사용).` / 260행 `false면 건물(현재 미사용).`
→ 구조는 이미 건물을 표현할 수 있으므로 **주석만** 갱신하면 된다.

### 15-5. `_services` 캐시 전략이 파일마다 다르다

`NetworkProductionController`·`NetworkHealthSync`·`NetworkCombatController`는 `OnNetworkSpawn` 1회 캐시(재조회 없음),
`NetworkSkillController`·`NetworkUpgradeController`는 `ResolveServices()` 지연 재조회.
**신규 컨트롤러는 후자를 따라야 한다**(스폰 레이스 방지 — MEMORY 교훈). 기존 파일 통일은 이번 범위 밖.

### 15-6. 멀티 서버 틱 해상도는 50ms 격자다

`NetworkCombatController.Update`(245~265행)는 `_attackInterval` 격자에서만 `TickCombat(realElapsed)`을 호출한다.
1초 틱·쿨다운에는 무해하나, 물안개 **소멸 시점**이 싱글(매 프레임)과 멀티(최대 50ms 지연)에서 미세하게 다를 수 있다.

### 15-7. `ProductionPanelUI` 자동 인디케이터 중복 배선 (선행 조사 재확인)

UI 규칙 14가 지적한 `_unitAutoIndicators`(List\<GameObject\>) / `_unitBorderOverlays`(List\<Image\>) 중복은 코드에서도 확인된다
(`ProductionPanelUI.cs` 43·58행 선언, 728~742행에서 같은 인덱스로 `SetActive` + `CanvasGroup.alpha` 이중 제어).
**MistShrine 패널은 이 구조를 복제하지 않는다**(규칙 14). 기존 정리는 별도 작업.

---

## 16. 미확인 항목 (CLAUDE.md 규칙 10 — 추정 금지)

| 항목 | 사유 |
|------|------|
| `Game.unity` 씬의 실제 UI 계층·SortingOrder 배치 | 씬 파일을 직접 파싱하지 않았다. 패널 배치 위치는 `GameSystemRules_CanvasSortingOrder.md`를 따르되 **에디터 스크립트 실행 시 사용자 확인** 필요 |
| `HitPresentationQueue`의 `OnBuildingDied` 구독 본문 | 파일 상단 주석(20행)만 확인. 이번 작업과 직접 관련이 없어 본문 미확인 |
| `AnimatedPanel` / `UIManager.ShowBlockingOverlay` 내부 구현 | 베이스가 캡슐화하므로 미확인. 상속만으로 동작 획득 |
| AI가 MistShrine을 사용하는 시나리오 | `GameSystemRules_AI*`에 MistShrine 사용 항목이 있는지 미조사(이번 범위 밖) |
| 물안개 VFX·아이콘 에셋 사양 | **미제작**이므로 조사 대상 없음 |
