# VFX / SFX 에셋 목록

> **목적·답하는 질문:** 어떤 VFX/SFX가 제작·선택·연결됐고 어떤 시각·청각 수용과 제작 잔여가 남았는지 찾는 목록이다.
> **포함:** 실제 선택 프리팹·preset 연결, 제작·자동 검증·실기 수용의 구분, 미제작·보류·교체 대상과 짧은 근거 포인터를 담는다. 파일 존재나 자동 PASS만으로 실기 수용을 확정하지 않는다.
> **제외·담당 문서:** 제작 방법·배선 조건은 [VFXSFXGuide](VFXSFXGuide.md), 유닛별 구조 이행·개별 테스트 수용은 [전투 감사표](UnitCombatAssetMatrix.md), 수치는 [StatsReference](../StatsReference.md)가 담당한다. 해시 목록·컴파일·빌드 클릭·긴 경기 관측은 Task/History에 둔다.
> **권위원본·주요 참조:** 효과 에셋의 현황 목록이며 실제 파일·Inspector의 Config/preset 연결을 우선 대조한다. 피해·힐·동기화 규범은 [유닛 규칙](../GameSystemRules/GameSystemRules_Units.md)과 [전투 동기화 규칙](../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md)을 따른다.
> **갱신 조건·방법:** 제작·실제 연결·선정·사용자 수용 또는 보류 결정이 바뀌면 해당 행과 잔여 요약을 제자리 수정한다. 이전 미연결·대기 문단을 현행 설명 옆에 누적하지 않고 근거를 이력으로 연결한다.

- HumanTower는 별도 preset의 prefab 참조를 actual Assault VFX로 교체했다. Tank/CannonCart는 actual Tank를 유지한다. Human-only 변경의 정적 검증과 이전 세대 native PASS는 구분하며 새 native/실기 시각 수용은 미확인이다.
- Mushroom/Bloom/Inferno burn 새 VFX와 RuneSpire 타워 번개는 연결된 상태로 시각 후속이 남는다. 상태 이펙트(화상·독·힐)의 대상 유닛 크기 맞춤, RuneSpire 번개의 원래 맞은 유닛 끝점 추적·크리스털→가슴 조준, 차지 VFX의 시전자 사망 정지는 2026-10-13 구현됐다([Plan](../_Tasks/2026-10-13/13_53_vfx-code-followups/Plan.md)). 차지 정지·크리스털 시작점·Bloom 힐(PS 4개) 재생·살아 있는 대상의 번개 가슴 높이 조준은 사용자 확인 PASS이고, 측정 도구 스케일 버그 수정(재작업 R3)·재측정 뒤 상태 이펙트 크기·높이는 프리셋 값으로 의도대로 조정된다(값 미세 조정은 사용자가 계속 — 데이터 작업). ⚠️ **OPEN: 맞는 순간 대상이 죽으면 번개 끝점이 살짝 어긋난다**(원인 미검증, [Research](../_Tasks/2026-10-13/13_53_vfx-code-followups/Research.md) 10-4). Tank/Cannon 간헐 diagnostics는 보류다. *(2026-10-15 갱신: 종전 「R3 수정·재측정 전이라 상태 이펙트 크기와 번개 가슴 높이의 시각 수용은 미판정」은 3차 실기로 해소)* 최신 Inferno 타격 수치는 [StatsReference](../StatsReference.md)를 따른다. *(2026-10-13 갱신: 종전 「Inferno burn fitting·Spirit 원래 대상 endpoint … 보류」는 위 구현으로 해소)*

현재 연결의 작업 근거는 [Mushroom·Inferno·타워 연결](../_Tasks/2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Research.md)과 [Bloom VFX 연결](../_Tasks/2026-10-08/17_09_bloomfairy-heal-vfx-naming/Research.md)이다. 개별 사용자 수용과 이후 관측 범위는 [전투 감사표](UnitCombatAssetMatrix.md)에서 확인한다.

날짜별 인계와 이전 계약 원문: [상세 이력](../History/ImplementationVerificationHistory.md#vfx-list-handoff).

## Ⅰ. VFX (시각 이펙트)


<a id="2026-10-10-현재-source-freeze--정적native수용-경계"></a>
<a id="2026-10-10-확정-연결-계약과-진행-단계"></a>

현재 제작·연결·수용은 아래 표에서 확인하며 source inventory·동결·과거 진행 단계는 [source freeze 이력](../History/ImplementationVerificationHistory.md#vfx-list-source-freeze)과 [과거 계약](../History/ImplementationVerificationHistory.md#vfx-list-contract-history)에 둔다.

### 1. 제작 및 수용 상태

> 프리팹 실물은 존재한다. **제작됨 / 게임 연결됨 / 자동 검증 / 실기·visual 수용**은 별도 상태이며, 제작 완료를 게임 전체 PASS로 취급하지 않는다. 아래 목록의 행별 상태를 따른다.
> 현재 Mushroom tracer/impact/poison·Inferno burn·RuneSpire는 연결 구현됐으며(2026-10-13 실측으로 프리팹 → preset → 설정 자산 연결 확인 — 아래 「신규 프리팹 연결 실측 (2026-10-13 현행)」) 시각·실기 수용은 행별로 별도다. Bloom의 실제 양 peer 재생 관측은 전체 Client HP/text·픽셀 수용을 뜻하지 않는다. 과거 GUID 참조0·미연결/검증 대기 표시(2026-10-09 시점 값)는 [당시 요약](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-01) 및 아래 이력 포인터로 구분한다.

---

### 2. VFX 목록 — 제작·연결 상태 및 제작 예정

#### 유닛 공격 이펙트

> **2026-10-14 실측 연결 상태(프리팹 → 프리셋 → `UnitEffectConfig` 2단계 GUID 참조로 확인):** 공격 VFX가 실제로 연결된 유닛은 Pistoleer·Assault·Sniper(셋 다 `vfx_pistoleer_attack`), Tank·CannonCart(`vfx_tank_attack`), InfernoSpirit·FoxMagician(각 `_charge`), QuakeSpirit·StreamSpirit·TorrentSpirit, MushroomBomber(tracer·impact)뿐이다. 나머지 행(LittleKnight·SpearMan·BattleAxe·EmberSpirit·FlameSpirit·DustSpirit·BoulderSpirit·TideSpirit·BearGuard·LionKnight·RhinoBreaker·EagleArcher·RabbitTrickster)은 그 파일명의 프리팹이 없고 attackPreset도 비어 있는 **제작 예정** 행이다. 미사용 프리팹: `vfx_cannon_attack.prefab`(참조 0건). 연결은 시각 수용과 별개다.

| 종족 | 유닛명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | Pistoleer | `vfx_pistoleer_attack.prefab` | 권총 발사. *(2026-10-14 실측: 게임 연결됨 — `EffectPreset_Pistoleer_Attack` → `UnitEffectConfig` attackPreset. 같은 프리팹을 Assault·Sniper·Human 타워도 쓴다)* |
| Human | Assault | `vfx_assault_attack.prefab` | 돌격소총 연사. *(2026-10-14 실측: 이 이름의 프리팹은 없다. 실제 연결은 Pistoleer와 공유하는 `vfx_pistoleer_attack.prefab` — `EffectPreset_Pistoleer_Attack` → `UnitEffectConfig` attackPreset)* |
| Human | Sniper | `vfx_sniper_attack.prefab` | 저격 발사. *(2026-10-14 실측: 이 이름의 프리팹은 없다. 실제 연결은 Pistoleer와 공유하는 `vfx_pistoleer_attack.prefab` — `EffectPreset_Pistoleer_Attack` → `UnitEffectConfig` attackPreset)* |
| Human | LittleKnight | `vfx_littleknight_attack.prefab` | 검 베기 |
| Human | SpearMan | `vfx_spearman_attack.prefab` | 창 찌르기 |
| Human | BattleAxe | `vfx_battleaxe_attack.prefab` | 도끼 내려치기 |
| Human | Tank | `vfx_tank_attack.prefab` | shared Cannon preset의 actual prefab 연결(2026-10-14 실측: `EffectPreset_Cannon_Attack` → `UnitEffectConfig` Tank attackPreset. 같은 preset을 CannonCart도 쓴다). 개별 테스트 완료·간헐 VFX 및 세부 diagnostics 보류; 시각 후속과 전체 이행은 별도. [근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-03) |
| Human | CannonCart | `vfx_tank_attack.prefab` | Tank와 actual prefab 공유(`EffectPreset_Cannon_Attack` → `vfx_tank_attack.prefab`, `UnitEffectConfig` CannonCart attackPreset). HumanTower는 별도 actual Assault 적용(`EffectPreset_HumanAutoTower_Attack` → `vfx_pistoleer_attack.prefab`, 아래 「건물 공격 이펙트」). 간헐 VFX 후속 보류. *(2026-10-14 정정, 2026-10-19 병합 반영: 종전 「기존 `vfx_cannoncart_attack.prefab`은 제작/존재 이력이며 현재 미선택」은 사실이 아니다 — 저장소에 그 이름의 파일은 없고, 실제로 남아 있는 옛 프리팹은 `Prefabs/VFX/Units/vfx_cannon_attack.prefab`이며 **어느 preset·config·코드에서도 참조되지 않는다(미사용)**)* [근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-03) |
| Spirit | EmberSpirit | `vfx_emberspirit_attack.prefab` | 불 오브 발사 |
| Spirit | FlameSpirit | `vfx_flamespirit_attack.prefab` | 화염 방출 |
| Spirit | InfernoSpirit | `vfx_infernospirit_attack.prefab` | 인페르노 폭발. *(2026-10-13 실측: 실제 연결된 공격 VFX는 이 이름이 아니라 `vfx_infernospirit_charge.prefab`(차지, `EffectPreset_InfernoSpirit_Attack` → `UnitEffectConfig` attackPreset). 이 preset은 「Stop When Caster Dies」가 켜져 시전자 사망·제거 시 즉시 정지 — 사용자 확인 PASS 2026-10-13. 화상 상태 이펙트는 아래 「유닛 특수 효과 이펙트」)* |
| Spirit | DustSpirit | `vfx_dustspirit_attack.prefab` | 흙먼지 공격 |
| Spirit | BoulderSpirit | `vfx_boulderspirit_attack.prefab` | 바위 투척 |
| Spirit | QuakeSpirit | `vfx_quakespirit_attack.prefab` | 지진 광역. *(2026-10-14 실측: 게임 연결됨 — `EffectPreset_QuakeSpirit_Attack` → `UnitEffectConfig` attackPreset)* |
| Spirit | TideSpirit | `vfx_tidespirit_attack.prefab` | 물 튀김 |
| Spirit | StreamSpirit | `vfx_streamspirit_attack.prefab` | 물줄기. *(2026-10-14 실측: 게임 연결됨 — `EffectPreset_StreamSpirit_Attack` → `UnitEffectConfig` attackPreset)* |
| Spirit | TorrentSpirit | `vfx_torrentspirit_attack.prefab` | 급류. *(2026-10-14 실측: 게임 연결됨 — `EffectPreset_TorrentSpirit_Attack` → `UnitEffectConfig` attackPreset)* |
| Transcendence | FoxMagician | `vfx_foxmagician_attack.prefab` | 마법 섬광. *(2026-10-13 실측: 실제 연결된 공격 VFX는 이 이름이 아니라 `vfx_foxmagician_charge.prefab`(차지, `EffectPreset_FoxMagician_Attack` → `UnitEffectConfig` attackPreset). 이 preset은 「Stop When Caster Dies」가 켜져 시전자 사망·제거 시 즉시 정지 — 사용자 확인 PASS 2026-10-13)* |
| Transcendence | BearGuard | `vfx_bearguard_attack.prefab` | 강타 |
| Transcendence | LionKnight | `vfx_lionknight_attack.prefab` | 발톱 베기 |
| Transcendence | RhinoBreaker | `vfx_rhinobreaker_attack.prefab` | 돌진 충격파 |
| Transcendence | EagleArcher | `vfx_eaglearcher_attack.prefab` | 화살 발사 |
| Transcendence | RabbitTrickster | `vfx_rabbittrickster_attack.prefab` | 단검 베기 |
| Transcendence | MushroomBomber | `vfx_mushroombomber_tracer.prefab` | 연결 구현됨(2026-10-13 실측: `EffectPreset_MushroomBomber_Tracer` → `UnitEffectConfig` tracerPreset). 비행 동안 루프 재생하는 포자 폭탄. 서버가 확정한 공격 표현 스냅샷이 남은 비행 트레이서와 착탄을 함께 소유한다(10-10 연결). 새 VFX 시각 수용 OPEN — 트레이서 단독의 시각 수용은 별도 확인 기록 없음. [이전 연결 단계](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-04) |
| Transcendence | MushroomBomber | `vfx_mushroombomber_impact.prefab` | 연결 구현(2026-10-13 실측: `EffectPreset_MushroomBomber_Impact` → `UnitEffectConfig` impactPreset)·양 peer ImpactPlay 관측 · **사용자 시각 승인(2026-10-13 1차 실기, [Research](../_Tasks/2026-10-13/13_53_vfx-code-followups/Research.md) 8-2 T-3)**. 짧은 포자 burst/fade·nonloop·StopAction None. 과거 독구름 표현은 superseded; 3초 지면 damage zone은 없고 recipient DoT3초 유지. 정밀 visual·전체 lifecycle 수용 OPEN. [이전 연결 단계](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-04) |

#### 유닛 사망 이펙트

> **2026-10-14 실측:** 아래 유닛별 파일은 아직 없다. 현재 25종 모두 공용 `EffectPreset_Unit_Death_Common` → `vfx_unit_death.prefab`(`UnitEffectConfig` deathPreset)을 쓴다.

| 종족 | 유닛명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | Pistoleer | `vfx_pistoleer_death.prefab` | |
| Human | Assault | `vfx_assault_death.prefab` | |
| Human | Sniper | `vfx_sniper_death.prefab` | |
| Human | LittleKnight | `vfx_littleknight_death.prefab` | |
| Human | SpearMan | `vfx_spearman_death.prefab` | |
| Human | BattleAxe | `vfx_battleaxe_death.prefab` | |
| Human | Tank | `vfx_tank_death.prefab` | |
| Human | CannonCart | `vfx_cannoncart_death.prefab` | *(2026-10-14 정정: 이 행의 「HumanTower … 별도 Assault 교체 예정」은 이미 적용됐다 — Human 타워는 별도 `EffectPreset_HumanAutoTower_Attack` → `vfx_pistoleer_attack.prefab`(아래 「건물 공격 이펙트」). 실제 CannonCart 연결은 Tank와 같은 `EffectPreset_Cannon_Attack` → `vfx_tank_attack.prefab`(`UnitEffectConfig` CannonCart attackPreset). 「기존 `vfx_cannoncart_attack.prefab` 제작/존재」는 사실이 아니다 — 저장소에 그 이름의 파일은 없고, 실제로 남아 있는 옛 프리팹은 `Prefabs/VFX/Units/vfx_cannon_attack.prefab`이며 **어느 preset·config·코드에서도 참조되지 않는다(미사용)**)* |
| Spirit | EmberSpirit | `vfx_emberspirit_death.prefab` | |
| Spirit | FlameSpirit | `vfx_flamespirit_death.prefab` | |
| Spirit | InfernoSpirit | `vfx_infernospirit_death.prefab` | |
| Spirit | DustSpirit | `vfx_dustspirit_death.prefab` | |
| Spirit | BoulderSpirit | `vfx_boulderspirit_death.prefab` | |
| Spirit | QuakeSpirit | `vfx_quakespirit_death.prefab` | |
| Spirit | TideSpirit | `vfx_tidespirit_death.prefab` | |
| Spirit | StreamSpirit | `vfx_streamspirit_death.prefab` | |
| Spirit | TorrentSpirit | `vfx_torrentspirit_death.prefab` | |
| Transcendence | FoxMagician | `vfx_foxmagician_death.prefab` | |
| Transcendence | BearGuard | `vfx_bearguard_death.prefab` | |
| Transcendence | LionKnight | `vfx_lionknight_death.prefab` | |
| Transcendence | RhinoBreaker | `vfx_rhinobreaker_death.prefab` | |
| Transcendence | EagleArcher | `vfx_eaglearcher_death.prefab` | |
| Transcendence | RabbitTrickster | `vfx_rabbittrickster_death.prefab` | |
| Transcendence | MushroomBomber | `vfx_mushroombomber_death.prefab` | |
| Transcendence | BloomFairy | `vfx_bloomfairy_death.prefab` | |

#### 유닛 특수 효과 이펙트

> BloomFairy 치료는 아군 1명에게 거는 **지속 회복(HoT)**이다 — 규칙: [GameSystemRules_Units.md](../GameSystemRules/GameSystemRules_Units.md) **규칙 34**.
> 공격 순간 1회 재생되는 「유닛 공격 이펙트」와 달리 **치료받는 아군에 붙어 회복이 이어지는 동안 루프 재생**되므로 이 절에 따로 둔다.
> 꽃요정의 `attackPreset` 칸에 연결하지 않는다 — 그 칸은 치료하는 꽃요정 자신의 위치에서 1회 재생한다.
> MushroomBomber 독 상태 이펙트는 착탄 반경 안에서 **지속 피해**(DoT)를 받는 **적 유닛 각각에 붙어** 피해가 이어지는 동안 루프 재생된다 — 규칙: `GameSystemRules_Units.md` 규칙 34 · 규칙 40. 착탄 지점의 폭발·독구름(1회 재생)은 「유닛 공격 이펙트」 표에 있다.
> ⚠️ 버섯폭격기의 `attackPreset`·`hitPreset` 칸에 연결하지 않는다 — `hitPreset` 은 **맞은 유닛의 종류**로 고르는 칸이라 버섯폭격기가 **맞았을 때** 재생된다.
> InfernoSpirit 화상 상태 이펙트도 같은 부류다 — 단일 대상 on-hit DoT를 받는 적 유닛에 붙어 피해가 이어지는 동안 루프 재생된다(규칙: `GameSystemRules_Units.md` 규칙 34 · 규칙 41). 화상·독은 `UnitEffectConfig`의 `timedDamagePreset`, 힐은 `healPreset` 칸에 연결돼 있다(2026-10-13 실측).
> **2026-10-13 대상 크기 맞춤:** 세 이펙트는 붙는 유닛의 크기 표(`UnitEffectConfig` 「상태 이펙트 크기 맞춤」, 에디터 메뉴 `Hexiege/Effects/Measure Unit Recipient Fit`으로 채움)에서 정한 균등 배율로 크기가 바뀐다. 측정 도구 스케일 버그(사람형 유닛에서 약 5배)는 재작업 R3(커밋 `84b6ff2a`)에서 고쳤고, 사용자가 재측정한 표로 세 이펙트의 크기·높이가 프리셋 `Local Position Offset` · `Scale Multiplier`로 의도대로 조정됨을 확인했다(2026-10-15 3차 실기, [Research](../_Tasks/2026-10-13/13_53_vfx-code-followups/Research.md) 10절). 재측정 표와 현재 프리셋 값은 커밋 `91371e55`(사용자)에 있다 — Pistoleer 키 0.342 · 최대 키 약 0.53 · 최대 반경 약 0.41 · `manualScale` 없음, Heal·Poison Scale Multiplier 2 · Burn 1 · 오프셋 0(같은 Research 10-3). 값 미세 조정은 사용자가 계속한다. *(2026-10-15 정정: 첫 판의 「`84b6ff2a` 표는 아직 옛 값 — 재측정분은 커밋 전으로 보인다」는 `91371e55`로 해소)* *(2026-10-15 갱신: 종전 「R3 수정·재측정 전에는 크기 시각 수용을 판정하지 않는다」는 해소)*

| 종족 | 유닛명 | 파일명 | 비고 |
|------|--------|--------|------|
| Spirit | InfernoSpirit | `vfx_infernospirit_burn.prefab` | 연결 구현됨(2026-10-13 실측: `EffectPreset_InfernoSpirit_Burn` → `UnitEffectConfig` timedDamagePreset). attack(차지) VFX와 별도 recipient burn loop; DoT 부여·refresh·종료 표현. PS 2개(루트·`Flame_Haze`). **프리팹 모양 사용자 승인 · R3 재측정 뒤 프리셋으로 위치·크기 조정됨 확인(2026-10-15 3차 실기) / 값 미세 조정 사용자 진행 중** · 전체 실기 수용 별도. *(2026-10-15 갱신, 2026-10-19 병합 반영: 종전 「fitting/크기 조정 보류」 및 「위치·크기 조정 필요(1차 실기 「더 아래·조금 더 작게」) / R3 재측정 뒤 재확인」)* [근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-05) |
| Transcendence | MushroomBomber | `vfx_mushroombomber_poison.prefab` | 연결 구현됨(2026-10-13 실측: `EffectPreset_MushroomBomber_Poison` → `UnitEffectConfig` timedDamagePreset). **프리팹 사용자 재제작(커밋 `10a38cc0`, PS 4개) · 프리팹 사용자 승인(2026-10-15) · R3 재측정 뒤 프리셋으로 크기·높이 조정됨 확인(3차 실기) / 값 미세 조정 사용자 진행 중** *(2026-10-15 갱신: 종전 「모양 OK(2차 실기) / 유닛 위 크기·높이 과대 — R3 재측정 뒤 재확인」 — 과대는 측정 도구 버그 탓이었다)*. "이 유닛이 중독됐다"를 보여 주는, DoT 대상 적 유닛 중심에 모여 따라가는 loop(1차 실기의 「주변 칸으로 퍼짐」은 재제작으로 대체). DoT 만료·대상 사망 시 방출 정지(남은 입자는 자연 소멸), 재부여 시 끊지 않고 유지 — same-owner refresh noRestart·overwrite 종료교체·drain/lease 계약 유지; 조기 Stop·forced Teardown 시간 경계와 정밀 visual 수용 OPEN. 건물에는 붙지 않는다(건물 DoT 신설 없음). [근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-05) |
| Transcendence | BloomFairy | `vfx_bloomfairy_heal.prefab` | healPreset→recipient VFX 연결됨. 전 PS loop·동일 HoT refresh noRestart·종료 StopEmitting/drain/lease. native self-validation과 양 peer 관측은 확보했지만 강제 cleanup은 자연 종료 PASS가 아니다. Client NotStarted는 수신 후 LocalDeath 취소로 확인(미수신·MissingAsset 아님). **3차 실기(2026-10-15)에서 PS 4개 프리팹 힐 재생 사용자 PASS, R3 재측정 뒤 크기·높이가 프리셋으로 조정됨 확인 — 값 미세 조정은 사용자 진행 중.** 전체·visual 수용 OPEN 중 이 두 가지만 닫혔고 Client C3 failures64 등 나머지 축은 그대로다([Research](../_Tasks/2026-10-13/13_53_vfx-code-followups/Research.md) 10절). *(2026-10-13 갱신: 프리팹은 사용자 결정으로 `Bloom_Ring`을 빼 **PS 4개**이며, 코드의 「정확히 5개」 검사는 「1개 이상 + 시스템별 계약」으로 완화됐다(커밋 `d3f9abb1`). 종전 「all5PS」 표기는 옛 프리팹 기준. 1차 실기 「크기는 괜찮지만 너무 높이 올라간다」 → R2-3 조정 → R3 재측정 뒤 위 확인)* [관측 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-02) |

#### Bloom HoT 최신 관측 근거

recipient HoT의 writer→current-state replication→presenter 연결과 양 peer playback/종료 관측을 확보했다. 동일 owner refresh는 재시작하지 않으며 종료 시 StopEmitting·tail drain·lease 반환을 구분한다. revision 전환·수신 후 LocalDeath 취소는 미수신/에셋 누락으로 세지 않는다. forced Teardown은 natural drain PASS가 아니고 local ROOT PASS는 formal CrossAudit가 아니다. Client C3 실패·clock 인과와 전체 visual·HP/text 수용은 별도 OPEN이다. 이후 관측과 잔여는 [전투 감사표](UnitCombatAssetMatrix.md)를 따른다. 상세 수치·raw 경로·행 근거는 [관측 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-02)에 보존한다.

<a id="신규-프리팹-연결-실측-2026-10-09"></a>
<a id="bloom-자동-검증빌드초기-fail-이력"></a>

당시 GUID 참조0·초기 fixture 실패·native 검증·빌드 관측은 [관측 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-02)에 보존한다. 현재 미연결 상태나 새 빌드 성공 판정으로 사용하지 않는다.

#### 신규 프리팹 연결 실측 (2026-10-13 현행)

> **[2026-10-13 갱신 — 2026-10-09 실측(5개 GUID 참조 0건) 원문은 위 관측 원문에 보존]** 같은 방법(`.meta` GUID를 `Assets` 안 `.asset/.unity/.prefab/.cs`에서 검색)으로 다시 재면 다섯 GUID가 모두 각 preset에서 참조되고, 그 preset들은 다시 설정 자산에서 참조된다 — 즉 **현재는 모두 게임 연결 상태**다. native 재생·시각 수용 판정은 각 행 비고를 따른다.
>
> | 프리팹 | 참조하는 preset | preset을 참조하는 설정 칸 |
> |---|---|---|
> | Mushroom tracer | `EffectPreset_MushroomBomber_Tracer` | `UnitEffectConfig` MushroomBomber tracerPreset |
> | Mushroom impact | `EffectPreset_MushroomBomber_Impact` | `UnitEffectConfig` MushroomBomber impactPreset |
> | Mushroom poison | `EffectPreset_MushroomBomber_Poison` | `UnitEffectConfig` MushroomBomber timedDamagePreset |
> | Inferno burn | `EffectPreset_InfernoSpirit_Burn` | `UnitEffectConfig` InfernoSpirit timedDamagePreset |
> | RuneSpire attack | `EffectPreset_RuneSpire_Attack` | `BuildingEffectConfig` AutoTower 공격 목록의 Spirit(race 1) 행 |

#### 건물 공격 이펙트

| 종족 | 건물명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | CannonTower (AutoTower + Human) | actual Assault production `vfx_pistoleer_attack` 재사용 | 별도 preset의 prefab 참조만 교체. Tank/Cannon=actual Tank, SFX/offset 불변. 정적 검증과 이전 세대 native를 구분하며 새 native·실기 visual 미확인. [근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-06) |
| Spirit | RuneSpire | `vfx_runespire_attack.prefab` | 연결 구현됨(2026-10-13 실측: `EffectPreset_RuneSpire_Attack` → `BuildingEffectConfig` AutoTower Spirit 행). **크리스털 시작점 사용자 확인 PASS(2026-10-13 2차 실기) · 살아 있는 대상 가슴 높이 조준 PASS(2026-10-15 3차 실기) / ⚠️ OPEN: 맞는 순간 대상이 죽으면 끝점이 살짝 어긋남(원인 미검증 — [Research](../_Tasks/2026-10-13/13_53_vfx-code-followups/Research.md) 10-4)** *(2026-10-15 갱신: 종전 「가슴 높이 조준은 R3 재측정 뒤 판정」. 2026-10-19 병합 반영: 종전 「원래 피격 대상 endpoint 추적 … 별도 후속/보류」는 2026-10-13 구현으로 해소)* — 타워 공격 번개. 2026-10-13부터 번개가 보이는 동안 **원래 맞은 유닛만** 화면 표현으로 따라가고(사망·제거·교체 시 마지막 지점에 고정, 다른 유닛으로 옮겨 가지 않음), 시작점 = 타워 + preset 위치 보정(현재 Y 0.65 = 크리스털 높이), 끝점 = 맞은 유닛 발밑 + 가슴 높이(크기 표의 키 × 0.6, 표에 없으면 0.6m), 조준·길이는 3D. 규칙: `GameSystemRules_Buildings.md` 방어 타워 시스템 규칙 12의 2026-10-13 블록. 기존 즉발 서버 피해·타겟 선택 불변. 정밀 시각 수용은 위 OPEN 포함 별도이며 SFX 미제작. [근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-assets-vfx-06) |
| Transcendence | VineTower (AutoTower + Transcendence) | 미지정 → **계획: `vfx_vinetower_attack.prefab` (제안 파일명, 미착수 — 아래 블록)** | 타 종족 fallback 금지. VFX 미지정이 기존 서버 피해를 차단·변경하지 않음. *(2026-10-15 실측: `BuildingEffectConfig.asset` `_autoTowerAttacks`에는 race 0(Human)·1(Spirit) 두 행뿐이고, `EffectManager.cs` 529행이 Transcendence 타워를 `UnspecifiedTranscendence`로 재생하지 않는다. `Prefabs/VFX/Buildings/`에 vinetower 공격 프리팹 없음 — 현재 공격 VFX 연결 0건)* |

> **VineTower 공격 VFX 계획 (2026-10-15 사용자 아이디어 기록 — 계획만, 미착수·미승인 구현):**
> - **채택 컨셉 (A) 가시 덩굴 솟구치기:** 대상 유닛 발밑 땅에서 가시 덩굴이 솟구쳐 대상을 휘감듯 내리치고 다시 땅속으로 들어간다. 흙·파편 파티클이 함께 튄다. [CommonAssetGuide](CommonAssetGuide.md)의 VineTower 「방어 타워 — 덩굴 지하 공격」 컨셉과 맞는다.
> - **구성:** 덩굴은 **애니메이션되는 메시 오브젝트**(솟구침 → 휘감기 → 들어감) + 흙·파편 **파티클**. 착탄형 이펙트이므로 대상 위치 지면에 놓는다(같은 가이드의 착탄 배치 원칙 — [VFXSFXGuide](VFXSFXGuide.md) 「Unity AI 프롬프트 작성 규칙 — 코드 계약」).
> - ⚠️ **코드 선행 조건:** 현재 풀은 **파티클이 모두 사라지면** 아이템을 반환한다(`VfxPoolItem.cs` 548행 `AnyAlive()` 기준, 494행 반환 판정). 애니메이션 메시가 파티클보다 오래 보여야 하면 그 전에 반환돼 잘리므로, **애니메이션 길이를 반환 시점에 반영하는 방법**(예: 애니메이션 종료 신호 또는 고정 수명)이 코드에 필요하다 — 설계·승인 대상. 같은 494행의 파도 경로(`_waveDriven`이면 `_wave.Duration`이 지날 때까지 반환하지 않음)가 「정해진 수명만큼 유지」의 기존 선례다. 또 Transcendence 타워 행을 `BuildingEffectConfig`에 추가하면 `EffectManager.cs` 529행의 「Transcendence 미지정」 분기와 「방어 타워 시스템」 규칙 12의 Transcendence 미지정 서술도 함께 바뀐다(규칙 변경은 사용자 승인 후).
> - **기각안 (B) 가시 씨앗 투사체:** 타워에서 가시 씨앗을 쏘아 날리는 방식 — 덩굴 지하 공격 컨셉과 맞지 않아 채택하지 않았다.
> - SFX(`sfx_vinetower_attack` 계열)는 아직 정하지 않았다(`GameSystemRules_Sound.md` 규칙 15 — VFX 연결 시 SFX 쌍 또는 「미제작」 주석).

#### 건물 파괴 이펙트

| 종족 | 건물명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | Castle | `vfx_castle_destroy.prefab` | 본기지 |
| Human | MiningPost | `vfx_miningpost_destroy.prefab` | |
| Human | CannonTower | `vfx_cannontower_destroy.prefab` | |
| Human | FlightFacility | `vfx_flightfacility_destroy.prefab` | |
| Human | TechnicalLaboratory | `vfx_technicallaboratory_destroy.prefab` | |
| Human | TrainingCamp | `vfx_trainingcamp_destroy.prefab` | |
| Human | WarAcademy | `vfx_waracademy_destroy.prefab` | |
| Human | Barracks | `vfx_barracks_destroy.prefab` | |
| Human | Gunsmith | `vfx_gunsmith_destroy.prefab` | |
| Human | Armory | `vfx_armory_destroy.prefab` | |
| Human | WeaponForge | `vfx_weaponforge_destroy.prefab` | |
| Human | Garage | `vfx_garage_destroy.prefab` | |
| Human | VehicleBay | `vfx_vehiclebay_destroy.prefab` | |
| Spirit | SpiritNexus | `vfx_spiritnexus_destroy.prefab` | 본기지 |
| Spirit | ManaRift | `vfx_manarift_destroy.prefab` | |
| Spirit | RuneSpire | `vfx_runespire_destroy.prefab` | |
| Spirit | MagicSpirit | `vfx_magicspirit_destroy.prefab` | |
| Spirit | AstronomicalSpirit | `vfx_astronomicalspirit_destroy.prefab` | |
| Spirit | FireSpire | `vfx_firespire_destroy.prefab` | |
| Spirit | BlazeConduit | `vfx_blazeconduit_destroy.prefab` | |
| Spirit | InfernoCore | `vfx_infernocore_destroy.prefab` | |
| Spirit | AquaSpring | `vfx_aquaspring_destroy.prefab` | |
| Spirit | TidalNexus | `vfx_tidalnexus_destroy.prefab` | |
| Spirit | OceanicHeart | `vfx_oceanicheart_destroy.prefab` | |
| Spirit | StoneMound | `vfx_stonemound_destroy.prefab` | |
| Spirit | TerraForge | `vfx_terraforge_destroy.prefab` | |
| Spirit | GaeaSanctum | `vfx_gaeasanctum_destroy.prefab` | |
| Transcendence | ElderTree | `vfx_eldertree_destroy.prefab` | 본기지 |
| Transcendence | FungalNode | `vfx_fungalnode_destroy.prefab` | |
| Transcendence | VineTower | `vfx_vinetower_destroy.prefab` | |
| Transcendence | MistShrine | `vfx_mistshrine_destroy.prefab` | |
| Transcendence | WillowShrine | `vfx_willowshrine_destroy.prefab` | |
| Transcendence | AncientGrove | `vfx_ancientgrove_destroy.prefab` | |
| Transcendence | PrimalAltar | `vfx_primalaltar_destroy.prefab` | |
| Transcendence | PrimalDen | `vfx_primalden_destroy.prefab` | |
| Transcendence | PrimalSanctuary | `vfx_primalsanctuary_destroy.prefab` | |
| Transcendence | FeralAltar | `vfx_feralaltar_destroy.prefab` | |
| Transcendence | FeralDen | `vfx_feralden_destroy.prefab` | |
| Transcendence | FeralSanctuary | `vfx_feralsanctuary_destroy.prefab` | |
| Transcendence | SporePatch | `vfx_sporepatch_destroy.prefab` | |
| Transcendence | FloralNursery | `vfx_floralnursery_destroy.prefab` | |

#### 건물 업그레이드 이펙트

> 업그레이드 가능한 건물에만 적용. 해당하지 않는 항목은 삭제.

| 종족 | 건물명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | Castle | `vfx_castle_upgrade.prefab` | |
| Human | MiningPost | `vfx_miningpost_upgrade.prefab` | |
| Human | CannonTower | `vfx_cannontower_upgrade.prefab` | |
| Human | FlightFacility | `vfx_flightfacility_upgrade.prefab` | |
| Human | TechnicalLaboratory | `vfx_technicallaboratory_upgrade.prefab` | |
| Human | TrainingCamp | `vfx_trainingcamp_upgrade.prefab` | |
| Human | WarAcademy | `vfx_waracademy_upgrade.prefab` | |
| Human | Barracks | `vfx_barracks_upgrade.prefab` | |
| Human | Gunsmith | `vfx_gunsmith_upgrade.prefab` | |
| Human | Armory | `vfx_armory_upgrade.prefab` | |
| Human | WeaponForge | `vfx_weaponforge_upgrade.prefab` | |
| Human | Garage | `vfx_garage_upgrade.prefab` | |
| Human | VehicleBay | `vfx_vehiclebay_upgrade.prefab` | |
| Spirit | SpiritNexus | `vfx_spiritnexus_upgrade.prefab` | |
| Spirit | ManaRift | `vfx_manarift_upgrade.prefab` | |
| Spirit | RuneSpire | `vfx_runespire_upgrade.prefab` | |
| Spirit | MagicSpirit | `vfx_magicspirit_upgrade.prefab` | |
| Spirit | AstronomicalSpirit | `vfx_astronomicalspirit_upgrade.prefab` | |
| Spirit | FireSpire | `vfx_firespire_upgrade.prefab` | |
| Spirit | BlazeConduit | `vfx_blazeconduit_upgrade.prefab` | |
| Spirit | InfernoCore | `vfx_infernocore_upgrade.prefab` | |
| Spirit | AquaSpring | `vfx_aquaspring_upgrade.prefab` | |
| Spirit | TidalNexus | `vfx_tidalnexus_upgrade.prefab` | |
| Spirit | OceanicHeart | `vfx_oceanicheart_upgrade.prefab` | |
| Spirit | StoneMound | `vfx_stonemound_upgrade.prefab` | |
| Spirit | TerraForge | `vfx_terraforge_upgrade.prefab` | |
| Spirit | GaeaSanctum | `vfx_gaeasanctum_upgrade.prefab` | |
| Transcendence | ElderTree | `vfx_eldertree_upgrade.prefab` | |
| Transcendence | FungalNode | `vfx_fungalnode_upgrade.prefab` | |
| Transcendence | VineTower | `vfx_vinetower_upgrade.prefab` | |
| Transcendence | MistShrine | `vfx_mistshrine_upgrade.prefab` | |
| Transcendence | WillowShrine | `vfx_willowshrine_upgrade.prefab` | |
| Transcendence | AncientGrove | `vfx_ancientgrove_upgrade.prefab` | |
| Transcendence | PrimalAltar | `vfx_primalaltar_upgrade.prefab` | |
| Transcendence | PrimalDen | `vfx_primalden_upgrade.prefab` | |
| Transcendence | PrimalSanctuary | `vfx_primalsanctuary_upgrade.prefab` | |
| Transcendence | FeralAltar | `vfx_feralaltar_upgrade.prefab` | |
| Transcendence | FeralDen | `vfx_feralden_upgrade.prefab` | |
| Transcendence | FeralSanctuary | `vfx_feralsanctuary_upgrade.prefab` | |
| Transcendence | SporePatch | `vfx_sporepatch_upgrade.prefab` | |
| Transcendence | FloralNursery | `vfx_floralnursery_upgrade.prefab` | |

#### 건물 특수 효과 이펙트

> MistShrine 물안개 힐은 **2026-08-12 기준 로직·UI 구현 완료**(에디터 싱글플레이 실기 검증 완료 · 멀티 미검증)이지만, 아래 이펙트는 **여전히 모두 미제작**이다.
> 즉 **물안개는 동작하되 눈에 보이지 않는 상태**이며, 이 이펙트 제작이 남은 작업이다.
> 규칙: [`GameSystemRules/GameSystemRules_Buildings.md`](../GameSystemRules/GameSystemRules_Buildings.md) **MistShrine 물안개 힐 시스템 규칙 26**.
> 물안개는 **뿌리는 순간**(1회)과 **지속 중**(루프)의 두 국면이 있어 이펙트를 분리한다.

| 종족 | 건물명 | 파일명 | 비고 |
|------|--------|--------|------|
| Transcendence | MistShrine | `vfx_mistshrine_mist_cast.prefab` | **미제작** — 물안개 시전 순간 1회 재생 |
| Transcendence | MistShrine | `vfx_mistshrine_mist_loop.prefab` | **미제작** — 물안개 지속 중 루프 재생. 건물 중심 고정(이동하지 않음), 지속시간 종료·시전 건물 파괴 시 즉시 정지 |

> **범위 표시 반투명 원형 UI는 이 문서 대상이 아니다.** VFX가 아니라 UI/셰이더 영역이며,
> 기존 지면 데칼 셰이더 `Assets/_Project/Shaders/SkillAimOverlay.shader` 재사용을 우선 검토한다(같은 문서 규칙 27).

#### 골드 채집 이펙트

| 오브젝트 | 파일명 | 비고 |
|---------|--------|------|
| GoldMine | `vfx_goldmine_harvest.prefab` | 루프 재생 |

#### UI 이펙트

| 이펙트 | 파일명 | 비고 |
|--------|--------|------|
| 버튼 클릭 | `vfx_ui_button_click.prefab` | |
| 버튼 확인 | `vfx_ui_button_confirm.prefab` | |
| 골드 획득 | `vfx_ui_gold_gain.prefab` | |
| 타이머 완료 | `vfx_ui_timer_complete.prefab` | |

#### 타일 점령 이펙트

| 팀 | 파일명 | 비고 |
|----|--------|------|
| Blue | `vfx_tile_capture_blue.prefab` | |
| Red | `vfx_tile_capture_red.prefab` | |

#### 게임 이벤트 이펙트

| 이벤트 | 파일명 | 비고 |
|--------|--------|------|
| 게임 시작 | `vfx_game_start.prefab` | |
| 승리 | `vfx_game_victory.prefab` | |
| 패배 | `vfx_game_defeat.prefab` | |

---

## Ⅱ. SFX (사운드 이펙트)

### 1. 완성된 SFX

> 현재 없음

---

### 2. 제작 예정 SFX

#### 유닛 공격 사운드

| 종족 | 유닛명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | Pistoleer | `sfx_pistoleer_attack.wav` | 권총 발사음 |
| Human | Assault | `sfx_assault_attack.wav` | 돌격소총 연사음 |
| Human | Sniper | `sfx_sniper_attack.wav` | 저격 발사음 |
| Human | LittleKnight | `sfx_littleknight_attack.wav` | 검 베는 소리 |
| Human | SpearMan | `sfx_spearman_attack.wav` | 창 찌르는 소리 |
| Human | BattleAxe | `sfx_battleaxe_attack.wav` | 도끼 내려치는 소리 |
| Human | Tank | `sfx_tank_attack.wav` | 포탄 발사음 |
| Human | CannonCart | `sfx_cannoncart_attack.wav` | 범위 포격음 *(2026-10-14 정정: 이 행의 「HumanTower … 별도 Assault 교체 예정」은 이미 적용됐다 — Human 타워는 별도 `EffectPreset_HumanAutoTower_Attack` → `vfx_pistoleer_attack.prefab`(아래 「건물 공격 이펙트」). 실제 CannonCart 연결은 Tank와 같은 `EffectPreset_Cannon_Attack` → `vfx_tank_attack.prefab`(`UnitEffectConfig` CannonCart attackPreset). 「기존 `vfx_cannoncart_attack.prefab` 제작/존재」는 사실이 아니다 — 저장소에 그 이름의 파일은 없고, 실제로 남아 있는 옛 프리팹은 `Prefabs/VFX/Units/vfx_cannon_attack.prefab`이며 **어느 preset·config·코드에서도 참조되지 않는다(미사용)**)* |
| Spirit | EmberSpirit | `sfx_emberspirit_attack.wav` | 불 마법음 |
| Spirit | FlameSpirit | `sfx_flamespirit_attack.wav` | 화염 방출음 |
| Spirit | InfernoSpirit | `sfx_infernospirit_attack.wav` | 인페르노 폭발음 |
| Spirit | DustSpirit | `sfx_dustspirit_attack.wav` | 흙먼지 공격음 |
| Spirit | BoulderSpirit | `sfx_boulderspirit_attack.wav` | 바위 투척음 |
| Spirit | QuakeSpirit | `sfx_quakespirit_attack.wav` | 지진 광역음 |
| Spirit | TideSpirit | `sfx_tidespirit_attack.wav` | 물 튀기는 소리 |
| Spirit | StreamSpirit | `sfx_streamspirit_attack.wav` | 물줄기 소리 |
| Spirit | TorrentSpirit | `sfx_torrentspirit_attack.wav` | 급류 소리 |
| Transcendence | FoxMagician | `sfx_foxmagician_attack.wav` | 마법 섬광음 |
| Transcendence | BearGuard | `sfx_bearguard_attack.wav` | 강타음 |
| Transcendence | LionKnight | `sfx_lionknight_attack.wav` | 발톱 베는 소리 |
| Transcendence | RhinoBreaker | `sfx_rhinobreaker_attack.wav` | 돌진 충격음 |
| Transcendence | EagleArcher | `sfx_eaglearcher_attack.wav` | 화살 발사음 |
| Transcendence | RabbitTrickster | `sfx_rabbittrickster_attack.wav` | 단검 베는 소리 |
| Transcendence | MushroomBomber | `sfx_mushroombomber_attack.wav` | 포자 폭발음 |

#### 유닛 사망 사운드

| 종족 | 유닛명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | Pistoleer | `sfx_pistoleer_death.wav` | |
| Human | Assault | `sfx_assault_death.wav` | |
| Human | Sniper | `sfx_sniper_death.wav` | |
| Human | LittleKnight | `sfx_littleknight_death.wav` | |
| Human | SpearMan | `sfx_spearman_death.wav` | |
| Human | BattleAxe | `sfx_battleaxe_death.wav` | |
| Human | Tank | `sfx_tank_death.wav` | |
| Human | CannonCart | `sfx_cannoncart_death.wav` | *(2026-10-14 정정: 이 행의 「HumanTower … 별도 Assault 교체 예정」은 이미 적용됐다 — Human 타워는 별도 `EffectPreset_HumanAutoTower_Attack` → `vfx_pistoleer_attack.prefab`(아래 「건물 공격 이펙트」). 실제 CannonCart 연결은 Tank와 같은 `EffectPreset_Cannon_Attack` → `vfx_tank_attack.prefab`(`UnitEffectConfig` CannonCart attackPreset). 「기존 `vfx_cannoncart_attack.prefab` 제작/존재」는 사실이 아니다 — 저장소에 그 이름의 파일은 없고, 실제로 남아 있는 옛 프리팹은 `Prefabs/VFX/Units/vfx_cannon_attack.prefab`이며 **어느 preset·config·코드에서도 참조되지 않는다(미사용)**)* |
| Spirit | EmberSpirit | `sfx_emberspirit_death.wav` | |
| Spirit | FlameSpirit | `sfx_flamespirit_death.wav` | |
| Spirit | InfernoSpirit | `sfx_infernospirit_death.wav` | |
| Spirit | DustSpirit | `sfx_dustspirit_death.wav` | |
| Spirit | BoulderSpirit | `sfx_boulderspirit_death.wav` | |
| Spirit | QuakeSpirit | `sfx_quakespirit_death.wav` | |
| Spirit | TideSpirit | `sfx_tidespirit_death.wav` | |
| Spirit | StreamSpirit | `sfx_streamspirit_death.wav` | |
| Spirit | TorrentSpirit | `sfx_torrentspirit_death.wav` | |
| Transcendence | FoxMagician | `sfx_foxmagician_death.wav` | |
| Transcendence | BearGuard | `sfx_bearguard_death.wav` | |
| Transcendence | LionKnight | `sfx_lionknight_death.wav` | |
| Transcendence | RhinoBreaker | `sfx_rhinobreaker_death.wav` | |
| Transcendence | EagleArcher | `sfx_eaglearcher_death.wav` | |
| Transcendence | RabbitTrickster | `sfx_rabbittrickster_death.wav` | |
| Transcendence | MushroomBomber | `sfx_mushroombomber_death.wav` | |
| Transcendence | BloomFairy | `sfx_bloomfairy_death.wav` | |

#### 유닛 특수 효과 사운드

> 사운드 규칙 15(VFX + SFX 쌍 호출)에 따라 위 **유닛 특수 효과 이펙트**와 대응한다. 현재 SFX 없음은 같은 규칙의 주석 명시 허용으로 보류한다.

| 종족 | 유닛명 | 파일명 | 비고 |
|------|--------|--------|------|
| Transcendence | BloomFairy | `sfx_bloomfairy_heal.wav` | **미제작** — `vfx_bloomfairy_heal`과 쌍. 1회음/반복음 여부 미정; 현재 결정 불필요. VFX 연동 시 SFX 없음 주석 명시로 보류 |

#### 건물 파괴 사운드

| 종족 | 건물명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | Castle | `sfx_castle_destroy.wav` | |
| Human | MiningPost | `sfx_miningpost_destroy.wav` | |
| Human | CannonTower | `sfx_cannontower_destroy.wav` | |
| Human | FlightFacility | `sfx_flightfacility_destroy.wav` | |
| Human | TechnicalLaboratory | `sfx_technicallaboratory_destroy.wav` | |
| Human | TrainingCamp | `sfx_trainingcamp_destroy.wav` | |
| Human | WarAcademy | `sfx_waracademy_destroy.wav` | |
| Human | Barracks | `sfx_barracks_destroy.wav` | |
| Human | Gunsmith | `sfx_gunsmith_destroy.wav` | |
| Human | Armory | `sfx_armory_destroy.wav` | |
| Human | WeaponForge | `sfx_weaponforge_destroy.wav` | |
| Human | Garage | `sfx_garage_destroy.wav` | |
| Human | VehicleBay | `sfx_vehiclebay_destroy.wav` | |
| Spirit | SpiritNexus | `sfx_spiritnexus_destroy.wav` | |
| Spirit | ManaRift | `sfx_manarift_destroy.wav` | |
| Spirit | RuneSpire | `sfx_runespire_destroy.wav` | |
| Spirit | MagicSpirit | `sfx_magicspirit_destroy.wav` | |
| Spirit | AstronomicalSpirit | `sfx_astronomicalspirit_destroy.wav` | |
| Spirit | FireSpire | `sfx_firespire_destroy.wav` | |
| Spirit | BlazeConduit | `sfx_blazeconduit_destroy.wav` | |
| Spirit | InfernoCore | `sfx_infernocore_destroy.wav` | |
| Spirit | AquaSpring | `sfx_aquaspring_destroy.wav` | |
| Spirit | TidalNexus | `sfx_tidalnexus_destroy.wav` | |
| Spirit | OceanicHeart | `sfx_oceanicheart_destroy.wav` | |
| Spirit | StoneMound | `sfx_stonemound_destroy.wav` | |
| Spirit | TerraForge | `sfx_terraforge_destroy.wav` | |
| Spirit | GaeaSanctum | `sfx_gaeasanctum_destroy.wav` | |
| Transcendence | ElderTree | `sfx_eldertree_destroy.wav` | |
| Transcendence | FungalNode | `sfx_fungalnode_destroy.wav` | |
| Transcendence | VineTower | `sfx_vinetower_destroy.wav` | |
| Transcendence | MistShrine | `sfx_mistshrine_destroy.wav` | |
| Transcendence | WillowShrine | `sfx_willowshrine_destroy.wav` | |
| Transcendence | AncientGrove | `sfx_ancientgrove_destroy.wav` | |
| Transcendence | PrimalAltar | `sfx_primalaltar_destroy.wav` | |
| Transcendence | PrimalDen | `sfx_primalden_destroy.wav` | |
| Transcendence | PrimalSanctuary | `sfx_primalsanctuary_destroy.wav` | |
| Transcendence | FeralAltar | `sfx_feralaltar_destroy.wav` | |
| Transcendence | FeralDen | `sfx_feralden_destroy.wav` | |
| Transcendence | FeralSanctuary | `sfx_feralsanctuary_destroy.wav` | |
| Transcendence | SporePatch | `sfx_sporepatch_destroy.wav` | |
| Transcendence | FloralNursery | `sfx_floralnursery_destroy.wav` | |

#### 건물 업그레이드 사운드

> 업그레이드 가능한 건물에만 적용. 해당하지 않는 항목은 삭제.

| 종족 | 건물명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | Castle | `sfx_castle_upgrade.wav` | |
| Human | MiningPost | `sfx_miningpost_upgrade.wav` | |
| Human | CannonTower | `sfx_cannontower_upgrade.wav` | |
| Human | FlightFacility | `sfx_flightfacility_upgrade.wav` | |
| Human | TechnicalLaboratory | `sfx_technicallaboratory_upgrade.wav` | |
| Human | TrainingCamp | `sfx_trainingcamp_upgrade.wav` | |
| Human | WarAcademy | `sfx_waracademy_upgrade.wav` | |
| Human | Barracks | `sfx_barracks_upgrade.wav` | |
| Human | Gunsmith | `sfx_gunsmith_upgrade.wav` | |
| Human | Armory | `sfx_armory_upgrade.wav` | |
| Human | WeaponForge | `sfx_weaponforge_upgrade.wav` | |
| Human | Garage | `sfx_garage_upgrade.wav` | |
| Human | VehicleBay | `sfx_vehiclebay_upgrade.wav` | |
| Spirit | SpiritNexus | `sfx_spiritnexus_upgrade.wav` | |
| Spirit | ManaRift | `sfx_manarift_upgrade.wav` | |
| Spirit | RuneSpire | `sfx_runespire_upgrade.wav` | |
| Spirit | MagicSpirit | `sfx_magicspirit_upgrade.wav` | |
| Spirit | AstronomicalSpirit | `sfx_astronomicalspirit_upgrade.wav` | |
| Spirit | FireSpire | `sfx_firespire_upgrade.wav` | |
| Spirit | BlazeConduit | `sfx_blazeconduit_upgrade.wav` | |
| Spirit | InfernoCore | `sfx_infernocore_upgrade.wav` | |
| Spirit | AquaSpring | `sfx_aquaspring_upgrade.wav` | |
| Spirit | TidalNexus | `sfx_tidalnexus_upgrade.wav` | |
| Spirit | OceanicHeart | `sfx_oceanicheart_upgrade.wav` | |
| Spirit | StoneMound | `sfx_stonemound_upgrade.wav` | |
| Spirit | TerraForge | `sfx_terraforge_upgrade.wav` | |
| Spirit | GaeaSanctum | `sfx_gaeasanctum_upgrade.wav` | |
| Transcendence | ElderTree | `sfx_eldertree_upgrade.wav` | |
| Transcendence | FungalNode | `sfx_fungalnode_upgrade.wav` | |
| Transcendence | VineTower | `sfx_vinetower_upgrade.wav` | |
| Transcendence | MistShrine | `sfx_mistshrine_upgrade.wav` | |
| Transcendence | WillowShrine | `sfx_willowshrine_upgrade.wav` | |
| Transcendence | AncientGrove | `sfx_ancientgrove_upgrade.wav` | |
| Transcendence | PrimalAltar | `sfx_primalaltar_upgrade.wav` | |
| Transcendence | PrimalDen | `sfx_primalden_upgrade.wav` | |
| Transcendence | PrimalSanctuary | `sfx_primalsanctuary_upgrade.wav` | |
| Transcendence | FeralAltar | `sfx_feralaltar_upgrade.wav` | |
| Transcendence | FeralDen | `sfx_feralden_upgrade.wav` | |
| Transcendence | FeralSanctuary | `sfx_feralsanctuary_upgrade.wav` | |
| Transcendence | SporePatch | `sfx_sporepatch_upgrade.wav` | |
| Transcendence | FloralNursery | `sfx_floralnursery_upgrade.wav` | |

#### 건물 특수 효과 사운드

> 사운드 규칙 15(VFX + SFX 쌍 호출)에 따라 위 **건물 특수 효과 이펙트**와 1:1로 대응한다.
> MistShrine 물안개 힐은 **2026-08-12 기준 로직·UI 구현 완료**(멀티 미검증)이지만, 아래 사운드는 **여전히 모두 미제작**이다.

| 종족 | 건물명 | 파일명 | 비고 |
|------|--------|--------|------|
| Transcendence | MistShrine | `sfx_mistshrine_mist_cast.wav` | **미제작** — 물안개 시전음(1회). `vfx_mistshrine_mist_cast`와 쌍 |
| Transcendence | MistShrine | `sfx_mistshrine_mist_loop.wav` | **미제작** — 물안개 지속 환경음(루프). `vfx_mistshrine_mist_loop`와 쌍, 물안개 소멸 시 함께 정지 |

#### UI 사운드

| 사운드 | 파일명 | 비고 |
|--------|--------|------|
| 버튼 클릭 | `sfx_ui_button_click.wav` | |
| 버튼 확인 | `sfx_ui_button_confirm.wav` | |
| 버튼 취소 | `sfx_ui_button_cancel.wav` | |
| 패널 열기 | `sfx_ui_panel_open.wav` | |
| 패널 닫기 | `sfx_ui_panel_close.wav` | |
| 오류/불가 | `sfx_ui_error.wav` | |

#### 타일 점령 사운드

| 사운드 | 파일명 | 비고 |
|--------|--------|------|
| 타일 점령 | `sfx_tile_capture.wav` | |

#### 배경음악 (BGM)

| 상황 | 파일명 | 비고 |
|------|--------|------|
| 전투 | `bgm_battle.wav` | 루프 |
| 로비 | `bgm_lobby.wav` | 루프 |
| 승리 | `bgm_victory.wav` | 루프 가능 |
| 패배 | `bgm_defeat.wav` | 루프 가능 |

#### 환경 효과음 (Ambient)

| 환경 | 파일명 | 비고 |
|------|--------|------|
| 전장 환경음 | `amb_battlefield.wav` | 루프 |
| 바람 | `amb_wind.wav` | 루프 |

#### 게임 이벤트 사운드

| 이벤트 | 파일명 | 비고 |
|--------|--------|------|
| 게임 시작 | `sfx_game_start.wav` | |
| 승리 | `sfx_game_victory.wav` | |
| 패배 | `sfx_game_defeat.wav` | |

---

## Ⅲ. 보류 (On Hold)

> 현재 제작하지 않으며, 추후 필요 시 제작 예정으로 이동.

### VFX 보류

| 카테고리 | 대상 | 비고 |
|---------|------|------|
| 유닛 피격 이펙트 | 전 유닛 | 추후 추가 예정 |
| 유닛 생산 완료 이펙트 | - | 추후 추가 예정 |
| 건물 건설 이펙트 | 전 건물 | 추후 추가 예정 |

### SFX 보류

| 카테고리 | 대상 | 비고 |
|---------|------|------|
| 유닛 피격 사운드 | 전 유닛 | 추후 추가 예정 |
| 유닛 생산 완료 사운드 | - | 추후 추가 예정 |
| 건물 건설 사운드 | 전 건물 | 추후 추가 예정 |
