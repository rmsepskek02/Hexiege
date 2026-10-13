# Hexiege — 스탯 레퍼런스

> 전투·경제·연구 수치와 단위를 참조하는 문서다. 상세 동작은 개별 게임 규칙, 현재 구현·검증 상태는 PROJECT_STATUS와 전투 에셋 감사표가 담당한다.
>
> 날짜별 타격 교정·native·실기 인계 원문 및 TorrentSpirit 표현 교정 기록은 [구현·검증 이력](History/ImplementationVerificationHistory.md#stats-handoff)에 보존했다.

**최종 수정일:** 2026-10-13

이 문서는 현재 구현 수치를 사람이 읽도록 정리한 스탯 미러다. 유닛 기본 스탯은 `Assets/_Project/Resources/Config/UnitStatsConfig.asset`, 타격·주기는 실제 UnitFactory 생산 연결과 선택된 Attack 클립의 소비 결과를 기준으로 한다. 사용자 지시에 따라 현재 구현값으로 사본을 맞추며 스탯 밸런싱은 추후 진행한다. 공격 의미는 `GameSystemRules/GameSystemRules_Units.md`, 멀티플레이 동기화 계약은 `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md`, 유닛별 에셋·구현 상태는 `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`를 따른다.

> **2026-08-10 변경:** 초월계 특수 건물 **MistShrine(HealShrine)** 항목에 재설계 확정 내용 반영(물안개 지속 힐 — 아군 유닛+건물 대상). **2026-08-12 구현 완료 / 에디터 싱글플레이 실기 검증 완료 · 멀티 미검증**이며, 회복량·지속시간·쿨다운·범위는 **여전히 밸런싱 미확정**이다(현재 게임에서 도는 값은 전부 임시값이므로 확정 수치로 인용 금지). 아울러 MistShrine은 **방어 타워가 아님**을 명시(Transcendence 방어 타워 = VineTower). 상세: `GameSystemRules/GameSystemRules_Buildings.md` MistShrine 물안개 힐 시스템.

---

> ## ✅ 밸런스 개편(×10 스케일 + 유닛 강화 시스템) — 구현 완료 (기능·멀티플레이 실기 PASS, 2026-07-31)
>
> 이 문서의 **모든 전투 수치(유닛 HP·공격력, 건물 HP, 타워 HP·공격력, DoT 틱값, 힐량)는 ×10 스케일로 재조정된 값**이며, 신규 **유닛 강화(연구소) 시스템**의 방어력·업그레이드 표도 함께 반영되어 있다. **강화 시스템·×10 스케일·연구 패널 UI 모두 구현되어 멀티플레이 실기 테스트를 통과했다.**
>
> - **×10 config 반영:** 전투 수치 ×10은 config `.asset`(UnitStatsConfig/BuildingStatsConfig/SpecialAttackConfig)에 **값이 커밋되어 저장소 기본값이 ×10**이다(코드 폴백 기본값도 ×10, Inspector 값이 코드 기본값보다 우선). ×10 적용에 쓰였던 셋업 에디터 스크립트는 역할 종료 후 제거되어 별도 실행이 필요 없다.
> - 각 유닛 비고의 "구현 완료 / 구현 확정" 표기는 **해당 특수 공격 메커니즘(로직)이 구현·검증되었다는 뜻**이며, 함께 적힌 숫자는 ×10 값이다.
> - 변경 전(old)/후(new) 전 수치 대조, TTK 불변 근거, 미확정 쟁점 확정 내역은 **`_Tasks/2026-07-22/10_08_unit-upgrade-system/BalanceReview.md`** 를 단일 진실 소스로 참조한다.
> - 강화 시스템의 구현 계약·구현 상태·연구 패널 UI 설계(트랙 구조·데미지 공식·연구소 운영·비용·UI)는 **`GameSystemRules/GameSystemRules_Upgrade.md`** 참조.
> - **후속 보류(미완/미검증):** 연구 패널 UI 레이아웃 다듬기·매트릭스 헤더 아이콘·AI 연구 사용 실기·MistShrine 힐(**2026-08-12 구현 완료 / 멀티 미검증 · 밸런싱 미확정**)·싱글플레이 자연회복 실기는 별도 작업(과대 표기 금지).
>
> **불변(×10 하지 않음):** 이동속도(칸/초)·공격/감지 사거리(타일)·공격 쿨다운·생산 시간·연구 시간·모든 골드 비용(생산비·건설비·업그레이드비·연구비)·채굴 수입(10골드/초)·스플래시/특수 비율.

---

## 자원 시스템

| 항목 | 값 |
|------|----|
| 시작 골드 (맵 사양 기준) | 무작위 맵의 중립 광산 1~6개에 각각 700/600/500/400/300/200. [무작위 맵 규칙](GameSystemRules/GameSystemRules_RandomMap.md) 규칙 3이 원본이며, 런타임 적용·검증 완료 여부는 PROJECT_STATUS에서 확인한다. |
| 기본 수입 | 0 골드/초 |
| 채굴소 수입 | 10 골드/초 (채굴소 1개당, 전 팩션 동일) |

---

## 범위 공격 규칙

같은 타일에 유닛이 여러 마리 겹쳐 있는 경우, 공격 타입에 따라 피해 적용 방식이 다르다.
> 아래 피해 수치는 ×10 스케일 값이다(비율·반경은 불변). 방어력 감쇄(신규)는 **직격·스플래시에만** 적용되고(타워→유닛 데미지 포함) **DoT 틱값에는 미적용**이다(구현 완료, `DamageCalculator.ApplyDefense`, `GameSystemRules_Upgrade.md` 참조).

### 착탄형 (MushroomBomber, QuakeSpirit)
- **주 타깃 타일**: 유닛 1마리만 직접(착탄) 피해 적용. 나머지 겹친 유닛은 범위(스플래시) 피해만 적용
- **범위 타일**: 해당 타일의 모든 유닛에게 범위 피해 적용
- **MushroomBomber 구현 확정 (2026-07-19, 수치는 ×10 설계값)**: 판정 범위는 타일 소속이 아니라 **착탄 중심(주 타깃 위치) 기준 월드 좌표 원형 반경**이다(도끼병 부채꼴에서 arc를 뺀 형태). 착탄 중심으로부터 XZ 평면 거리 ≤ `blastRadius`(SpecialAttackConfig, 기본 1.0 = 인접 1칸 거리, 불변)인 **적 유닛 전원**(주 타깃 포함)에게 DoT를 부여한다. **직접 100**은 기존 단일 타깃 공격(`ReplacesPrimaryAttack=false`)이 주 타깃 1마리에게 적용하며(건물 공성 포함), **DoT는 적 유닛만**(건물 제외 — 단 주 타깃이 건물이어도 주변 적 유닛엔 DoT). 아군 무피해. 상세: `GameSystemRules_Units.md` 규칙 38~40. (QuakeSpirit도 이 원형 반경 수집 헬퍼를 재사용해 구현 완료 — 아래 QuakeSpirit 항목 참조.)
- **QuakeSpirit 구현 확정 (2026-07-20, 멀티플레이 로그로 메커니즘 검증 / 수치는 ×10 설계값)**: 착탄형 **즉발** AoE(DoT 아님)다. MushroomBomber와 동일하게 **착탄 중심(주 타깃 위치) 기준 월드 좌표 원형 반경**(`_quakeRadius`, SpecialAttackConfig, 기본 1.0 = 인접 1칸, 불변)으로 판정하되, 감쇠가 아니라 **2단계 고정 배율**이다: **주 타깃 1마리 = 100%(공격력 200)만**(스플래시 제외 — 건물이 주 타깃이면 100%로 공성), **착탄 반경 내 다른 적 유닛 + 적 건물 = 50%**(올림 `CeilToInt(공격력×0.5)`=100, 주 타깃 제외). ⚠️ **MushroomBomber/BattleAxe와 달리 스플래시가 건물도 포함**한다(그쪽 두 유닛의 AoE는 유닛만 — 차별점). 아군 무피해, 서버 권위. 구현: MushroomBomber 원형 반경 수집 헬퍼(`CollectEnemyUnitsInRadius`)를 `internal static`으로 공용화해 유닛 수집에 재사용(MushroomBomber 유닛만 로직 무변경) + 건물 수집 `CollectEnemyBuildingsInRadius` 신설, **유닛/건물 hit-set 분리**(규칙 29 계승). 핸들러 `QuakeAttackBehavior`(`ReplacesPrimaryAttack=false`) + 레지스트리 1줄. 튜닝 `SpecialAttackConfig` `_quakeRadius`/`_quakeSplashRatio`(1.0/0.5, 불변). 상세: `GameSystemRules_Units.md` 규칙 43.

### 파도형 / 휩쓸기형 (TorrentSpirit, 도끼병)
- 범위 내 **모든 유닛**에게 동일 피해 적용 (같은 타일 겹침 여부 무관)
- **도끼병(BattleAxe) 구현 확정 (2026-07-17, 수치는 ×10 설계값)**: 판정 범위는 타일 소속이 아니라 **월드 좌표 전방 부채꼴**이다. 공격자 → 주 타깃 방향(forward) 기준으로 XZ 평면 거리 ≤ `sweepReach`(SpecialAttackConfig, 현재 실기값 0.75, 불변) **AND** 각도 ≤ `sweepArcHalfAngle`(반각, 기본 120°, 불변)인 적 유닛 전원에 동일 피해(공격력 150)를 적용한다. 아군/사망/공격자/주 타깃은 제외하며 건물은 AoE 대상이 아니다(주 타깃일 때만 단일 피해). 상세: `GameSystemRules_Units.md` 규칙 23~27. TorrentSpirit의 현행 구현·검증 상태는 [전투 에셋 감사표](Assets/UnitCombatAssetMatrix.md)를 따른다. **2026-10-13 사본 정리:** 기존 미구현 표기는 뒤의 기존 메커니즘 구현 및 날짜별 구현 이력과 충돌하여 현행 원본 참조로 바꿨다.

---

## 유닛 — 인간계 (Human)

> **시간 컬럼**: 권위 타격·발사·힐 발동 offset과 실제 생산 주기를 분리해 초 단위로 기록한다. `원래 시간 표기`는 과거 사본 보존용이다. 콜론을 일괄 환산하지 않고 현재 연결된 클립의 저장 초 값·소비 경로를 확인했다. EagleArcher의 클립 열거 선택 경계는 아래 구현 근거 절에 명시한다.
> 시간은 권위 행동 회차 시작부터 해당 사건까지의 offset이다. 일반 공격은 권위 피해 예약 시점, TorrentSpirit는 파도 발사 시점(대상별 피해·힐은 그 뒤 전선 접촉), BloomFairy는 힐 발동 시점이다. FoxMagician의 피해 예약 2.25초와 charge VFX marker 1초는 서로 다르며, 모든 `OnAttackHit` 이벤트를 실제 피해 시점과 동일시하지 않는다. `초:프레임(30fps)` 마커는 `초 + 프레임/30`으로 환산한다.
> **HP·공격력은 ×10 스케일 값(구현 완료 — config `.asset`에 ×10 커밋 반영(적용에 쓰였던 셋업 스크립트는 역할 종료 후 제거됨)).** 이동속도·사거리·쿨다운·생산시간·골드비용·인구는 불변.

| 유닛 | HP | 공격력 | 공격 사거리 (타일) | 감지 사거리 (타일) | 이동속도 (칸/초) | 타격·발동 시점 (초) | 전체 주기 (초) | 원래 시간 표기 | 생산 시간 | 골드 비용 | 인구 | 비고 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 권총병 (Pistoleer) | 300 | 60 | 1 | 1 | 0.5 | 0.8 | 2 | 0.8초(주기 2초) | 5초 | 50 | 1 | [원문 비고](#pistoleer-원문-비고) · [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| 돌격소총병 (Assault) | 400 | 10 | 2 | 2 | 1 | 0.16666667 | 0.33333334 | 0:05(주기 미확정) | 10초 | 100 | 1 | [원문 비고](#assault-원문-비고) · [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| 저격총병 (Sniper) | 300 | 180 | 5 | 5 | 0.25 | 1.7333333 | 3 | 1:22(3:00) | 20초 | 200 | 1 | [원문 비고](#sniper-원문-비고) · [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| 근접기사 (LittleKnight) | 350 | 40 | 0.5 | 1 | 1 | 0.25, 1.15 | 3.0000002 | 0:25, 1:15(3:00) | 5초 | 50 | 1 | [원문 비고](#littleknight-원문-비고) · [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| 창병 (SpearMan) | 500 | 100 | 1 | 1.5 | 1 | 0.8333333 | 2 | 0:25(2:00) | 10초 | 100 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| 도끼병 (BattleAxe) | 800 | 150 | 0.75 | 1 | 1 | 1.02 | 3.1666667 | 1:17(3:05) | 20초 | 200 | 1 | [원문 비고](#battleaxe-원문-비고) · [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| 전차 (Tank) | 1000 | 300 | 3 | 4 | 1 | 0.16666667 | 4 | 0:17(4:00) | 30초 | 400 | 1 | [원문 비고](#tank-원문-비고) · [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| 포격수레 (CannonCart) | 500 | 200 | 3 | 4 | 1 | 0.16666667 | 4 | 0:17(4:00) | 20초 | 150 | 1 | [원문 비고](#cannoncart-원문-비고) · [현행 감사표](Assets/UnitCombatAssetMatrix.md) |



## 유닛 — 정령계 (Spirit)

| 유닛 | HP | 공격력 | 공격 사거리 (타일) | 감지 사거리 (타일) | 이동속도 (칸/초) | 타격·발동 시점 (초) | 전체 주기 (초) | 원래 시간 표기 | 생산 시간 | 골드 비용 | 인구 | 비고 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| EmberSpirit | 350 | 60 | 0.5 | 1 | 0.5 | 1 | 2.6666667 | 1:00(2:20) | 5초 | 50 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| FlameSpirit | 350 | 30 | 0.5 | 1 | 2 | 0.6666667, 1.1666667, 1.4333334, 1.6666667, 1.9, 2.1 | 3 | 0.6667초, 1.1667초, 1.4333초, 1.6667초, 1.9000초, 2.1000초 (쿨다운 3.0초) | 10초 | 100 | 1 | [원문 비고](#flamespirit-원문-비고) · [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| InfernoSpirit | 600 | 250 | 4 | 4 | 1 | 1.5 | 3 | 1:15(3:00) | 30초 | 400 | 1 | [원문 비고](#infernospirit-원문-비고) · [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| DustSpirit | 500 | 60 | 0.5 | 1 | 0.5 | 1.04 | 3 | 1:04(3:00) | 5초 | 50 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| BoulderSpirit | 800 | 80 | 0.5 | 1 | 0.5 | 1.3333333 | 4 | 1:10(4:00) | 15초 | 100 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| QuakeSpirit | 2500 | 200 | 0.5 | 1 | 0.5 | 1.667 | 5 | 1:20(5:00) | 30초 | 400 | 1 | [원문 비고](#quakespirit-원문-비고) · [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| TideSpirit | 300 | 70 | 0.5 | 1 | 1 | 1.5 | 3 | 1:15(3:00) | 5초 | 50 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| StreamSpirit | 300 | 60 | 3 | 3 | 0.5 | 0.5 | 1.5 | 0:17(1:15) | 10초 | 100 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| TorrentSpirit | 1000 | 200 | 3 | 3 | 0.5 | 2.3333333 (파도 발사) | 4 | 2:10(4:00) | 30초 | 400 | 1 | [원문 비고](#torrentspirit-원문-비고) · [현행 감사표](Assets/UnitCombatAssetMatrix.md) |

## 유닛 — 초월계 (Transcendence)

| 유닛 | HP | 공격력 | 공격 사거리 (타일) | 감지 사거리 (타일) | 이동속도 (칸/초) | 타격·발동 시점 (초) | 전체 주기 (초) | 원래 시간 표기 | 생산 시간 | 골드 비용 | 인구 | 비고 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| FoxMagician | 200 | 150 | 3 | 3 | 0.5 | 2.25 (피해; VFX 1) | 4 | 2:25(4:00) | 5초 | 70 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| BearGuard | 1000 | 60 | 0.5 | 1 | 1 | 0.43333334 | 1.6666666 | 0:13(1:20) | 25초 | 300 | 1 | [원문 비고](#bearguard-원문-비고) · [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| LionKnight | 500 | 90 | 0.5 | 1 | 2 | 0.6, 1.4333333 | 2 | 0:18,1:13(3:00) | 15초 | 200 | 1 | [원문 비고](#lionknight-원문-비고) · [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| RhinoBreaker | 600 | 100 | 0.5 | 1 | 2 | 1.05 | 2 | 1:05(2:00) | 20초 | 200 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| EagleArcher | 350 | 60 | 3 | 3 | 1 | 0.1 | Attack 1 / Attack2 0.70000005 (선택 순서 미확인) | 0:10(1:00) | 15초 | 150 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| RabbitTrickster | 200 | 100 | 0.5 | 1 | 2 | 0.6666667 | 2 | 0:18(2:00) | 5초 | 50 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| MushroomBomber | 400 | 100 | 2 | 2 | 1 | 1 | 3 | 1:00(3:00) | 15초 | 200 | 1 | [원문 비고](#mushroombomber-원문-비고) · [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| BloomFairy | 500 | 0 (공격 불가) | 4 | 4 | 1 | 1 (힐 발동) | 4 (명목: 준비 1 + 발동 후 3) | 1:00(3:00) ⚠️예외 | 20초 | 150 | 1 | [원문 비고](#bloomfairy-원문-비고) · [현행 감사표](Assets/UnitCombatAssetMatrix.md) |

---

## 현재 시간값의 근거와 사본 불일치

**2026-10-13 사용자 추가 결정:** 스탯은 현재 구현값으로 맞추고 밸런싱은 추후 진행한다. 공격 시간은 이미 교정한 생산 연결의 실제 클립·Factory 소비 경로를 기준으로 기록한다. 표의 `원래 시간 표기`와 아래 날짜 비고는 이전 사본을 추적하는 원문이며 현재값과 다를 수 있다.

### 생산 타임라인 구현 근거

일반 공격의 타격은 선택된 Attack 클립의 `OnAttackHit` 이벤트를 추출한다. 주기는 선택된 클립 길이로 덮어쓴다. Fox는 피해 offset 2.25를 설정에서 유지하고 클립의 1초 이벤트는 충전 VFX용이다. Bloom은 marker 1초 뒤 **발동 후 쿨다운 3초**를 세므로 명목 주기는 4초이며 클립 길이와 같은 이유로 일반 공격 규칙을 적용한 값이 아니다. Torrent의 2.3333333은 파도 발사이며 실제 피해·힐은 이후 대상 접촉 시점이다.

실제 연결·클립 GUID·SHA 및 소비 코드 근거는 [현재 문서 정리 Research](_Tasks/2026-10-13/19_03_main-docs-organization/Research.md#실제-구현값-기준-후속-정정)에 기록했다. 새 Unity 실행·실기 타이밍 측정은 하지 않았다.

| config fallback과 실제 생산값의 차이 | 저장 config | 생산 클립 기준 |
|---|---|---|
| Assault | 타격 0.16666667 / 주기 0.2 | 타격 0.16666667 / 주기 0.33333334 |
| LittleKnight | 타격 0.25, 1.15 / 주기 3 | 타격 0.25, 1.15 / 주기 3.0000002 |
| BattleAxe | 타격 1.02 / 주기 3.05 | 타격 1.02 / 주기 3.1666667 |
| Tank | 타격 4 / 주기 4 | 타격 0.16666667 / 주기 4 |
| CannonCart | 타격 4 / 주기 4 | 타격 0.16666667 / 주기 4 |
| EmberSpirit | 타격 1 / 주기 2.2 | 타격 1 / 주기 2.6666667 |
| StreamSpirit | 타격 0.5 / 주기 1.15 | 타격 0.5 / 주기 1.5 |
| BearGuard | 타격 0.43333334 / 주기 1.2 | 타격 0.43333334 / 주기 1.6666666 |
| LionKnight | 타격 0.6, 1.4333333 / 주기 3 | 타격 0.6, 1.4333333 / 주기 2 |

**남은 구현 선택 경계:** EagleArcher의 controller Attack은 1초·marker0.1이며 Attack2는 0.70000005초·이벤트 없음이다. Factory가 이름에 Attack이 포함된 첫 클립을 선택하므로 실제 `animationClips` 순서를 실행 관측하지 않은 상태에서는 주기를 하나로 확정하지 않는다. 두 경로의 타격은 marker0.1 또는 config fallback0.1이며 동일하다. 사용자 개별 공격 교정·수용을 철회하거나 재시험 요청으로 바꾸는 항목이 아니다.

### 비시간 수치의 문서·설정 충돌 (구현 기준 정정 완료)

사용자 지시에 따라 현재 config를 문서에 반영했다. 설정 자체는 수정하지 않았으며 스탯 밸런스 확정·새 실기 검증을 뜻하지 않는다.

| 항목 | 이전 스탯 사본 | 현재 구현값 |
|---|---|---|
| DustSpirit HP | 400 | 500 |
| BoulderSpirit HP | 900 | 800 |
| BearGuard HP / 생산시간 / 비용 | 2000 / 40초 / 400 | 1000 / 25초 / 300 |
| RabbitTrickster 공격력 | 60 | 100 |
| RabbitTrickster 공격 연구 Lv0~5 / 증가폭 | 60/65/70/75/80/85, +5 | 100/108/116/124/132/140, +8 |

25개 유닛의 기본 HP·공격력·사거리·감지·이동·생산시간·비용·인구를 현재 UnitStatsConfig와 대조했다. BloomFairy의 공격력0은 공격 불가를 표시하며 힐량200과 별개다. 연구 증가폭은 현재 기본 공격력에 `Round(8%)`를 적용하는 기존 구현을 따른다.

## 유닛별 원문 비고 및 검증 근거

아래는 표에서 이관한 기존 비고 원문이다. 날짜별 단계·당시 미완 상태·반복 정정은 보존된 근거이며 현행 상태 도장이 아니다. 현재 구현·실기 범위는 [전투 에셋 감사표](Assets/UnitCombatAssetMatrix.md), 날짜별 인계는 [구현·검증 이력](History/ImplementationVerificationHistory.md#stats-handoff)를 따른다. 타격·주기 불일치를 해결된 것으로 확대하지 않는다.

**[2026-10-10 Sniper 표기 정정 — 원문 보존: 1:73(3:00)]** 30fps frame 부분73은 유효 표기가 아니다. 사용자가 승인한 현재 clip marker52/30초=1:22로 교정한다. Stats/fallback3초→52/30초 구현·native/runtime 수용은 새 Sniper Task 인계 대기다.

### Pistoleer 원문 비고

> **Pistoleer 최신 경기(2026-10-04 23:06 KST) — focused 로그상 긍정·정확 타이밍/육안 OPEN:** Editor22:31–22:34 / device22_56 paired session192c9a08…34711d의 단독235개(Blue119/Red116) 생산·Client 생성; Ready/commit/schedule 각1302, Host 결과1225=Client accepted1225(failure/reject0), 양쪽 필수 표현1020/1020(failure0). Host 이동220148 frames 핵심/fatal0·Client 복제1655 samples invalid/conflict/entrygap/order0·최신 ERROR/FATAL 양쪽0. ROOT는 representativeTypes<2로 INCOMPLETE(errors0), cap64가 전체235 coverage는 아니다. marker/실제 Impact 상세 없어 실제0.8초 offset·2초 주기는 계산 불가, 전체 exact-key join·최신 육안 PASS·full migration/분류 승격 없음. 두 메뉴 PASS와22:09 빌드 preflight만 관찰 기록은 유지한다. Blue 이벤트 확인/의도적 설정 변경 없음 진술과 이전 원인 미확인 해시 관찰은 함께 보존한다. [분석](_Tasks/2026-10-04/21_58_pistoleer-attack-timing-correction/Research.md#6-최신-paired-경기-분석--2026-10-04-2306-kst)

> **2026-10-04 정정:** 기존 Pistoleer `0:80(2:00)`를 0.8초 타격 offset·2초 주기로 구분했다. 30fps marker는 0:24다. Factory 이벤트 자동 추출 때문에 이전 config hit 2초만으로 실제 피해가 2초였다고 단정하지 않는다. 3파일 구현·새 Unit Action 22:04:36 PASS, 새 Root self-validation PASS·22:09 빌드 시도/preflight만 확인·실기 OPEN. [근거](_Tasks/2026-10-04/21_58_pistoleer-attack-timing-correction/Research.md)

30fps 타격 0:24; 생산 연결 정합화; 단독 경기 로그상 긍정·정확 타격/주기·육안 OPEN

### Assault 원문 비고

30fps 타격 5/30초(약 0.1666667초). 현재 clip-derived 주기 0:10(10/30초), 기본 주기 0.2초는 변경하지 않음. 이전 0:17(0:33)은 교정 전 이력이며 0:33 주기 의미는 미확정·임의 환산 금지

### Sniper 원문 비고

clip52/30초 보존·6파일 구현/Main6해시 일치·static4 errors0/actual generator37/37; 보안 modal로 fresh native/ROOT/build 차단, runtime OPEN

### LittleKnight 원문 비고

2히트 공격

### BattleAxe 원문 비고

휩쓸기형 AoE — 월드 좌표 전방 부채꼴(반경 `sweepReach` 실기값 0.75 · 반각 `sweepArcHalfAngle` 120°) 내 모든 적 유닛 동일 피해(공격력 150). 타격 1.1667s(클립 종료 프레임 35f/30fps). 상세: GameSystemRules_Units 규칙 23~27

### Tank 원문 비고

건물에 2배 대미지 ✅구현 완료(비율 불변, `ComputeFinalDamage` 건물 분기)

### CannonCart 원문 비고

건물에 2배 대미지 ✅구현 완료(비율 불변, `ComputeFinalDamage` 건물 분기)

### FlameSpirit 원문 비고

6히트 공격. **위 타격시점 = 사용자 제공 Animation 창 화면·확인에 따른 의도값이자 현재 저장된 에셋 값**: 마커 `초:프레임(30fps)`의 `0:20`→`20/30`, `1:05`→`35/30`, `1:13`→`43/30`, `1:20`→`50/30`, `1:27`→`57/30`, `2:03`→`63/30`초. 사용자 확인 마커 `1:27`은 2026-04 기록의 `1:28`보다 우선한다. `UnitStatsConfig.asset`의 `hitFrameTimes`와 `FlameSpirit_Attack.anim`의 `OnAttackHit`가 위 6개 값으로 일치하며, Unity `[UAS-DIAG]` production timeline/self-validation 및 `[UAS-ROOT-CROSS-AUDIT]` self-validation PASS(Console Error 0)를 확인했다. **2026-09-29 실기 갱신:** 같은 세션 Editor Host/Android Client에서 FlameSpirit 실제 생산과 Host 공격 시작 Ready·shadow commit 확인, 사용자 화면 관찰은 “정상적으로 되는 것으로 보여”. Host 결과 723/실패 0 ↔ Client 723 수락/0 거부, 양쪽 필수 표현 483/483·실패 0. 이 결과·표현 수치는 LittleKnight·Pistoleer도 포함한 경기 전체 집계이며 FlameSpirit 각 회차의 6개 HitIndex offset·정확한 화면/피해 subframe 일치 증거는 아니다. `MigrationRequired`/v2 Complete 미달 유지. 근거: `_Logs/_editor/2026-09-29/RuntimeLog.txt`, `_Logs/2026-09-29/00_12_logcat/RuntimeLog_device.txt`(같은 sharedSessionKey는 FlameSpirit Task 후속 절).

### InfernoSpirit 원문 비고

단일 대상 on-hit DoT(구현 완료, 수치는 ×10 설계값) — 원거리 공격이 주 타깃 1마리에 **직접 250**(기존 단일 피해 `ReplacesPrimaryAttack=false`, 건물 공성 포함) + 그 **주 타깃 1마리에게만** DoT 50/초×3초(총 150). AoE 아님(반경 없음). DoT는 **적 유닛만**(건물 제외 — 건물은 직접 250만), 주 타깃 유닛은 직접+DoT, 아군 무피해. DoT는 규칙 40 초 단위 discrete 틱 재사용(1초 간격·틱당 올림·총량 150 클램프·매초 남은 체력 데미지 텍스트·갱신 리셋·서버 권위). DoT 값은 MushroomBomber(20/3)와 별도 진입점(`ApplyInfernoDot`)으로 분리. 상세: GameSystemRules_Units 규칙 41~42

### QuakeSpirit 원문 비고

착탄형 즉발 AoE(구현 완료, 2026-07-20 멀티플레이 로그로 메커니즘 검증 / 수치는 ×10 설계값) — 주 타깃 1마리 **직접 100%(공격력 200)**(스플래시 제외, 건물이면 100% 공성) + 착탄 **월드 좌표 원형 반경**(`_quakeRadius` 기본 1.0=인접 1칸) 내 **다른 적 유닛·적 건물 50%**(올림 `CeilToInt(200×0.5)`=100, 주 타깃 제외). DoT 아님(즉발). ⚠️ **MushroomBomber/BattleAxe와 달리 스플래시가 건물도 포함**. 아군 무피해, 서버 권위. MushroomBomber 원형 반경 헬퍼(`CollectEnemyUnitsInRadius`, `internal static`) 재사용 + 건물 순회(`CollectEnemyBuildingsInRadius`) 신설, 유닛/건물 hit-set 분리. `SpecialAttackConfig` `_quakeRadius`/`_quakeSplashRatio`(1.0/0.5). **타격 시점 후속 교정(2026-09-14):** production Attack clip `OnAttackHit` 1.667초와 `UnitStatsConfig.hitFrameTimes` 1.667초 정렬 및 Supported 승격, Unity 자동 게이트 PASS. Android 빌드는 시작만 확인했으며 완료·설치·실기 결과는 OPEN. 상세: GameSystemRules_Units 규칙 43

### TorrentSpirit 원문 비고

파도형 이동 AoE(기존 메커니즘 구현, 수치는 ×10 설계값) — 전방으로 이동하는 월드 좌표 직사각형(폭 `waveWidth` 3 × 전방 `waveLength` 3, 타겟 방향) 전선이 닿는 **적 유닛·적 건물 200 피해 / 아군 유닛 100 힐**, 각 대상 1회. special-only(단일 공격 없음). 힐량·파도 파라미터는 SpecialAttackConfig, 피해는 공격력. 아군 힐 = 공격력×0.5(비율 불변). **`2:10`은 사용자가 수정·저장한 Attack 클립의 파도 이펙트/발사 마커 `초:프레임(30fps)`으로, `2+10/30=70/30≈2.3333333초`다.** 이 값은 공격 시작부터 파도 **발사**까지의 시각이며, 적/아군별 피해·회복은 그 뒤 서버 전선이 각 대상에 닿는 시각에 적용된다. 발사 후 전선 이동 시간 `waveTravelTime=0.5초`와 공격 주기 `4:00`(4초)는 별개로 유지한다. 이 시점 교정은 설정·생산 연결 구현 및 Unity 검증 전 **OPEN**이며, 클립은 이번 문서 갱신에서 재확인하지 않았다. 상세: GameSystemRules_Units 규칙 28~31

### BearGuard 원문 비고

30fps 타격 `0:13` = `13/30초`(클립·type 20 설정 저장값 `0.43333334초`). Unity 두 self-validation PASS, 2026-09-30 사용자 육안 수용으로 타격 focused 실기 PASS/CLOSED. 괄호의 설계 주기 `1:20` = `1.666666…초`와 실제 `UnitStatsConfig.attackCooldown=1.2초` 불일치는 별도 OPEN; 이번에는 타격 시각만 교정하고 cooldown은 변경하지 않음. 정확한 BearGuard 단독 marker→피해 offset·공식 CrossAudit/전체 migration 검증은 별도.

### LionKnight 원문 비고

2히트 타격 목표(30fps): `0:18` = `18/30 = 0.6초`, `1:13` = `43/30 ≈ 1.4333333초`. 괄호의 공격 주기 `3:00` = 3초 유지. 클립·type 22 설정 저장 및 Unity/실기 검증은 별도 진행 중.

### MushroomBomber 원문 비고

착탄형 AoE(구현 완료, 수치는 ×10 설계값) — 착탄 대상 1마리 **직접 100**(주 타깃 단일 피해, 건물 공성 포함) + 착탄 중심 **월드 원형 반경**(`blastRadius` 기본 1.0=인접 1칸) 내 **적 유닛** DoT 20/초×3초(총 60). DoT는 **1초 간격 discrete**·**매초 남은 체력 데미지 텍스트**·틱당 올림(총량 60 클램프). 건물은 직접만·DoT 제외(주 타깃 건물이어도 주변 유닛 DoT), 주 타깃 유닛은 직접+DoT, 아군 무피해. 서버 권위. special-only 아님(`ReplacesPrimaryAttack=false`). 생산: SporePatch(식물 1단계). 상세: GameSystemRules_Units 규칙 38~40

### BloomFairy 원문 비고

아군 단일 지정 힐 200 HP/3초 (공격 불가, 기존 회복 구현, 수치는 ×10 설계값). 생산: FloralNursery(식물 2단계). **2026-10-03 디스크 구현 확인: 30fps `1:00`=1.000초에 OnAttackHit 1개, 서버 `HitFrameTimes[0]`도 1.000초; Attack 클립은 3초 loop 유지, Game type27에 정확한 클립 명시 연결 및 production gate 추가.** ⚠️ **힐 주기 = 발동 준비 1.0초 + 발동 후 쿨다운 3.0초 = 실제 주기 4.0초**. **쿨다운 예외**: 다른 모든 유닛은 쿨다운이 발동 준비를 포함한 전체 주기(공격 시작 시 쿨다운 시작, `TryAttack` 패턴 — 위 표기 관례상 `(3:00)`=전체 3초)지만, **BloomFairy만 쿨다운(3.0s)이 힐 발동 준비(1.0s)를 미포함** → 발동 후부터 쿨다운 카운트라 실제 주기 4.0초. 3초 클립 길이는 실제 힐 주기4초와 별개다. **의도된 설계(버그 아님) — 다른 유닛과 같게 되돌리지 말 것.** 회복 발동은 애니 이벤트 `OnAttackHit`이 아니라 상태머신 타이머로 구동(`OnAttackHit`은 연출 전용). HoT diff 틱·종료 시 현재HP 텍스트1회·사망 시 생략 유지. Healer N/A/SeparateHealPipeline, attack VFX preset 미연결. **2026-10-03 후속 구현 확인:** 힐 전용 atomic current-state 복제와 Client 모션 cursor, 새 회차 재생/동일 회차 멱등·late init catch-up·one-shot marker·종료/사망 차단·Frozen/Held 및 Walk 덮어쓰기 방지. Aim은 증거용이고 새 Client Root/Visual offset 회전 없음. HP/HoT writer·원수치 불변. 새 Unit Action 04:52:02 및 Root SelfValidate 04:54:03 자동 PASS, 실제 경기 Analyze 아님. 최종 정적 컴파일 Runtime0/20·Editor0/0·PlayerDevelopment0/20·Release0/24(errors/warnings), 각 exit0; 메뉴 뒤 Console errors0/warnings25. Build And Run1회 클릭 후 Checking prerequisites/Starting Android build 관찰, 완료·설치·실행 미관찰, UI/build polling 중단. 이전 모션 FAIL 후 **실기 재검증 OPEN**, runtime PASS/Supported 승격 아님. 초기03:42 자동 게이트/JDK 및 실기 실패 이력은 기존 Bloom task Research/Plan §1~§8, 최종 파일/증거는 §9 참조. 상세: GameSystemRules_Units 규칙 22·32~37 **이전 경기 당시 분석 경계(2026-10-03 12:36~12:39, 6239bf47…9faf1; 아래 제안·미구현은 당시 상태이며17시 코드/에셋 적용 전 이력):** Bloom8기의 HostStart/HostPlay/ClientPlay50회 exact 회차 일치(missing/extra/conflict/duplicate0), Marker49/MotionEnd45 각각. 서버 Cast49/Applied43, ActualHealed 합4110은 Client HP/text exact 검증이 아니다. Client future-start32/50 offset0 즉시 재생과 실제 Walk-Held-Walk 수신이 확인됐으나 render pose/facing 증거는 부족하다. **힐 전달 성공 / 방향·모션 연결 미해결 — OPEN**. local ROOT PASS·MOVE 실패0은 heal continuity/facing PASS가 아니다. 점진 heal-facing/future-start wait/stationary handoff는 제안·미구현이며 규칙·원수치 변경 없음. 최신 근거는 Bloom task Research/Plan §10, 이전 §9는 이력이다. **최신 승인 정책/코드 인계:** Stats 표의 `1:00(3:00)`는 marker/쿨다운 예외이고 저장 clip `4:00`은 별개다. **현재 승인 힐 표현 정책:** marker1초(30fps `1:00`)는 유지하고 저장 clip4초(`4:00`)로 한다. 원본0~1초 보존, 후반1~3초→1~4초 시간1.5배/tangent 역수 보정·1초 curve 경계 검증을 native AnimationUtility로 수행한다. 일반 힐 endpoint 강제 Play/speed0 hold는 검증 전 삭제 없이 비활성화·주석 보존하고 speed1을 사용한다. 다음 유효 힐 target이 있으면 중간 inactive Walk/Held 없이 새 sequence로 연결하며 힐-only0.15초 blend는 시험값이다. 실제 Frozen/Held/target 없는 대기·사망·경기 종료는 유지한다. 서버 Action 소유권과 활성 priority1 아래 공통 RotateActionTowards를 재사용하고 Client Root/Visual offset writer는 금지한다(NET-AUTH/NET-ROOT). future-start는 IsDue 전 재생하지 않고 late-init catch-up·one-shot marker·sequence/revision/lifetime 멱등·terminal 차단을 유지한다. priority cache는 실제 적용 후 갱신한다. 양 peer 힐러는 config cooldown3을 보존하여 clip4 덮어쓰기의5초 회귀를 차단하며 일반 유닛 clip-derived cooldown은 유지한다. 준비1+발동 후3=cycle4·HoT200/3·서버 writer는 불변이다. **2026-10-03 승인 교정 코드 인계:** 기존 source4파일과 새 Setup.cs/.meta 작성 및 마지막 수정 뒤 정적 컴파일 Runtime0/20·Editor0/0·Dev0/20·Release0/24(errors/warnings) 확인. 서버 Action/priority1의 공통 RotateActionTowards, 연속 유효 target의 inactive 없는 다음 sequence, future-start IsDue 대기·실제 적용 뒤 priority cache 갱신, speed1/기존 snap·endpoint hold 주석 보존, 양 생성 경로 sharedApplyAttackClipCooldown의 힐러 config3 보존 및 future-start/priority-pending/terminal/current-value/cooldown/native-retime-helper fixture 보강이 코드에 반영됐다. native Setup은17:42:31 `[BloomHealSetup] native retime PASS`(changed=True, curves=130, front/tail samples preserved, clip=4s, marker=1s, both heal blends=0.15s)로 완료됐다. 디스크 양 prefab `_healCastBlend: 0.15` 및 OnAttackHit time1 확인. 클립/prefab3파일은 실제 변경이다. 이는 에셋/retime 검증이지 visual PASS가 아니다. **새 코드/에셋 자동 검증 완료:** Unit Action17:43:56의 BLOOM-HEAL-TIMELINE·UAS overall·production·C2·C3 PASS, Root Cross Audit SelfValidate17:44:41 PASS. Console errors0/warnings25(기존 compile20+fixture5)이며 Android warnings0·시각 실기 PASS를 뜻하지 않는다. native 디스크 m_StopTime4/m_SampleRate30/단일 OnAttackHit time1 및 양 prefab blend0.15 확인. **빌드 시도 완료:** File→Build And Run1회 실제 클릭 직후 Importing(iteration2)–Finishing import Settings/Mobile_RPAsset.asset 관찰까지만 확인했다. 이후 Unity UI/Editor/build log polling 및 완료·설치·게임 실행 확인은 하지 않았다. 빌드 성공 추정 없음. Setup·자동 메뉴·빌드 시도는 완료이며, 남은 시각 실기 검증은 OPEN이다. 신규 실기 OPEN, §9~§10은 이전 검증/경기 이력이며 §11은 승인 계획이다. 위 제안·미구현/3초 clip 표기는 당시 구현·분석 이력이며 현재 승인 정책과 구분한다. **이전 실기 분석 — 2026-10-03 18_34 세 observer run(Research/Plan §13):** run별 exact 힐 전달·future-start 실제 재생 대기 seam은 확인됐으나 **facing FAIL(사용자 보고) / 원인 OPEN**이다. B/C는 같은 shared key라도 새 runId/BEGIN/startedAt으로 분리한다. local ROOT PASS·MOVE 실패0·서버 completed HoT 결과는 실제 target-facing/model-pose 또는 Client HP/text 전수 검증이 아니다. payload Aim은 caster 현재 forward이며 target 방향이 아니고 Client 회전 writer도 아니다. target cache 시작1회/회차 중 재조회 없음은 source 제약일 뿐 이번 cache/guard 누락 발생·원인은 미확정이다. 다음 exact 각도/ownership/cache/rendered-forward trace는 별도 승인 필요한 제안·미구현이며 expected yaw는 실제 서버 caster→target 벡터로 계산해야 한다. 모델 offset/Client Root writer fix 금지, 정책·수치 불변. §12 구현/자동 gate/빌드 시도는 보존된 이력이다. **교정 전 두 테스트 분석(2026-10-03 22_48/23_04; Bloom Research/Plan §15):** 두 번째 device 파일의 첫 테스트 전체 prefix는 capture 중복이므로 두 run만 집계한다. exact 전달과 제한된 callback Root 수렴/self drift는 확인됐지만 pixel 방향 PASS는 아니다. 같은 Host clock·exact 회차에서 실제 첫 positive HP 적용이 accepted marker보다 먼저인2사례로 **TIMING EARLY HP FAIL**을 확인했다. 사용자 시각 방향 증상·최종 원인·exact Client HP/pixel timing은 OPEN이다. delta 누적/첫 frame 이전 delta 구조는 강한 원인 의심이며 absolute server dueTime=StartedAt+MarkerOffset은 다음 승인 대상 제안·미구현이다. §14 진단4파일/정적 errors0과 §12 에셋/자동 gate/빌드 시도는 이력으로 보존한다. 정책·스탯·native4초/marker1초(30fps4:00/1:00)·blend0.15초·준비1+post3·HoT200/3 불변. 상세 수치·raw/video 관측 범위는 §15 참조. **§16 승인 교정 구현 인계:** UnitView 한 파일에서 accepted StartedAt+MarkerOffset(1초/30fps1:00) absolute due를 같은 HealNow로 기다리고 yield 뒤 재조회하며 caster/authority/동일 active 회차·finite time guard 후 기존 CastHeal을 단일 실행하도록 교정했다. 이전 dt wait는 사용자 수용 전 주석 비활성화 보존, 기존 diagnostics Due/Now/Elapsed·DispatchRejected 반영. 최종 정적 Runtime0/22·Editor0/0·Development0/22·Release0/26(errors/warnings), 기존 Unit Action overall/Bloom/Production/C2/C3 및 Root Pose Cross Audit SelfValidate 새 실행 PASS는 자동 prebuild gate이지 새 heal coroutine runtime fixture/실기 수용이 아니다. **이번 빌드 시작 시도 확인:** 메인이 Unity File→Build And Run1회 클릭 직후 modal 제목 `Detect Java Development Kit (JDK)`/본문 `Checking Java Development Kit (JDK)...`를 관측했다. 완료·설치·runtime은 미관찰이며 사용자 지시로 이후 모든 UI/build log polling을 중단했다. 빌드 성공 추정 없음. §15 early HP FAIL은 교정 전 이력으로 유지하며 재시험/시각 방향·최종 원인 OPEN이다. 회전·HP/HoT·native clip/marker·정책/스탯 무변경, global cooldown ticker/post3 deadline은 미교정·별도 OPEN이다. 상세 actual source/gate/빌드 경계는 기존 Bloom Research/Plan §16 참조.

---

## 건물 — 인간계 (Human)

> **HP·타워 공격력은 ×10 스케일 값(구현 완료 — config `.asset`에 ×10 커밋 반영(적용에 쓰였던 셋업 스크립트는 역할 종료 후 제거됨)).** 건설 비용·업그레이드 비용·채굴 수입·타워 사거리·쿨다운·힐 범위는 불변.

### 기지 & 채굴소

| 건물 | BuildingType | HP | 건설 비용 | 수입(골드/초) |
|------|-------------|-----|-----------|--------------|
| 본기지 (Castle) | Castle | 2000 | - | - |
| 채굴소 (MiningPost) | MiningPost | 1000 | 50 | 10 |

### 생산 건물

| 건물 | BuildingType | HP | 건설 비용 | 업그레이드 비용 |
|------|-------------|-----|-----------|----------------|
| 근거리A 1단계 (TrainingCamp) | TrainingCamp | 1000 | 100 | 100 |
| 근거리A 2단계 (WarAcademy) | WarAcademy | 2000 | - | 200 |
| 근거리A 3단계 (HumanBarracks) | HumanBarracks | 3000 | - | - |
| 총기류 1단계 (Gunsmith) | Gunsmith | 1000 | 100 | 100 |
| 총기류 2단계 (Armory) | Armory | 2000 | - | 200 |
| 총기류 3단계 (WeaponForge) | WeaponForge | 3000 | - | - |
| 탈것류 1단계 (Garage) | Garage | 1000 | 200 | 300 |
| 탈것류 2단계 (VehicleBay) | VehicleBay | 2000 | - | - |

### 방어 건물

| 건물 | BuildingType | HP | 건설 비용 | 공격력 | 공격 사거리 | 공격 쿨다운 |
|------|-------------|-----|-----------|--------|------------|------------|
| 자동포탑 (CannonTower) | AutoTower | 500 | 150 | 150 | 4.0 | 5.0s |

### 특수 건물

| 건물 | BuildingType | HP | 건설 비용 | 힐량 | 효과 |
|------|-------------|-----|-----------|------|------|
| 비행시설 (FlightFacility) | FlightFacility | 1000 | 200 | - | - |
| 기술연구소 (TechnicalLaboratory) | Research | 1000 | 200 | - | 유닛 강화(연구소) — GameSystemRules_Upgrade.md |

---

## 건물 — 정령계 (Spirit)

### 기지 & 채굴소

| 건물 | BuildingType | HP | 건설 비용 | 수입(골드/초) |
|------|-------------|-----|-----------|--------------|
| 본기지 (SpiritNexus) | Castle | 1500 | - | - |
| 채굴소 (ManaRift) | MiningPost | 500 | 50 | 10 |

### 생산 건물

| 건물 | BuildingType | HP | 건설 비용 | 업그레이드 비용 |
|------|-------------|-----|-----------|----------------|
| 불 1단계 (FireSpire) | FireSpire | 500 | 75 | 200 |
| 불 2단계 (BlazeConduit) | BlazeConduit | 1000 | - | 400 |
| 불 3단계 (InfernoCore) | InfernoCore | 4000 | - | - |
| 물 1단계 (AquaSpring) | AquaSpring | 500 | 75 | 200 |
| 물 2단계 (TidalNexus) | TidalNexus | 1000 | - | 400 |
| 물 3단계 (OceanicHeart) | OceanicHeart | 4000 | - | - |
| 땅 1단계 (StoneMound) | StoneMound | 500 | 75 | 200 |
| 땅 2단계 (TerraForge) | TerraForge | 1000 | - | 400 |
| 땅 3단계 (GaeaSanctum) | GaeaSanctum | 4000 | - | - |

### 방어 건물

| 건물 | BuildingType | HP | 건설 비용 | 공격력 | 공격 사거리 | 공격 쿨다운 |
|------|-------------|-----|-----------|--------|------------|------------|
| 방어포탑 (RuneSpire) | AutoTower | 1500 | 200 | 150 | 4.0 | 3.5s |

### 특수 건물

| 건물 | BuildingType | HP | 건설 비용 | 힐량 | 효과 |
|------|-------------|-----|-----------|------|------|
| 스킬건물 (MagicSpirit) | MagicBuilding | 1000 | 200 | - | - |
| 연구건물 (AstronomicalSpirit) | Research | 1000 | 200 | - | 유닛 강화(연구소) — GameSystemRules_Upgrade.md |

---

## 건물 — 초월계 (Transcendence)

### 기지 & 채굴소

| 건물 | BuildingType | HP | 건설 비용 | 수입(골드/초) |
|------|-------------|-----|-----------|--------------|
| 본기지 (ElderTree) | Castle | 3000 | - | - |
| 채굴소 (FungalNode) | MiningPost | 1500 | 100 | 10 |

### 생산 건물

| 건물 | BuildingType | HP | 건설 비용 | 업그레이드 비용 |
|------|-------------|-----|-----------|----------------|
| 동물A 1단계 (PrimalAltar) | PrimalAltar | 1500 | 125 | 200 |
| 동물A 2단계 (PrimalDen) | PrimalDen | 3000 | - | 300 |
| 동물A 3단계 (PrimalSanctuary) | PrimalSanctuary | 4000 | - | - |
| 동물B 1단계 (FeralAltar) | FeralAltar | 1500 | 125 | 200 |
| 동물B 2단계 (FeralDen) | FeralDen | 3000 | - | 300 |
| 동물B 3단계 (FeralSanctuary) | FeralSanctuary | 4000 | - | - |
| 식물 1단계 (SporePatch) | SporePatch | 1500 | 125 | 200 |
| 식물 2단계 (FloralNursery) | FloralNursery | 3000 | - | - |

### 방어 건물

| 건물 | BuildingType | HP | 건설 비용 | 공격력 | 공격 사거리 | 공격 쿨다운 |
|------|-------------|-----|-----------|--------|------------|------------|
| 방어포탑 (VineTower) | AutoTower | 1000 | 175 | 150 | 4.0 | 5.0s |

### 특수 건물

| 건물 | BuildingType | HP | 건설 비용 | 힐량 | 효과 |
|------|-------------|-----|-----------|------|------|
| 힐 건물 (MistShrine) | HealShrine | 500 ※ | 100 ※ | **미확정** ※ | 물안개 지속 힐 — 아군 유닛+건물 회복 (**2026-08-12 구현 완료 / 멀티 미검증 · 회복량·지속·쿨다운·반경은 임시값**) |
| 스킬건물 (WillowShrine) | MagicBuilding | 1500 | 200 | - | - |
| 연구건물 (AncientGrove) | Research | 1500 | 200 | - | 유닛 강화(연구소) — GameSystemRules_Upgrade.md |

> **※ MistShrine 재설계 (2026-08-10 확정) — 위 수치는 밸런싱 재검토 대상이다.**
> MistShrine은 **방어 타워가 아니라 공격하지 않는 별도 힐 건물**이다(Transcendence 방어 타워는 위 §방어 건물의 **VineTower**).
> 2026-08-10 재설계로 동작이 확정되었다: 시전하면 **건물 중심 고정 원형 범위**에 물안개가 깔리고, 지속되는 동안
> **범위 안의 아군 유닛 + 아군 건물**(시전 건물 자신·Castle 포함)이 **1초 단위 discrete 틱**으로 회복된다.
> 범위를 벗어나면 즉시 끊기는 **아우라 방식**이고, **물안개 지속시간 < 쿨다운**(다운타임 존재)이며, 시전 비용은 없다.
> 물안개끼리는 **중첩되지 않고**(가까운 건물 우선, 거리 동률이면 건물 Id가 작은 쪽), 연구소 **자연회복과는 별개 효과로 중첩 적용**된다.
>
> **밸런싱 미확정 항목:** 회복량 · 물안개 지속시간 · 쿨다운 · 범위 반경 · 회복 텍스트 표시 주기(임시 3초).
> 표의 HP 500 / 건설비 100, 그리고 이전 표기였던 **10 HP/s(범위 3)** 는 **재설계 이전 값에 ×10만 적용한 수치**이므로
> 재설계 후에도 유효한지 밸런싱 단계에서 재확정한다. 상세 규칙:
> `GameSystemRules/GameSystemRules_Buildings.md` — **MistShrine 물안개 힐 시스템**(규칙 16 미확정 표).

---

# 유닛 강화(연구소) 시스템 — 스탯 (구현 완료 — 멀티플레이 실기 PASS)

> 아래 강화 스탯·트랙은 **연구소(Research 건물)로 골드·시간을 들여 팀 단위로 유닛을 강화**하는 신규 시스템의 값이며 **구현 완료(멀티플레이 실기 PASS, 2026-07-31)**. 트랙 구조·데미지 공식·연구소 운영·비용·연구 패널 UI·구현 상태(후속 보류 포함)의 단일 소스는 **`GameSystemRules/GameSystemRules_Upgrade.md`**, 변경 전/후 대조는 `_Tasks/2026-07-22/10_08_unit-upgrade-system/BalanceReview.md` 참조.
>
> **강화 스탯 3종 + 초월 전용 1종:** 공격력 / 방어력(신규) / 이동속도 + 자연회복(초월계 전용). 최대 HP는 강화 대상이 아니다.
> **적용 방식:** 팀별 연구 레벨에 따른 배율을 데미지·이동을 실제로 쓰는 순간에 곱한다(이미 전장에 나온 유닛도 연구 완료 즉시 소급 강화).

## 방어력 (신규 스탯)

- **전 유닛 기본 방어력 0.** 오직 연구로만 올린다(연구 전에는 방어력 0 → 감쇄 0% → 기존과 완전히 동일).
- **데미지 공식:** `데미지 = Max(1, Round(공격력 × (1 − 방어력 / (방어력 + 120))))` — 최소 1 보장(floor), 감쇄율 하드캡 60~65% 안전장치 포함.
- 방어력 감쇄는 **직격 · 스플래시에만 적용**하고(타워→유닛 데미지 포함) **DoT 틱값에는 미적용**한다(2026-07-23 확정 — 기존 "DoT에도 적용"을 뒤집음).
- 방어력 트랙 레벨 = 0/8/16/24/32/40 (아래 §방어력 트랙).

## 공격력 업그레이드 표 (유닛별, Lv1~5)

> 레벨당 증가폭 = `Round(공격력 × 8%)`의 **고정 정수 등차**. Lv0 = 위 유닛 표의 ×10 기본 공격력, Lv5 ≈ ×1.40. ×10 스케일 덕분에 죽은 레벨(증가폭 0)·불규칙 증가폭이 0건이다.
> **주의:** 공격력 연구는 **고정 DoT 틱값(MushroomBomber 20/s, InfernoSpirit 50/s)에는 반영하지 않는다.** QuakeSpirit 스플래시(공격력×0.5)·TorrentSpirit 파도(=공격력)는 공격력을 쓰므로 함께 커진다. 힐량은 아래 §힐량 트랙.

### 인간계
| 유닛 | Lv0 | Lv1 | Lv2 | Lv3 | Lv4 | Lv5 | 증가폭 |
|------|-----|-----|-----|-----|-----|-----|:-:|
| Pistoleer | 60 | 65 | 70 | 75 | 80 | 85 | +5 |
| Assault | 10 | 11 | 12 | 13 | 14 | 15 | +1 |
| Sniper | 180 | 194 | 208 | 222 | 236 | 250 | +14 |
| LittleKnight (2히트) | 40 | 43 | 46 | 49 | 52 | 55 | +3 |
| SpearMan | 100 | 108 | 116 | 124 | 132 | 140 | +8 |
| BattleAxe | 150 | 162 | 174 | 186 | 198 | 210 | +12 |
| Tank | 300 | 324 | 348 | 372 | 396 | 420 | +24 |
| CannonCart | 200 | 216 | 232 | 248 | 264 | 280 | +16 |

### 정령계
| 유닛 | Lv0 | Lv1 | Lv2 | Lv3 | Lv4 | Lv5 | 증가폭 |
|------|-----|-----|-----|-----|-----|-----|:-:|
| EmberSpirit | 60 | 65 | 70 | 75 | 80 | 85 | +5 |
| FlameSpirit (6히트) | 30 | 32 | 34 | 36 | 38 | 40 | +2 |
| InfernoSpirit | 250 | 270 | 290 | 310 | 330 | 350 | +20 |
| DustSpirit | 60 | 65 | 70 | 75 | 80 | 85 | +5 |
| BoulderSpirit | 80 | 86 | 92 | 98 | 104 | 110 | +6 |
| QuakeSpirit | 200 | 216 | 232 | 248 | 264 | 280 | +16 |
| TideSpirit | 70 | 76 | 82 | 88 | 94 | 100 | +6 |
| StreamSpirit | 60 | 65 | 70 | 75 | 80 | 85 | +5 |
| TorrentSpirit | 200 | 216 | 232 | 248 | 264 | 280 | +16 |

### 초월계
| 유닛 | Lv0 | Lv1 | Lv2 | Lv3 | Lv4 | Lv5 | 증가폭 |
|------|-----|-----|-----|-----|-----|-----|:-:|
| FoxMagician | 150 | 162 | 174 | 186 | 198 | 210 | +12 |
| BearGuard | 60 | 65 | 70 | 75 | 80 | 85 | +5 |
| LionKnight (2히트) | 90 | 97 | 104 | 111 | 118 | 125 | +7 |
| RhinoBreaker | 100 | 108 | 116 | 124 | 132 | 140 | +8 |
| EagleArcher | 60 | 65 | 70 | 75 | 80 | 85 | +5 |
| RabbitTrickster | 100 | 108 | 116 | 124 | 132 | 140 | +8 |
| MushroomBomber | 100 | 108 | 116 | 124 | 132 | 140 | +8 |

## 방어력 트랙 (전 유닛 공통, K=120)

| 레벨 | 방어력 | 감쇄율 = 방어/(방어+120) | 실효 HP 배율 = 1/(1−감쇄) |
|------|:-:|:-:|:-:|
| Lv0 | 0 | 0% | ×1.000 |
| Lv1 | 8 | 6.25% | ×1.067 |
| Lv2 | 16 | 11.76% | ×1.133 |
| Lv3 | 24 | 16.67% | ×1.200 |
| Lv4 | 32 | 21.05% | ×1.267 |
| Lv5 | 40 | 25.00% | ×1.333 |

> Lv0(방어 0)이면 감쇄 0% → 기존과 완전히 동일(하위호환). Lv5 감쇄 25% = 실효 HP +33%로 공격력 Lv5(+40%)와 대략 대칭.

## 이동속도 트랙 (전 유닛 공통 배율)

| 레벨 | Lv0 | Lv1 | Lv2 | Lv3 | Lv4 | Lv5 |
|------|:-:|:-:|:-:|:-:|:-:|:-:|
| 이동속도 배율 | ×1.000 | ×1.064 | ×1.128 | ×1.192 | ×1.256 | ×1.320 |

> 레벨당 +6.4%p, Lv5 +32%. 이동속도 기본값(칸/초)은 유닛 표대로 불변이며, 이 배율은 연구로만 붙는다.

## 힐량 트랙

**BloomFairy 힐 (초월 식물 그룹의 공격력 트랙을 따름, 증가폭 +16/Lv):**

| 레벨 | Lv0 | Lv1 | Lv2 | Lv3 | Lv4 | Lv5 |
|------|:-:|:-:|:-:|:-:|:-:|:-:|
| BloomFairy 힐(HP/3초) | 200 | 216 | 232 | 248 | 264 | 280 |

**TorrentSpirit 아군 힐 (= 물 공격력 × 0.5):**

| 레벨 | Lv0 | Lv1 | Lv2 | Lv3 | Lv4 | Lv5 |
|------|:-:|:-:|:-:|:-:|:-:|:-:|
| 물 공격력(TorrentSpirit) | 200 | 216 | 232 | 248 | 264 | 280 |
| TorrentSpirit 아군 힐(×0.5) | 100 | 108 | 116 | 124 | 132 | 140 |

## 자연회복 (초월계 전용)

> 초월계 전 유닛 대상 상시 HoT(전투 중 포함, 최대 HP 미변경·Heal 클램프). 기존 힐/DoT 틱 인프라 재사용.

| 레벨 | Lv0 | Lv1 | Lv2 | Lv3 | Lv4 | Lv5 |
|------|:-:|:-:|:-:|:-:|:-:|:-:|
| 자연회복(HP/초) | 0 (미연구) | 3.0 | 6.0 | 9.0 | 12.0 | 15.0 |

> 레벨당 +3.0 HP/s.

## 연구 비용·시간 (표준 트랙, 스탯 1종 Lv1~5)

| 레벨 | 골드 | 연구 시간 |
|------|:-:|:-:|
| Lv1 | 80 | 15초 |
| Lv2 | 120 | 25초 |
| Lv3 | 180 | 35초 |
| Lv4 | 260 | 50초 |
| Lv5 | 360 | 70초 |
| **합계** | **1,000** | — |

**그룹 비용 배율 (효과 동일, 비용만 배율):** 초월 동물 ×2.0(합 2,000) / 자연회복(초월 공용) ×2.5(합 2,500) / 초월 식물 포함 그 외 전부 ×1.0(합 1,000). 연구 시간은 배율과 무관하게 표준 유지. 연구소 건설비 200골드(불변). 상세: `GameSystemRules/GameSystemRules_Upgrade.md`.
