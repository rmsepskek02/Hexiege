# Plan — MistShrine 물안개 힐 시스템 구현

**작성일:** 2026-08-10
**갱신:** 2026-08-10 — ① `GameSystemRules_Buildings.md` **MistShrine 규칙 8 정정**(내용 교체·번호 유지)에 맞춰 인용 갱신 ② `InputHandler` `ClosedFrame` 가드의 **기존 결손 2종(연구·스킬 패널)을 이번 범위로 편입**(사용자 승인)
**갱신:** 2026-08-11 — 구현 완료 후 **사용자 실기 테스트 중 발생한 보완 수정 3건**을 **§9(신설)** 에 기재. 이에 맞춰 최상단 「기존 로직 제거 여부」 절에 **필드 제거 2건**을 추가하고, 위험 요소에 **R13·R14**를, 완료 판정에 해당 항목을 반영. 기존 §9(완료 판정)는 **§10으로 번호 이동**(내용 유지 + 항목 추가)
**현재 상태:** **코드 구현 완료 / 사용자 실기 테스트 진행 중** — "테스트 완료"가 아니다
**작업 폴더:** `Assets/_Project/Docs/_Tasks/2026-08-10/14_12_mistshrine-heal-implementation/`
**선행 문서:** [Research.md](Research.md)
**선행 사이클(문서 전용):** `_Tasks/2026-08-10/09_34_mistshrine-heal-redesign/`
**구현 담당:** **game-programmer 에이전트** (이 문서 작성 시점에는 코드를 작성하지 않는다)

---

## 이 계획이 무엇이고 왜 필요한가 (자연어 설명 — 기술 용어 없이)

초월 종족의 **MistShrine(물안개 신전)** 은 "회복시켜 주는 건물"로 만들어져 있지만,
**지금은 아무 회복도 하지 않습니다.** 지을 수 있고 부서지기만 하는 상태입니다.

오전 작업에서 이 건물이 앞으로 어떻게 동작할지를 정해 문서에 적어 두었고,
**이번 계획은 그 내용을 실제로 게임에서 작동하게 만드는 순서와 방법을 정리한 것**입니다.

만들려는 동작을 쉬운 말로 옮기면 이렇습니다.

- 내 MistShrine을 누르면 **전용 창**이 뜹니다. 창에는 **사용 버튼**과 **철거 버튼**이 있습니다.
- 사용 버튼을 **짧게 누르면 한 번 사용**되어 건물 주위에 **물안개가 깔립니다.**
- 물안개가 깔려 있는 동안, **그 안에 있는 내 편 부대와 내 편 건물이 1초마다 체력을 회복**합니다.
  내 것이니 **이 건물 자신과 본진도 회복 대상**입니다.
- 물안개 **밖으로 나가면 그 즉시 회복이 끊기고**, 다시 들어오면 다시 회복됩니다.
- 물안개는 정해진 시간이 지나면 **걷히고**, 그 뒤 조금 더 기다려야 다시 쓸 수 있습니다.
  기다리는 시간은 버튼 위에 **시계 모양으로 남은 초**가 표시됩니다.
- 사용 버튼을 **길게 누르면 자동 모드**가 켜집니다. 자동 모드에서는 기다리는 시간이 끝나는 즉시 알아서 사용됩니다.
  **처음에는 꺼져 있고**, 자동 모드일 때 짧게 누르면 꺼집니다.
- 쓰는 데 **골드는 들지 않습니다.**
- **건물이 부서지면 물안개도 그 즉시 사라집니다.**
- 물안개가 여러 개 겹쳐도 **회복은 한 번만** 받습니다(가장 가까운 건물 것으로).
  다만 연구소에서 배우는 **초월 자연회복은 다른 효과라 물안개와 같이 적용**됩니다.
- 창이 열려 있는 동안에는 **회복 범위가 땅에 반투명한 원**으로 보입니다. 창을 닫으면 사라집니다.
  **적의 MistShrine을 눌러도 범위는 보이지 않습니다.**

### 이번 작업에서 특히 중요한 두 가지 판단

**첫째, 숫자는 임시값으로 넣고 나중에 바꿉니다.**
회복량·물안개 지속시간·다시 쓰기까지 걸리는 시간·범위 크기는 **아직 정해지지 않았습니다.**
그래서 이번에는 **구조를 먼저 완성하고**, 숫자는 임시로 넣습니다.
이 프로젝트는 숫자를 코드가 아니라 **설정 파일에서 읽어 쓰는 구조**라서,
나중에 숫자가 정해지면 **설정 파일만 고치면 되고 다시 만들 필요가 없습니다.**

**둘째, 이번에는 물안개가 눈에 보이지 않습니다.**
물안개를 표현할 **그림 효과와 버튼 아이콘이 아직 만들어지지 않았습니다.**
그래서 이번 구현이 끝나도 **"동작은 하지만 연출은 없는" 상태**가 됩니다.
회복이 실제로 되는지는 체력이 오르는 숫자와 체력 막대로 확인하게 됩니다.
(회복 범위를 보여주는 반투명 원은 기존에 쓰던 것을 재활용하므로 이번에도 보입니다.)

### 이번에 함께 고치는, 원래 있던 문제 하나

이 게임에는 **창을 닫으려고 바깥쪽을 눌렀을 때, 그 누름이 창 뒤의 땅까지 같이 눌러 버리는 것을 막아 주는 장치**가 있습니다.
그런데 확인해 보니 **연구 창과 스킬 창에만 이 장치가 걸려 있지 않았습니다.**
그래서 이 두 창은 닫는 순간의 누름이 뒤쪽 땅 선택으로 새어 나갈 수 있는 상태였습니다.

이것은 **MistShrine과 아무 상관 없이 원래부터 있던 문제**입니다.
다만 이번에 새로 만드는 MistShrine 창에도 똑같은 장치를 달아야 해서 **어차피 같은 자리를 손대게 되므로**,
사용자 승인을 받아 **이번에 셋을 한 번에 고칩니다.**
(나중에 "MistShrine 작업이 왜 연구·스킬 창을 건드렸지?"라는 의문이 생기지 않도록 여기에 이유를 남겨 둡니다.)

### 규칙 문서 한 곳을 먼저 고쳤습니다

계획을 짜다가 **규칙 문서에 적힌 방법대로 만들면 오히려 회복이 아니라 피해가 들어가는** 문제를 발견했습니다.
그래서 구현을 시작하기 전에 **규칙 문서를 먼저 바로잡았습니다.**
회복이 **1초마다 일어난다**는 점은 그대로이고, **"기존의 어떤 장치를 빌려 쓸 것인가"** 만 바뀌었습니다.

---

## ⚠️ 기존 로직 제거 여부 (WORKFLOW.md [4] 최상단 기술 규칙)

> **요약 (2026-08-11 갱신):** **계획 단계에서는 제거 0건**이었으나, **구현 후 보완 수정에서 직렬화 필드 제거 2건이 발생**했다.
> 아래 ①이 계획 시점 내용(원문 유지), ②가 추가된 내용이다.

### ① 계획 단계 — 기존 로직 제거·비활성화 **0건**

**이 계획이 세운 범위에서 제거하거나 비활성화(주석 처리)하는 기존 로직은 없다. 전부 신규 추가 또는 확장이다.**

기존 코드에 손을 대는 지점은 아래 5곳뿐이며, **모두 "동작 추가" 또는 "주석 정정"이고 기존 동작을 없애지 않는다.**
(이 중 `ClosedFrame` 가드 한 곳은 **기존 결손 보정**을 겸한다 — §2-4-2.)

| 지점 | 성격 | 기존 동작 보존 근거 |
|------|------|-------------------|
| `NetworkHealthSync.OnEntityHealed` (146행 `if (!e.IsUnit \|\| !(e.Entity is UnitData unit)) return;`) | **분기 추가** — 기존 조기 반환을 "건물 분기로 흘려보내기"로 확장 | 유닛 경로(`SyncUnitHeal`)는 **한 줄도 바뀌지 않는다.** §2-3의 (b)안 채택 시 이 줄 자체를 건드리지 않아도 된다 |
| `InputHandler` 건물 클릭 분기 (262~295행) | **`else if` 한 갈래 추가** | 기존 4갈래는 순서·조건 모두 불변. `HealShrine`은 지금 마지막 액션 패널 갈래로 흘러가는데, 앞에 전용 갈래가 생겨도 **패널 미배선 시 기존 갈래로 폴백**된다(연구·스킬 패널과 동일 안전망) |
| `InputHandler` `ClosedFrame` 가드 (230~232행) | **OR 조건 3개 추가** (신규 MistShrine 패널 1 + **기존 누락분 연구·스킬 패널 2** — §2-4-2) | 기존 3개 항(`_buildingUI`/`_productionUI`/`_actionPanelUI`)은 **문구 그대로 유지**하고 뒤에 항만 덧붙인다. 제거·조건 변경 없음. 각 항은 `!= null` 가드를 동반하므로 **미배선 패널은 기존과 동일하게 무시**된다 |
| `GameBootstrapper.Update` / `NetworkCombatController.TickCombat` | **틱 호출 1줄 추가** | 기존 틱 호출 순서·가드 불변 |
| `FloatingHpTextSpawner.ShowHeal` 216행 주석 · `NetworkHealthSync` 311행 주석 · `GameEvents.EntityHealedEvent` 254·260행 주석 | **주석 문구 정정만** | 실행 코드 무변경 |

> **`BuildingTypeHelper.CanShowActionPanel`은 수정하지 않는다.** MistShrine 전용 분기가 그보다 먼저 걸리므로 변경이 불필요하며,
> 변경하면 패널 미배선 시의 폴백 안전망을 잃는다.

### ② 구현 후 보완 수정 — **직렬화 필드 제거 2건** (2026-08-11 추가 / 상세는 §9)

실기 테스트 중 드러난 문제를 고치면서 **`[SerializeField]` 필드 2개를 주석 처리가 아니라 제거**했다.
WORKFLOW.md [4]는 "검증 전까지는 제거 대신 비활성화(주석 처리)"를 기본으로 요구하므로, **그 기본을 따르지 않은 근거를 아래에 남긴다.**

| 제거 대상 | 소속(제거 시점) | 주석 처리가 아니라 **제거**를 택한 근거 |
|------|------|------|
| `MistShrinePanelUI._rangeRadius`<br>(`[SerializeField] float`) | **이번 사이클에서 처음 추가된 필드** — 기존 로직이 아니다 (이 Plan §2-4-1의 `SerializeField` 목록에도 없던, 구현 중 추가분) | ① 되돌릴 **검증된 기존 동작이 존재하지 않는다** ② 직렬화 필드는 주석 처리해도 **씬 저장값이 어차피 사라지므로 "비활성화"가 성립하지 않고**, "값이 두 곳에 있는 상태를 없앤다"는 수정 목적만 흐려진다 ③ 대체 경로(`MistShrineUseCase.GetRadius()`)가 **같은 커밋에 함께 들어가** 기능 공백 구간이 없다 → §9-2 |
| `MistShrineRangeIndicator._baseDiameter`<br>`SkillAimReticle._baseDiameter`<br>(`[SerializeField] float`) | 앞은 이번 사이클 산출물, **`SkillAimReticle`은 기존 파일**(사용자 승인으로 함께 수정) | **씬에 저장된 값(`1`) 자체가 버그의 원인**이므로, 필드를 남겨 두면 수정이 성립하지 않는다. 필드명을 `_baseDiameterOverride`로 바꾸고 **`[FormerlySerializedAs]`를 일부러 붙이지 않아** 옛 직렬화 키를 버린다 → §9-3 |

> 위 2건에 딸린 부수 정리(에디터 스크립트 `MistShrineSetup_Scene.cs`의 반경 배선 코드 · 전용 헬퍼 `ConnectFloat` · 호출자가 사라진 config 로드 블록)도
> **대상 필드가 없어져 호출자 0이 된 죽은 코드**이므로 같은 근거로 제거했다.

---

## 1. 구현 방침 (핵심 설계 판단)

### 1-1. 물안개 힐은 `_activeTimedEffects`(HoT/DoT 시스템)를 쓰지 않고 **독립 상태 목록 + 매 틱 범위 재수집**으로 만든다

**근거 규칙:** Buildings 규칙 8(**2026-08-10 정정 — 물안개 전용 독립 누적기 + 매 틱 재수집. 시간 지속 효과 목록 사용 금지**) · 규칙 9(아우라 — 범위 이탈 즉시 끊김) · 규칙 13(중첩 금지) · 규칙 14(독립 채널)

**코드 근거(Research.md §3-2 실측):**

| # | 사실 | 결과 |
|:-:|------|------|
| 1 | `TickTimedEffects`의 discrete 분기(`TickInterval > 0`)는 `effect.Kind`를 **검사하지 않고** `ApplyOneDamageTick`으로 직행한다 | `Kind=Heal` + `TickInterval=1f` 레코드를 넣으면 **회복 대상이 매초 피해를 입는다** |
| 2 | 대상 조회가 `_unitSpawn.Units.TryGetValue(effect.TargetId, …)` 뿐이고 `TargetId`가 단일 키다 | **건물은 대상이 될 수 없고**, 유닛 Id ↔ 건물 Id가 **충돌**한다 |
| 3 | 레코드가 `TotalAmount` 분할 모델이다 | **범위 이탈 즉시 끊김(규칙 9)** 을 표현할 수 없다 |
| 4 | `AddOrRefreshTimedEffect`가 `(TargetId, Kind)` 키로 **덮어쓴다** | `Heal` 버킷에 넣으면 **BloomFairy 힐과 상호 소멸**(규칙 14 ⚠️가 경고한 그대로) |

**채택 모델 — 자연회복(`TickNaturalRegen`)과 파도(`TickWaves`)의 선례:**

```
List<ActiveMist>  (물안개 인스턴스 목록, 서버 전용)
   └ 매 틱: 남은 시간 감소 → 1초 격자가 채워지면
              ① 이 물안개 범위 안의 아군 유닛·건물을 그 자리에서 재수집(아우라 = 규칙 9)
              ② 중첩 해소(가장 가까운 물안개만 적용 = 규칙 13)
              ③ 회복 적용 + 텍스트 억제 이벤트 발행
```

- `_activeTimedEffects`를 전혀 쓰지 않으므로 **규칙 14의 독립 채널이 구조적으로 보장**된다(자연회복이 `_regenAccumBlue/_regenBuffer`로 같은 방식을 이미 쓴다).
- 매 틱 재수집이므로 **규칙 9 아우라가 자연히 성립**한다.

> ✅ **규칙 문서 정합 — 해석이 아니라 규칙 자체가 정정되었다 (2026-08-10).**
> Research.md §15-1이 지적한 대로, 규칙 8의 옛 문구 *"규칙 40과 동일한 틱 방식을 재사용"* 은 위 1·2·3 때문에 코드로 성립하지 않았다.
> 사용자 승인 후 **`GameSystemRules_Buildings.md` 규칙 8의 내용을 교체**했다(규칙 번호는 8 그대로).
> - 유지: **회복 주기 1초**
> - 폐기: "규칙 40 DoT 틱 재사용" 서술
> - 신설: **물안개별 독립 누적기 + 매 틱 대상 재수집** 모델 확정, `_activeTimedEffects`에 `Kind=Heal` + `TickInterval>0` 레코드를 넣는 것을 **명시적 금지**(금지 사유를 코드 위치와 함께 규칙에 기록)
>
> 따라서 이 절의 채택 모델은 **규칙 8을 해석한 결과가 아니라 규칙 8이 직접 요구하는 구현**이다.

### 1-2. 중첩 해소는 "대상 기준 최근접 물안개 선택"으로 계산한다

**근거 규칙:** Buildings 규칙 13

물안개가 2개 이상 있을 때 대상별로 **가장 가까운 물안개 1개만** 적용한다.
**거리 완전 동률이면 시전 건물 `Id`가 작은 쪽**을 적용한다 — 순회 순서에 의존하지 않는 **결정적 규칙**이며 서버·클라 판정 분기를 막는다.

구현 형태(의사 흐름):

```
틱마다:
  후보 = 이번 틱에 발화할 물안개들 (1초 격자가 찬 것)
  대상별 최적 = {}
  for mist in 후보 (건물 Id 오름차순으로 순회)          ← 결정성 1차 보장
      for target in 범위 내 아군(유닛+건물)
          d2 = 거리제곱
          기존 = 대상별최적[target]
          if 기존 없음 or d2 < 기존.d2                 ← 엄격 부등호(<)
              대상별최적[target] = (mist, d2)          ← 동률이면 먼저 온(Id 작은) 것 유지
  대상별최적을 순회하며 회복 적용
```

- **건물 Id 오름차순 순회 + 엄격 부등호(`<`)** 조합으로 규칙 13의 동률 규칙이 성립한다.
- 물안개가 1개뿐인 일반 상황에서는 위 로직이 그대로 단순 경로가 된다(추가 비용 무시 가능).

### 1-3. 회복 텍스트는 "3초 누적 1회" 를 **이벤트 플래그로만** 제어한다

**근거 규칙:** Buildings 규칙 15 · UI 규칙 9

기존 메커니즘을 그대로 쓴다(Research.md §3-1·§9-6 실측):
- `EntityHealedEvent.ShowText == false` → `FloatingHpTextSpawner.ShowHeal`이 **213행에서 조기 반환**(텍스트 없음), HP 적용·동기화는 정상 진행.
- 물안개는 **매 1초 틱마다 `showText:false`** 로 발행하고, **표시 주기(임시 3초)가 찰 때만 `showText:true`** 로 발행한다.
- 표시 값은 프로젝트 통일 형식인 **"회복 후 현재 HP"** 다(`ShowHeal` 231행 `string label = $"{evt.CurrentHp}";`).
- **실제로 회복된 대상만** 표시한다 — 회복 전후 `Hp` 차이가 0이면 이벤트 자체를 발행하지 않는다(규칙 5·15).

### 1-4. 수치는 `SpecialAttackConfig`에 넣는다 (`BuildingStatsConfig` 아님)

**근거:** Research.md §12

| 수치 | 저장 위치 | 사유 |
|------|----------|------|
| HP 500 / 건설비 100 | **`BuildingStatsConfig.asset` (이미 존재 — 무변경)** | `buildingType: 6` 항목에 `transcendenceMaxHp: 500`, `transcendenceGoldCost: 100`이 **이미 커밋되어 있다** |
| 회복량 / 지속시간 / 쿨다운 / 반경 / 텍스트 표시 주기 | **`SpecialAttackConfig` 신규 필드 5개** | `BuildingStatsConfig`는 **종족 3분할 구조**라 초월 전용 단일 건물에 낭비이고, 필드 추가 시 Domain `BuildingStats.StatValues`·Getter까지 연쇄 수정이 필요하다. 반면 `SpecialAttackConfig`는 `[Header]` 단위 평면 구조 + **float 원시값 주입 + null 폴백 기본값** 패턴이 그대로 들어맞는다(BloomFairy 힐 2값이 동일 선례) |

---

## 2. 레이어별 신규/수정 파일

> WORKFLOW.md [4] 필수 요건에 따라 **각 항목의 근거 규칙**을 명시한다.
> 표기: `Buildings n` = `GameSystemRules_Buildings.md` MistShrine 물안개 힐 시스템 규칙 n
>       `UI n` = `GameSystemRules_UI.md` MistShrine 패널 UI 규칙 n

### 2-1. Domain (수정 1)

| 파일 | 구분 | 내용 | 근거 규칙 |
|------|:-:|------|------|
| `Scripts/Domain/Building/BuildingData.cs` | 수정 | `public void Heal(int amount)` 추가 — `UnitData.Heal`(200~206행)과 **동일 형태**: `if (!IsAlive) return; if (amount <= 0) return; Hp += amount; if (Hp > MaxHp) Hp = MaxHp;`. **MaxHp 클램프를 도메인 내부에 둔다**(규칙 5가 자동 성립) | Buildings 24, 5 |

> 이 한 메서드가 없으면 `Hp { get; private set; }` 때문에 어떤 레이어에서도 건물을 회복시킬 수 없다.

### 2-2. Application (신규 2 / 수정 2)

| 파일 | 구분 | 내용 | 근거 규칙 |
|------|:-:|------|------|
| `Scripts/Application/UseCases/MistShrineUseCase.cs` | **신규** | 아래 §2-2-1 상세 | Buildings 19·20·21·23·25, 13·14 |
| `Scripts/Application/Interfaces/INetworkMistShrineController.cs` | **신규** | `void RequestActivate(int buildingId, TeamId team);` / `void RequestToggleAuto(int buildingId, TeamId team);` — Presentation이 Netcode를 모르게 하는 추상화(`INetworkSkillController` 선례) | Buildings 22 (Application→Netcode 금지) |
| `Scripts/Application/Interfaces/IGameServices.cs` | 수정 | `MistShrineUseCase GetMistShrineUseCase();` 추가 — Infrastructure가 Bootstrap을 직접 참조하지 않고 UseCase에 접근 | Buildings 22 |
| `Scripts/Application/Events/GameEvents.cs` | 수정 | `EntityHealedEvent` **주석만** 갱신(254행 `현재는 유닛만 사용` / 260행 `false면 건물(현재 미사용)`). **구조 변경 없음** — `IDamageable Entity` + `bool IsUnit`이 이미 건물을 표현한다 | Buildings 15·24 |

#### 2-2-1. `MistShrineUseCase` 상세 설계

**의존성(생성자 주입 — `TowerCombatUseCase` 53~56행 선례와 동일 3종 + 튜닝값):**

```
BuildingPlacementUseCase _buildingPlacement   // 건물 조회·순회
UnitSpawnUseCase         _unitSpawn           // 아군 유닛 순회
IHexCoordinateMapper     _mapper              // HexCoord → 도메인 월드 (Domain→Core 금지 우회)
float _healPerSecond / _mistDuration / _cooldown / _radius / _textInterval   // SpecialAttackConfig에서 float 원시값
```

**상태(전부 `private`, 서버 권위):**

| 필드 | 형태 | 근거 |
|------|------|------|
| `Dictionary<int, bool> _autoMode` | 건물 Id → 자동 여부 **bool 1개** (`AutoTypes` 리스트 파생 방식 미사용) | Buildings 19 |
| `Dictionary<int, float> _cooldownRemaining` | 건물 Id → 남은 쿨다운 | Buildings 21 |
| `Dictionary<int, float> _cooldownTotal` | 건물 Id → 발동 시점 총 쿨다운(오버레이 radial fill 비율용) | Buildings 21 / UI 7 |
| `List<ActiveMist> _activeMists` | 물안개 인스턴스(`SourceBuildingId` / `Team` / `CenterWorld` / `Remaining` / `TickAccum` / `TextAccum`) | Buildings 6·9·10 |
| 재사용 버퍼 `_unitBuffer` / `_buildingBuffer` / `_expiredBuffer` / `_tickKeyBuffer` / `_bestByTarget` | GC 절감 (`SkillActivationUseCase` 226~227행, `_regenBuffer` 117행 선례) | — |

**공개 API:**

| 메서드 | 역할 | 근거 |
|------|------|------|
| `bool Activate(int buildingId)` | 서버 권위 시전. 재검증 순서 ① 건물 존재·`IsAlive` ② `Type == BuildingType.HealShrine` ③ 쿨다운 만료 → 통과 시 `_activeMists`에 추가 + `StartCooldownLocal` (`SkillActivationUseCase.Activate` 94~146행 구조 미러) | Buildings 20·22·6·11 |
| `void SetAutoMode(int buildingId, bool on)` / `bool GetAutoMode(int buildingId)` | 자동 모드 설정·조회 | Buildings 18·19 |
| `bool ToggleAutoMode(int buildingId)` | 토글 후 결과 반환(네트워크 브로드캐스트 값) | Buildings 18 |
| `void StartCooldownLocal(int buildingId, float cooldown)` | 서버 발동 시 + **멀티 클라 로컬 미러**로도 호출(이름·의미 모두 `SkillActivationUseCase.StartCooldownLocal` 178행과 동일) | Buildings 21 |
| `void TickCooldowns(float dt)` | 쿨다운 감소. 만료 키 수집 후 제거(순회 중 변경 회피) — `SkillActivationUseCase.TickCooldowns` 195행 이식 | Buildings 21 |
| `void TickMists(float dt)` | 물안개 진행 + 1초 격자 회복 + 텍스트 주기 + 만료 제거. **서버 전용**(내부 가드 포함) | Buildings 6·8·9·13·15 |
| `void TickAutoCast(float dt)` | 자동 모드 ON이고 쿨다운 0인 MistShrine을 **건물 Id 오름차순**으로 자동 시전 | Buildings 18 |
| `float GetCooldownRemaining(int id)` / `float GetCooldownTotal(int id)` / `bool IsOnCooldown(int id)` | UI 조회 | UI 7 |
| `bool IsMistActive(int buildingId)` | 물안개 지속 여부(UI에서 사용하지는 않음 — UI 규칙 13. 디버그·AI용) | Buildings 6 |
| `void OnShrineDestroyed(int buildingId)` | ① `_activeMists`에서 `SourceBuildingId` 일치분 제거 ② `_autoMode` 제거 ③ `_cooldownRemaining`/`_cooldownTotal` 제거 | Buildings 12·25 |
| `void ClearAll()` | 재경기/맵 전환 시 전체 초기화(`SkillActivationUseCase.ClearAll` 248행 선례) | — |

**내부 헬퍼(§5 조사 — 기존 헬퍼가 전부 `private` + 적 전용이라 자체 구현):**

| 메서드 | 기존 대비 차이 |
|------|--------------|
| `CollectAllyUnitsInRadius(TeamId team, Vector3 center, float r2, List<UnitData> result)` | `CollectEnemyUnitsInRadiusDomain`(1824행)에서 **팀 조건 반전**(`unit.Team != team → continue`) + **`Hp >= MaxHp` 스킵** 추가(규칙 5·15, 자연회복 368행 선례) |
| `CollectAllyBuildingsInRadius(TeamId team, Vector3 center, float r2, List<BuildingData> result)` | `CollectEnemyBuildingsInRadiusDomain`(1842행) 기준 동일 반전. **시전 건물 자신·Castle을 제외하지 않는다**(규칙 4 — 명시 주석 필수) |
| `ApplyHealToUnitEntity` / `ApplyHealToBuildingEntity` | `target.Heal(amount)` 후 `GameEvents.OnEntityHealed.OnNext(new EntityHealedEvent(target, target.Hp, isUnit: true/false, healerId: shrineId, healerIsUnit: **false**, showText: …))`. **`ApplyHealToUnit`(1378행)은 `isUnit:true` 하드코딩이라 재사용 불가** |

**서버 가드:** `TickMists` / `TickAutoCast` / `Activate` 진입부에
`if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return;`
(`TowerCombatUseCase.Tick` 107행과 동일 — 호출부 가드에 더한 **2중 방어**)

### 2-3. Infrastructure (신규 1 / 수정 3)

| 파일 | 구분 | 내용 | 근거 규칙 |
|------|:-:|------|------|
| `Scripts/Infrastructure/Network/NetworkMistShrineController.cs` | **신규** | `NetworkBehaviour, INetworkMistShrineController`. 아래 §2-3-1 | Buildings 22 |
| `Scripts/Infrastructure/Config/SpecialAttackConfig.cs` | 수정 | `[Header("MistShrine 물안개 힐")]` 아래 `_mistHealPerSecond` / `_mistDuration` / `_mistCooldown` / `_mistRadius` / `_mistHealTextInterval` 5개 `SerializeField` + 공개 프로퍼티. **코드 기본값 = §3 임시값** | Buildings 16 |
| `Scripts/Infrastructure/Network/NetworkHealthSync.cs` | 수정 | **건물 힐 동기화 신설 — (b)안 채택**: `OnEntityHealed`의 기존 유닛 조기 반환(146행)은 **그대로 두고**, 그 앞에 건물 분기를 추가해 `SyncBuildingHealClientRpc(buildingId, serverHp, healerId, showText)` 전송. 클라 측 `SyncBuildingHeal(...)`는 `int diff = serverHp - building.Hp; if (diff > 0) building.Heal(diff);` + `showText`면 `OnEntityHealed` 재발행(`SyncUnitHeal` 271~307행 미러). **311행 주석 갱신** | Buildings 24 |
| `Scripts/Infrastructure/Network/NetworkCombatController.cs` | 수정 | `TickCombat` 내 331행(`GetSkillActivationUseCase()?.TickCooldowns`) **바로 옆에** `_services?.GetMistShrineUseCase()?.TickCooldowns(elapsed); … TickMists(elapsed); … TickAutoCast(elapsed);` 추가 | Buildings 21·8·18 |

> **(a)안(기존 `SyncHealClientRpc`에 `isUnit` 추가) 대신 (b)안(전용 RPC 신설)을 채택한 이유:**
> (a)는 피해 경로(`SyncHealthClientRpc`)와 대칭이 되어 형태가 예뻐지지만 **기존 RPC 시그니처를 바꾼다** → 유닛 힐 전체(파도·BloomFairy·자연회복)에 회귀 위험이 생긴다.
> **완성도 우선 원칙(CLAUDE.md 규칙 7)** 은 "더 나은 구조"를 요구하지만, 여기서는 *검증된 유닛 힐 경로 무변경*이 더 큰 가치다.
> (a)안으로의 통합은 **건물 힐이 실기 검증된 이후** 별도 리팩터 작업으로 제안한다.

#### 2-3-1. `NetworkMistShrineController` 상세 (3단 구조)

`NetworkSkillController`(간결한 직접 브로드캐스트)를 본뜬다. `NetworkProductionController`의 "이벤트 → 전체 큐 상태 브로드캐스트" 구조는 **본뜨지 않는다**(자동 상태가 bool 1개뿐 — 규칙 19).

| 단계 | 시전 | 자동 토글 |
|:-:|------|----------|
| ① 래퍼 | `public void RequestActivate(int buildingId, TeamId team)` | `public void RequestToggleAuto(int buildingId, TeamId team)` |
| ② ServerRpc | `[ServerRpc(RequireOwnership = false)] public void ActivateMistServerRpc(int buildingId, int teamIndex, ServerRpcParams rpcParams = default)` | `[ServerRpc(RequireOwnership = false)] public void ToggleAutoServerRpc(int buildingId, int teamIndex, ServerRpcParams rpcParams = default)` |
| ②-1 팀 검증 | `ulong senderClientId = rpcParams.Receive.SenderClientId; TeamId expectedTeam = (senderClientId == 0) ? TeamId.Blue : TeamId.Red;` → 불일치 시 `LogWarning` 후 return. **건물 소유 팀도 함께 검증**(`building.Team != expectedTeam → return`, `NetworkSkillController` 150~154행 선례) | 동일 |
| ③ ClientRpc | 성공 시 `MistActivatedClientRpc(buildingId, cooldownTotal)` — 진입부 `if (IsServer) return;` 후 `StartCooldownLocal`로 **로컬 미러** | `AutoModeChangedClientRpc(buildingId, bool on)` — 진입부 `if (IsServer) return;` 후 `SetAutoMode` |

**서비스 해석:** `private IGameServices ResolveServices() { if (_services == null) _services = GameServicesLocator.Current; return _services; }`
→ **`NetworkSkillController` 99~104행 / `NetworkUpgradeController`와 동일한 지연 재조회.** `OnNetworkSpawn` 1회 캐시 방식(`NetworkProductionController`)은 **채택하지 않는다**(스폰 레이스 — MEMORY 교훈).

**RPC 명명:** 메서드명은 반드시 `ServerRpc` / `ClientRpc` 접미사(NGO 제약).

### 2-4. Presentation (신규 2 / 수정 2)

| 파일 | 구분 | 내용 | 근거 규칙 |
|------|:-:|------|------|
| `Scripts/Presentation/UI/MistShrinePanelUI.cs` | **신규** | 아래 §2-4-1 | UI 1~14 |
| `Scripts/Presentation/Effects/MistShrineRangeIndicator.cs` | **신규** | 지면 데칼 반투명 원(채움 + 외곽선). `SkillAimReticle`(43~93행)의 **fill + ring 2겹 SpriteRenderer + `_overlayMaterial`** 구조를 본뜨되 dot 없음·색상 별도. API `Show(Vector3 worldPos, float radius)` / `Hide()`. **`ZTest Always` 금지** | UI 8·12 / Buildings 27 |
| `Scripts/Presentation/Input/InputHandler.cs` | 수정 | ① `private MistShrinePanelUI _mistShrinePanelUI;` 필드 ② `Initialize(...)` 말미 파라미터 추가(**기본값 `= null`** — 스킬 패널 선례) ③ 분기에 `else if (buildingAtPos.Type == BuildingType.HealShrine && _mistShrinePanelUI != null) _mistShrinePanelUI.Show(buildingAtPos);` 를 **스킬 패널 갈래 뒤 · 액션 패널 갈래 앞**에 삽입 ④ **`ClosedFrame` 가드 보강 — 아래 §2-4-2** | UI 1 · 공통 UI 8·9 / TDD "PopupClosedFrame" |
| `Scripts/Presentation/UI/FloatingHpTextSpawner.cs` | 수정 | **216행 주석만** 갱신(`현재 힐 대상은 유닛만` → 건물도 대상). 실행 코드 무변경 — 건물 분기가 **이미 존재** | Buildings 15 / UI 9 |

#### 2-4-1. `MistShrinePanelUI : BuildingPanelBase` 상세

| 항목 | 내용 | 근거 |
|------|------|------|
| 상속 | `BuildingPanelBase` → 헤더·닫기[X]·철거+환불·배경 탭 닫기·`IGameUI`·**건물 파괴 시 자동 닫힘** 자동 획득 | UI 1·3·4 |
| `SerializeField` | `List<Button> _allSlotButtons`(9) / `Button _useButton`(슬롯 1) / `SkillCooldownOverlay _useCooldownOverlay` / `Image _autoBorderOverlay`(테두리 회전, **오브젝트 1개만**) / `MistShrineRangeIndicator _rangeIndicator` | UI 10·11·7·14·8 |
| 슬롯 관리 | `BuildSlotCanvasGroups()` — `BuildingActionPanelUI`(101행)/`BuildingSkillPanelUI`(123행)와 **동일 패턴**. CanvasGroup 없으면 자동 부착, 기본 `alpha=0`. **`SetActive(false)` 금지**(GridLayout 정렬 붕괴) | UI 11 |
| 슬롯 배치 | 1=사용, 2~5 숨김, 6=철거(베이스), 7~9 숨김 | UI 10 |
| `Initialize(...)` | `InitializeBase(buildingPlacement, resource, networkBuildingController)` 호출 후 `_mistShrine`(UseCase) / `_networkMistShrine`(`INetworkMistShrineController`, 싱글은 null) 주입 + `BuildSlotCanvasGroups()` | UI 1 |
| 조작 배선 | `EventTrigger`를 **코드에서 동적 부착**(`ProductionPanelUI` 351~363행과 동일 — 프리팹 사전 배선 불필요). `PointerDown`/`PointerUp` 엔트리 등록 | UI 5 |
| 탭/롱프레스 | `const float LongPressThreshold = 0.5f;` + `_isPointerDown` / `_longPressTriggered` / `_pointerDownTime`. `Update`에서 `Time.unscaledTime - _pointerDownTime >= 0.5f` → 롱프레스. `PointerUp`에서 `!_longPressTriggered`면 탭 | UI 5 |
| 자동 중 탭 = 해제 | `if (_mistShrine.GetAutoMode(id)) { HandleToggleAuto(); return; }` — `ProductionPanelUI` 440~443행과 동일 분기 형태 | UI 5 |
| 멀티/싱글 분기 | `if (_networkMistShrine != null && NetworkContext.IsNetworkActive) _networkMistShrine.RequestActivate(...) else _mistShrine.Activate(...)` (`ProductionPanelUI` 528~529행 형태) | Buildings 22 |
| 쿨다운 표시 | `Update()`에서 `if (!IsOpen \|\| _currentBuilding == null) return;` 후 `_useCooldownOverlay.SetCooldown(GetCooldownRemaining(id), GetCooldownTotal(id))` — `BuildingSkillPanelUI.Update` 269~289행 구조. 오버레이가 `blocksRaycasts`로 **쿨다운 중 입력을 자동 차단** | UI 7·13 |
| 자동 표시 | `_autoBorderOverlay` **단일 경로로만** on/off. **도트 인디케이터 두지 않음**, `ProductionPanelUI`의 중복 배선 복제 금지 | UI 6·14 |
| 범위 표시 | `OnShow(building)`에서 **아군일 때만** `_rangeIndicator.Show(worldPos, radius)`, `OnBeforeClose()`에서 `_rangeIndicator.Hide()`. 좌표는 **뷰 좌표로 변환**해 배치(`ViewConverter` — Red팀 반전) | UI 8·12 |
| 아이콘 | **미제작** → 임시 텍스트 라벨(스킬 패널과 동일 취급) | UI 15 |
| ⚠️ 금지 | **자체 `OnDestroy()` 선언 금지.** 베이스 정리가 은닉된다. 구독이 필요하면 `.AddTo(this)` | Research §9-1 / MEMORY 교훈 |

> **적 MistShrine 처리:** `InputHandler`는 262행 `isMine && isAlive`에서 이미 적 건물을 차단하므로 **적 건물은 패널 자체가 열리지 않는다**
> → UI 규칙 8의 "적 MistShrine은 범위 미표시"가 **추가 코드 없이 성립**한다.

#### 2-4-2. `ClosedFrame` 가드 보강 — 신규 1종 + **기존 결손 2종 함께 수정**

> **범위 편입 사유(사용자 승인, 2026-08-10):** 아래 ②는 **이번 MistShrine 작업과 무관한 기존 결손**이지만,
> ①에서 같은 가드를 손대는 김에 함께 처리하기로 사용자가 승인했다.
> 나중에 *"MistShrine 작업이 왜 연구·스킬 패널을 건드렸나"* 라는 혼동이 없도록 이 절에 사유를 명시해 둔다.
> **이 항목은 §8(범위 밖)에서 제외되어 이번 범위 안으로 들어왔다.**

**현재 코드(`InputHandler.cs` 230~232행) — 실측:**

```csharp
if ((_buildingUI != null && _buildingUI.ClosedFrame == frame)
    || (_productionUI != null && _productionUI.ClosedFrame == frame)
    || (_actionPanelUI != null && _actionPanelUI.ClosedFrame == frame))
    return;
```

| # | 수정 내용 | 성격 |
|:-:|----------|------|
| ① | **신규** `_mistShrinePanelUI.ClosedFrame == frame` 항을 가드에 추가 | 이번 작업 산출물 |
| ② | **기존 결손 보정** — `_researchPanelUI.ClosedFrame == frame` · `_skillPanelUI.ClosedFrame == frame` 두 항을 함께 추가 | **이번 작업 이전부터 있던 누락** |

**②가 결손인 근거(코드 실측):** 두 패널은 필드·주입·호출이 모두 갖춰져 있는데 **가드에서만 빠져 있다.**

| 패널 | 필드 선언 | `Initialize` 주입 | 패널 열기 호출 | `ClosedFrame` 가드 |
|------|:-:|:-:|:-:|:-:|
| `_researchPanelUI` | 60행 | 113행 | 277행 `Open(...)` | **누락** |
| `_skillPanelUI` | 63행 | 115행 | 286행 `Show(...)` | **누락** |

`ClosedFrame` 자체는 `BuildingPanelBase`가 공통 제공하는 프로퍼티(`public int ClosedFrame { get; protected set; } = -1;` — 134행)이므로,
연구·스킬 패널도 **값은 이미 정상적으로 기록하고 있다.** 읽는 쪽(`InputHandler`)만 빠져 있어 무효화되어 있었다.

**증상:** 연구 패널·스킬 패널을 배경 탭으로 닫은 **그 클릭이 같은 프레임에 타일 선택으로 흘러갈 수 있다.**

**근거 규칙:**
- **공통 UI 규칙 8·9** (`GameSystemRules_UI.md`) — 건물 패널은 **Popup 타입**이므로 배경 탭으로 닫힌다. 그 "닫기 위한 탭"이 뒤쪽 게임 입력까지 함께 발동시키면 규칙 9가 의도한 닫기 동작이 아니게 된다.
- **`TechnicalDesignDocument.md` "PopupClosedFrame (팝업 닫힘 프레임 보호)"** — 이 프로젝트가 위 문제를 해결하기로 정한 **표준 패턴**의 단일 소스. *"각 팝업 UI가 `ClosedFrame`에 닫힌 프레임 번호를 기록하고, `InputHandler`가 같은 프레임의 클릭을 무시한다"* → **모든 팝업이 가드에 포함되어야 패턴이 성립**한다.
- **MistShrine UI 규칙 4** — MistShrine 패널의 닫기 규칙은 공통 팝업 규칙(8·9)을 따른다 → 신규 패널도 같은 가드가 필요하다.

### 2-5. Bootstrap (수정 3)

| 파일 | 구분 | 내용 | 근거 |
|------|:-:|------|------|
| `Scripts/Bootstrap/GameBootstrapper.cs` | 수정 | ① `[SerializeField] private MistShrinePanelUI _mistShrinePanelUI;` ② `[SerializeField] private Hexiege.Infrastructure.NetworkMistShrineController _networkMistShrineController;` ③ `private MistShrineUseCase _mistShrine;` ④ `public MistShrineUseCase GetMistShrineUseCase() => _mistShrine;`(`IGameServices` 구현) ⑤ `Update()` 틱 3종 — §2-6 | 조합 루트 단일화 |
| `Scripts/Bootstrap/GameBootstrapper.Setup.cs` | 수정 | ① `SpecialAttackConfig`에서 float 5값 추출(283~313행 형태, **null 폴백 기본값 명시**) ② `_mistShrine = new MistShrineUseCase(...)` — `_skillActivation` 생성부(363행) 인근 ③ `SetupBuildings()`에 패널 초기화 블록(605~640행 형태, `isNetworkMode ? 컨트롤러 : null`) ④ `SetupInput()` `_inputHandler.Initialize(...)`에 패널 인자 추가 ⑤ **MistShrine 파괴 구독** — 416~422행 연구소 패턴 복제: `_mistShrineDestroyedSub?.Dispose(); … Subscribe(e => { if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer) return; if (e.Building == null \|\| e.Building.Type != BuildingType.HealShrine) return; _mistShrine?.OnShrineDestroyed(e.Building.Id); });` | Buildings 12·25·16 |
| `Scripts/Bootstrap/GameBootstrapper.Map.cs` | 수정 | `_uiManager.Register(_mistShrinePanelUI);` (47~60행 블록) — 게임 시작/종료 시 자동 닫힘 | UI 4 |

### 2-6. 틱 배선 (이중 틱 금지 — 가장 사고가 잦은 지점)

| 틱 | 싱글 | 멀티 서버(호스트) | 멀티 순수 클라 |
|------|------|------------------|--------------|
| `TickMists` (회복) | `GameBootstrapper.Update` — **`!IsNetworkMode()` 블록 내**(489·491행 옆) | `NetworkCombatController.TickCombat` (331행 옆) | **돌지 않는다** (HP는 `NetworkHealthSync`로 수신) |
| `TickAutoCast` (자동 시전) | 동일 | 동일 | **돌지 않는다** |
| `TickCooldowns` (쿨다운) | `GameBootstrapper.Update` — **`(!IsNetworkMode() \|\| !NetworkContext.IsNetworkServer)` 가드**(506~510행과 동일) | `NetworkCombatController.TickCombat` | **돈다 (표시용 로컬 미러)** |

> 쿨다운만 가드 형태가 다르다. 순수 클라의 오버레이가 서버와 같은 남은 시간을 표시해야 하기 때문이며,
> 이는 `_skillActivation.TickCooldowns`가 이미 쓰고 있는 검증된 형태다(Research.md §6-2).
> `MistShrineUseCase` 내부에도 `NetworkContext` 가드를 두어 **호출부 실수 시에도 이중 회복이 발생하지 않게** 한다(`TowerCombatUseCase` 107행 선례).

---

## 3. 임시값 (전부 밸런싱 미확정)

**근거 규칙:** Buildings 16(미확정 표) · UI 9(텍스트 주기 임시 3초)

| 항목 | 임시값 | 출처 / 상태 |
|------|-------|-----------|
| 건물 HP | **500** | `StatsReference.md` 특수 건물 표(기존값). **`BuildingStatsConfig.asset`에 이미 반영됨 — 이번에 건드리지 않음** |
| 건설 비용 | **100** | 동일. **이미 반영됨 — 건드리지 않음** |
| 회복량 | **10 HP/s** | `StatsReference.md` 주석의 "이전 표기였던 10 HP/s(범위 3)" — ×10 적용값. **재설계 이전 수치이므로 재검토 대상** |
| 범위 반경 | **3 타일** | 동일. **재설계 이전 수치** |
| 물안개 지속시간 | **10초** | ⚠️ **재설계로 신규 발생한 수치라 참고값이 전혀 없다.** 코디네이터 제안값이며 사용자 이의 없음 |
| 쿨다운 | **20초** | ⚠️ **동일하게 근거 없는 신규 수치.** 규칙 7(지속 < 쿨다운) 충족, 다운타임 10초 |
| 회복 텍스트 표시 주기 | **3초** | UI 규칙 9에 이미 "임시 3초"로 기재됨 |

> ⚠️ **위 7개는 전부 "임시값 — 밸런싱 미확정"이다.** 특히 **물안개 지속시간(10초)과 쿨다운(20초)은 어떤 문서에도 근거가 없는 신규 제안값**이며,
> 다른 값들처럼 "이전 수치를 이어받은 것"조차 아니다. 플레이테스트 전까지 어떤 문서에서도 **확정으로 인용해서는 안 된다.**

### 3-1. 반영 위치와 향후 변경 지점

| 수치 | 코드 기본값(폴백) | Inspector 값(우선) |
|------|-----------------|------------------|
| HP 500 / 건설비 100 | `BuildingStats` 폴백 | **`Resources/Config/BuildingStatsConfig.asset` → `buildingType: 6`** |
| 회복량 / 지속 / 쿨다운 / 반경 / 텍스트 주기 | `SpecialAttackConfig.cs`의 `= 10f / = 10f / = 20f / = 3f / = 3f` | **`Resources/Config/SpecialAttackConfig.asset`** |

> **밸런싱 확정 시 바꿀 곳은 `SpecialAttackConfig.asset` 한 파일뿐이다.**
> Inspector 값이 코드 기본값보다 우선하는 구조(`_specialAttackConfig != null ? … : 폴백`)이므로 **코드 재작성·재빌드가 필요 없다.**
> 이것이 "임시값으로 먼저 구현" 방침의 근거다.

---

## 4. 구현 순서 (각 단계에서 컴파일이 깨지지 않도록 분할)

| 단계 | 내용 | 완료 시 상태 |
|:-:|------|------------|
| **S1. Domain** | `BuildingData.Heal(int)` 추가 | 단독 컴파일 OK. 호출자 없음 → 기존 동작 무영향 |
| **S2. Config** | `SpecialAttackConfig`에 필드 5개 + 프로퍼티 추가 | 컴파일 OK. 에셋은 기본값(폴백)으로 동작 |
| **S3. Application 코어** | `MistShrineUseCase` 신규(상태·`Activate`·`TickCooldowns`·`TickMists`·`TickAutoCast`·아군 수집 헬퍼·`OnShrineDestroyed`). `IGameServices`에 Getter 추가 | 컴파일 OK. **아직 아무도 호출하지 않음** |
| **S4. Bootstrap 배선(싱글)** | UseCase 생성·주입, `Update` 틱 3종, `OnBuildingDied` 구독 | **싱글에서 로직이 실제로 돈다.** UI가 없어 시전은 불가 → 다음 단계까지 육안 확인 불가 |
| **S5. Presentation UI** | `MistShrineRangeIndicator` → `MistShrinePanelUI` → `InputHandler` 분기 + **`ClosedFrame` 가드 3항 보강**(신규 1 + 기존 결손 2 — §2-4-2) → `GameBootstrapper` 패널 초기화·`UIManager.Register` | 프리팹 배선 전이라 **패널 미배선 → 액션 패널로 폴백**(안전). 코드는 완성. **가드 보강분은 MistShrine 프리팹과 무관하게 이 단계에서 즉시 유효**하므로 연구·스킬 패널 회귀 확인(R9-1)을 이 시점에 함께 할 수 있다 |
| **S6. 에디터 스크립트 + 사용자 실행** | §5의 1회성 스크립트 작성 → **사용자에게 실행 요청** → 확인 | **싱글에서 전체 동작 확인 가능**(연출 제외) |
| **S7. Infrastructure 네트워크** | `INetworkMistShrineController` → `NetworkMistShrineController` → `NetworkHealthSync` 건물 힐 → `NetworkCombatController` 틱 → Bootstrap 멀티 배선 | 멀티 동작. 씬 오브젝트 배치 필요(§5) |
| **S8. 주석 정정** | `FloatingHpTextSpawner` 216행 · `NetworkHealthSync` 311행 · `GameEvents` 254·260행 | 문서–코드 정합 |

**의존 방향 근거:** Domain ← Application ← Infrastructure / Presentation ← Bootstrap.
S3이 S1·S2에 의존하고, S5가 S3에 의존하며, S7이 S3·S5에 의존한다.
**S4에서 싱글이 먼저 동작하도록 배치**해 멀티 도입 전에 로직을 검증할 수 있게 한다.

---

## 5. Inspector / 에디터 작업 (WORKFLOW.md [5-2])

Inspector 수동 작업이 필요한 항목은 **1회성 Editor 스크립트(`Hexiege/…` 메뉴)로 자동화**한 뒤 사용자에게 실행을 요청한다.
선례: `Assets/Editor/Setup/SkillSetup_Scene.cs` (메뉴 `Hexiege/Skill/2. Setup Scene (Panel, Aim, Network)`), `CreateSpecialAttackConfigAsset.cs`.

| # | 항목 | 자동화 방식 | 근거 |
|:-:|------|-----------|------|
| E1 | **MistShrine 패널 프리팹 생성·배치** | 기존 건물 패널(ActionPanel/ResearchPanel) 골격을 복제해 3×3 GridLayout 9슬롯 생성. Canvas 계층·SortingOrder는 `GameSystemRules_CanvasSortingOrder.md` 준수 | UI 1·10 |
| E2 | **베이스 `SerializeField` 배선** | `_popup`(AnimatedPanel) / `_headerText` / `_cancelButton` / `_demolishButton` / `_demolishRefundText` / `_colorConfig` — **전부 `protected`라 자식 Inspector에 노출됨** | UI 1·3 |
| E3 | **사용 버튼 + 쿨다운 오버레이 배선** | `_useButton`(슬롯 1), `SkillCooldownOverlay` 부착 + `_fillImage`(Image Type=Filled / **Radial 360 / Fill Origin=Top / Clockwise**) · `_remainingText`(TMP) · `_canvasGroup` | UI 7 |
| E4 | **자동 모드 테두리 오버레이 배선** | 생산 패널의 **테두리 회전 머티리얼 자산 재사용**. **오브젝트는 하나만** 두고 단일 경로 제어 — `ProductionPanelUI`의 GameObject/Image 이중 배선 **복제 금지** | UI 6·14 |
| E5 | **범위 표시 오브젝트 + 머티리얼** | `SkillSetup_Scene.EnsureOverlayMaterial()`(489~511행) 패턴 그대로: `Shader.Find("Hexiege/SkillAimOverlay")` → 없으면 경고 후 null 폴백, 있으면 `Assets/_Project/Materials/` 아래 머티리얼 **멱등 생성**. fill/ring 2겹 SpriteRenderer 부착 후 `MistShrineRangeIndicator`에 배선. **`ZTest Always` 금지** | UI 8·12 / Buildings 27 |
| E6 | **`NetworkMistShrineController` 씬 오브젝트** | 빈 GameObject + `NetworkObject` + 스크립트 부착, NetworkManager 씬 오브젝트(자동 스폰)로 설정 (`NetworkSkillController` 배치 주석과 동일 절차) | Buildings 22 |
| E7 | **`GameBootstrapper` 슬롯 연결** | `_mistShrinePanelUI` / `_networkMistShrineController` 두 필드 배선 | 조합 루트 |
| E8 | **`SpecialAttackConfig.asset` 값 반영** | §3 임시값 5개를 기존 에셋에 **추가 기록**(기존 값 무변경). 밸런싱 확정 시 **이 에셋만 재편집** | Buildings 16 |

> **롱프레스 `EventTrigger`는 에디터 작업 대상이 아니다** — `ProductionPanelUI` 351행처럼 **코드가 런타임에 자동 부착**한다(Research.md §9-3).
> 스크립트는 **멱등**(재실행 안전)하게 작성하고, 실행 완료 확인 후 삭제해도 무방하다.

---

## 6. 위험 요소

| # | 위험 | 근거 | 대응 |
|:-:|------|------|------|
| **R1** | **힐 채널 충돌** — 물안개 힐을 `TimedEffectKind.Heal` 버킷에 넣으면 `AddOrRefreshTimedEffect`가 `(TargetId, Heal)` 키로 **덮어써** BloomFairy 힐·자연회복 중 하나가 소멸 | Research §4, `UnitCombatUseCase` 1869~1910행 | **`_activeTimedEffects`를 아예 쓰지 않는다**(§1-1). 독립 `List<ActiveMist>` — 자연회복(`_regenAccum*`)과 동일한 구조적 분리. 규칙 14 만족 |
| **R2** | **discrete 틱 경로 오용 → 회복 대상이 피해를 입음** | `TickDiscreteDamageEffect`는 `Kind`를 검사하지 않고 `ApplyOneDamageTick` 호출 (Research §3-2, `UnitCombatUseCase` 1943~1949행) | 같은 대응(R1). **규칙 8이 2026-08-10 정정되어 이 경로 사용을 규칙 차원에서 금지**했다(금지 사유가 규칙 8-2 표에 코드 위치와 함께 기록됨) → 구현자가 규칙만 읽어도 함정을 피할 수 있다 |
| **R3** | **이중 틱** — 호스트에서 `GameBootstrapper.Update`와 `NetworkCombatController` 양쪽이 돌면 **회복량 2배** | 과거 유닛 쿨다운에서 유사 사고 이력 | §2-6 표 준수 + **UseCase 내부 `NetworkContext` 가드**(2중 방어, `TowerCombatUseCase` 107행 선례). 쿨다운만 클라 미러 허용 |
| **R4** | **거리 동률 시 서버·클라 판정 분기** → 멀티에서 회복 대상이 갈림 | Buildings 13 | **건물 Id 오름차순 순회 + 엄격 부등호(`<`)** 로 결정성 확보(§1-2). 회복 적용 자체는 서버 전용이라 실제 분기 위험은 낮지만 규칙을 코드로 못박는다 |
| **R5** | **`OnNetworkSpawn` 서비스 스폰 레이스** — `_services`가 null로 굳어 RPC가 조용히 무시됨 | 연구소 강화 실사고(2026-07-31), `NetworkUpgradeController.ResolveServices()` | 신규 컨트롤러는 **`ResolveServices()` 지연 재조회**를 처음부터 채택. `OnNetworkSpawn` 1회 캐시 방식 금지 |
| **R6** | **베이스 `OnDestroy` 은닉 회귀** — 패널이 자체 `OnDestroy`를 선언하면 `BuildingPanelBase`의 정리가 실행되지 않음 | `BuildingPanelBase` 112~115행 주석 | `MistShrinePanelUI`에 **`OnDestroy` 선언 금지**. 구독은 `.AddTo(this)` |
| **R7** | **건물 회복 경로 신설이 기존 전투/동기화에 회귀를 부름** — `BuildingData.Hp` 증가는 프로젝트 최초 | Research §2·§8 | ① `Heal`은 **`TakeDamage`와 대칭인 순수 추가**(기존 경로 무변경) ② 멀티 동기화는 **(b)안 전용 RPC 신설**로 유닛 힐 경로를 한 줄도 건드리지 않음 ③ `SyncBuildingHealth`(피해)와 `SyncBuildingHeal`(회복)이 **서로 다른 RPC**라 부호 오분류 불가 |
| **R8** | **UI 패널 미배선 상태에서의 동작** — 프리팹 생성 전에 코드만 머지되면 클릭 시 아무 반응 없음 | — | `InputHandler` 분기에 `&& _mistShrinePanelUI != null` 가드 → **기존 공용 액션 패널로 폴백**(연구·스킬 패널과 동일 안전망) |
| **R9** | **`ClosedFrame` 가드 누락 재발** — 패널을 배경 탭으로 닫은 프레임의 클릭이 타일 선택으로 흘러감 | `InputHandler` 230~232행이 이미 연구·스킬 패널을 누락 (Research §15-2) | **신규 MistShrine 패널 + 기존 누락 2종(연구·스킬)을 모두 가드에 포함**한다(§2-4-2 — 사용자 승인으로 이번 범위 안). 가드는 `!= null` 동반이라 미배선 패널은 무시된다 |
| **R9-1** | **가드 확대에 따른 회귀** — 연구·스킬 패널이 지금까지 가드 밖에 있었으므로, **"패널을 닫은 직후 같은 프레임에 타일이 선택되던 동작"에 의존한 흐름이 있었다면** 그 동작이 사라진다 | 이번에 처음 가드에 들어가는 2종이라 기존 동작 변화가 발생하는 유일한 지점 | ① 두 패널의 닫기 경로를 **정상 동작(의도된 UX)이 아니라 결손으로 판정**한 근거는 §2-4-2(공통 UI 규칙 8·9 + TDD `PopupClosedFrame` 패턴)다 ② **실기 확인 항목**: 연구 패널·스킬 패널을 배경 탭/[X]/철거로 닫은 직후 **패널 뒤 타일이 선택되지 않는지**, 그리고 **닫기 다음 프레임의 정상 타일 클릭은 여전히 동작하는지**(가드가 1프레임만 막는지)를 각각 확인한다 ③ 회귀 발견 시 **신규 MistShrine 항만 남기고 기존 2항을 되돌리는 것**이 최소 롤백 경로다(항 단위로 독립) |
| **R10** | **연출 부재로 인한 오판** — VFX가 없어 "동작 안 함"으로 오인 | 물안개 VFX·아이콘 미제작 | 회복 텍스트(3초 주기)와 HP 막대로 검증한다는 점을 사전 공유. 범위 원은 표시되므로 범위 판정은 육안 확인 가능 |
| **R11** | **멀티 서버 틱 해상도 50ms** — 물안개 소멸 시점이 싱글과 미세하게 다름 | `NetworkCombatController.Update` 245~265행 | 1초 격자 대비 무시 가능. 기록만 남기고 대응하지 않음 |
| **R12** | **자동 모드 상태가 서버에만 있어 클라 UI가 어긋남** | 규칙 19가 bool 1개를 요구 | 토글 성공 시 `AutoModeChangedClientRpc`로 **양 클라 브로드캐스트**(진입부 `if (IsServer) return;`로 호스트 이중 적용 방지) |
| **R13** | **`SkillAimReticle` 조준원 크기가 커진다** — §9-3의 기준 지름 자동 산출이 조준원에도 적용되어, **지금까지 착탄 반경보다 작게 그려지던 원이 실제 반경 크기로 확대**된다. MistShrine과 무관한 **기존 스킬 UX가 눈에 띄게 바뀌는 유일한 지점** | 기존 값 `_baseDiameter = 1`이 실제 스프라이트 월드 지름과 달랐음(§9-3) | ① 표시/숨김 흐름과 호출 시그니처는 **무변경**이라 로직 회귀 경로는 없다 ② **실기 재확인 필수** — 스킬 조준 시 원의 크기가 실제 착탄 범위와 일치하는지, 화면을 과도하게 덮지 않는지 확인(§10 체크리스트) ③ 크기가 과하다는 판단이 나오면 **코드 수정 없이 `_baseDiameterOverride`(양수)로 조정** 가능 — 되돌림 비용이 낮다 |
| **R14** | **컴파일 미검증** — 보완 수정 3건은 **이 작업 환경에 Unity가 없어** 정적 작성만 되었고 컴파일·플레이 검증을 거치지 않았다 | 환경 제약 | 사용자 에디터에서 **컴파일 오류 0건**을 먼저 확인한 뒤 실기 테스트를 진행한다(§10 체크리스트). 특히 §9-2에서 `_rangeRadius`를 참조하던 **에디터 스크립트 배선 코드가 함께 제거**되었으므로, `Assets/Editor/` 쪽 컴파일도 함께 확인한다 |

---

## 7. 아키텍처 제약 매핑

| 제약(MEMORY) | 이번 작업 적용 |
|------|--------------|
| **Domain → Core 참조 금지** | `MistShrineUseCase`는 `IHexCoordinateMapper.HexToWorld`로 좌표를 얻는다(`UnitCombatUseCase` 1831행과 동일). `BuildingData.Heal`은 순수 C#(Unity 의존 0) |
| **Application → Unity.Netcode 직접 참조 금지** | `MistShrineUseCase`는 Netcode를 모른다. 서버 판정 가드는 `NetworkContext` 정적 홀더로만 수행 |
| **Application → Infrastructure 역참조 금지** | Presentation은 `INetworkMistShrineController`(Application 인터페이스)만 알고, 구현은 Infrastructure(`NetworkMistShrineController`). `INetworkSkillController` 선례 |
| **NetworkBehaviour는 Infrastructure에만** | `NetworkMistShrineController`만 `NetworkBehaviour`. 패널·UseCase는 아님 |
| **NGO RPC 메서드명** | `ActivateMistServerRpc` / `ToggleAutoServerRpc` / `MistActivatedClientRpc` / `AutoModeChangedClientRpc` / `SyncBuildingHealClientRpc` — 전부 접미사 준수 |
| **`GameBootstrapper`가 유일한 조합 루트** | UseCase 생성·패널 초기화·입력 배선·`UIManager.Register`·파괴 구독 전부 Bootstrap. 다른 곳에서 직접 주입 금지 |
| **서버 권위** | 시전 판정·대상 수집·회복 적용·쿨다운·자동 시전은 **서버에서만**. 클라는 쿨다운 미러·자동 상태 미러·HP 수신만 |
| **Inspector 값이 코드 기본값보다 우선** | `SpecialAttackConfig` null 폴백 패턴 채택 → 밸런싱은 `.asset`만 교체 |
| **`UIManager.Instance?.` null-safe** | 베이스가 이미 `UIManager.Instance?.ShowBlockingOverlay(Close)` 형태로 처리(207·245행). 패널이 별도 호출하지 않음 |
| **좌표계 XZ 평면 / ViewConverter** | 회복 판정은 **도메인 월드 좌표**, 범위 표시 오브젝트 배치는 **뷰 좌표**(Red팀 반전). 두 좌표를 섞지 않는다 |

---

## 8. 이번 범위 밖 (CLAUDE.md 규칙 6)

| 항목 | 사유 / 처리 |
|------|-----------|
| **물안개 지속 VFX 제작** | 미제작. 등록 VFX는 `vfx_mistshrine_destroy` / `vfx_mistshrine_upgrade` 뿐(Buildings 26). 확보 후 별도 작업, 사운드 규칙 15(VFX+SFX 쌍) 동반 |
| **사용(시전) 버튼 아이콘 제작** | 미제작(UI 15). 임시 텍스트 라벨 사용 |
| **밸런싱 수치 확정** | 미확정. game-design-lead 협의 후 `SpecialAttackConfig.asset`만 교체 |
| **`ProductionPanelUI` 자동 인디케이터 중복 배선 정리** | 별도 작업 예정(UI 규칙 14 각주). MistShrine은 복제만 하지 않는다 |
| **`NetworkHealthSync` 힐 RPC 통합((a)안)** | 건물 힐 실기 검증 후 리팩터로 제안 |
| **AI의 MistShrine 사용** | `GameSystemRules_AI*`의 MistShrine 항목 유무 미조사(Research §16). 별도 작업 |
| **건물 방어 트랙 / MistShrine 업그레이드** | 기존 보류 항목. 무관 |
| **`Testcase.md` 작성** | **작성하지 않는다.** WORKFLOW.md [5-1] 및 절대 금지 사항 — **사용자 명시 지시가 있을 때만** |

> **이 표에서 빠진 항목 3건 (2026-08-10 · 2026-08-11 갱신) — 추적 누락이 아니라 처리 완료·범위 편입이다.**
>
> | 이전에 "범위 밖"이던 항목 | 현재 상태 |
> |------|----------|
> | `InputHandler` `ClosedFrame` 가드의 연구·스킬 패널 누락 수정 | **이번 범위 안으로 편입**(사용자 승인) → §2-4-2 · R9 · R9-1 |
> | 규칙 8 문구 보강 | **처리 완료** — `GameSystemRules_Buildings.md` 규칙 8 내용 교체(규칙 번호 유지). 이 Plan의 §1-1이 정정된 규칙을 인용하도록 갱신됨 |
> | `SkillAimReticle`의 `Awake` 자기 비활성화 함정 · 기준 지름 오류 (**MistShrine과 무관한 기존 버그 2종**) | **2026-08-11 범위 편입**(사용자 승인) — `MistShrineRangeIndicator`가 이 컴포넌트를 본떠 만들어져 **같은 함정·같은 계산식을 공유**하므로, 한쪽만 고치면 나머지가 남는다 → §9-1 · §9-3 · R13 |

---

## 9. 구현 후 보완 수정 (2026-08-11 추가 — 사용자 실기 테스트 중 발생)

> **이 절이 왜 있는가 (자연어 설명 — 기술 용어 없이)**
>
> 계획대로 구현을 마치고(구현 커밋 `be2b396`, 에디터 셋업 스크립트 `b0b663c`),
> 사용자가 실제로 게임을 돌려 보는 과정에서 **계획 단계에서는 보이지 않던 문제 세 가지**가 드러났습니다.
>
> 세 가지 모두 **기능을 더 넣는 수정이 아닙니다.**
> ① 만들어 둔 범위 원이 **화면에 아예 안 보이던 것**, ② 화면에 보이는 원 크기와 **실제 회복 범위가 따로 놀 수 있던 것**,
> ③ 원이 **실제 범위보다 작게 그려지던 것** — 즉 **"보이는 것과 실제가 어긋나는 문제"를 맞추는 수정**입니다.
>
> 나중에 *"계획에 없던 코드가 왜 들어갔지?"* 라는 의문이 생기지 않도록, 세 수정의 **문제 → 해결 → 근거 규칙**을 여기에 남깁니다.

| # | 커밋 | 한 줄 요약 | 대상 파일 |
|:-:|------|-----------|----------|
| §9-1 | `68ab380` | 비활성 상태로 저장된 오브젝트의 **`Awake` 자기 비활성화 함정** 해소 | `Presentation/Effects/MistShrineRangeIndicator.cs` · `Presentation/Effects/SkillAimReticle.cs` |
| §9-2 | `68ab380` | **범위 반경 값 이중화 제거** (씬 필드 → UseCase 단일 소스) | `Application/UseCases/MistShrineUseCase.cs` · `Presentation/UI/MistShrinePanelUI.cs` · `Assets/Editor/Setup/MistShrineSetup_Scene.cs` |
| §9-3 | `df6909d` | **원 기준 지름을 스프라이트에서 자동 산출** | `Presentation/Effects/MistShrineRangeIndicator.cs` · `Presentation/Effects/SkillAimReticle.cs` |

---

### 9-1. `Awake` 자기 비활성화 함정 (커밋 `68ab380`)

**증상:** 아군 MistShrine 패널을 열어도 **회복 범위 원이 영영 보이지 않았다.**

**원인:** 두 컴포넌트는 `Awake()`에서 `gameObject.SetActive(false)`로 스스로를 숨기는 구조였다.
그런데 오브젝트가 **비활성 상태로 씬에 저장**되면 Unity는 `Awake`를 실행하지 않고 미뤄 두었다가,
런타임에 `Show()`가 `SetActive(true)`를 호출하는 **바로 그 순간 `Awake`를 처음 실행**한다.
그 늦은 `Awake`가 다시 `SetActive(false)`를 수행해 **켜자마자 스스로를 꺼 버린다.**

**해결:** 런타임 전용 플래그 `_showRequested`를 두고, **`Show()`가 활성화 직전에 이 플래그를 먼저 세운다.**
늦게 도는 `Awake`는 플래그를 보고 **자기 비활성화만 건너뛴다**(`if (!_showRequested) gameObject.SetActive(false);`).

- **초기 숨김 동작과 `Hide()`는 불변** — 정상적으로 활성 상태로 저장된 경우의 흐름은 그대로다.
- 플래그에 **`[SerializeField]`를 붙이지 않는다** → 씬에 저장되지 않으므로 다음 실행에 상태가 새지 않는다.
- `SkillAimReticle`은 **이번 작업과 무관한 기존 버그**였으며, 같은 함정을 공유하므로 사용자 승인으로 함께 수정했다(§8 표 · R13).

**근거 규칙:**
- **UI 규칙 8** — *"아군 MistShrine을 클릭해 패널이 열려 있는 동안에만 반투명 원형 UI로 회복 범위를 표시한다 / 패널을 닫으면 즉시 사라진다."*
  원이 **한 번도 보이지 않으면 규칙 8이 요구하는 표시 자체가 성립하지 않는다.** 이 수정은 규칙 8을 동작하게 만드는 최소 수정이다.
- **UI 규칙 12** — 범위 표시 원의 시각 사양(반투명 채움 + 외곽선)을 정의한다. 원이 그려지지 않으면 이 사양도 확인할 수 없다.

---

### 9-2. 범위 반경 값 이중화 제거 (커밋 `68ab380`)

**증상(잠재):** 화면에 그려지는 원의 크기와 **실제 회복이 적용되는 범위가 조용히 어긋날 수 있는 상태**였다.

**원인:** 구현 과정에서 반경 값이 **두 곳에 저장**되었다.

| 저장 위치 | 성격 |
|------|------|
| `MistShrinePanelUI._rangeRadius` (씬에 저장되는 `[SerializeField]`) | **원을 그릴 때만** 쓰이는 표시용 값 |
| `SpecialAttackConfig.MistRadius` (`.asset`) | **실제 회복 판정**에 쓰이는 값 (§3-1이 정한 밸런싱 단일 지점) |

밸런싱은 §3-1에 따라 **`SpecialAttackConfig.asset`만 고치도록** 되어 있으므로,
에셋만 수정하면 **판정 범위는 바뀌는데 화면의 원은 옛 크기 그대로** 남는다. 경고도 오류도 없이 어긋난다.

**해결:**
- `MistShrineUseCase.GetRadius()`를 추가해 **판정에 쓰는 값을 그대로 노출**하고, 패널이 그릴 때 이 값을 조회한다.
- `MistShrinePanelUI._rangeRadius`는 **제거**한다(주석 처리 아님 — 제거 근거는 최상단 「기존 로직 제거 여부」 ② 참조).
- 에디터 셋업 스크립트에서 이 필드를 채우던 **배선 코드 · 전용 헬퍼 `ConnectFloat` · 호출자가 사라진 config 로드 블록**도 함께 정리한다.
- **UseCase 주입이 실패한 경우 임의의 기본값으로 원을 그리지 않는다** — 원을 **숨긴 뒤 경고만 남긴다.**
  *잘못된 범위를 자신 있게 보여 주는 것이 아무것도 보여 주지 않는 것보다 해롭다*는 판단이며, 이는 UI 규칙 8이 범위 표시를 **정보 전달 수단**으로 규정하기 때문이다.

**근거 규칙:**
- **Buildings 규칙 3** — *"회복 범위는 MistShrine 건물 자신의 중심을 기준으로 한 고정 원형 / 반경 판정은 월드 좌표 거리 기준."*
  범위는 **하나의 정의된 원**이므로, 표시용 반경과 판정용 반경이 별개 값으로 존재해서는 안 된다.
- **Buildings 규칙 16** — 범위 반경은 **미확정(밸런싱 단계 확정)** 이다. 앞으로 반드시 바뀔 값이므로, 두 곳에 두면 **어긋남이 사고가 아니라 예정된 일**이 된다.
- **UI 규칙 8** — 범위 표시는 플레이어가 회복 범위를 읽는 수단이다. 실제 판정과 다른 원은 규칙 8의 목적을 반대로 뒤집는다.
- **아키텍처 제약(MEMORY)** — *"값의 단일 소스 원칙: 같은 값을 두 곳에 저장하지 않는다"* · *"Inspector 값이 코드 기본값보다 우선"*.
- **§3-1** — *"밸런싱 확정 시 바꿀 곳은 `SpecialAttackConfig.asset` 한 파일뿐"* 이라는 이 Plan 자신의 약속을 지키기 위해 필요한 수정이다.

---

### 9-3. 원 기준 지름을 스프라이트에서 자동 산출 (커밋 `df6909d`)

**증상:** 범위 원이 **실제 회복 반경보다 작게** 그려졌다(사용자 실기 보고).

**원인:** 원의 크기는 `scale = (2 × radius) / _baseDiameter` 로 계산되고,
여기서 `_baseDiameter`는 **"그 스프라이트를 `localScale = 1`로 뒀을 때의 월드 지름"** 이어야 한다.
코드 기본값도 씬 저장값도 `1.0`이었지만, 실제로 배선된 것은 에디터 셋업이 넣은
**Unity 내장 임시 스프라이트(`UI/Skin/Knob.psd`)** 였고 **그 월드 지름은 1이 아니었다.**
결과적으로 분모가 틀려 원이 작게 그려졌다.

**해결:**
- `_baseDiameter`를 **제거**하고 `_baseDiameterOverride`(기본값 `0`)를 신설한다.
- **0 이하면 자동 산출** — `SpriteRenderer.sprite.bounds.size`에서 읽는다(Pixels Per Unit이 이미 반영된 **월드 크기**).
- **양수면 수동 오버라이드** — 예외 상황에서 값을 강제할 수 있는 탈출구를 남긴다.
- **매 `Show()` 호출마다 다시 읽는다(캐시 금지)** → 임시 스프라이트를 정식 아트로 교체해도 **코드·Inspector 수정 없이 크기가 자동으로 따라간다.**
- 지름 → `localScale` 변환을 **렌더러별로 각각 수행**한다. Ring / Fill / Dot이 서로 다른 스프라이트를 써도 각각 정확한 크기가 된다.
- **비정사각형 스프라이트는 긴 축을 기준으로 삼는다.**
  짧은 축을 기준으로 하면 **긴 축이 범위 밖으로 삐져나와** 실제보다 넓은 범위인 것처럼 보인다.
  범위 표시에서는 **과대 표시가 과소 표시보다 치명적**이므로(있지도 않은 범위를 믿고 유닛을 배치하게 된다) 긴 축 기준을 택했다.

#### ⚠️ 핵심 기법 — 필드 이름을 바꾸고 `[FormerlySerializedAs]`를 **일부러 붙이지 않았다**

씬에는 이미 `_baseDiameter: 1`이라는 **잘못된 값이 저장되어 있었다.** 이 값을 무력화하는 것이 수정의 본체다.

Unity 직렬화는 **필드 이름으로 저장값을 매칭**하므로, 이름을 `_baseDiameterOverride`로 바꾸면
**옛 키(`_baseDiameter: 1`)는 로드 시 버려지고**, 새 필드는 C# 기본값 `0`으로 초기화되어 **자동 산출 경로를 탄다.**
여기에 `[FormerlySerializedAs("_baseDiameter")]`를 붙였다면 **씬의 `1`이 그대로 마이그레이션되어 수정이 통째로 무효화**됐을 것이다.

> **일반 교훈 (다음 사이클에서 반드시 기억할 것):**
> **직렬화 필드(`[SerializeField]`)에 잘못 저장된 값은 "코드 기본값을 고치는 것"으로는 절대 고쳐지지 않는다.**
> 이 프로젝트의 대원칙 그대로 **Inspector(씬·프리팹·SO) 값이 코드 기본값을 항상 이기기** 때문이다.
> 고치는 방법은 ① 씬/에셋의 값을 직접 바꾸거나 ② **필드 이름을 바꿔 옛 직렬화 키를 버리거나**(이번 선택)
> ③ 런타임에 값을 무시하는 경로를 두는 것뿐이다. `[FormerlySerializedAs]`는 **값을 살리고 싶을 때만** 쓰는 도구이며,
> **값을 버리는 것이 목적일 때는 붙이지 않는 것이 수정 그 자체**다.

**근거 규칙:**
- **Buildings 규칙 3** — 회복 범위는 건물 중심 기준 **고정 원형**이며 판정은 **월드 좌표 거리**다. 화면의 원도 같은 월드 크기로 그려져야 한다.
- **UI 규칙 8** — 범위 표시의 목적은 플레이어가 **회복 범위를 정확히 읽는 것**이다. 크기가 어긋난 원은 이 목적을 달성하지 못한다.
- **UI 규칙 12** — *"반투명 채움만으로는 경계가 모호해 회복 범위를 정확히 읽기 어렵다"* 며 **채움 + 외곽선**을 요구한다.
  즉 규칙 12는 **경계선의 정확성**을 명시적으로 중요하게 다루므로, 그 경계선이 실제 반경과 달라서는 안 된다.
- **UI 규칙 15 / Buildings 규칙 26** — 아이콘·VFX가 **미제작**이라 현재 임시 에셋을 쓰고 있다.
  자동 산출 + 매 `Show()` 재확인 구조는 **정식 아트 교체 시 추가 작업이 발생하지 않도록** 하기 위한 대비다.

**⚠️ 부수 영향:** 같은 계산식을 쓰는 **`SkillAimReticle`의 조준원도 함께 커진다.**
지금까지 착탄 반경보다 작게 그려지고 있었다는 뜻이며, **표시/숨김 흐름 자체는 무변경**이지만
**조준원 크기는 실기 재확인이 필요하다** → **R13** · §10 체크리스트.

---

## 10. 완료 판정 (구현 작업)

- [ ] S1~S8 전 단계 컴파일 성공, 각 단계 종료 시점에도 빌드가 깨지지 않음
- [ ] 싱글: 탭 = 1회 시전 / 롱프레스 = 자동 토글 / 자동 중 탭 = 해제 동작
- [ ] 싱글: 물안개 지속 중 범위 안 **아군 유닛 + 아군 건물 + 시전 건물 자신 + Castle**의 HP가 1초마다 상승
- [ ] 싱글: 범위 이탈 즉시 회복 중단, 재진입 시 재개(규칙 9)
- [ ] 싱글: 풀피 대상에는 회복·텍스트 모두 없음(규칙 5·15)
- [ ] 싱글: 회복 텍스트가 **3초 주기**로만 뜨고 매초 도배되지 않음(규칙 15 / UI 9)
- [ ] 싱글: MistShrine 파괴 시 물안개·자동 모드·쿨다운이 즉시 정리되고 패널이 자동으로 닫힘(규칙 12·25 / UI 4)
- [ ] 싱글: 물안개 2개 중첩 시 회복이 **합산되지 않음**(규칙 13)
- [ ] 싱글: 자연회복 연구 상태에서 물안개 힐과 **둘 다 적용**됨(규칙 14)
- [ ] 멀티: 클라이언트 시전이 서버에서 재검증되고, 쿨다운 오버레이가 양쪽에서 동일하게 감소(규칙 21·22)
- [ ] 멀티: 건물 HP 회복이 클라이언트에 동기화됨(규칙 24)
- [ ] 멀티: 호스트에서 회복량이 **2배가 되지 않음**(이중 틱 없음 — R3)
- [ ] 아군 패널이 열린 동안에만 범위 원이 보이고, 닫으면 즉시 사라짐. **적 건물은 패널·범위 모두 없음**(UI 8)
- [ ] `MistShrinePanelUI`에 `OnDestroy` 선언이 없음(R6)
- [ ] **MistShrine 패널**을 배경 탭으로 닫은 프레임에 뒤쪽 타일이 선택되지 않음(§2-4-2 ①)
- [ ] **기존 결손 보정 확인** — **연구 패널·스킬 패널**을 배경 탭으로 닫은 프레임에도 뒤쪽 타일이 선택되지 않음(§2-4-2 ②)
- [ ] **회귀 없음 확인** — 위 패널들을 닫은 **다음 프레임부터는** 타일 클릭이 정상 동작(가드가 1프레임만 막음 — R9-1)
- [ ] 밸런싱 수치가 **`SpecialAttackConfig.asset` 한 곳**에만 있고, 코드에는 폴백 기본값만 존재
- [ ] 구현 상태 표기가 **과대 표기 없이** 갱신됨(연출·아이콘 미제작 명시)
- [ ] git 명령 실행 0건 (CLAUDE.md 규칙 5)

### 보완 수정 3건 관련 (§9 — 2026-08-11 추가)

- [ ] **컴파일 오류 0건** — 보완 수정 3건은 이 환경에 Unity가 없어 **정적 작성만 되었다.** 사용자 에디터에서 컴파일을 먼저 확인한다. `Assets/Editor/` 쪽(반경 배선·`ConnectFloat` 제거분)도 함께 확인 (R14 / §9-2)
- [ ] 아군 MistShrine 패널을 열면 **범위 원이 실제로 화면에 나타난다** — 늦게 실행되는 `Awake`가 스스로를 다시 끄지 않음 (§9-1 / UI 8)
- [ ] 패널을 닫으면 범위 원이 **즉시 사라지고**, 다시 열면 **다시 나타난다**(반복해도 동일) (§9-1 / UI 8)
- [ ] `SpecialAttackConfig.asset`의 반경 값을 바꾸면 **화면의 원 크기가 그대로 따라간다** — 값이 두 곳에 있지 않음 (§9-2 / Buildings 3·16)
- [ ] 범위 원의 겉보기 반경이 **실제 회복이 들어가는 범위와 일치**한다 — 원 경계 바로 안쪽 유닛은 회복되고, 바로 바깥쪽 유닛은 회복되지 않음 (§9-3 / Buildings 3 · UI 8·12)
- [ ] **`SkillAimReticle` 조준원 실기 재확인** — ① 조준원이 **첫 사용에서도 정상 표시**되는지(§9-1) ② **커진 조준원 크기가 실제 착탄 반경과 일치**하는지, 화면을 과도하게 덮지 않는지(§9-3 / R13) ③ 표시·숨김 흐름에 변화가 없는지
- [ ] 임시 스프라이트(`UI/Skin/Knob.psd`)를 **정식 아트로 교체했을 때** 코드·Inspector 수정 없이 크기가 자동으로 맞는지 — 교체 시점에 확인 (§9-3)
- [ ] 씬에 `_baseDiameter` 잔재가 남아 **원 크기를 되돌리지 않는지** — `[FormerlySerializedAs]`를 붙이지 않은 것이 이 수정의 본체임 (§9-3)
