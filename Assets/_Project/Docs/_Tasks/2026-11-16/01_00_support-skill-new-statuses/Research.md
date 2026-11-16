# Research — 지원 스킬 2-S 새 상태 + 2-B 건물 상태 (대지의 갑주 · 재생의 뿌리 · 신목의 각성 · 전술 지령 생산 속도)

## 이 문서는 무엇인가

지원 스킬 15개 가운데 아직 「사용 불가」로 꺼져 있거나 반쪽만 동작하는 스킬이 넷 있다.

- 정령 **대지의 갑주** — 맵 전체 아군 유닛이 **받는 피해를 줄인다.**
- 초월 **재생의 뿌리** — 맵 전체 아군 유닛 · 건물이 **체력을 조금씩 되찾고**, 그동안 받는 **회복이 더 커진다.**
- 초월 **신목의 각성** — 맵 전체 아군 유닛 · 건물에 **피해를 대신 받아 주는 막(쉴드)** 을 씌운다.
- 인간 **전술 지령** — 지금은 공격력만 올린다. 기획에는 **유닛 생산 속도**도 올리게 되어 있는데 그 부분이 없다.

이 넷이 꺼져 있던 이유는 게임에 **「받는 피해 줄이기」 · 「쉴드」 · 「재생 · 회복 증폭」 · 「생산 속도」라는 상태가 아예 없고**, **건물에 거는 상태**도 EMP(방어탑 무력화) 하나뿐이기 때문이다. 이 작업은 그 상태들을 만들어 네 스킬이 실제로 동작하게 하고, 각 스킬이 유닛과 건물에 보여 줄 이펙트(VFX) **칸을 미리 만들어 두어** 나중에 프리팹만 넣으면 코드 수정 없이 보이게 하는 것이다. 이펙트 프리팹은 아직 없다.

이 문서는 **지금 코드가 어떻게 되어 있는지**(어디를 고쳐야 하고 무엇이 함께 영향을 받는지)를 정리한다. 무엇을 어떤 순서로 만들지와 결정 항목은 [Plan.md](Plan.md)에 있다. **코드 · 자산은 이 문서 작성 중 바뀌지 않았다.**

> **현재 상태 (2026-11-16):** 조사 기록(HEAD `14247c6`). 결정 S-1~S-17은 Plan 1절 — 사용자 지시 「문서작성하고 바로 … 구현 진행해」에 따라 **메인 세션 추천안으로 진행**하며, 테스트 안내 때 사용자에게 보고하고 이견이 있으면 바꾼다(**사용자 확정이 아니다**). 구현 · 사용자 테스트는 단계 A → C → B로 나누고 이번 회차는 **단계 A**만 한다(Plan 3절).

**사용자 대화 원문(메인 세션 전달):**
1. 「다음작업후보로 지원스킬 관련 작업을 진행하면 될거같아」
2. 「1번부터 진행하자」 — 1번 = 상위 Plan 3-2 「2차」의 2-S(새 상태) + 2-B(건물 상태).
3. 「문서작성하고 바로 테스트시 사용할 검사로그까지 구현 진행해 완료되면 테스트절차 안내해줘」 — 이 Research · Plan 작성과 구현 진행 지시. Testcase 제외.

**출처 · 표기:** 아래 사실은 game-programmer의 HEAD `14247c6`(브랜치 `claude/vfx-code-followups`) 읽기 전용 조사 결과 **[전달]** 이다. 그중 document-manager가 같은 커밋의 파일을 직접 다시 읽어 행 내용을 대조한 것만 **[확인]** 으로 적었다. 대조에서 행 번호 · 파일이 어긋난 인용은 지우지 않고 **[정정 — 문서 담당]** 으로 바른 자리를 덧붙였다(15절에 모음). `[문서 담당]` = document-manager가 덧붙인 관찰(결정 아님). 경로 머리 `Scripts/` = `Assets/_Project/Scripts/`. 「상위 Plan N행」 = 아래 상위 Task Plan의 행 번호.

**상위 Task:** [종족별 지원 스킬 기획 Plan](../../2026-11-02/14_25_race-support-skills-design/Plan.md) — 1-1 · 1-2 · 1-3(슬롯), 2절(효과 · 필요 기능 S · B), 2-1(V14 · V15 · V17 · V19 · V22 · V24), 3-1(진행표), 3-2 「2차」 2-S · 2-B 행, 4절 10~13 · 20 · 22, 5절 O-13. **선례:** [EMP Task](../../2026-11-04/03_54_emp-tower-disable/Plan.md#11-implementation-and-user-test)(건물 Id 상태 서비스 · 서버 전용 상태), [지원 스킬 1차 강화 Task](../../2026-11-10/04_20_support-buff-skills-status-vfx/Plan.md#11-implementation-and-user-test)(발동 순간 대상 D1 · 발동 알림으로 VFX D2 · 임시 프리팹 D14), [지속 상태 구역 Task](../../2026-11-10/16_02_support-skill-persistent-zones/Plan.md#11-7-final-result)(별도 서버 서비스 Z3 · 임시 프리팹 Z-18 · 형식).

---

## 1. 범위

| 이번 Task 문서가 다루는 것 (단계 A · C · B 전체) | 이번 Task에서 하지 않는 것 |
|---|---|
| 받는 피해 감소(대지의 갑주) — 서버의 피해 지점 6곳을 한 헬퍼로 모음 | 이펙트 프리팹 제작(V14 · V15 · V17 · V19 · V22 · V24 — VFX 기획 · 제작 단계, 상위 Plan 3-0) |
| 생산 속도(전술 지령의 생산 부분) | 수치 확정(밸런싱 단계 — 상위 Plan 3-3, O-13) |
| 쉴드(신목의 각성) — 유닛 · 건물 | HP 바 같은 새 UI(지금 게임에 HP 바가 없다 — 3절 4) |
| 재생 · 회복 증폭 · 건물 재생(재생의 뿌리) | 원소 공명(기획 보류) · 연막탄 회피 · 공습 · 메테오 |
| 건물에 붙는 상태 VFX 칸과 재생 경로 | AI의 스킬 사용(AI는 스킬을 쓰지 않는다 — 9절) |
| 테스트 항목별 진단 로그(WORKFLOW [4] 「테스트 항목 진단 로그 규칙」) | |

- **이번 회차 구현 · 사용자 테스트는 단계 A**(공통 데이터 칸 · 새 서비스 뼈대 · 피해 지점 단일화 + 대지의 갑주 + 전술 지령 생산 속도)뿐이다. 단계 C(쉴드 · 건물 상태 VFX · 쉴드 소진 알림)와 B(재생 · 증폭 · 건물 재생)는 그 뒤 회차다(Plan 3절 · S-16).

---

## 2. 근거로 읽은 규칙 · 문서

- `GameSystemRules/GameSystemRules_Skills.md` — 규칙 3(건물 글로벌 쿨다운) · 7(데이터 주도 — 요소 표 「유닛 상태 VFX」 행) · 9(슬롯 1~5 스킬 · 6 철거) · 13(타입 C — 「대상 판정 시점」 발동 순간 · 「다시 걸릴 때」 교체 · 「수치」 임시값 8초 · 쿨다운 15 — 162행) · 15(즉시 발동) · 25(서버 권위 · 발동 표현 · 타입 C 상태 VFX · EMP) · 26 · 27 · 28 [확인]
- `GameSystemRules/GameSystemRules_Buildings.md` 「MistShrine 물안개 힐 시스템」 규칙 5(최대 체력이면 회복 안 됨) · 8-1(독립 누적기 + 매 틱 대상 재수집) · 8-2(지속 효과 목록에 회복을 넣지 않는다) · 14(자연회복과 별개 채널 · 중첩) · 15(실제로 회복된 대상만 표시) · 24(건물 회복 경로) [확인]
- `GameSystemRules/GameSystemRules_Units.md` 「전투 규칙 — 방어력 데미지 감쇄」 규칙 44(공식 · 최소 1 · 삽입 지점 · DoT 틱값 미적용) [확인]
- `GameSystemRules/GameSystemRules_Buildings.md` 「건물 철거 시스템」 규칙 5(생산 큐 처리) [확인]
- `WORKFLOW.md` [3] · [4] (「기존 로직 제거 규칙」 · 「테스트 항목 진단 로그 규칙」) [확인]
- 상위 Plan 1절 · 2절 · 2-1 · 3-1 · 3-2 · 4절 · 5절 O-13 [확인 — 아래 4절 표의 행]

---

## 3. 핵심 발견 5가지 `[전달]`

1. **「유닛 재생은 기존 타입 C 지속 회복으로 가능」(상위 Plan 141 · 216행)은 HEAD에서 그대로 쓸 수 없다.**
   - 체력이 가득 찬 유닛의 회복 효과는 첫 틱에 지워진다(`UnitCombatUseCase.cs` 2972, 2975~2995) — 2972 · 2975 [확인].
   - BloomFairy 힐과 저장 키(대상 + Heal)가 같아 서로 덮어쓴다(2733) [확인].
   - 수치가 「초당 회복량」이 아니라 「총 회복량」이다(2559~2561) [확인 — 2561 `ApplyTimedEffect(null, unit, TimedEffectKind.Heal, healAmount, duration)`].
   - 유닛 상태 VFX 장치가 이 종류를 일부러 건너뛴다(`UnitBuffVfxPresenter.cs` 280~284) [확인].
2. **피해가 HP에 들어가는 곳이 서버에 6곳 흩어져 있다.** 받는 피해 감소와 쉴드는 이 6곳을 모두 거쳐야 한다 [확인 — `Scripts/Application`의 `.TakeDamage(` 6건, 6-1 표].
3. **스킬 하나가 상태 하나만 걸 수 있는 데이터 구조다**(`SkillData` `StatusKind` / `Magnitude` 각 1개, `GlobalStatusChangeExecutor.cs` 42~51) [확인]. 네 스킬 중 대지의 갑주만 효과가 하나이고, 나머지 셋은 2~3개가 필요하다.
4. **화면에 HP 바가 없다.** HP는 맞거나 회복될 때 뜨는 「현재 HP 숫자」로만 보인다(`FloatingHpTextSpawner` 333 `$"{evt.CurrentHp}"` [확인], Presentation 안 `MaxHp` 참조 0건 [확인]). 그래서 쉴드량을 보여 줄 곳이 없고, 피해를 전부 막은 공격은 Host에는 숫자가 뜨고 Client에는 뜨지 않는 비대칭이 생긴다(6-2).
5. **한 팀은 한 종족이고 강화는 아군에게만 걸린다.** 그래서 대지의 갑주(정령)와 쉴드(초월)는 같은 유닛에 함께 걸릴 수 없다. 재생 증폭이 실제로 만나는 회복은 초월 쪽(자연회복 · MistShrine · BloomFairy)뿐이다.

---

## 4. 기획 원문 인용 (상위 Plan)

| 스킬 | 효과 · 대상 · 조준 (원문) | 수치 | 등급 |
|---|---|---|---|
| 대지의 갑주 (정령 4) | 「아군 받는 피해 감소, **맵 전체**」, 대상 「아군 유닛」, 「즉시(맵 전체)」, 필요 기능 「기존(C 경로) + **S**(받는 피해 감소)」(138행). 분류 「강화 · 맵 전체 `[확정]`」, 유닛만(92행) | 없음 — 「받는 피해 감소율」은 밸런싱 몫(213행) | [확인] |
| 재생의 뿌리 (초월 2) | 「체력 재생 부여 및 증폭, 맵 전체(O-4). **건물도 포함**(사용자 결정)」, 대상 「아군 유닛 · 건물」, 「유닛 재생 부여 = 기존(타입 C 지속 회복) · 증폭 = **S** · 건물 = **B**」(141행, 107행) | 없음 — 「재생 · 증폭량(유닛 · 건물), MistShrine 회복과의 관계(O-13)」(216행) | [확인] |
| 신목의 각성 (초월 3) | 「아군 건물 · 유닛에 쉴드, 맵 전체」, 「**S**(쉴드) + **B**」(142행, 108행) | 없음 — 「쉴드량(유닛 · 건물)」(217행) | [확인] |
| 전술 지령 (인간 5) | 「아군 공격력 · 생산 속도 증가, 맵 전체」, 대상 「아군 유닛 · 생산」, 「생산 속도 = **S**」(134행). 「생산 속도 = 건물 `[메인 해석]`」(77 · 82행) | 공격력만 정해짐(1.5배 · 8초 · 쿨 15, 임시값). 생산 배율 없음(209행) | [확인] |

- 지속시간은 네 스킬 모두 기획에 없다. 기존 강화의 임시값은 8초 · 쿨 15다(Skills 규칙 13 「수치」, `GameSystemRules_Skills.md` 162행) [확인].
- 기획 4절의 관련 열린 물음 [확인 — 상위 Plan 4절]: 10 「모든 피해를 덮는 한 자리가 있는가」 · 11 「재생 증폭이 자연회복 · MistShrine · BloomFairy와 어떻게 합쳐지는가」 · 12 「생산 속도를 넣을 자리, 진행 중인 생산에 소급하는가」 · 13 「건물용 상태 저장 공간」 · 20 「강화 상태 VFX … 건물에 붙일 자리, 전술 지령 생산 쪽 VFX를 생산 건물에 붙일지」 · 22 「건물 재생 경로 · 동기화 · 건물 쪽 증폭이 무엇을 증폭하는가」(상위 Plan 333~354행 [전달]).
- O-13은 「밸런싱 때 확인 — 열어 두기만」이다(상위 Plan 382행 [확인], B-3 301행 [전달]).
- 기획이 정해 둔 VFX 칸 [확인]: V24 대지의 갑주 발동 · V19 대지의 갑주 강화 상태(168~169행), V15 뿌리 · 재생 발동 · V22 재생 상태(유닛 · 건물)(171~172행), V14 쉴드(유닛 · 건물)(173행), V17 전술 지령 강화 상태(160행 — 생산 건물 쪽은 4절 20으로 열림).
- 슬롯 [확인]: 상위 Plan 1-1 인간 5 = 전술 지령, 1-2 정령 4 = 대지의 갑주, 1-3 초월 2 = 재생의 뿌리 · 3 = 신목의 각성. 셋업 도구의 0부터 세는 슬롯 값(전술 지령 `h, 4` · 대지의 갑주 `s, 3` · 재생의 뿌리 `t, 1` · 신목의 각성 `t, 2` — 5절)과 맞다.

---

## 5. 지금 자산 · 셋업 도구 상태

자산 경로 `Assets/_Project/Resources/Config/Skills/`.

| 자산 | 값 | 등급 |
|---|---|---|
| `Skill_Spirit_EarthArmor` · `Skill_Trans_RootsOfRegeneration` · `Skill_Trans_SacredTreeAwakening` | 세 개 모두 `_isAvailable: 0` · `_mechanic: 2`(타입 C) · `_statusKind: 0` · `_magnitude: 0` · `_duration: 0` · `_cooldown: 15` · `_targetsAllies: 1`. `_statusVfx*` 키 없음(불러오면 기본값). 이름 칸은 채워져 있다 | [확인] |
| `Skill_Human_TacticalOrder` | 사용 가능, `_statusKind: 2`(공격력 배율) · `_magnitude: 1.5` · `_duration: 8` · `_cooldown: 15`, `_statusVfxPrefab: {fileID: 0}`(비어 있음). 설명 문구에 이미 「…공격력과 생산 속도를 높인다」가 들어 있다 | 값 [확인] / 설명 문구 [전달] |

| 셋업 도구 `Assets/Editor/Setup/SupportSkillLoadoutSetup.cs` | 등급 |
|---|---|
| 92행 `private const int ExpectedAvailableCount = 8;` | [확인] |
| 287행 `InstantBuff("Skill_Human_TacticalOrder", h, 4, "전술 지령", …)` (공격력 배율 1.5 · 8초) | [확인 — 줄 머리] |
| 300 · 311 · 313행 `InstantPlaceholder("Skill_Spirit_EarthArmor", s, 3, …)` · `("Skill_Trans_RootsOfRegeneration", t, 1, …)` · `("Skill_Trans_SacredTreeAwakening", t, 2, …)` | [확인] |

| 자가 검증 스위트가 고정한 값 | 등급 |
|---|---|
| `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.SkillZones.cs` 347~350 — 사용 가능 수 `== 8` | [확인] |
| `.SupportBuffSkills.cs` 174~178 — 강화 스킬(전술 지령 포함)은 사용 가능 · 즉시 · 타입 C · 종류 · 배율 · 8초 · 쿨 15 | [확인] |
| 스키마 `== 9` — `.ServerProjectiles.cs` 264 · `.SupportBuffSkills.cs` 264 · `.SkillZones.cs` 344 | [확인] |
| 스키마 상수 `PresentationContractSchemaRevision = 9` — `Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs` 37 | [확인 — 문서 담당 추가] |

- 전술 지령은 지금 공격력만 건다. 생산 경로에는 배율이 하나도 없다 [전달].
- AI 설정의 `productionTimeMultiplier`는 값만 전달되고 읽는 곳이 없다(`GameBootstrapper.Setup.cs` 904에서 넘김, 그 밖은 선언 · 생성자 대입뿐) [확인].

---

## 6. 새 상태 4종 설계 근거

### 6-1. 받는 피해 감소 — 피해가 HP에 들어가는 6곳

| # | 위치 | 덮는 피해 | 지금 계산 | 등급 |
|---|---|---|---|---|
| ① | `UnitCombatUseCase.cs` 2029~2124 `ApplyDamageToVictim`(`TakeDamage` 2047) | 직접 공격, 유닛 투사체 착탄(`Projectiles.cs` 706 → `ApplyAttackDamageCore` 1094 → `ExecuteAttack` 1900), 휩쓸기(1912 위임), 파도 피해(3402 유닛 · 3435 건물), 다중 히트 | `ComputeFinalDamage` = 건물 2배 → 방어 감쇄(1988~2011) | 2029 · 2047 · 1988 · 2011 [확인] / 호출 경로 [전달] |
| ② | 2388 `ApplyFixedDamageToVictim`(2395) | QuakeSpirit 스플래시(2369) | `ComputeFinalDamage` | 2388 · 2395 [확인] |
| ③ | 2592 `ApplySkillInstantDamageToVictim`(2606) | 스킬 타입 A(포격) | 방어 감쇄만 | 2592 · 2606 [확인] |
| ④ | 3075 `ApplyTimedDamageToUnit`(3079) | 지속 피해 전부(독 · 화상 · 타입 B 장판) | **감쇄 없음**(1982행 「DoT 무감쇄」 — Units 규칙 44) | 3075 · 3079 · 1982 [확인] |
| ⑤ | `TowerCombatUseCase.cs` 389 `ExecuteTowerAttack`(427) | RuneSpire · VineTower 즉발 | 방어 감쇄 | [확인] |
| ⑥ | `TowerCombatUseCase.cs` 481 `ApplyTowerProjectileImpact`(493) | 인간 대포탑 투사체 | 방어 감쇄 | [확인] |

- 클라이언트 쪽 `NetworkHealthSync.cs` 368 · 512, `NetworkCombatController.cs` 3510 · 3554의 `TakeDamage`는 서버 값을 따라가는 동기화일 뿐 피해의 출처가 아니다 [확인 — 4줄 모두 Infrastructure, 서버 HP 차이 · 강제 사망].
- 6곳을 다 덮는 한 자리는 **없다**(상위 Plan 4절 10의 답) [전달].
- **제안 `[전달 — 프로그래머]`:** `IncomingDamage` 같은 헬퍼 하나를 만들어 6곳에서 `TakeDamage` 직전에 부른다. 순서: 방어 감쇄 → 받는 피해 배율(유닛만) → 쉴드 흡수 → HP. `TowerCombatUseCase`에는 기존 `SetBuildingStatusSystem`(144행 [확인])처럼 설정자로 주입한다 — 생성자를 바꾸면 회귀 fixture가 깨진다.
- 반올림 · 최소 피해 제안: `DamageCalculator.ApplyDefense`와 같은 규칙 — 반올림 AwayFromZero, 결과가 1보다 작으면 1(`Scripts/Domain/Combat/DamageCalculator.cs` 70~76 — 74 `MidpointRounding.AwayFromZero` · 77 `return result < 1 ? 1 : result;`) [확인].
- 지속 피해(④)에 감소를 적용할지는 **결정 항목**이다. 지속 피해는 지금 방어도 무시한다. 독 틱이 2이면 0.7배는 1.4 → 1이 된다 [전달].
- `[문서 담당]` Units 규칙 44의 「DoT 틱값에는 미적용」은 **방어력 감쇄**에 대한 것이다. 스킬의 받는 피해 감소를 DoT에 적용하는 것(S-4 추천)은 그 문장을 바꾸지 않지만, 「피해 순서(방어 감쇄 → 스킬 배율)」와 함께 규칙 문서에 적을 자리가 필요하다(Plan 11절).

### 6-2. 쉴드 (유닛 · 건물) — 단계 C

- HP와 별개인 흡수량으로 둔다. 피해 순서는 피해 감소 → 쉴드 흡수 → 남은 피해만 HP로 [전달].
- 기존 상태 값 `StatusEffect`는 남은 시간만 바뀌고 강도는 고정이라 줄어드는 쉴드량을 담을 수 없다. 쉴드는 새 저장소가 필요하다 — 출처 「`StatusEffect.cs:77-83`」[전달] **[정정 — 문서 담당: `Scripts/Domain/Status/StatusEffect.cs`는 51행이다. 강도 `Magnitude { get; }`는 28행, 바뀌는 남은 시간 `RemainingDuration { get; set; }`은 34행. 뜻(강도 고정 · 시간만 변경)은 맞다]**.
- **HP 동기화에 미치는 영향 [전달]:** 서버는 줄어든 HP만 보내므로 동기화 구조는 바뀌지 않는다. 다만 **전부 막은 공격**이 문제다.
  - Host: `OnEntityDamaged`가 그대로 발행되어 같은 HP 숫자 · 피격 VFX · 타격 반응이 나온다(`HitPresentationQueue.cs` 3~14).
  - Client: HP 차이가 0이라 이벤트를 다시 내지 않는다(`NetworkHealthSync.cs` 365~386 — `if (diff > 0)` 안에서만 재발행) [확인].
  - `OnEntityDamaged` 구독자는 이 두 곳뿐이다(`HitPresentationQueue.cs` 982, `NetworkHealthSync.cs` 87) [확인 — `Subscribe` 검색 2건].
- **HP 표시:** HP 바가 없다. 표시는 `FloatingHpTextSpawner`의 현재 HP 숫자뿐이라 쉴드량을 숫자로 보여 줄 자리가 없다 [확인].
- 만료: 남은 쉴드는 버린다. 다시 걸 때(제안): 쉴드량과 시간을 새 값으로 교체하고 더하지 않는다(Skills 규칙 13 「다시 걸릴 때」 교체와 같은 사고방식) [전달].

### 6-3. 재생 · 재생 증폭 · 건물 재생 — 단계 B

- 기존 지속 회복의 실제 동작은 3절 1과 같다. 거기에 더해 완료 텍스트가 1번 뜬다(2986~2990) [전달].
- **제안 `[전달 — 프로그래머]`:** MistShrine 규칙 8-1(「독립 누적기 + 매 틱 대상 재수집」)과 자연회복 모델을 따르는 **독립 재생 채널**을 만들고 유닛 · 건물을 같은 방식으로 처리한다. MistShrine 규칙 14의 「채널 독립」 원칙과도 맞다. `[문서 담당]` MistShrine 규칙 8-2는 물안개 회복을 지속 효과 목록(`_activeTimedEffects`)에 넣지 못하게 금지한다 — 3절 1의 문제(총량 분할 · 가득 차면 지움)가 같은 이유이므로 새 재생 채널도 그 목록을 쓰지 않는 쪽이 규칙과 맞다.
- 회복이 HP에 들어가는 곳(증폭 배율을 걸 곳):
  - `UnitCombatUseCase.ApplyHealToUnit`(2144~2180) — 호출 자리: 자연회복 650 · BloomFairy · 지속 회복 2941 · 파도 3418 [확인]
  - `MistShrineUseCase.ApplyHealToUnitEntity`(569~585, `target.Heal` 직접 — 574) [확인]
  - `MistShrineUseCase.ApplyHealToBuildingEntity`(592~606, 597) [확인]
- 증폭 반올림이 문제다. 자연회복은 한 번에 정수 1~수 HP씩 들어온다(`UnitCombatUseCase.cs` 631~650). 매번 곱해서 반올림하면 값이 크게 틀어지므로 **대상별 소수 누적(나머지 이월)** 방식을 제안한다 [전달].
- 건물 재생 경로는 이미 있다: `BuildingData.Heal`(최대 HP에서 멈춤, `BuildingData.cs` 130~137 [확인 — 132 `public void Heal(int amount)` · 137 `if (Hp > MaxHp) Hp = MaxHp;`]) → `OnEntityHealed(isUnit:false)` → `SyncBuildingHealClientRpc`(`NetworkHealthSync.cs` 202~205, 313~327, 445~479). 새 RPC가 필요 없다 [전달].
- 증폭이 함께 걸리는 회복: 초월 팀에서는 자연회복 · MistShrine · BloomFairy · 재생의 뿌리 자체. 파도 회복은 정령이라 만나지 않는다 [전달].

### 6-4. 생산 속도 — 단계 A

- 생산 진행 구조:
  - 싱글 / 서버: `ProductionTicker.Update` → `UnitProductionUseCase.Tick(dt)`(`ProductionTicker.cs` 302 [확인], `UnitProductionUseCase.cs` 244~253 [확인] → `TickProduction` 408~418 [확인 — `state.ElapsedTime += deltaTime;`]).
  - 순수 Client: `TickClientProgress` → `TickProgressOnly(dt)`로 진행 막대만 흉내 낸다(`ProductionTicker.cs` 291 · 655~662, `UnitProductionUseCase.cs` 260~270) [확인 — 291은 주석이고 `TickClientProgress(dt)` 호출은 292, `TickProgressOnly` 호출은 662]. 필요 시간은 `ProductionStartedClientRpc`로 한 번만 받는다(`NetworkProductionController.cs` 308~311, 645~670) [전달].
  - 필요 시간은 생산 시작 때 정해진다(`UnitProductionUseCase.cs` 324 · 390행 `GetProductionTime`) [확인].
- 안 A: `TickProduction`에서 `dt × 팀 배율` → 진행 중인 생산에도 즉시 적용되고, 정확히 만료 시각에 끝난다 [전달].
- 안 B: 시작 때 필요 시간을 줄임 → 진행 중인 생산에는 적용되지 않고, 버프 끝 무렵 시작한 생산은 끝까지 빨라진다 [전달].
- 배율을 클라이언트가 모르면 Client 진행 막대가 늦게 차고, 다음 생산 시작 RPC로 0%로 튄다(동작에는 영향 없음, 화면만 어긋남) [전달].
- 생산 건물을 업그레이드하면 Id가 바뀌고 큐가 취소 · 환불된 뒤 새로 등록된다(`BuildingPlacementUseCase.cs` 416~439, `ProductionTicker.cs` 439~461) — 건물 Id로 저장하면 업그레이드 순간 효과가 끊긴다 [전달].
- **추천 `[전달 — 프로그래머]`:** 안 A. 배율은 **팀 단위**(아군 생산 건물 전부, 업그레이드 · 새 건물도 자연스럽게 포함). Client 막대 맞춤은 RPC 없이 발동 알림을 받은 순간 같은 배율 · 시간을 로컬 타이머로 따라 한다(1차 강화 D2 선례). 대상 범위는 결정 항목.

### 6-5. 저장 위치 비교

| 안 | 내용 | 장점 | 단점 |
|---|---|---|---|
| 기존 상태 시스템 확장 | `StatusEffectSystem`은 유닛 Id 키, 같은 종류를 통째로 교체. 틱은 순수 Client 거울에서도 돌고(`GameBootstrapper.cs` 773~777 [확인]), `RemoveUnit`(`StatusEffectSystem.cs` 163 [확인])을 부르는 곳이 0건 [전달] | 피해 배율 · 회복 배율 같은 고정 배율은 그대로 들어가고, 기존 `[SKILL-BUFF]` 적용 / 만료 줄이 자동으로 따라온다 | 쉴드(줄어드는 양) · 건물 · 재생 누적은 넣을 수 없다. Client 거울에서도 걸리므로 재생을 넣으면 이중 회복 위험 |
| 건물 상태 확장 | `BuildingStatusEffectSystem`은 EMP 하나만 저장(`_attackDisabled`) | — | 넓히려면 종류별 저장소로 일반화해야 한다 |
| **별도 서비스**(Z3 선례 — 상태 시스템 무변경) | 서버 권위 틱에서만 도는 새 서비스 하나에 유닛 · 건물을 따로 저장하고 받는 피해 배율 · 회복 배율 · 재생 · 쉴드 · 생산 배율을 모두 둔다 | 같은 종류 덮어쓰기(규칙 13)와 섞이지 않고, Client 거울 · 이중 회복 위험이 없고, 경기 종료 `Close`와 사망 / 파괴 정리를 한 곳에서 한다 | 기존 `[SKILL-BUFF]` 적용 / 만료 줄이 자동으로 따라오지 않아 새로 만들어야 한다 |

- 「같은 종류를 통째로 교체」 출처 「`UnitStatusState.cs:143-157`」[전달] **[정정 — 문서 담당: `Scripts/Domain/Status/UnitStatusState.cs`는 133행이다. 교체는 `ApplyOrRefresh` 43행 · 같은 종류 비교 50행. 143~157행은 `StatusEffectSystem.cs`의 배율 조회(`GetMoveSpeedMultiplier` · `CanAttack`)다]**.
- `_attackDisabled` 출처 「87행」[전달] **[정정 — 문서 담당: 선언은 `BuildingStatusEffectSystem.cs` 80행. 87행은 「경기 종료로 닫혔는지」 주석]**.
- **추천 `[전달 — 프로그래머]`: 별도 서비스.** 지금 기존 상태 시스템 경로로 겹치는 조합은 없지만, 쉴드와 건물 때문에 어차피 새 저장소가 필요하므로 한 곳으로 모으는 편이 완성도가 높다.
  - 스킬 데이터는 타입 C를 유지하고 「추가 효과 목록」(대상 유닛 / 건물 · 종류 · 강도) 칸을 더하는 방식. 유닛 상태 VFX 장치가 계속 그대로 동작한다(`UnitBuffVfxPresenter.cs` 270행 조건 — 타입 C — 유지 [확인]).
  - 이 경우 주 종류는 `HealOverTime`이 아닌 새 값이어야 한다(280행에서 건너뜀 [확인]).

---

## 7. 동기화 `[전달]`

- 유닛 상태 VFX: 발동 알림 경로(`UnitBuffVfxPresenter` 발동 구동기)로 충분하다. 스키마는 9 그대로.
- 건물 VFX: 같은 방식으로, 각 화면이 알림을 받은 순간 시전 팀의 살아 있는 건물을 찾아 붙이면 RPC가 필요 없다.
- 쉴드가 시간 전에 깨졌을 때 VFX를 끄려면 서버 알림이 필요하다. Client는 흡수량을 계산할 수 없기 때문이다(동기화는 HP만 보냄). 새 RPC를 만들면 **스키마 9 → 10**이고 fixture 3곳(5절 표)을 고쳐야 한다. 알림 없이 가면 깨진 뒤에도 시간이 끝날 때까지 VFX가 남는다.
- 회복 HP는 기존 동기화 경로로 간다.
- 새 상태 값은 서버 전용으로 두고, 기존 상태 RPC(`StatusAppliedClientRpc`, `NetworkSkillController.cs` 525~532 [확인])로는 보내지 않는 것을 제안한다. `HealOverTime`을 보내지 않는 「576행 선례」와 같다 **[정정 — 문서 담당: 그 선례는 `UnitCombatUseCase.cs` 2576 `if (raiseNetworkEvent && kind != StatusEffectKind.HealOverTime)`이다. `NetworkSkillController.cs` 576행은 그 선례가 아니다]**.

---

## 8. VFX 경로 `[전달]`

- 유닛: 스킬별 `_statusVfxPrefab`과 `UnitBuffVfxPresenter`를 그대로 쓴다. 빠지는 조건 셋 — 타입 C가 아님(270), 종류 없음 또는 지속 0(274), `HealOverTime`(280) [확인].
- 건물: `TowerDisableVfxPresenter`는 방어탑 전용이다(245행 AutoTower 판정, 위치 · 크기를 종족 프리셋에서 읽음 `EffectManager.cs` 627~658).
- **제안:** 스킬 자산에 「건물 상태 VFX」 칸(프리팹 · 크기 · Y 보정)을 새로 두고 일반 건물용 표현 장치를 새로 만든다.
  - 탑 장치를 넓히면 EMP 판정과 얽히므로 따로 두는 쪽을 추천.
  - 위치는 건물 GameObject 위치(`BuildingYOffset` 0.1 > 타일 윗면 0.05 — `GameConfig.cs` 200 [확인]).
  - 건물 크기 차이를 맞출 표는 없다. 첫 판은 스킬 배율 하나로 시작하는 안과 건물 종류별 배율 표를 두는 안(결정 항목).
  - 정지: 업그레이드(`OnBuildingUpgraded` — 이전 GameObject가 사라져도 풀 항목은 남음), 파괴, 경기 종료, 재경기.
- 발동 VFX(V24 · V15): 기존 「발동 VFX」 칸이 시전 건물 위에서 재생한다(1차 강화 D3).
  - 첫 발동 프리팹이 실제로 확인되면 주석으로 남겨 둔 `SkillActivationVfxPresenter.cs` 153 블록을 지우고, 스위트 B-8(`SupportBuffSkills.cs` 277 [확인 — `Contains("// if (!p.HasAim)")`])도 같은 커밋에서 고쳐야 한다.

---

## 9. 새 유닛 · 사망 · 경기 종료 · AI `[전달]`

- 새 유닛 · 새 건물: Skills 규칙 13 「대상 판정 시점」(발동 순간만)에 따라 제외 제안. 생산 속도는 6-4대로 팀 단위가 결정 항목.
- 사망 · 파괴: 기록을 제거하고 VFX를 즉시 정지. 업그레이드 시 새 Id에 효과를 넘길지는 결정 항목이고 버리는 쪽 제안.
- 경기 종료: 새 서비스에 `Close()`를 두고 `GameBootstrapper.cs` 562처럼 `OnGameEnd`에서 부른다(562 = `GameEvents.OnGameEnd.Subscribe(_ => _buildingStatusEffectSystem?.Close())` [확인]).
- 재경기: 서비스는 맵을 불러올 때마다 새로 만들고(`Setup.cs` 409 / 441 선례), 표현 장치는 Initialize에서 정리.
- AI: `AIOpponentController`에 스킬 코드가 0건이다 — AI는 이 스킬을 쓰지 않는다(`grep -ci skill` = 0 [확인]).

---

## 10. 진단 로그 설계안 `[전달 — 프로그래머]`

모두 `GameLog.Dev`, `Diag=` · `DiagRev=1`, 상태가 바뀔 때만.

| 머리 | 이벤트 | 테스트 판정 값 | 단계 |
|---|---|---|---|
| `[SKILL-ARMOR]` | `ArmorApplied`(대상 수 · 배율) · `DamageReducedSample`(유닛 · 묶음마다 첫 피격, `Writer=Direct/Projectile/Splash/Skill/DoT/Tower/TowerShell/Wave`, `Raw` · `AfterDefense` · `AfterReduction`) · `ArmorEnded`(피해 지점별 감소 횟수 · `ActualMs`) | 감소 후 값 = 반올림(감쇄 후 × 배율), 6곳 각각 1건 이상 | A |
| `[SKILL-PROD]` | `ProdSpeedApplied` · `ProdCompleteSample`(`RequiredMs` · `ActualMs` · 버프 구간 ms) · `ProdSpeedEnded` · Client `ProdMirrorApplied` / `Ended` | 실제 시간 ≈ 필요 시간 ÷ 배율(버프 구간) | A |
| `[SKILL-SHIELD]` | `ShieldApplied`(유닛 · 건물 수 · 양) · `ShieldAbsorbSample`(들어온 피해 · 흡수 · 남은 양 · HP 피해) · `ShieldBroken` · `ShieldExpired`(버린 양) · `ShieldEnded`(합계) | 흡수 + HP 피해 = 들어온 피해 | C |
| `[SKILL-REGEN]` | `RegenApplied` · `RegenTickSample`(대상 첫 회복) · `HealAmpSample`(`Source=Natural/Mist/Bloom/SkillRegen`, 원래 → 증폭 · 이월분) · `BuildingRegenSample` · `RegenEnded`(합계 · 체력 가득으로 건너뛴 수) | 증폭 비율 ≈ 배율 | B |
| VFX | 유닛: 기존 `[SKILL-BUFF] BuffVfxStarted / Stopped / Skipped(NoPrefab)` [확인 — `UnitBuffVfxPresenter.cs` 46~50 머리 주석] / 건물: 새 `BuildingVfxStarted / Extended / Stopped(Path=Expire/Broken/Died/Upgraded/GameEnd/Reinit) / Skipped` | 칸이 비어 있으면 `Reason=NoPrefab` | 유닛 A · 건물 C |

- 쉴드 소진 RPC를 넣으면 `ShieldBrokenReceived` 줄을 더한다(Host와 Client의 Id 목록 일치로 판정).
- 「단계」 열은 document-manager가 덧붙였다(Plan S-16 기준).

---

## 11. 변경 예상 파일 · 구현 단위 · 위험 · 자가 검증 `[전달 — 프로그래머, 네 스킬 전체 기준]`

### 11-1. 변경 예상 파일 (약 20개)

- Domain: `StatusEffectKind`(맨 뒤에 추가 — 지금 `None = 0` ~ `HealOverTime = 5` [확인])
- Application: `SkillData` · `GlobalStatusChangeExecutor` · `SkillActivationContext` / `UseCase` · 새 서비스(+ 진단 partial) · `UnitCombatUseCase`(피해 지점 4곳 · 회복 지점) · `TowerCombatUseCase`(2곳 · 설정자) · `MistShrineUseCase`(회복 2곳) · `UnitProductionUseCase`
- Infrastructure: `SkillDefinition` · (선택) `NetworkSkillController` + 스키마
- Presentation: 새 건물 상태 VFX 장치 · `EffectManager` · `ProductionTicker`(Client 막대)
- Bootstrap: `Setup` / `Map` / `Update` / `NetworkCombatController` 틱 자리
- Editor: 셋업 도구(사용 가능 수 8 → 12) · 스위트 3파일 · 자산 4개

### 11-2. 커밋 단위 (프로그래머 제안 — 네 스킬 전체)

1. 데이터 칸 + 새 서비스 뼈대
2. 피해 지점 단일화 + 대지의 갑주
3. 생산 속도 + 전술 지령
4. 쉴드(+ RPC)
5. 재생 · 증폭 · 건물 재생
6. 건물 VFX 장치
7. 자산 켜기 · 스위트 · 진단

(단계 A는 1 · 2 · 3 · 7의 A 몫 — Plan 4절.)

### 11-3. 위험

- 피해 지점을 하나라도 빠뜨리면 그 피해는 감소 · 흡수되지 않는다(근거: 6곳 grep — 6-1 [확인]).
- 전부 막은 투사체 공격은 Client 피격 표현 보류가 타임아웃 · 빈 방출로 빠진다(`HitPresentationQueue.cs` 281~291). 서버 투사체 END 진단의 판정 수가 바뀔 수 있으나 **확인하지 않았다**(단계 C).
- 스위트가 사용 가능 수 8 · 스키마 9 · 전술 지령 값을 고정하고 있어 같은 커밋에서 고쳐야 한다(5절 표).
- 재생 증폭의 반올림 오차(단계 B).

### 11-4. 자가 검증 후보

- 6곳 각각 감소 · 흡수 적용 / 최소 1 · 반올림 / 쉴드 소진 · 만료 · 교체 / 회복 3곳 증폭 + 이월 합계 / 건물 회복이 최대 HP에서 멈춤 / 생산 배율 시간(5초 × 1.5배 → 3.333초) / 경기 종료 `Close` 뒤 적용 거부 / 소스 게이트(틱 자리 · 6곳 호출).

---

## 12. 결정 후보 (프로그래머 추천과 임시 수치) `[전달]`

Plan 1절에서 S-1~S-17로 정리했다. 원래 번호와의 대응은 Plan 1절 표의 「조사 번호」 열.

1. **임시 수치.** 기획에 수치가 없다. HP 범위(유닛 200~2500, `UnitStatsConfig.asset`)와 회복 크기(MistShrine 초당 10, BloomFairy 3초 200, 자연회복 레벨당 초당 3)를 참고한 제안. 지속은 모두 8초 · 쿨 15. 대지의 갑주 받는 피해 0.7배 · 재생의 뿌리 유닛 초당 20 · 건물 초당 40 · 회복 증폭 1.5배 · 신목의 각성 유닛 150 · 건물 500 · 전술 지령 생산 1.5배.
2. **O-13(MistShrine과의 관계).** 추천: 독립 채널로 함께 적용, 증폭은 MistShrine 회복에도. 최종 확인은 밸런싱.
3. **증폭 범위.** 받는 회복 전부인지 재생의 뿌리 자신의 재생만인지, 건물에도 거는지(기획 4절 22). 추천: 받는 회복 전부, 건물 포함.
4. **지속 피해에 받는 피해 감소를 적용할지.** 추천: 적용, 최소 1.
5. **쉴드를 다시 걸 때.** 추천: 새 값으로 교체(더하지 않음).
6. **전부 막은 피격의 표시.** (a) 지금대로 Host에만 숫자 · (b) Client도 HP 차이 0일 때 이벤트를 다시 냄 · (c) 양쪽 모두 없음. 추천 (b) — 단 (b)가 차이 0인 다른 경우에도 숫자를 띄울 수 있는지 구현 때 확인 필요.
7. **쉴드량 표시.** HP 바가 없으므로 VFX로만(추천).
8. **쉴드가 깨지면 VFX를 바로 끌지.** 끄려면 새 RPC와 스키마 10. 추천: 끈다.
9. **생산 속도 대상.** 추천: 팀 단위(아군 생산 건물 전부, 업그레이드 · 새 건물 포함).
10. **전술 지령 생산 쪽 VFX(V17)를 생산 건물에 붙일지**(기획 4절 20). 추천: 칸만 만들고 비워 둔다.
11. **강화 중 새로 만든 유닛 · 건물, 업그레이드된 건물.** 추천: 제외 · 버림(D1 선례).
12. **건물 VFX 크기.** 추천: 스킬 배율 하나로 시작, 프리팹이 나온 뒤 필요하면 건물 종류별 표.

---

## 13. 범위 나누기 `[전달]`

- **한 번에 하면:** 테스트 항목이 25개 이상으로 늘고, 피해 · 회복 · 생산 · 동기화 · VFX가 한 회차에 섞여 실패 원인을 가르기 어렵다. 스키마 변경까지 겹친다.
- **나누는 안:** A = 공통 데이터 · 새 서비스 · 피해 지점 단일화 + 대지의 갑주 + 전술 지령 생산 속도 / C = 신목의 각성 쉴드(A의 피해 지점과 건물 VFX 장치에 기댐, RPC가 있으면 스키마 10) / B = 재생의 뿌리(회복 지점 3곳 · 건물 재생).
- **추천:** Research / Plan과 결정 1~12는 **한 번에** 정하고, 구현 · 사용자 테스트는 **A → C → B 세 단계**. 쉴드는 A의 피해 지점이 필요하고, 재생은 다른 단계와 독립이라 마지막이 안전하다.
- 건물 VFX 장치는 C에서 처음 필요하다. 결정 10에서 생산 건물 VFX를 붙이기로 하면 A로 앞당긴다.

---

## 14. 범위 밖 관찰 — 기록만 (제안만, 손대지 않음) `[전달]`

- `StatusEffectSystem.RemoveUnit`을 부르는 곳이 없다. 유닛 Id는 단조 증가라 지금 해는 없다.
- `NetworkHealthSync.cs` 416 주석의 「HP바」는 존재하지 않는 UI를 가리킨다.
- AI 설정의 `productionTimeMultiplier`는 읽는 곳이 없다(5절 [확인]).

---

## 15. 대조 중 바로잡은 인용 (document-manager, HEAD `14247c6`)

| 조사 원문 인용 | 실제 | 영향 |
|---|---|---|
| `StatusEffect.cs:77-83` (강도 고정 · 시간만 변경) | 파일은 51행 — `Magnitude { get; }` 28 · `RemainingDuration { get; set; }` 34 | 뜻은 맞다. 행만 다르다 |
| `UnitStatusState.cs:143-157` (같은 종류 통째 교체) | 파일은 133행 — `ApplyOrRefresh` 43 · 같은 종류 비교 50. 143~157은 `StatusEffectSystem.cs` 배율 조회 | 뜻은 맞다. 파일 · 행이 다르다 |
| `BuildingStatusEffectSystem` `_attackDisabled` 87행 | 선언 80행(87은 경기 종료 주석) | 행만 다르다 |
| `HealOverTime`을 보내지 않는 「576행 선례」(`NetworkSkillController.cs` 문맥) | `UnitCombatUseCase.cs` 2576 | 파일이 다르다 |

---

## 16. 이번 조사에서 확인하지 않은 것

- 전부 막은 투사체 공격이 서버 투사체 END 진단 판정 수를 바꾸는지(11-3 — 단계 C).
- 차이 0 재발행(S-6 (b))이 다른 「차이 0」 경우(예: 이미 맞춘 HP를 다시 받는 경우)에도 숫자를 띄우는지 — 구현 때 확인(단계 C).
- `[문서 담당]` 싱글에서 AI 종족을 고를 수 있는지, AI가 쓰는 유닛 종류(투사체 · 스플래시 · 지속 피해 · 파도 유닛을 쓰는지) — 단계 A 테스트에서 6곳 중 무엇을 싱글로 볼 수 있는지가 여기에 달렸다(Plan 8절). 이 문서에서 조사하지 않았다.
- `[문서 담당]` AI가 방어탑을 짓는지 — 메인 세션 지시는 「AI는 탑이 없다」이며 이 문서가 다시 확인하지 않았다.
- 상위 Plan 4절 · B-3의 행 번호(333~354 · 301) — 내용은 대조했고 행 번호는 대조하지 않았다.

---

## 17. 변경 이력

| 날짜 | 내용 |
|---|---|
| 2026-11-16 | 첫 판. 사용자 「1번부터 진행하자」 · 「문서작성하고 바로 테스트시 사용할 검사로그까지 구현 진행해 완료되면 테스트절차 안내해줘」로 작성. game-programmer HEAD `14247c6` 조사 전부 옮김([전달]), document-manager 대조분 [확인], 어긋난 인용 4건 [정정 — 15절]. 코드 · 자산 · 규칙 문서 · 다른 Task 문서 · 메모리 무변경 |

---

## 주요 파일

- `Assets/_Project/Docs/_Tasks/2026-11-02/14_25_race-support-skills-design/Plan.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Skills.md` · `GameSystemRules_Buildings.md` · `GameSystemRules_Units.md`
- `Scripts/Application/UseCases/UnitCombatUseCase.cs` · `TowerCombatUseCase.cs` · `MistShrineUseCase.cs` · `UnitProductionUseCase.cs` · `SkillActivationUseCase.cs`
- `Scripts/Application/Services/StatusEffectSystem.cs` · `BuildingStatusEffectSystem.cs`
- `Scripts/Domain/Status/StatusEffectKind.cs` · `UnitStatusState.cs` · `StatusEffect.cs` · `Scripts/Domain/Combat/DamageCalculator.cs`
- `Scripts/Infrastructure/Network/NetworkHealthSync.cs` · `NetworkProductionController.cs` · `NetworkSkillController.cs` · `UnitAttackShadowProfileResolver.cs`
- `Scripts/Presentation/Effects/UnitBuffVfxPresenter.cs` · `TowerDisableVfxPresenter.cs` · `EffectManager.cs`
- `Scripts/Presentation/Production/ProductionTicker.cs`
- `Assets/_Project/Resources/Config/Skills/`
- `Assets/Editor/Setup/SupportSkillLoadoutSetup.cs`
- `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.SkillZones.cs` · `.SupportBuffSkills.cs` · `.ServerProjectiles.cs`
