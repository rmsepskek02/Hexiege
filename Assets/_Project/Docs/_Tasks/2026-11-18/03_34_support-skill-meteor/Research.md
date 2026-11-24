# Research — 지원 스킬 2-D 메테오 (정령 MagicSpirit 슬롯 1) + 지속 피해 종류별 구조(후속 확장 기록)

## 이 문서는 무엇인가

정령 스킬 건물(MagicSpirit)의 첫 번째 스킬 **메테오**는 기획상 「조준한 지점에 운석이 떨어져, 떨어지는 순간 원 안에 있는 적에게 피해를 주고 유닛에게는 화상까지 거는」 스킬이다. 지금 게임에는 슬롯에 들어가 있기만 하고 **「사용 불가」로 꺼져 있다.**

처음에는 두 단계로 조사했다. *(2026-11-18 2판 — 사용자 「이후 확장에 관한 부분이라서 얘기했던거고 지금 당장은 작업을 진행하지않아도 문서 기록에는 남기면 좋겠어」로 **이번 Task = 메테오(2단계)만**이 됐다. 1단계는 구현하지 않고 기록만 남긴다 — Plan 12절 「후속 확장(미구현)」. 아래 1단계 관련 조사(4~6 · 12 · 13절)는 그 확장의 출발점으로 그대로 둔다)*

- **1단계 — 지속 피해 규칙 바꾸기(후속 확장 · 이번에는 구현하지 않음).** 지금은 한 유닛에 지속 피해(독 · 화상 같은 것)가 **하나만** 붙는다. 독이 걸린 유닛에 화상이 걸리면 독이 사라지고 화상으로 바뀐다. 사용자는 2026-11-18에 이것을 바꾸기로 했다 — **종류가 다른 지속 피해(독 + 화상)는 함께 걸리고, 같은 종류(화상 + 화상)는 하나만 남아 시간이 다시 처음부터 돈다.** 화상은 InfernoSpirit이 걸었든 메테오가 걸었든 **같은 화상**으로 본다. 지속 회복(HoT)은 이 규칙과 상관없다.
- **2단계 — 메테오 켜기.** 포격이 이미 쓰는 「착탄 예약」(조금 뒤에 떨어지는 장치)을 그대로 쓰고, 착탄 순간 살아남은 적 유닛에게 화상을 거는 단계와, 예고 · 떨어지는 운석 · 착탄 폭발 이펙트를 넣을 칸을 더한다. 이펙트 프리팹은 아직 없어서, 공습처럼 **칸만 만들고 비어 있어도 피해 · 화상은 동작**하게 한다.

이번 메테오의 화상은 지금 코드 그대로 「지속 피해 칸 하나 — 같은 칸이면 갱신」을 쓴다. 화상끼리(메테오 · InfernoSpirit)는 갱신이라 사용자 규칙과 맞고, 독 + 화상은 실제 경기에서 생기지 않는다(4절 3 · 6-1, Plan 1-3).

이 문서는 **지금 코드가 어떻게 되어 있고, 두 단계를 하려면 어디를 고쳐야 하며, 무엇이 함께 영향을 받는지**를 정리한다. 무엇을 어떤 순서로 만들지, 남은 결정, 테스트 방법은 [Plan.md](Plan.md)에 있다. **코드 · 자산은 이 문서 작성 중 바뀌지 않았다.**

> **현재 상태 (2026-11-18): 조사 완료 · Task 종료** — 구현(`0f9b091`) · 사용자 테스트 PASS · 정리까지 끝났다([Plan 14절](Plan.md#14-final-result)). 아래 본문은 조사 당시(HEAD `aef9d5f`) 기록이라 행 번호 · 「구현 착수 전」 표기는 그대로 둔다. 14절 범위 밖 관찰 중 `SkillDefinition.cs` 주석 · 툴팁과 `ApplyInfernoDot` 요약 주석은 정리 때 고쳤다(Plan 14-6). *(2026-11-18 종료 갱신 — 직전 줄은 아래 그대로)* 조사 기록(HEAD `aef9d5f`, 브랜치 `claude/vfx-code-followups`). 사용자 결정 · 메인 세션 기본안 · 조사 설계안 · 열린 항목의 구분은 Plan 1~3절, 지속 피해 종류별 칸(후속 확장 · 미구현)은 Plan 12절. **구현 착수 전 — Plan 사용자 승인 대기.** *(2026-11-18 2판 — 이번 범위 = 메테오만, 스키마 11 유지(12절 끝), 예고 재생 속도 맞춤 이번에 넣음(10-1). 직전 「사용자 결정 · … 구분은 Plan 1~3절.」)*

**출처 · 표기:** 코드 사실은 game-programmer의 HEAD `aef9d5f` 읽기 전용 조사 결과 **[전달]** 이다. 그중 document-manager가 같은 커밋의 파일을 직접 열어 행 내용을 대조한 것만 **[확인]** 으로 적었다. 대조에서 어긋난 인용은 지우지 않고 15절에 바른 값을 모았다. `[판단]` = 실행 · 실기로 확인하지 않은 판단, `[문서 담당]` = document-manager가 덧붙인 관찰(결정 아님). 경로 머리 `Scripts/` = `Assets/_Project/Scripts/`. 「상위 Plan N행」 = 아래 상위 Task Plan의 HEAD `aef9d5f` 행 번호.

**상위 Task:** [종족별 지원 스킬 기획 Plan](../../2026-11-02/14_25_race-support-skills-design/Plan.md) — 1-2(정령 슬롯 1), 2절 메테오 행(135행), 2-1 V2, 3-1 정령 1, 3-2 2-D(287행), 3-3 B-1, 4절 9 · 23 · 24 · 25(330 · 357~359행), 5절 O-9 · O-19 · O-20 · O-25(378 · 386 · 387 · 392행), 5-1 결정 18 · 23~30(417 · 421~428행), 7-2 · 7-3. **직전 Task:** [공습 Task](../../2026-11-17/09_12_support-skill-air-strike/Plan.md#16-final-result)(문서 형식 · VFX 칸 · 셋업 도구 기대 수 12 · 스위트 고정값 · 진단 로그 존속).

---

## 1. 범위

| 이 Task가 다루는 것 | 이 Task에서 하지 않는 것 |
|---|---|
| ~~**1단계:** 지속 피해를 **종류별 칸**으로 — 같은 종류는 갱신, 다른 종류는 함께(지속 회복 제외)~~ **→ 2판: 후속 확장(미구현, Plan 12절) — 이번에는 기록만** | 지속 회복(HoT) · MistShrine 물안개 · 재생의 뿌리 규칙 변경 |
| ~~1단계: 서버 기록 · 표현 채널 · Client 동기화 · 화면 이펙트를 종류별로~~ (2판: 후속 확장) | 독 · 화상 수치 변경(InfernoSpirit · MushroomBomber 값 그대로) |
| ~~1단계: 기존 InfernoSpirit · MushroomBomber · 타입 B 장판 회귀 보장~~ (2판: 후속 확장. 이번에는 지속 피해 구조를 바꾸지 않는다) | 「투사체 화면 착탄 대기」 장치가 스킬 출처를 가리게 바꾸기(9-2) |
| **2단계:** 메테오 서버 판정(타입 A 착탄 예약 재사용 + 착탄 화상) | 메테오 VFX 프리팹 제작(`vfx_meteor_warning` · `vfx_meteor_body` · `vfx_meteor_impact` — VFX 제작 단계, 결정 30으로 일시 중단) |
| 2단계: 예고 · 몸체 · 착탄 VFX 칸과 재생 시점(프리팹이 비어도 동작), 예고 재생 속도 맞춤(2판 — 사용자 「지금 넣음」) | 수치 확정 — 피해 · 반경 · 쿨다운 · 화상 · 적 건물 피해 비율(밸런싱 B-1 · B-2) |
| 2단계: 메테오 켜기(셋업 도구 기대 수 13) · 진단 로그 · 자가 검증 | 원형 조준 방식 변경(메테오는 기존 원형 조준 그대로 — 11절) |
| | 연막탄 · 원소 공명(「사용 불가」 그대로), AI의 스킬 사용(AI는 스킬을 쓰지 않는다) |

---

## 2. 근거로 읽은 규칙 · 문서

- `GameSystemRules/GameSystemRules_Units.md` — 규칙 34 「HoT/DoT 공용 시간 지속 효과」(500행 — 505행 「**대상별 동종 효과 1개만** 유지한다」 · 508행 「**갱신 = 리셋(중첩 없음)**」), 규칙 40 「DoT 초 단위 틱」(554행 — 556행 Mushroom 착탄 표현 문단 「갱신 정책을 유지한다」, 563행 「동일 대상 동종 DoT 재부여는 … 리셋하고 stack하지 않는다」), 규칙 41 InfernoSpirit(573행 — 576행 「… 갱신 리셋을 따른다」), 규칙 42(579행 — 581행 「poison↔burn/다른 Damage overwrite는 이전 표현 종료·교체」), 규칙 44 방어력 감쇄(612행 — DoT 틱에는 미적용) [확인]
- `GameSystemRules/GameSystemRules_Skills.md` — 규칙 7(데이터 주도 — 요소 표), 9(슬롯), 11 「타입 A — 즉발 범위 피해」(145~153행 — 착탄 지연 · 착탄 순간 판정 · 「착탄 전에 시전 건물이 파괴돼도 착탄은 그대로」 · 경기 종료 버림 · 적 건물 피해 비율은 밸런싱), 12 「타입 B — 범위 지속 피해(장판)」(155~159행), 16 · 17~22-1(원형 조준), 25 · 26(서버 권위 · 좌표 1개 전송), 29(공습 — 새 메커니즘 번호 선례) [확인]
- `TechnicalDesignDocument_Network.md` 「경기 시작 호환 확인」(40행) — 스키마 = 11, 「**RPC 모양이 바뀌면 스키마를 올린다**」 [확인]
- `Assets/VFXSFXGuide.md` 「Unity AI 프롬프트 작성 규칙 — 코드 계약」(236행~ — 상태 이펙트 · 1회성 착탄 두 유형) · `Assets/VFXSFXList.md` 113 · 114행(화상 · 독 연결) · 273행(「`vfx_meteor_fall`은 아직 어디서도 참조되지 않는다」) [확인]
- `StatsReference.md` 292행 — 고정 DoT 틱값 MushroomBomber 20/s · InfernoSpirit 50/s [확인]
- `WORKFLOW.md` [3] · [4](「기존 로직 제거 규칙」 · 「테스트 항목 진단 로그 규칙」) · [6](수집 메뉴 · 회귀 스위트 판정) [확인]
- `.claude/MEMORY.md` 「공통 중요 교훈」 게임 규칙 체크리스트(한 경기 한 종족 · 자기 스킬 건물 · AI는 스킬을 쓰지 않음 · 건물은 짓는 즉시 생김 · 싱글/멀티 역할) [확인]

---

## 3. 사용자 결정의 출처 (요약 — 결정 표는 Plan 1절)

메인 세션이 2026-11-18 사용자 대화에서 정리해 전달했다 **[전달 — 메인 세션 정리]**. 지속 피해 규칙은 사용자 원문 두 개를 그대로 옮긴다.

1. **첫 원문:** 「중독 화상 등등 이런 지속대미지는 모두 중첩을 허용하도록할거야 단, 같은 효과는 중첩이아닌 갱신으로 진행할거야 이해했어?」 — 후속 확인에서 「InfernoSpirit 화상과 메테오 화상 = 다른 효과(중첩)」, 지속 회복(HoT)은 제외, 규칙 변경은 메테오 Task에 포함(1단계 = 지속 피해 구조, 2단계 = 메테오)으로 정해졌다.
2. **정정 원문(최종):** 「아니 출처가 달라도 같은화상으로 진행하도록하자」 — 위 「InfernoSpirit 화상과 메테오 화상 = 다른 효과」를 **대체**한다. 「같은 효과」 = **효과 종류(화상 / 독 등)**, 출처와 무관. HoT 제외는 그대로.

→ 같은 종류(화상 + 화상 — InfernoSpirit · 메테오 · 두 기 등 출처 불문) = **갱신**(하나만 유지, 지속시간 리셋), 다른 종류(독 + 화상) = **함께 각각 피해**. 갱신 때 수치(초당 피해 · 지속)는 **새로 걸린 쪽 값**이 메인 세션 기본안(사용자에게 보고 예정 — 확정 아님).

---

## 4. 핵심 발견 7가지

1. **지금 지속 피해는 유닛당 칸이 하나다.** 서버 기록의 갱신 판정이 「같은 대상 + 같은 Kind」이고 Kind는 `Heal` / `Damage` 두 가지뿐이라(`UnitCombatUseCase.cs` 2309~2315 · 2844~2848) [확인], 독 · 화상 · 장판 DoT가 모두 같은 `Damage` 칸을 쓴다 → 서로 덮어쓴다(5-1).
2. **표현 채널 · Client 동기화 · 화면 이펙트도 유닛당 하나다.** 채널 `_current`는 대상 Id 하나가 키(`TimedDamageEffectPresentation.cs` 27행), 네트워크 변수는 유닛마다 1개(`NetworkUnit.cs` 528~529), 화면 presenter의 `_owners`도 대상 Id 키(`TimedDamageEffectPresenter.cs` 24 · 186) [확인]. → 1단계는 서버 기록만이 아니라 이 세 곳을 함께 바꿔야 두 종류가 동시에 **보인다**(5-2~5-4).
3. **🔴 실제 경기에서는 「독 + 화상」이 한 유닛에 함께 걸릴 수 없다** `[판단 — 아래 근거]`. 지속 피해를 거는 곳은 독 = MushroomBomber(초월) · 화상 = InfernoSpirit(정령)과 메테오(정령) · 장판 = 타입 B 스킬뿐이고(6-1) [확인 — grep], 1:1 경기에서 한 유닛의 적은 상대 한 명(한 종족)뿐이다(게임 규칙 체크리스트 (1)). 타입 B 장판 자산 3개는 어느 설정 자산도 참조하지 않는다(6-1) [확인]. → **1단계의 「다른 종류 함께」는 지금 실기 · 로그로 관측할 수 없고 자가 검증으로만 확인된다.** 실기에서 볼 수 있는 것은 같은 종류 갱신(화상 + 화상, 독 + 독) 회귀뿐이다.
4. **같은 종류 갱신의 「새로 걸린 쪽 값」은 지금 코드가 이미 그렇게 한다.** 갱신 분기가 출처 · 총량 · 지속 · 틱 값을 새 값으로 바꾸고 경과를 0으로 되돌린다(2854~2880) [확인]. → 메인 세션 기본안을 따르면 같은 종류 쪽 동작은 바뀌지 않는다.
5. **착탄 피해 장치는 이미 메테오를 예정해 두었다.** 타입 A 다리 `SelectInstantAreaDamageBridge`(469~476)가 지연 > 0이면 `ScheduleImpact`(481~498)로 예약하고, 주석이 「지연 > 0(포격 0.4초 · 이후 메테오 1.5초)」(307) · 「메테오도 타입 A로 이 장치를 재사용」(434)이라고 적고 있다 [확인]. 새로 필요한 것은 착탄 순간의 화상 한 단계와 화상 수치 칸이다(8절).
6. **화상은 스킬(건물) 출처로도 걸린다.** `ApplyDamageOverTime(UnitData source, …)`(2412~2431)은 `source` null을 받고(SourceId −1 — 2840), 타입 B가 이미 그렇게 부른다(2637) [확인]. 화면 화상 이펙트는 종류만 보고 InfernoSpirit 프리셋을 고른다(`EffectManager.cs` 392~394) [확인] — 「InfernoSpirit 화상 재사용」(상위 Plan 2-1 178행)이 코드 변경 없이 성립한다.
7. **기존 자가 검증 한 줄이 1단계와 정면으로 어긋난다.** `RunUnitActionSelfValidation.VfxConnections.cs` 644~645가 같은 대상에 독 → 화상을 걸고 「Single Damage identity overwritten, not stacked.」를 요구한다 [확인]. 1단계 뒤에는 이 검사가 **실패해야 정상**이므로 같은 커밋에서 바꿔야 한다(13절).

---

## 5. 지속 피해 — 지금 구조

### 5-1. 서버 기록 (`Scripts/Application/UseCases/UnitCombatUseCase.cs`)

| 자리 | 지금 | 1단계에 필요한 변화 | 등급 |
|---|---|---|---|
| `TimedEffectKind` 2309~2315 | `Heal` · `Damage` 두 값 | Heal 유지. Damage 안에서 **종류**(독 / 화상 / 장판)를 가를 기준이 필요 | [확인] |
| `_activeTimedEffects` 2321 | 「대상별·종류별 1개」 목록 | 「대상별 · (Heal 또는 지속 피해 종류)별 1개」 | [확인] |
| `ApplyDamageOverTime` 2412~2431 | 수치를 인자로 받아 `AddOrRefreshTimedEffect(…, TimedEffectKind.Damage, …, damageVisual: visual)` | 종류를 기록 키로 넘긴다 | [확인] |
| `AddOrRefreshTimedEffect` 2831~2903 | 2844~2848 `e.TargetId == target.Id && e.Kind == kind`면 갱신(2854~2880 — 출처 · 총량 · 지속 · 경과 0 · 틱 값을 새 값으로), 아니면 새 기록 | 판정에 **지속 피해 종류**를 더한다(Heal은 그대로) | [확인] |
| `ActiveTimedEffect` 3254~ | `TargetId` · `SourceId` · `Kind` · 총량 · 지속 · 틱 값 · `DamageState`(표현). **종류(Visual) 칸이 없다** — 종류는 표현 채널에만 넘어간다(`ObserveDamageUpsert` 2908~2922) | 기록에 종류 칸 추가 | [확인] |
| `TickTimedEffects` 2964~ | 역순 순회, 대상 사망 · 만료면 제거 · 표현 정지 | 기록마다 따로 돌므로 두 종류가 각자 틱 · 만료(구조 변경 없음 `[판단]`) | [확인] |
| `ApplyTimedDamageToUnit` 3196~3220 | 틱 피해 = `IncomingDamage.Apply(…, IncomingDamageWriter.DoT)`(3202 — 방어 감쇄 없음, 대지의 갑주 · 쉴드는 적용), 사망 처리 | 변화 없음 — 두 종류 틱이 같은 프레임이면 각각 1회씩 | [확인] |

### 5-2. 표현 채널 (`Scripts/Application/Combat/TimedDamageEffectPresentation.cs`)

- `TimedDamageVisual : byte { None, Poison, Burn }`(7행) [확인]. 상태 구조체에 `TargetId` · `SourceId` · `Visual` · 시각 · 개정 번호(8~22행) [확인].
- `TimedDamageEffectChannel._current` = `Dictionary<int, Entry>` — **대상 Id 하나가 키**(27행) [확인]. `Upsert`(42~62)는 같은 대상에 기존 값이 있으면 같은 주인(owner)을 유지한 채 `Visual`을 바꾸고 「Refresh」(같은 종류) 또는 「**Overwrite**」(다른 종류)를 남긴다(60행) [확인]. `Receive`(72~88)의 은퇴 · 중복 판정도 대상 Id 키(75 · 78행) [확인].
- 진단: `Record`(115~129)가 `Stage=… Target=… Source=… Visual=…` 한 줄을 `GameLog.Dev`로 남긴다(Grant · Refresh · Overwrite · Stop · Publish · Receive · Drop) [확인] — 메테오 화상은 `Source=-1`로 가를 수 있다.

### 5-3. Client 동기화 (`Scripts/Infrastructure/Network/NetworkUnit.cs`)

- 유닛마다 `NetworkVariable<NetworkTimedDamageEffectState> _damageState` **1개**(528~529, 서버 쓰기) [확인]. `ApplyCurrentTimedDamage`(537~)가 서버는 `channel.TryGet(target)` 값을 게시(558~559), Client는 받은 값을 `channel.Receive`(563~) [확인]. 경기 정리 때 `_damageState.Value = default`(845) [확인].
- 직렬화 `NetworkTimedDamageEffectState.cs` — 상태 1개(개정 번호 · 대상 · 출처 · 공격 키 · 활성 · 시각 · 종류 바이트) [확인].
- → 두 종류를 함께 보이려면 유닛당 상태가 2개 이상 실려야 한다. 방식 후보와 스키마 판단은 12절.

### 5-4. 화면 이펙트

- `TimedDamageEffectPresenter.cs` — `_owners` = `Dictionary<int, Owner>`(24), 받은 상태를 `_owners[state.TargetId]`에 둔다(186) [확인]. 착탄 대기 `ShouldHoldStart`(132~) → 재생 `_manager?.StartTimedDamageEffect(…)`(222) [확인].
- `EffectManager.StartTimedDamageEffect` 389~401 — `Visual`이 Burn · Poison일 때만, Burn → `UnitType.InfernoSpirit` 프리셋 · Poison → `UnitType.MushroomBomber` 프리셋(394) [확인]. `None`(장판)은 이펙트 없음(392~393) [확인].
- → 두 종류 동시 표시는 presenter 주인 키를 (대상, 종류)로 바꾸면 각 종류의 기존 프리셋이 따로 붙는다 `[판단]`. 두 이펙트가 한 몸에서 겹쳐 보일 때의 모양(크기 · 높이 조정 여부)은 열린 항목(Plan O-4). *(2026-11-18 2판 — 사용자 「둘 다 표시」: 확장 때 둘 다 표시, Plan 12-1 U-12)*

---

## 6. 1단계 영향 범위

*(2026-11-18 2판 — 1단계는 후속 확장(미구현, Plan 12절). 이 절은 그 확장의 영향 조사로 보존한다. 「Plan O-3」 · 「Plan 11절」은 지금 Plan 12-1 · 12-4를 가리킨다)*

### 6-1. 지속 피해를 거는 곳 전부 [확인 — `TimedDamageVisual.Burn|Poison` · `ApplyDamageOverTime(` grep]

| 출처 | 종류(지금 Visual) | 거는 자리 | 실제 경기에서 걸리는가 |
|---|---|---|---|
| MushroomBomber(초월) 착탄 | Poison | `UnitCombatUseCase.cs` 1998 · 2442 | 예 — 초월 상대의 유닛 |
| InfernoSpirit(정령) 명중 | Burn | 1999 · 2456 | 예 — 정령 상대의 유닛 |
| 메테오(정령, 2단계) | Burn(이 Task) | 새 자리(8-2) | 2단계 뒤 — 정령 상대의 유닛 |
| 타입 B 장판 스킬 | **None**(인자 생략 — 2637) | `ApplySkillAreaDot` 2620~2639 | **아니오** — `Skill_Human_Dot` · `Skill_Spirit_Dot` · `Skill_Trans_Dot`(`_mechanic: 1`)의 GUID를 참조하는 설정 자산이 없다 [확인 — `Resources/Config` 안 GUID 검색 0건] |

- **판단 근거(4절 3):** 1:1 경기에서 한 유닛에 지속 피해를 거는 쪽은 그 유닛의 적 한 명이고, 한 경기에서 한 사람은 한 종족이다(게임 규칙 체크리스트 (1)). 정령은 화상만, 초월은 독만 건다(위 표). → 독 + 화상이 같은 유닛에 함께 걸리는 상황은 지금 경기에서 생기지 않는다 `[판단]`. 1단계의 「다른 종류 함께」는 **자가 검증으로만** 덮인다(Plan 7절 D-T1~D-T4 — 2판에서 Plan 12-3으로 옮김).
- 같은 종류 갱신이 실기에 나오는 경우: InfernoSpirit 두 기가 같은 대상 · Inferno 재명중(화상 + 화상), MushroomBomber 재착탄(독 + 독), 2단계 뒤 메테오 + InfernoSpirit(화상 + 화상) · 메테오 두 발.

### 6-2. 1단계가 바꾸는 동작 · 바꾸지 않는 동작

| 경우 | 지금 | 1단계 뒤 |
|---|---|---|
| 같은 대상 화상 + 화상(출처 불문) | 갱신(남은 시간 · 총량 리셋, 값은 새 쪽) | **같다**(4절 4) |
| 같은 대상 독 + 독 | 갱신 | **같다** |
| 같은 대상 독 + 화상 | 화상이 독을 덮어씀(표현 「Overwrite」) | **둘 다 각자 피해 · 각자 만료**, 이펙트 둘 다 |
| 장판(None) + 독 · 화상 | 덮어씀 | 종류를 어떻게 볼지에 따라(Plan O-3) — 지금 경기에서는 장판이 없다(6-1) |
| 지속 회복(HoT) | Heal 칸 1개, 갱신 | **같다**(사용자 결정 — HoT 제외) |

### 6-3. 함께 영향받는 곳

| 영역 | 영향 | 근거 |
|---|---|---|
| 회귀 스위트 | `VfxConnections.cs` 644~645 「Single Damage identity overwritten, not stacked.」가 1단계 뒤 실패한다 → 같은 커밋에서 새 기대로 | 13절 |
| 표현 채널을 쓰는 스위트 | `VfxConnections.cs` 638~708 · 731 · 765, `ServerProjectiles.cs` 1812 · 1853이 `TryGet(target)`(대상 하나 키)을 쓴다 → 채널 API가 바뀌면 함께 손봐야 한다 | [확인 — grep] |
| 접속 호환 | 유닛 동기화 형식이 바뀌면 스키마 판단 필요 | 12절 |
| 화상 · 독 VFX 수명 | 같은 주인 갱신은 끊지 않음(noRestart) · 다른 종류 교체는 이전 표현 종료 — 유닛 규칙 42(581행)의 「poison↔burn … overwrite」 문장이 거짓이 된다 | Plan 11절 |
| 투사체 화면 착탄 대기 | 종류만 보고 판정(`ProjectileKillPresentationSchedule.cs` 304~312 · 322~333) — 종류별 칸이 되어도 판정 입력은 같다 `[판단]` | 9-2 |

---

## 7. 메테오 자산 · 셋업 도구 · 스위트 지금 상태

| 자산 `Resources/Config/Skills/Skill_Spirit_Meteor.asset` | 값 | 등급 |
|---|---|---|
| 사용 가능 · 조준 · 메커니즘 | `_isAvailable: 0`(20행) · `_aimType: 1`(PointTarget) · `_mechanic: 0`(InstantAreaDamage) | [확인] |
| 수치 | `_cooldown: 10` · `_radius: 2` · `_duration: 0` · `_totalDamage: 150` · `_damagePerSecond: 0` · `_impactDelay: 0`(31행 — **1.5 아님**) | [확인] |
| VFX | `_activationVfxPrefab: {fileID: 0}`(비어 있음) · `_vfxReferenceRadius: 2` | [확인] |
| 이름 · 설명 | 「메테오」 · 「지정한 지점에 운석이 떨어져, 착탄 순간 범위 안의 적에게 피해와 화상을 준다.」 | [확인 — 유니코드 이스케이프 해독] |

- 이 자산에는 그 뒤에 생긴 칸(구역 VFX · 유닛 상태 VFX · 추가 효과 · 건물 상태 VFX · 직선 · 폭격 VFX) 키가 없다 → 코드 초기값으로 읽힌다 [전달].

| 셋업 도구 `Assets/Editor/Setup/SupportSkillLoadoutSetup.cs` | 등급 |
|---|---|
| 97행 `private const int ExpectedAvailableCount = 12;`(96행에 옛 값 `8` 주석) | [확인] |
| 184행 `PointPlaceholder` — 사용 불가 · PointTarget · InstantAreaDamage · 쿨 10 · 반경 2 · 피해 150 · 지연 0 | [확인 — 머리 행] |
| 339~340행 `PointPlaceholder("Skill_Spirit_Meteor", s, 0, "메테오", …)` | [확인] |
| 525~527행 — 사용 가능 수가 `ExpectedAvailableCount`와 다르면 쓰기 전에 멈춘다 | [확인] |
| 🔴 688~690행 — 도구를 다시 실행하면 「발동 VFX」 칸을 표 값(`ResolveActivationVfx` 615~ — 포격 · EMP 외엔 null)으로 **다시 쓴다** → 메테오 예고 프리팹을 이 칸에 넣으면 재실행 때 지워진다. 도구가 쓰지 않는 새 칸(몸체 · 착탄 · 화상)은 남는다 | [확인] |

| 스위트가 고정한 값 | 등급 |
|---|---|
| 사용 가능 수 `ExpectedAvailableCount` 12 검사 — `RunUnitActionSelfValidation.AirStrike.cs` 626~629 · `.SkillZones.cs` 348~352 · `.SupportNewStatuses.cs` 592~596 · `.SupportRegen.cs` 517~521 · `.SupportShield.cs` 356~360 (**5파일**) | [확인] |
| 스키마 `== 11` — `.AirStrike.cs` 576 · `.ServerProjectiles.cs` 266 · `.SkillZones.cs` 345 · `.SupportBuffSkills.cs` 266 · `.SupportNewStatuses.cs` 598 · `.SupportRegen.cs` 523 · `.SupportShield.cs` 354 (**7곳**) | [확인] |
| 스키마 상수 `PresentationContractSchemaRevision = 11` — `Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs` 45 | [확인] |
| 메테오를 「사용 불가」로 단정하는 스위트 검사 — 없다(`grep -i meteor` = 셋업 도구 339행뿐) | [전달] |

---

## 8. 착탄 피해 · 화상 — 재사용할 경로

### 8-1. 착탄 예약 (`Scripts/Application/UseCases/SkillActivationUseCase.cs`)

- 타입 A 다리 선택 324행 `SelectInstantAreaDamageBridge(skill.ImpactDelay, sequence)` → 469~476: 지연 0이면 즉시 다리, 0보다 크면 `ScheduleImpact`(481~498)가 `PendingImpact`(437~)에 팀 · 중심 · 반경 · 피해 · 시전 건물 · 순번 · 남은 시간 · 지연을 저장 [확인].
- 처리 `TickPendingImpacts`(506~): 남은 시간 ≤ 0이면 피해 적용 → `UnitCombatUseCase.ApplySkillInstantAreaDamage`(2589~2608 — 착탄 순간 원 안 적 유닛 · 적 건물을 먼저 모은 뒤 각각 `ApplySkillInstantDamageToVictim`) [확인]. 경기 종료 `ClosePendingImpacts`(559~) [확인].
- 서버 권위 틱만: 멀티 `NetworkCombatController.cs` 780행 `TickPendingImpacts(elapsed)` — 주석 778행 「`_attackInterval`(씬 값 0.1초) 주기라 착탄 피해가 최대 그만큼 늦을 수 있다」 [확인]. 싱글은 `GameBootstrapper.Update` [전달].
- 진단 이미 있음: `[SKILL-VFX] ImpactScheduled`(847~852) · `ImpactApplied`(865~889 — `MsAfterActivation` · `UnitHits` · `UnitKills` · `BuildingHits` · `CasterAlive`) · `ImpactDropped` [확인].
- 1.5초 착탄 시각: 0.1f를 15번 빼면 ≤ 0이 되지만 실제 경과는 프레임 누적이라 1틱 늦을지는 실기 `MsAfterActivation`으로 본다 [전달 — 계산 · 추정].

### 8-2. 착탄 화상을 붙일 자리 `[설계안 — 조사]`

- 화상은 **타입 A 다리가 고르는 함수에 함께 붙인다** — 즉시 다리 · 예약 다리 둘 다 화상 수치를 붙잡게 해서(예약이면 `PendingImpact`에 화상 두 칸) 착탄 지연을 0으로 바꿔도 화상이 사라지지 않게 한다 [전달].
- 피해 직후, 같은 호출 안에서 **피해 대상 버퍼의 살아남은 적 유닛**에게 `ApplyDamageOverTime(null, victim, 초당, 지속, 1초, Burn)` — 버퍼 `_skillUnitVictims`(2577)는 다음 스킬 호출 때 덮이므로 같은 호출 안이어야 하고, 피해로 죽은 유닛은 `ApplyDamageOverTime` 첫 줄(2416 `!target.IsAlive`)이 자연히 거른다 [확인 — 2577 · 2598~2605 · 2416]. 건물은 대상 아님(결정 18).
- 화상 수치는 스킬 자산의 **전용 칸 2개**(0 = 화상 없음)를 권장 — 기존 `_duration` · `_damagePerSecond`(타입 A가 읽지 않는 칸 — `InstantAreaDamageExecutor.cs` 40 · 43은 `TotalDamage` · `Radius`만 읽는다 [확인])를 재사용하면 새 칸은 없지만 「지속시간」이 타입 C · 구역 뜻과 섞인다 [전달 — 판단]. 메인 세션 기본안은 전용 칸(Plan 1절 M-3).
- 인페르노 수치(`SpecialAttackConfig.asset` 26~27행 `_infernoDotPerSecond: 50` · `_infernoDotDuration: 3` [확인])를 읽는 안은 유닛 밸런스를 바꾸면 스킬이 함께 바뀌어 권하지 않는다 [전달].

### 8-3. 피해 지점

- 착탄 피해 `ApplySkillInstantDamageToVictim` → `IncomingDamage.Apply(…, Skill)`, 화상 틱 → `IncomingDamage.Apply(…, DoT)`(3202) — 대지의 갑주 · 쉴드가 자동 적용, 추가 작업 없음 [확인 — 3202].

---

## 9. 화상의 Client 표시

### 9-1. 동기화

- 화상 현재 상태는 5-3의 유닛 네트워크 변수로, HP는 `OnEntityDamaged` → 기존 HP 동기화로 간다. 발동 표현은 기존 `SkillActivatedClientRpc`(착탄 지연 포함 — 포격과 같음) [전달]. **2단계만 보면 새 RPC가 없다** — 스키마 판단은 1단계 쪽(12절).

### 9-2. 「투사체 화면 착탄 대기」 장치

- `IsProjectileAligned`(`ProjectileKillPresentationSchedule.cs` 304~312)는 **종류만** 보고 Burn이면 InfernoSpirit 분류로 true → 메테오 화상에도 대기가 걸린다 [확인]. 대기 조건 `ShouldWait`(322~333): 같은 공격자의 투사체 피해 보관 중 · 「표시 시각 < 화상 시작 시각」 · 상한 0.3초(`MaxHoldSeconds` 295) [확인].
- 해가 없다 `[판단 — 조사]`: 싱글 · Host는 표시 지연 0이라 즉시 시작, 순수 Client는 최대 ~0.1초(상한 0.3초) 늦다. 고치지 않는다(범위 밖 — Plan 10절).

---

## 10. VFX 경로 (2단계)

### 10-1. 지금 쓸 수 있는 것

| 장치 | 자리 | 메테오 칸에 쓰는 법 `[설계안 — 조사]` | 등급 |
|---|---|---|---|
| 원형 발동 VFX | `SkillActivationVfxPresenter.OnSkillActivation`(157~) — 발동 알림 받은 순간 `_activationVfxPrefab`을 조준점 · 지면 + 0.06 · 「반경 ÷ 기준 반경」 배율로 1회, 비면 `NoPrefab` Warn(186) | **예고** 칸 = 기존 「발동 VFX」 | [확인 — 157 · 186] |
| 시각 예약 재생 | 공습 `SkillActivationVfxPresenter.LineStrike.cs` — `PendingStrikeVfx`(62~) · `DueTime = 받은 시각 + 첫 지연 + k × 간격`(72 · 242), 경기 종료 · 재초기화에 버림(98~113) | **착탄** 칸 = 받은 시각 + 착탄 지연에 1회 | [확인] |
| 반복 재생 + 확인 번호 | `VfxPoolItem.StatusLoop.cs` `BeginStatusLoop`(65) · `StopStatusLoop(lease, clear)`(84) · `FollowStatusLoop`(160) | **몸체**(반복 프리팹) — 확인 번호 필수(반복 프리팹은 `AnyAlive`가 꺼지지 않아 스스로 안 돌아온다 — `VfxPoolItem.cs` 508) | [확인] |
| 직선 이동 | `VfxPoolItem.Play`의 `forwardTravelDistance/Duration`(341~360) → `UpdateForwardTravel`(518~) | 몸체를 하늘 한쪽 → 착탄점으로 비스듬히 옮기기(선형) | [확인] |

- 재생 속도를 바꾸는 런타임 코드는 없고 풀 반환이 재생 속도를 되돌리지 않는다 → 예고 재생 속도 맞춤(결정 29 안 (나))을 실제로 하면 되돌리는 처리가 함께 필요하다 [전달]. 지금 기본값은 낙하 1.5 = 예고 1.5라 배율 1.
- 몸체 이동 곡선(`t²` / `0.3t + 0.7t²`)과 끝 높이 +0.35는 상위 Plan 4절 24(358행) — 열림 [확인]. 반복 몸체의 반환 · 안전 타임아웃은 VFXSFXGuide 「코드 계약」 표에 없는 새 유형이다(4절 25, 359행 — 가이드 반영은 사용자 확인) [확인].
- 「착탄 때 Rock_Core만 즉시 지움」(상위 Plan 7-3 560행)은 자식 이름 계약이 필요하다 — 몸체 프리팹이 없어 지금은 확인할 수 없다 [전달].

### 10-2. 임시 테스트 프리팹 후보 [전달 — YAML grep, document-manager 미대조]

| 칸 | 후보 | 주의 |
|---|---|---|
| 예고 | `Assets/_Project/Prefabs/VFX/Skills/vfx_meteor_fall.prefab`(커밋 `0e9df53`, 비반복 · Hierarchy, 예고 + 운석이 한 프리팹) | 셋업 도구 재실행 때 지워진다(7절 🔴). 지금 어느 자산도 참조하지 않음(VFXSFXList 273행 [확인]) |
| 착탄 | `Prefabs/VFX/Units/vfx_infernospirit_impact.prefab` 또는 `vfx_cannon_impact.prefab`(비반복 · 길이 1.0) | 유닛용이라 반경 2 원보다 작게 보일 수 있다 `[추정]` |
| 몸체(반복 경로 시험) | `Prefabs/VFX/Units/vfx_infernospirit_tracer.prefab`(반복 6개 · Hierarchy) | 예고 칸에 `vfx_meteor_fall`을 같이 넣으면 운석이 둘 보인다 |

---

## 11. 조준 UI

- 지점 지정 · 비직선 스킬은 `BuildingSkillPanelUI.cs` 462행 `BeginAim(buildingId, slotIndex, skill.Radius, …)`(원형) [확인] → 메테오는 **기존 원형 조준 그대로**. 공습 Task L-7의 맞는 타일 테두리 · 상태 색 · 두 손가락 화면 이동도 원형에 적용된다 [전달].
- 조준 표시는 「지금 서 있는 유닛」 기준이라 1.5초 뒤 회피는 표시되지 않는다 — **정상 동작**(설계상 회피 가능 — 스킬 규칙 11).

---

## 12. 스키마 판단 근거 (1단계)

*(2026-11-18 2판 — 결론: **이번 Task(메테오만)는 스키마 11 유지**(이 절 끝 줄). 1단계 확장 때는 사용자 「올림」으로 12로 올린다 — Plan 12-1 U-11)*

- 문서 규칙: 「**RPC 모양이 바뀌면 스키마를 올린다**」(TDD_Network 40행) — 선례 7건이 모두 RPC 인자 · 새 RPC다 [확인]. **네트워크 변수(동기화 변수)의 형식이 바뀔 때를 정한 문장은 없다** `[문서 담당 — 40행 대조]`.
- 1단계는 RPC를 바꾸지 않을 수 있으나(5-3), 두 종류를 Client에 보이려면 유닛 `NetworkObject`의 동기화 변수 구성(개수 또는 직렬화 형식)이 바뀐다 [판단 — 5-3 구조].
- 스키마는 경기 시작 때 두 빌드의 차이를 잡는 **유일한 관문**이다 — 다르면 Host가 시작을 거부한다(TDD_Network 40행) [확인]. 스키마를 그대로 두면 옛 빌드와 새 빌드가 경기를 시작한 뒤 화상 · 독 상태를 서로 다르게 읽을 수 있다 `[판단 — NGO 동작은 실행으로 확인하지 않음]`.
- → **스키마 11 → 12를 권한다**(Plan O-2 — 사용자 확인 필요). *(2026-11-18 2판 — 사용자 「올림」: 확장 때 올린다. Plan 12-1 U-11)* 올리면 스키마 상수 1곳 + 스위트 `== 11` 7곳(7절)을 같은 커밋에서 바꾸고, 멀티 테스트는 두 빌드 모두 새로 설치한다. TDD_Network 40행 문장(「RPC 모양」)도 넓힐지 함께 정한다(Plan 11절).
- 2단계(메테오)만 보면 RPC · 동기화 변수 변경이 없어 스키마 변경 이유가 없다 [전달].

---

## 13. 회귀 스위트에서 1단계가 건드리는 검사

*(2026-11-18 2판 — 1단계 미구현이므로 아래 검사는 이번 Task에서 바꾸지 않는다. 확장 때 Plan 12-3)*

| 파일 · 행 | 지금 검사 | 1단계 뒤 | 등급 |
|---|---|---|---|
| `RunUnitActionSelfValidation.VfxConnections.cs` 638~643 | 같은 대상 독 → 독 = 같은 주인 · 개정 증가 · 끝 시각 갱신 | **그대로 통과해야 한다**(같은 종류 갱신) | [확인] |
| 같은 파일 644~645 | 같은 대상 독 → 화상 = 「Single Damage identity overwritten, not stacked.」 | **반대로 바뀐다** — 독과 화상이 각자 주인 · 둘 다 활성. 옛 검사는 주석으로 남기고 새 검사로 | [확인] |
| 같은 파일 646~708 | 받는 쪽 옛 개정 · 중복 거부, 늦은 초기화 복원, 경기 교체 | 채널 키가 바뀌면 호출 모양만 따라간다 `[판단]` | [확인 — 머리 행] |
| `ProjectileDeathHold.cs` 124~151 | 화상 · 독 착탄 대기 판정(종류 입력) | 바뀌지 않는다 `[판단]` | [확인] |
| `ServerProjectiles.cs` 1812 · 1853 | Mushroom 착탄 뒤 대상에 독이 활성 | 그대로 통과해야 한다 | [확인] |

---

## 14. 범위 밖 관찰 — 기록만 (손대지 않음 · CLAUDE.md 규칙 6)

- `SkillDefinition.cs` 착탄 지연 · 발동 VFX 칸 주석 · 툴팁이 「읽는 코드가 아직 없다」고 적고 있다는 관찰 [전달 — document-manager 미대조].
- `UnitCombatUseCase.cs` `ApplyInfernoDot` 요약 주석 「InfernoSpirit 자기 튜닝값(초당 5/3초)」(2451행 근처)이 실제 기본값 50(`SpecialAttackConfig.cs` 97행)과 다르다 — ×10 이전 값으로 보인다 [확인 — 2447~2452 · 97]. (조사 전달은 「78행 주석」이라 했다 — 같은 내용이 두 곳에 있는지는 확인하지 않았다.)
- 조사 지시문이 언급한 「VFXSFXGuide/List V2 행」은 두 문서에 없다 — VFXSFXList 273행 한 줄뿐 [전달 · 273행 확인].

---

## 15. 대조 중 바로잡은 인용 (document-manager, HEAD `aef9d5f`)

| 전달 내용 | 대조 결과 |
|---|---|
| `AddOrRefreshTimedEffect` 2846~2877행 | 메서드는 **2831~2903**행, 같은 대상 · 종류 판정은 2844~2848, 갱신 분기는 2854~2880 |
| `ExpectedAvailableCount = 12` 97행 · 스위트 5곳 | 맞다(5파일 행 7절) |
| 스키마 `== 11` 7곳 · 상수 45행 | 맞다 |
| `NetworkUnit.cs` 528~529 · 537~568 | 맞다 |
| `ImpactApplied` 진단 「약 850~903행」 | `LogImpactScheduled` 847~852 · `LogImpactApplied` 865~889 |
| `SupportSkillLoadoutSetup.cs` 688~690 「발동 VFX 다시 쓰기」 | 맞다(688 옛 줄 주석 · 690 현재 줄) |
| 지속 피해 출처 · 장판 자산 미참조 | 전달에 없던 확인 — 6-1 표(grep · GUID 검색) |
| 스위트 `VfxConnections.cs` 644~645 충돌 | 전달에 없던 확인 — 13절 |

그 밖의 [확인] 행 번호는 HEAD `aef9d5f`에서 전달 범위와 일치했다.

---

## 16. 이번 조사에서 확인하지 않은 것

- 두 화상 · 독 이펙트가 한 유닛에 동시에 붙을 때의 모양(가려짐 · 크기) — 프리셋 조정이 필요한지는 실기 · 자가 검증 뒤(그러나 실제 경기에서는 생기지 않는다 — 6-1).
- NGO가 동기화 변수 구성이 다른 두 빌드를 만났을 때 실제로 어떻게 되는지(12절 판단의 실행 근거).
- `HasHeldProjectileHit`(조합 루트 `GameBootstrapper.Map.cs` 452행)의 본문 — 메테오(SourceId −1)에 걸릴 투사체가 없다는 판단 [전달 — 추정].
- 임시 프리팹 후보의 YAML 값(10-2) — document-manager가 다시 읽지 않았다.
- `vfx_meteor_fall` 안 운석 입자의 실제 궤적 · 도착 시각.

---

## 17. 변경 이력

| 날짜 | 내용 |
|---|---|
| 2026-11-18 | 첫 판 — game-programmer의 HEAD `aef9d5f` 읽기 전용 조사 결과와 메인 세션이 정리한 사용자 결정(지속 피해 규칙 첫 원문 · 정정 원문)을 바탕으로 작성. document-manager가 5 · 7 · 8 · 9 · 13절 행을 같은 커밋에서 대조하고, 지속 피해 출처 전부 · 장판 자산 미참조(실제 경기에서 독 + 화상이 함께 걸릴 수 없다는 판단 — 4절 3)와 스위트 644~645 충돌(4절 7)을 더함(15절) |
| 2026-11-18 | 2판 — 사용자 답 반영: 1단계 미구현(후속 확장 기록 — Plan 12절), 이번 범위 = 메테오만 · 스키마 11 유지 · 예고 재생 속도 맞춤 이번에 넣음. 머리 설명 · 현재 상태 · 1절 범위 · 6 · 12 · 13절 머리 메모만 고침(조사 본문은 그대로) |
| 2026-11-18 | 종료 — 현재 상태 줄(조사 완료 · Task 종료 · Plan 14절). 조사 본문은 그대로 |

---

## 주요 파일

- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` · `SkillActivationUseCase.cs`
- `Assets/_Project/Scripts/Application/Combat/TimedDamageEffectPresentation.cs` · `ProjectileKillPresentationSchedule.cs`
- `Assets/_Project/Scripts/Application/Skill/InstantAreaDamageExecutor.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs` · `NetworkTimedDamageEffectState.cs` · `NetworkCombatController.cs` · `UnitAttackShadowProfileResolver.cs`
- `Assets/_Project/Scripts/Infrastructure/Config/SkillDefinition.cs`
- `Assets/_Project/Scripts/Presentation/Effects/TimedDamageEffectPresenter.cs` · `EffectManager.cs` · `SkillActivationVfxPresenter.cs` · `SkillActivationVfxPresenter.LineStrike.cs` · `VfxPoolItem.cs` · `VfxPoolItem.StatusLoop.cs`
- `Assets/_Project/Scripts/Presentation/UI/BuildingSkillPanelUI.cs`
- `Assets/_Project/Resources/Config/Skills/Skill_Spirit_Meteor.asset` · `Resources/Config/SpecialAttackConfig.asset`
- `Assets/Editor/Setup/SupportSkillLoadoutSetup.cs`
- `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs` · `.ProjectileDeathHold.cs` · `.ServerProjectiles.cs` · `.AirStrike.cs` · `.SkillZones.cs` · `.SupportNewStatuses.cs` · `.SupportRegen.cs` · `.SupportShield.cs` · `.SupportBuffSkills.cs`
