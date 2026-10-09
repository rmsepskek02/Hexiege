# Research — 버섯폭격기(MushroomBomber) 이펙트를 3개로 나누기 (attack → tracer · impact · poison)

## 이 작업은 무엇이고 왜 하는가

버섯폭격기는 포자 폭탄을 적에게 던지는 유닛이다. 폭탄이 떨어진 자리에서 터지고, 그 주변에 있던 적들은 3초 동안 1초마다
독 피해를 입는다. 그런데 이펙트 목록 문서(`VFXSFXList.md`)에는 이 모든 것이 **이펙트 하나**(`vfx_mushroombomber_attack.prefab`, 비고 「포자 투척 폭발」)로 적혀 있다.

사용자는 지금 Unity AI 로 버섯폭격기 이펙트를 직접 만들고 있는데, 실제로 화면에 보여야 하는 것은 성격이 다른 **세 가지**다.

1. **날아가는 폭탄** — 버섯폭격기 손에서 적에게 날아가는 포자 덩어리 (`vfx_mushroombomber_tracer.prefab`)
2. **떨어진 자리의 폭발과 독구름** — 착탄 순간 터지고 3초 동안 남는 보라색 독구름 (`vfx_mushroombomber_impact.prefab`)
3. **독에 걸린 적의 몸에 붙는 표시** — 독 피해를 받는 동안 그 적에게 붙어 반복되는 이펙트 (`vfx_mushroombomber_poison.prefab`)

그래서 목록 문서의 한 줄을 세 줄로 바꾸려 한다. **프리팹은 사용자가 만들고, 이번 작업은 목록 문서만 맞춘다.**
이 문서는 그 전에 실제 파일을 열어 확인한 사실 — 버섯폭격기 공격이 실제로 어떻게 동작하는지, 지금 코드에 이 세 이펙트를 꽂을 자리가 있는지,
그리고 같은 문서를 고치는 꽃요정(BloomFairy) 작업과 어떻게 맞물리는지 — 를 정리한 것이다. 이펙트를 게임에 실제로 연결하는 일은 **이번 범위가 아니다.**

※ 근거 구분(CLAUDE.md 규칙 10): 아래 파일·행·설정값은 **2026-10-09 에 직접 실측**했다. 프리팹 사양(5절)은 호출 세션이 전달한 합의 내용의 요약이며,
생성 프롬프트 원문은 대화에 있다.

---

## 1. 지금 이름이 적힌 곳

`mushroombomber` 를 `.md`·`.cs`·`.asset`·`.prefab`·`.unity` 전체에서 찾았다(`_Tasks/`·`_Logs/` 제외).

| 위치 | 행 | 현재 내용 | 소속 |
|---|---|---|---|
| `Assets/_Project/Docs/Assets/VFXSFXList.md` | 45 | `` `vfx_mushroombomber_attack.prefab` `` · 비고 「포자 투척 폭발」 | Ⅰ. VFX → 2. 제작 예정 VFX → **「유닛 공격 이펙트」** 표 — **이번 변경 대상** |
| 같은 문서 | 75 | `` `vfx_mushroombomber_death.prefab` `` | 「유닛 사망 이펙트」 표 — 대상 아님 |
| 같은 문서 | 256 | `` `sfx_mushroombomber_attack.wav` `` · 비고 「포자 폭발음」 | 「유닛 공격 사운드」 표 — **사용자 결정으로 이번엔 그대로 둔다**(추후 사운드 작업에서 재검토) |
| 같은 문서 | 286 | `` `sfx_mushroombomber_death.wav` `` | 「유닛 사망 사운드」 표 — 대상 아님 |
| `Assets/_Project/Docs/Assets/AssetList.md` | 154 | `mushroombomber_portrait_blue/red.png` | 초상화 — 이펙트와 무관 |

- **`vfx_mushroombomber_attack` 은 `VFXSFXList.md` 45행 1곳뿐이다.** 코드·설정 에셋·프리팹·씬 어디에서도 이 이름을 쓰지 않는다.
- **버섯폭격기 VFX 프리팹은 아직 하나도 없다.** `Assets/_Project/Prefabs/VFX/Units/` 의 프리팹은 10개(`vfx_bloomfairy_heal` · `vfx_cannon_attack` · `vfx_foxmagician_charge` · `vfx_infernospirit_charge` · `vfx_pistoleer_attack` · `vfx_quakespirit_attack` · `vfx_streamspirit_attack` · `vfx_tank_attack` · `vfx_torrentspirit_attack` · `vfx_unit_death`)이고 버섯폭격기 것은 없다.
- → **이름을 바꾸거나 나눠도 끊어지는 참조(파일·GUID·코드 문자열)가 없다.** 바꾸는 것은 문서 1행이다.

### `VFXSFXGuide.md` — 변경 불필요

| 행 | 내용 | 판정 |
|---|---|---|
| 301 | `\| MushroomBomber \| Transcendence \| 독/폭발 투척 \| spore explosion, poison cloud \|` (VFX 「1. 유닛 공격 이펙트」 키워드 표) | **파일 이름이 없는 키워드 행**이다. 키워드(포자 폭발 · 독구름)는 이번에 나누는 이펙트 중 착탄 폭발과 그대로 맞는다. 꽃요정 작업의 같은 판단(그 작업 Research.md 1절)과 같은 이유로 고치지 않는다 |
| 492 | `\| MushroomBomber \| Transcendence \| spore throw, toxic pop \|` (SFX 키워드 표) | SFX 는 이번 범위 밖이다 |

- 같은 문서 「파일 명명 규칙」(69~110행)은 `vfx_[에셋명]_[이벤트].prefab` 이다. `tracer` · `impact` · `poison` 은 모두 「이벤트」 자리에 들어가므로 **명명 규칙 안이다.**
  「attack」이 아닌 이벤트명은 이미 선례가 있다 — 실물 프리팹 `vfx_foxmagician_charge` · `vfx_infernospirit_charge`, 목록 문서의 `vfx_mistshrine_mist_cast` · `vfx_mistshrine_mist_loop`.

---

## 2. 버섯폭격기 공격은 실제로 어떻게 동작하는가

| 사실 | 값 | 근거 |
|---|---|---|
| 사거리 | 2.0 | `Assets/_Project/Docs/StatsReference.md` 91행 |
| 주 타깃 1마리 직접 피해 | **100** (건물이 주 타깃이어도 들어감 — 공성) | `StatsReference.md` 91행 · `GameSystemRules_Units.md` 규칙 39 |
| 독 지속 피해(DoT) | **초당 20 × 3초**(총 60) | `StatsReference.md` 91행 · `Assets/_Project/Resources/Config/SpecialAttackConfig.asset` 24~25행(`_blastDotPerSecond: 20`, `_blastDotDuration: 3`) |
| DoT 대상 | 착탄 중심에서 **월드 XZ 평면 거리 ≤ `blastRadius`** 인 **적 유닛 전원**(주 타깃 포함) | `GameSystemRules_Units.md` 규칙 38 |
| `blastRadius` | **1.0**(인접 1칸 거리) | `SpecialAttackConfig.asset` 23행(`_blastRadius: 1`) · `Scripts/Infrastructure/Config/SpecialAttackConfig.cs:84` |
| 착탄 중심 | **주 타깃의 월드 위치** | `GameSystemRules_Units.md` 규칙 38 |
| 틱 방식 | **1초 간격 discrete**(뚝뚝), 매초 남은 체력 데미지 텍스트 | 규칙 40 · `Scripts/Application/UseCases/UnitCombatUseCase.cs:122`(`BlastDotTickInterval = 1.0f`) |
| 건물 | DoT 없음(직접 피해만) | 규칙 38·39 |
| 아군 | 직접·DoT 모두 무피해 | 규칙 39(규칙 16) |
| 다시 맞으면 | 남은 시간·총량을 **리셋**(겹쳐 쌓이지 않음) | 규칙 34 「갱신 = 리셋」 · 규칙 40 |
| DoT 가 끝나는 때 | **지속 만료** 또는 **대상 사망** | 규칙 34 「종료 처리」(풀피 종료는 HoT 전용) |

- `SpecialAttackConfig.asset` 이 실제로 쓰이는 값인지 확인했다 — 이 에셋의 GUID(`f8598314…`)가 `Assets/_Project/Scenes/Game.unity` 에서 참조된다.
- ⚠️ **규칙 문서의 숫자를 읽을 때 주의**: `GameSystemRules_Units.md` 규칙 38~40 본문은 「직접 10」「초당 2(총 6)」로 적혀 있다. 이는 전투 스탯 ×10 이전 값이며,
  `GameSystemRules_Upgrade.md` 규칙 1 이 「DoT 틱값(MushroomBomber 2→20/s)」을 포함해 ×10 을 정하고 **개별 값은 `StatsReference.md` 가 권위 소스**라고 적는다. 그래서 위 표는 100 · 20 을 쓴다. 반경 1.0 은 ×10 대상이 아니다(같은 규칙 1 「불변」).

### 2-1. 알아둘 사실 — 독구름은 「보이는 것」이고 피해 판정은 착탄 순간 한 번이다

`Scripts/Application/Combat/BlastAttackBehavior.cs` 의 `Apply`(66행~)는 착탄 순간 반경 안의 적 유닛을 **한 번 모아**(85행 `CollectEnemyUnitsInRadius`)
각자에게 DoT 를 건다(92행 `ctx.ApplyDot`). 그 뒤로는 **각 유닛에 붙은 DoT 가 따로 돈다.**

- 그래서 착탄 뒤 독구름 **밖으로 걸어 나간 적도 3초 동안 계속 피해를 받고**, 착탄 뒤 구름 **안으로 걸어 들어온 적은 피해를 받지 않는다.**
- 즉 착탄 이펙트의 독구름(3초, 반경 1.0)은 **피해 범위를 시각적으로 암시하는 연출**이고, 실제로 누가 독에 걸렸는지는 **3번 독 상태 이펙트(적 몸에 붙는 것)** 가 보여 준다.
  두 이펙트를 나누는 이유가 여기에 있다. (이 사실은 판단이 아니라 기록이다 — 연출을 어떻게 보이게 할지는 사용자가 정한다.)

---

## 3. 지금 코드에 있는 이펙트 「칸」과 세 이펙트의 관계

### 3-1. 유닛 이펙트 칸은 4개다

`Scripts/Presentation/Effects/UnitEffectConfig.cs` 의 `UnitEffectEntry`(37~54행)에 유닛 종류마다 칸이 4개 있다.

| 칸 | 언제·어디서 재생되나 | 세 이펙트에 쓸 수 있나 |
|---|---|---|
| `attackPreset`(43행) | 공격 애니메이션의 `OnAttackHit` 이벤트 순간, **공격하는 유닛의 `VfxSpawnPoint`(없으면 유닛 위치)에서 한 번** — `Scripts/Presentation/Unit/UnitView.cs:1879~1881` → `EffectManager.PlayUnitAttack`(`Scripts/Presentation/Effects/EffectManager.cs:174`) | 세 이펙트 어느 것도 이 위치에서 재생되지 않는다(발사 순간의 손 쪽 섬광 같은 것이 들어가는 칸이다) |
| `tracerPreset`(53행) | 같은 `OnAttackHit` 순간, 사거리 ≥ 1.0 인 유닛이면(`UnitView.cs:76` `RangedAttackThreshold = 1.0f`, 1897행) **발사 지점 → 발사 순간의 타깃 위치로 직선 비행** — `UnitView.cs:1908` → `EffectManager.PlayTracer`(275~291행) → `TracerProjectile` | **1번 투사체가 들어갈 칸이다.** 버섯폭격기 사거리 2.0 은 이 분기를 탄다 |
| `hitPreset`(49행) | 피격 연출 방출 시 **맞은 유닛의 위치에서 한 번**. 단, 칸을 고르는 키가 **맞은 유닛의 종류**다 — `Scripts/Presentation/Effects/HitPresentationQueue.cs:369~374`(`PlayUnitHit(targetUnit.Type, …)`) → `EffectManager.cs:197~201` | **버섯폭격기 전용 폭발을 담을 수 없다.** 여기에 넣으면 「버섯폭격기가 때렸을 때」가 아니라 「버섯폭격기가 **맞았을 때**」 재생된다 |
| `deathPreset`(46행) | 사망 시 한 번 | 무관 |

- **착탄 지점에서 폭발을 재생하는 칸·메서드가 없다.** `EffectManager` 의 공개 재생 메서드는 `PlayUnitAttack`(174) · `PlayUnitDeath`(184) · `PlayUnitHit`(197) · `PlayBuildingDestroy`(213) · `PlayBuildingUpgrade`(224) · `PlayBuildingAttack`(239) · `PlayUi`(253) · `PlayTracer`(275)뿐이다.
  트레이서의 착탄 콜백(`onArrive`)은 지금 「피격 연출 방출 신호」 하나만 보낸다(`UnitView.cs:1908~1909`).
- **대상에 붙여 반복 재생하다가 끄는 메서드가 없다.** 위 8개는 모두 「위치에서 한 번」이고 정지·부착(대상 추종) 메서드는 없다(꽃요정 작업 Research.md 4절 1번과 같은 사실 — 재확인).
  또 풀링된 VFX 는 **파티클이 전부 사라져야 풀로 돌아간다**(`Scripts/Presentation/Effects/VfxPoolItem.cs:183~195` `AnyAlive()`) — 반복(loop) 재생 이펙트를 기존 재생 경로에 넣으면 영영 돌아오지 않는다.
- **DoT 가 걸리고·갱신되고·끝나는 순간을 알리는 이벤트가 없다.** `Scripts/Application/Events/GameEvents.cs` 의 공개 정적 이벤트 42개 중 DoT·시간 지속 효과용은 0개다(회복 쪽 `OnEntityHealed` 766행 · `OnUnitHealCastStarted` 920행만 있다).
  멀티에서도 DoT 를 알리는 RPC 는 없고(`Scripts/Infrastructure/Network/*.cs` 검색), 클라이언트는 HP·데미지 텍스트만 동기화로 받는다(규칙 40 「서버 권위」).
  → 3번 독 상태 이펙트를 켜고 끄려면 **연결 작업에서 신호부터 새로 만들어야 한다.**

### 3-2. 트레이서의 두 가지 성질 (연결 작업에서 알아야 할 사실)

`Scripts/Presentation/Effects/TracerProjectile.cs`:

1. **직선 비행만 한다** — 매 프레임 `Vector3.Lerp(_start, _target, t)`(155행). 포물선(던지는 궤적)은 없다. 포물선은 코드가 필요하며 **사용자가 나중으로 미뤘다.**
2. **착탄하면 그 자리에서 바로 꺼진다** — `Arrive()`(169~190행)가 콜백을 부른 뒤 곧바로 `gameObject.SetActive(false)`(183행)하고 풀로 돌려보낸다.
   오브젝트를 끄면 그 아래 파티클도 함께 사라지므로, 투사체의 **월드 공간 꼬리 입자도 착탄 순간 같이 사라진다.**
   꼬리가 남아 흩어지길 원한다면 연결 작업에서 다룰 일이다(판단은 하지 않음 — 사실 기록).

- 현재 설정 에셋 `Assets/_Project/Resources/Config/UnitEffectConfig.asset` 의 25개 항목 **전부 `tracerPreset` 이 비어 있다**(실측 25/25). 즉 이 설정 기준으로는 트레이서 칸을 쓰는 유닛이 아직 없다.
  이 에셋이 실제로 쓰이는 것인지도 확인했다 — 에셋 GUID 가 `Game.unity` 에서 참조된다.

---

## 4. 지금 버섯폭격기에게는 공격 이펙트가 하나도 재생되지 않는다

| 확인 | 실측 |
|---|---|
| 설정 에셋의 버섯폭격기 항목 | `UnitEffectConfig.asset` 131~135행 `unitType: 26`(`Scripts/Domain/Unit/UnitType.cs:48` `MushroomBomber = 26`) — `attackPreset` · `hitPreset` · `tracerPreset` **비어 있음**, `deathPreset` 만 공용 사망 프리셋(`EffectPreset_Unit_Death_Common.asset`) |
| 공격 애니메이션의 이벤트 | `Assets/_Project/Animations/Units/MushroomBomber/MushroomBomber_Attack.anim` 71483행 **`m_Events: []`** — `OnAttackHit` 0건 |
| 그 클립이 실제 공격 상태에 쓰이는가 | 예 — `MushroomBomber.controller` 83행 `m_Name: Attack` 상태의 모션(96행)이 이 클립의 GUID(`8b7e1085…`)를 가리킨다 |

- `OnAttackHit` 이 불리지 않으므로 `UnitView.OnAttackHit()`(1865~1919행) 안의 공격 VFX·SFX·트레이서 발사가 버섯폭격기 몫으로는 **실행되지 않는다.** 칸에 프리셋을 넣어도 지금 상태로는 보이지 않는다.
- 피해 자체는 서버 타이머(`HitFrameTimes`)로 들어가므로 영향이 없다(규칙 18).
- ⚠️ **규칙 문서와 실측이 어긋난다.** `GameSystemRules_Units.md` 규칙 27 은 「MushroomBomber는 클립에 `OnAttackHit` **1개** 주입 완료(2026-07-19, 규칙 38~40)」라고 적는다.
  실측은 0건이다. 그 뒤에 이벤트가 지워진 것인지, 다른 이유인지는 이 문서에서 확정할 수 없다(git 명령 금지 — CLAUDE.md 규칙 5). **이번 범위에서는 규칙 문서를 고치지 않고 사실만 기록한다**(7절).

---

## 5. 합의된 프리팹 사양 (요약 — 생성 프롬프트 원문은 대화에 있다)

저장 위치는 `VFXSFXGuide.md` 「폴더 구조」에 따라 `Assets/_Project/Prefabs/VFX/Units/`.

| 파일 | 무엇 | 재생 | Stop Action | 시뮬레이션 공간 | 구성 |
|---|---|---|---|---|---|
| `vfx_mushroombomber_tracer.prefab` | 날아가는 포자 폭탄 — 지름 약 0.25 의 둥근 갈색 포자 구 + 보라 독가루 꼬리 | **반복(loop)** — 비행 동안 | None | 몸체 Local / 꼬리 World | — |
| `vfx_mushroombomber_impact.prefab` | 착탄 폭발 — 갈색 포자 파열(0~0.5초) + 보라 독구름 · 지면 원 · 독 기포가 **월드 반경 1.0** 에 **정확히 3.0초**(DoT 지속과 동일), 2.5→3.0초 사이 페이드아웃 | **1회** | Destroy | — | `Spore_Burst` / `Poison_Cloud` / `Poison_Ring` / `Toxic_Bubbles` |
| `vfx_mushroombomber_poison.prefab` | 독 상태 표시 — DoT 를 받는 적 유닛 각각에 붙음, 은은한 톤, 약 0.2초 페이드인. **정지 시 방출만 멈추고 남은 입자는 자연히 사라진다**(꽃요정 치료 이펙트와 같은 방식) | **반복(loop)** — 코드가 켜고 끔 | None | Local | `Poison_Bubbles` / `Toxic_Drips` / `Poison_Aura` |

- 착탄 이펙트의 반경 1.0 · 3.0초는 **지금의** `blastRadius`(1.0) · `blastDotDuration`(3) 값에 맞춘 것이다. 이 두 값은 Inspector 에서 바꿀 수 있으므로(규칙 25·40), 값이 바뀌면 프리팹이 저절로 따라가지 않는다(Plan.md 위험 요소).
- 독 상태 이펙트는 꽃요정 치료 이펙트와 같은 부류다 — **대상에 붙어, 길이가 매번 다른 반복 재생**(DoT 는 만료·사망으로 끝나고 재부여 시 리셋되어 길어질 수 있다 — 2절).
  사용자가 이 이펙트를 「유닛 공격 이펙트」가 아니라 **「유닛 특수 효과」** 절에 두기로 정했다.

---

## 6. 「유닛 특수 효과」 절과의 선후 관계 (꽃요정 작업)

3번 독 상태 이펙트가 들어갈 「유닛 특수 효과 이펙트」 절은 **아직 없다.** 그 절은 꽃요정 작업의 Plan 이 정의한다 —
`Assets/_Project/Docs/_Tasks/2026-10-08/17_09_bloomfairy-heal-vfx-naming/Plan.md` 2절 **B-2**(VFX 절 신설: 위치 · 제목 · 머리 안내문 3줄 · 4열 표)와 **B-4**(SFX 절 신설). 사용자는 그 문서 변경을 **직접** 하겠다고 했고 순서는 정해지지 않았다.

2026-10-09 실측(이 문서 작성 시점):

| 확인 | 결과 |
|---|---|
| `VFXSFXList.md` 의 `#### 유닛 특수 효과 이펙트` | **0건** — 절 없음 |
| `VFXSFXList.md` 의 `#### 유닛 특수 효과 사운드` | **0건** |
| 꽃요정 VFX 행의 위치 | 아직 「유닛 공격 이펙트」 표 46행(`` `vfx_bloomfairy_attack.prefab` ``) — 꽃요정 문서 변경은 아직 안 됐다 |
| 「유닛 공격 이펙트」 표의 행 수 | **25행**(Human 8 · Spirit 9 · Transcendence 8) |

두 작업이 서로에게 주는 영향:

1. **어느 쪽이 먼저든 마지막 모습이 같아야 한다.** 그래서 이번 Plan 은 「절이 이미 있으면 행만 넣고, 없으면 꽃요정 Plan B-2 그대로 만든다」는 두 경우를 모두 정한다(Plan.md 2절).
2. **꽃요정 Plan 7절의 확인 기대값 하나가 달라진다.** 그 Plan 은 꽃요정 행을 뺀 뒤 「유닛 공격 이펙트」 표가 **24행**(Transcendence 7)이 된다고 적었다.
   이번 작업은 그 표에서 1행을 2행으로 바꾸므로, 이번 작업이 먼저 반영되면 꽃요정 작업 후 그 표는 **25행**(Transcendence 8)이 된다. 「유닛 공격 사운드」 표는 이번에 건드리지 않으므로 24행 그대로다.
3. **꽃요정 Plan 이 적은 행 번호가 1칸씩 밀린다**(예: B-2 의 「76행」「78행」). 그 Plan 도 「위치는 행 번호가 아니라 절 제목으로 찾는다」고 적어 두었으므로 절차 자체는 그대로 쓸 수 있다.
4. 이번 작업이 먼저 절을 만들면, 꽃요정 작업은 B-2 에서 **절을 만들지 않고 꽃요정 행만 넣으면 된다.**

---

## 7. 작업 중 발견한 부가 이슈 (이번 범위 밖 — 보고만)

| # | 내용 | 실측 |
|---|---|---|
| 1 | `GameSystemRules_Units.md` 규칙 27 의 「MushroomBomber 클립에 `OnAttackHit` 1개 주입 완료」와 클립 실측(이벤트 0건)이 어긋난다. 꽃요정의 같은 부류 불일치(꽃요정 작업 Research.md 5절 3번)와 함께 확인할 거리다 | 4절 |
| 2 | `VFXSFXList.md` 「1. 완성된 VFX」(10~12행)가 「현재 없음」인데 `Prefabs/VFX/Units/` 에는 프리팹이 10개 있다 — 꽃요정 작업 Research.md 5절 1번에서 이미 보고된 것과 같은 사안이다 | 1절 |

→ 둘 다 이번 작업(목록 문서의 이름 나누기)과 무관하므로 고치지 않는다(CLAUDE.md 규칙 6). 필요하면 별도 작업으로 제안한다.
