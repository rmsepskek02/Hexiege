# Research — 전투 타격 타이밍 동기화 (combat-hit-timing-sync)

작성일: 2026-07-09
작업 폴더: `Assets/_Project/Docs/_Tasks/2026-07-09/01_12_combat-hit-timing-sync/`

---

## 이 작업이 무엇이고 왜 하는가 (비개발자용 설명)

우리 게임의 전투를 보면, 유닛이 칼을 휘두르거나 총을 쏘는 **화면 속 동작(애니메이션·이펙트)** 과 실제로 **상대의 체력이 깎이는 순간**이 서로 딱 맞지 않는 경우가 있습니다. 예를 들어 칼이 아직 상대에게 닿지도 않았는데 체력 숫자가 먼저 줄어들거나, 반대로 이미 다 휘두른 뒤에야 뒤늦게 체력이 깎이는 식입니다. 특히 여러 대가 함께 싸우거나, 인터넷으로 두 명이 대전하는 멀티플레이에서는 이 어긋남이 더 눈에 띕니다.

또 하나의 문제는 **연출 자체가 아예 없는 곳**이 있다는 점입니다. 방어 타워가 유닛을 공격할 때는 총알이나 대포 발사 이펙트가 전혀 없어서 "타워가 일하고 있다"는 느낌이 나지 않고, 맞은 유닛도 아무 반응이 없습니다. 원거리 유닛도 발사체(총알·화살)가 날아가는 모습 없이 상대 체력만 깎입니다.

이 작업은 이 두 가지를 근본적으로 해결하기 위한 **사전 조사(Research)** 입니다. 코드를 바로 고치지 않고, 지금 전투가 정확히 어떻게 돌아가는지 파일 단위로 확인하고, 어긋남이 생기는 원인을 정확히 짚어 다음 단계(Plan)에서 안전하게 설계할 수 있도록 근거를 정리합니다.

이 작업의 대원칙은 다음과 같습니다.
- **데미지 판정은 오직 서버만** 한다(서버 권위). 화면 연출을 고치더라도 "누가 얼마나 데미지를 입는가"의 결정 권한은 절대 흔들리지 않는다.
- **데이터(체력·데미지)는 서버 시계**를 따르고, **연출(애니메이션·이펙트)은 각 플레이어 화면의 로컬 시계**를 따른다. 즉 역할을 분리한다.
- 작업량이 많더라도 **완성도를 우선**한다.

---

## 1. 현재 전투 파이프라인 구조 (코드 근거)

전투는 "데이터 경로(누가 얼마나 데미지를 받는가)"와 "연출 경로(화면에 어떻게 보이는가)"가 나뉘어 있으며, 싱글플레이와 멀티플레이가 서로 다른 구현을 사용한다.

### 1-1. 유닛 전투 — 데이터 경로

**`Application/UseCases/UnitCombatUseCase.cs`**
- 타겟 탐색(`FindFirstEnemyTarget`), 사거리 계산(`CalculateRangeLimits`, 월드 좌표 기준), 데미지 적용(`ApplyAttackDamage` → `ExecuteAttack`)을 담당한다.
- 싱글플레이: `TryAttack()`에서 공격 애니메이션 시작 순간, `attacker.HitFrameTimes`(float[]) 각 원소마다 `PendingHit` 하나를 `_pendingHits` 리스트에 등록한다(L207~232). `TickPendingHits(dt)`(L370~393)가 `GameBootstrapper.Update()`에서 매 프레임 호출되어 각 타이머를 감소시키고, 0 이하가 되면 `ApplyAttackDamage`로 실제 데미지를 적용한다.
- 멀티플레이: `TryAttack()` 초입에서 `NetworkContext.IsNetworkActive`면 즉시 `return null`(L170) — 데미지는 전적으로 서버(`NetworkCombatController`)가 처리한다. HOST(서버)도 여기서 차단되어 이중 데미지를 방지한다.
- **타겟 고정(Target Lock) 설계**(L303~314 주석): 공격 모션이 시작되면 타겟이 확정되며, 딜레이 도중 타겟이 사거리를 벗어나도 데미지를 적용한다. 취소되는 경우는 오직 ① 공격자 사망 ② 타겟 사망 두 가지뿐이다(`ApplyAttackDamage` L322·L326).
- `ExecuteAttack`(L785~841)이 실제 데미지(`target.TakeDamage`)와 세 이벤트(`OnEntityAttacked`, `OnEntityDamaged`, 사망 시 `OnUnitDied`/`OnBuildingDied`)를 발행한다.

**`Domain/Unit/UnitStats.cs` / `UnitData.cs`**
- `HitFrameTimes`는 `UnitStatsConfig`(ScriptableObject)에 **수동 입력**되며, `UnitStats.GetHitFrameTimes()`(L149~156)가 반환한다. Config에 값이 없으면 안전망으로 `[0.2f]` 하나를 반환한다.
- `UnitData` 생성 시 `HitFrameTimes = UnitStats.GetHitFrameTimes(type)`(UnitData.cs L153), `AttackCooldown = UnitStats.GetAttackCooldown(type)`(L151)으로 복사된다.
- **`AttackCooldown`은 별도 경로로 덮어써진다**: `UnitFactory.CreateUnit`에서 `GetAttackClipLength(animator)`(UnitFactory.cs L419~426)가 이름에 "Attack"이 포함된 첫 클립의 `clip.length`를 읽어 `unitData.AttackCooldown`에 대입한다(L256~260). 즉 **쿨다운은 이미 클립 길이를 자동으로 따르고 있으나, 타격 시점(HitFrameTimes)은 여전히 수동**이다.

### 1-2. 유닛 전투 — 멀티플레이 서버 처리

**`Infrastructure/Network/NetworkCombatController.cs`** (NetworkBehaviour, 서버 권위)
- `_attackInterval = 0.05f`(50ms)마다 `Update()`(L193~207)에서 `TickCombat(elapsed)`을 호출한다. `_attackTimer -= _attackInterval`로 오버슈트를 다음 Tick에 이월하고, 쿨다운 감소는 고정값이 아니라 실제 경과 시간 `elapsed`로 처리한다.
- `TickCombat`(L220~361): 살아있는 모든 유닛을 순회하며 쿨다운 감소 → `TryFindTarget`으로 타겟 확인 → 상태 변화 시에만 3종 RPC(`StartCombatClientRpc`/`ChangeTargetClientRpc`/`StopCombatClientRpc`)를 전송한다. 같은 타겟을 계속 공격 중이면 RPC 없이 데미지 코루틴만 실행한다.
- `ExecuteAttack`(L404~415): 쿨다운 리셋 후 `unit.HitFrameTimes`의 각 원소마다 `DelayedAttackDamage` 코루틴을 하나씩 시작한다. 코루틴(L379~395)은 `WaitForSeconds(delay)` 후 `ApplyAttackDamage`를 호출한다.
- **첫 사이클 동기화**: `OnUnitEnteredCombatHandler`(L437~484)가 적 최초 감지 시 `StartCombatClientRpc`와 `ExecuteAttack`을 **동시에** 실행하여 서버 공격 사이클을 애니메이션 루프의 T=0과 맞춘다(L480~483 주석).

**`Infrastructure/Network/NetworkHealthSync.cs`** (NetworkBehaviour)
- 서버가 `OnEntityDamaged`를 구독(L74) → `SyncHealthClientRpc(entityId, isUnit, serverHp)`(L136)로 전파한다.
- 클라이언트는 도착 즉시 도메인 HP를 서버 값에 맞추고(`TakeDamage(diff)`), FloatingHpText가 반응하도록 `OnEntityDamaged`를 **재발행**한다(L192, L224).
- **핵심: 이 RPC에는 "누가 때렸는가(공격자 Id)"가 전혀 담기지 않는다.** `EntityDamagedEvent`(GameEvents.cs L151~168)도 `Entity`(피격자), `CurrentHp`, `IsUnit`만 보유하고 공격자 정보가 없다.

### 1-3. 유닛 전투 — 연출 경로

**`Presentation/Unit/UnitView.cs`**
- Attack 클립은 Loop Time=ON이며, `StartCombatAnimation`(L1540~1563)에서 `CrossFadeInFixedTime(StateAttack, _toAttackBlend=0.08f, 0)`로 1회만 CrossFade하면 무한 루프된다.
- Animation Event `OnAttackHit()`(L1477~1495)는 `EffectManager.PlayUnitAttack`(VFX)과 `AudioManager.PlayUnitAttackSfx`(SFX)만 재생한다. **주석(L1474~1475)에 "실제 데미지 로직과는 무관 — 순수 비주얼 피드백"이라고 명시되어 있다.** 즉 이 이벤트는 데미지를 유발하지 않고, 데미지 타이밍(HitFrameTimes/코루틴)과 완전히 별개로 동작한다.
- 이 이벤트는 모든 클라이언트에서 로컬로 실행되므로 멀티플레이 양쪽 화면에 공격 이펙트가 정상 재생된다.

**`Presentation/Unit/AnimationEventRelay.cs`**: 자식 Animator에 붙은 Animation Event를 부모의 `OnAttackHit()`로 중계한다(모델이 자식 오브젝트에 있는 구조 대응).

**`Presentation/Effects/EffectManager.cs`** (VFX 전용, static Instance)
- 공개 API: `PlayUnitAttack` / `PlayUnitDeath` / `PlayBuildingDestroy` / `PlayBuildingUpgrade` / `PlayUi`. **피격(Hit) 재생 API가 없다.**
- Config는 `UnitEffectConfig`(GetAttack/GetDeath) / `BuildingEffectConfig`(GetDestroy/GetUpgrade) / `UiEffectConfig`를 참조한다. `BuildingEffectConfig`에는 파괴·업그레이드 프리셋만 있고 **공격(발사) 프리셋 슬롯이 없다**(BuildingEffectConfig.cs L25~35).
- SFX 관련 코드는 이미 전부 주석 처리(`SOUND_SYSTEM_REFACTOR`)되어 AudioManager로 이관 완료 상태 — 검증 후 최종 삭제 예정(파일 내 주석). **이번 작업 범위 밖.**

**`Presentation/UI/FloatingHpTextSpawner.cs`**
- `OnEntityDamaged` 도착 즉시(L142~177) 피격 엔티티 머리 위에 남은 HP 텍스트를 띄운다. 서버는 데미지 적용 시점, 클라이언트는 `SyncHealthClientRpc` 도착 시점에 표시된다.

**`Presentation/Unit/UnitEffectView.cs`** — 전체 주석 처리(**DEPRECATED 2026-06-08**)
- 과거 `OnEntityAttacked`(머즐 플래시) + `OnEntityDamaged`(피격 이펙트)를 유닛별로 구독해 `Instantiate`하던 컴포넌트. 파일 상단 주석에 "기존 OnEntityAttacked 구독은 서버 전용이라 멀티플레이 클라이언트에서 VFX가 보이지 않는 버그가 있었음"이라고 기록되어 있다. 프리팹에서 컴포넌트 제거 및 파일 삭제는 "사용자 테스트 통과 후"로 유보된 상태.

### 1-4. 타워 전투

**`Application/UseCases/TowerCombatUseCase.cs`**
- `Tick(dt)`(L90~131): AutoTower만 순회하며 쿨다운 감소 → 사거리 내 가장 가까운 적 유닛 탐색 → **쿨다운 만료 즉시 데미지 적용**(히트 딜레이 없음, 유닛과 달리 즉발). 서버 권위 가드(L94)로 클라이언트 호출을 차단한다.
- `ExecuteTowerAttack`(L200~228)은 `OnEntityAttacked` / `OnEntityDamaged` / `OnUnitDied`를 발행한다. 따라서 HP 동기화·사망 전파는 기존 경로로 자동 처리된다.
- 멀티플레이에서는 `NetworkCombatController.TickCombat`이 `tower?.Tick(elapsed)`을 서버에서만 호출한다(NetworkCombatController.cs L238~239).
- **연출 부재 확인**: `OnEntityAttacked`를 구독하는 활성 Presentation 코드가 현재 없다(구독처였던 `UnitEffectView`가 DEPRECATED). 따라서 타워의 공격 발사 연출도, 맞은 유닛의 피격 연출도 전혀 없다.

---

## 2. 확인된 타이밍 불일치 3원인

### 원인 ① 타격 타이밍이 두 곳에 이중 정의
같은 "언제 타격이 일어나는가"가 **두 군데**에 따로 존재한다.
- 화면 연출: Attack 클립의 Animation Event `OnAttackHit` 위치(에디터에서 클립에 찍힌 시간)
- 데이터 데미지: `HitFrameTimes`(UnitStatsConfig에 **수동 입력**한 초 단위 배열)

둘은 서로 동기화 장치가 전혀 없다. 애니메이터가 클립의 Event 위치를 바꾸거나 수동 입력값을 잘못 넣으면 즉시 어긋난다. 반면 쿨다운(`AttackCooldown`)은 이미 클립 길이를 자동으로 읽어 오므로(1-1 참조), **타격 시점만 수동으로 남아 있는 불균형** 상태다.

### 원인 ② TickCombat 50ms 격자 오차 (멀티, 2번째 사이클부터)
멀티플레이 서버는 쿨다운 만료를 50ms 간격의 `TickCombat` 격자에서만 감지한다. 첫 사이클은 `OnUnitEnteredCombatHandler`에서 RPC와 `ExecuteAttack`을 동시에 실행해 T=0으로 정확히 맞춘다(L480~483). 그러나 **2번째 사이클부터는** 쿨다운이 만료된 실제 시점과 그 만료를 감지하는 다음 Tick 사이에 최대 50ms의 지연이 생긴다. 코드 주석(L433~435)도 "다음 TickCombat(T≈50ms)에서 ExecuteAttack이 처음 실행되어 서버 사이클이 T=0.05에서 시작 → 쿨다운 만료 T=3.05 ≠ 애니메이션 루프 경계 T=3.0"이라고 같은 문제를 지적한다. 이 오차는 사이클마다 최대 50ms씩 튀며 데미지 코루틴 딜레이의 기준 시점을 흔든다.

### 원인 ③ 네트워크 지연 + 피격 연출 부재
클라이언트 화면에서 공격자가 타격 프레임(`OnAttackHit`)에 도달하는 시점과, 서버가 계산한 데미지·HP가 `SyncHealthClientRpc`로 도착하는 시점은 네트워크 지연만큼 어긋난다. 게다가 맞은 쪽에는 **피격 반응 연출이 없어**(UnitEffectView DEPRECATED, EffectManager에 Hit API 없음) HP 숫자만 갑자기 바뀐다. 결과적으로 "때리는 화면"과 "맞는 화면"이 시간·시각 양쪽에서 따로 논다.

---

## 3. 연출 공백 정리

| 대상 | 현재 상태 | 근거 |
|------|-----------|------|
| 근접 유닛 공격 이펙트 | `OnAttackHit`에서 `PlayUnitAttack`+SFX 재생 (정상) | UnitView L1493~1494 |
| 유닛 피격 반응 | **없음** (UnitEffectView DEPRECATED, EffectManager Hit API 없음) | UnitEffectView.cs, EffectManager.cs |
| 타워 공격 발사 연출 | **없음** (OnEntityAttacked 구독처 부재) | TowerCombatUseCase, BuildingEffectConfig에 Attack 슬롯 없음 |
| 타워가 맞힌 유닛 피격 연출 | **없음** | 동일 |
| 원거리 유닛 발사체(트레이서) | **없음** (데미지만 발생, 발사체 비행 표현 없음) | UnitView/EffectManager에 트레이서 개념 없음 |

---

## 4. 영향 범위 (변경 시 파급)

- **데이터 경로**(UnitCombatUseCase / NetworkCombatController / TowerCombatUseCase): 데미지 판정 로직 자체는 유지하되, 타이밍 계산과 이벤트 페이로드(공격자 Id)에 손을 대면 싱글·멀티 양쪽 모든 전투에 영향을 준다. HOST/Client 모두 검증 필요.
- **이벤트 계약**(`EntityDamagedEvent` / `SyncHealthClientRpc`): 시그니처에 공격자 Id를 추가하면 발행처(UnitCombatUseCase, TowerCombatUseCase, NetworkHealthSync 재발행)와 구독처(FloatingHpTextSpawner, 신설 피격 큐) 전부가 함께 바뀐다. RPC 시그니처 변경은 서버·클라 빌드 호환성에 직접 영향.
- **연출 경로**(EffectManager / UnitView / Config들): Hit API·트레이서·타워 발사 프리셋 추가. 레이어 규칙상 Presentation 내부에서만 처리해야 하며, Infrastructure→Presentation 역참조 금지(반드시 GameEvents 경유).
- **에셋/Inspector**: `HitFrameTimes` 자동화 시 UnitStatsConfig의 수동 입력이 무의미해지고, 클립에 `OnAttackHit` 이벤트가 없는 유닛이 있으면 자동 추출이 실패한다 → 전 유닛 클립 전수 검사 필요.
- **삭제 예정 자산**: `UnitEffectView.cs`(주석 상태) 및 이를 부착한 프리팹 컴포넌트 — 신규 피격 파이프라인이 그 역할을 정식 대체하면 최종 삭제 대상이 된다.

---

## 5. 위험 요소 / 부가 이슈

1. **클립에 `OnAttackHit` 이벤트가 없는 유닛**: `HitFrameTimes` 자동 추출이 빈 배열을 만들면 안전망(`[0.2f]`)으로만 동작해 여전히 어긋난다. 전 유닛 Attack 클립의 이벤트 유무·개수를 실제로 확인해야 자동화의 신뢰도를 보장할 수 있다(에디터 검증 스크립트 필요).
2. **다중 히트 유닛**(예: FlameSpirit 6히트, LionKnight 2히트): 클립에 `OnAttackHit`가 여러 번 찍혀 있어야 하며, 자동 추출 시 이벤트 시간들을 오름차순으로 모두 수집해야 한다.
3. **RPC 시그니처 변경 호환성**: `SyncHealthClientRpc`에 공격자 Id 파라미터를 추가하면 구버전 클라이언트와 호환되지 않는다. 개발 단계라 문제는 아니나, 서버·클라 동시 빌드 갱신이 전제.
4. **피격 큐의 안전망**: 공격자의 로컬 `OnAttackHit`가 (지연·프레임 드랍으로) 오지 않거나 타겟이 먼저 죽는 경우, HP 표시가 영원히 보류되면 안 된다. 타임아웃·사망 시 강제 방출 로직이 반드시 필요하다.
5. **서버 권위 불변**: 어떤 연출 최적화도 "데미지는 서버 타이머로만" 원칙을 깨선 안 된다. 특히 데미지를 Animator 상태(`OnAttackHit`)에 종속시키면 애니메이션이 끊기거나 늦는 클라이언트에서 데미지 누락이 발생하므로, 데미지는 계속 서버 타이머 기반으로 유지해야 한다.
6. **`Y Scale 0.4` 타일 등 기타 무관 자산은 건드리지 않는다** — 이번 작업은 전투 타이밍·연출에 한정한다.

---

## 6. 참고한 파일 목록

- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`
- `Assets/_Project/Scripts/Application/UseCases/TowerCombatUseCase.cs`
- `Assets/_Project/Scripts/Application/Events/GameEvents.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkHealthSync.cs`
- `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs`
- `Assets/_Project/Scripts/Domain/Unit/UnitStats.cs`, `UnitData.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`, `UnitEffectView.cs`, `AnimationEventRelay.cs`
- `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs`, `BuildingEffectConfig.cs`
- `Assets/_Project/Scripts/Presentation/UI/FloatingHpTextSpawner.cs`
