# Hexiege — 스탯 레퍼런스

**문서 역할**
- **목적·질문:** 현재 구현된 유닛·건물 값과 단위, 잠정·미확정 범위를 찾는 참조표다.
- **포함:** 기본 스탯·자원·강화 수치와 실제 생산 경로의 타격·주기 및 선택 경계.
- **제외·담당:** 동작은 [유닛 규칙](GameSystemRules/GameSystemRules_Units.md)·[동기화 계약](GameSystemRules/GameSystemRules_UnitCombatSynchronization.md), 개별 수용은 [Matrix](Assets/UnitCombatAssetMatrix.md), 실행 영수증은 기존 Task·[이력](History/ImplementationVerificationHistory.md).
- **원본·참조:** 기본값은 `Assets/_Project/Resources/Config/UnitStatsConfig.asset`, 타격·주기는 실제 UnitFactory 생산 연결·선택 클립의 소비값을 옮긴 미러다. fallback과 생산값 차이를 구분한다.
- **갱신:** 실제 자산/소비 경로가 바뀌면 표·단위·선택 제한과 근거를 제자리 갱신한다. 별도 밸런싱이나 compile/해시 인계 문단은 추가하지 않는다.

> 날짜별 타격 교정·native·실기 인계 원문 및 TorrentSpirit 표현 교정 기록은 [구현·검증 이력](History/ImplementationVerificationHistory.md#stats-handoff)에 보존했다.

**최종 수정일:** 2026-10-14

## 수치 해석 기준

전투 HP·공격력·DoT·힐은 **×10 스케일이 저장 Config와 코드 fallback에 반영된 값**이다. 이동속도·사거리·쿨다운·생산/연구 시간·골드 비용·채굴 수입·비율에는 이 배율을 적용하지 않는다. Inspector 저장값이 코드 기본값보다 우선한다.

수치가 있다는 사실은 밸런싱 확정·전체 실기 수용이 아니다. MistShrine은 공격하지 않는 별도 힐 건물이며 효과 수치는 여전히 임시·밸런싱 미확정이다. 동작은 [건물 규칙](GameSystemRules/GameSystemRules_Buildings.md), 강화 계약·현재 검증 범위는 [강화 규칙](GameSystemRules/GameSystemRules_Upgrade.md)·[현재 상태](PROJECT_STATUS.md), 기존 ×10 적용·수용 경위는 [이력](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-stats-scale-intro)을 따른다.

## 자원 시스템

| 항목 | 값 |
|------|----|
| 시작 골드 (맵 사양 기준) | 무작위 맵의 중립 광산 1~6개에 각각 700/600/500/400/300/200. [무작위 맵 규칙](GameSystemRules/GameSystemRules_RandomMap.md) 규칙 3이 원본이며, 런타임 적용·검증 완료 여부는 PROJECT_STATUS에서 확인한다. |
| 기본 수입 | 0 골드/초 |
| 채굴소 수입 | 10 골드/초 (채굴소 1개당, 전 팩션 동일) |

---

<a id="범위-공격-규칙"></a>
<a id="착탄형-mushroombomber-quakespirit"></a>
<a id="파도형--휩쓸기형-torrentspirit-도끼병"></a>

## 범위 공격 수치 참조

수치는 ×10 스케일이며 판정 의미는 [유닛 특수 공격 규칙](GameSystemRules/GameSystemRules_Units.md)이 원본이다. 같은 타일 소속으로 범위 대상을 근사하지 않는다.

| 유닛 | 현재 튜닝·피해 수치 | 동작 원본에서 확인할 차이 |
|---|---|---|
| MushroomBomber | 직접100, blastRadius 기본1.0 | 원형 범위 DoT는 적 유닛만, 건물은 직접 피해만 |
| QuakeSpirit | 공격력200, radius1.0, splashRatio0.5, 주변 피해100(올림) | 주 타깃100% 한 번, 주변 적 유닛·건물50% |
| BattleAxe | 공격력150, sweepReach0.75, 반각120° | 전방 부채꼴 적 유닛; 주 타깃 중복·건물 AoE 제외 |

방어 감쇄는 직격·스플래시에 적용하고 DoT 틱값에는 적용하지 않는다. Torrent 파도는 발사 offset과 이후 전선 접촉을 구분한다. 현재 수용은 [Matrix](Assets/UnitCombatAssetMatrix.md), 이전 메커니즘·검증 설명 사본은 [이력](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-stats-aoe-copy)에 보존한다.

## 유닛 — 인간계 (Human)

> **시간 컬럼:** 권위 타격·발사·힐 발동 offset과 실제 생산 주기를 초 단위로 구분한다. 콜론 표기를 일괄 환산하지 않고 선택 클립의 저장값·소비 경로를 따른다. 이전 시간 사본은 이력에 보존한다.
> 시간은 권위 행동 회차 시작부터 해당 사건까지의 offset이다. 일반 공격은 권위 피해 예약 시점, TorrentSpirit는 파도 발사 시점(대상별 피해·힐은 그 뒤 전선 접촉), BloomFairy는 힐 발동 시점이다. 차지 유닛(InfernoSpirit·FoxMagician)은 피해·발사 `OnAttackHit`(Inferno 1.5초, Fox 85/30초)와 차지 시작 `OnAttackCharge`(Inferno 5/30초, Fox 1초)를 구분하며, 시간 칸에는 피해·발사 시점을 쓰고 차지 시작을 괄호에 적는다. 투사체 발사·공격 SFX는 `OnAttackHit` 순간이고 차지 시작은 피해 offset이 아니며 로컬 이벤트가 HP를 적용하지 않는다(`GameSystemRules_Units.md` 규칙 17 「차지 유닛 typed marker 계약」). *(2026-10-19 갱신: 종전 「FoxMagician은 피해 `OnAttackHit @ 85/30초`와 charge `OnAttackCharge @ 1초`를 구분하며 로컬 이벤트가 HP를 적용하지 않는다.」)* `초:프레임(30fps)` 마커는 `초 + 프레임/30`으로 환산한다.
> **HP·공격력은 ×10 스케일 값(구현 완료 — config `.asset`에 ×10 커밋 반영(적용에 쓰였던 셋업 스크립트는 역할 종료 후 제거됨)).** 이동속도·사거리·쿨다운·생산시간·골드비용·인구는 불변.

| 유닛 | HP | 공격력 | 공격 사거리 (타일) | 감지 사거리 (타일) | 이동속도 (칸/초) | 타격·발동 시점 (초) | 전체 주기 (초) | 생산 시간 | 골드 비용 | 인구 | 비고 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 권총병 (Pistoleer) | 300 | 60 | 1 | 1 | 0.5 | 0.8 | 2 | 5초 | 50 | 1 | [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| 돌격소총병 (Assault) | 400 | 10 | 2 | 2 | 1 | 0.16666667 | 0.33333334 | 10초 | 100 | 1 | [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| 저격총병 (Sniper) | 300 | 180 | 5 | 5 | 0.25 | 1.7333333 | 3 | 20초 | 200 | 1 | [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| 근접기사 (LittleKnight) | 350 | 40 | 0.5 | 1 | 1 | 0.25, 1.15 | 3.0000002 | 5초 | 50 | 1 | [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| 창병 (SpearMan) | 500 | 100 | 1 | 1.5 | 1 | 0.8333333 | 2 | 10초 | 100 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| 도끼병 (BattleAxe) | 800 | 150 | 0.75 | 1 | 1 | 1.02 | 3.1666667 | 20초 | 200 | 1 | [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| 전차 (Tank) | 1000 | 300 | 3 | 4 | 1 | 0.16666667 | 4 | 30초 | 400 | 1 | [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| 포격수레 (CannonCart) | 500 | 200 | 3 | 4 | 1 | 0.16666667 | 4 | 20초 | 150 | 1 | [현행 감사표](Assets/UnitCombatAssetMatrix.md) |



## 유닛 — 정령계 (Spirit)

| 유닛 | HP | 공격력 | 공격 사거리 (타일) | 감지 사거리 (타일) | 이동속도 (칸/초) | 타격·발동 시점 (초) | 전체 주기 (초) | 생산 시간 | 골드 비용 | 인구 | 비고 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| EmberSpirit | 350 | 60 | 0.5 | 1 | 0.5 | 1 | 2.6666667 | 5초 | 50 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| FlameSpirit | 350 | 30 | 0.5 | 1 | 2 | 0.6666667, 1.1666667, 1.4333334, 1.6666667, 1.9, 2.1 | 3 | 10초 | 100 | 1 | [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| InfernoSpirit | 600 | 250 | 4 | 4 | 1 | 1.5 (피해·발사; charge 0.1667) | 3 | 30초 | 400 | 1 | [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| DustSpirit | 500 | 60 | 0.5 | 1 | 0.5 | 1.04 | 3 | 5초 | 50 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| BoulderSpirit | 800 | 80 | 0.5 | 1 | 0.5 | 1.3333333 | 4 | 15초 | 100 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| QuakeSpirit | 2500 | 200 | 0.5 | 1 | 0.5 | 1.667 | 5 | 30초 | 400 | 1 | [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| TideSpirit | 300 | 70 | 0.5 | 1 | 1 | 1.5 | 3 | 5초 | 50 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| StreamSpirit | 300 | 60 | 3 | 3 | 0.5 | 0.5 | 1.5 | 10초 | 100 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| TorrentSpirit | 1000 | 200 | 3 | 3 | 0.5 | 2.3333333 (파도 발사) | 4 | 30초 | 400 | 1 | [현행 감사표](Assets/UnitCombatAssetMatrix.md) |

## 유닛 — 초월계 (Transcendence)

| 유닛 | HP | 공격력 | 공격 사거리 (타일) | 감지 사거리 (타일) | 이동속도 (칸/초) | 타격·발동 시점 (초) | 전체 주기 (초) | 생산 시간 | 골드 비용 | 인구 | 비고 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| FoxMagician | 200 | 150 | 3 | 3 | 0.5 | 2.8333333 (피해·발사; charge 1) | 4 | 5초 | 70 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| BearGuard | 1000 | 60 | 0.5 | 1 | 1 | 0.43333334 | 1.6666666 | 25초 | 300 | 1 | [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| LionKnight | 500 | 90 | 0.5 | 1 | 2 | 0.6, 1.4333333 | 2 | 15초 | 200 | 1 | [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| RhinoBreaker | 600 | 100 | 0.5 | 1 | 2 | 1.05 | 2 | 20초 | 200 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| EagleArcher | 350 | 60 | 3 | 3 | 1 | 0.1 | Attack 1 / Attack2 0.70000005 (선택 순서 미확인) | 15초 | 150 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| RabbitTrickster | 200 | 100 | 0.5 | 1 | 2 | 0.6666667 | 2 | 5초 | 50 | 1 | [생산 시간 근거](#생산-타임라인-구현-근거) |
| MushroomBomber | 400 | 100 | 2 | 2 | 1 | 1 | 3 | 15초 | 200 | 1 | [현행 감사표](Assets/UnitCombatAssetMatrix.md) |
| BloomFairy | 500 | 0 (공격 불가) | 4 | 4 | 1 | 1 (힐 발동) | 4 (명목: 준비 1 + 발동 후 3) | 20초 | 150 | 1 | [현행 감사표](Assets/UnitCombatAssetMatrix.md) |

---

## 현재 시간값의 근거와 사본 불일치

스탯은 현재 구현값으로 맞추고 밸런싱은 추후 진행한다. 공격 시간은 이미 교정한 생산 연결의 실제 클립·Factory 소비 경로를 기준으로 기록한다. 과거 시간 사본은 [이전 표 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-stats-old-time-column)에 보존하며 현행 표의 타격·주기와 구분한다.

### 생산 타임라인 구현 근거

일반 공격의 타격은 선택된 Attack 클립의 `OnAttackHit` 이벤트를 추출한다. 주기는 선택된 클립 길이로 덮어쓴다. Fox도 SOLO/server/client Factory 생성에서 피해 `OnAttackHit @ 85/30초`를 추출하며 type21 설정 fallback은2.8333333초다. 별도 `OnAttackCharge @ 1초`는 차지 VFX만 시작하고(공격 SFX는 `OnAttackHit` 발사 순간) 피해 offset에 포함하지 않는다. InfernoSpirit도 같은 차지 유닛이며 `OnAttackCharge @ 5/30초`는 피해 offset에 포함하지 않는다(추출 타격은 1.5초 하나). *(2026-10-19 갱신: 종전 「…는 source VFX/SFX만 시작하고 피해 offset에 포함하지 않는다.」)* Bloom은 marker 1초 뒤 **발동 후 쿨다운 3초**를 세므로 명목 주기는 4초이며 클립 길이와 같은 이유로 일반 공격 규칙을 적용한 값이 아니다. Torrent의 2.3333333은 파도 발사이며 실제 피해·힐은 이후 대상 접촉 시점이다.

Fox의 원래 `2:25@30fps`는85/30초이며 이전2.25초 환산값을 현재값으로 사용하지 않는다. 이는 새 밸런싱이 아니다. [복원·검증 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-stats-fox-01)과 [현재 개별 수용](Assets/UnitCombatAssetMatrix.md)을 구분한다.

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

<a id="비시간-수치의-문서설정-충돌-구현-기준-정정-완료"></a>

기본 HP·공격력·사거리·감지·이동·생산시간·비용·인구는 위 현재 표를 따른다. Bloom 공격력0은 힐량200과 별개이고, 공격 연구 증가폭은 현재 기본 공격력에 `Round(8%)`를 적용한다. 이전 문서값과의 대조표·반영 경위는 [수치 사본 정정 이력](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-stats-old-values)에서 확인한다. 이는 설정 변경·밸런싱 확정·새 실기 검증이 아니다.

<a id="유닛별-원문-비고-및-검증-근거"></a>
<a id="pistoleer-원문-비고"></a>
<a id="assault-원문-비고"></a>
<a id="sniper-원문-비고"></a>
<a id="littleknight-원문-비고"></a>
<a id="battleaxe-원문-비고"></a>
<a id="tank-원문-비고"></a>
<a id="cannoncart-원문-비고"></a>
<a id="flamespirit-원문-비고"></a>
<a id="infernospirit-원문-비고"></a>
<a id="quakespirit-원문-비고"></a>
<a id="torrentspirit-원문-비고"></a>
<a id="bearguard-원문-비고"></a>
<a id="lionknight-원문-비고"></a>
<a id="mushroombomber-원문-비고"></a>
<a id="bloomfairy-원문-비고"></a>

## 유닛별 근거 찾기

과거 유닛 비고 원문은 [보존 이력](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-stats-notes-01), 현재 개별 연결·수용·잔여는 [Matrix](Assets/UnitCombatAssetMatrix.md)를 따른다. 같은 원문을 가리키는 유닛별 빈 절은 두지 않는다.

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
