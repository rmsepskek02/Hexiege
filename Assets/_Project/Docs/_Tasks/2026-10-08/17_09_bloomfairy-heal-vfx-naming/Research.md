# Research — BloomFairy 힐 이펙트 이름 정리 (attack → heal)

## 이 작업은 무엇이고 왜 하는가

꽃요정(BloomFairy)은 적을 때리지 않고 아군을 치료하는 유닛이다. 그런데 이펙트 목록 문서에는 꽃요정의 이펙트가
다른 유닛과 똑같이 「공격(attack)」이라는 이름으로 적혀 있다(`vfx_bloomfairy_attack.prefab`, `sfx_bloomfairy_attack.wav`).

사용자는 지금 Unity AI 로 꽃요정의 치료 이펙트를 직접 만들려고 한다. 이 이펙트는 공격처럼 「한 번 번쩍하고 끝나는」 것이
아니라, **치료를 받는 아군 몸에 붙어서 치료가 이어지는 동안 계속 반복되는** 상태 이펙트다. 만들기 전에 파일 이름부터
정해야 하므로, 사용자는 **이펙트 에셋(VFX·SFX)에서는 꽃요정의 「attack」을 전부 「heal」로 부르기로** 결정했다.

다만 **애니메이션은 이름을 바꾸지 않는다.** 게임 코드가 모든 유닛의 공격 동작을 「Attack」이라는 공통 이름으로 찾기 때문에,
꽃요정만 이름을 바꾸면 치료 동작의 타이밍을 못 찾게 된다. 이 문서는 이름이 어디에 적혀 있는지, 무엇은 바꿔도 되고 무엇은
바꾸면 안 되는지를 실제 파일을 열어 확인한 결과다. 이펙트를 유닛에 실제로 연결하는 일은 **이번 범위가 아니며**,
이펙트가 만들어진 뒤 별도 작업으로 한다.

---

## 1. 이름이 적힌 곳 — 전수 조사

`bloomfairy_attack` / `bloomfairy_heal` 을 `.md`·`.cs`·`.asset`·`.prefab`·`.unity` 전체에서 찾았다(2026-10-08 실측).

| 위치 | 행 | 현재 내용 | 비고 |
|---|---|---|---|
| `Assets/_Project/Docs/Assets/VFXSFXList.md` | 46 | `` `vfx_bloomfairy_attack.prefab` `` · 비고 「꽃잎 힐」 | Ⅰ. VFX → 2. 제작 예정 VFX → **「유닛 공격 이펙트」** 표 |
| `Assets/_Project/Docs/Assets/VFXSFXList.md` | 257 | `` `sfx_bloomfairy_attack.wav` `` · 비고 「힐 차임음」 | Ⅱ. SFX → 2. 제작 예정 SFX → **「유닛 공격 사운드」** 표 |

- **이 2건 외에는 0건**이다. 코드(`.cs`)·설정 에셋(`.asset`)·프리팹·씬 어디에도 이 파일 이름은 없다. `_Tasks/`·`_Logs/` 에도 0건이다.
- **실제 파일은 둘 다 존재하지 않는다.**
  - `Assets/_Project/Prefabs/VFX/Units/` 에 있는 프리팹 9개 중 꽃요정 것은 없다.
  - `Assets/_Project/Audio/SFX/Units/` 에는 `sfx_pistoleer_attack.wav` · `sfx_tank_attack.wav` · `sfx_unit_death.wav` 3개뿐이다.
  - → **이름을 바꿔도 끊어지는 참조(파일·GUID·코드 문자열)가 없다.** 바꾸는 것은 문서 2행뿐이다.
- 같은 문서의 사망 이펙트 행(76행 `vfx_bloomfairy_death.prefab`, 287행 `sfx_bloomfairy_death.wav`)은 「attack」이 아니므로 대상이 아니다.

### `VFXSFXGuide.md` — 변경 불필요 (확인 완료)

| 행 | 내용 | 판정 |
|---|---|---|
| 302 | `\| BloomFairy \| Transcendence \| 힐 \| petal heal, bloom glow \|` (VFX 「유닛 공격 이펙트」 키워드 표) | 파일 이름이 없다. 키워드도 이미 「heal」이다 → 변경 불필요 |
| 493 | `\| BloomFairy \| Transcendence \| gentle heal chime, flower bloom \|` (SFX 「유닛 공격 사운드」 키워드 표) | 같은 이유로 변경 불필요 |

- 같은 문서의 「파일 명명 규칙」(69~110행)은 `vfx_[에셋명]_[이벤트].prefab` · `sfx_[에셋명]_[이벤트].wav` 이다.
  「이벤트」 자리에 `heal` 을 넣는 것은 이 규칙 안에 들어간다 — **명명 규칙을 고칠 필요는 없다.**

---

## 2. 바꾸면 안 되는 것 — 애니메이션과 코드의 「Attack」

모든 유닛이 공격 동작을 같은 이름으로 찾는다. 꽃요정의 치료 동작도 이 공통 경로를 탄다(실측).

| 무엇 | 위치 | 「Attack」을 어떻게 쓰는가 |
|---|---|---|
| 애니메이션 클립 | `Assets/_Project/Animations/Units/BloomFairy/BloomFairy_Attack.anim` (9행 `m_Name: BloomFairy_Attack`) | 아래 코드들이 클립 이름에 「Attack」이 **포함**됐는지로 찾는다 |
| Animator 상태 | `Assets/_Project/Animations/Units/BloomFairy/BloomFairy.controller` (10행 `m_Name: Attack`) — 이 상태의 모션(23행)이 위 `.anim` 의 GUID `b934af54…` 를 가리킨다 | 상태 이름 「Attack」의 해시로 재생한다 |
| 공격 클립 길이 조회 | `Scripts/Infrastructure/Factories/UnitFactory.cs:461` | `clip.name.Contains("Attack")` |
| 타격 프레임(`HitFrameTimes`) 추출 | `Scripts/Infrastructure/Factories/UnitFactory.cs:496` | `clip.name.Contains("Attack")` — 꽃요정의 **치료 발동 시점**이 여기서 정해진다(규칙 32) |
| 상태 재생 | `Scripts/Presentation/Unit/UnitView.cs:54` | `Animator.StringToHash("Attack")` |
| 멀티 애니 상태 적용 | `Scripts/Infrastructure/Network/NetworkUnit.cs:332` | `case UnitAnimState.Attack:` → `PlayAttackAnimation()` (enum 멤버 — 문자열은 아니지만 같은 상태로 이어진다) |
| 에디터 이벤트 주입기 | `Scripts/Editor/CombatHitEventInjector.cs:260` | `clip.name.Contains("Attack")` |
| 에디터 이벤트 검증기 | `Scripts/Editor/CombatHitEventValidator.cs:176` | `clip.name.Contains("Attack")` |

- 클립 이름을 `BloomFairy_Heal` 로 바꾸면 `UnitFactory.cs:496` 이 꽃요정 클립을 못 찾아 타격 프레임이 폴백값으로 떨어지고,
  `UnitView.cs:54` 의 상태 해시도 맞지 않아 치료 동작이 재생되지 않는다. **그래서 애니메이션 쪽은 「Attack」을 유지한다**(사용자 결정).
- 코드 식별자(`AttackCooldown`, `OnAttackHit`, `UnitAnimState.Attack`, `PlayUnitAttack`, `PlayUnitAttackSfx` 등)와,
  규칙 문서에서 이 식별자를 인용한 문장(예: `GameSystemRules_Units.md` 규칙 17·27·32·36)도 **그대로 둔다** — 코드와 문서가 같은 이름을 가리켜야 하기 때문이다.

---

## 3. 이 이펙트가 기존 유닛 이펙트와 다른 점

### 3-1. 치료는 「시간 지속 효과」다 — 그래서 반복 재생이 필요하다

| 사실 | 근거 |
|---|---|
| 꽃요정의 치료는 **아군 1명**에게 거는 지속 회복(HoT)이다 | `GameSystemRules_Units.md` 규칙 33 · `StatsReference.md` 92행 「아군 단일 지정 힐 200 HP/3초」 |
| 지속시간 기본 3초 — `SpecialAttackConfig._bloomHealDuration`(Inspector 조정 가능) | `Scripts/Infrastructure/Config/SpecialAttackConfig.cs:77` · `Scripts/Bootstrap/GameBootstrapper.Setup.cs:316` |
| **대상 사망 / 풀피 도달 / 지속 만료** 시 효과가 끝난다 → 3초를 다 채우지 않고 **일찍 끝날 수 있다** | `GameSystemRules_Units.md` 규칙 34 「종료 처리」 |
| 같은 대상에 다시 걸면 **남은 시간이 리셋**된다(중첩 없음) → 3초보다 **길게 이어질 수 있다** | `GameSystemRules_Units.md` 규칙 34 「갱신 = 리셋」 |
| 실제 치료 주기는 4.0초(발동 준비 1.0초 + 발동 후 쿨다운 3.0초) | `GameSystemRules_Units.md` 규칙 36 |

→ 효과의 길이가 **매번 다르다.** 정해진 길이로 한 번 재생하고 사라지는 이펙트로는 맞출 수 없으므로,
**반복(loop) 재생하다가 코드가 시작·정지시키는** 구조여야 한다.

### 3-2. 기존 유닛 VFX 프리팹 실측 (`Assets/_Project/Prefabs/VFX/Units/`, 9개)

| 프리팹 | 파티클 시스템 수 | looping | 재생 길이(초) | stopAction |
|---|---|---|---|---|
| `vfx_cannon_attack` | 4 | 전부 OFF | 0.35 | None 3 · Destroy 1 |
| `vfx_pistoleer_attack` | 4 | 전부 OFF | 0.35 | None 3 · Destroy 1 |
| `vfx_streamspirit_attack` | 4 | 전부 OFF | 0.35 / 0.5 | None 3 · Destroy 1 |
| `vfx_tank_attack` | 4 | 전부 OFF | 0.6 | None 3 · Destroy 1 |
| `vfx_quakespirit_attack` | 3 | 전부 OFF | 0.5 | None 3 |
| `vfx_torrentspirit_attack` | 5 | 전부 OFF | 0.5 | None 5 |
| `vfx_unit_death` | 3 | 전부 OFF | 0.8 | None 2 · Destroy 1 |
| `vfx_foxmagician_charge` | 2 | 전부 OFF | 2 | None 1 · Destroy 1 |
| `vfx_infernospirit_charge` | 1 | 전부 OFF | 1 | Destroy 1 |

- 9개 전부 **looping OFF** 다. 공격 이펙트는 0.35~0.6초, 사망 0.8초, 충전(charge) 이펙트 1~2초.
- **시뮬레이션 공간은 9개 프리팹의 파티클 시스템 30개가 전부 Local**(`moveWithTransform: 0`)이다.
  > ⚠️ **인계 내용 정정**: 인계 메모에는 꽃요정 힐 이펙트가 「Local 시뮬레이션 공간이라는 점에서도 기존과 다르다」고 적혀 있었으나,
  > 실측하면 **기존 유닛 VFX 도 전부 Local** 이다. 기존과 실제로 다른 점은 **반복 재생(loop ON)** 과 **자동 소멸 없음(stopAction None)** 두 가지다.
- 여러 파티클 시스템을 형제로 둔 프리팹은 풀 아이템이 직속 자식을 각각 재생해야 한다 — `GameSystemRules_Units.md` 규칙 31. 4층 구성인 힐 이펙트도 이 구조에 해당한다.

### 3-3. 합의된 이펙트 설계 (참고용 — 생성 프롬프트 원문은 대화에 있다)

- 파일: `vfx_bloomfairy_heal.prefab` (저장 위치는 `VFXSFXGuide.md` 「폴더 구조」에 따라 `Assets/_Project/Prefabs/VFX/Units/`)
- 4층 구성: `Bloom_Ring`(바닥의 초록 꽃 원) · `Rising_Petals`(옅은 파스텔 분홍 꽃잎 상승) · `Sparkle_Motes`(초록 반짝임) · `Soft_Aura`(희미한 초록 오라)
- 은은한 톤, **Loop ON**, **Stop Action None**, 약 0.2초 페이드인, 끝날 때는 방출만 멈춰 남은 입자가 자연히 사라지게, Simulation Space Local
- **붙는 대상은 치료를 받는 아군**이다(치료하는 꽃요정이 아니다).

---

## 4. 나중에 연결할 때 알아야 할 것 (이번 범위 밖 — 기록만)

이펙트를 유닛에 연결하는 일은 이펙트가 만들어진 뒤 별도 작업이다. 그때 필요한 사실을 실측해 둔다.

1. **`EffectManager` 에는 「대상에 붙여 반복 재생하고 나중에 끄는」 API 가 없다.**
   `Scripts/Presentation/Effects/EffectManager.cs` 의 공개 재생 메서드는 `PlayUnitAttack`(174행) · `PlayUnitDeath`(184행) · `PlayUnitHit`(197행) ·
   `PlayBuildingDestroy`(213행) · `PlayBuildingUpgrade`(224행) · `PlayBuildingAttack`(239행) · `PlayUi`(253행) · `PlayTracer`(275행)이고,
   모두 「위치에서 한 번 재생」 방식이다. 정지(Stop)·부착(대상 Transform 추종) 메서드는 없다. → **연결 작업에서 신설이 필요하다.**
2. **이 힐 이펙트를 `UnitEffectConfig` 의 꽃요정 `attackPreset` 칸에 넣으면 안 된다.**
   - `Assets/_Project/Resources/Config/UnitEffectConfig.asset` 136~139행: `unitType: 27`(BloomFairy, `Scripts/Domain/Unit/UnitType.cs:49`)의 `attackPreset` 은 비어 있다(`deathPreset` 만 연결됨).
   - `attackPreset` 은 `UnitView.OnAttackHit()`(`Scripts/Presentation/Unit/UnitView.cs:1865~1882`)이 `PlayUnitAttack` 으로 **치료하는 꽃요정 자신의 위치에서 한 번** 재생하는 칸이다.
     힐 이펙트(대상에 붙어 반복)와 재생 위치·방식이 모두 다르다.
   - `GameSystemRules_Units.md` 규칙 31 은 「유닛 공격 VFX는 … `attackPreset` 에 연결한다」고 적고 있다. 힐 이펙트 행이 「유닛 공격 이펙트」 표에 남아 있으면
     이 문장대로 `attackPreset` 에 연결될 여지가 있다 — Plan.md 의 미결 사항(표 위치)과 관련된다.
3. **`BloomFairy_Attack.anim` 에는 Animation Event 가 하나도 없다**(60755행 `m_Events: []`, `OnAttackHit` 0건 — 실측).
   - 따라서 현재 꽃요정에게는 `OnAttackHit()` 가 불리지 않고, 그 안의 `PlayUnitAttack` / `PlayUnitAttackSfx` 도 꽃요정 몫으로는 실행되지 않는다.
   - 치료 자체는 `HitFrameTimes` 타이머로 동작하므로 영향이 없다(규칙 32, `_Tasks/2026-07-18/03_40_bloomfairy-healer/Plan.md` 110행 「`OnAttackHit` 미주입이어도 회복은 정상 동작」).
   - ⚠️ `GameSystemRules_Units.md` 규칙 27 의 「BloomFairy 는 … `OnAttackHit`은 힐 연출 전용으로 처리 완료」라는 문장이 **이벤트가 주입됐다는 뜻인지, 「연출 전용으로 쓰기로 정했다」는 뜻인지**는
     문장만으로 확정할 수 없다. 실측은 「미주입」이다. 이번 범위에서는 규칙 문서를 고치지 않고 사실만 기록한다(연결 작업에서 확인할 것).
4. **SFX 짝 규칙**: VFX 와 SFX 는 같은 메서드에서 연달아 호출해야 한다 — `GameSystemRules_Sound.md` 규칙 15. 연결 작업에서
   힐 이펙트를 켜는 지점에 `sfx_bloomfairy_heal` 호출(또는 「SFX 없음」 주석)이 함께 와야 한다. **SFX 가 한 번 울리는 소리인지 반복음인지는 아직 정해지지 않았다.**

---

## 5. 작업 중 발견한 부가 이슈 (이번 범위 밖 — 보고만)

| # | 내용 | 실측 |
|---|---|---|
| 1 | `VFXSFXList.md` 「1. 완성된 VFX」(10~12행)가 「현재 없음」인데, `Prefabs/VFX/Units/` 에는 프리팹 9개가 있다 | 위 3-2 표 |
| 2 | `VFXSFXList.md` 「1. 완성된 SFX」(221~223행)가 「현재 없음」인데, `Audio/SFX/Units/` 에는 `.wav` 3개가 있다 | 1절 |
| 3 | `GameSystemRules_Units.md` 규칙 27 의 BloomFairy 「처리 완료」 서술과 클립 실측(이벤트 0건)의 관계가 불명확하다 | 4절 3번 |

→ 셋 다 이번 작업(이름 변경)과 무관하므로 고치지 않는다(CLAUDE.md 규칙 6). 필요하면 별도 작업으로 제안한다.
