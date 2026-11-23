# Research — 지원 스킬 「연막탄」 (인간 FlightFacility 슬롯 4)

## 이 문서는 무엇인가

인간 스킬 건물(FlightFacility)의 네 번째 스킬 **연막탄**은 기획상 「지정한 지점에 연막을 깔면, 연막 안에 있는 **우리 편 유닛**이 적의 **원거리 단일 공격**을 일정 확률로 피하는」 스킬이다. 피하면 날아오던 투사체가 옆으로 빗나가 보이고, 피해 숫자는 뜨지 않는다(「MISS」 글자도 띄우지 않는다). 지금 게임에는 슬롯에 들어가 있기만 하고 **「사용 불가」로 꺼져 있다.**

연막 「구역」 자체 — 들어오면 걸리고 나가면 풀리는 원 — 는 이미 만들어 둔 서리 감옥 · 수액 늪 장치를 거의 그대로 쓸 수 있다. 어려운 것은 **「피하기(회피)」** 다. 적의 원거리 공격이 우리 유닛에게 피해를 주는 길이 지금 **세 갈래로 나뉘어 있고**, 갈래마다 「여기서 피했다고 판정하고 피해를 막는 자리」와 「화면에 빗나감을 보여 주는 방법」이 다르다.

1. **날아가는 투사체**(Tank · CannonCart · InfernoSpirit · StreamSpirit · FoxMagician · EagleArcher + 인간 대포탑) — 서버가 발사 · 비행 · 착탄을 모두 관리한다. **쏘는 순간** 피할지 정해야 화면에서 투사체가 처음부터 옆으로 날아가 빗나가 보인다. 그 정보를 Client에 보내려면 발사 알림의 모양이 바뀌어 **접속 호환 번호(스키마)가 11 → 12**가 된다.
2. **즉시 맞는 총탄**(Pistoleer · Assault · Sniper) — 날아가는 그림(트레이서)이 원래 없다. 피하면 숫자만 안 뜨면 된다. 서버 쪽에는 「피함」이라는 결과 종류가 이미 준비돼 있다.
3. **즉시 맞는 방어탑**(정령 RuneSpire 번개 · 초월 VineTower) — 사용자가 「모든 방어탑 포함」으로 정했다. 번개는 지금 맞은 유닛을 따라가 꽂히므로, 빗나가 보이게 하려면 탑 공격 표현 알림에 「빗나감」을 실어야 한다.

이 문서는 **지금 코드가 어떻게 되어 있고, 어디를 고쳐야 하며, 무엇이 함께 영향을 받는지**를 정리한다. 무엇을 어떤 순서로 만들지, 남은 결정, 테스트 방법은 [Plan.md](Plan.md)에 있다. **코드 · 자산은 이 문서 작성 중 바뀌지 않았다.**

> **현재 상태 (2026-11-23): 조사 기록 — 구현 착수 전, Plan 사용자 승인 대기.** 사용자 결정 · 메인 세션 기본안 · 조사 설계안 · 열린 항목의 구분은 Plan 1~3절. *(2026-11-23 2판 — Plan 2판에 사용자 결정 「1단계부터 켬」 · 「임시 프리팹 넣음」과 메인 기본안(분류 토큰 해시 포함 · 스키마 12 = S-3)이 들어갔다. 이 문서의 조사 내용은 바뀌지 않았다)*

**출처 · 표기:** 코드 사실은 game-programmer의 HEAD `43dbf22` 읽기 전용 조사 결과 **[전달]** 이다. 그중 document-manager가 같은 커밋의 파일을 직접 열어 행 내용을 대조한 것만 **[확인]** 으로 적었다. 대조에서 어긋난 인용은 지우지 않고 13절에 바른 값을 모았다. `[판단]` = 실행 · 실기로 확인하지 않은 판단, `[문서 담당]` = document-manager가 덧붙인 관찰(결정 아님). 경로 머리 `Scripts/` = `Assets/_Project/Scripts/`, `Suite/` = `Assets/Editor/Tools/Combat/`. 행 번호는 모두 HEAD `43dbf22` 기준이다.

**상위 Task:** [종족별 지원 스킬 기획 Plan](../../2026-11-02/14_25_race-support-skills-design/Plan.md) — 1-1 인간 4(76행 「유닛만 · 아군 보호」), 2절 연막탄 행(133행), 2-1 V7(160행) · 182행(지속 범위 스킬 대상 상태 VFX는 새로 넣지 않음), 3-1 인간 4(210행 「회피 확률」), 3-2 「2차 끝 회피 판정(E)」(293행), 4절 17 · 18(349~350행), 5절 O-7(378행 — 열림), 6절 R-11(451행), 위험 「연막탄 RPC · 스키마」(583행). **직전 Task:** [메테오 Task](../../2026-11-18/03_34_support-skill-meteor/Plan.md#14-final-result) · [공습 Task](../../2026-11-17/09_12_support-skill-air-strike/Plan.md#16-final-result)(문서 형식 · 셋업 도구 기대 수 · 스위트 고정값 · 진단 로그 존속).

---

## 1. 범위

| 이 Task가 다루는 것 | 이 Task에서 하지 않는 것 |
|---|---|
| 연막 구역 — 아군 유닛 대상 지속 구역(서리 감옥 · 수액 늪 장치 재사용) + 새 상태 종류 「회피 확률」 | 연막 VFX 프리팹 제작(V7 — VFX 제작 단계. Unity AI 크레딧 소진으로 VFX 제작은 중단 중) |
| 회피 판정 ① 서버 권위 투사체(유닛 6종 + 인간 대포탑) — 발사 순간 판정 · 빗나간 지점 · 착탄 결과 「회피」 | MushroomBomber 포자(범위 폭발 — 「단일」 아님, 메인 기본안 O-7a) · TorrentSpirit 파도 · 근접 · 스킬 · 지속 피해 틱 · 스플래시 |
| 회피 판정 ② 즉발 총탄(Pistoleer · Assault · Sniper) | 연막 안 아군이 **쏘는** 공격에 대한 불이익(메인 기본안 O-S4 — 없음) |
| 회피 판정 ③ 즉발 탑(RuneSpire · VineTower) + 빗나감 표현(사용자 결정 「모든 방어탑 포함」) | 수치 확정(회피 확률 · 반경 · 지속 · 쿨다운 — 밸런싱) |
| 빗나감 화면 표현(투사체가 빗나간 지점으로 날아가 착탄 이펙트만, 숫자 없음) · 스키마 11 → 12 | 연막 속 아군 몸에 붙일 상태 VFX를 정하는 일(O-S12 — VFX 기획 단계) |
| 연막탄 켜기(셋업 도구 기대 수 14) · 진단 로그 · 자가 검증 | AI의 스킬 사용(AI는 스킬을 쓰지 않는다) · 원소 공명(「사용 불가」 그대로) |

---

## 2. 근거로 읽은 규칙 · 문서

- `GameSystemRules/GameSystemRules_Skills.md` — 규칙 7(요소 표 78행~), 규칙 13(타입 C — 상태 종류의 뜻), **규칙 28 「지속 상태 구역」(207행 — 209행 「대상: 적 유닛만(지금 쓰는 두 스킬 기준)」 · 221행 「연막탄도 같은 「구역」 스킬이지만 … 「사용 불가」로 남아 있다」)**, 규칙 25 「서버에서만 판정·실행」(340행~ — 구역 항목 「두 모드 모두 유닛 상태 틱 앞이라 같은 틱의 공격 판정이 구역 결과를 본다」), 26, 자연어 설명 19행(「기능이 붙은 것은 열셋」) [확인]
- `GameSystemRules/GameSystemRules_Units.md` — 규칙 19 「피격 표현 큐」(339행), 규칙 20 「원거리 유닛 트레이서」(350행), 규칙 39(554행 — Mushroom 주 타깃 + DoT AoE), 규칙 41 InfernoSpirit(580행), 규칙 44 방어력 감쇄 · 피해 적용 순서(619행), `U-IMPACT-LOCKEDPOINT`(225행 — 발사 시점 목표점 고정 개념) [확인]
- `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` — `NET-DELIVERY-HITSCAN`(264행), **`NET-DELIVERY-PROJECTILE`(268행 — 「서버가 발사 시각, 착탄 시각, 목표 처리 방식과 착탄 위치를 관리한다」)**, `NET-TIME-002`(198행) [확인]
- `GameSystemRules/GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 1(108행 — 타워는 적 유닛만 타겟) · 규칙 12(197행 — 타워 공격 프로필) [확인]
- `TechnicalDesignDocument_Network.md` 「서버 권위 투사체 전송 계약」(30행~) · 「경기 시작 호환 확인」(40행 — 스키마 = 11, 분류 토큰 `6,7,12,17,21,24,26;tower:human`, 「RPC 모양이 바뀌면 스키마를 올린다」) [확인]
- `Assets/SkillVFXDesign.md` V7(145~149행 — 칸 = 구역 VFX, 「빗나가는 궤적은 VFX가 아니라 투사체 연출 코드」) [확인]
- `GameSystemRules/GameSystemRules_AI.md` · `GameSystemRules_AI_Scenario_*.md` — 테스트 관측 조건(15절) [확인 — grep]
- `WORKFLOW.md` [3] · [4](「기존 로직 제거 규칙」 · 「테스트 항목 진단 로그 규칙」) · [6](수집 메뉴 · 회귀 스위트 판정) [확인]
- `.claude/MEMORY.md` 「공통 중요 교훈」 게임 규칙 체크리스트(한 경기 한 종족 · 자기 스킬 건물 · AI는 스킬을 쓰지 않고 탑을 짓지 않음 · 건물은 놓는 즉시 생김 · 싱글/멀티 역할) · 「Regression suite fixtures」 [확인]

---

## 3. 사용자 결정의 출처 (요약 — 결정 표는 Plan 1절)

메인 세션이 2026-11-23 사용자 대화에서 정리해 전달했다 **[전달 — 메인 세션 정리]**.

- 작업 지시 원문: 「연막탄 작업진행해보자 task 문서작성해줘」. 메테오 VFX 프롬프트는 사용자가 Unity에 옮겨 두었다(「유니티에 옮겨놔서 프롬포트는 괜찮을거같고」) — Unity AI 크레딧 소진으로 VFX 제작은 중단, 코드 작업을 진행한다.
- **사용자가 고른 것:** O-7b 방어탑 = **모든 방어탑 포함**(조사 추천 「대포탑만」이 아님) · 수치(임시) 회피 50% · 반경 2 · 지속 6초 · 쿨다운 15 · InfernoSpirit 화상도 막음 · 빗나감 표현(대상 옆에 떨어짐, 발사 순간 서버가 지점 고정, 착탄 이펙트만 · 숫자 없음, 스키마 11 → 12).
- **메인 세션 기본안(사용자에게 보고 예정 — 확정 아님):** O-7a · O-7d · O-S1 · O-S4 · O-S5 · O-S8 · O-S9 · O-S10 · O-S11 · O-S13.
- 진단 로그는 개발용으로 남긴다(공습 Task 사용자 결정 2026-11-18 「그럼 남겨두고」를 기본 방침으로).

---

## 4. 핵심 발견 8가지

1. **구역은 「새 상태 종류 1개 + 접근자 1개」로 얹을 수 있다.** 구역 서비스 `SkillZoneSystem`이 아군 대상을 이미 지원하고(162행 `TargetTeam = targetsAllies ? casterTeam : OppositeTeam(casterTeam)`, 316행 대상 팀 아닌 유닛 제외), 유닛만 센다(`_units.Units` 순회 — 310~323행) [확인]. 5절.
2. **원거리 공격의 피해 길은 셋이다** — ① 서버 권위 투사체(유닛 7종 분류 + 인간 대포탑), ② Hitscan 3종, ③ 즉발 탑 2종 [확인 — 6-1]. 경로마다 끊는 자리 · 화면 표현이 다르다(6~8절).
3. **피해 공통 헬퍼 `IncomingDamage.Apply` 안에서 끊으면 안 된다** — 그 시점엔 이미 특수 핸들러(화상 · 독 · 폭발)가 돌 준비가 끝났고, 같은 헬퍼를 스플래시 · DoT · 스킬도 지나며, 멀티 Hitscan은 C2 코디네이터가 「승인 = 명중인데 writer 미적용」을 불일치로 거절한다 [확인 — 6-2]. → **writer 호출 「이전」에서 끊는다.**
4. **C2 결과 종류에 「회피(`Evaded`)」가 이미 있다** — 생성 검사 · 순서기 · Client 수신 검사 · 화면 필터가 모두 받아 준다. 막힌 곳은 코디네이터 한 곳과 writer 상태 값(「회피」 없음)뿐이다 [확인 — 7절].
5. **투사체 착탄 결과는 「명중 / 대상 소실」 둘뿐이고 수신 검사가 그 둘만 통과시킨다** — 새 값 「회피」를 보내면 옛 빌드는 버린다. 화면은 발사 기록의 `Homing` 칸을 읽지 않고 언제나 대상을 따라간다 [확인 — 8절]. → 발사 알림에 「회피 · 빗나간 지점」을 실어야 하고 **스키마 12**가 된다.
6. **RuneSpire 번개는 대상을 따라가 꽂힌다**(고정 끝점 갈래는 대상 해석 실패 때만) — 빗나감을 보이려면 탑 공격 표현 알림에 빗나감 칸이 필요하다(RPC 모양 변경) [확인 — 9절].
7. **🔴 VineTower는 지금 공격 이펙트가 아예 없다** — `BuildingEffectConfig.asset`의 타워 공격 프로필에 초월(종족 2) 행이 없어 재생하지 않는다(`TowerProfileEmpty`) [확인 — 9-3]. 조사 자료의 「VineTower는 스냅샷 Impact에 한 번 솟는다」는 **프로필이 있을 때의 코드 경로**이고, 지금 화면에 보이는 것은 없다 → VineTower 회피는 「숫자 · HP 변화 없음」과 로그로만 관측된다.
8. **판정 시각 차이(멀티)** — 구역 구성원은 서버 전투 틱(씬 값 0.1초)마다 다시 세고, 투사체 발사는 매 서버 프레임이라 경계를 막 넘은 유닛은 최대 0.1초 이전 상태로 판정된다 [확인 + 판단 — 5-4].

---

## 5. 연막 구역 — 재사용할 장치

### 5-1. 그대로 쓸 수 있는 것 (`Scripts/Application/Services/SkillZoneSystem.cs`) [확인]

| 장치 | 행 | 연막에서 |
|---|---|---|
| 구역 생성 `CreateZone` | 150~ | 그대로. 대상 팀 = `targetsAllies`면 시전 팀(162행) |
| 권위 틱 `Tick` · 구성원 갱신 `UpdateMembership` | 196~ · 227~ | 그대로 — 진입 적용 · 이탈 해제 |
| 「구역 안」 판정 `CollectInside` | 310~323 | 살아 있는 · 대상 팀 · 타일 중심 ↔ 조준점 ≤ 반경. **유닛만** 순회 → 건물 자동 제외(기획 「유닛만」과 일치). 적 유닛은 아군 구역 구성원이 되지 않는다(316행) |
| 같은 스킬 겹침 1회 `RebuildEffects` | 360~ | `(unitId, 팀 · 슬롯)` 집합 — 연막 두 개가 겹쳐도 확률 1번 |
| 사망 정리 · 경기 종료 닫기 | 414~ · 434~ | 그대로 |
| 구성원 알림 `ZoneMembershipChangedClientRpc` | `Scripts/Infrastructure/Network/NetworkSkillController.cs` 460 | 유닛 상태 VFX 붙이기용으로 그대로. 확률은 Client에 보내지 않는다(회피 판정은 서버 전용) |
| 구역 VFX | `Scripts/Presentation/Effects/SkillZoneVfxPresenter.cs` 157 — `Mechanic != PersistentStatusZone`이면 반환(상태 종류는 보지 않음) | 자산 `_zoneVfxPrefab` 칸 재사용, 코드 변경 없음 |
| 유닛 상태 VFX | `Scripts/Presentation/Effects/UnitBuffVfxPresenter.ZoneDriver.cs` | 칸은 있다. 붙일지는 O-S12(VFX 기획) |

### 5-2. 바꿔야 하는 곳 [확인 — 위치 / 제안 — 방법]

1. **새 상태 종류** — `Scripts/Domain/Status/StatusEffectKind.cs` 마지막 멤버 `HealAmpMul = 10`(105행) 뒤에 `EvadeChance = 11`(이름 제안). 정수로 직렬화되므로 맨 뒤에 붙인다. 강도 = 확률 0~1.
2. **실행기 허용 목록** — `Scripts/Application/Skill/PersistentStatusZoneExecutor.cs` 51~60행: `MoveSpeedMul`(강도 ≥ 0 · 유한) 외에는 `Freeze`만 통과하고 나머지는 `return`. 새 종류를 통과시키고 0 ≤ 강도 ≤ 1 검사.
3. **효과 합** — `SkillZoneSystem.cs` 88~92행 `UnitZoneEffect`(`MoveMultiplier` · `CanAttack` 두 칸) → 회피 확률 칸 추가, `ApplyKind`(387~)에 case 추가. 지금 case에 없는 종류는 「구역 효과로 다루지 않는다」 주석 뒤로 빠진다.
   - 다른 출처 겹침: 연막 스킬은 하나뿐 → 같은 팀 · 같은 슬롯 = 같은 스킬이라 1회(규칙 28). 상대 팀 연막은 상대 팀 유닛만 구성원이라 한 유닛에 두 팀 연막이 겹치지 않는다(162 · 316행) `[판단 — 논리]`.
4. **접근자 1개** — `GetEvadeChance(int unitId)`(이름 제안). 기존 두 접근자 `GetMoveSpeedMultiplier`(334) · `CanAttack`(346)처럼 `_effects.Count == 0`이면 바로 0(평상시 비용 없음). 읽는 쪽: `UnitCombatUseCase`(주입 `SetSkillZoneSystem` 516행, 필드 509행) · **`TowerCombatUseCase`(구역 서비스를 주입받지 않는다 — 생성자 100행, grep 0건 → 새 주입 필요)**.
5. **진단 분기** — `Scripts/Application/Services/SkillZoneSystem.Diagnostics.cs` 153~154행(겹침 배율) · 177행(공격 관문 집계)이 종류별 분기 → 새 종류 표기(판정 무관).
6. 상태 시스템(`StatusEffectSystem`)은 건드리지 않는다 — 구역 효과를 따로 드는 이유가 파일 머리 17~21행에 이미 적혀 있다(같은 종류를 통째로 교체하는 상태 시스템과 섞지 않는다) [확인].

### 5-3. 자산 · 셋업 도구 — 7절.

### 5-4. 판정 시각 차이 [확인 + 판단]

- 구역 구성원 재계산: 멀티 `Scripts/Infrastructure/Network/NetworkCombatController.cs` 804행 `TickZones(elapsed)` — 전투 틱 간격 `_attackInterval` 씬 값 **0.1**(`Assets/_Project/Scenes/Game.unity` 45954행; 코드 기본값 61행 0.05). 싱글 `Scripts/Bootstrap/GameBootstrapper.cs` 782행(매 프레임).
- 투사체 발사 처리: 멀티 `NetworkCombatController.cs` 675행 `TickProjectiles`(서버 매 프레임), 싱글 `GameBootstrapper.cs` 694행.
- → 멀티에서 연막 경계를 막 넘은 유닛은 **최대 0.1초** 이전 구성원 상태로 판정될 수 있다 `[판단]`. 서리 감옥의 공격 관문 · 이동 배율과 같은 성격이다(규칙 28 「판정은 서버의 권위 틱마다」).

---

## 6. 원거리 공격 경로 전수

### 6-1. 어떤 공격이 어느 경로인가 [확인]

| 경로 | 유닛 / 탑 | 근거 | 이번 범위 |
|---|---|---|---|
| **① 서버 권위 투사체(유닛)** | Tank(6) · CannonCart(7) · InfernoSpirit(12) · StreamSpirit(17) · FoxMagician(21) · EagleArcher(24) · MushroomBomber(26) | `Scripts/Application/Combat/ProjectileDeliveryContracts.cs` 311~315(`ProjectileUnitTypes`) | **6종 포함 · Mushroom 제외**(메인 기본안 O-7a — 착탄 범위 폭발 + 독, 규칙 39) |
| **① 서버 권위 투사체(탑)** | 인간 CannonTower(AutoTower + Human) | 같은 파일 324(`ProjectileTowerRaces = { Human }`) · 334~335(`IsProjectileTower`) | 포함(사용자 결정) |
| **② 즉발 Hitscan** | Pistoleer(0) · Assault(1) · Sniper(2) | `Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs` 73~75 | 포함(메인 기본안 O-7a) |
| **③ 즉발 탑** | 정령 RuneSpire(번개) · 초월 VineTower(지면 분출) | `Scripts/Application/UseCases/TowerCombatUseCase.cs` 404~ `ExecuteTowerAttack` 즉발 분기(417행 주석 「RuneSpire·VineTower」) | 포함(사용자 결정 「모든 방어탑」) |
| 제외 | TorrentSpirit 파도(진행형 범위 — `SpecialAttackRegistry.cs` 45, `UnitCombatUseCase.cs` 1996 `ReplacesPrimaryAttack`) · 근접(`UnitAttackShadowProfileResolver.cs` 76~ MeleeContact) · BloomFairy 힐(120행 부근 `SeparateHealPipeline`) · 스킬 · 지속 피해 틱 · 스플래시(writer 종류 `Scripts/Application/Combat/IncomingDamage.cs` `IncomingDamageWriter` Direct~TowerShell 9종) | — | 기획 「원거리 단일」 밖 `[판단 — 기획 문구 해석]` |

- **유닛 투사체의 대상은 건물일 수도 있다** — 발사 `LaunchProjectile(ProjectileRecord, IDamageable target, …)`(`UnitCombatUseCase.Projectiles.cs` 579)의 대상 형식이 `IDamageable`이다 [확인]. 회피 판정은 **대상이 유닛**일 때만 한다(연막은 유닛만 보호 — O-S9). 탑은 유닛만 공격한다(Buildings 「방어 타워 시스템」 규칙 1, `Projectiles.cs` 249 · 692 주석) [확인].

### 6-2. 왜 `IncomingDamage.Apply` 안에서 끊으면 안 되는가 [확인]

- 호출 6곳: `UnitCombatUseCase.cs` 2168 · 2535 · 2748 · 3229, `TowerCombatUseCase.cs` 445 · 511.
- ① 유닛 공격은 `ExecuteAttack`(1957~)이 주 타깃 피해(`ApplyDamageToVictim`, 1997~2005) **뒤에** 특수 핸들러를 돌린다(`special.Apply(ctx)` 2023 — Inferno 화상 · Mushroom 독 · 폭발 람다가 2009~2022에서 만들어진다). HP만 막으면 화상이 걸리고 피해 이벤트가 난다.
- ② 같은 헬퍼를 스플래시 · DoT · 스킬도 지나 「원거리 단일」만 고를 정보가 없다.
- ③ 멀티 Hitscan은 C2 코디네이터가 「승인 = 명중 + writer 상태 ≠ 적용」을 거절한다(7-3).
- → **writer(`ApplyAttackDamageCore` / 탑 즉발 피해) 호출 이전에서 끊는다.**

---

## 7. ② Hitscan — 서버 판정 지점 · C2 결과

### 7-1. 피해 경로 [확인]

- **싱글:** `TryAttack`(`UnitCombatUseCase.cs` 794~) → `_pendingHits`(922행 `Add`) → `TickPendingHits`(1304~) → `ApplyAttackDamageCore`(1323 — **`presentationResultKey: default`**, 절대 시계 경로) / 옛 경로 `ApplyAttackDamage`(1344).
- **멀티 서버:** `NetworkCombatController.cs` `DelayedAttackDamage`(1244~) → 공간 승인 `TryDispatchAttackShadow`(1310 · 정의 1866) → `writerAuthorization`(1339) → `ApplyAttackDamageObservedWithPresentationBundle`(1353~1354) → `ApplyAttackDamageCore`.
- `ApplyAttackDamageCore`(1196~1251): 공격자 생존 재확인(1207~1209) → 대상 재탐색 · 생존(1212~1215) → `ExecuteAttack`(1234~1235) → `Applied`. **회피 관문 자리 제안:** 대상 생존 확인 뒤 · `ExecuteAttack` 앞 — 공격자가 Hitscan 분류이고 대상이 유닛일 때만 `[설계안 — 조사]`. 싱글 · 멀티 Hitscan을 한 곳에서 덮는다.

### 7-2. 「회피」 결과가 이미 받아들여지는 곳 [확인]

| 자리 | 행 | 내용 |
|---|---|---|
| `Scripts/Application/Combat/Sequencing/UnitActionContracts.cs` `AttackImpactOutcome` | 354~363 | `None 0 · HitApplied 1 · Miss 2 · Cancelled 3 · **Evaded 4** · Immune 5 · StatusEffectApplied 6` |
| 같은 파일 결과 생성 검사 | 423~430 | Evaded = 피해 0 · 결과 HP ≥ −1 허용 |
| `Scripts/Application/Combat/Sequencing/UnitActionSequencer.cs` | 440~443 | Evaded를 「명중 승인」 계열(`hitOutcome`)로 받는다 |
| `Scripts/Infrastructure/Network/NetworkUnitActionShadowState.cs` Client 수신 검사 | 205~217 | `Enum.IsDefined`로 4 통과, HitApplied가 아니면 피해 0만 요구 |
| `Scripts/Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs` | 120~121 | `IsVisualResult` = HitApplied · StatusEffectApplied만 → Evaded는 HP 숫자 · 피격 반응 없음 |
| `Scripts/Infrastructure/Network/NetworkAttackPresentationBundle.cs` | 41~42 · 113 | 같은 기준(주 결과가 HitApplied가 아니면 사실 목록 0건이어야 한다) |
| 스위트 `Suite/RunUnitActionSelfValidation.cs` | 6939~6940 · 6955 | Evaded 생성 검사 이미 있음 |

→ **RPC 모양 변경이 없다.** 다만 Host와 Client 빌드가 다르면 회피 판정이 한쪽에만 있게 되므로 결국 스키마 12 묶음으로 관리한다(8-3).

### 7-3. 막힌 곳 [확인]

- `Scripts/Infrastructure/Network/UnitAttackShadowCoordinator.cs` 892~918: 승인 `AuthorizedHit` + writer `Applied` → HitApplied(피해 0이면 StatusEffectApplied), 승인 Miss 3종 + 맞는 writer 상태 → Miss, **그 밖은 `InvalidInput`으로 닫는다**(주석 「Miss/HitApplied로 위조하지 않는다」).
- writer 상태 `Scripts/Application/Combat/AttackDamageApplyStatus.cs` 9~29: `Applied 0 · AttackerUnavailable 1 · TargetUnavailable 2 · CombatConditionFailed 3 · AuthorizationUnavailable 4 · PoseUnavailable 5` — 「회피」 값 없음.
- 제안: `AttackDamageApplyStatus.Evaded = 6` + 코디네이터에 「AuthorizedHit + Evaded → `AttackImpactOutcome.Evaded`」 한 갈래 `[설계안 — 조사]`. `ApplyAttackDamageObserved`(1061~)의 승인 입력 인식 목록(1067~1078)은 「승인 입력」 검사라 그대로 둬도 된다 `[판단 — 조사]`.
- **이중 판정 주의:** 투사체 착탄도 같은 writer를 부른다(`ApplyProjectileImpactDamage` 689~ → `ApplyAttackDamageCore` 706~708). 투사체는 발사 때 이미 판정했으므로 Hitscan 관문은 **투사체 착탄 중**(`ProjectileImpactInProgress` — `UnitCombatUseCase.Projectiles.cs` 141, `_projectileImpactAttacker != null`)이면 건너뛰어야 한다. 관문을 「Hitscan 분류 공격자만」으로 거르면 투사체 유닛은 애초에 해당하지 않지만, 두 조건을 함께 두는 편이 안전하다 `[제안 — 문서 담당]`.
- 회피된 공격의 처리(상위 Plan 4절 18 「피해 0 · 대상 유지」): writer를 부르지 않으면 대상 · 공격 순서는 그대로고, 쿨다운은 이미 커밋 때 리셋됐다(`ApplyAttackDamageCore` 1239행 주석 「쿨다운 리셋은 여기서 하지 않음」) [확인].
- `OnEntityAttacked`(피격자 기준 「공격받음」 이벤트)는 구독처가 0건이다(`grep` — 발행 `UnitCombatUseCase.cs` 2183 · 2544, `TowerCombatUseCase.cs` 458 · 518뿐) [확인] → 회피로 이 이벤트가 나지 않아도 반응이 빠지는 장치는 없다.

### 7-4. 화면

- 트레이서 프리셋이 비어 있다 — `Assets/_Project/Resources/Config/UnitEffectConfig.asset` 23 · 32 · 41행 `tracerPreset: {fileID: 0}`(unitType 0 · 1 · 2), `Scripts/Presentation/Unit/UnitView.cs` 3258행 주석 「트레이서 프리셋이 있는 유닛은 투사체 유닛 7종뿐」 [확인]. → 빗나가 보이는 그림이 없다. 총구 VFX · 발사음은 그대로 나고 **숫자만 안 뜬다.**
- 싱글 피격 표현 큐는 공격자별 FIFO이고 「빈 큐에 온 타격 신호는 버린다」(`Scripts/Presentation/Effects/HitPresentationQueue.cs` 60~68 · 119~123 주석) [확인]. 회피된 타격은 피해 이벤트가 없어 큐 항목이 없고 그 타격 신호는 버려진다 → 다음 타격 숫자와 어긋나지 않는지 실기로 볼 항목 `[판단]`(Plan 위험 R-7).

---

## 8. ① 서버 권위 투사체 — 발사 순간 판정

### 8-1. 서버 경로 (`Scripts/Application/UseCases/UnitCombatUseCase.Projectiles.cs`) [확인]

- **유닛 발사** `LaunchProjectile` 579~626: 위치 · 반경 · 속도 → 비행 거리 = 중심 거리 − 반경(585~587) → 착탄 시각을 한 번 고정(589~590) → 공격력 저장(599) → 발사 기록 `ProjectileLaunchPresentation`(607~613, **`homing: true`** 고정) → 검사 실패면 취소(614~619) → `PublishProjectileLaunched`(625). 싱글(`GameBootstrapper.cs` 694)과 멀티 서버(`NetworkCombatController.cs` 675) 공통. 예약은 멀티 `NetworkCombatController.cs` 1661 `TryReserveProjectileLaunch`.
- **탑 발사** `TryLaunchTowerProjectile` 253~(308행 `homing: true`).
- **착탄** `ProcessProjectileFlight` 628~682: 비행 중 대상 추적(633~640, 사라지면 `TargetLost`) → 착탄 시각 도달 → 표면 지점(646) → 대상이 있으면 `ApplyProjectileImpactDamage`(656) → `Applied`면 결과 `Hit`(665~667), **writer가 적용하지 않으면 결과 `TargetLost`**(670~673) → 대상 소실도 `TargetLost`(681~682).
- `ApplyProjectileImpactDamage` 689~715: 탑 공격자면 `ApplyTowerProjectileImpact`(`TowerCombatUseCase.cs` 499~), 유닛이면 `_projectileImpactAttacker` 표시를 세운 채 `ApplyAttackDamageCore`(706~708).
- **판정 자리 제안** `[설계안 — 조사]`: `LaunchProjectile` / `TryLaunchTowerProjectile`에서 「대상이 유닛 · 연막 구성원 · 회피 대상 공격자」면 난수 판정 → 기록에 회피 + 빗나간 지점 저장, 비행은 대상 추적 없이 빗나간 지점까지. 착탄 때 회피면 writer를 **부르지 않고** 새 결과 「회피」로 발행.
  - writer를 통째로 건너뛰므로 **InfernoSpirit 화상도 함께 막힌다**(화상은 writer 안 `special.Apply` — 6-2). 사용자 결정 「화상도 막음」과 일치.
  - 지금 「writer 미적용 = TargetLost」(670~673)라는 관례가 있다 — 회피는 이 갈래로 보내지 말고 별도 결과로 보내야 화면 · 진단이 구별된다 `[문서 담당]`.

### 8-2. 화면 경로 (`Scripts/Presentation/Effects/ProjectilePresentationPresenter.cs`) [확인]

| 장치 | 행 | 연막에서 |
|---|---|---|
| 발사 기록 꺼냄 — 대상 해석 · 첫 끝점 · `BeginServerProjectile`(EagleArcher 화살이 처음 바라볼 끝점 포함) | 281 · 292~299 | 회피면 대상을 해석하지 않고 빗나간 지점을 끝점으로 |
| `Advance` — 매 프레임 대상 화면 위치를 읽어 끝점 갱신, 결과가 오면 서버 지점으로 `ResultBlendSeconds`(71행 = 0.10초) 동안 맞춤 | 335~351 | 회피면 대상을 읽지 않고 고정 끝점 |
| `ResolveTarget` · `TryReadTargetCenter` | 409~ · 430~ | 읽기 실패 → 마지막 위치 고정(대상 소실 선례) |
| `Finish` — 착탄 이펙트는 명중 · 대상 소실 공통으로 결과 지점에서(362~373), 피해 숫자 방출은 `Outcome == Hit`일 때만(380~381) | 354~384 | 「회피」를 「대상 소실처럼 이펙트만, 숫자 없음」으로 처리하면 기획 「숫자 없음 · MISS 없음」과 맞는다 |
| `ServerPoint` | 448~ | 서버는 바닥 좌표만 안다 — 높이는 화면 끝점 높이를 쓴다 |

- **`Homing` 칸은 화면이 읽지 않는다** — `ProjectileDeliveryContracts.cs` 130~131 「2026-10-20 결정으로 모든 투사체가 true」, 화면 grep 결과 RPC 전달(`NetworkCombatController.Projectiles.cs` 228)뿐 [확인].
- **착탄 결과만 바꾸는 안은 비추천** `[설계안 — 조사]`: Host · 싱글은 화면 지연 0초(Presenter 66~68 `ClientPresentationDelaySeconds` · 네트워크 TDD 「표시 시각」)라 결과가 화면 착탄과 같은 프레임에 와서, 투사체가 대상에 닿는 순간 옆으로 튀는 그림이 된다(`Advance`의 0.10초 블렌드). → 발사 때 지점을 알아야 한다(O-S13 발사 시점 판정).

### 8-3. 전송 · 스키마 [확인]

- 착탄 결과 `ProjectileImpactOutcome`(`ProjectileDeliveryContracts.cs` 70~79): `None 0 · Hit 1 · TargetLost 2`. 수신 검사(194~198 `IsValid`)는 Hit · TargetLost만 통과 → 새 값 3을 옛 빌드는 `DroppedInvalid`로 버린다(`NetworkCombatController.Projectiles.cs` 340).
- 발사 알림 `ProjectileLaunchClientRpc`(`NetworkCombatController.Projectiles.cs` 307~310): epoch · id · 공격자 · 대상 · 위치 · 반경 · 발사/착탄 시각 · `homing`. 보내는 곳은 `SendProjectileLaunchRpc`(223~229) 하나이고, **늦은 접속 스냅샷**(113~120 — `CopyActiveProjectileLaunches` `UnitCombatUseCase.Projectiles.cs` 480)도 같은 함수를 지난다 → 발사 기록 구조체와 이 함수에 칸을 더하면 두 경로가 함께 실린다 `[판단]`.
- 두 전송안 `[설계안 — 조사]`: **(가) 발사 RPC에 `evaded` + `missPoint` 추가**(메인 기본안 O-S1) / (나) 기존 칸 재해석(`homing=false` · `targetCenter = 빗나간 지점` · `targetRadius = 0`). 어느 쪽이든 뜻이 바뀌므로 스키마 상향.
- 스키마 상수: `UnitAttackShadowProfileResolver.cs` 45행 `PresentationContractSchemaRevision = 11`, 해시 원문 306행, 핸드셰이크 비교 `NetworkGameFlow.cs` 83행. 투사체 분류 토큰 `ManifestToken`(`ProjectileDeliveryContracts.cs` 355~367)이 해시에 들어간다(`UnitAttackShadowProfileResolver.cs` 307~310 주석).
- 진단 줄: `[PROJECTILE-END]` `side=server` v=2 · `server-tower` v=1 · `side=view` v=3(공용 메모리 서버 권위 투사체 항목 · 네트워크 TDD) [전달]. 회피 수를 칸으로 더하면 해당 v를 올린다 `[설계안 — 조사]`. 화면 표본 `[PROJECTILE-VIEW]`(Presenter 575 · 593).

### 8-4. 빗나간 지점 규칙 `[설계안 — 조사 · 사용자 결정 O-S7 추천 채택]`

`M = C + n × (r + δ) + d × ε` — C = 발사 순간 대상 중심, r = 대상 반경(`_projectileSettings.GetTargetRadius`, 581행), n = 발사 방향에 수직인 단위벡터(좌 / 우는 같은 난수의 다른 비트), d = 발사 방향 단위벡터, δ = 0.3 · ε = 0.5(임시 — 화면 보고 조정).
- 비행 시간은 S → M 거리 ÷ 속도로 다시 계산(지금 식 585~590과 같은 방식) `[설계안 — 조사]`.
- 서버 · 양쪽 화면이 같은 지점을 쓰게 **계산 함수 하나**(표면 지점 `ProjectileSurfacePoint` 725~733 옆)에 둔다 — 공습 「한 함수」 교훈(programmer `skill-system.md` §8) [전달].
- 맵 밖 지점이어도 화면 표현만이라 문제는 적다. 지면 착탄 이펙트를 낼 거면 맵 안으로 잘라 둘지는 프로그래머 판단 `[판단 — 조사]`.

---

## 9. ③ 즉발 탑 — RuneSpire · VineTower

### 9-1. 서버 경로 (`TowerCombatUseCase.cs` 404~477 `ExecuteTowerAttack`) [확인]

- 410~416: 투사체 탑(인간)이면 `TryLaunchTowerProjectile`로 빠진다 — 이후는 즉발 경로(RuneSpire · VineTower).
- 즉발: 표현 좌표(422~428) → 피해 계산(431~440) → **`IncomingDamage.Apply`(445)** → 표현 스냅샷 `OnAttackEffectConfirmed`(447~453, `AppliedDamage` = HP 차이) → `OnEntityAttacked`(458) → `OnEntityDamaged`(464~466) → 사망(469~).
- **판정 자리 제안** `[설계안 — 조사]`: 445행 앞. 회피면 피해 · 두 이벤트를 내지 않고, 표현 스냅샷만 「빗나간 지점」으로 낸다.

### 9-2. 표현 · 전송 [확인]

- 표현 스냅샷 `Scripts/Application/Combat/AttackEffectPresentation.cs` 9~25: `Match · Lifetime · Sequence · AttackerId · TargetId · Visual · Race · Source · Impact · ImpactTime · AppliedDamage · ResultHp · ResultKey` — 빗나감 칸 없음.
- RPC `NetworkCombatController.cs` 80~82 `AttackEffectClientRpc(…)`(보내는 곳 73행) → **칸 추가 = RPC 모양 변경**(같은 스키마 12 묶음).
- RuneSpire 번개 `Scripts/Presentation/Effects/VfxPoolItem.TowerBolt.cs`: 대상이 해석되면 **추적**(160~167 「Tracked」, `LateUpdate` 184~198 매 프레임 끝점 갱신), 해석 실패 · 사망일 때만 스냅샷 `Impact`(+ 가슴 높이) 고정 끝점(168~174 「Untracked」). → 스냅샷 `Impact`만 바꿔서는 번개가 결국 대상 가슴에 꽂힌다. 빗나감이면 고정 끝점 갈래를 강제할 표시가 필요하다.
- 탑 공격 표현 고르기 `Scripts/Presentation/Effects/EffectManager.cs` 1078~1081 주석(번개 = 추적 · 지면 분출 = 「기준점(보통 공격 순간 대상 발밑)에 한 번 재생. 추적하지 않는다」), 기준점 `ResolveSnapshotAnchor` 1169~.

### 9-3. 🔴 VineTower는 지금 공격 이펙트가 없다 [확인 — 조사 자료 정정]

- `Assets/_Project/Resources/Config/BuildingEffectConfig.asset` 15~27행 `_towerAttackProfiles`에 **행이 둘뿐**이다 — 종족 0(인간, `kind: 1` 투사체 · `anchor: 2`), 종족 1(정령, `kind: 2` 번개 · `anchor: 0`). **종족 2(초월) 행이 없다.** 공격 방식 열거형 `Scripts/Presentation/Effects/TowerAttackProfile.cs` `Unset 0 · Projectile 1 · Beam 2 · GroundBurst 3`.
- 행이 없으면 재생하지 않는다(`EffectManager.cs` 1078 주석 「프로필 비어 있음 → 재생하지 않음. outcome = "TowerProfileEmpty"(… 덩굴 타워의 현재 상태)」, 공용 메모리 2026-11-03 탑 공격 프로필 항목) [확인].
- → **VineTower의 빗나감은 지금 화면에서 볼 수 없다**(명중도 이펙트가 없다). 이번 Task에서 VineTower 회피는 「피해 숫자 · HP 변화 없음」과 진단 로그로만 확인한다. 스냅샷 `Impact`를 빗나간 지점으로 주는 것은 나중에 지면 분출 프로필이 생겼을 때를 위한 준비다 `[문서 담당]`.

---

## 10. 결정적 난수 [확인 + 설계안]

- 기존 결정적 난수 `Scripts/Domain/Map/MapRandom.cs` — SplitMix64, 정수만, `Mix64`(123) · `Combine`(145). 머리 주석 5~30행이 .NET 기본 난수(런타임 버전에 따라 수열 변경) · Unity 정적 난수(전역 공유 상태) · 문자열 해시 · 부동소수점을 쓰지 않는 이유를 이미 적고 있다 [확인].
- 「열쇠 해시」 판정 `[설계안 — 조사 · 메인 기본안 O-S10]`: `roll = Mix64(Combine(경기 seed, 공격 열쇠))`, 회피 = `(roll % 10000) < 확률 × 10000`(정수 비교).
  - 투사체 열쇠 = `ProjectileDeliveryKey(epoch, id)`(`ProjectileDeliveryContracts.cs` 38~47 — 두 값 모두 0이 아니면 유효) — 발사마다 유일.
  - 멀티 Hitscan 열쇠 = C2 정규 결과 키 `AttackResultKey`. **싱글 Hitscan은 키가 없다** — `TickPendingHits`가 `presentationResultKey: default`로 부른다(1323~1325) [확인] → (공격자 Id · 대상 Id · 예약 순번(`hit.Sequence`) · 타격 번호) 같은 대체 열쇠 필요 `[설계안 — 문서 담당 보충, 프로그래머 확정]`.
  - 즉발 탑 열쇠 = 표현 스냅샷의 `Match · Lifetime · Sequence`(9-2) 후보 `[제안 — 문서 담당]`.
  - 장점: 다른 시스템이 난수를 더 뽑아도 결과가 바뀌지 않고, 로그의 열쇠 + seed로 판정을 다시 계산할 수 있다.
- 경기 seed: 경기마다 서버가 뽑고 로그에 남긴다(메인 기본안 O-S10). 뽑는 방법(시각 · 기존 seed 재사용 등)은 프로그래머 확정.
- Client는 난수를 뽑지 않는다(스킬 규칙 25) — 결과(회피 여부 · 지점)만 받는다.

---

## 11. 자산 · 셋업 도구 · 회귀 스위트 지금 상태

### 11-1. 자산 `Assets/_Project/Resources/Config/Skills/Skill_Human_SmokeBomb.asset` [확인]

| 칸 | 값 | 행 | 뜻 |
|---|---|---|---|
| `_displayName` | 연막탄 | 16 | |
| `_description` | 「연막 안의 아군 유닛이 원거리 단일 공격을 확률로 피한다.」 | 17~18 | 동작과 맞는 문구 — 그대로 둘 수 있다 |
| `_isAvailable` | 0 | 19 | 사용 불가 |
| `_aimType` | 1 | 20 | 지점 지정 |
| `_cooldown` | 10 | 21 | 자리표시(포격 값) — 사용자 결정 15(Plan U-2) |
| `_mechanic` | 0 | 22 | **타입 A(즉발 범위 피해)** — 구역 타입(4) 아님 |
| `_radius` | 2 | 23 | 사용자 결정 2와 같다(Plan U-2) |
| `_duration` | 0 | 24 | 사용자 결정 6초(Plan U-2) |
| `_totalDamage` | 150 | 25 | 자리표시 피해 → 0 |
| `_statusKind` | 0 | 27 | None |
| `_targetsAllies` | 0 | 29 | 적 대상 → **아군**이어야 함 |
| `_impactDelay` | 0 | 30 | |
| 구역 칸 `_zoneVfxPrefab` · `_statusVfxPrefab` · `_statusVfxScale` · `_statusVfxOffset` | **파일에 없음** | — | 비교: `Skill_Spirit_FrostPrison.asset` 33~36행에 있다. 키가 없으면 기본값으로 읽힌다 |

### 11-2. 셋업 도구 `Assets/Editor/Setup/SupportSkillLoadoutSetup.cs` [확인]

- 연막탄 줄: 367~368행 `PointPlaceholder("Skill_Human_SmokeBomb", h, 3, "연막탄", …)` — 슬롯 인덱스 3(0부터) = 4번 칸.
- `PointPlaceholder`: 200~212행(타입 A · 반경 2 · 피해 150 · 쿨 10 · `IsAvailable = false` · `TargetsAllies = false`).
- 재사용 후보 `PersistentZone(...)`: 255~268행 — **`TargetsAllies = false` 고정**(266행). 주석(250~254)이 「서리 감옥 · 수액 늪」만 말하고, 「새 칸(구역 VFX · 유닛 상태 VFX …)은 이 도구가 쓰지 않는다」(254행).
- `ExpectedAvailableCount = 13`: 98행(옛 값 주석 97행 · 변경 이력 주석 85~96행), 쓰는 곳 519 · 564~566행.

### 11-3. 스위트 — 연막탄을 켜면 바뀌어야 하는 검사 [확인 — grep]

**사용 가능 수 `== 13` — 6곳**

| 파일(`Suite/`) | 행 | 검사 |
|---|---|---|
| `RunUnitActionSelfValidation.AirStrike.cs` | 629~633 | LS-T9 |
| `RunUnitActionSelfValidation.Meteor.cs` | 453~456 (설명 26) | M-T8 |
| `RunUnitActionSelfValidation.SkillZones.cs` | 349~354 (PASS 문구 67) | Z-T10 |
| `RunUnitActionSelfValidation.SupportNewStatuses.cs` | 592~597 | S-T9 |
| `RunUnitActionSelfValidation.SupportRegen.cs` | 517~522 | R-T9 |
| `RunUnitActionSelfValidation.SupportShield.cs` | 356~361 | Sh-T9 |

**연막 「사용 불가」 고정 검사** — `RunUnitActionSelfValidation.SkillZones.cs` 48행(경로 상수) · 305~307행 `"Z-T9 Smoke Bomb must stay unavailable (its evasion is out of scope)."` · PASS 문구 67행 「Smoke Bomb still unavailable (Z-T9)」. 연막탄을 켜면 실패하므로 같은 커밋에서 바꾼다.

**스키마 `== 11` — 8곳:** `ServerProjectiles.cs` 266 · `SupportBuffSkills.cs` 266 · `SkillZones.cs` 346 · `SupportNewStatuses.cs` 599 · `Meteor.cs` 449 · `SupportRegen.cs` 524 · `SupportShield.cs` 354 · `AirStrike.cs` 578. (`ServerProjectiles.cs` 262 · `SupportBuffSkills.cs` 262의 옛 `== 8` 주석 줄은 제외.) 상수 본체 `UnitAttackShadowProfileResolver.cs` 45행.

### 11-4. 스위트 픽스처 방식 [전달 — 선례 근거]

- 새 파일 `RunUnitActionSelfValidation.SmokeBomb.cs`(partial) 후보 — 기존 `SkillZones.cs`(Z-T1~Z-T10) · `Meteor.cs` 형식.
- 전역 스탯 표는 비어 있거나 실제 값일 수 있으므로 **빈 표에서 시작해 필요한 행만 고정하고 끝나면 원래 참조로 되돌린다**(`SupportFixtureStatsScope` — `RunUnitActionSelfValidation.SupportNewStatuses.cs`, 공용 메모리 「Regression suite fixtures」).
- 헤드리스: 실제 UnityEngine DLL을 올리면 `TypeLoadException` — 가짜 `UnityEngine.CoreModule`로 돌릴 수 있다(programmer `skill-system.md` §4 · §7) [전달].
- 소스 문자열 게이트는 주석 처리한 옛 줄을 검사하면 정리 때 같이 바꿔야 한다(같은 메모리 §8) [전달].

---

## 12. 범위 밖 관찰 — 기록만 (손대지 않음 · CLAUDE.md 규칙 6)

- 셋업 도구 `PersistentZone` 주석(250~254행)이 「서리 감옥 · 수액 늪」만 말한다 — 연막 추가 때 같이 고칠 문구 [확인].
- `ProjectileLaunchPresentation.Homing`(`ProjectileDeliveryContracts.cs` 130~131)이 화면에서 쓰이지 않는 칸이다 — 이번 작업과 별개로 정리 대상일 수 있다(보고만) [확인].
- VineTower 공격 프로필 행 부재(9-3)는 이 Task 이전부터의 상태다 — 지면 분출 VFX를 만들지는 이 Task 범위 밖(보고만).

---

## 13. 대조 중 바로잡은 인용 (document-manager, HEAD `43dbf22`)

조사 자료의 값은 지우지 않고 바른 값을 여기 모은다.

| 조사 자료 인용 | 실제 | 비고 |
|---|---|---|
| VineTower 「공격 순간 대상 발밑 고정(`EffectManager.cs` 1135~1137 앵커)」 · 「Impact를 빗나간 지점으로 주면 그 자리에서 솟는다」 | 1135~1137은 번개 분기 주석이다. 지면 분출 설명은 1080행, 기준점 함수는 1169~. **그리고 초월 종족 프로필 행이 없어 지금 VineTower는 아무것도 재생하지 않는다**(9-3) | 핵심 정정 — 테스트 관측 조건이 바뀐다 |
| 투사체 착탄 중 표시 「`ProjectileImpactInProgress` 류 — `UnitCombatUseCase.cs` 2016 사용처」 | 정의 `UnitCombatUseCase.Projectiles.cs` 141(`_projectileImpactAttacker != null`), 사용처 `UnitCombatUseCase.cs` 547 · 2022 · 2197 · 2554 | |
| 특수 핸들러 `UnitCombatUseCase.cs` 1998~2017 · 화상 2003~2017 | 핸들러 조회 1991, 주 타깃 피해 1997~2005, 람다 2009~2022, `special.Apply` 2023 | |
| Presenter `ResolveTarget`/`TryReadTargetCenter` 386~421 · 대상 소실 고정 409~421 · `ServerPoint` 445~452 · `[PROJECTILE-VIEW]` 594 | `ResolveTarget` 409~, `TryReadTargetCenter` 430~, `ServerPoint` 448~, 표본 줄 575 · 593 | 행만 다름 |
| `UnitView.cs` 3256~3257 | `Scripts/Presentation/Unit/UnitView.cs` 3258 | |
| `UnitActionSequencer.cs` 440~445 | 440~443(`hitOutcome`), 444~446 일치 판정 | |
| `UnitAttackShadowCoordinator.cs` 893~918 | 892~918 | |
| `SkillZoneSystem.Diagnostics.cs` 93~130(`[SKILL-ZONE]` 생성 · 구성원) | 95(생성) · 124 · 130(구성원 변경) — 일치 | 확인만 |
| `ZoneMembershipChangedClientRpc` 「`NetworkSkillController.cs` 418 부근 주석」 | 정의 460행 | |

나머지 인용(자산 행, 셋업 도구 행, 스위트 6곳 · 8곳, 실행기 53~60, 구역 시스템 162 · 316 · 334 · 346, 투사체 분류 311~315 · 324 · 334~335, 결과 종류 70~79 · 194~198, 발사 579~626, 착탄 628~682, writer 1196~1251, 즉발 탑 404~477, 번개 160~196, MapRandom 123 · 145, 씬 45954, 틱 804 · 675 · 694 · 782)은 같은 커밋에서 일치했다.

---

## 14. 이번 조사에서 확인하지 않은 것

- 실행 · 실기 — 모든 경로 판단은 코드 읽기다. Unity 컴파일 · 헤드리스 실행 없음.
- 싱글에서 AI가 실제로 어떤 원거리 유닛을 언제 만드는지(시나리오 문서에 이름이 나오는 것만 확인 — 15절).
- 멀티 C2 경로에서 `Evaded` 결과가 Client의 C3 표현 · 진단 관찰기에 어떤 줄로 남는지(수신 검사 통과까지만 확인).
- 회피된 공격이 `[UAS-DIAG]` 계열 진단(공격 회차 관찰기)에서 불일치로 집계되는지.
- `AttackDamageApplyStatus`에 새 값을 더할 때 다른 `switch` · 진단 문자열이 영향을 받는지(전수 grep 미실시 — 프로그래머 확인 항목).
- 늦은 접속 스냅샷 경로의 실기(네트워크 TDD 「늦은 접속」 — 이 경로는 실기로 관측하지 않았다고 이미 적혀 있다).

---

## 15. 테스트 관측 조건 — 게임 규칙 대조 [확인 + 판단]

공용 메모리 「Game-rule checklist」를 이 Task에 대어 본다.

| 규칙 | 연막 테스트에 주는 제약 |
|---|---|
| (1) 한 경기 한 종족 | 연막을 쓰는 쪽은 **인간**이어야 한다. 상대 원거리 공격 종류는 **상대 종족**이 정한다 — 인간 상대 = Hitscan 3종 · Tank · CannonCart + CannonTower, 정령 상대 = InfernoSpirit · StreamSpirit + RuneSpire, 초월 상대 = FoxMagician · EagleArcher(+ 제외 대상 MushroomBomber) + VineTower |
| (2) 스킬은 내 스킬 건물에서만 | 인간 플레이어가 FlightFacility를 놓고 4번 칸 |
| (3) AI는 스킬을 쓰지 않고 탑을 짓지 않는다 | **싱글에서 탑 경로(CannonTower · RuneSpire · VineTower)는 관측할 수 없다** → 탑은 멀티(상대 플레이어가 탑을 짓는다)에서만. 싱글은 AI 원거리 유닛이 내 인간 유닛을 쏘는 장면만 가능 |
| (4) 건물은 놓는 즉시 생긴다 | 절차에 「다 지어지면」을 쓰지 않는다 |
| (5) 싱글 / 멀티 역할 | 싱글 AI 종족은 **무작위**(`GameBootstrapper.cs` 658~664 — `RaceId` 전체 중 `Random.Range`) [확인] → 원하는 공격 종류가 나올 때까지 다시 시작해야 할 수 있다. 멀티는 Host · Client가 각자 종족을 고른다 |

- AI 시나리오 문서에 나오는 원거리 유닛 이름 [확인 — grep, 실제 생산 시점은 실기]: 인간 = Pistoleer · Assault · Sniper · Tank · CannonCart, 정령 = InfernoSpirit · StreamSpirit, 초월 = FoxMagician · EagleArcher · MushroomBomber.
- **모든 탑 종류를 실기로 보려면 멀티 3경기**(상대 인간 · 정령 · 초월)가 필요하다 `[판단]`. VineTower는 이펙트가 없어(9-3) 로그로만 본다.

---

## 16. 변경 이력

| 날짜 | 내용 |
|---|---|
| 2026-11-23 | 2판 — 현재 상태 줄에 Plan 2판 결정 반영 표시만 더했다(조사 본문 무변경) |
| 2026-11-23 | 첫 판 — game-programmer 읽기 전용 조사(HEAD `43dbf22`)를 document-manager가 같은 커밋 파일과 대조해 정리. VineTower 프로필 부재(9-3) · 싱글 Hitscan 결과 키 없음(10절) · 탑 유스케이스 구역 미주입(5-2) · 유닛 투사체 대상이 건물일 수 있음(6-1)을 대조 중 덧붙였다 |

---

## 주요 파일

- `Scripts/Application/Services/SkillZoneSystem.cs` · `SkillZoneSystem.Diagnostics.cs` · `Scripts/Application/Skill/PersistentStatusZoneExecutor.cs` · `Scripts/Domain/Status/StatusEffectKind.cs`
- `Scripts/Application/UseCases/UnitCombatUseCase.cs` · `UnitCombatUseCase.Projectiles.cs` · `TowerCombatUseCase.cs`
- `Scripts/Application/Combat/ProjectileDeliveryContracts.cs` · `AttackDamageApplyStatus.cs` · `AttackEffectPresentation.cs` · `Sequencing/UnitActionContracts.cs` · `Sequencing/UnitActionSequencer.cs`
- `Scripts/Infrastructure/Network/NetworkCombatController.cs` · `NetworkCombatController.Projectiles.cs` · `UnitAttackShadowCoordinator.cs` · `UnitAttackShadowProfileResolver.cs` · `NetworkGameFlow.cs`
- `Scripts/Presentation/Effects/ProjectilePresentationPresenter.cs` · `VfxPoolItem.TowerBolt.cs` · `EffectManager.cs` · `HitPresentationQueue.cs`
- `Scripts/Domain/Map/MapRandom.cs`
- `Assets/_Project/Resources/Config/Skills/Skill_Human_SmokeBomb.asset` · `BuildingEffectConfig.asset` · `UnitEffectConfig.asset`
- `Assets/Editor/Setup/SupportSkillLoadoutSetup.cs` · `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.*.cs`
