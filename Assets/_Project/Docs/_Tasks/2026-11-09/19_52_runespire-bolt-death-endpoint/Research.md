# Research — RuneSpire 번개: 맞는 순간 대상이 죽으면 번개 끝점이 살짝 어긋난다

## 이 문서는 무엇인가

정령 종족의 방어탑 **RuneSpire(룬 첨탑)** 은 적 유닛에게 번개를 쏜다. 번개는 탑 꼭대기 크리스털에서 나와 맞은 유닛의 **가슴 높이**에 꽂히도록 만들어져 있고, 살아 있는 유닛에게는 사용자가 실기에서 정상으로 확인했다(2026-10-15). 그런데 **번개 한 방이 유닛을 죽이는 순간에는 번개 끝이 유닛 위치에서 살짝 벗어나 보인다.** 사용자는 VFX를 새로 만드는 일보다 이런 버그를 먼저 고치자고 정했다 — 원문(2026-11-10): 「RuneSpire 번개 끝점 어긋남 이것부터 수정하도록 하자. vfx까지 작업해야 하는 것보단 이런 버그부터 수정하는 게 좋을 것 같아」.

이 문서는 **고치기 전에 확인한 사실**을 모은다. 요점은 다음과 같다.

- 대상이 이미 죽은 상태로 번개 재생이 시작되면, 코드는 유닛을 따라가지 않고 **서버가 공격 순간에 기록한 위치(스냅샷)** 를 끝점으로 쓴다. 이 경우 **가슴 높이를 유닛 크기 표에서 찾지 못하고 0.6m 고정값을 쓴다** — 표에 있는 유닛의 실제 가슴 높이(약 0.18~0.32m)보다 **약 0.28~0.42m 위**다. 이것은 코드로 확인한 사실이다.
- 사용자가 올린 기존 테스트 로그(2026-11-09)를 메인 세션이 확인한 결과, 「죽는 순간」 경우는 실제로 이 고정값 갈래로 들어가고 있었다.
- 그 밖에 「서버가 기록한 위치와 화면에 그려진 위치가 다르다」 같은 원인 후보도 있으나 **아직 재지 않았다.** 그래서 다음 단계에서 진단 로그를 넣어 함께 잰다.
- 이 결함은 **화면 표현만의 문제**다. 피해 · 타겟 선택 · HP · 쿨다운은 어떤 수정 후보로도 바뀌지 않는다.

이 결함이 사용자 테스트를 통과하면, 이전 VFX 작업에서 주석으로 꺼 두었던 옛 코드 6곳을 최종 삭제한다(2026-10-15 사용자 결정). 수정 계획은 같은 폴더 [Plan.md](Plan.md)에 있다.

**인계 원본:** [이전 VFX 작업 Plan 13-4](../../2026-10-13/13_53_vfx-code-followups/Plan.md) · 같은 작업 [Research 10-4](../../2026-10-13/13_53_vfx-code-followups/Research.md) · [ROADMAP](../../../ROADMAP.md) 「VFX 후속 ①」 행 *(2026-11-10 — 이 작업 완료로 그 행은 삭제됐다)*. 그 문서들의 행 번호는 커밋 `84b6ff2a` 기준이라 지금과 다르다 — **이 문서의 행 번호(HEAD `c28d0d7`)를 쓴다**(대조표는 6절).

---

## 0. 근거 수준 표기

| 표기 | 뜻 |
|---|---|
| `[확인]` | document-manager가 HEAD `c28d0d7`(브랜치 `claude/vfx-code-followups`) 작업 트리에서 이 문서 작성 중 직접 다시 읽은 것 |
| `[전달]` | game-programmer의 HEAD `c28d0d7` 읽기 전용 조사 결과를 메인 세션이 전달한 것 — 이 문서에서 다시 읽지 않은 부분 |
| `[확인 — 메인 세션]` | 메인 세션이 사용자 로그 보관본에서 직접 센 값 — document-manager는 원시 로그를 보지 않았다 |
| `[추정]` | 코드 · 로그로 확인하지 않은 판단 |

- 저장소가 얕은 클론이라 옛 커밋 `84b6ff2a`와 직접 비교할 수 없었다 `[전달]`. 그래서 옛 인계 블록의 본문과 HEAD 코드를 직접 대조했다.
- 경로 머리 `Scripts/` = `Assets/_Project/Scripts/`, `Editor/` = `Assets/Editor/`.

---

## 1. 서버 쪽 — 공격 한 번에 무슨 일이 어떤 순서로 일어나는가

`Scripts/Application/UseCases/TowerCombatUseCase.cs` `ExecuteTowerAttack` `[확인]`

| 행 | 하는 일 |
|---|---|
| 395~401 | 투사체 타워(인간 대포탑)면 서버 발사 기록을 만들고 끝낸다. **RuneSpire는 이 분기를 타지 않는다 — 즉발 피해다** |
| 409~412 | 표현용 시작점(탑 위치)과 **맞은 위치(Impact)** 를 읽는다. Impact = `GetUnitWorldPosition(target.Id)`, 값이 0이면 헥스 중심 `HexToWorld(target.Position)`로 대신한다 |
| 427 | `target.TakeDamage(damage)` — **피해 적용** |
| 431~432 | 표현 스냅샷 생성. `ResultHp = target.Hp`(피해 뒤 HP) |
| 434 | `OnAttackEffectConfirmed` 발행 — 화면 표현 쪽이 이것을 받는다 |
| 440 · 446 | `OnEntityAttacked` · `OnEntityDamaged` 발행(HP 동기화는 이것을 받는다) |
| 451~461 | 대상이 죽었으면 `OnUnitDied` 발행(457) → 도메인 목록에서 제거(460) |

→ **스냅샷이 발행되는 시점에 대상의 HP는 이미 줄어 있다.** 치명타면 스냅샷을 받는 순간 대상 데이터는 「죽음」 상태다(서버 · Host · 싱글).

Impact의 원천: `Scripts/Infrastructure/UnitWorldPositionProvider.cs` 35~39행 — 유닛 GameObject **루트** `transform.position`, 파괴됐으면 `Vector3.zero` `[확인]`. 루트의 높이는 `GameConfig.asset` `UnitYOffset: 0.15`(35행) `[확인]` — 즉 Impact Y는 보통 0.15다.

---

## 2. 화면 쪽 — 스냅샷이 번개가 되기까지

### 2-1. 받는 곳과 재생하는 곳이 다르다

`Scripts/Presentation/Effects/HitPresentationQueue.cs` `[확인]`

- **받기(①):** 533~534행 — 싱글 · Host는 `OnAttackEffectConfirmed`를 구독한다. 535행 — 순수 Client는 `OnAttackEffectReceived`를 구독한다. 둘 다 `ReceiveAttackEffect`(377~389행)로 가서 **목록에 넣기만** 한다(중복 · 용량 확인, 받은 시각 기록).
  - 🔴 싱글 · Host에서는 이 받기가 **서버의 `ExecuteTowerAttack` 안(434행)에서 동기로** 실행된다. 받기 쪽에 넣는 코드가 예외를 던지면 서버 공격 흐름에 닿을 수 있다 `[확인 — 호출 구조]`.
- **재생(②):** `Update`(839~844행)가 매 프레임 `EmitAttackEffects`(390~438행)를 부른다. 재생은 받은 뒤 **다음 `Update`** 에서 일어난다.
- Client 반전 화면이면 402~403행에서 Source · Impact를 `ViewConverter.ToView`로 바꾼다(Y는 바꾸지 않는다 — 좌표계 규칙).
- 경기 종료(`OnGameEnd`)나 재초기화 때 `CloseAttackEffects`(368~375행)가 아직 재생하지 않은 항목을 `Skip … NotStarted=True`로 버린다.

Client로 가는 길 `[확인]`: `Scripts/Infrastructure/Network/NetworkCombatController.cs` 331행 구독 → `PublishAttackEffect`(70~78행) → `AttackEffectClientRpc`(79~96행) → `OnAttackEffectReceived`. HP는 `NetworkHealthSync.cs` `SyncHealthClientRpc`(234행~) → `SyncUnitHealth`(339행~, 피해 적용 368행), 사망은 `NetworkCombatController.cs` 2976행 `EntityDiedClientRpc` 호출 → 3019행 `Despawn(destroy: true)`. 🔴 **Client에서 이 세 RPC가 어떤 순서로 도착하는지는 확인하지 않았다** `[추정 — 미검증]`.

- 스냅샷에는 **맞은 유닛의 종류(UnitType)가 없다** — RPC 인자는 ID · 위치 · 시각 · 피해 · 결과 HP · 결과 키뿐이다(73~76행 · 80~82행) `[확인]`. 대상 종류는 화면 쪽에서 ID로 찾아야만 안다.
- 전투 표현 스키마는 지금 **8**이다(`Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs` 33행) `[확인]`. RPC 모양을 바꾸면 스키마를 올려야 한다.

### 2-2. 재생 시작 때 대상을 찾는다 — 한 번만

`HitPresentationQueue.cs` `ResolveTowerBoltTarget`(449~465행) `[확인]`. 위에서부터 첫 실패에서 멈추고 사유를 남긴다.

| 행 | 실패 조건 | 사유 토큰 |
|---|---|---|
| 452 | 찾는 수단이 연결되지 않음 | `NoResolver` |
| 454 | 그 ID의 유닛 오브젝트가 없다(이미 파괴) | `TargetViewMissing` |
| 459 | 화면 오브젝트의 데이터가 다른 유닛이다 | `TargetDataMismatch` |
| 460 | **대상이 죽었다** | `TargetDead` |
| 461 | 오브젝트가 꺼져 있다 | `TargetInactive` |
| 463 | 화면 Transform을 못 얻음 | `PoseUnavailable` |

실패하면 빈 대상(`default`)을 넘긴다. → **치명타면 재생 시작 때 대상은 이미 죽었으므로 460행(또는 이미 파괴됐으면 454행)에서 실패한다.**

### 2-3. 번개 끝점 — 두 갈래

`Scripts/Presentation/Effects/VfxPoolItem.TowerBolt.cs` `BeginTowerBolt`(138~177행) `[확인]`

| 갈래 | 조건 | 끝점 | 기록 |
|---|---|---|---|
| **추적** (160~166행) | 대상 참조가 있고 지금 읽을 수 있음 | `live + 위 × 가슴 높이`(165행). `live` = `VisualRootProjector.PresentationPosition`(78행), 없으면 VisualRoot `Pose.position`(79행) | `Tracked`. 이후 `LateUpdate`(183~196행)가 매 프레임 따라가고, 도중에 죽으면 그 자리 고정(`EndpointFrozen`, 195행) |
| **고정** (168~174행) | 대상 참조 없음(해석 실패 · 이미 사망) | **`snapshotImpact + 위 × 가슴 높이`(172행)** — 따라가지 않는다 | `Untracked(사유)`(173행) |

### 2-4. 🔴 가슴 높이 — 고정 갈래에서는 항상 0.6m

`Scripts/Presentation/Effects/EffectManager.cs` `[확인]`

- 702~703행: `BeginTowerBolt(…, ResolveBoltChestHeight(boltTarget.Data))`.
- 744~748행: `target == null`(또는 설정 없음)이면 **`UnitEffectConfig.FallbackChestHeight` = 0.6**(`UnitEffectConfig.cs` 210행), 아니면 `GetChestHeight(target.Type)`.
- `UnitEffectConfig.cs` 231~236행 `GetChestHeight` = 크기 표의 균등 배율 × 기준 키 1.0(186행) × 비율(`_chestHeightRatio: 0.6`, `UnitEffectConfig.asset` 367행). 균등 배율은 `manualScale`이 없으면 `height ÷ 1.0`(247~258행).

→ 고정 갈래는 빈 대상(`Data == null`)을 넘기므로 **유닛 종류와 상관없이 항상 0.6m**다. 크기 표의 가슴 높이와의 차이 `[확인 — 표 값 · 계산]`:

| 유닛 (UnitType 값) | 표 키 (`UnitEffectConfig.asset` 행) | 표 가슴 높이 (키 × 0.6) | 0.6m와의 차이 |
|---|---|---|---|
| Pistoleer (0) | 0.342 (244행) | 0.205 | **+0.395m** |
| DustSpirit (13) | 0.293 (299행) | 0.176 | **+0.424m** |
| FoxMagician (21) | 0.307 (334행) | 0.184 | **+0.416m** |
| Tank (6) | 0.521 (279행) | 0.313 | **+0.287m** |
| BearGuard (20) | 0.530 (329행) | 0.318 | **+0.282m** |

화면에서 세로로 약 0.16~0.24 정도로 보일 것이라는 값은 game-programmer의 계산이다 `[전달 — 추정 계산]`.

- 규칙 문구(`GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12 「끝점 높이」, 216행)는 「가슴 높이 = 크기 표의 키 × 비율. **표에 그 유닛이 없으면 0.6m**」다. 지금 고정 갈래는 **표에 있는 유닛에게도 0.6m를 준다** — 문구보다 넓게 대체값을 쓰고 있다 `[확인 — 문구 대조]`.

### 2-5. 살아 있을 때의 끝점과 고정 갈래 끝점의 수평 위치

`Scripts/Presentation/Unit/VisualRootProjector.cs` `ProjectNow`(86~107행) `[확인]`: 루트 위치를 그대로 VisualRoot에 쓰되, **Client이면서 화면이 반전된 경우에만** `ToView`로 바꾼다(Y는 그대로). `PresentationPosition`(37~44행)은 읽을 때마다 `ProjectNow`를 부른다.

- Host · 싱글: 스냅샷 Impact와 살아 있을 때 끝점은 **같은 루트 Transform**에서 나온다 — 같은 프레임이면 수평 차이는 거의 0일 것이다 `[전달 — 추정]`. 단 받기(서버 틱)와 재생(다음 `Update`) 사이에 유닛이 움직였다면 그만큼 차이가 생길 수 있다 `[추정]`.
- Client: 스냅샷 Impact는 **서버의 루트 위치**, 화면 끝점은 **Client에서 보간된 루트 위치**다 — 이동 중이면 차이가 날 수 있다 `[전달 — 추정]`.

### 2-6. 죽은 유닛의 화면 오브젝트는 언제 사라지나

- 타워 즉발 사망은 사망 표현 연기(death ghost) 대상이 아니다 `[전달]` — `Scripts/Presentation/Effects/DeferredUnitDeathPresenter.cs` `TryDefer`(102~112행)는 보관 중인 투사체 사망타 키가 있을 때만 미룬다 `[확인 — 그 범위까지]`.
- 싱글은 `Scripts/Presentation/Unit/UnitView.cs` 2085~2088행에서 `Destroy`, 멀티 서버는 `Despawn(destroy: true)`, Client는 NGO가 파괴한다 `[확인]`.
- 「죽음 처리」와 「재생 시작」의 실행 순서가 코드로 정해져 있지 않아, 재생 시작 때 사유가 `TargetDead`일지 `TargetViewMissing`일지는 코드만으로 정할 수 없다 → 3절 로그가 실제 분포다.

---

## 3. 기존 테스트 로그에서 확인한 것 `[확인 — 메인 세션]`

사용자가 올린 2026-11-09 로그 보관본(대포탑 작업 테스트 회차, 비공개 로그 보관소 — 원시 로그는 이 저장소에 옮기지 않는다. 아래는 수치만)에서 `[Combat/AttackEffectIdentity] Stage=Play … Visual=Tower, Race=Spirit … Endpoint=Untracked(…)` 줄을 센 결과다.

| 항목 | 에디터 (Host, `Role=Server`) | 기기 (Client, `Role=Client`) |
|---|---|---|
| `Untracked(TargetDead)` | **56** | 4 |
| `Untracked(TargetViewMissing)` | 0 | **20** |
| 같은 로그의 끝점 분포 | Tracked 27 · Untracked 56 · Frozen 2 | — |
| Untracked 중 스냅샷 결과 HP = 0 (치명타) | 40 / 56 | 14 / 24 |
| Untracked 중 결과 HP > 0 (살아남은 결과인데 재생 때는 죽어 있음) | 16 / 56 (HP=150 15건 · HP=80 1건) | 10 / 24 (HP=150 10건) |
| Untracked의 Impact Y | 전부 0.15 | 전부 0.15 |

- **결론적 사실:** 사용자가 본 「죽는 순간」 경우는 Host에서 `TargetDead`, Client에서 주로 `TargetViewMissing` 갈래로 들어간다. **두 갈래 모두 2-4의 0.6m 고정이 적용된다**(코드 사실 + 로그 사유 분포).
- Impact Y가 전부 0.15(유닛 루트 높이)이고 헥스 중심 대체(Y = 0)는 0건이다 → 이번 표본에서는 「헥스 중심으로 대체된 Impact」가 관측되지 않았다.
- **결과 HP > 0인데 재생 때 `TargetDead`** 인 줄이 있다(예: Host `Sequence=19 Target=9 Applied=150 HP=150 Endpoint=Untracked(TargetDead)`). 원인은 확인하지 않았다 → 4절 H4.
- 🔴 **사용자가 본 어긋남의 방향(위로 떴는가 · 옆으로 비켜났는가)은 전달받지 않았다.** 그래서 「0.6m 고정이 바로 그 어긋남이다」라고 단정하지 않는다.

참고 — 한 방에 죽는 조건 `[확인 — StatsReference.md]`: RuneSpire 공격력 150(214행), 연구 전 방어력 0이면 피해 150. 체력이 150 이하인 유닛은 **없다**(최저 FoxMagician · RabbitTrickster 200). 즉 **「죽는 순간」은 보통 두 번째 이상의 번개**다 — 예: Pistoleer · Sniper · TideSpirit · StreamSpirit(체력 300)은 정확히 두 방, FoxMagician · RabbitTrickster(200)도 두 방이다. 위 표의 결과 HP 150은 체력 300 유닛의 첫 방과 맞는다 `[추정 — 유닛 종류는 로그에 없다]`.

---

## 4. 원인 가설

| # | 가설 | 지금까지의 근거 | 판정에 필요한 것 |
|---|---|---|---|
| **H0** | 고정 갈래가 표 대신 **0.6m를 써서 끝점이 위로 뜬다** | 코드 사실(2-4) + 로그상 「죽는 순간」이 실제로 이 갈래로 들어감(3절). 차이 +0.28~0.42m | 어긋남이 「위」인지 — 진단 로그 `ChestDeltamm` + 사용자 육안 |
| **H1** | 스냅샷 Impact와 그 순간 화면에 그려진 위치가 **수평으로 다르다** | Host · 싱글은 같은 Transform이라 차이 ≈ 0 예상 `[추정]`, Client는 서버 루트 ↔ 보간 루트라 이동 중 차이 가능 `[추정]` | 받는 순간 · 재생 순간의 화면 위치와 Impact의 수평 차 `HDiffmm` |
| **H2** | Impact가 발밑이 아니다(헥스 중심 대체 · 높이 이중 가산) | 이중 가산 경로는 없다 `[전달]`. 헥스 중심 대체는 이번 표본 0건(3절) | `Impact` Y · 수평 차가 셀 중심 거리만큼 나는지 |
| **H3** | Client 반전(Red 시점)에서만 어긋난다 | 반전 공식은 추적 갈래와 같다 — 근거 없음 `[전달]` | `ViewFlipped=true` 줄과 `false` 줄의 차이 비교 |
| **H4** | 결과 HP > 0인데 재생 때 이미 죽어 있다 — 받기와 재생 사이에 **다른 공격이 먼저 죽였다**(같은 틱의 다른 탑 · 유닛 공격 등) `[추정]` | 3절 Host 16건 · Client 10건. 이 경우도 고정 갈래 + 0.6m가 된다 | 받는 순간 생존(`AliveAtReceive`) · 재생 순간 생존 · 치명 여부(`Lethal`)를 함께 기록 |

- 역할 · 시점 `[전달]`: Host는 항상 Blue, 싱글도 Blue(2026-11-10 수정 — 공용 메모리 대포탑 항목). **반전(Red) 화면은 멀티 Client에서만 생긴다.**
- 원인은 하나가 아닐 수 있다. H0은 코드로 확정된 결함이고, H1 · H4는 같은 진단 로그로 함께 판정할 수 있다.
- *(2026-11-10 테스트 결과 — 판정은 [Plan 12-3](Plan.md#12-3-verdict)이 단일 소스: **H0** F0 뒤 치명 6발(쪽마다) 높이 차 0 → 고쳐짐 · **H1** Host 0, Client 수평 차 최대 약 6.6cm(사용자 육안 문제 없음) · **H2** 관측 없음 · **H3** 반전 화면도 높이 차 0 · **H4** 이번 표본 0건 — 원인 미확인으로 남김)*

---

## 5. 수정 후보 — F0 채택 · 완료 (F1 · F2 · F3 진행 안 함) *(2026-11-10 정정 — 종전 제목: 「수정 후보 (⚠️ 어느 것도 승인되지 않았다)」. F0는 사용자 결정 안 B로 채택돼 `b495ce0`로 구현 · 사용자 테스트 PASS. F1은 결과를 본 뒤 진행하지 않기로 했고 F2 · F3는 채택하지 않았다 — [Plan 12절](Plan.md#12-implementation-and-user-test). 아래 표는 작성 당시 그대로다)*

공통: 피해 · 타겟 선택 · HP · 쿨다운 · 서버 스냅샷 생성은 **어느 후보도 바꾸지 않는다**(F2 · F3 제외 — 표에 적음). 규칙 근거는 Plan 0절.

| 후보 | 내용 | 규칙 12 문구 영향 (`GameSystemRules_Buildings.md` 「방어 타워 시스템」) | 비고 |
|---|---|---|---|
| **F0** | 고정 갈래에서도 **크기 표의 가슴 높이**를 쓴다. 표에 없으면 0.6m 유지 | **문구 변경 없음** — 216행 「표에 그 유닛이 없으면 0.6m」에 오히려 맞춰진다. 219행 상태 줄만 갱신 `[전달]` | 🔴 유닛 종류를 **받는 순간에 잡아 둬야** 한다 — 재생 때는 이미 화면 오브젝트가 없을 수 있다(`TargetViewMissing`). 스냅샷에 종류가 없으므로(2-1) 화면 쪽 데이터에서 찾는다. **Client에서 받는 순간에도 오브젝트 · 도메인 데이터가 이미 없으면 F0로도 0.6m가 남는다** `[추정 — 미검증]` → 진단 로그 `ViewAtReceive` · `TypeSource`로 판정 |
| **F1** | 재생 시작 때 대상이 죽었어도 화면 오브젝트가 남아 있으면 **그 순간 화면 위치**를 끝점으로 쓰고 고정 | 🔴 **문구 변경 필요** — 214행 「추적 불가로 시작할 때: … 서버 스냅샷의 Impact 위치를 끝점으로 쓴다」 → **규칙 변경 전 사용자 승인** | 수평 어긋남(H1)이 확인될 때의 후보 |
| **F2** | 서버의 Impact 원천을 바꾼다(409~412행) | 스키마는 그대로지만 218행 「바꾸지 않는 것: … 스냅샷 · RPC」에 걸린다 | H2가 확인될 때만 의미 |
| **F3** | RPC에 대상 종류 등 인자를 더한다 | 스냅샷 · RPC 구조 변경 → **스키마 8 → 9**, 별도 설계 | F0가 Client에서 종류를 못 찾을 때의 대안 |

검증기 영향 `[확인]`: `Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs` 1720~1726행 (e) 「해석 실패 → 스냅샷 Impact + **0.6m 대체 가슴 높이**」 단언. F0를 넣으면 「종류를 아는 고정 갈래」의 기대값이 바뀌므로 **같은 커밋에서** 검증기를 고쳐야 한다(종류를 모르는 경우의 0.6m 단언은 남는다) `[전달 + 확인]`.

---

## 6. 주석으로 꺼 둔 옛 로직 6곳 — HEAD 위치

이전 VFX 작업이 삭제하지 않고 주석으로 꺼 둔 블록이다. **이 결함이 사용자 테스트를 통과한 뒤에만 최종 삭제한다**(2026-10-15 사용자 결정 — 옛 Plan 13-2 3번). 6곳 모두 HEAD에 남아 있다 `[확인]`. *(2026-11-10 — 사용자 테스트 PASS 뒤 `fa3934c`에서 6곳 모두 삭제됐다(F0의 옛 호출 2곳 포함 8곳). 「남아 있다」는 `c28d0d7` 당시 사실 — [Plan 12-5](Plan.md#12-5-final-deletion))*

| # | HEAD `c28d0d7` 위치 | 꺼 둔 옛 로직 | 표식 | 옛 인계(`84b6ff2a`) 위치 |
|---|---|---|---|---|
| R-1 | `Scripts/Presentation/Effects/VfxPoolItem.cs` 170~173행 | Bloom 「파티클 정확히 5개」 거부 | `[R-1 비활성화` | 약 164행 |
| R-5 | 같은 파일 233~237행 | Bloom 대상 `lossyScale` 곱셈 | `[R-5 비활성화` | 약 210행 |
| R-4 | `Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs` 58~60행 | 화상 · 독 고정 배율 | `[R-4 비활성화` | 약 42행 |
| R-3 | `Editor/Tools/Combat/RunUnitActionSelfValidation.cs` 1422~1424행 | 검증기 개수 5 단언 | `[R-3 비활성화` | 약 1385행 — 🔴 **경로가 바뀌었다**: 옛 인계의 `Assets/_Project/Scripts/Editor/Combat/`는 HEAD에 없다(2026-10-19 에디터 도구 정리로 이동) |
| R2-X1 | `Scripts/Presentation/Effects/VfxPoolItem.TowerBolt.cs` 204~213행 | 번개 XZ 평면 조준 · 길이 | `[R2-2 비활성화` (코드 표식은 R2-2) | 약 204행 |
| R3 | `Editor/Tools/UnitRecipientFitMeasureTool.cs` 332~333행 | 스케일 1을 강제한 `TRS` 배치 행렬 | `[버그 — 2026-10-13 주석 처리]` | 약 332행 |

각 블록을 대체한 새 로직이 사용자 테스트를 통과했다는 기록은 [옛 Research 10-2](../../2026-10-13/13_53_vfx-code-followups/Research.md) 항목별 결과와 [옛 Plan 13-1](../../2026-10-13/13_53_vfx-code-followups/Plan.md) 항목별 최종 상태에 있다(대응은 Plan 최상단 표).

---

## 7. 진단 로그를 넣을 때의 영향 범위와 위험

진단 단계에서 바뀌는 파일 `[전달]`: `HitPresentationQueue.cs`(받는 순간 캡처 · 재생 순간 기록 · 키별 임시 보관), `EffectManager.cs`(진단용 읽기 전용 가슴 높이 조회). `VfxPoolItem.TowerBolt.cs`는 진단만이면 바꾸지 않는다.

| 위험 | 내용 |
|---|---|
| 서버 흐름 오염 | Host · 싱글에서 받기는 서버 `ExecuteTowerAttack` 434행 안에서 동기 실행된다(2-1). 진단 코드는 **자체 `try/catch` 필수** — 예외가 피해 · 사망 처리(440~460행)를 막으면 안 된다 |
| 화면 상태 재기록 | Client에서 받는 순간 `PresentationPosition`을 읽으면 `ProjectNow`가 VisualRoot를 다시 쓴다. 같은 입력이면 같은 값이라 무해하다고 판단 `[전달 — 판단]` |
| 미재생 항목 | 경기 종료 `CloseAttackEffects`(368~375행) 때 재생 전 항목은 재생 기록 줄이 생기지 않는다 — 받기 줄만 있고 짝이 없는 것은 정상일 수 있다 |
| 로그 폭주 | 탑 공격마다 남기면 많아진다 — 경기당 상한이 필요하다(LogRules §1.14 금지 8 취지) |
| 키별 보관 누수 | 받는 순간 값을 키별로 보관하면 재생 · 종료 때 반드시 비워야 한다 |

---

## 8. 이 조사에서 확인하지 않은 것

1. 사용자가 본 어긋남의 **방향과 크기** — 전달받지 않았다.
2. Client에서 HP · 사망 · 공격 표현 RPC의 **도착 순서** — `[추정 — 미검증]`. F0가 Client `TargetViewMissing` 경우까지 고칠 수 있는지가 이것에 달렸다.
3. H4(결과 HP > 0인데 재생 때 사망)의 실제 원인.
4. 받기와 재생 사이의 시간 · 그 사이 유닛 이동 거리.
5. 3절 로그 수치는 메인 세션이 센 값이며 document-manager는 원시 로그를 보지 않았다.
6. 2-6의 「타워 즉발 사망은 사망 연기 대상이 아니다」는 `TryDefer` 본문까지만 확인했고, 보관 사망타 키 수집(`CollectHeldKillShotKeys`) 내부는 읽지 않았다 `[전달]`.

*(2026-11-10 — 위 목록은 조사 당시 그대로 둔다. 이후 테스트 로그로 답이 나온 것: 2번 — Client 받는 순간 `AliveAtReceive=true` · `TypeSource=View` 12/12로 **이 표본에서는** 공격 표현이 사망 처리보다 먼저 도착했다. 4번 — 받는 순간 수평 차 `HDiffmm` 1~66mm. 3번(H4)은 이번 표본 0건이라 여전히 미확인. 1번은 사용자 육안 「문제없어 보여」로 대신했다. 근거: [Plan 12-3](Plan.md#12-3-verdict))*

---

## 9. 변경 이력

| 날짜 | 내용 |
|---|---|
| 2026-11-10 (KST · 폴더 이름은 UTC 2026-11-09 19:52) | 최초 작성 — 사용자 승인 「task 문서 작성해」. game-programmer 읽기 전용 조사(HEAD `c28d0d7`) · 메인 세션 로그 확인 · document-manager 직접 재확인 |
| 2026-11-10 | 완료 반영 — 5절 제목 정정(F0 채택 · 완료), 4절 · 8절 끝에 테스트 결과 포인터. 결과 본문은 Plan 12절. 6절 6곳은 `fa3934c`에서 삭제됨(Plan 12-5) — 6절 표의 행 번호는 `c28d0d7` 기준 기록이다 |
